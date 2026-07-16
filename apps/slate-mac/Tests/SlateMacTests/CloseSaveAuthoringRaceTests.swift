// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// Close/save continuations and editor callbacks must stay owned by the vault,
/// note, and exact draft that created them. These tests park the native write
/// after it commits but before MainActor publication, which is the narrow
/// window where a stale outer task or a still-editable control can otherwise
/// consume newer user state.
@MainActor
final class CloseSaveAuthoringRaceTests: XCTestCase {
    private static let saveInProgressReason =
        "Wait for the current save to finish."
    private static let propertyEditInProgressReason =
        "Wait for the current property update to finish."

    private actor AsyncGate {
        private var entered = false
        private var released = false
        private var entranceWaiters: [CheckedContinuation<Void, Never>] = []
        private var releaseWaiter: CheckedContinuation<Void, Never>?

        func suspend() async {
            guard !released else { return }
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
            released = true
            releaseWaiter?.resume()
            releaseWaiter = nil
        }
    }

    private var roots: [URL] = []

    override func tearDown() {
        for root in roots { try? FileManager.default.removeItem(at: root) }
        roots = []
        super.tearDown()
    }

    private func makeVault(
        _ name: String,
        files: [String: String] = [
            "alpha.md": "---\ntitle: Alpha\n---\n# Alpha\n",
            "beta.md": "# Beta\n",
        ]
    ) throws -> URL {
        let root = FileManager.default.temporaryDirectory
            .appendingPathComponent("close-save-authoring-race-\(UUID().uuidString)")
        roots.append(root)
        let vault = root.appendingPathComponent(name)
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        for (path, contents) in files {
            let url = vault.appendingPathComponent(path)
            try FileManager.default.createDirectory(
                at: url.deletingLastPathComponent(),
                withIntermediateDirectories: true)
            try contents.write(to: url, atomically: true, encoding: .utf8)
        }
        return vault
    }

    private func makeState(vault: URL, path: String = "alpha.md") async throws -> AppState {
        let state = AppState(
            recentsStore: RecentVaultsStore(
                fileURL: vault.deletingLastPathComponent().appendingPathComponent("recents.json")),
            externalOpener: { _ in true })
        state.openVault(at: vault)
        await state.scanTask?.value
        state.selectedFilePath = path
        await state.noteLoadTask?.value
        XCTAssertEqual(state.loadedFilePath, path)
        return state
    }

    private func makeTwoDirtyTabs(_ state: AppState) async throws {
        state.updateEditorText("# Alpha\nunsaved alpha\n")
        state.newTab()
        state.selectedFilePath = "beta.md"
        await state.noteLoadTask?.value
        state.updateEditorText("# Beta\nunsaved beta\n")
        XCTAssertEqual(state.workspace.dirtyParkedDocuments().count, 1)
    }

    private func installRowDraft(
        _ state: AppState,
        path: String = "alpha.md",
        key: String = "title",
        value: String
    ) {
        state.preservePropertyDraft(
            .scalarText(ScalarTextKind(kind: "text", value: value)),
            path: path,
            key: key)
    }

    private func createSetH1ThenExternalH2Race(
        _ state: AppState,
        vault: URL,
        path: String = "alpha.md",
        key: String = "title",
        mine: String = "Mine H1",
        external: String = "External H2",
        retainRowDraft: Bool = true
    ) async throws {
        let draft = PropertyEditDraft.scalarText(
            ScalarTextKind(kind: "text", value: mine))
        if retainRowDraft {
            state.preservePropertyDraft(draft, path: path, key: key)
        }
        let gate = AsyncGate()
        state.basesPostWritePublishGate = { await gate.suspend() }
        let task = try XCTUnwrap(
            state.setProperty(
                path: path,
                key: key,
                value: .text(value: mine),
                submittedDraft: retainRowDraft ? draft : nil))
        await gate.waitUntilEntered()
        try "---\ntitle: \(external)\n---\n# Alpha\n".write(
            to: vault.appendingPathComponent(path),
            atomically: true,
            encoding: .utf8)
        await gate.release()
        await task.value
        state.basesPostWritePublishGate = nil
    }

    private func createSourceH1ThenExternalH2Race(
        _ state: AppState,
        vault: URL
    ) async throws {
        let sourceH1 = "title: Mine Source H1\nstatus: retained\n"
        state.propertiesSourceDraftPath = "alpha.md"
        state.propertiesSourceDraft = sourceH1
        let gate = AsyncGate()
        state.basesPostWritePublishGate = { await gate.suspend() }
        let task = try XCTUnwrap(state.applyPropertiesSource(sourceH1))
        await gate.waitUntilEntered()
        try "---\ntitle: External Source H2\n---\n# Alpha\n".write(
            to: vault.appendingPathComponent("alpha.md"),
            atomically: true,
            encoding: .utf8)
        await gate.release()
        await task.value
        state.basesPostWritePublishGate = nil
    }

