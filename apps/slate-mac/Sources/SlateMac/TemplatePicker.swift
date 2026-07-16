// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI

/// Cmd+Shift+N picker (Milestone H). Lists every template under the
/// vault's templates folder (default `Templates/`); activating one
/// hands the flow to `AppState.selectTemplate(...)`, which either
/// routes to the prompt sheet or skips straight to the new-note
/// name sheet for prompt-less templates.
///
/// VoiceOver story:
///   - On open, the picker title is announced as a heading; the
///     polite live region carries the count summary fired by
///     `openTemplatePicker` ("Template picker opened. N templates
///     available.").
///   - Each row's accessibility label is `"<name>. <description>."`
///     when the template has a description, otherwise just the name.
///   - Empty state ("No templates found. Create one in
///     <vault>/Templates/.") is its own static-text element so the
///     screen reader hears it on row-list focus instead of silence.
///
/// Keyboard:
///   - Esc dismisses (binding on the Cancel button, same window-
///     scoped behavior `SearchOverlay` relies on).
///   - Return activates the focused row's button action.
///   - Tab/Shift-Tab + arrow keys traverse the row list — handled by
///     SwiftUI's default focus traversal once each row is a Button.
struct TemplatePicker: View {
    @EnvironmentObject private var appState: AppState

    @FocusState private var focus: FocusTarget?

    private enum FocusTarget: Hashable {
        case row(String)  // template path
        case cancel
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            header
            Divider()
            if let reason = appState.structuralMutationDisabledReason {
                Text(reason)
                    .font(.caption)
                    .foregroundStyle(Tokens.ColorRole.textSecondary)
                    .padding(.horizontal, 16)
                    .padding(.vertical, 8)
                    .accessibilityLabel(reason)
                Divider()
            }
            content
            Divider()
            footer
        }
        .frame(minWidth: 420, idealWidth: 520, minHeight: 280, idealHeight: 360)
        .onAppear {
            // Defer focus until the .focused bindings have wired up.
            DispatchQueue.main.async {
                if let first = appState.availableTemplates.first {
                    focus = .row(first.path)
                } else {
                    focus = .cancel
                }
            }
        }
    }

    // MARK: - Sections

    private var header: some View {
        VStack(alignment: .leading, spacing: 4) {
            Text("Choose a template")
                .font(.headline)
                .accessibilityAddTraits(.isHeader)
            Text("Command-Shift-N. Escape to cancel.")
                .font(.caption)
                .foregroundStyle(.secondary)
        }
        .padding(.horizontal, 16)
        .padding(.top, 12)
        .padding(.bottom, 8)
    }

    @ViewBuilder
    private var content: some View {
        if appState.availableTemplates.isEmpty {
            emptyState
        } else {
            list
        }
    }

    private var emptyState: some View {
        VStack(alignment: .leading, spacing: 8) {
            Spacer()
            Text("No templates found.")
                .font(.callout.weight(.semibold))
            Text(emptyStateDetail)
                .font(.callout)
                .foregroundStyle(.secondary)
            Spacer()
        }
        .padding(.horizontal, 16)
        .frame(maxWidth: .infinity, alignment: .leading)
        .accessibilityElement(children: .combine)
        .accessibilityLabel("No templates found. \(emptyStateDetail)")
    }

    private var emptyStateDetail: String {
        let vaultLabel = appState.currentVaultURL?.lastPathComponent ?? "this vault"
        return "Create a .md file in \(vaultLabel)/Templates/ to add one."
    }

    private var list: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 2) {
                ForEach(appState.availableTemplates, id: \.path) { summary in
                    row(for: summary)
                }
            }
            .padding(.horizontal, 8)
            .padding(.vertical, 4)
        }
    }

    private func row(for summary: TemplateSummary) -> some View {
        let disabledReason = appState.structuralMutationDisabledReason
        return Button {
            appState.selectTemplate(summary)
        } label: {
            VStack(alignment: .leading, spacing: 2) {
                Text(summary.name)
                    .font(.callout.weight(.semibold))
                    .foregroundStyle(.primary)
                if let description = summary.description, !description.isEmpty {
                    Text(description)
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }
            }
            .frame(maxWidth: .infinity, alignment: .leading)
            .padding(.vertical, 6)
            .padding(.horizontal, 8)
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .focused($focus, equals: .row(summary.path))
        .accessibilityLabel(rowAccessibilityLabel(for: summary))
        .accessibilityHint(
            disabledReason ?? "Activate to use this template for a new note.")
        .help(disabledReason ?? "Activate to use this template for a new note.")
        .disabled(disabledReason != nil)
    }

    private func rowAccessibilityLabel(for summary: TemplateSummary) -> String {
        if let description = summary.description, !description.isEmpty {
            return "\(summary.name). \(description)."
        }
        return summary.name
    }

    private var footer: some View {
        HStack {
            Spacer()
            Button("Cancel") {
                appState.cancelTemplateFlow()
            }
            .keyboardShortcut(.escape, modifiers: [])
            .focused($focus, equals: .cancel)
            .accessibilityHint("Closes the template picker without creating a note.")
        }
        .padding(.horizontal, 16)
        .padding(.vertical, 10)
    }
}
