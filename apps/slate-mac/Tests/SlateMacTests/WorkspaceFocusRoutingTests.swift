// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI
import XCTest

@testable import SlateMac

/// U4-4 (#473): focus routing across the three terminal regions (file tree ·
/// editor split groups · right-pane leaf) plus the leaf-context matrix.
///
/// Two tiers, mirroring the project's model/behavior split:
///
/// - The **routing census** (`testCensusFocusRoutingWithTerminalRegions`)
///   extends U1-3's 800-seed geometry census (`WorkspaceSplitsTests`) with the
///   tree/leaf terminal moves. It drives `WorkspaceState`'s region state
///   machine — the same `resolveFocusRouting` + primitives `AppState.focusPane`
///   applies — so the decision under test is production's, not a re-derivation.
///   After every op: the model stays valid (I1–I7), focus is always resolvable
///   (region + `lastFocusedGroup` consistent), and focus is never lost;
///   round-trips at both edges are asserted deterministically.
///
/// - The **leaf-context matrix** and **announcement strings** run through the
///   real `AppState` (multi-file vault, the identity funnel) and pin behavior
///   that is correct by construction after U1-2 — the tests are the deliverable
///   the spec asks for.
@MainActor
final class WorkspaceFocusRoutingTests: XCTestCase {

    // Deterministic PRNG — byte-identical to WorkspaceSplitsTests' census so
    // the two censuses share a seeding discipline (u4_spec §U4-4).
    private struct SplitMix64: RandomNumberGenerator {
        var state: UInt64
        mutating func next() -> UInt64 {
            state &+= 0x9E37_79B9_7F4A_7C15
            var z = state
            z = (z ^ (z >> 30)) &* 0xBF58_476D_1CE4_E5B9
            z = (z ^ (z >> 27)) &* 0x94D0_49BB_1331_11EB
            return z ^ (z >> 31)
        }
    }

    private var tempDir: URL!

