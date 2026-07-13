// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Accessibility
import AppKit
import Combine
import SwiftUI

/// The visual graph surface (Milestone P, P2-3 #559) — the Diagram mode
/// of the Graph tab. **Not an opaque view** (the Canvas #367 lesson):
/// every node in Tier A publishes an `NSAccessibilityElement` with a
/// screen-coordinate frame tracking pan/zoom, labelled with the SAME
/// P1-1 row copy the Table speaks, and exposing Open / Show connections /
/// Pin actions, so VoiceOver and Voice Control work *on* the surface.
///
/// - **Geometry:** node/edge layers live in the layout's coordinate space
///   inside a single `contentLayer`; pan/zoom is one affine transform on
///   that layer (no per-node re-layout — spec's 60 fps contract).
///   Positions change only when a `LayoutFrame` arrives.
/// - **Tiers (spec §P2-3):** at ≤ 1,500 nodes (Tier A) EVERY filtered
///   node has its own layer + label (label-capped at 200 by in-links) +
///   AX element — the whole set stays materialized regardless of pan, so
///   the AX tree is always complete. Above that (Tier B) nodes batch into
///   one transform-driven dot path, per-node layers/labels/elements drop,
///   and accessibility becomes a single summary element that switches to
///   Table mode. Hit-testing uses a uniform spatial grid in both tiers.
/// - **Reduce Motion:** the settle runs straight to convergence and a
///   single frame is applied — no drift. All layer work runs with
///   implicit animations disabled.
struct GraphDiagramView: NSViewRepresentable {
    @EnvironmentObject var appState: AppState
    let model: GraphDiagramModel
    let tabID: TabID
    /// Invoked by the Tier-B summary's "Switch to Table" action (the mode
    /// is the container's `@State`, so the renderer can't flip it itself).
    let onSwitchToTable: () -> Void
    @Environment(\.accessibilityReduceMotion) private var reduceMotion

    func makeNSView(context: Context) -> GraphDiagramNSView {
        let view = GraphDiagramNSView()
        view.configure(
            model: model, appState: appState, tabID: tabID, reduceMotion: reduceMotion,
            onSwitchToTable: onSwitchToTable)
        return view
    }

    func updateNSView(_ view: GraphDiagramNSView, context: Context) {
        view.configure(
            model: model, appState: appState, tabID: tabID, reduceMotion: reduceMotion,
            onSwitchToTable: onSwitchToTable)
    }

    static func dismantleNSView(_ view: GraphDiagramNSView, coordinator: ()) {
        view.stopSettling()
    }
}

/// One node's AX proxy on the renderer (mirrors `CanvasCardAXElement`).
/// Neighbor labels ride `accessibilityCustomContent` (edges are not
/// individual AX elements); VoiceOver focus syncs into diagram selection
/// and auto-pans so pure VO traversal never dead-ends off-window.
final class GraphNodeAXElement: NSAccessibilityElement, AXCustomContentProvider {
    var nodeId: UInt64 = 0
    var onPress: (() -> Void)?
    var onAXFocus: (() -> Void)?
    var accessibilityCustomContent: [AXCustomContent] = []
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
final class GraphDiagramNSView: NSView {
    private(set) weak var appState: AppState?
    private(set) var model: GraphDiagramModel?
    private var tabID: TabID?
    private(set) var reduceMotion = false
    private var onSwitchToTable: (() -> Void)?

    /// Layout-space content (pans/zooms via one transform); the selection
    /// ring is a screen-space overlay of constant thickness.
    private let contentLayer = CALayer()
    private let edgeLayer = CAShapeLayer()
    private let nodeContainer = CALayer()
    /// Tier-B host: one rasterized `CAShapeLayer` per occupied tile
    /// (manual tiling, spec §P2-3). Each tile draws its own nodes in
    /// tile-local coords and caches the render (`shouldRasterize`), so
    /// pan/zoom is a content-layer transform over cached bitmaps —
    /// off-screen tiles are frame-culled by Core Animation, and nothing
    /// re-rasterizes per frame.
    private let tileContainer = CALayer()
    private var tileLayers: [GridKey: CAShapeLayer] = [:]
    private let selectionLayer = CAShapeLayer()
    private let selectionAccentLayer = CAShapeLayer()

    private var nodeLayers: [UInt64: CAShapeLayer] = [:]
    private var labelLayers: [UInt64: CATextLayer] = [:]
    private var axElements: [GraphNodeAXElement] = []
    private var summaryElement: GraphNodeAXElement?
    private var subscriptions: Set<AnyCancellable> = []
    private var trackingArea: NSTrackingArea?

    /// Latest layout-space positions, keyed by node id (from frames).
    private var positions: [UInt64: CGPoint] = [:]
    /// Uniform spatial grid over `positions` (layout space) — the
    /// hit-test index, rebuilt when positions change (both tiers).
    private var grid: [GridKey: [UInt64]] = [:]
    private var settleTask: Task<Void, Never>?
    private var settleCancel: CancelToken?
    private var didInitialFit = false
    private var lastTierB = false
    /// The label-visibility state (zoom ≥ fade threshold) the last
    /// `rebuildTierA` materialized labels for — so a zoom that crosses the
    /// threshold can re-materialize/remove them (labels are created only
    /// in rebuild, not the pan/zoom transform).
    private var lastLabelsShown = false
    private var typeAheadBuffer = ""
    private var typeAheadStamp: TimeInterval = 0

