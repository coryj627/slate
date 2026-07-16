// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import XCTest

@testable import SlateMac

/// #871: undo/redo for structural file ops (move + rename, incl. drag-moves).
///
/// The structural domain is a THIRD undo stack routed by file-tree focus,
/// mutually exclusive with the canvas domain (#372/#867). These tests pin:
///  - the inverse actually reverses on disk (move-back / rename-back) and
///    redo re-applies,
///  - the "Undid/Redid …" VoiceOver announcements,
///  - the domain-routing precedence (canvas → structural → responder),
///  - the per-domain menu title + enablement, and
///  - the per-vault clearing on close + direct switch (constraint #871.6).
@MainActor
final class StructuralUndoTests: XCTestCase {

    private var tempDir: URL!

    override func setUpWithError() throws {
        tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("struct-undo-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: tempDir, withIntermediateDirectories: true)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: tempDir)
    }

    /// Build a real vault + opened AppState (mirrors MutationAnnouncementFocusTests).
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

    private actor BatchUndoRunnerProbe {
        private var reports: [BatchMoveReport]
        private var opIDs: [Int64] = []

        init(reports: [BatchMoveReport]) {
            self.reports = reports
        }

        func run(opID: Int64) -> BatchMoveReport {
            opIDs.append(opID)
            return reports.removeFirst()
        }

        func calls() -> [Int64] { opIDs }
    }

    private actor SuspendedBatchUndoRunner {
        private let report: BatchMoveReport
        private var opIDs: [Int64] = []
        private var entered = false
        private var entranceWaiters: [CheckedContinuation<Void, Never>] = []
        private var releaseWaiter: CheckedContinuation<Void, Never>?

        init(report: BatchMoveReport) {
            self.report = report
        }

        func run(opID: Int64) async -> BatchMoveReport {
            opIDs.append(opID)
            entered = true
            for waiter in entranceWaiters { waiter.resume() }
            entranceWaiters = []
            await withCheckedContinuation { releaseWaiter = $0 }
            return report
        }

        func waitUntilEntered() async {
            guard !entered else { return }
            await withCheckedContinuation { entranceWaiters.append($0) }
        }

        func release() {
            releaseWaiter?.resume()
            releaseWaiter = nil
        }

        func calls() -> [Int64] { opIDs }
    }

    @MainActor
    private final class BatchRefreshProbe {
        private(set) var calls = 0

        func run(_ state: AppState) async {
            calls += 1
        }
    }

    private func batchMoveReport(
        state: BatchMoveState = .succeeded,
        planned: [StructuralBatchItem],
        opID: Int64? = nil,
        standing: [BatchPathChange] = [],
        rolledBack: [BatchPathChange] = [],
        requiresRescan: Bool = false
    ) -> BatchMoveReport {
        BatchMoveReport(
            envelope: StructuralBatchEnvelope(
                planned: planned, skipped: [], preflightFailures: []),
            state: state,
            opId: opID,
            standing: standing,
            rolledBack: rolledBack,
            failure: nil,
            rollbackFailures: [],
            rewritten: [],
            rewriteFailures: [],
            requiresRescan: requiresRescan)
    }

    private func batchTrashReport(
        state: BatchTrashState = .succeeded,
        planned: [StructuralBatchItem],
        opID: Int64? = nil,
        trashed: [StructuralBatchItem] = [],
        requiresRescan: Bool = false
    ) -> BatchTrashReport {
        BatchTrashReport(
            envelope: StructuralBatchEnvelope(
                planned: planned, skipped: [], preflightFailures: []),
            state: state,
            opId: opID,
            trashed: trashed,
            untrashed: [],
            unknown: [],
            bookkeepingFailures: [],
            requiresRescan: requiresRescan)
    }

    private func armBatchHistory(
        on state: AppState,
        opID: Int64 = 700,
        standing: [BatchPathChange] = [
            BatchPathChange(oldPath: "a.md", newPath: "dest/a.md", isDirectory: false)
        ]
    ) async {
        let items = [StructuralBatchItem(path: "a.md", isDirectory: false)]
        let report = batchMoveReport(planned: items, opID: opID, standing: standing)
        state.batchMoveRunner = { _, _ in report }
        state.structuralBatchRefreshRunner = { _ in }
        await state.batchMove(
            [.init(path: "a.md", isDirectory: false)], to: "dest").value
    }

    private enum BatchUndoProbeError: LocalizedError {
        case unavailable

        var errorDescription: String? { "history endpoint unavailable" }
    }

    // MARK: - Move undo/redo reverses + re-applies

    func testMoveThenUndoMovesBackToOriginalParent() async throws {
        let (state, vault) = try await makeVault(files: ["a.md", "dest/x.md"])

        await state.moveEntry(path: "a.md", isDirectory: false, to: "dest")?.value
        XCTAssertTrue(exists(vault, "dest/a.md"), "moved into dest")
        XCTAssertFalse(exists(vault, "a.md"))
        XCTAssertEqual(state.structuralUndoStack.count, 1, "the move recorded one inverse")
        XCTAssertTrue(state.structuralRedoStack.isEmpty)

        state.structuralUndo()
        await state.pendingStructuralTaskForTesting?.value

        XCTAssertTrue(exists(vault, "a.md"), "undo moved it back to the root")
        XCTAssertFalse(exists(vault, "dest/a.md"))
        XCTAssertEqual(state.lastMutationAnnouncement, "Undid move of a.md.")
        XCTAssertTrue(state.structuralUndoStack.isEmpty, "the undone entry retired")
        XCTAssertEqual(state.structuralRedoStack.count, 1, "and staged a redo")
    }

    func testMoveUndoThenRedoReappliesTheMove() async throws {
        let (state, vault) = try await makeVault(files: ["a.md", "dest/x.md"])
        await state.moveEntry(path: "a.md", isDirectory: false, to: "dest")?.value
        state.structuralUndo()
        await state.pendingStructuralTaskForTesting?.value
        XCTAssertTrue(exists(vault, "a.md"))

        state.structuralRedo()
        await state.pendingStructuralTaskForTesting?.value

        XCTAssertTrue(exists(vault, "dest/a.md"), "redo re-applied the move")
        XCTAssertFalse(exists(vault, "a.md"))
        XCTAssertEqual(state.lastMutationAnnouncement, "Redid move of a.md.")
        XCTAssertEqual(state.structuralUndoStack.count, 1, "redo re-armed undo")
        XCTAssertTrue(state.structuralRedoStack.isEmpty)
    }

    func testBatchMoveUndoRedoUseDedicatedEndpointAndReturnedStanding() async throws {
        let (state, _) = try await makeVault(files: ["a.md", "sub/b.md", "dest/x.md"])
        let items = [
            StructuralBatchItem(path: "a.md", isDirectory: false),
            StructuralBatchItem(path: "sub/b.md", isDirectory: false),
        ]
        let forwardStanding = [
            BatchPathChange(oldPath: "a.md", newPath: "dest/a.md", isDirectory: false),
            BatchPathChange(oldPath: "sub/b.md", newPath: "dest/b.md", isDirectory: false),
        ]
        let forwardReport = batchMoveReport(
            planned: items, opID: 700, standing: forwardStanding)
        state.batchMoveRunner = { _, _ in forwardReport }
        state.structuralBatchRefreshRunner = { _ in }
        await state.batchMove(
            [
                .init(path: "a.md", isDirectory: false),
                .init(path: "sub/b.md", isDirectory: false),
            ],
            to: "dest").value
        XCTAssertEqual(
            state.structuralUndoStack,
            [.batchMove(opId: 700, entries: forwardStanding)])

        let undoStanding = [
            BatchPathChange(oldPath: "dest/a.md", newPath: "a.md", isDirectory: false),
            BatchPathChange(oldPath: "dest/b.md", newPath: "sub/b.md", isDirectory: false),
        ]
        let redoStanding = forwardStanding
        let probe = BatchUndoRunnerProbe(
            reports: [
                batchMoveReport(planned: items, opID: 701, standing: undoStanding),
                batchMoveReport(planned: items, opID: 702, standing: redoStanding),
            ])
        state.batchUndoMoveRunner = { _, opID in await probe.run(opID: opID) }

        state.structuralUndo()
        await state.pendingStructuralTaskForTesting?.value

        let undoCalls = await probe.calls()
        XCTAssertEqual(undoCalls, [700], "undo uses one dedicated native call")
        XCTAssertTrue(state.structuralUndoStack.isEmpty)
        XCTAssertEqual(
            state.structuralRedoStack,
            [.batchMove(opId: 701, entries: undoStanding)],
            "redo is armed from core's returned standing paths and operation id")
        guard case .batchMove(let standing, _)? = state.treeMutation?.kind else {
            return XCTFail("the returned undo report must publish one typed batch event")
        }
        XCTAssertEqual(standing, undoStanding)
        guard case let .move(report, mode)? = state.batchStructuralResult?.payload else {
            return XCTFail("the complete undo report must remain available")
        }
        XCTAssertEqual(report.opId, 701)
        XCTAssertEqual(mode, .undo)
        XCTAssertEqual(state.lastMutationAnnouncement, "Undid move of 2 items.")

        state.structuralRedo()
        await state.pendingStructuralTaskForTesting?.value

        let redoCalls = await probe.calls()
        XCTAssertEqual(
            redoCalls, [700, 701],
            "redo reverses the operation id returned by undo, not a synthesized path list")
        XCTAssertTrue(state.structuralRedoStack.isEmpty)
        XCTAssertEqual(
            state.structuralUndoStack,
            [.batchMove(opId: 702, entries: redoStanding)])
        guard case let .move(redoReport, redoMode)? = state.batchStructuralResult?.payload else {
            return XCTFail("the complete redo report must remain available")
        }
        XCTAssertEqual(redoReport.opId, 702)
        XCTAssertEqual(redoMode, .redo)
        XCTAssertEqual(state.lastMutationAnnouncement, "Redid move of 2 items.")
    }

    func testUnknownBatchTrashBarriersBatchMoveUndoBeforeItCanReachNativeCode()
        async throws
    {
        let (state, _) = try await makeVault(files: [
            "a.md", "dest/x.md", "unknown.md",
        ])
        await armBatchHistory(on: state)
        XCTAssertEqual(state.structuralUndoStack.count, 1)

        let uncertain = StructuralBatchItem(path: "unknown.md", isDirectory: false)
        let unknownReport = BatchTrashReport(
            envelope: StructuralBatchEnvelope(
                planned: [uncertain], skipped: [], preflightFailures: []),
            state: .failed,
            opId: nil,
            trashed: [],
            untrashed: [],
            unknown: [
                BatchTrashRemainder(
                    item: uncertain,
                    failure: BatchItemFailure(
                        item: uncertain,
                        stage: .reconciliation,
                        message: "physical Trash verification failed"))
            ],
            bookkeepingFailures: [],
            requiresRescan: true)
        state.batchTrashRunner = { _, _ in unknownReport }
        state.batchTrashPresenceProbeRunner = { _, _ in .indeterminate }
        state.structuralBatchRefreshRunner = { _ in }
        await state.batchDelete([
            .init(path: uncertain.path, isDirectory: uncertain.isDirectory)
        ]).value
        XCTAssertTrue(
            state.structuralUndoStack.isEmpty,
            "an indeterminate Trash outcome is a mandatory history barrier")
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            AppState.BatchTrashCopy.announcement(for: unknownReport))

        let undoReport = batchMoveReport(
            planned: [StructuralBatchItem(path: "dest/a.md", isDirectory: false)],
            opID: 701,
            standing: [
                BatchPathChange(
                    oldPath: "dest/a.md", newPath: "a.md", isDirectory: false)
            ])
        let probe = BatchUndoRunnerProbe(reports: [undoReport])
        state.batchUndoMoveRunner = { _, opID in await probe.run(opID: opID) }

        state.structuralUndo()
        await state.pendingStructuralTaskForTesting?.value

        let undoCalls = await probe.calls()
        XCTAssertEqual(
            undoCalls, [],
            "the cleared history must make the native batch-undo funnel unreachable")
        XCTAssertTrue(state.structuralUndoStack.isEmpty)
        XCTAssertEqual(state.lastMutationAnnouncement, "Nothing to undo.")
    }

