// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import Foundation

private enum BaseQueryBuilderPreviewThreadProbe {
    nonisolated static func isMainThread() -> Bool {
        Thread.isMainThread
    }
}

private enum BaseWriterThreadProbe {
    nonisolated static func isMainThread() -> Bool {
        Thread.isMainThread
    }
}

private enum BaseVaultWriteOutcome: Sendable {
    case success(existedBefore: Bool)
    case failure(String)
}

private enum BaseBuilderDestination: Sendable {
    case accepted(String)
    case rejected(String)
}

private enum BaseBuilderWriteOutcome: Sendable {
    case success
    case destinationExists(String)
    case failure(String)
}

private enum BaseQueriesRefreshOutcome: @unchecked Sendable {
    case success(
        saved: [SavedQuerySummary],
        bases: [BaseFileSummary],
        dashboards: [DashboardSummary]
    )
    case failure(String)
}

private enum SavedQueryUpdateOutcome: Sendable {
    case success
    case failure(String)
}

private enum BaseExportOutcome: Sendable {
    case success(String)
    case failure(String)
}

private enum VisibleBaseRefreshOwner: @unchecked Sendable {
    case registry(String)
    case dock(BasesDockTarget)
}

private struct VisibleBaseRefreshPlan: @unchecked Sendable {
    let workIndex: Int
    let owner: VisibleBaseRefreshOwner
    let document: BaseDocument
    let reservation: BaseContentRefreshReservation
    let previousMembership: BaseRowMembership
}

private enum VisibleDashboardRefreshOwner: @unchecked Sendable {
    case registry(String)
    case dock(BasesDockTarget)
}

private struct VisibleDashboardSectionRefreshPlan: @unchecked Sendable {
    let workIndex: Int
    let owner: VisibleDashboardRefreshOwner
    let dashboard: DashboardDocument
    let section: DashboardSectionDocument
    let reservation: DashboardSectionRefreshReservation
    let previousMembership: BaseRowMembership
}

private struct VisibleEmbedRefreshPlan: @unchecked Sendable {
    let workIndex: Int
    let key: BaseEmbedCacheKey
    let handle: BaseEmbedHandle
    let reservation: BaseEmbedHandleRefreshReservation
    let previousMemberships: [ObjectIdentifier: BaseRowMembership]
}

private enum VisibleBasesNativeWork: @unchecked Sendable {
    case base(Int, BaseContentRefreshReservation)
    case dashboard(Int, DashboardSectionRefreshReservation)
    case embed(Int, BaseEmbedHandleRefreshReservation)
}

private enum VisibleBasesNativeResult: @unchecked Sendable {
    case base(Int, BasePreparedLoad)
    case dashboard(Int, BasePreparedLoad)
    case embed(Int, BaseEmbedPreparedRefresh)

    var index: Int {
        switch self {
        case .base(let index, _), .dashboard(let index, _), .embed(let index, _):
            return index
        }
    }
}

struct BaseQueriesState: Equatable {
    var savedQueries: [SavedQuerySummary] = []
    var baseFiles: [BaseFileSummary] = []
    var dashboards: [DashboardSummary] = []
    var pinnedSavedQueryIDs: [String] = []
}

/// Bases tab lifecycle (Milestone N, #702): the `.base` arm of the
/// single navigation funnel, the per-path document registry, and the
/// palette command actions.
extension AppState {
    static let baseDocumentOpeningDisabledReason =
        "This Base is still opening. Wait for it to finish before making changes."
    static let baseDocumentReopeningDisabledReason =
        "This Base is reopening. Wait for it to finish before making changes."
    static let baseDocumentRetargetFailedDisabledReason =
        "This Base could not be reopened. Choose Retry before making changes."
    static let baseDocumentUnavailableDisabledReason =
        "This Base is no longer available. Choose Refresh if the file is restored."
    static let baseQueryBuilderSourceUnavailableReason =
        "The source Base is no longer available. Keep this draft or save it as a new .base file."

    func baseDocument(for path: String) -> BaseDocument {
        baseDocument(for: .file(path: path))
    }

    func baseDocument(for source: BaseDocumentSource) -> BaseDocument {
        if let existing = baseDocuments[source.key] { return existing }
        let doc = BaseDocument(source: source)
        baseDocuments[source.key] = doc
        return doc
    }

    /// Handle/state half of Base interaction admission. Kept separate from the
    /// path-capability check so views can preserve their existing component-safe
    /// quarantine assertion while sharing one detached-handle contract.
    func baseDocumentAvailabilityDisabledReason(
        for document: BaseDocument
    ) -> String? {
        if document.handle != nil { return nil }
        if document.hasPendingRetargetPreparation {
            if case .degraded = document.state {
                return Self.baseDocumentRetargetFailedDisabledReason
            }
            return Self.baseDocumentReopeningDisabledReason
        }
        switch document.state {
        case .loading:
            return Self.baseDocumentOpeningDisabledReason
        case .failed:
            return Self.baseDocumentUnavailableDisabledReason
        case .ready, .degraded:
            return Self.baseDocumentUnavailableDisabledReason
        }
    }

    func baseDocumentInteractionDisabledReason(
        for document: BaseDocument
    ) -> String? {
        if let path = document.source.filePath {
            switch batchTrashPathCapability(for: path) {
            case .writable:
                break
            case .readOnly(let reason), .invalid(let reason):
                return reason
            }
        }
        return baseDocumentAvailabilityDisabledReason(for: document)
    }

    /// Shared admission backstop for Base interactions that can also arrive
    /// through menus or keyboard commands while the mounted controls are
    /// disabled. A detached document must never silently accept or ignore the
    /// action; VoiceOver receives the same reason shown by the Base surface.
    @discardableResult
    func admitBaseDocumentInteraction(_ document: BaseDocument) -> Bool {
        guard let reason = baseDocumentInteractionDisabledReason(for: document)
        else { return true }
        postMutationAnnouncement(reason)
        return false
    }

    var activeBaseInteractionDisabledReason: String? {
        guard let document = activeBaseDocument else { return nil }
        return baseDocumentInteractionDisabledReason(for: document)
    }

    func baseDefinitionEditingDisabledReason(for document: BaseDocument) -> String? {
        if case .savedQuery = document.source { return nil }
        return baseDocumentInteractionDisabledReason(for: document)
    }

    var activeBaseDefinitionEditingDisabledReason: String? {
        guard let document = activeBaseDocument else { return nil }
        return baseDefinitionEditingDisabledReason(for: document)
    }

    /// Refresh remains the recovery route for a terminal unavailable Base, but
    /// it must not cancel a quarantine or an already-reserved asynchronous
    /// reopen. Those transient states use Check Again / Retry instead.
    func baseDocumentRefreshDisabledReason(for document: BaseDocument) -> String? {
        if let path = document.source.filePath {
            switch batchTrashPathCapability(for: path) {
            case .writable:
                break
            case .readOnly(let reason), .invalid(let reason):
                return reason
            }
        }
        guard document.hasPendingRetargetPreparation else { return nil }
        return baseDocumentAvailabilityDisabledReason(for: document)
    }

    var activeBaseRefreshDisabledReason: String? {
        guard let document = activeBaseDocument else { return nil }
        return baseDocumentRefreshDisabledReason(for: document)
    }

    func baseRecoveryActionLabel(for document: BaseDocument) -> String? {
        if let path = document.source.filePath,
            isBatchTrashPathQuarantined(path)
        {
            return BatchTrashCopy.checkAgainLabel
        }
        if document.hasPendingRetargetPreparation,
            !document.isRetargetPreparationInFlight
        {
            return "Retry"
        }
        return nil
    }

    func baseRecoveryActionHint(for document: BaseDocument) -> String? {
        if let path = document.source.filePath,
            isBatchTrashPathQuarantined(path)
        {
            return BatchTrashCopy.checkAgainHint
        }
        if document.hasPendingRetargetPreparation,
            !document.isRetargetPreparationInFlight
        {
            return "Attempts to reopen the Base at its current path."
        }
        return nil
    }

    @discardableResult
    func retryBaseRecovery(for document: BaseDocument) -> Task<Void, Never>? {
        if let reason = structuralMutationDisabledReason {
            postMutationAnnouncement(reason)
            return nil
        }
        if let path = document.source.filePath,
            isBatchTrashPathQuarantined(path)
        {
            return retryBatchTrashUnknownReconciliation()
        }
        guard document.hasPendingRetargetPreparation,
            !document.isRetargetPreparationInFlight,
            let session = currentSession,
            baseDocuments[document.source.key] === document
        else { return nil }
        scheduleBaseRetargetPreparationIfNeeded(
            document: document,
            owner: .registry(key: document.source.key),
            source: document.source,
            session: session)
        return nativeDocumentRetargetTask
    }

    /// The single file-backed Base load gate. Unknown batch-Trash paths keep
    /// their current Swift snapshot but must not acquire a native handle until
    /// reconciliation proves the definition is still present.
    @discardableResult
    func loadBaseDocumentIfAllowed(
        _ document: BaseDocument,
        session: VaultSession,
        thisPath: String? = nil
    ) -> Bool {
        if let path = document.source.filePath,
            isBatchTrashPathQuarantined(path)
        {
            return false
        }
        document.load(session: session, thisPath: thisPath)
        return document.handle != nil
    }

    func baseEmbedHandle(for request: BaseEmbedRequest, thisPath: String?) -> BaseEmbedHandle {
        let key = BaseEmbedCacheKey(request: request, thisPath: thisPath)
        if let existing = baseEmbedHandles[key] { return existing }
        let handle = BaseEmbedHandle(request: request, thisPath: thisPath)
        baseEmbedHandles[key] = handle
        return handle
    }

    /// File-backed embeds share the Base definition quarantine. Inline and
    /// saved-query embeds have no quarantinable vault path and remain usable.
    @discardableResult
    func loadBaseEmbedDocumentIfAllowed(
        _ document: BaseEmbedDocument,
        session: VaultSession
    ) -> Bool {
        if let path = document.request.targetPath,
            isBatchTrashPathQuarantined(path)
        {
            return false
        }
        document.load(session: session)
        return document.handle != nil
    }

    /// Detach every currently registered file-backed embed covered by the
    /// central unknown-outcome gate. `BaseEmbedDocument` retains its rendered
    /// result and interaction state while the shared native handle is closed.
    func quarantineUnknownBaseEmbedHandles(session: VaultSession) {
        guard currentSession === session else { return }
        let registered = baseEmbedHandles.sorted { lhs, rhs in
            BaseExactIdentity.lessThan(
                lhs.key.exactIdentityKey, rhs.key.exactIdentityKey)
        }
        for (key, handle) in registered {
            guard baseEmbedHandles[key] === handle,
                let path = handle.request.targetPath,
                isBatchTrashPathQuarantined(path),
                handle.session === session
            else { continue }
            handle.close(session: session)
        }
    }

    /// Reopen only file-backed embeds covered by roots whose post-refresh
    /// probe proved presence. Indeterminate roots never reach this function.
    func resumePresentBaseEmbedHandles(
        _ items: [StructuralBatchItem],
        session: VaultSession
    ) {
        guard currentSession === session, !items.isEmpty else { return }
        let presentPaths = VaultComponentPrefixIndex(
            items.map {
                VaultComponentPrefixIndex<StructuralBatchItem>.Entry(
                    path: $0.path,
                    includesDescendants: $0.isDirectory,
                    value: $0)
            })
        let registered = baseEmbedHandles.sorted { lhs, rhs in
            BaseExactIdentity.lessThan(
                lhs.key.exactIdentityKey, rhs.key.exactIdentityKey)
        }
        for (key, handle) in registered {
            guard baseEmbedHandles[key] === handle,
                let path = handle.request.targetPath,
                presentPaths.longestMatch(for: path) != nil,
                !isBatchTrashPathQuarantined(path)
            else { continue }
            let documents = handle.liveDocuments
            guard !documents.isEmpty else { continue }
            do {
                try handle.loadIfNeeded(session: session)
                for document in documents {
                    if document.needsInitialLoad {
                        document.load(session: session)
                    } else {
                        document.refreshAfterSharedHandleReload(session: session)
                    }
                }
            } catch {
                documents.forEach { $0.failRefresh(error) }
            }
        }
    }

    /// A definite absent probe may replace the snapshot preserved during the
    /// unknown window with an honest unavailable state. Matching remains
    /// component-safe for unknown directory roots.
    func invalidateAbsentBaseEmbedHandles(
        _ items: [StructuralBatchItem],
        session: VaultSession
    ) {
        guard currentSession === session, !items.isEmpty else { return }
        let absentPaths = VaultComponentPrefixIndex(
            items.map {
                VaultComponentPrefixIndex<StructuralBatchItem>.Entry(
                    path: $0.path,
                    includesDescendants: $0.isDirectory,
                    value: $0)
            })
        let registered = baseEmbedHandles.sorted { lhs, rhs in
            BaseExactIdentity.lessThan(
                lhs.key.exactIdentityKey, rhs.key.exactIdentityKey)
        }
        for (key, handle) in registered {
            guard baseEmbedHandles[key] === handle,
                let path = handle.request.targetPath,
                absentPaths.longestMatch(for: path) != nil
            else { continue }
            if handle.session === session {
                handle.close(session: session)
            }
            handle.liveDocuments.forEach { $0.invalidateMovedToTrash() }
        }
    }

    func openBaseEmbedDestination(_ destination: BaseEmbedOpenDestination) {
        switch destination {
        case .baseFile(let path):
            openFile(path, target: .newTab)
        case .sourceNote(let path):
            openFile(path, target: .newTab)
            setViewMode(.editing)
        case .savedQuery(let reference):
            let resolved = BaseEmbedRequest.savedQuerySummary(
                reference: reference, in: baseQueries.savedQueries)
            guard let resolved else {
                postBaseActionAnnouncement("Saved query \(reference) is no longer available.")
                return
            }
            openSavedQuery(resolved, target: .newTab)
        }
    }

