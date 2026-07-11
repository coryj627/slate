// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! History leaf plumbing (Milestone O-5, #543): version-list loads,
//! the pref-gated since-open funnel (compute-then-mark), the restore
//! and deleted-file recovery flows, the compaction-error listener,
//! and the retention prefs bridge. Stored state lives in
//! AppState.swift ("History leaf" MARK); this file is behavior only.

import Foundation

/// A display-ready error (`Error` conformance so it rides `Result`).
struct DisplayError: Error, Equatable {
    let message: String
}

/// One pending "Restore version?" confirmation.
struct HistoryRestoreRequest: Identifiable, Equatable {
    let id = UUID()
    let path: String
    let versionHash: String
    /// Pre-formatted absolute date for the alert copy.
    let formattedDate: String
}

/// A history-specific error alert (integrity failure, recovery
/// collision) — kept off the generic save-error surface.
struct HistoryAlert: Identifiable, Equatable {
    let id = UUID()
    let title: String
    let message: String
}

/// One compaction-failure event (the O-2 channel's Mac half). The
/// message is the core's copy, presented verbatim.
struct CompactionFailure: Identifiable, Equatable {
    let id = UUID()
    let path: String
    let message: String
}

/// uniffi callback adapter: the core invokes `onError` on a worker
/// thread; hop to the main actor and hand the event to AppState. The
/// weak reference means a closed vault's straggler events go nowhere.
final class VaultEventAdapter: VaultEventListener, @unchecked Sendable {
    private weak var appState: AppState?

    init(appState: AppState) {
        self.appState = appState
    }

    func onError(code: EventErrorCode, path: String, message: String) {
        Task { @MainActor [weak appState] in
            appState?.handleVaultEvent(code: code, path: path, message: message)
        }
    }
}

extension AppState {
    // MARK: - Note-open funnel

    /// Load the selected note's history surfaces in ONE detached
    /// slice, in the pinned order (o_spec §O-5 g3):
    ///
    /// 1. `changes_since_last_open` — compute FIRST,
    /// 2. `mark_opened` — then move the baseline,
    /// 3. `list_versions` — first page (limit 50).
    ///
    /// Steps 1–2 run only when the since-open pref is on; with it off
    /// neither call happens (no mark writes — the baseline stays
    /// wherever an earlier pref-on session left it).
    func loadHistoryForCurrentNote(path: String) async {
        guard let session = currentSession else { return }
        historyLoadSeq += 1
        let seq = historyLoadSeq
        let sinceOpenEnabled = historyShowChangesSinceOpen

        // Slice 1: COMPUTE only — no mutation. `markOpened` must not
        // ride this slice: the guards below run after it completes, so
        // a stale load for a note the user already left would still
        // move its baseline and swallow the changes from the user's
        // last real visit (adversarial round 1 High).
        let result: Result<(ChangesSinceOpen?, VersionSummaryPage), VaultError> =
            await Task.detached(priority: .userInitiated) {
                do {
                    var changes: ChangesSinceOpen?
                    if sinceOpenEnabled {
                        changes = try session.changesSinceLastOpen(path: path)
                    }
                    let page = try session.listVersions(
                        path: path, paging: Paging(cursor: nil, limit: 50))
                    return .success((changes, page))
                } catch let error as VaultError {
                    return .failure(error)
                } catch {
                    return .failure(.Io(message: error.localizedDescription))
                }
            }.value

        // Race-test seam: parks the load inside the race window
        // (post-compute, pre-guard). Nil in production — the M-3
        // syncDiagnosticsPublishGate pattern.
        if let gate = historyPublishGate { await gate() }

        // The #328 post-await publish guards + the seq recheck
        // (sync-diagnostics pattern): only the newest load for the
        // still-selected note on the still-open session may publish —
        // or MARK. A stale resume must leave the baseline untouched.
        guard !Task.isCancelled, currentSession === session,
            selectedFilePath == path, seq == historyLoadSeq
        else { return }
        switch result {
        case .success(let (changes, page)):
            sinceOpenChanges = changes
            historyVersions = page.items
            historyNextCursor = page.nextCursor
            historyTotalFiltered = page.totalFiltered
            historyLoadError = nil
            // The open is REAL (guards passed): move the baseline now,
            // AFTER the verdict — the pinned compute-then-mark order.
            if sinceOpenEnabled {
                let markResult: Result<Void, VaultError> =
                    await Task.detached(priority: .userInitiated) {
                        do {
                            try session.markOpened(path: path)
                            return .success(())
                        } catch let error as VaultError {
                            return .failure(error)
                        } catch {
                            return .failure(.Io(message: error.localizedDescription))
                        }
                    }.value
                if case .failure(let error) = markResult {
                    // A failed mark only means the NEXT open re-reports
                    // the same changes — never fatal, never blocking.
                    _ = error
                }
            }
        case .failure(let error):
            sinceOpenChanges = nil
            historyVersions = []
            historyNextCursor = nil
            historyTotalFiltered = 0
            historyLoadError = humanReadable(error)
        }
    }

