// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

/// Bases tab lifecycle (Milestone N, #702): the `.base` arm of the
/// single navigation funnel, the per-path document registry, and the
/// palette command actions.
extension AppState {
    func baseDocument(for path: String) -> BaseDocument {
        if let existing = baseDocuments[path] { return existing }
        let doc = BaseDocument(path: path)
        baseDocuments[path] = doc
        return doc
    }

    func openBaseFile(_ path: String, target: OpenTarget) {
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

    func activateBaseTab(_ id: TabID, path: String) {
        if id == workspace.model.activeGroup.activeTabID,
            selectedFilePath == path,
            baseDocuments[path]?.handle != nil
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
        let doc = baseDocument(for: path)
        if doc.handle == nil, let session = currentSession {
            doc.load(session: session)
        }
        if selectedFilePath != path {
            selectedFilePath = path
        }
    }

    var activeBaseDocument: BaseDocument? {
        guard let tab = workspace.activeTab, case .base(let path) = tab.item else { return nil }
        return baseDocument(for: path)
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
        guard case .base(let path) = workspace.activeTab?.item,
            let doc = baseDocuments[path]
        else { return }
        _ = doc.clearQuickFilter(session: currentSession)
    }

    private func setActiveBaseRendererOverride(_ mode: BaseRendererMode) {
        guard let tab = workspace.activeTab, case .base = tab.item else { return }
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
        guard let text = activeBaseDocument?.sortFocusedColumn() else { return }
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

    func basesRefresh() {
        guard let doc = activeBaseDocument, let session = currentSession else { return }
        doc.refresh(session: session)
        postAccessibilityAnnouncement("Base refreshed.", priority: .medium)
    }

    func releaseBaseDocumentIfUnreferenced(_ item: EditorItem?) {
        guard case .base(let path) = item else { return }
        let stillOpen = workspace.model.allTabs.contains { $0.item == .base(path: path) }
        guard !stillOpen, let doc = baseDocuments[path] else { return }
        if let session = currentSession {
            doc.close(session: session)
        }
        baseDocuments[path] = nil
    }

    func rekeyBaseDocumentIfRetargeted(_ changed: [TabID], oldPath: String, newPath: String) {
        guard oldPath != newPath,
            changed.contains(where: { id in
                workspace.model.allTabs.contains {
                    $0.id == id && $0.item == .base(path: newPath)
                }
            }),
            let doc = baseDocuments.removeValue(forKey: oldPath)
        else { return }
        if let existing = baseDocuments[newPath] {
            if existing !== doc, let session = currentSession {
                doc.close(session: session)
            }
            return
        }
        doc.retarget(to: newPath, session: currentSession)
        baseDocuments[newPath] = doc
    }

    func invalidateBaseDocument(path: String) {
        guard let doc = baseDocuments[path] else { return }
        if let session = currentSession {
            doc.close(session: session)
        }
        baseDocuments[path] = nil
    }

    func releaseAllBaseDocuments() {
        if let session = currentSession {
            for doc in baseDocuments.values {
                doc.close(session: session)
            }
        }
        baseDocuments = [:]
    }
}
