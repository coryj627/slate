// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI

struct DashboardEditorDraft: Equatable {
    var dashboardID: String?
    var name: String
    var sections: [DashboardEditorSectionDraft]
    var selectedSavedQueryID: String

    init(
        dashboardID: String? = nil,
        name: String = "New dashboard",
        sections: [DashboardEditorSectionDraft] = [],
        selectedSavedQueryID: String = ""
    ) {
        self.dashboardID = dashboardID
        self.name = name
        self.sections = sections
        self.selectedSavedQueryID = selectedSavedQueryID
    }

    init(savedQueries: [SavedQuerySummary]) {
        self.init(selectedSavedQueryID: savedQueries.first?.id ?? "")
    }

    init(dashboard: Dashboard, savedQueries: [SavedQuerySummary]) {
        let savedByID = Dictionary(uniqueKeysWithValues: savedQueries.map { ($0.id, $0.name) })
        let rows = dashboard.sections.map { status in
            DashboardEditorSectionDraft(
                savedQueryID: status.savedQueryId,
                savedQueryName: status.savedQueryName ?? savedByID[status.savedQueryId],
                headingOverride: status.headingOverride ?? "",
                viewOverride: status.viewOverride ?? "",
                missing: status.missing)
        }
        self.init(
            dashboardID: dashboard.id,
            name: dashboard.name,
            sections: rows,
            selectedSavedQueryID: savedQueries.first?.id ?? "")
    }

    var isEditing: Bool { dashboardID != nil }

    var title: String {
        isEditing ? "Edit dashboard" : "New dashboard"
    }

    var canSave: Bool {
        !name.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
    }

    var dashboardSections: [DashboardSection] {
        sections.map { section in
            DashboardSection(
                savedQueryId: section.savedQueryID,
                headingOverride: section.normalizedHeadingOverride,
                viewOverride: section.normalizedViewOverride)
        }
    }

    mutating func addSelectedSavedQuery(from savedQueries: [SavedQuerySummary]) {
        guard let summary = savedQueries.first(where: { $0.id == selectedSavedQueryID }) else {
            return
        }
        sections.append(
            DashboardEditorSectionDraft(
                savedQueryID: summary.id,
                savedQueryName: summary.name,
                headingOverride: "",
                viewOverride: "",
                missing: false))
    }

    mutating func moveSection(from source: Int, to destination: Int) {
        guard sections.indices.contains(source), sections.indices.contains(destination), source != destination else {
            return
        }
        let row = sections.remove(at: source)
        sections.insert(row, at: destination)
    }
}

struct DashboardEditorSectionDraft: Identifiable, Equatable {
    let id: UUID
    var savedQueryID: String
    var savedQueryName: String?
    var headingOverride: String
    var viewOverride: String
    var missing: Bool

    init(
        id: UUID = UUID(),
        savedQueryID: String,
        savedQueryName: String?,
        headingOverride: String,
        viewOverride: String,
        missing: Bool
    ) {
        self.id = id
        self.savedQueryID = savedQueryID
        self.savedQueryName = savedQueryName
        self.headingOverride = headingOverride
        self.viewOverride = viewOverride
        self.missing = missing
    }

    var displayName: String {
        savedQueryName ?? savedQueryID
    }

    var normalizedHeadingOverride: String? {
        nilIfBlank(headingOverride)
    }

    var normalizedViewOverride: String? {
        nilIfBlank(viewOverride)
    }

    private func nilIfBlank(_ value: String) -> String? {
        let trimmed = value.trimmingCharacters(in: .whitespacesAndNewlines)
        return trimmed.isEmpty ? nil : trimmed
    }
}

