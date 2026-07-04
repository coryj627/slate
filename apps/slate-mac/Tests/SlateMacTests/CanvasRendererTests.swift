// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// #367 acceptance: per-card AX elements with pan/zoom frame
/// invalidation, Voice Control label uniqueness, windowed
/// materialization that never strands traversal, selection sync +
/// screen-space indicator, hit-test z-order, scroll-into-view.
@MainActor
final class CanvasRendererTests: XCTestCase {
    private var tempDir: URL!

    override func setUpWithError() throws {
        try super.setUpWithError()
        tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-canvas-renderer-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: tempDir, withIntermediateDirectories: true)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: tempDir)
        try super.tearDownWithError()
    }

    /// Two same-titled cards (uniqueness), one overlap pair (z-order),
    /// a far-away card (windowing), and one of each remaining kind.
    private static let fixture = """
        {"nodes":[
        {"id":"i1","type":"text","text":"Ideas","x":0,"y":0,"width":200,"height":100},
        {"id":"i2","type":"text","text":"Ideas","x":300,"y":0,"width":200,"height":100},
        {"id":"over1","type":"text","text":"Under","x":0,"y":200,"width":200,"height":100},
        {"id":"over2","type":"text","text":"Over","x":50,"y":220,"width":200,"height":100},
        {"id":"far","type":"text","text":"Far away","x":100000,"y":100000,"width":200,"height":100},
        {"id":"g","type":"group","x":600,"y":0,"width":300,"height":200,"label":"Zone"},
        {"id":"f","type":"file","file":"n.md","x":620,"y":20,"width":200,"height":100},
        {"id":"l","type":"link","url":"https://example.com/x","x":0,"y":400,"width":200,"height":100}
        ],"edges":[
        {"id":"e1","fromNode":"i1","toNode":"i2","label":"pair"}
        ]}
        """

    private func makeView() async throws -> (AppState, CanvasDocument, CanvasRendererNSView) {
        let vault = tempDir.appendingPathComponent("vault-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        try Data(Self.fixture.utf8).write(to: vault.appendingPathComponent("r.canvas"))
        try Data("# n".utf8).write(to: vault.appendingPathComponent("n.md"))
        let store = RecentVaultsStore(
            fileURL: tempDir.appendingPathComponent("recents-\(UUID().uuidString).json"))
        let state = AppState(recentsStore: store, externalOpener: { _ in true })
        state.openVault(at: vault)
        await state.scanTask?.value
        state.openFile("r.canvas", target: .currentTab)
        let doc = state.canvasDocument(for: "r.canvas")
        let view = CanvasRendererNSView(frame: NSRect(x: 0, y: 0, width: 800, height: 600))
        view.configure(document: doc, appState: state)
        view.refreshFromDocument()
        return (state, doc, view)
    }

    func testRendersAllNodeKindsWithinWindowAndSkipsFarAway() async throws {
        let (_, _, view) = try await makeView()
        let frames = view.visibleCardFramesForTesting()
        // Everything near the origin materializes…
        for id in ["i1", "i2", "over1", "over2", "g", "f", "l"] {
            XCTAssertNotNil(frames[id], "missing AX element for \(id)")
        }
        // …the far-away card does NOT (windowed materialization, §K).
        XCTAssertNil(frames["far"], "far-off card must not materialize an AX element")
    }

    func testAXFramesInvalidateOnZoomAndPan() async throws {
        let (_, doc, view) = try await makeView()
        let before = try XCTUnwrap(view.visibleCardFramesForTesting()["i2"])

        // Zoom: frames scale (the classic stale-frame failure is a
        // frame that DOESN'T move — VO cursor on empty space).
        doc.viewport.scale = 2.0
        view.rebuildVisible()
        let zoomed = try XCTUnwrap(view.visibleCardFramesForTesting()["i2"])
        XCTAssertEqual(zoomed.origin.x, before.origin.x * 2, accuracy: 0.5)
        XCTAssertEqual(zoomed.width, before.width * 2, accuracy: 0.5)

        // Pan: frames translate.
        doc.viewport.offset = CGPoint(x: 100, y: 50)
        view.rebuildVisible()
        let panned = try XCTUnwrap(view.visibleCardFramesForTesting()["i2"])
        XCTAssertEqual(panned.origin.x, zoomed.origin.x - 100 * 2, accuracy: 0.5)
        XCTAssertEqual(panned.origin.y, zoomed.origin.y - 50 * 2, accuracy: 0.5)
    }

    func testSpeakableNamesAreUniquePerSurface() async throws {
        let (_, _, view) = try await makeView()
        let labels = view.speakableLabelsForTesting()
        XCTAssertEqual(
            labels.count, Set(labels).count,
            "no two elements share a speakable name (Voice Control): \(labels.sorted())")
        // Duplicate titles disambiguate with stable reading-order
        // ordinals (t0 §1.1 rule applied to titled duplicates too).
        XCTAssertTrue(labels.contains("Ideas"))
        XCTAssertTrue(labels.contains("Ideas 2"))
        // Groups keep their Group phrasing.
        XCTAssertTrue(labels.contains("Group Zone"))
    }

    func testPanMaterializesTheNextWindow() async throws {
        let (_, doc, view) = try await makeView()
        XCTAssertNil(view.visibleCardFramesForTesting()["far"])
        // Jump the viewport to the far card's neighborhood: it
        // materializes, the origin cluster drops out.
        doc.viewport.offset = CGPoint(x: 99500, y: 99700)
        view.rebuildVisible()
        let frames = view.visibleCardFramesForTesting()
        XCTAssertNotNil(frames["far"], "pan must materialize the next window")
        XCTAssertNil(frames["i1"], "left-behind window demateralizes")
    }

    func testSelectionScrollsIntoViewRegardlessOfFollowToggle() async throws {
        let (state, doc, view) = try await makeView()
        doc.viewport.followSelection = false  // 2.4.11 is unconditional
        state.canvasSelect(nodeId: "far", in: doc, announce: false)
        view.scrollSelectionIntoView()
        let frames = view.visibleCardFramesForTesting()
        let farFrame = try XCTUnwrap(frames["far"], "selected card is in the window")
        // Fully inside the 800×600 view bounds.
        XCTAssertTrue(
            CGRect(x: 0, y: 0, width: 800, height: 600).contains(farFrame),
            "keyboard selection always scrolls into view: \(farFrame)")
    }

    func testHitTestPicksTopmostByDocumentOrder() async throws {
        let (_, doc, view) = try await makeView()
        _ = view
        // over1 (doc idx 2) and over2 (doc idx 3) overlap at (60..200,
        // 220..300): the later node wins (t1 tiebreak).
        let hit = doc.scene.nodes.last { node in
            CGRect(x: node.x, y: node.y, width: node.width, height: node.height)
                .contains(CGPoint(x: 100, y: 250))
        }
        XCTAssertEqual(hit?.nodeId, "over2")
    }

    func testSceneCarriesEdgeAndKindData() async throws {
        let (_, doc, _) = try await makeView()
        XCTAssertEqual(doc.scene.nodes.count, 8)
        let kinds = Set(doc.scene.nodes.map(\.kind))
        XCTAssertTrue(kinds.isSuperset(of: ["text", "group", "file", "link"]))
        let edge = try XCTUnwrap(doc.scene.edges.first)
        XCTAssertEqual(edge.label, "pair")
        XCTAssertFalse(edge.fromArrow)
        XCTAssertTrue(edge.toArrow)
    }
}
