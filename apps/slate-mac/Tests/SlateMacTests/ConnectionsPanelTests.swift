// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// ConnectionsPanel model derivation + AppState helpers (P1-1 #554).
/// Pure logic — no session required.
final class ConnectionsPanelTests: XCTestCase {
    private func node(
        _ id: UInt64, _ label: String, path: String?, kind: GraphNodeKind,
        inLinks: UInt32 = 0, outLinks: UInt32 = 0, inEmbeds: UInt32 = 0
    ) -> GraphNode {
        GraphNode(
            id: id, path: path, label: label, kind: kind,
            inLinks: inLinks, outLinks: outLinks, inEmbeds: inEmbeds, outEmbeds: 0,
            component: 0, isOrphan: false, pagerank: 0, modifiedMs: nil)
    }

    func testDepthClampIntoOneToThree() {
        XCTAssertEqual(AppState.clampConnectionsDepth(0), 1)
        XCTAssertEqual(AppState.clampConnectionsDepth(1), 1)
        XCTAssertEqual(AppState.clampConnectionsDepth(3), 3)
        XCTAssertEqual(AppState.clampConnectionsDepth(99), 3)
    }

    func testGhostNotePathHonorsFolderAndExtension() {
        XCTAssertEqual(AppState.ghostNotePath("Missing Note"), "Missing Note.md")
        XCTAssertEqual(AppState.ghostNotePath("  Missing Note  "), "Missing Note.md")
        XCTAssertEqual(AppState.ghostNotePath("notes/Foo"), "notes/Foo.md")
        XCTAssertEqual(AppState.ghostNotePath("./notes/Foo"), "notes/Foo.md")
        XCTAssertEqual(AppState.ghostNotePath("/Foo"), "Foo.md")
        // An already-markdown target keeps its extension.
        XCTAssertEqual(AppState.ghostNotePath("Foo.md"), "Foo.md")
    }

    /// Depth-1 in/out split: incoming = edges into the center, outgoing
    /// = edges from it. Ghost target → unresolved row; embed edge → embed
    /// badge; snippets overlaid from the bundle by path.
    func testModelSplitsIncomingOutgoingWithGhostsAndEmbeds() {
        let center = node(1, "Center", path: "center.md", kind: .note, inLinks: 1, outLinks: 2)
        let inbound = node(2, "Inbound", path: "in.md", kind: .note, inLinks: 0, outLinks: 1)
        let outbound = node(3, "Outbound", path: "out.md", kind: .note, inLinks: 1, outLinks: 0)
        let ghost = node(4, "Missing", path: nil, kind: .ghost, inLinks: 1)
        let pic = node(5, "pic.png", path: "pic.png", kind: .attachment, inLinks: 0, inEmbeds: 1)

        let edges = [
            GraphEdge(sourceId: 2, targetId: 1, kind: .link, count: 1),  // Inbound → Center
            GraphEdge(sourceId: 1, targetId: 3, kind: .link, count: 1),  // Center → Outbound
            GraphEdge(sourceId: 1, targetId: 4, kind: .link, count: 1),  // Center → ghost
            GraphEdge(sourceId: 1, targetId: 5, kind: .embed, count: 1),  // Center → pic (embed)
        ]
        let hood = GraphNeighborhood(
            centerId: 1, depth: 1, nodes: [center, inbound, outbound, ghost, pic],
            edges: edges, audioSummary: "irrelevant")

        let model = ConnectionsModel(hood: hood, bundle: nil, depth: 1)

        XCTAssertEqual(model.incoming.map(\.label), ["Inbound"])
        XCTAssertEqual(model.outgoing.map(\.label), ["Missing", "Outbound", "pic.png"])

        let ghostRow = model.outgoing.first { $0.label == "Missing" }!
        XCTAssertTrue(ghostRow.isGhost)
        XCTAssertNil(ghostRow.path)
        XCTAssertEqual(ghostRow.references, 1)

        let picRow = model.outgoing.first { $0.label == "pic.png" }!
        XCTAssertTrue(picRow.isEmbed, "embed edge → embed badge")
        XCTAssertTrue(picRow.isAttachment)

        let inRow = model.incoming[0]
        XCTAssertEqual(inRow.rowRef.linksIn, 0)
        XCTAssertEqual(inRow.rowRef.linksOut, 1)
        XCTAssertFalse(inRow.isGhost)
    }

