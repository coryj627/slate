// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI

/// Workspace renderer for `.base` tabs (N3-1, #702): a compact view
/// switcher and a table result backed by `AccessibleDataGrid` v2.
struct BaseContainerView: View {
    @EnvironmentObject private var appState: AppState
    @ObservedObject var document: BaseDocument
    let tabID: TabID

    @State private var selectedRow: String?
    @State private var selectedCell: BaseGridCellSelection?
    @State private var quickFilterTask: Task<Void, Never>?
    @State private var resultFocusToken = 0
    @State private var gridEditRequest: AccessibleDataGrid<BaseGridRow>.EditRequest?
    @FocusState private var quickFilterFocused: Bool

    var body: some View {
        VStack(spacing: 0) {
            header
            Divider()
            banners
            content
        }
        .background(Tokens.ColorRole.surface)
        .onKeyPress(.escape) {
            guard quickFilterFocused || document.quickFilterActive else { return .ignored }
            quickFilterFocused = false
            clearQuickFilterAndRestoreSelection()
            return .handled
        }
        .onAppear {
            if document.handle == nil, let session = appState.currentSession {
                if document.hasPendingRetargetPreparation {
                    appState.scheduleBaseRetargetPreparationIfNeeded(
                        document: document,
                        owner: .registry(key: document.source.key),
                        source: document.source,
                        session: session)
                } else {
                    appState.loadBaseDocumentIfAllowed(document, session: session)
                }
            }
            updateActiveBaseSelection()
        }
        .onDisappear {
            quickFilterTask?.cancel()
            quickFilterTask = nil
        }
        .onChange(of: appState.baseQuickFilterFocusToken) { _, _ in
            guard handlesActiveTabActions else { return }
            quickFilterFocused = true
        }
        .onChange(of: document.quickFilterText) { _, _ in
            guard quickFilterFocused else { return }
            scheduleQuickFilterExecution(previousSelection: selectedRow)
        }
        .onChange(of: document.result) { _, result in
            reconcileSelectedCell(with: result)
            updateActiveBaseSelection()
        }
        .onChange(of: appState.baseEditPropertyRequestToken) { _, _ in
            guard handlesActiveTabActions else { return }
            beginSelectedPropertyEdit()
        }
        .onChange(of: appState.workspace.model.activeGroup.activeTabID) { _, _ in
            guard handlesActiveTabActions else { return }
            updateActiveBaseSelection()
        }
    }

    private var header: some View {
        HStack(spacing: Tokens.Spacing.sm) {
            SlateSymbol.base.decorative
                .foregroundStyle(Tokens.ColorRole.textSecondary)
            Text(document.displayName)
                .font(Tokens.Typography.body.weight(.semibold))
                .foregroundStyle(Tokens.ColorRole.textPrimary)
                .lineLimit(2)
            Picker("View", selection: activeViewBinding) {
                ForEach(Array(document.views.enumerated()), id: \.offset) { index, view in
                    Text(view.name).tag(index)
                }
            }
            .labelsHidden()
            .frame(maxWidth: 220)
            .disabled(
                document.views.isEmpty
                    || batchTrashInteractionDisabledReason != nil)
            .accessibilityHint(
                batchTrashInteractionDisabledReason
                    ?? "Choose the active Base view.")
            .help(
                batchTrashInteractionDisabledReason
                    ?? "Choose the active Base view")
            Spacer(minLength: 0)
            Text(resultCountText)
                .font(Tokens.Typography.caption)
                .foregroundStyle(Tokens.ColorRole.textSecondary)
            TextField("Quick filter", text: $document.quickFilterText)
                .textFieldStyle(.roundedBorder)
                .frame(width: 180)
                .focused($quickFilterFocused)
                .accessibilityLabel("Quick filter — temporary, does not change the base")
                .accessibilityValue(
                    document.quickFilterActive ? document.quickFilterText : "No quick filter")
                .disabled(batchTrashInteractionDisabledReason != nil)
                .accessibilityHint(
                    batchTrashInteractionDisabledReason
                        ?? "Temporarily filter the visible Base results.")
                .help(
                    batchTrashInteractionDisabledReason
                        ?? "Quick filter")
            Button {
                appState.basesEditViewFilters()
            } label: {
                SlateSymbol.rename.image(label: "Edit filters")
            }
            .buttonStyle(.interactiveRow())
            .disabled(baseDefinitionEditingDisabledReason != nil)
            .accessibilityHint(
                baseDefinitionEditingDisabledReason
                    ?? "Open the active view filters in the query builder.")
            .help(baseDefinitionEditingDisabledReason ?? "Edit filters")
            if let recoveryLabel = appState.baseRecoveryActionLabel(for: document) {
                let recoveryDisabledReason = appState.structuralMutationDisabledReason
                Button {
                    _ = appState.retryBaseRecovery(for: document)
                } label: {
                    SlateSymbol.syncDiagnostics.image(label: recoveryLabel)
                }
                .buttonStyle(.interactiveRow())
                .disabled(recoveryDisabledReason != nil)
                .accessibilityHint(
                    recoveryDisabledReason
                        ?? appState.baseRecoveryActionHint(for: document)
                        ?? "Attempts to restore Base interaction.")
                .help(
                    recoveryDisabledReason
                        ?? appState.baseRecoveryActionHint(for: document)
                        ?? recoveryLabel)
            } else {
                Button {
                    appState.basesRefresh()
                } label: {
                    SlateSymbol.syncDiagnostics.image(label: "Refresh")
                }
                .buttonStyle(.interactiveRow())
                .disabled(baseRefreshDisabledReason != nil)
                .accessibilityHint(
                    baseRefreshDisabledReason
                        ?? "Reload the Base and its current view.")
                .help(baseRefreshDisabledReason ?? "Refresh")
            }
            Button {
                appState.basesResultsPopover()
            } label: {
                SlateSymbol.moreActions.image(label: "Results")
            }
            .buttonStyle(.interactiveRow())
            .help("Results")
        }
        .padding(.horizontal, Tokens.Spacing.md)
        .padding(.vertical, Tokens.Spacing.xs)
        .slateSymbolSurface(.toolbar)
    }

