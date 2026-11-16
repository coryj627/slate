// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

/// Plan 43 PR 29 §3: one tab's buffer as the writer sees it, read from the
/// active fields while the tab is the loaded active tab, else from its
/// parked `NoteDocument`.
struct HotExitBuffer: Equatable {
    /// Which store the buffer was read from and its revision there: a cheap
    /// change signal, so a drain never compares whole bodies (red team PR
    /// 29.2). Every write to a buffer field bumps it.
    struct Stamp: Equatable {
        let active: Bool
        let revision: UInt64
        /// The parked document the revision counts for: a rename builds a
        /// new document whose count starts again (Codex adversarial review,
        /// round 3). Nil for the active fields.
        let document: ObjectIdentifier?
        let path: String
    }

    let stamp: Stamp
    let path: String
    let body: String
    let fmSource: String
    let baselineHash: String?
    let baselineIdentity: BaselineIdentity
    let dirty: Bool

    /// The parts a kept entry's baseline is made of (W§0's snapshot tuple
    /// without the text).
    func sameBaseline(_ other: HotExitBuffer) -> Bool {
        BaseExactIdentity.matches(path, other.path)
            && baselineHash == other.baselineHash
            && baselineIdentity.fileIdentity == other.baselineIdentity.fileIdentity
    }

    /// The whole snapshot: text and baseline.
    func sameSnapshot(_ other: HotExitBuffer) -> Bool {
        sameBaseline(other)
            && BaseExactIdentity.matches(body, other.body)
            && BaseExactIdentity.matches(fmSource, other.fmSource)
    }
}

/// What the writer reads its tabs from (`AppState`).
@MainActor
protocol HotExitBufferSource: AnyObject {
    /// Every Markdown tab in the workspace, in model order.
    var hotExitMarkdownTabs: [TabID] { get }
    /// The tab's note path, nil when the tab is gone or not Markdown.
    func hotExitTabPath(_ tab: TabID) -> String?
    /// Whether the tab is still in the model, whatever it holds.
    func hotExitTabExists(_ tab: TabID) -> Bool
    /// The tab's loaded buffer, nil while it has none.
    func hotExitBuffer(for tab: TabID) -> HotExitBuffer?
    /// The id the tab's claims, writes and retirements use (§2).
    func hotExitEntryId(for tab: TabID) -> String
    /// Gives a tab still in the model a fresh recovery id, so its next
    /// episode never claims over an abandoned entry under its old id.
    func hotExitAssignFreshEntryId(_ tab: TabID)
}

/// How a vault's store was opened (§2). Inert: no claim, no write.
struct HotExitOpen {
    let key: HotExitVaultKey?
    let inert: Bool
    /// Core's reason there is no hot exit (inert), nil for an uninstalled
    /// host: today's prompt, silently.
    let reason: String?
    let epoch: UInt64

    static func none(_ reason: String?) -> HotExitOpen {
        HotExitOpen(key: nil, inert: true, reason: reason, epoch: 0)
    }

    /// A read that returned `Damaged`: inert, but Discard All repairs it.
    static func damaged(_ key: HotExitVaultKey, reason: String) -> HotExitOpen {
        HotExitOpen(key: key, inert: true, reason: reason, epoch: 0)
    }

    static func active(_ key: HotExitVaultKey, epoch: UInt64) -> HotExitOpen {
        HotExitOpen(key: key, inert: false, reason: nil, epoch: epoch)
    }

    var activeKey: HotExitVaultKey? { inert ? nil : key }
}

enum HotExitSettleMode {
    /// Issues every dirty tab's latest snapshot and waits for all.
    case keep
    /// After a committed Discard All: no claim, no write.
    case retireOnly
}

/// What a settle found. Not kept with a nil reason is an uninstalled host:
/// the prompt, silently.
struct HotExitSettleResult: Equatable {
    let kept: Bool
    let reason: String?

    static let keptResult = HotExitSettleResult(kept: true, reason: nil)
}

enum HotExitDiscardWait: Equatable {
    case committed, notCommitted
}

