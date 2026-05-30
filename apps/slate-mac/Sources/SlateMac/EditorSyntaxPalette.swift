// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit

/// Foreground colors per `EditorSpanKind` for editor markdown
/// highlighting (#296, re-keyed onto the canonical Rust spans in #376).
///
/// `EditorSpanKind` is the FFI-generated span classifier from
/// `slate-core`'s `editor_highlightSpans` (#377/#391) — the editor no
/// longer classifies in Swift. This palette is the **apply layer**: it
/// maps each canonical kind to the foreground the coordinator stamps as
/// an `NSLayoutManager` temporary attribute.
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
/// **Contrast under default prefs:** every *coloured* kind's resolved
/// colour clears APCA Lc 75 against `textBackgroundColor` in both
/// appearances (measured by `EditorSyntaxPaletteTests`). Under
/// `accessibilityDisplayShouldIncreaseContrast` we collapse the
/// coloured kinds to `NSColor.labelColor` (Apple-guaranteed contrast
/// against the matched background) — colour stops being the cue,
/// but the underline / position / glyph already carry the structure
/// (WCAG 1.4.1, Use of Color: shape/position carry the structure,
/// not colour alone). Same pattern code-block tokens use in
/// `CodeTokenTheme`.
///
/// **Conservative defaults (#376).** This first cut preserves the
/// prior editor's visual identity rather than colouring every new
/// kind the Rust spans expose:
///
/// - `emphasis` / `strong` / `strikethrough` → `nil` (not coloured).
///   The Rust spans cover the whole run *including the prose between
///   the markers*; colouring it would dim the prose. The prior Swift
///   highlighter coloured only the markers. A marker-only pass is a
///   deliberate follow-up (#377) — until then these stay in body
///   colour.
/// - `link` / `image` / `blockQuote` → `nil`. `editor_highlightSpans`
///   never emits these (filtered backend-side); listed only so the
///   switch is exhaustive.
/// - all `code` kinds (`codeFence` / `inlineCode` / `code(token:)`)
///   → one `codeColor`. Per-token editor colouring needs an
///   APCA-validated `TokenKind` palette (follow-up); for now the code
///   surface gets one tint, matching the prior behaviour.
///
/// **Why these specific kinds get these colours:**
///
/// - `frontmatter`, `comment`, `citation`: `secondaryLabelColor`
///   — Apple's tuned "de-emphasized text" colour. Meets contrast
///   against textBackground while signalling "this is meta, not
///   prose."
/// - `heading`: deep blue (light) / pale blue (dark) — the default
///   accent in Apple's HIG; reads as "primary structure marker."
/// - `codeFence`, `inlineCode`, `code`: deep purple (light) / pale
///   purple (dark) — matches what code highlighters typically use for
///   the code surface tint.
/// - `wikilink`, `embed`: dark teal (light) / pale cyan (dark) —
///   distinct from code purple, distinct from heading blue. The embed
///   additionally carries the underline cue (audit #207, #230).
/// - `tag`: deep magenta (light) / peach pink (dark) — distinct
///   from everything above; matches what Obsidian / other markdown
///   editors converge on for tags.
enum EditorSyntaxPalette {

    // MARK: - Per-kind dynamic colours
    //
    // Each pair is hand-tuned to clear APCA |Lc| > 75 against
    // textBackgroundColor in the matching appearance. Hue families
    // preserved from the original system-colour palette (#296) so
    // the visual identity of each kind stays familiar — only
    // lightness/saturation moved. Test:
    // `testDefaultPaletteMeetsAPCAAgainstTextBackground`.

    /// Heading. Hue family: blue.
    static let headingColor = dynamicColor(
        name: "slate.editor.heading",
        light: NSColor(srgbRed: 0.00, green: 0.30, blue: 0.75, alpha: 1.0),
        dark: NSColor(srgbRed: 0.55, green: 0.88, blue: 1.00, alpha: 1.0)
    )