    func testDirectOpenVaultRoutesEveryDirtySurfaceThroughCloseGate()
        async throws
    {
        let vaultA = try makeVault("A")
        let vaultB = try makeVault("B", files: ["fresh.md": "# Fresh\n"])
        let state = try await makeState(vault: vaultA)
        state.updateEditorText("# Unsaved body\n")
        installRowDraft(state, value: "Unsaved row")
        state.propertiesSourceDraftPath = "alpha.md"
        state.propertiesSourceDraft = "title: Unsaved source\n"

        state.openVault(at: vaultB)

        XCTAssertEqual(
            state.currentVaultURL?.path, vaultA.path,
            "a direct open-document handoff must not replace a dirty vault")
        XCTAssertEqual(state.pendingVaultClose, 1)
        XCTAssertEqual(state.pendingVaultSwitchTarget?.path, vaultB.path)
        XCTAssertEqual(state.currentNoteText, "# Unsaved body\n")
        XCTAssertTrue(state.hasUnsavedChanges)
        XCTAssertNotNil(
            state.preservedPropertyDraft(path: "alpha.md", key: "title"))
        XCTAssertEqual(
            state.propertiesSourceDraft, "title: Unsaved source\n")
        XCTAssertEqual(state.propertiesSourceDraftPath, "alpha.md")
    }

    func testSaveAllOuterOwnerCannotCloseReplacementVault() async throws {
        let vaultA = try makeVault("A")
        let vaultB = try makeVault("B", files: ["fresh.md": "# Fresh\n"])
        let state = try await makeState(vault: vaultA)
        try await makeTwoDirtyTabs(state)
        let target = RecentVault(path: vaultB.path, displayName: "B", lastOpenedMs: 0)
        let gate = AsyncGate()
        state.basesPostWritePublishGate = { await gate.suspend() }
        defer { state.basesPostWritePublishGate = nil }

        state.switchToRecent(target)
        XCTAssertEqual(state.pendingVaultClose, 2)
        state.resolveVaultCloseSaveAll()
        let oldOuter = try XCTUnwrap(state.vaultCloseSaveAllTask)
        await gate.waitUntilEntered()

        // The lower-level open-document handoff follows the same ownership
        // rule as the user-facing switch: it must wait for A's save instead of
        // replacing A and discarding the draft underneath the outer owner.
        state.openVault(at: vaultB)
        XCTAssertEqual(state.currentVaultURL?.path, vaultA.path)
        XCTAssertEqual(state.lastMutationAnnouncement, Self.saveInProgressReason)
        XCTAssertEqual(state.pendingVaultSwitchTarget?.path, vaultB.path)

        await gate.release()
        await oldOuter.value

        XCTAssertEqual(state.currentVaultURL?.path, vaultB.path)
        XCTAssertNotNil(state.currentSession, "vault A's outer Save All must not close vault B")
    }

    func testPendingNavigationSaveOwnerCannotCloseReplacementVault() async throws {
        let vaultA = try makeVault("A")
        let vaultB = try makeVault("B", files: ["fresh.md": "# Fresh\n"])
        let state = try await makeState(vault: vaultA)
        state.updateEditorText("# Alpha\nunsaved\n")
        let target = RecentVault(path: vaultB.path, displayName: "B", lastOpenedMs: 0)
        let gate = AsyncGate()
        state.basesPostWritePublishGate = { await gate.suspend() }
        defer { state.basesPostWritePublishGate = nil }

        state.switchToRecent(target)
        XCTAssertEqual(state.pendingNavigation, .closeVault)
        let oldOuter = try XCTUnwrap(state.resolvePendingNavigationSave())
        await gate.waitUntilEntered()

        state.openVault(at: vaultB)
        XCTAssertEqual(state.currentVaultURL?.path, vaultA.path)
        XCTAssertEqual(state.lastMutationAnnouncement, Self.saveInProgressReason)
        XCTAssertEqual(state.pendingVaultSwitchTarget?.path, vaultB.path)

        await gate.release()
        await oldOuter.value

        XCTAssertEqual(state.currentVaultURL?.path, vaultB.path)
        XCTAssertNotNil(state.currentSession, "vault A's navigation task must not close vault B")
    }

    func testQueuedPropertyKeepMineAdmissionRestoresExactConflict() async throws {
        let vault = try makeVault("A")
        let state = try await makeState(vault: vault)
        let conflict = PropertyEditConflict(
            path: "alpha.md",
            key: "title",
            action: .set(.text(value: "Mine")),
            currentContentHash: "external-hash",
            expectedContentHash: "old-hash",
            currentMtimeMs: 1)
        state.currentPropertyEditConflict = conflict

        let retry = try XCTUnwrap(state.resolvePropertyEditConflictKeepMine())
        let reservation = try XCTUnwrap(
            state.admitStructuralRecoveryDestination("alpha.md"))
        let token = state.beginStructuralMutation(recoveryReservation: reservation)
        await retry.value

        XCTAssertEqual(state.currentPropertyEditConflict, conflict)
        XCTAssertFalse(state.isEditingProperty)
        state.endStructuralMutation(token)
    }