    struct GridKey: Hashable { let x: Int; let y: Int }
    /// Spatial-grid cell in LAYOUT units (≈64 pt at 100% zoom, spec).
    static let gridCell: CGFloat = 64
    /// Tier-B tile size in LAYOUT units (each tile is one rasterized
    /// `CAShapeLayer`, frame-culled + cached).
    static let tileSize: CGFloat = 512
    /// Visible-label cap (spec §P2-3): at most this many labels, chosen by
    /// in-links, so a dense graph doesn't drown in text.
    static let labelCap = 200
    /// Labels only draw at or above this zoom (text-fade parity).
    static let labelFadeZoom: CGFloat = 0.55

    override var acceptsFirstResponder: Bool { true }
    override var isFlipped: Bool { true }

    override init(frame: NSRect) {
        super.init(frame: frame)
        wantsLayer = true
        layer?.masksToBounds = true
        contentLayer.anchorPoint = .zero
        contentLayer.frame = bounds
        layer?.addSublayer(contentLayer)
        contentLayer.addSublayer(edgeLayer)
        contentLayer.addSublayer(tileContainer)
        contentLayer.addSublayer(nodeContainer)
        edgeLayer.fillColor = nil
        selectionLayer.fillColor = nil
        selectionLayer.lineWidth = 3  // WCAG 2.4.7 minimum screen thickness
        layer?.addSublayer(selectionLayer)
        selectionAccentLayer.fillColor = nil
        selectionAccentLayer.lineWidth = 1.5
        layer?.addSublayer(selectionAccentLayer)
        setAccessibilityRole(.group)
        setAccessibilityLabel("Graph, visual diagram")
        postsFrameChangedNotifications = true
        NotificationCenter.default.addObserver(
            self, selector: #selector(frameChanged),
            name: NSView.frameDidChangeNotification, object: self)
    }

    required init?(coder: NSCoder) { fatalError("not used") }

    func configure(
        model: GraphDiagramModel, appState: AppState, tabID: TabID, reduceMotion: Bool,
        onSwitchToTable: @escaping () -> Void
    ) {
        let rebind = self.model !== model
        let motionFlip = self.reduceMotion != reduceMotion
        self.model = model
        self.appState = appState
        self.tabID = tabID
        self.reduceMotion = reduceMotion
        self.onSwitchToTable = onSwitchToTable
        model.viewport.viewSize = bounds.size
        if rebind {
            subscriptions.removeAll()
            model.objectWillChange
                .sink { [weak self] _ in
                    Task { @MainActor [weak self] in self?.topologyChanged() }
                }
                .store(in: &subscriptions)
            model.viewport.objectWillChange
                .sink { [weak self] _ in
                    Task { @MainActor [weak self] in self?.applyTransform() }
                }
                .store(in: &subscriptions)
            // Inspector edits (Filters name query / Groups / Display /
            // Forces) live on `appState.graphConfig`; re-render and
            // re-settle when they change (P2-4 #560). A display/groups
            // change re-settles from the converged state (near no-op); a
            // forces change re-heated the engine via `set_forces`.
            appState.$graphConfig
                .dropFirst()
                .sink { [weak self] _ in
                    Task { @MainActor [weak self] in
                        self?.rebuildTopology()
                        self?.startSettling()
                    }
                }
                .store(in: &subscriptions)
            // The client name filter (shared with the Table) hides/shows
            // nodes but doesn't move them — re-render without re-settling.
            appState.$graphTableTextFilter
                .dropFirst()
                .sink { [weak self] _ in
                    Task { @MainActor [weak self] in
                        self?.rebuildTopology()
                        self?.applyTransform()
                    }
                }
                .store(in: &subscriptions)
            didInitialFit = false
            startSettling()
        } else if motionFlip {
            startSettling()
        }
    }

    // MARK: Settle loop (spec threading contract)

    func startSettling() {
        stopSettling()
        guard let session = model?.session else { return }
        let reduce = reduceMotion
        let cancel = CancelToken()
        settleCancel = cancel
        settleTask = Task { @MainActor [weak self] in
            if reduce {
                let frame = await Task.detached(priority: .userInitiated) {
                    session.runToConvergence(cancel: cancel)
                }.value
                guard !Task.isCancelled else { return }
                self?.applyFrame(frame)
                return
            }
            while !Task.isCancelled {
                let frame = await Task.detached(priority: .userInitiated) {
                    session.tick(iterations: 20)
                }.value
                guard !Task.isCancelled else { break }
                self?.applyFrame(frame)
                if frame.converged { break }
                try? await Task.sleep(nanoseconds: 16_000_000)  // ~60 fps
            }
        }
    }

    func stopSettling() {
        settleTask?.cancel()
        settleTask = nil
        settleCancel?.cancel()
        settleCancel = nil
    }

    private func applyFrame(_ frame: LayoutFrame) {
        guard let model else { return }
        // A frame is only valid for the topology it was solved against.
        // After a `warm_update` (refresh), an in-flight tick from the old
        // generation — or a new-generation tick that lands before `adopt`
        // publishes the new ids — must be DROPPED, not applied against the
        // wrong `nodeIDs`. `model.generation` tracks the layout's
        // generation (build handshake / adopt), so equality means the
        // buffer and the id order agree.
        guard frame.generation == model.generation,
            frame.positions.count == model.nodeIDs.count * 2
        else { return }
        let ids = model.nodeIDs
        positions.removeAll(keepingCapacity: true)
        for i in 0..<ids.count {
            positions[ids[i]] = CGPoint(
                x: CGFloat(frame.positions[2 * i]),
                y: CGFloat(frame.positions[2 * i + 1]))
        }
        model.contentBounds = positionsBounds()
        if !didInitialFit, !positions.isEmpty {
            didInitialFit = true
            model.fitToContent()  // sets the viewport; transform applied below
        }
        rebuildTopology()
        applyTransform()
    }

