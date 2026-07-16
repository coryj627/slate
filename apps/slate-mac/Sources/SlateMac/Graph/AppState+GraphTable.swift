// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

/// A graph-table preset (P1-3 #556) — a named parameterization of the
/// table (backend filter + client kind filter + a spoken headline), not
/// a new surface. "One model, thin projections."
enum GraphPreset {
    /// Notes with no links in or out — `GraphFilter.orphansOnly`.
    case orphans
    /// Unresolved link targets only — ghosts visible, kind-filtered to
    /// `.ghost` (the backend filter can't drop notes).
    case unresolved
    /// The default view (ghosts visible), sorted Links-in desc — the
    /// grid's default sort surfaces hubs; the announcement names the top.
    case mostLinked
}

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
        if let reason = propertyEditNavigationDisabledReason {
            postMutationAnnouncement(reason)
            return
        }
        // Literal "opens/activates" (spec): a fresh tab shows the DEFAULT
        // view (transient filter/kind state is reset when a graph tab
        // closes — see `releaseGraphStateIfUnreferenced` — so a new tab
        // never inherits a stale preset), while an EXISTING tab is just
        // re-activated, preserving whatever view the user left it on
        // (round 3 round-2: openGraphTab must not itself half-reset the
        // filter — that left Orphans' backend filter installed).
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
        // Load the persisted graph config OBJECT (safety net — the eager
        // vault-open load normally already did this), then restore the
        // persisted backend + name filter (and clear any transient preset
        // kind filter) into the live Table state BEFORE the fetch — but
        // ONLY on a FRESH open (no cached snapshot), never a preset
        // activation. This is the fix for the once-per-vault load that left
        // the saved filter unrestored on close→reopen (P2-4 #560, review
        // finding 4), while still PRESERVING the view when merely switching
        // back to an already-loaded graph tab (`graphTableSnapshot != nil`)
        // — the "re-activating preserves, fresh open resets" contract.
        ensureGraphConfigLoaded()
        if graphTablePendingPreset == nil, graphTableSnapshot == nil {
            applyPersistedGraphFilter()
        }
        loadGraphTable()
    }

    /// When the (singleton) graph tab closes, reset its transient view
    /// state — the backend filter, the client kind filter, the pending
    /// preset, the text filter, and the cached snapshot (round 3 round-2
    /// finding). This is what makes a later plain "Open Graph" a clean
    /// DEFAULT view regardless of the preset the tab was last on (Orphans'
    /// `orphansOnly` and Unresolved's `.ghost` both clear here), while
    /// merely SWITCHING tabs preserves the view. Mirrors
    /// `releaseCanvasDocumentIfUnreferenced`.
    func releaseGraphStateIfUnreferenced(_ item: EditorItem?) {
        guard case .graph = item else { return }
        guard !workspace.model.allTabs.contains(where: { $0.item == .graph }) else { return }
        resetGraphTableState()
        resetGraphDiagramState()
    }

    /// Clear graph-table state (vault open/close, and graph-tab close) so
    /// a snapshot or the seen-generation high-water mark from one vault —
    /// or a stale preset filter — never bleeds into the next.
    func resetGraphTableState() {
        graphTableSnapshot = nil
        graphTableError = nil
        graphTableLoading = false
        graphTableTextFilter = ""
        graphTableKindFilter = nil
        graphTablePendingPreset = nil
        // Drop the shared cross-projection selection (P2-5 #561) so a key
        // from the closing vault / prior graph tab can't bleed into the next.
        graphSelectedNodeKey = nil
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
                // Drop a shared selection whose node is gone from the fresh
                // snapshot — at the PUBLISH point, so it fires even while the
                // Table view isn't mounted (e.g. a delete during Diagram
                // mode; P2-5 review finding 4).
                self.revalidateGraphSelection(against: snap)
                // A pending preset consumes THIS load's fresh snapshot for
                // its headline; clear it unconditionally so a later refresh
                // can't replay a stale preset announcement (P1-3 #556).
                let preset = self.graphTablePendingPreset
                self.graphTablePendingPreset = nil
                guard speak else { return }
                if let preset {
                    self.graphAnnouncer.announce(
                        .status(self.graphPresetAnnouncement(preset, snap: snap)))
                    return
                }
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

    /// Drop the shared cross-projection selection if the node it names is
    /// no longer in `snap` — deleted, or dropped by a backend-filter change
    /// (P2-5 review finding 4). Keyed by the stable `GraphNodeKey`, so it's
    /// robust across id-reassigning generation bumps. Called at the snapshot
    /// publish point (view-independent) AND from the Table's generation
    /// `onChange`, so a churn during Diagram mode doesn't strand a stale key.
    func revalidateGraphSelection(against snap: GraphSnapshot) {
        guard let key = graphSelectedNodeKey else { return }
        if !snap.nodes.contains(where: { GraphNodeKey.make(for: $0) == key }) {
            graphSelectedNodeKey = nil
        }
    }

    /// Change the backend filter (Attachments / Unresolved / Orphans)
    /// and re-fetch. The re-fetch is async, so the resulting count is
    /// announced when the fresh snapshot publishes — not now, against the
    /// stale one. The client-side text filter changes without a re-fetch
    /// (bound directly) and announces synchronously in the view.
    func setGraphTableFilter(_ filter: GraphFilter) {
        // A manual filter-bar toggle is the user overriding any preset: drop
        // the preset kind filter so it can't linger as hidden state the
        // toggles don't reflect, AND drop a still-pending preset headline so
        // this filter's re-fetch announces its OWN count, not a stale
        // preset's (round 3 finding 2 — the toggle-before-fetch race).
        graphTableKindFilter = nil
        graphTablePendingPreset = nil
        guard filter != graphTableFilter else { return }
        graphTableFilter = filter
        loadGraphTable(announce: .filterCount)
        // Persist the backend filter to graph.json (P2-4 #560). The
        // diagram rebuild on a filter change is driven by the container's
        // `onChange(of: graphTableFilter)`, so it's not repeated here.
        scheduleGraphConfigSave()
    }

    // MARK: - Presets (P1-3 #556)

    /// The backend `GraphFilter` a preset applies. Pure — `nonisolated`
    /// so tests can assert the mapping off the main actor.
    nonisolated static func graphPresetFilter(_ preset: GraphPreset) -> GraphFilter {
        switch preset {
        case .orphans:
            // Orphans-only; ghosts/attachments off (an orphan is a note
            // with no links either way).
            return GraphFilter(includeAttachments: false, includeGhosts: false, orphansOnly: true)
        case .unresolved:
            // Ghosts visible; the `.ghost` kind filter drops notes.
            return GraphFilter(includeAttachments: false, includeGhosts: true, orphansOnly: false)
        case .mostLinked:
            // The default view — hubs surface via the default Links-in
            // descending sort.
            return GraphFilter(includeAttachments: false, includeGhosts: true, orphansOnly: false)
        }
    }

    /// The client-side kind filter a preset applies (`.ghost` only for
    /// unresolved; the others show all kinds). Pure — `nonisolated`.
    nonisolated static func graphPresetKind(_ preset: GraphPreset) -> GraphNodeKind? {
        preset == .unresolved ? .ghost : nil
    }

    /// Open/activate the Graph tab parameterized to a preset (P1-3 #556).
    /// The filter/kind are set BEFORE the tab's load so the first fetch is
    /// already correct, and the resting count/hub is announced once the
    /// fresh snapshot publishes (via `graphTablePendingPreset`).
    func openGraphPreset(_ preset: GraphPreset) {
        if let reason = propertyEditNavigationDisabledReason {
            postMutationAnnouncement(reason)
            return
        }
        graphTableTextFilter = ""
        graphTableKindFilter = Self.graphPresetKind(preset)
        graphTableFilter = Self.graphPresetFilter(preset)
        graphTablePendingPreset = preset
        // Load EXACTLY once (round 3 finding 1): activating an off-screen
        // graph tab already runs `loadGraphTable` (via `activateGraphTab`)
        // with the filter set above; only the already-active same-tab case
        // (whose activation guard no-ops) needs an explicit load, so a
        // preset never starts two redundant snapshot fetches.
        if let existing = workspace.model.allTabs.first(where: { $0.item == .graph }) {
            if workspace.model.activeGroup.activeTabID == existing.id {
                loadGraphTable()  // already active → activateGraphTab would no-op
            } else {
                activateTab(existing.id)  // activateGraphTab loads with the preset filter
            }
        } else {
            activateTab(workspace.openTab(.graph, activate: false))  // loads on activation
        }
    }

    /// The spoken headline for a preset, computed from the fresh snapshot
    /// (P1-3 normative copy). Orphans/unresolved report the shown count;
    /// most-linked names the top row under the grid's default sort.
    func graphPresetAnnouncement(_ preset: GraphPreset, snap: GraphSnapshot) -> String {
        switch preset {
        case .orphans:
            return "\(graphPresetShownCount(snap, kind: nil)) orphaned notes."
        case .unresolved:
            return "\(graphPresetShownCount(snap, kind: .ghost)) unresolved targets."
        case .mostLinked:
            // The top row is what the grid shows at row 0 under the
            // default Links-in-descending sort (label/key tie-break) —
            // reuse the exact comparator so the spoken hub matches.
            let rows = snap.nodes.map { GraphTableRow(node: $0, folder: "") }
            guard
                let top = rows.sorted(by: {
                    GraphTableColumn.linksIn.directionalComparator($0, $1, ascending: false)
                }).first
            else { return "No notes to rank." }
            return "Most linked: \(top.label), \(top.linksIn) links in."
        }
    }

    /// Rows shown for a preset = the fetched nodes narrowed by the
    /// client-side kind filter (presets clear the text filter).
    private func graphPresetShownCount(_ snap: GraphSnapshot, kind: GraphNodeKind?) -> Int {
        guard let kind else { return snap.nodes.count }
        return snap.nodes.filter { $0.kind == kind }.count
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
        // Capture the load sequence at SCHEDULE time. Any close/reset or
        // load bumps it, so a probe that started before such an event is
        // stale by the time it resumes.
        let scheduledEpoch = graphTableLoadSeq
        Task { [weak self] in
            let generation = await Task.detached(priority: .utility) {
                session.graphGeneration()
            }.value
            guard let self, self.currentSession === session else { return }
            // The decision is re-evaluated HERE, after the await. The graph
            // tab can close — and then a preset can REOPEN — during the
            // `graphGeneration()` probe; a stray reload would either
            // re-populate the just-cleared snapshot with no consumer, or
            // supersede (and silence) the reopening preset's load. The
            // epoch guard rejects any probe whose lifecycle moved on
            // (P1-3 close-reset × P1-2 finding-10 refresh — the
            // close→reopen race the reviewer flagged).
            guard self.shouldRefreshGraphTable(
                probedGeneration: generation, scheduledEpoch: scheduledEpoch)
            else { return }
            self.loadGraphTable(announce: .silent)
        }
    }

    /// Whether a generation-refresh that has finished probing should
    /// proceed to reload: only if (1) the graph-table load sequence hasn't
    /// advanced since the probe was scheduled (no intervening close/reset
    /// or load — this rejects the close→reopen zombie), (2) a graph tab is
    /// still visible, and (3) the generation actually moved. Extracted so
    /// the race guards are unit-testable without racing the async task.
    func shouldRefreshGraphTable(probedGeneration: UInt64, scheduledEpoch: UInt64) -> Bool {
        graphTableLoadSeq == scheduledEpoch
            && anyGraphTabVisible
            && probedGeneration != graphTableSeenGraphGeneration
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
