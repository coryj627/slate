// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import Foundation

/// Graph tab, Diagram mode (Milestone P, P2-3 #559): the visual
/// projection of the whole-graph. Builds a `LayoutSession` under the same
/// backend filter the Table mode uses and joins the layout's node ids to
/// the snapshot's `GraphNode`s for accessible labels and actions. The
/// heavy rendering + per-node accessibility live in `GraphDiagramView`;
/// this extension owns the model lifecycle, selection, and the actions
/// the diagram shares with the Table/Connections projections.
extension AppState {
    /// Build the diagram model for the current backend filter if it isn't
    /// already live (the Graph tab entered Diagram mode). Reuses
    /// `graphTableFilter` so Table ↔ Diagram cover the same node set.
    func ensureGraphDiagram() {
        guard graphDiagramModel == nil, !graphDiagramLoading else { return }
        buildGraphDiagram()
    }

    /// (Re)build the layout session + metadata join off the main actor,
    /// then publish the model. The snapshot (for labels/counts) and the
    /// layout are fetched together so their ids share a generation; a rare
    /// mid-build mutation is reconciled by the later generation refresh.
    func buildGraphDiagram() {
        guard let session = currentSession else {
            graphDiagramModel = nil
            return
        }
        graphDiagramBuildSeq += 1
        let seq = graphDiagramBuildSeq
        let filter = graphTableFilter
        graphDiagramLoading = true
        graphDiagramError = nil

        Task { [weak self] in
            let result:
                Result<(LayoutSession, [UInt64], [GraphEdge], [GraphNode], UInt64), VaultError> =
                    await Task.detached(priority: .userInitiated) {
                        do {
                            // ONE atomic build: `start_layout` computes the
                            // topology AND the node metadata under a single
                            // graph lock, so ids/edges/metadata/generation
                            // can never disagree on generation — no separate
                            // snapshot, no handshake (round 2 finding 2).
                            let layout = try session.startGraphLayout(
                                filter: filter, forces: LayoutForces(), config: LayoutConfig())
                            return .success((
                                layout, layout.nodeIds(), layout.edges(),
                                layout.nodeMetadata(), layout.generation()))
                        } catch let e as VaultError {
                            return .failure(e)
                        } catch {
                            return .failure(.Io(message: error.localizedDescription))
                        }
                    }.value

            guard let self, !Task.isCancelled, self.currentSession === session,
                seq == self.graphDiagramBuildSeq
            else { return }

            self.graphDiagramLoading = false
            switch result {
            case .success(let (layout, ids, edges, nodes, gen)):
                var byID: [UInt64: GraphNode] = [:]
                for node in nodes { byID[node.id] = node }
                // `generation` is the LAYOUT's (what tick frames carry), so
                // the renderer's frame-generation guard lines up.
                self.graphDiagramModel = GraphDiagramModel(
                    session: layout, filter: filter, nodeIDs: ids,
                    nodesByID: byID, edges: edges, generation: gen)
            case .failure(let error):
                self.graphDiagramError = self.humanReadable(error)
                self.graphDiagramModel = nil
            }
        }
    }

    /// Tear down the diagram (graph-tab close, vault open/close, or the
    /// mode toggle returning to Table). Bumps the build sequence so any
    /// in-flight build/refresh publishes nothing.
    func resetGraphDiagramState() {
        graphDiagramBuildSeq += 1
        graphDiagramRefreshTask?.cancel()
        graphDiagramRefreshTask = nil
        graphDiagramModel = nil
        graphDiagramLoading = false
        graphDiagramError = nil
    }

    /// Re-sync the diagram after a graph-generation bump
    /// (`VaultEventListener`): `session.refresh()` returns `nil` when
    /// nothing changed (cheap probe), otherwise the layout warm-updated
    /// and we re-fetch ids/edges + re-join a fresh snapshot's metadata.
    func refreshGraphDiagramIfGraphChanged() {
        guard let model = graphDiagramModel, currentSession != nil else { return }
        // Capture the Sendable layout handle on the main actor before
        // hopping off it (the model itself is main-actor-isolated).
        let layout = model.session
        let session = currentSession!
        let buildSeq = graphDiagramBuildSeq
        // Chain after any in-flight refresh so two file-change probes can't
        // race: the second waits for the first to adopt, then sees the
        // current generation (a no-op if the first already caught up) — no
        // lost adoption / stranding on the old generation (round 2
        // findings 1 & 11).
        let previous = graphDiagramRefreshTask
        graphDiagramRefreshTask = Task { [weak self] in
            await previous?.value
            guard let self, self.currentSession === session,
                buildSeq == self.graphDiagramBuildSeq, self.graphDiagramModel === model
            else { return }

            let synced: ([UInt64], [GraphEdge], [GraphNode], UInt64)? =
                await Task.detached(priority: .utility) {
                    () -> ([UInt64], [GraphEdge], [GraphNode], UInt64)? in
                    // `try?` flattens the throwing Optional: nil = threw OR
                    // generation unchanged (no-op). On a change the layout
                    // warm-updated; ids/edges/metadata are then the ATOMIC
                    // post-update topology (one graph lock) and the frame's
                    // generation matches them — no snapshot handshake.
                    guard let frame = try? layout.refresh() else { return nil }
                    return (
                        layout.nodeIds(), layout.edges(), layout.nodeMetadata(), frame.generation)
                }.value

            guard self.currentSession === session,
                buildSeq == self.graphDiagramBuildSeq,
                self.graphDiagramModel === model,
                let synced,
                synced.3 > model.generation  // monotonic: never regress the topology
            else { return }

            var byID: [UInt64: GraphNode] = [:]
            for node in synced.2 { byID[node.id] = node }
            model.adopt(
                nodeIDs: synced.0, nodesByID: byID, edges: synced.1, generation: synced.3)
        }
    }

