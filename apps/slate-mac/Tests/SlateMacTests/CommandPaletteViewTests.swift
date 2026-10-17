// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import SwiftUI
import XCTest

@testable import SlateMac

/// Tests for the Milestone Q command palette (#313 + #314 + #315).
///
/// What's covered here:
///
/// 1. **AppState wiring** (#313) — `isCommandPaletteOpen` default,
///    public mutability, and `closeVault()` reset.
/// 2. **APCA contrast** (#313 + #315) — `labelColor` /
///    `secondaryLabelColor` over `controlBackgroundColor` and
///    `selectedMenuItemTextColor` over
///    `selectedContentBackgroundColor` all clear `|Lc| > 75`.
/// 3. **View construction** — `CommandPaletteView` loads without
///    crashing.
/// 4. **VoiceOver chord composition** — `voiceOverLabel(for:)`.
/// 5. **Fuzzy matcher** (#315) — subsequence + boundary + prefix +
///    consecutive scoring.
/// 6. **Selection navigation** (#315) — arrow wrap via the model's
///    `selectNext`/`selectPrevious`; Home, End, Page Up and Page Down
///    (#1105) via `selectFirst`/`selectLast`/`movePage`, clamped.
/// 7. **Enter dispatch + ActionFailed announcement** (#315) — model
///    `invoke` returns the right `InvocationOutcome` and stores the
///    pending announcement on error.
///
/// What's NOT covered here (rides the Milestone Q integration suite,
/// #317): the SwiftUI shortcut routing, `.onSubmit` actually firing,
/// `NSEvent.addLocalMonitorForEvents` intercepting real keystrokes,
/// `.onAppear` auto-focus.
final class CommandPaletteViewTests: XCTestCase {

    // MARK: - AppState wiring (#313)

    @MainActor
    func testIsCommandPaletteOpenDefaultsToFalse() async {
        let appState = AppState()
        XCTAssertFalse(appState.isCommandPaletteOpen, "palette starts closed")
    }

    @MainActor
    func testIsCommandPaletteOpenIsPublishedAndMutable() async {
        let appState = AppState()
        appState.isCommandPaletteOpen = true
        XCTAssertTrue(appState.isCommandPaletteOpen)
        appState.isCommandPaletteOpen = false
        XCTAssertFalse(appState.isCommandPaletteOpen)
    }

    @MainActor
    func testCloseVaultResetsIsCommandPaletteOpen() async {
        let appState = AppState()
        appState.isCommandPaletteOpen = true
        appState.closeVault()
        XCTAssertFalse(
            appState.isCommandPaletteOpen,
            "closeVault must reset isCommandPaletteOpen so the next vault open doesn't auto-present"
        )
    }

    /// `requestCommandPalette()` with no vault open must NOT flip
    /// `isCommandPaletteOpen` — the palette sheet is only mounted by
    /// MainSplitView, so flipping the bool on the welcome screen
    /// would re-present it on the next vault open (#313/#328 hazard).
    /// It posts an accessibility announcement instead. (The "open a
    /// vault first" feedback was added after live debugging found
    /// ⌘⇧P was a silent no-op on the welcome screen.)
    @MainActor
    func testRequestCommandPaletteWithNoVaultDoesNotOpen() async {
        let appState = AppState()
        XCTAssertFalse(appState.isVaultOpen, "fresh AppState has no vault")
        appState.requestCommandPalette()
        XCTAssertFalse(
            appState.isCommandPaletteOpen,
            "requestCommandPalette must not open the palette with no vault — only announce"
        )
    }

    // MARK: - APCA contrast (#313 + #315)

    func testLabelColorClearsAPCAAgainstControlBackground() {
        for appearanceName in [NSAppearance.Name.darkAqua, .aqua] {
            guard let appearance = NSAppearance(named: appearanceName) else { continue }
            var fg = NSColor.black, bg = NSColor.white
            appearance.performAsCurrentDrawingAppearance {
                fg = NSColor.labelColor.usingColorSpace(.sRGB)!
                bg = NSColor.controlBackgroundColor.usingColorSpace(.sRGB)!
            }
            let lc = APCAContrast.lc(text: fg, background: bg)
            XCTAssertGreaterThan(
                abs(lc), 75,
                "labelColor vs controlBackgroundColor under \(appearanceName.rawValue) must clear APCA |Lc| > 75 (got Lc \(lc))"
            )
        }
    }

    func testSecondaryLabelColorClearsAPCAAgainstControlBackground() {
        for appearanceName in [NSAppearance.Name.darkAqua, .aqua] {
            guard let appearance = NSAppearance(named: appearanceName) else { continue }
            var fg = NSColor.black, bg = NSColor.white
            appearance.performAsCurrentDrawingAppearance {
                fg = NSColor.secondaryLabelColor.usingColorSpace(.sRGB)!
                bg = NSColor.controlBackgroundColor.usingColorSpace(.sRGB)!
            }
            let lc = APCAContrast.lc(text: fg, background: bg)
            XCTAssertGreaterThan(
                abs(lc), 75,
                "secondaryLabelColor vs controlBackgroundColor under \(appearanceName.rawValue) must clear APCA |Lc| > 75 (got Lc \(lc))"
            )
        }
    }

    /// Selected-row pairing: `selectedMenuItemTextColor` over
    /// `selectedContentBackgroundColor`. Introduced by #315's row
    /// highlight; must also clear the project's APCA bar.
    func testSelectedRowColorsClearAPCA() {
        for appearanceName in [NSAppearance.Name.darkAqua, .aqua] {
            guard let appearance = NSAppearance(named: appearanceName) else { continue }
            var fg = NSColor.black, bg = NSColor.white
            appearance.performAsCurrentDrawingAppearance {
                fg = NSColor.selectedMenuItemTextColor.usingColorSpace(.sRGB)!
                bg = NSColor.selectedContentBackgroundColor.usingColorSpace(.sRGB)!
            }
            let lc = APCAContrast.lc(text: fg, background: bg)
            XCTAssertGreaterThan(
                abs(lc), 75,
                "selectedMenuItemTextColor vs selectedContentBackgroundColor under \(appearanceName.rawValue) must clear APCA |Lc| > 75 (got Lc \(lc))"
            )
        }
    }

    // MARK: - View construction smoke

    @MainActor
    func testCommandPaletteViewLoadsWithoutCrashing() async {
        let appState = AppState()
        let view = CommandPaletteView().environmentObject(appState)
        let host = NSHostingController(rootView: view)
        _ = host.view
        XCTAssertNotNil(host.view)
    }

    // MARK: - VoiceOver chord composition

