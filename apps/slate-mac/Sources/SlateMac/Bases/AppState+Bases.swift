// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import Foundation

struct BaseQueriesState: Equatable {
    var savedQueries: [SavedQuerySummary] = []
    var baseFiles: [BaseFileSummary] = []
    var dashboards: [DashboardSummary] = []
    var pinnedSavedQueryIDs: [String] = []
}

/// Bases tab lifecycle (Milestone N, #702): the `.base` arm of the
/// single navigation funnel, the per-path document registry, and the
/// palette command actions.
extension AppState {
    func baseDocument(for path: String) -> BaseDocument {
        baseDocument(for: .file(path: path))
    }

    func baseDocument(for source: BaseDocumentSource) -> BaseDocument {
        if let existing = baseDocuments[source.key] { return existing }
        let doc = BaseDocument(source: source)
        baseDocuments[source.key] = doc
        return doc
    }

    func baseEmbedHandle(for request: BaseEmbedRequest, thisPath: String?) -> BaseEmbedHandle {
        let key = BaseEmbedCacheKey(request: request, thisPath: thisPath)
        if let existing = baseEmbedHandles[key] { return existing }
        let handle = BaseEmbedHandle(request: request, thisPath: thisPath)
        baseEmbedHandles[key] = handle
        return handle
    }

    func openBaseFile(_ path: String, target: OpenTarget = .currentTab) {
        switch target {
        case .currentTab:
            if let existing = workspace.activeGroupTab(forPath: path) {
                activateTab(existing.id)
                return
            }
            clearActiveBaseQuickFilter()
            parkOutgoingNoteBuffer()
            if workspace.activeTab != nil {
                let replacedItem = workspace.activeTab?.item
                workspace.replaceActiveItem(.base(path: path))
                releaseCanvasDocumentIfUnreferenced(replacedItem)
                releaseBaseDocumentIfUnreferenced(replacedItem)
                releaseDashboardDocumentIfUnreferenced(replacedItem)
                if let id = workspace.model.activeGroup.activeTabID {
                    clearBaseRendererOverride(for: id)
                    activateTab(id)
                }
            } else {
                let id = workspace.openTab(.base(path: path))
                activateTab(id)
            }
        case .newTab:
            if let existing = workspace.activeGroupTab(forPath: path) {
                activateTab(existing.id)
                return
            }
            clearActiveBaseQuickFilter()
            parkOutgoingNoteBuffer()
            let id = workspace.openTab(.base(path: path))
            activateTab(id)
        case .newSplit(let axis):
            clearActiveBaseQuickFilter()
            let paneCount = workspace.model.groupsInOrder.count
            splitActivePane(axis: axis)
            if workspace.model.groupsInOrder.count == paneCount {
                openBaseFile(path, target: .newTab)
                return
            }
            openBaseFile(path, target: .currentTab)
        }
    }

    func openSavedQuery(_ summary: SavedQuerySummary, target: OpenTarget = .currentTab) {
        openSavedQuery(id: summary.id, name: summary.name, target: target)
    }

    func openSavedQuery(id: String, name: String, target: OpenTarget = .currentTab) {
        let item = EditorItem.savedQuery(id: id, name: name)
        switch target {
        case .currentTab:
            if let existing = workspace.activeGroupSavedQueryTab(id: id) {
                activateTab(existing.id)
                return
            }
            clearActiveBaseQuickFilter()
            parkOutgoingNoteBuffer()
            if workspace.activeTab != nil {
                let replacedItem = workspace.activeTab?.item
                workspace.replaceActiveItem(item)
                releaseCanvasDocumentIfUnreferenced(replacedItem)
                releaseBaseDocumentIfUnreferenced(replacedItem)
                releaseDashboardDocumentIfUnreferenced(replacedItem)
                if let tabID = workspace.model.activeGroup.activeTabID {
                    clearBaseRendererOverride(for: tabID)
                    activateTab(tabID)
                }
            } else {
                let tabID = workspace.openTab(item)
                activateTab(tabID)
            }
        case .newTab:
            if let existing = workspace.activeGroupSavedQueryTab(id: id) {
                activateTab(existing.id)
                return
            }
            clearActiveBaseQuickFilter()
            parkOutgoingNoteBuffer()
            let tabID = workspace.openTab(item)
            activateTab(tabID)
        case .newSplit(let axis):
            clearActiveBaseQuickFilter()
            let paneCount = workspace.model.groupsInOrder.count
            splitActivePane(axis: axis)
            if workspace.model.groupsInOrder.count == paneCount {
                openSavedQuery(id: id, name: name, target: .newTab)
                return
            }
            openSavedQuery(id: id, name: name, target: .currentTab)
        }
    }

    func openDashboard(id: String, name: String, target: OpenTarget = .currentTab) {
        let item = EditorItem.dashboard(id: id, name: name)
        switch target {
        case .currentTab:
            if let existing = workspace.activeGroupDashboardTab(id: id) {
                activateTab(existing.id)
                return
            }
            clearActiveBaseQuickFilter()
            parkOutgoingNoteBuffer()
            if workspace.activeTab != nil {
                let replacedItem = workspace.activeTab?.item
                workspace.replaceActiveItem(item)
                releaseCanvasDocumentIfUnreferenced(replacedItem)
                releaseBaseDocumentIfUnreferenced(replacedItem)
                releaseDashboardDocumentIfUnreferenced(replacedItem)
                if let tabID = workspace.model.activeGroup.activeTabID {
                    activateTab(tabID)
                }
            } else {
                let tabID = workspace.openTab(item)
                activateTab(tabID)
            }
        case .newTab:
            if let existing = workspace.activeGroupDashboardTab(id: id) {
                activateTab(existing.id)
                return
            }
            clearActiveBaseQuickFilter()
            parkOutgoingNoteBuffer()
            let tabID = workspace.openTab(item)
            activateTab(tabID)
        case .newSplit(let axis):
            clearActiveBaseQuickFilter()
            let paneCount = workspace.model.groupsInOrder.count
            splitActivePane(axis: axis)
            if workspace.model.groupsInOrder.count == paneCount {
                openDashboard(id: id, name: name, target: .newTab)
                return
            }
            openDashboard(id: id, name: name, target: .currentTab)
        }
    }

    var orderedSavedQuerySummaries: [SavedQuerySummary] {
        let pinOrder = Dictionary(
            uniqueKeysWithValues: baseQueries.pinnedSavedQueryIDs.enumerated().map { ($0.element, $0.offset) })
        return baseQueries.savedQueries.sorted { lhs, rhs in
            switch (pinOrder[lhs.id], pinOrder[rhs.id]) {
            case let (left?, right?):
                return left < right
            case (_?, nil):
                return true
            case (nil, _?):
                return false
            case (nil, nil):
                let byName = lhs.name.localizedCaseInsensitiveCompare(rhs.name)
                return byName == .orderedSame ? lhs.id < rhs.id : byName == .orderedAscending
            }
        }
    }

