// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// Tests for the menu→registry bridge (Milestone Q #314).
///
/// The **drift check** (`testEveryDeclaredCommandIDIsRegistered`)
/// is the load-bearing test for the rest of the milestone: it
/// asserts that the registry's contents match `SlateCommandID.all`
/// exactly, so future menu additions can't drift away from the
/// palette without failing CI.
///
/// The **invoke-route** tests assert that triggering a command via
/// `invokeById` hits the same `AppState` mutation the corresponding
/// menu item would (or at least: the observable side-effect a
/// caller could check). For actions that touch real disk / system
/// dialogs (`pickAndOpenVault`, `saveCurrentNote`) we only assert
/// "doesn't crash" — full coverage of those rides the integration
/// suite in #317.
final class SlateCommandsTests: XCTestCase {

    // MARK: - Drift check

    /// Source-of-truth assertion: every id in `SlateCommandID.all`
    /// resolves to a registered `Command`, AND the registry has no
    /// commands beyond that list (no surprise registrations). A
    /// new menu item in MainSplitView / SlateMacApp /
    /// PropertiesPanel without a matching entry in `SlateCommandID`
    /// + `registerCoreCommands` fails this test.
    @MainActor
    func testEveryDeclaredCommandIDIsRegistered() async {
        let appState = AppState()
        let registered = appState.commandRegistry.list().map(\.id).sorted()
        let declared = SlateCommandID.all.sorted()
        XCTAssertEqual(
            registered,
            declared,
            "Registry contents drifted from SlateCommandID.all. " +
            "Add or remove ids on both sides so the palette mirrors the menu."
        )
    }

    /// Every registered command must have a non-empty label
    /// (palette renders the label as the row text — empty would be
    /// an invisible row a user couldn't recognise) and a section
    /// the palette's grouping (#316) will understand.
    @MainActor
    func testEveryRegisteredCommandHasNonEmptyLabel() async {
        let appState = AppState()
        for command in appState.commandRegistry.list() {
            XCTAssertFalse(
                command.label.isEmpty,
                "Command \(command.id) has an empty label"
            )
        }
    }

    /// Re-registration check: calling `registerCoreCommands` twice
    /// on the same registry would surface as replacements (which
    /// the bridge function `assert`s against). Confirms the
    /// function is safe to call exactly once per registry — and
    /// only once.
    @MainActor
    func testRegistryHasNoDuplicateIDs() async {
        let appState = AppState()
        let ids = appState.commandRegistry.list().map(\.id)
        XCTAssertEqual(
            Set(ids).count,
            ids.count,
            "Registry has duplicate ids: \(ids)"
        )
    }

    /// **Real drift check** — scrapes the actual menu source files
    /// for `.keyboardShortcut(...)` declarations and asserts that
    /// every chord found there is also represented in the registry
    /// (via a registered command's `hotkeyHint`).
    ///
    /// The previous `testEveryDeclaredCommandIDIsRegistered`
    /// compared `SlateCommandID.all` to the registry — both
    /// declared in `SlateCommands.swift`, so a contributor adding
    /// a new `keyboardShortcut` in `MainSplitView` without
    /// touching this file's catalogue had the test pass green.
    /// This test closes that loophole.
    ///
    /// Source-of-truth files: any `.swift` under
    /// `apps/slate-mac/Sources/SlateMac/` that declares an app-
    /// level shortcut (`keyboardShortcut(KeyEquivalent("...")...)`).
    /// Sheet-cancel chords (`.cancelAction`, `.defaultAction`,
    /// `.escape`) are excluded — they're per-sheet dismissal, not
    /// palette-worthy actions.
    @MainActor
    func testEveryMenuShortcutHasAMatchingRegistryEntry() async throws {
        let menuChords = try Self.scrapedMenuChords()
        XCTAssertFalse(
            menuChords.isEmpty,
            "Expected at least one app-level keyboardShortcut in the menu sources; the scrape regex may be broken"
        )

        let appState = AppState()
        let registryChords: Set<String> = Set(
            appState.commandRegistry.list().compactMap(\.hotkeyHint)
        )

        let missing = menuChords
            .subtracting(registryChords)
            .subtracting(Self.deliberatelyUnregisteredChords)

        XCTAssertTrue(
            missing.isEmpty,
            "Menu items have shortcuts that no registered command surfaces in the palette: \(missing.sorted()). " +
            "Add matching commands to registerCoreCommands in SlateCommands.swift, " +
            "or add the chord to deliberatelyUnregisteredChords here with a comment."
        )
    }

