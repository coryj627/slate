// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import Combine
import Foundation
import os

/// Plan 43 PR 19 (#1333; contract 40 R-9 on mac, the twin of Windows'
/// `VaultLifecycleViewModel.Rescan.cs`): the rescan, the reconciliation a
/// file created, changed or removed outside Slate gets on mac. Files Sidebar
/// Refresh (`slate.sidebar.refresh`) runs it `.explicit`; the app coming back
/// to the foreground after being away runs it `.foreground`
/// (`ForegroundRescanRoute`, wired in `SlateMacApp.swift`); and since plan 43
/// PR 20c (#1267) core's vault watcher asks for it `.watcher`, once per
/// coalesced burst of changes made outside this session
/// (`handleExternalChange`, reached through `VaultEventAdapter`).
///
/// One run, in order. The scan: core's incremental rescan on a detached task
/// under the run's ONE cancel token, which the scan, every inventory page and
/// every indexed-hash chunk carry, and which a close or a vault switch cancels
/// (plan 43 PR 12's rule; locked decision 05 §4). The re-sync from the index
/// (`resyncFromIndex`): the host retains nothing between runs and every step
/// is awaited to its publication. Then exactly one core sentence:
/// `VaultRescanIncomplete` when the walk was partial, a file failed or a
/// re-sync operation failed, otherwise `VaultRescanFinished` with this scan's
/// counts, spoken always for an explicit Refresh and for a foreground rescan
/// only when something was new, changed or removed. Nothing speaks before it:
/// the progress an explicit Refresh shows on the scan strip never speaks (the
/// reason-aware progress policy), the re-sync publishes silently, and the keys
/// never move unless the row they were on is gone. The sentence itself follows
/// VoiceOver's own reading of a return to Slate or of the palette a Refresh
/// was chosen from closing (`sentenceSettle`): posted under that reading, it
/// was dropped (this PR's VoiceOver check).
///
/// One run at a time. A request that arrives while a run is active joins the
/// pending-reason lattice, Explicit > Foreground > Watcher, and is served by
/// ONE follow-up run: never dropped, and never subject to the foreground
/// cooldown, which applies only to starting a foreground run from idle. Only
/// the open scan, an import or a Trash operation refuses a request; a
/// follow-up they block stays pending and runs when they settle.
///
/// Plan 43 PR 20c (design `20_vault_watcher_design.md` §4.2 item 3, §4.5): a
/// Watcher request is never dropped. While the open scan, an import or a
/// Trash operation runs it is recorded in the lattice where Explicit and
/// Foreground are refused, and the blockers settling serve it; while the app
/// is inactive it waits for the next activation, where it joins the
/// foreground route's request (one run, Foreground reason, so the return
/// still says what changed while the user was away) or, when the route
/// forwards none, runs on its own. Core already spaced it, so the cooldown
/// never applies. Its sentence is the foreground policy's, gated: at most one
/// per `watcherSentenceSpacingMs()` per open vault, and none while a modal
/// surface is open; a run the gate holds back posts nothing, and its counts
/// are not carried to a later one.
extension RescanReason: Comparable {
    /// The pending-reason lattice's order. Core's `RescanReason` is the
    /// sentence's payload; the order is the host's, explicit here so each
    /// reason takes its rank where the join reads it: Explicit > Foreground >
    /// Watcher (plan 43 PR 20's design §4.2 item 2). Explicit dominates,
    /// Foreground with Watcher is Foreground, Watcher with Watcher stays
    /// Watcher.
    var latticeRank: Int {
        switch self {
        case .watcher: return 0
        case .foreground: return 1
        case .explicit: return 2
        }
    }

    public static func < (lhs: RescanReason, rhs: RescanReason) -> Bool {
        lhs.latticeRank < rhs.latticeRank
    }

    /// The join a request makes with the reason already pending: the higher
    /// rank wins, so Explicit dominates and nothing joins down.
    static func joined(_ pending: RescanReason?, _ arriving: RescanReason) -> RescanReason {
        pending.map { max($0, arriving) } ?? arriving
    }
}

/// The foreground route, the twin of Windows' `ForegroundRescanRoute`. A
/// return to Slate after being away rescans, silently unless something
/// changed, so a file another app made is there on return. Since plan 43 PR
/// 20c (#1267) core's vault watcher reconciles while the app is active; the
/// route stays the baseline for the time it was away, for network volumes and
/// for lost events (design §4.6), and after its own decision every activation
/// serves a watcher request the app's absence deferred
/// (`applicationDidBecomeActive`). The first activation never forwards (the
/// open scan just ran), nor does a return after under two seconds away or one
/// with a modal surface open (the palette, Quick Open, the search overlay or
/// any sheet). Everything else is the coalescer's: the cooldown, the blockers
/// and the lattice.
struct ForegroundRescanRoute {
    /// How long the app must have been away.
    static let minimumAway: TimeInterval = 2

    private(set) var hasActivated = false
    private(set) var resignedAt: Date?

    /// Observation can start after the first activation was posted (SwiftUI
    /// subscribes when the root view appears); an app already active then
    /// has had it.
    mutating func beginObserving(appIsActive: Bool) {
        if appIsActive { hasActivated = true }
    }

