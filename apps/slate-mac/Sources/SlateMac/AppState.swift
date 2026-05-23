import AppKit
import Combine
import Foundation
import SwiftUI

/// Search-overlay state machine. Lives on `AppState`, drives the
/// `SearchOverlay` view's mode (idle/searching/results/error) and
/// the VoiceOver announcements that go with each transition.
enum SearchState: Equatable {
    /// No query in flight and no results to show. The initial state
    /// when the overlay first opens.
    case idle
    /// A query is in flight. UI shows a "Searching…" placeholder
    /// (announced through the polite live region).
    case searching
    /// Latest search returned `rows` with a pre-rendered audio
    /// summary string (`"Search returned N results"`).
    case results(rows: [QueryHit], summary: String)
    /// SQLite or FFI error — surfaced through the same panel slot
    /// as results so the user notices.
    case error(String)
}

/// Outcome of a single link-activation call. Mirrors the branches in
/// `AppState.openLink(_:)` so tests can assert routing without
/// observing AppKit side effects.
enum LinkActivationOutcome: Equatable {
    case openedInternal(String)
    case unresolved(String)
    case openedExternal(String)
    case externalOpenFailed(String)
}

/// Per-incident snapshot for the conflict-resolution alert.
///
/// Populated by `saveCurrentNote` when the backend returns
/// `WriteConflict`. Carries everything the resolution actions need:
/// the bytes the user tried to save (so "Keep mine" can re-issue the
/// save), the on-disk hash the alert reports, and the original
/// `expectedContentHash` (kept for telemetry — testers can confirm
/// the conflict was caught because the file moved, not because the
/// editor's hash tracking was wrong).
struct SaveConflict: Equatable {
    let path: String
    let attemptedContents: String
    let currentContentHash: String
    let expectedContentHash: String
    let currentMtimeMs: Int64
}

/// Destination the user asked to navigate to while the editor was
/// dirty. Held in `AppState.pendingNavigation` until the user
/// responds to the "Save changes?" alert.
enum PendingNavigation: Equatable {
    case closeVault
    case selectFile(String?)
}

/// Top-level app state.
///
/// Owns the currently-open `VaultSession` (or none, on the welcome
/// screen) and the most-recent error surfaced from opening one. The
/// session is held until `closeVault()` is called or another vault is
/// opened. uniffi gives us back a reference-counted `VaultSession`, so
/// storing it on the main-thread state object is enough — the Rust
/// side keeps the SQLite connection alive as long as we hold a
/// reference.
@MainActor
final class AppState: ObservableObject {
    @Published private(set) var currentSession: VaultSession?
    @Published private(set) var currentVaultURL: URL?
    @Published private(set) var recentVaults: [RecentVault] = []
    @Published var lastError: String?
    /// Set when an attempt to open a recent-vaults entry fails because
    /// the path no longer exists. WelcomeView reads this to drive the
    /// "missing vault, remove from recent?" confirmation alert.
    @Published var missingRecentVault: RecentVault?
    /// Markdown files in the currently-open vault, sorted by relative
    /// path (case-insensitive). Populated by `loadFiles()` after the
    /// scanner finishes; empty while no vault is open or while the
    /// initial scan is still running.
    @Published private(set) var files: [FileSummary] = []
    /// True while the initial scan + file load is in progress for the
    /// current vault. Sidebar uses this to show a progress indicator.
    @Published private(set) var isScanning: Bool = false
    /// Latest progress event from the scanner. Updated unconditionally
    /// for every `Started`/`FileIndexed`/`Finished` so the sidebar's
    /// progress bar can stay current; cleared back to `nil` on
    /// terminal events (Finished/Cancelled/Failed) so the bar hides.
    @Published private(set) var scanProgress: ScanProgress?
    /// Surfaced when scanning or listing fails. Independent of
    /// `lastError` (which guards the open path).
    @Published var scanError: String?
    /// Path of the file currently selected in the sidebar, if any.
    /// `NoteContentView` reads the corresponding bytes via
    /// `currentNoteText`; AppState watches this property and kicks
    /// off the load whenever it changes.
    @Published var selectedFilePath: String?

    /// UTF-8 text of the currently-selected note. Nil while no note
    /// is selected or while loading is in flight. Writable so the
    /// editor's two-way `Binding<String>` can update the buffer
    /// directly via `updateEditorText(_:)`.
    @Published var currentNoteText: String?
    /// The last-saved (or freshly-loaded) version of the current
    /// note. `hasUnsavedChanges` is derived from
    /// `currentNoteText != savedBaselineText`. Stored separately so
    /// the editor can compare without losing the live buffer.
    @Published private(set) var savedBaselineText: String?
    /// blake3 hex digest of the file's content at the moment we
    /// loaded it (or last saved it). Used as
    /// `expectedContentHash` in `saveCurrentNote` so an external
    /// writer is caught via `WriteConflict`.
    @Published private(set) var currentNoteContentHash: String?
    /// Path of the note whose text+hash we last successfully loaded.
    /// Distinct from `selectedFilePath`: the latter reflects the
    /// user's UI intent (and the file-list selection), while
    /// `loadedFilePath` is what's actually in `currentNoteText`. The
    /// two diverge while a dirty save-changes prompt is open.
    @Published private(set) var loadedFilePath: String?
    /// True when the editor buffer differs from the on-disk
    /// baseline. Drives the toolbar indicator, Cmd+S enablement,
    /// and the save-or-discard prompts triggered by navigation
    /// while dirty.
    @Published private(set) var hasUnsavedChanges: Bool = false
    /// Populated when `saveCurrentNote` returns `WriteConflict`.
    /// Drives the "Keep mine / Reload from disk / Cancel" alert in
    /// `MainSplitView`.
    @Published var currentSaveConflict: SaveConflict?
    /// Set when the user requests navigation (close-vault, switch
    /// file) while `hasUnsavedChanges == true`. Drives the
    /// "Save changes?" prompt. Nil otherwise.
    @Published var pendingNavigation: PendingNavigation?
    /// Surfaced when `saveCurrentNote` fails with anything other
    /// than `WriteConflict` (which goes through `currentSaveConflict`
    /// instead). Independent of `noteLoadError` so a load alert
    /// doesn't shadow a save alert.
    @Published var saveError: String?
    /// True while a save is in flight. Disables Cmd+S to keep the
    /// user from queuing overlapping saves.
    @Published private(set) var isSaving: Bool = false
    /// Handle on the in-flight save task. Exposed (internal) so
    /// tests can `await state.saveTask?.value`.
    private(set) var saveTask: Task<Void, Never>?
    /// Parsed Markdown headings of the currently-selected note, in
    /// document order. Empty while no note is selected (or when the
    /// note has no `#` headings).
    @Published private(set) var currentNoteHeadings: [Heading] = []
    /// True while the selected note's content is being read from disk.
    @Published private(set) var isLoadingNote: Bool = false
    /// Surfaced when reading the selected note fails. Independent of
    /// `lastError` (open path) and `scanError` (indexing path) so the
    /// UI alerts don't cross-fire.
    @Published var noteLoadError: String?

