// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import SwiftUI

/// Modal command palette shell — Milestone Q issue #313.
///
/// Lands the search field + empty results placeholder + dismissal
/// plumbing. The fuzzy filter (#315), section grouping +
/// announcements (#316), and menu-bridge population of the registry
/// (#314) come in follow-up issues; this PR deliberately ships an
/// empty list so the layout / focus / hotkey contract is reviewable
/// in isolation.
///
/// ## Behaviour
///
/// - Opens via `⌘⇧P` (menu wiring in `SlateMacApp`, gated on
///   `isVaultOpen`); the search field auto-focuses on appear.
/// - Closes via `Esc` (`.cancelAction` overlay). Matches the
///   Xcode / Sublime / TextMate convention — open via chord,
///   close via Esc. We deliberately don't ship a second hidden
///   `⌘⇧P` button inside the sheet to "toggle close" because
///   the behaviour would rest on SwiftUI's responder-chain
///   routing and can't be verified without XCUITest infra.
/// - Focus returns to the prior first responder via SwiftUI's
///   default sheet behaviour.
///
/// ## Colours
///
/// Background is `NSColor.controlBackgroundColor`; text uses
/// `labelColor` and `secondaryLabelColor`. Both pairings pass the
/// project's APCA `|Lc| > 75` bar in light and dark mode (verified
/// by `CommandPaletteViewTests`).
struct CommandPaletteView: View {
    @EnvironmentObject private var appState: AppState
    @Environment(\.dismiss) private var dismiss

    /// Local search query. Reset on every open — the palette doesn't
    /// preserve typed text across sessions. Per #315, this will feed
    /// the fuzzy filter once the registry has real commands.
    @State private var query: String = ""

    @FocusState private var searchFocused: Bool

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            searchField
            Divider()
            resultsPlaceholder
        }
        .frame(minWidth: 560, idealWidth: 560, minHeight: 360, idealHeight: 360)
        .background(Color(nsColor: .controlBackgroundColor))
        // Hidden Cancel button so Esc routes through SwiftUI's
        // .cancelAction even though there's no visible cancel UI.
        // accessibilityHidden so VoiceOver doesn't surface a
        // "Cancel" element the user never asked for.
        .overlay(alignment: .bottomTrailing) {
            Button("Cancel") { dismiss() }
                .keyboardShortcut(.cancelAction)
                .opacity(0)
                .frame(width: 0, height: 0)
                .accessibilityHidden(true)
        }
        .onAppear {
            // Auto-focus the search field on every open so the user
            // can start typing immediately.
            searchFocused = true
        }
    }

    // MARK: - Search field

    private var searchField: some View {
        HStack(spacing: 8) {
            Image(systemName: "magnifyingglass")
                .foregroundStyle(Color(nsColor: .secondaryLabelColor))
                .accessibilityHidden(true)
            TextField("Search commands", text: $query)
                .textFieldStyle(.plain)
                .font(.title3)
                .focused($searchFocused)
                .accessibilityLabel("Search commands")
                // Hint only describes wired behaviour. Enter-to-invoke
                // lands in #315; the hint will gain it then.
                .accessibilityHint("Type to filter. Press Escape to close.")
        }
        .padding(.horizontal, 16)
        .padding(.vertical, 12)
    }

    // MARK: - Results placeholder

    /// Until the menu-bridge issue lands, the palette has nothing
    /// to render. Surface the empty state with user-facing copy
    /// (no issue numbers leaking through to VoiceOver utterance).
    private var resultsPlaceholder: some View {
        VStack(spacing: 8) {
            Spacer()
            Text("No commands available yet")
                .font(.headline)
                .foregroundStyle(Color(nsColor: .labelColor))
            Text("Commands will appear here in a future update.")
                .font(.subheadline)
                .foregroundStyle(Color(nsColor: .secondaryLabelColor))
                .multilineTextAlignment(.center)
            Spacer()
        }
        .frame(maxWidth: .infinity)
        .padding(.horizontal, 24)
        .accessibilityElement(children: .combine)
        .accessibilityLabel("No commands available yet. Commands will appear here in a future update.")
    }
}
