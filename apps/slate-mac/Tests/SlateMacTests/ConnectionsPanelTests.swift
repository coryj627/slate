// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// The Connections panel's host side (P1-1 #554; W6-2 PR 0b): the tree's
/// derivation is core's now (`graph_connections_tree`, contracts doc
/// 0b-4) and its facts live in `crates/slate-core/src/graph_queries.rs`.
/// What remains here is the adapter — the nesting of core's flat rows
/// by `parent_id`, the snippet overlay by path — and the busy-reason
/// admission overlay (0bD-8). Pure logic — no session required.
final class ConnectionsPanelTests: XCTestCase {
    private func row(
        _ id: String, level: UInt32, parent: String?, nodeId: UInt64, _ label: String,
        path: String?, kind: GraphNodeKind = .note, embedOnly: Bool = false,
        inLinks: UInt32 = 0, outLinks: UInt32 = 0, references: UInt32 = 0
    ) -> GraphConnectionRow {
        GraphConnectionRow(
            id: id, level: level, parentId: parent, nodeId: nodeId,
            stableKey: path.map { "p:\($0)" } ?? "g:\(label.lowercased())",
            label: label, path: path, targetRaw: label, kind: kind, embedOnly: embedOnly,
            inLinks: inLinks, outLinks: outLinks, references: references)
    }

    private func tree(incoming: [GraphConnectionRow], outgoing: [GraphConnectionRow], depth: UInt32 = 1)
        -> GraphConnectionsTree
    {
        GraphConnectionsTree(
            generation: 1, centerId: 1, centerKey: "p:center.md", depth: depth,
            summaryCounts: GraphNeighborhoodCounts(
                centerLabel: "Center", inLinks: UInt32(incoming.count), outLinks: UInt32(outgoing.count),
                noteCount: 0, depth: depth),
            incoming: incoming, outgoing: outgoing)
    }

    /// The clamp reads core's constants (0b-8).
    func testDepthClampIntoOneToThree() {
        XCTAssertEqual(AppState.clampConnectionsDepth(0), 1)
        XCTAssertEqual(AppState.clampConnectionsDepth(1), 1)
        XCTAssertEqual(AppState.clampConnectionsDepth(3), 3)
        XCTAssertEqual(AppState.clampConnectionsDepth(99), 3)
        // Any Int: the conversion saturates instead of trapping (IPB-11).
        XCTAssertEqual(AppState.clampConnectionsDepth(Int.min), 1)
        XCTAssertEqual(AppState.clampConnectionsDepth(Int.max), 3)
        XCTAssertEqual(Int(graphConstants().connectionsDepthMax), 3)
    }

    /// The ghost path is core's (0b-11); the host wrapper forwards.
    func testGhostNotePathIsCores() {
        XCTAssertEqual(AppState.ghostNotePath("Missing Note"), "Missing Note.md")
        XCTAssertEqual(AppState.ghostNotePath("./notes/Foo"), "notes/Foo.md")
        XCTAssertEqual(AppState.ghostNotePath("Foo.md"), "Foo.md")
        XCTAssertEqual(AppState.ghostNotePath("dir/"), graphGhostNotePath(targetRaw: "dir/"))
    }

    @MainActor
    func testBusyReasonAppliesOnlyToGhostCreationControls() {
        let reason = AppState.structuralMutationBusyReason
        XCTAssertEqual(
            ConnectionsPanel.creationDisabledReason(
                forGhost: true, structuralReason: reason),
            reason)
        XCTAssertNil(
            ConnectionsPanel.creationDisabledReason(
                forGhost: false, structuralReason: reason))
        XCTAssertEqual(
            ConnectionsPanel.actionDisabledReason(
                .createNote, structuralReason: reason),
            reason)
        XCTAssertNil(
            ConnectionsPanel.actionDisabledReason(
                .open, structuralReason: reason),
            "unrelated graph context actions stay available")
        XCTAssertNil(
            ConnectionsPanel.actionDisabledReason(
                .createNote, structuralReason: nil),
            "idle ghost creation stays available")
    }

