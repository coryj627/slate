// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
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

    override func accessibilityPerformPress() -> Bool {
        onPress?()
        return onPress != nil
    }
}

@MainActor
final class CanvasRendererNSView: NSView {
    private(set) weak var appState: AppState?
    private(set) var document: CanvasDocument?

    private let contentLayer = CALayer()
    private let edgeLayer = CAShapeLayer()
    /// Screen-space overlay: selection indicator (constant thickness).
    private let selectionLayer = CAShapeLayer()

    private var cardLayers: [String: CALayer] = [:]
    private var axElements: [CanvasCardAXElement] = []

    /// Speakable names, deduplicated for Voice Control (t0 §1.1 /
    /// t3 uniqueness test): duplicate display titles get a stable
    /// reading-order ordinal suffix ("Ideas", "Ideas 2").
    private(set) var speakableNames: [String: String] = [:]

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
        setAccessibilityRole(.group)
        setAccessibilityLabel("Canvas visual view")
        // #520: zoom level inspectable from the renderer's AX value
        // (t0 §3) — refreshed on every rebuild.
        postsFrameChangedNotifications = true
        NotificationCenter.default.addObserver(
            self, selector: #selector(frameChanged),
            name: NSView.frameDidChangeNotification, object: self)
    }

    required init?(coder: NSCoder) { fatalError("not used") }

    func configure(document: CanvasDocument, appState: AppState) {
        let firstBind = self.document !== document
        self.document = document
        self.appState = appState
        document.viewport.viewSize = bounds.size
        if firstBind {
            refreshFromDocument()
        }
    }

    @objc private func frameChanged() {
        viewport?.viewSize = bounds.size
        rebuildVisible()
    }

    // MARK: Data → layers/elements

    func refreshFromDocument() {
        computeSpeakableNames()
        rebuildVisible()
    }

    private func computeSpeakableNames() {
        speakableNames = [:]
        guard let document else { return }
        var counts: [String: Int] = [:]
        for node in document.scene.nodes {
            let n = (counts[node.title] ?? 0) + 1
            counts[node.title] = n
            speakableNames[node.nodeId] = n == 1 ? node.title : "\(node.title) \(n)"
        }
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
            let canvasRect = CGRect(x: node.x, y: node.y, width: node.width, height: node.height)
            guard window.intersects(canvasRect) else { continue }
            seen.insert(node.nodeId)
            let viewRect = canvasToView(canvasRect)

            let cardLayer = cardLayers[node.nodeId] ?? makeCardLayer(for: node)
            cardLayers[node.nodeId] = cardLayer
            if cardLayer.superlayer == nil { contentLayer.addSublayer(cardLayer) }
            cardLayer.frame = viewRect
            if let text = cardLayer.sublayers?.first as? CATextLayer {
                // Frame tracks the SCALED rect, not the canvas-unit
                // width stamped at creation (Codoki #615) — at non-1.0
                // zoom the old width truncated or overflowed the card.
                text.fontSize = max(4, 12 * (viewport?.scale ?? 1))
                text.frame = CGRect(
                    x: 6, y: 4, width: max(0, viewRect.width - 12), height: text.fontSize * 1.6)
            }

            let element = axElement(for: node)
            element.setAccessibilityFrame(screenRect(from: viewRect))
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
        cardLayer.borderColor = NSColor.separatorColor.cgColor
        cardLayer.backgroundColor =
            node.kind == "group"
            ? NSColor.quaternarySystemFill.cgColor
            : NSColor.controlBackgroundColor.cgColor
        let text = CATextLayer()
        text.string = node.title
        text.fontSize = 12
        text.foregroundColor = NSColor.textColor.cgColor
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
        var byId: [String: CanvasSceneNode] = [:]
        for node in document.scene.nodes { byId[node.nodeId] = node }
        for edge in document.scene.edges {
            guard let from = byId[edge.fromNode], let to = byId[edge.toNode] else { continue }
            let fromRect = CGRect(x: from.x, y: from.y, width: from.width, height: from.height)
            let toRect = CGRect(x: to.x, y: to.y, width: to.width, height: to.height)
            guard window.intersects(fromRect) || window.intersects(toRect) else { continue }
            let start = canvasToView(anchorPoint(on: fromRect, side: edge.fromSide, toward: toRect))
            let end = canvasToView(anchorPoint(on: toRect, side: edge.toSide, toward: fromRect))
            path.move(to: CGPoint(x: start.midX, y: start.midY))
            path.addLine(to: CGPoint(x: end.midX, y: end.midY))
            if edge.toArrow {
                addArrowHead(to: path, from: start, at: end)
            }
            if edge.fromArrow {
                addArrowHead(to: path, from: end, at: start)
            }
        }
        edgeLayer.path = path
        edgeLayer.strokeColor = NSColor.tertiaryLabelColor.cgColor
        edgeLayer.fillColor = nil
        edgeLayer.lineWidth = max(1, 1.5 * (viewport?.scale ?? 1))
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
            return
        }
        let viewRect = canvasToView(
            CGRect(x: node.x, y: node.y, width: node.width, height: node.height)
        ).insetBy(dx: -3, dy: -3)
        selectionLayer.path = CGPath(
            roundedRect: viewRect, cornerWidth: 8, cornerHeight: 8, transform: nil)
        selectionLayer.strokeColor = NSColor.controlAccentColor.cgColor
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
        let visibleWidth = bounds.width / viewport.scale
        let visibleHeight = bounds.height / viewport.scale
        var origin = viewport.offset
        if node.x < origin.x { origin.x = node.x - 40 }
        if node.y < origin.y { origin.y = node.y - 40 }
        if node.x + node.width > origin.x + visibleWidth {
            origin.x = node.x + node.width - visibleWidth + 40
        }
        if node.y + node.height > origin.y + visibleHeight {
            origin.y = node.y + node.height - visibleHeight + 40
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
        // Topmost by document order = LAST hit in scene order (t1 tiebreak).
        let hit = document.scene.nodes.last { node in
            canvasToView(CGRect(x: node.x, y: node.y, width: node.width, height: node.height))
                .contains(viewPoint)
        }
        if let hit {
            appState.canvasSelect(nodeId: hit.nodeId, in: document)
            updateSelectionIndicator()
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
        switch event.keyCode {
        case 125:  // ↓ next in reading order (R2: all four arrows navigate)
            appState.canvasSelectAdjacent(offset: 1)
        case 126:  // ↑ previous
            appState.canvasSelectAdjacent(offset: -1)
        case 123:  // ← follow connection back
            appState.canvasFollowConnection(forward: false)
        case 124:  // → follow connection forward
            appState.canvasFollowConnection(forward: true)
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
        viewport.scale = viewport.clampScale(viewport.scale * (1 + event.magnification))
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
}
