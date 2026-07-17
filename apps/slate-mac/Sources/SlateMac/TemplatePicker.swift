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
///   - A user-triggered refresh presents a labelled progress state
///     immediately while retaining Escape/Cancel in the sheet footer.
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
        case retry
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
                updateFocus(for: appState.templateAvailability)
            }
        }
        .onChange(of: appState.templateAvailability) { _, availability in
            updateFocus(for: availability)
        }
    }

    // MARK: - Sections

    private var header: some View {
        VStack(alignment: .leading, spacing: 4) {
            Text("Choose a template")
                .font(.headline)
                .accessibilityAddTraits(.isHeader)
            Text(
                "Create in \(appState.templateCreationDestinationDescription). "
                    + "Command-Shift-N. Escape to cancel."
            )
                .font(.caption)
                .foregroundStyle(.secondary)
        }
        .padding(.horizontal, 16)
        .padding(.top, 12)
        .padding(.bottom, 8)
    }

    @ViewBuilder
    private var content: some View {
        if appState.templateAvailability == .loading {
            loadingState
        } else if appState.templateAvailability == .failed {
            failedState
        } else if appState.availableTemplates.isEmpty {
            emptyState
        } else {
            list
        }
    }

    private var loadingState: some View {
        VStack(spacing: 12) {
            Spacer()
            ProgressView()
            Text("Loading templates…")
                .font(.callout)
                .foregroundStyle(.secondary)
            Spacer()
        }
        .frame(maxWidth: .infinity)
        .accessibilityElement(children: .combine)
        .accessibilityLabel("Loading templates.")
    }

    private var emptyState: some View {
        VStack(alignment: .leading, spacing: 8) {
            Spacer()
            Text("No templates found.")
                .font(.callout.weight(.semibold))
            Text(emptyStateDetail)
                .font(.callout)
                .foregroundStyle(.secondary)
            retryButton
            Spacer()
        }
        .padding(.horizontal, 16)
        .frame(maxWidth: .infinity, alignment: .leading)
        .accessibilityElement(children: .contain)
    }

    private var emptyStateDetail: String {
        "Create a .md file in this vault’s configured template folder to add one."
    }

    private var failedState: some View {
        VStack(alignment: .leading, spacing: 8) {
            Spacer()
            Text("Couldn’t load templates")
                .font(.callout.weight(.semibold))
            Text("Check the configured template folder, then try again.")
                .font(.callout)
                .foregroundStyle(.secondary)
            retryButton
            Spacer()
        }
        .padding(.horizontal, 16)
        .frame(maxWidth: .infinity, alignment: .leading)
        .accessibilityElement(children: .contain)
    }

    private var retryButton: some View {
        Button("Try Again") {
            appState.retryTemplatePickerLoad()
        }
        .focused($focus, equals: .retry)
        .accessibilityHint("Reloads templates for the same destination.")
    }

    private func updateFocus(for availability: TemplateAvailability) {
        switch availability {
        case .available:
            if let first = appState.availableTemplates.first {
                focus = .row(first.path)
            } else {
                focus = .cancel
            }
        case .empty, .failed:
            focus = .retry
        case .loading:
            focus = .cancel
        }
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
