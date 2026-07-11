// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI

/// Cmd+F search overlay. Sits above the `MainSplitView` content
/// area while open; Esc closes and returns focus to whatever
/// element the user came from.
///
/// VoiceOver story:
///   - Field announces "Search vault." on focus.
///   - Result count fires through the polite live region on every
///     state transition into `.results`.
///   - Each row's accessibility label is `"<filename>: <snippet>"`.
///     The line number was dropped from the row label in #92 item
///     1 — full_text_search no longer fetches body_text per hit, so
///     the line is derived at result-activation time (the
///     post-activation announcement still says
///     "Opened <filename>, line N: <snippet>" using the loaded
///     note's body).
///
/// Keyboard:
///   - Cmd+F (from MainSplitView) toggles the overlay.
///   - The field auto-focuses on open.
///   - Return on the field activates the top-ranked result.
///   - Tab moves focus from the field into the first result row;
///     arrow keys cycle within results.
///   - Esc closes the overlay (shortcut bound to the container, not
///     the close button — keeps the dismiss path working no matter
///     which child has focus).
struct SearchOverlay: View {
    @EnvironmentObject private var appState: AppState

    /// Focus target. `.field` while typing, `.result(idx)` once the
    /// user Tabs into the results list or hits Return on the field.
    /// `nil` means "no overlay element has focus" (briefly true on
    /// open before the .onAppear closure assigns the field).
    @FocusState private var focus: FocusTarget?

    private enum FocusTarget: Hashable {
        case field
        case result(Int)
    }

    /// #422 (F-E2): local keyDown monitor for Return-on-row. SwiftUI
    /// .plain Buttons on macOS activate with Space but not Return —
    /// the runbook's most surprising no-op ("Return on a focused
    /// result row is a no-op; Space activates"). Same local-monitor
    /// idiom as CommandPaletteView; installed on appear, removed on
    /// disappear so it can't leak past the overlay's lifetime.
    @State private var returnKeyMonitor: Any? = nil

    var body: some View {
        VStack(spacing: 0) {
            field
            // Active-scope chip: only present when a non-vault scope is
            // armed (today: reading-view tag activation). Sits between
            // the field and the results so it reads as a filter on the
            // query below it.
            if let tagName = activeTagScopeName {
                scopeChip(tagName: tagName)
            }
            Divider()
            content
        }
        .frame(maxWidth: 560)
        .background(.regularMaterial, in: RoundedRectangle(cornerRadius: 10))
        .overlay(
            RoundedRectangle(cornerRadius: 10)
                .stroke(.separator, lineWidth: 0.5)
        )
        .padding(.top, 12)
        .shadow(radius: 8, y: 4)
        .onAppear {
            // Defer focus until the next runloop tick so the
            // .focused binding has been wired up by SwiftUI.
            DispatchQueue.main.async { focus = .field }
            installReturnKeyMonitor()
            // #422 (F-E1 adjacent): closeSearchOverlay keeps
            // searchQuery (so Cmd+F → Esc → Cmd+F lands back where
            // the user was) but resets searchState to .idle — on
            // reopen the retained query sat in the field with no
            // results until the user edited it. That idle-with-text
            // shape is also the closest reproducible cousin of the
            // VO test's unconfirmed "first query sat in the field"
            // observation. Re-arm the search whenever the overlay
            // opens with a non-empty query.
            if !appState.searchQuery.isEmpty {
                appState.bumpSearchQuery()
            }
        }
        .onDisappear {
            removeReturnKeyMonitor()
        }
        // Polite live region — but only when the summary string
        // actually differs from the last announcement. Subscribing to
        // the publisher with `removeDuplicates()` (rather than
        // `.onChange(of:)`) dedupes re-assignments of an identical
        // string at the source. Fast typing produces
        // `searching → results → searching → results` cycles where
        // the same "Search returned N results" string can land
        // multiple times; without dedup VoiceOver re-announces on
        // every keystroke past the 150ms debounce (#91 item 1).
        .onReceive(appState.$searchSummary.removeDuplicates()) { summary in
            if !summary.isEmpty {
                postAccessibilityAnnouncement(summary)
            }
        }
    }

    // MARK: - Field

