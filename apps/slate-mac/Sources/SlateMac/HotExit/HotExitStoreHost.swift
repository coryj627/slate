// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation
import os

/// Plan 43 PR 29 §1 (spec `docs/plans/43_mac_backlog_delivery/specs/pr29_hot_exit_mac_half.md`,
/// after plan 44 WB-29 §0–§1): the kind of a call the store host makes.
enum HotExitCallKind: Equatable, Sendable {
    case epoch, read, claim, write, retire, discard
}

/// What the test hooks see of a store call.
struct HotExitStoreCall: Sendable {
    let kind: HotExitCallKind
    let keyText: String
    let entryId: String?
    let incarnations: [String]
    let generation: UInt64
}

/// A failure a test hook injects. From `beforeCallForTests` the call is
/// never issued; from `afterCallForTests` it was issued and its result is
/// replaced (the "errored after it reached the disk" shape).
enum HotExitFault: Sendable {
    case io
    case damaged
    case refused(HotExitRefusal)
    /// Core's own refused claim outcome (a cap), not an error.
    case claimRefused(HotExitRefusal)
}

/// How a store call resolved. A store error is an outcome carrying core's
/// reason; nothing throws past the host.
struct HotExitResult<Value>: @unchecked Sendable {
    let value: Value?
    let errorReason: String?
    let damaged: Bool
    /// The call can no longer change the store's outcome (W§0 O5).
    let moot: Bool

    var succeeded: Bool { errorReason == nil && !moot }

    static func ok(_ value: Value) -> Self {
        Self(value: value, errorReason: nil, damaged: false, moot: false)
    }

    static func failed(_ reason: String, damaged: Bool = false) -> Self {
        Self(value: nil, errorReason: reason, damaged: damaged, moot: false)
    }

    static var overtaken: Self { Self(value: nil, errorReason: nil, damaged: false, moot: true) }
}

/// The one `HotExitStore` of the process, the ledger of every store call the
/// host has made per vault key, each vault's hold, and every call in flight.
/// It lives as long as the process, so its obligations outlive tabs, vaults
/// and `AppState`. Nothing joins it: no teardown awaits a store call beyond a
/// settle's budget, and a call cut short by process exit is a crash. Calls
/// take the vault's key, never the session (WB-29a §8). Calls run on their
/// own work items of a dedicated concurrent queue, never the cooperative
/// pool, so a stalled store pins no thread a save or `Task` needs.
final class HotExitStoreHost: @unchecked Sendable {
    /// A host with no store and no reason: the prompt, silently (W§6).
    static let uninstalled = HotExitStoreHost(store: nil, unavailableReason: nil)

    private static let log = Logger(subsystem: "com.startingblind.slate", category: "hot-exit")

    private let store: HotExitStore?
    /// Core's reason this host has no store; nil for a host never installed.
    let unavailableReason: String?
    private let lock = NSLock()
    private var ledgers: [String: VaultLedger] = [:]
    private var dead = false
    private let workers = DispatchQueue(
        label: "com.startingblind.slate.hot-exit", qos: .utility, attributes: .concurrent)

    /// Runs on the call's own thread before the store. May block (a held
    /// call). A fault means the call is never issued.
    var beforeCallForTests: ((HotExitStoreCall) -> HotExitFault?)? {
        get { lock.withLock { _before } }
        set { lock.withLock { _before = newValue } }
    }
    /// Runs on the call's own thread after the store. May block. A fault
    /// replaces the store's result.
    var afterCallForTests: ((HotExitStoreCall) -> HotExitFault?)? {
        get { lock.withLock { _after } }
        set { lock.withLock { _after = newValue } }
    }
    private var _before: ((HotExitStoreCall) -> HotExitFault?)?
    private var _after: ((HotExitStoreCall) -> HotExitFault?)?

    /// A failed retirement's first retry delay, doubling to the cap (W§1).
    var retirementRetryDelay: TimeInterval = 1
    var retirementRetryCap: TimeInterval = 30

