// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// Outcome-unknown Trash must make dirty-close recovery fail closed without
/// consuming its alert, spinning the main actor, or arming a later close.
@MainActor
final class BatchTrashDirtyCloseTests: XCTestCase {
    private var roots: [URL] = []

    override func tearDown() {
        for root in roots {
            try? FileManager.default.removeItem(at: root)
        }
        roots = []
        super.tearDown()
    }

    private struct Fixture {
        let state: AppState
        let vault: URL
        let alphaTab: TabID
        let betaTab: TabID
    }

    private func makeTwoDirtyNotes(activePath: String) async throws -> Fixture {
        let root = FileManager.default.temporaryDirectory
            .appendingPathComponent("batch-trash-dirty-close-\(UUID().uuidString)")
        roots.append(root)
        let vault = root.appendingPathComponent("vault")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        try "# Alpha\n".write(
            to: vault.appendingPathComponent("alpha.md"),
            atomically: true,
            encoding: .utf8)
        try "# Beta\n".write(
            to: vault.appendingPathComponent("beta.md"),
            atomically: true,
            encoding: .utf8)

        let state = AppState(
            recentsStore: RecentVaultsStore(
                fileURL: root.appendingPathComponent("recents.json")),
            externalOpener: { _ in true })
        state.openVault(at: vault)
        await state.scanTask?.value

        state.selectedFilePath = "alpha.md"
        await state.noteLoadTask?.value
        state.updateEditorText("# Alpha\nunsaved alpha\n")
        let alphaTab = try XCTUnwrap(state.workspace.model.activeGroup.activeTabID)

        state.newTab()
        state.selectedFilePath = "beta.md"
        await state.noteLoadTask?.value
        state.updateEditorText("# Beta\nunsaved beta\n")
        let betaTab = try XCTUnwrap(state.workspace.model.activeGroup.activeTabID)

        if activePath == "alpha.md" {
            state.selectTab(id: alphaTab)
            await state.noteLoadTask?.value
        }
        XCTAssertEqual(state.loadedFilePath, activePath)
        XCTAssertTrue(state.hasUnsavedChanges)
        XCTAssertEqual(state.workspace.dirtyParkedDocuments().count, 1)
        return Fixture(
            state: state,
            vault: vault,
            alphaTab: alphaTab,
            betaTab: betaTab)
    }

    private func unknownTrashReport(for path: String) -> BatchTrashReport {
        let item = StructuralBatchItem(path: path, isDirectory: false)
        return BatchTrashReport(
            envelope: StructuralBatchEnvelope(
                planned: [item], skipped: [], preflightFailures: []),
            state: .failed,
            opId: nil,
            trashed: [],
            untrashed: [],
            unknown: [
                BatchTrashRemainder(
                    item: item,
                    failure: BatchItemFailure(
                        item: item,
                        stage: .reconciliation,
                        message: "physical Trash verification failed"))
            ],
            bookkeepingFailures: [],
            requiresRescan: true)
    }

    private func quarantine(_ path: String, in state: AppState) async throws {
        let report = unknownTrashReport(for: path)
        state.batchTrashRunner = { _, _ in report }
        state.batchTrashPresenceProbeRunner = { _, _ in .indeterminate }
        state.structuralBatchRefreshRunner = { _ in }
        let task = try XCTUnwrap(
            state.batchDelete(
                [.init(path: path, isDirectory: false)],
                preferredFocusPath: path))
        await task.value
        XCTAssertEqual(
            state.batchTrashPathCapability(for: path),
            .readOnly(AppState.batchTrashQuarantineReason))
    }

    private func provePresent(_ state: AppState) async throws {
        state.batchTrashPresenceProbeRunner = { _, _ in .present }
        let task = try XCTUnwrap(state.retryBatchTrashUnknownReconciliation())
        await task.value
    }

