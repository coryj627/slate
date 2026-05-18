import Combine
import Foundation

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

    /// UTF-8 text of the currently-selected note, once it's been
    /// loaded. Nil while no note is selected or while loading is
    /// in flight.
    @Published private(set) var currentNoteText: String?
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

    /// One-shot channel for "scroll the content pane to this heading
    /// anchor." `OutlineSidebar` sends; `NoteContentView` subscribes
    /// via `.onReceive`. PassthroughSubject (not @Published) so
    /// repeated clicks on the same heading re-trigger the scroll
    /// without needing a counter.
    let scrollAnchorRequest = PassthroughSubject<String, Never>()

    /// Handle on the in-flight scan + list task kicked off by
    /// `openVault`. Exposed (internal, not Published) so tests can
    /// `await state.scanTask?.value` to deterministically observe the
    /// post-scan state.
    private(set) var scanTask: Task<Void, Never>?

    /// Handle on the in-flight note-load task. Same shape as
    /// `scanTask` so tests can await deterministically.
    private(set) var noteLoadTask: Task<Void, Never>?

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
                .appendingPathComponent("yana-recent-vaults-fallback.json")
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
    }

    /// Internal hook from the selectedFilePath subscription. Cancels
    /// any in-flight note load and kicks off a fresh one — or clears
    /// content if `path` is nil.
    private func handleSelectionChange(to path: String?) {
        noteLoadTask?.cancel()
        noteLoadTask = nil
        currentNoteText = nil
        currentNoteHeadings = []
        noteLoadError = nil
        guard let path else {
            isLoadingNote = false
            return
        }
        noteLoadTask = Task { [weak self] in
            await self?.loadCurrentNote(path: path)
        }
    }

    /// Ask the content pane to scroll to the given heading anchor.
    /// Sent by `OutlineSidebar` rows; `NoteContentView` subscribes via
    /// `onReceive(scrollAnchorRequest)`.
    func requestScrollToHeading(anchor: String) {
        scrollAnchorRequest.send(anchor)
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
        currentSession = nil
        currentVaultURL = nil
        files = []
        scanError = nil
        selectedFilePath = nil
        currentNoteText = nil
        currentNoteHeadings = []
        noteLoadError = nil
        isScanning = false
        isLoadingNote = false
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
                "YANA: failed to persist recent-vaults removal: \(error)\n",
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
        let adapter = ScanProgressAdapter { [weak self] event in
            Task { @MainActor in
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
        defer { isLoadingNote = false }

        let result: Result<(String, [Heading]), VaultError> = await Task.detached(priority: .userInitiated) {
            do {
                let text = try session.readText(path: path)
                // get_file_metadata returns nil when the path isn't in
                // the index yet (race: file showed up between scan and
                // selection). Empty headings is the right fallback —
                // the outline pane shows its "no headings" empty state
                // and the user still sees the content.
                let metadata = try session.getFileMetadata(path: path)
                return .success((text, metadata?.headings ?? []))
            } catch let error as VaultError {
                return .failure(error)
            } catch {
                return .failure(.Io(message: error.localizedDescription))
            }
        }.value

        // Drop the result if the user has already moved on (closed
        // the vault, selected a different note). Without this the
        // pane could flash stale content for ~100 ms after a switch.
        guard !Task.isCancelled, selectedFilePath == path else { return }

        switch result {
        case .success(let (text, headings)):
            currentNoteText = text
            currentNoteHeadings = headings
            noteLoadError = nil
        case .failure(let error):
            currentNoteText = nil
            currentNoteHeadings = []
            noteLoadError = humanReadable(error)
        }
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
                "YANA: failed to persist recent-vaults add: \(error)\n",
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
        }
    }
}
