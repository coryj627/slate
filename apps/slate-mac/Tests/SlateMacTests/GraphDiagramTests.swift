// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// #559 acceptance: the visual Diagram mode over a real vault — per-node
/// AX elements with the P1-1 row copy + roles + actions + neighbor
/// custom-content, the EXACT tier boundary and its summary→Table switch,
/// deterministic keyboard spatial navigation on a fixed layout, the
/// Reduce-Motion single-frame path, single-node fit, spatial-grid
/// hit-testing, and APCA measurements in both appearances.
@MainActor
final class GraphDiagramTests: XCTestCase {
    private var tempDir: URL!
    /// The view holds `appState` weakly (production design), so tests must
    /// retain it or `axLabel` falls back to the bare node label.
    private var retainedStates: [AppState] = []

    override func setUpWithError() throws {
        try super.setUpWithError()
        tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-graph-diagram-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: tempDir, withIntermediateDirectories: true)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: tempDir)
        retainedStates.removeAll()
        try super.tearDownWithError()
    }

    private var filter: GraphFilter {
        GraphFilter(includeAttachments: false, includeGhosts: true, orphansOnly: false)
    }

    /// A small fixed link graph: a → b, a → c, b → c, and an orphan d.
    private func makeSession() throws -> VaultSession {
        let vault = tempDir.appendingPathComponent("vault-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        try "# A\n[[b]] and [[c]]\n".write(
            to: vault.appendingPathComponent("a.md"), atomically: true, encoding: .utf8)
        try "# B\n[[c]]\n".write(
            to: vault.appendingPathComponent("b.md"), atomically: true, encoding: .utf8)
        try "# C\n".write(
            to: vault.appendingPathComponent("c.md"), atomically: true, encoding: .utf8)
        try "# D orphan\n".write(
            to: vault.appendingPathComponent("d.md"), atomically: true, encoding: .utf8)
        let session = try VaultSession.openFilesystem(rootPath: vault.path)
        try session.scanInitial(cancel: CancelToken())
        return session
    }

    private func makeModel(_ session: VaultSession) throws -> GraphDiagramModel {
        let snap = try session.graphSnapshot(filter: filter)
        let layout = try session.startGraphLayout(
            filter: filter, forces: LayoutForces(), config: LayoutConfig())
        var byID: [UInt64: GraphNode] = [:]
        for node in snap.nodes { byID[node.id] = node }
        // Model generation tracks the LAYOUT's (what frames carry), so the
        // renderer's frame-generation guard lines up (as production does).
        let gen = layout.tick(iterations: 0).generation
        return GraphDiagramModel(
            session: layout, filter: filter, nodeIDs: layout.nodeIds(),
            nodesByID: byID, edges: layout.edges(), generation: gen)
    }

    private func makeView(
        _ model: GraphDiagramModel, reduceMotion: Bool = false,
        onSwitchToTable: @escaping () -> Void = {}
    ) -> (AppState, GraphDiagramNSView) {
        let store = RecentVaultsStore(
            fileURL: tempDir.appendingPathComponent("recents-\(UUID().uuidString).json"))
        let state = AppState(recentsStore: store, externalOpener: { _ in true })
        state.graphDiagramModel = model
        retainedStates.append(state)
        let tabID = state.workspace.openTab(.graph, activate: true)
        let view = GraphDiagramNSView(frame: NSRect(x: 0, y: 0, width: 800, height: 600))
        view.configure(
            model: model, appState: state, tabID: tabID, reduceMotion: reduceMotion,
            onSwitchToTable: onSwitchToTable)
        view.stopSettling()  // drive frames synchronously in tests
        return (state, view)
    }

    private func idByPath(_ session: VaultSession) throws -> [String: UInt64] {
        var out: [String: UInt64] = [:]
        for n in try session.graphSnapshot(filter: filter).nodes {
            if let p = n.path { out[p] = n.id }
        }
        return out
    }

    // MARK: Per-node accessibility (tier A)

    func testTierAMaterializesEveryNodeWithRowCopyRoleAndActions() throws {
        let session = try makeSession()
        let model = try makeModel(session)
        let (_, view) = makeView(model)
        view.tickOnceForTesting()

        XCTAssertEqual(view.axChildCountForTesting(), model.nodeCount)
        XCTAssertFalse(view.isTierBForTesting())
        XCTAssertEqual(view.axRoleForTesting(), .button)

        let labels = view.axLabelsForTesting()
        XCTAssertTrue(labels.contains("a, 0 links in, 2 links out"))
        XCTAssertTrue(labels.contains("c, 2 links in, 0 links out"))

        // A note exposes Show connections + Pin; a neighbor is listed in
        // custom content.
        let actions = view.axCustomActionNamesForTesting()
        XCTAssertTrue(actions.contains("Show connections"))
        XCTAssertTrue(actions.contains("Pin") || actions.contains("Unpin"))
        XCTAssertNotNil(view.axNeighborContentForTesting())

        XCTAssertEqual(view.nodeFramesForTesting().count, model.nodeCount)
    }

    func testTierARemainsCompleteAfterAPan() throws {
        // The whole ≤1,500 set stays materialized regardless of the
        // viewport — a pan must never drop nodes from the AX tree.
        let session = try makeSession()
        let model = try makeModel(session)
        let (_, view) = makeView(model)
        view.tickOnceForTesting()
        let before = view.axChildCountForTesting()
        model.viewport.offset = CGPoint(x: 100_000, y: 100_000)  // pan far away
        view.applyTransform()
        XCTAssertEqual(view.axChildCountForTesting(), before, "a pan never drops AX nodes")
    }

    func testGhostNodeOmitsShowConnectionsButANoteHasIt() throws {
        // A note links an unresolved target: the note's element carries
        // "Show connections"; the ghost's does NOT (nothing to re-root on).
        let vault = tempDir.appendingPathComponent("ghost-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        try "# A\n[[Missing Target]]\n".write(
            to: vault.appendingPathComponent("a.md"), atomically: true, encoding: .utf8)
        let session = try VaultSession.openFilesystem(rootPath: vault.path)
        try session.scanInitial(cancel: CancelToken())
        let model = try makeModel(session)
        let (_, view) = makeView(model)
        view.tickOnceForTesting()

        let ghost = try XCTUnwrap(model.nodesByID.values.first { $0.kind == .ghost })
        let note = try XCTUnwrap(model.nodesByID.values.first { $0.kind == .note })
        XCTAssertFalse(
            view.axCustomActionNamesForTesting(nodeId: ghost.id).contains("Show connections"),
            "a ghost exposes no Show connections action")
        XCTAssertTrue(
            view.axCustomActionNamesForTesting(nodeId: note.id).contains("Show connections"),
            "a real note does")
    }

    // MARK: Tier switch (exact boundary) + summary routing

    func testTierBoundaryIsInclusiveAt1500AndSwitchesAt1501() throws {
        let session = try makeSession()
        let base = try makeModel(session)
        func model(_ count: Int) -> GraphDiagramModel {
            let ids = Array(UInt64(1)...UInt64(count))
            var byID: [UInt64: GraphNode] = [:]
            for id in ids {
                byID[id] = GraphNode(
                    id: id, path: "n\(id).md", label: "n\(id)", kind: .note, inLinks: 0,
                    outLinks: 0, inEmbeds: 0, outEmbeds: 0, component: 0, isOrphan: true,
                    pagerank: 0, modifiedMs: nil)
            }
            return GraphDiagramModel(
                session: base.session, filter: filter, nodeIDs: ids, nodesByID: byID,
                edges: [], generation: 1)
        }
        XCTAssertFalse(model(GraphDiagramModel.tierBThreshold).isTierB, "1500 is Tier A")
        XCTAssertTrue(model(GraphDiagramModel.tierBThreshold + 1).isTierB, "1501 is Tier B")
    }

    func testTierBSummaryElementSwitchesToTable() throws {
        let session = try makeSession()
        let base = try makeModel(session)
        let ids = Array(UInt64(1)...UInt64(GraphDiagramModel.tierBThreshold + 1))
        var byID: [UInt64: GraphNode] = [:]
        var positions: [UInt64: CGPoint] = [:]
        for (i, id) in ids.enumerated() {
            byID[id] = GraphNode(
                id: id, path: "n\(id).md", label: "n\(id)", kind: .note, inLinks: 0,
                outLinks: 0, inEmbeds: 0, outEmbeds: 0, component: 0, isOrphan: true,
                pagerank: 0, modifiedMs: nil)
            positions[id] = CGPoint(x: Double(i), y: Double(i))
        }
        let big = GraphDiagramModel(
            session: base.session, filter: filter, nodeIDs: ids, nodesByID: byID, edges: [],
            generation: 1)
        var switched = false
        let (_, view) = makeView(big, onSwitchToTable: { switched = true })
        view.injectPositionsForTesting(positions)
        XCTAssertTrue(view.isTierBForTesting())
        XCTAssertEqual(view.axChildCountForTesting(), 1, "one summary element, not 1,501 nodes")
        XCTAssertTrue(view.summaryHasSwitchActionForTesting())
        XCTAssertTrue(view.performSummaryPressForTesting())
        XCTAssertTrue(switched, "the summary's press switches to Table mode")
    }

    // MARK: Keyboard spatial navigation (fixed layout)

    func testSpatialMoveNeighborsFirstThenFallbackOnAFixedLayout() throws {
        let session = try makeSession()
        let model = try makeModel(session)
        let ids = try idByPath(session)
        let (state, view) = makeView(model)
        // Fixed layout: a at origin, b to the right, c below, d far right.
        // a's graph neighbors are b and c (a→b, a→c); d is an orphan.
        let layout: [UInt64: CGPoint] = [
            ids["a.md"]!: CGPoint(x: 0, y: 0),
            ids["b.md"]!: CGPoint(x: 100, y: 0),
            ids["c.md"]!: CGPoint(x: 0, y: 100),
            ids["d.md"]!: CGPoint(x: 300, y: 0),
        ]
        view.injectPositionsForTesting(layout)

        // → from a: among neighbors {b (right), c (down)}, b wins.
        state.graphDiagramSelect(ids["a.md"]!, announce: false)
        view.spatialMoveForTesting(dx: 1, dy: 0)
        XCTAssertEqual(view.selectionForTesting(), ids["b.md"], "→ picks the right-hand neighbor")

        // ↓ from a: among neighbors, c wins.
        state.graphDiagramSelect(ids["a.md"]!, announce: false)
        view.spatialMoveForTesting(dx: 0, dy: 1)
        XCTAssertEqual(view.selectionForTesting(), ids["c.md"], "↓ picks the below neighbor")

        // ← from d (orphan, no neighbors): fall back to nearest visible
        // node to the left — b (100,0) is nearer than a (0,0).
        state.graphDiagramSelect(ids["d.md"]!, announce: false)
        view.spatialMoveForTesting(dx: -1, dy: 0)
        XCTAssertEqual(view.selectionForTesting(), ids["b.md"], "fallback to nearest in-direction node")
    }

    // MARK: Reduce Motion

    func testReduceMotionAppliesASingleConvergedFrame() throws {
        let session = try makeSession()
        let model = try makeModel(session)
        let (_, view) = makeView(model, reduceMotion: true)
        let frame = try XCTUnwrap(view.settleReduceMotionForTesting())
        XCTAssertGreaterThan(frame.iteration, 0)
        XCTAssertEqual(view.axChildCountForTesting(), model.nodeCount)
    }

    // MARK: Hit-testing (spatial grid)

    func testGridHitTestFindsTheNodeUnderAPoint() throws {
        let session = try makeSession()
        let model = try makeModel(session)
        let ids = try idByPath(session)
        let (_, view) = makeView(model)
        view.injectPositionsForTesting([
            ids["a.md"]!: CGPoint(x: 0, y: 0),
            ids["b.md"]!: CGPoint(x: 400, y: 300),
        ])
        // At 100% zoom centered on origin, layout ≈ view near the node.
        model.viewport.scale = 1
        model.viewport.offset = .zero
        view.applyTransform()
        XCTAssertEqual(view.hitTestForTesting(atViewPoint: CGPoint(x: 0, y: 0)), ids["a.md"])
        XCTAssertNil(view.hitTestForTesting(atViewPoint: CGPoint(x: 200, y: 200)), "empty space")
    }

    // MARK: Model invariants

    func testAdoptDropsStaleSelectionAndPins() throws {
        let session = try makeSession()
        let model = try makeModel(session)
        let gone = model.nodeIDs.first!
        model.selection = gone
        model.pinned.insert(gone)
        model.adopt(nodeIDs: [], nodesByID: [:], edges: [], generation: 999)
        XCTAssertNil(model.selection)
        XCTAssertTrue(model.pinned.isEmpty)
        XCTAssertEqual(model.generation, 999)
    }

    func testSingleNodeFitFramesTheNode() throws {
        let vault = tempDir.appendingPathComponent("solo-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        try "# Solo\n".write(
            to: vault.appendingPathComponent("solo.md"), atomically: true, encoding: .utf8)
        let session = try VaultSession.openFilesystem(rootPath: vault.path)
        try session.scanInitial(cancel: CancelToken())
        let model = try makeModel(session)
        XCTAssertEqual(model.nodeCount, 1)
        let (_, view) = makeView(model)
        view.tickOnceForTesting()
        model.viewport.scale = 99  // clearly non-fit
        view.fitGraph()
        // A single node's zero-size bounds is inflated + fit, so the scale
        // lands at a sane fitted value (not the pre-fit 99, not a no-op).
        XCTAssertNotEqual(model.viewport.scale, 99, "single-node fit is not a no-op")
        XCTAssertLessThanOrEqual(model.viewport.scale, CanvasViewport.maxScale)
    }

    func testNodeDiameterMatchesTheSpec() {
        XCTAssertEqual(GraphDiagramNSView.nodeDiameter(inLinks: 0), 8, accuracy: 0.001)
        XCTAssertLessThanOrEqual(GraphDiagramNSView.nodeDiameter(inLinks: 100_000), 28)
        XCTAssertGreaterThan(
            GraphDiagramNSView.nodeDiameter(inLinks: 5),
            GraphDiagramNSView.nodeDiameter(inLinks: 1))
    }

    // MARK: Zoom router

    func testZoomRouterInactiveWithoutADiagram() throws {
        let store = RecentVaultsStore(
            fileURL: tempDir.appendingPathComponent("recents-\(UUID().uuidString).json"))
        let state = AppState(recentsStore: store, externalOpener: { _ in true })
        XCTAssertFalse(state.graphDiagramZoomActive)
        XCTAssertEqual(state.graphDiagramFilterPhrase(filter), "filters: unresolved shown")
    }

    func testZoomRoutePriorityGraphVsEditor() throws {
        // No diagram (no canvas either) ⇒ chords fall through to the editor.
        let store = RecentVaultsStore(
            fileURL: tempDir.appendingPathComponent("recents-\(UUID().uuidString).json"))
        let bare = AppState(recentsStore: store, externalOpener: { _ in true })
        XCTAssertEqual(bare.zoomRouteTarget, .editor)

        // A graph tab active + a live diagram model ⇒ the chords drive the
        // graph viewport (ahead of the editor fallback; the single routed
        // decision is the one menu owner). No canvas is open, so canvas
        // never wins here — its priority is unchanged from #848.
        let session = try makeSession()
        let model = try makeModel(session)
        let (state, _) = makeView(model)  // opens + activates a graph tab
        XCTAssertNil(state.activeCanvasDocument)
        XCTAssertTrue(state.graphDiagramZoomActive)
        XCTAssertEqual(state.zoomRouteTarget, .graph)
    }

    func testZoomRoutePrefersCanvasOverGraph() async throws {
        // An active canvas tab wins the zoom chords even if a graph
        // diagram exists elsewhere — canvas → graph → editor priority.
        let vault = tempDir.appendingPathComponent("canvasroute-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        try #"{"nodes":[{"id":"c1","type":"text","text":"Card","x":0,"y":0,"width":200,"height":100}],"edges":[]}"#
            .write(to: vault.appendingPathComponent("board.canvas"), atomically: true, encoding: .utf8)
        let store = RecentVaultsStore(
            fileURL: tempDir.appendingPathComponent("recents-\(UUID().uuidString).json"))
        let state = AppState(recentsStore: store, externalOpener: { _ in true })
        retainedStates.append(state)
        state.openVault(at: vault)
        await state.scanTask?.value
        state.openFile("board.canvas", target: .currentTab)
        XCTAssertNotNil(state.activeCanvasDocument, "a canvas tab is active")
        XCTAssertEqual(state.zoomRouteTarget, .canvas)
    }

    func testWhereAmIRoutePriorityGraphReachable() throws {
        // The bug: the always-enabled "Where Am I?" menu item swallowed ⌃⌘I
        // and only ran the canvas readback (a no-op off a canvas tab), so the
        // graph diagram's readback was unreachable. The fix routes ⌃⌘I like
        // the zoom chords. No surface ⇒ .none (harmless no-op, not the old
        // canvas no-op); a live graph diagram ⇒ .graph (now reachable).
        let store = RecentVaultsStore(
            fileURL: tempDir.appendingPathComponent("recents-\(UUID().uuidString).json"))
        let bare = AppState(recentsStore: store, externalOpener: { _ in true })
        XCTAssertEqual(bare.whereAmIRouteTarget, .none)

        let session = try makeSession()
        let model = try makeModel(session)
        let (state, _) = makeView(model)  // opens + activates a graph tab
        XCTAssertNil(state.activeCanvasDocument)
        XCTAssertTrue(state.graphDiagramZoomActive)
        XCTAssertEqual(state.whereAmIRouteTarget, .graph)
    }

    func testWhereAmIRoutePrefersCanvasOverGraph() async throws {
        // canvas → graph → bases priority (mirrors the zoom router): an
        // active canvas tab wins ⌃⌘I even if a graph diagram exists.
        let vault = tempDir.appendingPathComponent("wai-canvas-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        try #"{"nodes":[{"id":"c1","type":"text","text":"Card","x":0,"y":0,"width":200,"height":100}],"edges":[]}"#
            .write(to: vault.appendingPathComponent("board.canvas"), atomically: true, encoding: .utf8)
        let store = RecentVaultsStore(
            fileURL: tempDir.appendingPathComponent("recents-\(UUID().uuidString).json"))
        let state = AppState(recentsStore: store, externalOpener: { _ in true })
        retainedStates.append(state)
        state.openVault(at: vault)
        await state.scanTask?.value
        state.openFile("board.canvas", target: .currentTab)
        XCTAssertNotNil(state.activeCanvasDocument)
        XCTAssertEqual(state.whereAmIRouteTarget, .canvas)
    }

    func testApplyFrameDropsMismatchedGenerationFrame() throws {
        let session = try makeSession()
        let model = try makeModel(session)
        let (_, view) = makeView(model)
        view.tickOnceForTesting()  // applies a real (matching-generation) frame
        let n = model.nodeCount
        let before = view.nodeFramesForTesting()

        // A frame from a DIFFERENT generation is dropped (no mis-assignment
        // of coordinates to the wrong nodes after a warm_update).
        let stale = LayoutFrame(
            positions: Array(repeating: Float(0), count: n * 2), iteration: 9,
            converged: false, generation: model.generation &+ 999)
        view.applyFrameForTesting(stale)
        XCTAssertEqual(
            view.nodeFramesForTesting(), before, "a stale-generation frame is ignored")

        // A same-generation, correctly-sized frame IS applied.
        var pos: [Float] = []
        for i in 0..<n { pos.append(Float(i) * 37); pos.append(Float(i) * -19) }
        let fresh = LayoutFrame(
            positions: pos, iteration: 10, converged: false, generation: model.generation)
        view.applyFrameForTesting(fresh)
        XCTAssertNotEqual(
            view.nodeFramesForTesting(), before, "a matching-generation frame is applied")
    }

    // MARK: P2-4 — filters / display / groups / forces

    func testNameFilterHidesNonMatchingNodesInDiagram() throws {
        let session = try makeSession()
        let model = try makeModel(session)
        let (state, view) = makeView(model)
        state.graphTableTextFilter = "a"  // only the note "a" matches
        view.tickOnceForTesting()
        let visible = Set(view.nodeFramesForTesting().keys)
        let snapshot = try session.graphSnapshot(filter: filter)
        let aID = snapshot.nodes.first { $0.label == "a" }!.id
        XCTAssertEqual(visible, [aID], "only the name-matching node is materialized")
    }

    func testFilterEquivalenceTableAndDiagramShareOnePredicate() throws {
        let session = try makeSession()
        let snapshot = try session.graphSnapshot(filter: filter)
        let needle = "c"
        // Table's visible set (its filteredRows use the same static).
        let tableVisible = Set(
            snapshot.nodes.filter { AppState.graphNameMatches($0.label, needle: needle) }.map(\.id))
        // Diagram's visible set (the renderer applies the same static).
        let model = try makeModel(session)
        let (state, view) = makeView(model)
        state.graphTableTextFilter = needle
        view.tickOnceForTesting()
        XCTAssertEqual(
            Set(view.nodeFramesForTesting().keys), tableVisible,
            "one name predicate ⇒ one node set across projections")
    }

    func testNodeSizeMultiplierScalesTheDrawnDiameter() throws {
        let session = try makeSession()
        let model = try makeModel(session)
        let (state, view) = makeView(model)
        view.tickOnceForTesting()
        let id = model.nodeIDs.first!
        let base = try XCTUnwrap(view.nodeFramesForTesting()[id]).width
        state.graphConfig.display.nodeSizeMultiplier = 2.0
        view.tickOnceForTesting()
        let scaled = try XCTUnwrap(view.nodeFramesForTesting()[id]).width
        XCTAssertEqual(scaled, base * 2, accuracy: 0.01)
    }

    func testGroupColoursMatchingNodesWithADistinctRing() throws {
        let session = try makeSession()
        let model = try makeModel(session)
        let (state, view) = makeView(model)
        state.graphConfig.groups = [
            GraphGroup(query: "a", colorToken: .green, ringStyle: .dashed)
        ]
        view.tickOnceForTesting()
        let snapshot = try session.graphSnapshot(filter: filter)
        let aID = snapshot.nodes.first { $0.label == "a" }!.id
        let style = try XCTUnwrap(view.nodeStyleForTesting(nodeId: aID))
        XCTAssertEqual(style.fill, GraphColorToken.green.color.cgColor, "group colour applied")
        XCTAssertEqual(style.dash, GraphRingStyle.dashed.dashPattern, "group ring style applied")
        // A non-matching node keeps the default (no dashed group ring).
        let dID = snapshot.nodes.first { $0.label == "d" }!.id
        XCTAssertNotEqual(
            view.nodeStyleForTesting(nodeId: dID)?.fill, GraphColorToken.green.color.cgColor)
    }

    func testGroupedNodeRingIsThickerThanUngroupedEvenWhenSolid() throws {
        // A SOLID-ring group must still be distinguishable from an
        // ungrouped node WITHOUT relying on colour — the ring is heavier
        // (review finding 7a, WCAG 1.4.1). This is the case the reviewer
        // flagged: solid dash == ungrouped dash, so width must differ.
        let session = try makeSession()
        let model = try makeModel(session)
        let (state, view) = makeView(model)
        state.graphConfig.groups = [
            GraphGroup(query: "a", colorToken: .green, ringStyle: .solid)
        ]
        view.tickOnceForTesting()
        let snap = try session.graphSnapshot(filter: filter)
        let aID = snap.nodes.first { $0.label == "a" }!.id
        let dID = snap.nodes.first { $0.label == "d" }!.id
        let grouped = try XCTUnwrap(view.nodeLineWidthForTesting(nodeId: aID))
        let ungrouped = try XCTUnwrap(view.nodeLineWidthForTesting(nodeId: dID))
        XCTAssertGreaterThan(
            grouped, ungrouped, "a solid-ring group reads without colour via a heavier ring")
    }

    func testNameFilterCollapsesTierBToTheVisibleSet() throws {
        // A name filter that drops a Tier-B graph below the threshold must
        // render Tier A over exactly the matching nodes — the tier
        // decision, the count, and the AX tree all key off the VISIBLE set
        // (review finding 5), not the raw 1,501-node topology.
        let session = try makeSession()
        let base = try makeModel(session)
        let n = GraphDiagramModel.tierBThreshold + 1  // 1,501
        var ids: [UInt64] = []
        var byID: [UInt64: GraphNode] = [:]
        var positions: [UInt64: CGPoint] = [:]
        for i in 1...n {
            let id = UInt64(i)
            ids.append(id)
            let label = i <= 3 ? "keep\(i)" : "n\(i)"
            byID[id] = GraphNode(
                id: id, path: "n\(id).md", label: label, kind: .note, inLinks: 0, outLinks: 0,
                inEmbeds: 0, outEmbeds: 0, component: 0, isOrphan: true, pagerank: 0, modifiedMs: nil)
            positions[id] = CGPoint(x: Double(i), y: Double(i))
        }
        let big = GraphDiagramModel(
            session: base.session, filter: filter, nodeIDs: ids, nodesByID: byID, edges: [],
            generation: 1)
        let (state, view) = makeView(big)
        view.injectPositionsForTesting(positions)
        XCTAssertTrue(view.isTierBForTesting(), "1,501 unfiltered ⇒ Tier B")

        state.graphTableTextFilter = "keep"
        view.injectPositionsForTesting(positions)  // drive a deterministic rebuild
        XCTAssertFalse(view.isTierBForTesting(), "3 visible ⇒ Tier A")
        XCTAssertEqual(view.visibleNodeCountForTesting(), 3)
        XCTAssertEqual(
            view.axChildCountForTesting(), 3, "per-node AX for the 3 visible, not a summary element")
    }

    func testDiagramHonoursThePresetKindFilterLikeTheTable() throws {
        // The Unresolved preset sets `graphTableKindFilter = .ghost`; the
        // Diagram must apply it too, so both projections show one node set
        // (review finding 5). A note linking a missing target yields one
        // note + one ghost.
        let vault = tempDir.appendingPathComponent("kind-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        try "# A\n[[Missing Target]]\n".write(
            to: vault.appendingPathComponent("a.md"), atomically: true, encoding: .utf8)
        let session = try VaultSession.openFilesystem(rootPath: vault.path)
        try session.scanInitial(cancel: CancelToken())
        let model = try makeModel(session)
        let (state, view) = makeView(model)
        view.tickOnceForTesting()
        let ghosts = Set(model.nodesByID.values.filter { $0.kind == .ghost }.map(\.id))
        XCTAssertFalse(ghosts.isEmpty, "fixture has an unresolved target")
        XCTAssertGreaterThan(view.nodeFramesForTesting().count, ghosts.count, "notes shown too")

        state.graphTableKindFilter = .ghost
        view.tickOnceForTesting()  // rebuild
        XCTAssertEqual(
            Set(view.nodeFramesForTesting().keys), ghosts,
            "with the .ghost kind filter the Diagram shows exactly the ghosts, as the Table would")
    }

    func testNeighborContentExcludesFilteredOutNodes() throws {
        // a links b and c; a name filter hides b. Node a's AX "Connects to"
        // custom content must list only the VISIBLE neighbor (c), matching
        // the drawn edges (review finding 5 — the AX path was unfiltered).
        let session = try makeSession()
        let base = try makeModel(session)
        func node(_ id: UInt64, _ label: String) -> GraphNode {
            GraphNode(
                id: id, path: "\(label).md", label: label, kind: .note, inLinks: 0, outLinks: 0,
                inEmbeds: 0, outEmbeds: 0, component: 0, isOrphan: false, pagerank: 0,
                modifiedMs: nil)
        }
        let ids: [UInt64] = [1, 2, 3]
        let byID: [UInt64: GraphNode] = [
            1: node(1, "keep-a"), 2: node(2, "hide-b"), 3: node(3, "keep-c"),
        ]
        let edges = [
            GraphEdge(sourceId: 1, targetId: 2, kind: .link, count: 1),
            GraphEdge(sourceId: 1, targetId: 3, kind: .link, count: 1),
        ]
        let model = GraphDiagramModel(
            session: base.session, filter: filter, nodeIDs: ids, nodesByID: byID, edges: edges,
            generation: 1)
        let (state, view) = makeView(model)
        state.graphTableTextFilter = "keep"
        view.injectPositionsForTesting([
            1: CGPoint(x: 0, y: 0), 2: CGPoint(x: 10, y: 0), 3: CGPoint(x: 0, y: 10),
        ])
        // "keep-a" is the first visible element (id order).
        let content = try XCTUnwrap(view.axNeighborContentForTesting(nodeIndex: 0))
        XCTAssertTrue(content.contains("keep-c"), "visible neighbor is listed")
        XCTAssertFalse(content.contains("hide-b"), "a filtered-out neighbor is NOT listed")
    }

    func testSettleAnnouncementNotArmedWithoutADiagramAndClearedOnTeardown() throws {
        // The settled-state announcement must arm ONLY with a live diagram
        // and never survive teardown, or a later initial build would
        // spuriously say "settled" (review finding 8).
        let session = try makeSession()
        let model = try makeModel(session)
        let (state, _) = makeView(model)  // sets graphDiagramModel
        state.setGraphForces(
            GraphForcesConfig(center: 0.1, repel: 0.9, link: 0.2, linkDistance: 0.8))
        XCTAssertTrue(state.graphForcesSettlePending, "a live diagram arms the settled announcement")

        state.resetGraphDiagramState()
        XCTAssertFalse(state.graphForcesSettlePending, "teardown clears the pending flag")

        state.setGraphForces(
            GraphForcesConfig(center: 0.3, repel: 0.3, link: 0.3, linkDistance: 0.3))
        XCTAssertFalse(
            state.graphForcesSettlePending, "no live diagram ⇒ nothing to settle, nothing armed")
    }

    // MARK: Projection sync (P2-5 #561)

    func testDiagramNodeActionsMatchTheCanonicalSetPlusPin() throws {
        // The Diagram's per-node VoiceOver actions must be exactly the
        // shared canonical set (+ diagram-only Pin), so the three
        // projections don't drift (DoD §P-B parity). A note linking a
        // missing target gives one note + one ghost.
        let vault = tempDir.appendingPathComponent("actions-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        try "# A\n[[Missing Target]]\n".write(
            to: vault.appendingPathComponent("a.md"), atomically: true, encoding: .utf8)
        let session = try VaultSession.openFilesystem(rootPath: vault.path)
        try session.scanInitial(cancel: CancelToken())
        let model = try makeModel(session)
        let (_, view) = makeView(model)
        view.tickOnceForTesting()
        let note = try XCTUnwrap(model.nodesByID.values.first { $0.kind == .note })
        let ghost = try XCTUnwrap(model.nodesByID.values.first { $0.kind == .ghost })
        XCTAssertEqual(
            view.axCustomActionNamesForTesting(nodeId: note.id),
            GraphRowAction.actions(forGhost: false).map(\.title) + ["Pin"],
            "a real note exposes the four canonical nav actions + Pin")
        XCTAssertEqual(
            view.axCustomActionNamesForTesting(nodeId: ghost.id),
            GraphRowAction.actions(forGhost: true).map(\.title) + ["Pin"],
            "a ghost exposes Create note + Pin")
    }

    func testDiagramSelectionMirrorsToTheSharedKey() throws {
        let session = try makeSession()
        let model = try makeModel(session)
        let (state, _) = makeView(model)
        let snap = try session.graphSnapshot(filter: filter)
        let aNode = snap.nodes.first { $0.label == "a" }!
        state.graphDiagramSelect(aNode.id)
        XCTAssertEqual(model.selection, aNode.id)
        XCTAssertEqual(
            state.graphSelectedNodeKey, GraphNodeKey.make(for: aNode),
            "selecting in the Diagram writes the shared cross-projection key")
    }

    func testSharedKeySeedsTheDiagramSelectionAndMissesGracefully() throws {
        // The mapping used to seed the Diagram's selection on a Table→
        // Diagram switch: a shared key present in the diagram maps to its
        // id; an absent key maps to nil (no crash, no stale selection).
        let session = try makeSession()
        let model = try makeModel(session)
        let (state, _) = makeView(model)  // sets graphDiagramModel
        let snap = try session.graphSnapshot(filter: filter)
        let cNode = snap.nodes.first { $0.label == "c" }!
        state.graphSelectedNodeKey = GraphNodeKey.make(for: cNode)
        XCTAssertEqual(state.graphDiagramIDForSharedSelection(), cNode.id)
        state.graphSelectedNodeKey = "p:/does-not-exist.md"
        XCTAssertNil(state.graphDiagramIDForSharedSelection())
        state.graphSelectedNodeKey = nil
        XCTAssertNil(state.graphDiagramIDForSharedSelection())
    }

    func testGenerationRefreshRemapsSelectionByStableKey() throws {
        // A generation churn can reassign a node a NEW numeric id; the
        // selection must follow the node by its STABLE key, not stick to the
        // old id (which could now name a different node) — P2-5 review
        // finding 1. This exercises the remap `refreshGraphDiagramIfGraph-
        // Changed` applies after `adopt`.
        let session = try makeSession()
        let model = try makeModel(session)
        let snap = try session.graphSnapshot(filter: filter)
        let a = snap.nodes.first { $0.label == "a" }!
        let key = GraphNodeKey.make(for: a)
        let newID = a.id &+ 1000
        let aReassigned = GraphNode(
            id: newID, path: a.path, label: a.label, kind: a.kind, inLinks: a.inLinks,
            outLinks: a.outLinks, inEmbeds: a.inEmbeds, outEmbeds: a.outEmbeds,
            component: a.component, isOrphan: a.isOrphan, pagerank: a.pagerank,
            modifiedMs: a.modifiedMs)
        model.adopt(
            nodeIDs: [newID], nodesByID: [newID: aReassigned], edges: [],
            generation: model.generation + 1)
        XCTAssertEqual(
            AppState.graphDiagramNodeID(forKey: key, ids: model.nodeIDs, byID: model.nodesByID),
            newID, "the stable key follows the node to its reassigned id")
    }

    func testSetGraphForcesUpdatesConfigAndTheLiveLayoutStaysFinite() throws {
        let session = try makeSession()
        let model = try makeModel(session)
        let (state, _) = makeView(model)
        state.setGraphForces(
            GraphForcesConfig(center: 0.1, repel: 0.9, link: 0.2, linkDistance: 0.8))
        XCTAssertEqual(state.graphConfig.forces.repel, 0.9)
        // set_forces reached the session (re-heat); a subsequent tick still
        // produces a finite layout under the new forces.
        let frame = model.session.tick(iterations: 5)
        XCTAssertTrue(frame.positions.allSatisfy { $0.isFinite })
    }

    // MARK: APCA contrast (both appearances)

    func testDiagramColorsMeetAPCAInBothAppearances() {
        // node/edge/label pairs measured against the diagram background in
        // light AND dark (project standard |Lc| > 75 for the text label;
        // graphical marks assert a meaningful non-trivial contrast).
        for name in ["NSAppearanceNameAqua", "NSAppearanceNameDarkAqua"] {
            let appearance = NSAppearance(named: NSAppearance.Name(name))!
            let bg = NSColor.windowBackgroundColor
            let label = APCAContrast.lc(
                text: .labelColor, background: bg, for: appearance)
            XCTAssertGreaterThan(abs(label), 75, "\(name): label text meets APCA G-4g")
            let node = APCAContrast.lc(
                text: .controlAccentColor, background: bg, for: appearance)
            XCTAssertGreaterThan(abs(node), 15, "\(name): node fill is a visible graphical mark")
            let edge = APCAContrast.lc(
                text: .separatorColor, background: bg, for: appearance)
            XCTAssertGreaterThan(abs(edge), 3, "\(name): edges are visible against the background")
        }
    }
}