    @ViewBuilder
    private var banners: some View {
        if let warning = stateWarning {
            banner(warning)
        }
        if let result = document.result {
            ForEach(Array(result.warnings.enumerated()), id: \.offset) { _, warning in
                banner(warning)
            }
            if let error = result.viewError, !error.isEmpty {
                banner(error)
            }
        }
        if batchTrashInteractionDisabledReason != nil,
            !document.quickFilterText.isEmpty
        {
            quickFilterDraftRecoveryBanner
        }
    }

    private var quickFilterDraftRecoveryBanner: some View {
        HStack(alignment: .firstTextBaseline, spacing: Tokens.Spacing.xs) {
            Text("Quick filter draft")
                .font(Tokens.Typography.caption.weight(.semibold))
            Text(verbatim: document.quickFilterText)
                .font(Tokens.Typography.code)
                .textSelection(.enabled)
                .lineLimit(2)
                .frame(maxWidth: .infinity, alignment: .leading)
                .accessibilityLabel(
                    "Quick filter draft: \(document.quickFilterText)")
                .accessibilityHint("Selectable copy of the preserved quick filter draft.")
            Button("Copy Quick Filter Draft") {
                appState.copyBaseQuickFilterDraft(document)
            }
            .accessibilityHint("Copies the preserved quick filter text.")
        }
        .padding(.horizontal, Tokens.Spacing.md)
        .padding(.vertical, Tokens.Spacing.xs)
        .background(Tokens.ColorRole.surfaceSecondary)
        .accessibilityElement(children: .contain)
    }

    @ViewBuilder
    private var content: some View {
        switch document.state {
        case .loading:
            placeholder("Loading base…")
        case .failed(let message):
            placeholder(message)
        case .ready, .degraded:
            if let result = document.result {
                switch BaseResultContentState(result: result) {
                case .empty:
                    placeholder("No base results.")
                case .rowOnly:
                    resultList(result)
                case .tabular:
                    resultRenderer(result)
                }
            } else {
                placeholder("No base results.")
            }
        }
    }

    @ViewBuilder
    private func resultRenderer(_ result: BasesResultSet) -> some View {
        switch BaseRendererMode.resolved(
            view: activeView,
            override: appState.baseRendererOverride(for: tabID))
        {
        case .table:
            resultGrid(result)
        case .list:
            resultList(result)
        }
    }

