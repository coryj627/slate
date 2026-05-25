import SwiftUI

/// Read-only Markdown source pane.
///
/// Bound to `AppState.currentNoteText`, which AppState populates by
/// observing `selectedFilePath` and calling
/// `VaultSession.readText(path:)` off the main actor. Milestone B
/// ships the raw source (no rendered preview) because raw text is
/// the most accessible thing we can give VoiceOver while we don't
/// have content pipelines for math / Mermaid / code highlighting
/// (those land in V1.x per `docs/plans/05` §6).
///
/// When the parsed-heading list is non-empty, the content is split
/// into one section per heading with stable anchors (`.id(heading
/// .anchorId)`) so `OutlineSidebar` row taps can scroll-to via a
/// `ScrollViewReader`. Each section header carries `.isHeader` so
/// VoiceOver's heading rotor (VO+H) walks them in document order.
struct NoteContentView: View {
    @EnvironmentObject private var appState: AppState

    @State private var announcedFilePath: String?
    /// Respect the system "Reduce motion" setting (WCAG 2.3.1). When
    /// true, scroll-to-anchor jumps instantly instead of animating —
    /// vestibular-sensitive users see no movement.
    @Environment(\.accessibilityReduceMotion) private var reduceMotion

    /// Focus-return target for the embed-preview popover (#188).
    /// The popover's content + dismiss path set this to `.editor`
    /// so VoiceOver / keyboard focus returns to the editor after
    /// dismissal — WCAG 2.4.3 + 2.1.2.
    @AccessibilityFocusState private var popoverFocusReturn: PopoverFocusTarget?

    enum PopoverFocusTarget: Hashable {
        case editor
    }

    var body: some View {
        Group {
            if let error = appState.noteLoadError {
                errorState(error)
            } else if appState.isLoadingNote {
                loadingState
            } else if let text = appState.currentNoteText {
                contentState(text)
            } else if appState.selectedFilePath != nil {
                // selectedFilePath set but currentNoteText not yet
                // populated → load is queued but hasn't flipped
                // isLoadingNote = true yet. Show the loading shell so
                // the user doesn't see the empty-state flash.
                loadingState
            } else {
                emptyState
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .onChange(of: appState.currentNoteText) { _ in
            announceIfNeeded()
        }
        .onAppear {
            announceIfNeeded()
        }
    }

    // MARK: - States

    private var emptyState: some View {
        VStack(spacing: 8) {
            Spacer()
            Text("Select a file to read.")
                .foregroundStyle(.secondary)
            Text("Reading lands in a follow-up milestone.")
                .font(.caption)
                .foregroundStyle(.secondary)
            Spacer()
        }
        .padding()
        .accessibilityElement(children: .combine)
        .accessibilityLabel("Detail area. Select a file in the sidebar to read its content.")
    }

    private var loadingState: some View {
        VStack(spacing: 12) {
            ProgressView()
            Text(loadingMessage)
                .foregroundStyle(.secondary)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .accessibilityElement(children: .combine)
        .accessibilityLabel(loadingMessage)
    }

    private func errorState(_ message: String) -> some View {
        VStack(alignment: .leading, spacing: 8) {
            Text("Could not load file")
                .font(.headline)
                .accessibilityAddTraits(.isHeader)
            Text(message)
                .foregroundStyle(.secondary)
        }
        .padding()
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
    }

    private func contentState(_ text: String) -> some View {
        // Editor: the read-only sectioned shape that lived here
        // before #63 has moved to NoteEditorView (NSTextView
        // wrapper). Scroll routing goes through the wrapper's
        // Combine subscriptions; the SwiftUI `ScrollViewReader`
        // anchor scheme doesn't apply to NSTextView. See
        // `NoteEditorView` for the heading-rotor trade-off — the
        // outline sidebar continues to provide heading navigation.
        //
        // `text` is consumed inside `.onAppear` so SwiftUI keeps
        // an observation edge on the loaded buffer for re-render
        // tracking; the editor reads the live buffer through its
        // binding.
        return NoteEditorView(
            text: appState.noteTextBinding(),
            headings: appState.currentNoteHeadings,
            accessibilityLabel: accessibilityLabelForContent,
            onSave: { [appState] in appState.saveCurrentNote() },
            scrollAnchorRequest: appState.scrollAnchorRequest.eraseToAnyPublisher(),
            lineScrollRequest: appState.lineScrollRequest.eraseToAnyPublisher(),
            cursorByteOffsetRequest: appState.cursorByteOffsetRequest.eraseToAnyPublisher(),
            previewEmbedAtCursor: { [appState] target in
                appState.requestEmbedPreview(target: target)
            }
        )
        .onAppear { _ = text }
        .accessibilityFocused($popoverFocusReturn, equals: .editor)
        // Embed-preview popover (#188): bound to AppState's
        // `pendingEmbedPreview`. Cmd+E in the editor populates it
        // via the closure above; the popover dismisses via the
        // binding setter (click outside, Esc) or "Jump to source"
        // (which closes + navigates). Each dismissal path sets
        // `popoverFocusReturn = .editor` so VoiceOver/keyboard
        // focus returns to the editor (WCAG 2.4.3 + 2.1.2).
        .popover(
            isPresented: Binding(
                get: { appState.pendingEmbedPreview != nil },
                set: { isShown in
                    if !isShown {
                        appState.dismissEmbedPreview()
                        popoverFocusReturn = .editor
                    }
                }
            ),
            arrowEdge: .top
        ) {
            if let preview = appState.pendingEmbedPreview {
                VStack(alignment: .leading, spacing: 8) {
                    EmbedView(
                        resolution: preview.resolution,
                        jumpToSourceAction: { [appState] target in
                            appState.dismissEmbedPreview()
                            popoverFocusReturn = .editor
                            appState.openEmbedTarget(target)
                        }
                    )
                    Button("Close") {
                        appState.dismissEmbedPreview()
                        popoverFocusReturn = .editor
                    }
                    .keyboardShortcut(.cancelAction)
                    .accessibilityHint(
                        "Close the embed preview. Focus returns to the editor."
                    )
                }
                .frame(minWidth: 420, idealWidth: 480, maxWidth: 640)
                .padding(12)
                .accessibilityLabel("Embed preview for \(preview.target).")
            }
        }
    }

    // MARK: - Helpers

    private var displayName: String {
        guard let path = appState.selectedFilePath else { return "" }
        return (path as NSString).lastPathComponent
    }

    private var loadingMessage: String {
        if displayName.isEmpty {
            return "Loading file…"
        } else {
            return "Loading \(displayName)…"
        }
    }

    private var accessibilityLabelForContent: String {
        // VoiceOver users hear the filename + first line of content
        // before navigating into the text. Without this label, the
        // ScrollView is announced as just "scrollable content."
        if displayName.isEmpty {
            return "Note content."
        } else {
            return "Note content for \(displayName)."
        }
    }

    private func announceIfNeeded() {
        // Only announce when content has actually loaded — i.e.
        // currentNoteText is non-nil — and only once per file. The
        // announcement re-arms when the path changes.
        guard appState.currentNoteText != nil,
            let path = appState.selectedFilePath,
            announcedFilePath != path
        else { return }
        announcedFilePath = path
        postAccessibilityAnnouncement("Showing \(displayName).")
    }

}

