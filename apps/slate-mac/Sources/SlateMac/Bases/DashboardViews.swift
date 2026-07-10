// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI

struct DashboardContainerView: View {
    @EnvironmentObject private var appState: AppState
    @ObservedObject var document: DashboardDocument

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: Tokens.Spacing.md) {
                Text(document.name)
                    .font(Tokens.Typography.title)
                    .foregroundStyle(Tokens.ColorRole.textPrimary)
                    .accessibilityAddTraits(.isHeader)
                    .accessibilityHeading(.h1)
                content
            }
            .padding(Tokens.Spacing.md)
        }
        .background(Tokens.ColorRole.surface)
        .onAppear {
            if document.dashboard == nil, let session = appState.currentSession {
                document.load(session: session)
            }
        }
    }

    @ViewBuilder
    private var content: some View {
        switch document.state {
        case .loading:
            placeholder("Loading dashboard...")
        case .failed(let message):
            placeholder(message)
        case .ready:
            if document.sections.isEmpty {
                placeholder("No dashboard sections. Add a saved query section to show results.")
            } else {
                let expectedSections = document.editableSectionsSnapshot
                ForEach(document.sections) { section in
                    DashboardSectionView(
                        section: section,
                        replacementChoices: appState.baseQueries.savedQueries,
                        onRemove: {
                            appState.removeMissingDashboardSection(
                                dashboardID: document.id,
                                index: section.index,
                                expectedSections: expectedSections)
                        },
                        onReplace: { replacementID in
                            appState.replaceMissingDashboardSection(
                                dashboardID: document.id,
                                index: section.index,
                                expectedSections: expectedSections,
                                replacementSavedQueryID: replacementID)
                        })
                }
            }
        }
    }

    private func placeholder(_ text: String) -> some View {
        Text(text)
            .font(Tokens.Typography.callout)
            .foregroundStyle(Tokens.ColorRole.textSecondary)
            .frame(maxWidth: .infinity, alignment: .leading)
            .accessibilityLabel(text)
    }
}

private struct DashboardSectionView: View {
    @ObservedObject var section: DashboardSectionDocument
    let replacementChoices: [SavedQuerySummary]
    let onRemove: () -> Void
    let onReplace: (String) -> Void
    @State private var showingReplacementPicker = false