    private var field: some View {
        HStack(spacing: 8) {
            SlateSymbol.search.decorative
            TextField("Search vault…", text: $appState.searchQuery)
                .textFieldStyle(.plain)
                .focused($focus, equals: .field)
                .accessibilityLabel("Search vault")
                .accessibilityHint(
                    "Type to search across every note in this vault. "
                        + "Return activates the top result. "
                        + "Tab moves to the results list."
                )
                // #418 (F-A1): tabbing/arrowing through result rows
                // is silent — announce the focused row (hoisted to a
                // method; an inline closure here tripped the Swift
                // type-checker budget, the PR #263 failure mode).
                .onChange(of: focus) { _, newFocus in announceFocusedResult(newFocus) }
                .onChange(of: appState.searchQuery) {
                    appState.bumpSearchQuery()
                }
                .onSubmit {
                    // Return on the field activates the top result
                    // if there is one — matches the acceptance
                    // criteria of "Return activates" + "Tab cycles
                    // into the results list."
                    if let first = firstResultHit() {
                        appState.openSearchResult(first)
                    }
                }
            // Clear-text affordance — core search-field anatomy
            // (search-fields.md: "Search icon, Clear button,
            // placeholder"). Previously the ONLY ×-glyph here closed
            // the whole overlay, so clicking what reads as "clear"
            // lost the user's context. Shown only with a query, like
            // the native NSSearchField clear button.
            if !appState.searchQuery.isEmpty {
                Button {
                    appState.searchQuery = ""
                    focus = .field
                } label: {
                    SlateSymbol.clearSearch.decorative
                        .foregroundStyle(Tokens.ColorRole.textSecondary)
                        .frame(minWidth: 28, minHeight: 28)
                        .contentShape(Rectangle())
                }
                .buttonStyle(.plain)
                .accessibilityLabel("Clear search text")
                .accessibilityHint("Empties the search field and keeps focus in it.")
            }
            Button {
                appState.closeSearchOverlay()
            } label: {
                // Plain xmark (the closeTab dismiss glyph), NOT the
                // circle-fill clearSearch glyph the clear button now
                // owns — the two adjacent actions must not share a
                // shape. Decorative: this button carries its own AX
                // label.
                SlateSymbol.closeTab.decorative
                    // The comment below promises a "properly-sized"
                    // button; a bare glyph in a .plain button renders
                    // ~16pt with no padding. Pin the HIG macOS
                    // DEFAULT click target (28×28pt; HIG minimum 20,
                    // WCAG 2.5.8 minimum 24 — 28 clears all three)
                    // so the promise is implemented, not aspirational.
                    .frame(minWidth: 28, minHeight: 28)
                    .contentShape(Rectangle())
            }
            .buttonStyle(.plain)
            .accessibilityLabel("Close search")
            .accessibilityHint("Closes the search overlay and returns to the previous view.")
            // Esc dismiss. macOS `.keyboardShortcut` is
            // window-scoped, so this fires no matter which view in
            // the overlay has focus (field, result row, etc.) —
            // the audit-#88-B6 concern about focus-routing fragility
            // was unfounded for SwiftUI's window-level shortcut
            // resolution. Keeping the binding on a visible,
            // properly-sized button avoids the WCAG 2.5.8 0x0 hit-
            // target warning a hidden ancillary button would trip.
            .keyboardShortcut(.escape, modifiers: [])
        }
        .padding(.horizontal, 12)
        .padding(.vertical, 8)
    }

    // MARK: - Scope chip

    /// The tag name when a `.tag` scope is armed, else `nil`. Drives
    /// the chip's presence; a `switch` (not `if case` in the view) so
    /// a future scope surfaces here explicitly rather than silently.
    private var activeTagScopeName: String? {
        switch appState.searchScope {
        case .tag(let name): return name
        default: return nil
        }
    }

    /// Dismissible "Tag: <name>" chip shown while a tag scope filters
    /// the search. All colors come from `Tokens.ColorRole` (APCA-gated
    /// roles only — no literals): `accentText` label + `accentFill`
    /// wash so the chip reads as an active filter, `textSecondary` for
    /// the ⓧ. The chip is one AX element ("Filtered to tag <name>");
    /// the focusable clear button ("Clear tag filter") drops back to
    /// vault scope.
    private func scopeChip(tagName: String) -> some View {
        HStack(spacing: Tokens.Spacing.sm) {
            HStack(spacing: Tokens.Spacing.xs) {
                Text("Tag: \(tagName)")
                    .font(Tokens.Typography.caption)
                    .foregroundStyle(Tokens.ColorRole.accentText)
                Button {
                    appState.clearSearchScope()
                } label: {
                    SlateSymbol.clearSearch.decorative
                        .foregroundStyle(Tokens.ColorRole.textSecondary)
                        // HIG macOS default click target, 28×28pt
                        // (same as the overlay close button above).
                        .frame(minWidth: 28, minHeight: 28)
                        .contentShape(Rectangle())
                }
                .buttonStyle(.plain)
                .accessibilityLabel("Clear tag filter")
                .accessibilityHint("Removes the tag filter and searches the whole vault.")
            }
            .padding(.horizontal, Tokens.Spacing.sm)
            .padding(.vertical, Tokens.Spacing.xs)
            .background(
                Tokens.ColorRole.accentFill.opacity(0.18),
                in: RoundedRectangle(cornerRadius: Tokens.Radius.chip)
            )
            // One AX element so VoiceOver reads the filter as a unit;
            // the clear button stays independently focusable/activatable.
            .accessibilityElement(children: .contain)
            .accessibilityLabel("Filtered to tag \(tagName)")

            Spacer(minLength: 0)
        }
        .padding(.horizontal, Tokens.Spacing.md)
        .padding(.bottom, Tokens.Spacing.sm)
    }