    func testPropertyReloadPreservesDirtyBodyAndRefreshesOnlyPropertyState()
        async throws
    {
        let vault = try makeVault("A")
        let state = try await makeState(vault: vault)
        let originalBaseline = state.savedBaselineText
        state.updateEditorText("# Unsaved body\n")
        installRowDraft(state, value: "Mine")
        installRowDraft(state, key: "other", value: "Keep this draft")
        try "---\ntitle: External\n---\n# Disk body\n".write(
            to: vault.appendingPathComponent("alpha.md"),
            atomically: true,
            encoding: .utf8)
        state.currentPropertyEditConflict = PropertyEditConflict(
            path: "alpha.md",
            key: "title",
            action: .set(.text(value: "Mine")),
            currentContentHash: "external-hash",
            expectedContentHash: "old-hash",
            currentMtimeMs: 1)

        let reload = try XCTUnwrap(
            state.resolvePropertyEditConflictReloadFromDisk())
        await reload.value

        XCTAssertEqual(state.currentNoteText, "# Unsaved body\n")
        XCTAssertEqual(state.savedBaselineText, originalBaseline)
        XCTAssertTrue(state.hasUnsavedChanges)
        XCTAssertEqual(
            state.currentNoteProperties.first(where: { $0.key == "title" })?.valueJson,
            "\"External\"")
        XCTAssertNil(state.preservedPropertyDraft(path: "alpha.md", key: "title"))
        XCTAssertNotNil(state.preservedPropertyDraft(path: "alpha.md", key: "other"))
    }

    func testSecondSameTabCloseDuringSaveCannotExposeDiscard() async throws {
        let vault = try makeVault("A")
        let state = try await makeState(vault: vault)
        let tab = try XCTUnwrap(state.workspace.model.activeGroup.activeTabID)
        let gate = AsyncGate()
        state.updateEditorText("# Save then close\n")
        state.basesPostWritePublishGate = { await gate.suspend() }
        defer { state.basesPostWritePublishGate = nil }

        state.requestCloseTab(tab)
        state.resolveTabCloseSave()
        await gate.waitUntilEntered()
        XCTAssertEqual(state.pendingTabCloseAfterSave, tab)

        state.requestCloseTab(tab)

        XCTAssertNil(
            state.pendingTabClose,
            "a second close must not offer Discard after native Save has begun")
        XCTAssertEqual(state.pendingTabCloseAfterSave, tab)
        XCTAssertNotNil(state.workspace.model.tab(tab))

        await gate.release()
        await state.saveTask?.value
        XCTAssertNil(state.workspace.model.tab(tab))
    }

    func testOrdinaryInFlightSaveQueuesSameTabCloseWithoutDiscardPrompt() async throws {
        let vault = try makeVault("A")
        let state = try await makeState(vault: vault)
        let tab = try XCTUnwrap(state.workspace.model.activeGroup.activeTabID)
        let gate = AsyncGate()
        state.updateEditorText("# Ordinary save\n")
        state.basesPostWritePublishGate = { await gate.suspend() }
        defer { state.basesPostWritePublishGate = nil }

        let save = try XCTUnwrap(state.saveCurrentNote())
        await gate.waitUntilEntered()
        state.requestCloseTab(tab)

        XCTAssertNil(state.pendingTabClose)
        XCTAssertEqual(state.pendingTabCloseAfterSave, tab)
        await gate.release()
        await save.value
        XCTAssertNil(state.workspace.model.tab(tab))
    }

    func testSaveInFlightBlocksNewerBodyEditAndKeepsDuplicateCoherent() async throws {
        let vault = try makeVault("A")
        let state = try await makeState(vault: vault)
        let first = try XCTUnwrap(state.workspace.model.activeGroup.activeTabID)
        state.newTab()
        let duplicate = try XCTUnwrap(state.workspace.model.activeGroup.activeTabID)
        state.updateEditorText("# Captured v1\n")
        let gate = AsyncGate()
        state.basesPostWritePublishGate = { await gate.suspend() }
        defer { state.basesPostWritePublishGate = nil }

        let save = try XCTUnwrap(state.saveCurrentNote())
        await gate.waitUntilEntered()
        state.updateEditorText("# Must not land v2\n")

        XCTAssertEqual(state.currentNoteText, "# Captured v1\n")
        XCTAssertEqual(state.lastMutationAnnouncement, Self.saveInProgressReason)
        XCTAssertEqual(state.workspace.document(for: first)?.text, "# Captured v1\n")
        // The active duplicate is represented by the live AppState buffer;
        // only parked duplicates necessarily have a NoteDocument.
        if let parkedDuplicate = state.workspace.document(for: duplicate) {
            XCTAssertEqual(parkedDuplicate.text, "# Captured v1\n")
        }

        await gate.release()
        await save.value
        XCTAssertFalse(state.hasUnsavedChanges)
    }

