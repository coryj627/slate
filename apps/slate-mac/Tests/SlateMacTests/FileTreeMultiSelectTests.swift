// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import SwiftUI
import XCTest

@testable import SlateMac

/// #852: file-tree multi-select + batch move / delete.
///
/// The core new logic is a PURE selection model — `applySelectionClick` folds a
/// plain / ⌘ / ⇧ pointer click into `(selection, anchor, focus)` over a
/// flattened visible-row order — and the batch orchestration on AppState
/// (`batchMove` / `requestBatchDelete` / `batchDelete`), which route every item
/// through the SAME per-item `moveEntry` / `deleteEntry` funnel and post ONE
/// summary announcement. Both are unit-tested directly here. The SwiftUI wiring
/// that can't be driven from XCTest (the tap dispatch, the open-suppression
/// `.onChange`, the batch context menu) is pinned by source inspection — the
/// repo's `…ByInspection` pattern, comments + strings stripped so the tokens
/// must appear as LIVE code.
@MainActor
final class FileTreeMultiSelectTests: XCTestCase {

    // MARK: - Fixtures

    private var tempDir: URL!

    override func setUpWithError() throws {
        tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("multiselect-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: tempDir, withIntermediateDirectories: true)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: tempDir)
    }

    /// A real vault + opened AppState (mirrors StructuralUndoTests).
    private func makeVault(named: String = "vault", files: [String]) async throws -> (AppState, URL) {
        let vault = tempDir.appendingPathComponent(named)
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        for rel in files {
            let url = vault.appendingPathComponent(rel)
            try FileManager.default.createDirectory(
                at: url.deletingLastPathComponent(), withIntermediateDirectories: true)
            try "# \((rel as NSString).lastPathComponent)\n".write(
                to: url, atomically: true, encoding: .utf8)
        }
        let state = AppState(
            recentsStore: RecentVaultsStore(
                fileURL: tempDir.appendingPathComponent("recents-\(named).json")),
            externalOpener: { _ in true })
        state.openVault(at: vault)
        await state.scanTask?.value
        return (state, vault)
    }

    private func exists(_ vault: URL, _ rel: String) -> Bool {
        FileManager.default.fileExists(atPath: vault.appendingPathComponent(rel).path)
    }

    private func sel(_ path: String, dir: Bool = false) -> AppState.TreeSelection {
        AppState.TreeSelection(path: path, isDirectory: dir)
    }

    private func fileRow(_ path: String) -> FileTreeSidebar.RowID { .node(.file(path: path)) }

    /// The order handed to `applySelectionClick` in most tests: four file rows.
    private var order: [FileTreeSidebar.RowID] {
        ["a.md", "b.md", "c.md", "d.md"].map(fileRow)
    }

    private func clickEvent(_ modifiers: NSEvent.ModifierFlags) -> NSEvent? {
        NSEvent.keyEvent(
            with: .keyDown, location: .zero, modifierFlags: modifiers, timestamp: 0,
            windowNumber: 0, context: nil, characters: "x",
            charactersIgnoringModifiers: "x", isARepeat: false, keyCode: 0)
    }

    // MARK: - Modifier decode (selectionClick)

    func testSelectionClickDecodesPlainToggleRange() {
        XCTAssertEqual(FileTreeSidebar.selectionClick(from: clickEvent([])), .plain)
        XCTAssertEqual(FileTreeSidebar.selectionClick(from: clickEvent([.command])), .toggle)
        XCTAssertEqual(FileTreeSidebar.selectionClick(from: clickEvent([.shift])), .range)
        XCTAssertEqual(FileTreeSidebar.selectionClick(from: nil), .plain)
    }

    /// ⇧ wins over ⌘ (a ⇧⌘-click range-selects) — the documented simplification.
    func testShiftBeatsCommandForRange() {
        XCTAssertEqual(
            FileTreeSidebar.selectionClick(from: clickEvent([.shift, .command])), .range)
    }

    /// Unrelated modifiers (Caps Lock, Option) don't turn a plain click into a
    /// multi gesture.
    func testStrayModifiersStayPlain() {
        XCTAssertEqual(FileTreeSidebar.selectionClick(from: clickEvent([.capsLock])), .plain)
        XCTAssertEqual(FileTreeSidebar.selectionClick(from: clickEvent([.option])), .plain)
    }

    // MARK: - Plain click

    func testPlainClickSelectsOnlyThisRowAndAnchors() {
        let out = FileTreeSidebar.applySelectionClick(
            order: order, current: [fileRow("a.md"), fileRow("c.md")],
            anchor: fileRow("a.md"), clicked: fileRow("b.md"), click: .plain)
        XCTAssertEqual(out.selection, [fileRow("b.md")], "plain click collapses to one row")
        XCTAssertEqual(out.anchor, fileRow("b.md"))
        XCTAssertEqual(out.focus, fileRow("b.md"))
    }

    // MARK: - ⌘ toggle

    func testCommandClickAddsRowAndRepivotsAnchor() {
        let out = FileTreeSidebar.applySelectionClick(
            order: order, current: [fileRow("a.md")], anchor: fileRow("a.md"),
            clicked: fileRow("c.md"), click: .toggle)
        XCTAssertEqual(out.selection, [fileRow("a.md"), fileRow("c.md")])
        XCTAssertEqual(out.anchor, fileRow("c.md"), "a ⌘-click re-pivots the range anchor")
        XCTAssertEqual(out.focus, fileRow("c.md"))
    }

    func testCommandClickRemovesRowAndFocusesLastRemaining() {
        let out = FileTreeSidebar.applySelectionClick(
            order: order, current: [fileRow("a.md"), fileRow("b.md"), fileRow("d.md")],
            anchor: fileRow("a.md"), clicked: fileRow("b.md"), click: .toggle)
        XCTAssertEqual(out.selection, [fileRow("a.md"), fileRow("d.md")], "toggled b out")
        XCTAssertEqual(out.anchor, fileRow("b.md"))
        XCTAssertEqual(
            out.focus, fileRow("d.md"),
            "focus moves to the LAST still-selected row in visible order")
    }

    func testCommandClickRemovingLastRowEmptiesSetAndNilsFocus() {
        let out = FileTreeSidebar.applySelectionClick(
            order: order, current: [fileRow("c.md")], anchor: fileRow("c.md"),
            clicked: fileRow("c.md"), click: .toggle)
        XCTAssertTrue(out.selection.isEmpty)
        XCTAssertNil(out.focus)
    }

    // MARK: - ⇧ range

    func testShiftRangeSpansAnchorToClickedInclusive() {
        let out = FileTreeSidebar.applySelectionClick(
            order: order, current: [fileRow("b.md")], anchor: fileRow("b.md"),
            clicked: fileRow("d.md"), click: .range)
        XCTAssertEqual(out.selection, [fileRow("b.md"), fileRow("c.md"), fileRow("d.md")])
        XCTAssertEqual(out.anchor, fileRow("b.md"), "the anchor is UNCHANGED by a ⇧-click")
        XCTAssertEqual(out.focus, fileRow("d.md"))
    }

    /// A ⇧-click ABOVE the anchor spans upward — the range is order-agnostic.
    func testShiftRangeReversedStillInclusive() {
        let out = FileTreeSidebar.applySelectionClick(
            order: order, current: [fileRow("d.md")], anchor: fileRow("d.md"),
            clicked: fileRow("a.md"), click: .range)
        XCTAssertEqual(out.selection, Set(order))
        XCTAssertEqual(out.anchor, fileRow("d.md"))
        XCTAssertEqual(out.focus, fileRow("a.md"))
    }

    /// Successive ⇧-clicks grow AND shrink from a FIXED pivot (the anchor is set
    /// by the earlier plain/⌘ click and never moves under ⇧).
    func testSuccessiveShiftClicksReanchorFromFixedPivot() {
        // Plain-click b establishes the pivot.
        let first = FileTreeSidebar.applySelectionClick(
            order: order, current: [], anchor: nil, clicked: fileRow("b.md"), click: .plain)
        XCTAssertEqual(first.anchor, fileRow("b.md"))
        // ⇧-click d → span b…d.
        let grown = FileTreeSidebar.applySelectionClick(
            order: order, current: first.selection, anchor: first.anchor,
            clicked: fileRow("d.md"), click: .range)
        XCTAssertEqual(grown.selection, [fileRow("b.md"), fileRow("c.md"), fileRow("d.md")])
        // ⇧-click c (shrink) → span b…c, STILL pivoting on b (not on the last click).
        let shrunk = FileTreeSidebar.applySelectionClick(
            order: order, current: grown.selection, anchor: grown.anchor,
            clicked: fileRow("c.md"), click: .range)
        XCTAssertEqual(shrunk.selection, [fileRow("b.md"), fileRow("c.md")])
        XCTAssertEqual(shrunk.anchor, fileRow("b.md"))
    }

    func testShiftRangeWithNoUsableAnchorDegradesToPlain() {
        // nil anchor.
        let noAnchor = FileTreeSidebar.applySelectionClick(
            order: order, current: [], anchor: nil, clicked: fileRow("c.md"), click: .range)
        XCTAssertEqual(noAnchor.selection, [fileRow("c.md")])
        XCTAssertEqual(noAnchor.anchor, fileRow("c.md"), "adopts clicked as the new pivot")
        // Off-list anchor (a row no longer visible).
        let stale = FileTreeSidebar.applySelectionClick(
            order: order, current: [], anchor: fileRow("gone.md"),
            clicked: fileRow("c.md"), click: .range)
        XCTAssertEqual(stale.selection, [fileRow("c.md")])
    }

    // MARK: - topLevelSelection dedup

    func testTopLevelSelectionDropsDescendantsOfSelectedFolders() {
        let items = [sel("folder", dir: true), sel("folder/x.md"), sel("other.md")]
        XCTAssertEqual(
            AppState.topLevelSelection(items).map(\.path), ["folder", "other.md"],
            "a folder AND a file inside it collapse to just the folder")
    }

    func testTopLevelSelectionKeepsIndependentItems() {
        let items = [sel("a.md"), sel("b.md"), sel("dir", dir: true)]
        XCTAssertEqual(AppState.topLevelSelection(items).map(\.path), ["a.md", "b.md", "dir"])
    }

    func testTopLevelSelectionCollapsesNestedFolders() {
        let items = [sel("a", dir: true), sel("a/b", dir: true), sel("a/b/c.md")]
        XCTAssertEqual(AppState.topLevelSelection(items).map(\.path), ["a"])
    }

    func testTopLevelSelectionPreservesOrder() {
        let items = [sel("z.md"), sel("a.md"), sel("m.md")]
        XCTAssertEqual(AppState.topLevelSelection(items).map(\.path), ["z.md", "a.md", "m.md"])
    }

    // MARK: - Announcement builders

    func testBatchMoveAnnouncementPhrasing() {
        XCTAssertEqual(
            AppState.batchMoveAnnouncement(count: 3, destination: "Archive"),
            "Moved 3 items to Archive.")
        XCTAssertEqual(
            AppState.batchMoveAnnouncement(count: 1, destination: "Archive"),
            "Moved 1 item to Archive.")
        XCTAssertEqual(
            AppState.batchMoveAnnouncement(count: 2, destination: ""),
            "Moved 2 items to vault root.")
        XCTAssertEqual(
            AppState.batchMoveAnnouncement(count: 2, destination: "a/b/Deep"),
            "Moved 2 items to Deep.", "the destination reads as its last component")
    }

    func testBatchDeleteAnnouncementPhrasing() {
        XCTAssertEqual(AppState.batchDeleteAnnouncement(count: 3), "Moved 3 items to Trash.")
        XCTAssertEqual(AppState.batchDeleteAnnouncement(count: 1), "Moved 1 item to Trash.")
    }

    // MARK: - batchMove routes per item + one announcement

    func testBatchMoveRoutesEachItemAndAnnouncesOnce() async throws {
        let (state, vault) = try await makeVault(files: ["a.md", "b.md", "dest/x.md"])
        await state.batchMove([sel("a.md"), sel("b.md")], to: "dest").value

        XCTAssertTrue(exists(vault, "dest/a.md"), "a moved into dest")
        XCTAssertTrue(exists(vault, "dest/b.md"), "b moved into dest")
        XCTAssertFalse(exists(vault, "a.md"))
        XCTAssertFalse(exists(vault, "b.md"))
        XCTAssertEqual(
            state.lastMutationAnnouncement, "Moved 2 items to dest.",
            "ONE summary announcement, not one per item")
    }

    /// #871: the batch routes the per-item `moveEntry`, which each push an
    /// inverse — a 2-item batch is 2 ⌘Z (we deliberately don't coalesce).
    func testBatchMoveRecordsAPerItemStructuralInverse() async throws {
        let (state, _) = try await makeVault(files: ["a.md", "b.md", "dest/x.md"])
        await state.batchMove([sel("a.md"), sel("b.md")], to: "dest").value
        XCTAssertEqual(
            state.structuralUndoStack.count, 2,
            "each moved item recorded its own inverse (#871 is per-op)")
    }

    /// No-op items (already directly in the destination) are skipped so the
    /// announced count is truthful, and illegal folder-into-own-subtree moves
    /// are skipped rather than round-tripping to a backend error.
    func testBatchMoveSkipsNoOpsAndCountsOnlyRealMoves() async throws {
        let (state, vault) = try await makeVault(files: ["dest/a.md", "b.md", "dest/x.md"])
        // a.md is ALREADY in dest → skipped; only b.md actually moves.
        await state.batchMove([sel("dest/a.md"), sel("b.md")], to: "dest").value
        XCTAssertTrue(exists(vault, "dest/b.md"))
        XCTAssertEqual(
            state.lastMutationAnnouncement, "Moved 1 item to dest.",
            "the no-op item is not counted")
    }

    /// #852 red-team: a batch item that FAILS (a name collision at the
    /// destination) must NOT be counted — moveEntry returns a non-nil task even
    /// on failure, so the summary must reflect ACTUAL successes only.
    func testBatchMoveCountsOnlySuccessesOnPartialFailure() async throws {
        // dest/ already holds a.md → moving a.md there collides; b.md succeeds.
        let (state, vault) = try await makeVault(files: ["a.md", "b.md", "dest/a.md"])
        await state.batchMove([sel("a.md"), sel("b.md")], to: "dest").value
        XCTAssertTrue(exists(vault, "dest/b.md"), "b moved")
        XCTAssertTrue(exists(vault, "a.md"), "a stayed put — the collision failed")
        XCTAssertEqual(
            state.lastMutationAnnouncement, "Moved 1 item to dest.",
            "only the item that ACTUALLY moved is counted (not the collision)")
    }

    // MARK: - batchDelete routes per item + one announcement

    func testBatchDeleteRoutesEachItemAndAnnouncesOnce() async throws {
        let (state, vault) = try await makeVault(files: ["a.md", "b.md", "c.md"])
        await state.batchDelete([sel("a.md"), sel("b.md")]).value

        XCTAssertFalse(exists(vault, "a.md"), "a trashed")
        XCTAssertFalse(exists(vault, "b.md"), "b trashed")
        XCTAssertTrue(exists(vault, "c.md"), "c untouched")
        XCTAssertEqual(
            state.lastMutationAnnouncement, "Moved 2 items to Trash.",
            "ONE summary announcement, not one per item")
    }

    /// #852 red-team: a batch delete whose item FAILS (here, externally removed
    /// from disk before the trash op) must not be counted as trashed.
    func testBatchDeleteCountsOnlySuccessesOnPartialFailure() async throws {
        let (state, vault) = try await makeVault(files: ["a.md", "b.md"])
        // a.md is gone before the batch runs → its trash op fails; b.md succeeds.
        try FileManager.default.removeItem(at: vault.appendingPathComponent("a.md"))
        await state.batchDelete([sel("a.md"), sel("b.md")]).value
        XCTAssertFalse(exists(vault, "b.md"), "b trashed")
        XCTAssertEqual(
            state.lastMutationAnnouncement, "Moved 1 item to Trash.",
            "the item that failed to trash is not counted")
    }

    /// #852 red-team: the batch menu pluralizes on the (deduped) top-level count
    /// so a folder+descendant selection never reads "Move 1 Items to…".
    func testBatchMenuPluralizesCountByInspection() throws {
        let src = try Self.normalizedSidebarSource()
        XCTAssertTrue(
            src.contains("let noun = count == 1 ?"),
            "the batch menu chooses Item vs Items on the actual count")
    }

    // MARK: - requestBatchDelete confirmation gate (#860 at batch scope)

    func testRequestBatchDeleteStagesConfirmationWhenNonEmptyFolderIncluded() async throws {
        let (state, _) = try await makeVault(files: ["a.md", "folder/x.md"])
        state.requestBatchDelete([sel("a.md"), sel("folder", dir: true)])
        let pending = try XCTUnwrap(state.pendingBatchDelete, "a non-empty folder must confirm")
        XCTAssertEqual(pending.itemCount, 2)
        XCTAssertEqual(pending.nonEmptyFolderCount, 1)
    }

    func testRequestBatchDeleteAllFilesTrashesStraightThrough() async throws {
        let (state, vault) = try await makeVault(files: ["a.md", "b.md"])
        state.requestBatchDelete([sel("a.md"), sel("b.md")])
        XCTAssertNil(state.pendingBatchDelete, "an all-files batch does not confirm")
        await state.pendingStructuralTaskForTesting?.value
        XCTAssertFalse(exists(vault, "a.md"))
        XCTAssertFalse(exists(vault, "b.md"))
        XCTAssertEqual(state.lastMutationAnnouncement, "Moved 2 items to Trash.")
    }

    /// A single-item "batch" routes through the SINGLE #860 funnel so it gets
    /// the exact single-folder confirmation copy.
    func testRequestBatchDeleteSingleItemUsesSingleFunnel() async throws {
        let (state, _) = try await makeVault(files: ["folder/x.md", "a.md"])
        state.requestBatchDelete([sel("folder", dir: true)])
        XCTAssertNil(state.pendingBatchDelete)
        XCTAssertEqual(
            state.pendingFolderDelete?.path, "folder",
            "one item routes to the single-node #860 confirmation")
    }

    func testConfirmPendingBatchDeleteTrashesTheStagedItems() async throws {
        let (state, vault) = try await makeVault(files: ["a.md", "folder/x.md"])
        state.requestBatchDelete([sel("a.md"), sel("folder", dir: true)])
        XCTAssertNotNil(state.pendingBatchDelete)
        state.confirmPendingBatchDelete()
        XCTAssertNil(state.pendingBatchDelete, "confirming clears the staged batch")
        await state.pendingStructuralTaskForTesting?.value
        XCTAssertFalse(exists(vault, "a.md"))
        XCTAssertFalse(exists(vault, "folder"))
        XCTAssertEqual(state.lastMutationAnnouncement, "Moved 2 items to Trash.")
    }

    func testCancelPendingBatchDeleteDeletesNothing() async throws {
        let (state, vault) = try await makeVault(files: ["a.md", "folder/x.md"])
        state.requestBatchDelete([sel("a.md"), sel("folder", dir: true)])
        state.cancelPendingBatchDelete()
        XCTAssertNil(state.pendingBatchDelete)
        XCTAssertTrue(exists(vault, "a.md"), "cancel trashes nothing")
        XCTAssertTrue(exists(vault, "folder/x.md"))
    }

    /// The single-node move path (U2-5) is untouched — `moveEntry` still fires
    /// its own per-item announcement, so single-select behavior is unchanged.
    func testSingleMoveStillAnnouncesPerItem() async throws {
        let (state, vault) = try await makeVault(files: ["a.md", "dest/x.md"])
        await state.moveEntry(path: "a.md", isDirectory: false, to: "dest")?.value
        XCTAssertTrue(exists(vault, "dest/a.md"))
        XCTAssertEqual(state.lastMutationAnnouncement, "Moved a.md to dest.")
    }

    // MARK: - Cross-vault safety

    func testBatchPendingFieldsClearOnVaultClose() async throws {
        let (state, _) = try await makeVault(files: ["a.md", "folder/x.md"])
        state.pendingBatchMove = AppState.BatchMove(items: [sel("a.md")])
        state.requestBatchDelete([sel("a.md"), sel("folder", dir: true)])
        XCTAssertNotNil(state.pendingBatchDelete)
        state.closeVault()
        XCTAssertNil(state.pendingBatchMove, "batch sheet dies with the vault")
        XCTAssertNil(state.pendingBatchDelete, "batch confirmation dies with the vault")
    }

    // MARK: - View wiring (source inspection)

    /// The delicate #643 + #852 tap dispatch: BOTH row taps read the live
    /// modifier flags via `selectionClick(from: NSApp.currentEvent)` and fork
    /// plain → `applyPlainSelection` vs ⌘/⇧ → `applyMultiSelectClick`. Native
    /// `List(selection: Set<>)` ⌘/⇧-click can't drive this (the `.onDrag`
    /// swallows native selection), so the tokens MUST appear as live code.
    func testTapDispatchReadsModifiersAndForksByInspection() throws {
        let src = try Self.normalizedSidebarSource()
        XCTAssertTrue(
            src.contains("let click = Self.selectionClick(from: NSApp.currentEvent)"),
            "the tap handler must read the live modifiers itself")
        XCTAssertTrue(
            src.contains(
                "if click == .plain { applyPlainSelection(.node(node.nodeID)) } "
                    + "else { applyMultiSelectClick(.node(node.nodeID), click: click) }"),
            "plain forks to single-select-opens; ⌘/⇧ forks to multi")
    }

    /// A multi gesture SUPPRESSES the open: `applyMultiSelectClick` raises the
    /// one-shot suppress flag BEFORE moving `listSelection`, and the
    /// `.onChange(of: listSelection)` consumes it and returns before the open —
    /// so a live ⌘ can't mis-route the focus move to a new tab (#852 point 2/4).
    func testMultiSelectSuppressesOpenByInspection() throws {
        let src = try Self.normalizedSidebarSource()
        // #852 red-team: the one-shot must be armed ONLY when the focus
        // assignment will actually fire .onChange — a same-value assignment is a
        // no-op that would strand the flag true (swallowing a later open AND
        // leaving multiSelection stale for a wrong-target ⌘⌫). The guard must
        // be present, immediately before the focus move.
        XCTAssertTrue(
            src.contains(
                "if listSelection != outcome.focus { suppressOpenForSelectionChange = true } "
                    + "listSelection = outcome.focus"),
            "suppress is armed conditionally (only when focus changes), then focus moves")
        XCTAssertTrue(
            src.contains(
                "if suppressOpenForSelectionChange { suppressOpenForSelectionChange = false "
                    + "announceFocusedFileSelection( newSelection, suppressed: announcementIsSuppressed) return }"),
            "the onChange path consumes suppression, speaks the focused row, and returns before open")
        XCTAssertTrue(
            src.contains("if count == 1, case let .node(.file(path)) = outcome.focus {"),
            "a collapse-to-one-row opens explicitly (current tab), not via the ⌘-live onChange")
    }

    /// The batch context menu is armed only when the right-clicked row is part
    /// of a ≥2-row selection; both row menus branch on `isInMultiSelection`.
    func testContextMenuBranchesToBatchByInspection() throws {
        let src = try Self.normalizedSidebarSource()
        XCTAssertTrue(
            src.contains("if isInMultiSelection(node) { batchManagementMenu(for: node) }"),
            "both row context menus swap to the batch menu for a multi-selected row")
        XCTAssertTrue(
            src.contains("appState.pendingBatchMove = AppState.BatchMove(items: items)"),
            "batch Move routes through pendingBatchMove")
        XCTAssertTrue(
            src.contains("appState.requestBatchDelete(items)"),
            "batch Trash routes through requestBatchDelete")
    }

    /// The ⌘⌫ chord routes the WHOLE multi-selection to the batch delete funnel
    /// (Move to Trash is a batch action) while single-item commands stay on the
    /// focus. Both delete handlers call the shared helper.
    func testKeyboardDeleteRoutesToBatchWhenMultiSelectedByInspection() throws {
        let src = try Self.normalizedSidebarSource()
        XCTAssertTrue(
            src.contains("requestDeleteFromKeyboard()"),
            "both ⌘⌫ handlers route through the shared delete helper")
        XCTAssertTrue(
            src.contains(
                "if hasMultiSelection { appState.requestBatchDelete(selectedNodesForBatch) return }"),
            "the keyboard delete helper routes the whole selection to the batch funnel")
    }

    /// The batch Move-to-folder sheet and the batch-delete confirmation are
    /// wired into MainSplitView.
    func testMainSplitViewWiresBatchSurfacesByInspection() throws {
        let src = try Self.normalizedSource("MainSplitView.swift")
        XCTAssertTrue(
            src.contains("MoveToFolderSheet(batch: batch)"),
            "the batch move sheet is presented from pendingBatchMove")
        XCTAssertTrue(
            src.contains("appState.confirmPendingBatchDelete()"),
            "the batch-delete alert confirms through the funnel")
    }

    // MARK: - Codex finding 4: batchTargets is ordered + pruned + path-validated

    private func vfile(_ path: String) -> (rowID: FileTreeSidebar.RowID, path: String, isDirectory: Bool) {
        (rowID: .node(.file(path: path)), path: path, isDirectory: false)
    }
    private func vdir(_ path: String, id: Int64) -> (rowID: FileTreeSidebar.RowID, path: String, isDirectory: Bool) {
        (rowID: .node(.dir(id)), path: path, isDirectory: true)
    }
    /// A path snapshot in which every visible row's snapshot == its current path
    /// (i.e. nothing repointed) — the common case for the ordering/dedup tests.
    private func snap(_ order: [(rowID: FileTreeSidebar.RowID, path: String, isDirectory: Bool)])
        -> [FileTreeSidebar.RowID: String] {
        Dictionary(uniqueKeysWithValues: order.map { ($0.rowID, $0.path) })
    }

    /// The batch targets follow the VISIBLE row order, not `Set` iteration order
    /// (which is nondeterministic) — otherwise a multi-folder move into an empty
    /// dest picks an arbitrary basename-collision winner.
    func testBatchTargetsAreInVisibleRowOrderNotSetOrder() {
        let order = [vfile("z.md"), vfile("a.md"), vfile("m.md")]
        let selection: Set<FileTreeSidebar.RowID> = [
            .node(.file(path: "z.md")), .node(.file(path: "a.md")), .node(.file(path: "m.md")),
        ]
        XCTAssertEqual(
            FileTreeSidebar.batchTargets(
                visibleOrder: order, selection: selection, snapshot: snap(order)).map(\.path),
            ["z.md", "a.md", "m.md"], "targets preserve the visible order, not sorted/Set order")
    }

    /// A selected row that was collapsed / deleted away (a stale id absent from
    /// the live visible order) is PRUNED — so the announced count and the menu
    /// label match what actually acts.
    func testBatchTargetsPruneStaleIdsAbsentFromVisibleOrder() {
        let order = [vfile("a.md"), vfile("b.md")]
        let selection: Set<FileTreeSidebar.RowID> = [
            .node(.file(path: "a.md")), .node(.file(path: "b.md")),
            .node(.file(path: "gone.md")),  // selected earlier, now collapsed away
        ]
        let targets = FileTreeSidebar.batchTargets(
            visibleOrder: order, selection: selection, snapshot: snap(order))
        XCTAssertEqual(targets.map(\.path), ["a.md", "b.md"], "the stale id is dropped")
    }

    /// A folder AND a descendant both selected collapse to just the folder
    /// (topLevelSelection) for the OPERATION — while the PRE-dedup pruned count
    /// (mode / announcement) still sees both rows (Codex finding 3).
    func testBatchTargetsDedupWhilePrunedCountSeesBothRows() {
        let order = [vdir("folder", id: 1), vfile("folder/x.md"), vfile("other.md")]
        let selection: Set<FileTreeSidebar.RowID> = [
            .node(.dir(1)), .node(.file(path: "folder/x.md")), .node(.file(path: "other.md")),
        ]
        // Operation targets: deduped (folder swallows its child).
        XCTAssertEqual(
            FileTreeSidebar.batchTargets(
                visibleOrder: order, selection: selection, snapshot: snap(order)).map(\.path),
            ["folder", "other.md"], "descendant deduped; folder + sibling kept in order")
        // Mode / announcement: PRE-dedup — all three visible rows count.
        XCTAssertEqual(
            FileTreeSidebar.prunedSelection(
                visibleOrder: order, selection: selection, snapshot: snap(order)).count,
            3, "the pre-dedup pruned count sees every visible selected row")
    }

    /// Codex finding 3 in miniature: a folder + a child inside it are TWO visible
    /// selected rows (multi mode), yet the OPERATION targets just [folder].
    func testFolderPlusChildIsMultiModeButOperatesOnFolderOnly() {
        let order = [vdir("F", id: 7), vfile("F/note.md")]
        let selection: Set<FileTreeSidebar.RowID> = [.node(.dir(7)), .node(.file(path: "F/note.md"))]
        let pruned = FileTreeSidebar.prunedSelection(
            visibleOrder: order, selection: selection, snapshot: snap(order))
        let targets = FileTreeSidebar.batchTargets(
            visibleOrder: order, selection: selection, snapshot: snap(order))
        XCTAssertEqual(pruned.count, 2, "two visible selected rows → multi-select MODE (≥2)")
        XCTAssertEqual(targets.map(\.path), ["F"], "the op acts on the folder only (Move 1 Item)")
    }

    // MARK: - Codex finding 4: reused-id path reconciliation

    /// A selected dir id whose CURRENT path differs from its selection-time
    /// snapshot (a reused SQLite id now pointing at a new folder) is DROPPED —
    /// so the new folder is not shown selected / not a batch target.
    func testPrunedSelectionDropsRepointedReusedId() {
        // id 5 was selected as "old"; after a rescan the SAME id is "new".
        let order = [vdir("new", id: 5), vfile("keep.md")]
        let selection: Set<FileTreeSidebar.RowID> = [.node(.dir(5)), .node(.file(path: "keep.md"))]
        let snapshot: [FileTreeSidebar.RowID: String] = [
            .node(.dir(5)): "old", .node(.file(path: "keep.md")): "keep.md",
        ]
        XCTAssertEqual(
            FileTreeSidebar.prunedSelection(
                visibleOrder: order, selection: selection, snapshot: snapshot).map(\.path),
            ["keep.md"], "the repointed reused-id folder is dropped; the file stays")
    }

    /// `reconcileSelection` drops a repointed id (resolves to a different path),
    /// keeps a matching id, and keeps a transiently-unresolvable id (mid-refetch).
    func testReconcileSelectionDropsRepointedKeepsMatchingAndTransient() {
        let selection: Set<FileTreeSidebar.RowID> = [
            .node(.dir(5)),  // repointed: snapshot "old", resolves "new"
            .node(.dir(6)),  // matching: snapshot "keep", resolves "keep"
            .node(.dir(7)),  // transient: snapshot "later", resolves nil
        ]
        let snapshot: [FileTreeSidebar.RowID: String] = [
            .node(.dir(5)): "old", .node(.dir(6)): "keep", .node(.dir(7)): "later",
        ]
        let resolve: (FileTreeSidebar.RowID) -> String? = { rowID in
            switch rowID {
            case .node(.dir(5)): return "new"
            case .node(.dir(6)): return "keep"
            default: return nil
            }
        }
        let result = FileTreeSidebar.reconcileSelection(
            selection: selection, snapshot: snapshot, resolve: resolve)
        XCTAssertFalse(result.selection.contains(.node(.dir(5))), "repointed id dropped")
        XCTAssertTrue(result.selection.contains(.node(.dir(6))), "matching id kept")
        XCTAssertTrue(result.selection.contains(.node(.dir(7))), "transient (nil-resolve) id kept")
    }

    // MARK: - Codex round-4 finding 2: in-app rename/move REMAPS (not drops)

    /// A KNOWN in-app folder RENAME (id preserved, path intentionally changed)
    /// remaps the selected folder's snapshot to the new path and keeps it
    /// selected + focused + anchored — multi-mode preserved (test b).
    func testRemapFolderRenameKeepsItSelectedUnderNewPath() {
        let selection: Set<FileTreeSidebar.RowID> = [.node(.dir(7)), .node(.file(path: "G.md"))]
        let snapshot: [FileTreeSidebar.RowID: String] = [.node(.dir(7)): "F", .node(.file(path: "G.md")): "G.md"]
        let r = FileTreeSidebar.remapSelectionForMove(
            selection: selection, snapshot: snapshot,
            focus: .node(.dir(7)), anchor: .node(.dir(7)), anchorSnapshot: "F",
            oldPath: "F", newPath: "F2")
        XCTAssertTrue(r.selection.contains(.node(.dir(7))), "the dir id is preserved (still selected)")
        XCTAssertTrue(r.selection.contains(.node(.file(path: "G.md"))), "the sibling stays selected")
        XCTAssertEqual(r.snapshot[.node(.dir(7))], "F2", "the folder's snapshot follows to the new path")
        XCTAssertEqual(r.focus, .node(.dir(7)), "focus stays on the renamed folder")
        XCTAssertEqual(r.anchor, .node(.dir(7)), "anchor stays coherent")
        XCTAssertEqual(r.anchorSnapshot, "F2", "the anchor snapshot follows to the new path")
    }

    /// A KNOWN in-app folder MOVE likewise remaps the snapshot to the new parent
    /// path — and remaps a selected DESCENDANT file's RowID + snapshot (test c).
    func testRemapFolderMoveRemapsFolderAndDescendantFile() {
        let selection: Set<FileTreeSidebar.RowID> = [
            .node(.dir(1)), .node(.file(path: "folder/x.md")),
        ]
        let snapshot: [FileTreeSidebar.RowID: String] = [
            .node(.dir(1)): "folder", .node(.file(path: "folder/x.md")): "folder/x.md",
        ]
        let r = FileTreeSidebar.remapSelectionForMove(
            selection: selection, snapshot: snapshot,
            focus: .node(.dir(1)), anchor: nil, anchorSnapshot: nil,
            oldPath: "folder", newPath: "dest/folder")
        XCTAssertTrue(r.selection.contains(.node(.dir(1))), "folder dir id preserved")
        XCTAssertEqual(r.snapshot[.node(.dir(1))], "dest/folder", "folder snapshot moved")
        XCTAssertTrue(
            r.selection.contains(.node(.file(path: "dest/folder/x.md"))),
            "the descendant FILE row is re-keyed to its new path")
        XCTAssertFalse(
            r.selection.contains(.node(.file(path: "folder/x.md"))), "the old file row is gone")
        XCTAssertEqual(r.snapshot[.node(.file(path: "dest/folder/x.md"))], "dest/folder/x.md")
    }

    /// A FILE rename re-keys the focus RowID (files are path-keyed).
    func testRemapFileRenameRekeysFocus() {
        let selection: Set<FileTreeSidebar.RowID> = [.node(.file(path: "a.md"))]
        let snapshot: [FileTreeSidebar.RowID: String] = [.node(.file(path: "a.md")): "a.md"]
        let r = FileTreeSidebar.remapSelectionForMove(
            selection: selection, snapshot: snapshot,
            focus: .node(.file(path: "a.md")), anchor: .node(.file(path: "a.md")), anchorSnapshot: "a.md",
            oldPath: "a.md", newPath: "b.md")
        XCTAssertEqual(r.selection, [.node(.file(path: "b.md"))])
        XCTAssertEqual(r.focus, .node(.file(path: "b.md")))
        XCTAssertEqual(r.anchor, .node(.file(path: "b.md")))
    }

    /// Entries UNAFFECTED by the rename/move keep their RowID + snapshot.
    func testRemapLeavesUnaffectedEntriesUntouched() {
        let selection: Set<FileTreeSidebar.RowID> = [.node(.dir(9)), .node(.file(path: "sib.md"))]
        let snapshot: [FileTreeSidebar.RowID: String] = [.node(.dir(9)): "Other", .node(.file(path: "sib.md")): "sib.md"]
        let r = FileTreeSidebar.remapSelectionForMove(
            selection: selection, snapshot: snapshot, focus: .node(.dir(9)), anchor: nil,
            anchorSnapshot: nil, oldPath: "F", newPath: "F2")
        XCTAssertEqual(r.selection, selection, "unrelated entries are untouched")
        XCTAssertEqual(r.snapshot[.node(.dir(9))], "Other")
    }

    // MARK: - Codex round-5: the range ANCHOR is snapshotted + reconciled + remapped

    /// (a/d) The anchor reconcile rule: CLEARED only on a confirmed path mismatch
    /// (reused id); KEPT when transiently unresolvable (mid-refetch) or matching.
    func testAnchorSurvivesReconcileRule() {
        XCTAssertFalse(
            FileTreeSidebar.anchorSurvivesReconcile(snapshot: "Old", resolved: "New"),
            "a reused-id anchor (resolves to a different path) is cleared")
        XCTAssertTrue(
            FileTreeSidebar.anchorSurvivesReconcile(snapshot: "Old", resolved: nil),
            "a transiently-unresolvable anchor (mid-refetch) is retained")
        XCTAssertTrue(
            FileTreeSidebar.anchorSurvivesReconcile(snapshot: "Keep", resolved: "Keep"),
            "a still-matching anchor is retained")
        XCTAssertTrue(
            FileTreeSidebar.anchorSurvivesReconcile(snapshot: nil, resolved: "Anything"),
            "no snapshot ⇒ nothing to invalidate against")
    }

    /// (b) A DESELECTED descendant anchor under a KNOWN moved folder is remapped
    /// to follow the move — even though it is NOT in the selection.
    func testRemapMovesDeselectedDescendantAnchor() {
        // Only the folder is selected; the anchor is a deselected file inside it.
        let selection: Set<FileTreeSidebar.RowID> = [.node(.dir(1))]
        let snapshot: [FileTreeSidebar.RowID: String] = [.node(.dir(1)): "folder"]
        let r = FileTreeSidebar.remapSelectionForMove(
            selection: selection, snapshot: snapshot,
            focus: .node(.dir(1)),
            anchor: .node(.file(path: "folder/x.md")), anchorSnapshot: "folder/x.md",
            oldPath: "folder", newPath: "dest/folder")
        XCTAssertEqual(
            r.anchor, .node(.file(path: "dest/folder/x.md")),
            "the deselected descendant anchor follows the move (re-keyed)")
        XCTAssertEqual(r.anchorSnapshot, "dest/folder/x.md", "and its snapshot follows too")
    }

    /// (c) The pure model makes a ⌘-REMOVED (deselected) row the anchor — so the
    /// view's `setSelectionAnchor` snapshots it (a deselected row still gets a
    /// snapshot). This pins the pure half; the wiring is asserted by inspection.
    func testCommandRemoveMakesRemovedRowTheAnchor() {
        let order = ["A", "Old", "Z"].map(fileRow)
        let out = FileTreeSidebar.applySelectionClick(
            order: order, current: [fileRow("A"), fileRow("Old")], anchor: fileRow("A"),
            clicked: fileRow("Old"), click: .toggle)
        XCTAssertEqual(out.selection, [fileRow("A")], "Old is ⌘-removed")
        XCTAssertEqual(
            out.anchor, fileRow("Old"),
            "the DESELECTED removed row becomes the anchor (must be snapshotted)")
    }

    /// #852 Codex round-5 (source wiring): the anchor is snapshotted on every set
    /// (incl. deselected rows), reconciled independently before the selection
    /// early-return, remapped across known moves, and path-validated for ranges.
    func testAnchorSnapshotReconcileRemapWiringByInspection() throws {
        let src = try Self.normalizedSidebarSource()
        XCTAssertTrue(
            src.contains(
                "private func setSelectionAnchor(_ anchor: RowID?) { selectionAnchor = anchor "
                    + "selectionAnchorPath = anchor.flatMap { resolvedPath(of: $0) } }"),
            "every anchor set snapshots its path (incl. a ⌘-removed row)")
        // The anchor reconcile runs BEFORE the selection reconcile's early-return.
        XCTAssertTrue(
            src.contains("reconcileAnchorAfterTreeChange() let (survivors, snapshot)"),
            "anchor reconcile precedes the selection reconcile (so its early-return can't skip it)")
        XCTAssertTrue(
            src.contains(
                "anchor: pathValidatedAnchor()"),
            "the ⇧-range computation uses the fail-closed anchor")
        XCTAssertTrue(
            src.contains(
                "anchor: selectionAnchor, anchorSnapshot: selectionAnchorPath"),
            "known-move remap threads the anchor's independent snapshot")
    }

    /// #852 Codex findings 3 & 4 (source wiring): multi-select MODE reads the
    /// PRE-dedup pruned count; the operation targets are the deduped set; and the
    /// selection is reconciled on tree mutation + rescan.
    func testModeAndReconcileWiringByInspection() throws {
        let src = try Self.normalizedSidebarSource()
        XCTAssertTrue(
            src.contains("private var hasMultiSelection: Bool { prunedSelectedNodes.count >= 2 }"),
            "multi-select MODE is the pre-dedup pruned count ≥ 2 (finding 3)")
        XCTAssertTrue(
            src.contains(
                "private var selectedNodesForBatch: [AppState.TreeSelection] { "
                    + "AppState.topLevelSelection(prunedSelectedNodes) }"),
            "the operation targets are the deduped pruned selection")
        XCTAssertTrue(
            src.contains("reconcileSelectionAfterTreeChange()"),
            "the selection is reconciled after tree changes (finding 4)")
        XCTAssertTrue(
            src.contains(
                "private func setMultiSelection(_ newSelection: Set<RowID>) { "
                    + "multiSelection = newSelection"),
            "every selection assignment snapshots the members' paths (finding 4)")
    }

    /// #852 Codex round-4 finding 1 (source wiring): the reused-id reconcile
    /// re-anchors the NATIVE focus, runs after the refetch lands, and the
    /// single-item command target (`selectedTreeNode` + the AppState mirror) is
    /// FAIL-CLOSED via `pathValidatedNode`.
    func testNativeFocusReanchorAndFailClosedTargetByInspection() throws {
        let src = try Self.normalizedSidebarSource()
        XCTAssertTrue(
            src.contains(
                "if let focus = listSelection, !survivors.contains(focus) { "
                    + "let newFocus = visibleRowOrder.first { survivors.contains($0) } "
                    + "suppressOpenForSelectionChange = true listSelection = newFocus }"),
            "reconcile re-anchors the native focus (suppressing the open) when it was repointed")
        XCTAssertTrue(
            src.contains(
                ".onChange(of: tree.visibleRows) { _, _ in reconcileSelectionAfterTreeChange() "
                    + "mirrorTreeSelectionToAppState(listSelection) }"),
            "reconcile + mirror re-sync run once the refetch lands (fresh id resolution)")
        XCTAssertTrue(
            src.contains(
                "private var selectedTreeNode: TreeNode? { guard let listSelection else "
                    + "{ return nil } return pathValidatedNode(for: listSelection) }"),
            "selectedTreeNode resolves through the fail-closed pathValidatedNode")
        XCTAssertTrue(
            src.contains(
                "private func pathValidatedNode(for rowID: RowID) -> TreeNode? { "
                    + "guard case let .node(id) = rowID, let node = tree.node(for: id) else { return nil } "
                    + "if let snapshot = selectionPaths[rowID], snapshot != node.path { return nil } return node }"),
            "pathValidatedNode refuses a reused id whose current path differs from its snapshot")
        XCTAssertTrue(
            src.contains("guard let selection, let node = pathValidatedNode(for: selection) else {"),
            "the AppState mirror is fail-closed too (never mirrors a reused id)")
    }

    /// #852 Codex round-4 finding 2 (source wiring): a KNOWN in-app rename/move
    /// REMAPS the selection instead of letting the generic reconcile misread the
    /// intentional path change as a reused id.
    func testKnownRenameMoveRemapsByInspection() throws {
        let src = try Self.normalizedSidebarSource()
        XCTAssertTrue(
            src.contains(
                "case let .rename(oldPath, newPath), let .move(oldPath, newPath, _, _): "
                    + "remapSelectionForKnownMove(oldPath: oldPath, newPath: newPath)"),
            "handleTreeMutation remaps the selection for a known rename/move")
        XCTAssertFalse(
            src.contains("reconcileSelectionAfterTreeChange() applyPostMutationFocus"),
            "the generic reconcile no longer runs synchronously inside handleTreeMutation")
    }

    // MARK: - Codex findings 1 & 2: create gated on session + explicit result

    func testCreateFolderThenBatchMoveCreatesTheFolderAndMovesItems() async throws {
        let (state, vault) = try await makeVault(files: ["a.md", "b.md", "keep.md"])
        await state.createFolderThenBatchMove(
            newFolderName: "New Folder", in: "", items: [sel("a.md"), sel("b.md")])?.value
        XCTAssertTrue(exists(vault, "New Folder/a.md"), "a moved into the new folder")
        XCTAssertTrue(exists(vault, "New Folder/b.md"), "b moved into the new folder")
        XCTAssertFalse(exists(vault, "a.md"))
        XCTAssertFalse(exists(vault, "b.md"))
        XCTAssertTrue(exists(vault, "keep.md"))
        XCTAssertEqual(
            state.lastMutationAnnouncement, "Moved 2 items to New Folder.",
            "the batch move announces once after the create landed")
    }

    /// A STALE `lastError` (set by an earlier, unrelated failure) must NOT block
    /// a genuinely-successful create-then-move — the gate is the create's actual
    /// result, not the global error heuristic.
    func testCreateFolderThenBatchMoveIgnoresStalePriorLastError() async throws {
        let (state, vault) = try await makeVault(files: ["a.md", "dest/x.md"])
        state.lastError = "an old, unrelated failure"  // stale
        await state.createFolderThenBatchMove(
            newFolderName: "New Folder", in: "", items: [sel("a.md")])?.value
        XCTAssertTrue(
            exists(vault, "New Folder/a.md"),
            "the move ran despite a stale lastError (gate is the create result, not lastError)")
    }

    /// #852 Codex finding 2 (the data-loss bug): a pre-existing EMPTY "New
    /// Folder" (invisible to the old `files`-based uniqueName) must NOT swallow
    /// the moved items. With uniqueName now seeing empty folders, the create
    /// suffixes to a fresh "New Folder 2" and the items land THERE — never
    /// inside the pre-existing folder.
    func testCreateThenMoveDoesNotMoveIntoPreExistingEmptyFolder() async throws {
        let (state, vault) = try await makeVault(files: ["a.md", "keep.md"])
        // An EMPTY folder named exactly "New Folder" (invisible to `files`).
        try FileManager.default.createDirectory(
            at: vault.appendingPathComponent("New Folder"), withIntermediateDirectories: true)

        await state.createFolderThenBatchMove(
            newFolderName: "New Folder", in: "", items: [sel("a.md")])?.value
        XCTAssertFalse(
            exists(vault, "New Folder/a.md"),
            "the item is NOT moved into the pre-existing empty folder")
        XCTAssertTrue(exists(vault, "New Folder 2/a.md"), "it lands in the freshly-created folder")
        XCTAssertFalse(exists(vault, "a.md"))
    }

    /// #852 Codex finding 2: `createFolder` reports its ACTUAL outcome via
    /// `onResult` — `true` on success, `false` on a collision failure — the
    /// signal the create-then-move flows gate on (never folder existence).
    func testCreateFolderReportsSuccessAndFailureViaOnResult() async throws {
        let (state, vault) = try await makeVault(files: ["keep.md"])
        var ok: Bool?
        await state.createFolder(name: "Fresh", in: "", onResult: { ok = $0 })?.value
        XCTAssertEqual(ok, true, "a successful create reports true")

        // A collision (the folder now exists) reports false — an existence check
        // would wrongly report success on exactly this pre-existing directory.
        var collided: Bool?
        await state.createFolder(name: "Fresh", in: "", onResult: { collided = $0 })?.value
        XCTAssertEqual(collided, false, "a colliding create reports false")
        XCTAssertTrue(exists(vault, "Fresh"), "the pre-existing folder is still on disk (existence ≠ success)")
    }

    /// #852 Codex finding 2: `uniqueName` derives collisions from an authoritative
    /// directory listing, so it sees EMPTY folders (absent from `files`).
    func testUniqueNameSeesEmptyFoldersViaDirectoryListing() async throws {
        let (state, vault) = try await makeVault(files: ["keep.md"])
        try FileManager.default.createDirectory(
            at: vault.appendingPathComponent("New Folder"), withIntermediateDirectories: true)
        // createFolderThenBatchMove with no items: the create still runs and must
        // not collide — it suffixes to "New Folder 2" because the empty folder is
        // now visible to uniqueName.
        await state.createFolderThenBatchMove(
            newFolderName: "New Folder", in: "", items: [])?.value
        XCTAssertTrue(
            exists(vault, "New Folder 2"),
            "uniqueName suffixed past the pre-existing empty 'New Folder'")
    }

    /// #852 Codex findings 1 & 2 (source): both flows guard the CREATE on the
    /// original session (finding 1) and gate the MOVE on the create's explicit
    /// `created` result + same session (finding 2) — never folder existence.
    func testCreateThenMoveFlowsGuardSessionAndGateOnResultByInspection() throws {
        let src = try Self.normalizedSource("AppState.swift")
        // createFolder reports its actual success via onResult.
        XCTAssertTrue(
            src.contains("func createFolder( name: String, in parent: String, onResult: ((Bool) -> Void)? = nil )"),
            "createFolder reports its actual outcome via onResult")
        // Both flows: guard the create on session, then gate the move on `created`.
        XCTAssertTrue(
            src.contains(
                "guard self.currentSession === session else { return } var created = false "
                    + "await self.createFolder(name: suffixed, in: parent, onResult: { created = $0 })?.value "
                    + "guard created, self.currentSession === session else { return } "
                    + "await self.moveEntry(path: movePath, isDirectory: isDirectory, to: newFolderPath)?.value"),
            "createFolderThenMove guards the create on session and gates the move on `created`")
        XCTAssertTrue(
            src.contains(
                "guard self.currentSession === session else { return } var created = false "
                    + "await self.createFolder(name: suffixed, in: parent, onResult: { created = $0 })?.value "
                    + "guard created, self.currentSession === session else { return } "
                    + "await self.batchMove(items, to: newFolderPath).value"),
            "createFolderThenBatchMove guards the create on session and gates the batch move on `created`")
        // The retired existence heuristic is gone.
        XCTAssertFalse(
            src.contains("folderCreateLanded"),
            "the folder-existence heuristic (folderCreateLanded) is retired")
    }

    // MARK: - Codex finding 5: completed old-vault batches don't touch new UI

    func testBatchPostLoopWritesAreSessionGuardedByInspection() throws {
        let src = try Self.normalizedSource("AppState.swift")
        XCTAssertTrue(
            src.contains(
                "guard self.currentSession === session else { return } self.pendingBatchMove = nil"),
            "batchMove rechecks session ownership before clearing the (possibly new) sheet")
        XCTAssertTrue(
            src.contains(
                "guard self.currentSession === session else { return } if deleted > 0 {"),
            "batchDelete rechecks session ownership before its summary announcement")
    }

    // MARK: - Codex finding 1: every batch member is visibly + accessibly selected

    func testEveryBatchMemberIsSelectedVisuallyAndAccessiblyByInspection() throws {
        let src = try Self.normalizedSidebarSource()
        // The AX `.isSelected` trait reads multiSelection membership AND
        // path-validates the id (finding 4) so a reused dir id isn't reported.
        XCTAssertTrue(
            src.contains(
                "private func isRowSelected(_ rowID: RowID, currentPath: String) -> Bool "
                    + "{ multiSelection.contains(rowID) && selectionPaths[rowID] == currentPath }"),
            "the AX selected predicate reads multiSelection membership + path snapshot")
        XCTAssertTrue(
            src.contains(
                "isRowSelected(.node(node.nodeID), currentPath: node.path) ? .isSelected : []"),
            "both rows carry .isSelected for every selected, path-valid batch member")
        // The custom selection fill is a non-focus, path-valid selected member.
        XCTAssertTrue(
            src.contains("isRowSelected(rowID, currentPath: currentPath) && listSelection != rowID"),
            "the fill predicate is a non-focus, path-valid multiSelection member")
        XCTAssertTrue(
            src.contains("isMultiSelectFill(.node(node.nodeID), currentPath: node.path) {"),
            "both rows paint the multi-select fill")
    }

    // MARK: - Codex finding 2a/2b: same-value focus normalization

    /// A programmatic / single open collapses the batch set UNCONDITIONALLY —
    /// the collapse is NOT gated on `listSelection` changing (a same-value
    /// assignment wouldn't fire the `.onChange(of: listSelection)` collapse).
    func testProgrammaticOpenCollapsesBatchUnconditionally() {
        let mirrored = FileTreeSidebar.RowID.node(.file(path: "opened.md"))
        let other = FileTreeSidebar.RowID.node(.file(path: "other.md"))

        for current in [nil, mirrored, other] {
            let outcome = FileTreeSidebar.programmaticSelectionOutcome(
                currentListSelection: current,
                mirroredSelection: mirrored)
            XCTAssertEqual(outcome.selection, [mirrored])
            XCTAssertEqual(outcome.anchor, mirrored)
            XCTAssertEqual(outcome.listSelection, mirrored)
            XCTAssertEqual(outcome.shouldMirrorListSelection, current != mirrored)
            XCTAssertEqual(outcome.shouldSuppressAnnouncement, current != mirrored)
        }

        let cleared = FileTreeSidebar.programmaticSelectionOutcome(
            currentListSelection: mirrored,
            mirroredSelection: nil)
        XCTAssertTrue(cleared.selection.isEmpty)
        XCTAssertNil(cleared.anchor)
        XCTAssertNil(cleared.listSelection)
        XCTAssertTrue(cleared.shouldMirrorListSelection)
        XCTAssertTrue(cleared.shouldSuppressAnnouncement)
    }

    /// A plain click on the ALREADY-focused row opens it explicitly, because the
    /// same-value `listSelection` assignment wouldn't fire `.onChange`.
    func testPlainClickSameFocusOpensExplicitlyByInspection() throws {
        let src = try Self.normalizedSidebarSource()
        XCTAssertTrue(
            src.contains(
                "let sameFocus = (listSelection == row) setMultiSelection([row]) setSelectionAnchor(row) "
                    + "listSelection = row if sameFocus, case let .node(.file(path)) = row, "
                    + "appState.selectedFilePath != path { appState.openFile(path, target: .currentTab) }"),
            "a same-focus plain click opens the file explicitly (onChange won't fire)")
    }

    // MARK: - Source helpers

    /// Load a `Sources/SlateMac/<name>`, strip comments + strings, and collapse
    /// whitespace runs to single spaces so the contiguous-chain assertions are
    /// robust to formatting.
    private static func normalizedSource(_ name: String) throws -> String {
        var cursor = URL(fileURLWithPath: #filePath).deletingLastPathComponent()
        for _ in 0..<8 {
            let candidate = cursor.appendingPathComponent("Sources/SlateMac/\(name)")
            if FileManager.default.fileExists(atPath: candidate.path) {
                let raw = try String(contentsOf: candidate, encoding: .utf8)
                let stripped = SwiftSourceStripping.strippingCommentsAndStrings(raw)
                return stripped.split(whereSeparator: { $0.isWhitespace }).joined(separator: " ")
            }
            cursor = cursor.deletingLastPathComponent()
        }
        throw XCTSkip("\(name) not found relative to the test file")
    }

    private static func normalizedSidebarSource() throws -> String {
        try normalizedSource("FileTreeSidebar.swift")
    }
}
