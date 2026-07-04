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
        XCTAssertEqual(
            state.renamingNode, AppState.RenamingNode(path: "Untitled.md", isDirectory: false),
            "drops into inline rename of the new note")
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
        state.renamingNode = AppState.RenamingNode(path: "a.md", isDirectory: false)
        // Rename a.md → b.md (collides).
        await state.renameEntry(path: "a.md", isDirectory: false, to: "b.md")?.value

        XCTAssertNotNil(state.structuralRenameError, "collision surfaces a specific inline error")
        XCTAssertEqual(
            state.renamingNode, AppState.RenamingNode(path: "a.md", isDirectory: false),
            "rename field stays open + focused so the user can correct — never silent")
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

        // New Folder (context/palette command) → creates + enters rename.
        try state.commandRegistry.invokeById(id: SlateCommandID.newFolder)
        await state.pendingStructuralTaskForTesting?.value
        XCTAssertTrue(fileExists(vault, "Untitled Folder"))

        // Select the folder, then New Note into it.
        state.treeSelectedNode = AppState.TreeSelection(path: "Untitled Folder", isDirectory: true)
        try state.commandRegistry.invokeById(id: SlateCommandID.newNote)
        await state.pendingStructuralTaskForTesting?.value
        await state.noteLoadTask?.value
        XCTAssertTrue(fileExists(vault, "Untitled Folder/Untitled.md"))
    }

    func testRenameCommandEntersRenameModeForSelection() async throws {
        let (state, _) = try await makeVault(files: ["a.md"])
        state.treeSelectedNode = AppState.TreeSelection(path: "a.md", isDirectory: false)
        try state.commandRegistry.invokeById(id: SlateCommandID.renameEntry)
        XCTAssertEqual(
            state.renamingNode, AppState.RenamingNode(path: "a.md", isDirectory: false))
    }

    func testMoveCommandOpensSheetForSelection() async throws {
        let (state, _) = try await makeVault(files: ["a.md"])
        state.treeSelectedNode = AppState.TreeSelection(path: "a.md", isDirectory: false)
        try state.commandRegistry.invokeById(id: SlateCommandID.moveTo)
        XCTAssertEqual(state.pendingMove?.path, "a.md")
    }

    func testDeleteCommandDeletesSelection() async throws {
        let (state, vault) = try await makeVault(files: ["a.md", "b.md"])
        state.treeSelectedNode = AppState.TreeSelection(path: "a.md", isDirectory: false)
        try state.commandRegistry.invokeById(id: SlateCommandID.deleteEntry)
        await state.pendingStructuralTaskForTesting?.value
        XCTAssertFalse(fileExists(vault, "a.md"))
    }

    func testCommandsNoOpWithoutSelection() async throws {
        let (state, _) = try await makeVault(files: ["a.md"])
        state.treeSelectedNode = nil
        // Rename / move / delete need a target — must not crash or act.
        try state.commandRegistry.invokeById(id: SlateCommandID.renameEntry)
        try state.commandRegistry.invokeById(id: SlateCommandID.moveTo)
        try state.commandRegistry.invokeById(id: SlateCommandID.deleteEntry)
        XCTAssertNil(state.renamingNode)
        XCTAssertNil(state.pendingMove)
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
    }

    func testDistinctRewrittenCount() {
        let report = StructuralReport(
            opId: 1, moved: [],
            rewritten: [
                RewriteOutcome(path: "x.md", hashBefore: "1", hashAfter: "2"),
                RewriteOutcome(path: "x.md", hashBefore: "2", hashAfter: "3"),
                RewriteOutcome(path: "y.md", hashBefore: "1", hashAfter: "2"),
            ],
            failed: [])
        XCTAssertEqual(AppState.distinctRewrittenCount(report), 2, "counts DISTINCT files")
    }
}