    // MARK: Selection + readback

    /// Select a node in the diagram and (unless silent) speak its P1-1
    /// row copy through the graph announcer — the SAME phrasing the Table
    /// speaks. A silent select (VoiceOver landing on the element, which
    /// already speaks it) only moves the ring.
    func graphDiagramSelect(_ id: UInt64, announce: Bool = true) {
        guard let model = graphDiagramModel else { return }
        guard model.selection != id else { return }
        model.selection = id
        if announce, let ref = model.rowRef(id) {
            graphAnnouncer.announce(.rowFocused(ref))
        }
    }

    /// Toggle a node's pin at the layout-space position `(x, y)` it
    /// currently occupies (the renderer passes the live coordinates). A
    /// pinned node stops moving but still repels — the Pin AX action and
    /// the drag-to-place gesture both funnel here.
    func graphDiagramTogglePin(_ id: UInt64, x: Float, y: Float) {
        guard let model = graphDiagramModel else { return }
        if model.pinned.contains(id) {
            model.pinned.remove(id)
            model.session.unpinNode(id: id)
            graphAnnouncer.announce(.status("Unpinned."))
        } else {
            model.pinned.insert(id)
            model.session.pinNode(id: id, x: x, y: y)
            graphAnnouncer.announce(.status("Pinned."))
        }
    }

    /// The ⌃⌘I "Where am I?" readback for the diagram (spec §P2-3): the
    /// selected node's row copy, its component, the zoom level, and the
    /// active backend filters — assembled once and spoken assertively.
    func graphDiagramWhereAmI() {
        guard let model = graphDiagramModel else { return }
        var parts: [String] = []
        if let sel = model.selection, let node = model.node(sel), let ref = model.rowRef(sel) {
            parts.append(graphAnnouncer.rowPhrase(ref))
            parts.append("component \(node.component)")
        } else {
            parts.append("No node selected")
        }
        parts.append("zoom \(model.viewport.zoomPercent) percent")
        parts.append(graphDiagramFilterPhrase(model.filter))
        graphAnnouncer.announce(.summary(parts.joined(separator: ", ") + "."))
    }

    // MARK: Zoom router (join the #848 focus-routed menu owner)

    /// True when the visual diagram is the active graph surface, so the
    /// focus-routed ⌘=/⌘−/⌘0 chords drive the graph viewport (canvas wins
    /// first, then graph, then the editor-text fallback). Non-nil model ⇒
    /// Diagram mode; `graphTabActive` ⇒ the graph tab is frontmost.
    var graphDiagramZoomActive: Bool {
        graphTabActive && graphDiagramModel != nil
    }

    /// The single owner of the ⌘=/⌘−/⌘0 routing decision (#848 + P2-3):
    /// canvas tab → canvas viewport; graph tab in Diagram mode → graph
    /// viewport; else editor text zoom. Extracted so the priority is
    /// unit-testable and the three menu items share ONE decision.
    enum ZoomRouteTarget { case canvas, graph, editor }
    var zoomRouteTarget: ZoomRouteTarget {
        if activeCanvasDocument != nil { return .canvas }
        if graphDiagramZoomActive { return .graph }
        return .editor
    }

    func routedZoomIn() {
        switch zoomRouteTarget {
        case .canvas: canvasZoomIn()
        case .graph: graphDiagramZoomIn()
        case .editor: editorZoomIn()
        }
    }

    func routedZoomOut() {
        switch zoomRouteTarget {
        case .canvas: canvasZoomOut()
        case .graph: graphDiagramZoomOut()
        case .editor: editorZoomOut()
        }
    }

    func routedActualSize() {
        switch zoomRouteTarget {
        case .canvas: canvasActualSize()
        case .graph: graphDiagramActualSize()
        case .editor: editorActualSize()
        }
    }

    func graphDiagramZoomIn() { graphDiagramZoom { $0.zoom(by: CanvasViewport.zoomStep) } }
    func graphDiagramZoomOut() { graphDiagramZoom { $0.zoom(by: 1 / CanvasViewport.zoomStep) } }
    func graphDiagramActualSize() { graphDiagramZoom { $0.setScale(1.0) } }

    private func graphDiagramZoom(_ change: (CanvasViewport) -> Void) {
        guard let viewport = graphDiagramModel?.viewport else { return }
        change(viewport)
        graphAnnouncer.announce(.status("Zoom \(viewport.zoomPercent) percent."))
    }

    /// ⌥⌘0 "Fit Graph" (a new chord — spec §P2-3 / T rule R3): frame every
    /// node. The viewport change is observed by the renderer, which
    /// rebuilds; announced once.
    func graphDiagramFit() {
        guard let model = graphDiagramModel else { return }
        model.fitToContent()
        graphAnnouncer.announce(.status("Fit graph. Zoom \(model.viewport.zoomPercent) percent."))
    }

    /// A spoken description of the active backend filter (Where-am-I).
    func graphDiagramFilterPhrase(_ filter: GraphFilter) -> String {
        var active: [String] = []
        if filter.orphansOnly { active.append("orphans only") }
        if filter.includeAttachments { active.append("attachments shown") }
        active.append(filter.includeGhosts ? "unresolved shown" : "unresolved hidden")
        return "filters: " + active.joined(separator: ", ")
    }
}
