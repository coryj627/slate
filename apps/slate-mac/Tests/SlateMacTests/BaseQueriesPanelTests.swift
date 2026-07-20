// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// N4-3 (#709): saved-query sidebar state and palette lifecycle.
@MainActor
final class BaseQueriesPanelTests: XCTestCase {
    private final class NativeEventRecorder: @unchecked Sendable {
        private let lock = NSLock()
        private var eventsStorage: [BaseRetargetNativeExecutionEvent] = []

        func record(_ event: BaseRetargetNativeExecutionEvent) {
            lock.lock()
            eventsStorage.append(event)
            lock.unlock()
        }

        var events: [BaseRetargetNativeExecutionEvent] {
            lock.lock()
            defer { lock.unlock() }
            return eventsStorage
        }
    }

    func testBaseFileListIDsKeepCanonicallyEquivalentPathsDistinct() {
        let composed = BaseFileSummary(
            path: "Queries/é.base", name: "é", viewCount: 1,
            warningCount: 0, degraded: false, indexedAtMs: 1)
        let decomposed = BaseFileSummary(
            path: "Queries/e\u{301}.base", name: "e\u{301}", viewCount: 1,
            warningCount: 0, degraded: false, indexedAtMs: 1)

        let entries = BaseFileListEntry.make([composed, decomposed])

        XCTAssertEqual(entries.count, 2)
        XCTAssertNotEqual(entries[0].id, entries[1].id)
    }

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

