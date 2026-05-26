// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI

/// "Citation Summary" alert / sheet (Cmd+Shift+J / Milestone L #282).
///
/// Reads `AppState.currentNoteCitations` and displays the two stats
/// the issue specifies: total citation count (N) and unique-source
/// count (M). The sheet's `accessibilityLabel` carries the full
/// summary text so VoiceOver reads it on appear (no need for the
/// user to navigate into the body).
///
/// Empty-document case shows a single OK button instead of the
/// walk-through option, per spec.
struct CitationSummarySheet: View {
    @EnvironmentObject private var appState: AppState

    var body: some View {
        VStack(alignment: .leading, spacing: 16) {
            Text("Citation Summary")
                .font(.title2.weight(.semibold))
                .accessibilityAddTraits(.isHeader)

            let citations = appState.currentNoteCitations

            if citations.isEmpty {
                Text("This document has no citations.")
                    .foregroundStyle(.secondary)
            } else {
                Text(summaryText(citations))
                    .foregroundStyle(.primary)
            }

            HStack {
                Spacer()
                if !citations.isEmpty {
                    Button("Walk through citations") {
                        appState.isCitationSummaryOpen = false
                        // Open the Citations sidebar tab so the user
                        // can step through rows one at a time. Real
                        // SwiftUI tab-selection programmatic control
                        // is fiddly (each TabView reads its own
                        // .id); for V1 we rely on the user's tab
                        // muscle memory from the announcement.
                        postAccessibilityAnnouncement(
                            "Walk through citations. Switch to the Citations sidebar tab and arrow through the list.",
                            priority: .medium
                        )
                    }
                    .accessibilityHint("Closes this sheet and starts a walk-through.")
                }
                Button(citations.isEmpty ? "OK" : "Done") {
                    appState.isCitationSummaryOpen = false
                }
                .keyboardShortcut(.defaultAction)
                .accessibilityHint("Close the citation summary.")
            }
        }
        .padding(20)
        .frame(minWidth: 380, idealWidth: 460, maxWidth: 560, minHeight: 160)
        .accessibilityElement(children: .contain)
        // Sheet-level label so VoiceOver reads the stat on appear
        // without the user having to navigate to the body Text.
        .accessibilityLabel(announcementLabel())
    }

    private func summaryText(_ citations: [RenderedCitation]) -> String {
        let (totalText, uniqueText) = countParts(citations)
        return "This document has \(totalText) referencing \(uniqueText)."
    }

    private func announcementLabel() -> String {
        let citations = appState.currentNoteCitations
        if citations.isEmpty {
            return "Citation Summary. This document has no citations."
        }
        return "Citation Summary. \(summaryText(citations))"
    }

    private func countParts(_ citations: [RenderedCitation])
        -> (totalText: String, uniqueText: String)
    {
        let total = citations.count
        var keys = Set<String>()
        for citation in citations {
            // Each RenderedCitation wraps a resolved or unresolved
            // citation site. Use `bibEntry.key` for resolved cases;
            // for unresolved, extract the citation key from the raw
            // source-form text so the same key with different
            // locators (e.g. "[@smith2020]" vs "[@smith2020, p. 2]")
            // collapses to one unique source — same semantic the
            // resolved branch gets for free.
            //
            // Codoki PR #293: previously used `raw` directly here,
            // which over-counted unique sources whenever the same
            // key appeared with multiple locators.
            if let entry = citation.bibEntry {
                keys.insert(entry.key)
            } else {
                let key = extractCitationKey(from: citation.raw)
                keys.insert(key.isEmpty ? citation.raw : key)
            }
        }
        let unique = keys.count
        let totalText = "\(total) \(total == 1 ? "citation" : "citations")"
        let uniqueText = "\(unique) unique \(unique == 1 ? "source" : "sources")"
        return (totalText, uniqueText)
    }
}
