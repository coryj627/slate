// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import Combine
import SwiftUI

/// Zoom/pan state for one canvas (shared with #520's viewport
/// commands). Canvas→view mapping: `view = (canvas − offset) × scale`.
@MainActor
final class CanvasViewport: ObservableObject {
    static let minScale: CGFloat = 0.1
    static let maxScale: CGFloat = 4.0
    static let zoomStep: CGFloat = 1.25

    @Published var scale: CGFloat = 1.0
    @Published var offset: CGPoint = .zero
    /// Viewport-follows-selection (t2 decision 6): default ON; the
    /// auto-pan itself is silent (t0 §1.5 no-doubling).
    @Published var followSelection: Bool = true
    /// Last laid-out renderer size — lets fit/zoom-to-selection math
    /// run from commands without reaching into the view (#520).
    var viewSize: CGSize = CGSize(width: 800, height: 600)

    func clampScale(_ value: CGFloat) -> CGFloat {
        min(Self.maxScale, max(Self.minScale, value))
    }

    /// Zoom keeping the view center stationary (#520).
    func zoom(by factor: CGFloat) {
        setScale(scale * factor)
    }

    func setScale(_ newScale: CGFloat) {
        let clamped = clampScale(newScale)
        // Keep the canvas point at the view center fixed.
        let centerCanvas = CGPoint(
            x: offset.x + viewSize.width / (2 * scale),
            y: offset.y + viewSize.height / (2 * scale))
        scale = clamped
        offset = CGPoint(
            x: centerCanvas.x - viewSize.width / (2 * clamped),
            y: centerCanvas.y - viewSize.height / (2 * clamped))
    }

    /// Fit a canvas-space rect (with padding) into the view (#520:
    /// Fit Canvas ⇧1 / Zoom to Selection ⇧2).
    func fit(rect: CGRect, padding: CGFloat = 40) {
        guard rect.width > 0 || rect.height > 0 else { return }
        let padded = rect.insetBy(dx: -padding, dy: -padding)
        let fitScale = min(
            viewSize.width / max(padded.width, 1),
            viewSize.height / max(padded.height, 1))
        scale = clampScale(fitScale)
        offset = CGPoint(
            x: padded.midX - viewSize.width / (2 * scale),
            y: padded.midY - viewSize.height / (2 * scale))
    }

    /// The zoom level as the announced/inspectable percentage (#520).
    var zoomPercent: Int { Int((scale * 100).rounded()) }
}

/// The visual canvas surface (Milestone T, #367) — read-only in this
/// slice, selection-synced, and **not an opaque view** (interview
/// decision 3): every visible card publishes an
/// `NSAccessibilityElement` with a screen-coordinate frame that tracks
/// pan/zoom, so VoiceOver works *on* the visual surface and Voice
/// Control "Show numbers" can target cards.
///
/// - **Windowing (§K without stranding VO):** elements and layers
///   materialize for the viewport plus a one-viewport margin on every
///   side. Keyboard/VO traversal is never a dead end: selection moves
///   in reading order (navigator), selection change auto-pans
///   (keyboard selection ALWAYS scrolls into view — WCAG 2.4.11), and
///   the pan materializes the next window.
/// - **Focus visibility (WCAG 2.4.7):** the selection indicator draws
///   in a screen-space overlay at a minimum 3 pt thickness — never a
///   scaled sub-pixel ring at low zoom. View focus (first responder)
///   keeps the system focus ring, distinct from card selection.
/// - **Z-order:** overlapping cards hit-test topmost-by-document-order
///   (the t1 tiebreak).
/// - **Reduce Motion:** all layer updates run with implicit animations
///   disabled; pans/zooms are instant transforms.
struct CanvasRendererView: NSViewRepresentable {
    @EnvironmentObject var appState: AppState
    let document: CanvasDocument

    func makeNSView(context: Context) -> CanvasRendererNSView {
        let view = CanvasRendererNSView()
        view.configure(document: document, appState: appState)
        return view
    }

    func updateNSView(_ view: CanvasRendererNSView, context: Context) {
        view.configure(document: document, appState: appState)
        view.refreshFromDocument()
    }
}