/// Plan 43 PR 29 §3 (W§0, W§3, W§4): one vault's recovery writes. Every tab
/// owns its entry for one dirty episode, ended by the reader (retired) or
/// abandoned (kept, as a crash keeps it). The writer never decides inside a
/// mutation (O7): a change schedules one coalesced re-evaluation that reads
/// the finished state. It holds nothing the store host needs, and nothing
/// joins it.
@MainActor
final class HotExitWriter {
    /// A changed body is written this long after the last change (§3).
    static var debounce: TimeInterval = 1

    private let host: HotExitStoreHost
    let open: HotExitOpen
    private weak var source: HotExitBufferSource?
    private var states: [TabID: TabState] = [:]
    private var epoch: UInt64
    private var evaluationPosted = false
    private(set) var detached = false
    private var fenced = false
    private var fenceLift = 0
    private var liftingFence = false
    private var settleAttempt = 0
    private var discardTask: Task<HotExitResult<UInt64>, Never>?
    private var discardOutcome: HotExitResult<UInt64>?

    init(host: HotExitStoreHost, open: HotExitOpen, source: HotExitBufferSource) {
        self.host = host
        self.open = open
        self.source = source
        self.epoch = open.epoch
    }

    var key: HotExitVaultKey? { open.key }
    var isInert: Bool { open.inert }

    // MARK: - Install (§5)

    /// A restored tab starts its episode at install: it adopts the claim the
    /// snapshot observed and carries every inherited incarnation until the
    /// reader ends it.
    func installRestored(_ tab: TabID, observedClaim: String?, inherited: [String]) {
        guard open.activeKey != nil, !detached else { return }
        let state = stateFor(tab)
        state.expectedClaim = observedClaim
        for incarnation in inherited where !state.inherited.contains(incarnation) {
            state.inherited.append(incarnation)
        }
        observe()
    }

    /// A current claim with no payload whose id a layout tab holds: the tab
    /// carries it and claims expecting it on its first edit; closing or
    /// re-pointing it first retires it (W§2).
    func inheritOrphanClaim(_ tab: TabID, incarnation: String) {
        guard open.activeKey != nil, !detached else { return }
        let state = stateFor(tab)
        state.expectedClaim = incarnation
        state.inherited.append(incarnation)
        state.inheritedPath = source?.hotExitTabPath(tab)
    }

    /// An orphan claim no tab holds, or a redundant peer's entry.
    func retireUnheld(entryId: String, incarnations: [String]) {
        guard let key = open.activeKey, !detached, !incarnations.isEmpty else { return }
        host.retireDetached(key, entryId: entryId, incarnations: incarnations, claimedAtEpoch: epoch)
    }

    // MARK: - O7: read after the mutation

    /// Schedules one coalesced re-evaluation of every tab, run after the
    /// mutation's caller returns.
    func observe() {
        guard open.activeKey != nil, !detached, !evaluationPosted else { return }
        evaluationPosted = true
        DispatchQueue.main.async { [weak self] in
            MainActor.assumeIsolated { self?.drain() }
        }
    }

    /// Reads every tab now rather than on the queued drain, so a close that
    /// decides whether anything needs settling sees a buffer that turned
    /// clean (or dirty) in this same turn (Codex adversarial review, round
    /// 6).
    func reconcileNow() {
        guard open.activeKey != nil, !detached else { return }
        drain()
    }

    private func drain() {
        evaluationPosted = false
        guard !detached, let source else { return }
        var ids = source.hotExitMarkdownTabs
        for id in states.keys where !ids.contains(id) { ids.append(id) }
        for id in ids { evaluate(id) }
    }

