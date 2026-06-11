// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI

/// Sidebar listing of Markdown files in the open vault.
///
/// SwiftUI's `List` is lazy under the hood (NSCollectionView on macOS),
/// so a vault with 10k+ files only renders the visible rows. Selection
/// is single-row; the selected file's path lives on `AppState` so a
/// future content view can react. Arrow keys, Return, and Tab to the
/// next region all work out of the box once the rows have a stable
/// `id` and bind to a selection binding.
struct FileListSidebar: View {
    @EnvironmentObject private var appState: AppState
    @State private var didAnnounceCount = false
    /// Keyboard focus on the file list — gates the #418 selection
    /// announcements to list-driven changes only.
    @FocusState private var fileListFocused: Bool

    var body: some View {
        VStack(spacing: 0) {
            // Thin progress strip that mirrors the scanner's
            // FileIndexed events. The `@ViewBuilder` renders
            // EmptyView when there's no scanProgress, which collapses
            // to no rendered output.
            progressBar
            Group {
                if appState.isScanning && appState.files.isEmpty {
                    scanningState
                } else if let error = appState.scanError {
                    errorState(error)
                } else if appState.files.isEmpty {
                    emptyState
                } else {
                    fileList
                }
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity)

            // Per-note panels live below the file list inside the
            // same sidebar column. They self-hide when no note is
            // selected (returning EmptyView), so they don't push the
            // file list around in the empty case. Order matches the
            // mental model of "what does this note say about itself
            // / link to / get linked from": properties → outgoing →
            // backlinks → tasks. Tasks lands last because users
            // typically scroll to it after reading the note's
            // structural context; the panel is dense (toggleable
            // rows) and benefits from being below the metadata
            // sections that don't require interaction. (Outline
            // lives in the third NavigationSplit column, not here.)
            Divider()
            ScrollView {
                VStack(alignment: .leading, spacing: 0) {
                    PropertiesPanel()
                    OutgoingLinksPanel()
                    BacklinksPanel()
                    EmbedsPanel()
                    // Milestone K surfaces (#410): math / code /
                    // diagram pipelines for the selected note, after
                    // embeds (same "what does this note contain"
                    // family), before tasks (interaction-dense, kept
                    // last).
                    MathBlocksPanel()
                    CodeBlocksPanel()
                    DiagramsPanel()
                    TasksPanel()
                }
            }
            .frame(maxHeight: 400)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .navigationTitle("Files")
        // Two-arg .onChange(of:_:) is macOS 14+; sticking with the
        // one-arg form so the app keeps its declared macOS 13 minimum.
        .onChange(of: appState.isScanning) { scanning in
            // Announce once the scan finishes — at that point `files`
            // has been populated and N items is the count VoiceOver
            // should hear.
            if !scanning && !didAnnounceCount && appState.scanError == nil {
                didAnnounceCount = true
                postAccessibilityAnnouncement(
                    "File list, \(appState.files.count) "
                        + (appState.files.count == 1 ? "item" : "items")
                )
            }
        }
        .onChange(of: appState.currentVaultURL) { _ in
            // Each new vault gets its own count announcement.
            didAnnounceCount = false
        }
    }

    // MARK: - States

    private var scanningState: some View {
        VStack(spacing: 12) {
            ProgressView()
            Text("Scanning vault…")
                .foregroundStyle(.secondary)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .accessibilityElement(children: .combine)
        .accessibilityLabel("Scanning vault. The file list will appear when the scan finishes.")
    }

    private func errorState(_ message: String) -> some View {
        VStack(alignment: .leading, spacing: 8) {
            Text("Could not load files")
                .font(.headline)
                .accessibilityAddTraits(.isHeader)
            Text(message)
                .foregroundStyle(.secondary)
        }
        .padding()
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
    }

    private var emptyState: some View {
        VStack(spacing: 8) {
            Text("No Markdown files in this vault.")
                .foregroundStyle(.secondary)
        }
        .padding()
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .accessibilityLabel("No Markdown files in this vault.")
    }

    private var fileList: some View {
        List(appState.files, id: \.path, selection: $appState.selectedFilePath) { file in
            row(for: file)
        }
        .listStyle(.sidebar)
        .focused($fileListFocused)
        // #418 (F-A1): keyboard selection in the list is silent —
        // VO speaks only side-effect live regions ("Outline, N
        // headings.") and a blind user can't tell which file is
        // selected while arrowing. Announce the selection, but ONLY
        // when the list itself has keyboard focus: programmatic
        // selection changes (search-open's "Opened <file>, line N",
        // template create's "Created <file>…", the dirty-gate
        // rollback) carry their own announcements and must not
        // double-speak. Same "Selected:" phrasing as the command
        // palette.
        .onChange(of: appState.selectedFilePath) { newPath in
            guard fileListFocused, let newPath else { return }
            // Red-team note: the dirty-gate rollback re-sets the
            // selection asynchronously while the "Save changes?"
            // alert presents — announcing the rollback on top of the
            // prompt is chatter the user didn't ask for.
            guard appState.pendingNavigation == nil else { return }
            guard let file = appState.files.first(where: { $0.path == newPath }) else { return }
            postAccessibilityAnnouncement(
                "Selected: \(file.name)",
                priority: .medium
            )
        }
    }

    /// Determinate progress strip rendered above the file list while a
    /// scan is in flight. Returns nil between scans (or once the scan
    /// terminates) so it stays hidden by default.
    ///
    /// We render the bar for `FileIndexed` events; `Started` reports 0
    /// indexed which gives an empty bar (still useful — it lets the
    /// user see the scan kicked off before the first file lands).
    /// `Finished` / `Cancelled` / `Failed` clear `scanProgress` on the
    /// AppState side so this returns nil and the strip disappears.
    @ViewBuilder private var progressBar: some View {
        switch appState.scanProgress {
        case .started(let total):
            scanStrip(
                label: total == 1
                    ? "Scanning vault — 1 file to index."
                    : "Scanning vault — \(total) files to index.",
                progress: total == 0 ? nil : 0,
                total: total
            )
        case .fileIndexed(_, let indexed, let total):
            scanStrip(
                label: total == 0
                    ? "Indexed \(indexed) files."
                    : "Indexed \(indexed) of \(total) files.",
                progress: total == 0 ? nil : Double(indexed) / Double(total),
                total: total
            )
        case .finished, .cancelled, .failed, .none:
            EmptyView()
        case .some:
            // Defensive: future enum variants stay hidden rather than
            // showing a stale strip.
            EmptyView()
        }
    }

    private func scanStrip(label: String, progress: Double?, total: UInt64) -> some View {
        // Indeterminate (`progress == nil`) when we don't yet know the
        // denominator — Started{totalFiles: 0} or a FileIndexed with
        // total == 0. The label still tells the user what's happening.
        HStack(spacing: 8) {
            if let progress {
                ProgressView(value: progress)
                    .progressViewStyle(.linear)
            } else {
                ProgressView()
                    .progressViewStyle(.linear)
            }
            Text(label)
                .font(.caption)
                .foregroundStyle(.secondary)
                // WCAG 1.4.4: no lineLimit(1) — let Dynamic Type wrap.
                .lineLimit(2)
        }
        .padding(.horizontal, 12)
        .padding(.vertical, 6)
        // Combine into one accessible element so VoiceOver reads
        // "<label>" instead of separately announcing the bar.
        .accessibilityElement(children: .combine)
        .accessibilityLabel(label)
        .accessibilityValue(
            progress.map { String(Int($0 * 100)) + " percent" } ?? "Scanning"
        )
    }

    private func row(for file: FileSummary) -> some View {
        // Explicit `.primary` / `.secondary` so the text colors don't
        // fall back to whatever inherited container style happens to
        // be in scope. Xcode's Accessibility Inspector reported
        // contrast failures on these rows with foreground and
        // background colors nearly identical (#100F16 vs #101016) —
        // most likely the inspector sampling antialiased edges on a
        // dark sidebar bg, but pinning the foreground style makes
        // the intent unambiguous either way.
        VStack(alignment: .leading, spacing: 2) {
            Text(file.name)
                .foregroundStyle(.primary)
            Text("Modified \(relativeDate(for: file.mtimeMs))")
                .font(.caption)
                .foregroundStyle(.secondary)
        }
        .accessibilityElement(children: .combine)
        .accessibilityLabel("\(file.name), modified \(relativeDate(for: file.mtimeMs))")
        .help(file.path)
    }

    // MARK: - Helpers

    /// Cached so a vault of 10k rows doesn't allocate 10k formatters.
    /// RelativeDateTimeFormatter is thread-safe for `localizedString`
    /// reads, and we only mutate `unitsStyle` once at init.
    private static let relativeFormatter: RelativeDateTimeFormatter = {
        let formatter = RelativeDateTimeFormatter()
        formatter.unitsStyle = .full
        return formatter
    }()

    private func relativeDate(for mtimeMs: Int64) -> String {
        let date = Date(timeIntervalSince1970: TimeInterval(mtimeMs) / 1000)
        return Self.relativeFormatter.localizedString(for: date, relativeTo: Date())
    }
}
