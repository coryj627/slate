// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// Thrown by a drift-test source locator that can't find its
/// target (#344). Paired with an `XCTFail` carrying the diagnostic;
/// the throw just exits the `throws` locator. File-private —
/// `CloseVaultSheetParityTests` declares its own so each drift-test
/// file stays self-contained.
private struct DriftLocatorError: Error { let message: String }

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

    /// **Reverse drift check** (#330). The forward test above only
    /// catches `keyboardShortcut(...)` in source without a matching
    /// registry entry. This test catches the inverse: a registry
    /// entry whose `hotkeyHint` doesn't map to any source-side
    /// shortcut OR any SwiftUI scene that auto-installs one.
    ///
    /// Why this matters: before #320 every registry chord came
    /// from an explicit `keyboardShortcut(...)`, so a one-direction
    /// check was sufficient. #320 introduced `⌘,` for
    /// `slate.settings.open`, which SwiftUI auto-installs via the
    /// `Settings { }` scene — there's no source-side declaration
    /// for the scraper to find. A future contributor adding a
    /// `hotkeyHint: "⌘X"` to a registry entry without a matching
    /// keyboardShortcut (and forgetting the implicit-allow-list
    /// entry) would ship a palette chord that does nothing from
    /// the menu bar — a silent UX regression. This test forces
    /// the matching declaration to exist.
    @MainActor
    func testEveryRegistryChordHasASourceOrImplicitMenuBinding() throws {
        let menuChords = try Self.scrapedMenuChords()
        let appState = AppState()
        let registryChords: Set<String> = Set(
            appState.commandRegistry.list().compactMap(\.hotkeyHint)
        )

        // #330 red-team F1 (P1): the scrape returns EVERY
        // `keyboardShortcut(...)` chord in our source, including
        // sheet-scoped declarations (TemplatePromptSheet's `⌘↩`
        // submit, PropertyEditorRow's `⌘⌫` delete). Those are NOT
        // menu-bar-reachable — they live in `deliberatelyUnregisteredChords`
        // precisely because they're palette-omitted by design.
        // Without this subtraction, a registry entry mistakenly
        // bound to `⌘↩` would pass the reverse test because the
        // scrape sees the sheet-only declaration — defeating the
        // whole point of the check.
        let menuReachableChords = menuChords
            .subtracting(Self.deliberatelyUnregisteredChords)
        let allKnownChords = menuReachableChords
            .union(Self.chordsImplicitFromSwiftUIScenes)
        let orphans = registryChords.subtracting(allKnownChords)
        XCTAssertTrue(
            orphans.isEmpty,
            "Registry has chords with no menu binding (source-reachable or implicit-from-SwiftUI): \(orphans.sorted()). " +
            "Either add a matching keyboardShortcut(...) declaration in a menu-reachable scope, " +
            "or — if it's auto-installed by a SwiftUI scene — add the chord to chordsImplicitFromSwiftUIScenes with a comment citing the scene."
        )

        // Symmetric staleness: an entry in the implicit allow-list
        // that no registry hotkeyHint claims is dead weight, and
        // worse: it silently shields the next added chord with the
        // same string. Mirrors the staleness check on
        // `deliberatelyUnregisteredChords` above.
        let staleImplicitEntries = Self.chordsImplicitFromSwiftUIScenes
            .subtracting(registryChords)
        XCTAssertTrue(
            staleImplicitEntries.isEmpty,
            "chordsImplicitFromSwiftUIScenes has entries with no matching registry chord: \(staleImplicitEntries.sorted()). " +
            "Either the registry command was removed (drop the allow-list entry) " +
            "or the implicit chord is no longer auto-installed (investigate)."
        )

        // #330 red-team F3 (P2) + Codoki review on #346: the two
        // allow-lists must be disjoint on the MENU-REACHABLE
        // subset. `deliberatelyUnregisteredChords` is for chords
        // that exist in source but are deliberately NOT in the
        // registry (sheet-only bindings like TemplatePromptSheet's
        // `⌘↩`); `chordsImplicitFromSwiftUIScenes` is for chords
        // that ARE in the registry but have NO source declaration
        // (auto-installed by SwiftUI scenes).
        //
        // Intersecting against raw `menuChords` would false-trip on
        // a sheet-only declaration colliding with an implicit chord
        // — e.g. if someone adds `.keyboardShortcut(",", ...)` to
        // a sheet submit button, the implicit `⌘,` entry is still
        // needed for the Settings scene; dropping it would break
        // the orphan check. Intersecting `menuReachableChords`
        // catches the real regression: a source-side menu-bar-
        // reachable declaration that retires the implicit entry.
        let overlap = Self.chordsImplicitFromSwiftUIScenes
            .intersection(menuReachableChords)
        XCTAssertTrue(
            overlap.isEmpty,
            "chordsImplicitFromSwiftUIScenes overlaps with menu-reachable source chords: \(overlap.sorted()). " +
            "A source-side keyboardShortcut(...) was added in a menu-reachable position for what was previously an implicit chord — drop the now-redundant implicit allow-list entry."
        )
    }

    /// **#422 dead-zone gate.** Every registry chord must be declared
    /// in the menu bar (`SlateMacApp.swift`'s `.commands` block) or
    /// auto-installed by a SwiftUI scene. A chord whose only source
    /// declaration is a toolbar/hidden-button `keyboardShortcut`
    /// satisfies the all-files reverse check above while being
    /// unreachable with sidebar focus — exactly the ⌘F defect #422
    /// fixed, and the defect ⌘S / ⇧⌘N / ⇧⌘T / ⇧⌘J / ⌘J / ⇧⌘R shipped
    /// with until the menu migration. This test makes the regression
    /// class red instead of silently green.
    @MainActor
    func testEveryRegistryChordIsDeclaredInTheMenuBar() throws {
        let menuBarChords = try Self.scrapedMenuBarChords()
        let appState = AppState()
        let registryChords: Set<String> = Set(
            appState.commandRegistry.list().compactMap(\.hotkeyHint)
        )
        let reachable = menuBarChords
            .union(Self.chordsImplicitFromSwiftUIScenes)
        let deadZone = registryChords.subtracting(reachable)
        XCTAssertTrue(
            deadZone.isEmpty,
            "Registry chords with no menu-bar declaration (#422 dead-zone — a "
                + "toolbar/hidden-button keyboardShortcut is unreachable with sidebar "
                + "focus): \(deadZone.sorted()). Move the chord to a CommandGroup in "
                + "SlateMacApp.swift and leave the in-view button click/AX-only, or — "
                + "if a SwiftUI scene auto-installs it — add it to "
                + "chordsImplicitFromSwiftUIScenes with a comment citing the scene."
        )
    }

    /// Chords known to be auto-installed by SwiftUI scene
    /// declarations rather than by explicit `keyboardShortcut(...)`
    /// calls in our source. These are real menu chords from the
    /// user's perspective, but the scraper can't find them.
    ///
    /// New entries need a comment naming the scene and the file
    /// it lives in — keeps the allow-list auditable and forces a
    /// contributor to verify that the chord IS in fact installed
    /// by the named scene rather than just assumed.
    static let chordsImplicitFromSwiftUIScenes: Set<String> = [
        // SwiftUI's `Settings { }` scene (SlateMacApp.swift) auto-
        // installs the "Preferences…" menu item under the app
        // menu with the standard `⌘,` shortcut. There is no
        // `.keyboardShortcut(...)` declaration for it anywhere
        // in source — the scene is the source of truth.
        "⌘,",
    ]

    /// Chords intentionally absent from the registry. New entries
    /// here need a comment explaining why — the drift check defers
    /// to this list rather than silently passing.
    static let deliberatelyUnregisteredChords: Set<String> = [
        // Self-reference: showing the palette from inside the
        // palette would be weird UX, and toggling-to-close
        // duplicates the Esc dismissal already wired in #313.
        "⇧⌘P",
        // #372: the system Undo/Redo pair. The menu owns the chords
        // and routes them by focus (canvas stack vs responder chain);
        // duplicating "Undo" as a palette row would shadow the
        // context-dependence (which stack fires depends on focus, and
        // the palette steals focus).
        "⌘Z", "⇧⌘Z",
        // NotePropertiesHeader.swift's source-mode "Apply" binding
        // (⌘↩ commits the YAML draft). In-sheet submission action,
        // structurally identical to a global chord but scoped to the
        // view's responder chain by SwiftUI. Not a menu / palette-
        // worthy command. Picked up by the broader scraper (#322) —
        // earlier scrapers missed `.return` entirely because it
        // isn't a quoted single char. (TemplatePromptSheet's former
        // ⌘↩ moved to `.defaultAction` — bare Return + the visually-
        // primary default treatment — in the HIG-conformance pass.)
        "⌘↩",
        // PropertyEditorRow.swift's per-row Delete button. Row-
        // scoped action — deletes one property of the focused
        // row, not a global "delete property" verb. Picked up by
        // the #322 scraper's new `.delete` coverage.
        "⌘⌫",
        // SlateMacApp.swift's "Close Window" (U1-2, #454): the
        // explicit stand-in for the window's implicit ⌘W after
        // "Close Tab" claimed that chord. A window action routed
        // to performClose:, not a vault verb — the palette's own
        // host window would be the thing being closed.
        "⇧⌘W",
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
        // #344: hard-fail with a precise message if `projectRoot`
        // resolution broke. Without this, a wrong `sourcesDir` makes
        // `scrapeChordsFromDirectory` return an empty set, and the
        // drift tests fail downstream with a misleading "regex may
        // be broken" / orphan-chord message instead of pointing at
        // the real cause (the repo layout / path walk-up changed).
        // A locator that can't find its target must fail loudly, not
        // let the drift checks run against nothing.
        guard FileManager.default.fileExists(atPath: sourcesDir.path) else {
            let message =
                "Sources/SlateMac not found at \(sourcesDir.path) — projectRoot "
                + "resolution from \(#filePath) likely broke (repo layout change). "
                + "The drift scraper has nothing to read; fix projectRoot rather "
                + "than letting the menu↔registry drift checks pass on an empty scrape."
            XCTFail(message)
            throw DriftLocatorError(message: message)
        }
        return try scrapeChordsFromDirectory(sourcesDir)
    }

    /// Scrape ONLY `SlateMacApp.swift` — the file whose `.commands`
    /// block declares the menu bar. #422 hardening: the all-files
    /// scrape above treats a `keyboardShortcut(...)` on a toolbar or
    /// hidden button as a "menu binding", but those registrations are
    /// DEAD with sidebar focus (the ⌘F lesson — AppKit's key-window
    /// sweep doesn't reach them from every responder). Menu-bar
    /// reachability is a property of `SlateMacApp.swift` alone; if a
    /// second file ever hosts a `.commands` block, add it here with a
    /// comment.
    static func scrapedMenuBarChords() throws -> Set<String> {
        let appFile = projectRoot
            .appendingPathComponent("apps")
            .appendingPathComponent("slate-mac")
            .appendingPathComponent("Sources")
            .appendingPathComponent("SlateMac")
            .appendingPathComponent("SlateMacApp.swift")
        guard FileManager.default.fileExists(atPath: appFile.path) else {
            let message =
                "SlateMacApp.swift not found at \(appFile.path) — projectRoot "
                + "resolution from \(#filePath) likely broke (repo layout change), "
                + "or the app entry point moved. The menu-bar reachability check "
                + "has nothing to read; fix the locator rather than letting the "
                + "#422 dead-zone gate pass on an empty scrape."
            XCTFail(message)
            throw DriftLocatorError(message: message)
        }
        let text = try String(contentsOf: appFile, encoding: .utf8)
        return extractChords(from: text)
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
            // Unescape (#335) BEFORE uppercasing: a `\\` / `\"`
            // capture must collapse to `\` / `"` first.
            let key = Self.unescapeQuotedKey(String(text[keyRange])).uppercased()
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
    /// `X` is one of:
    /// - an escaped backslash `\\` (the source text for a `\` key)
    /// - an escaped quote `\"` (the source text for a `"` key)
    /// - any other single non-quote non-backslash char
    ///   (alphanumerics + punctuation).
    ///
    /// The two escape branches (#335) let the scraper see
    /// `keyboardShortcut("\\", …)` / `keyboardShortcut("\"", …)`,
    /// which the old single-char `[^"\\]` class skipped. The
    /// two-char capture is unescaped in `extractChords` before
    /// formatting (`\\` → `\`, `\"` → `"`). Requires non-empty
    /// `modifiers:` to skip sheet-dismiss bindings declared with
    /// `modifiers: []`.
    ///
    /// Both escape alternatives are written with a backslash-
    /// escaped special char for symmetry: `\\\\` matches a literal
    /// backslash, `\\\"` matches a literal quote. (In ICU a bare
    /// `"` also matches a quote, so `\\"` would be equivalent — but
    /// the escaped form keeps the two branches visually parallel
    /// and the intent unambiguous. #349 review.)
    private static let quotedKeyRegex: NSRegularExpression = {
        // swiftlint:disable:next force_try
        try! NSRegularExpression(
            pattern: #"keyboardShortcut\(\s*(?:KeyEquivalent\(\s*)?"((?:\\\\|\\\"|[^"\\]))"\s*\)?\s*,\s*modifiers:\s*(\[[^\]]+\]|\.command|\.shift|\.option|\.control|\.function)"#
        )
    }()

    /// Unescape a captured quoted-key (#335). The `quotedKeyRegex`
    /// capture is the raw *source text* between the quotes, so a
    /// backslash key arrives as the two characters `\\` and a quote
    /// key as `\"`. Collapse those two escape sequences to the
    /// single character they denote; every other capture (a lone
    /// alphanumeric or punctuation char) passes through unchanged.
    private static func unescapeQuotedKey(_ raw: String) -> String {
        switch raw {
        case #"\\"#: return #"\"#  // escaped backslash → one backslash
        case #"\""#: return "\""    // escaped quote → one quote
        default: return raw
        }
    }

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
    /// command without a session must not throw AND must NOT reveal
    /// the Tasks Review leaf (#879 — it's a leaf now, not a sheet).
    /// Full positive coverage (with a vault open) rides the
    /// integration suite (#317).
    @MainActor
    func testInvokingTasksReviewCommandWithNoSessionIsNoOp() async throws {
        let appState = AppState()
        XCTAssertNotEqual(appState.workspace.activeLeaf, .tasksReview)
        try appState.commandRegistry.invokeById(id: SlateCommandID.tasksReview)
        XCTAssertNotEqual(
            appState.workspace.activeLeaf, .tasksReview,
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

    /// Invoking `slate.view.toggleRightPane` (#882) flips
    /// `isRightPaneVisible` — the palette twin of View ▸ Hide/Show Right
    /// Pane (⌥⌘I). The chord wiring itself is covered by the drift/dead-zone
    /// tests above; this pins the action's observable effect.
    @MainActor
    func testInvokingToggleRightPaneFlipsVisibility() async throws {
        let appState = AppState()
        XCTAssertTrue(appState.isRightPaneVisible, "the right pane starts visible")
        try appState.commandRegistry.invokeById(id: SlateCommandID.toggleRightPane)
        XCTAssertFalse(appState.isRightPaneVisible)
        try appState.commandRegistry.invokeById(id: SlateCommandID.toggleRightPane)
        XCTAssertTrue(appState.isRightPaneVisible)
    }

    /// Invoking `slate.editor.addProperty` without an active note must not
    /// present a hostless sheet that cannot safely own a property draft.
    @MainActor
    func testInvokingAddPropertyCommandWithoutNoteDoesNotOpenHostlessSheet() async throws {
        let appState = AppState()
        XCTAssertFalse(appState.isAddPropertySheetOpen)
        try appState.commandRegistry.invokeById(id: SlateCommandID.addProperty)
        XCTAssertFalse(appState.isAddPropertySheetOpen)
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

    /// `slate.help.open` (U4-3, #472) routes the repository README URL through
    /// AppState's injected `externalOpener` (gap G13) — the same hand-off the
    /// SidebarUtilityBar "Help" button uses. A recording opener stands in for
    /// `NSWorkspace.open`, so invoking the command opens exactly the README URL
    /// without spawning a browser.
    @MainActor
    func testInvokingOpenHelpCommandOpensReadmeURL() async throws {
        var opened: [URL] = []
        let appState = AppState(externalOpener: { url in
            opened.append(url)
            return true
        })
        try appState.commandRegistry.invokeById(id: SlateCommandID.openHelp)
        XCTAssertEqual(opened, [AppState.helpURL])
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
    ///
    /// #333: the source is run through `SwiftSourceStripping` first
    /// so a `// TODO: restore the Settings { } scene` comment or a
    /// `"Settings { ... }"` string literal can't false-pass the
    /// check. The match must come from real scene-declaration code.
    @MainActor
    func testSettingsSceneStillExistsInSlateMacApp() async throws {
        let appFile = Self.projectRoot
            .appendingPathComponent("apps")
            .appendingPathComponent("slate-mac")
            .appendingPathComponent("Sources")
            .appendingPathComponent("SlateMac")
            .appendingPathComponent("SlateMacApp.swift")
        let rawText = try String(contentsOf: appFile, encoding: .utf8)
        // #333 red-team P3: SwiftSourceStripping deliberately does
        // NOT model multiline (`"""…"""`) or raw (`#"…"#`) string
        // literals (see its doc). SlateMacApp.swift has none today,
        // so the strip below is faithful. Make that assumption
        // self-enforcing: if a future edit adds one of those
        // constructs, this fails LOUDLY — prompting a stripper
        // upgrade — rather than silently risking a false negative
        // on the scene grep (a mis-modelled string could blank past
        // a real `Settings {`).
        //
        // This is a conservative over-approximation (#348 Codoki):
        // it also trips if a *comment* merely mentions `"""` / `#"`.
        // That's an acceptable false alarm — it fails safe (over-
        // cautious), the message points at the cause, and a comment
        // referencing those tokens in this tiny app-entry file is
        // about as unlikely as the construct itself. Refine to a
        // comment-aware check only if it ever false-fires.
        XCTAssertFalse(
            rawText.contains("\"\"\"") || rawText.contains("#\""),
            "SlateMacApp.swift gained a multiline or raw string literal, which "
                + "SwiftSourceStripping does not model. Upgrade the stripper "
                + "(see #333 option 2 — swift-syntax) before relying on the "
                + "stripped scene grep below."
        )
        let text = SwiftSourceStripping.strippingCommentsAndStrings(rawText)
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

    func testExtractChordsHandlesEscapedBackslashKey() {
        // #335: `keyboardShortcut("\\", …)` — the source text has
        // TWO backslash chars between the quotes (a raw string here
        // reproduces that), denoting a single `\` key. The old
        // single-char `[^"\\]` class skipped it entirely.
        let text = #".keyboardShortcut("\\", modifiers: [.command])"#
        XCTAssertEqual(
            SlateCommandsTests.extractChords(from: text),
            ["⌘\\"],  // ⌘ + literal backslash
            "escaped-backslash key must scrape as ⌘\\ after unescaping"
        )
    }

    func testExtractChordsHandlesEscapedQuoteKey() {
        // #335: `keyboardShortcut("\"", …)` — the source text has
        // backslash-then-quote between the outer quotes, denoting a
        // single `"` key.
        let text = #".keyboardShortcut("\"", modifiers: [.command])"#
        XCTAssertEqual(
            SlateCommandsTests.extractChords(from: text),
            ["⌘\""],  // ⌘ + literal double-quote
            "escaped-quote key must scrape as ⌘\" after unescaping"
        )
    }

    func testExtractChordsHandlesEscapedKeyViaKeyEquivalentWrapper() {
        // The wrapper form must also unescape — exercises the
        // `(?:KeyEquivalent\(\s*)?` branch alongside the escape.
        let text = #".keyboardShortcut(KeyEquivalent("\\"), modifiers: [.command, .shift])"#
        XCTAssertEqual(
            SlateCommandsTests.extractChords(from: text),
            ["⇧⌘\\"]
        )
    }

    func testExtractChordsHandlesEscapedQuoteViaKeyEquivalentWrapper() {
        // #349 review: exercise the escaped-QUOTE branch through the
        // KeyEquivalent wrapper too, so both escape branches are
        // covered through both call forms (bare + wrapper).
        let text = #".keyboardShortcut(KeyEquivalent("\""), modifiers: [.command])"#
        XCTAssertEqual(
            SlateCommandsTests.extractChords(from: text),
            ["⌘\""]
        )
    }

    func testExtractChordsEscapedKeysCoexistWithOrdinaryKeys() {
        // A file mixing an ordinary key, an escaped backslash, and
        // an escaped quote — all three must surface, none clobber.
        let text = #"""
            .keyboardShortcut("s", modifiers: .command)
            .keyboardShortcut("\\", modifiers: .command)
            .keyboardShortcut("\"", modifiers: .command)
            """#
        XCTAssertEqual(
            SlateCommandsTests.extractChords(from: text),
            ["⌘S", "⌘\\", "⌘\""]
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

    /// #345 (from #330 red-team F2): a `.keyboardShortcut(...)` whose
    /// arguments span multiple lines — e.g. after a `swift-format`
    /// reflow — must still be scraped. NSRegularExpression's `\s`
    /// matches newlines (unlike `.`), and the scraper uses `\s*`
    /// between every token plus `[^\]]+` inside the modifier
    /// brackets (also newline-tolerant), so multi-line declarations
    /// already work. This pins that behaviour so a future regex
    /// change can't silently regress it — which would make the
    /// drift tests mis-attribute the lost chord as "missing from
    /// the registry" / an orphan rather than "scraper stopped
    /// seeing it".
    func testExtractChordsHandlesMultiLineDeclarations() {
        let cases: [(label: String, source: String, expected: Set<String>)] = [
            (
                "args on separate lines",
                """
                .keyboardShortcut(
                    "x",
                    modifiers: [.command]
                )
                """,
                ["⌘X"]
            ),
            (
                "KeyEquivalent wrapper split across lines",
                """
                .keyboardShortcut(
                    KeyEquivalent("y"),
                    modifiers: [.command, .shift]
                )
                """,
                ["⇧⌘Y"]
            ),
            (
                "modifiers array spanning lines",
                """
                .keyboardShortcut("z", modifiers: [
                    .command,
                    .shift
                ])
                """,
                ["⇧⌘Z"]
            ),
        ]
        for c in cases {
            XCTAssertEqual(
                SlateCommandsTests.extractChords(from: c.source),
                c.expected,
                "multi-line form '\(c.label)' must still scrape"
            )
        }
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

    // MARK: - Quick switcher registry (#495, chords per #863)

    /// `slate.workspace.quickOpen` is registered, labelled "Quick
    /// Open…", grouped under Navigation, and carries ⌘O — Obsidian's
    /// actual quick-switcher default (#863 superseded #495's ⌘T).
    @MainActor
    func testQuickOpenCommandIsRegisteredWithCommandO() throws {
        let appState = AppState()
        let cmd = appState.commandRegistry.list().first { $0.id == SlateCommandID.quickOpen }
        let quickOpen = try XCTUnwrap(cmd, "quickOpen must be registered")
        XCTAssertEqual(quickOpen.label, "Quick Open…")
        XCTAssertEqual(quickOpen.section, .navigation)
        XCTAssertEqual(quickOpen.hotkeyHint, "⌘O", "⌘O maps to Quick Open (#863)")
    }

    /// The Duplicate Tab command keeps the `newTab` id (stability
    /// contract) and carries ⌘T — the chord returned to the tab
    /// family when Quick Open moved to ⌘O (#863).
    @MainActor
    func testDuplicateTabCommandCarriesCommandT() throws {
        let appState = AppState()
        let cmd = appState.commandRegistry.list().first { $0.id == SlateCommandID.newTab }
        let dup = try XCTUnwrap(cmd, "the newTab id must still be registered")
        XCTAssertEqual(dup.label, "Duplicate Tab")
        XCTAssertEqual(dup.hotkeyHint, "⌘T", "⌘T is Duplicate Tab again (#863)")
    }

    /// Reopen Closed Tab (#863): registered in the View section with
    /// ⇧⌘T — the macOS/Obsidian convention next to Close Tab.
    @MainActor
    func testReopenClosedTabCommandIsRegisteredWithShiftCommandT() throws {
        let appState = AppState()
        let cmd = appState.commandRegistry.list().first {
            $0.id == SlateCommandID.reopenClosedTab
        }
        let reopen = try XCTUnwrap(cmd, "reopenClosedTab must be registered")
        XCTAssertEqual(reopen.label, "Reopen Closed Tab")
        XCTAssertEqual(reopen.section, .view)
        XCTAssertEqual(reopen.hotkeyHint, "⇧⌘T")
    }

    // MARK: - Single-claimant cross-checks (#863)
    //
    // Each reallocated chord must have exactly ONE registry claimant —
    // no accidental double-binding survived the move. Exact-string
    // filters: "⌘R" does not match "⌥⌘R"/"⇧⌘R"/"⌃⌘R" and so on.

    @MainActor
    func testExactlyOneCommandClaimsCommandT() {
        let appState = AppState()
        let claimants = appState.commandRegistry.list().filter { $0.hotkeyHint == "⌘T" }
        XCTAssertEqual(claimants.map(\.id), [SlateCommandID.newTab])
    }

    @MainActor
    func testExactlyOneCommandClaimsCommandO() {
        let appState = AppState()
        let claimants = appState.commandRegistry.list().filter { $0.hotkeyHint == "⌘O" }
        XCTAssertEqual(claimants.map(\.id), [SlateCommandID.quickOpen])
    }

    @MainActor
    func testExactlyOneCommandClaimsShiftCommandO() {
        let appState = AppState()
        let claimants = appState.commandRegistry.list().filter { $0.hotkeyHint == "⇧⌘O" }
        XCTAssertEqual(claimants.map(\.id), [SlateCommandID.openVault])
    }

    @MainActor
    func testExactlyOneCommandClaimsCommandR() {
        let appState = AppState()
        let claimants = appState.commandRegistry.list().filter { $0.hotkeyHint == "⌘R" }
        XCTAssertEqual(claimants.map(\.id), [SlateCommandID.tasksReview])
    }

    @MainActor
    func testExactlyOneCommandClaimsShiftCommandT() {
        let appState = AppState()
        let claimants = appState.commandRegistry.list().filter { $0.hotkeyHint == "⇧⌘T" }
        XCTAssertEqual(claimants.map(\.id), [SlateCommandID.reopenClosedTab])
    }

    // MARK: - Find chord reallocation (#874)
    //
    // ⌘F = find-in-note (`findInNote`); ⇧⌘F = vault search
    // (`toggleSearch`, moved off ⌘F). The Cory-confirmed 2026-07-12
    // decision reverses the #422 vault-first ⌘F. Exact-string single-
    // claimant filters guard against a stray double-binding surviving
    // the swap.

    @MainActor
    func testFindInNoteCommandIsRegisteredWithCommandF() throws {
        let appState = AppState()
        let cmd = appState.commandRegistry.list().first { $0.id == SlateCommandID.findInNote }
        let find = try XCTUnwrap(cmd, "findInNote must be registered")
        XCTAssertEqual(find.label, "Find…")
        XCTAssertEqual(find.section, .editor)
        XCTAssertEqual(find.hotkeyHint, "⌘F", "⌘F is find-in-note (#874)")
    }

    @MainActor
    func testVaultSearchCommandMovedToShiftCommandF() throws {
        let appState = AppState()
        let cmd = appState.commandRegistry.list().first { $0.id == SlateCommandID.toggleSearch }
        let search = try XCTUnwrap(cmd, "toggleSearch must still be registered")
        XCTAssertEqual(search.section, .view)
        XCTAssertEqual(
            search.hotkeyHint, "⇧⌘F",
            "vault search moved from ⌘F to ⇧⌘F (#874)")
    }

    @MainActor
    func testExactlyOneCommandClaimsCommandF() {
        let appState = AppState()
        let claimants = appState.commandRegistry.list().filter { $0.hotkeyHint == "⌘F" }
        XCTAssertEqual(claimants.map(\.id), [SlateCommandID.findInNote])
    }

    @MainActor
    func testExactlyOneCommandClaimsShiftCommandF() {
        let appState = AppState()
        let claimants = appState.commandRegistry.list().filter { $0.hotkeyHint == "⇧⌘F" }
        XCTAssertEqual(claimants.map(\.id), [SlateCommandID.toggleSearch])
    }

    /// Invoking `slate.editor.findInNote` through the registry routes to
    /// `requestFindInFocusedSurface()` → `showFindInNote()`. With NO note
    /// open AND no vault open (the welcome screen), the fallback runs the
    /// vault-guarded `requestSearchOverlay()`, which announces "open a
    /// vault first" instead of mounting a hostless overlay — so ⌘F does
    /// not throw and does NOT open the overlay here. (The positive
    /// vault-open fallback — overlay actually opens — is covered in
    /// AppStateTests, which can open a real vault.)
    @MainActor
    func testInvokingFindInNoteWithNoVaultDoesNotOpenHostlessOverlay() throws {
        let appState = AppState()
        XCTAssertNil(appState.selectedFilePath)
        XCTAssertFalse(appState.isVaultOpen)
        XCTAssertFalse(appState.isSearchOpen)
        try appState.commandRegistry.invokeById(id: SlateCommandID.findInNote)
        XCTAssertFalse(
            appState.isSearchOpen,
            "⌘F with no vault open must route through the vault guard "
                + "(announce), not mount a hostless search overlay (#874)")
    }

    // The positive "editing mode + markdown tab open publishes
    // findInNoteRequest" test lives in BasesTabRoutingTests, which can
    // open a REAL `.markdown` tab (a mounted NoteContentView is what the
    // corrected `workspace.activeTabPath` guard requires — setting
    // `selectedFilePath` alone no longer satisfies it, by design: a stale
    // path on a Graph/Canvas/Base tab must NOT route to the absent editor).

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

/// #526: docs/help/canvas.md's shortcut table drifts against the
/// registry — every canvas-section command with a hotkey must appear
/// in the doc with that exact chord.
@MainActor
extension SlateCommandsTests {
    func testBasesCommandsRegisterInBasesSectionWithoutGlobalChords() {
        let appState = AppState()
        let commands = Dictionary(uniqueKeysWithValues: appState.commandRegistry.list().map { ($0.id, $0) })
        let expected = [
            SlateCommandID.basesOpenViewSwitcher,
            SlateCommandID.basesNextView,
            SlateCommandID.basesPreviousView,
            SlateCommandID.basesSortByColumn,
            SlateCommandID.basesSaveSortToView,
            SlateCommandID.basesViewAsTable,
            SlateCommandID.basesViewAsList,
            SlateCommandID.basesQuickFilter,
            SlateCommandID.basesWhereAmI,
            SlateCommandID.basesOpenRow,
            SlateCommandID.basesCopyLink,
            SlateCommandID.basesShowBacklinks,
            SlateCommandID.basesEditProperty,
            SlateCommandID.basesExportCSV,
            SlateCommandID.basesExportMarkdown,
            SlateCommandID.basesCopyMarkdown,
            SlateCommandID.basesResultsPopover,
            SlateCommandID.basesRefresh,
            SlateCommandID.basesNewQuery,
            SlateCommandID.basesEditViewFilters,
            SlateCommandID.basesBuilderAddCondition,
            SlateCommandID.basesBuilderAddGroup,
            SlateCommandID.basesBuilderEditCondition,
            SlateCommandID.basesBuilderRemoveCondition,
        ]

        for id in expected {
            let command = commands[id]
            XCTAssertEqual(command?.section, .bases, "\(id) must live in CommandSection.bases")
            XCTAssertNil(command?.hotkeyHint, "\(id) must not introduce a global chord")
        }
    }
}

@MainActor
extension SlateCommandsTests {
    func testCanvasHelpDocCarriesEveryCanvasChord() throws {
        let docURL = Self.projectRoot
            .appendingPathComponent("docs")
            .appendingPathComponent("help")
            .appendingPathComponent("canvas.md")
        let doc = try String(contentsOf: docURL, encoding: .utf8)
        let appState = AppState()
        let canvasChords = appState.commandRegistry.list()
            .filter { $0.section == .canvas }
            .compactMap { command in command.hotkeyHint.map { (command.label, $0) } }
        XCTAssertFalse(canvasChords.isEmpty, "registry lists canvas chords")
        for (label, hotkey) in canvasChords {
            XCTAssertTrue(
                doc.contains(hotkey),
                "docs/help/canvas.md is missing the chord \(hotkey) (\(label)) — update the reference table"
            )
        }
    }

    /// Graph help table drift (P2-3 #559, R3): every `.graph`-section
    /// chord must appear in `docs/help/graph.md`'s keyboard reference.
    func testGraphHelpDocCarriesEveryGraphChord() throws {
        let docURL = Self.projectRoot
            .appendingPathComponent("docs")
            .appendingPathComponent("help")
            .appendingPathComponent("graph.md")
        let doc = try String(contentsOf: docURL, encoding: .utf8)
        let appState = AppState()
        let graphChords = appState.commandRegistry.list()
            .filter { $0.section == .graph }
            .compactMap { command in command.hotkeyHint.map { (command.label, $0) } }
        XCTAssertFalse(graphChords.isEmpty, "registry lists graph chords")
        for (label, hotkey) in graphChords {
            // Match the chord as a whole table cell (`| ⌘0 |`), not a bare
            // substring — otherwise `⌥⌘0` would satisfy a search for `⌘0`
            // and a dropped Actual-Size row would pass (round 4 finding).
            XCTAssertTrue(
                doc.contains("| \(hotkey) |"),
                "docs/help/graph.md keyboard table is missing a row for \(hotkey) (\(label))"
            )
        }
    }

    /// Single-menu-owner (P2-3 #559, R3 / spec §P2-3): the focus-routed
    /// chords must each be declared EXACTLY ONCE in the menu bar
    /// (`SlateMacApp.swift`). The registry carries per-surface palette
    /// mirrors that share the chord as a `hotkeyHint` (canvas + graph +
    /// editor/bases), so ownership is a property of the MENU source, counted
    /// with multiplicity — not the set-based drift scrape. ⌃⌘I ("Where Am
    /// I?") is focus-routed the same way (canvas/graph/bases → `routedWhereAmI`),
    /// so it's here too: a second ⌃⌘I menu item would resurrect the
    /// swallowed-chord bug this test now guards against.
    func testEachFocusRoutedChordHasExactlyOneMenuOwner() throws {
        let appFile = Self.projectRoot
            .appendingPathComponent("apps/slate-mac/Sources/SlateMac/SlateMacApp.swift")
        let src = try String(contentsOf: appFile, encoding: .utf8)
        let declarations: [(String, String)] = [
            ("⌘=", #".keyboardShortcut("=", modifiers: [.command])"#),
            ("⌘-", #".keyboardShortcut("-", modifiers: [.command])"#),
            ("⌘0", #".keyboardShortcut("0", modifiers: [.command])"#),
            ("⌥⌘0", #".keyboardShortcut("0", modifiers: [.command, .option])"#),
            ("⌃⌘I", #".keyboardShortcut("i", modifiers: [.control, .command])"#),
        ]
        for (chord, decl) in declarations {
            let count = src.components(separatedBy: decl).count - 1
            XCTAssertEqual(count, 1, "\(chord) must have exactly one menu owner (found \(count))")
        }
    }

    func testBasesHelpDocCoversEveryStaticBasesCommand() throws {
        let docURL = Self.projectRoot
            .appendingPathComponent("docs")
            .appendingPathComponent("help")
            .appendingPathComponent("bases.md")
        let doc = try String(contentsOf: docURL, encoding: .utf8)
        let appState = AppState()
        let basesCommands = appState.commandRegistry.list()
            .filter { $0.section == .bases && !SlateCommandID.isBasesRunSavedQuery($0.id) }
        XCTAssertFalse(basesCommands.isEmpty, "registry lists static Bases commands")
        for command in basesCommands {
            XCTAssertTrue(
                doc.contains(command.label),
                "docs/help/bases.md is missing the Bases command \(command.label) (\(command.id))"
            )
        }
    }

    func testBasesHelpDocCarriesPinnedSummaryAndDQLMigrationSemantics() throws {
        let docURL = Self.projectRoot
            .appendingPathComponent("docs")
            .appendingPathComponent("help")
            .appendingPathComponent("bases.md")
        let doc = try String(contentsOf: docURL, encoding: .utf8)

        XCTAssertTrue(
            doc.contains("Summaries are computed after filtering and grouping, but before `limit`"),
            "Bases help must pin the post-filter, pre-limit summary contract")
        XCTAssertTrue(doc.contains("`total_count`"), "Bases help must explain the total row count")
        XCTAssertTrue(doc.contains("`shown_count`"), "Bases help must explain the shown row count")

        let migrationStart = try XCTUnwrap(doc.range(of: "## Dataview Migration"))
        let migrationEnd = try XCTUnwrap(
            doc.range(of: "## CLI Querying", range: migrationStart.upperBound..<doc.endIndex))
        let migration = String(doc[migrationStart.lowerBound..<migrationEnd.lowerBound])
        for heading in [
            "### DQL sources and commands",
            "### DQL file fields",
            "### DQL task fields",
            "### DQL functions",
        ] {
            XCTAssertTrue(migration.contains(heading), "Bases help is missing \(heading)")
        }

        let requiredMappings = [
            "outgoing([[note]])", "[[#]]", "file.inFolder", "file.hasTag", "linksTo",
            "file.name", "file.path", "file.folder", "file.ext", "file.size", "file.ctime",
            "file.mtime", "file.tags", "file.aliases", "file.cday", "file.mday", "file.link",
            "file.inlinks", "file.outlinks", "file.etags", "file.lists", "file.frontmatter",
            "file.day", "file.starred", "task.text", "task.status", "completed", "checked",
            "task.due", "task.scheduled", "created", "completion", "fullyCompleted", "children",
            "section", "contains", "lower", "replace", "join", "length", "sort", "reverse",
            "unique", "flat", "slice", "filter", "map", "sum", "average", "min", "max",
            "startswith", "endswith", "round", "trunc", "floor", "ceil", "regextest",
            "regexmatch", "regexreplace", "split", "substring", "striptime", "choice",
            "default", "typeof", "number", "string", "date", "dur", "link", "object",
            "list", "embed", "upper", "truncate", "padleft", "padright", "containsword",
            "econtains", "icontains", "dateformat", "durationformat", "currencyformat",
            "localtime", "hash", "meta", "minby", "maxby", "product", "reduce", "extract",
            "firstvalue", "nonnull", "display", "elink",
        ]
        for mapping in requiredMappings {
            XCTAssertTrue(
                migration.contains(mapping),
                "Dataview migration help is missing the pinned mapping for \(mapping)")
        }
    }
}
