// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import SwiftUI
import XCTest

@testable import SlateMac

/// #852: file-tree multi-select + batch move / delete.
///
/// The core new logic is a PURE selection model — `applySelectionClick` folds a
/// plain / ⌘ / ⇧ pointer click into `(selection, anchor, focus)` over a
/// flattened visible-row order — and the batch orchestration on AppState
/// (`batchMove` / `requestBatchDelete` / `batchDelete`), which route every item
/// through the SAME per-item `moveEntry` / `deleteEntry` funnel and post ONE
/// summary announcement. Both are unit-tested directly here. The SwiftUI wiring
/// that can't be driven from XCTest (the tap dispatch, the open-suppression
/// `.onChange`, the batch context menu) is pinned by source inspection — the
/// repo's `…ByInspection` pattern, comments + strings stripped so the tokens
/// must appear as LIVE code.
@MainActor
final class FileTreeMultiSelectTests: XCTestCase {

    // MARK: - Fixtures

    private var tempDir: URL!

    override func setUpWithError() throws {
        tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("multiselect-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: tempDir, withIntermediateDirectories: true)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: tempDir)
    }

    /// A real vault + opened AppState (mirrors StructuralUndoTests).
    private func makeVault(named: String = "vault", files: [String]) async throws -> (AppState, URL) {
        let vault = tempDir.appendingPathComponent(named)
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        for rel in files {
            let url = vault.appendingPathComponent(rel)
            try FileManager.default.createDirectory(
                at: url.deletingLastPathComponent(), withIntermediateDirectories: true)
            try "# \((rel as NSString).lastPathComponent)\n".write(
                to: url, atomically: true, encoding: .utf8)
        }
        let state = AppState(
            recentsStore: RecentVaultsStore(
                fileURL: tempDir.appendingPathComponent("recents-\(named).json")),
            externalOpener: { _ in true })
        state.openVault(at: vault)
        await state.scanTask?.value
        return (state, vault)
    }

    private func exists(_ vault: URL, _ rel: String) -> Bool {
        FileManager.default.fileExists(atPath: vault.appendingPathComponent(rel).path)
    }

    private func sel(_ path: String, dir: Bool = false) -> AppState.TreeSelection {
        AppState.TreeSelection(path: path, isDirectory: dir)
    }

    private func fileRow(_ path: String) -> FileTreeSidebar.RowID { .node(.file(path: path)) }

    /// The order handed to `applySelectionClick` in most tests: four file rows.
    private var order: [FileTreeSidebar.RowID] {
        ["a.md", "b.md", "c.md", "d.md"].map(fileRow)
    }

    private func clickEvent(_ modifiers: NSEvent.ModifierFlags) -> NSEvent? {
        NSEvent.keyEvent(
            with: .keyDown, location: .zero, modifierFlags: modifiers, timestamp: 0,
            windowNumber: 0, context: nil, characters: "x",
            charactersIgnoringModifiers: "x", isARepeat: false, keyCode: 0)
    }

    private actor BatchRunnerProbe {
        private(set) var moveRequests: [BatchMoveRequest] = []
        private(set) var trashRequests: [BatchTrashRequest] = []
        let moveReport: BatchMoveReport
        let trashReport: BatchTrashReport

        init(moveReport: BatchMoveReport, trashReport: BatchTrashReport) {
            self.moveReport = moveReport
            self.trashReport = trashReport
        }

        func runMove(_ request: BatchMoveRequest) -> BatchMoveReport {
            moveRequests.append(request)
            return moveReport
        }

        func runTrash(_ request: BatchTrashRequest) -> BatchTrashReport {
            trashRequests.append(request)
            return trashReport
        }

        func callCounts() -> (move: Int, trash: Int) {
            (moveRequests.count, trashRequests.count)
        }

        func lastMoveRequest() -> BatchMoveRequest? { moveRequests.last }
        func lastTrashRequest() -> BatchTrashRequest? { trashRequests.last }
    }

    private actor BatchDeleteConfirmationProbe {
        private(set) var roots: [URL] = []
        private(set) var folderPathRequests: [[String]] = []
        let nonEmptyFolderCount: Int
        let gate: SuspensionGate?

        init(nonEmptyFolderCount: Int, gate: SuspensionGate? = nil) {
            self.nonEmptyFolderCount = nonEmptyFolderCount
            self.gate = gate
        }

        func run(vaultURL: URL, folderPaths: [String]) async -> Int {
            roots.append(vaultURL)
            folderPathRequests.append(folderPaths)
            if let gate { await gate.enter() }
            return nonEmptyFolderCount
        }

        func callCount() -> Int { roots.count }
        func lastRoot() -> URL? { roots.last }
        func lastFolderPaths() -> [String]? { folderPathRequests.last }
    }

    /// Deterministic async suspension for ownership/admission tests. Tests wait
    /// for an exact entrant count and release explicitly; no timing sleeps.
    private actor SuspensionGate {
        private var permits = 0
        private var waiters: [CheckedContinuation<Void, Never>] = []
        private var entrants = 0
        private var entrantWaiters: [(Int, CheckedContinuation<Void, Never>)] = []

        func enter() async {
            entrants += 1
            var remaining: [(Int, CheckedContinuation<Void, Never>)] = []
            for (expected, continuation) in entrantWaiters {
                if entrants >= expected {
                    continuation.resume()
                } else {
                    remaining.append((expected, continuation))
                }
            }
            entrantWaiters = remaining
            if permits > 0 {
                permits -= 1
                return
            }
            await withCheckedContinuation { continuation in
                waiters.append(continuation)
            }
        }

        func waitForEntrants(_ expected: Int) async {
            guard entrants < expected else { return }
            await withCheckedContinuation { continuation in
                entrantWaiters.append((expected, continuation))
            }
        }

        func releaseOne() {
            if waiters.isEmpty {
                permits += 1
            } else {
                waiters.removeFirst().resume()
            }
        }
    }

    @MainActor
    private final class RefreshProbe {
        var calls = 0
        func run(_ state: AppState) async { calls += 1 }
    }

    @MainActor
    private final class TaskBox {
        var task: Task<Void, Never>?
    }

    private enum BatchRunnerError: LocalizedError {
        case unavailable

        var errorDescription: String? { "batch endpoint unavailable" }
    }

    private func item(_ path: String, dir: Bool = false) -> StructuralBatchItem {
        StructuralBatchItem(path: path, isDirectory: dir)
    }

    private func envelope(
        planned: [StructuralBatchItem],
        skipped: [BatchSkippedItem] = [],
        preflightFailures: [BatchItemFailure] = []
    ) -> StructuralBatchEnvelope {
        StructuralBatchEnvelope(
            planned: planned,
            skipped: skipped,
            preflightFailures: preflightFailures)
    }

    private func moveReport(
        state: BatchMoveState,
        planned: [StructuralBatchItem],
        opID: Int64? = nil,
        standing: [BatchPathChange] = [],
        rolledBack: [BatchPathChange] = [],
        skipped: [BatchSkippedItem] = [],
        preflightFailures: [BatchItemFailure] = [],
        failure: BatchItemFailure? = nil,
        rollbackFailures: [BatchItemFailure] = [],
        rewritten: [RewriteOutcome] = [],
        rewriteFailures: [RewriteFailure] = [],
        requiresRescan: Bool = false
    ) -> BatchMoveReport {
        BatchMoveReport(
            envelope: envelope(
                planned: planned, skipped: skipped,
                preflightFailures: preflightFailures),
            state: state,
            opId: opID,
            standing: standing,
            rolledBack: rolledBack,
            failure: failure,
            rollbackFailures: rollbackFailures,
            rewritten: rewritten,
            rewriteFailures: rewriteFailures,
            requiresRescan: requiresRescan)
    }

    private func trashReport(
        state: BatchTrashState,
        planned: [StructuralBatchItem],
        opID: Int64? = nil,
        trashed: [StructuralBatchItem] = [],
        untrashed: [BatchTrashRemainder] = [],
        unknown: [BatchTrashRemainder] = [],
        skipped: [BatchSkippedItem] = [],
        preflightFailures: [BatchItemFailure] = [],
        bookkeepingFailures: [BatchItemFailure] = [],
        requiresRescan: Bool = false
    ) -> BatchTrashReport {
        BatchTrashReport(
            envelope: envelope(
                planned: planned, skipped: skipped,
                preflightFailures: preflightFailures),
            state: state,
            opId: opID,
            trashed: trashed,
            untrashed: untrashed,
            unknown: unknown,
            bookkeepingFailures: bookkeepingFailures,
            requiresRescan: requiresRescan)
    }

    // MARK: - Modifier decode (selectionClick)

    func testSelectionClickDecodesPlainToggleRange() {
        XCTAssertEqual(FileTreeSidebar.selectionClick(from: clickEvent([])), .plain)
        XCTAssertEqual(FileTreeSidebar.selectionClick(from: clickEvent([.command])), .toggle)
        XCTAssertEqual(FileTreeSidebar.selectionClick(from: clickEvent([.shift])), .range)
        XCTAssertEqual(FileTreeSidebar.selectionClick(from: nil), .plain)
    }

    /// ⇧ wins over ⌘ (a ⇧⌘-click range-selects) — the documented simplification.
    func testShiftBeatsCommandForRange() {
        XCTAssertEqual(
            FileTreeSidebar.selectionClick(from: clickEvent([.shift, .command])), .range)
    }

    /// Unrelated modifiers (Caps Lock, Option) don't turn a plain click into a
    /// multi gesture.
    func testStrayModifiersStayPlain() {
        XCTAssertEqual(FileTreeSidebar.selectionClick(from: clickEvent([.capsLock])), .plain)
        XCTAssertEqual(FileTreeSidebar.selectionClick(from: clickEvent([.option])), .plain)
    }

    // MARK: - Plain click

    func testPlainClickSelectsOnlyThisRowAndAnchors() {
        let out = FileTreeSidebar.applySelectionClick(
            order: order, current: [fileRow("a.md"), fileRow("c.md")],
            anchor: fileRow("a.md"), clicked: fileRow("b.md"), click: .plain)
        XCTAssertEqual(out.selection, [fileRow("b.md")], "plain click collapses to one row")
        XCTAssertEqual(out.anchor, fileRow("b.md"))
        XCTAssertEqual(out.focus, fileRow("b.md"))
    }

    // MARK: - ⌘ toggle

    func testCommandClickAddsRowAndRepivotsAnchor() {
        let out = FileTreeSidebar.applySelectionClick(
            order: order, current: [fileRow("a.md")], anchor: fileRow("a.md"),
            clicked: fileRow("c.md"), click: .toggle)
        XCTAssertEqual(out.selection, [fileRow("a.md"), fileRow("c.md")])
        XCTAssertEqual(out.anchor, fileRow("c.md"), "a ⌘-click re-pivots the range anchor")
        XCTAssertEqual(out.focus, fileRow("c.md"))
    }

    func testCommandClickRemovesRowAndFocusesLastRemaining() {
        let out = FileTreeSidebar.applySelectionClick(
            order: order, current: [fileRow("a.md"), fileRow("b.md"), fileRow("d.md")],
            anchor: fileRow("a.md"), clicked: fileRow("b.md"), click: .toggle)
        XCTAssertEqual(out.selection, [fileRow("a.md"), fileRow("d.md")], "toggled b out")
        XCTAssertEqual(out.anchor, fileRow("b.md"))
        XCTAssertEqual(
            out.focus, fileRow("d.md"),
            "focus moves to the LAST still-selected row in visible order")
    }

    func testCommandClickRemovingLastRowEmptiesSetAndNilsFocus() {
        let out = FileTreeSidebar.applySelectionClick(
            order: order, current: [fileRow("c.md")], anchor: fileRow("c.md"),
            clicked: fileRow("c.md"), click: .toggle)
        XCTAssertTrue(out.selection.isEmpty)
        XCTAssertNil(out.focus)
    }

    // MARK: - ⇧ range

    func testShiftRangeSpansAnchorToClickedInclusive() {
        let out = FileTreeSidebar.applySelectionClick(
            order: order, current: [fileRow("b.md")], anchor: fileRow("b.md"),
            clicked: fileRow("d.md"), click: .range)
        XCTAssertEqual(out.selection, [fileRow("b.md"), fileRow("c.md"), fileRow("d.md")])
        XCTAssertEqual(out.anchor, fileRow("b.md"), "the anchor is UNCHANGED by a ⇧-click")
        XCTAssertEqual(out.focus, fileRow("d.md"))
    }

    /// A ⇧-click ABOVE the anchor spans upward — the range is order-agnostic.
    func testShiftRangeReversedStillInclusive() {
        let out = FileTreeSidebar.applySelectionClick(
            order: order, current: [fileRow("d.md")], anchor: fileRow("d.md"),
            clicked: fileRow("a.md"), click: .range)
        XCTAssertEqual(out.selection, Set(order))
        XCTAssertEqual(out.anchor, fileRow("d.md"))
        XCTAssertEqual(out.focus, fileRow("a.md"))
    }

    /// Successive ⇧-clicks grow AND shrink from a FIXED pivot (the anchor is set
    /// by the earlier plain/⌘ click and never moves under ⇧).
    func testSuccessiveShiftClicksReanchorFromFixedPivot() {
        // Plain-click b establishes the pivot.
        let first = FileTreeSidebar.applySelectionClick(
            order: order, current: [], anchor: nil, clicked: fileRow("b.md"), click: .plain)
        XCTAssertEqual(first.anchor, fileRow("b.md"))
        // ⇧-click d → span b…d.
        let grown = FileTreeSidebar.applySelectionClick(
            order: order, current: first.selection, anchor: first.anchor,
            clicked: fileRow("d.md"), click: .range)
        XCTAssertEqual(grown.selection, [fileRow("b.md"), fileRow("c.md"), fileRow("d.md")])
        // ⇧-click c (shrink) → span b…c, STILL pivoting on b (not on the last click).
        let shrunk = FileTreeSidebar.applySelectionClick(
            order: order, current: grown.selection, anchor: grown.anchor,
            clicked: fileRow("c.md"), click: .range)
        XCTAssertEqual(shrunk.selection, [fileRow("b.md"), fileRow("c.md")])
        XCTAssertEqual(shrunk.anchor, fileRow("b.md"))
    }

    func testShiftRangeWithNoUsableAnchorDegradesToPlain() {
        // nil anchor.
        let noAnchor = FileTreeSidebar.applySelectionClick(
            order: order, current: [], anchor: nil, clicked: fileRow("c.md"), click: .range)
        XCTAssertEqual(noAnchor.selection, [fileRow("c.md")])
        XCTAssertEqual(noAnchor.anchor, fileRow("c.md"), "adopts clicked as the new pivot")
        // Off-list anchor (a row no longer visible).
        let stale = FileTreeSidebar.applySelectionClick(
            order: order, current: [], anchor: fileRow("gone.md"),
            clicked: fileRow("c.md"), click: .range)
        XCTAssertEqual(stale.selection, [fileRow("c.md")])
    }

    // MARK: - topLevelSelection dedup

    func testTopLevelSelectionDropsDescendantsOfSelectedFolders() {
        let items = [sel("folder", dir: true), sel("folder/x.md"), sel("other.md")]
        XCTAssertEqual(
            AppState.topLevelSelection(items).map(\.path), ["folder", "other.md"],
            "a folder AND a file inside it collapse to just the folder")
    }

    func testTopLevelSelectionKeepsIndependentItems() {
        let items = [sel("a.md"), sel("b.md"), sel("dir", dir: true)]
        XCTAssertEqual(AppState.topLevelSelection(items).map(\.path), ["a.md", "b.md", "dir"])
    }

    func testTopLevelSelectionCollapsesNestedFolders() {
        let items = [sel("a", dir: true), sel("a/b", dir: true), sel("a/b/c.md")]
        XCTAssertEqual(AppState.topLevelSelection(items).map(\.path), ["a"])
    }

    func testTopLevelSelectionPreservesOrder() {
        let items = [sel("z.md"), sel("a.md"), sel("m.md")]
        XCTAssertEqual(AppState.topLevelSelection(items).map(\.path), ["z.md", "a.md", "m.md"])
    }

    // MARK: - Announcement builders

    func testBatchMoveAnnouncementPhrasing() {
        XCTAssertEqual(
            AppState.batchMoveAnnouncement(count: 3, destination: "Archive"),
            "Moved 3 items to Archive.")
        XCTAssertEqual(
            AppState.batchMoveAnnouncement(count: 1, destination: "Archive"),
            "Moved 1 item to Archive.")
        XCTAssertEqual(
            AppState.batchMoveAnnouncement(count: 2, destination: ""),
            "Moved 2 items to vault root.")
        XCTAssertEqual(
            AppState.batchMoveAnnouncement(count: 2, destination: "a/b/Deep"),
            "Moved 2 items to Deep.", "the destination reads as its last component")
    }

    func testBatchDeleteAnnouncementPhrasing() {
        XCTAssertEqual(AppState.batchDeleteAnnouncement(count: 3), "Moved 3 items to Trash.")
        XCTAssertEqual(AppState.batchDeleteAnnouncement(count: 1), "Moved 1 item to Trash.")
    }

    func testBatchFailureStageLabelsAndFailureIdentityAreExhaustive() {
        let stages: [(BatchFailureStage, String)] = [
            (.preflight, "Preflight"),
            (.move, "Move"),
            (.index, "Index"),
            (.linkRewrite, "Link update"),
            (.linkRewriteRestore, "Link restoration"),
            (.journal, "History recording"),
            (.rollback, "Restoration"),
            (.trash, "Trash"),
            (.reconciliation, "Reconciliation"),
            (.recoveryBarrier, "Recovery safety"),
        ]

        for (stage, expected) in stages {
            XCTAssertEqual(
                AppState.BatchStructuralCopy.failureStageLabel(stage), expected)
        }
        XCTAssertEqual(
            AppState.BatchStructuralCopy.failureLine(
                BatchItemFailure(
                    item: nil, stage: .reconciliation,
                    message: "the request ledger disagreed")),
            "Request — Reconciliation: the request ledger disagreed")
        XCTAssertEqual(
            AppState.BatchStructuralCopy.failureLine(
                BatchItemFailure(
                    item: item("Folder/a.md"), stage: .move,
                    message: "the destination already exists")),
            "Folder/a.md — Move: the destination already exists")
    }

    func testBatchTrashCopyOwnsConfirmationAndNamesTheSystemBoundary() {
        XCTAssertEqual(
            AppState.BatchTrashCopy.confirmationTitle(itemCount: 2),
            "Move 2 items to Trash?")
        XCTAssertEqual(
            AppState.BatchTrashCopy.confirmationMessage(
                itemCount: 2, nonEmptyFolderCount: 1),
            "Move 2 items, including 1 folder with contents, to the system Trash. "
                + "Slate can't undo this action.")
        XCTAssertEqual(AppState.BatchTrashCopy.actionLabel, "Move to Trash")
        XCTAssertEqual(
            AppState.BatchTrashCopy.actionHint,
            "Move the selected items and folder contents to the system Trash. "
                + "Slate can't undo this action.")
        XCTAssertEqual(AppState.BatchTrashCopy.cancelLabel, "Cancel")
        XCTAssertEqual(
            AppState.BatchTrashCopy.cancelHint,
            "Keep the selected items. Nothing is moved to Trash.")
        XCTAssertEqual(
            AppState.BatchTrashCopy.singleFolderConfirmationTitle(name: "Project"),
            "Move “Project” to Trash?")
        XCTAssertEqual(
            AppState.BatchTrashCopy.singleFolderConfirmationMessage(
                name: "Project", itemCount: 2),
            "Move “Project” and its 2 items to the system Trash. "
                + "Slate can't undo this action.")
        XCTAssertEqual(
            AppState.BatchTrashCopy.singleFolderCancelHint(name: "Project"),
            "Keep “Project” and everything inside it. Nothing is moved to Trash.")
    }

    func testReturnIsInertForBatchTrashConfirmationAndCancelUsesCapturedID()
        async throws
    {
        let report = trashReport(
            state: .noOp,
            planned: [item("folder", dir: true), item("a.md")])
        let probe = BatchRunnerProbe(
            moveReport: moveReport(state: .noOp, planned: []),
            trashReport: report)
        let (state, _) = try await makeVault(files: ["folder/x.md", "a.md"])
        state.batchTrashRunner = { _, request in await probe.runTrash(request) }
        XCTAssertTrue(
            state.requestBatchDelete([
                sel("folder", dir: true), sel("a.md")
            ]))
        await state.pendingStructuralTaskForTesting?.value
        let pending = try XCTUnwrap(state.pendingBatchDelete)

        let handled = TrashConfirmationReturnKey.route(
            keyCode: 36,
            modifierFlags: [],
            owner: .batch(pending.id)
        ) { owner in
            guard case .batch(let id) = owner else {
                return XCTFail("the mounted batch owner must stay captured")
            }
            XCTAssertFalse(state.handlePendingBatchDeleteKey(.returnKey, id: id))
        }
        XCTAssertTrue(handled, "the live alert router consumes bare Return")
        XCTAssertEqual(state.pendingBatchDelete?.id, pending.id)
        var counts = await probe.callCounts()
        XCTAssertEqual(counts.trash, 0)

        XCTAssertTrue(state.handlePendingBatchDeleteKey(.cancel, id: pending.id))
        XCTAssertNil(state.pendingBatchDelete)
        counts = await probe.callCounts()
        XCTAssertEqual(counts.trash, 0)
    }

    func testBusyBatchTrashConfirmationRetainsTheCapturedRequest() async throws {
        let gate = SuspensionGate()
        let trashProbe = BatchRunnerProbe(
            moveReport: moveReport(state: .noOp, planned: []),
            trashReport: trashReport(
                state: .noOp,
                planned: [item("folder", dir: true), item("a.md")]))
        let (state, _) = try await makeVault(
            files: ["folder/x.md", "a.md", "busy.md", "dest/keep.md"])
        let busyReport = moveReport(
            state: .noOp, planned: [item("busy.md")])
        state.batchTrashRunner = { _, request in
            await trashProbe.runTrash(request)
        }
        state.batchMoveRunner = { _, _ in
            await gate.enter()
            return busyReport
        }
        state.structuralBatchRefreshRunner = { _ in }
        XCTAssertTrue(
            state.requestBatchDelete([
                sel("folder", dir: true), sel("a.md")
            ]))
        await state.pendingStructuralTaskForTesting?.value
        let pending = try XCTUnwrap(state.pendingBatchDelete)
        let busyTask = try XCTUnwrap(
            state.batchMove([sel("busy.md")], to: "dest", preferredFocusPath: nil))
        await gate.waitForEntrants(1)

        XCTAssertEqual(
            state.structuralMutationDisabledReason,
            "Wait for the current file operation to finish.")
        XCTAssertFalse(state.confirmPendingBatchDelete(id: pending.id))
        XCTAssertEqual(
            state.pendingBatchDelete?.id, pending.id,
            "failed admission keeps the confirmation available for retry or Cancel")
        let counts = await trashProbe.callCounts()
        XCTAssertEqual(counts.trash, 0)

        await gate.releaseOne()
        await busyTask.value
        state.cancelPendingBatchDelete(id: pending.id)
    }

    func testSingleFolderTrashConfirmationHasUUIDSessionAndBusyRetention() async throws {
        let gate = SuspensionGate()
        let (state, _) = try await makeVault(
            files: ["folder/x.md", "busy.md", "dest/keep.md"])
        let busyReport = moveReport(
            state: .noOp, planned: [item("busy.md")])
        state.batchMoveRunner = { _, _ in
            await gate.enter()
            return busyReport
        }
        state.structuralBatchRefreshRunner = { _ in }

        state.requestDeleteEntry(path: "folder", isDirectory: true)
        let stale = try XCTUnwrap(state.pendingFolderDelete)
        XCTAssertEqual(
            stale.sessionIdentity,
            state.currentSession.map(ObjectIdentifier.init))
        XCTAssertTrue(state.cancelPendingFolderDelete(id: stale.id))
        state.requestDeleteEntry(path: "folder", isDirectory: true)
        let current = try XCTUnwrap(state.pendingFolderDelete)
        XCTAssertNotEqual(current.id, stale.id, "re-staging the same path gets a fresh owner")
        XCTAssertFalse(state.confirmPendingFolderDelete(id: stale.id))
        XCTAssertEqual(state.pendingFolderDelete?.id, current.id, "a stale callback cannot clear B")

        let busyTask = try XCTUnwrap(
            state.batchMove([sel("busy.md")], to: "dest", preferredFocusPath: nil))
        await gate.waitForEntrants(1)
        XCTAssertFalse(state.confirmPendingFolderDelete(id: current.id))
        XCTAssertEqual(
            state.pendingFolderDelete?.id, current.id,
            "single-folder failed admission retains the exact captured request")

        await gate.releaseOne()
        await busyTask.value
        XCTAssertTrue(state.cancelPendingFolderDelete(id: current.id))
    }

    func testSinglePendingMoveIsUUIDSessionOwnedAndDiesAcrossVaultLifecycle()
        async throws
    {
        let (state, vaultA) = try await makeVault(
            named: "move-owner-a", files: ["a.md", "dest/keep.md"])
        XCTAssertTrue(state.requestPendingMove(path: "a.md", isDirectory: false))
        let a = try XCTUnwrap(state.pendingMove)
        XCTAssertEqual(
            a.sessionIdentity,
            state.currentSession.map(ObjectIdentifier.init))
        XCTAssertTrue(state.cancelPendingMove(id: a.id))

        XCTAssertTrue(state.requestPendingMove(path: "a.md", isDirectory: false))
        let b = try XCTUnwrap(state.pendingMove)
        XCTAssertNotEqual(a.id, b.id)
        XCTAssertFalse(state.cancelPendingMove(id: a.id))
        XCTAssertFalse(state.commitPendingMove(id: a.id, to: "dest"))
        XCTAssertEqual(state.pendingMove?.id, b.id, "stale A cannot clear or commit B")
        XCTAssertTrue(exists(vaultA, "a.md"))

        let vaultB = tempDir.appendingPathComponent("move-owner-b")
        try FileManager.default.createDirectory(
            at: vaultB.appendingPathComponent("dest"),
            withIntermediateDirectories: true)
        try "# B\n".write(
            to: vaultB.appendingPathComponent("a.md"),
            atomically: true,
            encoding: .utf8)
        state.openVault(at: vaultB)
        await state.scanTask?.value
        XCTAssertNil(state.pendingMove, "direct open clears the old sheet capture")
        XCTAssertFalse(state.commitPendingMove(id: b.id, to: "dest"))
        XCTAssertTrue(exists(vaultB, "a.md"), "stale vault-A commit writes nothing in B")
        XCTAssertFalse(exists(vaultB, "dest/a.md"))

        XCTAssertTrue(state.requestPendingMove(path: "a.md", isDirectory: false))
        let c = try XCTUnwrap(state.pendingMove)
        XCTAssertNotEqual(c.id, b.id)
        state.closeVault()
        XCTAssertNil(state.pendingMove, "close/reset clears the captured sheet")
        XCTAssertFalse(state.cancelPendingMove(id: c.id))
    }

    func testBusySingleMoveExistingDestinationRetainsThenRetriesExactlyOnce()
        async throws
    {
        let gate = SuspensionGate()
        let busyReport = moveReport(
            state: .noOp, planned: [item("busy.md")])
        let (state, vault) = try await makeVault(
            named: "single-existing-busy",
            files: ["a.md", "busy.md", "dest/keep.md"])
        state.batchMoveRunner = { _, _ in
            await gate.enter()
            return busyReport
        }
        state.structuralBatchRefreshRunner = { _ in }
        XCTAssertTrue(state.requestPendingMove(path: "a.md", isDirectory: false))
        let pending = try XCTUnwrap(state.pendingMove)
        let busyTask = try XCTUnwrap(
            state.batchMove([sel("busy.md")], to: "dest", preferredFocusPath: nil))
        await gate.waitForEntrants(1)

        XCTAssertFalse(state.commitPendingMove(id: pending.id, to: "dest"))
        XCTAssertEqual(state.pendingMove?.id, pending.id)
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            "Wait for the current file operation to finish.")
        XCTAssertTrue(exists(vault, "a.md"))
        XCTAssertFalse(exists(vault, "dest/a.md"))

        await gate.releaseOne()
        await busyTask.value
        XCTAssertTrue(state.commitPendingMove(id: pending.id, to: "dest"))
        XCTAssertNil(state.pendingMove, "only synchronous admission dismisses the sheet")
        await state.pendingStructuralTaskForTesting?.value
        XCTAssertFalse(exists(vault, "a.md"))
        XCTAssertTrue(exists(vault, "dest/a.md"))
    }

