// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI

/// Sidebar pane listing every citation in the currently-loaded
/// note in document order.
///
/// Bound to `AppState.currentNoteCitations`. Each row's accessibility
/// label is the citation's `speech_text` (built from structured data
/// by the renderer in #277), never the visual text — that's the
/// Milestone L a11y differentiator. Activating a row sets
/// `AppState.expandedCitation`, which `MainSplitView`'s sheet
/// modifier surfaces as a `CitationPopover`.
///
/// Scope decision: the issue (#279) calls for inline non-editable
/// citation tokens inside `NoteEditorView`. The math / code / Mermaid
/// pipelines (Milestone K) shipped the same way — data + view
/// component, with inline-in-editor integration deferred to V1.x
/// because `EditorEmbedSpans` doesn't yet apply `NSTextAttachment`
/// to the NSTextStorage. Until that lands, citations surface via this
/// panel (AT label = speech_text, expand popover) so tester users
/// hear the differentiator and can navigate citations from the
/// keyboard. The inline editor token is its own follow-up.
struct CitationsPanel: View {
    @EnvironmentObject private var appState: AppState
    @State private var announcedFilePath: String?

    var body: some View {
        Group {
            if appState.selectedFilePath == nil {
                emptyState(message: "Select a file to see its citations.")
            } else if appState.isLoadingCitations && appState.currentNoteCitations.isEmpty {
                loadingState
            } else if let error = appState.citationsLoadError {
                errorState(error)
            } else if appState.currentNoteCitations.isEmpty {
                emptyState(message: "This note has no citations.")
            } else {
                citationList
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .navigationTitle("Citations")
        .onChange(of: appState.currentNoteCitations) {
            announceIfNeeded()
        }
        .onChange(of: appState.selectedFilePath) {
            // Re-arm the announcement so the next file gets its own
            // count read out, even if the citation list is the same
            // length.
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
            Text("Loading citations…")
                .foregroundStyle(.secondary)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .accessibilityElement(children: .combine)
        .accessibilityLabel("Loading citations.")
    }

    private func errorState(_ message: String) -> some View {
        VStack(alignment: .leading, spacing: 8) {
            Text("Citations couldn't be loaded")
                .font(.headline)
                .accessibilityAddTraits(.isHeader)
            Text(message)
                .foregroundStyle(.secondary)
        }
        .padding()
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
        .accessibilityElement(children: .combine)
        .accessibilityLabel("Citations couldn't be loaded. \(message)")
    }

    private var citationList: some View {
        // Same pattern as OutlineSidebar — List backs onto
        // NSCollectionView for free keyboard + VoiceOver navigation.
        // Stable id is the citation's byte offset within the source,
        // which is unique per site in a single file.
        List(
            Array(appState.currentNoteCitations.enumerated()),
            id: \.offset
        ) { _, citation in
            row(for: citation)
        }
        .listStyle(.sidebar)
    }

    private func row(for citation: RenderedCitation) -> some View {
        Button {
            appState.expandedCitation = citation
        } label: {
            // Visible: the visual_text from hayagriva
            // (e.g. "(Smith, 2020, p. 23)"). VoiceOver overrides this
            // with the row's `accessibilityLabel` below, so sighted
            // and AT users get distinct treatments without showing
            // both side by side (which would clutter the row + risk
            // truncation under Dynamic Type — WCAG 1.4.4).
            Text(citation.visualText.isEmpty ? citation.raw : citation.visualText)
                .lineLimit(2)
                .frame(maxWidth: .infinity, alignment: .leading)
                .foregroundStyle(
                    citation.bibEntry == nil && citation.styleId.isEmpty == false
                        ? Color.orange  // unresolved key marker
                        : Color.primary
                )
                .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        // The AT label IS the speech_text — never the visual_text.
        // This is the entire reason Milestone L exists: screen readers
        // never say "open paren Smith comma twenty twenty close paren"
        // because we never give them the parenthesised form.
        .accessibilityLabel(citation.speechText)
        .accessibilityHint("Activate to expand citation fields.")
    }

    private func announceIfNeeded() {
        guard let path = appState.selectedFilePath,
            announcedFilePath != path,
            !appState.currentNoteCitations.isEmpty
        else { return }
        announcedFilePath = path
        let n = appState.currentNoteCitations.count
        let suffix = n == 1 ? "citation" : "citations"
        postAccessibilityAnnouncement("Citations, \(n) \(suffix).")
    }
}
