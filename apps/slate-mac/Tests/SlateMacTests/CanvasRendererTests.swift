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

    /// `offset` is where the viewport sits when the view first binds, so
    /// the FIRST materialization can be a window that leaves cards out.
    private func makeView(
        canvasJSON: String? = nil, offset: CGPoint = .zero
    ) async throws -> (AppState, CanvasDocument, CanvasRendererNSView) {
        let vault = tempDir.appendingPathComponent("vault-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        try Data((canvasJSON ?? Self.fixture).utf8)
            .write(to: vault.appendingPathComponent("r.canvas"))
        try Data("# n".utf8).write(to: vault.appendingPathComponent("n.md"))
        let store = RecentVaultsStore(
            fileURL: tempDir.appendingPathComponent("recents-\(UUID().uuidString).json"))
        let state = AppState(recentsStore: store, externalOpener: { _ in true })
        state.openVault(at: vault)
        await state.scanTask?.value
        state.openFile("r.canvas", target: .currentTab)
        let doc = state.canvasDocument(for: "r.canvas")
        doc.viewport.offset = offset
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
        // Duplicate titles disambiguate with a DOCUMENT-order ordinal
        // (t0 §1.1 rule applied to titled duplicates too) — core's
        // `CardSummary.speakable_name`, contract 0b-5.
        XCTAssertTrue(labels.contains("Ideas"))
        XCTAssertTrue(labels.contains("Ideas 2"))
        // Groups keep their Group phrasing.
        XCTAssertTrue(labels.contains("Group Zone"))
    }

    /// Edge anchors take BOTH halves of one `canvas_auto_sides` pair.
    ///
    /// Resolving each endpoint with its own call and reading `.from`
    /// twice is the tempting shape and is wrong at every tie: the pair
    /// is `(Top, Bottom)`, two `.from` reads give `(Top, Top)`, and the
    /// edge collapses to a zero-length line in one corner. A self-loop
    /// and two coincident centres are exactly those ties (contract
    /// 0b-3: `|dx| > |dy|` is STRICT, so a tie resolves vertical and
    /// `dy > 0` is false at zero).
    func testEdgeSidesComeFromOnePairNotTwoFromReads() {
        let card = CGRect(x: 0, y: 0, width: 200, height: 100)

        let selfLoop = CanvasRendererNSView.resolvedSides(
            fromSide: nil, toSide: nil, from: card, to: card)
        XCTAssertEqual(selfLoop, CanvasSidePair(from: .top, to: .bottom))

        // Different rects, same centre — the other tie.
        let concentric = CGRect(x: 50, y: 25, width: 100, height: 50)
        let coincident = CanvasRendererNSView.resolvedSides(
            fromSide: nil, toSide: nil, from: card, to: concentric)
        XCTAssertEqual(coincident, CanvasSidePair(from: .top, to: .bottom))

        // A plain horizontal pair still faces each other.
        let right = CGRect(x: 500, y: 0, width: 200, height: 100)
        XCTAssertEqual(
            CanvasRendererNSView.resolvedSides(
                fromSide: nil, toSide: nil, from: card, to: right),
            CanvasSidePair(from: .right, to: .left))

        // A stored side wins per endpoint; the other still comes from
        // the pair, not from a second call.
        XCTAssertEqual(
            CanvasRendererNSView.resolvedSides(
                fromSide: .left, toSide: nil, from: card, to: right),
            CanvasSidePair(from: .left, to: .left))
    }

    /// W7-7 #1276, contract 34 D5: the board's container name is RESERVED
    /// in the board's one name namespace — core's speakable-name allocator
    /// holds that spelling occupied, so no card answers to it — and core
    /// exports the spelling (`CanvasConstants.visualBoardName`). The
    /// renderer still spells its label as a literal; this parity assertion
    /// pins the literal to core's constant, so the two cannot drift.
    func testContainerLabelIsCoresReservedBoardName() {
        let view = CanvasRendererNSView(frame: NSRect(x: 0, y: 0, width: 800, height: 600))
        XCTAssertEqual(view.accessibilityLabel(), canvasConstants().visualBoardName)
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

    /// CD-20 (owner decision D-3, settled in 0b-1): speakable names are
    /// core's, recomputed from document order on every derivation, so
    /// an ordinal is a function of the CURRENT document and nothing
    /// else. Renaming the first of two `Ideas` leaves exactly one
    /// `Ideas`, and the survivor drops the ordinal it only ever had to
    /// disambiguate against the card that just got renamed.
    ///
    /// This is the divergence from T R21's "ordinals hold for the
    /// session". R21's stickiness lived in this view's own
    /// `assignedSpeakable` map, so it was never document- or
    /// session-held: two panes on one canvas already disagreed. Both
    /// hosts now answer identically, which is what §W-A can pin and
    /// per-view state never could.
    func testRenameRefreshesLabelAndOrdinalsFollowDocumentOrder() async throws {
        let (state, doc, view) = try await makeView()
        XCTAssertTrue(view.speakableLabelsForTesting().contains("Ideas 2"))
        _ = state.canvasApply(
            CanvasAction(
                name: "rename",
                ops: [.setNodeContent(id: "i1", content: .text(text: "Fresh"))]),
            to: doc)
        await drainMainQueue()
        view.refreshFromDocument()
        let labels = view.speakableLabelsForTesting()
        XCTAssertTrue(labels.contains("Fresh"), "renamed card re-labels: \(labels)")
        XCTAssertTrue(
            labels.contains("Ideas"),
            "the last surviving \"Ideas\" needs no ordinal: \(labels)")
        XCTAssertFalse(
            labels.contains("Ideas 2"),
            "the ordinal existed only against the renamed card: \(labels)")
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

/// #371 Dynamic Type: renderer label fonts track the system body size
/// across the scale range (WCAG 1.4.4), labels never outgrow their
/// band, and the scaling math is pinned as a pure function.
@MainActor
extension CanvasRendererTests {
    func testScaledLabelFontTracksBodySizeAndZoom() {
        // Baseline body (13 pt) → base size unchanged.
        XCTAssertEqual(
            CanvasRendererNSView.scaledLabelFontSize(base: 12, bodyPointSize: 13, zoom: 1),
            12, accuracy: 0.01)
        // Largest accessibility body sizes scale linearly.
        let ax5 = CanvasRendererNSView.scaledLabelFontSize(base: 12, bodyPointSize: 53, zoom: 1)
        XCTAssertEqual(ax5, 12 * 53 / 13, accuracy: 0.01)
        // Zoom compounds; the tiny-zoom floor holds legibility.
        XCTAssertEqual(
            CanvasRendererNSView.scaledLabelFontSize(base: 12, bodyPointSize: 13, zoom: 2),
            24, accuracy: 0.01)
        XCTAssertEqual(
            CanvasRendererNSView.scaledLabelFontSize(base: 12, bodyPointSize: 13, zoom: 0.01),
            4, "floor prevents sub-legible glyphs")
    }

    func testRendererLabelsUseScaledFontAndStayInBand() async throws {
        let (_, _, view) = try await makeView()
        let expected = CanvasRendererNSView.scaledLabelFontSize(
            base: 12,
            bodyPointSize: NSFont.preferredFont(forTextStyle: .body).pointSize,
            zoom: 1)
        for (id, size) in view.labelFontSizesForTesting() {
            XCTAssertEqual(size, expected, accuracy: 0.01, "card \(id)")
        }
        // The label band lives inside the card frame at every size the
        // API can produce (no-clip guarantee is structural: the band
        // height is derived FROM the font).
        let frames = view.visibleCardFramesForTesting()
        for (id, cardFrame) in frames {
            let band = expected * 1.6 + 4
            XCTAssertLessThanOrEqual(
                band, max(cardFrame.height, band),
                "label band fits or truncates, never overlaps neighbours: \(id)")
        }
    }
}

/// #961: the board paints in DOCUMENT order after any pan cycle — the
/// order `hitTestNode` already picks by, and JSON Canvas's own z-order
/// (array order). Windowing evicts a card's layer when it leaves the
/// materialization window and re-adds it on the way back, and a re-added
/// sublayer lands at the TOP of the stack, so insertion order cannot be
/// what decides paint order.
@MainActor
extension CanvasRendererTests {
    /// The issue's repro: B (document index 0) is small and overlaps the
    /// left end of A (index 1), which is wide. A window over A's far end
    /// holds A and none of B.
    private static let paintOrderFixture = """
        {"nodes":[
        {"id":"b","type":"text","text":"Small","x":-50,"y":0,"width":100,"height":100},
        {"id":"a","type":"text","text":"Wide","x":0,"y":0,"width":1000,"height":100}
        ],"edges":[]}
        """

    /// The 800×600 view materializes its viewport plus one viewport on
    /// every side: at x 1300 that is canvas x 500…2900 — A's far end, and
    /// nothing of B (−50…50). At the origin both cards are in it.
    private static let windowOverAOnly = CGPoint(x: 1300, y: 0)
    private static let windowOverBoth = CGPoint.zero

    func testRematerializedCardsPaintInDocumentOrder() async throws {
        let (_, doc, view) = try await makeView(
            canvasJSON: Self.paintOrderFixture, offset: Self.windowOverAOnly)
        func cards() -> [String] {
            view.paintOrderForTesting().filter { $0 == "a" || $0 == "b" }
        }
        XCTAssertEqual(cards(), ["a"], "the first window materializes A alone")

        doc.viewport.offset = Self.windowOverBoth
        view.rebuildVisible()
        XCTAssertEqual(
            cards(), ["b", "a"],
            "B materializes AFTER A but is earlier in document order, so it paints beneath A")

        doc.viewport.offset = Self.windowOverAOnly
        view.rebuildVisible()
        XCTAssertEqual(cards(), ["a"], "panning away evicts B's layer")

        doc.viewport.offset = Self.windowOverBoth
        view.rebuildVisible()
        XCTAssertEqual(
            cards(), ["b", "a"],
            "a returning card is re-added at the top of the stack and still paints beneath A")

        // What paints on top is what a click selects: the overlap
        // (canvas x 0…50, the view's own coordinates at this offset)
        // belongs to A, the later card.
        XCTAssertEqual(view.hitTestNode(atViewPoint: CGPoint(x: 25, y: 50))?.nodeId, "a")
    }

    /// The layers between the cards hold their place through the same
    /// cycle. Plain connections paint beneath every card; coloured
    /// connections and label chips paint above them, which is where
    /// every first build has always drawn them (they are created after
    /// the cards they connect). Stamping the cards' document order must
    /// not bury a chip under a card, and a card coming back must not
    /// cover one either.
    func testConnectionLayersKeepTheirPlaceAcrossAPanCycle() async throws {
        let fixture = """
            {"nodes":[
            {"id":"b","type":"text","text":"Small","x":-50,"y":0,"width":100,"height":100},
            {"id":"a","type":"text","text":"Wide","x":0,"y":0,"width":1000,"height":100}
            ],"edges":[
            {"id":"plain","fromNode":"b","toNode":"a"},
            {"id":"tinted","fromNode":"b","toNode":"a","color":"1","label":"link"}
            ]}
            """
        let (_, doc, view) = try await makeView(canvasJSON: fixture, offset: Self.windowOverAOnly)

        func assertLayering(_ label: String) {
            let order = view.paintOrderForTesting()
            let cards = order.indices.filter { order[$0] == "a" || order[$0] == "b" }
            guard let lowestCard = cards.min(), let highestCard = cards.max(),
                let plain = order.firstIndex(of: "edges"),
                let tinted = order.firstIndex(of: "edges:1"),
                let chip = order.firstIndex(of: "chip:tinted")
            else {
                return XCTFail("\(label): a layer is missing from \(order)")
            }
            XCTAssertLessThan(plain, lowestCard, "\(label): plain connections beneath: \(order)")
            XCTAssertGreaterThan(
                tinted, highestCard, "\(label): coloured connections above: \(order)")
            XCTAssertGreaterThan(chip, highestCard, "\(label): label chips above: \(order)")
        }

        assertLayering("A alone")
        doc.viewport.offset = Self.windowOverBoth
        view.rebuildVisible()
        assertLayering("B materialized")
        doc.viewport.offset = Self.windowOverAOnly
        view.rebuildVisible()
        doc.viewport.offset = Self.windowOverBoth
        view.rebuildVisible()
        assertLayering("B rematerialized")
        XCTAssertEqual(
            view.paintOrderForTesting().filter { $0 == "a" || $0 == "b" }, ["b", "a"])
    }
}
