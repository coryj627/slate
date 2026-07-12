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

    /// Once every graph tab closes, the generation-driven refresh gates
    /// off — even though the cached snapshot is deliberately NOT cleared
    /// on close (review round 1 finding 10: keying refresh on the stale
    /// snapshot leaked a forever-refresh with no on-screen consumer).
    func testRefreshGateClosesWithLastGraphTab() async throws {
        let state = try await makeAppState()
        state.openFile("a.md", target: .currentTab)
        state.openGraphTab()
        try await pollUntil { state.graphTableSnapshot != nil }
        XCTAssertTrue(state.anyGraphTabVisible)
        let graphID = try XCTUnwrap(state.workspace.activeTab?.id)
        state.requestCloseTab(graphID)
        XCTAssertNotNil(state.graphTableSnapshot, "snapshot is intentionally retained on close")
        XCTAssertFalse(state.anyGraphTabVisible, "refresh gate is off once the graph tab is gone")
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