    func openBaseFile(
        _ path: String,
        target: OpenTarget = .currentTab,
        advancesSidebarSelectionRevision: Bool = true
    ) {
        if let reason = propertyEditNavigationDisabledReason {
            postMutationAnnouncement(reason)
            return
        }
        if advancesSidebarSelectionRevision {
            recordExplicitSidebarNavigationIntent()
        }
        switch target {
        case .currentTab:
            if let existing = workspace.activeGroupBaseTab(forPath: path) {
                activateTab(existing.id)
                return
            }
            guard admitCurrentTabReplacementForPropertyRecovery() else { return }
            clearActiveBaseQuickFilter()
            parkOutgoingNoteBuffer()
            if workspace.activeTab != nil {
                let replacedItem = workspace.activeTab?.item
                workspace.replaceActiveItem(.base(path: path))
                releaseCanvasDocumentIfUnreferenced(replacedItem)
                releaseBaseDocumentIfUnreferenced(replacedItem)
                releaseDashboardDocumentIfUnreferenced(replacedItem)
                if let id = workspace.model.activeGroup.activeTabID {
                    clearBaseRendererOverride(for: id)
                    activateTab(id)
                }
            } else {
                let id = workspace.openTab(.base(path: path))
                activateTab(id)
            }
        case .newTab:
            if let existing = workspace.activeGroupBaseTab(forPath: path) {
                activateTab(existing.id)
                return
            }
            clearActiveBaseQuickFilter()
            parkOutgoingNoteBuffer()
            let id = workspace.openTab(.base(path: path))
            activateTab(id)
        case .newSplit(let axis):
            clearActiveBaseQuickFilter()
            let paneCount = workspace.model.groupsInOrder.count
            splitActivePane(axis: axis)
            if workspace.model.groupsInOrder.count == paneCount {
                openBaseFile(
                    path,
                    target: .newTab,
                    advancesSidebarSelectionRevision: false)
                return
            }
            openBaseFile(
                path,
                target: .currentTab,
                advancesSidebarSelectionRevision: false)
        }
    }

    func openSavedQuery(
        _ summary: SavedQuerySummary,
        target: OpenTarget = .currentTab,
        advancesSidebarSelectionRevision: Bool = true
    ) {
        openSavedQuery(
            id: summary.id,
            name: summary.name,
            target: target,
            advancesSidebarSelectionRevision: advancesSidebarSelectionRevision)
    }

    func openSavedQuery(
        id: String,
        name: String,
        target: OpenTarget = .currentTab,
        advancesSidebarSelectionRevision: Bool = true
    ) {
        if let reason = propertyEditNavigationDisabledReason {
            postMutationAnnouncement(reason)
            return
        }
        if advancesSidebarSelectionRevision {
            recordExplicitSidebarNavigationIntent()
        }
        let item = EditorItem.savedQuery(id: id, name: name)
        switch target {
        case .currentTab:
            if let existing = workspace.activeGroupSavedQueryTab(id: id) {
                activateTab(existing.id)
                return
            }
            guard admitCurrentTabReplacementForPropertyRecovery() else { return }
            clearActiveBaseQuickFilter()
            parkOutgoingNoteBuffer()
            if workspace.activeTab != nil {
                let replacedItem = workspace.activeTab?.item
                workspace.replaceActiveItem(item)
                releaseCanvasDocumentIfUnreferenced(replacedItem)
                releaseBaseDocumentIfUnreferenced(replacedItem)
                releaseDashboardDocumentIfUnreferenced(replacedItem)
                if let tabID = workspace.model.activeGroup.activeTabID {
                    clearBaseRendererOverride(for: tabID)
                    activateTab(tabID)
                }
            } else {
                let tabID = workspace.openTab(item)
                activateTab(tabID)
            }
        case .newTab:
            if let existing = workspace.activeGroupSavedQueryTab(id: id) {
                activateTab(existing.id)
                return
            }
            clearActiveBaseQuickFilter()
            parkOutgoingNoteBuffer()
            let tabID = workspace.openTab(item)
            activateTab(tabID)
        case .newSplit(let axis):
            clearActiveBaseQuickFilter()
            let paneCount = workspace.model.groupsInOrder.count
            splitActivePane(axis: axis)
            if workspace.model.groupsInOrder.count == paneCount {
                openSavedQuery(
                    id: id,
                    name: name,
                    target: .newTab,
                    advancesSidebarSelectionRevision: false)
                return
            }
            openSavedQuery(
                id: id,
                name: name,
                target: .currentTab,
                advancesSidebarSelectionRevision: false)
        }
    }

    func openDashboard(
        id: String,
        name: String,
        target: OpenTarget = .currentTab,
        advancesSidebarSelectionRevision: Bool = true
    ) {
        if let reason = propertyEditNavigationDisabledReason {
            postMutationAnnouncement(reason)
            return
        }
        if advancesSidebarSelectionRevision {
            recordExplicitSidebarNavigationIntent()
        }
        let item = EditorItem.dashboard(id: id, name: name)
        switch target {
        case .currentTab:
            if let existing = workspace.activeGroupDashboardTab(id: id) {
                activateTab(existing.id)
                return
            }
            guard admitCurrentTabReplacementForPropertyRecovery() else { return }
            clearActiveBaseQuickFilter()
            parkOutgoingNoteBuffer()
            if workspace.activeTab != nil {
                let replacedItem = workspace.activeTab?.item
                workspace.replaceActiveItem(item)
                releaseCanvasDocumentIfUnreferenced(replacedItem)
                releaseBaseDocumentIfUnreferenced(replacedItem)
                releaseDashboardDocumentIfUnreferenced(replacedItem)
                if let tabID = workspace.model.activeGroup.activeTabID {
                    activateTab(tabID)
                }
            } else {
                let tabID = workspace.openTab(item)
                activateTab(tabID)
            }
        case .newTab:
            if let existing = workspace.activeGroupDashboardTab(id: id) {
                activateTab(existing.id)
                return
            }
            clearActiveBaseQuickFilter()
            parkOutgoingNoteBuffer()
            let tabID = workspace.openTab(item)
            activateTab(tabID)
        case .newSplit(let axis):
            clearActiveBaseQuickFilter()
            let paneCount = workspace.model.groupsInOrder.count
            splitActivePane(axis: axis)
            if workspace.model.groupsInOrder.count == paneCount {
                openDashboard(
                    id: id,
                    name: name,
                    target: .newTab,
                    advancesSidebarSelectionRevision: false)
                return
            }
            openDashboard(
                id: id,
                name: name,
                target: .currentTab,
                advancesSidebarSelectionRevision: false)
        }
    }

    var orderedSavedQuerySummaries: [SavedQuerySummary] {
        let pinOrder = Dictionary(
            uniqueKeysWithValues: baseQueries.pinnedSavedQueryIDs.enumerated().map { ($0.element, $0.offset) })
        return baseQueries.savedQueries.sorted { lhs, rhs in
            switch (pinOrder[lhs.id], pinOrder[rhs.id]) {
            case let (left?, right?):
                return left < right
            case (_?, nil):
                return true
            case (nil, _?):
                return false
            case (nil, nil):
                let byName = lhs.name.localizedCaseInsensitiveCompare(rhs.name)
                return byName == .orderedSame ? lhs.id < rhs.id : byName == .orderedAscending
            }
        }
    }

    var baseQueriesAccessibilityValue: String {
        let count =
            baseQueries.savedQueries.count + baseQueries.baseFiles.count
            + baseQueries.dashboards.count
        let pinned = baseQueries.pinnedSavedQueryIDs.count
        return "Queries, \(count) items, \(pinned) pinned"
    }

    @discardableResult
    func refreshBaseQueries() -> Task<Void, Never>? {
        baseQueriesRefreshGeneration &+= 1
        let generation = baseQueriesRefreshGeneration
        guard let session = currentSession else {
            baseQueriesRefreshTask?.cancel()
            baseQueriesRefreshTask = nil
            resetBaseQueriesForClosedVault()
            return nil
        }

        let observer = baseRetargetNativeExecutionObserverForTesting
        let task = Task { @MainActor [weak self, session, observer] in
            let outcome: BaseQueriesRefreshOutcome = await Task.detached(
                priority: .userInitiated
            ) {
                do {
                    BasePreparedLoader.observe(.listSavedQueries, observer: observer)
                    let saved = try session.listSavedQueries()
                    BasePreparedLoader.observe(.listBases, observer: observer)
                    let bases = try session.basesList()
                    BasePreparedLoader.observe(.listDashboards, observer: observer)
                    let dashboards = try session.listDashboards()
                    return .success(saved: saved, bases: bases, dashboards: dashboards)
                } catch {
                    return .failure(error.localizedDescription)
                }
            }.value

            guard let self,
                self.currentSession === session,
                self.baseQueriesRefreshGeneration == generation
            else { return }

            switch outcome {
            case .success(let unsortedSaved, let unsortedBases, let unsortedDashboards):
                let saved = unsortedSaved.sorted(by: self.savedQuerySort)
                let baseFiles = unsortedBases.sorted {
                    $0.path.localizedCaseInsensitiveCompare($1.path) == .orderedAscending
                }
                let dashboards = unsortedDashboards.sorted(by: self.dashboardSort)
                let validIDs = Set(saved.map(\.id))
                let pins = self.baseQueries.pinnedSavedQueryIDs.filter {
                    validIDs.contains($0)
                }
                self.baseQueries = BaseQueriesState(
                    savedQueries: saved,
                    baseFiles: baseFiles,
                    dashboards: dashboards,
                    pinnedSavedQueryIDs: pins)
                self.persistBaseQueryPinsIfNeeded(pins)
                self.retargetOpenSavedQueries(saved)
                self.retargetOpenDashboards(dashboards)
                self.refreshSavedQueryCommands(saved)
            case .failure(let message):
                self.postBaseActionAnnouncement("Queries could not be refreshed: \(message)")
            }
        }
        baseQueriesRefreshTask = task
        return task
    }

    func resetBaseQueriesForClosedVault() {
        refreshSavedQueryCommands([])
        baseQueries = BaseQueriesState(
            pinnedSavedQueryIDs: preferencesStore.loadBaseQueryPrefs().pinnedSavedQueryIDs)
        clearBasesDock()
    }

    func resetBaseRefreshTasksForVaultTransition() {
        baseQueriesRefreshGeneration &+= 1
        baseQueriesRefreshTask?.cancel()
        baseQueriesRefreshTask = nil
        visibleBasesRefreshGeneration &+= 1
        visibleBasesRefreshTask?.cancel()
        visibleBasesRefreshTask = nil
        savedQueryUpdateGeneration &+= 1
        savedQueryUpdateTask?.cancel()
        savedQueryUpdateTask = nil
    }

    func toggleSavedQueryPin(id: String) {
        var pins = baseQueries.pinnedSavedQueryIDs
        if let index = pins.firstIndex(of: id) {
            pins.remove(at: index)
        } else if baseQueries.savedQueries.contains(where: { $0.id == id }) {
            pins.insert(id, at: 0)
        } else {
            return
        }
        baseQueries.pinnedSavedQueryIDs = pins
        persistBaseQueryPinsIfNeeded(pins)
    }

    func runSavedQuery(id: String) {
        guard let summary = savedQuerySummary(id: id) else {
            postBaseActionAnnouncement("Saved query is no longer available.")
            return
        }
        openSavedQuery(summary)
    }

    func editSavedQueryInBuilder(id: String) {
        guard let session = currentSession else { return }
        do {
            let saved = try session.getSavedQuery(id: id)
            let draft = try BaseQueryBuilderDraft(queryJSON: saved.queryJson)
            activeBaseQueryBuilder = BaseQueryBuilderModel(
                draft: draft,
                editingSavedQuery: EditingSavedQuery(
                    id: saved.id,
                    name: saved.name,
                    description: saved.description))
            postBaseActionAnnouncement("Editing \(saved.name) in builder.")
        } catch {
            postBaseActionAnnouncement("Saved query could not be edited: \(error.localizedDescription)")
        }
    }

    func renameSavedQuery(id: String, name: String) {
        guard let session = currentSession else { return }
        let trimmed = name.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else {
            postBaseActionAnnouncement("Enter a saved query name before renaming.")
            return
        }
        do {
            try session.renameSavedQuery(id: id, name: trimmed)
            refreshBaseQueries()
            reloadDashboardDocumentsAfterSavedQueryChange()
            postBaseActionAnnouncement("Renamed saved query to \(trimmed).")
        } catch {
            postBaseActionAnnouncement("Saved query could not be renamed: \(error.localizedDescription)")
        }
    }

    func deleteSavedQuery(id: String) {
        guard let session = currentSession else { return }
        do {
            try session.deleteSavedQuery(id: id)
            reloadRegisteredSavedQueryEmbeds(
                id: id,
                session: session,
                includeUnleasedHandles: true)
            baseQueries.pinnedSavedQueryIDs.removeAll { $0 == id }
            persistBaseQueryPinsIfNeeded(baseQueries.pinnedSavedQueryIDs)
            closeOpenSavedQueryTabs(id: id)
            if case .savedQuery(let dockedID, _) = basesDock.target, dockedID == id {
                clearBasesDock()
            }
            refreshBaseQueries()
            reloadDashboardDocumentsAfterSavedQueryChange()
            postBaseActionAnnouncement("Deleted saved query.")
        } catch {
            postBaseActionAnnouncement("Saved query could not be deleted: \(error.localizedDescription)")
        }
    }

    @discardableResult
    func exportSavedQuery(
        id: String,
        path: String,
        nativeThreadObserver: (@Sendable (Bool) -> Void)? = nil
    ) -> Task<Void, Never>? {
        guard let session = currentSession else { return nil }
        let trimmed = path.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else {
            postBaseActionAnnouncement("Choose a .base path before exporting.")
            return nil
        }
        guard admitStructuralMutationRequest() else { return nil }
        guard let recoveryReservation =
                admitStructuralRecoveryDestination(trimmed),
            admitBatchTrashWrite(to: [trimmed])
        else { return nil }

        let vaultURL = currentVaultURL
        let token = beginStructuralMutation(
            recoveryReservation: recoveryReservation)
        let refresher = structuralBatchRefreshRunner
        let task = Task { @MainActor [weak self] in
            let outcome = await Task.detached(priority: .userInitiated) {
                let existedBefore = vaultURL.map {
                    FileManager.default.fileExists(
                        atPath: $0.appendingPathComponent(trimmed).path)
                } ?? false
                do {
                    nativeThreadObserver?(BaseWriterThreadProbe.isMainThread())
                    try session.exportSavedQueryAsBase(id: id, path: trimmed)
                    return BaseVaultWriteOutcome.success(existedBefore: existedBefore)
                } catch {
                    return BaseVaultWriteOutcome.failure(error.localizedDescription)
                }
            }.value

            guard let self else { return }
            defer { self.endStructuralMutation(token) }
            guard self.ownsStructuralMutation(token, session: session) else { return }

            switch outcome {
            case .success(let existedBefore):
                await refresher(self)
                guard self.ownsStructuralMutation(token, session: session) else { return }
                self.barrierStructuralUndoForCreatedVaultPath(
                    relativePath: trimmed, existedBefore: existedBefore)
                await self.refreshBaseQueries()?.value
                guard self.ownsStructuralMutation(token, session: session) else { return }
                _ = await self.refreshVisibleBasesAfterInAppWrite(
                    session: session,
                    changedPath: trimmed)?.value
                guard self.ownsStructuralMutation(token, session: session) else { return }
                self.postBaseActionAnnouncement("Exported saved query as \(trimmed).")
            case .failure(let message):
                self.postBaseActionAnnouncement(
                    "Saved query could not be exported: \(message)")
            }
        }
        recordPendingStructuralTask(task)
        return task
    }

