// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI

struct BaseQueriesPanel: View {
    @EnvironmentObject private var appState: AppState
    @State private var renameTarget: SavedQuerySummary?
    @State private var renameDraft = ""
    @State private var deleteTarget: SavedQuerySummary?
    @State private var dashboardDraft: DashboardEditorDraft?
    @AccessibilityFocusState private var actionFocusReturn: ActionFocusTarget?

    enum ActionFocusTarget: Hashable {
        case refresh
        case savedQueryActions(String)
    }

    private var renamePresented: Binding<Bool> {
        Binding(
            get: { renameTarget != nil },
            set: { shown in
                if !shown {
                    renameTarget = nil
                    renameDraft = ""
                }
            })
    }

    private var deletePresented: Binding<Bool> {
        Binding(
            get: { deleteTarget != nil },
            set: { shown in
                if !shown { deleteTarget = nil }
            })
    }

    private var dashboardEditorPresented: Binding<Bool> {
        Binding(
            get: { dashboardDraft != nil },
            set: { shown in
                if !shown { dashboardDraft = nil }
            })
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            header
            Divider()
            ScrollView {
                LazyVStack(alignment: .leading, spacing: Tokens.Spacing.sm) {
                    savedQueriesSection
                    baseFilesSection
                    dashboardsSection
                }
                .padding(Tokens.Spacing.md)
            }
        }
        .onAppear { appState.refreshBaseQueries() }
        .alert("Rename Saved Query", isPresented: renamePresented) {
            Text("Saved query name")
            TextField("Saved query name", text: $renameDraft)
                .accessibilityLabel("Saved query name")
            Button("Rename") {
                let targetID = renameTarget?.id
                if let renameTarget {
                    appState.renameSavedQuery(id: renameTarget.id, name: renameDraft)
                }
                renameTarget = nil
                renameDraft = ""
                if let targetID {
                    actionFocusReturn = .savedQueryActions(targetID)
                }
            }
            Button("Cancel", role: .cancel) {
                let targetID = renameTarget?.id
                renameTarget = nil
                renameDraft = ""
                if let targetID {
                    actionFocusReturn = .savedQueryActions(targetID)
                }
            }
        } message: {
            Text(renameTarget?.name ?? "")
        }
        .alert("Delete saved query?", isPresented: deletePresented) {
            Button("Delete", role: .destructive) {
                if let deleteTarget {
                    appState.deleteSavedQuery(id: deleteTarget.id)
                }
                deleteTarget = nil
                actionFocusReturn = .refresh
            }
            Button("Cancel", role: .cancel) {
                let targetID = deleteTarget?.id
                deleteTarget = nil
                if let targetID {
                    actionFocusReturn = .savedQueryActions(targetID)
                }
            }
        } message: {
            Text(deleteTarget?.name ?? "")
        }
        .sheet(isPresented: dashboardEditorPresented) {
            DashboardEditorSheet(
                draft: Binding(
                    get: {
                        dashboardDraft
                            ?? DashboardEditorDraft(savedQueries: appState.baseQueries.savedQueries)
                    },
                    set: { dashboardDraft = $0 }),
                savedQueries: appState.baseQueries.savedQueries,
                onCancel: { dashboardDraft = nil },
                onSave: saveDashboard)
                .environmentObject(appState)
        }
    }

    private var header: some View {
        HStack(spacing: Tokens.Spacing.sm) {
            Text("Queries")
                .font(Tokens.Typography.sectionHeader)
                .foregroundStyle(Tokens.ColorRole.textPrimary)
                .accessibilityAddTraits(.isHeader)
            Spacer()
            Button {
                dashboardDraft = DashboardEditorDraft(savedQueries: appState.baseQueries.savedQueries)
            } label: {
                SlateSymbol.addProperty.label("New dashboard")
            }
            .help("New dashboard")
            Button {
                appState.refreshBaseQueries()
            } label: {
                SlateSymbol.refresh.image(label: "Refresh queries")
                    .frame(width: 28, height: 28)
                    .contentShape(Rectangle())
            }
            .buttonStyle(.interactiveRow())
            .help("Refresh queries")
            .accessibilityFocused($actionFocusReturn, equals: .refresh)
        }
        .padding(.horizontal, Tokens.Spacing.md)
        .padding(.vertical, Tokens.Spacing.sm)
        .accessibilityElement(children: .combine)
        .accessibilityValue(appState.baseQueriesAccessibilityValue)
    }

    @ViewBuilder
    private var savedQueriesSection: some View {
        let exportDisabledReason = appState.structuralMutationDisabledReason
        BaseQueriesSectionHeader(title: "Saved queries")
        if let exportDisabledReason {
            Text(exportDisabledReason)
                .font(Tokens.Typography.caption)
                .foregroundStyle(Tokens.ColorRole.textSecondary)
                .accessibilityLabel(exportDisabledReason)
        }
        if appState.baseQueries.savedQueries.isEmpty {
            emptyRow("No saved queries")
        } else {
            ForEach(appState.orderedSavedQuerySummaries, id: \.id) { summary in
                savedQueryRow(summary)
            }
        }
    }

    @ViewBuilder
    private var baseFilesSection: some View {
        BaseQueriesSectionHeader(title: "Base files")
        if appState.baseQueries.baseFiles.isEmpty {
            emptyRow("No base files")
        } else {
            ForEach(BaseFileListEntry.make(appState.baseQueries.baseFiles)) { entry in
                baseFileRow(entry.summary)
            }
        }
    }

    @ViewBuilder
    private var dashboardsSection: some View {
        BaseQueriesSectionHeader(title: "Dashboards")
        if appState.baseQueries.dashboards.isEmpty {
            emptyRow("No dashboards")
        } else {
            ForEach(appState.baseQueries.dashboards, id: \.id) { dashboard in
                dashboardRow(dashboard)
            }
        }
    }

    private func emptyRow(_ title: String) -> some View {
        Text(title)
            .font(Tokens.Typography.caption)
            .foregroundStyle(Tokens.ColorRole.textSecondary)
            .frame(maxWidth: .infinity, alignment: .leading)
            .padding(.vertical, Tokens.Spacing.xs)
    }

    private func savedQueryRow(_ summary: SavedQuerySummary) -> some View {
        let isPinned = appState.baseQueries.pinnedSavedQueryIDs.contains(summary.id)
        let exportDisabledReason = appState.structuralMutationDisabledReason
        return HStack(spacing: Tokens.Spacing.sm) {
            Button {
                appState.openSavedQuery(summary)
            } label: {
                VStack(alignment: .leading, spacing: 2) {
                    Text(summary.name)
                        .font(Tokens.Typography.body)
                        .foregroundStyle(Tokens.ColorRole.textPrimary)
                    if let description = summary.description, !description.isEmpty {
                        Text(description)
                            .font(Tokens.Typography.caption)
                            .foregroundStyle(Tokens.ColorRole.textSecondary)
                    }
                }
                .frame(maxWidth: .infinity, alignment: .leading)
                .contentShape(Rectangle())
            }
            .buttonStyle(.interactiveRow())
            .help(savedQueryHelp(summary))
            .accessibilityLabel("\(summary.name), saved query")
            .accessibilityHint(savedQueryHelp(summary))

            Button {
                appState.toggleSavedQueryPin(id: summary.id)
            } label: {
                SlateSymbol.pin.image(label: isPinned ? "Unpin saved query" : "Pin saved query")
                    .frame(width: 28, height: 28)
                    .contentShape(Rectangle())
            }
            .buttonStyle(.interactiveRow())
            .help(isPinned ? "Unpin saved query" : "Pin saved query")
            .accessibilityAddTraits(isPinned ? [.isSelected] : [])

            Menu {
                Button("Run") { appState.openSavedQuery(summary) }
                Button("Edit in Builder") { appState.editSavedQueryInBuilder(id: summary.id) }
                Button("Rename...") {
                    renameTarget = summary
                    renameDraft = summary.name
                }
                Button("Export as .base...") {
                    appState.exportSavedQueryUsingSavePanel(id: summary.id)
                }
                .disabled(exportDisabledReason != nil)
                .accessibilityHint(
                    exportDisabledReason
                        ?? "Export this saved query to a .base file in the vault.")
                .help(
                    exportDisabledReason
                        ?? "Export this saved query to a .base file in the vault")
                Button("Dock to Sidebar") {
                    appState.dockSavedQueryToSidebar(id: summary.id)
                }
                Button("Delete...", role: .destructive) {
                    deleteTarget = summary
                }
            } label: {
                SlateSymbol.moreActions.image(label: "Saved query actions")
                    .frame(width: 28, height: 28)
                    .contentShape(Rectangle())
            }
            .menuStyle(.borderlessButton)
            .menuIndicator(.hidden)
            .help("Saved query actions")
            .accessibilityFocused($actionFocusReturn, equals: .savedQueryActions(summary.id))
        }
        .accessibilityElement(children: .contain)
        .accessibilityValue(isPinned ? "Pinned" : "")
    }

    private func savedQueryHelp(_ summary: SavedQuerySummary) -> String {
        guard let description = summary.description?.trimmingCharacters(in: .whitespacesAndNewlines),
            !description.isEmpty
        else { return "Open saved query" }
        return description
    }

    private func baseFileRow(_ file: BaseFileSummary) -> some View {
        HStack(spacing: Tokens.Spacing.sm) {
            Button {
                appState.openBaseFile(file.path)
            } label: {
                baseFileLabel(file)
            }
            .buttonStyle(.interactiveRow())
            .accessibilityLabel("\(file.name), base file")

            Menu {
                Button("Open") { appState.openBaseFile(file.path) }
                Button("Dock to Sidebar") {
                    appState.dockBaseFileToSidebar(path: file.path, name: file.name)
                }
            } label: {
                SlateSymbol.moreActions.image(label: "Base file actions")
                    .frame(width: 28, height: 28)
                    .contentShape(Rectangle())
            }
            .menuStyle(.borderlessButton)
            .menuIndicator(.hidden)
            .help("Base file actions")
        }
    }

    private func baseFileLabel(_ file: BaseFileSummary) -> some View {
        HStack(spacing: Tokens.Spacing.sm) {
            SlateSymbol.base.decorative
                .font(Tokens.Typography.caption)
                .foregroundStyle(Tokens.ColorRole.textSecondary)
            VStack(alignment: .leading, spacing: 2) {
                Text(file.name)
                    .font(Tokens.Typography.body)
                    .foregroundStyle(Tokens.ColorRole.textPrimary)
                Text(file.path)
                    .font(Tokens.Typography.caption)
                    .foregroundStyle(Tokens.ColorRole.textSecondary)
            }
            Spacer(minLength: 0)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .contentShape(Rectangle())
    }

    private func dashboardRow(_ dashboard: DashboardSummary) -> some View {
        HStack(spacing: Tokens.Spacing.sm) {
            Button {
                appState.openDashboard(id: dashboard.id, name: dashboard.name)
            } label: {
                VStack(alignment: .leading, spacing: 2) {
                    Text(dashboard.name)
                        .font(Tokens.Typography.body)
                        .foregroundStyle(Tokens.ColorRole.textPrimary)
                    Text("\(CountCopy.counted(dashboard.sectionCount, "section", "sections"))")
                        .font(Tokens.Typography.caption)
                        .foregroundStyle(Tokens.ColorRole.textSecondary)
                }
                .frame(maxWidth: .infinity, alignment: .leading)
                .contentShape(Rectangle())
            }
            .buttonStyle(.interactiveRow())
            .accessibilityLabel("\(dashboard.name), dashboard")

            Menu {
                Button("Open") { appState.openDashboard(id: dashboard.id, name: dashboard.name) }
                Button("Edit...") {
                    editDashboard(dashboard)
                }
                Button("Dock to Sidebar") {
                    appState.dockDashboardToSidebar(id: dashboard.id)
                }
                Button("Delete...", role: .destructive) {
                    appState.deleteDashboard(id: dashboard.id)
                }
            } label: {
                SlateSymbol.moreActions.image(label: "Dashboard actions")
                    .frame(width: 28, height: 28)
                    .contentShape(Rectangle())
            }
            .menuStyle(.borderlessButton)
            .menuIndicator(.hidden)
            .help("Dashboard actions")
        }
    }

    private func editDashboard(_ summary: DashboardSummary) {
        guard let dashboard = appState.dashboardForEditing(id: summary.id) else { return }
        dashboardDraft = DashboardEditorDraft(
            dashboard: dashboard,
            savedQueries: appState.baseQueries.savedQueries)
    }

    private func saveDashboard(_ draft: DashboardEditorDraft) {
        let didSave: Bool
        if let id = draft.dashboardID {
            didSave = appState.updateDashboard(
                id: id, name: draft.name, sections: draft.dashboardSections)
        } else {
            didSave = appState.saveDashboard(
                name: draft.name, sections: draft.dashboardSections) != nil
        }
        if didSave {
            dashboardDraft = nil
        }
    }
}

struct BaseFileListEntry: Identifiable {
    let summary: BaseFileSummary
    let id: String

    static func make(_ summaries: [BaseFileSummary]) -> [Self] {
        summaries.map { summary in
            Self(
                summary: summary,
                id: BaseExactIdentity.key(
                    prefix: "base-file-row", components: [summary.path]))
        }
    }
}

private struct BaseQueriesSectionHeader: View {
    let title: String

    var body: some View {
        Text(title)
            .font(Tokens.Typography.caption.weight(.semibold))
            .foregroundStyle(Tokens.ColorRole.textSecondary)
            .textCase(.uppercase)
            .frame(maxWidth: .infinity, alignment: .leading)
            .accessibilityAddTraits(.isHeader)
    }
}
