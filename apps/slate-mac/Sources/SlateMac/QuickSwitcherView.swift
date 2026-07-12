// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import SwiftUI

/// Modal quick switcher — fuzzy filename quick-open on ⌘O (U1-5
/// follow-up #495; chord moved ⌘T→⌘O by #863, Obsidian's actual
/// quick-switcher default).
///
/// Presentation and interaction mirror `CommandPaletteView` wholesale;
/// only the payload (vault files, not commands) and the activation
/// (three open-target chords instead of a single invoke) differ.
///
/// ## Behaviour
///
/// - Opens via `⌘O` (menu wiring in `SlateMacApp`; with no vault the
///   chord falls through to the vault picker instead — the sheet only
///   mounts inside a vault); the search field auto-focuses on appear.
/// - Typing filters the file list via the shared fuzzy matcher with a
///   name-over-path bias (`QuickSwitcherModel.score`).
/// - Arrow ↑ / ↓ moves selection with wrap. Modified arrows (Shift/Cmd/
///   Ctrl+Option/Fn) pass through so text-editing and VoiceOver chords
///   keep working — same mask as the palette.
/// - Return opens the selected file: bare ↩ → current tab, ⌘↩ → new
///   tab, ⌘⌥↩ → new split (horizontal). The chord is read from the
///   keyDown monitor (the field's `onSubmit` can't see modifiers).
/// - `Esc` dismisses via the same monitor (a focused field editor eats
///   raw Escape before `.onExitCommand`).
/// - Hover updates selection, debounced against recent keyboard nav.
///
/// ## Colours
///
/// Background `controlBackgroundColor`; primary name `labelColor`,
/// secondary path `secondaryLabelColor`; selected row
/// `selectedContentBackgroundColor` + `selectedMenuItemTextColor`. All
/// pairings clear APCA `|Lc| > 75` (verified by `QuickSwitcherViewTests`,
/// which reuse the palette's contrast assertions since the roles match).
struct QuickSwitcherView: View {
    @EnvironmentObject private var appState: AppState
    @Environment(\.dismiss) private var dismiss
    @Environment(\.accessibilityReduceMotion) private var reduceMotion

    @StateObject private var model = QuickSwitcherModel()

    /// Local `.keyDown` monitor. Handles ↑/↓ selection, bare-Escape
    /// dismissal, AND the three Return chords — the field editor would
    /// otherwise swallow Escape, and `onSubmit` can't see the ⌘/⌥
    /// modifiers that pick the open target.
    @State private var keyDownMonitor: Any? = nil

    /// Timestamp of the most recent arrow-key selection change. Hover
    /// only updates selection when the mouse moves AFTER this, so a
    /// stationary cursor doesn't yank the keyboard-driven choice.
    @State private var lastKeyboardNavAt: Date = .distantPast

    /// True between `.onAppear` and the first user-driven selection
    /// change. Suppresses the polite selection announcement at open
    /// time — the initial row is recency-ordered and announcing it
    /// before the user does anything is noise.
    @State private var isInitialLoad: Bool = true

