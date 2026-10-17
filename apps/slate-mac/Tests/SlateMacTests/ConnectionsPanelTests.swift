// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import SwiftUI
import XCTest

@testable import SlateMac

/// The Connections panel's host side (P1-1 #554; W6-2 PR 0b): the tree's
/// derivation is core's now (`graph_connections_tree`, contracts doc
/// 0b-4) and its facts live in `crates/slate-core/src/graph_queries.rs`.
/// What remains here is the adapter — the nesting of core's flat rows
/// by `parent_id`, the snippet overlay by path — and the busy-reason
/// admission overlay (0bD-8), all pure logic; and the leaf's load
/// triggers and loading row (#1217), over a real session with the panel
/// mounted the way the right pane retains it.
final class ConnectionsPanelTests: XCTestCase {
    private var tempDir: URL?
    /// The window a fact mounted the panel in, closed at teardown (a close
    /// deferred inside a throwing async fact masks the fact's own error).
    private var hostWindow: NSWindow?

    override func tearDownWithError() throws {
        MainActor.assumeIsolated {
            hostWindow?.close()
            hostWindow = nil
        }
        if let tempDir { try? FileManager.default.removeItem(at: tempDir) }
        try super.tearDownWithError()
    }

    private func row(
        _ id: String, level: UInt32, parent: String?, nodeId: UInt64, _ label: String,
        path: String?, kind: GraphNodeKind = .note, embedOnly: Bool = false,
        inLinks: UInt32 = 0, outLinks: UInt32 = 0, references: UInt32 = 0
    ) -> GraphConnectionRow {
        GraphConnectionRow(
            id: id, level: level, parentId: parent, nodeId: nodeId,
            stableKey: path.map { "p:\($0)" } ?? "g:\(label.lowercased())",
            label: label, path: path, targetRaw: label, kind: kind, embedOnly: embedOnly,
            inLinks: inLinks, outLinks: outLinks, references: references)
    }

    private func tree(incoming: [GraphConnectionRow], outgoing: [GraphConnectionRow], depth: UInt32 = 1)
        -> GraphConnectionsTree
    {
        GraphConnectionsTree(
            generation: 1, centerId: 1, centerKey: "p:center.md", depth: depth,
            summaryCounts: GraphNeighborhoodCounts(
                centerLabel: "Center", inLinks: UInt32(incoming.count), outLinks: UInt32(outgoing.count),
                noteCount: 0, depth: depth),
            incoming: incoming, outgoing: outgoing)
    }

    /// The clamp reads core's constants (0b-8).
    func testDepthClampIntoOneToThree() {
        XCTAssertEqual(AppState.clampConnectionsDepth(0), 1)
        XCTAssertEqual(AppState.clampConnectionsDepth(1), 1)
        XCTAssertEqual(AppState.clampConnectionsDepth(3), 3)
        XCTAssertEqual(AppState.clampConnectionsDepth(99), 3)
        // Any Int: the conversion saturates instead of trapping (IPB-11).
        XCTAssertEqual(AppState.clampConnectionsDepth(Int.min), 1)
        XCTAssertEqual(AppState.clampConnectionsDepth(Int.max), 3)
        XCTAssertEqual(Int(graphConstants().connectionsDepthMax), 3)
    }

    /// The ghost path is core's (0b-11); the host wrapper forwards.
    func testGhostNotePathIsCores() {
        XCTAssertEqual(AppState.ghostNotePath("Missing Note"), "Missing Note.md")
        XCTAssertEqual(AppState.ghostNotePath("./notes/Foo"), "notes/Foo.md")
        XCTAssertEqual(AppState.ghostNotePath("Foo.md"), "Foo.md")
        XCTAssertEqual(AppState.ghostNotePath("dir/"), graphGhostNotePath(targetRaw: "dir/"))
    }