    func testBodyKeepMineUsesLatestVisibleBuffer() async throws {
        let vault = try makeVault("A")
        let state = try await makeState(vault: vault)
        state.updateEditorText("# Attempt v1\n")
        try "---\ntitle: External\n---\n# External\n".write(
            to: vault.appendingPathComponent("alpha.md"),
            atomically: true,
            encoding: .utf8)
        try await XCTUnwrap(state.saveCurrentNote()).value
        XCTAssertNotNil(state.currentSaveConflict)
        state.updateEditorText("# Visible v2\n")

        try await XCTUnwrap(state.resolveSaveConflictKeepMine()).value

        let disk = try String(
            contentsOf: vault.appendingPathComponent("alpha.md"),
            encoding: .utf8)
        XCTAssertTrue(disk.hasSuffix("# Visible v2\n"), "Keep Mine must write visible mine")
        XCTAssertEqual(state.currentNoteText, "# Visible v2\n")
        XCTAssertFalse(state.hasUnsavedChanges)
    }

    func testPropertyKeepMineUsesLatestRecoverableRowDraft() async throws {
        let vault = try makeVault("A")
        let state = try await makeState(vault: vault)
        try "---\ntitle: External\n---\n# Alpha\n".write(
            to: vault.appendingPathComponent("alpha.md"),
            atomically: true,
            encoding: .utf8)
        try await XCTUnwrap(
            state.setProperty(
                path: "alpha.md", key: "title", value: .text(value: "Mine v1")))
            .value
        XCTAssertNotNil(state.currentPropertyEditConflict)
        installRowDraft(state, value: "Mine v2")

        try await XCTUnwrap(state.resolvePropertyEditConflictKeepMine()).value

        let disk = try String(
            contentsOf: vault.appendingPathComponent("alpha.md"),
            encoding: .utf8)
        XCTAssertTrue(disk.contains("title: Mine v2"))
    }

    func testPropertyKeepMineUsesLatestRecoverableSourceDraft() async throws {
        let vault = try makeVault("A")
        let state = try await makeState(vault: vault)
        try "---\ntitle: External\n---\n# Alpha\n".write(
            to: vault.appendingPathComponent("alpha.md"),
            atomically: true,
            encoding: .utf8)
        state.propertiesSourceDraftPath = "alpha.md"
        state.propertiesSourceDraft = "title: Mine v1\n"
        try await XCTUnwrap(
            state.applyPropertiesSource("title: Mine v1\n"))
            .value
        XCTAssertNotNil(state.currentPropertyEditConflict)
        state.updatePropertiesSourceDraft("title: Mine v2\n")

        try await XCTUnwrap(state.resolvePropertyEditConflictKeepMine()).value

        let disk = try String(
            contentsOf: vault.appendingPathComponent("alpha.md"),
            encoding: .utf8)
        XCTAssertTrue(disk.contains("title: Mine v2"))
    }

    func testPropertyCommitBlocksQueuedRowAndSourceDraftCallbacks() async throws {
        let vault = try makeVault("A")
        let state = try await makeState(vault: vault)
        let rowGate = AsyncGate()
        state.basesPostWritePublishGate = { await rowGate.suspend() }
        let row = try XCTUnwrap(
            state.setProperty(
                path: "alpha.md", key: "title", value: .text(value: "Committed")))
        await rowGate.waitUntilEntered()

        installRowDraft(state, value: "Queued row")

        XCTAssertNil(state.preservedPropertyDraft(path: "alpha.md", key: "title"))
        XCTAssertEqual(state.lastMutationAnnouncement, Self.propertyEditInProgressReason)
        await rowGate.release()
        await row.value

        let sourceGate = AsyncGate()
        state.propertiesSourceDraftPath = "alpha.md"
        state.propertiesSourceDraft = "title: Source v1\n"
        state.basesPostWritePublishGate = { await sourceGate.suspend() }
        let source = try XCTUnwrap(state.applyPropertiesSource("title: Source v1\n"))
        await sourceGate.waitUntilEntered()

        state.updatePropertiesSourceDraft("title: Queued source v2\n")

        XCTAssertEqual(state.propertiesSourceDraft, "title: Source v1\n")
        XCTAssertEqual(state.lastMutationAnnouncement, Self.propertyEditInProgressReason)
        await sourceGate.release()
        await source.value
        state.basesPostWritePublishGate = nil
    }