    init(store: HotExitStore?, unavailableReason: String?) {
        self.store = store
        self.unavailableReason = unavailableReason
    }

    var hasStore: Bool { store != nil }

    /// The production device root: `~/Library/Application Support/Slate`.
    static func applicationSupportRoot() -> URL? {
        guard
            let base = try? FileManager.default.url(
                for: .applicationSupportDirectory, in: .userDomainMask,
                appropriateFor: nil, create: true)
        else { return nil }
        return base.appendingPathComponent("Slate", isDirectory: true)
    }

    /// Makes the device root exist and its name durable in its parent, on
    /// every install whoever created it, then opens the store. Any failure is
    /// a host with no store and core's reason, logged once (§1).
    static func install(
        root: URL,
        syncParent: (URL) -> Bool = HotExitStoreHost.fullSyncParent
    ) -> HotExitStoreHost {
        do {
            try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
        } catch {
            log.error("hot-exit store unavailable: \(error.localizedDescription, privacy: .public)")
            return HotExitStoreHost(store: nil, unavailableReason: hotExitReasonStoreUnwritable())
        }
        guard syncParent(root) else {
            log.error("hot-exit store unavailable: the device root's parent could not be synced")
            return HotExitStoreHost(store: nil, unavailableReason: hotExitReasonStoreUnwritable())
        }
        do {
            return HotExitStoreHost(store: try HotExitStore.open(deviceRoot: root.path), unavailableReason: nil)
        } catch let error as HotExitError {
            log.error("hot-exit store unavailable: \(String(describing: error), privacy: .public)")
            return HotExitStoreHost(store: nil, unavailableReason: reason(of: error))
        } catch {
            log.error("hot-exit store unavailable: \(error.localizedDescription, privacy: .public)")
            return HotExitStoreHost(store: nil, unavailableReason: hotExitReasonStoreUnwritable())
        }
    }

    /// `F_FULLFSYNC` on the directory that holds `root`, so the root's own
    /// name is durable (WB-29a §2's macOS sequence).
    static func fullSyncParent(_ root: URL) -> Bool {
        let parent = root.deletingLastPathComponent().path
        let descriptor = open(parent, O_RDONLY)
        guard descriptor >= 0 else { return false }
        defer { close(descriptor) }
        return fcntl(descriptor, F_FULLFSYNC) == 0
    }

    /// Core's reason fragment for a store error.
    static func reason(of error: HotExitError) -> String {
        switch error {
        case .Io(_, let reason), .InvalidEntryId(let reason), .InvalidIncarnation(let reason),
            .Damaged(let reason), .HeldElsewhere(let reason):
            return reason
        }
    }

    // MARK: - Holds (§1)

    /// Acquires the vault's hold for an opening vault, or reuses the one this
    /// process still owns. Another process's hold, or any other failure, says
    /// why there is no hot exit.
    func acquireHold(_ key: HotExitVaultKey) -> (held: Bool, reason: String?) {
        guard let store else { return (false, unavailableReason) }
        return lock.withLock {
            let ledger = ledgerFor(key)
            if ledger.hold == nil {
                do {
                    ledger.hold = try store.holdVault(key: key)
                } catch let error as HotExitError {
                    if case .HeldElsewhere(let reason) = error { return (false, reason) }
                    Self.log.error("hot-exit hold failed: \(String(describing: error), privacy: .public)")
                    return (false, hotExitReasonStoreUnwritable())
                } catch {
                    return (false, hotExitReasonStoreUnwritable())
                }
            }
            ledger.openSessions += 1
            return (true, nil)
        }
    }

    /// The vault that acquired the hold is gone: a closed vault, a failed
    /// open. The hold is freed once no obligation of the key is unresolved.
    func releaseSession(_ key: HotExitVaultKey) {
        lock.withLock {
            let ledger = ledgerFor(key)
            ledger.openSessions = max(0, ledger.openSessions - 1)
            releaseHoldIfIdle(ledger)
        }
    }

