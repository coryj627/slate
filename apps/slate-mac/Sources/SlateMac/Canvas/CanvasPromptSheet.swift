// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI

/// Input sheets for prompt-driven canvas verbs (#368): New Group…,
/// Rename Group…, Move into Group…, Set Color…. Sheets are the M6
/// visible-control path — fully operable by Switch Control and Voice
/// Control; Esc cancels, Return commits.
struct CanvasPromptSheet: View {
    @EnvironmentObject var appState: AppState
    let prompt: CanvasPrompt

    @State private var chosenGroup: String = ""
    @State private var direction: CanvasConnectionDirectionChoice = .toTarget
    @State private var pendingDiscard = false
    @AccessibilityFocusState private var draftDialogFocusReturn: DraftFocusTarget?

    private enum DraftFocusTarget: Hashable {
        case dismiss
    }

    var body: some View {
        VStack(alignment: .leading, spacing: Tokens.Spacing.md) {
            switch prompt {
            case .newGroup:
                Text("New Group").font(Tokens.Typography.body.weight(.semibold))
                TextField("Group label", text: $appState.canvasPromptDraft)
                    .textFieldStyle(.roundedBorder)
                    .textContentType(.none)
                    .accessibilityLabel("Group label")
                commitRow("Create") {
                    appState.canvasNewGroup(label: appState.canvasPromptDraft)
                }
            case .renameGroup:
                Text("Rename Group").font(Tokens.Typography.body.weight(.semibold))
                TextField("Group label", text: $appState.canvasPromptDraft)
                    .textFieldStyle(.roundedBorder)
                    .textContentType(.none)
                    .accessibilityLabel("Group label")
                commitRow("Rename") {
                    appState.canvasRenameGroup(to: appState.canvasPromptDraft)
                }
            case .moveIntoGroup(let groups):
                Text("Move into Group").font(Tokens.Typography.body.weight(.semibold))
                Picker("Group", selection: $chosenGroup) {
                    ForEach(groups, id: \.id) { group in
                        Text(group.title).tag(group.id)
                    }
                }
                .pickerStyle(.radioGroup)
                .accessibilityLabel("Destination group")
                .onAppear { chosenGroup = groups.first?.id ?? "" }
                commitRow("Move") {
                    if !chosenGroup.isEmpty {
                        appState.canvasMoveIntoGroup(groupId: chosenGroup)
                    }
                }
            case .connectLabel(let targetId, let targetTitle):
                Text("Connect to \"\(targetTitle)\"")
                    .font(Tokens.Typography.body.weight(.semibold))
                TextField(
                    "Connection label (optional)",
                    text: $appState.canvasPromptDraft)
                    .textFieldStyle(.roundedBorder)
                    .textContentType(.none)
                    .accessibilityLabel("Connection label, optional")
                commitRow("Connect") {
                    if let origin = appState.activeCanvasDocument?.selection.selected {
                        appState.canvasConnect(
                            from: origin,
                            to: targetId,
                            label: appState.canvasPromptDraft)
                    }
                }
            case .pickConnection(let choices, let toDelete):
                Text(toDelete ? "Delete Connection" : "Edit Connection")
                    .font(Tokens.Typography.body.weight(.semibold))
                Picker("Connection", selection: $chosenGroup) {
                    ForEach(choices, id: \.edgeId) { choice in
                        Text(choice.label).tag(choice.edgeId)
                    }
                }
                .pickerStyle(.radioGroup)
                .accessibilityLabel("Connection")
                .onAppear { chosenGroup = choices.first?.edgeId ?? "" }
                commitRow(toDelete ? "Delete" : "Edit") {
                    guard !chosenGroup.isEmpty else { return }
                    if toDelete {
                        appState.canvasDeleteConnection(edgeId: chosenGroup)
                    } else {
                        appState.canvasOpenConnectionEditor(edgeId: chosenGroup)
                    }
                }
            case .editConnection(let edgeId, _):
                Text("Edit Connection")
                    .font(Tokens.Typography.body.weight(.semibold))
                TextField("Label", text: $appState.canvasPromptDraft)
                    .textFieldStyle(.roundedBorder)
                    .textContentType(.none)
                    .accessibilityLabel("Connection label")
                Picker("Direction", selection: $direction) {
                    ForEach(CanvasConnectionDirectionChoice.allCases, id: \.self) { choice in
                        Text(choice.title).tag(choice)
                    }
                }
                .pickerStyle(.radioGroup)
                .accessibilityLabel("Direction")
                commitRow("Apply") {
                    appState.canvasEditConnection(
                        edgeId: edgeId,
                        label: appState.canvasPromptDraft,
                        direction: direction)
                }
            case .marksList:
                marksListBody
            case .groupMarked:
                Text("Group Marked Cards")
                    .font(Tokens.Typography.body.weight(.semibold))
                TextField("Group label", text: $appState.canvasPromptDraft)
                    .textFieldStyle(.roundedBorder)
                    .textContentType(.none)
                    .accessibilityLabel("Group label")
                commitRow("Group") {
                    appState.canvasGroupMarked(label: appState.canvasPromptDraft)
                }
            case .addNote(let files):
                filePickBody(
                    heading: "Add Note to Canvas",
                    prompt: "Pick a note — a file card is placed next to your selection.",
                    files: files
                ) { appState.canvasAddFileCard(path: $0) }
            case .addMedia(let files):
                filePickBody(
                    heading: "Add Media",
                    prompt: "Pick a media file — a file card is placed next to your selection.",
                    files: files
                ) { appState.canvasAddFileCard(path: $0) }
            case .addLink:
                Text("Add Link Card")
                    .font(Tokens.Typography.body.weight(.semibold))
                TextField(
                    "URL",
                    text: $appState.canvasPromptDraft,
                    prompt: Text("https://…"))
                    .textFieldStyle(.roundedBorder)
                    .textContentType(.URL)
                    .accessibilityLabel("Link URL")
                commitRow("Add") {
                    appState.canvasAddLinkCard(url: appState.canvasPromptDraft)
                }
            case .locate(let nodeId, let title, let files):
                filePickBody(
                    heading: "Locate File for \"\(title)\"",
                    prompt: "Pick the vault file this card should point at.",
                    files: files
                ) { appState.canvasLocate(nodeId: nodeId, path: $0) }
            case .connectedDirection:
                Text("Create Connected Card")
                    .font(Tokens.Typography.body.weight(.semibold))
                Text("The new card is placed on the side you pick, already connected.")
                    .font(Tokens.Typography.caption)
                    .foregroundStyle(Tokens.ColorRole.textSecondary)
                ForEach(
                    [
                        ("Below", CanvasPlaceDirection.below),
                        ("Right", .rightOf),
                        ("Above", .above),
                        ("Left", .leftOf),
                    ], id: \.0
                ) { title, direction in
                    Button(title) {
                        commitMutation {
                            appState.canvasCreateConnectedCard(direction: direction)
                        }
                    }
                    .disabled(mutationDisabledReason != nil)
                    .accessibilityHint(
                        mutationDisabledReason ?? "Create the connected card.")
                    .help(mutationDisabledReason ?? "Create the connected card.")
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .accessibilityLabel("Place \(title.lowercased())")
                }
                dismissButton
            case .convertToNote(let nodeId, _):
                let disabledReason = appState.structuralMutationDisabledReason
                Text("Convert Card to Note")
                    .font(Tokens.Typography.body.weight(.semibold))
                Text("Creates a vault note with the card's text; the card then points at it.")
                    .font(Tokens.Typography.caption)
                    .foregroundStyle(Tokens.ColorRole.textSecondary)
                TextField("Note path", text: $appState.canvasPromptDraft)
                    .textFieldStyle(.roundedBorder)
                    .textContentType(.none)
                    .accessibilityLabel("Note path, ends in .md")
                HStack {
                    Spacer()
                    dismissButton
                    Button("Convert") {
                        if appState.canvasConvertToNote(
                            nodeId: nodeId,
                            path: appState.canvasPromptDraft) != nil
                        {
                            appState.dismissCanvasPrompt()
                        }
                    }
                    .keyboardShortcut(.defaultAction)
                    .disabled(disabledReason != nil || mutationDisabledReason != nil)
                    .accessibilityHint(
                        disabledReason ?? mutationDisabledReason
                            ?? "Create the note and point this card at it.")
                    .help(
                        disabledReason ?? mutationDisabledReason
                            ?? "Create the note and point this card at it.")
                }
                if let disabledReason {
                    Text(disabledReason)
                        .font(Tokens.Typography.caption)
                        .foregroundStyle(Tokens.ColorRole.textSecondary)
                        .accessibilityLabel(disabledReason)
                }
            case .setColor:
                Text("Set Color").font(Tokens.Typography.body.weight(.semibold))
                // Color is never color-alone: named buttons (1.4.1).
                HStack(spacing: Tokens.Spacing.xs) {
                    ForEach(
                        Array(["red", "orange", "yellow", "green", "cyan", "purple"].enumerated()),
                        id: \.offset
                    ) { index, name in
                        Button(name.capitalized) {
                            commitMutation {
                                appState.canvasSetColor(preset: index + 1)
                            }
                        }
                        .disabled(mutationDisabledReason != nil)
                        .accessibilityHint(
                            mutationDisabledReason ?? "Set the selected card’s color.")
                        .help(mutationDisabledReason ?? "Set the selected card’s color.")
                    }
                }
                HStack {
                    Button("Clear Color") {
                        commitMutation { appState.canvasSetColor(preset: nil) }
                    }
                    .disabled(mutationDisabledReason != nil)
                    .accessibilityHint(
                        mutationDisabledReason ?? "Clear the selected card’s color.")
                    .help(mutationDisabledReason ?? "Clear the selected card’s color.")
                    Spacer()
                    dismissButton
                }
            }
            if let mutationDisabledReason {
                Text(mutationDisabledReason)
                    .font(Tokens.Typography.caption)
                    .foregroundStyle(Tokens.ColorRole.textSecondary)
                    .textSelection(.enabled)
                    .accessibilityLabel(mutationDisabledReason)
            }
            if let document = appState.activeCanvasRecoveryDocument,
                let recoveryLabel = appState.canvasRecoveryActionLabel(for: document)
            {
                let recoveryDisabledReason = appState.structuralMutationDisabledReason
                Button(recoveryLabel) {
                    _ = appState.retryCanvasRecovery(for: document)
                }
                .disabled(recoveryDisabledReason != nil)
                .accessibilityHint(
                    recoveryDisabledReason
                        ?? appState.canvasRecoveryActionHint(for: document)
                        ?? "Try to restore Canvas editing.")
                .help(
                    recoveryDisabledReason
                        ?? appState.canvasRecoveryActionHint(for: document)
                        ?? "Try to restore Canvas editing")
            }
        }
        .padding(Tokens.Spacing.lg)
        .frame(minWidth: 340)
        .accessibilityElement(children: .contain)
        .accessibilityLabel("Canvas: \(title)")
        .interactiveDismissDisabled(mutationDisabledReason != nil)
        .confirmationDialog(
            "Discard this Canvas draft?",
            isPresented: $pendingDiscard,
            titleVisibility: .visible
        ) {
            Button("Cancel", role: .cancel) {
                draftDialogFocusReturn = .dismiss
            }
            Button("Discard Draft", role: .destructive) {
                appState.dismissCanvasPrompt()
            }
        } message: {
            Text("Closing discards the input in this sheet. Copy it first if you need to keep it.")
        }
        .onChange(of: pendingDiscard) { wasPresented, isPresented in
            if wasPresented, !isPresented, appState.canvasPrompt != nil {
                draftDialogFocusReturn = .dismiss
            }
        }
    }

