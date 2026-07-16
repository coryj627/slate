// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Combine
import XCTest

@testable import SlateMac

/// Result-presentation regressions found by the whole-milestone HIG review.
/// These tests drive native-shaped reports through AppState and keep the alert
/// preview/copy boundary deterministic without presenting an AppKit alert.
@MainActor
final class BatchResultPresentationTests: XCTestCase {
    private var tempDir: URL!

    override func setUpWithError() throws {
        tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("batch-result-presentation-\(UUID().uuidString)")
        try FileManager.default.createDirectory(
            at: tempDir, withIntermediateDirectories: true)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: tempDir)
    }

    func testNoOpSkipLedgerRequiresAttentionPreservesReasonsAndAnnouncesOnce()
        async throws
    {
        let skipped = [
            BatchSkippedItem(
                item: item("duplicate.md"), reason: .duplicate,
                detail: "the same path was selected twice"),
            BatchSkippedItem(
                item: item("Folder/child.md"), reason: .coveredBySelectedFolder,
                detail: "Folder is already selected"),
            BatchSkippedItem(
                item: item("dest/already.md"), reason: .alreadyInDestination,
                detail: "the item is already under dest"),
        ]
        let report = moveReport(state: .noOp, skipped: skipped)
        let (state, _) = try await makeVault(
            files: ["duplicate.md", "Folder/child.md", "dest/already.md", "dest/keep.md"])
        state.batchMoveRunner = { _, _ in report }
        state.structuralBatchRefreshRunner = { _ in }

        var announcements: [String] = []
        let observation = state.$lastMutationAnnouncement
            .compactMap { $0 }
            .sink { announcements.append($0) }

        await state.batchMove(
            [selection("duplicate.md"), selection("Folder/child.md"),
             selection("dest/already.md")],
            to: "dest"
        ).value

        let result = try activeResult(in: state)
        XCTAssertTrue(result.requiresAttention)
        let attention = AppState.BatchStructuralCopy.attention(for: result)
        XCTAssertTrue(attention.hasDetails)
        XCTAssertFalse(attention.hasOmittedDetails)
        XCTAssertTrue(
            attention.inlineMessage.contains(
                "Skipped — duplicate.md: Duplicate selection — the same path was selected twice"))
        XCTAssertTrue(
            attention.inlineMessage.contains(
                "Skipped — Folder/child.md: Covered by selected folder — Folder is already selected"))
        XCTAssertTrue(
            attention.inlineMessage.contains(
                "Skipped — dest/already.md: Already in destination — the item is already under dest"))
        XCTAssertEqual(announcements, ["Nothing moved."])
        withExtendedLifetime(observation) {}
    }

    func testNoOpFailureAndRescanLedgersRequireAttentionButEmptyNoOpDoesNot()
        async throws
    {
        let (state, _) = try await makeVault(files: ["source.md", "dest/keep.md"])
        state.structuralBatchRefreshRunner = { _ in }

        let failure = BatchItemFailure(
            item: item("source.md"), stage: .move,
            message: "destination changed during the request")
        let cases: [(BatchMoveReport, Bool, String)] = [
            (
                moveReport(state: .noOp, failure: failure),
                true,
                "a native failure ledger is actionable"
            ),
            (
                moveReport(state: .noOp, requiresRescan: true),
                true,
                "a reconciliation request is actionable"
            ),
            (
                moveReport(state: .noOp),
                false,
                "a genuinely empty no-op stays nonmodal"
            ),
        ]

        for (report, expectedAttention, message) in cases {
            state.batchMoveRunner = { _, _ in report }
            await state.batchMove([selection("source.md")], to: "dest").value

            let result = try XCTUnwrap(state.batchStructuralResult)
            XCTAssertEqual(result.requiresAttention, expectedAttention, message)
            if expectedAttention {
                let active = try activeResult(in: state)
                XCTAssertEqual(active.id, result.id)
                XCTAssertTrue(state.dismissBatchStructuralResult(id: active.id))
            } else {
                XCTAssertNil(state.activeBatchAlertPresentation, message)
            }
        }
    }

    func testInlinePreviewCapsMoveAndTrashAtThreeDetailsWhileCopyIsComplete() {
        let skipped = (0..<5).map { index in
            BatchSkippedItem(
                item: item("item-\(index).md"), reason: .alreadyInDestination,
                detail: "already under dest \(index)")
        }
        let results = [
            AppState.BatchStructuralResult(
                payload: .move(
                    moveReport(state: .noOp, skipped: skipped),
                    mode: .forward(destination: "dest")),
                requiresAttention: true),
            AppState.BatchStructuralResult(
                payload: .trash(
                    trashReport(state: .noOp, skipped: skipped)),
                requiresAttention: true),
        ]

        for result in results {
            let attention = AppState.BatchStructuralCopy.attention(for: result)
            let lines = attention.inlineMessage.split(separator: "\n").map(String.init)
            XCTAssertEqual(lines.count, 5, "summary + three details + omitted count")
            XCTAssertTrue(lines[1].contains("item-0.md"))
            XCTAssertTrue(lines[2].contains("item-1.md"))
            XCTAssertTrue(lines[3].contains("item-2.md"))
            XCTAssertEqual(lines[4], "…and 2 more")
            XCTAssertFalse(attention.inlineMessage.contains("item-3.md"))
            XCTAssertTrue(attention.hasDetails)
            XCTAssertTrue(attention.hasOmittedDetails)
            XCTAssertEqual(attention.previewWorkCount, 5)

            let copied = AppState.BatchStructuralCopy.copiedDetails(for: result)
            XCTAssertTrue(copied.contains("item-4.md"), "copy expands the complete ledger")
            XCTAssertFalse(copied.contains("…and"))
        }
    }

    func testCopyActionIsOfferedForAnyDetailAndTruthfullyDisclosesDismissal() {
        let oneDetail = AppState.BatchStructuralResult(
            payload: .move(
                moveReport(
                    state: .noOp,
                    skipped: [
                        BatchSkippedItem(
                            item: item("already.md"), reason: .alreadyInDestination,
                            detail: "already under dest")
                    ]),
                mode: .forward(destination: "dest")),
            requiresAttention: true)
        let attention = AppState.BatchStructuralCopy.attention(for: oneDetail)

        XCTAssertTrue(attention.hasDetails)
        XCTAssertFalse(attention.hasOmittedDetails, "one inline detail is not truncated")
        XCTAssertEqual(AppState.BatchTrashCopy.copyDetailsLabel, "Copy Details and Close")
        XCTAssertEqual(
            AppState.BatchTrashCopy.copyDetailsHint,
            "Copies the complete batch report, closes this report, and returns to the files sidebar."
        )
    }

    func testCopyAndCloseRejectsStaleUUIDThenPromotesDeferredOwnerAndFocusesOnce()
        async throws
    {
        let report = moveReport(
            state: .noOp,
            skipped: [
                BatchSkippedItem(
                    item: item("already.md"), reason: .alreadyInDestination,
                    detail: "already under dest")
            ])
        let openPaths = (1...10).map { "open-\($0).md" }
        let (state, _) = try await makeVault(
            files: ["already.md", "dest/keep.md"] + openPaths)
        state.batchMoveRunner = { _, _ in report }
        state.structuralBatchRefreshRunner = { _ in }
        var copied: [String] = []
        state.batchStructuralDetailsCopier = {
            copied.append($0)
            return true
        }

        await state.batchMove([selection("already.md")], to: "dest").value
        let active = try activeResult(in: state)
        let identity = try XCTUnwrap(state.currentSession.map(ObjectIdentifier.init))
        let deferred = FileTreeSidebar.OpenSelectionRequest(
            sessionIdentity: identity,
            batch: .init(paths: openPaths, focusedPath: "open-4.md"))
        XCTAssertTrue(state.enqueueOpenSelection(deferred))

        var focusEdges = 0
        func copyAndFocus(_ id: UUID) -> Bool {
            BatchAttentionDismissal.resolve(
                id: id,
                dismiss: state.copyAndDismissBatchStructuralDetails,
                focus: { focusEdges += 1 })
        }

        XCTAssertFalse(copyAndFocus(UUID()), "a stale alert cannot copy or dismiss")
        XCTAssertTrue(copied.isEmpty)
        XCTAssertEqual(focusEdges, 0)

        XCTAssertTrue(copyAndFocus(active.id))
        XCTAssertEqual(copied.count, 1)
        XCTAssertTrue(
            copied[0].contains(
                "Skipped — already.md: Already in destination — already under dest"))
        XCTAssertEqual(focusEdges, 1)
        guard case .open(let promoted)? = state.activeBatchAlertPresentation else {
            return XCTFail("copy-and-close must promote the deferred alert owner")
        }
        XCTAssertEqual(promoted.id, deferred.id)

        XCTAssertFalse(copyAndFocus(active.id), "a replay cannot copy or focus twice")
        XCTAssertEqual(copied.count, 1)
        XCTAssertEqual(focusEdges, 1)
    }

    func testUnknownTrashOutcomeIsNeverPresentedAsDefinitelyNotMoved() {
        let uncertain = item("uncertain.md")
        let report = trashReport(
            state: .failed,
            unknown: [
                BatchTrashRemainder(
                    item: uncertain,
                    failure: BatchItemFailure(
                        item: uncertain,
                        stage: .reconciliation,
                        message: "physical verification failed"))
            ],
            requiresRescan: true)

        XCTAssertEqual(AppState.BatchTrashCopy.ledgerCount(report), 1)
        XCTAssertEqual(
            AppState.BatchTrashCopy.announcement(for: report),
            "Couldn’t verify whether 1 item moved to Trash. Rescan required.")

        let attention = AppState.BatchTrashCopy.attention(for: report)
        XCTAssertEqual(attention.title, "Trash Outcome Needs Review")
        XCTAssertTrue(attention.inlineMessage.contains("Outcome unknown — uncertain.md"))
        XCTAssertFalse(attention.inlineMessage.contains("Not moved — uncertain.md"))

        let copied = AppState.BatchTrashCopy.copiedDetails(for: report)
        XCTAssertTrue(copied.contains("Outcome unknown — uncertain.md"))
        XCTAssertTrue(copied.contains("physical verification failed"))
        XCTAssertTrue(copied.contains("Reconciliation required: Yes"))
        XCTAssertFalse(copied.contains("Not moved — uncertain.md"))
        XCTAssertFalse(copied.localizedCaseInsensitiveContains("undo"))
        XCTAssertFalse(copied.localizedCaseInsensitiveContains("rollback"))
    }

    func testMixedTrashAnnouncementCountsKnownAndUnknownOutcomesSeparately() {
        let moved = item("moved.md")
        let stayed = item("stayed.md")
        let uncertain = item("uncertain.md")
        let report = trashReport(
            state: .partial,
            trashed: [moved],
            untrashed: [
                BatchTrashRemainder(
                    item: stayed,
                    failure: BatchItemFailure(
                        item: stayed, stage: .trash, message: "permission denied"))
            ],
            unknown: [
                BatchTrashRemainder(
                    item: uncertain,
                    failure: BatchItemFailure(
                        item: uncertain,
                        stage: .reconciliation,
                        message: "physical verification failed"))
            ],
            requiresRescan: true)

        XCTAssertEqual(AppState.BatchTrashCopy.ledgerCount(report), 3)
        XCTAssertEqual(
            AppState.BatchTrashCopy.announcement(for: report),
            "Moved 1 of 3 items to Trash. 1 item was not moved. "
                + "Couldn’t verify whether 1 item moved to Trash. Rescan required.")
    }

    // MARK: - Fixtures

    private func makeVault(files: [String]) async throws -> (AppState, URL) {
        let vault = tempDir.appendingPathComponent("vault")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        for path in files {
            let url = vault.appendingPathComponent(path)
            try FileManager.default.createDirectory(
                at: url.deletingLastPathComponent(), withIntermediateDirectories: true)
            try "# \((path as NSString).lastPathComponent)\n".write(
                to: url, atomically: true, encoding: .utf8)
        }
        let state = AppState(
            recentsStore: RecentVaultsStore(
                fileURL: tempDir.appendingPathComponent("recents.json")),
            externalOpener: { _ in true })
        state.openVault(at: vault)
        await state.scanTask?.value
        return (state, vault)
    }

    private func activeResult(in state: AppState) throws -> AppState.BatchStructuralResult {
        guard case .result(let result)? = state.activeBatchAlertPresentation else {
            throw PresentationTestError.expectedActiveResult
        }
        return result
    }

    private func selection(_ path: String) -> AppState.TreeSelection {
        AppState.TreeSelection(path: path, isDirectory: false)
    }

    private func item(_ path: String) -> StructuralBatchItem {
        StructuralBatchItem(path: path, isDirectory: false)
    }

    private func envelope(
        skipped: [BatchSkippedItem] = [],
        preflightFailures: [BatchItemFailure] = []
    ) -> StructuralBatchEnvelope {
        StructuralBatchEnvelope(
            planned: [], skipped: skipped, preflightFailures: preflightFailures)
    }

    private func moveReport(
        state: BatchMoveState,
        skipped: [BatchSkippedItem] = [],
        preflightFailures: [BatchItemFailure] = [],
        failure: BatchItemFailure? = nil,
        rollbackFailures: [BatchItemFailure] = [],
        rewriteFailures: [RewriteFailure] = [],
        requiresRescan: Bool = false
    ) -> BatchMoveReport {
        BatchMoveReport(
            envelope: envelope(
                skipped: skipped, preflightFailures: preflightFailures),
            state: state,
            opId: nil,
            standing: [],
            rolledBack: [],
            failure: failure,
            rollbackFailures: rollbackFailures,
            rewritten: [],
            rewriteFailures: rewriteFailures,
            requiresRescan: requiresRescan)
    }

    private func trashReport(
        state: BatchTrashState,
        skipped: [BatchSkippedItem] = [],
        trashed: [StructuralBatchItem] = [],
        untrashed: [BatchTrashRemainder] = [],
        unknown: [BatchTrashRemainder] = [],
        bookkeepingFailures: [BatchItemFailure] = [],
        requiresRescan: Bool = false
    ) -> BatchTrashReport {
        BatchTrashReport(
            envelope: envelope(skipped: skipped),
            state: state,
            opId: nil,
            trashed: trashed,
            untrashed: untrashed,
            unknown: unknown,
            bookkeepingFailures: bookkeepingFailures,
            requiresRescan: requiresRescan)
    }

    private enum PresentationTestError: Error {
        case expectedActiveResult
    }
}
