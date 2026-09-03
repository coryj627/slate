// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

/// The visual Diagram mode's model (Milestone P, P2-3 #559): owns the
/// running `LayoutSession`, the id→metadata map for accessible labels and
/// actions, the edge list, and the shared zoom/pan viewport. One per live
/// diagram; `AppState` builds it when the Graph tab enters Diagram mode
/// and tears it down on graph-tab close / vault change.
///
/// "One model, two projections": node metadata comes straight from the
/// same backend `GraphNode`s the Table mode shows, so a node's spoken row
/// copy is byte-identical across projections (the P2-5 groundwork). Only
/// flat position buffers cross the FFI — the solver state stays
/// pointer-side in `session`.
@MainActor
final class GraphDiagramModel: ObservableObject {
    /// The force-layout session driving positions.
    let session: LayoutSession
    /// Shared zoom/pan. Reuses the canvas viewport math verbatim
    /// (view = (point − offset) × scale); the graph's "canvas space" is
    /// the layout's f64 coordinate plane.
    let viewport = CanvasViewport()
    /// The backend filter this layout is bound to (the SAME
    /// `graphTableFilter` Table mode uses, so both projections cover the
    /// same node set).
    let filter: GraphFilter

    /// Node ids in position order — `nodeIDs[i]` names the node whose
    /// coordinates are `frame.positions[2i], [2i+1]`.
    private(set) var nodeIDs: [UInt64]
    /// id → node metadata (label, link counts, kind, path …) for the
    /// accessible row copy and the Open/Show-connections/Pin actions.
    private(set) var nodesByID: [UInt64: GraphNode]
    /// Collapsed edges among `nodeIDs` (id-keyed, deterministic order).
    private(set) var edges: [GraphEdge]
    /// The graph generation `nodeIDs`/`edges` were derived at — a change
    /// means the topology was re-synced and ids may have been reassigned.
    private(set) var generation: UInt64

    /// Selected node id. Diagram-local in P2-3; P2-5 unifies selection
    /// across the three projections. Drives the selection ring and the
    /// ⌃⌘I "Where am I?" readback.
    @Published var selection: UInt64?
    /// Nodes the user pinned (frozen in place; they still repel). Kept
    /// here so the toggle survives re-renders and a `refresh` can carry it.
    @Published var pinned: Set<UInt64> = []

    /// Layout-space bounding box of the current positions — the renderer
    /// updates this each frame so a "fit graph" command (menu ⌥⌘0) can run
    /// without reaching into the view.
    var contentBounds: CGRect = .zero

    /// Tier boundary (spec §P2-3): above this visible-node count the
    /// renderer drops to a tiled summary and routes accessibility to the
    /// Table mode (which always has every node).
    static let tierBThreshold = 1500

    init(
        session: LayoutSession, filter: GraphFilter, nodeIDs: [UInt64],
        nodesByID: [UInt64: GraphNode], edges: [GraphEdge], generation: UInt64
    ) {
        self.session = session
        self.filter = filter
        self.nodeIDs = nodeIDs
        self.nodesByID = nodesByID
        self.edges = edges
        self.generation = generation
    }

    var nodeCount: Int { nodeIDs.count }

    /// Tier B (large graph): summary accessibility + tiled drawing rather
    /// than one layer/element per node (spec §P2-3).
    var isTierB: Bool { nodeCount > Self.tierBThreshold }

    func node(_ id: UInt64) -> GraphNode? { nodesByID[id] }

    /// Fit the whole graph into the viewport (menu ⌥⌘0 / the initial
    /// frame). No-op only when there are genuinely no nodes; a single-node
    /// (or coincident) layout has a zero-SIZE but valid rect, which we
    /// inflate so ⌥⌘0 still frames it rather than leaving it clipped at
    /// the origin (round 1 finding 8).
    func fitToContent() {
        guard nodeCount > 0 else { return }
        var rect = contentBounds
        if rect.width <= 0 && rect.height <= 0 {
            rect = rect.insetBy(dx: -100, dy: -100)
        }
        viewport.fit(rect: rect, padding: 60)
    }

    /// The `GraphRowRef` (P1-1 verbatim VoiceOver copy) for a node — the
    /// SAME phrasing the Table and Connections surfaces speak, so a node
    /// sounds identical in every projection.
    func rowRef(_ id: UInt64) -> GraphRowRef? {
        guard let n = nodesByID[id] else { return nil }
        return GraphRowRef(
            label: n.label,
            linksIn: n.inLinks,
            linksOut: n.outLinks,
            isGhost: n.kind == .ghost,
            references: n.inLinks,
            isEmbed: false)
    }

    /// Adopt a fresh topology after `session.refresh()` re-synced the
    /// layout to a new generation (nodes added/removed, ids possibly
    /// reassigned). Drops a selection/pin whose node no longer exists.
    func adopt(
        nodeIDs: [UInt64], nodesByID: [UInt64: GraphNode], edges: [GraphEdge],
        generation: UInt64
    ) {
        objectWillChange.send()
        self.nodeIDs = nodeIDs
        self.nodesByID = nodesByID
        self.edges = edges
        self.generation = generation
        if let sel = selection, nodesByID[sel] == nil { selection = nil }
        pinned = pinned.filter { nodesByID[$0] != nil }
    }
}
