// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI

/// Second + third sheets of the create-from-template flow
/// (Milestone H). Bound to `AppState.pendingTemplateFlow` —
/// `.needsPrompts` renders the prompt sheet; `.needsName` renders
/// the new-note name sheet. Both can also be reached from each
/// other (Submit on prompts transitions to name) so this file
/// keeps them together rather than splitting into two files.
///
/// Accessibility:
///   - Each prompt's `TextField` has `accessibilityLabel` set to
///     the prompt's `label` text (not the slug `key`) — VoiceOver
///     announces what the template author wrote, in the
///     declaration order `extract_template_metadata` preserved.
///   - The new-note name sheet announces its validation error
///     inline rather than as an alert.
///   - Esc cancels at either step and resets the flow back to
///     `.idle` via `AppState.cancelTemplateFlow`.
struct TemplatePromptSheet: View {
    @EnvironmentObject private var appState: AppState

    var body: some View {
        Group {
            switch appState.pendingTemplateFlow {
            case .needsPrompts(let template, let prompts):
                PromptStep(template: template, prompts: prompts)
                    .environmentObject(appState)
            case .needsName(let template, _):
                NameStep(template: template)
                    .environmentObject(appState)
            case .idle:
                // Should never present in this state — the binding
                // wrapper that drives the sheet only goes true for
                // the two non-idle cases. Render an empty view as a
                // belt-and-braces fallback so SwiftUI doesn't crash
                // if the state flips during dismissal animation.
                EmptyView()
            }
        }
        .frame(minWidth: 420, idealWidth: 520)
    }
}

// MARK: - Prompt step

private struct PromptStep: View {
    let template: TemplateSummary
    let prompts: [TemplatePrompt]

    @EnvironmentObject private var appState: AppState
    @State private var values: [String: String] = [:]
    @FocusState private var focusedKey: String?

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            header
            Divider()
            ScrollView {
                VStack(alignment: .leading, spacing: 12) {
                    ForEach(prompts, id: \.key) { prompt in
                        promptField(prompt)
                    }
                }
                .padding(16)
            }
            Divider()
            footer
        }
        .onAppear {
            // Seed every key so SwiftUI's TextField binding never
            // hits a nil → String coercion in the dictionary lookup.
            for prompt in prompts where values[prompt.key] == nil {
                values[prompt.key] = ""
            }
            DispatchQueue.main.async {
                focusedKey = prompts.first?.key
            }
        }
    }

    private var header: some View {
        VStack(alignment: .leading, spacing: 4) {
            Text("Fill in template details")
                .font(.headline)
                .accessibilityAddTraits(.isHeader)
            Text(
                "\(template.name) · Create in "
                    + appState.templateCreationDestinationDescription
            )
                .font(.callout)
                .foregroundStyle(.secondary)
        }
        .padding(.horizontal, 16)
        .padding(.top, 12)
        .padding(.bottom, 8)
    }

    private func promptField(_ prompt: TemplatePrompt) -> some View {
        VStack(alignment: .leading, spacing: 4) {
            Text(prompt.label)
                .font(.callout)
                .accessibilityHidden(true)  // the field's label carries the same text
            TextField(
                prompt.label,
                text: Binding(
                    get: { values[prompt.key] ?? "" },
                    set: { values[prompt.key] = $0 }
                )
            )
            .textFieldStyle(.roundedBorder)
            .focused($focusedKey, equals: prompt.key)
            .accessibilityLabel(prompt.label)
        }
    }

    private var footer: some View {
        let disabledReason = appState.structuralMutationDisabledReason
        return HStack {
            Button("Cancel") {
                appState.cancelTemplateFlow()
            }
            .keyboardShortcut(.cancelAction)
            .accessibilityHint("Cancels the create-from-template flow.")
            Spacer()
            // `.defaultAction`, not the former ⌘↩: bare Return in a
            // macOS dialog activates the default button even from a
            // text field (AddPropertySheet is the in-app precedent),
            // and `.defaultAction` is what confers the visually-
            // primary (blue) treatment — with ⌘↩ the sheet rendered
            // no default button at all and Return was a dead key.
            Button("Next") {
                appState.submitTemplatePrompts(values)
            }
            .keyboardShortcut(.defaultAction)
            .accessibilityHint(
                disabledReason ?? "Continue to choose the new note's name. Return.")
            .help(
                disabledReason ?? "Continue to choose the new note's name. Return.")
            .disabled(disabledReason != nil)
        }
        .padding(.horizontal, 16)
        .padding(.vertical, 10)
    }
}

