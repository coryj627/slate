// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// #364 acceptance: navigator movements on fixture canvases
/// (dead-ends, group boundaries, multi-edge, cycles) and the
/// CanvasModeController's M1–M7 contract, driven against a test mode.
@MainActor
final class CanvasNavigatorTests: XCTestCase {
    private var tempDir: URL!
    private var posted: [String] = []

    override func setUpWithError() throws {
        try super.setUpWithError()
        tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-canvas-nav-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: tempDir, withIntermediateDirectories: true)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: tempDir)
        try super.tearDownWithError()
    }

    /// Group of two cards, a two-hop chain with a cycle back, a
    /// multi-edge pair, and a dead-end card.
    private static let fixture = """
        {"nodes":[
        {"id":"g1","type":"group","x":-20,"y":-20,"width":460,"height":300,"label":"Zone"},
        {"id":"a","type":"text","text":"Alpha","x":0,"y":0,"width":200,"height":100},
        {"id":"b","type":"text","text":"Beta","x":220,"y":0,"width":200,"height":100},
        {"id":"c","type":"text","text":"Gamma","x":600,"y":0,"width":200,"height":100},
        {"id":"d","type":"text","text":"Delta","x":600,"y":140,"width":200,"height":100}
        ],"edges":[
        {"id":"ab1","fromNode":"a","toNode":"b","label":"first"},
        {"id":"ab2","fromNode":"a","toNode":"b","label":"second"},
        {"id":"bc","fromNode":"b","toNode":"c"},
        {"id":"ca","fromNode":"c","toNode":"a"},
        {"id":"dc","fromNode":"d","toNode":"c"}
        ]}
        """

    private func makeState() async throws -> AppState {
        let vault = tempDir.appendingPathComponent("vault-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        try Data(Self.fixture.utf8).write(to: vault.appendingPathComponent("nav.canvas"))
        let store = RecentVaultsStore(
            fileURL: tempDir.appendingPathComponent("recents-\(UUID().uuidString).json"))
        let state = AppState(recentsStore: store, externalOpener: { _ in true })
        state.openVault(at: vault)
        await state.scanTask?.value
        state.openFile("nav.canvas", target: .currentTab)
        posted = []
        state.canvasAnnouncer = CanvasAnnouncer(verbosity: .standard, coalesceWindow: 60) {
            text, _ in self.posted.append(text)
        }
        return state
    }

    func testNextPreviousWithBoundaries() async throws {
        let state = try await makeState()
        let doc = try XCTUnwrap(state.activeCanvasDocument)

        // No selection: next selects the first row (the group).
        state.canvasSelectAdjacent(offset: 1)
        XCTAssertEqual(doc.selection.selected, "g1")
        state.canvasSelectAdjacent(offset: 1)
        XCTAssertEqual(doc.selection.selected, "a")

        // Walk to the end; the boundary announces, selection holds.
        for _ in 0..<10 { state.canvasSelectAdjacent(offset: 1) }
        XCTAssertEqual(doc.selection.selected, "d")
        state.canvasAnnouncer.flushForTests()
        XCTAssertTrue(posted.contains("End of canvas."), "\(posted)")

        state.canvasSelectAdjacent(offset: -1)
        XCTAssertEqual(doc.selection.selected, "c")
    }

    func testEnterExitGroupBoundaries() async throws {
        let state = try await makeState()
        let doc = try XCTUnwrap(state.activeCanvasDocument)

        doc.selection.selected = "g1"
        state.canvasEnterGroup()
        XCTAssertEqual(doc.selection.selected, "a", "enter selects the first child")

        state.canvasExitGroup()
        XCTAssertEqual(doc.selection.selected, "g1", "exit selects the containing group")

        state.canvasExitGroup()
        state.canvasAnnouncer.flushForTests()
        XCTAssertTrue(posted.contains("At canvas level."))

        doc.selection.selected = "c"
        state.canvasEnterGroup()
        state.canvasAnnouncer.flushForTests()
        XCTAssertTrue(posted.contains("Not a group."))
    }

    func testFollowConnectionMultiEdgeAndDirection() async throws {
        let state = try await makeState()
        let doc = try XCTUnwrap(state.activeCanvasDocument)

        // Multi-edge: ordinal picks among parallel connections.
        doc.selection.selected = "a"
        state.canvasFollowConnection(forward: true, ordinal: 2)
        XCTAssertEqual(doc.selection.selected, "b")
        state.canvasAnnouncer.flushForTests()
        XCTAssertTrue(
            posted.contains { $0.contains("labelled \"second\"") },
            "the second parallel edge is the ordinal-2 target: \(posted)")

        // Direction: back from 'a' follows the incoming c→a edge.
        doc.selection.selected = "a"
        posted = []
        state.canvasFollowConnection(forward: false)
        XCTAssertEqual(doc.selection.selected, "c")

        // Dead end: 'd' has no incoming connections.
        doc.selection.selected = "d"
        posted = []
        state.canvasFollowConnection(forward: false)
        state.canvasAnnouncer.flushForTests()
        XCTAssertEqual(doc.selection.selected, "d", "selection holds at a dead end")
        XCTAssertTrue(posted.contains { $0.contains("No incoming connection") })
    }

    func testTracePathIsCycleSafe() async throws {
        let state = try await makeState()
        let doc = try XCTUnwrap(state.activeCanvasDocument)

        // a → b → c → (a, cycle): visits each once, ends at c.
        doc.selection.selected = "a"
        state.canvasTracePath()
        state.canvasAnnouncer.flushForTests()
        XCTAssertEqual(doc.selection.selected, "c")
        let path = try XCTUnwrap(posted.first { $0.contains("End of path") })
        XCTAssertTrue(path.contains("Alpha, then Beta, then Gamma"), path)
        XCTAssertTrue(path.contains("3 cards visited"), path)

        // From 'd' the chain is d → c → a → b (bc closes the cycle at
        // already-visited c, ending the walk at b).
        doc.selection.selected = "d"
        posted = []
        state.canvasTracePath()
        state.canvasAnnouncer.flushForTests()
        XCTAssertEqual(doc.selection.selected, "b")
        XCTAssertTrue(posted.contains { $0.contains("4 cards visited") }, "\(posted)")
    }

    // MARK: Mode controller (t0 §2 M1–M7, against a test mode)

    private func makeMode(
        name: String = "Move mode", object: String = "'Research'",
        onCommit: @escaping () -> String? = { "Moved 'Research'" },
        onCancel: @escaping () -> String = { "Move cancelled — card returned." }
    ) -> CanvasModeController.ModeSpec {
        .init(
            name: name, object: object,
            exits: "Arrows to move, Return to place, Escape to cancel.",
            onCommit: onCommit, onCancel: onCancel)
    }

    private func makeController() -> (CanvasModeController, () -> [String]) {
        var events: [String] = []
        let announcer = CanvasAnnouncer(verbosity: .standard, coalesceWindow: 60) { text, _ in
            events.append(text)
        }
        let controller = CanvasModeController { announcer.announce($0) }
        return (controller, {
            announcer.flushForTests()
            return events
        })
    }

    func testM1EntryAnnouncesModeObjectAndExits() {
        let (controller, events) = makeController()
        XCTAssertTrue(controller.enter(makeMode()))
        XCTAssertEqual(
            events(),
            ["Move mode — 'Research'. Arrows to move, Return to place, Escape to cancel."])
    }

    func testM2CommitAndCancelAnnouncements() {
        let (controller, events) = makeController()
        controller.enter(makeMode())
        XCTAssertTrue(controller.commit())
        XCTAssertNil(controller.active)
        XCTAssertTrue(events().contains("Moved 'Research'"))

        controller.enter(makeMode())
        XCTAssertTrue(controller.cancel())
        XCTAssertNil(controller.active)
        XCTAssertTrue(events().contains("Move cancelled — card returned."))
    }

    func testM3QueryableContainerValue() {
        let (controller, _) = makeController()
        XCTAssertNil(controller.containerAXValue)
        controller.enter(makeMode())
        XCTAssertEqual(controller.containerAXValue, "Move mode: 'Research'")
        controller.commit()
        XCTAssertNil(controller.containerAXValue)
    }

    func testM4FocusDepartureAutoCancels() {
        let (controller, events) = makeController()
        var cancelled = false
        controller.enter(makeMode(onCancel: { cancelled = true; return "Move cancelled." }))
        controller.handleFocusDeparture()
        XCTAssertTrue(cancelled, "prior state restored on focus departure")
        XCTAssertNil(controller.active)
        XCTAssertTrue(events().contains("Move cancelled."))
    }

    func testM5EscLadderConsumesOneRungPerPress() {
        let (controller, _) = makeController()
        var filterCleared = false
        controller.escapeRungs = [{
            guard !filterCleared else { return false }
            filterCleared = true
            return true
        }]
        controller.enter(makeMode())
        XCTAssertTrue(controller.handleEscape(), "rung 1: the mode")
        XCTAssertFalse(filterCleared, "one press, one rung")
        XCTAssertTrue(controller.handleEscape(), "rung 2: the filter")
        XCTAssertTrue(filterCleared)
        XCTAssertFalse(controller.handleEscape(), "no rung left — bubbles to the surface")
    }

    func testM7SecondModeRejectedNamingActive() {
        let (controller, events) = makeController()
        controller.enter(makeMode())
        var committed = false
        let second = makeMode(
            name: "Resize mode", object: "'Ideas'",
            onCommit: { committed = true; return nil })
        XCTAssertFalse(controller.enter(second))
        XCTAssertFalse(committed, "rejection commits nothing")
        XCTAssertEqual(controller.active?.name, "Move mode")
        XCTAssertTrue(
            events().contains {
                $0.contains("Move mode is active")
            }, "rejection names the active mode")
    }
}