    func testVoiceOverLabelComposesChordIntoSpokenString() {
        let cases: [(label: String, hint: String?, expected: String)] = [
            ("Save", "⌘S", "Save, Command S"),
            ("New from Template…", "⇧⌘N", "New from Template…, Shift Command N"),
            ("Search", "⌘F", "Search, Command F"),
            ("Citation Summary", "⇧⌘J", "Citation Summary, Shift Command J"),
            ("Close Vault", nil, "Close Vault"),
            ("Plain Command", "", "Plain Command"),
            // Settings (slate.settings.open, #320) — chord ends in
            // ',' which VoiceOver may elide at punctuation = None.
            // Spelling out "Comma" makes it pronounceable at every
            // VO punctuation setting.
            ("Settings…", "⌘,", "Settings…, Command Comma"),
            // Hypothetical future plugin command using "/".
            ("Open Help", "⌘/", "Open Help, Command Slash"),
        ]
        for (label, hint, expected) in cases {
            let cmd = Command(
                id: "test.\(label)",
                label: label,
                accessibilityHint: nil,
                hotkeyHint: hint,
                section: .editor
            )
            XCTAssertEqual(
                CommandPaletteView.voiceOverLabel(for: cmd),
                expected,
                "voiceOverLabel for \(label) / \(hint ?? "nil") drifted"
            )
        }
    }

    // MARK: - .isSelected trait accumulation (#324)
    //
    // An empirical NSHostingController-based test for SwiftUI
    // `.isSelected` trait accumulation across renders was attempted
    // and abandoned: `swift test` doesn't have a real AX consumer
    // attached, so SwiftUI never populates the AppKit accessibility
    // metadata layer fully. A positive control (rendering with
    // selected=true and asserting `accessibilitySelected = true`
    // on the AX tree) failed — the trait isn't queryable from
    // `swift test` regardless of the SwiftUI declaration.
    //
    // The substantive fix instead lives in `CommandPaletteView.swift`:
    // the `.accessibilityIsSelected(_:)` View extension uses a
    // `@ViewBuilder` conditional so the
    // `.accessibilityAddTraits(.isSelected)` modifier is only in
    // the view's modifier chain when selected. The `[]` argument
    // that prompted the original "does this accumulate?" question
    // doesn't exist anywhere. Correct by construction.
    //
    // If a future PR introduces XCUITest infrastructure (a real
    // SwiftUI accessibility runner), revisit this with a proper
    // end-to-end assertion. For now, the structural change is the
    // closure — see #324 PR description for the full reasoning.

    // MARK: - Arrow-key modifier passthrough (#315 review follow-up)

    /// Locks in the contract that bare ↑ / ↓ moves palette
    /// selection while every modified arrow chord passes through
    /// unconsumed. This is load-bearing for screen-reader users
    /// (Ctrl+Option+↓ is VoiceOver Quick Nav) and for text-field
    /// editing inside the search field (Shift+↓ extends, Cmd+↓
    /// jumps caret). Fn+↓ is Page Down, a page move since #1105
    /// (`testFnArrowIsAPageMove`). A regression here would
    /// silently break a11y; the test exists so the modifier mask
    /// can't drift without CI flagging it.

    func testBareArrowDownIsConsumed() {
        XCTAssertFalse(
            CommandPaletteView.shouldPassThroughArrow(
                keyCode: CommandPaletteView.ArrowKey.down,
                modifierFlags: []
            ),
            "bare ↓ must be consumed by the palette monitor"
        )
    }

    func testBareArrowUpIsConsumed() {
        XCTAssertFalse(
            CommandPaletteView.shouldPassThroughArrow(
                keyCode: CommandPaletteView.ArrowKey.up,
                modifierFlags: []
            ),
            "bare ↑ must be consumed by the palette monitor"
        )
    }

    /// Flipped by #1105 (was `testFnArrowPassesThroughForPageNav`):
    /// Fn+↓ / Fn+↑ are Page Down / Page Up, and the palette pages.
    /// Apple's keyboard drivers rewrite fn+arrow to the page key
    /// before any event exists (`FnKeyboardUsageMap` in
    /// AppleHIDKeyboard.kext and the built-in keyboard's driver:
    /// ↑ → Page Up, ↓ → Page Down, ← → Home, → → End), so the monitor
    /// receives the page key's code carrying `.function`, the same
    /// event a dedicated Page Down / Page Up key sends.
    func testFnArrowIsAPageMove() {
        XCTAssertEqual(
            CommandPaletteView.pageNavigation(
                keyCode: CommandPaletteView.PageKey.pageDown,
                modifierFlags: Self.systemPageKeyFlags
            ),
            .pageDown,
            "Fn+↓ (macOS Page Down) must move the selection down a page"
        )
        XCTAssertEqual(
            CommandPaletteView.pageNavigation(
                keyCode: CommandPaletteView.PageKey.pageUp,
                modifierFlags: Self.systemPageKeyFlags
            ),
            .pageUp,
            "Fn+↑ (macOS Page Up) must move the selection up a page"
        )
        XCTAssertEqual(
            CommandPaletteView.pageNavigation(
                keyCode: CommandPaletteView.PageKey.home,
                modifierFlags: Self.systemPageKeyFlags
            ),
            .first,
            "Fn+← (macOS Home) must select the first row"
        )
        XCTAssertEqual(
            CommandPaletteView.pageNavigation(
                keyCode: CommandPaletteView.PageKey.end,
                modifierFlags: Self.systemPageKeyFlags
            ),
            .last,
            "Fn+→ (macOS End) must select the last row"
        )
    }

    func testShiftArrowPassesThroughForSelectionExtend() {
        XCTAssertTrue(
            CommandPaletteView.shouldPassThroughArrow(
                keyCode: CommandPaletteView.ArrowKey.down,
                modifierFlags: .shift
            ),
            "Shift+↓ (extend text-field selection) must pass through unconsumed"
        )
    }

    func testCommandArrowPassesThroughForCaretJump() {
        XCTAssertTrue(
            CommandPaletteView.shouldPassThroughArrow(
                keyCode: CommandPaletteView.ArrowKey.down,
                modifierFlags: .command
            ),
            "Cmd+↓ (caret to end-of-text) must pass through unconsumed"
        )
    }

    func testCtrlOptArrowPassesThroughForVoiceOverQuickNav() {
        // Ctrl+Option+↓ is VoiceOver Quick Nav — the screen-reader
        // chord we absolutely cannot intercept. Failing this test
        // means we just broke VO navigation for blind users.
        XCTAssertTrue(
            CommandPaletteView.shouldPassThroughArrow(
                keyCode: CommandPaletteView.ArrowKey.down,
                modifierFlags: [.control, .option]
            ),
            "Ctrl+Option+↓ (VoiceOver Quick Nav) MUST pass through"
        )
    }

