// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit

/// Foreground colors per `SyntaxKind` for editor markdown highlighting (#296).
///
/// Default palette uses custom per-appearance sRGB colours tuned
/// for APCA `|Lc| > 75` against `NSColor.textBackgroundColor` in
/// both Aqua and DarkAqua. Replaces the original
/// `NSColor.system*` mapping (closes #308) — Apple's system
/// colours fell short of Lc 75 in several mode combinations and
/// their resolved sRGB values shift across macOS versions (we saw
/// `systemTeal` swing by ~0.4 between CI macOS 14 and local
/// macOS 15), so following Apple's semantics didn't even buy us
/// stable cross-OS appearance. Pinned sRGB pairs are deterministic
/// and the test suite enforces the Lc 75 floor.
///
/// **Contrast under default prefs:** every kind's resolved colour
/// clears APCA Lc 75 against `textBackgroundColor` in both
/// appearances (measured by `EditorSyntaxPaletteTests`). Under
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
/// - `heading`, `setextUnderline`: deep blue (light) / pale blue
///   (dark) — the default accent in Apple's HIG; reads as "primary
///   structure marker."
/// - `codeBlock`, `inlineCode`: deep purple (light) / pale purple
///   (dark) — matches what code highlighters typically use for the
///   code surface tint.
/// - `wikilink`: dark teal (light) / pale cyan (dark) — distinct
///   from code purple, distinct from heading blue.
/// - `tag`: deep magenta (light) / peach pink (dark) — distinct
///   from everything above; matches what Obsidian / other markdown
///   editors converge on for tags.
/// - `emphasisMarker`: `tertiaryLabelColor` — the markers (`**`,
///   `_`) are visual noise; de-emphasizing them lets the prose
///   they wrap stay readable while still signalling "this is
///   formatting syntax." The text BETWEEN the markers is not
///   spanned — it stays in body colour.
enum EditorSyntaxPalette {

    // MARK: - Per-kind dynamic colours
    //
    // Each pair is hand-tuned to clear APCA |Lc| > 75 against
    // textBackgroundColor in the matching appearance. Hue families
    // preserved from the original system-colour palette (#296) so
    // the visual identity of each kind stays familiar — only
    // lightness/saturation moved. Test:
    // `testDefaultPaletteMeetsAPCAAgainstTextBackground`.

    /// Heading / setext-underline. Hue family: blue.
    static let headingColor = dynamicColor(
        light: NSColor(srgbRed: 0.00, green: 0.30, blue: 0.75, alpha: 1.0),
        dark: NSColor(srgbRed: 0.55, green: 0.88, blue: 1.00, alpha: 1.0)
    )

    /// Code block / inline code. Hue family: purple.
    static let codeColor = dynamicColor(
        light: NSColor(srgbRed: 0.55, green: 0.15, blue: 0.65, alpha: 1.0),
        dark: NSColor(srgbRed: 0.92, green: 0.82, blue: 1.00, alpha: 1.0)
    )

    /// Wikilink. Hue family: teal.
    static let wikilinkColor = dynamicColor(
        light: NSColor(srgbRed: 0.00, green: 0.42, blue: 0.42, alpha: 1.0),
        dark: NSColor(srgbRed: 0.45, green: 0.95, blue: 0.95, alpha: 1.0)
    )

    /// Tag. Hue family: pink/magenta.
    static let tagColor = dynamicColor(
        light: NSColor(srgbRed: 0.78, green: 0.05, blue: 0.30, alpha: 1.0),
        dark: NSColor(srgbRed: 1.00, green: 0.75, blue: 0.80, alpha: 1.0)
    )

    /// Build an appearance-aware NSColor from a (light, dark) sRGB
    /// pair. HC appearances inherit their non-HC sibling's colour
    /// (the IC branch in `color(for:increaseContrast:)` already
    /// handles the `accessibilityDisplayShouldIncreaseContrast`
    /// case by collapsing to `labelColor`).
    private static func dynamicColor(light: NSColor, dark: NSColor) -> NSColor {
        NSColor(name: nil) { appearance in
            appearance.bestMatch(from: [.aqua, .darkAqua]) == .darkAqua
                ? dark
                : light
        }
    }

    // MARK: - Public mapping

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
            return headingColor
        case .codeBlock, .inlineCode:
            return codeColor
        case .wikilink:
            return wikilinkColor
        case .tag:
            return tagColor
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
