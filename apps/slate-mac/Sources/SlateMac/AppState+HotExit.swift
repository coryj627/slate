// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import Combine
import Foundation

/// Plan 43 PR 29 (spec `docs/plans/43_mac_backlog_delivery/specs/pr29_hot_exit_mac_half.md`):
/// the discard-pending prompt (owner call D-1). Its message and its three
/// labels are core's.
struct HotExitDiscardPendingPrompt: Identifiable, Equatable {
    let id = UUID()
    let message: String
    let tryAgain: String
    let closeKeepingCopies: String
    let cancel: String
}

/// What the discard-pending prompt's choices do for the route that showed it.
struct HotExitDiscardPendingHandlers {
    /// Try again: true when the store has now confirmed what it owed.
    let retry: @MainActor () async -> Bool
    /// Close once the store confirmed (a Discard All's revert and close).
    let proceed: @MainActor () -> Void
    /// Close and keep the copies: close without discarding anything more.
    let keepCopies: @MainActor () -> Void
    /// Keep the vault open (a quit replies false).
    let cancel: @MainActor () -> Void
    /// What the reader had when the route chose; either close re-checks
    /// against it first, so text typed since is asked about (owner call 3).
    let before: HotExitRecheck
}

enum HotExitDiscardPendingChoice {
    case tryAgain, closeKeepingCopies, cancel
}

/// §4's re-check: what a close compares after a settle with what it saw
/// before. Text typed, a tab dirtied, a draft edited or a save started in
/// between sends the close back to its own prompt.
struct HotExitRecheck {
    let dirty: [TabID: HotExitBuffer]
    let draftRevision: UInt64
    let saving: Bool

    func unchanged(since earlier: HotExitRecheck) -> Bool {
        guard !saving, draftRevision == earlier.draftRevision,
            dirty.count == earlier.dirty.count
        else { return false }
        return dirty.allSatisfy { id, buffer in
            earlier.dirty[id].map { $0.sameSnapshot(buffer) && $0.dirty == buffer.dirty } ?? false
        }
    }
}

// MARK: - The writer's view of the tabs (§3)

extension AppState: HotExitBufferSource {
    var hotExitMarkdownTabs: [TabID] {
        workspace.model.allTabs.compactMap { tab in
            if case .markdown = tab.item { return tab.id }
            return nil
        }
    }

    func hotExitTabPath(_ tab: TabID) -> String? {
        guard let found = workspace.model.tab(tab), case .markdown(let path) = found.item else {
            return nil
        }
        return path
    }

    /// The active tab's buffer lives in the fields while it is loaded; its
    /// parked document is stale while it is active. Every other tab reads
    /// its parked document once it has loaded.
    func hotExitBuffer(for tab: TabID) -> HotExitBuffer? {
        guard let path = hotExitTabPath(tab) else { return nil }
        if workspace.activeTab?.id == tab {
            guard let loaded = loadedFilePath, BaseExactIdentity.matches(loaded, path),
                let body = currentNoteText
            else { return nil }
            return HotExitBuffer(
                stamp: .init(
                    active: true, revision: hotExitBufferRevision, document: nil, path: loaded),
                path: loaded, body: body, fmSource: currentNoteFMSource,
                baselineHash: currentNoteContentHash, baselineIdentity: currentNoteBaselineIdentity,
                dirty: hasUnsavedChanges)
        }
        guard let document = workspace.document(for: tab), document.hasLoaded,
            BaseExactIdentity.matches(document.path, path)
        else { return nil }
        return HotExitBuffer(
            stamp: .init(
                active: false, revision: document.hotExitRevision,
                document: ObjectIdentifier(document), path: document.path),
            path: document.path, body: document.text, fmSource: document.fmSource,
            baselineHash: document.contentHash, baselineIdentity: document.baselineIdentity,
            dirty: document.hasUnsavedChanges)
    }

    func hotExitEntryId(for tab: TabID) -> String {
        workspace.recoveryEntryId(for: tab)
    }

    func hotExitTabExists(_ tab: TabID) -> Bool {
        workspace.model.tab(tab) != nil
    }

    func hotExitAssignFreshEntryId(_ tab: TabID) {
        workspace.assignFreshRecoveryEntryId(tab)
    }
}

extension AppState {
    // MARK: - Installation (§1)

    /// The app's launch-time install of the production store under
    /// Application Support, beside the sidebar journal's.
    @MainActor static func installProductionHotExitStore() {
        guard !sharedHotExitStoreHost.hasStore,
            let root = HotExitStoreHost.applicationSupportRoot()
        else { return }
        sharedHotExitStoreHost = HotExitStoreHost.install(root: root)
    }