// MARK: - Name step

private struct NameStep: View {
    let template: TemplateSummary

    @EnvironmentObject private var appState: AppState
    @State private var noteName: String = ""
    @State private var didSeed: Bool = false
    @FocusState private var nameFocused: Bool

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            header
            Divider()
            VStack(alignment: .leading, spacing: 6) {
                Text("New note name")
                    .font(.callout)
                    .accessibilityHidden(true)
                Text(
                    "Name relative to "
                        + appState.templateCreationDestinationDescription + "."
                )
                    .font(.caption)
                    .foregroundStyle(.secondary)
                    .accessibilityHidden(true)
                TextField("Note name", text: $noteName)
                    .textFieldStyle(.roundedBorder)
                    .focused($nameFocused)
                    .accessibilityLabel(
                        "New note name. Name relative to "
                            + appState.templateCreationDestinationDescription)
                    .accessibilityHint(
                        ".md extension added automatically."
                    )
                    .onSubmit {
                        submit()
                    }
                if let error = appState.templateNoteNameError {
                    Text(error)
                        .font(.caption)
                        .foregroundStyle(Tokens.ColorRole.destructiveText)
                        .accessibilityLabel("Validation error: \(error)")
                }
                if let reason = appState.structuralMutationDisabledReason {
                    Text(reason)
                        .font(.caption)
                        .foregroundStyle(Tokens.ColorRole.textSecondary)
                        .accessibilityLabel(reason)
                }
            }
            .padding(16)
            Divider()
            footer
        }
        .onAppear {
            if !didSeed {
                noteName = appState.templateRetryNoteName
                    ?? appState.defaultNewNoteName(for: template)
                didSeed = true
            }
            DispatchQueue.main.async { nameFocused = true }
        }
    }

    private var header: some View {
        VStack(alignment: .leading, spacing: 4) {
            Text("Name the new note")
                .font(.headline)
                .accessibilityAddTraits(.isHeader)
            Text(
                "from \(template.name) · Create in "
                    + appState.templateCreationDestinationDescription
            )
                .font(.callout)
                .foregroundStyle(.secondary)
        }
        .padding(.horizontal, 16)
        .padding(.top, 12)
        .padding(.bottom, 8)
    }

    private var footer: some View {
        let disabledReason = appState.structuralMutationDisabledReason
        return HStack {
            Button("Cancel") {
                appState.cancelTemplateFlow()
            }
            .keyboardShortcut(.cancelAction)
            .accessibilityHint("Cancels the create-from-template flow. No file is written.")
            Spacer()
            // `.defaultAction` (not bare `.return`): same key, but
            // also confers the visually-primary default-button
            // treatment the prompt step's footer now has — one
            // consistent dialog idiom across both steps.
            Button("Create") {
                submit()
            }
            .keyboardShortcut(.defaultAction)
            .accessibilityHint(
                disabledReason ?? "Render the template and open the new note. Return.")
            .help(
                disabledReason ?? "Render the template and open the new note. Return.")
            .disabled(disabledReason != nil)
        }
        .padding(.horizontal, 16)
        .padding(.vertical, 10)
    }

    private func submit() {
        appState.submitTemplateNoteName(noteName)
    }
}
