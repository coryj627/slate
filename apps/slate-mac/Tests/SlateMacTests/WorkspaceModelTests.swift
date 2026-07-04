// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// U1-1 (#453): unit tests + the adversarial censuses over `WorkspaceModel`.
///
/// The censuses are the release guarantee that the workspace can never reach
/// an unfocusable or orphaned state (invariants I1–I7,
/// `docs/plans/08_ui_parity/specs/u1_spec.md`). They run in every
/// configuration — not `debug_assert`-gated (the #404 discipline).
final class WorkspaceModelTests: XCTestCase {

    // MARK: - Helpers

    private func md(_ path: String) -> EditorItem { .markdown(path: path) }

    /// Canonical 2-group / 3-tab fixture: [a, b] | [c], focus on group 2.
    private func twoGroupFixture() -> WorkspaceModel {
        var model = WorkspaceModel()
        model.openTab(md("a.md"))
        model.openTab(md("b.md"))
        let g2 = model.split(model.activeGroupID, axis: .horizontal)
        XCTAssertNotNil(g2)
        model.replaceActiveTabItem(md("c.md"))
        return model
    }

    private func assertValid(
        _ model: WorkspaceModel, _ context: @autoclosure () -> String = "",
        file: StaticString = #filePath, line: UInt = #line
    ) {
        let violations = model.validate()
        XCTAssertTrue(
            violations.isEmpty,
            "invariant violations \(violations) \(context())", file: file, line: line)
    }

    // MARK: - Constructors

    func testInvariantsOnEveryConstructor() {
        assertValid(WorkspaceModel(), "fresh")

        var single = WorkspaceModel()
        single.openTab(md("a.md"))
        assertValid(single, "single tab")
        XCTAssertEqual(single.allTabs.count, 1)
        XCTAssertEqual(single.activeGroup.activeTab?.item, md("a.md"))

        assertValid(twoGroupFixture(), "two-group fixture")
    }

    func testFreshWorkspaceIsEmptyRootGroup() {
        let model = WorkspaceModel()
        XCTAssertTrue(model.isEmpty)
        XCTAssertNil(model.activeGroup.activeTabID)
        XCTAssertEqual(model.groupsInOrder.count, 1)
    }

    // MARK: - Open semantics

    func testOpenDeduplicatesWithinGroup() {
        var model = WorkspaceModel()
        let first = model.openTab(md("a.md"))
        let second = model.openTab(md("a.md"))
        XCTAssertEqual(first, second, "same item in same group reuses the tab")
        XCTAssertEqual(model.allTabs.count, 1)
    }

    func testOpenAllowDuplicateBypassesDedupWithinGroup() {
        var model = WorkspaceModel()
        let first = model.openTab(md("a.md"))
        let dup = model.openTab(md("a.md"), allowDuplicate: true)
        XCTAssertNotEqual(first, dup, "explicit duplicate creates a second tab")
        XCTAssertEqual(model.allTabs.count, 2)
        XCTAssertEqual(model.activeGroup.activeTabID, dup)
        assertValid(model)
        // Navigation-open still dedups — it selects one of the existing
        // tabs rather than opening a third.
        let third = model.openTab(md("a.md"))
        XCTAssertEqual(model.allTabs.count, 2)
        XCTAssertTrue(third == first || third == dup)
    }

    func testOpenAllowsSameItemAcrossGroups() {
        var model = twoGroupFixture()
        // Fixture: group1 [a, b], group2 [c] (split duplicated b then we
        // replaced it with c). Open a.md in group 2 — allowed.
        model.openTab(md("a.md"))
        XCTAssertEqual(
            model.allTabs.filter { $0.item == md("a.md") }.count, 2,
            "distinct groups may hold the same item")
        assertValid(model)
    }

    func testOpenInsertsAfterActiveTab() {
        var model = WorkspaceModel()
        model.openTab(md("a.md"))
        model.openTab(md("b.md"))
        model.selectTab(ordinal: 1)  // back to a
        model.openTab(md("c.md"))
        XCTAssertEqual(
            model.activeGroup.tabs.map(\.item),
            [md("a.md"), md("c.md"), md("b.md")],
            "new tab lands after the active tab")
    }

    func testReplaceActiveTabItemOnEmptyGroupOpens() {
        var model = WorkspaceModel()
        model.replaceActiveTabItem(md("a.md"))
        XCTAssertEqual(model.activeGroup.activeTab?.item, md("a.md"))
        assertValid(model)
    }

