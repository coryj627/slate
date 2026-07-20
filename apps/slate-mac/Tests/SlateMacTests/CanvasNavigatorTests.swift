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

    private final class NativeEventRecorder: @unchecked Sendable {
        private let lock = NSLock()
        private var storage: [CanvasNewFileNativeExecutionEvent] = []

        func append(_ event: CanvasNewFileNativeExecutionEvent) {
            lock.lock()
            storage.append(event)
            lock.unlock()
        }

        func events() -> [CanvasNewFileNativeExecutionEvent] {
            lock.lock()
            defer { lock.unlock() }
            return storage
        }
    }

    private final class SynchronousPreloadGate: @unchecked Sendable {
        private let condition = NSCondition()
        private var entered = false
        private var released = false

        func block() {
            condition.lock()
            entered = true
            condition.broadcast()
            while !released {
                condition.wait()
            }
            condition.unlock()
        }

        func waitUntilEntered() async {
            while true {
                condition.lock()
                let hasEntered = entered
                condition.unlock()
                if hasEntered { return }
                await Task.yield()
            }
        }

        func release() {
            condition.lock()
            released = true
            condition.broadcast()
            condition.unlock()
        }
    }

    private actor AsyncSuspensionGate {
        private var entered = false
        private var entranceWaiters: [CheckedContinuation<Void, Never>] = []
        private var releaseWaiter: CheckedContinuation<Void, Never>?

        func enter() async {
            entered = true
            for waiter in entranceWaiters { waiter.resume() }
            entranceWaiters = []
            await withCheckedContinuation { releaseWaiter = $0 }
        }

        func waitUntilEntered() async {
            guard !entered else { return }
            await withCheckedContinuation { entranceWaiters.append($0) }
        }

        func release() {
            releaseWaiter?.resume()
            releaseWaiter = nil
        }
    }

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

        // Simulate a session teardown: releasing and recreating the document
        // (what a restart does) starts with empty stacks; the journal keeps
        // the durable record (backend test pins that half). Missing-file
        // invalidation deliberately retains the same document and undo stack
        // for recovery, so it is not a session boundary.
        state.releaseAllCanvasDocuments()
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
        try await XCTUnwrap(state.canvasNewCanvasFile()).value
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
        try await XCTUnwrap(state.canvasNewCanvasFile()).value
        guard case .canvas(let second) = state.workspace.activeTab?.item else {
            return XCTFail("second canvas opens")
        }
        XCTAssertEqual(second, "Untitled Canvas 2.canvas")
    }

    func testNewCanvasPreservesTheSoleDirtyMarkdownBufferInItsOwnTab() async throws {
        let state = try await makeState()
        let session = try XCTUnwrap(state.currentSession)
        _ = try session.createExclusive(path: "draft.md", content: "# Saved\n")
        state.openFile("draft.md", target: .newTab)
        await state.noteLoadTask?.value
        state.updateEditorText("# Unsaved\n")
        let sourceTabID = try XCTUnwrap(state.workspace.activeTab?.id)

        try await XCTUnwrap(state.canvasNewCanvasFile()).value

        XCTAssertEqual(
            state.workspace.model.tab(sourceTabID)?.item,
            .markdown(path: "draft.md"),
            "creating a document must not replace the only owner of a dirty buffer")
        XCTAssertEqual(
            state.workspace.document(for: sourceTabID)?.text,
            "# Unsaved\n")
        XCTAssertEqual(
            state.workspace.document(for: sourceTabID)?.hasUnsavedChanges,
            true)

        state.activateTab(sourceTabID)
        XCTAssertEqual(state.currentNoteText, "# Unsaved\n")
        XCTAssertTrue(state.hasUnsavedChanges)
    }

    func testNewCanvasNativePreparationAndLandingNeverUseMainThreadFFI() async throws {
        let state = try await makeState()
        let recorder = NativeEventRecorder()
        state.canvasNewFileNativeExecutionObserverForTesting = { event in
            recorder.append(event)
        }

        try await XCTUnwrap(state.canvasNewCanvasFile()).value

        let events = recorder.events()
        let phases = events.map(\.phase)
        XCTAssertTrue(phases.contains(.create), "missing create phase: \(phases)")
        XCTAssertTrue(phases.contains(.open), "missing open phase: \(phases)")
        XCTAssertTrue(phases.contains(.outline), "missing outline phase: \(phases)")
        XCTAssertTrue(phases.contains(.table), "missing table phase: \(phases)")
        XCTAssertTrue(phases.contains(.scene), "missing scene phase: \(phases)")
        XCTAssertFalse(
            phases.contains(.activationLoad),
            "prepared activation must not fall back to CanvasDocument.load")
        XCTAssertTrue(
            events.allSatisfy { !$0.ranOnMainThread },
            "all New Canvas native calls must stay off-main: \(events)")
    }

    func testNewCanvasReusesMissingPathDocumentAndResetsOldIdentityState() async throws {
        let state = try await makeState()
        let originalTabID = try XCTUnwrap(state.workspace.activeTab?.id)

        state.openFile("Untitled Canvas.canvas", target: .newTab)
        let missingDocument = state.canvasDocument(for: "Untitled Canvas.canvas")
        guard case .failed = missingDocument.state else {
            return XCTFail("the pre-existing path should begin as a missing-file document")
        }
        let missingTabID = try XCTUnwrap(state.workspace.activeTab?.id)

        missingDocument.selection.selected = "old-selection"
        missingDocument.selection.marked = ["old-mark"]
        missingDocument.lastActivatedNode = "old-activation"
        missingDocument.undoStack = [
            (name: "old undo", inverse: CanvasAction(name: "old", ops: []))
        ]
        missingDocument.redoStack = [
            (name: "old redo", inverse: CanvasAction(name: "old", ops: []))
        ]
        missingDocument.filterText = "old filter"
        missingDocument.transientRects = [:]
        missingDocument.viewport.scale = 2
        missingDocument.viewport.offset = CGPoint(x: 42, y: 24)
        missingDocument.viewport.followSelection = false
        _ = state.canvasModeController(for: missingDocument)
        state.activateTab(originalTabID)

        let recorder = NativeEventRecorder()
        state.canvasNewFileNativeExecutionObserverForTesting = { event in
            recorder.append(event)
        }
        try await XCTUnwrap(state.canvasNewCanvasFile()).value

        XCTAssertTrue(
            state.canvasDocument(for: "Untitled Canvas.canvas") === missingDocument,
            "same-path tabs retain their shared Swift document object")
        XCTAssertEqual(state.workspace.activeTab?.id, missingTabID)
        XCTAssertEqual(missingDocument.state, .ready)
        XCTAssertNotNil(missingDocument.handle)
        XCTAssertNil(missingDocument.selection.selected)
        XCTAssertTrue(missingDocument.selection.marked.isEmpty)
        XCTAssertNil(missingDocument.lastActivatedNode)
        XCTAssertTrue(missingDocument.undoStack.isEmpty)
        XCTAssertTrue(missingDocument.redoStack.isEmpty)
        XCTAssertEqual(missingDocument.filterText, "")
        XCTAssertNil(missingDocument.transientRects)
        XCTAssertEqual(missingDocument.viewport.scale, 1)
        XCTAssertEqual(missingDocument.viewport.offset, .zero)
        XCTAssertTrue(missingDocument.viewport.followSelection)
        XCTAssertNil(state.canvasModeControllers["Untitled Canvas.canvas"])
        XCTAssertFalse(
            recorder.events().contains { $0.phase == .activationLoad },
            "the reused object must consume the prepared snapshot")
    }

    func testNewCanvasReservationSurvivesTabAwayAndBackDuringBlockedPreload() async throws {
        let state = try await makeState()
        let originalTabID = try XCTUnwrap(state.workspace.activeTab?.id)
        state.openFile("Untitled Canvas.canvas", target: .newTab)
        let missingTabID = try XCTUnwrap(state.workspace.activeTab?.id)
        let reservedDocument = state.canvasDocument(for: "Untitled Canvas.canvas")
        state.activateTab(originalTabID)

        let recorder = NativeEventRecorder()
        state.canvasNewFileNativeExecutionObserverForTesting = { event in
            recorder.append(event)
        }
        let gate = SynchronousPreloadGate()
        let realPreloader = state.canvasNewFilePreloadRunner
        state.canvasNewFilePreloadRunner = { session, path, observer in
            gate.block()
            return realPreloader(session, path, observer)
        }

        let creation = try XCTUnwrap(state.canvasNewCanvasFile())
        await gate.waitUntilEntered()
        XCTAssertTrue(reservedDocument.hasPreparedLoadReservation)

        state.activateTab(missingTabID)
        XCTAssertNil(reservedDocument.handle)
        XCTAssertEqual(reservedDocument.state, .loading)
        state.activateTab(originalTabID)
        state.activateTab(missingTabID)
        XCTAssertFalse(
            recorder.events().contains { $0.phase == .activationLoad },
            "navigation during preparation must trust the published reservation")

        gate.release()
        await creation.value
        XCTAssertTrue(
            state.canvasDocument(for: "Untitled Canvas.canvas") === reservedDocument)
        XCTAssertEqual(reservedDocument.state, .ready)
        XCTAssertNotNil(reservedDocument.handle)
        XCTAssertFalse(recorder.events().contains { $0.phase == .activationLoad })
    }

    func testNewCanvasCloseLastSamePathTabDuringRefreshKeepsPreparedOwner() async throws {
        let state = try await makeState()
        let originalTabID = try XCTUnwrap(state.workspace.activeTab?.id)
        state.openFile("Untitled Canvas.canvas", target: .newTab)
        let missingTabID = try XCTUnwrap(state.workspace.activeTab?.id)
        let reservedDocument = state.canvasDocument(for: "Untitled Canvas.canvas")
        state.activateTab(originalTabID)

        let refresh = AsyncSuspensionGate()
        state.structuralBatchRefreshRunner = { _ in await refresh.enter() }
        let recorder = NativeEventRecorder()
        state.canvasNewFileNativeExecutionObserverForTesting = { event in
            recorder.append(event)
        }
        let creation = try XCTUnwrap(state.canvasNewCanvasFile())
        await refresh.waitUntilEntered()

        XCTAssertTrue(reservedDocument.hasPreparedLoadReservation)
        state.performCloseTab(missingTabID)
        XCTAssertFalse(
            state.workspace.model.allTabs.contains {
                $0.item == .canvas(path: "Untitled Canvas.canvas")
            })
        XCTAssertTrue(
            state.canvasDocuments["Untitled Canvas.canvas"] === reservedDocument,
            "the reservation remains registry-owned after its last tab closes")

        await refresh.release()
        await creation.value
        XCTAssertTrue(
            state.canvasDocument(for: "Untitled Canvas.canvas") === reservedDocument)
        XCTAssertEqual(reservedDocument.state, .ready)
        XCTAssertNotNil(reservedDocument.handle)
        guard case .canvas(let path) = state.workspace.activeTab?.item else {
            return XCTFail("completion should reopen the created canvas")
        }
        XCTAssertEqual(path, "Untitled Canvas.canvas")
        XCTAssertFalse(recorder.events().contains { $0.phase == .activationLoad })
        XCTAssertFalse(
            recorder.events().contains { $0.phase == .closePrepared },
            "the installed prepared handle must remain owned by the live document")
    }

    func testNewCanvasPreparedFailureStillLandsCreatedFileWithoutActivationLoad() async throws {
        let state = try await makeState()
        let recorder = NativeEventRecorder()
        state.canvasNewFileNativeExecutionObserverForTesting = { event in
            recorder.append(event)
        }
        state.canvasNewFilePreloadRunner = { _, _, _ in
            .failed("Injected prepared load failure.")
        }

        try await XCTUnwrap(state.canvasNewCanvasFile()).value

        let vault = try XCTUnwrap(state.currentVaultURL)
        XCTAssertTrue(
            FileManager.default.fileExists(
                atPath: vault.appendingPathComponent("Untitled Canvas.canvas").path),
            "native preparation failure must not misreport the successful file create")
        guard case .canvas(let path) = state.workspace.activeTab?.item else {
            return XCTFail("the created canvas should still land in a tab")
        }
        XCTAssertEqual(path, "Untitled Canvas.canvas")
        XCTAssertEqual(
            state.canvasDocument(for: path).state,
            .failed("Injected prepared load failure."))
        XCTAssertFalse(
            recorder.events().contains { $0.phase == .activationLoad },
            "the immediate activation must preserve the prepared error state")
        state.canvasAnnouncer.flushForTests()
        XCTAssertTrue(posted.contains("Created canvas \"Untitled Canvas\"."))
    }

    func testNewCanvasReplacesStaleSamePathHandleOffMainExactlyOnce() async throws {
        let state = try await makeState()
        let session = try XCTUnwrap(state.currentSession)
        _ = try session.createExclusive(
            path: "Untitled Canvas.canvas",
            content: "{}\n")
        let originalTabID = try XCTUnwrap(state.workspace.activeTab?.id)
        state.openFile("Untitled Canvas.canvas", target: .newTab)
        let reusedDocument = state.canvasDocument(for: "Untitled Canvas.canvas")
        XCTAssertNotNil(reusedDocument.handle)
        try session.deleteFile(path: "Untitled Canvas.canvas")
        state.activateTab(originalTabID)

        let recorder = NativeEventRecorder()
        state.canvasNewFileNativeExecutionObserverForTesting = { event in
            recorder.append(event)
        }
        try await XCTUnwrap(state.canvasNewCanvasFile()).value

        XCTAssertTrue(
            state.canvasDocument(for: "Untitled Canvas.canvas") === reusedDocument)
        XCTAssertEqual(reusedDocument.state, .ready)
        XCTAssertNotNil(reusedDocument.handle)
        let replacedCloses = recorder.events().filter { $0.phase == .closeReplaced }
        XCTAssertEqual(replacedCloses.count, 1)
        XCTAssertTrue(replacedCloses.allSatisfy { !$0.ranOnMainThread })
        XCTAssertFalse(recorder.events().contains { $0.phase == .activationLoad })
    }

    func testPreparedLoaderReleasesDegradedOpenHandleOffMainExactlyOnce() async throws {
        let state = try await makeState()
        let session = try XCTUnwrap(state.currentSession)
        _ = try session.createExclusive(
            path: "invalid.canvas",
            content: "not json canvas")
        let recorder = NativeEventRecorder()

        let prepared = await Task.detached(priority: .userInitiated) {
            CanvasPreparedLoader.prepare(
                session: session,
                path: "invalid.canvas",
                observer: { event in recorder.append(event) })
        }.value

        guard case .degraded = prepared else {
            return XCTFail("invalid JSON should produce a prepared degraded state")
        }
        let events = recorder.events()
        XCTAssertEqual(events.filter { $0.phase == .open }.count, 1)
        XCTAssertEqual(events.filter { $0.phase == .closePrepared }.count, 1)
        XCTAssertFalse(events.contains { $0.phase == .outline })
        XCTAssertTrue(events.allSatisfy { !$0.ranOnMainThread })
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

/// #524: mark-then-act — cross-surface marks, bulk actions as one
/// action/undo/summary, selection-vs-marks separation.
@MainActor
extension CanvasNavigatorTests {
    func testToggleMarkAnnouncesCountsAndArrowsNeverMutateMarks() async throws {
        let (state, doc) = try await normalizedState()
        doc.selection.selected = "a"
        posted = []
        state.canvasToggleMark()
        state.canvasAnnouncer.flushForTests()
        XCTAssertTrue(posted.contains("Marked \"Alpha\". 1 marked."), "\(posted)")
        XCTAssertTrue(doc.selection.marked.contains("a"))

        // Arrows move selection and never mutate marks (t4 pin).
        state.canvasSelectAdjacent(offset: 1)
        state.canvasSelectAdjacent(offset: 1)
        XCTAssertEqual(doc.selection.marked, ["a"])

        state.canvasSelect(nodeId: "a", in: doc, announce: false)
        posted = []
        state.canvasToggleMark()
        state.canvasAnnouncer.flushForTests()
        XCTAssertTrue(posted.contains("Unmarked \"Alpha\". 0 marked."))
    }

    func testBulkDeleteIsOneActionOneUndoOneSummary() async throws {
        let (state, doc) = try await normalizedState()
        doc.selection.marked = ["a", "b"]
        doc.selection.selected = "a"
        let outlineBefore = doc.outline.count
        let undoBefore = doc.undoStack.count
        posted = []
        state.canvasDeleteMarked()
        state.canvasAnnouncer.flushForTests()

        XCTAssertEqual(doc.outline.count, outlineBefore - 2)
        XCTAssertEqual(doc.undoStack.count, undoBefore + 1, "one undo step")
        XCTAssertEqual(
            posted.filter { $0.hasPrefix("Deleted 2 cards") }.count, 1,
            "exactly one summary: \(posted)")
        XCTAssertTrue(doc.selection.marked.isEmpty, "marks clear after the bulk act")

        // Single undo restores BOTH cards and their connections.
        state.canvasUndo()
        XCTAssertEqual(doc.outline.count, outlineBefore)
        XCTAssertEqual(
            doc.neighbors(of: "a", session: state.currentSession).count, 3)
    }

    func testBulkColorAndGroupMarked() async throws {
        let (state, doc) = try await normalizedState()
        doc.selection.marked = ["c", "d"]
        posted = []
        state.canvasColorMarked(preset: 4)
        state.canvasAnnouncer.flushForTests()
        XCTAssertEqual(posted.filter { $0 == "Set 2 cards to green." }.count, 1)
        XCTAssertEqual(doc.tableRows.first { $0.nodeId == "c" }?.colorName, "green")
        XCTAssertEqual(doc.undoStack.last?.name, "color 2 cards")

        // Group: one bounding group; geometric containment reparen ts.
        state.canvasGroupMarked(label: "Pair")
        state.canvasAnnouncer.flushForTests()
        XCTAssertTrue(posted.contains("Grouped 2 cards into \"Pair\"."))
        XCTAssertEqual(doc.outline.first { $0.nodeId == "c" }?.groupPath, ["Pair"])
        XCTAssertEqual(doc.outline.first { $0.nodeId == "d" }?.groupPath, ["Pair"])
        XCTAssertEqual(doc.undoStack.last?.name, "group 2 cards")
        // One undo removes the frame; children stay.
        state.canvasUndo()
        XCTAssertEqual(doc.outline.first { $0.nodeId == "c" }?.groupPath, [])
    }

    func testMarksAreInspectableInOutlineValuesAndClearOnClose() async throws {
        let (state, doc) = try await normalizedState()
        doc.selection.marked = ["b"]
        // t0 §3: the outline row VALUE carries "marked" (same string
        // pipeline the view uses).
        let row = try XCTUnwrap(doc.outline.first { $0.nodeId == "b" })
        var value = "\(row.ordinalN) of \(row.totalM) in \(row.groupPath.last ?? "canvas")"
        if doc.selection.marked.contains("b") { value += ", marked" }
        XCTAssertTrue(value.hasSuffix(", marked"))

        // Marks clear when the last tab for the path closes (t2).
        let tabID = try XCTUnwrap(state.workspace.model.activeGroup.activeTabID)
        state.requestCloseTab(tabID)
        XCTAssertNil(state.canvasDocuments["nav.canvas"], "document released with the last tab")
    }
}

/// #368 part 2: the real text-card editor (Esc commits, one action,
/// one undo), creation for every card kind, Locate… repointing, and
/// Remove from Group.
@MainActor
extension CanvasNavigatorTests {
    func testEditCardCommitIsOneActionOneUndo() async throws {
        let (state, doc) = try await normalizedState()
        state.canvasSelect(nodeId: "a", in: doc, announce: false)
        state.canvasEditCard()
        let request = try XCTUnwrap(state.canvasCardEditor)
        XCTAssertEqual(request.initialText, "Alpha")

        let undoBefore = doc.undoStack.count
        posted = []
        state.canvasCommitCardEdit(nodeId: "a", newText: "Alpha revised")
        state.canvasAnnouncer.flushForTests()
        XCTAssertNil(state.canvasCardEditor, "commit dismisses the editor")
        XCTAssertEqual(doc.undoStack.count, undoBefore + 1, "one undo step")
        XCTAssertTrue(posted.contains("Updated \"Alpha\"."), "\(posted)")
        let text = try state.currentSession?.canvasNodeText(
            handle: doc.handle ?? 0, nodeId: "a")
        XCTAssertEqual(text ?? nil, "Alpha revised")

        state.canvasUndo()
        let reverted = try state.currentSession?.canvasNodeText(
            handle: doc.handle ?? 0, nodeId: "a")
        XCTAssertEqual(reverted ?? nil, "Alpha")
    }

    func testEditCardNoChangeWritesNothing() async throws {
        let (state, doc) = try await normalizedState()
        state.canvasSelect(nodeId: "b", in: doc, announce: false)
        state.canvasEditCard()
        let undoBefore = doc.undoStack.count
        posted = []
        state.canvasCommitCardEdit(nodeId: "b", newText: "Beta")
        state.canvasAnnouncer.flushForTests()
        XCTAssertEqual(doc.undoStack.count, undoBefore, "no-op commit adds no undo entry")
        XCTAssertTrue(posted.contains("No changes."))
        XCTAssertNil(state.canvasCardEditor)
    }

    func testNewCardLandsInEditMode() async throws {
        let (state, doc) = try await normalizedState()
        state.canvasSelect(nodeId: "d", in: doc, announce: false)
        state.canvasNewCard()
        let request = try XCTUnwrap(state.canvasCardEditor, "G22: new card lands in edit mode")
        XCTAssertEqual(request.initialText, "")
        XCTAssertEqual(doc.selection.selected, request.nodeId)
    }

    func testAddFileCardAndLocateRepoint() async throws {
        let (state, doc) = try await normalizedState()
        state.canvasSelect(nodeId: "c", in: doc, announce: false)
        posted = []
        state.canvasAddFileCard(path: "notes/first.md")
        state.canvasAnnouncer.flushForTests()
        let fileRow = try XCTUnwrap(doc.outline.first { $0.kind == "file" })
        XCTAssertEqual(doc.target(of: fileRow.nodeId), "notes/first.md")
        XCTAssertTrue(
            posted.contains { $0.contains("first.md") }, "creation announced: \(posted)")

        // Locate… repoints the SAME card — one action, one undo.
        let undoBefore = doc.undoStack.count
        state.canvasLocate(nodeId: fileRow.nodeId, path: "notes/second.md")
        XCTAssertEqual(doc.target(of: fileRow.nodeId), "notes/second.md")
        XCTAssertEqual(doc.undoStack.count, undoBefore + 1)
        state.canvasUndo()
        XCTAssertEqual(doc.target(of: fileRow.nodeId), "notes/first.md")
    }

    func testAddLinkCardValidatesURL() async throws {
        let (state, doc) = try await normalizedState()
        let countBefore = doc.outline.count
        posted = []
        state.canvasAddLinkCard(url: "not a url")
        state.canvasAnnouncer.flushForTests()
        XCTAssertEqual(doc.outline.count, countBefore, "garbage URL creates nothing")
        XCTAssertTrue(posted.contains { $0.contains("URL") })

        state.canvasAddLinkCard(url: "https://example.com/x")
        let linkRow = try XCTUnwrap(doc.outline.first { $0.kind == "link" })
        XCTAssertEqual(doc.target(of: linkRow.nodeId), "https://example.com/x")
    }

    func testRemoveFromGroupPlacesOutsideAndUndoes() async throws {
        let (state, doc) = try await normalizedState()
        state.canvasSelect(nodeId: "a", in: doc, announce: false)
        XCTAssertEqual(doc.outline.first { $0.nodeId == "a" }?.groupPath, ["Zone"])
        posted = []
        state.canvasRemoveFromGroup()
        state.canvasAnnouncer.flushForTests()
        XCTAssertEqual(
            doc.outline.first { $0.nodeId == "a" }?.groupPath, [],
            "card left the group via engine placement")
        XCTAssertTrue(posted.contains("Removed from group \"Zone\"."), "\(posted)")
        state.canvasUndo()
        XCTAssertEqual(doc.outline.first { $0.nodeId == "a" }?.groupPath, ["Zone"])

        // Ungrouped card: honest status, no mutation.
        state.canvasSelect(nodeId: "c", in: doc, announce: false)
        let undoBefore = doc.undoStack.count
        state.canvasRemoveFromGroup()
        XCTAssertEqual(doc.undoStack.count, undoBefore)
    }
}

/// #525: parity extras — create-connected-card, duplicate (single +
/// marked set + group expansion), convert-to-note, subpath open.
@MainActor
extension CanvasNavigatorTests {
    func testCreateConnectedCardIsOneActionWithEdgeAndEditor() async throws {
        let (state, doc) = try await normalizedState()
        state.canvasSelect(nodeId: "d", in: doc, announce: false)
        let undoBefore = doc.undoStack.count
        let edgesBefore = doc.scene.edges.count
        posted = []
        state.canvasCreateConnectedCard()
        state.canvasAnnouncer.flushForTests()

        let editor = try XCTUnwrap(state.canvasCardEditor, "lands in edit mode")
        XCTAssertEqual(doc.undoStack.count, undoBefore + 1, "card + edge = ONE action")
        XCTAssertEqual(doc.scene.edges.count, edgesBefore + 1)
        let edge = try XCTUnwrap(
            doc.scene.edges.first { $0.toNode == editor.nodeId })
        XCTAssertEqual(edge.fromNode, "d")
        XCTAssertTrue(
            posted.contains { $0.hasPrefix("Created connected card") && $0.contains("Delta") },
            "\(posted)")

        // One undo removes BOTH the card and its connection.
        state.canvasCardEditor = nil
        state.canvasUndo()
        XCTAssertEqual(doc.scene.edges.count, edgesBefore)
        XCTAssertNil(doc.outline.first { $0.nodeId == editor.nodeId })
    }

    func testDuplicateSingleCardCopiesContent() async throws {
        let (state, doc) = try await normalizedState()
        state.canvasSelect(nodeId: "c", in: doc, announce: false)
        let undoBefore = doc.undoStack.count
        posted = []
        state.canvasDuplicate()
        state.canvasAnnouncer.flushForTests()
        XCTAssertEqual(doc.undoStack.count, undoBefore + 1)
        let copies = doc.outline.filter { $0.title == "Gamma" }
        XCTAssertEqual(copies.count, 2, "duplicate carries the text content")
        XCTAssertTrue(posted.contains { $0.hasPrefix("Duplicated \"Gamma\"") }, "\(posted)")
        // No overlap with the source (engine placement).
        let ids = copies.map(\.nodeId)
        let rects = ids.compactMap { id in doc.scene.nodes.first { $0.nodeId == id } }
        XCTAssertEqual(rects.count, 2)
        let disjoint =
            rects[0].x + rects[0].width <= rects[1].x || rects[1].x + rects[1].width <= rects[0].x
            || rects[0].y + rects[0].height <= rects[1].y
            || rects[1].y + rects[1].height <= rects[0].y
        XCTAssertTrue(disjoint, "copies must not stack on the source")
    }

    func testDuplicateGroupExpandsToMembersAsOneAction() async throws {
        let (state, doc) = try await normalizedState()
        state.canvasSelect(nodeId: "g1", in: doc, announce: false)
        let undoBefore = doc.undoStack.count
        let countBefore = doc.outline.count
        posted = []
        state.canvasDuplicate()
        state.canvasAnnouncer.flushForTests()
        // Group frame + Alpha + Beta = 3 new entries, ONE action.
        XCTAssertEqual(doc.outline.count, countBefore + 3)
        XCTAssertEqual(doc.undoStack.count, undoBefore + 1)
        XCTAssertEqual(doc.outline.filter { $0.title == "Zone" }.count, 2)
        XCTAssertEqual(doc.outline.filter { $0.title == "Alpha" }.count, 2)
        XCTAssertTrue(posted.contains("Duplicated 3 cards — one undo restores."), "\(posted)")
        // The duplicated members are inside the duplicated frame
        // (geometric parenting preserved by rigid set placement).
        let newZone = try XCTUnwrap(
            doc.outline.filter { $0.title == "Zone" }.first { $0.nodeId != "g1" })
        let newAlpha = try XCTUnwrap(
            doc.outline.filter { $0.title == "Alpha" }.first { $0.nodeId != "a" })
        XCTAssertEqual(newAlpha.groupPath.last, newZone.title)

        state.canvasUndo()
        XCTAssertEqual(doc.outline.count, countBefore, "one undo restores all 3")
    }

    func testConvertCardToNoteCreatesFileAndRetargets() async throws {
        let (state, doc) = try await normalizedState()
        state.canvasSelect(nodeId: "b", in: doc, announce: false)
        posted = []
        try await XCTUnwrap(
            state.canvasConvertToNote(nodeId: "b", path: "Beta.md")
        ).value
        state.canvasAnnouncer.flushForTests()

        let vault = try XCTUnwrap(state.currentVaultURL)
        let noteURL = vault.appendingPathComponent("Beta.md")
        XCTAssertTrue(FileManager.default.fileExists(atPath: noteURL.path))
        XCTAssertEqual(try String(contentsOf: noteURL, encoding: .utf8), "Beta")
        let row = try XCTUnwrap(doc.outline.first { $0.nodeId == "b" })
        XCTAssertEqual(row.kind, "file", "card retargeted at the note")
        XCTAssertEqual(doc.target(of: "b"), "Beta.md")
        XCTAssertTrue(posted.contains { $0.hasPrefix("Converted to note Beta.md") })

        // Canvas undo restores the TEXT card; the note file remains
        // (U2 convention: file ops journal separately).
        state.canvasUndo()
        XCTAssertEqual(doc.outline.first { $0.nodeId == "b" }?.kind, "text")
        XCTAssertTrue(FileManager.default.fileExists(atPath: noteURL.path))

        // Bad extension: honest error, nothing written.
        XCTAssertNil(state.canvasConvertToNote(nodeId: "b", path: "nope.txt"))
        state.canvasAnnouncer.flushForTests()
        XCTAssertTrue(posted.contains { $0.contains("must end in .md") })
    }

    func testSubpathCardTitleAndSceneCarrySubpath() async throws {
        // Dedicated vault: a note with a heading + a canvas whose file
        // card narrows to it (t5: "Note › Heading" display).
        let vault = tempDir.appendingPathComponent("vault-sub-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        try Data("# Top\n\n## Target Head\n\nBody.\n".utf8)
            .write(to: vault.appendingPathComponent("n.md"))
        let canvas = """
            {"nodes":[{"id":"f1","type":"file","file":"n.md","subpath":"#Target Head",\
            "x":0,"y":0,"width":200,"height":100}],"edges":[]}
            """
        try Data(canvas.utf8).write(to: vault.appendingPathComponent("s.canvas"))
        let store = RecentVaultsStore(
            fileURL: tempDir.appendingPathComponent("recents-\(UUID().uuidString).json"))
        let state = AppState(recentsStore: store, externalOpener: { _ in true })
        state.openVault(at: vault)
        await state.scanTask?.value
        state.openFile("s.canvas", target: .currentTab)
        let doc = state.canvasDocument(for: "s.canvas")

        let row = try XCTUnwrap(doc.outline.first { $0.nodeId == "f1" })
        XCTAssertTrue(row.title.hasSuffix("› Target Head"), row.title)
        XCTAssertEqual(
            doc.scene.nodes.first { $0.nodeId == "f1" }?.subpath, "#Target Head")

        // Open-to-anchor: the load lands, then the anchor routes.
        var anchors: [String] = []
        let sub = state.scrollAnchorRequest.sink { anchors.append($0) }
        state.canvasOpenFileAtHeading(path: "n.md", heading: "Target Head")
        for _ in 0..<40 where anchors.isEmpty {
            try await Task.sleep(nanoseconds: 50_000_000)
        }
        sub.cancel()
        XCTAssertEqual(anchors.count, 1, "anchor scroll fired once")
        XCTAssertEqual(state.selectedFilePath, "n.md")
    }
}

/// #373: the in-canvas filter — a view, never a mutation; navigator
/// honors the filtered set; Esc rung; coalesced count announcements.
@MainActor
extension CanvasNavigatorTests {
    func testFilterNarrowsOutlineAndTableAndNavigatorHonorsIt() async throws {
        let (state, doc) = try await normalizedState()
        doc.filterText = "gamma"
        XCTAssertEqual(doc.filteredOutline.map(\.title), ["Gamma"])

        // Navigator walks ONLY matches while active.
        doc.selection.selected = nil
        state.canvasSelectAdjacent(offset: 1)
        XCTAssertEqual(doc.selection.selected, "c")
        posted = []
        state.canvasSelectAdjacent(offset: 1)
        state.canvasAnnouncer.flushForTests()
        XCTAssertTrue(posted.contains("End of canvas."), "\(posted)")

        // Kind + group-label matches work; the file stays untouched
        // (a view, never a mutation).
        doc.filterText = "group"
        XCTAssertEqual(doc.filteredOutline.map(\.nodeId), ["g1"])
        doc.filterText = "zone"
        XCTAssertTrue(Set(doc.filteredOutline.map(\.nodeId)).isSuperset(of: ["g1", "a", "b"]))
        doc.filterText = ""
        XCTAssertEqual(doc.filteredOutline.count, doc.outline.count)
    }

    func testFilterCountAnnouncesAndClearRestores() async throws {
        let (state, doc) = try await normalizedState()
        doc.filterText = "alp"
        posted = []
        state.canvasAnnounceFilterCount(doc: doc)
        state.canvasAnnouncer.flushForTests()
        XCTAssertTrue(posted.contains("1 card match."), "\(posted)")

        posted = []
        state.canvasClearFilter()
        state.canvasAnnouncer.flushForTests()
        XCTAssertEqual(doc.filterText, "")
        XCTAssertTrue(posted.contains("Filter cleared — 5 cards."), "\(posted)")

        // No-match narration on movement.
        doc.filterText = "zzz-nothing"
        posted = []
        state.canvasSelectAdjacent(offset: 1)
        state.canvasAnnouncer.flushForTests()
        XCTAssertTrue(posted.contains("No cards match the filter."), "\(posted)")
        doc.filterText = ""
    }

    func testWhereAmIDisclosesActiveFilter() async throws {
        let (state, doc) = try await normalizedState()
        state.canvasSelect(nodeId: "a", in: doc, announce: false)
        doc.filterText = "alpha"
        state.canvasWhereAmI()
        let readback = try XCTUnwrap(state.canvasWhereAmIReadback)
        XCTAssertTrue(
            readback.contains("Filter active: 1 of 5 cards matches."), readback)
    }
}