    private func resultGrid(_ result: BasesResultSet) -> some View {
        AccessibleDataGrid(
            columns: columns(from: result),
            rows: rows(from: result),
            summary: summaryText(result),
            accessibilityLabel: result.audioSummary,
            groups: groups(from: result),
            selection: Binding(
                get: { selectedRow },
                set: {
                    selectedRow = $0
                    updateActiveBaseSelection()
                }),
            cellSelection: Binding(
                get: { selectedCell?.position(in: result) },
                set: { position in
                    selectedCell = position.flatMap {
                        BaseGridCellSelection(position: $0, result: result)
                    }
                    if let columnIndex = selectedCell?.columnIndex(in: result) {
                        document.focusColumn(columnIndex)
                    }
                    updateActiveBaseSelection()
                }),
            sortState: gridSortBinding,
            cellNavigation: true,
            sortsRowsLocally: false,
            onActivate: { _ = appState.basesOpen(row: $0.row) },
            onEditCell: { row, columnIndex in
                beginEdit(row: row.row, columnIndex: columnIndex, result: result)
            },
            editRequest: Binding(
                get: {
                    gridEditRequest.flatMap { request in
                        selectedCell?.reconciledEditRequest(request, in: result)
                    }
                },
                set: { gridEditRequest = $0 }),
            onCommitEdit: { row, columnIndex, draft, navigation in
                commitEdit(
                    row: row.row,
                    rowID: row.id,
                    columnIndex: columnIndex,
                    draft: draft,
                    navigation: navigation,
                    result: result)
            },
            onCancelEdit: {
                gridEditRequest = nil
                appState.postBaseActionAnnouncement("Edit canceled.")
            },
            rowAccessibilityDescription: { $0.row.audioDescription },
            rowActions: gridRowActions(result: result),
            focusRequest: resultFocusToken)
    }

    private func resultList(_ result: BasesResultSet) -> some View {
        let projection = BaseListProjection(
            result: result,
            options: BaseListOptions(slateStateJson: activeView?.slateStateJson),
            isQuickFiltered: document.quickFilterActive)
        return BaseListView(
            projection: projection,
            selection: Binding(
                get: { selectedRow },
                set: {
                    selectedRow = $0
                    updateActiveBaseSelection()
                }),
            focusRequest: resultFocusToken,
            onActivate: { _ = appState.basesOpen(row: $0.row) },
            rowActions: listRowActions(result: result))
    }

    private func gridRowActions(
        result: BasesResultSet
    ) -> [AccessibleDataGrid<BaseGridRow>.RowAction] {
        var actions: [AccessibleDataGrid<BaseGridRow>.RowAction] = [
            .init("Open") { row in _ = appState.basesOpen(row: row.row) },
            .init("Copy link") { row in _ = appState.basesCopyLink(for: row.row) },
            .init("Show backlinks") { row in _ = appState.basesShowBacklinks(for: row.row) },
            // O15 handoff: the reserved "show local graph" slot, now
            // that Milestone P's Connections leaf exists (P1-1 #554).
            .init("Show connections") { row in appState.basesShowConnections(for: row.row) },
        ]
        if BaseGridRowActionPolicy.canEditProperty(in: result) {
            actions.append(
            .init("Edit property") { row in
                let columnIndex = selectedCell?.rowID == row.id
                    ? selectedCell?.columnIndex(in: result)
                    : nil
                beginEdit(
                    row: row.row,
                    columnIndex: columnIndex ?? firstEditableColumnIndex(result: result),
                    result: result)
            })
        }
        return actions
    }

    private func listRowActions(result: BasesResultSet) -> [BaseListRowAction] {
        var actions: [BaseListRowAction] = [
            .init("Open") { row in _ = appState.basesOpen(row: row.row) },
            .init("Copy link") { row in _ = appState.basesCopyLink(for: row.row) },
            .init("Show backlinks") { row in _ = appState.basesShowBacklinks(for: row.row) },
            // O15 handoff (P1-1 #554), mirroring the grid actions.
            .init("Show connections") { row in appState.basesShowConnections(for: row.row) },
        ]
        if BaseGridRowActionPolicy.canEditProperty(in: result) {
            actions.append(
            .init("Edit property") { row in
                appState.basesViewAsTable()
                beginEdit(
                    row: row.row,
                    columnIndex: firstEditableColumnIndex(result: result),
                    result: result)
            })
        }
        return actions
    }