    func testReplaceActiveTabItemDedupSelectsExisting() {
        var model = WorkspaceModel()
        let a = model.openTab(md("a.md"))
        model.openTab(md("b.md"))
        model.replaceActiveTabItem(md("a.md"))
        XCTAssertEqual(model.activeGroup.activeTabID, a)
        XCTAssertEqual(
            model.activeGroup.tabs.map(\.item), [md("a.md"), md("b.md")],
            "b is not replaced when a already exists — selection moves instead")
    }

    // MARK: - Close semantics

    func testCloseFocusesRightNeighborThenLeft() {
        var model = WorkspaceModel()
        let a = model.openTab(md("a.md"))
        let b = model.openTab(md("b.md"))
        let c = model.openTab(md("c.md"))
        model.selectTab(b)
        var outcome = model.closeTab(b)
        XCTAssertEqual(outcome.focusedTab, c, "right neighbor takes focus")
        outcome = model.closeTab(c)
        XCTAssertEqual(outcome.focusedTab, a, "no right neighbor → left")
        assertValid(model)
    }

    func testCloseInactiveTabKeepsFocus() {
        var model = WorkspaceModel()
        let a = model.openTab(md("a.md"))
        let b = model.openTab(md("b.md"))
        let outcome = model.closeTab(a)
        XCTAssertEqual(outcome.focusedTab, b, "closing an inactive tab keeps focus put")
        XCTAssertEqual(model.activeGroup.activeTabID, b)
    }

    func testCloseLastTabCollapsesToEmptyRoot() {
        var model = WorkspaceModel()
        let a = model.openTab(md("a.md"))
        let outcome = model.closeTab(a)
        XCTAssertNil(outcome.focusedTab)
        XCTAssertNil(outcome.collapsedGroup, "root group never collapses")
        XCTAssertTrue(model.isEmpty)
        assertValid(model)
    }

    func testCloseLastTabOfPaneCollapsesPaneAndFocusesPreviousInReadingOrder() {
        var model = twoGroupFixture()
        let group1 = model.groupsInOrder[0].id
        let group2 = model.groupsInOrder[1].id
        XCTAssertEqual(model.activeGroupID, group2)
        let cTab = model.activeGroup.activeTabID!
        let outcome = model.closeTab(cTab)
        XCTAssertEqual(outcome.collapsedGroup, group2)
        XCTAssertEqual(model.activeGroupID, group1, "previous group in reading order")
        XCTAssertEqual(outcome.focusedTab, model.activeGroup.activeTabID)
        XCTAssertEqual(model.groupsInOrder.count, 1, "split dissolved")
        assertValid(model)
    }

    func testCloseFirstPaneFocusesNextInReadingOrder() {
        var model = twoGroupFixture()
        let group1 = model.groupsInOrder[0].id
        let group2 = model.groupsInOrder[1].id
        model.focusGroup(group1)
        // Close both tabs of group 1.
        let tabs = model.group(group1)!.tabs.map(\.id)
        model.closeTab(tabs[0])
        let outcome = model.closeTab(tabs[1])
        XCTAssertEqual(outcome.collapsedGroup, group1)
        XCTAssertEqual(model.activeGroupID, group2, "no previous → next group")
        assertValid(model)
    }

    // MARK: - Selection

    func testSelectOrdinalNineSelectsLast() {
        var model = WorkspaceModel()
        for i in 1...4 { model.openTab(md("n\(i).md")) }
        model.selectTab(ordinal: 9)
        XCTAssertEqual(model.activeGroup.activeTab?.item, md("n4.md"))
        model.selectTab(ordinal: 2)
        XCTAssertEqual(model.activeGroup.activeTab?.item, md("n2.md"))
        model.selectTab(ordinal: 7)  // out of range (≠9) — no-op
        XCTAssertEqual(model.activeGroup.activeTab?.item, md("n2.md"))
    }

    func testNextPreviousWrap() {
        var model = WorkspaceModel()
        model.openTab(md("a.md"))
        model.openTab(md("b.md"))
        model.openTab(md("c.md"))
        model.selectNextTab()
        XCTAssertEqual(model.activeGroup.activeTab?.item, md("a.md"), "wraps forward")
        model.selectPreviousTab()
        XCTAssertEqual(model.activeGroup.activeTab?.item, md("c.md"), "wraps backward")
    }

