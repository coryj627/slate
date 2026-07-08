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

    private func makeListAppState() async throws -> AppState {
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
                name: Table
                filters: "file.inFolder(\"Notes\")"
                order:
                  - file.name
                  - status
              - type: list
                name: List
                filters: "file.inFolder(\"Notes\")"
                order:
                  - file.name
                  - status
                slate:
                  list:
                    marker: number
                    secondaryProperties: indented
                    separator: " · "
            """#.utf8
        ).write(to: vault.appendingPathComponent("Queries/Reading.base"))
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

    private func makeListAppStateWithEscapedSlateState() async throws -> AppState {
        let vault = tempDir.appendingPathComponent("vault-\(UUID().uuidString)")
        vaultURL = vault
        try FileManager.default.createDirectory(
            at: vault.appendingPathComponent("Queries"), withIntermediateDirectories: true)
        try FileManager.default.createDirectory(
            at: vault.appendingPathComponent("Notes"), withIntermediateDirectories: true)
        try Data(
            #"""
            views:
              - type: list
                name: List
                filters: "file.inFolder(\"Notes\")"
                order:
                  - file.name
                  - status
                slate:
                  list:
                    marker: number
                    secondaryProperties: indented
                    separator: "\t"
                  pluginState:
                    "odd key": "line\nbreak"
                    values:
                      - "plain"
                      - "tab\tvalue"
            """#.utf8
        ).write(to: vault.appendingPathComponent("Queries/Reading.base"))
        try Data("---\nstatus: active\n---\n# Alpha\n".utf8)
            .write(to: vault.appendingPathComponent("Notes/Alpha.md"))

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

    func testListRendererModeUsesViewTypeUnlessTransientOverrideWins() {
        let table = BaseViewSummary(
            name: "Table", viewType: "table", source: "files", status: .executable,
            slateStateJson: nil)
        let list = BaseViewSummary(
            name: "List", viewType: "list", source: "files", status: .executable,
            slateStateJson: nil)

        XCTAssertEqual(BaseRendererMode.resolved(view: table, override: nil), .table)
        XCTAssertEqual(BaseRendererMode.resolved(view: list, override: nil), .list)
        XCTAssertEqual(BaseRendererMode.resolved(view: list, override: .table), .table)
        XCTAssertEqual(BaseRendererMode.resolved(view: table, override: .list), .list)
    }

    func testListProjectionKeepsTableRowIdentityAndUsesPrimaryColumn() {
        let result = Self.sampleBaseResult()
        let projection = BaseListProjection(
            result: result,
            options: BaseListOptions(slateStateJson: nil))

        XCTAssertEqual(projection.items.map(\.id), [
            "Notes/Alpha.md:0",
            "Notes/Beta.md:1",
        ])
        XCTAssertEqual(projection.items.map(\.filePath), result.rows.map(\.filePath))
        XCTAssertEqual(projection.items.map(\.primaryText), ["Alpha", "Beta"])
        XCTAssertEqual(projection.items.map(\.inlineDetailText), [
            "status: active, priority: high",
            "status: done, priority: low",
        ])
        XCTAssertEqual(projection.sections.map(\.label), ["Status: active", "Status: done"])
        XCTAssertEqual(projection.summary, "status count: 2")
        XCTAssertEqual(projection.items.first?.accessibilityLabel, "Alpha row audio")
    }

    func testIndentedListDisplayRowsExposeDetailsAndSkipSectionsForHomeEnd() {
        let projection = BaseListProjection(
            result: Self.sampleBaseResult(),
            options: BaseListOptions(
                slateStateJson:
                    #"{"list":{"secondaryProperties":"indented","marker":"number"}}"#))
        let display = BaseListDisplayModel(projection: projection)

        XCTAssertEqual(display.rows.map(\.kind), [
            .section, .item, .detail, .detail,
            .section, .item, .detail, .detail,
        ])
        XCTAssertEqual(display.firstItemIndex, 1)
        XCTAssertEqual(display.lastItemIndex, 5)
        XCTAssertEqual(display.selectionID(at: 2), "Notes/Alpha.md:0")
        XCTAssertEqual(display.activationItem(at: 2)?.filePath, "Notes/Alpha.md")
        XCTAssertEqual(display.accessibilityLabel(at: 0), "Group: Status: active, 1 row")
        XCTAssertEqual(display.accessibilityLabel(at: 1), "Alpha row audio")
        XCTAssertEqual(display.accessibilityLabel(at: 2), "status: active")
    }

    func testListViewTypeAndSlateStateCrossTheBaseOpenFfi() async throws {
        let state = try await makeListAppState()
        state.openFile("Queries/Reading.base", target: .currentTab)
        let doc = state.baseDocument(for: "Queries/Reading.base")

        state.basesSelectNextView()

        let view = try XCTUnwrap(
            doc.views.indices.contains(doc.activeViewIndex)
                ? doc.views[doc.activeViewIndex]
                : nil)
        XCTAssertEqual(view.viewType, "list")
        XCTAssertEqual(BaseRendererMode.resolved(view: view, override: nil), .list)
        XCTAssertTrue(view.slateStateJson?.contains(#""marker":"number""#) == true)
        XCTAssertEqual(
            BaseListOptions(slateStateJson: view.slateStateJson).secondaryProperties,
            .indented)
        XCTAssertEqual(doc.result?.rows.map(\.filePath), ["Notes/Alpha.md", "Notes/Beta.md"])
    }

    func testSavingSortOnListViewPreservesListSlateState() async throws {
        let state = try await makeListAppState()
        state.openFile("Queries/Reading.base", target: .currentTab)
        let doc = state.baseDocument(for: "Queries/Reading.base")
        state.basesSelectNextView()

        doc.focusColumn(1)
        state.basesSortByColumn()
        state.basesSaveSortToView()

        let source = try String(
            contentsOf: vaultURL.appendingPathComponent("Queries/Reading.base"),
            encoding: .utf8)
        XCTAssertTrue(source.contains("marker: number"))
        XCTAssertTrue(source.contains("secondaryProperties: indented"))
        XCTAssertTrue(source.contains("separator: \" · \""))
        XCTAssertTrue(source.contains("sort:"))
        XCTAssertTrue(source.contains("property: \"status\""))
    }

    func testSavingSortEscapesUnknownSlateStateControlCharacters() async throws {
        let state = try await makeListAppStateWithEscapedSlateState()
        state.openFile("Queries/Reading.base", target: .currentTab)
        let doc = state.baseDocument(for: "Queries/Reading.base")

        doc.focusColumn(1)
        state.basesSortByColumn()
        state.basesSaveSortToView()

        let source = try String(
            contentsOf: vaultURL.appendingPathComponent("Queries/Reading.base"),
            encoding: .utf8)
        XCTAssertTrue(source.contains(#"separator: "\t""#))
        XCTAssertTrue(source.contains(#""odd key": "line\nbreak""#))
        XCTAssertTrue(source.contains(#"- "tab\tvalue""#))
        XCTAssertTrue(source.contains("property: \"status\""))
    }

    func testListOptionsParseIndentedNumberedAndNoneMarkerVariants() {
        let indented = BaseListOptions(
            slateStateJson:
                #"{"list":{"marker":"number","secondaryProperties":"indented","separator":" · "}}"#)

        XCTAssertEqual(indented.marker, .number)
        XCTAssertEqual(indented.secondaryProperties, .indented)
        XCTAssertEqual(indented.separator, " · ")

        let noMarkers = BaseListOptions(
            slateStateJson: #"{"list":{"marker":"none","secondaryProperties":"inline"}}"#)

        XCTAssertEqual(noMarkers.marker, .none)
        XCTAssertEqual(noMarkers.secondaryProperties, .inline)
        XCTAssertEqual(noMarkers.separator, ", ")
    }

    func testListRendererOverrideIsTabScopedAndNeverPersists() async throws {
        let state = try await makeAppState()
        state.openFile("Queries/Reading.base", target: .currentTab)
        let tabID = try XCTUnwrap(state.workspace.activeTab?.id)
        let sourceBefore = try String(
            contentsOf: vaultURL.appendingPathComponent("Queries/Reading.base"),
            encoding: .utf8)

        state.basesViewAsList()

        XCTAssertEqual(state.baseRendererOverride(for: tabID), .list)
        XCTAssertEqual(
            try String(
                contentsOf: vaultURL.appendingPathComponent("Queries/Reading.base"),
                encoding: .utf8),
            sourceBefore,
            "View-as commands are transient UI state and must not dirty .base files")
    }

    private static func sampleBaseResult() -> BasesResultSet {
        BasesResultSet(
            columns: [
                BasesColumn(id: "file.name", label: "Name", valueKind: "text", role: .identifier),
                BasesColumn(id: "status", label: "status", valueKind: "text", role: .metadata),
                BasesColumn(id: "priority", label: "priority", valueKind: "text", role: .metadata),
            ],
            rows: [
                BasesRow(
                    filePath: "Notes/Alpha.md",
                    taskOrdinal: nil,
                    values: [
                        textValue("Alpha"),
                        textValue("active"),
                        textValue("high"),
                    ],
                    audioDescription: "Alpha row audio"),
                BasesRow(
                    filePath: "Notes/Beta.md",
                    taskOrdinal: nil,
                    values: [
                        textValue("Beta"),
                        textValue("done"),
                        textValue("low"),
                    ],
                    audioDescription: "Beta row audio"),
            ],
            groups: [
                BasesGroup(label: "Status: active", rowStart: 0, rowCount: 1, summaries: []),
                BasesGroup(label: "Status: done", rowStart: 1, rowCount: 1, summaries: []),
            ],
            summaries: [
                BasesSummaryCell(columnId: "status", summary: "count", value: textValue("2"))
            ],
            totalCount: 2,
            shownCount: 2,
            executedAtMs: 0,
            warnings: [],
            viewError: nil,
            audioSummary: "2 notes.")
    }

    private static func textValue(_ value: String) -> BasesValue {
        BasesValue(
            rawKind: "text",
            display: value,
            text: value,
            number: nil,
            boolValue: nil,
            dateEpochMs: nil,
            dateHasTime: false,
            linkTarget: nil,
            linkDisplay: nil,
            list: [],
            error: nil)
    }
}