    /// `force`: an explicit re-evaluation (an outcome, the fence lifting, a
    /// settle). A drain skips a tab whose buffer is unchanged since it was
    /// last seen, so unrelated churn never retries a failed call; the next
    /// change or settle does (W§3).
    private func evaluate(_ id: TabID, force: Bool = false) {
        guard open.activeKey != nil, !detached, let source else { return }
        let state = stateFor(id)
        guard let path = source.hotExitTabPath(id) else {
            vanished(state)
            return
        }
        // A carried orphan claim retires when its tab is re-pointed before
        // any edit (W§2).
        if let inheritedPath = state.inheritedPath, !state.episodeLive,
            !BaseExactIdentity.matches(inheritedPath, path)
        {
            endByReader(state)
            return
        }
        guard let buffer = source.hotExitBuffer(for: id) else {
            // A Markdown tab with no loaded buffer (loading): nothing to read.
            return
        }
        if !force, let last = state.lastSeen, last.stamp == buffer.stamp {
            return
        }
        state.lastSeen = buffer
        if !buffer.dirty {
            state.discardedUnkept = false
            if state.episodeLive || !state.held.isEmpty {
                endByReader(state)
            }
            return
        }
        if state.discardedUnkept {
            if let covered = state.coveredBuffer, covered.sameSnapshot(buffer) { return }
            state.discardedUnkept = false
            state.notKeptReason = nil
        }
        if fenced { return }
        if !state.episodeLive {
            state.episodeLive = true
            state.token += 1
            state.claimLost = false
            state.notKeptReason = nil
            state.lastIssued = nil
            state.lastKept = nil
            state.nextGeneration = 1
            state.inheritedPath = nil
        }
        if state.claimLost { return }
        guard state.liveClaim != nil else {
            issueClaim(state)
            return
        }
        if let issued = state.lastIssued, issued.sameSnapshot(buffer) { return }
        // A baseline change is written at once (§3); an edit after the
        // debounce.
        if let issued = state.lastIssued, issued.sameBaseline(buffer) {
            scheduleDebounce(state)
        } else {
            issueWrite(state)
        }
    }

    /// The tab left the model or stopped being Markdown without the reader
    /// ending its episode through a gate: a dirty buffer dropped with no
    /// gate is abandoned, so the store keeps it (§3) — unless another tab
    /// holds the same text on the same path and its own entry has durably
    /// kept it. A clean tab carrying incarnations retires them.
    /// The active tab is pointed at another note in place (an open into the
    /// current tab) after the dirty gate let it through, because a peer
    /// holds the text. The outgoing note is treated as if its tab closed: a
    /// clean buffer retires its entry, a dirty one retires only behind a
    /// durably kept peer, and otherwise its entry is abandoned and the tab
    /// takes a fresh id for its next note (Codex adversarial review, round
    /// 5). A rename does not come here: it keeps the tab's note.
    func tabReplacing(_ tab: TabID) {
        guard open.activeKey != nil, !detached, let source, let state = states[tab] else { return }
        if let buffer = source.hotExitBuffer(for: tab) {
            if !buffer.dirty {
                evaluate(tab, force: true)
                // An inherited orphan claim goes too, or the next note's
                // claim under this id is refused against it (Codex
                // adversarial review, round 6).
                if state.episodeLive || !state.held.isEmpty || !state.inherited.isEmpty {
                    endByReader(state)
                }
                states[tab] = nil
                return
            }
            state.lastSeen = buffer
        }
        vanished(state, tabStays: true)
    }

    private func vanished(_ state: TabState, tabStays: Bool = false) {
        defer { states[state.tab] = nil }
        state.cancelDebounce()
        guard state.episodeLive else {
            if !state.held.isEmpty || !state.inherited.isEmpty { endByReader(state) }
            return
        }
        // The peer must have kept this text itself, not only hold it: a
        // peer whose own write was refused leaves this entry the only
        // durable copy (Codex adversarial review, round 4).
        if let last = state.lastSeen, let source,
            source.hotExitMarkdownTabs.contains(where: { other in
                guard other != state.tab, let buffer = source.hotExitBuffer(for: other),
                    buffer.dirty, BaseExactIdentity.matches(buffer.path, last.path),
                    BaseExactIdentity.matches(buffer.body, last.body),
                    BaseExactIdentity.matches(buffer.fmSource, last.fmSource),
                    let peer = states[other], !peer.claimLost, !peer.discardedUnkept,
                    peer.liveClaim != nil, let kept = peer.lastKept, kept.sameSnapshot(buffer)
                else { return false }
                return true
            })
        {
            endByReader(state)
            return
        }
        state.token += 1
        // A tab still in the model (now a canvas or base) keeps the
        // abandoned entry under its old id; its next note claims a fresh one
        // (red team PR 29.2), and the next open restores the abandoned text
        // as a new tab.
        if tabStays
            || (source?.hotExitMarkdownTabs.contains(state.tab) == false
                && source?.hotExitTabExists(state.tab) == true)
        {
            source?.hotExitAssignFreshEntryId(state.tab)
        }
    }