    /// O7: every published change and every parked document's change
    /// schedules one coalesced re-evaluation, run after the mutation.
    func wireHotExitObservation() {
        objectWillChange
            .sink { [weak self] _ in self?.hotExitWriter?.observe() }
            .store(in: &hotExitCancellables)
        workspace.objectWillChange
            .sink { [weak self] _ in self?.hotExitWriter?.observe() }
            .store(in: &hotExitCancellables)
        workspace.onBufferChange = { [weak self] in self?.hotExitWriter?.observe() }
        // The not-kept sentence belongs to the close prompt it was posted
        // for: it goes when no close prompt shows.
        $pendingNavigation.combineLatest($pendingVaultClose)
            .map { navigation, count in navigation != .closeVault && count == nil }
            .removeDuplicates()
            .filter { $0 }
            .sink { [weak self] _ in self?.hotExitPromptNotice = nil }
            .store(in: &hotExitCancellables)
    }

    /// The VoiceOver field pass (2026-11-14): VoiceOver reads a prompt the
    /// moment it opens, over the not-kept sentence posted just before it, so
    /// the close prompt shows the sentence under its question as well.
    func showNotKeptInPrompt(_ notKept: A11yEvent) {
        hotExitPromptNotice = a11yRender(event: notKept).text
    }

    /// Whether this vault's closes go through hot exit: a writer from an
    /// installed host (an uninstalled host keeps today's prompts silently).
    var hotExitEngaged: Bool {
        guard let writer = hotExitWriter else { return false }
        return !(writer.isInert && writer.open.reason == nil && writer.key == nil)
    }

    /// Every unsaved buffer or draft a close would ask about.
    var hotExitAnyDirty: Bool {
        hasUnsavedChanges || !workspace.dirtyParkedDocuments().isEmpty
            || !uncommittedNoteDraftPaths().isEmpty
    }

    func hotExitRecheck() -> HotExitRecheck {
        var dirty: [TabID: HotExitBuffer] = [:]
        for id in hotExitMarkdownTabs {
            if let buffer = hotExitBuffer(for: id), buffer.dirty { dirty[id] = buffer }
        }
        return HotExitRecheck(dirty: dirty, draftRevision: hotExitDraftRevision, saving: isSaving)
    }

    // MARK: - The open (§2)

    /// Steps 1–3, after WB-28's root-identity admission: the key, the hold,
    /// and whether an earlier session's work is still out.
    func beginHotExitOpen(session: VaultSession) {
        tearDownHotExit()
        let host = hotExitHost
        guard host.hasStore else {
            hotExitPendingOpen = .none(host.unavailableReason)
            return
        }
        switch session.hotExitKey() {
        case .unavailable(let reason):
            hotExitPendingOpen = .none(reason)
        case .key(let key):
            let hold = host.acquireHold(key)
            guard hold.held else {
                hotExitPendingOpen = .none(hold.reason)
                return
            }
            host.retryParkedRetirements(key)
            if host.hasUnresolved(key) {
                // Deviation 1: the Mac open never waits on the main actor.
                host.releaseSession(key)
                hotExitPendingOpen = .none(hotExitReasonEarlierWorkPending())
                return
            }
            hotExitPendingOpen = .active(key, epoch: 0)
        }
    }

    /// Steps 4–5: the read and core's plan, at the layout restore. Creates
    /// the vault's writer either way.
    func readAndPlanHotExit(layoutEntryIds: [String]) -> HotExitRestorePlan? {
        let pending = hotExitPendingOpen ?? .none(hotExitHost.unavailableReason)
        hotExitPendingOpen = nil
        guard let key = pending.activeKey, let session = currentSession else {
            hotExitWriter = HotExitWriter(host: hotExitHost, open: pending, source: self)
            return nil
        }
        let read = hotExitHost.readNow(key)
        if read.damaged {
            hotExitWriter = HotExitWriter(
                host: hotExitHost,
                open: .damaged(key, reason: read.errorReason ?? hotExitReasonStoreUnwritable()),
                source: self)
            return nil
        }
        guard read.succeeded, let snapshot = read.value else {
            // Entries nobody has read are never deleted on a transient error.
            hotExitHost.releaseSession(key)
            hotExitWriter = HotExitWriter(
                host: hotExitHost, open: .none(read.errorReason), source: self)
            return nil
        }
        hotExitWriter = HotExitWriter(
            host: hotExitHost, open: .active(key, epoch: snapshot.epoch), source: self)
        return session.planHotExitRestore(snapshot: snapshot, layoutEntryIds: layoutEntryIds)
    }

    /// The vault is closing or being replaced: every live episode is
    /// abandoned (O4); the hold goes once its obligations resolve.
    func tearDownHotExit() {
        hotExitRouteGeneration &+= 1
        if let writer = hotExitWriter {
            writer.detach()
            if let key = writer.key {
                hotExitHost.releaseSession(key)
            }
        }
        if let key = hotExitPendingOpen?.key { hotExitHost.releaseSession(key) }
        hotExitWriter = nil
        hotExitPendingOpen = nil
        pendingHotExitNotices = []
        heldLaunchAnnouncement?.cancel()
        heldLaunchAnnouncement = nil
        hotExitPromptNotice = nil
        pendingHotExitDiscard = nil
        hotExitDiscardPendingHandlers = nil
    }

    // MARK: - Restore (§5)

