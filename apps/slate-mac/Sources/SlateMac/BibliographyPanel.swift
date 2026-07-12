// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI

/// Vault-wide bibliography pane. Two segments:
///
/// - **Entries** — every entry in the merged bibliography, with a
///   search field that filters on title + author family + key.
/// - **Unresolved** — every `[@key]` citation whose key has no
///   matching `bibliography_entries` row, grouped by file.
///
/// Bound to `AppState.bibliographyEntries` /
/// `AppState.unresolvedCitations`, which are loaded lazily by
/// `loadBibliographyEntries`. Each row's accessibility tree is
/// `.contain`-scoped so VoiceOver can step through fields one at a
/// time — same anti-blob pattern as `CitationPopover`.
///
/// Activating an entry row sets `AppState.expandedBibEntry` and presents
/// the shared `CitationPopover` anchored to that row (#878 — a field-level
/// expansion belongs on its trigger, not a detached sheet).
struct BibliographyPanel: View {
    @EnvironmentObject private var appState: AppState
    @State private var segment: Segment = .entries
    @State private var hasLoaded: Bool = false

    /// Focus-return target for the anchored entry popover (#878, WCAG 2.4.3 +
    /// 2.1.2): on dismiss, focus returns to the entry row that opened it —
    /// keyed by the entry's unique key.
    @AccessibilityFocusState private var focusedEntryKey: String?

    enum Segment: Hashable {
        case entries
        case unresolved
    }

