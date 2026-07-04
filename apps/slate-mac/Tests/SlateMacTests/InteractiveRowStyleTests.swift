// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI
import XCTest

@testable import SlateMac

/// The shared interactive-affordance mapping (U5-2, #475): rest / hover /
/// pressed → the expected token wash, plus a render smoke in both appearances.
/// This is the "shared style unit tests (state → expected token mapping)"
/// deliverable from u5_spec §U5-2 — the states are one implementation, so they
/// are asserted once here rather than per call site.
@MainActor
final class InteractiveRowStyleTests: XCTestCase {

    // MARK: state → wash mapping

    func testRestStateHasNoFill() {
        XCTAssertEqual(
            InteractiveRowStyle.fill(hovered: false, pressed: false),
            Color.clear,
            "Rest (not hovered, not pressed) must have no wash.")
    }

    func testHoverStateIsSurfaceSecondary() {
        XCTAssertEqual(
            InteractiveRowStyle.fill(hovered: true, pressed: false),
            Tokens.ColorRole.surfaceSecondary,
            "Hover must wash with the surfaceSecondary token (u5_spec §U5-2).")
    }

    func testPressedStateIsDeepenedAndDistinctFromHover() {
        let pressed = InteractiveRowStyle.fill(hovered: false, pressed: true)
        XCTAssertEqual(
            pressed, Tokens.ColorRole.textPrimary.opacity(0.10),
            "Pressed must be the deepened wash (a faint textPrimary veil).")
        XCTAssertNotEqual(
            pressed, Tokens.ColorRole.surfaceSecondary,
            "Pressed must read as distinct from hover, not the same wash.")
    }

    func testPressedOutranksHover() {
        // Both flags set (a press while the pointer is over the control): the
        // pressed wash wins, so a press always looks like a press.
        XCTAssertEqual(
            InteractiveRowStyle.fill(hovered: true, pressed: true),
            InteractiveRowStyle.fill(hovered: false, pressed: true),
            "Pressed must outrank hover.")
    }

    // MARK: render smoke (both appearances) — through the shared harness

    /// A token-styled button wearing the shared style renders finite + non-empty
    /// in both Aqua and DarkAqua (catches a per-appearance crash / failed render
    /// introduced by the style's background layer).
    func testStyledButtonRendersInBothAppearances() {
        let button = Button("Row") {}
            .buttonStyle(.interactiveRow())
            .frame(width: 120, height: 24)
        PresentationReady.assertRendersInBothAppearances(button)
    }
}
