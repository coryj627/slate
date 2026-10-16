// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import SwiftUI
import XCTest

@testable import SlateMac

/// The graph table's host side (P1-2 #555; W6-2 PR 0b): the rows, their
/// cells, their order and the stable identity are core's now
/// (`graph_table_rows`, contracts doc 0b-7; the facts live in
/// `crates/slate-core/src/graph_queries.rs`). What remains here is the
/// grid's consumption — the column adapters over `cells`, the identity by
/// `stableKey`, the action set through core's list, the busy-state
/// admission overlay (0bD-8), and the external-sort lifecycle (0b-14) —
/// and the tab body's mode line (#1220), over a real session with the
/// body mounted.
final class GraphTableViewTests: XCTestCase {
    private var vaultRoot: URL?
    /// The window a fact mounted the tab body in, closed at teardown (a
    /// close deferred inside a throwing async fact masks the fact's error).
    private var hostWindow: NSWindow?

    override func tearDownWithError() throws {
        MainActor.assumeIsolated {
            hostWindow?.close()
            hostWindow = nil
        }
        if let vaultRoot { try? FileManager.default.removeItem(at: vaultRoot) }
        try super.tearDownWithError()
    }

    private static func source(_ relativePath: String) throws -> String {
        let root = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .appendingPathComponent("Sources/SlateMac")
        return try String(
            contentsOf: root.appendingPathComponent(relativePath),
            encoding: .utf8)
    }

    private func row(
        _ nodeId: UInt64, _ label: String, path: String?, kind: GraphNodeKind = .note,
        linksIn: UInt32 = 0, folder: String = "", modified: String = ""
    ) -> GraphTableRow {
        let kindLabel: String
        switch kind {
        case .note: kindLabel = "Note"
        case .attachment: kindLabel = "Attachment"
        case .ghost: kindLabel = "Unresolved"
        }
        return GraphTableRow(
            stableKey: path.map { "p:\($0)" } ?? "g:\(label.lowercased())",
            nodeId: nodeId, label: label, path: path, kind: kind,
            cells: [label, String(linksIn), "0", "0", "0", "0", modified, folder, kindLabel],
            linksIn: linksIn, linksOut: 0, embedsIn: 0, embedsOut: 0, component: 0, modifiedMs: nil)
    }

    /// Every grid column is built from core's ordered specs (design B) and
    /// reads its cell by the spec's index; no column is listed in Swift.
    func testColumnsAreBuiltFromCoresSpecs() {
        let specs = GraphTableColumns.specs
        XCTAssertEqual(specs.count, 9)
        XCTAssertEqual(specs.map(\.header), graphTableColumns().map(\.header), "fetched once, core's vector")
        XCTAssertEqual(GraphTableColumns.index(of: .linksIn), 1, "the default sort's column index")
        XCTAssertEqual(GraphTableColumns.column(at: 7), .folder)
        XCTAssertNil(GraphTableColumns.column(at: 9))
        let cols = GraphTableColumns.columns
        XCTAssertEqual(cols.count, 9)
        let r = row(1, "Alpha", path: "notes/Alpha.md", linksIn: 3, folder: "notes", modified: "1970-01-01 00:00")
        XCTAssertEqual(cols[0].cell(r), "Alpha")
        XCTAssertEqual(cols[1].cell(r), "3")
        XCTAssertEqual(cols[6].cell(r), "1970-01-01 00:00")
        XCTAssertEqual(cols[7].cell(r), "notes")
        XCTAssertEqual(cols[8].cell(r), "Note")
        XCTAssertFalse(r.isGhost)
        XCTAssertTrue(row(2, "Missing", path: nil, kind: .ghost).isGhost)
    }

    /// Row identity is core's stable key — never the backend node id,
    /// which is reassigned on a rebuild.
    func testRowIdIsCoresStableKey() {
        let real = row(42, "Alpha", path: "notes/Alpha.md")
        XCTAssertEqual(real.id, "p:notes/Alpha.md")
        XCTAssertEqual(real.id, row(999, "Alpha", path: "notes/Alpha.md").id)
        XCTAssertEqual(graphStableKeyForPath(path: "notes/Alpha.md"), real.id)
    }

