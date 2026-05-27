// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import SwiftUI
import XCTest

@testable import SlateMac

/// Tests for the Milestone Q command palette shell (#313).
///
/// What's covered here:
///
/// 1. **AppState wiring** — `isCommandPaletteOpen` defaults to
///    `false`, mutates via the public setter, and `closeVault()`
///    resets it (regression guard for the welcome-screen-poison
///    case where a vault-close mid-palette would leave the bool
///    `true` and re-trigger the empty sheet on next vault open).
/// 2. **APCA contrast** — palette text colours (`labelColor` and
///    `secondaryLabelColor`) against `controlBackgroundColor`
///    clear the project's `|Lc| > 75` bar in both Aqua and
///    DarkAqua. Same project-wide standard the editor palette
///    enforces; helpers live in `APCAContrast`.
/// 3. **View construction** — `CommandPaletteView` loads into an
///    `NSHostingController` without crashing. Catches SwiftUI
///    hierarchy mistakes (missing environmentObject, etc.).
///
/// What's NOT covered here (rides the Milestone Q integration
/// suite, #317):
///
/// - Search-field auto-focus on sheet appear.
/// - Esc dismissing the sheet through SwiftUI's responder chain.
/// - Focus restoration to the prior first responder.
/// - The `⌘⇧P` menu shortcut actually firing.
///
/// These need XCUITest / a real running app — unit tests can only
/// reach the model layer.
final class CommandPaletteViewTests: XCTestCase {

    // MARK: - AppState wiring

    @MainActor
    func testIsCommandPaletteOpenDefaultsToFalse() async {
        let appState = AppState()
        XCTAssertFalse(appState.isCommandPaletteOpen, "palette starts closed")
    }

    @MainActor
    func testIsCommandPaletteOpenIsPublishedAndMutable() async {
        // Exercises the published binding the menu item writes to
        // and the sheet binding reads from. Doesn't test the
        // SwiftUI shortcut routing — that needs XCUITest (#317).
        let appState = AppState()
        appState.isCommandPaletteOpen = true
        XCTAssertTrue(appState.isCommandPaletteOpen)
        appState.isCommandPaletteOpen = false
        XCTAssertFalse(appState.isCommandPaletteOpen)
    }

    /// Regression guard for the welcome-screen-poison bug
    /// (red-team finding on #313): if the user opens the palette
    /// then closes the vault, `closeVault()` must reset the bool
    /// so the next vault open doesn't auto-present an empty
    /// palette. Belt-and-suspenders with the menu item's
    /// `.disabled(!isVaultOpen)` gate in `SlateMacApp`.
    @MainActor
    func testCloseVaultResetsIsCommandPaletteOpen() async {
        let appState = AppState()
        appState.isCommandPaletteOpen = true
        appState.closeVault()
        XCTAssertFalse(
            appState.isCommandPaletteOpen,
            "closeVault must reset isCommandPaletteOpen so the next vault open doesn't auto-present"
        )
    }

    // MARK: - APCA contrast

    /// Palette body text (`labelColor`) against modal background
    /// (`controlBackgroundColor`) must clear APCA `|Lc| > 75` in
    /// both light and dark mode. The palette uses these colours
    /// explicitly so this test is the gate against a future
    /// regression (system colour shift, hard-coded override, etc.).
    func testLabelColorClearsAPCAAgainstControlBackground() {
        for appearanceName in [NSAppearance.Name.darkAqua, .aqua] {
            guard let appearance = NSAppearance(named: appearanceName) else { continue }
            var fg = NSColor.black, bg = NSColor.white
            appearance.performAsCurrentDrawingAppearance {
                fg = NSColor.labelColor.usingColorSpace(.sRGB)!
                bg = NSColor.controlBackgroundColor.usingColorSpace(.sRGB)!
            }
            let lc = APCAContrast.lc(text: fg, background: bg)
            XCTAssertGreaterThan(
                abs(lc), 75,
                "labelColor vs controlBackgroundColor under \(appearanceName.rawValue) must clear APCA |Lc| > 75 (got Lc \(lc))"
            )
        }
    }

    /// Secondary text (`secondaryLabelColor`, used for the search
    /// magnifier glyph and the placeholder helper line) must also
    /// clear Lc 75. `secondaryLabelColor` is a reduced-alpha
    /// variant of `labelColor` over the window background, so this
    /// is the actual cross-mode failure mode worth testing.
    func testSecondaryLabelColorClearsAPCAAgainstControlBackground() {
        for appearanceName in [NSAppearance.Name.darkAqua, .aqua] {
            guard let appearance = NSAppearance(named: appearanceName) else { continue }
            var fg = NSColor.black, bg = NSColor.white
            appearance.performAsCurrentDrawingAppearance {
                fg = NSColor.secondaryLabelColor.usingColorSpace(.sRGB)!
                bg = NSColor.controlBackgroundColor.usingColorSpace(.sRGB)!
            }
            let lc = APCAContrast.lc(text: fg, background: bg)
            XCTAssertGreaterThan(
                abs(lc), 75,
                "secondaryLabelColor vs controlBackgroundColor under \(appearanceName.rawValue) must clear APCA |Lc| > 75 (got Lc \(lc))"
            )
        }
    }

    // MARK: - View construction smoke

    /// Hosting `CommandPaletteView` into an `NSHostingController` and
    /// loading its view triggers SwiftUI's body resolution — this
    /// catches missing `environmentObject`, broken bindings, or any
    /// crash inside the view's initialiser. Doesn't validate visual
    /// behaviour; that's the integration suite's job.
    @MainActor
    func testCommandPaletteViewLoadsWithoutCrashing() async {
        let appState = AppState()
        let view = CommandPaletteView().environmentObject(appState)
        let host = NSHostingController(rootView: view)
        _ = host.view // force `loadView`
        XCTAssertNotNil(host.view)
    }

    // MARK: - VoiceOver chord composition

    /// `voiceOverLabel(for:)` composes the command label with the
    /// spelled-out chord so blind users hear the same thing the
    /// macOS menu bar reads. Catches glyph→word translation
    /// regressions in `CommandPaletteView.chordGlyphWord`.
    func testVoiceOverLabelComposesChordIntoSpokenString() {
        let cases: [(label: String, hint: String?, expected: String)] = [
            ("Save", "⌘S", "Save, Command S"),
            ("New from Template…", "⇧⌘N", "New from Template…, Shift Command N"),
            ("Search", "⌘F", "Search, Command F"),
            ("Citation Summary", "⇧⌘J", "Citation Summary, Shift Command J"),
            ("Close Vault", nil, "Close Vault"),
            ("Plain Command", "", "Plain Command"),
        ]

        for (label, hint, expected) in cases {
            let cmd = Command(
                id: "test.\(label)",
                label: label,
                accessibilityHint: nil,
                hotkeyHint: hint,
                section: .editor
            )
            XCTAssertEqual(
                CommandPaletteView.voiceOverLabel(for: cmd),
                expected,
                "voiceOverLabel for \(label) / \(hint ?? "nil") drifted"
            )
        }
    }

    // APCA helper lives in `APCAContrast.swift` (shared with
    // `EditorSyntaxPaletteTests`). Reference: APCA-W3 v0.1.9,
    // G-4g constants.
}