    private func columns(from result: BasesResultSet) -> [AccessibleDataGrid<BaseGridRow>.Column] {
        let sortingAvailable = batchTrashInteractionDisabledReason == nil
        return result.columns.enumerated().map { columnIndex, column in
            AccessibleDataGrid<BaseGridRow>.Column(
                column.label,
                cell: { row in row.value(at: columnIndex) },
                sort: sortingAvailable
                    ? { lhs, rhs in lhs.sortsBefore(rhs, at: columnIndex) }
                    : nil,
                accessibilityHint: { _ in
                    BaseCellEditPolicy.propertyKey(for: column) == nil
                        ? BaseCellEditPolicy.readOnlyHint(for: column)
                        : nil
                })
        }
    }

    private func rows(from result: BasesResultSet) -> [BaseGridRow] {
        result.rows.enumerated().map { rowIndex, row in
            BaseGridRow(row: row, ordinal: rowIndex)
        }
    }

    private func groups(from result: BasesResultSet) -> [AccessibleDataGrid<BaseGridRow>.Group] {
        result.groups.map {
            .init(
                label: $0.label,
                rowStart: Int($0.rowStart),
                rowCount: Int($0.rowCount),
                summary: BaseSummaryFormatter.summaryText(
                    summaries: $0.summaries, columns: result.columns))
        }
    }

    private func summaryText(_ result: BasesResultSet) -> String {
        BaseSummaryFormatter.summaryText(result, isQuickFiltered: document.quickFilterActive)
    }

    private func scheduleQuickFilterExecution(previousSelection: String?) {
        quickFilterTask?.cancel()
        quickFilterTask = Task { @MainActor in
            do {
                try await Task.sleep(nanoseconds: 150_000_000)
            } catch {
                return
            }
            guard !Task.isCancelled, let session = appState.currentSession else { return }
            let announcement = document.applyQuickFilter(document.quickFilterText, session: session)
            restoreSelection(previous: previousSelection)
            postAccessibilityAnnouncement(announcement, priority: .medium)
        }
    }

    private func clearQuickFilterAndRestoreSelection() {
        quickFilterTask?.cancel()
        quickFilterTask = nil
        let previousSelection = selectedRow
        let announcement = document.clearQuickFilter(session: appState.currentSession)
        restoreSelection(previous: previousSelection)
        if let announcement {
            postAccessibilityAnnouncement(announcement, priority: .medium)
        }
        resultFocusToken &+= 1
    }

    private func restoreSelection(previous: String?) {
        guard let result = document.result else {
            selectedRow = nil
            updateActiveBaseSelection()
            return
        }
        selectedRow = BaseSelectionRestorer.restoredSelection(
            previous: previous,
            current: selectedRow,
            availableIDs: rows(from: result).map(\.id))
        updateActiveBaseSelection()
    }

    private func updateActiveBaseSelection() {
        guard handlesActiveTabActions else { return }
        appState.updateActiveBaseSelection(
            path: document.selectionKey,
            rowID: selectedRow,
            columnIndex: document.result.flatMap { selectedCell?.columnIndex(in: $0) },
            result: document.result)
    }

    private var handlesActiveTabActions: Bool {
        BaseContainerTabRouting.handles(
            tabID: tabID,
            activeTabID: appState.workspace.model.activeGroup.activeTabID)
    }

    private func reconcileSelectedCell(with result: BasesResultSet?) {
        guard let selectedCell else { return }
        let reconciliation = BaseGridSelectionReconciliation(
            selectedCell: selectedCell,
            result: result)
        self.selectedCell = reconciliation.selectedCell
        selectedRow = reconciliation.selectedRowID
        if reconciliation.clearEditRequest {
            gridEditRequest = nil
        }
        guard let result, let columnIndex = reconciliation.columnIndex else {
            if let result, !result.columns.isEmpty {
                document.focusColumn(firstEditableColumnIndex(result: result))
            }
            return
        }
        document.focusColumn(columnIndex)
        if let gridEditRequest {
            self.gridEditRequest = selectedCell.reconciledEditRequest(
                gridEditRequest, in: result)
        }
    }

    private func beginSelectedPropertyEdit() {
        guard let row = appState.activeBaseSelectedRow,
            let result = document.result
        else { return }
        if BaseRendererMode.resolved(
            view: activeView,
            override: appState.baseRendererOverride(for: tabID)) == .list
        {
            appState.basesViewAsTable()
        }
        let columnIndex: Int
        if selectedCell?.rowID == BaseGridRow.id(for: row),
            let index = selectedCell?.columnIndex(in: result)
        {
            columnIndex = index
        } else if let column = appState.activeBaseSelectedColumn,
            let index = result.exactColumnIndex(forID: column.id)
        {
            columnIndex = index
        } else {
            columnIndex = firstEditableColumnIndex(result: result)
        }
        beginEdit(row: row, columnIndex: columnIndex, result: result)
    }

