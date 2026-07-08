// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// N3-1 (#702): `.base` files route through the same openFile funnel as
/// notes/canvases, land in a Bases workspace tab, and render the first view
/// through the shared Bases FFI result shape.
@MainActor
final class BasesTabRoutingTests: XCTestCase {
    private var tempDir: URL!
    private var vaultURL: URL!

    override func setUpWithError() throws {
        try super.setUpWithError()
        tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-bases-routing-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: tempDir, withIntermediateDirectories: true)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: tempDir)
        try super.tearDownWithError()
    }

    private func makeAppState() async throws -> AppState {
        let vault = tempDir.appendingPathComponent("vault-\(UUID().uuidString)")
        vaultURL = vault
        try FileManager.default.createDirectory(
            at: vault.appendingPathComponent("Queries"), withIntermediateDirectories: true)
        try FileManager.default.createDirectory(
            at: vault.appendingPathComponent("Notes"), withIntermediateDirectories: true)
        try Data(
            #"""
            views:
              - type: table
                name: Reading
                filters: "file.inFolder(\"Notes\")"
                order:
                  - file.name
                  - status
                summaries:
                  status: count
              - type: table
                name: Done
                filters:
                  and:
                    - "file.inFolder(\"Notes\")"
                    - "status == \"done\""
                order:
                  - file.name
                  - status
            """#.utf8
        ).write(to: vault.appendingPathComponent("Queries/Reading.base"))
        try Data(
            #"""
            views:
              - type: table
                name: Other
                filters: "file.inFolder(\"Notes\")"
                order:
                  - file.name
            """#.utf8
        ).write(to: vault.appendingPathComponent("Queries/Other.base"))
        try Data("---\nstatus: active\n---\n# Alpha\n".utf8)
            .write(to: vault.appendingPathComponent("Notes/Alpha.md"))
        try Data("---\nstatus: done\n---\n# Beta\n".utf8)
            .write(to: vault.appendingPathComponent("Notes/Beta.md"))

        let store = RecentVaultsStore(
            fileURL: tempDir.appendingPathComponent("recents-\(UUID().uuidString).json"))
        let state = AppState(recentsStore: store, externalOpener: { _ in true })
        state.openVault(at: vault)
        await state.scanTask?.value
        return state
    }

    func testOpenFileRoutesBaseToBasesTabAndLoadsDefaultView() async throws {
        let state = try await makeAppState()

        state.openFile("Queries/Reading.base", target: .currentTab)
        guard case .base(let path) = state.workspace.activeTab?.item else {
            return XCTFail("active tab is not a base: \(String(describing: state.workspace.activeTab))")
        }
        XCTAssertEqual(path, "Queries/Reading.base")

        let doc = state.baseDocument(for: "Queries/Reading.base")
        XCTAssertEqual(doc.state, .ready)
        XCTAssertEqual(doc.views.map(\.name), ["Reading", "Done"])
        XCTAssertEqual(doc.activeViewIndex, 0, "first base view is the default")
        XCTAssertEqual(doc.result?.columns.map(\.label), ["file.name", "status"])
        XCTAssertEqual(doc.result?.rows.map(\.filePath), ["Notes/Alpha.md", "Notes/Beta.md"])

        XCTAssertNil(state.currentNoteText, "the note loader must not read .base as markdown")
        XCTAssertNil(state.noteLoadError)
    }

    func testSummaryFormatterUsesSummaryCellsNotOnlyAudioSummary() async throws {
        let state = try await makeAppState()
        state.openFile("Queries/Reading.base", target: .currentTab)
        let result = try XCTUnwrap(state.baseDocument(for: "Queries/Reading.base").result)

        XCTAssertEqual(result.audioSummary, "2 notes.")
        XCTAssertEqual(
            BaseSummaryFormatter.summaryText(result),
            "status count: 2")
    }

    func testBaseViewSwitcherExecutesSelectedView() async throws {
        let state = try await makeAppState()
        state.openFile("Queries/Reading.base", target: .currentTab)
        let doc = state.baseDocument(for: "Queries/Reading.base")

        state.basesSelectNextView()
        XCTAssertEqual(doc.activeViewIndex, 1)
        XCTAssertEqual(doc.activeViewName, "Done")
        XCTAssertEqual(doc.result?.rows.map(\.filePath), ["Notes/Beta.md"])

        state.basesSelectPreviousView()
        XCTAssertEqual(doc.activeViewIndex, 0)
        XCTAssertEqual(doc.result?.rows.map(\.filePath), ["Notes/Alpha.md", "Notes/Beta.md"])
    }

    func testBasesSortCommandAndSaveSortPersistSlateState() async throws {
        let state = try await makeAppState()
        state.openFile("Queries/Reading.base", target: .currentTab)
        let doc = state.baseDocument(for: "Queries/Reading.base")

        doc.focusColumn(1)
        state.basesSortByColumn()
        state.basesSortByColumn()
        XCTAssertEqual(doc.sortState, DataGridSortState(columnIndex: 1, ascending: false))

        state.basesSaveSortToView()

        let source = try String(
            contentsOf: vaultURL.appendingPathComponent("Queries/Reading.base"),
            encoding: .utf8)
        XCTAssertTrue(source.contains("slate:"))
        XCTAssertTrue(source.contains("property: \"status\""))
        XCTAssertTrue(source.contains("direction: DESC"))
        XCTAssertEqual(doc.result?.rows.map(\.filePath), ["Notes/Beta.md", "Notes/Alpha.md"])
    }

    func testReplacingBaseTabReleasesUnreferencedBaseDocument() async throws {
        let state = try await makeAppState()
        state.openFile("Queries/Reading.base", target: .currentTab)
        XCTAssertNotNil(state.baseDocuments["Queries/Reading.base"]?.handle)

        state.openFile("Queries/Other.base", target: .currentTab)

        XCTAssertNil(
            state.baseDocuments["Queries/Reading.base"],
            "replacing a base tab closes and drops the old unreferenced base document")
        XCTAssertNotNil(state.baseDocuments["Queries/Other.base"]?.handle)
    }

    func testRenamingOpenBaseRekeysBaseDocument() async throws {
        let state = try await makeAppState()
        state.openFile("Queries/Reading.base", target: .currentTab)
        let doc = state.baseDocument(for: "Queries/Reading.base")

        await state.renameEntry(
            path: "Queries/Reading.base", isDirectory: false, to: "Renamed.base")?.value

        XCTAssertNil(state.baseDocuments["Queries/Reading.base"])
        XCTAssertTrue(state.baseDocuments["Queries/Renamed.base"] === doc)
        XCTAssertEqual(doc.path, "Queries/Renamed.base")
        XCTAssertEqual(state.workspace.activeTab?.item, .base(path: "Queries/Renamed.base"))

        doc.focusColumn(1)
        state.basesSortByColumn()
        state.basesSaveSortToView()

        let oldURL = vaultURL.appendingPathComponent("Queries/Reading.base")
        let newURL = vaultURL.appendingPathComponent("Queries/Renamed.base")
        XCTAssertFalse(
            FileManager.default.fileExists(atPath: oldURL.path),
            "saving sort after rename must not resurrect the old base path")
        let source = try String(contentsOf: newURL, encoding: .utf8)
        XCTAssertTrue(source.contains("property: \"status\""))
    }

    func testQuickOpenSurfacesBaseFiles() async throws {
        let state = try await makeAppState()
        let session = try XCTUnwrap(state.currentSession)

        let page = try session.listFiles(
            filter: .openableDocuments, paging: Paging(cursor: nil, limit: 100))
        XCTAssertTrue(page.items.contains { $0.name == "Reading.base" })
    }

    func testTabStripValueCarriesBaseKind() {
        XCTAssertEqual(
            TabBarView.accessibilityValue(index: 0, count: 2, isDirty: false, isCanvas: false, isBase: true),
            "tab 1 of 2, base")
    }
}