    func exportSavedQueryUsingSavePanel(id: String) {
        guard let originSession = currentSession else { return }
        guard let summary = savedQuerySummary(id: id) else {
            postBaseActionAnnouncement("Saved query is no longer available.")
            return
        }
        guard admitStructuralMutationRequest() else { return }
        let originVaultURL = currentVaultURL
        let panel = NSSavePanel()
        panel.directoryURL = originVaultURL
        panel.canCreateDirectories = true
        panel.nameFieldStringValue = "\(summary.name).base"
        panel.begin { [weak self] response in
            guard response == .OK, let url = panel.url else { return }
            Task { @MainActor [weak self] in
                guard let self else { return }
                guard self.currentSession === originSession else { return }
                guard
                    let path = Self.vaultRelativePath(
                        of: url, vaultURL: originVaultURL)
                else {
                    self.postBaseActionAnnouncement("Choose a path inside the vault.")
                    return
                }
                _ = self.exportSavedQuery(id: id, path: path)
            }
        }
    }

    @discardableResult
    func saveDashboard(name: String, sections: [DashboardSection]) -> String? {
        guard let session = currentSession else { return nil }
        let trimmed = name.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else {
            postBaseActionAnnouncement("Enter a dashboard name before saving.")
            return nil
        }
        do {
            let id = try session.saveDashboard(name: trimmed, sections: sections)
            refreshBaseQueries()
            postBaseActionAnnouncement("Saved dashboard \(trimmed).")
            return id
        } catch {
            postBaseActionAnnouncement("Dashboard could not be saved: \(error.localizedDescription)")
            return nil
        }
    }

    @discardableResult
    func updateDashboard(id: String, name: String, sections: [DashboardSection]) -> Bool {
        guard let session = currentSession else { return false }
        let trimmed = name.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else {
            postBaseActionAnnouncement("Enter a dashboard name before saving.")
            return false
        }
        do {
            try session.updateDashboard(id: id, name: trimmed, sections: sections)
            dashboardDocuments[id]?.load(session: session, thisPath: nil)
            if case .dashboard(let dockedID, _) = basesDock.target, dockedID == id {
                basesDockDashboardDocument?.load(session: session, thisPath: basesDockActiveNotePath)
                if let docked = basesDockDashboardDocument {
                    basesDock.rebaseMembership(docked.membershipSignature)
                }
            }
            refreshBaseQueries()
            postBaseActionAnnouncement("Updated dashboard \(trimmed).")
            return true
        } catch {
            postBaseActionAnnouncement("Dashboard could not be updated: \(error.localizedDescription)")
            return false
        }
    }

    func removeMissingDashboardSection(
        dashboardID: String,
        index: Int,
        expectedSections: [DashboardSection]
    ) {
        guard let session = currentSession else { return }
        do {
            let dashboard = try session.getDashboard(id: dashboardID)
            let freshSections = editableDashboardSections(dashboard.sections)
            guard freshSections == expectedSections,
                dashboard.sections.indices.contains(index),
                dashboard.sections[index].missing
            else {
                postBaseActionAnnouncement("Dashboard section changed; reload and try again.")
                return
            }
            var sections = freshSections
            sections.remove(at: index)
            updateDashboard(id: dashboardID, name: dashboard.name, sections: sections)
        } catch {
            postBaseActionAnnouncement(
                "Dashboard section could not be removed: \(error.localizedDescription)")
        }
    }

    func replaceMissingDashboardSection(
        dashboardID: String,
        index: Int,
        expectedSections: [DashboardSection],
        replacementSavedQueryID: String
    ) {
        guard let session = currentSession else { return }
        do {
            _ = try session.getSavedQuery(id: replacementSavedQueryID)
            let dashboard = try session.getDashboard(id: dashboardID)
            let freshSections = editableDashboardSections(dashboard.sections)
            guard freshSections == expectedSections,
                dashboard.sections.indices.contains(index),
                dashboard.sections[index].missing
            else {
                postBaseActionAnnouncement("Dashboard section changed; reload and try again.")
                return
            }
            var sections = freshSections
            let current = sections[index]
            sections[index] = DashboardSection(
                savedQueryId: replacementSavedQueryID,
                headingOverride: current.headingOverride,
                viewOverride: current.viewOverride)
            updateDashboard(id: dashboardID, name: dashboard.name, sections: sections)
        } catch {
            postBaseActionAnnouncement(
                "Dashboard section could not be replaced: \(error.localizedDescription)")
        }
    }

    private func editableDashboardSections(
        _ statuses: [DashboardSectionStatus]
    ) -> [DashboardSection] {
        statuses.map {
            DashboardSection(
                savedQueryId: $0.savedQueryId,
                headingOverride: $0.headingOverride,
                viewOverride: $0.viewOverride)
        }
    }

    func deleteDashboard(id: String) {
        guard let session = currentSession else { return }
        do {
            try session.deleteDashboard(id: id)
            closeOpenDashboardTabs(id: id)
            refreshBaseQueries()
            postBaseActionAnnouncement("Deleted dashboard.")
        } catch {
            postBaseActionAnnouncement("Dashboard could not be deleted: \(error.localizedDescription)")
        }
    }

    func dashboardForEditing(id: String) -> Dashboard? {
        guard let session = currentSession else { return nil }
        do {
            return try session.getDashboard(id: id)
        } catch {
            postBaseActionAnnouncement("Dashboard could not be edited: \(error.localizedDescription)")
            return nil
        }
    }

    private func savedQuerySummary(id: String) -> SavedQuerySummary? {
        baseQueries.savedQueries.first { $0.id == id }
    }

    private func dashboardSummary(id: String) -> DashboardSummary? {
        baseQueries.dashboards.first { $0.id == id }
    }

    private func savedQuerySort(_ lhs: SavedQuerySummary, _ rhs: SavedQuerySummary) -> Bool {
        let byName = lhs.name.localizedCaseInsensitiveCompare(rhs.name)
        return byName == .orderedSame ? lhs.id < rhs.id : byName == .orderedAscending
    }

    private func dashboardSort(_ lhs: DashboardSummary, _ rhs: DashboardSummary) -> Bool {
        let byName = lhs.name.localizedCaseInsensitiveCompare(rhs.name)
        return byName == .orderedSame ? lhs.id < rhs.id : byName == .orderedAscending
    }

    private func persistBaseQueryPinsIfNeeded(_ pins: [String]) {
        let current = preferencesStore.loadBaseQueryPrefs()
        guard current.pinnedSavedQueryIDs != pins else { return }
        preferencesStore.saveBaseQueryPrefs(BaseQueryPrefs(pinnedSavedQueryIDs: pins))
    }

    private func retargetOpenSavedQueries(_ saved: [SavedQuerySummary]) {
        for summary in saved {
            _ = workspace.retargetSavedQuery(id: summary.id, name: summary.name)
            baseDocuments[BaseDocumentSource.savedQuery(id: summary.id, name: summary.name).key]?
                .retargetSavedQueryName(summary.name)
            if case .savedQuery(let id, _) = basesDock.target,
                BaseExactIdentity.matches(id, summary.id)
            {
                basesDock.setTarget(.savedQuery(id: summary.id, name: summary.name))
                basesDockDocument?.retargetSavedQueryName(summary.name)
            }
        }
    }

    private func retargetOpenDashboards(_ dashboards: [DashboardSummary]) {
        for summary in dashboards {
            _ = workspace.retargetDashboard(id: summary.id, name: summary.name)
            dashboardDocuments[summary.id]?.retargetName(summary.name)
            if case .dashboard(let id, _) = basesDock.target,
                BaseExactIdentity.matches(id, summary.id)
            {
                basesDock.setTarget(.dashboard(id: summary.id, name: summary.name))
            }
        }
    }

    private func closeOpenSavedQueryTabs(id: String) {
        let matchingTabs = workspace.model.allTabs.filter { tab in
            if case .savedQuery(let queryID, _) = tab.item {
                return BaseExactIdentity.matches(queryID, id)
            }
            return false
        }
        for tab in matchingTabs {
            performCloseTab(tab.id)
        }
        // The entity is GONE — its reopen records must go with it
        // (both the pushes from the loop above and any older ones).
        workspace.purgeClosedTabs { record in
            if case .savedQuery(let recordID, _) = record.item {
                return BaseExactIdentity.matches(recordID, id)
            }
            return false
        }
        let key = BaseDocumentSource.savedQuery(id: id, name: "").key
        if let doc = baseDocuments[key] {
            if let session = currentSession {
                doc.close(session: session)
            }
            baseDocuments[key] = nil
        }
        saveWorkspaceLayout()
    }

    private func closeOpenDashboardTabs(id: String) {
        let matchingTabs = workspace.model.allTabs.filter { tab in
            if case .dashboard(let dashboardID, _) = tab.item {
                return dashboardID == id
            }
            return false
        }
        for tab in matchingTabs {
            performCloseTab(tab.id)
        }
        workspace.purgeClosedTabs { record in
            if case .dashboard(let recordID, _) = record.item {
                return recordID == id
            }
            return false
        }
        if let doc = dashboardDocuments[id] {
            if let session = currentSession {
                doc.close(session: session)
            }
            dashboardDocuments[id] = nil
        }
        if case .dashboard(let dashboardID, _) = basesDock.target, dashboardID == id {
            clearBasesDock()
        }
        saveWorkspaceLayout()
    }

    private func refreshSavedQueryCommands(_ saved: [SavedQuerySummary]) {
        let wanted = Set(saved.map { SlateCommandID.basesRunSavedQuery(id: $0.id) })
        for id in savedQueryCommandIDs.subtracting(wanted) {
            _ = commandRegistry.unregister(id: id)
        }
        for summary in saved {
            let id = SlateCommandID.basesRunSavedQuery(id: summary.id)
            _ = commandRegistry.register(
                command: Command(
                    id: id,
                    label: "Run query: \(summary.name)",
                    accessibilityHint: "Open the saved query.",
                    hotkeyHint: nil,
                    section: .bases),
                action: MenuCommandAction { [weak self] in
                    self?.runSavedQuery(id: summary.id)
                })
        }
        savedQueryCommandIDs = wanted
    }

    func activateBaseTab(_ id: TabID, path: String) {
        activateBaseDocumentTab(id, source: .file(path: path), selectedPath: path)
    }

    func activateSavedQueryTab(_ id: TabID, savedQueryID: String, name: String) {
        activateBaseDocumentTab(id, source: .savedQuery(id: savedQueryID, name: name), selectedPath: nil)
    }

    func dashboardDocument(id: String, name: String) -> DashboardDocument {
        if let existing = dashboardDocuments[id] { return existing }
        let doc = DashboardDocument(id: id, name: name)
        dashboardDocuments[id] = doc
        return doc
    }

    func activateDashboardTab(_ id: TabID, dashboardID: String, name: String) {
        if id == workspace.model.activeGroup.activeTabID,
            selectedFilePath == nil,
            dashboardDocuments[dashboardID]?.dashboard != nil
        {
            return
        }
        workspace.markEditorRegionActive()
        if let pending = pendingTabCloseAfterSave, pending != id {
            pendingTabCloseAfterSave = nil
        }
        isActivatingTab = true
        defer { isActivatingTab = false }
        parkOutgoingNoteBuffer()
        cancelNoteScopedWork()
        clearActiveNoteFields()
        workspace.select(id)
        clearTransitionSensitiveCollections()
        let doc = dashboardDocument(id: dashboardID, name: name)
        if doc.dashboard == nil, let session = currentSession {
            doc.load(session: session)
        }
        if selectedFilePath != nil {
            selectedFilePath = nil
        }
        clearActiveBaseSelection()
    }

    private func activateBaseDocumentTab(
        _ id: TabID,
        source: BaseDocumentSource,
        selectedPath: String?
    ) {
        if id == workspace.model.activeGroup.activeTabID,
            BaseExactIdentity.matches(selectedFilePath, selectedPath),
            baseDocuments[source.key]?.handle != nil
        {
            return
        }
        workspace.markEditorRegionActive()
        if let pending = pendingTabCloseAfterSave, pending != id {
            pendingTabCloseAfterSave = nil
        }
        isActivatingTab = true
        defer { isActivatingTab = false }
        parkOutgoingNoteBuffer()
        cancelNoteScopedWork()
        clearActiveNoteFields()
        workspace.select(id)
        clearTransitionSensitiveCollections()
        let doc = baseDocument(for: source)
        let isQuarantined = source.filePath.map(isBatchTrashPathQuarantined) ?? false
        if doc.handle == nil, !isQuarantined, let session = currentSession {
            if doc.hasPendingRetargetPreparation {
                scheduleBaseRetargetPreparationIfNeeded(
                    document: doc,
                    owner: .registry(key: source.key),
                    source: source,
                    session: session)
            } else {
                loadBaseDocumentIfAllowed(doc, session: session)
            }
        }
        if !BaseExactIdentity.matches(selectedFilePath, selectedPath) {
            selectedFilePath = selectedPath
        }
        if !BaseExactIdentity.matches(activeBaseSelectionPath, source.selectionKey) {
            clearActiveBaseSelection()
        }
    }

    var activeBaseDocument: BaseDocument? {
        guard let tab = workspace.activeTab, let source = BaseDocumentSource(item: tab.item)
        else { return nil }
        return baseDocument(for: source)
    }

    var activeDashboardDocument: DashboardDocument? {
        guard let tab = workspace.activeTab,
            case .dashboard(let id, let name) = tab.item
        else { return nil }
        return dashboardDocument(id: id, name: name)
    }

