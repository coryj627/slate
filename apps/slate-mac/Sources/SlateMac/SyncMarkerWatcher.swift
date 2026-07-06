// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

/// Live re-detection of sync markers (#638) — the bounded watch that
/// keeps the sync diagnostics leaf honest after vault open.
///
/// Milestone M ran detection once per vault open plus manual refresh;
/// if a sync system starts managing the vault mid-session (`git init`,
/// LiveSync install, Dropbox pointed at the folder), the leaf was
/// stale until reopen. This watcher closes that gap WITHOUT building
/// general vault-watching infrastructure (`FsVaultProvider::watch` is
/// still a stub by design): it holds `DispatchSource` directory
/// watches on exactly the places the detector's in-vault probes look —
/// the vault root, `.obsidian`, and `.obsidian/plugins` — and fires a
/// debounced callback the host wires to `refreshSyncDiagnostics()`.
///
/// Bounds and non-goals, deliberately:
/// - Directory-entry events only (a marker appearing/disappearing in
///   one of the three watched dirs). Ancestor/location signals (an
///   ancestor `.dropbox.cache`, iCloud eviction xattrs) don't emit
///   events here — those describe where the vault LIVES, which doesn't
///   change mid-session; the per-open probe covers them.
/// - Note saves at the vault root also churn the root's entry list
///   (atomic temp-file + rename), so events over-fire. That's fine:
///   the debounce collapses bursts and the probes behind the refresh
///   are bounded exact-path checks (< 1 ms) — correctness never
///   depends on event filtering.
/// - The `.obsidian`/`plugins` watches re-arm after every event, so a
///   plugins tree created mid-session gets picked up by the parent
///   watch firing first (root → `.obsidian` → `plugins` chain).
///
/// One robustness bound the naive "reset the timer on every event"
/// debounce is missing, closed here:
/// - **Max-latency ceiling (#638 adversarial).** A pure trailing
///   debounce can be starved forever: continuous sub-interval root
///   churn (a busy sync tool, or a Cmd+S habit) reschedules the timer
///   every time and it never fires. `maxLatency` caps the wait so a
///   callback still lands within a fixed bound of the FIRST event in a
///   burst even under unbroken churn.
///
/// Setup-race note (#638 adversarial): a marker that appears in the
/// microsecond gap between the host's post-scan probe and the watch
/// fds opening emits no event (the entry already exists when the fd
/// opens). The host mitigates by DISPATCHING `start()` before running
/// that probe (`startSyncMarkerWatcher`), and the async arm
/// (`open` + source creation) almost always beats the slower detached
/// FFI probe — so the watch is live first. The residual is benign:
/// any later change in a watched dir re-fires the event (the common
/// case — a sync tool writes into the dir it just made), so only a
/// marker that lands in that gap AND is never touched again waits for
/// the next manual refresh. A synchronous arm was tried and rejected:
/// `queue.sync` on the `@MainActor` owner stalls the main thread under
/// a parallel workload.
///
/// Threading: `start()`/`stop()` are main-thread affine (the AppState
/// owner is `@MainActor`); events arrive on a private queue and
/// `onChange` is delivered on the MAIN queue. `stop()` (or deinit)
/// cancels everything; a cancelled watcher never calls back.
final class SyncMarkerWatcher {
    /// The vault-relative directories the detector's in-vault probes
    /// read (m_spec §M-1 table): root markers (.git, .stfolder,
    /// .stignore, .dropbox, .dropbox.cache, .tmp.drive*, the OneDrive
    /// GUID, *.icloud placeholders), and the LiveSync plugin dir.
    /// "" is the vault root itself.
    static let watchedSubdirectories = ["", ".obsidian", ".obsidian/plugins"]

    private let root: URL
    private let debounceInterval: TimeInterval
    /// Upper bound on how long a callback can be deferred by a
    /// continuous event burst — the anti-starvation ceiling. Defaults
    /// to 4× the debounce so a normal quiet-period settle is unchanged
    /// but unbroken churn still surfaces within a fixed window.
    private let maxLatency: TimeInterval
    private let onChange: () -> Void
    /// Serial queue owning all mutable watcher state after `start()`.
    private let queue = DispatchQueue(label: "slate.sync-marker-watcher")
    /// Keyed by vault-relative subdirectory ("" = root).
    private var sources: [String: DispatchSourceFileSystemObject] = [:]
    private var debounceTimer: DispatchSourceTimer?
    /// Deadline for the current burst's max-latency ceiling. Set on the
    /// FIRST event after a callback (or after a quiet period), cleared
    /// when the callback fires. `nil` = no burst in flight.
    private var burstCeiling: DispatchTime?
    private var cancelled = false

    /// - Parameters:
    ///   - root: the vault root (the same `fs_root` the detector probes).
    ///   - debounceInterval: quiet period before `onChange` fires;
    ///     injectable so tests don't wait production-scale seconds.
    ///   - maxLatency: ceiling on how long continuous churn may defer a
    ///     callback; defaults to `4 × debounceInterval`. Clamped to at
    ///     least `debounceInterval` (a smaller ceiling would be
    ///     meaningless — it can never fire before the trailing timer).
    ///   - onChange: delivered on the MAIN queue, never after `stop()`.
    init(
        root: URL,
        debounceInterval: TimeInterval = 2.5,
        maxLatency: TimeInterval? = nil,
        onChange: @escaping () -> Void
    ) {
        self.root = root
        self.debounceInterval = debounceInterval
        self.maxLatency = max(debounceInterval, maxLatency ?? debounceInterval * 4)
        self.onChange = onChange
    }

