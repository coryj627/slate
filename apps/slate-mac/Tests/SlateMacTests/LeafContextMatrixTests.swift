// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// U4-4 (#473) leaf-context matrix. The right-pane leaves bind
/// `appState.current*` — which, after the U1-2 architecture, IS the focused
/// tab's document — so leaf content tracking the focused note is correct by
/// construction. These tests pin that behavior (the spec's deliverable for the
/// "leaf context" half): switching the active tab, switching pane focus, and
/// closing a tab all rebind the outline / backlinks / tasks leaves to the new
/// active document; with no document open, the leaf-bound collections are empty
/// (the leaves render their empty states).
@MainActor
final class LeafContextMatrixTests: XCTestCase {

    private var tempDir: URL!

    override func setUpWithError() throws {
        try super.setUpWithError()
        tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-leaf-context-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: tempDir, withIntermediateDirectories: true)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: tempDir)
        try super.tearDownWithError()
    }

    /// A two-note vault with DISTINCT leaf content per note:
    /// - `alpha.md`: one H1 "Alpha", a task "- [ ] alpha task", links to beta.
    /// - `beta.md`:  one H1 "Beta",  a task "- [ ] beta task",  links to alpha.
    /// The cross-links make each note a backlink target of the other, so the
    /// Backlinks leaf has observably different content per focused note.
    private func makeOpenState() async throws -> AppState {
        let vault = tempDir.appendingPathComponent("vault-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        try "# Alpha\n\n- [ ] alpha task\n\nSee [[beta]].\n".write(
            to: vault.appendingPathComponent("alpha.md"), atomically: true, encoding: .utf8)
        try "# Beta\n\n- [ ] beta task\n\nSee [[alpha]].\n".write(
            to: vault.appendingPathComponent("beta.md"), atomically: true, encoding: .utf8)
        let store = RecentVaultsStore(
            fileURL: tempDir.appendingPathComponent("recents.json"))
        let state = AppState(recentsStore: store, externalOpener: { _ in true })
        state.openVault(at: vault)
        await state.scanTask?.value
        return state
    }

    /// Await every leaf-feeding load for the current selection.
    private func awaitLeafLoads(_ state: AppState) async {
        await state.noteLoadTask?.value      // headings (Outline)
        await state.linksLoadTask?.value     // backlinks + outgoing (Backlinks / Outgoing)
        await state.tasksLoadTask?.value     // tasks (Tasks)
    }

    private func select(_ path: String, in state: AppState) async {
        state.selectedFilePath = path
        await awaitLeafLoads(state)
    }

    // MARK: - Switch tab → leaves rebind

    /// Switching the active tab to another document rebinds outline, backlinks,
    /// and tasks to that document.
    func testSwitchTabRebindsOutlineBacklinksTasks() async throws {
        let state = try await makeOpenState()
        await select("alpha.md", in: state)

        XCTAssertEqual(state.currentNoteHeadings.map(\.text), ["Alpha"])
        XCTAssertEqual(state.currentNoteTasks.map(\.text), ["alpha task"])
        // beta.md links to alpha → alpha's backlinks include beta.
        XCTAssertEqual(state.currentBacklinks.map(\.sourcePath), ["beta.md"])

        // Open beta in a second tab and activate it (the identity funnel).
        let betaTab = state.workspace.openTab(.markdown(path: "beta.md"))
        state.activateTab(betaTab)
        await awaitLeafLoads(state)

        XCTAssertEqual(state.currentNoteHeadings.map(\.text), ["Beta"], "outline rebinds")
        XCTAssertEqual(state.currentNoteTasks.map(\.text), ["beta task"], "tasks rebind")
        XCTAssertEqual(
            state.currentBacklinks.map(\.sourcePath), ["alpha.md"], "backlinks rebind")
    }

    // MARK: - Switch pane focus → leaves rebind

    /// With two panes holding different documents, moving pane focus rebinds
    /// the leaves to the newly-focused pane's document — the same funnel a
    /// ⌘⌥arrow drives.
    func testSwitchPaneFocusRebindsLeaves() async throws {
        let state = try await makeOpenState()
        await select("alpha.md", in: state)

        // Split → the right pane duplicates alpha; open beta there.
        state.splitActivePane(axis: .horizontal)
        await select("beta.md", in: state)
        XCTAssertEqual(state.currentNoteHeadings.map(\.text), ["Beta"])

        // Focus the left pane (alpha) — leaves rebind to alpha.
        state.focusPane(.left)
        await awaitLeafLoads(state)
        XCTAssertEqual(
            state.currentNoteHeadings.map(\.text), ["Alpha"], "left pane's outline")
        XCTAssertEqual(
            state.currentBacklinks.map(\.sourcePath), ["beta.md"], "left pane's backlinks")

        // Focus the right pane (beta) — leaves rebind back to beta.
        state.focusPane(.right)
        await awaitLeafLoads(state)
        XCTAssertEqual(
            state.currentNoteHeadings.map(\.text), ["Beta"], "right pane's outline")
        XCTAssertEqual(
            state.currentBacklinks.map(\.sourcePath), ["alpha.md"], "right pane's backlinks")
    }

    // MARK: - Close tab → leaves fall back to the successor document

    /// Closing the active tab activates the successor (close-focus rule) and
    /// the leaves rebind to it.
    func testCloseTabFallsBackToSuccessorDocument() async throws {
        let state = try await makeOpenState()
        await select("alpha.md", in: state)
        let betaTab = state.workspace.openTab(.markdown(path: "beta.md"))
        state.activateTab(betaTab)
        await awaitLeafLoads(state)
        XCTAssertEqual(state.currentNoteHeadings.map(\.text), ["Beta"])

        // Close beta → focus falls back to alpha (the only sibling), leaves
        // rebind to alpha.
        state.performCloseTab(betaTab)
        await awaitLeafLoads(state)
        XCTAssertEqual(
            state.currentNoteHeadings.map(\.text), ["Alpha"], "outline follows successor")
        XCTAssertEqual(
            state.currentNoteTasks.map(\.text), ["alpha task"], "tasks follow successor")
    }

    // MARK: - No document → leaf empty states

    /// Closing the last tab empties the workspace; every leaf-bound collection
    /// clears, so the leaves render their empty states.
    func testNoDocumentClearsLeafCollections() async throws {
        let state = try await makeOpenState()
        await select("alpha.md", in: state)
        XCTAssertFalse(state.currentNoteHeadings.isEmpty)

        guard let onlyTab = state.workspace.model.activeGroup.activeTabID else {
            return XCTFail("expected one open tab")
        }
        state.performCloseTab(onlyTab)
        await awaitLeafLoads(state)

        XCTAssertTrue(state.workspace.model.isEmpty, "workspace is empty")
        XCTAssertNil(state.selectedFilePath, "no note selected")
        XCTAssertTrue(state.currentNoteHeadings.isEmpty, "outline leaf empty")
        XCTAssertTrue(state.currentBacklinks.isEmpty, "backlinks leaf empty")
        XCTAssertTrue(state.currentNoteTasks.isEmpty, "tasks leaf empty")
    }
}
