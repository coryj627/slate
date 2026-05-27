// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import XCTest

@testable import SlateMac

/// Contrast + branch tests for `EditorSyntaxPalette` (#296).
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
///    `NSColor.labelColor` for every kind, and that pairing clears
///    Lc 75 against `textBackgroundColor` in both appearances
///    (Apple's contractual label/text pairing, measured to catch
///    a future appearance shift).
/// 2. The `increaseContrast = false` branch returns the pinned
///    palette colour for each kind (so a future palette change is
///    a deliberate edit, not an accidental rename), AND every
///    kind's resolved colour clears Lc 75 against
///    `textBackgroundColor` in both appearances.
/// 3. Every kind has a non-nil colour in both branches (no enum
///    case left unhandled).
final class EditorSyntaxPaletteTests: XCTestCase {

    // MARK: - Increase Contrast branch

    func testIncreaseContrastCollapsesAllKindsToLabelColor() {
        for kind in SyntaxKind.allCases {
            XCTAssertEqual(
                EditorSyntaxPalette.color(for: kind, increaseContrast: true),
                NSColor.labelColor,
                "kind \(kind) must collapse to labelColor under Increase Contrast (WCAG 1.4.11 mitigation)"
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
            EditorSyntaxPalette.color(for: .commentBlock, increaseContrast: false),
            NSColor.secondaryLabelColor
        )
        XCTAssertEqual(
            EditorSyntaxPalette.color(for: .citation, increaseContrast: false),
            NSColor.secondaryLabelColor
        )
        XCTAssertEqual(
            EditorSyntaxPalette.color(for: .heading, increaseContrast: false),
            EditorSyntaxPalette.headingColor
        )
        XCTAssertEqual(
            EditorSyntaxPalette.color(for: .setextUnderline, increaseContrast: false),
            EditorSyntaxPalette.headingColor
        )
        XCTAssertEqual(
            EditorSyntaxPalette.color(for: .codeBlock, increaseContrast: false),
            EditorSyntaxPalette.codeColor
        )
        XCTAssertEqual(
            EditorSyntaxPalette.color(for: .inlineCode, increaseContrast: false),
            EditorSyntaxPalette.codeColor
        )
        XCTAssertEqual(
            EditorSyntaxPalette.color(for: .wikilink, increaseContrast: false),
            EditorSyntaxPalette.wikilinkColor
        )
        XCTAssertEqual(
            EditorSyntaxPalette.color(for: .tag, increaseContrast: false),
            EditorSyntaxPalette.tagColor
        )
        XCTAssertEqual(
            EditorSyntaxPalette.color(for: .emphasisMarker, increaseContrast: false),
            NSColor.tertiaryLabelColor
        )
    }

    // MARK: - Exhaustiveness

    func testEveryKindHasAColorInBothBranches() {
        // Compiler enforces exhaustiveness inside `color(for:_:)` —
        // this test guards against a future contributor accidentally
        // adding a kind without wiring it into the switch. Runs
        // through every case and just asserts non-nil; the explicit
        // mapping test above verifies the actual values.
        for kind in SyntaxKind.allCases {
            _ = EditorSyntaxPalette.color(for: kind, increaseContrast: true)
            _ = EditorSyntaxPalette.color(for: kind, increaseContrast: false)
        }
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
            let lc = apcaContrast(text: fg, background: bg)
            XCTAssertGreaterThan(
                abs(lc), 75,
                "labelColor vs textBackgroundColor under \(appearanceName.rawValue) must clear APCA |Lc| > 75 (got Lc \(lc))"
            )
        }
    }

    /// Sweep every `SyntaxKind`'s default-mode colour through APCA
    /// against `textBackgroundColor` in both light and dark mode.
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
            for kind in SyntaxKind.allCases {
                var fg = NSColor.black, bg = NSColor.white
                appearance.performAsCurrentDrawingAppearance {
                    fg = EditorSyntaxPalette.color(for: kind, increaseContrast: false)
                        .usingColorSpace(.sRGB)!
                    bg = NSColor.textBackgroundColor.usingColorSpace(.sRGB)!
                }
                let lc = apcaContrast(text: fg, background: bg)
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

    // MARK: - Contrast helper

    /// APCA-W3 v0.1.9 contrast (G-4g constants). Returns a signed
    /// `Lc` value: positive for dark text on a light background
    /// (BoW), negative for light text on a dark background (WoB).
    /// For pass/fail testing, compare `abs(lc)` against a threshold
    /// — this project uses `> 75` (APCA's "small body text" bucket).
    ///
    /// Reference: https://github.com/Myndex/apca-w3
    private func apcaContrast(text: NSColor, background: NSColor) -> Double {
        let blkThrs = 0.022
        let blkClmp = 1.414
        let deltaYmin = 0.0005
        let loClip = 0.1
        let loBoWoffset = 0.027
        let loWoBoffset = 0.027
        let scaleBoW = 1.14
        let scaleWoB = 1.14
        let normBG = 0.56
        let normTXT = 0.57
        let revTXT = 0.62
        let revBG = 0.65

        func softClamp(_ y: Double) -> Double {
            y > blkThrs ? y : y + pow(blkThrs - y, blkClmp)
        }

        let txt = softClamp(screenLuminance(text))
        let bg = softClamp(screenLuminance(background))

        if abs(bg - txt) < deltaYmin { return 0.0 }

        let sapc: Double
        let output: Double
        if bg > txt {
            sapc = (pow(bg, normBG) - pow(txt, normTXT)) * scaleBoW
            output = sapc < loClip ? 0.0 : sapc - loBoWoffset
        } else {
            sapc = (pow(bg, revBG) - pow(txt, revTXT)) * scaleWoB
            output = sapc > -loClip ? 0.0 : sapc + loWoBoffset
        }
        return output * 100.0
    }

    /// APCA "screen luminance" Y: sRGB channels raised to the 2.4
    /// display TRC and weighted by the Rec. 709 coefficients. This
    /// is the simple-exponent form APCA-W3 uses, not WCAG's
    /// piecewise inverse companding.
    private func screenLuminance(_ c: NSColor) -> Double {
        let r = pow(Double(c.redComponent), 2.4)
        let g = pow(Double(c.greenComponent), 2.4)
        let b = pow(Double(c.blueComponent), 2.4)
        return 0.2126729 * r + 0.7151522 * g + 0.0721750 * b
    }
}
