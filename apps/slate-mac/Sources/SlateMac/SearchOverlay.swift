// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI

/// Vault-search overlay (⇧⌘F since #874 — bare ⌘F is now find-in-note).
/// Sits above the `MainSplitView` content area while open. It is laid
/// inline, not presented as a sheet, so AppKit restores nothing when it
/// closes; Esc (the close button, and ⇧⌘F while open) gives keyboard
/// focus back to the element that had it when the overlay opened, if
/// that element is still in the window (`AppState.dismissSearchOverlay`,
/// contract 29 SD-2). Opening a result does not, and neither does a
/// close while the command palette or a sheet owns focus.
///
/// VoiceOver story:
///   - Field announces its label, "Search vault", on focus.
///   - Result count fires through the polite live region on every
///     state transition into `.results`.
///   - Each row's accessibility label is `"<filename>: <snippet>"`.
///     The line number was dropped from the row label in #92 item
///     1 — full_text_search no longer fetches body_text per hit, so
///     the line is derived at result-activation time (the
///     post-activation announcement still says
///     "Opened <filename>, line N: <snippet>" using the loaded
///     note's body).
///   - A row that takes focus, by Tab or an arrow, speaks its
///     filename (a recent row, its query): `announceFocusedResult`
///     (#418 F-A1).
///
/// Keyboard:
///   - ⇧⌘F (Edit ▸ Search Vault…) toggles the overlay.
///   - The field auto-focuses on open.
///   - Before typing, the idle state offers recent vault searches
///     (#876) as re-runnable rows, with a Clear affordance.
///   - Return on the field activates the top-ranked result.
///   - Down moves focus from the field to the first row (a result, or
///     a recent search in the idle state) and on down the list; Up
///     moves back, and from the first row to the field (SD-1). The
///     list does not wrap. Tab reaches the rows too, after the field's
///     Clear and Close buttons. Both need Keyboard navigation (Full
///     Keyboard Access) on: a plain button takes keyboard focus only
///     then, so with it off the arrows keep their field meaning
///     (#1377).
///   - Return or keypad Enter on a focused row opens it (a recent row
///     re-runs its search); ⌘Return opens a result row in a new tab
///     (plan 41 D2). ⌘Return anywhere else is not the overlay's.
///   - Esc closes the overlay: a window-scoped shortcut on the close
///     button, so it fires whichever child has focus.
struct SearchOverlay: View {
    @EnvironmentObject private var appState: AppState

    /// Focus target. `.field` while typing, `.result(idx)` (or, in the
    /// idle state, `.recent(idx)`) once Tab or an arrow moves into the
    /// list. `nil` means "no element with a focus binding has focus":
    /// briefly on open, before the .onAppear closure assigns the
    /// field, and while one of the field's buttons has it.
    @FocusState private var focus: FocusTarget?

    enum FocusTarget: Hashable {
        case field
        case result(Int)
        // #876 red-team: the idle-state recent rows need the same
        // focus scaffolding the result rows have, or a keyboard-only
        // user (no VoiceOver, no Full Keyboard Access) can't Tab to a
        // recent or Return-run it. (Measured in #1113: a plain button
        // takes keyboard focus only with Keyboard navigation on, so the
        // scaffolding reaches these rows only then — #1377.)
        case recent(Int)
    }

    /// The overlay's local keyDown monitor: Return on a focused row
    /// (#422 F-E2 — SwiftUI .plain Buttons on macOS activate with
    /// Space but not Return, the runbook's most surprising no-op),
    /// ⌘Return on a focused row (D2), and the Up/Down walk (SD-1,
    /// #1113). Same local-monitor idiom as CommandPaletteView;
    /// installed on appear, removed on disappear so it can't leak past
    /// the overlay's lifetime. Its decisions are `Self.route(...)`.
    @State private var keyMonitor: Any? = nil

