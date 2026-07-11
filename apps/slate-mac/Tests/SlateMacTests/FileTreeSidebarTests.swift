// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import SwiftUI
import XCTest

@testable import SlateMac

/// U2-4 (#462): the `FileTreeSidebar` / `FileTreeViewModel` — lazy per-level
/// fetching (the 10k budget), expand/collapse state machine, the
/// `treeInvalidation` refresh seam, keyboard →/← disclosure mapping, the AX
/// label/value builders, and the both-appearances render smoke (PresentationReady).
///
/// The model fetches through an injectable seam (`bindForTesting(fetcher:)`), so
/// these tests assert *which* levels are fetched without an FFI round-trip and
/// stand up a 10k-file synthetic vault cheaply.
@MainActor
final class FileTreeSidebarTests: XCTestCase {

    // MARK: - Fixture builders

    private func dir(
        _ id: Int64, _ path: String, dirCount: Int = 0, fileCount: Int = 0
    ) -> DirNodeSummary {
        DirNodeSummary(
            id: id, path: path, name: (path as NSString).lastPathComponent,
            childDirCount: UInt32(dirCount), childFileCount: UInt32(fileCount))
    }

    private func file(_ path: String, mtime: Int64 = 0) -> FileSummary {
        FileSummary(
            path: path, name: (path as NSString).lastPathComponent, mtimeMs: mtime,
            sizeBytes: 0, isMarkdown: true)
    }

    private func listing(dirs: [DirNodeSummary], files: [FileSummary]) -> DirListing {
        DirListing(
            dirs: dirs,
            files: FileSummaryPage(
                items: files, nextCursor: nil, totalFiltered: UInt64(files.count)))
    }

    /// A recording fetcher over a fixed `parentPath → listing` map. Records the
    /// ordered list of parentPaths requested so tests can assert laziness. An
    /// unknown parent returns an empty level (never throws) unless `throwOn`
    /// matches, which lets the error-path test drive `.failed`.
    private final class FetchSpy {
        var calls: [String] = []
        let table: [String: DirListing]
        let throwOn: String?

        init(_ table: [String: DirListing], throwOn: String? = nil) {
            self.table = table
            self.throwOn = throwOn
        }

        func fetch(_ parentPath: String) throws -> DirListing {
            calls.append(parentPath)
            if parentPath == throwOn {
                throw VaultError.Db(message: "boom")
            }
            return table[parentPath]
                ?? DirListing(
                    dirs: [], files: FileSummaryPage(items: [], nextCursor: nil, totalFiltered: 0))
        }
    }

    // MARK: - AX builders (spec: label/value builders unit-tested)

    func testFolderAccessibilityValueCollapsedPluralAtRoot() {
        let node = TreeNode(
            nodeID: .dir(1), path: "notes", name: "notes", depth: 0,
            kind: .directory(childDirCount: 2, childFileCount: 3))
        // Collapsed, 5 immediate items, depth 0 → level 1 (1-based).
        XCTAssertEqual(
            FileTreeSidebar.folderAccessibilityValue(for: node, expanded: false),
            "collapsed, 5 items, level 1")
    }

    func testFolderAccessibilityValueExpandedAndDeeperLevel() {
        let node = TreeNode(
            nodeID: .dir(2), path: "notes/sub", name: "sub", depth: 2,
            kind: .directory(childDirCount: 0, childFileCount: 4))
        XCTAssertEqual(
            FileTreeSidebar.folderAccessibilityValue(for: node, expanded: true),
            "expanded, 4 items, level 3")
    }

    func testFolderAccessibilityValueSingularItem() {
        let node = TreeNode(
            nodeID: .dir(3), path: "solo", name: "solo", depth: 0,
            kind: .directory(childDirCount: 0, childFileCount: 1))
        // Singular phrasing for exactly one item.
        XCTAssertEqual(
            FileTreeSidebar.folderAccessibilityValue(for: node, expanded: false),
            "collapsed, 1 item, level 1")
    }

    func testFolderAccessibilityValueEmptyFolder() {
        let node = TreeNode(
            nodeID: .dir(4), path: "empty", name: "empty", depth: 1,
            kind: .directory(childDirCount: 0, childFileCount: 0))
        XCTAssertEqual(
            FileTreeSidebar.folderAccessibilityValue(for: node, expanded: false),
            "collapsed, 0 items, level 2")
    }

    func testIndentWidthScalesWithDepthByTokensSpacingMd() {
        XCTAssertEqual(FileTreeSidebar.indentWidth(for: 0), 0)
        XCTAssertEqual(FileTreeSidebar.indentWidth(for: 1), Tokens.Spacing.md)
        XCTAssertEqual(FileTreeSidebar.indentWidth(for: 3), Tokens.Spacing.md * 3)
    }

