// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import SwiftUI
import XCTest

@testable import SlateMac

/// Reusable "presentation-ready" test assertions (Milestone U0-4, #452) that
/// every U1–U5 surface calls to hold itself to the program's Definition of Done
/// (`docs/plans/08_ui_parity/00_program.md`) — one entry point per check.
///
/// **What this covers (and what it deliberately doesn't).** The DoD's §D
/// (dark + light + APCA) is unit-testable and lives here: contrast is measured
/// in both appearances, and each surface is rendered in both appearances as a
/// smoke test. What is NOT reliably unit-testable — and is therefore left to
/// the `a11y-check` static gate (run in CI over all of `Sources/SlateMac`, so
/// new component families are scanned automatically) and the VoiceOver
/// feature-test runbook (`docs/runbooks/voiceover-feature-test.md`):
///   - VoiceOver label/trait presence and reading/focus order — there is no
///     public API to read a rendered SwiftUI accessibility tree from XCTest;
///   - Reduce-Motion animation behaviour;
///   - Dynamic Type reflow — the `\.dynamicTypeSize` environment override is
///     NOT honored by headless `ImageRenderer`/`NSHostingView` (measured: a
///     token-styled surface renders at an identical size at `.large` and
///     `.accessibility5`), so a unit assertion can't distinguish a scaling
///     view from a fixed-size one. `a11y-check` covers the fixed-size-font
///     anti-pattern statically instead.
/// This split is honest by design: a fake assertion that couldn't actually
/// verify its claim would give false confidence.
enum PresentationReady {

    static let appearanceNames: [NSAppearance.Name] = [.aqua, .darkAqua]

    // MARK: §D — contrast (measured, both appearances)

    /// Assert every `(text, surface)` pairing clears the project's APCA floor
    /// (`|Lc| > minLc`, default 75) in BOTH Aqua and DarkAqua. Dynamic colors
    /// resolve per appearance; failures name the pairing, appearance, and Lc.
    static func assertContrastFloor(
        _ pairings: [(name: String, text: NSColor, surface: NSColor)],
        minLc: Double = 75,
        file: StaticString = #filePath, line: UInt = #line
    ) {
        for pairing in pairings {
            for name in appearanceNames {
                guard let appearance = NSAppearance(named: name) else {
                    XCTFail("Appearance \(name.rawValue) unavailable.", file: file, line: line)
                    continue
                }
                let lc = APCAContrast.lc(
                    text: pairing.text, background: pairing.surface, for: appearance)
                XCTAssertGreaterThan(
                    abs(lc), minLc,
                    "\(pairing.name) under \(name.rawValue): APCA |Lc| "
                        + "\(String(format: "%.1f", lc)) < \(minLc) "
                        + "(text \(hex(pairing.text, name)), bg \(hex(pairing.surface, name))).",
                    file: file, line: line)
            }
        }
    }

    /// Assert each dynamic color resolves to a DIFFERENT sRGB value in light vs
    /// dark — proves the light/dark pair is wired and no appearance leaks.
    static func assertResolvesDistinctlyPerAppearance(
        _ roles: [(name: String, color: NSColor)],
        file: StaticString = #filePath, line: UInt = #line
    ) {
        for role in roles {
            let light = hex(role.color, .aqua)
            let dark = hex(role.color, .darkAqua)
            XCTAssertNotEqual(
                light, dark,
                "\(role.name) resolves identically (\(light)) in light and dark.",
                file: file, line: line)
        }
    }

    // MARK: §E — layout (rendered, headless via NSHostingView)

    /// Assert the view renders to a finite, non-empty size in BOTH appearances
    /// — a smoke test that catches per-appearance crashes and failed renders.
    @MainActor
    static func assertRendersInBothAppearances(
        _ view: some View, file: StaticString = #filePath, line: UInt = #line
    ) {
        for name in appearanceNames {
            let size = renderedSize(view, appearance: name)
            XCTAssertTrue(
                size.width.isFinite && size.height.isFinite && size.height > 0,
                "View failed to render under \(name.rawValue) (size \(size)).",
                file: file, line: line)
        }
    }

    // MARK: - internals

    /// Rendered content size (points) via `ImageRenderer`, under `name`. Used to
    /// prove the surface actually renders (non-nil image, non-empty size) in a
    /// given appearance.
    @MainActor
    private static func renderedSize(
        _ view: some View, appearance name: NSAppearance.Name
    ) -> CGSize {
        let renderer = ImageRenderer(content: view.frame(width: 320))
        renderer.scale = 1  // 1 pt == 1 px
        var size = CGSize.zero
        NSAppearance(named: name)?.performAsCurrentDrawingAppearance {
            if let image = renderer.nsImage { size = image.size }
        }
        return size
    }

    /// Resolve a (possibly dynamic) color to an sRGB hex under `name`.
    private static func hex(_ color: NSColor, _ name: NSAppearance.Name) -> String {
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