    /// The entry ids of a layout's Markdown tabs (§2 step 5): only a tab
    /// that is built and holds a note can take an entry.
    static func markdownEntryIds(_ model: WorkspaceModel?) -> [String] {
        model?.allTabs.compactMap { tab in
            if case .markdown = tab.item { return tab.id.raw.uuidString }
            return nil
        } ?? []
    }

    /// Writes each displaced tab's fresh id (and its group's active tab) and
    /// each restored tab's classified path into the saved layout before the
    /// model is built from it.
    static func rewriteForHotExit(
        _ snapshot: WorkspaceStore.Snapshot,
        plan: HotExitRestorePlan,
        displaced: [String: TabID]
    ) -> WorkspaceStore.Snapshot {
        var paths: [String: String] = [:]
        for planned in plan.layout { paths[planned.entryId] = planned.path }
        for planned in plan.displaced { paths[planned.entryId] = planned.path }
        func rewrite(_ node: WorkspaceStore.Node) -> WorkspaceStore.Node {
            switch node {
            case .group(let id, let activeTab, let tabs):
                let active = activeTab.map { active in
                    displaced[active.uuidString]?.raw ?? active
                }
                let rewritten = tabs.map { tab -> WorkspaceStore.Tab in
                    var tab = tab
                    let key = tab.id.uuidString
                    if let path = paths[key], tab.item.kind == "markdown" {
                        tab.item.path = path
                    }
                    if let fresh = displaced[key] { tab.id = fresh.raw }
                    return tab
                }
                return .group(id: id, activeTab: active, tabs: rewritten)
            case .split(let axis, let weights, let children):
                return .split(axis: axis, weights: weights, children: children.map(rewrite))
            }
        }
        var snapshot = snapshot
        snapshot.root = rewrite(snapshot.root)
        return snapshot
    }

    /// Installs the plan's buffers into the adopted layout, before the first
    /// activation, so no restored tab reads the disk or reports a load
    /// failure first. Each restored tab starts its episode adopting.
    func installHotExitRestore(_ plan: HotExitRestorePlan, displaced: [String: TabID]) {
        guard let writer = hotExitWriter, !writer.isInert, let session = currentSession else { return }
        var notices: [A11yEvent] = []
        var restored = 0
        var installed: [NoteDocument] = []
        func inheritedOf(_ planned: HotExitPlannedTab) -> [String] {
            var inherited: [String] = []
            if let observed = planned.observedClaim { inherited.append(observed) }
            if !inherited.contains(planned.entry.incarnation) {
                inherited.append(planned.entry.incarnation)
            }
            return inherited
        }
        func land(
            _ planned: HotExitPlannedTab, tab: TabID, inherited: [String], observed: String?,
            recoveryId: String?
        ) {
            let document = makeRestoredDocument(planned, tab: tab, session: session)
            workspace.installRestoredDocument(document, recoveryEntryId: recoveryId)
            installed.append(document)
            if document.hasUnsavedChanges {
                // Only what comes back unsaved is counted and spoken (red
                // team PR 29.2).
                restored += 1
                if let notice = Self.restoreNotice(planned) { notices.append(notice) }
                writer.installRestored(tab, observedClaim: observed, inherited: inherited)
            } else {
                // A kept buffer equal to the disk restores clean: nothing to
                // keep, so what it inherited retires at once.
                writer.retireUnheld(
                    entryId: recoveryId ?? tab.raw.uuidString, incarnations: inherited)
            }
        }
        for planned in plan.layout {
            guard let uuid = UUID(uuidString: planned.entryId) else { continue }
            land(
                planned, tab: TabID(raw: uuid), inherited: inheritedOf(planned),
                observed: planned.observedClaim, recoveryId: nil)
        }
        for planned in plan.displaced {
            guard let fresh = displaced[planned.entryId] else { continue }
            land(planned, tab: fresh, inherited: [], observed: nil, recoveryId: nil)
        }
        let taken = Set(workspace.model.allTabs.map(\.id))
        for planned in plan.newTabs {
            // A canonical id becomes the tab's id, so the next layout names
            // it and a kept close reopens one tab, not two (§2).
            let canonical = UUID(uuidString: planned.entryId).flatMap {
                $0.uuidString == planned.entryId ? TabID(raw: $0) : nil
            }
            let id = canonical.flatMap { taken.contains($0) ? nil : $0 } ?? TabID()
            workspace.appendRestoredTab(WorkspaceTab(id: id, item: .markdown(path: planned.path)))
            land(
                planned, tab: id, inherited: inheritedOf(planned),
                observed: planned.observedClaim, recoveryId: planned.entryId)
        }
        // Codex round 1: a same-path layout tab with no entry of its own
        // takes the restored buffer, or its first activation would read the
        // disk and its first edit mirror that over the restored text.
        for tab in workspace.model.allTabs {
            guard case .markdown(let path) = tab.item, workspace.document(for: tab.id) == nil,
                let source = installed.last(where: {
                    $0.hasUnsavedChanges && BaseExactIdentity.matches($0.path, path)
                })
            else { continue }
            workspace.installRestoredDocument(Self.copy(source, as: tab.id))
        }
        for orphan in plan.orphanClaims {
            if let holder = workspace.model.allTabs.first(where: { tab in
                if case .markdown = tab.item {
                    return workspace.recoveryEntryId(for: tab.id) == orphan.entryId
                }
                return false
            }) {
                writer.inheritOrphanClaim(holder.id, incarnation: orphan.incarnation)
            } else {
                writer.retireUnheld(entryId: orphan.entryId, incarnations: [orphan.incarnation])
            }
        }
        for redundant in plan.redundant {
            writer.retireUnheld(entryId: redundant.entryId, incarnations: redundant.incarnations)
        }
        if restored > 0 {
            pendingHotExitNotices = [.unsavedNotesRestored(count: UInt32(restored))] + notices
        }
        writer.observe()
    }