    var baseQueriesAccessibilityValue: String {
        let count =
            baseQueries.savedQueries.count + baseQueries.baseFiles.count
            + baseQueries.dashboards.count
        let pinned = baseQueries.pinnedSavedQueryIDs.count
        return "Queries, \(count) items, \(pinned) pinned"
    }

    func refreshBaseQueries() {
        guard let session = currentSession else {
            resetBaseQueriesForClosedVault()
            return
        }
        do {
            let saved = try session.listSavedQueries().sorted(by: savedQuerySort)
            let baseFiles = try session.basesList().sorted {
                $0.path.localizedCaseInsensitiveCompare($1.path) == .orderedAscending
            }
            let dashboards = try session.listDashboards().sorted(by: dashboardSort)
            let validIDs = Set(saved.map(\.id))
            let pins = baseQueries.pinnedSavedQueryIDs.filter { validIDs.contains($0) }
            baseQueries = BaseQueriesState(
                savedQueries: saved,
                baseFiles: baseFiles,
                dashboards: dashboards,
                pinnedSavedQueryIDs: pins)
            persistBaseQueryPinsIfNeeded(pins)
            retargetOpenSavedQueries(saved)
            retargetOpenDashboards(dashboards)
            refreshSavedQueryCommands(saved)
        } catch {
            postBaseActionAnnouncement("Queries could not be refreshed: \(error.localizedDescription)")
        }
    }

    func resetBaseQueriesForClosedVault() {
        refreshSavedQueryCommands([])
        baseQueries = BaseQueriesState(
            pinnedSavedQueryIDs: preferencesStore.loadBaseQueryPrefs().pinnedSavedQueryIDs)
        clearBasesDock()
    }

    func toggleSavedQueryPin(id: String) {
        var pins = baseQueries.pinnedSavedQueryIDs
        if let index = pins.firstIndex(of: id) {
            pins.remove(at: index)
        } else if baseQueries.savedQueries.contains(where: { $0.id == id }) {
            pins.insert(id, at: 0)
        } else {
            return
        }
        baseQueries.pinnedSavedQueryIDs = pins
        persistBaseQueryPinsIfNeeded(pins)
    }

    func runSavedQuery(id: String) {
        guard let summary = savedQuerySummary(id: id) else {
            postBaseActionAnnouncement("Saved query is no longer available.")
            return
        }
        openSavedQuery(summary)
    }

    func editSavedQueryInBuilder(id: String) {
        guard let session = currentSession else { return }
        do {
            let saved = try session.getSavedQuery(id: id)
            let draft = try BaseQueryBuilderDraft(queryJSON: saved.queryJson)
            activeBaseQueryBuilder = BaseQueryBuilderModel(
                draft: draft,
                editingSavedQuery: EditingSavedQuery(
                    id: saved.id,
                    name: saved.name,
                    description: saved.description))
            postBaseActionAnnouncement("Editing \(saved.name) in builder.")
        } catch {
            postBaseActionAnnouncement("Saved query could not be edited: \(error.localizedDescription)")
        }
    }

    func renameSavedQuery(id: String, name: String) {
        guard let session = currentSession else { return }
        let trimmed = name.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else {
            postBaseActionAnnouncement("Enter a saved query name before renaming.")
            return
        }
        do {
            try session.renameSavedQuery(id: id, name: trimmed)
            refreshBaseQueries()
            reloadDashboardDocumentsAfterSavedQueryChange()
            postBaseActionAnnouncement("Renamed saved query to \(trimmed).")
        } catch {
            postBaseActionAnnouncement("Saved query could not be renamed: \(error.localizedDescription)")
        }
    }

    func deleteSavedQuery(id: String) {
        guard let session = currentSession else { return }
        do {
            try session.deleteSavedQuery(id: id)
            baseQueries.pinnedSavedQueryIDs.removeAll { $0 == id }
            persistBaseQueryPinsIfNeeded(baseQueries.pinnedSavedQueryIDs)
            closeOpenSavedQueryTabs(id: id)
            if case .savedQuery(let dockedID, _) = basesDock.target, dockedID == id {
                clearBasesDock()
            }
            refreshBaseQueries()
            reloadDashboardDocumentsAfterSavedQueryChange()
            postBaseActionAnnouncement("Deleted saved query.")
        } catch {
            postBaseActionAnnouncement("Saved query could not be deleted: \(error.localizedDescription)")
        }
    }

    func exportSavedQuery(id: String, path: String) {
        guard let session = currentSession else { return }
        let trimmed = path.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else {
            postBaseActionAnnouncement("Choose a .base path before exporting.")
            return
        }
        do {
            try session.exportSavedQueryAsBase(id: id, path: trimmed)
            refreshBaseQueries()
            postBaseActionAnnouncement("Exported saved query as \(trimmed).")
        } catch {
            postBaseActionAnnouncement("Saved query could not be exported: \(error.localizedDescription)")
        }
    }

    func exportSavedQueryUsingSavePanel(id: String) {
        guard let summary = savedQuerySummary(id: id) else {
            postBaseActionAnnouncement("Saved query is no longer available.")
            return
        }
        let panel = NSSavePanel()
        panel.directoryURL = currentVaultURL
        panel.canCreateDirectories = true
        panel.nameFieldStringValue = "\(summary.name).base"
        panel.begin { [weak self] response in
            guard response == .OK, let url = panel.url else { return }
            Task { @MainActor [weak self] in
                guard let self else { return }
                guard let path = self.vaultRelativeExportPath(for: url) else {
                    self.postBaseActionAnnouncement("Choose a path inside the vault.")
                    return
                }
                self.exportSavedQuery(id: id, path: path)
            }
        }
    }

    @discardableResult
    func saveDashboard(name: String, sections: [DashboardSection]) -> String? {
        guard let session = currentSession else { return nil }
        let trimmed = name.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else {
            postBaseActionAnnouncement("Enter a dashboard name before saving.")
            return nil
        }
        do {
            let id = try session.saveDashboard(name: trimmed, sections: sections)
            refreshBaseQueries()
            postBaseActionAnnouncement("Saved dashboard \(trimmed).")
            return id
        } catch {
            postBaseActionAnnouncement("Dashboard could not be saved: \(error.localizedDescription)")
            return nil
        }
    }

    func updateDashboard(id: String, name: String, sections: [DashboardSection]) {
        guard let session = currentSession else { return }
        let trimmed = name.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else {
            postBaseActionAnnouncement("Enter a dashboard name before saving.")
            return
        }
        do {
            try session.renameDashboard(id: id, name: trimmed)
            try session.updateDashboardSections(id: id, sections: sections)
            dashboardDocuments[id]?.load(session: session, thisPath: nil)
            if case .dashboard(let dockedID, _) = basesDock.target, dockedID == id {
                basesDockDashboardDocument?.load(session: session, thisPath: basesDockActiveNotePath)
            }
            refreshBaseQueries()
            postBaseActionAnnouncement("Updated dashboard \(trimmed).")
        } catch {
            postBaseActionAnnouncement("Dashboard could not be updated: \(error.localizedDescription)")
        }
    }

