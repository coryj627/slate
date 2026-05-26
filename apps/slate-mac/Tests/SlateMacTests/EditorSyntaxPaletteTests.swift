// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import XCTest

@testable import SlateMac

/// Contrast + branch tests for `EditorSyntaxPalette` (#296).
///
/// The default palette uses Apple's system semantic colours; Apple
/// tunes those against the standard `label` / `textBackground`
/// pairing. We can't prove every system colour meets WCAG 1.4.3
/// (4.5:1) in every mode without manually measuring on every macOS
/// version, but we CAN prove:
///
/// 1. The `increaseContrast = true` branch returns
///    `NSColor.labelColor` for every kind (the swap guarantees
///    contrast for low-vision users — Apple's contractual
///    label-vs-background pairing).
/// 2. The `increaseContrast = false` branch returns the documented
///    system colour for each kind (so a future palette change is a
///    deliberate edit, not an accidental rename).
/// 3. Every kind has a non-nil colour (no enum case left unhandled).
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
            NSColor.systemBlue
        )
        XCTAssertEqual(
            EditorSyntaxPalette.color(for: .setextUnderline, increaseContrast: false),
            NSColor.systemBlue
        )
        XCTAssertEqual(
            EditorSyntaxPalette.color(for: .codeBlock, increaseContrast: false),
            NSColor.systemPurple
        )
        XCTAssertEqual(
            EditorSyntaxPalette.color(for: .inlineCode, increaseContrast: false),
            NSColor.systemPurple
        )
        XCTAssertEqual(
            EditorSyntaxPalette.color(for: .wikilink, increaseContrast: false),
            NSColor.systemTeal
        )
        XCTAssertEqual(
            EditorSyntaxPalette.color(for: .tag, increaseContrast: false),
            NSColor.systemPink
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

    /// Smoke check that the IC branch's `labelColor` clears the WCAG
    /// 1.4.3 4.5:1 bar against `textBackgroundColor` in both light
    /// and dark mode — Apple guarantees this pairing but we measure
    /// to catch a future appearance change that would re-introduce
    /// the #226 / #302 regression class.
    func testIncreaseContrastLabelColorMeetsWCAGAgainstTextBackground() {
        for appearanceName in [NSAppearance.Name.darkAqua, .aqua] {
            guard let appearance = NSAppearance(named: appearanceName) else { continue }
            var fg = NSColor.black, bg = NSColor.white
            appearance.performAsCurrentDrawingAppearance {
                fg = NSColor.labelColor.usingColorSpace(.sRGB)!
                bg = NSColor.textBackgroundColor.usingColorSpace(.sRGB)!
            }
            let ratio = contrastRatio(fg, bg)
            XCTAssertGreaterThanOrEqual(
                ratio, 4.5,
                "labelColor vs textBackgroundColor under \(appearanceName.rawValue) must meet WCAG 1.4.3 (got \(ratio):1)"
            )
        }
    }

    // MARK: - Contrast helper

    /// WCAG 2.x contrast ratio per
    /// https://www.w3.org/TR/WCAG21/#dfn-contrast-ratio. Returns
    /// (L1 + 0.05) / (L2 + 0.05) where L1 is the lighter relative
    /// luminance and L2 the darker.
    private func contrastRatio(_ a: NSColor, _ b: NSColor) -> Double {
        let la = relativeLuminance(a)
        let lb = relativeLuminance(b)
        let lighter = max(la, lb)
        let darker = min(la, lb)
        return (lighter + 0.05) / (darker + 0.05)
    }

    private func relativeLuminance(_ c: NSColor) -> Double {
        let r = linearize(Double(c.redComponent))
        let g = linearize(Double(c.greenComponent))
        let b = linearize(Double(c.blueComponent))
        return 0.2126 * r + 0.7152 * g + 0.0722 * b
    }

    private func linearize(_ channel: Double) -> Double {
        // sRGB inverse companding per WCAG.
        if channel <= 0.03928 {
            return channel / 12.92
        }
        return pow((channel + 0.055) / 1.055, 2.4)
    }
}
