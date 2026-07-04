// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Combine
import SwiftUI

/// The text-card editor (#368, interview decision 7): the REAL post-U3
/// editing component (`NoteEditorView` — native NSTextView VoiceOver
/// behavior, allowsUndo, Markdown highlighting) hosted in a sheet.
///
/// t0 M8 embedded-editor carve-out: **Escape commits** (the sheet's
/// cancel action routes to the same commit as Done and ⌘S), never the
/// M2 discard semantics — leaving an editor must never silently drop
/// typed text. One `canvas_apply` per commit; "No changes." when the
/// buffer is untouched. Focus returns to the canvas on dismiss (the
/// container re-focuses its content).
struct CanvasCardEditorSheet: View {
    @EnvironmentObject var appState: AppState
    let request: CanvasCardEditorRequest

    @State private var text: String

    init(request: CanvasCardEditorRequest) {
        self.request = request
        _text = State(initialValue: request.initialText)
    }

    var body: some View {
        VStack(alignment: .leading, spacing: Tokens.Spacing.sm) {
            Text("Edit \"\(request.title)\"")
                .font(Tokens.Typography.body.weight(.semibold))
            NoteEditorView(
                text: $text,
                headings: [],
                accessibilityLabel:
                    "Editor for card \(request.title). Escape saves and returns to the canvas.",
                onSave: commit,
                scrollAnchorRequest: Empty().eraseToAnyPublisher(),
                lineScrollRequest: Empty().eraseToAnyPublisher(),
                cursorByteOffsetRequest: Empty().eraseToAnyPublisher(),
                previewEmbedAtCursor: nil
            )
            .frame(minWidth: 480, minHeight: 280)
            HStack {
                Text("Escape or Done saves the card.")
                    .font(Tokens.Typography.caption)
                    .foregroundStyle(Tokens.ColorRole.textSecondary)
                Spacer()
                Button("Done", action: commit)
                    .keyboardShortcut(.cancelAction)
            }
        }
        .padding(Tokens.Spacing.lg)
        .accessibilityElement(children: .contain)
        .accessibilityLabel("Editing card \(request.title)")
    }

    private func commit() {
        appState.canvasCommitCardEdit(nodeId: request.nodeId, newText: text)
    }
}