    /// Last outcome from `openLink` / `openBacklink`. Exposed for
    /// tests so they can verify activation routing without observing
    /// AppKit side effects (NSWorkspace open, accessibility
    /// announcements). UI doesn't read this.
    private(set) var lastActivatedLinkOutcome: LinkActivationOutcome?

    /// Last line scrolled to via `openSearchResult`. Same role as
    /// `lastActivatedLinkOutcome` — verifiable in tests without
    /// observing AppKit side effects.
    private(set) var lastActivatedSearchResultLine: Int?
    private(set) var lastActivatedSearchResultPath: String?

    /// Frontmatter properties of the currently-selected note, in
    /// document order. Empty while no note is selected, while the
    /// fetch is still in flight, or when the note has no frontmatter.
    /// Loaded by `loadCurrentLinks(path:)` (which already runs a
    /// `get_file_metadata` under the SQLite mutex) so we don't pay
    /// for two trips through the lock per selection.
    @Published private(set) var currentNoteProperties: [Property] = []

    /// Inbound links to the currently-selected note. Updated whenever
    /// `selectedFilePath` changes. Empty while no note is selected or
    /// while the query is still in flight.
    @Published private(set) var currentBacklinks: [Backlink] = []
    /// Outgoing links from the currently-selected note — resolved,
    /// unresolved, and external in document order. Same lifecycle as
    /// `currentBacklinks`.
    @Published private(set) var currentOutgoingLinks: [OutgoingLink] = []
    /// True while a backlinks/outgoing fetch is in flight. The panels
    /// use this to decide whether to show a `ProgressView`.
    @Published private(set) var isLoadingLinks: Bool = false
    /// Surfaced when the link queries fail (rare — would mean SQLite
    /// itself errored). Independent of `noteLoadError` so the panels'
    /// alert state doesn't cross-fire with the content pane's.
    @Published var linksLoadError: String?

    /// True while the search overlay is visible. Cmd+F toggles;
    /// Esc clears.
    @Published var isSearchOpen: Bool = false
    /// Live search query — bound to the overlay's TextField. Every
    /// edit feeds the debouncer; the actual search fires ~150 ms
    /// after the user stops typing.
    @Published var searchQuery: String = ""
    /// Current state of the search overlay's results panel.
    @Published private(set) var searchState: SearchState = .idle
    /// Pre-rendered audio summary for the live region. Mirrors
    /// `searchState`'s results.summary so the SwiftUI .onChange
    /// observer can fire a polite announcement.
    @Published private(set) var searchSummary: String = ""

    /// One-shot channel for "scroll the content pane to this heading
    /// anchor." `OutlineSidebar` sends; `NoteContentView` subscribes
    /// via `.onReceive`. PassthroughSubject (not @Published) so
    /// repeated clicks on the same heading re-trigger the scroll
    /// without needing a counter.
    let scrollAnchorRequest = PassthroughSubject<String, Never>()

    /// One-shot channel for "scroll to line N." Search-result
    /// activation (#59) sends a 1-based line number; the content
    /// pane's `.onReceive` resolves to the `line-<N>` anchor.
    let lineScrollRequest = PassthroughSubject<Int, Never>()

    /// Live wire for the search-text debouncer. Every keystroke
    /// pushes the latest query string; the Combine pipeline waits
    /// 150 ms of inactivity before kicking off
    /// `runSearch(query:)`. Cancellation of any in-flight search
    /// happens at the head of each run so we don't pile up
    /// background queries.
    private let searchQuerySubject = PassthroughSubject<String, Never>()
    /// Handle on the in-flight search task. Exposed (internal) so
    /// tests can `await state.searchTask?.value` to deterministically
    /// observe the post-search state.
    private(set) var searchTask: Task<Void, Never>?
    private var searchCancelToken: CancelToken?

    /// Handle on the in-flight scan + list task kicked off by
    /// `openVault`. Exposed (internal, not Published) so tests can
    /// `await state.scanTask?.value` to deterministically observe the
    /// post-scan state.
    private(set) var scanTask: Task<Void, Never>?

    /// Handle on the in-flight note-load task. Same shape as
    /// `scanTask` so tests can await deterministically.
    private(set) var noteLoadTask: Task<Void, Never>?

    /// Handle on the in-flight links-fetch task. Held separately from
    /// `noteLoadTask` so the panels can stay responsive while the
    /// note content loads, and so tests can await each path
    /// independently.
    private(set) var linksLoadTask: Task<Void, Never>?

    /// Total announcements fired since the most recent vault was
    /// opened. Internal so the test target can verify the rate-guard
    /// keeps things <= 3/s; the UI never reads it.
    private(set) var scanAnnouncementCount: Int = 0
    /// Most recent message passed to `postAccessibilityAnnouncement`.
    /// Same role as `scanAnnouncementCount` — for tests only.
    private(set) var scanAnnouncementLastMessage: String?

    /// Time source for rate-limiting scan announcements. Injectable so
    /// tests can advance simulated time without sleeping.
    var scanClock: () -> Date = { Date() }
    /// Minimum gap between throttled scan announcements. The
    /// acceptance criteria say "no more than ~3 per second" so 350 ms
    /// gives us 2–3 announcements per real-world second with a little
    /// headroom for the synchronous overhead of posting through
    /// AppKit's accessibility bus.
    private let scanAnnouncementMinInterval: TimeInterval = 0.350
    private var scanAnnouncementLastFiredAt: Date = .distantPast

    private var subscriptions: Set<AnyCancellable> = []
    private let recentsStore: RecentVaultsStore

    init(recentsStore: RecentVaultsStore? = nil) {
        // Fall back to an in-memory-only store (writes go to a temp
        // path that's discarded on exit) if the standard Application
        // Support location can't be set up. Better degraded than crash
        // on launch.
        if let store = recentsStore {
            self.recentsStore = store
        } else if let store = try? RecentVaultsStore() {
            self.recentsStore = store
        } else {
            let fallback = FileManager.default.temporaryDirectory
                .appendingPathComponent("slate-recent-vaults-fallback.json")
            self.recentsStore = RecentVaultsStore(fileURL: fallback)
        }
        self.recentVaults = self.recentsStore.load()

        // Watch `selectedFilePath` and (re)trigger note loading on
        // every change. Combine's removeDuplicates avoids reloading
        // when the same path is rebound (e.g. by SwiftUI list
        // diffing). The closure runs on the main actor since the
        // class is @MainActor.
        $selectedFilePath
            .removeDuplicates()
            .sink { [weak self] path in
                self?.handleSelectionChange(to: path)
            }
            .store(in: &subscriptions)

        // Search query debouncer. 150 ms matches the acceptance
        // criteria and is short enough that typing feels live but
        // long enough that fast typists don't fire one query per
        // keystroke.
        searchQuerySubject
            .debounce(for: .milliseconds(150), scheduler: DispatchQueue.main)
            .removeDuplicates()
            .sink { [weak self] query in
                self?.runSearch(query: query)
            }
            .store(in: &subscriptions)
    }