    private static func sourceFile(_ relativePath: String) throws -> String {
        var cursor = URL(fileURLWithPath: #filePath).deletingLastPathComponent()
        while cursor.path != "/" {
            let candidate = cursor.appendingPathComponent(relativePath)
            if FileManager.default.fileExists(atPath: candidate.path) {
                return try String(contentsOf: candidate, encoding: .utf8)
            }
            cursor.deleteLastPathComponent()
        }
        throw CocoaError(.fileNoSuchFile)
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

        _ = await state.refreshBaseQueries()?.value
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

        _ = await state.refreshBaseQueries()?.value

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
        _ = await state.refreshBaseQueries()?.value

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
        draft.sections[activeIndexAfterMove].viewOverride = "List"
        let backlogIndexAfterMove = try XCTUnwrap(draft.sectionIndex(id: backlogSectionID))
        XCTAssertEqual(draft.sections[backlogIndexAfterMove].headingOverride, "")

        let dashboardID = try XCTUnwrap(
            state.saveDashboard(name: draft.name, sections: draft.dashboardSections))
        let saved = try session.getDashboard(id: dashboardID)
        XCTAssertEqual(saved.name, "Team overview")
        XCTAssertEqual(saved.sections.map(\.savedQueryId), [backlogID, activeID])
        XCTAssertEqual(saved.sections[1].headingOverride, "Pinned work")
        XCTAssertEqual(saved.sections[1].viewOverride, "List")

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

    func testDashboardEditorSaveUsesSingleAtomicSessionMutation() throws {
        let source = try Self.sourceFile("Sources/SlateMac/Bases/AppState+Bases.swift")

        XCTAssertEqual(
            source.components(separatedBy: "try session.updateDashboard(").count - 1,
            1,
            "dashboard editor save must cross the FFI boundary exactly once")
        XCTAssertFalse(
            source.contains("try session.renameDashboard("),
            "dashboard editor save must not persist the name separately")
        XCTAssertFalse(
            source.contains("try session.updateDashboardSections("),
            "dashboard editor save must not persist sections separately")
    }

    func testDashboardEditorKeepsDraftWhenAtomicUpdateFails() async throws {
        let (state, session) = try await makeState()
        let firstID = try session.saveDashboard(name: "Existing", sections: [])
        let secondID = try session.saveDashboard(name: "Draft", sections: [])

        XCTAssertFalse(
            state.updateDashboard(id: secondID, name: "Existing", sections: []),
            "a duplicate name must report failure to the editor")
        XCTAssertEqual(try session.getDashboard(id: firstID).name, "Existing")
        XCTAssertEqual(try session.getDashboard(id: secondID).name, "Draft")

        let source = try Self.sourceFile("Sources/SlateMac/Bases/BaseQueriesPanel.swift")
        XCTAssertTrue(source.contains("if didSave {\n            dashboardDraft = nil"), source)
    }

    func testDashboardEditorDraftCanonicalizesStoredTableAndListOverrides() {
        let dashboard = Dashboard(
            id: "dashboard",
            name: "Renderer overrides",
            sections: [
                DashboardSectionStatus(
                    savedQueryId: "lower-table",
                    savedQueryName: "Lower table",
                    headingOverride: nil,
                    viewOverride: "table",
                    missing: false),
                DashboardSectionStatus(
                    savedQueryId: "trimmed-table",
                    savedQueryName: "Trimmed table",
                    headingOverride: nil,
                    viewOverride: " TABLE ",
                    missing: false),
                DashboardSectionStatus(
                    savedQueryId: "lower-list",
                    savedQueryName: "Lower list",
                    headingOverride: nil,
                    viewOverride: "list",
                    missing: false),
                DashboardSectionStatus(
                    savedQueryId: "mixed-list",
                    savedQueryName: "Mixed list",
                    headingOverride: nil,
                    viewOverride: " LiSt ",
                    missing: false),
            ],
            createdAtMs: 0,
            modifiedAtMs: 0)

        let draft = DashboardEditorDraft(dashboard: dashboard, savedQueries: [])

        XCTAssertEqual(draft.sections.map(\.viewOverride), ["Table", "Table", "List", "List"])
    }

    func testDashboardEditorDraftMapsBlankOverrideToDefaultButPreservesUnsupportedValues() {
        let dashboard = Dashboard(
            id: "dashboard",
            name: "Renderer overrides",
            sections: [
                DashboardSectionStatus(
                    savedQueryId: "nil",
                    savedQueryName: "Nil",
                    headingOverride: nil,
                    viewOverride: nil,
                    missing: false),
                DashboardSectionStatus(
                    savedQueryId: "blank",
                    savedQueryName: "Blank",
                    headingOverride: nil,
                    viewOverride: "   ",
                    missing: false),
                DashboardSectionStatus(
                    savedQueryId: "default",
                    savedQueryName: "Default",
                    headingOverride: nil,
                    viewOverride: "Default",
                    missing: false),
                DashboardSectionStatus(
                    savedQueryId: "unsupported",
                    savedQueryName: "Unsupported",
                    headingOverride: nil,
                    viewOverride: "Board",
                    missing: false),
            ],
            createdAtMs: 0,
            modifiedAtMs: 0)

        let draft = DashboardEditorDraft(dashboard: dashboard, savedQueries: [])

        XCTAssertEqual(draft.sections.map(\.viewOverride), ["", "", "Default", "Board"])
    }

    func testDashboardSectionReorderKeysAreScopedToTheFocusedSection() throws {
        let source = try Self.sourceFile("Sources/SlateMac/Bases/DashboardEditorSheet.swift")

        XCTAssertTrue(source.contains("@FocusState private var focusedSectionID"))
        XCTAssertTrue(source.contains("handleSectionReorder("))
        XCTAssertTrue(source.contains("BaseRowReorderCommand"))
        XCTAssertTrue(source.contains("BaseRowReorderCommand.route("))
        XCTAssertTrue(source.contains("isFocused: focusedSectionID == sectionID"))
        XCTAssertTrue(source.contains(".focusable()"))
        XCTAssertTrue(
            source.contains(
                ".focused($focusedSectionID, equals: section.wrappedValue.id)"))
        XCTAssertTrue(source.contains(".onKeyPress(.upArrow, phases: .down)"))
        XCTAssertTrue(source.contains(".onKeyPress(.downArrow, phases: .down)"))
        XCTAssertTrue(
            source.contains(
                "sectionID: section.wrappedValue.id,\n                direction: .up,"))
        XCTAssertTrue(
            source.contains(
                "sectionID: section.wrappedValue.id,\n                direction: .down,"))
        XCTAssertTrue(source.contains("retainFocus: { _ in focusedSectionID = sectionID }"))
        XCTAssertTrue(
            source.contains(
                "announce: { postAccessibilityAnnouncement(.hostComposed(text: $0, priority: .medium)) }"
            ))
    }

    func testOptionArrowReordersFocusedDashboardSectionOnceAndRetainsItsIdentity() throws {
        let first = DashboardEditorSectionDraft(
            savedQueryID: "first",
            savedQueryName: "First",
            headingOverride: "",
            viewOverride: "",
            missing: false)
        let second = DashboardEditorSectionDraft(
            savedQueryID: "second",
            savedQueryName: "Second",
            headingOverride: "",
            viewOverride: "",
            missing: false)
        var draft = DashboardEditorDraft(sections: [first, second])
        var moveCount = 0
        var moveDestination: Int?
        var focusedSectionID: DashboardEditorSectionDraft.ID?
        var announcements: [String] = []

        let handled = BaseRowReorderCommand.route(
            isFocused: true,
            direction: .up,
            modifiers: .option,
            index: 1,
            count: draft.sections.count,
            label: "Dashboard section 2",
            move: { destination in
                moveCount += 1
                moveDestination = destination
                draft.moveSection(from: 1, to: destination)
            },
            retainFocus: { _ in focusedSectionID = second.id },
            announce: { announcements.append($0) })

        XCTAssertTrue(handled)
        XCTAssertEqual(draft.sections.map(\.id), [second.id, first.id])
        XCTAssertEqual(moveCount, 1)
        XCTAssertEqual(focusedSectionID, second.id)
        XCTAssertEqual(moveDestination, 0)
        XCTAssertEqual(
            announcements,
            ["Dashboard section 2 moved up to position 1 of 2."])

        var boundaryMoves = 0
        var boundaryFocus: Int?
        var boundaryAnnouncements: [String] = []
        let boundaryHandled = BaseRowReorderCommand.route(
            isFocused: true,
            direction: .up,
            modifiers: .option,
            index: 0,
            count: draft.sections.count,
            label: "Dashboard section 1",
            move: { _ in boundaryMoves += 1 },
            retainFocus: { boundaryFocus = $0 },
            announce: { boundaryAnnouncements.append($0) })

        XCTAssertTrue(boundaryHandled)
        XCTAssertEqual(boundaryMoves, 0)
        XCTAssertEqual(boundaryFocus, 0)
        XCTAssertEqual(
            boundaryAnnouncements,
            ["Dashboard section 1 is already first."])
        var ignoredCallbacks = 0
        let voiceOverHandled = BaseRowReorderCommand.route(
            isFocused: true,
            direction: .up,
            modifiers: [.control, .option],
            index: 1,
            count: draft.sections.count,
            label: "Dashboard section 2",
            move: { _ in ignoredCallbacks += 1 },
            retainFocus: { _ in ignoredCallbacks += 1 },
            announce: { _ in ignoredCallbacks += 1 })
        XCTAssertFalse(
            voiceOverHandled,
            "Control-Option-Up belongs to VoiceOver Quick Nav")
        XCTAssertEqual(ignoredCallbacks, 0)
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

        _ = await state.refreshBaseQueries()?.value
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

        _ = await state.refreshBaseQueries()?.value
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

        _ = await state.refreshBaseQueries()?.value
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

        _ = await state.refreshBaseQueries()?.value
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
                    viewOverride: "List"),
                DashboardSection(
                    savedQueryId: missingTwo,
                    headingOverride: "Remove me",
                    viewOverride: nil),
            ])

