// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// N4-3 (#709): saved-query sidebar state and palette lifecycle.
@MainActor
final class BaseQueriesPanelTests: XCTestCase {
    private var tempDir: URL!
    private var defaults: UserDefaults!
    private var defaultsSuiteName: String!

    override func setUpWithError() throws {
        try super.setUpWithError()
        tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-base-queries-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: tempDir, withIntermediateDirectories: true)
        defaultsSuiteName = "slate-base-queries-\(UUID().uuidString)"
        defaults = UserDefaults(suiteName: defaultsSuiteName)
        defaults.removePersistentDomain(forName: defaultsSuiteName)
    }

    override func tearDownWithError() throws {
        if let defaultsSuiteName {
            defaults.removePersistentDomain(forName: defaultsSuiteName)
        }
        try? FileManager.default.removeItem(at: tempDir)
        try super.tearDownWithError()
    }

    func testSavedQueriesListPinsAndBaseFilesRefreshFromVault() async throws {
        let (state, session) = try await makeState()
        let active = try session.saveQuery(
            name: "Active projects",
            description: "Open work",
            queryJson: queryJSON(folder: "Projects"),
            sourceSyntax: .builder)
        let backlog = try session.saveQuery(
            name: "Backlog",
            description: nil,
            queryJson: queryJSON(folder: "Backlog"),
            sourceSyntax: .builder)
        try session.saveQueryAsBase(
            queryJson: queryJSON(folder: "Projects"),
            path: "Queries/Complete.base")
        try session.scanInitial(cancel: CancelToken())

        state.refreshBaseQueries()
        XCTAssertEqual(state.baseQueries.savedQueries.map(\.name), ["Active projects", "Backlog"])
        XCTAssertEqual(state.baseQueries.baseFiles.map(\.path), ["Queries/Complete.base"])
        XCTAssertEqual(state.baseQueriesAccessibilityValue, "Queries, 3 items, 0 pinned")

        state.toggleSavedQueryPin(id: backlog)
        XCTAssertEqual(state.baseQueries.pinnedSavedQueryIDs, [backlog])
        XCTAssertEqual(state.orderedSavedQuerySummaries.map(\.id), [backlog, active])
        XCTAssertEqual(state.baseQueriesAccessibilityValue, "Queries, 3 items, 1 pinned")

        let reloaded = PreferencesStore(defaults: defaults).loadBaseQueryPrefs()
        XCTAssertEqual(reloaded.pinnedSavedQueryIDs, [backlog])
    }

    func testDashboardsRefreshAndOpenAsTabs() async throws {
        let (state, session) = try await makeState()
        let queryID = try session.saveQuery(
            name: "Active projects",
            description: "Open work",
            queryJson: queryJSON(folder: "Projects"),
            sourceSyntax: .builder)
        let dashboardID = try session.saveDashboard(
            name: "Overview",
            sections: [
                DashboardSection(
                    savedQueryId: queryID,
                    headingOverride: "Pinned work",
                    viewOverride: nil)
            ])

        state.refreshBaseQueries()

        XCTAssertEqual(state.baseQueries.dashboards.map(\.id), [dashboardID])
        XCTAssertEqual(state.baseQueries.dashboards.map(\.name), ["Overview"])
        XCTAssertEqual(state.baseQueriesAccessibilityValue, "Queries, 2 items, 0 pinned")

        state.openDashboard(id: dashboardID, name: "Overview")

        XCTAssertEqual(state.workspace.activeTab?.item, .dashboard(id: dashboardID, name: "Overview"))
        let document = try XCTUnwrap(state.activeDashboardDocument)
        XCTAssertEqual(document.dashboard?.name, "Overview")
        XCTAssertEqual(document.sections.map(\.title), ["Pinned work"])
        XCTAssertFalse(document.sections[0].isMissing)
    }

    func testDashboardEditorDraftSavesUpdatesAndReordersSections() async throws {
        let (state, session) = try await makeState()
        let activeID = try session.saveQuery(
            name: "Active projects",
            description: nil,
            queryJson: queryJSON(folder: "Projects"),
            sourceSyntax: .builder)
        let backlogID = try session.saveQuery(
            name: "Backlog",
            description: nil,
            queryJson: queryJSON(folder: "Backlog"),
            sourceSyntax: .builder)
        state.refreshBaseQueries()

        var draft = DashboardEditorDraft(savedQueries: state.baseQueries.savedQueries)
        draft.name = "Team overview"
        draft.addSelectedSavedQuery(from: state.baseQueries.savedQueries)
        let activeSectionID = draft.sections[0].id
        draft.selectedSavedQueryID = backlogID
        draft.addSelectedSavedQuery(from: state.baseQueries.savedQueries)
        let backlogSectionID = draft.sections[1].id
        draft.moveSection(from: 1, to: 0)
        let activeIndexAfterMove = try XCTUnwrap(draft.sectionIndex(id: activeSectionID))
        draft.sections[activeIndexAfterMove].headingOverride = "Pinned work"
        draft.sections[activeIndexAfterMove].viewOverride = "Preview"
        let backlogIndexAfterMove = try XCTUnwrap(draft.sectionIndex(id: backlogSectionID))
        XCTAssertEqual(draft.sections[backlogIndexAfterMove].headingOverride, "")

        let dashboardID = try XCTUnwrap(
            state.saveDashboard(name: draft.name, sections: draft.dashboardSections))
        let saved = try session.getDashboard(id: dashboardID)
        XCTAssertEqual(saved.name, "Team overview")
        XCTAssertEqual(saved.sections.map(\.savedQueryId), [backlogID, activeID])
        XCTAssertEqual(saved.sections[1].headingOverride, "Pinned work")
        XCTAssertEqual(saved.sections[1].viewOverride, "Preview")

        var editDraft = DashboardEditorDraft(
            dashboard: saved,
            savedQueries: state.baseQueries.savedQueries)
        editDraft.name = "Renamed overview"
        editDraft.moveSection(from: 0, to: 1)
        editDraft.sections[0].headingOverride = "Still pinned"
        state.updateDashboard(
            id: dashboardID,
            name: editDraft.name,
            sections: editDraft.dashboardSections)

        let updated = try session.getDashboard(id: dashboardID)
        XCTAssertEqual(updated.name, "Renamed overview")
        XCTAssertEqual(updated.sections.map(\.savedQueryId), [activeID, backlogID])
        XCTAssertEqual(updated.sections[0].headingOverride, "Still pinned")
    }

    func testDockedSavedQueryUsesActiveNoteThisPathAndRefreshesOnNoteSwitch() async throws {
        let (state, session) = try await makeState()
        let expressionJSON = try XCTUnwrap(
            session.validateBaseExpression(source: "this.file.name").exprJson)
        var draft = BaseQueryBuilderDraft()
        draft.source = .folder("Projects")
        draft.formulas = [
            try BaseQueryFormula(
                name: "activeNote",
                expression: "this.file.name",
                expressionJSON: expressionJSON)
        ]
        draft.columns = [
            BaseQueryColumn(property: .file(.name), displayName: nil),
            BaseQueryColumn(property: .formula("activeNote"), displayName: "Active note"),
        ]
        let queryID = try session.saveQuery(
            name: "Context query",
            description: nil,
            queryJson: draft.queryJSON(),
            sourceSyntax: .builder)

        state.refreshBaseQueries()
        state.selectedFilePath = "Projects/Alpha.md"
        state.dockSavedQueryToSidebar(id: queryID, refreshDelayNanoseconds: 0)
        await state.basesDockRefreshTask?.value

        XCTAssertEqual(state.workspace.activeLeaf, .basesDock)
        XCTAssertEqual(state.basesDock.target, .savedQuery(id: queryID, name: "Context query"))
        var dockDocument = try XCTUnwrap(state.basesDockDocument)
        XCTAssertEqual(try dockedActiveNoteValue(dockDocument), "Alpha.md")

        state.selectedFilePath = "Projects/Beta.md"
        state.scheduleBasesDockFollowActiveRefresh(delayNanoseconds: 0)
        await state.basesDockRefreshTask?.value

        dockDocument = try XCTUnwrap(state.basesDockDocument)
        XCTAssertEqual(try dockedActiveNoteValue(dockDocument), "Beta.md")

        try session.saveQueryAsBase(
            queryJson: queryJSON(folder: "Projects"),
            path: "Queries/Docked.base")
        state.openBaseFile("Queries/Docked.base")
        state.scheduleBasesDockFollowActiveRefresh(delayNanoseconds: 0)
        await state.basesDockRefreshTask?.value

        XCTAssertNil(state.basesDock.thisPath)
        dockDocument = try XCTUnwrap(state.basesDockDocument)
        guard case .degraded(let message) = dockDocument.state else {
            return XCTFail("expected docked saved query to degrade without an active note, got \(dockDocument.state)")
        }
        XCTAssertTrue(message.contains("this is unavailable in this evaluation context"), message)
    }

    func testDockFollowActiveAnnouncesEmptyToNonemptyMembership() async throws {
        let (state, session) = try await makeState()
        let expressionJSON = try XCTUnwrap(
            session.validateBaseExpression(source: "file.hasLink(this)").exprJson)
        var draft = BaseQueryBuilderDraft()
        draft.rows = [
            .advanced(
                rawExpression: "file.hasLink(this)",
                filterJSON: #"{"Stmt":\#(expressionJSON)}"#)
        ]
        let queryID = try session.saveQuery(
            name: "Backlinks",
            description: nil,
            queryJson: draft.queryJSON(),
            sourceSyntax: .builder)

        state.refreshBaseQueries()
        state.selectedFilePath = "Projects/Alpha.md"
        state.dockSavedQueryToSidebar(id: queryID, refreshDelayNanoseconds: 0)
        await state.basesDockRefreshTask?.value
        XCTAssertTrue(try XCTUnwrap(state.basesDockDocument?.result).rows.isEmpty)
        XCTAssertTrue(state.basesDock.hasPublishedBaseline)

        state.lastBaseActionAnnouncement = nil
        state.selectedFilePath = "Projects/Beta.md"
        state.scheduleBasesDockFollowActiveRefresh(delayNanoseconds: 0)
        await state.basesDockRefreshTask?.value

        XCTAssertEqual(
            try XCTUnwrap(state.basesDockDocument?.result).rows.map(\.filePath),
            ["Projects/Linker.md"])
        XCTAssertEqual(
            state.lastBaseActionAnnouncement,
            "Base dock updated for active note.")
    }

    func testDeletingDashboardClosesOpenDashboardTabsAndDock() async throws {
        let (state, session) = try await makeState()
        let queryID = try session.saveQuery(
            name: "Active projects",
            description: nil,
            queryJson: queryJSON(folder: "Projects"),
            sourceSyntax: .builder)
        let dashboardID = try session.saveDashboard(
            name: "Overview",
            sections: [
                DashboardSection(
                    savedQueryId: queryID,
                    headingOverride: nil,
                    viewOverride: nil)
            ])

        state.refreshBaseQueries()
        state.openDashboard(id: dashboardID, name: "Overview")
        state.dockDashboardToSidebar(id: dashboardID, refreshDelayNanoseconds: 0)
        await state.basesDockRefreshTask?.value

        XCTAssertEqual(state.workspace.activeTab?.item, .dashboard(id: dashboardID, name: "Overview"))
        XCTAssertEqual(state.basesDock.target, .dashboard(id: dashboardID, name: "Overview"))

        state.deleteDashboard(id: dashboardID)

        XCTAssertFalse(state.workspace.model.allTabs.contains { tab in
            if case .dashboard(let id, _) = tab.item {
                return id == dashboardID
            }
            return false
        })
        XCTAssertNil(state.dashboardDocuments[dashboardID])
        XCTAssertNil(state.basesDock.target)
        XCTAssertNil(state.basesDockDashboardDocument)
    }

    func testDeletingSavedQueryReloadsOpenDashboardsAsMissingSections() async throws {
        let (state, session) = try await makeState()
        let queryID = try session.saveQuery(
            name: "Active projects",
            description: nil,
            queryJson: queryJSON(folder: "Projects"),
            sourceSyntax: .builder)
        let dashboardID = try session.saveDashboard(
            name: "Overview",
            sections: [
                DashboardSection(
                    savedQueryId: queryID,
                    headingOverride: nil,
                    viewOverride: nil)
            ])

        state.refreshBaseQueries()
        state.openDashboard(id: dashboardID, name: "Overview")
        let openDocument = try XCTUnwrap(state.activeDashboardDocument)
        XCTAssertFalse(openDocument.sections[0].isMissing)
        state.dockDashboardToSidebar(id: dashboardID, refreshDelayNanoseconds: 0)
        await state.basesDockRefreshTask?.value

        state.deleteSavedQuery(id: queryID)

        XCTAssertTrue(openDocument.sections[0].isMissing)
        XCTAssertEqual(openDocument.sections[0].title, "Missing saved query")
        let dockedDocument = try XCTUnwrap(state.basesDockDashboardDocument)
        XCTAssertTrue(dockedDocument.sections[0].isMissing)
        XCTAssertEqual(dockedDocument.sections[0].title, "Missing saved query")
    }

    func testMissingDashboardSectionActionsPersistReplacementAndRemoval() async throws {
        let (state, session) = try await makeState()
        let missingOne = try session.saveQuery(
            name: "Missing one",
            description: nil,
            queryJson: queryJSON(folder: "Projects"),
            sourceSyntax: .builder)
        let missingTwo = try session.saveQuery(
            name: "Missing two",
            description: nil,
            queryJson: queryJSON(folder: "Backlog"),
            sourceSyntax: .builder)
        let replacement = try session.saveQuery(
            name: "Replacement",
            description: nil,
            queryJson: queryJSON(folder: "Projects"),
            sourceSyntax: .builder)
        let dashboardID = try session.saveDashboard(
            name: "Repairable",
            sections: [
                DashboardSection(
                    savedQueryId: missingOne,
                    headingOverride: "Keep this heading",
                    viewOverride: "Keep this view"),
                DashboardSection(
                    savedQueryId: missingTwo,
                    headingOverride: "Remove me",
                    viewOverride: nil),
            ])

        state.refreshBaseQueries()
        state.openDashboard(id: dashboardID, name: "Repairable")
        state.dockDashboardToSidebar(id: dashboardID, refreshDelayNanoseconds: 0)
        await state.basesDockRefreshTask?.value
        state.deleteSavedQuery(id: missingOne)
        state.deleteSavedQuery(id: missingTwo)
        XCTAssertTrue(try XCTUnwrap(state.activeDashboardDocument).sections.allSatisfy(\.isMissing))

        let replacementSnapshot = try XCTUnwrap(state.activeDashboardDocument)
            .editableSectionsSnapshot
        state.replaceMissingDashboardSection(
            dashboardID: dashboardID,
            index: 0,
            expectedSections: replacementSnapshot,
            replacementSavedQueryID: replacement)
        let removalSnapshot = try XCTUnwrap(state.activeDashboardDocument)
            .editableSectionsSnapshot
        state.removeMissingDashboardSection(
            dashboardID: dashboardID,
            index: 1,
            expectedSections: removalSnapshot)

        let updated = try session.getDashboard(id: dashboardID)
        XCTAssertEqual(updated.sections.count, 1)
        XCTAssertEqual(updated.sections[0].savedQueryId, replacement)
        XCTAssertEqual(updated.sections[0].headingOverride, "Keep this heading")
        XCTAssertEqual(updated.sections[0].viewOverride, "Keep this view")
        XCTAssertFalse(try XCTUnwrap(state.activeDashboardDocument).sections[0].isMissing)
        XCTAssertFalse(try XCTUnwrap(state.basesDockDashboardDocument).sections[0].isMissing)
    }

    func testMissingDashboardSectionActionsRejectStaleDuplicateIDReorder() async throws {
        let (state, session) = try await makeState()
        let missing = try session.saveQuery(
            name: "Missing duplicate",
            description: nil,
            queryJson: queryJSON(folder: "Projects"),
            sourceSyntax: .builder)
        let replacement = try session.saveQuery(
            name: "Replacement",
            description: nil,
            queryJson: queryJSON(folder: "Backlog"),
            sourceSyntax: .builder)
        let first = DashboardSection(
            savedQueryId: missing,
            headingOverride: "First copy",
            viewOverride: "Table")
        let second = DashboardSection(
            savedQueryId: missing,
            headingOverride: "Second copy",
            viewOverride: "List")
        let dashboardID = try session.saveDashboard(
            name: "Duplicate repair",
            sections: [first, second])

        state.refreshBaseQueries()
        state.openDashboard(id: dashboardID, name: "Duplicate repair")
        state.deleteSavedQuery(id: missing)
        let staleSnapshot = try XCTUnwrap(state.activeDashboardDocument)
            .editableSectionsSnapshot
        XCTAssertEqual(staleSnapshot, [first, second])

        try session.updateDashboardSections(id: dashboardID, sections: [second, first])

        state.replaceMissingDashboardSection(
            dashboardID: dashboardID,
            index: 0,
            expectedSections: staleSnapshot,
            replacementSavedQueryID: replacement)
        state.removeMissingDashboardSection(
            dashboardID: dashboardID,
            index: 1,
            expectedSections: staleSnapshot)

        let persisted = try session.getDashboard(id: dashboardID)
        XCTAssertEqual(persisted.sections.map(\.savedQueryId), [missing, missing])
        XCTAssertEqual(persisted.sections.map(\.headingOverride), ["Second copy", "First copy"])
        XCTAssertEqual(persisted.sections.map(\.viewOverride), ["List", "Table"])
        XCTAssertEqual(
            state.lastBaseActionAnnouncement,
            "Dashboard section changed; reload and try again.")
    }

    func testSavedQueryPaletteCommandsRefreshOnRenameAndDelete() async throws {
        let (state, session) = try await makeState()
        let id = try session.saveQuery(
            name: "Active projects",
            description: nil,
            queryJson: queryJSON(folder: "Projects"),
            sourceSyntax: .builder)

        state.refreshBaseQueries()
        let commandID = SlateCommandID.basesRunSavedQuery(id: id)
        XCTAssertEqual(state.commandRegistry.findById(id: commandID)?.label, "Run query: Active projects")
        XCTAssertEqual(state.commandRegistry.findById(id: commandID)?.section, .bases)
        XCTAssertNil(state.commandRegistry.findById(id: commandID)?.hotkeyHint)

        try state.commandRegistry.invokeById(id: commandID)
        XCTAssertEqual(state.workspace.activeTab?.item, .savedQuery(id: id, name: "Active projects"))

        try session.renameSavedQuery(id: id, name: "Renamed")
        state.refreshBaseQueries()
        XCTAssertEqual(state.commandRegistry.findById(id: commandID)?.label, "Run query: Renamed")

        try session.deleteSavedQuery(id: id)
        state.refreshBaseQueries()
        XCTAssertNil(state.commandRegistry.findById(id: commandID))
    }

    func testEditingSavedQueryBuilderUpdatesOriginalSavedQuery() async throws {
        let (state, session) = try await makeState()
        let id = try session.saveQuery(
            name: "Active projects",
            description: "Open work",
            queryJson: queryJSON(folder: "Projects"),
            sourceSyntax: .builder)

        state.refreshBaseQueries()
        state.editSavedQueryInBuilder(id: id)
        let model = try XCTUnwrap(state.activeBaseQueryBuilder)
        XCTAssertEqual(model.editingSavedQuery?.id, id)
        XCTAssertEqual(model.editingSavedQuery?.name, "Active projects")

        model.source = .folder("Backlog")
        state.basesBuilderUpdateSavedQuery()

        let saved = try session.getSavedQuery(id: id)
        let draft = try BaseQueryBuilderDraft(queryJSON: saved.queryJson)
        XCTAssertEqual(draft.source, .folder("Backlog"))
        XCTAssertEqual(saved.description, "Open work")
        XCTAssertEqual(try session.listSavedQueries().map(\.id), [id])
        XCTAssertEqual(
            state.commandRegistry.findById(id: SlateCommandID.basesRunSavedQuery(id: id))?.label,
            "Run query: Active projects")
    }

    func testDeletingSavedQueryClosesOpenTabsAndDocument() async throws {
        let (state, session) = try await makeState()
        let id = try session.saveQuery(
            name: "Active projects",
            description: nil,
            queryJson: queryJSON(folder: "Projects"),
            sourceSyntax: .builder)

        state.refreshBaseQueries()
        state.openSavedQuery(id: id, name: "Active projects")
        XCTAssertTrue(state.workspace.model.allTabs.contains { tab in
            tab.item == .savedQuery(id: id, name: "Active projects")
        })
        XCTAssertNotNil(state.baseDocuments[BaseDocumentSource.savedQuery(id: id, name: "Active projects").key])

        state.deleteSavedQuery(id: id)

        XCTAssertFalse(state.workspace.model.allTabs.contains { tab in
            if case .savedQuery(let queryID, _) = tab.item {
                return queryID == id
            }
            return false
        })
        XCTAssertNil(state.baseDocuments[BaseDocumentSource.savedQuery(id: id, name: "Active projects").key])
    }

    func testThisQueryInPlainSavedQueryTabSurfacesDockHint() async throws {
        let (state, session) = try await makeState()
        let exprJSON = try XCTUnwrap(
            session.validateBaseExpression(source: "this.file.name").exprJson)
        var draft = BaseQueryBuilderDraft()
        draft.source = .folder("Projects")
        draft.formulas = [
            try BaseQueryFormula(
                name: "thisName",
                expression: "this.file.name",
                expressionJSON: exprJSON)
        ]
        draft.columns = [
            BaseQueryColumn(property: .file(.name), displayName: nil),
            BaseQueryColumn(property: .formula("thisName"), displayName: nil),
        ]
        let id = try session.saveQuery(
            name: "Context query",
            description: nil,
            queryJson: draft.queryJSON(),
            sourceSyntax: .builder)

        state.refreshBaseQueries()
        state.openSavedQuery(id: id, name: "Context query")
        let doc = try XCTUnwrap(state.activeBaseDocument)
        guard case .degraded(let message) = doc.state else {
            return XCTFail("expected degraded state, got \(doc.state)")
        }
        XCTAssertTrue(message.contains("this is unavailable in this evaluation context"), message)
        XCTAssertTrue(message.contains("Dock to sidebar to follow the active note."), message)
    }

    private func makeState() async throws -> (AppState, VaultSession) {
        let vault = tempDir.appendingPathComponent("vault")
        try FileManager.default.createDirectory(
            at: vault.appendingPathComponent("Projects"), withIntermediateDirectories: true)
        try FileManager.default.createDirectory(
            at: vault.appendingPathComponent("Backlog"), withIntermediateDirectories: true)
        try FileManager.default.createDirectory(
            at: vault.appendingPathComponent("Queries"), withIntermediateDirectories: true)
        try "---\nstatus: active\n---\n# Alpha\n".write(
            to: vault.appendingPathComponent("Projects/Alpha.md"),
            atomically: true,
            encoding: .utf8)
        try "---\nstatus: active\n---\n# Beta\n".write(
            to: vault.appendingPathComponent("Projects/Beta.md"),
            atomically: true,
            encoding: .utf8)
        try "# Linker\n\n[[Projects/Beta]]\n".write(
            to: vault.appendingPathComponent("Projects/Linker.md"),
            atomically: true,
            encoding: .utf8)
        let state = AppState(
            recentsStore: RecentVaultsStore(fileURL: tempDir.appendingPathComponent("recents.json")),
            externalOpener: { _ in true },
            preferencesStore: PreferencesStore(defaults: defaults))
        state.openVault(at: vault)
        await state.scanTask?.value
        return (state, try XCTUnwrap(state.currentSession))
    }

    private func queryJSON(folder: String) throws -> String {
        var draft = BaseQueryBuilderDraft()
        draft.source = .folder(folder)
        return try draft.queryJSON()
    }

    private func dockedActiveNoteValue(_ document: BaseDocument) throws -> String {
        let row = try XCTUnwrap(document.result?.rows.first)
        XCTAssertGreaterThan(row.values.count, 1)
        return row.values[1].display
    }
}