    // MARK: - Root load + node building (dirs-then-files, depth)

    func testRootLoadsDirsThenFilesFromTheApiOrder() {
        let spy = FetchSpy([
            "": listing(
                dirs: [dir(1, "beta"), dir(2, "alpha")],  // API already sorted
                files: [file("readme.md"), file("todo.md")])
        ])
        let vm = FileTreeViewModel()
        vm.bindForTesting(fetcher: spy.fetch)

        // Root fetched exactly once, on bind.
        XCTAssertEqual(spy.calls, [""])
        // Dirs come before files; order preserved from the API (the view trusts
        // the API sort).
        XCTAssertEqual(
            vm.rootLevel.map(\.name), ["beta", "alpha", "readme.md", "todo.md"])
        XCTAssertEqual(vm.rootLevel.map(\.depth), [0, 0, 0, 0])
        XCTAssertTrue(vm.rootLevel[0].isDirectory)
        XCTAssertFalse(vm.rootLevel[2].isDirectory)
        XCTAssertEqual(vm.rootLevel[3].nodeID, .file(path: "todo.md"))
    }

    // MARK: - Lazy fetch (spec: expanding fetches ONLY that level)

    func testExpandingFetchesOnlyThatLevelNotSiblingsOrGrandchildren() {
        let spy = FetchSpy([
            "": listing(dirs: [dir(1, "a"), dir(2, "b")], files: []),
            "a": listing(dirs: [dir(3, "a/deep")], files: [file("a/one.md")]),
            "a/deep": listing(dirs: [], files: [file("a/deep/two.md")]),
            "b": listing(dirs: [], files: [file("b/three.md")]),
        ])
        let vm = FileTreeViewModel()
        vm.bindForTesting(fetcher: spy.fetch)
        XCTAssertEqual(spy.calls, [""])  // only root so far

        let a = try! XCTUnwrap(vm.rootLevel.first { $0.name == "a" })
        vm.expand(a)

        // Expanding "a" fetched ONLY "a" — not sibling "b", not grandchild
        // "a/deep". This is the 10k budget: nothing recursive, nothing eager.
        XCTAssertEqual(spy.calls, ["", "a"])
        XCTAssertNil(vm.children[.dir(2)])  // sibling "b" untouched
        XCTAssertNil(vm.children[.dir(3)])  // grandchild "a/deep" untouched
        XCTAssertEqual(vm.children[.dir(1)]?.map(\.name), ["deep", "one.md"])
        // Child level depth is parent depth + 1.
        XCTAssertEqual(vm.children[.dir(1)]?.map(\.depth), [1, 1])
    }

    func testReExpandUsesCacheAndDoesNotRefetch() {
        let spy = FetchSpy([
            "": listing(dirs: [dir(1, "a")], files: []),
            "a": listing(dirs: [], files: [file("a/one.md")]),
        ])
        let vm = FileTreeViewModel()
        vm.bindForTesting(fetcher: spy.fetch)
        let a = try! XCTUnwrap(vm.rootLevel.first { $0.name == "a" })

        vm.expand(a)
        XCTAssertEqual(spy.calls, ["", "a"])
        vm.collapse(a)
        vm.expand(a)  // cached — must NOT refetch
        XCTAssertEqual(spy.calls, ["", "a"])
        // Collapse retains the cached level.
        XCTAssertNotNil(vm.children[.dir(1)])
    }

    func testTenThousandFileVaultRendersRootFastAndFetchesRootOnly() {
        // A wide root: 10k files + 50 folders, none expanded. The tree should
        // materialize only the root level (the flatten walks expanded state
        // only), and fetch exactly once.
        let files = (0..<10_000).map { file(String(format: "note-%05d.md", $0)) }
        let dirs = (0..<50).map { dir(Int64($0) + 1, "folder-\($0)", fileCount: 200) }
        let spy = FetchSpy(["": listing(dirs: dirs, files: files)])
        let vm = FileTreeViewModel()

        let clock = ContinuousClock()
        let elapsed = clock.measure {
            vm.bindForTesting(fetcher: spy.fetch)
            _ = vm.visibleRows
        }

        XCTAssertEqual(spy.calls, [""])  // root only, nothing recursive
        // Only the root level is materialized (10k files + 50 dirs); no child
        // level was fetched, so `children` is empty.
        XCTAssertEqual(vm.visibleRows.count, 10_050)
        XCTAssertTrue(vm.children.isEmpty)
        // The spec's budget: root renders "fast" (< 1s in tests). Building the
        // node array + flattening 10k+ rows is microseconds; assert with
        // enormous headroom so this is a smoke guard, not a flaky perf gate.
        XCTAssertLessThan(elapsed, .seconds(1))
    }

