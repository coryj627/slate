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
///    `selectNext`/`selectPrevious`.
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

    // MARK: - Arrow-key modifier passthrough (#315 review follow-up)

    /// Locks in the contract that bare ↑ / ↓ moves palette
    /// selection while every modified arrow chord passes through
    /// unconsumed. This is load-bearing for screen-reader users
    /// (Ctrl+Option+↓ is VoiceOver Quick Nav) and for text-field
    /// editing inside the search field (Shift+↓ extends, Cmd+↓
    /// jumps caret, Fn+↓ is Page Down). A regression here would
    /// silently break a11y; the test exists so the modifier mask
    /// can't drift without CI flagging it.

    func testBareArrowDownIsConsumed() {
        XCTAssertFalse(
            CommandPaletteView.shouldPassThroughArrow(keyCode: 125, modifierFlags: []),
            "bare ↓ must be consumed by the palette monitor"
        )
    }

    func testBareArrowUpIsConsumed() {
        XCTAssertFalse(
            CommandPaletteView.shouldPassThroughArrow(keyCode: 126, modifierFlags: []),
            "bare ↑ must be consumed by the palette monitor"
        )
    }

    func testFnArrowPassesThroughForPageNav() {
        // macOS treats Fn+↓ / Fn+↑ as Page Down / Page Up.
        XCTAssertTrue(
            CommandPaletteView.shouldPassThroughArrow(keyCode: 125, modifierFlags: .function),
            "Fn+↓ (macOS Page Down) must pass through unconsumed"
        )
        XCTAssertTrue(
            CommandPaletteView.shouldPassThroughArrow(keyCode: 126, modifierFlags: .function),
            "Fn+↑ (macOS Page Up) must pass through unconsumed"
        )
    }

    func testShiftArrowPassesThroughForSelectionExtend() {
        XCTAssertTrue(
            CommandPaletteView.shouldPassThroughArrow(keyCode: 125, modifierFlags: .shift),
            "Shift+↓ (extend text-field selection) must pass through unconsumed"
        )
    }

    func testCommandArrowPassesThroughForCaretJump() {
        XCTAssertTrue(
            CommandPaletteView.shouldPassThroughArrow(keyCode: 125, modifierFlags: .command),
            "Cmd+↓ (caret to end-of-text) must pass through unconsumed"
        )
    }

    func testCtrlOptArrowPassesThroughForVoiceOverQuickNav() {
        // Ctrl+Option+↓ is VoiceOver Quick Nav — the screen-reader
        // chord we absolutely cannot intercept. Failing this test
        // means we just broke VO navigation for blind users.
        XCTAssertTrue(
            CommandPaletteView.shouldPassThroughArrow(keyCode: 125, modifierFlags: [.control, .option]),
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

    // MARK: - Fuzzy matcher (#315)

    func testFuzzyScoreReturnsNilForNonSubsequence() {
        XCTAssertNil(
            CommandPaletteModel.fuzzyScore(query: "xyz", target: "Save"),
            "query with chars not in target must return nil"
        )
        XCTAssertNil(
            CommandPaletteModel.fuzzyScore(query: "vault", target: "Save"),
            "subsequence requires order — 'vault' chars aren't all in 'Save'"
        )
    }

    func testFuzzyScoreIsCaseInsensitive() {
        let a = CommandPaletteModel.fuzzyScore(query: "save", target: "Save")
        let b = CommandPaletteModel.fuzzyScore(query: "SAVE", target: "save")
        XCTAssertEqual(a, b)
        XCTAssertNotNil(a)
    }

    func testFuzzyScoreReturnsHigherForPrefixThanSubsequence() {
        let prefix = CommandPaletteModel.fuzzyScore(query: "save", target: "Save")!
        let scattered = CommandPaletteModel.fuzzyScore(query: "save", target: "Citations Are Visible Embeds")!
        XCTAssertGreaterThan(prefix, scattered)
    }

    func testFuzzyScoreRewardsConsecutiveMatches() {
        let consecutive = CommandPaletteModel.fuzzyScore(query: "sa", target: "Save")!
        let split = CommandPaletteModel.fuzzyScore(query: "sa", target: "Slate Add")!
        XCTAssertGreaterThan(consecutive, split)
    }

    func testFuzzyScoreRewardsWordBoundaryHits() {
        let boundary = CommandPaletteModel.fuzzyScore(query: "ts", target: "Tasks Review")!
        let mid = CommandPaletteModel.fuzzyScore(query: "ts", target: "Citations Review")!
        XCTAssertGreaterThan(boundary, mid)
    }

    func testFuzzyScoreEmptyQueryReturnsZero() {
        XCTAssertEqual(CommandPaletteModel.fuzzyScore(query: "", target: "Anything"), 0)
    }

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
        XCTAssertNil(model.pendingAnnouncement, "success path posts no announcement")
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
            model.pendingAnnouncement,
            "Failing failed: disk full",
            "announcement must include the unwrapped message"
        )
    }

    @MainActor
    func testInvokeNoSelectionIsNoOp() async {
        let model = CommandPaletteModel()
        let registry = CommandRegistry()
        // Don't load any commands → no selection.
        let outcome = model.invokeSelected(via: registry)
        XCTAssertEqual(outcome, .noSelection)
        XCTAssertNil(model.pendingAnnouncement)
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
