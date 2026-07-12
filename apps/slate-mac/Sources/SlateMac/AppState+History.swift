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

/// One pending "Restore version?" confirmation. Everything the
/// perform step needs is CAPTURED at staging time (adversarial round
/// 2): reading selection-scoped state at confirmation time would
/// associate another note's buffer/hash with this request if the
/// selection changed between staging and confirming.
struct HistoryRestoreRequest: Identifiable, Equatable {
    let id = UUID()
    let path: String
    let versionHash: String
    /// Pre-formatted absolute date for the alert copy.
    let formattedDate: String
    /// The loaded document's hash at staging — the compare-and-swap
    /// guard restoreVersion runs against.
    let expectedContentHash: String
    /// The loaded buffer body at staging — what "Keep Mine" preserves
    /// if the restore conflicts.
    let attemptedBody: String
    /// Whether the buffer was dirty at staging (routes straight to
    /// the conflict flow, before touching disk).
    let bufferWasDirty: Bool
}

/// A history-specific error alert (integrity failure, recovery
/// collision) — kept off the generic save-error surface.
struct HistoryAlert: Identifiable, Equatable {
    let id = UUID()
    let title: String
    let message: String
}

/// A pending "Restore As…" destination prompt (#795).
struct RestoreAsPrompt: Identifiable, Equatable {
    enum Source: Equatable {
        /// Recover a deleted file's tail to a new path (the remnant
        /// log re-binds — history follows the file).
        case deletedFile(path: String)
        /// Materialize one VERSION of a live note as a new file (a
        /// copy; the original and its history are untouched).
        case version(path: String, hash: String, formattedDate: String)
    }

    let id = UUID()
    let source: Source
    /// Pre-filled, collision-avoiding destination suggestion.
    let suggestedPath: String
    /// Identity of the session the prompt was staged against
    /// (adversarial review): confirming after a vault switch must do
    /// NOTHING — a matching path/hash in the new vault would
    /// otherwise recover or copy unrelated data there.
    let sessionID: ObjectIdentifier
}

/// One compaction-failure event (the O-2 channel's Mac half). The
/// message is the core's copy, presented verbatim.
struct CompactionFailure: Identifiable, Equatable {
    let id = UUID()
    let path: String
    let message: String
}

/// uniffi callback adapter: the core invokes callbacks on worker
/// threads (the #802 methods may even arrive with session locks held);
/// hop to the main actor and hand the event to AppState — never call
/// back into the session synchronously. The weak reference means a
/// closed vault's straggler events go nowhere.
final class VaultEventAdapter: VaultEventListener, @unchecked Sendable {
    private weak var appState: AppState?
    /// #802 observation seams. Milestone P (#554) wired the first real
    /// production consumer here — the Connections leaf's graph-generation
    /// refresh (see `registerVaultEventListener`); PD (OCR label refresh,
    /// GC prompts) layers on later. Tests also observe through them so
    /// the uniffi conformance is exercised end to end.
    let onFileChangeHook: (@Sendable (FileChangeEvent) -> Void)?
    let onIndexPhaseHook: (@Sendable (IndexPhase, UInt64) -> Void)?

    init(
        appState: AppState,
        onFileChangeHook: (@Sendable (FileChangeEvent) -> Void)? = nil,
        onIndexPhaseHook: (@Sendable (IndexPhase, UInt64) -> Void)? = nil
    ) {
        self.appState = appState
        self.onFileChangeHook = onFileChangeHook
        self.onIndexPhaseHook = onIndexPhaseHook
    }

    func onError(code: EventErrorCode, path: String, message: String) {
        Task { @MainActor [weak appState] in
            appState?.handleVaultEvent(code: code, path: path, message: message)
        }
    }

    func onFileChange(event: FileChangeEvent) {
        onFileChangeHook?(event)
    }

    func onIndexPhase(phase: IndexPhase, filesSeen: UInt64) {
        onIndexPhaseHook?(phase, filesSeen)
    }
}

extension AppState {
    // MARK: - Note-open funnel