    func baseRendererOverride(for tabID: TabID) -> BaseRendererMode? {
        baseRendererOverrides[tabID]
    }

    func basesViewAsTable() {
        setActiveBaseRendererOverride(.table)
    }

    func basesViewAsList() {
        setActiveBaseRendererOverride(.list)
    }

    func basesFocusQuickFilter() {
        guard let document = activeBaseDocument,
            admitBaseDocumentInteraction(document)
        else { return }
        baseQuickFilterFocusToken += 1
    }

    @discardableResult
    func basesWhereAmI() -> String? {
        guard let doc = activeBaseDocument else { return nil }
        let text = doc.whereAmIReadback
        // W0.5-3 residue: BaseDocument.whereAmIReadback
        postAccessibilityAnnouncement(.hostComposed(text: text, priority: .medium))
        return text
    }

    func clearBaseQuickFilterIfLeavingActiveTab(for destination: TabID) {
        guard let current = workspace.activeTab,
            current.id != destination
        else { return }
        clearActiveBaseQuickFilter()
    }

    func clearActiveBaseQuickFilter() {
        guard let item = workspace.activeTab?.item,
            let source = BaseDocumentSource(item: item),
            let doc = baseDocuments[source.key]
        else { return }
        _ = doc.clearQuickFilter(session: currentSession)
    }

    func dockBaseFileToSidebar(path: String, name: String? = nil, refreshDelayNanoseconds: UInt64 = 500_000_000) {
        basesDock.setTarget(.base(
            path: path,
            name: name ?? ((path as NSString).lastPathComponent as NSString).deletingPathExtension))
        workspace.activeLeaf = .basesDock
        isRightPaneVisible = true  // #882: un-hide the pane on reveal
        scheduleBasesDockFollowActiveRefresh(delayNanoseconds: refreshDelayNanoseconds)
    }

    func dockSavedQueryToSidebar(id: String, refreshDelayNanoseconds: UInt64 = 500_000_000) {
        guard let summary = savedQuerySummary(id: id) else {
            postBaseActionAnnouncement("Saved query is no longer available.")
            return
        }
        basesDock.setTarget(.savedQuery(id: summary.id, name: summary.name))
        workspace.activeLeaf = .basesDock
        isRightPaneVisible = true  // #882: un-hide the pane on reveal
        scheduleBasesDockFollowActiveRefresh(delayNanoseconds: refreshDelayNanoseconds)
    }

    func dockDashboardToSidebar(id: String, refreshDelayNanoseconds: UInt64 = 500_000_000) {
        guard let summary = dashboardSummary(id: id) else {
            postBaseActionAnnouncement("Dashboard is no longer available.")
            return
        }
        basesDock.setTarget(.dashboard(id: summary.id, name: summary.name))
        workspace.activeLeaf = .basesDock
        isRightPaneVisible = true  // #882: un-hide the pane on reveal
        scheduleBasesDockFollowActiveRefresh(delayNanoseconds: refreshDelayNanoseconds)
    }

    func scheduleBasesDockFollowActiveRefresh(delayNanoseconds: UInt64 = 500_000_000) {
        basesDockRefreshTask?.cancel()
        guard let target = basesDock.target, let session = currentSession else { return }
        let thisPath = basesDockActiveNotePath
        basesDock.thisPath = thisPath
        basesDockRefreshTask = Task { @MainActor [weak self, target, session, thisPath] in
            do {
                if delayNanoseconds > 0 {
                    try await Task.sleep(nanoseconds: delayNanoseconds)
                }
            } catch {
                return
            }
            guard let self, !Task.isCancelled, self.basesDock.target == target else { return }
            self.refreshBasesDockTarget(target, session: session, thisPath: thisPath)
        }
    }

    func clearBasesDock() {
        basesDockRefreshTask?.cancel()
        basesDockRefreshTask = nil
        if let session = currentSession {
            basesDockDocument?.close(session: session)
            basesDockDashboardDocument?.close(session: session)
        }
        basesDock = BasesDockState()
        basesDockDocument = nil
        basesDockDashboardDocument = nil
    }

    private func refreshBasesDockTarget(
        _ target: BasesDockTarget,
        session: VaultSession,
        thisPath: String?
    ) {
        let membership: BaseRowMembership
        switch target {
        case .base(let path, _):
            if let dashboardDoc = basesDockDashboardDocument {
                dashboardDoc.close(session: session)
            }
            basesDockDashboardDocument = nil
            let doc = basesDockDocument ?? BaseDocument(source: .file(path: path))
            basesDockDocument = doc
            if !BaseExactIdentity.matches(doc.selectionKey, path) {
                doc.close(session: session)
                doc.retarget(to: .file(path: path), session: nil)
            }
            let isQuarantined = isBatchTrashPathQuarantined(path)
            if doc.handle == nil, !isQuarantined {
                if doc.hasPendingRetargetPreparation {
                    scheduleBaseRetargetPreparationIfNeeded(
                        document: doc,
                        owner: .basesDock,
                        source: .file(path: path),
                        session: session)
                } else {
                    loadBaseDocumentIfAllowed(
                        doc,
                        session: session,
                        thisPath: thisPath)
                }
            } else if !isQuarantined {
                doc.executeActiveView(session: session, thisPath: thisPath)
            }
            membership = BaseRowMembership(rows: doc.result?.rows ?? [])
        case .savedQuery(let id, let name):
            if let dashboardDoc = basesDockDashboardDocument {
                dashboardDoc.close(session: session)
            }
            basesDockDashboardDocument = nil
            let source = BaseDocumentSource.savedQuery(id: id, name: name)
            let doc = basesDockDocument ?? BaseDocument(source: source)
            basesDockDocument = doc
            if !BaseExactIdentity.matches(doc.selectionKey, source.selectionKey) {
                doc.close(session: session)
                doc.retarget(to: source, session: nil)
            }
            if doc.handle == nil {
                if doc.hasPendingRetargetPreparation {
                    scheduleBaseRetargetPreparationIfNeeded(
                        document: doc,
                        owner: .basesDock,
                        source: source,
                        session: session)
                } else {
                    loadBaseDocumentIfAllowed(
                        doc,
                        session: session,
                        thisPath: thisPath)
                }
            } else {
                doc.executeActiveView(session: session, thisPath: thisPath)
            }
            membership = BaseRowMembership(rows: doc.result?.rows ?? [])
        case .dashboard(let id, let name):
            if let baseDoc = basesDockDocument {
                baseDoc.close(session: session)
            }
            basesDockDocument = nil
            if let existing = basesDockDashboardDocument, existing.id != id {
                existing.close(session: session)
                basesDockDashboardDocument = nil
            }
            let doc = basesDockDashboardDocument ?? DashboardDocument(id: id, name: name)
            basesDockDashboardDocument = doc
            if doc.dashboard == nil {
                doc.load(session: session, thisPath: thisPath)
            } else {
                doc.refresh(session: session, thisPath: thisPath)
            }
            membership = doc.membershipSignature
        }
        basesDock.setTarget(target)
        if basesDock.publishMembership(membership) {
            postBaseActionAnnouncement("Base dock updated for active note.")
        }
    }

    private var basesDockActiveNotePath: String? {
        if case .markdown(let path) = workspace.activeTab?.item {
            return path
        }
        guard let selectedFilePath,
            selectedFilePath.lowercased().hasSuffix(".md")
        else { return nil }
        return selectedFilePath
    }