    func testNonArrowKeyPassesThrough() {
        // Any key that isn't ↑ / ↓ has nothing to do with palette
        // selection.
        XCTAssertTrue(
            CommandPaletteView.shouldPassThroughArrow(keyCode: 0 /* 'a' */, modifierFlags: []),
            "non-arrow keys must always pass through"
        )
        XCTAssertTrue(
            CommandPaletteView.shouldPassThroughArrow(keyCode: 36 /* Return */, modifierFlags: []),
            "Return is handled by the search field's onSubmit, not the monitor"
        )
    }

    /// #325 follow-up: device-dependent flags like `.capsLock`
    /// and `.numericPad` shouldn't affect chord routing. The
    /// `chordModifierMask` is deliberately narrow — it doesn't
    /// contain `.capsLock` or `.numericPad`, so the intersection
    /// is empty for those flags alone. (We initially tried
    /// `.deviceIndependentFlagsMask` normalization but the mask is
    /// `0xffff0000UL` and capsLock/numericPad live in the high 16
    /// bits, so they survive — the normalization would have been a
    /// no-op for these flags. The correct mechanism is mask
    /// narrowness, locked in by these regression tests.)
    func testCapsLockOnBareArrowIsStillConsumed() {
        XCTAssertFalse(
            CommandPaletteView.shouldPassThroughArrow(
                keyCode: CommandPaletteView.ArrowKey.down,
                modifierFlags: .capsLock
            ),
            "Caps Lock isn't in chordModifierMask; bare ↓ with caps lock still navigates the palette"
        )
    }

    func testNumericPadFlagOnBareArrowIsStillConsumed() {
        // macOS flags arrow keys .numericPad (with .function, see
        // the next fact). Doesn't change semantics — still a bare arrow.
        XCTAssertFalse(
            CommandPaletteView.shouldPassThroughArrow(
                keyCode: CommandPaletteView.ArrowKey.up,
                modifierFlags: .numericPad
            ),
            ".numericPad isn't in chordModifierMask; bare ↑ with it set still navigates"
        )
    }

    /// #1105: macOS puts `.function` on every bare arrow itself. The
    /// system's own key events for ↑ / ↓ carry `[.numericPad,
    /// .function]` (`systemArrowFlags`), so `.function` is no chord: a
    /// bare arrow carrying it still moves one row, and an arrow is
    /// never a page move (fn+arrow arrives as a page key,
    /// `testFnArrowIsAPageMove`). Before #1105 the mask held
    /// `.function`, which passed these events through to the field.
    func testFunctionFlagOnBareArrowIsStillConsumed() {
        for keyCode in [CommandPaletteView.ArrowKey.up, CommandPaletteView.ArrowKey.down] {
            XCTAssertFalse(
                CommandPaletteView.shouldPassThroughArrow(
                    keyCode: keyCode,
                    modifierFlags: Self.systemArrowFlags
                ),
                "a bare arrow as macOS sends it (key \(keyCode)) must move the palette selection"
            )
            XCTAssertFalse(
                CommandPaletteView.shouldPassThroughArrow(
                    keyCode: keyCode,
                    modifierFlags: .function
                ),
                ".function alone on key \(keyCode) is not a chord"
            )
            XCTAssertNil(
                CommandPaletteView.pageNavigation(
                    keyCode: keyCode,
                    modifierFlags: Self.systemArrowFlags
                ),
                "an arrow (key \(keyCode)) is never a page move"
            )
        }
    }

    func testCapsLockPlusShiftArrowStillPassesThrough() {
        // Combined: Caps Lock (not in mask) + Shift (in mask).
        // Shift alone is enough to trip the passthrough.
        XCTAssertTrue(
            CommandPaletteView.shouldPassThroughArrow(
                keyCode: CommandPaletteView.ArrowKey.down,
                modifierFlags: [.capsLock, .shift]
            ),
            "Caps + Shift + ↓ — Shift IS in chordModifierMask; chord passes through"
        )
    }

    // MARK: - ArrowKey.direction(for:) (#339)
    //
    // The keycode→Direction map is the single source of truth for
    // "which arrows the palette navigates with". Both
    // shouldPassThroughArrow and the monitor dispatch consult it,
    // so pin it directly.

    func testDirectionMapsUpKeycode() {
        XCTAssertEqual(
            CommandPaletteView.ArrowKey.direction(for: CommandPaletteView.ArrowKey.up),
            .up
        )
    }

    func testDirectionMapsDownKeycode() {
        XCTAssertEqual(
            CommandPaletteView.ArrowKey.direction(for: CommandPaletteView.ArrowKey.down),
            .down
        )
    }

    func testDirectionIsNilForLeftRightAndNonArrowKeys() {
        // Left (123) / Right (124) are NOT navigable today — they
        // map to nil, so shouldPassThroughArrow lets them through.
        // This pins the "no horizontal navigation yet" contract: if
        // a future change adds .left/.right to Direction, it must
        // update this test too (and the dispatch switch stops
        // compiling until the new cases are handled — #339).
        XCTAssertNil(CommandPaletteView.ArrowKey.direction(for: 123 /* kVK_LeftArrow */))
        XCTAssertNil(CommandPaletteView.ArrowKey.direction(for: 124 /* kVK_RightArrow */))
        XCTAssertNil(CommandPaletteView.ArrowKey.direction(for: 0 /* 'a' */))
        XCTAssertNil(CommandPaletteView.ArrowKey.direction(for: 36 /* Return */))
    }

    func testDirectionDrivesPassThroughForLeftRight() {
        // Cross-check: because left/right map to nil, they pass
        // through the monitor unconsumed (the palette ignores them).
        XCTAssertTrue(
            CommandPaletteView.shouldPassThroughArrow(keyCode: 123, modifierFlags: []),
            "bare ← isn't navigable today; must pass through"
        )
        XCTAssertTrue(
            CommandPaletteView.shouldPassThroughArrow(keyCode: 124, modifierFlags: []),
            "bare → isn't navigable today; must pass through"
        )
    }

    // MARK: - Page navigation keys (#1105)
    //
    // `pageNavigation(keyCode:modifierFlags:)`: Home, End, Page Up and
    // Page Down are selection moves only bare, with no chord modifier
    // held (Windows' `ModifierKeys.None` rule, contract 28 P7), so the
    // search field keeps its Shift and Command chords and VoiceOver's
    // Control+Option keys reach the AT.

