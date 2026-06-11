// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI

/// Right-hand outline pane showing the parsed headings of the
/// currently-selected note.
///
/// Bound to `AppState.currentNoteHeadings`. Clicking (or pressing
/// Return on) a row sends the heading's `anchorId` through
/// `AppState.requestScrollToHeading`, which the content pane picks
/// up via `onReceive(scrollAnchorRequest)` and scrolls into view.
///
/// VoiceOver story: each row carries `.isHeader` so the heading rotor
/// (VO+H) walks them in document order, and the label carries the
/// level in the documented phrasing ("Level 2 heading: …", #420) so
/// the user can build a mental outline. On note change, we post a
/// polite announcement summarizing the heading count.
struct OutlineSidebar: View {
    @EnvironmentObject private var appState: AppState
    @State private var announcedFilePath: String?

    var body: some View {
        Group {
            if appState.selectedFilePath == nil {
                emptyState(message: "Select a file to see its outline.")
            } else if appState.isLoadingNote && appState.currentNoteHeadings.isEmpty {
                loadingState
            } else if appState.currentNoteHeadings.isEmpty {
                emptyState(message: "This note has no headings.")
            } else {
                headingList
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .navigationTitle("Outline")
        .onChange(of: appState.currentNoteHeadings) { _ in
            announceIfNeeded()
        }
        .onChange(of: appState.selectedFilePath) { _ in
            // Re-arm the announcement so the next selected file gets
            // its own count read out, even if the heading list happens
            // to be the same length.
            announcedFilePath = nil
        }
        .onAppear {
            announceIfNeeded()
        }
    }

    // MARK: - States

    private func emptyState(message: String) -> some View {
        VStack(spacing: 8) {
            Spacer()
            Text(message)
                .foregroundStyle(.secondary)
                .multilineTextAlignment(.center)
            Spacer()
        }
        .padding()
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .accessibilityElement(children: .combine)
        .accessibilityLabel(message)
    }

    private var loadingState: some View {
        VStack(spacing: 12) {
            ProgressView()
            Text("Loading outline…")
                .foregroundStyle(.secondary)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .accessibilityElement(children: .combine)
        .accessibilityLabel("Loading outline.")
    }

    private var headingList: some View {
        // List (not LazyVStack) gives us NSCollectionView under the
        // hood, which handles keyboard navigation (arrows + Return) +
        // VoiceOver focus management for free. Stable id is the
        // anchor_id, which the scanner guarantees is unique within a
        // file.
        List(appState.currentNoteHeadings, id: \.anchorId) { heading in
            row(for: heading)
        }
        .listStyle(.sidebar)
    }

    private func row(for heading: Heading) -> some View {
        Button {
            appState.requestScrollToHeading(anchor: heading.anchorId)
            // #420 (F-B3): the scroll itself is silent and keyboard
            // focus stays in the panel — without an announcement a
            // VO user gets no confirmation the activation did
            // anything. "Scrolled to", not "Moved to": focus does
            // NOT relocate (same verb as the tasks flow's "Scrolled
            // to <file>, line N." and this row's own hint). Polite
            // priority so it doesn't clip navigation speech.
            postAccessibilityAnnouncement(
                "Scrolled to \(heading.text).",
                priority: .medium
            )
        } label: {
            HStack(spacing: 0) {
                // Two spaces per level (after h1) gives a visible
                // hierarchy without eating sidebar real estate. h1 has
                // no indent.
                if heading.level > 1 {
                    Spacer()
                        .frame(width: indentWidth(for: heading.level))
                }
                Text(heading.text)
                    .lineLimit(2)
                    .frame(maxWidth: .infinity, alignment: .leading)
            }
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        // #420 (F-B2): the trait-only design read nicely on paper —
        // `.accessibilityHeading(level)` + `.isHeader` SHOULD make
        // VO say "Heading level 2, …" — but the 2026-06-10 VO test
        // proved macOS speaks plain "…button" for Button rows and
        // never voices the trait. Bake the documented phrasing
        // ("Level N heading: <text>", same form the original smoke
        // test shipped) into the label; keep the traits so rotor
        // navigation still groups rows as headings.
        .accessibilityLabel(Self.rowAccessibilityLabel(for: heading))
        .accessibilityAddTraits(.isHeader)
        .accessibilityHeading(headingLevel(for: heading.level))
        .accessibilityHint("Scrolls the note to this heading.")
        .help(heading.text)
    }

    /// AX label for an outline row — the documented "Level N
    /// heading: <text>" phrasing (#420). Static for the regression
    /// test.
    static func rowAccessibilityLabel(for heading: Heading) -> String {
        "Level \(heading.level) heading: \(heading.text)"
    }

    private func indentWidth(for level: UInt8) -> CGFloat {
        // 12pt per level beyond h1. Multiplied by the system text-size
        // factor inside `Spacer().frame(width:)` so Dynamic Type
        // doesn't visually collapse the hierarchy.
        CGFloat(level - 1) * 12
    }

    private func headingLevel(for level: UInt8) -> AccessibilityHeadingLevel {
        switch level {
        case 1: return .h1
        case 2: return .h2
        case 3: return .h3
        case 4: return .h4
        case 5: return .h5
        default: return .h6
        }
    }

    private func announceIfNeeded() {
        guard let path = appState.selectedFilePath,
            announcedFilePath != path,
            !appState.currentNoteHeadings.isEmpty
        else { return }
        announcedFilePath = path
        let n = appState.currentNoteHeadings.count
        let suffix = n == 1 ? "heading" : "headings"
        postAccessibilityAnnouncement("Outline, \(n) \(suffix).")
    }
}