    func holdsVaultForTests(_ key: HotExitVaultKey) -> Bool {
        lock.withLock { ledgerFor(key).hold != nil }
    }

    /// The crash shape's "new process": drops every hold as the OS does at
    /// exit, and resolves every call still held in `beforeCallForTests`
    /// unissued. A call already issued has done whatever it did.
    func simulateProcessDeathForTests() {
        lock.withLock {
            dead = true
            for ledger in ledgers.values {
                ledger.hold?.release()
                ledger.hold = nil
                ledger.openSessions = 0
            }
        }
    }

    private func releaseHoldIfIdle(_ ledger: VaultLedger) {
        if ledger.openSessions == 0, let hold = ledger.hold, !ledger.hasUnresolved {
            hold.release()
            ledger.hold = nil
        }
    }

    // MARK: - The ledger (W§0)

    /// Whether any obligation of the key that can change the store is still
    /// unresolved (a moot one counts as resolved, O5; a read never counts).
    func hasUnresolved(_ key: HotExitVaultKey) -> Bool {
        lock.withLock { ledgerFor(key).hasUnresolved }
    }

    /// Whether any call of the key, a read included, is recorded (O1).
    func hasRecordedCallForTests(_ key: HotExitVaultKey) -> Bool {
        lock.withLock { !ledgerFor(key).unresolved.isEmpty }
    }

    /// Whether any vault discard of the key is unresolved (O6).
    func hasUnresolvedDiscard(_ key: HotExitVaultKey) -> Bool {
        lock.withLock { ledgerFor(key).unresolvedDiscards > 0 }
    }

    /// Retries every parked retirement of the key now: a settle, and a reopen
    /// before it reads (§1, §2).
    func retryParkedRetirements(_ key: HotExitVaultKey) {
        let ready: [QueuedCall] = lock.withLock {
            var ready: [QueuedCall] = []
            for queue in ledgerFor(key).queues.values {
                if let head = queue.calls.first, head.parked, !head.running {
                    head.parked = false
                    head.running = true
                    ready.append(head)
                }
            }
            return ready
        }
        ready.forEach(start)
    }

    // MARK: - Calls

    /// §2: the open's read, synchronous on the main actor (Deviation 1). It
    /// is recorded (O1) and changes nothing.
    func readNow(_ key: HotExitVaultKey) -> HotExitResult<HotExitSnapshot> {
        guard store != nil else {
            return .failed(unavailableReason ?? hotExitReasonStoreUnwritable())
        }
        let obligation = Obligation(kind: .read, entryId: nil, incarnations: [], claimedAtEpoch: nil)
        lock.withLock { ledgerFor(key).add(obligation) }
        let result = invoke(
            HotExitStoreCall(kind: .read, keyText: key.text, entryId: nil, incarnations: [], generation: 0)
        ) { try $0.readBuffers(key: key) }
        lock.withLock { resolve(ledgerFor(key), obligation) }
        return result
    }

    func epoch(_ key: HotExitVaultKey) async -> HotExitResult<UInt64> {
        await unqueued(
            key,
            HotExitStoreCall(kind: .epoch, keyText: key.text, entryId: nil, incarnations: [], generation: 0)
        ) { try $0.currentEpoch(key: key) }
    }

    /// A write is ordered by the claim it carries and has no queue (O2).
    func write(
        _ key: HotExitVaultKey, claim: HotExitClaim, generation: UInt64, entry: HotExitEntryWrite
    ) async -> HotExitResult<HotExitWriteOutcome> {
        await unqueued(
            key,
            HotExitStoreCall(
                kind: .write, keyText: key.text, entryId: claim.entryId,
                incarnations: [claim.incarnation], generation: generation),
            incarnations: [claim.incarnation],
            claimedAtEpoch: claim.epoch
        ) { try $0.writeBuffer(key: key, claim: claim, generation: generation, entry: entry) }
    }

