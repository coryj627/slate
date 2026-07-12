// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

/// Graph tab, Table mode (Milestone P, P1-2 #555): the whole vault
/// graph as a sortable, filterable grid — the global graph projected
/// accessibly, and (until P2 adds the diagram) the sole Graph-tab mode.
/// Backed by `graph_snapshot`, fetched once per generation and
/// sorted/filtered client-side.
extension AppState {
    /// Open (or activate the existing) Graph tab. Workspace-GLOBAL
    /// singleton: activate an existing `.graph` tab in ANY split group
    /// rather than opening a second (review round 1 finding 6 — the
    /// per-group `openTab` dedup alone let a split duplicate it).
    func openGraphTab() {
        if let existing = workspace.model.allTabs.first(where: { $0.item == .graph }) {
            activateTab(existing.id)
        } else {
            // Create the tab WITHOUT activating it, so the outgoing note
            // is still the active tab when `activateTab` runs its park —
            // opening-and-activating in one step would switch the active
            // tab to the graph first, and the park would then snapshot
            // the graph tab instead of the note, losing unsaved edits
            // (round 2 finding 1).
            let id = workspace.openTab(.graph, activate: false)
            activateTab(id)
        }
        graphAnnouncer.announce(.status("Graph."))
    }

    /// Activate a `.graph` tab: park the outgoing note buffer first so
    /// unsaved edits survive the switch (review round 1 finding 1 —
    /// `activateTab`'s markdown-guard early-returned for `.graph`,
    /// skipping the park and losing edits on return). Mirrors
    /// `activateCanvasTab`.
    func activateGraphTab(_ id: TabID) {
        if id == workspace.model.activeGroup.activeTabID, graphTableSnapshot != nil {
            return  // same-tab no-op (mirrors the canvas/markdown guard)
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
        loadGraphTable()
    }

    /// Clear graph-table state (vault open/close) so a snapshot or the
    /// seen-generation high-water mark from one vault never bleeds into
    /// the next.
    func resetGraphTableState() {
        graphTableSnapshot = nil
        graphTableError = nil
        graphTableLoading = false
        graphTableTextFilter = ""
        graphTableFilter = GraphFilter(
            includeAttachments: false, includeGhosts: true, orphansOnly: false)
        graphTableSeenGraphGeneration = 0
        graphTableLoadSeq += 1
        // Drop any queued filter/nav announcement so a stale count from
        // the closing vault can't fire into the next one (round 2
        // finding 8).
        graphAnnouncer.cancelPending()
    }

    /// What a completed load announces (on success, when the graph tab
    /// is active): `.summary` = the backend audio summary (deliberate
    /// open); `.filterCount` = the fresh "{k} of {n} shown" count AFTER a
    /// backend-filter re-fetch (round 2 finding 7 — never against the
    /// stale pre-fetch snapshot); `.silent` = nothing (background
    /// generation refresh on a possibly-parked view).
    enum GraphTableLoadAnnounce { case summary, filterCount, silent }

    /// (Re)fetch the whole-graph snapshot under the current backend
    /// filter. Compute-then-publish with the O-5 guards; the announcement
    /// (if any) is decided AFTER the fresh snapshot publishes and is
    /// gated on a live, on-screen graph tab.
    func loadGraphTable(announce: GraphTableLoadAnnounce = .summary) {
        guard let session = currentSession else {
            graphTableSnapshot = nil
            graphTableError = nil
            graphTableLoading = false
            return
        }
        graphTableLoadSeq += 1
        let seq = graphTableLoadSeq
        let filter = graphTableFilter
        graphTableLoading = true

        Task { [weak self] in
            let result: Result<GraphSnapshot, VaultError> =
                await Task.detached(priority: .userInitiated) {
                    do { return .success(try session.graphSnapshot(filter: filter)) }
                    catch let e as VaultError { return .failure(e) }
                    catch { return .failure(.Io(message: error.localizedDescription)) }
                }.value

            if let gate = self?.graphTablePublishGate { await gate() }

            guard let self else { return }
            guard !Task.isCancelled, self.currentSession === session,
                seq == self.graphTableLoadSeq
            else { return }

            self.graphTableLoading = false
            // Re-evaluate liveness HERE, after the async fetch — a load
            // that started while active must stay silent if the user has
            // since switched away.
            let speak = announce != .silent && self.graphTabActive
            switch result {
            case .success(let snap):
                self.graphTableSnapshot = snap
                self.graphTableError = nil
                self.graphTableSeenGraphGeneration = snap.generation
                guard speak else { return }
                switch announce {
                case .summary:
                    self.graphAnnouncer.announce(.summary(snap.audioSummary))
                case .filterCount:
                    self.graphAnnouncer.announceFilterCount(
                        self.graphFilterCountText(snap),
                        gate: { [weak self] in self?.graphTabActive == true })
                case .silent:
                    break
                }
            case .failure(let error):
                self.graphTableError = self.humanReadable(error)
                self.graphTableSnapshot = nil
                if speak {
                    self.graphAnnouncer.announce(
                        .error("Couldn't load the graph: \(self.humanReadable(error))"))
                }
            }
        }
    }

    /// "{k} of {n} shown" for `snap` under the current client-side text
    /// filter — the single source of truth the view's synchronous
    /// announcement and the post-fetch announcement both use, so the two
    /// paths can't drift (round 2 finding 7).
    func graphFilterCountText(_ snap: GraphSnapshot) -> String {
        let total = snap.nodes.count
        let needle = graphTableTextFilter.trimmingCharacters(in: .whitespaces)
        let shown =
            needle.isEmpty
            ? total
            : snap.nodes.filter {
                $0.label.range(of: needle, options: [.caseInsensitive, .diacriticInsensitive]) != nil
            }.count
        return "\(shown) of \(total) shown"
    }

    /// Change the backend filter (Attachments / Unresolved / Orphans)
    /// and re-fetch. The re-fetch is async, so the resulting count is
    /// announced when the fresh snapshot publishes — not now, against the
    /// stale one. The client-side text filter changes without a re-fetch
    /// (bound directly) and announces synchronously in the view.
    func setGraphTableFilter(_ filter: GraphFilter) {
        guard filter != graphTableFilter else { return }
        graphTableFilter = filter
        loadGraphTable(announce: .filterCount)
    }

    /// Re-probe `graph_generation()` after a `VaultEventListener` event
    /// and re-fetch the table only when the graph changed (P0-3 refresh
    /// contract) — mirrors `refreshConnectionsIfGraphChanged`.
    func refreshGraphTableIfGraphChanged() {
        guard let session = currentSession else { return }
        // Only refresh while a graph tab is actually on screen; once
        // every graph tab closes there's no consumer, and re-opening
        // re-fetches via `activateGraphTab` (finding 10). Keying on the
        // never-cleared `graphTableSnapshot` instead leaked a
        // forever-refresh after close.
        guard anyGraphTabVisible else { return }
        Task { [weak self] in
            let generation = await Task.detached(priority: .utility) {
                session.graphGeneration()
            }.value
            guard let self, self.currentSession === session else { return }
            guard generation != self.graphTableSeenGraphGeneration else { return }
            self.loadGraphTable(announce: .silent)
        }
    }

    /// The parent folder of a vault path (empty = vault root); the
    /// Folder column. Ghost nodes (no path) show empty.
    func folder(of path: String) -> String {
        (path as NSString).deletingLastPathComponent
    }

    /// Reveal a note in the file tree (the graph table's + Connections
    /// leaf's "Reveal in File Tree" row action, deferred from P1-1):
    /// expand every ancestor directory, select/open the file (the
    /// selection funnel scrolls the tree to it), and move focus to the
    /// tree region.
    func revealInFileTree(_ path: String) {
        // Expand each ancestor dir, most-specific last (recency order).
        var ancestors: [String] = []
        var dir = (path as NSString).deletingLastPathComponent
        while !dir.isEmpty {
            ancestors.append(dir)
            dir = (dir as NSString).deletingLastPathComponent
        }
        for a in ancestors.reversed() where !treeExpandedDirPaths.contains(a) {
            treeExpandedDirPaths.append(a)
        }
        openFile(path, target: .currentTab)
        workspace.focusTreeRegion()
    }
}
