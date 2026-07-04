// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI

/// Outgoing-links section: every link from the currently-selected
/// note — resolved internal, unresolved internal, and external —
/// in document order. Bound to `AppState.currentOutgoingLinks`.
///
/// The Outgoing-links LEAF in the right-pane rail (U4-2, #471). Bindings
/// and row AX are unchanged from the sidebar-stack panel it replaces; the
/// self-hiding `EmptyView` gate is now a labeled leaf empty state (DoD §A)
/// and the `DisclosureGroup` is now a non-collapsible header row (the rail
/// selects; the disclosure was a stack-era space-saver). Every entry is in
/// the leaf's AX tree with no collapse to walk past — the #424 (F-C2)
/// concern that a collapsed disclosure hid entries from VoiceOver is moot
/// now the disclosure is gone.
struct OutgoingLinksPanel: View {
    @EnvironmentObject private var appState: AppState

    var body: some View {
        Group {
            if appState.selectedFilePath == nil {
                LeafEmptyState(message: "Select a note to see its outgoing links.")
            } else {
                LeafSection { header } content: { content }
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .navigationTitle("Outgoing links")
    }

    private var header: some View {
        let count = appState.currentOutgoingLinks.count
        let suffix = count == 1 ? "entry" : "entries"
        return Text("Outgoing links, \(count) \(suffix)")
            .font(.headline)
            .accessibilityAddTraits(.isHeader)
    }

    @ViewBuilder
    private var content: some View {
        if appState.isLoadingLinks && appState.currentOutgoingLinks.isEmpty {
            HStack(spacing: 8) {
                ProgressView()
                    .controlSize(.small)
                Text("Loading outgoing links…")
                    .foregroundStyle(.secondary)
            }
            .padding(.vertical, 4)
            .accessibilityElement(children: .combine)
            .accessibilityLabel("Loading outgoing links.")
        } else if appState.currentOutgoingLinks.isEmpty {
            Text("This note has no outgoing links.")
                .font(.callout)
                .foregroundStyle(.secondary)
                .padding(.vertical, 4)
                .accessibilityLabel("This note has no outgoing links.")
        } else {
            VStack(alignment: .leading, spacing: 4) {
                ForEach(
                    Array(appState.currentOutgoingLinks.enumerated()),
                    id: \.offset
                ) { _, link in
                    row(for: link)
                }
            }
        }
    }

    private func row(for link: OutgoingLink) -> some View {
        Button {
            appState.openLink(link)
        } label: {
            VStack(alignment: .leading, spacing: 2) {
                HStack(spacing: 6) {
                    Text(displayTarget(for: link))
                        .font(.callout)
                        .foregroundStyle(linkColor(for: link))
                        // Strikethrough on unresolved: visual axis
                        // complements the accessibility-label "Unresolved
                        // link:" prefix so the state is unmistakable for
                        // both sighted and screen-reader users (the
                        // acceptance criteria require both).
                        .strikethrough(link.isUnresolved, color: Tokens.ColorRole.warningText)
                    badge(for: link)
                }
                if !link.snippet.isEmpty {
                    // No lineLimit: snippet is a context excerpt from
                    // the note body — readable content, not chrome.
                    // The prior `.lineLimit(2)` truncated at large
                    // Dynamic Type sizes below the WCAG 1.4.4
                    // threshold.
                    Text(link.snippet)
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }
            }
            .frame(maxWidth: .infinity, alignment: .leading)
            .padding(.vertical, 2)
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .accessibilityLabel(accessibilityLabel(for: link))
        .accessibilityHint(accessibilityHint(for: link))
        .help(link.targetRaw)
        // U1-5 (#457): open-in targets for RESOLVED internal links only —
        // external links open in the browser and unresolved ones don't
        // navigate, so neither earns tab/split variants.
        .contextMenu {
            if let path = link.targetPath, !link.isExternal {
                Button("Open") {
                    appState.openFile(path, target: .currentTab)
                }
                Button("Open in New Tab") {
                    appState.openFile(path, target: .newTab)
                }
                Button("Open in Split") {
                    appState.openFile(path, target: .newSplit(.horizontal))
                }
            }
        }
    }

    private func accessibilityHint(for link: OutgoingLink) -> String {
        if link.isExternal {
            return "Opens in the default browser."
        }
        if link.isUnresolved {
            return "Cannot open. Target file is not in the vault."
        }
        return "Opens the linked note."
    }

    @ViewBuilder
    private func badge(for link: OutgoingLink) -> some View {
        if link.isExternal {
            badgeText("External")
        } else if link.isUnresolved {
            badgeText("Unresolved")
                .foregroundStyle(Tokens.ColorRole.warningText)
        } else if link.isEmbed {
            badgeText("Embed")
        }
    }

    private func badgeText(_ text: String) -> some View {
        // Plain Text is the most reliable AX-friendly badge here — a
        // custom shape would need its own label, and Dynamic Type
        // already scales font-based badges naturally.
        Text(text)
            .font(.caption2)
            .padding(.horizontal, 4)
            .padding(.vertical, 1)
            .overlay(
                RoundedRectangle(cornerRadius: 3)
                    .stroke(.secondary, lineWidth: 0.5)
            )
            .accessibilityHidden(true) // role is in the row's label
    }

    private func displayTarget(for link: OutgoingLink) -> String {
        if let path = link.targetPath {
            return (path as NSString).lastPathComponent
        }
        return link.targetRaw
    }

    private func linkColor(for link: OutgoingLink) -> Color {
        if link.isUnresolved {
            // Unresolved-link target text: the amber "needs attention" role
            // (APCA-gated ≥ 78 both appearances). Raw `.orange` measured Lc ≈ 43
            // on this leaf's surface — below the project floor (U5-3, #476).
            return Tokens.ColorRole.warningText
        }
        return Tokens.ColorRole.textPrimary
    }

    private func accessibilityLabel(for link: OutgoingLink) -> String {
        let target = displayTarget(for: link)
        if link.isExternal {
            return "External link: \(link.targetRaw)"
        }
        if link.isUnresolved {
            return "Unresolved link: \(link.targetRaw)"
        }
        return "Link to \(target)"
    }
}