    // MARK: - Claims

    private func issueClaim(_ state: TabState) {
        guard let key = open.activeKey, let source, !state.claimInFlight else { return }
        // An errored claim retries with its own incarnation (O2).
        if state.claimIncarnation == nil {
            guard let incarnation = try? newHotExitIncarnation() else {
                state.notKeptReason = hotExitReasonStoreUnwritable()
                return
            }
            state.claimIncarnation = incarnation
        }
        let incarnation = state.claimIncarnation!
        state.held.insert(incarnation)
        state.claimInFlight = true
        let token = state.token
        let epoch = self.epoch
        let entryId = source.hotExitEntryId(for: state.tab)
        let expected = state.expectedClaim
        // Queued now, so a retirement this turn issues runs after it (Codex
        // adversarial review, round 7).
        host.claimNow(
            key, epoch: epoch, entryId: entryId, expected: expected, incarnation: incarnation
        ) { [weak self] result in
            self?.onClaimResolved(state, token: token, incarnation: incarnation, result: result)
        }
    }

    private func onClaimResolved(
        _ state: TabState, token: Int, incarnation: String, result: HotExitResult<HotExitClaimOutcome>
    ) {
        guard !detached else { return }
        // O3: an outcome counts only for the current token.
        let current = token == state.token && state.episodeLive
        if token == state.token { state.claimInFlight = false }
        if result.moot { return }
        if let reason = result.errorReason {
            if current { state.notKeptReason = reason }
            return
        }
        switch result.value {
        case .claimed(let claim)?:
            guard current else { return }
            state.liveClaim = claim
            state.claimIncarnation = nil
            state.expectedClaim = nil
            state.notKeptReason = nil
            evaluate(state.tab, force: true)
        case .superseded(let by)?:
            // Only this call's own incarnation: a newer episode's pending
            // claim survives an older one's outcome.
            state.held.remove(incarnation)
            if state.claimIncarnation == incarnation { state.claimIncarnation = nil }
            if current { onSuperseded(state, by: by, isClaim: true) }
        case .refused(let refusal)?:
            state.held.remove(incarnation)
            if state.claimIncarnation == incarnation { state.claimIncarnation = nil }
            if current { state.notKeptReason = hotExitRefusalReason(refusal: refusal) }
        case nil:
            break
        }
    }

    private func onSuperseded(_ state: TabState, by: HotExitSupersession, isClaim: Bool) {
        if case .epoch(let current) = by {
            onEpochMoved(current)
            return
        }
        // Another holder owns the entry: this tab no longer carries what
        // that holder inherited, and never takes a claim back (§3, §5).
        state.claimLost = true
        state.liveClaim = nil
        state.notKeptReason = hotExitReasonTakenOver()
        if isClaim {
            state.inherited.removeAll()
            state.expectedClaim = nil
        }
    }

    /// O6: an epoch this writer's own fence does not explain. No episode
    /// re-claims on its own: what the epoch retired stays unkept and the
    /// next close asks; an edit after it starts a fresh episode.
    private func onEpochMoved(_ current: UInt64) {
        guard current > epoch else { return }
        epoch = current
        clearCarriedClaims()
        guard !fenced, let source else { return }
        for state in states.values {
            if let buffer = source.hotExitBuffer(for: state.tab), buffer.dirty, !state.discardedUnkept {
                markUnkept(state, buffer: buffer, reason: hotExitReasonTakenOver())
            }
        }
    }

    /// An epoch passed every claim of an older epoch: a tab not in an
    /// episode no longer expects or carries one (red team PR 29.2).
    private func clearCarriedClaims() {
        for state in states.values where !state.episodeLive {
            state.expectedClaim = nil
            state.inherited.removeAll()
            state.inheritedPath = nil
        }
    }

    private func markUnkept(_ state: TabState, buffer: HotExitBuffer, reason: String) {
        state.cancelDebounce()
        state.token += 1
        state.claimInFlight = false
        state.writeInFlight = false
        state.episodeLive = false
        state.liveClaim = nil
        state.claimIncarnation = nil
        state.expectedClaim = nil
        state.lastIssued = nil
        state.lastKept = nil
        state.held.removeAll()
        state.inherited.removeAll()
        state.discardedUnkept = true
        state.coveredBuffer = buffer
        state.notKeptReason = reason
    }