    func testBarePageKeysMapToTheirMoves() {
        let keys: [(UInt16, CommandPaletteView.PageMove, String)] = [
            (CommandPaletteView.PageKey.home, .first, "Home"),
            (CommandPaletteView.PageKey.end, .last, "End"),
            (CommandPaletteView.PageKey.pageUp, .pageUp, "Page Up"),
            (CommandPaletteView.PageKey.pageDown, .pageDown, "Page Down"),
        ]
        for (keyCode, move, name) in keys {
            // As macOS sends it: the key carries `.function` itself.
            XCTAssertEqual(
                CommandPaletteView.pageNavigation(
                    keyCode: keyCode, modifierFlags: Self.systemPageKeyFlags),
                move, "bare \(name)")
            // A synthesized event without the flag is the same key.
            XCTAssertEqual(
                CommandPaletteView.pageNavigation(keyCode: keyCode, modifierFlags: []),
                move, "\(name) without .function")
            // Device state is not a chord, as for the arrows.
            XCTAssertEqual(
                CommandPaletteView.pageNavigation(
                    keyCode: keyCode, modifierFlags: Self.systemPageKeyFlags.union(.capsLock)),
                move, "\(name) with Caps Lock on")
        }
        XCTAssertNil(CommandPaletteView.pageNavigation(keyCode: 0 /* 'a' */, modifierFlags: []))
        XCTAssertNil(CommandPaletteView.pageNavigation(keyCode: 36 /* Return */, modifierFlags: []))
        XCTAssertNil(
            CommandPaletteView.pageNavigation(
                keyCode: 123 /* kVK_LeftArrow */, modifierFlags: Self.systemArrowFlags))
    }

    func testShiftHomeAndCommandEndPassThrough() {
        let shiftHome = Self.systemPageKeyFlags.union(.shift)
        let commandEnd = Self.systemPageKeyFlags.union(.command)
        XCTAssertNil(
            CommandPaletteView.pageNavigation(
                keyCode: CommandPaletteView.PageKey.home, modifierFlags: shiftHome),
            "Shift+Home is the search field's, not a selection move")
        XCTAssertEqual(
            CommandPaletteView.keyRoute(
                keyCode: CommandPaletteView.PageKey.home, modifierFlags: shiftHome,
                hasMarkedText: false),
            .passThrough)
        XCTAssertNil(
            CommandPaletteView.pageNavigation(
                keyCode: CommandPaletteView.PageKey.end, modifierFlags: commandEnd),
            "Cmd+End is the search field's, not a selection move")
        XCTAssertEqual(
            CommandPaletteView.keyRoute(
                keyCode: CommandPaletteView.PageKey.end, modifierFlags: commandEnd,
                hasMarkedText: false),
            .passThrough)
    }

    /// Every chord passes every page key through, VoiceOver's
    /// Control+Option keys included. Only `.function` (which the keys
    /// carry themselves) and device state are not chords.
    func testAnyChordModifierPassesAPageKeyThrough() {
        let chords: [(NSEvent.ModifierFlags, String)] = [
            (.shift, "Shift"), (.control, "Control"), (.option, "Option"),
            (.command, "Command"), ([.control, .option], "Control+Option"),
            ([.control, .option, .shift], "Control+Option+Shift"),
        ]
        let keys: [UInt16] = [
            CommandPaletteView.PageKey.home, CommandPaletteView.PageKey.end,
            CommandPaletteView.PageKey.pageUp, CommandPaletteView.PageKey.pageDown,
        ]
        for keyCode in keys {
            for (chord, name) in chords {
                XCTAssertNil(
                    CommandPaletteView.pageNavigation(
                        keyCode: keyCode, modifierFlags: Self.systemPageKeyFlags.union(chord)),
                    "\(name) with key \(keyCode) must pass through")
                XCTAssertNil(
                    CommandPaletteView.pageNavigation(keyCode: keyCode, modifierFlags: chord),
                    "\(name) with key \(keyCode) and no .function must pass through")
            }
        }
    }

    // MARK: - Key route (#1105)
    //
    // `keyRoute(keyCode:modifierFlags:hasMarkedText:)` is the keyDown
    // monitor's whole decision, in its order of checks: marked text
    // passes everything through, then bare Escape dismisses, then a
    // page key moves by page, then a bare arrow moves one row; anything
    // else reaches the search field.

    func testKeyRouteSendsEachKeyToItsHandler() {
        let cases: [(UInt16, NSEvent.ModifierFlags, CommandPaletteView.KeyRoute, String)] = [
            (CommandPaletteView.escapeKeyCode, [], .dismiss, "bare Escape"),
            (CommandPaletteView.PageKey.home, Self.systemPageKeyFlags, .page(.first), "Home"),
            (CommandPaletteView.PageKey.end, Self.systemPageKeyFlags, .page(.last), "End"),
            (CommandPaletteView.PageKey.pageUp, Self.systemPageKeyFlags, .page(.pageUp), "Page Up"),
            (CommandPaletteView.PageKey.pageDown, Self.systemPageKeyFlags, .page(.pageDown), "Page Down"),
            (CommandPaletteView.ArrowKey.up, Self.systemArrowFlags, .arrow(.up), "bare ↑"),
            (CommandPaletteView.ArrowKey.down, Self.systemArrowFlags, .arrow(.down), "bare ↓"),
            (
                CommandPaletteView.ArrowKey.down, Self.systemArrowFlags.union([.control, .option]),
                .passThrough, "Ctrl+Option+↓ (VoiceOver Quick Nav)"
            ),
            (CommandPaletteView.ArrowKey.down, Self.systemArrowFlags.union(.shift), .passThrough, "Shift+↓"),
            (CommandPaletteView.escapeKeyCode, [.command], .passThrough, "Cmd+Escape"),
            (0 /* 'a' */, [], .passThrough, "a typed letter"),
            (36 /* Return */, [], .passThrough, "Return (the field's onSubmit runs it)"),
        ]
        for (keyCode, flags, expected, name) in cases {
            XCTAssertEqual(
                CommandPaletteView.keyRoute(
                    keyCode: keyCode, modifierFlags: flags, hasMarkedText: false),
                expected, "\(name) routed wrong")
        }
    }

    /// The IME guard comes first (#393's red team): while an input
    /// method is composing, Escape must reach it to cancel the
    /// composition and the arrow and page keys drive its candidate
    /// window, so the monitor intercepts nothing.
    func testMarkedTextPassesEveryKeyThrough() {
        let keys: [(UInt16, NSEvent.ModifierFlags)] = [
            (CommandPaletteView.escapeKeyCode, []),
            (CommandPaletteView.ArrowKey.up, Self.systemArrowFlags),
            (CommandPaletteView.ArrowKey.down, Self.systemArrowFlags),
            (CommandPaletteView.PageKey.home, Self.systemPageKeyFlags),
            (CommandPaletteView.PageKey.end, Self.systemPageKeyFlags),
            (CommandPaletteView.PageKey.pageUp, Self.systemPageKeyFlags),
            (CommandPaletteView.PageKey.pageDown, Self.systemPageKeyFlags),
        ]
        for (keyCode, flags) in keys {
            XCTAssertEqual(
                CommandPaletteView.keyRoute(
                    keyCode: keyCode, modifierFlags: flags, hasMarkedText: true),
                .passThrough, "key \(keyCode) must reach the input method while it composes")
        }
    }