    @MainActor
    func testBusyReasonAppliesOnlyToGhostCreationControls() {
        let reason = AppState.structuralMutationBusyReason
        XCTAssertEqual(
            ConnectionsPanel.creationDisabledReason(
                forGhost: true, structuralReason: reason),
            reason)
        XCTAssertNil(
            ConnectionsPanel.creationDisabledReason(
                forGhost: false, structuralReason: reason))
        XCTAssertEqual(
            ConnectionsPanel.actionDisabledReason(
                .createNote, structuralReason: reason),
            reason)
        XCTAssertNil(
            ConnectionsPanel.actionDisabledReason(
                .open, structuralReason: reason),
            "unrelated graph context actions stay available")
        XCTAssertNil(
            ConnectionsPanel.actionDisabledReason(
                .createNote, structuralReason: nil),
            "idle ghost creation stays available")
    }

    /// The adapter keeps core's lists and order, carries the kind, the
    /// embed flag and the counts, and derives nothing.
    func testAdapterCarriesCoresRowsInOrder() {
        let t = tree(
            incoming: [row("in/p%3Ain%2Emd", level: 1, parent: nil, nodeId: 2, "Inbound", path: "in.md", outLinks: 1)],
            outgoing: [
                row("out/g%3Amissing", level: 1, parent: nil, nodeId: 4, "Missing", path: nil, kind: .ghost, references: 1),
                row("out/p%3Aout%2Emd", level: 1, parent: nil, nodeId: 3, "Outbound", path: "out.md", inLinks: 1),
                row("out/p%3Apic%2Epng", level: 1, parent: nil, nodeId: 5, "pic.png", path: "pic.png", kind: .attachment, embedOnly: true, references: 1),
            ])
        let model = ConnectionsModel(tree: t, bundle: nil)
        XCTAssertEqual(model.incoming.map(\.label), ["Inbound"])
        XCTAssertEqual(model.outgoing.map(\.label), ["Missing", "Outbound", "pic.png"], "core's order, untouched")
        let ghostRow = model.outgoing.first { $0.label == "Missing" }!
        XCTAssertTrue(ghostRow.isGhost)
        XCTAssertNil(ghostRow.path)
        XCTAssertEqual(ghostRow.references, 1)
        XCTAssertEqual(ghostRow.stableKey, "g:missing")
        let picRow = model.outgoing.first { $0.label == "pic.png" }!
        XCTAssertTrue(picRow.isEmbed, "core's embed_only → the Embed badge")
        XCTAssertTrue(picRow.isAttachment)
        let inRow = model.incoming[0]
        XCTAssertEqual(inRow.rowCopy.inLinks, 0)
        XCTAssertEqual(inRow.rowCopy.outLinks, 1)
        XCTAssertFalse(inRow.isGhost)
        XCTAssertEqual(inRow.id, "in/p%3Ain%2Emd", "the occurrence id is core's")
        XCTAssertEqual(model.row(id: "out/g%3Amissing")?.label, "Missing")
        XCTAssertEqual(t.summaryCounts.inLinks, 1, "the leaf's summary rides on the tree (design A)")
    }