    /// A discard joins no entry's queue: it fences at the epoch. Only a
    /// returned epoch commits it (O5), and that makes older calls moot.
    func discard(_ key: HotExitVaultKey) async -> HotExitResult<UInt64> {
        await unqueued(
            key,
            HotExitStoreCall(kind: .discard, keyText: key.text, entryId: nil, incarnations: [], generation: 0),
            isDiscard: true
        ) { [weak self] store in
            let epoch = try store.discardVault(key: key)
            self?.onDiscardCommitted(key, newEpoch: epoch)
            return epoch
        }
    }

    /// A claim runs in order for its entry id (O2). It carries the
    /// incarnation made for it, so a retry after an error is idempotent and a
    /// retirement can name it either way. It is admitted to its entry's
    /// queue before this returns, so a retirement issued after it always runs
    /// after it; the outcome comes back on the main actor (Codex adversarial
    /// review, round 7).
    func claimNow(
        _ key: HotExitVaultKey, epoch: UInt64, entryId: String, expected: String?, incarnation: String,
        onResult: @escaping @MainActor (HotExitResult<HotExitClaimOutcome>) -> Void
    ) {
        let deliver: @Sendable (HotExitResult<HotExitClaimOutcome>) -> Void = { result in
            DispatchQueue.main.async { MainActor.assumeIsolated { onResult(result) } }
        }
        enqueueQueued(
            key, entryId: entryId,
            HotExitStoreCall(kind: .claim, keyText: key.text, entryId: entryId, incarnations: [incarnation], generation: 0),
            incarnations: [incarnation], claimedAtEpoch: epoch, parksOnError: false,
            body: { (store: HotExitStore) -> HotExitClaimOutcome in
                try store.claimEntry(
                    key: key, epoch: epoch, entryId: entryId, expected: expected, incarnation: incarnation)
            },
            completion: Completion<HotExitClaimOutcome>(deliver: deliver))
    }

    /// A retirement runs in order for its entry id. It resolves only once the
    /// store confirms it: an error parks it at the head of its queue, retried
    /// on a backoff, when the next call for its entry is queued, and by every
    /// settle. Damage resolves it with its reason, so a damaged vault never
    /// pins the hold.
    @discardableResult
    func retire(
        _ key: HotExitVaultKey, entryId: String, incarnations: [String], claimedAtEpoch: UInt64
    ) async -> HotExitResult<Bool> {
        await queued(
            key, entryId: entryId,
            HotExitStoreCall(kind: .retire, keyText: key.text, entryId: entryId, incarnations: incarnations, generation: 0),
            incarnations: incarnations, claimedAtEpoch: claimedAtEpoch, parksOnError: true
        ) { [weak self] store in
            try store.retireEntry(key: key, entryId: entryId, incarnations: incarnations)
            self?.onRetirementConfirmed(key, entryId: entryId, incarnations: incarnations)
            return true
        }
    }

    /// Fire-and-forget retirement: the obligation is recorded before this
    /// returns (O1), so a settle that runs next waits for it.
    func retireDetached(_ key: HotExitVaultKey, entryId: String, incarnations: [String], claimedAtEpoch: UInt64) {
        enqueueQueued(
            key, entryId: entryId,
            HotExitStoreCall(kind: .retire, keyText: key.text, entryId: entryId, incarnations: incarnations, generation: 0),
            incarnations: incarnations, claimedAtEpoch: claimedAtEpoch, parksOnError: true,
            body: { [weak self] (store: HotExitStore) -> Bool in
                try store.retireEntry(key: key, entryId: entryId, incarnations: incarnations)
                self?.onRetirementConfirmed(key, entryId: entryId, incarnations: incarnations)
                return true
            },
            completion: Completion<Bool>(nil))
    }

    // MARK: - Machinery

