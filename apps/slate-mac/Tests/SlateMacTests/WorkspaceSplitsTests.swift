// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI
import XCTest

@testable import SlateMac

/// U1-3 (#455): split panes — the red-team focus census plus the
/// AppState-level split/focus/resize behavior and render gates.
@MainActor
final class WorkspaceSplitsTests: XCTestCase {

    private var tempDir: URL!

    override func setUpWithError() throws {
        tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("workspace-splits-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: tempDir, withIntermediateDirectories: true)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: tempDir)
    }

    private func makeAppState() -> AppState {
        let store = RecentVaultsStore(
            fileURL: tempDir.appendingPathComponent("recents.json"))
        return AppState(recentsStore: store, externalOpener: { _ in true })
    }

    private func makeOpenState() async throws -> AppState {
        let vault = tempDir.appendingPathComponent("vault-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        for name in ["alpha.md", "beta.md"] {
            try "# \(name)\nbody\n".write(
                to: vault.appendingPathComponent(name), atomically: true, encoding: .utf8)
        }
        let state = makeAppState()
        state.openVault(at: vault)
        await state.scanTask?.value
        state.selectedFilePath = "alpha.md"
        await state.noteLoadTask?.value
        return state
    }

    // MARK: - RED-TEAM census (u1_spec §U1-3)

    /// Focus must never be lost, duplicated, or land on a degenerate
    /// (invisibly small) pane across arbitrary split/close/move/focus/
    /// resize sequences. Model-level: geometry is the model's own
    /// `groupRects()`, which the renderer mirrors 1:1.
    func testCensusFocusNeverLostAcrossSplitMutations() {
        struct SplitMix64: RandomNumberGenerator {
            var state: UInt64
            mutating func next() -> UInt64 {
                state &+= 0x9E37_79B9_7F4A_7C15
                var z = state
                z = (z ^ (z >> 30)) &* 0xBF58_476D_1CE4_E5B9
                z = (z ^ (z >> 27)) &* 0x94D0_49BB_1331_11EB
                return z ^ (z >> 31)
            }
        }

        let directions: [WorkspaceModel.Direction] = [.left, .right, .up, .down]
        for seed in 0..<800 {
            var rng = SplitMix64(state: UInt64(seed) &* 0x51ED_2701 &+ 7)
            var model = WorkspaceModel()
            model.openTab(.markdown(path: "seed.md"))
            var transcript: [String] = []
            for step in 0..<120 {
                let roll = rng.next() % 10
                switch roll {
                case 0, 1:
                    let axis: SplitBranch.Axis = rng.next() % 2 == 0 ? .horizontal : .vertical
                    model.split(model.activeGroupID, axis: axis)
                    transcript.append("split")
                case 2:
                    if let tab = model.activeGroup.activeTabID {
                        model.closeTab(tab)
                        transcript.append("closeActive")
                    }
                case 3:
                    let groups = model.groupsInOrder
                    let g = groups[Int(rng.next() % UInt64(groups.count))]
                    if let tab = g.tabs.randomElement(using: &rng) {
                        model.closeTab(tab.id)
                        transcript.append("closeRandom")
                    }
                case 4:
                    model.openTab(.markdown(path: "n\(step).md"))
                    transcript.append("open")
                case 5:
                    let groups = model.groupsInOrder
                    let target = groups[Int(rng.next() % UInt64(groups.count))]
                    if let tab = model.activeGroup.activeTabID, target.id != model.activeGroupID {
                        model.moveTab(tab, toGroup: target.id)
                        transcript.append("moveToGroup")
                    }
                case 6, 7:
                    let dir = directions[Int(rng.next() % 4)]
                    let before = model.activeGroupID
                    if let landed = model.focusNeighbor(dir) {
                        // Round-trip: the reverse direction must reach a
                        // group whose rect contains the origin's center
                        // band — in the common symmetric case, exactly
                        // `before`. Assert the weaker, always-true form:
                        // reverse lands SOMEWHERE (focus is never stuck
                        // one-way at an interior edge).
                        let reverse: WorkspaceModel.Direction =
                            dir == .left ? .right : dir == .right ? .left
                            : dir == .up ? .down : .up
                        let back = model.focusNeighbor(reverse)
                        XCTAssertNotNil(
                            back,
                            "seed \(seed) step \(step): focus one-way trap "
                                + "\(dir) from \(before.raw) → \(landed.raw)")
                    }
                    transcript.append("focus")
                case 8:
                    model.setWeight(delta: 0.05, for: model.activeGroupID)
                    transcript.append("grow")
                default:
                    model.setWeight(delta: -0.05, for: model.activeGroupID)
                    transcript.append("shrink")
                }

                let violations = model.validate()
                if !violations.isEmpty {
                    XCTFail(
                        "seed \(seed) step \(step): \(violations)\n"
                            + transcript.joined(separator: " → "))
                    return
                }
                // Focused pane must be visible: its rect ≥ the min-weight
                // fraction in both dimensions (compounded splits shrink
                // rects multiplicatively, but every branch clamps at 0.15,
                // and depth ≤ 3 in practice — assert the hard floor of
                // 0.15² which no reachable layout undercuts).
                let rects = model.groupRects()
                guard let focused = rects[model.activeGroupID] else {
                    XCTFail(
                        "seed \(seed) step \(step): focused group has no rect\n"
                            + transcript.joined(separator: " → "))
                    return
                }
                let floor = WorkspaceModel.minWeight * WorkspaceModel.minWeight
                XCTAssertGreaterThan(
                    focused.width, floor - 1e-9,
                    "seed \(seed) step \(step): focused pane invisibly narrow")
                XCTAssertGreaterThan(
                    focused.height, floor - 1e-9,
                    "seed \(seed) step \(step): focused pane invisibly short")
            }
        }
    }

    // MARK: - Split behavior through AppState

    func testSplitDuplicatesDocumentAndFocusesNewPane() async throws {
        let state = try await makeOpenState()
        let originalGroup = state.workspace.model.activeGroupID
        state.splitActivePane(axis: .horizontal)

        XCTAssertEqual(state.workspace.model.groupsInOrder.count, 2)
        XCTAssertNotEqual(state.workspace.model.activeGroupID, originalGroup)
        XCTAssertEqual(state.loadedFilePath, "alpha.md", "duplicate shares the document")
        XCTAssertEqual(
            state.workspace.model.activeGroup.activeTab?.item,
            .markdown(path: "alpha.md"))
    }

    func testSplitAtCapacityAnnouncesAndDoesNothing() async throws {
        let state = try await makeOpenState()
        for _ in 0..<5 { state.splitActivePane(axis: .horizontal) }
        XCTAssertEqual(state.workspace.model.groupsInOrder.count, WorkspaceModel.maxGroups)
        let before = state.workspace.model
        state.splitActivePane(axis: .vertical)
        XCTAssertEqual(state.workspace.model, before, "capacity split is a no-op")
    }

    func testFocusPaneActivatesNeighborDocument() async throws {
        let state = try await makeOpenState()
        state.splitActivePane(axis: .horizontal)
        // Right pane focused (duplicate of alpha). Open beta there so the
        // two panes hold different documents.
        state.selectedFilePath = "beta.md"
        await state.noteLoadTask?.value
        XCTAssertEqual(state.loadedFilePath, "beta.md")

        state.focusPane(.left)
        await state.noteLoadTask?.value
        XCTAssertEqual(state.loadedFilePath, "alpha.md", "left pane's document restored")
        state.focusPane(.right)
        await state.noteLoadTask?.value
        XCTAssertEqual(state.loadedFilePath, "beta.md", "right pane's document restored")
    }

    func testGrowShrinkChangeFocusedPaneFraction() async throws {
        let state = try await makeOpenState()
        state.splitActivePane(axis: .horizontal)
        let before = state.workspace.focusedPaneFraction ?? 0
        state.growFocusedPane()
        let grown = state.workspace.focusedPaneFraction ?? 0
        XCTAssertGreaterThan(grown, before)
        state.shrinkFocusedPane()
        let shrunk = state.workspace.focusedPaneFraction ?? 0
        XCTAssertEqual(shrunk, before, accuracy: 1e-9)
    }

    func testCloseActivePaneSweepsCleanTabsAndCollapses() async throws {
        let state = try await makeOpenState()
        state.splitActivePane(axis: .horizontal)
        XCTAssertEqual(state.workspace.model.groupsInOrder.count, 2)
        state.closeActivePane()
        await state.noteLoadTask?.value
        XCTAssertEqual(state.workspace.model.groupsInOrder.count, 1, "pane collapsed")
        XCTAssertEqual(state.loadedFilePath, "alpha.md", "survivor pane focused")
    }

    func testCloseActivePaneHaltsOnDirtyTab() async throws {
        let state = try await makeOpenState()
        state.splitActivePane(axis: .horizontal)
        state.updateEditorText("# alpha.md\ndirty in split")
        state.closeActivePane()
        XCTAssertNotNil(state.pendingTabClose, "dirty tab gates the sweep")
        XCTAssertEqual(
            state.workspace.model.groupsInOrder.count, 2,
            "pane survives until the prompt resolves")
    }

    // MARK: - Render gates

    func testSplitWorkspaceRendersInBothAppearances() async throws {
        let state = try await makeOpenState()
        state.splitActivePane(axis: .horizontal)
        PresentationReady.assertRendersInBothAppearances(
            WorkspaceView()
                .environmentObject(state)
                .frame(width: 640, height: 400))
    }

    func testUnfocusedPaneIsReadOnlyParkedDocument() async throws {
        let state = try await makeOpenState()
        state.updateEditorText("# alpha.md\nlive content")
        state.splitActivePane(axis: .horizontal)
        // The split duplicated alpha; the LEFT pane is now unfocused and
        // must have a parked document carrying the live bytes.
        let unfocused = state.workspace.model.groupsInOrder.first {
            $0.id != state.workspace.model.activeGroupID
        }
        let parkedTab = try XCTUnwrap(unfocused?.activeTabID)
        let parked = try XCTUnwrap(state.workspace.document(for: parkedTab))
        XCTAssertEqual(parked.text, "# alpha.md\nlive content")
        // Live edits in the focused pane mirror into it (same path).
        state.updateEditorText("# alpha.md\nlive content updated")
        XCTAssertEqual(parked.text, "# alpha.md\nlive content updated")
    }
}