    private var title: String {
        switch prompt {
        case .newGroup: return "New Group"
        case .renameGroup: return "Rename Group"
        case .moveIntoGroup: return "Move into Group"
        case .setColor: return "Set Color"
        case .connectLabel: return "Connect"
        case .pickConnection: return "Choose Connection"
        case .editConnection: return "Edit Connection"
        case .marksList: return "Marked Cards"
        case .groupMarked: return "Group Marked Cards"
        case .addNote: return "Add Note to Canvas"
        case .addMedia: return "Add Media"
        case .addLink: return "Add Link Card"
        case .locate: return "Locate File"
        case .connectedDirection: return "Create Connected Card"
        case .convertToNote: return "Convert Card to Note"
        }
    }

    /// #524: the focusable marks list — the pull-based counterpart to
    /// mark announcements (t0 §3). Each row jumps or unmarks; Clear
    /// All empties the set.
    @ViewBuilder
    private var marksListBody: some View {
        let doc = appState.activeCanvasDocument
        let markedRows: [CanvasOutlineRow] =
            doc.map { d in d.outline.filter { d.selection.marked.contains($0.nodeId) } } ?? []
        Text("Marked Cards (\(markedRows.count))")
            .font(Tokens.Typography.body.weight(.semibold))
        List(markedRows, id: \.nodeId) { row in
            HStack(spacing: Tokens.Spacing.sm) {
                Text(row.title)
                    .font(Tokens.Typography.body)
                Spacer()
                Button("Jump") {
                    if let d = doc {
                        appState.canvasSelect(nodeId: row.nodeId, in: d)
                    }
                    appState.canvasPrompt = nil
                }
                .accessibilityLabel("Jump to \(row.title)")
                Button("Unmark") {
                    doc?.selection.marked.remove(row.nodeId)
                    if doc?.selection.marked.isEmpty == true {
                        appState.canvasPrompt = nil
                    }
                }
                .accessibilityLabel("Unmark \(row.title)")
            }
            .accessibilityElement(children: .contain)
            .accessibilityLabel("\(row.title), marked")
        }
        .frame(minHeight: 160, maxHeight: 260)
        .accessibilityLabel("Marked cards")
        HStack {
            Button("Clear All Marks") {
                appState.canvasClearMarks()
                appState.canvasPrompt = nil
            }
            Spacer()
            Button("Close") { appState.canvasPrompt = nil }
                .keyboardShortcut(.cancelAction)
        }
    }