    func testQuarantinedParkedNoteAbortsSaveAllWithoutSpinOrClosingVault()
        async throws
    {
        let fixture = try await makeTwoDirtyNotes(activePath: "beta.md")
        let state = fixture.state
        try await quarantine("alpha.md", in: state)

        state.closeVaultFromUserAction()
        XCTAssertEqual(state.pendingVaultClose, 2)
        XCTAssertEqual(
            state.pendingVaultCloseSaveAllDisabledReason,
            AppState.batchTrashQuarantineReason,
            "a parked dirty path must disable Save All with the exact recovery reason")

        state.resolveVaultCloseSaveAll()

        XCTAssertEqual(
            state.pendingVaultClose,
            2,
            "an unavailable parked save must leave the recovery alert intact")
        XCTAssertNil(
            state.vaultCloseSaveAllTask,
            "preflight must not launch the Save All loop when any dirty path is read-only")
        XCTAssertNotNil(state.currentSession, "the vault must remain open")
        XCTAssertTrue(state.hasUnsavedChanges, "the active dirty buffer must survive")
        XCTAssertEqual(
            state.workspace.dirtyParkedDocuments().map(\.path),
            ["alpha.md"],
            "the quarantined parked draft must survive")

        // RED-run cleanup: old code launches a task that would spin once it
        // reaches the quarantined note. Neutralize dirt before yielding so the
        // intentionally failing regression can exit instead of hanging XCTest.
        if let launched = state.vaultCloseSaveAllTask {
            state.updateEditorText(state.savedBaselineText ?? "")
            for document in state.workspace.dirtyParkedDocuments() {
                document.hasUnsavedChanges = false
            }
            await launched.value
        }
    }

    func testQuarantinedTabCloseSaveRetainsPromptAndNeverArmsLaterClose()
        async throws
    {
        let fixture = try await makeTwoDirtyNotes(activePath: "alpha.md")
        let state = fixture.state
        try await quarantine("alpha.md", in: state)

        state.requestCloseTab(fixture.alphaTab)
        XCTAssertEqual(state.pendingTabClose, fixture.alphaTab)
        XCTAssertEqual(
            state.pendingTabCloseSaveDisabledReason,
            AppState.batchTrashQuarantineReason)
        state.resolveTabCloseSave()

        XCTAssertEqual(
            state.pendingTabClose,
            fixture.alphaTab,
            "a rejected save must keep the close recovery alert")
        XCTAssertNil(
            state.pendingTabCloseAfterSave,
            "a rejected save must not arm a close continuation")
        XCTAssertTrue(state.hasUnsavedChanges)
        XCTAssertEqual(state.workspace.model.allTabs.count, 2)
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            AppState.batchTrashQuarantineReason)

        try await provePresent(state)
        state.resolveTabCloseCancel()
        let laterText = "# Alpha\nlater ordinary save\n"
        state.updateEditorText(laterText)
        let laterSave = try XCTUnwrap(state.saveCurrentNote())
        await laterSave.value