    // MARK: - Flatten reflects expanded state (pre-order)

    func testVisibleRowsIsPreOrderOfExpandedSubtreeOnly() {
        let spy = FetchSpy([
            "": listing(dirs: [dir(1, "a"), dir(2, "b")], files: [file("root.md")]),
            "a": listing(dirs: [], files: [file("a/one.md"), file("a/two.md")]),
        ])
        let vm = FileTreeViewModel()
        vm.bindForTesting(fetcher: spy.fetch)
        let a = try! XCTUnwrap(vm.rootLevel.first { $0.name == "a" })

        // Collapsed: just the root level.
        XCTAssertEqual(vm.visibleRows.map(\.name), ["a", "b", "root.md"])

        // Expand "a": its children splice in immediately after it, before "b".
        vm.expand(a)
        XCTAssertEqual(
            vm.visibleRows.map(\.name), ["a", "one.md", "two.md", "b", "root.md"])
    }

    // MARK: - treeInvalidation seam (spec: drop level, refetch iff expanded)

    func testTreeInvalidationOfCollapsedLevelDropsCacheAndDefersRefetch() {
        let spy = FetchSpy([
            "": listing(dirs: [dir(1, "a")], files: []),
            "a": listing(dirs: [], files: [file("a/one.md")]),
        ])
        let vm = FileTreeViewModel()
        vm.bindForTesting(fetcher: spy.fetch)
        let a = try! XCTUnwrap(vm.rootLevel.first { $0.name == "a" })
        vm.expand(a)
        vm.collapse(a)
        XCTAssertEqual(spy.calls, ["", "a"])

        // Invalidate the (now-collapsed) "a": cache dropped, NOT refetched yet.
        vm.treeInvalidation(parent: .dir(1))
        XCTAssertNil(vm.children[.dir(1)])
        XCTAssertEqual(spy.calls, ["", "a"])  // no new fetch

        // Next expand refetches.
        vm.expand(a)
        XCTAssertEqual(spy.calls, ["", "a", "a"])
    }

    func testTreeInvalidationOfExpandedLevelRefetchesImmediately() {
        let spy = FetchSpy([
            "": listing(dirs: [dir(1, "a")], files: []),
            "a": listing(dirs: [], files: [file("a/one.md")]),
        ])
        let vm = FileTreeViewModel()
        vm.bindForTesting(fetcher: spy.fetch)
        let a = try! XCTUnwrap(vm.rootLevel.first { $0.name == "a" })
        vm.expand(a)
        XCTAssertEqual(spy.calls, ["", "a"])

        // Expanded level: invalidation refetches now (U2-5 mutation refresh).
        vm.treeInvalidation(parent: .dir(1))
        XCTAssertEqual(spy.calls, ["", "a", "a"])
        XCTAssertNotNil(vm.children[.dir(1)])
    }

    func testTreeInvalidationRootRefetchesRootAndExpandedChildren() {
        let spy = FetchSpy([
            "": listing(dirs: [dir(1, "a"), dir(2, "b")], files: []),
            "a": listing(dirs: [], files: [file("a/one.md")]),
            "b": listing(dirs: [], files: [file("b/two.md")]),
        ])
        let vm = FileTreeViewModel()
        vm.bindForTesting(fetcher: spy.fetch)
        let a = try! XCTUnwrap(vm.rootLevel.first { $0.name == "a" })
        vm.expand(a)  // "a" expanded; "b" left collapsed
        XCTAssertEqual(spy.calls, ["", "a"])

        // Root invalidation (a rescan): refetch root + the expanded child "a".
        // "b" is collapsed, so it is NOT refetched (its cache never existed).
        vm.treeInvalidation(parent: nil)
        XCTAssertEqual(spy.calls, ["", "a", "", "a"])
        XCTAssertNotNil(vm.children[.dir(1)])  // "a" refetched
        XCTAssertNil(vm.children[.dir(2)])  // "b" still lazy
    }

    // MARK: - Error + retry path