    /// The app lost the foreground: the time away starts.
    mutating func onResignActive(at now: Date) {
        resignedAt = now
    }

    /// The app became active. Whether to request a foreground rescan. The
    /// modal check reads AppKit's windows, so it runs on the main actor.
    @MainActor
    mutating func onBecomeActive(
        at now: Date, modalSurfaceOpen: @autoclosure @MainActor () -> Bool
    ) -> Bool {
        let resigned = resignedAt
        resignedAt = nil
        guard hasActivated else {
            hasActivated = true
            return false
        }
        guard let resigned, now.timeIntervalSince(resigned) >= Self.minimumAway else {
            return false
        }
        return !modalSurfaceOpen()
    }
}

/// What a rescan's reload of the active note did (`AppState.swift`'s
/// `reloadActiveNoteInPlaceForRescan`).
enum RescanActiveNoteReload: Equatable {
    /// The clean note took the indexed bytes in place.
    case reloaded
    /// Nothing to do: not the active clean note, already current, or edited
    /// or saved since the read (its own publication owns it).
    case unchanged
    /// The bytes read are not the ones the index vouches for (the read
    /// failed, or the disk moved past the index): the tab keeps its text, the
    /// run counts one failure, and the next rescan reloads it.
    case unvouched
}

/// The rescan's run state, owned by `AppState` (`rescanRun`). Every field is
/// written only here; the test seams are the facts' handles on the run.
@MainActor
final class VaultRescanRun {
    /// The running rescan and its coalesced follow-ups, for a fact to await.
    fileprivate(set) var task: Task<Void, Never>?
    fileprivate(set) var isActive = false
    /// The lattice's pending reason: what one follow-up run will serve.
    fileprivate(set) var pendingReason: RescanReason?
    /// The running scan's one cancel token (a close or a switch cancels it).
    fileprivate(set) var cancelToken: CancelToken?
    /// When the last rescan ended (the foreground cooldown's start).
    fileprivate(set) var lastScanEndedAt: Date?
    /// True while an explicit run's scan may still move the scan strip.
    fileprivate(set) var showsProgress = false
    /// When the app last became active (`ProcessInfo.systemUptime`):
    /// VoiceOver reads the returning window and focus then.
    fileprivate(set) var lastActivationUptime: TimeInterval?
    /// The running rescan was started, or joined, with a sheet up (the
    /// palette a Refresh was chosen from): its sentence waits for the sheet
    /// to end. The run's own: set only by a request that starts or joins it,
    /// cleared when its sentence posts and when it ends, spoken or not.
    fileprivate(set) var sentenceFollowsModal = false
    /// Plan 43 PR 20c (#1267, design §4.5 item 4): the app is not active
    /// (between `applicationDidResignActive` and the next activation, or
    /// observed inactive when observation began). No Watcher run starts then.
    fileprivate(set) var appIsInactive = false
    /// A watcher request the app's absence deferred: the next activation
    /// serves it, joined into the foreground route's request when the route
    /// forwards one. The open vault's own; a close or a switch forgets it.
    fileprivate(set) var watcherRequestWhileInactive = false
    /// When the open vault's last watcher sentence was posted (`scanClock`):
    /// the start of the sentence window (design §4.5 item 2). A close and a
    /// reopen start a new one.
    fileprivate(set) var lastWatcherSentenceAt: Date?
    fileprivate var generation: UInt64 = 0
    /// Waits for the blockers of a pending follow-up to settle.
    fileprivate var deferredFollowUp: AnyCancellable?
    fileprivate var foregroundRoute = ForegroundRescanRoute()
    private var treeRefreshers: [(id: UUID, refresh: @MainActor () async -> UInt64)] = []

    /// The mounted files tree's silent refetch, registered while the tree is
    /// mounted (`FileTreeSidebar`). It returns how many levels failed: shown
    /// in the tree, counted in the sentence, never spoken.
    @discardableResult
    func registerTreeRefresher(_ refresh: @escaping @MainActor () async -> UInt64) -> UUID {
        let id = UUID()
        treeRefreshers.append((id, refresh))
        return id
    }

    func unregisterTreeRefresher(_ id: UUID) {
        treeRefreshers.removeAll { $0.id == id }
    }

    /// The most recently mounted tree's refetch.
    fileprivate var treeRefresher: (@MainActor () async -> UInt64)? {
        treeRefreshers.last?.refresh
    }

    // MARK: Test seams