    // MARK: - Writes

    private func scheduleDebounce(_ state: TabState) {
        state.cancelDebounce()
        let delay = Self.debounce
        state.debounce = Task { [weak self] in
            try? await Task.sleep(nanoseconds: UInt64(delay * 1_000_000_000))
            guard !Task.isCancelled, let self, !self.detached else { return }
            state.debounce = nil
            self.issueWrite(state)
        }
    }

    private func issueWrite(_ state: TabState) {
        guard let key = open.activeKey, let claim = state.liveClaim, !state.claimLost, !fenced,
            let buffer = source?.hotExitBuffer(for: state.tab), buffer.dirty
        else { return }
        if state.writeInFlight {
            state.writeDue = true
            return
        }
        state.cancelDebounce()
        let generation = state.nextGeneration
        state.nextGeneration += 1
        state.writeInFlight = true
        state.writeDue = false
        state.lastIssued = buffer
        let token = state.token
        Task { [weak self, host] in
            // The whole note, composed off the main actor.
            let entry = await Task.detached(priority: .utility) {
                HotExitEntryWrite(
                    path: buffer.path,
                    text: composeNote(fmSource: buffer.fmSource, body: buffer.body),
                    baselineHash: buffer.baselineHash,
                    baselineFileId: buffer.baselineIdentity.fileIdentity)
            }.value
            let result = await host.write(key, claim: claim, generation: generation, entry: entry)
            self?.onWriteResolved(
                state, token: token, claim: claim, snapshot: buffer, generation: generation,
                result: result)
        }
    }

    private func onWriteResolved(
        _ state: TabState, token: Int, claim: HotExitClaim, snapshot: HotExitBuffer,
        generation: UInt64, result: HotExitResult<HotExitWriteOutcome>
    ) {
        guard !detached else { return }
        let current = token == state.token && state.liveClaim?.incarnation == claim.incarnation
        if token == state.token { state.writeInFlight = false }
        guard current, !result.moot else { return }
        if let reason = result.errorReason {
            state.notKeptReason = reason
        } else {
            switch result.value {
            case .durable?:
                state.lastKept = snapshot
                state.notKeptReason = nil
            case .superseded(.newer(let stored))? where stored >= generation:
                state.lastKept = snapshot
                state.notKeptReason = nil
            case .superseded(let by)?:
                onSuperseded(state, by: by, isClaim: false)
            case .refused(let refusal)?:
                state.notKeptReason = hotExitRefusalReason(refusal: refusal)
            case nil:
                break
            }
        }
        if state.writeDue {
            issueWrite(state)
        } else {
            evaluate(state.tab, force: true)
        }
    }

    // MARK: - Ending an episode (O4)

    /// The reader ended this tab's episode through a gate (Discard on its
    /// close, a dirty navigation's Discard, a recovery draft's discard),
    /// before it is closed or re-pointed. Whatever it holds or inherited is
    /// retired.
    func endEpisodeByReader(_ tab: TabID) {
        guard open.activeKey != nil, !detached else { return }
        endByReader(stateFor(tab))
    }

    /// A tab about to leave the model, read while it still can be. A clean
    /// buffer ends its episode, so a save that closes its tab in the same
    /// turn retires the entry rather than leaving it to come back unsaved
    /// (Codex adversarial review, round 2). A dirty one goes through the
    /// gate or the vanish rule, as before.
    func tabClosing(_ tab: TabID) {
        guard open.activeKey != nil, !detached, let source, states[tab] != nil,
            let buffer = source.hotExitBuffer(for: tab), !buffer.dirty
        else { return }
        evaluate(tab, force: true)
    }