    /// "Show older versions": append the next page. A generation-bump
    /// cursor error means a compaction rewrote the log between pages —
    /// the list is fresher, not wrong — so silently reload page one.
    func loadOlderVersions() async {
        guard let session = currentSession, let path = selectedFilePath,
            let cursor = historyNextCursor
        else { return }
        historyLoadSeq += 1
        let seq = historyLoadSeq
        let result: Result<VersionSummaryPage, VaultError> =
            await Task.detached(priority: .userInitiated) {
                do {
                    return .success(
                        try session.listVersions(
                            path: path, paging: Paging(cursor: cursor, limit: 50)))
                } catch let error as VaultError {
                    return .failure(error)
                } catch {
                    return .failure(.Io(message: error.localizedDescription))
                }
            }.value
        guard !Task.isCancelled, currentSession === session,
            selectedFilePath == path, seq == historyLoadSeq
        else { return }
        switch result {
        case .success(let page):
            historyVersions.append(contentsOf: page.items)
            historyNextCursor = page.nextCursor
            historyTotalFiltered = page.totalFiltered
        case .failure(.InvalidArgument):
            await loadHistoryForCurrentNote(path: path)
        case .failure(let error):
            historyLoadError = humanReadable(error)
        }
    }

    /// Compute one on-demand diff (Compare / two-version compare) off
    /// the main actor. Pure read — no state beyond the returned value.
    func historyDiff(path: String, fromHash: String, toHash: String) async
        -> Result<StructuredDiff, DisplayError>
    {
        guard let session = currentSession else {
            return .failure(DisplayError(message: "No open vault."))
        }
        let result: Result<StructuredDiff, VaultError> =
            await Task.detached(priority: .userInitiated) {
                do {
                    return .success(
                        try session.diffVersions(
                            path: path, fromHash: fromHash, toHash: toHash))
                } catch let error as VaultError {
                    return .failure(error)
                } catch {
                    return .failure(.Io(message: error.localizedDescription))
                }
            }.value
        return result.mapError { DisplayError(message: humanReadable($0)) }
    }

    /// Reset all per-note and per-vault history state (selection
    /// change / vault close).
    func resetHistoryState() {
        historyVersions = []
        historyNextCursor = nil
        historyTotalFiltered = 0
        historyLoadError = nil
        sinceOpenChanges = nil
        deletedFiles = []
        deletedLoadError = nil
        historyRestoreRequest = nil
        historyLoadTask?.cancel()
        historyLoadTask = nil
    }

    // MARK: - Restore flow

    /// Row action: stage the confirmation alert.
    func requestRestore(versionHash: String, formattedDate: String) {
        guard let path = selectedFilePath else { return }
        historyRestoreRequest = HistoryRestoreRequest(
            path: path, versionHash: versionHash, formattedDate: formattedDate)
    }

