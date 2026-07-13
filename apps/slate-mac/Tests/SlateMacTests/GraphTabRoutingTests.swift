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

    /// The filter-count text is computed from the FRESH snapshot + the
    /// current text needle (round 2 finding 7), so a post-fetch
    /// announcement can't drift from the view's synchronous one.
    func testGraphFilterCountText() async throws {
        let state = try await makeAppState()
        state.openGraphTab()
        try await pollUntil { state.graphTableSnapshot != nil }
        let snap = try XCTUnwrap(state.graphTableSnapshot)
        let total = snap.nodes.count
        state.graphTableTextFilter = ""
        XCTAssertEqual(state.graphFilterCountText(snap), "\(total) of \(total) shown")
        state.graphTableTextFilter = "a"
        let shown = snap.nodes.filter {
            $0.label.range(of: "a", options: [.caseInsensitive, .diacriticInsensitive]) != nil
        }.count
        XCTAssertEqual(state.graphFilterCountText(snap), "\(shown) of \(total) shown")
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
        let expected = state.graphPresetAnnouncement(.orphans, snap: snap)
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
        state.graphSelectedNodeKey = GraphNodeKey.make(for: real)
        state.revalidateGraphSelection(against: snap)
        XCTAssertEqual(
            state.graphSelectedNodeKey, GraphNodeKey.make(for: real),
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