    func testDeleteConflictBlocksNavigationUntilItsActionIsRecoverable() async throws {
        let vault = try makeVault("A")
        let state = try await makeState(vault: vault)
        try "---\ntitle: External\n---\n# Alpha\n".write(
            to: vault.appendingPathComponent("alpha.md"),
            atomically: true,
            encoding: .utf8)

        let edit = try XCTUnwrap(
            state.deleteProperty(path: "alpha.md", key: "title"))
        state.openFile("beta.md", target: .currentTab)

        XCTAssertEqual(state.loadedFilePath, "alpha.md")
        XCTAssertEqual(state.lastMutationAnnouncement, Self.propertyEditInProgressReason)
        await edit.value
        XCTAssertEqual(state.currentPropertyEditConflict?.action, .delete)
    }

    func testAddConflictBlocksNavigationUntilItsActionIsRecoverable() async throws {
        let vault = try makeVault("A")
        let state = try await makeState(vault: vault)
        try "---\ntitle: External\n---\n# Alpha\n".write(
            to: vault.appendingPathComponent("alpha.md"),
            atomically: true,
            encoding: .utf8)

        let edit = try XCTUnwrap(
            state.setProperty(
                path: "alpha.md",
                key: "status",
                value: .text(value: "Mine Add")))
        state.openFile("beta.md", target: .currentTab)

        XCTAssertEqual(state.loadedFilePath, "alpha.md")
        XCTAssertEqual(state.lastMutationAnnouncement, Self.propertyEditInProgressReason)
        await edit.value
        XCTAssertEqual(
            state.currentPropertyEditConflict?.action,
            .set(.text(value: "Mine Add")))
    }

    func testDeleteH1RemainsRecoverableAfterPostWriteNavigationToBeta() async throws {
        let vault = try makeVault("A")
        let state = try await makeState(vault: vault)
        let gate = AsyncGate()
        state.basesPostWritePublishGate = { await gate.suspend() }
        let edit = try XCTUnwrap(
            state.deleteProperty(path: "alpha.md", key: "title"))
        await gate.waitUntilEntered()

        state.openFile("beta.md", target: .currentTab)
        await state.noteLoadTask?.value
        try "---\ntitle: External H2\n---\n# Alpha\n".write(
            to: vault.appendingPathComponent("alpha.md"),
            atomically: true,
            encoding: .utf8)
        await gate.release()
        await edit.value
        state.basesPostWritePublishGate = nil

        XCTAssertEqual(state.loadedFilePath, "beta.md")
        XCTAssertTrue(state.hasAuthoredPropertyPublicationRecovery(for: "alpha.md"))
        state.openFile("alpha.md", target: .currentTab)
        await state.noteLoadTask?.value
        XCTAssertTrue(
            try XCTUnwrap(state.activePropertyPublicationRecoveryText)
                .contains("Action: Delete"))
    }

    func testAddH1RemainsRecoverableAfterPostWriteNavigationToBeta() async throws {
        let vault = try makeVault("A")
        let state = try await makeState(vault: vault)
        let gate = AsyncGate()
        state.basesPostWritePublishGate = { await gate.suspend() }
        let edit = try XCTUnwrap(
            state.setProperty(
                path: "alpha.md",
                key: "status",
                value: .text(value: "Mine Add H1")))
        await gate.waitUntilEntered()

        state.openFile("beta.md", target: .currentTab)
        await state.noteLoadTask?.value
        try "---\ntitle: External H2\n---\n# Alpha\n".write(
            to: vault.appendingPathComponent("alpha.md"),
            atomically: true,
            encoding: .utf8)
        await gate.release()
        await edit.value
        state.basesPostWritePublishGate = nil

        XCTAssertEqual(state.loadedFilePath, "beta.md")
        XCTAssertTrue(state.hasAuthoredPropertyPublicationRecovery(for: "alpha.md"))
        state.openFile("alpha.md", target: .currentTab)
        await state.noteLoadTask?.value
        let recovery = try XCTUnwrap(state.activePropertyPublicationRecoveryText)
        XCTAssertTrue(recovery.contains("Property: status"))
        XCTAssertTrue(recovery.contains("Action: Set"))
        XCTAssertTrue(recovery.contains("Mine Add H1"))
    }