    private func makeRestoredDocument(
        _ planned: HotExitPlannedTab, tab: TabID, session: VaultSession
    ) -> NoteDocument {
        let split = splitNote(text: planned.text)
        let document = NoteDocument(id: tab, path: planned.path)
        document.text = split.body
        document.fmSource = split.fmSource
        document.bodyByteOffset = Int(split.bodyByteOffset)
        document.bodyLineOffset = Int(split.bodyLineOffset)
        document.hasLoaded = true
        switch planned.decision {
        case .restore, .restoreStale:
            // The baseline is the entry's, not the disk's: the first save
            // sends it and conflicts when the file changed since (§5).
            document.contentHash = planned.entry.baselineHash
            document.baselineIdentity = BaselineIdentity(
                fileIdentity: planned.entry.baselineFileId, identityEnforced: true,
                textUnknown: true)
            if case .restore = planned.decision,
                let disk = try? session.readNoteParts(path: planned.path),
                disk.contentHash == planned.entry.baselineHash
            {
                document.savedBaselineText = disk.body
                document.baselineIdentity.textUnknown = false
                if BaseExactIdentity.matches(disk.body, split.body),
                    BaseExactIdentity.matches(disk.fmSource, split.fmSource)
                {
                    document.baselineIdentity = .fromDisk(disk.fileIdentity)
                    document.hasUnsavedChanges = false
                    return document
                }
            }
        case .existsNow, .gone, .recovered:
            // A new unsaved note: its first save is the exclusive create,
            // which refuses a file now there.
            document.contentHash = nil
            document.baselineIdentity = BaselineIdentity(textUnknown: true)
        }
        document.hasUnsavedChanges = true
        return document
    }

    private static func copy(_ source: NoteDocument, as tab: TabID) -> NoteDocument {
        let document = NoteDocument(id: tab, path: source.path)
        document.text = source.text
        document.savedBaselineText = source.savedBaselineText
        document.contentHash = source.contentHash
        document.baselineIdentity = source.baselineIdentity
        document.fmSource = source.fmSource
        document.bodyByteOffset = source.bodyByteOffset
        document.bodyLineOffset = source.bodyLineOffset
        document.hasLoaded = true
        document.hasUnsavedChanges = source.hasUnsavedChanges
        return document
    }

    static func restoreNotice(_ planned: HotExitPlannedTab) -> A11yEvent? {
        let name = (planned.path as NSString).lastPathComponent
        switch planned.decision {
        case .restore: return nil
        case .restoreStale: return .restoredNoteChangedOnDisk(name: name)
        case .existsNow: return .restoredNoteExistsOnDisk(name: name)
        case .gone: return .restoredNoteMissingOnDisk(name: name)
        case .recovered: return .restoredNoteRecovered(name: name)
        }
    }

    // MARK: - The copy (§5)

    /// The vault-opened line, then core's restore sentences, once: the one
    /// seam both opening sites call, so the order is this function's.
    /// The split view's mount (the launch, or an open from the welcome
    /// screen). VoiceOver drops an announcement a newly launched app posts
    /// before it has read the app's window, and that read came 0.5 s to more
    /// than 4 s after the mount in the field pass (2026-11-14). So the first
    /// vault-opened line in a process, with core's restore sentences behind
    /// it, waits until the app has settled; every later mount speaks at once.
    func announceVaultOpenedOnMount(vaultTitle: String, sidebarNotice: String) {
        guard !hasAnnouncedAVaultOpen else {
            announceVaultOpened(vaultTitle: vaultTitle, sidebarNotice: sidebarNotice)
            return
        }
        heldLaunchAnnouncement?.cancel()
        heldLaunchAnnouncement = launchAnnouncementSettle { [weak self] in
            guard let self, self.heldLaunchAnnouncement != nil else { return }
            self.heldLaunchAnnouncement = nil
            self.announceVaultOpened(vaultTitle: vaultTitle, sidebarNotice: sidebarNotice)
        }
    }

    func announceVaultOpened(vaultTitle: String, sidebarNotice: String) {
        hasAnnouncedAVaultOpen = true
        announcer.post(.vaultOpened(vaultTitle: vaultTitle, sidebarNotice: sidebarNotice))
        let notices = pendingHotExitNotices
        pendingHotExitNotices = []
        for notice in notices { announcer.post(notice) }
    }

