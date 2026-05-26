// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit

/// Foreground colors per `SyntaxKind` for editor markdown highlighting (#296).
///
/// Default palette uses Apple's `NSColor.system*` semantic colors —
/// these resolve dynamically per appearance, so dark mode picks up
/// the brighter variant and light mode the darker one without us
/// having to maintain two tables.
///
/// **Contrast under default prefs:** Apple tunes the system colors
/// against the standard label/background pairing, but a few land in
/// the 3.0–4.5:1 range against `NSColor.textBackgroundColor` in
/// specific modes (the same risk audit #230 + #252 documented for
/// the embed underline and code-block tokens). Under
/// `accessibilityDisplayShouldIncreaseContrast` we collapse the
/// whole palette to `NSColor.labelColor` (Apple-guaranteed contrast
/// against the matched background) — colour stops being the cue,
/// but the underline / position / glyph already carry the structure
/// (WCAG 1.4.1, Use of Color: shape/position carry the structure,
/// not colour alone). Same pattern code-block tokens use in
/// `CodeTokenTheme`.
///
/// **Why these specific kinds get these colours:**
///
/// - `frontmatter`, `commentBlock`, `citation`: `secondaryLabelColor`
///   — Apple's tuned "de-emphasized text" colour. Meets contrast
///   against textBackground while signalling "this is meta, not
///   prose."
/// - `heading`, `setextUnderline`: `systemBlue` — the default accent
///   in Apple's HIG; reads as "primary structure marker."
/// - `codeBlock`, `inlineCode`: `systemPurple` — matches what code
///   highlighters typically use for the code surface tint.
/// - `wikilink`: `systemTeal` — distinct from code purple, distinct
///   from heading blue.
/// - `tag`: `systemPink` — distinct from everything above; matches
///   what Obsidian / other markdown editors converge on for tags.
/// - `emphasisMarker`: `tertiaryLabelColor` — the markers (`**`,
///   `_`) are visual noise; de-emphasizing them lets the prose
///   they wrap stay readable while still signalling "this is
///   formatting syntax." The text BETWEEN the markers is not
///   spanned — it stays in body colour.
enum EditorSyntaxPalette {

    /// Pure helper — takes the toggle directly so a unit test can
    /// drive both branches without mocking `NSWorkspace`.
    static func color(for kind: SyntaxKind, increaseContrast: Bool) -> NSColor {
        if increaseContrast {
            // Single high-contrast colour across all kinds. Tokens
            // are still semantically tagged (via the attribute
            // layer), just not colour-coded — which is correct a11y
            // behaviour: shape / position carry the structure, not
            // colour alone (WCAG 1.4.1).
            return NSColor.labelColor
        }
        switch kind {
        case .frontmatter, .commentBlock, .citation:
            return NSColor.secondaryLabelColor
        case .heading, .setextUnderline:
            return NSColor.systemBlue
        case .codeBlock, .inlineCode:
            return NSColor.systemPurple
        case .wikilink:
            return NSColor.systemTeal
        case .tag:
            return NSColor.systemPink
        case .emphasisMarker:
            return NSColor.tertiaryLabelColor
        }
    }

    /// Instance form for the running editor — reads
    /// `NSWorkspace.shared.accessibilityDisplayShouldIncreaseContrast`
    /// directly so the coordinator doesn't have to thread the flag
    /// through every call site.
    static func color(for kind: SyntaxKind) -> NSColor {
        color(
            for: kind,
            increaseContrast: NSWorkspace.shared
                .accessibilityDisplayShouldIncreaseContrast
        )
    }
}
