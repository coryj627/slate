// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// Milestone T close-out (#365): the accessible-canvas E2E — real
/// AppState + FFI, no mocks — through OPEN (outline/table/scene),
/// AUTHORING (create → connect → group the marked set → move → edit),
/// the UNDO CHAIN back to byte-identical disk state, scale behavior on
/// the committed 2,000-node fixture (§K), and t0 announcement-grammar
/// conformance per verbosity level.
@MainActor
final class MilestoneTIntegrationTests: XCTestCase {
    private var tempDir: URL!
    private var posted: [String] = []

    override func setUpWithError() throws {
        try super.setUpWithError()
        tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-canvas-e2e-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: tempDir, withIntermediateDirectories: true)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: tempDir)
        try super.tearDownWithError()
    }

    private static var fixturesDir: URL {
        URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()  // SlateMacTests
            .deletingLastPathComponent()  // Tests
            .deletingLastPathComponent()  // slate-mac
            .deletingLastPathComponent()  // apps
            .deletingLastPathComponent()  // repo root
            .appendingPathComponent("crates/slate-core/tests/fixtures/canvas")
    }

    private func makeState(canvas fixtureName: String) async throws -> (AppState, CanvasDocument)
    {
        let vault = tempDir.appendingPathComponent("vault-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        let fixture = Self.fixturesDir.appendingPathComponent(fixtureName)
        try FileManager.default.copyItem(
            at: fixture, to: vault.appendingPathComponent("board.canvas"))
        try Data("# research\n".utf8)
            .write(to: vault.appendingPathComponent("notes/canvas research.md".sanitizedPath(in: vault)))
        let store = RecentVaultsStore(
            fileURL: tempDir.appendingPathComponent("recents-\(UUID().uuidString).json"))
        let state = AppState(recentsStore: store, externalOpener: { _ in true })
        state.canvasAnnouncer = CanvasAnnouncer(verbosity: .standard, coalesceWindow: 10) {
            [weak self] text, _ in self?.posted.append(text)
        }
        state.openVault(at: vault)
        await state.scanTask?.value
        state.openFile("board.canvas", target: .currentTab)
        return (state, state.canvasDocument(for: "board.canvas"))
    }

    // MARK: Open: every surface reads the same structure

    func testOpenSampleExposesOutlineTableAndScene() async throws {
        let (state, doc) = try await makeState(canvas: "sample.canvas")
        guard case .ready = doc.state else {
            return XCTFail("sample must open ready: \(doc.state)")
        }
        XCTAssertEqual(doc.outline.count, 9)
        XCTAssertEqual(doc.tableRows.count, 9)
        XCTAssertEqual(doc.scene.nodes.count, 9)
        // Reading order: groups then members, DFS (t1).
        XCTAssertEqual(doc.outline.first?.kind, "group")
        // Color names are backend-owned text (t0 §1.1 / #370): the red
        // preset and the hex→nearest-family custom both phrase.
        let colors = Set(doc.outline.compactMap(\.colorName))
        XCTAssertTrue(colors.contains("red"), "\(colors)")
        XCTAssertTrue(colors.contains("purple (custom)"), "\(colors)")
        // Subpath file cards read "Note › Heading" (t5/#525).
        XCTAssertTrue(doc.outline.contains { $0.title.contains(" › ") })
        // Navigator traversal end-to-end.
        state.canvasSelectAdjacent(offset: 1)
        XCTAssertNotNil(doc.selection.selected)
        state.canvasWhereAmI()
        XCTAssertNotNil(state.canvasWhereAmIReadback)
    }

    // MARK: Authoring loop + undo chain, byte-compared

    func testAuthoringLoopThenUndoChainRestoresBytes() async throws {
        let (state, doc) = try await makeState(canvas: "sample.canvas")
        let vault = try XCTUnwrap(state.currentVaultURL)
        let fileURL = vault.appendingPathComponent("board.canvas")
        // Canonicalize once (the fixture may not be byte-canonical),
        // then treat THAT as the baseline the undo chain must restore.
        state.canvasSelect(nodeId: doc.outline.first!.nodeId, in: doc, announce: false)
        state.canvasNewCard()
        state.canvasCardEditor = nil
        state.canvasUndo()
        let baseline = try Data(contentsOf: fileURL)
        let undoBefore = doc.undoStack.count

        // create card → connect → mark two → group marked → move.
        state.canvasNewCard()
        let newId = try XCTUnwrap(state.canvasCardEditor?.nodeId)
        state.canvasCommitCardEdit(nodeId: newId, newText: "E2E card")
        let anchor = try XCTUnwrap(doc.outline.first { $0.kind != "group" && $0.nodeId != newId })
        state.canvasConnect(from: newId, to: anchor.nodeId, label: "e2e")
        doc.selection.marked = [newId, anchor.nodeId]
        state.canvasGroupMarked(label: "E2E Zone")
        state.canvasSelect(nodeId: newId, in: doc, announce: false)
        state.canvasEnterMoveMode()
        state.canvasModeStep(dx: 1, dy: 0, large: true)
        _ = state.canvasModeController(for: doc).commit()

        let steps = doc.undoStack.count - undoBefore
        XCTAssertEqual(steps, 5, "create, edit, connect, group, move = five undo steps")
        for _ in 0..<steps { state.canvasUndo() }
        let restored = try Data(contentsOf: fileURL)
        XCTAssertEqual(restored, baseline, "undo chain restores the file BYTE-identically")
    }

    // MARK: §K scale: 2,000 nodes stays interactive

    func testLargeCanvasOpensNavigatesAndWindowsResponsively() async throws {
        let (state, doc) = try await makeState(canvas: "large_2000.canvas")
        guard case .ready = doc.state else { return XCTFail("large fixture must open") }
        XCTAssertEqual(doc.outline.count, 2000, "the committed §K fixture is 2,000 nodes")

        // Open + surface build is already benchmarked Rust-side
        // (BENCHMARKS.md); here we pin the UI-side invariants.
        let view = CanvasRendererNSView(frame: NSRect(x: 0, y: 0, width: 800, height: 600))
        view.configure(document: doc, appState: state)
        let renderStart = Date()
        view.refreshFromDocument()
        let renderMs = Date().timeIntervalSince(renderStart) * 1000
        // AX windowing (§K): a one-viewport margin materializes a
        // small fraction of 2,000 — never the whole board.
        let materialized = view.visibleCardFramesForTesting().count
        XCTAssertGreaterThan(materialized, 0)
        XCTAssertLessThan(materialized, 600, "windowing must bound the AX tree: \(materialized)")
        XCTAssertLessThan(renderMs, 500, "windowed rebuild stays interactive: \(renderMs) ms")
        print("BENCH canvas_ui first_windowed_rebuild_2000=\(String(format: "%.1f", renderMs))ms materialized=\(materialized)")

        // Pan sweep: ten window-hops stay under a keystroke budget each.
        let panStart = Date()
        for i in 0..<10 {
            doc.viewport.offset = CGPoint(x: Double(i) * 400, y: Double(i) * 250)
            view.rebuildVisible()
        }
        let panMs = Date().timeIntervalSince(panStart) * 1000 / 10
        XCTAssertLessThan(panMs, 100, "per-pan rebuild at 2,000 nodes: \(panMs) ms")
        print("BENCH canvas_ui pan_rebuild_2000=\(String(format: "%.1f", panMs))ms")

        // Navigator traversal doesn't degrade (spot-check 50 steps).
        let navStart = Date()
        for _ in 0..<50 { state.canvasSelectAdjacent(offset: 1) }
        let navMs = Date().timeIntervalSince(navStart) * 1000 / 50
        XCTAssertLessThan(navMs, 50, "per-step traversal at 2,000 nodes: \(navMs) ms")
        print("BENCH canvas_ui nav_step_2000=\(String(format: "%.2f", navMs))ms")
    }

    // MARK: t0 announcement-grammar conformance per verbosity

    func testAnnouncementGrammarConformsPerVerbosity() async throws {
        let (state, doc) = try await makeState(canvas: "sample.canvas")
        let cases: [(CanvasVerbosity, (String) -> Bool)] = [
            // Terse: the title, nothing positional.
            (.terse, { !$0.contains(" of ") }),
            // Standard: title + kind + N-of-M positional context.
            (.standard, { $0.contains(" of ") }),
            // Verbose: adds connections phrasing.
            (.verbose, { $0.contains(" of ") && $0.lowercased().contains("connection") }),
        ]
        let target = try XCTUnwrap(doc.outline.first { $0.connectionCount > 0 })
        for (verbosity, conforms) in cases {
            posted = []
            state.canvasAnnouncer = CanvasAnnouncer(verbosity: verbosity, coalesceWindow: 10) {
                [weak self] text, _ in self?.posted.append(text)
            }
            doc.selection.selected = nil
            state.canvasSelect(nodeId: target.nodeId, in: doc)
            state.canvasAnnouncer.flushForTests()
            let movement = try XCTUnwrap(
                posted.first(where: { $0.contains(target.title) }),
                "\(verbosity): \(posted)")
            XCTAssertTrue(conforms(movement), "\(verbosity) grammar: \(movement)")
        }
        // §1.3: destructive confirmations carry the undo hint at
        // standard+ (funnel-owned phrasing).
        posted = []
        state.canvasAnnouncer = CanvasAnnouncer(verbosity: .standard, coalesceWindow: 10) {
            [weak self] text, _ in self?.posted.append(text)
        }
        state.canvasSelect(nodeId: target.nodeId, in: doc, announce: false)
        state.canvasDeleteSelection()
        state.canvasAnnouncer.flushForTests()
        XCTAssertTrue(
            posted.contains { $0.contains("⌘Z") || $0.lowercased().contains("undo") },
            "destructive confirmation carries the undo hint: \(posted)")
        state.canvasUndo()
    }
}

extension String {
    /// Create intermediate directories for a vault-relative path and
    /// return the file URL (test helper).
    fileprivate func sanitizedPath(in vault: URL) -> String {
        let url = vault.appendingPathComponent(self)
        try? FileManager.default.createDirectory(
            at: url.deletingLastPathComponent(), withIntermediateDirectories: true)
        return self
    }
}
