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
///   fuzzy matcher (`slate_core::palette::fuzzy_score` behind the
///   FFI since W0.5-1 #717).
/// - Arrow ↑ / ↓ moves selection with wrap. Modified arrows
///   (`Shift+↓`, `Cmd+↓`, `Ctrl+Option+↓` for VoiceOver Quick Nav,
///   etc.) pass through so text-editing chords and VoiceOver
///   keystrokes keep working.
/// - Home / End select the first / last row; Page Up / Page Down
///   (fn+↑ / fn+↓ on Apple keyboards) move ten rows, stopping at the
///   ends (#1105, Windows parity). Chorded, they pass through too.
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
    /// (#315/#339), Home / End / Page Up / Page Down (#1105), AND bare
    /// Escape dismissal: a focused search field
    /// swallows the raw Escape key before SwiftUI's
    /// `.onExitCommand` fires, so the monitor — which sees the
    /// keyDown first — intercepts Escape and dismisses. (Cmd-. still
    /// reaches `.onExitCommand`, but no user discovers that.)
    @State private var keyDownMonitor: Any? = nil

    /// Timestamp of the most recent keyboard (arrow or page key)
    /// selection change. Hover-update only fires when the mouse moves
    /// AFTER this — prevents stationary-cursor flicker from yanking
    /// selection back to the row under the pointer while the user is
    /// navigating by keyboard.
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
        .background {
            CommandPaletteWindowReader {
                appState.setTemplateShortcutActionLauncherWindow($0)
            }
            .frame(width: 0, height: 0)
            .accessibilityHidden(true)
        }
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
            let disabledReason = Self.disabledReason(
                for: command,
                sidebarActionProjection:
                    appState.sidebarActionProjection(surface: .commandPalette),
                structuralMutationDisabledReason: appState.structuralMutationDisabledReason,
                importCancellationDisabledReason:
                    appState.importCancellationDisabledReason,
                canvasMutationDisabledReason: appState.activeCanvasMutationDisabledReason,
                noteAuthoringDisabledReason: appState.activeNoteAuthoringDisabledReason,
                baseInteractionDisabledReason: appState.activeBaseInteractionDisabledReason,
                baseDefinitionEditingDisabledReason:
                    appState.activeBaseDefinitionEditingDisabledReason,
                baseRefreshDisabledReason: appState.activeBaseRefreshDisabledReason)
            // #418 (F-A1): was .low — anything else speaking (typing
            // echoes, filter-count announcements) superseded it and
            // the VO test heard nothing while arrowing. Medium is
            // the politeness floor that actually survives (pinned on
            // the core event).
            postAccessibilityAnnouncement(
                .paletteCommandSelected(
                    label: command.label, disabledReason: disabledReason))
        }
        .onChange(of: model.pendingAnnouncementText) { _, text in
            guard let text, !text.isEmpty, let event = model.pendingAnnouncement else { return }
            postAccessibilityAnnouncement(event)
            model.clearPendingAnnouncement()
        }
        .onChange(of: model.filterAnnouncementText) { _, text in
            // Filter-change announcement (#316). `.medium` priority
            // (default) so per-keystroke announcements coalesce
            // gracefully — VoiceOver supersedes the in-flight one
            // when a new one arrives. `.high` would interrupt
            // mid-word and produce "1—5—12 commands matching"
            // garbage at typing speed. Matches the
            // `SearchOverlay.swift` precedent.
            guard let text, !text.isEmpty, let event = model.filterAnnouncement else { return }
            postAccessibilityAnnouncement(event)
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
        let disabledReason = Self.disabledReason(
            for: command,
            sidebarActionProjection:
                appState.sidebarActionProjection(surface: .commandPalette),
            structuralMutationDisabledReason: appState.structuralMutationDisabledReason,
            importCancellationDisabledReason:
                appState.importCancellationDisabledReason,
            canvasMutationDisabledReason: appState.activeCanvasMutationDisabledReason,
            noteAuthoringDisabledReason: appState.activeNoteAuthoringDisabledReason,
            baseInteractionDisabledReason: appState.activeBaseInteractionDisabledReason,
            baseDefinitionEditingDisabledReason:
                appState.activeBaseDefinitionEditingDisabledReason,
            baseRefreshDisabledReason: appState.activeBaseRefreshDisabledReason)
        return Button {
            invoke(command)
        } label: {
            VStack(alignment: .leading, spacing: 3) {
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
                if let disabledReason {
                    Text(disabledReason)
                        .font(.caption)
                        .foregroundStyle(
                            isSelected
                                ? Color(nsColor: .selectedMenuItemTextColor)
                                : Color(nsColor: .secondaryLabelColor))
                        .lineLimit(2)
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
        .disabled(disabledReason != nil)
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
        .accessibilityHint(disabledReason ?? command.accessibilityHint ?? "")
        .help(disabledReason ?? command.accessibilityHint ?? "")
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

    /// One availability resolver drives the visible row state, the selection
    /// announcement, and Return routing so those surfaces cannot disagree.
    static func disabledReason(
        for command: Command,
        sidebarActionProjection: [SidebarActionEvaluation] = [],
        structuralMutationDisabledReason: String?,
        importCancellationDisabledReason: String? = nil,
        canvasMutationDisabledReason: String? = nil,
        noteAuthoringDisabledReason: String? = nil,
        baseInteractionDisabledReason: String? = nil,
        baseDefinitionEditingDisabledReason: String? = nil,
        baseRefreshDisabledReason: String? = nil
    ) -> String? {
        if let evaluation = sidebarActionProjection.first(where: {
            $0.id == command.id
        }) {
            return evaluation.disabledReason
        }
        if command.id == SlateCommandID.cancelImport {
            return importCancellationDisabledReason
        }
        if SlateCommandID.structuralMutationCommands.contains(command.id),
            let structuralMutationDisabledReason
        {
            return structuralMutationDisabledReason
        }
        if SlateCommandID.canvasMutationCommands.contains(command.id) {
            return canvasMutationDisabledReason
        }
        if SlateCommandID.noteAuthoringCommands.contains(command.id) {
            return noteAuthoringDisabledReason
        }
        if SlateCommandID.baseInteractionCommands.contains(command.id) {
            return baseInteractionDisabledReason
        }
        if SlateCommandID.baseDefinitionEditingCommands.contains(command.id) {
            return baseDefinitionEditingDisabledReason
        }
        if command.id == SlateCommandID.basesRefresh {
            return baseRefreshDisabledReason
        }
        return nil
    }

    /// Delegates to the core vocabulary (W0.5-3 #717/#719): the copy is
    /// `slate_core::a11y`'s `PaletteCommandSelected` template; this
    /// wrapper survives only for the tests that pin the rendered shape.
    static func selectionAnnouncement(
        for command: Command,
        disabledReason: String?
    ) -> String {
        a11yRender(
            event: .paletteCommandSelected(
                label: command.label, disabledReason: disabledReason)
        ).text
    }

    /// View-layer Return/click gate. A disabled selection keeps search focus,
    /// announces its exact reason, and never reaches the registry action. The
    /// registry's own guard remains the backstop for non-palette callers.
    @discardableResult
    static func invokeIfAvailable(
        disabledReason: String?,
        restoreSearchFocus: () -> Void,
        announceUnavailable: (String) -> Void,
        invoke: () -> InvocationOutcome
    ) -> InvocationOutcome? {
        restoreSearchFocus()
        if let disabledReason {
            announceUnavailable(disabledReason)
            return nil
        }
        return invoke()
    }

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

    // MARK: - Key monitor (arrows, page keys, Escape)

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

    /// Carbon virtual key codes for the four page-navigation keys
    /// (#1105): `kVK_Home` 115, `kVK_End` 119, `kVK_PageUp` 116 and
    /// `kVK_PageDown` 121, the same stable HIToolbox ABI as the arrow
    /// codes above.
    ///
    /// fn+↑ / fn+↓ / fn+← / fn+→ arrive as these codes, not as arrows:
    /// Apple's keyboard drivers rewrite fn+arrow to the page key before
    /// any event exists (`FnKeyboardUsageMap` in AppleHIDKeyboard.kext
    /// and the built-in keyboard's driver: ↑ → Page Up, ↓ → Page Down,
    /// ← → Home, → → End). So fn+arrow pages with no arrow rule at all.
    enum PageKey {
        static let home: UInt16 = 115     // kVK_Home
        static let end: UInt16 = 119      // kVK_End
        static let pageUp: UInt16 = 116   // kVK_PageUp
        static let pageDown: UInt16 = 121 // kVK_PageDown
    }

    /// The selection move a bare page key makes (#1105): Windows'
    /// `HandleCommandPaletteKey` arms, Home → `SelectFirst`, End →
    /// `SelectLast`, Page Up / Page Down → `MovePage(-1)` / `MovePage(1)`.
    enum PageMove: Equatable {
        case first
        case last
        case pageUp
        case pageDown
    }

    /// The chord modifiers: any of these held on an arrow or a page key
    /// means "let it through" — Shift+↓ and Shift+Home extend text-field
    /// selection, Cmd+↓ jumps caret to end-of-text, **Ctrl+Option+↓ is
    /// VoiceOver Quick Nav** (the one we absolutely must not steal).
    /// Only a bare key moves palette selection, Windows'
    /// `ModifierKeys.None` rule (contract 28 P7). One mask for both
    /// families, so their chord rules cannot drift apart.
    ///
    /// `.function` is not a chord (#1105). macOS puts it on these keys
    /// itself: the system's own key events carry `[.numericPad,
    /// .function]` for a bare ↑ / ↓ and `[.function]` for Home, End,
    /// Page Up and Page Down. A mask that held `.function` passed every
    /// such arrow through to the field. fn+arrow still pages, because
    /// it arrives as a page key (`PageKey`).
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
    private nonisolated static let chordModifierMask: NSEvent.ModifierFlags =
        [.shift, .control, .option, .command]

    /// Decide whether a `.keyDown` event should pass through the
    /// monitor unconsumed. Pure function — no `@State` access — so
    /// the modifier-passthrough contract is unit-testable without
    /// having to synthesise live `NSEvent`s into the run loop.
    ///
    /// Pass through (returns true) when:
    /// - Key is not a navigable arrow (`ArrowKey.direction(for:)`
    ///   returns `nil`)
    /// - Key IS a navigable arrow but any modifier in
    ///   `chordModifierMask` is held (Shift, Ctrl, Option, Cmd —
    ///   covers text-field selection, caret-jump, and VoiceOver
    ///   Quick Nav).
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
        return !modifierFlags.intersection(chordModifierMask).isEmpty
    }

    /// Map a `.keyDown` to the page move it makes, or `nil` when the key
    /// is not a page key or a chord modifier is held (#1105). Pure, like
    /// `shouldPassThroughArrow`, so the modifier rule is unit-testable.
    /// A chord (Shift+Home, Cmd+End, VoiceOver's Control+Option keys)
    /// belongs to the search field or the AT, so it passes through;
    /// `.function`, which these keys carry themselves, and device state
    /// such as Caps Lock are not chords.
    nonisolated static func pageNavigation(
        keyCode: UInt16,
        modifierFlags: NSEvent.ModifierFlags
    ) -> PageMove? {
        guard modifierFlags.intersection(chordModifierMask).isEmpty else { return nil }
        switch keyCode {
        case PageKey.home: return .first
        case PageKey.end: return .last
        case PageKey.pageUp: return .pageUp
        case PageKey.pageDown: return .pageDown
        default: return nil
        }
    }

    /// What the keyDown monitor does with one key event (#1105).
    enum KeyRoute: Equatable {
        /// Let the event reach the search field (or the input method).
        case passThrough
        /// Bare Escape: close the palette.
        case dismiss
        /// A bare page key: move the selection by page, or to an end.
        case page(PageMove)
        /// A bare ↑ / ↓: move the selection one row, wrapping.
        case arrow(ArrowKey.Direction)
    }

    /// The monitor's whole decision for one `.keyDown`, pure so its
    /// order of checks is unit-testable without live `NSEvent`s (#1105):
    ///
    /// 1. **Marked text passes everything through.** While an input
    ///    method is composing, Escape must reach the input context to
    ///    cancel the composition and the arrow and page keys drive the
    ///    candidate window, so intercepting nothing keeps an IME user
    ///    from being stranded mid-composition (red-team, #393).
    /// 2. Bare Escape dismisses (`isDismissKey`), ahead of the field
    ///    that would otherwise eat it.
    /// 3. A bare page key moves by page (`pageNavigation`).
    /// 4. A bare ↑ / ↓ moves one row (`shouldPassThroughArrow`,
    ///    `ArrowKey.direction(for:)`).
    /// 5. Anything else passes through: typing, Return (the field's
    ///    `onSubmit` runs it) and every chord.
    nonisolated static func keyRoute(
        keyCode: UInt16,
        modifierFlags: NSEvent.ModifierFlags,
        hasMarkedText: Bool
    ) -> KeyRoute {
        if hasMarkedText { return .passThrough }
        if isDismissKey(keyCode: keyCode, modifierFlags: modifierFlags) { return .dismiss }
        if let move = pageNavigation(keyCode: keyCode, modifierFlags: modifierFlags) {
            return .page(move)
        }
        if !shouldPassThroughArrow(keyCode: keyCode, modifierFlags: modifierFlags),
            let direction = ArrowKey.direction(for: keyCode)
        {
            return .arrow(direction)
        }
        return .passThrough
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
    /// The check-set omits `.function`, as `chordModifierMask` does:
    /// it is no chord (macOS sets it on the arrow and page keys
    /// itself), and Escape never carries it. fn+Escape therefore still
    /// dismisses, which is intentional and harmless.
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
            // `keyRoute` decides (pure, unit-tested): marked text in
            // the field editor first, then Escape, the page keys and
            // the bare arrows; everything else passes through.
            let hasMarkedText =
                (event.window?.firstResponder as? NSTextView)?.hasMarkedText() == true
            switch Self.keyRoute(
                keyCode: event.keyCode,
                modifierFlags: event.modifierFlags,
                hasMarkedText: hasMarkedText
            ) {
            case .passThrough:
                return event
            case .dismiss:
                appState.isCommandPaletteOpen = false
                return nil
            case .page(let move):
                lastKeyboardNavAt = Date()
                switch move {
                case .first:
                    model.selectFirst()
                case .last:
                    model.selectLast()
                case .pageUp:
                    model.movePage(-1)
                case .pageDown:
                    model.movePage(1)
                }
                return nil
            case .arrow(let direction):
                lastKeyboardNavAt = Date()
                // Exhaustive over `Direction` (#339): when a future
                // `.left` / `.right` case is added, this switch stops
                // compiling until the new case is handled, so a new
                // arrow can't silently fall through to the wrong action.
                switch direction {
                case .up:
                    model.selectPrevious()
                case .down:
                    model.selectNext()
                }
                return nil
            }
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
        let disabledReason = Self.disabledReason(
            for: command,
            sidebarActionProjection:
                appState.sidebarActionProjection(surface: .commandPalette),
            structuralMutationDisabledReason: appState.structuralMutationDisabledReason,
            importCancellationDisabledReason:
                appState.importCancellationDisabledReason,
            canvasMutationDisabledReason: appState.activeCanvasMutationDisabledReason,
            noteAuthoringDisabledReason: appState.activeNoteAuthoringDisabledReason,
            baseInteractionDisabledReason: appState.activeBaseInteractionDisabledReason,
            baseDefinitionEditingDisabledReason:
                appState.activeBaseDefinitionEditingDisabledReason,
            baseRefreshDisabledReason: appState.activeBaseRefreshDisabledReason)
        guard let outcome = Self.invokeIfAvailable(
            disabledReason: disabledReason,
            restoreSearchFocus: { searchFocused = true },
            announceUnavailable: {
                postAccessibilityAnnouncement(.paletteCommandUnavailable(reason: $0))
            },
            invoke: {
                model.invoke(command, via: appState.commandRegistry)
            })
        else { return }
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

/// Registers the palette's exact AppKit sheet window. AppState uses identity,
/// not a presentation Bool, so a stale/competing sheet can never inherit the
/// palette's permission to launch another action surface.
private struct CommandPaletteWindowReader: NSViewRepresentable {
    let windowChanged: (NSWindow?) -> Void

    func makeNSView(context: Context) -> WindowReaderView {
        let view = WindowReaderView()
        view.windowChanged = windowChanged
        return view
    }

    func updateNSView(_ view: WindowReaderView, context: Context) {
        view.windowChanged = windowChanged
    }

    static func dismantleNSView(_ view: WindowReaderView, coordinator: ()) {
        view.stop()
    }

    final class WindowReaderView: NSView {
        var windowChanged: (NSWindow?) -> Void = { _ in }
        private var isActive = true
        override var intrinsicContentSize: NSSize { .zero }

        func stop() {
            isActive = false
            windowChanged(nil)
        }

        override func viewDidMoveToWindow() {
            super.viewDidMoveToWindow()
            guard isActive else { return }
            let currentWindow = window
            // Publish after SwiftUI finishes mounting this representable; a
            // synchronous object change during view installation is illegal.
            DispatchQueue.main.async { [weak self] in
                guard let self, self.isActive, self.window === currentWindow
                else { return }
                self.windowChanged(currentWindow)
            }
        }
    }
}

// `accessibilityIsSelected(_:)` View extension lives in
// `AccessibilityExtensions.swift` so the other call sites
// (TasksReviewPanel, TasksPanel) can share one source of truth.