    var body: some View {
        VStack(alignment: .leading, spacing: Tokens.Spacing.sm) {
            Text(section.title)
                .font(Tokens.Typography.sectionHeader)
                .foregroundStyle(Tokens.ColorRole.textPrimary)
                .accessibilityAddTraits(.isHeader)
                .accessibilityHeading(.h2)
            content
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }

    @ViewBuilder
    private var content: some View {
        switch section.state {
        case .loading:
            placeholder("Loading section...")
        case .missing:
            missingSection
        case .failed(let message):
            placeholder(message)
        case .ready:
            if let result = section.result, !result.columns.isEmpty {
                BaseReadOnlyResultView(result: result, accessibilityLabel: "\(section.title) grid")
                    .frame(minHeight: 160)
            } else {
                placeholder("No results in this section.")
            }
        }
    }

    private var missingSection: some View {
        VStack(alignment: .leading, spacing: Tokens.Spacing.sm) {
            placeholder("Missing saved query. Remove this section or pick a replacement.")
            HStack(spacing: Tokens.Spacing.sm) {
                Button("Remove section") { onRemove() }
                    .buttonStyle(.bordered)
                Button("Pick replacement") { showingReplacementPicker = true }
                    .buttonStyle(.borderedProminent)
            }
        }
        .accessibilityElement(children: .contain)
        .accessibilityAction(named: Text("Remove section")) { onRemove() }
        .accessibilityAction(named: Text("Pick replacement")) {
            showingReplacementPicker = true
        }
        .sheet(isPresented: $showingReplacementPicker) {
            VStack(alignment: .leading, spacing: Tokens.Spacing.md) {
                Text("Pick replacement")
                    .font(Tokens.Typography.sectionHeader)
                    .accessibilityAddTraits(.isHeader)
                if replacementChoices.isEmpty {
                    Text("No saved queries are available.")
                        .font(Tokens.Typography.callout)
                        .foregroundStyle(Tokens.ColorRole.textSecondary)
                } else {
                    ScrollView {
                        VStack(alignment: .leading, spacing: Tokens.Spacing.xs) {
                            ForEach(replacementChoices, id: \.id) { choice in
                                Button(choice.name) {
                                    showingReplacementPicker = false
                                    onReplace(choice.id)
                                }
                                .buttonStyle(.plain)
                                .frame(maxWidth: .infinity, alignment: .leading)
                                .accessibilityLabel("Replace with \(choice.name)")
                            }
                        }
                    }
                    .frame(minHeight: 180)
                }
                HStack {
                    Spacer()
                    Button("Cancel") { showingReplacementPicker = false }
                        .keyboardShortcut(.cancelAction)
                }
            }
            .padding(Tokens.Spacing.md)
            .frame(minWidth: 360, minHeight: 260)
        }
    }

    private func placeholder(_ text: String) -> some View {
        Text(text)
            .font(Tokens.Typography.caption)
            .foregroundStyle(Tokens.ColorRole.textSecondary)
            .frame(maxWidth: .infinity, alignment: .leading)
            .accessibilityLabel(text)
    }
}

struct BasesDockPanel: View {
    @EnvironmentObject private var appState: AppState

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            header
            Divider()
            content
        }
        .onAppear {
            appState.scheduleBasesDockFollowActiveRefresh()
        }
        .onChange(of: appState.selectedFilePath) {
            appState.scheduleBasesDockFollowActiveRefresh()
        }
    }

    private var header: some View {
        HStack(spacing: Tokens.Spacing.sm) {
            Text("Base dock")
                .font(Tokens.Typography.sectionHeader)
                .foregroundStyle(Tokens.ColorRole.textPrimary)
                .accessibilityAddTraits(.isHeader)
            Spacer()
            Text(appState.basesDock.thisPath ?? "No active note")
                .font(Tokens.Typography.caption)
                .foregroundStyle(Tokens.ColorRole.textSecondary)
                .multilineTextAlignment(.trailing)
        }
        .padding(.horizontal, Tokens.Spacing.md)
        .padding(.vertical, Tokens.Spacing.sm)
    }

    @ViewBuilder
    private var content: some View {
        if let target = appState.basesDock.target {
            switch target {
            case .base, .savedQuery:
                if let doc = appState.basesDockDocument {
                    dockedBase(doc)
                } else {
                    placeholder("Loading \(target.displayName)...")
                }
            case .dashboard:
                if let doc = appState.basesDockDashboardDocument {
                    DashboardContainerView(document: doc)
                } else {
                    placeholder("Loading \(target.displayName)...")
                }
            }
        } else {
            placeholder("Dock a base, saved query, or dashboard to follow the active note.")
        }
    }

    @ViewBuilder
    private func dockedBase(_ doc: BaseDocument) -> some View {
        switch doc.state {
        case .loading:
            placeholder("Loading \(doc.displayName)...")
        case .failed(let message), .degraded(let message):
            VStack(alignment: .leading, spacing: Tokens.Spacing.sm) {
                Text(doc.displayName)
                    .font(Tokens.Typography.sectionHeader)
                    .accessibilityAddTraits(.isHeader)
                placeholder(message)
            }
            .padding(Tokens.Spacing.md)
        case .ready:
            if let result = doc.result, !result.columns.isEmpty {
                VStack(alignment: .leading, spacing: Tokens.Spacing.sm) {
                    Text(doc.displayName)
                        .font(Tokens.Typography.sectionHeader)
                        .foregroundStyle(Tokens.ColorRole.textPrimary)
                        .accessibilityAddTraits(.isHeader)
                    BaseReadOnlyResultView(result: result, accessibilityLabel: "\(doc.displayName) grid")
                }
                .padding(Tokens.Spacing.md)
            } else {
                placeholder("No base results.")
            }
        }
    }

    private func placeholder(_ text: String) -> some View {
        Text(text)
            .font(Tokens.Typography.callout)
            .foregroundStyle(Tokens.ColorRole.textSecondary)
            .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .center)
            .padding(Tokens.Spacing.md)
            .accessibilityLabel(text)
    }
}

struct BaseReadOnlyResultView: View {
    let result: BasesResultSet
    let accessibilityLabel: String
    @State private var selectedRow: String?
    @State private var selectedCell: AccessibleDataGrid<BaseGridRow>.CellPosition?
    @State private var sortState: DataGridSortState?

    var body: some View {
        AccessibleDataGrid(
            columns: columns,
            rows: rows,
            summary: BaseSummaryFormatter.summaryText(result),
            accessibilityLabel: accessibilityLabel,
            groups: groups,
            selection: $selectedRow,
            cellSelection: $selectedCell,
            sortState: $sortState,
            cellNavigation: true,
            onActivate: { _ in },
            rowAccessibilityDescription: { $0.row.audioDescription },
            rowActions: [])
    }

    private var columns: [AccessibleDataGrid<BaseGridRow>.Column] {
        result.columns.enumerated().map { columnIndex, column in
            AccessibleDataGrid<BaseGridRow>.Column(
                column.label,
                cell: { row in row.value(at: columnIndex) },
                sort: { lhs, rhs in
                    let ascending = sortState?.ascending ?? true
                    return ascending
                        ? lhs.sortsBefore(rhs, at: columnIndex, ascending: true)
                        : rhs.sortsBefore(lhs, at: columnIndex, ascending: false)
                })
        }
    }

    private var rows: [BaseGridRow] {
        result.rows.enumerated().map { rowIndex, row in
            BaseGridRow(row: row, ordinal: rowIndex)
        }
    }

    private var groups: [AccessibleDataGrid<BaseGridRow>.Group] {
        result.groups.map {
            .init(
                label: $0.label,
                rowStart: Int($0.rowStart),
                rowCount: Int($0.rowCount),
                summary: BaseSummaryFormatter.summaryText(
                    summaries: $0.summaries, columns: result.columns))
        }
    }
}