    /// A topology re-sync (nodes added/removed) restarts the settle so
    /// newcomers seat, and rebuilds the layer/element set.
    private func topologyChanged() {
        startSettling()
        rebuildTopology()
        applyTransform()
    }

    // MARK: Geometry

    private var viewport: CanvasViewport? { model?.viewport }

    // MARK: Config-driven display / filter (P2-4 #560)

    private var display: GraphDisplay { appState?.graphConfig.display ?? .default }
    private var nameNeedle: String { appState?.graphTableTextFilter ?? "" }

    /// The client name filter — the SAME predicate the Table applies.
    private func nameMatches(_ label: String) -> Bool {
        AppState.graphNameMatches(label, needle: nameNeedle)
    }

    /// Node diameter with the display multiplier applied (`nodeDiameter`
    /// is the base spec formula, kept pure for the unit test).
    private func scaledDiameter(inLinks: UInt32) -> CGFloat {
        Self.nodeDiameter(inLinks: inLinks) * CGFloat(display.nodeSizeMultiplier)
    }

    /// Layout → view point (`view = (p − offset) × scale`) — the mapping
    /// AX frames, the selection ring, and hit-testing use; the content
    /// layer applies the equivalent affine transform.
    private func layoutToView(_ p: CGPoint) -> CGPoint {
        guard let v = viewport else { return p }
        return CGPoint(x: (p.x - v.offset.x) * v.scale, y: (p.y - v.offset.y) * v.scale)
    }

    private func viewToLayout(_ p: CGPoint) -> CGPoint? {
        guard let v = viewport, v.scale != 0 else { return nil }
        return CGPoint(x: p.x / v.scale + v.offset.x, y: p.y / v.scale + v.offset.y)
    }

    private func positionsBounds() -> CGRect {
        guard !positions.isEmpty else { return .zero }
        var minX = CGFloat.greatestFiniteMagnitude
        var minY = CGFloat.greatestFiniteMagnitude
        var maxX = -CGFloat.greatestFiniteMagnitude
        var maxY = -CGFloat.greatestFiniteMagnitude
        for p in positions.values {
            minX = min(minX, p.x)
            minY = min(minY, p.y)
            maxX = max(maxX, p.x)
            maxY = max(maxY, p.y)
        }
        return CGRect(x: minX, y: minY, width: maxX - minX, height: maxY - minY)
    }

    func fitGraph() {
        guard bounds.width > 0, let model else { return }
        model.contentBounds = positionsBounds()
        model.fitToContent()
        applyTransform()
    }

    /// Node diameter in LAYOUT units (spec §P2-3): `8 + 6·ln(1+in_links)`,
    /// clamped 8–28. The content-layer transform scales it with zoom.
    static func nodeDiameter(inLinks: UInt32) -> CGFloat {
        let d = 8.0 + 6.0 * log(1.0 + Double(inLinks))
        return CGFloat(min(28.0, max(8.0, d)))
    }

    // MARK: Data → layers / elements (layout coordinates)

    /// Rebuild the layer + AX materialization from the current positions.
    /// Layers are placed in LAYOUT space; pan/zoom is a transform applied
    /// separately (`applyTransform`). Implicit animations stay off.
    func rebuildTopology() {
        guard let model else { return }
        CATransaction.begin()
        CATransaction.setDisableActions(true)
        defer { CATransaction.commit() }
        buildGrid()
        if model.isTierB {
            rebuildTierB(model)
        } else {
            rebuildTierA(model)
        }
        rebuildEdges(model)
        if model.isTierB != lastTierB {
            lastTierB = model.isTierB
            if model.isTierB {
                appState?.graphAnnouncer.announce(
                    .status(
                        "Large graph: summary accessibility mode. Table mode has every node."))
            }
        }
    }