    private func endByReader(_ state: TabState) {
        guard let key = open.activeKey, let source else { return }
        state.cancelDebounce()
        var incarnations = Array(state.held) + state.inherited
        if let live = state.liveClaim {
            incarnations.append(live.incarnation)
            incarnations.append(contentsOf: live.adopted)
        }
        if let pending = state.claimIncarnation { incarnations.append(pending) }
        state.token += 1
        state.claimInFlight = false
        state.writeInFlight = false
        state.episodeLive = false
        state.liveClaim = nil
        state.claimIncarnation = nil
        state.expectedClaim = nil
        state.claimLost = false
        state.lastIssued = nil
        state.lastKept = nil
        state.notKeptReason = nil
        state.writeDue = false
        state.held.removeAll()
        state.inherited.removeAll()
        state.inheritedPath = nil
        var seen = Set<String>()
        let distinct = incarnations.filter { seen.insert($0).inserted }
        if !distinct.isEmpty {
            host.retireDetached(
                key, entryId: source.hotExitEntryId(for: state.tab), incarnations: distinct,
                claimedAtEpoch: epoch)
        }
    }

    // MARK: - Settling (§4, O5)

    /// Waits, within `budget`, for every obligation of the vault's key and
    /// every dirty tab to be kept, re-reading the tabs after each frame.
    /// Always runs at least one frame.
    func settle(budget: TimeInterval = 4, mode: HotExitSettleMode) async -> HotExitSettleResult {
        if open.inert { return settleInert(mode) }
        guard let key = open.activeKey else { return HotExitSettleResult(kept: false, reason: nil) }
        let deadline = Date().addingTimeInterval(budget)
        await Self.frame()
        var epochChecked = false
        // A kept snapshot counts only under an epoch this settle read back.
        var epochConfirmed = false
        var epochFailure: String?
        settleAttempt += 1
        let attempt = settleAttempt
        // Once per settle, never per frame (red team PR 29.2).
        host.retryParkedRetirements(key)
        while true {
            if detached { return HotExitSettleResult(kept: false, reason: hotExitReasonTimedOut()) }
            drain()
            let issued = mode == .keep && issueLatest(attempt)
            if !issued, !host.hasUnresolved(key), !anyCallInFlight {
                if mode == .retireOnly { return .keptResult }
                if !epochChecked {
                    epochChecked = true
                    if !anyDirtyToKeep { continue }
                    let read = await Self.bounded(until: deadline) { [host] in await host.epoch(key) }
                    if let read, read.succeeded, let value = read.value {
                        epochConfirmed = true
                        onEpochMoved(value)
                    } else if let reason = read?.errorReason {
                        epochFailure = reason
                    }
                    continue
                }
                return decide(key, epochConfirmed: epochConfirmed, epochFailure: epochFailure)
            }
            if Date() >= deadline {
                return mode == .keep && !host.hasUnresolved(key)
                    ? decide(key, epochConfirmed: epochConfirmed, epochFailure: epochFailure)
                    : HotExitSettleResult(kept: false, reason: firstReason(key))
            }
            await Self.frame()
        }
    }

    private func decide(
        _ key: HotExitVaultKey, epochConfirmed: Bool, epochFailure: String?
    ) -> HotExitSettleResult {
        guard allDirtyKept else { return HotExitSettleResult(kept: false, reason: firstReason(key)) }
        if epochConfirmed || !anyDirtyToKeep { return .keptResult }
        return HotExitSettleResult(
            kept: false,
            reason: host.hasUnresolvedDiscard(key)
                ? hotExitReasonDiscardPending() : epochFailure ?? hotExitReasonTimedOut())
    }

    private func settleInert(_ mode: HotExitSettleMode) -> HotExitSettleResult {
        if let key = open.key, host.hasUnresolved(key) {
            return HotExitSettleResult(kept: false, reason: hotExitReasonDiscardPending())
        }
        return mode == .keep && anyDirtyToKeep
            ? HotExitSettleResult(kept: false, reason: open.reason)
            : .keptResult
    }

    /// Whether every obligation of the key is resolved (for a clean close
    /// that needs no settle at all).
    var hasUnresolvedObligations: Bool {
        guard let key = open.key else { return false }
        return host.hasUnresolved(key)
    }

    private var anyCallInFlight: Bool {
        liftingFence || states.values.contains { $0.claimInFlight || $0.writeInFlight }
    }

