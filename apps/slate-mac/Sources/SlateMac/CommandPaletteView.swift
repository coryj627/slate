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

    /// Local `.keyDown` monitor. Handles ↑/↓ selection navigation
    /// (#315/#339) AND bare Escape dismissal: a focused search field
    /// swallows the raw Escape key before SwiftUI's
    /// `.onExitCommand` fires, so the monitor — which sees the
    /// keyDown first — intercepts Escape and dismisses. (Cmd-. still
    /// reaches `.onExitCommand`, but no user discovers that.)
    @State private var keyDownMonitor: Any? = nil

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
            installKeyDownMonitor()
        }
        .onDisappear {
            removeKeyDownMonitor()
        }
        .onChange(of: model.query) {
            model.handleQueryChange()
        }
        .onChange(of: model.selectedID) { _, newID in
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
            // #418 (F-A1): was .low — anything else speaking (typing
            // echoes, filter-count announcements) superseded it and
            // the VO test heard nothing while arrowing. Medium is
            // the politeness floor that actually survives.
            postAccessibilityAnnouncement(
                "Selected: \(command.label)",
                priority: .medium
            )
        }
        .onChange(of: model.pendingAnnouncement) { _, announcement in
            guard let announcement, !announcement.isEmpty else { return }
            postAccessibilityAnnouncement(announcement, priority: .high)
            model.clearPendingAnnouncement()
        }
        .onChange(of: model.filterAnnouncement) { _, announcement in
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
            SlateSymbol.search.decorative
                .foregroundStyle(Color(nsColor: .secondaryLabelColor))
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
                .onChange(of: model.selectedID) { _, newID in
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
        // Conditional modifier instead of `.accessibilityAddTraits(
        // isSelected ? [.isSelected] : [])`. The empty-array branch
        // theoretically would have to be a no-op, but the `Add` in
        // the API name leaves doubt about whether SwiftUI clears a
        // previously-applied trait when the next render passes []
        // (see #324 red team). The conditional pattern below sidesteps
        // the question — when isSelected is false, the
        // `.accessibilityAddTraits(.isSelected)` modifier simply
        // isn't in the view's modifier chain for that render.
        .accessibilityIsSelected(isSelected)
    }

    // MARK: - VoiceOver label

    /// Compose the command's label with the spelled-out chord so
    /// VoiceOver users hear "Save, Command S" the way the macOS
    /// menu bar does — and "Settings…, Command Comma" rather than
    /// "Settings…, Command ," (which VoiceOver may elide entirely
    /// when the user's punctuation setting is "None").
    ///
    /// The chord-glyph → spoken-word walk lives in `HotkeySpoken`
    /// (#332) so the punctuation table stays single-source as more
    /// consumers arrive. This method owns only the label-composition
    /// shape (the comma separator, the empty-hint short-circuit).
    static func voiceOverLabel(for command: Command) -> String {
        guard let hint = command.hotkeyHint, !hint.isEmpty else {
            return command.label
        }
        return "\(command.label), \(HotkeySpoken.spoken(for: hint))"
    }

    // MARK: - Arrow-key monitor

    /// macOS virtual key codes for the two arrow keys we intercept.
    /// Values match Carbon's `kVK_UpArrow` (126) / `kVK_DownArrow`
    /// (125); both have been stable since OS X 10.0 and are part
    /// of the published HIToolbox ABI, so the literal is safe.
    /// Declared locally to avoid `import Carbon.HIToolbox` for two
    /// constants.
    enum ArrowKey {
        static let up: UInt16 = 126   // kVK_UpArrow
        static let down: UInt16 = 125 // kVK_DownArrow

        /// Logical palette-navigation direction for an intercepted
        /// arrow key (#339). Only the directions actually wired to
        /// navigation are modelled — there is intentionally no
        /// `.left` / `.right` case yet, because the palette doesn't
        /// navigate horizontally and a dead enum case would be
        /// untestable speculative generality.
        ///
        /// The payoff of routing dispatch through this enum is
        /// compile-time exhaustiveness: when left/right *do* get a
        /// palette action, you add the keycode constant, a
        /// `direction(for:)` arm, and a `case` here — and every
        /// `switch` over `Direction` that isn't updated becomes a
        /// build error, so no dispatch site can silently fall
        /// through. That's the "less error-prone, co-located"
        /// property Codoki's #338 review asked for, delivered
        /// without speculative cases.
        enum Direction {
            case up
            case down
        }

        /// Map a virtual key code to its palette-navigation
        /// `Direction`, or `nil` if the key isn't one the palette
        /// navigates with. Single source of truth for "which arrow
        /// keycodes mean navigation" — both `shouldPassThroughArrow`
        /// and the monitor dispatch consult it, so the navigable set
        /// can't drift between the two.
        static func direction(for keyCode: UInt16) -> Direction? {
            switch keyCode {
            case up:   return .up
            case down: return .down
            default:   return nil
            }
        }
    }

    /// Modifier mask: any of these on an arrow key means "let it
    /// through" — Shift+↓ extends text-field selection, Cmd+↓
    /// jumps caret to end-of-text, **Ctrl+Option+↓ is VoiceOver
    /// Quick Nav** (the one we absolutely must not steal), and
    /// Fn+↓ is macOS Page Down. Only bare ↑ / ↓ moves palette
    /// selection.
    ///
    /// Device-dependent flags like `.capsLock` and `.numericPad`
    /// are deliberately not in the mask. We don't normalize them
    /// away with `.deviceIndependentFlagsMask` because that mask
    /// (`0xffff0000UL`) only strips the low 16 bits — the CG-level
    /// left/right-key disambiguation that NSEvent doesn't surface
    /// in the first place. `.capsLock` (bit 16) etc. live in the
    /// high 16 bits and survive that intersection. The chord
    /// passthrough works correctly because the mask is narrow,
    /// not because the input is sanitised; the regression tests
    /// (`testCapsLockOnBareArrowIsStillConsumed`, etc.) lock in
    /// the narrowness so it can't drift.
    private static let arrowModifierMask: NSEvent.ModifierFlags =
        [.shift, .control, .option, .command, .function]

    /// Decide whether a `.keyDown` event should pass through the
    /// monitor unconsumed. Pure function — no `@State` access — so
    /// the modifier-passthrough contract is unit-testable without
    /// having to synthesise live `NSEvent`s into the run loop.
    ///
    /// Pass through (returns true) when:
    /// - Key is not a navigable arrow (`ArrowKey.direction(for:)`
    ///   returns `nil`)
    /// - Key IS a navigable arrow but any modifier in
    ///   `arrowModifierMask` is held (Shift, Ctrl, Option, Cmd, Fn
    ///   — covers text-field selection, caret-jump, Page-Up/Down,
    ///   and VoiceOver Quick Nav).
    ///
    /// Consume (returns false) only for a bare navigable arrow.
    /// "Navigable" is defined solely by `direction(for:)` (#339), so
    /// when left/right gain a `Direction` case they automatically
    /// become consumable here too — the navigable set lives in one
    /// place.
    nonisolated static func shouldPassThroughArrow(
        keyCode: UInt16,
        modifierFlags: NSEvent.ModifierFlags
    ) -> Bool {
        guard ArrowKey.direction(for: keyCode) != nil else { return true }
        return !modifierFlags.intersection(arrowModifierMask).isEmpty
    }

    /// Carbon virtual key code for Escape (`kVK_Escape`). Stable
    /// since OS X 10.0, like the arrow codes above.
    static let escapeKeyCode: UInt16 = 53

    /// Decide whether a `.keyDown` event is a bare Escape that should
    /// dismiss the palette. Pure (no `@State`) so it's unit-testable
    /// without synthesising live `NSEvent`s.
    ///
    /// **Why a monitor handles Escape at all.** Dismiss is also wired
    /// via `.onExitCommand` (which fires for Esc *and* Cmd-.), but a
    /// focused search field's AppKit field editor swallows the raw
    /// Escape key as `cancelOperation:` before `.onExitCommand` ever
    /// sees it — so Esc silently failed to close the palette while
    /// Cmd-. worked (a dead end no user discovers). The keyDown
    /// monitor runs before the field editor (same mechanism that
    /// lets ↑/↓ drive selection while the field has focus), so it
    /// catches Escape and dismisses. Found via live debugging; the
    /// responder-chain behaviour is exactly what the #313 unit tests
    /// couldn't reach.
    ///
    /// Only BARE Escape dismisses — any modifier (e.g. a system
    /// Cmd+Esc) passes through untouched.
    ///
    /// The check-set omits `.function` (unlike `arrowModifierMask`,
    /// which includes it): arrow keys carry `.function` intrinsically —
    /// they're in the `NSFunctionKey` Unicode range — so the arrow mask
    /// has to list it, but Escape never carries `.function`. fn+Escape
    /// therefore still dismisses, which is intentional and harmless.
    nonisolated static func isDismissKey(
        keyCode: UInt16,
        modifierFlags: NSEvent.ModifierFlags
    ) -> Bool {
        keyCode == escapeKeyCode
            && modifierFlags.intersection([.command, .option, .control, .shift]).isEmpty
    }

    private func installKeyDownMonitor() {
        // Idempotent: if SwiftUI fires `.onAppear` again before a
        // matching `.onDisappear` (re-presentation / view-identity
        // churn), keep the existing monitor rather than overwriting its
        // token — an orphaned monitor can never be removed and would
        // keep swallowing bare Escape app-wide for the rest of the
        // session (red-team hardening).
        guard keyDownMonitor == nil else { return }
        keyDownMonitor = NSEvent.addLocalMonitorForEvents(matching: .keyDown) { event in
            // While an input method is composing (marked text in the
            // field editor), intercept nothing: Escape must reach the
            // input context to cancel the composition, and ↑/↓ drive the
            // candidate window. Pass the event through untouched so we
            // don't strand an IME user mid-composition (red-team).
            if let editor = event.window?.firstResponder as? NSTextView,
                editor.hasMarkedText()
            {
                return event
            }
            // Bare Escape → dismiss. Checked before the arrow logic
            // because the focused search field would otherwise eat
            // it (see `isDismissKey`).
            if Self.isDismissKey(
                keyCode: event.keyCode,
                modifierFlags: event.modifierFlags
            ) {
                appState.isCommandPaletteOpen = false
                return nil
            }
            if Self.shouldPassThroughArrow(
                keyCode: event.keyCode,
                modifierFlags: event.modifierFlags
            ) {
                return event
            }
            lastKeyboardNavAt = Date()
            // Dispatch via the Direction switch (#339). Exhaustive
            // over `Direction?` today; when a future `.left` /
            // `.right` case is added to `Direction`, this switch
            // stops compiling until the new case is handled — so a
            // new arrow can't silently fall through to the wrong
            // action. `nil` is the not-a-navigable-arrow case;
            // `shouldPassThroughArrow` already filtered those out
            // above, so it's an explicit no-op rather than a path
            // we expect to hit.
            switch Self.ArrowKey.direction(for: event.keyCode) {
            case .up:
                model.selectPrevious()
            case .down:
                model.selectNext()
            case nil:
                break
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

    // MARK: - Invoke

    /// Triggered by `onSubmit` (Enter in the search field).
    /// Resolves the currently-selected command and hands off to
    /// `invoke(_:)` — which is where success-dismissal, recents
    /// recording, and the stays-open-on-error behaviour actually
    /// live. No-op if nothing is selected.
    private func invokeSelected() {
        guard let id = model.selectedID,
              let command = model.displayOrder.first(where: { $0.id == id })
        else { return }
        invoke(command)
    }

    /// Run `command` through the registry. On success: record it to
    /// recents (#316) so it surfaces in the Recent section next
    /// launch, then dismiss. On failure: stay open and let the
    /// model's pending announcement surface the error (#315).
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

// `accessibilityIsSelected(_:)` View extension lives in
// `AccessibilityExtensions.swift` so the other call sites
// (TasksReviewPanel, TasksPanel) can share one source of truth.