    // MARK: - Escape-to-dismiss (live-debugging fix)
    //
    // The keyDown monitor intercepts bare Escape because a focused
    // search field swallows it before `.onExitCommand` fires. These
    // pin the pure decision; the actual monitor-beats-field-editor
    // routing is verified live (it can't be reached from XCTest).

    func testIsDismissKeyOnBareEscape() {
        XCTAssertTrue(
            CommandPaletteView.isDismissKey(
                keyCode: CommandPaletteView.escapeKeyCode, modifierFlags: []
            ),
            "bare Escape must dismiss the palette"
        )
    }

    func testIsDismissKeyIgnoredWithCommandModifier() {
        // Cmd+Esc is a system chord — don't steal it.
        XCTAssertFalse(
            CommandPaletteView.isDismissKey(
                keyCode: CommandPaletteView.escapeKeyCode, modifierFlags: [.command]
            )
        )
    }

    func testIsDismissKeyIgnoredWithShiftModifier() {
        XCTAssertFalse(
            CommandPaletteView.isDismissKey(
                keyCode: CommandPaletteView.escapeKeyCode, modifierFlags: [.shift]
            ),
            "only BARE Escape dismisses"
        )
    }

    func testIsDismissKeyFalseForNonEscapeKeys() {
        // Arrows and ordinary keys must never trigger dismissal.
        XCTAssertFalse(
            CommandPaletteView.isDismissKey(
                keyCode: CommandPaletteView.ArrowKey.down, modifierFlags: []
            )
        )
        XCTAssertFalse(
            CommandPaletteView.isDismissKey(keyCode: 0 /* 'a' */, modifierFlags: [])
        )
        XCTAssertFalse(
            CommandPaletteView.isDismissKey(keyCode: 36 /* Return */, modifierFlags: [])
        )
    }

    func testIsDismissKeyIgnoresDeviceDependentFlags() {
        // Caps Lock / numeric-pad / fn aren't "real" chord modifiers
        // (same philosophy as chordModifierMask) — Escape with them
        // set still dismisses.
        XCTAssertTrue(
            CommandPaletteView.isDismissKey(
                keyCode: CommandPaletteView.escapeKeyCode, modifierFlags: [.capsLock]
            ),
            "Caps Lock isn't a meaningful modifier; bare Escape still dismisses"
        )
    }

    // The fuzzy-matcher unit tests (#315) moved to Rust with the
    // matcher itself (W0.5-1 #717): `slate_core::palette::tests`
    // carries the same seven cases as goldens, plus exact-score and
    // match-span pins. Ranking behavior through the model is still
    // exercised end-to-end below and in MilestoneQIntegrationTests.

    // MARK: - Selection navigation (#315)

    @MainActor
    func testSelectNextWrapsAtEnd() async {
        let model = CommandPaletteModel()
        model.loadCommands(fixtureCommands())
        // After load, selection sits on the first command.
        XCTAssertEqual(model.selectedID, "test.a")
        model.selectNext()
        XCTAssertEqual(model.selectedID, "test.b")
        model.selectNext()
        XCTAssertEqual(model.selectedID, "test.c")
        // Wrap.
        model.selectNext()
        XCTAssertEqual(model.selectedID, "test.a")
    }

    @MainActor
    func testSelectPreviousWrapsAtStart() async {
        let model = CommandPaletteModel()
        model.loadCommands(fixtureCommands())
        // Start at first; previous should wrap to last.
        XCTAssertEqual(model.selectedID, "test.a")
        model.selectPrevious()
        XCTAssertEqual(model.selectedID, "test.c")
        model.selectPrevious()
        XCTAssertEqual(model.selectedID, "test.b")
        model.selectPrevious()
        XCTAssertEqual(model.selectedID, "test.a")
    }

    @MainActor
    func testSelectionResetsToFirstWhenQueryFiltersOutCurrent() async {
        let model = CommandPaletteModel()
        model.loadCommands(fixtureCommands())
        model.selectedID = "test.c"
        // Query that filters out "c" but keeps "a" and "b".
        model.query = "alpha"
        model.handleQueryChange()
        XCTAssertEqual(model.selectedID, "test.a", "selection snaps to first remaining match")
    }

    @MainActor
    func testEmptyFilterMakesSelectionNil() async {
        let model = CommandPaletteModel()
        model.loadCommands(fixtureCommands())
        model.query = "zzznonematch"
        model.handleQueryChange()
        XCTAssertNil(model.selectedID, "no matches → no selection")
    }

    // MARK: - Home / End / Page Up / Page Down (#1105)
    //
    // Windows' `SelectFirst` / `SelectLast` / `MovePage` twins
    // (`CommandPaletteViewModel.cs`, contract 28 P7; PD-1 retired by
    // #1105): over `displayOrder`, a page move clamps and never wraps,
    // and from no selection Page Down lands on the first row and Page
    // Up on the last.

    @MainActor
    func testHomeSelectsTheFirstVisibleRow() async {
        let model = CommandPaletteModel()
        // A Recent row heads the display order, so the first VISIBLE
        // row is not the registry's first command.
        model.loadCommands(fixturePageCommands(), recents: ["test.page.17"])
        model.selectedID = "test.page.09"
        model.selectFirst()
        XCTAssertEqual(model.selectedID, "test.page.17", "Home selects the first row the user sees")
        model.selectFirst()
        XCTAssertEqual(model.selectedID, "test.page.17", "Home on the first row stays there")
    }

    @MainActor
    func testEndSelectsTheLastVisibleRow() async {
        let model = CommandPaletteModel()
        // The Tasks row is the registry's first command, but its section
        // renders after Editor, so it is the last VISIBLE row.
        let tasks = Command(
            id: "test.tasks.review", label: "Review Tasks",
            accessibilityHint: nil, hotkeyHint: nil, section: .tasks)
        model.loadCommands([tasks] + fixturePageCommands())
        XCTAssertEqual(model.selectedID, "test.page.00")
        model.selectLast()
        XCTAssertEqual(model.selectedID, "test.tasks.review", "End selects the last row the user sees")
        model.selectLast()
        XCTAssertEqual(model.selectedID, "test.tasks.review", "End on the last row stays there")
    }

    @MainActor
    func testPageDownMovesTenAndClampsAtTheEnd() async {
        let model = CommandPaletteModel()
        model.loadCommands(fixturePageCommands())  // 25 rows; selection on the first
        XCTAssertEqual(model.pageSize, 10, "a page is ten rows, Windows' PageSize default")
        model.movePage(1)
        XCTAssertEqual(model.selectedID, "test.page.10")
        model.movePage(1)
        XCTAssertEqual(model.selectedID, "test.page.20")
        model.movePage(1)
        XCTAssertEqual(model.selectedID, "test.page.24", "a short last page stops on the last row")
        model.movePage(1)
        XCTAssertEqual(model.selectedID, "test.page.24", "Page Down on the last row stays there; it never wraps")
    }