        _ = await state.refreshBaseQueries()?.value
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
        XCTAssertEqual(updated.sections[0].viewOverride, "List")
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

        _ = await state.refreshBaseQueries()?.value
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

    func testDashboardViewOverrideUsesTableListRendererAndRejectsLegacyValues() async throws {
        let (state, session) = try await makeState()
        let queryID = try session.saveQuery(
            name: "Active projects",
            description: nil,
            queryJson: queryJSON(folder: "Projects"),
            sourceSyntax: .builder)
        let dashboardID = try session.saveDashboard(
            name: "Renderer overrides",
            sections: [
                DashboardSection(
                    savedQueryId: queryID,
                    headingOverride: "List section",
                    viewOverride: "List"),
                DashboardSection(
                    savedQueryId: queryID,
                    headingOverride: "Legacy section",
                    viewOverride: "Board"),
                DashboardSection(
                    savedQueryId: queryID,
                    headingOverride: "Nonblank default",
                    viewOverride: "Default"),
            ])
        _ = await state.refreshBaseQueries()?.value
        state.openDashboard(id: dashboardID, name: "Renderer overrides")
        let document = try XCTUnwrap(state.activeDashboardDocument)

        XCTAssertEqual(document.sections[0].state, .ready)
        XCTAssertEqual(document.sections[0].rendererOverride, .list)
        XCTAssertEqual(
            document.sections[1].state,
            .failed("Unsupported dashboard view override \"Board\". Choose Default, Table, or List."))
        XCTAssertNil(document.sections[1].result)
        XCTAssertEqual(
            document.sections[2].state,
            .failed(
                "Unsupported dashboard view override \"Default\". Choose Default, Table, or List."))
        XCTAssertNil(document.sections[2].result)

        let source = try Self.sourceFile("Sources/SlateMac/Bases/DashboardEditorSheet.swift")
        XCTAssertTrue(source.contains("Picker(\"Section view\""))
        XCTAssertTrue(source.contains("Text(\"Default\").tag(\"\")"))
        XCTAssertTrue(source.contains("Text(\"Table\").tag(\"Table\")"))
        XCTAssertTrue(source.contains("Text(\"List\").tag(\"List\")"))
        XCTAssertFalse(source.contains("TextField(\"View override\""))
        let dashboardSource = try Self.sourceFile("Sources/SlateMac/Bases/DashboardViews.swift")
        XCTAssertTrue(dashboardSource.contains("rendererOverride: section.resolvedRenderer"))
        XCTAssertTrue(dashboardSource.contains("if rendererOverride == .list"))
    }

    func testDashboardDefaultUsesSavedQueryAuthoredRenderer() async throws {
        let (state, session) = try await makeState()
        var listDraft = BaseQueryBuilderDraft()
        listDraft.source = .folder("Projects")
        listDraft.viewType = .list
        let queryID = try session.saveQuery(
            name: "List projects",
            description: nil,
            queryJson: listDraft.queryJSON(),
            sourceSyntax: .builder)
        let dashboardID = try session.saveDashboard(
            name: "Authored renderer",
            sections: [
                DashboardSection(
                    savedQueryId: queryID,
                    headingOverride: nil,
                    viewOverride: nil)
            ])
        _ = await state.refreshBaseQueries()?.value
        state.openDashboard(id: dashboardID, name: "Authored renderer")
        let section = try XCTUnwrap(state.activeDashboardDocument?.sections.first)

        XCTAssertEqual(section.rendererOverride, nil)
        XCTAssertEqual(section.authoredRenderer, .list)
        XCTAssertEqual(section.resolvedRenderer, .list)
    }