    func testChildFetchErrorRecordsFailedStateWithSpecificMessage() {
        let spy = FetchSpy(
            [
                "": listing(dirs: [dir(1, "a")], files: []),
            ], throwOn: "a")
        let vm = FileTreeViewModel()
        vm.bindForTesting(fetcher: spy.fetch)
        let a = try! XCTUnwrap(vm.rootLevel.first { $0.name == "a" })

        vm.expand(a)
        // Failed level is recorded (drives the inline error row + Retry); no
        // children cached.
        XCTAssertEqual(vm.fetchState[.dir(1)], .failed(message: "Couldn't load this folder: boom"))
        XCTAssertNil(vm.children[.dir(1)])
    }

    func testMessageForVaultErrorIsUserFacingNotDebugReflection() {
        // VaultError.errorDescription is a debug reflection; the tree fronts it
        // with plain prose.
        XCTAssertEqual(
            FileTreeViewModel.message(for: VaultError.Io(message: "disk gone")),
            "Couldn't load this folder: disk gone")
        XCTAssertEqual(
            FileTreeViewModel.message(for: VaultError.Cancelled),
            "Loading this folder was cancelled.")
    }

    // MARK: - Keyboard →/← mapping (spec: expand/collapse mapping tests)

    private func expandedTreeVM() -> (FileTreeViewModel, FetchSpy) {
        let spy = FetchSpy([
            "": listing(dirs: [dir(1, "a")], files: [file("root.md")]),
            "a": listing(dirs: [dir(2, "a/sub")], files: [file("a/one.md")]),
            "a/sub": listing(dirs: [], files: [file("a/sub/deep.md")]),
        ])
        let vm = FileTreeViewModel()
        vm.bindForTesting(fetcher: spy.fetch)
        return (vm, spy)
    }

    func testRightExpandsACollapsedFolder() {
        let (vm, _) = expandedTreeVM()
        XCTAssertEqual(vm.moveOutcome(for: .dir(1), right: true), .expand(.dir(1)))
    }

    func testRightOnExpandedFolderSelectsFirstChild() {
        let (vm, _) = expandedTreeVM()
        let a = try! XCTUnwrap(vm.node(for: .dir(1)))
        vm.expand(a)  // now expanded, children = [sub, one.md]
        XCTAssertEqual(vm.moveOutcome(for: .dir(1), right: true), .select(.dir(2)))
    }

    func testRightOnFileDoesNothing() {
        let (vm, _) = expandedTreeVM()
        XCTAssertEqual(vm.moveOutcome(for: .file(path: "root.md"), right: false), .none)
        XCTAssertEqual(vm.moveOutcome(for: .file(path: "root.md"), right: true), .none)
    }

    func testLeftCollapsesAnExpandedFolder() {
        let (vm, _) = expandedTreeVM()
        let a = try! XCTUnwrap(vm.node(for: .dir(1)))
        vm.expand(a)
        XCTAssertEqual(vm.moveOutcome(for: .dir(1), right: false), .collapse(.dir(1)))
    }

    func testLeftOnCollapsedChildSelectsParent() {
        let (vm, _) = expandedTreeVM()
        let a = try! XCTUnwrap(vm.node(for: .dir(1)))
        vm.expand(a)  // materialize "a/sub" as a collapsed child of "a"
        // Left on the collapsed child "a/sub" → move to parent "a".
        XCTAssertEqual(vm.moveOutcome(for: .dir(2), right: false), .select(.dir(1)))
    }

    func testLeftOnAFileSelectsItsParentFolder() {
        let (vm, _) = expandedTreeVM()
        let a = try! XCTUnwrap(vm.node(for: .dir(1)))
        vm.expand(a)  // "a" children include the file "a/one.md"
        XCTAssertEqual(
            vm.moveOutcome(for: .file(path: "a/one.md"), right: false), .select(.dir(1)))
    }

    func testLeftAtRootLevelDoesNothing() {
        let (vm, _) = expandedTreeVM()
        // A collapsed root-level folder has no parent → nothing to do.
        XCTAssertEqual(vm.moveOutcome(for: .dir(1), right: false), .none)
    }

    // MARK: - Vault binding lifecycle

    func testBindToNilClearsTheTree() {
        let spy = FetchSpy(["": listing(dirs: [dir(1, "a")], files: [])])
        let vm = FileTreeViewModel()
        vm.bindForTesting(fetcher: spy.fetch)
        XCTAssertFalse(vm.rootLevel.isEmpty)

        vm.bind(to: nil)
        XCTAssertTrue(vm.rootLevel.isEmpty)
        XCTAssertTrue(vm.children.isEmpty)
        XCTAssertTrue(vm.expanded.isEmpty)
    }

    // MARK: - PresentationReady (spec: appearance snapshots, both appearances)

    /// A sidebar rendered against a small real vault, in both appearances.
    // MARK: - Delete-command modifier gate (spec §U2-5: the chord is ⌘⌫)

