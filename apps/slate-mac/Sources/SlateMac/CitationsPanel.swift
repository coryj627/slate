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
/// `AppState.expandedCitation` and presents the `CitationPopover`
/// anchored to that row (#878 — a field-level expansion belongs on its
/// trigger, not a detached sheet).
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

    /// Which row's `CitationPopover` is open (#878). The list can hold
    /// duplicate citations (the same `[@key]` cited twice is an equal
    /// `RenderedCitation`), so the anchor is keyed by the row's stable
    /// index — `appState.expandedCitation` alone can't disambiguate which
    /// row to point the arrow at. Cleared together with `expandedCitation`.
    @State private var expandedIndex: Int?

    /// Focus-return target for the anchored popover (#878, WCAG 2.4.3 +
    /// 2.1.2): on dismiss, VoiceOver/keyboard focus returns to the row that
    /// opened it — the contract the sheet path established, now homed on the
    /// trigger itself.
    @AccessibilityFocusState private var focusedRow: Int?

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
        .onChange(of: appState.expandedCitation) { _, newValue in
            // #878 red-team: an EXTERNAL clear (⌘J jump-to-bib, note-switch,
            // vault close) nils `expandedCitation` without invoking the
            // popover binding's set, so `dismissExpansion` never runs and
            // this panel-local index stays pointed at the last row. Drop it
            // here so a later Reading-mode click can't reopen a stale popover.
            if newValue == nil { expandedIndex = nil }
        }
        .onAppear {
            announceIfNeeded()
        }
    }

    // MARK: - States

    private func emptyState(message: String) -> some View {
        VStack(spacing: Tokens.Spacing.sm) {
            Spacer()
            Text(message)
                .font(Tokens.Typography.body)
                .foregroundStyle(Tokens.ColorRole.textSecondary)
                .multilineTextAlignment(.center)
            Spacer()
        }
        .padding(Tokens.Spacing.lg)
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .accessibilityElement(children: .combine)
        .accessibilityLabel(message)
    }

    private var loadingState: some View {
        VStack(spacing: Tokens.Spacing.md) {
            ProgressView()
            Text("Loading citations…")
                .font(Tokens.Typography.body)
                .foregroundStyle(Tokens.ColorRole.textSecondary)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .accessibilityElement(children: .combine)
        .accessibilityLabel("Loading citations.")
    }

    private func errorState(_ message: String) -> some View {
        VStack(alignment: .leading, spacing: Tokens.Spacing.sm) {
            Text("Citations couldn't be loaded")
                .font(Tokens.Typography.sectionHeader)
                .accessibilityAddTraits(.isHeader)
            Text(message)
                .font(Tokens.Typography.body)
                .foregroundStyle(Tokens.ColorRole.textSecondary)
        }
        .padding(Tokens.Spacing.lg)
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
        ) { item in
            row(for: item.element, index: item.offset)
        }
        .listStyle(.sidebar)
    }

    private func row(for citation: RenderedCitation, index: Int) -> some View {
        let isUnresolved =
            citation.bibEntry == nil && citation.styleId.isEmpty == false
        return Button {
            // Row-anchored: this row owns the popover; the detached
            // fallback in MainSplitView stays closed (#878).
            expandedIndex = index
            appState.expandedCitationRowAnchored = true
            appState.expandedCitation = citation
        } label: {
            // Visible: the visual_text from hayagriva
            // (e.g. "(Smith, 2020, p. 23)"). VoiceOver overrides this
            // with the row's `accessibilityLabel` below, so sighted
            // and AT users get distinct treatments without showing
            // both side by side (which would clutter the row + risk
            // truncation under Dynamic Type — WCAG 1.4.4).
            HStack(spacing: Tokens.Spacing.xs) {
                Text(citation.visualText.isEmpty ? citation.raw : citation.visualText)
                    .lineLimit(2)
                    .foregroundStyle(
                        // Unresolved-key marker: the U5-3 APCA-gated
                        // warning role (this was the one `.orange`
                        // literal the U5-3 sweep missed — raw orange
                        // measured Lc ≈ 43 light, below the floor) PLUS
                        // the text badge below, so the state is never
                        // color-alone (WCAG 1.4.1; the
                        // OutgoingLinksPanel pattern).
                        isUnresolved
                            ? Tokens.ColorRole.warningText
                            : Tokens.ColorRole.textPrimary
                    )
                if isUnresolved {
                    Text("Unresolved")
                        .font(Tokens.Typography.caption)
                        .foregroundStyle(Tokens.ColorRole.warningText)
                }
                Spacer(minLength: 0)
            }
            .frame(maxWidth: .infinity, alignment: .leading)
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        // The AT label IS the speech_text — never the visual_text.
        // This is the entire reason Milestone L exists: screen readers
        // never say "open paren Smith comma twenty twenty close paren"
        // because we never give them the parenthesised form. The
        // unresolved state rides the label as a prefix so AT users get
        // the same cue the badge gives sighted users.
        .accessibilityLabel(
            isUnresolved
                ? "Unresolved citation key. \(citation.speechText)"
                : citation.speechText
        )
        .accessibilityHint("Activate to expand citation fields.")
        .accessibilityFocused($focusedRow, equals: index)
        // popovers.md:21 — "a popover appears above other content when people
        // click a control." The expansion is a field-level detail of THIS row,
        // so the popover anchors here (arrow pointing at the row, leading edge
        // — the panel sits at the trailing edge of the window) instead of the
        // detached sheet #878 replaced.
        .popover(
            isPresented: popoverBinding(index: index),
            attachmentAnchor: .rect(.bounds),
            arrowEdge: .leading
        ) {
            CitationPopover(
                citation: citation,
                onClose: {
                    // WCAG 2.4.3 + 2.1.2: return focus to the row anchor.
                    focusedRow = index
                    dismissExpansion(returningFocusTo: index)
                }
            )
            .environmentObject(appState)
        }
    }

    /// Per-row popover presentation, keyed by the row's stable index so
    /// duplicate citations don't both present (#878). Dismissal (Close,
    /// Escape, or an outside click) clears the shared expansion state and
    /// returns focus to the row.
    private func popoverBinding(index: Int) -> Binding<Bool> {
        Binding(
            // #878 red-team: also gate on the anchor discriminator. Without
            // it, a Reading-mode expansion (anchored == false) that inherits
            // a stale `expandedIndex` would re-arm this row's popover AT THE
            // SAME TIME as MainSplitView's detached fallback — two citation
            // surfaces for one click. Only a panel-row activation sets
            // anchored == true, so only it presents here.
            get: {
                expandedIndex == index && appState.expandedCitation != nil
                    && appState.expandedCitationRowAnchored
            },
            set: { presented in
                if !presented { dismissExpansion(returningFocusTo: index) }
            }
        )
    }

    private func dismissExpansion(returningFocusTo index: Int) {
        expandedIndex = nil
        appState.expandedCitation = nil
        appState.expandedCitationRowAnchored = false
        // WCAG 2.4.3 + 2.1.2: return VoiceOver/keyboard focus to the row that
        // opened the popover (its anchor) — the focus-return contract the
        // sheet path established.
        focusedRow = index
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