    /// Every row exposes only the actions core lists for its kind (0b-9).
    func testRowActionEnablementByKind() {
        let nav = ["Open", "Open in New Tab", "Show connections", "Reveal in File Tree"]
        for name in nav {
            XCTAssertTrue(GraphTableView.rowActionEnabled(name, isGhost: false), "\(name) on a real note")
            XCTAssertFalse(GraphTableView.rowActionEnabled(name, isGhost: true), "\(name) hidden on a ghost")
        }
        XCTAssertTrue(GraphTableView.rowActionEnabled("Create note", isGhost: true))
        XCTAssertFalse(GraphTableView.rowActionEnabled("Create note", isGhost: false))
        XCTAssertEqual(GraphRowAction.allCases.map(\.title), nav + ["Create note"])
    }

    @MainActor
    func testBusyGhostCellsExposeExactStructuralCreationReason() {
        let ghost = row(7, "Missing", path: nil, kind: .ghost)
        let note = row(8, "Present", path: "Present.md")
        let columns = GraphTableColumns.columns(
            ghostCreationDisabledReason: AppState.structuralMutationBusyReason)

        XCTAssertEqual(
            columns.compactMap { $0.accessibilityHint?(ghost) },
            Array(repeating: AppState.structuralMutationBusyReason, count: 9))
        XCTAssertTrue(columns.allSatisfy { $0.accessibilityHint?(note) == nil })
        XCTAssertTrue(
            GraphTableColumns.columns.allSatisfy {
                $0.accessibilityHint?(ghost) == nil
            },
            "idle ghost rows retain their normal activation behavior")
    }

    func testBusyGhostPrimaryAndModifiedActivationUseOneSuppressionPolicy() throws {
        let source = try Self.source("Graph/GraphTableView.swift")
        let compact = source.split(whereSeparator: { $0.isWhitespace }).joined(separator: " ")

        XCTAssertTrue(
            compact.contains("onActivate: { row in activate(row) }"),
            "Return and double-click retain the ordinary activation funnel")
        XCTAssertTrue(
            compact.contains("onActivateModified: { row in activateInNewTab(row) }"),
            "Command-Return and Command-double-click retain the modified funnel")
        XCTAssertTrue(
            compact.contains(
                "private func activate(_ row: GraphTableRow) { activate(row, fileTarget: .currentTab) }"))
        XCTAssertTrue(
            compact.contains(
                "private func activateInNewTab(_ row: GraphTableRow) { activate(row, fileTarget: .newTab) }"))
        XCTAssertTrue(
            compact.contains(
                "if row.isGhost { guard appState.structuralMutationDisabledReason == nil else { return } appState.createNoteFromGhost(targetRaw: row.label) }"),
            "every primary ghost path must stop before invoking the structural funnel while busy")
        XCTAssertTrue(
            compact.contains("else if let path = row.path { appState.openFile(path, target: fileTarget) }"),
            "ordinary and modified note opening remain available")
        XCTAssertTrue(
            compact.contains("sortsRowsLocally: false"),
            "the grid never re-sorts core's rows (0b-7)")
    }

    // MARK: - The load token (0b-14, design A; the grid's owner contract)

    private func rows(_ labels: [String], generation: UInt64 = 0) -> GraphTableRows {
        GraphTableRows(
            generation: generation, total: UInt64(labels.count),
            rows: labels.enumerated().map { row(UInt64($0.offset + 1), $0.element, path: "\($0.element).md") })
    }

    /// A sort request records the REQUESTED sort and leaves the accepted
    /// one alone until rows arrive; the rows and the accepted sort then
    /// publish together, rows first.
    @MainActor
    func testSortRequestPublishesRowsAndAcceptedSortTogether() {
        let state = AppState()
        XCTAssertEqual(state.graphTableSort, GraphTableSort(column: .linksIn, ascending: false))
        let token = state.issueGraphTableToken(sort: GraphTableSort(column: .note, ascending: true))
        XCTAssertEqual(state.graphTableRequestedSort, GraphTableSort(column: .note, ascending: true))
        XCTAssertEqual(state.graphTableSort.column, .linksIn, "not accepted before the rows exist")
        XCTAssertTrue(state.receiveGraphTableRows(token: token, result: rows(["Alpha", "Beta"])))
        XCTAssertEqual(state.graphTableRows.map(\.label), ["Alpha", "Beta"])
        XCTAssertEqual(state.graphTableSort, GraphTableSort(column: .note, ascending: true), "accepted with the rows")
        XCTAssertNil(state.graphTableRequestedSort, "the request is discharged")
    }