/// #372: session-scoped undo/redo stacks over canvas_apply inverses,
/// responder routing, and conflict-safe stale undo.
@MainActor
extension CanvasNavigatorTests {
    func testUndoRedoRoundTripsDiskBytes() async throws {
        let state = try await makeState()
        let doc = try XCTUnwrap(state.activeCanvasDocument)

        // Normalize once: the first save canonicalizes the fixture's
        // foreign formatting (documented #366 behavior). One apply+undo
        // cycle leaves the canonical baseline on disk; the assertions
        // below are then byte-exact.
        _ = state.canvasApply(
            CanvasAction(
                name: "normalize",
                ops: [.setNodeColor(id: "a", color: "1")]),
            to: doc)
        state.canvasUndo()
        doc.undoStack = []
        doc.redoStack = []
        let before = try XCTUnwrap(try? state.currentSession?.readText(path: "nav.canvas"))

        let applied = state.canvasApply(
            CanvasAction(
                name: "create card",
                ops: [
                    .createNode(
                        id: "u1",
                        content: .text(text: "Undo me"),
                        x: 0, y: 700, width: 200, height: 100, color: nil)
                ]),
            to: doc)
        XCTAssertTrue(applied)
        let mutated = try XCTUnwrap(try? state.currentSession?.readText(path: "nav.canvas"))
        XCTAssertNotEqual(before, mutated)
        XCTAssertEqual(doc.undoStack.map(\.name), ["create card"])
        XCTAssertTrue(doc.outline.contains { $0.nodeId == "u1" }, "document refreshed")

        // Undo: disk returns to the exact prior bytes; redo stack fills.
        posted = []
        state.canvasUndo()
        XCTAssertEqual(try? state.currentSession?.readText(path: "nav.canvas"), before)
        XCTAssertTrue(doc.undoStack.isEmpty)
        XCTAssertEqual(doc.redoStack.map(\.name), ["create card"])
        XCTAssertFalse(doc.outline.contains { $0.nodeId == "u1" })
        state.canvasAnnouncer.flushForTests()
        XCTAssertTrue(posted.contains("Undid: create card"), "\(posted)")

        // Redo restores the mutation byte-for-byte.
        posted = []
        state.canvasRedo()
        XCTAssertEqual(try? state.currentSession?.readText(path: "nav.canvas"), mutated)
        XCTAssertEqual(doc.undoStack.map(\.name), ["create card"])
        state.canvasAnnouncer.flushForTests()
        XCTAssertTrue(posted.contains("Redid: create card"))

        // A fresh action clears redo.
        _ = state.canvasApply(
            CanvasAction(
                name: "recolor",
                ops: [.setNodeColor(id: "a", color: "3")]),
            to: doc)
        XCTAssertTrue(doc.redoStack.isEmpty, "new action clears redo")
        XCTAssertEqual(doc.undoStack.map(\.name), ["create card", "recolor"])
    }