    private func beginEdit(row: BasesRow, columnIndex: Int, result: BasesResultSet) {
        guard result.columns.indices.contains(columnIndex),
            row.values.indices.contains(columnIndex)
        else { return }
        let column = result.columns[columnIndex]
        guard BaseCellEditPolicy.propertyKey(for: column) != nil else {
            appState.postBaseActionAnnouncement(BaseCellEditPolicy.readOnlyHint(for: column))
            return
        }
        let rowID = BaseGridRow.id(for: row)
        selectedRow = rowID
        selectedCell = BaseGridCellSelection(rowID: rowID, columnID: column.id)
        gridEditRequest = .init(
            rowID: rowID,
            columnIndex: columnIndex,
            text: BaseCellEditPolicy.draftText(from: row.values[columnIndex]))
        updateActiveBaseSelection()
    }

    private func commitEdit(
        row: BasesRow,
        rowID: String,
        columnIndex: Int,
        draft: String,
        navigation: AccessibleDataGrid<BaseGridRow>.EditCommitNavigation,
        result: BasesResultSet
    ) {
        guard result.columns.indices.contains(columnIndex) else { return }
        let column = result.columns[columnIndex]
        let previousSelection = selectedRow
        if draft.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
            gridEditRequest = nil
            Task { @MainActor in
                await appState.basesDeleteProperty(row: row, column: column)
                restoreSelection(previous: previousSelection)
                moveAfterEditCommit(navigation, from: rowID, columnIndex: columnIndex)
            }
            return
        }
        switch BaseCellEditPolicy.propertyValue(from: draft, valueKind: column.valueKind) {
        case .success(let value):
            gridEditRequest = nil
            Task { @MainActor in
                await appState.basesSetProperty(row: row, column: column, value: value)
                restoreSelection(previous: previousSelection)
                moveAfterEditCommit(navigation, from: rowID, columnIndex: columnIndex)
            }
        case .failure(let error):
            gridEditRequest = .init(rowID: rowID, columnIndex: columnIndex, text: draft)
            appState.postBaseActionAnnouncement(error.message)
        }
    }

    private func moveAfterEditCommit(
        _ navigation: AccessibleDataGrid<BaseGridRow>.EditCommitNavigation,
        from rowID: String,
        columnIndex: Int
    ) {
        guard navigation != .stay,
            let result = document.result,
            !result.rows.isEmpty,
            !result.columns.isEmpty
        else { return }
        let gridRows = rows(from: result)
        guard let currentRowIndex = gridRows.firstIndex(where: { $0.id == rowID }) else {
            return
        }
        let currentLinearIndex = currentRowIndex * result.columns.count + columnIndex
        let nextLinearIndex: Int
        switch navigation {
        case .stay:
            return
        case .next:
            nextLinearIndex = min(currentLinearIndex + 1, gridRows.count * result.columns.count - 1)
        case .previous:
            nextLinearIndex = max(currentLinearIndex - 1, 0)
        }
        let nextRow = gridRows[nextLinearIndex / result.columns.count]
        let nextColumnIndex = nextLinearIndex % result.columns.count
        selectedRow = nextRow.id
        selectedCell = BaseGridCellSelection(
            rowID: nextRow.id,
            columnID: result.columns[nextColumnIndex].id)
        updateActiveBaseSelection()
        resultFocusToken &+= 1
    }

    private func firstEditableColumnIndex(result: BasesResultSet) -> Int {
        result.columns.firstIndex { BaseCellEditPolicy.propertyKey(for: $0) != nil } ?? 0
    }

    private var activeViewBinding: Binding<Int> {
        Binding(
            get: { document.activeViewIndex },
            set: { index in
                guard appState.admitBaseDocumentInteraction(document) else { return }
                guard let session = appState.currentSession else { return }
                document.selectView(index: index, session: session)
            })
    }

    /// Removing both the sort binding and every comparator prevents AppKit
    /// from exposing clickable/VoiceOver-actionable table headers while the
    /// native Base handle is detached. The admission check remains in the
    /// setter as a stale-event backstop if availability changes mid-gesture.
    private var gridSortBinding: Binding<DataGridSortState?>? {
        guard batchTrashInteractionDisabledReason == nil else { return nil }
        return Binding(
            get: { document.sortState },
            set: { sort in
                guard appState.admitBaseDocumentInteraction(document),
                    let session = appState.currentSession
                else { return }
                document.setTransientSort(sort, session: session)
            })
    }

    private var batchTrashInteractionDisabledReason: String? {
        if let path = document.source.filePath {
            switch appState.batchTrashPathCapability(for: path) {
            case .writable:
                break
            case .readOnly(let reason), .invalid(let reason):
                return reason
            }
        }
        return appState.baseDocumentAvailabilityDisabledReason(for: document)
    }

    private var baseRefreshDisabledReason: String? {
        appState.baseDocumentRefreshDisabledReason(for: document)
    }

    private var baseDefinitionEditingDisabledReason: String? {
        appState.baseDefinitionEditingDisabledReason(for: document)
    }

    private var activeView: BaseViewSummary? {
        guard document.views.indices.contains(document.activeViewIndex) else { return nil }
        return document.views[document.activeViewIndex]
    }

    private var resultCountText: String {
        guard let result = document.result else { return "No results" }
        let total = document.quickFilterActive ? result.unfilteredShownCount : result.totalCount
        return "\(result.shownCount) of \(total)"
    }

    private var stateWarning: String? {
        if case .degraded(let message) = document.state { return message }
        return nil
    }

    private func banner(_ text: String) -> some View {
        HStack(spacing: Tokens.Spacing.xs) {
            SlateSymbol.warning.decorative
            Text(text)
                .font(Tokens.Typography.caption)
                .foregroundStyle(Tokens.ColorRole.textPrimary)
                .lineLimit(2)
            Spacer(minLength: 0)
        }
        .padding(.horizontal, Tokens.Spacing.md)
        .padding(.vertical, Tokens.Spacing.xs)
        .background(Tokens.ColorRole.surfaceSecondary)
        .accessibilityLabel(text)
    }

    private func placeholder(_ text: String) -> some View {
        Text(text)
            .font(Tokens.Typography.callout)
            .foregroundStyle(Tokens.ColorRole.textSecondary)
            .textSelection(.enabled)
            .frame(maxWidth: .infinity, maxHeight: .infinity)
    }
}