    // MARK: - Content

    @ViewBuilder
    private var content: some View {
        switch appState.searchState {
        case .idle:
            idleState
        case .searching:
            searchingState
        case .results(let rows, _):
            if rows.isEmpty {
                emptyResultsState
            } else {
                resultsList(rows)
            }
        case .error(let message):
            errorState(message)
        }
    }

    private var idleState: some View {
        Text("Type to search.")
            .font(.callout)
            .foregroundStyle(.secondary)
            .padding()
            .accessibilityLabel("Type to search.")
    }

    private var searchingState: some View {
        HStack(spacing: 8) {
            ProgressView()
                .controlSize(.small)
            Text("Searching…")
                .foregroundStyle(.secondary)
        }
        .padding()
        .frame(maxWidth: .infinity, alignment: .leading)
        .accessibilityElement(children: .combine)
        .accessibilityLabel("Searching.")
    }

    private var emptyResultsState: some View {
        Text("No results.")
            .font(.callout)
            .foregroundStyle(.secondary)
            .padding()
            .accessibilityLabel("No results.")
    }

    private func errorState(_ message: String) -> some View {
        VStack(alignment: .leading, spacing: 4) {
            Text("Search error")
                .font(.headline)
                .accessibilityAddTraits(.isHeader)
            Text(message)
                .font(.callout)
                .foregroundStyle(.secondary)
        }
        .padding()
        .frame(maxWidth: .infinity, alignment: .leading)
    }

    private func resultsList(_ rows: [QueryHit]) -> some View {
        VStack(alignment: .leading, spacing: 0) {
            // Visible twin of the "Search returned N results"
            // live-region announcement — sighted users now get the
            // tally the AT side always had. Hidden from AX: the
            // announcement is the audio source of truth, and a second
            // speaking element would double-announce every query.
            if !appState.searchSummary.isEmpty {
                Text(appState.searchSummary)
                    .font(Tokens.Typography.caption)
                    .foregroundStyle(Tokens.ColorRole.textSecondary)
                    .padding(.horizontal, 12)
                    .padding(.top, Tokens.Spacing.sm)
                    .accessibilityHidden(true)
            }
            ScrollView {
                VStack(alignment: .leading, spacing: 4) {
                    // Stable id on `.path` rather than offset — FTS5
                    // returns one hit per file, so paths are unique
                    // within a result set, and using a stable id keeps
                    // SwiftUI from re-creating row views when results
                    // reorder by score after a query change.
                    ForEach(Array(rows.enumerated()), id: \.element.path) { idx, hit in
                        row(for: hit, at: idx)
                    }
                }
                .padding(.horizontal, 8)
                .padding(.vertical, 4)
            }
            .frame(maxHeight: 320)
        }
    }

    /// FTS5 brackets each matched term in STX/ETX (`\u{2}`/`\u{3}`)
    /// markers. Convert the marked spans to a semibold, primary-color
    /// run so sighted users can see WHAT matched — previously the
    /// markers were stripped and the emphasis silently discarded
    /// (the row comment even called them "useful for visual
    /// emphasis"). Unmarked runs carry no explicit style, so the
    /// view-level `.caption`/`.secondary` modifiers keep styling
    /// them. Static + pure so the marker parse is unit-testable.
    static func emphasizedSnippet(_ raw: String) -> AttributedString {
        var out = AttributedString()
        var bold = false
        var run = ""
        func flush() {
            guard !run.isEmpty else { return }
            var piece = AttributedString(run)
            if bold {
                piece.font = Tokens.Typography.caption.weight(.semibold)
                piece.foregroundColor = Tokens.ColorRole.textPrimary
            }
            out += piece
            run = ""
        }
        for ch in raw {
            if ch == "\u{2}" {
                flush()
                bold = true
            } else if ch == "\u{3}" {
                flush()
                bold = false
            } else {
                run.append(ch)
            }
        }
        flush()
        return out
    }

