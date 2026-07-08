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
}
