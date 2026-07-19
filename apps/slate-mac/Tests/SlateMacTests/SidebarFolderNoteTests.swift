// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// FL6-1 (#667): folder notes — hidden-row/count honesty, presentation
/// and AX, the three lifecycle verbs, compound rename routing through
/// the ONE core operation, and the active-note highlight mapping.
@MainActor
final class SidebarFolderNoteTests: XCTestCase {
    private var root: URL!

    override func setUpWithError() throws {
        try super.setUpWithError()
        root = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-folder-note-\(UUID().uuidString)")
        try FileManager.default.createDirectory(
            at: root, withIntermediateDirectories: true)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: root)
        try super.tearDownWithError()
    }

    private func openVault(
        named name: String, files: [String]
    ) throws -> (state: AppState, vault: URL) {
        let vault = root.appendingPathComponent(name)
        try FileManager.default.createDirectory(
            at: vault, withIntermediateDirectories: true)
        for path in files {
            let url = vault.appendingPathComponent(path)
            try FileManager.default.createDirectory(
                at: url.deletingLastPathComponent(),
                withIntermediateDirectories: true)
            try "# \(path)\n".write(to: url, atomically: true, encoding: .utf8)
        }
        let state = AppState(
            recentsStore: RecentVaultsStore(
                fileURL: root.appendingPathComponent("\(name)-recents.json")))
        state.openVault(at: vault)
        _ = try XCTUnwrap(state.currentSession).scanInitial(
            cancel: CancelToken())
        return (state, vault)
    }

    private func publishFolder(_ state: AppState, path: String) throws {
        _ = state.publishSidebarSelectionSnapshot(
            SidebarSelectionSnapshot(
                sessionIdentity: ObjectIdentifier(
                    try XCTUnwrap(state.currentSession)),
                items: [
                    SidebarSelectionItem(
                        path: path, isDirectory: true, isMarkdown: false)
                ],
                focusedPath: path,
                creationParent: path))
    }

    // MARK: - Model: hidden row + honest counts

    func testRepresentedNoteRowIsHiddenWhileCountsStayHonest() {
        let listing = DirListing(
            dirs: [],
            files: FileSummaryPage(
                items: [
                    FileSummary(
                        path: "P/P.md", name: "P.md", mtimeMs: 0, sizeBytes: 0,
                        isMarkdown: true, displayName: nil, createdDate: nil,
                        createdMs: nil, wordCount: nil, preview: nil,
                        taskTotal: 0, taskOpen: 0),
                    FileSummary(
                        path: "P/other.md", name: "other.md", mtimeMs: 0,
                        sizeBytes: 0, isMarkdown: true, displayName: nil,
                        createdDate: nil, createdMs: nil, wordCount: nil,
                        preview: nil, taskTotal: 0, taskOpen: 0),
                ],
                nextCursor: nil, totalFiltered: 2))
        let nodes = FileTreeViewModel.nodes(from: listing, depth: 1)
        XCTAssertEqual(
            nodes.map(\.path), ["P/other.md"],
            "the represented note row is hidden from the expanded children")

        // The folder's own row keeps the honest count including the
        // hidden note (spec rule 2).
        let folder = TreeNode(
            nodeID: .dir(1), path: "P", name: "P", depth: 0,
            kind: .directory(
                childDirCount: 0, childFileCount: 2, hasFolderNote: true))
        XCTAssertEqual(folder.itemCount, 2)
        XCTAssertEqual(folder.folderNotePath, "P/P.md")
    }

    func testIsRepresentedFolderNoteMatchesTheCoreConvention() {
        XCTAssertTrue(
            FileTreeViewModel.isRepresentedFolderNote(path: "P/P.md", name: "P.md"))
        XCTAssertTrue(
            FileTreeViewModel.isRepresentedFolderNote(
                path: "a/b/b.md", name: "b.md"))
        XCTAssertFalse(
            FileTreeViewModel.isRepresentedFolderNote(
                path: "XA/A.md", name: "A.md"),
            "slash-anchored: a folder XA does not own A.md as its note")
        XCTAssertFalse(
            FileTreeViewModel.isRepresentedFolderNote(
                path: "P/p.md", name: "p.md"),
            "case-sensitive exact stem")
        XCTAssertFalse(
            FileTreeViewModel.isRepresentedFolderNote(path: "P.md", name: "P.md"),
            "a root-level file has no owning folder")
    }

    func testFolderAccessibilityValueSpeaksPresence() {
        let with = TreeNode(
            nodeID: .dir(1), path: "P", name: "P", depth: 0,
            kind: .directory(
                childDirCount: 0, childFileCount: 1, hasFolderNote: true))
        XCTAssertEqual(
            FileTreeSidebar.folderAccessibilityValue(for: with, expanded: false),
            "collapsed, 1 item, level 1, has folder note")
        let without = TreeNode(
            nodeID: .dir(2), path: "Q", name: "Q", depth: 1,
            kind: .directory(
                childDirCount: 2, childFileCount: 1, hasFolderNote: false))
        XCTAssertEqual(
            FileTreeSidebar.folderAccessibilityValue(for: without, expanded: true),
            "expanded, 3 items, level 2")
    }

    // MARK: - Lifecycle verbs

    func testOpenFolderNoteOpensAndRefusalsAreTyped() async throws {
        let (state, _) = try openVault(
            named: "open", files: ["P/P.md", "Bare/other.md"])
        try publishFolder(state, path: "P")
        _ = try state.dispatchSidebarAction(id: SlateCommandID.openFolderNote)
        await state.noteLoadTask?.value
        XCTAssertEqual(state.selectedFilePath, "P/P.md")

        try publishFolder(state, path: "Bare")
        XCTAssertThrowsError(
            try state.dispatchSidebarAction(id: SlateCommandID.openFolderNote)
        ) { error in
            XCTAssertTrue(
                "\(error)".contains("no folder note"), "typed refusal: \(error)")
        }
        // Create refuses when the note already exists.
        try publishFolder(state, path: "P")
        XCTAssertThrowsError(
            try state.dispatchSidebarAction(id: SlateCommandID.createFolderNote)
        ) { error in
            XCTAssertTrue(
                "\(error)".contains("already has a folder note"), "\(error)")
        }
    }

    func testCreateFolderNoteCreatesOpensAndAnnounces() async throws {
        let (state, vault) = try openVault(named: "create", files: ["Bare/x.md"])
        try publishFolder(state, path: "Bare")
        _ = try state.dispatchSidebarAction(id: SlateCommandID.createFolderNote)
        // The create runs through the structural machinery; settle it.
        for _ in 0..<200 {
            if FileManager.default.fileExists(
                atPath: vault.appendingPathComponent("Bare/Bare.md").path)
            {
                break
            }
            try await Task.sleep(nanoseconds: 10_000_000)
        }
        XCTAssertTrue(
            FileManager.default.fileExists(
                atPath: vault.appendingPathComponent("Bare/Bare.md").path))
        for _ in 0..<200 {
            if state.lastMutationAnnouncement != nil { break }
            try await Task.sleep(nanoseconds: 10_000_000)
        }
        XCTAssertEqual(state.lastMutationAnnouncement, "Created note Bare.md.")
    }

    func testDeleteFolderNoteRoutesThroughTheExistingDeletePath() throws {
        let (state, _) = try openVault(named: "delete", files: ["P/P.md"])
        try publishFolder(state, path: "P")
        var requested: [(path: String, isDirectory: Bool)] = []
        state.sidebarActionDispatchOverrides.trashSingle = { selection in
            requested.append((selection.path, selection.isDirectory))
            return true
        }
        _ = try state.dispatchSidebarAction(id: SlateCommandID.deleteFolderNote)
        XCTAssertEqual(requested.count, 1)
        XCTAssertEqual(requested[0].path, "P/P.md")
        XCTAssertFalse(requested[0].isDirectory)
    }

    // MARK: - Compound rename routing

    func testFolderRenameRoutesThroughTheCompoundCoreOperation() async throws {
        let (state, vault) = try openVault(
            named: "compound",
            files: [
                "Projects/Projects.md", "Projects/sibling.md",
                "Loose/inner.md", "refs.md",
            ])
        try vault.appendingPathComponent("refs.md").deleteIfExists()
        try "See [[Projects/Projects]]\n".write(
            to: vault.appendingPathComponent("refs.md"),
            atomically: true, encoding: .utf8)
        _ = try XCTUnwrap(state.currentSession).scanInitial(
            cancel: CancelToken())

        let session = try XCTUnwrap(state.currentSession)
        let report = try await state.structuralRenameRunner(
            session, "Projects", true, "Work")
        let movedPairs: [(String, String)] = report.moved.map {
            ($0.oldPath, $0.newPath)
        }
        XCTAssertTrue(
            movedPairs.contains(where: { pair in
                pair.0 == "Projects/Projects.md" && pair.1 == "Work/Work.md"
            }),
            "one compound operation renames the note with the folder: \(movedPairs)")
        XCTAssertTrue(
            FileManager.default.fileExists(
                atPath: vault.appendingPathComponent("Work/Work.md").path))
        let refs = try String(
            contentsOf: vault.appendingPathComponent("refs.md"),
            encoding: .utf8)
        XCTAssertTrue(refs.contains("[[Work/Work]]"), refs)

        // A note-less folder still routes through the plain rename —
        // its files keep their own names.
        let plain = try await state.structuralRenameRunner(
            session, "Loose", true, "Free")
        let plainPairs: [(String, String)] = plain.moved.map {
            ($0.oldPath, $0.newPath)
        }
        XCTAssertTrue(
            plainPairs.contains(where: { pair in
                pair.0 == "Loose/inner.md" && pair.1 == "Free/inner.md"
            }),
            "note-less folders take the plain rename: \(plainPairs)")
        XCTAssertFalse(
            FileManager.default.fileExists(
                atPath: vault.appendingPathComponent("Free/Free.md").path),
            "no phantom folder note appears from a plain rename")
    }
}

extension URL {
    fileprivate func deleteIfExists() throws {
        if FileManager.default.fileExists(atPath: path) {
            try FileManager.default.removeItem(at: self)
        }
    }
}
