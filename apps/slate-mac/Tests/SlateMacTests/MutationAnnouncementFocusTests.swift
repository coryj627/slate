// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import XCTest

@testable import SlateMac

/// U2-6 (#464): mutation announcements + focus preservation.
///
/// Two halves:
///  - VERBATIM announcement strings (asserted against `lastMutationAnnouncement`,
///    which records what `postMutationAnnouncement` routed — the announcement
///    itself is a no-op under XCTest, so the string is observed here), and
///  - `FileTreeViewModel` post-mutation focus-target computation for every
///    mutation × edge position, plus the delete-last-file-in-folder integration.
@MainActor
final class MutationAnnouncementFocusTests: XCTestCase {

    // MARK: - Fixture builders (mirror FileTreeSidebarTests)

    private func dir(
        _ id: Int64, _ path: String, dirCount: Int = 0, fileCount: Int = 0
    ) -> DirNodeSummary {
        DirNodeSummary(
            id: id, path: path, name: (path as NSString).lastPathComponent,
            childDirCount: UInt32(dirCount), childFileCount: UInt32(fileCount))
    }

    private func file(_ path: String) -> FileSummary {
        FileSummary(
            path: path, name: (path as NSString).lastPathComponent, mtimeMs: 0,
            sizeBytes: 0, isMarkdown: true)
    }

    private func listing(dirs: [DirNodeSummary], files: [FileSummary]) -> DirListing {
        DirListing(
            dirs: dirs,
            files: FileSummaryPage(
                items: files, nextCursor: nil, totalFiltered: UInt64(files.count)))
    }

    private final class FetchSpy {
        let table: [String: DirListing]
        init(_ table: [String: DirListing]) { self.table = table }
        func fetch(_ parentPath: String) throws -> DirListing {
            table[parentPath]
                ?? DirListing(
                    dirs: [], files: FileSummaryPage(items: [], nextCursor: nil, totalFiltered: 0))
        }
    }

    // MARK: - VERBATIM announcement strings

    private var tempDir: URL!