    func deleteDashboard(id: String) {
        guard let session = currentSession else { return }
        do {
            try session.deleteDashboard(id: id)
            closeOpenDashboardTabs(id: id)
            refreshBaseQueries()
            postBaseActionAnnouncement("Deleted dashboard.")
        } catch {
            postBaseActionAnnouncement("Dashboard could not be deleted: \(error.localizedDescription)")
        }
    }

    func dashboardForEditing(id: String) -> Dashboard? {
        guard let session = currentSession else { return nil }
        do {
            return try session.getDashboard(id: id)
        } catch {
            postBaseActionAnnouncement("Dashboard could not be edited: \(error.localizedDescription)")
            return nil
        }
    }

    private func savedQuerySummary(id: String) -> SavedQuerySummary? {
        if let summary = baseQueries.savedQueries.first(where: { $0.id == id }) {
            return summary
        }
        refreshBaseQueries()
        return baseQueries.savedQueries.first { $0.id == id }
    }

    private func dashboardSummary(id: String) -> DashboardSummary? {
        if let summary = baseQueries.dashboards.first(where: { $0.id == id }) {
            return summary
        }
        refreshBaseQueries()
        return baseQueries.dashboards.first { $0.id == id }
    }

    private func savedQuerySort(_ lhs: SavedQuerySummary, _ rhs: SavedQuerySummary) -> Bool {
        let byName = lhs.name.localizedCaseInsensitiveCompare(rhs.name)
        return byName == .orderedSame ? lhs.id < rhs.id : byName == .orderedAscending
    }

    private func dashboardSort(_ lhs: DashboardSummary, _ rhs: DashboardSummary) -> Bool {
        let byName = lhs.name.localizedCaseInsensitiveCompare(rhs.name)
        return byName == .orderedSame ? lhs.id < rhs.id : byName == .orderedAscending
    }

    private func vaultRelativeExportPath(for url: URL) -> String? {
        guard let root = currentVaultURL else { return nil }
        let rootPath = root.standardizedFileURL.path
        let selectedPath = url.standardizedFileURL.path
        guard selectedPath == rootPath || selectedPath.hasPrefix(rootPath + "/") else {
            return nil
        }
        let start = selectedPath.index(selectedPath.startIndex, offsetBy: rootPath.count)
        let relative = selectedPath[start...].trimmingCharacters(in: CharacterSet(charactersIn: "/"))
        return relative.isEmpty ? nil : relative
    }

    private func persistBaseQueryPinsIfNeeded(_ pins: [String]) {
        let current = preferencesStore.loadBaseQueryPrefs()
        guard current.pinnedSavedQueryIDs != pins else { return }
        preferencesStore.saveBaseQueryPrefs(BaseQueryPrefs(pinnedSavedQueryIDs: pins))
    }

    private func retargetOpenSavedQueries(_ saved: [SavedQuerySummary]) {
        for summary in saved {
            _ = workspace.retargetSavedQuery(id: summary.id, name: summary.name)
            baseDocuments[BaseDocumentSource.savedQuery(id: summary.id, name: summary.name).key]?
                .retargetSavedQueryName(summary.name)
        }
    }

    private func retargetOpenDashboards(_ dashboards: [DashboardSummary]) {
        for summary in dashboards {
            _ = workspace.retargetDashboard(id: summary.id, name: summary.name)
            dashboardDocuments[summary.id]?.retargetName(summary.name)
            if case .dashboard(let id, _) = basesDock.target, id == summary.id {
                basesDock.target = .dashboard(id: summary.id, name: summary.name)
            }
        }
    }

    private func closeOpenSavedQueryTabs(id: String) {
        let matchingTabs = workspace.model.allTabs.filter { tab in
            if case .savedQuery(let queryID, _) = tab.item {
                return queryID == id
            }
            return false
        }
        for tab in matchingTabs {
            performCloseTab(tab.id)
        }
        let key = BaseDocumentSource.savedQuery(id: id, name: "").key
        if let doc = baseDocuments[key] {
            if let session = currentSession {
                doc.close(session: session)
            }
            baseDocuments[key] = nil
        }
        saveWorkspaceLayout()
    }

    private func closeOpenDashboardTabs(id: String) {
        let matchingTabs = workspace.model.allTabs.filter { tab in
            if case .dashboard(let dashboardID, _) = tab.item {
                return dashboardID == id
            }
            return false
        }
        for tab in matchingTabs {
            performCloseTab(tab.id)
        }
        if let doc = dashboardDocuments[id] {
            if let session = currentSession {
                doc.close(session: session)
            }
            dashboardDocuments[id] = nil
        }
        if case .dashboard(let dashboardID, _) = basesDock.target, dashboardID == id {
            clearBasesDock()
        }
        saveWorkspaceLayout()
    }

    private func refreshSavedQueryCommands(_ saved: [SavedQuerySummary]) {
        let wanted = Set(saved.map { SlateCommandID.basesRunSavedQuery(id: $0.id) })
        for id in savedQueryCommandIDs.subtracting(wanted) {
            _ = commandRegistry.unregister(id: id)
        }
        for summary in saved {
            let id = SlateCommandID.basesRunSavedQuery(id: summary.id)
            _ = commandRegistry.register(
                command: Command(
                    id: id,
                    label: "Run query: \(summary.name)",
                    accessibilityHint: "Open the saved query.",
                    hotkeyHint: nil,
                    section: .bases),
                action: MenuCommandAction { [weak self] in
                    self?.runSavedQuery(id: summary.id)
                })
        }
        savedQueryCommandIDs = wanted
    }

    func activateBaseTab(_ id: TabID, path: String) {
        activateBaseDocumentTab(id, source: .file(path: path), selectedPath: path)
    }

    func activateSavedQueryTab(_ id: TabID, savedQueryID: String, name: String) {
        activateBaseDocumentTab(id, source: .savedQuery(id: savedQueryID, name: name), selectedPath: nil)
    }

    func dashboardDocument(id: String, name: String) -> DashboardDocument {
        if let existing = dashboardDocuments[id] { return existing }
        let doc = DashboardDocument(id: id, name: name)
        dashboardDocuments[id] = doc
        return doc
    }

    func activateDashboardTab(_ id: TabID, dashboardID: String, name: String) {
        if id == workspace.model.activeGroup.activeTabID,
            selectedFilePath == nil,
            dashboardDocuments[dashboardID]?.dashboard != nil
        {
            return
        }
        workspace.markEditorRegionActive()
        if let pending = pendingTabCloseAfterSave, pending != id {
            pendingTabCloseAfterSave = nil
        }
        isActivatingTab = true
        defer { isActivatingTab = false }
        parkOutgoingNoteBuffer()
        cancelNoteScopedWork()
        clearActiveNoteFields()
        workspace.select(id)
        clearTransitionSensitiveCollections()
        let doc = dashboardDocument(id: dashboardID, name: name)
        if doc.dashboard == nil, let session = currentSession {
            doc.load(session: session)
        }
        if selectedFilePath != nil {
            selectedFilePath = nil
        }
        clearActiveBaseSelection()
    }