    func testInactivePropertyEditOwnerCannotCloseDuringPostWriteVerification()
        async throws
    {
        let vault = try makeVault("A")
        let state = try await makeState(vault: vault)
        let alphaTab = try XCTUnwrap(
            state.workspace.model.activeGroup.activeTabID)
        let gate = AsyncGate()
        state.basesPostWritePublishGate = { await gate.suspend() }
        defer { state.basesPostWritePublishGate = nil }

        let edit = try XCTUnwrap(
            state.setProperty(
                path: "alpha.md",
                key: "title",
                value: .text(value: "Mine")))
        await gate.waitUntilEntered()

        // Native I/O succeeded, so same-vault navigation may proceed while
        // authoritative publication is still pending. Alpha remains the
        // exact owner whose final tab must survive that verification window.
        state.openFile("beta.md", target: .newTab)
        await state.noteLoadTask?.value
        XCTAssertEqual(state.loadedFilePath, "beta.md")

        state.requestCloseTab(alphaTab)
        XCTAssertNotNil(state.workspace.model.tab(alphaTab))
        XCTAssertNil(state.pendingTabClose)
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            Self.propertyEditInProgressReason)

        // The close primitive is also used by internal funnels. It must carry
        // the same final-owner belt instead of relying only on the UI request.
        state.performCloseTab(alphaTab)
        XCTAssertNotNil(state.workspace.model.tab(alphaTab))

        await gate.release()
        await edit.value

