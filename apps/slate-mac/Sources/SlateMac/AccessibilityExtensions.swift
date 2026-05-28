// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI

/// Shared accessibility helpers used by multiple surfaces
/// (`CommandPaletteView`, `TasksReviewView`, `TasksPanel`, …). New
/// helpers go here rather than at file scope so neighbouring
/// features can adopt them without a cross-file import dependency.
///
/// **Visibility:** module-internal (Swift default). Helpers in
/// this file are not part of the SlateMac module's public API —
/// they exist to keep accessibility patterns consistent across
/// the app. If a future module needs the same primitive, copy
/// the implementation rather than `public`-promoting these and
/// inviting cross-module coupling on UI a11y semantics.

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
    /// Example — chain it after the label / hint modifiers, passing
    /// the selection state directly:
    ///
    /// ```swift
    /// Button { … } label: { … }
    ///     .accessibilityLabel("Heading")
    ///     .accessibilityIsSelected(rowIsSelected)
    /// ```
    ///
    /// The current consumers follow this shape: `CommandPaletteView`
    /// (result row), `TasksReviewView` (filter chip + task toggle),
    /// and `TasksPanel` (task toggle) — each chains after
    /// `.accessibilityLabel` / `.accessibilityHint` and passes a
    /// `Bool` selection state.
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
