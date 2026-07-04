// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// #369 acceptance: `.canvas` routing through the single navigation
/// funnel, tab dedup, the close-gate bypass, surface persistence, and
/// the canvas states — through a real `AppState` + FFI session, no
/// mocks.
@MainActor
final class CanvasTabRoutingTests: XCTestCase {
    private var tempDir: URL!

    override func setUpWithError() throws {
        try super.setUpWithError()
        tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-canvas-routing-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: tempDir, withIntermediateDirectories: true)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: tempDir)
        try super.tearDownWithError()
    }

    private static let sampleCanvas = """
        {"nodes":[
        {"id":"a","type":"text","text":"# First","x":0,"y":0,"width":100,"height":50},
        {"id":"b","type":"text","text":"Second","x":0,"y":100,"width":100,"height":50}
        ],"edges":[{"id":"e","fromNode":"a","toNode":"b"}]}
        """

    private func makeAppState(
        canvas: String = sampleCanvas
    ) async throws -> AppState {
        let vault = tempDir.appendingPathComponent("vault-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        try Data("# note".utf8).write(to: vault.appendingPathComponent("note.md"))
        try Data(canvas.utf8).write(to: vault.appendingPathComponent("board.canvas"))
        let store = RecentVaultsStore(
            fileURL: tempDir.appendingPathComponent("recents-\(UUID().uuidString).json"))
        let state = AppState(recentsStore: store, externalOpener: { _ in true })
        state.openVault(at: vault)
        await state.scanTask?.value
        return state
    }

    func testOpenFileRoutesCanvasToCanvasTabAndLoadsDocument() async throws {
        let state = try await makeAppState()

        state.openFile("board.canvas", target: .currentTab)
        guard case .canvas(let path) = state.workspace.activeTab?.item else {
            return XCTFail("active tab is not a canvas: \(String(describing: state.workspace.activeTab))")
        }
        XCTAssertEqual(path, "board.canvas")

        // The shared document loaded through the FFI and is navigable.
        let doc = state.canvasDocument(for: "board.canvas")
        XCTAssertEqual(doc.state, .ready)
        XCTAssertEqual(doc.outline.map(\.title), ["First", "Second"])
        XCTAssertNotNil(doc.handle)

        // The note loader never touched the canvas: no note text loaded.
        XCTAssertNil(state.currentNoteText)
        XCTAssertNil(state.noteLoadError)
    }

    func testCanvasTabDedupAndSharedDocumentAcrossTabs() async throws {
        let state = try await makeAppState()
        state.openFile("board.canvas", target: .newTab)
        let first = state.workspace.model.activeGroup.activeTabID
        // Opening the same path again is a tab SWITCH, not a duplicate.
        state.openFile("board.canvas", target: .newTab)
        XCTAssertEqual(state.workspace.model.activeGroup.activeTabID, first)
        XCTAssertEqual(
            state.workspace.model.allTabs.filter { $0.item.path == "board.canvas" }.count, 1)
        // One document per path.
        XCTAssertTrue(
            state.canvasDocument(for: "board.canvas")
                === state.canvasDocument(for: "board.canvas"))
    }

    func testCanvasCloseBypassesDirtyGateAndReleasesDocument() async throws {
        let state = try await makeAppState()
        // Dirty the note FIRST, then open the canvas in a new tab.
        state.selectedFilePath = "note.md"
        await state.noteLoadTask?.value
        state.openFile("board.canvas", target: .newTab)
        guard case .canvas = state.workspace.activeTab?.item else {
            return XCTFail("expected canvas tab")
        }
        let canvasTabID = try XCTUnwrap(state.workspace.model.activeGroup.activeTabID)

        // Closing the canvas tab NEVER prompts (write-through, #369
        // decision 4) — even while a sibling note tab is dirty.
        state.requestCloseTab(canvasTabID)
        XCTAssertNil(state.pendingTabClose, "canvas close must bypass the dirty gate")
        XCTAssertFalse(
            state.workspace.model.allTabs.contains { $0.id == canvasTabID },
            "canvas tab closed immediately")
        // Last tab for the path gone → document released (marks clear,
        // FFI handle freed — t2 multi-pane scoping).
        XCTAssertNil(state.canvasDocuments["board.canvas"])
    }

    func testCanvasSurfacePersistsPerTabAcrossStoreRoundTrip() async throws {
        let state = try await makeAppState()
        state.openFile("board.canvas", target: .currentTab)
        let tabID = try XCTUnwrap(state.workspace.model.activeGroup.activeTabID)

        state.showCanvasSurface(.table)
        XCTAssertEqual(state.workspace.canvasSurface(for: tabID), .table)

        let snapshot = WorkspaceStore.snapshot(
            of: state.workspace.model,
            canvasSurfaces: state.workspace.canvasSurfaces)
        let data = try JSONEncoder().encode(snapshot)
        let decoded = try JSONDecoder().decode(WorkspaceStore.Snapshot.self, from: data)
        XCTAssertEqual(WorkspaceStore.canvasSurfaces(from: decoded), [tabID: .table])
    }

    func testShowCanvasSurfaceIsNoOpOnNoteTab() async throws {
        let state = try await makeAppState()
        state.selectedFilePath = "note.md"
        await state.noteLoadTask?.value
        let tabID = state.workspace.model.activeGroup.activeTabID
        state.showCanvasSurface(.visual)
        if let tabID {
            XCTAssertEqual(state.workspace.canvasSurface(for: tabID), .outline)
        }
    }

    func testDegradedCanvasIsReadOnlyErrorStateNotCrash() async throws {
        let state = try await makeAppState(canvas: "not json at all")
        state.openFile("board.canvas", target: .currentTab)
        let doc = state.canvasDocument(for: "board.canvas")
        guard case .degraded = doc.state else {
            return XCTFail("expected degraded state, got \(doc.state)")
        }
        XCTAssertTrue(doc.outline.isEmpty)
    }

    func testTabStripValueCarriesCanvasKind() {
        XCTAssertEqual(
            TabBarView.accessibilityValue(index: 0, count: 2, isDirty: false, isCanvas: true),
            "tab 1 of 2, canvas")
        XCTAssertEqual(
            TabBarView.accessibilityValue(index: 1, count: 2, isDirty: true, isCanvas: false),
            "tab 2 of 2, edited")
    }

    func testQuickOpenSurfacesCanvasFiles() async throws {
        let state = try await makeAppState()
        let session = try XCTUnwrap(state.currentSession)
        let page = try session.listFiles(
            filter: .markdownAndCanvas, paging: Paging(cursor: nil, limit: 100))
        XCTAssertTrue(page.items.contains { $0.name == "board.canvas" })
    }
}