    func testSelectTabAcrossGroupsMovesGroupFocus() {
        var model = twoGroupFixture()
        let group1 = model.groupsInOrder[0]
        model.selectTab(group1.tabs[0].id)
        XCTAssertEqual(model.activeGroupID, group1.id)
        assertValid(model)
    }

    // MARK: - Reorder / move

    func testReorderWithinGroupClamps() {
        var model = WorkspaceModel()
        let a = model.openTab(md("a.md"))
        model.openTab(md("b.md"))
        model.openTab(md("c.md"))
        model.moveTab(a, toIndex: 99)
        XCTAssertEqual(
            model.activeGroup.tabs.map(\.item), [md("b.md"), md("c.md"), md("a.md")])
        model.moveTab(a, toIndex: 0)
        XCTAssertEqual(
            model.activeGroup.tabs.map(\.item), [md("a.md"), md("b.md"), md("c.md")])
        assertValid(model)
    }

    func testMoveTabToOtherGroupCollapsesEmptiedSource() {
        var model = twoGroupFixture()
        let group1 = model.groupsInOrder[0]
        let group2 = model.groupsInOrder[1]
        let c = model.group(group2.id)!.tabs[0].id
        model.moveTab(c, toGroup: group1.id)
        XCTAssertEqual(model.groupsInOrder.count, 1, "emptied source collapsed")
        XCTAssertEqual(model.activeGroup.activeTabID, c, "moved tab is active in destination")
        assertValid(model)
    }

    // MARK: - Split

    func testSplitDuplicatesActiveItemByDefault() {
        var model = WorkspaceModel()
        model.openTab(md("a.md"))
        let newGroup = model.split(model.activeGroupID, axis: .horizontal)
        XCTAssertNotNil(newGroup)
        XCTAssertEqual(model.allTabs.count, 2, "duplicate, not move")
        XCTAssertEqual(model.activeGroupID, newGroup, "focus lands in the new pane")
        XCTAssertEqual(model.activeGroup.activeTab?.item, md("a.md"))
        assertValid(model)
    }

    func testSplitMoveRejectedOnSingleTabGroup() {
        var model = WorkspaceModel()
        model.openTab(md("a.md"))
        let before = model
        XCTAssertNil(model.split(model.activeGroupID, axis: .horizontal, moveActiveTab: true))
        XCTAssertEqual(model, before, "rejected split leaves the model untouched")
    }

    func testSplitEmptyGroupRejected() {
        var model = WorkspaceModel()
        let before = model
        XCTAssertNil(model.split(model.activeGroupID, axis: .vertical))
        XCTAssertEqual(model, before)
    }

    func testSplitRejectedAtGroupCapacity() {
        var model = WorkspaceModel()
        model.openTab(md("a.md"))
        // 1 pane + 5 splits = 6 groups (the global capacity). Alternate axes
        // so the cap is exercised as a WORKSPACE limit, not a branch limit.
        let axes: [SplitBranch.Axis] = [.horizontal, .vertical, .horizontal, .vertical, .horizontal]
        for (i, axis) in axes.enumerated() {
            XCTAssertNotNil(
                model.split(model.activeGroupID, axis: axis),
                "split \(i + 1) fits under capacity")
        }
        XCTAssertEqual(model.groupsInOrder.count, WorkspaceModel.maxGroups)
        let before = model
        XCTAssertNil(
            model.split(model.activeGroupID, axis: .vertical),
            "a 7th pane could not hold the min-weight floor after merges")
        XCTAssertEqual(model, before, "rejected split leaves the model untouched")
        // Closing a pane frees capacity again.
        if let tab = model.activeGroup.activeTabID { model.closeTab(tab) }
        XCTAssertNotNil(model.split(model.activeGroupID, axis: .horizontal))
        assertValid(model)
    }

    func testSameAxisSplitFlattens() {
        var model = WorkspaceModel()
        model.openTab(md("a.md"))
        model.split(model.activeGroupID, axis: .horizontal)
        model.split(model.activeGroupID, axis: .horizontal)
        // Three side-by-side panes must be ONE 3-child split, not nested.
        guard case .split(let branch) = modelRoot(model) else {
            return XCTFail("expected a split root")
        }
        XCTAssertEqual(branch.children.count, 3, "n-ary same-axis split")
        XCTAssertEqual(branch.weights.reduce(0, +), 1, accuracy: 1e-9)
        assertValid(model)
    }

