// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import SwiftUI

/// The design-token layer (Milestone U0-3, #451): spacing, a type ramp, and
/// semantic color roles, so call sites reach for a token instead of a literal.
///
/// Colors are **dynamic** (a pinned sRGB pair per appearance) rather than
/// system semantics, for two reasons: (1) contrast is measurable and pinned —
/// `DesignTokensTests` asserts every text-on-surface and control pairing clears
/// APCA `|Lc| > 75` in BOTH Aqua and DarkAqua (system colors fell short of the
/// project's APCA floor in several mode combinations — see `EditorSyntaxPalette`
/// / #308); and (2) this is the seam Milestone R re-skins — a theme swaps the
/// role values, and no call site changes.
///
/// Text roles are tuned so even `textSecondary` clears the APCA floor: secondary
/// text is differentiated by weight/size, never by dangerously-low contrast.
enum Tokens {

    // MARK: Spacing — a 2pt-based scale. Call sites use these instead of magic
    // paddings so vertical rhythm and density stay consistent (and re-tunable).
    enum Spacing {
        /// 2 — hairline gaps (icon-to-text within a dense row).
        static let xxs: CGFloat = 2
        /// 4 — tight (related lines in a stacked cell).
        static let xs: CGFloat = 4
        /// 8 — default intra-group spacing.
        static let sm: CGFloat = 8
        /// 12 — standard control padding / group padding.
        static let md: CGFloat = 12
        /// 16 — section padding.
        static let lg: CGFloat = 16
        /// 24 — inter-section separation.
        static let xl: CGFloat = 24
        /// 32 — page-level breathing room.
        static let xxl: CGFloat = 32
    }

    // MARK: Typography — semantic roles mapped to Dynamic-Type text styles (never
    // fixed sizes, so text scales — WCAG 1.4.4). Milestone R can re-map roles.
    enum Typography {
        static let largeTitle = Font.largeTitle
        static let title = Font.title2
        /// Section / group headers (carry `.isHeader` at the call site).
        static let sectionHeader = Font.headline
        static let body = Font.body
        static let callout = Font.callout
        static let caption = Font.caption
        /// Monospaced role for code / keys.
        static let code = Font.system(.body, design: .monospaced)
    }

    // MARK: Color — semantic roles as SwiftUI `Color`, backed by dynamic
    // `NSColor` roles (below) so they resolve per appearance and are APCA-
    // measurable in tests.
    enum ColorRole {
        static let surface = Color(nsColor: .tokenSurface)
        static let surfaceSecondary = Color(nsColor: .tokenSurfaceSecondary)
        static let textPrimary = Color(nsColor: .tokenTextPrimary)
        static let textSecondary = Color(nsColor: .tokenTextSecondary)
        static let accent = Color(nsColor: .tokenAccent)
        static let onAccent = Color(nsColor: .tokenOnAccent)
        static let destructive = Color(nsColor: .tokenDestructive)
        static let onDestructive = Color(nsColor: .tokenOnDestructive)
        static let selection = Color(nsColor: .tokenSelection)
        static let separator = Color(nsColor: .tokenSeparator)
    }

    /// Text-on-surface + control (text-on-fill) pairings that MUST clear APCA
    /// `|Lc| > 75` in both appearances. `DesignTokensTests` iterates this — a new
    /// role that carries text is added here so its contrast is enforced.
    static let contrastPairings: [(name: String, text: NSColor, surface: NSColor)] = [
        ("textPrimary on surface", .tokenTextPrimary, .tokenSurface),
        ("textPrimary on surfaceSecondary", .tokenTextPrimary, .tokenSurfaceSecondary),
        ("textSecondary on surface", .tokenTextSecondary, .tokenSurface),
        ("textSecondary on surfaceSecondary", .tokenTextSecondary, .tokenSurfaceSecondary),
        ("onAccent on accent", .tokenOnAccent, .tokenAccent),
        ("onDestructive on destructive", .tokenOnDestructive, .tokenDestructive),
    ]
}

// MARK: - Dynamic NSColor roles (pinned sRGB per appearance)

extension NSColor {
    /// A dynamic color resolving to `light` in Aqua and `dark` in DarkAqua.
    fileprivate static func tokenDynamic(light: NSColor, dark: NSColor) -> NSColor {
        NSColor(name: nil) { appearance in
            appearance.bestMatch(from: [.aqua, .darkAqua]) == .darkAqua ? dark : light
        }
    }

    fileprivate static func srgb(_ r: Double, _ g: Double, _ b: Double) -> NSColor {
        NSColor(srgbRed: CGFloat(r), green: CGFloat(g), blue: CGFloat(b), alpha: 1)
    }

    // Surfaces
    static let tokenSurface = tokenDynamic(
        light: srgb(1.00, 1.00, 1.00), dark: srgb(0.117, 0.117, 0.129))
    static let tokenSurfaceSecondary = tokenDynamic(
        light: srgb(0.94, 0.94, 0.95), dark: srgb(0.160, 0.160, 0.176))

    // Text
    static let tokenTextPrimary = tokenDynamic(
        light: srgb(0.09, 0.09, 0.10), dark: srgb(0.96, 0.96, 0.97))
    static let tokenTextSecondary = tokenDynamic(
        light: srgb(0.34, 0.34, 0.36), dark: srgb(0.82, 0.82, 0.84))

    // Accent (fixed, not the user's control-accent, so contrast is deterministic)
    static let tokenAccent = tokenDynamic(
        light: srgb(0.00, 0.34, 0.78), dark: srgb(0.16, 0.45, 0.85))
    static let tokenOnAccent = tokenDynamic(
        light: srgb(1.00, 1.00, 1.00), dark: srgb(1.00, 1.00, 1.00))

    // Destructive
    static let tokenDestructive = tokenDynamic(
        light: srgb(0.72, 0.00, 0.05), dark: srgb(0.80, 0.20, 0.22))
    static let tokenOnDestructive = tokenDynamic(
        light: srgb(1.00, 1.00, 1.00), dark: srgb(1.00, 1.00, 1.00))

    // Non-text roles (selection fill, hairline separator). These are decorative
    // dividers/highlights redundant with spacing and selection state, so they
    // are intentionally NOT held to the non-text 3:1 bar (WCAG 1.4.11 exempts
    // decorative elements; Apple's own `separatorColor` is ~1.3:1). Kept subtle
    // on purpose — a 3:1 separator reads as a heavy rule, not a hairline.
    static let tokenSelection = tokenDynamic(
        light: srgb(0.82, 0.89, 0.99), dark: srgb(0.20, 0.28, 0.40))
    static let tokenSeparator = tokenDynamic(
        light: srgb(0.80, 0.80, 0.82), dark: srgb(0.28, 0.28, 0.30))
}