    /// The single N3-07 post-write funnel. UI snapshots stay visible while a
    /// two-wide detached scheduler prepares replacement native handles. Only
    /// the latest generation in the exact committing vault may publish.
    @discardableResult
    func refreshVisibleBasesAfterInAppWrite(
        session: VaultSession,
        changedPath: String,
        alreadyRefreshedDefinitionOwner: BaseDocument? = nil
    ) -> Task<[String], Never>? {
        guard currentSession === session else { return nil }

        visibleBasesRefreshGeneration &+= 1
        let generation = visibleBasesRefreshGeneration
        let selectedPath = activeBaseSelectionPath
        let selectedIdentity = activeBaseSelectedRow.map {
            BaseRowMembership.Identity(path: $0.filePath, taskOrdinal: $0.taskOrdinal)
        }
        let selectedColumnID = activeBaseSelectedColumn?.id
        let changedBasePath = changedPath.lowercased().hasSuffix(".base")
            ? changedPath
            : nil
        let observer = baseRetargetNativeExecutionObserverForTesting
        let runner = baseRetargetPreloadRunner
        let preparationLimiter = nativeDocumentPreparationLimiter

        var nextWorkIndex = 0
        var basePlans: [VisibleBaseRefreshPlan] = []
        var dashboardPlans: [VisibleDashboardSectionRefreshPlan] = []
        var embedPlans: [VisibleEmbedRefreshPlan] = []
        var nativeWork: [VisibleBasesNativeWork] = []

        func reserveBase(
            owner: VisibleBaseRefreshOwner,
            document: BaseDocument,
            thisPath: String?
        ) {
            if let path = document.source.filePath,
                isBatchTrashPathQuarantined(path)
            {
                return
            }
            let index = nextWorkIndex
            nextWorkIndex += 1
            let reservation = document.beginContentRefresh(thisPath: thisPath)
            basePlans.append(
                VisibleBaseRefreshPlan(
                    workIndex: index,
                    owner: owner,
                    document: document,
                    reservation: reservation,
                    previousMembership: BaseRowMembership(rows: document.result?.rows ?? [])))
            nativeWork.append(.base(index, reservation))
        }

        func reserveDashboard(
            owner: VisibleDashboardRefreshOwner,
            dashboard: DashboardDocument,
            thisPath: String?
        ) {
            for section in dashboard.sections {
                guard let reservation = section.beginContentRefresh(thisPath: thisPath) else {
                    continue
                }
                let index = nextWorkIndex
                nextWorkIndex += 1
                dashboardPlans.append(
                    VisibleDashboardSectionRefreshPlan(
                        workIndex: index,
                        owner: owner,
                        dashboard: dashboard,
                        section: section,
                        reservation: reservation,
                        previousMembership: section.membership))
                nativeWork.append(.dashboard(index, reservation))
            }
        }

        for (key, document) in baseDocuments.sorted(by: { $0.key < $1.key }) {
            guard baseDocuments[key] === document else { continue }
            if let changedBasePath,
                document === alreadyRefreshedDefinitionOwner,
                document.source.filePath.map({
                    BaseExactIdentity.matches($0, changedBasePath)
                }) == true
            {
                continue
            }
            reserveBase(owner: .registry(key), document: document, thisPath: nil)
        }

        for (id, dashboard) in dashboardDocuments.sorted(by: { $0.key < $1.key }) {
            guard dashboardDocuments[id] === dashboard else { continue }
            reserveDashboard(owner: .registry(id), dashboard: dashboard, thisPath: nil)
        }

        let registeredHandles = baseEmbedHandles.sorted { lhs, rhs in
            BaseExactIdentity.lessThan(lhs.key.exactIdentityKey, rhs.key.exactIdentityKey)
        }
        for (key, handle) in registeredHandles {
            guard baseEmbedHandles[key] === handle,
                handle.session === session,
                handle.request.targetPath.map({
                    !isBatchTrashPathQuarantined($0)
                }) ?? true,
                let reservation = handle.beginContentRefresh()
            else { continue }
            let index = nextWorkIndex
            nextWorkIndex += 1
            let liveByID = Dictionary(
                uniqueKeysWithValues: handle.liveDocuments.map {
                    (ObjectIdentifier($0), $0)
                })
            let previousMemberships = Dictionary(
                uniqueKeysWithValues: reservation.documents.map { snapshot in
                    (
                        snapshot.id,
                        BaseRowMembership(rows: liveByID[snapshot.id]?.result?.rows ?? [])
                    )
                })
            embedPlans.append(
                VisibleEmbedRefreshPlan(
                    workIndex: index,
                    key: key,
                    handle: handle,
                    reservation: reservation,
                    previousMemberships: previousMemberships))
            nativeWork.append(.embed(index, reservation))
        }

        if let target = basesDock.target {
            switch target {
            case .base(let path, _):
                if let document = basesDockDocument,
                    document.source.filePath.map({
                        BaseExactIdentity.matches($0, path)
                    }) == true
                {
                    reserveBase(
                        owner: .dock(target),
                        document: document,
                        thisPath: basesDock.thisPath)
                }
            case .savedQuery(let id, let name):
                let source = BaseDocumentSource.savedQuery(id: id, name: name)
                if let document = basesDockDocument,
                    BaseExactIdentity.matches(document.selectionKey, source.selectionKey)
                {
                    reserveBase(
                        owner: .dock(target),
                        document: document,
                        thisPath: basesDock.thisPath)
                }
            case .dashboard(let id, _):
                if let dashboard = basesDockDashboardDocument, dashboard.id == id {
                    reserveDashboard(
                        owner: .dock(target),
                        dashboard: dashboard,
                        thisPath: basesDock.thisPath)
                }
            }
        }

        let basePlansByIndex = Dictionary(
            uniqueKeysWithValues: basePlans.map { ($0.workIndex, $0) })
        let dashboardPlansByIndex = Dictionary(
            uniqueKeysWithValues: dashboardPlans.map { ($0.workIndex, $0) })
        let embedPlansByIndex = Dictionary(
            uniqueKeysWithValues: embedPlans.map { ($0.workIndex, $0) })

        let task: Task<[String], Never> = Task { @MainActor [weak self] in
            let results = await Task.detached(priority: .userInitiated) {
                var preparedResults: [VisibleBasesNativeResult] = []
                var offset = 0
                while offset < nativeWork.count {
                    let end = min(offset + 2, nativeWork.count)
                    let chunk = Array(nativeWork[offset..<end])
                    let chunkResults = await withTaskGroup(
                        of: VisibleBasesNativeResult.self,
                        returning: [VisibleBasesNativeResult].self
                    ) { group in
                        for work in chunk {
                            group.addTask {
                                await preparationLimiter.acquire()
                                let result = await Task.detached(
                                    priority: .userInitiated
                                ) { () -> VisibleBasesNativeResult in
                                    switch work {
                                    case .base(let index, let reservation):
                                        return .base(
                                            index,
                                            runner(session, reservation.request, observer))
                                    case .dashboard(let index, let reservation):
                                        return .dashboard(
                                            index,
                                            runner(session, reservation.request, observer))
                                    case .embed(let index, let reservation):
                                        return .embed(
                                            index,
                                            BaseEmbedPreparedLoader.prepare(
                                                session: session,
                                                reservation: reservation,
                                                observer: observer))
                                    }
                                }.value
                                await preparationLimiter.release()
                                return result
                            }
                        }
                        var results: [VisibleBasesNativeResult] = []
                        for await result in group { results.append(result) }
                        return results
                    }
                    preparedResults.append(contentsOf: chunkResults)
                    offset = end
                }
                return preparedResults.sorted { $0.index < $1.index }
            }.value

            guard !Task.isCancelled,
                let self,
                self.currentSession === session,
                self.visibleBasesRefreshGeneration == generation
            else {
                await Task.detached(priority: .utility) {
                    for result in results {
                        switch result {
                        case .base(_, let prepared), .dashboard(_, let prepared):
                            BasePreparedLoader.release(
                                prepared, session: session, observer: observer)
                        case .embed(_, let prepared):
                            BaseEmbedPreparedLoader.release(
                                prepared, session: session, observer: observer)
                        }
                    }
                }.value
                return [String]()
            }

            var settledIndices: Set<Int> = []
            var resultsToRelease: [VisibleBasesNativeResult] = []
            var replacedBaseHandlesToClose: [UInt64] = []
            var replacedEmbedHandlesToClose: [UInt64] = []

            for result in results {
                switch result {
                case .base(let index, let prepared):
                    guard let plan = basePlansByIndex[index] else {
                        resultsToRelease.append(result)
                        continue
                    }
                    let ownsOwner: Bool
                    switch plan.owner {
                    case .registry(let key):
                        ownsOwner = self.baseDocuments[key] === plan.document
                    case .dock(let target):
                        ownsOwner = self.basesDock.target == target
                            && self.basesDockDocument === plan.document
                    }
                    guard ownsOwner else {
                        resultsToRelease.append(result)
                        continue
                    }
                    switch plan.document.applyContentRefresh(
                        prepared, reservation: plan.reservation)
                    {
                    case .applied(let replacedHandle):
                        if let replacedHandle {
                            replacedBaseHandlesToClose.append(replacedHandle)
                        }
                        settledIndices.insert(index)
                    case .failed:
                        settledIndices.insert(index)
                    case .stale:
                        resultsToRelease.append(result)
                    }

                case .dashboard(let index, let prepared):
                    guard let plan = dashboardPlansByIndex[index] else {
                        resultsToRelease.append(result)
                        continue
                    }
                    let ownsOwner: Bool
                    switch plan.owner {
                    case .registry(let id):
                        ownsOwner = self.dashboardDocuments[id] === plan.dashboard
                    case .dock(let target):
                        ownsOwner = self.basesDock.target == target
                            && self.basesDockDashboardDocument === plan.dashboard
                    }
                    guard ownsOwner,
                        plan.dashboard.sections.contains(where: { $0 === plan.section })
                    else {
                        resultsToRelease.append(result)
                        continue
                    }
                    switch plan.section.applyContentRefresh(
                        prepared, reservation: plan.reservation)
                    {
                    case .applied(let replacedHandle):
                        if let replacedHandle {
                            replacedBaseHandlesToClose.append(replacedHandle)
                        }
                        settledIndices.insert(index)
                    case .failed:
                        settledIndices.insert(index)
                    case .stale:
                        resultsToRelease.append(result)
                    }

                case .embed(let index, let prepared):
                    guard let plan = embedPlansByIndex[index],
                        self.baseEmbedHandles[plan.key] === plan.handle
                    else {
                        resultsToRelease.append(result)
                        continue
                    }
                    switch plan.handle.applyContentRefresh(
                        prepared,
                        reservation: plan.reservation,
                        session: session)
                    {
                    case .applied(let replacedHandle):
                        replacedEmbedHandlesToClose.append(replacedHandle)
                        settledIndices.insert(index)
                    case .failed:
                        settledIndices.insert(index)
                    case .stale:
                        resultsToRelease.append(result)
                    }
                }
            }

            await Task.detached(priority: .utility) {
                for result in resultsToRelease {
                    switch result {
                    case .base(_, let prepared), .dashboard(_, let prepared):
                        BasePreparedLoader.release(
                            prepared, session: session, observer: observer)
                    case .embed(_, let prepared):
                        BaseEmbedPreparedLoader.release(
                            prepared, session: session, observer: observer)
                    }
                }
                for handle in replacedBaseHandlesToClose {
                    BasePreparedLoader.closeReplaced(
                        handle: handle, session: session, observer: observer)
                }
                for handle in replacedEmbedHandlesToClose {
                    BaseEmbedPreparedLoader.closeReplaced(
                        handle: handle, session: session, observer: observer)
                }
            }.value

            guard !Task.isCancelled,
                self.currentSession === session,
                self.visibleBasesRefreshGeneration == generation
            else { return [String]() }

            var announcements: [String] = []
            func append(_ message: String) {
                guard !announcements.contains(message) else { return }
                announcements.append(message)
            }
            func appendMembershipChange(
                previous: BaseRowMembership,
                result: BasesResultSet?
            ) {
                let current = BaseRowMembership(rows: result?.rows ?? [])
                guard previous != current, let summary = result?.audioSummary else { return }
                append("Updated: \(summary)")
            }

            for plan in basePlans where settledIndices.contains(plan.workIndex) {
                appendMembershipChange(
                    previous: plan.previousMembership,
                    result: plan.document.result)
                if case .dock(let target) = plan.owner,
                    self.basesDock.target == target,
                    self.basesDockDocument === plan.document
                {
                    self.basesDock.rebaseMembership(
                        BaseRowMembership(rows: plan.document.result?.rows ?? []))
                }
            }
            for plan in dashboardPlans where settledIndices.contains(plan.workIndex) {
                appendMembershipChange(
                    previous: plan.previousMembership,
                    result: plan.section.result)
            }
            for plan in embedPlans where settledIndices.contains(plan.workIndex) {
                let liveByID = Dictionary(
                    uniqueKeysWithValues: plan.handle.liveDocuments.map {
                        (ObjectIdentifier($0), $0)
                    })
                for snapshot in plan.reservation.documents {
                    guard let document = liveByID[snapshot.id] else { continue }
                    appendMembershipChange(
                        previous: plan.previousMemberships[snapshot.id] ?? .empty,
                        result: document.result)
                }
            }
            let dockDashboardPlans = dashboardPlans.filter {
                if case .dock = $0.owner { return true }
                return false
            }
            if !dockDashboardPlans.isEmpty,
                dockDashboardPlans.allSatisfy({ settledIndices.contains($0.workIndex) }),
                let plan = dockDashboardPlans.first,
                case .dock(let target) = plan.owner,
                    self.basesDock.target == target,
                    self.basesDockDashboardDocument === plan.dashboard
            {
                self.basesDock.rebaseMembership(plan.dashboard.membershipSignature)
            }

            let currentIdentity = self.activeBaseSelectedRow.map {
                BaseRowMembership.Identity(path: $0.filePath, taskOrdinal: $0.taskOrdinal)
            }
            if BaseExactIdentity.matches(self.activeBaseSelectionPath, selectedPath),
                currentIdentity == selectedIdentity,
                BaseExactIdentity.matches(self.activeBaseSelectedColumn?.id, selectedColumnID)
            {
                self.restoreActiveBaseSelection(
                    path: selectedPath,
                    identity: selectedIdentity,
                    columnID: selectedColumnID)
            }
            self.lastBaseRefreshAnnouncements = announcements
            announcements.forEach(self.postBaseActionAnnouncement)
            return announcements
        }
        visibleBasesRefreshTask = task
        return task
    }

    private func restoreActiveBaseSelection(
        path: String?,
        identity: BaseRowMembership.Identity?,
        columnID: String?
    ) {
        guard let path, let identity,
            let document = baseDocuments.values.first(where: {
                BaseExactIdentity.matches($0.selectionKey, path)
            }),
            let result = document.result,
            let row = result.rows.first(where: {
                BaseExactIdentity.matches($0.filePath, identity.path)
                    && $0.taskOrdinal == identity.taskOrdinal
            })
        else {
            if path != nil { clearActiveBaseSelection() }
            return
        }
        activeBaseSelectionPath = path
        activeBaseSelectedRow = row
        activeBaseSelectedColumn = columnID.flatMap { id in
            result.columns.first { BaseExactIdentity.matches($0.id, id) }
        }
    }

    private func reloadDashboardDocumentsAfterSavedQueryChange() {
        guard let session = currentSession else { return }
        for doc in dashboardDocuments.values {
            doc.load(session: session)
        }
        if let docked = basesDockDashboardDocument {
            docked.load(session: session, thisPath: basesDockActiveNotePath)
            basesDock.rebaseMembership(docked.membershipSignature)
        }
    }

    private func setActiveBaseRendererOverride(_ mode: BaseRendererMode) {
        guard let tab = workspace.activeTab, BaseDocumentSource(item: tab.item) != nil else { return }
        baseRendererOverrides[tab.id] = mode
        postAccessibilityAnnouncement(.baseViewMode(mode: mode.rawValue))
    }

    private func clearBaseRendererOverride(for tabID: TabID) {
        baseRendererOverrides[tabID] = nil
    }

    func basesOpenViewSwitcher() {
        guard let doc = activeBaseDocument else { return }
        postAccessibilityAnnouncement(.baseViewSwitcher(viewCount: UInt32(doc.views.count)))
    }

    func basesNewQuery() {
        guard currentSession != nil else { return }
        activeBaseQueryBuilder = BaseQueryBuilderModel()
        postAccessibilityAnnouncement(.basesNewQueryBuilder)
    }

    func basesEditViewFilters() {
        guard let doc = activeBaseDocument, let session = currentSession else { return }
        if case .savedQuery(let id, _) = doc.source {
            editSavedQueryInBuilder(id: id)
            return
        }
        if let reason = baseDocumentInteractionDisabledReason(for: doc) {
            postMutationAnnouncement(reason)
            return
        }
        guard let handle = doc.handle else { return }
        do {
            let effectiveQueryJSON = try session.baseViewQueryJson(
                handle: handle,
                view: UInt32(doc.activeViewIndex))
            let localQueryJSON = try session.baseViewEditQueryJson(
                handle: handle,
                view: UInt32(doc.activeViewIndex))
            activeBaseQueryBuilder = try BaseQueryBuilderModel(
                draft: BaseQueryBuilderDraft(
                    effectiveQueryJSON: effectiveQueryJSON,
                    localQueryJSON: localQueryJSON),
                editingBaseView: EditingBaseView(
                    source: doc.source,
                    viewIndex: UInt32(doc.activeViewIndex)))
            let viewName = doc.activeViewName ?? "active view"
            postAccessibilityAnnouncement(.basesEditingFilters(viewName: viewName))
        } catch {
            postAccessibilityAnnouncement(
                .basesFiltersOpenFailed(detail: error.localizedDescription))
        }
    }

    func basesCloseQueryBuilder() {
        activeBaseQueryBuilder = nil
    }

    func clearBaseQueryBuilderSaveError() {
        baseQueryBuilderSaveError = nil
    }

    func basesBuilderSchedulePreview(delayNanoseconds: UInt64 = 300_000_000) {
        baseQueryBuilderPreviewGeneration += 1
        let generation = baseQueryBuilderPreviewGeneration
        baseQueryBuilderPreviewTask?.cancel()
        baseQueryBuilderPreviewCancelToken?.cancel()
        baseQueryBuilderPreviewCancelToken = nil
        guard let model = activeBaseQueryBuilder, let session = currentSession else { return }
        let queryJSON: String
        do {
            queryJSON = try model.draft.queryJSON()
            model.previewState = .loading
        } catch {
            model.previewState = .failed(error.localizedDescription)
            return
        }
        let cancelToken = CancelToken()
        baseQueryBuilderPreviewCancelToken = cancelToken
        let executionObserver = baseQueryBuilderPreviewExecutionObserver
        let previewThisPath = model.previewThisPath
        baseQueryBuilderPreviewTask = Task {
            @MainActor [
                weak self, weak model, session, queryJSON, cancelToken,
                executionObserver, previewThisPath,
            ] in
            do {
                if delayNanoseconds > 0 {
                    try await Task.sleep(nanoseconds: delayNanoseconds)
                }
                if Task.isCancelled {
                    cancelToken.cancel()
                    return
                }
            } catch is CancellationError {
                cancelToken.cancel()
                return
            } catch {
                cancelToken.cancel()
                return
            }

            let nativeTask = Task.detached(priority: .userInitiated) {
                () -> BaseQueryBuilderPreviewExecutionOutcome in
                if Task.isCancelled || cancelToken.isCancelled() { return .cancelled }

                let handle: UInt64
                do {
                    let ranOnMainThread = BaseQueryBuilderPreviewThreadProbe.isMainThread()
                    handle = try session.openQuery(
                        queryJson: queryJSON, thisPath: previewThisPath)
                    await executionObserver?(
                        BaseQueryBuilderPreviewExecutionEvent(
                            phase: .opened,
                            generation: generation,
                            handle: handle,
                            ranOnMainThread: ranOnMainThread))
                } catch {
                    if Task.isCancelled || cancelToken.isCancelled() { return .cancelled }
                    return .failure(error.localizedDescription)
                }

                let outcome: BaseQueryBuilderPreviewExecutionOutcome
                if Task.isCancelled || cancelToken.isCancelled() {
                    outcome = .cancelled
                } else {
                    let ranOnMainThread = BaseQueryBuilderPreviewThreadProbe.isMainThread()
                    do {
                        let result = try session.baseExecute(
                            handle: handle,
                            view: 0,
                            thisPath: previewThisPath,
                            quickFilter: nil,
                            cancel: cancelToken)
                        await executionObserver?(
                            BaseQueryBuilderPreviewExecutionEvent(
                                phase: .executed,
                                generation: generation,
                                handle: handle,
                                ranOnMainThread: ranOnMainThread))
                        outcome = Task.isCancelled || cancelToken.isCancelled()
                            ? .cancelled : .success(result)
                    } catch {
                        await executionObserver?(
                            BaseQueryBuilderPreviewExecutionEvent(
                                phase: .executed,
                                generation: generation,
                                handle: handle,
                                ranOnMainThread: ranOnMainThread))
                        outcome = Task.isCancelled || cancelToken.isCancelled()
                            ? .cancelled : .failure(error.localizedDescription)
                    }
                }

                let ranOnMainThread = BaseQueryBuilderPreviewThreadProbe.isMainThread()
                session.closeBase(handle: handle)
                await executionObserver?(
                    BaseQueryBuilderPreviewExecutionEvent(
                        phase: .closed,
                        generation: generation,
                        handle: handle,
                        ranOnMainThread: ranOnMainThread))
                return outcome
            }
            let outcome = await withTaskCancellationHandler {
                await nativeTask.value
            } onCancel: {
                cancelToken.cancel()
                nativeTask.cancel()
            }
            if Task.isCancelled {
                cancelToken.cancel()
                return
            }
            switch outcome {
            case .success(let result):
                self?.basesBuilderPublishPreview(
                    result: result,
                    for: model,
                    session: session,
                    cancelToken: cancelToken,
                    generation: generation)
            case .failure(let message):
                self?.basesBuilderPublishPreviewFailure(
                    message: message,
                    for: model,
                    session: session,
                    cancelToken: cancelToken,
                    generation: generation)
            case .cancelled:
                break
            }
            self?.basesBuilderClearPreviewToken(
                for: model,
                session: session,
                cancelToken: cancelToken,
                generation: generation)
        }
    }