    /// Each core call that carries the run's token: its operation ("scan",
    /// "inventory", "hashes") and the token. Called on the worker.
    var coreCallObserver: (@Sendable (String, CancelToken) -> Void)?
    /// Awaited after the scan returns, before the re-sync (a fact parks a run
    /// here to send requests while it is active).
    var afterScanHook: (@MainActor () async -> Void)?
    /// The inventory's page size (a fact makes it small to prove the pages
    /// share the run's token).
    var inventoryPageLimit: UInt32 = 1_000
    /// The indexed-hash chunk; core's bound (`maxIndexedHashPaths()`) when nil.
    var indexedHashChunkLimit: Int?
    /// How long the sentence waits after the app becomes active, or after the
    /// sheet a request came from ends: VoiceOver drops an announcement posted
    /// while it starts reading the returning window or focus ("Closing
    /// dialog", the focused row), so the sentence follows that reading. The
    /// facts shorten it.
    var sentenceSettle: TimeInterval = 1.0
    /// Each run's reason as it starts (one pass of `runOneRescan`): which
    /// route ran, for the facts.
    var runStartObserver: (@MainActor (RescanReason) -> Void)?
    /// How many watcher signals reached `handleExternalChange`: the facts'
    /// handle on the adapter's hop to the main actor. Never reset.
    fileprivate(set) var watcherSignalsHandled: UInt64 = 0
}

extension AppState {
    /// A foreground request that would START a run within this long of the
    /// last rescan's end is skipped; a request during a running rescan is
    /// never subject to it (Windows' `ForegroundRescanCooldown`).
    static let foregroundRescanCooldown: TimeInterval = 5

    /// Refresh's reason while the open scan runs. Catalog availability copy,
    /// spoken through the structural gate's existing announcement site (the
    /// shared `postMutationAnnouncement` residue); Windows gives its generic
    /// "This command is not available right now." here.
    static let rescanScanInProgressReason = "Wait for the current scan to finish."

    /// Plan 43 PR 20c (design §4.5 item 2, owner decision Q2): at most one
    /// watcher sentence per this long per open vault. Core's one number
    /// (`watcherSentenceSpacingMs()`), so both hosts agree.
    static let watcherSentenceSpacing = TimeInterval(watcherSentenceSpacingMs()) / 1_000

    var vaultRescanTask: Task<Void, Never>? { rescanRun.task }
    var isRescanning: Bool { rescanRun.isActive }

    /// Why a rescan request would be refused now, or nil: the open scan, an
    /// import or a Trash operation in flight. A running rescan is joined, not
    /// refused. Refresh's availability on every surface.
    var rescanBlockedReason: String? {
        if currentImportBatchOwner != nil || trashProgress != nil {
            return Self.structuralMutationBusyReason
        }
        if isScanning { return Self.rescanScanInProgressReason }
        return nil
    }

    // MARK: - Requests

    /// Request a rescan of the open vault for `reason` (the coalescer).
    /// Joined to the running rescan's lattice when one is active; refused,
    /// silently, with no vault or while blocked; skipped for a foreground
    /// request inside the cooldown. A Watcher request is recorded where the
    /// others are refused, and deferred while the app is inactive (plan 43
    /// PR 20c). Returns the run that will serve it.
    @discardableResult
    func requestRescan(_ reason: RescanReason) -> Task<Void, Never>? {
        guard let session = currentSession else { return nil }
        // A watcher request the app's absence deferred joins the foreground
        // route's request on activation (design §4.5 item 4): one run,
        // Foreground reason, with the watcher request's guarantees — never
        // dropped, never subject to the cooldown.
        var carriesWatcher = reason == .watcher
        if reason == .foreground, rescanRun.watcherRequestWhileInactive {
            rescanRun.watcherRequestWhileInactive = false
            carriesWatcher = true
        }
        // No Watcher run while the app is inactive: the request waits for
        // the next activation (`serveWatcherRequestOnActivation`).
        if reason == .watcher, rescanRun.appIsInactive {
            rescanRun.watcherRequestWhileInactive = true
            return nil
        }
        if rescanRun.isActive {
            rescanRun.pendingReason = RescanReason.joined(rescanRun.pendingReason, reason)
            // Joined from a sheet: the run's sentence waits for it too. A
            // request refused or skipped below joins nothing and marks
            // nothing, and the watcher's request comes from no sheet.
            if reason != .watcher, rescanModalSheetIsUp { rescanRun.sentenceFollowsModal = true }
            return rescanRun.task
        }
        guard rescanBlockedReason == nil else {
            // The open scan, an import or a Trash operation in flight: a
            // request carrying a watcher's waits for them in the lattice
            // (design §4.2 item 3); Explicit and Foreground are refused, as
            // before.
            if carriesWatcher {
                rescanRun.pendingReason = RescanReason.joined(rescanRun.pendingReason, reason)
                deferFollowUpUntilUnblocked()
            }
            return nil
        }
        if reason == .foreground, !carriesWatcher, rescanRun.pendingReason == nil,
            let ended = rescanRun.lastScanEndedAt,
            scanClock().timeIntervalSince(ended) < Self.foregroundRescanCooldown
        {
            return nil
        }
        // A follow-up still waiting for its blockers is served by this run.
        let joined = RescanReason.joined(rescanRun.pendingReason, reason)
        rescanRun.pendingReason = nil
        return startRescanRun(joined, session: session)
    }

    // MARK: - The vault watcher