    @MainActor
    func testPageUpMovesTenAndClampsAtTheStart() async {
        let model = CommandPaletteModel()
        model.loadCommands(fixturePageCommands())
        model.selectedID = "test.page.24"
        model.movePage(-1)
        XCTAssertEqual(model.selectedID, "test.page.14")
        model.movePage(-1)
        XCTAssertEqual(model.selectedID, "test.page.04")
        model.movePage(-1)
        XCTAssertEqual(model.selectedID, "test.page.00", "a short first page stops on the first row")
        model.movePage(-1)
        XCTAssertEqual(model.selectedID, "test.page.00", "Page Up on the first row stays there; it never wraps")
    }

    @MainActor
    func testPageDownWithNoSelectionStartsAtTheFirstRow() async {
        let model = CommandPaletteModel()
        model.loadCommands(fixturePageCommands())
        model.selectedID = nil
        model.movePage(1)
        XCTAssertEqual(
            model.selectedID, "test.page.00",
            "from no selection Page Down lands on the first row, not a page in")
        // A selection that is no longer a visible row counts as none.
        model.selectedID = "slate.removed.command"
        model.movePage(1)
        XCTAssertEqual(model.selectedID, "test.page.00")
    }

    @MainActor
    func testPageUpWithNoSelectionStartsAtTheLastRow() async {
        let model = CommandPaletteModel()
        model.loadCommands(fixturePageCommands())
        model.selectedID = nil
        model.movePage(-1)
        XCTAssertEqual(model.selectedID, "test.page.24", "from no selection Page Up lands on the last row")
        model.selectedID = "slate.removed.command"
        model.movePage(-1)
        XCTAssertEqual(model.selectedID, "test.page.24")
    }

    /// The flat cross-section cycle (contract 28 P7): Home, End and the
    /// page keys walk `displayOrder` (Recent first, then the native
    /// sections in declared order) straight across section boundaries,
    /// as Windows' `HomeEndAndPageKeysNavigateTheFlatCycleAndClampAtTheEnds`
    /// does with a page of two.
    @MainActor
    func testPageMovesCrossSectionsInDisplayOrder() async {
        let model = CommandPaletteModel()
        model.loadCommands(fixturePageAcrossSections(), recents: ["test.editor.bold"])
        model.pageSize = 2
        XCTAssertEqual(
            model.displayOrder.map(\.id),
            [
                "test.editor.bold", "test.file.alpha", "test.file.beta",
                "test.editor.save", "test.tasks.review",
            ],
            "Recent, then File, Editor and Tasks in declared order, the Recent row not repeated")

        model.selectLast()
        XCTAssertEqual(model.selectedID, "test.tasks.review")
        model.selectFirst()
        XCTAssertEqual(model.selectedID, "test.editor.bold")

        model.movePage(1)
        XCTAssertEqual(model.selectedID, "test.file.beta", "from Recent into File")
        model.movePage(1)
        XCTAssertEqual(model.selectedID, "test.tasks.review", "from File past Editor into Tasks")
        model.movePage(1)
        XCTAssertEqual(model.selectedID, "test.tasks.review", "stops on the last row; never wraps to Recent")
        model.movePage(-1)
        XCTAssertEqual(model.selectedID, "test.file.beta")
        model.movePage(-1)
        XCTAssertEqual(model.selectedID, "test.editor.bold")
        model.movePage(-1)
        XCTAssertEqual(model.selectedID, "test.editor.bold", "stops on the first row; never wraps to Tasks")
    }

    @MainActor
    func testPageKeysWithNoRowsOrNoDeltaDoNothing() async {
        let model = CommandPaletteModel()
        model.loadCommands(fixturePageCommands())
        model.query = "zzznonematch"
        model.handleQueryChange()
        XCTAssertNil(model.selectedID)
        model.selectFirst()
        model.selectLast()
        model.movePage(1)
        model.movePage(-1)
        XCTAssertNil(model.selectedID, "with no rows every page key is a no-op")

        model.query = ""
        model.handleQueryChange()
        model.selectedID = nil
        model.movePage(0)
        XCTAssertNil(model.selectedID, "a zero page move moves nothing, even from no selection")
    }

    /// Windows' `PageSize` setter floors at one row; so does mac's.
    @MainActor
    func testPageSizeNeverDropsBelowOneRow() async {
        let model = CommandPaletteModel()
        model.loadCommands(fixturePageCommands())
        model.pageSize = 0
        XCTAssertEqual(model.pageSize, 1, "a page is at least one row")
        model.movePage(1)
        XCTAssertEqual(model.selectedID, "test.page.01")
        model.pageSize = -3
        XCTAssertEqual(model.pageSize, 1)
        model.movePage(1)
        XCTAssertEqual(model.selectedID, "test.page.02", "a negative page size never reverses Page Down")
    }

    // MARK: - Section grouping + Recent (#316)

    /// Empty query, no recents → sections are the registry's
    /// commands grouped by their native CommandSection in
    /// declared order.
    @MainActor
    func testSectionsEmptyQueryNoRecentsGroupsByNativeSection() async {
        let model = CommandPaletteModel()
        model.loadCommands(fixtureCommandsAcrossSections())
        let sections = model.sections
        // Editor + File were the only two sections in the fixture.
        // File ships first in the declared order.
        XCTAssertEqual(sections.map(\.title), ["File", "Editor"])
        XCTAssertEqual(sections[0].commands.map(\.id), ["test.file.alpha", "test.file.beta"])
        XCTAssertEqual(sections[1].commands.map(\.id), ["test.editor.save"])
    }

    @MainActor
    func testSectionsPlaceSidebarImmediatelyAfterGraph() async {
        let model = CommandPaletteModel()
        model.loadCommands([
            Command(id: "test.sidebar", label: "Sidebar", accessibilityHint: nil, hotkeyHint: nil, section: .sidebar),
            Command(id: "test.canvas", label: "Canvas", accessibilityHint: nil, hotkeyHint: nil, section: .canvas),
            Command(id: "test.graph", label: "Graph", accessibilityHint: nil, hotkeyHint: nil, section: .graph),
            Command(id: "test.bases", label: "Bases", accessibilityHint: nil, hotkeyHint: nil, section: .bases),
        ])

        XCTAssertEqual(model.sections.map(\.title), ["Canvas", "Bases", "Graph", "Sidebar"])
    }