    /// Confirmed restore. Passes the loaded document's hash as the
    /// compare-and-swap guard; a dirty buffer routes to the existing
    /// conflict-alert flow BEFORE touching disk (restoring over
    /// unsaved edits is exactly the clobber that alert exists to
    /// stop), and a disk-side `WriteConflict` routes there after.
    /// `HistoryUnavailable` (integrity failure) gets its own alert and
    /// writes nothing. Success: announce, reload through the normal
    /// changed-file path, refresh the list, and move focus to the new
    /// head row (position 0 — the restored state; WCAG 2.4.3).
    func performRestore(_ request: HistoryRestoreRequest) async {
        guard let session = currentSession else { return }
        guard let expectedHash = currentNoteContentHash else { return }

        if hasUnsavedChanges {
            // Buffer-dirty: same surface as an external-change save
            // conflict — keep-mine saves the buffer, reload discards.
            currentSaveConflict = SaveConflict(
                path: request.path,
                attemptedContents: currentNoteText ?? "",
                currentContentHash: expectedHash,
                expectedContentHash: expectedHash,
                currentMtimeMs: 0
            )
            return
        }

        let result: Result<SaveReport, VaultError> =
            await Task.detached(priority: .userInitiated) {
                do {
                    return .success(
                        try session.restoreVersion(
                            path: request.path,
                            versionHash: request.versionHash,
                            expectedContentHash: expectedHash))
                } catch let error as VaultError {
                    return .failure(error)
                } catch {
                    return .failure(.Io(message: error.localizedDescription))
                }
            }.value
        guard !Task.isCancelled, currentSession === session else { return }

        switch result {
        case .success:
            announcer.post(
                "Restored version from \(request.formattedDate).", priority: .high)
            if selectedFilePath == request.path {
                await loadCurrentNote(path: request.path)
                await loadHistoryForCurrentNote(path: request.path)
                historyFocusHeadToken &+= 1
            }
        case .failure(
            .WriteConflict(
                let currentContentHash, let expectedContentHash, let currentMtimeMs)):
            // "Mine" = MY LOADED buffer body — the same semantics as
            // every other conflict producer: Keep Mine writes my state
            // back over the external change (then the version list is
            // fresh for a retry). Re-reading the DISK body here would
            // make Keep Mine a no-op that writes the external content
            // over itself (adversarial round 1 High).
            currentSaveConflict = SaveConflict(
                path: request.path,
                attemptedContents: currentNoteText ?? "",
                currentContentHash: currentContentHash,
                expectedContentHash: expectedContentHash,
                currentMtimeMs: currentMtimeMs
            )
        case .failure(.HistoryUnavailable):
            historyAlert = HistoryAlert(
                title: "Restore failed",
                message:
                    "This version can't be restored: its history failed an integrity check."
            )
        case .failure(let error):
            historyAlert = HistoryAlert(
                title: "Restore failed", message: humanReadable(error))
        }
    }

    // MARK: - Deleted segment

    func loadDeletedFiles() async {
        guard let session = currentSession else { return }
        let result: Result<DeletedFilePage, VaultError> =
            await Task.detached(priority: .userInitiated) {
                do {
                    return .success(
                        try session.listDeletedFiles(
                            paging: Paging(cursor: nil, limit: 200)))
                } catch let error as VaultError {
                    return .failure(error)
                } catch {
                    return .failure(.Io(message: error.localizedDescription))
                }
            }.value
        guard !Task.isCancelled, currentSession === session else { return }
        switch result {
        case .success(let page):
            deletedFiles = page.items
            deletedLoadError = nil
        case .failure(let error):
            deletedLoadError = humanReadable(error)
        }
    }