    func testDashboardAndDockSelectionFollowColumnIdentityAfterSavedQueryUpdate() async throws {
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
        _ = await state.refreshBaseQueries()?.value
        state.openDashboard(id: dashboardID, name: "Overview")
        let initial = try XCTUnwrap(state.activeDashboardDocument?.sections[0].result)
        let rowID = BaseGridRow.id(for: try XCTUnwrap(initial.rows.first))
        var dashboardState = BaseGridInteractionState()
        dashboardState.setCellPosition(.init(rowID: rowID, columnIndex: 0), in: initial)
        dashboardState.setSortState(
            DataGridSortState(columnIndex: 0, ascending: false),
            in: initial)
        var dockState = dashboardState

        var reordered = initial
        reordered.columns.insert(
            BasesColumn(id: "status", label: "Status", valueKind: "text", role: .metadata),
            at: 0)
        for index in reordered.rows.indices {
            reordered.rows[index].values.insert(reordered.rows[index].values[0], at: 0)
        }
        dashboardState.reconcile(with: reordered)
        dockState.reconcile(with: reordered)

        for interaction in [dashboardState, dockState] {
            XCTAssertEqual(interaction.cellPosition(in: reordered)?.columnIndex, 1)
            XCTAssertEqual(
                interaction.sortState(in: reordered),
                DataGridSortState(columnIndex: 1, ascending: false))
        }

        reordered.columns = []
        reordered.rows = reordered.rows.map { row in
            var row = row
            row.values = []
            return row
        }
        dashboardState.reconcile(with: reordered)
        dockState.reconcile(with: reordered)
        XCTAssertNil(dashboardState.selectedCell)
        XCTAssertNil(dashboardState.sortSelection)
        XCTAssertNil(dockState.selectedCell)
        XCTAssertNil(dockState.sortSelection)

        let source = try Self.sourceFile("Sources/SlateMac/Bases/DashboardViews.swift")
        XCTAssertTrue(source.contains("@State private var gridInteraction"))
        XCTAssertTrue(source.contains("interaction: $gridInteraction"))
    }

    func testSavedQueryPaletteCommandsRefreshOnRenameAndDelete() async throws {
        let (state, session) = try await makeState()
        let id = try session.saveQuery(
            name: "Active projects",
            description: nil,
            queryJson: queryJSON(folder: "Projects"),
            sourceSyntax: .builder)

        _ = await state.refreshBaseQueries()?.value
        let commandID = SlateCommandID.basesRunSavedQuery(id: id)
        XCTAssertEqual(state.commandRegistry.findById(id: commandID)?.label, "Run query: Active projects")
        XCTAssertEqual(state.commandRegistry.findById(id: commandID)?.section, .bases)
        XCTAssertNil(state.commandRegistry.findById(id: commandID)?.hotkeyHint)

        try state.commandRegistry.invokeById(id: commandID)
        XCTAssertEqual(state.workspace.activeTab?.item, .savedQuery(id: id, name: "Active projects"))
        state.dockSavedQueryToSidebar(id: id, refreshDelayNanoseconds: 0)
        await state.basesDockRefreshTask?.value
        let dockDocument = try XCTUnwrap(state.basesDockDocument)

        try session.renameSavedQuery(id: id, name: "Renamed")
        _ = await state.refreshBaseQueries()?.value
        XCTAssertEqual(state.commandRegistry.findById(id: commandID)?.label, "Run query: Renamed")
        XCTAssertEqual(state.basesDock.target, .savedQuery(id: id, name: "Renamed"))
        XCTAssertTrue(state.basesDockDocument === dockDocument)
        XCTAssertEqual(dockDocument.source, .savedQuery(id: id, name: "Renamed"))

        try session.deleteSavedQuery(id: id)
        _ = await state.refreshBaseQueries()?.value
        XCTAssertNil(state.commandRegistry.findById(id: commandID))
    }

    func testEditingSavedQueryBuilderUpdatesOriginalSavedQuery() async throws {
        let (state, session) = try await makeState()
        let id = try session.saveQuery(
            name: "Active projects",
            description: "Open work",
            queryJson: queryJSON(folder: "Projects"),
            sourceSyntax: .builder)

        _ = await state.refreshBaseQueries()?.value
        state.editSavedQueryInBuilder(id: id)
        let model = try XCTUnwrap(state.activeBaseQueryBuilder)
        XCTAssertEqual(model.editingSavedQuery?.id, id)
        XCTAssertEqual(model.editingSavedQuery?.name, "Active projects")

        model.source = .folder("Backlog")
        await state.basesBuilderUpdateSavedQuery()?.value

        let saved = try session.getSavedQuery(id: id)
        let draft = try BaseQueryBuilderDraft(queryJSON: saved.queryJson)
        XCTAssertEqual(draft.source, .folder("Backlog"))
        XCTAssertEqual(saved.description, "Open work")
        XCTAssertEqual(try session.listSavedQueries().map(\.id), [id])
        XCTAssertEqual(
            state.commandRegistry.findById(id: SlateCommandID.basesRunSavedQuery(id: id))?.label,
            "Run query: Active projects")
    }

