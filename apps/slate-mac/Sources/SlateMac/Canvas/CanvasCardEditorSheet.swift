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

    @State private var pendingDiscard = false
    @AccessibilityFocusState private var draftDialogFocusReturn: DraftFocusTarget?

    private enum DraftFocusTarget: Hashable {
        case dismiss
    }

    private var disabledReason: String? {
        appState.activeCanvasCardEditorDisabledReason
    }

    var body: some View {
        VStack(alignment: .leading, spacing: Tokens.Spacing.sm) {
            Text("Edit \"\(request.title)\"")
                .font(Tokens.Typography.body.weight(.semibold))
            NoteEditorView(
                text: $appState.canvasCardEditorDraft,
                headings: [],
                accessibilityLabel:
                    "Editor for card \(request.title). Escape saves and returns to the canvas.",
                isEditable: disabledReason == nil,
                readOnlyReason: disabledReason,
                onSave: commit,
                scrollAnchorRequest: Empty().eraseToAnyPublisher(),
                lineScrollRequest: Empty().eraseToAnyPublisher(),
                cursorByteOffsetRequest: Empty().eraseToAnyPublisher(),
                previewEmbedAtCursor: nil,
                // Codex review: route the NATIVE context-menu spelling
                // toggle through the same app pref as the main editor —
                // unrouted it was a silent no-op here (updateNSView's
                // pref guard reverts the raw view flag).
                onToggleSpellCheckFromNativeMenu: { [appState] in
                    appState.toggleEditorSpellCheck()
                },
                // Compact host: no 120pt overscroll spill (red-team).
                bottomOverscroll: 0,
                textScale: appState.editorTextScale,
                spellCheckEnabled: appState.editorSpellCheckEnabled
            )
            .frame(minWidth: 480, minHeight: 280)
            if let disabledReason {
                Text(disabledReason)
                    .font(Tokens.Typography.caption)
                    .foregroundStyle(Tokens.ColorRole.textSecondary)
                    .accessibilityLabel(disabledReason)
                    .help(disabledReason)
            }
            HStack {
                Text(
                    disabledReason == nil
                        ? "Escape or Done saves the card."
                        : "The draft remains selectable and copyable while editing is unavailable."
                )
                    .font(Tokens.Typography.caption)
                    .foregroundStyle(Tokens.ColorRole.textSecondary)
                Spacer()
                if disabledReason == AppState.batchTrashQuarantineReason {
                    Button("Check Again") {
                        _ = appState.retryCanvasCardEditorReconciliation()
                    }
                    .disabled(appState.isMutatingStructure)
                    .accessibilityHint("Rescan the vault and check whether this canvas is still present.")
                    .help("Check whether this canvas is still present")
                }
                if disabledReason == nil {
                    Button("Discard…") { pendingDiscard = true }
                        .accessibilityFocused($draftDialogFocusReturn, equals: .dismiss)
                    Button("Done", action: commit)
                        // M8's established embedded-editor behavior: Escape
                        // saves while the destination is writable.
                        .keyboardShortcut(.cancelAction)
                } else {
                    Button("Close…") { pendingDiscard = true }
                        .keyboardShortcut(.cancelAction)
                        .accessibilityFocused($draftDialogFocusReturn, equals: .dismiss)
                    Button("Done", action: commit)
                        .disabled(true)
                        .accessibilityHint(disabledReason ?? "Editing is unavailable.")
                        .help(disabledReason ?? "Editing is unavailable")
                }
            }
        }
        .padding(Tokens.Spacing.lg)
        .accessibilityElement(children: .contain)
        .accessibilityLabel("Editing card \(request.title)")
        .interactiveDismissDisabled(disabledReason != nil)
        .confirmationDialog(
            "Discard the unsaved card draft?",
            isPresented: $pendingDiscard,
            titleVisibility: .visible
        ) {
            Button("Cancel", role: .cancel) {
                draftDialogFocusReturn = .dismiss
            }
            Button("Discard Draft", role: .destructive) {
                appState.dismissCanvasCardEditor()
            }
        } message: {
            Text("Closing without saving discards this draft. Copy it first if you need to keep it.")
        }
        .onChange(of: pendingDiscard) { wasPresented, isPresented in
            if wasPresented, !isPresented, appState.canvasCardEditor != nil {
                draftDialogFocusReturn = .dismiss
            }
        }
    }

    private func commit() {
        appState.canvasCommitCardEdit(
            nodeId: request.nodeId,
            newText: appState.canvasCardEditorDraft)
    }
}