    /// Schedule a history load STRICTLY AFTER any in-flight one
    /// (adversarial round 2 High): without serialization, load B can
    /// compute its verdict against the old baseline while load A's
    /// post-guard `markOpened` is still in flight — B then publishes a
    /// verdict whose comparison point no longer matches the persisted
    /// baseline. Chaining on the previous task means a load's compute
    /// can never overlap another's mark. All production entry points
    /// (the selection funnel and the restore refresh) route through
    /// here; the publish guards inside remain the stale-drop defence.
    @discardableResult
    func scheduleHistoryLoad(path: String) -> Task<Void, Never> {
        // Flag at SCHEDULE time, not body-entry: the chained task may
        // sit behind a predecessor, and the panel must read "loading"
        // for that whole window. The window where it VISIBLY matters
        // is a genuinely-empty list — first load after a vault open
        // (the list starts empty; note switches latch the prior
        // note's rows until the new publish, deliberately no flash)
        // and marker-only histories.
        isHistoryLoading = true
        let previous = historyLoadTask
        let task = Task { [weak self] in
            await previous?.value
            await self?.loadHistoryForCurrentNote(path: path)
        }
        historyLoadTask = task
        return task
    }

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
            isHistoryLoading = false
            // The open is REAL (guards passed): move the baseline now,
            // AFTER the verdict — the pinned compute-then-mark order.
            // A selection switch DURING this mark is benign — the open
            // it records genuinely happened and its verdict was
            // published above; competing loads can't interleave with
            // it because every load is serialized through
            // scheduleHistoryLoad.
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
            isHistoryLoading = false
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
            // This publish holds the newest seq — the bump above just
            // staled any queued first-page reload, whose guard-return
            // deliberately leaves the flag alone. Clear it here or it
            // sticks true until the next reset (red-team F3 on the
            // HIG-audit pass).
            isHistoryLoading = false
        case .failure(.InvalidArgument):
            // Through the serialized chain, like every other entry
            // point — a direct call here could compute while a
            // scheduled load's mark is in flight (round 3 High).
            // (The re-schedule owns `isHistoryLoading`: sets on
            // schedule, clears on ITS publish.)
            await scheduleHistoryLoad(path: path).value
        case .failure(let error):
            historyLoadError = humanReadable(error)
            isHistoryLoading = false
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
        isHistoryLoading = false
        sinceOpenChanges = nil
        deletedFiles = []
        deletedLoadError = nil
        historyRestoreRequest = nil
        historyRestoreAsPrompt = nil
        historyLoadTask?.cancel()
        historyLoadTask = nil
    }

    // MARK: - Restore flow

    /// Row action: stage the confirmation alert, capturing the
    /// selection-scoped inputs NOW (see HistoryRestoreRequest).
    func requestRestore(versionHash: String, formattedDate: String) {
        guard let path = selectedFilePath, let expectedHash = currentNoteContentHash
        else { return }
        historyRestoreRequest = HistoryRestoreRequest(
            path: path, versionHash: versionHash, formattedDate: formattedDate,
            expectedContentHash: expectedHash,
            attemptedBody: currentNoteText ?? "",
            bufferWasDirty: hasUnsavedChanges)
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
        // The request is selection-scoped: if the user moved on
        // between staging and confirming, do nothing — never mix one
        // note's captured state with another's path (round 2 High).
        guard selectedFilePath == request.path, loadedFilePath == request.path
        else { return }
        let expectedHash = request.expectedContentHash

        if request.bufferWasDirty {
            // Buffer-dirty: same surface as an external-change save
            // conflict — keep-mine saves the CAPTURED buffer, reload
            // discards.
            currentSaveConflict = SaveConflict(
                path: request.path,
                attemptedContents: request.attemptedBody,
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
                await scheduleHistoryLoad(path: request.path).value
                historyFocusHeadToken &+= 1
            }
        case .failure(
            .WriteConflict(
                let currentContentHash, let expectedContentHash, let currentMtimeMs)):
            // "Mine" = the CAPTURED buffer body for request.path — the
            // same semantics as every other conflict producer: Keep
            // Mine writes my state back over the external change. The
            // capture (not a live read) matters twice over: a live
            // DISK re-read made Keep Mine a no-op (round 1), and a
            // live BUFFER read after the await could carry another
            // note's body if the selection switched mid-restore
            // (round 2). The resolver's own loadedFilePath guard
            // additionally no-ops a conflict whose note is gone.
            currentSaveConflict = SaveConflict(
                path: request.path,
                attemptedContents: request.attemptedBody,
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
            // #795: offer Restore As… straight from the collision —
            // the alert copy names the block, the prompt takes a new
            // destination.
            historyRestoreAsPrompt = RestoreAsPrompt(
                source: .deletedFile(path: path),
                suggestedPath: Self.restoredCopyPath(for: path),
                sessionID: ObjectIdentifier(session))
        case .failure(let error):
            historyAlert = HistoryAlert(
                title: "Can't restore", message: humanReadable(error))
        }
    }

    // MARK: - Restore As… (#795)

    /// Version-row entry point: materialize `versionHash` of the
    /// selected note as a new file.
    func requestRestoreAs(versionHash: String, formattedDate: String) {
        guard let path = selectedFilePath, let session = currentSession else { return }
        historyRestoreAsPrompt = RestoreAsPrompt(
            source: .version(path: path, hash: versionHash, formattedDate: formattedDate),
            suggestedPath: Self.restoredCopyPath(for: path),
            sessionID: ObjectIdentifier(session))
    }

    /// Confirmed Restore As…: write to `destination` through the
    /// no-clobber machinery. Success announces, refreshes, and lands
    /// selection (and thus focus) on the new file; a collision at the
    /// CHOSEN destination re-raises the prompt with the standard copy
    /// on the alert channel.
    func performRestoreAs(_ prompt: RestoreAsPrompt, destination: String) async {
        guard let session = currentSession,
            ObjectIdentifier(session) == prompt.sessionID
        else { return }
        let trimmed = destination.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else { return }
        let result: Result<Void, VaultError> =
            await Task.detached(priority: .userInitiated) {
                do {
                    switch prompt.source {
                    case .deletedFile(let path):
                        _ = try session.recoverDeletedFileAs(
                            path: path, destination: trimmed)
                    case .version(let path, let hash, _):
                        // versionContent is integrity-verified — wrong
                        // bytes are never served, so never written.
                        let content = try session.versionContent(
                            path: path, versionHash: hash)
                        _ = try session.createExclusive(
                            path: trimmed, content: content)
                    }
                    return .success(())
                } catch let error as VaultError {
                    return .failure(error)
                } catch {
                    return .failure(.Io(message: error.localizedDescription))
                }
            }.value
        guard currentSession === session else { return }

        switch result {
        case .success:
            let sourceName: String
            switch prompt.source {
            case .deletedFile(let path): sourceName = filename(of: path)
            case .version(_, _, let date): sourceName = "version from \(date)"
            }
            announcer.post(
                "Restored \(sourceName) as \(filename(of: trimmed)).", priority: .high)
            await loadFiles()
            await loadDeletedFiles()
            // Selection carries focus to the new file (the funnel).
            selectedFilePath = trimmed
        case .failure(.DestinationExists):
            historyAlert = HistoryAlert(
                title: "Can't restore",
                message:
                    "A file already exists at \(trimmed). Choose a different name.")
            historyRestoreAsPrompt = prompt
        case .failure(let error):
            historyAlert = HistoryAlert(
                title: "Can't restore", message: humanReadable(error))
        }
    }

    /// Collision-avoiding "<stem> (restored).md" suggestion against
    /// the indexed file set (pure over the published list; the
    /// no-clobber write still guards the race).
    static func restoredCopyPath(for path: String, existing: Set<String>? = nil)
        -> String
    {
        let ns = path as NSString
        let ext = ns.pathExtension.isEmpty ? "md" : ns.pathExtension
        let stem = ns.deletingPathExtension
        var candidate = "\(stem) (restored).\(ext)"
        var counter = 2
        while existing?.contains(candidate) == true {
            candidate = "\(stem) (restored \(counter)).\(ext)"
            counter += 1
        }
        return candidate
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
        focusLeafRegionRevealingPane()  // #882: un-hide the pane on reveal
    }

    // MARK: - Vault event listener (O-2 channel, Mac half)

    func registerVaultEventListener(on session: VaultSession) {
        // Milestone P (#552/#554) is the first production consumer of
        // the #802 observation seams: the Connections leaf (and later
        // graph surfaces) refresh on any committed mutation or scan
        // completion by re-probing graph_generation(). The hooks arrive
        // off the session's thread — they MUST NOT call the session
        // synchronously (the documented contract); marshal to the main
        // actor, where refreshConnectionsIfGraphChanged dispatches the
        // generation probe off-main again.
        let adapter = VaultEventAdapter(
            appState: self,
            onFileChangeHook: { [weak self] _ in
                Task { @MainActor [weak self] in
                    self?.refreshConnectionsIfGraphChanged()
                    self?.refreshGraphTableIfGraphChanged()
                }
            },
            onIndexPhaseHook: { [weak self] phase, _ in
                guard phase == .scanFinished else { return }
                Task { @MainActor [weak self] in
                    self?.refreshConnectionsIfGraphChanged()
                    self?.refreshGraphTableIfGraphChanged()
                }
            })
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
            if compactionAlertSuppressed {
                // #881: the user pressed "Don't Show Again" (alerts.md:36),
                // and this alert isn't actionable (alerts.md:27 — a
                // non-actionable alert must yield a non-interrupting path).
                // Drop the app-modal interruption but NOT the signal: route
                // the core's verbatim message to the polite (non-interrupting)
                // VoiceOver channel so o_spec §O-2's "never silent" contract
                // holds — a screen-reader user still learns compaction failed;
                // sighted users chose to stop the interruptions.
                announcer.post(message, priority: .medium)
            } else {
                compactionFailure = CompactionFailure(path: path, message: message)
            }
        }
    }

    /// Persist the user's "Don't Show Again" choice from the compaction-
    /// failure alert (#881, alerts.md:36). Future failures then route to the
    /// polite AX announcement in `handleVaultEvent` above instead of the
    /// app-modal alert — never weakening o_spec §O-2's never-silent contract.
    /// App-level pref (PreferencesStore), like `editorSpellCheckEnabled`.
    func suppressCompactionFailureAlert() {
        compactionAlertSuppressed = true
        preferencesStore.saveSuppressCompactionFailureAlert(true)
    }
}