    /// Code block / inline code. Hue family: purple.
    static let codeColor = dynamicColor(
        name: "slate.editor.code",
        light: NSColor(srgbRed: 0.55, green: 0.15, blue: 0.65, alpha: 1.0),
        dark: NSColor(srgbRed: 0.92, green: 0.82, blue: 1.00, alpha: 1.0)
    )

    /// Wikilink. Hue family: teal.
    static let wikilinkColor = dynamicColor(
        name: "slate.editor.wikilink",
        light: NSColor(srgbRed: 0.00, green: 0.42, blue: 0.42, alpha: 1.0),
        dark: NSColor(srgbRed: 0.45, green: 0.95, blue: 0.95, alpha: 1.0)
    )

    /// Tag. Hue family: pink/magenta.
    static let tagColor = dynamicColor(
        name: "slate.editor.tag",
        light: NSColor(srgbRed: 0.78, green: 0.05, blue: 0.30, alpha: 1.0),
        dark: NSColor(srgbRed: 1.00, green: 0.75, blue: 0.80, alpha: 1.0)
    )

    /// Build an appearance-aware NSColor from a (light, dark) sRGB
    /// pair. The `name` surfaces in NSColor inspectors and `po`
    /// output — pick a stable identifier per call site so debug
    /// dumps name the colour rather than its anonymous dynamic
    /// provider.
    ///
    /// HC appearances inherit their non-HC sibling's colour (the IC
    /// branch in `color(for:increaseContrast:)` already handles the
    /// `accessibilityDisplayShouldIncreaseContrast` case by
    /// collapsing to `labelColor`).
    private static func dynamicColor(
        name: NSColor.Name,
        light: NSColor,
        dark: NSColor
    ) -> NSColor {
        NSColor(name: name) { appearance in
            appearance.bestMatch(from: [.aqua, .darkAqua]) == .darkAqua
                ? dark
                : light
        }
    }

    // MARK: - Public mapping

    /// Foreground colour for an editor span kind, or `nil` when the
    /// kind is intentionally left in the body text colour (see the
    /// "Conservative defaults" note on the type). Pure — takes the
    /// Increase Contrast toggle directly so a unit test can drive both
    /// branches without mocking `NSWorkspace`.
    static func color(for kind: EditorSpanKind, increaseContrast: Bool) -> NSColor? {
        // One exhaustive switch — adding an `EditorSpanKind` is a compile
        // error here, not a silent fall-through.
        //
        // Uncoloured kinds return `nil` regardless of Increase Contrast:
        // they're not part of the colour cue at all. `emphasis` / `strong`
        // / `strikethrough` cover the whole run (markers + prose), so
        // colouring them would dim the prose; `link` / `image` /
        // `blockQuote` are never emitted by `editorHighlightSpans`.
        //
        // Coloured kinds collapse to `labelColor` under Increase Contrast
        // — colour stops being the cue, but glyph / position still carry
        // the structure (WCAG 1.4.1).
        switch kind {
        case .emphasis, .strong, .strikethrough, .link, .image, .blockQuote:
            return nil
        case .frontmatter, .comment, .citation:
            return increaseContrast ? NSColor.labelColor : NSColor.secondaryLabelColor
        case .heading:
            return increaseContrast ? NSColor.labelColor : headingColor
        case .codeFence, .inlineCode, .code:
            return increaseContrast ? NSColor.labelColor : codeColor
        case .wikilink, .embed:
            return increaseContrast ? NSColor.labelColor : wikilinkColor
        case .tag:
            return increaseContrast ? NSColor.labelColor : tagColor
        }
    }

    /// Instance form for the running editor — reads
    /// `NSWorkspace.shared.accessibilityDisplayShouldIncreaseContrast`
    /// directly so the coordinator doesn't have to thread the flag
    /// through every call site.
    static func color(for kind: EditorSpanKind) -> NSColor? {
        color(
            for: kind,
            increaseContrast: NSWorkspace.shared
                .accessibilityDisplayShouldIncreaseContrast
        )
    }
}