    func testUpdatingSavedQueryReopensTabDashboardDirectDockAndNameAndIDEmbeds() async throws {
        let (state, session) = try await makeState()
        let id = try session.saveQuery(
            name: "Active projects",
            description: "Open work",
            queryJson: queryJSON(folder: "Projects"),
            sourceSyntax: .builder)
        let dashboardID = try session.saveDashboard(
            name: "Overview",
            sections: [
                DashboardSection(
                    savedQueryId: id,
                    headingOverride: nil,
                    viewOverride: nil)
            ])
        _ = await state.refreshBaseQueries()?.value

        let nameRequest = try XCTUnwrap(
            BaseEmbedRequest.codeFence(
                language: "slate-query",
                source: "```slate-query\nquery: Active projects\n```"))
        let nameEmbed = BaseEmbedDocument(
            request: nameRequest,
            thisPath: "Projects/Alpha.md",
            sharedHandle: state.baseEmbedHandle(
                for: nameRequest,
                thisPath: "Projects/Alpha.md"))
        nameEmbed.load(session: session)
        let idRequest = try XCTUnwrap(
            BaseEmbedRequest.codeFence(
                language: "slate-query",
                source: "```slate-query\nquery: \(id)\n```"))
        let idEmbed = BaseEmbedDocument(
            request: idRequest,
            thisPath: "Projects/Beta.md",
            sharedHandle: state.baseEmbedHandle(
                for: idRequest,
                thisPath: "Projects/Beta.md"))
        idEmbed.load(session: session)
        XCTAssertEqual(nameEmbed.result?.rows.count, 3)
        XCTAssertEqual(idEmbed.result?.rows.count, 3)

        state.renameSavedQuery(id: id, name: "Renamed projects")
        state.openSavedQuery(id: id, name: "Renamed projects")
        let tab = try XCTUnwrap(state.activeBaseDocument)
        state.openDashboard(id: dashboardID, name: "Overview", target: .newTab)
        let dashboard = try XCTUnwrap(state.activeDashboardDocument)
        state.dockSavedQueryToSidebar(id: id, refreshDelayNanoseconds: 0)
        // #999: `await state.basesDockRefreshTask?.value` on a nil task is a
        // no-op, which silently turns "the dock never refreshed" into a
        // confusing unwrap failure two lines down. Unwrap the task instead.
        await (try XCTUnwrap(state.basesDockRefreshTask)).value
        let dock = try XCTUnwrap(state.basesDockDocument)
        XCTAssertEqual(tab.result?.rows.count, 3)
        XCTAssertEqual(dashboard.sections[0].result?.rows.count, 3)
        XCTAssertEqual(dock.result?.rows.count, 3)
        let tabHandle = try XCTUnwrap(tab.handle)
        let dockHandle = try XCTUnwrap(dock.handle)
        let nameEmbedHandle = try XCTUnwrap(nameEmbed.handle)
        let idEmbedHandle = try XCTUnwrap(idEmbed.handle)
        let nativeEvents = NativeEventRecorder()
        state.baseRetargetNativeExecutionObserverForTesting = { nativeEvents.record($0) }

        state.editSavedQueryInBuilder(id: id)
        let model = try XCTUnwrap(state.activeBaseQueryBuilder)
        model.source = .folder("Backlog")
        let updateTask = try XCTUnwrap(state.basesBuilderUpdateSavedQuery())

        XCTAssertEqual(tab.handle, tabHandle)
        XCTAssertEqual(dock.handle, dockHandle)
        XCTAssertEqual(nameEmbed.handle, nameEmbedHandle)
        XCTAssertEqual(idEmbed.handle, idEmbedHandle)
        XCTAssertEqual(tab.result?.rows.count, 3)
        XCTAssertEqual(dashboard.sections[0].result?.rows.count, 3)
        XCTAssertEqual(dock.result?.rows.count, 3)
        XCTAssertEqual(nameEmbed.result?.rows.count, 3)
        XCTAssertEqual(idEmbed.result?.rows.count, 3)

        await updateTask.value

        XCTAssertTrue(tab.result?.rows.isEmpty == true)
        XCTAssertTrue(dashboard.sections[0].result?.rows.isEmpty == true)
        XCTAssertTrue(dock.result?.rows.isEmpty == true)
        XCTAssertEqual(nameEmbed.state, .ready)
        XCTAssertTrue(nameEmbed.result?.rows.isEmpty == true)
        XCTAssertEqual(idEmbed.state, .ready)
        XCTAssertTrue(idEmbed.result?.rows.isEmpty == true)
        XCTAssertFalse(nativeEvents.events.isEmpty)
        XCTAssertFalse(nativeEvents.events.contains(where: \.ranOnMainThread))
    }

