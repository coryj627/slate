// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

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
    /// Observed directly: `viewModes` lives on WorkspaceState, and nested
    /// ObservableObject changes are not forwarded through `appState` (the
    /// U1 WorkspaceTreeView lesson). Callers pass `appState.workspace`.
    @ObservedObject var workspace: WorkspaceState

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

    /// Initial-focus target inside the popover. Audit #215 caught
    /// that the popover opened without an explicit focus target,
    /// leaving keyboard-only users unable to reach the Close /
    /// Jump-to-source controls via Tab from the editor. We focus
    /// the Close button on appear so Esc / Return / Tab all work
    /// immediately.
    @FocusState private var popoverInitialFocus: PopoverInitialFocusTarget?

    enum PopoverFocusTarget: Hashable {
        case editor
    }

    enum PopoverInitialFocusTarget: Hashable {
        case closeButton
    }

    /// U3-2: VoiceOver focus target for the reading surface. Set when the
    /// mode flips to reading so AX focus lands on the first content the
    /// new surface owns (the editing direction is handled by the caret
    /// one-shot, which makes the editor first responder).
    @AccessibilityFocusState private var readingSurfaceFocused: Bool

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
        .onChange(of: appState.currentNoteText) {
            announceIfNeeded()
        }
        .onAppear {
            announceIfNeeded()
        }
    }

    // MARK: - States

    private var emptyState: some View {
        VStack(spacing: Tokens.Spacing.sm) {
            Spacer()
            Text("Select a file to read.")
                .font(Tokens.Typography.body)
                .foregroundStyle(Tokens.ColorRole.textSecondary)
            Text("Open a note from the file tree, or press ⌘N to create one.")
                .font(Tokens.Typography.caption)
                .foregroundStyle(Tokens.ColorRole.textSecondary)
            Spacer()
        }
        .padding(Tokens.Spacing.lg)
        .accessibilityElement(children: .combine)
        .accessibilityLabel("Detail area. Select a file in the sidebar to read its content.")
    }

    private var loadingState: some View {
        VStack(spacing: Tokens.Spacing.md) {
            ProgressView()
            Text(loadingMessage)
                .font(Tokens.Typography.body)
                .foregroundStyle(Tokens.ColorRole.textSecondary)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .accessibilityElement(children: .combine)
        .accessibilityLabel(loadingMessage)
    }

    private func errorState(_ message: String) -> some View {
        VStack(alignment: .leading, spacing: Tokens.Spacing.sm) {
            Text("Could not load file")
                .font(Tokens.Typography.sectionHeader)
                .accessibilityAddTraits(.isHeader)
            Text(message)
                .font(Tokens.Typography.body)
                .foregroundStyle(Tokens.ColorRole.textSecondary)
        }
        .padding(Tokens.Spacing.lg)
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
    }

    @ViewBuilder
    private func contentState(_ text: String) -> some View {
        // U3-3 (#467): the properties widget is PINNED above the mode
        // surface, in BOTH modes, outside the editor scroll view —
        // VoiceOver reads Properties, then the content, matching where
        // frontmatter physically lives in the file.
        VStack(spacing: 0) {
            NotePropertiesHeader(workspace: workspace)
            // U3-2 (#466): one mode mounted at a time — `if` (never the
            // ZStack-retention pattern): an offscreen editor duplicates the
            // whole text tree for VO and double-fires publishers; the
            // reading view's scroll position is cheap to lose, and the
            // editor caret round-trips via AppState's caret park.
            if workspace.activeViewMode == .reading {
                readingSurface(text)
            } else {
                editorSurface(text)
            }
        }
    }

    /// The rendered read-only surface (U3-1's ReadingView, live buffer in).
    private func readingSurface(_ text: String) -> some View {
        ReadingView(
            text: text,
            pathLabel: displayName,
            onSwitchToEditing: { [appState] in
                appState.setViewMode(.editing)
            },
            router: .live(appState: appState),
            context: ReadingView.ReadingBlockContext(
                mathBlocks: appState.currentNoteMathBlocks,
                codeBlocks: appState.currentNoteCodeBlocks,
                diagramBlocks: appState.currentNoteDiagramBlocks,
                citations: appState.currentNoteCitations,
                tasks: appState.currentNoteTasks,
                isDocumentDirty: appState.hasUnsavedChanges,
                onToggleTask: { [appState] item in
                    appState.toggleCurrentTask(item)
                },
                taskLineOffset: appState.bodyLineOffset
            )
        )
        .accessibilityFocused($readingSurfaceFocused)
        .onAppear {
            // Mode flip → focus lands in the new surface (WCAG 2.4.3).
            // onAppear (not onChange of mode): the reading surface also
            // mounts via tab switches onto reading-mode tabs.
            readingSurfaceFocused = true
        }
    }

    private func editorSurface(_ text: String) -> some View {
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
            cursorByteOffsetRequest: appState.cursorByteOffsetRequest
                .compactMap { $0 }
                .handleEvents(receiveOutput: { [appState] _ in
                    // One-shot: clear on delivery so a later editor
                    // re-attach doesn't replay a stale park (#421).
                    appState.clearPendingCursorByteOffset()
                })
                .eraseToAnyPublisher(),
            previewEmbedAtCursor: { [appState] target, line in
                appState.requestEmbedPreview(target: target, sourceLine: line)
            },
            onCaretUTF16Change: { [appState] location in
                appState.noteEditorCaretDidMove(toUTF16: location)
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
                embedPreviewContent(preview)
            }
        }
    }

    /// Popover body for an active `EmbedPreview`. Split out so the
    /// `.popover` modifier's content closure stays readable, and so
    /// the a11y wiring (initial focus, contain-children scope,
    /// scrollable body, header with source-line cue) all lives in
    /// one place.
    @ViewBuilder
    private func embedPreviewContent(_ preview: EmbedPreview) -> some View {
        // Audit #213: cap the popover height so large Dynamic
        // Type doesn't push the body past the visible area
        // (WCAG 1.4.10). Body scrolls if it exceeds the cap.
        ScrollView {
            VStack(alignment: .leading, spacing: Tokens.Spacing.sm) {
                // Audit #209: textual spatial-bearing cue. The
                // popover can't visually anchor at the cursor's
                // screen position without geometry plumbing, so
                // surface the line number in the header instead
                // — VoiceOver users + magnifier users both
                // benefit.
                Text(verbatim: previewHeaderText(preview))
                    // Emphasized caption: a semantic Dynamic-Type style (scales);
                    // Tokens.Typography has no bold-caption role, so kept direct.
                    .font(.caption.weight(.semibold))
                    .foregroundStyle(Tokens.ColorRole.textSecondary)
                    .accessibilityAddTraits(.isHeader)

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
                // Audit #215: initial keyboard focus on the
                // popover. Close button gets focus on appear so
                // Esc / Return / Tab all work from the moment
                // the popover renders.
                .focused($popoverInitialFocus, equals: .closeButton)
            }
            .padding(Tokens.Spacing.md)
        }
        .frame(
            minWidth: 420,
            idealWidth: 480,
            maxWidth: 640,
            minHeight: 200,
            maxHeight: 540
        )
        // Audit #210: pair `.accessibilityLabel` with
        // `.accessibilityElement(children: .contain)` so the
        // label scopes the contained tree without the (undefined)
        // sibling-vs-combined behaviour the lone label modifier
        // produced.
        .accessibilityElement(children: .contain)
        .accessibilityLabel(previewAccessibilityLabel(preview))
        .onAppear {
            popoverInitialFocus = .closeButton
        }
    }

    private func previewHeaderText(_ preview: EmbedPreview) -> String {
        if let line = preview.sourceLine {
            return "Preview for `\(preview.target)` — source line \(line)"
        }
        return "Preview for `\(preview.target)`"
    }

    private func previewAccessibilityLabel(_ preview: EmbedPreview) -> String {
        if let line = preview.sourceLine {
            return "Embed preview for \(preview.target), source line \(line)."
        }
        return "Embed preview for \(preview.target)."
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

