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

    @ViewBuilder
    private func contentState(_ text: String) -> some View {
        let headings = appState.currentNoteHeadings
        ScrollViewReader { proxy in
            ScrollView {
                if headings.isEmpty {
                    plainContent(text)
                } else {
                    sectionedContent(text, headings: headings)
                }
            }
            .onReceive(appState.scrollAnchorRequest) { anchor in
                // Anchor on `.top` so the heading lands just under the
                // toolbar instead of being scrolled past. When reduce-
                // motion is on, skip the animation entirely (WCAG 2.3.1).
                if reduceMotion {
                    proxy.scrollTo(anchor, anchor: .top)
                } else {
                    withAnimation(.easeOut(duration: 0.15)) {
                        proxy.scrollTo(anchor, anchor: .top)
                    }
                }
            }
            .onReceive(appState.lineScrollRequest) { line in
                // Search-result activation (#59) requests a scroll to
                // a specific 1-based line number. Per-line anchors
                // get id `"line-N"`; this routes the request through
                // the same proxy + reduce-motion gate as the heading
                // anchor path.
                //
                // Clamp to >= 1: defensive against the FFI ever
                // returning 0 or a negative line (line numbers in
                // the search result are u32 today, but treating
                // unsigned-zero as line 1 keeps the scroll
                // predictable rather than landing on a non-existent
                // `line-0` anchor.
                let anchor = "line-\(max(1, line))"
                if reduceMotion {
                    proxy.scrollTo(anchor, anchor: .top)
                } else {
                    withAnimation(.easeOut(duration: 0.15)) {
                        proxy.scrollTo(anchor, anchor: .top)
                    }
                }
            }
        }
    }

    /// Single `Text` view fallback when the note has no `#` headings.
    /// The whole pane is still selectable and Dynamic-Type-aware; we
    /// just skip the sectioning work since there are no anchors to wire
    /// up.
    private func plainContent(_ text: String) -> some View {
        Text(text)
            .frame(maxWidth: .infinity, alignment: .leading)
            .padding(16)
            .textSelection(.enabled)
            .accessibilityLabel(accessibilityLabelForContent)
    }

    /// Split `text` into one block per heading using `Heading.ordinal`
    /// to mark boundaries. Each block is rendered as a header row +
    /// body row, with `.id(anchor_id)` on the wrapping VStack so
    /// `ScrollViewReader.scrollTo` can target it.
    ///
    /// Why slice on byte offsets in Swift: the Rust scanner records
    /// heading text and ordinal, but not the byte ranges of the body
    /// underneath each heading. We do the slicing here on the source
    /// string, which is fine because the heading-rotor experience only
    /// needs *the* heading to be a navigable element — the body just
    /// needs to be in the right scroll position.
    ///
    /// Plain `VStack` (not `LazyVStack`): on macOS, `LazyVStack` is
    /// backed by `NSCollectionView`, which the AX bridge exposes as
    /// a list/collection. VoiceOver then announces the note content
    /// container as "list" — wrong for a prose document. `VStack`
    /// maps to `AXGroup`. Note content is small enough (handful of
    /// sections per file) that eager layout is fine — `Text` only
    /// rasterizes the visible portion of the ScrollView anyway.
    @ViewBuilder
    private func sectionedContent(_ text: String, headings: [Heading]) -> some View {
        let sections = sliceIntoSections(text: text, headings: headings)
        VStack(alignment: .leading, spacing: 0) {
            ForEach(sections, id: \.anchorId) { section in
                sectionView(section)
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(16)
        .accessibilityLabel(accessibilityLabelForContent)
    }

    private func sectionView(_ section: NoteSection) -> some View {
        // VStack groups the heading line with its per-line body Texts
        // so .id() on the group anchors scrolling to the heading
        // itself, while per-line anchors below let search-result
        // activation (#59) scroll to a specific source-line.
        VStack(alignment: .leading, spacing: 4) {
            if let heading = section.heading {
                Text(heading.text)
                    .font(headingFont(for: heading.level))
                    .accessibilityAddTraits(.isHeader)
                    .accessibilityHeading(headingLevel(for: heading.level))
                    // Heading line is also a navigable scroll target.
                    // Per-line scroll requests can land on a heading
                    // when the matched body line happens to be the
                    // heading itself.
                    .id("line-\(section.startLineInFile)")
            }
            if !section.body.isEmpty {
                // Per-line rendering: each source line becomes its
                // own Text + `.id("line-N")` anchor. The earlier
                // single-Text approach was simpler for selection-
                // across-lines but couldn't anchor a scroll to a
                // specific line — #59 needs per-line precision.
                //
                // `.textSelection` is still on each Text individually
                // (not on the container) so each line stays
                // selectable; we lose the ability to select a
                // multi-line range in one drag, which we'll revisit
                // if testers complain. Continuous-read flow stays
                // intact because each Text is its own AX element.
                let bodyLines = section.body.components(separatedBy: "\n")
                ForEach(Array(bodyLines.enumerated()), id: \.offset) { i, line in
                    let absoluteLine = section.bodyStartLineInFile + i
                    Text(line.isEmpty ? " " : line)
                        .frame(maxWidth: .infinity, alignment: .leading)
                        .textSelection(.enabled)
                        .id("line-\(absoluteLine)")
                }
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(.bottom, 8)
        .id(section.anchorId)
    }

    private func headingFont(for level: UInt8) -> Font {
        switch level {
        case 1: return .title
        case 2: return .title2
        case 3: return .title3
        case 4: return .headline
        case 5: return .subheadline
        default: return .body
        }
    }

    private func headingLevel(for level: UInt8) -> AccessibilityHeadingLevel {
        // SwiftUI's AccessibilityHeadingLevel caps at h6; clamp anything
        // beyond (which shouldn't happen since Markdown stops at # # # # # #).
        switch level {
        case 1: return .h1
        case 2: return .h2
        case 3: return .h3
        case 4: return .h4
        case 5: return .h5
        default: return .h6
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