    private func activateBaseDocumentTab(
        _ id: TabID,
        source: BaseDocumentSource,
        selectedPath: String?
    ) {
        if id == workspace.model.activeGroup.activeTabID,
            selectedFilePath == selectedPath,
            baseDocuments[source.key]?.handle != nil
        {
            return
        }
        workspace.markEditorRegionActive()
        if let pending = pendingTabCloseAfterSave, pending != id {
            pendingTabCloseAfterSave = nil
        }
        isActivatingTab = true
        defer { isActivatingTab = false }
        parkOutgoingNoteBuffer()
        cancelNoteScopedWork()
        clearActiveNoteFields()
        workspace.select(id)
        clearTransitionSensitiveCollections()
        let doc = baseDocument(for: source)
        if doc.handle == nil, let session = currentSession {
            doc.load(session: session)
        }
        if selectedFilePath != selectedPath {
            selectedFilePath = selectedPath
        }
        if activeBaseSelectionPath != source.selectionKey {
            clearActiveBaseSelection()
        }
    }

    var activeBaseDocument: BaseDocument? {
        guard let tab = workspace.activeTab, let source = BaseDocumentSource(item: tab.item)
        else { return nil }
        return baseDocument(for: source)
    }

    var activeDashboardDocument: DashboardDocument? {
        guard let tab = workspace.activeTab,
            case .dashboard(let id, let name) = tab.item
        else { return nil }
        return dashboardDocument(id: id, name: name)
    }

    func baseRendererOverride(for tabID: TabID) -> BaseRendererMode? {
        baseRendererOverrides[tabID]
    }

    func basesViewAsTable() {
        setActiveBaseRendererOverride(.table)
    }

    func basesViewAsList() {
        setActiveBaseRendererOverride(.list)
    }

    func basesFocusQuickFilter() {
        guard activeBaseDocument != nil else { return }
        baseQuickFilterFocusToken += 1
    }

    @discardableResult
    func basesWhereAmI() -> String? {
        guard let doc = activeBaseDocument else { return nil }
        let text = doc.whereAmIReadback
        postAccessibilityAnnouncement(text, priority: .medium)
        return text
    }

    func clearBaseQuickFilterIfLeavingActiveTab(for destination: TabID) {
        guard let current = workspace.activeTab,
            current.id != destination
        else { return }
        clearActiveBaseQuickFilter()
    }

    func clearActiveBaseQuickFilter() {
        guard let item = workspace.activeTab?.item,
            let source = BaseDocumentSource(item: item),
            let doc = baseDocuments[source.key]
        else { return }
        _ = doc.clearQuickFilter(session: currentSession)
    }

    func dockBaseFileToSidebar(path: String, name: String? = nil, refreshDelayNanoseconds: UInt64 = 500_000_000) {
        basesDock.target = .base(
            path: path,
            name: name ?? ((path as NSString).lastPathComponent as NSString).deletingPathExtension)
        workspace.activeLeaf = .basesDock
        scheduleBasesDockFollowActiveRefresh(delayNanoseconds: refreshDelayNanoseconds)
    }

    func dockSavedQueryToSidebar(id: String, refreshDelayNanoseconds: UInt64 = 500_000_000) {
        guard let summary = savedQuerySummary(id: id) else {
            postBaseActionAnnouncement("Saved query is no longer available.")
            return
        }
        basesDock.target = .savedQuery(id: summary.id, name: summary.name)
        workspace.activeLeaf = .basesDock
        scheduleBasesDockFollowActiveRefresh(delayNanoseconds: refreshDelayNanoseconds)
    }

    func dockDashboardToSidebar(id: String, refreshDelayNanoseconds: UInt64 = 500_000_000) {
        guard let summary = dashboardSummary(id: id) else {
            postBaseActionAnnouncement("Dashboard is no longer available.")
            return
        }
        basesDock.target = .dashboard(id: summary.id, name: summary.name)
        workspace.activeLeaf = .basesDock
        scheduleBasesDockFollowActiveRefresh(delayNanoseconds: refreshDelayNanoseconds)
    }

    func scheduleBasesDockFollowActiveRefresh(delayNanoseconds: UInt64 = 500_000_000) {
        basesDockRefreshTask?.cancel()
        guard let target = basesDock.target, let session = currentSession else { return }
        let thisPath = basesDockActiveNotePath
        basesDock.thisPath = thisPath
        basesDockRefreshTask = Task { @MainActor [weak self, target, session, thisPath] in
            do {
                if delayNanoseconds > 0 {
                    try await Task.sleep(nanoseconds: delayNanoseconds)
                }
            } catch {
                return
            }
            guard let self, !Task.isCancelled, self.basesDock.target == target else { return }
            self.refreshBasesDockTarget(target, session: session, thisPath: thisPath)
        }
    }

    func clearBasesDock() {
        basesDockRefreshTask?.cancel()
        basesDockRefreshTask = nil
        if let session = currentSession {
            basesDockDocument?.close(session: session)
            basesDockDashboardDocument?.close(session: session)
        }
        basesDock = BasesDockState()
        basesDockDocument = nil
        basesDockDashboardDocument = nil
    }

    private func refreshBasesDockTarget(
        _ target: BasesDockTarget,
        session: VaultSession,
        thisPath: String?
    ) {
        let previous = basesDock.lastMembershipSignature
        switch target {
        case .base(let path, let name):
            if let dashboardDoc = basesDockDashboardDocument {
                dashboardDoc.close(session: session)
            }
            basesDockDashboardDocument = nil
            let doc = basesDockDocument ?? BaseDocument(source: .file(path: path))
            basesDockDocument = doc
            if doc.selectionKey != path {
                doc.close(session: session)
                doc.retarget(to: .file(path: path), session: nil)
            }
            if doc.handle == nil {
                doc.load(session: session, thisPath: thisPath)
            } else {
                doc.executeActiveView(session: session, thisPath: thisPath)
            }
            basesDock.lastMembershipSignature = doc.result?.rows.map(baseRowMembership) ?? []
            basesDock.target = .base(path: path, name: name)
        case .savedQuery(let id, let name):
            if let dashboardDoc = basesDockDashboardDocument {
                dashboardDoc.close(session: session)
            }
            basesDockDashboardDocument = nil
            let source = BaseDocumentSource.savedQuery(id: id, name: name)
            let doc = basesDockDocument ?? BaseDocument(source: source)
            basesDockDocument = doc
            if doc.selectionKey != source.selectionKey {
                doc.close(session: session)
                doc.retarget(to: source, session: nil)
            }
            if doc.handle == nil {
                doc.load(session: session, thisPath: thisPath)
            } else {
                doc.executeActiveView(session: session, thisPath: thisPath)
            }
            basesDock.lastMembershipSignature = doc.result?.rows.map(baseRowMembership) ?? []
        case .dashboard(let id, let name):
            if let baseDoc = basesDockDocument {
                baseDoc.close(session: session)
            }
            basesDockDocument = nil
            if let existing = basesDockDashboardDocument, existing.id != id {
                existing.close(session: session)
                basesDockDashboardDocument = nil
            }
            let doc = basesDockDashboardDocument ?? DashboardDocument(id: id, name: name)
            basesDockDashboardDocument = doc
            if doc.dashboard == nil {
                doc.load(session: session, thisPath: thisPath)
            } else {
                doc.refresh(session: session, thisPath: thisPath)
            }
            basesDock.lastMembershipSignature = doc.membershipSignature
        }
        if !previous.isEmpty, previous != basesDock.lastMembershipSignature {
            postBaseActionAnnouncement("Base dock updated for active note.")
        }
    }

