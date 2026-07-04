// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI

/// The one interactive-affordance implementation for the presentation-ready
/// surfaces (Milestone U5-2, #475). Every custom `Button`-backed control that
/// is NOT hosted in a `List` (which draws its own native rest/hover/pressed/
/// selected/focus states) reaches for this instead of hand-rolling a hover
/// wash — so the six interactive states are defined once, not seven times
/// (u5_spec §U5-2 checklist item 3).
///
/// **What it owns (emphasis only):**
///  - **rest** — no fill.
///  - **hover** — a `surfaceSecondary` wash (the token the checklist names).
///  - **pressed** — a deepened wash (`surfaceSecondary` over a faint
///    `textPrimary` veil), so a press reads as distinct from a hover.
///
/// **What it deliberately does NOT own (unchanged semantics — U5-2 is
/// spacing/typography/emphasis only, never behavior):**
///  - **selected** — the call site keeps drawing its own selection fill +
///    shape indicator (e.g. the tab's `surface` fill, the rail's 2pt bar) and
///    owns the `.isSelected` trait. A ButtonStyle can't see selection, and
///    conflating the two would move an AX decision into presentation.
///  - **focused** — the system focus ring is left to the platform (never
///    suppressed; the U5-2 audit confirmed no `focusEffectDisabled` /
///    `focusable(false)` anywhere). `.plain`/this style both let AppKit draw it.
///  - **disabled** — SwiftUI's built-in disabled dimming + the call site's
///    non-interactive `help`/hint stay as they are.
///
/// **Accessibility:** purely visual. It adds no AX elements, labels, traits,
/// or actions, so the call sites' VoiceOver contracts are untouched. The wash
/// appears/disappears WITHOUT animation (an instant fill swap), so it is
/// inherently Reduce-Motion-safe — there is no motion to guard (WCAG 2.3.1).
/// Corner radius is caller-supplied so a control's hover shape matches its
/// existing selected shape (the tab strip's 5pt, the rail's square).
struct InteractiveRowStyle: ButtonStyle {
    /// The hover/press wash's corner radius — match the control's own
    /// selected-state shape so hover and selection register as the same object.
    var cornerRadius: CGFloat = Tokens.Radius.control

    @State private var isHovered = false

    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .background(
                RoundedRectangle(cornerRadius: cornerRadius)
                    .fill(Self.fill(hovered: isHovered, pressed: configuration.isPressed))
            )
            .contentShape(Rectangle())
            .onHover { isHovered = $0 }
    }

    /// The state → wash mapping, pure + static so `InteractiveRowStyleTests`
    /// pins it (the u5_spec "shared style unit tests: state → expected token"
    /// requirement). Pressed outranks hover; rest is no fill.
    ///  - rest (`!hovered, !pressed`) → `.clear`
    ///  - hover → `surfaceSecondary`
    ///  - pressed → a deepened wash (a faint `textPrimary` veil — "deepened",
    ///    distinct from the hover surface, no new token).
    static func fill(hovered: Bool, pressed: Bool) -> Color {
        if pressed {
            return Tokens.ColorRole.textPrimary.opacity(0.10)
        }
        if hovered {
            return Tokens.ColorRole.surfaceSecondary
        }
        return .clear
    }
}

extension ButtonStyle where Self == InteractiveRowStyle {
    /// The shared interactive-row affordance (U5-2). Use on custom Button
    /// controls outside a `List`; pass the control's own selected-shape corner
    /// radius so the hover wash matches (defaults to `Tokens.Radius.control`).
    static func interactiveRow(cornerRadius: CGFloat = Tokens.Radius.control) -> Self {
        InteractiveRowStyle(cornerRadius: cornerRadius)
    }
}
