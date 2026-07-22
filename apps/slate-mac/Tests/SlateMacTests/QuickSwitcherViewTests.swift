// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import SwiftUI
import XCTest

@testable import SlateMac

/// Tests for the quick switcher view (#495).
///
/// Covers the pure decision functions the keyDown monitor routes
/// through (arrow passthrough, Esc dismiss, the three Return→OpenTarget
/// chords), the APCA contrast of the row text roles, and a
/// construction smoke test. The live monitor-beats-field-editor routing
/// and `.onAppear` focus can't be reached from `swift test` (no AX
/// consumer / responder chain) — same boundary as CommandPaletteView.
final class QuickSwitcherViewTests: XCTestCase {

    // MARK: - Arrow passthrough (mirrors the palette mask)

    func testBareArrowsAreConsumed() {
        XCTAssertFalse(
            QuickSwitcherView.shouldPassThroughArrow(keyCode: 125, modifierFlags: []),
            "bare ↓ navigates")
        XCTAssertFalse(
            QuickSwitcherView.shouldPassThroughArrow(keyCode: 126, modifierFlags: []),
            "bare ↑ navigates")
    }

    func testModifiedArrowsPassThrough() {
        // Ctrl+Option+↓ is VoiceOver Quick Nav — must not be stolen.
        XCTAssertTrue(
            QuickSwitcherView.shouldPassThroughArrow(
                keyCode: 125, modifierFlags: [.control, .option]),
            "VoiceOver Quick Nav must pass through")
        XCTAssertTrue(
            QuickSwitcherView.shouldPassThroughArrow(keyCode: 125, modifierFlags: .shift),
            "Shift+↓ (selection extend) passes through")
        XCTAssertTrue(
            QuickSwitcherView.shouldPassThroughArrow(keyCode: 125, modifierFlags: .function),
            "Fn+↓ (Page Down) passes through")
    }

    func testCapsLockOnBareArrowStillConsumed() {
        XCTAssertFalse(
            QuickSwitcherView.shouldPassThroughArrow(keyCode: 126, modifierFlags: .capsLock),
            "Caps Lock isn't in the mask; bare ↑ still navigates")
    }

    func testNonArrowKeysPassThrough() {
        XCTAssertTrue(
            QuickSwitcherView.shouldPassThroughArrow(keyCode: 0, modifierFlags: []),
            "non-arrow keys pass through")
    }

    // MARK: - Escape dismiss

    func testBareEscapeDismisses() {
        XCTAssertTrue(
            QuickSwitcherView.isDismissKey(keyCode: 53, modifierFlags: []))
    }

    func testModifiedEscapeDoesNotDismiss() {
        XCTAssertFalse(
            QuickSwitcherView.isDismissKey(keyCode: 53, modifierFlags: [.command]),
            "Cmd+Esc is a system chord — not ours")
    }

    func testNonEscapeKeyIsNotDismiss() {
        XCTAssertFalse(QuickSwitcherView.isDismissKey(keyCode: 36, modifierFlags: []))
    }

    // MARK: - Return → OpenTarget chords

    func testBareReturnOpensCurrentTab() {
        XCTAssertEqual(
            QuickSwitcherView.openTarget(forReturnKeyCode: 36, modifierFlags: []),
            .currentTab)
    }

    func testKeypadEnterAlsoOpensCurrentTab() {
        // 76 = keypad Enter, accepted for parity with SearchOverlay.
        XCTAssertEqual(
            QuickSwitcherView.openTarget(forReturnKeyCode: 76, modifierFlags: []),
            .currentTab)
    }

    func testCommandReturnOpensNewTab() {
        XCTAssertEqual(
            QuickSwitcherView.openTarget(forReturnKeyCode: 36, modifierFlags: [.command]),
            .newTab)
    }

    func testCommandOptionReturnOpensNewSplitHorizontal() {
        XCTAssertEqual(
            QuickSwitcherView.openTarget(
                forReturnKeyCode: 36, modifierFlags: [.command, .option]),
            .newSplit(.horizontal))
    }

    func testUnhandledReturnChordsReturnNil() {
        // ⇧↩, ⌥↩, ⌃↩ aren't defined chords — fall through untouched
        // rather than opening with a guessed target.
        XCTAssertNil(
            QuickSwitcherView.openTarget(forReturnKeyCode: 36, modifierFlags: [.shift]))
        XCTAssertNil(
            QuickSwitcherView.openTarget(forReturnKeyCode: 36, modifierFlags: [.option]))
        XCTAssertNil(
            QuickSwitcherView.openTarget(forReturnKeyCode: 36, modifierFlags: [.control]))
    }

