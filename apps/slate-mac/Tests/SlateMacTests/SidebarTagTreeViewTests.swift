// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// FL5-2 (#665): the Tags section — pure disclosure/AX math over the
/// flattened pre-order FFI projection, the lazy fetch/refresh
/// lifecycle, activation handoffs into the shared filter overlay, and
/// tag/Untagged shortcut round-trips as containers.
@MainActor
final class SidebarTagTreeViewTests: XCTestCase {
    private func entry(
        _ full: String, depth: UInt32, direct: UInt32 = 1, files: UInt32 = 1
    ) -> TagTreeEntry {
        TagTreeEntry(
            segment: String(full.split(separator: "/").last ?? ""),
            full: full,
            fileCount: files,
            directCount: direct,
            depth: depth)
    }

    // MARK: - Pure presentation math

    func testVisibleRowsHonorAncestorDisclosureInPreOrder() {
        let entries = [
            entry("a", depth: 0, direct: 0, files: 3),
            entry("a/b", depth: 1, files: 2),
            entry("a/b/c", depth: 2),
            entry("m", depth: 0),
        ]
        // Nothing expanded: roots only, children flags correct.
        var rows = SidebarTagTreeModel.visibleRows(entries: entries, expanded: [])
        XCTAssertEqual(rows.map(\.entry.full), ["a", "m"])
        XCTAssertEqual(rows.map(\.hasChildren), [true, false])

        // Expanding a reveals a/b but NOT a/b/c (its own parent stays
        // collapsed).
        rows = SidebarTagTreeModel.visibleRows(entries: entries, expanded: ["a"])
        XCTAssertEqual(rows.map(\.entry.full), ["a", "a/b", "m"])

        // A stale expanded descendant with a collapsed ancestor stays
        // hidden.
        rows = SidebarTagTreeModel.visibleRows(
            entries: entries, expanded: ["a/b"])
        XCTAssertEqual(rows.map(\.entry.full), ["a", "m"])

        rows = SidebarTagTreeModel.visibleRows(
            entries: entries, expanded: ["a", "a/b"])
        XCTAssertEqual(rows.map(\.entry.full), ["a", "a/b", "a/b/c", "m"])
    }

    func testAccessibilityValueMatchesFolderRowConventions() {
        let entries = [
            entry("projects", depth: 0, direct: 0, files: 12),
            entry("projects/reading", depth: 1),
        ]
        let collapsed = SidebarTagTreeModel.visibleRows(
            entries: entries, expanded: [])
        XCTAssertEqual(
            SidebarTagTreeModel.accessibilityValue(for: collapsed[0]),
            "12 notes, collapsed, level 1")
        let expanded = SidebarTagTreeModel.visibleRows(
            entries: entries, expanded: ["projects"])
        XCTAssertEqual(
            SidebarTagTreeModel.accessibilityValue(for: expanded[0]),
            "12 notes, expanded, level 1")
        XCTAssertEqual(
            SidebarTagTreeModel.accessibilityValue(for: expanded[1]),
            "1 note, level 2")
    }

    func testRealTagCountExcludesSynthesizedIntermediates() {
        let entries = [
            entry("a", depth: 0, direct: 0, files: 2),
            entry("a/b", depth: 1),
            entry("m", depth: 0),
        ]
        XCTAssertEqual(SidebarTagTreeModel.realTagCount(entries: entries), 2)
    }

    // MARK: - Lifecycle (lazy fetch + refresh gating)

    private func makeState() -> AppState {
        AppState(
            recentsStore: RecentVaultsStore(
                fileURL: FileManager.default.temporaryDirectory
                    .appendingPathComponent(
                        "tagtree-recents-\(UUID().uuidString).json")))
    }

    func testTreeFetchesLazilyOnFirstExpandAndRefreshGatesOnExpansion() {
        let state = makeState()
        var fetches = 0
        state.sidebarTagTreeProvider = {
            fetches += 1
            return TagTree(
                entries: [], untaggedCount: 0, audioSummary: "0 tags.")
        }
        UserDefaults.standard.removeObject(
            forKey: AppState.tagsSectionExpandedKey)

        XCTAssertNil(state.sidebarTagTree, "collapsed: zero cost")
        state.refreshSidebarTagTree()
        XCTAssertEqual(fetches, 0, "refresh is a no-op while collapsed")

        state.sidebarTagsSectionExpanded = true
        XCTAssertEqual(fetches, 1, "first expand fetches")
        XCTAssertNotNil(state.sidebarTagTree)

        state.refreshSidebarTagTree()
        XCTAssertEqual(fetches, 2, "expanded refresh re-fetches")

        state.sidebarTagsSectionExpanded = false
        state.refreshSidebarTagTree()
        XCTAssertEqual(fetches, 2, "collapsing gates refresh again")
        UserDefaults.standard.removeObject(
            forKey: AppState.tagsSectionExpandedKey)
    }

    func testTransientFetchFailureKeepsThePreviousTree() {
        let state = makeState()
        var next: TagTree? = TagTree(
            entries: [entry("keep", depth: 0)],
            untaggedCount: 0, audioSummary: "1 tags.")
        state.sidebarTagTreeProvider = { next }
        UserDefaults.standard.removeObject(
            forKey: AppState.tagsSectionExpandedKey)
        state.sidebarTagsSectionExpanded = true
        XCTAssertEqual(state.sidebarTagTree?.entries.first?.full, "keep")
        next = nil
        state.refreshSidebarTagTree()
        XCTAssertEqual(
            state.sidebarTagTree?.entries.first?.full, "keep",
            "a failed refresh must not blank an expanded section")
        state.sidebarTagsSectionExpanded = false
        UserDefaults.standard.removeObject(
            forKey: AppState.tagsSectionExpandedKey)
    }
}
