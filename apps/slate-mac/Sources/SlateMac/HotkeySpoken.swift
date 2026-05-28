// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

/// Converts a keyboard-chord glyph string (e.g. `⌘⇧N`, `⌘,`) into a
/// VoiceOver-pronounceable form (`"Shift Command N"`, `"Command
/// Comma"`).
///
/// **Why this exists.** macOS renders chords as compact glyph
/// strings — `⌘,` for Settings, `⇧⌘N` for New-from-Template. Those
/// glyphs aren't reliably spoken by VoiceOver: the modifier glyphs
/// have no default pronunciation, and a trailing punctuation key
/// like `,` is *elided entirely* when the user's VoiceOver
/// punctuation level is "None". A sighted user reads "Command
/// Comma" off the menu bar; a VoiceOver user at punctuation=None
/// would hear just "Command" with no key indicator. Spelling every
/// glyph + punctuation key out makes the chord pronounceable at
/// every VoiceOver punctuation setting.
///
/// **Single source of truth (#332).** This was originally inlined
/// in `CommandPaletteView` (the first and only consumer). Extracted
/// here so the glyph + punctuation tables stay single-source as
/// additional consumers arrive — a future keybindings settings
/// panel, plugin command help popups, or a "what does ⌘⇧X do?"
/// speakable affordance would otherwise each re-derive the same
/// walk and drift apart. Pure `static` members on a caseless enum
/// (no actor isolation, like `CommandPaletteModel.fuzzyScore`) so
/// callers can use it from any isolation context.
enum HotkeySpoken {
    /// Walk every character of `hint` in order. Modifier glyphs
    /// (⌘⇧⌥⌃) become their spoken word; punctuation keys become
    /// their spoken name; everything else (alphanumerics, and any
    /// glyph not in either table) passes through unchanged.
    ///
    /// Returns the spoken chord ONLY — callers compose it with a
    /// label themselves (e.g. `"\(label), \(HotkeySpoken.spoken(for:
    /// hint))"`). Returns an empty string for an empty `hint`, so
    /// the caller's guard against empty hints stays the caller's
    /// responsibility.
    static func spoken(for hint: String) -> String {
        var parts: [String] = []
        for char in hint {
            if let modifierWord = glyphWord[char] {
                parts.append(modifierWord)
            } else {
                parts.append(keyWord[char] ?? String(char))
            }
        }
        return parts.joined(separator: " ")
    }

    /// Modifier-key glyphs → spoken names. `private` — callers go
    /// through `spoken(for:)`; exposing the tables directly would
    /// invite a second consumer to walk them itself and drift from
    /// the canonical composition (Codoki review on #347).
    private static let glyphWord: [Character: String] = [
        "⌘": "Command",
        "⇧": "Shift",
        "⌥": "Option",
        "⌃": "Control",
    ]

    /// Punctuation keys → spoken names. VoiceOver's default
    /// punctuation level ("Some") speaks "comma" for ",", but
    /// users with "None" hear nothing — so a chord ending in
    /// punctuation like ⌘, would become "Command" with no key
    /// indicator. Spelling out the punctuation makes the chord
    /// pronounceable at every VoiceOver punctuation level.
    /// `private` for the same reason as `glyphWord` above.
    private static let keyWord: [Character: String] = [
        ",": "Comma",
        ".": "Period",
        "/": "Slash",
        "\\": "Backslash",
        ";": "Semicolon",
        "'": "Quote",
        "[": "Left Bracket",
        "]": "Right Bracket",
        "-": "Minus",
        "=": "Equals",
        "`": "Backtick",
        " ": "Space",
    ]
}