    // MARK: - Quit (§4)

    /// ⌘Q through hot exit: nil when hot exit does not take this quit
    /// (today's path runs).
    func admitHotExitTermination(
        reply: @escaping @MainActor (Bool) -> Void
    ) -> NSApplication.TerminateReply? {
        guard isVaultOpen, let writer = hotExitWriter, hotExitEngaged else { return nil }
        hotExitPromptNotice = nil
        if let reason = authoredPropertyPublicationRecoveryReason {
            postMutationAnnouncement(reason)
            return .terminateCancel
        }
        if let reason = inFlightDraftDiscardDisabledReason {
            postMutationAnnouncement(reason)
            return .terminateCancel
        }
        writer.reconcileNow()
        let dirty = hotExitAnyDirty
        if writer.isInert {
            // No key, a damaged store, a vault held elsewhere: today's
            // prompt, saying why (§6).
            if dirty, let reason = writer.open.reason {
                let notKept = A11yEvent.unsavedNotesNotKept(reason: reason)
                announcer.post(notKept)
                showNotKeptInPrompt(notKept)
            }
            return nil
        }
        // Sidebar or Trash work pending makes the quit wait with the editor
        // writable, so hot exit takes it over and re-checks after the wait
        // (Codex adversarial review, round 7).
        guard dirty || writer.hasUnresolvedObligations || Self.hasPendingSidebarWorkAtTermination
        else { return nil }
        isTerminationReplyOutstanding = true
        pendingTerminationSettleReply = reply
        hotExitRouteGeneration &+= 1
        let generation = hotExitRouteGeneration
        hotExitRouteTask = Task { [weak self] in await self?.runHotExitQuit(generation) }
        return .terminateLater
    }

    private func runHotExitQuit(_ generation: UInt64) async {
        // The sidebar fence first, so nothing typed lands after the last
        // re-check.
        var fenceExpired = false
        if Self.hasPendingSidebarWorkAtTermination {
            await Self.settleSidebarWriterChainsForTermination()
            fenceExpired = Self.hasPendingSidebarWorkAtTermination
        }
        while true {
            guard hotExitRouteGeneration == generation, let reply = pendingTerminationSettleReply,
                let writer = hotExitWriter
            else { return }
            let before = hotExitRecheck()
            let draftsBefore = !uncommittedNoteDraftPaths().isEmpty
            let result = await writer.settle(mode: .keep)
            guard hotExitRouteGeneration == generation, pendingTerminationSettleReply != nil else {
                return
            }
            let after = hotExitRecheck()
            let unchanged = after.unchanged(since: before)
            let draftsPending = draftsBefore || !uncommittedNoteDraftPaths().isEmpty
            if result.kept, unchanged, !draftsPending {
                // The sidebar and Trash fence once more, for work admitted
                // during the settle (red team PR 29.2). The editor stays
                // writable while it waits, so the settle and the re-check run
                // again after it: only a pass with no fence left answers
                // (Codex adversarial review, round 2).
                // A wait whose bound ran out with work still pending is not
                // repeated: that work was journaled before it was
                // acknowledged, as on today's path.
                if Self.hasPendingSidebarWorkAtTermination, !fenceExpired {
                    await Self.settleSidebarWriterChainsForTermination()
                    fenceExpired = Self.hasPendingSidebarWorkAtTermination
                    continue
                }
                pendingTerminationSettleReply = nil
                terminationAnswer(reply)(true)
                return
            }
            if after.dirty.isEmpty, !draftsPending {
                guard unchanged else { continue }
                // Nothing dirty and an obligation unresolved (D-1). The reply
                // waits where every newer request can answer it.
                pendingTerminationSettleReply = nil
                pendingTerminationReply = reply
                let close: @MainActor () -> Void = { [weak self] in
                    self?.finishPendingTermination(proceed: true)
                }
                presentHotExitDiscardPending(
                    retry: { [weak self] in await self?.hotExitSettleClean() ?? true },
                    proceed: close,
                    keepCopies: close,
                    cancel: { [weak self] in self?.finishPendingTermination(proceed: false) },
                    before: after)
                return
            }
            if unchanged, !result.kept || draftsPending,
                let reason = draftsPending ? hotExitReasonDraftsNotKept() : result.reason
            {
                let notKept = A11yEvent.unsavedNotesNotKept(reason: reason)
                announcer.post(notKept)
                showNotKeptInPrompt(notKept)
            }
            pendingTerminationSettleReply = nil
            switch presentTerminationPrompt() {
            case .presented:
                pendingTerminationReply = reply
                frontPromptHostForTermination()
                return
            case .clean:
                pendingTerminationSettleReply = reply
                continue
            case .refused:
                terminationAnswer(reply)(false)
                return
            }
        }
    }