    /// Push the current `searchQuery` through the debouncer. Called
    /// from the SwiftUI TextField's `.onChange` so the UI doesn't
    /// have to know about the subject.
    func bumpSearchQuery() {
        searchQuerySubject.send(searchQuery)
    }

    /// Toggle the search overlay open / closed. When opening, the
    /// query is preserved so re-opening with a previous query lands
    /// the user back at the same results. When closing, we cancel
    /// the in-flight query so the worker doesn't keep churning
    /// after the user has moved on.
    func toggleSearchOverlay() {
        if isSearchOpen {
            closeSearchOverlay()
        } else {
            isSearchOpen = true
        }
    }

    /// Close the overlay and cancel any in-flight search. Keep
    /// `searchQuery` so a Cmd+F → Esc → Cmd+F round trip lands
    /// back where the user was.
    func closeSearchOverlay() {
        isSearchOpen = false
        cancelInFlightSearch()
        searchState = .idle
        searchSummary = ""
    }

    /// Cancel any currently-running search task. Safe to call when
    /// nothing's in flight.
    private func cancelInFlightSearch() {
        searchCancelToken?.cancel()
        searchCancelToken = nil
        searchTask?.cancel()
        searchTask = nil
    }

    /// Kick off a fresh search. Called from the debouncer; callers
    /// shouldn't invoke directly.
    private func runSearch(query: String) {
        let trimmed = query.trimmingCharacters(in: .whitespaces)
        if trimmed.isEmpty {
            cancelInFlightSearch()
            searchState = .idle
            searchSummary = ""
            return
        }
        guard let session = currentSession else {
            // No vault open → surface a benign placeholder rather
            // than an error toast.
            searchState = .idle
            return
        }
        // Tear down the previous query before starting a new one.
        cancelInFlightSearch()
        searchState = .searching
        let cancel = CancelToken()
        searchCancelToken = cancel
        // Explicit `@MainActor` on the Task body so the
        // post-await @Published writes are guaranteed to run on
        // the main thread (Codoki PR 86 callout). Without this the
        // awaiting Task could resume on whatever cooperative-pool
        // thread the inner detached task ran on, and SwiftUI
        // throws a runtime warning when @Published changes off-main.
        searchTask = Task { @MainActor [weak self] in
            let outcome: Result<QueryResultSet, VaultError> =
                await Task.detached(priority: .userInitiated) {
                    do {
                        let rs = try session.fullTextSearch(
                            query: trimmed,
                            scope: .vault,
                            cancel: cancel
                        )
                        return .success(rs)
                    } catch let error as VaultError {
                        return .failure(error)
                    } catch {
                        return .failure(.Io(message: error.localizedDescription))
                    }
                }
                .value

            // Drop late results: another query may have taken over
            // while this one was in flight; tossing the result keeps
            // the overlay's panel coherent with the latest typed
            // query.
            guard let self else { return }
            if Task.isCancelled || self.searchCancelToken !== cancel {
                return
            }
            switch outcome {
            case .success(let rs):
                self.searchState = .results(rows: rs.rows, summary: rs.summary)
                self.searchSummary = rs.summary
            case .failure(let error):
                if case .Cancelled = error {
                    // Cancellation is a normal user action — keep
                    // whatever the panel was showing before.
                    return
                }
                let message = self.humanReadable(error)
                self.searchState = .error(message)
                self.searchSummary = "Search error: \(message)"
            }
        }
    }

    /// Internal hook from the selectedFilePath subscription. Cancels
    /// any in-flight note load and kicks off a fresh one — or clears
    /// content if `path` is nil.
    private func handleSelectionChange(to path: String?) {
        // Same file re-selected → no-op. This guard matters
        // because the dirty-state rollback below writes
        // `selectedFilePath = loadedFilePath` to re-highlight the
        // unsaved file in the sidebar, which re-triggers this
        // subscription with `path == loadedFilePath`. Without the
        // guard, the rollback would clear and reload the file the
        // user is still editing, blowing away the dirty buffer
        // we're trying to preserve.
        if let loaded = loadedFilePath, path == loaded {
            return
        }
        // Dirty-state gate (issue #63): switching files while the
        // editor has unsaved changes must not silently drop the
        // user's edits. Park the requested destination in
        // `pendingNavigation` and let the "Save changes?" alert
        // route the actual transition.
        if hasUnsavedChanges, path != loadedFilePath {
            pendingNavigation = .selectFile(path)
            // Roll the selection back so the file list re-highlights
            // the dirty file while the alert is up. The async hop is
            // required because we're inside the `$selectedFilePath`
            // willSet/sink chain: a synchronous write here would be
            // overwritten by the outer assignment once the willSet
            // returns. Dispatching to the next main-loop tick lets
            // the outer write finish, then our rollback takes
            // effect; the same-file guard at the top of this method
            // short-circuits the re-entry triggered by the rollback.
            if let loaded = loadedFilePath {
                DispatchQueue.main.async { [weak self] in
                    guard let self else { return }
                    if self.selectedFilePath != loaded {
                        self.selectedFilePath = loaded
                    }
                }
            }
            return
        }
        noteLoadTask?.cancel()
        noteLoadTask = nil
        linksLoadTask?.cancel()
        linksLoadTask = nil
        currentNoteText = nil
        savedBaselineText = nil
        currentNoteContentHash = nil
        loadedFilePath = nil
        hasUnsavedChanges = false
        currentSaveConflict = nil
        saveError = nil
        currentNoteHeadings = []
        noteLoadError = nil
        linksLoadError = nil
        guard let path else {
            // Full clear when nothing is selected. Safe here because
            // there's no destination note to attribute stale content
            // to — and `closeVault` / explicit deselect callers expect
            // the panels to drop their contents synchronously.
            currentBacklinks = []
            currentOutgoingLinks = []
            currentNoteProperties = []
            isLoadingNote = false
            isLoadingLinks = false
            return
        }
        // Note-to-note transitions: leave `currentBacklinks`,
        // `currentOutgoingLinks`, and `currentNoteProperties` holding
        // the previous selection's values until the new load resolves.
        // The previous shape cleared them synchronously, which made
        // the PropertiesPanel render `EmptyView` for the duration of
        // the load — a visible flicker for sighted users and a
        // disappear/reappear of the "Properties, N items" rotor item
        // for VoiceOver on every selection change (#90). The
        // race-cancel guard in `loadCurrentLinks` (selectedFilePath ==
        // path) ensures the newer task's writes win, so the user only
        // sees stale content for the duration of the IO — typically
        // a few milliseconds.
        noteLoadTask = Task { [weak self] in
            await self?.loadCurrentNote(path: path)
        }
        linksLoadTask = Task { [weak self] in
            await self?.loadCurrentLinks(path: path)
        }
    }

    /// Ask the content pane to scroll to the given heading anchor.
    /// Sent by `OutlineSidebar` rows; `NoteContentView` subscribes via
    /// `onReceive(scrollAnchorRequest)`.
    func requestScrollToHeading(anchor: String) {
        scrollAnchorRequest.send(anchor)
    }