    private func rebuildTierA(_ model: GraphDiagramModel) {
        clearTiles()
        summaryElement = nil
        let increaseContrast = NSWorkspace.shared.accessibilityDisplayShouldIncreaseContrast
        let showLabels = (viewport?.scale ?? 1) >= display.textFadeZoom
        lastLabelsShown = showLabels
        let labelIDs = showLabels ? labelPriorityIDs(model) : []

        var seen: Set<UInt64> = []
        var elements: [GraphNodeAXElement] = []
        // Tier A materializes EVERY VISIBLE node (≤1,500) regardless of the
        // viewport, so the AX tree is always complete and a pan never drops
        // nodes (P2-3 vs Canvas windowing). The name filter (P2-4) hides
        // non-matching nodes — the SAME predicate the Table applies — so
        // both projections show one node set.
        for id in model.nodeIDs {
            guard let node = model.node(id), let p = positions[id], nameMatches(node.label) else {
                continue
            }
            seen.insert(id)
            let diameter = scaledDiameter(inLinks: node.inLinks)
            let rect = CGRect(
                x: p.x - diameter / 2, y: p.y - diameter / 2, width: diameter, height: diameter)

            let dot = nodeLayers[id] ?? makeNodeLayer()
            nodeLayers[id] = dot
            if dot.superlayer == nil { nodeContainer.addSublayer(dot) }
            dot.frame = rect
            dot.path = CGPath(ellipseIn: CGRect(origin: .zero, size: rect.size), transform: nil)
            let group = appState?.graphConfig.matchingGroup(for: node.label)
            styleNode(dot, node: node, group: group, increaseContrast: increaseContrast)

            if labelIDs.contains(id) {
                let label = labelLayers[id] ?? makeLabelLayer()
                labelLayers[id] = label
                if label.superlayer == nil { nodeContainer.addSublayer(label) }
                label.string = node.label
                label.fontSize = 11
                label.foregroundColor = NSColor.labelColor.cgColor
                label.contentsScale = window?.backingScaleFactor ?? 2
                label.frame = CGRect(x: p.x - 60, y: rect.maxY + 1, width: 120, height: 13)
            } else if let stale = labelLayers[id] {
                stale.removeFromSuperlayer()
                labelLayers[id] = nil
            }

            elements.append(axElement(for: id, node: node, model: model))
        }

        for (id, layer) in nodeLayers where !seen.contains(id) {
            layer.removeFromSuperlayer()
            nodeLayers[id] = nil
        }
        for (id, layer) in labelLayers where !seen.contains(id) {
            layer.removeFromSuperlayer()
            labelLayers[id] = nil
        }
        axElements = elements
        setAccessibilityChildren(elements)
    }

    /// Tier B (> 1,500 nodes): manual tiling (spec §P2-3). Nodes batch
    /// into one rasterized `CAShapeLayer` per occupied tile — no per-node
    /// layers/labels/elements — so pan/zoom is a content-layer transform
    /// over cached per-tile bitmaps (Core Animation frame-culls off-screen
    /// tiles, nothing re-rasterizes per frame). Accessibility is a single
    /// summary element whose press/action switches to Table mode (which
    /// always has every node).
    private func rebuildTierB(_ model: GraphDiagramModel) {
        for (_, layer) in nodeLayers { layer.removeFromSuperlayer() }
        nodeLayers.removeAll()
        for (_, layer) in labelLayers { layer.removeFromSuperlayer() }
        labelLayers.removeAll()

        // Group node positions into fixed layout-space tiles.
        var byTile: [GridKey: [CGPoint]] = [:]
        for id in model.nodeIDs {
            guard let p = positions[id] else { continue }
            let key = GridKey(
                x: Int((p.x / Self.tileSize).rounded(.down)),
                y: Int((p.y / Self.tileSize).rounded(.down)))
            byTile[key, default: []].append(p)
        }
        let fill = NSColor.controlAccentColor.cgColor
        var seenTiles: Set<GridKey> = []
        for (key, pts) in byTile {
            seenTiles.insert(key)
            let origin = CGPoint(
                x: CGFloat(key.x) * Self.tileSize, y: CGFloat(key.y) * Self.tileSize)
            let path = CGMutablePath()
            for p in pts {
                // Tile-LOCAL coords so each tile caches independently.
                path.addEllipse(
                    in: CGRect(x: p.x - origin.x - 2, y: p.y - origin.y - 2, width: 4, height: 4))
            }
            let tile = tileLayers[key] ?? CAShapeLayer()
            tileLayers[key] = tile
            if tile.superlayer == nil { tileContainer.addSublayer(tile) }
            tile.frame = CGRect(x: origin.x, y: origin.y, width: Self.tileSize, height: Self.tileSize)
            tile.path = path
            tile.fillColor = fill
            tile.shouldRasterize = true
            tile.rasterizationScale = window?.backingScaleFactor ?? 2
        }
        for (key, layer) in tileLayers where !seenTiles.contains(key) {
            layer.removeFromSuperlayer()
            tileLayers[key] = nil
        }

        let summary = summaryElement ?? GraphNodeAXElement()
        summaryElement = summary
        summary.setAccessibilityRole(.button)
        summary.setAccessibilityParent(self)
        summary.setAccessibilityLabel(
            "\(model.nodeCount) nodes — too many for per-node navigation. "
                + "Switch to Table mode for the full, navigable list.")
        summary.onPress = { [weak self] in self?.onSwitchToTable?() }
        let switchAction = NSAccessibilityCustomAction(name: "Switch to Table") { [weak self] in
            self?.onSwitchToTable?()
            return true
        }
        summary.setAccessibilityCustomActions([switchAction])
        axElements = []
        setAccessibilityChildren([summary])
    }

    private func labelPriorityIDs(_ model: GraphDiagramModel) -> Set<UInt64> {
        if model.nodeIDs.count <= Self.labelCap { return Set(model.nodeIDs) }
        let ranked = model.nodeIDs.sorted { a, b in
            (model.node(a)?.inLinks ?? 0) > (model.node(b)?.inLinks ?? 0)
        }
        return Set(ranked.prefix(Self.labelCap))
    }

    private func makeNodeLayer() -> CAShapeLayer {
        let layer = CAShapeLayer()
        layer.lineWidth = 1.5
        return layer
    }

    private func makeLabelLayer() -> CATextLayer {
        let text = CATextLayer()
        text.truncationMode = .end
        text.alignmentMode = .center
        return text
    }