    private func unqueued<T>(
        _ key: HotExitVaultKey,
        _ call: HotExitStoreCall,
        incarnations: [String] = [],
        claimedAtEpoch: UInt64? = nil,
        isDiscard: Bool = false,
        body: @escaping (HotExitStore) throws -> T
    ) async -> HotExitResult<T> {
        guard store != nil else {
            return .failed(unavailableReason ?? hotExitReasonStoreUnwritable())
        }
        return await withCheckedContinuation { continuation in
            let completion = Completion<T>(continuation)
            let obligation = Obligation(
                kind: call.kind, entryId: call.entryId, incarnations: incarnations,
                claimedAtEpoch: claimedAtEpoch)
            obligation.onMoot = { completion.trySet(.overtaken) }
            lock.withLock {
                let ledger = ledgerFor(key)
                ledger.add(obligation)
                if isDiscard { ledger.unresolvedDiscards += 1 }
            }
            workers.async { [self] in
                let result = invoke(call, body)
                lock.withLock {
                    let ledger = ledgerFor(key)
                    if isDiscard { ledger.unresolvedDiscards -= 1 }
                    resolve(ledger, obligation)
                }
                completion.trySet(result)
            }
        }
    }

    private func queued<T>(
        _ key: HotExitVaultKey,
        entryId: String,
        _ call: HotExitStoreCall,
        incarnations: [String],
        claimedAtEpoch: UInt64,
        parksOnError: Bool,
        body: @escaping (HotExitStore) throws -> T
    ) async -> HotExitResult<T> {
        guard store != nil else {
            return .failed(unavailableReason ?? hotExitReasonStoreUnwritable())
        }
        return await withCheckedContinuation { continuation in
            enqueueQueued(
                key, entryId: entryId, call, incarnations: incarnations,
                claimedAtEpoch: claimedAtEpoch, parksOnError: parksOnError, body: body,
                completion: Completion<T>(continuation))
        }
    }

    private func enqueueQueued<T>(
        _ key: HotExitVaultKey,
        entryId: String,
        _ call: HotExitStoreCall,
        incarnations: [String],
        claimedAtEpoch: UInt64,
        parksOnError: Bool,
        body: @escaping (HotExitStore) throws -> T,
        completion: Completion<T>
    ) {
        guard store != nil else {
            completion.trySet(.failed(unavailableReason ?? hotExitReasonStoreUnwritable()))
            return
        }
        let obligation = Obligation(
            kind: call.kind, entryId: entryId, incarnations: incarnations,
            claimedAtEpoch: claimedAtEpoch)
        let start: QueuedCall? = lock.withLock {
            let ledger = ledgerFor(key)
            ledger.add(obligation)
            let queue = ledger.queueFor(entryId)
            let queued = QueuedCall(ledger: ledger, queue: queue, obligation: obligation)
            queued.run = { [weak self, weak queued] in
                guard let self, let queued else { return }
                let result = self.invoke(call, body)
                let parked = parksOnError && result.errorReason != nil && !result.damaged
                    && !self.lock.withLock({ self.dead })
                self.onQueuedCallFinished(queued, parked: parked)
                if !parked { completion.trySet(result) }
            }
            obligation.onMoot = { [weak self, weak queued] in
                if let self, let queued {
                    self.lock.withLock { queued.queue.remove(queued) }
                }
                completion.trySet(.overtaken)
            }
            queue.calls.append(queued)
            // First in its queue it starts now; behind a parked retirement it
            // gets that retirement retried first (§1).
            if let head = queue.calls.first, !head.running {
                head.parked = false
                head.running = true
                return head
            }
            return nil
        }
        if let start { self.start(start) }
    }

    private func start(_ call: QueuedCall) {
        workers.async { call.run() }
    }