    /// Activate a search result row from `SearchOverlay`.
    ///
    /// 1. Set `selectedFilePath` (which kicks off the regular note-
    ///    load observer).
    /// 2. Close the overlay so the content area gets focus.
    /// 3. Await the in-flight note-load so the per-line anchors are
    ///    in the rendered tree before we ask `ScrollViewReader` to
    ///    target one.
    /// 4. Send the line-scroll request.
    /// 5. Post a polite announcement with filename + line +
    ///    snippet — matches the acceptance criteria's
    ///    `"Opened <filename>, line N: <snippet>"`.
    ///
    /// If the path is the SAME file already open, we skip the
    /// selection assignment (the load observer wouldn't re-trigger
    /// the load anyway because of `.removeDuplicates()`) but still
    /// scroll and announce.
    func openSearchResult(_ hit: QueryHit) {
        let cleanSnippet = hit.snippet
            .replacingOccurrences(of: "\u{2}", with: "")
            .replacingOccurrences(of: "\u{3}", with: "")
        let filename = (hit.path as NSString).lastPathComponent
        // Capture the query that produced this hit ahead of the
        // await — by the time the load resolves the user may have
        // edited the field, but the line we want to scroll to is
        // the one matching the query that was active when they
        // pressed Return on this row.
        let queryForLineLookup = searchQuery

        // Close the overlay first so focus moves cleanly back to
        // the content area before the file load completes.
        closeSearchOverlay()

        let wasAlreadyOpen = selectedFilePath == hit.path
        if !wasAlreadyOpen {
            selectedFilePath = hit.path
        }
        // Snapshot the in-flight load up front so the Task closure
        // doesn't have to reach back through `self?.` before its
        // strong-unwrap guard — that pre-guard `self?` access was
        // tripping Codoki's weak-self lint on PR 98 even though the
        // post-await `guard let self` correctly shadow-unwraps. The
        // Task reference outlives `self` cleanly if AppState
        // dealloc's during scheduling; we just await whatever load
        // was pending.
        let pendingLoad = noteLoadTask

        Task { @MainActor [weak self] in
            // Wait for any in-flight note load to finish so the
            // per-line anchors exist in the rendered tree before
            // we ask `ScrollViewReader.scrollTo` to target one.
            // For the same-file case the snapshot is nil and this
            // await is skipped.
            if let pendingLoad {
                await pendingLoad.value
            }
            guard let self else { return }
            // A subsequent selection change (the user moved to a
            // different file while we were waiting) cancels this
            // scroll — sending into the subject would land on the
            // wrong file's anchors.
            guard self.selectedFilePath == hit.path else { return }
            // Derive the line UI-side from the loaded body. Up
            // through PR 94 this came back on the QueryHit, but
            // computing it Rust-side meant pulling `body_text`
            // through SQLite for every hit (#92 item 1). The body
            // is loaded anyway by the time we get here, so we
            // tokenize the original query and scan for the first
            // match — same heuristic the Rust side used.
            let body = self.currentNoteText ?? ""
            let line = firstTokenLineNumber(in: body, query: queryForLineLookup)
            self.lineScrollRequest.send(line)
            postAccessibilityAnnouncement(
                "Opened \(filename), line \(line): \(cleanSnippet)"
            )
            self.lastActivatedSearchResultLine = line
            self.lastActivatedSearchResultPath = hit.path
        }
    }

    /// Activate an outgoing-link row from the OutgoingLinksPanel.
    ///
    /// - Resolved internal: navigate to the target and announce the
    ///   filename. (The subsequent "Showing <filename>" announcement
    ///   from `NoteContentView.onAppear` rounds out the audio
    ///   feedback once the content has actually loaded.)
    /// - Unresolved internal: don't navigate; announce that we
    ///   couldn't open it so a screen-reader user doesn't think the
    ///   click was a no-op.
    /// - External: hand off to NSWorkspace; announce that the browser
    ///   was invoked.
    ///
    /// The branch chosen is also reflected in `lastActivatedLinkOutcome`
    /// so tests can verify behaviour without observing UIKit/AppKit
    /// side effects.
    func openLink(_ link: OutgoingLink) {
        if link.isExternal {
            // Allowlist the schemes we hand to LaunchServices. The
            // link parser flags `file:`, `javascript:`, and custom
            // schemes as external too, but blindly passing them to
            // NSWorkspace.open would let a typo in a markdown link
            // hand control of the user's machine to whatever app
            // happens to be registered for that scheme. http/https
            // (web pages) and mailto (compose new email) are the
            // schemes a notes app's "external link" feature is
            // expected to handle.
            guard let url = URL(string: link.targetRaw),
                let scheme = url.scheme?.lowercased(),
                ["http", "https", "mailto"].contains(scheme)
            else {
                postAccessibilityAnnouncement(
                    "Cannot open external link \(link.targetRaw). "
                        + "Only web and mail links are supported."
                )
                lastActivatedLinkOutcome = .externalOpenFailed(link.targetRaw)
                return
            }
            if NSWorkspace.shared.open(url) {
                postAccessibilityAnnouncement(
                    "Opened external link in default browser."
                )
                lastActivatedLinkOutcome = .openedExternal(link.targetRaw)
            } else {
                postAccessibilityAnnouncement(
                    "Could not open external link \(link.targetRaw)."
                )
                lastActivatedLinkOutcome = .externalOpenFailed(link.targetRaw)
            }
            return
        }
        if link.isUnresolved {
            postAccessibilityAnnouncement(
                "\(link.targetRaw) is unresolved. Cannot open."
            )
            lastActivatedLinkOutcome = .unresolved(link.targetRaw)
            return
        }
        guard let path = link.targetPath else {
            // Defensive: a non-external, non-unresolved row should
            // always carry a target_path. Treat the impossible case
            // as unresolved so the user gets feedback instead of
            // silence.
            postAccessibilityAnnouncement(
                "\(link.targetRaw) is unresolved. Cannot open."
            )
            lastActivatedLinkOutcome = .unresolved(link.targetRaw)
            return
        }
        navigate(to: path, kind: "Opened")
    }

    /// Activate a backlink row from the BacklinksPanel — navigates
    /// to the source file that linked here. Backlinks are always
    /// resolved (the query joins on resolved target_path), so this
    /// is the simple `navigate(to:)` path.
    func openBacklink(_ backlink: Backlink) {
        navigate(to: backlink.sourcePath, kind: "Opened backlink to")
    }

    /// Shared post-activation step: update `selectedFilePath` (which
    /// the file-list selection binding + the note-load observer both
    /// pick up) and post an immediate audio confirmation so the user
    /// hears that the click worked before the content load finishes.
    private func navigate(to path: String, kind: String) {
        selectedFilePath = path
        let filename = (path as NSString).lastPathComponent
        postAccessibilityAnnouncement("\(kind) \(filename).")
        lastActivatedLinkOutcome = .openedInternal(path)
    }

    var isVaultOpen: Bool { currentSession != nil }