    /// Node styling — never color alone. A colour GROUP (P2-4) overrides
    /// the kind fill and stamps its RING STYLE (dashed/dotted/double) so
    /// group membership reads without relying on colour (WCAG 1.4.1).
    /// Ungrouped: ghosts draw hollow + dashed, notes/attachments filled.
    /// Increase Contrast strengthens the stroke.
    private func styleNode(
        _ layer: CAShapeLayer, node: GraphNode, group: GraphGroup?, increaseContrast: Bool
    ) {
        let stroke = increaseContrast ? NSColor.labelColor : NSColor.secondaryLabelColor
        layer.strokeColor = stroke.cgColor
        layer.lineWidth = 1.5
        if let group {
            layer.fillColor = group.colorToken.color.cgColor
            layer.lineDashPattern = group.ringStyle.dashPattern
            if group.ringStyle == .double { layer.lineWidth = 3 }  // double = thicker ring
            return
        }
        switch node.kind {
        case .ghost:
            layer.fillColor = NSColor.windowBackgroundColor.cgColor
            layer.lineDashPattern = [3, 2]
        case .attachment:
            layer.fillColor = NSColor.systemGray.cgColor
            layer.lineDashPattern = nil
        case .note:
            layer.fillColor = NSColor.controlAccentColor.cgColor
            layer.lineDashPattern = nil
        }
    }

    private func rebuildEdges(_ model: GraphDiagramModel) {
        let path = CGMutablePath()
        let arrows = display.arrows
        // ALL edges among VISIBLE (name-matching) endpoints are drawn in
        // layout space (the content-layer clips off-screen geometry) — a
        // long segment crossing the viewport with both endpoints outside
        // is kept (round 1 finding 13). Edges to a name-filtered-out node
        // are dropped so the diagram matches the Table's visible set.
        for edge in model.edges {
            guard let a = positions[edge.sourceId], let b = positions[edge.targetId],
                nameMatches(model.node(edge.sourceId)?.label ?? ""),
                nameMatches(model.node(edge.targetId)?.label ?? "")
            else { continue }
            path.move(to: a)
            path.addLine(to: b)
            if arrows { addArrowhead(to: path, from: a, to: b) }
        }
        edgeLayer.path = path
        edgeLayer.strokeColor = NSColor.separatorColor.cgColor
        edgeLayer.lineWidth = max(0.5, CGFloat(display.linkThickness))
    }

    /// A small arrowhead at `to`, pointing along `from → to` (layout
    /// coords; scales with the content transform). Drawn only when the
    /// Arrows display toggle is on (P2-4).
    private func addArrowhead(to path: CGMutablePath, from: CGPoint, to end: CGPoint) {
        let angle = atan2(end.y - from.y, end.x - from.x)
        let size: CGFloat = 6
        for spread in [CGFloat.pi * 0.85, -CGFloat.pi * 0.85] {
            path.move(to: end)
            path.addLine(
                to: CGPoint(
                    x: end.x + size * cos(angle + spread), y: end.y + size * sin(angle + spread)))
        }
    }

    private func buildGrid() {
        grid.removeAll(keepingCapacity: true)
        for (id, p) in positions {
            grid[gridKey(p), default: []].append(id)
        }
    }

    private func clearTiles() {
        for (_, layer) in tileLayers { layer.removeFromSuperlayer() }
        tileLayers.removeAll()
    }

    private func gridKey(_ p: CGPoint) -> GridKey {
        GridKey(
            x: Int((p.x / Self.gridCell).rounded(.down)),
            y: Int((p.y / Self.gridCell).rounded(.down)))
    }

    private func axElement(for id: UInt64, node: GraphNode, model: GraphDiagramModel)
        -> GraphNodeAXElement
    {
        let element = axElements.first(where: { $0.nodeId == id }) ?? GraphNodeAXElement()
        element.nodeId = id
        element.setAccessibilityRole(.button)
        element.setAccessibilityParent(self)
        element.setAccessibilityLabel(axLabel(node, model: model))
        element.setAccessibilityHelp("Graph node. Press to open.")
        element.setAccessibilityValue(model.pinned.contains(id) ? "pinned" : "")
        element.accessibilityCustomContent = neighborCustomContent(of: id, model: model)
        element.onPress = { [weak self] in self?.activate(id) }
        element.onAXFocus = { [weak self] in
            guard let self else { return }
            // Sync selection only when it changed, but ALWAYS pan the
            // focused node into view — selected nodes are retained
            // off-window, so skipping the pan would strand VO focus
            // (round 1 finding 7).
            if self.model?.selection != id { self.appState?.graphDiagramSelect(id, announce: false) }
            self.scrollNodeIntoView(id)
        }
        // "Show connections" only where it can execute (a ghost has no
        // note to re-root on); Pin/Unpin always applies.
        var actions: [NSAccessibilityCustomAction] = []
        if node.kind != .ghost, node.path != nil {
            actions.append(
                NSAccessibilityCustomAction(name: "Show connections") { [weak self] in
                    self?.showConnections(id)
                    return true
                })
        }
        actions.append(
            NSAccessibilityCustomAction(name: model.pinned.contains(id) ? "Unpin" : "Pin") {
                [weak self] in
                self?.togglePin(id)
                return true
            })
        element.setAccessibilityCustomActions(actions)
        return element
    }

    private func axLabel(_ node: GraphNode, model: GraphDiagramModel) -> String {
        guard let ref = model.rowRef(node.id) else { return node.label }
        return appState?.graphAnnouncer.rowPhrase(ref) ?? node.label
    }