    func basesBuilderPublishPreview(
        result: BasesResultSet,
        for model: BaseQueryBuilderModel?,
        session: VaultSession,
        cancelToken: CancelToken,
        generation: Int
    ) {
        guard let model = currentBaseQueryBuilderPreviewModel(
            model,
            session: session,
            cancelToken: cancelToken,
            generation: generation)
        else { return }
        if let message = result.viewError,
            !message.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
        {
            model.previewState = .failed(message)
        } else {
            model.previewState = .ready(result)
        }
        // W0.5-3 residue: BaseQueryPreviewState.accessibilityAnnouncement
        postAccessibilityAnnouncement(
            .hostComposed(
                text: model.previewState.accessibilityAnnouncement,
                priority: .medium))
    }

    func basesBuilderPublishPreviewFailure(
        message: String,
        for model: BaseQueryBuilderModel?,
        session: VaultSession,
        cancelToken: CancelToken,
        generation: Int
    ) {
        guard let model = currentBaseQueryBuilderPreviewModel(
            model,
            session: session,
            cancelToken: cancelToken,
            generation: generation)
        else { return }
        model.previewState = .failed(message)
        postAccessibilityAnnouncement(.basesPreviewFailed(detail: message))
    }

    private func currentBaseQueryBuilderPreviewModel(
        _ model: BaseQueryBuilderModel?,
        session: VaultSession,
        cancelToken: CancelToken,
        generation: Int
    ) -> BaseQueryBuilderModel? {
        guard let model,
            currentSession === session,
            activeBaseQueryBuilder === model,
            let currentToken = baseQueryBuilderPreviewCancelToken,
            currentToken === cancelToken,
            baseQueryBuilderPreviewGeneration == generation,
            !cancelToken.isCancelled()
        else { return nil }
        return model
    }

    private func basesBuilderClearPreviewToken(
        for model: BaseQueryBuilderModel?,
        session: VaultSession,
        cancelToken: CancelToken,
        generation: Int
    ) {
        guard currentBaseQueryBuilderPreviewModel(
            model,
            session: session,
            cancelToken: cancelToken,
            generation: generation) != nil
        else { return }
        baseQueryBuilderPreviewCancelToken = nil
    }

    /// Only the in-place write targets the builder's source Base. The draft,
    /// preview, saved-query route, and Save as .base remain usable recovery
    /// tools while that source path has an outcome-unknown Trash state.
    var baseQueryBuilderSaveToViewDisabledReason: String? {
        guard let editingView = activeBaseQueryBuilder?.editingBaseView else {
            return nil
        }
        if let path = editingView.source.filePath {
            switch batchTrashPathCapability(for: path) {
            case .writable:
                break
            case .readOnly(let reason), .invalid(let reason):
                return reason
            }
        }
        guard let document = baseDocuments[editingView.source.key] else {
            return Self.baseQueryBuilderSourceUnavailableReason
        }
        if document.handle != nil { return nil }
        if document.hasPendingRetargetPreparation {
            if case .degraded = document.state {
                return Self.baseDocumentRetargetFailedDisabledReason
            }
            return Self.baseDocumentReopeningDisabledReason
        }
        switch document.state {
        case .loading:
            return Self.baseDocumentOpeningDisabledReason
        case .failed, .ready, .degraded:
            return Self.baseQueryBuilderSourceUnavailableReason
        }
    }

    func basesBuilderSaveToView() {
        guard let model = activeBaseQueryBuilder,
            let editingView = model.editingBaseView,
            let session = currentSession
        else { return }
        if let reason = baseQueryBuilderSaveToViewDisabledReason {
            postMutationAnnouncement(reason)
            return
        }
        if let path = editingView.source.filePath,
            !admitBatchTrashWrite(to: [path])
        {
            return
        }
        guard let doc = baseDocuments[editingView.source.key],
            let handle = doc.handle
        else {
            postMutationAnnouncement(Self.baseQueryBuilderSourceUnavailableReason)
            return
        }
        do {
            let edits = try model.baseEditsForView(editingView.viewIndex)
            try session.baseApplyEdits(handle: handle, edits: edits)
            model.rebaseAfterSuccessfulSave()
            refreshVisibleBasesAfterInAppWrite(
                session: session,
                changedPath: editingView.source.filePath ?? doc.selectionKey)
            postAccessibilityAnnouncement(.basesBuilderSaved)
        } catch {
            postAccessibilityAnnouncement(
                .basesViewSaveFailed(detail: error.localizedDescription))
        }
    }

    var baseQueryBuilderRecoveryActionLabel: String? {
        guard let editingView = activeBaseQueryBuilder?.editingBaseView else {
            return nil
        }
        guard let document = baseDocuments[editingView.source.key] else { return nil }
        return baseRecoveryActionLabel(for: document)
    }

    var baseQueryBuilderRecoveryActionHint: String? {
        guard let editingView = activeBaseQueryBuilder?.editingBaseView else {
            return nil
        }
        guard let document = baseDocuments[editingView.source.key] else { return nil }
        return baseRecoveryActionHint(for: document)
    }

    @discardableResult
    func retryBaseQueryBuilderSourceRecovery() -> Task<Void, Never>? {
        guard let editingView = activeBaseQueryBuilder?.editingBaseView else {
            return nil
        }
        guard let document = baseDocuments[editingView.source.key] else { return nil }
        return retryBaseRecovery(for: document)
    }

    @discardableResult
    func basesBuilderSaveAsBase(
        path: String,
        nativeThreadObserver: (@Sendable (Bool) -> Void)? = nil
    ) -> Task<Void, Never>? {
        guard let model = activeBaseQueryBuilder, let session = currentSession else { return nil }
        clearBaseQueryBuilderSaveError()
        let normalizedPath: String
        switch Self.normalizeBuilderBaseDestination(path) {
        case .accepted(let path):
            normalizedPath = path
        case .rejected(let message):
            baseQueryBuilderSaveError = message
            postMutationAnnouncement(message)
            return nil
        }
        guard !normalizedPath.isEmpty else {
            let message = "Enter a .base path before saving."
            baseQueryBuilderSaveError = message
            postMutationAnnouncement(message)
            return nil
        }
        guard admitStructuralMutationRequest() else { return nil }
        guard let recoveryReservation =
            admitStructuralRecoveryDestination(normalizedPath)
        else {
            baseQueryBuilderSaveError = Self.structuralRecoveryDestinationReason
            return nil
        }
        guard admitBatchTrashWrite(to: [normalizedPath]) else {
            baseQueryBuilderSaveError = lastMutationAnnouncement
            return nil
        }

        let queryJSON: String
        do {
            queryJSON = try model.draft.queryJSON()
        } catch {
            let message = "Base file could not be saved: \(error.localizedDescription)"
            baseQueryBuilderSaveError = message
            postMutationAnnouncement(message)
            return nil
        }
        let token = beginStructuralMutation(
            recoveryReservation: recoveryReservation)
        let refresher = structuralBatchRefreshRunner
        let task = Task { @MainActor [weak self] in
            let outcome = await Task.detached(priority: .userInitiated) {
                do {
                    nativeThreadObserver?(BaseWriterThreadProbe.isMainThread())
                    try session.saveQueryAsBase(queryJson: queryJSON, path: normalizedPath)
                    return BaseBuilderWriteOutcome.success
                } catch VaultError.DestinationExists(let existingPath) {
                    return BaseBuilderWriteOutcome.destinationExists(existingPath)
                } catch {
                    return BaseBuilderWriteOutcome.failure(error.localizedDescription)
                }
            }.value

            guard let self else { return }
            defer { self.endStructuralMutation(token) }
            guard self.ownsStructuralMutation(token, session: session) else { return }

            switch outcome {
            case .success:
                await refresher(self)
                guard self.ownsStructuralMutation(token, session: session) else { return }
                // The core call is exclusive-create. Reaching this branch is
                // authoritative proof that a new vault path was committed.
                self.barrierStructuralUndoForCreatedVaultPath(
                    relativePath: normalizedPath,
                    existedBefore: false)
                await self.refreshBaseQueries()?.value
                guard self.ownsStructuralMutation(token, session: session) else { return }
                _ = await self.refreshVisibleBasesAfterInAppWrite(
                    session: session, changedPath: normalizedPath)?.value
                guard self.ownsStructuralMutation(token, session: session) else { return }
                if self.activeBaseQueryBuilder === model {
                    self.baseQueryBuilderSaveError = nil
                }
                self.postMutationAnnouncement("Saved query as \(normalizedPath).")
            case .destinationExists(let existingPath):
                let message =
                    "A file already exists at \(existingPath). Choose a different Base path."
                if self.activeBaseQueryBuilder === model {
                    self.baseQueryBuilderSaveError = message
                }
                self.postMutationAnnouncement(message)
            case .failure(let message):
                let message = "Base file could not be saved: \(message)"
                if self.activeBaseQueryBuilder === model {
                    self.baseQueryBuilderSaveError = message
                }
                self.postMutationAnnouncement(message)
            }
        }
        recordPendingStructuralTask(task)
        return task
    }

    private static func normalizeBuilderBaseDestination(
        _ proposedPath: String
    ) -> BaseBuilderDestination {
        let trimmed = proposedPath.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else {
            return .rejected("Enter a .base path before saving.")
        }

        let finalComponent = (trimmed as NSString).lastPathComponent
        let pathExtension = (finalComponent as NSString).pathExtension
        if pathExtension.isEmpty {
            guard !finalComponent.hasSuffix(".") else {
                return .rejected("Base paths must end in .base.")
            }
            return .accepted(trimmed + ".base")
        }
        guard pathExtension.caseInsensitiveCompare("base") == .orderedSame else {
            return .rejected("Base paths must end in .base.")
        }
        return .accepted(trimmed)
    }

    func basesBuilderSaveAsSavedQuery(name: String, description: String?) {
        guard let model = activeBaseQueryBuilder, let session = currentSession else { return }
        let trimmed = name.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else {
            postAccessibilityAnnouncement(.basesSavedQueryNameNeeded)
            return
        }
        do {
            _ = try session.saveQuery(
                name: trimmed,
                description: description?.trimmingCharacters(in: .whitespacesAndNewlines),
                queryJson: model.draft.queryJSON(),
                sourceSyntax: .builder)
            refreshBaseQueries()
            postAccessibilityAnnouncement(.basesSavedQueryCreated(name: trimmed))
        } catch {
            postAccessibilityAnnouncement(
                .basesSavedQueryCreateFailed(detail: error.localizedDescription))
        }
    }

    @discardableResult
    func basesBuilderUpdateSavedQuery() -> Task<Void, Never>? {
        guard let model = activeBaseQueryBuilder,
            let editingSavedQuery = model.editingSavedQuery,
            let session = currentSession
        else { return nil }

        let id = editingSavedQuery.id
        let name = editingSavedQuery.name
        let description = editingSavedQuery.description?
            .trimmingCharacters(in: .whitespacesAndNewlines)
        let queryJSON: String
        do {
            queryJSON = try model.draft.queryJSON()
        } catch {
            postAccessibilityAnnouncement(
                .basesSavedQueryUpdateFailed(detail: error.localizedDescription))
            return nil
        }
        savedQueryUpdateGeneration &+= 1
        let generation = savedQueryUpdateGeneration
        let predecessor = savedQueryUpdateTask
        let task = Task { @MainActor [weak self] in
            await predecessor?.value
            guard !Task.isCancelled,
                let self,
                self.currentSession === session,
                self.savedQueryUpdateGeneration == generation
            else { return }

            let outcome: SavedQueryUpdateOutcome = await Task.detached(
                priority: .userInitiated
            ) {
                do {
                    try session.updateSavedQuery(
                        id: id,
                        description: description,
                        queryJson: queryJSON,
                        sourceSyntax: .builder)
                    return .success
                } catch {
                    return .failure(error.localizedDescription)
                }
            }.value

            guard !Task.isCancelled,
                self.currentSession === session,
                self.savedQueryUpdateGeneration == generation
            else { return }

            switch outcome {
            case .success:
                await self.refreshBaseQueries()?.value
                guard !Task.isCancelled,
                    self.currentSession === session,
                    self.savedQueryUpdateGeneration == generation
                else { return }
                _ = await self.refreshVisibleBasesAfterInAppWrite(
                    session: session,
                    changedPath: "")?.value
                guard !Task.isCancelled,
                    self.currentSession === session,
                    self.savedQueryUpdateGeneration == generation
                else { return }
                postAccessibilityAnnouncement(.basesSavedQueryUpdated(name: name))
            case .failure(let message):
                postAccessibilityAnnouncement(
                    .basesSavedQueryUpdateFailed(detail: message))
            }
        }
        savedQueryUpdateTask = task
        return task
    }

