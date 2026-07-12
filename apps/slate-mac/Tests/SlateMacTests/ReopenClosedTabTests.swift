// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI
import XCTest

@testable import SlateMac

/// Reopen Closed Tab (#863): push-on-close at the WorkspaceState
/// funnel, pop-reopen through the standard open-target path (U1-2
/// dedup honored), the per-vault-session clear, the capacity bound,
/// the missing-file skip, and the menu-enablement mirror. Same
/// real-vault harness as `WorkspaceTabsTests`.
@MainActor
final class ReopenClosedTabTests: XCTestCase {

    private var tempDir: URL!

    override func setUpWithError() throws {
        tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("reopen-closed-tab-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: tempDir, withIntermediateDirectories: true)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: tempDir)
    }

    private func makeAppState() -> AppState {
        let store = RecentVaultsStore(
            fileURL: tempDir.appendingPathComponent("recents.json"))
        return AppState(recentsStore: store, externalOpener: { _ in true })
    }

    /// Vault with alpha/beta/gamma; opens it, scans, selects alpha.
    private func makeOpenState() async throws -> (AppState, URL) {
        let vault = tempDir.appendingPathComponent("vault-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        for name in ["alpha.md", "beta.md", "gamma.md"] {
            try "# \(name)\nbody of \(name)\n".write(
                to: vault.appendingPathComponent(name), atomically: true, encoding: .utf8)
        }
        let state = makeAppState()
        state.openVault(at: vault)
        await state.scanTask?.value
        state.selectedFilePath = "alpha.md"
        await state.noteLoadTask?.value
        return (state, vault)
    }

    /// The U1-2 multi-file flow: duplicate the active tab, then select
    /// another file — two tabs over two files.
    private func openSecondFile(_ state: AppState, path: String) async {
        state.newTab()
        state.selectedFilePath = path
        await state.noteLoadTask?.value
    }

    // MARK: Push on close (the funnel) + enablement signal

    func testCloseTabPushesRecordAndFlipsEnablementSignal() async throws {
        let (state, _) = try await makeOpenState()
        await openSecondFile(state, path: "beta.md")
        XCTAssertFalse(state.canReopenClosedTab, "no closes yet")
        XCTAssertTrue(state.workspace.closedTabs.isEmpty)

        state.requestCloseTab()  // beta, clean → closes immediately
        XCTAssertEqual(
            state.workspace.closedTabs.last?.item, .markdown(path: "beta.md"),
            "the user close funnel pushes the closed tab's item")
        XCTAssertTrue(
            state.canReopenClosedTab,
            "the published menu-enablement mirror flips with the stack")
    }

    func testDiscardGateResolutionPushesClosedTab() async throws {
        // The save/discard gate resolutions route through the same
        // funnel — a Discard close is reopenable like any other.
        let (state, _) = try await makeOpenState()
        await openSecondFile(state, path: "beta.md")
        state.updateEditorText("# beta.md\ndirty")
        state.requestCloseTab()
        XCTAssertNotNil(state.pendingTabClose, "dirty close gates first")
        XCTAssertTrue(
            state.workspace.closedTabs.isEmpty,
            "nothing is pushed while the close is still gated")

        state.resolveTabCloseDiscard()
        XCTAssertEqual(
            state.workspace.closedTabs.last?.item, .markdown(path: "beta.md"))
        XCTAssertTrue(state.canReopenClosedTab)
    }

    // MARK: Pop + reopen

    func testReopenRestoresClosedItemAndConsumesRecord() async throws {
        let (state, _) = try await makeOpenState()
        await openSecondFile(state, path: "beta.md")
        state.requestCloseTab()  // close beta
        XCTAssertEqual(state.workspace.model.allTabs.count, 1)

        state.reopenClosedTab()
        await state.noteLoadTask?.value

        XCTAssertEqual(state.workspace.model.allTabs.count, 2, "beta is back")
        XCTAssertEqual(state.loadedFilePath, "beta.md", "the reopened tab is active")
        XCTAssertEqual(
            state.workspace.model.activeGroup.activeTab?.item,
            .markdown(path: "beta.md"))
        XCTAssertFalse(state.canReopenClosedTab, "the record was consumed")
        XCTAssertTrue(state.workspace.closedTabs.isEmpty)
    }

    func testReopenHonorsDedupWhenItemAlreadyOpen() async throws {
        // U1-2 dedup: if the note was reopened by hand in the meantime,
        // ⇧⌘T activates the existing tab instead of duplicating — the
        // popped record is simply consumed.
        let (state, _) = try await makeOpenState()
        await openSecondFile(state, path: "beta.md")
        state.requestCloseTab()  // close beta → stack [beta]
        state.openFile("beta.md", target: .newTab)  // manual reopen
        await state.noteLoadTask?.value
        XCTAssertEqual(state.workspace.model.allTabs.count, 2)

        state.selectPreviousTab()  // park beta; alpha active
        await state.noteLoadTask?.value
        state.reopenClosedTab()
        await state.noteLoadTask?.value

        XCTAssertEqual(
            state.workspace.model.allTabs.count, 2,
            "dedup activated the existing beta tab — no duplicate")
        XCTAssertEqual(state.loadedFilePath, "beta.md")
        XCTAssertFalse(state.canReopenClosedTab, "the record was still consumed")
    }

    func testReopenSkipsMissingFileAndReopensNext() async throws {
        let (state, vault) = try await makeOpenState()
        await openSecondFile(state, path: "beta.md")
        await openSecondFile(state, path: "gamma.md")
        state.requestCloseTab()  // close gamma → stack [gamma]
        await state.noteLoadTask?.value
        state.requestCloseTab()  // close beta → stack [gamma, beta]
        await state.noteLoadTask?.value
        XCTAssertEqual(state.workspace.closedTabs.count, 2)

        // beta (the top record) vanishes from disk.
        try FileManager.default.removeItem(at: vault.appendingPathComponent("beta.md"))

        state.reopenClosedTab()
        await state.noteLoadTask?.value

        XCTAssertEqual(
            state.loadedFilePath, "gamma.md",
            "the missing beta record is skipped; the next record reopens")
        XCTAssertFalse(
            state.workspace.model.allTabs.contains { $0.item == .markdown(path: "beta.md") },
            "the missing file must not come back as a tab")
        XCTAssertTrue(state.workspace.closedTabs.isEmpty, "both records consumed")
        XCTAssertFalse(state.canReopenClosedTab)
    }

    /// Red-team F1 (chords PR): a PURE-SKIP reopen (every record dead)
    /// must be a no-op on pane focus and the live buffer fields. The
    /// first draft parked + focused the record's group BEFORE the
    /// existence check, so skipping a deleted cross-pane file stranded
    /// the previous pane's buffer under the destination tab's title.
    func testPureSkipReopenLeavesFocusAndBufferUntouched() async throws {
        let (state, vault) = try await makeOpenState()  // pane1: alpha
        state.splitActivePane(axis: .horizontal)  // pane2 (focused)
        await state.noteLoadTask?.value
        await openSecondFile(state, path: "beta.md")  // pane2: beta tab
        state.requestCloseTab()  // close beta → record carries pane2
        await state.noteLoadTask?.value
        XCTAssertEqual(state.workspace.closedTabs.count, 1)
        state.focusPane(.left)  // back to pane1
        await state.noteLoadTask?.value
        let groupBefore = state.workspace.model.activeGroupID
        let loadedBefore = state.loadedFilePath

        try FileManager.default.removeItem(
            at: vault.appendingPathComponent("beta.md"))
        state.reopenClosedTab()  // pure skip — the only record is dead
        await state.noteLoadTask?.value

        XCTAssertEqual(
            state.workspace.model.activeGroupID, groupBefore,
            "a pure-skip reopen must not move pane focus")
        XCTAssertEqual(
            state.loadedFilePath, loadedBefore,
            "a pure-skip reopen must not touch the live note fields")
        XCTAssertTrue(state.workspace.closedTabs.isEmpty)
        XCTAssertFalse(state.canReopenClosedTab)
    }

    func testReopenWithEmptyStackIsNoOp() async throws {
        let (state, _) = try await makeOpenState()
        let tabsBefore = state.workspace.model.allTabs
        state.reopenClosedTab()
        XCTAssertEqual(state.workspace.model.allTabs, tabsBefore)
    }

    // MARK: Per-vault-session semantics

    func testVaultCloseClearsClosedTabStack() async throws {
        let (state, _) = try await makeOpenState()
        await openSecondFile(state, path: "beta.md")
        state.requestCloseTab()
        XCTAssertTrue(state.canReopenClosedTab)

        state.closeVaultFromUserAction()  // clean → closes immediately
        XCTAssertNil(state.currentSession)
        XCTAssertTrue(
            state.workspace.closedTabs.isEmpty,
            "the stack is per-vault-session — cleared by the vault-close reset")
        XCTAssertFalse(state.canReopenClosedTab)

        // ⇧⌘T on the welcome screen stays inert (guarded on the vault).
        state.reopenClosedTab()
        XCTAssertTrue(state.workspace.model.isEmpty)
    }

    /// Codex review (chords PR): deleting a saved query / dashboard
    /// auto-closes its tabs through the recording funnel — the purge
    /// must strip those records (and any older ones for the same id),
    /// or ⇧⌘T resurrects the deleted entity as a failure tab.
    func testPurgeStripsRecordsForDeletedEntity() {
        let ws = WorkspaceState()
        let q1 = ws.openTab(.savedQuery(id: "q-1", name: "Q1"))
        _ = ws.openTab(.dashboard(id: "d-1", name: "D1"))
        let q1b = ws.openTab(.savedQuery(id: "q-1", name: "Q1"), allowDuplicate: true)
        _ = ws.close(q1)   // record 1 for q-1
        _ = ws.close(q1b)  // record 2 for q-1
        XCTAssertEqual(ws.closedTabs.count, 2)

        ws.purgeClosedTabs { record in
            if case .savedQuery(let id, _) = record.item { return id == "q-1" }
            return false
        }
        XCTAssertTrue(
            ws.closedTabs.isEmpty,
            "every record of the deleted id goes — old and new alike")

        // Unrelated records survive a targeted purge.
        let d = ws.openTab(.dashboard(id: "d-2", name: "D2"), activate: true)
        _ = ws.close(d)
        ws.purgeClosedTabs { record in
            if case .savedQuery = record.item { return true }
            return false
        }
        XCTAssertEqual(ws.closedTabs.count, 1, "dashboard record untouched")
    }

    // MARK: WorkspaceState-level bounds + placement

    func testClosedTabStackCapsAtCapacityEvictingOldest() {
        let workspace = WorkspaceState()
        let overflow = 5
        for index in 0..<(WorkspaceState.closedTabCapacity + overflow) {
            let id = workspace.openTab(.markdown(path: "note-\(index).md"))
            workspace.close(id)
        }
        XCTAssertEqual(workspace.closedTabs.count, WorkspaceState.closedTabCapacity)
        XCTAssertEqual(
            workspace.closedTabs.first?.item, .markdown(path: "note-\(overflow).md"),
            "oldest records evict first")
        XCTAssertEqual(
            workspace.closedTabs.last?.item,
            .markdown(
                path: "note-\(WorkspaceState.closedTabCapacity + overflow - 1).md"))
    }

    func testCloseRecordsOwningGroupPlacement() {
        let workspace = WorkspaceState()
        workspace.openTab(.markdown(path: "a.md"))
        let secondGroup = workspace.split(
            workspace.model.activeGroupID, axis: .horizontal)
        XCTAssertNotNil(secondGroup)
        let tabInSecond = workspace.model.activeGroup.activeTabID
        XCTAssertNotNil(tabInSecond)

        workspace.close(tabInSecond!)
        XCTAssertEqual(
            workspace.closedTabs.last?.groupID, secondGroup,
            "the record captures the pane the tab was closed from")
    }

    func testWorkspaceResetClearsStack() {
        let workspace = WorkspaceState()
        let id = workspace.openTab(.markdown(path: "a.md"))
        workspace.close(id)
        XCTAssertFalse(workspace.closedTabs.isEmpty)
        workspace.reset()
        XCTAssertTrue(workspace.closedTabs.isEmpty)
    }
}
