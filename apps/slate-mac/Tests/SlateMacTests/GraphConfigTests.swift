// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// #560 acceptance, the host side (W6-2 PR 0b): the codec — defaults,
/// clamps, the version rule, unknown-key preservation, group precedence,
/// the ring cycle — is core's (`graph_config`, contracts doc 0b-12) and
/// its facts live in `crates/slate-core/src/graph_config.rs`. What remains
/// here is the store's I/O against that codec (refuse-to-clobber and
/// refuse-to-downgrade leave the file byte-intact), the writer's
/// generation gate, the verbosity load / fallback / save (0b-14), and the
/// rendering extensions on the generated enums.
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

    private func writeGraphJson(_ text: String) throws -> URL {
        let slate = tempDir.appendingPathComponent(".slate")
        try FileManager.default.createDirectory(at: slate, withIntermediateDirectories: true)
        let url = slate.appendingPathComponent("graph.json")
        try text.write(to: url, atomically: true, encoding: .utf8)
        return url
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
        cfg.verbosity = .verbose

        try store.write(cfg)
        let read = try store.read()
        XCTAssertEqual(read, cfg)
        XCTAssertEqual(read.verbosity, .verbose, "the verbosity key round-trips (0bD-7)")
    }

    func testMissingFileReadsDefault() throws {
        XCTAssertEqual(try GraphConfigStore(vaultRoot: tempDir).read(), .default)
        XCTAssertEqual(GraphConfig.default, graphConfigDefault(), "the default is core's")
        XCTAssertEqual(GraphConfig.default.verbosity, .standard)
    }

    func testPreservesUnknownTopLevelKeys() throws {
        // A future Slate wrote a key we don't know; our rewrite must keep it.
        let url = try writeGraphJson(#"{"version":1,"futureThing":{"keep":true},"mode":"table"}"#)
        let store = GraphConfigStore(vaultRoot: tempDir)
        var cfg = try store.read()
        cfg.mode = .diagram
        try store.write(cfg)

        let root =
            try JSONSerialization.jsonObject(with: Data(contentsOf: url)) as? [String: Any]
        XCTAssertNotNil((root?["futureThing"] as? [String: Any])?["keep"] as? Bool)
        XCTAssertEqual(root?["mode"] as? String, "diagram")
        // The bytes are core's canonical writer (0b-D5): sorted keys, LF.
        let text = try String(contentsOf: url, encoding: .utf8)
        XCTAssertTrue(text.hasPrefix("{\n  \"connectionsDepth\": 1,\n  \"display\": {"))
        XCTAssertFalse(text.contains("\r"))
    }

    func testRefusesToClobberUnparseableFile() throws {
        let url = try writeGraphJson("this is not json {{{")
        XCTAssertThrowsError(try GraphConfigStore(vaultRoot: tempDir).write(.default))
        // The garbage is left intact, not overwritten.
        XCTAssertEqual(try String(contentsOf: url, encoding: .utf8), "this is not json {{{")
    }

    func testRefusesToDowngradeANewerVersionFile() throws {
        // A newer Slate wrote version 999 with a section we don't model.
        // Reading must REFUSE (not silently downgrade to defaults) and a
        // write must REFUSE to clobber it — the file stays byte-intact
        // (review finding 2).
        let future = #"{"version":999,"mode":"diagram","futureSection":{"x":1}}"#
        let url = try writeGraphJson(future)
        let store = GraphConfigStore(vaultRoot: tempDir)
        XCTAssertThrowsError(try store.read(), "a newer version is not downgraded on read")
        XCTAssertThrowsError(try store.write(.default), "a newer version is not clobbered on write")
        XCTAssertEqual(try String(contentsOf: url, encoding: .utf8), future)
    }

    /// The changed control and its resting percent are the event's
    /// payload; the spoken copy is core's golden (W6-2 PR 0a).
    func testChangedForceNamesTheOneChangedControl() {
        let base = GraphForcesConfig.default  // all 0.5
        var repel = base
        repel.repel = 0.7
        let repelChange = AppState.changedForce(old: base, new: repel)
        XCTAssertEqual(repelChange?.control, .repel)
        XCTAssertEqual(repelChange?.percent, 70)
        var center = base
        center.center = 0.25
        let centerChange = AppState.changedForce(old: base, new: center)
        XCTAssertEqual(centerChange?.control, .center)
        XCTAssertEqual(centerChange?.percent, 25)
        var dist = base
        dist.linkDistance = 1.0
        let distChange = AppState.changedForce(old: base, new: dist)
        XCTAssertEqual(distChange?.control, .linkDistance)
        XCTAssertEqual(distChange?.percent, 100)
        XCTAssertNil(AppState.changedForce(old: base, new: base), "no change ⇒ nothing spoken")
    }

    func testWriterDropsASupersededGeneration() async throws {
        // The writer actor is monotonic per vault: a write with an OLDER
        // generation is dropped even if it is delivered AFTER a newer one,
        // so actor reordering can never let a stale snapshot win (review
        // finding 3, round 3).
        let writer = GraphConfigWriter()
        var newer = GraphConfig.default
        newer.mode = .diagram
        var older = GraphConfig.default
        older.mode = .table
        await writer.write(vault: tempDir, config: newer, generation: 2)
        await writer.write(vault: tempDir, config: older, generation: 1)  // superseded ⇒ dropped
        XCTAssertEqual(
            try GraphConfigStore(vaultRoot: tempDir).read().mode, .diagram,
            "an older generation must not overwrite the newer one")
    }

    /// The clamps are core's; the store hands the text through.
    func testClampsOutOfRangeValues() throws {
        _ = try writeGraphJson(
            #"{"forces":{"repel":9.0,"center":-3},"connectionsDepth":99,"display":{"nodeSizeMultiplier":100}}"#)
        let cfg = try GraphConfigStore(vaultRoot: tempDir).read()
        XCTAssertEqual(cfg.forces.repel, 1.0)
        XCTAssertEqual(cfg.forces.center, 0.0)
        XCTAssertEqual(cfg.connectionsDepth, 3)
        XCTAssertEqual(cfg.display.nodeSizeMultiplier, 2.0)
    }

    /// First-match-wins is core's; the Swift sugar returns the group.
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
        XCTAssertEqual(
            graphConfigMatchingGroups(config: cfg, labels: ["meeting-note", "unrelated"]), [0, nil],
            "the batch form the diagram uses (0bD-11)")
        // A blank query never swallows every node.
        cfg.groups = [GraphGroup(query: "  ", colorToken: .pink, ringStyle: .solid)]
        XCTAssertNil(cfg.matchingGroup(for: "anything"))
    }

    /// The pickers iterate core's option vectors (design B): `allCases` and
    /// the titles are the vectors', in core's order, one per enum case.
    func testPickersAreBuiltFromCoresOptionVectors() {
        let tokens = graphColorTokens()
        XCTAssertEqual(GraphColorToken.allCases, tokens.map(\.token))
        XCTAssertEqual(GraphColorToken.allCases.map(\.title), tokens.map(\.title))
        XCTAssertEqual(tokens.map(\.tag), ["red", "orange", "yellow", "green", "teal", "blue", "purple", "pink"])
        let rings = graphRingStyles()
        XCTAssertEqual(GraphRingStyle.allCases, rings.map(\.style))
        XCTAssertEqual(GraphRingStyle.allCases.map(\.title), rings.map(\.title))
        XCTAssertEqual(rings.map(\.tag), ["solid", "dashed", "double", "dotted"])
        // Every generated case is in its vector exactly once.
        XCTAssertEqual(Set(tokens.map(\.token)).count, tokens.count)
        XCTAssertEqual(Set(rings.map(\.style)).count, rings.count)
        // The modes and the levels too (design B): the switcher and the
        // Verbosity menu iterate core's vectors, the shipped titles and tags.
        let modes = graphSurfaceModes()
        XCTAssertEqual(GraphSurfaceMode.allCases, modes.map(\.mode))
        XCTAssertEqual(GraphSurfaceMode.allCases.map(\.title), ["Table", "Diagram"])
        XCTAssertEqual(modes.map(\.tag), ["table", "diagram"])
        XCTAssertEqual(GraphSurfaceMode(persistenceTag: "diagram"), .diagram)
        let levels = graphVerbosities()
        XCTAssertEqual(GraphVerbosity.allCases, levels.map(\.verbosity))
        XCTAssertEqual(GraphVerbosity.allCases.map(\.title), ["Terse", "Standard", "Verbose"])
        XCTAssertEqual(levels.map(\.tag), ["terse", "standard", "verbose"])
    }

    func testRingStylesAreADistinctNonColorChannel() {
        // Four ring styles so the first four groups are distinguishable
        // without relying on colour (WCAG 1.4.1). The tokens and titles are
        // core's; the dash pattern is a rendering.
        XCTAssertEqual(GraphRingStyle.allCases.count, 4)
        XCTAssertEqual(GraphRingStyle.allCases.map(\.title), ["Solid", "Dashed", "Double", "Dotted"])
        XCTAssertNil(GraphRingStyle.solid.dashPattern)
        XCTAssertNotNil(GraphRingStyle.dashed.dashPattern)
        XCTAssertNotNil(GraphRingStyle.dotted.dashPattern)
        let style = graphConfigNextGroupStyle(groupCount: 1)
        XCTAssertEqual(style.ringStyle, .dashed)
        XCTAssertEqual(style.colorToken, .orange)
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
                    abs(lc), 10, "\(token.title) is a visible mark in \(name) (Lc \(lc))")
            }
        }
    }

    // MARK: - Verbosity (0b-14, 0bD-7)

    /// A loaded config applies its verbosity to the announcer.
    @MainActor
    func testLoadedConfigAppliesVerbosityToTheAnnouncer() throws {
        _ = try writeGraphJson(#"{"version":1,"verbosity":"terse"}"#)
        let state = AppState()
        state.applyLoadedGraphConfig(try GraphConfigStore(vaultRoot: tempDir).read(), vaultURL: tempDir)
        XCTAssertEqual(state.graphAnnouncer.verbosity, .terse)
        XCTAssertTrue(state.graphConfigWritable)
    }

    /// A malformed file falls back to the defaults — Standard — and marks
    /// the config read-only.
    @MainActor
    func testMalformedConfigFallsBackToStandard() throws {
        _ = try writeGraphJson("not json")
        let state = AppState()
        state.graphAnnouncer.verbosity = .verbose
        state.applyGraphConfigLoadFailure(vaultURL: tempDir)
        XCTAssertEqual(state.graphAnnouncer.verbosity, .standard)
        XCTAssertFalse(state.graphConfigWritable)
    }

    /// The live verbosity is what a save persists.
    @MainActor
    func testSaveAggregateCarriesTheLiveVerbosity() throws {
        let state = AppState()
        state.applyLoadedGraphConfig(.default, vaultURL: tempDir)
        state.graphAnnouncer.verbosity = .verbose
        XCTAssertEqual(state.graphConfigSaveAggregate().verbosity, .verbose)
    }
}