    /// Plan 43 PR 20c (#1267): core's vault watcher saw files change outside
    /// this session — one call per coalesced burst, from its own thread
    /// ("slate-watch"), with no session lock held and no paths, hopped here
    /// by `VaultEventAdapter` under its `currentSession === session` guard.
    /// It asks for a Watcher rescan: run now when the app is active; deferred
    /// while it is inactive (the next activation serves it, joined into the
    /// foreground route's request); recorded while the open scan, an import
    /// or a Trash operation runs; joined to a running rescan's lattice —
    /// never dropped.
    @discardableResult
    func handleExternalChange() -> Task<Void, Never>? {
        rescanRun.watcherSignalsHandled &+= 1
        return requestRescan(.watcher)
    }

    /// The app was activated, after the foreground route's own decision: a
    /// watcher request the app's absence deferred runs now, as a Watcher run
    /// — unless the foreground request the route forwarded first already took
    /// it (one run, Foreground reason). The run, or nil when nothing was
    /// deferred.
    private func serveWatcherRequestOnActivation() -> Task<Void, Never>? {
        guard rescanRun.watcherRequestWhileInactive else { return nil }
        rescanRun.watcherRequestWhileInactive = false
        return requestRescan(.watcher)
    }

    /// A Watcher follow-up due while the app is inactive waits for the next
    /// activation instead of running (design §4.5 item 4). True when it was
    /// deferred.
    private func deferWatcherFollowUpWhileInactive(_ next: RescanReason) -> Bool {
        guard next == .watcher, rescanRun.appIsInactive else { return false }
        rescanRun.pendingReason = nil
        rescanRun.watcherRequestWhileInactive = true
        return true
    }

    /// Design §4.6: the open's `startWatching()` armed no watch — an
    /// unsupported volume, a platform limit, a root that no longer names the
    /// opened folder, or the test switch. A diagnostic in the unified log,
    /// with no path (#507), and never spoken: nothing the user hears or sees
    /// changes. The unified log rather than `NSLog`, which also writes to
    /// stderr, where the test suite's switch would add a line per open.
    nonisolated static func logVaultWatchUnavailable() {
        Logger(subsystem: "com.startingblind.slate", category: "vault-watch").notice(
            "Slate: no vault watch for this vault; Refresh and the foreground rescan reconcile it"
        )
    }

    /// Design §4.5 items 2 and 3: a watcher sentence never floods a screen
    /// reader. None is posted while a modal surface is open (the foreground
    /// route's predicate), and at most one per `watcherSentenceSpacing` per
    /// open vault: a run whose sentence would fall inside the window posts
    /// nothing, and its counts are not carried to a later one — the tree and
    /// Quick Open are current, and Refresh speaks its own scan's counts. A
    /// sentence the modal check holds back does not start the window.
    private func admitWatcherSentence() -> Bool {
        if foregroundRescanModalSurfaceOpen { return false }
        let now = scanClock()
        if let last = rescanRun.lastWatcherSentenceAt,
            now.timeIntervalSince(last) < Self.watcherSentenceSpacing
        {
            return false
        }
        rescanRun.lastWatcherSentenceAt = now
        return true
    }

    // MARK: - The foreground route

    /// The root view started observing activation (`SlateMacApp.swift`). An
    /// app not active then defers Watcher runs until its first activation.
    func beginForegroundRescanObservation(appIsActive: Bool) {
        rescanRun.foregroundRoute.beginObserving(appIsActive: appIsActive)
        rescanRun.appIsInactive = !appIsActive
    }

    func applicationDidResignActive() {
        rescanRun.foregroundRoute.onResignActive(at: scanClock())
        rescanRun.appIsInactive = true
    }

    /// The app became active: a foreground rescan when the route forwards
    /// one — which takes a watcher request the app's absence deferred — and
    /// otherwise that deferred request as a Watcher run of its own (plan 43
    /// PR 20c). The run, or nil when neither ran.
    @discardableResult
    func applicationDidBecomeActive() -> Task<Void, Never>? {
        rescanRun.lastActivationUptime = ProcessInfo.processInfo.systemUptime
        rescanRun.appIsInactive = false
        let forwarded =
            rescanRun.foregroundRoute.onBecomeActive(
                at: scanClock(), modalSurfaceOpen: foregroundRescanModalSurfaceOpen)
            ? requestRescan(.foreground) : nil
        let deferred = serveWatcherRequestOnActivation()
        return forwarded ?? deferred
    }

    /// The palette, Quick Open and the search overlay, or any sheet or
    /// app-modal window: a modal surface the foreground route waits behind.
    /// Main-actor state, as `AppState` is: read synchronously in
    /// `applicationDidBecomeActive`, through the route's main-actor check.
    var foregroundRescanModalSurfaceOpen: Bool {
        if isCommandPaletteOpen || isQuickSwitcherOpen || isSearchOpen { return true }
        guard let app = NSApp else { return false }
        return app.modalWindow != nil || app.windows.contains { $0.attachedSheet != nil }
    }

    /// A sheet or modal window up, or the palette or Quick Open open: what
    /// the run's sentence waits to end. Main-actor state, as `AppState` is:
    /// its readers (`requestRescan`, `startRescanRun` and the sentence's poll,
    /// which resumes on the main actor after each sleep) are all isolated
    /// members, and no detached or worker path reads it.
    private var rescanModalSheetIsUp: Bool {
        if isCommandPaletteOpen || isQuickSwitcherOpen { return true }
        guard let app = NSApp else { return false }
        return app.modalWindow != nil || app.windows.contains { $0.attachedSheet != nil }
    }

