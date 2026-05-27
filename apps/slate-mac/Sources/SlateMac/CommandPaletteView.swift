// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import SwiftUI

/// Modal command palette — Milestone Q.
///
/// `#313` shipped the shell. `#314` populated the registry from the
/// menu surfaces. `#315` (this PR) wires the fuzzy filter, arrow-
/// key navigation, Enter-to-invoke, and the ActionFailed
/// announcement path. `#316` is section grouping / recents / live
/// regions.
///
/// ## Behaviour
///
/// - Opens via `⌘⇧P` (menu wiring in `SlateMacApp`, gated on
///   `isVaultOpen`); the search field auto-focuses on appear.
/// - Typing filters the command list via a subsequence-with-boost
///   fuzzy matcher (see `CommandPaletteModel.fuzzyScore`).
/// - Arrow ↑ / ↓ moves selection with wrap. Modified arrows
///   (`Shift+↓`, `Cmd+↓`, `Ctrl+Option+↓` for VoiceOver Quick Nav,
///   etc.) pass through so text-editing chords and VoiceOver
///   keystrokes keep working.
/// - `Enter` invokes the selected command. On success the palette
///   dismisses; on `ActionFailed` / `UnknownId` it **stays open**
///   and posts an assertive VoiceOver announcement.
/// - `Esc` dismisses (via `.onExitCommand`).
/// - Hover updates selection, but mouse-jitter doesn't yank it
///   away from the user's keyboard-driven choice (debounced).
///
/// ## Colours
///
/// Background is `controlBackgroundColor`; text uses `labelColor`
/// / `secondaryLabelColor`; selected row uses
/// `selectedContentBackgroundColor` + `selectedMenuItemTextColor`.
/// All pairings pass APCA `|Lc| > 75` (verified by
/// `CommandPaletteViewTests`).
struct CommandPaletteView: View {
    @EnvironmentObject private var appState: AppState
    @Environment(\.dismiss) private var dismiss
    @Environment(\.accessibilityReduceMotion) private var reduceMotion

    @StateObject private var model = CommandPaletteModel()

    @State private var arrowKeyMonitor: Any? = nil

    /// Timestamp of the most recent arrow-key selection change.
    /// Hover-update only fires when the mouse moves AFTER this —
    /// prevents stationary-cursor flicker from yanking selection
    /// back to the row under the pointer while the user is
    /// arrow-navigating.
    @State private var lastKeyboardNavAt: Date = .distantPast

    /// True between `.onAppear` and the first user-driven selection
    /// change. Suppresses the polite `Selected: <label>` VoiceOver
    /// announcement at open time — with Recent at the top of the
    /// list (#316) the initial selection is non-alphabetical and
    /// announcing it before the user does anything is noise.
    @State private var isInitialLoad: Bool = true