    private func row(for hit: QueryHit, at index: Int) -> some View {
        Button {
            appState.openSearchResult(hit)
        } label: {
            VStack(alignment: .leading, spacing: 2) {
                Text(filename(for: hit.path))
                    .font(.callout.weight(.semibold))
                    .foregroundStyle(.primary)
                // No lineLimit: at large Dynamic Type the snippet was
                // truncating below the threshold WCAG 1.4.4 needs for
                // sighted users (the `.help()` tooltip helps mouse
                // users but not keyboard-only ones). Let it wrap; the
                // panel's scroll view absorbs the height. The STX/ETX
                // hit markers become semibold emphasis on the matched
                // term (`emphasizedSnippet`).
                Text(Self.emphasizedSnippet(hit.snippet))
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }
            .frame(maxWidth: .infinity, alignment: .leading)
            .padding(.vertical, 4)
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        // Per-row focus binding so Tab from the field cycles into
        // the result list. The first Tab press lands on `.result(0)`;
        // subsequent Tabs walk via the system focus-traversal logic
        // (Return activates the focused row's button action).
        .focused($focus, equals: .result(index))
        // VoiceOver row label per spec: "<filename>, line <N>: <snippet>".
        // We strip the STX/ETX hit markers from the snippet for the
        // audio side — they're useful for visual emphasis but a
        // screen reader doesn't need them.
        .accessibilityLabel(rowAccessibilityLabel(for: hit))
        .help(hit.path)
    }

    private func rowAccessibilityLabel(for hit: QueryHit) -> String {
        let cleanSnippet = hit.snippet
            .replacingOccurrences(of: "\u{2}", with: "")
            .replacingOccurrences(of: "\u{3}", with: "")
        return "\(filename(for: hit.path)): \(cleanSnippet)"
    }

    /// #418 (F-A1): speak the focused result row. The focus engine
    /// moves through these custom .plain buttons silently; announce
    /// the filename — the full row label (with snippet) stays on the
    /// element for VO-cursor interaction without forcing every focus
    /// hop through a paragraph of snippet speech.
    private func announceFocusedResult(_ newFocus: FocusTarget?) {
        guard case .result(let idx) = newFocus else { return }
        guard case .results(let rows, _) = appState.searchState,
            rows.indices.contains(idx)
        else { return }
        postAccessibilityAnnouncement(
            "Selected: \(filename(for: rows[idx].path))",
            priority: .medium
        )
    }

    /// #422 (F-E2): Return activates the focused result row, with
    /// parity to Space and to Return-on-the-field. Only intercepts
    /// the bare Return key while a result row has focus — the field
    /// keeps its native onSubmit, IME composition passes through
    /// untouched (same guard as the palette monitor), and every
    /// other key falls through.
    private func installReturnKeyMonitor() {
        guard returnKeyMonitor == nil else { return }
        returnKeyMonitor = NSEvent.addLocalMonitorForEvents(matching: .keyDown) { event in
            if let editor = event.window?.firstResponder as? NSTextView,
                editor.hasMarkedText()
            {
                return event
            }
            // 36 = Return, 76 = keypad Enter (field onSubmit accepts
            // both; row parity should too — red-team F5).
            guard event.keyCode == 36 || event.keyCode == 76,
                event.modifierFlags.intersection(.deviceIndependentFlagsMask).isEmpty,
                // Red-team F2: the monitor is app-wide; with the
                // command palette sheet up over an open search
                // overlay, a Return meant for the palette field
                // would otherwise be hijacked into a result
                // activation (the parent window keeps its
                // firstResponder during sheets).
                !appState.isCommandPaletteOpen,
                case .result(let idx) = focus,
                case .results(let rows, _) = appState.searchState,
                rows.indices.contains(idx)
            else { return event }
            appState.openSearchResult(rows[idx])
            return nil
        }
    }

    private func removeReturnKeyMonitor() {
        if let monitor = returnKeyMonitor {
            NSEvent.removeMonitor(monitor)
            returnKeyMonitor = nil
        }
    }

    private func filename(for path: String) -> String {
        (path as NSString).lastPathComponent
    }

    /// First hit in the current results list, if any. Used to wire
    /// the field's Return-key submit action to the top-ranked
    /// result without duplicating the row's openSearchResult call
    /// site.
    private func firstResultHit() -> QueryHit? {
        if case .results(let rows, _) = appState.searchState {
            return rows.first
        }
        return nil
    }
}
