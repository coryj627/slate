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
/// Threading: `start()`/`stop()` are main-thread affine (the AppState
/// owner is `@MainActor`); events arrive on a private queue and the
/// debounced callback is delivered on the MAIN queue. `stop()` (or
/// deinit) cancels everything; a cancelled watcher never calls back.
final class SyncMarkerWatcher {
    /// The vault-relative directories the detector's in-vault probes
    /// read (m_spec §M-1 table): root markers (.git, .stfolder,
    /// .stignore, .dropbox, .dropbox.cache, .tmp.drive*, the OneDrive
    /// GUID, *.icloud placeholders), and the LiveSync plugin dir.
    /// "" is the vault root itself.
    static let watchedSubdirectories = ["", ".obsidian", ".obsidian/plugins"]

    private let root: URL
    private let debounceInterval: TimeInterval
    private let onChange: () -> Void
    /// Serial queue owning all mutable watcher state after `start()`.
    private let queue = DispatchQueue(label: "slate.sync-marker-watcher")
    /// Keyed by vault-relative subdirectory ("" = root).
    private var sources: [String: DispatchSourceFileSystemObject] = [:]
    private var debounceTimer: DispatchSourceTimer?
    private var cancelled = false

    /// - Parameters:
    ///   - root: the vault root (the same `fs_root` the detector probes).
    ///   - debounceInterval: quiet period before `onChange` fires;
    ///     injectable so tests don't wait production-scale seconds.
    ///   - onChange: delivered on the MAIN queue, never after `stop()`.
    init(
        root: URL,
        debounceInterval: TimeInterval = 2.5,
        onChange: @escaping () -> Void
    ) {
        self.root = root
        self.debounceInterval = debounceInterval
        self.onChange = onChange
    }

    deinit {
        stop()
    }

    /// Arm the directory watches. Idempotent; safe to call once after
    /// vault open. Missing subdirectories (no `.obsidian` yet) are
    /// skipped now and picked up by the parent watch's re-arm when
    /// they appear.
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
    /// into one callback after `debounceInterval` of quiet. On `queue`.
    private func scheduleDebouncedCallback() {
        debounceTimer?.cancel()
        let timer = DispatchSource.makeTimerSource(queue: queue)
        timer.schedule(deadline: .now() + debounceInterval)
        timer.setEventHandler { [weak self] in
            guard let self, !self.cancelled else { return }
            self.debounceTimer = nil
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