    private var dirtyTabs: [(TabID, HotExitBuffer)] {
        guard let source else { return [] }
        return source.hotExitMarkdownTabs.compactMap { id in
            source.hotExitBuffer(for: id).flatMap { $0.dirty ? (id, $0) : nil }
        }
    }

    private var anyDirtyToKeep: Bool { !dirtyTabs.isEmpty }

    private var allDirtyKept: Bool {
        dirtyTabs.allSatisfy { id, buffer in
            guard let state = states[id], !state.discardedUnkept, !state.claimLost,
                state.liveClaim != nil, let kept = state.lastKept
            else { return false }
            return kept.sameSnapshot(buffer)
        }
    }

    private func firstReason(_ key: HotExitVaultKey) -> String {
        if host.hasUnresolvedDiscard(key) { return hotExitReasonDiscardPending() }
        for (id, _) in dirtyTabs {
            if let reason = states[id]?.notKeptReason { return reason }
        }
        return hotExitReasonTimedOut()
    }

    /// Issues what the debounce was holding, and retries an errored claim or
    /// write once per settle, for every dirty tab not kept at its latest.
    /// True when it issued anything.
    private func issueLatest(_ attempt: Int) -> Bool {
        guard !fenced else { return false }
        var issued = false
        for (id, buffer) in dirtyTabs {
            let state = stateFor(id)
            state.cancelDebounce()
            if state.discardedUnkept || state.claimLost || state.claimInFlight || state.writeInFlight {
                continue
            }
            if let kept = state.lastKept, kept.sameSnapshot(buffer) { continue }
            // New text is always tried; a failure of this same snapshot once.
            if state.settleAttempt == attempt, let issuedBuffer = state.lastIssued,
                issuedBuffer.sameSnapshot(buffer)
            {
                continue
            }
            state.settleAttempt = attempt
            if state.episodeLive, state.liveClaim != nil {
                issueWrite(state)
            } else if state.settleClaimAttempt != attempt {
                state.settleClaimAttempt = attempt
                evaluate(id, force: true)
            }
            issued = issued || state.claimInFlight || state.writeInFlight
        }
        return issued
    }

    // MARK: - Discard All (§4, O6)

    /// Records and retires the covered buffers, fences the writer, issues the
    /// discard (or joins the one in flight), and waits at most `budget`.
    func discardAll(budget: TimeInterval = 4) async -> HotExitDiscardWait {
        guard let key = open.key else { return .committed }
        if !open.inert { coverDirtyTabs() }
        return await fenceAndDiscard(key, budget: budget)
    }

    /// Try again: the same discard while it runs, a new one once none is in
    /// flight, under the same fence.
    func retryDiscard(budget: TimeInterval = 4) async -> HotExitDiscardWait {
        guard let key = open.key else { return .committed }
        return await fenceAndDiscard(key, budget: budget)
    }

    /// Whether a discard of this writer's vault is unresolved.
    var hasUnresolvedDiscard: Bool {
        guard let key = open.key else { return false }
        return host.hasUnresolvedDiscard(key)
    }

    private func fenceAndDiscard(_ key: HotExitVaultKey, budget: TimeInterval) async -> HotExitDiscardWait {
        fenced = true
        fenceLift += 1
        liftingFence = false
        if !(host.hasUnresolvedDiscard(key) && discardTask != nil) {
            discardOutcome = nil
            discardTask = Task { [weak self, host] in
                let result = await host.discard(key)
                self?.discardOutcome = result
                self?.onDiscardResolved(result)
                return result
            }
        }
        let deadline = Date().addingTimeInterval(budget)
        while discardOutcome == nil, Date() < deadline {
            await Self.frame()
        }
        return discardOutcome?.succeeded == true ? .committed : .notCommitted
    }

    private func coverDirtyTabs() {
        for (id, buffer) in dirtyTabs {
            let state = stateFor(id)
            state.coveredBuffer = buffer
            state.covered = true
            endByReader(state)
        }
    }

