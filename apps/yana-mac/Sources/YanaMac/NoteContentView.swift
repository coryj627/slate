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
    @ViewBuilder
    private func sectionedContent(_ text: String, headings: [Heading]) -> some View {
        let sections = sliceIntoSections(text: text, headings: headings)
        LazyVStack(alignment: .leading, spacing: 0) {
            ForEach(sections, id: \.anchorId) { section in
                sectionView(section)
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(16)
        .textSelection(.enabled)
        .accessibilityLabel(accessibilityLabelForContent)
    }

    private func sectionView(_ section: NoteSection) -> some View {
        // VStack groups the heading line with its body so .id() on the
        // group anchors scrolling to the heading itself, not just the
        // body underneath.
        VStack(alignment: .leading, spacing: 4) {
            if let heading = section.heading {
                Text(heading.text)
                    .font(headingFont(for: heading.level))
                    .accessibilityAddTraits(.isHeader)
                    .accessibilityHeading(headingLevel(for: heading.level))
            }
            if !section.body.isEmpty {
                Text(section.body)
                    .frame(maxWidth: .infinity, alignment: .leading)
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

    // MARK: - Section slicing

    /// Walk the source line-by-line and split at ATX (`#`) heading
    /// lines that match the scanner's recorded headings (in document
    /// order). The first section may have no heading (preamble before
    /// any `#`). Heading lines whose text doesn't line up with the
    /// scanner's record (e.g. inside a fenced code block) get folded
    /// into the body of the prior section.
    ///
    /// Pure function so it's straightforward to unit-test if needed
    /// later; doesn't touch `self`.
    private func sliceIntoSections(text: String, headings: [Heading]) -> [NoteSection] {
        let lines = text.components(separatedBy: "\n")
        var sections: [NoteSection] = []
        var current = NoteSection(heading: nil, anchorId: "__preamble", body: "")
        var bodyBuf: [String] = []
        var headingIdx = 0

        for line in lines {
            if headingIdx < headings.count,
                let parsed = parseAtxHeading(line),
                parsed.level == headings[headingIdx].level,
                parsed.text == headings[headingIdx].text
            {
                // Finalize the current section (preamble or prior heading).
                current.body = bodyBuf.joined(separator: "\n")
                if current.heading != nil || !current.body.isEmpty {
                    sections.append(current)
                }
                let h = headings[headingIdx]
                current = NoteSection(heading: h, anchorId: h.anchorId, body: "")
                bodyBuf = []
                headingIdx += 1
            } else {
                bodyBuf.append(line)
            }
        }
        // Final section.
        current.body = bodyBuf.joined(separator: "\n")
        if current.heading != nil || !current.body.isEmpty {
            sections.append(current)
        }
        return sections
    }

    /// Return `(level, text)` if `line` is an ATX heading
    /// (`# Heading`, `## Sub`, …), else nil. Match the Rust scanner's
    /// heading-extraction rule (1–6 `#` chars + space + body) so the
    /// boundaries line up. We deliberately don't try to detect setext
    /// (=== / ---) headings since the scanner doesn't emit those today.
    private func parseAtxHeading(_ line: String) -> (level: UInt8, text: String)? {
        var level: UInt8 = 0
        var iterator = line.unicodeScalars.makeIterator()
        while let s = iterator.next() {
            if s == "#" {
                level += 1
                if level > 6 { return nil }
            } else if s == " " {
                if level == 0 { return nil }
                let rest = line.dropFirst(Int(level) + 1)
                let trimmed = rest.trimmingCharacters(in: .whitespaces)
                if trimmed.isEmpty { return nil }
                return (level, trimmed)
            } else {
                return nil
            }
        }
        return nil
    }
}

/// One heading-bounded chunk of the note source. `.heading` is nil for
/// the preamble block before the first `#`.
private struct NoteSection {
    var heading: Heading?
    var anchorId: String
    var body: String
}
