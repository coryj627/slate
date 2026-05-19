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
        // VStack groups the heading line with the section body. The
        // heading row carries its own `line-N` anchor; the body is
        // one coalesced Text overlaid with an invisible
        // `LineAnchorColumn` for per-line scroll targets — see the
        // comment on `bodyContent` below for why we no longer
        // render per-line Text views.
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
                bodyContent(section)
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(.bottom, 8)
        .id(section.anchorId)
    }

    /// Section body: one coalesced `Text` + an invisible
    /// `LineAnchorColumn` overlay providing per-line scroll
    /// anchors.
    ///
    /// PR 59 introduced a per-line shape — one Text per source line
    /// with `.id("line-N")` — so search-result activation could
    /// scroll to a specific 1-based line. At 10k-line notes that
    /// materialized 10k Text views in a plain VStack: each carried
    /// `.textSelection(.enabled)`, a frame modifier, and an AX
    /// element. SwiftUI laid out every child to compute the VStack
    /// intrinsic size, ballooning first-paint cost (~100–300 ms),
    /// inflating the AX tree, and slowing the rotor traversal
    /// (#92 item 2).
    ///
    /// The new shape:
    ///   - **One Text** per section body, `.textSelection(.enabled)`
    ///     applied at the leaf so VoiceOver continuous-read flows
    ///     across the section (matches memory note
    ///     `feedback_swiftui_textselection_ax`).
    ///   - **LineAnchorColumn** overlaid invisibly: one
    ///     `Color.clear` per source line at the body font's line
    ///     height, each carrying `.id("line-N")` and
    ///     accessibility-hidden so the AX tree only sees the
    ///     coalesced body Text. The scroll target is
    ///     approximate when a source line wraps to multiple visual
    ///     lines (the anchor is one nominal line tall regardless),
    ///     which is fine: the snippet in the search overlay tells
    ///     the user what to find, and the landing position is
    ///     within a few visual lines of the match.
    @ViewBuilder
    private func bodyContent(_ section: NoteSection) -> some View {
        let bodyLineCount = section.body.components(separatedBy: "\n").count
        ZStack(alignment: .topLeading) {
            Text(section.body)
                .frame(maxWidth: .infinity, alignment: .leading)
                .textSelection(.enabled)
            LineAnchorColumn(
                lineCount: bodyLineCount,
                firstLineNumber: section.bodyStartLineInFile
            )
        }
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

/// Invisible column of `Color.clear` rows, one per source line,
/// each tagged with `.id("line-N")` so `ScrollViewReader.scrollTo`
/// has a target. Sits behind the coalesced body Text in a ZStack;
/// the anchors are accessibility-hidden so VoiceOver walks the
/// body as one element.
///
/// Each row is `lineHeight` tall, scaled via `@ScaledMetric` so
/// Dynamic Type sizes shift the anchor column in lockstep with
/// the body Text. The 17pt default matches the macOS `.body`
/// font's natural line height; it's a best-fit, not an exact
/// measurement of the rendered Text's per-line baseline. A
/// source line that wraps to multiple visual lines (e.g. a 200-
/// char line in a narrow pane) ends up with a 1-line-tall anchor
/// behind a multi-line-tall visible run, so the scroll-to-line
/// position is approximate in that case — within a few visual
/// lines, which is fine: the snippet in the search overlay
/// tells the user what they were looking for, and they read
/// from there.
private struct LineAnchorColumn: View {
    let lineCount: Int
    let firstLineNumber: Int

    @ScaledMetric(relativeTo: .body) private var lineHeight: CGFloat = 17

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            ForEach(0..<lineCount, id: \.self) { i in
                Color.clear
                    .frame(height: lineHeight)
                    .id("line-\(firstLineNumber + i)")
                    .accessibilityHidden(true)
            }
        }
        .allowsHitTesting(false)
    }
}
