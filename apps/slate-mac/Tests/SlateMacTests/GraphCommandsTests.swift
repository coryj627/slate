// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// P1-3 (#556): graph commands + presets. The commands register under
/// `CommandSection.graph` with zero new chords; the presets parameterize
/// the graph table (backend filter + client kind filter) and announce a
/// headline computed from the fresh snapshot.
final class GraphCommandsTests: XCTestCase {

    /// The seven graph commands live in `CommandSection.graph` and none
    /// registers a global chord — P1's zero-new-chords rule (mirrors
    /// Bases' `…RegisterInBasesSectionWithoutGlobalChords`).
    @MainActor
    func testGraphCommandsRegisterInGraphSectionWithoutGlobalChords() {
        let appState = AppState()
        let byID = Dictionary(
            uniqueKeysWithValues: appState.commandRegistry.list().map { ($0.id, $0) })
        let graphIDs = [
            "slate.graph.openTab",
            "slate.graph.showConnections",
            "slate.graph.connectionsDeeper",
            "slate.graph.connectionsShallower",
            "slate.graph.orphans",
            "slate.graph.unresolved",
            "slate.graph.mostLinked",
        ]
        for id in graphIDs {
            let command = byID[id]
            XCTAssertNotNil(command, "\(id) is not registered")
            XCTAssertEqual(command?.section, .graph, "\(id) must be in the graph section")
            XCTAssertNil(command?.hotkeyHint, "\(id) must register no chord (P1 rule R1)")
        }
    }

    /// Preset → backend `GraphFilter` + client kind filter (P1-3 table).
    func testPresetFilterAndKindMapping() {
        XCTAssertEqual(
            AppState.graphPresetFilter(.orphans),
            GraphFilter(includeAttachments: false, includeGhosts: false, orphansOnly: true))
        XCTAssertNil(AppState.graphPresetKind(.orphans))

        XCTAssertEqual(
            AppState.graphPresetFilter(.unresolved),
            GraphFilter(includeAttachments: false, includeGhosts: true, orphansOnly: false))
        XCTAssertEqual(AppState.graphPresetKind(.unresolved), .ghost, "unresolved shows only ghosts")

        // Most-linked is the DEFAULT view: ghosts visible, attachments
        // off, no orphans-only, no kind narrowing (the hubs surface via
        // the grid's default Links-in-desc sort, not a filter).
        XCTAssertEqual(
            AppState.graphPresetFilter(.mostLinked),
            GraphFilter(includeAttachments: false, includeGhosts: true, orphansOnly: false))
        XCTAssertNil(AppState.graphPresetKind(.mostLinked))
    }

    private func node(
        _ id: UInt64, _ label: String, kind: GraphNodeKind, inLinks: UInt32 = 0
    ) -> GraphNode {
        GraphNode(
            id: id, path: kind == .ghost ? nil : "\(label).md", label: label, kind: kind,
            inLinks: inLinks, outLinks: 0, inEmbeds: 0, outEmbeds: 0, component: 0,
            isOrphan: false, pagerank: 0, modifiedMs: nil)
    }

    private func snapshot(_ nodes: [GraphNode]) -> GraphSnapshot {
        GraphSnapshot(nodes: nodes, edges: [], generation: 1, audioSummary: "")
    }

    /// Announcement copy is normative (P1-3). Orphans/unresolved report a
    /// count; most-linked names the top row under the default sort.
    @MainActor
    func testPresetAnnouncementStringsAreVerbatim() {
        let appState = AppState()

        // Orphans: the snapshot IS the orphan set (backend filtered), so
        // the count is every node; the string stays plural regardless.
        let orphanSnap = snapshot([
            node(1, "a", kind: .note), node(2, "b", kind: .note), node(3, "c", kind: .note),
        ])
        XCTAssertEqual(
            appState.graphPresetAnnouncement(.orphans, snap: orphanSnap), "3 orphaned notes.")

        // Unresolved: counts only ghost rows (the client kind filter).
        let mixedSnap = snapshot([
            node(1, "a", kind: .note),
            node(2, "Missing", kind: .ghost),
            node(3, "Also Missing", kind: .ghost),
        ])
        XCTAssertEqual(
            appState.graphPresetAnnouncement(.unresolved, snap: mixedSnap),
            "2 unresolved targets.")

        // Most linked: the top row under Links-in-desc (label tie-break).
        let hubSnap = snapshot([
            node(1, "low", kind: .note, inLinks: 1),
            node(2, "hub", kind: .note, inLinks: 9),
            node(3, "mid", kind: .note, inLinks: 4),
        ])
        XCTAssertEqual(
            appState.graphPresetAnnouncement(.mostLinked, snap: hubSnap),
            "Most linked: hub, 9 links in.")
    }

    /// Empty graph: most-linked degrades gracefully rather than crashing.
    @MainActor
    func testMostLinkedOnEmptyGraph() {
        let appState = AppState()
        XCTAssertEqual(
            appState.graphPresetAnnouncement(.mostLinked, snap: snapshot([])),
            "No notes to rank.")
    }
}