    /// Restore a deleted file from its remnant log. Success announces
    /// and refreshes through the standard mutation flow (the file tree
    /// picks the file up via the announced refresh); `DestinationExists`
    /// gets the spec's alert copy.
    func recoverDeleted(path: String) async {
        guard let session = currentSession else { return }
        let result: Result<SaveReport, VaultError> =
            await Task.detached(priority: .userInitiated) {
                do {
                    return .success(try session.recoverDeletedFile(path: path))
                } catch let error as VaultError {
                    return .failure(error)
                } catch {
                    return .failure(.Io(message: error.localizedDescription))
                }
            }.value
        guard !Task.isCancelled, currentSession === session else { return }
        switch result {
        case .success:
            announcer.post("Restored \(filename(of: path)).", priority: .high)
            await loadFiles()
            await loadDeletedFiles()
        case .failure(.DestinationExists):
            historyAlert = HistoryAlert(
                title: "Can't restore",
                message:
                    "A file already exists at \(path). Rename or move it first, then restore."
            )
        case .failure(let error):
            historyAlert = HistoryAlert(
                title: "Can't restore", message: humanReadable(error))
        }
    }

    // MARK: - Settings bridge

    /// Persist + live-apply the retention window through the session
    /// (`.slate/prefs.json` `history` section; unknown keys preserved
    /// core-side). Errors surface on the history alert channel.
    func applyHistoryRetention(days: UInt32) async {
        guard let session = currentSession else { return }
        let result: Result<Void, VaultError> =
            await Task.detached(priority: .utility) {
                do {
                    try session.setHistoryPrefs(
                        prefs: HistoryPrefs(retentionDays: days))
                    return .success(())
                } catch let error as VaultError {
                    return .failure(error)
                } catch {
                    return .failure(.Io(message: error.localizedDescription))
                }
            }.value
        guard currentSession === session else { return }
        if case .failure(let error) = result {
            historyAlert = HistoryAlert(
                title: "Couldn't save history settings",
                message: humanReadable(error))
        }
    }

    /// The current retention setting, for Settings to seed its picker.
    func currentHistoryRetentionDays() -> UInt32 {
        currentSession?.historyPrefs().retentionDays ?? 90
    }

    /// Toggle handler: persists the host pref and mirrors it into the
    /// published property the funnel + panel read. Takes effect at the
    /// next note open (the funnel decides per-open).
    func setHistoryShowChangesSinceOpen(_ enabled: Bool) {
        historyShowChangesSinceOpen = enabled
        preferencesStore.saveHistoryShowChangesSinceOpen(enabled)
        if !enabled {
            sinceOpenChanges = nil
        }
    }

    // MARK: - Command surface

    /// `slate.history.showPanel`: activate the History leaf and move
    /// focus into the leaf region (menu + palette entry; the leaf-
    /// switch announcement covers AT).
    func showHistoryPanel() {
        if workspace.activeLeaf != .history {
            workspace.activeLeaf = .history
            postAccessibilityAnnouncement("History panel.", priority: .medium)
        }
        workspace.focusLeafRegion()
    }

    // MARK: - Vault event listener (O-2 channel, Mac half)

    func registerVaultEventListener(on session: VaultSession) {
        let adapter = VaultEventAdapter(appState: self)
        vaultEventAdapter = adapter
        vaultEventListenerToken = session.registerEventListener(listener: adapter)
    }

    func unregisterVaultEventListener() {
        if let token = vaultEventListenerToken, let session = currentSession {
            session.unregisterEventListener(token: token)
        }
        vaultEventListenerToken = nil
        vaultEventAdapter = nil
    }

    /// Main-actor handler for core events. One alert per (path,
    /// session): repeated failures on the same file stay quiet (the
    /// announcement-gate pattern), and the core's message is presented
    /// verbatim.
    func handleVaultEvent(code: EventErrorCode, path: String, message: String) {
        switch code {
        case .compactionFailed:
            guard !compactionAlertedPaths.contains(path) else { return }
            compactionAlertedPaths.insert(path)
            compactionFailure = CompactionFailure(path: path, message: message)
        }
    }
}