    deinit {
        // At deinit the refcount is 0, so no event handler (all capture
        // `[weak self]`) is holding this instance — nothing else can be
        // touching the queue-confined state. Cancel the sources/timer
        // DIRECTLY rather than via `stop()`'s `queue.sync`: if the last
        // release happened to land on `queue` itself, a `queue.sync`
        // here would deadlock (sync onto the current queue). Each
        // source's cancel handler still runs on `queue` and closes its
        // fd — no fd leak.
        for source in sources.values {
            source.cancel()
        }
        debounceTimer?.cancel()
    }

    /// Arm the directory watches. Idempotent; safe to call once after
    /// vault open. Missing subdirectories (no `.obsidian` yet) are
    /// skipped now and picked up by the parent watch's re-arm when they
    /// appear.
    ///
    /// Arming is dispatched ASYNCHRONOUSLY onto the private queue and
    /// this returns immediately — it deliberately does NOT block the
    /// caller's thread. The caller is `@MainActor`; a synchronous
    /// `queue.sync` here would pin the main thread on every vault open,
    /// and under a parallel test run (hundreds of watchers) that
    /// starves the dispatch pool and stalls unrelated main-actor work.
    /// The host still gets setup-race coverage by DISPATCHING the arm
    /// before it runs the post-scan probe (see `startSyncMarkerWatcher`):
    /// arming is `open(O_EVTONLY)` + source creation (microseconds) and
    /// the probe is a slower detached FFI round-trip, so in practice the
    /// watch is live before the probe reads. The residual sub-window is
    /// benign: a marker that lands in it and is followed by ANY later
    /// change in a watched dir (a sync tool writing into the dir it just
    /// created — the overwhelmingly common case) is caught by the event;
    /// only a marker that appears in that microsecond gap AND is never
    /// touched again waits for the next manual refresh.
    func start() {
        queue.async { [weak self] in
            guard let self, !self.cancelled else { return }
            self.armMissingWatches()
        }
    }

    /// Cancel all watches and the pending debounce. After this returns
    /// (or deinit runs), `onChange` will not be called again: the
    /// main-queue hop re-checks `cancelled` under the queue.
    func stop() {
        queue.sync {
            cancelled = true
            for source in sources.values {
                source.cancel()
            }
            sources.removeAll()
            debounceTimer?.cancel()
            debounceTimer = nil
            burstCeiling = nil
        }
    }

    // MARK: - Queue-confined internals

    /// Open an event-only fd + DispatchSource for each watched subdir
    /// that exists and isn't already watched. Called on `queue`.
    private func armMissingWatches() {
        for subdir in Self.watchedSubdirectories where sources[subdir] == nil {
            let url =
                subdir.isEmpty ? root : root.appendingPathComponent(subdir)
            let fd = open(url.path, O_EVTONLY)
            guard fd >= 0 else { continue }  // absent dir: parent re-arms us later
            let source = DispatchSource.makeFileSystemObjectSource(
                fileDescriptor: fd,
                // .write = the directory's entry list changed (create/
                // delete/rename inside it) — exactly the marker signal.
                // .delete/.rename catch the watched dir itself going away
                // so we drop the stale source and let the parent re-arm.
                eventMask: [.write, .delete, .rename],
                queue: queue
            )
            source.setEventHandler { [weak self] in
                self?.handleEvent(subdir: subdir, source: source)
            }
            source.setCancelHandler {
                close(fd)
            }
            sources[subdir] = source
            source.resume()
        }
    }

    /// Called on `queue` for every raw event.
    private func handleEvent(subdir: String, source: DispatchSourceFileSystemObject) {
        guard !cancelled else { return }
        // If the watched dir itself was deleted/renamed, drop the
        // source; the parent watch re-arms a fresh one if it returns.
        if source.data.contains(.delete) || source.data.contains(.rename) {
            source.cancel()
            sources = sources.filter { $0.value !== source }
        }
        // A child dir may have just appeared (.obsidian created, then
        // plugins) — chase the chain before debouncing so the next
        // event in the new dir is already covered.
        armMissingWatches()
        scheduleDebouncedCallback()
    }

    /// Coalesce event bursts (note saves churn the root's entry list)
    /// into one callback after `debounceInterval` of quiet — but never
    /// defer past the burst's max-latency ceiling, so unbroken churn
    /// can't starve the callback forever. On `queue`.
    private func scheduleDebouncedCallback() {
        let now = DispatchTime.now()
        // First event of a fresh burst: anchor the ceiling. Subsequent
        // events keep the SAME ceiling (it's measured from the first
        // event), so continuous churn still fires within `maxLatency`.
        let ceiling = burstCeiling ?? (now + maxLatency)
        burstCeiling = ceiling
        // Trailing quiet-period deadline, clamped to the ceiling.
        let trailing = now + debounceInterval
        let deadline = min(trailing, ceiling)

        debounceTimer?.cancel()
        let timer = DispatchSource.makeTimerSource(queue: queue)
        timer.schedule(deadline: deadline)
        timer.setEventHandler { [weak self] in
            guard let self, !self.cancelled else { return }
            self.debounceTimer = nil
            // Burst delivered: the next event starts a new ceiling.
            self.burstCeiling = nil
            let callback = self.onChange
            DispatchQueue.main.async { [weak self] in
                // Re-check on the main hop: stop() may have landed
                // between the timer firing and this block running.
                guard let self, !self.isCancelled else { return }
                callback()
            }
        }
        debounceTimer = timer
        timer.resume()
    }

    /// Queue-synchronized read for the main-hop cancellation re-check.
    private var isCancelled: Bool {
        queue.sync { cancelled }
    }
}
