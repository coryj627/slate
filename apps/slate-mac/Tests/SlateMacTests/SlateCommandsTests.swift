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

        // Catch the inverse: an allow-list entry whose source-side
        // chord has been removed. Without this, deletes of the
        // TemplatePromptSheet binding (or any other allow-listed
        // chord) leave a dead `deliberatelyUnregisteredChords`
        // entry that silently shields the next added chord.
        let staleAllowListEntries = Self.deliberatelyUnregisteredChords
            .subtracting(menuChords)
        XCTAssertTrue(
            staleAllowListEntries.isEmpty,
            "deliberatelyUnregisteredChords has entries with no matching source-side chord: \(staleAllowListEntries.sorted()). " +
            "Either the chord was removed from the menu source (drop the allow-list entry) " +
            "or the regex no longer scrapes it (investigate)."
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
        // TemplatePromptSheet.swift's "commit" button binding.
        // In-sheet submission action, structurally identical to a
        // global chord but scoped to the sheet's responder chain
        // by SwiftUI. Not a menu / palette-worthy command. Picked
        // up by the broader scraper (#322) — earlier scrapers
        // missed `.return` entirely because it isn't a quoted
        // single char.
        "⌘↩",
        // PropertyEditorRow.swift's per-row Delete button. Row-
        // scoped action — deletes one property of the focused
        // row, not a global "delete property" verb. Picked up by
        // the #322 scraper's new `.delete` coverage.
        "⌘⌫",
    ]

    /// Walk every `.swift` source file under
    /// `apps/slate-mac/Sources/SlateMac/` **recursively** (so a
    /// future contributor adding `Sources/SlateMac/Panels/Foo.swift`
    /// doesn't silently escape the drift check), extract every
    /// `keyboardShortcut(...)` chord, and return the set of
    /// human-readable chord strings (⌘S, ⇧⌘N, ⌘,, ⌘↩, etc.).
    ///
    /// Sheet-semantics overloads (`.cancelAction`, `.defaultAction`)
    /// are excluded — they don't take a `modifiers:` argument so
    /// the regex naturally skips them. Bare special keys like
    /// `.escape` / `.return` / arrows WITHOUT modifiers are also
    /// excluded (same reason). A special key paired with modifiers
    /// (e.g. `keyboardShortcut(.return, modifiers: [.command])`)
    /// IS a real chord and gets scraped.
    static func scrapedMenuChords() throws -> Set<String> {
        let sourcesDir = projectRoot
            .appendingPathComponent("apps")
            .appendingPathComponent("slate-mac")
            .appendingPathComponent("Sources")
            .appendingPathComponent("SlateMac")
        return try scrapeChordsFromDirectory(sourcesDir)
    }

    /// Recursive walk over `.swift` files under `dir`, accumulating
    /// every chord via `extractChords(from:)`. Extracted so unit
    /// tests can exercise the walker against a temp-dir fixture
    /// with nested subdirectories.
    static func scrapeChordsFromDirectory(_ dir: URL) throws -> Set<String> {
        var chords: Set<String> = []
        guard let enumerator = FileManager.default.enumerator(
            at: dir,
            includingPropertiesForKeys: [.isRegularFileKey],
            options: [.skipsHiddenFiles, .skipsPackageDescendants]
        ) else {
            return chords
        }
        for case let url as URL in enumerator {
            guard url.pathExtension == "swift" else { continue }
            // Skip symlinks / non-regular files — defensive against
            // someone dropping a symlink to a .swift file under
            // Sources/SlateMac/. No current source does this, but
            // following a symlink to a different file (or worse, a
            // cycle) would be surprising scraper behaviour.
            let values = try? url.resourceValues(forKeys: [.isRegularFileKey])
            guard values?.isRegularFile == true else { continue }
            let text = try String(contentsOf: url, encoding: .utf8)
            chords.formUnion(extractChords(from: text))
        }
        return chords
    }

    /// Pure: scan `text` for every `keyboardShortcut(...)` chord
    /// declaration and return the set of human-readable chord
    /// strings. Two regex passes:
    ///
    /// 1. **Quoted single char** — `keyboardShortcut("X", modifiers: ...)`
    ///    or `keyboardShortcut(KeyEquivalent("X"), modifiers: ...)`.
    ///    `X` is any single non-quote non-backslash character —
    ///    covers alphanumerics AND punctuation (`,`, `/`, etc.).
    /// 2. **Special-key dot-syntax** — `keyboardShortcut(.upArrow,
    ///    modifiers: ...)` etc. Only the eight constants we plausibly
    ///    use as chords (arrows + return / tab / space / escape).
    ///    Excludes `.cancelAction` / `.defaultAction` (no `modifiers:`
    ///    argument; regex skips naturally).
    static func extractChords(from text: String) -> Set<String> {
        var chords: Set<String> = []
        let range = NSRange(text.startIndex..., in: text)

        Self.quotedKeyRegex.enumerateMatches(in: text, range: range) { match, _, _ in
            guard let match,
                  let keyRange = Range(match.range(at: 1), in: text),
                  let modsRange = Range(match.range(at: 2), in: text)
            else { return }
            let key = String(text[keyRange]).uppercased()
            let mods = String(text[modsRange])
            chords.insert(Self.formatChord(key: key, modifiers: mods))
        }

        Self.specialKeyRegex.enumerateMatches(in: text, range: range) { match, _, _ in
            guard let match,
                  let keyRange = Range(match.range(at: 1), in: text),
                  let modsRange = Range(match.range(at: 2), in: text)
            else { return }
            let key = Self.glyphForSpecialKey(String(text[keyRange]))
            let mods = String(text[modsRange])
            chords.insert(Self.formatChord(key: key, modifiers: mods))
        }

        return chords
    }

    /// Translate a SwiftUI `KeyEquivalent` special-key constant
    /// name to its display glyph. Single source of truth for the
    /// scraper's special-key formatting.
    ///
    /// `fatalError` on unknown input — if anyone widens the
    /// `specialKeys` regex alternation in `extractChords(from:)`
    /// without updating this switch, the test crashes loudly
    /// instead of producing nonsense chord strings like `"⌘home"`
    /// that would either fail the drift test for the wrong reason
    /// or false-pass via an `deliberatelyUnregisteredChords`
    /// match. Keeps the regex list and the glyph table in
    /// lock-step.
    ///
    /// SwiftUI's `KeyEquivalent` has no `.enter` — Return is
    /// `.return`. `.delete` is Backspace; `.deleteForward` is the
    /// distinct "del" / fn+Delete key. Both are covered.
    private static func glyphForSpecialKey(_ name: String) -> String {
        switch name {
        case "upArrow":       return "↑"
        case "downArrow":     return "↓"
        case "leftArrow":     return "←"
        case "rightArrow":    return "→"
        case "return":        return "↩"
        case "tab":           return "⇥"
        case "space":         return "␣"
        case "escape":        return "⎋"
        case "delete":        return "⌫"
        case "deleteForward": return "⌦"
        case "clear":         return "⌧"
        case "home":          return "↖"
        case "end":           return "↘"
        case "pageUp":        return "⇞"
        case "pageDown":      return "⇟"
        default:
            fatalError(
                "Unknown special key '\(name)' — "
                + "add a case to glyphForSpecialKey or "
                + "remove it from the specialKeys regex alternation."
            )
        }
    }

    // MARK: - Compiled regexes (hoisted from extractChords)
    //
    // The two patterns compile once at class-load instead of per
    // call to extractChords(from:). With ~47 files in
    // Sources/SlateMac/, the prior `try! NSRegularExpression(...)`
    // inside the function would recompile twice per file — sub-
    // millisecond but unnecessary.

    /// `keyboardShortcut("X", modifiers: ...)` /
    /// `keyboardShortcut(KeyEquivalent("X"), modifiers: ...)` —
    /// `X` is any single non-quote non-backslash char (alphanumerics
    /// + punctuation). Requires non-empty `modifiers:` to skip
    /// sheet-dismiss bindings declared with `modifiers: []`.
    private static let quotedKeyRegex: NSRegularExpression = {
        // swiftlint:disable:next force_try
        try! NSRegularExpression(
            pattern: #"keyboardShortcut\(\s*(?:KeyEquivalent\(\s*)?"([^"\\])"\s*\)?\s*,\s*modifiers:\s*(\[[^\]]+\]|\.command|\.shift|\.option|\.control|\.function)"#
        )
    }()

    /// `keyboardShortcut(.<specialKey>, modifiers: ...)` —
    /// special-key constants explicitly listed so a typo in source
    /// (`.upArro`) doesn't get scraped as a chord. Includes
    /// `.deleteForward` distinct from `.delete` (macOS treats them
    /// as different keys — Backspace vs the "del" key). Function
    /// keys (`KeyEquivalent("\u{F704}")` shape) are deferred — no
    /// source uses them yet.
    private static let specialKeyRegex: NSRegularExpression = {
        let specialKeys =
            "upArrow|downArrow|leftArrow|rightArrow"
            + "|return|tab|space|escape"
            + "|delete|deleteForward"
            + "|clear|home|end|pageUp|pageDown"
        // swiftlint:disable:next force_try
        return try! NSRegularExpression(
            pattern: #"keyboardShortcut\(\s*\.(\#(specialKeys))\s*,\s*modifiers:\s*(\[[^\]]+\]|\.command|\.shift|\.option|\.control|\.function)"#
        )
    }()

    private static func formatChord(key: String, modifiers: String) -> String {
        var glyphs = ""
        if modifiers.contains(".control")  { glyphs += "⌃" }
        if modifiers.contains(".option")   { glyphs += "⌥" }
        if modifiers.contains(".shift")    { glyphs += "⇧" }
        if modifiers.contains(".command")  { glyphs += "⌘" }
        if modifiers.contains(".function") { glyphs += "fn+" }
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
    ///
    /// Uses a regex (`Settings\s*\{`) rather than literal
    /// `"Settings {"` so a future formatter pass that varies
    /// whitespace around the brace doesn't false-trip the check.
    @MainActor
    func testSettingsSceneStillExistsInSlateMacApp() async throws {
        let appFile = Self.projectRoot
            .appendingPathComponent("apps")
            .appendingPathComponent("slate-mac")
            .appendingPathComponent("Sources")
            .appendingPathComponent("SlateMac")
            .appendingPathComponent("SlateMacApp.swift")
        let text = try String(contentsOf: appFile, encoding: .utf8)
        let sceneRegex = try NSRegularExpression(pattern: #"\bSettings\s*\{"#)
        let range = NSRange(text.startIndex..., in: text)
        let match = sceneRegex.firstMatch(in: text, range: range)
        XCTAssertNotNil(
            match,
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

    // MARK: - Drift scraper coverage (#322)

    /// Pure-function self-tests of `extractChords(from:)` — the
    /// regex-level coverage the integration drift test can't
    /// exercise directly. Without these, a regex regression that
    /// stops matching e.g. punctuation keys would silently let
    /// drift-untracked chords ship.

    func testExtractChordsHandlesAlphanumericKey() {
        let text = #".keyboardShortcut("s", modifiers: .command)"#
        XCTAssertEqual(
            SlateCommandsTests.extractChords(from: text),
            ["⌘S"]
        )
    }

    func testExtractChordsHandlesPunctuationKey() {
        // The case that prompted #322 — `Cmd+,` for Settings was
        // never scraped by the old `[a-zA-Z0-9]` regex.
        let text = #".keyboardShortcut(",", modifiers: [.command])"#
        XCTAssertEqual(
            SlateCommandsTests.extractChords(from: text),
            ["⌘,"]
        )
    }

    func testExtractChordsHandlesSlashKey() {
        let text = #".keyboardShortcut("/", modifiers: [.command])"#
        XCTAssertEqual(
            SlateCommandsTests.extractChords(from: text),
            ["⌘/"]
        )
    }

    func testExtractChordsHandlesKeyEquivalentWrapped() {
        // The KeyEquivalent(...) wrapper form, used in PropertiesPanel.
        let text = #".keyboardShortcut(KeyEquivalent("r"), modifiers: [.command, .shift])"#
        XCTAssertEqual(
            SlateCommandsTests.extractChords(from: text),
            ["⇧⌘R"]
        )
    }

    func testExtractChordsHandlesSpecialKeyWithModifiers() {
        // `.return` paired with `.command` is a real chord (the
        // TemplatePromptSheet commit binding).
        let text = #".keyboardShortcut(.return, modifiers: [.command])"#
        XCTAssertEqual(
            SlateCommandsTests.extractChords(from: text),
            ["⌘↩"]
        )
    }

    func testExtractChordsDistinguishesDeleteFromDeleteForward() {
        // `.delete` is Backspace (⌫); `.deleteForward` is the
        // "del" key / fn+Delete (⌦). Different keys, different
        // glyphs — a future chord using one shouldn't surface as
        // the other.
        XCTAssertEqual(
            SlateCommandsTests.extractChords(
                from: #".keyboardShortcut(.delete, modifiers: .command)"#
            ),
            ["⌘⌫"]
        )
        XCTAssertEqual(
            SlateCommandsTests.extractChords(
                from: #".keyboardShortcut(.deleteForward, modifiers: .command)"#
            ),
            ["⌘⌦"]
        )
    }

    func testExtractChordsHandlesArrowKeyWithModifiers() {
        // Hypothetical: `Cmd+↓` for "jump to bottom".
        let text = #".keyboardShortcut(.downArrow, modifiers: [.command])"#
        XCTAssertEqual(
            SlateCommandsTests.extractChords(from: text),
            ["⌘↓"]
        )
    }

    func testExtractChordsSkipsBareSpecialKey() {
        // `.escape` without modifiers is SwiftUI's sheet-dismiss
        // binding, not a global menu chord. Empty-array modifiers
        // also count as "bare" — the regex requires non-empty.
        let bareCases = [
            #".keyboardShortcut(.escape, modifiers: [])"#,
            #".keyboardShortcut(.return, modifiers: [])"#,
        ]
        for text in bareCases {
            XCTAssertTrue(
                SlateCommandsTests.extractChords(from: text).isEmpty,
                "bare special key without modifiers must not scrape: \(text)"
            )
        }
    }

    func testExtractChordsSkipsCancelAndDefaultAction() {
        // The `.cancelAction` / `.defaultAction` overloads don't
        // take a `modifiers:` argument, so the regex naturally
        // misses them. Lock in that contract.
        let semanticCases = [
            ".keyboardShortcut(.cancelAction)",
            ".keyboardShortcut(.defaultAction)",
        ]
        for text in semanticCases {
            XCTAssertTrue(
                SlateCommandsTests.extractChords(from: text).isEmpty,
                "semantic-role shortcut must not scrape: \(text)"
            )
        }
    }

    func testExtractChordsFindsMultipleChordsInOneFile() {
        let text = """
            Button("A") {}.keyboardShortcut("a", modifiers: .command)
            Button("B") {}.keyboardShortcut("b", modifiers: [.command, .shift])
            Button("Comma") {}.keyboardShortcut(",", modifiers: [.command])
            """
        XCTAssertEqual(
            SlateCommandsTests.extractChords(from: text),
            ["⌘A", "⇧⌘B", "⌘,"]
        )
    }

    /// Recursive directory walk — fixture has a nested subdirectory
    /// holding a `.swift` file with a chord declaration. The old
    /// `contentsOfDirectory(at:)` scraper would silently miss it.
    func testScrapeChordsFromDirectoryWalksRecursively() throws {
        let tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-322-scrape-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: tempDir, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: tempDir) }

        // Top-level .swift
        try #".keyboardShortcut("a", modifiers: .command)"#.write(
            to: tempDir.appendingPathComponent("Top.swift"),
            atomically: true,
            encoding: .utf8
        )
        // Nested .swift two levels deep
        let nested = tempDir
            .appendingPathComponent("Sub")
            .appendingPathComponent("Deeper")
        try FileManager.default.createDirectory(at: nested, withIntermediateDirectories: true)
        try #".keyboardShortcut("b", modifiers: [.command, .shift])"#.write(
            to: nested.appendingPathComponent("Nested.swift"),
            atomically: true,
            encoding: .utf8
        )
        // Non-swift file in nested dir — should be ignored
        try #".keyboardShortcut("Z", modifiers: .command)"#.write(
            to: nested.appendingPathComponent("ignored.txt"),
            atomically: true,
            encoding: .utf8
        )

        let chords = try SlateCommandsTests.scrapeChordsFromDirectory(tempDir)
        XCTAssertEqual(
            chords,
            ["⌘A", "⇧⌘B"],
            "must find top-level + nested .swift; must skip .txt"
        )
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