    func openVault(at url: URL) {
        do {
            let session = try VaultSession.openFilesystem(rootPath: url.path)
            currentSession = session
            currentVaultURL = url
            lastError = nil
            // Reset file-list state so the previous vault's contents
            // don't briefly flash in the new vault's sidebar.
            files = []
            scanError = nil
            selectedFilePath = nil
            scanProgress = nil
            scanAnnouncementCount = 0
            scanAnnouncementLastMessage = nil
            scanAnnouncementLastFiredAt = .distantPast
            recordOpened(url: url)
            scanTask?.cancel()
            scanTask = Task { [weak self] in
                await self?.loadFiles()
            }
        } catch let error as VaultError {
            currentSession = nil
            currentVaultURL = nil
            lastError = humanReadable(error)
        } catch {
            currentSession = nil
            currentVaultURL = nil
            lastError = error.localizedDescription
        }
    }

    /// Open a recent-vaults entry. If the path no longer exists on
    /// disk, do *not* try to open it (which would either fail with
    /// InvalidPath or, on older bugs, silently materialize a vault) —
    /// instead surface the entry through `missingRecentVault` so the
    /// UI can offer removal.
    func openRecent(_ entry: RecentVault) {
        let url = URL(fileURLWithPath: entry.path)
        var isDir: ObjCBool = false
        let exists = FileManager.default.fileExists(atPath: url.path, isDirectory: &isDir)
        if !exists || !isDir.boolValue {
            missingRecentVault = entry
            return
        }
        openVault(at: url)
    }

    /// Show the directory picker and, if the user chose a folder, open
    /// it as a vault. Centralizes the flow so the WelcomeView button
    /// and the App-level Cmd+O command share the same code path.
    ///
    /// `@MainActor` is redundant given the class-level annotation but
    /// is repeated here for self-documenting clarity: this method
    /// presents an `NSOpenPanel`, which AppKit requires on the main
    /// thread.
    @MainActor
    func pickAndOpenVault() {
        guard let url = VaultPicker.pick() else { return }
        openVault(at: url)
    }

    func closeVault() {
        scanTask?.cancel()
        scanTask = nil
        noteLoadTask?.cancel()
        noteLoadTask = nil
        linksLoadTask?.cancel()
        linksLoadTask = nil
        closeSearchOverlay()
        searchQuery = ""
        currentSession = nil
        currentVaultURL = nil
        files = []
        scanError = nil
        selectedFilePath = nil
        currentNoteText = nil
        currentNoteHeadings = []
        noteLoadError = nil
        currentBacklinks = []
        currentOutgoingLinks = []
        currentNoteProperties = []
        linksLoadError = nil
        isScanning = false
        isLoadingNote = false
        isLoadingLinks = false
        scanProgress = nil
    }

    /// Drop a recent-vaults entry by path. Used by the welcome screen
    /// when the user confirms removal of a missing vault.
    func removeRecent(path: String) {
        do {
            recentVaults = try recentsStore.remove(path: path)
        } catch {
            // Recents persistence isn't critical to app function — the
            // in-memory list is what the UI reads. Log so the failure
            // isn't completely silent during dev, but don't surface to
            // the user; they're already mid-flow on removing an entry.
            fputs(
                "Slate: failed to persist recent-vaults removal: \(error)\n",
                stderr
            )
        }
    }

    /// Run the initial scan against the current session, then page
    /// through `listFiles` to build the sidebar's in-memory list.
    /// Called automatically after `openVault` succeeds; can be called
    /// again later (e.g. a refresh action) once we have one.
    ///
    /// Idempotent: the Rust scanner upserts on path so re-running on
    /// an already-indexed vault is fine.
    ///
    /// Honors `Task.isCancelled`: closing the vault or opening a
    /// different one cancels the wrapping task, which (a) signals the
    /// in-flight `scan_initial` via the `CancelToken` so the Rust side
    /// bails at the next per-entry cancel check, and (b) suppresses
    /// the post-scan publish so a late completion can't repopulate
    /// `files` after the user has already moved on.
    func loadFiles() async {
        guard let session = currentSession else { return }
        isScanning = true
        scanError = nil
        defer { isScanning = false }

        let cancel = CancelToken()
        // Adapter bridges scanner-thread `onProgress` callbacks back
        // to the main actor where AppState can publish them. Holding a
        // strong reference here is enough to keep the FFI handle live
        // for the duration of the scan; uniffi releases it when the
        // last Swift reference goes away.
        // The Task closure needs its own explicit `[weak self]` so
        // strict-concurrency mode (Swift 6 / sendability checks) can
        // verify the capture is sendable across the @Sendable boundary
        // — without it, the implicit re-capture of `self?` from the
        // outer closure trips a "reference to captured var 'self' in
        // concurrently-executing code" diagnostic on the CI toolchain.
        let adapter = ScanProgressAdapter { [weak self] event in
            Task { @MainActor [weak self] in
                self?.handleScanProgress(event)
            }
        }

        do {
            let loaded: [FileSummary] = try await withTaskCancellationHandler {
                try Task.checkCancellation()
                // Scan + list both go through SQLite under a Mutex, so
                // dispatching off the main actor keeps the UI responsive
                // on multi-thousand-file vaults.
                return try await Task.detached(priority: .userInitiated) {
                    _ = try session.scanInitialWithProgress(
                        cancel: cancel,
                        listener: adapter
                    )
                    var all: [FileSummary] = []
                    var cursor: String? = nil
                    repeat {
                        let page = try session.listFiles(
                            filter: .markdownOnly,
                            paging: Paging(cursor: cursor, limit: 1_000)
                        )
                        all.append(contentsOf: page.items)
                        cursor = page.nextCursor
                    } while cursor != nil
                    return all
                }.value
            } onCancel: {
                // Bridge structured-concurrency cancellation across to
                // the CancelToken the Rust scanner is polling.
                cancel.cancel()
            }

            // If we were cancelled mid-flight (e.g. closeVault fired
            // between the detached task starting and finishing), don't
            // overwrite the freshly-cleared state with stale results.
            guard !Task.isCancelled else { return }
            files = loaded.sorted { lhs, rhs in
                lhs.path.localizedCaseInsensitiveCompare(rhs.path) == .orderedAscending
            }
        } catch is CancellationError {
            // Cancellation isn't an error condition the user needs to
            // see; the new vault flow will start its own scan.
        } catch let error as VaultError {
            // Cancelled scans surface from Rust as VaultError.Cancelled
            // — also intentionally non-user-visible.
            if case .Cancelled = error { return }
            guard !Task.isCancelled else { return }
            scanError = humanReadable(error)
        } catch {
            guard !Task.isCancelled else { return }
            scanError = error.localizedDescription
        }
    }

