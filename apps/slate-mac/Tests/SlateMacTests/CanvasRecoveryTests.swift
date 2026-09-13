// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import SwiftUI
import XCTest

@testable import SlateMac

private final class CanvasRecoveryProbe: @unchecked Sendable {
    private let lock = NSLock()
    private var events: [CanvasNewFileNativeExecutionEvent] = []

    func record(_ event: CanvasNewFileNativeExecutionEvent) {
        lock.lock()
        defer { lock.unlock() }
        events.append(event)
    }

    var closes: Int {
        lock.lock()
        defer { lock.unlock() }
        return events.filter { $0.phase == .closePrepared }.count
    }
}

/// A real malformed top-level section keeps surviving cards readable while
/// every authoring route preserves the exact original file.
@MainActor
final class CanvasRecoveryTests: XCTestCase {
    private var tempDir: URL!

    private static let recovered = #"""
        {"nodes":[
        {"id":"g","type":"group","label":"Survivors","x":0,"y":0,"width":600,"height":400},
        {"id":"a","type":"text","text":"Survived text\nSecond line","x":20,"y":20,"width":180,"height":80},
        {"id":"f","type":"file","file":"note.md","x":240,"y":20,"width":180,"height":80}
        ],"edges":{"bad":true},"extension":{"keep":[1,2,3]}}
        """#

    private static var repaired: String {
        recovered.replacingOccurrences(of: #""edges":{"bad":true}"#, with: #""edges":[]"#)
    }

    override func setUpWithError() throws {
        try super.setUpWithError()
        tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-canvas-recovery-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: tempDir, withIntermediateDirectories: true)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: tempDir)
        try super.tearDownWithError()
    }

    private func makeState(_ source: String = recovered) async throws -> AppState {
        let vault = tempDir.appendingPathComponent("vault-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        try Data(source.utf8).write(to: vault.appendingPathComponent("board.canvas"))
        try Data("# Note".utf8).write(to: vault.appendingPathComponent("note.md"))
        let state = AppState(
            recentsStore: RecentVaultsStore(
                fileURL: tempDir.appendingPathComponent("recents-\(UUID().uuidString).json")),
            externalOpener: { _ in true })
        state.openVault(at: vault)
        await state.scanTask?.value
        state.openFile("board.canvas", target: .currentTab)
        return state
    }

    private func replaceSource(_ source: String, in state: AppState) throws {
        let vault = try XCTUnwrap(state.currentVaultURL)
        try Data(source.utf8).write(to: vault.appendingPathComponent("board.canvas"))
    }

    func testRecoveredSnapshotKeepsRealReadQueriesAndExactSource() async throws {
        let state = try await makeState()
        let doc = try XCTUnwrap(state.activeCanvasDocument)
        let session = try XCTUnwrap(state.currentSession)
        let handle = try XCTUnwrap(doc.handle)
        XCTAssertEqual(doc.state, .ready)
        XCTAssertEqual(doc.disposition, .recoveredReadOnly)
        XCTAssertEqual(doc.outline.map(\.nodeId), ["g", "a", "f"])
        XCTAssertEqual(doc.tableRows.count, 3)
        XCTAssertEqual(doc.scene.nodes.count, 3)
        let snapshot = try session.canvasCurrentText(handle: handle)
        XCTAssertEqual(snapshot.text, Self.recovered)
        XCTAssertEqual(doc.contentHash, snapshot.contentHash)
        XCTAssertFalse(snapshot.contentHash.isEmpty)
        XCTAssertNil(state.canvasReadRefusal(for: doc))
        XCTAssertNotNil(state.canvasMutationDisabledReason(for: doc))
        state.canvasSelect(nodeId: "g", in: doc, announce: false)
        state.canvasEnterGroup()
        XCTAssertEqual(doc.selection.selected, "a")
        doc.filterText = "Survived"
        XCTAssertTrue(doc.filterView(session: session).current)
        XCTAssertEqual(doc.filterView(session: session).rows.map(\.nodeId), ["a"])
        XCTAssertEqual(
            try session.canvasNodeText(handle: handle, nodeId: "a"), "Survived text\nSecond line")
        XCTAssertEqual(try session.readText(path: "board.canvas"), Self.recovered)

        // A scan sees repaired disk data; this old handle remains the original
        // immutable recovered snapshot until Retry opens a new one.
        try replaceSource(
            Self.repaired.replacingOccurrences(of: "Survived", with: "Changed"), in: state)
        _ = try session.scanInitial(cancel: CancelToken())
        XCTAssertEqual(try session.canvasOutline(handle: handle).map(\.nodeId), ["g", "a", "f"])
        XCTAssertEqual(
            try session.canvasNodeText(handle: handle, nodeId: "a"), "Survived text\nSecond line")
        XCTAssertEqual(doc.disposition, .recoveredReadOnly)
        XCTAssertEqual(try session.canvasCurrentText(handle: handle).text, Self.recovered)
        XCTAssertEqual(try session.canvasCurrentText(handle: handle).contentHash, doc.contentHash)
    }

    func testRecoveredWritersRefuseBeforeActionsHistoryAndFileCreation() async throws {
        let state = try await makeState()
        let doc = try XCTUnwrap(state.activeCanvasDocument)
        let session = try XCTUnwrap(state.currentSession)
        let action = CanvasAction(name: "old action", ops: [.deleteNode(id: "a")])
        doc.undoStack = [(name: "old action", inverse: action)]
        doc.redoStack = [(name: "old redo", inverse: action)]
        doc.selection.selected = "a"
        var applied = 0
        state.canvasApplyObserverForTesting = { _ in applied += 1 }
        var posted: [String] = []
        state.canvasAnnouncer = CanvasAnnouncer(verbosity: .standard, coalesceWindow: 60) {
            text, _ in posted.append(text)
        }
        XCTAssertFalse(state.canvasApply(action, to: doc))
        state.canvasDuplicate()
        state.canvasDeleteSelection()
        state.canvasNewCard()
        state.canvasEnterMoveMode()
        state.canvasUndo()
        state.canvasRedo()
        XCTAssertFalse(state.commitCanvasPromptMutation { applied += 1 })
        XCTAssertFalse(state.commitCanvasCardPickerSelection(in: doc) { applied += 1 })
        XCTAssertNil(state.canvasConvertToNote(nodeId: "a", path: "Recovered.md"))
        XCTAssertEqual(applied, 0)
        XCTAssertEqual(doc.undoStack.count, 1)
        XCTAssertEqual(doc.redoStack.count, 1)
        XCTAssertEqual(try session.readText(path: "board.canvas"), Self.recovered)
        XCTAssertFalse(
            FileManager.default.fileExists(
                atPath: try XCTUnwrap(state.currentVaultURL).appendingPathComponent("Recovered.md")
                    .path))
        XCTAssertFalse(posted.isEmpty)
        XCTAssertTrue(posted.allSatisfy { $0.contains("read-only") }, "\(posted)")
    }

    func testRecoveredTextInspectionStaysReadOnlyAfterRepair() async throws {
        let state = try await makeState()
        let doc = try XCTUnwrap(state.activeCanvasDocument)
        state.canvasInspectCard(nodeId: "a")
        let request = try XCTUnwrap(state.canvasCardEditor)
        XCTAssertTrue(request.inspectionOnly)
        XCTAssertEqual(request.initialText, "Survived text\nSecond line")
        XCTAssertFalse(state.canvasCommitCardEdit(nodeId: "a", newText: "must not save"))
        XCTAssertEqual(state.canvasCardEditor, request)
        try replaceSource(Self.repaired, in: state)
        await state.retryCanvasRecovery(for: doc)?.value
        XCTAssertEqual(doc.disposition, .editable)
        XCTAssertNotNil(state.activeCanvasCardEditorDisabledReason)
        XCTAssertFalse(state.canvasCommitCardEdit(nodeId: "a", newText: "still cannot save"))
        XCTAssertEqual(state.canvasCardEditor, request)
    }

    func testRecoveryRetryPreservesIdentityThenClearsOnlyChangedSourceHistory() async throws {
        let state = try await makeState()
        let doc = try XCTUnwrap(state.activeCanvasDocument)
        let session = try XCTUnwrap(state.currentSession)
        let oldHandle = try XCTUnwrap(doc.handle)
        doc.selection.selected = "a"
        doc.selection.marked = ["f"]
        doc.filterText = "Survived"
        let action = CanvasAction(name: "old", ops: [.deleteNode(id: "a")])
        doc.undoStack = [(name: "old", inverse: action)]
        let revision = doc.loadRevision
        XCTAssertNotNil(doc.takeLoadAnnouncement())
        XCTAssertNil(doc.takeLoadAnnouncement())
        await state.retryCanvasRecovery(for: doc)?.value
        XCTAssertTrue(state.activeCanvasDocument === doc)
        XCTAssertEqual(doc.loadRevision, revision + 1)
        XCTAssertEqual(doc.disposition, .recoveredReadOnly)
        XCTAssertEqual(doc.undoStack.count, 1, "same bytes preserve history")
        XCTAssertThrowsError(try session.canvasOutline(handle: oldHandle))
        XCTAssertNotNil(doc.takeLoadAnnouncement())
        XCTAssertNil(doc.takeLoadAnnouncement())

        try replaceSource(Self.repaired, in: state)
        await state.retryCanvasRecovery(for: doc)?.value
        XCTAssertEqual(doc.disposition, .editable)
        XCTAssertNil(state.canvasMutationDisabledReason(for: doc))
        XCTAssertEqual(doc.selection.selected, "a")
        XCTAssertEqual(doc.selection.marked, ["f"])
        XCTAssertEqual(doc.filterText, "Survived")
        XCTAssertTrue(doc.undoStack.isEmpty)
        XCTAssertTrue(doc.redoStack.isEmpty)
        XCTAssertTrue(
            state.canvasApply(
                CanvasAction(name: "color", ops: [.setNodeColor(id: "a", color: "1")]), to: doc))
        state.canvasUndo()
        XCTAssertEqual(
            doc.contentHash,
            try session.canvasCurrentText(handle: XCTUnwrap(doc.handle)).contentHash)
        state.canvasRedo()
        XCTAssertEqual(
            doc.contentHash,
            try session.canvasCurrentText(handle: XCTUnwrap(doc.handle)).contentHash)
        state.canvasEditCard(nodeId: "a")
        XCTAssertNil(
            state.activeCanvasCardEditorDisabledReason, "a fresh post-repair editor stays writable")
        state.dismissCanvasCardEditor()
        await state.canvasConvertToNote(nodeId: "a", path: "Recovered.md")?.value
        XCTAssertEqual(
            doc.contentHash,
            try session.canvasCurrentText(handle: XCTUnwrap(doc.handle)).contentHash)
        try replaceSource(Self.recovered, in: state)
        doc.load(session: session)
        XCTAssertEqual(
            doc.disposition, .recoveredReadOnly, "a later malformed reload removes write capability"
        )
    }

    func testExistingDirtyEditorSurvivesRecoveryAndChangedSourceRetry() async throws {
        let state = try await makeState(Self.repaired)
        let doc = try XCTUnwrap(state.activeCanvasDocument)
        let session = try XCTUnwrap(state.currentSession)
        state.canvasEditCard(nodeId: "a")
        let request = try XCTUnwrap(state.canvasCardEditor)
        state.canvasCardEditorDraft = "unsaved draft to keep"
        try replaceSource(Self.recovered, in: state)
        doc.load(session: session)
        XCTAssertFalse(
            state.canvasCommitCardEdit(nodeId: "a", newText: state.canvasCardEditorDraft))
        XCTAssertEqual(state.canvasCardEditor, request)
        XCTAssertFalse(
            request.inspectionOnly, "existing drafts retain confirmation-on-close behavior")
        XCTAssertEqual(state.canvasCardEditorDraft, "unsaved draft to keep")
        try replaceSource(
            Self.repaired.replacingOccurrences(of: "Survived", with: "Repaired"), in: state)
        await state.retryCanvasRecovery(for: doc)?.value
        XCTAssertEqual(doc.disposition, .editable)
        XCTAssertNotNil(state.activeCanvasCardEditorDisabledReason)
        XCTAssertFalse(
            state.canvasCommitCardEdit(nodeId: "a", newText: state.canvasCardEditorDraft))
        XCTAssertEqual(state.canvasCardEditorDraft, "unsaved draft to keep")
    }

    func testPreparedRecoveryTransfersHandleAndStaleGenerationReleasesIt() async throws {
        let state = try await makeState()
        let session = try XCTUnwrap(state.currentSession)
        let doc = try XCTUnwrap(state.activeCanvasDocument)
        let reservation = doc.beginBatchRetarget(to: "board.canvas")
        if let old = reservation.replacedHandle { session.closeCanvas(handle: old) }
        XCTAssertEqual(doc.claimRetargetPreparation(), reservation.generation)
        let probe = CanvasRecoveryProbe()
        let prepared = await Task.detached {
            CanvasPreparedLoader.prepare(
                session: session, path: "board.canvas", observer: probe.record)
        }.value
        let handle = try XCTUnwrap(prepared.retainedHandle)
        XCTAssertEqual(probe.closes, 0, "readable recovery retains its handle")
        XCTAssertEqual(try session.canvasOutline(handle: handle).count, 3)
        _ = doc.beginBatchRetarget(to: "newer.canvas")
        XCTAssertFalse(
            doc.applyRetargetPreparation(
                prepared, generation: reservation.generation, path: "board.canvas"))
        await Task.detached {
            CanvasPreparedLoader.release(prepared, session: session, observer: probe.record)
        }.value
        XCTAssertEqual(probe.closes, 1)
        XCTAssertThrowsError(try session.canvasOutline(handle: handle))
        XCTAssertNil(doc.handle)
        XCTAssertEqual(doc.path, "newer.canvas")
    }

    func testPreparedUnavailableClosesButReadableRecoveryInstallsAllProjections() async throws {
        let state = try await makeState()
        let session = try XCTUnwrap(state.currentSession)
        let prepared = await Task.detached {
            CanvasPreparedLoader.prepare(session: session, path: "board.canvas", observer: nil)
        }.value
        let document = CanvasDocument(path: "board.canvas")
        document.applyPreparedLoad(prepared)
        XCTAssertEqual(document.disposition, .recoveredReadOnly)
        XCTAssertEqual(document.state, .ready)
        XCTAssertEqual(document.outline.count, 3)
        XCTAssertEqual(document.tableRows.count, 3)
        XCTAssertEqual(document.scene.nodes.count, 3)
        let handle = try XCTUnwrap(document.handle)
        document.close(session: session)
        document.close(session: session)
        XCTAssertThrowsError(try session.canvasOutline(handle: handle))

        for source in ["not json", "[]", #"{"nodes":false,"edges":[]}"#] {
            try replaceSource(source, in: state)
            let probe = CanvasRecoveryProbe()
            let failed = await Task.detached {
                CanvasPreparedLoader.prepare(
                    session: session, path: "board.canvas", observer: probe.record)
            }.value
            XCTAssertNil(failed.retainedHandle)
            XCTAssertEqual(probe.closes, 1)
            document.applyPreparedLoad(failed)
            XCTAssertEqual(document.disposition, .unavailable)
            XCTAssertTrue(document.outline.isEmpty)
            XCTAssertNil(document.contentHash)
        }
    }

    func testOrdinarySkippedItemsStayEditableAndRecoverySummaryCountsCards() async throws {
        let source = #"{"nodes":[{"type":"future-widget","id":"opaque"}],"edges":[]}"#
        let state = try await makeState(source)
        let doc = try XCTUnwrap(state.activeCanvasDocument)
        XCTAssertEqual(doc.disposition, .editable)
        XCTAssertNil(state.canvasMutationDisabledReason(for: doc))
        XCTAssertEqual(doc.preservedItemCount, 1)
        let warning = try XCTUnwrap(doc.takeLoadAnnouncement())
        XCTAssertEqual(
            a11yRender(event: .canvas(event: warning)).text,
            "Canvas loaded. 1 unsupported item are preserved in the file but not shown.")
        let recoveredWithSkipped = Self.recovered.replacingOccurrences(
            of: "\"nodes\":[", with: "\"nodes\":[{\"type\":\"future-widget\",\"id\":\"opaque\"},")
        try replaceSource(recoveredWithSkipped, in: state)
        doc.load(session: try XCTUnwrap(state.currentSession))
        XCTAssertEqual(doc.preservedItemCount, 1)
        XCTAssertTrue(doc.warnings.contains { $0.kind == .parseFailed })
        let recovered = try XCTUnwrap(doc.takeLoadAnnouncement())
        XCTAssertEqual(
            a11yRender(event: .canvas(event: recovered)).text,
            "Canvas opened read-only. 3 cards available to inspect. Repair the file and retry to edit."
        )
        doc.filterText = "absent"
        XCTAssertNil(
            doc.takeLoadAnnouncement(), "filtering cannot repeat or change the load summary")
    }

    func testMountedRecoveredTableKeepsSelectionAndReadableLoadStatus() async throws {
        let state = try await makeState()
        let doc = try XCTUnwrap(state.activeCanvasDocument)
        let tabID = try XCTUnwrap(state.workspace.activeTab?.id)
        state.showCanvasSurface(.table)
        let announced = expectation(description: "mounted recovery status")
        announced.assertForOverFulfill = true
        var posted: [String] = []
        state.canvasAnnouncer = CanvasAnnouncer(verbosity: .standard, coalesceWindow: 60) {
            text, _ in
            posted.append(text)
            if text.hasPrefix("Canvas opened read-only.") { announced.fulfill() }
        }
        let window = NSWindow(
            contentRect: NSRect(x: 0, y: 0, width: 900, height: 600),
            styleMask: [.titled], backing: .buffered, defer: false)
        window.isReleasedWhenClosed = false
        defer { window.orderOut(nil) }
        let host = NSHostingView(
            rootView: CanvasContainerView(document: doc, workspace: state.workspace, tabID: tabID)
                .environmentObject(state))
        window.contentView = host
        window.makeKeyAndOrderFront(nil)
        await fulfillment(of: [announced], timeout: 5)
        host.layoutSubtreeIfNeeded()
        func findTable(in view: NSView) -> NSTableView? {
            if let table = view as? NSTableView { return table }
            for child in view.subviews {
                if let table = findTable(in: child) { return table }
            }
            return nil
        }
        let table = try XCTUnwrap(findTable(in: host), "the recovered table must be mounted")
        XCTAssertEqual(table.numberOfRows, 3)
        XCTAssertTrue(table.isEnabled, "read-only authoring must not disable card inspection")
        table.selectRowIndexes(IndexSet(integer: 1), byExtendingSelection: false)
        XCTAssertEqual(table.selectedRow, 1)
        XCTAssertEqual(state.canvasRecoveryActionLabel(for: doc), "Retry")
        XCTAssertTrue(state.canvasRecoveryActionHint(for: doc)?.contains("Repair") == true)
        XCTAssertEqual(
            posted.filter { $0.hasPrefix("Canvas opened read-only.") }.count, 1,
            "mounting announces the same readable status exactly once")
        XCTAssertNil(doc.takeLoadAnnouncement())
    }
}