    /// #999: `renameSavedQuery` refreshes the query list fire-and-forget, and
    /// that refresh rewrites the dock's target with the new display name. A
    /// dock refresh scheduled before it landed must still run — a rename is
    /// not a retarget — or the dock pane stays empty forever, silently, with
    /// nothing left to reschedule it.
    func testDockingSavedQuerySurvivesRenameRefreshLandingFirst() async throws {
        let (state, session) = try await makeState()
        let id = try session.saveQuery(
            name: "Active projects",
            description: nil,
            queryJson: queryJSON(folder: "Projects"),
            sourceSyntax: .builder)
        _ = await state.refreshBaseQueries()?.value

        state.renameSavedQuery(id: id, name: "Renamed projects")
        // The delay pins the interleaving the flake hit by chance: the dock
        // refresh is still sleeping when the rename refresh awaited below
        // retargets the dock's name.
        state.dockSavedQueryToSidebar(id: id, refreshDelayNanoseconds: 5_000_000)
        await state.baseQueriesRefreshTask?.value
        XCTAssertEqual(state.basesDock.target, .savedQuery(id: id, name: "Renamed projects"))

        await (try XCTUnwrap(state.basesDockRefreshTask)).value
        let dock = try XCTUnwrap(state.basesDockDocument)
        XCTAssertEqual(dock.result?.rows.count, 3)
    }

    /// Holds every prepared native load off in a barrier so a test can pin the
    /// interleaving exactly: the rename/re-dock below runs on the main actor
    /// while the replacement result is provably still in flight.
    private final class PreparedLoadBarrier: @unchecked Sendable {
        private let condition = NSCondition()
        private var released = false

        func run<T>(_ work: () -> T) -> T {
            condition.lock()
            while !released { condition.wait() }
            condition.unlock()
            return work()
        }

        func release() {
            condition.lock()
            released = true
            condition.broadcast()
            condition.unlock()
        }
    }

