import SwiftUI

/// Outgoing-links section: every link from the currently-selected
/// note — resolved internal, unresolved internal, and external —
/// in document order. Bound to `AppState.currentOutgoingLinks`.
///
/// Default state is collapsed because outgoing-links list is more
/// useful as an audit (Cmd+expand) than a permanently-open sidebar
/// element; per the acceptance criteria this gets revisited after
/// tester feedback.
struct OutgoingLinksPanel: View {
    @EnvironmentObject private var appState: AppState
    @State private var isExpanded = false

    var body: some View {
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
                        .strikethrough(link.isUnresolved, color: .orange)
                    badge(for: link)
                }
                if !link.snippet.isEmpty {
                    Text(link.snippet)
                        .font(.caption)
                        .foregroundStyle(.secondary)
                        .lineLimit(2)
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
                .foregroundStyle(.orange)
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
            return .orange
        }
        return .primary
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