    /// Reopen every registry entry resolved to `id`, using stable saved-query
    /// identity for both name- and ID-authored embeds. On deletion, reopening
    /// intentionally fails through `BaseEmbedDocument`'s normal unknown-query
    /// surface after `BaseEmbedHandle.reload` has closed the old native handle.
    /// Updates retain the prior optimization of skipping handles with no live,
    /// already-loaded document.
    private func reloadRegisteredSavedQueryEmbeds(
        id: String,
        session: VaultSession,
        includeUnleasedHandles: Bool
    ) {
        guard currentSession === session else { return }
        let registeredEmbeds = baseEmbedHandles.sorted { lhs, rhs in
            BaseExactIdentity.lessThan(lhs.key.exactIdentityKey, rhs.key.exactIdentityKey)
        }
        for (key, handle) in registeredEmbeds {
            guard currentSession === session,
                baseEmbedHandles[key] === handle,
                handle.session === session,
                handle.resolvedSavedQueryID == id
            else { continue }
            let documents = handle.liveDocuments.filter { !$0.needsInitialLoad }
            guard includeUnleasedHandles || !documents.isEmpty else { continue }
            do {
                try handle.reload(session: session)
                documents.forEach { $0.refreshAfterSharedHandleReload(session: session) }
            } catch {
                documents.forEach { $0.failRefresh(error) }
            }
        }
    }

    func basesBuilderAddCondition() {
        activeBaseQueryBuilder?.perform(.addCondition)
        basesBuilderSchedulePreview()
    }

    func basesBuilderAddGroup() {
        activeBaseQueryBuilder?.perform(.addGroup)
        basesBuilderSchedulePreview()
    }

    func basesBuilderEditCondition() {
        guard let model = activeBaseQueryBuilder,
            let index = model.selectedRowIndex ?? model.editingRowIndex ?? model.rows.indices.first
        else { return }
        model.perform(.editCondition(index: index))
    }

    func basesBuilderRemoveCondition() {
        guard let model = activeBaseQueryBuilder,
            let index = model.selectedRowIndex ?? model.editingRowIndex ?? model.rows.indices.last
        else { return }
        model.perform(.removeCondition(index: index))
    }

    func basesLoadPropertyKeys() async -> [PropertyKeySummary] {
        guard let session = currentSession else { return [] }
        return await Task.detached(priority: .userInitiated) {
            (try? session.listPropertyKeys()) ?? []
        }.value
    }

    func basesLoadTags() async -> [String] {
        guard let session = currentSession else { return [] }
        return await Task.detached(priority: .userInitiated) {
            (try? session.listTags()) ?? []
        }.value
    }

    func basesLoadNotePaths() async -> [String] {
        guard let session = currentSession else { return [] }
        return await Task.detached(priority: .userInitiated) {
            var cursor: String?
            var out: [String] = []
            repeat {
                guard
                    let page = try? session.listFiles(
                        filter: .markdownOnly,
                        paging: Paging(cursor: cursor, limit: 5_000))
                else { break }
                out.append(contentsOf: page.items.map(\.path))
                cursor = page.nextCursor
            } while cursor != nil && out.count < 50_000
            return out
        }.value
    }

    func basesSelectNextView() {
        guard let doc = activeBaseDocument,
            admitBaseDocumentInteraction(doc),
            let session = currentSession
        else { return }
        doc.selectNextView(session: session)
        if let name = doc.activeViewName {
            postAccessibilityAnnouncement(.basesViewSelected(name: name))
        }
    }

    func basesSelectPreviousView() {
        guard let doc = activeBaseDocument,
            admitBaseDocumentInteraction(doc),
            let session = currentSession
        else { return }
        doc.selectPreviousView(session: session)
        if let name = doc.activeViewName {
            postAccessibilityAnnouncement(.basesViewSelected(name: name))
        }
    }

    func basesSortByColumn() {
        guard let doc = activeBaseDocument,
            admitBaseDocumentInteraction(doc),
            let session = currentSession,
            let text = doc.sortFocusedColumn(session: session)
        else { return }
        // W0.5-3 residue: BaseDocument.sortFocusedColumn
        postAccessibilityAnnouncement(.hostComposed(text: text, priority: .medium))
    }

    func basesSaveSortToView() {
        guard let doc = activeBaseDocument,
            admitBaseDocumentInteraction(doc),
            let session = currentSession
        else { return }
        if let path = doc.source.filePath,
            !admitBatchTrashWrite(to: [path])
        {
            return
        }
        do {
            if let text = try doc.saveSortToView(session: session) {
                refreshVisibleBasesAfterInAppWrite(
                    session: session,
                    changedPath: doc.source.filePath ?? doc.selectionKey,
                    alreadyRefreshedDefinitionOwner: doc)
                // W0.5-3 residue: BaseDocument.saveSortToView
                postAccessibilityAnnouncement(.hostComposed(text: text, priority: .medium))
            }
        } catch {
            postAccessibilityAnnouncement(
                .basesSortSaveFailed(detail: error.localizedDescription))
        }
    }

    func basesResultsPopover() {
        guard let doc = activeBaseDocument, let result = doc.result else { return }
        let suffix = doc.quickFilterActive ? " \(doc.whereAmIReadback)." : ""
        // W0.5-3 residue: BasesResultSet.audioSummary + whereAmIReadback suffix
        postAccessibilityAnnouncement(
            .hostComposed(text: "\(result.audioSummary)\(suffix)", priority: .medium))
    }

    @discardableResult
    func basesOpen(row: BasesRow) -> String? {
        if let taskOrdinal = row.taskOrdinal,
            let task = taskItem(path: row.filePath, ordinal: taskOrdinal)
        {
            openTaskRowInEditor(
                TaskWithLocation(task: task, path: row.filePath, fileName: baseFilename(row.filePath)))
            return postBaseActionEvent(
                .openedAtLine(filename: baseFilename(row.filePath), line: UInt32(task.line)))
        }
        openFile(row.filePath, target: .currentTab)
        return postBaseActionEvent(.openedFile(filename: baseFilename(row.filePath)))
    }

    @discardableResult
    func basesCopyLink(for row: BasesRow) -> String {
        let link = baseWikilink(for: row.filePath)
        NSPasteboard.general.clearContents()
        NSPasteboard.general.setString(link, forType: .string)
        postBaseActionAnnouncement("Copied link to \(displayNameWithoutExtension(row.filePath)).")
        return link
    }

    @discardableResult
    func basesShowBacklinks(for row: BasesRow) -> String {
        openFile(row.filePath, target: .currentTab)
        workspace.activeLeaf = .backlinks
        focusLeafRegionRevealingPane()  // #882: un-hide the pane on reveal
        let text = "Backlinks for \(displayNameWithoutExtension(row.filePath))."
        postBaseActionAnnouncement(text)
        return text
    }

    /// The reserved "show local graph" row action (Bases gap O15 /
    /// n3 §N3-4 rule 1), realized now that Milestone P's Connections
    /// leaf exists: re-root it on the row's note.
    func basesShowConnections(for row: BasesRow) {
        reRootConnections(on: row.filePath)
    }

    func basesExportText(
        format: ExportFormat,
        includeQuickFilter: Bool = true
    ) async throws -> String {
        guard let doc = activeBaseDocument, let session = currentSession else {
            throw BaseActionError.noActiveBase
        }
        let snapshot = try doc.exportSnapshot(
            format: format,
            includeQuickFilter: includeQuickFilter)
        let observer = baseRetargetNativeExecutionObserverForTesting
        let outcome: BaseExportOutcome = await Task.detached(priority: .userInitiated) {
            do {
                return .success(
                    try BaseNativeExporter.run(
                        session: session,
                        snapshot: snapshot,
                        observer: observer))
            } catch {
                return .failure(error.localizedDescription)
            }
        }.value
        guard currentSession === session,
            activeBaseDocument === doc,
            doc.ownsExportSnapshot(snapshot)
        else {
            throw BaseActionError.staleBase
        }
        switch outcome {
        case .success(let text): return text
        case .failure(let message): throw BaseActionError.nativeFailure(message)
        }
    }

    @discardableResult
    func basesCopyViewAsMarkdown(
        includeQuickFilter: Bool? = nil
    ) -> Task<String?, Never>? {
        guard let doc = activeBaseDocument else {
            postBaseActionAnnouncement("Base view could not be copied: No active base.")
            return nil
        }
        let shouldIncludeQuickFilter =
            includeQuickFilter ?? baseExportQuickFilterChoice(doc: doc, verb: "Copy")
        guard let shouldIncludeQuickFilter else { return nil }
        return Task { @MainActor [weak self] in
            guard let self else { return nil }
            do {
                let markdown = try await self.basesExportText(
                    format: .markdown,
                    includeQuickFilter: shouldIncludeQuickFilter)
                NSPasteboard.general.clearContents()
                NSPasteboard.general.setString(markdown, forType: .string)
                self.postBaseActionAnnouncement("Copied base view as Markdown.")
                return markdown
            } catch {
                self.postBaseActionAnnouncement(
                    "Base view could not be copied: \(error.localizedDescription)")
                return nil
            }
        }
    }

    func basesExportCSV() {
        basesExportToSavePanel(format: .csv, fileExtension: "csv")
    }

    func basesExportMarkdown() {
        basesExportToSavePanel(format: .markdown, fileExtension: "md")
    }

    @discardableResult
    func basesSetProperty(row: BasesRow, column: BasesColumn, value: PropertyValue) async -> String? {
        await basesApplyProperty(row: row, column: column, action: .set(value))
    }

    @discardableResult
    func basesDeleteProperty(row: BasesRow, column: BasesColumn) async -> String? {
        await basesApplyProperty(row: row, column: column, action: .delete)
    }

    func basesOpenSelectedRow() {
        guard let row = activeBaseSelectedRowForCommand() else {
            postBaseActionAnnouncement("Select a base row first.")
            return
        }
        basesOpen(row: row)
    }

    func basesCopySelectedLink() {
        guard let row = activeBaseSelectedRowForCommand() else {
            postBaseActionAnnouncement("Select a base row first.")
            return
        }
        basesCopyLink(for: row)
    }

    func basesShowSelectedBacklinks() {
        guard let row = activeBaseSelectedRowForCommand() else {
            postBaseActionAnnouncement("Select a base row first.")
            return
        }
        basesShowBacklinks(for: row)
    }

    func basesEditSelectedProperty() {
        guard activeBaseSelectedRowForCommand() != nil else {
            postBaseActionAnnouncement("Select a base row first.")
            return
        }
        guard activeBaseSelectedColumn != nil else {
            postBaseActionAnnouncement("No editable property is available for the selected row.")
            return
        }
        baseEditPropertyRequestToken &+= 1
    }

    func updateActiveBaseSelection(
        path: String,
        rowID: String?,
        columnIndex: Int?,
        result: BasesResultSet?
    ) {
        guard let result else {
            clearActiveBaseSelection()
            return
        }
        let rows = result.rows.enumerated().map { BaseGridRow(row: $0.element, ordinal: $0.offset) }
        guard let rowID, let selected = rows.first(where: { $0.id == rowID }) else {
            clearActiveBaseSelection()
            return
        }
        activeBaseSelectionPath = path
        activeBaseSelectedRow = selected.row
        if let columnIndex, result.columns.indices.contains(columnIndex) {
            activeBaseSelectedColumn = result.columns[columnIndex]
        } else {
            activeBaseSelectedColumn = result.columns.first {
                BaseCellEditPolicy.propertyKey(for: $0) != nil
            }
        }
    }

    func clearActiveBaseSelection() {
        activeBaseSelectionPath = nil
        activeBaseSelectedRow = nil
        activeBaseSelectedColumn = nil
    }

    func basesRefresh() {
        guard let doc = activeBaseDocument, let session = currentSession else { return }
        if let reason = baseDocumentRefreshDisabledReason(for: doc) {
            postMutationAnnouncement(reason)
            return
        }
        guard loadBaseDocumentIfAllowed(doc, session: session) else { return }
        postAccessibilityAnnouncement(.baseRefreshed)
    }

    @discardableResult
    func copyBaseQuickFilterDraft(
        _ document: BaseDocument,
        to pasteboard: NSPasteboard = .general
    ) -> Bool {
        pasteboard.clearContents()
        return copyBaseQuickFilterDraft(document) {
            pasteboard.setString($0, forType: .string)
        }
    }

    /// Testable copy core. Keeping the pasteboard boundary injectable avoids
    /// treating an unavailable macOS pasteboard server as a Base recovery
    /// failure while still exercising the exact success/error announcements.
    @discardableResult
    func copyBaseQuickFilterDraft(
        _ document: BaseDocument,
        _ write: (String) -> Bool
    ) -> Bool {
        guard !document.quickFilterText.isEmpty else { return false }
        guard write(document.quickFilterText) else {
            postMutationAnnouncement("Quick filter draft could not be copied.")
            return false
        }
        postMutationAnnouncement("Quick filter draft copied.")
        return true
    }

    func releaseBaseDocumentIfUnreferenced(_ item: EditorItem?) {
        guard let source = item.flatMap(BaseDocumentSource.init(item:)) else { return }
        let stillOpen = workspace.model.allTabs.contains { BaseDocumentSource(item: $0.item)?.key == source.key }
        guard !stillOpen, let doc = baseDocuments[source.key] else { return }
        if let session = currentSession {
            doc.close(session: session)
        }
        baseDocuments[source.key] = nil
    }

    func releaseDashboardDocumentIfUnreferenced(_ item: EditorItem?) {
        guard case .dashboard(let id, _) = item else { return }
        let stillOpen = workspace.model.allTabs.contains {
            if case .dashboard(let dashboardID, _) = $0.item {
                return dashboardID == id
            }
            return false
        }
        guard !stillOpen, let doc = dashboardDocuments[id] else { return }
        if let session = currentSession {
            doc.close(session: session)
        }
        dashboardDocuments[id] = nil
    }

    func rekeyBaseDocumentIfRetargeted(_ changed: [TabID], oldPath: String, newPath: String) {
        let plans = detachBaseDocumentsForRetarget(
            changed, oldPath: oldPath, newPath: newPath)
        scheduleNativeDocumentRetargets(plans)
    }

