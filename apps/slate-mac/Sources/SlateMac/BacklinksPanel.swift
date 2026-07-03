// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI

/// Inbound-links section: every file that links to the currently-
/// selected note. Bound to `AppState.currentBacklinks`.
///
/// Activation flow (clicking a row to jump to the source file) is
/// wired in #C5 — this panel ships the readable, screen-reader-
/// navigable list shape only.
struct BacklinksPanel: View {
    @EnvironmentObject private var appState: AppState
    @State private var isExpanded = true

    var body: some View {
        // Hide entirely when no note is selected. `EmptyView` removes
        // the panel from the AX tree so VoiceOver doesn't enumerate
        // an empty section — per the acceptance criteria.
        if appState.selectedFilePath == nil {
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
        let count = appState.currentBacklinks.count
        let suffix = count == 1 ? "entry" : "entries"
        return Text("Backlinks, \(count) \(suffix)")
            .font(.headline)
            .accessibilityAddTraits(.isHeader)
    }

    @ViewBuilder
    private var content: some View {
        if appState.isLoadingLinks && appState.currentBacklinks.isEmpty {
            HStack(spacing: 8) {
                ProgressView()
                    .controlSize(.small)
                Text("Loading backlinks…")
                    .foregroundStyle(.secondary)
            }
            .padding(.vertical, 4)
            .accessibilityElement(children: .combine)
            .accessibilityLabel("Loading backlinks.")
        } else if appState.currentBacklinks.isEmpty {
            Text("No notes link here yet.")
                .font(.callout)
                .foregroundStyle(.secondary)
                .padding(.vertical, 4)
                .accessibilityLabel("No notes link here yet.")
        } else {
            VStack(alignment: .leading, spacing: 4) {
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
            VStack(alignment: .leading, spacing: 2) {
                Text(filename(for: backlink.sourcePath))
                    .foregroundStyle(.primary)
                    .font(.callout)
                // No lineLimit: the snippet is a context excerpt
                // from the source note's body — content the user
                // wants to read, not chrome to constrain. At large
                // Dynamic Type the prior `.lineLimit(3)` truncated
                // below the WCAG 1.4.4 threshold. Let it wrap.
                Text(backlink.snippet)
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }
            .frame(maxWidth: .infinity, alignment: .leading)
            .padding(.vertical, 2)
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
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