    func testBatchUndoRejectedNoOpAndRolledBackRetainSourceHistory() async throws {
        let attempted = BatchPathChange(
            oldPath: "dest/a.md", newPath: "a.md", isDirectory: false)
        let planned = [StructuralBatchItem(path: "dest/a.md", isDirectory: false)]
        let scenarios: [(name: String, report: BatchMoveReport, announcement: String, attention: Bool)] = [
            (
                "rejected",
                batchMoveReport(state: .rejected, planned: planned),
                "Move could not start. No items were moved.",
                true
            ),
            (
                "no-op",
                batchMoveReport(state: .noOp, planned: planned),
                "Nothing moved.",
                false
            ),
            (
                "rolled-back",
                batchMoveReport(state: .rolledBack, planned: planned, rolledBack: [attempted]),
                "Move stopped. Slate restored every item to its original location.",
                true
            ),
        ]

        for (index, scenario) in scenarios.enumerated() {
            let (state, _) = try await makeVault(
                named: "retain-\(index)", files: ["a.md", "dest/x.md"])
            await armBatchHistory(on: state)
            let source = try XCTUnwrap(state.structuralUndoStack.last)
            let oldTreeToken = state.treeMutation?.token
            state.batchUndoMoveRunner = { _, _ in scenario.report }

            state.structuralUndo()
            await state.pendingStructuralTaskForTesting?.value

            XCTAssertEqual(
                state.structuralUndoStack.last, source,
                "\(scenario.name) must leave the captured undo entry available")
            XCTAssertTrue(state.structuralRedoStack.isEmpty)
            guard case let .move(report, mode)? = state.batchStructuralResult?.payload else {
                XCTFail("\(scenario.name) must retain its complete report")
                continue
            }
            XCTAssertEqual(report, scenario.report)
            XCTAssertEqual(mode, .undo)
            XCTAssertEqual(state.batchStructuralResult?.requiresAttention, scenario.attention)
            XCTAssertEqual(state.lastMutationAnnouncement, scenario.announcement)
            if scenario.report.state == .rolledBack {
                guard case .batchMove(let standing, let touched)? = state.treeMutation?.kind else {
                    XCTFail("full rollback must publish restored paths once")
                    continue
                }
                XCTAssertTrue(standing.isEmpty)
                XCTAssertEqual(touched, [attempted])
            } else {
                XCTAssertEqual(
                    state.treeMutation?.token, oldTreeToken,
                    "\(scenario.name) without rescan has no physical tree landing")
            }
        }
    }

    func testBatchUndoIncompleteAndContradictoryReportsClearHistory() async throws {
        let planned = [StructuralBatchItem(path: "dest/a.md", isDirectory: false)]
        let reverse = BatchPathChange(
            oldPath: "dest/a.md", newPath: "a.md", isDirectory: false)
        let reports = [
            batchMoveReport(
                state: .rollbackIncomplete, planned: planned,
                standing: [reverse], rolledBack: [reverse]),
            batchMoveReport(state: .succeeded, planned: planned, opID: 801),
            batchMoveReport(state: .succeeded, planned: planned, standing: [reverse]),
        ]

        for (index, report) in reports.enumerated() {
            let (state, _) = try await makeVault(
                named: "barrier-\(index)", files: ["a.md", "dest/x.md"])
            await armBatchHistory(on: state)
            state.batchUndoMoveRunner = { _, _ in report }

            state.structuralUndo()
            await state.pendingStructuralTaskForTesting?.value

            XCTAssertTrue(
                state.structuralUndoStack.isEmpty,
                "\(report.state) makes the captured history unsafe")
            XCTAssertTrue(state.structuralRedoStack.isEmpty)
            guard case let .move(landed, mode)? = state.batchStructuralResult?.payload else {
                XCTFail("\(report.state) must surface its complete report")
                continue
            }
            XCTAssertEqual(landed, report)
            XCTAssertEqual(mode, .undo)
            XCTAssertTrue(state.batchStructuralResult?.requiresAttention ?? false)
            if report.state == .rollbackIncomplete {
                XCTAssertFalse(state.treeMutation?.requiresRescan ?? true)
                guard case .batchMove(let standing, let touched)? = state.treeMutation?.kind else {
                    XCTFail("incomplete rollback must publish standing and restored paths")
                    continue
                }
                XCTAssertEqual(standing, [reverse])
                XCTAssertEqual(touched, [reverse])
                XCTAssertEqual(
                    state.lastMutationAnnouncement,
                    "Move stopped. Slate restored 1 item. 1 item remains in its new location.")
            } else {
                XCTAssertTrue(state.treeMutation?.requiresRescan ?? false)
                XCTAssertEqual(
                    state.lastMutationAnnouncement,
                    "Move could not be reconciled safely.")
            }
        }
    }