    func testCrossAxisSplitNests() {
        var model = WorkspaceModel()
        model.openTab(md("a.md"))
        model.split(model.activeGroupID, axis: .horizontal)
        model.split(model.activeGroupID, axis: .vertical)
        guard case .split(let branch) = modelRoot(model) else {
            return XCTFail("expected a split root")
        }
        XCTAssertEqual(branch.axis, .horizontal)
        XCTAssertEqual(branch.children.count, 2)
        let nested = branch.children.compactMap { child -> SplitBranch? in
            if case .split(let inner) = child { return inner }
            return nil
        }
        XCTAssertEqual(nested.count, 1)
        XCTAssertEqual(nested.first?.axis, .vertical)
        assertValid(model)
    }

    /// Read the root via a test-only mirror (the model hides `root` behind
    /// `private(set)`; tests inspect through `groupRects` + this helper).
    private func modelRoot(_ model: WorkspaceModel) -> SplitNode {
        Mirror(reflecting: model).descendant("root") as! SplitNode
    }

    // MARK: - Weights

    func testMinWeightClamp() {
        var model = WorkspaceModel()
        model.openTab(md("a.md"))
        model.split(model.activeGroupID, axis: .horizontal)
        let focused = model.activeGroupID
        for _ in 0..<50 { model.setWeight(delta: -WorkspaceModel.resizeStep, for: focused) }
        let rects = model.groupRects()
        XCTAssertGreaterThanOrEqual(
            rects[focused]!.width, WorkspaceModel.minWeight - 1e-9,
            "keyboard shrink clamps at the floor")
        assertValid(model)
    }

    func testGrowRedistributesProportionally() {
        var model = WorkspaceModel()
        model.openTab(md("a.md"))
        model.split(model.activeGroupID, axis: .horizontal)
        model.split(model.activeGroupID, axis: .horizontal)
        let focused = model.activeGroupID
        model.setWeight(delta: 0.1, for: focused)
        let rects = model.groupRects()
        let widths = model.groupsInOrder.map { rects[$0.id]!.width }
        XCTAssertEqual(widths.reduce(0, +), 1, accuracy: 1e-6)
        XCTAssertTrue(widths.allSatisfy { $0 >= WorkspaceModel.minWeight - 1e-9 })
        assertValid(model)
    }

    // MARK: - Spatial focus

    func testSpatialFocusGeometryTwoByTwo() {
        // [A | B] over [C | D] — an H split of two V splits… built as:
        // open A, split right (B), split A down (C), split B down (D).
        var model = WorkspaceModel()
        model.openTab(md("a.md"))
        let gA = model.activeGroupID
        let gB = model.split(gA, axis: .horizontal)!
        model.focusGroup(gA)
        let gC = model.split(gA, axis: .vertical)!
        model.focusGroup(gB)
        let gD = model.split(gB, axis: .vertical)!
        assertValid(model)

        // From every corner, every direction lands on the geometric neighbor.
        model.focusGroup(gA)
        XCTAssertEqual(model.focusNeighbor(.right), gB)
        XCTAssertEqual(model.focusNeighbor(.down), gD)
        XCTAssertEqual(model.focusNeighbor(.left), gC)
        XCTAssertEqual(model.focusNeighbor(.up), gA)
        XCTAssertNil(model.focusNeighbor(.up), "edge: no neighbor above the top row")
        XCTAssertEqual(model.activeGroupID, gA, "failed move never moves focus")
    }

    func testSpatialFocusTLayoutTieBreak() {
        // Left pane full-height; right side split into top/bottom. From the
        // left pane, → must pick the TOP-right pane (equal distance, equal
        // overlap tie → top-most).
        var model = WorkspaceModel()
        model.openTab(md("left.md"))
        let gLeft = model.activeGroupID
        let gRight = model.split(gLeft, axis: .horizontal)!
        let gBottom = model.split(gRight, axis: .vertical)!
        model.focusGroup(gLeft)
        let target = model.focusNeighbor(.right)
        XCTAssertEqual(target, gRight, "tie-break: top-most wins")
        // And ← from the bottom-right pane returns to the left pane.
        model.focusGroup(gBottom)
        XCTAssertEqual(model.focusNeighbor(.left), gLeft)
    }