enum BaseContainerTabRouting {
    static func handles(tabID: TabID, activeTabID: TabID?) -> Bool {
        tabID == activeTabID
    }
}

extension BasesResultSet {
    func exactColumnIndex(forID columnID: String) -> Int? {
        columns.firstIndex { BaseExactIdentity.matches($0.id, columnID) }
    }
}

struct BaseGridRow: Identifiable {
    let row: BasesRow
    let ordinal: Int

    var id: String {
        Self.id(for: row)
    }

    static func id(for row: BasesRow) -> String {
        let pathBytes = row.filePath.utf8.map { String($0) }.joined(separator: ".")
        let task = row.taskOrdinal.map { String($0) } ?? "file"
        return "\(pathBytes)#\(task)"
    }

    func value(at columnIndex: Int) -> String {
        guard row.values.indices.contains(columnIndex) else { return "" }
        return row.values[columnIndex].display
    }

    func sortsBefore(
        _ other: BaseGridRow,
        at columnIndex: Int,
        ascending: Bool = true
    ) -> Bool {
        guard row.values.indices.contains(columnIndex),
            other.row.values.indices.contains(columnIndex)
        else { return pathTiebreaksBefore(other) }
        let lhs = row.values[columnIndex]
        let rhs = other.row.values[columnIndex]
        let lhsNull = lhs.rawKind == "null"
        let rhsNull = rhs.rawKind == "null"
        if lhsNull != rhsNull { return !lhsNull }
        let ordering = BaseValueOrdering.compare(lhs, rhs)
        if ordering == 0 { return pathTiebreaksBefore(other) }
        return ascending ? ordering < 0 : ordering > 0
    }

    private func pathTiebreaksBefore(_ other: BaseGridRow) -> Bool {
        if !row.filePath.utf8.elementsEqual(other.row.filePath.utf8) {
            return row.filePath.utf8.lexicographicallyPrecedes(other.row.filePath.utf8)
        }
        switch (row.taskOrdinal, other.row.taskOrdinal) {
        case (nil, nil): return false
        case (nil, .some): return true
        case (.some, nil): return false
        case (.some(let lhs), .some(let rhs)): return lhs < rhs
        }
    }
}