    // MARK: - Teardown

    /// A close or a vault switch: the run's token is cancelled and the run is
    /// forgotten, so a late continuation of it publishes and speaks nothing.
    /// The vault's deferred watcher request and its watcher sentence window
    /// go with it (the app's activation state does not).
    func cancelRescanForVaultTransition() {
        rescanRun.generation &+= 1
        rescanRun.cancelToken?.cancel()
        rescanRun.cancelToken = nil
        rescanRun.task?.cancel()
        rescanRun.isActive = false
        rescanRun.pendingReason = nil
        rescanRun.lastScanEndedAt = nil
        rescanRun.deferredFollowUp = nil
        rescanRun.showsProgress = false
        rescanRun.sentenceFollowsModal = false
        rescanRun.watcherRequestWhileInactive = false
        rescanRun.lastWatcherSentenceAt = nil
    }

    // MARK: - The run

    private func startRescanRun(_ reason: RescanReason, session: VaultSession) -> Task<Void, Never> {
        rescanRun.isActive = true
        rescanRun.deferredFollowUp = nil
        // The run's modal mark starts from the sheet up now (the palette a
        // Refresh was chosen from), never from an earlier request's; the
        // watcher's request comes from no sheet.
        rescanRun.sentenceFollowsModal = reason != .watcher && rescanModalSheetIsUp
        rescanRun.generation &+= 1
        let generation = rescanRun.generation
        let task = Task { [weak self] in
            guard let self else { return }
            await self.runRescans(from: reason, session: session, generation: generation)
        }
        rescanRun.task = task
        return task
    }

    private func isCurrentRescan(_ session: VaultSession, _ generation: UInt64) -> Bool {
        currentSession === session && rescanRun.generation == generation && !Task.isCancelled
    }

    /// The run and its follow-ups. A follow-up an import or a Trash operation
    /// began meanwhile blocks stays pending and runs when they settle; a
    /// Watcher follow-up due while the app is inactive waits for its return.
    private func runRescans(
        from first: RescanReason, session: VaultSession, generation: UInt64
    ) async {
        var reason = first
        while true {
            await runOneRescan(reason, session: session, generation: generation)
            guard isCurrentRescan(session, generation),
                let next = rescanRun.pendingReason
            else { break }
            if rescanBlockedReason != nil {
                deferFollowUpUntilUnblocked()
                break
            }
            if deferWatcherFollowUpWhileInactive(next) { break }
            rescanRun.pendingReason = nil
            reason = next
        }
        guard rescanRun.generation == generation else { return }
        rescanRun.isActive = false
        // Spoken or silent, the run's mark ends with it. A follow-up still
        // pending for its blockers starts a run of its own, marked afresh.
        rescanRun.sentenceFollowsModal = false
        rescanRun.lastScanEndedAt = scanClock()
    }

    /// A pending follow-up waits for the blockers: every publish of AppState
    /// re-checks them on the next main-actor turn (a published value changes
    /// after its will-change signal).
    private func deferFollowUpUntilUnblocked() {
        let generation = rescanRun.generation
        rescanRun.deferredFollowUp = objectWillChange.sink { [weak self] _ in
            Task { @MainActor [weak self] in
                self?.serveDeferredFollowUp(generation: generation)
            }
        }
    }

    private func serveDeferredFollowUp(generation: UInt64) {
        guard rescanRun.generation == generation,
            rescanRun.deferredFollowUp != nil,
            !rescanRun.isActive,
            let pending = rescanRun.pendingReason,
            let session = currentSession,
            rescanBlockedReason == nil
        else { return }
        rescanRun.deferredFollowUp = nil
        // A recorded Watcher request whose blockers settled while the app is
        // inactive waits for its return instead.
        if deferWatcherFollowUpWhileInactive(pending) { return }
        rescanRun.pendingReason = nil
        // A recorded request: never subject to the cooldown.
        _ = startRescanRun(pending, session: session)
    }