    private func onQueuedCallFinished(_ call: QueuedCall, parked: Bool) {
        var next: QueuedCall?
        var retryAfter: TimeInterval?
        lock.withLock {
            call.running = false
            if parked {
                call.parked = true
                // At most one retry timer per parked call (red team PR 29.2:
                // a failing store must not grow retry chains).
                if !call.retryScheduled {
                    let delay = call.nextRetryDelay ?? retirementRetryDelay
                    retryAfter = delay
                    call.nextRetryDelay = min(delay * 2, retirementRetryCap)
                    call.retryScheduled = true
                }
            } else {
                call.queue.remove(call)
                resolve(call.ledger, call.obligation)
                if let head = call.queue.calls.first, !head.running {
                    head.parked = false
                    head.running = true
                    next = head
                }
            }
        }
        if let next { start(next) }
        if let retryAfter {
            workers.asyncAfter(deadline: .now() + retryAfter) { [weak self, weak call] in
                guard let self, let call else { return }
                self.retryParked(call)
            }
        }
    }

    private func retryParked(_ call: QueuedCall) {
        let go: Bool = lock.withLock {
            call.retryScheduled = false
            guard call.parked, !call.running, !dead, call.queue.calls.first === call else { return false }
            call.parked = false
            call.running = true
            return true
        }
        if go { start(call) }
    }

    private func invoke<T>(_ call: HotExitStoreCall, _ body: (HotExitStore) throws -> T) -> HotExitResult<T> {
        if let fault = beforeCallForTests?(call) {
            return Self.result(from: fault)
        }
        if lock.withLock({ dead }) {
            return .failed(hotExitReasonStoreUnwritable())
        }
        guard let store else { return .failed(unavailableReason ?? hotExitReasonStoreUnwritable()) }
        do {
            let value = try body(store)
            if let fault = afterCallForTests?(call) {
                return Self.result(from: fault)
            }
            return .ok(value)
        } catch let error as HotExitError {
            if case .Damaged(let reason) = error { return .failed(reason, damaged: true) }
            return .failed(Self.reason(of: error))
        } catch {
            Self.log.error("hot-exit call failed: \(error.localizedDescription, privacy: .public)")
            return .failed(hotExitReasonStoreUnwritable())
        }
    }

    private static func result<T>(from fault: HotExitFault) -> HotExitResult<T> {
        switch fault {
        case .damaged:
            return .failed(hotExitReasonStoreUnwritable(), damaged: true)
        case .refused(let refusal):
            return .failed(hotExitRefusalReason(refusal: refusal))
        case .claimRefused(let refusal):
            if let outcome = HotExitClaimOutcome.refused(refusal: refusal) as? T {
                return .ok(outcome)
            }
            return .failed(hotExitRefusalReason(refusal: refusal))
        case .io:
            return .failed(hotExitReasonStoreUnwritable())
        }
    }

    /// Called with the lock held.
    private func resolve(_ ledger: VaultLedger, _ obligation: Obligation) {
        ledger.remove(obligation)
        if !ledger.hasUnresolved { releaseHoldIfIdle(ledger) }
    }

    /// O5: a confirmed retirement makes moot every write or claim of the
    /// incarnations it named.
    private func onRetirementConfirmed(_ key: HotExitVaultKey, entryId: String, incarnations: [String]) {
        let named = Set(incarnations)
        makeMoot(key) { obligation in
            (obligation.kind == .write || obligation.kind == .claim)
                && obligation.entryId == entryId
                && !named.isDisjoint(with: obligation.incarnations)
        }
    }

    /// O5: a committed discard makes moot every claim, write or retirement of
    /// an incarnation claimed at an epoch it passed.
    private func onDiscardCommitted(_ key: HotExitVaultKey, newEpoch: UInt64) {
        makeMoot(key) { obligation in
            [.write, .claim, .retire].contains(obligation.kind)
                && (obligation.claimedAtEpoch ?? .max) < newEpoch
        }
    }