    /// A quit about to answer yes waits on the sidebar and Trash fence, and
    /// the editor stays writable while it does. The buffers are compared with
    /// the moment of the choice once the fence is done: unchanged, the quit
    /// goes ahead; text typed meanwhile goes back to the quit's prompt (owner
    /// call 3), never closed over (Codex adversarial review, round 2).
    func finishHotExitQuitAfterFence(_ reply: @escaping @MainActor (Bool) -> Void) {
        let before = hotExitRecheck()
        let draftsBefore = uncommittedNoteDraftPaths()
        hotExitRouteTask = Task { [weak self] in
            await Self.settleSidebarWriterChainsForTermination()
            guard let self else {
                reply(true)
                return
            }
            if self.hotExitRecheck().unchanged(since: before),
                self.uncommittedNoteDraftPaths() == draftsBefore
            {
                self.terminationAnswer(reply)(true)
                return
            }
            switch self.presentTerminationPrompt() {
            case .presented:
                self.pendingTerminationReply = reply
                self.frontPromptHostForTermination()
            case .clean:
                self.terminationAnswer(reply)(true)
            case .refused:
                self.terminationAnswer(reply)(false)
            }
        }
    }

    /// A newer request takes the quit over while its settle runs: the quit is
    /// answered false and the settle's task stops deciding.
    func supersedeHotExitTerminationSettle() {
        guard let reply = pendingTerminationSettleReply else { return }
        pendingTerminationSettleReply = nil
        hotExitRouteGeneration &+= 1
        terminationAnswer(reply)(false)
    }

    /// The re-front `applicationShouldTerminate` does for today's prompt,
    /// for every prompt a quit presents later: the app comes forward, and a
    /// closed main window reopens to host it. The app installs it at launch
    /// (`SlateMacApp`); XCTest and previews keep the no-op, so no test
    /// activates its own process.
    @MainActor static var frontPromptHostHook: @MainActor (AppState) -> Void = { _ in }

    func frontPromptHostForTermination() {
        Self.frontPromptHostHook(self)
    }

    /// The production re-front.
    @MainActor static func frontPromptHostInApp(_ state: AppState) {
        guard let app = NSApp else { return }
        app.activate()
        if let window = state.promptHostWindow {
            window.makeKeyAndOrderFront(nil)
        } else {
            DispatchQueue.main.async { SlateAppDelegate.reopenMainWindow() }
        }
    }

    // MARK: - Vault switch (§4)

    /// A switch through hot exit: true when it took the switch over (the
    /// close follows from its task).
    func beginHotExitSwitch() -> Bool {
        guard let writer = hotExitWriter, hotExitEngaged else { return false }
        supersedeHotExitTerminationSettle()
        hotExitPromptNotice = nil
        writer.reconcileNow()
        let dirty = hotExitAnyDirty
        if writer.isInert {
            if dirty, let reason = writer.open.reason {
                let notKept = A11yEvent.unsavedNotesNotKept(reason: reason)
                announcer.post(notKept)
                showNotKeptInPrompt(notKept)
            }
            return false
        }
        guard dirty || writer.hasUnresolvedObligations else { return false }
        hotExitRouteGeneration &+= 1
        let generation = hotExitRouteGeneration
        hotExitRouteTask = Task { [weak self] in await self?.runHotExitSwitch(generation) }
        return true
    }

    private func runHotExitSwitch(_ generation: UInt64) async {
        while true {
            guard hotExitRouteGeneration == generation, pendingVaultSwitchTarget != nil,
                let writer = hotExitWriter
            else { return }
            let before = hotExitRecheck()
            let draftsBefore = !uncommittedNoteDraftPaths().isEmpty
            let result = await writer.settle(mode: .keep)
            guard hotExitRouteGeneration == generation, pendingVaultSwitchTarget != nil else { return }
            let after = hotExitRecheck()
            let unchanged = after.unchanged(since: before)
            let draftsPending = draftsBefore || !uncommittedNoteDraftPaths().isEmpty
            if result.kept, unchanged, !draftsPending {
                // `closeVault` detaches the writer after its guards, so
                // nothing retires; its tail opens the target.
                closeVault()
                return
            }
            if after.dirty.isEmpty, !draftsPending {
                guard unchanged else { continue }
                let close: @MainActor () -> Void = { [weak self] in self?.closeVault() }
                presentHotExitDiscardPending(
                    retry: { [weak self] in await self?.hotExitSettleClean() ?? true },
                    proceed: close,
                    keepCopies: close,
                    cancel: { [weak self] in self?.pendingVaultSwitchTarget = nil },
                    before: after)
                return
            }
            if unchanged, !result.kept || draftsPending,
                let reason = draftsPending ? hotExitReasonDraftsNotKept() : result.reason
            {
                let notKept = A11yEvent.unsavedNotesNotKept(reason: reason)
                announcer.post(notKept)
                showNotKeptInPrompt(notKept)
            }
            presentVaultSwitchPrompt()
            return
        }
    }

    // MARK: - Closes that asked nothing, or whose prompt resolved (§4, O5)