struct BaseGridCellSelection: Equatable {
    let rowID: String
    let columnID: String

    init(rowID: String, columnID: String) {
        self.rowID = rowID
        self.columnID = columnID
    }

    static func == (lhs: BaseGridCellSelection, rhs: BaseGridCellSelection) -> Bool {
        BaseExactIdentity.matches(lhs.rowID, rhs.rowID)
            && BaseExactIdentity.matches(lhs.columnID, rhs.columnID)
    }

    init?(
        position: AccessibleDataGrid<BaseGridRow>.CellPosition,
        result: BasesResultSet
    ) {
        guard result.columns.indices.contains(position.columnIndex),
            result.rows.contains(where: { BaseGridRow.id(for: $0) == position.rowID })
        else { return nil }
        self.init(
            rowID: position.rowID,
            columnID: result.columns[position.columnIndex].id)
    }

    func columnIndex(in result: BasesResultSet) -> Int? {
        guard result.rows.contains(where: { BaseGridRow.id(for: $0) == rowID }) else {
            return nil
        }
        return result.columns.firstIndex { BaseExactIdentity.matches($0.id, columnID) }
    }

    func position(
        in result: BasesResultSet
    ) -> AccessibleDataGrid<BaseGridRow>.CellPosition? {
        guard let columnIndex = columnIndex(in: result) else { return nil }
        return .init(rowID: rowID, columnIndex: columnIndex)
    }

    func column(in result: BasesResultSet) -> BasesColumn? {
        guard let columnIndex = columnIndex(in: result) else { return nil }
        return result.columns[columnIndex]
    }

    func reconciledEditRequest(
        _ request: AccessibleDataGrid<BaseGridRow>.EditRequest,
        in result: BasesResultSet
    ) -> AccessibleDataGrid<BaseGridRow>.EditRequest? {
        guard request.rowID == rowID, let columnIndex = columnIndex(in: result) else {
            return nil
        }
        return .init(rowID: rowID, columnIndex: columnIndex, text: request.text)
    }
}

struct BaseGridSelectionReconciliation: Equatable {
    let selectedRowID: String?
    let selectedCell: BaseGridCellSelection?
    let columnIndex: Int?
    let clearEditRequest: Bool

    init(selectedCell: BaseGridCellSelection, result: BasesResultSet?) {
        guard let result else {
            selectedRowID = nil
            self.selectedCell = nil
            columnIndex = nil
            clearEditRequest = true
            return
        }
        guard result.rows.contains(where: { BaseGridRow.id(for: $0) == selectedCell.rowID }) else {
            selectedRowID = nil
            self.selectedCell = nil
            columnIndex = nil
            clearEditRequest = true
            return
        }

        selectedRowID = selectedCell.rowID
        guard let columnIndex = result.columns.firstIndex(where: {
            BaseExactIdentity.matches($0.id, selectedCell.columnID)
        }) else {
            self.selectedCell = nil
            self.columnIndex = nil
            clearEditRequest = true
            return
        }

        self.selectedCell = selectedCell
        self.columnIndex = columnIndex
        clearEditRequest = false
    }
}

struct BaseGridSortSelection: Equatable {
    let columnID: String
    let ascending: Bool

    init(columnID: String, ascending: Bool) {
        self.columnID = columnID
        self.ascending = ascending
    }

    static func == (lhs: BaseGridSortSelection, rhs: BaseGridSortSelection) -> Bool {
        BaseExactIdentity.matches(lhs.columnID, rhs.columnID)
            && lhs.ascending == rhs.ascending
    }

    init?(sortState: DataGridSortState, result: BasesResultSet) {
        guard result.columns.indices.contains(sortState.columnIndex) else { return nil }
        self.init(
            columnID: result.columns[sortState.columnIndex].id,
            ascending: sortState.ascending)
    }

    func sortState(in result: BasesResultSet) -> DataGridSortState? {
        guard let columnIndex = result.columns.firstIndex(where: {
            BaseExactIdentity.matches($0.id, columnID)
        }) else {
            return nil
        }
        return DataGridSortState(columnIndex: columnIndex, ascending: ascending)
    }
}

