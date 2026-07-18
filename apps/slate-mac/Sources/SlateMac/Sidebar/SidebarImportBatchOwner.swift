// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

/// One positively materialized top-level result in original provider order.
/// The aggregate tree landing uses this single schema for verified external
/// creates, authoritative native moves, and post-scan unknown candidates.
struct SidebarImportMaterializedResult: Equatable, Sendable {
    let providerIndex: Int
    let path: String
    let isDirectory: Bool
}

/// The single AppState-owned lifetime for one Finder import batch.
///
/// Creation is synchronous on the main actor, before provider loading starts.
/// The frozen session, destination, intake, cancellation signal, and mutation
/// token therefore cannot drift while item-provider callbacks are outstanding.
@MainActor
final class SidebarImportBatchOwner {
    let mutationToken: Int
    let session: VaultSession
    let vaultURL: URL
    let destinationFolder: String
    let selectionRevision: UInt64
    let supportedProviderCount: Int
    let intake: SidebarImportProviderIntake
    let cancellationSignal: SidebarImportEngineSignal
    let progress: SidebarImportProgressModel
    private let cancellationAvailabilityChanged: @MainActor (Bool) -> Void

    private(set) var workerTask: Task<Void, Never>?
    private(set) var isCancellationRequested = false
    private var terminalProviderIndices = Set<Int>()
    private var activePhaseCancellation: (() -> Void)?
    private var activePhaseCanBeCancelledByUser = true

    var completedProviderCount: Int { progress.completedProviderCount }
    var canRequestUserCancellation: Bool {
        !isCancellationRequested
            && activePhaseCanBeCancelledByUser
            && terminalProviderIndices.count < supportedProviderCount
    }

    private func publishCancellationAvailability() {
        let available = canRequestUserCancellation
        progress.setCancellationAvailability(available)
        cancellationAvailabilityChanged(available)
    }

    init(
        mutationToken: Int,
        session: VaultSession,
        vaultURL: URL,
        destinationFolder: String,
        selectionRevision: UInt64,
        supportedProviderCount: Int,
        intake: SidebarImportProviderIntake,
        cancellationSignal: SidebarImportEngineSignal,
        cancellationAvailabilityChanged: @escaping @MainActor (Bool) -> Void
    ) {
        self.mutationToken = mutationToken
        self.session = session
        self.vaultURL = vaultURL
        self.destinationFolder = destinationFolder
        self.selectionRevision = selectionRevision
        self.supportedProviderCount = supportedProviderCount
        self.intake = intake
        self.cancellationSignal = cancellationSignal
        self.cancellationAvailabilityChanged = cancellationAvailabilityChanged
        progress = SidebarImportProgressModel(
            admittedProviderCount: supportedProviderCount)
    }

    func installWorkerTask(_ task: Task<Void, Never>) {
        precondition(workerTask == nil, "an import owner may start only one worker")
        workerTask = task
        if isCancellationRequested {
            task.cancel()
        }
    }

    func recordCompletedProviderCount(_ count: Int) {
        progress.recordCompletedProviderCount(count)
    }

    /// A supported provider becomes terminal exactly once, after its load and
    /// copy/move outcome can no longer change. Duplicate callback delivery is
    /// harmless and cannot inflate the frozen denominator.
    func markProviderTerminal(_ providerIndex: Int) {
        guard terminalProviderIndices.insert(providerIndex).inserted else { return }
        progress.recordCompletedProviderCount(terminalProviderIndices.count)
        publishCancellationAvailability()
    }

    func markProvidersTerminal<S: Sequence>(_ providerIndices: S)
    where S.Element == Int {
        for providerIndex in providerIndices {
            terminalProviderIndices.insert(providerIndex)
        }
        progress.recordCompletedProviderCount(terminalProviderIndices.count)
        publishCancellationAvailability()
    }

    func clearWorkerTask() {
        workerTask = nil
    }

    func installActivePhaseCancellation(
        phase: SidebarImportProgressPhase = .importing,
        cancellableByUser: Bool = true,
        _ cancellation: @escaping () -> Void
    ) {
        activePhaseCancellation = cancellation
        activePhaseCanBeCancelledByUser = cancellableByUser
        progress.setPhase(phase)
        publishCancellationAvailability()
        if isCancellationRequested, cancellableByUser {
            cancellation()
        }
    }

    func clearActivePhaseCancellation() {
        activePhaseCancellation = nil
        activePhaseCanBeCancelledByUser = false
        publishCancellationAvailability()
    }

    /// Synchronous and nonblocking: provider loads are cancelled immediately,
    /// Task cancellation is signalled, and Task 3's engine is told to stop at
    /// its next ownership boundary without waiting for an entered FFI create.
    @discardableResult
    func requestCancellation() -> Bool {
        guard canRequestUserCancellation else { return false }
        isCancellationRequested = true
        progress.setPhase(.cancelling)
        intake.cancel()
        cancellationSignal.requestCancellation()
        if activePhaseCanBeCancelledByUser {
            activePhaseCancellation?()
        }
        publishCancellationAvailability()
        return true
    }

    /// Vault replacement/close invalidates the whole capability, including an
    /// otherwise non-user-cancellable mandatory reconciliation phase.
    func requestOwnershipLossCancellation() {
        if !isCancellationRequested {
            isCancellationRequested = true
            intake.cancel()
            cancellationSignal.requestCancellation()
        }
        publishCancellationAvailability()
        activePhaseCancellation?()
    }
}
