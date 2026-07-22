// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

/// Testable view-model for the quick switcher (U1-5 follow-up #495).
///
/// Ranking is core-owned since W0.5-2 (#718). The main actor owns only
/// snapshots, selection, announcements, and publication. A debounced serial
/// process-scoped background actor constructs the FFI input and calls
/// `switcher_rank_top`, so opening or typing in Quick Open never runs O(N)
/// ranking/FFI work on the UI thread or fans out concurrent native ranks.
@MainActor
final class QuickSwitcherModel: ObservableObject {

    /// A rankable vault file. Display-name derivation intentionally does not
    /// happen here: candidates are created on the main actor, while core
    /// returns the canonical display name from the background ranking pass.
    struct CandidateFile: Equatable, Sendable {
        let path: String
        let name: String
    }

    /// One ranked, display-ready row returned from core.
    struct FileRow: Identifiable, Equatable, Sendable {
        let path: String
        let name: String
        let displayName: String

        var id: String { path }
    }

    /// Host-owned, sendable projection of the UniFFI result. No generated FFI
    /// object crosses back to the main actor.
    struct RankingPage: Equatable, Sendable {
        let rows: [FileRow]
        let total: UInt64
    }

    typealias RankingOperation = @Sendable (
        _ files: [CandidateFile],
        _ query: String,
        _ recentPaths: [String],
        _ limit: UInt32
    ) -> RankingPage

    /// Serial, non-main owner for the synchronous UniFFI call. A cancelled
    /// request that was queued behind another rank is discarded before it can
    /// enter native code, so rapid queries never fan out concurrent ranks.
    actor RankingWorker {
        let operation: RankingOperation

        init(operation: @escaping RankingOperation) {
            self.operation = operation
        }

        func rank(
            files: [CandidateFile],
            query: String,
            recentPaths: [String],
            limit: UInt32
        ) -> RankingPage? {
            guard !Task.isCancelled else { return nil }
            return operation(files, query, recentPaths, limit)
        }
    }

    static let displayCap = 50
    static let rankingDebounceNanoseconds: UInt64 = 60_000_000
    private static let sharedRankingWorker = RankingWorker(operation: performRanking)

    /// All candidate files from the current vault snapshot. The view uses only
    /// emptiness; ranked/display rows live in `displayOrder`.
    @Published private(set) var files: [CandidateFile] = []

    @Published var query: String = "" {
        didSet {
            guard query != oldValue else { return }
            // Property mutation and generation invalidation share one
            // MainActor turn; no older continuation can publish in between.
            scheduleRanking(announceOnPublish: true)
        }
    }
    @Published var selectedID: String? = nil
    @Published private(set) var displayOrder: [FileRow] = []
    @Published private(set) var isRanking = false
    @Published private(set) var resultRevision: UInt64 = 0
    @Published private(set) var resultAnnouncement: A11yEvent?

    private var recentPaths: [String] = []
    private var rankingGeneration: UInt64 = 0
    private var publishedGeneration: UInt64?
    private var publishedTotal: UInt64 = 0
    private var initialAnnouncementGeneration: UInt64?
    private let debounceNanoseconds: UInt64
    private let rankingWorker: RankingWorker
    private let rankingRequestObserverForTesting: (@Sendable () -> Void)?
    private(set) var rankingTaskForTesting: Task<Void, Never>?
    private(set) var supersededRankingTaskForTesting: Task<Void, Never>?
    private(set) var rankingPublicationCountForTesting = 0

    init(
        debounceNanoseconds: UInt64 = QuickSwitcherModel.rankingDebounceNanoseconds,
        rankingWorker: RankingWorker? = nil,
        rankingRequestObserverForTesting: (@Sendable () -> Void)? = nil
    ) {
        self.debounceNanoseconds = debounceNanoseconds
        self.rankingWorker = rankingWorker ?? Self.sharedRankingWorker
        self.rankingRequestObserverForTesting = rankingRequestObserverForTesting
    }

    var rankingWorkerForTesting: RankingWorker { rankingWorker }

    deinit {
        rankingTaskForTesting?.cancel()
    }

    /// Initial-load entry point. Snapshot capture and publication stay on the
    /// main actor; FFI conversion and ranking are scheduled off-main.
    func load(files: [CandidateFile], recents: [String]) {
        self.files = files
        recentPaths = recents
        selectedID = nil
        resultAnnouncement = nil
        initialAnnouncementGeneration = nil
        scheduleRanking(announceOnPublish: false)
    }

    /// Announce the opening list after its asynchronous rank publishes. If the
    /// rank already settled, publish immediately.
    func announceInitialCount() {
        if publishedGeneration == rankingGeneration {
            publishAnnouncement(query: query, total: publishedTotal)
        } else {
            initialAnnouncementGeneration = rankingGeneration
        }
    }