        XCTAssertEqual(
            state.workspace.model.allTabs.count,
            2,
            "a later ordinary save must not close the formerly targeted tab")
        XCTAssertNil(state.pendingTabCloseAfterSave)
        XCTAssertEqual(
            try String(
                contentsOf: fixture.vault.appendingPathComponent("alpha.md"),
                encoding: .utf8),
            laterText)
    }

    func testInFlightTrashBlocksTabCloseSaveAndLandingShowsQuarantineReason()
        async throws
    {
        let fixture = try await makeTwoDirtyNotes(activePath: "alpha.md")
        let state = fixture.state
        let path = "alpha.md"
        let report = unknownTrashReport(for: path)
        state.batchTrashRunner = { _, _ in report }
        state.batchTrashPresenceProbeRunner = { _, _ in .indeterminate }
        state.structuralBatchRefreshRunner = { _ in }

        // Structural ownership is synchronous: the close prompt remains, but
        // Save cannot launch while Trash owns the vault-wide mutation gate.
        let quarantine = try XCTUnwrap(
            state.batchDelete(
                [.init(path: path, isDirectory: false)],
                preferredFocusPath: path))
        state.requestCloseTab(fixture.alphaTab)
        state.resolveTabCloseSave()
        XCTAssertNil(state.saveTask)
        XCTAssertEqual(state.pendingTabClose, fixture.alphaTab)
        XCTAssertNil(state.pendingTabCloseAfterSave)
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            AppState.structuralMutationBusyReason)

        await quarantine.value

        XCTAssertEqual(
            state.pendingTabClose,
            fixture.alphaTab,
            "the close recovery alert must remain after Trash lands")
        XCTAssertNil(state.pendingTabCloseAfterSave)
        XCTAssertTrue(state.hasUnsavedChanges)
        XCTAssertEqual(state.workspace.model.allTabs.count, 2)
        XCTAssertEqual(
            state.pendingTabCloseSaveDisabledReason,
            AppState.batchTrashQuarantineReason)
    }

    func testTrashLandingNeverOverwritesNewerTabClosePrompt() async throws {
        let fixture = try await makeTwoDirtyNotes(activePath: "alpha.md")
        let state = fixture.state
        let path = "alpha.md"
        let report = unknownTrashReport(for: path)
        state.batchTrashRunner = { _, _ in report }
        state.batchTrashPresenceProbeRunner = { _, _ in .indeterminate }
        state.structuralBatchRefreshRunner = { _ in }

        let quarantine = try XCTUnwrap(
            state.batchDelete(
                [.init(path: path, isDirectory: false)],
                preferredFocusPath: path))
        state.requestCloseTab(fixture.alphaTab)
        state.resolveTabCloseSave()
        XCTAssertNil(state.saveTask)
        XCTAssertEqual(state.pendingTabClose, fixture.alphaTab)
        XCTAssertNil(state.pendingTabCloseAfterSave)

        // A newer recovery request for parked beta replaces alpha's prompt.
        // The older Trash landing must never overwrite that live alert owner.
        state.requestCloseTab(fixture.betaTab)
        XCTAssertEqual(state.pendingTabClose, fixture.betaTab)

        await quarantine.value

        XCTAssertEqual(
            state.pendingTabClose,
            fixture.betaTab,
            "an older Trash landing must not displace a newer close prompt")
        XCTAssertNil(state.pendingTabCloseAfterSave)
        XCTAssertTrue(state.hasUnsavedChanges)
        XCTAssertEqual(state.workspace.dirtyParkedDocuments().map(\.path), ["beta.md"])
        XCTAssertEqual(state.workspace.model.allTabs.count, 2)
    }

    func testVaultClosePromptSupersedesTabCloseWhileTrashIsInFlight() async throws {
        let fixture = try await makeTwoDirtyNotes(activePath: "alpha.md")
        let state = fixture.state
        let path = "alpha.md"
        let report = unknownTrashReport(for: path)
        state.batchTrashRunner = { _, _ in report }
        state.batchTrashPresenceProbeRunner = { _, _ in .indeterminate }
        state.structuralBatchRefreshRunner = { _ in }

        let quarantine = try XCTUnwrap(
            state.batchDelete(
                [.init(path: path, isDirectory: false)],
                preferredFocusPath: path))
        state.requestCloseTab(fixture.alphaTab)
        XCTAssertEqual(state.pendingTabClose, fixture.alphaTab)

        state.closeVaultFromUserAction()
        XCTAssertEqual(state.pendingVaultClose, 2)
        XCTAssertNil(state.pendingTabClose)
        XCTAssertNil(state.pendingTabCloseAfterSave)

        await quarantine.value

        XCTAssertEqual(state.pendingVaultClose, 2)
        XCTAssertNil(
            state.pendingTabClose,
            "the superseded tab prompt must not return after Trash lands")
        XCTAssertNil(state.pendingTabCloseAfterSave)
        XCTAssertNotNil(state.currentSession)
        XCTAssertEqual(state.workspace.model.allTabs.count, 2)
    }

    func testVaultNavigationPromptSupersedesTabCloseWhileTrashIsInFlight() async throws {
        let fixture = try await makeTwoDirtyNotes(activePath: "alpha.md")
        let state = fixture.state
        let path = "alpha.md"
        let report = unknownTrashReport(for: path)
        state.batchTrashRunner = { _, _ in report }
        state.batchTrashPresenceProbeRunner = { _, _ in .indeterminate }
        state.structuralBatchRefreshRunner = { _ in }

        let quarantine = try XCTUnwrap(
            state.batchDelete(
                [.init(path: path, isDirectory: false)],
                preferredFocusPath: path))
        state.requestCloseTab(fixture.alphaTab)
        XCTAssertEqual(state.pendingTabClose, fixture.alphaTab)

        state.attemptCloseVault()
        XCTAssertEqual(state.pendingNavigation, .closeVault)
        XCTAssertNil(state.pendingTabClose)
        XCTAssertNil(state.pendingTabCloseAfterSave)

        await quarantine.value

        XCTAssertEqual(state.pendingNavigation, .closeVault)
        XCTAssertNil(
            state.pendingTabClose,
            "the superseded tab prompt must not return after Trash lands")
        XCTAssertNil(state.pendingTabCloseAfterSave)
        XCTAssertNotNil(state.currentSession)
        XCTAssertEqual(state.workspace.model.allTabs.count, 2)
    }

    func testQuarantinedPendingNavigationSaveIsExplicitlyUnavailable() async throws {
        let fixture = try await makeTwoDirtyNotes(activePath: "alpha.md")
        let state = fixture.state
        try await quarantine("alpha.md", in: state)

        state.attemptCloseVault()
        XCTAssertEqual(state.pendingNavigation, .closeVault)
        XCTAssertEqual(
            state.pendingNavigationSaveDisabledReason,
            AppState.batchTrashQuarantineReason)
        XCTAssertNil(state.resolvePendingNavigationSave())
        XCTAssertEqual(
            state.pendingNavigation,
            .closeVault,
            "an unavailable Save must retain the navigation recovery prompt")
        XCTAssertTrue(state.hasUnsavedChanges)
        XCTAssertNotNil(state.currentSession)
    }

    func testQuarantinedSaveConflictKeepMineUsesExactReasonAndRetainsConflict()
        async throws
    {
        let fixture = try await makeTwoDirtyNotes(activePath: "alpha.md")
        let state = fixture.state
        try await quarantine("alpha.md", in: state)
        let conflict = SaveConflict(
            path: "alpha.md",
            attemptedContents: state.currentNoteText ?? "",
            currentContentHash: "external-hash",
            expectedContentHash: "original-hash",
            currentMtimeMs: 1)
        state.currentSaveConflict = conflict

        XCTAssertEqual(
            state.saveConflictKeepMineDisabledReason,
            AppState.batchTrashQuarantineReason)
        XCTAssertNil(state.resolveSaveConflictKeepMine())
        XCTAssertEqual(
            state.currentSaveConflict,
            conflict,
            "an unavailable Keep Mine must retain the conflict recovery alert")
        XCTAssertTrue(state.hasUnsavedChanges)
    }

    func testQuarantinedPropertyConflictKeepMineUsesExactReasonAndRetainsConflict()
        async throws
    {
        let fixture = try await makeTwoDirtyNotes(activePath: "alpha.md")
        let state = fixture.state
        try await quarantine("alpha.md", in: state)
        let conflict = PropertyEditConflict(
            path: "alpha.md",
            key: "status",
            action: .set(.text(value: "mine")),
            currentContentHash: "external-hash",
            expectedContentHash: "original-hash",
            currentMtimeMs: 1)
        state.currentPropertyEditConflict = conflict

        XCTAssertEqual(
            state.propertyEditConflictKeepMineDisabledReason,
            AppState.batchTrashQuarantineReason)
        XCTAssertNil(state.resolvePropertyEditConflictKeepMine())
        XCTAssertEqual(
            state.currentPropertyEditConflict,
            conflict,
            "an unavailable Keep Mine must retain the property recovery alert")
    }
}
