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

    @State private var text: String = ""
    @State private var chosenGroup: String = ""
    @State private var direction: CanvasConnectionDirectionChoice = .toTarget

    var body: some View {
        VStack(alignment: .leading, spacing: Tokens.Spacing.md) {
            switch prompt {
            case .newGroup:
                Text("New Group").font(Tokens.Typography.body.weight(.semibold))
                TextField("Group label", text: $text)
                    .textFieldStyle(.roundedBorder)
                    .accessibilityLabel("Group label")
                commitRow("Create") {
                    appState.canvasNewGroup(label: text)
                }
            case .renameGroup(let current):
                Text("Rename Group").font(Tokens.Typography.body.weight(.semibold))
                TextField("Group label", text: $text)
                    .textFieldStyle(.roundedBorder)
                    .accessibilityLabel("Group label")
                    .onAppear { text = current }
                commitRow("Rename") {
                    appState.canvasRenameGroup(to: text)
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
                TextField("Connection label (optional)", text: $text)
                    .textFieldStyle(.roundedBorder)
                    .accessibilityLabel("Connection label, optional")
                commitRow("Connect") {
                    if let origin = appState.activeCanvasDocument?.selection.selected {
                        appState.canvasConnect(from: origin, to: targetId, label: text)
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
            case .editConnection(let edgeId, let currentLabel):
                Text("Edit Connection")
                    .font(Tokens.Typography.body.weight(.semibold))
                TextField("Label", text: $text)
                    .textFieldStyle(.roundedBorder)
                    .accessibilityLabel("Connection label")
                    .onAppear { text = currentLabel }
                Picker("Direction", selection: $direction) {
                    ForEach(CanvasConnectionDirectionChoice.allCases, id: \.self) { choice in
                        Text(choice.title).tag(choice)
                    }
                }
                .pickerStyle(.radioGroup)
                .accessibilityLabel("Direction")
                commitRow("Apply") {
                    appState.canvasEditConnection(
                        edgeId: edgeId, label: text, direction: direction)
                }
            case .marksList:
                marksListBody
            case .groupMarked:
                Text("Group Marked Cards")
                    .font(Tokens.Typography.body.weight(.semibold))
                TextField("Group label", text: $text)
                    .textFieldStyle(.roundedBorder)
                    .accessibilityLabel("Group label")
                commitRow("Group") {
                    appState.canvasGroupMarked(label: text)
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
                TextField("URL", text: $text, prompt: Text("https://…"))
                    .textFieldStyle(.roundedBorder)
                    .textContentType(.URL)
                    .accessibilityLabel("Link URL")
                commitRow("Add") {
                    appState.canvasAddLinkCard(url: text)
                }
            case .locate(let nodeId, let title, let files):
                filePickBody(
                    heading: "Locate File for \"\(title)\"",
                    prompt: "Pick the vault file this card should point at.",
                    files: files
                ) { appState.canvasLocate(nodeId: nodeId, path: $0) }
            case .setColor:
                Text("Set Color").font(Tokens.Typography.body.weight(.semibold))
                // Color is never color-alone: named buttons (1.4.1).
                HStack(spacing: Tokens.Spacing.xs) {
                    ForEach(
                        Array(["red", "orange", "yellow", "green", "cyan", "purple"].enumerated()),
                        id: \.offset
                    ) { index, name in
                        Button(name.capitalized) {
                            appState.canvasSetColor(preset: index + 1)
                            appState.canvasPrompt = nil
                        }
                    }
                }
                HStack {
                    Button("Clear Color") {
                        appState.canvasSetColor(preset: nil)
                        appState.canvasPrompt = nil
                    }
                    Spacer()
                    Button("Cancel") { appState.canvasPrompt = nil }
                        .keyboardShortcut(.cancelAction)
                }
            }
        }
        .padding(Tokens.Spacing.lg)
        .frame(minWidth: 340)
        .accessibilityElement(children: .contain)
        .accessibilityLabel("Canvas: \(title)")
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
            text.isEmpty
            ? files
            : files.filter { $0.localizedCaseInsensitiveContains(text) }
        Text(heading)
            .font(Tokens.Typography.body.weight(.semibold))
        Text(prompt)
            .font(Tokens.Typography.caption)
            .foregroundStyle(Tokens.ColorRole.textSecondary)
        TextField("Filter", text: $text)
            .textFieldStyle(.roundedBorder)
            .accessibilityLabel("Filter files")
        List(filtered.prefix(200), id: \.self) { path in
            Button {
                appState.canvasPrompt = nil
                commit(path)
            } label: {
                Text(path)
                    .font(Tokens.Typography.body)
                    .frame(maxWidth: .infinity, alignment: .leading)
            }
            .buttonStyle(.plain)
            .accessibilityLabel(path)
        }
        .frame(minHeight: 180, maxHeight: 280)
        .accessibilityLabel("\(heading): \(filtered.count) file\(filtered.count == 1 ? "" : "s")")
        HStack {
            Text("\(filtered.count) of \(files.count) files")
                .font(Tokens.Typography.caption)
                .foregroundStyle(Tokens.ColorRole.textSecondary)
            Spacer()
            Button("Cancel") { appState.canvasPrompt = nil }
                .keyboardShortcut(.cancelAction)
        }
    }

    @ViewBuilder
    private func commitRow(_ verb: String, action: @escaping () -> Void) -> some View {
        HStack {
            Spacer()
            Button("Cancel") { appState.canvasPrompt = nil }
                .keyboardShortcut(.cancelAction)
            Button(verb) {
                action()
                appState.canvasPrompt = nil
            }
            .keyboardShortcut(.defaultAction)
        }
    }
}