/// One card's AX proxy on the renderer. Frames are set in screen
/// coordinates by the owning view on every pan/zoom/resize.
final class CanvasCardAXElement: NSAccessibilityElement {
    var nodeId: String = ""
    var onPress: (() -> Void)?
    /// Red-team #367 F5 (t3 non-stranding): VO next/prev sets AX focus
    /// on the element; syncing it into `CanvasSelection` drives the
    /// auto-pan that materializes the next window, so pure VO
    /// traversal never dead-ends at the window edge. Silent — VO
    /// already speaks the element it landed on.
    var onAXFocus: (() -> Void)?
    private var axFocused = false

    override func accessibilityPerformPress() -> Bool {
        onPress?()
        return onPress != nil
    }

    override func isAccessibilityFocused() -> Bool { axFocused }

    override func setAccessibilityFocused(_ focused: Bool) {
        axFocused = focused
        if focused { onAXFocus?() }
    }
}

@MainActor
final class CanvasRendererNSView: NSView {
    private(set) weak var appState: AppState?
    private(set) var document: CanvasDocument?

    private let contentLayer = CALayer()
    private let edgeLayer = CAShapeLayer()
    /// Per-color edge overlays (#370); keyed by raw color string.
    private var coloredEdgeLayers: [String: CAShapeLayer] = [:]
    /// Screen-space overlay: selection indicator (constant thickness).
    /// Dual stroke (#370 G7): `selectionLayer` (labelColor, 3 pt) is
    /// the measured APCA carrier against every fill; the thinner
    /// accent core is brand, not the contrast guarantee.
    private let selectionLayer = CAShapeLayer()
    private let selectionAccentLayer = CAShapeLayer()

    private var cardLayers: [String: CALayer] = [:]
    private var axElements: [CanvasCardAXElement] = []
    private var subscriptions: Set<AnyCancellable> = []
    /// Last selection the renderer auto-panned for — dedupes the
    /// observation-driven pan against the synchronous keyboard one.
    private var lastAutoPannedSelection: String?

    /// Speakable names, deduplicated for Voice Control (t0 §1.1 /
    /// t3 uniqueness test): duplicate display titles get a stable
    /// reading-order ordinal suffix ("Ideas", "Ideas 2").
    private(set) var speakableNames: [String: String] = [:]
    /// Session-sticky assignments (red-team #367 F4): survivors keep
    /// their name across mutations — deletes never renumber, renames
    /// re-assign only the renamed card, and a rematerialized element
    /// can never collide with a reused ordinal.
    private var assignedSpeakable: [String: (title: String, name: String)] = [:]

    override var acceptsFirstResponder: Bool { true }
    override var isFlipped: Bool { true }

    override init(frame: NSRect) {
        super.init(frame: frame)
        wantsLayer = true
        layer?.masksToBounds = true
        contentLayer.anchorPoint = .zero
        layer?.addSublayer(contentLayer)
        contentLayer.addSublayer(edgeLayer)
        selectionLayer.fillColor = nil
        selectionLayer.lineWidth = 3  // minimum screen-space thickness
        layer?.addSublayer(selectionLayer)
        selectionAccentLayer.fillColor = nil
        selectionAccentLayer.lineWidth = 1.5
        layer?.addSublayer(selectionAccentLayer)
        setAccessibilityRole(.group)
        setAccessibilityLabel("Canvas visual view")
        // #520: zoom level inspectable from the renderer's AX value
        // (t0 §3) — refreshed on every rebuild.
        postsFrameChangedNotifications = true
        NotificationCenter.default.addObserver(
            self, selector: #selector(frameChanged),
            name: NSView.frameDidChangeNotification, object: self)
        NSWorkspace.shared.notificationCenter.addObserver(
            self, selector: #selector(contrastPreferencesChanged),
            name: NSWorkspace.accessibilityDisplayOptionsDidChangeNotification,
            object: nil)
    }

    required init?(coder: NSCoder) { fatalError("not used") }