/// View-local row, cell, and sort state stored by stable result identity.
/// SwiftUI keeps this value alive while a builder/dashboard/embed republishes
/// result snapshots, so reconciliation must eagerly discard disappeared IDs
/// instead of letting a former array offset resurrect later.
struct BaseGridInteractionState: Equatable {
    private(set) var selectedRowID: String? = nil
    private(set) var selectedCell: BaseGridCellSelection? = nil
    private(set) var sortSelection: BaseGridSortSelection? = nil

    mutating func setSelectedRowID(_ rowID: String?, in result: BasesResultSet) {
        guard let rowID else {
            selectedRowID = nil
            selectedCell = nil
            return
        }
        guard result.rows.contains(where: { BaseGridRow.id(for: $0) == rowID }) else {
            selectedRowID = nil
            selectedCell = nil
            return
        }
        selectedRowID = rowID
        if selectedCell?.rowID != rowID {
            selectedCell = nil
        }
    }

    mutating func setCellPosition(
        _ position: AccessibleDataGrid<BaseGridRow>.CellPosition?,
        in result: BasesResultSet
    ) {
        selectedCell = position.flatMap { BaseGridCellSelection(position: $0, result: result) }
        if let selectedCell {
            selectedRowID = selectedCell.rowID
        }
    }

    mutating func setSortState(_ sortState: DataGridSortState?, in result: BasesResultSet) {
        sortSelection = sortState.flatMap { BaseGridSortSelection(sortState: $0, result: result) }
    }

    func cellPosition(
        in result: BasesResultSet
    ) -> AccessibleDataGrid<BaseGridRow>.CellPosition? {
        selectedCell?.position(in: result)
    }

    func sortState(in result: BasesResultSet) -> DataGridSortState? {
        sortSelection?.sortState(in: result)
    }

    mutating func reconcile(with result: BasesResultSet?) {
        guard let result else {
            selectedRowID = nil
            selectedCell = nil
            sortSelection = nil
            return
        }
        let availableRows = Set(result.rows.map { BaseGridRow.id(for: $0) })
        if let selectedRowID, !availableRows.contains(selectedRowID) {
            self.selectedRowID = nil
        }
        if selectedCell?.position(in: result) == nil {
            selectedCell = nil
        }
        if sortSelection?.sortState(in: result) == nil {
            sortSelection = nil
        }
    }
}

enum BaseGridRowActionPolicy {
    static func canEditProperty(in result: BasesResultSet) -> Bool {
        result.columns.contains { BaseCellEditPolicy.propertyKey(for: $0) != nil }
    }
}

enum BaseValueOrdering {
    static func compare(_ lhs: BasesValue, _ rhs: BasesValue) -> Int {
        compare(lhs.sortKey, rhs.sortKey)
    }

    private static func compare<T: Comparable>(_ lhs: T, _ rhs: T) -> Int {
        if lhs == rhs { return 0 }
        return lhs < rhs ? -1 : 1
    }
}

enum BaseSummaryFormatter {
    static func summaryText(_ result: BasesResultSet, isQuickFiltered: Bool = false) -> String {
        let text: String
        if let summary = summaryText(summaries: result.summaries, columns: result.columns) {
            text = summary
        } else if !result.audioSummary.isEmpty {
            text = result.audioSummary
        } else {
            text = "Base table: \(result.shownCount) of \(result.totalCount) rows."
        }
        return isQuickFiltered ? "Summaries: filtered — \(text)" : text
    }

    static func summaryText(
        summaries: [BasesSummaryCell], columns: [BasesColumn]
    ) -> String? {
        guard !summaries.isEmpty else { return nil }
        var labelsByID: [String: String] = [:]
        for column in columns {
            let key = BaseExactIdentity.registryKey(prefix: "column", value: column.id)
            if labelsByID[key] == nil {
                labelsByID[key] = column.label
            }
        }
        let cells = summaries.map { summary in
            let key = BaseExactIdentity.registryKey(prefix: "column", value: summary.columnId)
            let label = labelsByID[key] ?? summary.columnId
            let value = summary.value.display.isEmpty ? "empty" : summary.value.display
            return "\(label) \(summary.summary): \(value)"
        }
        return cells.joined(separator: ", ")
    }
}

enum BaseSelectionRestorer {
    static func restoredSelection(
        previous: String?,
        current: String? = nil,
        availableIDs: [String]
    ) -> String? {
        if let current, current != previous, availableIDs.contains(current) {
            return current
        }
        if let previous, availableIDs.contains(previous) {
            return previous
        }
        return availableIDs.first
    }
}
