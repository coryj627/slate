// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// P1-2 (#555): the `.graph` tab routes through the workspace, dedups
/// (singleton), loads its snapshot, and survives serialization — plus
/// the Bases O15 "Show connections" routing (Codoki's round-1 ask on
/// #892) — through a real AppState + FFI session, no mocks.
@MainActor
final class GraphTabRoutingTests: XCTestCase {
    /// The count the rows result carries (W6-2 PR 0b, design B): core's
    /// predicate over the snapshot's labels, as `graph_table_rows` answers it.
    private static func countUnderNeedle(_ snap: GraphSnapshot, _ needle: String) -> (shown: UInt32, total: UInt32) {
        let shown = snap.nodes.filter { graphLabelMatches(label: $0.label, query: needle) }.count
        return (UInt32(shown), UInt32(snap.nodes.count))
    }

    private var tempDir: URL!

    override func setUpWithError() throws {
        try super.setUpWithError()
        tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-graph-routing-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: tempDir, withIntermediateDirectories: true)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: tempDir)
        try super.tearDownWithError()
    }

    private func makeAppState() async throws -> AppState {
        let vault = tempDir.appendingPathComponent("vault-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        try Data("[[b]]".utf8).write(to: vault.appendingPathComponent("a.md"))
        try Data("[[a]]".utf8).write(to: vault.appendingPathComponent("b.md"))
        let store = RecentVaultsStore(
            fileURL: tempDir.appendingPathComponent("recents-\(UUID().uuidString).json"))
        let state = AppState(recentsStore: store, externalOpener: { _ in true })
        state.openVault(at: vault)
        await state.scanTask?.value
        return state
    }

    func testOpenGraphTabRoutesAndDedups() async throws {
        let state = try await makeAppState()

        state.openGraphTab()
        guard case .graph = state.workspace.activeTab?.item else {
            return XCTFail("active tab is not the graph: \(String(describing: state.workspace.activeTab))")
        }
        let tabCount = state.workspace.model.allTabs.filter { $0.item == .graph }.count
        XCTAssertEqual(tabCount, 1)

        // Singleton: opening again activates the SAME tab.
        state.openGraphTab()
        XCTAssertEqual(state.workspace.model.allTabs.filter { $0.item == .graph }.count, 1)
    }

    func testGraphTabLoadsSnapshot() async throws {
        let state = try await makeAppState()
        state.openGraphTab()
        // The load is async; wait for the published snapshot.
        try await pollUntil { state.graphTableSnapshot != nil }
        let snap = try XCTUnwrap(state.graphTableSnapshot)
        XCTAssertTrue(snap.nodes.contains { $0.label == "a" })
        XCTAssertTrue(snap.nodes.contains { $0.label == "b" })
    }

    func testGraphTabSurvivesSerializationRoundTrip() async throws {
        let state = try await makeAppState()
        state.openGraphTab()
        let snapshot = WorkspaceStore.snapshot(of: state.workspace.model)
        let data = try JSONEncoder().encode(snapshot)
        let decoded = try JSONDecoder().decode(WorkspaceStore.Snapshot.self, from: data)
        let rebuilt = try XCTUnwrap(WorkspaceStore.model(from: decoded))
        XCTAssertTrue(rebuilt.allTabs.contains { $0.item == .graph })
        XCTAssertTrue(rebuilt.validate().isEmpty)
    }

    /// The graph tab is a HARD workspace-global singleton: re-opening,
    /// splitting, and Duplicate Tab all refuse to create a second graph
    /// (round 1 finding 6, round 2 finding 6 — split/duplicate copied
    /// the item before).
    func testGraphTabIsHardSingleton() async throws {
        let state = try await makeAppState()
        state.openFile("a.md", target: .currentTab)
        state.openGraphTab()
        func graphCount() -> Int { state.workspace.model.allTabs.filter { $0.item == .graph }.count }
        XCTAssertEqual(graphCount(), 1)

        // Re-open: activates the existing one.
        state.openGraphTab()
        XCTAssertEqual(graphCount(), 1)

        // Split from the graph pane is refused (would duplicate the item).
        let panes = state.workspace.model.groupsInOrder.count
        state.splitActivePane(axis: .horizontal)
        XCTAssertEqual(graphCount(), 1, "split must not create a second graph")
        XCTAssertEqual(
            state.workspace.model.groupsInOrder.count, panes, "the split was refused")

        // Duplicate Tab (⌘T) from the graph is a no-op.
        state.newTab()
        XCTAssertEqual(graphCount(), 1, "Duplicate Tab must not create a second graph")
    }

    /// A restored snapshot that somehow holds two graph tabs is collapsed
    /// to one on rebuild (round 2 finding 6 completeness).
    func testRestoreCollapsesDuplicateGraphTabs() async throws {
        let state = try await makeAppState()
        state.openGraphTab()
        // Hand-build a snapshot with the graph tab duplicated in its group.
        var snapshot = WorkspaceStore.snapshot(of: state.workspace.model)
        if case .group(let id, _, var tabs) = snapshot.root, let graph = tabs.first {
            tabs.append(
                WorkspaceStore.Tab(id: UUID(), item: graph.item, mode: nil, propsCollapsed: nil))
            snapshot.root = .group(id: id, activeTab: graph.id, tabs: tabs)
        } else {
            return XCTFail("expected a single group holding the graph tab")
        }
        let rebuilt = try XCTUnwrap(WorkspaceStore.model(from: snapshot))
        XCTAssertEqual(
            rebuilt.allTabs.filter { $0.item == .graph }.count, 1,
            "restore keeps a single graph tab")
    }

    /// Opening the graph parks the outgoing note's unsaved buffer, so
    /// switching back restores the edits byte-identically (round 2
    /// finding 1 — the tab was activated before the park, losing edits).
    func testOpenGraphTabParksDirtyOutgoingNote() async throws {
        let state = try await makeAppState()
        state.openFile("a.md", target: .currentTab)
        await state.noteLoadTask?.value
        let noteTab = try XCTUnwrap(state.workspace.activeTab?.id)
        let dirty = "[[b]]\nunsaved edit ✏️"
        state.updateEditorText(dirty)
        XCTAssertTrue(state.hasUnsavedChanges)

        state.openGraphTab()
        guard case .graph = state.workspace.activeTab?.item else {
            return XCTFail("graph did not become active")
        }

        // Return to the note: its dirty buffer must have survived the park.
        state.activateTab(noteTab)
        await state.noteLoadTask?.value
        XCTAssertEqual(state.currentNoteText, dirty, "unsaved edits survived opening the graph")
        XCTAssertTrue(state.hasUnsavedChanges)
    }

    /// The synthetic graph tab is never addressable by file path — a
    /// real vault file couldn't be hijacked by (or hijack) it (round 2
    /// finding 5).
    func testGraphTabNotAddressableByPath() async throws {
        let state = try await makeAppState()
        state.openGraphTab()
        XCTAssertNil(state.workspace.activeGroupTab(forPath: "graph:singleton"))
        XCTAssertNil(state.workspace.activeGroupTab(forPath: "graph"))
    }

    /// The filter count is computed from the FRESH snapshot + the current
    /// text needle (round 2 finding 7), so a post-fetch announcement can't
    /// drift from the view's synchronous one. The tuple feeds the one
    /// gated entry; the copy is core's (W6-2 PR 0a).
    func testGraphFilterCountFollowsTheNeedle() async throws {
        let state = try await makeAppState()
        state.openGraphTab()
        try await pollUntil { state.graphTableSnapshot != nil }
        let snap = try XCTUnwrap(state.graphTableSnapshot)
        let total = snap.nodes.count
        state.graphTableTextFilter = ""
        let all = Self.countUnderNeedle(snap, state.graphTableTextFilter)
        XCTAssertEqual(all.shown, UInt32(total))
        XCTAssertEqual(all.total, UInt32(total))
        state.graphTableTextFilter = "a"
        let shown = snap.nodes.filter {
            $0.label.range(of: "a", options: [.caseInsensitive, .diacriticInsensitive]) != nil
        }.count
        let narrowed = Self.countUnderNeedle(snap, state.graphTableTextFilter)
        XCTAssertEqual(narrowed.shown, UInt32(shown))
        XCTAssertEqual(narrowed.total, UInt32(total))
    }

    /// The orphans preset (P1-3 #556) activates the graph tab with the
    /// orphans-only backend filter and no kind filter.
    func testOpenGraphPresetOrphans() async throws {
        let state = try await makeAppState()
        state.openGraphPreset(.orphans)
        guard case .graph = state.workspace.activeTab?.item else {
            return XCTFail("orphans preset did not activate the graph tab")
        }
        XCTAssertTrue(state.graphTableFilter.orphansOnly)
        XCTAssertNil(state.graphTableKindFilter)
        XCTAssertEqual(state.graphTableTextFilter, "")
        try await pollUntil { state.graphTableSnapshot != nil }
    }

    /// The unresolved preset sets the client `.ghost` kind filter (which
    /// GraphFilter can't express) with ghosts visible.
    func testOpenGraphPresetUnresolvedSetsGhostKindFilter() async throws {
        let state = try await makeAppState()
        state.openGraphPreset(.unresolved)
        XCTAssertEqual(state.graphTableKindFilter, .ghost)
        XCTAssertTrue(state.graphTableFilter.includeGhosts)
        XCTAssertFalse(state.graphTableFilter.orphansOnly)
        try await pollUntil { state.graphTableSnapshot != nil }
    }

    /// A manual filter-bar toggle drops the preset's hidden kind filter
    /// AND the pending preset headline, so the toggles never disagree with
    /// what's shown and the re-fetch announces its own count, not a stale
    /// preset's (P1-3; round 3 findings 2 & 4).
    func testManualFilterToggleClearsPresetStateAndDoesNotAnnounceStalePreset() async throws {
        let state = try await makeAppState()
        var posts: [String] = []
        state.graphAnnouncer = GraphAnnouncer(post: { text, _ in posts.append(text) })
        state.openGraphPreset(.unresolved)
        XCTAssertEqual(state.graphTableKindFilter, .ghost)
        XCTAssertNotNil(state.graphTablePendingPreset)
        // Toggle a filter before the preset fetch's headline could fire.
        state.setGraphTableFilter(
            GraphFilter(includeAttachments: true, includeGhosts: true, orphansOnly: false))
        XCTAssertNil(state.graphTableKindFilter, "manual toggle clears the preset kind filter")
        XCTAssertNil(state.graphTablePendingPreset, "manual toggle clears the pending preset")
        // Let every load settle, then flush the coalesced filter-count post.
        try await pollUntil { !state.graphTableLoading && state.graphTableSnapshot != nil }
        state.graphAnnouncer.flushForTests()
        // The stale preset headline ("… unresolved targets.") must NOT be
        // announced; the superseding load speaks its own count instead
        // (round 3 round-2: state clearing wasn't enough, prove the copy).
        XCTAssertFalse(
            posts.contains { $0.contains("unresolved targets") },
            "a superseded preset must not announce its stale headline: \(posts)")
        XCTAssertTrue(
            posts.contains { $0.contains("shown") },
            "the manual filter announces its own count: \(posts)")
    }

    /// A preset announces its headline exactly once, from the FRESH
    /// snapshot (not the generic summary), and a later refresh does NOT
    /// replay it (P1-3; round 3 finding 2 & the item-7 gap).
    func testPresetAnnouncesOnceFromFreshSnapshotNoReplay() async throws {
        let state = try await makeAppState()
        var posts: [String] = []
        state.graphAnnouncer = GraphAnnouncer(post: { text, _ in posts.append(text) })
        state.openGraphPreset(.orphans)
        try await pollUntil { state.graphTableSnapshot != nil && !state.graphTableLoading }
        let snap = try XCTUnwrap(state.graphTableSnapshot)
        let expected = a11yRender(
            event: .graph(
                event: state.graphPresetEvent(
                    .orphans, rows: GraphTableRows(generation: snap.generation, total: UInt64(snap.nodes.count), rows: [])))
        ).text
        XCTAssertEqual(
            posts.filter { $0 == expected }.count, 1,
            "preset headline announced exactly once from the fresh snapshot")
        XCTAssertFalse(
            posts.contains(snap.audioSummary),
            "the preset supersedes the generic summary announcement")
        XCTAssertNil(state.graphTablePendingPreset, "pending preset consumed")

        // A later refresh must NOT replay the headline.
        let seqBefore = state.graphTableLoadSeq
        state.loadGraphTable(announce: .silent)
        try await pollUntil {
            state.graphTableLoadSeq > seqBefore && !state.graphTableLoading
        }
        state.graphAnnouncer.flushForTests()
        XCTAssertEqual(
            posts.filter { $0 == expected }.count, 1,
            "a refresh after the preset must not replay the headline: \(posts)")
    }

    /// Closing the graph tab resets its transient view state — including
    /// the Orphans BACKEND filter, not just the kind filter — so a later
    /// plain "Open Graph" is a clean DEFAULT view (round 3 round-2: the
    /// Orphans counterexample the kind-only reset missed).
    func testGraphTabCloseResetsPresetViewStateIncludingBackendFilter() async throws {
        let state = try await makeAppState()
        state.openGraphPreset(.orphans)
        try await pollUntil { state.graphTableSnapshot != nil }
        XCTAssertTrue(state.graphTableFilter.orphansOnly)
        let graphID = try XCTUnwrap(state.workspace.activeTab?.id)
        state.requestCloseTab(graphID)
        // Close resets EVERYTHING back to the default view.
        XCTAssertFalse(state.graphTableFilter.orphansOnly, "close resets the Orphans backend filter")
        XCTAssertTrue(state.graphTableFilter.includeGhosts)
        XCTAssertFalse(state.graphTableFilter.includeAttachments)
        XCTAssertNil(state.graphTableKindFilter)
        XCTAssertNil(state.graphTablePendingPreset)
        XCTAssertNil(state.graphTableSnapshot)

        // A subsequent plain Open Graph is the default view.
        state.openGraphTab()
        guard case .graph = state.workspace.activeTab?.item else {
            return XCTFail("Open Graph did not activate the graph tab")
        }
        XCTAssertFalse(state.graphTableFilter.orphansOnly)
        XCTAssertNil(state.graphTableKindFilter)
        try await pollUntil { state.graphTableSnapshot != nil }
    }

    /// The PERSISTED filter is restored on every plain reopen, not just
    /// reset to the default — the fix for the once-per-vault load that left
    /// the saved filter unrestored after the first open (P2-4 review
    /// finding 4). A transient preset must NOT become the persisted filter.
    func testPersistedFilterIsRestoredOnPlainReopen() async throws {
        let state = try await makeAppState()
        state.openGraphTab()
        try await pollUntil { state.graphTableSnapshot != nil }
        // Persist a NON-default backend filter (this writes graphConfig
        // synchronously before the debounced disk write).
        state.setGraphTableFilter(
            GraphFilter(includeAttachments: true, includeGhosts: true, orphansOnly: false))
        XCTAssertTrue(state.graphConfig.filters.includeAttachments, "persisted into graphConfig")

        // Open a transient preset, then close: the preset must not have
        // overwritten the persisted filter.
        state.openGraphPreset(.orphans)
        try await pollUntil { state.graphTableSnapshot != nil }
        let graphID = try XCTUnwrap(state.workspace.activeTab?.id)
        state.requestCloseTab(graphID)
        XCTAssertFalse(state.graphTableFilter.includeAttachments, "close resets the live filter")

        // A plain reopen restores the PERSISTED filter (Attachments on),
        // not the default and not the orphans preset.
        state.openGraphTab()
        XCTAssertTrue(
            state.graphTableFilter.includeAttachments,
            "plain reopen restores the persisted Attachments filter")
        XCTAssertFalse(state.graphTableFilter.orphansOnly, "the transient preset did not persist")
        XCTAssertTrue(state.graphConfig.filters.includeAttachments)
    }

    /// Re-rooting the Connections leaf writes the SHARED cross-projection
    /// selection key (P2-5 #561), so the Table/Diagram reflect that node.
    /// Closing the graph tab clears it (no bleed into the next vault/tab).
    func testReRootConnectionsWritesSharedSelectionAndCloseClearsIt() async throws {
        let state = try await makeAppState()
        state.reRootConnections(on: "a.md")
        XCTAssertEqual(
            state.graphSelectedNodeKey, "p:a.md", "re-root writes the shared 'p:' key")

        state.openGraphTab()
        try await pollUntil { state.graphTableSnapshot != nil }
        let graphID = try XCTUnwrap(state.workspace.activeTab?.id)
        state.requestCloseTab(graphID)
        XCTAssertNil(state.graphSelectedNodeKey, "closing the graph tab clears the shared selection")
    }

    /// The shared selection is revalidated at the snapshot PUBLISH point
    /// (view-independent), so a node that vanishes while Diagram mode is
    /// active doesn't strand a stale key (P2-5 review finding 4).
    func testSharedSelectionRevalidatesAgainstSnapshot() async throws {
        let state = try await makeAppState()
        state.openGraphTab()
        try await pollUntil { state.graphTableSnapshot != nil }
        let snap = try XCTUnwrap(state.graphTableSnapshot)
        let real = try XCTUnwrap(snap.nodes.first { $0.path != nil })
        state.graphSelectedNodeKey = real.stableKey
        state.revalidateGraphSelection(against: snap)
        XCTAssertEqual(
            state.graphSelectedNodeKey, real.stableKey,
            "a node present in the snapshot keeps its selection")
        state.graphSelectedNodeKey = "p:/vanished-note.md"
        state.revalidateGraphSelection(against: snap)
        XCTAssertNil(state.graphSelectedNodeKey, "a node absent from the snapshot is deselected")
    }

    /// Connections back-navigation moves the shared selection to the
    /// RESTORED node, so the Table/Diagram follow the leaf back rather than
    /// lingering on the forward destination (P2-5 review finding 5).
    func testConnectionsBackWritesSharedSelection() async throws {
        let state = try await makeAppState()  // vault: a.md ↔ b.md
        state.reRootConnections(on: "a.md")
        state.reRootConnections(on: "b.md")
        XCTAssertEqual(state.graphSelectedNodeKey, "p:b.md")
        XCTAssertTrue(state.connectionsBack())
        XCTAssertEqual(
            state.graphSelectedNodeKey, "p:a.md", "back moves the shared selection to the prior node")
    }

    /// A generation-refresh probe that resumes after its graph-table
    /// lifecycle moved on must not reload — covers both "closed and stayed
    /// closed" and the "close → quick preset reopen" zombie that a bare
    /// visibility check would miss (round 3 round-3, the reviewer's races).
    func testRefreshProbeGatedOutWhenLifecycleAdvanced() async throws {
        let state = try await makeAppState()
        state.openGraphTab()
        try await pollUntil { state.graphTableSnapshot != nil }
        let gen = state.graphTableSeenGraphGeneration
        let epoch = state.graphTableLoadSeq
        // Visible, same epoch, a moved generation ⇒ would reload.
        XCTAssertTrue(state.shouldRefreshGraphTable(probedGeneration: gen + 1, scheduledEpoch: epoch))

        // (a) Close and stay closed: not visible ⇒ no reload.
        let graphID = try XCTUnwrap(state.workspace.activeTab?.id)
        state.requestCloseTab(graphID)
        XCTAssertFalse(
            state.shouldRefreshGraphTable(probedGeneration: gen + 1, scheduledEpoch: epoch),
            "a probe whose tab closed mid-probe must not reload")

        // (b) Close → quick preset reopen: a graph tab is visible again,
        // but the epoch advanced, so the STALE pre-close probe still bails
        // (it must not supersede/silence the preset's load).
        state.openGraphPreset(.orphans)
        try await pollUntil { state.graphTableSnapshot != nil }
        XCTAssertTrue(state.anyGraphTabVisible, "the preset reopened a visible graph tab")
        XCTAssertNotEqual(state.graphTableLoadSeq, epoch, "the lifecycle epoch advanced")
        XCTAssertFalse(
            state.shouldRefreshGraphTable(probedGeneration: gen + 1, scheduledEpoch: epoch),
            "a pre-close probe must not fire after a close→reopen (epoch mismatch)")
    }

    /// The kind filter also clears on close (Unresolved counterexample),
    /// so Unresolved → close → Open Graph is not a hidden ghost-only grid.
    func testUnresolvedKindFilterClearsOnClose() async throws {
        let state = try await makeAppState()
        state.openGraphPreset(.unresolved)
        XCTAssertEqual(state.graphTableKindFilter, .ghost)
        let graphID = try XCTUnwrap(state.workspace.activeTab?.id)
        state.requestCloseTab(graphID)
        XCTAssertNil(state.graphTableKindFilter, "close clears the hidden ghost kind filter")
    }

    /// Once every graph tab closes, the generation-driven refresh gates
    /// off (`anyGraphTabVisible` — review round 1 finding 10, the load-
    /// bearing guard). P1-3 additionally resets the transient view state
    /// on close (`releaseGraphStateIfUnreferenced`), so the snapshot is
    /// now cleared too — cleaner than the previously-retained stale
    /// snapshot, and the refresh gate is unaffected either way.
    func testRefreshGateClosesWithLastGraphTab() async throws {
        let state = try await makeAppState()
        state.openFile("a.md", target: .currentTab)
        state.openGraphTab()
        try await pollUntil { state.graphTableSnapshot != nil }
        XCTAssertTrue(state.anyGraphTabVisible)
        let graphID = try XCTUnwrap(state.workspace.activeTab?.id)
        state.requestCloseTab(graphID)
        XCTAssertFalse(state.anyGraphTabVisible, "refresh gate is off once the graph tab is gone")
        XCTAssertNil(state.graphTableSnapshot, "close resets the transient graph-table state (P1-3)")
    }

    /// Codoki round-1 (#892): the Bases "Show connections" row action
    /// re-roots the Connections leaf on the row's note.
    func testBasesShowConnectionsReRootsLeaf() async throws {
        let state = try await makeAppState()
        state.basesShowConnections(
            for: BasesRow(filePath: "b.md", taskOrdinal: nil, values: [], audioDescription: "b"))
        XCTAssertEqual(state.workspace.activeLeaf, .connections)
        XCTAssertEqual(state.connectionsRootPath, "b.md")
        XCTAssertEqual(state.connectionsEffectivePath, "b.md")
    }

    // MARK: - W6-2 PR C: the mac lane of C-2 (i)–(iv) and C-8 (contracts doc §PR C)

    /// A one-shot hold at a publish gate (CloseSaveAuthoringRaceTests'
    /// shape): `suspend()` parks the caller until `release()`.
    private actor AsyncGate {
        private var entered = false
        private var released = false
        private var entranceWaiters: [CheckedContinuation<Void, Never>] = []
        private var releaseWaiter: CheckedContinuation<Void, Never>?

        func suspend() async {
            guard !released else { return }
            entered = true
            for waiter in entranceWaiters { waiter.resume() }
            entranceWaiters = []
            await withCheckedContinuation { releaseWaiter = $0 }
        }

        func waitUntilEntered() async {
            guard !entered else { return }
            await withCheckedContinuation { entranceWaiters.append($0) }
        }

        func release() {
            released = true
            releaseWaiter?.resume()
            releaseWaiter = nil
        }
    }

    /// A graph tab open and quiescent: the snapshot held, no load in
    /// flight, the shown rows the newest request's.
    private func openQuiescentGraph(_ state: AppState) async throws {
        state.openGraphTab()
        try await pollUntil {
            state.graphTableSnapshot != nil && !state.graphTableLoading
                && state.graphTablePublishedRequest == state.graphTableRequest
        }
    }

    /// C-2 (i): after a preset's own field writes the ONE composed-query
    /// observer issues nothing (a value rule: the token in flight already
    /// carries the query it wrote), so the pair publishes the headline —
    /// exactly one line, no summary, no count — from a table that had a
    /// needle and a kind overlay before it.
    func testPresetFromTheActiveTableWithANeedleAndAKindChangeSpeaksTheHeadlineAlone() async throws {
        let state = try await makeAppState()
        try await openQuiescentGraph(state)
        // A needle and a kind overlay typed on the live table, each a token
        // the observer issues because the query differs from the request's.
        state.graphTableTextFilter = "a"
        state.requestGraphTableRowsIfQueryChanged()
        state.graphTableKindFilter = .ghost
        state.requestGraphTableRowsIfQueryChanged()
        try await pollUntil { state.graphTablePublishedRequest == state.graphTableRequest }
        var posts: [String] = []
        state.graphAnnouncer = GraphAnnouncer(post: { text, _ in posts.append(text) })

        state.openGraphPreset(.mostLinked)
        let seqAfterPreset = state.graphTableSeq
        // The render pass after the preset's writes: the observer sees the
        // query the token already carries and issues nothing.
        state.requestGraphTableRowsIfQueryChanged()
        XCTAssertEqual(state.graphTableSeq, seqAfterPreset, "the value rule: no token for the preset's own writes")
        try await pollUntil { !state.graphTableLoading && state.graphTablePendingPreset == nil }
        state.graphAnnouncer.flushForTests()
        XCTAssertEqual(posts.count, 1, "the headline alone: \(posts)")
        XCTAssertTrue(posts.first?.hasPrefix("Most linked:") == true, "\(posts)")
    }

    /// C-2 (i) from Diagram mode: no table observer is mounted and the
    /// preset's pair still publishes the headline alone.
    func testPresetFromDiagramModeSpeaksTheHeadlineAlone() async throws {
        let state = try await makeAppState()
        try await openQuiescentGraph(state)
        state.setGraphMode(.diagram)
        var posts: [String] = []
        state.graphAnnouncer = GraphAnnouncer(post: { text, _ in posts.append(text) })
        state.openGraphPreset(.orphans)
        state.requestGraphTableRowsIfQueryChanged()
        try await pollUntil { !state.graphTableLoading && state.graphTablePendingPreset == nil }
        state.graphAnnouncer.flushForTests()
        XCTAssertEqual(posts, ["0 orphaned notes."], "the headline alone")
    }

    /// C-2 (ii): a failed pair speaks the block and forgets the preset —
    /// nothing remembers a headline a later refresh could replay.
    func testAFailedPresetLeavesNoPendingHeadline() async throws {
        let state = try await makeAppState()
        try await openQuiescentGraph(state)
        var posts: [String] = []
        state.graphAnnouncer = GraphAnnouncer(post: { text, _ in posts.append(text) })
        state.graphTableLoadFailureForTests = .Io(message: "disk gone")
        state.openGraphPreset(.orphans)
        XCTAssertNotNil(state.graphTablePendingPreset, "armed for the pair")
        try await pollUntil { !state.graphTableLoading }
        state.graphTableLoadFailureForTests = nil
        XCTAssertNil(state.graphTablePendingPreset, "the failure arm forgets the preset")
        XCTAssertNil(state.graphTableInFlightAnnounce, "nothing in flight")
        XCTAssertNotNil(state.graphTableError)
        // A silent refresh after the failure replays nothing.
        state.loadGraphTable(announce: .silent)
        try await pollUntil { !state.graphTableLoading && state.graphTableSnapshot != nil }
        state.graphAnnouncer.flushForTests()
        XCTAssertEqual(posts.count, 1, "the block alone, no headline: \(posts)")
        XCTAssertTrue(posts.first?.hasPrefix("Couldn't load the graph") == true, "\(posts)")
    }

    /// IPG-1 (rule Q, Term Q2): a superseded PAIR's failure belongs to
    /// nobody. The pair's own guard is the LOAD sequence, which a rows
    /// request does not advance, so a failing pair released after a newer
    /// needle published used to wipe the snapshot, install its error over
    /// the rows on screen, spend the newer lineage's in-flight announce and
    /// speak a block for a request nobody made. The Windows twin returns at
    /// the same guard (`GraphDocumentViewModel.Receive`).
    func testASupersededPairsFailureLeavesTheNewerRowsAlone() async throws {
        let state = try await makeAppState()
        try await openQuiescentGraph(state)
        var posts: [String] = []
        state.graphAnnouncer = GraphAnnouncer(post: { text, _ in posts.append(text) })
        let gate = AsyncGate()
        // A: a pair that will fail, parked before it publishes.
        state.graphTableLoadFailureForTests = .Io(message: "disk gone")
        state.graphTablePublishGate = { _ in await gate.suspend() }
        state.loadGraphTable(announce: .summary)
        let pairSeq = state.graphTableSeq
        state.graphTableLoadFailureForTests = nil
        await gate.waitUntilEntered()
        // B: a needle, issued and published while A waits.
        state.graphTablePublishGate = nil
        state.graphTableTextFilter = "a"
        state.requestGraphTableRowsIfQueryChanged()
        XCTAssertNotEqual(state.graphTableSeq, pairSeq, "the needle issued its own token")
        try await pollUntil { state.graphTablePublishedRequest?.query.nameQuery == "a" }
        let published = try XCTUnwrap(state.graphTablePublishedRequest)
        let heldRows = state.graphTableRows.count
        // A is released: it is not the lineage's any more.
        await gate.release()
        try await pollUntil { !state.graphTableLoading }
        state.graphAnnouncer.flushForTests()
        XCTAssertNil(state.graphTableError, "the stale pair installed its error")
        XCTAssertNotNil(state.graphTableSnapshot, "the stale pair wiped the snapshot")
        XCTAssertEqual(state.graphTablePublishedRequest, published, "the needle's publication stands")
        XCTAssertEqual(state.graphTableRows.count, heldRows, "the needle's rows stand")
        XCTAssertFalse(
            posts.contains { $0.hasPrefix("Couldn't load the graph") },
            "a superseded pair spoke its failure: \(posts)")
    }

    /// IPG-4 (rule Q, Term Q2): the same for a superseded ROWS failure. The
    /// rollback was already guarded, so it changed no state — and announced
    /// a load failure for a request the newer token had replaced. The
    /// success arm next to it speaks only what published.
    func testASupersededRowsFailureSaysNothing() async throws {
        let state = try await makeAppState()
        try await openQuiescentGraph(state)
        var posts: [String] = []
        state.graphAnnouncer = GraphAnnouncer(post: { text, _ in posts.append(text) })
        let gate = AsyncGate()
        // A: a rows request that will fail, parked before it publishes.
        state.graphTableRowsFailureForTests = .Io(message: "disk gone")
        state.graphTableTextFilter = "a"
        state.graphTableRowsPublishGate = { _ in await gate.suspend() }
        state.requestGraphTableRowsIfQueryChanged()
        let failingSeq = state.graphTableSeq
        state.graphTableRowsFailureForTests = nil
        await gate.waitUntilEntered()
        // B: a second needle, issued behind it.
        state.graphTableRowsPublishGate = nil
        state.graphTableTextFilter = "ab"
        state.requestGraphTableRowsIfQueryChanged()
        XCTAssertNotEqual(state.graphTableSeq, failingSeq, "the second needle issued its own token")
        try await pollUntil { state.graphTablePublishedRequest?.query.nameQuery == "ab" }
        await gate.release()
        try await pollUntil { state.graphTablePublishedRequest == state.graphTableRequest }
        state.graphAnnouncer.flushForTests()
        XCTAssertNil(state.graphTableError)
        XCTAssertEqual(state.graphTablePublishedRequest?.query.nameQuery, "ab")
        XCTAssertFalse(
            posts.contains { $0.hasPrefix("Couldn't load the graph") },
            "a superseded rows failure spoke: \(posts)")
    }

    /// IPG-9 (rule Q, Terms Q2/Q7; C-2 (iii)): a pair superseded by a ROWS
    /// request installs its snapshot only while those rows are UNANSWERED.
    /// The rule is read off `pairResultInstalls` rather than driven end to
    /// end: the interleaving needs the vault to move while the pair is still
    /// to fetch, and a moving vault wakes the event listener's own refresh,
    /// whose load supersedes the pair before it can arrive — the same reason
    /// `shouldRefreshGraphTable` is extracted.
    func testASupersededPairInstallsOnlyWhileTheNewerRequestIsUnanswered() async throws {
        let state = try await makeAppState()
        try await openQuiescentGraph(state)
        let answered = try XCTUnwrap(state.graphTableRequest)
        // The CURRENT token installs, as every quiescent publication does.
        XCTAssertTrue(
            state.pairResultInstalls(
                token: GraphTableToken(request: answered, seq: state.graphTableSeq)))
        let stale = GraphTableToken(request: answered, seq: state.graphTableSeq)

        // A needle issued and PARKED: the newer request is unanswered, so the
        // superseded pair may still install — C-2 (iii)'s pair-first order.
        let gate = AsyncGate()
        state.graphTableRowsPublishGate = { _ in await gate.suspend() }
        state.graphTableTextFilter = "a"
        state.requestGraphTableRowsIfQueryChanged()
        await gate.waitUntilEntered()
        XCTAssertNotEqual(stale.seq, state.graphTableSeq, "the needle issued its own token")
        XCTAssertNotEqual(
            state.graphTablePublishedRequest, state.graphTableRequest,
            "the needle has not published while it is parked")
        XCTAssertTrue(state.pairResultInstalls(token: stale), "pair-first must still install")

        // Released and published: the same superseded pair now installs
        // nothing, so the seen mark stays where the probe can repair from.
        await gate.release()
        state.graphTableRowsPublishGate = nil
        try await pollUntil { state.graphTablePublishedRequest == state.graphTableRequest }
        XCTAssertFalse(
            state.pairResultInstalls(token: stale),
            "a superseded pair installed over an answered request")

        // ANSWERED covers a FAILURE too (IPG-29): a rows request that failed
        // never publishes, so `graphTablePublishedRequest` stays behind the
        // current request for ever — which read as "still pending" and let a
        // superseded pair install its authority over the rows the failure had
        // left standing.
        let answeredAfterSuccess = state.graphTableAnsweredSeq
        state.graphTableRowsFailureForTests = .Io(message: "disk gone")
        state.graphTableTextFilter = "ab"
        state.requestGraphTableRowsIfQueryChanged()
        try await pollUntil { state.graphTableAnsweredSeq != answeredAfterSuccess }
        state.graphTableRowsFailureForTests = nil
        XCTAssertNotEqual(
            state.graphTablePublishedRequest, state.graphTableRequest,
            "the failed request never published, which is the trap")
        XCTAssertEqual(state.graphTableAnsweredSeq, state.graphTableSeq, "the failure answered it")
        XCTAssertFalse(
            state.pairResultInstalls(token: stale),
            "a superseded pair installed after the newer request FAILED")
    }

    /// IPG-31's reachable half: a CURRENT rows publish answers the error a
    /// failed pair installed. The mac issues rows only for every needle —
    /// that is C-2 (iii)'s landed design, and the ISSUE-side kind rule codex
    /// asked for is the Windows document's Term Q3, not the mac's — but the
    /// rows arm never cleared `graphTableError`, so a needle typed after a
    /// failure published its rows underneath the failure view.
    func testANeedleAfterAFailedPairClearsTheErrorItPublishesUnder() async throws {
        let state = try await makeAppState()
        try await openQuiescentGraph(state)
        state.graphTableLoadFailureForTests = .Io(message: "disk gone")
        state.loadGraphTable(announce: .summary)
        try await pollUntil { !state.graphTableLoading }
        state.graphTableLoadFailureForTests = nil
        XCTAssertNil(state.graphTableSnapshot, "the failed pair cleared the authority")
        XCTAssertNotNil(state.graphTableError)

        var posts: [String] = []
        state.graphAnnouncer = GraphAnnouncer(post: { text, _ in posts.append(text) })
        state.graphTableTextFilter = "a"
        state.requestGraphTableRowsIfQueryChanged()
        try await pollUntil { state.graphTablePublishedRequest == state.graphTableRequest }
        // The needle's rows are current, and they ANSWER the error the failed
        // pair installed — it used to stand over them, so the reader kept a
        // failure view over a successful request.
        XCTAssertNil(state.graphTableError, "a current rows publish left the old error standing")
        XCTAssertEqual(state.graphTableRequest?.query.nameQuery, "a")
        // …and the authority comes BACK (IPG-36, created by TGC-17): a
        // visible table whose Where-am-I can never answer is worse than the
        // error it replaced, so the publish asks for a snapshot and carries
        // the needle's count to that final publication.
        try await pollUntil { state.graphTableSnapshot != nil && !state.graphTableLoading }
        XCTAssertNotNil(
            state.graphDiagramWhereAmIEvent(),
            "the rows published with no authority and none was asked for")
        state.graphAnnouncer.flushForTests()
        let expected = a11yRender(
            event: .graph(
                event: .graphFilterCount(
                    shown: UInt32(state.graphTableRows.count), total: UInt32(state.graphTableTotal))))
        XCTAssertEqual(posts, [expected.text], "recovery speaks the current count exactly once")
    }

    /// IPG-38 (rule Q, Term Q2): the rows ROLLBACK belongs to the current
    /// token in EVERY field. It guarded the sequence alone, so a token whose
    /// sequence matched but whose request did not cleared the pending sort
    /// and marked the sequence answered before the caller could turn it away.
    func testAForeignRequestAtTheCurrentSequenceRollsBackNothing() async throws {
        let state = try await makeAppState()
        try await openQuiescentGraph(state)
        let byNote = GraphTableSort(column: .note, ascending: true)
        state.graphTableRequestedSort = byNote
        let answered = state.graphTableAnsweredSeq
        let current = try XCTUnwrap(state.graphTableRequest)
        let foreign = GraphTableRequest(
            query: GraphVisibilityQuery(
                filter: current.query.filter, nameQuery: "not-this-one", kindOnly: nil),
            sort: current.sort)
        state.failGraphTableRows(
            token: GraphTableToken(request: foreign, seq: state.graphTableSeq))
        XCTAssertEqual(state.graphTableRequestedSort, byNote, "a foreign request rolled the sort back")
        XCTAssertEqual(state.graphTableAnsweredSeq, answered, "a foreign request answered the sequence")
    }

    /// IPG-33 (rule Q, Term Q5 (c)): asking for the ACCEPTED sort back while
    /// another is pending cancels it "at issue with no line". The rows path
    /// speaks its count on every publish, so the cancellation spoke one too.
    func testCancellingAPendingSortSaysNothing() async throws {
        let state = try await makeAppState()
        try await openQuiescentGraph(state)
        var posts: [String] = []
        state.graphAnnouncer = GraphAnnouncer(post: { text, _ in posts.append(text) })
        let accepted = state.graphTableSort
        let byNote = GraphTableSort(column: .note, ascending: true)
        XCTAssertNotEqual(byNote, accepted)
        let gate = AsyncGate()
        state.graphTableRowsPublishGate = { _ in await gate.suspend() }
        state.setGraphTableSort(byNote)
        await gate.waitUntilEntered()
        XCTAssertEqual(state.graphTableRequestedSort, byNote, "B is pending")
        // A asked for again: B is cancelled at issue.
        state.graphTableRowsPublishGate = nil
        state.setGraphTableSort(accepted)
        XCTAssertNil(state.graphTableRequestedSort, "the pending sort was cancelled")
        XCTAssertEqual(state.graphTablePublishedRequest, state.graphTableRequest)
        XCTAssertNil(state.graphDiagramWhereAmIEvent(), "the cancellation request is still in flight")
        await gate.release()
        try await pollUntil { state.graphTableAnsweredSeq == state.graphTableSeq }
        state.graphAnnouncer.flushForTests()
        XCTAssertEqual(state.graphTableSort, accepted)
        XCTAssertNotNil(state.graphDiagramWhereAmIEvent(), "readback resumes when cancellation publishes")
        XCTAssertFalse(
            posts.contains { $0.contains("shown") },
            "the cancellation spoke a count: \(posts)")
    }

    /// IPG-32 (C-8): the table readback answers only while the shown rows
    /// are the LIVE query's. The published request can equal the current one
    /// while both trail the view state — an edit made in Diagram mode used to
    /// leave exactly that state — and the readback then described the old
    /// rows under the new filter prose.
    func testTheTableReadbackIsUnavailableWhileTheLiveQueryHasMovedOn() async throws {
        let state = try await makeAppState()
        try await openQuiescentGraph(state)
        XCTAssertNotNil(state.graphDiagramWhereAmIEvent(), "quiescent, the readback answers")
        // The live query moves WITHOUT a token — the shape a Diagram-mode
        // edit left behind before the observer moved to the container.
        state.graphTableTextFilter = "zz-nothing-issued"
        XCTAssertNotEqual(state.graphTableRequest?.query, state.graphVisibilityQuery)
        XCTAssertNil(
            state.graphDiagramWhereAmIEvent(),
            "the readback answered over rows the live query no longer asks for")
    }

    /// IPG-20 (rule P Term P4, rule Q Term Q9; C-2 (iv)): only the token
    /// that is STILL CURRENT consumes the pending preset. A receiver that
    /// re-fetched under its own request returns `published == false` and the
    /// replacement it issued is the one that speaks the headline — clearing
    /// on that path erased the headline first, so a straddled preset pair
    /// spoke the generic summary. Read off the predicate for the reason
    /// `pairResultInstalls` is: the interleaving needs the two crossings of
    /// ONE pair to straddle a rebuild.
    func testOnlyTheCurrentTokenConsumesThePendingPreset() async throws {
        let state = try await makeAppState()
        try await openQuiescentGraph(state)
        let current = try XCTUnwrap(state.graphTableRequest)
        let token = GraphTableToken(request: current, seq: state.graphTableSeq)
        XCTAssertTrue(state.pairConsumesThePendingPreset(token: token))
        // A replacement issued under its own request advances the sequence,
        // and the headline belongs to it now.
        state.graphTableTextFilter = "a"
        state.requestGraphTableRowsIfQueryChanged()
        XCTAssertNotEqual(state.graphTableSeq, token.seq, "the replacement issued its own token")
        XCTAssertFalse(
            state.pairConsumesThePendingPreset(token: token),
            "a superseded continuation consumed a headline it no longer owns")
    }

    /// IPG-10 (rule Q, Term Q6; C-6): the count's stored gate is the tab's
    /// liveness AND the token's currency, re-checked at FIRE. A count queued
    /// for A used to speak after B had become current, which a slow B makes
    /// reachable — the Windows twin has the complete predicate.
    func testACountQueuedForASupersededTokenNeverFires() async throws {
        let state = try await makeAppState()
        try await openQuiescentGraph(state)
        var posts: [String] = []
        state.graphAnnouncer = GraphAnnouncer(post: { text, _ in posts.append(text) })
        // A: a needle whose rows publish and queue their count.
        state.graphTableTextFilter = "a"
        state.requestGraphTableRowsIfQueryChanged()
        try await pollUntil { state.graphTablePublishedRequest?.query.nameQuery == "a" }
        // B: a second needle, parked before it publishes, so A's queued line
        // is flushed while B is the current token.
        let gate = AsyncGate()
        state.graphTableRowsPublishGate = { _ in await gate.suspend() }
        state.graphTableTextFilter = "ab"
        state.requestGraphTableRowsIfQueryChanged()
        await gate.waitUntilEntered()
        state.graphAnnouncer.flushForTests()
        XCTAssertFalse(
            posts.contains { $0.contains("shown") },
            "a count queued for a superseded token fired: \(posts)")
        await gate.release()
        state.graphTableRowsPublishGate = nil
        try await pollUntil { state.graphTablePublishedRequest == state.graphTableRequest }
        state.graphAnnouncer.flushForTests()
        XCTAssertEqual(
            posts.filter { $0.contains("shown") }.count, 1,
            "B's own count is the one line: \(posts)")
    }

    /// C-2 (iii)/(iv): a needle typed during a backend-changing pair, in
    /// BOTH completion orders, ends with one snapshot under the preset's
    /// filter, the rows under the needle's request, the count spoken once
    /// and no headline (rule Q, Term Q4) — never a snapshot with another
    /// request's rows. Rows first: the mismatch arm re-fetches under the
    /// needle's request with its announce. Pair first: the pair's rows drop
    /// at the seq guard and the needle's publish over the fresh snapshot.
    func testANeedleDuringAnOrphansPairRefetchesUnderItsOwnRequest() async throws {
        for pairFirst in [false, true] {
            let state = try await makeAppState()
            try await openQuiescentGraph(state)
            var posts: [String] = []
            state.graphAnnouncer = GraphAnnouncer(post: { text, _ in posts.append(text) })
            let gate = AsyncGate()
            state.openGraphPreset(.orphans)
            let presetSeq = state.graphTableSeq
            state.graphTableTextFilter = "a"
            state.requestGraphTableRowsIfQueryChanged()
            let needleSeq = state.graphTableSeq
            XCTAssertNotEqual(needleSeq, presetSeq, "the needle issued its token")
            let needle = try XCTUnwrap(state.graphTableRequest)
            XCTAssertTrue(needle.query.filter.orphansOnly)
            XCTAssertEqual(needle.query.nameQuery, "a")
            XCTAssertNil(state.graphTablePendingPreset, "the needle replaced the headline (Term Q4)")
            if pairFirst {
                state.graphTableRowsPublishGate = { token in
                    if token.seq == needleSeq { await gate.suspend() }
                }
            } else {
                state.graphTablePublishGate = { token in
                    if token.seq == presetSeq { await gate.suspend() }
                }
            }
            await gate.waitUntilEntered()
            if pairFirst {
                // The pair published its snapshot; its rows dropped at the seq guard.
                try await pollUntil { state.graphTableSnapshotFilter?.orphansOnly == true && !state.graphTableLoading }
                XCTAssertNotEqual(state.graphTablePublishedRequest, state.graphTableRequest, "pair first: the needle's rows are still held")
            } else {
                // The rows landed under the old snapshot and re-fetched under their own request.
                try await pollUntil { state.graphTablePublishedRequest?.query.nameQuery == "a" }
            }
            await gate.release()
            try await pollUntil { !state.graphTableLoading && state.graphTablePublishedRequest == state.graphTableRequest }
            state.graphTablePublishGate = nil
            state.graphTableRowsPublishGate = nil
            state.graphAnnouncer.flushForTests()
            let order = pairFirst ? "pair first" : "rows first"
            XCTAssertEqual(state.graphTableSnapshotFilter?.orphansOnly, true, order)
            XCTAssertEqual(state.graphTablePublishedRequest?.query.nameQuery, "a", "\(order): the rows are the needle's request's")
            XCTAssertEqual(state.graphTablePublishedRequest?.query.filter.orphansOnly, true, order)
            XCTAssertNil(state.graphTablePendingPreset, order)
            XCTAssertNil(state.graphTableInFlightAnnounce, "\(order): nothing in flight")
            XCTAssertEqual(posts.filter { $0.contains("shown") }.count, 1, "\(order): the count once: \(posts)")
            XCTAssertFalse(posts.contains { $0.contains("orphaned") }, "\(order): no headline: \(posts)")
        }
    }

    /// C-2 (iii): a sort typed during a backend-changing pair, in BOTH
    /// completion orders, ends adopted — the re-fetch carries the sort as
    /// the request's own — with the count once and no headline.
    func testASortDuringAnOrphansPairRefetchesUnderItsOwnRequest() async throws {
        let byNote = GraphTableSort(column: .note, ascending: true)
        for pairFirst in [false, true] {
            let state = try await makeAppState()
            try await openQuiescentGraph(state)
            var posts: [String] = []
            state.graphAnnouncer = GraphAnnouncer(post: { text, _ in posts.append(text) })
            let gate = AsyncGate()
            state.openGraphPreset(.orphans)
            let presetSeq = state.graphTableSeq
            state.requestGraphTableRows(sort: byNote)
            let sortSeq = state.graphTableSeq
            XCTAssertEqual(state.graphTableRequestedSort, byNote)
            if pairFirst {
                state.graphTableRowsPublishGate = { token in
                    if token.seq == sortSeq { await gate.suspend() }
                }
            } else {
                state.graphTablePublishGate = { token in
                    if token.seq == presetSeq { await gate.suspend() }
                }
            }
            await gate.waitUntilEntered()
            if pairFirst {
                try await pollUntil { state.graphTableSnapshotFilter?.orphansOnly == true && !state.graphTableLoading }
            } else {
                try await pollUntil { state.graphTablePublishedRequest?.sort == byNote }
            }
            await gate.release()
            try await pollUntil { !state.graphTableLoading && state.graphTablePublishedRequest == state.graphTableRequest }
            state.graphTablePublishGate = nil
            state.graphTableRowsPublishGate = nil
            state.graphAnnouncer.flushForTests()
            let order = pairFirst ? "pair first" : "rows first"
            XCTAssertEqual(state.graphTableSort, byNote, "\(order): the sort adopted under its own request")
            XCTAssertNil(state.graphTableRequestedSort, order)
            XCTAssertEqual(state.graphTableSnapshotFilter?.orphansOnly, true, order)
            XCTAssertNil(state.graphTablePendingPreset, order)
            XCTAssertEqual(posts.filter { $0.contains("shown") }.count, 1, "\(order): the count once: \(posts)")
            XCTAssertFalse(posts.contains { $0.contains("orphaned") }, "\(order): no headline: \(posts)")
        }
    }

    /// C-2 (iv): the generation refresh that supersedes a preset's pair in
    /// flight inherits its announce and the pending preset, so the headline
    /// is spoken once over the newer generation — CR-1's race, closed.
    func testAFileChangeDuringAPresetsFetchSpeaksTheHeadlineOverTheNewerGeneration() async throws {
        let state = try await makeAppState()
        try await openQuiescentGraph(state)
        var posts: [String] = []
        state.graphAnnouncer = GraphAnnouncer(post: { text, _ in posts.append(text) })
        let gate = AsyncGate()
        state.openGraphPreset(.orphans)
        let presetSeq = state.graphTableSeq
        state.graphTablePublishGate = { token in
            if token.seq == presetSeq { await gate.suspend() }
        }
        await gate.waitUntilEntered()
        XCTAssertEqual(state.graphTableInFlightAnnounce, .summary, "the preset's pair is in flight")
        // The probe finds a newer graph while the pair is held: the seen
        // mark is an older generation's, as after a file change.
        state.graphTableSeenGraphGeneration = state.graphTableSeenGraphGeneration &+ 1
        state.refreshGraphTableIfGraphChanged()
        try await pollUntil {
            state.graphTableSeq > presetSeq && !state.graphTableLoading && state.graphTablePendingPreset == nil
        }
        await gate.release()
        try await pollUntil { !state.graphTableLoading }
        state.graphTablePublishGate = nil
        state.graphAnnouncer.flushForTests()
        XCTAssertEqual(posts, ["0 orphaned notes."], "the replacing load spoke the headline once, no summary")
        XCTAssertNil(state.graphTableInFlightAnnounce)
    }

    /// C-8: ⌃⌘I routes to the graph in Table mode too — the table's
    /// readback answers where the always-enabled item was a silent no-op.
    func testWhereAmIRoutesToTheGraphInTableMode() async throws {
        let state = try await makeAppState()
        XCTAssertEqual(state.whereAmIRouteTarget, .none, "no surface")
        try await openQuiescentGraph(state)
        XCTAssertNil(state.graphDiagramModel, "Table mode: no diagram model")
        XCTAssertFalse(state.graphDiagramZoomActive)
        XCTAssertEqual(state.whereAmIRouteTarget, .graph, "the table's readback answers (contracts doc §PR C, C-8)")
        XCTAssertNotNil(state.graphDiagramWhereAmIEvent())
    }

    /// C-8: the table's readback names the shared key's shown row the
    /// diagram's way, with no zoom clause; no key reads NoSelection; the
    /// kind overlay reads as the closed Unresolved arm.
    func testTheTableReadbackNamesTheSelectedSnapshotNodeWithoutAZoomClause() async throws {
        let state = try await makeAppState()
        try await openQuiescentGraph(state)
        let a = try XCTUnwrap(state.graphTableRows.first { $0.label == "a" })
        let defaults = GraphWhereAmIFilter.normal(orphansOnly: false, attachmentsShown: false, ghostsShown: true)
        state.graphSelectedNodeKey = nil
        let none = try XCTUnwrap(state.graphDiagramWhereAmIEvent())
        XCTAssertEqual(
            none, .graphWhereAmI(selection: .noSelection, zoomPercent: nil, filter: defaults, nameFilter: ""))
        XCTAssertEqual(a11yRender(event: .graph(event: none)).text, "No node selected, filters: unresolved shown.")

        state.graphSelectedNodeKey = a.stableKey
        let event = try XCTUnwrap(state.graphDiagramWhereAmIEvent())
        XCTAssertEqual(
            event,
            .graphWhereAmI(
                selection: .node(
                    row: GraphRowCopy(
                        label: "a", kind: .note, inLinks: a.linksIn, outLinks: a.linksOut,
                        references: a.linksIn, embed: false),
                    component: a.component),
                zoomPercent: nil, filter: defaults, nameFilter: ""))
        let text = a11yRender(event: .graph(event: event)).text
        XCTAssertTrue(text.hasPrefix("a, "), text)
        XCTAssertFalse(text.contains("zoom"), "no zoom clause on the table: \(text)")

        // The kind overlay: the shown rows are ghosts alone, so the note's
        // key reads NoSelection under the closed Unresolved arm.
        state.graphTableKindFilter = .ghost
        state.requestGraphTableRowsIfQueryChanged()
        try await pollUntil { state.graphTablePublishedRequest == state.graphTableRequest }
        let ghosts = try XCTUnwrap(state.graphDiagramWhereAmIEvent())
        XCTAssertEqual(
            a11yRender(event: .graph(event: ghosts)).text,
            "No node selected, filters: unresolved shown, unresolved only.")
    }

    /// C-8: the table's readback is refused while a pair or a rows request
    /// is in flight (rule Q, Term Q7) and answers again at the install.
    func testTheTableReadbackIsUnavailableWhileALoadIsInFlight() async throws {
        let state = try await makeAppState()
        try await openQuiescentGraph(state)
        XCTAssertNotNil(state.graphDiagramWhereAmIEvent(), "quiescent: answers")
        let gate = AsyncGate()
        state.loadGraphTable(announce: .silent)
        let seq = state.graphTableSeq
        state.graphTablePublishGate = { token in
            if token.seq == seq { await gate.suspend() }
        }
        XCTAssertNil(state.graphDiagramWhereAmIEvent(), "a pair in flight: refused")
        await gate.waitUntilEntered()
        XCTAssertNil(state.graphDiagramWhereAmIEvent(), "still in flight at the gate")
        await gate.release()
        try await pollUntil { !state.graphTableLoading && state.graphTablePublishedRequest == state.graphTableRequest }
        state.graphTablePublishGate = nil
        XCTAssertNotNil(state.graphDiagramWhereAmIEvent(), "answers again at the install")

        state.graphTableTextFilter = "zzz"
        state.requestGraphTableRowsIfQueryChanged()
        XCTAssertNil(state.graphDiagramWhereAmIEvent(), "a rows request in flight: the shown rows are not the newest request's")
        try await pollUntil { state.graphTablePublishedRequest == state.graphTableRequest }
        XCTAssertNotNil(state.graphDiagramWhereAmIEvent())
    }

    func testTheTableReadbackWaitsWhenTheNeedleReturnsToItsAcceptedValue() async throws {
        let state = try await makeAppState()
        try await openQuiescentGraph(state)
        let acceptedNeedle = state.graphTableTextFilter
        let gate = AsyncGate()
        state.graphTableRowsPublishGate = { token in
            if token.request.query.nameQuery == acceptedNeedle { await gate.suspend() }
        }
        state.graphTableTextFilter = "zzz"
        state.requestGraphTableRowsIfQueryChanged()
        // Clear before the changed needle publishes: the newest request has
        // the same values as the accepted one, but has not answered yet.
        state.graphTableTextFilter = acceptedNeedle
        state.requestGraphTableRowsIfQueryChanged()
        await gate.waitUntilEntered()
        XCTAssertFalse(state.graphTableLoading, "a rows request does not set the pair's loading flag")
        XCTAssertEqual(state.graphTablePublishedRequest, state.graphTableRequest)
        XCTAssertNil(state.graphDiagramWhereAmIEvent(), "matching values do not establish quiescence")

        await gate.release()
        state.graphTableRowsPublishGate = nil
        try await pollUntil { state.graphTableAnsweredSeq == state.graphTableSeq }
        XCTAssertNotNil(state.graphDiagramWhereAmIEvent())
    }

    func testAnUnavailableDiagramDoesNotReadTheCachedTable() async throws {
        let state = try await makeAppState()
        try await openQuiescentGraph(state)
        state.graphSelectedNodeKey = try XCTUnwrap(state.graphTableRows.first).stableKey
        XCTAssertNotNil(state.graphDiagramWhereAmIEvent(), "the cached table can answer")

        state.setGraphMode(.diagram)
        state.graphDiagramLoading = true
        XCTAssertNil(state.graphDiagramModel)
        XCTAssertNil(state.graphDiagramWhereAmIEvent(), "a diagram awaiting its model has no readback")

        state.graphDiagramLoading = false
        state.graphDiagramError = "Unable to load graph"
        XCTAssertNil(state.graphDiagramWhereAmIEvent(), "a failed diagram must not read the table's selection")

        state.setGraphMode(.table)
        state.resetGraphDiagramState()
        XCTAssertNotNil(state.graphDiagramWhereAmIEvent(), "switching to the table restores its readback")
    }

    private func pollUntil(
        timeout: TimeInterval = 5, _ condition: @MainActor () -> Bool
    ) async throws {
        let deadline = Date().addingTimeInterval(timeout)
        while !condition() {
            if Date() > deadline { XCTFail("condition not met within \(timeout)s"); return }
            try await Task.sleep(nanoseconds: 20_000_000)
        }
    }
}