    /// Read the selected note's bytes + indexed headings off the main
    /// actor and publish both. Surfaces InvalidUtf8 / FileTooLarge / IO
    /// via `noteLoadError`. Honors task cancellation so a fast click-
    /// through doesn't leave a stale string in `currentNoteText` or a
    /// stale outline.
    ///
    /// Both calls go in the same detached task: `read_text` and
    /// `get_file_metadata` each take the same SQLite mutex on the Rust
    /// side, so serializing them at the call site keeps the lock-
    /// contention picture predictable and ensures the text + outline
    /// we publish are from the same observation.
    func loadCurrentNote(path: String) async {
        guard let session = currentSession else { return }
        isLoadingNote = true

        // Capture (text, headings, contentHash) in one detached
        // task. The hash comes from `get_file_metadata.contentHash`
        // — that's the same hash the scanner cached, so it matches
        // what's on disk right now (modulo external writes between
        // the scan and this load, which `read_text` would have
        // surfaced as an error or stale-content situation). The
        // save-flow uses this hash as `expected_content_hash` so a
        // mid-edit external write is caught as `WriteConflict`.
        let result: Result<(String, [Heading], String?), VaultError> = await Task.detached(priority: .userInitiated) {
            do {
                let text = try session.readText(path: path)
                // get_file_metadata returns nil when the path isn't in
                // the index yet (race: file showed up between scan and
                // selection). Empty headings is the right fallback —
                // the outline pane shows its "no headings" empty state
                // and the user still sees the content. A nil hash is
                // fine too: subsequent saves will pass nil as
                // expected_content_hash, which means "save
                // unconditionally" — the right behavior when we
                // don't have a known baseline.
                let metadata = try session.getFileMetadata(path: path)
                return .success((text, metadata?.headings ?? [], metadata?.contentHash))
            } catch let error as VaultError {
                return .failure(error)
            } catch {
                return .failure(.Io(message: error.localizedDescription))
            }
        }.value

        // Don't touch `isLoadingNote` if the user has already moved
        // on. Same reasoning as `loadCurrentLinks`: the newer task
        // already set the flag, and clearing it here would flicker
        // the loading state off briefly.
        guard !Task.isCancelled, selectedFilePath == path else { return }

        switch result {
        case .success(let (text, headings, contentHash)):
            currentNoteText = text
            savedBaselineText = text
            currentNoteContentHash = contentHash
            loadedFilePath = path
            hasUnsavedChanges = false
            currentNoteHeadings = headings
            noteLoadError = nil
        case .failure(let error):
            currentNoteText = nil
            savedBaselineText = nil
            currentNoteContentHash = nil
            loadedFilePath = nil
            hasUnsavedChanges = false
            currentNoteHeadings = []
            noteLoadError = humanReadable(error)
        }
        isLoadingNote = false
    }

    /// Load the inbound (backlinks) and outgoing-links lists for the
    /// currently-selected note off the main actor. Both queries hit
    /// the same SQLite mutex so we run them in one detached task to
    /// keep the lock-contention picture predictable.
    ///
    /// The backlinks query is bounded with a generous page size
    /// (200) because the sidebar panel doesn't yet paginate — that
    /// lands when we wire link activation in #C5. Vaults with more
    /// than 200 inbound links to a single note are vanishingly rare
    /// in V1 territory and will get a "+ more" affordance later.
    func loadCurrentLinks(path: String) async {
        guard let session = currentSession else { return }
        isLoadingLinks = true

        // Pull links + properties under a single mutex acquisition.
        // Previously this called `backlinks`, `outgoingLinks`, and
        // `getFileMetadata` in sequence — three independent lock
        // grabs that each raced the scanner's transaction-long lock
        // hold (#92 item 4). The new `noteLoadBundle` API holds the
        // mutex for one contiguous slice while it runs all three
        // queries.
        let result: Result<([Backlink], [OutgoingLink], [Property]), VaultError> =
            await Task.detached(priority: .userInitiated) {
                do {
                    let bundle = try session.noteLoadBundle(
                        path: path,
                        backlinksPaging: Paging(cursor: nil, limit: 200)
                    )
                    return .success((bundle.backlinks.items, bundle.outgoingLinks, bundle.properties))
                } catch let error as VaultError {
                    return .failure(error)
                } catch {
                    return .failure(.Io(message: error.localizedDescription))
                }
            }
            .value

        // Don't touch `isLoadingLinks` if the user has already moved
        // on: a newer task is in flight and has already re-set the
        // flag to `true`, so clearing it here would flicker the
        // spinner off mid-load. The newer task owns the flag's
        // lifecycle from this point on.
        guard !Task.isCancelled, selectedFilePath == path else { return }

        switch result {
        case .success(let (backlinks, outgoing, properties)):
            currentBacklinks = backlinks
            currentOutgoingLinks = outgoing
            currentNoteProperties = properties
            linksLoadError = nil
        case .failure(let error):
            currentBacklinks = []
            currentOutgoingLinks = []
            currentNoteProperties = []
            linksLoadError = humanReadable(error)
        }
        isLoadingLinks = false
    }

    // MARK: - Save flow

    /// Editor's two-way binding writes new buffer contents through
    /// this method. Keeps `currentNoteText` as the live buffer and
    /// recomputes `hasUnsavedChanges` against `savedBaselineText` so
    /// the dirty indicator and the dirty-gate stay in sync.
    ///
    /// Calling this with the same string the editor already holds
    /// is a no-op (the equality check below) — SwiftUI sometimes
    /// re-applies bindings during view updates, and we don't want
    /// that to spuriously flip the dirty flag.
    func updateEditorText(_ newText: String) {
        if currentNoteText == newText { return }
        currentNoteText = newText
        hasUnsavedChanges = (newText != (savedBaselineText ?? ""))
    }

    /// SwiftUI `Binding<String>` for the editor view. Wraps the
    /// `currentNoteText` getter and routes writes through
    /// `updateEditorText` so the dirty-state bookkeeping happens
    /// exactly once per buffer change, regardless of how many
    /// times SwiftUI re-applies the binding during a render pass.
    func noteTextBinding() -> Binding<String> {
        Binding(
            get: { self.currentNoteText ?? "" },
            set: { self.updateEditorText($0) }
        )
    }

    /// Save the current editor buffer back to the file under
    /// `loadedFilePath`, refresh the cached hash + headings, and
    /// announce success or surface a conflict. Cmd+S calls this.
    ///
    /// Re-entrancy: a save already in flight is a no-op so the
    /// user can't queue overlapping `save_text` calls by spamming
    /// Cmd+S. The Rust side's session mutex would serialize them
    /// anyway, but this also keeps the UI flag (`isSaving`)
    /// coherent.
    ///
    /// Returns through `saveTask` so tests can `await` to
    /// deterministically observe the post-save state.
    @discardableResult
    func saveCurrentNote() -> Task<Void, Never>? {
        guard !isSaving else { return nil }
        guard let session = currentSession,
            let path = loadedFilePath,
            let contents = currentNoteText
        else { return nil }
        isSaving = true
        saveError = nil
        let expected = currentNoteContentHash
        let task: Task<Void, Never> = Task { [weak self] in
            await self?.performSave(
                session: session,
                path: path,
                contents: contents,
                expectedHash: expected
            )
            return
        }
        saveTask = task
        return task
    }