    private func baseRowMembership(_ row: BasesRow) -> String {
        row.taskOrdinal.map { "\(row.filePath)#\($0)" } ?? row.filePath
    }

    private var basesDockActiveNotePath: String? {
        if case .markdown(let path) = workspace.activeTab?.item {
            return path
        }
        guard let selectedFilePath,
            selectedFilePath.lowercased().hasSuffix(".md")
        else { return nil }
        return selectedFilePath
    }

    private func reloadDashboardDocumentsAfterSavedQueryChange() {
        guard let session = currentSession else { return }
        for doc in dashboardDocuments.values {
            doc.load(session: session)
        }
        if let docked = basesDockDashboardDocument {
            docked.load(session: session, thisPath: basesDockActiveNotePath)
            basesDock.lastMembershipSignature = docked.membershipSignature
        }
    }

    private func setActiveBaseRendererOverride(_ mode: BaseRendererMode) {
        guard let tab = workspace.activeTab, BaseDocumentSource(item: tab.item) != nil else { return }
        baseRendererOverrides[tab.id] = mode
        postAccessibilityAnnouncement("Base view as \(mode.rawValue).", priority: .medium)
    }

    private func clearBaseRendererOverride(for tabID: TabID) {
        baseRendererOverrides[tabID] = nil
    }

    func basesOpenViewSwitcher() {
        guard let doc = activeBaseDocument else { return }
        postAccessibilityAnnouncement(
            "Base view switcher. \(doc.views.count) \(doc.views.count == 1 ? "view" : "views").",
            priority: .medium)
    }

    func basesNewQuery() {
        guard currentSession != nil else { return }
        activeBaseQueryBuilder = BaseQueryBuilderModel()
        postAccessibilityAnnouncement("New Bases query builder.", priority: .medium)
    }

    func basesEditViewFilters() {
        guard let doc = activeBaseDocument, let session = currentSession else { return }
        if doc.handle == nil {
            doc.load(session: session)
        }
        guard let handle = doc.handle else { return }
        do {
            let queryJSON = try session.baseViewEditQueryJson(
                handle: handle,
                view: UInt32(doc.activeViewIndex))
            activeBaseQueryBuilder = try BaseQueryBuilderModel(
                draft: BaseQueryBuilderDraft(queryJSON: queryJSON))
            let viewName = doc.activeViewName ?? "active view"
            postAccessibilityAnnouncement("Editing filters for \(viewName).", priority: .medium)
        } catch {
            postAccessibilityAnnouncement(
                "Base filters could not be opened in the builder: \(error.localizedDescription)",
                priority: .medium)
        }
    }

    func basesCloseQueryBuilder() {
        baseQueryBuilderPreviewTask?.cancel()
        baseQueryBuilderPreviewTask = nil
        baseQueryBuilderPreviewCancelToken?.cancel()
        baseQueryBuilderPreviewCancelToken = nil
        activeBaseQueryBuilder = nil
    }

    func basesBuilderSchedulePreview(delayNanoseconds: UInt64 = 300_000_000) {
        baseQueryBuilderPreviewTask?.cancel()
        baseQueryBuilderPreviewCancelToken?.cancel()
        baseQueryBuilderPreviewCancelToken = nil
        guard let model = activeBaseQueryBuilder, let session = currentSession else { return }
        let queryJSON: String
        do {
            queryJSON = try model.draft.queryJSON()
            model.previewState = .loading
        } catch {
            model.previewState = .failed(error.localizedDescription)
            return
        }
        let cancelToken = CancelToken()
        baseQueryBuilderPreviewCancelToken = cancelToken
        baseQueryBuilderPreviewTask = Task { [weak self, weak model, session, queryJSON, cancelToken] in
            defer {
                Task { @MainActor [weak self] in
                    if let current = self?.baseQueryBuilderPreviewCancelToken,
                        current === cancelToken
                    {
                        self?.baseQueryBuilderPreviewCancelToken = nil
                    }
                }
            }
            do {
                if delayNanoseconds > 0 {
                    try await Task.sleep(nanoseconds: delayNanoseconds)
                }
                if Task.isCancelled {
                    cancelToken.cancel()
                    return
                }
                let handle = try session.openQuery(queryJson: queryJSON, thisPath: nil)
                defer { session.closeBase(handle: handle) }
                let result = try session.baseExecute(
                    handle: handle,
                    view: 0,
                    thisPath: nil,
                    quickFilter: nil,
                    cancel: cancelToken)
                if Task.isCancelled {
                    cancelToken.cancel()
                    return
                }
                await MainActor.run { [weak self] in
                    self?.basesBuilderPublishPreview(
                        result: result,
                        for: model,
                        cancelToken: cancelToken)
                }
            } catch is CancellationError {
                cancelToken.cancel()
                return
            } catch {
                if Task.isCancelled || cancelToken.isCancelled() {
                    return
                }
                await MainActor.run { [weak self] in
                    self?.basesBuilderPublishPreviewFailure(
                        message: error.localizedDescription,
                        for: model,
                        cancelToken: cancelToken)
                }
            }
        }
    }

    func basesBuilderPublishPreview(
        result: BasesResultSet,
        for model: BaseQueryBuilderModel?,
        cancelToken: CancelToken
    ) {
        guard let model = currentBaseQueryBuilderPreviewModel(model, cancelToken: cancelToken)
        else { return }
        model.previewState = .ready(result)
        postAccessibilityAnnouncement(
            model.previewState.accessibilityAnnouncement,
            priority: .medium)
    }

    func basesBuilderPublishPreviewFailure(
        message: String,
        for model: BaseQueryBuilderModel?,
        cancelToken: CancelToken
    ) {
        guard let model = currentBaseQueryBuilderPreviewModel(model, cancelToken: cancelToken)
        else { return }
        model.previewState = .failed(message)
        postAccessibilityAnnouncement(
            "Base preview failed: \(message)",
            priority: .medium)
    }