    func configure(document: CanvasDocument, appState: AppState) {
        let firstBind = self.document !== document
        self.document = document
        self.appState = appState
        document.viewport.viewSize = bounds.size
        if firstBind {
            // Red-team #367 F1/F5/F7: the renderer OBSERVES its inputs —
            // palette viewport commands, selection moves from any
            // surface, and scene/transient refreshes all invalidate
            // without needing renderer focus or a SwiftUI pass.
            subscriptions.removeAll()
            // Main-actor task hops (not main-queue dispatch): the
            // willChange fires pre-mutation, and a queued main-actor
            // job runs after the mutating call finishes its turn.
            document.viewport.objectWillChange
                .sink { [weak self] _ in
                    Task { @MainActor [weak self] in self?.rebuildVisible() }
                }
                .store(in: &subscriptions)
            document.selection.objectWillChange
                .sink { [weak self] _ in
                    Task { @MainActor [weak self] in self?.selectionDidChange() }
                }
                .store(in: &subscriptions)
            document.objectWillChange
                .sink { [weak self] _ in
                    Task { @MainActor [weak self] in self?.refreshFromDocument() }
                }
                .store(in: &subscriptions)
            refreshFromDocument()
        }
    }

    /// Selection moved (any surface): repaint, and — when
    /// follow-selection is ON (t2 decision 6, the F7 wiring) — auto-pan
    /// silently. The keyboard path pans unconditionally (2.4.11) via
    /// its own synchronous call; `lastAutoPannedSelection` dedupes.
    private func selectionDidChange() {
        guard let document else { return }
        if let selected = document.selection.selected,
            selected != lastAutoPannedSelection,
            document.viewport.followSelection
        {
            scrollSelectionIntoView()
        } else {
            rebuildVisible()
        }
    }

    @objc private func frameChanged() {
        viewport?.viewSize = bounds.size
        rebuildVisible()
    }

    override func viewDidChangeEffectiveAppearance() {
        super.viewDidChangeEffectiveAppearance()
        rebuildVisible()
    }

    /// Increase Contrast flip repaints (#370) — registered in init on
    /// the workspace center (the #416 lesson: that's where it posts).
    @objc private func contrastPreferencesChanged() {
        rebuildVisible()
    }

    // MARK: Data → layers/elements

    func refreshFromDocument() {
        computeSpeakableNames()
        rebuildVisible()
    }

    private func computeSpeakableNames() {
        guard let document else {
            speakableNames = [:]
            return
        }
        var result: [String: String] = [:]
        var used: Set<String> = []
        // Pass 1: survivors with unchanged titles keep their name
        // (t0 §1.1: ordinals hold for the session).
        for node in document.scene.nodes {
            if let assigned = assignedSpeakable[node.nodeId], assigned.title == node.title {
                result[node.nodeId] = assigned.name
                used.insert(assigned.name)
            }
        }
        // Pass 2: new/renamed cards take the first non-colliding name
        // in reading order.
        for node in document.scene.nodes where result[node.nodeId] == nil {
            var candidate = node.title
            var n = 1
            while used.contains(candidate) {
                n += 1
                candidate = "\(node.title) \(n)"
            }
            result[node.nodeId] = candidate
            used.insert(candidate)
        }
        speakableNames = result
        assignedSpeakable = Dictionary(
            uniqueKeysWithValues: document.scene.nodes.compactMap { node in
                result[node.nodeId].map { (node.nodeId, (title: node.title, name: $0)) }
            })
    }

    private var viewport: CanvasViewport? { document?.viewport }

    /// Canvas-space rect currently worth materializing: the viewport
    /// plus a one-viewport margin on every side (t3 windowing).
    var materializationRect: CGRect {
        guard let viewport, bounds.width > 0 else { return .zero }
        let visibleWidth = bounds.width / viewport.scale
        let visibleHeight = bounds.height / viewport.scale
        return CGRect(
            x: viewport.offset.x - visibleWidth,
            y: viewport.offset.y - visibleHeight,
            width: visibleWidth * 3,
            height: visibleHeight * 3)
    }

    private func canvasToView(_ rect: CGRect) -> CGRect {
        guard let viewport else { return rect }
        return CGRect(
            x: (rect.origin.x - viewport.offset.x) * viewport.scale,
            y: (rect.origin.y - viewport.offset.y) * viewport.scale,
            width: rect.width * viewport.scale,
            height: rect.height * viewport.scale)
    }