    /// Flat pre-order rows nest by `parent_id` in one pass: a level-2 row
    /// lands under its parent, a diamond descendant under each parent.
    func testAdapterNestsFlatRowsByParentId() {
        let t = tree(
            incoming: [],
            outgoing: [
                row("out/p%3Aalpha%2Emd", level: 1, parent: nil, nodeId: 3, "Alpha", path: "alpha.md"),
                row("out/p%3Aalpha%2Emd/p%3Ashared%2Emd", level: 2, parent: "out/p%3Aalpha%2Emd", nodeId: 4, "Shared", path: "shared.md"),
                row("out/p%3Abeta%2Emd", level: 1, parent: nil, nodeId: 2, "beta", path: "beta.md"),
                row("out/p%3Abeta%2Emd/p%3Ashared%2Emd", level: 2, parent: "out/p%3Abeta%2Emd", nodeId: 4, "Shared", path: "shared.md"),
                row("out/p%3Abeta%2Emd/p%3Ashared%2Emd/p%3Adeep%2Emd", level: 3, parent: "out/p%3Abeta%2Emd/p%3Ashared%2Emd", nodeId: 5, "Deep", path: "deep.md"),
            ], depth: 3)
        let model = ConnectionsModel(tree: t, bundle: nil)
        XCTAssertEqual(model.outgoing.map(\.label), ["Alpha", "beta"])
        XCTAssertEqual(model.outgoing[0].nested.map(\.label), ["Shared"])
        XCTAssertEqual(model.outgoing[1].nested.map(\.label), ["Shared"])
        XCTAssertEqual(model.outgoing[1].nested[0].nested.map(\.label), ["Deep"], "the third hop nests under the second")
        XCTAssertTrue(model.outgoing[0].nested[0].nested.isEmpty)
        XCTAssertNotEqual(model.outgoing[0].nested[0].id, model.outgoing[1].nested[0].id, "a diamond descendant is two occurrences")
        XCTAssertEqual(model.outgoing[0].nested[0].nodeId, model.outgoing[1].nested[0].nodeId, "of one node")
    }

    /// The depth-one snippet overlay reads the bundle by path (0bD-2);
    /// deeper rows carry none.
    func testSnippetOverlayFromBundleAtDepthOne() {
        let t = tree(
            incoming: [],
            outgoing: [
                row("out/p%3Aout%2Emd", level: 1, parent: nil, nodeId: 3, "Outbound", path: "out.md"),
                row("out/p%3Aout%2Emd/p%3Afar%2Emd", level: 2, parent: "out/p%3Aout%2Emd", nodeId: 4, "Far", path: "far.md"),
            ], depth: 2)
        let bundle = NoteLoadBundle(
            backlinks: BacklinkPage(items: [], nextCursor: nil, totalFiltered: 0),
            outgoingLinks: [
                OutgoingLink(
                    targetPath: "out.md", targetRaw: "Outbound", targetAnchor: nil,
                    kind: "wikilink", isEmbed: false, isExternal: false, isUnresolved: false,
                    snippet: "…the Outbound note…", ordinal: 0, spanStart: 0, spanEnd: 0, displayText: nil),
                OutgoingLink(
                    targetPath: "far.md", targetRaw: "Far", targetAnchor: nil,
                    kind: "wikilink", isEmbed: false, isExternal: false, isUnresolved: false,
                    snippet: "…never shown at level two…", ordinal: 1, spanStart: 0, spanEnd: 0, displayText: nil),
            ],
            properties: [])
        let model = ConnectionsModel(tree: t, bundle: bundle)
        XCTAssertEqual(model.outgoing.first?.snippet, "…the Outbound note…")
        XCTAssertNil(model.outgoing.first?.nested.first?.snippet)
    }

    // MARK: - The leaf's load triggers and its loading row (#1217)

    /// A real session over a three-note vault — a.md → b.md, b.md → a.md and
    /// c.md, c.md → b.md — so the notes' neighbourhoods differ.
    @MainActor
    private func makeAppState() async throws -> AppState {
        let root = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-connections-panel-\(UUID().uuidString)")
        tempDir = root
        let vault = root.appendingPathComponent("vault")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        try Data("[[b]]".utf8).write(to: vault.appendingPathComponent("a.md"))
        try Data("[[a]] and [[c]]".utf8).write(to: vault.appendingPathComponent("b.md"))
        try Data("[[b]]".utf8).write(to: vault.appendingPathComponent("c.md"))
        let store = RecentVaultsStore(fileURL: root.appendingPathComponent("recents.json"))
        let state = AppState(recentsStore: store, externalOpener: { _ in true })
        state.openVault(at: vault)
        await state.scanTask?.value
        // The generation probe is a trigger of its own (contract 35 Term
        // 3(f)): the scan's completion hook can land at any point, and its
        // silent refresh would load the note in view behind these facts.
        // Marking the scanned generation seen makes that probe a no-op.
        state.connectionsSeenGraphGeneration = try XCTUnwrap(state.currentSession).graphGeneration()
        return state
    }