    private func currentBaseQueryBuilderPreviewModel(
        _ model: BaseQueryBuilderModel?,
        cancelToken: CancelToken
    ) -> BaseQueryBuilderModel? {
        guard let model,
            activeBaseQueryBuilder === model,
            let currentToken = baseQueryBuilderPreviewCancelToken,
            currentToken === cancelToken,
            !cancelToken.isCancelled()
        else { return nil }
        return model
    }

    func basesBuilderSaveToView() {
        guard let model = activeBaseQueryBuilder,
            let doc = activeBaseDocument,
            let session = currentSession
        else { return }
        if doc.handle == nil {
            doc.load(session: session)
        }
        guard let handle = doc.handle else { return }
        do {
            for edit in try model.baseEditsForView(UInt32(doc.activeViewIndex)) {
                try session.baseApplyEdit(handle: handle, edit: edit)
            }
            doc.refresh(session: session)
            postAccessibilityAnnouncement("Saved builder changes to view.", priority: .medium)
        } catch {
            postAccessibilityAnnouncement(
                "Base view could not be saved: \(error.localizedDescription)",
                priority: .medium)
        }
    }

    func basesBuilderSaveAsBase(path: String) {
        guard let model = activeBaseQueryBuilder, let session = currentSession else { return }
        let trimmed = path.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else {
            postAccessibilityAnnouncement("Enter a .base path before saving.", priority: .medium)
            return
        }
        do {
            try session.saveQueryAsBase(queryJson: model.draft.queryJSON(), path: trimmed)
            refreshBaseQueries()
            postAccessibilityAnnouncement("Saved query as \(trimmed).", priority: .medium)
        } catch {
            postAccessibilityAnnouncement(
                "Base file could not be saved: \(error.localizedDescription)",
                priority: .medium)
        }
    }

    func basesBuilderSaveAsSavedQuery(name: String, description: String?) {
        guard let model = activeBaseQueryBuilder, let session = currentSession else { return }
        let trimmed = name.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else {
            postAccessibilityAnnouncement("Enter a saved query name before saving.", priority: .medium)
            return
        }
        do {
            _ = try session.saveQuery(
                name: trimmed,
                description: description?.trimmingCharacters(in: .whitespacesAndNewlines),
                queryJson: model.draft.queryJSON(),
                sourceSyntax: .builder)
            refreshBaseQueries()
            postAccessibilityAnnouncement("Saved query \(trimmed).", priority: .medium)
        } catch {
            postAccessibilityAnnouncement(
                "Saved query could not be created: \(error.localizedDescription)",
                priority: .medium)
        }
    }

    func basesBuilderUpdateSavedQuery() {
        guard let model = activeBaseQueryBuilder,
            let editingSavedQuery = model.editingSavedQuery,
            let session = currentSession
        else { return }
        do {
            try session.updateSavedQuery(
                id: editingSavedQuery.id,
                description: editingSavedQuery.description?.trimmingCharacters(in: .whitespacesAndNewlines),
                queryJson: model.draft.queryJSON(),
                sourceSyntax: .builder)
            refreshBaseQueries()
            refreshOpenSavedQueryDocument(id: editingSavedQuery.id, name: editingSavedQuery.name)
            postAccessibilityAnnouncement(
                "Updated saved query \(editingSavedQuery.name).",
                priority: .medium)
        } catch {
            postAccessibilityAnnouncement(
                "Saved query could not be updated: \(error.localizedDescription)",
                priority: .medium)
        }
    }

    private func refreshOpenSavedQueryDocument(id: String, name: String) {
        guard let session = currentSession else { return }
        let key = BaseDocumentSource.savedQuery(id: id, name: name).key
        baseDocuments[key]?.refresh(session: session)
    }

    func basesBuilderAddCondition() {
        activeBaseQueryBuilder?.perform(.addCondition)
        basesBuilderSchedulePreview()
    }

    func basesBuilderAddGroup() {
        activeBaseQueryBuilder?.perform(.addGroup)
        basesBuilderSchedulePreview()
    }

    func basesBuilderEditCondition() {
        guard let model = activeBaseQueryBuilder,
            let index = model.selectedRowIndex ?? model.editingRowIndex ?? model.rows.indices.first
        else { return }
        model.perform(.editCondition(index: index))
    }

    func basesBuilderRemoveCondition() {
        guard let model = activeBaseQueryBuilder,
            let index = model.selectedRowIndex ?? model.editingRowIndex ?? model.rows.indices.last
        else { return }
        model.perform(.removeCondition(index: index))
    }

    func basesLoadPropertyKeys() async -> [String] {
        guard let session = currentSession else { return [] }
        return await Task.detached(priority: .userInitiated) {
            (try? session.listPropertyKeys().map(\.key)) ?? []
        }.value
    }

    func basesLoadTags() async -> [String] {
        guard let session = currentSession else { return [] }
        return await Task.detached(priority: .userInitiated) {
            (try? session.listTags()) ?? []
        }.value
    }

    func basesLoadNotePaths() async -> [String] {
        guard let session = currentSession else { return [] }
        return await Task.detached(priority: .userInitiated) {
            var cursor: String?
            var out: [String] = []
            repeat {
                guard
                    let page = try? session.listFiles(
                        filter: .markdownOnly,
                        paging: Paging(cursor: cursor, limit: 5_000))
                else { break }
                out.append(contentsOf: page.items.map(\.path))
                cursor = page.nextCursor
            } while cursor != nil && out.count < 50_000
            return out
        }.value
    }

    func basesSelectNextView() {
        guard let doc = activeBaseDocument, let session = currentSession else { return }
        doc.selectNextView(session: session)
        if let name = doc.activeViewName {
            postAccessibilityAnnouncement("Base view: \(name).", priority: .medium)
        }
    }

    func basesSelectPreviousView() {
        guard let doc = activeBaseDocument, let session = currentSession else { return }
        doc.selectPreviousView(session: session)
        if let name = doc.activeViewName {
            postAccessibilityAnnouncement("Base view: \(name).", priority: .medium)
        }
    }

    func basesSortByColumn() {
        guard let session = currentSession,
            let text = activeBaseDocument?.sortFocusedColumn(session: session)
        else { return }
        postAccessibilityAnnouncement(text, priority: .medium)
    }

    func basesSaveSortToView() {
        guard let doc = activeBaseDocument, let session = currentSession else { return }
        do {
            if let text = try doc.saveSortToView(session: session) {
                postAccessibilityAnnouncement(text, priority: .medium)
            }
        } catch {
            postAccessibilityAnnouncement(
                "Base sort could not be saved: \(error.localizedDescription)",
                priority: .medium)
        }
    }

