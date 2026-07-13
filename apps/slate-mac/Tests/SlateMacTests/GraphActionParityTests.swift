// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// #561 acceptance (DoD §P-B): the three graph projections (Table rows,
/// Diagram nodes, Connections rows) expose ONE canonical action set, so
/// they can't silently drift; plus the shared cross-projection node key
/// round-trips. The Diagram-side action assertion lives in
/// `GraphDiagramTests` (it needs the NSView harness); this file locks the
/// pure contract every projection derives from.
final class GraphActionParityTests: XCTestCase {
    func testCanonicalActionLabelsAndOrder() {
        // The exact labels + order every projection renders. If a label is
        // reworded, this fails and forces all three projections to agree.
        XCTAssertEqual(
            GraphRowAction.allCases.map(\.title),
            ["Open", "Open in New Tab", "Show connections", "Reveal in File Tree", "Create note"])
    }

    func testAvailabilityByGhostness() {
        // A real note gets the four navigation actions; a ghost gets only
        // "Create note" — no projection ever offers a no-op action.
        XCTAssertEqual(
            GraphRowAction.actions(forGhost: false).map(\.title),
            ["Open", "Open in New Tab", "Show connections", "Reveal in File Tree"])
        XCTAssertEqual(GraphRowAction.actions(forGhost: true).map(\.title), ["Create note"])
    }

    func testTableRowActionPolicyDerivesFromCanonicalSet() {
        // GraphTableView's availability wrapper must agree with the enum for
        // every canonical action (both ghost-ness values).
        for action in GraphRowAction.allCases {
            for isGhost in [true, false] {
                XCTAssertEqual(
                    GraphTableView.rowActionEnabled(action.title, isGhost: isGhost),
                    action.applies(toGhost: isGhost),
                    "\(action.title) ghost=\(isGhost)")
            }
        }
    }

    func testSharedNodeKeyRoundTripsAndSeparatesGhosts() {
        // Real node ⇒ "p:<path>"; ghost ⇒ "g:<percent-encoded folded
        // label>" — two disjoint namespaces, byte-stable (P2-5 #561).
        let note = GraphNode(
            id: 1, path: "Notes/Alpha.md", label: "Alpha", kind: .note, inLinks: 0, outLinks: 0,
            inEmbeds: 0, outEmbeds: 0, component: 0, isOrphan: false, pagerank: 0, modifiedMs: nil)
        XCTAssertEqual(GraphNodeKey.make(for: note), "p:Notes/Alpha.md")

        let ghost = GraphNode(
            id: 2, path: nil, label: "Missing Note", kind: .ghost, inLinks: 0, outLinks: 0,
            inEmbeds: 0, outEmbeds: 0, component: 0, isOrphan: true, pagerank: 0, modifiedMs: nil)
        XCTAssertEqual(GraphNodeKey.make(for: ghost), "g:missing%20note")

        // Case/byte folding: "MISSING NOTE" folds to the SAME ghost key.
        let ghostUpper = GraphNode(
            id: 3, path: nil, label: "MISSING NOTE", kind: .ghost, inLinks: 0, outLinks: 0,
            inEmbeds: 0, outEmbeds: 0, component: 0, isOrphan: true, pagerank: 0, modifiedMs: nil)
        XCTAssertEqual(GraphNodeKey.make(for: ghostUpper), GraphNodeKey.make(for: ghost))

        // A real node and a ghost never collide (disjoint "p:"/"g:").
        XCTAssertNotEqual(GraphNodeKey.make(for: note), GraphNodeKey.make(for: ghost))

        // The Table row id IS the shared key (round-trip through the row).
        XCTAssertEqual(GraphTableRow(node: note, folder: "Notes").id, GraphNodeKey.make(for: note))
        XCTAssertEqual(GraphTableRow(node: ghost, folder: "").id, GraphNodeKey.make(for: ghost))
    }
}