    /// The window the overlay is laid in. A local monitor sees every
    /// window's keys, so it claims a key only when the key arrived in
    /// this one — not in a sheet over it (the palette, Quick Open, a
    /// dialog) or in another window, where the overlay's focus still
    /// reads as the field or a row.
    @State private var overlayWindow = OverlayWindowRef()

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
        .background {
            OverlayWindowReader(ref: overlayWindow)
                .frame(width: 0, height: 0)
                .accessibilityHidden(true)
        }
        .onAppear {
            // Defer focus until the next runloop tick so the
            // .focused binding has been wired up by SwiftUI.
            DispatchQueue.main.async { focus = .field }
            installKeyMonitor()
            // #422 (F-E1 adjacent): closeSearchOverlay keeps
            // searchQuery (so ⇧⌘F → Esc → ⇧⌘F lands back where
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
            removeKeyMonitor()
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
            if !summary.isEmpty, let event = appState.searchAnnouncement {
                postAccessibilityAnnouncement(event)
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
                    // into the results list." The key monitor leaves
                    // Return on the field to this submit.
                    appState.openTopSearchResult()
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
                // The user's close (this button and its Esc shortcut):
                // close, then give focus back to where it was at open.
                appState.dismissSearchOverlay()
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

    /// Idle (pre-typing) state. #876 (searching.md:37 — "show recent
    /// searches … before and during typing"): when the vault has
    /// remembered queries, surface them as re-runnable rows with a Clear
    /// affordance; otherwise keep the plain "Type to search." hint.
    @ViewBuilder
    private var idleState: some View {
        if appState.searchRecents.isEmpty {
            Text("Type to search.")
                .font(.callout)
                .foregroundStyle(.secondary)
                .padding()
                .accessibilityLabel("Type to search.")
        } else {
            recentSearchesList
        }
    }

    /// "Recent Searches" section: a header with a Clear button, then a
    /// scrollable list of selectable rows that re-run the query. Every
    /// element is VoiceOver-labeled; the section is one AX container so
    /// the list reads as a unit while each row stays independently
    /// focusable (the results-list pattern above).
    private var recentSearchesList: some View {
        VStack(alignment: .leading, spacing: 0) {
            HStack(spacing: Tokens.Spacing.sm) {
                Text("Recent Searches")
                    .font(Tokens.Typography.caption)
                    .foregroundStyle(Tokens.ColorRole.textSecondary)
                    .accessibilityAddTraits(.isHeader)
                Spacer(minLength: 0)
                Button {
                    appState.clearSearchRecents()
                    focus = .field
                } label: {
                    Text("Clear")
                        .font(Tokens.Typography.caption)
                        .foregroundStyle(Tokens.ColorRole.accentText)
                        // HIG macOS default click target (28pt tall,
                        // clears the WCAG 2.5.8 24pt minimum) — matches
                        // the overlay's other bare-label buttons.
                        .frame(minHeight: 28)
                        .contentShape(Rectangle())
                }
                .buttonStyle(.plain)
                .accessibilityLabel("Clear recent searches")
                .accessibilityHint("Forgets every remembered search in this vault.")
            }
            .padding(.horizontal, 12)
            .padding(.top, Tokens.Spacing.sm)

            ScrollView {
                VStack(alignment: .leading, spacing: 4) {
                    // Stable id on the query string itself — recents are
                    // deduped, so each is unique within the list. Indexed
                    // so each row binds a `.recent(index)` focus target
                    // (Tab-into + Return-activate), mirroring result rows.
                    ForEach(Array(appState.searchRecents.enumerated()), id: \.element) {
                        index, query in
                        recentRow(query)
                            .focused($focus, equals: .recent(index))
                    }
                }
                .padding(.horizontal, 8)
                .padding(.vertical, 4)
            }
            .frame(maxHeight: 260)
        }
        // One AX container so VoiceOver reads the section as a unit; the
        // rows and Clear button stay independently focusable/activatable.
        .accessibilityElement(children: .contain)
    }

    /// A single recent-query row. Activating it re-runs the query
    /// through the normal search path (`runRecentSearch`) and returns
    /// focus to the field so the user can refine.
    private func recentRow(_ query: String) -> some View {
        Button {
            appState.runRecentSearch(query)
            focus = .field
        } label: {
            HStack(spacing: 8) {
                // Decorative: the row's AX label already names it.
                SlateSymbol.search.decorative
                    .foregroundStyle(Tokens.ColorRole.textSecondary)
                Text(query)
                    .font(.callout)
                    .foregroundStyle(.primary)
                Spacer(minLength: 0)
            }
            .frame(maxWidth: .infinity, alignment: .leading)
            .padding(.vertical, 4)
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .accessibilityLabel("Recent search: \(query)")
        .accessibilityHint("Runs this search again.")
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
        // Per-row focus binding: Tab from the field reaches the rows
        // (after the field's Clear and Close buttons), and Down / Up
        // set it directly (SD-1, the key monitor). Return on the
        // focused row opens it through the key monitor (#422 F-E2).
        .focused($focus, equals: .result(index))
        // VoiceOver row label: "<filename>: <snippet>". No line number
        // since #92 item 1 (the post-activation announcement carries
        // it). The STX/ETX hit markers are stripped for the audio
        // side; they drive the visual emphasis only.
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
    ///
    /// #876 red-team: the idle-state recent rows reuse the identical
    /// silent `.plain`-button `.focused(...)` scaffolding, so they need
    /// the same on-focus announcement — without it a VoiceOver user
    /// Tabbing through recents hears nothing (unlike the result rows they
    /// mirror). Announce the query itself.
    private func announceFocusedResult(_ newFocus: FocusTarget?) {
        if case .result(let idx) = newFocus {
            guard case .results(let rows, _) = appState.searchState,
                rows.indices.contains(idx)
            else { return }
            postAccessibilityAnnouncement(
                .rowSelected(name: filename(for: rows[idx].path)))
            return
        }
        if case .recent(let idx) = newFocus,
            appState.searchRecents.indices.contains(idx)
        {
            postAccessibilityAnnouncement(
                .recentSearchFocused(query: appState.searchRecents[idx]))
        }
    }

    /// The key monitor (#422 F-E2, #1113). Every decision is
    /// `Self.route(...)`; this closure only gathers what the monitor
    /// knows and applies the route. A moved focus speaks through
    /// `announceFocusedResult` (the field's `.onChange(of: focus)`), so
    /// each arrow move announces its row — the S8 rule.
    private func installKeyMonitor() {
        guard keyMonitor == nil else { return }
        keyMonitor = NSEvent.addLocalMonitorForEvents(matching: .keyDown) { event in
            let hostWindow = overlayWindow.window
            let context = KeyContext(
                focus: focus,
                rows: rowList,
                rowsTakeFocus: NSApp.isFullKeyboardAccessEnabled,
                isInOverlayWindow: hostWindow != nil && event.window === hostWindow,
                isCommandPaletteOpen: appState.isCommandPaletteOpen,
                hasMarkedText: (event.window?.firstResponder as? NSTextView)?
                    .hasMarkedText() == true)
            switch Self.route(
                keyCode: event.keyCode, modifierFlags: event.modifierFlags, in: context)
            {
            case .passThrough:
                return event
            case .moveFocus(let target):
                focus = target
                return nil
            case .hold:
                return nil
            case .openResult(let index):
                // The #422 F-E2 path, and D2's ⌘Return: the open target
                // comes from the live event (`openTargetFromCurrentEvent`).
                guard case .results(let rows, _) = appState.searchState,
                    rows.indices.contains(index)
                else { return event }
                appState.openSearchResult(rows[index])
                return nil
            case .runRecent(let index):
                // #876 red-team: a focused RECENT row re-runs its query.
                guard appState.searchRecents.indices.contains(index) else { return event }
                appState.runRecentSearch(appState.searchRecents[index])
                focus = .field
                return nil
            }
        }
    }

    private func removeKeyMonitor() {
        if let monitor = keyMonitor {
            NSEvent.removeMonitor(monitor)
            keyMonitor = nil
        }
    }

    /// The rows on screen, as the key monitor sees them: the result
    /// rows, the idle state's recent rows (`idleState` lists them
    /// whenever there are any), or none while searching or on an error.
    private var rowList: RowList {
        switch appState.searchState {
        case .results(let rows, _): return .results(count: rows.count)
        case .idle: return .recents(count: appState.searchRecents.count)
        case .searching, .error: return .none
        }
    }

    private func filename(for path: String) -> String {
        (path as NSString).lastPathComponent
    }
}

// MARK: - Key routing (#1113)

extension SearchOverlay {
    /// What the key monitor does with one keyDown.
    enum KeyRoute: Equatable {
        /// Not the overlay's key: the event continues untouched.
        case passThrough
        /// Consume the key and move keyboard focus (SD-1). The focus
        /// change is what speaks (`announceFocusedResult`).
        case moveFocus(FocusTarget)
        /// Consume the key and leave focus where it is: Down on the
        /// last row (the list does not wrap).
        case hold
        /// Open the result row at this index: Return, keypad Enter, or
        /// ⌘Return (D2) on a focused result row.
        case openResult(Int)
        /// Re-run the recent search at this index: Return, keypad
        /// Enter, or ⌘Return on a focused recent row.
        case runRecent(Int)
    }

    /// The list below the field.
    enum RowList: Equatable {
        /// The results panel with this many rows (zero is "No results.").
        case results(count: Int)
        /// The idle state's recent searches (zero is "Type to search.").
        case recents(count: Int)
        /// Searching, or an error: no rows on screen.
        case none
    }

    /// What the monitor knows when a key arrives.
    struct KeyContext: Equatable {
        var focus: FocusTarget?
        var rows: RowList
        /// A plain button takes keyboard focus only with Keyboard
        /// navigation (Full Keyboard Access) on —
        /// `NSApp.isFullKeyboardAccessEnabled`. Without it the rows
        /// cannot take focus, so the arrows stay the field's.
        var rowsTakeFocus: Bool
        /// The key arrived in the overlay's own window (not a sheet
        /// over it, not another window).
        var isInOverlayWindow: Bool
        var isCommandPaletteOpen: Bool
        /// An input method is composing in the key's window.
        var hasMarkedText: Bool
    }

    /// Carbon virtual key codes (`kVK_Return`, `kVK_ANSI_KeypadEnter`,
    /// `kVK_DownArrow`, `kVK_UpArrow`), stable since OS X 10.0. The
    /// field's onSubmit accepts both Return and keypad Enter, and row
    /// activation matches it (red-team F5).
    nonisolated static let returnKeyCode: UInt16 = 36
    nonisolated static let keypadEnterKeyCode: UInt16 = 76
    nonisolated static let downArrowKeyCode: UInt16 = 125
    nonisolated static let upArrowKeyCode: UInt16 = 126

    /// Flags that never change what a key means: Caps Lock, and the
    /// function and numeric-pad bits AppKit sets on the arrow keys and
    /// keypad Enter themselves (the sidebar's `TreeOpenSelectedKey`
    /// strips the same three).
    nonisolated static let modifierNoise: NSEvent.ModifierFlags = [
        .capsLock, .function, .numericPad,
    ]

    /// Decide what the monitor does with a keyDown. Pure, so
    /// `SearchOverlayKeyRoutingTests` pins every decision.
    ///
    /// - An input method composing owns every key (checked first: the
    ///   candidate window takes Return and the arrows); a key typed in
    ///   another window or a sheet is not the overlay's; and while the
    ///   command palette is up nothing is claimed (red-team F2 — the
    ///   parent window keeps its first responder during sheets, so the
    ///   overlay's focus can still read as a row).
    /// - Return and keypad Enter: bare, or exactly ⌘ (D2), and only on
    ///   a focused row. Return on the field stays the field's (its
    ///   onSubmit opens the top result); ⌘Return anywhere else passes
    ///   through untouched.
    /// - Down and Up: bare only, so Control-Option-arrows (VoiceOver),
    ///   Shift (extend a selection) and ⌘ (move the caret) pass
    ///   through; and only where rows can take focus. Down from the
    ///   field to the first row and down the list, holding on the last
    ///   row; Up back, from the first row to the field. Up on the field
    ///   passes through (the caret moves to the start).
    nonisolated static func route(
        keyCode: UInt16,
        modifierFlags: NSEvent.ModifierFlags,
        in context: KeyContext
    ) -> KeyRoute {
        guard !context.hasMarkedText, context.isInOverlayWindow,
            !context.isCommandPaletteOpen
        else { return .passThrough }
        let modifiers = modifierFlags
            .intersection(.deviceIndependentFlagsMask)
            .subtracting(modifierNoise)
        switch keyCode {
        case returnKeyCode, keypadEnterKeyCode:
            guard modifiers.isEmpty || modifiers == .command else { return .passThrough }
            switch (context.focus, context.rows) {
            case (.result(let index)?, .results(let count)) where (0..<count).contains(index):
                return .openResult(index)
            case (.recent(let index)?, .recents(let count)) where (0..<count).contains(index):
                return .runRecent(index)
            default:
                return .passThrough
            }
        case downArrowKeyCode, upArrowKeyCode:
            guard modifiers.isEmpty, context.rowsTakeFocus else { return .passThrough }
            return arrowRoute(down: keyCode == downArrowKeyCode, in: context)
        default:
            return .passThrough
        }
    }

    private nonisolated static func arrowRoute(down: Bool, in context: KeyContext) -> KeyRoute {
        let count: Int
        let row: (Int) -> FocusTarget
        // The focused row's index; nil while the field has focus.
        let current: Int?
        switch (context.rows, context.focus) {
        case (.results(let rows), .field?) where rows > 0:
            (count, row, current) = (rows, FocusTarget.result, nil)
        case (.results(let rows), .result(let index)?) where (0..<rows).contains(index):
            (count, row, current) = (rows, FocusTarget.result, index)
        case (.recents(let rows), .field?) where rows > 0:
            (count, row, current) = (rows, FocusTarget.recent, nil)
        case (.recents(let rows), .recent(let index)?) where (0..<rows).contains(index):
            (count, row, current) = (rows, FocusTarget.recent, index)
        default:
            return .passThrough
        }
        guard let current else {
            return down ? .moveFocus(row(0)) : .passThrough
        }
        if down {
            return current + 1 < count ? .moveFocus(row(current + 1)) : .hold
        }
        return current == 0 ? .moveFocus(.field) : .moveFocus(row(current - 1))
    }
}

/// The window the search overlay is laid in, held weakly.
final class OverlayWindowRef {
    weak var window: NSWindow?
}

/// A zero-size view that reports the window it is placed in, so the
/// overlay's app-wide key monitor can tell its own window's keys from a
/// sheet's or another window's.
private struct OverlayWindowReader: NSViewRepresentable {
    let ref: OverlayWindowRef

    func makeNSView(context: Context) -> ReaderView {
        let view = ReaderView()
        view.ref = ref
        return view
    }

    func updateNSView(_ view: ReaderView, context: Context) {
        view.ref = ref
        ref.window = view.window
    }

    final class ReaderView: NSView {
        var ref: OverlayWindowRef?

        override var intrinsicContentSize: NSSize { .zero }

        override func viewDidMoveToWindow() {
            super.viewDidMoveToWindow()
            ref?.window = window
        }
    }
}

// MARK: - Focus restore (SD-2, #1113)

/// Where keyboard focus was when the search overlay opened: the window
/// and its first responder, both held weakly so the overlay never keeps
/// a closed window or a removed view alive. `AppState` captures it at
/// open and restores it on the user's close (`dismissSearchOverlay`).
struct SearchOverlayFocusOrigin {
    private(set) weak var window: NSWindow?
    private(set) weak var responder: NSView?

    /// Capture from the key window at open. When the key window is a
    /// sheet (Search Vault run from the command palette), the overlay is
    /// laid in the window the sheet is attached to, and that window
    /// keeps its first responder while the sheet is up: capture there.
    /// A field editor stands for the text field it is editing. The
    /// window itself (focus nowhere) is no origin: there is nothing to
    /// give back.
    @MainActor static func capture(keyWindow: NSWindow?) -> SearchOverlayFocusOrigin? {
        guard let keyWindow else { return nil }
        let window = keyWindow.sheetParent ?? keyWindow
        var responder = window.firstResponder
        if let editor = responder as? NSTextView, editor.isFieldEditor,
            let field = editor.delegate as? NSView
        {
            responder = field
        }
        guard let view = responder as? NSView, view.window === window else { return nil }
        return SearchOverlayFocusOrigin(window: window, responder: view)
    }

    /// Whether the captured element can take focus back: it is still
    /// in the window it was captured in.
    @MainActor var isRestorable: Bool {
        guard let window, let responder else { return false }
        return responder.window === window
    }

    /// Give focus back to the captured element, and only to it: an
    /// element that has left its window is not replaced with another.
    @MainActor @discardableResult func restore() -> Bool {
        guard isRestorable, let window, let responder else { return false }
        return window.makeFirstResponder(responder)
    }
}
