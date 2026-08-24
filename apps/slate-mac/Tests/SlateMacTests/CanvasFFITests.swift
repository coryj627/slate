// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// #361 acceptance: Swift can drive the full canvas read API over the
/// FFI — open, outline, table, neighbors, where-am-I, placement,
/// overlap — against a real on-disk vault, no mocks. W6-1 PR 0b adds
/// the structural query surface (§W-G rows B–M) the mac host now
/// consumes instead of re-deriving.
final class CanvasFFITests: XCTestCase {
    private func makeVault() throws -> URL {
        let tmp = FileManager.default.temporaryDirectory
            .appendingPathComponent("canvas-ffi-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: tmp, withIntermediateDirectories: true)
        let sample = """
            {"nodes":[
            {"id":"grp","type":"group","x":-20,"y":-20,"width":400,"height":300,"label":"Ideas"},
            {"id":"a","type":"text","text":"# Hello","x":0,"y":0,"width":100,"height":50,"color":"1"},
            {"id":"b","type":"text","text":"World","x":0,"y":100,"width":100,"height":50}
            ],"edges":[
            {"id":"e","fromNode":"a","toNode":"b","label":"links"}
            ]}
            """
        try sample.write(
            to: tmp.appendingPathComponent("t.canvas"), atomically: true, encoding: .utf8)
        return tmp
    }

    func testCanvasReadAPIOverFFI() throws {
        let vault = try makeVault()
        defer { try? FileManager.default.removeItem(at: vault) }
        let session = try VaultSession.openFilesystem(rootPath: vault.path)

        let info = try session.openCanvas(path: "t.canvas")
        XCTAssertFalse(info.degraded)
        XCTAssertEqual(info.nodeCount, 3)
        XCTAssertEqual(info.edgeCount, 1)
        XCTAssertTrue(info.warnings.isEmpty)

        // Outline: reading order (group precedes children), titles per
        // t0 §1.1, N-of-M context, pinned color names.
        let outline = try session.canvasOutline(handle: info.handle)
        XCTAssertEqual(outline.map(\.nodeId), ["grp", "a", "b"])
        XCTAssertEqual(outline.map(\.title), ["Ideas", "Hello", "World"])
        XCTAssertEqual(outline[1].depth, 1)
        XCTAssertEqual(outline[1].groupPath, ["Ideas"])
        XCTAssertEqual(outline[1].ordinalN, 1)
        XCTAssertEqual(outline[1].totalM, 2)
        XCTAssertEqual(outline[1].colorName, "red")

        // Table rows mirror the same derivation.
        let rows = try session.canvasTableRows(handle: info.handle)
        XCTAssertEqual(rows.count, 3)
        XCTAssertEqual(rows[1].kind, "text")

        // Adjacency with direction + label for #518 phrasing.
        let neighbors = try session.canvasNeighbors(handle: info.handle, nodeId: "a")
        XCTAssertEqual(neighbors.count, 1)
        XCTAssertEqual(neighbors[0].otherTitle, "World")
        XCTAssertEqual(neighbors[0].direction, .outgoing)
        XCTAssertEqual(neighbors[0].label, "links")

        // Where am I? (⌃⌘I readback data.)
        let ctx = try session.canvasWhereAmI(handle: info.handle, nodeId: "a")
        XCTAssertEqual(ctx.title, "Hello")
        XCTAssertEqual(ctx.groupPath, ["Ideas"])
        XCTAssertEqual(ctx.outCount, 1)

        // Placement: below the anchor, typed relative description.
        let place = try session.canvasPlaceNew(
            handle: info.handle, anchor: "b", width: 260, height: 140,
            directionHint: nil, exclude: [])
        guard case let .below(anchorTitle) = place.relative else {
            return XCTFail("expected below placement, got \(place.relative)")
        }
        XCTAssertEqual(anchorTitle, "World")

        // Overlap query (cards only; the group frame doesn't block).
        let overlaps = try session.canvasCheckOverlap(
            handle: info.handle,
            rect: CanvasRect(x: 0, y: 0, width: 50, height: 40),
            exclude: ["a"])
        XCTAssertTrue(overlaps.isEmpty)

        // Handle lifecycle.
        session.closeCanvas(handle: info.handle)
        XCTAssertThrowsError(try session.canvasOutline(handle: info.handle))
    }

    /// W6-1 PR 0b: every handle-based structural query the mac host
    /// migrated onto, over the same fixture. The fixture is a group
    /// `Ideas` containing `Hello` (0,0 100×50) and `World` (0,100
    /// 100×50), with one `Hello → World` edge labelled `links`.
    func testCanvasStructuralQueriesOverFFI() throws {
        let vault = try makeVault()
        defer { try? FileManager.default.removeItem(at: vault) }
        let session = try VaultSession.openFilesystem(rootPath: vault.path)
        let info = try session.openCanvas(path: "t.canvas")
        defer { session.closeCanvas(handle: info.handle) }
        let handle = info.handle

        // Containment (0b-8): the tree the outline's depth column is
        // flattened from, id-keyed, in sibling order.
        XCTAssertEqual(try session.canvasParentOf(handle: handle, nodeId: "a"), "grp")
        XCTAssertNil(try session.canvasParentOf(handle: handle, nodeId: "grp"))
        XCTAssertEqual(try session.canvasChildrenOf(handle: handle, groupId: "grp"), ["a", "b"])
        XCTAssertEqual(
            try session.canvasChildrenOf(handle: handle, groupId: "a"), [],
            "a card is childless, not an error")
        XCTAssertThrowsError(try session.canvasParentOf(handle: handle, nodeId: "nope"))

        // Reading-order projection (0b-10): unknown ids drop, duplicates
        // collapse, the order is the canvas's not the caller's.
        XCTAssertEqual(
            try session.canvasOrderNodes(handle: handle, ids: ["b", "nope", "a", "b"]),
            ["a", "b"])
        XCTAssertEqual(try session.canvasOrderNodes(handle: handle, ids: []), [])

        // Trace path (0b-9): hops EXCLUDE the start card, so a dead end
        // is an empty list rather than a one-element one.
        let hops = try session.canvasTracePath(handle: handle, nodeId: "a")
        XCTAssertEqual(hops.map(\.nodeId), ["b"])
        XCTAssertEqual(hops.first?.edgeId, "e")
        XCTAssertEqual(hops.first?.title, "World")
        XCTAssertEqual(hops.first?.label, "links")
        XCTAssertEqual(
            try session.canvasTracePath(handle: handle, nodeId: "b"), [],
            "the incoming edge is not traversable from its target")

        // Filter (0b-13): reading order, four fields, and the empty
        // needle matching EVERYTHING.
        XCTAssertEqual(try session.canvasFilter(handle: handle, query: ""), ["grp", "a", "b"])
        XCTAssertEqual(
            try session.canvasFilter(handle: handle, query: "   "), ["grp", "a", "b"],
            "whitespace trims to empty, which matches everything")
        XCTAssertEqual(try session.canvasFilter(handle: handle, query: "hello"), ["a"])
        XCTAssertEqual(
            try session.canvasFilter(handle: handle, query: "group"), ["grp"],
            "the kind type word is a matched field")
        XCTAssertEqual(
            try session.canvasFilter(handle: handle, query: "IDEAS"), ["grp", "a", "b"],
            "the group's title, then its members' group path")
        XCTAssertEqual(try session.canvasFilter(handle: handle, query: "zzz"), [])

        // Relative description (0b-7): the sense is INVERTED — the
        // phrase says where the SUBJECT sits, so a subject below
        // "World" reads `Below "World"`.
        let descs = try session.canvasDescribeRelative(
            handle: handle,
            rect: CanvasRect(x: 0, y: 300, width: 100, height: 50),
            exclude: [])
        XCTAssertEqual(descs, [.below(anchorTitle: "World")])
        XCTAssertEqual(
            try session.canvasDescribeRelative(
                handle: handle,
                rect: CanvasRect(x: 0, y: 300, width: 100, height: 50),
                exclude: ["a", "b"]),
            [],
            "an empty candidate set is an empty list, not a phrase")

        // Bounds (0b-11): every node, group frames included.
        XCTAssertEqual(
            try session.canvasBounds(handle: handle),
            CanvasRect(x: -20, y: -20, width: 400, height: 300))

        // Group rect (0b-11): the members' union inflated by
        // DEFAULT_GAP on all four sides; `nil` when nothing resolves.
        XCTAssertEqual(
            try session.canvasGroupRectAround(handle: handle, members: ["a", "b"]),
            CanvasRect(x: -40, y: -40, width: 180, height: 230))
        XCTAssertNil(try session.canvasGroupRectAround(handle: handle, members: ["nope"]))

        // Inside-group placement (0b-12): clipped to the frame, walked
        // column by column, each column top to bottom. The first two
        // slots of the first column are taken by the two cards.
        let placement = try session.canvasPlaceInsideGroup(
            handle: handle, groupId: "grp", width: 40, height: 40, exclude: [])
        guard case let .placed(x, y) = placement else {
            return XCTFail("expected a free slot inside the group, got \(placement)")
        }
        XCTAssertEqual(x, 0)
        XCTAssertEqual(y, 180)
        // Two DIFFERENT refusals: not in the canvas, versus in the
        // canvas but not a container.
        XCTAssertThrowsError(
            try session.canvasPlaceInsideGroup(
                handle: handle, groupId: "nope", width: 40, height: 40, exclude: []))
        XCTAssertThrowsError(
            try session.canvasPlaceInsideGroup(
                handle: handle, groupId: "a", width: 40, height: 40, exclude: []))

        // Speakable names (0b-5) reach all four record types. Every
        // title here is unique, so each name IS its title — the
        // ordinal path is core's to pin; what matters over the FFI is
        // that the field arrives on every surface.
        XCTAssertEqual(
            try session.canvasOutline(handle: handle).map(\.speakableName),
            ["Ideas", "Hello", "World"])
        XCTAssertEqual(
            try session.canvasTableRows(handle: handle).map(\.speakableName),
            ["Ideas", "Hello", "World"])
        XCTAssertEqual(
            try session.canvasScene(handle: handle).nodes.map(\.speakableName),
            ["Ideas", "Hello", "World"])
        XCTAssertEqual(
            try session.canvasWhereAmI(handle: handle, nodeId: "a").speakableName, "Hello")
    }

    /// The three handle-free canvas exports (0b-4 / CD-17): a caller
    /// needs no open canvas to read a constant, mint an id, or ask
    /// which edges two rects should join on.
    func testCanvasHandleFreeExportsOverFFI() {
        let geometry = canvasConstants()
        XCTAssertEqual(geometry.gridStep, 20)
        XCTAssertEqual(geometry.gridStepLarge, 100)
        XCTAssertEqual(geometry.defaultCardW, 260)
        XCTAssertEqual(geometry.defaultCardH, 140)
        XCTAssertEqual(geometry.defaultGroupW, 400)
        XCTAssertEqual(geometry.defaultGroupH, 300)
        XCTAssertEqual(geometry.defaultGap, 40)
        XCTAssertEqual(geometry.minCardSize, 40)

        // JSON Canvas ids: 16 lowercase hex with the v4 version nibble
        // at index 12. No collision check, on either host.
        var minted: Set<String> = []
        for _ in 0..<64 {
            let id = canvasNewId()
            let lowerHex = id.allSatisfy { $0.isHexDigit && !$0.isUppercase }
            XCTAssertEqual(id.count, 16, id)
            XCTAssertTrue(lowerHex, id)
            // `dropFirst`, not a subscript: a length regression should
            // fail this assertion, not trap the whole suite.
            XCTAssertEqual(id.dropFirst(12).first, "4", id)
            minted.insert(id)
        }
        XCTAssertEqual(minted.count, 64, "16 hex characters do not repeat over 64 draws")

        // Auto sides (0b-3): |dx| > |dy| STRICTLY picks horizontal, so
        // every tie — the diagonal and the self-loop included —
        // resolves vertical.
        let left = CanvasRect(x: 0, y: 0, width: 100, height: 50)
        let below = CanvasRect(x: 0, y: 100, width: 100, height: 50)
        let right = CanvasRect(x: 500, y: 0, width: 100, height: 50)
        XCTAssertEqual(
            canvasAutoSides(from: left, to: below), CanvasSidePair(from: .bottom, to: .top))
        XCTAssertEqual(
            canvasAutoSides(from: below, to: left), CanvasSidePair(from: .top, to: .bottom))
        XCTAssertEqual(
            canvasAutoSides(from: left, to: right), CanvasSidePair(from: .right, to: .left))
        XCTAssertEqual(
            canvasAutoSides(from: right, to: left), CanvasSidePair(from: .left, to: .right))
        XCTAssertEqual(
            canvasAutoSides(from: left, to: left), CanvasSidePair(from: .top, to: .bottom),
            "a self-loop is the |dx| == |dy| == 0 tie")
    }

    func testQuickOpenFilterIncludesCanvas() throws {
        let vault = try makeVault()
        defer { try? FileManager.default.removeItem(at: vault) }
        try "# note".write(
            to: vault.appendingPathComponent("n.md"), atomically: true, encoding: .utf8)
        let session = try VaultSession.openFilesystem(rootPath: vault.path)
        _ = try session.scanInitial(cancel: CancelToken())

        let page = try session.listFiles(
            filter: .markdownAndCanvas, paging: Paging(cursor: nil, limit: 100))
        XCTAssertEqual(page.items.map(\.name).sorted(), ["n.md", "t.canvas"])

        let mdOnly = try session.listFiles(
            filter: .markdownOnly, paging: Paging(cursor: nil, limit: 100))
        XCTAssertEqual(mdOnly.items.map(\.name), ["n.md"])
    }
}