    /// #999 sibling: the post-write apply guard is an OWNERSHIP test — "is the
    /// dock still showing this entity, and is this still its document?" A
    /// display-name-only re-dock is neither a retarget nor a reason to throw
    /// away a prepared result, and `BasesDockState.setTarget` agrees: it
    /// invalidates the membership baseline on `stableIdentity`, not on the
    /// name. A name-sensitive guard here released the result (stale rows) and
    /// skipped the rebase, so the next follow-active publish announced a change
    /// the user had already been told about.
    func testDockedBaseAppliesInAppWriteResultWhenDisplayNameChangesMidPreparation()
        async throws
    {
        let (state, session) = try await makeState()
        let basePath = "Queries/Projects.base"
        try #"""
            views:
              - type: table
                name: All
                filters: 'file.inFolder("Projects")'
                order: [file.name]
            """#.write(
            to: tempDir.appendingPathComponent("vault/\(basePath)"),
            atomically: true,
            encoding: .utf8)
        try session.scanInitial(cancel: CancelToken())
        _ = await state.refreshBaseQueries()?.value

        state.dockBaseFileToSidebar(path: basePath, name: "Projects", refreshDelayNanoseconds: 0)
        await state.basesDockRefreshTask?.value
        let dock = try XCTUnwrap(state.basesDockDocument)
        XCTAssertEqual(dock.result?.rows.count, 3, "the dock must start from the settled rows")
        XCTAssertEqual(state.basesDock.lastMembershipSignature, BaseRowMembership(rows: dock.result?.rows ?? []))

        _ = try session.createExclusive(path: "Projects/Delta.md", content: "# Delta\n")
        let barrier = PreparedLoadBarrier()
        state.baseRetargetPreloadRunner = { session, request, observer in
            barrier.run {
                BasePreparedLoader.prepare(
                    session: session, request: request, observer: observer)
            }
        }
        let refresh = try XCTUnwrap(
            state.refreshVisibleBasesAfterInAppWrite(
                session: session,
                changedPath: "Projects/Delta.md"))

        // Pinned interleaving: the replacement is parked in the barrier, so the
        // re-dock below provably lands before the apply loop reads the target.
        state.dockBaseFileToSidebar(
            path: basePath,
            name: "Projects renamed",
            refreshDelayNanoseconds: 60_000_000_000)
        XCTAssertEqual(
            state.basesDock.target,
            .base(path: basePath, name: "Projects renamed"))
        XCTAssertTrue(state.basesDockDocument === dock, "the re-dock must keep the document")
        barrier.release()
        _ = await refresh.value
        state.basesDockRefreshTask?.cancel()

        XCTAssertEqual(
            dock.result?.rows.count, 4,
            "a display-name-only re-dock must not release the prepared result: the docked "
                + "Base kept stale rows")
        XCTAssertEqual(
            state.basesDock.lastMembershipSignature, BaseRowMembership(rows: dock.result?.rows ?? []),
            "the dock baseline must be rebased onto the applied rows, or the next "
                + "follow-active publish re-announces a change the user already heard")

        // The out-of-band change announced once, through the refresh itself.
        XCTAssertEqual(state.lastBaseRefreshAnnouncements.count, 1)
        state.lastBaseActionAnnouncement = nil
        state.scheduleBasesDockFollowActiveRefresh(delayNanoseconds: 0)
        await state.basesDockRefreshTask?.value
        XCTAssertEqual(dock.result?.rows.count, 4)
        XCTAssertNotEqual(
            state.lastBaseActionAnnouncement, "Base dock updated for active note.",
            "a follow-active refresh over unchanged rows must stay silent")
    }

    /// #999 sibling, dashboard half: a dashboard rename retargets the dock's
    /// target name without touching the section reservations, so a
    /// name-sensitive apply guard drops a perfectly good prepared section
    /// result and leaves the docked dashboard on stale rows.
    func testDockedDashboardAppliesInAppWriteResultWhenRenameLandsMidPreparation()
        async throws
    {
        let (state, session) = try await makeState()
        let queryID = try session.saveQuery(
            name: "Active projects",
            description: nil,
            queryJson: queryJSON(folder: "Projects"),
            sourceSyntax: .builder)
        let sections = [
            DashboardSection(savedQueryId: queryID, headingOverride: nil, viewOverride: nil)
        ]
        let dashboardID = try session.saveDashboard(name: "Overview", sections: sections)
        _ = await state.refreshBaseQueries()?.value

        state.dockDashboardToSidebar(id: dashboardID, refreshDelayNanoseconds: 0)
        await state.basesDockRefreshTask?.value
        let docked = try XCTUnwrap(state.basesDockDashboardDocument)
        XCTAssertEqual(docked.sections[0].result?.rows.count, 3)
        XCTAssertEqual(state.basesDock.lastMembershipSignature, docked.membershipSignature)

        _ = try session.createExclusive(path: "Projects/Delta.md", content: "# Delta\n")
        let barrier = PreparedLoadBarrier()
        state.baseRetargetPreloadRunner = { session, request, observer in
            barrier.run {
                BasePreparedLoader.prepare(
                    session: session, request: request, observer: observer)
            }
        }
        let refresh = try XCTUnwrap(
            state.refreshVisibleBasesAfterInAppWrite(
                session: session,
                changedPath: "Projects/Delta.md"))

        // Pinned interleaving: the rename lands (out of band, as another
        // surface would do it) while the section result sits in the barrier.
        try session.updateDashboard(
            id: dashboardID, name: "Renamed overview", sections: sections)
        _ = await state.refreshBaseQueries()?.value
        XCTAssertEqual(
            state.basesDock.target,
            .dashboard(id: dashboardID, name: "Renamed overview"))
        XCTAssertTrue(state.basesDockDashboardDocument === docked)
        barrier.release()
        _ = await refresh.value

        XCTAssertEqual(
            docked.sections[0].result?.rows.count, 4,
            "a rename must not release the docked dashboard's prepared section result: "
                + "the dock kept stale rows")
        XCTAssertEqual(
            state.basesDock.lastMembershipSignature, docked.membershipSignature,
            "the dock baseline must be rebased onto the applied dashboard rows")

        state.lastBaseActionAnnouncement = nil
        state.scheduleBasesDockFollowActiveRefresh(delayNanoseconds: 0)
        await state.basesDockRefreshTask?.value
        XCTAssertEqual(docked.sections[0].result?.rows.count, 4)
        XCTAssertNotEqual(
            state.lastBaseActionAnnouncement, "Base dock updated for active note.",
            "a follow-active refresh over unchanged rows must stay silent")
    }

    func testUpdatingSavedQueryReopensDockedDashboardSection() async throws {
        let (state, session) = try await makeState()
        let id = try session.saveQuery(
            name: "Active projects",
            description: nil,
            queryJson: queryJSON(folder: "Projects"),
            sourceSyntax: .builder)
        let dashboardID = try session.saveDashboard(
            name: "Overview",
            sections: [
                DashboardSection(
                    savedQueryId: id,
                    headingOverride: nil,
                    viewOverride: nil)
            ])
        _ = await state.refreshBaseQueries()?.value
        state.dockDashboardToSidebar(id: dashboardID, refreshDelayNanoseconds: 0)
        await state.basesDockRefreshTask?.value
        let dockedDashboard = try XCTUnwrap(state.basesDockDashboardDocument)
        XCTAssertEqual(dockedDashboard.sections[0].result?.rows.count, 3)

        state.editSavedQueryInBuilder(id: id)
        let model = try XCTUnwrap(state.activeBaseQueryBuilder)
        model.source = .folder("Backlog")
        let updateTask = try XCTUnwrap(state.basesBuilderUpdateSavedQuery())

        XCTAssertEqual(dockedDashboard.sections[0].result?.rows.count, 3)
        await updateTask.value

        XCTAssertTrue(dockedDashboard.sections[0].result?.rows.isEmpty == true)
    }

    func testDeletingSavedQueryInvalidatesVisibleNameAndIDEmbedsButPreservesUnrelatedEmbed()
        async throws
    {
        let (state, session) = try await makeState()
        let deletedID = try session.saveQuery(
            name: "Active projects",
            description: nil,
            queryJson: queryJSON(folder: "Projects"),
            sourceSyntax: .builder)
        _ = try session.saveQuery(
            name: "Backlog",
            description: nil,
            queryJson: queryJSON(folder: "Backlog"),
            sourceSyntax: .builder)

        func makeEmbed(reference: String, thisPath: String) throws -> BaseEmbedDocument {
            let request = try XCTUnwrap(
                BaseEmbedRequest.codeFence(
                    language: "slate-query",
                    source: "```slate-query\nquery: \(reference)\n```"))
            return BaseEmbedDocument(
                request: request,
                thisPath: thisPath,
                sharedHandle: state.baseEmbedHandle(for: request, thisPath: thisPath))
        }

        let nameEmbed = try makeEmbed(
            reference: "Active projects",
            thisPath: "Projects/Alpha.md")
        let idEmbed = try makeEmbed(
            reference: deletedID,
            thisPath: "Projects/Beta.md")
        let unrelatedEmbed = try makeEmbed(
            reference: "Backlog",
            thisPath: "Projects/Gamma.md")
        [nameEmbed, idEmbed, unrelatedEmbed].forEach { $0.load(session: session) }

        XCTAssertEqual(nameEmbed.state, .ready)
        XCTAssertEqual(idEmbed.state, .ready)
        XCTAssertEqual(unrelatedEmbed.state, .ready)
        let nameHandle = try XCTUnwrap(nameEmbed.handle)
        let idHandle = try XCTUnwrap(idEmbed.handle)
        let unrelatedHandle = try XCTUnwrap(unrelatedEmbed.handle)

        state.deleteSavedQuery(id: deletedID)

        for document in [nameEmbed, idEmbed] {
            guard case .failed(let message) = document.state else {
                XCTFail("deleted saved-query embed remained visible: \(document.state)")
                continue
            }
            XCTAssertTrue(
                message.localizedCaseInsensitiveContains("saved query"),
                "the existing unknown-query surface must explain the missing saved query: \(message)")
            XCTAssertNil(document.result)
            XCTAssertNil(document.handle)
        }
        XCTAssertThrowsError(try session.baseViews(handle: nameHandle))
        XCTAssertThrowsError(try session.baseViews(handle: idHandle))

        XCTAssertEqual(unrelatedEmbed.state, .ready)
        XCTAssertEqual(unrelatedEmbed.handle, unrelatedHandle)
        XCTAssertNoThrow(try session.baseViews(handle: unrelatedHandle))
    }

    func testDeletingSavedQueryClosesOpenTabsAndDocument() async throws {
        let (state, session) = try await makeState()
        let id = try session.saveQuery(
            name: "Active projects",
            description: nil,
            queryJson: queryJSON(folder: "Projects"),
            sourceSyntax: .builder)

        _ = await state.refreshBaseQueries()?.value
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

        _ = await state.refreshBaseQueries()?.value
        state.openSavedQuery(id: id, name: "Context query")
        let doc = try XCTUnwrap(state.activeBaseDocument)
        guard case .degraded(let message) = doc.state else {
            return XCTFail("expected degraded state, got \(doc.state)")
        }
        XCTAssertTrue(message.contains("this is unavailable in this evaluation context"), message)
        XCTAssertTrue(message.contains("Dock to sidebar to follow the active note."), message)
    }

    func testContextlessDashboardSectionSurfacesEngineViewError() async throws {
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
            name: "Needs context",
            description: nil,
            queryJson: draft.queryJSON(),
            sourceSyntax: .builder)
        let dashboardID = try session.saveDashboard(
            name: "Context dashboard",
            sections: [
                DashboardSection(
                    savedQueryId: queryID,
                    headingOverride: nil,
                    viewOverride: nil)
            ])

        state.openDashboard(id: dashboardID, name: "Context dashboard")

        let section = try XCTUnwrap(state.activeDashboardDocument?.sections.first)
        guard case .failed(let message) = section.state else {
            return XCTFail("contextless dashboard section must fail loud: \(section.state)")
        }
        XCTAssertTrue(message.contains("this is unavailable in this evaluation context"), message)

        let source = try Self.sourceFile("Sources/SlateMac/Bases/DashboardViews.swift")
        XCTAssertTrue(source.contains("Dashboard section error"), source)
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