    /// O6: when the last unresolved discard resolves, the fence lifts, a
    /// covered tab still at its covered buffer is discarded-unkept, and any
    /// other dirty tab starts a fresh episode at the current epoch.
    private func onDiscardResolved(_ result: HotExitResult<UInt64>) {
        if result.succeeded, let committed = result.value, committed > epoch { epoch = committed }
        guard let key = open.key, !detached, !host.hasUnresolvedDiscard(key) else { return }
        fenceLift += 1
        let lift = fenceLift
        liftingFence = true
        Task { [weak self, host] in
            let read = await host.epoch(key)
            guard let self, lift == self.fenceLift, !self.detached,
                !self.host.hasUnresolvedDiscard(key)
            else { return }
            if read.succeeded, let value = read.value, value > self.epoch { self.epoch = value }
            self.clearCarriedClaims()
            self.liftingFence = false
            self.fenced = false
            self.reevaluateAfterFence()
        }
    }

    private func reevaluateAfterFence() {
        guard let source else { return }
        for state in states.values where state.covered {
            state.covered = false
            if let buffer = source.hotExitBuffer(for: state.tab), buffer.dirty,
                let covered = state.coveredBuffer, covered.sameSnapshot(buffer)
            {
                state.discardedUnkept = true
                state.notKeptReason = hotExitReasonDiscarded()
            }
        }
        for id in source.hotExitMarkdownTabs { evaluate(id, force: true) }
    }

    // MARK: - Detach

    /// The vault is closing (after a decided route) or the open failed: late
    /// outcomes publish nothing, and every live episode is abandoned (O4).
    func detach() {
        detached = true
        for state in states.values { state.cancelDebounce() }
    }

    // MARK: - Test seams

    /// True once nothing is posted, debounced or in flight and no obligation
    /// of the key is unresolved.
    var isQuiescentForTests: Bool {
        guard let key = open.key else { return true }
        return !evaluationPosted && !anyCallInFlight && !host.hasUnresolved(key)
            && states.values.allSatisfy { $0.debounce == nil }
    }

    func notKeptReasonForTests(_ tab: TabID) -> String? { states[tab]?.notKeptReason }
    func isKeptForTests(_ tab: TabID) -> Bool {
        guard let state = states[tab], let kept = state.lastKept,
            let buffer = source?.hotExitBuffer(for: tab)
        else { return false }
        return kept.sameSnapshot(buffer)
    }

    // MARK: - Helpers

    private func stateFor(_ tab: TabID) -> TabState {
        if let state = states[tab] { return state }
        let state = TabState(tab: tab)
        states[tab] = state
        return state
    }

    /// One frame of the main run loop: posted outcomes run in it.
    static func frame() async {
        await Task.yield()
        try? await Task.sleep(nanoseconds: 2_000_000)
    }

    /// Awaits `operation`, giving up at `deadline` (nil). The operation keeps
    /// running; nothing joins it.
    static func bounded<T>(until deadline: Date, _ operation: @escaping @MainActor () async -> T) async -> T? {
        let box = Box<T>()
        Task { @MainActor in box.value = await operation() }
        while box.value == nil, Date() < deadline {
            await frame()
        }
        return box.value
    }

    private final class Box<T> {
        var value: T?
    }

    private final class TabState {
        let tab: TabID
        /// O3's token: bumped when an episode starts or ends.
        var token = 0
        var episodeLive = false
        var liveClaim: HotExitClaim?
        var claimInFlight = false
        /// The incarnation of the claim being issued, kept across an error
        /// so its retry is idempotent (O2).
        var claimIncarnation: String?
        /// What the next claim expects to replace (O2).
        var expectedClaim: String?
        var claimLost = false
        /// Every incarnation issued not answered Superseded or Refused (O4).
        var held: Set<String> = []
        var inherited: [String] = []
        /// The path an inherited orphan claim was carried on.
        var inheritedPath: String?
        var nextGeneration: UInt64 = 1
        var lastIssued: HotExitBuffer?
        var lastKept: HotExitBuffer?
        var lastSeen: HotExitBuffer?
        var writeInFlight = false
        var writeDue = false
        var notKeptReason: String?
        /// O6: a Discard All covered this tab and has not resolved.
        var covered = false
        var coveredBuffer: HotExitBuffer?
        var discardedUnkept = false
        var settleAttempt = 0
        var settleClaimAttempt = 0
        var debounce: Task<Void, Never>?

        init(tab: TabID) { self.tab = tab }

        func cancelDebounce() {
            debounce?.cancel()
            debounce = nil
        }
    }
}
