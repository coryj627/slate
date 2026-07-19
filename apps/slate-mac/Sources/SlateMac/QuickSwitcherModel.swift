// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

/// Testable view-model for the quick switcher (U1-5 follow-up #495).
///
/// Mirrors `CommandPaletteModel`: a `@MainActor ObservableObject` the
/// view binds `@Published` state to, with the filter + selection +
/// announcement logic extracted here so tests can drive it without an
/// `NSHostingController`. The two differences from the palette model
/// are the domain (vault files, not commands) and the ranking
/// (recency-weighted rather than section-grouped).
///
/// Ranking is core-owned since W0.5-2 (#718): scoring (the shared
/// subsequence-with-boost engine, `slate_core::palette::fuzzy_score`,
/// plus the name-over-path bias) and the recency-blended orderings all
/// live in `slate_core::switcher` behind the FFI. This model forwards
/// its snapshot state and keeps only UI concerns — selection,
/// announcements, and the rendered-row display cap.
@MainActor
final class QuickSwitcherModel: ObservableObject {

    /// A single rankable file. `path` is vault-relative (`notes/foo.md`);
    /// `name` is the file's display name WITH extension (`foo.md`), as
    /// `FileSummary` provides it.
    struct FileRow: Identifiable, Equatable {
        let path: String
        let name: String

        /// Row identity for SwiftUI + arrow nav: the vault-relative
        /// path is unique within a vault, so it's the stable id.
        var id: String { path }

        /// The canonical extension-stripped row label, from core
        /// (`slate_core::switcher::display_name` — the derivation is
        /// ranking vocabulary, shared verbatim with the Windows host).
        var displayName: String {
            switcherDisplayName(name: name)
        }
    }

    /// All candidate files, set once on `load` (matches the view's
    /// `.onAppear`). Order here is AppState's path-sorted `files`.
    @Published private(set) var files: [FileRow] = []

    /// User's typed query. Bound two-way to the search field.
    @Published var query: String = ""

    /// Currently selected row id (its `path`). Mutated by arrow keys,
    /// hover, and the snap-to-first-on-query-change rule.
    @Published var selectedID: String? = nil

    /// Result-count announcement string. The view posts it at
    /// `.medium` priority whenever the filtered set changes. Mirrors
    /// `CommandPaletteModel.filterAnnouncement`, but it also announces
    /// the empty-query "N recent files" count — the quick switcher's
    /// opening list is recency-ordered and worth announcing, unlike the
    /// palette's static full list.
    @Published private(set) var resultAnnouncement: String?

    /// Recency order (most-recent-first) of vault-relative paths, set
    /// on `load` from the vault's `FileRecentsStore`. Passed to core's
    /// ranking as data — pruning to still-present files, rank building,
    /// and the tie-break all happen core-side.
    private var recentPaths: [String] = []

    /// Display cap. A huge vault would otherwise render thousands of
    /// rows; List/LazyVStack virtualizes, but capping bounds the work
    /// regardless. The count announcement reflects the TOTAL matches,
    /// with the cap noted only when it actually clips (no SearchOverlay
    /// precedent for a "showing N of M" phrasing — SearchOverlay caps
    /// silently — so we announce the total and render the capped list).
    static let displayCap = 50

    /// Initial-load entry point — called from the view's `.onAppear`.
    /// Idempotent; snaps selection to the first row of the
    /// (empty-query) display order. Recents pass through raw — core
    /// prunes entries whose file no longer exists (spec: filter in the
    /// ranking, don't rewrite the store on every open).
    func load(files: [FileRow], recents: [String]) {
        self.files = files
        self.recentPaths = recents
        self.selectedID = displayOrder.first?.id
    }

    /// Re-run snap-to-first and refresh the result-count announcement.
    /// Bound to the view's `.onChange(of: query)`.
    func handleQueryChange() {
        let order = displayOrder
        if selectedID == nil || !order.contains(where: { $0.id == selectedID }) {
            selectedID = order.first?.id
        }
        refreshAnnouncement()
    }

    /// Announce the opening list too (unlike the palette). Called from
    /// the view's `.onAppear` so a screen-reader user hears how many
    /// files the recency list offers before typing.
    func announceInitialCount() {
        refreshAnnouncement()
    }

    private func refreshAnnouncement() {
        let total = matches.count
        if query.isEmpty {
            resultAnnouncement =
                "\(total) recent file\(total == 1 ? "" : "s")"
        } else if total == 0 {
            resultAnnouncement = "No files matching \"\(query)\""
        } else {
            resultAnnouncement =
                "\(total) file\(total == 1 ? "" : "s") matching \"\(query)\""
        }
    }

    /// Clear the announcement after the view has posted it (tests read
    /// and clear directly).
    func clearAnnouncement() {
        resultAnnouncement = nil
    }

    // MARK: - Ranking (core-owned since W0.5-2 #718)

    /// The full ranked match set BEFORE the display cap, computed by
    /// `slate_core::switcher::switcher_rank` through the FFI — the
    /// name-over-path score bias, recency blending, prune-on-rank, and
    /// every tie-break live core-side so both hosts rank identically.
    /// `matches.count` is what the announcement reports.
    var matches: [FileRow] {
        switcherRank(
            files: files.map { SwitcherFile(path: $0.path, name: $0.name) },
            query: query,
            recentPaths: recentPaths
        )
        .map { FileRow(path: $0.path, name: $0.name) }
    }

    /// The rows the view renders — `matches` clipped to `displayCap`.
    /// Arrow nav cycles this exact list so selection matches the
    /// visible rows. The cap is view virtualization policy and stays
    /// host-side; core returns the full ranked list.
    var displayOrder: [FileRow] {
        Array(matches.prefix(Self.displayCap))
    }

    // MARK: - Selection navigation

    /// Move selection to the next visible row, wrapping at the end.
    func selectNext() {
        let order = displayOrder
        guard !order.isEmpty else { return }
        let idx = order.firstIndex { $0.id == selectedID } ?? -1
        selectedID = order[(idx + 1) % order.count].id
    }

    /// Move selection to the previous visible row, wrapping at the start.
    func selectPrevious() {
        let order = displayOrder
        guard !order.isEmpty else { return }
        let idx = order.firstIndex { $0.id == selectedID } ?? order.count
        selectedID = order[(idx - 1 + order.count) % order.count].id
    }

    /// The currently-selected row, if any — the view resolves this to
    /// route the open.
    var selectedRow: FileRow? {
        guard let id = selectedID else { return nil }
        return displayOrder.first { $0.id == id }
    }
}
