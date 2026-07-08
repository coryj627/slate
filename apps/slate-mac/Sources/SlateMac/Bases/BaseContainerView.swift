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
    @State private var selectedCell: AccessibleDataGrid<BaseGridRow>.CellPosition?

    var body: some View {
        VStack(spacing: 0) {
            header
            Divider()
            banners
            content
        }
        .background(Tokens.ColorRole.surface)
        .onAppear {
            if document.handle == nil, let session = appState.currentSession {
                document.load(session: session)
            }
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
            TextField("Quick filter", text: .constant(""))
                .textFieldStyle(.roundedBorder)
                .frame(width: 160)
                .disabled(true)
                .accessibilityLabel("Quick filter")
                .accessibilityHint("Quick filtering is reserved for a later Bases phase.")
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
            if let result = document.result, !result.columns.isEmpty {
                resultRenderer(result)
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
            accessibilityLabel: "Base table",
            groups: groups(from: result),
            selection: Binding(get: { selectedRow }, set: { selectedRow = $0 }),
            cellSelection: Binding(
                get: { selectedCell },
                set: {
                    selectedCell = $0
                    if let columnIndex = $0?.columnIndex {
                        document.focusColumn(columnIndex)
                    }
                }),
            sortState: Binding(
                get: { document.sortState },
                set: { document.sortState = $0 }),
            cellNavigation: true,
            onActivate: { appState.openFile($0.row.filePath, target: .currentTab) },
            rowActions: [
                .init("Open") { row in
                    appState.openFile(row.row.filePath, target: .currentTab)
                }
            ])
    }

    private func resultList(_ result: BasesResultSet) -> some View {
        let projection = BaseListProjection(
            result: result,
            options: BaseListOptions(slateStateJson: activeView?.slateStateJson))
        return BaseListView(
            projection: projection,
            selection: Binding(get: { selectedRow }, set: { selectedRow = $0 }),
            onActivate: { appState.openFile($0.filePath, target: .currentTab) },
            rowActions: [
                .init("Open") { row in
                    appState.openFile(row.filePath, target: .currentTab)
                }
            ])
    }

    private func columns(from result: BasesResultSet) -> [AccessibleDataGrid<BaseGridRow>.Column] {
        result.columns.enumerated().map { columnIndex, column in
            AccessibleDataGrid<BaseGridRow>.Column(column.label) { row in
                row.value(at: columnIndex)
            } sort: { lhs, rhs in
                lhs.value(at: columnIndex).localizedCaseInsensitiveCompare(rhs.value(at: columnIndex))
                    == .orderedAscending
            }
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
        BaseSummaryFormatter.summaryText(result)
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
        return "\(result.shownCount) of \(result.totalCount)"
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
        let task = row.taskOrdinal.map { "#\($0)" } ?? ""
        return "\(row.filePath)\(task):\(ordinal)"
    }

    func value(at columnIndex: Int) -> String {
        guard row.values.indices.contains(columnIndex) else { return "" }
        return row.values[columnIndex].display
    }
}

enum BaseSummaryFormatter {
    static func summaryText(_ result: BasesResultSet) -> String {
        if let text = summaryText(summaries: result.summaries, columns: result.columns) {
            return text
        }
        if !result.audioSummary.isEmpty { return result.audioSummary }
        return "Base table: \(result.shownCount) of \(result.totalCount) rows."
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