    /// Mount the leaf the way the right pane retains it: hosted whether or
    /// not it is the active leaf (`RightPaneView`'s ZStack retention), so a
    /// rail switch changes the active leaf under a panel that stays mounted
    /// and whose `onAppear` does not re-fire.
    @MainActor
    private func mount(_ state: AppState) async {
        let appeared = XCTestExpectation(description: "the Connections leaf mounted")
        let window = NSWindow(
            contentRect: NSRect(x: 0, y: 0, width: 360, height: 480),
            styleMask: [.titled], backing: .buffered, defer: false)
        window.isReleasedWhenClosed = false
        window.contentView = NSHostingView(
            rootView: ConnectionsPanel().environmentObject(state).onAppear { appeared.fulfill() })
        hostWindow = window
        window.makeKeyAndOrderFront(nil)
        await fulfillment(of: [appeared], timeout: 30)
    }

    private struct Timeout: Error, CustomStringConvertible {
        let description: String
    }

    /// Turn the main run loop — where the panel's updates, their `onChange`
    /// actions and a load's publish all run — for `seconds`.
    @MainActor
    private func spin(_ seconds: TimeInterval) async {
        let turned = XCTestExpectation(description: "the main run loop turned")
        DispatchQueue.main.asyncAfter(deadline: .now() + seconds) { turned.fulfill() }
        await fulfillment(of: [turned], timeout: seconds + 30)
    }

    @MainActor
    private func waitFor(
        _ what: String, timeout: TimeInterval = 15, _ condition: () -> Bool
    ) async throws {
        let deadline = Date().addingTimeInterval(timeout)
        while !condition() {
            guard Date() < deadline else { throw Timeout(description: "timed out waiting for \(what)") }
            await spin(0.02)
        }
    }

    /// Holds a load at its publish point (`connectionsPublishGate`).
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

    /// #1217 (contract 35 Term 3(b), B-D11): a MOUNTED leaf whose note changed
    /// while it was inactive loads when the rail makes it active again. The
    /// retained panel's `onAppear` does not re-fire, so the leaf used to show
    /// "Loading connections…" and start nothing. The switch's load is
    /// audible — the neighbourhood is spoken once — and a switch back to a
    /// CURRENT leaf loads nothing.
    @MainActor
    func testAMountedRailSwitchToAStaleLeafLoads() async throws {
        let state = try await makeAppState()
        var posts: [String] = []
        state.graphAnnouncer = GraphAnnouncer(post: { text, _ in posts.append(text) })
        state.workspace.activeLeaf = .connections
        await mount(state)
        state.openFile("a.md", target: .currentTab)
        try await waitFor("a.md's neighbourhood") {
            state.connectionsLoadedPath == "a.md" && !state.connectionsLoading
        }

        // The rail moves to another leaf, and the note changes while the
        // Connections leaf is mounted but inactive: nothing loads.
        state.workspace.activeLeaf = .outline
        await spin(0.25)
        let loadsBefore = state.connectionsLoadSeq
        state.openFile("b.md", target: .currentTab)
        await spin(0.25)
        XCTAssertEqual(state.connectionsLoadSeq, loadsBefore, "an inactive leaf does not follow the note")
        XCTAssertEqual(state.connectionsLoadedPath, "a.md", "so its publication is stale")
        posts.removeAll()

        // The rail switch back to the stale leaf loads the note in view.
        state.workspace.activeLeaf = .connections
        try await waitFor("b.md's neighbourhood after the rail switch") {
            state.connectionsLoadedPath == "b.md" && !state.connectionsLoading
        }
        XCTAssertEqual(state.connectionsLoadSeq, loadsBefore + 1, "the switch issues one load")
        let tree = try XCTUnwrap(state.connectionsTree)
        let summary = a11yRender(
            event: .graph(event: .graphNeighborhoodSummary(counts: tree.summaryCounts))
        ).text
        XCTAssertEqual(posts, [summary], "the switch's load is audible and speaks the neighbourhood once")

        // Away and back while the publication is current: nothing loads.
        state.workspace.activeLeaf = .outline
        await spin(0.25)
        state.workspace.activeLeaf = .connections
        await spin(0.25)
        XCTAssertEqual(
            state.connectionsLoadSeq, loadsBefore + 1, "a switch to a CURRENT leaf loads nothing")
        XCTAssertEqual(posts, [summary], "and speaks nothing more")
    }