    /// Rebuild the windowed layer + AX materialization. Implicit
    /// animations stay off (Reduce Motion compliant by construction).
    func rebuildVisible() {
        guard let document else { return }
        CATransaction.begin()
        CATransaction.setDisableActions(true)
        defer { CATransaction.commit() }

        let window = materializationRect
        var seen: Set<String> = []
        var elements: [CanvasCardAXElement] = []

        for node in document.scene.nodes {
            // #521: an active move/resize mode's hypothetical geometry
            // overrides the committed scene rect.
            let transient = document.transientRects?[node.nodeId]
            let canvasRect = CGRect(
                x: transient?.x ?? node.x, y: transient?.y ?? node.y,
                width: transient?.width ?? node.width,
                height: transient?.height ?? node.height)
            // Red-team #367 F2: the SELECTED card always materializes —
            // a long move-mode drag past the window must never strand
            // the VO cursor on a vanished element.
            guard window.intersects(canvasRect) || node.nodeId == document.selection.selected
            else { continue }
            seen.insert(node.nodeId)
            let viewRect = canvasToView(canvasRect)

            let cardLayer = cardLayers[node.nodeId] ?? makeCardLayer(for: node)
            cardLayers[node.nodeId] = cardLayer
            if cardLayer.superlayer == nil { contentLayer.addSublayer(cardLayer) }
            cardLayer.frame = viewRect
            // #370: color paints EVERY pass — cached layers must track
            // color edits, appearance flips, and Increase Contrast.
            let increaseContrast = NSWorkspace.shared.accessibilityDisplayShouldIncreaseContrast
            cardLayer.backgroundColor = CanvasColorPalette.cardFill(
                raw: node.color, isGroup: node.kind == "group",
                increaseContrast: increaseContrast, appearance: effectiveAppearance
            ).cgColor
            cardLayer.borderColor = CanvasColorPalette.cardBorder(
                raw: node.color, increaseContrast: increaseContrast,
                appearance: effectiveAppearance
            ).cgColor
            if let text = cardLayer.sublayers?.first as? CATextLayer {
                // Frame tracks the SCALED rect, not the canvas-unit
                // width stamped at creation (Codoki #615) — at non-1.0
                // zoom the old width truncated or overflowed the card.
                text.fontSize = max(4, 12 * (viewport?.scale ?? 1))
                text.frame = CGRect(
                    x: 6, y: 4, width: max(0, viewRect.width - 12), height: text.fontSize * 1.6)
                text.foregroundColor =
                    CanvasColorPalette.cardText(appearance: effectiveAppearance).cgColor
            }

            let element = axElement(for: node)
            element.setAccessibilityFrame(screenRect(from: viewRect))
            // Red-team #367 F4: labels re-stamp on every pass — a
            // reused element must not speak a pre-rename title.
            let speakable = speakableNames[node.nodeId] ?? node.title
            element.setAccessibilityLabel(
                node.kind == "group" ? "Group \(speakable)" : speakable)
            // t0 §3: marked state is pull-readable on the element
            // everywhere the card appears.
            element.setAccessibilityValue(
                document.selection.marked.contains(node.nodeId) ? "marked" : "")
            elements.append(element)
        }

        // Drop layers/elements that left the window (row-reuse spirit).
        for (id, cardLayer) in cardLayers where !seen.contains(id) {
            cardLayer.removeFromSuperlayer()
            cardLayers[id] = nil
        }
        axElements = elements
        setAccessibilityChildren(elements)

        rebuildEdges(window: window)
        updateSelectionIndicator()
        setAccessibilityValue("Zoom \(viewport?.zoomPercent ?? 100) percent")
    }

    private func makeCardLayer(for node: CanvasSceneNode) -> CALayer {
        let cardLayer = CALayer()
        cardLayer.cornerRadius = 6
        cardLayer.borderWidth = 1
        // Colors are stamped per-pass in rebuildVisible (#370).
        let text = CATextLayer()
        text.string = node.title
        text.fontSize = 12
        text.truncationMode = .end
        text.contentsScale = window?.backingScaleFactor ?? 2
        text.frame = CGRect(x: 6, y: 4, width: max(0, node.width - 12), height: 20)
        cardLayer.addSublayer(text)
        return cardLayer
    }