    func testUndoRoutingTargetsCanvasOnlyOnCanvasTabs() async throws {
        let state = try await makeState()
        XCTAssertTrue(state.undoTargetsCanvas, "canvas tab active → canvas stack")
        // A note tab takes the responder chain.
        let vault = try XCTUnwrap(state.currentVaultURL)
        try Data("# n".utf8).write(to: vault.appendingPathComponent("n.md"))
        _ = try state.currentSession?.scanInitial(cancel: CancelToken())
        state.openFile("n.md", target: .newTab)
        await state.noteLoadTask?.value
        XCTAssertFalse(state.undoTargetsCanvas, "note tab active → responder chain")
    }

    func testStaleUndoAfterExternalChangeIsBlockedNotBlindApplied() async throws {
        let state = try await makeState()
        let doc = try XCTUnwrap(state.activeCanvasDocument)
        _ = state.canvasApply(
            CanvasAction(
                name: "recolor", ops: [.setNodeColor(id: "a", color: "5")]),
            to: doc)
        XCTAssertEqual(doc.undoStack.count, 1)

        // External writer changes the file behind our back.
        let vault = try XCTUnwrap(state.currentVaultURL)
        try Data("{\"nodes\":[],\"edges\":[]}".utf8)
            .write(to: vault.appendingPathComponent("nav.canvas"))

        posted = []
        state.canvasUndo()
        state.canvasAnnouncer.flushForTests()
        XCTAssertTrue(
            posted.contains { $0.contains("Undo blocked") },
            "conflict surfaces, never a blind overwrite: \(posted)")
        XCTAssertEqual(doc.undoStack.count, 1, "the entry is retained for retry")
        // The external content is untouched.
        XCTAssertEqual(
            try? state.currentSession?.readText(path: "nav.canvas"),
            "{\"nodes\":[],\"edges\":[]}")
    }

    func testUndoStacksAreSessionScopedNotPersisted() async throws {
        let state = try await makeState()
        let doc = try XCTUnwrap(state.activeCanvasDocument)
        _ = state.canvasApply(
            CanvasAction(name: "recolor", ops: [.setNodeColor(id: "a", color: "2")]),
            to: doc)
        XCTAssertEqual(doc.undoStack.count, 1)

        // Simulate reopen: releasing and recreating the document (what
        // a restart does) starts with empty stacks; the journal keeps
        // the durable record (backend test pins that half).
        state.invalidateCanvasDocument(path: "nav.canvas")
        state.openFile("nav.canvas", target: .currentTab)
        let fresh = try XCTUnwrap(state.activeCanvasDocument)
        XCTAssertTrue(fresh.undoStack.isEmpty)
        XCTAssertTrue(fresh.redoStack.isEmpty)
    }
}

/// #368 core verbs: engine placement, one-write-one-undo mutations,
/// t0 §1.3 confirmations, and the new-canvas file path.
@MainActor
extension CanvasNavigatorTests {
    func testNewCardPlacesViaEngineAndAnnouncesRelatively() async throws {
        let state = try await makeState()
        let doc = try XCTUnwrap(state.activeCanvasDocument)
        doc.selection.selected = "d"  // (600,140) — free space below

        let countBefore = doc.outline.count
        state.canvasNewCard()
        state.canvasAnnouncer.flushForTests()

        XCTAssertEqual(doc.outline.count, countBefore + 1)
        let created = try XCTUnwrap(doc.selection.selected)
        XCTAssertNotEqual(created, "d", "selection lands on the new card")
        let node = try XCTUnwrap(doc.scene.nodes.first { $0.nodeId == created })
        // Engine placement: grid-aligned, non-overlapping, below anchor.
        XCTAssertEqual(node.x.truncatingRemainder(dividingBy: 20), 0)
        XCTAssertEqual(node.y.truncatingRemainder(dividingBy: 20), 0)
        XCTAssertGreaterThanOrEqual(node.y, 240 + 40, "below 'Delta' with the gap")
        XCTAssertTrue(
            posted.contains { $0.hasPrefix("Created text card") && $0.contains("below \"Delta\"") },
            "\(posted)")
        // One undo step reverts the creation.
        XCTAssertEqual(doc.undoStack.map(\.name).last, "create card")
        state.canvasUndo()
        XCTAssertEqual(doc.outline.count, countBefore)
    }