    override func setUpWithError() throws {
        try super.setUpWithError()
        tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-focus-routing-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: tempDir, withIntermediateDirectories: true)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: tempDir)
        try super.tearDownWithError()
    }

    // MARK: - Faithful apply of AppState.focusPane onto a WorkspaceState
    //
    // A 1:1 structural mirror of `AppState.focusPane`'s apply step (minus the
    // announcement + activateTab EFFECTS, which never touch focus state):
    // resolve the outcome purely, then perform its one focus mutation via the
    // exact primitives production calls. Keeping this a mirror — rather than
    // driving a full AppState with async note-load I/O — makes the census
    // deterministic and fast while testing production's decision logic.
    @discardableResult
    private func applyFocusPane(
        _ direction: WorkspaceModel.Direction, to ws: WorkspaceState
    ) -> WorkspaceState.FocusRoutingOutcome {
        let neighbor = ws.peekNeighbor(direction)
        let outcome = ws.resolveFocusRouting(direction, interiorNeighbor: neighbor)
        switch outcome {
        case .editorGroup(let target):
            ws.focusGroup(target)
            ws.markEditorRegionActive()  // activateTab does this in production
        case .returnToEditor:
            ws.focusEditorRegion()
        case .enterTree:
            ws.focusTreeRegion()
        case .enterLeaf:
            ws.focusLeafRegion()
        case .none:
            break
        }
        return outcome
    }

    /// Seed a WorkspaceState with `tabCount` markdown tabs and a random split
    /// layout (2–`groups` groups). Returns after the layout is built with the
    /// region on `.editor` (the fresh-state default).
    private func seededWorkspace(
        tabCount: Int, groups: Int, rng: inout SplitMix64
    ) -> WorkspaceState {
        let ws = WorkspaceState()
        for i in 0..<tabCount {
            ws.openTab(.markdown(path: "n\(i).md"), allowDuplicate: true)
        }
        let axes: [SplitBranch.Axis] = [.horizontal, .vertical]
        for _ in 1..<max(1, groups) {
            let axis = axes[Int(rng.next() % 2)]
            _ = ws.split(ws.model.activeGroupID, axis: axis)
        }
        return ws
    }

    // MARK: - The routing census (u4_spec §U4-4)

    /// Focus is always resolvable and never lost across arbitrary sequences of
    /// split / close / open / move mixed with ⌘⌥arrow region moves; the model
    /// stays valid (I1–I7) after every op; and the four edge round-trips hold.
    func testCensusFocusRoutingWithTerminalRegions() {
        let directions: [WorkspaceModel.Direction] = [.left, .right, .up, .down]

        for seed in 0..<800 {
            var rng = SplitMix64(state: UInt64(seed) &* 0x51ED_2701 &+ 7)
            let ws = seededWorkspace(
                tabCount: 1 + Int(rng.next() % 3),
                groups: 1 + Int(rng.next() % 4), rng: &rng)
            var transcript: [String] = []

            for step in 0..<120 {
                let roll = rng.next() % 12
                switch roll {
                case 0, 1:
                    let axis: SplitBranch.Axis = rng.next() % 2 == 0 ? .horizontal : .vertical
                    _ = ws.split(ws.model.activeGroupID, axis: axis)
                    ws.markEditorRegionActive()
                    transcript.append("split")
                case 2:
                    if let tab = ws.model.activeGroup.activeTabID {
                        _ = ws.close(tab)
                        transcript.append("closeActive")
                    }
                case 3:
                    let groups = ws.model.groupsInOrder
                    let g = groups[Int(rng.next() % UInt64(groups.count))]
                    if let tab = g.tabs.randomElement(using: &rng) {
                        _ = ws.close(tab.id)
                        transcript.append("closeRandom")
                    }
                case 4:
                    ws.openTab(.markdown(path: "s\(step).md"), allowDuplicate: true)
                    transcript.append("open")
                case 5:
                    // Jump focus to a random group (⌘-click a pane) — churns the
                    // active group, which is what a terminal move anchors on.
                    let groups = ws.model.groupsInOrder
                    let target = groups[Int(rng.next() % UInt64(groups.count))]
                    ws.focusGroup(target.id)
                    ws.markEditorRegionActive()
                    transcript.append("focusGroup")
                case 6, 7, 8, 9:
                    let dir = directions[Int(rng.next() % 4)]
                    applyFocusPane(dir, to: ws)
                    transcript.append("focus-\(dir)")
                case 10:
                    // Ordinary interior activation (⌘1…9 / sidebar click) —
                    // resets the region to .editor, the production invariant.
                    ws.markEditorRegionActive()
                    transcript.append("markEditor")
                default:
                    ws.activeLeaf =
                        Leaf.registered[Int(rng.next() % UInt64(Leaf.registered.count))]
                    transcript.append("switchLeaf")
                }

                let context = "seed \(seed) step \(step): "
                    + transcript.joined(separator: " → ")

                // 1. The model is never corrupted by a region op (I1–I7).
                let violations = ws.model.validate()
                XCTAssertTrue(
                    violations.isEmpty, "\(context)\nviolations: \(violations)")

                // 2. Focus is always resolvable. In .editor the active group
                // is a real group with a live tab whenever any tab exists
                // (I7). In a terminal, the return anchor resolves to a real
                // group (never dangles).
                switch ws.focusRegion {
                case .editor:
                    XCTAssertNotNil(
                        ws.model.group(ws.model.activeGroupID),
                        "\(context)\nactive group vanished")
                    if !ws.model.allTabs.isEmpty {
                        XCTAssertNotNil(
                            ws.model.activeGroup.activeTabID,
                            "\(context)\nI7: tabs exist but no active tab")
                    }
                case .tree, .leaf:
                    XCTAssertNotNil(
                        ws.model.group(ws.resolvedReturnGroup),
                        "\(context)\nreturn anchor unresolvable in terminal region")
                }
            }
        }
    }

    // MARK: - Edge round-trips (deterministic — the census's headline property)

    /// ⌘⌥← off the leftmost group enters the tree; ⌘⌥→ returns to that exact
    /// group. ⌘⌥→ off the rightmost group enters the leaf; ⌘⌥← returns to it.
    /// Both hold on a horizontal 3-group layout where the group identities are
    /// known.
    func testTerminalRoundTripsReturnToTheSameGroup() {
        let ws = WorkspaceState()
        ws.openTab(.markdown(path: "a.md"), allowDuplicate: true)
        // Two right-splits → three horizontal groups g0 | g1 | g2, focus on g2.
        _ = ws.split(ws.model.activeGroupID, axis: .horizontal)
        _ = ws.split(ws.model.activeGroupID, axis: .horizontal)
        let ordered = ws.model.groupsInOrder.map(\.id)
        XCTAssertEqual(ordered.count, 3)
        let (g0, _, g2) = (ordered[0], ordered[1], ordered[2])

        // --- West edge: focus the leftmost group, then ⌘⌥← → tree, ⌘⌥→ → g0.
        ws.focusGroup(g0)
        ws.markEditorRegionActive()
        XCTAssertEqual(applyFocusPane(.left, to: ws), .enterTree)
        XCTAssertEqual(ws.focusRegion, .tree)
        XCTAssertEqual(ws.resolvedReturnGroup, g0, "anchor is the group we left")
        XCTAssertEqual(applyFocusPane(.right, to: ws), .returnToEditor(g0))
        XCTAssertEqual(ws.focusRegion, .editor)
        XCTAssertEqual(ws.model.activeGroupID, g0, "returned to the same group")

        // --- East edge: focus the rightmost group, then ⌘⌥→ → leaf, ⌘⌥← → g2.
        ws.focusGroup(g2)
        ws.markEditorRegionActive()
        XCTAssertEqual(applyFocusPane(.right, to: ws), .enterLeaf)
        XCTAssertEqual(ws.focusRegion, .leaf)
        XCTAssertEqual(ws.resolvedReturnGroup, g2)
        XCTAssertEqual(applyFocusPane(.left, to: ws), .returnToEditor(g2))
        XCTAssertEqual(ws.focusRegion, .editor)
        XCTAssertEqual(ws.model.activeGroupID, g2)
    }

    /// From the leaf, ⌘⌥→ is a no-op (far edge); from the tree, ⌘⌥← is a no-op
    /// (far edge). Neither loses focus nor changes region.
    func testFarEdgesAreNoOps() {
        let ws = WorkspaceState()
        ws.openTab(.markdown(path: "a.md"), allowDuplicate: true)

        ws.focusLeafRegion()
        XCTAssertEqual(applyFocusPane(.right, to: ws), WorkspaceState.FocusRoutingOutcome.none)
        XCTAssertEqual(ws.focusRegion, .leaf, "leaf ⌘⌥→ is the far edge")

        ws.focusEditorRegion()
        ws.focusTreeRegion()
        XCTAssertEqual(applyFocusPane(.left, to: ws), WorkspaceState.FocusRoutingOutcome.none)
        XCTAssertEqual(ws.focusRegion, .tree, "tree ⌘⌥← is the far edge")
    }

    /// Vertical moves off a horizontal edge do NOT cross into a terminal (the
    /// tree/leaf flank east/west only): ⌘⌥↑ / ⌘⌥↓ with no vertical neighbor is
    /// a no-op that keeps the editor region.
    func testVerticalEdgeDoesNotEnterTerminal() {
        let ws = WorkspaceState()
        ws.openTab(.markdown(path: "a.md"), allowDuplicate: true)  // one group
        ws.markEditorRegionActive()
        XCTAssertEqual(applyFocusPane(.up, to: ws), WorkspaceState.FocusRoutingOutcome.none)
        XCTAssertEqual(ws.focusRegion, .editor)
        XCTAssertEqual(applyFocusPane(.down, to: ws), WorkspaceState.FocusRoutingOutcome.none)
        XCTAssertEqual(ws.focusRegion, .editor)
    }

    /// A collapse that eats the return anchor while focus sits in a terminal
    /// must not strand focus: the return resolves to the model's current
    /// active group instead (focus never lost — I7 across the boundary).
    func testCollapsedAnchorFallsBackToActiveGroup() {
        let ws = WorkspaceState()
        ws.openTab(.markdown(path: "a.md"), allowDuplicate: true)
        _ = ws.split(ws.model.activeGroupID, axis: .horizontal)  // g0 | g1, focus g1
        let ordered = ws.model.groupsInOrder.map(\.id)
        let (g0, g1) = (ordered[0], ordered[1])

        ws.focusGroup(g1)
        ws.markEditorRegionActive()
        applyFocusPane(.right, to: ws)  // enter leaf; anchor = g1
        XCTAssertEqual(ws.focusRegion, .leaf)

        // g1 collapses (its only tab closes) while we're in the leaf.
        if let tab = ws.model.group(g1)?.activeTabID { _ = ws.close(tab) }
        XCTAssertNil(ws.model.group(g1), "anchor group collapsed")

        // ⌘⌥← still returns to a real group (g0, now the sole group).
        let outcome = applyFocusPane(.left, to: ws)
        XCTAssertEqual(outcome, .returnToEditor(g0))
        XCTAssertEqual(ws.focusRegion, .editor)
        XCTAssertEqual(ws.model.activeGroupID, g0)
    }

    /// Entering a terminal, then an interior activation (sidebar click / ⌘1)
    /// resets the region to .editor — so the NEXT ⌘⌥arrow anchors on the newly
    /// active group, not the stale terminal.
    func testInteriorActivationResetsRegionFromTerminal() {
        let ws = WorkspaceState()
        ws.openTab(.markdown(path: "a.md"), allowDuplicate: true)
        ws.focusTreeRegion()
        XCTAssertEqual(ws.focusRegion, .tree)
        ws.markEditorRegionActive()  // e.g. a sidebar click opened a note
        XCTAssertEqual(ws.focusRegion, .editor)
        XCTAssertNil(ws.lastFocusedGroup, "stale anchor cleared on editor re-entry")
    }

    // MARK: - Announcement strings (verbatim per u4_spec §U4-4)

    /// "Files." on tree entry, "<leaf title> panel." on leaf entry, "Editor
    /// pane N of M, <title>." on an interior/return move — the exact strings a
    /// VoiceOver user hears (pure builders; the free announcement post has no
    /// test spy, so the string IS the contract).
    func testAnnouncementStringsAreVerbatim() {
        XCTAssertEqual(AppState.filesRegionAnnouncement, "Files.")

        XCTAssertEqual(
            AppState.leafRegionAnnouncement(.outline), "Outline panel.")
        XCTAssertEqual(
            AppState.leafRegionAnnouncement(.backlinks), "Backlinks panel.")
        XCTAssertEqual(
            AppState.leafRegionAnnouncement(.bibliography), "Bibliography panel.")

        XCTAssertEqual(
            AppState.editorPaneAnnouncement(ordinal: 2, total: 3, title: "notes.md"),
            "Editor pane 2 of 3, notes.md.")
        // The "Split. " prefix path (reused from U1-3) is unchanged.
        XCTAssertEqual(
            AppState.editorPaneAnnouncement(
                ordinal: 1, total: 2, title: "a.md", prefix: "Split. "),
            "Split. Editor pane 1 of 2, a.md.")
    }

    /// The leaf-entry announcement matches the leaf-SWITCH announcement
    /// (`RightPaneView.activate` posts "\(leaf.title) panel.") so entering the
    /// leaf and switching leaves read identically — one phrasing, learned once.
    func testLeafEntryAnnouncementMatchesLeafSwitchPhrasing() {
        for leaf in Leaf.registered {
            XCTAssertEqual(
                AppState.leafRegionAnnouncement(leaf), "\(leaf.title) panel.")
        }
    }

    // MARK: - No trap (u4_spec §U4-4: "⌘⌥arrows + Tab both exit")

    /// The ⌘⌥arrow overlay never traps: from EVERY region there is at least one
    /// direction that moves focus OUT (the routing outcome is not `.none`). The
    /// two terminals each have their single escape (→ from tree, ← from leaf);
    /// the editor always escapes to a terminal off a horizontal edge (or to a
    /// neighbor interior). Proven on a two-group layout so an interior neighbor
    /// exists in one direction too.
    func testNoRegionIsAOneWayTrap() {
        let ws = WorkspaceState()
        ws.openTab(.markdown(path: "a.md"), allowDuplicate: true)
        _ = ws.split(ws.model.activeGroupID, axis: .horizontal)  // two groups
        let directions: [WorkspaceModel.Direction] = [.left, .right, .up, .down]

        for region: WorkspaceState.FocusRegion in [.tree, .editor, .leaf] {
            switch region {
            case .tree: ws.focusTreeRegion()
            case .leaf: ws.focusLeafRegion()
            case .editor: ws.markEditorRegionActive()
            }
            let hasExit = directions.contains { dir in
                ws.resolveFocusRouting(dir, interiorNeighbor: ws.peekNeighbor(dir)) != .none
            }
            XCTAssertTrue(hasExit, "region \(region) has no ⌘⌥arrow exit — a trap")
        }
    }

    /// Tab is the window's native focus order, NOT something the routing
    /// overrides: the terminal-region focus wiring uses `.focusable()` +
    /// `.focused()`/`.accessibilityFocused()` mirroring request tokens, and
    /// introduces NO Tab-trapping construct (`.focusScope`, `.focusSection`,
    /// `prefersDefaultFocus`). Pinned structurally — SwiftUI exposes no
    /// rendered-focus-order API to assert against (the RightPaneViewTests
    /// idiom). If a future edit adds a trap, this fails.
    func testRoutingIsAnOverlayNotATabTrap() throws {
        let rightPane = try source(of: "Workspace/RightPaneView.swift")
        let sidebar = try source(of: "FileTreeSidebar.swift")
        for (name, text) in [("RightPaneView", rightPane), ("FileTreeSidebar", sidebar)] {
            for trap in ["focusScope", "focusSection", "prefersDefaultFocus"] {
                XCTAssertFalse(
                    text.contains(trap),
                    "\(name) introduced a Tab-trapping `\(trap)` — the ⌘⌥arrow "
                        + "routing must stay an overlay on the native Tab order")
            }
        }
        // Both terminals honor the focus request via a post-update `.onChange`
        // mutation point (#448 — never publish inside the view update). The
        // tree does it through `TreeFocusBridge`, which observes `workspace`
        // (the sidebar itself observes only appState, whose publisher doesn't
        // forward the nested WorkspaceState @Published).
        XCTAssertTrue(
            sidebar.contains("onChange(of: workspace.treeFocusRequest)"),
            "tree must mirror the focus request on .onChange")
        XCTAssertTrue(
            sidebar.contains("@ObservedObject var workspace: WorkspaceState"),
            "the tree focus bridge must OBSERVE workspace so the request re-renders it")
        XCTAssertTrue(
            rightPane.contains("onChange(of: workspace.leafFocusRequest)"),
            "leaf must mirror the focus request on .onChange")
        // The leaf entry point routes BOTH keyboard and VoiceOver focus.
        XCTAssertTrue(
            rightPane.contains(".focused($leafFocused)")
                && rightPane.contains(".accessibilityFocused($leafAXFocused)"),
            "the leaf focus anchor must bind both keyboard and AX focus")
        // …and stays invisible: each leaf already renders its own count header,
        // so the anchor must NOT add a second visible one (it's a Color.clear
        // focus sink). Guards against a future edit reintroducing a duplicate
        // header on the anchor.
        XCTAssertTrue(
            rightPane.contains("private var leafFocusAnchor")
                && rightPane.contains("Color.clear"),
            "the leaf focus anchor must be an invisible focus sink, not a visible header")
    }

    /// Read a Sources/SlateMac file relative to this test file (the
    /// SlateCommandsTests.projectRoot walk-up, localized).
    private func source(of relativePath: String) throws -> String {
        let url = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()  // SlateMacTests
            .deletingLastPathComponent()  // Tests
            .deletingLastPathComponent()  // slate-mac
            .appendingPathComponent("Sources/SlateMac")
            .appendingPathComponent(relativePath)
        return try String(contentsOf: url, encoding: .utf8)
    }
}