    /// Neighbor labels as `accessibilityCustomContent` (spec §P2-3 — edges
    /// aren't AX elements): first 10 unique, then "and k more".
    private func neighborCustomContent(of id: UInt64, model: GraphDiagramModel)
        -> [AXCustomContent]
    {
        var seen: Set<UInt64> = []
        var labels: [String] = []
        var extra = 0
        for edge in model.edges {
            let other: UInt64? =
                edge.sourceId == id ? edge.targetId : (edge.targetId == id ? edge.sourceId : nil)
            guard let other, seen.insert(other).inserted, let n = model.node(other) else { continue }
            if labels.count < 10 { labels.append(n.label) } else { extra += 1 }
        }
        guard !labels.isEmpty else { return [] }
        var value = labels.joined(separator: ", ")
        if extra > 0 { value += " and \(extra) more" }
        return [AXCustomContent(label: "Connects to", value: value)]
    }

    private func screenRect(from viewRect: CGRect) -> CGRect {
        guard let window else { return viewRect }
        return window.convertToScreen(convert(viewRect, to: nil))
    }

    // MARK: Transform (pan/zoom) — no per-node re-layout

    /// Apply the current viewport as one affine transform on the content
    /// layer (60 fps pan/zoom), and refresh the screen-coordinate AX
    /// frames + the selection ring (which cannot be transformed).
    func applyTransform() {
        // A zoom that crosses the label-fade threshold must add/remove
        // labels — they're materialized only in rebuildTierA, so relabel
        // here before applying the transform (round 2 finding 4).
        if let model, !model.isTierB,
            ((viewport?.scale ?? 1) >= display.textFadeZoom) != lastLabelsShown
        {
            rebuildTopology()
        }
        CATransaction.begin()
        CATransaction.setDisableActions(true)
        defer { CATransaction.commit() }
        if let v = viewport {
            contentLayer.frame = bounds
            contentLayer.setAffineTransform(
                CGAffineTransform(
                    a: v.scale, b: 0, c: 0, d: v.scale,
                    tx: -v.scale * v.offset.x, ty: -v.scale * v.offset.y))
        }
        updateAXFrames()
        updateSelectionIndicator()
    }

    private func updateAXFrames() {
        guard let model else { return }
        if let summary = summaryElement {
            summary.setAccessibilityFrame(screenRect(from: bounds))
            return
        }
        for element in axElements {
            guard let node = model.node(element.nodeId), let p = positions[element.nodeId] else {
                continue
            }
            let d = scaledDiameter(inLinks: node.inLinks) * (viewport?.scale ?? 1)
            let center = layoutToView(p)
            element.setAccessibilityFrame(
                screenRect(
                    from: CGRect(x: center.x - d / 2, y: center.y - d / 2, width: d, height: d)))
        }
    }

    // MARK: Selection ring (screen-space, constant thickness)

    func updateSelectionIndicator() {
        guard let model, let selected = model.selection, let p = positions[selected],
            let node = model.node(selected)
        else {
            selectionLayer.path = nil
            selectionAccentLayer.path = nil
            return
        }
        let diameter = scaledDiameter(inLinks: node.inLinks) * (viewport?.scale ?? 1)
        let center = layoutToView(p)
        let ringRect = CGRect(
            x: center.x - diameter / 2 - 4, y: center.y - diameter / 2 - 4,
            width: diameter + 8, height: diameter + 8)
        let ring = CGPath(ellipseIn: ringRect, transform: nil)
        selectionLayer.path = ring
        selectionLayer.strokeColor = NSColor.labelColor.cgColor
        selectionAccentLayer.path = ring
        selectionAccentLayer.strokeColor = NSColor.controlAccentColor.cgColor
    }

    // MARK: Hit-testing (uniform spatial grid, both tiers)

    /// Topmost node whose circle contains `viewPoint`, via the layout-space
    /// grid: map the point back to layout, scan the 3×3 cell block, keep
    /// the nearest center within its radius.
    func hitTest(atViewPoint viewPoint: CGPoint) -> UInt64? {
        guard let model, let lp = viewToLayout(viewPoint) else { return nil }
        let base = gridKey(lp)
        var best: (id: UInt64, dist: CGFloat)?
        for dx in -1...1 {
            for dy in -1...1 {
                for id in grid[GridKey(x: base.x + dx, y: base.y + dy)] ?? [] {
                    guard let p = positions[id], let node = model.node(id),
                        nameMatches(node.label)  // name-filtered-out nodes aren't hittable
                    else { continue }
                    let radius = scaledDiameter(inLinks: node.inLinks) / 2 + 2  // layout, +slop
                    let dist = hypot(p.x - lp.x, p.y - lp.y)
                    if dist <= radius, best == nil || dist < best!.dist { best = (id, dist) }
                }
            }
        }
        return best?.id
    }

    // MARK: Actions

    private func focusOwningGroup() {
        guard let appState, let tabID,
            appState.workspace.model.activeGroup.activeTabID != tabID
        else { return }
        appState.activateTab(tabID)
    }

    private func activate(_ id: UInt64) {
        guard let appState, let node = model?.node(id) else { return }
        focusOwningGroup()
        if node.kind == .ghost {
            appState.createNoteFromGhost(targetRaw: node.label)
        } else if let path = node.path {
            appState.openFile(path, target: .currentTab)
        }
    }

    private func showConnections(_ id: UInt64) {
        guard let appState, let path = model?.node(id)?.path else { return }
        focusOwningGroup()
        appState.reRootConnections(on: path)
    }