    /// The adapter keeps core's lists and order, carries the kind, the
    /// embed flag and the counts, and derives nothing.
    func testAdapterCarriesCoresRowsInOrder() {
        let t = tree(
            incoming: [row("in/p%3Ain%2Emd", level: 1, parent: nil, nodeId: 2, "Inbound", path: "in.md", outLinks: 1)],
            outgoing: [
                row("out/g%3Amissing", level: 1, parent: nil, nodeId: 4, "Missing", path: nil, kind: .ghost, references: 1),
                row("out/p%3Aout%2Emd", level: 1, parent: nil, nodeId: 3, "Outbound", path: "out.md", inLinks: 1),
                row("out/p%3Apic%2Epng", level: 1, parent: nil, nodeId: 5, "pic.png", path: "pic.png", kind: .attachment, embedOnly: true, references: 1),
            ])
        let model = ConnectionsModel(tree: t, bundle: nil)
        XCTAssertEqual(model.incoming.map(\.label), ["Inbound"])
        XCTAssertEqual(model.outgoing.map(\.label), ["Missing", "Outbound", "pic.png"], "core's order, untouched")
        let ghostRow = model.outgoing.first { $0.label == "Missing" }!
        XCTAssertTrue(ghostRow.isGhost)
        XCTAssertNil(ghostRow.path)
        XCTAssertEqual(ghostRow.references, 1)
        XCTAssertEqual(ghostRow.stableKey, "g:missing")
        let picRow = model.outgoing.first { $0.label == "pic.png" }!
        XCTAssertTrue(picRow.isEmbed, "core's embed_only → the Embed badge")
        XCTAssertTrue(picRow.isAttachment)
        let inRow = model.incoming[0]
        XCTAssertEqual(inRow.rowCopy.inLinks, 0)
        XCTAssertEqual(inRow.rowCopy.outLinks, 1)
        XCTAssertFalse(inRow.isGhost)
        XCTAssertEqual(inRow.id, "in/p%3Ain%2Emd", "the occurrence id is core's")
        XCTAssertEqual(model.row(id: "out/g%3Amissing")?.label, "Missing")
        XCTAssertEqual(t.summaryCounts.inLinks, 1, "the leaf's summary rides on the tree (design A)")
    }

    /// Flat pre-order rows nest by `parent_id` in one pass: a level-2 row
    /// lands under its parent, a diamond descendant under each parent.
    func testAdapterNestsFlatRowsByParentId() {
        let t = tree(
            incoming: [],
            outgoing: [
                row("out/p%3Aalpha%2Emd", level: 1, parent: nil, nodeId: 3, "Alpha", path: "alpha.md"),
                row("out/p%3Aalpha%2Emd/p%3Ashared%2Emd", level: 2, parent: "out/p%3Aalpha%2Emd", nodeId: 4, "Shared", path: "shared.md"),
                row("out/p%3Abeta%2Emd", level: 1, parent: nil, nodeId: 2, "beta", path: "beta.md"),
                row("out/p%3Abeta%2Emd/p%3Ashared%2Emd", level: 2, parent: "out/p%3Abeta%2Emd", nodeId: 4, "Shared", path: "shared.md"),
                row("out/p%3Abeta%2Emd/p%3Ashared%2Emd/p%3Adeep%2Emd", level: 3, parent: "out/p%3Abeta%2Emd/p%3Ashared%2Emd", nodeId: 5, "Deep", path: "deep.md"),
            ], depth: 3)
        let model = ConnectionsModel(tree: t, bundle: nil)
        XCTAssertEqual(model.outgoing.map(\.label), ["Alpha", "beta"])
        XCTAssertEqual(model.outgoing[0].nested.map(\.label), ["Shared"])
        XCTAssertEqual(model.outgoing[1].nested.map(\.label), ["Shared"])
        XCTAssertEqual(model.outgoing[1].nested[0].nested.map(\.label), ["Deep"], "the third hop nests under the second")
        XCTAssertTrue(model.outgoing[0].nested[0].nested.isEmpty)
        XCTAssertNotEqual(model.outgoing[0].nested[0].id, model.outgoing[1].nested[0].id, "a diamond descendant is two occurrences")
        XCTAssertEqual(model.outgoing[0].nested[0].nodeId, model.outgoing[1].nested[0].nodeId, "of one node")
    }

    /// The depth-one snippet overlay reads the bundle by path (0bD-2);
    /// deeper rows carry none.
    func testSnippetOverlayFromBundleAtDepthOne() {
        let t = tree(
            incoming: [],
            outgoing: [
                row("out/p%3Aout%2Emd", level: 1, parent: nil, nodeId: 3, "Outbound", path: "out.md"),
                row("out/p%3Aout%2Emd/p%3Afar%2Emd", level: 2, parent: "out/p%3Aout%2Emd", nodeId: 4, "Far", path: "far.md"),
            ], depth: 2)
        let bundle = NoteLoadBundle(
            backlinks: BacklinkPage(items: [], nextCursor: nil, totalFiltered: 0),
            outgoingLinks: [
                OutgoingLink(
                    targetPath: "out.md", targetRaw: "Outbound", targetAnchor: nil,
                    kind: "wikilink", isEmbed: false, isExternal: false, isUnresolved: false,
                    snippet: "…the Outbound note…", ordinal: 0, spanStart: 0, spanEnd: 0, displayText: nil),
                OutgoingLink(
                    targetPath: "far.md", targetRaw: "Far", targetAnchor: nil,
                    kind: "wikilink", isEmbed: false, isExternal: false, isUnresolved: false,
                    snippet: "…never shown at level two…", ordinal: 1, spanStart: 0, spanEnd: 0, displayText: nil),
            ],
            properties: [])
        let model = ConnectionsModel(tree: t, bundle: bundle)
        XCTAssertEqual(model.outgoing.first?.snippet, "…the Outbound note…")
        XCTAssertNil(model.outgoing.first?.nested.first?.snippet)
    }
}