    private func runOneRescan(
        _ reason: RescanReason, session: VaultSession, generation: UInt64
    ) async {
        rescanRun.runStartObserver?(reason)
        // ONE token for the whole run: the scan, every inventory page and
        // every indexed-hash chunk take it. The task's cancellation reaches it
        // through the handlers (a detached task inherits nothing), and a close
        // or a vault switch cancels it directly.
        let cancel = CancelToken()
        rescanRun.cancelToken = cancel
        defer {
            if rescanRun.cancelToken === cancel { rescanRun.cancelToken = nil }
        }
        let observer = rescanRun.coreCallObserver
        // Only an explicit Refresh shows progress, on the scan strip; nothing
        // it hears speaks.
        let progress: ScanProgressAdapter? =
            reason == .explicit
            ? ScanProgressAdapter { [weak self] event in
                Task { @MainActor [weak self] in
                    guard let self, self.isCurrentRescan(session, generation),
                        self.rescanRun.showsProgress
                    else { return }
                    self.handleRescanProgress(event)
                }
            }
            : nil
        rescanRun.showsProgress = progress != nil

        let report: ScanReport
        do {
            report = try await withTaskCancellationHandler {
                try await Task.detached(priority: .userInitiated) {
                    observer?("scan", cancel)
                    if let progress {
                        return try session.rescanWithProgress(cancel: cancel, listener: progress)
                    }
                    return try session.rescan(cancel: cancel)
                }.value
            } onCancel: {
                cancel.cancel()
            }
        } catch {
            guard isCurrentRescan(session, generation), !cancel.isCancelled() else { return }
            endRescanProgress()
            if case VaultError.Cancelled = error { return }
            // The call threw before any report existed: an honest "results
            // may be incomplete", never silence and never "No changes".
            await postRescanSentence(
                .vaultRescanIncomplete(errors: 1), reason: reason, session: session,
                generation: generation, cancel: cancel)
            return
        }
        guard isCurrentRescan(session, generation), !cancel.isCancelled() else { return }
        endRescanProgress()

        if let hook = rescanRun.afterScanHook { await hook() }
        guard isCurrentRescan(session, generation), !cancel.isCancelled() else { return }

        let failed = await resyncFromIndex(
            session: session, cancel: cancel, reason: reason, report: report,
            generation: generation)
        guard isCurrentRescan(session, generation), !cancel.isCancelled() else { return }

        // The one sentence. The scan's error count crosses the FFI in full;
        // each failed re-sync operation adds one. A Watcher run speaks on the
        // foreground policy, through the watcher's gate (plan 43 PR 20c,
        // `postRescanSentence`).
        let sentence: A11yEvent
        if !report.complete || failed > 0 {
            let (sum, overflow) = report.errorCount.addingReportingOverflow(failed)
            sentence = .vaultRescanIncomplete(errors: max(1, overflow ? .max : sum))
        } else if reason == .explicit || report.filesChanged + report.filesRemoved > 0 {
            sentence = .vaultRescanFinished(
                reason: reason, changed: report.filesChanged, removed: report.filesRemoved)
        } else {
            return
        }
        await postRescanSentence(
            sentence, reason: reason, session: session, generation: generation, cancel: cancel)
    }

    /// The scan returned: the strip it moved goes, and a straggler event
    /// cannot bring it back.
    private func endRescanProgress() {
        guard rescanRun.showsProgress else { return }
        rescanRun.showsProgress = false
        handleRescanProgress(nil)
    }

    /// The run's one completion sentence, rendered by core. It follows
    /// VoiceOver's own reading of a return to Slate or of a closing sheet
    /// (`sentenceSettle`), so that reading never drops it, and a run
    /// cancelled meanwhile says nothing. A Watcher pass's sentence passes the
    /// watcher's gate (`admitWatcherSentence`) and never waits for a sheet:
    /// one due while a modal surface is open is dropped, not deferred, and
    /// the run's mark stays for the request that joined it from the sheet.
    private func postRescanSentence(
        _ event: A11yEvent, reason: RescanReason, session: VaultSession, generation: UInt64,
        cancel: CancelToken
    ) async {
        let settle = rescanRun.sentenceSettle
        let followsModal = reason != .watcher && rescanRun.sentenceFollowsModal
        if settle > 0 {
            var since = rescanRun.lastActivationUptime
            if followsModal {
                // The sheet the request came from may still be closing.
                var polls = 0
                while rescanModalSheetIsUp, polls < 100 {
                    try? await Task.sleep(nanoseconds: 50_000_000)
                    polls += 1
                }
                since = ProcessInfo.processInfo.systemUptime
            }
            if let since {
                let remaining = settle - (ProcessInfo.processInfo.systemUptime - since)
                if remaining > 0 {
                    try? await Task.sleep(nanoseconds: UInt64(remaining * 1_000_000_000))
                }
            }
        }
        guard isCurrentRescan(session, generation), !cancel.isCancelled() else { return }
        if reason == .watcher {
            guard admitWatcherSentence() else { return }
        } else {
            rescanRun.sentenceFollowsModal = false
        }
        announcer.post(event)
    }

    // MARK: - The re-sync from the index