    /// #1217: the loading row is RENDERED from `connectionsLoading`, which the
    /// leaf wrote four times and read nowhere. The row shows only while a
    /// load is in flight for a note the published payload does not describe;
    /// with none in flight the panel shows its empty state, so "Loading
    /// connections…" can no longer stand over a load nobody started. A load
    /// for the SAME note keeps what is published, rows or error (contract 35
    /// Term 7). The flag follows the load: set when it is issued, cleared
    /// when the newest load lands — also when the reader left its note
    /// mid-load and it publishes nothing.
    @MainActor
    func testTheLoadingRowFollowsConnectionsLoading() async throws {
        typealias Panel = ConnectionsPanel
        XCTAssertEqual(
            Panel.contentState(payloadCurrent: false, loading: true, error: nil, hasTree: false),
            .loading)
        XCTAssertEqual(
            Panel.contentState(payloadCurrent: false, loading: false, error: nil, hasTree: true),
            .empty, "another note's payload with no load in flight is not loading")
        XCTAssertEqual(
            Panel.contentState(
                payloadCurrent: false, loading: false, error: "The disk is full.", hasTree: false),
            .empty, "another note's error is not this note's")
        XCTAssertEqual(
            Panel.contentState(payloadCurrent: true, loading: true, error: nil, hasTree: true),
            .rows, "the same note reloading keeps its rows, with no indicator")
        XCTAssertEqual(
            Panel.contentState(
                payloadCurrent: true, loading: true, error: "The disk is full.", hasTree: false),
            .error("The disk is full."), "the same note reloading keeps its error visible")
        XCTAssertEqual(
            Panel.contentState(payloadCurrent: true, loading: false, error: nil, hasTree: false),
            .empty)

        // The flag over a real load, held at its publish point.
        let state = try await makeAppState()
        state.openFile("a.md", target: .currentTab)
        XCTAssertEqual(
            Panel.contentState(for: state), .empty, "nothing published and nothing in flight")
        let first = AsyncGate()
        state.connectionsPublishGate = { await first.suspend() }
        state.loadConnections()
        XCTAssertTrue(state.connectionsLoading, "set when the load is issued")
        XCTAssertEqual(Panel.contentState(for: state), .loading, "a.md's load is in flight")
        await first.waitUntilEntered()
        await first.release()
        try await waitFor("a.md's load to land") { !state.connectionsLoading }
        XCTAssertEqual(state.connectionsLoadedPath, "a.md")
        XCTAssertEqual(Panel.contentState(for: state), .rows)

        // The reader leaves the note while its load is in flight: the load
        // lands for a note nobody is on and publishes nothing — and the flag
        // does not outlive it.
        let second = AsyncGate()
        state.connectionsPublishGate = { await second.suspend() }
        state.openFile("b.md", target: .currentTab)
        state.loadConnections()
        await second.waitUntilEntered()
        state.openFile("c.md", target: .currentTab)
        await second.release()
        try await waitFor("b.md's load to land") { !state.connectionsLoading }
        XCTAssertEqual(state.connectionsLoadedPath, "a.md", "b.md's result publishes nothing for c.md")
        XCTAssertEqual(
            Panel.contentState(for: state), .empty,
            "with no load in flight the panel shows the empty state, never the loading text")
    }
}
