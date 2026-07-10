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

    // MARK: Corner radius — the small, fixed rounding scale for control shapes
    // (selection fills, chips, hover washes). Fixed points, not Dynamic-Type
    // scaled: a corner radius is a shape constant, not text (WCAG 1.4.4 governs
    // the text inside, which still scales). Call sites reach for these so a
    // control's hover wash and its selected fill share one rounding (U5-2).
    enum Radius {
        /// 3 — tight chips / inline badges (e.g. an unresolved-link badge).
        static let chip: CGFloat = 3
        /// 4 — small blocks (raw-source / code-fence fallbacks).
        static let small: CGFloat = 4
        /// 5 — the default control shape (tab background, interactive-row wash).
        static let control: CGFloat = 5
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
        // Accent/destructive are split into FILL vs TEXT so the two can never
        // be confused (Codex review): `accentFill` is a control background that
        // `onAccentFill` text sits on; `accentText` is an accent-colored
        // foreground (a link/label) that sits on `surface`. Each is contrast-
        // tuned for its own direction and gated below.
        static let accentFill = Color(nsColor: .tokenAccentFill)
        static let onAccentFill = Color(nsColor: .tokenOnAccentFill)
        static let accentText = Color(nsColor: .tokenAccentText)
        static let destructiveFill = Color(nsColor: .tokenDestructiveFill)
        static let onDestructiveFill = Color(nsColor: .tokenOnDestructiveFill)
        static let destructiveText = Color(nsColor: .tokenDestructiveText)
        // Warning/attention — an accent-colored foreground for non-error
        // "needs attention" states (an unresolved wikilink, an embed that
        // couldn't resolve). Split TEXT-only like accent/destructive: the
        // amber that reads as "warning" is inherently light, so the light-mode
        // value is a dark amber-brown (clears the APCA floor on `surface` AND
        // `surfaceSecondary`) and the dark-mode value a pale amber. Gated in
        // `contrastPairings`. Replaces the raw `.orange` literals U1–U4 left in
        // the link/embed leaves (U5-3, #476) — those measured Lc ≈ 43 (light),
        // below the project floor; this role is measured ≥ 78.
        static let warningText = Color(nsColor: .tokenWarningText)
        static let selection = Color(nsColor: .tokenSelection)
        // Text that sits ON the `selection` fill. `selection` is the highlight
        // behind a selected row's label, so the label's foreground must clear
        // the APCA floor against it in both appearances (gated below). Kept a
        // distinct role from `textPrimary` so a future theme (Milestone R) can
        // re-tune the selection fill and its text together without touching the
        // on-surface text roles (U5-3, #476).
        static let onSelection = Color(nsColor: .tokenOnSelection)
        static let separator = Color(nsColor: .tokenSeparator)
    }

    /// Every pairing that carries text MUST clear APCA `|Lc| > 75` in both
    /// appearances — foreground text roles over BOTH surfaces, and text-on-fill
    /// for the control fills. `DesignTokensTests` iterates this; a new
    /// text-carrying role is added here so its contrast can't go unchecked.
    static let contrastPairings: [(name: String, text: NSColor, surface: NSColor)] = [
        ("textPrimary on surface", .tokenTextPrimary, .tokenSurface),
        ("textPrimary on surfaceSecondary", .tokenTextPrimary, .tokenSurfaceSecondary),
        ("textSecondary on surface", .tokenTextSecondary, .tokenSurface),
        ("textSecondary on surfaceSecondary", .tokenTextSecondary, .tokenSurfaceSecondary),
        ("accentText on surface", .tokenAccentText, .tokenSurface),
        ("accentText on surfaceSecondary", .tokenAccentText, .tokenSurfaceSecondary),
        ("destructiveText on surface", .tokenDestructiveText, .tokenSurface),
        ("destructiveText on surfaceSecondary", .tokenDestructiveText, .tokenSurfaceSecondary),
        ("warningText on surface", .tokenWarningText, .tokenSurface),
        ("warningText on surfaceSecondary", .tokenWarningText, .tokenSurfaceSecondary),
        ("onAccentFill on accentFill", .tokenOnAccentFill, .tokenAccentFill),
        ("onDestructiveFill on destructiveFill", .tokenOnDestructiveFill, .tokenDestructiveFill),
        ("onSelection on selection", .tokenOnSelection, .tokenSelection),
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
        light: srgb(0.34, 0.34, 0.36), dark: srgb(0.855, 0.855, 0.875))

    // Accent — fixed (not the user's control-accent) so contrast is
    // deterministic. FILL (control background, white text on it) vs TEXT
    // (accent-colored foreground on `surface`) are separate roles: a
    // colored foreground that clears APCA |Lc|>75 must be dark in light mode
    // and near-pale in dark mode, so it can't share values with the fill.
    static let tokenAccentFill = tokenDynamic(
        light: srgb(0.00, 0.34, 0.78), dark: srgb(0.16, 0.45, 0.85))
    static let tokenOnAccentFill = tokenDynamic(
        light: srgb(1.00, 1.00, 1.00), dark: srgb(1.00, 1.00, 1.00))
    static let tokenAccentText = tokenDynamic(
        light: srgb(0.031, 0.271, 0.627), dark: srgb(0.780, 0.870, 0.990))

    // Destructive — same FILL vs TEXT split.
    static let tokenDestructiveFill = tokenDynamic(
        light: srgb(0.72, 0.00, 0.05), dark: srgb(0.80, 0.20, 0.22))
    static let tokenOnDestructiveFill = tokenDynamic(
        light: srgb(1.00, 1.00, 1.00), dark: srgb(1.00, 1.00, 1.00))
    // The light value also clears the builder's blue selected-row wash; the
    // brighter system red and the former #A80014 token both missed that carrier.
    static let tokenDestructiveText = tokenDynamic(
        light: srgb(0.470, 0.00, 0.078), dark: srgb(0.960, 0.800, 0.810))

    // Warning/attention TEXT — dark amber-brown in light, pale amber in dark.
    // Measured APCA (both surfaces, both appearances) all clear |Lc| > 78 —
    // the tightest is warningText-on-surfaceSecondary (light 78.5, dark 84.0);
    // system `.orange` in the same slots measured ≈ 43/-60, below the floor.
    static let tokenWarningText = tokenDynamic(
        light: srgb(0.46, 0.27, 0.00), dark: srgb(1.00, 0.86, 0.58))

    // Text ON the selection fill. Shares `textPrimary`'s tuned values (the
    // selection fill is a light wash in light / muted blue in dark, so primary
    // text clears the floor over it — measured light 87.2, dark 89.3) but kept
    // a NAMED role so the selection pairing is gated and theme-swappable
    // independently of on-surface text.
    static let tokenOnSelection = tokenDynamic(
        light: srgb(0.09, 0.09, 0.10), dark: srgb(0.96, 0.96, 0.97))

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