    func testDeleteAndColorVerbsAnnouncePerGrammar() async throws {
        let state = try await makeState()
        let doc = try XCTUnwrap(state.activeCanvasDocument)

        doc.selection.selected = "a"
        state.canvasSetColor(preset: 5)
        state.canvasAnnouncer.flushForTests()
        XCTAssertTrue(posted.contains("Set \"Alpha\" to cyan."), "\(posted)")
        let alpha = doc.tableRows.first { $0.nodeId == "a" }
        XCTAssertEqual(alpha?.colorName, "cyan")

        posted = []
        state.canvasDeleteSelection()
        state.canvasAnnouncer.flushForTests()
        // Destructive confirmation carries the undo hint (standard).
        XCTAssertTrue(
            posted.contains("Deleted Text card \"Alpha\" — ⌘Z to undo"), "\(posted)")
        XCTAssertFalse(doc.outline.contains { $0.nodeId == "a" })
        XCTAssertNil(doc.selection.selected)
        // Incident connections went with it; a single undo restores all.
        state.canvasUndo()
        XCTAssertTrue(doc.outline.contains { $0.nodeId == "a" })
        XCTAssertEqual(
            doc.neighbors(of: "a", session: state.currentSession).count, 3)
    }

    func testGroupVerbsRenameUngroupAndMoveInto() async throws {
        let state = try await makeState()
        let doc = try XCTUnwrap(state.activeCanvasDocument)

        doc.selection.selected = "g1"
        state.canvasRenameGroup(to: "Renamed Zone")
        XCTAssertEqual(
            doc.outline.first { $0.nodeId == "g1" }?.title, "Renamed Zone")

        // Move a root card into the group by name (no coordinates).
        doc.selection.selected = "c"
        state.canvasMoveIntoGroup(groupId: "g1")
        state.canvasAnnouncer.flushForTests()
        XCTAssertTrue(posted.contains("Moved into group \"Renamed Zone\"."), "\(posted)")
        XCTAssertEqual(
            doc.outline.first { $0.nodeId == "c" }?.groupPath, ["Renamed Zone"])

        // Ungroup keeps children.
        doc.selection.selected = "g1"
        let cardCount = doc.outline.count
        state.canvasDeleteSelection()
        XCTAssertEqual(doc.outline.count, cardCount - 1, "only the frame went away")
        XCTAssertTrue(doc.outline.contains { $0.nodeId == "a" })
    }

    func testNewCanvasFileCreatesOpensAndAnnounces() async throws {
        let state = try await makeState()
        state.canvasNewCanvasFile()
        state.canvasAnnouncer.flushForTests()
        guard case .canvas(let path) = state.workspace.activeTab?.item else {
            return XCTFail("new canvas should open as the active tab")
        }
        XCTAssertEqual(path, "Untitled Canvas.canvas")
        let doc = state.canvasDocument(for: path)
        XCTAssertEqual(doc.state, .ready)
        XCTAssertTrue(doc.outline.isEmpty, "empty canvas — onboarding state")
        XCTAssertTrue(posted.contains("Created canvas \"Untitled Canvas\"."))
        // Collision-avoidance on the second create.
        state.canvasNewCanvasFile()
        guard case .canvas(let second) = state.workspace.activeTab?.item else {
            return XCTFail("second canvas opens")
        }
        XCTAssertEqual(second, "Untitled Canvas 2.canvas")
    }
}

/// Codoki #617: Set Color on a GROUP is a real, announced mutation —
/// JSON Canvas groups carry `color` like any node (the spec's group
/// node inherits the generic node fields), the backend applies it
/// with a snapshot inverse, and the outline value phrases the name.
/// Pinned here so the "silent no-op on groups" concern stays refuted.
@MainActor
extension CanvasNavigatorTests {
    func testSetColorOnGroupMutatesAnnouncesAndUndoes() async throws {
        let state = try await makeState()
        let doc = try XCTUnwrap(state.activeCanvasDocument)
        state.canvasSelect(nodeId: "g1", in: doc, announce: false)
        posted = []
        state.canvasSetColor(preset: 4)
        state.canvasAnnouncer.flushForTests()
        XCTAssertTrue(posted.contains("Set \"Zone\" to green."), "\(posted)")
        XCTAssertEqual(
            doc.outline.first { $0.nodeId == "g1" }?.colorName, "green",
            "the group's color is a modeled, spoken attribute")
        state.canvasUndo()
        XCTAssertNil(doc.outline.first { $0.nodeId == "g1" }?.colorName)
    }
}