    /// Inner save body. Split out so it can be reused by
    /// `resolveSaveConflictKeepMine` (which re-saves with the
    /// current on-disk hash so the user's bytes win).
    private func performSave(
        session: VaultSession,
        path: String,
        contents: String,
        expectedHash: String?
    ) async {
        // Detached so the SQLite-mutex-holding `save_text` doesn't
        // pin the main actor while disk IO + tree rewrites run.
        let outcome: Result<SaveReport, VaultError> = await Task.detached(priority: .userInitiated) {
            do {
                let report = try session.saveText(
                    path: path,
                    contents: contents,
                    expectedContentHash: expectedHash
                )
                return .success(report)
            } catch let error as VaultError {
                return .failure(error)
            } catch {
                return .failure(.Io(message: error.localizedDescription))
            }
        }.value

        // The user could have switched files (or closed the vault)
        // while we were saving. Drop the result in that case
        // rather than mutating state for a file the user has
        // already moved on from.
        guard loadedFilePath == path else {
            isSaving = false
            return
        }

        switch outcome {
        case .success(let report):
            currentNoteContentHash = report.newContentHash
            savedBaselineText = contents
            hasUnsavedChanges = false
            // Refresh headings so the outline matches the just-
            // saved buffer. Same shape as `loadCurrentNote` —
            // a metadata fetch, no need to re-read text. The
            // links/properties panels still get refreshed by the
            // existing `loadCurrentLinks` path when the user next
            // switches selection; refreshing them here would
            // double-spin the SQLite mutex for marginal benefit.
            refreshHeadingsAfterSave(session: session, path: path)
            postAccessibilityAnnouncement(
                "Saved \(filename(of: path)).",
                priority: .medium
            )
        case .failure(.WriteConflict(let currentHash, let expected, let currentMtimeMs)):
            currentSaveConflict = SaveConflict(
                path: path,
                attemptedContents: contents,
                currentContentHash: currentHash,
                expectedContentHash: expected,
                currentMtimeMs: currentMtimeMs
            )
            // Polite announcement: surface the conflict state
            // without yanking focus away from whatever the user
            // is currently doing in the editor. The alert itself
            // is modal and will steal focus when SwiftUI presents
            // it.
            postAccessibilityAnnouncement(
                "Save blocked. \(filename(of: path)) was modified externally. Resolve in the dialog.",
                priority: .medium
            )
        case .failure(let error):
            saveError = humanReadable(error)
        }
        isSaving = false
    }

    /// Re-run the save with `expected_content_hash` set to the
    /// hash we just observed on disk. The user explicitly chose to
    /// overwrite the external version; resetting `expected` to
    /// `current` removes the conflict guard so the second save
    /// goes through.
    @discardableResult
    func resolveSaveConflictKeepMine() -> Task<Void, Never>? {
        guard let conflict = currentSaveConflict,
            let session = currentSession,
            loadedFilePath == conflict.path
        else {
            currentSaveConflict = nil
            return nil
        }
        // Clear the conflict so the alert dismisses immediately
        // — the in-flight task takes over from here.
        currentSaveConflict = nil
        isSaving = true
        let task: Task<Void, Never> = Task { [weak self] in
            await self?.performSave(
                session: session,
                path: conflict.path,
                contents: conflict.attemptedContents,
                expectedHash: conflict.currentContentHash
            )
            return
        }
        saveTask = task
        return task
    }

    /// Discard the in-editor buffer for the conflicted file and
    /// reload the current on-disk version. Equivalent to "let the
    /// external write win." Clears the conflict either way so the
    /// alert can dismiss.
    @discardableResult
    func resolveSaveConflictReloadFromDisk() -> Task<Void, Never>? {
        guard let conflict = currentSaveConflict else { return nil }
        currentSaveConflict = nil
        hasUnsavedChanges = false
        // Same path the conflict came from — kick `loadCurrentNote`
        // to refresh text + hash + headings together. If the user
        // has since navigated away, the load's `selectedFilePath
        // == path` guard will drop the result.
        let task: Task<Void, Never> = Task { [weak self] in
            await self?.loadCurrentNote(path: conflict.path)
            return
        }
        noteLoadTask = task
        return task
    }

    /// Dismiss the conflict alert without writing or reloading.
    /// The buffer stays as the user left it; `hasUnsavedChanges`
    /// stays true so the indicator and dirty-gate remain active.
    func resolveSaveConflictCancel() {
        currentSaveConflict = nil
    }

    /// "Save changes?" prompt: Save → run the save, then continue
    /// with the pending navigation if the save succeeds. A
    /// `WriteConflict` short-circuits the navigation so the user
    /// gets the conflict alert in place of the navigation step.
    @discardableResult
    func resolvePendingNavigationSave() -> Task<Void, Never>? {
        guard let pending = pendingNavigation else { return nil }
        let task: Task<Void, Never> = Task { [weak self] in
            await self?.saveAndPerformNavigation(pending)
            return
        }
        return task
    }

    private func saveAndPerformNavigation(_ pending: PendingNavigation) async {
        await saveCurrentNote()?.value
        // Save can complete in one of three states; only proceed
        // with the navigation when the save genuinely succeeded
        // (hasUnsavedChanges cleared, no conflict, no error).
        guard !hasUnsavedChanges,
            currentSaveConflict == nil,
            saveError == nil
        else {
            return
        }
        applyPendingNavigation(pending)
    }

    /// "Save changes?" prompt: Discard → drop the dirty flag and
    /// continue with the pending navigation.
    func resolvePendingNavigationDiscard() {
        guard let pending = pendingNavigation else { return }
        hasUnsavedChanges = false
        // Restore the editor buffer to the baseline so a subsequent
        // load doesn't have a stale dirty buffer hanging around in
        // memory (the load will overwrite anyway, but matching
        // baseline keeps the dirty flag honest in the interim).
        currentNoteText = savedBaselineText
        applyPendingNavigation(pending)
    }

    /// "Save changes?" prompt: Cancel → clear the pending
    /// navigation without saving. The dirty buffer stays.
    func resolvePendingNavigationCancel() {
        pendingNavigation = nil
    }

    /// Common tail for the Save / Discard branches: clear the
    /// pending state and actually perform the requested
    /// navigation.
    private func applyPendingNavigation(_ pending: PendingNavigation) {
        pendingNavigation = nil
        switch pending {
        case .closeVault:
            closeVault()
        case .selectFile(let path):
            // Setting selectedFilePath re-triggers the
            // handleSelectionChange subscription. At this point
            // `hasUnsavedChanges` is false (Save cleared it; Discard
            // cleared it), so the dirty gate falls through and the
            // load proceeds normally.
            selectedFilePath = path
        }
    }

    /// Refresh headings for the just-saved note without
    /// re-reading its text. `save_text` already updates the
    /// `headings` table inside its transaction, so a single
    /// metadata fetch is enough. Failures are non-fatal: the
    /// outline shows the old list until the next reload.
    private func refreshHeadingsAfterSave(session: VaultSession, path: String) {
        Task { [weak self] in
            // Off-actor metadata fetch — getFileMetadata grabs the
            // SQLite mutex, so we don't want to pin the main actor
            // for its duration. The post-await re-grab of `self`
            // keeps the Sendable check happy under the Swift 6
            // language mode.
            let headings: [Heading]? = await Task.detached(priority: .userInitiated) {
                (try? session.getFileMetadata(path: path))?.headings
            }.value
            guard let self else { return }
            guard self.loadedFilePath == path else { return }
            self.currentNoteHeadings = headings ?? []
        }
    }