    private func axElement(for node: CanvasSceneNode) -> CanvasCardAXElement {
        if let existing = axElements.first(where: { $0.nodeId == node.nodeId }) {
            return existing
        }
        let element = CanvasCardAXElement()
        element.nodeId = node.nodeId
        element.setAccessibilityRole(.button)
        element.setAccessibilityParent(self)
        let speakable = speakableNames[node.nodeId] ?? node.title
        element.setAccessibilityLabel(
            node.kind == "group" ? "Group \(speakable)" : speakable)
        element.setAccessibilityHelp(
            "\(node.kind.capitalized). Press to select. The outline and table carry the same card.")
        element.onPress = { [weak self] in
            guard let self, let document = self.document, let appState = self.appState else {
                return
            }
            appState.canvasSelect(nodeId: node.nodeId, in: document)
            self.updateSelectionIndicator()
        }
        element.onAXFocus = { [weak self] in
            guard let self, let document = self.document, let appState = self.appState,
                document.selection.selected != node.nodeId
            else { return }
            appState.canvasSelect(nodeId: node.nodeId, in: document, announce: false)
        }
        return element
    }

    private func screenRect(from viewRect: CGRect) -> CGRect {
        guard let window = self.window else {
            // Windowless (unit tests): view coordinates stand in; the
            // invalidation contract is exercised on relative values.
            return viewRect
        }
        let inWindow = convert(viewRect, to: nil)
        return window.convertToScreen(inWindow)
    }

    private func rebuildEdges(window: CGRect) {
        guard let document else { return }
        let path = CGMutablePath()
        var coloredPaths: [String: CGMutablePath] = [:]
        var byId: [String: CanvasSceneNode] = [:]
        for node in document.scene.nodes { byId[node.nodeId] = node }
        for edge in document.scene.edges {
            guard let from = byId[edge.fromNode], let to = byId[edge.toNode] else { continue }
            let fromTransient = document.transientRects?[edge.fromNode]
            let toTransient = document.transientRects?[edge.toNode]
            let fromRect = CGRect(
                x: fromTransient?.x ?? from.x, y: fromTransient?.y ?? from.y,
                width: fromTransient?.width ?? from.width,
                height: fromTransient?.height ?? from.height)
            let toRect = CGRect(
                x: toTransient?.x ?? to.x, y: toTransient?.y ?? to.y,
                width: toTransient?.width ?? to.width,
                height: toTransient?.height ?? to.height)
            guard window.intersects(fromRect) || window.intersects(toRect) else { continue }
            let start = canvasToView(anchorPoint(on: fromRect, side: edge.fromSide, toward: toRect))
            let end = canvasToView(anchorPoint(on: toRect, side: edge.toSide, toward: fromRect))
            // #370: colored connections stroke in their own layer.
            let target: CGMutablePath
            if let raw = edge.color, !raw.isEmpty {
                let existing = coloredPaths[raw] ?? CGMutablePath()
                coloredPaths[raw] = existing
                target = existing
            } else {
                target = path
            }
            target.move(to: CGPoint(x: start.midX, y: start.midY))
            target.addLine(to: CGPoint(x: end.midX, y: end.midY))
            if edge.toArrow {
                addArrowHead(to: target, from: start, at: end)
            }
            if edge.fromArrow {
                addArrowHead(to: target, from: end, at: start)
            }
        }
        let increaseContrast = NSWorkspace.shared.accessibilityDisplayShouldIncreaseContrast
        let lineWidth = max(1, 1.5 * (viewport?.scale ?? 1))
        edgeLayer.path = path
        edgeLayer.strokeColor = CanvasColorPalette.edgeStroke(
            raw: nil, increaseContrast: increaseContrast, appearance: effectiveAppearance
        ).cgColor
        edgeLayer.fillColor = nil
        edgeLayer.lineWidth = lineWidth
        for (raw, layer) in coloredEdgeLayers where coloredPaths[raw] == nil {
            layer.removeFromSuperlayer()
            coloredEdgeLayers[raw] = nil
        }
        for (raw, coloredPath) in coloredPaths {
            let layer = coloredEdgeLayers[raw] ?? CAShapeLayer()
            coloredEdgeLayers[raw] = layer
            if layer.superlayer == nil { contentLayer.addSublayer(layer) }
            layer.path = coloredPath
            layer.strokeColor = CanvasColorPalette.edgeStroke(
                raw: raw, increaseContrast: increaseContrast, appearance: effectiveAppearance
            ).cgColor
            layer.fillColor = nil
            layer.lineWidth = lineWidth
        }
    }