/// #522: structural placement — proximity picker ordering, engine
/// geometry, rigid-unit marked-set moves, align overlap refusal.
@MainActor
extension CanvasNavigatorTests {
    func testPickerCandidatesAreProximitySortedWithGrammarLabels() async throws {
        let state = try await makeState()
        let doc = try XCTUnwrap(state.activeCanvasDocument)
        // Anchor on 'a' (center 100,50): the Zone frame's center
        // (210,130) is nearest, then Beta (320,50), then Gamma, Delta.
        doc.selection.selected = "a"
        let picker = CanvasCardPicker(
            document: doc, purpose: .placeBelow, excluded: ["a"]
        ) { _ in }
        let ids = picker.candidates.map(\.id)
        XCTAssertEqual(ids, ["g1", "b", "c", "d"], "nearest first: \(ids)")
        XCTAssertTrue(
            picker.candidates.allSatisfy { !$0.label.isEmpty })
        let bLabel = picker.candidates.first { $0.id == "b" }?.label
        XCTAssertEqual(bLabel, "Text card \"Beta\", in Zone")
    }

    func testPlaceBelowMovesViaEngineAndAnnounces() async throws {
        let state = try await makeState()
        let doc = try XCTUnwrap(state.activeCanvasDocument)
        doc.selection.selected = "c"  // move Gamma below Delta
        posted = []
        state.canvasPlaceRelative(target: "d", direction: .below)
        state.canvasAnnouncer.flushForTests()

        let moved = try XCTUnwrap(doc.scene.nodes.first { $0.nodeId == "c" })
        XCTAssertGreaterThanOrEqual(moved.y, 140 + 100 + 40, "below Delta with the gap")
        XCTAssertEqual(moved.x.truncatingRemainder(dividingBy: 20), 0, "grid-aligned")
        XCTAssertTrue(
            posted.contains("Moved \"Gamma\" below \"Delta\"."), "\(posted)")
        // One undo restores the old spot.
        XCTAssertEqual(doc.undoStack.last?.name, "move \"Gamma\"")
    }

    func testMarkedSetPlacesAsRigidUnitWithOneUndoAndOneSummary() async throws {
        let state = try await makeState()
        let doc = try XCTUnwrap(state.activeCanvasDocument)
        // Mark a (0,0) and b (220,0): offsets must survive the move.
        doc.selection.marked = ["a", "b"]
        doc.selection.selected = "a"
        posted = []
        state.canvasPlaceRelative(target: "d", direction: .below)
        state.canvasAnnouncer.flushForTests()

        let a = try XCTUnwrap(doc.scene.nodes.first { $0.nodeId == "a" })
        let b = try XCTUnwrap(doc.scene.nodes.first { $0.nodeId == "b" })
        XCTAssertEqual(b.x - a.x, 220, "pairwise offsets preserved exactly")
        XCTAssertEqual(b.y - a.y, 0)
        XCTAssertGreaterThanOrEqual(a.y, 240, "the unit sits below Delta")
        // One summary, one undo entry for the whole unit.
        XCTAssertEqual(posted.filter { $0.hasPrefix("Moved 2 cards") }.count, 1, "\(posted)")
        XCTAssertEqual(doc.undoStack.last?.name, "move 2 cards")
        let undoCount = doc.undoStack.count
        state.canvasUndo()
        XCTAssertEqual(doc.undoStack.count, undoCount - 1)
        let aBack = try XCTUnwrap(doc.scene.nodes.first { $0.nodeId == "a" })
        XCTAssertEqual(aBack.x, 0)
        XCTAssertEqual(aBack.y, 0)
    }

    func testAlignRefusesOverlapAndAlignsWhenClear() async throws {
        let state = try await makeState()
        let doc = try XCTUnwrap(state.activeCanvasDocument)
        // Aligning Beta (220,0) with Alpha (0,0) keeps y=0 — no move
        // needed but legal (no overlap at its own x).
        doc.selection.selected = "d"  // Delta (600,140) → align with c (600,0)
        posted = []
        state.canvasAlignWith(target: "c")
        state.canvasAnnouncer.flushForTests()
        // d at (600, 0) would overlap c exactly → refused.
        XCTAssertTrue(
            posted.contains { $0.contains("would overlap") }, "\(posted)")
        let d = try XCTUnwrap(doc.scene.nodes.first { $0.nodeId == "d" })
        XCTAssertEqual(d.y, 140, "refused align leaves geometry untouched")

        // A clear align: Beta (220,0) aligns with Delta's row (y=140)
        // — (220,140) collides with nothing.
        doc.selection.selected = "b"
        posted = []
        state.canvasAlignWith(target: "d")
        state.canvasAnnouncer.flushForTests()
        let aligned = try XCTUnwrap(doc.scene.nodes.first { $0.nodeId == "b" })
        XCTAssertEqual(aligned.y, 140, "aligned tops with Delta")
        XCTAssertEqual(aligned.x, 220, "x untouched by align")
        XCTAssertTrue(posted.contains("Aligned \"Beta\" with \"Delta\"."), "\(posted)")
    }
}

/// #521: move & resize modes — grid nudges, rigid sets, overlap
/// onset/offset, single-entry commits, exact-restore cancels.
@MainActor
extension CanvasNavigatorTests {
    private func normalizedState() async throws -> (AppState, CanvasDocument) {
        let state = try await makeState()
        let doc = try XCTUnwrap(state.activeCanvasDocument)
        // Canonicalize once so disk comparisons are byte-exact.
        _ = state.canvasApply(
            CanvasAction(name: "normalize", ops: [.setNodeColor(id: "a", color: "1")]),
            to: doc)
        state.canvasUndo()
        doc.undoStack = []
        doc.redoStack = []
        return (state, doc)
    }

