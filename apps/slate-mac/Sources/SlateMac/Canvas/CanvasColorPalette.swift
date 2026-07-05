// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit

/// Canvas color accessibility (Milestone T, #370).
///
/// JSON Canvas colors are the six Obsidian presets ("1"…"6") or hex;
/// authors encode meaning in them (red = blocker). Three rules:
///
/// 1. **Never color-alone (WCAG 1.4.1):** the NAME travels in text —
///    outline/table values and announcements phrase the backend's
///    `color_name` (hex → nearest preset + "custom"). This palette
///    only paints.
/// 2. **Text on fills passes APCA Lc > 75 in both appearances:** card
///    fills are the tint composited over `controlBackgroundColor` at
///    low opacity, so `NSColor.textColor` keeps body-text contrast on
///    every preset and any hex (the tint can shift the background only
///    fractionally). Verified exhaustively in CanvasColorPaletteTests.
/// 3. **Increase Contrast collapses decoration** (the
///    `EditorSyntaxPalette.color(for:increaseContrast:)` convention):
///    fills → plain `controlBackgroundColor`, borders/edges →
///    label colors. The color's MEANING stays available as text.
enum CanvasColorPalette {
    /// How much of the tint survives compositing into a card fill.
    /// Low enough that textColor keeps Lc > 75 on every preset in both
    /// appearances; high enough to read as the color it names.
    static let fillTintFraction: CGFloat = 0.18

    /// Obsidian's default preset RGBs — same reference points as the
    /// backend's nearest-preset naming (canvas/mod.rs PRESET_RGB).
    static func baseColor(forRaw raw: String?) -> NSColor? {
        guard let raw, !raw.isEmpty else { return nil }
        switch raw {
        case "1": return NSColor(srgbRed: 0xFB / 255, green: 0x46 / 255, blue: 0x4C / 255, alpha: 1)
        case "2": return NSColor(srgbRed: 0xE9 / 255, green: 0x97 / 255, blue: 0x3F / 255, alpha: 1)
        case "3": return NSColor(srgbRed: 0xE0 / 255, green: 0xDE / 255, blue: 0x71 / 255, alpha: 1)
        case "4": return NSColor(srgbRed: 0x44 / 255, green: 0xCF / 255, blue: 0x6E / 255, alpha: 1)
        case "5": return NSColor(srgbRed: 0x53 / 255, green: 0xDF / 255, blue: 0xDD / 255, alpha: 1)
        case "6": return NSColor(srgbRed: 0xA8 / 255, green: 0x82 / 255, blue: 0xFF / 255, alpha: 1)
        default: return hexColor(raw)
        }
    }

    static func hexColor(_ hex: String) -> NSColor? {
        guard hex.hasPrefix("#") else { return nil }
        var body = String(hex.dropFirst())
        if body.count == 3 { body = body.map { "\($0)\($0)" }.joined() }
        guard body.count == 6, let value = UInt32(body, radix: 16) else { return nil }
        return NSColor(
            srgbRed: CGFloat((value >> 16) & 0xFF) / 255,
            green: CGFloat((value >> 8) & 0xFF) / 255,
            blue: CGFloat(value & 0xFF) / 255,
            alpha: 1)
    }

    /// Card/group fill: the tint composited SOLID over the standard
    /// card background (deterministic — contrast is measurable, and
    /// stacked layers can't double up alpha). Uncolored: the plain
    /// background the pre-#370 renderer used. Always OPAQUE — system
    /// fills carry alpha, which would make any luminance measurement a
    /// lie; alpha resolves against `windowBackgroundColor` here.
    static func cardFill(raw: String?, isGroup: Bool, increaseContrast: Bool, appearance: NSAppearance)
        -> NSColor
    {
        let base = over(
            isGroup ? .quaternarySystemFill : .controlBackgroundColor,
            .windowBackgroundColor, appearance)
        guard !increaseContrast, let tint = baseColor(forRaw: raw) else {
            return base
        }
        return blend(tint: tint, fraction: fillTintFraction, over: base, appearance: appearance)
    }

    /// Card border: the solid tint names the color visually; under
    /// Increase Contrast (or uncolored) the system separator/label.
    static func cardBorder(raw: String?, increaseContrast: Bool, appearance: NSAppearance)
        -> NSColor
    {
        if increaseContrast {
            return resolved(baseColor(forRaw: raw) != nil ? .labelColor : .separatorColor, appearance)
        }
        return resolved(baseColor(forRaw: raw) ?? .separatorColor, appearance)
    }

    /// Edge stroke; colorless edges keep the quiet default.
    static func edgeStroke(raw: String?, increaseContrast: Bool, appearance: NSAppearance)
        -> NSColor
    {
        if increaseContrast {
            return resolved(
                baseColor(forRaw: raw) != nil ? .labelColor : .secondaryLabelColor, appearance)
        }
        return resolved(baseColor(forRaw: raw) ?? .tertiaryLabelColor, appearance)
    }

    /// Text on a card fill: the dynamic body color (the fills are
    /// tint-composited precisely so this stays compliant). Drawing
    /// uses `.textColor` directly; measurement composites its alpha
    /// over the fill it sits on.
    static func cardText(appearance: NSAppearance) -> NSColor {
        resolved(.textColor, appearance)
    }

    /// The measured on-fill text color: `.textColor`'s alpha
    /// composited over `fill` — what actually reaches the screen.
    static func cardTextOn(fill: NSColor, appearance: NSAppearance) -> NSColor {
        over(.textColor, fill, appearance)
    }

    /// The selection ring's contrast carrier (red-team-informed dual
    /// stroke: labelColor outer ring + accent core): labelColor is the
    /// measured Lc > 75 guarantee against every fill; the accent core
    /// is brand, not the carrier.
    static func selectionRingCarrier(appearance: NSAppearance) -> NSColor {
        resolved(.labelColor, appearance)
    }

    /// The ring carrier composited over the fill it rings (labelColor
    /// carries alpha) — the measured pairing.
    static func selectionRingCarrierOn(fill: NSColor, appearance: NSAppearance) -> NSColor {
        over(.labelColor, fill, appearance)
    }

    // MARK: internals

    private static func resolved(_ color: NSColor, _ appearance: NSAppearance) -> NSColor {
        var result = color
        appearance.performAsCurrentDrawingAppearance {
            result = color.usingColorSpace(.sRGB) ?? color
        }
        return result
    }

    /// Alpha-composite `top` over `bottom` under `appearance` — the
    /// result is opaque sRGB (measurable luminance).
    private static func over(_ top: NSColor, _ bottom: NSColor, _ appearance: NSAppearance)
        -> NSColor
    {
        let t = resolved(top, appearance)
        let b = resolved(bottom, appearance)
        let a = t.alphaComponent
        return NSColor(
            srgbRed: t.redComponent * a + b.redComponent * (1 - a),
            green: t.greenComponent * a + b.greenComponent * (1 - a),
            blue: t.blueComponent * a + b.blueComponent * (1 - a),
            alpha: 1)
    }

    private static func blend(
        tint: NSColor, fraction: CGFloat, over base: NSColor, appearance: NSAppearance
    ) -> NSColor {
        let t = resolved(tint, appearance)
        let b = resolved(base, appearance)
        return NSColor(
            srgbRed: t.redComponent * fraction + b.redComponent * (1 - fraction),
            green: t.greenComponent * fraction + b.greenComponent * (1 - fraction),
            blue: t.blueComponent * fraction + b.blueComponent * (1 - fraction),
            alpha: 1)
    }
}
