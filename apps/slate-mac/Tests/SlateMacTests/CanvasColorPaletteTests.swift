// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import XCTest

@testable import SlateMac

/// #370 acceptance: canvas color is never meaning-alone, fills keep
/// APCA Lc > 75 for body text in BOTH appearances, and Increase
/// Contrast collapses decoration to system colors — verified over all
/// six presets, a hex sample, and the uncolored default.
@MainActor
final class CanvasColorPaletteTests: XCTestCase {
    private static let rawSamples: [String?] = ["1", "2", "3", "4", "5", "6", "#8b1a1a", nil]
    private static let appearances: [NSAppearance.Name] = [.aqua, .darkAqua]

    func testTextOnEveryFillPassesAPCAInBothAppearances() throws {
        for name in Self.appearances {
            let appearance = try XCTUnwrap(NSAppearance(named: name))
            for raw in Self.rawSamples {
                for isGroup in [false, true] {
                    let fill = CanvasColorPalette.cardFill(
                        raw: raw, isGroup: isGroup,
                        increaseContrast: false, appearance: appearance)
                    let text = CanvasColorPalette.cardTextOn(fill: fill, appearance: appearance)
                    let lc = abs(APCAContrast.lc(text: text, background: fill, for: appearance))
                    XCTAssertGreaterThan(
                        lc, 75.0,
                        "APCA floor: raw=\(raw ?? "nil") group=\(isGroup) \(name.rawValue) Lc=\(lc)"
                    )
                }
            }
        }
    }

    func testSelectionRingCarrierPassesAPCAAgainstEveryFill() throws {
        // The dual-stroke ring's carrier (labelColor) is the measured
        // guarantee (t5 G7) — against every fill AND the canvas
        // background, both appearances. Zoom never changes colors and
        // the ring is constant 3 pt screen-space, so the matrix is
        // zoom-independent by construction.
        for name in Self.appearances {
            let appearance = try XCTUnwrap(NSAppearance(named: name))
            var backgrounds: [NSColor] = Self.rawSamples.flatMap { raw in
                [
                    CanvasColorPalette.cardFill(
                        raw: raw, isGroup: false, increaseContrast: false, appearance: appearance),
                    CanvasColorPalette.cardFill(
                        raw: raw, isGroup: true, increaseContrast: false, appearance: appearance),
                ]
            }
            backgrounds.append(
                CanvasColorPalette.cardFill(
                    raw: nil, isGroup: false, increaseContrast: false, appearance: appearance))
            for fill in backgrounds {
                let ring = CanvasColorPalette.selectionRingCarrierOn(
                    fill: fill, appearance: appearance)
                let lc = abs(APCAContrast.lc(text: ring, background: fill, for: appearance))
                XCTAssertGreaterThan(lc, 75.0, "ring carrier vs fill in \(name.rawValue): \(lc)")
            }
        }
    }

    func testIncreaseContrastCollapsesToSystemColors() throws {
        let appearance = try XCTUnwrap(NSAppearance(named: .aqua))
        for raw in Self.rawSamples {
            let fill = CanvasColorPalette.cardFill(
                raw: raw, isGroup: false, increaseContrast: true, appearance: appearance)
            let plain = CanvasColorPalette.cardFill(
                raw: nil, isGroup: false, increaseContrast: true, appearance: appearance)
            XCTAssertEqual(fill, plain, "IC collapses every fill to the plain background")
        }
        // Colored borders/edges collapse to label colors (still
        // visible, no hue) — the color NAME remains in text values.
        let border = CanvasColorPalette.cardBorder(
            raw: "1", increaseContrast: true, appearance: appearance)
        let edge = CanvasColorPalette.edgeStroke(
            raw: "5", increaseContrast: true, appearance: appearance)
        XCTAssertEqual(border, CanvasColorPalette.selectionRingCarrier(appearance: appearance))
        XCTAssertEqual(edge, CanvasColorPalette.selectionRingCarrier(appearance: appearance))
    }

    func testHexParsingAndPresetBases() {
        XCTAssertNotNil(CanvasColorPalette.baseColor(forRaw: "#fb464c"))
        XCTAssertNotNil(CanvasColorPalette.baseColor(forRaw: "#f00"))
        XCTAssertNil(CanvasColorPalette.baseColor(forRaw: "bogus"))
        XCTAssertNil(CanvasColorPalette.baseColor(forRaw: nil))
        for preset in ["1", "2", "3", "4", "5", "6"] {
            XCTAssertNotNil(CanvasColorPalette.baseColor(forRaw: preset), preset)
        }
    }

    /// The one Swift spelling of the preset set is exactly the ids core
    /// names: every id in `presetIDs` gets a preset name from
    /// `canvasColorName`, and the ids just outside it get core's
    /// out-of-set answer, so a preset added (or dropped) core-side fails
    /// here instead of drifting from the color sheets that iterate it.
    func testThePresetRangeIsExactlyTheIdsCoreNames() {
        let outside = canvasColorName(color: .preset(preset: 0))
        XCTAssertEqual(outside, "custom color", "core's answer for a non-preset id")
        let names = CanvasColorPalette.presetIDs.map {
            canvasColorName(color: .preset(preset: UInt8($0)))
        }
        XCTAssertEqual(names, ["red", "orange", "yellow", "green", "cyan", "purple"])
        let after = UInt8(CanvasColorPalette.presetIDs.upperBound + 1)
        XCTAssertEqual(canvasColorName(color: .preset(preset: after)), outside)
        for id in CanvasColorPalette.presetIDs {
            XCTAssertNotNil(CanvasColorPalette.baseColor(forRaw: String(id)), "preset \(id) paints")
        }
    }
}
