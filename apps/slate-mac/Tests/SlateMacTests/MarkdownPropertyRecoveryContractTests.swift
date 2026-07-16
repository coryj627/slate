// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// User-authored Markdown property recovery is part of the note's dirty state.
/// Structural destinations must not acquire one path while recovery bytes still
/// belong to another, and close/save flows must never report a body-only save as
/// having committed a property editor's independent draft.
@MainActor
final class MarkdownPropertyRecoveryContractTests: XCTestCase {
    private static let structuralRecoveryDestinationReason =
        "Resolve or discard the uncommitted note or property draft at the destination before continuing."
    private static let propertyDraftSaveReason =
        "Apply or discard the uncommitted property changes before saving the note."
    private static let structuralBusyReason =
        "Wait for the current file operation to finish."
    private static let saveInProgressReason =
        "Wait for the current save to finish."

    private var roots: [URL] = []

    private actor CallProbe {
        private var calls = 0

        func record() { calls += 1 }
        func count() -> Int { calls }
    }

    private actor AsyncGate {
        private var entered = false
        private var entranceWaiters: [CheckedContinuation<Void, Never>] = []
        private var releaseWaiter: CheckedContinuation<Void, Never>?

        func suspend() async {
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

    private struct Fixture {
        let state: AppState
        let vault: URL
    }

    override func tearDown() {
        for root in roots {
            try? FileManager.default.removeItem(at: root)
        }
        roots = []
        super.tearDown()
    }

    private func makeFixture(activePath: String = "alpha.md") async throws -> Fixture {
        let root = FileManager.default.temporaryDirectory
            .appendingPathComponent("markdown-property-recovery-\(UUID().uuidString)")
        roots.append(root)
        let vault = root.appendingPathComponent("vault")
        try FileManager.default.createDirectory(
            at: vault.appendingPathComponent("folder"),
            withIntermediateDirectories: true)
        try FileManager.default.createDirectory(
            at: vault.appendingPathComponent("dest"),
            withIntermediateDirectories: true)
        try "---\ntitle: Alpha\n---\n# Alpha\n".write(
            to: vault.appendingPathComponent("alpha.md"),
            atomically: true,
            encoding: .utf8)
        try "# Beta\n".write(
            to: vault.appendingPathComponent("beta.md"),
            atomically: true,
            encoding: .utf8)
        try "# Source\n".write(
            to: vault.appendingPathComponent("folder/source.md"),
            atomically: true,
            encoding: .utf8)
        try "# Moving\n".write(
            to: vault.appendingPathComponent("moving.md"),
            atomically: true,
            encoding: .utf8)
        try """
            {"nodes":[
            {"id":"card","type":"text","text":"Canvas draft","x":0,"y":0,"width":200,"height":100}
            ],"edges":[]}
            """.write(
                to: vault.appendingPathComponent("draft.canvas"),
                atomically: true,
                encoding: .utf8)

        let state = AppState(
            recentsStore: RecentVaultsStore(
                fileURL: root.appendingPathComponent("recents.json")),
            externalOpener: { _ in true })
        state.openVault(at: vault)
        await state.scanTask?.value
        state.openFile(activePath, target: .currentTab)
        await state.noteLoadTask?.value
        XCTAssertEqual(state.loadedFilePath, activePath)
        XCTAssertFalse(state.hasUnsavedChanges)
        return Fixture(state: state, vault: vault)
    }

    private func installRowDraft(_ state: AppState, path: String) {
        state.preservePropertyDraft(
            .scalarText(
                ScalarTextKind(kind: "text", value: "user-owned destination draft")),
            path: path,
            key: "title")
    }

    private func installRowDraft(
        _ state: AppState,
        path: String,
        value: String
    ) -> PropertyEditDraft {
        let draft = PropertyEditDraft.scalarText(
            ScalarTextKind(kind: "text", value: value))
        state.preservePropertyDraft(draft, path: path, key: "title")
        return draft
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

    func testRenameRejectsDestinationPropertyDraftBeforeNativeRunner() async throws {
        let fixture = try await makeFixture()
        let state = fixture.state
        let probe = CallProbe()
        installRowDraft(state, path: "folder/renamed.md")
        state.structuralRenameRunner = { _, _, _, _ in
            await probe.record()
            throw VaultError.DestinationExists(path: "folder/renamed.md")
        }

        let task = state.renameEntry(
            path: "folder/source.md",
            isDirectory: false,
            to: "renamed.md")
        await task?.value
        let nativeCalls = await probe.count()

        XCTAssertNil(task)
        XCTAssertEqual(nativeCalls, 0, "admission must stop before native mutation I/O")
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            Self.structuralRecoveryDestinationReason)
        XCTAssertNotNil(
            state.preservedPropertyDraft(path: "folder/renamed.md", key: "title"))
        XCTAssertTrue(
            FileManager.default.fileExists(
                atPath: fixture.vault.appendingPathComponent("folder/source.md").path))
    }

    func testMoveRejectsDestinationPropertyDraftBeforeFilesystemMutation() async throws {
        let fixture = try await makeFixture()
        let state = fixture.state
        installRowDraft(state, path: "dest/source.md")

        let task = state.moveEntry(
            path: "folder/source.md",
            isDirectory: false,
            to: "dest")
        await task?.value

        XCTAssertNil(task)
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            Self.structuralRecoveryDestinationReason)
        XCTAssertTrue(
            FileManager.default.fileExists(
                atPath: fixture.vault.appendingPathComponent("folder/source.md").path))
        XCTAssertFalse(
            FileManager.default.fileExists(
                atPath: fixture.vault.appendingPathComponent("dest/source.md").path))
        XCTAssertNotNil(
            state.preservedPropertyDraft(path: "dest/source.md", key: "title"))
    }

    func testMoveRejectsExternallyDeletedDestinationWithActiveDirtyBody()
        async throws
    {
        let fixture = try await makeFixture()
        let state = fixture.state
        let destination = fixture.vault.appendingPathComponent("dest/source.md")
        try "# Destination baseline\n".write(
            to: destination, atomically: true, encoding: .utf8)
        await state.loadFiles()
        state.openFile("dest/source.md", target: .currentTab)
        await state.noteLoadTask?.value
        state.updateEditorText("# Active destination draft\n")
        try FileManager.default.removeItem(at: destination)
        await state.loadFiles()

        let task = state.moveEntry(
            path: "folder/source.md",
            isDirectory: false,
            to: "dest")
        await task?.value

        XCTAssertNil(task)
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            Self.structuralRecoveryDestinationReason)
        XCTAssertEqual(state.loadedFilePath, "dest/source.md")
        XCTAssertEqual(state.currentNoteText, "# Active destination draft\n")
        XCTAssertTrue(state.hasUnsavedChanges)
        XCTAssertTrue(
            FileManager.default.fileExists(
                atPath: fixture.vault.appendingPathComponent(
                    "folder/source.md").path))
    }

