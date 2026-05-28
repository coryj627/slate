// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// Direct unit tests for `HotkeySpoken` (#332) — the chord-glyph →
/// VoiceOver-pronounceable-string helper extracted out of
/// `CommandPaletteView`.
///
/// `CommandPaletteViewTests.testVoiceOverLabelComposesChordIntoSpokenString`
/// still exercises the full `voiceOverLabel(for:)` path (label +
/// comma + spoken chord). These tests pin the extracted helper in
/// isolation so a regression in the glyph/punctuation tables fails
/// here with a precise message rather than only surfacing through
/// the composed label.
final class HotkeySpokenTests: XCTestCase {

    // MARK: - Modifier glyphs

    func testSingleModifierPlusLetter() {
        XCTAssertEqual(HotkeySpoken.spoken(for: "⌘S"), "Command S")
    }

    func testMultipleModifiersPreserveGlyphOrder() {
        // The spoken order follows the glyph order in the input —
        // the helper does NOT re-sort to a canonical modifier
        // order. `⇧⌘N` (as the menu bar renders it) → "Shift
        // Command N".
        XCTAssertEqual(HotkeySpoken.spoken(for: "⇧⌘N"), "Shift Command N")
    }

    func testAllFourModifiers() {
        XCTAssertEqual(
            HotkeySpoken.spoken(for: "⌃⌥⇧⌘A"),
            "Control Option Shift Command A"
        )
    }

    // MARK: - Punctuation keys

    func testTrailingCommaSpelledOut() {
        // The case that motivated spelling punctuation out (#320):
        // ⌘, would be elided to just "Command" at VoiceOver
        // punctuation = None without this.
        XCTAssertEqual(HotkeySpoken.spoken(for: "⌘,"), "Command Comma")
    }

    func testEveryPunctuationKeyInTableSpeaks() {
        // Lock the full punctuation table so a deletion or typo in
        // `keyWord` fails loudly. Pairs each key with ⌘ so the
        // output shape matches real chords.
        let expectations: [(glyph: String, spoken: String)] = [
            ("⌘,", "Command Comma"),
            ("⌘.", "Command Period"),
            ("⌘/", "Command Slash"),
            ("⌘\\", "Command Backslash"),
            ("⌘;", "Command Semicolon"),
            ("⌘'", "Command Quote"),
            ("⌘[", "Command Left Bracket"),
            ("⌘]", "Command Right Bracket"),
            ("⌘-", "Command Minus"),
            ("⌘=", "Command Equals"),
            ("⌘`", "Command Backtick"),
            ("⌘ ", "Command Space"),
        ]
        for (glyph, spoken) in expectations {
            XCTAssertEqual(
                HotkeySpoken.spoken(for: glyph),
                spoken,
                "punctuation glyph \(glyph) drifted"
            )
        }
    }

    // MARK: - Pass-through + edge cases

    func testUnknownGlyphPassesThroughUnchanged() {
        // A glyph in neither table (here a letter with no special
        // handling) passes through as its own string.
        XCTAssertEqual(HotkeySpoken.spoken(for: "Z"), "Z")
    }

    func testEmptyHintProducesEmptyString() {
        // The caller owns the empty-hint guard (voiceOverLabel
        // returns the bare label); the helper just produces "".
        XCTAssertEqual(HotkeySpoken.spoken(for: ""), "")
    }

    func testBareModifierGlyphWithNoKey() {
        // Defensive: a malformed hint that's only a modifier still
        // produces its spoken word rather than crashing.
        XCTAssertEqual(HotkeySpoken.spoken(for: "⌘"), "Command")
    }

    func testDigitKeyPassesThroughUnchanged() {
        // Digits aren't in either table, so they pass through as
        // their own string. Pins this against a future "helpfully
        // spell out the number" change (Codoki review on #347).
        XCTAssertEqual(HotkeySpoken.spoken(for: "⌘1"), "Command 1")
    }

    func testLowercaseLetterPassesThroughUnchanged() {
        // The helper does NOT uppercase — that's the scraper's job
        // (`extractChords` uppercases before composing the glyph).
        // Registry hotkeyHints are already glyph-cased (`⌘S`), so
        // the helper sees an uppercase key. Pin the no-op so a
        // future "helpfully uppercase here too" change is a
        // conscious one.
        XCTAssertEqual(HotkeySpoken.spoken(for: "⌘s"), "Command s")
    }

    // MARK: - Parity with the old inlined behaviour

    /// Before #332 the walk lived in `CommandPaletteView` with
    /// private `chordGlyphWord` / `chordKeyWord` dicts. This pins
    /// that `voiceOverLabel(for:)` still produces byte-identical
    /// output after delegating to `HotkeySpoken` — the extraction
    /// must be behaviour-preserving.
    func testVoiceOverLabelStillComposesViaHelper() {
        let cmd = Command(
            id: "test.settings",
            label: "Settings…",
            accessibilityHint: nil,
            hotkeyHint: "⌘,",
            section: .editor
        )
        XCTAssertEqual(
            CommandPaletteView.voiceOverLabel(for: cmd),
            "Settings…, Command Comma"
        )
    }
}
