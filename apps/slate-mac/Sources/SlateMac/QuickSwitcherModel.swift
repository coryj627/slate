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
/// Scoring **reuses** `CommandPaletteModel.fuzzyScore` — the
/// subsequence-with-boost algorithm is single-source there and must
/// not be duplicated. This model only adds the name-over-path bias and
/// the recency ordering on top of it.
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

        /// The name with its markdown extension stripped, for the
        /// primary row label ("foo", not "foo.md"). Only the trailing
        /// `.md`/`.markdown` is removed; a dotted stem like
        /// `2026.01.notes.md` keeps everything but the final extension.
        var displayName: String {
            let lower = name.lowercased()
            for ext in [".md", ".markdown"] where lower.hasSuffix(ext) {
                return String(name.dropLast(ext.count))
            }
            return name
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
    /// on `load` from the vault's `FileRecentsStore`. Drives the
    /// empty-query ordering and the fuzzy tie-break.
    private var recentPaths: [String] = []

    /// Rank of each path in `recentPaths` (0 = most recent). Absent =
    /// never opened. Precomputed on `load` so ordering is O(1) per row.
    private var recencyRank: [String: Int] = [:]

    /// Display cap. A huge vault would otherwise render thousands of
    /// rows; List/LazyVStack virtualizes, but capping bounds the work
    /// regardless. The count announcement reflects the TOTAL matches,
    /// with the cap noted only when it actually clips (no SearchOverlay
    /// precedent for a "showing N of M" phrasing — SearchOverlay caps
    /// silently — so we announce the total and render the capped list).
    static let displayCap = 50

    /// Bonus added to a row's score when the QUERY subsequence-matched
    /// the file's NAME (not merely its full path). Biases `foo` toward
    /// `foo.md` over `notes/foo-archive/bar.md`. Local + documented per
    /// spec; pinned by `QuickSwitcherModelTests`.
    static let nameMatchBonus = 20

    /// Initial-load entry point — called from the view's `.onAppear`.
    /// Idempotent; recomputes the recency map and snaps selection to
    /// the first row of the (empty-query) display order.
    func load(files: [FileRow], recents: [String]) {
        self.files = files
        // Prune recents for files that no longer exist (moved/deleted
        // since last session) at load time — spec: filter in the model,
        // don't rewrite the store on every open.
        let present = Set(files.map(\.path))
        self.recentPaths = recents.filter { present.contains($0) }
        var rank: [String: Int] = [:]
        for (i, path) in recentPaths.enumerated() { rank[path] = i }
        self.recencyRank = rank
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

    // MARK: - Ranking

    /// The full ranked match set BEFORE the display cap. `matches.count`
    /// is what the announcement reports.
    ///
    /// - Empty query: every file, recents first (in recency order),
    ///   then the remaining files in their incoming (path-sorted) order.
    /// - Non-empty query: files whose name or path fuzzy-matches, sorted
    ///   by descending score; ties broken by recency rank, then path.
    ///   Recency only ever breaks ties — a materially better fuzzy score
    ///   always wins (spec).
    var matches: [FileRow] {
        guard !query.isEmpty else {
            let recentSet = Set(recentPaths)
            let recentRows: [FileRow] = recentPaths.compactMap { path in
                files.first(where: { $0.path == path })
            }
            let rest = files.filter { !recentSet.contains($0.path) }
            return recentRows + rest
        }
        return
            files
            .compactMap { row -> (FileRow, Int)? in
                guard let score = Self.score(query: query, row: row) else { return nil }
                return (row, score)
            }
            .sorted { lhs, rhs in
                if lhs.1 != rhs.1 { return lhs.1 > rhs.1 }
                // Tie-break 1: recency (opened files sort ahead of
                // never-opened; `.max` for absent puts them last).
                let lRank = recencyRank[lhs.0.path] ?? Int.max
                let rRank = recencyRank[rhs.0.path] ?? Int.max
                if lRank != rRank { return lRank < rRank }
                // Tie-break 2: path, for a fully deterministic order.
                return lhs.0.path < rhs.0.path
            }
            .map { $0.0 }
    }

    /// The rows the view renders — `matches` clipped to `displayCap`.
    /// Arrow nav cycles this exact list so selection matches the
    /// visible rows.
    var displayOrder: [FileRow] {
        Array(matches.prefix(Self.displayCap))
    }

    /// Score a file for `query`, biased toward name matches. Returns
    /// `nil` when neither the name nor the path subsequence-matches.
    ///
    /// Reuses `CommandPaletteModel.fuzzyScore` (the shared matcher) on
    /// three targets — the extension-stripped name, the full name, and
    /// the full path — and takes the max. When a NAME target (either
    /// form) matched, `nameMatchBonus` is added so a name hit outranks
    /// a same-strength path-only hit. Path-only matches (`dir/foo`)
    /// still score, just below an equivalent name hit.
    nonisolated static func score(query: String, row: FileRow) -> Int? {
        let nameScores = [row.displayName, row.name].compactMap {
            CommandPaletteModel.fuzzyScore(query: query, target: $0)
        }
        let pathScore = CommandPaletteModel.fuzzyScore(query: query, target: row.path)

        let bestName = nameScores.max()
        let candidates: [Int] = [
            bestName.map { $0 + nameMatchBonus },
            pathScore,
        ].compactMap { $0 }
        return candidates.max()
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