    /// Closes the vault when the editor is clean; routes through
    /// the "Save changes?" alert when it's dirty. The toolbar
    /// "Close Vault" button calls this instead of `closeVault()`
    /// directly so the dirty path can't be bypassed.
    func attemptCloseVault() {
        if hasUnsavedChanges {
            pendingNavigation = .closeVault
        } else {
            closeVault()
        }
    }

    private func filename(of path: String) -> String {
        (path as NSString).lastPathComponent
    }

    // MARK: - Scan progress

    /// Main-actor entry point for scanner events. Called by
    /// `ScanProgressAdapter` after marshaling from the scanner thread.
    /// `internal` so XCTest can drive synthetic event streams against
    /// it without spinning up a real scan.
    func handleScanProgress(_ event: ScanProgress) {
        scanProgress = event
        switch event {
        case .started(let totalFiles):
            // Forced announcement: the user wants to know a scan
            // started even if they just opened the vault and we
            // haven't accumulated 350 ms of cooldown yet.
            announceScan(
                message: "Scanning vault. "
                    + "\(totalFiles) \(totalFiles == 1 ? "file" : "files") to index.",
                force: true
            )
        case .fileIndexed(_, let indexed, let total):
            // Rate-limited: at most ~3 per second per the acceptance
            // criteria, so VoiceOver flow stays polite even on a
            // 50k-file vault.
            announceScan(
                message: "Indexed \(indexed) of \(total) files.",
                force: false
            )
        case .finished(let report):
            announceScan(
                message: "Scan complete. "
                    + "\(report.filesIndexed) "
                    + (report.filesIndexed == 1 ? "file" : "files")
                    + " indexed.",
                force: true
            )
            // Clear so the progress bar hides; loadFiles' post-scan
            // populate runs next and updates `files`.
            scanProgress = nil
        case .cancelled, .failed:
            // No "finished" announcement. Failed is surfaced via
            // `scanError` (the existing path); cancelled is silent
            // because closeVault / next-vault flow is already
            // visible.
            scanProgress = nil
        }
    }

    /// Post a VoiceOver announcement subject to the rate guard. When
    /// `force` is true the announcement always fires (used for
    /// Started/Finished). Otherwise it only fires if the clock has
    /// advanced past the configured min-interval since the last fire.
    private func announceScan(message: String, force: Bool) {
        let now = scanClock()
        if !force,
            now.timeIntervalSince(scanAnnouncementLastFiredAt) < scanAnnouncementMinInterval
        {
            return
        }
        scanAnnouncementLastFiredAt = now
        scanAnnouncementCount += 1
        scanAnnouncementLastMessage = message
        postAccessibilityAnnouncement(message)
    }

    // MARK: - Private

    private func recordOpened(url: URL) {
        let entry = RecentVault(url: url)
        do {
            recentVaults = try recentsStore.add(entry)
        } catch {
            // Same rationale as removeRecent: don't block the open flow
            // on a recents-list write failure.
            fputs(
                "Slate: failed to persist recent-vaults add: \(error)\n",
                stderr
            )
        }
    }

    private func humanReadable(_ error: VaultError) -> String {
        switch error {
        case .Io(let message), .Db(let message), .Trash(let message):
            return message
        case .InvalidPath(let path, let reason):
            return "Invalid path \(path): \(reason)"
        case .Cancelled:
            return "Operation cancelled."
        case .InvalidUtf8(let path):
            return "File at \(path) is not valid UTF-8."
        case .FileTooLarge(let path, let size):
            return "File at \(path) is \(size) bytes — larger than this build's refuse threshold."
        case .InvalidQuery(let message):
            return "Search query is invalid: \(message)"
        case .Unsupported(let feature):
            return "\(feature) is not implemented yet."
        case .WriteConflict:
            // The editor's save-flow handles this case directly with
            // a "Keep mine / Reload from disk" affordance (issue #64);
            // surfacing it through the generic humanReadable path is
            // a last-resort fallback for non-editor callers.
            return "This file was modified by another writer since you opened it. Reload to see the latest version."
        }
    }
}

/// 1-based line number of the first occurrence (in `body`) of any
/// alphanumeric token from `query`. Falls back to 1 when no token
/// can be found — FTS5 may have matched through stemming, so the
/// raw tokens needn't appear literally in the note.
///
/// Lives at file scope (not on AppState) so it has no implicit
/// MainActor isolation — pure string crunching, the caller can
/// invoke it from any actor context.
///
/// Mirrors the Rust-side heuristic that lived in
/// `search_db::find_first_token_line` before #92 item 1 moved
/// line derivation out of `full_text_search`. Lowercases the body
/// once and counts newlines in the lowercased prefix — avoids a
/// cross-string slice that would panic on non-boundary indices
/// when Unicode lowercasing changes byte length (`İ` → `i` + U+0307
/// is 2→3 bytes).
func firstTokenLineNumber(in body: String, query: String) -> Int {
    let bodyLower = body.lowercased()
    // Strip FTS5 column-filter prefixes before tokenizing. Today the
    // only indexed column is `body_text`; a user typing
    // `body_text:foo` means "find `foo` inside body_text", so the
    // `body_text:` part shouldn't seed tokens for the line scan. If
    // more columns ever land in the FTS5 schema, add their names
    // here.
    let preprocessed = query.lowercased()
        .replacingOccurrences(of: "body_text:", with: " ")
    // FTS5 keywords that would otherwise sneak through the split and
    // pollute the line lookup if they happen to appear as bare words
    // in prose (#93 item 5). Pure-numeric tokens are also dropped:
    // numbers appearing inside composite FTS5 constructs
    // (`NEAR(a b, 5)`, `LIMIT 10`) aren't semantically meaningful to
    // a body-line scan.
    let fts5Keywords: Set<String> = ["and", "or", "not", "near"]
    let tokens = preprocessed
        .split { !$0.isLetter && !$0.isNumber }
        .map(String.init)
        .filter { tok in
            !tok.isEmpty
                && !fts5Keywords.contains(tok)
                && !tok.allSatisfy(\.isNumber)
        }
    var earliest: String.Index? = nil
    for tok in tokens {
        if let range = bodyLower.range(of: tok) {
            switch earliest {
            case .none:
                earliest = range.lowerBound
            case .some(let prev):
                earliest = min(prev, range.lowerBound)
            }
        }
    }
    guard let earliest else { return 1 }
    // Count newlines in the prefix using UTF-8 view so we don't
    // pay for an O(n) String.distance over the prefix.
    let prefix = bodyLower[..<earliest]
    return prefix.utf8.reduce(1) { acc, byte in
        byte == 0x0A ? acc + 1 : acc
    }
}