    func testBusyRenameStageAndCommitRejectWithSharedReasonAndNoWrites()
        async throws
    {
        let gate = SuspensionGate()
        let busyReport = moveReport(
            state: .noOp, planned: [item("busy.md")])
        let (state, vault) = try await makeVault(
            named: "rename-busy",
            files: ["a.md", "busy.md", "dest/keep.md"])
        state.batchMoveRunner = { _, _ in
            await gate.enter()
            return busyReport
        }
        state.structuralBatchRefreshRunner = { _ in }

        XCTAssertTrue(state.requestRename(path: "a.md", isDirectory: false))
        let pending = try XCTUnwrap(state.renamingNode)
        let busyTask = try XCTUnwrap(
            state.batchMove([sel("busy.md")], to: "dest", preferredFocusPath: nil))
        await gate.waitForEntrants(1)

        XCTAssertFalse(state.commitPendingRename(id: pending.id, to: "alpha.md"))
        XCTAssertEqual(
            state.renamingNode?.id, pending.id,
            "busy admission keeps the captured field available for retry or Cancel")
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            "Wait for the current file operation to finish.")
        XCTAssertTrue(exists(vault, "a.md"))
        XCTAssertFalse(exists(vault, "alpha.md"))

        XCTAssertTrue(state.cancelPendingRename(id: pending.id))
        XCTAssertFalse(state.requestRename(path: "a.md", isDirectory: false))
        XCTAssertNil(state.renamingNode, "busy structural work cannot open inline rename")
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            "Wait for the current file operation to finish.")

        await gate.releaseOne()
        await busyTask.value
    }

    func testBusyNewFolderInContextCreatesNothingStagesNothingAndAnnounces()
        async throws
    {
        let gate = SuspensionGate()
        let busyReport = moveReport(
            state: .noOp, planned: [item("busy.md")])
        let (state, vault) = try await makeVault(
            named: "new-folder-busy",
            files: ["busy.md", "dest/keep.md"])
        state.batchMoveRunner = { _, _ in
            await gate.enter()
            return busyReport
        }
        state.structuralBatchRefreshRunner = { _ in }
        let busyTask = try XCTUnwrap(
            state.batchMove([sel("busy.md")], to: "dest", preferredFocusPath: nil))
        await gate.waitForEntrants(1)

        state.newFolderInContext(parent: "")
        // A synchronous busy rejection retains the already-running batch task;
        // awaiting the shared test handle here would intentionally await the
        // gate we have not released yet. Yield instead so the retired bogus
        // outer Task would have time to stage its nonexistent folder rename.
        for _ in 0..<3 { await Task.yield() }

        XCTAssertFalse(exists(vault, "Untitled Folder"))
        XCTAssertNil(state.renamingNode)
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            "Wait for the current file operation to finish.")

        await gate.releaseOne()
        await busyTask.value
    }

    // MARK: - C1 shared non-drag admission + VoiceOver origin parity

    func testC1BusyNewNoteAndDuplicateCommandsRejectSynchronouslyWithExactReason()
        async throws
    {
        let gate = SuspensionGate()
        let busyReport = moveReport(
            state: .noOp, planned: [item("busy.md")])
        let (state, vault) = try await makeVault(
            named: "c1-command-busy",
            files: ["a.md", "busy.md", "dest/keep.md"])
        state.batchMoveRunner = { _, _ in
            await gate.enter()
            return busyReport
        }
        state.structuralBatchRefreshRunner = { _ in }
        state.treeSelectedNode = AppState.TreeSelection(
            path: "a.md", isDirectory: false)

        let busyTask = try XCTUnwrap(
            state.batchMove([sel("busy.md")], to: "dest", preferredFocusPath: nil))
        await gate.waitForEntrants(1)

        state.newNoteCommand()
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            "Wait for the current file operation to finish.")
        XCTAssertFalse(exists(vault, "Untitled.md"), "busy New Note stages no write")
        XCTAssertNil(state.renamingNode, "busy New Note stages no inline field")

        state.duplicateSelectedCommand()
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            "Wait for the current file operation to finish.")
        XCTAssertFalse(exists(vault, "a copy.md"), "busy Duplicate stages no write")

        await gate.releaseOne()
        await busyTask.value
    }

    func testC1RegistryRejectsEveryBusyStructuralCommandWithExactActionFailed()
        async throws
    {
        let gate = SuspensionGate()
        let busyReport = moveReport(
            state: .noOp, planned: [item("busy.md")])
        let (state, _) = try await makeVault(
            named: "c1-registry-busy",
            files: ["a.md", "busy.md", "dest/keep.md", "Templates/base.md"])
        await state.templateAvailabilityTask?.value
        state.batchMoveRunner = { _, _ in
            await gate.enter()
            return busyReport
        }
        state.structuralBatchRefreshRunner = { _ in }
        let session = try XCTUnwrap(state.currentSession)
        let snapshot = SidebarSelectionSnapshot(
            sessionIdentity: ObjectIdentifier(session),
            items: [
                SidebarSelectionItem(
                    path: "a.md", isDirectory: false, isMarkdown: true)
            ],
            focusedPath: "a.md",
            creationParent: "")
        XCTAssertTrue(state.publishSidebarSelectionSnapshot(snapshot))

        let structuralCommandIDs = [
            SlateCommandID.newNote,
            SlateCommandID.newFolder,
            SlateCommandID.newFromTemplate,
            SlateCommandID.renameEntry,
            SlateCommandID.moveTo,
            SlateCommandID.deleteEntry,
            SlateCommandID.duplicateEntry,
        ]
        for id in structuralCommandIDs {
            let evaluation = try XCTUnwrap(
                state.sidebarActionProjection(surface: .commandPalette)
                    .first(where: { $0.id == id }))
            XCTAssertNil(
                evaluation.disabledReason,
                "\(id) must pass capability admission before the busy reason is introduced")
            XCTAssertNotNil(evaluation.intent)
        }

        let busyTask = try XCTUnwrap(
            state.batchMove([sel("busy.md")], to: "dest", preferredFocusPath: nil))
        await gate.waitForEntrants(1)

        for id in structuralCommandIDs {
            XCTAssertThrowsError(try state.commandRegistry.invokeById(id: id), id) { error in
                guard case let CommandError.ActionFailed(message) = error else {
                    return XCTFail("\(id) returned \(error), not ActionFailed")
                }
                XCTAssertEqual(
                    message, "Wait for the current file operation to finish.", id)
            }
        }

        XCTAssertNil(state.pendingMove, "busy registry Move stages no sheet")
        XCTAssertNil(state.pendingFolderDelete, "busy registry Trash stages no alert")
        XCTAssertNil(state.renamingNode, "busy registry Rename stages no field")

        await gate.releaseOne()
        await busyTask.value
    }

    func testC1NonDragSurfacesUseSharedAdmissionAndExposeBusyReasonByInspection()
        throws
    {
        let appState = try Self.rawSource("AppState.swift")
        let sidebar = try Self.rawSource("FileTreeSidebar.swift")
        let menu = try Self.rawSource("SlateMacApp.swift")
        let commands = try Self.rawSource("SlateCommands.swift")
        let palette = try Self.rawSource("CommandPaletteView.swift")
        let compactMenu = menu.filter { !$0.isWhitespace }

        XCTAssertTrue(
            appState.contains("func sidebarActionProjection("),
            "AppState must own the one live admission projection")
        XCTAssertTrue(
            appState.contains("func dispatchSidebarAction("),
            "all Sidebar surfaces must converge on one frozen-intent dispatcher")
        XCTAssertTrue(
            sidebar.contains("static func sidebarRowActionProjection("),
            "context menus and VoiceOver need one row-aware snapshot capture")
        XCTAssertTrue(
            sidebar.contains("private func sidebarCatalogActions("))
        XCTAssertTrue(sidebar.contains("appState.dispatchSidebarAction(intent)"))
        XCTAssertFalse(sidebar.contains("fileManagementMenu("))
        XCTAssertFalse(sidebar.contains("batchManagementMenu("))

        XCTAssertTrue(
            compactMenu.contains("appState.sidebarActionProjection(surface:.menuBar)"),
            "File menu must render the shared ordered projection")
        XCTAssertTrue(compactMenu.contains(".disabled(evaluation.disabledReason!=nil)"))
        XCTAssertTrue(
            compactMenu.contains(
                ".accessibilityHint(evaluation.disabledReason??evaluation.definition.accessibilityHint)"))
        XCTAssertTrue(
            compactMenu.contains(
                ".help(evaluation.disabledReason??evaluation.definition.accessibilityHint)"))
        XCTAssertTrue(
            commands.contains("registerSidebarCommands(into: registry)"),
            "the registry must register the catalog once")
        XCTAssertTrue(commands.contains("appState.dispatchSidebarAction(id: id)"))
        XCTAssertTrue(
            palette.contains("appState.sidebarActionProjection(surface: .commandPalette)"),
            "palette availability must come from the same projection")
        XCTAssertTrue(palette.contains("return evaluation.disabledReason"))
        XCTAssertTrue(palette.contains(".disabled(disabledReason != nil)"))
        XCTAssertTrue(palette.contains(".accessibilityHint(disabledReason"))
    }

    func testC1BusySidebarOmitsStructuralRotorsAndShowsScopedProgressByInspection()
        throws
    {
        let source = try Self.rawSource("FileTreeSidebar.swift")
        let splitSource = try Self.rawSource("MainSplitView.swift")
        let normalizedSource = try Self.normalizedSidebarSource()

        func body(_ source: String, from start: String, to end: String) throws -> String {
            let startRange = try XCTUnwrap(source.range(of: start), start)
            let tail = source[startRange.lowerBound...]
            let endRange = try XCTUnwrap(tail.range(of: end), end)
            return String(tail[..<endRange.lowerBound])
        }

        func compact(_ value: String) -> String {
            value.split(whereSeparator: { $0.isWhitespace }).joined(separator: " ")
        }

        let folder = compact(
            try body(source, from: "private func folderRow(", to: "private func fileRow("))
        let file = compact(
            try body(source, from: "private func fileRow(", to: "// MARK: - Inline rename"))
        let normalizedFolder = try body(
            normalizedSource,
            from: "private func folderRow(_ node: TreeNode)",
            to: "private func fileRow(_ node: TreeNode)")
        let normalizedFile = try body(
            normalizedSource,
            from: "private func fileRow(_ node: TreeNode)",
            to: "private func renameOwner(for node: TreeNode)")
        XCTAssertTrue(
            normalizedFolder.contains(
                ".accessibilityActions { if let publishedSnapshot = appState.sidebarSelectionSnapshot"),
            "the folder rotor must capture the live selection when it renders")
        XCTAssertTrue(
            normalizedFolder.contains("Self.sidebarRowActionProjection( surface: .voiceOver"))
        XCTAssertTrue(
            normalizedFolder.contains("actionDisabledReasons: sidebarRowActionDisabledReasons"))
        XCTAssertTrue(
            normalizedFolder.contains("sidebarCatalogActions(projection.evaluations)"))

        XCTAssertTrue(
            normalizedFile.contains(
                "let voiceOverProjection = appState.sidebarSelectionSnapshot.map { Self.sidebarRowActionProjection( surface: .voiceOver"),
            "the file row must retain one Open-aware projection for its role, hint, default action, and rotor")
        XCTAssertTrue(
            normalizedFile.contains("actionDisabledReasons: sidebarRowActionDisabledReasons"))
        XCTAssertTrue(
            normalizedFile.contains(
                ".accessibilityActions { if let voiceOverProjection { sidebarCatalogActions(voiceOverProjection.evaluations)"),
            "the file rotor must consume the same retained projection")

        for row in [normalizedFolder, normalizedFile] {
            XCTAssertFalse(
                row.contains("if isInMultiSelection(node)"),
                "the catalog, not a view-local busy/single/batch branch, owns omission")
            XCTAssertFalse(row.contains("appState.requestBatchMove("))
            XCTAssertFalse(row.contains("appState.requestPendingMove("))
            XCTAssertFalse(row.contains("appState.requestBatchDelete("))
        }
        XCTAssertTrue(
            folder.contains("Self.rowAccessibilityHint("),
            "busy folders must compose disclosure with the scoped reason")
        XCTAssertTrue(
            file.contains("Self.fileRowOpenAccessibilityPresentation("),
            "file-row hint and role must follow the retained Open evaluation")
        XCTAssertTrue(file.contains(".accessibilityHint(openPresentation.hint)"))
        for row in [folder, file] {
            XCTAssertTrue(
                row.contains(".help(disabledReason ?? node.path)"),
                "busy rows expose the exact shared reason as pointer help")
        }
        XCTAssertTrue(
            source.contains(
                "return \"\\(primaryAction) File changes are unavailable. \\(reason)\""),
            "the scoped busy suffix must append the exact shared reason once")
        XCTAssertTrue(
            folder.contains(
                "primaryAction: \"Expands or collapses.\""),
            "busy folder help must retain its enabled disclosure action")
        XCTAssertTrue(
            file.contains(
                "Self.fileRowAvailableOpenHint("),
            "busy file help must retain the count-aware enabled Open action")
        XCTAssertTrue(
            source.contains("let primaryAction = targetCount == 1")
                && source.contains("\"Opens the note.\"")
                && source.contains("\"Opens the selected files.\""),
            "the shared busy hint must describe the frozen single or batch target")
        XCTAssertTrue(
            folder.contains(
                ".accessibilityAction(named: Text(isExpanded ? \"Collapse\" : \"Expand\"))"),
            "folder disclosure remains available while structural mutation is busy")

        let sidebarBody = compact(
            try body(source, from: "var body: some View {", to: ".navigationTitle(\"Files\")"))
        XCTAssertTrue(
            sidebarBody.contains("progressBar structuralMutationProgress"),
            "ordinary structural progress remains immediately below scan progress")

        let sidebarProgress = compact(
            try body(
                source,
                from: "private var structuralMutationProgress",
                to: "private var batchTrashQuarantineRecovery"))
        XCTAssertTrue(sidebarProgress.contains("!appState.isValidatingSidebarAction"))
        XCTAssertTrue(sidebarProgress.contains("appState.structuralMutationDisabledReason"))

        let splitCore = compact(
            try body(
                splitSource,
                from: "private var splitViewCore: some View",
                to: "private var windowStructuralStatusSurface"))
        XCTAssertTrue(
            splitCore.contains(".safeAreaInset(edge: .top, spacing: 0) { windowStructuralStatusSurface }"),
            "selection-validation progress belongs to the always-mounted window shell")

        let progress = compact(
            try body(
                splitSource,
                from: "private var sidebarActionBackgroundProgress",
                to: "private var sidebarActionBackgroundFailure"))
        XCTAssertTrue(progress.contains("appState.sidebarActionBackgroundProgressReason"))
        XCTAssertTrue(progress.contains("ProgressView()"))
        XCTAssertTrue(progress.contains(".controlSize(.small)"))
        XCTAssertTrue(progress.contains(".accessibilityLabel(reason)"))
        XCTAssertTrue(progress.contains(".help(reason)"))
        XCTAssertFalse(
            progress.contains("postAccessibilityAnnouncement"),
            "visual mutation progress must not duplicate AppState's assertive announcement")
    }

    func testC1VoiceOverMovePreservesSingleAndBatchOriginByInspection() throws {
        let owner = NSObject()
        let first = SidebarSelectionItem(
            path: "a.md", isDirectory: false, isMarkdown: true)
        let folder = SidebarSelectionItem(
            path: "folder", isDirectory: true, isMarkdown: false)
        let published = SidebarSelectionSnapshot(
            sessionIdentity: ObjectIdentifier(owner),
            items: [first, folder],
            focusedPath: folder.path,
            creationParent: folder.path)

        let batch = FileTreeSidebar.sidebarRowActionProjection(
            surface: .voiceOver,
            row: first,
            publishedSnapshot: published,
            structuralMutationDisabledReason: nil,
            actionDisabledReasons: [:])
        XCTAssertEqual(batch.targetSnapshot, published)
        XCTAssertEqual(
            batch.evaluations.map(\.id),
            [SlateCommandID.moveTo, SlateCommandID.deleteEntry],
            "a row inside a mixed selection retains only the batch-capable catalog actions")
        XCTAssertEqual(
            batch.evaluations.first(where: { $0.id == SlateCommandID.moveTo })?
                .intent?.snapshot,
            published,
            "VoiceOver Move must freeze the complete visible-order batch and its focus origin")

        let outside = SidebarSelectionItem(
            path: "nested/outside.md", isDirectory: false, isMarkdown: true)
        let single = FileTreeSidebar.sidebarRowActionProjection(
            surface: .voiceOver,
            row: outside,
            publishedSnapshot: published,
            structuralMutationDisabledReason: nil,
            actionDisabledReasons: [:])
        XCTAssertEqual(single.targetSnapshot.items, [outside])
        XCTAssertEqual(single.targetSnapshot.focusedPath, outside.path)
        XCTAssertEqual(single.targetSnapshot.creationParent, "nested")
        XCTAssertEqual(
            single.evaluations.first(where: { $0.id == SlateCommandID.moveTo })?
                .intent?.snapshot,
            single.targetSnapshot,
            "VoiceOver on an outside row must freeze that row rather than the unrelated batch")

        let source = try Self.normalizedSidebarSource()

        func body(from start: String, to end: String) throws -> String {
            let startRange = try XCTUnwrap(source.range(of: start), start)
            let tail = source[startRange.lowerBound...]
            let endRange = try XCTUnwrap(tail.range(of: end), end)
            return String(tail[..<endRange.lowerBound])
        }

        func assertVoiceOverOwner(
            _ rowBody: String, file: StaticString = #filePath, line: UInt = #line
        ) {
            XCTAssertTrue(
                rowBody.contains(".accessibilityActions"),
                "management actions need a VoiceOver catalog owner",
                file: file, line: line)
            XCTAssertTrue(
                rowBody.contains("Self.sidebarRowActionProjection( surface: .voiceOver"),
                "the shared row projection must choose the frozen single or batch target",
                file: file, line: line)
            XCTAssertTrue(
                rowBody.contains("row: sidebarSelectionItem(for: node)"),
                "the acted-on row must be supplied explicitly",
                file: file, line: line)
            XCTAssertTrue(
                rowBody.contains("publishedSnapshot: publishedSnapshot"),
                "the semantic selection snapshot must be the only batch source",
                file: file, line: line)
            XCTAssertTrue(
                rowBody.contains("sidebarCatalogActions(projection.evaluations)"),
                "the shared renderer must own all emitted management actions",
                file: file, line: line)
            XCTAssertFalse(rowBody.contains("appState.requestBatchMove("), file: file, line: line)
            XCTAssertFalse(rowBody.contains("appState.requestPendingMove("), file: file, line: line)
            XCTAssertFalse(rowBody.contains("beginRename(node)"), file: file, line: line)
        }

        let folderOwner = try body(
            from: "private func folderRow(_ node: TreeNode)",
            to: "private func fileRow(_ node: TreeNode)")
        let fileOwner = try body(
            from: "private func fileRow(_ node: TreeNode)",
            to: "private func renameOwner(for node: TreeNode)")
        assertVoiceOverOwner(folderOwner)
        assertVoiceOverOwner(fileOwner)
    }

    func testStaleAsyncRenameCompletionCannotClearNewVaultRenameAtSamePath()
        async throws
    {
        let gate = SuspensionGate()
        let (state, _) = try await makeVault(
            named: "rename-owner-a", files: ["a.md"])
        state.structuralRenameRunner = { _, _, _, _ in
            await gate.enter()
            return StructuralReport(opId: 71, moved: [], rewritten: [], failed: [])
        }
        XCTAssertTrue(state.requestRename(path: "a.md", isDirectory: false))
        let a = try XCTUnwrap(state.renamingNode)
        XCTAssertTrue(state.commitPendingRename(id: a.id, to: "alpha.md"))
        let staleTask = try XCTUnwrap(state.pendingStructuralTaskForTesting)
        await gate.waitForEntrants(1)

        let vaultB = tempDir.appendingPathComponent("rename-owner-b")
        try FileManager.default.createDirectory(
            at: vaultB, withIntermediateDirectories: true)
        try "# B\n".write(
            to: vaultB.appendingPathComponent("a.md"), atomically: true,
            encoding: .utf8)
        state.openVault(at: vaultB)
        await state.scanTask?.value
        XCTAssertTrue(state.requestRename(path: "a.md", isDirectory: false))
        let b = try XCTUnwrap(state.renamingNode)
        XCTAssertNotEqual(a.id, b.id)

        await gate.releaseOne()
        await staleTask.value

        XCTAssertEqual(
            state.renamingNode?.id, b.id,
            "the old vault's async completion cannot clear or replace B")
        XCTAssertTrue(exists(vaultB, "a.md"))
        XCTAssertFalse(exists(vaultB, "alpha.md"))
    }

    func testRenameSurfacesAndCreatedFlowsUseOwnedResolverByInspection() throws {
        let appState = try Self.normalizedSource("AppState.swift")
        let sidebar = try Self.normalizedSource("FileTreeSidebar.swift")
        let graph = try Self.normalizedSource("Graph/AppState+Connections.swift")

        func body(_ source: String, from start: String, to end: String) throws -> String {
            let startRange = try XCTUnwrap(source.range(of: start))
            let tail = source[startRange.lowerBound...]
            let endRange = try XCTUnwrap(tail.range(of: end))
            return String(tail[..<endRange.lowerBound])
        }

        XCTAssertEqual(
            sidebar.components(separatedBy: "beginRename(node)").count - 1,
            0,
            "F2, VoiceOver, and context menus must not bypass the shared catalog dispatcher")
        XCTAssertEqual(
            sidebar.components(
                separatedBy:
                    "appState.requestRename(path: node.path, isDirectory: node.isDirectory)"
            ).count - 1,
            1,
            "the retained compatibility helper stages only through AppState")
        let f2Owner = try body(
            sidebar,
            from: ".onKeyPress(keys: [Self.f2Key])",
            to: ".onKeyPress(characters: Self.typeSelectCharacters")
        XCTAssertTrue(f2Owner.contains("appState.sidebarActionProjection(surface: .keyboard)"))
        XCTAssertTrue(f2Owner.contains("id: SlateCommandID.renameEntry"))
        XCTAssertTrue(f2Owner.contains("appState.dispatchSidebarAction(intent)"))
        XCTAssertTrue(
            sidebar.contains("Self.sidebarRowActionProjection( surface: .voiceOver"),
            "VoiceOver Rename availability comes from the same capability catalog")
        XCTAssertTrue(
            sidebar.contains("Self.sidebarRowActionProjection( surface: .contextMenu"),
            "context-menu Rename availability comes from the same capability catalog")
        XCTAssertEqual(
            sidebar.components(
                separatedBy: "sidebarCatalogActions(projection.evaluations)"
            ).count - 1,
            3,
            "both context menus and the folder rotor render through the shared owner")
        XCTAssertEqual(
            sidebar.components(
                separatedBy: "sidebarCatalogActions(voiceOverProjection.evaluations)"
            ).count - 1,
            1,
            "the file rotor reuses its retained Open-aware projection")
        XCTAssertFalse(
            sidebar.contains("appState.renamingNode ="),
            "the view cannot create, clear, or replace rename ownership directly")
        XCTAssertTrue(
            sidebar.contains("cancelPendingRename(id: rename.id)"),
            "the rendered field's Cancel closure owns its captured UUID")
        XCTAssertTrue(
            sidebar.contains("commitPendingRename(id: rename.id"),
            "the rendered field's Return closure owns its captured UUID")

        let selectedCommand = try body(
            appState, from: "func renameSelectedCommand()", to: "func moveSelectedCommand()")
        XCTAssertTrue(
            selectedCommand.contains(
                "requestRename(path: node.path, isDirectory: node.isDirectory)"))
        XCTAssertFalse(selectedCommand.contains("renamingNode ="))

        let newFolder = try body(
            appState, from: "func newFolderInContext(parent: String)",
            to: "func renameSelectedCommand()")
        XCTAssertTrue(newFolder.contains("onResult:"))
        XCTAssertTrue(newFolder.contains("guard created"))
        XCTAssertTrue(newFolder.contains("installRenameForCreatedEntry("))
        XCTAssertFalse(newFolder.contains("lastError"))
        XCTAssertFalse(
            newFolder.contains("Task {"),
            "New Folder retains the admitted create task instead of fabricating an outer task")

        let createNote = try body(
            appState, from: "func createNote(in parent: String)",
            to: "func duplicateEntry(path: String)")
        XCTAssertTrue(createNote.contains("installRenameForCreatedEntry("))
        XCTAssertTrue(graph.contains("installRenameForCreatedEntry("))
        XCTAssertFalse(graph.contains("renamingNode ="))
        XCTAssertEqual(
            appState.components(separatedBy: "renamingNode = RenamingNode(").count - 1,
            1,
            "all stage sources converge on one current-session-owned installer")
    }

    func testBusySingleMoveNewFolderCreatesNothingThenRetriesExactlyOnce()
        async throws
    {
        let gate = SuspensionGate()
        let busyReport = moveReport(
            state: .noOp, planned: [item("busy.md")])
        let (state, vault) = try await makeVault(
            named: "single-new-busy",
            files: ["a.md", "busy.md", "dest/keep.md"])
        state.batchMoveRunner = { _, _ in
            await gate.enter()
            return busyReport
        }
        state.structuralBatchRefreshRunner = { _ in }
        XCTAssertTrue(state.requestPendingMove(path: "a.md", isDirectory: false))
        let pending = try XCTUnwrap(state.pendingMove)
        let busyTask = try XCTUnwrap(
            state.batchMove([sel("busy.md")], to: "dest", preferredFocusPath: nil))
        await gate.waitForEntrants(1)

        XCTAssertFalse(
            state.commitPendingMoveToNewFolder(
                id: pending.id, newFolderName: "New Folder", in: ""))
        XCTAssertEqual(state.pendingMove?.id, pending.id)
        XCTAssertFalse(exists(vault, "New Folder"))
        XCTAssertTrue(exists(vault, "a.md"))

        await gate.releaseOne()
        await busyTask.value
        XCTAssertTrue(
            state.commitPendingMoveToNewFolder(
                id: pending.id, newFolderName: "New Folder", in: ""))
        XCTAssertNil(state.pendingMove)
        await state.pendingStructuralTaskForTesting?.value
        XCTAssertTrue(exists(vault, "New Folder/a.md"))
        XCTAssertFalse(exists(vault, "a.md"))
        XCTAssertFalse(exists(vault, "New Folder 2"))
    }

    func testBusyBatchMoveNewFolderCreatesNothingThenRetriesOneBatchCall()
        async throws
    {
        let gate = SuspensionGate()
        let busyReport = moveReport(
            state: .noOp, planned: [item("busy.md")])
        let planned = [item("a.md"), item("b.md")]
        let retryProbe = BatchRunnerProbe(
            moveReport: moveReport(state: .noOp, planned: planned),
            trashReport: trashReport(state: .noOp, planned: []))
        let (state, vault) = try await makeVault(
            named: "batch-new-busy",
            files: ["a.md", "b.md", "busy.md", "dest/keep.md"])
        state.batchMoveRunner = { _, _ in
            await gate.enter()
            return busyReport
        }
        state.structuralBatchRefreshRunner = { _ in }
        XCTAssertTrue(
            state.requestBatchMove(
                [sel("a.md"), sel("b.md")],
                preferredFocusPath: "b.md"))
        let pending = try XCTUnwrap(state.pendingBatchMove)
        let busyTask = try XCTUnwrap(
            state.batchMove([sel("busy.md")], to: "dest", preferredFocusPath: nil))
        await gate.waitForEntrants(1)

        XCTAssertFalse(
            state.commitPendingBatchMoveToNewFolder(
                id: pending.id, newFolderName: "New Folder", in: ""))
        XCTAssertEqual(state.pendingBatchMove?.id, pending.id)
        XCTAssertFalse(exists(vault, "New Folder"))
        let rejectedCounts = await retryProbe.callCounts()
        XCTAssertEqual(rejectedCounts.move, 0)

        await gate.releaseOne()
        await busyTask.value
        state.batchMoveRunner = { _, request in
            await retryProbe.runMove(request)
        }
        XCTAssertTrue(
            state.commitPendingBatchMoveToNewFolder(
                id: pending.id, newFolderName: "New Folder", in: ""))
        XCTAssertNil(state.pendingBatchMove)
        await state.pendingStructuralTaskForTesting?.value
        let counts = await retryProbe.callCounts()
        XCTAssertEqual(counts.move, 1)
        XCTAssertTrue(exists(vault, "New Folder"))
        XCTAssertFalse(exists(vault, "New Folder 2"))
    }

    func testSingleNewFolderPhaseTwoContentionTruthfullyLeavesItemUnmoved()
        async throws
    {
        let gate = SuspensionGate()
        let (state, vault) = try await makeVault(
            named: "single-new-phase-two-contention",
            files: ["a.md", "busy.md", "dest/keep.md"])
        let competitorReport = moveReport(
            state: .noOp, planned: [item("busy.md")])
        state.batchMoveRunner = { _, _ in
            await gate.enter()
            return competitorReport
        }
        state.structuralBatchRefreshRunner = { _ in }
        let competitor = TaskBox()
        state.createFolderMoveContinuationGateForTesting = {
            state.createFolderMoveContinuationGateForTesting = nil
            competitor.task = state.batchMove(
                [self.sel("busy.md")], to: "dest", preferredFocusPath: nil)
        }

        let compound = try XCTUnwrap(
            state.createFolderThenMove(
                newFolderName: "New Folder", in: "", movePath: "a.md",
                isDirectory: false))
        await compound.value
        await gate.waitForEntrants(1)

        XCTAssertTrue(exists(vault, "New Folder"))
        XCTAssertTrue(exists(vault, "a.md"))
        XCTAssertFalse(exists(vault, "New Folder/a.md"))
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            "\(AppState.structuralMutationBusyReason) a.md was not moved.")

        await gate.releaseOne()
        await competitor.task?.value
        XCTAssertTrue(exists(vault, "a.md"), "phase two never retries implicitly")
        XCTAssertFalse(exists(vault, "New Folder/a.md"))
    }

    func testBatchNewFolderPhaseTwoContentionTruthfullyLeavesEveryItemUnmoved()
        async throws
    {
        let gate = SuspensionGate()
        let (state, vault) = try await makeVault(
            named: "batch-new-phase-two-contention",
            files: ["a.md", "b.md", "busy.md", "dest/keep.md"])
        let competitorReport = moveReport(
            state: .noOp, planned: [item("busy.md")])
        state.batchMoveRunner = { _, _ in
            await gate.enter()
            return competitorReport
        }
        state.structuralBatchRefreshRunner = { _ in }
        let competitor = TaskBox()
        state.createFolderMoveContinuationGateForTesting = {
            state.createFolderMoveContinuationGateForTesting = nil
            competitor.task = state.batchMove(
                [self.sel("busy.md")], to: "dest", preferredFocusPath: nil)
        }

        let compound = try XCTUnwrap(
            state.createFolderThenBatchMove(
                newFolderName: "New Folder", in: "",
                items: [sel("a.md"), sel("b.md")],
                preferredFocusPath: "b.md"))
        await compound.value
        await gate.waitForEntrants(1)

        XCTAssertTrue(exists(vault, "New Folder"))
        XCTAssertTrue(exists(vault, "a.md"))
        XCTAssertTrue(exists(vault, "b.md"))
        XCTAssertFalse(exists(vault, "New Folder/a.md"))
        XCTAssertFalse(exists(vault, "New Folder/b.md"))
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            "\(AppState.structuralMutationBusyReason) 2 items were not moved.")

        await gate.releaseOne()
        await competitor.task?.value
        XCTAssertTrue(exists(vault, "a.md"), "phase two never retries implicitly")
        XCTAssertTrue(exists(vault, "b.md"))
    }

    func testMoveSheetsUseCapturedResolversAndSharedBusyPresentationByInspection()
        throws
    {
        let appState = try Self.normalizedSource("AppState.swift")
        let main = try Self.normalizedSource("MainSplitView.swift")
        let sheet = try Self.normalizedSource("MoveToFolderSheet.swift")
        let sidebar = try Self.normalizedSidebarSource()

        XCTAssertTrue(
            sidebar.contains("Self.sidebarRowActionProjection("),
            "row-owned Move captures its exact single or batch target through the catalog")
        XCTAssertTrue(sidebar.contains("sidebarCatalogActions(projection.evaluations)"))
        XCTAssertTrue(sidebar.contains("appState.dispatchSidebarAction(intent)"))
        XCTAssertFalse(sidebar.contains("appState.requestPendingMove("))
        XCTAssertFalse(sidebar.contains("appState.requestBatchMove("))
        XCTAssertFalse(sidebar.contains("appState.pendingMove ="))
        XCTAssertTrue(
            main.contains(
                "item: Self.pendingMoveSheetBinding( appState: appState, "
                    + "presented: $presentedMove)"))
        XCTAssertTrue(
            main.contains(
                "item: Self.pendingBatchMoveSheetBinding( appState: appState, "
                    + "presented: $presentedBatchMove)"))
        XCTAssertFalse(
            main.contains("get: { appState.pendingMove != nil }, set: { _ in }"),
            "single-move external sheet dismissal must not be ignored")
        XCTAssertFalse(
            main.contains("get: { appState.pendingBatchMove != nil }, set: { _ in }"),
            "batch-move external sheet dismissal must not be ignored")
        XCTAssertTrue(sheet.contains("appState.commitPendingMove(id: move.id, to: target)"))
        XCTAssertTrue(
            sheet.contains(
                "appState.commitPendingMoveToNewFolder( id: move.id, "
                    + "newFolderName: name, in:"))
        XCTAssertTrue(sheet.contains("appState.cancelPendingMove(id: move.id)"))
        XCTAssertTrue(sheet.contains("appState.cancelPendingBatchMove(id: batch.id)"))
        XCTAssertFalse(
            sheet.contains("if cancelled { dismiss() }"),
            "a frozen old sheet must close after identity-safe cancellation so its replacement can promote")
        XCTAssertTrue(
            sheet.contains(
                "private func dismissSheet() { switch scope { "
                    + "case .single(let move): appState.cancelPendingMove(id: move.id) "
                    + "case .batch(let batch): appState.cancelPendingBatchMove(id: batch.id) "
                    + "} dismiss() }"),
            "Cancel/Escape always closes the frozen owner after its identity-safe cancel attempt")
        XCTAssertFalse(sheet.contains("appState.pendingMove = nil"))
        XCTAssertTrue(
            sheet.contains("if let reason = appState.structuralMutationDisabledReason"))
        XCTAssertTrue(
            sheet.contains(".disabled(appState.structuralMutationDisabledReason != nil)"))
        XCTAssertTrue(sheet.contains(".onSubmit(commitSelected)"), "Return semantics stay intact")
        XCTAssertTrue(
            appState.contains(
                "guard let createTask = createFolder( name: suffixed, in: parent, "
                    + "onResult: { created = $0 }) else"))
    }

    func testC3MoveSheetExtractsDestinationLegalityWithDeterministicWorkSeams()
        throws
    {
        let sheet = try Self.normalizedSource("MoveToFolderSheet.swift")

        XCTAssertTrue(
            sheet.contains("struct DestinationLegalityIndex"),
            "destination legality needs one reusable index built from the captured selection")
        XCTAssertTrue(
            sheet.contains("selectedItemVisits: inout Int"),
            "selection precomputation needs a deterministic item-visit seam")
        XCTAssertTrue(
            sheet.contains("selectedItemComparisons: inout Int"),
            "candidate checks need to prove they never rescan selected items")
        XCTAssertTrue(
            sheet.contains("componentVisits: inout Int"),
            "candidate work needs a deterministic path-component seam")
        XCTAssertTrue(
            sheet.contains("destinationLegalityStore.index("),
            "the live offered-destination path must use the extracted index")
    }

    func testC3RepeatedSheetInitializationReusesTheOperationIdentityIndex()
        throws
    {
        let sheet = try Self.normalizedSource("MoveToFolderSheet.swift")
        XCTAssertTrue(
            sheet.contains(
                "@StateObject private var destinationLegalityStore = DestinationLegalityStore()"),
            "SwiftUI reinitialization must retain selection preprocessing in lifetime-managed state")
        XCTAssertTrue(
            sheet.contains(
                "destinationLegalityStore.index( for: operationID, items: capturedItems"),
            "every candidate must retrieve the index by the pending operation's stable identity")

        let operationID = UUID(uuidString: "00000000-0000-0000-0000-0000000000C3")!
        let nextOperationID = UUID(uuidString: "00000000-0000-0000-0000-0000000000C4")!
        let items = (0..<10_000).map { sel("selected-\($0)", dir: true) }
        let store = MoveToFolderSheet.DestinationLegalityStore()
        var selectedItemVisits = 0

        let first = store.index(
            for: operationID, items: items,
            selectedItemVisits: &selectedItemVisits)
        XCTAssertEqual(selectedItemVisits, items.count)
        XCTAssertFalse(first.isLegalTarget("selected-9999/child"))

        let repeatedInitialization = store.index(
            for: operationID, items: [sel("poison", dir: true)],
            selectedItemVisits: &selectedItemVisits)
        XCTAssertEqual(
            selectedItemVisits, items.count,
            "reinitializing the same sheet identity must not rescan its 10,000 captured items")
        XCTAssertFalse(
            repeatedInitialization.isLegalTarget("selected-9999/child"),
            "the same operation identity retains its original captured-selection index")

        let nextOperation = store.index(
            for: nextOperationID, items: [sel("poison", dir: true)],
            selectedItemVisits: &selectedItemVisits)
        XCTAssertEqual(
            selectedItemVisits, items.count + 1,
            "a genuinely new pending operation receives exactly one fresh precomputation")
        XCTAssertFalse(nextOperation.isLegalTarget("poison/child"))
    }

    func testC3DestinationLegalityPreservesTheCompleteSemanticMatrix() {
        let composed = "caf\u{00E9}"
        let decomposed = "cafe\u{0301}"
        let directory = MoveToFolderSheet.DestinationLegalityIndex([
            sel(composed, dir: true)
        ])

        XCTAssertFalse(directory.isLegalTarget(composed), "a selected directory is not a target")
        XCTAssertFalse(
            directory.isLegalTarget(decomposed),
            "current Swift equality treats canonically-equivalent spellings as the same path")
        XCTAssertFalse(
            directory.isLegalTarget(decomposed + "/child"),
            "canonical equivalence also applies at the selected-directory component boundary")
        XCTAssertTrue(
            directory.isLegalTarget("CAF\u{00C9}"),
            "destination legality remains case-sensitive")

        let boundary = MoveToFolderSheet.DestinationLegalityIndex([
            sel("folder", dir: true)
        ])
        XCTAssertFalse(boundary.isLegalTarget("folder/subtree"))
        XCTAssertTrue(
            boundary.isLegalTarget("folderish/subtree"),
            "textual prefixes that do not end at a slash boundary remain legal")

        let sharedParent = MoveToFolderSheet.DestinationLegalityIndex([
            sel("parent/a.md"), sel("parent/b.md"),
        ])
        XCTAssertFalse(
            sharedParent.isLegalTarget("parent"),
            "a target is a no-op only when every selected item already lives there")
        XCTAssertTrue(sharedParent.isLegalTarget(""), "moving the shared-parent batch to root is legal")

        let mixedParents = MoveToFolderSheet.DestinationLegalityIndex([
            sel("parent/a.md"), sel("other/b.md"),
        ])
        XCTAssertTrue(
            mixedParents.isLegalTarget("parent"),
            "one no-op member does not make a mixed-parent batch a pure no-op")
        XCTAssertTrue(mixedParents.isLegalTarget(""), "files-only mixed-parent batches can move to root")

        let rootFiles = MoveToFolderSheet.DestinationLegalityIndex([
            sel("a.md"), sel("b.md"),
        ])
        XCTAssertFalse(rootFiles.isLegalTarget(""), "an all-root batch cannot move to root again")
        XCTAssertFalse(
            MoveToFolderSheet.DestinationLegalityIndex([]).isLegalTarget("anywhere"),
            "the prior empty-selection allSatisfy behavior rejects every target")

        let malformedEmptyDirectory = MoveToFolderSheet.DestinationLegalityIndex([
            sel("", dir: true)
        ])
        XCTAssertFalse(malformedEmptyDirectory.isLegalTarget(""))
        XCTAssertTrue(
            malformedEmptyDirectory.isLegalTarget("child"),
            "an empty selected path does not exclude an ordinary relative child")
        XCTAssertFalse(
            malformedEmptyDirectory.isLegalTarget("/child"),
            "the prior empty-path prefix still excludes malformed absolute descendants")
    }

    func testC3TenThousandSelectedItemsArePrecomputedOncePerSheetLifetime() {
        let items = (0..<10_000).map { sel("selected-\($0)", dir: true) }
        var selectedItemVisits = 0
        let legality = MoveToFolderSheet.DestinationLegalityIndex(
            items, selectedItemVisits: &selectedItemVisits)

        XCTAssertEqual(selectedItemVisits, items.count)

        var selectedItemComparisons = 0
        var componentVisits = 0
        XCTAssertTrue(
            legality.isLegalTarget(
                "candidate/branch",
                selectedItemComparisons: &selectedItemComparisons,
                componentVisits: &componentVisits))
        XCTAssertEqual(
            selectedItemComparisons, 0,
            "candidate legality must not rescan any of the 10,000 selected items")
        XCTAssertLessThanOrEqual(
            componentVisits, 2,
            "one candidate costs at most its two path components")
        XCTAssertEqual(
            selectedItemVisits, items.count,
            "re-evaluating offered destinations for query changes reuses the same precomputation")
    }

    func testC3FiftyThousandFoldersUseOnlyBoundedComponentLookups() {
        var selectedItemVisits = 0
        let legality = MoveToFolderSheet.DestinationLegalityIndex(
            [sel("selected", dir: true)],
            selectedItemVisits: &selectedItemVisits)
        let folders = (0..<50_000).map { "candidate-\($0)/nested" }
        var selectedItemComparisons = 0
        var componentVisits = 0
        var legalCount = 0

        for folder in folders {
            if legality.isLegalTarget(
                folder,
                selectedItemComparisons: &selectedItemComparisons,
                componentVisits: &componentVisits)
            {
                legalCount += 1
            }
        }

        XCTAssertEqual(legalCount, folders.count)
        XCTAssertEqual(selectedItemVisits, 1)
        XCTAssertEqual(
            selectedItemComparisons, 0,
            "50,000 candidates must not multiply by the captured selection size")
        XCTAssertLessThanOrEqual(
            componentVisits, folders.count * 2,
            "candidate work stays bounded by total path components, never wall-clock time")
    }

    func testC3TenThousandSelectionsAcrossFiftyThousandFoldersReuseOneIndex() {
        let items = (0..<10_000).map { sel("selected-\($0)", dir: true) }
        let folders = (0..<50_000).map { "candidate-\($0)/nested" }
        var selectedItemVisits = 0
        let legality = MoveToFolderSheet.DestinationLegalityIndex(
            items, selectedItemVisits: &selectedItemVisits)
        var selectedItemComparisons = 0
        var componentVisits = 0
        var legalCount = 0

        // Two evaluations model the SwiftUI `offered` recomputations caused by
        // an initial render plus a query change. The selection-derived index is
        // sheet-lifetime state and must not be rebuilt by either pass.
        for _ in 0..<2 {
            for folder in folders {
                if legality.isLegalTarget(
                    folder,
                    selectedItemComparisons: &selectedItemComparisons,
                    componentVisits: &componentVisits)
                {
                    legalCount += 1
                }
            }
        }

        XCTAssertEqual(legalCount, folders.count * 2)
        XCTAssertEqual(selectedItemVisits, items.count)
        XCTAssertEqual(selectedItemComparisons, 0)
        XCTAssertLessThanOrEqual(
            componentVisits, folders.count * 2 * 2,
            "two 50,000-folder passes remain proportional only to candidate path depth")
    }

    func testBatchTrashAttentionPreviewIsBoundedAndCopiedDetailsAreComplete() {
        let planned = (0..<22).map { item("Folder/item-\($0).md") }
        let lateBookkeepingFailure = BatchItemFailure(
            item: item("late-ledger.md"), stage: .journal,
            message: "history entry could not be recorded")
        let report = trashReport(
            state: .succeeded, planned: planned, opID: 901, trashed: planned,
            bookkeepingFailures: [lateBookkeepingFailure])
        let result = AppState.BatchStructuralResult(
            id: UUID(uuidString: "00000000-0000-0000-0000-000000000901")!,
            payload: .trash(report), requiresAttention: true)

        let preview = AppState.BatchStructuralCopy.attention(for: result)
        XCTAssertEqual(preview.title, "Trash Completed with Details")
        XCTAssertTrue(
            preview.message.contains(
                "late-ledger.md — History recording: history entry could not be recorded"),
            "a late actionable failure outranks a large planned/request list")
        XCTAssertTrue(preview.message.contains("Moved to system Trash — Folder/item-18.md"))
        XCTAssertFalse(preview.message.contains("Moved to system Trash — Folder/item-19.md"))
        XCTAssertTrue(preview.message.contains("…and 25 more"))
        XCTAssertTrue(preview.hasOmittedDetails)
        XCTAssertEqual(preview.previewWorkCount, 20)

        let details = AppState.BatchStructuralCopy.copiedDetails(for: result)
        XCTAssertTrue(details.contains("State: Succeeded"))
        XCTAssertTrue(details.contains("Planned — Folder/item-21.md"))
        XCTAssertTrue(details.contains("Moved to system Trash — Folder/item-21.md"))
        XCTAssertFalse(details.contains("…and"))
    }

    func testBatchMoveAttentionDetailsRetainRecoveryVectorsAndUndoRedoMode() {
        let planned = (0..<25).map { item("request/item-\($0).md") }
        let skipped = BatchSkippedItem(
            item: item("request/skipped.md"), reason: .alreadyInDestination,
            detail: "already there")
        let preflight = BatchItemFailure(
            item: nil, stage: .preflight, message: "request check failed")
        let primary = BatchItemFailure(
            item: item("problem.md"), stage: .move, message: "move failed")
        let rollback = BatchItemFailure(
            item: nil, stage: .rollback, message: "global restoration stopped")
        let report = moveReport(
            state: .rollbackIncomplete,
            planned: planned,
            opID: 902,
            standing: [
                BatchPathChange(
                    oldPath: "stood.md", newPath: "dest/stood.md",
                    isDirectory: false)
            ],
            rolledBack: [
                BatchPathChange(
                    oldPath: "restored.md", newPath: "dest/restored.md",
                    isDirectory: false)
            ],
            skipped: [skipped],
            preflightFailures: [preflight],
            failure: primary,
            rollbackFailures: [rollback],
            rewritten: [
                RewriteOutcome(
                    path: "links-updated.md", hashBefore: "old", hashAfter: "new")
            ],
            rewriteFailures: [
                RewriteFailure(
                    path: "links-failed.md",
                    kind: RewriteFailureKind(kind: "other", detail: "changed on disk"))
            ],
            requiresRescan: true)
        let undo = AppState.BatchStructuralResult(
            payload: .move(report, mode: .undo), requiresAttention: true)

        let preview = AppState.BatchStructuralCopy.attention(for: undo)
        XCTAssertLessThanOrEqual(preview.previewWorkCount, 20)
        XCTAssertTrue(preview.message.contains("problem.md — Move: move failed"))
        XCTAssertTrue(
            preview.message.contains(
                "Request — Restoration: global restoration stopped"))
        XCTAssertTrue(
            preview.message.contains("Standing — stood.md → dest/stood.md"))
        XCTAssertTrue(
            preview.message.contains("Restored — dest/restored.md → restored.md"))
        XCTAssertFalse(
            preview.message.contains("Planned — request/item-12.md"),
            "request context can use spare slots but never displaces actionable details")

        let details = AppState.BatchStructuralCopy.copiedDetails(for: undo)
        XCTAssertTrue(details.contains("Mode: Undo"))
        XCTAssertTrue(details.contains("Planned — request/item-24.md"))
        XCTAssertTrue(details.contains("Skipped — request/skipped.md"))
        XCTAssertTrue(details.contains("Request — Preflight: request check failed"))
        XCTAssertTrue(details.contains("problem.md — Move: move failed"))
        XCTAssertTrue(details.contains("Request — Restoration: global restoration stopped"))
        XCTAssertTrue(details.contains("Standing — stood.md → dest/stood.md"))
        XCTAssertTrue(details.contains("Restored — dest/restored.md → restored.md"))
        XCTAssertTrue(details.contains("Link updated — links-updated.md"))
        XCTAssertTrue(
            details.contains(
                "Link update failed — links-failed.md: Other: changed on disk"))
        XCTAssertTrue(details.contains("Reconciliation required: Yes"))

        let redo = AppState.BatchStructuralResult(
            payload: .move(report, mode: .redo), requiresAttention: true)
        XCTAssertTrue(
            AppState.BatchStructuralCopy.copiedDetails(for: redo)
                .contains("Mode: Redo"))
    }

    func testBatchAttentionPreviewVisitsAtMostTwentyEntriesForLargeReports() {
        let items = (0..<10_000).map { item("large/item-\($0).md") }
        let trash = AppState.BatchStructuralResult(
            payload: .trash(
                trashReport(
                    state: .succeeded, planned: items, opID: 903,
                    trashed: items)),
            requiresAttention: true)
        let move = AppState.BatchStructuralResult(
            payload: .move(
                moveReport(
                    state: .succeeded, planned: items, opID: 904,
                    standing: items.map {
                        BatchPathChange(
                            oldPath: $0.path, newPath: "dest/\($0.path)",
                            isDirectory: false)
                    }),
                mode: .forward(destination: "dest")),
            requiresAttention: true)

        XCTAssertEqual(
            AppState.BatchStructuralCopy.attention(for: trash).previewWorkCount, 20)
        XCTAssertEqual(
            AppState.BatchStructuralCopy.attention(for: move).previewWorkCount, 20)
    }

    func testRewriteFailureCopyPreservesEveryGeneratedKindAndOtherDetail() {
        let kinds = [
            RewriteFailureKind(kind: "write_conflict", detail: ""),
            RewriteFailureKind(kind: "malformed_frontmatter", detail: ""),
            RewriteFailureKind(kind: "cancelled", detail: ""),
            RewriteFailureKind(kind: "other", detail: "permission denied"),
            RewriteFailureKind(kind: "future_kind", detail: "future detail"),
        ]
        let expected = [
            "Write conflict",
            "Malformed frontmatter",
            "Cancelled",
            "Other: permission denied",
            "future_kind: future detail",
        ]

        for (kind, copy) in zip(kinds, expected) {
            XCTAssertEqual(
                AppState.BatchStructuralCopy.rewriteFailureReason(kind), copy)
        }
    }

    func testBatchTrashProductionCopyHasOneBuilderByInspection() throws {
        let main = try Self.normalizedSource("MainSplitView.swift")
        let state = try Self.normalizedSource("AppState.swift")
        XCTAssertTrue(main.contains("AppState.BatchTrashCopy.confirmationTitle"))
        XCTAssertTrue(main.contains("AppState.BatchTrashCopy.confirmationMessage"))
        XCTAssertTrue(main.contains("AppState.BatchTrashCopy.actionLabel"))
        XCTAssertTrue(main.contains("AppState.BatchStructuralCopy.attention"))
        XCTAssertTrue(
            main.contains("dismiss: appState.copyAndDismissBatchStructuralDetails"))
        XCTAssertFalse(main.contains("appState.copyBatchStructuralDetails(id: result.id)"))
        XCTAssertTrue(state.contains("BatchStructuralCopy.copiedDetails(for: result)"))
        XCTAssertTrue(state.contains("BatchTrashCopy.announcement(for: report)"))
    }

    func testStaleBatchAttentionDismissalCannotFocusOrClearPromotedResult()
        async throws
    {
        let trash = trashReport(
            state: .failed, planned: [item("a.md"), item("b.md")])
        let move = moveReport(
            state: .rejected, planned: [item("a.md"), item("b.md")])
        let (state, _) = try await makeVault(
            files: ["a.md", "b.md", "dest/keep.md"])
        state.batchTrashRunner = { _, _ in trash }
        state.batchMoveRunner = { _, _ in move }
        state.structuralBatchRefreshRunner = { _ in }

        await state.batchDelete([sel("a.md"), sel("b.md")]).value
        guard case .result(let a)? = state.activeBatchAlertPresentation else {
            return XCTFail("A must be the rendered result")
        }
        await state.batchMove(
            [sel("a.md"), sel("b.md")],
            to: "dest",
            preferredFocusPath: nil
        )?.value
        guard case .result(let b)? = state.deferredBatchAlertPresentation else {
            return XCTFail("B must wait behind rendered A")
        }
        var responderFocusEdges = 0
        var accessibilityFocusEdges = 0

        func resolve(_ id: UUID) -> Bool {
            let resolved = BatchAttentionDismissal.resolve(
                id: id,
                dismiss: state.dismissBatchStructuralResult,
                focus: { responderFocusEdges += 1 })
            if resolved { accessibilityFocusEdges += 1 }
            return resolved
        }

        XCTAssertTrue(resolve(a.id))
        guard case .result(let promoted)? = state.activeBatchAlertPresentation else {
            return XCTFail("A dismissal must promote B")
        }
        XCTAssertEqual(promoted.id, b.id)
        XCTAssertEqual(responderFocusEdges, 1)
        XCTAssertEqual(accessibilityFocusEdges, 1)

        XCTAssertFalse(
            resolve(a.id),
            "a stale A callback must be rejected after B is active")
        guard case .result(let stillB)? = state.activeBatchAlertPresentation else {
            return XCTFail("stale A must leave B rendered")
        }
        XCTAssertEqual(stillB.id, b.id)
        XCTAssertEqual(
            responderFocusEdges, 1,
            "rejected stale callbacks create no responder focus edge")
        XCTAssertEqual(
            accessibilityFocusEdges, 1,
            "rejected stale callbacks create no accessibility focus edge")
    }

    func testBatchAttentionDoneAndExitUseRenderedCapturedUUIDByInspection() throws {
        let main = try Self.normalizedSource("MainSplitView.swift")
        XCTAssertTrue(
            main.contains(
                "Button(AppState.BatchTrashCopy.doneLabel, role: .cancel) { "
                    + "if BatchAttentionDismissal.resolve( id: result.id, "
                    + "dismiss: appState.dismissBatchStructuralResult, "
                    + "focus: appState.workspace.focusTreeRegion ) { "
                    + "alertFocusReturn = .tree } } "
                    + ".keyboardShortcut(.defaultAction)"),
            "Return, Escape, and Command-Period share the rendered result's captured UUID")
        XCTAssertFalse(main.contains("dismissActiveBatchAttention"))
        XCTAssertTrue(
            main.contains(
                "FileTreeSidebar(rowPreferences: appState.sidebarPreferences.rowSnapshot) "
                    + ".accessibilityLabel( ) "
                    + ".accessibilityFocused($alertFocusReturn, equals: .tree)"),
            "the files sidebar is the real accessibility focus target")
        XCTAssertEqual(
            main.components(separatedBy: "alertFocusReturn = .tree").count - 1, 4,
            "Done, Copy Details, and both Move sheets expose focus return to the alert checker")
        XCTAssertFalse(main.contains("private func dismissBatchStructuralAttention"))
    }

    func testCopyDetailsCopiesDismissesPromotesOnceAndRejectsStaleReplay()
        async throws
    {
        let planned = (0..<25).map { item("item-\($0).md") }
        let report = trashReport(state: .failed, planned: planned)
        let (state, _) = try await makeVault(
            files: planned.map(\.path) + (1...10).map { "open-\($0).md" })
        state.batchTrashRunner = { _, _ in report }
        state.structuralBatchRefreshRunner = { _ in }
        var copied: [String] = []
        state.batchStructuralDetailsCopier = {
            copied.append($0)
            return false
        }
        await state.batchDelete(planned.map { sel($0.path) }).value
        guard case .result(let active)? = state.activeBatchAlertPresentation else {
            return XCTFail("the failed report must own the active alert")
        }
        let identity = try XCTUnwrap(state.currentSession.map(ObjectIdentifier.init))
        let deferred = FileTreeSidebar.OpenSelectionRequest(
            sessionIdentity: identity,
            batch: .init(
                paths: (1...10).map { "open-\($0).md" },
                focusedPath: "open-4.md"))
        XCTAssertTrue(state.enqueueOpenSelection(deferred))

        var responderFocusEdges = 0
        var accessibilityFocusEdges = 0
        func copyAndFocus(_ id: UUID) -> Bool {
            let resolved = BatchAttentionDismissal.resolve(
                id: id,
                dismiss: state.copyAndDismissBatchStructuralDetails,
                focus: { responderFocusEdges += 1 })
            if resolved { accessibilityFocusEdges += 1 }
            return resolved
        }

        XCTAssertFalse(
            copyAndFocus(active.id),
            "a clipboard failure keeps the rendered report and returns no focus")
        XCTAssertEqual(copied.count, 1)
        XCTAssertTrue(copied[0].contains("Planned — item-24.md"))
        XCTAssertEqual(responderFocusEdges, 0)
        XCTAssertEqual(accessibilityFocusEdges, 0)
        guard case .result(let stillActive)? = state.activeBatchAlertPresentation else {
            return XCTFail("clipboard failure must retain the rendered report")
        }
        XCTAssertEqual(stillActive.id, active.id)

        state.batchStructuralDetailsCopier = {
            copied.append($0)
            return true
        }
        XCTAssertTrue(copyAndFocus(active.id))
        XCTAssertEqual(copied.count, 2)
        XCTAssertEqual(responderFocusEdges, 1)
        XCTAssertEqual(accessibilityFocusEdges, 1)
        XCTAssertNil(state.batchStructuralResult)
        guard case .open(let promoted)? = state.activeBatchAlertPresentation else {
            return XCTFail("Copy Details must dismiss and promote the deferred owner")
        }
        XCTAssertEqual(promoted.id, deferred.id)
        XCTAssertFalse(copyAndFocus(active.id))
        XCTAssertEqual(copied.count, 2, "a stale replay neither copies nor dismisses again")
        XCTAssertEqual(responderFocusEdges, 1)
        XCTAssertEqual(accessibilityFocusEdges, 1)
    }

    func testTrashAlertHIGWiringUsesCapturedResolversAndVisibleBusyReason()
        throws
    {
        let main = try Self.normalizedSource("MainSplitView.swift")
        let sidebar = try Self.normalizedSidebarSource()
        XCTAssertGreaterThanOrEqual(
            main.components(separatedBy: "set: { _ in }").count - 1,
            2,
            "batch confirmation and attention Binding setters have no side effects")
        XCTAssertTrue(
            sidebar.contains("set: { _ in }"),
            "a stale Open alert Binding callback cannot cancel a newer UUID")
        XCTAssertEqual(
            main.components(
                separatedBy: ".disabled(appState.structuralMutationDisabledReason != nil)"
            ).count - 1,
            2,
            "only the single and batch destructive Trash actions share the busy gate")
        XCTAssertGreaterThanOrEqual(
            main.components(
                separatedBy: "if let reason = appState.structuralMutationDisabledReason"
            ).count - 1,
            2,
            "both confirmations render the shared busy reason visibly")
        XCTAssertTrue(
            main.contains(
                "dismiss: appState.copyAndDismissBatchStructuralDetails"),
            "Copy Details uses the captured result through the shared success gate")
        XCTAssertFalse(
            main.contains("appState.copyBatchStructuralDetails(id: result.id)"),
            "Copy Details uses one copy+dismiss resolver, then one focus edge")
        XCTAssertEqual(
            main.components(separatedBy: ".keyboardShortcut(.defaultAction)").count - 1,
            1,
            "Done is the sole default action; destructive confirmation ignores Return")
        XCTAssertTrue(main.contains("BatchTrashCopy.singleFolderConfirmationTitle"))
        XCTAssertTrue(main.contains("BatchTrashCopy.singleFolderConfirmationMessage"))
        XCTAssertTrue(
            main.contains(
                "TrashConfirmationReturnKeyMonitor( owner: activeTrashConfirmationOwner, "
                    + "onReturn: handleTrashConfirmationReturn)"),
            "the mounted alert view routes Return through the inert captured-UUID resolver")
    }

    func testOpenConfirmationDefersBatchAttentionUntilDismissal() async throws {
        let (state, _) = try await makeVault(
            files: (1...10).map { "note-\($0).md" } + ["trash-a.md", "trash-b.md"])
        let identity = try XCTUnwrap(state.currentSession.map(ObjectIdentifier.init))
        let open = FileTreeSidebar.OpenSelectionRequest(
            sessionIdentity: identity,
            batch: FileTreeSidebar.OpenSelectionBatch(
                paths: (1...10).map { "note-\($0).md" },
                focusedPath: "note-3.md"))
        XCTAssertTrue(state.enqueueOpenSelection(open))
        guard case .open(let activeOpen)? = state.activeBatchAlertPresentation else {
            return XCTFail("the open confirmation must be the sole active alert")
        }
        XCTAssertEqual(activeOpen, open)

        let failure = BatchItemFailure(
            item: item("trash-b.md"), stage: .trash, message: "busy on disk")
        let report = trashReport(
            state: .partial,
            planned: [item("trash-a.md"), item("trash-b.md")],
            trashed: [item("trash-a.md")],
            untrashed: [
                BatchTrashRemainder(item: item("trash-b.md"), failure: failure)
            ])
        let gate = SuspensionGate()
        state.batchTrashRunner = { _, _ in
            await gate.enter()
            return report
        }
        state.structuralBatchRefreshRunner = { _ in }
        let task = state.batchDelete([sel("trash-a.md"), sel("trash-b.md")])
        await gate.waitForEntrants(1)
        guard case .open? = state.activeBatchAlertPresentation else {
            return XCTFail("the open alert must remain active while the batch is suspended")
        }
        XCTAssertNil(state.deferredBatchAlertPresentation)
        await gate.releaseOne()
        await task.value

        guard case .open? = state.activeBatchAlertPresentation else {
            return XCTFail("a finishing batch must not replace the active open alert")
        }
        guard case .result(let deferred)? = state.deferredBatchAlertPresentation else {
            return XCTFail("the complete attention result must be deferred")
        }
        XCTAssertEqual(deferred, state.batchStructuralResult)

        guard case .opened(let paths) = state.confirmOpenSelection(id: open.id) else {
            return XCTFail("the current frozen Open request must execute")
        }
        XCTAssertEqual(paths.last, "note-3.md")
        guard case .result(let promoted)? = state.activeBatchAlertPresentation else {
            return XCTFail("dismissal must promote the deferred result exactly once")
        }
        XCTAssertEqual(promoted.id, deferred.id)
        XCTAssertNil(state.deferredBatchAlertPresentation)
    }

    func testBatchAttentionDefersOpenConfirmationAndCancelPromotesIt() async throws {
        let (state, _) = try await makeVault(
            files: (1...10).map { "note-\($0).md" } + ["trash-a.md", "trash-b.md"])
        let failed = trashReport(
            state: .failed,
            planned: [item("trash-a.md"), item("trash-b.md")])
        state.batchTrashRunner = { _, _ in failed }
        state.structuralBatchRefreshRunner = { _ in }
        await state.batchDelete([sel("trash-a.md"), sel("trash-b.md")]).value
        guard case .result(let activeResult)? = state.activeBatchAlertPresentation else {
            return XCTFail("the attention result must be active")
        }

        let identity = try XCTUnwrap(state.currentSession.map(ObjectIdentifier.init))
        let open = FileTreeSidebar.OpenSelectionRequest(
            sessionIdentity: identity,
            batch: FileTreeSidebar.OpenSelectionBatch(
                paths: (1...10).map { "note-\($0).md" }, focusedPath: nil))
        XCTAssertTrue(state.enqueueOpenSelection(open))
        guard case .result(let stillActive)? = state.activeBatchAlertPresentation else {
            return XCTFail("the open request must not replace an active result")
        }
        XCTAssertEqual(stillActive.id, activeResult.id)
        guard case .open(let deferred)? = state.deferredBatchAlertPresentation else {
            return XCTFail("the open request must be retained")
        }
        XCTAssertEqual(deferred, open)

        XCTAssertTrue(state.dismissBatchStructuralResult(id: activeResult.id))
        guard case .open(let promoted)? = state.activeBatchAlertPresentation else {
            return XCTFail("Done must promote the deferred open confirmation")
        }
        XCTAssertEqual(promoted, open)
        XCTAssertTrue(state.cancelOpenSelection(id: open.id))
        XCTAssertNil(state.activeBatchAlertPresentation)
        XCTAssertNil(state.deferredBatchAlertPresentation)
    }

    func testSessionReplacementDropsActiveAndDeferredBatchAlerts() async throws {
        let (state, vault) = try await makeVault(
            files: (1...10).map { "note-\($0).md" } + ["trash-a.md", "trash-b.md"])
        let originalIdentity = try XCTUnwrap(
            state.currentSession.map(ObjectIdentifier.init))
        let open = FileTreeSidebar.OpenSelectionRequest(
            sessionIdentity: originalIdentity,
            batch: FileTreeSidebar.OpenSelectionBatch(
                paths: (1...10).map { "note-\($0).md" }, focusedPath: nil))
        XCTAssertTrue(state.enqueueOpenSelection(open))

        let failed = trashReport(
            state: .failed,
            planned: [item("trash-a.md"), item("trash-b.md")])
        state.batchTrashRunner = { _, _ in failed }
        state.structuralBatchRefreshRunner = { _ in }
        await state.batchDelete([sel("trash-a.md"), sel("trash-b.md")]).value
        XCTAssertNotNil(state.deferredBatchAlertPresentation)

        state.openVault(at: vault)
        await state.scanTask?.value
        XCTAssertNotEqual(
            state.currentSession.map(ObjectIdentifier.init), originalIdentity)
        XCTAssertNil(state.activeBatchAlertPresentation)
        XCTAssertNil(state.deferredBatchAlertPresentation)
        XCTAssertNil(state.batchStructuralResult)
        XCTAssertEqual(state.confirmOpenSelection(id: open.id), .ignored)
    }

    func testBatchMoveLandingKeepsUnrelatedLiveFocusChangedWhileRunnerSuspended()
        async throws
    {
        let planned = [item("a.md"), item("b.md")]
        let standing = [
            BatchPathChange(
                oldPath: "a.md", newPath: "dest/a.md", isDirectory: false),
            BatchPathChange(
                oldPath: "b.md", newPath: "dest/b.md", isDirectory: false),
        ]
        let report = moveReport(
            state: .succeeded, planned: planned, opID: 920, standing: standing)
        let gate = SuspensionGate()
        let (state, _) = try await makeVault(
            files: ["a.md", "b.md", "unrelated.md", "dest/keep.md"])
        state.batchMoveRunner = { _, _ in
            await gate.enter()
            return report
        }
        state.structuralBatchRefreshRunner = { _ in }

        let task = state.batchMove(
            [sel("a.md"), sel("b.md")], to: "dest",
            preferredFocusPath: "b.md")
        await gate.waitForEntrants(1)
        let liveFocusAtLanding = "unrelated.md"
        await gate.releaseOne()
        await task?.value

        guard case .batchMove(let landed, _)? = state.treeMutation?.kind else {
            return XCTFail("expected standing changes")
        }
        let index = FileTreeSidebar.SelectionModel.KnownMoveIndex(landed.map {
            .init(
                oldPath: $0.oldPath, newPath: $0.newPath,
                isDirectory: $0.isDirectory)
        })
        var visits = 0
        let plan = FileTreeSidebar.batchMoveFocusPlan(
            liveFocusPath: liveFocusAtLanding,
            liveFocusIsResolvable: true,
            preferredFocusPath: state.treeMutation?.preferredFocusPath,
            firstStandingPath: landed.first?.newPath,
            using: index,
            componentVisits: &visits)

        XCTAssertEqual(plan.path, "unrelated.md")
        XCTAssertFalse(plan.shouldRevealMovedAncestry)
    }

    func testBatchMoveFocusRestorationWaitsForMaterializationAndIsOneShot() {
        let pending = FileTreeSidebar.PendingBatchFocus(
            plan: .init(
                path: "dest/b.md", shouldRevealMovedAncestry: true),
            expectedFocusedPath: "dest/b.md")

        XCTAssertEqual(
            FileTreeSidebar.pendingBatchFocusDisposition(
                pending,
                currentFocusedPath: "dest/b.md",
                targetIsMaterialized: false),
            .wait)
        XCTAssertEqual(
            FileTreeSidebar.pendingBatchFocusDisposition(
                pending,
                currentFocusedPath: "dest/b.md",
                targetIsMaterialized: true),
            .restore)
        XCTAssertEqual(
            FileTreeSidebar.pendingBatchFocusDisposition(
                pending,
                currentFocusedPath: "unrelated.md",
                targetIsMaterialized: true),
            .cancel,
            "a later user focus edge supersedes the deferred batch fallback")
    }

    func testOrdinaryBatchSuccessAndNoOpNeverEnterAlertOwnership() async throws {
        let planned = [item("a.md"), item("b.md")]
        let scenarios = [
            trashReport(
                state: .succeeded, planned: planned, opID: 910,
                trashed: planned),
            trashReport(state: .noOp, planned: planned),
        ]

        for (index, report) in scenarios.enumerated() {
            let (state, _) = try await makeVault(
                named: "ordinary-alert-\(index)", files: ["a.md", "b.md"])
            state.batchTrashRunner = { _, _ in report }
            state.structuralBatchRefreshRunner = { _ in }
            await state.batchDelete([sel("a.md"), sel("b.md")]).value

            XCTAssertNil(state.activeBatchAlertPresentation)
            XCTAssertNil(state.deferredBatchAlertPresentation)
            XCTAssertFalse(state.batchStructuralResult?.requiresAttention ?? true)
        }
    }

    func testNewDeferredRequestReplacesOldWithoutStaleUUIDPromotion() async throws {
        let (state, _) = try await makeVault(
            files: (1...11).map { "note-\($0).md" } + ["a.md", "b.md"])
        let failed = trashReport(
            state: .failed, planned: [item("a.md"), item("b.md")])
        state.batchTrashRunner = { _, _ in failed }
        state.structuralBatchRefreshRunner = { _ in }
        await state.batchDelete([sel("a.md"), sel("b.md")]).value
        guard case .result(let active)? = state.activeBatchAlertPresentation else {
            return XCTFail("fixture requires one active result")
        }

        let identity = try XCTUnwrap(state.currentSession.map(ObjectIdentifier.init))
        let old = FileTreeSidebar.OpenSelectionRequest(
            sessionIdentity: identity,
            batch: FileTreeSidebar.OpenSelectionBatch(
                paths: (1...10).map { "note-\($0).md" }, focusedPath: nil))
        let newer = FileTreeSidebar.OpenSelectionRequest(
            sessionIdentity: identity,
            batch: FileTreeSidebar.OpenSelectionBatch(
                paths: (2...11).map { "note-\($0).md" }, focusedPath: "note-11.md"))
        XCTAssertTrue(state.enqueueOpenSelection(old))
        XCTAssertTrue(state.enqueueOpenSelection(newer))
        XCTAssertFalse(state.cancelOpenSelection(id: old.id))
        guard case .open(let deferred)? = state.deferredBatchAlertPresentation else {
            return XCTFail("the newer typed request must replace the old deferred one")
        }
        XCTAssertEqual(deferred.id, newer.id)

        XCTAssertTrue(state.dismissBatchStructuralResult(id: active.id))
        guard case .open(let promoted)? = state.activeBatchAlertPresentation else {
            return XCTFail("only the newer deferred request may promote")
        }
        XCTAssertEqual(promoted.id, newer.id)
    }

    // MARK: - Native one-call batch semantics (FL-03 Task 5)

    func testTwoItemBatchMoveUsesOneRunnerOneLandingAndOneUndoItem() async throws {
        let planned = [item("a.md"), item("b.md")]
        let standing = [
            BatchPathChange(oldPath: "a.md", newPath: "dest/a.md", isDirectory: false),
            BatchPathChange(oldPath: "b.md", newPath: "dest/b.md", isDirectory: false),
        ]
        let report = moveReport(
            state: .succeeded, planned: planned, opID: 77, standing: standing)
        let probe = BatchRunnerProbe(
            moveReport: report,
            trashReport: trashReport(state: .noOp, planned: []))
        let refresh = RefreshProbe()
        let (state, vault) = try await makeVault(files: ["a.md", "b.md", "dest/x.md"])
        state.batchMoveRunner = { _, request in await probe.runMove(request) }
        state.structuralBatchRefreshRunner = { appState in await refresh.run(appState) }

        let task = try XCTUnwrap(
            state.batchMove(
                [sel("a.md"), sel("b.md")], to: "dest",
                preferredFocusPath: "b.md"))
        await task.value

        let counts = await probe.callCounts()
        let capturedMove = await probe.lastMoveRequest()
        XCTAssertEqual(counts.move, 1, "exactly one native batch call")
        XCTAssertEqual(counts.trash, 0)
        XCTAssertEqual(capturedMove?.items, planned)
        XCTAssertEqual(refresh.calls, 1, "exactly one authoritative refresh")
        XCTAssertEqual(state.treeMutation?.token, 1, "one tree event")
        guard case .batchMove(let landed, _)? = state.treeMutation?.kind else {
            return XCTFail("expected one typed batch Move tree event")
        }
        XCTAssertEqual(landed, standing)
        XCTAssertEqual(state.lastMutationAnnouncement, "Moved 2 items to dest.")
        XCTAssertEqual(state.structuralUndoStack.count, 1, "the batch is one undo step")
        XCTAssertEqual(
            state.structuralUndoStack.last,
            .batchMove(opId: 77, entries: standing))
        guard case let .move(landedReport, mode)? = state.batchStructuralResult?.payload else {
            return XCTFail("the complete native Move report must be retained")
        }
        XCTAssertEqual(landedReport, report)
        XCTAssertEqual(mode, .forward(destination: "dest"))
        XCTAssertFalse(state.batchStructuralResult?.requiresAttention ?? true)
        XCTAssertTrue(exists(vault, "a.md"))
        XCTAssertTrue(exists(vault, "b.md"))
        XCTAssertFalse(
            exists(vault, "dest/a.md"),
            "the injected batch result must not fall through to single-item runners")
    }

    func testProjectedOneItemBatchStillUsesTheBatchRunner() async throws {
        let planned = [item("folder", dir: true)]
        let report = moveReport(
            state: .succeeded,
            planned: planned,
            opID: 90,
            standing: [
                BatchPathChange(
                    oldPath: "folder", newPath: "dest/folder", isDirectory: true)
            ])
        let probe = BatchRunnerProbe(
            moveReport: report,
            trashReport: trashReport(state: .noOp, planned: []))
        let refresh = RefreshProbe()
        let (state, _) = try await makeVault(files: ["folder/inside.md", "dest/x.md"])
        state.batchMoveRunner = { _, request in await probe.runMove(request) }
        state.structuralBatchRefreshRunner = { appState in await refresh.run(appState) }

        let task = try XCTUnwrap(
            state.batchMove(
                [sel("folder", dir: true), sel("folder/inside.md")],
                to: "dest", preferredFocusPath: "folder/inside.md"))
        await task.value

        let counts = await probe.callCounts()
        let capturedMove = await probe.lastMoveRequest()
        XCTAssertEqual(counts.move, 1)
        XCTAssertEqual(
            capturedMove?.items,
            [item("folder", dir: true), item("folder/inside.md")],
            "Swift passes the complete captured batch; core reports covered descendants")
        XCTAssertEqual(state.structuralUndoStack.count, 1)
    }

    /// No-op items (already directly in the destination) are skipped so the
    /// announced count is truthful, and illegal folder-into-own-subtree moves
    /// are skipped rather than round-tripping to a backend error.
    func testBatchMoveSkipsNoOpsAndCountsOnlyRealMoves() async throws {
        let (state, vault) = try await makeVault(files: ["dest/a.md", "b.md", "dest/x.md"])
        // a.md is ALREADY in dest → skipped; only b.md actually moves.
        await state.batchMove([sel("dest/a.md"), sel("b.md")], to: "dest").value
        XCTAssertTrue(exists(vault, "dest/b.md"))
        XCTAssertEqual(
            state.lastMutationAnnouncement, "Moved 1 item to dest.",
            "the no-op item is not counted")
    }

    /// Native batch Move preflights the whole projected action. One destination
    /// collision rejects the batch before either source moves or history lands.
    func testBatchMoveCountsOnlySuccessesOnPartialFailure() async throws {
        // dest/ already holds a.md, so neither a.md nor b.md may move.
        let (state, vault) = try await makeVault(files: ["a.md", "b.md", "dest/a.md"])
        await state.batchMove([sel("a.md"), sel("b.md")], to: "dest").value
        XCTAssertTrue(exists(vault, "a.md"))
        XCTAssertTrue(exists(vault, "b.md"))
        XCTAssertFalse(exists(vault, "dest/b.md"), "preflight is all-or-nothing")
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            "Move could not start. No items were moved.")
        XCTAssertTrue(state.structuralUndoStack.isEmpty)
        XCTAssertTrue(state.structuralRedoStack.isEmpty)
        guard case let .move(report, mode)? = state.batchStructuralResult?.payload else {
            return XCTFail("the typed native rejection must be retained")
        }
        XCTAssertEqual(report.state, .rejected)
        XCTAssertEqual(mode, .forward(destination: "dest"))
        XCTAssertTrue(report.standing.isEmpty)
        XCTAssertNil(report.opId, "a rejected batch has no history edge")
        XCTAssertTrue(state.batchStructuralResult?.requiresAttention ?? false)
    }

    // MARK: - Native one-call Trash semantics

    func testTwoItemBatchTrashUsesOneRunnerOneLandingAndNoHistory() async throws {
        let planned = [item("a.md"), item("b.md")]
        let report = trashReport(
            state: .succeeded, planned: planned, opID: 44, trashed: planned)
        let probe = BatchRunnerProbe(
            moveReport: moveReport(state: .noOp, planned: []),
            trashReport: report)
        let refresh = RefreshProbe()
        let (state, vault) = try await makeVault(files: ["a.md", "b.md", "c.md"])
        state.batchTrashRunner = { _, request in await probe.runTrash(request) }
        state.structuralBatchRefreshRunner = { appState in await refresh.run(appState) }

        let task = try XCTUnwrap(
            state.batchDelete(
                [sel("a.md"), sel("b.md")], preferredFocusPath: "b.md"))
        await task.value

        let counts = await probe.callCounts()
        let capturedTrash = await probe.lastTrashRequest()
        XCTAssertEqual(counts.move, 0)
        XCTAssertEqual(counts.trash, 1, "exactly one native batch Trash call")
        XCTAssertEqual(capturedTrash?.items, planned)
        XCTAssertEqual(refresh.calls, 1)
        XCTAssertEqual(state.treeMutation?.token, 1)
        guard case .batchTrash(let landed)? = state.treeMutation?.kind else {
            return XCTFail("expected one typed batch Trash tree event")
        }
        XCTAssertEqual(landed, planned)
        XCTAssertEqual(state.lastMutationAnnouncement, "Moved 2 items to Trash.")
        XCTAssertTrue(state.structuralUndoStack.isEmpty)
        XCTAssertTrue(state.structuralRedoStack.isEmpty)
        guard case let .trash(landedReport)? = state.batchStructuralResult?.payload else {
            return XCTFail("the complete native Trash report must be retained")
        }
        XCTAssertEqual(landedReport, report)
        XCTAssertFalse(state.batchStructuralResult?.requiresAttention ?? true)
        XCTAssertTrue(exists(vault, "a.md"))
        XCTAssertTrue(exists(vault, "b.md"))
        XCTAssertTrue(exists(vault, "c.md"))
    }

    func testFailedTrashWithReportedTrashedItemsForcesReconciliationAndTruthfulCopy()
        async throws
    {
        let planned = [item("a.md"), item("b.md")]
        let report = trashReport(
            state: .failed,
            planned: planned,
            opID: 300,
            trashed: [item("a.md")],
            requiresRescan: false)
        let refresh = RefreshProbe()
        let (state, _) = try await makeVault(
            files: ["a.md", "b.md", "history.md", "dest/x.md"])
        await state.moveEntry(
            path: "history.md", isDirectory: false, to: "dest")?.value
        XCTAssertEqual(state.structuralUndoStack.count, 1, "fixture arms prior history")
        state.batchTrashRunner = { _, _ in report }
        state.structuralBatchRefreshRunner = { appState in await refresh.run(appState) }

        let task = try XCTUnwrap(
            state.batchDelete(
                [sel("a.md"), sel("b.md")], preferredFocusPath: "a.md"))
        await task.value

        XCTAssertEqual(refresh.calls, 1)
        XCTAssertTrue(state.structuralUndoStack.isEmpty, "unknown remainder is a history barrier")
        XCTAssertTrue(state.structuralRedoStack.isEmpty)
        guard case .batchTrash(let trashed)? = state.treeMutation?.kind else {
            return XCTFail("reported successes must still land")
        }
        XCTAssertEqual(trashed, [item("a.md")])
        XCTAssertTrue(
            state.treeMutation?.requiresRescan ?? false,
            "failed + non-empty trashed is contradictory and forces root reconciliation")
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            "Moved 1 of 2 items to Trash, but the operation did not finish safely.")
        guard case .trash(let landed)? = state.batchStructuralResult?.payload else {
            return XCTFail("the contradictory report must remain available")
        }
        XCTAssertEqual(landed, report)
        XCTAssertTrue(state.batchStructuralResult?.requiresAttention ?? false)
    }

    func testPartialAndFailedTrashAnnouncementsPluralizeReportedCounts() async throws {
        let a = item("a.md")
        let b = item("b.md")
        let c = item("c.md")
        let bFailure = BatchItemFailure(
            item: b, stage: .trash, message: "b stayed")
        let cFailure = BatchItemFailure(
            item: c, stage: .trash, message: "c stayed")
        let scenarios: [(BatchTrashReport, String)] = [
            (
                trashReport(
                    state: .partial, planned: [a, b], trashed: [a],
                    untrashed: [BatchTrashRemainder(item: b, failure: bFailure)]),
                "Moved 1 of 2 items to Trash. 1 item was not moved."
            ),
            (
                trashReport(
                    state: .partial, planned: [a, b, c], trashed: [a],
                    untrashed: [
                        BatchTrashRemainder(item: b, failure: bFailure),
                        BatchTrashRemainder(item: c, failure: cFailure),
                    ]),
                "Moved 1 of 3 items to Trash. 2 items were not moved."
            ),
            (
                trashReport(state: .failed, planned: [a]),
                "Couldn’t move 1 item to Trash."
            ),
            (
                trashReport(state: .failed, planned: [a, b]),
                "Couldn’t move 2 items to Trash."
            ),
        ]

        for (index, scenario) in scenarios.enumerated() {
            let paths = scenario.0.envelope.planned.map(\.path)
            let (state, _) = try await makeVault(
                named: "trash-copy-\(index)", files: paths)
            state.batchTrashRunner = { _, _ in scenario.0 }
            state.structuralBatchRefreshRunner = { _ in }

            await state.batchDelete(paths.map { sel($0) }).value

            XCTAssertEqual(
                state.lastMutationAnnouncement, scenario.1,
                "\(scenario.0.state) count grammar must match the returned ledger")
        }
    }

    func testTrashNonStandingStatesRespectRefreshAndHistoryMatrix() async throws {
        let planned = [item("a.md"), item("b.md")]
        let scenarios: [(
            report: BatchTrashReport,
            refreshes: Int,
            preservesHistory: Bool,
            attention: Bool
        )] = [
            (trashReport(state: .rejected, planned: planned), 0, true, true),
            (
                trashReport(state: .rejected, planned: planned, requiresRescan: true),
                1, true, true
            ),
            (trashReport(state: .noOp, planned: planned), 0, true, false),
            (
                trashReport(state: .noOp, planned: planned, requiresRescan: true),
                1, true, false
            ),
            (trashReport(state: .failed, planned: planned), 0, true, true),
            (
                trashReport(state: .failed, planned: planned, requiresRescan: true),
                1, false, true
            ),
        ]

        for (index, scenario) in scenarios.enumerated() {
            let (state, _) = try await makeVault(
                named: "trash-matrix-\(index)",
                files: ["a.md", "b.md", "history.md", "dest/x.md"])
            let historyStanding = [
                BatchPathChange(
                    oldPath: "history.md", newPath: "dest/history.md",
                    isDirectory: false)
            ]
            let historyReport = moveReport(
                state: .succeeded,
                planned: [item("history.md")],
                opID: Int64(400 + index),
                standing: historyStanding)
            state.batchMoveRunner = { _, _ in historyReport }
            state.structuralBatchRefreshRunner = { _ in }
            await state.batchMove([sel("history.md")], to: "dest").value
            XCTAssertEqual(state.structuralUndoStack.count, 1)
            let oldTreeToken = state.treeMutation?.token

            let refresh = RefreshProbe()
            state.batchTrashRunner = { _, _ in scenario.report }
            state.structuralBatchRefreshRunner = { appState in await refresh.run(appState) }
            await state.batchDelete([sel("a.md"), sel("b.md")]).value

            XCTAssertEqual(refresh.calls, scenario.refreshes)
            XCTAssertEqual(
                state.structuralUndoStack.count,
                scenario.preservesHistory ? 1 : 0,
                "\(scenario.report.state), rescan \(scenario.report.requiresRescan)")
            XCTAssertEqual(state.batchStructuralResult?.requiresAttention, scenario.attention)
            guard case .trash(let landed)? = state.batchStructuralResult?.payload else {
                XCTFail("every state retains its report")
                continue
            }
            XCTAssertEqual(landed, scenario.report)
            if scenario.report.requiresRescan {
                XCTAssertTrue(state.treeMutation?.requiresRescan ?? false)
                XCTAssertEqual(state.treeMutation?.token, (oldTreeToken ?? 0) + 1)
            } else {
                XCTAssertEqual(state.treeMutation?.token, oldTreeToken)
            }
        }
    }

    func testContradictorySuccessfulTrashWithNoTrashedItemsFailsClosed() async throws {
        let planned = [item("a.md"), item("b.md")]
        let report = trashReport(
            state: .succeeded, planned: planned, opID: 450, trashed: [])
        let refresh = RefreshProbe()
        let (state, _) = try await makeVault(files: ["a.md", "b.md"])
        state.batchTrashRunner = { _, _ in report }
        state.structuralBatchRefreshRunner = { appState in await refresh.run(appState) }

        await state.batchDelete([sel("a.md"), sel("b.md")]).value

        XCTAssertEqual(refresh.calls, 1)
        XCTAssertTrue(state.treeMutation?.requiresRescan ?? false)
        XCTAssertTrue(state.batchStructuralResult?.requiresAttention ?? false)
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            "Trash result could not be reconciled safely.")
    }

    func testTrashInfrastructureFailureRefreshesOnceAndFailsClosed() async throws {
        let refresh = RefreshProbe()
        let (state, _) = try await makeVault(files: ["a.md", "b.md"])
        state.batchTrashRunner = { _, _ in throw BatchRunnerError.unavailable }
        state.structuralBatchRefreshRunner = { appState in await refresh.run(appState) }

        await state.batchDelete([sel("a.md"), sel("b.md")]).value

        XCTAssertEqual(refresh.calls, 1)
        XCTAssertTrue(state.treeMutation?.requiresRescan ?? false)
        XCTAssertTrue(state.structuralUndoStack.isEmpty)
        guard case let .infrastructure(operation, message)? =
            state.batchStructuralResult?.payload
        else { return XCTFail("unknown Trash outcome must remain distinct from typed states") }
        XCTAssertEqual(operation, .trash)
        XCTAssertEqual(message, "batch endpoint unavailable")
        XCTAssertTrue(state.batchStructuralResult?.requiresAttention ?? false)
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            "Trash operation failed: batch endpoint unavailable")
    }

    // MARK: - Batch admission, ownership, and staged identity (FL-03 Task 5)

    func testBatchMoveRejectsSecondSubmissionSynchronouslyAndMakesNoEarlyWrites() async throws {
        let planned = [item("a.md"), item("b.md")]
        let standing = [
            BatchPathChange(oldPath: "a.md", newPath: "dest/a.md", isDirectory: false),
            BatchPathChange(oldPath: "b.md", newPath: "dest/b.md", isDirectory: false),
        ]
        let report = moveReport(
            state: .succeeded, planned: planned, opID: 101, standing: standing)
        let gate = SuspensionGate()
        let probe = BatchRunnerProbe(
            moveReport: report,
            trashReport: trashReport(state: .noOp, planned: []))
        let (state, _) = try await makeVault(files: ["a.md", "b.md", "dest/x.md"])
        state.batchMoveRunner = { _, request in
            _ = await probe.runMove(request)
            await gate.enter()
            return report
        }
        state.structuralBatchRefreshRunner = { _ in }

        let first = try XCTUnwrap(
            state.batchMove(
                [sel("a.md"), sel("b.md")], to: "dest",
                preferredFocusPath: "b.md"))
        await gate.waitForEntrants(1)

        XCTAssertTrue(state.isMutatingStructure)
        XCTAssertNil(state.treeMutation)
        XCTAssertNil(state.batchStructuralResult)
        XCTAssertNil(state.lastMutationAnnouncement)
        XCTAssertTrue(state.structuralUndoStack.isEmpty)

        let second = state.batchMove(
            [sel("a.md"), sel("b.md")], to: "dest",
            preferredFocusPath: "a.md")
        XCTAssertNil(second, "admission is claimed before the first Task can suspend")
        let beforeReleaseCounts = await probe.callCounts()
        XCTAssertEqual(beforeReleaseCounts.move, 1)
        XCTAssertTrue(state.isMutatingStructure)

        await gate.releaseOne()
        await first.value
        XCTAssertFalse(state.isMutatingStructure)
        XCTAssertEqual(state.treeMutation?.token, 1)
        XCTAssertEqual(state.structuralUndoStack.count, 1)
    }

    func testBatchTrashRejectsSecondSubmissionSynchronouslyAndMakesNoEarlyWrites() async throws {
        let planned = [item("a.md"), item("b.md")]
        let report = trashReport(
            state: .succeeded, planned: planned, opID: 102, trashed: planned)
        let gate = SuspensionGate()
        let probe = BatchRunnerProbe(
            moveReport: moveReport(state: .noOp, planned: []),
            trashReport: report)
        let (state, _) = try await makeVault(files: ["a.md", "b.md"])
        state.batchTrashRunner = { _, request in
            _ = await probe.runTrash(request)
            await gate.enter()
            return report
        }
        state.structuralBatchRefreshRunner = { _ in }

        let first = try XCTUnwrap(
            state.batchDelete(
                [sel("a.md"), sel("b.md")], preferredFocusPath: "a.md"))
        await gate.waitForEntrants(1)
        XCTAssertTrue(state.isMutatingStructure)
        XCTAssertNil(state.treeMutation)
        XCTAssertNil(state.batchStructuralResult)
        XCTAssertNil(state.lastMutationAnnouncement)

        XCTAssertNil(
            state.batchDelete(
                [sel("a.md"), sel("b.md")], preferredFocusPath: "b.md"))
        let beforeReleaseCounts = await probe.callCounts()
        XCTAssertEqual(beforeReleaseCounts.trash, 1)

        await gate.releaseOne()
        await first.value
        XCTAssertFalse(state.isMutatingStructure)
        XCTAssertEqual(state.treeMutation?.token, 1)
        XCTAssertTrue(state.structuralUndoStack.isEmpty)
    }

    func testSuspendedBatchRunnerCannotLandAfterVaultSwitch() async throws {
        let report = moveReport(
            state: .succeeded,
            planned: [item("a.md")],
            opID: 103,
            standing: [
                BatchPathChange(oldPath: "a.md", newPath: "dest/a.md", isDirectory: false)
            ])
        let gate = SuspensionGate()
        let (state, _) = try await makeVault(named: "vault-a", files: ["a.md", "dest/x.md"])
        state.batchMoveRunner = { _, _ in
            await gate.enter()
            return report
        }
        state.structuralBatchRefreshRunner = { _ in }

        let oldTask = try XCTUnwrap(
            state.batchMove([sel("a.md")], to: "dest", preferredFocusPath: "a.md"))
        await gate.waitForEntrants(1)

        let vaultB = tempDir.appendingPathComponent("vault-b")
        try FileManager.default.createDirectory(at: vaultB, withIntermediateDirectories: true)
        try "# b\n".write(
            to: vaultB.appendingPathComponent("b.md"), atomically: true, encoding: .utf8)
        state.openVault(at: vaultB)
        await state.scanTask?.value
        let replacementSession = try XCTUnwrap(state.currentSession)

        await gate.releaseOne()
        await oldTask.value

        XCTAssertTrue(state.currentSession === replacementSession)
        XCTAssertFalse(state.isMutatingStructure)
        XCTAssertNil(state.treeMutation)
        XCTAssertNil(state.batchStructuralResult)
        XCTAssertNil(state.lastMutationAnnouncement)
        XCTAssertTrue(state.structuralUndoStack.isEmpty)
    }

    func testStaleRefreshCannotClearReplacementBatchOwnershipOrPublish() async throws {
        let oldReport = moveReport(
            state: .succeeded,
            planned: [item("a.md")],
            opID: 104,
            standing: [
                BatchPathChange(oldPath: "a.md", newPath: "dest/a.md", isDirectory: false)
            ])
        let oldRefresh = SuspensionGate()
        let (state, _) = try await makeVault(named: "vault-a", files: ["a.md", "dest/x.md"])
        state.batchMoveRunner = { _, _ in oldReport }
        state.structuralBatchRefreshRunner = { _ in await oldRefresh.enter() }
        let oldTask = try XCTUnwrap(
            state.batchMove([sel("a.md")], to: "dest", preferredFocusPath: "a.md"))
        await oldRefresh.waitForEntrants(1)

        let vaultB = tempDir.appendingPathComponent("vault-b")
        try FileManager.default.createDirectory(at: vaultB, withIntermediateDirectories: true)
        try FileManager.default.createDirectory(
            at: vaultB.appendingPathComponent("dest"), withIntermediateDirectories: true)
        try "# b\n".write(
            to: vaultB.appendingPathComponent("b.md"), atomically: true, encoding: .utf8)
        state.openVault(at: vaultB)
        await state.scanTask?.value

        let newReport = moveReport(
            state: .succeeded,
            planned: [item("b.md")],
            opID: 105,
            standing: [
                BatchPathChange(oldPath: "b.md", newPath: "dest/b.md", isDirectory: false)
            ])
        let newRunner = SuspensionGate()
        state.batchMoveRunner = { _, _ in
            await newRunner.enter()
            return newReport
        }
        state.structuralBatchRefreshRunner = { _ in }
        let newTask = try XCTUnwrap(
            state.batchMove([sel("b.md")], to: "dest", preferredFocusPath: "b.md"))
        await newRunner.waitForEntrants(1)

        await oldRefresh.releaseOne()
        await oldTask.value
        XCTAssertTrue(state.isMutatingStructure, "the stale owner cannot release the new token")
        XCTAssertNil(state.treeMutation)
        XCTAssertNil(state.batchStructuralResult)
        XCTAssertNil(state.lastMutationAnnouncement)

        await newRunner.releaseOne()
        await newTask.value
        XCTAssertFalse(state.isMutatingStructure)
        guard case let .move(report, mode)? = state.batchStructuralResult?.payload else {
            return XCTFail("replacement batch should land once")
        }
        XCTAssertEqual(report, newReport)
        XCTAssertEqual(mode, .forward(destination: "dest"))
    }

    func testStagedBatchMoveUsesUniqueIdentityAndOldCancelCannotClearNewerSheet() async throws {
        let (state, _) = try await makeVault(files: ["folder/inside.md", "dest/x.md"])
        state.requestBatchMove(
            [sel("folder", dir: true), sel("folder/inside.md")],
            preferredFocusPath: "folder/inside.md")
        let first = try XCTUnwrap(state.pendingBatchMove)
        XCTAssertEqual(first.items.count, 2, "batch origin survives projected top-level count one")
        XCTAssertEqual(first.preferredFocusPath, "folder/inside.md")
        XCTAssertEqual(first.sessionIdentity, ObjectIdentifier(try XCTUnwrap(state.currentSession)))

        state.requestBatchMove(
            [sel("folder", dir: true), sel("folder/inside.md")],
            preferredFocusPath: "folder")
        let second = try XCTUnwrap(state.pendingBatchMove)
        XCTAssertNotEqual(first.id, second.id, "identical content still gets a unique operation ID")

        state.cancelPendingBatchMove(id: first.id)
        XCTAssertEqual(state.pendingBatchMove?.id, second.id)
        XCTAssertEqual(state.pendingBatchMove?.preferredFocusPath, "folder")
    }

    func testSingleMovePresentationBindingClearsCurrentRequestOnExternalDismissal() async throws {
        let (state, _) = try await makeVault(files: ["a.md", "dest/x.md"])
        XCTAssertTrue(state.requestPendingMove(path: "a.md", isDirectory: false))
        let request = try XCTUnwrap(state.pendingMove)
        var presented: AppState.PendingMove? = request
        let presentedBinding = Binding(
            get: { presented },
            set: { presented = $0 })
        let presentation = MainSplitView.pendingMoveSheetBinding(
            appState: state,
            presented: presentedBinding)

        XCTAssertEqual(presentation.wrappedValue?.id, request.id)
        presentation.wrappedValue = nil

        XCTAssertNil(presented)
        XCTAssertNil(
            state.pendingMove,
            "Escape, system dismissal, and host-window closure must retire the presented request")
        XCTAssertNil(presentation.wrappedValue, "the dismissed sheet must not re-present")
    }

    func testSingleMovePresentationBindingCannotDismissReplacementRequest() async throws {
        let (state, _) = try await makeVault(files: ["a.md", "b.md", "dest/x.md"])
        XCTAssertTrue(state.requestPendingMove(path: "a.md", isDirectory: false))
        let first = try XCTUnwrap(state.pendingMove)
        var presented: AppState.PendingMove? = first
        let presentedBinding = Binding(
            get: { presented },
            set: { presented = $0 })
        _ = MainSplitView.pendingMoveSheetBinding(
            appState: state,
            presented: presentedBinding)

        XCTAssertTrue(state.requestPendingMove(path: "b.md", isDirectory: false))
        let replacement = try XCTUnwrap(state.pendingMove)
        XCTAssertNotEqual(first.id, replacement.id)
        // Model SwiftUI rebuilding the sheet modifier after AppState publishes
        // B while sheet A remains visible. Dismissal must still read the frozen
        // item owner, not capture B from the newly constructed modifier.
        let rebuiltPresentation = MainSplitView.pendingMoveSheetBinding(
            appState: state,
            presented: presentedBinding)
        rebuiltPresentation.wrappedValue = nil

        XCTAssertNil(presented)
        XCTAssertEqual(
            state.pendingMove?.id,
            replacement.id,
            "a late dismissal callback from the old sheet must not erase its replacement")
        MainSplitView.promotePendingMoveAfterDismissal(
            appState: state,
            presented: presentedBinding)
        XCTAssertEqual(
            presented?.id,
            replacement.id,
            "onDismiss must promote the preserved replacement after sheet A closes")
    }

    func testBatchMovePresentationBindingClearsCurrentRequestOnExternalDismissal() async throws {
        let (state, _) = try await makeVault(files: ["a.md", "b.md", "dest/x.md"])
        XCTAssertTrue(
            state.requestBatchMove(
                [sel("a.md"), sel("b.md")],
                preferredFocusPath: "a.md"))
        let request = try XCTUnwrap(state.pendingBatchMove)
        var presented: AppState.BatchMove? = request
        let presentedBinding = Binding(
            get: { presented },
            set: { presented = $0 })
        let presentation = MainSplitView.pendingBatchMoveSheetBinding(
            appState: state,
            presented: presentedBinding)

        XCTAssertEqual(presentation.wrappedValue?.id, request.id)
        presentation.wrappedValue = nil

        XCTAssertNil(presented)
        XCTAssertNil(
            state.pendingBatchMove,
            "Escape, system dismissal, and host-window closure must retire the presented batch")
        XCTAssertNil(presentation.wrappedValue, "the dismissed batch sheet must not re-present")
    }

    func testBatchMovePresentationBindingCannotDismissReplacementRequest() async throws {
        let (state, _) = try await makeVault(files: ["a.md", "b.md", "dest/x.md"])
        XCTAssertTrue(
            state.requestBatchMove(
                [sel("a.md"), sel("b.md")],
                preferredFocusPath: "a.md"))
        let first = try XCTUnwrap(state.pendingBatchMove)
        var presented: AppState.BatchMove? = first
        let presentedBinding = Binding(
            get: { presented },
            set: { presented = $0 })
        _ = MainSplitView.pendingBatchMoveSheetBinding(
            appState: state,
            presented: presentedBinding)

        XCTAssertTrue(
            state.requestBatchMove(
                [sel("a.md"), sel("b.md")],
                preferredFocusPath: "b.md"))
        let replacement = try XCTUnwrap(state.pendingBatchMove)
        XCTAssertNotEqual(first.id, replacement.id)
        let rebuiltPresentation = MainSplitView.pendingBatchMoveSheetBinding(
            appState: state,
            presented: presentedBinding)
        rebuiltPresentation.wrappedValue = nil

        XCTAssertNil(presented)
        XCTAssertEqual(
            state.pendingBatchMove?.id,
            replacement.id,
            "a late dismissal callback from the old batch sheet must not erase its replacement")
        MainSplitView.promotePendingBatchMoveAfterDismissal(
            appState: state,
            presented: presentedBinding)
        XCTAssertEqual(
            presented?.id,
            replacement.id,
            "onDismiss must promote the preserved replacement batch after sheet A closes")
    }

    func testSameURLReopenRejectsStaleStagedBatchMoveCommitWithoutRunnerCall() async throws {
        let report = moveReport(state: .noOp, planned: [item("a.md")])
        let probe = BatchRunnerProbe(
            moveReport: report,
            trashReport: trashReport(state: .noOp, planned: []))
        let (state, vault) = try await makeVault(files: ["a.md", "dest/x.md"])
        state.batchMoveRunner = { _, request in await probe.runMove(request) }
        state.requestBatchMove([sel("a.md")], preferredFocusPath: "a.md")
        let staged = try XCTUnwrap(state.pendingBatchMove)

        state.openVault(at: vault)
        await state.scanTask?.value
        XCTAssertFalse(state.commitPendingBatchMove(id: staged.id, to: "dest"))

        let counts = await probe.callCounts()
        XCTAssertEqual(counts.move, 0)
        XCTAssertNil(state.pendingBatchMove)
        XCTAssertNil(state.batchStructuralResult)
        XCTAssertNil(state.lastMutationAnnouncement)
    }

    /// Native batch Trash also preflights the whole projected action. A missing
    /// input is an authoritative rejection: the remaining file stays put and
    /// no successful Trash/history claim is emitted.
    func testBatchDeleteCountsOnlySuccessesOnPartialFailure() async throws {
        let (state, vault) = try await makeVault(files: ["a.md", "b.md"])
        // a.md is gone before the batch runs, so b.md must not be trashed.
        try FileManager.default.removeItem(at: vault.appendingPathComponent("a.md"))
        await state.batchDelete([sel("a.md"), sel("b.md")]).value
        XCTAssertTrue(exists(vault, "b.md"), "preflight rejection writes nothing")
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            "Couldn’t start moving the selected items to Trash.")
        XCTAssertTrue(state.structuralUndoStack.isEmpty)
        XCTAssertTrue(state.structuralRedoStack.isEmpty)
        guard case let .trash(report)? = state.batchStructuralResult?.payload else {
            return XCTFail("the typed native Trash rejection must be retained")
        }
        XCTAssertEqual(report.state, .rejected)
        XCTAssertTrue(report.trashed.isEmpty)
        XCTAssertTrue(state.batchStructuralResult?.requiresAttention ?? false)
    }

    /// FL04-A replaces count-derived batch grammar with stable catalog labels;
    /// the exact frozen intent, rather than a label-local count, identifies the
    /// complete acted-on selection.
    func testBatchMenuPluralizesCountByInspection() throws {
        let owner = NSObject()
        let snapshot = SidebarSelectionSnapshot(
            sessionIdentity: ObjectIdentifier(owner),
            items: [
                SidebarSelectionItem(
                    path: "a.md", isDirectory: false, isMarkdown: true),
                SidebarSelectionItem(
                    path: "folder", isDirectory: true, isMarkdown: false),
            ],
            focusedPath: "folder",
            creationParent: "folder")
        let evaluations = SidebarActionCatalog.project(
            surface: .contextMenu, snapshot: snapshot)
        let move = try XCTUnwrap(
            evaluations.first(where: { $0.id == SlateCommandID.moveTo }))
        let trash = try XCTUnwrap(
            evaluations.first(where: { $0.id == SlateCommandID.deleteEntry }))

        XCTAssertEqual(move.label, "Move To…")
        XCTAssertEqual(trash.label, "Move to Trash")
        XCTAssertEqual(move.intent?.snapshot, snapshot)
        XCTAssertEqual(trash.intent?.snapshot, snapshot)
        XCTAssertFalse(
            evaluations.contains(where: { $0.label.contains("2") }),
            "stable catalog labels cannot drift into contradictory item-count grammar")
    }

    // MARK: - requestBatchDelete confirmation gate (#860 at batch scope)

    func testC5BatchDeleteDeclaresOneAsyncWholeSelectionProbeSeamByInspection() throws {
        let src = try Self.normalizedSource("AppState.swift")

        XCTAssertTrue(
            src.contains(
                "typealias BatchDeleteConfirmationProbeRunner = @Sendable (URL, [String]) async -> Int"),
            "batch confirmation probing needs one injectable whole-selection async seam")
        XCTAssertTrue(
            src.contains("var batchDeleteConfirmationProbeRunner:"),
            "AppState must own the production/test runner")
    }

    func testC5BatchDeleteProbeIgnoresOnlyDSStoreAndCountsHiddenEntries() async throws {
        let (state, vault) = try await makeVault(
            files: ["ds-only/.DS_Store", "hidden/.env"])

        let count = await state.batchDeleteConfirmationProbeRunner(
            vault, ["ds-only", "hidden"])

        XCTAssertEqual(
            count, 1,
            ".DS_Store-only is empty, while another hidden entry requires confirmation")
    }

    func testC5BatchDeleteProbeFailsClosedWhenFolderCannotBeEnumerated() async throws {
        let (state, vault) = try await makeVault(files: ["a.md"])

        let count = await state.batchDeleteConfirmationProbeRunner(
            vault, ["missing-folder"])

        XCTAssertEqual(count, 1, "unknown folder contents must require confirmation")
    }

    func testC5BatchDeleteProbeFailsClosedWhenRegularFileIsClaimedAsFolder() async throws {
        let (state, vault) = try await makeVault(files: ["a.md"])

        let count = await state.batchDeleteConfirmationProbeRunner(
            vault, ["missing-folder", "a.md"])

        XCTAssertEqual(
            count, 2,
            "both a missing path and an existing non-directory must require confirmation")
    }

    func testC5BatchDeleteRequestProbesAllFoldersOnceAndStagesCapturedConfirmation()
        async throws
    {
        let (state, vault) = try await makeVault(
            files: ["a.md", "folder/x.md", "empty/.DS_Store"])
        let probe = BatchDeleteConfirmationProbe(nonEmptyFolderCount: 1)
        state.batchDeleteConfirmationProbeRunner = { vaultURL, folderPaths in
            await probe.run(vaultURL: vaultURL, folderPaths: folderPaths)
        }
        let items = [sel("a.md"), sel("folder", dir: true), sel("empty", dir: true)]

        XCTAssertTrue(
            state.requestBatchDelete(items, preferredFocusPath: "empty"),
            "the captured probe request is admitted synchronously")
        let task = try XCTUnwrap(state.pendingStructuralTaskForTesting)
        await task.value

        let probeCallCount = await probe.callCount()
        let probedRoot = await probe.lastRoot()
        let probedFolderPaths = await probe.lastFolderPaths()
        XCTAssertEqual(probeCallCount, 1)
        XCTAssertEqual(probedRoot, vault)
        XCTAssertEqual(probedFolderPaths, ["folder", "empty"])
        let pending = try XCTUnwrap(state.pendingBatchDelete)
        XCTAssertEqual(pending.items, items)
        XCTAssertEqual(pending.preferredFocusPath, "empty")
        XCTAssertEqual(pending.nonEmptyFolderCount, 1)
        XCTAssertFalse(state.isMutatingStructure, "staging completes before ownership releases")
    }

    func testC5BatchDeleteAllEmptyProbeRunsExactlyOneNativeTrashAndLandsItsReport()
        async throws
    {
        let planned = [item("a.md"), item("empty", dir: true)]
        let report = trashReport(state: .noOp, planned: planned)
        let trashProbe = BatchRunnerProbe(
            moveReport: moveReport(state: .noOp, planned: []),
            trashReport: report)
        let (state, _) = try await makeVault(files: ["a.md", "empty/.DS_Store"])
        state.batchDeleteConfirmationProbeRunner = { _, _ in 0 }
        state.batchTrashRunner = { _, request in await trashProbe.runTrash(request) }
        state.structuralBatchRefreshRunner = { _ in }

        XCTAssertTrue(
            state.requestBatchDelete(
                [sel("a.md"), sel("empty", dir: true)],
                preferredFocusPath: "empty"))
        await state.pendingStructuralTaskForTesting?.value

        let callCounts = await trashProbe.callCounts()
        XCTAssertEqual(callCounts.trash, 1, "the captured selection reaches core exactly once")
        XCTAssertNil(state.pendingBatchDelete)
        guard case let .trash(landedReport)? = state.batchStructuralResult?.payload else {
            return XCTFail("the one native report must land through the standard matrix")
        }
        XCTAssertEqual(landedReport, report)
        XCTAssertFalse(state.isMutatingStructure)
    }

    func testC5BatchDeleteProbeClaimsOwnershipSynchronouslyAndRejectsSecondSubmission()
        async throws
    {
        let gate = SuspensionGate()
        let probe = BatchDeleteConfirmationProbe(nonEmptyFolderCount: 1, gate: gate)
        let (state, _) = try await makeVault(files: ["a.md", "folder/x.md"])
        state.batchDeleteConfirmationProbeRunner = { vaultURL, folderPaths in
            await probe.run(vaultURL: vaultURL, folderPaths: folderPaths)
        }
        let items = [sel("a.md"), sel("folder", dir: true)]

        XCTAssertTrue(state.requestBatchDelete(items, preferredFocusPath: "folder"))
        let firstTask = try XCTUnwrap(state.pendingStructuralTaskForTesting)
        XCTAssertTrue(state.isMutatingStructure, "ownership is reserved before Task suspension")
        XCTAssertFalse(
            state.requestBatchDelete(items, preferredFocusPath: "a.md"),
            "the second submission rejects synchronously")
        XCTAssertEqual(state.lastMutationAnnouncement, AppState.structuralMutationBusyReason)

        await gate.waitForEntrants(1)
        let probeCalls = await probe.callCount()
        XCTAssertEqual(probeCalls, 1)
        await gate.releaseOne()
        await firstTask.value

        XCTAssertEqual(state.pendingBatchDelete?.preferredFocusPath, "folder")
        XCTAssertFalse(state.isMutatingStructure)
    }

    func testC5BatchDeleteAllEmptyProbeKeepsOwnershipThroughNativeTrash()
        async throws
    {
        let probeGate = SuspensionGate()
        let trashGate = SuspensionGate()
        let confirmationProbe = BatchDeleteConfirmationProbe(
            nonEmptyFolderCount: 0, gate: probeGate)
        let planned = [item("a.md"), item("empty", dir: true)]
        let report = trashReport(state: .noOp, planned: planned)
        let trashProbe = BatchRunnerProbe(
            moveReport: moveReport(state: .noOp, planned: []),
            trashReport: report)
        let (state, _) = try await makeVault(files: ["a.md", "empty/.DS_Store"])
        state.batchDeleteConfirmationProbeRunner = { vaultURL, folderPaths in
            await confirmationProbe.run(vaultURL: vaultURL, folderPaths: folderPaths)
        }
        state.batchTrashRunner = { _, request in
            let result = await trashProbe.runTrash(request)
            await trashGate.enter()
            return result
        }
        state.structuralBatchRefreshRunner = { _ in }
        let items = [sel("a.md"), sel("empty", dir: true)]

        XCTAssertTrue(state.requestBatchDelete(items, preferredFocusPath: "empty"))
        let requestTask = try XCTUnwrap(state.pendingStructuralTaskForTesting)
        await probeGate.waitForEntrants(1)
        XCTAssertTrue(state.isMutatingStructure)

        await probeGate.releaseOne()
        await trashGate.waitForEntrants(1)
        XCTAssertTrue(
            state.isMutatingStructure,
            "the probe transfers its token without a false-idle publication")
        XCTAssertNil(state.pendingBatchDelete)
        let nativeCalls = await trashProbe.callCounts()
        XCTAssertEqual(nativeCalls.trash, 1)
        XCTAssertFalse(state.requestBatchDelete(items, preferredFocusPath: "a.md"))
        XCTAssertEqual(state.lastMutationAnnouncement, AppState.structuralMutationBusyReason)

        await trashGate.releaseOne()
        await requestTask.value
        XCTAssertFalse(state.isMutatingStructure)
    }

    func testC5SuspendedBatchDeleteProbeIsInertAfterVaultSwitch() async throws {
        let probeGate = SuspensionGate()
        let confirmationProbe = BatchDeleteConfirmationProbe(
            nonEmptyFolderCount: 1, gate: probeGate)
        let trashProbe = BatchRunnerProbe(
            moveReport: moveReport(state: .noOp, planned: []),
            trashReport: trashReport(state: .noOp, planned: []))
        let (state, _) = try await makeVault(
            named: "vault-a", files: ["a.md", "folder/x.md"])
        state.batchDeleteConfirmationProbeRunner = { vaultURL, folderPaths in
            await confirmationProbe.run(vaultURL: vaultURL, folderPaths: folderPaths)
        }
        state.batchTrashRunner = { _, request in await trashProbe.runTrash(request) }

        XCTAssertTrue(
            state.requestBatchDelete(
                [sel("a.md"), sel("folder", dir: true)],
                preferredFocusPath: "folder"))
        let oldTask = try XCTUnwrap(state.pendingStructuralTaskForTesting)
        await probeGate.waitForEntrants(1)

        let vaultB = tempDir.appendingPathComponent("vault-b")
        try FileManager.default.createDirectory(at: vaultB, withIntermediateDirectories: true)
        try "# b\n".write(
            to: vaultB.appendingPathComponent("b.md"), atomically: true, encoding: .utf8)
        state.openVault(at: vaultB)
        await state.scanTask?.value
        let replacementSession = try XCTUnwrap(state.currentSession)

        await probeGate.releaseOne()
        await oldTask.value

        XCTAssertTrue(state.currentSession === replacementSession)
        XCTAssertEqual(state.currentVaultURL, vaultB)
        XCTAssertFalse(state.isMutatingStructure)
        XCTAssertNil(state.pendingBatchDelete)
        XCTAssertNil(state.batchStructuralResult)
        XCTAssertNil(state.treeMutation)
        XCTAssertNil(state.lastMutationAnnouncement)
        let nativeCalls = await trashProbe.callCounts()
        XCTAssertEqual(nativeCalls.trash, 0)
    }

    func testC5StaleBatchDeleteProbeCannotReleaseReplacementVaultOwner() async throws {
        let oldProbeGate = SuspensionGate()
        let confirmationProbe = BatchDeleteConfirmationProbe(
            nonEmptyFolderCount: 1, gate: oldProbeGate)
        let trashProbe = BatchRunnerProbe(
            moveReport: moveReport(state: .noOp, planned: []),
            trashReport: trashReport(state: .noOp, planned: []))
        let (state, _) = try await makeVault(
            named: "vault-a", files: ["a.md", "folder/x.md"])
        state.batchDeleteConfirmationProbeRunner = { vaultURL, folderPaths in
            await confirmationProbe.run(vaultURL: vaultURL, folderPaths: folderPaths)
        }
        state.batchTrashRunner = { _, request in await trashProbe.runTrash(request) }

        XCTAssertTrue(
            state.requestBatchDelete(
                [sel("a.md"), sel("folder", dir: true)],
                preferredFocusPath: "folder"))
        let oldTask = try XCTUnwrap(state.pendingStructuralTaskForTesting)
        await oldProbeGate.waitForEntrants(1)

        let vaultB = tempDir.appendingPathComponent("vault-b")
        try FileManager.default.createDirectory(at: vaultB, withIntermediateDirectories: true)
        try FileManager.default.createDirectory(
            at: vaultB.appendingPathComponent("dest"), withIntermediateDirectories: true)
        try "# b\n".write(
            to: vaultB.appendingPathComponent("b.md"), atomically: true, encoding: .utf8)
        state.openVault(at: vaultB)
        await state.scanTask?.value

        let replacementReport = moveReport(
            state: .succeeded,
            planned: [item("b.md")],
            opID: 106,
            standing: [
                BatchPathChange(oldPath: "b.md", newPath: "dest/b.md", isDirectory: false)
            ])
        let replacementGate = SuspensionGate()
        state.batchMoveRunner = { _, _ in
            await replacementGate.enter()
            return replacementReport
        }
        state.structuralBatchRefreshRunner = { _ in }
        let replacementTask = try XCTUnwrap(
            state.batchMove(
                [sel("b.md")], to: "dest", preferredFocusPath: "b.md"))
        await replacementGate.waitForEntrants(1)

        await oldProbeGate.releaseOne()
        await oldTask.value

        XCTAssertTrue(
            state.isMutatingStructure,
            "the stale probe cannot release the replacement vault's ownership")
        XCTAssertNil(state.pendingBatchDelete)
        XCTAssertNil(state.batchStructuralResult)
        XCTAssertNil(state.treeMutation)
        XCTAssertNil(state.lastMutationAnnouncement)
        let nativeCalls = await trashProbe.callCounts()
        XCTAssertEqual(nativeCalls.trash, 0)

        await replacementGate.releaseOne()
        await replacementTask.value

        XCTAssertFalse(state.isMutatingStructure)
        guard case let .move(report, mode)? = state.batchStructuralResult?.payload else {
            return XCTFail("the replacement owner should land after its explicit release")
        }
        XCTAssertEqual(report, replacementReport)
        XCTAssertEqual(mode, .forward(destination: "dest"))
    }

    func testRequestBatchDeleteStagesConfirmationWhenNonEmptyFolderIncluded() async throws {
        let (state, _) = try await makeVault(files: ["a.md", "folder/x.md"])
        state.requestBatchDelete([sel("a.md"), sel("folder", dir: true)])
        await state.pendingStructuralTaskForTesting?.value
        let pending = try XCTUnwrap(state.pendingBatchDelete, "a non-empty folder must confirm")
        XCTAssertEqual(pending.itemCount, 2)
        XCTAssertEqual(pending.nonEmptyFolderCount, 1)
    }

    func testRequestBatchDeleteAllFilesTrashesStraightThrough() async throws {
        let (state, vault) = try await makeVault(files: ["a.md", "b.md"])
        state.requestBatchDelete([sel("a.md"), sel("b.md")])
        XCTAssertNil(state.pendingBatchDelete, "an all-files batch does not confirm")
        await state.pendingStructuralTaskForTesting?.value
        XCTAssertFalse(exists(vault, "a.md"))
        XCTAssertFalse(exists(vault, "b.md"))
        XCTAssertEqual(state.lastMutationAnnouncement, "Moved 2 items to Trash.")
    }

    /// A single-item "batch" routes through the SINGLE #860 funnel so it gets
    /// the exact single-folder confirmation copy.
    func testRequestBatchDeleteSingleItemUsesSingleFunnel() async throws {
        let (state, _) = try await makeVault(files: ["folder/x.md", "a.md"])
        let probe = BatchDeleteConfirmationProbe(nonEmptyFolderCount: 0)
        state.batchDeleteConfirmationProbeRunner = { vaultURL, folderPaths in
            await probe.run(vaultURL: vaultURL, folderPaths: folderPaths)
        }
        state.requestBatchDelete([sel("folder", dir: true)])
        let probeCalls = await probe.callCount()
        XCTAssertNil(state.pendingBatchDelete)
        XCTAssertEqual(probeCalls, 0, "one item never enters the batch probe")
        XCTAssertEqual(
            state.pendingFolderDelete?.path, "folder",
            "one item routes to the single-node #860 confirmation")
    }

    func testConfirmPendingBatchDeleteTrashesTheStagedItems() async throws {
        let (state, vault) = try await makeVault(files: ["a.md", "folder/x.md"])
        state.requestBatchDelete([sel("a.md"), sel("folder", dir: true)])
        await state.pendingStructuralTaskForTesting?.value
        let pending = try XCTUnwrap(state.pendingBatchDelete)
        state.confirmPendingBatchDelete(id: pending.id)
        XCTAssertNil(state.pendingBatchDelete, "confirming clears the staged batch")
        await state.pendingStructuralTaskForTesting?.value
        XCTAssertFalse(exists(vault, "a.md"))
        XCTAssertFalse(exists(vault, "folder"))
        XCTAssertEqual(state.lastMutationAnnouncement, "Moved 2 items to Trash.")
    }

    func testCancelPendingBatchDeleteDeletesNothing() async throws {
        let (state, vault) = try await makeVault(files: ["a.md", "folder/x.md"])
        state.requestBatchDelete([sel("a.md"), sel("folder", dir: true)])
        await state.pendingStructuralTaskForTesting?.value
        let pending = try XCTUnwrap(state.pendingBatchDelete)
        state.cancelPendingBatchDelete(id: pending.id)
        XCTAssertNil(state.pendingBatchDelete)
        XCTAssertTrue(exists(vault, "a.md"), "cancel trashes nothing")
        XCTAssertTrue(exists(vault, "folder/x.md"))
    }

    /// The single-node move path (U2-5) is untouched — `moveEntry` still fires
    /// its own per-item announcement, so single-select behavior is unchanged.
    func testSingleMoveStillAnnouncesPerItem() async throws {
        let (state, vault) = try await makeVault(files: ["a.md", "dest/x.md"])
        await state.moveEntry(path: "a.md", isDirectory: false, to: "dest")?.value
        XCTAssertTrue(exists(vault, "dest/a.md"))
        XCTAssertEqual(state.lastMutationAnnouncement, "Moved a.md to dest.")
    }

    // MARK: - Cross-vault safety

    func testBatchPendingFieldsClearOnVaultClose() async throws {
        let (state, _) = try await makeVault(files: ["a.md", "folder/x.md"])
        state.requestBatchMove([sel("a.md")], preferredFocusPath: "a.md")
        state.requestBatchDelete([sel("a.md"), sel("folder", dir: true)])
        await state.pendingStructuralTaskForTesting?.value
        XCTAssertNotNil(state.pendingBatchDelete)
        state.closeVault()
        XCTAssertNil(state.pendingBatchMove, "batch sheet dies with the vault")
        XCTAssertNil(state.pendingBatchDelete, "batch confirmation dies with the vault")
    }

    // MARK: - View wiring (source inspection)

    /// The delicate #643 + #852 click dispatch: BOTH AppKit row bridges retain
    /// the mouse-down modifiers and fork plain → the shared pointer/AX
    /// `activate` closure vs ⌘/⇧ → `applyMultiSelectClick`. The bridge owns
    /// click-versus-drag so a drag can capture the full selection before
    /// SwiftUI collapses it.
    func testTapDispatchReadsModifiersAndForksByInspection() throws {
        let src = try Self.normalizedSidebarSource()
        XCTAssertEqual(
            src.components(
                separatedBy: "let click = Self.selectionClick(from: modifiers)"
            ).count - 1,
            2,
            "both bridges must use the modifiers captured at mouse-down")
        XCTAssertEqual(
            src.components(
                separatedBy: "if click == .plain { activate() } else { "
                    + "fileTreeFocused = true "
                    + "applyMultiSelectClick(.node(node.nodeID), click: click) }"
            ).count - 1,
            2,
            "plain uses the shared single-select activation; ⌘/⇧ forks to multi")
    }

    /// A multi gesture SUPPRESSES the open: `applyMultiSelectClick` raises the
    /// one-shot suppress flag BEFORE moving `listSelection`, and the
    /// `.onChange(of: listSelection)` consumes it and returns before the open —
    /// so a live ⌘ can't mis-route the focus move to a new tab (#852 point 2/4).
    func testMultiSelectSuppressesOpenByInspection() throws {
        let src = try Self.normalizedSidebarSource()
        // #852 red-team: the one-shot must be armed ONLY when the focus
        // assignment will actually fire .onChange — a same-value assignment is a
        // no-op that would strand the flag true (swallowing a later open AND
        // leaving multiSelection stale for a wrong-target ⌘⌫). The guard must
        // be present, immediately before the focus move.
        XCTAssertTrue(
            src.contains(
                "if listSelection != selectionModel.focused { "
                    + "suppressOpenForSelectionChange = true } "
                    + "listSelection = selectionModel.focused"),
            "suppress is armed conditionally (only when focus changes), then focus moves")
        XCTAssertTrue(
            src.contains(
                "if suppressOpenForSelectionChange { suppressOpenForSelectionChange = false "
                    + "suppressOpenForPostMutationFocus = false "
                    + "mirrorTreeSelectionToAppState(selectionModel.focused) "
                    + "announceFocusedFileSelection( selectionModel.focused, "
                    + "suppressed: announcementIsSuppressed) return }"),
            "the onChange path clears both one-shots, speaks focus, and returns before open")
        XCTAssertFalse(
            src.contains(
                "if count == 1, case let .node(.file(path)) = selectionModel.focused {"),
            "a modifier transition never opens, even when it collapses to one row")
    }

    /// A context-menu row inside the published selection keeps the complete
    /// batch; a row outside it gets a one-row frozen target. The shared catalog
    /// then derives the applicable action set without a view-local branch.
    func testContextMenuBranchesToBatchByInspection() throws {
        let owner = NSObject()
        let first = SidebarSelectionItem(
            path: "a.md", isDirectory: false, isMarkdown: true)
        let folder = SidebarSelectionItem(
            path: "folder", isDirectory: true, isMarkdown: false)
        let published = SidebarSelectionSnapshot(
            sessionIdentity: ObjectIdentifier(owner),
            items: [first, folder],
            focusedPath: first.path,
            creationParent: "")
        let inside = FileTreeSidebar.sidebarRowActionProjection(
            surface: .contextMenu,
            row: folder,
            publishedSnapshot: published,
            structuralMutationDisabledReason: nil,
            actionDisabledReasons: [:])
        XCTAssertEqual(inside.targetSnapshot, published)
        XCTAssertEqual(
            inside.evaluations.map(\.id),
            [SlateCommandID.moveTo, SlateCommandID.deleteEntry])

        let outside = SidebarSelectionItem(
            path: "other.md", isDirectory: false, isMarkdown: true)
        let single = FileTreeSidebar.sidebarRowActionProjection(
            surface: .contextMenu,
            row: outside,
            publishedSnapshot: published,
            structuralMutationDisabledReason: nil,
            actionDisabledReasons: [:])
        XCTAssertEqual(single.targetSnapshot.items, [outside])
        XCTAssertEqual(single.targetSnapshot.focusedPath, outside.path)

        let src = try Self.normalizedSidebarSource()
        func body(from start: String, to end: String) throws -> String {
            let startRange = try XCTUnwrap(src.range(of: start), start)
            let tail = src[startRange.lowerBound...]
            let endRange = try XCTUnwrap(tail.range(of: end), end)
            return String(tail[..<endRange.lowerBound])
        }
        let folderOwner = try body(
            from: "private func folderRow(_ node: TreeNode)",
            to: "private func fileRow(_ node: TreeNode)")
        let fileOwner = try body(
            from: "private func fileRow(_ node: TreeNode)",
            to: "private func renameOwner(for node: TreeNode)")
        for owner in [folderOwner, fileOwner] {
            XCTAssertTrue(owner.contains(".contextMenu"))
            XCTAssertTrue(owner.contains("surface: .contextMenu"))
            XCTAssertTrue(owner.contains("row: sidebarSelectionItem(for: node)"))
            XCTAssertTrue(owner.contains("sidebarCatalogActions(projection.evaluations)"))
            XCTAssertFalse(owner.contains("batchManagementMenu("))
            XCTAssertFalse(owner.contains("appState.requestBatchMove("))
            XCTAssertFalse(owner.contains("appState.requestBatchDelete("))
        }
        XCTAssertTrue(
            fileOwner.contains("if projection.targetSnapshot.items.count == 1"),
            "single-file placement actions must not leak into a batch context menu")
    }

    /// The ⌘⌫ chord routes the WHOLE multi-selection to the batch delete funnel
    /// (Move to Trash is a batch action) while single-item commands stay on the
    /// focus. Both delete handlers call the shared helper.
    func testKeyboardDeleteRoutesToBatchWhenMultiSelectedByInspection() throws {
        let src = try Self.normalizedSidebarSource()
        XCTAssertEqual(
            src.components(separatedBy: "requestDeleteFromKeyboard()").count - 1,
            3,
            "both ⌘⌫ handlers plus the shared helper must remain wired")
        let start = try XCTUnwrap(
            src.range(of: "private func requestDeleteFromKeyboard()"))
        let tail = src[start.lowerBound...]
        let end = try XCTUnwrap(
            tail.range(of: "private func applyMultiSelectClick("))
        let helper = String(tail[..<end.lowerBound])
        XCTAssertTrue(helper.contains("appState.sidebarActionProjection(surface: .keyboard)"))
        XCTAssertTrue(helper.contains("id: SlateCommandID.deleteEntry"))
        XCTAssertTrue(helper.contains("appState.dispatchSidebarAction(intent)"))
        XCTAssertTrue(helper.contains("appState.postMutationAnnouncement("))
        XCTAssertFalse(helper.contains("appState.requestBatchDelete("))
        XCTAssertFalse(helper.contains("appState.requestDeleteEntry("))

        let owner = NSObject()
        let snapshot = SidebarSelectionSnapshot(
            sessionIdentity: ObjectIdentifier(owner),
            items: [
                SidebarSelectionItem(
                    path: "a.md", isDirectory: false, isMarkdown: true),
                SidebarSelectionItem(
                    path: "folder", isDirectory: true, isMarkdown: false),
            ],
            focusedPath: "folder",
            creationParent: "folder")
        let delete = try XCTUnwrap(
            SidebarActionCatalog.project(surface: .keyboard, snapshot: snapshot)
                .first(where: { $0.id == SlateCommandID.deleteEntry }))
        XCTAssertEqual(
            delete.intent?.snapshot,
            snapshot,
            "keyboard Trash must freeze the complete selection and preferred focus")
    }

    /// The batch Move-to-folder sheet and the batch-delete confirmation are
    /// wired into MainSplitView.
    func testMainSplitViewWiresBatchSurfacesByInspection() throws {
        let src = try Self.normalizedSource("MainSplitView.swift")
        XCTAssertTrue(
            src.contains("MoveToFolderSheet(batch: $0)"),
            "the batch move sheet renders the frozen item owned by its item binding")
        XCTAssertTrue(
            src.contains("appState.confirmPendingBatchDelete(id: pending.id)"),
            "the batch-delete alert confirms through the funnel")
    }

    // MARK: - Codex finding 4: batchTargets is ordered + pruned + path-validated

    private func vfile(_ path: String) -> (rowID: FileTreeSidebar.RowID, path: String, isDirectory: Bool) {
        (rowID: .node(.file(path: path)), path: path, isDirectory: false)
    }
    private func vdir(_ path: String, id: Int64) -> (rowID: FileTreeSidebar.RowID, path: String, isDirectory: Bool) {
        (rowID: .node(.dir(id)), path: path, isDirectory: true)
    }
    /// A path snapshot in which every visible row's snapshot == its current path
    /// (i.e. nothing repointed) — the common case for the ordering/dedup tests.
    private func snap(_ order: [(rowID: FileTreeSidebar.RowID, path: String, isDirectory: Bool)])
        -> [FileTreeSidebar.RowID: String] {
        Dictionary(uniqueKeysWithValues: order.map { ($0.rowID, $0.path) })
    }

    /// The batch targets follow the VISIBLE row order, not `Set` iteration order
    /// (which is nondeterministic) — otherwise a multi-folder move into an empty
    /// dest picks an arbitrary basename-collision winner.
    func testBatchTargetsAreInVisibleRowOrderNotSetOrder() {
        let order = [vfile("z.md"), vfile("a.md"), vfile("m.md")]
        let selection: Set<FileTreeSidebar.RowID> = [
            .node(.file(path: "z.md")), .node(.file(path: "a.md")), .node(.file(path: "m.md")),
        ]
        XCTAssertEqual(
            FileTreeSidebar.batchTargets(
                visibleOrder: order, selection: selection, snapshot: snap(order)).map(\.path),
            ["z.md", "a.md", "m.md"], "targets preserve the visible order, not sorted/Set order")
    }

    /// A selected row that was collapsed / deleted away (a stale id absent from
    /// the live visible order) is PRUNED — so the announced count and the menu
    /// label match what actually acts.
    func testBatchTargetsPruneStaleIdsAbsentFromVisibleOrder() {
        let order = [vfile("a.md"), vfile("b.md")]
        let selection: Set<FileTreeSidebar.RowID> = [
            .node(.file(path: "a.md")), .node(.file(path: "b.md")),
            .node(.file(path: "gone.md")),  // selected earlier, now collapsed away
        ]
        let targets = FileTreeSidebar.batchTargets(
            visibleOrder: order, selection: selection, snapshot: snap(order))
        XCTAssertEqual(targets.map(\.path), ["a.md", "b.md"], "the stale id is dropped")
    }

    /// A folder AND a descendant both selected collapse to just the folder
    /// (topLevelSelection) for the OPERATION — while the PRE-dedup pruned count
    /// (mode / announcement) still sees both rows (Codex finding 3).
    func testBatchTargetsDedupWhilePrunedCountSeesBothRows() {
        let order = [vdir("folder", id: 1), vfile("folder/x.md"), vfile("other.md")]
        let selection: Set<FileTreeSidebar.RowID> = [
            .node(.dir(1)), .node(.file(path: "folder/x.md")), .node(.file(path: "other.md")),
        ]
        // Operation targets: deduped (folder swallows its child).
        XCTAssertEqual(
            FileTreeSidebar.batchTargets(
                visibleOrder: order, selection: selection, snapshot: snap(order)).map(\.path),
            ["folder", "other.md"], "descendant deduped; folder + sibling kept in order")
        // Mode / announcement: PRE-dedup — all three visible rows count.
        XCTAssertEqual(
            FileTreeSidebar.prunedSelection(
                visibleOrder: order, selection: selection, snapshot: snap(order)).count,
            3, "the pre-dedup pruned count sees every visible selected row")
    }

    /// Codex finding 3 in miniature: a folder + a child inside it are TWO visible
    /// selected rows (multi mode), yet the OPERATION targets just [folder].
    func testFolderPlusChildIsMultiModeButOperatesOnFolderOnly() {
        let order = [vdir("F", id: 7), vfile("F/note.md")]
        let selection: Set<FileTreeSidebar.RowID> = [.node(.dir(7)), .node(.file(path: "F/note.md"))]
        let pruned = FileTreeSidebar.prunedSelection(
            visibleOrder: order, selection: selection, snapshot: snap(order))
        let targets = FileTreeSidebar.batchTargets(
            visibleOrder: order, selection: selection, snapshot: snap(order))
        XCTAssertEqual(pruned.count, 2, "two visible selected rows → multi-select MODE (≥2)")
        XCTAssertEqual(targets.map(\.path), ["F"], "the op acts on the folder only (Move 1 Item)")
    }

    // MARK: - Codex finding 4: reused-id path reconciliation

    /// A selected dir id whose CURRENT path differs from its selection-time
    /// snapshot (a reused SQLite id now pointing at a new folder) is DROPPED —
    /// so the new folder is not shown selected / not a batch target.
    func testPrunedSelectionDropsRepointedReusedId() {
        // id 5 was selected as "old"; after a rescan the SAME id is "new".
        let order = [vdir("new", id: 5), vfile("keep.md")]
        let selection: Set<FileTreeSidebar.RowID> = [.node(.dir(5)), .node(.file(path: "keep.md"))]
        let snapshot: [FileTreeSidebar.RowID: String] = [
            .node(.dir(5)): "old", .node(.file(path: "keep.md")): "keep.md",
        ]
        XCTAssertEqual(
            FileTreeSidebar.prunedSelection(
                visibleOrder: order, selection: selection, snapshot: snapshot).map(\.path),
            ["keep.md"], "the repointed reused-id folder is dropped; the file stays")
    }

    /// `reconcileSelection` drops a repointed id (resolves to a different path),
    /// keeps a matching id, and keeps a transiently-unresolvable id (mid-refetch).
    func testReconcileSelectionDropsRepointedKeepsMatchingAndTransient() {
        let selection: Set<FileTreeSidebar.RowID> = [
            .node(.dir(5)),  // repointed: snapshot "old", resolves "new"
            .node(.dir(6)),  // matching: snapshot "keep", resolves "keep"
            .node(.dir(7)),  // transient: snapshot "later", resolves nil
        ]
        let snapshot: [FileTreeSidebar.RowID: String] = [
            .node(.dir(5)): "old", .node(.dir(6)): "keep", .node(.dir(7)): "later",
        ]
        let resolve: (FileTreeSidebar.RowID) -> String? = { rowID in
            switch rowID {
            case .node(.dir(5)): return "new"
            case .node(.dir(6)): return "keep"
            default: return nil
            }
        }
        let result = FileTreeSidebar.reconcileSelection(
            selection: selection, snapshot: snapshot, resolve: resolve)
        XCTAssertFalse(result.selection.contains(.node(.dir(5))), "repointed id dropped")
        XCTAssertTrue(result.selection.contains(.node(.dir(6))), "matching id kept")
        XCTAssertTrue(result.selection.contains(.node(.dir(7))), "transient (nil-resolve) id kept")
    }

    // MARK: - Codex round-4 finding 2: in-app rename/move REMAPS (not drops)

    /// A KNOWN in-app folder RENAME (id preserved, path intentionally changed)
    /// remaps the selected folder's snapshot to the new path and keeps it
    /// selected + focused + anchored — multi-mode preserved (test b).
    func testRemapFolderRenameKeepsItSelectedUnderNewPath() {
        let selection: Set<FileTreeSidebar.RowID> = [.node(.dir(7)), .node(.file(path: "G.md"))]
        let snapshot: [FileTreeSidebar.RowID: String] = [.node(.dir(7)): "F", .node(.file(path: "G.md")): "G.md"]
        let r = FileTreeSidebar.remapSelectionForMove(
            selection: selection, snapshot: snapshot,
            focus: .node(.dir(7)), anchor: .node(.dir(7)), anchorSnapshot: "F",
            oldPath: "F", newPath: "F2")
        XCTAssertTrue(r.selection.contains(.node(.dir(7))), "the dir id is preserved (still selected)")
        XCTAssertTrue(r.selection.contains(.node(.file(path: "G.md"))), "the sibling stays selected")
        XCTAssertEqual(r.snapshot[.node(.dir(7))], "F2", "the folder's snapshot follows to the new path")
        XCTAssertEqual(r.focus, .node(.dir(7)), "focus stays on the renamed folder")
        XCTAssertEqual(r.anchor, .node(.dir(7)), "anchor stays coherent")
        XCTAssertEqual(r.anchorSnapshot, "F2", "the anchor snapshot follows to the new path")
    }

    /// A KNOWN in-app folder MOVE likewise remaps the snapshot to the new parent
    /// path — and remaps a selected DESCENDANT file's RowID + snapshot (test c).
    func testRemapFolderMoveRemapsFolderAndDescendantFile() {
        let selection: Set<FileTreeSidebar.RowID> = [
            .node(.dir(1)), .node(.file(path: "folder/x.md")),
        ]
        let snapshot: [FileTreeSidebar.RowID: String] = [
            .node(.dir(1)): "folder", .node(.file(path: "folder/x.md")): "folder/x.md",
        ]
        let r = FileTreeSidebar.remapSelectionForMove(
            selection: selection, snapshot: snapshot,
            focus: .node(.dir(1)), anchor: nil, anchorSnapshot: nil,
            oldPath: "folder", newPath: "dest/folder")
        XCTAssertTrue(r.selection.contains(.node(.dir(1))), "folder dir id preserved")
        XCTAssertEqual(r.snapshot[.node(.dir(1))], "dest/folder", "folder snapshot moved")
        XCTAssertTrue(
            r.selection.contains(.node(.file(path: "dest/folder/x.md"))),
            "the descendant FILE row is re-keyed to its new path")
        XCTAssertFalse(
            r.selection.contains(.node(.file(path: "folder/x.md"))), "the old file row is gone")
        XCTAssertEqual(r.snapshot[.node(.file(path: "dest/folder/x.md"))], "dest/folder/x.md")
    }

    /// A FILE rename re-keys the focus RowID (files are path-keyed).
    func testRemapFileRenameRekeysFocus() {
        let selection: Set<FileTreeSidebar.RowID> = [.node(.file(path: "a.md"))]
        let snapshot: [FileTreeSidebar.RowID: String] = [.node(.file(path: "a.md")): "a.md"]
        let r = FileTreeSidebar.remapSelectionForMove(
            selection: selection, snapshot: snapshot,
            focus: .node(.file(path: "a.md")), anchor: .node(.file(path: "a.md")), anchorSnapshot: "a.md",
            oldPath: "a.md", newPath: "b.md")
        XCTAssertEqual(r.selection, [.node(.file(path: "b.md"))])
        XCTAssertEqual(r.focus, .node(.file(path: "b.md")))
        XCTAssertEqual(r.anchor, .node(.file(path: "b.md")))
    }

    /// Entries UNAFFECTED by the rename/move keep their RowID + snapshot.
    func testRemapLeavesUnaffectedEntriesUntouched() {
        let selection: Set<FileTreeSidebar.RowID> = [.node(.dir(9)), .node(.file(path: "sib.md"))]
        let snapshot: [FileTreeSidebar.RowID: String] = [.node(.dir(9)): "Other", .node(.file(path: "sib.md")): "sib.md"]
        let r = FileTreeSidebar.remapSelectionForMove(
            selection: selection, snapshot: snapshot, focus: .node(.dir(9)), anchor: nil,
            anchorSnapshot: nil, oldPath: "F", newPath: "F2")
        XCTAssertEqual(r.selection, selection, "unrelated entries are untouched")
        XCTAssertEqual(r.snapshot[.node(.dir(9))], "Other")
    }

    // MARK: - Codex round-5: the range ANCHOR is snapshotted + reconciled + remapped

    /// (a/d) The anchor reconcile rule: CLEARED only on a confirmed path mismatch
    /// (reused id); KEPT when transiently unresolvable (mid-refetch) or matching.
    func testAnchorSurvivesReconcileRule() {
        XCTAssertFalse(
            FileTreeSidebar.anchorSurvivesReconcile(snapshot: "Old", resolved: "New"),
            "a reused-id anchor (resolves to a different path) is cleared")
        XCTAssertTrue(
            FileTreeSidebar.anchorSurvivesReconcile(snapshot: "Old", resolved: nil),
            "a transiently-unresolvable anchor (mid-refetch) is retained")
        XCTAssertTrue(
            FileTreeSidebar.anchorSurvivesReconcile(snapshot: "Keep", resolved: "Keep"),
            "a still-matching anchor is retained")
        XCTAssertTrue(
            FileTreeSidebar.anchorSurvivesReconcile(snapshot: nil, resolved: "Anything"),
            "no snapshot ⇒ nothing to invalidate against")
    }

    /// (b) A DESELECTED descendant anchor under a KNOWN moved folder is remapped
    /// to follow the move — even though it is NOT in the selection.
    func testRemapMovesDeselectedDescendantAnchor() {
        // Only the folder is selected; the anchor is a deselected file inside it.
        let selection: Set<FileTreeSidebar.RowID> = [.node(.dir(1))]
        let snapshot: [FileTreeSidebar.RowID: String] = [.node(.dir(1)): "folder"]
        let r = FileTreeSidebar.remapSelectionForMove(
            selection: selection, snapshot: snapshot,
            focus: .node(.dir(1)),
            anchor: .node(.file(path: "folder/x.md")), anchorSnapshot: "folder/x.md",
            oldPath: "folder", newPath: "dest/folder")
        XCTAssertEqual(
            r.anchor, .node(.file(path: "dest/folder/x.md")),
            "the deselected descendant anchor follows the move (re-keyed)")
        XCTAssertEqual(r.anchorSnapshot, "dest/folder/x.md", "and its snapshot follows too")
    }

    /// (c) The pure model makes a ⌘-REMOVED (deselected) row the anchor — so the
    /// view's `setSelectionAnchor` snapshots it (a deselected row still gets a
    /// snapshot). This pins the pure half; the wiring is asserted by inspection.
    func testCommandRemoveMakesRemovedRowTheAnchor() {
        let order = ["A", "Old", "Z"].map(fileRow)
        let out = FileTreeSidebar.applySelectionClick(
            order: order, current: [fileRow("A"), fileRow("Old")], anchor: fileRow("A"),
            clicked: fileRow("Old"), click: .toggle)
        XCTAssertEqual(out.selection, [fileRow("A")], "Old is ⌘-removed")
        XCTAssertEqual(
            out.anchor, fileRow("Old"),
            "the DESELECTED removed row becomes the anchor (must be snapshotted)")
    }

    /// The remaining view-only wiring runs reconciliation after refetch and
    /// mirrors through the fail-closed node resolver. Reconcile/focus/path
    /// behavior itself is exercised directly by `SidebarSelectionModelTests`.
    func testNativeFocusReanchorAndFailClosedTargetByInspection() throws {
        let src = try Self.normalizedSidebarSource()
        XCTAssertTrue(
            src.contains(
                ".onChange(of: tree.visibleRows) { _, _ in "
                    + "if restorePendingBatchFocus(proxy: proxy) "
                    + "|| restorePendingPostMutationFocus(proxy: proxy) { "
                    + "mirrorTreeSelectionToAppState(listSelection) return } "
                    + "reconcileSelectionAfterTreeChange() "
                    + "mirrorTreeSelectionToAppState(listSelection) }"),
            "pending focus owns transient edges; otherwise reconcile then mirror")
        XCTAssertTrue(
            src.contains(
                "let resolvedSelection = selection.flatMap { selection in "
                    + "pathValidatedNode(for: selection).map { "
                    + "AppState.TreeSelection(path: $0.path, isDirectory: $0.isDirectory) } }"),
            "the AppState mirror still resolves reused identities fail-closed")
        XCTAssertTrue(
            src.contains(
                "Self.publishGuardAwareTreeSelectionMirror( "
                    + "pending: pendingPostMutationFocus, model: selectionModel, "
                    + "currentSessionIdentity: appState.currentSession.map(ObjectIdentifier.init), "
                    + "currentMutationToken: appState.treeMutation?.token, "
                    + "resolvedSelection: resolvedSelection, appState: appState)"),
            "only the session-and-mutation-owned Delete fallback may replace the nil resolution")
    }

    /// #852 Codex round-4 finding 2 (source wiring): a KNOWN in-app rename/move
    /// REMAPS the selection instead of letting the generic reconcile misread the
    /// intentional path change as a reused id.
    func testKnownRenameMoveRemapsByInspection() throws {
        let src = try Self.normalizedSidebarSource()
        XCTAssertTrue(
            src.contains(
                "case let .rename(oldPath, newPath), let .move(oldPath, newPath, _, _): "
                    + "remapSelectionForKnownMove(oldPath: oldPath, newPath: newPath)"),
            "handleTreeMutation remaps the selection for a known rename/move")
        XCTAssertFalse(
            src.contains("reconcileSelectionAfterTreeChange() applyPostMutationFocus"),
            "the generic reconcile no longer runs synchronously inside handleTreeMutation")
        XCTAssertEqual(
            src.components(separatedBy: "tree.remapExpansions( using: batchMoveIndex").count - 1,
            1,
            "exactly one indexed batch transition remaps all moved expansions")
        XCTAssertEqual(
            src.components(separatedBy: "tree.removeExpansions( using: batchRemovalIndex").count - 1,
            1,
            "exactly one indexed batch transition removes all trashed expansions")
        XCTAssertEqual(
            src.components(separatedBy:
                "result.remapKnownMoves( using: index, "
                    + "identityForRemappedPath: remappedSelectionIdentity, "
                    + "componentVisits: &componentVisits"
            ).count - 1,
            1,
            "exactly one indexed kernel prepares the remapped semantic selection")
        XCTAssertTrue(
            src.contains(
                "applyBatchMoveFocus( batchMoveFocus, moveIndex: batchMoveIndex, "
                    + "componentVisits: &componentVisits, proxy: proxy)"),
            "production carries the one batch index into atomic remap-plus-final-focus publication")
        XCTAssertEqual(
            src.components(
                separatedBy:
                    "mutateSelectionAndPublish(visibleRows: preMutationRows) { "
                    + "$0.removeKnownItems( using: batchRemovalIndex, "
                    + "preferredFocusPath: mutation.preferredFocusPath, "
                    + "visibleRows: preMutationRows, componentVisits: &componentVisits) }"
            ).count - 1,
            1,
            "one indexed Trash transition atomically publishes surviving semantic selection")
        XCTAssertTrue(
            src.contains("restorePendingBatchFocus(proxy: proxy)"),
            "an unmaterialized destination gets one landing restoration attempt")
    }

    // MARK: - Codex findings 1 & 2: create gated on session + explicit result

    func testCreateFolderThenBatchMoveCreatesTheFolderAndMovesItems() async throws {
        let (state, vault) = try await makeVault(files: ["a.md", "b.md", "keep.md"])
        await state.createFolderThenBatchMove(
            newFolderName: "New Folder", in: "", items: [sel("a.md"), sel("b.md")])?.value
        XCTAssertTrue(exists(vault, "New Folder/a.md"), "a moved into the new folder")
        XCTAssertTrue(exists(vault, "New Folder/b.md"), "b moved into the new folder")
        XCTAssertFalse(exists(vault, "a.md"))
        XCTAssertFalse(exists(vault, "b.md"))
        XCTAssertTrue(exists(vault, "keep.md"))
        XCTAssertEqual(
            state.lastMutationAnnouncement, "Moved 2 items to New Folder.",
            "the batch move announces once after the create landed")
    }

    /// A STALE `lastError` (set by an earlier, unrelated failure) must NOT block
    /// a genuinely-successful create-then-move — the gate is the create's actual
    /// result, not the global error heuristic.
    func testCreateFolderThenBatchMoveIgnoresStalePriorLastError() async throws {
        let (state, vault) = try await makeVault(files: ["a.md", "dest/x.md"])
        state.lastError = "an old, unrelated failure"  // stale
        await state.createFolderThenBatchMove(
            newFolderName: "New Folder", in: "", items: [sel("a.md")])?.value
        XCTAssertTrue(
            exists(vault, "New Folder/a.md"),
            "the move ran despite a stale lastError (gate is the create result, not lastError)")
    }

    /// #852 Codex finding 2 (the data-loss bug): a pre-existing EMPTY "New
    /// Folder" (invisible to the old `files`-based uniqueName) must NOT swallow
    /// the moved items. With uniqueName now seeing empty folders, the create
    /// suffixes to a fresh "New Folder 2" and the items land THERE — never
    /// inside the pre-existing folder.
    func testCreateThenMoveDoesNotMoveIntoPreExistingEmptyFolder() async throws {
        let (state, vault) = try await makeVault(files: ["a.md", "keep.md"])
        // An EMPTY folder named exactly "New Folder" (invisible to `files`).
        try FileManager.default.createDirectory(
            at: vault.appendingPathComponent("New Folder"), withIntermediateDirectories: true)

        await state.createFolderThenBatchMove(
            newFolderName: "New Folder", in: "", items: [sel("a.md")])?.value
        XCTAssertFalse(
            exists(vault, "New Folder/a.md"),
            "the item is NOT moved into the pre-existing empty folder")
        XCTAssertTrue(exists(vault, "New Folder 2/a.md"), "it lands in the freshly-created folder")
        XCTAssertFalse(exists(vault, "a.md"))
    }

    /// #852 Codex finding 2: `createFolder` reports its ACTUAL outcome via
    /// `onResult` — `true` on success, `false` on a collision failure — the
    /// signal the create-then-move flows gate on (never folder existence).
    func testCreateFolderReportsSuccessAndFailureViaOnResult() async throws {
        let (state, vault) = try await makeVault(files: ["keep.md"])
        var ok: Bool?
        await state.createFolder(name: "Fresh", in: "", onResult: { ok = $0 })?.value
        XCTAssertEqual(ok, true, "a successful create reports true")

        // A collision (the folder now exists) reports false — an existence check
        // would wrongly report success on exactly this pre-existing directory.
        var collided: Bool?
        await state.createFolder(name: "Fresh", in: "", onResult: { collided = $0 })?.value
        XCTAssertEqual(collided, false, "a colliding create reports false")
        XCTAssertTrue(exists(vault, "Fresh"), "the pre-existing folder is still on disk (existence ≠ success)")
    }

    /// #852 Codex finding 2: `uniqueName` derives collisions from an authoritative
    /// directory listing, so it sees EMPTY folders (absent from `files`).
    func testUniqueNameSeesEmptyFoldersViaDirectoryListing() async throws {
        let (state, vault) = try await makeVault(files: ["keep.md"])
        try FileManager.default.createDirectory(
            at: vault.appendingPathComponent("New Folder"), withIntermediateDirectories: true)
        // createFolderThenBatchMove with no items: the create still runs and must
        // not collide — it suffixes to "New Folder 2" because the empty folder is
        // now visible to uniqueName.
        await state.createFolderThenBatchMove(
            newFolderName: "New Folder", in: "", items: [])?.value
        XCTAssertTrue(
            exists(vault, "New Folder 2"),
            "uniqueName suffixed past the pre-existing empty 'New Folder'")
    }

    /// #852 Codex findings 1 & 2 (source): both flows synchronously admit the
    /// CREATE, then gate the MOVE on its explicit `created` result + original
    /// session — never folder existence. Because the accepted flow is two
    /// phase, both also report phase-two contention instead of implying the
    /// already-created folder includes a move that never ran.
    func testCreateThenMoveFlowsGuardSessionAndGateOnResultByInspection() throws {
        let src = try Self.normalizedSource("AppState.swift")
        let raw = try Self.rawSource("AppState.swift")
        // createFolder reports its actual success via onResult.
        XCTAssertTrue(
            src.contains("func createFolder( name: String, in parent: String, onResult: ((Bool) -> Void)? = nil )"),
            "createFolder reports its actual outcome via onResult")
        // Both flows admit the create before returning a chain Task, then gate
        // the dependent move on its explicit outcome and captured session.
        let admission =
            "guard let createTask = createFolder( name: suffixed, in: parent, "
            + "onResult: { created = $0 }) else { return nil }"
        XCTAssertEqual(
            src.components(separatedBy: admission).count - 1, 2,
            "single and batch New Folder flows reject synchronously when create is busy")
        let phaseTwoAdmission =
            "guard created, self.currentSession === session else { return } "
            + "if let continuationGate = self.createFolderMoveContinuationGateForTesting { "
            + "await continuationGate() } guard self.currentSession === session else { return } "
            + "if let reason = self.structuralMutationDisabledReason"
        XCTAssertEqual(
            src.components(separatedBy: phaseTwoAdmission).count - 1, 2,
            "both dependent moves recheck session and gate availability")
        XCTAssertTrue(
            raw.contains(
                #"\(reason) \((movePath as NSString).lastPathComponent) was not moved."#),
            "single-item contention names the item that stayed in place")
        XCTAssertTrue(
            raw.contains(#"\(reason) \(subject) not moved."#),
            "batch contention reports that the selection stayed in place")
        XCTAssertTrue(
            src.contains(
                "await self.moveEntry(path: movePath, isDirectory: isDirectory, to: newFolderPath)?.value"),
            "the single-item phase proceeds only after the second admission check")
        XCTAssertTrue(
            src.contains(
                "await self.batchMove( items, to: newFolderPath, "
                    + "preferredFocusPath: preferredFocusPath)?.value"),
            "the batch phase proceeds only after the second admission check")
        // The retired existence heuristic is gone.
        XCTAssertFalse(
            src.contains("folderCreateLanded"),
            "the folder-existence heuristic (folderCreateLanded) is retired")
    }

    // MARK: - Codex finding 5: completed old-vault batches don't touch new UI

    func testBatchPostLoopWritesAreSessionGuardedByInspection() throws {
        let src = try Self.normalizedSource("AppState.swift")

        func functionBody(start: String, end: String) throws -> String {
            let startRange = try XCTUnwrap(src.range(of: start))
            let tail = src[startRange.lowerBound...]
            let endRange = try XCTUnwrap(tail.range(of: end))
            return String(tail[..<endRange.lowerBound])
        }

        func assertOwnershipGuards(
            _ body: String,
            successLanding: String,
            usesExplicitSelf: Bool = true,
            ownershipGuardCount: Int = 3,
            refreshedSuccessContinuation: String? = nil,
            file: StaticString = #filePath,
            line: UInt = #line
        ) {
            let owner = "ownsStructuralMutation(token, session: session)"
            let qualifier = usesExplicitSelf ? "self." : ""
            let initialGuard = usesExplicitSelf
                ? "guard let self, self.\(owner) else { return }"
                : "guard \(owner) else { return }"
            XCTAssertEqual(
                body.components(separatedBy: owner).count - 1, ownershipGuardCount,
                "every asynchronous continuation rechecks structural ownership",
                file: file, line: line)
            XCTAssertEqual(
                body.components(separatedBy: "await refresher(self)").count - 1, 2,
                "success-refresh and infrastructure-failure paths each refresh once",
                file: file, line: line)
            XCTAssertTrue(
                body.contains(
                    "\(initialGuard) switch outcome"),
                "the no-refresh success path is owned before any state landing",
                file: file, line: line)
            XCTAssertTrue(
                body.contains(
                    "await refresher(self) guard \(qualifier)\(owner) else { return } } "
                        + (refreshedSuccessContinuation ?? successLanding)),
                "the refreshed success path rechecks ownership before continuing",
                file: file, line: line)

            guard let failure = body.range(of: "case .failure(let error):") else {
                return XCTFail("missing infrastructure-failure branch", file: file, line: line)
            }
            let failureBody = String(body[failure.lowerBound...])
            let ordered = [
                "await refresher(self)",
                "guard \(qualifier)\(owner) else { return }",
                "\(qualifier)clearStructuralUndoStacks()",
                "\(qualifier)publishTreeMutation(",
                "\(qualifier)setBatchStructuralResult(",
                "\(qualifier)postMutationAnnouncement(",
            ]
            var remainder = failureBody[...]
            for token in ordered {
                guard let range = remainder.range(of: token) else {
                    return XCTFail(
                        "failure state write is missing or precedes its ownership guard: \(token)",
                        file: file, line: line)
                }
                remainder = remainder[range.upperBound...]
            }
        }

        let move = try functionBody(
            start: "func batchMove( _ items: [TreeSelection], to newParent: String, "
                + "preferredFocusPath: String? ) -> Task<Void, Never>? {",
            end: "private static func batchMoveNeedsRefresh")
        assertOwnershipGuards(move, successLanding: "self.landForwardBatchMove(")

        let trash = try functionBody(
            start: "private func performBatchTrash(",
            end: "private static func batchTrashNeedsRefresh")
        assertOwnershipGuards(
            trash,
            successLanding: "landBatchTrash(",
            usesExplicitSelf: false,
            ownershipGuardCount: 4,
            refreshedSuccessContinuation: "if !report.unknown.isEmpty {")

        guard let unknownStart = trash.range(of: "if !report.unknown.isEmpty {") else {
            return XCTFail("missing unknown Trash reconciliation branch")
        }
        var unknownRemainder = trash[unknownStart.lowerBound...]
        for token in [
            "}.value",
            "guard ownsStructuralMutation(token, session: session) else { return }",
            "reconcileBatchTrashUnknownPathsAfterRefresh(",
            "landBatchTrash(",
        ] {
            guard let range = unknownRemainder.range(of: token) else {
                return XCTFail(
                    "unknown Trash state write is missing or precedes its ownership guard: \(token)")
            }
            unknownRemainder = unknownRemainder[range.upperBound...]
        }
    }

    // MARK: - Codex finding 1: every batch member is visibly + accessibly selected

    func testEveryBatchMemberIsSelectedVisuallyAndAccessiblyByInspection() throws {
        let src = try Self.normalizedSidebarSource()
        // Path-valid membership is covered directly by SidebarSelectionModelTests;
        // this pins only the SwiftUI-only trait and fill consumption sites.
        XCTAssertTrue(
            src.contains(
                "isRowSelected(.node(node.nodeID), currentPath: node.path) ? .isSelected : []"),
            "both rows carry .isSelected for every selected, path-valid batch member")
        XCTAssertTrue(
            src.contains("isMultiSelectFill(.node(node.nodeID), currentPath: node.path) {"),
            "both rows paint the multi-select fill")
    }

    // MARK: - Codex finding 2a/2b: same-value focus normalization

    /// A programmatic / single open collapses the batch set UNCONDITIONALLY —
    /// the collapse is NOT gated on `listSelection` changing (a same-value
    /// assignment wouldn't fire the `.onChange(of: listSelection)` collapse).
    func testProgrammaticOpenCollapsesBatchUnconditionally() {
        let mirrored = FileTreeSidebar.RowID.node(.file(path: "opened.md"))
        let other = FileTreeSidebar.RowID.node(.file(path: "other.md"))

        for current in [nil, mirrored, other] {
            let outcome = FileTreeSidebar.programmaticSelectionOutcome(
                currentListSelection: current,
                mirroredSelection: mirrored)
            XCTAssertEqual(outcome.selection, [mirrored])
            XCTAssertEqual(outcome.anchor, mirrored)
            XCTAssertEqual(outcome.listSelection, mirrored)
            XCTAssertEqual(outcome.shouldMirrorListSelection, current != mirrored)
            XCTAssertEqual(outcome.shouldSuppressAnnouncement, current != mirrored)
        }

        let cleared = FileTreeSidebar.programmaticSelectionOutcome(
            currentListSelection: mirrored,
            mirroredSelection: nil)
        XCTAssertTrue(cleared.selection.isEmpty)
        XCTAssertNil(cleared.anchor)
        XCTAssertNil(cleared.listSelection)
        XCTAssertTrue(cleared.shouldMirrorListSelection)
        XCTAssertTrue(cleared.shouldSuppressAnnouncement)
    }

    // MARK: - Source helpers

    /// Load a `Sources/SlateMac/<name>`, strip comments + strings, and collapse
    /// whitespace runs to single spaces so the contiguous-chain assertions are
    /// robust to formatting.
    private static func normalizedSource(_ name: String) throws -> String {
        let raw = try rawSource(name)
        let stripped = SwiftSourceStripping.strippingCommentsAndStrings(raw)
        return stripped.split(whereSeparator: { $0.isWhitespace }).joined(separator: " ")
    }

    private static func rawSource(_ name: String) throws -> String {
        var cursor = URL(fileURLWithPath: #filePath).deletingLastPathComponent()
        for _ in 0..<8 {
            let candidate = cursor.appendingPathComponent("Sources/SlateMac/\(name)")
            if FileManager.default.fileExists(atPath: candidate.path) {
                return try String(contentsOf: candidate, encoding: .utf8)
            }
            cursor = cursor.deletingLastPathComponent()
        }
        throw XCTSkip("\(name) not found relative to the test file")
    }

    private static func normalizedSidebarSource() throws -> String {
        try normalizedSource("FileTreeSidebar.swift")
    }
}
