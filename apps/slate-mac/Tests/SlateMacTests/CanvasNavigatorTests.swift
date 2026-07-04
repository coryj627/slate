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
