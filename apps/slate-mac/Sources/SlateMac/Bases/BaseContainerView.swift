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
                document.load(session: session)
            }
            updateActiveBaseSelection()
        }
        .onDisappear {
            quickFilterTask?.cancel()
            quickFilterTask = nil
        }
        .onChange(of: appState.baseQuickFilterFocusToken) { _, _ in
            guard appState.workspace.model.activeGroup.activeTabID == tabID else { return }
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
            beginSelectedPropertyEdit()
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
            .disabled(document.views.isEmpty)
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
            Button {
                appState.basesEditViewFilters()
            } label: {
                SlateSymbol.rename.image(label: "Edit filters")
            }
            .buttonStyle(.interactiveRow())
            .help("Edit filters")
            Button {
                appState.basesRefresh()
            } label: {
                SlateSymbol.syncDiagnostics.image(label: "Refresh")
            }
            .buttonStyle(.interactiveRow())
            .help("Refresh")
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
            sortState: Binding(
                get: { document.sortState },
                set: { sort in
                    guard let session = appState.currentSession else { return }
                    document.setTransientSort(sort, session: session)
                }),
            cellNavigation: true,
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
        [
            .init("Open") { row in _ = appState.basesOpen(row: row.row) },
            .init("Copy link") { row in _ = appState.basesCopyLink(for: row.row) },
            .init("Show backlinks") { row in _ = appState.basesShowBacklinks(for: row.row) },
            .init("Edit property") { row in
                let columnIndex = selectedCell?.rowID == row.id
                    ? selectedCell?.columnIndex(in: result)
                    : nil
                beginEdit(
                    row: row.row,
                    columnIndex: columnIndex ?? firstEditableColumnIndex(result: result),
                    result: result)
            },
        ]
    }

    private func listRowActions(result: BasesResultSet) -> [BaseListRowAction] {
        [
            .init("Open") { row in _ = appState.basesOpen(row: row.row) },
            .init("Copy link") { row in _ = appState.basesCopyLink(for: row.row) },
            .init("Show backlinks") { row in _ = appState.basesShowBacklinks(for: row.row) },
            .init("Edit property") { row in
                appState.basesViewAsTable()
                beginEdit(
                    row: row.row,
                    columnIndex: firstEditableColumnIndex(result: result),
                    result: result)
            },
        ]
    }

    private func columns(from result: BasesResultSet) -> [AccessibleDataGrid<BaseGridRow>.Column] {
        result.columns.enumerated().map { columnIndex, column in
            AccessibleDataGrid<BaseGridRow>.Column(
                column.label,
                cell: { row in row.value(at: columnIndex) },
                sort: { lhs, rhs in
                    let ascending = document.sortState?.ascending ?? true
                    return ascending
                        ? lhs.sortsBefore(rhs, at: columnIndex, ascending: true)
                        : rhs.sortsBefore(lhs, at: columnIndex, ascending: false)
                },
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
        appState.updateActiveBaseSelection(
            path: document.selectionKey,
            rowID: selectedRow,
            columnIndex: document.result.flatMap { selectedCell?.columnIndex(in: $0) },
            result: document.result)
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
            let column = selectedCell?.column(in: result),
            let index = result.columns.firstIndex(of: column)
        {
            columnIndex = index
        } else if let column = appState.activeBaseSelectedColumn,
            let index = result.columns.firstIndex(of: column)
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
                guard let session = appState.currentSession else { return }
                document.selectView(index: index, session: session)
            })
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
            .frame(maxWidth: .infinity, maxHeight: .infinity)
    }
}

struct BaseGridRow: Identifiable {
    let row: BasesRow
    let ordinal: Int

    var id: String {
        Self.id(for: row)
    }

    static func id(for row: BasesRow) -> String {
        let task = row.taskOrdinal.map { "#\($0)" } ?? ""
        return "\(row.filePath)\(task)"
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
        if row.filePath != other.row.filePath {
            return row.filePath < other.row.filePath
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
        return result.columns.firstIndex { $0.id == columnID }
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
            $0.id == selectedCell.columnID
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

private enum BaseValueOrdering {
    static func compare(_ lhs: BasesValue, _ rhs: BasesValue) -> Int {
        let lhsNull = lhs.rawKind == "null"
        let rhsNull = rhs.rawKind == "null"
        if lhsNull || rhsNull {
            if lhsNull == rhsNull { return 0 }
            return lhsNull ? 1 : -1
        }
        let lhsRank = rank(lhs.rawKind)
        let rhsRank = rank(rhs.rawKind)
        if lhsRank != rhsRank { return lhsRank < rhsRank ? -1 : 1 }
        switch (lhs.rawKind, rhs.rawKind) {
        case ("bool", "bool"):
            return compare(lhs.boolValue == true ? 1 : 0, rhs.boolValue == true ? 1 : 0)
        case ("number", "number"):
            return compare(lhs.number ?? 0, rhs.number ?? 0)
        case ("date", "date"):
            return compare(lhs.dateEpochMs ?? 0, rhs.dateEpochMs ?? 0)
        case ("text", "text"):
            return compare(
                (lhs.text ?? lhs.display).lowercased(),
                (rhs.text ?? rhs.display).lowercased())
        default:
            return compare(lhs.display, rhs.display)
        }
    }

    private static func rank(_ kind: String) -> Int {
        switch kind {
        case "bool": 0
        case "number": 1
        case "date": 2
        case "duration": 3
        case "text": 4
        case "link": 5
        case "file": 6
        case "regex": 7
        case "list": 8
        default: 9
        }
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
        let labelsByID = Dictionary(uniqueKeysWithValues: columns.map { ($0.id, $0.label) })
        let cells = summaries.map { summary in
            let label = labelsByID[summary.columnId] ?? summary.columnId
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
