// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Combine
import XCTest

@testable import SlateMac

/// FL-03 Task 5 C4: the four already-shipped file-creation funnels share the
/// same structural mutation admission and ownership as Move/Trash.
@MainActor
final class StructuralCreationGateTests: XCTestCase {
    private var tempDirs: [URL] = []

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

    private static let sourceRoot = URL(fileURLWithPath: #filePath)
        .deletingLastPathComponent()
        .deletingLastPathComponent()
        .deletingLastPathComponent()
        .appendingPathComponent("Sources/SlateMac")

    override func tearDown() {
        for dir in tempDirs {
            try? FileManager.default.removeItem(at: dir)
        }
        tempDirs = []
        super.tearDown()
    }

    private actor SuspensionGate {
        private var entered = false
        private var entries = 0
        private var entranceWaiters: [CheckedContinuation<Void, Never>] = []
        private var releaseWaiter: CheckedContinuation<Void, Never>?

        func enter() async {
            entered = true
            entries += 1
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

        func entryCount() -> Int { entries }
    }

    private actor Counter {
        private var value = 0

        func increment() { value += 1 }
        func count() -> Int { value }
    }

    private func makeVaultDirectory(files: [String: String]) throws -> URL {
        let dir = FileManager.default.temporaryDirectory
            .appendingPathComponent("structural-creates-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        tempDirs.append(dir)
        for (path, contents) in files {
            let url = dir.appendingPathComponent(path)
            try FileManager.default.createDirectory(
                at: url.deletingLastPathComponent(), withIntermediateDirectories: true)
            try contents.write(to: url, atomically: true, encoding: .utf8)
        }
        return dir
    }

    private func makeVault(files: [String: String]) async throws -> (AppState, URL) {
        let dir = try makeVaultDirectory(files: files)
        let state = AppState(
            recentsStore: nil,
            externalOpener: { _ in true },
            preferencesStore: PreferencesStore(
                defaults: UserDefaults(suiteName: UUID().uuidString)!))
        state.openVault(at: dir)
        await state.scanTask?.value
        return (state, dir)
    }

    private func blockingMove(on state: AppState) async throws
        -> (task: Task<Void, Never>, gate: SuspensionGate)
    {
        let gate = SuspensionGate()
        let item = StructuralBatchItem(path: "blocker.md", isDirectory: false)
        let report = BatchMoveReport(
            envelope: StructuralBatchEnvelope(
                planned: [item], skipped: [], preflightFailures: []),
            state: .succeeded,
            opId: 404,
            standing: [
                BatchPathChange(
                    oldPath: "blocker.md", newPath: "dest/blocker.md",
                    isDirectory: false)
            ],
            rolledBack: [],
            failure: nil,
            rollbackFailures: [],
            rewritten: [],
            rewriteFailures: [],
            requiresRescan: false)
        state.batchMoveRunner = { _, _ in
            await gate.enter()
            return report
        }
        state.structuralBatchRefreshRunner = { _ in }
        let task = try XCTUnwrap(
            state.batchMove(
                [.init(path: "blocker.md", isDirectory: false)],
                to: "dest", preferredFocusPath: "blocker.md"))
        await gate.waitUntilEntered()
        XCTAssertTrue(state.isMutatingStructure)
        return (task, gate)
    }

    private func mutationMessages(from state: AppState)
        -> (messages: () -> [String], cancellable: AnyCancellable)
    {
        final class Box { var values: [String] = [] }
        let box = Box()
        let cancellable = state.$lastMutationAnnouncement
            .dropFirst()
            .compactMap { $0 }
            .sink { box.values.append($0) }
        return ({ box.values }, cancellable)
    }

    private func drainMainQueue() async {
        await withCheckedContinuation { continuation in
            DispatchQueue.main.async { continuation.resume() }
        }
    }

    private func prepareRecoverableDeletedFile(
        on state: AppState,
        path: String = "gone.md",
        contents: String = "recover me\n"
    ) async throws {
        let session = try XCTUnwrap(state.currentSession)
        _ = try session.saveText(
            path: path, contents: contents, expectedContentHash: nil)
        try session.deleteFile(path: path)
        _ = try session.scanInitial(cancel: CancelToken())
        await state.loadDeletedFiles()
        XCTAssertEqual(state.deletedFiles.map(\.path), [path])
    }

    @discardableResult
    private func stageMissingFileSaveConflict(
        on state: AppState,
        vault: URL,
        path: String = "note.md",
        mine: String = "# My unsaved version\n"
    ) async throws -> SaveConflict {
        state.selectedFilePath = path
        await state.noteLoadTask?.value
        try FileManager.default.removeItem(at: vault.appendingPathComponent(path))
        state.updateEditorText(mine)
        await state.saveCurrentNote()?.value
        let conflict = try XCTUnwrap(state.currentSaveConflict)
        XCTAssertEqual(conflict.currentContentHash, "")
        return conflict
    }

    private func sourceSegment(
        file: String, from start: String, to end: String
    ) throws -> String {
        let source = try String(
            contentsOf: Self.sourceRoot.appendingPathComponent(file), encoding: .utf8)
        let startRange = try XCTUnwrap(source.range(of: start))
        let endRange = try XCTUnwrap(
            source.range(of: end, range: startRange.upperBound..<source.endIndex))
        return String(source[startRange.lowerBound..<endRange.lowerBound])
            .replacingOccurrences(
                of: #"\s+"#, with: " ", options: .regularExpression)
    }

    func testBusyBatchRejectsNewCanvasBeforeWriteAndRetrySucceeds() async throws {
        let (state, vault) = try await makeVault(files: [
            "blocker.md": "# blocker\n", "dest/keep.md": "# keep\n"
        ])
        let blocker = try await blockingMove(on: state)
        let capture = mutationMessages(from: state)
        defer { capture.cancellable.cancel() }

        state.canvasNewCanvasFile()

        XCTAssertFalse(
            FileManager.default.fileExists(
                atPath: vault.appendingPathComponent("Untitled Canvas.canvas").path))
        XCTAssertEqual(capture.messages(), [AppState.structuralMutationBusyReason])

        await blocker.gate.release()
        await blocker.task.value
        state.canvasNewCanvasFile()
        await state.pendingStructuralTaskForTesting?.value

        XCTAssertTrue(
            FileManager.default.fileExists(
                atPath: vault.appendingPathComponent("Untitled Canvas.canvas").path))
        XCTAssertFalse(state.isMutatingStructure)
    }

    func testBusyBatchRejectsTemplateCommitRetainsSheetAndRetrySucceeds() async throws {
        let summary = TemplateSummary(
            path: "Templates/Seed.md", name: "Seed", description: nil)
        let (state, vault) = try await makeVault(files: [
            "blocker.md": "# blocker\n",
            "dest/keep.md": "# keep\n",
            summary.path: "# {{title}}\n",
        ])
        state.installTemplateDestinationForTesting("")
        state.pendingTemplateFlow = .needsName(summary, [:])
        let blocker = try await blockingMove(on: state)
        let capture = mutationMessages(from: state)
        defer { capture.cancellable.cancel() }

        let rejected = state.submitTemplateNoteName("templated.md")
        await rejected?.value

        XCTAssertNil(rejected)
        XCTAssertEqual(state.pendingTemplateFlow, .needsName(summary, [:]))
        XCTAssertFalse(
            FileManager.default.fileExists(
                atPath: vault.appendingPathComponent("templated.md").path))
        XCTAssertEqual(capture.messages(), [AppState.structuralMutationBusyReason])

        await blocker.gate.release()
        await blocker.task.value
        let retry = try XCTUnwrap(state.submitTemplateNoteName("templated.md"))
        await retry.value

        XCTAssertEqual(state.pendingTemplateFlow, .idle)
        XCTAssertTrue(
            FileManager.default.fileExists(
                atPath: vault.appendingPathComponent("templated.md").path))
        XCTAssertFalse(state.isMutatingStructure)
    }

    func testBusyTemplateReturnRejectsBeforeInvalidNameMutatesPresentation() async throws {
        let summary = TemplateSummary(
            path: "Templates/Seed.md", name: "Seed", description: nil)
        let (state, _) = try await makeVault(files: [
            "blocker.md": "# blocker\n",
            "dest/keep.md": "# keep\n",
            summary.path: "# {{title}}\n",
        ])
        state.installTemplateDestinationForTesting("")
        state.pendingTemplateFlow = .needsName(summary, [:])
        state.templateNoteNameError = "Keep the existing validation state."
        let blocker = try await blockingMove(on: state)
        let capture = mutationMessages(from: state)
        defer { capture.cancellable.cancel() }

        let rejected = state.submitTemplateNoteName("../escape.md")

        XCTAssertNil(rejected)
        XCTAssertEqual(state.pendingTemplateFlow, .needsName(summary, [:]))
        XCTAssertEqual(state.templateNoteNameError, "Keep the existing validation state.")
        XCTAssertEqual(capture.messages(), [AppState.structuralMutationBusyReason])

        await blocker.gate.release()
        await blocker.task.value
    }

    func testTemplateCommitRequiresExactCurrentSessionDestinationOwnerBeforeWrite()
        async throws
    {
        let summary = TemplateSummary(
            path: "Templates/Seed.md", name: "Seed", description: nil)
        let (state, vault) = try await makeVault(files: [
            summary.path: "# {{title}}\n",
        ])
        state.pendingTemplateFlow = .needsName(summary, [:])

        XCTAssertNil(state.submitTemplateNoteName("must-not-write.md"))
        XCTAssertEqual(
            state.templateNoteNameError,
            AppState.templateDestinationUnavailableReason)
        XCTAssertFalse(
            FileManager.default.fileExists(
                atPath: vault.appendingPathComponent("must-not-write.md").path))

        let staleRoot = vault.deletingLastPathComponent()
            .appendingPathComponent("stale-template-owner")
        try FileManager.default.createDirectory(
            at: staleRoot, withIntermediateDirectories: true)
        let staleSession = try VaultSession.openFilesystem(rootPath: staleRoot.path)
        state.installTemplateDestinationForTesting(
            "", sessionIdentity: ObjectIdentifier(staleSession))
        state.templateNoteNameError = nil

        XCTAssertNil(state.submitTemplateNoteName("still-must-not-write.md"))
        XCTAssertEqual(
            state.templateNoteNameError,
            AppState.templateDestinationUnavailableReason)
        XCTAssertFalse(
            FileManager.default.fileExists(
                atPath: vault.appendingPathComponent("still-must-not-write.md").path))
    }

    func testBusyBatchRejectsRestoreAsBeforeWriteRetainsPromptAndRetrySucceeds() async throws {
        let (state, vault) = try await makeVault(files: [
            "blocker.md": "# blocker\n", "dest/keep.md": "# keep\n", "note.md": "v0\n"
        ])
        let session = try XCTUnwrap(state.currentSession)
        let first = try session.saveText(
            path: "note.md", contents: "v1\n", expectedContentHash: nil)
        _ = try session.saveText(
            path: "note.md", contents: "v2\n", expectedContentHash: first.newContentHash)
        let prompt = RestoreAsPrompt(
            source: .version(
                path: "note.md", hash: first.newContentHash, formattedDate: "test date"),
            suggestedPath: "note copy.md",
            sessionID: ObjectIdentifier(session))
        state.historyRestoreAsPrompt = prompt
        let blocker = try await blockingMove(on: state)
        let capture = mutationMessages(from: state)
        defer { capture.cancellable.cancel() }

        await state.performRestoreAs(prompt, destination: "note copy.md")

        XCTAssertEqual(state.historyRestoreAsPrompt, prompt)
        XCTAssertFalse(
            FileManager.default.fileExists(
                atPath: vault.appendingPathComponent("note copy.md").path))
        XCTAssertEqual(capture.messages(), [AppState.structuralMutationBusyReason])

        await blocker.gate.release()
        await blocker.task.value
        await state.performRestoreAs(prompt, destination: "note copy.md")

        XCTAssertNil(state.historyRestoreAsPrompt)
        XCTAssertEqual(try session.readText(path: "note copy.md"), "v1\n")
        XCTAssertFalse(state.isMutatingStructure)
    }

    func testBusyBatchRejectsNormalDeletedRestoreSynchronouslyAndRetainsRow()
        async throws
    {
        let (state, vault) = try await makeVault(files: [
            "blocker.md": "# blocker\n", "dest/keep.md": "# keep\n"
        ])
        try await prepareRecoverableDeletedFile(on: state)
        let blocker = try await blockingMove(on: state)
        let capture = mutationMessages(from: state)
        defer { capture.cancellable.cancel() }

        XCTAssertNil(state.requestRecoverDeleted(path: "gone.md"))
        XCTAssertEqual(state.deletedFiles.map(\.path), ["gone.md"])
        XCTAssertFalse(
            FileManager.default.fileExists(
                atPath: vault.appendingPathComponent("gone.md").path))
        XCTAssertEqual(capture.messages(), [AppState.structuralMutationBusyReason])

        await blocker.gate.release()
        await blocker.task.value
        try await XCTUnwrap(state.requestRecoverDeleted(path: "gone.md")).value

        XCTAssertEqual(
            try XCTUnwrap(state.currentSession).readText(path: "gone.md"),
            "recover me\n")
        XCTAssertTrue(state.deletedFiles.isEmpty)
        XCTAssertFalse(state.isMutatingStructure)
    }

    func testDeletedRestoreOwnsGateThroughBothRefreshesBeforeUndoBarrier()
        async throws
    {
        let (state, vault) = try await makeVault(files: ["sibling.md": "# sibling\n"])
        await state.renameEntry(
            path: "sibling.md", isDirectory: false, to: "renamed.md")?.value
        XCTAssertEqual(state.structuralUndoStack.count, 1)
        try await prepareRecoverableDeletedFile(on: state)

        let refresh = SuspensionGate()
        state.structuralBatchRefreshRunner = { _ in await refresh.enter() }
        let capture = mutationMessages(from: state)
        defer { capture.cancellable.cancel() }

        let restore = try XCTUnwrap(state.requestRecoverDeleted(path: "gone.md"))
        await refresh.waitUntilEntered()

        XCTAssertTrue(
            FileManager.default.fileExists(
                atPath: vault.appendingPathComponent("gone.md").path),
            "native recovery finishes off-main before the shared refresh")
        XCTAssertTrue(state.isMutatingStructure)
        XCTAssertEqual(
            state.structuralUndoStack.count, 1,
            "the recovery barrier must not clear undo while another owner could race")
        XCTAssertNil(state.requestCreateNote(in: ""))
        XCTAssertEqual(capture.messages(), [AppState.structuralMutationBusyReason])

        await refresh.release()
        await restore.value

        XCTAssertTrue(state.structuralUndoStack.isEmpty)
        XCTAssertTrue(state.deletedFiles.isEmpty)
        XCTAssertFalse(state.isMutatingStructure)
    }

    func testDeletedRestoreStaleRefreshCannotLandOrReleaseNewVaultOwner()
        async throws
    {
        let (state, _) = try await makeVault(files: [:])
        try await prepareRecoverableDeletedFile(on: state)
        let vaultB = try makeVaultDirectory(files: [
            "gone.md": "# Existing in B\n",
            "history.md": "# history\n",
            "blocker.md": "# blocker\n",
            "dest/keep.md": "# keep\n",
        ])
        let oldRefresh = SuspensionGate()
        state.structuralBatchRefreshRunner = { _ in await oldRefresh.enter() }

        let oldRestore = try XCTUnwrap(state.requestRecoverDeleted(path: "gone.md"))
        await oldRefresh.waitUntilEntered()

        state.openVault(at: vaultB)
        await state.scanTask?.value
        state.structuralBatchRefreshRunner = { _ in }
        await state.renameEntry(
            path: "history.md", isDirectory: false, to: "history-renamed.md")?.value
        XCTAssertEqual(state.structuralUndoStack.count, 1)
        state.selectedFilePath = "gone.md"
        await state.noteLoadTask?.value
        let newOwner = try await blockingMove(on: state)

        await oldRefresh.release()
        await oldRestore.value

        XCTAssertTrue(state.isMutatingStructure)
        XCTAssertEqual(state.selectedFilePath, "gone.md")
        XCTAssertEqual(state.currentNoteText, "# Existing in B\n")
        XCTAssertNil(state.historyRestoreAsPrompt)
        XCTAssertNil(state.historyAlert)
        XCTAssertEqual(
            state.structuralUndoStack.count, 1,
            "vault A's stale restore must not clear vault B's undo history")

        await newOwner.gate.release()
        await newOwner.task.value
        XCTAssertFalse(state.isMutatingStructure)
    }

    func testMissingFileKeepMineRejectsBusyWithoutDismissingThenRetries()
        async throws
    {
        let mine = "# Keep this version\n"
        let (state, vault) = try await makeVault(files: [
            "note.md": "# Original\n",
            "blocker.md": "# blocker\n",
            "dest/keep.md": "# keep\n",
        ])
        let conflict = try await stageMissingFileSaveConflict(
            on: state, vault: vault, mine: mine)
        let blocker = try await blockingMove(on: state)
        let capture = mutationMessages(from: state)
        defer { capture.cancellable.cancel() }

        XCTAssertEqual(
            state.saveConflictKeepMineDisabledReason,
            AppState.structuralMutationBusyReason)
        XCTAssertNil(state.resolveSaveConflictKeepMine())
        XCTAssertEqual(state.currentSaveConflict, conflict)
        XCTAssertFalse(
            FileManager.default.fileExists(
                atPath: vault.appendingPathComponent("note.md").path))
        XCTAssertEqual(capture.messages(), [AppState.structuralMutationBusyReason])

        await blocker.gate.release()
        await blocker.task.value
        try await XCTUnwrap(state.resolveSaveConflictKeepMine()).value

        XCTAssertNil(state.currentSaveConflict)
        XCTAssertEqual(
            try String(
                contentsOf: vault.appendingPathComponent("note.md"), encoding: .utf8),
            mine)
        XCTAssertFalse(state.isMutatingStructure)
    }

    func testExistingFileKeepMineRemainsARegularSaveWhileStructuralGateIsBusy()
        async throws
    {
        let mine = "# My overwrite\n"
        let (state, vault) = try await makeVault(files: [
            "note.md": "# Original\n",
            "blocker.md": "# blocker\n",
            "dest/keep.md": "# keep\n",
        ])
        state.selectedFilePath = "note.md"
        await state.noteLoadTask?.value
        try "# External\n".write(
            to: vault.appendingPathComponent("note.md"),
            atomically: true,
            encoding: .utf8)
        state.updateEditorText(mine)
        await state.saveCurrentNote()?.value
        XCTAssertFalse(try XCTUnwrap(state.currentSaveConflict).currentContentHash.isEmpty)
        let blocker = try await blockingMove(on: state)

        XCTAssertNil(
            state.saveConflictKeepMineDisabledReason,
            "an ordinary overwrite stays enabled during unrelated tree work")
        try await XCTUnwrap(state.resolveSaveConflictKeepMine()).value

        XCTAssertTrue(
            state.isMutatingStructure,
            "an ordinary overwrite neither claims nor releases the structural owner")
        XCTAssertNil(state.currentSaveConflict)
        XCTAssertEqual(
            try String(
                contentsOf: vault.appendingPathComponent("note.md"), encoding: .utf8),
            mine)

        await blocker.gate.release()
        await blocker.task.value
    }

    func testMissingFileKeepMineBlocksDirectReplacementThroughRefresh()
        async throws
    {
        let (state, vaultA) = try await makeVault(files: [
            "note.md": "# Original in A\n", "sibling.md": "# sibling\n"
        ])
        await state.renameEntry(
            path: "sibling.md", isDirectory: false, to: "renamed.md")?.value
        XCTAssertEqual(state.structuralUndoStack.count, 1)
        let conflict = try await stageMissingFileSaveConflict(
            on: state, vault: vaultA)
        let vaultB = try makeVaultDirectory(files: [
            "note.md": "# Existing in B\n",
            "history.md": "# history\n",
            "blocker.md": "# blocker\n",
            "dest/keep.md": "# keep\n",
        ])
        let oldRefresh = SuspensionGate()
        state.structuralBatchRefreshRunner = { _ in await oldRefresh.enter() }

        let oldKeepMine = try XCTUnwrap(state.resolveSaveConflictKeepMine())
        await oldRefresh.waitUntilEntered()
        XCTAssertEqual(
            state.structuralUndoStack.count, 1,
            "the create barrier waits until the guarded refresh can land")

        // User-facing vault switching is now correctly blocked for the full
        // save/structural lifetime.
        state.switchToRecent(RecentVault(url: vaultB))
        XCTAssertEqual(state.currentVaultURL?.path, vaultA.path)
        XCTAssertNil(state.pendingNavigation)
        XCTAssertNil(state.pendingVaultSwitchTarget)
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            "Wait for the current save to finish.")

        // The lower-level open-document handoff obeys the same full-duration
        // ownership rule. Replacing A here would discard the only draft while
        // its create-and-refresh transaction still owns it.
        state.openVault(at: vaultB)
        XCTAssertEqual(state.currentVaultURL?.path, vaultA.path)
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            "Wait for the current save to finish.")

        await oldRefresh.release()
        await oldKeepMine.value

        XCTAssertFalse(state.isMutatingStructure)
        XCTAssertEqual(state.currentVaultURL?.path, vaultA.path)
        XCTAssertEqual(state.selectedFilePath, "note.md")
        XCTAssertEqual(state.loadedFilePath, "note.md")
        XCTAssertEqual(state.currentNoteText, conflict.attemptedContents)
        XCTAssertNil(state.currentSaveConflict)
        XCTAssertEqual(
            try String(
                contentsOf: vaultA.appendingPathComponent("note.md"),
                encoding: .utf8),
            conflict.attemptedContents)
    }

    func testBusyBatchRejectsGhostCreationSynchronously() async throws {
        let (state, vault) = try await makeVault(files: [
            "blocker.md": "# blocker\n", "dest/keep.md": "# keep\n"
        ])
        let blocker = try await blockingMove(on: state)
        let capture = mutationMessages(from: state)
        defer { capture.cancellable.cancel() }

        state.createNoteFromGhost(targetRaw: "Missing Note")

        XCTAssertEqual(capture.messages(), [AppState.structuralMutationBusyReason])
        XCTAssertFalse(
            FileManager.default.fileExists(
                atPath: vault.appendingPathComponent("Missing Note.md").path))

        await blocker.gate.release()
        await blocker.task.value

        let retry = try XCTUnwrap(state.createNoteFromGhost(targetRaw: "Missing Note"))
        await retry.value
        XCTAssertTrue(
            FileManager.default.fileExists(
                atPath: vault.appendingPathComponent("Missing Note.md").path))
        XCTAssertFalse(state.isMutatingStructure)
    }

    func testAllFourCreationFunnelsUseTheSharedStructuralRefresh() async throws {
        let summary = TemplateSummary(
            path: "Templates/Seed.md", name: "Seed", description: nil)
        let (state, _) = try await makeVault(files: [
            summary.path: "# {{title}}\n",
            "note.md": "v0\n",
        ])
        let session = try XCTUnwrap(state.currentSession)
        let first = try session.saveText(
            path: "note.md", contents: "v1\n", expectedContentHash: nil)
        _ = try session.saveText(
            path: "note.md", contents: "v2\n", expectedContentHash: first.newContentHash)
        let refreshes = Counter()
        state.structuralBatchRefreshRunner = { _ in await refreshes.increment() }

        try await XCTUnwrap(state.canvasNewCanvasFile()).value

        state.installTemplateDestinationForTesting("")
        state.pendingTemplateFlow = .needsName(summary, [:])
        try await XCTUnwrap(state.submitTemplateNoteName("templated.md")).value

        try await XCTUnwrap(state.createNoteFromGhost(targetRaw: "Missing Note")).value

        let prompt = RestoreAsPrompt(
            source: .version(
                path: "note.md", hash: first.newContentHash, formattedDate: "test date"),
            suggestedPath: "note copy.md",
            sessionID: ObjectIdentifier(session))
        state.historyRestoreAsPrompt = prompt
        try await XCTUnwrap(
            state.commitRestoreAs(prompt, destination: "note copy.md")
        ).value

        let refreshCount = await refreshes.count()
        XCTAssertEqual(refreshCount, 4)
        XCTAssertFalse(state.isMutatingStructure)
    }

    func testCreationOwnsTokenThroughRefreshAndRejectsSecondSubmission() async throws {
        let (state, vault) = try await makeVault(files: [:])
        let refresh = SuspensionGate()
        state.structuralBatchRefreshRunner = { _ in await refresh.enter() }
        let capture = mutationMessages(from: state)
        defer { capture.cancellable.cancel() }

        let canvasTask = try XCTUnwrap(state.canvasNewCanvasFile())
        await refresh.waitUntilEntered()

        XCTAssertTrue(state.isMutatingStructure)
        XCTAssertNotNil(state.pendingStructuralTaskForTesting)
        XCTAssertNil(state.createNoteFromGhost(targetRaw: "Must Wait"))
        XCTAssertNotNil(state.pendingStructuralTaskForTesting)
        XCTAssertEqual(capture.messages(), [AppState.structuralMutationBusyReason])
        XCTAssertFalse(
            FileManager.default.fileExists(
                atPath: vault.appendingPathComponent("Must Wait.md").path))

        await refresh.release()
        await canvasTask.value

        let refreshCount = await refresh.entryCount()
        XCTAssertEqual(refreshCount, 1)
        XCTAssertEqual(state.selectedFilePath, "Untitled Canvas.canvas")
        XCTAssertFalse(state.isMutatingStructure)
    }

    func testStaleRefreshCompletionCannotLandOrReleaseNewVaultOwner() async throws {
        let (state, vaultA) = try await makeVault(files: [:])
        let vaultB = try makeVaultDirectory(files: [
            "blocker.md": "# blocker\n", "dest/keep.md": "# keep\n",
        ])
        var canvasMessages: [String] = []
        state.canvasAnnouncer = CanvasAnnouncer(
            verbosity: .standard, coalesceWindow: 60
        ) { message, _ in
            canvasMessages.append(message)
        }
        let oldRefresh = SuspensionGate()
        state.structuralBatchRefreshRunner = { _ in await oldRefresh.enter() }
        let nativeEvents = NativeEventRecorder()
        state.canvasNewFileNativeExecutionObserverForTesting = { event in
            nativeEvents.append(event)
        }

        let oldTask = try XCTUnwrap(state.canvasNewCanvasFile())
        await oldRefresh.waitUntilEntered()
        XCTAssertTrue(
            FileManager.default.fileExists(
                atPath: vaultA.appendingPathComponent("Untitled Canvas.canvas").path))

        state.openVault(at: vaultB)
        await state.scanTask?.value
        let newOwner = try await blockingMove(on: state)

        await oldRefresh.release()
        await oldTask.value

        let preparedCloses = nativeEvents.events().filter {
            $0.phase == .closePrepared
        }
        XCTAssertEqual(
            preparedCloses.count, 1,
            "the stale ready snapshot must release its retained native handle exactly once")
        XCTAssertTrue(
            preparedCloses.allSatisfy { !$0.ranOnMainThread },
            "stale native cleanup must never block the main actor")

        XCTAssertTrue(state.isMutatingStructure)
        XCTAssertNil(state.selectedFilePath)
        XCTAssertFalse(
            FileManager.default.fileExists(
                atPath: vaultB.appendingPathComponent("Untitled Canvas.canvas").path))
        state.canvasAnnouncer.flushForTests()
        XCTAssertFalse(canvasMessages.contains { $0.contains("Created canvas") })

        await newOwner.gate.release()
        await newOwner.task.value
        XCTAssertFalse(state.isMutatingStructure)
    }

    func testTemplateCursorLandingCannotCrossVaultsWithTheSameRelativePath() async throws {
        let summary = TemplateSummary(
            path: "Templates/Daily.md", name: "Daily", description: nil)
        let (state, _) = try await makeVault(files: [
            summary.path: "# Created in A\n{{cursor}}after\n"
        ])
        let vaultB = try makeVaultDirectory(files: [
            "Daily.md": "# Existing in B\n"
        ])
        let landing = SuspensionGate()
        state.templateCursorLandingGateForTesting = { await landing.enter() }
        state.installTemplateDestinationForTesting("")
        state.pendingTemplateFlow = .needsName(summary, [:])

        let oldCreate = try XCTUnwrap(state.submitTemplateNoteName("Daily.md"))
        await landing.waitUntilEntered()
        XCTAssertTrue(
            state.isMutatingStructure,
            "the template owner must remain held through cursor landing")

        state.openVault(at: vaultB)
        await state.scanTask?.value
        state.selectedFilePath = "Daily.md"
        await state.noteLoadTask?.value
        XCTAssertEqual(state.currentNoteText, "# Existing in B\n")
        XCTAssertNil(state.cursorByteOffsetRequest.value)

        await landing.release()
        await oldCreate.value

        XCTAssertEqual(state.selectedFilePath, "Daily.md")
        XCTAssertEqual(state.currentNoteText, "# Existing in B\n")
        XCTAssertNil(
            state.cursorByteOffsetRequest.value,
            "vault A's deferred cursor offset must never land in vault B")
    }

    func testDirtyTemplateCreationDefersCursorUntilTheCreatedNoteActuallyLoads() async throws {
        let summary = TemplateSummary(
            path: "Templates/Daily.md", name: "Daily", description: nil)
        let (state, _) = try await makeVault(files: [
            "dirty.md": "# Saved\n",
            summary.path: "# Created\n{{cursor}}after\n",
        ])
        state.selectedFilePath = "dirty.md"
        await state.noteLoadTask?.value
        state.updateEditorText("# Unsaved edit\n")
        state.installTemplateDestinationForTesting("")
        state.pendingTemplateFlow = .needsName(summary, [:])

        let create = try XCTUnwrap(state.submitTemplateNoteName("Daily.md"))
        await create.value

        XCTAssertEqual(state.pendingNavigation, .selectFile("Daily.md"))
        XCTAssertEqual(state.loadedFilePath, "dirty.md")
        XCTAssertNil(
            state.cursorByteOffsetRequest.value,
            "the new note's cursor offset must not enter the dirty note's editor")

        await drainMainQueue()
        state.resolvePendingNavigationDiscard()
        await state.noteLoadTask?.value

        XCTAssertEqual(state.loadedFilePath, "Daily.md")
        XCTAssertEqual(state.currentNoteText, "# Created\nafter\n")
        XCTAssertEqual(
            state.cursorByteOffsetRequest.value,
            10,
            "the deferred cursor offset must land after the created note's own load")
    }

    func testDeferredTemplateCursorIsClearedWhenItsTargetLoadFails() async throws {
        let summary = TemplateSummary(
            path: "Templates/Daily.md", name: "Daily", description: nil)
        let (state, vault) = try await makeVault(files: [
            "dirty.md": "# Saved\n",
            summary.path: "# Created\n{{cursor}}after\n",
        ])
        state.selectedFilePath = "dirty.md"
        await state.noteLoadTask?.value
        state.updateEditorText("# Unsaved edit\n")
        state.installTemplateDestinationForTesting("")
        state.pendingTemplateFlow = .needsName(summary, [:])

        let create = try XCTUnwrap(state.submitTemplateNoteName("Daily.md"))
        await create.value
        await drainMainQueue()
        try FileManager.default.removeItem(
            at: vault.appendingPathComponent("Daily.md"))

        state.resolvePendingNavigationDiscard()
        await state.noteLoadTask?.value

        XCTAssertEqual(state.selectedFilePath, "Daily.md")
        XCTAssertNil(state.loadedFilePath)
        XCTAssertNotNil(state.noteLoadError)
        XCTAssertNil(state.cursorByteOffsetRequest.value)

        try "# Replacement\n".write(
            to: vault.appendingPathComponent("Daily.md"),
            atomically: true,
            encoding: .utf8)
        await state.loadCurrentNote(path: "Daily.md")

        XCTAssertEqual(state.currentNoteText, "# Replacement\n")
        XCTAssertNil(
            state.cursorByteOffsetRequest.value,
            "a later file at the same path must not inherit the failed create's offset")
    }

    func testDirtyTemplateNavigationCancelClearsItsDeferredCursor() async throws {
        let summary = TemplateSummary(
            path: "Templates/Daily.md", name: "Daily", description: nil)
        let (state, _) = try await makeVault(files: [
            "dirty.md": "# Saved\n",
            summary.path: "# Created\n{{cursor}}after\n",
        ])
        state.selectedFilePath = "dirty.md"
        await state.noteLoadTask?.value
        state.updateEditorText("# Unsaved edit\n")
        state.installTemplateDestinationForTesting("")
        state.pendingTemplateFlow = .needsName(summary, [:])

        let create = try XCTUnwrap(state.submitTemplateNoteName("Daily.md"))
        await create.value
        await drainMainQueue()
        state.resolvePendingNavigationCancel()

        XCTAssertNil(state.pendingNavigation)
        XCTAssertEqual(state.loadedFilePath, "dirty.md")
        XCTAssertNil(state.cursorByteOffsetRequest.value)

        state.updateEditorText("# Saved\n")
        state.selectedFilePath = "Daily.md"
        await state.noteLoadTask?.value

        XCTAssertEqual(state.loadedFilePath, "Daily.md")
        XCTAssertNil(
            state.cursorByteOffsetRequest.value,
            "a later explicit navigation must not revive the cancelled cursor request")
    }

    func testTemplatePickerCancelCannotBeUndoneBySuspendedSelection() async throws {
        let summary = TemplateSummary(
            path: "Templates/Seed.md", name: "Seed", description: nil)
        let (state, _) = try await makeVault(files: [
            summary.path: "{{prompt:topic|Topic}}\n"
        ])
        let landing = SuspensionGate()
        state.installTemplateDestinationForTesting("")
        state.isTemplatePickerOpen = true
        state.templateSelectionLandingGateForTesting = { _ in await landing.enter() }

        let selection = try XCTUnwrap(state.selectTemplate(summary))
        await landing.waitUntilEntered()
        state.cancelTemplateFlow()

        XCTAssertFalse(state.isTemplatePickerOpen)
        XCTAssertEqual(state.pendingTemplateFlow, .idle)

        await landing.release()
        await selection.value

        XCTAssertFalse(
            state.isTemplatePickerOpen,
            "Cancel must remain authoritative after the selection read resumes")
        XCTAssertEqual(
            state.pendingTemplateFlow,
            .idle,
            "a cancelled selection must not reopen its prompt sheet")
    }

    func testTemplatePickerCancelOwnsEveryRapidSelection() async throws {
        let first = TemplateSummary(
            path: "Templates/First.md", name: "First", description: nil)
        let second = TemplateSummary(
            path: "Templates/Second.md", name: "Second", description: nil)
        let (state, _) = try await makeVault(files: [
            first.path: "{{prompt:first|First}}\n",
            second.path: "{{prompt:second|Second}}\n",
        ])
        let firstLanding = SuspensionGate()
        let secondLanding = SuspensionGate()
        state.installTemplateDestinationForTesting("")
        state.isTemplatePickerOpen = true
        state.templateSelectionLandingGateForTesting = { summary in
            if summary.path == first.path {
                await firstLanding.enter()
            } else {
                await secondLanding.enter()
            }
        }

        let firstTask = try XCTUnwrap(state.selectTemplate(first))
        await firstLanding.waitUntilEntered()
        let secondTask = try XCTUnwrap(state.selectTemplate(second))
        await secondLanding.waitUntilEntered()
        state.cancelTemplateFlow()

        await secondLanding.release()
        await firstLanding.release()
        await secondTask.value
        await firstTask.value

        XCTAssertFalse(state.isTemplatePickerOpen)
        XCTAssertEqual(
            state.pendingTemplateFlow,
            .idle,
            "Cancel must invalidate both the tracked and superseded selections")
    }

    func testLatestRapidTemplateSelectionWinsOutOfOrderLanding() async throws {
        let first = TemplateSummary(
            path: "Templates/First.md", name: "First", description: nil)
        let second = TemplateSummary(
            path: "Templates/Second.md", name: "Second", description: nil)
        let (state, _) = try await makeVault(files: [
            first.path: "{{prompt:first|First}}\n",
            second.path: "{{prompt:second|Second}}\n",
        ])
        let firstLanding = SuspensionGate()
        let secondLanding = SuspensionGate()
        state.installTemplateDestinationForTesting("")
        state.isTemplatePickerOpen = true
        state.templateSelectionLandingGateForTesting = { summary in
            if summary.path == first.path {
                await firstLanding.enter()
            } else {
                await secondLanding.enter()
            }
        }

        let firstTask = try XCTUnwrap(state.selectTemplate(first))
        await firstLanding.waitUntilEntered()
        let secondTask = try XCTUnwrap(state.selectTemplate(second))
        await secondLanding.waitUntilEntered()

        await secondLanding.release()
        await secondTask.value
        await firstLanding.release()
        await firstTask.value

        guard case .needsPrompts(let selected, _) = state.pendingTemplateFlow else {
            return XCTFail("expected latest template prompt presentation")
        }
        XCTAssertEqual(
            selected,
            second,
            "a superseded read must not replace the most recent row selection")
    }

    func testTemplateSubmitDismissesNameSheetBeforeSuspendedRefresh() async throws {
        let summary = TemplateSummary(
            path: "Templates/Seed.md", name: "Seed", description: nil)
        let (state, vault) = try await makeVault(files: [
            summary.path: "# {{title}}\n"
        ])
        let refresh = SuspensionGate()
        state.structuralBatchRefreshRunner = { _ in await refresh.enter() }
        state.installTemplateDestinationForTesting("")
        state.pendingTemplateFlow = .needsName(summary, ["topic": "Retained"])

        let create = try XCTUnwrap(state.submitTemplateNoteName("created.md"))

        XCTAssertEqual(
            state.pendingTemplateFlow,
            .idle,
            "successful admission must remove the Cancel control before work starts")

        await refresh.waitUntilEntered()
        XCTAssertTrue(
            FileManager.default.fileExists(
                atPath: vault.appendingPathComponent("created.md").path))
        XCTAssertEqual(
            state.pendingTemplateFlow,
            .idle,
            "a sheet promising no write must not remain visible after the write")

        // Even a stale action closure from the dismissal animation cannot turn
        // the already-admitted operation into a false cancellation promise.
        state.cancelTemplateFlow()
        await refresh.release()
        await create.value

        XCTAssertEqual(state.selectedFilePath, "created.md")
        XCTAssertEqual(
            state.templateAnnouncementLastMessage,
            "Created created.md from Seed.")
        XCTAssertFalse(state.isMutatingStructure)
    }

    func testTemplateWriteFailureRestoresExactFlowAndDestination() async throws {
        let summary = TemplateSummary(
            path: "Templates/Seed.md", name: "Seed", description: nil)
        let promptValues = ["topic": "Retained answer"]
        let (state, _) = try await makeVault(files: [
            summary.path: "# {{title}}\n",
            "Projects/existing.md": "# Existing\n",
        ])
        state.installTemplateDestinationForTesting("Projects")
        state.pendingTemplateFlow = .needsName(summary, promptValues)

        let create = try XCTUnwrap(state.submitTemplateNoteName("existing.md"))
        XCTAssertEqual(state.pendingTemplateFlow, .idle)
        await create.value

        XCTAssertEqual(
            state.pendingTemplateFlow,
            .needsName(summary, promptValues),
            "failure must restore the exact template and prompt values")
        XCTAssertEqual(state.templateRetryNoteName, "existing.md")
        XCTAssertEqual(state.templateCreationDestination, "Projects")
        XCTAssertNotNil(state.templateNoteNameError)
        XCTAssertFalse(state.isMutatingStructure)

        let seed = try sourceSegment(
            file: "TemplatePromptSheet.swift",
            from: "if !didSeed {",
            to: "didSeed = true")
        XCTAssertTrue(
            seed.contains(
                "noteName = appState.templateRetryNoteName ?? appState.defaultNewNoteName"),
            "the re-presented field must consume the retained destination")
    }

    func testBusyRegistryRejectsTemplateAndCanvasCommandsWithExactReason() async throws {
        let (state, vault) = try await makeVault(files: [
            "blocker.md": "# blocker\n", "dest/keep.md": "# keep\n",
            "Templates/Seed.md": "# Seed\n",
        ])
        await state.settleTemplateAvailability()
        XCTAssertEqual(state.templateAvailability, .available)
        let blocker = try await blockingMove(on: state)
        let ids = [SlateCommandID.newFromTemplate, SlateCommandID.newCanvas]

        for id in ids {
            XCTAssertTrue(SlateCommandID.structuralMutationCommands.contains(id))
            XCTAssertThrowsError(try state.commandRegistry.invokeById(id: id)) { error in
                XCTAssertEqual(
                    error as? CommandError,
                    .ActionFailed(message: AppState.structuralMutationBusyReason))
            }
        }
        XCTAssertFalse(state.isTemplatePickerOpen)
        XCTAssertFalse(
            FileManager.default.fileExists(
                atPath: vault.appendingPathComponent("Untitled Canvas.canvas").path))

        await blocker.gate.release()
        await blocker.task.value
    }

    func testBusyCreationStagingRoutesRetainExistingPresentationState() async throws {
        let summary = TemplateSummary(
            path: "Templates/Seed.md", name: "Seed", description: nil)
        let (state, _) = try await makeVault(files: [
            "blocker.md": "# blocker\n",
            "dest/keep.md": "# keep\n",
            "note.md": "# note\n",
            summary.path: "{{prompt:topic|Topic}}\n",
        ])
        state.selectedFilePath = "note.md"
        let prompts = [TemplatePrompt(key: "topic", label: "Topic")]
        state.installTemplateDestinationForTesting("")
        state.pendingTemplateFlow = .needsPrompts(summary, prompts)
        let blocker = try await blockingMove(on: state)
        let capture = mutationMessages(from: state)
        defer { capture.cancellable.cancel() }

        XCTAssertNil(state.openTemplatePicker())
        XCTAssertFalse(state.isTemplatePickerOpen)
        XCTAssertNil(state.selectTemplate(summary))
        state.submitTemplatePrompts(["topic": "Kept"])
        XCTAssertEqual(state.pendingTemplateFlow, .needsPrompts(summary, prompts))
        state.requestRestoreAs(versionHash: "unused", formattedDate: "today")
        XCTAssertNil(state.historyRestoreAsPrompt)
        XCTAssertEqual(
            capture.messages(),
            Array(repeating: AppState.structuralMutationBusyReason, count: 4))

        await blocker.gate.release()
        await blocker.task.value
    }

    func testLiveTemplateAndHistoryControlsProjectTheSharedBusyReason() throws {
        let menu = try sourceSegment(
            file: "SlateMacApp.swift",
            from: "private func sidebarFileMenuActions(",
            to: "/// Top-level router:")
        XCTAssertTrue(menu.contains("evaluation.disabledReason"))
        XCTAssertTrue(menu.contains(".disabled("))
        XCTAssertTrue(menu.contains(".accessibilityHint("))
        XCTAssertTrue(menu.contains(".help("))

        let toolbar = try sourceSegment(
            file: "MainSplitView.swift",
            from: "ToolbarItem(id: \"template\"",
            to: "ToolbarItem(id: \"tasksReview\"")
        XCTAssertTrue(toolbar.contains("sidebarActionProjection(surface: .toolbar)"))
        XCTAssertTrue(toolbar.contains("evaluation.disabledReason"))
        XCTAssertTrue(toolbar.contains(".accessibilityHint("))
        XCTAssertTrue(toolbar.contains(".help("))

        let picker = try sourceSegment(
            file: "TemplatePicker.swift",
            from: "private func row(for summary:",
            to: "private func rowAccessibilityLabel")
        XCTAssertTrue(picker.contains("structuralMutationDisabledReason"))
        XCTAssertTrue(picker.contains(".disabled("))
        XCTAssertTrue(picker.contains(".accessibilityHint("))
        XCTAssertTrue(picker.contains(".help("))

        let nameStep = try sourceSegment(
            file: "TemplatePromptSheet.swift",
            from: "private struct NameStep",
            to: "private func submit()")
        XCTAssertTrue(nameStep.contains("structuralMutationDisabledReason"))
        XCTAssertTrue(nameStep.contains(".disabled("))
        XCTAssertTrue(nameStep.contains(".accessibilityHint("))
        XCTAssertTrue(nameStep.contains(".help("))

        let restore = try sourceSegment(
            file: "HistoryPanel.swift",
            from: "Button(\"Restore\")",
            to: "} message: { prompt in")
        XCTAssertTrue(restore.contains("commitRestoreAs"))
        XCTAssertFalse(
            restore.contains("historyRestoreAsPrompt = nil"),
            "the live alert must not dismiss before synchronous admission")
        XCTAssertTrue(restore.contains("structuralMutationDisabledReason"))
        XCTAssertTrue(restore.contains(".disabled("))
        XCTAssertTrue(restore.contains(".accessibilityHint("))
        XCTAssertTrue(restore.contains(".help("))

        let restoreRow = try sourceSegment(
            file: "HistoryPanel.swift",
            from: "appState.requestRestoreAs(",
            to: "Text(version.audioFragment)")
        XCTAssertTrue(restoreRow.contains("structuralMutationDisabledReason"))
        XCTAssertTrue(restoreRow.contains(".disabled("))
        XCTAssertTrue(restoreRow.contains(".accessibilityHint("))
        XCTAssertTrue(restoreRow.contains(".help("))

        let deletedRestore = try sourceSegment(
            file: "HistoryPanel.swift",
            from: "if entry.recoverable",
            to: ".padding(.horizontal, 12)")
        XCTAssertTrue(deletedRestore.contains("requestRecoverDeleted(path:"))
        XCTAssertTrue(deletedRestore.contains("structuralMutationDisabledReason"))
        XCTAssertTrue(deletedRestore.contains(".disabled("))
        XCTAssertTrue(deletedRestore.contains(".accessibilityHint("))
        XCTAssertTrue(deletedRestore.contains(".help("))

        let keepMine = try sourceSegment(
            file: "MainSplitView.swift",
            from: "Button(\"Keep Mine\")",
            to: "Button(\"Reload from Disk\"")
        XCTAssertTrue(keepMine.contains("saveConflictKeepMineDisabledReason"))
        XCTAssertTrue(keepMine.contains(".disabled("))
        XCTAssertTrue(keepMine.contains(".accessibilityHint("))
        XCTAssertTrue(keepMine.contains(".help("))
    }
}