    private func anchorPoint(on rect: CGRect, side: CanvasSide?, toward other: CGRect) -> CGRect {
        let point: CGPoint
        switch side {
        case .top: point = CGPoint(x: rect.midX, y: rect.minY)
        case .bottom: point = CGPoint(x: rect.midX, y: rect.maxY)
        case .left: point = CGPoint(x: rect.minX, y: rect.midY)
        case .right: point = CGPoint(x: rect.maxX, y: rect.midY)
        case nil:
            // Auto: nearest side toward the other card's center.
            let dx = other.midX - rect.midX
            let dy = other.midY - rect.midY
            if abs(dx) > abs(dy) {
                point = CGPoint(x: dx > 0 ? rect.maxX : rect.minX, y: rect.midY)
            } else {
                point = CGPoint(x: rect.midX, y: dy > 0 ? rect.maxY : rect.minY)
            }
        }
        return CGRect(origin: point, size: .zero)
    }

    private func addArrowHead(to path: CGMutablePath, from startRect: CGRect, at endRect: CGRect) {
        let start = CGPoint(x: startRect.midX, y: startRect.midY)
        let end = CGPoint(x: endRect.midX, y: endRect.midY)
        let angle = atan2(end.y - start.y, end.x - start.x)
        let size: CGFloat = 8
        for spread in [CGFloat.pi * 0.85, -CGFloat.pi * 0.85] {
            path.move(to: end)
            path.addLine(
                to: CGPoint(
                    x: end.x + size * cos(angle + spread),
                    y: end.y + size * sin(angle + spread)))
        }
    }

    // MARK: Selection

    /// Screen-space indicator: constant 3 pt border regardless of zoom
    /// (WCAG 2.4.7 — never a scaled sub-pixel ring).
    func updateSelectionIndicator() {
        CATransaction.begin()
        CATransaction.setDisableActions(true)
        defer { CATransaction.commit() }
        guard let document, let selected = document.selection.selected,
            let node = document.scene.nodes.first(where: { $0.nodeId == selected })
        else {
            selectionLayer.path = nil
            selectionAccentLayer.path = nil
            return
        }
        // #521 preview: the ring tracks the transient, not the
        // committed rect (red-team #367 F3).
        let transient = document.transientRects?[selected]
        let viewRect = canvasToView(
            CGRect(
                x: transient?.x ?? node.x, y: transient?.y ?? node.y,
                width: transient?.width ?? node.width,
                height: transient?.height ?? node.height)
        ).insetBy(dx: -3, dy: -3)
        let ring = CGPath(
            roundedRect: viewRect, cornerWidth: 8, cornerHeight: 8, transform: nil)
        selectionLayer.path = ring
        selectionLayer.strokeColor =
            CanvasColorPalette.selectionRingCarrier(appearance: effectiveAppearance).cgColor
        selectionAccentLayer.path = ring
        selectionAccentLayer.strokeColor = NSColor.controlAccentColor.cgColor
    }

    /// Keyboard selection ALWAYS scrolls into view (WCAG 2.4.11),
    /// regardless of the follow-selection toggle; the auto-pan is
    /// silent (t0 §1.5).
    func scrollSelectionIntoView() {
        guard let document, let viewport,
            let selected = document.selection.selected,
            let node = document.scene.nodes.first(where: { $0.nodeId == selected }),
            bounds.width > 0
        else { return }
        lastAutoPannedSelection = selected
        // Track the PREVIEW during a move (red-team #367 F2): panning
        // to the committed rect would chase where the card was.
        let transient = document.transientRects?[selected]
        let x = transient?.x ?? node.x
        let y = transient?.y ?? node.y
        let width = transient?.width ?? node.width
        let height = transient?.height ?? node.height
        let visibleWidth = bounds.width / viewport.scale
        let visibleHeight = bounds.height / viewport.scale
        var origin = viewport.offset
        if x < origin.x { origin.x = x - 40 }
        if y < origin.y { origin.y = y - 40 }
        if x + width > origin.x + visibleWidth {
            origin.x = x + width - visibleWidth + 40
        }
        if y + height > origin.y + visibleHeight {
            origin.y = y + height - visibleHeight + 40
        }
        if origin != viewport.offset {
            viewport.offset = origin
        }
        rebuildVisible()
    }

