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
            model.filterAnnouncement,
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
            model.filterAnnouncement,
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
        XCTAssertNotNil(model.filterAnnouncement)
        XCTAssertTrue(
            model.filterAnnouncement!.contains("commands matching"),
            "got: \(model.filterAnnouncement ?? "nil")"
        )
    }

    @MainActor
    func testFilterAnnouncementNoMatches() async {
        let model = CommandPaletteModel()
        model.loadCommands(fixtureCommandsAcrossSections())
        model.query = "zzznothingmatches"
        model.handleQueryChange()
        XCTAssertEqual(
            model.filterAnnouncement,
            "No commands match \"zzznothingmatches\""
        )
    }

    @MainActor
    func testClearFilterAnnouncementResetsToNil() async {
        let model = CommandPaletteModel()
        model.loadCommands(fixtureCommandsAcrossSections())
        model.query = "save"
        model.handleQueryChange()
        XCTAssertNotNil(model.filterAnnouncement)
        model.clearFilterAnnouncement()
        XCTAssertNil(model.filterAnnouncement)
    }

    // MARK: - Section title mapping

    func testSectionTitleMapping() {
        XCTAssertEqual(CommandPaletteModel.title(for: .file), "File")
        XCTAssertEqual(CommandPaletteModel.title(for: .navigation), "Navigation")
        XCTAssertEqual(CommandPaletteModel.title(for: .view), "View")
        XCTAssertEqual(CommandPaletteModel.title(for: .vault), "Vault")
        XCTAssertEqual(CommandPaletteModel.title(for: .editor), "Editor")
        XCTAssertEqual(CommandPaletteModel.title(for: .tasks), "Tasks")
        XCTAssertEqual(CommandPaletteModel.title(for: .settings), "Settings")
        XCTAssertEqual(CommandPaletteModel.title(for: .plugins), "Plugins")
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