    func testMoveRejectsExternallyDeletedDestinationWithParkedDirtyBody()
        async throws
    {
        let fixture = try await makeFixture()
        let state = fixture.state
        let destination = fixture.vault.appendingPathComponent("dest/source.md")
        try "# Destination baseline\n".write(
            to: destination, atomically: true, encoding: .utf8)
        await state.loadFiles()
        state.openFile("dest/source.md", target: .currentTab)
        await state.noteLoadTask?.value
        state.updateEditorText("# Parked destination draft\n")
        state.newTab()
        state.openFile("alpha.md", target: .currentTab)
        await state.noteLoadTask?.value
        XCTAssertEqual(state.workspace.dirtyParkedDocuments().count, 1)
        try FileManager.default.removeItem(at: destination)
        await state.loadFiles()

        let task = state.moveEntry(
            path: "folder/source.md",
            isDirectory: false,
            to: "dest")
        await task?.value

        XCTAssertNil(task)
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            Self.structuralRecoveryDestinationReason)
        XCTAssertEqual(
            state.workspace.dirtyParkedDocuments().first?.text,
            "# Parked destination draft\n")
        XCTAssertTrue(
            FileManager.default.fileExists(
                atPath: fixture.vault.appendingPathComponent(
                    "folder/source.md").path))
    }

    func testCreateNoteRejectsRecoveryAtComputedDestinationBeforeWrite() async throws {
        let fixture = try await makeFixture()
        let state = fixture.state
        installRowDraft(state, path: "Untitled.md")

        let task = state.createNote(in: "")
        await task?.value

        XCTAssertNil(task)
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            Self.structuralRecoveryDestinationReason)
        XCTAssertFalse(
            FileManager.default.fileExists(
                atPath: fixture.vault.appendingPathComponent("Untitled.md").path))
        XCTAssertNotNil(
            state.preservedPropertyDraft(path: "Untitled.md", key: "title"))
    }

    func testDuplicateRejectsDynamicRecoveryDestinationBeforeExclusiveCreate()
        async throws
    {
        let fixture = try await makeFixture()
        let state = fixture.state
        installRowDraft(state, path: "folder/source copy.md")

        let task = try XCTUnwrap(state.duplicateEntry(path: "folder/source.md"))
        await task.value

        XCTAssertEqual(
            state.lastMutationAnnouncement,
            Self.structuralRecoveryDestinationReason)
        XCTAssertFalse(
            FileManager.default.fileExists(
                atPath: fixture.vault.appendingPathComponent(
                    "folder/source copy.md").path))
        XCTAssertNotNil(
            state.preservedPropertyDraft(
                path: "folder/source copy.md", key: "title"))
    }

    func testNewCanvasRejectsRecoveryAtFirstDynamicCandidateBeforeExclusiveCreate()
        async throws
    {
        let fixture = try await makeFixture()
        let state = fixture.state
        installRowDraft(state, path: "Untitled Canvas.canvas")

        let task = try XCTUnwrap(state.canvasNewCanvasFile())
        await task.value

        XCTAssertEqual(
            state.lastMutationAnnouncement,
            Self.structuralRecoveryDestinationReason)
        XCTAssertFalse(
            FileManager.default.fileExists(
                atPath: fixture.vault.appendingPathComponent(
                    "Untitled Canvas.canvas").path))
        XCTAssertNotNil(
            state.preservedPropertyDraft(
                path: "Untitled Canvas.canvas", key: "title"))
    }

    func testNewCanvasRejectsRecoveryAtRetriedDynamicCandidateBeforeExclusiveCreate()
        async throws
    {
        let fixture = try await makeFixture()
        let state = fixture.state
        try "{}\n".write(
            to: fixture.vault.appendingPathComponent("Untitled Canvas.canvas"),
            atomically: true,
            encoding: .utf8)
        installRowDraft(state, path: "Untitled Canvas 2.canvas")

        let task = try XCTUnwrap(state.canvasNewCanvasFile())
        await task.value

        XCTAssertEqual(
            state.lastMutationAnnouncement,
            Self.structuralRecoveryDestinationReason)
        XCTAssertFalse(
            FileManager.default.fileExists(
                atPath: fixture.vault.appendingPathComponent(
                    "Untitled Canvas 2.canvas").path))
        XCTAssertNotNil(
            state.preservedPropertyDraft(
                path: "Untitled Canvas 2.canvas", key: "title"))
    }

    func testNewCanvasReservationCoversPostWriteRefresh() async throws {
        let fixture = try await makeFixture()
        let state = fixture.state
        let gate = AsyncGate()
        state.structuralBatchRefreshRunner = { _ in await gate.suspend() }

        let task = try XCTUnwrap(state.canvasNewCanvasFile())
        await gate.waitUntilEntered()

        XCTAssertEqual(
            state.noteAuthoringDisabledReason(for: "Untitled Canvas.canvas"),
            Self.structuralBusyReason)
        installRowDraft(state, path: "Untitled Canvas.canvas")
        XCTAssertNil(
            state.preservedPropertyDraft(
                path: "Untitled Canvas.canvas", key: "title"))

        await gate.release()
        await task.value

        XCTAssertNil(
            state.noteAuthoringDisabledReason(for: "Untitled Canvas.canvas"))
    }

    func testCentralReservationRejectsQueuedRowSourceAndWriterCallbacks()
        async throws
    {
        let fixture = try await makeFixture()
        let state = fixture.state
        state.propertiesSourceDraftPath = "alpha.md"
        state.propertiesSourceDraft = state.currentNoteFMSource
        let reservation = try XCTUnwrap(
            state.admitStructuralRecoveryDestination("alpha.md"))
        let token = state.beginStructuralMutation(
            recoveryReservation: reservation)

        XCTAssertEqual(
            state.noteAuthoringDisabledReason(for: "alpha.md"),
            Self.structuralBusyReason)
        state.updatePropertiesSourceDraft("title: must-not-land\n")
        XCTAssertEqual(state.propertiesSourceDraft, state.currentNoteFMSource)
        state.preservePropertyDraft(
            .scalarText(ScalarTextKind(kind: "text", value: "must not land")),
            path: "alpha.md",
            key: "queued")
        XCTAssertNil(state.preservedPropertyDraft(path: "alpha.md", key: "queued"))
        XCTAssertFalse(state.admitBatchTrashWrite(to: ["alpha.md"]))
        XCTAssertEqual(state.lastMutationAnnouncement, Self.structuralBusyReason)

        state.endStructuralMutation(token)
        XCTAssertNil(state.noteAuthoringDisabledReason(for: "alpha.md"))
        state.updatePropertiesSourceDraft("title: admitted after release\n")
        XCTAssertEqual(state.propertiesSourceDraft, "title: admitted after release\n")
    }

    func testVaultTransitionClearsOldReservationAndStaleOwnerCannotReleaseNewOne()
        async throws
    {
        let fixture = try await makeFixture()
        let state = fixture.state
        let oldReservation = try XCTUnwrap(
            state.admitStructuralRecoveryDestination("old-destination.md"))
        let oldToken = state.beginStructuralMutation(
            recoveryReservation: oldReservation)
        XCTAssertEqual(
            state.noteAuthoringDisabledReason(for: "old-destination.md"),
            Self.structuralBusyReason)

        let nextVault = fixture.vault.deletingLastPathComponent()
            .appendingPathComponent("reservation-next-vault")
        try FileManager.default.createDirectory(
            at: nextVault,
            withIntermediateDirectories: true)
        state.openVault(at: nextVault)
        await state.scanTask?.value
        XCTAssertNil(
            state.noteAuthoringDisabledReason(for: "old-destination.md"))

        let newReservation = try XCTUnwrap(
            state.admitStructuralRecoveryDestination("new-destination.md"))
        let newToken = state.beginStructuralMutation(
            recoveryReservation: newReservation)
        state.endStructuralMutation(oldToken)
        XCTAssertEqual(
            state.noteAuthoringDisabledReason(for: "new-destination.md"),
            Self.structuralBusyReason)
        state.endStructuralMutation(newToken)
        XCTAssertNil(
            state.noteAuthoringDisabledReason(for: "new-destination.md"))
    }

    func testCreateNoteReservesDestinationThroughPostWriteRefresh() async throws {
        let fixture = try await makeFixture()
        let state = fixture.state
        let gate = AsyncGate()
        state.structuralBatchRefreshRunner = { _ in await gate.suspend() }

        let task = try XCTUnwrap(state.createNote(in: ""))
        XCTAssertEqual(
            state.noteAuthoringDisabledReason(for: "Untitled.md"),
            Self.structuralBusyReason)
        state.preservePropertyDraft(
            .scalarText(ScalarTextKind(kind: "text", value: "queued")),
            path: "Untitled.md",
            key: "title")
        XCTAssertNil(
            state.preservedPropertyDraft(path: "Untitled.md", key: "title"))

        await gate.waitUntilEntered()
        XCTAssertEqual(
            state.noteAuthoringDisabledReason(for: "Untitled.md"),
            Self.structuralBusyReason)
        await gate.release()
        await task.value

        XCTAssertNil(state.noteAuthoringDisabledReason(for: "Untitled.md"))
        XCTAssertTrue(
            FileManager.default.fileExists(
                atPath: fixture.vault.appendingPathComponent("Untitled.md").path))
    }

    func testRenameReservationBlocksDestinationButAllowsAndRekeysSourceRecovery()
        async throws
    {
        let fixture = try await makeFixture()
        let state = fixture.state
        let gate = AsyncGate()
        state.structuralRenameRunner = { session, path, isDirectory, newName in
            await gate.suspend()
            return isDirectory
                ? try session.renameFolder(path: path, newName: newName)
                : try session.renameFile(path: path, newName: newName)
        }

        let task = try XCTUnwrap(
            state.renameEntry(
                path: "folder/source.md",
                isDirectory: false,
                to: "renamed.md"))
        await gate.waitUntilEntered()

        XCTAssertNil(state.noteAuthoringDisabledReason(for: "folder/source.md"))
        XCTAssertEqual(
            state.noteAuthoringDisabledReason(for: "folder/renamed.md"),
            Self.structuralBusyReason)
        installRowDraft(state, path: "folder/source.md")
        installRowDraft(state, path: "folder/renamed.md")
        XCTAssertNotNil(
            state.preservedPropertyDraft(path: "folder/source.md", key: "title"))
        XCTAssertNil(
            state.preservedPropertyDraft(path: "folder/renamed.md", key: "title"))

        await gate.release()
        await task.value

        XCTAssertNil(state.noteAuthoringDisabledReason(for: "folder/renamed.md"))
        XCTAssertNil(
            state.preservedPropertyDraft(path: "folder/source.md", key: "title"))
        XCTAssertNotNil(
            state.preservedPropertyDraft(path: "folder/renamed.md", key: "title"))
    }

    func testAdmittedRenameKeepsSourceDraftButRejectsNativePropertyCommit()
        async throws
    {
        let fixture = try await makeFixture(activePath: "folder/source.md")
        let state = fixture.state
        let gate = AsyncGate()
        state.structuralRenameRunner = { session, path, isDirectory, newName in
            await gate.suspend()
            return isDirectory
                ? try session.renameFolder(path: path, newName: newName)
                : try session.renameFile(path: path, newName: newName)
        }
        let rename = try XCTUnwrap(
            state.renameEntry(
                path: "folder/source.md",
                isDirectory: false,
                to: "renamed.md"))
        await gate.waitUntilEntered()
        let draft = installRowDraft(
            state, path: "folder/source.md", value: "Retained while moving")

        let propertyCommit = state.setProperty(
            path: "folder/source.md",
            key: "title",
            value: .text(value: "Retained while moving"),
            submittedDraft: draft)
        await propertyCommit?.value

        XCTAssertNil(propertyCommit)
        XCTAssertEqual(state.lastMutationAnnouncement, Self.structuralBusyReason)
        XCTAssertEqual(
            state.preservedPropertyDraft(
                path: "folder/source.md", key: "title"),
            draft)

        await gate.release()
        await rename.value
        XCTAssertNil(
            state.preservedPropertyDraft(
                path: "folder/source.md", key: "title"))
        XCTAssertEqual(
            state.preservedPropertyDraft(
                path: "folder/renamed.md", key: "title"),
            draft)
    }

    func testPropertyConflictAndRetryIntentFollowRenamedNote() async throws {
        let fixture = try await makeFixture(activePath: "folder/source.md")
        let state = fixture.state
        let draft = installRowDraft(
            state, path: "folder/source.md", value: "Mine")
        try "---\ntitle: External\n---\n# Source\n".write(
            to: fixture.vault.appendingPathComponent("folder/source.md"),
            atomically: true,
            encoding: .utf8)
        let property = try XCTUnwrap(
            state.setProperty(
                path: "folder/source.md",
                key: "title",
                value: .text(value: "Mine"),
                submittedDraft: draft))
        await property.value
        XCTAssertEqual(
            state.currentPropertyEditConflict?.path,
            "folder/source.md")

        let rename = try XCTUnwrap(
            state.renameEntry(
                path: "folder/source.md",
                isDirectory: false,
                to: "renamed.md"))
        await rename.value

        XCTAssertEqual(state.loadedFilePath, "folder/renamed.md")
        XCTAssertEqual(
            state.currentPropertyEditConflict?.path,
            "folder/renamed.md")
        let retry = try XCTUnwrap(
            state.resolvePropertyEditConflictKeepMine())
        await retry.value
        XCTAssertNil(state.currentPropertyEditConflict)
        XCTAssertTrue(
            try String(
                contentsOf: fixture.vault.appendingPathComponent(
                    "folder/renamed.md"),
                encoding: .utf8
            ).contains("title: Mine"))
    }

    func testMoveAndBatchReservationsCoverTheirAsyncNativeWindows() async throws {
        let moveFixture = try await makeFixture()
        let moveState = moveFixture.state
        let move = try XCTUnwrap(
            moveState.moveEntry(
                path: "moving.md",
                isDirectory: false,
                to: "dest"))
        XCTAssertEqual(
            moveState.noteAuthoringDisabledReason(for: "dest/moving.md"),
            Self.structuralBusyReason)
        XCTAssertNil(moveState.noteAuthoringDisabledReason(for: "moving.md"))
        await move.value
        XCTAssertNil(moveState.noteAuthoringDisabledReason(for: "dest/moving.md"))

        let batchFixture = try await makeFixture()
        let batchState = batchFixture.state
        let gate = AsyncGate()
        batchState.batchMoveRunner = { session, request in
            await gate.suspend()
            return try await Task.detached {
                try session.batchMove(request: request)
            }.value
        }
        let batch = try XCTUnwrap(
            batchState.batchMove(
                [.init(path: "folder/source.md", isDirectory: false)],
                to: "dest",
                preferredFocusPath: nil))
        await gate.waitUntilEntered()
        XCTAssertEqual(
            batchState.noteAuthoringDisabledReason(for: "dest/source.md"),
            Self.structuralBusyReason)
        XCTAssertNil(
            batchState.noteAuthoringDisabledReason(for: "folder/source.md"))
        XCTAssertFalse(batchState.admitBatchTrashWrite(to: ["dest/source.md"]))
        await gate.release()
        await batch.value
        XCTAssertNil(
            batchState.noteAuthoringDisabledReason(for: "dest/source.md"))
    }

    func testDuplicateReservesItsDynamicCandidateBeforeExclusiveCreate()
        async throws
    {
        let fixture = try await makeFixture()
        let state = fixture.state
        let gate = AsyncGate()
        var observedCandidate: String?
        state.duplicateCandidateReservationGateForTesting = { candidate in
            observedCandidate = candidate
            await gate.suspend()
        }

        let task = try XCTUnwrap(state.duplicateEntry(path: "folder/source.md"))
        await gate.waitUntilEntered()
        let candidate = try XCTUnwrap(observedCandidate)
        XCTAssertEqual(candidate, "folder/source copy.md")
        XCTAssertEqual(
            state.noteAuthoringDisabledReason(for: candidate),
            Self.structuralBusyReason)
        XCTAssertNil(
            state.noteAuthoringDisabledReason(for: "folder/source copy.md-like"))
        XCTAssertNil(state.noteAuthoringDisabledReason(for: "folder/source.md"))
        installRowDraft(state, path: candidate)
        XCTAssertNil(state.preservedPropertyDraft(path: candidate, key: "title"))

        await gate.release()
        await task.value

        XCTAssertNil(state.noteAuthoringDisabledReason(for: candidate))
        XCTAssertTrue(
            FileManager.default.fileExists(
                atPath: fixture.vault.appendingPathComponent(candidate).path))
    }

    func testGraphAndBaseExtensionWritersRetainDestinationReservations()
        async throws
    {
        let graphFixture = try await makeFixture()
        let graphState = graphFixture.state
        let graphGate = AsyncGate()
        graphState.structuralBatchRefreshRunner = { _ in
            await graphGate.suspend()
        }
        let graph = try XCTUnwrap(
            graphState.createNoteFromGhost(targetRaw: "reserved ghost"))
        XCTAssertEqual(
            graphState.noteAuthoringDisabledReason(for: "reserved ghost.md"),
            Self.structuralBusyReason)
        await graphGate.waitUntilEntered()
        XCTAssertEqual(
            graphState.noteAuthoringDisabledReason(for: "reserved ghost.md"),
            Self.structuralBusyReason)
        await graphGate.release()
        await graph.value
        XCTAssertNil(
            graphState.noteAuthoringDisabledReason(for: "reserved ghost.md"))

        let baseFixture = try await makeFixture()
        let baseState = baseFixture.state
        let session = try XCTUnwrap(baseState.currentSession)
        let baseGate = AsyncGate()
        baseState.structuralBatchRefreshRunner = { _ in
            await baseGate.suspend()
        }
        let destination = baseFixture.vault.appendingPathComponent("base-export.md")
        let base = try XCTUnwrap(
            baseState.performBaseSavePanelWrite(
                text: "reserved",
                to: destination,
                originSession: session,
                successMessage: "saved",
                failurePrefix: "failed"))
        XCTAssertEqual(
            baseState.noteAuthoringDisabledReason(for: "base-export.md"),
            Self.structuralBusyReason)
        await baseGate.waitUntilEntered()
        XCTAssertEqual(
            baseState.noteAuthoringDisabledReason(for: "base-export.md"),
            Self.structuralBusyReason)
        await baseGate.release()
        await base.value
        XCTAssertNil(
            baseState.noteAuthoringDisabledReason(for: "base-export.md"))
    }

    func testFolderCreateTreatsRecoveryOnlyDescendantAsOccupied() async throws {
        let fixture = try await makeFixture()
        let state = fixture.state
        installRowDraft(state, path: "Recovered Folder/child.md")

        let task = state.createFolder(name: "Recovered Folder", in: "")
        await task?.value

        XCTAssertNil(task)
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            Self.structuralRecoveryDestinationReason)
        XCTAssertFalse(
            FileManager.default.fileExists(
                atPath: fixture.vault.appendingPathComponent("Recovered Folder").path))
    }

    func testBatchMoveRejectsAnyTopLevelDestinationRecoveryBeforeRunner()
        async throws
    {
        let fixture = try await makeFixture()
        let state = fixture.state
        installRowDraft(state, path: "dest/source.md")

        let task = state.batchMove(
            [.init(path: "folder/source.md", isDirectory: false)],
            to: "dest",
            preferredFocusPath: nil)
        await task?.value

        XCTAssertNil(task)
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            Self.structuralRecoveryDestinationReason)
        XCTAssertTrue(
            FileManager.default.fileExists(
                atPath: fixture.vault.appendingPathComponent("folder/source.md").path))
    }

    func testFolderRenameRekeysRegistryOnlyDescendantRecovery() async throws {
        let fixture = try await makeFixture()
        let state = fixture.state
        installRowDraft(state, path: "folder/missing.md")

        let task = try XCTUnwrap(
            state.renameEntry(
                path: "folder",
                isDirectory: true,
                to: "renamed"))
        await task.value

        XCTAssertNil(
            state.preservedPropertyDraft(path: "folder/missing.md", key: "title"))
        XCTAssertNotNil(
            state.preservedPropertyDraft(path: "renamed/missing.md", key: "title"))
    }

    func testMissingNoteRecoveryBlocksAnUnrelatedRenameDestination() async throws {
        let fixture = try await makeFixture(activePath: "beta.md")
        let state = fixture.state
        state.updateEditorText("# Beta recovery body\n")
        let staging = fixture.vault.deletingLastPathComponent()
            .appendingPathComponent("missing-destination-staging")
        try FileManager.default.createDirectory(
            at: staging,
            withIntermediateDirectories: true)
        try FileManager.default.moveItem(
            at: fixture.vault.appendingPathComponent("beta.md"),
            to: staging.appendingPathComponent("beta.md"))
        let unknownReport = unknownTrashReport(for: "beta.md")
        state.batchTrashRunner = { _, _ in unknownReport }
        state.structuralBatchRefreshRunner = { _ in }
        let deletion = try XCTUnwrap(
            state.batchDelete(
                [.init(path: "beta.md", isDirectory: false)],
                preferredFocusPath: "beta.md"))
        await deletion.value
        XCTAssertNotNil(state.missingNoteRecoveryDraft(for: "beta.md"))

        let rename = state.renameEntry(
            path: "moving.md",
            isDirectory: false,
            to: "beta.md")
        await rename?.value

        XCTAssertNil(rename)
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            Self.structuralRecoveryDestinationReason)
        XCTAssertNotNil(state.missingNoteRecoveryDraft(for: "beta.md"))
        XCTAssertTrue(
            FileManager.default.fileExists(
                atPath: fixture.vault.appendingPathComponent("moving.md").path))
    }

    func testBatchUndoRejectsRecoveryAtItsInverseDestinationAndKeepsHistory()
        async throws
    {
        let fixture = try await makeFixture()
        let state = fixture.state
        let move = try XCTUnwrap(
            state.batchMove(
                [.init(path: "folder/source.md", isDirectory: false)],
                to: "dest",
                preferredFocusPath: nil))
        await move.value
        XCTAssertEqual(state.structuralUndoStack.count, 1)
        installRowDraft(state, path: "folder/source.md")
        let probe = CallProbe()
        state.batchUndoMoveRunner = { _, _ in
            await probe.record()
            throw VaultError.Io(message: "must not run")
        }

        state.structuralUndo()
        await Task.yield()
        let nativeCalls = await probe.count()

        XCTAssertEqual(nativeCalls, 0)
        XCTAssertEqual(state.structuralUndoStack.count, 1)
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            Self.structuralRecoveryDestinationReason)
        XCTAssertTrue(
            FileManager.default.fileExists(
                atPath: fixture.vault.appendingPathComponent("dest/source.md").path))
        XCTAssertFalse(
            FileManager.default.fileExists(
                atPath: fixture.vault.appendingPathComponent("folder/source.md").path))
    }

    func testSaveCurrentNoteRejectsPropertyOnlyDraftWithoutClaimingSuccess()
        async throws
    {
        let fixture = try await makeFixture()
        let state = fixture.state
        installRowDraft(state, path: "alpha.md")

        let task = state.saveCurrentNote()
        await task?.value

        XCTAssertNil(task)
        XCTAssertEqual(state.lastMutationAnnouncement, Self.propertyDraftSaveReason)
        XCTAssertNotNil(state.preservedPropertyDraft(path: "alpha.md", key: "title"))
        XCTAssertFalse(state.hasUnsavedChanges, "body dirtiness remains a separate signal")
    }

    func testCommandPaletteSaveReportsPropertyDraftAsUnavailable() async throws {
        let fixture = try await makeFixture()
        let state = fixture.state
        installRowDraft(state, path: "alpha.md")

        XCTAssertThrowsError(
            try state.commandRegistry.invokeById(id: SlateCommandID.save)
        ) { error in
            guard case CommandError.ActionFailed(let message) = error else {
                XCTFail("expected property-draft availability failure, got \(error)")
                return
            }
            XCTAssertEqual(message, Self.propertyDraftSaveReason)
        }
        XCTAssertNotNil(state.preservedPropertyDraft(path: "alpha.md", key: "title"))
    }

    func testFileMenuAndToolbarUsePropertyAwareSaveState() throws {
        let sourceRoot = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .appendingPathComponent("Sources/SlateMac")
        let app = try String(
            contentsOf: sourceRoot.appendingPathComponent("SlateMacApp.swift"),
            encoding: .utf8)
        let split = try String(
            contentsOf: sourceRoot.appendingPathComponent("MainSplitView.swift"),
            encoding: .utf8)

        XCTAssertTrue(
            app.contains(
                "let noteSaveDisabledReason = appState.activeNoteSaveDisabledReason"))
        XCTAssertTrue(app.contains("!appState.activeNoteHasUnsavedChanges"))
        XCTAssertTrue(
            split.contains(
                "Text(appState.activeNoteHasUnsavedChanges ? \"Modified\" : \"Saved\")"))
        XCTAssertTrue(split.contains("appState.activeNoteSaveDisabledReason"))
        XCTAssertTrue(split.contains("Save changes before closing the vault?"))
        XCTAssertFalse(split.contains("open tabs have unsaved changes"))
    }

    func testActivePropertyOnlyTabCloseOffersNoFalseSaveAndExplicitDiscardClears()
        async throws
    {
        let fixture = try await makeFixture()
        let state = fixture.state
        let tab = try XCTUnwrap(state.workspace.model.activeGroup.activeTabID)
        installRowDraft(state, path: "alpha.md")

        state.requestCloseTab(tab)

        XCTAssertEqual(state.pendingTabClose, tab)
        XCTAssertEqual(
            state.pendingTabCloseSaveDisabledReason,
            Self.propertyDraftSaveReason)
        state.resolveTabCloseSave()
        await state.saveTask?.value
        XCTAssertEqual(state.pendingTabClose, tab)
        XCTAssertEqual(state.workspace.model.allTabs.count, 1)
        XCTAssertNotNil(state.preservedPropertyDraft(path: "alpha.md", key: "title"))

        state.resolveTabCloseDiscard()

        XCTAssertNil(state.pendingTabClose)
        XCTAssertTrue(state.workspace.model.allTabs.isEmpty)
        XCTAssertNil(state.preservedPropertyDraft(path: "alpha.md", key: "title"))
    }

    func testPropertiesSourceOnlyTabCloseRequiresExplicitDiscard() async throws {
        let fixture = try await makeFixture()
        let state = fixture.state
        let tab = try XCTUnwrap(state.workspace.model.activeGroup.activeTabID)
        state.propertiesSourceDraftPath = "alpha.md"
        state.propertiesSourceDraft = "title: source-only draft\n"

        state.requestCloseTab(tab)

        XCTAssertEqual(state.pendingTabClose, tab)
        XCTAssertEqual(
            state.pendingTabCloseSaveDisabledReason,
            Self.propertyDraftSaveReason)
        XCTAssertEqual(state.propertiesSourceDraftPath, "alpha.md")
        state.resolveTabCloseDiscard()
        XCTAssertTrue(state.workspace.model.allTabs.isEmpty)
        XCTAssertNil(state.propertiesSourceDraftPath)
        XCTAssertTrue(state.recoverablePropertyDraftPaths().isEmpty)
    }

    func testParkedPropertyOnlyTabCloseIsDirty() async throws {
        let fixture = try await makeFixture()
        let state = fixture.state
        let alpha = try XCTUnwrap(state.workspace.model.activeGroup.activeTabID)
        installRowDraft(state, path: "alpha.md")
        state.openFile("beta.md", target: .newTab)
        await state.noteLoadTask?.value

        state.requestCloseTab(alpha)

        XCTAssertEqual(state.pendingTabClose, alpha)
        XCTAssertEqual(
            state.pendingTabCloseSaveDisabledReason,
            Self.propertyDraftSaveReason)
        XCTAssertNotNil(state.workspace.model.tab(alpha))
        XCTAssertNotNil(state.preservedPropertyDraft(path: "alpha.md", key: "title"))
    }

    func testClosingOneDuplicateTabKeepsPathRecoveryForTheRemainingOwner()
        async throws
    {
        let fixture = try await makeFixture()
        let state = fixture.state
        let first = try XCTUnwrap(state.workspace.model.activeGroup.activeTabID)
        state.newTab()
        let duplicate = try XCTUnwrap(state.workspace.model.activeGroup.activeTabID)
        XCTAssertNotEqual(first, duplicate)
        installRowDraft(state, path: "alpha.md")

        state.requestCloseTab(duplicate)

        XCTAssertNil(state.pendingTabClose)
        XCTAssertNil(state.workspace.model.tab(duplicate))
        XCTAssertNotNil(state.workspace.model.tab(first))
        XCTAssertNotNil(state.preservedPropertyDraft(path: "alpha.md", key: "title"))

        state.requestCloseTab(first)
        XCTAssertEqual(state.pendingTabClose, first)
        XCTAssertNotNil(state.preservedPropertyDraft(path: "alpha.md", key: "title"))
    }

    func testVaultCloseCountsPropertyOnlyPathAndRequiresExplicitDiscard() async throws {
        let fixture = try await makeFixture()
        let state = fixture.state
        installRowDraft(state, path: "alpha.md")

        state.closeVaultFromUserAction()

        XCTAssertEqual(state.pendingVaultClose, 1)
        XCTAssertEqual(
            state.pendingVaultCloseSaveAllDisabledReason,
            Self.propertyDraftSaveReason)
        XCTAssertNotNil(state.currentSession)
        state.resolveVaultCloseSaveAll()
        XCTAssertEqual(state.pendingVaultClose, 1)
        XCTAssertNil(state.vaultCloseSaveAllTask)
        XCTAssertNotNil(state.preservedPropertyDraft(path: "alpha.md", key: "title"))

        state.resolveVaultCloseDiscardAll()

        XCTAssertNil(state.currentSession)
        XCTAssertNil(state.pendingVaultClose)
        XCTAssertTrue(state.recoverablePropertyDraftPaths().isEmpty)
    }

    func testVaultCloseCountsRegistryOnlyRecoveryWithoutAnOpenTab() async throws {
        let fixture = try await makeFixture()
        let state = fixture.state
        installRowDraft(state, path: "registry-only.md")

        state.closeVaultFromUserAction()

        XCTAssertEqual(state.pendingVaultClose, 1)
        XCTAssertEqual(
            state.pendingVaultCloseSaveAllDisabledReason,
            Self.propertyDraftSaveReason)
        XCTAssertNotNil(state.currentSession)
        XCTAssertNotNil(
            state.preservedPropertyDraft(path: "registry-only.md", key: "title"))
    }

    func testVaultCloseCountsDuplicateTabsByExactPathOnce() async throws {
        let fixture = try await makeFixture()
        let state = fixture.state
        state.newTab()
        XCTAssertEqual(state.workspace.model.allTabs.count, 2)
        installRowDraft(state, path: "alpha.md")

        state.closeVaultFromUserAction()

        XCTAssertEqual(state.pendingVaultClose, 1)
        XCTAssertNotNil(state.currentSession)
        XCTAssertEqual(
            state.pendingVaultCloseSaveAllDisabledReason,
            Self.propertyDraftSaveReason)
    }

    func testQueuedPropertyDraftCallbackIsRejectedAfterBodySaveTakesOwnership()
        async throws
    {
        let fixture = try await makeFixture()
        let state = fixture.state
        state.updateEditorText("# Must remain only in memory\n")
        let task = try XCTUnwrap(state.saveCurrentNote())
        installRowDraft(state, path: "alpha.md")

        await task.value

        XCTAssertFalse(state.hasUnsavedChanges)
        XCTAssertEqual(state.lastMutationAnnouncement, Self.saveInProgressReason)
        XCTAssertEqual(
            try String(
                contentsOf: fixture.vault.appendingPathComponent("alpha.md"),
                encoding: .utf8),
            "---\ntitle: Alpha\n---\n# Must remain only in memory\n")
        XCTAssertNil(state.preservedPropertyDraft(path: "alpha.md", key: "title"))
    }

    func testPropertyOnlyRecentVaultSwitchRequiresExplicitDiscard() async throws {
        let fixture = try await makeFixture()
        let state = fixture.state
        let secondVault = fixture.vault.deletingLastPathComponent()
            .appendingPathComponent("second-vault")
        try FileManager.default.createDirectory(
            at: secondVault,
            withIntermediateDirectories: true)
        installRowDraft(state, path: "alpha.md")

        state.switchToRecent(RecentVault(url: secondVault))

        XCTAssertEqual(state.currentVaultURL, fixture.vault)
        XCTAssertEqual(state.pendingVaultClose, 1)
        XCTAssertEqual(
            state.pendingVaultCloseSaveAllDisabledReason,
            Self.propertyDraftSaveReason)
        XCTAssertEqual(state.pendingVaultSwitchTarget?.path, secondVault.path)
        XCTAssertNotNil(state.preservedPropertyDraft(path: "alpha.md", key: "title"))

        state.resolveVaultCloseCancel()
        XCTAssertEqual(state.currentVaultURL, fixture.vault)
        XCTAssertNil(state.pendingVaultSwitchTarget)
        XCTAssertNotNil(state.preservedPropertyDraft(path: "alpha.md", key: "title"))

        state.switchToRecent(RecentVault(url: secondVault))
        state.resolveVaultCloseDiscardAll()
        await state.scanTask?.value

        XCTAssertEqual(
            state.currentVaultURL?.standardizedFileURL.path,
            secondVault.standardizedFileURL.path)
        XCTAssertTrue(state.recoverablePropertyDraftPaths().isEmpty)
    }

    func testOpenRecentRoutesThroughRecoveryAwareVaultSwitch() async throws {
        let fixture = try await makeFixture()
        let state = fixture.state
        let secondVault = fixture.vault.deletingLastPathComponent()
            .appendingPathComponent("open-recent-vault")
        try FileManager.default.createDirectory(
            at: secondVault,
            withIntermediateDirectories: true)
        installRowDraft(state, path: "alpha.md")

        state.openRecent(RecentVault(url: secondVault))

        XCTAssertEqual(state.currentVaultURL, fixture.vault)
        XCTAssertEqual(state.pendingVaultClose, 1)
        XCTAssertEqual(state.pendingVaultSwitchTarget?.path, secondVault.path)
        XCTAssertNotNil(state.preservedPropertyDraft(path: "alpha.md", key: "title"))
    }

    func testOpenVaultPickerRoutesThroughRecoveryAwareVaultSwitch() async throws {
        let fixture = try await makeFixture()
        let state = fixture.state
        let secondVault = fixture.vault.deletingLastPathComponent()
            .appendingPathComponent("picked-vault")
        try FileManager.default.createDirectory(
            at: secondVault,
            withIntermediateDirectories: true)
        installRowDraft(state, path: "alpha.md")
        state.vaultPicker = { secondVault }

        state.pickAndOpenVault()

        XCTAssertEqual(state.currentVaultURL, fixture.vault)
        XCTAssertEqual(state.pendingVaultClose, 1)
        XCTAssertEqual(state.pendingVaultSwitchTarget?.path, secondVault.path)
        XCTAssertNotNil(state.preservedPropertyDraft(path: "alpha.md", key: "title"))
    }

    func testPropertyRecoveryProjectsEditedStateForActiveAndParkedTabs() async throws {
        let fixture = try await makeFixture()
        let state = fixture.state
        let alpha = try XCTUnwrap(state.workspace.model.activeGroup.activeTabID)
        installRowDraft(state, path: "alpha.md")

        XCTAssertTrue(state.noteTabHasUnsavedChanges(alpha))

        state.openFile("beta.md", target: .newTab)
        await state.noteLoadTask?.value
        XCTAssertTrue(state.noteTabHasUnsavedChanges(alpha))

        let sourceRoot = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .appendingPathComponent("Sources/SlateMac")
        let tabBar = try String(
            contentsOf: sourceRoot.appendingPathComponent("Workspace/TabBarView.swift"),
            encoding: .utf8)
        XCTAssertTrue(tabBar.contains("appState.noteTabHasUnsavedChanges(tab.id)"))
    }

    func testKeepMineIsUnavailableWhilePropertyDraftRemainsAndRetainsConflict()
        async throws
    {
        let fixture = try await makeFixture()
        let state = fixture.state
        state.updateEditorText("# Mine\n")
        try "# External\n".write(
            to: fixture.vault.appendingPathComponent("alpha.md"),
            atomically: true,
            encoding: .utf8)
        try await XCTUnwrap(state.saveCurrentNote()).value
        let conflict = try XCTUnwrap(state.currentSaveConflict)
        installRowDraft(state, path: "alpha.md")

        XCTAssertEqual(
            state.saveConflictKeepMineDisabledReason,
            Self.propertyDraftSaveReason)
        XCTAssertNil(state.resolveSaveConflictKeepMine())
        XCTAssertEqual(state.currentSaveConflict, conflict)
        XCTAssertTrue(state.hasUnsavedChanges)
        XCTAssertEqual(
            try String(
                contentsOf: fixture.vault.appendingPathComponent("alpha.md"),
                encoding: .utf8),
            "# External\n")
        XCTAssertNotNil(state.preservedPropertyDraft(path: "alpha.md", key: "title"))
    }

    func testQueuedPropertyDraftCallbackIsRejectedAfterKeepMineTakesOwnership()
        async throws
    {
        let fixture = try await makeFixture()
        let state = fixture.state
        state.updateEditorText("# Mine\n")
        try "# External\n".write(
            to: fixture.vault.appendingPathComponent("alpha.md"),
            atomically: true,
            encoding: .utf8)
        try await XCTUnwrap(state.saveCurrentNote()).value
        XCTAssertNotNil(state.currentSaveConflict)

        let keepMine = try XCTUnwrap(state.resolveSaveConflictKeepMine())
        installRowDraft(state, path: "alpha.md")
        await keepMine.value

        XCTAssertNil(state.currentSaveConflict)
        XCTAssertEqual(state.lastMutationAnnouncement, Self.saveInProgressReason)
        XCTAssertFalse(state.hasUnsavedChanges)
        XCTAssertNil(state.preservedPropertyDraft(path: "alpha.md", key: "title"))
        XCTAssertTrue(
            try String(
                contentsOf: fixture.vault.appendingPathComponent("alpha.md"),
                encoding: .utf8).hasSuffix("# Mine\n"))
    }

    func testQueuedPropertyDraftCallbackCannotOutliveCloseSaveOwnership()
        async throws
    {
        let fixture = try await makeFixture()
        let state = fixture.state
        let tab = try XCTUnwrap(state.workspace.model.activeGroup.activeTabID)
        let gate = AsyncGate()
        state.updateEditorText("# Body save in flight\n")
        state.basesPostWritePublishGate = { await gate.suspend() }
        defer { state.basesPostWritePublishGate = nil }

        state.requestCloseTab(tab)
        state.resolveTabCloseSave()
        await gate.waitUntilEntered()
        installRowDraft(state, path: "alpha.md")
        await gate.release()
        await state.saveTask?.value

        XCTAssertNil(state.workspace.model.tab(tab))
        XCTAssertNil(state.pendingTabClose)
        XCTAssertEqual(state.lastMutationAnnouncement, Self.saveInProgressReason)
        XCTAssertNil(state.preservedPropertyDraft(path: "alpha.md", key: "title"))
    }

    func testHistoryGraphAndBaseWritersRejectRecoveryDestinationsSynchronously()
        async throws
    {
        let fixture = try await makeFixture()
        let state = fixture.state
        let session = try XCTUnwrap(state.currentSession)

        installRowDraft(state, path: "history.md")
        XCTAssertNil(state.requestRecoverDeleted(path: "history.md"))
        XCTAssertEqual(state.lastMutationAnnouncement, Self.structuralRecoveryDestinationReason)

        installRowDraft(state, path: "ghost.md")
        XCTAssertNil(state.createNoteFromGhost(targetRaw: "ghost"))
        XCTAssertEqual(state.lastMutationAnnouncement, Self.structuralRecoveryDestinationReason)
        XCTAssertFalse(
            FileManager.default.fileExists(
                atPath: fixture.vault.appendingPathComponent("ghost.md").path))

        installRowDraft(state, path: "query-export.md")
        XCTAssertNil(state.exportSavedQuery(id: "missing", path: "query-export.md"))
        XCTAssertEqual(state.lastMutationAnnouncement, Self.structuralRecoveryDestinationReason)

        installRowDraft(state, path: "panel-export.md")
        XCTAssertNil(
            state.performBaseSavePanelWrite(
                text: "must not write",
                to: fixture.vault.appendingPathComponent("panel-export.md"),
                originSession: session,
                successMessage: "saved",
                failurePrefix: "failed"))
        XCTAssertEqual(state.lastMutationAnnouncement, Self.structuralRecoveryDestinationReason)
        XCTAssertFalse(
            FileManager.default.fileExists(
                atPath: fixture.vault.appendingPathComponent("panel-export.md").path))
    }

    func testCanvasConvertRejectsPropertyRecoveryDestinationBeforeWriting() async throws {
        let fixture = try await makeFixture()
        let state = fixture.state
        state.openFile("draft.canvas", target: .currentTab)
        let document = try XCTUnwrap(state.activeCanvasDocument)
        XCTAssertNotNil(document.outline.first { $0.nodeId == "card" })
        installRowDraft(state, path: "Canvas draft.md")

        XCTAssertNil(
            state.canvasConvertToNote(
                nodeId: "card",
                path: "Canvas draft.md"))

        XCTAssertEqual(state.lastMutationAnnouncement, Self.structuralRecoveryDestinationReason)
        XCTAssertFalse(
            FileManager.default.fileExists(
                atPath: fixture.vault.appendingPathComponent("Canvas draft.md").path))
        XCTAssertEqual(document.outline.first { $0.nodeId == "card" }?.kind, "text")
    }
}
