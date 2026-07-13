// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// #560 acceptance: `.slate/graph.json` round-trips, preserves unknown
/// keys, refuses to clobber an unparseable file, and clamps; group
/// precedence is first-match-wins with a distinct ring channel.
final class GraphConfigTests: XCTestCase {
    private var tempDir: URL!

    override func setUpWithError() throws {
        try super.setUpWithError()
        tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-graph-config-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: tempDir, withIntermediateDirectories: true)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: tempDir)
        try super.tearDownWithError()
    }

    func testRoundTripsAllSections() throws {
        let store = GraphConfigStore(vaultRoot: tempDir)
        var cfg = GraphConfig.default
        cfg.filters = GraphFilterConfig(
            includeAttachments: true, includeGhosts: false, orphansOnly: true, nameQuery: "café")
        cfg.groups = [
            GraphGroup(query: "project", colorToken: .green, ringStyle: .dashed),
            GraphGroup(query: "archive", colorToken: .purple, ringStyle: .dotted),
        ]
        cfg.display = GraphDisplay(
            arrows: true, textFadeZoom: 0.8, nodeSizeMultiplier: 1.5, linkThickness: 2.0)
        cfg.forces = GraphForcesConfig(center: 0.2, repel: 0.9, link: 0.3, linkDistance: 0.7)
        cfg.mode = .diagram
        cfg.connectionsDepth = 3

        try store.write(cfg)
        let read = try store.read()
        XCTAssertEqual(read, cfg)
    }

    func testMissingFileReadsDefault() throws {
        XCTAssertEqual(try GraphConfigStore(vaultRoot: tempDir).read(), .default)
    }

    func testPreservesUnknownTopLevelKeys() throws {
        // A future Slate wrote a key we don't know; our rewrite must keep it.
        let slate = tempDir.appendingPathComponent(".slate")
        try FileManager.default.createDirectory(at: slate, withIntermediateDirectories: true)
        let url = slate.appendingPathComponent("graph.json")
        try #"{"version":1,"futureThing":{"keep":true},"mode":"table"}"#
            .write(to: url, atomically: true, encoding: .utf8)

        let store = GraphConfigStore(vaultRoot: tempDir)
        var cfg = try store.read()
        cfg.mode = .diagram
        try store.write(cfg)

        let root =
            try JSONSerialization.jsonObject(with: Data(contentsOf: url)) as? [String: Any]
        XCTAssertNotNil((root?["futureThing"] as? [String: Any])?["keep"] as? Bool)
        XCTAssertEqual(root?["mode"] as? String, "diagram")
    }

    func testRefusesToClobberUnparseableFile() throws {
        let slate = tempDir.appendingPathComponent(".slate")
        try FileManager.default.createDirectory(at: slate, withIntermediateDirectories: true)
        let url = slate.appendingPathComponent("graph.json")
        try "this is not json {{{".write(to: url, atomically: true, encoding: .utf8)

        XCTAssertThrowsError(try GraphConfigStore(vaultRoot: tempDir).write(.default))
        // The garbage is left intact, not overwritten.
        XCTAssertEqual(try String(contentsOf: url, encoding: .utf8), "this is not json {{{")
    }

    func testClampsOutOfRangeValues() throws {
        let slate = tempDir.appendingPathComponent(".slate")
        try FileManager.default.createDirectory(at: slate, withIntermediateDirectories: true)
        try #"{"forces":{"repel":9.0,"center":-3},"connectionsDepth":99,"display":{"nodeSizeMultiplier":100}}"#
            .write(to: slate.appendingPathComponent("graph.json"), atomically: true, encoding: .utf8)
        let cfg = try GraphConfigStore(vaultRoot: tempDir).read()
        XCTAssertEqual(cfg.forces.repel, 1.0)
        XCTAssertEqual(cfg.forces.center, 0.0)
        XCTAssertEqual(cfg.connectionsDepth, 3)
        XCTAssertEqual(cfg.display.nodeSizeMultiplier, 2.0)
    }

    func testGroupPrecedenceIsFirstMatchWins() {
        var cfg = GraphConfig.default
        cfg.groups = [
            GraphGroup(query: "note", colorToken: .blue, ringStyle: .solid),
            GraphGroup(query: "meeting", colorToken: .red, ringStyle: .dashed),
        ]
        // "meeting-note" matches both queries; first rule wins.
        XCTAssertEqual(cfg.matchingGroup(for: "meeting-note")?.colorToken, .blue)
        XCTAssertEqual(cfg.matchingGroup(for: "weekly meeting")?.colorToken, .red)
        XCTAssertNil(cfg.matchingGroup(for: "unrelated"))
        // A blank query never swallows every node.
        cfg.groups = [GraphGroup(query: "  ", colorToken: .pink, ringStyle: .solid)]
        XCTAssertNil(cfg.matchingGroup(for: "anything"))
    }

    func testRingStylesAreADistinctNonColorChannel() {
        // Four ring styles so the first four groups are distinguishable
        // without relying on colour (WCAG 1.4.1).
        XCTAssertEqual(GraphRingStyle.allCases.count, 4)
        XCTAssertNil(GraphRingStyle.solid.dashPattern)
        XCTAssertNotNil(GraphRingStyle.dashed.dashPattern)
        XCTAssertNotNil(GraphRingStyle.dotted.dashPattern)
    }

    func testColorPaletteHasEightSlotsMeetingAPCA() {
        // 8 slots, each a visible graphical mark against the graph
        // background in both appearances (spec §P2-4).
        XCTAssertEqual(GraphColorToken.allCases.count, 8)
        for name in ["NSAppearanceNameAqua", "NSAppearanceNameDarkAqua"] {
            let appearance = NSAppearance(named: NSAppearance.Name(name))!
            for token in GraphColorToken.allCases {
                let lc = APCAContrast.lc(
                    text: token.color, background: .windowBackgroundColor, for: appearance)
                XCTAssertGreaterThan(
                    abs(lc), 10, "\(token.rawValue) is a visible mark in \(name) (Lc \(lc))")
            }
        }
    }
}
