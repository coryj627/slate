// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import Combine
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
            childDirCount: UInt32(dirCount), childFileCount: UInt32(fileCount), hasFolderNote: false)
    }

    private func file(_ path: String, mtime: Int64 = 0) -> FileSummary {
        FileSummary(
            path: path, name: (path as NSString).lastPathComponent, mtimeMs: mtime,
            sizeBytes: 0, isMarkdown: true, displayName: nil, createdDate: nil,
            createdMs: nil, wordCount: nil, preview: nil, taskTotal: 0, taskOpen: 0)
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

    private final class PageProbe: @unchecked Sendable {
        private let lock = NSLock()
        private var storedPages: [Int] = []

        func record(_ page: Int) {
            lock.lock()
            storedPages.append(page)
            lock.unlock()
        }

        var pages: [Int] {
            lock.lock()
            defer { lock.unlock() }
            return storedPages
        }
    }

    // MARK: - AX builders (spec: label/value builders unit-tested)

    func testFolderAccessibilityValueCollapsedPluralAtRoot() {
        let node = TreeNode(
            nodeID: .dir(1), path: "notes", name: "notes", depth: 0,
            kind: .directory(childDirCount: 2, childFileCount: 3, hasFolderNote: false))
        // Collapsed, 5 immediate items, depth 0 → level 1 (1-based).
        XCTAssertEqual(
            FileTreeSidebar.folderAccessibilityValue(for: node, expanded: false),
            "collapsed, 5 items, level 1")
    }

    func testFolderAccessibilityValueExpandedAndDeeperLevel() {
        let node = TreeNode(
            nodeID: .dir(2), path: "notes/sub", name: "sub", depth: 2,
            kind: .directory(childDirCount: 0, childFileCount: 4, hasFolderNote: false))
        XCTAssertEqual(
            FileTreeSidebar.folderAccessibilityValue(for: node, expanded: true),
            "expanded, 4 items, level 3")
    }

    func testFolderAccessibilityValueSingularItem() {
        let node = TreeNode(
            nodeID: .dir(3), path: "solo", name: "solo", depth: 0,
            kind: .directory(childDirCount: 0, childFileCount: 1, hasFolderNote: false))
        // Singular phrasing for exactly one item.
        XCTAssertEqual(
            FileTreeSidebar.folderAccessibilityValue(for: node, expanded: false),
            "collapsed, 1 item, level 1")
    }

    func testFolderAccessibilityValueEmptyFolder() {
        let node = TreeNode(
            nodeID: .dir(4), path: "empty", name: "empty", depth: 1,
            kind: .directory(childDirCount: 0, childFileCount: 0, hasFolderNote: false))
        XCTAssertEqual(
            FileTreeSidebar.folderAccessibilityValue(for: node, expanded: false),
            "collapsed, 0 items, level 2")
    }

    func testIndentWidthScalesWithDepthByTokensSpacingMd() {
        XCTAssertEqual(FileTreeSidebar.indentWidth(for: 0), 0)
        XCTAssertEqual(FileTreeSidebar.indentWidth(for: 1), Tokens.Spacing.md)
        XCTAssertEqual(FileTreeSidebar.indentWidth(for: 3), Tokens.Spacing.md * 3)
    }

    func testSidebarRecoveryActionMeetsTheMacHitTargetFloor() {
        XCTAssertGreaterThanOrEqual(FileTreeSidebar.recoveryActionMinimumHeight, 20)
    }

    func testRelativeDatesUseOneBoundedSidebarRefreshInterval() {
        XCTAssertEqual(FileTreeSidebar.relativeDateRefreshInterval, .seconds(60))
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

    func testFileNodeRetainsTheFullSummaryWithoutASecondaryLookup() throws {
        let rich = FileSummary(
            path: "Journal/review.md", name: "review.md", mtimeMs: 123,
            sizeBytes: 456, isMarkdown: true, displayName: "Weekly review",
            createdDate: "2026-07-14", createdMs: 789, wordCount: 1_240,
            preview: "A useful preview", taskTotal: 5, taskOpen: 3)

        let node = try XCTUnwrap(
            FileTreeViewModel.nodes(
                from: listing(dirs: [], files: [rich]), depth: 2
            ).first)

        guard case let .file(fileState) = node.kind else {
            return XCTFail("expected a file node")
        }
        XCTAssertEqual(fileState.summary, rich)
        XCTAssertEqual(node.name, "review.md", "rename and file operations keep using filename")
        XCTAssertEqual(node.path, "Journal/review.md")
        XCTAssertEqual(node.depth, 2)
    }

    func testTargetedSummaryReplacementUpdatesOnlyTheCachedRowWithoutRefetching() throws {
        let original = file("notes/target.md", mtime: 1)
        let spy = FetchSpy([
            "": listing(dirs: [dir(1, "notes", fileCount: 1)], files: []),
            "notes": listing(dirs: [], files: [original]),
        ])
        let vm = FileTreeViewModel()
        vm.bindForTesting(fetcher: spy.fetch)
        vm.expand(try XCTUnwrap(vm.rootLevel.first))
        XCTAssertEqual(spy.calls, ["", "notes"])

        let refreshed = FileSummary(
            path: original.path,
            name: original.name,
            mtimeMs: 2,
            sizeBytes: 99,
            isMarkdown: true,
            displayName: "Fresh title",
            createdDate: "2026-07-14",
            createdMs: nil,
            wordCount: 42,
            preview: "Fresh preview",
            taskTotal: 3,
            taskOpen: 1)

        XCTAssertTrue(vm.replaceFileSummary(refreshed))
        XCTAssertEqual(spy.calls, ["", "notes"], "targeted refresh must not fetch siblings")
        let node = try XCTUnwrap(vm.node(for: .file(path: refreshed.path)))
        guard case let .file(fileState) = node.kind else {
            return XCTFail("expected refreshed file node")
        }
        XCTAssertEqual(fileState.summary, refreshed)
        XCTAssertEqual(node.depth, 1)
    }

    func testSummaryBurstReplacesEveryCachedPathWithoutRefetching() throws {
        let first = file("notes/a.md", mtime: 1)
        let second = file("notes/b.md", mtime: 1)
        let spy = FetchSpy([
            "": listing(dirs: [dir(1, "notes", fileCount: 2)], files: []),
            "notes": listing(dirs: [], files: [first, second]),
        ])
        let vm = FileTreeViewModel()
        vm.bindForTesting(fetcher: spy.fetch)
        vm.expand(try XCTUnwrap(vm.rootLevel.first))

        let refreshedFirst = file("notes/a.md", mtime: 2)
        let refreshedSecond = file("notes/b.md", mtime: 3)
        XCTAssertEqual(vm.replaceFileSummaries([refreshedFirst, refreshedSecond]), 2)
        XCTAssertEqual(spy.calls, ["", "notes"])
        XCTAssertEqual(vm.fileSummary(forPath: refreshedFirst.path), refreshedFirst)
        XCTAssertEqual(vm.fileSummary(forPath: refreshedSecond.path), refreshedSecond)
    }

    func testTargetedSummaryReplacementIsOneLookupAtFiftyThousandRows() throws {
        let files = (0..<50_000).map { file(String(format: "note-%05d.md", $0)) }
        let spy = FetchSpy(["": listing(dirs: [], files: files)])
        let vm = FileTreeViewModel()
        vm.bindForTesting(fetcher: spy.fetch)
        let refreshed = file("note-49999.md", mtime: 2)
        var structuralInvalidations = 0
        let observation = vm.objectWillChange.sink {
            structuralInvalidations += 1
        }

        XCTAssertTrue(vm.replaceFileSummary(refreshed))
        XCTAssertEqual(
            vm.summaryReplacementLookupCountForTesting,
            1,
            "one changed file must not scan or map the 50k-row root")
        XCTAssertEqual(
            vm.levelReorganizeCountForTesting,
            0,
            "mtime is not an active key under the default name sort")
        XCTAssertEqual(vm.liveReorganizationRowVisitCountForTesting, 0)
        XCTAssertEqual(
            structuralInvalidations,
            0,
            "metadata-only refresh must invalidate its keyed row, not the structural tree")
        XCTAssertEqual(vm.fileSummary(forPath: refreshed.path), refreshed)
        XCTAssertEqual(spy.calls, [""])
        withExtendedLifetime(observation) {}
    }

    func testSummaryIndexDropsOldVaultLocationsOnRebind() {
        let vm = FileTreeViewModel()
        vm.bindForTesting { _ in self.listing(dirs: [], files: [self.file("a.md")]) }
        vm.bindForTesting { _ in self.listing(dirs: [], files: [self.file("b.md")]) }

        XCTAssertFalse(vm.replaceFileSummary(file("a.md", mtime: 2)))
        XCTAssertTrue(vm.replaceFileSummary(file("b.md", mtime: 2)))
        XCTAssertEqual(vm.fileSummary(forPath: "b.md"), file("b.md", mtime: 2))
    }

    func testSummaryIndexDropsCollapsedInvalidatedChildUntilRefetch() throws {
        var childFiles = [file("notes/old.md")]
        let vm = FileTreeViewModel()
        vm.bindForTesting { parent in
            parent.isEmpty
                ? self.listing(dirs: [self.dir(1, "notes", fileCount: 1)], files: [])
                : self.listing(dirs: [], files: childFiles)
        }
        let notes = try XCTUnwrap(vm.rootLevel.first)
        vm.expand(notes)
        vm.collapse(notes)
        childFiles = [file("notes/new.md")]
        vm.treeInvalidation(parent: .dir(1))

        XCTAssertFalse(vm.replaceFileSummary(file("notes/old.md", mtime: 2)))
        XCTAssertFalse(vm.replaceFileSummary(file("notes/new.md", mtime: 2)))
        vm.expand(notes)
        XCTAssertTrue(vm.replaceFileSummary(file("notes/new.md", mtime: 2)))
    }

    func testSummaryIndexRebuildsOffsetsAfterExpandedChildRefetchReordersRows() throws {
        var childFiles = [file("notes/a.md"), file("notes/b.md")]
        let vm = FileTreeViewModel()
        vm.bindForTesting { parent in
            parent.isEmpty
                ? self.listing(dirs: [self.dir(1, "notes", fileCount: 2)], files: [])
                : self.listing(dirs: [], files: childFiles)
        }
        vm.expand(try XCTUnwrap(vm.rootLevel.first))
        childFiles = [file("notes/b.md"), file("notes/a.md")]
        vm.treeInvalidation(parent: .dir(1))
        let refreshed = file("notes/a.md", mtime: 2)

        XCTAssertTrue(vm.replaceFileSummary(refreshed))
        XCTAssertEqual(vm.fileSummary(forPath: refreshed.path), refreshed)
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

    func testFL05TargetedRootInvalidationPreservesUnaffectedExpandedCache() throws {
        let spy = FetchSpy([
            "": listing(
                dirs: [dir(1, "notes", fileCount: 1)],
                files: [file("root.md")]),
            "notes": listing(dirs: [], files: [file("notes/keep.md")]),
        ])
        let vm = FileTreeViewModel()
        vm.bindForTesting(fetcher: spy.fetch)
        vm.expand(try XCTUnwrap(vm.rootLevel.first))
        XCTAssertEqual(spy.calls, ["", "notes"])

        vm.rootLevelInvalidation()

        XCTAssertEqual(spy.calls, ["", "notes", ""])
        XCTAssertTrue(vm.expanded.contains(.dir(1)))
        XCTAssertNotNil(vm.node(for: .file(path: "notes/keep.md")))
        vm.collapse(try XCTUnwrap(vm.node(for: .dir(1))))
        vm.expand(try XCTUnwrap(vm.node(for: .dir(1))))
        XCTAssertEqual(
            spy.calls,
            ["", "notes", ""],
            "a targeted root refresh must retain the unaffected child cache")

        vm.authoritativeTreeInvalidation()
        XCTAssertEqual(spy.calls.filter { $0 == "" }.count, 3)
        XCTAssertGreaterThan(
            spy.calls.filter { $0 == "notes" }.count,
            1,
            "an authoritative scan must refetch expanded descendants")
    }

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

    func testChildFetchErrorRecordsFailedStateWithPrivacySafeMessage() {
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
        XCTAssertEqual(
            vm.fetchState[.dir(1)],
            .failed(message: "Couldn't load this folder."))
        XCTAssertNil(vm.children[.dir(1)])
    }

    func testMessageForVaultErrorDoesNotExposeBackendPayload() {
        XCTAssertEqual(
            FileTreeViewModel.message(for: VaultError.Io(message: "disk gone")),
            "Couldn't load this folder.")
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
    // MARK: - Type-select modifier gate (red-team: shift/caps must pass)

    func testTypeSelectAllowsShiftAndCapsLockOnly() {
        XCTAssertTrue(FileTreeSidebar.typeSelectModifiersAllowed([]))
        XCTAssertTrue(FileTreeSidebar.typeSelectModifiersAllowed([.shift]))
        XCTAssertTrue(FileTreeSidebar.typeSelectModifiersAllowed([.capsLock]))
        XCTAssertTrue(FileTreeSidebar.typeSelectModifiersAllowed([.shift, .capsLock]))
        XCTAssertFalse(FileTreeSidebar.typeSelectModifiersAllowed([.command]))
        XCTAssertFalse(FileTreeSidebar.typeSelectModifiersAllowed([.option]))
        XCTAssertFalse(FileTreeSidebar.typeSelectModifiersAllowed([.control]))
        XCTAssertFalse(FileTreeSidebar.typeSelectModifiersAllowed([.shift, .command]))
    }

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

    /// Caps Lock is typing state, not an added chord. Every other standard
    /// modifier changes the command and must fall through to its owner in both
    /// SwiftUI's key-press interceptor and AppKit's delete-command delivery.
    func testCommandDeleteRequiresExactChordIgnoringCapsLock() {
        XCTAssertTrue(
            FileTreeSidebar.deleteKeyModifiersAllowed([.command]))
        XCTAssertTrue(
            FileTreeSidebar.deleteKeyModifiersAllowed([.command, .capsLock]))

        for extra: EventModifiers in [.shift, .option, .control] {
            XCTAssertFalse(
                FileTreeSidebar.deleteKeyModifiersAllowed([.command, extra]),
                "SwiftUI must reject Command plus \(extra)")
        }

        XCTAssertTrue(
            FileTreeSidebar.deleteCommandAllowed(
                event: Self.deleteKeyEvent(modifiers: [.command, .capsLock])))
        for extra: NSEvent.ModifierFlags in [.shift, .option, .control] {
            XCTAssertFalse(
                FileTreeSidebar.deleteCommandAllowed(
                    event: Self.deleteKeyEvent(modifiers: [.command, extra])),
                "AppKit must reject Command plus \(extra)")
        }
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

    func testBoundedDirectoryPageBridgePreservesDirsFirstContinuation() throws {
        let tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("directory-page-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: tempDir, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: tempDir) }
        for name in ["Zulu", "alpha", "beta"] {
            try FileManager.default.createDirectory(
                at: tempDir.appendingPathComponent(name),
                withIntermediateDirectories: true)
        }
        try "# note\n".write(
            to: tempDir.appendingPathComponent("note.md"),
            atomically: true,
            encoding: .utf8)

        let session = try VaultSession.openFilesystem(rootPath: tempDir.path)
        try session.scanInitial(cancel: CancelToken())
        let first = try session.listDirChildrenPage(
            parentPath: "",
            paging: Paging(cursor: nil, limit: 2),
            cancel: CancelToken())
        let bridged = first.asTreeListing()
        XCTAssertEqual(bridged.dirs.map(\.name), ["alpha", "beta"])
        XCTAssertTrue(bridged.files.items.isEmpty)
        XCTAssertNotNil(bridged.files.nextCursor)

        let second = try session.listDirChildrenPage(
            parentPath: "",
            paging: Paging(cursor: first.nextCursor, limit: 2),
            cancel: CancelToken())
        XCTAssertEqual(second.dirs.map(\.name), ["Zulu"])
        XCTAssertEqual(second.files.map(\.name), ["note.md"])
        XCTAssertFalse(second.truncated)
        XCTAssertNil(second.nextCursor)
    }

    func testTreeDrainPublishesContinuationDirectoriesAndRestoredExpansion() async throws {
        let rootFirst = DirListing(
            dirs: [dir(1, "alpha")],
            files: FileSummaryPage(
                items: [], nextCursor: "root-2", totalFiltered: 1))
        let rootSecond = DirListing(
            dirs: [dir(2, "beta", fileCount: 1)],
            files: FileSummaryPage(
                items: [file("root.md")], nextCursor: nil, totalFiltered: 1))
        let betaChildren = listing(
            dirs: [], files: [file("beta/inside.md")])
        let vm = FileTreeViewModel()
        vm.bindForTesting(
            pagedFetcher: { parent, cursor in
                switch (parent, cursor) {
                case ("", nil): return rootFirst
                case ("", "root-2"): return rootSecond
                case ("beta", nil): return betaChildren
                default:
                    XCTFail(
                        "unexpected page \(parent) / \(String(describing: cursor))")
                    return self.listing(dirs: [], files: [])
                }
            },
            restoringExpandedDirPaths: ["beta"])

        await vm.levelDrainTasksForTesting[FileTreeViewModel.rootFetchKey]?.value
        XCTAssertEqual(vm.rootLevel.map(\.path), ["alpha", "beta", "root.md"])
        let beta = try XCTUnwrap(vm.rootLevel.first { $0.path == "beta" })
        XCTAssertTrue(vm.expanded.contains(beta.nodeID))
        XCTAssertEqual(vm.children[beta.nodeID]?.map(\.path), ["beta/inside.md"])
    }

    func testTreeDrainStopsAtExactSafetyCapAndPublishesIncompleteReason() async {
        let probe = PageProbe()
        let vm = FileTreeViewModel()
        vm.bindForTesting(
            pagedFetcher: { _, cursor in
                let page = Int(cursor ?? "0")!
                probe.record(page)
                let start = page * 5_000
                let dirs = (start..<(start + 5_000)).map {
                    let path = String(format: "folder-%05d", $0)
                    return DirNodeSummary(
                        id: Int64($0 + 1), path: path, name: path,
                        childDirCount: 0, childFileCount: 0,
                        hasFolderNote: false)
                }
                return DirListing(
                    dirs: dirs,
                    files: FileSummaryPage(
                        items: [],
                        nextCursor: page < 10 ? String(page + 1) : nil,
                        totalFiltered: 0))
            })

        await vm.levelDrainTasksForTesting[FileTreeViewModel.rootFetchKey]?.value
        XCTAssertEqual(vm.rootLevel.count, FileTreeViewModel.levelTotalSafetyCap)
        XCTAssertEqual(probe.pages, Array(0...9), "page beyond the cap must not be fetched")
        XCTAssertEqual(
            vm.incompleteLevelMessage(for: FileTreeViewModel.rootFetchKey),
            FileTreeViewModel.levelSafetyCapMessage)
    }

    func testRebindingCancelsTheNativeContinuationToken() async {
        let entered = expectation(description: "continuation entered")
        let cancelled = expectation(description: "native token cancelled")
        let vm = FileTreeViewModel()
        vm.bindForTesting(
            cancellablePagedFetcher: { _, cursor, cancel in
                guard cursor != nil else {
                    return DirListing(
                        dirs: [self.dir(1, "alpha")],
                        files: FileSummaryPage(
                            items: [], nextCursor: "next", totalFiltered: 0))
                }
                entered.fulfill()
                while !cancel.isCancelled() {
                    Thread.sleep(forTimeInterval: 0.001)
                }
                cancelled.fulfill()
                throw CancellationError()
            })

        await fulfillment(of: [entered], timeout: 10)
        vm.bindForTesting { _ in self.listing(dirs: [], files: []) }
        await fulfillment(of: [cancelled], timeout: 10)
        XCTAssertTrue(vm.rootLevel.isEmpty)
    }

    /// The tree's text-on-surface pairings ride the token registry — re-assert
    /// the DoD §D floor here so a tree-specific token change can't slip below it.
    func testTreeContrastPairings() {
        PresentationReady.assertContrastFloor([
            ("row title on surface", .tokenTextPrimary, .tokenSurface),
            ("row subtitle on surface", .tokenTextSecondary, .tokenSurface),
            (
                "active selected row text on native selection",
                SidebarSelectionColors.text(active: true),
                SidebarSelectionColors.background(active: true)
            ),
            (
                "inactive selected row text on native selection",
                SidebarSelectionColors.text(active: false),
                SidebarSelectionColors.background(active: false)
            ),
            (
                "unselected folder drop indicator on surface",
                SidebarSelectionColors.dropIndicator(selected: false, active: true),
                .tokenSurface
            ),
            ("error message on surface", .tokenDestructiveText, .tokenSurface),
        ])
    }

    func testSelectionEmphasisRequiresTreeFocusAndAKeyWindow() {
        XCTAssertTrue(
            FileTreeSidebar.selectionIsActive(
                treeFocused: true,
                controlActiveState: .key))
        XCTAssertFalse(
            FileTreeSidebar.selectionIsActive(
                treeFocused: true,
                controlActiveState: .active),
            "a focused control in a non-key window uses the inactive carrier")
        XCTAssertFalse(
            FileTreeSidebar.selectionIsActive(
                treeFocused: true,
                controlActiveState: .inactive))
        XCTAssertFalse(
            FileTreeSidebar.selectionIsActive(
                treeFocused: false,
                controlActiveState: .key))
    }

    func testSelectedFolderContentRendersWithActiveAndInactiveSystemPalettes() {
        let node = TreeNode(
            nodeID: .dir(7), path: "Reference", name: "Reference", depth: 1,
            kind: .directory(childDirCount: 2, childFileCount: 3, hasFolderNote: false))

        PresentationReady.assertRendersInBothAppearances(
            SidebarFolderRowContent(
                node: node,
                isExpanded: false,
                isSelected: true,
                selectionIsActive: true,
                isDropTargeted: true))
        PresentationReady.assertRendersInBothAppearances(
            SidebarFolderRowContent(
                node: node,
                isExpanded: true,
                isSelected: true,
                selectionIsActive: false,
                isDropTargeted: true))
    }

    // MARK: - Type-select matcher (#850, pure — the moveOutcome pattern)

    /// A single-character buffer advances: the scan starts AFTER the current
    /// selection, so repeated presses of one letter cycle through its
    /// matches — and wrap back around past the end.
    func testTypeSelectSingleCharAdvancesFromSelectionAndWraps() {
        let names = ["alpha", "beta", "banana", "gamma"]
        // From "beta" (index 1), "b" advances to "banana" (index 2)…
        XCTAssertEqual(
            FileTreeSidebar.typeSelectIndex(names: names, prefix: "b", selectedIndex: 1), 2)
        // …and from "banana" (index 2), "b" wraps back to "beta" (index 1).
        XCTAssertEqual(
            FileTreeSidebar.typeSelectIndex(names: names, prefix: "b", selectedIndex: 2), 1)
        // No selection: the scan starts at the top.
        XCTAssertEqual(
            FileTreeSidebar.typeSelectIndex(names: names, prefix: "b", selectedIndex: nil), 1)
    }

    /// Case- and diacritic-insensitive: "re" finds "README.md", "e" finds
    /// "Éclair.md".
    func testTypeSelectIsCaseAndDiacriticInsensitive() {
        let names = ["Éclair.md", "README.md", "zoo.md"]
        XCTAssertEqual(
            FileTreeSidebar.typeSelectIndex(names: names, prefix: "re", selectedIndex: nil), 1)
        XCTAssertEqual(
            FileTreeSidebar.typeSelectIndex(names: names, prefix: "e", selectedIndex: nil), 0)
        // The fold works in both directions: an accented prefix matches an
        // unaccented name.
        XCTAssertEqual(
            FileTreeSidebar.typeSelectIndex(names: ["reveal.md"], prefix: "ré", selectedIndex: nil),
            0)
    }

    /// A refining (multi-character) buffer stays on the current row while it
    /// still matches — "r" landed on "readme.md"; growing to "re" must not
    /// jump away.
    func testTypeSelectRefiningBufferStaysOnMatchingSelection() {
        let names = ["notes", "readme.md", "recipes.md"]
        XCTAssertEqual(
            FileTreeSidebar.typeSelectIndex(names: names, prefix: "re", selectedIndex: 1), 1)
    }

    /// A refining buffer that the current row no longer matches scans onward
    /// (wrapping): from "rust.md", "re" moves to "readme.md".
    func testTypeSelectRefiningBufferMovesWhenSelectionStopsMatching() {
        let names = ["readme.md", "rust.md", "zoo.md"]
        XCTAssertEqual(
            FileTreeSidebar.typeSelectIndex(names: names, prefix: "re", selectedIndex: 1), 0)
    }

    /// Folder and file names participate alike — `names` is the flattened
    /// visible-row list, so a folder is just another candidate.
    func testTypeSelectMatchesFolderAndFileNamesAlike() {
        let names = ["Projects", "alpha.md", "projects-notes.md"]
        // The folder wins first…
        XCTAssertEqual(
            FileTreeSidebar.typeSelectIndex(names: names, prefix: "pro", selectedIndex: nil), 0)
        // …and single-char cycling from the folder reaches the file.
        XCTAssertEqual(
            FileTreeSidebar.typeSelectIndex(names: names, prefix: "p", selectedIndex: 0), 2)
        // A refining buffer the folder still matches stays put (Finder).
        XCTAssertEqual(
            FileTreeSidebar.typeSelectIndex(names: names, prefix: "pro", selectedIndex: 0), 0)
    }

    /// No match / empty inputs return nil (the keystroke is still consumed by
    /// the view — Finder-style — but the selection stays put).
    func testTypeSelectNoMatchAndEmptyGuards() {
        XCTAssertNil(
            FileTreeSidebar.typeSelectIndex(
                names: ["alpha", "beta"], prefix: "q", selectedIndex: 0))
        XCTAssertNil(FileTreeSidebar.typeSelectIndex(names: [], prefix: "a", selectedIndex: nil))
        XCTAssertNil(
            FileTreeSidebar.typeSelectIndex(names: ["alpha"], prefix: "", selectedIndex: nil))
        // An out-of-bounds selection index (stale after a level refetch)
        // degrades to a top-of-list scan, never a crash.
        XCTAssertEqual(
            FileTreeSidebar.typeSelectIndex(names: ["alpha"], prefix: "a", selectedIndex: 99), 0)
    }

    /// F2 is delivered as the AppKit function-key scalar (U+F705) —
    /// regression-lock the constant the `.onKeyPress(keys:)` gate matches on.
    func testF2KeyEquivalentIsTheAppKitFunctionKeyScalar() {
        XCTAssertEqual(
            FileTreeSidebar.f2Key.character, Character(UnicodeScalar(0xF705)!))
    }

    /// Space must never reach type-select — it belongs to folder disclosure
    /// (shipped semantics, #850 keeps them unchanged).
    func testTypeSelectCharactersExcludeWhitespaceAndControls() {
        XCTAssertFalse(FileTreeSidebar.typeSelectCharacters.contains(" "))
        XCTAssertFalse(FileTreeSidebar.typeSelectCharacters.contains("\n"))
        XCTAssertFalse(FileTreeSidebar.typeSelectCharacters.contains("\u{7F}"))
        XCTAssertTrue(FileTreeSidebar.typeSelectCharacters.contains("a"))
        XCTAssertTrue(FileTreeSidebar.typeSelectCharacters.contains("7"))
        XCTAssertTrue(FileTreeSidebar.typeSelectCharacters.contains("-"))
    }

    // MARK: - Spring-load re-collapse set (#851, pure)

    /// A cancelled/exited drag (nil destination) re-collapses every folder
    /// the drag spring-opened.
    func testSpringRecollapseAllOnCancelledDrag() {
        XCTAssertEqual(
            FileTreeSidebar.springFoldersToRecollapse(
                openedPaths: ["a", "a/b", "c"], destinationFolder: nil),
            ["a", "a/b", "c"])
    }

    /// A drop into (or beneath) a spring-opened folder keeps that folder's
    /// chain open — the user is about to look inside it.
    func testSpringKeepsDestinationChainOpen() {
        // Drop directly INTO the spring-opened folder.
        XCTAssertEqual(
            FileTreeSidebar.springFoldersToRecollapse(
                openedPaths: ["a", "c"], destinationFolder: "a"),
            ["c"])
        // Drop deeper inside it: both ancestors stay open.
        XCTAssertEqual(
            FileTreeSidebar.springFoldersToRecollapse(
                openedPaths: ["a", "a/b", "c"], destinationFolder: "a/b"),
            ["c"])
    }

    /// A drop at the vault root ("" — the tree background) landed in none of
    /// the spring-opened folders: all of them re-collapse.
    func testSpringRootDropRecollapsesEverything() {
        XCTAssertEqual(
            FileTreeSidebar.springFoldersToRecollapse(
                openedPaths: ["a", "a/b"], destinationFolder: ""),
            ["a", "a/b"])
    }

    /// Path-boundary discipline: a destination "Projects/x" is NOT within a
    /// spring-opened folder "Pro" — prefix matching is per component, not
    /// per character.
    func testSpringRecollapseRespectsPathComponentBoundaries() {
        XCTAssertEqual(
            FileTreeSidebar.springFoldersToRecollapse(
                openedPaths: ["Pro"], destinationFolder: "Projects/x"),
            ["Pro"])
    }

    // MARK: - Expansion-state restore (#873)

    /// `bind`/`bindForTesting` with restored ids materializes exactly the
    /// reachable expanded chain — one fetch per expanded level, nothing for
    /// collapsed siblings (the lazy 10k budget holds through a restore).
    func testBindRestoresExpandedChainLazily() {
        let spy = FetchSpy([
            "": listing(dirs: [dir(1, "a"), dir(3, "other")], files: [file("root.md")]),
            "a": listing(dirs: [dir(2, "a/b")], files: [file("a/x.md")]),
            "a/b": listing(dirs: [], files: [file("a/b/y.md")]),
        ])
        let vm = FileTreeViewModel()
        vm.bindForTesting(fetcher: spy.fetch, restoringExpandedDirPaths: ["a", "a/b"])

        XCTAssertEqual(spy.calls, ["", "a", "a/b"], "restored chain fetches level by level")
        XCTAssertTrue(vm.expanded.contains(.dir(1)))
        XCTAssertTrue(vm.expanded.contains(.dir(2)))
        // "other" (id 3) was not restored: collapsed, never fetched.
        XCTAssertFalse(vm.expanded.contains(.dir(3)))
        XCTAssertEqual(
            vm.visibleRows.map(\.path),
            ["a", "a/b", "a/b/y.md", "a/x.md", "other", "root.md"],
            "pre-order walk shows the restored expansion")
    }

    /// A restored PATH whose parent is NOT restored stays dormant (no fetch)
    /// until the parent expands — then it reappears expanded, exactly like
    /// an in-session collapse/re-expand.
    func testRestoredIdUnderCollapsedParentStaysDormantUntilParentExpands() {
        let spy = FetchSpy([
            "": listing(dirs: [dir(1, "a")], files: []),
            "a": listing(dirs: [dir(2, "a/b")], files: []),
            "a/b": listing(dirs: [], files: [file("a/b/y.md")]),
        ])
        let vm = FileTreeViewModel()
        vm.bindForTesting(fetcher: spy.fetch, restoringExpandedDirPaths: ["a/b"])

        XCTAssertEqual(spy.calls, [""], "nothing beneath a collapsed parent is fetched")
        XCTAssertTrue(vm.pendingExpandedPaths.contains("a/b"), "dormant as a pending PATH")
        XCTAssertEqual(vm.visibleRows.map(\.path), ["a"])

        // Expanding the parent materializes its level AND cascades into the
        // remembered child expansion.
        vm.expand(vm.rootLevel[0])
        XCTAssertEqual(spy.calls, ["", "a", "a/b"])
        XCTAssertEqual(vm.visibleRows.map(\.path), ["a", "a/b", "a/b/y.md"])
    }

    /// Paths that no longer resolve are harmless: no fetch, no phantom
    /// rows; they linger only in the capped pending set.
    func testUnknownRestoredIdsAreHarmless() {
        let spy = FetchSpy([
            "": listing(dirs: [dir(1, "a")], files: []),
            "a": listing(dirs: [], files: [file("a/x.md")]),
        ])
        let vm = FileTreeViewModel()
        vm.bindForTesting(fetcher: spy.fetch, restoringExpandedDirPaths: ["a", "ghost"])

        XCTAssertEqual(spy.calls, ["", "a"], "the unknown path costs nothing")
        XCTAssertEqual(vm.visibleRows.map(\.path), ["a", "a/x.md"])
    }

    /// The persisted mirror is the dir PATHS (materialized ∪ dormant),
    /// sorted (deterministic file form).
    func testExpandedDirPathsMirrorIsSortedAndIncludesDormant() {
        let spy = FetchSpy([
            "": listing(dirs: [dir(7, "b"), dir(3, "a")], files: [])
        ])
        let vm = FileTreeViewModel()
        vm.bindForTesting(fetcher: spy.fetch, restoringExpandedDirPaths: ["ghost/dir"])
        vm.expand(vm.rootLevel[0])  // "b"
        vm.expand(vm.rootLevel[1])  // "a"
        // RECENCY order (oldest→newest): restored dormant first, then the
        // session expansions in the order they happened (Codex r3 — the
        // cap evicts from the FRONT, so order is the eviction policy).
        XCTAssertEqual(vm.expandedDirPaths, ["ghost/dir", "b", "a"])
    }

    func testCollapseAllMirrorsPendingOnlyDisclosureIntoAppState() {
        let tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("collapse-mirror-\(UUID().uuidString)")
        defer { try? FileManager.default.removeItem(at: tempDir) }
        let state = AppState(
            recentsStore: RecentVaultsStore(
                fileURL: tempDir.appendingPathComponent("recents.json")),
            externalOpener: { _ in true })
        let vm = FileTreeViewModel()
        vm.bindForTesting(
            fetcher: { _ in self.listing(dirs: [], files: []) },
            restoringExpandedDirPaths: ["ghost/dir"])
        state.treeExpandedDirPaths = vm.expandedDirPaths

        XCTAssertTrue(vm.expanded.isEmpty)
        XCTAssertEqual(state.treeExpandedDirPaths, ["ghost/dir"])
        XCTAssertTrue(
            FileTreeSidebar.collapseAllAndMirror(
                tree: vm, appState: state, preserving: nil))
        XCTAssertTrue(vm.pendingExpandedPaths.isEmpty)
        XCTAssertTrue(state.treeExpandedDirPaths.isEmpty)
    }

    /// Codex round 2 (probe-proven): SQLite reuses INTEGER PRIMARY KEY
    /// rowids after deletes. Expansion must NOT follow a recycled id to
    /// an unrelated folder: invalidation demotes the deleted folder's
    /// expansion to a path, and a NEW folder arriving under the same id
    /// stays collapsed.
    func testRecycledDirIDDoesNotInheritExpansion() {
        // Plain closure fetcher (no FetchSpy: the table is phase-dependent).
        var phase2 = false
        var calls: [String] = []
        let fetcher: (String) throws -> DirListing = { parent in
            calls.append(parent)
            if parent.isEmpty {
                return phase2
                    ? self.listing(dirs: [self.dir(2, "c")], files: [])
                    : self.listing(dirs: [self.dir(2, "b")], files: [])
            }
            return self.listing(dirs: [], files: [self.file("\(parent)/x.md")])
        }
        let vm = FileTreeViewModel()
        vm.bindForTesting(fetcher: fetcher)
        vm.expand(vm.rootLevel[0])  // expand "b" (id 2)
        XCTAssertTrue(vm.expanded.contains(.dir(2)))

        // "b" is deleted externally; "c" is created and REUSES id 2.
        phase2 = true
        vm.treeInvalidation(parent: nil)

        XCTAssertFalse(
            vm.expanded.contains(.dir(2)),
            "the recycled id must not resurrect b's expansion onto c")
        XCTAssertTrue(
            vm.pendingExpandedPaths.contains("b"),
            "the deleted folder survives only as an inert pending path")
        XCTAssertEqual(
            vm.visibleRows.map(\.path), ["c"],
            "c renders collapsed — no phantom children fetch")
        XCTAssertFalse(
            calls.contains("c"),
            "lazy-fetch holds: the recycled folder's children are never fetched (Codex r3)")
    }

    /// Codex round 3: NESTED mutations use targeted invalidation
    /// (`treeInvalidation(parent:)`) — the demote must run there too, or
    /// a recycled child id under that parent inherits expansion.
    func testRecycledNestedDirIDDoesNotInheritExpansion() {
        var phase2 = false
        let fetcher: (String) throws -> DirListing = { parent in
            if parent.isEmpty {
                return self.listing(dirs: [self.dir(1, "a")], files: [])
            }
            if parent == "a" {
                return phase2
                    ? self.listing(dirs: [self.dir(2, "a/c")], files: [])
                    : self.listing(dirs: [self.dir(2, "a/b")], files: [])
            }
            return self.listing(dirs: [], files: [])
        }
        let vm = FileTreeViewModel()
        vm.bindForTesting(fetcher: fetcher)
        vm.expand(vm.rootLevel[0])                       // a
        vm.expand(vm.visibleRows.first { $0.path == "a/b" }!)  // a/b (id 2)
        XCTAssertTrue(vm.expanded.contains(.dir(2)))

        phase2 = true
        vm.treeInvalidation(parent: .dir(1))             // a's level changed

        XCTAssertFalse(
            vm.expanded.contains(.dir(2)),
            "a/c must not inherit a/b's expansion via the recycled id")
        XCTAssertTrue(vm.pendingExpandedPaths.contains("a/b"))
        XCTAssertEqual(vm.visibleRows.map(\.path), ["a", "a/c"])
    }

    // MARK: - Expansion follows mutations (Codex round 4)

    /// Rename: the expanded folder's path (and descendants) remap, so the
    /// reloaded level re-promotes at the NEW path — no collapse, no
    /// tombstone.
    func testRemapExpansionFollowsRenameIncludingDescendants() {
        let vm = FileTreeViewModel()
        vm.bindForTesting(
            fetcher: { _ in self.listing(dirs: [], files: []) },
            restoringExpandedDirPaths: ["a/b", "a/b/deep", "other"])
        vm.remapExpansion(fromPrefix: "a/b", to: "a/c")
        XCTAssertEqual(
            Set(vm.pendingExpandedPaths), ["a/c", "a/c/deep", "other"])
        XCTAssertEqual(vm.expansionRecency, ["a/c", "a/c/deep", "other"])
        // Prefix means PATH COMPONENT prefix: "a/bx" must not remap.
        vm.remapExpansion(fromPrefix: "a/c", to: "a/b")
        let vm2 = FileTreeViewModel()
        vm2.bindForTesting(
            fetcher: { _ in self.listing(dirs: [], files: []) },
            restoringExpandedDirPaths: ["a/bx"])
        vm2.remapExpansion(fromPrefix: "a/b", to: "a/z")
        XCTAssertEqual(vm2.expansionRecency, ["a/bx"], "component boundary respected")
    }

    /// Delete: the subtree's expansion bookkeeping drops entirely — no
    /// pending tombstones eating cap slots.
    func testRemoveExpansionDropsDeletedSubtree() {
        let vm = FileTreeViewModel()
        vm.bindForTesting(
            fetcher: { _ in self.listing(dirs: [], files: []) },
            restoringExpandedDirPaths: ["gone", "gone/child", "kept"])
        vm.removeExpansion(underPrefix: "gone")
        XCTAssertEqual(vm.expansionRecency, ["kept"])
        XCTAssertEqual(vm.pendingExpandedPaths, ["kept"])
    }

    func testBatchExpansionMoveUsesStandingIndexAndLinearLookupWork() {
        let vm = FileTreeViewModel()
        vm.bindForTesting(
            fetcher: { _ in self.listing(dirs: [], files: []) },
            restoringExpandedDirPaths: [
                "source-9999", "source-9999/deep", "sourceish-9999", "rolled/path",
            ])
        let changes = (0..<10_000).map {
            FileTreeSidebar.SelectionModel.KnownMove(
                oldPath: "source-\($0)", newPath: "dest/source-\($0)",
                isDirectory: true)
        }
        let index = FileTreeSidebar.SelectionModel.KnownMoveIndex(changes)
        var visits = 0

        vm.remapExpansions(using: index, componentVisits: &visits)

        XCTAssertEqual(
            vm.expansionRecency,
            ["dest/source-9999", "dest/source-9999/deep", "sourceish-9999", "rolled/path"])
        XCTAssertLessThanOrEqual(visits, 7)
    }

    func testBatchExpansionTrashRemovesOnlyReportedDirectorySubtrees() {
        let vm = FileTreeViewModel()
        vm.bindForTesting(
            fetcher: { _ in self.listing(dirs: [], files: []) },
            restoringExpandedDirPaths: [
                "trash", "trash/deep", "trashish", "exact-file/deep", "kept",
            ])
        let index = FileTreeSidebar.SelectionModel.KnownRemovalIndex([
            .init(path: "trash", isDirectory: true),
            .init(path: "exact-file", isDirectory: false),
        ])
        var visits = 0

        vm.removeExpansions(using: index, componentVisits: &visits)

        XCTAssertEqual(
            vm.expansionRecency,
            ["trashish", "exact-file/deep", "kept"])
        XCTAssertEqual(vm.pendingExpandedPaths, ["trashish", "exact-file/deep", "kept"])
    }

    func testRemovalOwnerOrderingVisitsFiftyThousandUnrelatedRootsLinearly() {
        var candidates: [(id: NodeID, path: String)] = (0..<50_000).map {
            (
                id: .dir(Int64($0 + 1)),
                path: String(format: "root-%05d", $0)
            )
        }
        candidates.append(contentsOf: (0..<1_000).map {
            (
                id: .dir(Int64(50_001 + $0)),
                path: String(format: "root-00000/child-%04d", $0)
            )
        })
        candidates.append((id: .dir(51_001), path: "root-00000"))
        var visits = 0

        let roots = FileTreeViewModel.orderedRemovedDirectoryOwners(
            candidates, componentVisits: &visits)

        XCTAssertEqual(roots.count, 51_001)
        XCTAssertEqual(visits, 52_001)
        XCTAssertEqual(
            Set(
                roots.filter { $0.path == "root-00000" }.map { $0.id }
            ).count, 2,
            "same-path predecessor and replacement owners must both clear")
        XCTAssertTrue(roots.contains { $0.path == "root-00000/child-0000" })
    }

    func testBatchRemovalPublishesOnceForOneThousandOwnedLevels() {
        let paths = (0..<1_000).map {
            String(format: "root-%04d", $0)
        }
        let vm = FileTreeViewModel()
        vm.bindForTesting { parent in
            parent.isEmpty
                ? self.listing(
                    dirs: paths.enumerated().map {
                        self.dir(Int64($0.offset + 1), $0.element)
                    }, files: [])
                : self.listing(dirs: [], files: [])
        }
        vm.expandLoadedLevels()
        XCTAssertEqual(vm.children.count, 1_000)
        let presentationBefore = vm.presentationRevision
        let index = FileTreeSidebar.SelectionModel.KnownRemovalIndex(
            paths.map { .init(path: $0, isDirectory: true) })
        var visits = 0

        vm.removeExpansions(using: index, componentVisits: &visits)

        XCTAssertTrue(vm.expanded.isEmpty)
        XCTAssertTrue(vm.children.isEmpty)
        XCTAssertEqual(
            vm.removalOwnershipBatchPublicationCountForTesting, 1)
        XCTAssertEqual(vm.presentationRevision, presentationBefore + 1)
    }

    /// Targeted invalidation also drops DESCENDANT caches: a recycled id
    /// must not serve the deleted folder's stale children on next expand.
    func testRecycledNestedIDDoesNotServeStaleChildren() {
        var phase2 = false
        var calls: [String] = []
        let fetcher: (String) throws -> DirListing = { parent in
            calls.append(parent)
            if parent.isEmpty {
                return self.listing(dirs: [self.dir(1, "a")], files: [])
            }
            if parent == "a" {
                return phase2
                    ? self.listing(dirs: [self.dir(2, "a/c")], files: [])
                    : self.listing(dirs: [self.dir(2, "a/b")], files: [])
            }
            if parent == "a/b" {
                return self.listing(dirs: [], files: [self.file("a/b/stale.md")])
            }
            if parent == "a/c" {
                return self.listing(dirs: [], files: [self.file("a/c/fresh.md")])
            }
            return self.listing(dirs: [], files: [])
        }
        let vm = FileTreeViewModel()
        vm.bindForTesting(fetcher: fetcher)
        vm.expand(vm.rootLevel[0])
        vm.expand(vm.visibleRows.first { $0.path == "a/b" }!)
        XCTAssertTrue(vm.visibleRows.map(\.path).contains("a/b/stale.md"))

        phase2 = true
        vm.treeInvalidation(parent: .dir(1))
        vm.expand(vm.visibleRows.first { $0.path == "a/c" }!)
        XCTAssertTrue(
            vm.visibleRows.map(\.path).contains("a/c/fresh.md"),
            "the recycled id must fetch C's REAL children, not serve B's cache")
        XCTAssertFalse(vm.visibleRows.map(\.path).contains("a/b/stale.md"))
    }

    /// Codex round 5: the REAL mutation ordering — invalidation reloads
    /// the level synchronously BEFORE the remap runs. The remap's
    /// re-adoption sweep must promote the renamed folder on the
    /// already-landed level (and fetch its children).
    func testRenameOrderingKeepsFolderExpandedThroughInvalidateReloadRemap() {
        var renamed = false
        let fetcher: (String) throws -> DirListing = { parent in
            if parent.isEmpty {
                return self.listing(dirs: [self.dir(1, "a")], files: [])
            }
            if parent == "a" {
                return renamed
                    ? self.listing(dirs: [self.dir(9, "a/c")], files: [])
                    : self.listing(dirs: [self.dir(2, "a/b")], files: [])
            }
            if parent == "a/c" {
                return self.listing(dirs: [], files: [self.file("a/c/kid.md")])
            }
            if parent == "a/b" {
                return self.listing(dirs: [], files: [self.file("a/b/kid.md")])
            }
            return self.listing(dirs: [], files: [])
        }
        let vm = FileTreeViewModel()
        vm.bindForTesting(fetcher: fetcher)
        vm.expand(vm.rootLevel[0])
        vm.expand(vm.visibleRows.first { $0.path == "a/b" }!)

        // Exactly handleTreeMutation's sequence for a rename:
        renamed = true
        vm.treeInvalidation(parent: .dir(1))          // reload lands FIRST
        vm.remapExpansion(fromPrefix: "a/b", to: "a/c")  // then the remap

        XCTAssertTrue(
            vm.visibleRows.map(\.path).contains("a/c/kid.md"),
            "the renamed folder stays expanded with children fetched")
        XCTAssertFalse(vm.pendingExpandedPaths.contains("a/b"), "no tombstone")
    }

    /// Codex round 6: production renames PRESERVE the dir id — the
    /// `expanded` ID SET can be identical pre/post reconciliation, so
    /// the persisted-path source (`expandedDirPaths`) must reflect the
    /// new path anyway (the view syncs the mirror explicitly because
    /// `.onChange(of: expanded)` can't fire in this case).
    func testStableIDRenameUpdatesPathLedgerDespiteUnchangedIDSet() {
        var renamed = false
        let fetcher: (String) throws -> DirListing = { parent in
            if parent.isEmpty {
                return self.listing(dirs: [self.dir(1, "a")], files: [])
            }
            if parent == "a" {
                // SAME id 2 both phases — the production rename shape.
                return renamed
                    ? self.listing(dirs: [self.dir(2, "a/c")], files: [])
                    : self.listing(dirs: [self.dir(2, "a/b")], files: [])
            }
            return self.listing(dirs: [], files: [])
        }
        let vm = FileTreeViewModel()
        vm.bindForTesting(fetcher: fetcher)
        vm.expand(vm.rootLevel[0])
        vm.expand(vm.visibleRows.first { $0.path == "a/b" }!)
        let idSetBefore = vm.expanded

        renamed = true
        vm.treeInvalidation(parent: .dir(1))
        vm.remapExpansion(fromPrefix: "a/b", to: "a/c")

        XCTAssertEqual(vm.expanded, idSetBefore, "the id set really is unchanged")
        XCTAssertTrue(vm.expandedDirPaths.contains("a/c"))
        XCTAssertFalse(
            vm.expandedDirPaths.contains("a/b"),
            "the path ledger moved even though the id set didn't")
    }

    /// Same ordering for a cross-parent MOVE (two levels invalidate).
    func testMoveOrderingKeepsFolderExpandedAcrossParents() {
        var moved = false
        let fetcher: (String) throws -> DirListing = { parent in
            if parent.isEmpty {
                return self.listing(
                    dirs: [self.dir(1, "src"), self.dir(5, "dst")], files: [])
            }
            if parent == "src" {
                return moved
                    ? self.listing(dirs: [], files: [])
                    : self.listing(dirs: [self.dir(2, "src/pack")], files: [])
            }
            if parent == "dst" {
                return moved
                    ? self.listing(dirs: [self.dir(8, "dst/pack")], files: [])
                    : self.listing(dirs: [], files: [])
            }
            if parent.hasSuffix("/pack") {
                return self.listing(dirs: [], files: [self.file("\(parent)/kid.md")])
            }
            return self.listing(dirs: [], files: [])
        }
        let vm = FileTreeViewModel()
        vm.bindForTesting(fetcher: fetcher)
        vm.expand(vm.rootLevel[0])  // src
        vm.expand(vm.rootLevel[1])  // dst
        vm.expand(vm.visibleRows.first { $0.path == "src/pack" }!)

        moved = true
        vm.treeInvalidation(parent: .dir(1))  // source level
        vm.treeInvalidation(parent: .dir(5))  // destination level
        vm.remapExpansion(fromPrefix: "src/pack", to: "dst/pack")

        XCTAssertTrue(
            vm.visibleRows.map(\.path).contains("dst/pack/kid.md"),
            "the moved folder arrives expanded at its destination")
        XCTAssertFalse(vm.pendingExpandedPaths.contains("src/pack"))
    }

    // MARK: - Type-select buffer semantics (Codex round 2)

    /// Repeating the same character cycles (buffer holds ONE char, so the
    /// matcher's advance-from-selection lands the next match); a different
    /// character refines the prefix.
    func testTypeSelectBufferRepeatCyclesAndRefines() {
        XCTAssertEqual(
            FileTreeSidebar.nextTypeSelectBuffer(current: "", incoming: "b"), "b")
        XCTAssertEqual(
            FileTreeSidebar.nextTypeSelectBuffer(current: "b", incoming: "b"), "b",
            "repeat = cycle, not the dead-end prefix bb")
        XCTAssertEqual(
            FileTreeSidebar.nextTypeSelectBuffer(current: "b", incoming: "B"), "B",
            "case-insensitive repeat still cycles")
        XCTAssertEqual(
            FileTreeSidebar.nextTypeSelectBuffer(current: "b", incoming: "e"), "be")
        XCTAssertEqual(
            FileTreeSidebar.nextTypeSelectBuffer(current: "be", incoming: "t"), "bet")
    }

    func testTypeSelectUsesTheDisplayedTitleForFilesAndTheNameForFolders() {
        let titled = FileSummary(
            path: "Journal/review-2026-07.md", name: "review-2026-07.md", mtimeMs: 0,
            sizeBytes: 0, isMarkdown: true, displayName: "Weekly review",
            createdDate: nil, createdMs: nil, wordCount: nil, preview: nil,
            taskTotal: 0, taskOpen: 0)
        let fileNode = TreeNode(
            nodeID: .file(path: titled.path), path: titled.path, name: titled.name,
            depth: 1, kind: .file(FileTreeFileState(summary: titled)))
        let folderNode = TreeNode(
            nodeID: .dir(9), path: "Reference", name: "Reference", depth: 0,
            kind: .directory(childDirCount: 0, childFileCount: 1, hasFolderNote: false))

        XCTAssertEqual(FileTreeSidebar.typeSelectName(for: fileNode), "Weekly review")
        XCTAssertEqual(FileTreeSidebar.typeSelectName(for: folderNode), "Reference")
        XCTAssertEqual(
            FileTreeSidebar.typeSelectIndex(
                names: [
                    FileTreeSidebar.typeSelectName(for: fileNode),
                    FileTreeSidebar.typeSelectName(for: folderNode),
                ],
                prefix: "wee",
                selectedIndex: nil),
            0)
    }

    func testProgrammaticSelectionAnnouncementSuppressionIsOneShot() {
        var gate = FileTreeSidebar.SelectionAnnouncementGate()

        XCTAssertFalse(gate.consume(), "a user-driven selection must announce")
        gate.arm()
        XCTAssertTrue(gate.consume(), "a programmatic mirror suppresses its list edge")
        XCTAssertFalse(gate.consume(), "the next user-driven selection must announce")

        gate.arm()
        gate.arm()
        XCTAssertTrue(gate.consume(), "repeated mirrors still suppress only one list edge")
        XCTAssertFalse(gate.consume())
    }

    func testFL05SelectionRevisionCarrierGateMatchesOnceClearsMismatchAndRearms() {
        var gate = FileTreeSidebar.SelectionRevisionGate()
        let first = FileTreeSidebar.RowID.node(.file(path: "first.md"))
        let second = FileTreeSidebar.RowID.node(.file(path: "second.md"))

        XCTAssertFalse(gate.consume(if: first))
        gate.arm(for: first)
        XCTAssertTrue(gate.consume(if: first), "the exact carrier is neutral once")
        XCTAssertFalse(gate.consume(if: first), "the next same-row user intent is newer")

        gate.arm(for: first)
        XCTAssertFalse(
            gate.consume(if: second),
            "a different user value must not inherit a stale programmatic origin")
        XCTAssertFalse(gate.consume(if: first), "a mismatch clears the stale gate")

        gate.arm(for: first)
        gate.arm(for: second)
        XCTAssertFalse(gate.consume(if: first), "re-arming overwrites a coalesced expectation")
        gate.arm(for: second)
        XCTAssertTrue(gate.consume(if: second))
    }
}