    /// #368 R5: the shared quick-open-style vault-file picker — filter
    /// field + list, one activation commits. Used by Add Note, Add
    /// Media, and Locate….
    @ViewBuilder
    private func filePickBody(
        heading: String, prompt: String, files: [String],
        commit: @escaping (String) -> Void
    ) -> some View {
        let filtered =
            appState.canvasPromptDraft.isEmpty
            ? files
            : files.filter {
                $0.localizedCaseInsensitiveContains(appState.canvasPromptDraft)
            }
        Text(heading)
            .font(Tokens.Typography.body.weight(.semibold))
        Text(prompt)
            .font(Tokens.Typography.caption)
            .foregroundStyle(Tokens.ColorRole.textSecondary)
        TextField("Filter", text: $appState.canvasPromptDraft)
            .textFieldStyle(.roundedBorder)
            .textContentType(.none)
            .accessibilityLabel("Filter files")
        List(filtered.prefix(200), id: \.self) { path in
            Button {
                commitMutation { commit(path) }
            } label: {
                Text(path)
                    .font(Tokens.Typography.body)
                    .frame(maxWidth: .infinity, alignment: .leading)
            }
            .buttonStyle(.plain)
            .disabled(mutationDisabledReason != nil)
            .accessibilityLabel(path)
            .accessibilityHint(
                mutationDisabledReason ?? "Choose this file.")
            .help(mutationDisabledReason ?? "Choose this file.")
        }
        .frame(minHeight: 180, maxHeight: 280)
        .accessibilityLabel("\(heading): \(filtered.count) file\(filtered.count == 1 ? "" : "s")")
        HStack {
            Text("\(filtered.count) of \(files.count) files")
                .font(Tokens.Typography.caption)
                .foregroundStyle(Tokens.ColorRole.textSecondary)
            Spacer()
            dismissButton
        }
    }

    @ViewBuilder
    private func commitRow(_ verb: String, action: @escaping () -> Void) -> some View {
        HStack {
            Spacer()
            dismissButton
            Button(verb) {
                commitMutation(action)
            }
            .keyboardShortcut(.defaultAction)
            .disabled(mutationDisabledReason != nil)
            .accessibilityHint(
                mutationDisabledReason ?? "Commit this Canvas change.")
            .help(mutationDisabledReason ?? "Commit this Canvas change.")
        }
    }

    private var mutationDisabledReason: String? {
        if case .marksList = prompt { return nil }
        return appState.activeCanvasMutationDisabledReason
    }

    private func commitMutation(_ action: () -> Void) {
        guard appState.commitCanvasPromptMutation(action) else { return }
        appState.dismissCanvasPrompt()
    }

    private var dismissButton: some View {
        Button(mutationDisabledReason == nil ? "Cancel" : "Close…") {
            requestDismiss()
        }
        .keyboardShortcut(.cancelAction)
        .accessibilityFocused($draftDialogFocusReturn, equals: .dismiss)
    }

    private func requestDismiss() {
        if mutationDisabledReason != nil {
            pendingDiscard = true
        } else {
            appState.dismissCanvasPrompt()
        }
    }
}
