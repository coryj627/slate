// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// GraphTableRow derivation + column sort determinism (P1-2 #555).
/// Pure logic — no session.
final class GraphTableViewTests: XCTestCase {
    private static func source(_ relativePath: String) throws -> String {
        let root = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .appendingPathComponent("Sources/SlateMac")
        return try String(
            contentsOf: root.appendingPathComponent(relativePath),
            encoding: .utf8)
    }

    private func node(
        _ id: UInt64, _ label: String, path: String?, kind: GraphNodeKind,
        inLinks: UInt32 = 0, outLinks: UInt32 = 0, inEmbeds: UInt32 = 0, outEmbeds: UInt32 = 0,
        component: UInt32 = 0, modifiedMs: Int64? = nil
    ) -> GraphNode {
        GraphNode(
            id: id, path: path, label: label, kind: kind,
            inLinks: inLinks, outLinks: outLinks, inEmbeds: inEmbeds, outEmbeds: outEmbeds,
            component: component, isOrphan: false, pagerank: 0, modifiedMs: modifiedMs)
    }

    func testRowDerivesColumnsFromNode() {
        let n = node(
            1, "Alpha", path: "notes/Alpha.md", kind: .note,
            inLinks: 3, outLinks: 2, inEmbeds: 1, outEmbeds: 0, component: 4, modifiedMs: 0)
        let row = GraphTableRow(node: n, folder: "notes")
        XCTAssertEqual(row.label, "Alpha")
        XCTAssertEqual(row.linksIn, 3)
        XCTAssertEqual(row.linksOut, 2)
        XCTAssertEqual(row.embedsIn, 1)
        XCTAssertEqual(row.component, 4)
        XCTAssertEqual(row.folder, "notes")
        XCTAssertEqual(row.kindLabel, "Note")
        XCTAssertFalse(row.isGhost)
        XCTAssertFalse(row.modifiedText.isEmpty)  // epoch 0 formats to a date
    }

    func testGhostAndAttachmentKindLabels() {
        let ghost = GraphTableRow(node: node(2, "Missing", path: nil, kind: .ghost), folder: "")
        XCTAssertEqual(ghost.kindLabel, "Unresolved")
        XCTAssertTrue(ghost.isGhost)
        XCTAssertEqual(ghost.modifiedText, "", "ghosts have no modified date")
        let pic = GraphTableRow(node: node(3, "pic.png", path: "img/pic.png", kind: .attachment), folder: "img")
        XCTAssertEqual(pic.kindLabel, "Attachment")
    }

    /// The default column set exposes all nine, and the Links-in column
    /// (the default sort) orders descending with a stable label
    /// tie-break (spec §P1-2: hubs first, ties break by key).
    func testColumnsAndLinksInSortDeterminism() {
        let cols = GraphTableColumn.columns
        XCTAssertEqual(cols.count, 9)
        let rows = [
            GraphTableRow(node: node(1, "Beta", path: "b.md", kind: .note, inLinks: 5), folder: ""),
            GraphTableRow(node: node(2, "Alpha", path: "a.md", kind: .note, inLinks: 5), folder: ""),
            GraphTableRow(node: node(3, "Gamma", path: "g.md", kind: .note, inLinks: 9), folder: ""),
        ]
        // Descending (default): 9 first, then the two 5s tie-broken by
        // label (Alpha before Beta).
        let sorted = rows.sorted { GraphTableColumn.linksIn.directionalComparator($0, $1, ascending: false) }
        XCTAssertEqual(sorted.map(\.label), ["Gamma", "Alpha", "Beta"])
        // Ascending flips the numeric order but keeps the label
        // tie-break stable.
        let asc = rows.sorted { GraphTableColumn.linksIn.directionalComparator($0, $1, ascending: true) }
        XCTAssertEqual(asc.map(\.label), ["Alpha", "Beta", "Gamma"])
    }

    func testDefaultSortIsLinksInColumn() {
        // The view seeds sortState to the Links-in column, descending.
        XCTAssertEqual(GraphTableColumn.linksIn.rawValue, 1)
        XCTAssertEqual(GraphTableColumn.allCases.count, 9)
    }

    /// Row identity is a STABLE key in one of two DISJOINT namespaces —
    /// `p:<path>` for real nodes, `g:<folded label>` for ghosts — NOT
    /// the backend node id, which is reassigned on a rebuild and could
    /// otherwise open a different note from a stale selection (round 1
    /// finding 3, round 2 finding 3).
    func testRowIdIsStableNamespacedKeyNotBackendNodeId() {
        let real = GraphTableRow(node: node(42, "Alpha", path: "notes/Alpha.md", kind: .note), folder: "notes")
        XCTAssertEqual(real.id, "p:notes/Alpha.md")
        // Same node, different backend id after a rebuild → same row id.
        let realReassigned = GraphTableRow(
            node: node(999, "Alpha", path: "notes/Alpha.md", kind: .note), folder: "notes")
        XCTAssertEqual(real.id, realReassigned.id)
        // Ghosts key on the percent-encoded, case-folded label under
        // "g:" (round 3 finding 1): ASCII stays legible, spaces encode.
        let ghost = GraphTableRow(node: node(7, "Missing Note", path: nil, kind: .ghost), folder: "")
        XCTAssertEqual(ghost.id, "g:missing%20note")
    }

