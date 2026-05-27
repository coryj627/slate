// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import SwiftUI

/// Modal command palette shell — Milestone Q issue #313.
///
/// Lands the search field + empty results placeholder + dismissal
/// plumbing. The fuzzy filter (#315), section grouping +
/// announcements (#316), and menu-bridge population of the registry
/// (#314) come in follow-up issues; this PR deliberately ships an
/// empty list so the layout / focus / hotkey contract is reviewable
/// in isolation.
///
/// ## Behaviour
///
/// - Opens via `⌘⇧P` (menu wiring in `SlateMacApp`, gated on
///   `isVaultOpen`); the search field auto-focuses on appear.
/// - Closes via `Esc` (`.cancelAction` overlay). Matches the
///   Xcode / Sublime / TextMate convention — open via chord,
///   close via Esc. We deliberately don't ship a second hidden
///   `⌘⇧P` button inside the sheet to "toggle close" because
///   the behaviour would rest on SwiftUI's responder-chain
///   routing and can't be verified without XCUITest infra.
/// - Focus returns to the prior first responder via SwiftUI's
///   default sheet behaviour.
///
/// ## Colours
///
/// Background is `NSColor.controlBackgroundColor`; text uses
/// `labelColor` and `secondaryLabelColor`. Both pairings pass the
/// project's APCA `|Lc| > 75` bar in light and dark mode (verified
/// by `CommandPaletteViewTests`).
struct CommandPaletteView: View {
    @EnvironmentObject private var appState: AppState
    @Environment(\.dismiss) private var dismiss

    /// Local search query. Reset on every open — the palette doesn't
    /// preserve typed text across sessions. Per #315, this will feed
    /// the fuzzy filter once it lands.
    @State private var query: String = ""

    /// Snapshot of the registry's commands taken on `onAppear` so
    /// `body` doesn't re-fetch on every keystroke (the registry's
    /// own `list()` docstring asks callers to cache the snapshot
    /// for the palette's open lifetime). #315 will filter this in
    /// place; #316 will group by `section`.
    @State private var commands: [Command] = []

    @FocusState private var searchFocused: Bool

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            searchField
            Divider()
            results
        }
        .frame(minWidth: 560, idealWidth: 560, minHeight: 360, idealHeight: 360)
        .background(Color(nsColor: .controlBackgroundColor))
        // Esc dismisses via SwiftUI's built-in exit command. Direct
        // API — no fake button to satisfy the keyboard-shortcut
        // routing, so the static a11y check (`small-touch-target`,
        // WCAG 2.5.8) has nothing to flag.
        .onExitCommand { dismiss() }
        .onAppear {
            // Auto-focus the search field on every open so the user
            // can start typing immediately.
            searchFocused = true
            // Snapshot the registry — #314 onward, the palette has
            // real commands. We don't expect the registry's contents
            // to mutate while the palette is open (no live re-load
            // path in V1), so a one-shot snapshot is fine.
            commands = appState.commandRegistry.list()
        }
    }

    // MARK: - Search field

    private var searchField: some View {
        HStack(spacing: 8) {
            Image(systemName: "magnifyingglass")
                .foregroundStyle(Color(nsColor: .secondaryLabelColor))
                .accessibilityHidden(true)
            TextField("Search commands", text: $query)
                .textFieldStyle(.plain)
                .font(.title3)
                .focused($searchFocused)
                .accessibilityLabel("Search commands")
                // Hint only describes wired behaviour. Enter-to-invoke
                // lands in #315; the hint will gain it then.
                .accessibilityHint("Type to filter. Press Escape to close.")
        }
        .padding(.horizontal, 16)
        .padding(.vertical, 12)
    }

    // MARK: - Results

    /// Renders the registered commands as a scrollable list, or a
    /// placeholder if the registry is empty (shouldn't happen in
    /// practice once #314 ships, but defensive — a future Settings
    /// "disable all" toggle or a fresh-init race could empty it).
    /// Filtering on `query` is wired in #315; grouping into
    /// sections + recents lands in #316.
    @ViewBuilder
    private var results: some View {
        if commands.isEmpty {
            emptyState
        } else {
            ScrollView {
                LazyVStack(alignment: .leading, spacing: 0) {
                    ForEach(commands, id: \.id) { command in
                        commandRow(command)
                        Divider()
                    }
                }
            }
        }
    }

    private func commandRow(_ command: Command) -> some View {
        Button {
            invoke(command)
        } label: {
            HStack(spacing: 12) {
                Text(command.label)
                    .foregroundStyle(Color(nsColor: .labelColor))
                Spacer()
                if let hotkey = command.hotkeyHint {
                    Text(hotkey)
                        .foregroundStyle(Color(nsColor: .secondaryLabelColor))
                        .font(.callout)
                        .monospacedDigit()
                        .accessibilityHidden(true)
                }
            }
            .padding(.horizontal, 16)
            .padding(.vertical, 10)
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        // VoiceOver reads "<label>, <Modifier> <Modifier> <Key>" so
        // blind users learn the chord the same way they do from
        // macOS standard menu items. The chord-glyph translation
        // (⌘ → "Command", etc.) is necessary because VoiceOver
        // pronunciation of the raw glyphs is unpredictable.
        .accessibilityLabel(Self.voiceOverLabel(for: command))
        .accessibilityHint(command.accessibilityHint ?? "")
    }

    /// Compose the command's label with the spelled-out chord so
    /// VoiceOver users hear "Save, Command S" the way the macOS
    /// menu bar does, not just "Save".
    static func voiceOverLabel(for command: Command) -> String {
        guard let hint = command.hotkeyHint, !hint.isEmpty else {
            return command.label
        }
        let chordWords = hint.compactMap { chordGlyphWord[$0] }
        // Last char of the hint is the literal key (e.g. "S" in "⌘S",
        // "N" in "⇧⌘N"). Fallback to the whole hint if parsing fails.
        let keyChar = hint.last.flatMap { chordGlyphWord[$0] == nil ? String($0) : nil }
            ?? hint
        let chordSpoken = (chordWords + [keyChar]).joined(separator: " ")
        return "\(command.label), \(chordSpoken)"
    }

    /// Map chord glyphs to spoken modifier names. The key char
    /// itself (S, N, J, etc.) is appended in `voiceOverLabel`.
    private static let chordGlyphWord: [Character: String] = [
        "⌘": "Command",
        "⇧": "Shift",
        "⌥": "Option",
        "⌃": "Control",
    ]

    private func invoke(_ command: Command) {
        // Dismiss the palette first so SwiftUI animates the sheet
        // closure before any palette-invoked command tries to
        // present its own sheet (Add Property, Bulk Rename,
        // Citation Summary, Tasks Review, Template Picker — all
        // would race the dismissal otherwise). Then defer the
        // invoke to the next runloop turn.
        dismiss()
        let registry = appState.commandRegistry
        let commandID = command.id
        DispatchQueue.main.async {
            do {
                try registry.invokeById(id: commandID)
            } catch {
                // Surface in debug builds — silent swallow in
                // release is the #315 territory where a live-region
                // announcement will replace the assertion.
                assertionFailure("command \(commandID) failed: \(error)")
            }
        }
    }

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
}