    func testMoveModeNudgesCommitOnceAndAnnounceRelatively() async throws {
        let (state, doc) = try await normalizedState()
        doc.selection.selected = "d"  // Delta (600,140)
        state.canvasEnterMoveMode()
        state.canvasAnnouncer.flushForTests()
        XCTAssertTrue(
            posted.contains {
                $0.hasPrefix("Move mode — \"Delta\".") && $0.contains("Escape to cancel")
            }, "M1 entry names mode, object, exits: \(posted)")
        // M3: inspectable while active.
        XCTAssertEqual(
            state.canvasModeController(for: doc).containerAXValue, "Move mode: \"Delta\"")

        let undoCountBefore = doc.undoStack.count
        let diskBefore = try XCTUnwrap(try? state.currentSession?.readText(path: "nav.canvas"))

        // Nudge: 2 small + 1 large step right, 1 small down.
        posted = []
        state.canvasModeStep(dx: 1, dy: 0, large: false)
        state.canvasModeStep(dx: 1, dy: 0, large: false)
        state.canvasModeStep(dx: 1, dy: 0, large: true)
        state.canvasModeStep(dx: 0, dy: 1, large: false)
        // Transient only: nothing on disk yet.
        XCTAssertEqual(
            try? state.currentSession?.readText(path: "nav.canvas"), diskBefore,
            "nudges never touch disk before commit")
        // Coalesced: the resting description is one debounced post.
        state.canvasAnnouncer.flushForTests()
        XCTAssertEqual(
            posted.count, 1, "held-arrow narration coalesces to the resting position: \(posted)")

        // Commit: ONE canvas_apply capturing start→end.
        _ = state.canvasModeController(for: doc).commit()
        let moved = try XCTUnwrap(doc.scene.nodes.first { $0.nodeId == "d" })
        XCTAssertEqual(moved.x, 600 + 20 + 20 + 100)
        XCTAssertEqual(moved.y, 140 + 20)
        XCTAssertEqual(doc.undoStack.count, undoCountBefore + 1, "one undo step for the mode")
        XCTAssertNil(doc.transientRects)
        // Undo restores the exact pre-mode geometry.
        state.canvasUndo()
        let restored = try XCTUnwrap(doc.scene.nodes.first { $0.nodeId == "d" })
        XCTAssertEqual(restored.x, 600)
        XCTAssertEqual(restored.y, 140)
    }

    func testMoveModeCancelRestoresExactGeometryWithNoWrite() async throws {
        let (state, doc) = try await normalizedState()
        let diskBefore = try XCTUnwrap(try? state.currentSession?.readText(path: "nav.canvas"))
        doc.selection.selected = "d"
        state.canvasEnterMoveMode()
        state.canvasModeStep(dx: 1, dy: 1, large: true)
        posted = []
        _ = state.canvasModeController(for: doc).cancel()
        state.canvasAnnouncer.flushForTests()
        XCTAssertTrue(posted.contains("Move cancelled — card returned."), "\(posted)")
        XCTAssertNil(doc.transientRects)
        XCTAssertEqual(
            try? state.currentSession?.readText(path: "nav.canvas"), diskBefore,
            "cancel = zero backend calls, byte-identical disk")
        let node = try XCTUnwrap(doc.scene.nodes.first { $0.nodeId == "d" })
        XCTAssertEqual(node.x, 600)
        XCTAssertEqual(node.y, 140)
    }

    func testMoveModeOverlapOnsetAndOffsetAreFlagged() async throws {
        let (state, doc) = try await normalizedState()
        doc.selection.selected = "d"  // (600,140); c sits at (600,0)
        state.canvasEnterMoveMode()
        state.canvasAnnouncer.flushForTests()
        posted = []
        // One large step up: d → (600,40) overlaps c (0..100 rows).
        state.canvasModeStep(dx: 0, dy: -1, large: true)
        state.canvasAnnouncer.flushForTests()
        XCTAssertTrue(
            posted.contains { $0.contains("Overlapping another card") },
            "onset flagged (G20): \(posted)")
        posted = []
        state.canvasModeStep(dx: 0, dy: 1, large: true)  // back down, clear
        state.canvasAnnouncer.flushForTests()
        XCTAssertTrue(
            posted.contains { $0.contains("Clear of overlaps") },
            "offset flagged: \(posted)")
        _ = state.canvasModeController(for: doc).cancel()
    }

    func testMarkedSetMovesRigidlyInMoveMode() async throws {
        let (state, doc) = try await normalizedState()
        doc.selection.marked = ["a", "b"]
        doc.selection.selected = "a"
        state.canvasEnterMoveMode()
        state.canvasAnnouncer.flushForTests()
        XCTAssertTrue(
            posted.contains { $0.hasPrefix("Move mode — 2 cards.") }, "\(posted)")
        state.canvasModeStep(dx: 0, dy: 1, large: true)
        _ = state.canvasModeController(for: doc).commit()
        let a = try XCTUnwrap(doc.scene.nodes.first { $0.nodeId == "a" })
        let b = try XCTUnwrap(doc.scene.nodes.first { $0.nodeId == "b" })
        XCTAssertEqual(a.x, 0)
        XCTAssertEqual(a.y, 100)
        XCTAssertEqual(b.x, 220, "offsets preserved through the nudge")
        XCTAssertEqual(b.y, 100)
        XCTAssertEqual(doc.undoStack.last?.name, "move 2 cards")
    }

