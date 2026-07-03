// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import SwiftUI
import XCTest

@testable import SlateMac

/// Proves the `PresentationReady` harness (#452) itself works — including that
/// each check actually has teeth (negative controls), so U1–U5 can trust it.
/// Token *correctness* lives in `DesignTokensTests` (which drives the same
/// harness); this file exercises the harness's own behaviour.
@MainActor
final class PresentationReadyTests: XCTestCase {

    /// A representative "surface": primary + secondary text on the surface
    /// token, styled entirely through the token layer (typography roles are
    /// Dynamic-Type text styles, so this scales).
    private var sampleSurface: some View {
        VStack(alignment: .leading, spacing: Tokens.Spacing.sm) {
            Text("Primary label")
                .font(Tokens.Typography.body)
                .foregroundStyle(Tokens.ColorRole.textPrimary)
            Text("A secondary caption long enough to wrap onto multiple lines at large text sizes.")
                .font(Tokens.Typography.caption)
                .foregroundStyle(Tokens.ColorRole.textSecondary)
        }
        .padding(Tokens.Spacing.md)
        .background(Tokens.ColorRole.surface)
    }

    /// §E render smoke: a real token-styled surface renders with a finite,
    /// non-empty size in both appearances (catches per-appearance crashes /
    /// failed renders).
    func testHarnessRendersSampleSurfaceInBothAppearances() {
        PresentationReady.assertRendersInBothAppearances(sampleSurface)
    }

    // MARK: - Negative control (proves the contrast check flags a bad input)

    /// The contrast check must flag a pairing below the APCA floor.
    func testContrastFloorCatchesLowContrast() {
        let lowContrast: [(name: String, text: NSColor, surface: NSColor)] = [
            (
                "mid-gray on white",
                NSColor(srgbRed: 0.6, green: 0.6, blue: 0.6, alpha: 1),
                NSColor(srgbRed: 1, green: 1, blue: 1, alpha: 1)
            )
        ]
        XCTExpectFailure("Mid-gray on white is below the APCA floor and must be flagged.") {
            PresentationReady.assertContrastFloor(lowContrast)
        }
    }
}
