// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

// `GraphPreset` is core's enum since W6-2 PR C (contracts doc §PR C, C-2):
// the generated Swift type replaces the local one — 0a-18's precedent for
// the mode enum — and the mapping and the headline are core's rules.

/// A table request: the complete input core answers (W6-2 PR 0b,
/// design A) — the visibility query and the sort.
struct GraphTableRequest: Equatable {
    var query: GraphVisibilityQuery
    var sort: GraphTableSort
}

/// A load token: the request and the sequence it was issued under. A
/// result publishes only when its token equals the current one in every
/// field AND its generation equals the held snapshot's (0b-2b).
struct GraphTableToken: Equatable {
    let request: GraphTableRequest
    let seq: UInt64
}

/// Graph tab, Table mode (Milestone P, P1-2 #555): the whole vault
/// graph as a sortable, filterable grid — the global graph projected
/// accessibly. Backed by `graph_snapshot` for the summary and the
/// selection, and by `graph_table_rows` for the rows core formats and
/// orders (W6-2 PR 0b, 0b-7); the host sorts and filters nothing.
extension AppState {
    /// Open (or activate the existing) Graph tab. Workspace-GLOBAL
    /// singleton: activate an existing `.graph` tab in ANY split group
    /// rather than opening a second (review round 1 finding 6 — the
    /// per-group `openTab` dedup alone let a split duplicate it).
    func openGraphTab(advancesSidebarSelectionRevision: Bool = true) {
        if let reason = propertyEditNavigationDisabledReason {
            postMutationAnnouncement(reason)
            return
        }
        if advancesSidebarSelectionRevision {
            recordExplicitSidebarNavigationIntent()
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
        graphAnnouncer.announce(.graphStatus(note: .opened))
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
        graphTableSnapshotFilter = nil
        graphTableRows = []
        graphTableTotal = 0
        graphTableSort = graphTableDefaultSort()
        graphTableRequestedSort = nil
        graphTableRequest = nil
        graphTableSeq += 1
        graphTableError = nil
        graphTableLoading = false
        graphTableTextFilter = ""
        graphTableKindFilter = nil
        graphTablePendingPreset = nil
        graphTableInFlightAnnounce = nil
        graphTablePublishedRequest = nil
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
    ///
    /// `sort` is the request's sort when a REPLACING load re-fetches under
    /// its own request (W6-2 PR C, C-2 (iii)); otherwise the accepted sort,
    /// or the default under a pending preset (design A).
    func loadGraphTable(announce: GraphTableLoadAnnounce = .summary, sort: GraphTableSort? = nil) {
        guard let session = currentSession else {
            graphTableSnapshot = nil
            graphTableError = nil
            graphTableLoading = false
            return
        }
        graphTableLoadSeq += 1
        let seq = graphTableLoadSeq
        let filter = graphTableFilter
        // A preset is a request like any other (design A): it sets the
        // DEFAULT sort in the same token as its filter, kind and needle.
        let token = issueGraphTableToken(
            sort: sort
                ?? (graphTablePendingPreset != nil
                    ? graphTableDefaultSort() : graphTableSort))
        graphTableLoading = true
        // (iv) The announce a replacing load inherits while this one is in
        // flight (contracts doc §PR C C-2 (iv); rule Q, Term Q9).
        graphTableInFlightAnnounce = announce
        let injectedFailure = graphTableLoadFailureForTests

        Task { [weak self] in
            // The snapshot (the summary, the selection's held generation)
            // and the rows core orders for the token's request (0b-7).
            let result: Result<(GraphSnapshot, GraphTableRows), VaultError> =
                await Task.detached(priority: .userInitiated) {
                    if let injectedFailure { return .failure(injectedFailure) }
                    do {
                        let snap = try session.graphSnapshot(filter: filter)
                        let rows = try session.graphTableRows(
                            query: token.request.query, sort: token.request.sort)
                        return .success((snap, rows))
                    } catch let e as VaultError {
                        return .failure(e)
                    } catch {
                        return .failure(.Io(message: error.localizedDescription))
                    }
                }.value

            if let gate = self?.graphTablePublishGate { await gate(token) }

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
            case .success(let (snap, rows)):
                // The snapshot and the filter it was fetched under publish
                // together — the authority's identity (design A).
                self.graphTableSnapshot = snap
                self.graphTableSnapshotFilter = filter
                self.graphTableError = nil
                self.graphTableSeenGraphGeneration = snap.generation
                // ONE publish (design A): rows, then the accepted sort. A
                // token that no longer matches drops the rows and keeps the
                // snapshot; the reissue is the newer token's load.
                let published = self.receiveGraphTableRows(token: token, result: rows)
                // (iv) This token published or dropped as the current one: the
                // in-flight announce is spent — unless the receiver issued a
                // replacing load, which recorded its own.
                if self.graphTableSeq == token.seq { self.graphTableInFlightAnnounce = nil }
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
                guard speak, published else { return }
                if let preset {
                    // The headline is THIS result's (design A): row zero under
                    // the default sort, or the published count.
                    self.graphAnnouncer.announce(self.graphPresetEvent(preset, rows: rows))
                    return
                }
                switch announce {
                case .summary:
                    // Typed over the counts the record carries (contracts doc 0a-7).
                    self.graphAnnouncer.announce(
                        .graphSnapshotSummary(counts: snap.summaryCounts))
                case .filterCount:
                    // The count is the rows result's (design B): one path.
                    self.graphAnnouncer.announceFilterCount(
                        shown: UInt32(rows.rows.count), total: UInt32(rows.total),
                        gate: { [weak self] in self?.graphTabActive == true })
                case .silent:
                    break
                }
            case .failure(let error):
                self.graphTableError = self.humanReadable(error)
                self.graphTableSnapshot = nil
                self.failGraphTableRows(token: token)
                // (ii) A failed pair forgets the preset as the success arm
                // does (contracts doc §PR C C-2 (ii); rule P, Term P4):
                // nothing remembers a headline a later refresh could replay.
                self.graphTablePendingPreset = nil
                self.graphTableInFlightAnnounce = nil
                if speak {
                    self.graphAnnouncer.announce(
                        .graphBlocked(reason: .loadFailed(message: self.humanReadable(error))))
                }
            }
        }
    }

    // MARK: - The load token (W6-2 PR 0b, design A)

    /// Issue the next token: every input change advances the sequence and
    /// records the request; `sort` is the requested sort until its rows
    /// publish.
    @discardableResult
    func issueGraphTableToken(sort: GraphTableSort) -> GraphTableToken {
        graphTableSeq += 1
        let request = GraphTableRequest(query: graphVisibilityQuery, sort: sort)
        graphTableRequest = request
        graphTableRequestedSort = sort == graphTableSort ? nil : sort
        return GraphTableToken(request: request, seq: graphTableSeq)
    }

    /// Publish a rows result under its token (design A): the token must
    /// equal the current one in EVERY field and the result's generation
    /// must equal the held snapshot's, else the result is dropped whole
    /// (0b-2b) — and, on a generation mismatch, the snapshot is re-fetched
    /// so the held value catches up. On success the rows and the accepted
    /// sort publish in ONE synchronous assignment, rows first. Returns
    /// whether the result published.
    @discardableResult
    func receiveGraphTableRows(token: GraphTableToken, result: GraphTableRows) -> Bool {
        guard token.seq == graphTableSeq, token.request == graphTableRequest else { return false }
        // The authority's identity (design A): the filter the held snapshot
        // was fetched under AND its generation. Filter-B rows never land
        // on a filter-A snapshot; a generation mismatch re-fetches.
        if let held = graphTableSnapshotFilter, held != token.request.query.filter {
            // (iii) A result under another backend filter re-fetches UNDER
            // ITS OWN REQUEST — sort included — instead of returning bare,
            // so a needle or a sort typed during a backend-changing pair
            // whose result lands first never leaves a snapshot with another
            // request's rows (contracts doc §PR C C-2 (iii); rule Q, Term
            // Q3's compatible-snapshot rule). The announce is the replaced
            // token's (iv).
            loadGraphTable(announce: graphTableInFlightAnnounce ?? .silent, sort: token.request.sort)
            return false
        }
        if let snap = graphTableSnapshot, snap.generation != result.generation {
            // The generation arm keeps the mac's sort rule (contracts doc
            // §PR C, C-D17) and inherits the announce (C-2 (iv)).
            graphTableRequestedSort = nil
            loadGraphTable(announce: graphTableInFlightAnnounce ?? .silent)
            return false
        }
        graphTableRows = result.rows
        graphTableTotal = result.total
        graphTableSort = token.request.sort
        graphTableRequestedSort = nil
        // The publication is CURRENT while this equals the newest request
        // (W6-2 PR C, C-8: the table's Where-am-I reads it).
        graphTablePublishedRequest = token.request
        return true
    }

    /// A failed query rolls the request back to the accepted state.
    func failGraphTableRows(token: GraphTableToken) {
        guard token.seq == graphTableSeq else { return }
        graphTableRequestedSort = nil
    }

    /// (i) The table's ONE observer on the composed query (contracts doc
    /// §PR C C-2 (i)): a VALUE rule, not a flag — the preset's token
    /// already carries the query it wrote, so the render pass after its
    /// field writes issues nothing whatever fields changed and in whatever
    /// order, while a needle typed later differs from the request in
    /// flight and issues its token.
    func requestGraphTableRowsIfQueryChanged() {
        guard graphTableRequest?.query != graphVisibilityQuery else { return }
        requestGraphTableRows()
    }

    /// The grid asked for a sort, or an input changed under the accepted
    /// sort: issue a token and query the rows; the receiver publishes rows
    /// and the accepted sort together or drops the result.
    func requestGraphTableRows(sort: GraphTableSort? = nil) {
        let token = issueGraphTableToken(sort: sort ?? graphTableSort)
        // A needle or a sort typed during a preset's pair replaces its
        // headline with the count (rule Q, Term Q4 — the order the pair's
        // continuation kept, now kept when a re-fetch drops that
        // continuation, C-2 (iii)); the rows request's own announce is the
        // count a replacing load inherits (C-2 (iv)).
        graphTablePendingPreset = nil
        graphTableInFlightAnnounce = .filterCount
        guard let session = currentSession else {
            failGraphTableRows(token: token)
            return
        }
        Task { [weak self] in
            let result: Result<GraphTableRows, VaultError> =
                await Task.detached(priority: .userInitiated) {
                    do {
                        return .success(
                            try session.graphTableRows(
                                query: token.request.query, sort: token.request.sort))
                    } catch let e as VaultError {
                        return .failure(e)
                    } catch {
                        return .failure(.Io(message: error.localizedDescription))
                    }
                }.value
            // The rows result's race-test seam, the pair's twin.
            if let gate = self?.graphTableRowsPublishGate { await gate(token) }
            guard let self, self.currentSession === session else { return }
            switch result {
            case .success(let rows):
                let published = self.receiveGraphTableRows(token: token, result: rows)
                if self.graphTableSeq == token.seq { self.graphTableInFlightAnnounce = nil }
                if published, self.graphTabActive {
                    self.graphAnnouncer.announceFilterCount(
                        shown: UInt32(rows.rows.count), total: UInt32(rows.total),
                        gate: { [weak self] in self?.graphTabActive == true })
                }
            case .failure(let error):
                self.failGraphTableRows(token: token)
                if token.seq == self.graphTableSeq { self.graphTableInFlightAnnounce = nil }
                if self.graphTabActive {
                    self.graphAnnouncer.announce(
                        .graphBlocked(reason: .loadFailed(message: self.humanReadable(error))))
                }
            }
        }
    }

    /// The grid's sort request (0b-14): a token change like any other.
    func setGraphTableSort(_ sort: GraphTableSort) {
        guard sort != graphTableSort || graphTableRequestedSort != nil else { return }
        requestGraphTableRows(sort: sort)
    }

    /// Drop the shared cross-projection selection if the node it names is
    /// no longer in `snap` — deleted, or dropped by a backend-filter change
    /// (P2-5 review finding 4). Keyed by core's `stableKey`, so it's
    /// robust across id-reassigning generation bumps. Called at the snapshot
    /// publish point (view-independent) AND from the Table's generation
    /// `onChange`, so a churn during Diagram mode doesn't strand a stale key.
    func revalidateGraphSelection(against snap: GraphSnapshot) {
        guard let key = graphSelectedNodeKey else { return }
        if !snap.nodes.contains(where: { $0.stableKey == key }) {
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

    // MARK: - Presets (P1-3 #556; the mapping and the headline are core's
    // rules since W6-2 PR C — `graph_preset_query`, `graph_preset_outcome`)

    /// Open/activate the Graph tab parameterized to a preset (P1-3 #556).
    /// The filter/kind are set BEFORE the tab's load so the first fetch is
    /// already correct, and the resting count/hub is announced once the
    /// fresh snapshot publishes (via `graphTablePendingPreset`).
    func openGraphPreset(
        _ preset: GraphPreset,
        advancesSidebarSelectionRevision: Bool = true
    ) {
        if let reason = propertyEditNavigationDisabledReason {
            postMutationAnnouncement(reason)
            return
        }
        if advancesSidebarSelectionRevision {
            recordExplicitSidebarNavigationIntent()
        }
        // The query is core's rule (W6-2 PR C, C-2): ONE record, written
        // field by field into the live state both projections read.
        let query = graphPresetQuery(preset: preset)
        graphTableTextFilter = query.nameQuery
        graphTableKindFilter = query.kindOnly
        graphTableFilter = query.filter
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

    /// The headline event for a preset, from THE PUBLISHED RESULT (P1-3;
    /// the copy is core's since W6-2 PR 0a; design A): orphans and
    /// unresolved carry the published count — the rows core returned for
    /// the preset's query, kind overlay included — and most-linked names
    /// row zero under the default sort the preset requested.
    func graphPresetEvent(_ preset: GraphPreset, rows: GraphTableRows) -> GraphA11yEvent {
        // The rule is core's (W6-2 PR C, C-2): one crossing per successful
        // publication of a current preset token.
        .graphPreset(
            outcome: graphPresetOutcome(
                preset: preset, shown: UInt64(rows.rows.count), first: rows.rows.first))
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
            // (iv) The replacing load inherits the announce of the load it
            // supersedes (contracts doc §PR C C-2 (iv); rule Q, Term Q9): a
            // preset's headline, an open's summary or a needle's count is
            // spoken over the NEWER generation, never lost to the refresh.
            self.loadGraphTable(announce: self.graphTableInFlightAnnounce ?? .silent)
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
