// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import XCTest

@testable import SlateMac

/// Token *correctness* (#451), asserted through the shared `PresentationReady`
/// harness (#452) — the same single entry point U1–U5 surfaces call:
///  - every text-on-surface and control pairing clears APCA `|Lc| > 75` in both
///    appearances,
///  - the dynamic color roles resolve distinctly light vs dark, and
///  - the spacing scale is a sane, strictly-increasing ramp.
final class DesignTokensTests: XCTestCase {

    func testEveryPairingClearsAPCAFloorInBothAppearances() {
        PresentationReady.assertContrastFloor(Tokens.contrastPairings)
    }

    func testRolesResolveDistinctlyPerAppearance() {
        PresentationReady.assertResolvesDistinctlyPerAppearance([
            ("surface", .tokenSurface),
            ("surfaceSecondary", .tokenSurfaceSecondary),
            ("textPrimary", .tokenTextPrimary),
            ("textSecondary", .tokenTextSecondary),
            ("accentFill", .tokenAccentFill),
            ("accentText", .tokenAccentText),
            ("destructiveFill", .tokenDestructiveFill),
            ("destructiveText", .tokenDestructiveText),
            ("warningText", .tokenWarningText),
            ("selection", .tokenSelection),
            ("onSelection", .tokenOnSelection),
            ("separator", .tokenSeparator),
        ])
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
}
