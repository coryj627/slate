// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI

/// Shared accessibility helpers used by multiple surfaces
/// (`CommandPaletteView`, `TasksReviewView`, `TasksPanel`, …). New
/// helpers go here rather than at file scope so neighbouring
/// features can adopt them without a cross-file import dependency.

extension View {
    /// Conditionally add the `.isSelected` accessibility trait —
    /// only when `isSelected` is true. Avoids the
    /// `.accessibilityAddTraits(isSelected ? [.isSelected] : [])`
    /// pattern whose empty-array branch leaves doubt about whether
    /// SwiftUI clears a previously-applied trait between renders
    /// (see #324 investigation).
    ///
    /// With this helper the trait either appears in the modifier
    /// chain for the current render (when selected) or doesn't
    /// appear at all (when not) — no accumulation question. SwiftUI
    /// rebuilds the modifier chain per-render from the body
    /// expression, so a modifier that isn't present in this render's
    /// chain can't carry state over from a previous render where it
    /// was present.
    ///
    /// **Do not inline back to**
    /// `.accessibilityAddTraits(isSelected ? [.isSelected] : [])`
    /// **— see #324 for the rationale.** That pattern works in
    /// practice (SwiftUI's `Add`-traits with an empty `OptionSet` is
    /// a no-op) but the API name leaves the question open and we
    /// settled it by construction instead.
    @ViewBuilder
    func accessibilityIsSelected(_ isSelected: Bool) -> some View {
        if isSelected {
            self.accessibilityAddTraits(.isSelected)
        } else {
            self
        }
    }
}