    func testBatchUndoInfrastructureFailureRefreshesAndClearsHistory() async throws {
        let (state, _) = try await makeVault(files: ["a.md", "dest/x.md"])
        await armBatchHistory(on: state)
        let refresh = BatchRefreshProbe()
        state.structuralBatchRefreshRunner = { appState in await refresh.run(appState) }
        state.batchUndoMoveRunner = { _, _ in throw BatchUndoProbeError.unavailable }

        state.structuralUndo()
        await state.pendingStructuralTaskForTesting?.value

        XCTAssertTrue(state.structuralUndoStack.isEmpty)
        XCTAssertTrue(state.structuralRedoStack.isEmpty)
        XCTAssertEqual(refresh.calls, 1, "unknown physical outcome refreshes exactly once")
        XCTAssertTrue(state.treeMutation?.requiresRescan ?? false)
        guard case let .infrastructure(operation, message)? =
            state.batchStructuralResult?.payload
        else { return XCTFail("unknown physical outcome must surface as infrastructure") }
        XCTAssertEqual(operation, .move(.undo))
        XCTAssertEqual(message, "history endpoint unavailable")
        XCTAssertTrue(state.batchStructuralResult?.requiresAttention ?? false)
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            "Undo failed: history endpoint unavailable")
    }

    func testDoubleCommandZDuringBatchUndoCallsCoreOnceAndKeepsFirstTask() async throws {
        let (state, _) = try await makeVault(files: ["a.md", "dest/x.md"])
        await armBatchHistory(on: state)
        let reverse = BatchPathChange(
            oldPath: "dest/a.md", newPath: "a.md", isDirectory: false)
        let runner = SuspendedBatchUndoRunner(
            report: batchMoveReport(
                planned: [StructuralBatchItem(path: "dest/a.md", isDirectory: false)],
                opID: 701,
                standing: [reverse]))
        state.batchUndoMoveRunner = { _, opID in await runner.run(opID: opID) }

        state.structuralUndo()
        let firstTask = try XCTUnwrap(state.pendingStructuralTaskForTesting)
        await runner.waitUntilEntered()
        let source = state.structuralUndoStack

        state.structuralUndo()

        let callsWhileSuspended = await runner.calls()
        XCTAssertEqual(callsWhileSuspended, [700], "the second chord submits no native call")
        XCTAssertEqual(state.structuralUndoStack, source, "the source edge stays armed in flight")
        XCTAssertNotNil(
            state.pendingStructuralTaskForTesting,
            "a rejected second chord must not discard the first task handle")
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            "Wait for the current file operation to finish.")

        await runner.release()
        await firstTask.value
        XCTAssertTrue(state.structuralUndoStack.isEmpty)
        XCTAssertEqual(
            state.structuralRedoStack,
            [.batchMove(opId: 701, entries: [reverse])])
    }

    func testBatchUndoCompletionAfterVaultSwitchChangesNoNewVaultState() async throws {
        let (state, _) = try await makeVault(named: "stale-A", files: ["a.md", "dest/x.md"])
        await armBatchHistory(on: state)
        let reverse = BatchPathChange(
            oldPath: "dest/a.md", newPath: "a.md", isDirectory: false)
        let runner = SuspendedBatchUndoRunner(
            report: batchMoveReport(
                planned: [StructuralBatchItem(path: "dest/a.md", isDirectory: false)],
                opID: 701,
                standing: [reverse]))
        state.batchUndoMoveRunner = { _, opID in await runner.run(opID: opID) }

        state.structuralUndo()
        let oldTask = try XCTUnwrap(state.pendingStructuralTaskForTesting)
        await runner.waitUntilEntered()

        let vaultB = tempDir.appendingPathComponent("stale-B")
        try FileManager.default.createDirectory(at: vaultB, withIntermediateDirectories: true)
        try "# B\n".write(
            to: vaultB.appendingPathComponent("b.md"), atomically: true, encoding: .utf8)
        state.openVault(at: vaultB)
        await state.scanTask?.value
        let resultAfterSwitch = state.batchStructuralResult
        let treeAfterSwitch = state.treeMutation
        let announcementAfterSwitch = state.lastMutationAnnouncement

        await runner.release()
        await oldTask.value

        XCTAssertTrue(state.currentVaultURL?.standardizedFileURL == vaultB.standardizedFileURL)
        XCTAssertTrue(state.structuralUndoStack.isEmpty)
        XCTAssertTrue(state.structuralRedoStack.isEmpty)
        XCTAssertEqual(state.batchStructuralResult, resultAfterSwitch)
        XCTAssertEqual(state.treeMutation, treeAfterSwitch)
        XCTAssertEqual(state.lastMutationAnnouncement, announcementAfterSwitch)
    }

    func testBatchUndoUsesReturnedFolderAndEmptyFolderStandingPaths() async throws {
        let (state, vault) = try await makeVault(
            files: ["full/inside.md", "dest/x.md"])
        try FileManager.default.createDirectory(
            at: vault.appendingPathComponent("empty"), withIntermediateDirectories: true)
        let items = [
            StructuralBatchItem(path: "full", isDirectory: true),
            StructuralBatchItem(path: "empty", isDirectory: true),
        ]
        let forward = [
            BatchPathChange(oldPath: "full", newPath: "dest/full", isDirectory: true),
            BatchPathChange(oldPath: "empty", newPath: "dest/empty", isDirectory: true),
        ]
        let forwardReport = batchMoveReport(planned: items, opID: 900, standing: forward)
        state.batchMoveRunner = { _, _ in forwardReport }
        state.structuralBatchRefreshRunner = { _ in }
        await state.batchMove(
            [
                .init(path: "full", isDirectory: true),
                .init(path: "empty", isDirectory: true),
            ],
            to: "dest").value

        let reverse = [
            BatchPathChange(oldPath: "dest/full", newPath: "full", isDirectory: true),
            BatchPathChange(oldPath: "dest/empty", newPath: "empty", isDirectory: true),
        ]
        let probe = BatchUndoRunnerProbe(
            reports: [batchMoveReport(planned: items, opID: 901, standing: reverse)])
        state.batchUndoMoveRunner = { _, opID in await probe.run(opID: opID) }
        state.structuralUndo()
        await state.pendingStructuralTaskForTesting?.value

        let calls = await probe.calls()
        XCTAssertEqual(calls, [900])
        XCTAssertEqual(
            state.structuralRedoStack,
            [.batchMove(opId: 901, entries: reverse)],
            "the empty folder remains a first-class returned history entry")
        XCTAssertEqual(state.treeMutation?.affectedParents, ["dest", nil])
        XCTAssertEqual(state.lastMutationAnnouncement, "Undid move of 2 items.")
    }

    func testSuccessfulBatchTrashClearsArmedMoveHistoryAndCreatesNoUndoEdge() async throws {
        let (state, _) = try await makeVault(files: ["a.md", "b.md", "dest/x.md"])
        await armBatchHistory(on: state)
        XCTAssertEqual(state.structuralUndoStack.count, 1)
        let item = StructuralBatchItem(path: "b.md", isDirectory: false)
        let report = batchTrashReport(planned: [item], opID: 990, trashed: [item])
        state.batchTrashRunner = { _, _ in report }
        state.structuralBatchRefreshRunner = { _ in }

        await state.batchDelete(
            [.init(path: "b.md", isDirectory: false)]).value

        XCTAssertTrue(state.structuralUndoStack.isEmpty)
        XCTAssertTrue(state.structuralRedoStack.isEmpty)
        state.structuralUndo()
        XCTAssertEqual(state.lastMutationAnnouncement, "Nothing to undo.")
    }

    func testMoveToVaultRootUndoReturnsToOriginalFolder() async throws {
        let (state, vault) = try await makeVault(files: ["sub/a.md"])
        await state.moveEntry(path: "sub/a.md", isDirectory: false, to: "")?.value
        XCTAssertTrue(exists(vault, "a.md"))

        state.structuralUndo()
        await state.pendingStructuralTaskForTesting?.value
        XCTAssertTrue(exists(vault, "sub/a.md"), "undo restores the original folder")
        XCTAssertFalse(exists(vault, "a.md"))
    }

    // MARK: - Rename undo/redo reverses + re-applies

    func testRenameThenUndoRestoresOldName() async throws {
        let (state, vault) = try await makeVault(files: ["a.md", "b.md"])

        await state.renameEntry(path: "a.md", isDirectory: false, to: "alpha.md")?.value
        XCTAssertTrue(exists(vault, "alpha.md"))
        XCTAssertFalse(exists(vault, "a.md"))
        XCTAssertEqual(state.structuralUndoStack.count, 1)

        state.structuralUndo()
        await state.pendingStructuralTaskForTesting?.value

        XCTAssertTrue(exists(vault, "a.md"), "undo restored the original name")
        XCTAssertFalse(exists(vault, "alpha.md"))
        XCTAssertEqual(state.lastMutationAnnouncement, "Undid rename to a.md.")
    }

    func testRenameUndoThenRedoReappliesTheRename() async throws {
        let (state, vault) = try await makeVault(files: ["a.md", "b.md"])
        await state.renameEntry(path: "a.md", isDirectory: false, to: "alpha.md")?.value
        state.structuralUndo()
        await state.pendingStructuralTaskForTesting?.value

        state.structuralRedo()
        await state.pendingStructuralTaskForTesting?.value

        XCTAssertTrue(exists(vault, "alpha.md"), "redo re-applied the rename")
        XCTAssertFalse(exists(vault, "a.md"))
        XCTAssertEqual(state.lastMutationAnnouncement, "Redid rename to alpha.md.")
    }

    /// A fresh op after an undo clears the redo stack (the standard linear
    /// undo contract — you can't redo across a divergence).
    func testFreshOpClearsRedoStack() async throws {
        let (state, _) = try await makeVault(files: ["a.md", "b.md", "dest/x.md"])
        await state.renameEntry(path: "a.md", isDirectory: false, to: "alpha.md")?.value
        state.structuralUndo()
        await state.pendingStructuralTaskForTesting?.value
        XCTAssertEqual(state.structuralRedoStack.count, 1, "an undo staged a redo")

        // A brand-new move must wipe the pending redo.
        await state.moveEntry(path: "b.md", isDirectory: false, to: "dest")?.value
        XCTAssertTrue(state.structuralRedoStack.isEmpty, "a fresh op clears redo")
        XCTAssertEqual(state.structuralUndoStack.count, 1)
    }

    /// The drag-move path is `moveEntry` with the default `.record` context —
    /// so a mis-drop is one ⌘Z from recovery, exactly like the menu path.
    func testDragMoveIsUndoable() async throws {
        let (state, vault) = try await makeVault(files: ["note.md", "dest/x.md"])
        // What FileTreeSidebar.handleDrop calls on an intra-tree drop.
        await state.moveEntry(path: "note.md", isDirectory: false, to: "dest")?.value
        XCTAssertEqual(state.structuralUndoStack.count, 1, "the drag-move recorded an inverse")

        state.structuralUndo()
        await state.pendingStructuralTaskForTesting?.value
        XCTAssertTrue(exists(vault, "note.md"), "a mis-drop is recoverable with one ⌘Z")
    }

    // MARK: - Empty-stack announcement (canvas-parity affordance)

    func testEmptyUndoStackAnnouncesRatherThanSilentNoOp() async throws {
        let (state, _) = try await makeVault(files: ["a.md"])
        state.structuralUndo()
        XCTAssertEqual(state.lastMutationAnnouncement, "Nothing to undo.")
        state.structuralRedo()
        XCTAssertEqual(state.lastMutationAnnouncement, "Nothing to redo.")
    }

    // MARK: - Domain routing precedence (published-state only)

    /// Tree focus (and no canvas claiming the chord) → the structural domain.
    func testTreeFocusRoutesToStructuralDomain() async throws {
        let (state, _) = try await makeVault(files: ["a.md"])
        XCTAssertFalse(state.undoTargetsCanvas, "a markdown vault has no active canvas")
        XCTAssertFalse(
            state.undoTargetsStructural,
            "default focus is the editor — structural must NOT own the chord")

        state.workspace.focusTreeRegion()
        XCTAssertEqual(state.workspace.focusRegion, .tree)
        XCTAssertTrue(state.undoTargetsStructural, "tree focus routes ⌘Z to the file-op stack")
    }

    /// #871 red-team regression: while the inline tree-rename field is open,
    /// `focusRegion` stays `.tree` (the field is a focus-WITHIN descendant of
    /// the List), but ⌘Z must NOT route to the structural stack — it belongs
    /// to the field editor's own text undo. `undoTargetsStructural` therefore
    /// excludes `renamingNode != nil`, mirroring `treeKeyInterceptionActive`'s
    /// `!isRenaming` guard. Without this, a ⌘Z to fix a typo mid-rename would
    /// reverse an unrelated prior move on disk and shadow text undo.
    func testInlineRenameSuppressesStructuralUndoDomain() async throws {
        let (state, _) = try await makeVault(files: ["a.md"])
        state.workspace.focusTreeRegion()
        XCTAssertTrue(
            state.undoTargetsStructural, "precondition: tree focus, not renaming")

        XCTAssertTrue(state.requestRename(path: "a.md", isDirectory: false))
        let pending = try XCTUnwrap(state.renamingNode)
        XCTAssertEqual(
            state.workspace.focusRegion, .tree,
            "the rename field is focus-within: focusRegion stays .tree")
        XCTAssertFalse(
            state.undoTargetsStructural,
            "an open inline rename hands ⌘Z to the field editor, not the file-op stack")

        XCTAssertTrue(state.cancelPendingRename(id: pending.id))
        XCTAssertTrue(
            state.undoTargetsStructural,
            "ending the rename returns the chord to the structural domain")
    }

    /// An active canvas tab wins the chord even with the tree focused —
    /// `undoTargetsStructural` is FALSE whenever `undoTargetsCanvas` is true,
    /// so the two are provably mutually exclusive.
    func testCanvasDomainTakesPrecedenceOverStructural() async throws {
        let vault = tempDir.appendingPathComponent("canvas-vault")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        let fixture = """
            {"nodes":[{"id":"a","type":"text","text":"Alpha","x":0,"y":0,\
            "width":200,"height":100}],"edges":[]}
            """
        try Data(fixture.utf8).write(to: vault.appendingPathComponent("c.canvas"))
        let state = AppState(
            recentsStore: RecentVaultsStore(
                fileURL: tempDir.appendingPathComponent("recents-canvas.json")),
            externalOpener: { _ in true })
        state.openVault(at: vault)
        await state.scanTask?.value
        state.openFile("c.canvas", target: .currentTab)
        XCTAssertTrue(state.undoTargetsCanvas, "canvas surface owns ⌘Z")

        // Even with the tree focused, canvas precedence holds.
        state.workspace.focusTreeRegion()
        XCTAssertTrue(state.undoTargetsCanvas, "canvas still wins")
        XCTAssertFalse(
            state.undoTargetsStructural,
            "mutual exclusivity: structural is false whenever canvas is true")
    }

    /// Editor focus with no canvas → neither special domain, so ⌘Z falls
    /// through to the NSText responder chain (title/enablement prove it).
    func testEditorFocusFallsThroughToResponderChain() async throws {
        let (state, _) = try await makeVault(files: ["a.md"])
        state.selectedFilePath = "a.md"
        await state.noteLoadTask?.value
        XCTAssertEqual(state.workspace.focusRegion, .editor)
        XCTAssertFalse(state.undoTargetsCanvas)
        XCTAssertFalse(state.undoTargetsStructural, "editor focus is the responder domain")
    }

    // MARK: - Per-domain menu title + enablement

    func testStructuralMenuTitleAndEnablementPerDomain() async throws {
        let (state, _) = try await makeVault(files: ["a.md", "dest/x.md"])
        state.workspace.focusTreeRegion()

        // Empty stacks, tree-focused: enabled (canvas-parity affordance),
        // bare-verb titles.
        XCTAssertTrue(state.undoTargetsStructural)
        XCTAssertEqual(state.undoMenuItemTitle, "Undo")
        XCTAssertEqual(state.redoMenuItemTitle, "Redo")
        XCTAssertTrue(state.undoMenuItemEnabled)
        XCTAssertTrue(state.redoMenuItemEnabled)

        await state.moveEntry(path: "a.md", isDirectory: false, to: "dest")?.value
        XCTAssertEqual(
            state.undoMenuItemTitle, "Undo Move of a.md",
            "the title names the pending undo op")

        state.structuralUndo()
        await state.pendingStructuralTaskForTesting?.value
        XCTAssertEqual(
            state.redoMenuItemTitle, "Redo Move of a.md",
            "the undone op's name moves to the redo side")
    }

    /// The pure action-name composer is direction-truthful and shared by the
    /// title and the announcement (so they can't drift).
    func testStructuralUndoActionNamePhrasing() {
        XCTAssertEqual(
            AppState.structuralUndoActionName(
                .move(path: "dest/a.md", isDirectory: false, targetParent: "")),
            "move of a.md")
        XCTAssertEqual(
            AppState.structuralUndoActionName(
                .rename(path: "b.md", isDirectory: false, newName: "alpha.md")),
            "rename to alpha.md")
    }

    // MARK: - Per-vault clearing (constraint #871.6)

    func testStacksClearedOnVaultClose() async throws {
        let (state, _) = try await makeVault(files: ["a.md", "dest/x.md"])
        await state.moveEntry(path: "a.md", isDirectory: false, to: "dest")?.value
        XCTAssertEqual(state.structuralUndoStack.count, 1)

        state.closeVault()
        XCTAssertTrue(state.structuralUndoStack.isEmpty, "close drops the per-vault undo stack")
        XCTAssertTrue(state.structuralRedoStack.isEmpty)
    }

    // MARK: - Routing wiring (source inspection)

    /// XCTest can't drive the `.commands` menu builder, so the ⌘Z / ⇧⌘Z
    /// THREE-domain routing in SlateMacApp is pinned by source inspection
    /// (the repo's `…ByInspection` pattern). Comments + string literals are
    /// stripped first so the tokens must appear as LIVE code — a removed
    /// structural branch (silent regression to the two-domain routing) fails
    /// here even though the AppState gates above would still pass.
    func testUndoRedoRoutingWiresStructuralDomainByInspection() throws {
        let source = try Self.slateMacAppSource()
        let stripped = SwiftSourceStripping.strippingCommentsAndStrings(source)
        for token in [
            "appState.undoTargetsStructural",
            "appState.structuralUndo()",
            "appState.structuralRedo()",
        ] {
            XCTAssertTrue(
                stripped.contains(token),
                "SlateMacApp's .undoRedo routing must wire \(token) (three-domain precedence)")
        }
        // The canvas branch must still precede structural (mutual-exclusivity
        // precedence): canvasUndo appears before structuralUndo in the source.
        let canvas = try XCTUnwrap(stripped.range(of: "appState.canvasUndo()"))
        let structural = try XCTUnwrap(stripped.range(of: "appState.structuralUndo()"))
        XCTAssertLessThan(
            canvas.lowerBound, structural.lowerBound,
            "canvas must be checked before structural (precedence)")
    }

    private static func slateMacAppSource() throws -> String {
        var cursor = URL(fileURLWithPath: #filePath).deletingLastPathComponent()
        for _ in 0..<8 {
            let candidate = cursor.appendingPathComponent(
                "Sources/SlateMac/SlateMacApp.swift")
            if FileManager.default.fileExists(atPath: candidate.path) {
                return try String(contentsOf: candidate, encoding: .utf8)
            }
            cursor = cursor.deletingLastPathComponent()
        }
        throw XCTSkip("SlateMacApp.swift not found relative to the test file")
    }

    func testStacksClearedOnDirectVaultSwitch() async throws {
        let (state, _) = try await makeVault(named: "A", files: ["a.md", "dest/x.md"])
        await state.moveEntry(path: "a.md", isDirectory: false, to: "dest")?.value
        XCTAssertEqual(state.structuralUndoStack.count, 1)

        // Direct Open Vault (bypasses closeVault) onto a second vault.
        let vaultB = tempDir.appendingPathComponent("B")
        try FileManager.default.createDirectory(at: vaultB, withIntermediateDirectories: true)
        try "# b\n".write(
            to: vaultB.appendingPathComponent("b.md"), atomically: true, encoding: .utf8)
        state.openVault(at: vaultB)
        await state.scanTask?.value

        XCTAssertTrue(
            state.structuralUndoStack.isEmpty,
            "a direct switch must not carry vault A's inverse into vault B")
        XCTAssertTrue(state.structuralRedoStack.isEmpty)
    }

    // MARK: - Codex round 1 regressions

    /// #871 Codex round 1 (F1): a non-undoable structural mutation
    /// (create/delete/import) is a history BARRIER — it clears the undo/redo
    /// stacks, so a later ⌘Z can't replay an inverse whose path a
    /// create/import may have refilled with a DIFFERENT file.
    func testNonUndoableMutationClearsStructuralHistory() async throws {
        let (state, _) = try await makeVault(files: ["a.md", "dest/x.md"])
        await state.moveEntry(path: "a.md", isDirectory: false, to: "dest")?.value
        XCTAssertEqual(state.structuralUndoStack.count, 1, "move armed an inverse")

        // A create is non-undoable → barrier.
        await state.createFolder(name: "NewFolder", in: "")?.value
        XCTAssertTrue(
            state.structuralUndoStack.isEmpty,
            "createFolder is a barrier — the stale move-inverse is dropped")

        // And so is a delete.
        await state.moveEntry(path: "dest/a.md", isDirectory: false, to: "")?.value
        XCTAssertEqual(state.structuralUndoStack.count, 1, "second move re-armed")
        await state.deleteEntry(path: "dest/x.md", isDirectory: false)?.value
        XCTAssertTrue(
            state.structuralUndoStack.isEmpty, "deleteEntry is a barrier too")
    }

    /// #871 Codex round 1 (F2): a structural op in flight when the vault
    /// switches must not leave `isMutatingStructure` stuck true — the openVault
    /// reset releases it, so the new vault's ops are not wedged. (Kicks a move
    /// WITHOUT awaiting, so the flag is true across the synchronous switch.)
    func testVaultSwitchDoesNotWedgeStructuralMutations() async throws {
        let (state, _) = try await makeVault(named: "A", files: ["a.md", "dest/x.md"])

        // In-flight move: the detached FFI work suspends at the await, so the
        // guard flag is TRUE when the next synchronous line runs.
        let inflight = state.moveEntry(path: "a.md", isDirectory: false, to: "dest")
        XCTAssertNotNil(inflight)

        let vaultB = tempDir.appendingPathComponent("B")
        try FileManager.default.createDirectory(at: vaultB, withIntermediateDirectories: true)
        try "# b\n".write(
            to: vaultB.appendingPathComponent("b.md"), atomically: true, encoding: .utf8)
        state.openVault(at: vaultB)
        await state.scanTask?.value

        // Not wedged: a fresh structural op in vault B is admitted.
        let followUp = state.createFolder(name: "Fresh", in: "")
        XCTAssertNotNil(
            followUp,
            "vault switch must release isMutatingStructure — no permanent wedge")
        await followUp?.value
        await inflight?.value  // drain the stale task (its session guard no-ops)
    }

    /// #871 Codex round 1 (F4): an undo/redo-context RENAME that FAILS at the
    /// FFI (a collision the execution-time guard couldn't predict — e.g. a
    /// TOCTOU race between the guard and the FFI) has no inline field to render
    /// into, so it must surface via the general alert (`lastError`), NOT
    /// `structuralRenameError` — a silent failure. Exercised by invoking the
    /// `.undoing`-context rename directly against a guaranteed collision (the
    /// same call `structuralUndo` makes once past the guard).
    func testFailedUndoContextRenameSurfacesGeneralAlertNotInlineError() async throws {
        let (state, _) = try await makeVault(files: ["a.md", "b.md"])
        state.structuralRenameError = nil
        state.lastError = nil

        // b.md already exists → the rename FFI fails with DestinationExists.
        await state.renameEntry(
            path: "a.md", isDirectory: false, to: "b.md",
            undoContext: AppState.StructuralUndoContext.undoing)?.value

        XCTAssertNotNil(
            state.lastError,
            "an undo-context rename failure surfaces the general alert")
        XCTAssertNil(
            state.structuralRenameError,
            "must NOT write the inline-only error a hidden field can't show")
    }

    // MARK: - Codex round 2 regressions

    /// #871 Codex round 2 (F1): a file-creation funnel that BYPASSES
    /// `publishTreeMutation` (here New Canvas) must still clear the structural
    /// undo history — else a stale inverse could target the path the create
    /// just filled, advertising a doomed undo.
    func testBypassingCreateFunnelClearsStructuralHistory() async throws {
        let (state, _) = try await makeVault(files: ["a.md", "dest/x.md"])
        await state.moveEntry(path: "a.md", isDirectory: false, to: "dest")?.value
        XCTAssertEqual(state.structuralUndoStack.count, 1, "move armed an inverse")

        // New Canvas creates a file via createExclusive, NOT publishTreeMutation.
        try await XCTUnwrap(state.canvasNewCanvasFile()).value

        XCTAssertTrue(
            state.structuralUndoStack.isEmpty,
            "a bypassing create funnel is a structural-history barrier")
    }

    /// #871 Codex round 2 (F1): the execution-time safety net — if the files
    /// changed under an inverse (an EXTERNAL edit, or a funnel this build
    /// forgot to barrier), replaying it must be REFUSED, the suspect history
    /// dropped, and nothing mutated — not a wrong-file rename.
    func testStaleInverseWithOccupiedDestinationIsRefused() async throws {
        let (state, vault) = try await makeVault(files: ["a.md"])
        await state.renameEntry(path: "a.md", isDirectory: false, to: "b.md")?.value
        state.structuralUndo()  // b.md → a.md; redo stack = "rename a.md → b.md"
        await state.pendingStructuralTaskForTesting?.value
        XCTAssertTrue(exists(vault, "a.md"))
        XCTAssertEqual(state.structuralRedoStack.count, 1)

        // EXTERNALLY occupy the redo's destination (bypasses every in-app
        // barrier), so replaying "rename a.md → b.md" would collide/misfire.
        try "# squatter\n".write(
            to: vault.appendingPathComponent("b.md"), atomically: true, encoding: .utf8)

        state.structuralRedo()  // aborts synchronously in the validation guard

        XCTAssertTrue(
            state.structuralRedoStack.isEmpty, "the suspect redo history is dropped")
        XCTAssertEqual(
            state.lastMutationAnnouncement, "Can't redo — the files have changed.")
        XCTAssertTrue(exists(vault, "a.md"), "a.md was NOT renamed onto the squatter")
        XCTAssertEqual(
            try String(
                contentsOf: vault.appendingPathComponent("b.md"), encoding: .utf8),
            "# squatter\n", "the external b.md is untouched")
    }

    /// #871 Codex round 3 (F1a): the execution-time guard must lstat, not
    /// `fileExists` — a DANGLING symlink at the inverse's destination is
    /// reported ABSENT by `fileExists` (it follows the link), which would let
    /// the replay clobber it. The guard must see it as occupied and REFUSE.
    func testUndoRefusesWhenDestinationIsADanglingSymlink() async throws {
        let (state, vault) = try await makeVault(files: ["a.md", "dest/x.md"])
        await state.moveEntry(path: "a.md", isDirectory: false, to: "dest")?.value
        XCTAssertEqual(state.structuralUndoStack.count, 1)  // inverse: dest/a.md → root

        // Externally plant a DANGLING symlink at the inverse's destination slot.
        try FileManager.default.createSymbolicLink(
            at: vault.appendingPathComponent("a.md"),
            withDestinationURL: vault.appendingPathComponent("nowhere.md"))
        XCTAssertFalse(
            FileManager.default.fileExists(atPath: vault.appendingPathComponent("a.md").path),
            "precondition: fileExists follows the dangling link and reports absent")

        state.structuralUndo()  // aborts synchronously in the lstat guard

        XCTAssertTrue(
            state.structuralUndoStack.isEmpty, "suspect history dropped")
        XCTAssertEqual(
            state.lastMutationAnnouncement, "Can't undo — the files have changed.")
        // The symlink survives (lstat sees it) and the move was NOT reversed.
        XCTAssertNotNil(
            try? FileManager.default.attributesOfItem(
                atPath: vault.appendingPathComponent("a.md").path),
            "the dangling symlink at a.md was not clobbered")
        XCTAssertTrue(exists(vault, "dest/a.md"), "the move was not wrongly reversed")
    }

    // MARK: - Post-merge audit: bypassing-create funnels are all barriers

    /// #871 post-merge audit (PR9/#901 Codex round): `recoverDeleted` restores
    /// a trashed file via `session.recoverDeletedFile` — a structural CREATE
    /// that BYPASSES `publishTreeMutation`, so it must clear the structural
    /// undo history itself. Otherwise a stale move/rename inverse armed before
    /// the restore could target the very path the restored file now occupies,
    /// advertising a doomed one-keystroke undo. Behavioral proof: arm an
    /// inverse AFTER the delete, restore, and confirm the stack is barriered
    /// (and the file actually came back, so the barrier assertion is real).
    func testRecoverDeletedIsAStructuralHistoryBarrier() async throws {
        let (state, vault) = try await makeVault(files: ["b.md", "dest/x.md"])
        guard let session = state.currentSession else { return XCTFail("no session") }

        // Create a.md THROUGH the session (journaled, so its remnant is
        // recoverable), then trash it and surface the remnant at scan reconcile
        // — the proven recipe from HistoryPanelTests.
        _ = try session.saveText(
            path: "a.md", contents: "recover me\n", expectedContentHash: nil)
        try session.deleteFile(path: "a.md")
        _ = try session.scanInitial(cancel: CancelToken())
        await state.loadDeletedFiles()
        XCTAssertTrue(
            state.deletedFiles.contains { $0.path == "a.md" && $0.recoverable },
            "precondition: a.md is a recoverable remnant")

        // Arm a structural inverse AFTER the delete, so a stale move-inverse sits
        // on the stack when the restore (a bypassing create) lands.
        await state.moveEntry(path: "b.md", isDirectory: false, to: "dest")?.value
        XCTAssertEqual(state.structuralUndoStack.count, 1, "the move armed an inverse")

        await state.recoverDeleted(path: "a.md")

        XCTAssertEqual(
            try session.readText(path: "a.md"), "recover me\n",
            "the success path ran — a.md was restored")
        XCTAssertTrue(
            state.structuralUndoStack.isEmpty,
            "recoverDeleted bypasses publishTreeMutation — it must barrier the stale inverse")
        XCTAssertTrue(exists(vault, "dest/b.md"), "the arming move stands (only history cleared)")
    }

    /// #871 barrier-contract census (post-merge audit of PR9/#901). Every
    /// creation funnel that BYPASSES the `publishTreeMutation` choke point must
    /// apply the structural-undo barrier on its SUCCESS path — a stale
    /// move/rename inverse could otherwise replay onto the path the new file
    /// just filled ("Can't undo — the files have changed"). Reads each funnel's
    /// brace-balanced body from source and asserts, per funnel, that the barrier
    /// call is:
    ///   - PRESENT (guards silent removal),
    ///   - AFTER the create/write call (guards the clear being moved before the
    ///     create — where it would drop a legit undo without protecting the new
    ///     path), and
    ///   - on the SUCCESS path — for a linear do/switch success the barrier is
    ///     reached with NO intervening `catch`; for the two save-panel funnels
    ///     (write in a do/catch, barrier gated by a success flag) the `if
    ///     wroteOK` guard sits between the write and the barrier.
    /// A missing funnel is an XCTFail, not a silent skip, so the census can't be
    /// defeated by a rename.
    func testEveryBypassingCreateFunnelBarriersOnItsSuccessPath() {
        struct Funnel {
            let name: String
            let file: String
            let createAnchor: String
            let barrier: String
            /// nil → linear success path (assert NO `catch` between the anchor
            /// and the barrier); non-nil → the success-flag guard that must sit
            /// between the write and the barrier (save-panel funnels).
            let successGuard: String?
            /// Optional: a token the barrier must appear strictly BEFORE — guards
            /// a success-path ORDERING regression (performSave's session-global
            /// barrier must precede the per-note `loadedFilePath` guard, or a
            /// switch-away mid-write skips it).
            var mustPrecede: String? = nil
        }
        let funnels = [
            Funnel(
                name: "requestRecoverDeleted", file: "AppState+History.swift",
                createAnchor: "case .success:",
                barrier: "clearStructuralUndoStacks()", successGuard: nil),
            Funnel(
                name: "canvasConvertToNote", file: "Canvas/AppState+CanvasExtras.swift",
                createAnchor: "session.saveText(",
                barrier: "clearStructuralUndoStacks()",
                successGuard: "if outcome.createdNote"),
            Funnel(
                name: "exportSavedQuery", file: "Bases/AppState+Bases.swift",
                createAnchor: "exportSavedQueryAsBase(",
                barrier: "barrierStructuralUndoForCreatedVaultPath(",
                successGuard: "case .success"),
            Funnel(
                name: "basesBuilderSaveAsBase", file: "Bases/AppState+Bases.swift",
                createAnchor: "saveQueryAsBase(",
                barrier: "barrierStructuralUndoForCreatedVaultPath(",
                successGuard: "case .success"),
            Funnel(
                name: "performSave", file: "AppState.swift",
                createAnchor: "if case .success = outcome {",
                barrier: "barrierStructuralUndoForCreatedVaultPath(", successGuard: nil,
                mustPrecede:
                    "guard BaseExactIdentity.matches(loadedFilePath, path) else {"),
            Funnel(
                name: "performBaseSavePanelWrite", file: "Bases/AppState+Bases.swift",
                createAnchor: "text.write(to: url",
                barrier: "barrierStructuralUndoForCreatedVaultPath(",
                successGuard: "if let relativePath"),
        ]
        for funnel in funnels {
            guard let source = Self.slateMacSource(funnel.file) else {
                XCTFail(
                    "source \(funnel.file) not found relative to the test file — "
                        + "the #871 barrier census can no longer read the funnel")
                continue
            }
            guard let bodySub = Self.functionBody(funnel.name, in: source) else {
                XCTFail(
                    "func \(funnel.name) not found in \(funnel.file) — the #871 "
                        + "barrier census can no longer locate the funnel")
                continue
            }
            // #912: blank comments + string literals before every source scan
            // below, so an incidental "catch" (or any anchor token) sitting in a
            // comment or string literal can't false-ALARM the census. The
            // stripper is length-preserving, so anchor offsets are unchanged —
            // only the *content* of comments/strings is blanked to spaces.
            let body = SwiftSourceStripping.strippingCommentsAndStrings(String(bodySub))
            guard let createRange = body.range(of: funnel.createAnchor) else {
                XCTFail(
                    "\(funnel.name): create/write anchor \"\(funnel.createAnchor)\" "
                        + "not found in the funnel body")
                continue
            }
            guard
                let barrierRange = body.range(
                    of: funnel.barrier,
                    range: createRange.upperBound..<body.endIndex)
            else {
                XCTFail(
                    "\(funnel.name): barrier \"\(funnel.barrier)\" missing or not "
                        + "AFTER the create/write — the #871 barrier must be on "
                        + "the success path")
                continue
            }
            let between = body[createRange.upperBound..<barrierRange.lowerBound]
            if let guardToken = funnel.successGuard {
                XCTAssertTrue(
                    between.contains(guardToken),
                    "\(funnel.name): the barrier must be gated by \"\(guardToken)\" "
                        + "(success-only) — not run on the write's failure path")
            } else {
                XCTAssertFalse(
                    Self.hasInterveningCatch(between),
                    "\(funnel.name): a `catch` between the create and the barrier "
                        + "means the barrier isn't on the linear success path")
            }
            if let precedeToken = funnel.mustPrecede {
                guard let precedeRange = body.range(of: precedeToken) else {
                    XCTFail(
                        "\(funnel.name): ordering anchor \"\(precedeToken)\" not "
                            + "found — can't verify the barrier's position")
                    continue
                }
                XCTAssertLessThan(
                    barrierRange.lowerBound, precedeRange.lowerBound,
                    "\(funnel.name): the session-global barrier must appear BEFORE "
                        + "\"\(precedeToken)\" — placing it after that per-note guard "
                        + "lets a switch-away mid-write skip the clear (ordering race)")
            }
        }
    }

    /// #912 meta-test: the barrier census's success-path `catch` scan
    /// (`hasInterveningCatch`, used by
    /// `testEveryBypassingCreateFunnelBarriersOnItsSuccessPath`) must ignore an
    /// incidental "catch" token yet still flag a real intervening `catch`.
    /// Without this hardening, a plain `contains("catch")` would false-ALARM on
    /// a comment/string/identifier that merely spells "catch" between a funnel's
    /// create anchor and its barrier — a loud spurious failure (never a
    /// false-green). This pins the fix so it can't silently regress.
    func testBarrierCensusCatchScanIgnoresIncidentalCatchTokens() {
        // A "catch" inside a line comment is NOT a real success-path catch.
        XCTAssertFalse(
            Self.hasInterveningCatch(
                """
                let n = restore()  // this could catch you out
                clearStructuralUndoStacks()
                """),
            "a `catch` in a line comment must not trip the success-path check")

        // Nor inside a block comment…
        XCTAssertFalse(
            Self.hasInterveningCatch("/* catch */ clearStructuralUndoStacks()"),
            "a `catch` in a block comment is incidental")

        // …nor inside a string literal…
        XCTAssertFalse(
            Self.hasInterveningCatch(#"log("catch"); clearStructuralUndoStacks()"#),
            "a `catch` in a string literal is incidental")

        // …nor as a lowercase substring of a larger identifier. `catcher`
        // deliberately contains "catch" as a sub-token, so a regressed
        // `contains("catch")` would WRONGLY match (true) while `\bcatch\b`
        // correctly does not — this is what pins the word-boundary behavior.
        XCTAssertFalse(
            Self.hasInterveningCatch("catcher.reset(); clearStructuralUndoStacks()"),
            "`catch` inside the identifier `catcher` is not the keyword")

        // But a REAL intervening `catch { … }` on the path is still detected —
        // the invariant is unchanged, only the false-alarm surface shrank.
        XCTAssertTrue(
            Self.hasInterveningCatch(
                """
                do { try session.write() } catch { return }
                clearStructuralUndoStacks()
                """),
            "a real `catch` block between the create and the barrier still fails")
    }

    /// #871 over-clearing regression (Codex finding 3). `exportSavedQuery` calls
    /// the UNCONDITIONAL (create-OR-overwrite) `exportSavedQueryAsBase`, so it
    /// must barrier ONLY when a NEW in-vault `.base` is created — exporting OVER
    /// an existing `.base` must leave an unrelated legit move/rename undo intact.
    func testExportSavedQueryBarriersOnlyWhenCreatingANewBasePath() async throws {
        let (state, vault) = try await makeVault(files: ["b.md", "dest/x.md"])
        let session = try XCTUnwrap(state.currentSession)
        let queryID = try session.saveQuery(
            name: "All Files", description: nil,
            queryJson: Self.minimalSavedQueryJSON, sourceSyntax: .builder)

        // NEW path: the export creates a .base that did not exist → barrier.
        await state.moveEntry(path: "b.md", isDirectory: false, to: "dest")?.value
        XCTAssertEqual(state.structuralUndoStack.count, 1, "move armed an inverse")
        try await XCTUnwrap(
            state.exportSavedQuery(id: queryID, path: "new.base")
        ).value
        XCTAssertTrue(exists(vault, "new.base"), "the export created the file")
        XCTAssertTrue(
            state.structuralUndoStack.isEmpty,
            "exporting to a NEW in-vault path barriers the stale inverse")

        // EXISTING path: the export OVERWRITES new.base → must NOT barrier.
        await state.moveEntry(path: "dest/b.md", isDirectory: false, to: "")?.value
        XCTAssertEqual(state.structuralUndoStack.count, 1, "second move re-armed")
        try await XCTUnwrap(
            state.exportSavedQuery(id: queryID, path: "new.base")
        ).value
        XCTAssertEqual(
            state.structuralUndoStack.count, 1,
            "overwriting an EXISTING .base must NOT drop the legit move undo")
    }

    /// #871 Keep Mine create-from-missing barrier (Codex finding 2). Move an
    /// open note's sibling to arm an inverse, externally delete the note, then
    /// Keep Mine: the save observes the path as MISSING (empty expected hash)
    /// and RECREATES it — a bypassing create that must barrier the stale
    /// inverse. A normal save (non-empty expected hash) is covered by the
    /// census; here we prove the create-from-missing transition end-to-end.
    func testKeepMineRecreatingAMissingNoteIsAStructuralHistoryBarrier() async throws {
        let (state, vault) = try await makeVault(files: ["note.md", "b.md"])

        // Load note.md into the editor.
        state.selectedFilePath = "note.md"
        await state.noteLoadTask?.value
        XCTAssertEqual(state.loadedFilePath, "note.md")

        // Arm a structural inverse (rename a sibling) that must survive to Keep
        // Mine — a stale inverse waiting when the recreate lands.
        await state.renameEntry(path: "b.md", isDirectory: false, to: "c.md")?.value
        XCTAssertEqual(state.structuralUndoStack.count, 1, "rename armed an inverse")

        // Externally DELETE the open note, then dirty + save → the save sees the
        // file missing (hash mismatch) and raises a conflict (no barrier yet).
        try FileManager.default.removeItem(at: vault.appendingPathComponent("note.md"))
        state.updateEditorText("# note\n\nmy unsaved edit.\n")
        await state.saveCurrentNote()?.value
        let conflict = try XCTUnwrap(
            state.currentSaveConflict, "a missing file surfaces a save conflict")
        XCTAssertEqual(
            conflict.currentContentHash, "", "the disk hash of a missing file is empty")
        XCTAssertEqual(
            state.structuralUndoStack.count, 1, "the conflict did not touch history")

        // Keep Mine re-saves with the empty (missing) expected hash — a
        // create-from-missing that recreates note.md and must barrier.
        await state.resolveSaveConflictKeepMine()?.value

        XCTAssertTrue(exists(vault, "note.md"), "Keep Mine recreated the note")
        XCTAssertTrue(
            state.structuralUndoStack.isEmpty,
            "a create-from-missing save is a structural-history barrier")
    }

    /// #871 the shared save-panel barrier helper's decision matrix (Codex
    /// finding 1). `barrierStructuralUndoForExternalWrite` is what the two
    /// save-panel funnels (Dataview → .base conversion, Export Markdown)
    /// delegate to; NSSavePanel can target anywhere, so it must clear ONLY for a
    /// newly created IN-VAULT path — never for an overwrite, never off-vault.
    func testExternalWriteBarrierRespectsInVaultAndCreateVsOverwrite() async throws {
        let (state, vault) = try await makeVault(files: ["a.md", "dest/x.md"])

        // (1) A NEW in-vault path clears the history.
        await state.moveEntry(path: "a.md", isDirectory: false, to: "dest")?.value
        XCTAssertEqual(state.structuralUndoStack.count, 1)
        state.barrierStructuralUndoForExternalWrite(
            to: vault.appendingPathComponent("fresh.base"), existedBefore: false)
        XCTAssertTrue(
            state.structuralUndoStack.isEmpty, "a new in-vault path barriers")

        // (2) An OVERWRITE of an existing in-vault path must NOT clear.
        await state.moveEntry(path: "dest/a.md", isDirectory: false, to: "")?.value
        XCTAssertEqual(state.structuralUndoStack.count, 1)
        state.barrierStructuralUndoForExternalWrite(
            to: vault.appendingPathComponent("dest/x.md"), existedBefore: true)
        XCTAssertEqual(
            state.structuralUndoStack.count, 1, "overwriting keeps the legit undo")

        // (3) A write OUTSIDE the vault must NOT clear, even for a new path.
        let outside = FileManager.default.temporaryDirectory
            .appendingPathComponent("outside-\(UUID().uuidString).base")
        state.barrierStructuralUndoForExternalWrite(to: outside, existedBefore: false)
        XCTAssertEqual(
            state.structuralUndoStack.count, 1, "off-vault writes never barrier")
    }

    /// #871 Keep-Mine ORDERING race (Codex round 2, finding 1). The
    /// The create-from-missing barrier is SESSION-GLOBAL and fires before the
    /// per-note publication guard. Save ownership now blocks navigation for its
    /// entire lifetime, so the former reachable switch-away race is tested as
    /// a rejected navigation plus the still-required success barrier.
    func testKeepMineBarrierRunsWhileNavigationIsBlockedMidWrite() async throws {
        let (state, vault) = try await makeVault(files: ["note.md", "other.md", "b.md"])

        // Load note.md.
        state.selectedFilePath = "note.md"
        await state.noteLoadTask?.value
        XCTAssertEqual(state.loadedFilePath, "note.md")

        // Arm a structural inverse that must survive to Keep Mine.
        await state.renameEntry(path: "b.md", isDirectory: false, to: "c.md")?.value
        XCTAssertEqual(state.structuralUndoStack.count, 1, "rename armed an inverse")

        // Externally delete note.md, then save → missing-file conflict (empty
        // hash). No dirtying — a clean save still conflicts and keeps the
        // mid-flight note switch free of dirty-navigation entanglement.
        try FileManager.default.removeItem(at: vault.appendingPathComponent("note.md"))
        await state.saveCurrentNote()?.value
        let conflict = try XCTUnwrap(state.currentSaveConflict)
        XCTAssertEqual(conflict.currentContentHash, "", "missing file → empty hash")

        // Park the Keep-Mine recreate save at the post-write seam so we can
        // switch the loaded note WHILE the write is in flight.
        let entered = expectation(description: "keep-mine reached the post-write gate")
        let (gate, release) = AsyncStream.makeStream(of: Void.self)
        state.basesPostWritePublishGate = {
            entered.fulfill()
            for await _ in gate {}
        }
        let keepMine = state.resolveSaveConflictKeepMine()
        await fulfillment(of: [entered], timeout: 10)
        state.basesPostWritePublishGate = nil

        // A save owns the body for the full operation. A queued sidebar write
        // must roll back instead of moving the editor underneath Keep Mine.
        state.selectedFilePath = "other.md"
        await Task.yield()
        await Task.yield()
        XCTAssertEqual(state.loadedFilePath, "note.md")
        XCTAssertEqual(state.selectedFilePath, "note.md")
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            "Wait for the current save to finish.")

        // Release the gate. The session-global barrier must still clear the
        // inverse before the active-note publication tail.
        release.finish()
        await keepMine?.value

        XCTAssertTrue(exists(vault, "note.md"), "Keep Mine recreated the note on disk")
        XCTAssertTrue(
            state.structuralUndoStack.isEmpty,
            "the create-from-missing barrier is SESSION-GLOBAL — it must fire even "
                + "when the note switched away mid-write (the ordering fix)")
    }

    /// #871 the empty-hash guard is REAL (Codex round 2, finding 2b). A NORMAL
    /// save (non-empty expected hash, existing file) must NOT clear a pending
    /// move/rename inverse — the barrier is create-from-missing only. The source
    /// census can't see this condition, so lock it behaviorally: an
    /// unconditional barrier would fail here.
    func testNormalSaveDoesNotClearStructuralHistory() async throws {
        let (state, _) = try await makeVault(files: ["note.md", "b.md"])
        state.selectedFilePath = "note.md"
        await state.noteLoadTask?.value
        XCTAssertNotNil(
            state.currentNoteContentHash, "a loaded existing note has a real hash")

        // Arm a structural inverse.
        await state.renameEntry(path: "b.md", isDirectory: false, to: "c.md")?.value
        XCTAssertEqual(state.structuralUndoStack.count, 1, "rename armed an inverse")

        // A normal edit + save of an EXISTING file (non-empty expected hash).
        state.updateEditorText("# note\n\nedited normally.\n")
        await state.saveCurrentNote()?.value
        XCTAssertFalse(state.hasUnsavedChanges, "the normal save committed")

        XCTAssertEqual(
            state.structuralUndoStack.count, 1,
            "a normal save of an existing file must NOT drop the legit move undo")
    }

    /// A successful builder save is now an exclusive create, so it always
    /// barriers stale structural history. A collision is a failed create: it
    /// must preserve both the occupant and the still-valid inverse.
    func testBasesBuilderSaveAsBaseBarriersOnlyAfterExclusiveCreateCommits() async throws {
        let (state, vault) = try await makeVault(files: ["b.md", "dest/x.md"])
        state.activeBaseQueryBuilder = BaseQueryBuilderModel()

        // NEW path: saving creates a .base that did not exist → barrier.
        await state.moveEntry(path: "b.md", isDirectory: false, to: "dest")?.value
        XCTAssertEqual(state.structuralUndoStack.count, 1, "move armed an inverse")
        try await XCTUnwrap(
            state.basesBuilderSaveAsBase(path: "builder.base")
        ).value
        XCTAssertTrue(exists(vault, "builder.base"), "the builder save created the file")
        XCTAssertTrue(
            state.structuralUndoStack.isEmpty,
            "saving to a NEW in-vault path barriers the stale inverse")

        // EXISTING path: exclusive create fails without clobbering or barrier.
        await state.moveEntry(path: "dest/b.md", isDirectory: false, to: "")?.value
        XCTAssertEqual(state.structuralUndoStack.count, 1, "second move re-armed")
        let occupant = try Data(contentsOf: vault.appendingPathComponent("builder.base"))
        try await XCTUnwrap(
            state.basesBuilderSaveAsBase(path: "builder.base")
        ).value
        XCTAssertEqual(
            state.structuralUndoStack.count, 1,
            "a failed exclusive create must NOT drop the legit move undo")
        XCTAssertEqual(
            try Data(contentsOf: vault.appendingPathComponent("builder.base")),
            occupant,
            "a collision must preserve the existing Base byte-for-byte")
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            "A file already exists at builder.base. Choose a different Base path.")
    }

    /// A minimal valid saved-query envelope (mirrors BaseEmbedTests) — a
    /// Files-over-Notes table with one column. Exporting it produces `.base`
    /// text without needing the referenced folder to exist.
    private static let minimalSavedQueryJSON = #"""
        {
          "source": { "Folder": "Notes" },
          "row_source": "Files",
          "filters": null,
          "formulas": [],
          "custom_summaries": [],
          "group_by": null,
          "sort": [],
          "columns": [
            { "id": "file.name", "display_name": null }
          ],
          "summaries": [],
          "limit": null,
          "view": { "Table": { "fallback_from": null } }
        }
        """#

    /// Read a SlateMac source file by its path relative to `Sources/SlateMac`
    /// (mirrors `slateMacAppSource`, parameterized). Returns nil when the file
    /// can't be found/read; the census turns that into an XCTFail (not a skip),
    /// so a moved/renamed source can't silently defeat the barrier guarantee.
    private static func slateMacSource(_ relativePath: String) -> String? {
        var cursor = URL(fileURLWithPath: #filePath).deletingLastPathComponent()
        for _ in 0..<8 {
            let candidate = cursor.appendingPathComponent(
                "Sources/SlateMac/\(relativePath)")
            if FileManager.default.fileExists(atPath: candidate.path) {
                return try? String(contentsOf: candidate, encoding: .utf8)
            }
            cursor = cursor.deletingLastPathComponent()
        }
        return nil
    }

    /// The brace-balanced body of `func <name>` within `source`, from its
    /// opening `{` to the matching `}`, or nil when the function or its braces
    /// aren't found (the census turns nil into an XCTFail). Balancing (not
    /// "next func") keeps a neighboring function's body out of the slice.
    private static func functionBody(_ name: String, in source: String) -> Substring? {
        guard let funcRange = source.range(of: "func \(name)"),
            let open = source[funcRange.upperBound...].firstIndex(of: "{")
        else { return nil }
        var depth = 0
        var i = open
        while i < source.endIndex {
            switch source[i] {
            case "{": depth += 1
            case "}":
                depth -= 1
                if depth == 0 { return source[open...i] }
            default: break
            }
            i = source.index(after: i)
        }
        return nil
    }

    /// #912 false-ALARM hardening for the barrier census's linear-success
    /// check. Reports whether a LIVE Swift `catch` keyword appears in `segment`
    /// (the span between a funnel's create anchor and its barrier). It is
    /// robust to incidental text in two independent ways:
    ///   - comment + string-literal content is blanked first, so a `catch` in a
    ///     `// …` / `/* … */` comment or a `"…catch…"` literal can't trip it, and
    ///   - the keyword is matched on WORD BOUNDARIES (`\bcatch\b`), so a `catch`
    ///     embedded in a larger identifier (e.g. `errorCatcher`) can't either.
    /// The invariant the census proves is unchanged — a real `catch { … }` on
    /// the path is still detected (see `testBarrierCensus…IgnoresIncidental…`);
    /// only false-alarm tokens are ignored. This is never a false-GREEN: a real
    /// intervening `catch` keyword still fails the check.
    static func hasInterveningCatch(_ segment: some StringProtocol) -> Bool {
        let stripped = SwiftSourceStripping.strippingCommentsAndStrings(String(segment))
        return stripped.range(of: #"\bcatch\b"#, options: .regularExpression) != nil
    }
}