    /// A result whose token was superseded is dropped whole: neither its
    /// rows nor its sort reach the grid — even at the same generation.
    @MainActor
    func testSupersededTokenIsDroppedWhole() {
        let state = AppState()
        let first = state.issueGraphTableToken(sort: GraphTableSort(column: .note, ascending: true))
        let second = state.issueGraphTableToken(sort: GraphTableSort(column: .folder, ascending: false))
        XCTAssertTrue(state.receiveGraphTableRows(token: second, result: rows(["Second"])))
        XCTAssertFalse(state.receiveGraphTableRows(token: first, result: rows(["First"])), "the late first result never lands")
        XCTAssertEqual(state.graphTableRows.map(\.label), ["Second"])
        XCTAssertEqual(state.graphTableSort, GraphTableSort(column: .folder, ascending: false))
    }

    /// Two same-generation answers under DIFFERENT needles: the token
    /// carries the full request, so the answer to the older needle is
    /// dropped even though its sequence would have been valid alone.
    @MainActor
    func testReorderedSameGenerationAnswersUnderDifferentNeedlesAreDropped() {
        let state = AppState()
        state.graphTableTextFilter = "al"
        let older = state.issueGraphTableToken(sort: state.graphTableSort)
        state.graphTableTextFilter = "alp"
        let newer = state.issueGraphTableToken(sort: state.graphTableSort)
        XCTAssertNotEqual(older.request, newer.request, "the needle is part of the request")
        XCTAssertTrue(state.receiveGraphTableRows(token: newer, result: rows(["Alpine"])))
        XCTAssertFalse(state.receiveGraphTableRows(token: older, result: rows(["Alpha", "Alpine"])))
        XCTAssertEqual(state.graphTableRows.map(\.label), ["Alpine"])
    }

    /// A failed query rolls the requested sort back to the accepted one.
    @MainActor
    func testFailedQueryRollsBackTheRequest() {
        let state = AppState()
        let token = state.issueGraphTableToken(sort: GraphTableSort(column: .kind, ascending: true))
        state.failGraphTableRows(token: token)
        XCTAssertNil(state.graphTableRequestedSort)
        XCTAssertEqual(state.graphTableSort, GraphTableSort(column: .linksIn, ascending: false), "the accepted sort stands")
    }

    /// Filter-B rows never land on a held filter-A snapshot (design A):
    /// the snapshot's identity is the filter it was fetched under AND its
    /// generation.
    @MainActor
    func testRowsUnderAnotherFilterThanTheHeldSnapshotAreDropped() {
        let state = AppState()
        state.graphTableSnapshot = GraphSnapshot(
            nodes: [], edges: [], generation: 0, audioSummary: "",
            summaryCounts: GraphSnapshotCounts(notes: 0, links: 0, orphans: 0, unresolved: 0, filtered: false))
        state.graphTableSnapshotFilter = GraphFilter(includeAttachments: false, includeGhosts: true, orphansOnly: false)
        state.graphTableFilter = GraphFilter(includeAttachments: true, includeGhosts: true, orphansOnly: false)
        let token = state.issueGraphTableToken(sort: state.graphTableSort)
        XCTAssertFalse(state.receiveGraphTableRows(token: token, result: rows(["B"])), "rows under filter B against a filter-A snapshot")
        XCTAssertTrue(state.graphTableRows.isEmpty)
    }