    /// Chords intentionally absent from the registry. New entries
    /// here need a comment explaining why — the drift check defers
    /// to this list rather than silently passing.
    static let deliberatelyUnregisteredChords: Set<String> = [
        // Self-reference: showing the palette from inside the
        // palette would be weird UX, and toggling-to-close
        // duplicates the Esc dismissal already wired in #313.
        "⇧⌘P",
    ]

    /// Walk every `.swift` source file under `apps/slate-mac/Sources/SlateMac/`,
    /// extract every `keyboardShortcut("X", modifiers: ...)` chord,
    /// and return the set of human-readable chord strings (⌘S,
    /// ⇧⌘N, etc.). Excludes `.cancelAction` / `.defaultAction` /
    /// `.escape` overloads.
    static func scrapedMenuChords() throws -> Set<String> {
        let sourcesDir = projectRoot
            .appendingPathComponent("apps")
            .appendingPathComponent("slate-mac")
            .appendingPathComponent("Sources")
            .appendingPathComponent("SlateMac")
        let files = try FileManager.default.contentsOfDirectory(
            at: sourcesDir,
            includingPropertiesForKeys: nil
        ).filter { $0.pathExtension == "swift" }

        // Matches:  keyboardShortcut("x", modifiers: .command)
        //           keyboardShortcut("x", modifiers: [.command, .shift])
        //           keyboardShortcut(KeyEquivalent("r"), modifiers: [.command, .shift])
        let chordRegex = try NSRegularExpression(
            pattern: #"keyboardShortcut\(\s*(?:KeyEquivalent\(\s*)?"([a-zA-Z0-9])"\s*\)?\s*,\s*modifiers:\s*(\[[^\]]*\]|\.command|\.shift|\.option|\.control)"#
        )

        var chords: Set<String> = []
        for file in files {
            let text = try String(contentsOf: file, encoding: .utf8)
            let range = NSRange(text.startIndex..., in: text)
            chordRegex.enumerateMatches(in: text, range: range) { match, _, _ in
                guard let match,
                      let keyRange = Range(match.range(at: 1), in: text),
                      let modsRange = Range(match.range(at: 2), in: text)
                else { return }
                let key = String(text[keyRange]).uppercased()
                let mods = String(text[modsRange])
                chords.insert(Self.formatChord(key: key, modifiers: mods))
            }
        }
        return chords
    }

    private static func formatChord(key: String, modifiers: String) -> String {
        var glyphs = ""
        if modifiers.contains(".control") { glyphs += "⌃" }
        if modifiers.contains(".option")  { glyphs += "⌥" }
        if modifiers.contains(".shift")   { glyphs += "⇧" }
        if modifiers.contains(".command") { glyphs += "⌘" }
        return glyphs + key
    }

    /// Project root derived from this test file's path.
    /// `#filePath` for this file is something like
    /// `/Users/<u>/Dev/slate/apps/slate-mac/Tests/SlateMacTests/SlateCommandsTests.swift`;
    /// walking up four levels lands at the repo root, which works
    /// both locally and on CI runners.
    static var projectRoot: URL {
        URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent() // SlateMacTests
            .deletingLastPathComponent() // Tests
            .deletingLastPathComponent() // slate-mac
            .deletingLastPathComponent() // apps
            .deletingLastPathComponent() // <repo root>
    }

    // MARK: - Invoke-route checks

    /// `slate.tasks.review` with no vault open is a no-op —
    /// `openTasksReview()` has a `guard currentSession != nil` and
    /// the menu item carries `.disabled(appState.currentSession ==
    /// nil)`. The bridge must preserve that semantic; invoking the
    /// command without a session must not throw AND must leave
    /// `isTasksReviewOpen` false. Full positive coverage (with a
    /// vault open) rides the integration suite (#317).
    @MainActor
    func testInvokingTasksReviewCommandWithNoSessionIsNoOp() async throws {
        let appState = AppState()
        XCTAssertFalse(appState.isTasksReviewOpen)
        try appState.commandRegistry.invokeById(id: SlateCommandID.tasksReview)
        XCTAssertFalse(
            appState.isTasksReviewOpen,
            "tasks review must respect the no-session guard from openTasksReview()"
        )
    }

    /// Invoking `slate.editor.citationSummary` flips
    /// `isCitationSummaryOpen`.
    @MainActor
    func testInvokingCitationSummaryCommandFlipsIsCitationSummaryOpen() async throws {
        let appState = AppState()
        XCTAssertFalse(appState.isCitationSummaryOpen)
        try appState.commandRegistry.invokeById(id: SlateCommandID.citationSummary)
        XCTAssertTrue(appState.isCitationSummaryOpen)
    }