    /// MainActor-only identity landing for both tab-owned and dock-owned Base
    /// documents. It performs no native close/open/views/execute call.
    func detachBaseDocumentsForRetarget(
        _ changed: [TabID],
        oldPath: String,
        newPath: String
    ) -> [NativeDocumentRetargetPlan] {
        let oldKey = BaseDocumentSource.file(path: oldPath).key
        let newSource = BaseDocumentSource.file(path: newPath)
        let newKey = newSource.key
        guard !BaseExactIdentity.matches(oldPath, newPath) else { return [] }
        var plans: [NativeDocumentRetargetPlan] = []

        let retargetedBaseTab = changed.contains(where: { id in
            workspace.model.allTabs.contains {
                guard $0.id == id, case .base(let path) = $0.item else { return false }
                return BaseExactIdentity.matches(path, newPath)
            }
        })
        if retargetedBaseTab, let doc = baseDocuments.removeValue(forKey: oldKey) {
            let reservation = doc.beginBatchRetarget(to: newSource)
            let collided = baseDocuments[newKey].map { $0 !== doc } ?? false
            if !collided {
                baseDocuments[newKey] = doc
            }
            let visible = !collided && workspace.model.groupsInOrder.contains { group in
                guard case .base(let path)? = group.activeTab?.item else { return false }
                return BaseExactIdentity.matches(path, newPath)
            }
            let shouldPrepare = visible
                && doc.claimRetargetPreparation() == reservation.generation
            plans.append(
                .base(
                    owner: .registry(key: newKey),
                    source: newSource,
                    generation: reservation.generation,
                    replacedHandle: reservation.replacedHandle,
                    request: reservation.request,
                    prepare: shouldPrepare))
        }

        if case .base(let dockPath, _) = basesDock.target,
            BaseExactIdentity.matches(dockPath, oldPath)
        {
            basesDockRefreshTask?.cancel()
            basesDockRefreshTask = nil
            let target = BasesDockTarget.base(
                path: newPath, name: newSource.displayName)
            basesDock.setTarget(target)
            let dockIsVisible = isRightPaneVisible && workspace.activeLeaf == .basesDock
            if let dock = basesDockDocument {
                let reservation = dock.beginBatchRetarget(
                    to: newSource, thisPath: basesDock.thisPath)
                let shouldPrepare = dockIsVisible
                    && dock.claimRetargetPreparation() == reservation.generation
                plans.append(
                    .base(
                        owner: .basesDock,
                        source: newSource,
                        generation: reservation.generation,
                        replacedHandle: reservation.replacedHandle,
                        request: reservation.request,
                        prepare: shouldPrepare))
            } else if dockIsVisible {
                let dock = BaseDocument(source: newSource)
                basesDockDocument = dock
                let reservation = dock.beginBatchRetarget(
                    to: newSource, thisPath: basesDock.thisPath)
                let shouldPrepare =
                    dock.claimRetargetPreparation() == reservation.generation
                plans.append(
                    .base(
                        owner: .basesDock,
                        source: newSource,
                        generation: reservation.generation,
                        replacedHandle: reservation.replacedHandle,
                        request: reservation.request,
                        prepare: shouldPrepare))
            }
        }
        return plans
    }

    func scheduleBaseRetargetPreparationIfNeeded(
        document: BaseDocument,
        owner: NativeDocumentRetargetOwner,
        source: BaseDocumentSource,
        session: VaultSession
    ) {
        let ownsDocument: Bool
        switch owner {
        case .registry(let key):
            ownsDocument = baseDocuments[key] === document
        case .basesDock:
            ownsDocument = basesDockDocument === document
        }
        guard currentSession === session, ownsDocument,
            let generation = document.claimRetargetPreparation()
        else { return }
        let request = document.preparedRetargetRequest(thisPath: owner == .basesDock
            ? basesDock.thisPath : nil)
        scheduleNativeDocumentRetargets(
            [
                .base(
                    owner: owner,
                    source: source,
                    generation: generation,
                    replacedHandle: nil,
                    request: request,
                    prepare: true)
            ],
            session: session)
    }

    func invalidateBaseDocument(path: String) {
        let key = BaseDocumentSource.file(path: path).key
        guard let doc = baseDocuments[key] else { return }
        doc.markMovedToTrash(session: currentSession)
    }

    func releaseAllBaseDocuments() {
        if let session = currentSession {
            for doc in baseDocuments.values {
                doc.close(session: session)
            }
        }
        baseDocuments = [:]
    }

    func releaseAllDashboardDocuments() {
        if let session = currentSession {
            for doc in dashboardDocuments.values {
                doc.close(session: session)
            }
            basesDockDocument?.close(session: session)
            basesDockDashboardDocument?.close(session: session)
        }
        dashboardDocuments = [:]
        basesDockDocument = nil
        basesDockDashboardDocument = nil
        basesDockRefreshTask?.cancel()
        basesDockRefreshTask = nil
        basesDock = BasesDockState()
    }

    func releaseAllBaseEmbedDocuments() {
        if let session = currentSession {
            for handle in baseEmbedHandles.values {
                handle.close(session: session)
            }
        }
        baseEmbedHandles = [:]
    }

    /// Active-note transitions must not tear down embeds still rendered in a
    /// sibling editor/reading surface. Weak document leases identify handles
    /// with no visible owner; those are the only entries safe to release here.
    func releaseUnleasedBaseEmbedDocuments() {
        guard let session = currentSession else {
            baseEmbedHandles = [:]
            return
        }
        let unleased = baseEmbedHandles.filter { !$0.value.hasLiveLease }
        for (key, handle) in unleased {
            handle.close(session: session)
            baseEmbedHandles[key] = nil
        }
    }

    private enum BasePropertyAction: Equatable {
        case set(PropertyValue)
        case delete
    }

    private enum BaseActionError: LocalizedError {
        case noActiveBase
        case staleBase
        case nativeFailure(String)

        var errorDescription: String? {
            switch self {
            case .noActiveBase:
                return "No active base."
            case .staleBase:
                return "The Base view changed before the export finished. Try again."
            case .nativeFailure(let message):
                return message
            }
        }
    }

    private func basesApplyProperty(
        row: BasesRow,
        column: BasesColumn,
        action: BasePropertyAction
    ) async -> String? {
        guard let key = BaseCellEditPolicy.propertyKey(for: column) else {
            let hint = BaseCellEditPolicy.readOnlyHint(for: column)
            postBaseActionAnnouncement(hint)
            return hint
        }
        guard admitBatchTrashWrite(to: [row.filePath]) else { return nil }
        guard let session = currentSession, let doc = activeBaseDocument else { return nil }
        let outcome: Result<SaveReport, VaultError> = await Task.detached(priority: .userInitiated) {
            do {
                switch action {
                case .set(let value):
                    return .success(
                        try session.setProperty(
                            path: row.filePath,
                            key: key,
                            value: value,
                            expectedContentHash: nil))
                case .delete:
                    return .success(
                        try session.deleteProperty(
                            path: row.filePath,
                            key: key,
                            expectedContentHash: nil))
                }
            } catch let error as VaultError {
                return .failure(error)
            } catch {
                return .failure(.Io(message: error.localizedDescription))
            }
        }.value

        guard currentSession === session else { return nil }

        switch outcome {
        case .success:
            _ = await refreshVisibleBasesAfterInAppWrite(
                session: session,
                changedPath: row.filePath)?.value
            guard currentSession === session else { return nil }
            let stillRegistered = baseDocuments[doc.source.key] === doc
            let stillPresent = stillRegistered && (doc.result?.rows.contains {
                BaseExactIdentity.matches($0.filePath, row.filePath)
                    && $0.taskOrdinal == row.taskOrdinal
            } ?? false)
            let text: String
            if stillPresent {
                switch action {
                case .set(let value):
                    text = "Saved. \(column.label): \(BaseCellEditPolicy.displayValue(value))"
                case .delete:
                    text = "Saved. \(column.label): empty"
                }
            } else {
                text = "Saved. Row no longer matches this view"
            }
            postBaseActionAnnouncement(text)
            return text
        case .failure(let error):
            let text = "Base edit failed: \(error.localizedDescription)"
            postBaseActionAnnouncement(text)
            return text
        }
    }

    /// One ownership-safe landing funnel for text produced by a standard save
    /// panel. Save panels may target either the originating vault or an
    /// arbitrary external directory. We conservatively serialize both through
    /// the structural gate; only a newly created in-vault destination refreshes
    /// the tree and barriers structural undo.
    @discardableResult
    func performBaseSavePanelWrite(
        text: String,
        to url: URL,
        originSession: VaultSession,
        successMessage: String,
        failurePrefix: String,
        nativeThreadObserver: (@Sendable (Bool) -> Void)? = nil
    ) -> Task<Void, Never>? {
        guard currentSession === originSession else { return nil }

        let originVaultURL = currentVaultURL
        let relativePath = Self.vaultRelativePath(of: url, vaultURL: originVaultURL)
        guard admitStructuralMutationRequest() else { return nil }
        let recoveryReservation: StructuralRecoveryReservation?
        if let relativePath {
            guard let reservation = admitStructuralRecoveryDestination(relativePath),
                admitBatchTrashWrite(to: [relativePath])
            else { return nil }
            recoveryReservation = reservation
        } else {
            recoveryReservation = nil
        }
        let token = beginStructuralMutation(
            recoveryReservation: recoveryReservation)
        let refresher = structuralBatchRefreshRunner
        let task = Task { @MainActor [weak self] in
            let outcome: BaseVaultWriteOutcome = await Task.detached(
                priority: .userInitiated
            ) {
                let existedBefore = FileManager.default.fileExists(atPath: url.path)
                let scoped = url.startAccessingSecurityScopedResource()
                defer { if scoped { url.stopAccessingSecurityScopedResource() } }
                do {
                    nativeThreadObserver?(BaseWriterThreadProbe.isMainThread())
                    try text.write(to: url, atomically: true, encoding: .utf8)
                    return .success(existedBefore: existedBefore)
                } catch {
                    return .failure(error.localizedDescription)
                }
            }.value

            guard let self else { return }
            defer { self.endStructuralMutation(token) }
            guard self.ownsStructuralMutation(token, session: originSession) else { return }

            switch outcome {
            case .success(let existedBefore):
                if let relativePath {
                    await refresher(self)
                    guard self.ownsStructuralMutation(token, session: originSession)
                    else { return }
                    self.barrierStructuralUndoForCreatedVaultPath(
                        relativePath: relativePath,
                        existedBefore: existedBefore)
                    await self.refreshBaseQueries()?.value
                    guard self.ownsStructuralMutation(token, session: originSession)
                    else { return }
                    _ = await self.refreshVisibleBasesAfterInAppWrite(
                        session: originSession,
                        changedPath: relativePath)?.value
                    guard self.ownsStructuralMutation(token, session: originSession)
                    else { return }
                }
                self.postBaseActionAnnouncement(successMessage)
            case .failure(let message):
                self.postBaseActionAnnouncement("\(failurePrefix): \(message)")
            }
        }
        recordPendingStructuralTask(task)
        return task
    }

    private func basesExportToSavePanel(format: ExportFormat, fileExtension: String) {
        guard let doc = activeBaseDocument, let originSession = currentSession else { return }
        guard admitStructuralMutationRequest() else { return }
        guard let includeQuickFilter = baseExportQuickFilterChoice(doc: doc, verb: "Export")
        else { return }
        let suggestedName = "\(doc.displayName) — \(doc.activeViewName ?? "View").\(fileExtension)"
        Task { @MainActor [weak self] in
            guard let self else { return }
            do {
                let text = try await self.basesExportText(
                    format: format,
                    includeQuickFilter: includeQuickFilter)
                guard self.currentSession === originSession else { return }
                let panel = NSSavePanel()
                panel.nameFieldStringValue = suggestedName
                panel.begin { [weak self] response in
                    guard response == .OK, let url = panel.url else { return }
                    Task { @MainActor [weak self] in
                        guard let self, self.currentSession === originSession else { return }
                        _ = self.performBaseSavePanelWrite(
                            text: text,
                            to: url,
                            originSession: originSession,
                            successMessage: "Exported base view.",
                            failurePrefix: "Base view could not be exported")
                    }
                }
            } catch {
                self.postBaseActionAnnouncement(
                    "Base view could not be exported: \(error.localizedDescription)")
            }
        }
    }

    private func baseExportQuickFilterChoice(doc: BaseDocument, verb: String) -> Bool? {
        guard doc.quickFilterActive, let result = doc.result else { return true }
        let alert = NSAlert()
        alert.alertStyle = .informational
        alert.messageText = "\(verb) quick-filtered base view?"
        alert.informativeText =
            "Quick filter \"\(doc.quickFilterText)\" is active. Choose filtered rows "
            + "(\(result.shownCount)) or all rows (\(result.unfilteredShownCount))."
        alert.addButton(withTitle: "\(verb) Filtered Rows")
        alert.addButton(withTitle: "\(verb) All Rows")
        alert.addButton(withTitle: "Cancel")
        switch alert.runModal() {
        case .alertFirstButtonReturn:
            return true
        case .alertSecondButtonReturn:
            return false
        default:
            postBaseActionAnnouncement("\(verb) canceled.")
            return nil
        }
    }

    func postBaseActionAnnouncement(_ message: String) {
        lastBaseActionAnnouncement = message
        // W0.5-3 residue: Bases action message builders (postBaseActionAnnouncement callers)
        postAccessibilityAnnouncement(.hostComposed(text: message, priority: .medium))
    }

    /// Typed sibling of `postBaseActionAnnouncement`: posts the canonical
    /// event and feeds core's rendered text through the same
    /// `lastBaseActionAnnouncement` seam the string form records.
    @discardableResult
    func postBaseActionEvent(_ event: A11yEvent) -> String {
        let text = a11yRender(event: event).text
        lastBaseActionAnnouncement = text
        postAccessibilityAnnouncement(event)
        return text
    }

    private func activeBaseSelectedRowForCommand() -> BasesRow? {
        guard let doc = activeBaseDocument,
            activeBaseSelectionPath.map({
                BaseExactIdentity.matches($0, doc.selectionKey)
            }) == true,
            let row = activeBaseSelectedRow,
            doc.result?.rows.contains(where: { $0.hasSameBaseIdentity(as: row) }) == true
        else { return nil }
        return row
    }

    private func taskItem(path: String, ordinal: UInt64) -> TaskItem? {
        guard let session = currentSession else { return nil }
        return try? session.tasksForFile(path: path).first {
            UInt64($0.ordinal) == ordinal
        }
    }

    private func baseWikilink(for path: String) -> String {
        let target = (path as NSString).deletingPathExtension
        return "[[\(target)]]"
    }

    private func displayNameWithoutExtension(_ path: String) -> String {
        let name = (path as NSString).lastPathComponent
        return (name as NSString).deletingPathExtension
    }

    private func baseFilename(_ path: String) -> String {
        (path as NSString).lastPathComponent
    }
}

extension BasesRow {
    func hasSameBaseIdentity(as other: BasesRow) -> Bool {
        BaseExactIdentity.matches(filePath, other.filePath)
            && taskOrdinal == other.taskOrdinal
    }
}