    /// A result from a generation the held snapshot does not match is
    /// discarded (0b-2b), never applied.
    @MainActor
    func testStaleGenerationResultIsDiscarded() {
        let state = AppState()
        state.graphTableSnapshot = GraphSnapshot(
            nodes: [], edges: [], generation: 5, audioSummary: "",
            summaryCounts: GraphSnapshotCounts(notes: 0, links: 0, orphans: 0, unresolved: 0, filtered: false))
        let token = state.issueGraphTableToken(sort: GraphTableSort(column: .note, ascending: true))
        XCTAssertFalse(state.receiveGraphTableRows(token: token, result: rows(["Stale"], generation: 4)))
        XCTAssertTrue(state.graphTableRows.isEmpty, "a stale generation never lands")
        XCTAssertEqual(state.graphTableSort.column, .linksIn)
    }

    /// A preset after a Folder sort: the preset's token carries the DEFAULT
    /// sort, the rows and the accepted sort publish together, and the
    /// headline is row zero of THAT result.
    @MainActor
    func testPresetAfterFolderSortPublishesDefaultSortAndHeadlineTogether() {
        let state = AppState()
        let folder = state.issueGraphTableToken(sort: GraphTableSort(column: .folder, ascending: true))
        XCTAssertTrue(state.receiveGraphTableRows(token: folder, result: rows(["a", "b"])))
        XCTAssertEqual(state.graphTableSort.column, .folder)
        let preset = state.issueGraphTableToken(sort: GraphTableSort(column: .linksIn, ascending: false))
        let hubsFirst = GraphTableRows(
            generation: 0, total: 2,
            rows: [row(1, "hub", path: "hub.md", linksIn: 9), row(2, "leaf", path: "leaf.md", linksIn: 1)])
        XCTAssertTrue(state.receiveGraphTableRows(token: preset, result: hubsFirst))
        XCTAssertEqual(state.graphTableSort, GraphTableSort(column: .linksIn, ascending: false))
        XCTAssertEqual(
            state.graphPresetEvent(.mostLinked, rows: hubsFirst),
            .graphPreset(outcome: .mostLinked(label: "hub", inLinks: 9)))
    }

    // MARK: - The mode line (#1220; contract 35 DD-18, Term M1)

    /// A real session over a two-note vault whose `.slate/graph.json` holds
    /// `graphJSON` when one is given.
    @MainActor
    private func makeGraphState(graphJSON: String? = nil) async throws -> AppState {
        let root = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-graph-mode-\(UUID().uuidString)")
        vaultRoot = root
        let vault = root.appendingPathComponent("vault")
        try FileManager.default.createDirectory(
            at: vault.appendingPathComponent(".slate"), withIntermediateDirectories: true)
        try Data("[[b]]".utf8).write(to: vault.appendingPathComponent("a.md"))
        try Data("[[a]]".utf8).write(to: vault.appendingPathComponent("b.md"))
        if let graphJSON {
            try Data(graphJSON.utf8).write(to: vault.appendingPathComponent(".slate/graph.json"))
        }
        let store = RecentVaultsStore(fileURL: root.appendingPathComponent("recents.json"))
        let state = AppState(recentsStore: store, externalOpener: { _ in true })
        state.openVault(at: vault)
        await state.scanTask?.value
        return state
    }

    /// Open the Graph tab and mount its body as the editor area hosts it.
    @MainActor
    private func mountGraphTab(_ state: AppState) async throws {
        // A control's action dispatches through `NSApp.sendAction`, a silent
        // no-op until `NSApplication.shared` exists (AccessibleDataGridTests).
        _ = NSApplication.shared
        state.openGraphTab()
        let tabID = try XCTUnwrap(state.workspace.activeTab?.id)
        let appeared = XCTestExpectation(description: "the Graph tab body mounted")
        let window = NSWindow(
            contentRect: NSRect(x: 0, y: 0, width: 960, height: 640),
            styleMask: [.titled], backing: .buffered, defer: false)
        window.isReleasedWhenClosed = false
        window.contentView = NSHostingView(
            rootView: GraphContainerView(tabID: tabID).environmentObject(state)
                .onAppear { appeared.fulfill() })
        hostWindow = window
        window.makeKeyAndOrderFront(nil)
        await fulfillment(of: [appeared], timeout: 30)
    }

