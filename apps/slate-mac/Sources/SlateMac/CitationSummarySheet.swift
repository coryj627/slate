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
                Text("This note has no citations.")
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
                        postAccessibilityAnnouncement(.citationWalkThrough)
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
        // Esc dismissal. This was the one sheet in the app with no
        // cancel path — Done carries `.defaultAction` (Return), and a
        // second `.keyboardShortcut` can't sit on the same button, so
        // the container-level exit command supplies Esc (the
        // MoveToFolderSheet pattern). Return and Esc now both close,
        // which is correct for a read-only summary: there is nothing
        // to cancel, only to leave.
        .onExitCommand {
            appState.isCitationSummaryOpen = false
        }
        .accessibilityElement(children: .contain)
        // Sheet-level label so VoiceOver reads the stat on appear
        // without the user having to navigate to the body Text.
        .accessibilityLabel(announcementLabel())
    }

    private func summaryText(_ citations: [RenderedCitation]) -> String {
        let (totalText, uniqueText) = countParts(citations)
        return "This note has \(totalText) referencing \(uniqueText)."
    }

    private func announcementLabel() -> String {
        let citations = appState.currentNoteCitations
        if citations.isEmpty {
            return "Citation Summary. This note has no citations."
        }
        return "Citation Summary. \(summaryText(citations))"
    }

    private func countParts(_ citations: [RenderedCitation])
        -> (totalText: String, uniqueText: String)
    {
        let total = citations.count
        // The structured CitationReferences (loaded in parallel with
        // the rendered list) carry the per-site `citations` array
        // each site can contain (`[@a; @b]` is one site with two
        // CitedItems). Use them when present so multi-citation
        // sites contribute every key to the unique-source set —
        // the RenderedCitation FFI shape doesn't expose those.
        // Fall back to the resolved `bibEntry.key` / raw-extracted
        // key when refs aren't loaded yet (race window).
        var keys = Set<String>()
        let refs = appState.currentNoteCitationRefs
        if refs.count == total {
            for ref in refs {
                for item in ref.citations {
                    keys.insert(item.key)
                }
            }
        } else {
            for citation in citations {
                if let entry = citation.bibEntry {
                    keys.insert(entry.key)
                } else {
                    let key = extractCitationKey(from: citation.raw)
                    keys.insert(key.isEmpty ? citation.raw : key)
                }
            }
        }
        let unique = keys.count
        let totalText = "\(CountCopy.counted(total, "citation", "citations"))"
        let uniqueText = "\(unique) unique \(unique == 1 ? "source" : "sources")"
        return (totalText, uniqueText)
    }
}
