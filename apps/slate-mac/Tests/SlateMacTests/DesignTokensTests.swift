// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import SwiftUI
import XCTest

@testable import SlateMac

/// Guards the design-token layer (#451):
///  - every text-on-surface and control pairing clears the project's APCA floor
///    (`|Lc| > 75`) in BOTH Aqua and DarkAqua,
///  - the dynamic color roles actually resolve to different values per
///    appearance (light/dark correctness — no appearance leak), and
///  - the spacing scale is a sane, strictly-increasing ramp.
final class DesignTokensTests: XCTestCase {

    private static let appearances: [NSAppearance.Name] = [.aqua, .darkAqua]

    /// The core guarantee: each pairing is measured independently per appearance
    /// so a failure names the pairing, appearance, resolved sRGB, and Lc.
    func testEveryPairingClearsAPCAFloorInBothAppearances() {
        for pairing in Tokens.contrastPairings {
            for name in Self.appearances {
                guard let appearance = NSAppearance(named: name) else {
                    XCTFail("Appearance \(name.rawValue) unavailable.")
                    continue
                }
                let lc = APCAContrast.lc(
                    text: pairing.text, background: pairing.surface, for: appearance)
                XCTAssertGreaterThan(
                    abs(lc), 75.0,
                    "\(pairing.name) under \(name.rawValue) must clear APCA |Lc| > 75 "
                        + "(got Lc \(String(format: "%.1f", lc)); "
                        + "text \(Self.rgb(pairing.text, name)), bg \(Self.rgb(pairing.surface, name)))."
                )
            }
        }
    }

    /// Each dynamic role must resolve to a DIFFERENT sRGB value in light vs dark
    /// — proves the light/dark pair is wired and nothing leaks one appearance
    /// into the other.
    func testRolesResolveDistinctlyPerAppearance() {
        let roles: [(String, NSColor)] = [
            ("surface", .tokenSurface),
            ("surfaceSecondary", .tokenSurfaceSecondary),
            ("textPrimary", .tokenTextPrimary),
            ("textSecondary", .tokenTextSecondary),
            ("accentFill", .tokenAccentFill),
            ("accentText", .tokenAccentText),
            ("destructiveFill", .tokenDestructiveFill),
            ("destructiveText", .tokenDestructiveText),
            ("selection", .tokenSelection),
            ("separator", .tokenSeparator),
        ]
        for (name, color) in roles {
            let light = Self.rgb(color, .aqua)
            let dark = Self.rgb(color, .darkAqua)
            XCTAssertNotEqual(
                light, dark,
                "\(name) resolves identically in light and dark (\(light)) — the "
                    + "dynamic pair isn't wired, or an appearance is leaking."
            )
        }
    }

    func testSpacingScaleIsStrictlyIncreasing() {
        let scale = [
            Tokens.Spacing.xxs, Tokens.Spacing.xs, Tokens.Spacing.sm, Tokens.Spacing.md,
            Tokens.Spacing.lg, Tokens.Spacing.xl, Tokens.Spacing.xxl,
        ]
        XCTAssertEqual(scale, scale.sorted(), "Spacing scale must be ascending.")
        XCTAssertEqual(Set(scale).count, scale.count, "Spacing steps must be distinct.")
        XCTAssertGreaterThan(scale.first!, 0, "Spacing must be positive.")
    }

    // MARK: - Helpers

    /// Resolve a (possibly dynamic) color to an sRGB description under `name`.
    private static func rgb(_ color: NSColor, _ name: NSAppearance.Name) -> String {
        guard let appearance = NSAppearance(named: name) else { return "?" }
        var out = "?"
        appearance.performAsCurrentDrawingAppearance {
            let c = color.usingColorSpace(.sRGB) ?? color
            out = String(
                format: "#%02X%02X%02X",
                Int((c.redComponent * 255).rounded()),
                Int((c.greenComponent * 255).rounded()),
                Int((c.blueComponent * 255).rounded()))
        }
        return out
    }
}