    var body: some View {
        VStack(alignment: .leading, spacing: Tokens.Spacing.sm) {
            segmentPicker
                .padding(.horizontal, Tokens.Spacing.md)
                .padding(.top, Tokens.Spacing.sm)

            Divider()

            content
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .navigationTitle("Bibliography")
        .task {
            // Lazy-load on first tab open. Re-loads on session change
            // are driven by `closeVault` clearing the bibliography +
            // `setBibliographySources` calling loadBibliographyEntries
            // explicitly.
            if !hasLoaded {
                hasLoaded = true
                await appState.loadBibliographyEntries()
            }
        }
        .onChange(of: appState.pendingBibliographyKeyFocus) { _, newKey in
            // Cmd+J from an expanded citation routes here. Switch to
            // the Entries segment and let the search field do the
            // filtering — `bibliographySearchText` is already set by
            // `jumpToBibliographyFromExpandedCitation`. Clear the
            // pending key once we've handled it so the next jump can
            // re-trigger this onChange.
            if newKey != nil {
                segment = .entries
                appState.pendingBibliographyKeyFocus = nil
            }
        }
    }

    // MARK: - Segments

    private var segmentPicker: some View {
        Picker("", selection: $segment) {
            Text("Entries").tag(Segment.entries)
            Text("Unresolved").tag(Segment.unresolved)
        }
        .pickerStyle(.segmented)
        .accessibilityLabel("Bibliography view")
        .accessibilityValue(segment == .entries ? "Entries" : "Unresolved")
    }

    @ViewBuilder
    private var content: some View {
        switch segment {
        case .entries:
            entriesView
        case .unresolved:
            unresolvedView
        }
    }

    // MARK: - Entries

    @ViewBuilder
    private var entriesView: some View {
        if appState.currentSession == nil {
            emptyState(message: "Open a vault to see its bibliography.")
        } else if appState.isLoadingBibliography {
            loadingState(message: "Loading bibliography…")
        } else if let error = appState.bibliographyLoadError {
            errorState(message: error)
        } else if appState.bibliographyEntries.isEmpty {
            emptyState(
                message:
                    "No bibliography sources configured. Open Settings → Bibliography to add one."
            )
        } else {
            VStack(alignment: .leading, spacing: Tokens.Spacing.sm) {
                searchField
                    .padding(.horizontal, Tokens.Spacing.md)
                Divider()
                let filtered = appState.filteredBibliographyEntries()
                if filtered.isEmpty {
                    emptyState(
                        message: "No entries match '\(appState.bibliographySearchText)'."
                    )
                } else {
                    List(filtered, id: \.key) { entry in
                        entryRow(entry)
                    }
                    .listStyle(.sidebar)
                }
            }
        }
    }

    private var searchField: some View {
        TextField(
            "Search title, author, key…",
            text: $appState.bibliographySearchText
        )
        .textFieldStyle(.roundedBorder)
        .accessibilityLabel("Search bibliography")
        .accessibilityHint("Filters entries by title, author family name, or citation key.")
    }

    private func entryRow(_ entry: BibEntry) -> some View {
        Button {
            appState.expandedBibEntry = entry
        } label: {
            VStack(alignment: .leading, spacing: Tokens.Spacing.xxs) {
                Text(rowTitle(for: entry))
                    .font(Tokens.Typography.body)
                    .lineLimit(2)
                    .frame(maxWidth: .infinity, alignment: .leading)
                Text(rowSubtitle(for: entry))
                    .font(Tokens.Typography.caption)
                    .foregroundStyle(Tokens.ColorRole.textSecondary)
                    .lineLimit(2)
                    .accessibilityHidden(true)
            }
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        // Each row's AT label gives every field separately so the
        // user hears title → authors → year → key without having to
        // dive into the row's children.
        .accessibilityElement(children: .contain)
        .accessibilityLabel(rowAccessibilityLabel(for: entry))
        .accessibilityHint("Activate to expand citation fields.")
        .accessibilityFocused($focusedEntryKey, equals: entry.key)
        // popovers.md:21 — the entry expansion is a field-level detail of THIS
        // row, so the CitationPopover anchors here (arrow on the leading edge,
        // pointing at the row) rather than the detached sheet #878 replaced.
        // Entry keys are unique in the filtered list (List id: \.key), so the
        // row's own key disambiguates the anchor with no local index.
        .popover(
            isPresented: entryPopoverBinding(entry),
            attachmentAnchor: .rect(.bounds),
            arrowEdge: .leading
        ) {
            CitationPopover(
                entry: entry,
                onClose: {
                    // WCAG 2.4.3 + 2.1.2: return focus to the row anchor.
                    focusedEntryKey = entry.key
                    dismissEntryExpansion(returningFocusTo: entry.key)
                }
            )
            .environmentObject(appState)
        }
        .contextMenu {
            Button("Show files citing this entry") {
                Task {
                    await appState.requestFilesCiting(key: entry.key)
                }
            }
            Button("Insert citation in current note (V1.x)") {
                // V1.x scope per the milestone description. The
                // button has to stay enabled so the announcement
                // actually fires when activated — disabling it would
                // make the menu item silent, defeating the discovery
                // intent (Codoki PR #291).
                postAccessibilityAnnouncement(
                    "Insert citation lands in V1.x. See Milestone L.",
                    priority: .medium
                )
            }
        }
    }

    /// Per-row popover presentation keyed by the entry's unique key (#878).
    /// Dismissal (Close, Escape, or an outside click) clears the shared
    /// `expandedBibEntry` and returns focus to the entry row.
    private func entryPopoverBinding(_ entry: BibEntry) -> Binding<Bool> {
        Binding(
            get: { appState.expandedBibEntry?.key == entry.key },
            set: { presented in
                if !presented { dismissEntryExpansion(returningFocusTo: entry.key) }
            }
        )
    }

    private func dismissEntryExpansion(returningFocusTo key: String) {
        appState.expandedBibEntry = nil
        // WCAG 2.4.3 + 2.1.2: return focus to the row that opened the popover
        // — the focus-return contract the sheet path established.
        focusedEntryKey = key
    }

    private func rowTitle(for entry: BibEntry) -> String {
        let title = entry.title.isEmpty ? entry.key : entry.title
        if let year = entry.year {
            return "\(title) (\(year))"
        }
        return title
    }

    private func rowSubtitle(for entry: BibEntry) -> String {
        let names = entry.authors.prefix(3).map { $0.family }
        var subtitle = names.joined(separator: ", ")
        if entry.authors.count > 3 {
            subtitle += ", et al."
        }
        if let journal = entry.journal, !journal.isEmpty {
            subtitle += subtitle.isEmpty ? journal : " — \(journal)"
        }
        return subtitle.isEmpty ? entry.key : subtitle
    }

    private func rowAccessibilityLabel(for entry: BibEntry) -> String {
        var parts: [String] = []
        let title = entry.title.isEmpty ? entry.key : entry.title
        parts.append("Title: \(title).")
        if !entry.authors.isEmpty {
            let names = entry.authors.map { author in
                if let given = author.given, !given.isEmpty {
                    return "\(author.family), \(given)"
                }
                return author.family
            }
            parts.append("Authors: \(names.joined(separator: "; ")).")
        }
        if let year = entry.year {
            parts.append("Year: \(year).")
        }
        if let journal = entry.journal, !journal.isEmpty {
            parts.append("Journal: \(journal).")
        }
        parts.append("Key: \(entry.key).")
        return parts.joined(separator: " ")
    }

    // MARK: - Unresolved

    @ViewBuilder
    private var unresolvedView: some View {
        if appState.currentSession == nil {
            emptyState(message: "Open a vault to see unresolved citations.")
        } else if appState.isLoadingBibliography {
            loadingState(message: "Loading unresolved citations…")
        } else if appState.unresolvedCitations.isEmpty {
            emptyState(
                message: "No unresolved citations. Every key in your notes has a bibliography entry."
            )
        } else {
            unresolvedList
        }
    }

    private var unresolvedList: some View {
        // Group by file path so the user sees one section per file
        // with its missing keys nested. SwiftUI's List + Section
        // already handles AT semantics for grouped lists.
        let grouped = Dictionary(grouping: appState.unresolvedCitations) { $0.path }
        let paths = grouped.keys.sorted()
        return List {
            ForEach(paths, id: \.self) { path in
                Section {
                    ForEach(grouped[path] ?? [], id: \.key) { item in
                        Text(item.key)
                            .accessibilityLabel("Unresolved key: \(item.key) in \(path).")
                    }
                } header: {
                    Text(path)
                        // Grouped-list section header: an emphasized caption
                        // (semantic Dynamic-Type style, scales). Tokens.Typography
                        // has no bold-caption role, so kept direct.
                        .font(.caption.weight(.semibold))
                        .accessibilityAddTraits(.isHeader)
                }
            }
        }
        .listStyle(.sidebar)
    }

    // MARK: - Shared states

    private func emptyState(message: String) -> some View {
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

    private func loadingState(message: String) -> some View {
        VStack(spacing: Tokens.Spacing.md) {
            ProgressView()
            Text(message)
                .font(Tokens.Typography.body)
                .foregroundStyle(Tokens.ColorRole.textSecondary)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .accessibilityElement(children: .combine)
        .accessibilityLabel(message)
    }

    private func errorState(message: String) -> some View {
        VStack(alignment: .leading, spacing: Tokens.Spacing.sm) {
            Text("Bibliography couldn't be loaded")
                .font(Tokens.Typography.sectionHeader)
                .accessibilityAddTraits(.isHeader)
            Text(message)
                .font(Tokens.Typography.body)
                .foregroundStyle(Tokens.ColorRole.textSecondary)
        }
        .padding(Tokens.Spacing.lg)
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
        .accessibilityElement(children: .combine)
        .accessibilityLabel("Bibliography couldn't be loaded. \(message)")
    }
}

/// Sheet shown for the "Show files citing this entry" right-click
/// action. Lists every file that contains at least one `[@key]`
/// reference to the entry's key, ordered by vault path. Empty list
/// is announced as a friendly state rather than a blank sheet.
struct FilesCitingSheet: View {
    let paths: [String]
    let onClose: () -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: Tokens.Spacing.md) {
            Text("Files citing this entry")
                .font(Tokens.Typography.sectionHeader)
                .accessibilityAddTraits(.isHeader)

            if paths.isEmpty {
                Text("No files in this vault cite this entry.")
                    .font(Tokens.Typography.body)
                    .foregroundStyle(Tokens.ColorRole.textSecondary)
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .accessibilityLabel("No files in this vault cite this entry.")
            } else {
                List(paths, id: \.self) { path in
                    Text(path)
                        .accessibilityLabel(path)
                }
                .listStyle(.inset)
                .frame(minHeight: 200)
            }

            HStack {
                Spacer()
                Button("Close") { onClose() }
                    .keyboardShortcut(.cancelAction)
            }
        }
        .padding(Tokens.Spacing.lg)
        .frame(minWidth: 380, idealWidth: 460, maxWidth: 600, minHeight: 200, maxHeight: 500)
        .accessibilityElement(children: .contain)
        .accessibilityLabel(
            "Files citing this entry. \(paths.count) \(paths.count == 1 ? "file" : "files")."
        )
    }
}