    func basesResultsPopover() {
        guard let doc = activeBaseDocument, let result = doc.result else { return }
        let suffix = doc.quickFilterActive ? " \(doc.whereAmIReadback)." : ""
        postAccessibilityAnnouncement("\(result.audioSummary)\(suffix)", priority: .medium)
    }

    @discardableResult
    func basesOpen(row: BasesRow) -> String? {
        if let taskOrdinal = row.taskOrdinal,
            let task = taskItem(path: row.filePath, ordinal: taskOrdinal)
        {
            openTaskRowInEditor(
                TaskWithLocation(task: task, path: row.filePath, fileName: baseFilename(row.filePath)))
            let text = "Opened \(baseFilename(row.filePath)), line \(task.line)."
            postBaseActionAnnouncement(text)
            return text
        }
        openFile(row.filePath, target: .currentTab)
        let text = "Opened \(baseFilename(row.filePath))."
        postBaseActionAnnouncement(text)
        return text
    }

    @discardableResult
    func basesCopyLink(for row: BasesRow) -> String {
        let link = baseWikilink(for: row.filePath)
        NSPasteboard.general.clearContents()
        NSPasteboard.general.setString(link, forType: .string)
        postBaseActionAnnouncement("Copied link to \(displayNameWithoutExtension(row.filePath)).")
        return link
    }

    @discardableResult
    func basesShowBacklinks(for row: BasesRow) -> String {
        openFile(row.filePath, target: .currentTab)
        workspace.activeLeaf = .backlinks
        workspace.focusLeafRegion()
        let text = "Backlinks for \(displayNameWithoutExtension(row.filePath))."
        postBaseActionAnnouncement(text)
        return text
    }

    func basesExportText(format: ExportFormat, includeQuickFilter: Bool = true) throws -> String {
        guard let doc = activeBaseDocument, let session = currentSession else {
            throw BaseActionError.noActiveBase
        }
        return try doc.export(
            format: format,
            session: session,
            includeQuickFilter: includeQuickFilter)
    }

    @discardableResult
    func basesCopyViewAsMarkdown(includeQuickFilter: Bool? = nil) -> String? {
        do {
            guard let doc = activeBaseDocument else {
                throw BaseActionError.noActiveBase
            }
            let shouldIncludeQuickFilter =
                includeQuickFilter ?? baseExportQuickFilterChoice(doc: doc, verb: "Copy")
            guard let shouldIncludeQuickFilter else { return nil }
            let markdown = try basesExportText(
                format: .markdown,
                includeQuickFilter: shouldIncludeQuickFilter)
            NSPasteboard.general.clearContents()
            NSPasteboard.general.setString(markdown, forType: .string)
            postBaseActionAnnouncement("Copied base view as Markdown.")
            return markdown
        } catch {
            postBaseActionAnnouncement("Base view could not be copied: \(error.localizedDescription)")
            return nil
        }
    }

    func basesExportCSV() {
        basesExportToSavePanel(format: .csv, fileExtension: "csv")
    }

    func basesExportMarkdown() {
        basesExportToSavePanel(format: .markdown, fileExtension: "md")
    }

    @discardableResult
    func basesSetProperty(row: BasesRow, column: BasesColumn, value: PropertyValue) async -> String? {
        await basesApplyProperty(row: row, column: column, action: .set(value))
    }

    @discardableResult
    func basesDeleteProperty(row: BasesRow, column: BasesColumn) async -> String? {
        await basesApplyProperty(row: row, column: column, action: .delete)
    }

    func basesOpenSelectedRow() {
        guard let row = activeBaseSelectedRowForCommand() else {
            postBaseActionAnnouncement("Select a base row first.")
            return
        }
        basesOpen(row: row)
    }

    func basesCopySelectedLink() {
        guard let row = activeBaseSelectedRowForCommand() else {
            postBaseActionAnnouncement("Select a base row first.")
            return
        }
        basesCopyLink(for: row)
    }

    func basesShowSelectedBacklinks() {
        guard let row = activeBaseSelectedRowForCommand() else {
            postBaseActionAnnouncement("Select a base row first.")
            return
        }
        basesShowBacklinks(for: row)
    }

    func basesEditSelectedProperty() {
        guard activeBaseSelectedRowForCommand() != nil else {
            postBaseActionAnnouncement("Select a base row first.")
            return
        }
        baseEditPropertyRequestToken &+= 1
    }

    func updateActiveBaseSelection(
        path: String,
        rowID: String?,
        columnIndex: Int?,
        result: BasesResultSet?
    ) {
        guard let result else {
            clearActiveBaseSelection()
            return
        }
        let rows = result.rows.enumerated().map { BaseGridRow(row: $0.element, ordinal: $0.offset) }
        guard let rowID, let selected = rows.first(where: { $0.id == rowID }) else {
            clearActiveBaseSelection()
            return
        }
        activeBaseSelectionPath = path
        activeBaseSelectedRow = selected.row
        if let columnIndex, result.columns.indices.contains(columnIndex) {
            activeBaseSelectedColumn = result.columns[columnIndex]
        } else {
            activeBaseSelectedColumn = result.columns.first {
                BaseCellEditPolicy.propertyKey(for: $0) != nil
            }
        }
    }

    func clearActiveBaseSelection() {
        activeBaseSelectionPath = nil
        activeBaseSelectedRow = nil
        activeBaseSelectedColumn = nil
    }

    func basesRefresh() {
        guard let doc = activeBaseDocument, let session = currentSession else { return }
        doc.refresh(session: session)
        postAccessibilityAnnouncement("Base refreshed.", priority: .medium)
    }

    func releaseBaseDocumentIfUnreferenced(_ item: EditorItem?) {
        guard let source = item.flatMap(BaseDocumentSource.init(item:)) else { return }
        let stillOpen = workspace.model.allTabs.contains { BaseDocumentSource(item: $0.item)?.key == source.key }
        guard !stillOpen, let doc = baseDocuments[source.key] else { return }
        if let session = currentSession {
            doc.close(session: session)
        }
        baseDocuments[source.key] = nil
    }

    func releaseDashboardDocumentIfUnreferenced(_ item: EditorItem?) {
        guard case .dashboard(let id, _) = item else { return }
        let stillOpen = workspace.model.allTabs.contains {
            if case .dashboard(let dashboardID, _) = $0.item {
                return dashboardID == id
            }
            return false
        }
        guard !stillOpen, let doc = dashboardDocuments[id] else { return }
        if let session = currentSession {
            doc.close(session: session)
        }
        dashboardDocuments[id] = nil
    }

    func rekeyBaseDocumentIfRetargeted(_ changed: [TabID], oldPath: String, newPath: String) {
        let oldKey = BaseDocumentSource.file(path: oldPath).key
        let newSource = BaseDocumentSource.file(path: newPath)
        let newKey = newSource.key
        guard oldPath != newPath,
            changed.contains(where: { id in
                workspace.model.allTabs.contains {
                    $0.id == id && $0.item == .base(path: newPath)
                }
            }),
            let doc = baseDocuments.removeValue(forKey: oldKey)
        else { return }
        if let existing = baseDocuments[newKey] {
            if existing !== doc, let session = currentSession {
                doc.close(session: session)
            }
            return
        }
        doc.retarget(to: newPath, session: currentSession)
        baseDocuments[newKey] = doc
    }