    func testResizeModeArrowsMinimumAndPresets() async throws {
        let (state, doc) = try await normalizedState()
        doc.selection.selected = "d"
        state.canvasCommitOrEnterResize()
        state.canvasAnnouncer.flushForTests()
        XCTAssertTrue(
            posted.contains { $0.hasPrefix("Resize mode — \"Delta\".") }, "\(posted)")

        // Width +20 (→), height +100 (⇧↓).
        state.canvasModeStep(dx: 1, dy: 0, large: false)
        state.canvasModeStep(dx: 0, dy: 1, large: true)
        // Minimum size: shrinking width below 40 is refused.
        posted = []
        for _ in 0..<20 { state.canvasModeStep(dx: -1, dy: 0, large: true) }
        state.canvasAnnouncer.flushForTests()
        XCTAssertTrue(posted.contains("Minimum size."), "\(posted)")

        // Preset then commit (⌃⌘R toggles commit while active).
        state.canvasResizeDefaultSize()
        state.canvasCommitOrEnterResize()
        let node = try XCTUnwrap(doc.scene.nodes.first { $0.nodeId == "d" })
        XCTAssertEqual(node.width, 260)
        XCTAssertEqual(node.height, 140)
        XCTAssertEqual(doc.undoStack.last?.name, "resize \"Delta\"")
    }

    func testModeEntryWithoutSelectionAnnounces() async throws {
        let (state, doc) = try await normalizedState()
        doc.selection.selected = nil
        posted = []
        state.canvasEnterMoveMode()
        state.canvasAnnouncer.flushForTests()
        XCTAssertTrue(posted.contains("Nothing selected."))
        XCTAssertNil(state.canvasModeController(for: doc).active)
    }
}

/// Red-team #521 regressions: mutation guards while a mode is active,
/// mode-state teardown on release, entry-time overlap seeding.
@MainActor
extension CanvasNavigatorTests {
    func testUndoRefusedWhileModeActiveAndCommitStaysExact() async throws {
        let (state, doc) = try await normalizedState()
        state.canvasSelect(nodeId: "d", in: doc, announce: false)
        state.canvasEnterMoveMode()
        state.canvasModeStep(dx: 1, dy: 0, large: true)
        let undoBefore = doc.undoStack.count
        posted = []
        state.canvasUndo()
        state.canvasAnnouncer.flushForTests()
        XCTAssertEqual(doc.undoStack.count, undoBefore, "undo refused mid-mode")
        XCTAssertTrue(
            posted.contains { $0.contains("move or resize is in progress") }, "\(posted)")
        XCTAssertNotNil(state.canvasTransient, "mode survives the refused undo")

        // Out-of-band verbs are refused too (the clobber path).
        let outlineBefore = doc.outline.count
        state.canvasDeleteSelection()
        XCTAssertEqual(doc.outline.count, outlineBefore, "delete refused mid-mode")

        // The mode's own commit still works and writes the stepped rect.
        _ = state.canvasModeController(for: doc).commit()
        XCTAssertNil(state.canvasTransient)
        let node = try XCTUnwrap(doc.scene.nodes.first { $0.nodeId == "d" })
        XCTAssertEqual(node.x, 700, "commit wrote the transient, exactly")
        state.canvasUndo()
        XCTAssertEqual(doc.scene.nodes.first { $0.nodeId == "d" }?.x, 600)
    }

    func testInvalidateDropsModeControllerAndTransient() async throws {
        let (state, doc) = try await normalizedState()
        state.canvasSelect(nodeId: "d", in: doc, announce: false)
        state.canvasEnterMoveMode()
        state.canvasModeStep(dx: 1, dy: 0, large: false)
        XCTAssertNotNil(state.canvasTransient)

        state.invalidateCanvasDocument(path: "nav.canvas")
        XCTAssertNil(state.canvasTransient, "transient dies with the document")
        XCTAssertNil(state.canvasModeControllers["nav.canvas"], "controller dies too")

        // Reopen: a fresh mode enters cleanly (no phantom M7 block).
        state.openFile("nav.canvas", target: .currentTab)
        let doc2 = state.canvasDocument(for: "nav.canvas")
        state.canvasSelect(nodeId: "d", in: doc2, announce: false)
        state.canvasEnterMoveMode()
        XCTAssertNotNil(state.canvasTransient, "fresh mode enters after reopen")
        _ = state.canvasModeController(for: doc2).cancel()
    }

    func testEntryOverlapSeedsSoOnsetIsATransition() async throws {
        let (state, doc) = try await normalizedState()
        // Move Beta onto Alpha first so move mode STARTS overlapped.
        state.canvasSelect(nodeId: "b", in: doc, announce: false)
        let alpha = try XCTUnwrap(doc.scene.nodes.first { $0.nodeId == "a" })
        _ = state.canvasApply(
            CanvasAction(
                name: "setup",
                ops: [
                    .updateNodeGeometry(
                        id: "b", x: alpha.x + 20, y: alpha.y + 20, width: 200, height: 100)
                ]),
            to: doc)
        state.canvasEnterMoveMode()
        posted = []
        // One tiny step that stays overlapping: NO onset announcement.
        state.canvasModeStep(dx: 1, dy: 0, large: false)
        state.canvasAnnouncer.flushForTests()
        XCTAssertFalse(
            posted.contains { $0.contains("Overlapping another card") },
            "already-overlapped entry must not fake an onset: \(posted)")
        // Step clear: the OFFSET announces (flush per step — the
        // coalescer keeps only the latest text within its window).
        for _ in 0..<6 {
            state.canvasModeStep(dx: 1, dy: 0, large: true)
            state.canvasAnnouncer.flushForTests()
        }
        XCTAssertTrue(
            posted.contains { $0.contains("Clear of overlaps") }, "\(posted)")
        _ = state.canvasModeController(for: doc).cancel()
    }
}