    /// The reader's own switch: the mode picker is AppKit's segmented control
    /// under SwiftUI, and selecting a segment then sending the control's
    /// action is what a click does.
    @MainActor
    private func switchMode(to mode: GraphSurfaceMode) throws {
        let content = try XCTUnwrap(hostWindow?.contentView, "the tab body is mounted")
        let picker = try XCTUnwrap(
            Self.firstSubview(of: NSSegmentedControl.self, in: content), "the mode picker is mounted")
        picker.selectedSegment = try XCTUnwrap(GraphSurfaceMode.allCases.firstIndex(of: mode))
        XCTAssertTrue(picker.sendAction(picker.action, to: picker.target), "the click reached the picker")
    }

    private static func firstSubview<T: NSView>(of type: T.Type, in root: NSView) -> T? {
        if let match = root as? T { return match }
        for child in root.subviews {
            if let match = firstSubview(of: type, in: child) { return match }
        }
        return nil
    }

    private struct Timeout: Error, CustomStringConvertible {
        let description: String
    }

    /// Turn the main run loop — where the body's updates and their
    /// `onChange` actions run — until `condition` holds.
    @MainActor
    private func waitFor(
        _ what: String, timeout: TimeInterval = 15, _ condition: () -> Bool
    ) async throws {
        let deadline = Date().addingTimeInterval(timeout)
        while !condition() {
            guard Date() < deadline else { throw Timeout(description: "timed out waiting for \(what)") }
            let turned = XCTestExpectation(description: "the main run loop turned")
            DispatchQueue.main.asyncAfter(deadline: .now() + 0.02) { turned.fulfill() }
            await fulfillment(of: [turned], timeout: 30)
        }
    }

    private static func modeLine(_ mode: GraphSurfaceMode) -> String {
        a11yRender(event: .graph(event: .graphMode(mode: mode))).text
    }

    /// #1220 (contract 35 DD-18; Term M1): a persisted Diagram mode is
    /// RESTORED at the seat and speaks no mode line — `GraphMode` is the
    /// switch's line. The restore-driven `onChange(of: mode)` used to skip
    /// only the focus bump and speak "Diagram mode." over the open. The
    /// restore's flag is spent by the restore, so the reader's own next
    /// switch still speaks.
    @MainActor
    func testARestoredDiagramSpeaksNoModeLine() async throws {
        let state = try await makeGraphState(graphJSON: #"{"version":1,"mode":"diagram"}"#)
        XCTAssertEqual(state.graphConfig.mode, .diagram, "the persisted mode loads with the vault")
        let vault = try XCTUnwrap(state.currentVaultURL)
        var posts: [String] = []
        state.graphAnnouncer = GraphAnnouncer(post: { text, _ in posts.append(text) })
        try await mountGraphTab(state)
        // The restore's `onChange(of: mode)` persists the mode it restored;
        // the save it schedules is the sign that the restore has run.
        try await waitFor("the restore's mode change") { state.graphConfigSaveGen[vault] != nil }
        state.graphAnnouncer.flushForTests()
        XCTAssertFalse(
            posts.contains(Self.modeLine(.diagram)), "a restored Diagram speaks no mode line: \(posts)")

        try switchMode(to: .table)
        try await waitFor("the reader's switch to Table to speak") {
            posts.contains(Self.modeLine(.table))
        }
        XCTAssertFalse(posts.contains(Self.modeLine(.diagram)), "the restore stayed silent: \(posts)")
    }

    /// #1220's other half: the silence is the restore's alone. The reader's
    /// own switch to Diagram speaks `GraphMode{Diagram}` once and persists
    /// the mode.
    @MainActor
    func testAUserSwitchToDiagramSpeaksTheModeLine() async throws {
        let state = try await makeGraphState()
        XCTAssertEqual(state.graphConfig.mode, .table, "no graph.json: the Table default")
        var posts: [String] = []
        state.graphAnnouncer = GraphAnnouncer(post: { text, _ in posts.append(text) })
        try await mountGraphTab(state)
        try switchMode(to: .diagram)
        try await waitFor("the reader's switch to Diagram to speak") {
            posts.contains(Self.modeLine(.diagram))
        }
        XCTAssertEqual(
            posts.filter { $0 == Self.modeLine(.diagram) }.count, 1, "spoken once: \(posts)")
        XCTAssertEqual(state.graphConfig.mode, .diagram, "the switch persists its mode")
    }
}
