// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import SwiftUI
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

    private func makeQuickFilterAppState() async throws -> AppState {
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
        try Data("---\nstatus: café\n---\n# Cafe\n".utf8)
            .write(to: vault.appendingPathComponent("Notes/Cafe.md"))

        let store = RecentVaultsStore(
            fileURL: tempDir.appendingPathComponent("recents-\(UUID().uuidString).json"))
        let state = AppState(recentsStore: store, externalOpener: { _ in true })
        state.openVault(at: vault)
        await state.scanTask?.value
        return state
    }

    private func makeTypedSortAppState() async throws -> AppState {
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
                order: [file.name, score, due]
              - type: list
                name: List
                filters: "file.inFolder(\"Notes\")"
                order: [file.name, score, due]
            """#.utf8
        ).write(to: vault.appendingPathComponent("Queries/Typed.base"))
        try Data("---\nscore: 10\ndue: 2026-03-01\n---\n# Aardvark\n".utf8)
            .write(to: vault.appendingPathComponent("Notes/Aardvark.md"))
        try Data("---\nscore: 10\ndue: 2026-03-01\n---\n# Alpha\n".utf8)
            .write(to: vault.appendingPathComponent("Notes/Alpha.md"))
        try Data("---\nscore: 2\ndue: 2026-02-01\n---\n# Beta\n".utf8)
            .write(to: vault.appendingPathComponent("Notes/Beta.md"))
        try Data("# Null\n".utf8)
            .write(to: vault.appendingPathComponent("Notes/Null.md"))

        let store = RecentVaultsStore(
            fileURL: tempDir.appendingPathComponent("recents-\(UUID().uuidString).json"))
        let state = AppState(recentsStore: store, externalOpener: { _ in true })
        state.openVault(at: vault)
        await state.scanTask?.value
        return state
    }

    private func makeLiveTaskSurfacesState() async throws -> (
        state: AppState,
        openBase: BaseDocument,
        openDashboard: DashboardDocument,
        dock: BaseDocument
    ) {
        let vault = tempDir.appendingPathComponent("live-task-vault-\(UUID().uuidString)")
        vaultURL = vault
        try FileManager.default.createDirectory(
            at: vault.appendingPathComponent("Queries"), withIntermediateDirectories: true)
        try FileManager.default.createDirectory(
            at: vault.appendingPathComponent("Notes"), withIntermediateDirectories: true)
        try Data(
            #"""
            views:
              - type: table
                name: Tasks
                source: tasks
                filters: "file.inFolder(\"Notes\")"
                order:
                  - task.text
                  - task.status
                  - task.file
            """#.utf8
        ).write(to: vault.appendingPathComponent("Queries/Tasks.base"))
        try Data("# Alpha\n".utf8)
            .write(to: vault.appendingPathComponent("Notes/Alpha.md"))

        let store = RecentVaultsStore(
            fileURL: tempDir.appendingPathComponent("recents-\(UUID().uuidString).json"))
        let state = AppState(recentsStore: store, externalOpener: { _ in true })
        state.openVault(at: vault)
        await state.scanTask?.value
        let session = try XCTUnwrap(state.currentSession)

        var taskQuery = BaseQueryBuilderDraft()
        taskQuery.source = .tasks
        let queryID = try session.saveQuery(
            name: "All tasks",
            description: nil,
            queryJson: taskQuery.queryJSON(),
            sourceSyntax: .builder)
        let dashboardID = try session.saveDashboard(
            name: "Task dashboard",
            sections: [
                DashboardSection(
                    savedQueryId: queryID,
                    headingOverride: nil,
                    viewOverride: nil)
            ])
        state.refreshBaseQueries()

        state.openBaseFile("Queries/Tasks.base")
        let openBase = state.baseDocument(for: "Queries/Tasks.base")
        state.openDashboard(id: dashboardID, name: "Task dashboard", target: .newTab)
        let openDashboard = try XCTUnwrap(state.activeDashboardDocument)
        state.dockSavedQueryToSidebar(id: queryID, refreshDelayNanoseconds: 0)
        await state.basesDockRefreshTask?.value
        let dock = try XCTUnwrap(state.basesDockDocument)

        state.openFile("Notes/Alpha.md", target: .newTab)
        await state.noteLoadTask?.value
        return (state, openBase, openDashboard, dock)
    }

    private func makeLivePropertySurfacesState() async throws -> (
        state: AppState,
        active: BaseDocument,
        sibling: BaseDocument,
        dashboard: DashboardDocument,
        dock: BaseDocument,
        alpha: BasesRow,
        beta: BasesRow,
        status: BasesColumn
    ) {
        let vault = tempDir.appendingPathComponent("live-property-vault-\(UUID().uuidString)")
        vaultURL = vault
        try FileManager.default.createDirectory(
            at: vault.appendingPathComponent("Queries"), withIntermediateDirectories: true)
        try FileManager.default.createDirectory(
            at: vault.appendingPathComponent("Notes"), withIntermediateDirectories: true)
        try Data(
            #"""
            views:
              - type: table
                name: Primary
                filters: "file.inFolder(\"Notes\")"
                order: [file.name, status]
              - type: table
                name: Alternate
                filters: "file.inFolder(\"Notes\")"
                order: [file.name, status]
            """#.utf8
        ).write(to: vault.appendingPathComponent("Queries/Edit.base"))
        try Data("---\nstatus: active\n---\n# Alpha\n".utf8)
            .write(to: vault.appendingPathComponent("Notes/Alpha.md"))
        try Data("---\nstatus: active\n---\n# Beta\n".utf8)
            .write(to: vault.appendingPathComponent("Notes/Beta.md"))

        let state = AppState(
            recentsStore: RecentVaultsStore(
                fileURL: tempDir.appendingPathComponent("recents-\(UUID().uuidString).json")),
            externalOpener: { _ in true })
        state.openVault(at: vault)
        await state.scanTask?.value
        let session = try XCTUnwrap(state.currentSession)

        var activeQuery = BaseQueryBuilderDraft()
        activeQuery.source = .folder("Notes")
        activeQuery.rows = [
            .condition(
                BaseQueryCondition(
                    property: .note("status"),
                    operator: .equals,
                    value: .text("active")))
        ]
        let queryID = try session.saveQuery(
            name: "Active notes",
            description: nil,
            queryJson: activeQuery.queryJSON(),
            sourceSyntax: .builder)
        let dashboardID = try session.saveDashboard(
            name: "Active dashboard",
            sections: [
                DashboardSection(
                    savedQueryId: queryID,
                    headingOverride: nil,
                    viewOverride: nil)
            ])
        state.refreshBaseQueries()

        state.openSavedQuery(id: queryID, name: "Active notes")
        let sibling = try XCTUnwrap(state.activeBaseDocument)
        state.openDashboard(id: dashboardID, name: "Active dashboard", target: .newTab)
        let dashboard = try XCTUnwrap(state.activeDashboardDocument)
        state.openBaseFile("Queries/Edit.base", target: .newTab)
        let active = try XCTUnwrap(state.activeBaseDocument)
        state.dockSavedQueryToSidebar(id: queryID, refreshDelayNanoseconds: 0)
        await state.basesDockRefreshTask?.value
        let dock = try XCTUnwrap(state.basesDockDocument)

        active.selectView(index: 1, session: session)
        let unfiltered = try XCTUnwrap(active.result)
        let alpha = try XCTUnwrap(unfiltered.rows.first { $0.filePath == "Notes/Alpha.md" })
        let beta = try XCTUnwrap(unfiltered.rows.first { $0.filePath == "Notes/Beta.md" })
        let status = try XCTUnwrap(unfiltered.columns.first { $0.id == "status" })
        _ = active.applyQuickFilter("Alpha", session: session)
        _ = active.setTransientSort(
            DataGridSortState(columnIndex: 1, ascending: false),
            session: session)
        state.updateActiveBaseSelection(
            path: active.selectionKey,
            rowID: BaseGridRow.id(for: alpha),
            columnIndex: 1,
            result: active.result)
        return (state, active, sibling, dashboard, dock, alpha, beta, status)
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

    func testSavingNoteRefreshesOpenBaseDashboardAndDock() async throws {
        let fixture = try await makeLiveTaskSurfacesState()
        XCTAssertTrue(fixture.openBase.result?.rows.isEmpty == true)
        XCTAssertTrue(fixture.openDashboard.sections[0].result?.rows.isEmpty == true)
        XCTAssertTrue(fixture.dock.result?.rows.isEmpty == true)

        fixture.state.updateEditorText("# Alpha\n\n- [ ] Ship it\n")
        await fixture.state.saveCurrentNote()?.value

        XCTAssertEqual(fixture.openBase.result?.rows.count, 1)
        XCTAssertEqual(fixture.openDashboard.sections[0].result?.rows.count, 1)
        XCTAssertEqual(fixture.dock.result?.rows.count, 1)
        XCTAssertEqual(
            fixture.openBase.result?.rows.first?.filePath,
            "Notes/Alpha.md")
        XCTAssertEqual(fixture.state.lastBaseRefreshAnnouncements, ["Updated: 1 task."])
    }

    func testGridSetAndDeleteRefreshSiblingSurfacesAndPreserveActiveState() async throws {
        let fixture = try await makeLivePropertySurfacesState()

        _ = await fixture.state.basesSetProperty(
            row: fixture.alpha,
            column: fixture.status,
            value: .text(value: "archived"))

        XCTAssertEqual(fixture.sibling.result?.rows.map(\.filePath), ["Notes/Beta.md"])
        XCTAssertEqual(
            fixture.dashboard.sections[0].result?.rows.map(\.filePath),
            ["Notes/Beta.md"])
        XCTAssertEqual(fixture.dock.result?.rows.map(\.filePath), ["Notes/Beta.md"])
        XCTAssertEqual(fixture.active.activeViewIndex, 1)
        XCTAssertEqual(fixture.active.quickFilterText, "Alpha")
        XCTAssertEqual(
            fixture.active.sortState,
            DataGridSortState(columnIndex: 1, ascending: false))
        XCTAssertEqual(fixture.active.result?.rows.map(\.filePath), ["Notes/Alpha.md"])
        XCTAssertEqual(fixture.state.activeBaseSelectedRow?.filePath, "Notes/Alpha.md")
        XCTAssertEqual(fixture.state.activeBaseSelectedColumn?.id, "status")
        XCTAssertEqual(fixture.state.lastBaseRefreshAnnouncements, ["Updated: 1 note."])

        _ = await fixture.state.basesDeleteProperty(
            row: fixture.beta,
            column: fixture.status)

        XCTAssertTrue(fixture.sibling.result?.rows.isEmpty == true)
        XCTAssertTrue(fixture.dashboard.sections[0].result?.rows.isEmpty == true)
        XCTAssertTrue(fixture.dock.result?.rows.isEmpty == true)
        XCTAssertEqual(fixture.active.activeViewIndex, 1)
        XCTAssertEqual(fixture.active.quickFilterText, "Alpha")
        XCTAssertEqual(
            fixture.active.sortState,
            DataGridSortState(columnIndex: 1, ascending: false))
        XCTAssertEqual(fixture.state.activeBaseSelectedRow?.filePath, "Notes/Alpha.md")
        XCTAssertEqual(fixture.state.lastBaseRefreshAnnouncements, ["Updated: No results."])
    }

    func testPropertyPanelSetAndDeleteRefreshOpenBase() async throws {
        let state = try await makeAppState()
        state.openBaseFile("Queries/Reading.base")
        let doc = state.baseDocument(for: "Queries/Reading.base")
        let session = try XCTUnwrap(state.currentSession)
        doc.selectView(index: 1, session: session)
        XCTAssertEqual(doc.result?.rows.map(\.filePath), ["Notes/Beta.md"])
        state.openFile("Notes/Alpha.md", target: .newTab)
        await state.noteLoadTask?.value

        await state.setProperty(
            path: "Notes/Alpha.md",
            key: "status",
            value: .text(value: "done"))?.value

        XCTAssertEqual(
            Set(doc.result?.rows.map(\.filePath) ?? []),
            Set(["Notes/Alpha.md", "Notes/Beta.md"]))
        XCTAssertEqual(doc.activeViewIndex, 1)
        XCTAssertEqual(state.lastBaseRefreshAnnouncements, ["Updated: 2 notes."])

        await state.deleteProperty(path: "Notes/Alpha.md", key: "status")?.value

        XCTAssertEqual(doc.result?.rows.map(\.filePath), ["Notes/Beta.md"])
        XCTAssertEqual(doc.activeViewIndex, 1)
        XCTAssertEqual(state.lastBaseRefreshAnnouncements, ["Updated: 1 note."])
    }

    func testConflictedNoteSaveDoesNotRefreshOrAnnounce() async throws {
        let fixture = try await makeLiveTaskSurfacesState()
        let session = try XCTUnwrap(fixture.state.currentSession)
        _ = try session.saveComposed(
            path: "Notes/Alpha.md",
            fmSource: "",
            body: "# Alpha\n\n- [ ] External task\n",
            expectedContentHash: fixture.state.currentNoteContentHash)
        fixture.state.updateEditorText("# Alpha\n\n- [ ] Local task\n")

        await fixture.state.saveCurrentNote()?.value

        XCTAssertNotNil(fixture.state.currentSaveConflict)
        XCTAssertTrue(fixture.openBase.result?.rows.isEmpty == true)
        XCTAssertTrue(fixture.openDashboard.sections[0].result?.rows.isEmpty == true)
        XCTAssertTrue(fixture.dock.result?.rows.isEmpty == true)
        XCTAssertTrue(fixture.state.lastBaseRefreshAnnouncements.isEmpty)
    }

    func testStaleSessionNoteSaveCannotPublishIntoReopenedVault() async throws {
        let fixture = try await makeLiveTaskSurfacesState()
        let entered = expectation(description: "save parked before Bases publish")
        let (gateStream, release) = AsyncStream.makeStream(of: Void.self)
        fixture.state.basesPostWritePublishGate = {
            entered.fulfill()
            for await _ in gateStream {}
        }
        fixture.state.updateEditorText("# Alpha\n\n- [ ] Stale task\n")
        let staleSave = try XCTUnwrap(fixture.state.saveCurrentNote())
        await fulfillment(of: [entered], timeout: 10)
        fixture.state.basesPostWritePublishGate = nil

        let replacement = tempDir.appendingPathComponent("replacement-vault")
        try FileManager.default.createDirectory(at: replacement, withIntermediateDirectories: true)
        try Data("# Replacement\n".utf8).write(to: replacement.appendingPathComponent("note.md"))
        fixture.state.openVault(at: replacement)
        await fixture.state.scanTask?.value
        fixture.state.openFile("note.md", target: .currentTab)
        await fixture.state.noteLoadTask?.value

        let replacementEntered = expectation(description: "replacement save parked")
        let (replacementGate, releaseReplacement) = AsyncStream.makeStream(of: Void.self)
        fixture.state.basesPostWritePublishGate = {
            replacementEntered.fulfill()
            for await _ in replacementGate {}
        }
        fixture.state.updateEditorText("# Replacement saved\n")
        let replacementSave = try XCTUnwrap(fixture.state.saveCurrentNote())
        await fulfillment(of: [replacementEntered], timeout: 10)
        XCTAssertTrue(fixture.state.isSaving)

        release.finish()
        await staleSave.value

        XCTAssertTrue(
            fixture.state.isSaving,
            "a stale old-session save must not clear the replacement session's in-flight flag")
        XCTAssertTrue(fixture.openBase.result?.rows.isEmpty == true)
        XCTAssertTrue(fixture.openDashboard.sections[0].result?.rows.isEmpty == true)
        XCTAssertTrue(fixture.dock.result?.rows.isEmpty == true)
        XCTAssertTrue(fixture.state.lastBaseRefreshAnnouncements.isEmpty)

        fixture.state.basesPostWritePublishGate = nil
        releaseReplacement.finish()
        await replacementSave.value
        XCTAssertFalse(fixture.state.isSaving)
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

    func testQuickFilterExecutesThroughFfiAndNeverDirtiesBase() async throws {
        let state = try await makeQuickFilterAppState()
        state.openFile("Queries/Reading.base", target: .currentTab)
        let session = try XCTUnwrap(state.currentSession)
        let doc = state.baseDocument(for: "Queries/Reading.base")
        let baseURL = vaultURL.appendingPathComponent("Queries/Reading.base")
        let bytesBefore = try Data(contentsOf: baseURL)
        let mtimeBefore = try XCTUnwrap(
            FileManager.default.attributesOfItem(atPath: baseURL.path)[.modificationDate]
                as? Date)

        let announcement = doc.applyQuickFilter("CAFE", session: session)

        XCTAssertEqual(doc.quickFilterText, "CAFE")
        XCTAssertTrue(doc.quickFilterActive)
        XCTAssertEqual(doc.result?.rows.map(\.filePath), ["Notes/Cafe.md"])
        XCTAssertEqual(doc.result?.audioSummary, "1 note.")
        XCTAssertEqual(announcement, "1 of 3 results")
        XCTAssertEqual(doc.result?.unfilteredShownCount, 3)
        XCTAssertTrue(doc.whereAmIReadback.contains("quick filter: CAFE"))
        XCTAssertEqual(
            BaseSummaryFormatter.summaryText(
                try XCTUnwrap(doc.result), isQuickFiltered: doc.quickFilterActive),
            "Summaries: filtered — status count: 1")
        XCTAssertEqual(try Data(contentsOf: baseURL), bytesBefore)
        XCTAssertEqual(
            try XCTUnwrap(
                FileManager.default.attributesOfItem(atPath: baseURL.path)[.modificationDate]
                    as? Date),
            mtimeBefore)
        XCTAssertFalse(state.hasUnsavedChanges)
        XCTAssertFalse(state.workspace.anyTabDirty(activeTabDirty: state.hasUnsavedChanges))
    }

    func testQuickFilterClearsOnViewSwitchAndTabSwitch() async throws {
        let state = try await makeQuickFilterAppState()
        state.openFile("Queries/Reading.base", target: .currentTab)
        let session = try XCTUnwrap(state.currentSession)
        let doc = state.baseDocument(for: "Queries/Reading.base")

        _ = doc.applyQuickFilter("cafe", session: session)
        XCTAssertEqual(doc.result?.rows.map(\.filePath), ["Notes/Cafe.md"])

        state.basesSelectNextView()

        XCTAssertEqual(doc.quickFilterText, "")
        XCTAssertFalse(doc.quickFilterActive)
        XCTAssertEqual(doc.activeViewName, "Done")
        XCTAssertEqual(doc.result?.rows.map(\.filePath), ["Notes/Beta.md"])

        _ = doc.applyQuickFilter("beta", session: session)
        XCTAssertEqual(doc.result?.rows.map(\.filePath), ["Notes/Beta.md"])

        state.openFile("Queries/Other.base", target: .newTab)

        XCTAssertEqual(doc.quickFilterText, "")
        XCTAssertFalse(doc.quickFilterActive)
        XCTAssertEqual(doc.result?.rows.map(\.filePath), ["Notes/Beta.md"])
    }

    func testBasesQuickFilterFocusTokenIsBaseScoped() async throws {
        let state = try await makeQuickFilterAppState()

        state.basesFocusQuickFilter()
        XCTAssertEqual(state.baseQuickFilterFocusToken, 0)

        state.openFile("Queries/Reading.base", target: .currentTab)
        state.basesFocusQuickFilter()
        XCTAssertEqual(state.baseQuickFilterFocusToken, 1)

        state.openFile("Notes/Alpha.md", target: .currentTab)
        state.basesFocusQuickFilter()
        XCTAssertEqual(
            state.baseQuickFilterFocusToken,
            1,
            "non-base tabs must not claim the scoped quick-filter focus token")
    }

    func testFindRoutingOnlyFocusesBaseQuickFilterFromEditorRegion() async throws {
        let state = try await makeQuickFilterAppState()
        state.openFile("Queries/Reading.base", target: .currentTab)

        state.requestFindInFocusedSurface()
        XCTAssertEqual(state.baseQuickFilterFocusToken, 1)
        XCTAssertFalse(state.isSearchOpen)

        state.workspace.focusLeafRegion()
        state.requestFindInFocusedSurface()
        XCTAssertEqual(
            state.baseQuickFilterFocusToken,
            1,
            "right-pane focus must not be stolen by the active Base tab")
        XCTAssertTrue(state.isSearchOpen, "non-Bases focus falls back to vault search")
    }

    func testBasesWhereAmIIncludesQuickFilterReadback() async throws {
        let state = try await makeQuickFilterAppState()
        state.openFile("Queries/Reading.base", target: .currentTab)
        let doc = state.baseDocument(for: "Queries/Reading.base")
        let session = try XCTUnwrap(state.currentSession)

        _ = doc.applyQuickFilter("CAFE", session: session)

        XCTAssertEqual(
            state.basesWhereAmI(),
            "Base: Reading, view: Reading, quick filter: CAFE")
    }

    func testBaseExportUsesQuickFilterAndCopyMarkdown() async throws {
        let state = try await makeQuickFilterAppState()
        state.openFile("Queries/Reading.base", target: .currentTab)
        let doc = state.baseDocument(for: "Queries/Reading.base")
        let session = try XCTUnwrap(state.currentSession)

        _ = doc.applyQuickFilter("done", session: session)

        XCTAssertEqual(
            try state.basesExportText(format: .csv),
            "file.name,status\r\nBeta.md,done\r\n")
        XCTAssertEqual(
            try state.basesExportText(format: .csv, includeQuickFilter: false),
            "file.name,status\r\nAlpha.md,active\r\nBeta.md,done\r\nCafe.md,café\r\n")
        XCTAssertEqual(
            state.basesCopyViewAsMarkdown(includeQuickFilter: true),
            "| file.name | status |\n| --- | --- |\n| Beta.md | done |\n")
        XCTAssertEqual(state.lastBaseActionAnnouncement, "Copied base view as Markdown.")
    }

    func testQuickFilterLimitMatchesFilteredAndUnfilteredExportRows() async throws {
        let state = try await makeQuickFilterAppState()
        let baseURL = vaultURL.appendingPathComponent("Queries/Reading.base")
        let source = try String(contentsOf: baseURL, encoding: .utf8)
        try source.replacingOccurrences(
            of: "    name: Reading\n",
            with: "    name: Reading\n    limit: 2\n"
        ).write(to: baseURL, atomically: true, encoding: .utf8)
        state.openFile("Queries/Reading.base", target: .currentTab)
        let doc = state.baseDocument(for: "Queries/Reading.base")
        let session = try XCTUnwrap(state.currentSession)

        XCTAssertEqual(doc.applyQuickFilter("cafe", session: session), "1 of 2 results")
        XCTAssertEqual(doc.result?.shownCount, 1)
        XCTAssertEqual(doc.result?.unfilteredShownCount, 2)
        XCTAssertEqual(
            try doc.export(format: .csv, session: session, includeQuickFilter: true),
            "file.name,status\r\nCafe.md,café\r\n")
        XCTAssertEqual(
            try doc.export(format: .csv, session: session, includeQuickFilter: false),
            "file.name,status\r\nAlpha.md,active\r\nBeta.md,done\r\n")
    }

    func testTransientTypedSortDrivesTableListExportAndLifecycle() async throws {
        let state = try await makeTypedSortAppState()
        state.openFile("Queries/Typed.base", target: .currentTab)
        let doc = state.baseDocument(for: "Queries/Typed.base")
        let session = try XCTUnwrap(state.currentSession)
        let initial = try XCTUnwrap(doc.result)
        let localRows = Dictionary(
            uniqueKeysWithValues: initial.rows.enumerated().map {
                ($0.element.filePath, BaseGridRow(row: $0.element, ordinal: $0.offset))
            })
        let beta = try XCTUnwrap(localRows["Notes/Beta.md"])
        let aardvark = try XCTUnwrap(localRows["Notes/Aardvark.md"])
        let alpha = try XCTUnwrap(localRows["Notes/Alpha.md"])
        let null = try XCTUnwrap(localRows["Notes/Null.md"])
        XCTAssertTrue(beta.sortsBefore(aardvark, at: 1), "numbers compare by value, not display")
        XCTAssertTrue(aardvark.sortsBefore(alpha, at: 1), "equal values use the path tiebreak")
        XCTAssertTrue(alpha.sortsBefore(null, at: 1), "nulls sort after typed values")
        XCTAssertTrue(beta.sortsBefore(aardvark, at: 2), "dates compare by epoch")
        XCTAssertTrue(
            aardvark.sortsBefore(beta, at: 1, ascending: false),
            "descending reverses typed values")
        XCTAssertTrue(
            beta.sortsBefore(null, at: 1, ascending: false),
            "nulls remain last descending")
        XCTAssertTrue(
            aardvark.sortsBefore(alpha, at: 1, ascending: false),
            "path ties remain ascending regardless of sort direction")

        doc.setTransientSort(
            DataGridSortState(columnIndex: 1, ascending: true), session: session)
        let numericPaths = [
            "Notes/Beta.md", "Notes/Aardvark.md", "Notes/Alpha.md", "Notes/Null.md",
        ]
        XCTAssertEqual(doc.result?.rows.map(\.filePath), numericPaths)
        XCTAssertEqual(
            try doc.export(format: .csv, session: session)
                .split(whereSeparator: \.isNewline).dropFirst()
                .compactMap { $0.split(separator: ",").first.map(String.init) },
            ["Beta.md", "Aardvark.md", "Alpha.md", "Null.md"])
        XCTAssertEqual(
            BaseListProjection(
                result: try XCTUnwrap(doc.result),
                options: BaseListOptions(slateStateJson: nil)
            ).items.map(\.filePath),
            numericPaths)

        doc.setTransientSort(
            DataGridSortState(columnIndex: 2, ascending: false), session: session)
        XCTAssertEqual(doc.result?.rows.map(\.filePath), [
            "Notes/Aardvark.md", "Notes/Alpha.md", "Notes/Beta.md", "Notes/Null.md",
        ])

        doc.selectView(index: 1, session: session)
        XCTAssertNil(doc.sortState)
        XCTAssertEqual(doc.result?.rows.map(\.filePath), [
            "Notes/Aardvark.md", "Notes/Alpha.md", "Notes/Beta.md", "Notes/Null.md",
        ])
        doc.setTransientSort(
            DataGridSortState(columnIndex: 1, ascending: true), session: session)
        XCTAssertEqual(
            BaseListProjection(
                result: try XCTUnwrap(doc.result),
                options: BaseListOptions(slateStateJson: nil)
            ).items.map(\.filePath),
            numericPaths)

        doc.setTransientSort(nil, session: session)
        XCTAssertNil(doc.sortState)
        XCTAssertEqual(doc.result?.rows.map(\.filePath), [
            "Notes/Aardvark.md", "Notes/Alpha.md", "Notes/Beta.md", "Notes/Null.md",
        ])

        let staleHandle = try XCTUnwrap(doc.handle)
        doc.close(session: session)
        XCTAssertThrowsError(
            try session.baseSetTransientSort(
                handle: staleHandle, view: 0, columnId: "score", ascending: true))
    }

    func testBasePropertyEditUsesExistingWritePathAndReexecutes() async throws {
        let state = try await makeQuickFilterAppState()
        state.openFile("Queries/Reading.base", target: .currentTab)
        let doc = state.baseDocument(for: "Queries/Reading.base")
        let result = try XCTUnwrap(doc.result)
        let row = try XCTUnwrap(result.rows.first { $0.filePath == "Notes/Alpha.md" })
        let status = try XCTUnwrap(result.columns.first { $0.id == "status" })

        let announcement = await state.basesSetProperty(
            row: row,
            column: status,
            value: .text(value: "review"))

        XCTAssertEqual(announcement, "Saved. status: review")
        XCTAssertEqual(state.lastBaseActionAnnouncement, "Saved. status: review")
        XCTAssertTrue(
            try String(
                contentsOf: vaultURL.appendingPathComponent("Notes/Alpha.md"),
                encoding: .utf8)
                .contains("status: review"))
        XCTAssertEqual(
            doc.result?.rows.first { $0.filePath == "Notes/Alpha.md" }?.values[1].display,
            "review")
    }

    func testBasePropertyEditAnnouncesWhenRowLeavesResultSet() async throws {
        let state = try await makeQuickFilterAppState()
        state.openFile("Queries/Reading.base", target: .currentTab)
        let doc = state.baseDocument(for: "Queries/Reading.base")
        let session = try XCTUnwrap(state.currentSession)
        _ = doc.applyQuickFilter("active", session: session)
        let result = try XCTUnwrap(doc.result)
        let row = try XCTUnwrap(result.rows.first { $0.filePath == "Notes/Alpha.md" })
        let status = try XCTUnwrap(result.columns.first { $0.id == "status" })

        let announcement = await state.basesSetProperty(
            row: row,
            column: status,
            value: .text(value: "archived"))

        XCTAssertEqual(announcement, "Saved. Row no longer matches this view")
        XCTAssertTrue(doc.result?.rows.isEmpty == true)
    }

    func testBlankCellCommitRoutesToDeleteAndPreservesNonblankText() async throws {
        let state = try await makeQuickFilterAppState()
        state.openFile("Queries/Reading.base", target: .currentTab)
        let result = try XCTUnwrap(state.activeBaseDocument?.result)
        let row = try XCTUnwrap(result.rows.first { $0.filePath == "Notes/Alpha.md" })
        let status = try XCTUnwrap(result.columns.first { $0.id == "status" })

        let announcement = await state.basesDeleteProperty(row: row, column: status)

        XCTAssertEqual(announcement, "Saved. status: empty")
        let note = try String(
            contentsOf: vaultURL.appendingPathComponent("Notes/Alpha.md"),
            encoding: .utf8)
        XCTAssertFalse(note.contains("status:"), "blank commit must remove the property key")
        XCTAssertEqual(
            BaseCellEditPolicy.propertyValue(from: "  keep exactly  ", valueKind: "text"),
            .success(.text(value: "  keep exactly  ")),
            "nonblank text drafts must not be normalized")

        let source = try sourceFile("Bases/BaseContainerView.swift")
        XCTAssertTrue(source.contains("trimmingCharacters(in: .whitespacesAndNewlines).isEmpty"))
        XCTAssertTrue(
            source.contains("await appState.basesDeleteProperty"),
            "the editable-cell commit path must route blank drafts to deletion")
    }

    func testBaseRowActionsCopyLinkAndShowBacklinks() async throws {
        let state = try await makeQuickFilterAppState()
        state.openFile("Queries/Reading.base", target: .currentTab)
        let row = try XCTUnwrap(
            state.activeBaseDocument?.result?.rows.first { $0.filePath == "Notes/Alpha.md" })

        XCTAssertEqual(state.basesCopyLink(for: row), "[[Notes/Alpha]]")
        XCTAssertEqual(state.lastBaseActionAnnouncement, "Copied link to Alpha.")

        XCTAssertEqual(state.basesShowBacklinks(for: row), "Backlinks for Alpha.")
        XCTAssertEqual(state.selectedFilePath, "Notes/Alpha.md")
        XCTAssertEqual(state.workspace.activeLeaf, .backlinks)
    }

    func testSelectedBaseRowCommandsRequireActiveBaseSurfaceAndDocument() async throws {
        let state = try await makeQuickFilterAppState()
        state.openFile("Queries/Reading.base", target: .currentTab)
        let row = try XCTUnwrap(
            state.activeBaseDocument?.result?.rows.first { $0.filePath == "Notes/Alpha.md" })
        state.activeBaseSelectionPath = "Queries/Reading.base"
        state.activeBaseSelectedRow = row
        state.openFile("Notes/Beta.md", target: .currentTab)

        state.basesCopySelectedLink()

        XCTAssertEqual(state.lastBaseActionAnnouncement, "Select a base row first.")

        state.openFile("Queries/Reading.base", target: .currentTab)
        state.activeBaseSelectionPath = "Queries/Reading.base"
        state.activeBaseSelectedRow = row
        state.openFile("Queries/Other.base", target: .newTab)

        state.basesCopySelectedLink()

        XCTAssertEqual(
            state.lastBaseActionAnnouncement,
            "Select a base row first.",
            "Base commands must not act on another Base document's stale selection")
    }

    func testBaseEditPolicyAllowsOnlyNoteMetadataColumns() {
        XCTAssertEqual(
            BaseCellEditPolicy.propertyKey(
                for: BasesColumn(id: "status", label: "status", valueKind: "text", role: .metadata)),
            "status")
        XCTAssertEqual(
            BaseCellEditPolicy.propertyKey(
                for: BasesColumn(
                    id: "note.priority", label: "priority", valueKind: "number", role: .metadata)),
            "priority")
        XCTAssertNil(
            BaseCellEditPolicy.propertyKey(
                for: BasesColumn(
                    id: "file.name", label: "Name", valueKind: "text", role: .identifier)))
        XCTAssertNil(
            BaseCellEditPolicy.propertyKey(
                for: BasesColumn(
                    id: "formula.score", label: "Score", valueKind: "number", role: .metric)))
        XCTAssertNil(
            BaseCellEditPolicy.propertyKey(
                for: BasesColumn(
                    id: "task.status", label: "Task status", valueKind: "text", role: .metadata)))
    }

    func testBaseEditPolicyConvertsDisplayedValueKinds() {
        XCTAssertEqual(
            BaseCellEditPolicy.propertyValue(from: "42", valueKind: "number"),
            .success(.integer(value: 42)))
        XCTAssertEqual(
            BaseCellEditPolicy.propertyValue(from: "3.5", valueKind: "number"),
            .success(.float(value: 3.5)))
        XCTAssertEqual(
            BaseCellEditPolicy.propertyValue(from: "Project/Alpha", valueKind: "wikilink"),
            .success(.wikilink(target: "Project/Alpha")))
        XCTAssertEqual(
            BaseCellEditPolicy.displayValue(.float(value: 2.2999999999999998)),
            "2.3")
    }

    func testBaseSelectionRestorerKeepsSurvivingRowElseFallsBackToFirst() {
        XCTAssertEqual(
            BaseSelectionRestorer.restoredSelection(
                previous: "Notes/Beta.md",
                availableIDs: ["Notes/Alpha.md", "Notes/Beta.md"]),
            "Notes/Beta.md")
        XCTAssertEqual(
            BaseSelectionRestorer.restoredSelection(
                previous: "Notes/Beta.md",
                availableIDs: ["Notes/Alpha.md"]),
            "Notes/Alpha.md")
        XCTAssertEqual(
            BaseSelectionRestorer.restoredSelection(
                previous: "Notes/Beta.md",
                current: "Notes/Cafe.md",
                availableIDs: ["Notes/Alpha.md", "Notes/Cafe.md"]),
            "Notes/Cafe.md")
        XCTAssertNil(
            BaseSelectionRestorer.restoredSelection(
                previous: "Notes/Beta.md",
                availableIDs: []))
    }

    func testBaseQuickFilterEscapeRestoresNativeResultFocus() throws {
        let baseContainer = try sourceFile("Bases/BaseContainerView.swift")
        let grid = try sourceFile("AccessibleDataGrid.swift")
        let list = try sourceFile("Bases/BaseListRenderer.swift")

        XCTAssertTrue(baseContainer.contains("resultFocusToken &+="))
        XCTAssertTrue(baseContainer.contains("focusRequest: resultFocusToken"))
        XCTAssertTrue(grid.contains("makeFirstResponder(table)"))
        XCTAssertTrue(list.contains("makeFirstResponder(outline)"))
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

    func testSaveSortPreservesOwnerStateWhileReloadingSameBaseDockMetadata() async throws {
        let fixture = try await makeLivePropertySurfacesState()
        fixture.state.dockBaseFileToSidebar(
            path: "Queries/Edit.base",
            refreshDelayNanoseconds: 0)
        await fixture.state.basesDockRefreshTask?.value
        let dock = try XCTUnwrap(fixture.state.basesDockDocument)
        let ownerHandle = try XCTUnwrap(fixture.active.handle)
        let dockHandle = try XCTUnwrap(dock.handle)
        XCTAssertFalse(
            fixture.active.views[1].slateStateJson?.contains(#""sort""#) == true)
        XCTAssertEqual(fixture.active.result?.rows.map(\.filePath), ["Notes/Alpha.md"])

        fixture.state.basesSaveSortToView()

        XCTAssertEqual(
            fixture.active.handle,
            ownerHandle,
            "saveSortToView already refreshes its handle; the shared funnel must not reopen it")
        XCTAssertEqual(fixture.active.activeViewIndex, 1)
        XCTAssertEqual(fixture.active.quickFilterText, "Alpha")
        XCTAssertEqual(
            fixture.active.sortState,
            DataGridSortState(columnIndex: 1, ascending: false))
        XCTAssertEqual(fixture.active.result?.rows.map(\.filePath), ["Notes/Alpha.md"])
        XCTAssertEqual(fixture.state.activeBaseSelectedRow?.filePath, "Notes/Alpha.md")
        XCTAssertEqual(fixture.state.activeBaseSelectedColumn?.id, "status")

        XCTAssertNotEqual(
            dock.handle,
            dockHandle,
            "the separately opened dock handle must reload the edited .base definition")
        let ownerSlateState = try XCTUnwrap(fixture.active.views[1].slateStateJson)
        let dockSlateState = try XCTUnwrap(dock.views[1].slateStateJson)
        XCTAssertEqual(dockSlateState, ownerSlateState)
        XCTAssertTrue(dockSlateState.contains(#""sort""#), dockSlateState)
        XCTAssertTrue(dockSlateState.contains(#""property":"status""#), dockSlateState)
        XCTAssertTrue(dockSlateState.contains(#""direction":"DESC""#), dockSlateState)
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
            "Notes/Alpha.md",
            "Notes/Beta.md",
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

    func testGridAndListEntryUseResultAudioSummaryAndListSectionsAreHeadings() throws {
        let result = Self.sampleBaseResult()
        let projection = BaseListProjection(
            result: result,
            options: BaseListOptions(slateStateJson: nil))
        var selection: String?
        let list = BaseListView(
            projection: projection,
            selection: Binding(get: { selection }, set: { selection = $0 }),
            onActivate: { _ in })
        let host = NSHostingView(rootView: list)
        host.frame = NSRect(x: 0, y: 0, width: 640, height: 480)
        host.layoutSubtreeIfNeeded()

        let outline = try XCTUnwrap(firstSubview(of: NSOutlineView.self, in: host))
        outline.reloadData()
        outline.layoutSubtreeIfNeeded()
        XCTAssertEqual(outline.accessibilityLabel(), result.audioSummary)
        let section = try XCTUnwrap(
            outline.view(atColumn: 0, row: 0, makeIfNecessary: true))
        XCTAssertEqual(
            section.accessibilityRole(),
            NSAccessibility.Role(rawValue: "AXHeading"))

        XCTAssertTrue(
            try sourceFile("Bases/BaseContainerView.swift")
                .contains("accessibilityLabel: result.audioSummary"),
            "grid entry must expose the engine result audio summary")
    }

    func testNativeListAudioSummaryRefreshesWhenProjectionUpdates() throws {
        let result = Self.sampleBaseResult()
        var selection: String?
        let selectionBinding = Binding<String?>(
            get: { selection }, set: { selection = $0 })
        let list = BaseListView(
            projection: BaseListProjection(
                result: result,
                options: BaseListOptions(slateStateJson: nil)),
            selection: selectionBinding,
            onActivate: { _ in })
        let host = NSHostingView(rootView: list)
        host.frame = NSRect(x: 0, y: 0, width: 640, height: 480)
        host.layoutSubtreeIfNeeded()

        let outline = try XCTUnwrap(firstSubview(of: NSOutlineView.self, in: host))
        XCTAssertEqual(outline.accessibilityLabel(), "2 notes.")

        var filteredResult = result
        filteredResult.rows = Array(result.rows.prefix(1))
        filteredResult.groups = Array(result.groups.prefix(1))
        filteredResult.totalCount = 1
        filteredResult.shownCount = 1
        filteredResult.unfilteredShownCount = 1
        filteredResult.audioSummary = "1 note."
        host.rootView = BaseListView(
            projection: BaseListProjection(
                result: filteredResult,
                options: BaseListOptions(slateStateJson: nil)),
            selection: selectionBinding,
            onActivate: { _ in })
        host.layoutSubtreeIfNeeded()

        let updatedOutline = try XCTUnwrap(firstSubview(of: NSOutlineView.self, in: host))
        XCTAssertTrue(outline === updatedOutline, "the probe must exercise updateNSView, not makeNSView")
        XCTAssertEqual(updatedOutline.accessibilityLabel(), "1 note.")
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
        XCTAssertEqual(display.selectionID(at: 2), "Notes/Alpha.md")
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
            unfilteredShownCount: 2,
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

    private func sourceFile(_ relativePath: String) throws -> String {
        let root = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
        return try String(
            contentsOf: root
                .appendingPathComponent("apps/slate-mac/Sources/SlateMac")
                .appendingPathComponent(relativePath),
            encoding: .utf8)
    }

    private func firstSubview<T: NSView>(of type: T.Type, in root: NSView) -> T? {
        if let match = root as? T { return match }
        for child in root.subviews {
            if let match = firstSubview(of: type, in: child) { return match }
        }
        return nil
    }
}