    func testOrdinalIsReadingOrder() {
        let model = twoGroupFixture()
        XCTAssertEqual(model.ordinal(of: model.groupsInOrder[0].id), 1)
        XCTAssertEqual(model.ordinal(of: model.groupsInOrder[1].id), 2)
        XCTAssertNil(model.ordinal(of: GroupID()))
    }

    // MARK: - Censuses

    /// Deterministic RNG so census failures are replayable from the printed
    /// seed (SplitMix64 — tiny and adequate for operation shuffling).
    private struct SplitMix64: RandomNumberGenerator {
        var state: UInt64
        init(seed: UInt64) { state = seed }
        mutating func next() -> UInt64 {
            state &+= 0x9E37_79B9_7F4A_7C15
            var z = state
            z = (z ^ (z >> 30)) &* 0xBF58_476D_1CE4_E5B9
            z = (z ^ (z >> 27)) &* 0x94D0_49BB_1331_11EB
            return z ^ (z >> 31)
        }
    }

    private enum CensusOp: CaseIterable {
        case open, openExisting, close, closeInactive, select, next, previous,
            ordinal, reorder, moveToGroup, splitHDup, splitVDup, splitHMove,
            splitVMove, focusGroup, focusLeft, focusRight, focusUp, focusDown,
            grow, shrink, replaceActive
    }

    /// Apply one random operation; returns a transcript line for replay.
    private func applyRandomOp(
        _ op: CensusOp, to model: inout WorkspaceModel, rng: inout SplitMix64,
        pathCounter: inout Int
    ) -> String {
        func randomTab() -> TabID? {
            let tabs = model.allTabs
            guard !tabs.isEmpty else { return nil }
            return tabs[Int(rng.next() % UInt64(tabs.count))].id
        }
        func randomGroup() -> GroupID {
            let groups = model.groupsInOrder
            return groups[Int(rng.next() % UInt64(groups.count))].id
        }
        switch op {
        case .open:
            pathCounter += 1
            let path = "note-\(pathCounter).md"
            model.openTab(.markdown(path: path))
            return "open(\(path))"
        case .openExisting:
            if let tab = randomTab(), let item = model.tab(tab)?.item {
                model.openTab(item, in: randomGroup())
                return "openExisting"
            }
            return "openExisting(skip)"
        case .close:
            if let active = model.activeGroup.activeTabID {
                model.closeTab(active)
                return "closeActive"
            }
            return "close(skip)"
        case .closeInactive:
            if let tab = randomTab() {
                model.closeTab(tab)
                return "close(random)"
            }
            return "close(skip)"
        case .select:
            if let tab = randomTab() {
                model.selectTab(tab)
                return "select"
            }
            return "select(skip)"
        case .next:
            model.selectNextTab()
            return "next"
        case .previous:
            model.selectPreviousTab()
            return "previous"
        case .ordinal:
            let n = Int(rng.next() % 10)
            model.selectTab(ordinal: n)
            return "ordinal(\(n))"
        case .reorder:
            if let tab = randomTab() {
                let idx = Int(rng.next() % 8)
                model.moveTab(tab, toIndex: idx)
                return "reorder(\(idx))"
            }
            return "reorder(skip)"
        case .moveToGroup:
            if let tab = randomTab() {
                model.moveTab(tab, toGroup: randomGroup())
                return "moveToGroup"
            }
            return "moveToGroup(skip)"
        case .splitHDup:
            model.split(randomGroup(), axis: .horizontal)
            return "splitH(dup)"
        case .splitVDup:
            model.split(randomGroup(), axis: .vertical)
            return "splitV(dup)"
        case .splitHMove:
            model.split(randomGroup(), axis: .horizontal, moveActiveTab: true)
            return "splitH(move)"
        case .splitVMove:
            model.split(randomGroup(), axis: .vertical, moveActiveTab: true)
            return "splitV(move)"
        case .focusGroup:
            model.focusGroup(randomGroup())
            return "focusGroup"
        case .focusLeft:
            model.focusNeighbor(.left)
            return "focus(←)"
        case .focusRight:
            model.focusNeighbor(.right)
            return "focus(→)"
        case .focusUp:
            model.focusNeighbor(.up)
            return "focus(↑)"
        case .focusDown:
            model.focusNeighbor(.down)
            return "focus(↓)"
        case .grow:
            model.setWeight(delta: WorkspaceModel.resizeStep, for: model.activeGroupID)
            return "grow"
        case .shrink:
            model.setWeight(delta: -WorkspaceModel.resizeStep, for: model.activeGroupID)
            return "shrink"
        case .replaceActive:
            pathCounter += 1
            model.replaceActiveTabItem(.markdown(path: "swap-\(pathCounter).md"))
            return "replaceActive"
        }
    }