    /// Empty query, recents present → Recent section appears at
    /// top; commands shown in Recent are EXCLUDED from their
    /// native section so the flat displayOrder doesn't duplicate.
    @MainActor
    func testSectionsEmptyQueryWithRecentsAddsRecentSection() async {
        let model = CommandPaletteModel()
        model.loadCommands(
            fixtureCommandsAcrossSections(),
            recents: ["test.editor.save", "test.file.alpha"]
        )
        let sections = model.sections
        XCTAssertEqual(sections.map(\.title), ["Recent", "File"])
        XCTAssertEqual(
            sections[0].commands.map(\.id),
            ["test.editor.save", "test.file.alpha"],
            "Recent preserves invocation order"
        )
        // The Editor section dropped out entirely because its only
        // command ('save') is now in Recent.
        XCTAssertEqual(sections[1].commands.map(\.id), ["test.file.beta"])
    }

    /// Non-empty query → no Recent section (filter results are
    /// what the user asked for, not history). Fuzzy-matched
    /// commands grouped by native section.
    @MainActor
    func testSectionsNonEmptyQueryHasNoRecentSection() async {
        let model = CommandPaletteModel()
        model.loadCommands(
            fixtureCommandsAcrossSections(),
            recents: ["test.editor.save"]
        )
        model.query = "save"
        let sections = model.sections
        XCTAssertEqual(sections.map(\.title), ["Editor"])
        XCTAssertEqual(sections[0].commands.map(\.id), ["test.editor.save"])
    }

    /// Recent ids that no longer exist in the registry (e.g.
    /// removed in an app update) are silently skipped — not a
    /// crash and not a "phantom" row.
    @MainActor
    func testSectionsSkipsRecentsMissingFromRegistry() async {
        let model = CommandPaletteModel()
        model.loadCommands(
            fixtureCommandsAcrossSections(),
            recents: ["slate.removed.command", "test.editor.save"]
        )
        let sections = model.sections
        XCTAssertEqual(sections[0].title, "Recent")
        XCTAssertEqual(
            sections[0].commands.map(\.id),
            ["test.editor.save"],
            "missing recents are skipped, present ones survive"
        )
    }

    /// `displayOrder` is the flat list arrow nav cycles through.
    /// It must include Recent commands first when query is empty.
    @MainActor
    func testDisplayOrderReflectsRecentThenNative() async {
        let model = CommandPaletteModel()
        model.loadCommands(
            fixtureCommandsAcrossSections(),
            recents: ["test.editor.save"]
        )
        XCTAssertEqual(
            model.displayOrder.map(\.id),
            ["test.editor.save", "test.file.alpha", "test.file.beta"],
            "Recent first, then native sections in declared order, excluding recents"
        )
    }

    /// Arrow navigation cycles through displayOrder (including
    /// Recent rows), not the legacy flat-by-id list.
    @MainActor
    func testArrowNavCyclesAcrossRecentAndNative() async {
        let model = CommandPaletteModel()
        model.loadCommands(
            fixtureCommandsAcrossSections(),
            recents: ["test.editor.save"]
        )
        XCTAssertEqual(model.selectedID, "test.editor.save", "starts on Recent row")
        model.selectNext()
        XCTAssertEqual(model.selectedID, "test.file.alpha")
        model.selectNext()
        XCTAssertEqual(model.selectedID, "test.file.beta")
        // Wrap.
        model.selectNext()
        XCTAssertEqual(model.selectedID, "test.editor.save")
    }

    // MARK: - Filter-change announcement (#316)

    @MainActor
    func testFilterAnnouncementEmptyQueryIsNil() async {
        let model = CommandPaletteModel()
        model.loadCommands(fixtureCommandsAcrossSections())
        model.query = ""
        model.handleQueryChange()
        XCTAssertNil(
            model.filterAnnouncementText,
            "empty query doesn't announce — user just opened or cleared"
        )
    }

    @MainActor
    func testFilterAnnouncementSingleMatch() async {
        let model = CommandPaletteModel()
        model.loadCommands(fixtureCommandsAcrossSections())
        model.query = "save"
        model.handleQueryChange()
        XCTAssertEqual(
            model.filterAnnouncementText,
            "1 command matching \"save\""
        )
    }

    @MainActor
    func testFilterAnnouncementMultipleMatches() async {
        let model = CommandPaletteModel()
        model.loadCommands(fixtureCommandsAcrossSections())
        // "a" matches alpha + save + beta — pick a query that hits more than one
        model.query = "e"
        model.handleQueryChange()
        // Expect a "<N> commands matching" with plural.
        XCTAssertNotNil(model.filterAnnouncementText)
        XCTAssertTrue(
            model.filterAnnouncementText!.contains("commands matching"),
            "got: \(model.filterAnnouncementText ?? "nil")"
        )
    }

    @MainActor
    func testFilterAnnouncementNoMatches() async {
        let model = CommandPaletteModel()
        model.loadCommands(fixtureCommandsAcrossSections())
        model.query = "zzznothingmatches"
        model.handleQueryChange()
        XCTAssertEqual(
            model.filterAnnouncementText,
            "No commands match \"zzznothingmatches\""
        )
    }

    @MainActor
    func testClearFilterAnnouncementResetsToNil() async {
        let model = CommandPaletteModel()
        model.loadCommands(fixtureCommandsAcrossSections())
        model.query = "save"
        model.handleQueryChange()
        XCTAssertNotNil(model.filterAnnouncementText)
        model.clearFilterAnnouncement()
        XCTAssertNil(model.filterAnnouncementText)
    }

    // MARK: - Section title mapping

    /// The canonical header strings now come from core
    /// (`slate_core::palette::section_title`, W0.5-1 #717) — assert
    /// them through the real sections pipe so a core-side copy change
    /// can't slip past the mac suite.
    @MainActor
    func testSectionTitleMapping() async {
        let allSections: [(CommandSection, String)] = [
            (.file, "File"), (.navigation, "Navigation"), (.view, "View"),
            (.vault, "Vault"), (.editor, "Editor"), (.tasks, "Tasks"),
            (.settings, "Settings"), (.plugins, "Plugins"), (.canvas, "Canvas"),
            (.bases, "Bases"), (.graph, "Graph"), (.sidebar, "Sidebar"),
        ]
        let model = CommandPaletteModel()
        model.loadCommands(allSections.enumerated().map { index, entry in
            Command(
                id: "test.section.\(index)",
                label: "Probe \(entry.1)",
                accessibilityHint: nil,
                hotkeyHint: nil,
                section: entry.0
            )
        })
        let titlesByKind = Dictionary(
            uniqueKeysWithValues: model.sections.compactMap { section in
                section.kind.map { ($0, section.title) }
            }
        )
        for (kind, expected) in allSections {
            XCTAssertEqual(titlesByKind[kind], expected)
        }
    }

    // MARK: - Invoke (#315)

