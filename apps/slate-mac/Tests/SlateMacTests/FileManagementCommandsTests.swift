// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// U2-5 (#463): file-management commands end-to-end against a real temp vault —
/// create/rename/move/delete for files + folders, collision + invalid-name
/// surfacing, the keyboard-only walkthrough through the command registry, and
/// open-tab retargeting (rename follows, delete flips to the error state).
///
/// These run through the live `VaultSession` FFI (not a mock) so the AppState
/// wrappers are exercised against the real slate-core mutation surface — the
/// same shape `WorkspaceStoreTests` uses.
@MainActor
final class FileManagementCommandsTests: XCTestCase {

    private var tempDir: URL!

    override func setUpWithError() throws {
        tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("file-mgmt-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: tempDir, withIntermediateDirectories: true)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: tempDir)
    }

    /// Build a vault with the given `files` (relative paths, "" body) and an
    /// AppState scanned over it.
    private func makeVault(files: [String]) async throws -> (AppState, URL) {
        let vault = tempDir.appendingPathComponent("vault")
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
                fileURL: tempDir.appendingPathComponent("recents.json")),
            externalOpener: { _ in true })
        state.openVault(at: vault)
        await state.scanTask?.value
        return (state, vault)
    }

    private func fileExists(_ vault: URL, _ rel: String) -> Bool {
        FileManager.default.fileExists(atPath: vault.appendingPathComponent(rel).path)
    }

    /// Publish the same session-tagged semantic selection that the live tree
    /// owns. Registry commands intentionally no longer infer an action target
    /// from the legacy single-focus mirror alone.
    private func publishSidebarSelection(
        _ state: AppState,
        path: String? = nil,
        isDirectory: Bool = false
    ) throws {
        let session = try XCTUnwrap(state.currentSession)
        let item = path.map {
            SidebarSelectionItem(
                path: $0,
                isDirectory: isDirectory,
                isMarkdown: !isDirectory
                    && ($0 as NSString).pathExtension
                        .caseInsensitiveCompare("md") == .orderedSame)
        }
        let creationParent: String
        if let path {
            let parent = (path as NSString).deletingLastPathComponent
            creationParent = isDirectory ? path : (parent == "." ? "" : parent)
        } else {
            creationParent = ""
        }
        XCTAssertTrue(
            state.publishSidebarSelectionSnapshot(
                SidebarSelectionSnapshot(
                    sessionIdentity: ObjectIdentifier(session),
                    items: item.map { [$0] } ?? [],
                    focusedPath: path,
                    creationParent: creationParent)))
    }

    // MARK: - Create

    func testCreateFolderCreatesDirectoryAndFiresTreeMutation() async throws {
        let (state, vault) = try await makeVault(files: ["a.md"])
        await state.createFolder(name: "Notes", in: "")?.value

        XCTAssertTrue(fileExists(vault, "Notes"), "folder created on disk")
        XCTAssertNil(state.lastError)
        guard case .createFolder(let path)? = state.treeMutation?.kind else {
            return XCTFail("expected a createFolder tree mutation, got \(String(describing: state.treeMutation?.kind))")
        }
        XCTAssertEqual(path, "Notes")
    }

    func testCreateNoteWritesFileOpensItAndEntersRename() async throws {
        let (state, vault) = try await makeVault(files: ["a.md"])
        await state.createNote(in: "")?.value
        await state.noteLoadTask?.value

        XCTAssertTrue(fileExists(vault, "Untitled.md"), "note created on disk")
        XCTAssertEqual(state.selectedFilePath, "Untitled.md", "opened in the current tab")
        let pending = try XCTUnwrap(
            state.renamingNode, "drops into inline rename of the new note")
        XCTAssertEqual(pending.path, "Untitled.md")
        XCTAssertFalse(pending.isDirectory)
        XCTAssertEqual(
            pending.sessionIdentity,
            state.currentSession.map(ObjectIdentifier.init))
    }

    func testCreateNoteAutoSuffixesOnCollision() async throws {
        let (state, vault) = try await makeVault(files: ["Untitled.md"])
        await state.createNote(in: "")?.value
        XCTAssertTrue(fileExists(vault, "Untitled 2.md"), "second untitled auto-suffixes")
        XCTAssertNil(state.lastError)
    }

    func testCreateNoteInSelectedFolder() async throws {
        let (state, vault) = try await makeVault(files: ["proj/a.md"])
        state.treeSelectedNode = AppState.TreeSelection(path: "proj", isDirectory: true)
        state.newNoteCommand()
        await state.pendingStructuralTaskForTesting?.value
        await state.noteLoadTask?.value
        XCTAssertTrue(fileExists(vault, "proj/Untitled.md"), "created inside the selected folder")
    }

    // MARK: - Rename

    func testRenameFileMovesOnDiskAndClearsRenamingNode() async throws {
        let (state, vault) = try await makeVault(files: ["a.md", "b.md"])
        await state.renameEntry(path: "a.md", isDirectory: false, to: "alpha.md")?.value

        XCTAssertFalse(fileExists(vault, "a.md"))
        XCTAssertTrue(fileExists(vault, "alpha.md"))
        XCTAssertNil(state.renamingNode, "rename mode cleared on success")
        XCTAssertNil(state.structuralRenameError)
        guard case .rename(let old, let new)? = state.treeMutation?.kind else {
            return XCTFail("expected a rename tree mutation")
        }
        XCTAssertEqual(old, "a.md")
        XCTAssertEqual(new, "alpha.md")
    }

    func testRenameFolderMovesSubtree() async throws {
        let (state, vault) = try await makeVault(files: ["proj/a.md", "proj/sub/b.md"])
        await state.renameEntry(path: "proj", isDirectory: true, to: "project")?.value

        XCTAssertFalse(fileExists(vault, "proj/a.md"))
        XCTAssertTrue(fileExists(vault, "project/a.md"))
        XCTAssertTrue(fileExists(vault, "project/sub/b.md"))
    }

    func testRenameCollisionSurfacesInlineErrorAndKeepsRenamingNode() async throws {
        let (state, _) = try await makeVault(files: ["a.md", "b.md"])
        XCTAssertTrue(state.requestRename(path: "a.md", isDirectory: false))
        let pending = try XCTUnwrap(state.renamingNode)
        // Rename a.md → b.md (collides).
        XCTAssertTrue(state.commitPendingRename(id: pending.id, to: "b.md"))
        await state.pendingStructuralTaskForTesting?.value

        XCTAssertNotNil(state.structuralRenameError, "collision surfaces a specific inline error")
        XCTAssertEqual(
            state.renamingNode?.id, pending.id,
            "rename field stays open + focused so the user can correct — never silent")
    }

    func testPendingRenameIsUUIDSessionOwnedAndDiesAcrossVaultLifecycle()
        async throws
    {
        let (state, vaultA) = try await makeVault(files: ["a.md", "b.md"])
        XCTAssertTrue(state.requestRename(path: "a.md", isDirectory: false))
        let a = try XCTUnwrap(state.renamingNode)
        XCTAssertEqual(
            a.sessionIdentity,
            state.currentSession.map(ObjectIdentifier.init))
        XCTAssertTrue(state.cancelPendingRename(id: a.id))

        XCTAssertTrue(state.requestRename(path: "a.md", isDirectory: false))
        let b = try XCTUnwrap(state.renamingNode)
        XCTAssertNotEqual(a.id, b.id, "re-staging the same path gets a fresh owner")
        XCTAssertFalse(state.cancelPendingRename(id: a.id))
        XCTAssertFalse(state.commitPendingRename(id: a.id, to: "alpha.md"))
        XCTAssertEqual(state.renamingNode?.id, b.id, "stale A cannot clear or commit B")
        XCTAssertTrue(fileExists(vaultA, "a.md"))
        XCTAssertFalse(fileExists(vaultA, "alpha.md"))

        let vaultB = tempDir.appendingPathComponent("vault-b")
        try FileManager.default.createDirectory(
            at: vaultB, withIntermediateDirectories: true)
        try "# B\n".write(
            to: vaultB.appendingPathComponent("a.md"), atomically: true,
            encoding: .utf8)
        state.openVault(at: vaultB)
        await state.scanTask?.value
        XCTAssertNil(state.renamingNode, "direct A→B open clears the old capture")
        XCTAssertFalse(state.commitPendingRename(id: b.id, to: "alpha.md"))
        XCTAssertTrue(fileExists(vaultB, "a.md"), "stale vault-A commit writes nothing in B")
        XCTAssertFalse(fileExists(vaultB, "alpha.md"))

        XCTAssertTrue(state.requestRename(path: "a.md", isDirectory: false))
        let c = try XCTUnwrap(state.renamingNode)
        state.closeVault()
        XCTAssertNil(state.renamingNode, "close/reset clears the captured rename")
        XCTAssertFalse(state.cancelPendingRename(id: c.id))
    }

    func testNewFolderInContextIgnoresStaleErrorAndStagesTheCreatedFolder()
        async throws
    {
        let (state, vault) = try await makeVault(files: ["seed.md"])
        state.lastError = "an old unrelated failure"

        state.newFolderInContext(parent: "")
        await state.pendingStructuralTaskForTesting?.value

        XCTAssertTrue(fileExists(vault, "Untitled Folder"))
        let pending = try XCTUnwrap(state.renamingNode)
        XCTAssertEqual(pending.path, "Untitled Folder")
        XCTAssertTrue(pending.isDirectory)
        XCTAssertEqual(
            pending.sessionIdentity,
            state.currentSession.map(ObjectIdentifier.init))
    }

    func testNewFolderInContextBackendFailureNeverStagesRename() async throws {
        let (state, vault) = try await makeVault(files: ["seed.md"])
        state.lastError = nil

        // Dot-prefixed path components are rejected by the real structural
        // backend. This is a non-busy admitted create whose actual result is
        // failure — distinct from the synchronous busy-rejection regression.
        state.newFolderInContext(parent: ".slate")
        await state.pendingStructuralTaskForTesting?.value

        XCTAssertFalse(fileExists(vault, ".slate/Untitled Folder"))
        XCTAssertNil(
            state.renamingNode,
            "a failed backend create cannot stage a rename from global error state")
        XCTAssertNotNil(state.lastError, "the underlying create failure still surfaces")
    }

    func testRenameInvalidNameSurfacesInlineError() async throws {
        let (state, _) = try await makeVault(files: ["a.md"])
        // A slash makes it a non-leaf component → InvalidPath/InvalidArgument.
        await state.renameEntry(path: "a.md", isDirectory: false, to: "b/c.md")?.value
        XCTAssertNotNil(state.structuralRenameError)
    }

    // MARK: - Move

    func testMoveFileIntoFolder() async throws {
        let (state, vault) = try await makeVault(files: ["a.md", "dest/x.md"])
        await state.moveEntry(path: "a.md", isDirectory: false, to: "dest")?.value

        XCTAssertFalse(fileExists(vault, "a.md"))
        XCTAssertTrue(fileExists(vault, "dest/a.md"))
        XCTAssertNil(state.pendingMove, "move sheet cleared on success")
        guard case .move(let old, let new, _, let newParent)? = state.treeMutation?.kind else {
            return XCTFail("expected a move tree mutation")
        }
        XCTAssertEqual(old, "a.md")
        XCTAssertEqual(new, "dest/a.md")
        XCTAssertEqual(newParent, "dest")
    }

    func testMoveFileToVaultRoot() async throws {
        let (state, vault) = try await makeVault(files: ["sub/a.md"])
        await state.moveEntry(path: "sub/a.md", isDirectory: false, to: "")?.value
        XCTAssertTrue(fileExists(vault, "a.md"))
        XCTAssertFalse(fileExists(vault, "sub/a.md"))
    }

    func testMoveCollisionSurfacesError() async throws {
        let (state, _) = try await makeVault(files: ["a.md", "dest/a.md"])
        await state.moveEntry(path: "a.md", isDirectory: false, to: "dest")?.value
        XCTAssertNotNil(state.lastError, "moving onto an existing name surfaces an error, never silent")
    }

    // MARK: - Delete

    func testDeleteFileRemovesItFromDisk() async throws {
        let (state, vault) = try await makeVault(files: ["a.md", "b.md"])
        await state.deleteEntry(path: "a.md", isDirectory: false)?.value
        XCTAssertFalse(fileExists(vault, "a.md"))
        XCTAssertTrue(fileExists(vault, "b.md"))
        guard case .delete(let path, _, let wasDir)? = state.treeMutation?.kind else {
            return XCTFail("expected a delete tree mutation")
        }
        XCTAssertEqual(path, "a.md")
        XCTAssertFalse(wasDir)
    }

    func testDeleteFolderRemovesSubtree() async throws {
        let (state, vault) = try await makeVault(files: ["proj/a.md", "proj/sub/b.md", "keep.md"])
        await state.deleteEntry(path: "proj", isDirectory: true)?.value
        XCTAssertFalse(fileExists(vault, "proj/a.md"))
        XCTAssertFalse(fileExists(vault, "proj/sub/b.md"))
        XCTAssertTrue(fileExists(vault, "keep.md"))
    }

    // MARK: - Retargeting (open tab follows rename; delete → error tab)

    func testRenameRetargetsTheOpenActiveTab() async throws {
        let (state, _) = try await makeVault(files: ["a.md", "b.md"])
        state.selectedFilePath = "a.md"
        await state.noteLoadTask?.value
        XCTAssertEqual(state.loadedFilePath, "a.md")

        await state.renameEntry(path: "a.md", isDirectory: false, to: "alpha.md")?.value

        XCTAssertEqual(state.loadedFilePath, "alpha.md", "active tab followed the rename")
        XCTAssertEqual(state.selectedFilePath, "alpha.md")
        XCTAssertEqual(
            state.workspace.model.allTabs.map(\.item), [.markdown(path: "alpha.md")],
            "the tab's EditorItem was retargeted, not closed")
    }

    /// Codex round 3: the link-record OWNERSHIP marker must follow a
    /// rename of the active note. `loadedFilePath` updates before
    /// `selectedFilePath`, so the selection sink early-returns and no
    /// new link query restamps it — a stale marker left every reading
    /// link warning-styled and refusing activation.
    func testRenameRetargetsLinkRecordOwnership() async throws {
        let (state, _) = try await makeVault(files: ["a.md", "b.md"])
        state.selectedFilePath = "a.md"
        await state.noteLoadTask?.value
        await state.linksLoadTask?.value
        XCTAssertEqual(state.currentOutgoingLinksPath, "a.md")

        await state.renameEntry(path: "a.md", isDirectory: false, to: "alpha.md")?.value

        XCTAssertEqual(
            state.currentOutgoingLinksPath, "alpha.md",
            "reading classification keeps working through the rename")
    }

    /// Codex round 4: rename RACING the in-flight link query. The old
    /// query's result is dropped by its race guard (selection moved),
    /// which also leaves `isLoadingLinks` true assuming a newer task
    /// exists — the retarget must BE that newer task. Whichever side
    /// wins the race, ownership must land on the new path and the
    /// loading flag must clear.
    func testRenameDuringInFlightLinkQueryHealsOwnership() async throws {
        let (state, _) = try await makeVault(files: ["a.md", "b.md"])
        state.selectedFilePath = "a.md"
        await state.noteLoadTask?.value
        // Deliberately NOT awaiting linksLoadTask here.
        await state.renameEntry(path: "a.md", isDirectory: false, to: "alpha.md")?.value
        await state.linksLoadTask?.value
        await state.embedsLoadTask?.value

        XCTAssertEqual(state.currentOutgoingLinksPath, "alpha.md")
        XCTAssertFalse(state.isLoadingLinks, "no stuck loading flag either way")
        XCTAssertFalse(
            state.isLoadingEmbeds,
            "the orphaned old chain must not re-strand the embeds flag")
    }

    /// Codex round 6: active-note deletion retains `selectedFilePath`
    /// for the missing-file error tab, so the path guards alone can't
    /// drop an in-flight embeds/citations leg — deletion must CANCEL
    /// them (cancelNoteScopedWork omitted both). This pins the
    /// contract: after delete, both handles are cancelled and cleared,
    /// and the embeds entry guard's isCancelled check does the rest
    /// for any leg already past its spawn point.
    func testDeleteCancelsEmbedAndCitationLegs() async throws {
        let (state, _) = try await makeVault(files: ["a.md", "b.md"])
        state.selectedFilePath = "a.md"
        await state.noteLoadTask?.value
        await state.linksLoadTask?.value
        XCTAssertNotNil(state.embedsLoadTask, "premise: the chain assigned the leg")
        XCTAssertNotNil(state.citationsLoadTask)

        await state.deleteEntry(path: "a.md", isDirectory: false)?.value

        XCTAssertNil(state.embedsLoadTask, "delete cancels + clears the embeds leg")
        XCTAssertNil(state.citationsLoadTask, "and the citations leg")
        XCTAssertFalse(state.isLoadingEmbeds)
    }

    /// Codex round 9: ownership compares the ACTIVE MARKDOWN TAB's
    /// path, not `selectedFilePath` — during dirty navigation the
    /// selection is a transient mirror (it briefly holds the requested
    /// destination while the tab stays put and the rollback is async).
    /// Deleting the dirty note while such a mirror points elsewhere
    /// must still flip THIS tab to the error state while retaining the dirty
    /// buffer in the explicit missing-note recovery surface.
    func testDeleteOfDirtyNoteDuringDirtyNavStillFlipsToError() async throws {
        let (state, _) = try await makeVault(files: ["a.md", "b.md"])
        state.selectedFilePath = "a.md"
        await state.noteLoadTask?.value
        state.updateEditorText("# a.md\nEDITED, unsaved\n")
        XCTAssertTrue(state.hasUnsavedChanges)

        let deletion = state.deleteEntry(path: "a.md", isDirectory: false)
        // Dirty nav: the mirror flips to b.md; the tab stays a.md and
        // the rollback lands asynchronously. No suspension between the
        // delete and this write, so the completion can observe the
        // disagreement window.
        state.selectedFilePath = "b.md"
        await deletion?.value

        XCTAssertNotNil(state.noteLoadError, "the deleted tab flips to the error state")
        XCTAssertEqual(state.currentNoteText, "# a.md\nEDITED, unsaved\n")
        XCTAssertTrue(state.hasUnsavedChanges)
        XCTAssertEqual(
            state.missingNoteRecoveryDraft(for: "a.md")?.body,
            "# a.md\nEDITED, unsaved\n")
    }

    /// Codex round 8, direction one: select a note and delete it
    /// IMMEDIATELY — before the note load lands. The stale call-time
    /// `loadedFilePath` capture said "not active", skipped the whole
    /// cleanup, and the doomed read then published the deleted note's
    /// content. Ownership is now decided at completion against the
    /// live selection: whichever side wins the load/delete race, the
    /// tab must end in the error state, never showing deleted content.
    func testImmediateDeleteAfterSelectShowsErrorNotDeletedContent() async throws {
        let (state, _) = try await makeVault(files: ["a.md", "b.md"])
        state.selectedFilePath = "a.md"
        let doomedLoad = state.noteLoadTask

        await state.deleteEntry(path: "a.md", isDirectory: false)?.value
        await doomedLoad?.value

        XCTAssertNil(state.currentNoteText, "the doomed load must not publish deleted content")
        XCTAssertNotNil(state.noteLoadError, "the tab flips to the missing-file error state")
        XCTAssertFalse(state.isLoadingNote)
    }

    /// Codex round 8, direction two: delete the loaded note, then
    /// switch selection BEFORE the deletion completes. The stale
    /// capture said "active" and the completion clobbered the NEW
    /// note's state with the deleted-note error. Deterministic: no
    /// suspension between deleteEntry() and the selection switch, so
    /// the MainActor completion always observes the new selection.
    func testDeleteCompletionNeverClobbersASwitchedSelection() async throws {
        let (state, _) = try await makeVault(files: ["a.md", "b.md"])
        state.selectedFilePath = "a.md"
        await state.noteLoadTask?.value

        let deletion = state.deleteEntry(path: "a.md", isDirectory: false)
        state.selectedFilePath = "b.md"
        await deletion?.value
        await state.noteLoadTask?.value

        XCTAssertNil(state.noteLoadError, "b.md must not be labeled as deleted")
        XCTAssertEqual(state.loadedFilePath, "b.md")
        XCTAssertNotNil(state.currentNoteText, "b.md's buffer survives the completion")
    }

    /// Codex round 7: delete while the fan-out is MID-FLIGHT (nothing
    /// awaited past the note load). The explicit-clear loaders (note,
    /// links, embeds) relied on "a newer task will clear the spinner"
    /// after a dropped landing — deletion has no newer task, so the
    /// delete site clears the trio and the loaders' entry guards stop
    /// a cancelled late-starting body from re-setting one. Whatever
    /// the interleaving, no spinner may survive the delete.
    func testDeleteMidFlightFanOutStrandsNoSpinner() async throws {
        let (state, _) = try await makeVault(files: ["a.md", "b.md"])
        state.selectedFilePath = "a.md"
        await state.noteLoadTask?.value
        // Capture the handles cancelNoteScopedWork is about to nil so
        // the orphans can be awaited to land (and drop) AFTER delete.
        let links = state.linksLoadTask
        let embeds = state.embedsLoadTask

        await state.deleteEntry(path: "a.md", isDirectory: false)?.value
        await links?.value
        await embeds?.value

        XCTAssertFalse(state.isLoadingNote)
        XCTAssertFalse(state.isLoadingLinks)
        XCTAssertFalse(state.isLoadingEmbeds)
        XCTAssertFalse(state.isLoadingTasks)
        XCTAssertFalse(state.isLoadingMathBlocks)
        XCTAssertFalse(state.isLoadingCodeBlocks)
        XCTAssertFalse(state.isLoadingDiagramBlocks)
        XCTAssertFalse(state.isLoadingCitations)
    }

    /// Codex round 5: a CANCELLED links leg must not chain an embeds
    /// leg — the chain it would spawn is the orphan that could flip
    /// `isLoadingEmbeds` after the newer chain already finished.
    /// Cancelling before the task body runs is deterministic (the
    /// cancellation flag is set while this test still owns the actor).
    func testCancelledLinksLegDoesNotChainEmbeds() async throws {
        let (state, _) = try await makeVault(files: ["a.md", "b.md"])
        state.selectedFilePath = "a.md"
        state.linksLoadTask?.cancel()
        await state.linksLoadTask?.value
        XCTAssertNil(
            state.embedsLoadTask,
            "no embeds leg is spawned for a cancelled links leg")
        XCTAssertFalse(state.isLoadingEmbeds)
    }

    func testMoveRetargetsTheOpenActiveTab() async throws {
        let (state, _) = try await makeVault(files: ["a.md", "dest/x.md"])
        state.selectedFilePath = "a.md"
        await state.noteLoadTask?.value

        await state.moveEntry(path: "a.md", isDirectory: false, to: "dest")?.value

        XCTAssertEqual(state.loadedFilePath, "dest/a.md", "active tab followed the move")
    }

    func testDeleteOpenActiveTabFlipsToErrorState() async throws {
        let (state, _) = try await makeVault(files: ["a.md", "b.md"])
        state.selectedFilePath = "a.md"
        await state.noteLoadTask?.value
        XCTAssertEqual(state.loadedFilePath, "a.md")

        await state.deleteEntry(path: "a.md", isDirectory: false)?.value

        XCTAssertNil(state.loadedFilePath, "deleted active tab has no loaded file")
        XCTAssertNotNil(state.noteLoadError, "the tab flips to the missing-file error state, never silent")
        XCTAssertNil(state.currentNoteText, "the stale buffer is cleared")
    }

    /// THE dangerous variant: renaming the ACTIVE note while its buffer is
    /// DIRTY. `rebindActiveIfRetargeted` touches `selectedFilePath`, which
    /// drives the load pipeline — an unguarded reload here would clobber the
    /// unsaved edits with disk content. The buffer, dirty flag, and caret
    /// anchor (content hash) must all survive the rename untouched.
    func testRenameOfDirtyActiveNotePreservesUnsavedBuffer() async throws {
        let (state, _) = try await makeVault(files: ["a.md", "b.md"])
        state.selectedFilePath = "a.md"
        await state.noteLoadTask?.value
        let dirty = "# a.md\nEDITED, unsaved ✏️\n"
        state.updateEditorText(dirty)
        XCTAssertTrue(state.hasUnsavedChanges)

        await state.renameEntry(path: "a.md", isDirectory: false, to: "alpha.md")?.value
        await state.noteLoadTask?.value

        XCTAssertEqual(state.loadedFilePath, "alpha.md")
        XCTAssertEqual(
            state.currentNoteText, dirty,
            "rename must never clobber an unsaved buffer with disk content")
        XCTAssertTrue(state.hasUnsavedChanges, "dirty flag survives the rename")
    }

    /// Same property for a PARKED tab: a dirty background buffer follows the
    /// rename through the document rebind (NoteDocument.path is immutable, so
    /// retarget mints a fresh document carrying the old buffer state).
    func testRenameOfDirtyParkedNotePreservesItsBuffer() async throws {
        let (state, _) = try await makeVault(files: ["a.md", "b.md"])
        state.selectedFilePath = "a.md"
        await state.noteLoadTask?.value
        let dirty = "# a.md\nparked and dirty\n"
        state.updateEditorText(dirty)

        // Park a.md by opening b.md in a NEW tab, then rename a.md from the tree.
        state.openFile("b.md", target: .newTab)
        await state.noteLoadTask?.value
        XCTAssertEqual(state.loadedFilePath, "b.md")

        await state.renameEntry(path: "a.md", isDirectory: false, to: "alpha.md")?.value

        // Switch back: the retargeted tab restores the dirty buffer.
        state.selectPreviousTab()
        await state.noteLoadTask?.value
        XCTAssertEqual(state.loadedFilePath, "alpha.md")
        XCTAssertEqual(
            state.currentNoteText, dirty,
            "parked dirty buffer must survive the rename rebind")
        XCTAssertTrue(state.hasUnsavedChanges)
    }

    func testRenameRetargetsAFolderDescendantOpenInATab() async throws {
        let (state, _) = try await makeVault(files: ["proj/a.md"])
        state.selectedFilePath = "proj/a.md"
        await state.noteLoadTask?.value

        await state.renameEntry(path: "proj", isDirectory: true, to: "project")?.value

        XCTAssertEqual(
            state.loadedFilePath, "project/a.md",
            "an open descendant of a renamed folder follows the move")
    }

    // MARK: - Keyboard-only walkthrough (through the command registry)

    /// Scripted through the registry (no pointer): create a folder, then create
    /// a note, then move it into the folder — the DoD's drag-free path.
    func testKeyboardOnlyCreateAndMoveThroughRegistry() async throws {
        let (state, vault) = try await makeVault(files: ["seed.md"])
        try publishSidebarSelection(state)

        // New Folder (context/palette command) → creates + enters rename.
        try state.commandRegistry.invokeById(id: SlateCommandID.newFolder)
        await state.pendingStructuralTaskForTesting?.value
        XCTAssertTrue(fileExists(vault, "Untitled Folder"))

        // Select the folder, then New Note into it.
        try publishSidebarSelection(
            state, path: "Untitled Folder", isDirectory: true)
        try state.commandRegistry.invokeById(id: SlateCommandID.newNote)
        await state.pendingStructuralTaskForTesting?.value
        await state.noteLoadTask?.value
        XCTAssertTrue(fileExists(vault, "Untitled Folder/Untitled.md"))
    }

    func testRenameCommandEntersRenameModeForSelection() async throws {
        let (state, _) = try await makeVault(files: ["a.md"])
        try publishSidebarSelection(state, path: "a.md")
        try state.commandRegistry.invokeById(id: SlateCommandID.renameEntry)
        XCTAssertEqual(state.renamingNode?.path, "a.md")
        XCTAssertEqual(state.renamingNode?.isDirectory, false)
        XCTAssertEqual(
            state.renamingNode?.sessionIdentity,
            state.currentSession.map(ObjectIdentifier.init))
    }

    func testMoveCommandOpensSheetForSelection() async throws {
        let (state, _) = try await makeVault(files: ["a.md"])
        try publishSidebarSelection(state, path: "a.md")
        try state.commandRegistry.invokeById(id: SlateCommandID.moveTo)
        XCTAssertEqual(state.pendingMove?.path, "a.md")
    }

    func testDeleteCommandDeletesSelection() async throws {
        let (state, vault) = try await makeVault(files: ["a.md", "b.md"])
        try publishSidebarSelection(state, path: "a.md")
        try state.commandRegistry.invokeById(id: SlateCommandID.deleteEntry)
        await state.pendingStructuralTaskForTesting?.value
        XCTAssertFalse(fileExists(vault, "a.md"))
    }

    func testCommandsRejectWithoutSelection() async throws {
        let (state, vault) = try await makeVault(files: ["a.md"])
        try publishSidebarSelection(state)

        let rejections = [
            (
                SlateCommandID.renameEntry,
                "Select exactly one file or folder to rename."
            ),
            (
                SlateCommandID.moveTo,
                "Select one or more files or folders to move."
            ),
            (
                SlateCommandID.deleteEntry,
                "Select one or more files or folders to move to the Trash."
            ),
        ]
        for (id, expectedReason) in rejections {
            XCTAssertThrowsError(try state.commandRegistry.invokeById(id: id), id) { error in
                guard case let CommandError.ActionFailed(message) = error else {
                    return XCTFail("\(id) returned \(error), not ActionFailed")
                }
                XCTAssertEqual(message, expectedReason, id)
            }
        }

        XCTAssertTrue(fileExists(vault, "a.md"), "rejected commands have no filesystem effect")
        XCTAssertNil(state.renamingNode)
        XCTAssertNil(state.pendingMove)
        XCTAssertNil(state.pendingBatchMove)
        XCTAssertNil(state.pendingFolderDelete)
        XCTAssertNil(state.pendingBatchDelete)
        XCTAssertNil(state.pendingStructuralTaskForTesting)
    }

    // MARK: - creationParentPath

    func testCreationParentPathRules() async throws {
        let (state, _) = try await makeVault(files: ["proj/a.md"])
        // Nothing selected → root.
        state.treeSelectedNode = nil
        XCTAssertEqual(state.creationParentPath, "")
        // Folder selected → the folder itself.
        state.treeSelectedNode = AppState.TreeSelection(path: "proj", isDirectory: true)
        XCTAssertEqual(state.creationParentPath, "proj")
        // File selected → its parent.
        state.treeSelectedNode = AppState.TreeSelection(path: "proj/a.md", isDirectory: false)
        XCTAssertEqual(state.creationParentPath, "proj")
    }

    // MARK: - Path helpers (pure)

    func testJoinVaultPath() {
        XCTAssertEqual(AppState.joinVaultPath("", "a.md"), "a.md")
        XCTAssertEqual(AppState.joinVaultPath("dir", "a.md"), "dir/a.md")
        XCTAssertEqual(AppState.joinVaultPath("a/b", "c.md"), "a/b/c.md")
    }

    func testSiblingPath() {
        XCTAssertEqual(AppState.siblingPath(of: "dir/a.md", newName: "b.md"), "dir/b.md")
        XCTAssertEqual(AppState.siblingPath(of: "a.md", newName: "b.md"), "b.md")
    }

    func testPathIsWithin() {
        XCTAssertTrue(AppState.pathIsWithin("a.md", path: "a.md", isDirectory: false))
        XCTAssertFalse(AppState.pathIsWithin("ab.md", path: "a.md", isDirectory: false))
        XCTAssertTrue(AppState.pathIsWithin("proj/a.md", path: "proj", isDirectory: true))
        XCTAssertTrue(AppState.pathIsWithin("proj/sub/a.md", path: "proj", isDirectory: true))
        XCTAssertFalse(AppState.pathIsWithin("project/a.md", path: "proj", isDirectory: true))
        // A file (not a dir) never contains descendants.
        XCTAssertFalse(AppState.pathIsWithin("proj/a.md", path: "proj", isDirectory: false))
    }

    func testTreeMutationParentPath() {
        XCTAssertNil(AppState.TreeMutation.parentPath(of: "a.md"))
        XCTAssertEqual(AppState.TreeMutation.parentPath(of: "dir/a.md"), "dir")
        XCTAssertEqual(AppState.TreeMutation.parentPath(of: "a/b/c.md"), "a/b")
    }

    func testTreeMutationAffectedParents() {
        // Create at root → [nil].
        XCTAssertEqual(
            AppState.TreeMutation(
                token: 1, kind: .createNote(path: "a.md"), rewrittenCount: 0).affectedParents,
            [nil])
        // Create nested → [parent].
        XCTAssertEqual(
            AppState.TreeMutation(
                token: 1, kind: .createFolder(path: "d/e"), rewrittenCount: 0).affectedParents,
            ["d"])
        // Move dirties source + destination (root normalized to nil).
        XCTAssertEqual(
            AppState.TreeMutation(
                token: 1,
                kind: .move(oldPath: "src/a.md", newPath: "a.md", oldParent: "src", newParent: ""),
                rewrittenCount: 0
            ).affectedParents,
            ["src", nil])

        let standing = [
            BatchPathChange(
                oldPath: "src/a.md", newPath: "dest/a.md", isDirectory: false),
            BatchPathChange(
                oldPath: "src/b.md", newPath: "dest/b.md", isDirectory: false),
            BatchPathChange(
                oldPath: "root.md", newPath: "dest/root.md", isDirectory: false),
        ]
        let rollbackTouched = [
            BatchPathChange(
                oldPath: "rollback/x.md", newPath: "src/x.md", isDirectory: false),
            BatchPathChange(
                oldPath: "root-2.md", newPath: "dest/root-2.md", isDirectory: false),
        ]
        XCTAssertEqual(
            AppState.TreeMutation(
                token: 2,
                kind: .batchMove(standing: standing, touched: rollbackTouched),
                rewrittenCount: 0
            ).affectedParents,
            ["src", "dest", nil, "rollback"],
            "batch Move invalidates standing and rollback-touched parents once, in report order")
        XCTAssertEqual(
            AppState.TreeMutation(
                token: 3,
                kind: .batchTrash(trashed: [
                    StructuralBatchItem(path: "src/a.md", isDirectory: false),
                    StructuralBatchItem(path: "root.md", isDirectory: false),
                    StructuralBatchItem(path: "src/b.md", isDirectory: false),
                    StructuralBatchItem(path: "root-2.md", isDirectory: false),
                ]),
                rewrittenCount: 0
            ).affectedParents,
            ["src", nil],
            "batch Trash stable-deduplicates repeated parent levels, including root")
        XCTAssertEqual(
            AppState.TreeMutation(
                token: 4,
                kind: .batchMove(standing: standing, touched: rollbackTouched),
                rewrittenCount: 0,
                requiresRescan: true
            ).affectedParents,
            [nil],
            "uncertain reports request exactly one authoritative root invalidation")
    }

    func testDistinctRewrittenCount() {
        let report = StructuralReport(
            opId: 1, undoOpIds: [1], moved: [],
            rewritten: [
                RewriteOutcome(path: "x.md", hashBefore: "1", hashAfter: "2"),
                RewriteOutcome(path: "x.md", hashBefore: "2", hashAfter: "3"),
                RewriteOutcome(path: "y.md", hashBefore: "1", hashAfter: "2"),
            ],
            failed: [])
        XCTAssertEqual(AppState.distinctRewrittenCount(report), 2, "counts DISTINCT files")
    }

    // MARK: - Duplicate (#853)

    /// The pure naming rule: Finder-parity " copy" suffixing with base
    /// normalization (a source already named "… copy" doesn't stack).
    func testDuplicateNamePure() {
        // Free name: first candidate wins.
        XCTAssertEqual(
            AppState.duplicateName(for: "a.md", existingLowercasedNames: ["a.md"]),
            "a copy.md")
        // "a copy.md" taken → numbered.
        XCTAssertEqual(
            AppState.duplicateName(
                for: "a.md", existingLowercasedNames: ["a.md", "a copy.md"]),
            "a copy 2.md")
        XCTAssertEqual(
            AppState.duplicateName(
                for: "a.md",
                existingLowercasedNames: ["a.md", "a copy.md", "a copy 2.md"]),
            "a copy 3.md")
        // Duplicating a copy re-uses the base — never "a copy copy.md".
        XCTAssertEqual(
            AppState.duplicateName(
                for: "a copy.md", existingLowercasedNames: ["a.md", "a copy.md"]),
            "a copy 2.md")
        XCTAssertEqual(
            AppState.duplicateName(
                for: "a copy 2.md",
                existingLowercasedNames: ["a copy 2.md"]),
            "a copy.md")
        // Case-insensitive collision (APFS default).
        XCTAssertEqual(
            AppState.duplicateName(
                for: "Note.md", existingLowercasedNames: ["note copy.md"]),
            "Note copy 2.md")
        // No extension degrades gracefully.
        XCTAssertEqual(
            AppState.duplicateName(for: "raw", existingLowercasedNames: ["raw"]),
            "raw copy")
    }

    func testDuplicateFileCreatesCopyWithSameContentAndAnnounces() async throws {
        let (state, vault) = try await makeVault(files: ["a.md", "other.md"])
        await state.duplicateEntry(path: "a.md")?.value

        XCTAssertTrue(fileExists(vault, "a copy.md"), "copy created on disk")
        XCTAssertNil(state.lastError)
        let source = try String(
            contentsOf: vault.appendingPathComponent("a.md"), encoding: .utf8)
        let copy = try String(
            contentsOf: vault.appendingPathComponent("a copy.md"), encoding: .utf8)
        XCTAssertEqual(copy, source, "byte-for-byte content copy")
        XCTAssertEqual(
            state.lastMutationAnnouncement, "Duplicated a.md as a copy.md.",
            "verbatim announcement (spec §U2-6 discipline)")
        guard case .createNote(let path)? = state.treeMutation?.kind else {
            return XCTFail("expected a createNote tree mutation for the copy")
        }
        XCTAssertEqual(path, "a copy.md", "focus funnel targets the copy")
    }

    func testDuplicateInSubfolderStaysInSubfolder() async throws {
        let (state, vault) = try await makeVault(files: ["proj/a.md"])
        await state.duplicateEntry(path: "proj/a.md")?.value
        XCTAssertTrue(fileExists(vault, "proj/a copy.md"), "sibling of the source")
        XCTAssertNil(state.lastError)
    }

    func testDuplicateAutoSuffixesAgainstIndexedSiblings() async throws {
        let (state, vault) = try await makeVault(files: ["a.md", "a copy.md"])
        await state.duplicateEntry(path: "a.md")?.value
        XCTAssertTrue(fileExists(vault, "a copy 2.md"))
        XCTAssertNil(state.lastError)
        XCTAssertEqual(state.lastMutationAnnouncement, "Duplicated a.md as a copy 2.md.")
    }

    /// The exclusive-create race (#793 context): a sibling that exists ON
    /// DISK but not in the index (external create after the scan) must not
    /// be clobbered — `create_exclusive` surfaces DestinationExists and the
    /// loop advances to the next candidate.
    func testDuplicateRaceAgainstUnindexedSiblingAdvancesNotClobbers() async throws {
        let (state, vault) = try await makeVault(files: ["a.md"])
        // Drop "a copy.md" behind the index's back.
        try "EXTERNAL — must survive\n".write(
            to: vault.appendingPathComponent("a copy.md"),
            atomically: true, encoding: .utf8)

        await state.duplicateEntry(path: "a.md")?.value

        XCTAssertNil(state.lastError)
        XCTAssertTrue(fileExists(vault, "a copy 2.md"), "loop advanced past the race")
        let external = try String(
            contentsOf: vault.appendingPathComponent("a copy.md"), encoding: .utf8)
        XCTAssertEqual(
            external, "EXTERNAL — must survive\n",
            "the un-indexed sibling was never truncated")
    }

    func testDuplicateCommandActsOnSelectionAndIgnoresFolders() async throws {
        let (state, vault) = try await makeVault(files: ["proj/a.md", "b.md"])
        // A folder selection is rejected with the catalog's exact capability
        // reason (folders are out of #853's scope) and produces no side effect.
        try publishSidebarSelection(state, path: "proj", isDirectory: true)
        XCTAssertThrowsError(
            try state.commandRegistry.invokeById(id: SlateCommandID.duplicateEntry)
        ) { error in
            guard case let CommandError.ActionFailed(message) = error else {
                return XCTFail("folder Duplicate returned \(error), not ActionFailed")
            }
            XCTAssertEqual(message, "Select exactly one file to duplicate.")
        }
        await state.pendingStructuralTaskForTesting?.value
        XCTAssertFalse(fileExists(vault, "proj copy"), "no folder duplicate")

        // A file selection duplicates through the registry.
        try publishSidebarSelection(state, path: "b.md")
        try state.commandRegistry.invokeById(id: SlateCommandID.duplicateEntry)
        await state.pendingStructuralTaskForTesting?.value
        XCTAssertTrue(fileExists(vault, "b copy.md"))
    }

    // MARK: - Non-empty-folder delete confirmation (#860)

    /// A folder with children STAGES the confirmation — nothing is deleted
    /// until the alert's Move to Trash resolves it.
    func testRequestDeleteFolderWithChildrenStagesConfirmation() async throws {
        let (state, vault) = try await makeVault(files: ["proj/a.md", "proj/b.md"])
        state.requestDeleteEntry(path: "proj", isDirectory: true)
        let pending = try XCTUnwrap(state.pendingFolderDelete)
        XCTAssertEqual(pending.path, "proj")
        XCTAssertEqual(pending.itemCount, 2, "staged with the FileManager shallow count")
        XCTAssertTrue(fileExists(vault, "proj/a.md"), "nothing deleted yet")

        XCTAssertTrue(state.confirmPendingFolderDelete(id: pending.id))
        await state.pendingStructuralTaskForTesting?.value
        XCTAssertNil(state.pendingFolderDelete)
        XCTAssertFalse(fileExists(vault, "proj/a.md"), "confirmed → trashed")
        XCTAssertEqual(state.lastMutationAnnouncement, "Moved proj to Trash.")
    }

    func testCancelPendingFolderDeleteKeepsEverything() async throws {
        let (state, vault) = try await makeVault(files: ["proj/a.md"])
        state.requestDeleteEntry(path: "proj", isDirectory: true)
        let pending = try XCTUnwrap(state.pendingFolderDelete)

        XCTAssertTrue(state.cancelPendingFolderDelete(id: pending.id))
        await state.pendingStructuralTaskForTesting?.value
        XCTAssertNil(state.pendingFolderDelete)
        XCTAssertTrue(fileExists(vault, "proj/a.md"), "cancel deletes nothing")
    }

    /// Empty folders keep the no-confirm Finder-parity path.
    func testRequestDeleteEmptyFolderSkipsConfirmation() async throws {
        let (state, vault) = try await makeVault(files: ["keep.md"])
        await state.createFolder(name: "empty", in: "")?.value
        XCTAssertTrue(fileExists(vault, "empty"))

        state.requestDeleteEntry(path: "empty", isDirectory: true)
        XCTAssertNil(state.pendingFolderDelete, "0 children ⇒ no alert")
        await state.pendingStructuralTaskForTesting?.value
        XCTAssertFalse(fileExists(vault, "empty"), "deleted directly")
    }

    /// Files never stage, regardless of route.
    func testRequestDeleteFileSkipsConfirmation() async throws {
        let (state, vault) = try await makeVault(files: ["a.md"])
        state.requestDeleteEntry(path: "a.md", isDirectory: false)
        XCTAssertNil(state.pendingFolderDelete)
        await state.pendingStructuralTaskForTesting?.value
        XCTAssertFalse(fileExists(vault, "a.md"))
    }

    /// Positive cached counts are trusted verbatim (no filesystem probe);
    /// a cached ZERO re-probes — a stale zero must never bypass the
    /// confirmation and trash a non-empty folder unprompted (red-team).
    func testRequestDeleteHonorsKnownChildCount() async throws {
        let (state, vault) = try await makeVault(files: ["proj/a.md"])
        // knownChildCount: 0 with a child actually on disk: the zero is
        // treated as unknown, the probe finds the child, the alert stages.
        state.requestDeleteEntry(path: "proj", isDirectory: true, knownChildCount: 0)
        let pending = try XCTUnwrap(state.pendingFolderDelete)
        XCTAssertEqual(pending.itemCount, 1)
        XCTAssertTrue(state.cancelPendingFolderDelete(id: pending.id))

        // HIDDEN-only contents still confirm (Codex r2/r3: .skipsHiddenFiles
        // made a folder holding only `.env` fail OPEN — exactly the
        // deletions that hurt). .DS_Store alone would NOT count.
        try FileManager.default.moveItem(
            at: vault.appendingPathComponent("proj/a.md"),
            to: vault.appendingPathComponent("proj/.env"))
        state.requestDeleteEntry(path: "proj", isDirectory: true, knownChildCount: nil)
        let hiddenPending = try XCTUnwrap(
            state.pendingFolderDelete, "hidden-only folder must still confirm")
        XCTAssertEqual(hiddenPending.itemCount, 1)
        XCTAssertTrue(state.confirmPendingFolderDelete(id: hiddenPending.id))
        await state.pendingStructuralTaskForTesting?.value
        XCTAssertFalse(fileExists(vault, "proj"))

        // knownChildCount: 3 stages without touching the filesystem — the
        // path doesn't even exist anymore.
        state.requestDeleteEntry(path: "proj", isDirectory: true, knownChildCount: 3)
        let trusted = try XCTUnwrap(state.pendingFolderDelete)
        XCTAssertEqual(trusted.path, "proj")
        XCTAssertEqual(trusted.itemCount, 3)
        XCTAssertTrue(state.cancelPendingFolderDelete(id: trusted.id))
    }

    /// The registry's Move to Trash routes folder selections through the
    /// same staging funnel (#860 covers every surface, not just the tree).
    func testDeleteCommandStagesForNonEmptyFolderSelection() async throws {
        let (state, vault) = try await makeVault(files: ["proj/a.md"])
        try publishSidebarSelection(state, path: "proj", isDirectory: true)
        try state.commandRegistry.invokeById(id: SlateCommandID.deleteEntry)
        let pending = try XCTUnwrap(
            state.pendingFolderDelete, "palette/menu path stages too")
        XCTAssertTrue(fileExists(vault, "proj/a.md"))
        XCTAssertTrue(state.cancelPendingFolderDelete(id: pending.id))
    }

    // MARK: - Expansion persistence (#873, AppState round trip)

    /// The expanded-folder mirror persists through workspace.json and
    /// rehydrates across a close/reopen cycle: closeVault saves the live
    /// set FIRST (the U1-6 order), the teardown resets the mirror, and
    /// restoreWorkspaceLayout refills it before any view could bind the
    /// tree.
    func testTreeExpansionPersistsAcrossVaultReopen() async throws {
        let (state, vault) = try await makeVault(files: ["proj/a.md"])
        state.treeExpandedDirPaths = ["proj", "archive"]
        state.closeVault()
        XCTAssertEqual(state.treeExpandedDirPaths, [], "mirror dies with the vault")

        state.openVault(at: vault)
        await state.scanTask?.value
        XCTAssertEqual(
            state.treeExpandedDirPaths, ["proj", "archive"],
            "restored in recency order, synchronously inside openVault")
    }
}
