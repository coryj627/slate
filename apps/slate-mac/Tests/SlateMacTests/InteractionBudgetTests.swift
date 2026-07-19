// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// U5-4 (#477): Milestone U interaction budgets, measured at the state
/// layer (the synchronous funnels the views bind — view-independent, so
/// the numbers are stable in CI). Two enforcement layers, per the repo's
/// own precedent (FileTreeSidebarTests' 10k render test):
///
/// - `ContinuousClock` CEILINGS — generous absolute bounds that FAIL the
///   suite on an order-of-magnitude regression (an accidental O(n) in a
///   funnel, a publish storm). These are regression tripwires, not
///   targets.
/// - `measure {}` blocks — the recorded numbers pasted into
///   BENCHMARKS.md §"Milestone U interaction budgets" as the 2026-07
///   baseline rows.
///
/// Budgets (ceilings): tab switch < 50ms · mode toggle < 50ms · leaf
/// switch < 10ms · tree expand (10k sibling fixture) < 500ms.
@MainActor
final class InteractionBudgetTests: XCTestCase {

    private var tempDir: URL!

    override func setUpWithError() throws {
        tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("budget-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: tempDir, withIntermediateDirectories: true)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: tempDir)
    }

    /// Two loaded tabs (both parked at least once), so the measured switch
    /// exercises the FULL funnel — snapshot ⊕ restore — on the synchronous
    /// parked path (no disk IO inside the measurement).
    private func makeTwoTabState() async throws -> AppState {
        let vault = tempDir.appendingPathComponent("vault")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        // Realistic small notes; the funnel cost must not depend on size
        // (that's the #404 guarantee, benched separately in Rust).
        let body = String(repeating: "line of text\n", count: 200)
        try "---\ntitle: A\n---\n\(body)".write(
            to: vault.appendingPathComponent("a.md"), atomically: true, encoding: .utf8)
        try body.write(
            to: vault.appendingPathComponent("b.md"), atomically: true, encoding: .utf8)
        let state = AppState(
            recentsStore: RecentVaultsStore(
                fileURL: tempDir.appendingPathComponent("recents.json")),
            externalOpener: { _ in true })
        state.openVault(at: vault)
        await state.scanTask?.value
        state.selectedFilePath = "a.md"
        await state.noteLoadTask?.value
        state.openFile("b.md", target: .newTab)
        await state.noteLoadTask?.value
        // Park/restore each side once so both documents are warm.
        state.selectPreviousTab()
        await state.noteLoadTask?.value
        state.selectNextTab()
        await state.noteLoadTask?.value
        return state
    }

    func testTabSwitchBudget() async throws {
        let state = try await makeTwoTabState()
        let clock = ContinuousClock()
        let elapsed = clock.measure {
            for _ in 0..<10 {
                state.selectPreviousTab()
                state.selectNextTab()
            }
        }
        // 20 switches under 20 × 50ms; per-switch ceiling 50ms.
        XCTAssertLessThan(
            elapsed, .seconds(1.0),
            "tab switch funnel blew the 50ms/switch ceiling: \(elapsed) for 20")
        measure {
            state.selectPreviousTab()
            state.selectNextTab()
        }
    }

    func testModeToggleBudget() async throws {
        let state = try await makeTwoTabState()
        let clock = ContinuousClock()
        let elapsed = clock.measure {
            for _ in 0..<10 {
                state.toggleViewMode()
                state.toggleViewMode()
            }
        }
        XCTAssertLessThan(
            elapsed, .seconds(1.0),
            "mode toggle blew the 50ms/toggle ceiling: \(elapsed) for 20")
        measure {
            state.toggleViewMode()
            state.toggleViewMode()
        }
    }

    func testLeafSwitchBudget() {
        let ws = WorkspaceState()
        let leaves = Leaf.registered
        let clock = ContinuousClock()
        let elapsed = clock.measure {
            for _ in 0..<10 {
                for leaf in leaves { ws.activeLeaf = leaf }
            }
        }
        // 100 switches; per-switch ceiling 10ms.
        XCTAssertLessThan(
            elapsed, .seconds(1.0),
            "leaf switch blew the 10ms ceiling: \(elapsed) for \(10 * leaves.count)")
        measure {
            for leaf in leaves { ws.activeLeaf = leaf }
        }
    }

    func testTreeExpandBudget10k() {
        // The U2-4 shape: a folder holding 10k files, expand + flatten +
        // collapse — the lazy-fetch budget the sidebar lives on.
        let files = (0..<10_000).map { i in
            FileSummary(
                path: String(format: "big/note-%05d.md", i),
                name: String(format: "note-%05d.md", i),
                mtimeMs: 0, sizeBytes: 0, isMarkdown: true, displayName: nil,
                createdDate: nil, createdMs: nil, wordCount: nil, preview: nil,
                taskTotal: 0, taskOpen: 0)
        }
        let root = DirListing(
            dirs: [
                DirNodeSummary(
                    id: 1, path: "big", name: "big",
                    childDirCount: 0, childFileCount: 10_000, hasFolderNote: false)
            ],
            files: FileSummaryPage(items: [], nextCursor: nil, totalFiltered: 0))
        let level = DirListing(
            dirs: [],
            files: FileSummaryPage(
                items: files, nextCursor: nil, totalFiltered: UInt64(files.count)))
        let vm = FileTreeViewModel()
        vm.bindForTesting(fetcher: { path in path.isEmpty ? root : level })
        let folder = vm.rootLevel[0]

        let clock = ContinuousClock()
        let elapsed = clock.measure {
            vm.expand(folder)
            _ = vm.visibleRows
        }
        XCTAssertEqual(vm.visibleRows.count, 10_001)
        XCTAssertLessThan(
            elapsed, .seconds(0.5),
            "10k expand+flatten blew the 500ms ceiling: \(elapsed)")
        vm.collapse(folder)
        measure {
            vm.expand(folder)  // cached level — steady-state re-expand
            _ = vm.visibleRows
            vm.collapse(folder)
        }
    }
}