    /// A clean Close Vault, a clean switch or quit after its prompt, and
    /// Save All's or Save's close: settle the vault's obligations, re-check,
    /// then close. True when hot exit took the close over (the close follows
    /// from its task); false: close now, as today.
    func hotExitCloseAfterSettle(proceed: @escaping @MainActor () -> Void) -> Bool {
        guard let writer = hotExitWriter, !writer.isInert, !writer.detached else { return false }
        writer.reconcileNow()
        guard writer.hasUnresolvedObligations || hotExitAnyDirty else { return false }
        supersedeHotExitTerminationSettle()
        let before = hotExitRecheck()
        hotExitRouteGeneration &+= 1
        let generation = hotExitRouteGeneration
        hotExitRouteTask = Task { [weak self] in
            await self?.runHotExitCloseGate(
                generation, mode: .keep, before: before, proceed: proceed)
        }
        return true
    }

    /// Settle, re-check, decide: close on a kept, unchanged vault; back to the
    /// route's own prompt on a change; the discard-pending prompt when the
    /// store owes something it has not confirmed.
    func runHotExitCloseGate(
        _ generation: UInt64,
        mode: HotExitSettleMode,
        before: HotExitRecheck,
        proceed: @escaping @MainActor () -> Void
    ) async {
        guard let writer = hotExitWriter else {
            proceed()
            return
        }
        let result = await writer.settle(mode: mode)
        guard hotExitRouteGeneration == generation else { return }
        let after = hotExitRecheck()
        guard after.unchanged(since: before) else {
            hotExitReprompt(proceed: proceed)
            return
        }
        if result.kept {
            proceed()
            return
        }
        presentHotExitDiscardPending(
            retry: { [weak self] in
                guard let writer = self?.hotExitWriter else { return true }
                return await writer.settle(mode: mode).kept
            },
            proceed: proceed,
            keepCopies: proceed,
            cancel: { [weak self] in self?.cancelHotExitRoute() },
            before: before)
    }

    /// A settle with nothing dirty: kept once every obligation resolved.
    func hotExitSettleClean() async -> Bool {
        guard let writer = hotExitWriter else { return true }
        let before = hotExitRecheck()
        let result = await writer.settle(mode: .keep)
        return result.kept && hotExitRecheck().unchanged(since: before)
    }

    /// The re-check failed: the route goes back to its own decision, today's
    /// prompt with the new dirty count (owner call 3: text typed after the
    /// choice is asked about, never dropped).
    func hotExitReprompt(proceed: @escaping @MainActor () -> Void) {
        if isTerminationPending {
            switch presentTerminationPrompt() {
            case .presented: frontPromptHostForTermination()
            case .clean:
                if !hotExitCloseAfterSettle(proceed: proceed) { proceed() }
            case .refused: finishPendingTermination(proceed: false)
            }
        } else if pendingVaultSwitchTarget != nil {
            presentVaultSwitchPrompt()
        } else if hotExitAnyDirty {
            closeVaultFromUserAction()
        } else if !hotExitCloseAfterSettle(proceed: proceed) {
            proceed()
        }
    }

    /// Close and keep the copies after a Discard All the store has not
    /// confirmed: nothing is reverted, and the close speaks as a plain close
    /// (contract 38 D-10); a quit goes ahead, a switch opens its target.
    func hotExitCloseKeepingCopies() {
        if isTerminationPending {
            finishPendingTermination(proceed: true)
            return
        }
        pendingVaultClose = nil
        if case .closeVault? = pendingNavigation { pendingNavigation = nil }
        let completesVaultSwitch = pendingVaultSwitchTarget != nil
        if closeVault(), !completesVaultSwitch {
            announcer.post(.vaultClosed)
        }
    }

    /// Cancel on the discard-pending prompt: the vault stays open.
    func cancelHotExitRoute() {
        if isTerminationPending {
            finishPendingTermination(proceed: false)
        } else {
            pendingVaultSwitchTarget = nil
        }
    }

    // MARK: - Discard All (§4, O6)

    /// Close Vault's Discard All, and the fallback prompt's on quit and
    /// switch, through the store's fence: nothing is reverted until the
    /// discard commits. True when hot exit took it over; `perform` is the
    /// resolver's own discard-and-close, run once the fence holds (or the
    /// reader closes keeping the copies).
    func beginHotExitDiscardAll(
        dismiss: @MainActor () -> Void,
        perform: @escaping @MainActor () -> Void
    ) -> Bool {
        guard let writer = hotExitWriter, !writer.detached, writer.key != nil else { return false }
        supersedeHotExitTerminationSettle()
        dismiss()
        let before = hotExitRecheck()
        hotExitRouteGeneration &+= 1
        let generation = hotExitRouteGeneration
        hotExitRouteTask = Task { [weak self] in
            guard let self else { return }
            let wait = await writer.discardAll()
            guard self.hotExitRouteGeneration == generation else { return }
            if wait == .committed {
                await self.runHotExitCloseGate(
                    generation, mode: .retireOnly, before: before, proceed: perform)
                return
            }
            self.presentHotExitDiscardPending(
                retry: { [weak self] in
                    guard let writer = self?.hotExitWriter else { return true }
                    guard await writer.retryDiscard() == .committed else { return false }
                    return await writer.settle(mode: .retireOnly).kept
                },
                proceed: perform,
                // The discard did not commit: closing keeps the copies and
                // reverts nothing (red team PR 29.2).
                keepCopies: { [weak self] in self?.hotExitCloseKeepingCopies() },
                cancel: { [weak self] in self?.cancelHotExitRoute() },
                before: before)
        }
        return true
    }

