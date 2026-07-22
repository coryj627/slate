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

        /// The canonical extension-stripped row label
        /// (`slate_core::switcher::display_name` — the derivation is
        /// ranking vocabulary, shared verbatim with the Windows host).
        /// Stored, not computed: one FFI call at construction; rows
        /// coming back from the ranking already carry it for free.
        let displayName: String

        init(path: String, name: String) {
            self.init(
                path: path,
                name: name,
                displayName: switcherDisplayName(name: name)
            )
        }

        init(path: String, name: String, displayName: String) {
            self.path = path
            self.name = name
            self.displayName = displayName
        }

        /// Row identity for SwiftUI + arrow nav: the vault-relative
        /// path is unique within a vault, so it's the stable id.
        var id: String { path }
    }

    /// All candidate files, set once on `load` (matches the view's
    /// `.onAppear`). Order here is AppState's path-sorted `files`.
    @Published private(set) var files: [FileRow] = []

    /// User's typed query. Bound two-way to the search field.
    @Published var query: String = ""

    /// Currently selected row id (its `path`). Mutated by arrow keys,
    /// hover, and the snap-to-first-on-query-change rule.
    @Published var selectedID: String? = nil

    /// Result-count announcement event. The view posts it at
    /// `.medium` priority whenever the filtered set changes. Mirrors
    /// `CommandPaletteModel.filterAnnouncement`, but it also announces
    /// the empty-query "N recent files" count — the quick switcher's
    /// opening list is recency-ordered and worth announcing, unlike the
    /// palette's static full list. Typed since #963: the model decides
    /// *when* and *which* event fires; core owns the rendered text
    /// (W0.5-3 — the view posts the event, never composes strings).
    @Published private(set) var resultAnnouncement: A11yEvent?

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
        self.ffiFiles = files.map { SwitcherFile(path: $0.path, name: $0.name) }
        self.rankedCacheQuery = nil
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
        let total = UInt32(clamping: rankedTotal())
        if query.isEmpty {
            resultAnnouncement = .switcherRecentCount(count: total)
        } else if total == 0 {
            resultAnnouncement = .switcherNoMatches(query: query)
        } else {
            resultAnnouncement = .switcherMatchCount(count: total, query: query)
        }
    }

    /// Clear the announcement after the view has posted it (tests read
    /// and clear directly).
    func clearAnnouncement() {
        resultAnnouncement = nil
    }

    // MARK: - Ranking (core-owned since W0.5-2 #718)

    /// The file snapshot in FFI shape, built once per `load` so a
    /// keystroke re-rank doesn't rebuild N input records first.
    private var ffiFiles: [SwitcherFile] = []

    /// Bounded ranked-row cache: core ranks once per input change, not
    /// once per property access. It returns only the display page plus
    /// the exact total used by announcements. Keyed by the query
    /// (files/recents invalidate via `load`).
    private var rankedCache: [SwitcherRow] = []
    private var rankedCacheTotal: UInt64 = 0
    private var rankedCacheQuery: String? = nil

    private func refreshRankedCache() {
        if rankedCacheQuery != query {
            let page = switcherRankTop(
                files: ffiFiles,
                query: query,
                recentPaths: recentPaths,
                limit: UInt32(Self.displayCap)
            )
            rankedCache = page.rows
            rankedCacheTotal = page.total
            rankedCacheQuery = query
        }
    }

    /// The ranked display page. Scoring, recency blending, and every
    /// tie-break remain core-owned and identical across both hosts.
    private func rankedRows() -> [SwitcherRow] {
        refreshRankedCache()
        return rankedCache
    }

    private func rankedTotal() -> UInt64 {
        refreshRankedCache()
        return rankedCacheTotal
    }

    /// The bounded ranked display page as `FileRow`s (display names
    /// carried back from core — no per-row FFI).
    var matches: [FileRow] {
        rankedRows().map {
            FileRow(path: $0.path, name: $0.name, displayName: $0.displayName)
        }
    }

    /// The rows the view renders. Arrow nav cycles this exact list so
    /// selection matches the visible rows. Core already bounds the page
    /// to `displayCap`; the prefix is a defensive host-side invariant.
    var displayOrder: [FileRow] {
        rankedRows().prefix(Self.displayCap).map {
            FileRow(path: $0.path, name: $0.name, displayName: $0.displayName)
        }
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