    /// The two namespaces cannot collide: a real file literally named
    /// "g:X" keys as "p:g:X", never the ghost (round 2 finding 3).
    func testRealAndGhostIdNamespacesAreDisjoint() {
        let sneaky = GraphTableRow(node: node(1, "g:X", path: "g:X", kind: .note), folder: "")
        let ghost = GraphTableRow(node: node(2, "g:X", path: nil, kind: .ghost), folder: "")
        XCTAssertEqual(sneaky.id, "p:g:X")
        XCTAssertEqual(ghost.id, "g:g%3Ax")  // ':' percent-encoded
        XCTAssertNotEqual(sneaky.id, ghost.id)
    }

    /// Unicode normalization variants of the same visible ghost target
    /// are DISTINCT backend nodes; the percent-encoded (byte-keyed) id
    /// keeps them distinct where a plain Swift String would compare them
    /// canonically equal and collapse two rows onto one id (round 3
    /// finding 1).
    func testGhostIdDistinguishesUnicodeNormalizationVariants() {
        let composed = "caf\u{00E9}"  // é as one scalar (NFC)
        let decomposed = "cafe\u{0301}"  // e + combining acute (NFD)
        XCTAssertEqual(composed, decomposed, "Swift String compares these canonically equal")
        let a = GraphTableRow(node: node(1, composed, path: nil, kind: .ghost), folder: "")
        let b = GraphTableRow(node: node(2, decomposed, path: nil, kind: .ghost), folder: "")
        XCTAssertNotEqual(a.id, b.id, "byte-distinct ghost targets must get distinct ids")
    }

    /// Two same-labeled notes with distinct paths must get a total,
    /// deterministic order — the comparator's final tie-break is the
    /// stable id, so neither `<` direction reports them equal (review
    /// round 1 finding 8).
    func testSameLabelDistinctPathIsTotallyOrdered() {
        let a = GraphTableRow(node: node(1, "Note", path: "a/Note.md", kind: .note, inLinks: 2), folder: "a")
        let b = GraphTableRow(node: node(2, "Note", path: "b/Note.md", kind: .note, inLinks: 2), folder: "b")
        let ab = GraphTableColumn.linksIn.directionalComparator(a, b, ascending: false)
        let ba = GraphTableColumn.linksIn.directionalComparator(b, a, ascending: false)
        XCTAssertNotEqual(ab, ba, "distinct same-label rows must not compare equal in both directions")
        // The winner is keyed on the (distinct) id, so it's stable across
        // the primary direction too.
        XCTAssertTrue(ab, "a/Note.md sorts before b/Note.md by stable id")
    }

    /// Every row exposes only the actions that would actually do
    /// something: ghosts get "Create note" alone; real notes get the
    /// four navigation actions and NOT "Create note" (review round 1
    /// finding 4).
    func testRowActionEnablementByKind() {
        let nav = ["Open", "Open in New Tab", "Show connections", "Reveal in File Tree"]
        for name in nav {
            XCTAssertTrue(GraphTableView.rowActionEnabled(name, isGhost: false), "\(name) on a real note")
            XCTAssertFalse(GraphTableView.rowActionEnabled(name, isGhost: true), "\(name) hidden on a ghost")
        }
        XCTAssertTrue(GraphTableView.rowActionEnabled("Create note", isGhost: true))
        XCTAssertFalse(GraphTableView.rowActionEnabled("Create note", isGhost: false))
    }

    @MainActor
    func testBusyGhostCellsExposeExactStructuralCreationReason() {
        let ghost = GraphTableRow(
            node: node(7, "Missing", path: nil, kind: .ghost), folder: "")
        let note = GraphTableRow(
            node: node(8, "Present", path: "Present.md", kind: .note), folder: "")
        let columns = GraphTableColumn.columns(
            ghostCreationDisabledReason: AppState.structuralMutationBusyReason)

        XCTAssertEqual(
            columns.compactMap { $0.accessibilityHint?(ghost) },
            Array(repeating: AppState.structuralMutationBusyReason, count: 9))
        XCTAssertTrue(columns.allSatisfy { $0.accessibilityHint?(note) == nil })
        XCTAssertTrue(
            GraphTableColumn.columns.allSatisfy {
                $0.accessibilityHint?(ghost) == nil
            },
            "idle ghost rows retain their normal activation behavior")
    }

    func testBusyGhostPrimaryAndModifiedActivationUseOneSuppressionPolicy() throws {
        let source = try Self.source("Graph/GraphTableView.swift")
        let compact = source.split(whereSeparator: { $0.isWhitespace }).joined(separator: " ")

        XCTAssertTrue(
            compact.contains("onActivate: { row in activate(row) }"),
            "Return and double-click retain the ordinary activation funnel")
        XCTAssertTrue(
            compact.contains("onActivateModified: { row in activateInNewTab(row) }"),
            "Command-Return and Command-double-click retain the modified funnel")
        XCTAssertTrue(
            compact.contains(
                "private func activate(_ row: GraphTableRow) { activate(row, fileTarget: .currentTab) }"))
        XCTAssertTrue(
            compact.contains(
                "private func activateInNewTab(_ row: GraphTableRow) { activate(row, fileTarget: .newTab) }"))
        XCTAssertTrue(
            compact.contains(
                "if row.isGhost { guard appState.structuralMutationDisabledReason == nil else { return } appState.createNoteFromGhost(targetRaw: row.label) }"),
            "every primary ghost path must stop before invoking the structural funnel while busy")
        XCTAssertTrue(
            compact.contains("else if let path = row.path { appState.openFile(path, target: fileTarget) }"),
            "ordinary and modified note opening remain available")
    }
}