    func clearAnnouncement() {
        resultAnnouncement = nil
    }

    /// Cancel publication when the sheet disappears. A native rank already in
    /// progress may finish, but its generation is no longer allowed to adopt.
    func cancel() {
        rankingGeneration &+= 1
        if let task = rankingTaskForTesting {
            task.cancel()
            supersededRankingTaskForTesting = task
        }
        rankingTaskForTesting = nil
        initialAnnouncementGeneration = nil
        resultAnnouncement = nil
        isRanking = false
    }

    /// Event-driven settle seam for focused tests.
    func settleRanking() async {
        let task = rankingTaskForTesting
        await task?.value
    }

    func settleSupersededRanking() async {
        let task = supersededRankingTaskForTesting
        await task?.value
    }

    /// Core-owned ranking executed by the serial worker. Generated UniFFI
    /// values are constructed and consumed on that worker; only host-owned,
    /// sendable strings and counts cross back to MainActor.
    nonisolated static func performRanking(
        files: [CandidateFile],
        query: String,
        recentPaths: [String],
        limit: UInt32
    ) -> RankingPage {
        let page = switcherRankTop(
            files: files.map { SwitcherFile(path: $0.path, name: $0.name) },
            query: query,
            recentPaths: recentPaths,
            limit: limit
        )
        return RankingPage(
            rows: page.rows.map {
                FileRow(path: $0.path, name: $0.name, displayName: $0.displayName)
            },
            total: page.total
        )
    }

    /// Alias retained for model-level parity tests and callers that consume
    /// the bounded page as matches.
    var matches: [FileRow] { displayOrder }

    // MARK: - Selection navigation

    func selectNext() {
        guard !displayOrder.isEmpty else { return }
        let index = displayOrder.firstIndex { $0.id == selectedID } ?? -1
        selectedID = displayOrder[(index + 1) % displayOrder.count].id
    }

    func selectPrevious() {
        guard !displayOrder.isEmpty else { return }
        let index = displayOrder.firstIndex { $0.id == selectedID } ?? displayOrder.count
        selectedID = displayOrder[(index - 1 + displayOrder.count) % displayOrder.count].id
    }

    var selectedRow: FileRow? {
        guard let selectedID else { return nil }
        return displayOrder.first { $0.id == selectedID }
    }

    // MARK: - Ranking ownership

    private func scheduleRanking(announceOnPublish: Bool) {
        rankingGeneration &+= 1
        let generation = rankingGeneration
        if let task = rankingTaskForTesting {
            task.cancel()
            supersededRankingTaskForTesting = task
        }
        publishedGeneration = nil
        let preferredSelectionID = selectedID
        displayOrder = []
        resultAnnouncement = nil
        isRanking = true

        let files = files
        let query = query
        let recentPaths = recentPaths
        let debounceNanoseconds = debounceNanoseconds
        let rankingWorker = rankingWorker
        let rankingRequestObserverForTesting = rankingRequestObserverForTesting
        let limit = UInt32(Self.displayCap)
        rankingTaskForTesting = Task { @MainActor [weak self] in
            if debounceNanoseconds > 0 {
                do {
                    try await Task.sleep(nanoseconds: debounceNanoseconds)
                } catch {
                    return
                }
            }
            guard !Task.isCancelled else { return }
            rankingRequestObserverForTesting?()
            let page = await rankingWorker.rank(
                files: files,
                query: query,
                recentPaths: recentPaths,
                limit: limit
            )
            guard let self,
                let page,
                !Task.isCancelled,
                self.rankingGeneration == generation
            else { return }

            self.displayOrder = Array(page.rows.prefix(Self.displayCap))
            self.publishedTotal = page.total
            self.publishedGeneration = generation
            let nextSelectionID = preferredSelectionID.flatMap { preferredID in
                self.displayOrder.contains { $0.id == preferredID } ? preferredID : nil
            } ?? self.displayOrder.first?.id
            if self.selectedID != nextSelectionID {
                self.selectedID = nextSelectionID
            }
            self.resultRevision &+= 1
            self.isRanking = false
            self.rankingTaskForTesting = nil
            self.rankingPublicationCountForTesting += 1

            if announceOnPublish || self.initialAnnouncementGeneration == generation {
                self.initialAnnouncementGeneration = nil
                self.publishAnnouncement(query: query, total: page.total)
            }
        }
    }

    private func publishAnnouncement(query: String, total: UInt64) {
        let count = UInt32(clamping: total)
        if query.isEmpty {
            resultAnnouncement = .switcherRecentCount(count: count)
        } else if total == 0 {
            resultAnnouncement = .switcherNoMatches(query: query)
        } else {
            resultAnnouncement = .switcherMatchCount(count: count, query: query)
        }
    }
}