struct DashboardEditorSheet: View {
    @Binding var draft: DashboardEditorDraft
    let savedQueries: [SavedQuerySummary]
    let onCancel: () -> Void
    let onSave: (DashboardEditorDraft) -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            header
            Divider()
            ScrollView {
                VStack(alignment: .leading, spacing: Tokens.Spacing.md) {
                    nameSection
                    addSection
                    sectionsList
                }
                .padding(Tokens.Spacing.md)
            }
            Divider()
            footer
        }
        .frame(minWidth: 520, idealWidth: 620, minHeight: 480, idealHeight: 560)
        .background(Color(nsColor: .controlBackgroundColor))
    }

    private var header: some View {
        HStack(spacing: Tokens.Spacing.sm) {
            SlateSymbol.base.decorative
                .foregroundStyle(Tokens.ColorRole.textSecondary)
            Text(draft.title)
                .font(Tokens.Typography.sectionHeader)
                .foregroundStyle(Tokens.ColorRole.textPrimary)
                .accessibilityAddTraits(.isHeader)
            Spacer()
            Button("Cancel", action: onCancel)
                .keyboardShortcut(.cancelAction)
        }
        .padding(.horizontal, Tokens.Spacing.md)
        .padding(.vertical, Tokens.Spacing.sm)
    }

    private var nameSection: some View {
        VStack(alignment: .leading, spacing: Tokens.Spacing.xs) {
            Text("Dashboard name")
                .font(Tokens.Typography.caption.weight(.semibold))
                .foregroundStyle(Tokens.ColorRole.textSecondary)
                .accessibilityAddTraits(.isHeader)
            TextField("Dashboard name", text: $draft.name)
                .textFieldStyle(.roundedBorder)
                .accessibilityLabel("Dashboard name")
        }
    }

    private var addSection: some View {
        VStack(alignment: .leading, spacing: Tokens.Spacing.xs) {
            Text("Add section")
                .font(Tokens.Typography.caption.weight(.semibold))
                .foregroundStyle(Tokens.ColorRole.textSecondary)
                .accessibilityAddTraits(.isHeader)
            HStack(spacing: Tokens.Spacing.sm) {
                Picker("Saved query", selection: $draft.selectedSavedQueryID) {
                    if savedQueries.isEmpty {
                        Text("No saved queries").tag("")
                    } else {
                        ForEach(savedQueries, id: \.id) { query in
                            Text(query.name).tag(query.id)
                        }
                    }
                }
                .labelsHidden()
                .frame(maxWidth: .infinity)
                .accessibilityLabel("Saved query")
                Button {
                    draft.addSelectedSavedQuery(from: savedQueries)
                } label: {
                    SlateSymbol.addProperty.label("Add section")
                }
                .disabled(savedQueries.isEmpty || draft.selectedSavedQueryID.isEmpty)
            }
        }
    }

    @ViewBuilder
    private var sectionsList: some View {
        VStack(alignment: .leading, spacing: Tokens.Spacing.xs) {
            Text("Sections")
                .font(Tokens.Typography.caption.weight(.semibold))
                .foregroundStyle(Tokens.ColorRole.textSecondary)
                .accessibilityAddTraits(.isHeader)
            if draft.sections.isEmpty {
                Text("No dashboard sections")
                    .font(Tokens.Typography.callout)
                    .foregroundStyle(Tokens.ColorRole.textSecondary)
                    .frame(maxWidth: .infinity, alignment: .leading)
            } else {
                VStack(alignment: .leading, spacing: Tokens.Spacing.sm) {
                    ForEach(Array(draft.sections.enumerated()), id: \.element.id) { index, _ in
                        sectionRow($draft.sections[index], index: index)
                    }
                }
            }
        }
    }

    private func sectionRow(_ section: Binding<DashboardEditorSectionDraft>, index: Int) -> some View {
        VStack(alignment: .leading, spacing: Tokens.Spacing.sm) {
            HStack(spacing: Tokens.Spacing.sm) {
                Text("Section \(index + 1)")
                    .font(Tokens.Typography.body.weight(.semibold))
                    .foregroundStyle(Tokens.ColorRole.textPrimary)
                    .accessibilityAddTraits(.isHeader)
                if section.wrappedValue.missing {
                    Text("Missing saved query")
                        .font(Tokens.Typography.caption)
                        .foregroundStyle(Tokens.ColorRole.textSecondary)
                }
                Spacer()
                Button {
                    draft.moveSection(from: index, to: index - 1)
                } label: {
                    SlateSymbol.moveUp.image(label: "Move section up")
                        .frame(width: 28, height: 28)
                }
                .disabled(index == 0)
                Button {
                    draft.moveSection(from: index, to: index + 1)
                } label: {
                    SlateSymbol.moveDown.image(label: "Move section down")
                        .frame(width: 28, height: 28)
                }
                .disabled(index == draft.sections.count - 1)
                Button {
                    draft.sections.removeAll { $0.id == section.wrappedValue.id }
                } label: {
                    SlateSymbol.trash.image(label: "Remove section")
                        .frame(width: 28, height: 28)
                }
            }
            Picker("Saved query", selection: savedQuerySelection(for: section)) {
                if section.wrappedValue.missing {
                    Text("Missing: \(section.wrappedValue.savedQueryID)").tag(section.wrappedValue.savedQueryID)
                }
                ForEach(savedQueries, id: \.id) { query in
                    Text(query.name).tag(query.id)
                }
            }
            .accessibilityLabel("Section saved query")
            TextField("Section title override", text: section.headingOverride)
                .textFieldStyle(.roundedBorder)
                .accessibilityLabel("Section title override")
            TextField("View override", text: section.viewOverride)
                .textFieldStyle(.roundedBorder)
                .accessibilityLabel("View override")
        }
        .padding(Tokens.Spacing.sm)
        .background(Tokens.ColorRole.surface)
        .clipShape(RoundedRectangle(cornerRadius: 8))
        .accessibilityElement(children: .contain)
        .accessibilityLabel("Dashboard section \(index + 1)")
        .accessibilityValue(section.wrappedValue.displayName)
    }

    private func savedQuerySelection(
        for section: Binding<DashboardEditorSectionDraft>
    ) -> Binding<String> {
        Binding(
            get: { section.wrappedValue.savedQueryID },
            set: { newID in
                section.wrappedValue.savedQueryID = newID
                section.wrappedValue.savedQueryName = savedQueries.first(where: { $0.id == newID })?.name
                section.wrappedValue.missing = false
            })
    }

    private var footer: some View {
        HStack {
            Text("\(draft.sections.count) \(draft.sections.count == 1 ? "section" : "sections")")
                .font(Tokens.Typography.caption)
                .foregroundStyle(Tokens.ColorRole.textSecondary)
            Spacer()
            Button("Cancel", action: onCancel)
            Button("Save") { onSave(draft) }
                .keyboardShortcut(.defaultAction)
                .disabled(!draft.canSave)
        }
        .padding(Tokens.Spacing.md)
    }
}
