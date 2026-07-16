// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI
import XCTest

@testable import SlateMac

/// U1-2 (#454): tab lifecycle over the parked-document architecture —
/// snapshot/restore byte-fidelity, dirty travel, same-path mirroring, the
/// tab-close and vault-close gates, and the AX value strings.
@MainActor
final class WorkspaceTabsTests: XCTestCase {

    private var tempDir: URL!

    override func setUpWithError() throws {
        tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("workspace-tabs-\(UUID().uuidString)")
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

    /// The U1-2 multi-file flow: duplicate the active tab (⌘T), then select
    /// another file — the duplicate's item is replaced in place, yielding
    /// two tabs over two files.
    private func openSecondFile(_ state: AppState, path: String) async {
        state.newTab()
        state.selectedFilePath = path
        await state.noteLoadTask?.value
    }

    private func successfulBatchMoveReport(
        planned: [StructuralBatchItem],
        standing: [BatchPathChange],
        opID: Int64 = 500
    ) -> BatchMoveReport {
        BatchMoveReport(
            envelope: StructuralBatchEnvelope(
                planned: planned, skipped: [], preflightFailures: []),
            state: .succeeded,
            opId: opID,
            standing: standing,
            rolledBack: [],
            failure: nil,
            rollbackFailures: [],
            rewritten: [],
            rewriteFailures: [],
            requiresRescan: false)
    }

    private func partialBatchTrashReport(
        planned: [StructuralBatchItem],
        trashed: [StructuralBatchItem],
        untrashed: [BatchTrashRemainder],
        unknown: [BatchTrashRemainder] = []
    ) -> BatchTrashReport {
        BatchTrashReport(
            envelope: StructuralBatchEnvelope(
                planned: planned, skipped: [], preflightFailures: []),
            state: .partial,
            opId: 600,
            trashed: trashed,
            untrashed: untrashed,
            unknown: unknown,
            bookkeepingFailures: [],
            requiresRescan: false)
    }

    // MARK: - Batch structural path index (FL-03 Task 5)

    func testVaultComponentPrefixIndexUsesLongestComponentBoundaryAndByteExactIdentity() {
        let composed = "caf\u{00E9}.base"
        let decomposed = "cafe\u{0301}.base"
        let index = VaultComponentPrefixIndex<String>([
            .init(path: "folder", includesDescendants: true, value: "outer"),
            .init(path: "folder/nested", includesDescendants: true, value: "inner"),
            .init(path: "folder/nested/exact.base", includesDescendants: false, value: "exact"),
            .init(path: composed, includesDescendants: false, value: "composed"),
            .init(path: decomposed, includesDescendants: false, value: "decomposed"),
        ])

        let exact = index.longestMatch(for: "folder/nested/exact.base")
        XCTAssertEqual(exact?.entry.value, "exact", "an exact file beats a folder ancestor")
        XCTAssertEqual(exact?.relativeSuffix, "")

        let nested = index.longestMatch(for: "folder/nested/child.md")
        XCTAssertEqual(nested?.entry.value, "inner", "the deepest directory prefix wins")
        XCTAssertEqual(nested?.relativeSuffix, "child.md")

        let outer = index.longestMatch(for: "folder/elsewhere/child.md")
        XCTAssertEqual(outer?.entry.value, "outer")
        XCTAssertEqual(outer?.relativeSuffix, "elsewhere/child.md")
        XCTAssertNil(index.longestMatch(for: "folderish/child.md"))

        XCTAssertEqual(index.longestMatch(for: composed)?.entry.value, "composed")
        XCTAssertEqual(index.longestMatch(for: decomposed)?.entry.value, "decomposed")
        XCTAssertEqual(
            index.longestMatch(for: "./folder//nested/exact.base/")?.entry.value,
            "exact",
            "lookup syntax matches core's CurDir/repeated-separator normalization")

        let descendantOnly = VaultComponentPrefixIndex<String>([
            .init(
                path: "folder/note.md",
                includesDescendants: false,
                value: "descendant")
        ])
        XCTAssertTrue(descendantOnly.containsEntry(atOrBelow: "folder"))
        XCTAssertTrue(descendantOnly.containsEntry(atOrBelow: "./folder//"))
        XCTAssertFalse(descendantOnly.containsEntry(atOrBelow: "folderish"))

        let deepComponents = (0..<100).map { "level-\($0)" }
        let deepPath = deepComponents.joined(separator: "/")
        let deepIndex = VaultComponentPrefixIndex<String>([
            .init(path: deepPath, includesDescendants: true, value: "root")
        ])
        let deepCandidate = deepPath + "/leaf.md"
        var componentVisits = 0
        XCTAssertEqual(
            deepIndex.longestMatch(
                for: deepCandidate, componentVisits: &componentVisits)?.entry.value,
            "root")
        XCTAssertEqual(
            componentVisits, deepComponents.count + 1,
            "lookup walks candidate components once; it never rebuilds every prefix")

        var model = WorkspaceModel()
        let markdownComposed = "notes/\(composed.replacingOccurrences(of: ".base", with: ".md"))"
        let markdownDecomposed =
            "notes/\(decomposed.replacingOccurrences(of: ".base", with: ".md"))"
        let tabID = model.openTab(.markdown(path: markdownComposed))
        let retargets = model.retargetFileBackedItems { item in
            guard case .markdown = item else { return nil }
            return .markdown(path: markdownDecomposed)
        }
        XCTAssertEqual(retargets.map(\.tabID), [tabID])
        guard case .markdown(let landedPath)? = model.tab(tabID)?.item else {
            return XCTFail("expected retargeted markdown tab")
        }
        XCTAssertTrue(
            BaseExactIdentity.matches(landedPath, markdownDecomposed),
            "file-backed retarget decisions are UTF-8 exact, not canonically equivalent")
    }

    func testBatchFolderMoveRetargetsActiveAndParkedMarkdownWithoutChangingTabIdentity()
        async throws
    {
        let vault = tempDir.appendingPathComponent("batch-folder-vault")
        try FileManager.default.createDirectory(
            at: vault.appendingPathComponent("folder"), withIntermediateDirectories: true)
        try FileManager.default.createDirectory(
            at: vault.appendingPathComponent("dest"), withIntermediateDirectories: true)
        try "# a\n".write(
            to: vault.appendingPathComponent("folder/a.md"),
            atomically: true, encoding: .utf8)
        try "---\ntitle: B\n---\n# b\n".write(
            to: vault.appendingPathComponent("folder/b.md"),
            atomically: true, encoding: .utf8)

        let state = makeAppState()
        state.openVault(at: vault)
        await state.scanTask?.value
        state.selectedFilePath = "folder/a.md"
        await state.noteLoadTask?.value
        await openSecondFile(state, path: "folder/b.md")
        state.updateEditorText("# dirty body\n")
        let parkedID = try XCTUnwrap(state.workspace.model.activeGroup.activeTabID)
        state.workspace.setViewMode(.reading, for: parkedID)
        state.workspace.parkReadingScroll(
            blockIndex: 7, path: "folder/b.md", for: parkedID)
        state.selectPreviousTab()
        await state.noteLoadTask?.value

        let activeID = try XCTUnwrap(state.workspace.model.activeGroup.activeTabID)
        let originalTabIDs = state.workspace.model.allTabs.map(\.id)
        let parkedBefore = try XCTUnwrap(state.workspace.document(for: parkedID))
        let preserved = (
            text: parkedBefore.text,
            baseline: parkedBefore.savedBaselineText,
            hash: parkedBefore.contentHash,
            dirty: parkedBefore.hasUnsavedChanges,
            fm: parkedBefore.fmSource,
            byte: parkedBefore.bodyByteOffset,
            line: parkedBefore.bodyLineOffset,
            loaded: parkedBefore.hasLoaded)

        let standing = BatchPathChange(
            oldPath: "folder", newPath: "dest/folder", isDirectory: true)
        let report = successfulBatchMoveReport(
            planned: [StructuralBatchItem(path: "folder", isDirectory: true)],
            standing: [standing])
        state.batchMoveRunner = { _, _ in report }
        state.structuralBatchRefreshRunner = { _ in }

        let task = try XCTUnwrap(
            state.batchMove(
                [AppState.TreeSelection(path: "folder", isDirectory: true)],
                to: "dest", preferredFocusPath: "folder/a.md"))
        await task.value

        XCTAssertEqual(state.workspace.model.allTabs.map(\.id), originalTabIDs)
        XCTAssertEqual(state.workspace.model.activeGroup.activeTabID, activeID)
        XCTAssertEqual(
            state.workspace.model.allTabs.map(\.item),
            [
                .markdown(path: "dest/folder/a.md"),
                .markdown(path: "dest/folder/b.md"),
            ])
        XCTAssertEqual(state.loadedFilePath, "dest/folder/a.md")
        XCTAssertEqual(state.selectedFilePath, "dest/folder/a.md")

        let parkedAfter = try XCTUnwrap(state.workspace.document(for: parkedID))
        XCTAssertEqual(parkedAfter.path, "dest/folder/b.md")
        XCTAssertEqual(parkedAfter.text, preserved.text)
        XCTAssertEqual(parkedAfter.savedBaselineText, preserved.baseline)
        XCTAssertEqual(parkedAfter.contentHash, preserved.hash)
        XCTAssertEqual(parkedAfter.hasUnsavedChanges, preserved.dirty)
        XCTAssertEqual(parkedAfter.fmSource, preserved.fm)
        XCTAssertEqual(parkedAfter.bodyByteOffset, preserved.byte)
        XCTAssertEqual(parkedAfter.bodyLineOffset, preserved.line)
        XCTAssertEqual(parkedAfter.hasLoaded, preserved.loaded)
        XCTAssertEqual(
            state.workspace.parkedReadingScroll(
                for: parkedID, path: "dest/folder/b.md"),
            7)
        XCTAssertEqual(state.workspace.viewMode(for: parkedID), .reading)
    }

    func testBatchFolderMoveReopensCanvasHandleAndPostMoveEditWritesOnlyNewPath()
        async throws
    {
        let vault = tempDir.appendingPathComponent("batch-canvas-vault")
        try FileManager.default.createDirectory(
            at: vault.appendingPathComponent("folder"), withIntermediateDirectories: true)
        try FileManager.default.createDirectory(
            at: vault.appendingPathComponent("dest"), withIntermediateDirectories: true)
        let canvas = """
            {"nodes":[{"id":"a","type":"text","text":"A","x":0,"y":0,"width":100,"height":50}],"edges":[]}
            """
        try canvas.write(
            to: vault.appendingPathComponent("folder/board.canvas"),
            atomically: true, encoding: .utf8)

        let state = makeAppState()
        state.openVault(at: vault)
        await state.scanTask?.value
        state.openCanvasFile("folder/board.canvas", target: .currentTab)
        let document = try XCTUnwrap(state.canvasDocuments["folder/board.canvas"])
        let handle = try XCTUnwrap(document.handle)
        let selection = document.selection
        let viewport = document.viewport
        let controller = state.canvasModeController(for: document)
        document.filterText = "A"

        let task = try XCTUnwrap(
            state.batchMove(
                [AppState.TreeSelection(path: "folder", isDirectory: true)],
                to: "dest", preferredFocusPath: "folder/board.canvas"))
        await task.value
        await state.nativeDocumentRetargetTask?.value

        XCTAssertEqual(
            state.workspace.model.activeGroup.activeTab?.item,
            .canvas(path: "dest/folder/board.canvas"))
        XCTAssertNil(state.canvasDocuments["folder/board.canvas"])
        XCTAssertTrue(state.canvasDocuments["dest/folder/board.canvas"] === document)
        XCTAssertEqual(document.path, "dest/folder/board.canvas")
        XCTAssertNotEqual(
            document.handle, handle,
            "the native Canvas handle owns its open path and must be reopened after a move")
        XCTAssertTrue(document.selection === selection)
        XCTAssertTrue(document.viewport === viewport)
        XCTAssertEqual(document.filterText, "A")
        XCTAssertNil(state.canvasModeControllers["folder/board.canvas"])
        XCTAssertTrue(state.canvasModeControllers["dest/folder/board.canvas"] === controller)
        XCTAssertEqual(state.selectedFilePath, "dest/folder/board.canvas")

        let newURL = vault.appendingPathComponent("dest/folder/board.canvas")
        let oldURL = vault.appendingPathComponent("folder/board.canvas")
        let beforeEdit = try Data(contentsOf: newURL)
        XCTAssertTrue(
            state.canvasApply(
                CanvasAction(
                    name: "color moved card",
                    ops: [.setNodeColor(id: "a", color: "2")]),
                to: document))
        let afterEdit = try Data(contentsOf: newURL)
        XCTAssertNotEqual(afterEdit, beforeEdit, "the edit must persist at the moved path")
        XCTAssertTrue(
            String(decoding: afterEdit, as: UTF8.self).contains(#""color":"2""#))
        XCTAssertFalse(
            FileManager.default.fileExists(atPath: oldURL.path),
            "the path-bound handle must not recreate or write the moved-away path")
    }

    func testBatchFolderMoveReopensBaseHandleAndPostMoveEditWritesOnlyNewPath()
        async throws
    {
        let vault = tempDir.appendingPathComponent("batch-base-vault")
        try FileManager.default.createDirectory(
            at: vault.appendingPathComponent("folder"), withIntermediateDirectories: true)
        try FileManager.default.createDirectory(
            at: vault.appendingPathComponent("dest"), withIntermediateDirectories: true)
        try FileManager.default.createDirectory(
            at: vault.appendingPathComponent("Notes"), withIntermediateDirectories: true)
        try Data(
            #"""
            views:
              - type: table
                name: Reading
                filters: "file.inFolder(\"Notes\")"
                order:
                  - file.name
                  - status
            """#.utf8
        ).write(to: vault.appendingPathComponent("folder/Reading.base"))
        try Data("---\nstatus: active\n---\n# Alpha\n".utf8)
            .write(to: vault.appendingPathComponent("Notes/Alpha.md"))

        let state = makeAppState()
        state.openVault(at: vault)
        await state.scanTask?.value
        state.openFile("folder/Reading.base", target: .currentTab)
        let document = try XCTUnwrap(state.activeBaseDocument)
        let handle = try XCTUnwrap(document.handle)

        let task = try XCTUnwrap(
            state.batchMove(
                [AppState.TreeSelection(path: "folder", isDirectory: true)],
                to: "dest", preferredFocusPath: "folder/Reading.base"))
        await task.value
        await state.nativeDocumentRetargetTask?.value

        XCTAssertTrue(state.activeBaseDocument === document)
        XCTAssertEqual(document.path, "dest/folder/Reading.base")
        XCTAssertNotEqual(
            document.handle, handle,
            "the native Base handle owns its open path and must be reopened after a move")

        let newURL = vault.appendingPathComponent("dest/folder/Reading.base")
        let oldURL = vault.appendingPathComponent("folder/Reading.base")
        let beforeEdit = try Data(contentsOf: newURL)
        document.focusColumn(1)
        state.basesSortByColumn()
        state.basesSaveSortToView()
        let afterEdit = try Data(contentsOf: newURL)
        XCTAssertNotEqual(afterEdit, beforeEdit, "the edit must persist at the moved path")
        XCTAssertTrue(String(decoding: afterEdit, as: UTF8.self).contains("slate:"))
        XCTAssertFalse(
            FileManager.default.fileExists(atPath: oldURL.path),
            "the reopened handle must not recreate or write the moved-away path")
    }

    func testPartialBatchTrashInvalidatesOnlyReturnedTrashedSubtree() async throws {
        let vault = tempDir.appendingPathComponent("batch-trash-vault")
        try FileManager.default.createDirectory(
            at: vault.appendingPathComponent("folder"), withIntermediateDirectories: true)
        try "# doomed\n".write(
            to: vault.appendingPathComponent("folder/doomed.md"),
            atomically: true, encoding: .utf8)
        try "# keep\n".write(
            to: vault.appendingPathComponent("keep.md"),
            atomically: true, encoding: .utf8)
        let canvas = """
            {"nodes":[{"id":"a","type":"text","text":"A","x":0,"y":0,"width":100,"height":50}],"edges":[]}
            """
        try canvas.write(
            to: vault.appendingPathComponent("folder/board.canvas"),
            atomically: true, encoding: .utf8)

        let state = makeAppState()
        state.openVault(at: vault)
        await state.scanTask?.value
        state.selectedFilePath = "folder/doomed.md"
        await state.noteLoadTask?.value
        let doomedTab = try XCTUnwrap(state.workspace.model.activeGroup.activeTabID)
        await openSecondFile(state, path: "keep.md")
        let keepTab = try XCTUnwrap(state.workspace.model.activeGroup.activeTabID)
        let keepText = state.currentNoteText
        state.openCanvasFile("folder/board.canvas", target: .newTab)
        let canvasDocument = try XCTUnwrap(state.canvasDocuments["folder/board.canvas"])
        XCTAssertNotNil(canvasDocument.handle)
        state.selectPreviousTab()
        await state.noteLoadTask?.value
        XCTAssertEqual(state.workspace.model.activeGroup.activeTabID, keepTab)
        XCTAssertNotNil(state.workspace.document(for: doomedTab))

        let folderItem = StructuralBatchItem(path: "folder", isDirectory: true)
        let keepItem = StructuralBatchItem(path: "keep.md", isDirectory: false)
        let keepFailure = BatchItemFailure(
            item: keepItem, stage: .trash, message: "permission denied")
        let report = partialBatchTrashReport(
            planned: [folderItem, keepItem],
            trashed: [folderItem],
            untrashed: [BatchTrashRemainder(item: keepItem, failure: keepFailure)])
        state.batchTrashRunner = { _, _ in report }
        state.structuralBatchRefreshRunner = { _ in }

        let task = try XCTUnwrap(
            state.batchDelete(
                [
                    AppState.TreeSelection(path: "folder", isDirectory: true),
                    AppState.TreeSelection(path: "keep.md", isDirectory: false),
                ], preferredFocusPath: "keep.md"))
        await task.value

        XCTAssertNil(state.workspace.document(for: doomedTab))
        XCTAssertTrue(
            state.canvasDocuments["folder/board.canvas"] === canvasDocument,
            "an open Canvas tab retains its document identity in the unavailable state")
        XCTAssertNil(canvasDocument.handle)
        guard case .failed(let canvasFailure) = canvasDocument.state else {
            return XCTFail("the trashed Canvas must land in a truthful failed state")
        }
        XCTAssertTrue(canvasFailure.contains("moved to Trash"))
        XCTAssertEqual(state.workspace.model.activeGroup.activeTabID, keepTab)
        XCTAssertEqual(state.selectedFilePath, "keep.md")
        XCTAssertEqual(state.loadedFilePath, "keep.md")
        XCTAssertEqual(state.currentNoteText, keepText)
        XCTAssertEqual(
            state.workspace.model.allTabs.map(\.item),
            [
                .markdown(path: "folder/doomed.md"),
                .markdown(path: "keep.md"),
                .canvas(path: "folder/board.canvas"),
            ],
            "Trash invalidation keeps error-state tabs open and never touches untrashed paths")
    }

    // MARK: New tab

    func testNewTabDuplicatesActiveItem() async throws {
        let (state, _) = try await makeOpenState()
        state.newTab()
        XCTAssertEqual(state.workspace.model.allTabs.count, 2)
        XCTAssertEqual(
            state.workspace.model.allTabs.map(\.item),
            [.markdown(path: "alpha.md"), .markdown(path: "alpha.md")])
        // Buffer unchanged — same file, fields intact.
        XCTAssertEqual(state.loadedFilePath, "alpha.md")
        XCTAssertNotNil(state.currentNoteText)
    }

    func testNewTabWithoutVaultIsNoOp() {
        let state = makeAppState()
        state.newTab()
        XCTAssertTrue(state.workspace.model.isEmpty)
    }

    // MARK: Snapshot / restore

    func testTabSwitchRestoresDirtyBufferByteIdentical() async throws {
        let (state, _) = try await makeOpenState()
        await openSecondFile(state, path: "beta.md")
        XCTAssertEqual(state.workspace.model.allTabs.count, 2)

        // Dirty beta (active), then switch back to alpha's tab.
        let dirtyBeta = "# beta.md\nEDITED, unsaved ✏️\n"
        state.updateEditorText(dirtyBeta)
        XCTAssertTrue(state.hasUnsavedChanges)
        state.selectPreviousTab()
        await state.noteLoadTask?.value

        XCTAssertEqual(state.loadedFilePath, "alpha.md")
        XCTAssertFalse(state.hasUnsavedChanges, "alpha's tab is clean")

        // Switch forward: beta's dirty buffer restored byte-identical, no
        // disk read (the parked restore path leaves noteLoadTask nil).
        state.selectNextTab()
        XCTAssertEqual(state.loadedFilePath, "beta.md")
        XCTAssertEqual(state.currentNoteText, dirtyBeta)
        XCTAssertTrue(state.hasUnsavedChanges, "dirty state travels with the tab")
    }

    func testTabSwitchBypassesDirtyGate() async throws {
        let (state, _) = try await makeOpenState()
        await openSecondFile(state, path: "beta.md")
        state.updateEditorText("# beta.md\ndirty")
        state.selectPreviousTab()
        XCTAssertNil(
            state.pendingNavigation,
            "switching tabs never prompts — the buffer parks with its tab")
        XCTAssertEqual(state.loadedFilePath, "alpha.md")
    }

    func testSidebarSelectionOfOpenPathSwitchesTabInsteadOfReplacing() async throws {
        let (state, _) = try await makeOpenState()
        await openSecondFile(state, path: "beta.md")
        // Sidebar re-selects alpha (open in the other tab): tab count must
        // stay 2 — this is a switch, not an in-place replace.
        state.selectedFilePath = "alpha.md"
        await state.noteLoadTask?.value
        XCTAssertEqual(state.workspace.model.allTabs.count, 2)
        XCTAssertEqual(state.loadedFilePath, "alpha.md")
        XCTAssertEqual(
            state.workspace.model.activeGroup.activeTab?.item,
            .markdown(path: "alpha.md"))
    }

    // MARK: Same-path mirroring

    func testSamePathTabsMirrorEditsAndSaves() async throws {
        let (state, vault) = try await makeOpenState()
        state.newTab()  // duplicate of alpha
        let parkedID = try XCTUnwrap(
            state.workspace.model.allTabs.first(where: {
                $0.id != state.workspace.model.activeGroup.activeTabID
            })?.id)

        let edited = "# alpha.md\nmirrored edit\n"
        state.updateEditorText(edited)
        let parked = try XCTUnwrap(state.workspace.document(for: parkedID))
        XCTAssertEqual(parked.text, edited, "duplicate tab renders live bytes")
        XCTAssertTrue(parked.hasUnsavedChanges)

        state.saveCurrentNote()
        await state.saveTask?.value
        XCTAssertFalse(parked.hasUnsavedChanges, "save clears the duplicate too")
        XCTAssertEqual(parked.savedBaselineText, edited)
        let onDisk = try String(
            contentsOf: vault.appendingPathComponent("alpha.md"), encoding: .utf8)
        XCTAssertEqual(onDisk, edited)
    }

    // MARK: Close gates

    func testCleanTabClosesImmediatelyAndFocusesSuccessor() async throws {
        let (state, _) = try await makeOpenState()
        await openSecondFile(state, path: "beta.md")
        state.requestCloseTab()
        XCTAssertNil(state.pendingTabClose)
        XCTAssertEqual(state.workspace.model.allTabs.count, 1)
        await state.noteLoadTask?.value
        XCTAssertEqual(state.loadedFilePath, "alpha.md", "left neighbor takes focus")
    }

    func testDirtyTabCloseGatesAndDiscardCloses() async throws {
        let (state, _) = try await makeOpenState()
        await openSecondFile(state, path: "beta.md")
        state.updateEditorText("# beta.md\ndirty")
        state.requestCloseTab()
        XCTAssertNotNil(state.pendingTabClose, "dirty close prompts")
        XCTAssertEqual(state.workspace.model.allTabs.count, 2, "nothing closed yet")

        state.resolveTabCloseDiscard()
        XCTAssertEqual(state.workspace.model.allTabs.count, 1)
        await state.noteLoadTask?.value
        XCTAssertEqual(state.loadedFilePath, "alpha.md")
        XCTAssertFalse(state.hasUnsavedChanges, "discarded buffer is gone")
    }

    func testDirtyTabCloseSaveSavesThenCloses() async throws {
        let (state, vault) = try await makeOpenState()
        await openSecondFile(state, path: "beta.md")
        let dirty = "# beta.md\nsaved on close\n"
        state.updateEditorText(dirty)
        state.requestCloseTab()
        state.resolveTabCloseSave()
        await state.saveTask?.value

        XCTAssertEqual(state.workspace.model.allTabs.count, 1, "closed after save")
        let onDisk = try String(
            contentsOf: vault.appendingPathComponent("beta.md"), encoding: .utf8)
        XCTAssertEqual(onDisk, dirty)
        await state.noteLoadTask?.value
        XCTAssertEqual(state.loadedFilePath, "alpha.md")
    }

    func testDirtyParkedTabCloseGatesToo() async throws {
        let (state, _) = try await makeOpenState()
        await openSecondFile(state, path: "beta.md")
        state.updateEditorText("# beta.md\ndirty parked")
        let betaTab = try XCTUnwrap(state.workspace.model.activeGroup.activeTabID)
        state.selectPreviousTab()  // park dirty beta
        await state.noteLoadTask?.value

        state.requestCloseTab(betaTab)
        XCTAssertEqual(state.pendingTabClose, betaTab, "parked dirty close prompts")
        state.resolveTabCloseCancel()
        XCTAssertEqual(state.workspace.model.allTabs.count, 2)
    }

    func testPendingCloseAfterSaveDoesNotLeakAcrossTabSwitch() async throws {
        // Codoki #492 (High): choose Save on the close prompt, switch tabs
        // before the save lands, come back, save again — the tab must NOT
        // close on that later unrelated save.
        let (state, _) = try await makeOpenState()
        await openSecondFile(state, path: "beta.md")
        state.updateEditorText("# beta.md\ndirty")
        let betaTab = try XCTUnwrap(state.workspace.model.activeGroup.activeTabID)

        state.requestCloseTab()
        state.resolveTabCloseSave()
        // Switch away BEFORE awaiting the in-flight save (the save's
        // main-actor continuation can't run until we yield).
        state.selectPreviousTab()
        await state.saveTask?.value
        await state.noteLoadTask?.value

        XCTAssertEqual(
            state.workspace.model.allTabs.count, 2,
            "close skipped — the user moved on mid-save")
        XCTAssertNil(state.pendingTabCloseAfterSave, "scope cleared on switch")

        // Return to beta and do an ordinary save: the tab must survive.
        state.selectTab(id: betaTab)
        state.updateEditorText("# beta.md\nlater edit")
        state.saveCurrentNote()
        await state.saveTask?.value
        XCTAssertEqual(
            state.workspace.model.allTabs.count, 2,
            "a later unrelated save must not close the tab")
    }

    // MARK: Vault-close aggregation

    func testVaultCloseAggregatesParkedDirtyTabs() async throws {
        let (state, _) = try await makeOpenState()
        await openSecondFile(state, path: "beta.md")
        state.updateEditorText("# beta.md\ndirty")
        state.selectPreviousTab()  // park dirty beta; alpha active clean
        await state.noteLoadTask?.value

        state.closeVaultFromUserAction()
        XCTAssertEqual(state.pendingVaultClose, 1, "one dirty tab found")
        XCTAssertNotNil(state.currentSession, "vault still open behind the prompt")
        state.resolveVaultCloseCancel()
        XCTAssertNil(state.pendingVaultClose)
    }

    func testVaultCloseSaveAllSavesEveryDirtyTabThenCloses() async throws {
        let (state, vault) = try await makeOpenState()
        await openSecondFile(state, path: "beta.md")
        state.updateEditorText("# beta.md\nsave-all beta\n")
        state.selectPreviousTab()
        await state.noteLoadTask?.value
        state.updateEditorText("# alpha.md\nsave-all alpha\n")

        state.closeVaultFromUserAction()
        XCTAssertEqual(state.pendingVaultClose, 2)
        state.resolveVaultCloseSaveAll()
        await state.vaultCloseSaveAllTask?.value

        XCTAssertNil(state.currentSession, "vault closed after save-all")
        XCTAssertTrue(state.workspace.model.isEmpty)
        let alpha = try String(
            contentsOf: vault.appendingPathComponent("alpha.md"), encoding: .utf8)
        let beta = try String(
            contentsOf: vault.appendingPathComponent("beta.md"), encoding: .utf8)
        XCTAssertEqual(alpha, "# alpha.md\nsave-all alpha\n")
        XCTAssertEqual(beta, "# beta.md\nsave-all beta\n")
    }

    // MARK: Ordinal + reorder commands

    func testOrdinalAndReorderCommands() async throws {
        let (state, _) = try await makeOpenState()
        await openSecondFile(state, path: "beta.md")
        await openSecondFile(state, path: "gamma.md")
        XCTAssertEqual(state.workspace.model.allTabs.count, 3)

        state.selectTab(ordinal: 1)
        await state.noteLoadTask?.value
        XCTAssertEqual(state.loadedFilePath, "alpha.md")

        state.selectTab(ordinal: 9)
        await state.noteLoadTask?.value
        XCTAssertEqual(state.loadedFilePath, "gamma.md", "9 = last")

        state.moveActiveTabLeft()
        XCTAssertEqual(
            state.workspace.model.activeGroup.tabs.map(\.item),
            [
                .markdown(path: "alpha.md"), .markdown(path: "gamma.md"),
                .markdown(path: "beta.md"),
            ])
        state.moveActiveTabRight()
        XCTAssertEqual(
            state.workspace.model.activeGroup.tabs.map(\.item),
            [
                .markdown(path: "alpha.md"), .markdown(path: "beta.md"),
                .markdown(path: "gamma.md"),
            ])
    }

    // MARK: AX strings + render gates

    func testTabAccessibilityValueStrings() {
        XCTAssertEqual(
            TabBarView.accessibilityValue(index: 1, count: 5, isDirty: false),
            "tab 2 of 5")
        XCTAssertEqual(
            TabBarView.accessibilityValue(index: 0, count: 1, isDirty: true),
            "tab 1 of 1, edited")
        // The Graph tab's kind rides in the value string (review round 1
        // finding 9) — the strip omitted it before.
        XCTAssertEqual(
            TabBarView.accessibilityValue(index: 0, count: 2, isDirty: false, isGraph: true),
            "tab 1 of 2, graph")
    }

    func testTabBarRendersInBothAppearances() async throws {
        let (state, _) = try await makeOpenState()
        await openSecondFile(state, path: "beta.md")
        state.updateEditorText("# beta.md\ndirty")
        PresentationReady.assertRendersInBothAppearances(
            TabBarView(group: state.workspace.model.activeGroup)
                .environmentObject(state))
    }

    func testTabBarContrastPairings() {
        // The strip's text-on-surface pairings ride the token registry —
        // re-assert the floor here so a strip-specific token change can't
        // slip below it (DoD §D).
        PresentationReady.assertContrastFloor([
            ("tab title on strip", .tokenTextPrimary, .tokenSurfaceSecondary),
            ("inactive tab title on strip", .tokenTextSecondary, .tokenSurfaceSecondary),
            ("active tab title on tab fill", .tokenTextPrimary, .tokenSurface),
        ])
    }
}
