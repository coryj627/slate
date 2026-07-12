// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// #552 acceptance: Swift can drive the graph query surface over the
/// FFI — snapshot, neighborhood, generation probe — against a real
/// on-disk vault, no mocks (the CanvasFFITests shape).
final class GraphFFITests: XCTestCase {
    private func makeVault() throws -> URL {
        let tmp = FileManager.default.temporaryDirectory
            .appendingPathComponent("graph-ffi-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: tmp, withIntermediateDirectories: true)
        try "[[b]] twice [[b]], embed ![[b]], ghost [[Missing Note]]"
            .write(to: tmp.appendingPathComponent("a.md"), atomically: true, encoding: .utf8)
        try "back to [[a]]"
            .write(to: tmp.appendingPathComponent("b.md"), atomically: true, encoding: .utf8)
        try "alone"
            .write(to: tmp.appendingPathComponent("loner.md"), atomically: true, encoding: .utf8)
        return tmp
    }

    private var defaultFilter: GraphFilter {
        GraphFilter(includeAttachments: false, includeGhosts: true, orphansOnly: false)
    }

    func testGraphQuerySurfaceOverFFI() throws {
        let vault = try makeVault()
        defer { try? FileManager.default.removeItem(at: vault) }
        let session = try VaultSession.openFilesystem(rootPath: vault.path)
        try session.scanInitial(cancel: CancelToken())

        // Cold probe: 0 before the first graph query builds the index.
        XCTAssertEqual(session.graphGeneration(), 0)

        let snapshot = try session.graphSnapshot(filter: defaultFilter)
        XCTAssertEqual(snapshot.nodes.map(\.label), ["a", "b", "loner", "Missing Note"])
        XCTAssertEqual(
            snapshot.audioSummary,
            "3 notes, 5 links. 1 orphans, 1 unresolved targets.")
        let ghost = try XCTUnwrap(snapshot.nodes.first { $0.kind == .ghost })
        XCTAssertNil(ghost.path)
        XCTAssertNil(ghost.modifiedMs)
        let a = try XCTUnwrap(snapshot.nodes.first { $0.label == "a" })
        XCTAssertEqual(a.outLinks, 3)
        XCTAssertEqual(a.outEmbeds, 1)
        XCTAssertNotNil(a.modifiedMs)
        XCTAssertGreaterThan(a.pagerank, 0)

        // Edges are collapsed with reference counts, deterministic order.
        let toB = snapshot.edges.filter { $0.sourceId == a.id && $0.kind == .link }
        XCTAssertTrue(toB.contains { $0.count == 2 }, "parallel [[b]] references collapse")

        // Neighborhood: depth clamps, summary is the pre-rendered
        // VoiceOver string.
        let hood = try session.graphNeighborhood(path: "b.md", depth: 1, filter: defaultFilter)
        XCTAssertEqual(
            hood.audioSummary,
            "b: 2 links in, 1 links out. Showing 2 notes within 1 links.")
        XCTAssertEqual(hood.depth, 1)
        let clamped = try session.graphNeighborhood(path: "b.md", depth: 99, filter: defaultFilter)
        XCTAssertEqual(clamped.depth, 3)

        // Unknown path surfaces the typed error.
        XCTAssertThrowsError(
            try session.graphNeighborhood(path: "nope.md", depth: 1, filter: defaultFilter))

        // Generation: bumps once per applied mutation batch (the
        // refresh contract's cheap discriminator).
        let g0 = session.graphGeneration()
        _ = try session.saveText(path: "loner.md", contents: "[[a]]", expectedContentHash: nil)
        XCTAssertEqual(session.graphGeneration(), g0 + 1)
    }
}
