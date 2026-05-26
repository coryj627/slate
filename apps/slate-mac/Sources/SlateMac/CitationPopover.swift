// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI

/// Field-level expansion of a single citation. Each `BibEntry` field
/// renders as its own AT element so VoiceOver users can step through
/// title → authors → year → journal → DOI → URL → abstract one at a
/// time. The "one giant blob" anti-pattern called out in the academic
/// library research is exactly what this view exists to avoid.
struct CitationPopover: View {
    let citation: RenderedCitation
    let onClose: () -> Void

    @State private var abstractExpanded: Bool = false
    @FocusState private var initialFocus: InitialFocusTarget?

    enum InitialFocusTarget: Hashable {
        case title
        case unresolvedNotice
    }

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 12) {
                if let entry = citation.bibEntry {
                    resolvedBody(entry: entry)
                } else {
                    unresolvedBody
                }

                Divider()

                Button("Close") {
                    onClose()
                }
                .keyboardShortcut(.cancelAction)
                .accessibilityHint("Close the expanded citation.")
            }
            .padding(16)
        }
        .frame(
            minWidth: 380,
            idealWidth: 460,
            maxWidth: 600,
            minHeight: 220,
            maxHeight: 520
        )
        .accessibilityElement(children: .contain)
        .accessibilityLabel(accessibilitySummary)
        .onAppear {
            // Initial keyboard / VO focus lands on the most-relevant
            // row so Tab + arrow navigation start there.
            initialFocus = citation.bibEntry == nil ? .unresolvedNotice : .title
        }
    }

    // MARK: - Resolved entry

    @ViewBuilder
    private func resolvedBody(entry: BibEntry) -> some View {
        Group {
            if !entry.title.isEmpty {
                fieldRow(
                    label: "Title",
                    value: entry.title,
                    focus: .title
                )
            }
            if !entry.authors.isEmpty {
                fieldRow(label: "Authors", value: formatAuthors(entry.authors))
            }
            if let year = entry.year {
                fieldRow(label: "Year", value: String(year))
            }
            if let journal = entry.journal, !journal.isEmpty {
                fieldRow(label: "Journal", value: journal)
            }
            if let publisher = entry.publisher, !publisher.isEmpty {
                fieldRow(label: "Publisher", value: publisher)
            }
            if let doi = entry.doi, !doi.isEmpty {
                linkRow(label: "DOI", value: doi, url: doiURL(doi))
            }
            if let url = entry.url, !url.isEmpty, let parsed = URL(string: url) {
                linkRow(label: "URL", value: url, url: parsed)
            }
            if let abstract = entry.abstractText, !abstract.isEmpty {
                abstractRow(text: abstract)
            }
        }
    }

    private func fieldRow(
        label: String,
        value: String,
        focus: InitialFocusTarget? = nil
    ) -> some View {
        VStack(alignment: .leading, spacing: 2) {
            Text(label)
                .font(.caption.weight(.semibold))
                .foregroundStyle(.secondary)
                .accessibilityHidden(true)
            Text(value)
                .textSelection(.enabled)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .accessibilityElement(children: .combine)
        .accessibilityLabel("\(label): \(value)")
        .modifier(MaybeFocused(target: focus, focus: $initialFocus))
    }

    private func linkRow(label: String, value: String, url: URL) -> some View {
        HStack(alignment: .firstTextBaseline, spacing: 8) {
            Text(label)
                .font(.caption.weight(.semibold))
                .foregroundStyle(.secondary)
                .accessibilityHidden(true)
            Link(value, destination: url)
                .lineLimit(2)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .accessibilityElement(children: .combine)
        .accessibilityLabel("\(label): \(value)")
        .accessibilityAddTraits(.isLink)
    }

    private func abstractRow(text: String) -> some View {
        DisclosureGroup(isExpanded: $abstractExpanded) {
            Text(text)
                .font(.body)
                .textSelection(.enabled)
                .frame(maxWidth: .infinity, alignment: .leading)
                .padding(.top, 4)
        } label: {
            Text("Abstract")
                .font(.caption.weight(.semibold))
                .foregroundStyle(.secondary)
        }
        .accessibilityElement(children: .contain)
        .accessibilityLabel(
            abstractExpanded ? "Abstract: \(text)" : "Abstract, collapsed."
        )
    }

    // MARK: - Unresolved

    @ViewBuilder
    private var unresolvedBody: some View {
        let key = extractKey(from: citation.raw)
        VStack(alignment: .leading, spacing: 8) {
            Text("Citation key not found")
                .font(.headline)
                .accessibilityAddTraits(.isHeader)
            Text("'\(key)' isn't in any bibliography source.")
                .foregroundStyle(.secondary)
        }
        .accessibilityElement(children: .combine)
        .accessibilityLabel(
            "Unresolved citation: \(key). This key isn't in any bibliography source."
        )
        .modifier(MaybeFocused(target: .unresolvedNotice, focus: $initialFocus))
    }

    // MARK: - Helpers

    private var accessibilitySummary: String {
        if let entry = citation.bibEntry {
            var parts: [String] = ["Citation expanded."]
            if !entry.title.isEmpty {
                parts.append("Title: \(entry.title).")
            }
            return parts.joined(separator: " ")
        }
        let key = extractKey(from: citation.raw)
        return "Unresolved citation: \(key)."
    }

    /// Pull the first citation key from the source-form `raw` text.
    /// Handles `[@key]`, `[@key, ...]`, `[-@key]`, `@key`, etc.
    private func extractKey(from raw: String) -> String {
        guard let atIdx = raw.firstIndex(of: "@") else {
            return unknownKeyFallback()
        }
        let after = raw.index(after: atIdx)
        let tail = raw[after...]
        let key = tail.prefix { ch in
            ch.isLetter || ch.isNumber || ch == "_" || ch == "-" || ch == ":"
                || ch == "." || ch == "+"
        }
        let trimmed = key.trimmingCharacters(in: CharacterSet(charactersIn: "."))
        return trimmed.isEmpty ? unknownKeyFallback() : String(trimmed)
    }

    private func formatAuthors(_ authors: [Author]) -> String {
        let names: [String] = authors.map { author in
            if let given = author.given, !given.isEmpty {
                return "\(author.family), \(given)"
            }
            return author.family
        }
        return names.joined(separator: "; ")
    }

    private func doiURL(_ doi: String) -> URL {
        // `doi.org/<doi>` resolves through the global DOI handle
        // system. Doesn't depend on the user's network proxy.
        return URL(string: "https://doi.org/\(doi)")
            ?? URL(string: "https://doi.org/")!
    }

    private func unknownKeyFallback() -> String {
        // Defensive — should not happen because the panel only
        // routes here for RenderedCitations that came from real refs.
        "unknown"
    }
}

/// SwiftUI's `.focused()` modifier wants a non-optional target. This
/// shim makes it optional so a field can opt out of initial focus
/// without each call-site having to wrap the modifier in `if`.
private struct MaybeFocused: ViewModifier {
    let target: CitationPopover.InitialFocusTarget?
    var focus: FocusState<CitationPopover.InitialFocusTarget?>.Binding

    func body(content: Content) -> some View {
        if let target {
            content.focused(focus, equals: target)
        } else {
            content
        }
    }
}

