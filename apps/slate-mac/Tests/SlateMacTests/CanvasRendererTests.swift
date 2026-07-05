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

/// #520: viewport commands — transforms per command, silence of
/// auto-pan vs announced zooms, follow-selection default.
@MainActor
extension CanvasRendererTests {
    func testViewportCommandsTransformAndAnnounce() async throws {
        let (state, doc, _) = try await makeView()
        var posted: [String] = []
        state.canvasAnnouncer = CanvasAnnouncer(verbosity: .standard, coalesceWindow: 60) {
            text, _ in posted.append(text)
        }
        doc.viewport.viewSize = CGSize(width: 800, height: 600)

        XCTAssertEqual(doc.viewport.scale, 1.0)
        XCTAssertTrue(doc.viewport.followSelection, "follow-selection defaults ON")

        state.canvasZoomIn()
        XCTAssertEqual(doc.viewport.scale, 1.25, accuracy: 0.001)
        state.canvasZoomOut()
        XCTAssertEqual(doc.viewport.scale, 1.0, accuracy: 0.001)
        state.canvasActualSize()
        XCTAssertEqual(doc.viewport.scale, 1.0, accuracy: 0.001)

        // Fit: the whole scene (origin cluster + far card) fits — the
        // scale clamps small and both extremes land inside the view.
        state.canvasFitCanvas()
        XCTAssertEqual(doc.viewport.scale, CanvasViewport.minScale, accuracy: 0.02)

        // Zoom to selection.
        state.canvasSelect(nodeId: "i1", in: doc, announce: false)
        state.canvasZoomToSelection()
        XCTAssertGreaterThan(doc.viewport.scale, 1.0, "a 200pt card zooms past 100%")

        state.canvasToggleFollowSelection()
        XCTAssertFalse(doc.viewport.followSelection)

        state.canvasAnnouncer.flushForTests()
        XCTAssertTrue(posted.contains("Zoom 125 percent."), "\(posted)")
        XCTAssertTrue(posted.contains { $0.hasPrefix("Fit canvas.") })
        XCTAssertTrue(posted.contains { $0.hasPrefix("Zoomed to selection.") })
        XCTAssertTrue(posted.contains("Viewport stays put."))
    }

    func testZoomKeepsViewCenterStationary() async throws {
        let (_, doc, _) = try await makeView()
        doc.viewport.viewSize = CGSize(width: 800, height: 600)
        doc.viewport.offset = CGPoint(x: 100, y: 100)
        let centerBefore = CGPoint(
            x: doc.viewport.offset.x + 400 / doc.viewport.scale,
            y: doc.viewport.offset.y + 300 / doc.viewport.scale)
        doc.viewport.zoom(by: 2.0)
        let centerAfter = CGPoint(
            x: doc.viewport.offset.x + 400 / doc.viewport.scale,
            y: doc.viewport.offset.y + 300 / doc.viewport.scale)
        XCTAssertEqual(centerBefore.x, centerAfter.x, accuracy: 0.5)
        XCTAssertEqual(centerBefore.y, centerAfter.y, accuracy: 0.5)
    }
}

/// Red-team #367 regressions: observation-driven invalidation, sticky
/// speakable names, transient-aware indicator/window/hit-test,
/// follow-selection wiring.
@MainActor
extension CanvasRendererTests {
    /// Let the scheduled main-actor observation tasks fire.
    private func drainMainQueue() async {
        for _ in 0..<8 { await Task.yield() }
    }

    func testPaletteZoomInvalidatesWithoutManualRebuild() async throws {
        let (state, _, view) = try await makeView()
        let before = try XCTUnwrap(view.visibleCardFramesForTesting()["i2"])
        state.canvasZoomIn()  // palette command — NO manual rebuild call
        await drainMainQueue()
        let after = try XCTUnwrap(view.visibleCardFramesForTesting()["i2"])
        XCTAssertEqual(after.width, before.width * 1.25, accuracy: 0.5,
            "viewport observation must rebuild frames (red-team F1)")
    }

