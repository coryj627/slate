// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// #361 acceptance: Swift can drive the full canvas read API over the
/// FFI — open, outline, table, neighbors, where-am-I, placement,
/// overlap — against a real on-disk vault, no mocks.
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