/// #523: connect flow — auto sides, labels, direction round-trips,
/// connect mode reusing navigator movements, edit/delete.
@MainActor
extension CanvasNavigatorTests {
    func testConnectPickerFlowAutoSidesAndLabel() async throws {
        let (state, doc) = try await normalizedState()
        doc.selection.selected = "c"  // Gamma (600,0)
        posted = []
        state.canvasConnect(from: "c", to: "d", label: "feeds")  // d below c
        state.canvasAnnouncer.flushForTests()
        XCTAssertTrue(
            posted.contains("Connected \"Gamma\" to \"Delta\", labelled \"feeds\"."),
            "\(posted)")
        let edge = try XCTUnwrap(
            doc.scene.edges.first { $0.fromNode == "c" && $0.toNode == "d" })
        // Auto sides: d is directly below c → bottom→top.
        XCTAssertEqual(edge.fromSide, .bottom)
        XCTAssertEqual(edge.toSide, .top)
        XCTAssertFalse(edge.fromArrow)
        XCTAssertTrue(edge.toArrow, "default: one-way arrow at the target")
        XCTAssertEqual(edge.label, "feeds")
        // One undo removes it.
        state.canvasUndo()
        XCTAssertNil(doc.scene.edges.first { $0.fromNode == "c" && $0.toNode == "d" })
    }

    func testConnectModeReusesNavigatorAndConfirmsWithReturn() async throws {
        let (state, doc) = try await normalizedState()
        doc.selection.selected = "a"
        state.canvasEnterConnectMode()
        state.canvasAnnouncer.flushForTests()
        XCTAssertTrue(
            posted.contains { $0.hasPrefix("Connect mode — \"Alpha\".") }, "\(posted)")
        XCTAssertEqual(
            state.canvasModeController(for: doc).containerAXValue,
            "Connect mode: \"Alpha\"")

        // Navigator movements work verbatim while the mode is armed
        // (no transient → arrows stay navigation).
        XCTAssertFalse(state.canvasModeConsumesArrows)
        state.canvasSelectAdjacent(offset: 1)  // → b… walk to d
        state.canvasSelectAdjacent(offset: 1)
        state.canvasSelectAdjacent(offset: 1)
        XCTAssertEqual(doc.selection.selected, "d")

        posted = []
        _ = state.canvasModeController(for: doc).commit()
        state.canvasAnnouncer.flushForTests()
        XCTAssertTrue(
            posted.contains("Connected \"Alpha\" to \"Delta\"."), "\(posted)")
        XCTAssertNotNil(doc.scene.edges.first { $0.fromNode == "a" && $0.toNode == "d" })

        // Esc path: origin restored.
        doc.selection.selected = "a"
        state.canvasEnterConnectMode()
        state.canvasSelectAdjacent(offset: 1)
        posted = []
        _ = state.canvasModeController(for: doc).cancel()
        state.canvasAnnouncer.flushForTests()
        XCTAssertEqual(doc.selection.selected, "a", "Esc returns to the origin")
        XCTAssertTrue(posted.contains { $0.contains("back at \"Alpha\"") })
    }

    func testEditConnectionDirectionAndLabelRoundTrips() async throws {
        let (state, doc) = try await normalizedState()
        doc.selection.selected = "a"
        // ab1 exists a→b labelled "first".
        state.canvasEditConnection(
            edgeId: "ab1", label: "rewired", direction: .both)
        let edge = try XCTUnwrap(doc.scene.edges.first { $0.edgeId == "ab1" })
        XCTAssertEqual(edge.label, "rewired")
        XCTAssertTrue(edge.fromArrow)
        XCTAssertTrue(edge.toArrow)

        // Direction: none (undirected link).
        state.canvasEditConnection(edgeId: "ab1", label: "", direction: .none)
        let undirected = try XCTUnwrap(doc.scene.edges.first { $0.edgeId == "ab1" })
        XCTAssertNil(undirected.label, "empty label clears")
        XCTAssertFalse(undirected.fromArrow)
        XCTAssertFalse(undirected.toArrow)
        // The outline phrases it as linked-with now.
        let neighbors = doc.neighbors(of: "a", session: state.currentSession)
        XCTAssertEqual(
            neighbors.first { $0.edgeId == "ab1" }?.direction, .undirected)
    }

    func testDeleteConnectionSingleAndChoices() async throws {
        let (state, doc) = try await normalizedState()
        doc.selection.selected = "d"  // exactly one connection (dc)
        posted = []
        state.canvasPromptDeleteConnection()
        state.canvasAnnouncer.flushForTests()
        XCTAssertNil(doc.scene.edges.first { $0.edgeId == "dc" }, "single choice deletes directly")
        XCTAssertTrue(posted.contains { $0.hasPrefix("Deleted connection") })

        // Multi-choice card opens the picker prompt instead.
        doc.selection.selected = "a"
        state.canvasPromptDeleteConnection()
        guard case .pickConnection(let choices, let toDelete)? = state.canvasPrompt else {
            return XCTFail("expected the connection picker prompt")
        }
        XCTAssertTrue(toDelete)
        XCTAssertEqual(choices.count, 3)
    }
}
