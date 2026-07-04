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