    override func setUpWithError() throws {
        tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("mut-announce-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: tempDir, withIntermediateDirectories: true)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: tempDir)
    }

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

    func testCreateFolderAnnouncementVerbatim() async throws {
        let (state, _) = try await makeVault(files: ["a.md"])
        await state.createFolder(name: "Notes", in: "")?.value
        XCTAssertEqual(state.lastMutationAnnouncement, "Created folder Notes.")
    }

    func testRenameAnnouncementVerbatim() async throws {
        let (state, _) = try await makeVault(files: ["a.md", "b.md"])
        await state.renameEntry(path: "a.md", isDirectory: false, to: "alpha.md")?.value
        XCTAssertEqual(state.lastMutationAnnouncement, "Renamed a.md to alpha.md.")
    }

    func testMoveAnnouncementVerbatim() async throws {
        let (state, _) = try await makeVault(files: ["a.md", "dest/x.md"])
        await state.moveEntry(path: "a.md", isDirectory: false, to: "dest")?.value
        XCTAssertEqual(state.lastMutationAnnouncement, "Moved a.md to dest.")
    }

    func testMoveToVaultRootAnnouncementSaysVaultRoot() async throws {
        let (state, _) = try await makeVault(files: ["sub/a.md"])
        await state.moveEntry(path: "sub/a.md", isDirectory: false, to: "")?.value
        XCTAssertEqual(state.lastMutationAnnouncement, "Moved a.md to vault root.")
    }

    func testDeleteAnnouncementVerbatim() async throws {
        let (state, _) = try await makeVault(files: ["a.md", "b.md"])
        await state.deleteEntry(path: "a.md", isDirectory: false)?.value
        XCTAssertEqual(state.lastMutationAnnouncement, "Moved a.md to Trash.")
    }

    /// The ", updated links in N notes." suffix (spec §U2-6) — count is DISTINCT
    /// rewritten files. Asserted on the pure `withLinksSuffix` builder because
    /// the base branch's mutation surface returns an EMPTY `rewritten` list
    /// (U2-3 part 2 — the rewriter's session integration — is not wired here;
    /// see the reported deviation), so the suffix can't be produced end-to-end
    /// yet. This locks the verbatim phrasing so it's correct the moment link
    /// rewriting lands.
    func testUpdatedLinksSuffixVerbatimPlural() {
        XCTAssertEqual(
            AppState.withLinksSuffix("Renamed target.md to goal.md.", rewrittenCount: 2),
            "Renamed target.md to goal.md, updated links in 2 notes.")
    }

    func testUpdatedLinksSuffixVerbatimSingular() {
        XCTAssertEqual(
            AppState.withLinksSuffix("Renamed target.md to goal.md.", rewrittenCount: 1),
            "Renamed target.md to goal.md, updated links in 1 note.")
    }

    func testNoLinksSuffixWhenZeroRewrites() {
        XCTAssertEqual(
            AppState.withLinksSuffix("Moved a.md to dest.", rewrittenCount: 0),
            "Moved a.md to dest.",
            "no rewrites ⇒ no suffix, base string unchanged")
    }

    func testFailureAnnouncementVerbatim() async throws {
        let (state, _) = try await makeVault(files: ["a.md", "b.md"])
        // Rename a.md → b.md collides → "Could not rename a.md: <reason>."
        await state.renameEntry(path: "a.md", isDirectory: false, to: "b.md")?.value
        let announcement = try XCTUnwrap(state.lastMutationAnnouncement)
        XCTAssertTrue(
            announcement.hasPrefix("Could not rename a.md: "),
            "failure form is 'Could not <verb> <name>: <reason>.' — got \(announcement)")
    }

    // MARK: - Focus target: CREATE → new row

    func testCreateFolderFocusTargetsNewFolderRow() {
        let spy = FetchSpy(["": listing(dirs: [dir(7, "Notes")], files: [])])
        let vm = FileTreeViewModel()
        vm.bindForTesting(fetcher: spy.fetch)
        XCTAssertEqual(vm.focusTarget(forPath: "Notes"), .dir(7))
    }

    func testCreateNoteFocusTargetsNewFileRow() {
        let spy = FetchSpy(["": listing(dirs: [], files: [file("Untitled.md")])])
        let vm = FileTreeViewModel()
        vm.bindForTesting(fetcher: spy.fetch)
        XCTAssertEqual(vm.focusTarget(forPath: "Untitled.md"), .file(path: "Untitled.md"))
    }

    func testFocusTargetNilWhenLevelNotMaterialized() {
        let spy = FetchSpy(["": listing(dirs: [dir(1, "a")], files: [])])
        let vm = FileTreeViewModel()
        vm.bindForTesting(fetcher: spy.fetch)
        // "a/x.md" lives in an unexpanded level → not resolvable yet.
        XCTAssertNil(vm.focusTarget(forPath: "a/x.md"))
    }

    // MARK: - Focus target: RENAME → keep row (at new path)

    func testRenameFocusTargetsRenamedFileAtNewPath() {
        let spy = FetchSpy(["": listing(dirs: [], files: [file("alpha.md")])])
        let vm = FileTreeViewModel()
        vm.bindForTesting(fetcher: spy.fetch)
        XCTAssertEqual(vm.focusTarget(forPath: "alpha.md"), .file(path: "alpha.md"))
    }

    // MARK: - Focus target: MOVE → follow + auto-expand ancestors

    func testEnsureAncestorsExpandedRevealsMovedNode() {
        let spy = FetchSpy([
            "": listing(dirs: [dir(1, "dest")], files: []),
            "dest": listing(dirs: [dir(2, "dest/sub")], files: []),
            "dest/sub": listing(dirs: [], files: [file("dest/sub/a.md")]),
        ])
        let vm = FileTreeViewModel()
        vm.bindForTesting(fetcher: spy.fetch)
        // Nothing expanded yet.
        XCTAssertTrue(vm.expanded.isEmpty)

        vm.ensureAncestorsExpanded(forPath: "dest/sub/a.md")

        // Both ancestor folders are now expanded, so the moved file is visible.
        XCTAssertTrue(vm.expanded.contains(.dir(1)), "dest expanded")
        XCTAssertTrue(vm.expanded.contains(.dir(2)), "dest/sub expanded")
        XCTAssertEqual(vm.focusTarget(forPath: "dest/sub/a.md"), .file(path: "dest/sub/a.md"))
    }

    func testEnsureAncestorsExpandedNoOpForRootLevelPath() {
        let spy = FetchSpy(["": listing(dirs: [], files: [file("a.md")])])
        let vm = FileTreeViewModel()
        vm.bindForTesting(fetcher: spy.fetch)
        vm.ensureAncestorsExpanded(forPath: "a.md")
        XCTAssertTrue(vm.expanded.isEmpty, "a root-level file has no ancestors to expand")
    }

    // MARK: - Focus target: DELETE → next sibling / prev / parent, never root

    /// Delete a MIDDLE child → next sibling.
    func testDeleteFocusTargetsNextSibling() {
        let spy = FetchSpy([
            "": listing(dirs: [], files: [file("a.md"), file("b.md"), file("c.md")])
        ])
        let vm = FileTreeViewModel()
        vm.bindForTesting(fetcher: spy.fetch)
        // Deleting b.md (middle) → next sibling c.md.
        XCTAssertEqual(
            vm.deleteFocusTarget(deletedPath: "b.md", parentPath: ""),
            .file(path: "c.md"))
    }

    /// Delete the FIRST child → next sibling.
    func testDeleteFirstChildTargetsNextSibling() {
        let spy = FetchSpy(["": listing(dirs: [], files: [file("a.md"), file("b.md")])])
        let vm = FileTreeViewModel()
        vm.bindForTesting(fetcher: spy.fetch)
        XCTAssertEqual(
            vm.deleteFocusTarget(deletedPath: "a.md", parentPath: ""),
            .file(path: "b.md"))
    }

    /// Delete the LAST child → previous sibling.
    func testDeleteLastChildTargetsPreviousSibling() {
        let spy = FetchSpy(["": listing(dirs: [], files: [file("a.md"), file("b.md")])])
        let vm = FileTreeViewModel()
        vm.bindForTesting(fetcher: spy.fetch)
        XCTAssertEqual(
            vm.deleteFocusTarget(deletedPath: "b.md", parentPath: ""),
            .file(path: "a.md"))
    }

    /// Delete the ONLY child of a folder → the parent folder (not the window
    /// root). This is the spec's delete-last-file-in-folder rule.
    func testDeleteOnlyChildInFolderTargetsParentFolder() {
        let spy = FetchSpy([
            "": listing(dirs: [dir(1, "folder")], files: []),
            "folder": listing(dirs: [], files: [file("folder/only.md")]),
        ])
        let vm = FileTreeViewModel()
        vm.bindForTesting(fetcher: spy.fetch)
        let f = try! XCTUnwrap(vm.node(for: .dir(1)))
        vm.expand(f)  // materialize the folder's children

        XCTAssertEqual(
            vm.deleteFocusTarget(deletedPath: "folder/only.md", parentPath: "folder"),
            .dir(1),
            "deleting the last file in a folder lands on the folder — never dead focus")
    }

    /// Delete the ONLY entry at the vault ROOT → nil (no move; never the window
    /// root). The list keeps a valid (empty) state.
    func testDeleteOnlyRootEntryTargetsNil() {
        let spy = FetchSpy(["": listing(dirs: [], files: [file("solo.md")])])
        let vm = FileTreeViewModel()
        vm.bindForTesting(fetcher: spy.fetch)
        XCTAssertNil(
            vm.deleteFocusTarget(deletedPath: "solo.md", parentPath: ""),
            "the only root entry deleted → nil target, never window-root")
    }

    /// A mix of dirs + files at a level: deleting a folder targets the next
    /// sibling (which may be a file) — order is the API's dirs-then-files.
    func testDeleteFolderTargetsNextSiblingAcrossKinds() {
        let spy = FetchSpy([
            "": listing(dirs: [dir(1, "adir"), dir(2, "bdir")], files: [file("c.md")])
        ])
        let vm = FileTreeViewModel()
        vm.bindForTesting(fetcher: spy.fetch)
        // Level order: adir, bdir, c.md. Delete bdir → next sibling c.md.
        XCTAssertEqual(
            vm.deleteFocusTarget(deletedPath: "bdir", parentPath: ""),
            .file(path: "c.md"))
    }

    // MARK: - Integration: delete last file in a folder (no dead focus)

    /// End-to-end: an open note that's the last file in its folder is deleted;
    /// the workspace tab flips to the error state (U2-5) AND the focus target is
    /// the parent folder (U2-6) — no dead focus.
    func testDeleteLastFileInFolderIntegration() async throws {
        let vault = tempDir.appendingPathComponent("vault")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        let folder = vault.appendingPathComponent("folder")
        try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
        try "# only\n".write(
            to: folder.appendingPathComponent("only.md"), atomically: true, encoding: .utf8)
        try "# root\n".write(
            to: vault.appendingPathComponent("root.md"), atomically: true, encoding: .utf8)
        let state = AppState(
            recentsStore: RecentVaultsStore(
                fileURL: tempDir.appendingPathComponent("recents.json")),
            externalOpener: { _ in true })
        state.openVault(at: vault)
        await state.scanTask?.value
        state.selectedFilePath = "folder/only.md"
        await state.noteLoadTask?.value

        // The tree VM must know the folder's children for the focus computation.
        let vm = FileTreeViewModel()
        vm.bind(to: state.currentSession)
        let f = try XCTUnwrap(vm.node(for: .dir(vm.rootLevel.first { $0.name == "folder" }!.nodeID.dirID!)))
        vm.expand(f)
        let focusTarget = vm.deleteFocusTarget(
            deletedPath: "folder/only.md", parentPath: "folder")

        await state.deleteEntry(path: "folder/only.md", isDirectory: false)?.value

        // Tab flipped to the error state (U2-5).
        XCTAssertNil(state.loadedFilePath)
        XCTAssertNotNil(state.noteLoadError)
        // Focus lands on the parent folder, not dead (U2-6).
        XCTAssertEqual(focusTarget, f.nodeID, "focus lands on the containing folder")
        XCTAssertEqual(state.lastMutationAnnouncement, "Moved only.md to Trash.")
    }
}

/// Test-only helper: pull the `Int64` id out of a `.dir` NodeID.
extension NodeID {
    fileprivate var dirID: Int64? {
        if case let .dir(id) = self { return id }
        return nil
    }
}