    @FocusState private var searchFocused: Bool

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            searchField
            Divider()
            results
        }
        .frame(minWidth: 560, idealWidth: 560, minHeight: 360, idealHeight: 360)
        .background(Color(nsColor: .controlBackgroundColor))
        .onExitCommand { dismiss() }
        .onAppear {
            searchFocused = true
            isInitialLoad = true
            model.loadCommands(
                appState.commandRegistry.list(),
                recents: appState.commandPaletteRecents
            )
            installArrowKeyMonitor()
        }
        .onDisappear {
            removeArrowKeyMonitor()
        }
        .onChange(of: model.query) { _ in
            model.handleQueryChange()
        }
        .onChange(of: model.selectedID) { newID in
            // Suppress the initial selection announcement at open
            // time — with Recent at the top the first row is non-
            // alphabetical and announcing it before the user
            // touches anything is noise. First user-driven change
            // clears the flag.
            guard !isInitialLoad else {
                isInitialLoad = false
                return
            }
            // Polite VoiceOver announcement so screen-reader users
            // hear what arrow keys are doing — the .isSelected
            // trait change on a non-focused row doesn't trigger
            // VO speech on its own.
            guard let newID,
                  let command = model.displayOrder.first(where: { $0.id == newID })
            else { return }
            postAccessibilityAnnouncement(
                "Selected: \(command.label)",
                priority: .low
            )
        }
        .onChange(of: model.pendingAnnouncement) { announcement in
            guard let announcement, !announcement.isEmpty else { return }
            postAccessibilityAnnouncement(announcement, priority: .high)
            model.clearPendingAnnouncement()
        }
        .onChange(of: model.filterAnnouncement) { announcement in
            // Filter-change announcement (#316). `.medium` priority
            // (default) so per-keystroke announcements coalesce
            // gracefully — VoiceOver supersedes the in-flight one
            // when a new one arrives. `.high` would interrupt
            // mid-word and produce "1—5—12 commands matching"
            // garbage at typing speed. Matches the
            // `SearchOverlay.swift` precedent.
            guard let announcement, !announcement.isEmpty else { return }
            postAccessibilityAnnouncement(announcement, priority: .medium)
            model.clearFilterAnnouncement()
        }
    }

    // MARK: - Search field

    private var searchField: some View {
        HStack(spacing: 8) {
            Image(systemName: "magnifyingglass")
                .foregroundStyle(Color(nsColor: .secondaryLabelColor))
                .accessibilityHidden(true)
            TextField("Search commands", text: $model.query)
                .textFieldStyle(.plain)
                .font(.title3)
                .focused($searchFocused)
                .accessibilityLabel("Search commands")
                .accessibilityHint(
                    "Arrow up and down to move selection. Return runs the selected command."
                )
                .onSubmit(invokeSelected)
        }
        .padding(.horizontal, 16)
        .padding(.vertical, 12)
    }

    // MARK: - Results

    @ViewBuilder
    private var results: some View {
        let sections = model.sections
        if sections.isEmpty {
            if model.commands.isEmpty {
                emptyState
            } else {
                noMatchesState
            }
        } else {
            // List + Section + header is the canonical SwiftUI
            // pattern for grouped content with VoiceOver-recognised
            // section headings (matches `BibliographyPanel`). A
            // free-standing `Text` inside a `LazyVStack` would NOT
            // register as a heading-rotor stop on macOS — the
            // rotor looks for AX container groupings, which only
            // `Section` synthesises.
            ScrollViewReader { proxy in
                List {
                    ForEach(sections) { section in
                        Section {
                            ForEach(section.commands, id: \.id) { command in
                                commandRow(command)
                                    .id(command.id)
                            }
                        } header: {
                            sectionHeader(section.title)
                        }
                    }
                }
                .listStyle(.inset)
                .onChange(of: model.selectedID) { newID in
                    // Keep the selected row visible as the user
                    // arrows past the viewport edge. Skip the
                    // animation under Reduce Motion (WCAG 2.3.1).
                    guard let newID else { return }
                    if reduceMotion {
                        proxy.scrollTo(newID, anchor: .center)
                    } else {
                        withAnimation(.easeOut(duration: 0.12)) {
                            proxy.scrollTo(newID, anchor: .center)
                        }
                    }
                }
            }
        }
    }

    /// Section header. `.accessibilityAddTraits(.isHeader)`
    /// reinforces the heading role on the `Text` element itself
    /// (the parent `Section` synthesises the AX container; the
    /// trait ensures the header text is also tagged as a heading
    /// for rotor navigation).
    private func sectionHeader(_ title: String) -> some View {
        Text(title)
            .font(.caption)
            .fontWeight(.semibold)
            .foregroundStyle(Color(nsColor: .secondaryLabelColor))
            .accessibilityAddTraits(.isHeader)
            // Explicit AX label so VoiceOver speaks the cased
            // form, not letter-by-letter if a future style adds
            // `.textCase(.uppercase)`.
            .accessibilityLabel(title)
    }

    private func commandRow(_ command: Command) -> some View {
        let isSelected = command.id == model.selectedID
        return Button {
            // Restore focus to the search field so a subsequent
            // Enter actually fires onSubmit (red-team finding P2 #4
            // — click-then-Enter would otherwise no-op).
            searchFocused = true
            invoke(command)
        } label: {
            HStack(spacing: 12) {
                Text(command.label)
                    .foregroundStyle(
                        isSelected
                            ? Color(nsColor: .selectedMenuItemTextColor)
                            : Color(nsColor: .labelColor)
                    )
                Spacer()
                if let hotkey = command.hotkeyHint {
                    Text(hotkey)
                        .foregroundStyle(
                            isSelected
                                ? Color(nsColor: .selectedMenuItemTextColor)
                                : Color(nsColor: .secondaryLabelColor)
                        )
                        .font(.callout)
                        .monospacedDigit()
                        .accessibilityHidden(true)
                }
            }
            .padding(.horizontal, 16)
            .padding(.vertical, 10)
            .frame(maxWidth: .infinity, alignment: .leading)
            .background(
                isSelected
                    ? Color(nsColor: .selectedContentBackgroundColor)
                    : Color.clear
            )
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .onHover { hovering in
            // Hover-update is debounced against recent keyboard
            // nav so stationary mouse jitter doesn't yank selection
            // away from arrow-key choices.
            guard hovering else { return }
            if Date().timeIntervalSince(lastKeyboardNavAt) > 0.25 {
                model.selectedID = command.id
            }
        }
        .accessibilityLabel(Self.voiceOverLabel(for: command))
        .accessibilityHint(command.accessibilityHint ?? "")
        .accessibilityAddTraits(isSelected ? [.isSelected] : [])
    }

    // MARK: - VoiceOver label

    /// Compose the command's label with the spelled-out chord so
    /// VoiceOver users hear "Save, Command S" the way the macOS
    /// menu bar does — and "Settings…, Command Comma" rather than
    /// "Settings…, Command ," (which VoiceOver may elide entirely
    /// when the user's punctuation setting is "None").
    ///
    /// Walks every character of `hotkeyHint` in order. Modifier
    /// glyphs (⌘⇧⌥⌃) become their spoken word; punctuation keys
    /// become their spoken name; alphanumeric keys pass through
    /// as-is.
    static func voiceOverLabel(for command: Command) -> String {
        guard let hint = command.hotkeyHint, !hint.isEmpty else {
            return command.label
        }
        var spoken: [String] = []
        for char in hint {
            if let modifierWord = chordGlyphWord[char] {
                spoken.append(modifierWord)
            } else {
                spoken.append(chordKeyWord[char] ?? String(char))
            }
        }
        return "\(command.label), \(spoken.joined(separator: " "))"
    }

    /// Modifier-key glyphs → spoken names.
    private static let chordGlyphWord: [Character: String] = [
        "⌘": "Command",
        "⇧": "Shift",
        "⌥": "Option",
        "⌃": "Control",
    ]

    /// Punctuation keys → spoken names. VoiceOver's default
    /// punctuation level ("Some") speaks "comma" for ",", but
    /// users with "None" hear nothing — so a chord ending in
    /// punctuation like ⌘, would become "Command" with no key
    /// indicator. Spelling out the punctuation makes the chord
    /// pronounceable at every VoiceOver punctuation level.
    private static let chordKeyWord: [Character: String] = [
        ",": "Comma",
        ".": "Period",
        "/": "Slash",
        "\\": "Backslash",
        ";": "Semicolon",
        "'": "Quote",
        "[": "Left Bracket",
        "]": "Right Bracket",
        "-": "Minus",
        "=": "Equals",
        "`": "Backtick",
        " ": "Space",
    ]

    // MARK: - Arrow-key monitor

    /// Modifier mask: any of these on an arrow key means "let it
    /// through" — Shift+↓ extends text-field selection, Cmd+↓
    /// jumps caret to end-of-text, **Ctrl+Option+↓ is VoiceOver
    /// Quick Nav** (the one we absolutely must not steal), and
    /// Fn+↓ is macOS Page Down. Only bare ↑ / ↓ moves palette
    /// selection.
    private static let arrowModifierMask: NSEvent.ModifierFlags =
        [.shift, .control, .option, .command, .function]

    /// Decide whether a `.keyDown` event should pass through the
    /// monitor unconsumed. Pure function — no `@State` access — so
    /// the modifier-passthrough contract is unit-testable without
    /// having to synthesise live `NSEvent`s into the run loop.
    ///
    /// Pass through (returns true) when:
    /// - Key is not ↑ (126) or ↓ (125)
    /// - Key is ↑ / ↓ but any modifier in `arrowModifierMask` is
    ///   held (Shift, Ctrl, Option, Cmd, Fn — covers text-field
    ///   selection, caret-jump, Page-Up/Down, and VoiceOver Quick
    ///   Nav).
    ///
    /// Consume (returns false) only for bare ↑ / ↓.
    nonisolated static func shouldPassThroughArrow(
        keyCode: UInt16,
        modifierFlags: NSEvent.ModifierFlags
    ) -> Bool {
        guard keyCode == 126 || keyCode == 125 else { return true }
        return !modifierFlags.intersection(arrowModifierMask).isEmpty
    }

    private func installArrowKeyMonitor() {
        arrowKeyMonitor = NSEvent.addLocalMonitorForEvents(matching: .keyDown) { event in
            if Self.shouldPassThroughArrow(
                keyCode: event.keyCode,
                modifierFlags: event.modifierFlags
            ) {
                return event
            }
            lastKeyboardNavAt = Date()
            if event.keyCode == 126 {
                model.selectPrevious()
            } else {
                model.selectNext()
            }
            return nil
        }
    }

    private func removeArrowKeyMonitor() {
        if let m = arrowKeyMonitor {
            NSEvent.removeMonitor(m)
            arrowKeyMonitor = nil
        }
    }

    // MARK: - Invoke

    /// Triggered by `onSubmit` (Enter in the search field). Invokes
    /// the selected command; dismisses on success and records the
    /// command in recents (#316). Stays open on error per #315.
    private func invokeSelected() {
        guard let id = model.selectedID,
              let command = model.displayOrder.first(where: { $0.id == id })
        else { return }
        invoke(command)
    }

    private func invoke(_ command: Command) {
        let outcome = model.invoke(command, via: appState.commandRegistry)
        if case .success = outcome {
            // Persist the invocation to recents so it surfaces in
            // the Recent section next time the palette opens.
            // Non-fatal — recents store failures are logged
            // internally, not bubbled.
            appState.recordCommandInvocation(id: command.id)
            dismiss()
        }
        // Other outcomes: model.pendingAnnouncement is set, the
        // .onChange handler posts it. Palette stays open per #315.
    }

    // MARK: - Empty / no-match states

    private var emptyState: some View {
        VStack(spacing: 8) {
            Spacer()
            Text("No commands available")
                .font(.headline)
                .foregroundStyle(Color(nsColor: .labelColor))
                .accessibilityAddTraits(.isHeader)
            Text("Open a vault to access the palette.")
                .foregroundStyle(Color(nsColor: .secondaryLabelColor))
                .multilineTextAlignment(.center)
            Spacer()
        }
        .frame(maxWidth: .infinity)
        .padding(.horizontal, 24)
        .accessibilityElement(children: .combine)
        .accessibilityLabel("No commands available. Open a vault to access the palette.")
    }

    private var noMatchesState: some View {
        VStack(spacing: 8) {
            Spacer()
            Text("No matches")
                .font(.headline)
                .foregroundStyle(Color(nsColor: .labelColor))
                .accessibilityAddTraits(.isHeader)
            Text("No command matches \"\(model.query)\". Try fewer letters or a different word.")
                .foregroundStyle(Color(nsColor: .secondaryLabelColor))
                .multilineTextAlignment(.center)
            Spacer()
        }
        .frame(maxWidth: .infinity)
        .padding(.horizontal, 24)
        .accessibilityElement(children: .combine)
        .accessibilityLabel("No command matches \(model.query). Try fewer letters or a different word.")
    }
}