    private func makeMoot(_ key: HotExitVaultKey, where covered: (Obligation) -> Bool) {
        let notify: [() -> Void] = lock.withLock {
            let ledger = ledgerFor(key)
            var notify: [() -> Void] = []
            for obligation in ledger.unresolved where covered(obligation) {
                ledger.remove(obligation)
                if let onMoot = obligation.onMoot { notify.append(onMoot) }
            }
            if !ledger.hasUnresolved { releaseHoldIfIdle(ledger) }
            return notify
        }
        notify.forEach { $0() }
        // A moot head leaves its queue (O5): whatever waited behind it runs.
        let ready: [QueuedCall] = lock.withLock {
            var ready: [QueuedCall] = []
            for queue in ledgerFor(key).queues.values {
                if let head = queue.calls.first, !head.running {
                    head.parked = false
                    head.running = true
                    ready.append(head)
                }
            }
            return ready
        }
        ready.forEach(start)
    }

    /// Called with the lock held.
    private func ledgerFor(_ key: HotExitVaultKey) -> VaultLedger {
        if let ledger = ledgers[key.text] { return ledger }
        let ledger = VaultLedger()
        ledgers[key.text] = ledger
        return ledger
    }

    // MARK: - Types

    /// Resumes its continuation exactly once.
    private final class Completion<T>: @unchecked Sendable {
        private let lock = NSLock()
        private var continuation: CheckedContinuation<HotExitResult<T>, Never>?
        private var deliver: (@Sendable (HotExitResult<T>) -> Void)?

        init(_ continuation: CheckedContinuation<HotExitResult<T>, Never>?) {
            self.continuation = continuation
        }

        init(deliver: @escaping @Sendable (HotExitResult<T>) -> Void) {
            self.deliver = deliver
        }

        func trySet(_ result: HotExitResult<T>) {
            let (pending, callback) = lock.withLock {
                defer {
                    continuation = nil
                    deliver = nil
                }
                return (continuation, deliver)
            }
            pending?.resume(returning: result)
            callback?(result)
        }
    }

    private final class Obligation: Hashable, @unchecked Sendable {
        let kind: HotExitCallKind
        let entryId: String?
        let incarnations: [String]
        let claimedAtEpoch: UInt64?
        var onMoot: (() -> Void)?

        init(kind: HotExitCallKind, entryId: String?, incarnations: [String], claimedAtEpoch: UInt64?) {
            self.kind = kind
            self.entryId = entryId
            self.incarnations = incarnations
            self.claimedAtEpoch = claimedAtEpoch
        }

        static func == (lhs: Obligation, rhs: Obligation) -> Bool { lhs === rhs }
        func hash(into hasher: inout Hasher) { hasher.combine(ObjectIdentifier(self)) }
    }

    private final class EntryQueue: @unchecked Sendable {
        var calls: [QueuedCall] = []
        func remove(_ call: QueuedCall) { calls.removeAll { $0 === call } }
    }

    private final class QueuedCall: @unchecked Sendable {
        let ledger: VaultLedger
        let queue: EntryQueue
        let obligation: Obligation
        var run: () -> Void = {}
        var running = false
        var parked = false
        var retryScheduled = false
        var nextRetryDelay: TimeInterval?

        init(ledger: VaultLedger, queue: EntryQueue, obligation: Obligation) {
            self.ledger = ledger
            self.queue = queue
            self.obligation = obligation
        }
    }

    private final class VaultLedger: @unchecked Sendable {
        var unresolved: Set<Obligation> = []
        var queues: [String: EntryQueue] = [:]
        var hold: HotExitVaultHold?
        var openSessions = 0
        var unresolvedDiscards = 0

        /// A call that can change the store is still out. A read is recorded
        /// (O1) but changes nothing, so nothing waits on it.
        var hasUnresolved: Bool {
            unresolved.contains { $0.kind != .epoch && $0.kind != .read }
        }

        func add(_ obligation: Obligation) { unresolved.insert(obligation) }
        func remove(_ obligation: Obligation) { unresolved.remove(obligation) }

        func queueFor(_ entryId: String) -> EntryQueue {
            if let queue = queues[entryId] { return queue }
            let queue = EntryQueue()
            queues[entryId] = queue
            return queue
        }
    }
}