    @MainActor
    func testInvokeSuccessReturnsSuccessOutcome() async {
        let model = CommandPaletteModel()
        let registry = CommandRegistry()
        let action = StubAction()
        _ = registry.register(
            command: Command(
                id: "test.success",
                label: "Success",
                accessibilityHint: nil,
                hotkeyHint: nil,
                section: .editor
            ),
            action: action
        )
        model.loadCommands(registry.list())

        let outcome = model.invokeSelected(via: registry)
        XCTAssertEqual(outcome, .success)
        XCTAssertEqual(action.invocationCount, 1)
        XCTAssertNil(model.pendingAnnouncementText, "success path posts no announcement")
    }

    @MainActor
    func testInvokeActionFailedStaysOpenAndAnnounces() async {
        let model = CommandPaletteModel()
        let registry = CommandRegistry()
        let action = StubAction(failWith: .ActionFailed(message: "disk full"))
        _ = registry.register(
            command: Command(
                id: "test.failing",
                label: "Failing",
                accessibilityHint: nil,
                hotkeyHint: nil,
                section: .editor
            ),
            action: action
        )
        model.loadCommands(registry.list())

        let outcome = model.invokeSelected(via: registry)

        // Outcome carries the unwrapped message (not the Swift
        // debug repr) — verifies the red-team's
        // localizedDescription regression doesn't return.
        if case .actionFailed(let label, let message) = outcome {
            XCTAssertEqual(label, "Failing")
            XCTAssertEqual(message, "disk full")
        } else {
            XCTFail("expected .actionFailed, got \(outcome)")
        }

        XCTAssertEqual(
            model.pendingAnnouncementText,
            "Failing failed: disk full",
            "announcement must include the unwrapped message"
        )
    }

    @MainActor
    func testBusyStructuralFailureAnnouncesTheExactReasonAndIsNotSuccess() async {
        let model = CommandPaletteModel()
        let registry = CommandRegistry()
        let reason = "Wait for the current file operation to finish."
        let action = StubAction(failWith: .ActionFailed(message: reason))
        let command = Command(
            id: SlateCommandID.newNote,
            label: "New Note",
            accessibilityHint: "Create an untitled note.",
            hotkeyHint: "⌘N",
            section: .file)
        _ = registry.register(command: command, action: action)
        model.loadCommands(registry.list())

        let outcome = model.invokeSelected(via: registry)

        XCTAssertEqual(
            outcome,
            .actionFailed(label: "New Note", message: reason),
            "busy structural work must not masquerade as a successful invocation")
        XCTAssertEqual(
            model.pendingAnnouncementText, reason,
            "VoiceOver must hear the shared reason verbatim, without a second failure prefix")
        XCTAssertEqual(action.invocationCount, 1)
    }

    @MainActor
    func testInvokeNoSelectionIsNoOp() async {
        let model = CommandPaletteModel()
        let registry = CommandRegistry()
        // Don't load any commands → no selection.
        let outcome = model.invokeSelected(via: registry)
        XCTAssertEqual(outcome, .noSelection)
        XCTAssertNil(model.pendingAnnouncementText)
    }

    // MARK: - Fixtures

    @MainActor
    private func fixtureCommands() -> [Command] {
        [
            Command(id: "test.a", label: "Alpha", accessibilityHint: nil, hotkeyHint: nil, section: .editor),
            Command(id: "test.b", label: "Beta",  accessibilityHint: nil, hotkeyHint: nil, section: .editor),
            Command(id: "test.c", label: "Gamma", accessibilityHint: nil, hotkeyHint: nil, section: .editor),
        ]
    }

    /// Multi-section fixture used by the section-grouping tests
    /// (#316). Two sections so we can verify cross-section
    /// arrow nav and the Recent-excludes-from-native rule.
    @MainActor
    private func fixtureCommandsAcrossSections() -> [Command] {
        [
            Command(id: "test.file.alpha", label: "Alpha", accessibilityHint: nil, hotkeyHint: nil, section: .file),
            Command(id: "test.file.beta",  label: "Beta",  accessibilityHint: nil, hotkeyHint: nil, section: .file),
            Command(id: "test.editor.save", label: "Save", accessibilityHint: nil, hotkeyHint: nil, section: .editor),
        ]
    }

    /// Twenty-five Editor rows (#1105): two full pages of ten and a
    /// short third. One section, so display order is input order.
    @MainActor
    private func fixturePageCommands() -> [Command] {
        (0..<25).map { index in
            let suffix = String(format: "%02d", index)
            return Command(
                id: "test.page.\(suffix)", label: "Page Row \(suffix)",
                accessibilityHint: nil, hotkeyHint: nil, section: .editor)
        }
    }

    /// Five rows over three sections, given in an order unlike the
    /// display order (#1105), for the flat cross-section page cycle.
    @MainActor
    private func fixturePageAcrossSections() -> [Command] {
        [
            Command(id: "test.tasks.review", label: "Review Tasks", accessibilityHint: nil, hotkeyHint: nil, section: .tasks),
            Command(id: "test.editor.save", label: "Save", accessibilityHint: nil, hotkeyHint: nil, section: .editor),
            Command(id: "test.file.alpha", label: "Alpha", accessibilityHint: nil, hotkeyHint: nil, section: .file),
            Command(id: "test.editor.bold", label: "Bold", accessibilityHint: nil, hotkeyHint: nil, section: .editor),
            Command(id: "test.file.beta", label: "Beta", accessibilityHint: nil, hotkeyHint: nil, section: .file),
        ]
    }

    /// The modifier flags macOS itself puts on these keys (#1105),
    /// measured through the system's own key events
    /// (`CGEvent(keyboardEventSource:virtualKey:keyDown:)` to
    /// `NSEvent(cgEvent:)`, every event-source state, macOS 27): a bare
    /// ↑ / ↓ carries `.numericPad` and `.function`; Home, End, Page Up
    /// and Page Down carry `.function`.
    private static let systemArrowFlags: NSEvent.ModifierFlags = [.numericPad, .function]
    private static let systemPageKeyFlags: NSEvent.ModifierFlags = [.function]

    /// Swift-side `CommandAction` for tests. `@unchecked Sendable`
    /// matches the project contract for FFI callbacks (see
    /// `CommandAction` doc on the FFI side).
    final class StubAction: CommandAction, @unchecked Sendable {
        private let lock = NSLock()
        private var _invocationCount: Int = 0
        private let failWith: CommandError?

        init(failWith: CommandError? = nil) {
            self.failWith = failWith
        }

        var invocationCount: Int {
            lock.lock(); defer { lock.unlock() }
            return _invocationCount
        }

        func invoke() throws {
            lock.lock()
            _invocationCount += 1
            lock.unlock()
            if let err = failWith { throw err }
        }
    }

    // APCA helper lives in `APCAContrast.swift`.
}
