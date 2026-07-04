// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI

/// Inbound-links section: every file that links to the currently-
/// selected note. Bound to `AppState.currentBacklinks`.
///
/// The Backlinks LEAF in the right-pane rail (U4-2, #471). Bindings and
/// row AX are unchanged from the sidebar-stack panel it replaces; the two
/// structural changes the leaf host requires: the old self-hiding
/// `EmptyView` gate is now a labeled leaf empty state (a leaf must never be
/// a blank rectangle — DoD §A), and the `DisclosureGroup` is now a
/// non-collapsible header row (the rail selects the leaf; the disclosure was
/// a stack-era space-saver). The header keeps its `.isHeader` trait + count.
struct BacklinksPanel: View {
    @EnvironmentObject private var appState: AppState

    var body: some View {
        Group {
            if appState.selectedFilePath == nil {
                LeafEmptyState(message: "Select a note to see its backlinks.")
            } else {
                LeafSection { header } content: { content }
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .navigationTitle("Backlinks")
    }

    private var header: some View {
        let count = appState.currentBacklinks.count
        let suffix = count == 1 ? "entry" : "entries"
        return Text("Backlinks, \(count) \(suffix)")
            .font(Tokens.Typography.sectionHeader)
            .accessibilityAddTraits(.isHeader)
    }

    @ViewBuilder
    private var content: some View {
        if appState.isLoadingLinks && appState.currentBacklinks.isEmpty {
            HStack(spacing: Tokens.Spacing.sm) {
                ProgressView()
                    .controlSize(.small)
                Text("Loading backlinks…")
                    .font(Tokens.Typography.body)
                    .foregroundStyle(Tokens.ColorRole.textSecondary)
            }
            .padding(.vertical, Tokens.Spacing.xs)
            .accessibilityElement(children: .combine)
            .accessibilityLabel("Loading backlinks.")
        } else if appState.currentBacklinks.isEmpty {
            Text("No notes link here yet.")
                .font(Tokens.Typography.callout)
                .foregroundStyle(Tokens.ColorRole.textSecondary)
                .padding(.vertical, Tokens.Spacing.xs)
                .accessibilityLabel("No notes link here yet.")
        } else {
            VStack(alignment: .leading, spacing: Tokens.Spacing.xs) {
                ForEach(Array(appState.currentBacklinks.enumerated()), id: \.offset) { _, backlink in
                    row(for: backlink)
                }
            }
        }
    }

    private func row(for backlink: Backlink) -> some View {
        Button {
            appState.openBacklink(backlink)
        } label: {
            VStack(alignment: .leading, spacing: Tokens.Spacing.xxs) {
                Text(filename(for: backlink.sourcePath))
                    .font(Tokens.Typography.callout)
                    .foregroundStyle(Tokens.ColorRole.textPrimary)
                // No lineLimit: the snippet is a context excerpt
                // from the source note's body — content the user
                // wants to read, not chrome to constrain. At large
                // Dynamic Type the prior `.lineLimit(3)` truncated
                // below the WCAG 1.4.4 threshold. Let it wrap.
                Text(backlink.snippet)
                    .font(Tokens.Typography.caption)
                    .foregroundStyle(Tokens.ColorRole.textSecondary)
            }
            .frame(maxWidth: .infinity, alignment: .leading)
            .padding(.vertical, Tokens.Spacing.xxs)
            .contentShape(Rectangle())
        }
        // Leaf row rest/hover/pressed affordance (U5-2).
        .buttonStyle(.interactiveRow(cornerRadius: Tokens.Radius.small))
        .accessibilityLabel(
            "Backlink from \(filename(for: backlink.sourcePath)), context: \(backlink.snippet)"
        )
        .accessibilityHint("Opens the source note.")
        .help(backlink.sourcePath)
        // U1-5 (#457): keyboard-discoverable open-in targets (rotor
        // actions); ⌘-click on the row is the pointer shortcut.
        .contextMenu {
            Button("Open") {
                appState.openFile(backlink.sourcePath, target: .currentTab)
            }
            Button("Open in New Tab") {
                appState.openFile(backlink.sourcePath, target: .newTab)
            }
            Button("Open in Split") {
                appState.openFile(backlink.sourcePath, target: .newSplit(.horizontal))
            }
        }
    }

    private func filename(for path: String) -> String {
        (path as NSString).lastPathComponent
    }
}