    /// 2,000 seeded runs × 100 operations = 200k ops. Invariants validated
    /// after every op; cross-op assertions on tab-count deltas. Failure
    /// output includes the seed and full transcript for replay.
    func testCensusRandomOperationSequences() {
        let runs = 2_000
        let opsPerRun = 100
        let ops = CensusOp.allCases
        for run in 0..<runs {
            var rng = SplitMix64(seed: UInt64(run) &* 0x0FA5_7EE1 &+ 1)
            var model = WorkspaceModel()
            var pathCounter = 0
            var transcript: [String] = []
            for step in 0..<opsPerRun {
                let op = ops[Int(rng.next() % UInt64(ops.count))]
                let before = model.allTabs.count
                let line = applyRandomOp(
                    op, to: &model, rng: &rng, pathCounter: &pathCounter)
                transcript.append(line)
                let violations = model.validate()
                if !violations.isEmpty {
                    XCTFail(
                        "census seed \(run) step \(step): \(violations)\n"
                            + transcript.joined(separator: " → "))
                    return
                }
                // Cross-op tab-count sanity: any single op changes the total
                // by at most 1 (open/close/split-dup ±1, everything else 0).
                let after = model.allTabs.count
                if abs(after - before) > 1 {
                    XCTFail(
                        "census seed \(run) step \(step): tab count jumped "
                            + "\(before)→\(after) on \(line)\n"
                            + transcript.joined(separator: " → "))
                    return
                }
            }
        }
    }

    /// Exhaustive small-N: from the canonical fixture, every operation
    /// sequence of length ≤ 3 over a bounded, deterministic alphabet.
    /// ~19³ ≈ 6.9k sequences; invariants after every step.
    func testCensusExhaustiveSmallN() {
        // Deterministic alphabet: closures over the *current* model state.
        typealias Op = (inout WorkspaceModel) -> Void
        let alphabet: [(String, Op)] = [
            ("open", { $0.openTab(.markdown(path: "x.md")) }),
            ("openY", { $0.openTab(.markdown(path: "y.md")) }),
            ("closeActive", { m in
                if let t = m.activeGroup.activeTabID { m.closeTab(t) }
            }),
            ("closeFirst", { m in
                if let t = m.groupsInOrder.first?.tabs.first?.id { m.closeTab(t) }
            }),
            ("closeLast", { m in
                if let t = m.groupsInOrder.last?.tabs.last?.id { m.closeTab(t) }
            }),
            ("next", { $0.selectNextTab() }),
            ("prev", { $0.selectPreviousTab() }),
            ("ord1", { $0.selectTab(ordinal: 1) }),
            ("ord9", { $0.selectTab(ordinal: 9) }),
            ("reorder0", { m in
                if let t = m.activeGroup.activeTabID { m.moveTab(t, toIndex: 0) }
            }),
            ("moveToFirstGroup", { m in
                if let t = m.activeGroup.activeTabID,
                    let g = m.groupsInOrder.first?.id { m.moveTab(t, toGroup: g) }
            }),
            ("splitH", { m in _ = m.split(m.activeGroupID, axis: .horizontal) }),
            ("splitV", { m in _ = m.split(m.activeGroupID, axis: .vertical) }),
            ("splitHMove", { m in
                _ = m.split(m.activeGroupID, axis: .horizontal, moveActiveTab: true)
            }),
            ("focusFirst", { m in m.focusGroup(m.groupsInOrder.first!.id) }),
            ("focusLast", { m in m.focusGroup(m.groupsInOrder.last!.id) }),
            ("left", { m in _ = m.focusNeighbor(.left) }),
            ("right", { m in _ = m.focusNeighbor(.right) }),
            ("grow", { m in m.setWeight(delta: 0.05, for: m.activeGroupID) }),
        ]

        func run(_ sequence: [Int]) {
            var model = twoGroupFixture()
            var names: [String] = []
            for idx in sequence {
                let (name, op) = alphabet[idx]
                names.append(name)
                op(&model)
                let violations = model.validate()
                if !violations.isEmpty {
                    XCTFail("exhaustive [\(names.joined(separator: " → "))]: \(violations)")
                    return
                }
                // I7 restated: if any tab exists the active group can show one.
                if !model.isEmpty {
                    XCTAssertNotNil(
                        model.activeGroup.activeTabID,
                        "unfocusable state after [\(names.joined(separator: " → "))]")
                }
            }
        }

        for a in alphabet.indices {
            run([a])
            for b in alphabet.indices {
                run([a, b])
                for c in alphabet.indices {
                    run([a, b, c])
                }
            }
        }
    }

