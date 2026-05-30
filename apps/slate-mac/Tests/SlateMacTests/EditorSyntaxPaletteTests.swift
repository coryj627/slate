// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import XCTest

@testable import SlateMac

/// Contrast + branch tests for `EditorSyntaxPalette` (#296, re-keyed
/// onto the canonical `EditorSpanKind` spans in #376).
///
/// Contrast is measured with APCA (APCA-W3 v0.1.9, G-4g constants)
/// rather than WCAG 2.x's relative-luminance ratio. APCA models
/// perceived lightness contrast at the actual sRGB transfer
/// function, so it tracks readability on modern displays better
/// than the 4.5:1 ratio — the project's threshold is `|Lc| > 75`
/// (APCA's "small body text" bucket; comfortably above the 60
/// "medium body" and 45 "any text" floors).
///
/// The default palette pins per-appearance sRGB pairs (#308) tuned
/// to clear that bar. We verify both branches measurably and
/// structurally:
///
/// 1. The `increaseContrast = true` branch returns
///    `NSColor.labelColor` for every *coloured* kind, and that
///    pairing clears Lc 75 against `textBackgroundColor` in both
///    appearances (Apple's contractual label/text pairing, measured
///    to catch a future appearance shift).
/// 2. The `increaseContrast = false` branch returns the pinned
///    palette colour for each coloured kind (so a future palette
///    change is a deliberate edit, not an accidental rename), AND
///    every coloured kind's resolved colour clears Lc 75 against
///    `textBackgroundColor` in both appearances.
/// 3. The intentionally-uncoloured kinds (emphasis / strong /
///    strikethrough — and the never-emitted link / image /
///    blockQuote) return `nil` in BOTH branches, so they stay in
///    body colour rather than collapsing to labelColor under IC.
///
/// `EditorSpanKind` is an FFI enum with associated values and no
/// `CaseIterable`, so the kind lists are spelled out explicitly.
final class EditorSyntaxPaletteTests: XCTestCase {

    /// Every kind the editor paints a colour for. Heading carries a
    /// level and code carries a token; representative payloads stand in
    /// (all heading levels map to `headingColor`, all code tokens to
    /// `codeColor`).
    private static let colouredKinds: [EditorSpanKind] = [
        .frontmatter, .comment, .citation,
        .heading(level: 1),
        .codeFence, .inlineCode, .code(token: .keyword),
        .wikilink, .embed, .tag,
    ]

    /// Kinds intentionally left in body colour (see `EditorSyntaxPalette`
    /// "Conservative defaults"). `nil` in both branches.
    private static let uncolouredKinds: [EditorSpanKind] = [
        .emphasis, .strong, .strikethrough, .link, .image, .blockQuote,
    ]

    // MARK: - Increase Contrast branch

    func testIncreaseContrastCollapsesColouredKindsToLabelColor() {
        for kind in Self.colouredKinds {
            XCTAssertEqual(
                EditorSyntaxPalette.color(for: kind, increaseContrast: true),
                NSColor.labelColor,
                "kind \(kind) must collapse to labelColor under Increase Contrast (WCAG 1.4.11 mitigation)"
            )
        }
    }

    /// The uncoloured kinds must return `nil` even under Increase
    /// Contrast — they're not part of the colour cue at all (run-level
    /// emphasis would dim the prose it wraps; link / image / blockQuote
    /// are never emitted by the backend). Returning nil keeps them in
    /// body colour in both branches.
    func testUncolouredKindsReturnNilInBothBranches() {
        for kind in Self.uncolouredKinds {
            XCTAssertNil(
                EditorSyntaxPalette.color(for: kind, increaseContrast: false),
                "kind \(kind) must stay uncoloured (default)"
            )
            XCTAssertNil(
                EditorSyntaxPalette.color(for: kind, increaseContrast: true),
                "kind \(kind) must stay uncoloured (Increase Contrast)"
            )
        }
    }

    // MARK: - Default palette mapping