    private func togglePin(_ id: UInt64) {
        guard let appState, let p = positions[id] else { return }
        appState.graphDiagramTogglePin(id, x: Float(p.x), y: Float(p.y))
        rebuildTopology()  // refresh the element's pin value + action name
        applyTransform()
    }

    private func select(_ id: UInt64, announce: Bool = true) {
        appState?.graphDiagramSelect(id, announce: announce)
        scrollNodeIntoView(id)
        updateSelectionIndicator()
    }

    /// Keep the node inside the viewport (WCAG 2.4.11); silent.
    private func scrollNodeIntoView(_ id: UInt64) {
        guard let viewport, let p = positions[id], bounds.width > 0 else { return }
        let c = layoutToView(p)
        let margin: CGFloat = 48
        var off = viewport.offset
        if c.x < margin { off.x -= (margin - c.x) / viewport.scale }
        if c.y < margin { off.y -= (margin - c.y) / viewport.scale }
        if c.x > bounds.width - margin { off.x += (c.x - (bounds.width - margin)) / viewport.scale }
        if c.y > bounds.height - margin { off.y += (c.y - (bounds.height - margin)) / viewport.scale }
        if off != viewport.offset {
            viewport.offset = off
            applyTransform()
        }
    }

    // MARK: Mouse + hover

    override func updateTrackingAreas() {
        super.updateTrackingAreas()
        if let trackingArea { removeTrackingArea(trackingArea) }
        let area = NSTrackingArea(
            rect: bounds, options: [.mouseMoved, .activeInKeyWindow, .inVisibleRect],
            owner: self, userInfo: nil)
        addTrackingArea(area)
        trackingArea = area
    }

    override func mouseMoved(with event: NSEvent) {
        let viewPoint = convert(event.locationInWindow, from: nil)
        guard let id = hitTest(atViewPoint: viewPoint), let node = model?.node(id) else {
            toolTip = nil
            return
        }
        // Hover tooltip: "label — n in / m out" (spec §P2-3).
        toolTip = "\(node.label) — \(node.inLinks) in / \(node.outLinks) out"
    }

    override func mouseDown(with event: NSEvent) {
        window?.makeFirstResponder(self)
        let viewPoint = convert(event.locationInWindow, from: nil)
        guard let hit = hitTest(atViewPoint: viewPoint) else { return }
        if event.clickCount >= 2 {
            select(hit, announce: false)
            activate(hit)
        } else {
            select(hit)
        }
    }

    override func scrollWheel(with event: NSEvent) {
        guard let viewport else { return }
        viewport.offset.x -= event.scrollingDeltaX / viewport.scale
        viewport.offset.y -= event.scrollingDeltaY / viewport.scale
        applyTransform()  // transform only — no re-layout
    }

    override func magnify(with event: NSEvent) {
        guard let viewport else { return }
        viewport.setScale(viewport.scale * (1 + event.magnification))
        applyTransform()
    }

    @objc private func frameChanged() {
        viewport?.viewSize = bounds.size
        if !didInitialFit, !positions.isEmpty {
            didInitialFit = true
            model?.fitToContent()
        }
        applyTransform()
    }

    override func viewDidChangeEffectiveAppearance() {
        super.viewDidChangeEffectiveAppearance()
        rebuildTopology()
        applyTransform()
    }

    // MARK: Keyboard

    override func keyDown(with event: NSEvent) {
        guard let model else { return super.keyDown(with: event) }
        let flags = event.modifierFlags

        // ⌘=/⌘−/⌘0 and ⌥⌘0 (fit graph) are owned by the focus-routed menu
        // items (one owner per chord, #848) — never handled here. ⌃⌘I
        // "Where am I?" is a surface action (canvas precedent).
        if flags.contains(.command), flags.contains(.control),
            event.charactersIgnoringModifiers?.lowercased() == "i"
        {
            appState?.graphDiagramWhereAmI()
            return
        }

        switch event.keyCode {
        case 125: spatialMove(dx: 0, dy: 1); return  // ↓
        case 126: spatialMove(dx: 0, dy: -1); return  // ↑
        case 123: spatialMove(dx: -1, dy: 0); return  // ←
        case 124: spatialMove(dx: 1, dy: 0); return  // →
        case 48: structuralMove(forward: !flags.contains(.shift)); return  // Tab / ⇧Tab
        case 36, 76:  // Return / Enter
            if let sel = model.selection { activate(sel) }
            return
        default:
            if let chars = event.charactersIgnoringModifiers, !chars.isEmpty,
                flags.isDisjoint(with: [.command, .control, .option]),
                chars.first!.isLetter || chars.first!.isNumber
            {
                typeAhead(chars)
                return
            }
            super.keyDown(with: event)
        }
    }

    /// Spatial nearest-neighbor move (MIT-VIS): the selected node's graph
    /// neighbors FIRST, falling back to all nodes; pick the best-aligned
    /// with the arrow direction (angle then distance, id tie-break).
    func spatialMove(dx: CGFloat, dy: CGFloat) {
        guard let model else { return }
        guard let current = model.selection, let from = positions[current] else {
            if let first = model.nodeIDs.first { select(first) }
            return
        }
        let dir = CGVector(dx: dx, dy: dy)
        if let best = bestInDirection(
            from: from, dir: dir, candidates: graphNeighbors(of: current, model: model), model: model)
        {
            select(best)
            return
        }
        if let best = bestInDirection(
            from: from, dir: dir, candidates: model.nodeIDs.filter { $0 != current }, model: model)
        {
            select(best)
        }
    }