    // MARK: Input

    override func mouseDown(with event: NSEvent) {
        window?.makeFirstResponder(self)
        guard let document, let appState else { return }
        let viewPoint = convert(event.locationInWindow, from: nil)
        let hit = hitTestNode(atViewPoint: viewPoint)
        if let hit {
            appState.canvasSelect(nodeId: hit.nodeId, in: document)
            updateSelectionIndicator()
        }
    }

    /// Topmost by document order = LAST hit in scene order (t1
    /// tiebreak), against the DRAWN rects — a mid-move preview is what
    /// the user sees, so it is what a click means (red-team #367 F6).
    func hitTestNode(atViewPoint viewPoint: CGPoint) -> CanvasSceneNode? {
        guard let document else { return nil }
        return document.scene.nodes.last { node in
            let transient = document.transientRects?[node.nodeId]
            let rect = CGRect(
                x: transient?.x ?? node.x, y: transient?.y ?? node.y,
                width: transient?.width ?? node.width,
                height: transient?.height ?? node.height)
            return canvasToView(rect).contains(viewPoint)
        }
    }

    override func keyDown(with event: NSEvent) {
        guard let appState else { return super.keyDown(with: event) }
        // ⇧1 fit canvas / ⇧2 zoom to selection: typing keys, so rule
        // R2 applies strictly — surface focus only, palette
        // equivalents always (#520).
        if event.modifierFlags.contains(.shift),
            let chars = event.charactersIgnoringModifiers
        {
            if chars == "1" || chars == "!" {
                appState.canvasFitCanvas()
                rebuildVisible()
                return
            }
            if chars == "2" || chars == "@" {
                appState.canvasZoomToSelection()
                rebuildVisible()
                return
            }
        }
        let shift = event.modifierFlags.contains(.shift)
        let inMode = appState.canvasModeConsumesArrows
        switch event.keyCode {
        case 125:  // ↓ nudge in mode; next in reading order otherwise
            inMode
                ? appState.canvasModeStep(dx: 0, dy: 1, large: shift)
                : appState.canvasSelectAdjacent(offset: 1)
        case 126:  // ↑
            inMode
                ? appState.canvasModeStep(dx: 0, dy: -1, large: shift)
                : appState.canvasSelectAdjacent(offset: -1)
        case 123:  // ←
            inMode
                ? appState.canvasModeStep(dx: -1, dy: 0, large: shift)
                : appState.canvasFollowConnection(forward: false)
        case 124:  // →
            inMode
                ? appState.canvasModeStep(dx: 1, dy: 0, large: shift)
                : appState.canvasFollowConnection(forward: true)
        case 36 where inMode:  // Return commits the mode
            if let doc = appState.activeCanvasDocument {
                _ = appState.canvasModeController(for: doc).commit()
            }
        default:
            return super.keyDown(with: event)
        }
        scrollSelectionIntoView()
        updateSelectionIndicator()
    }

    override func scrollWheel(with event: NSEvent) {
        guard let viewport else { return }
        viewport.offset.x -= event.scrollingDeltaX / viewport.scale
        viewport.offset.y -= event.scrollingDeltaY / viewport.scale
        rebuildVisible()
    }

    override func magnify(with event: NSEvent) {
        guard let viewport else { return }
        // Through setScale so the pinch keeps the view center fixed
        // like every other zoom path (red-team #367 F8).
        viewport.setScale(viewport.scale * (1 + event.magnification))
        rebuildVisible()
    }

    // MARK: Test seams

    /// Visible card frames in VIEW coordinates (unit tests assert the
    /// invalidation contract: zooming/panning changes these).
    func visibleCardFramesForTesting() -> [String: CGRect] {
        var out: [String: CGRect] = [:]
        for element in axElements {
            out[element.nodeId] = element.accessibilityFrame()
        }
        return out
    }

    func speakableLabelsForTesting() -> [String] {
        axElements.compactMap { $0.accessibilityLabel() }
    }

    /// #370 test seam: the painted fill per materialized card.
    func cardFillsForTesting() -> [String: CGColor] {
        var out: [String: CGColor] = [:]
        for (id, layer) in cardLayers {
            if let fill = layer.backgroundColor { out[id] = fill }
        }
        return out
    }
}
