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
            model.loadCommands(appState.commandRegistry.list())
            installArrowKeyMonitor()
        }
        .onDisappear {
            removeArrowKeyMonitor()
        }
        .onChange(of: model.query) { _ in
            model.handleQueryChange()
        }
        .onChange(of: model.selectedID) { newID in
            // Polite VoiceOver announcement so screen-reader users
            // hear what arrow keys are doing — the .isSelected
            // trait change on a non-focused row doesn't trigger
            // VO speech on its own.
            guard let newID,
                  let command = model.filteredCommands.first(where: { $0.id == newID })
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
        let filtered = model.filteredCommands
        if filtered.isEmpty {
            if model.commands.isEmpty {
                emptyState
            } else {
                noMatchesState
            }
        } else {
            ScrollViewReader { proxy in
                ScrollView {
                    LazyVStack(alignment: .leading, spacing: 0) {
                        ForEach(filtered, id: \.id) { command in
                            commandRow(command)
                                .id(command.id)
                            Divider()
                        }
                    }
                }
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

    static func voiceOverLabel(for command: Command) -> String {
        guard let hint = command.hotkeyHint, !hint.isEmpty else {
            return command.label
        }
        let chordWords = hint.compactMap { chordGlyphWord[$0] }
        let keyChar = hint.last.flatMap { chordGlyphWord[$0] == nil ? String($0) : nil }
            ?? hint
        let chordSpoken = (chordWords + [keyChar]).joined(separator: " ")
        return "\(command.label), \(chordSpoken)"
    }

    private static let chordGlyphWord: [Character: String] = [
        "⌘": "Command",
        "⇧": "Shift",
        "⌥": "Option",
        "⌃": "Control",
    ]

    // MARK: - Arrow-key monitor

    /// Modifier mask: any of these on an arrow key means "let it
    /// through" — Shift+↓ extends text-field selection, Cmd+↓
    /// jumps caret to end-of-text, **Ctrl+Option+↓ is VoiceOver
    /// Quick Nav** (the one we absolutely must not steal). Only
    /// bare ↑ / ↓ moves palette selection.
    private static let arrowModifierMask: NSEvent.ModifierFlags =
        [.shift, .control, .option, .command]

    private func installArrowKeyMonitor() {
        arrowKeyMonitor = NSEvent.addLocalMonitorForEvents(matching: .keyDown) { event in
            // Up = 126, Down = 125 on macOS virtual key codes.
            guard event.keyCode == 126 || event.keyCode == 125 else {
                return event
            }
            // Pass through any modified arrow chord so text editing
            // and VoiceOver Quick Nav keep working.
            if !event.modifierFlags.intersection(Self.arrowModifierMask).isEmpty {
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
    /// the selected command; dismisses only on success.
    private func invokeSelected() {
        let outcome = model.invokeSelected(via: appState.commandRegistry)
        if case .success = outcome {
            dismiss()
        }
        // Other outcomes: model.pendingAnnouncement is set, the
        // .onChange handler on the View posts it. Palette stays
        // open per #315 spec.
    }

    private func invoke(_ command: Command) {
        let outcome = model.invoke(command, via: appState.commandRegistry)
        if case .success = outcome {
            dismiss()
        }
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