    private func graphNeighbors(of id: UInt64, model: GraphDiagramModel) -> [UInt64] {
        var out: [UInt64] = []
        for edge in model.edges {
            if edge.sourceId == id { out.append(edge.targetId) }
            if edge.targetId == id { out.append(edge.sourceId) }
        }
        return out
    }

    func bestInDirection(
        from: CGPoint, dir: CGVector, candidates: [UInt64], model: GraphDiagramModel
    ) -> UInt64? {
        var best: (id: UInt64, score: CGFloat)?
        for id in candidates {
            guard let p = positions[id] else { continue }
            let vx = p.x - from.x
            let vy = p.y - from.y
            let dist = hypot(vx, vy)
            guard dist > 0.0001 else { continue }
            let proj = (vx * dir.dx + vy * dir.dy) / dist  // cosθ
            guard proj > 0.1 else { continue }
            let score = dist / max(proj, 0.0001)
            if let b = best {
                if score < b.score || (score == b.score && id < b.id) { best = (id, score) }
            } else {
                best = (id, score)
            }
        }
        return best?.id
    }

    /// Tab/⇧Tab: next/previous node in KEY order (structural), wrapping.
    func structuralMove(forward: Bool) {
        guard let model, !model.nodeIDs.isEmpty else { return }
        let ids = model.nodeIDs
        guard let current = model.selection, let idx = ids.firstIndex(of: current) else {
            select(forward ? ids.first! : ids.last!)
            return
        }
        select(ids[forward ? (idx + 1) % ids.count : (idx - 1 + ids.count) % ids.count])
    }

    func typeAhead(_ chars: String) {
        guard let model else { return }
        let now = Date().timeIntervalSinceReferenceDate
        if now - typeAheadStamp > 1.0 { typeAheadBuffer = "" }
        typeAheadStamp = now
        typeAheadBuffer += chars.lowercased()
        let prefix = typeAheadBuffer
        if let match = model.nodeIDs.first(where: {
            (model.node($0)?.label.lowercased().hasPrefix(prefix)) == true
        }) {
            select(match)
        }
    }

    // MARK: Test seams

    func nodeFramesForTesting() -> [UInt64: CGRect] {
        var out: [UInt64: CGRect] = [:]
        for (id, layer) in nodeLayers { out[id] = layer.frame }
        return out
    }

    /// A materialized node's fill + ring dash pattern (group styling test).
    func nodeStyleForTesting(nodeId: UInt64) -> (fill: CGColor?, dash: [NSNumber]?)? {
        guard let layer = nodeLayers[nodeId] else { return nil }
        return (layer.fillColor, layer.lineDashPattern)
    }

    func axLabelsForTesting() -> [String] { axElements.compactMap { $0.accessibilityLabel() } }

    func axChildCountForTesting() -> Int { (accessibilityChildren()?.count) ?? 0 }

    func axCustomActionNamesForTesting() -> [String] {
        (axElements.first?.accessibilityCustomActions() ?? []).map { $0.name }
    }

    func axCustomActionNamesForTesting(nodeId: UInt64) -> [String] {
        guard let el = axElements.first(where: { $0.nodeId == nodeId }) else { return [] }
        return (el.accessibilityCustomActions() ?? []).map { $0.name }
    }

    /// Feed a frame directly (tests the generation/size guard in
    /// `applyFrame`): a mismatched-generation or wrong-size frame is
    /// dropped, a matching one is applied.
    func applyFrameForTesting(_ frame: LayoutFrame) { applyFrame(frame) }

    func axNeighborContentForTesting(nodeIndex: Int = 0) -> String? {
        guard axElements.indices.contains(nodeIndex) else { return nil }
        return axElements[nodeIndex].accessibilityCustomContent.first?.value
    }

    func summaryHasSwitchActionForTesting() -> Bool {
        (summaryElement?.accessibilityCustomActions() ?? []).contains { $0.name == "Switch to Table" }
    }

    @discardableResult
    func performSummaryPressForTesting() -> Bool {
        summaryElement?.accessibilityPerformPress() ?? false
    }

    func axRoleForTesting(nodeIndex: Int = 0) -> NSAccessibility.Role? {
        guard axElements.indices.contains(nodeIndex) else { return nil }
        return axElements[nodeIndex].accessibilityRole()
    }

    func performPressForTesting(nodeIndex: Int = 0) {
        guard axElements.indices.contains(nodeIndex) else { return }
        _ = axElements[nodeIndex].accessibilityPerformPress()
    }

    func isTierBForTesting() -> Bool { model?.isTierB ?? false }

    func tickOnceForTesting() {
        guard let session = model?.session else { return }
        applyFrame(session.tick(iterations: 20))
    }

    @discardableResult
    func settleReduceMotionForTesting() -> LayoutFrame? {
        guard let session = model?.session else { return nil }
        let frame = session.runToConvergence(cancel: CancelToken())
        applyFrame(frame)
        return frame
    }

    func injectPositionsForTesting(_ p: [UInt64: CGPoint]) {
        positions = p
        model?.contentBounds = positionsBounds()
        rebuildTopology()
        applyTransform()
    }

    func selectionForTesting() -> UInt64? { model?.selection }

    func spatialMoveForTesting(dx: CGFloat, dy: CGFloat) { spatialMove(dx: dx, dy: dy) }

    func hitTestForTesting(atViewPoint p: CGPoint) -> UInt64? { hitTest(atViewPoint: p) }
}