    func testPaletteSelectionAutoPansWhenFollowIsOn() async throws {
        let (state, doc, view) = try await makeView()
        XCTAssertTrue(doc.viewport.followSelection)
        state.canvasSelect(nodeId: "far", in: doc, announce: false)
        await drainMainQueue()
        XCTAssertNotNil(view.visibleCardFramesForTesting()["far"],
            "selection observation + follow-selection materializes the target (F5/F7)")

        // Follow OFF: palette selection repaints but does not pan.
        doc.viewport.followSelection = false
        await drainMainQueue()
        let offsetBefore = doc.viewport.offset
        state.canvasSelect(nodeId: "i1", in: doc, announce: false)
        await drainMainQueue()
        XCTAssertEqual(doc.viewport.offset, offsetBefore,
            "follow-selection OFF: no observation-driven pan (F7)")
    }

    func testRenameRefreshesLabelAndSurvivorsKeepOrdinals() async throws {
        let (state, doc, view) = try await makeView()
        XCTAssertTrue(view.speakableLabelsForTesting().contains("Ideas 2"))
        // Rename the FIRST duplicate; the survivor must keep "Ideas 2"
        // (session-sticky, no renumber) and the renamed card re-labels.
        _ = state.canvasApply(
            CanvasAction(
                name: "rename",
                ops: [.setNodeContent(id: "i1", content: .text(text: "Fresh"))]),
            to: doc)
        await drainMainQueue()
        view.refreshFromDocument()
        let labels = view.speakableLabelsForTesting()
        XCTAssertTrue(labels.contains("Fresh"), "\(labels)")
        XCTAssertTrue(labels.contains("Ideas 2"), "survivor keeps its ordinal: \(labels)")
        XCTAssertFalse(labels.contains("Ideas"), "old label gone after rename: \(labels)")
        XCTAssertEqual(labels.count, Set(labels).count, "uniqueness holds: \(labels)")
    }

    func testMoveTransientKeepsSelectionMaterializedIndicatorAndHitTest() async throws {
        let (state, doc, view) = try await makeView()
        state.canvasSelect(nodeId: "i1", in: doc, announce: false)
        // Preview far outside the window, as a long move-mode drag.
        doc.transientRects = [
            "i1": CanvasRect(x: 5000, y: 5000, width: 200, height: 100)
        ]
        view.refreshFromDocument()
        let frames = view.visibleCardFramesForTesting()
        XCTAssertNotNil(frames["i1"],
            "selected card materializes even when its preview leaves the window (F2)")

        // Hit-test: the vacated spot no longer selects i1; a click on
        // another card's committed rect still does (F6).
        XCTAssertNotEqual(view.hitTestNode(atViewPoint: CGPoint(x: 100, y: 50))?.nodeId, "i1")
        XCTAssertEqual(
            view.hitTestNode(atViewPoint: CGPoint(x: 400, y: 50))?.nodeId, "i2")

        // Scroll chases the PREVIEW (F2b): after scroll the window
        // contains the transient rect.
        view.scrollSelectionIntoView()
        let after = try XCTUnwrap(view.visibleCardFramesForTesting()["i1"])
        XCTAssertTrue(
            CGRect(x: 0, y: 0, width: 800, height: 600).intersects(after),
            "auto-pan followed the transient: \(after)")
        doc.transientRects = nil
    }

    func testAXFocusSyncSelectsSilently() async throws {
        // Keep the AppState alive — the element's closure holds it weak.
        let (state, doc, view) = try await makeView()
        defer { _ = state }
        // Simulate VO cursor landing on i2's element (F5).
        let frames = view.visibleCardFramesForTesting()
        XCTAssertNotNil(frames["i2"])
        let element = try XCTUnwrap(
            view.accessibilityChildren()?
                .compactMap { $0 as? CanvasCardAXElement }
                .first { $0.nodeId == "i2" })
        element.setAccessibilityFocused(true)
        XCTAssertEqual(doc.selection.selected, "i2",
            "VO focus moves CanvasSelection (t3 non-stranding)")
    }
}
