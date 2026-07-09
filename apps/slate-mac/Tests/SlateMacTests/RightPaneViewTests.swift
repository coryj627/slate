// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import SwiftUI
import XCTest

@testable import SlateMac

/// U4-1 (#470): the right-pane leaf container + vertical icon rail.
///
/// What is unit-testable lives here: the rail's keyboard-navigation mapping
/// (a pure function), the `Leaf` registry/persistence, `activeLeaf` round-trip
/// through `workspace.json`, the mounted-ZStack retention (mechanism +
/// load-fire spy), and the rail's render/contrast under the `PresentationReady`
/// harness. What has no XCTest-introspectable surface — the rendered SwiftUI AX
/// tree (labels/traits/reading order), Dynamic-Type reflow — is pinned
/// structurally (the source carries the AX modifiers / retention shape, the
/// technique `ContentBlockPanelsTests` and `CloseVaultSheetParityTests` already
/// trust) and covered behaviourally only by the VoiceOver runbook. This split
/// is honest by design — a fake AX-tree assertion would give false confidence.
@MainActor
final class RightPaneViewTests: XCTestCase {

    private var tempDir: URL!

    override func setUpWithError() throws {
        try super.setUpWithError()
        tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-rightpane-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: tempDir, withIntermediateDirectories: true)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: tempDir)
        try super.tearDownWithError()
    }

    private static func sourceFile(_ relativePath: String) throws -> String {
        var cursor = URL(fileURLWithPath: #filePath).deletingLastPathComponent()
        while cursor.path != "/" {
            let candidate = cursor.appendingPathComponent(relativePath)
            if FileManager.default.fileExists(atPath: candidate.path) {
                return try String(contentsOf: candidate, encoding: .utf8)
            }
            cursor.deleteLastPathComponent()
        }
        throw CocoaError(.fileNoSuchFile)
    }

    // MARK: - Rail keyboard navigation (pure function)

    /// ↑/↓ step through the registered leaves in order, clamped at the ends
    /// (no wrap — a segmented picker's arrow-within behavior). As of U4-2 all
    /// leaves are registered, in the rail/registry order
    /// [outline, backlinks, outgoingLinks, embeds, math, code, diagrams, tasks,
    /// citations, bibliography, queries, basesDock, syncDiagnostics].
    func testRailMoveSteppingAndClamping() {
        // Down from the top advances; down from the bottom is a no-op (clamp).
        XCTAssertEqual(Leaf.railMove(from: .outline, .down), .backlinks)
        XCTAssertEqual(Leaf.railMove(from: .backlinks, .down), .outgoingLinks)
        XCTAssertEqual(Leaf.railMove(from: .tasks, .down), .citations)
        XCTAssertEqual(Leaf.railMove(from: .citations, .down), .bibliography)
        // N4-3 (#709): queries sits between file-centric panels and sync.
        XCTAssertEqual(Leaf.railMove(from: .bibliography, .down), .queries)
        XCTAssertEqual(Leaf.railMove(from: .queries, .down), .basesDock)
        XCTAssertEqual(Leaf.railMove(from: .basesDock, .down), .syncDiagnostics)
        XCTAssertNil(Leaf.railMove(from: .syncDiagnostics, .down), "clamped at the end")

        // Up mirrors it.
        XCTAssertEqual(Leaf.railMove(from: .syncDiagnostics, .up), .basesDock)
        XCTAssertEqual(Leaf.railMove(from: .basesDock, .up), .queries)
        XCTAssertEqual(Leaf.railMove(from: .queries, .up), .bibliography)
        XCTAssertEqual(Leaf.railMove(from: .bibliography, .up), .citations)
        XCTAssertEqual(Leaf.railMove(from: .outgoingLinks, .up), .backlinks)
        XCTAssertEqual(Leaf.railMove(from: .backlinks, .up), .outline)
        XCTAssertNil(Leaf.railMove(from: .outline, .up), "clamped at the start")
    }

    /// Left/right don't move a vertical rail.
    func testRailMoveIgnoresHorizontal() {
        XCTAssertNil(Leaf.railMove(from: .outline, .left))
        XCTAssertNil(Leaf.railMove(from: .outline, .right))
        XCTAssertNil(Leaf.railMove(from: .citations, .left))
        XCTAssertNil(Leaf.railMove(from: .citations, .right))
    }

    /// An origin that isn't in the supplied order yields no move (defensive:
    /// the function stays total even for an origin outside the given order).
    /// All `Leaf` cases are registered now, so this is exercised via an
    /// explicit restricted order rather than a real unregistered leaf.
    func testRailMoveFromOriginOutsideOrderIsNoMove() {
        let order: [Leaf] = [.outline, .citations, .bibliography]
        XCTAssertNil(Leaf.railMove(from: .tasks, .down, in: order))
        XCTAssertNil(Leaf.railMove(from: .backlinks, .up, in: order))
    }

    /// The mapping is defined over an arbitrary order, so U4-2 growing the
    /// registry can't silently break clamping semantics.
    func testRailMoveOverExplicitOrder() {
        let order: [Leaf] = [.outline, .backlinks, .tasks]
        XCTAssertEqual(Leaf.railMove(from: .outline, .down, in: order), .backlinks)
        XCTAssertEqual(Leaf.railMove(from: .backlinks, .down, in: order), .tasks)
        XCTAssertNil(Leaf.railMove(from: .tasks, .down, in: order))
        XCTAssertEqual(Leaf.railMove(from: .tasks, .up, in: order), .backlinks)
    }

    // MARK: - Leaf registry & metadata

    /// U4-2/#709 register all leaves in the rail/registry order (outline first
    /// — most used, matches the old default tab): the three detail tabs U4-1
    /// seeded plus the seven ported out of the retired sidebar stack.
    func testAllTwelveLeavesRegisteredInRailOrder() {
        XCTAssertEqual(
            Leaf.registered,
            [
                .outline, .backlinks, .outgoingLinks, .embeds, .math, .code, .diagrams,
                .tasks, .citations, .bibliography, .queries, .basesDock, .syncDiagnostics,
            ])
        // Every case is now registered — no leaf presents a selectable-but-
        // blank rail icon.
        for leaf in Leaf.allCases {
            XCTAssertTrue(leaf.isRegistered, "\(leaf) must be registered as of U4-2")
        }
        XCTAssertEqual(Leaf.allCases.count, 13, "the full leaf vocabulary is declared")
        // The registry is exactly the case set (no duplicates, none missing).
        XCTAssertEqual(Set(Leaf.registered), Set(Leaf.allCases))
        XCTAssertEqual(Leaf.registered.count, Leaf.allCases.count)
        // M-3 (#534): sync diagnostics is registered LAST — vault-level
        // diagnostics, least-frequently visited (m_spec §M-3).
        XCTAssertEqual(Leaf.registered.last, .syncDiagnostics)
    }

    /// Every leaf has a non-empty title (the rail label / help / announcement)
    /// and its intended semantic symbol role (u4_spec SlateSymbol table). The
    /// tasks leaf deliberately shares `.tasksReview`'s glyph via `.tasksLeaf`.
    func testLeafTitlesAndSymbols() {
        for leaf in Leaf.allCases {
            XCTAssertFalse(leaf.title.isEmpty, "\(leaf) has no title")
        }
        XCTAssertEqual(Leaf.outline.symbol, .outline)
        XCTAssertEqual(Leaf.backlinks.symbol, .backlinks)
        XCTAssertEqual(Leaf.outgoingLinks.symbol, .outgoingLinks)
        XCTAssertEqual(Leaf.embeds.symbol, .embed)
        XCTAssertEqual(Leaf.math.symbol, .math)
        XCTAssertEqual(Leaf.code.symbol, .code)
        XCTAssertEqual(Leaf.diagrams.symbol, .diagram)
        XCTAssertEqual(Leaf.tasks.symbol, .tasksLeaf)
        XCTAssertEqual(Leaf.citations.symbol, .citationSummary)
        XCTAssertEqual(Leaf.bibliography.symbol, .bibliography)
        XCTAssertEqual(Leaf.queries.symbol, .base)
        XCTAssertEqual(Leaf.queries.title, "Queries")
        XCTAssertEqual(Leaf.basesDock.symbol, .base)
        XCTAssertEqual(Leaf.basesDock.title, "Base dock")
        XCTAssertEqual(Leaf.syncDiagnostics.symbol, .syncDiagnostics)
        XCTAssertEqual(Leaf.syncDiagnostics.title, "Sync")
    }

    /// N4-3 (#709): saved queries get a first-class right-pane leaf, after the
    /// file-centric leaves and before vault-level sync diagnostics.
    func testQueriesLeafIsRegisteredAndMounted() throws {
        XCTAssertEqual(Leaf.queries.title, "Queries")
        XCTAssertEqual(Leaf.queries.symbol, .base)
        XCTAssertTrue(Leaf.queries.isRegistered)
        XCTAssertEqual(Leaf.railMove(from: .bibliography, .down), .queries)
        XCTAssertEqual(Leaf.railMove(from: .queries, .down), .basesDock)
        XCTAssertEqual(Leaf.railMove(from: .basesDock, .down), .syncDiagnostics)
        XCTAssertEqual(Leaf.railMove(from: .syncDiagnostics, .up), .basesDock)

        let source = try Self.sourceFile("Sources/SlateMac/Workspace/RightPaneView.swift")
        XCTAssertTrue(source.contains("case .queries"))
        XCTAssertTrue(source.contains("BaseQueriesPanel()"))
    }

    /// N4-4 (#710): the docked Bases leaf is distinct from the Queries list
    /// leaf. It hosts follow-active `.base`, saved-query, and dashboard grids.
    func testBasesDockLeafIsRegisteredAndMounted() throws {
        XCTAssertEqual(Leaf.basesDock.title, "Base dock")
        XCTAssertEqual(Leaf.basesDock.symbol, .base)
        XCTAssertTrue(Leaf.basesDock.isRegistered)
        XCTAssertEqual(Leaf.railMove(from: .queries, .down), .basesDock)
        XCTAssertEqual(Leaf.railMove(from: .basesDock, .down), .syncDiagnostics)
        XCTAssertEqual(Leaf.railMove(from: .syncDiagnostics, .up), .basesDock)

        let source = try Self.sourceFile("Sources/SlateMac/Workspace/RightPaneView.swift")
        XCTAssertTrue(source.contains("case .basesDock"))
        XCTAssertTrue(source.contains("BasesDockPanel()"))
    }

    func testBaseRowMembershipUsesStableIdentityMultisetSemantics() {
        let alpha = BasesRow(
            filePath: "Notes/Alpha.md",
            taskOrdinal: nil,
            values: [],
            audioDescription: "Alpha")
        let task = BasesRow(
            filePath: "Notes/Tasks.md",
            taskOrdinal: 2,
            values: [],
            audioDescription: "Task")

        XCTAssertEqual(
            BaseRowMembership(rows: [alpha, task, alpha]),
            BaseRowMembership(rows: [alpha, alpha, task]),
            "reordering alone is not a membership change")
        XCTAssertNotEqual(
            BaseRowMembership(rows: [alpha, task, alpha]),
            BaseRowMembership(rows: [alpha, task]),
            "duplicate row identities are counted, not collapsed like a set")
        XCTAssertNotEqual(
            BaseRowMembership(rows: []),
            BaseRowMembership(rows: [alpha]),
            "empty-to-nonempty is a membership change")
        XCTAssertNotEqual(
            BaseRowMembership(rows: [task]),
            BaseRowMembership(rows: []),
            "nonempty-to-empty is a membership change")
    }

    // MARK: - Persistence

    /// Unknown / absent tokens fall back to `.outline`; every known token
    /// round-trips now that all leaves are registered.
    func testLeafPersistedInitFallback() {
        XCTAssertEqual(Leaf(persisted: nil), .outline)
        XCTAssertEqual(Leaf(persisted: "not-a-leaf"), .outline)
        // Every valid rawValue is registered, so each round-trips to itself.
        XCTAssertEqual(Leaf(persisted: "outline"), .outline)
        XCTAssertEqual(Leaf(persisted: "backlinks"), .backlinks)
        XCTAssertEqual(Leaf(persisted: "outgoingLinks"), .outgoingLinks)
        XCTAssertEqual(Leaf(persisted: "embeds"), .embeds)
        XCTAssertEqual(Leaf(persisted: "math"), .math)
        XCTAssertEqual(Leaf(persisted: "code"), .code)
        XCTAssertEqual(Leaf(persisted: "diagrams"), .diagrams)
        XCTAssertEqual(Leaf(persisted: "tasks"), .tasks)
        XCTAssertEqual(Leaf(persisted: "citations"), .citations)
        XCTAssertEqual(Leaf(persisted: "bibliography"), .bibliography)
        XCTAssertEqual(Leaf(persisted: "queries"), .queries)
        XCTAssertEqual(Leaf(persisted: "basesDock"), .basesDock)
        // M-3 (#534): the new leaf round-trips; older builds decode the
        // token to `.outline` by the same unknown-token fallback above.
        XCTAssertEqual(Leaf(persisted: "syncDiagnostics"), .syncDiagnostics)
    }

    /// `activeLeaf` round-trips through the `WorkspaceStore` snapshot schema.
    func testActiveLeafSnapshotRoundTrip() {
        let model = WorkspaceModel()
        let snapshot = WorkspaceStore.snapshot(
            of: model, activeLeaf: Leaf.bibliography.rawValue)
        XCTAssertEqual(snapshot.activeLeaf, "bibliography")
        XCTAssertEqual(Leaf(persisted: snapshot.activeLeaf), .bibliography)
    }

    /// Absent `activeLeaf` in the snapshot restores `.outline` (backward compat
    /// with pre-U4 workspace.json files, which have no such key).
    func testAbsentActiveLeafRestoresOutline() throws {
        let group = UUID()
        let json = """
            {"version": 1, "activeGroup": "\(group.uuidString)",
             "root": {"kind": "group", "id": "\(group.uuidString)",
                      "activeTab": null, "tabs": []}}
            """
        let snapshot = try JSONDecoder().decode(
            WorkspaceStore.Snapshot.self, from: Data(json.utf8))
        XCTAssertNil(snapshot.activeLeaf)
        XCTAssertEqual(Leaf(persisted: snapshot.activeLeaf), .outline)
    }

    /// End-to-end: a leaf chosen in one session is restored on vault reopen.
    func testActiveLeafPersistsAcrossVaultReopen() async throws {
        let vault = tempDir.appendingPathComponent("vault")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        try "# note\n".write(
            to: vault.appendingPathComponent("note.md"), atomically: true, encoding: .utf8)
        let recents = RecentVaultsStore(
            fileURL: tempDir.appendingPathComponent("recents.json"))

        let first = AppState(recentsStore: recents, externalOpener: { _ in true })
        first.openVault(at: vault)
        await first.scanTask?.value
        XCTAssertEqual(first.workspace.activeLeaf, .outline, "fresh vault opens on Outline")
        first.workspace.activeLeaf = .bibliography
        first.saveWorkspaceLayout()  // deterministic write (bypass the debounce)
        first.closeVault()

        let second = AppState(recentsStore: recents, externalOpener: { _ in true })
        second.openVault(at: vault)
        await second.scanTask?.value
        XCTAssertEqual(
            second.workspace.activeLeaf, .bibliography,
            "the chosen leaf is restored from workspace.json")
    }

    /// Opening a different vault resets the leaf to the default rather than
    /// leaking the previous vault's choice (before that vault's own snapshot
    /// loads).
    func testVaultSwitchResetsLeafToDefault() async throws {
        let vaultA = tempDir.appendingPathComponent("A")
        let vaultB = tempDir.appendingPathComponent("B")
        for v in [vaultA, vaultB] {
            try FileManager.default.createDirectory(at: v, withIntermediateDirectories: true)
            try "# n\n".write(
                to: v.appendingPathComponent("n.md"), atomically: true, encoding: .utf8)
        }
        let recents = RecentVaultsStore(
            fileURL: tempDir.appendingPathComponent("recents2.json"))
        let state = AppState(recentsStore: recents, externalOpener: { _ in true })

        state.openVault(at: vaultA)
        await state.scanTask?.value
        state.workspace.activeLeaf = .citations
        state.saveWorkspaceLayout()

        // B has never stored a leaf → it must open on the default, not A's.
        state.openVault(at: vaultB)
        await state.scanTask?.value
        XCTAssertEqual(state.workspace.activeLeaf, .outline)
    }

    // MARK: - Retention (mechanism + load-fire spy)

    /// The load-fire spy: a Bibliography leaf kept mounted across leaf switches
    /// must not re-fetch. Toggling `activeLeaf` (what the rail does) is a pure
    /// view-state change — the fetch fired once on the initial load and the
    /// mounted-ZStack keeps the panel (and its `hasLoaded`) alive, so the count
    /// never climbs. A `switch`-over-active-leaf host would re-mount the panel
    /// and re-fire the load; this test fails in that world.
    func testLeafSwitchDoesNotRefetchBibliography() async throws {
        let vault = tempDir.appendingPathComponent("vault")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        try "# note\n".write(
            to: vault.appendingPathComponent("note.md"), atomically: true, encoding: .utf8)
        let recents = RecentVaultsStore(
            fileURL: tempDir.appendingPathComponent("recents.json"))
        let state = AppState(recentsStore: recents, externalOpener: { _ in true })
        state.openVault(at: vault)
        await state.scanTask?.value

        // Simulate the panel's one-shot load (BibliographyPanel.onAppear does
        // this behind its `hasLoaded` guard).
        await state.loadBibliographyEntries()
        let afterFirstLoad = state.bibliographyLoadCount
        XCTAssertEqual(afterFirstLoad, 1, "the Bibliography leaf loads exactly once")

        // Switch outline → bibliography → outline. The mounted panel is never
        // torn down, so no reload fires.
        state.workspace.activeLeaf = .bibliography
        state.workspace.activeLeaf = .outline
        state.workspace.activeLeaf = .bibliography
        XCTAssertEqual(
            state.bibliographyLoadCount, afterFirstLoad,
            "leaf switching a mounted panel must not re-fetch (retention holds)")
    }

    /// Structural pin of the retention MECHANISM: `RightPaneView` mounts every
    /// registered leaf in a `ZStack` gated by opacity + allowsHitTesting +
    /// accessibilityHidden — never a `switch` that would rebuild panels. If a
    /// refactor swaps the ZStack for a switch (silently regressing
    /// BibliographyPanel.segment / hasLoaded / re-fired IO), this fails.
    func testRightPaneSourceUsesMountedZStackRetention() throws {
        let source = try rightPaneSource()
        XCTAssertTrue(
            source.contains("ZStack"), "the leaf host must mount leaves in a ZStack")
        XCTAssertTrue(
            source.contains("ForEach(Leaf.registered)"),
            "the host iterates the registered leaves, not a hardcoded set")
        for gate in [".opacity(", ".allowsHitTesting(", ".accessibilityHidden("] {
            XCTAssertTrue(
                source.contains(gate),
                "retention gate \(gate) missing — hidden leaves would leak into AX/pointer")
        }
    }

    /// Only the visible leaf is in the AX/pointer tree: hidden leaves are gated
    /// out with `accessibilityHidden(activeLeaf != leaf)` +
    /// `allowsHitTesting(activeLeaf == leaf)`. No XCTest surface reads the
    /// rendered AX tree, so this pins the source expression that produces it —
    /// the same technique the panel-stack wiring tests use.
    func testHiddenLeavesAreExcludedFromAXTree() throws {
        let source = try rightPaneSource()
        XCTAssertTrue(
            source.contains("accessibilityHidden(workspace.activeLeaf != leaf)"),
            "hidden leaves must be accessibilityHidden(true)")
        XCTAssertTrue(
            source.contains("allowsHitTesting(workspace.activeLeaf == leaf)"),
            "hidden leaves must not take pointer hits")
    }

    // MARK: - Rail AX (structural)

    /// The rail container is one AX element labeled "Panel rail" with the
    /// choose-a-panel hint, and each item carries `.isSelected` when active —
    /// the radio-group semantics VoiceOver announces. Pinned structurally
    /// (no rendered-AX-tree API); the VoiceOver runbook covers the behaviour.
    func testRailCarriesContainerAndSelectionSemantics() throws {
        let source = try rightPaneSource()
        XCTAssertTrue(source.contains("accessibilityElement(children: .contain)"))
        XCTAssertTrue(source.contains(#"accessibilityLabel("Panel rail")"#))
        XCTAssertTrue(source.contains(#"accessibilityHint("Choose which panel is shown")"#))
        XCTAssertTrue(
            source.contains(".isSelected"),
            "the active rail item must carry the .isSelected trait")
        // Items are labeled glyphs (image(label:)), never bare icons.
        XCTAssertTrue(source.contains("image(label: leaf.title)"))
        // One focus stop + arrow-within (onMoveCommand) + activate.
        XCTAssertTrue(source.contains(".focusable()"))
        XCTAssertTrue(source.contains(".onMoveCommand"))
    }

    /// The leaf-switch announcement matches the spec phrasing ("<title>
    /// panel.") — the medium-priority live region that replaces the picker's
    /// native announcement.
    func testLeafSwitchAnnouncementPhrasing() throws {
        let source = try rightPaneSource()
        XCTAssertTrue(
            source.contains(#"postAccessibilityAnnouncement("\(leaf.title) panel.""#),
            "leaf switch must announce '<title> panel.'")
    }

    // MARK: - PresentationReady (§D contrast + §E render, both appearances)

    /// The rail's text/glyph states clear the project APCA floor in both
    /// appearances: the active item's `accentText` and the rest item's
    /// `textSecondary`, both on the rail's `surface` background. (The selection
    /// bar is `accentFill`, a non-text shape cue exempt from the text floor —
    /// its own contrast is covered by the tokens' `onAccentFill` pairing.)
    func testRailSelectedAndRestStatesClearContrastFloor() {
        PresentationReady.assertContrastFloor([
            ("rail selected (accentText on surface)", .tokenAccentText, .tokenSurface),
            ("rail rest (textSecondary on surface)", .tokenTextSecondary, .tokenSurface),
        ])
    }

    /// The right pane renders to a finite, non-empty size in both appearances
    /// — a smoke test over the real view (rail + a mounted leaf) that catches
    /// per-appearance crashes / failed renders.
    func testRightPaneRendersInBothAppearances() {
        let state = AppState(
            recentsStore: RecentVaultsStore(
                fileURL: tempDir.appendingPathComponent("recents3.json")),
            externalOpener: { _ in true })
        let view = RightPaneView(workspace: state.workspace)
            .environmentObject(state)
        PresentationReady.assertRendersInBothAppearances(view)
    }

    // MARK: - internals

    /// Locate and read `RightPaneView.swift` from the test file's path (the
    /// module doesn't ship its own source; walk up like the sidebar-wiring
    /// tests do).
    private func rightPaneSource() throws -> String {
        var cursor = URL(fileURLWithPath: #filePath).deletingLastPathComponent()
        for _ in 0..<8 {
            let candidate = cursor.appendingPathComponent(
                "Sources/SlateMac/Workspace/RightPaneView.swift")
            if FileManager.default.fileExists(atPath: candidate.path) {
                return try String(contentsOf: candidate, encoding: .utf8)
            }
            cursor = cursor.deletingLastPathComponent()
        }
        XCTFail("Could not locate RightPaneView.swift from \(#filePath)")
        return ""
    }
}
