// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI

/// Shared chrome for the right-pane leaves ported out of the retired left-
/// sidebar panel stack (Milestone U4-2, #471).
///
/// Two pieces, factored here so every ported leaf presents the same shape
/// and a future leaf can't drift:
///
///  - `LeafEmptyState` — the labeled, Tokens-styled placeholder a leaf shows
///    instead of self-hiding. A leaf occupies a selectable rail icon, so it
///    must never be a blank rectangle (DoD §A). This supersedes the stack-era
///    self-hiding `EmptyView`, which existed only to avoid pushing the file
///    list around — a constraint that no longer applies now the panels live
///    in their own pane.
///  - `LeafSection` — the header row + fill-the-pane content the leaf shows
///    when it has something to display. The stack panels wrapped their
///    contents in a `DisclosureGroup`; the leaf host doesn't (the rail
///    selects the leaf and the leaf fills the pane, so a collapse control is
///    redundant — the disclosure was a stack-era space-saver). The header the
///    panel supplies keeps its `.isHeader` trait + count, unchanged.

/// A leaf's empty state: a centered, labeled sentence in secondary text on
/// the surface token. `.combine`d into one AX element so VoiceOver reads the
/// single sentence rather than walking an empty container.
struct LeafEmptyState: View {
    let message: String

    var body: some View {
        VStack(spacing: Tokens.Spacing.sm) {
            Spacer()
            Text(message)
                .font(Tokens.Typography.body)
                .foregroundStyle(Tokens.ColorRole.textSecondary)
                .multilineTextAlignment(.center)
                .padding(.horizontal, Tokens.Spacing.md)
            Spacer()
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .accessibilityElement(children: .combine)
        .accessibilityLabel(message)
    }
}

/// A leaf's populated state: a non-collapsible header row over vertically-
/// scrolling content that fills the pane. The header carries its own
/// `.isHeader` trait and count (supplied by the panel); this wrapper adds no
/// AX of its own, so the panel's accessibility tree is unchanged from the
/// stack era apart from the dropped disclosure control.
struct LeafSection<Header: View, Content: View>: View {
    private let header: Header
    private let content: Content

    init(
        @ViewBuilder header: () -> Header,
        @ViewBuilder content: () -> Content
    ) {
        self.header = header()
        self.content = content()
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            header
                .frame(maxWidth: .infinity, alignment: .leading)
                .padding(.horizontal, Tokens.Spacing.sm)
                .padding(.vertical, Tokens.Spacing.xs)
            Divider()
            ScrollView {
                VStack(alignment: .leading, spacing: 0) {
                    content
                }
                .frame(maxWidth: .infinity, alignment: .leading)
                .padding(.horizontal, Tokens.Spacing.sm)
                .padding(.vertical, Tokens.Spacing.xs)
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .top)
    }
}