    /// A bare ⌫ keyDown must NOT delete — Finder ignores the unmodified
    /// key, and the tree's highlight can sit on a row selected long ago.
    func testBareDeleteKeyIsRejected() {
        let bare = Self.deleteKeyEvent(modifiers: [])
        XCTAssertFalse(FileTreeSidebar.deleteCommandAllowed(event: bare))
    }

    /// ⌘⌫ — the documented Move-to-Trash chord — passes the gate.
    func testCommandDeleteKeyIsAllowed() {
        let cmd = Self.deleteKeyEvent(modifiers: [.command])
        XCTAssertTrue(FileTreeSidebar.deleteCommandAllowed(event: cmd))
    }

    /// Non-key deliveries (VoiceOver's AX delete action, a menu `delete:`)
    /// have no matching keyDown — they're deliberate and must pass.
    func testNonKeyDeliveryIsAllowed() {
        XCTAssertTrue(FileTreeSidebar.deleteCommandAllowed(event: nil))
        let keyUp = NSEvent.keyEvent(
            with: .keyUp, location: .zero, modifierFlags: [], timestamp: 0,
            windowNumber: 0, context: nil, characters: "\u{7F}",
            charactersIgnoringModifiers: "\u{7F}", isARepeat: false, keyCode: 51)
        XCTAssertTrue(FileTreeSidebar.deleteCommandAllowed(event: keyUp))
    }

    private static func deleteKeyEvent(modifiers: NSEvent.ModifierFlags) -> NSEvent? {
        NSEvent.keyEvent(
            with: .keyDown, location: .zero, modifierFlags: modifiers,
            timestamp: 0, windowNumber: 0, context: nil, characters: "\u{7F}",
            charactersIgnoringModifiers: "\u{7F}", isARepeat: false, keyCode: 51)
    }

    // MARK: - List-level key-interception gate (red-team F1 on the HIG audit)

    /// `.focused` on the List has focus-WITHIN semantics: it stays true
    /// while the inline RenameField is first responder, and a List-level
    /// `.onKeyPress` sees keys before the field editor. The gate must
    /// therefore be OFF during rename — otherwise Space toggles the
    /// folder instead of typing, Return can't commit, and ⌘⌫ trashes the
    /// node under rename.
    func testTreeKeyInterceptionInactiveDuringRename() {
        XCTAssertFalse(
            FileTreeSidebar.treeKeyInterceptionActive(
                fileTreeFocused: true, isRenaming: true))
    }

    func testTreeKeyInterceptionActiveWithTreeFocusAndNoRename() {
        XCTAssertTrue(
            FileTreeSidebar.treeKeyInterceptionActive(
                fileTreeFocused: true, isRenaming: false))
    }

    func testTreeKeyInterceptionInactiveWithoutTreeFocus() {
        XCTAssertFalse(
            FileTreeSidebar.treeKeyInterceptionActive(
                fileTreeFocused: false, isRenaming: false))
        XCTAssertFalse(
            FileTreeSidebar.treeKeyInterceptionActive(
                fileTreeFocused: false, isRenaming: true))
    }

    /// Uses a live session (not the fetch spy) so the *view* — not just the VM —
    /// is exercised end-to-end through the render.
    func testSidebarRendersInBothAppearances() async throws {
        let tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("file-tree-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: tempDir, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: tempDir) }
        let vault = tempDir.appendingPathComponent("vault")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        let sub = vault.appendingPathComponent("notes")
        try FileManager.default.createDirectory(at: sub, withIntermediateDirectories: true)
        try "# a\n".write(to: vault.appendingPathComponent("a.md"), atomically: true, encoding: .utf8)
        try "# b\n".write(to: sub.appendingPathComponent("b.md"), atomically: true, encoding: .utf8)

        let state = AppState(
            recentsStore: RecentVaultsStore(
                fileURL: tempDir.appendingPathComponent("recents.json")),
            externalOpener: { _ in true })
        state.openVault(at: vault)
        await state.scanTask?.value

        PresentationReady.assertRendersInBothAppearances(
            FileTreeSidebar().environmentObject(state))
    }

    /// The tree's text-on-surface pairings ride the token registry — re-assert
    /// the DoD §D floor here so a tree-specific token change can't slip below it.
    func testTreeContrastPairings() {
        PresentationReady.assertContrastFloor([
            ("row title on surface", .tokenTextPrimary, .tokenSurface),
            ("row subtitle on surface", .tokenTextSecondary, .tokenSurface),
            ("error message on surface", .tokenDestructiveText, .tokenSurface),
        ])
    }
}