    /// Persistence round-trip prerequisite (full store lands in U1-6): the
    /// `EditorItem` Codable schema must reject unknown kinds by throwing —
    /// the store's tab-dropping tolerance builds on that contract. Both
    /// inhabited kinds round-trip (#369 activated "canvas"); "graph"
    /// (Milestone P, still future) is the forward-compat probe.
    func testEditorItemCodableRoundTripAndUnknownKind() throws {
        for item in [
            EditorItem.markdown(path: "notes/α β.md"),
            EditorItem.canvas(path: "boards/α plan.canvas"),
        ] {
            let data = try JSONEncoder().encode(item)
            let decoded = try JSONDecoder().decode(EditorItem.self, from: data)
            XCTAssertEqual(item, decoded)
        }

        let unknown = Data(#"{"kind":"graph","path":"vault.graph"}"#.utf8)
        XCTAssertThrowsError(try JSONDecoder().decode(EditorItem.self, from: unknown))
    }

    // MARK: - retargetItem (U2-5, #463)

    /// A rename/move of an open file rewrites every tab pointing at it — across
    /// all groups — preserving tab identity, order, and each group's active
    /// pointer.
    func testRetargetItemRewritesAllMatchingTabsAcrossGroups() {
        var model = twoGroupFixture()  // [a, b] | [c], focus g2
        // Open a.md a second time in group 2 so two groups reference it.
        let g2 = model.activeGroupID
        model.openTab(md("a.md"), in: g2)
        let aTabsBefore = model.allTabs.filter { $0.item == md("a.md") }.map(\.id)
        XCTAssertEqual(aTabsBefore.count, 2, "a.md open in two tabs")

        let changed = model.retargetItem(from: md("a.md"), to: md("renamed.md"))

        XCTAssertEqual(Set(changed), Set(aTabsBefore), "both a.md tabs retargeted, by id")
        XCTAssertTrue(
            model.allTabs.allSatisfy { $0.item != md("a.md") }, "no a.md left")
        XCTAssertEqual(
            model.allTabs.filter { $0.item == md("renamed.md") }.map(\.id).sorted(by: idOrder),
            aTabsBefore.sorted(by: idOrder),
            "same tab ids now hold renamed.md")
        assertValid(model, "after retarget")
    }

    func testRetargetItemNoOpWhenPathAbsentOrUnchanged() {
        var model = twoGroupFixture()
        XCTAssertEqual(model.retargetItem(from: md("nope.md"), to: md("x.md")), [])
        XCTAssertEqual(model.retargetItem(from: md("a.md"), to: md("a.md")), [])
        assertValid(model)
    }

    /// A folder move retargets each descendant file by its own mapping — the
    /// `applyRetargets` per-file loop relies on this.
    func testRetargetItemFolderDescendantsEachByOwnPath() {
        var model = WorkspaceModel()
        model.openTab(md("proj/a.md"))
        model.openTab(md("proj/sub/b.md"))
        model.openTab(md("other.md"))

        // Simulate the two descendant mappings a folder move produces.
        _ = model.retargetItem(from: md("proj/a.md"), to: md("archive/proj/a.md"))
        _ = model.retargetItem(from: md("proj/sub/b.md"), to: md("archive/proj/sub/b.md"))

        XCTAssertEqual(
            Set(model.allTabs.map(\.item)),
            [md("archive/proj/a.md"), md("archive/proj/sub/b.md"), md("other.md")],
            "descendants followed; the bystander file is untouched")
        assertValid(model)
    }

    private func idOrder(_ a: TabID, _ b: TabID) -> Bool {
        a.raw.uuidString < b.raw.uuidString
    }
}