    func invalidateBaseDocument(path: String) {
        let key = BaseDocumentSource.file(path: path).key
        guard let doc = baseDocuments[key] else { return }
        if let session = currentSession {
            doc.close(session: session)
        }
        baseDocuments[key] = nil
    }

    func releaseAllBaseDocuments() {
        if let session = currentSession {
            for doc in baseDocuments.values {
                doc.close(session: session)
            }
        }
        baseDocuments = [:]
    }

    func releaseAllDashboardDocuments() {
        if let session = currentSession {
            for doc in dashboardDocuments.values {
                doc.close(session: session)
            }
            basesDockDocument?.close(session: session)
            basesDockDashboardDocument?.close(session: session)
        }
        dashboardDocuments = [:]
        basesDockDocument = nil
        basesDockDashboardDocument = nil
        basesDockRefreshTask?.cancel()
        basesDockRefreshTask = nil
        basesDock = BasesDockState()
    }

    func releaseAllBaseEmbedDocuments() {
        if let session = currentSession {
            for handle in baseEmbedHandles.values {
                handle.close(session: session)
            }
        }
        baseEmbedHandles = [:]
    }

    private enum BasePropertyAction: Equatable {
        case set(PropertyValue)
        case delete
    }

    private enum BaseActionError: LocalizedError {
        case noActiveBase

        var errorDescription: String? {
            switch self {
            case .noActiveBase:
                return "No active base."
            }
        }
    }

    private func basesApplyProperty(
        row: BasesRow,
        column: BasesColumn,
        action: BasePropertyAction
    ) async -> String? {
        guard let key = BaseCellEditPolicy.propertyKey(for: column) else {
            let hint = BaseCellEditPolicy.readOnlyHint(for: column)
            postBaseActionAnnouncement(hint)
            return hint
        }
        guard let session = currentSession, let doc = activeBaseDocument else { return nil }
        let outcome: Result<SaveReport, VaultError> = await Task.detached(priority: .userInitiated) {
            do {
                switch action {
                case .set(let value):
                    return .success(
                        try session.setProperty(
                            path: row.filePath,
                            key: key,
                            value: value,
                            expectedContentHash: nil))
                case .delete:
                    return .success(
                        try session.deleteProperty(
                            path: row.filePath,
                            key: key,
                            expectedContentHash: nil))
                }
            } catch let error as VaultError {
                return .failure(error)
            } catch {
                return .failure(.Io(message: error.localizedDescription))
            }
        }.value

        switch outcome {
        case .success:
            doc.executeActiveView(session: session)
            let stillPresent = doc.result?.rows.contains {
                $0.filePath == row.filePath && $0.taskOrdinal == row.taskOrdinal
            } ?? false
            let text: String
            if stillPresent {
                switch action {
                case .set(let value):
                    text = "Saved. \(column.label): \(BaseCellEditPolicy.displayValue(value))"
                case .delete:
                    text = "Saved. \(column.label): empty"
                }
            } else {
                text = "Saved. Row no longer matches this view"
            }
            postBaseActionAnnouncement(text)
            return text
        case .failure(let error):
            let text = "Base edit failed: \(error.localizedDescription)"
            postBaseActionAnnouncement(text)
            return text
        }
    }

    private func basesExportToSavePanel(format: ExportFormat, fileExtension: String) {
        guard let doc = activeBaseDocument else { return }
        do {
            guard let includeQuickFilter = baseExportQuickFilterChoice(doc: doc, verb: "Export")
            else { return }
            let text = try basesExportText(format: format, includeQuickFilter: includeQuickFilter)
            let panel = NSSavePanel()
            let viewName = doc.activeViewName ?? "View"
            panel.nameFieldStringValue = "\(doc.displayName) — \(viewName).\(fileExtension)"
            panel.begin { [weak self] response in
                guard response == .OK, let url = panel.url else { return }
                DispatchQueue.global(qos: .userInitiated).async { [weak self] in
                    let message: String
                    do {
                        try text.write(to: url, atomically: true, encoding: .utf8)
                        message = "Exported base view."
                    } catch {
                        message = "Base view could not be exported: \(error.localizedDescription)"
                    }
                    DispatchQueue.main.async { [weak self] in
                        self?.postBaseActionAnnouncement(message)
                    }
                }
            }
        } catch {
            postBaseActionAnnouncement("Base view could not be exported: \(error.localizedDescription)")
        }
    }

    private func baseExportQuickFilterChoice(doc: BaseDocument, verb: String) -> Bool? {
        guard doc.quickFilterActive, let result = doc.result else { return true }
        let alert = NSAlert()
        alert.alertStyle = .informational
        alert.messageText = "\(verb) quick-filtered base view?"
        alert.informativeText =
            "Quick filter \"\(doc.quickFilterText)\" is active. Choose filtered rows "
            + "(\(result.shownCount)) or all rows (\(result.unfilteredShownCount))."
        alert.addButton(withTitle: "\(verb) Filtered Rows")
        alert.addButton(withTitle: "\(verb) All Rows")
        alert.addButton(withTitle: "Cancel")
        switch alert.runModal() {
        case .alertFirstButtonReturn:
            return true
        case .alertSecondButtonReturn:
            return false
        default:
            postBaseActionAnnouncement("\(verb) canceled.")
            return nil
        }
    }

    func postBaseActionAnnouncement(_ message: String) {
        lastBaseActionAnnouncement = message
        postAccessibilityAnnouncement(message, priority: .medium)
    }

    private func activeBaseSelectedRowForCommand() -> BasesRow? {
        guard let doc = activeBaseDocument,
            activeBaseSelectionPath == doc.selectionKey,
            let row = activeBaseSelectedRow,
            doc.result?.rows.contains(where: { $0.hasSameBaseIdentity(as: row) }) == true
        else { return nil }
        return row
    }

    private func taskItem(path: String, ordinal: UInt64) -> TaskItem? {
        guard let session = currentSession else { return nil }
        return try? session.tasksForFile(path: path).first {
            UInt64($0.ordinal) == ordinal
        }
    }

    private func baseWikilink(for path: String) -> String {
        let target = (path as NSString).deletingPathExtension
        return "[[\(target)]]"
    }

    private func displayNameWithoutExtension(_ path: String) -> String {
        let name = (path as NSString).lastPathComponent
        return (name as NSString).deletingPathExtension
    }

    private func baseFilename(_ path: String) -> String {
        (path as NSString).lastPathComponent
    }
}

extension BasesRow {
    func hasSameBaseIdentity(as other: BasesRow) -> Bool {
        filePath == other.filePath && taskOrdinal == other.taskOrdinal
    }
}
