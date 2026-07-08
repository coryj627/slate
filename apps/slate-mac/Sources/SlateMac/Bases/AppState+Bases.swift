// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
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

    func basesExportText(format: ExportFormat) throws -> String {
        guard let doc = activeBaseDocument, let session = currentSession else {
            throw BaseActionError.noActiveBase
        }
        return try doc.export(format: format, session: session)
    }

    @discardableResult
    func basesCopyViewAsMarkdown() -> String? {
        do {
            let markdown = try basesExportText(format: .markdown)
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
        guard activeBaseDocument != nil, let row = activeBaseSelectedRow else {
            postBaseActionAnnouncement("Select a base row first.")
            return
        }
        basesOpen(row: row)
    }

    func basesCopySelectedLink() {
        guard activeBaseDocument != nil, let row = activeBaseSelectedRow else {
            postBaseActionAnnouncement("Select a base row first.")
            return
        }
        basesCopyLink(for: row)
    }

    func basesShowSelectedBacklinks() {
        guard activeBaseDocument != nil, let row = activeBaseSelectedRow else {
            postBaseActionAnnouncement("Select a base row first.")
            return
        }
        basesShowBacklinks(for: row)
    }

    func basesEditSelectedProperty() {
        guard activeBaseDocument != nil, activeBaseSelectedRow != nil else {
            postBaseActionAnnouncement("Select a base row first.")
            return
        }
        baseEditPropertyRequestToken &+= 1
    }

    func updateActiveBaseSelection(rowID: String?, columnIndex: Int?, result: BasesResultSet?) {
        guard let result else {
            activeBaseSelectedRow = nil
            activeBaseSelectedColumn = nil
            return
        }
        let rows = result.rows.enumerated().map { BaseGridRow(row: $0.element, ordinal: $0.offset) }
        guard let rowID, let selected = rows.first(where: { $0.id == rowID }) else {
            activeBaseSelectedRow = nil
            activeBaseSelectedColumn = nil
            return
        }
        activeBaseSelectedRow = selected.row
        if let columnIndex, result.columns.indices.contains(columnIndex) {
            activeBaseSelectedColumn = result.columns[columnIndex]
        } else {
            activeBaseSelectedColumn = result.columns.first {
                BaseCellEditPolicy.propertyKey(for: $0) != nil
            }
        }
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
            let text = try basesExportText(format: format)
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

    func postBaseActionAnnouncement(_ message: String) {
        lastBaseActionAnnouncement = message
        postAccessibilityAnnouncement(message, priority: .medium)
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