    /// R-9's re-sync (AR-18's fallback): every surface the host shows is
    /// re-synchronized from what the index holds now, whoever indexed it, and
    /// each step is awaited to its publication. Nothing speaks and nothing is
    /// retained between runs. Returns how many operations failed (each counts
    /// one error in the sentence). The one function PR 21's change feed
    /// narrows (#1444), with this full re-sync as its fallback.
    ///
    /// (a) The files inventory, the openable documents Quick Open lists, read
    /// in pages under the run's token. (b) The files tree's silent refetch and
    /// the Tags section. (c) The open tabs by their indexed hashes: a path
    /// with no index row is probed on disk (an unindexed file that exists is
    /// never marked missing), a respelled one re-seats onto the stored
    /// spelling, a gone one marks its clean tabs missing, a clean tab whose
    /// hash changed reloads in place, a dirty one never does, and every
    /// missing tab whose note is back re-seats (PR 10). (d) The active note's
    /// leaves. A foreground or Watcher run that found nothing new, changed or
    /// removed, in an inventory that did not change either, leaves (b) and
    /// (d) as they are: their refetch is visible (the tree collapses and
    /// re-expands its loaded folders) and would flicker on every return to
    /// Slate and every burst another app's writes make.
    func resyncFromIndex(
        session: VaultSession,
        cancel: CancelToken,
        reason: RescanReason,
        report: ScanReport,
        generation: UInt64
    ) async -> UInt64 {
        var failed: UInt64 = 0

        // (a) The files inventory.
        let inventory = await resyncFileInventory(
            session: session, cancel: cancel, generation: generation)
        guard isCurrentRescan(session, generation), !cancel.isCancelled() else { return 0 }
        failed += inventory.failed
        let surfacesMayHaveChanged =
            reason == .explicit
            || !report.complete
            || report.filesChanged + report.filesRemoved > 0
            || inventory.changed

        // (b) The files tree, silently.
        if surfacesMayHaveChanged {
            refreshSidebarTagTree()
            if let refresh = rescanRun.treeRefresher {
                failed += await refresh()
            }
            guard isCurrentRescan(session, generation), !cancel.isCancelled() else { return 0 }
        }

        // (c) The open tabs.
        let tabs = await resyncOpenTabsFromIndex(
            session: session, cancel: cancel, generation: generation)
        guard isCurrentRescan(session, generation), !cancel.isCancelled() else { return 0 }
        failed += tabs.failed

        // (d) The active note's leaves.
        if surfacesMayHaveChanged || tabs.reloadedActiveNote {
            await refreshActiveNoteLeavesForRescan(contentChanged: tabs.reloadedActiveNote)
        }
        return failed
    }

    private func resyncFileInventory(
        session: VaultSession, cancel: CancelToken, generation: UInt64
    ) async -> (failed: UInt64, changed: Bool) {
        let observer = rescanRun.coreCallObserver
        let limit = max(1, rescanRun.inventoryPageLimit)
        do {
            let loaded: [FileSummary] = try await withTaskCancellationHandler {
                try await Task.detached(priority: .userInitiated) {
                    var all: [FileSummary] = []
                    var cursor: String?
                    repeat {
                        observer?("inventory", cancel)
                        let page = try session.listFiles(
                            filter: .openableDocuments,
                            paging: Paging(cursor: cursor, limit: limit),
                            cancel: cancel)
                        all.append(contentsOf: page.items)
                        cursor = page.nextCursor
                    } while cursor != nil
                    return all
                }.value
            } onCancel: {
                cancel.cancel()
            }
            guard isCurrentRescan(session, generation), !cancel.isCancelled() else {
                return (0, false)
            }
            return (0, publishRescanInventory(loaded))
        } catch {
            // A cancelled run says nothing; a failed listing keeps the list
            // Quick Open has and counts one error.
            guard isCurrentRescan(session, generation), !cancel.isCancelled() else {
                return (0, false)
            }
            return (1, false)
        }
    }

    private struct TabResync {
        var failed: UInt64 = 0
        var reloadedActiveNote = false
    }

    /// The distinct Markdown paths the workspace's tabs show, in order.
    private func rescanMarkdownTabPaths() -> [String] {
        var seen = Set<String>()
        var paths: [String] = []
        for tab in workspace.model.allTabs {
            guard case .markdown(let path) = tab.item, !path.isEmpty,
                seen.insert(path).inserted
            else { continue }
            paths.append(path)
        }
        return paths
    }

