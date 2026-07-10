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

    private static var projectRoot: URL {
        URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()  // SlateMacTests
            .deletingLastPathComponent()  // Tests
            .deletingLastPathComponent()  // slate-mac
            .deletingLastPathComponent()  // apps
            .deletingLastPathComponent()  // repo root
    }

    private func baseQueryBuilderSource() throws -> String {
        try String(
            contentsOf: Self.projectRoot
                .appendingPathComponent(
                    "apps/slate-mac/Sources/SlateMac/Bases/BaseQueryBuilderSheet.swift"),
            encoding: .utf8)
    }

    /// Resolve the same stack SwiftUI draws for a builder row: a translucent
    /// selection/separator wash over the sheet's control background. Returning
    /// a dynamic opaque color lets `PresentationReady` measure the real carrier
    /// independently in Aqua and Dark Aqua.
    private func builderRowBackground(selected: Bool) -> NSColor {
        let overlay: NSColor = selected ? .selectedContentBackgroundColor : .separatorColor
        let opacity: CGFloat = selected ? 0.18 : 0.12
        return NSColor(name: nil) { appearance in
            var resolvedOverlay = overlay
            var resolvedBase = NSColor.controlBackgroundColor
            appearance.performAsCurrentDrawingAppearance {
                resolvedOverlay = overlay.usingColorSpace(.sRGB) ?? overlay
                resolvedBase =
                    NSColor.controlBackgroundColor.usingColorSpace(.sRGB)
                    ?? .controlBackgroundColor
            }
            let alpha = resolvedOverlay.alphaComponent * opacity
            return NSColor(
                srgbRed:
                    resolvedOverlay.redComponent * alpha
                    + resolvedBase.redComponent * (1 - alpha),
                green:
                    resolvedOverlay.greenComponent * alpha
                    + resolvedBase.greenComponent * (1 - alpha),
                blue:
                    resolvedOverlay.blueComponent * alpha
                    + resolvedBase.blueComponent * (1 - alpha),
                alpha: 1)
        }
    }

    func testEveryPairingClearsAPCAFloorInBothAppearances() {
        PresentationReady.assertContrastFloor(Tokens.contrastPairings)
    }

    func testBuilderAdvancedValidationClearsActualSelectedAndUnselectedRowBackgrounds()
        throws
    {
        let source = try baseQueryBuilderSource()
        XCTAssertTrue(
            source.contains(".foregroundStyle(Tokens.ColorRole.destructiveText)"),
            "advanced validation must use the APCA-gated destructive text role")
        XCTAssertFalse(
            source.contains(".foregroundStyle(Color(nsColor: .systemRed))"),
            "raw system red fails against the actual selected and unselected row washes")

        PresentationReady.assertContrastFloor([
            (
                "builder advanced validation on selected row",
                .tokenDestructiveText,
                builderRowBackground(selected: true)
            ),
            (
                "builder advanced validation on unselected row",
                .tokenDestructiveText,
                builderRowBackground(selected: false)
            ),
        ])
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