    @FocusState private var searchFocused: Bool

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            searchField
            Divider()
            results
            Divider()
            // Visible key legend: the ⌘↩ / ⌥⌘↩ open targets existed
            // only in the field's accessibilityHint, so sighted users
            // had no way to learn them (the palette shows per-row
            // hotkey hints; this is the switcher's equivalent).
            // Hidden from AX — the hint above is the audio source,
            // and glyph-runs read poorly compared to its prose.
            HStack(spacing: Tokens.Spacing.lg) {
                Text("↩ Open")
                Text("⌘↩ New Tab")
                Text("⌥⌘↩ Split")
            }
            .font(Tokens.Typography.caption)
            .foregroundStyle(Tokens.ColorRole.textSecondary)
            .padding(.horizontal, 16)
            .padding(.vertical, Tokens.Spacing.sm)
            .accessibilityHidden(true)
        }
        .frame(minWidth: 560, idealWidth: 560, minHeight: 360, idealHeight: 360)
        .background(Color(nsColor: .controlBackgroundColor))
        .onExitCommand { dismiss() }
        .onAppear {
            searchFocused = true
            isInitialLoad = true
            model.load(
                files: appState.files
                    .filter { file in
                        file.isMarkdown
                            || file.path.lowercased().hasSuffix(".canvas")
                            || file.path.lowercased().hasSuffix(".base")
                    }
                    .map { QuickSwitcherModel.FileRow(path: $0.path, name: $0.name) },
                recents: appState.fileRecents)
            model.announceInitialCount()
            installKeyDownMonitor()
        }
        .onDisappear { removeKeyDownMonitor() }
        .onChange(of: model.query) {
            model.handleQueryChange()
        }
        .onChange(of: model.selectedID) { _, newID in
            guard !isInitialLoad else {
                isInitialLoad = false
                return
            }
            guard let newID,
                let row = model.displayOrder.first(where: { $0.id == newID })
            else { return }
            // `.medium` is the politeness floor that survives typing
            // echoes + count announcements (palette #418 F-A1 finding).
            postAccessibilityAnnouncement(
                "Selected: \(row.displayName)", priority: .medium)
        }
        .onChange(of: model.resultAnnouncement) { _, announcement in
            // `.medium` so per-keystroke count announcements coalesce
            // rather than interrupting mid-word — same as the palette's
            // filter-count announcement.
            guard let announcement, !announcement.isEmpty else { return }
            postAccessibilityAnnouncement(announcement, priority: .medium)
            model.clearAnnouncement()
        }
    }

    // MARK: - Search field

    private var searchField: some View {
        HStack(spacing: 8) {
            SlateSymbol.search.decorative
                .foregroundStyle(Color(nsColor: .secondaryLabelColor))
            TextField("Search files", text: $model.query)
                .textFieldStyle(.plain)
                .font(.title3)
                .focused($searchFocused)
                .accessibilityLabel("Search files")
                .accessibilityHint(
                    "Arrow up and down to move selection. Return opens the selected file. "
                    + "Command Return opens it in a new tab. Command Option Return opens it in a split."
                )
                .onSubmit { open(target: .currentTab) }
        }
        .padding(.horizontal, 16)
        .padding(.vertical, 12)
    }

    // MARK: - Results

    @ViewBuilder
    private var results: some View {
        let rows = model.displayOrder
        if rows.isEmpty {
            if model.files.isEmpty {
                emptyState
            } else {
                noMatchesState
            }
        } else {
            ScrollViewReader { proxy in
                List {
                    ForEach(rows) { row in
                        fileRow(row).id(row.id)
                    }
                }
                .listStyle(.inset)
                .onChange(of: model.selectedID) { _, newID in
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

    private func fileRow(_ row: QuickSwitcherModel.FileRow) -> some View {
        let isSelected = row.id == model.selectedID
        return Button {
            // Restore focus so a subsequent Return fires onSubmit
            // (palette red-team P2 #4: click-then-Enter otherwise no-ops).
            searchFocused = true
            // A click carries the same modifier semantics as Return —
            // read the live event so ⌘-click opens in a new tab, etc.
            open(row, target: appState.openTargetFromCurrentEvent())
        } label: {
            VStack(alignment: .leading, spacing: 2) {
                Text(row.displayName)
                    .foregroundStyle(
                        isSelected
                            ? Color(nsColor: .selectedMenuItemTextColor)
                            : Color(nsColor: .labelColor))
                Text(row.path)
                    .font(.caption)
                    .foregroundStyle(
                        isSelected
                            ? Color(nsColor: .selectedMenuItemTextColor)
                            : Color(nsColor: .secondaryLabelColor))
            }
            .padding(.horizontal, 16)
            .padding(.vertical, 8)
            .frame(maxWidth: .infinity, alignment: .leading)
            .background(
                isSelected
                    ? Color(nsColor: .selectedContentBackgroundColor)
                    : Color.clear)
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .onHover { hovering in
            guard hovering else { return }
            if Date().timeIntervalSince(lastKeyboardNavAt) > 0.25 {
                model.selectedID = row.id
            }
        }
        // "<name>, <path>" per spec — the display name then the
        // vault-relative path so VoiceOver disambiguates same-named
        // files in different folders.
        .accessibilityLabel("\(row.displayName), \(row.path)")
        .accessibilityIsSelected(isSelected)
    }

    // MARK: - Key monitor

    /// Arrow keycodes + the narrow modifier mask, both identical to the
    /// palette's. Reused verbatim rather than shared: the palette's
    /// `ArrowKey` / `arrowModifierMask` are `private`, and re-exposing
    /// them for one more consumer would widen that type's surface for a
    /// two-constant saving. If a third consumer appears, promote these
    /// to a shared `PaletteKeyMonitor` utility.
    private enum Key {
        static let up: UInt16 = 126
        static let down: UInt16 = 125
        static let escape: UInt16 = 53
        static let returnKey: UInt16 = 36
        static let keypadEnter: UInt16 = 76
    }

    private static let arrowModifierMask: NSEvent.ModifierFlags =
        [.shift, .control, .option, .command, .function]

    /// Bare ↑/↓ navigate; any masked modifier passes through (text-field
    /// selection, caret jump, Page Up/Down, VoiceOver Quick Nav).
    nonisolated static func shouldPassThroughArrow(
        keyCode: UInt16, modifierFlags: NSEvent.ModifierFlags
    ) -> Bool {
        guard keyCode == Key.up || keyCode == Key.down else { return true }
        return !modifierFlags.intersection(arrowModifierMask).isEmpty
    }

    /// Only BARE Escape dismisses — any real chord modifier passes
    /// through (a system Cmd+Esc, etc.). `.function` is omitted: Escape
    /// never carries it, so fn+Escape still dismisses (harmless).
    nonisolated static func isDismissKey(
        keyCode: UInt16, modifierFlags: NSEvent.ModifierFlags
    ) -> Bool {
        keyCode == Key.escape
            && modifierFlags.intersection([.command, .option, .control, .shift]).isEmpty
    }

    /// Resolve a Return keydown to its open target, or `nil` when the
    /// event isn't a Return chord this view handles. Pure so the chord
    /// table is unit-testable without synthesising `NSEvent`s:
    ///  - bare ↩  → `.currentTab`
    ///  - ⌘↩      → `.newTab`
    ///  - ⌘⌥↩     → `.newSplit(.horizontal)`
    ///
    /// Other modifier combinations (⌥↩, ⇧↩, ⌃↩) return `nil` — the
    /// event falls through untouched rather than opening with a guessed
    /// target. Both Return and keypad Enter are accepted (SearchOverlay
    /// precedent).
    nonisolated static func openTarget(
        forReturnKeyCode keyCode: UInt16, modifierFlags: NSEvent.ModifierFlags
    ) -> AppState.OpenTarget? {
        guard keyCode == Key.returnKey || keyCode == Key.keypadEnter else { return nil }
        let mods = modifierFlags.intersection([.command, .option, .control, .shift])
        if mods.isEmpty { return .currentTab }
        if mods == [.command] { return .newTab }
        if mods == [.command, .option] { return .newSplit(.horizontal) }
        return nil
    }

    private func installKeyDownMonitor() {
        guard keyDownMonitor == nil else { return }
        keyDownMonitor = NSEvent.addLocalMonitorForEvents(matching: .keyDown) { event in
            // Don't strand an IME user mid-composition: while marked
            // text is present, intercept nothing.
            if let editor = event.window?.firstResponder as? NSTextView,
                editor.hasMarkedText()
            {
                return event
            }
            if Self.isDismissKey(
                keyCode: event.keyCode, modifierFlags: event.modifierFlags)
            {
                appState.isQuickSwitcherOpen = false
                return nil
            }
            // Return chords: bare onSubmit handles the currentTab case
            // too, but reading the chord here is the ONLY way to see the
            // ⌘/⌥ modifiers. Handle all three here and consume; let a
            // no-target Return (e.g. ⇧↩) fall through to onSubmit.
            if let target = Self.openTarget(
                forReturnKeyCode: event.keyCode, modifierFlags: event.modifierFlags)
            {
                open(target: target)
                return nil
            }
            if Self.shouldPassThroughArrow(
                keyCode: event.keyCode, modifierFlags: event.modifierFlags)
            {
                return event
            }
            lastKeyboardNavAt = Date()
            switch event.keyCode {
            case Key.up: model.selectPrevious()
            case Key.down: model.selectNext()
            default: break
            }
            return nil
        }
    }

    private func removeKeyDownMonitor() {
        if let m = keyDownMonitor {
            NSEvent.removeMonitor(m)
            keyDownMonitor = nil
        }
    }

    // MARK: - Open

    /// Open the currently-selected row with `target`. No-op if nothing
    /// is selected.
    private func open(target: AppState.OpenTarget) {
        guard let row = model.selectedRow else { return }
        open(row, target: target)
    }

    /// Dismiss first, THEN open — mirrors how the palette dismisses
    /// before invoking so focus lands back in the editor, not the
    /// dismissing sheet. `openFile` records the open into file-recents
    /// at AppState's single choke point.
    private func open(_ row: QuickSwitcherModel.FileRow, target: AppState.OpenTarget) {
        dismiss()
        appState.openFile(row.path, target: target)
    }

    // MARK: - Empty / no-match states

    private var emptyState: some View {
        VStack(spacing: 8) {
            Spacer()
            Text("No files")
                .font(.headline)
                .foregroundStyle(Color(nsColor: .labelColor))
                .accessibilityAddTraits(.isHeader)
            Text("This vault has no notes yet.")
                .foregroundStyle(Color(nsColor: .secondaryLabelColor))
                .multilineTextAlignment(.center)
            Spacer()
        }
        .frame(maxWidth: .infinity)
        .padding(.horizontal, 24)
        .accessibilityElement(children: .combine)
        .accessibilityLabel("No files. This vault has no notes yet.")
    }

    private var noMatchesState: some View {
        VStack(spacing: 8) {
            Spacer()
            Text("No matching files")
                .font(.headline)
                .foregroundStyle(Color(nsColor: .labelColor))
                .accessibilityAddTraits(.isHeader)
            Text("No file matches \"\(model.query)\". Try fewer letters or a different word.")
                .foregroundStyle(Color(nsColor: .secondaryLabelColor))
                .multilineTextAlignment(.center)
            Spacer()
        }
        .frame(maxWidth: .infinity)
        .padding(.horizontal, 24)
        .accessibilityElement(children: .combine)
        .accessibilityLabel(
            "No file matches \(model.query). Try fewer letters or a different word.")
    }
}
