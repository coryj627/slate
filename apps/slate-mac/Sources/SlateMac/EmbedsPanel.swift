// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI

/// Sidebar section: every `![[…]]` embed in the currently-selected
/// note, rendered through `EmbedView` so AT users can read the
/// resolved content + jump to source.
///
/// Why a panel instead of inline rendering in the editor? The
/// editor uses `NSTextView` (read-only mode just disables editing),
/// which doesn't natively support SwiftUI splicing. True inline
/// rendering lives on `NSTextAttachment` and is the next milestone-
/// J issue (#188). The panel ships first because it can light up
/// embeds today against the existing architecture without touching
/// the editor's responder chain.
struct EmbedsPanel: View {
    @EnvironmentObject private var appState: AppState
    @State private var isExpanded = true

    var body: some View {
        // Hide when no note is selected OR when the loaded note has
        // no embeds. `EmptyView` removes the panel from the AX tree
        // so VoiceOver doesn't enumerate an empty section.
        if appState.selectedFilePath == nil || embedLinks.isEmpty {
            EmptyView()
        } else {
            DisclosureGroup(isExpanded: $isExpanded) {
                content
            } label: {
                header
            }
            .padding(.horizontal, 8)
            .padding(.vertical, 4)
        }
    }

    private var header: some View {
        let count = embedLinks.count
        let suffix = count == 1 ? "entry" : "entries"
        return Text(verbatim: "Embeds, \(count) \(suffix)")
            .font(.headline)
            .accessibilityAddTraits(.isHeader)
    }

    @ViewBuilder
    private var content: some View {
        if appState.isLoadingEmbeds {
            HStack(spacing: 8) {
                ProgressView()
                    .controlSize(.small)
                Text(verbatim: "Resolving embeds…")
                    .foregroundStyle(.secondary)
            }
            .padding(.vertical, 4)
            .accessibilityElement(children: .combine)
            .accessibilityLabel("Resolving embeds.")
        } else if let err = appState.embedsLoadError {
            HStack(alignment: .top, spacing: 6) {
                SlateSymbol.warning.decorative
                    .foregroundStyle(.orange)
                Text(verbatim: "Could not resolve embeds: \(err)")
                    .font(.caption)
                    .foregroundStyle(.primary)
            }
            .padding(.vertical, 4)
            .accessibilityElement(children: .combine)
            .accessibilityLabel("Could not resolve embeds: \(err)")
        } else {
            VStack(alignment: .leading, spacing: 8) {
                ForEach(Array(embedLinks.enumerated()), id: \.offset) { _, link in
                    row(for: link)
                }
            }
        }
    }

    private func row(for link: OutgoingLink) -> some View {
        let key = appState.embedTargetKey(link)
        let resolution = appState.currentNoteEmbedResolutions[key]
        return VStack(alignment: .leading, spacing: 4) {
            if let resolution {
                EmbedView(
                    resolution: resolution,
                    jumpToSourceAction: { target in
                        appState.openEmbedTarget(target)
                    }
                )
            } else {
                // The resolution hasn't landed yet (selection
                // change in flight, or the embed wasn't in the
                // batch). Show a placeholder so the panel still
                // accounts for every embed link.
                HStack(alignment: .top, spacing: 6) {
                    SlateSymbol.moreActions.decorative
                        .foregroundStyle(.secondary)
                    Text(verbatim: "Embed not yet resolved: \(key)")
                        .font(.callout)
                        .foregroundStyle(.secondary)
                }
                .padding(.vertical, 4)
                .accessibilityElement(children: .combine)
                .accessibilityLabel("Embed not yet resolved: \(key)")
            }
        }
    }

    private var embedLinks: [OutgoingLink] {
        appState.currentOutgoingLinks.filter { $0.isEmbed }
    }
}