    private func resyncOpenTabsFromIndex(
        session: VaultSession, cancel: CancelToken, generation: UInt64
    ) async -> TabResync {
        var outcome = TabResync()
        let paths = rescanMarkdownTabPaths()
        if !paths.isEmpty {
            var hashes: [String: String]
            do {
                hashes = try await indexedContentHashesForRescan(
                    paths, session: session, cancel: cancel)
            } catch {
                guard isCurrentRescan(session, generation), !cancel.isCancelled() else {
                    return TabResync()
                }
                // Without the hashes no tab can be compared: one failure.
                outcome.failed += 1
                return outcome
            }
            guard isCurrentRescan(session, generation), !cancel.isCancelled() else {
                return TabResync()
            }

            // A path the index has no row for: what is on disk under it.
            var respelled: [(old: String, stored: String)] = []
            var gone: [String] = []
            for path in paths where hashes[path] == nil {
                let stored: String?
                do {
                    stored = try canonicalPathProbe(session, path)
                } catch {
                    // An identity core cannot read is kept, never marked
                    // missing on a guess (contract 32 I7), and counted.
                    outcome.failed += 1
                    continue
                }
                guard let stored else {
                    gone.append(path)
                    continue
                }
                // An unindexed file that exists under this spelling.
                if BaseExactIdentity.matches(stored, path) { continue }
                respelled.append((path, stored))
            }

            // A respelled note (a case-only rename outside Slate, core's
            // Deleted plus Created) is not missing: every tab on it takes the
            // stored spelling by a rename's steps (PR 10's re-seat), buffers
            // kept, and is then compared under that spelling.
            if !respelled.isEmpty {
                for entry in respelled {
                    retargetTabsForReseat(from: entry.old, to: entry.stored)
                }
                do {
                    let more = try await indexedContentHashesForRescan(
                        respelled.map(\.stored), session: session, cancel: cancel)
                    hashes.merge(more) { _, new in new }
                } catch {
                    guard isCurrentRescan(session, generation), !cancel.isCancelled() else {
                        return TabResync()
                    }
                    outcome.failed += 1
                }
                guard isCurrentRescan(session, generation), !cancel.isCancelled() else {
                    return TabResync()
                }
            }

            // A gone note: its clean tabs are marked missing, silently. A
            // dirty buffer is never touched; its next save meets the file's
            // absence and says so.
            for path in gone {
                markCleanTabsMissingForRescan(path: path)
            }
            if let load = noteLoadTask { await load.value }
            guard isCurrentRescan(session, generation), !cancel.isCancelled() else {
                return TabResync()
            }

            // A clean tab whose indexed hash changed reloads; a dirty one
            // never does (its next save is a compare-and-swap that conflicts).
            for path in rescanMarkdownTabPaths() {
                guard let indexHash = hashes[path] else { continue }
                switch await reloadActiveNoteInPlaceForRescan(
                    session: session, path: path, indexHash: indexHash)
                {
                case .reloaded:
                    outcome.reloadedActiveNote = true
                case .unvouched:
                    outcome.failed += 1
                case .unchanged:
                    break
                }
                guard isCurrentRescan(session, generation), !cancel.isCancelled() else {
                    return TabResync()
                }
                if await !reloadParkedTabsForRescan(
                    session: session, path: path, indexHash: indexHash)
                {
                    outcome.failed += 1
                }
                guard isCurrentRescan(session, generation), !cancel.isCancelled() else {
                    return TabResync()
                }
            }
        }

        // Missing tabs whose note is back re-seat (PR 10's sweep).
        reseatMissingTabs()
        if let load = noteLoadTask { await load.value }
        return outcome
    }

    /// The index's committed content hash of each path that has a row, read
    /// in chunks of core's bound under the run's token, on the worker.
    private func indexedContentHashesForRescan(
        _ paths: [String], session: VaultSession, cancel: CancelToken
    ) async throws -> [String: String] {
        let chunk = max(1, rescanRun.indexedHashChunkLimit ?? Int(maxIndexedHashPaths()))
        let observer = rescanRun.coreCallObserver
        return try await withTaskCancellationHandler {
            try await Task.detached(priority: .userInitiated) {
                var found: [String: String] = [:]
                var start = 0
                while start < paths.count {
                    let slice = Array(paths[start..<min(start + chunk, paths.count)])
                    observer?("hashes", cancel)
                    let hashes = try session.indexedContentHashes(paths: slice, cancel: cancel)
                    for (path, hash) in zip(slice, hashes) {
                        if let hash { found[path] = hash }
                    }
                    start += chunk
                }
                return found
            }.value
        } onCancel: {
            cancel.cancel()
        }
    }

    /// A note gone from disk under every spelling: the active tab, when
    /// clean, takes the load-error state through the same-path reload (the
    /// read fails; nothing is lost), and every clean parked document on the
    /// path is dropped, so its next activation reads the disk and fails the
    /// same way. A dirty buffer is never touched.
    private func markCleanTabsMissingForRescan(path: String) {
        let activeID = workspace.model.activeGroup.activeTabID
        let cleanParked = workspace.model.allTabs.compactMap { tab -> TabID? in
            guard tab.id != activeID, case .markdown(let tabPath) = tab.item,
                BaseExactIdentity.matches(tabPath, path),
                let document = workspace.document(for: tab.id),
                !document.hasUnsavedChanges
            else { return nil }
            return tab.id
        }
        workspace.invalidateParkedDocuments(tabIDs: cleanParked)
        if activeNoteIsCleanForRescan(at: path) {
            reloadActiveNoteForReseat(path: path)
        }
    }

    /// Every clean parked document on `path` whose hash is not the index's
    /// reloads in place from one read of the note, applied only when the bytes
    /// are the ones the index vouches for. False when a tab could not be
    /// vouched for (the read failed or the disk moved past the index): it
    /// keeps its text and the next rescan reloads it.
    private func reloadParkedTabsForRescan(
        session: VaultSession, path: String, indexHash: String
    ) async -> Bool {
        func stale() -> [NoteDocument] {
            let activeID = workspace.model.activeGroup.activeTabID
            return workspace.documents.compactMap { id, document in
                id != activeID
                    && BaseExactIdentity.matches(document.path, path)
                    && document.hasLoaded
                    && !document.hasUnsavedChanges
                    && !document.isMissingFromDisk
                    && document.saveConflict == nil
                    && !BaseExactIdentity.matches(document.contentHash, indexHash)
                    ? document : nil
            }
        }
        guard !stale().isEmpty else { return true }
        let parts: NotePartsBundle? = await Task.detached(priority: .userInitiated) {
            try? session.readNoteParts(path: path)
        }.value
        guard let parts, BaseExactIdentity.matches(parts.contentHash, indexHash) else {
            return false
        }
        workspace.reloadCleanDocuments(stale(), with: parts)
        return true
    }
}