        state.requestCloseTab(alphaTab)
        XCTAssertNil(
            state.workspace.model.tab(alphaTab),
            "the path-scoped guard must release when verification finishes")
    }

    func testRecoveryCopyDistinguishesFullSourceReplacementFromPerKeyEdit()
        async throws
    {
        let sourceVault = try makeVault("source")
        let sourceState = try await makeState(vault: sourceVault)
        try await createSourceH1ThenExternalH2Race(
            sourceState, vault: sourceVault)
        XCTAssertTrue(
            sourceState.activePropertyPublicationRecoveryReplacesAllProperties)

        let rowVault = try makeVault("row")
        let rowState = try await makeState(vault: rowVault)
        try await createSetH1ThenExternalH2Race(rowState, vault: rowVault)
        XCTAssertFalse(
            rowState.activePropertyPublicationRecoveryReplacesAllProperties)
    }

    func testVaultRecoveryReasonDisambiguatesDuplicateBasenames() async throws {
        let vault = try makeVault(
            "A",
            files: [
                "a/note.md": "---\ntitle: A\n---\n# A\n",
                "b/note.md": "---\ntitle: B\n---\n# B\n",
            ])
        let state = try await makeState(vault: vault, path: "a/note.md")
        try await createSetH1ThenExternalH2Race(
            state, vault: vault, path: "a/note.md")
        state.resolvePropertyEditConflictCancel()

        state.openFile("b/note.md", target: .newTab)
        await state.noteLoadTask?.value
        try await createSetH1ThenExternalH2Race(
            state, vault: vault, path: "b/note.md")

        let reason = try XCTUnwrap(
            state.authoredPropertyPublicationRecoveryReason)
        XCTAssertTrue(reason.contains("a/note.md"))
        XCTAssertTrue(reason.contains("b/note.md"))
        XCTAssertFalse(reason.contains("note.md, note.md"))
    }

    func testAuthoredH1RecoveryBlocksFinalCloseNavigationAndEveryVaultSwitch()
        async throws
    {
        let vaultA = try makeVault("A")
        let vaultB = try makeVault("B", files: ["fresh.md": "# Fresh\n"])
        let state = try await makeState(vault: vaultA)
        try await createSetH1ThenExternalH2Race(state, vault: vaultA)
        state.resolvePropertyEditConflictCancel()
        let tab = try XCTUnwrap(state.workspace.model.activeGroup.activeTabID)

        XCTAssertTrue(state.hasAuthoredPropertyPublicationRecovery(for: "alpha.md"))
        XCTAssertTrue(
            try XCTUnwrap(state.activePropertyPublicationRecoveryText)
                .contains("Mine H1"))
        XCTAssertNotNil(state.structuralMutationDisabledReason)

        state.requestCloseTab(tab)
        XCTAssertNotNil(state.workspace.model.tab(tab))
        XCTAssertNil(state.pendingTabClose)

        state.openCanvasFile("missing.canvas", target: .currentTab)
        state.openBaseFile("missing.base", target: .currentTab)
        state.openSavedQuery(id: "missing-query", name: "Missing", target: .currentTab)
        state.openDashboard(id: "missing-dashboard", name: "Missing", target: .currentTab)
        XCTAssertEqual(
            state.workspace.model.tab(tab)?.item,
            .markdown(path: "alpha.md"),
            "every direct current-tab router must preserve the final recovery owner")

        state.selectedFilePath = "beta.md"
        await Task.yield()
        await Task.yield()
        XCTAssertEqual(state.loadedFilePath, "alpha.md")
        XCTAssertEqual(state.selectedFilePath, "alpha.md")

        state.closeVaultFromUserAction()
        XCTAssertEqual(state.currentVaultURL?.path, vaultA.path)
        XCTAssertNil(state.pendingVaultClose)

        let recentB = RecentVault(
            path: vaultB.path, displayName: "B", lastOpenedMs: 0)
        state.switchToRecent(recentB)
        XCTAssertEqual(state.currentVaultURL?.path, vaultA.path)
        XCTAssertNil(state.pendingVaultSwitchTarget)

        state.openVault(at: vaultB)
        XCTAssertEqual(state.currentVaultURL?.path, vaultA.path)
        XCTAssertTrue(state.hasAuthoredPropertyPublicationRecovery(for: "alpha.md"))
    }

    func testDuplicateMayCloseButFinalH1RecoveryOwnerMustRemainOpen() async throws {
        let vault = try makeVault("A")
        let state = try await makeState(vault: vault)
        try await createSetH1ThenExternalH2Race(state, vault: vault)
        state.resolvePropertyEditConflictCancel()
        let first = try XCTUnwrap(state.workspace.model.activeGroup.activeTabID)
        state.newTab()
        let duplicate = try XCTUnwrap(state.workspace.model.activeGroup.activeTabID)
        XCTAssertNotEqual(first, duplicate)

        state.requestCloseTab(duplicate)
        XCTAssertNil(state.workspace.model.tab(duplicate))
        XCTAssertNotNil(state.workspace.model.tab(first))
        XCTAssertTrue(state.hasAuthoredPropertyPublicationRecovery(for: "alpha.md"))

        state.requestCloseTab(first)
        XCTAssertNotNil(state.workspace.model.tab(first))
        XCTAssertTrue(state.hasAuthoredPropertyPublicationRecovery(for: "alpha.md"))
    }

    func testUseCurrentVersionLoadsH2ThenReleasesRecoveryAndAllowsClose()
        async throws
    {
        let vault = try makeVault("A")
        let state = try await makeState(vault: vault)
        try await createSetH1ThenExternalH2Race(state, vault: vault)
        state.resolvePropertyEditConflictCancel()
        let tab = try XCTUnwrap(state.workspace.model.activeGroup.activeTabID)

        let resolution = try XCTUnwrap(
            state.useCurrentVersionForActivePropertyPublication())
        await resolution.value

        XCTAssertFalse(state.hasAuthoredPropertyPublicationRecovery(for: "alpha.md"))
        XCTAssertNil(state.activePropertyPublicationRecoveryText)
        XCTAssertEqual(
            state.currentNoteProperties.first(where: { $0.key == "title" })?.valueJson,
            "\"External H2\"")
        state.requestCloseTab(tab)
        XCTAssertNil(state.workspace.model.tab(tab))
    }

    func testReapplyMineUsesFreshH2HashAndVerifiesNewH1Version() async throws {
        let vault = try makeVault("A")
        let state = try await makeState(vault: vault)
        try await createSetH1ThenExternalH2Race(state, vault: vault)
        state.resolvePropertyEditConflictCancel()

        let resolution = try XCTUnwrap(state.reapplyActivePropertyPublication())
        await resolution.value

        let disk = try String(
            contentsOf: vault.appendingPathComponent("alpha.md"),
            encoding: .utf8)
        XCTAssertTrue(disk.contains("title: Mine H1"))
        XCTAssertFalse(state.hasAuthoredPropertyPublicationRecovery(for: "alpha.md"))
        XCTAssertNil(state.currentPropertyEditConflict)
    }

    func testSourceH1RemainsSelectableAndCannotBeOrdinarilyDiscarded() async throws {
        let vault = try makeVault("A")
        let state = try await makeState(vault: vault)
        try await createSourceH1ThenExternalH2Race(state, vault: vault)
        state.resolvePropertyEditConflictCancel()
        let tab = try XCTUnwrap(state.workspace.model.activeGroup.activeTabID)

        let retained = try XCTUnwrap(state.activePropertyPublicationRecoveryText)
        XCTAssertTrue(retained.contains("Mine Source H1"))
        XCTAssertNotNil(
            state.propertiesSourceDraftDiscardDisabledReason(
                path: "alpha.md", draft: state.propertiesSourceDraft))
        state.discardRecoverablePropertyDrafts(for: "alpha.md")
        XCTAssertTrue(state.hasAuthoredPropertyPublicationRecovery(for: "alpha.md"))
        XCTAssertEqual(state.propertiesSourceDraft, "title: Mine Source H1\nstatus: retained\n")
        state.requestCloseTab(tab)
        XCTAssertNotNil(state.workspace.model.tab(tab))
    }

    func testDeleteAndAddActionsRetainIntentWithoutARowDraft() async throws {
        let deleteVault = try makeVault("Delete")
        let deleteState = try await makeState(vault: deleteVault)
        let deleteGate = AsyncGate()
        deleteState.basesPostWritePublishGate = { await deleteGate.suspend() }
        let deleteTask = try XCTUnwrap(
            deleteState.deleteProperty(path: "alpha.md", key: "title"))
        await deleteGate.waitUntilEntered()
        try "---\ntitle: External Delete H2\n---\n# Alpha\n".write(
            to: deleteVault.appendingPathComponent("alpha.md"),
            atomically: true,
            encoding: .utf8)
        await deleteGate.release()
        await deleteTask.value
        deleteState.basesPostWritePublishGate = nil
        deleteState.resolvePropertyEditConflictCancel()
        XCTAssertTrue(
            try XCTUnwrap(deleteState.activePropertyPublicationRecoveryText)
                .contains("Action: Delete"))

        let addVault = try makeVault("Add")
        let addState = try await makeState(vault: addVault)
        try await createSetH1ThenExternalH2Race(
            addState,
            vault: addVault,
            key: "new-property",
            mine: "Added H1",
            retainRowDraft: false)
        addState.resolvePropertyEditConflictCancel()
        let addRecovery = try XCTUnwrap(
            addState.activePropertyPublicationRecoveryText)
        XCTAssertTrue(addRecovery.contains("Property: new-property"))
        XCTAssertTrue(addRecovery.contains("Added H1"))
        XCTAssertTrue(addState.hasAuthoredPropertyPublicationRecovery(for: "alpha.md"))
    }

    func testMissingPostWriteReadRetainsH1UntilThePathReturns() async throws {
        let vault = try makeVault("A")
        let state = try await makeState(vault: vault)
        let draft = PropertyEditDraft.scalarText(
            ScalarTextKind(kind: "text", value: "Mine Missing H1"))
        state.preservePropertyDraft(draft, path: "alpha.md", key: "title")
        let gate = AsyncGate()
        state.basesPostWritePublishGate = { await gate.suspend() }
        let task = try XCTUnwrap(
            state.setProperty(
                path: "alpha.md",
                key: "title",
                value: .text(value: "Mine Missing H1"),
                submittedDraft: draft))
        await gate.waitUntilEntered()
        let path = vault.appendingPathComponent("alpha.md")
        let holding = vault.deletingLastPathComponent()
            .appendingPathComponent("alpha-holding-\(UUID().uuidString)")
        try FileManager.default.moveItem(at: path, to: holding)
        await gate.release()
        await task.value
        state.basesPostWritePublishGate = nil

        try "---\ntitle: Returned H2\n---\n# Alpha\n".write(
            to: path, atomically: true, encoding: .utf8)
        try? FileManager.default.removeItem(at: holding)
        let tab = try XCTUnwrap(state.workspace.model.activeGroup.activeTabID)
        XCTAssertTrue(state.hasAuthoredPropertyPublicationRecovery(for: "alpha.md"))
        XCTAssertTrue(
            try XCTUnwrap(state.activePropertyPublicationRecoveryText)
                .contains("Mine Missing H1"))
        state.requestCloseTab(tab)
        XCTAssertNotNil(state.workspace.model.tab(tab))

        let retry = try XCTUnwrap(state.retryActivePropertyPublication())
        await retry.value
        XCTAssertNotNil(state.currentPropertyEditConflict)
        XCTAssertTrue(state.hasAuthoredPropertyPublicationRecovery(for: "alpha.md"))
    }

    func testBulkRenameRetryAdoptsH2AndReleasesDirtyBodySave() async throws {
        let vault = try makeVault(
            "Bulk",
            files: [
                "alpha.md": "---\nauthor: Original\n---\n# Alpha\n"
            ])
        let state = try await makeState(vault: vault)
        state.updateEditorText("# Unsaved body remains mine\n")
        let gate = AsyncGate()
        state.renamePostWritePublishGate = { await gate.suspend() }
        defer { state.renamePostWritePublishGate = nil }

        let rename = try XCTUnwrap(
            state.applyPropertyRename(oldKey: "author", newKey: "by"))
        await gate.waitUntilEntered()
        try "---\nby: External H2\n---\n# External body\n".write(
            to: vault.appendingPathComponent("alpha.md"),
            atomically: true,
            encoding: .utf8)
        await gate.release()
        await rename.value

        XCTAssertNotNil(state.activePropertyPublicationUncertaintyReason)
        XCTAssertNotNil(state.activeNoteSaveDisabledReason)
        XCTAssertTrue(state.hasUnsavedChanges)
        XCTAssertEqual(state.currentNoteText, "# Unsaved body remains mine\n")

        let retry = try XCTUnwrap(state.retryActivePropertyPublication())
        await retry.value

        XCTAssertNil(state.activePropertyPublicationUncertaintyReason)
        XCTAssertNil(state.activeNoteSaveDisabledReason)
        XCTAssertTrue(state.hasUnsavedChanges)
        XCTAssertEqual(state.currentNoteText, "# Unsaved body remains mine\n")
        XCTAssertEqual(
            state.currentNoteProperties.first(where: { $0.key == "by" })?
                .valueJson,
            "\"External H2\"")
    }
}