    func testNonReturnKeyReturnsNil() {
        XCTAssertNil(
            QuickSwitcherView.openTarget(forReturnKeyCode: 125 /* ↓ */, modifierFlags: []))
    }

    // MARK: - Post-ranking viewport restoration

    func testRetainedNonFirstSelectionIsPostRankingScrollTarget() {
        XCTAssertEqual(
            QuickSwitcherView.selectionScrollTarget(
                selectedID: "later.md", rowIDs: ["first.md", "later.md"]),
            "later.md")
    }

    func testMissingSelectionIsNotPostRankingScrollTarget() {
        XCTAssertNil(
            QuickSwitcherView.selectionScrollTarget(
                selectedID: "missing.md", rowIDs: ["first.md", "later.md"]))
        XCTAssertNil(
            QuickSwitcherView.selectionScrollTarget(
                selectedID: nil, rowIDs: ["first.md", "later.md"]))
    }

    func testStationaryPointerCannotOwnSelectionAfterListReconstruction() {
        let stationary = CGPoint(x: 100, y: 200)
        XCTAssertFalse(
            QuickSwitcherView.shouldAdmitHoverSelection(
                resultRevision: 9,
                armedRevision: 8,
                baselineLocation: stationary,
                currentLocation: CGPoint(x: 101, y: 200)),
            "movement from an older publication must not take selection")
        XCTAssertFalse(
            QuickSwitcherView.shouldAdmitHoverSelection(
                resultRevision: 9,
                armedRevision: 9,
                baselineLocation: stationary,
                currentLocation: stationary))
    }

    func testActualPointerMovementCanOwnSelectionForCurrentPublication() {
        XCTAssertTrue(
            QuickSwitcherView.shouldAdmitHoverSelection(
                resultRevision: 9,
                armedRevision: 9,
                baselineLocation: CGPoint(x: 100, y: 200),
                currentLocation: CGPoint(x: 101, y: 200)))
        XCTAssertFalse(
            QuickSwitcherView.shouldAdmitHoverSelection(
                resultRevision: 9,
                armedRevision: 9,
                baselineLocation: nil,
                currentLocation: CGPoint(x: 101, y: 200)))
    }

    // MARK: - APCA contrast (row text roles)
    //
    // The quick switcher reuses the palette's exact colour roles:
    // labelColor (primary name) / secondaryLabelColor (dimmed path)
    // over controlBackgroundColor, and selectedMenuItemTextColor over
    // selectedContentBackgroundColor. Re-assert |Lc| > 75 here so a
    // future divergence in this view fails its own suite.

    func testPrimaryAndSecondaryTextClearAPCAOverControlBackground() {
        for name in [NSAppearance.Name.darkAqua, .aqua] {
            guard let appearance = NSAppearance(named: name) else { continue }
            for role in [NSColor.labelColor, NSColor.secondaryLabelColor] {
                var fg = NSColor.black, bg = NSColor.white
                appearance.performAsCurrentDrawingAppearance {
                    fg = role.usingColorSpace(.sRGB)!
                    bg = NSColor.controlBackgroundColor.usingColorSpace(.sRGB)!
                }
                let lc = APCAContrast.lc(text: fg, background: bg)
                XCTAssertGreaterThan(
                    abs(lc), 75,
                    "row text vs controlBackgroundColor under \(name.rawValue) must clear |Lc| > 75 (got \(lc))")
            }
        }
    }

    func testSelectedRowTextClearsAPCA() {
        for name in [NSAppearance.Name.darkAqua, .aqua] {
            guard let appearance = NSAppearance(named: name) else { continue }
            var fg = NSColor.black, bg = NSColor.white
            appearance.performAsCurrentDrawingAppearance {
                fg = NSColor.selectedMenuItemTextColor.usingColorSpace(.sRGB)!
                bg = NSColor.selectedContentBackgroundColor.usingColorSpace(.sRGB)!
            }
            let lc = APCAContrast.lc(text: fg, background: bg)
            XCTAssertGreaterThan(abs(lc), 75, "selected-row text must clear |Lc| > 75 (got \(lc))")
        }
    }

    // MARK: - Construction smoke

    @MainActor
    func testQuickSwitcherViewLoadsWithoutCrashing() async {
        let appState = AppState()
        let view = QuickSwitcherView().environmentObject(appState)
        let host = NSHostingController(rootView: view)
        _ = host.view
        XCTAssertNotNil(host.view)
    }
}