    // MARK: - The discard-pending prompt (D-1)

    func presentHotExitDiscardPending(
        retry: @escaping @MainActor () async -> Bool,
        proceed: @escaping @MainActor () -> Void,
        keepCopies: @escaping @MainActor () -> Void,
        cancel: @escaping @MainActor () -> Void,
        before: HotExitRecheck
    ) {
        let choices = hotExitDiscardPendingChoices()
        hotExitDiscardPendingHandlers = HotExitDiscardPendingHandlers(
            retry: retry, proceed: proceed, keepCopies: keepCopies, cancel: cancel,
            before: before)
        pendingHotExitDiscard = HotExitDiscardPendingPrompt(
            message: a11yRender(event: .unsavedNotesNotDiscarded).text,
            tryAgain: choices.tryAgain,
            closeKeepingCopies: choices.closeKeepingCopies,
            cancel: choices.cancel)
        if isTerminationReplyOutstanding { frontPromptHostForTermination() }
    }

    func resolveHotExitDiscardPending(_ choice: HotExitDiscardPendingChoice) {
        guard let handlers = hotExitDiscardPendingHandlers else { return }
        pendingHotExitDiscard = nil
        hotExitDiscardPendingHandlers = nil
        switch choice {
        case .tryAgain:
            // Try again is one more settle, never a round. The alert is gone
            // while it waits, so a newer route or another vault can take
            // over; then this retry decides nothing (Codex adversarial
            // review, round 2).
            hotExitRouteGeneration &+= 1
            let generation = hotExitRouteGeneration
            let writer = hotExitWriter
            hotExitRouteTask = Task { [weak self] in
                guard let self else { return }
                let confirmed = await handlers.retry()
                guard self.hotExitRouteGeneration == generation, self.hotExitWriter === writer,
                    writer?.detached == false
                else { return }
                guard self.hotExitRecheck().unchanged(since: handlers.before) else {
                    self.hotExitReprompt(proceed: handlers.keepCopies)
                    return
                }
                if confirmed {
                    handlers.proceed()
                } else {
                    self.presentHotExitDiscardPending(
                        retry: handlers.retry, proceed: handlers.proceed,
                        keepCopies: handlers.keepCopies, cancel: handlers.cancel,
                        before: handlers.before)
                }
            }
        case .closeKeepingCopies:
            // A change since the choice goes back to the route's own prompt
            // (owner call 3), never closes over it.
            if hotExitRecheck().unchanged(since: handlers.before) {
                handlers.keepCopies()
            } else {
                hotExitReprompt(proceed: handlers.keepCopies)
            }
        case .cancel:
            handlers.cancel()
        }
    }

    // MARK: - Test seams

    /// Waits until the writer has nothing posted, debounced or in flight and
    /// the store host owes nothing for this vault (bounded).
    func settleHotExitForTesting(timeout: TimeInterval = 10) async {
        let deadline = Date().addingTimeInterval(timeout)
        while Date() < deadline {
            await HotExitWriter.frame()
            guard let writer = hotExitWriter else { return }
            if writer.isQuiescentForTests { return }
        }
    }
}

/// When the launch's vault-opened line can be heard: once the main thread
/// has kept time for `quiet` seconds (a launching app is busy, and VoiceOver
/// reads its window once it answers), or after `cap` seconds whatever
/// happens.
enum LaunchAnnouncementSettle {
    typealias Waiter = @MainActor (@escaping @MainActor () -> Void) -> AnyCancellable

    static let quiet: TimeInterval = 1.5
    static let tick: TimeInterval = 0.25
    static let late: TimeInterval = 0.1
    static let cap: TimeInterval = 10

    @MainActor static func production(_ body: @escaping @MainActor () -> Void) -> AnyCancellable {
        let watch = Watch(body: body)
        watch.schedule()
        return AnyCancellable { watch.cancelled = true }
    }

    @MainActor private final class Watch {
        let body: @MainActor () -> Void
        let start = Date()
        lazy var quietSince = start
        var cancelled = false

        init(body: @escaping @MainActor () -> Void) { self.body = body }

        func schedule() {
            let due = Date().addingTimeInterval(LaunchAnnouncementSettle.tick)
            DispatchQueue.main.asyncAfter(deadline: .now() + LaunchAnnouncementSettle.tick) {
                MainActor.assumeIsolated { self.check(due: due) }
            }
        }

        private func check(due: Date) {
            guard !cancelled else { return }
            let now = Date()
            if now.timeIntervalSince(due) > LaunchAnnouncementSettle.late { quietSince = now }
            if now.timeIntervalSince(quietSince) >= LaunchAnnouncementSettle.quiet
                || now.timeIntervalSince(start) >= LaunchAnnouncementSettle.cap
            {
                cancelled = true
                body()
            } else {
                schedule()
            }
        }
    }
}