    func testDepthTwoNestsSecondHopNeighbors() {
        // Center → A → B. At depth 2 the A row nests B.
        let center = node(1, "Center", path: "center.md", kind: .note)
        let a = node(2, "A", path: "a.md", kind: .note)
        let b = node(3, "B", path: "b.md", kind: .note)
        let edges = [
            GraphEdge(sourceId: 1, targetId: 2, kind: .link, count: 1),
            GraphEdge(sourceId: 2, targetId: 3, kind: .link, count: 1),
        ]
        let hood = GraphNeighborhood(
            centerId: 1, depth: 2, nodes: [center, a, b], edges: edges,
            audioSummary: "")
        let model = ConnectionsModel(hood: hood, bundle: nil, depth: 2)
        let aRow = model.outgoing.first { $0.label == "A" }!
        XCTAssertEqual(aRow.nested.map(\.label), ["B"], "second-hop B nests under A")
        // B is not expanded further at depth 2 (no third hop).
        XCTAssertTrue(aRow.nested[0].nested.isEmpty)
    }

    /// Depth 3 renders the third hop: Center → A → B → C, C nests under
    /// B under A (review round 1 finding 8).
    func testDepthThreeRendersThirdHop() {
        let center = node(1, "Center", path: "center.md", kind: .note)
        let a = node(2, "A", path: "a.md", kind: .note)
        let b = node(3, "B", path: "b.md", kind: .note)
        let c = node(4, "C", path: "c.md", kind: .note)
        let edges = [
            GraphEdge(sourceId: 1, targetId: 2, kind: .link, count: 1),
            GraphEdge(sourceId: 2, targetId: 3, kind: .link, count: 1),
            GraphEdge(sourceId: 3, targetId: 4, kind: .link, count: 1),
        ]
        let hood = GraphNeighborhood(
            centerId: 1, depth: 3, nodes: [center, a, b, c], edges: edges, audioSummary: "")
        let model = ConnectionsModel(hood: hood, bundle: nil, depth: 3)
        let aRow = model.outgoing.first { $0.label == "A" }!
        let bRow = aRow.nested.first { $0.label == "B" }!
        XCTAssertEqual(bRow.nested.map(\.label), ["C"], "third-hop C nests under B")
        // Cycle guard: C does not re-expand back to B/A.
        XCTAssertTrue(bRow.nested[0].nested.isEmpty)
    }

    /// A neighbor reached by BOTH a link and an embed edge is ONE row
    /// (not two with a duplicate id), and is not embed-only so it gets
    /// no Embed badge (review round 1 finding 6).
    func testLinkAndEmbedToSameNodeCollapseToOneRow() {
        let center = node(1, "Center", path: "center.md", kind: .note)
        let both = node(2, "Both", path: "both.md", kind: .note)
        let edges = [
            GraphEdge(sourceId: 1, targetId: 2, kind: .link, count: 1),
            GraphEdge(sourceId: 1, targetId: 2, kind: .embed, count: 1),
        ]
        let hood = GraphNeighborhood(
            centerId: 1, depth: 1, nodes: [center, both], edges: edges, audioSummary: "")
        let model = ConnectionsModel(hood: hood, bundle: nil, depth: 1)
        XCTAssertEqual(model.outgoing.count, 1, "one row per neighbor, not per edge")
        XCTAssertFalse(model.outgoing[0].isEmbed, "has a real link → not embed-only")
    }

    /// A self-edge (a note linking to itself) is not a connection to
    /// another note and appears in neither list (review round 1
    /// finding 6).
    func testSelfEdgeOmittedFromBothLists() {
        let center = node(1, "Center", path: "center.md", kind: .note)
        let edges = [GraphEdge(sourceId: 1, targetId: 1, kind: .link, count: 1)]
        let hood = GraphNeighborhood(
            centerId: 1, depth: 1, nodes: [center], edges: edges, audioSummary: "")
        let model = ConnectionsModel(hood: hood, bundle: nil, depth: 1)
        XCTAssertTrue(model.incoming.isEmpty && model.outgoing.isEmpty)
    }

    func testSnippetOverlayFromBundleAtDepthOne() {
        let center = node(1, "Center", path: "center.md", kind: .note)
        let outbound = node(3, "Outbound", path: "out.md", kind: .note)
        let edges = [GraphEdge(sourceId: 1, targetId: 3, kind: .link, count: 1)]
        let hood = GraphNeighborhood(
            centerId: 1, depth: 1, nodes: [center, outbound], edges: edges,
            audioSummary: "")
        let bundle = NoteLoadBundle(
            backlinks: BacklinkPage(items: [], nextCursor: nil, totalFiltered: 0),
            outgoingLinks: [
                OutgoingLink(
                    targetPath: "out.md", targetRaw: "Outbound", targetAnchor: nil,
                    kind: "wikilink", isEmbed: false, isExternal: false, isUnresolved: false,
                    snippet: "…the Outbound note…", ordinal: 0, displayText: nil)
            ],
            properties: [])
        let model = ConnectionsModel(hood: hood, bundle: bundle, depth: 1)
        XCTAssertEqual(model.outgoing.first?.snippet, "…the Outbound note…")
    }
}