    /// Invoking `slate.editor.addProperty` flips
    /// `isAddPropertySheetOpen`.
    @MainActor
    func testInvokingAddPropertyCommandFlipsIsAddPropertySheetOpen() async throws {
        let appState = AppState()
        XCTAssertFalse(appState.isAddPropertySheetOpen)
        try appState.commandRegistry.invokeById(id: SlateCommandID.addProperty)
        XCTAssertTrue(appState.isAddPropertySheetOpen)
    }

    /// Invoking `slate.editor.bulkRenameProperties` flips
    /// `isBulkRenameSheetOpen`.
    @MainActor
    func testInvokingBulkRenameCommandFlipsIsBulkRenameSheetOpen() async throws {
        let appState = AppState()
        XCTAssertFalse(appState.isBulkRenameSheetOpen)
        try appState.commandRegistry.invokeById(id: SlateCommandID.bulkRenameProperties)
        XCTAssertTrue(appState.isBulkRenameSheetOpen)
    }

    /// `slate.editor.save` with no loaded file is a no-op (the
    /// menu item's `.disabled(...)` precondition reflects this).
    /// Just assert it doesn't crash or throw.
    @MainActor
    func testInvokingSaveCommandWithNoFileIsNoOp() async throws {
        let appState = AppState()
        try appState.commandRegistry.invokeById(id: SlateCommandID.save)
    }

    /// `slate.settings.open` invokes
    /// `NSApplication.shared.sendAction("showSettingsWindow:")`,
    /// which targets the SwiftUI-installed Settings scene's
    /// responder. In a unit-test runner there's no installed
    /// menu / responder chain, so the sendAction returns false
    /// silently — same as the menu item itself would in that
    /// environment. The contract we CAN test is that invoking
    /// through the registry doesn't throw and matches the
    /// shape the menu surface uses.
    @MainActor
    func testInvokingOpenSettingsCommandDoesNotThrow() async throws {
        let appState = AppState()
        try appState.commandRegistry.invokeById(id: SlateCommandID.openSettings)
    }

    /// The `slate.settings.open` action relies on SwiftUI's
    /// auto-installed `Settings { }` scene to provide the
    /// `showSettingsWindow:` responder. If the scene is ever
    /// removed from `SlateMacApp.swift`, the command silently
    /// becomes a no-op (sendAction returns `false`, no responder).
    /// This structural test fails CI if someone removes the
    /// scene without also dropping the palette command — closes
    /// the silent-regression gap the invoke-doesn't-throw test
    /// can't cover.
    @MainActor
    func testSettingsSceneStillExistsInSlateMacApp() async throws {
        let appFile = Self.projectRoot
            .appendingPathComponent("apps")
            .appendingPathComponent("slate-mac")
            .appendingPathComponent("Sources")
            .appendingPathComponent("SlateMac")
            .appendingPathComponent("SlateMacApp.swift")
        let text = try String(contentsOf: appFile, encoding: .utf8)
        XCTAssertTrue(
            text.contains("Settings {"),
            "SlateMacApp.swift must declare a `Settings { }` scene "
                + "so the showSettingsWindow: responder exists for "
                + "the slate.settings.open palette command. "
                + "If you intentionally removed the scene, also "
                + "drop the SlateCommandID.openSettings registration."
        )
    }

    /// `slate.navigation.jumpToBibliography` with no expanded
    /// citation is a no-op — the bridge mirrors the menu's
    /// disabled precondition.
    @MainActor
    func testInvokingJumpToBibliographyWithNoExpandedCitationIsNoOp() async throws {
        let appState = AppState()
        try appState.commandRegistry.invokeById(id: SlateCommandID.jumpToBibliography)
    }

    // MARK: - Unknown id

    /// Invoking a command id that isn't registered surfaces
    /// `CommandError.UnknownId` from Rust through the FFI.
    @MainActor
    func testInvokingUnknownIdReturnsUnknownIdError() async {
        let appState = AppState()
        XCTAssertThrowsError(
            try appState.commandRegistry.invokeById(id: "slate.bogus.does-not-exist")
        ) { error in
            guard case CommandError.UnknownId(let id) = error else {
                XCTFail("expected UnknownId, got \(error)")
                return
            }
            XCTAssertEqual(id, "slate.bogus.does-not-exist")
        }
    }
}