    func testDefaultPaletteMapping() {
        XCTAssertEqual(
            EditorSyntaxPalette.color(for: .frontmatter, increaseContrast: false),
            NSColor.secondaryLabelColor
        )
        XCTAssertEqual(
            EditorSyntaxPalette.color(for: .comment, increaseContrast: false),
            NSColor.secondaryLabelColor
        )
        XCTAssertEqual(
            EditorSyntaxPalette.color(for: .citation, increaseContrast: false),
            NSColor.secondaryLabelColor
        )
        XCTAssertEqual(
            EditorSyntaxPalette.color(for: .heading(level: 1), increaseContrast: false),
            EditorSyntaxPalette.headingColor
        )
        XCTAssertEqual(
            EditorSyntaxPalette.color(for: .heading(level: 6), increaseContrast: false),
            EditorSyntaxPalette.headingColor,
            "every heading level shares one colour"
        )
        XCTAssertEqual(
            EditorSyntaxPalette.color(for: .codeFence, increaseContrast: false),
            EditorSyntaxPalette.codeColor
        )
        XCTAssertEqual(
            EditorSyntaxPalette.color(for: .inlineCode, increaseContrast: false),
            EditorSyntaxPalette.codeColor
        )
        XCTAssertEqual(
            EditorSyntaxPalette.color(for: .code(token: .keyword), increaseContrast: false),
            EditorSyntaxPalette.codeColor,
            "code-internal tokens share the one code surface tint (#376 conservative default)"
        )
        XCTAssertEqual(
            EditorSyntaxPalette.color(for: .wikilink, increaseContrast: false),
            EditorSyntaxPalette.wikilinkColor
        )
        XCTAssertEqual(
            EditorSyntaxPalette.color(for: .embed, increaseContrast: false),
            EditorSyntaxPalette.wikilinkColor,
            "embed shares the wikilink colour; its extra cue is the underline"
        )
        XCTAssertEqual(
            EditorSyntaxPalette.color(for: .tag, increaseContrast: false),
            EditorSyntaxPalette.tagColor
        )
    }

    // MARK: - Contrast measurement against textBackgroundColor

    /// Smoke check that the IC branch's `labelColor` clears the
    /// project's APCA `|Lc| > 75` bar against `textBackgroundColor`
    /// in both light and dark mode — Apple guarantees this pairing
    /// but we measure to catch a future appearance change that
    /// would re-introduce the #226 / #302 regression class.
    func testIncreaseContrastLabelColorMeetsAPCAAgainstTextBackground() {
        for appearanceName in [NSAppearance.Name.darkAqua, .aqua] {
            guard let appearance = NSAppearance(named: appearanceName) else { continue }
            var fg = NSColor.black, bg = NSColor.white
            appearance.performAsCurrentDrawingAppearance {
                fg = NSColor.labelColor.usingColorSpace(.sRGB)!
                bg = NSColor.textBackgroundColor.usingColorSpace(.sRGB)!
            }
            let lc = APCAContrast.lc(text: fg, background: bg)
            XCTAssertGreaterThan(
                abs(lc), 75,
                "labelColor vs textBackgroundColor under \(appearanceName.rawValue) must clear APCA |Lc| > 75 (got Lc \(lc))"
            )
        }
    }

    /// Sweep every coloured `EditorSpanKind`'s default-mode colour
    /// through APCA against `textBackgroundColor` in both appearances.
    /// The pinned palette in `EditorSyntaxPalette` (#308) is tuned
    /// to clear the project's `|Lc| > 75` gate; this test enforces
    /// the floor and surfaces any drift (palette edit, system-colour
    /// fallback, etc.) with an actionable per-pair failure.
    ///
    /// Each kind × appearance pair asserts independently so a
    /// single failure reports every offending pair in one run
    /// (kind, appearance, resolved sRGB, computed Lc).
    func testDefaultPaletteMeetsAPCAAgainstTextBackground() {
        for appearanceName in [NSAppearance.Name.darkAqua, .aqua] {
            guard let appearance = NSAppearance(named: appearanceName) else { continue }
            for kind in Self.colouredKinds {
                guard let kindColor = EditorSyntaxPalette.color(for: kind, increaseContrast: false) else {
                    XCTFail("coloured kind \(kind) unexpectedly returned nil")
                    continue
                }
                var fg = NSColor.black, bg = NSColor.white
                appearance.performAsCurrentDrawingAppearance {
                    fg = kindColor.usingColorSpace(.sRGB)!
                    bg = NSColor.textBackgroundColor.usingColorSpace(.sRGB)!
                }
                let lc = APCAContrast.lc(text: fg, background: bg)
                XCTAssertGreaterThan(
                    abs(lc), 75,
                    "\(kind) under \(appearanceName.rawValue) must clear APCA |Lc| > 75 against textBackgroundColor (got Lc \(lc); fg \(rgbDescription(fg)), bg \(rgbDescription(bg)))"
                )
            }
        }
    }

    private func rgbDescription(_ c: NSColor) -> String {
        String(format: "rgb(%.3f, %.3f, %.3f)", c.redComponent, c.greenComponent, c.blueComponent)
    }
}
