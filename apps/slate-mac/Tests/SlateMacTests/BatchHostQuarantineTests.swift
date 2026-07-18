// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import Combine
import SwiftUI
import XCTest

@testable import SlateMac

/// Host-side safety at the native batch/refresh boundary. Core has already
/// changed the filesystem when its report arrives; open documents must stop
/// owning the reported old paths before the first refresh suspension.
@MainActor
final class BatchHostQuarantineTests: XCTestCase {
    private var tempDirs: [URL] = []

    private actor SuspensionGate {
        private var permits = 0
        private var waiters: [CheckedContinuation<Void, Never>] = []
        private var entrants = 0
        private var entrantWaiters: [(Int, CheckedContinuation<Void, Never>)] = []

        func enter() async {
            entrants += 1
            var remaining: [(Int, CheckedContinuation<Void, Never>)] = []
            for (expected, continuation) in entrantWaiters {
                if entrants >= expected {
                    continuation.resume()
                } else {
                    remaining.append((expected, continuation))
                }
            }
            entrantWaiters = remaining
            if permits > 0 {
                permits -= 1
                return
            }
            await withCheckedContinuation { waiters.append($0) }
        }

        func waitForEntrants(_ expected: Int) async {
            guard entrants < expected else { return }
            await withCheckedContinuation { entrantWaiters.append((expected, $0)) }
        }

        func releaseOne() {
            if waiters.isEmpty {
                permits += 1
            } else {
                waiters.removeFirst().resume()
            }
        }

        func entrantCount() -> Int { entrants }
    }

    private struct TypedState {
        let state: AppState
        let vault: URL
        let canvas: CanvasDocument
        let base: BaseDocument
        let markdownText: String
        let canvasFilter: String
        let baseFilter: String
    }

    private final class PresenceBox: @unchecked Sendable {
        private let lock = NSLock()
        private var storage: BatchTrashPhysicalPresence

        init(_ presence: BatchTrashPhysicalPresence) {
            storage = presence
        }

        var presence: BatchTrashPhysicalPresence {
            get {
                lock.lock()
                defer { lock.unlock() }
                return storage
            }
            set {
                lock.lock()
                storage = newValue
                lock.unlock()
            }
        }
    }

    private struct LiveNoteEditorHost: View {
        @ObservedObject var state: AppState

        var body: some View {
            NoteEditorView(
                text: state.noteTextBinding(),
                headings: [],
                accessibilityLabel: "Quarantine note editor",
                isEditable: state.activeNoteAuthoringDisabledReason == nil,
                readOnlyReason: state.activeNoteAuthoringDisabledReason,
                onSave: { state.saveCurrentNote() },
                scrollAnchorRequest: Empty<String, Never>().eraseToAnyPublisher(),
                lineScrollRequest: Empty<Int, Never>().eraseToAnyPublisher(),
                cursorByteOffsetRequest: Empty<Int, Never>().eraseToAnyPublisher(),
                previewEmbedAtCursor: nil
            )
        }
    }

    private static func findEditor(in view: NSView) -> SlateEditorTextView? {
        if let editor = view as? SlateEditorTextView { return editor }
        for child in view.subviews {
            if let editor = findEditor(in: child) { return editor }
        }
        return nil
    }

    private static func containsView(
        in view: NSView,
        typeNameContaining fragment: String
    ) -> Bool {
        if String(describing: type(of: view)).contains(fragment) { return true }
        for child in view.subviews {
            if containsView(in: child, typeNameContaining: fragment) {
                return true
            }
        }
        return false
    }

    private func pumpUntil(
        timeout: TimeInterval = 2,
        _ condition: () -> Bool
    ) -> Bool {
        let deadline = Date(timeIntervalSinceNow: timeout)
        repeat {
            if condition() { return true }
            RunLoop.main.run(until: Date(timeIntervalSinceNow: 0.02))
        } while Date() < deadline
        return condition()
    }

    private func commandEvent(_ key: String, window: NSWindow) -> NSEvent {
        NSEvent.keyEvent(
            with: .keyDown,
            location: .zero,
            modifierFlags: [.command],
            timestamp: ProcessInfo.processInfo.systemUptime,
            windowNumber: window.windowNumber,
            context: nil,
            characters: key,
            charactersIgnoringModifiers: key,
            isARepeat: false,
            keyCode: key == "z" ? 6 : 1
        )!
    }

    override func tearDown() {
        for directory in tempDirs {
            try? FileManager.default.removeItem(at: directory)
        }
        tempDirs = []
        super.tearDown()
    }

    private func makeAppState(in root: URL) -> AppState {
        AppState(
            recentsStore: RecentVaultsStore(
                fileURL: root.appendingPathComponent("recents.json")),
            externalOpener: { _ in true })
    }

    private func makeTypedState() async throws -> TypedState {
        let root = FileManager.default.temporaryDirectory
            .appendingPathComponent("batch-host-quarantine-\(UUID().uuidString)")
        tempDirs.append(root)
        let vault = root.appendingPathComponent("vault")
        try FileManager.default.createDirectory(
            at: vault.appendingPathComponent("folder"),
            withIntermediateDirectories: true)
        try FileManager.default.createDirectory(
            at: vault.appendingPathComponent("dest"),
            withIntermediateDirectories: true)
        try FileManager.default.createDirectory(
            at: vault.appendingPathComponent("Notes"),
            withIntermediateDirectories: true)

        try "# Original note\n".write(
            to: vault.appendingPathComponent("folder/note.md"),
            atomically: true,
            encoding: .utf8)
        try "# Keep\n".write(
            to: vault.appendingPathComponent("keep.md"),
            atomically: true,
            encoding: .utf8)
        try "# Duplicate source\n".write(
            to: vault.appendingPathComponent("folder/a.md"),
            atomically: true,
            encoding: .utf8)
        try Data(
            #"{"nodes":[{"id":"a","type":"text","text":"A","x":0,"y":0,"width":100,"height":50}],"edges":[]}"#.utf8
        ).write(to: vault.appendingPathComponent("folder/board.canvas"))
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

        let state = makeAppState(in: root)
        state.openVault(at: vault)
        await state.scanTask?.value

        state.openFile("folder/board.canvas", target: .currentTab)
        let canvas = try XCTUnwrap(state.canvasDocuments["folder/board.canvas"])
        XCTAssertNotNil(canvas.handle)

        state.openFile("folder/Reading.base", target: .newTab)
        let base = try XCTUnwrap(state.activeBaseDocument)
        XCTAssertNotNil(base.handle)
        base.focusColumn(1)
        state.basesSortByColumn()

        state.openFile("folder/note.md", target: .newTab)
        await state.noteLoadTask?.value
        let markdownText = "# Typed markdown\nunsaved work\n"
        state.updateEditorText(markdownText)
        XCTAssertTrue(state.hasUnsavedChanges)

        let canvasFilter = "typed canvas filter"
        let baseFilter = "typed base filter"
        canvas.filterText = canvasFilter
        base.quickFilterText = baseFilter

        return TypedState(
            state: state,
            vault: vault,
            canvas: canvas,
            base: base,
            markdownText: markdownText,
            canvasFilter: canvasFilter,
            baseFilter: baseFilter)
    }

    private func moveItems() -> [AppState.TreeSelection] {
        [
            .init(path: "folder/note.md", isDirectory: false),
            .init(path: "folder/board.canvas", isDirectory: false),
            .init(path: "folder/Reading.base", isDirectory: false),
        ]
    }

    private func trashItems() -> [StructuralBatchItem] {
        moveItems().map {
            StructuralBatchItem(path: $0.path, isDirectory: $0.isDirectory)
        }
    }

    private func activateCanvasTab(
        in state: AppState,
        path: String
    ) throws {
        let tab = try XCTUnwrap(
            state.workspace.model.allTabs.first {
                $0.item == .canvas(path: path)
            })
        state.activateTab(tab.id)
        XCTAssertEqual(state.workspace.activeTab?.item, .canvas(path: path))
    }

    private func unknownTrashReport(
        _ items: [StructuralBatchItem]
    ) -> BatchTrashReport {
        BatchTrashReport(
            envelope: StructuralBatchEnvelope(
                planned: items, skipped: [], preflightFailures: []),
            state: .failed,
            opId: nil,
            trashed: [],
            untrashed: [],
            unknown: items.map { item in
                BatchTrashRemainder(
                    item: item,
                    failure: BatchItemFailure(
                        item: item,
                        stage: .reconciliation,
                        message: "physical Trash verification failed"))
            },
            bookkeepingFailures: [],
            requiresRescan: true)
    }

    private func fileURLProvider(_ url: URL) -> NSItemProvider {
        let provider = NSItemProvider()
        provider.registerDataRepresentation(
            forTypeIdentifier: FileTreeSidebar.fileURLUTType,
            visibility: .all
        ) { completion in
            completion(url.dataRepresentation, nil)
            return nil
        }
        return provider
    }

    private func canonicalTemporaryDirectory(
        file: StaticString = #filePath,
        line: UInt = #line
    ) throws -> URL {
        let path = try FileManager.default.temporaryDirectory
            .resourceValues(forKeys: [.canonicalPathKey])
            .canonicalPath
        return URL(
            fileURLWithPath: try XCTUnwrap(path, file: file, line: line),
            isDirectory: true)
    }

    func testBatchMoveQuarantinesCoreStandingPathsBeforeRefreshAndPreservesTypedState()
        async throws
    {
        let fixture = try await makeTypedState()
        let state = fixture.state
        let refresh = SuspensionGate()
        state.structuralBatchRefreshRunner = { _ in await refresh.enter() }

        let task = try XCTUnwrap(
            state.batchMove(
                moveItems(),
                to: "dest",
                preferredFocusPath: "folder/note.md"))
        await refresh.waitForEntrants(1)

        XCTAssertEqual(
            state.workspace.model.allTabs.map(\.item),
            [
                .canvas(path: "dest/board.canvas"),
                .base(path: "dest/Reading.base"),
                .markdown(path: "dest/note.md"),
            ],
            "core-reported standing paths must land before refresh can suspend")
        XCTAssertEqual(state.loadedFilePath, "dest/note.md")
        XCTAssertEqual(state.selectedFilePath, "dest/note.md")
        XCTAssertEqual(state.currentNoteText, fixture.markdownText)
        XCTAssertTrue(state.hasUnsavedChanges)

        XCTAssertNil(state.canvasDocuments["folder/board.canvas"])
        XCTAssertTrue(state.canvasDocuments["dest/board.canvas"] === fixture.canvas)
        XCTAssertEqual(fixture.canvas.path, "dest/board.canvas")
        XCTAssertNil(
            fixture.canvas.handle,
            "a parked Canvas must synchronously surrender its old path-bound handle")
        XCTAssertEqual(fixture.canvas.filterText, fixture.canvasFilter)

        let oldBaseKey = BaseDocumentSource.file(path: "folder/Reading.base").key
        let newBaseKey = BaseDocumentSource.file(path: "dest/Reading.base").key
        XCTAssertNil(state.baseDocuments[oldBaseKey])
        XCTAssertTrue(state.baseDocuments[newBaseKey] === fixture.base)
        XCTAssertEqual(fixture.base.path, "dest/Reading.base")
        XCTAssertNil(
            fixture.base.handle,
            "a parked Base must synchronously surrender its old path-bound handle")
        XCTAssertEqual(fixture.base.quickFilterText, fixture.baseFilter)
        XCTAssertEqual(fixture.base.focusedColumnIndex, 1)

        XCTAssertNil(state.treeMutation, "tree/result landing still waits for the one refresh")
        XCTAssertNil(state.batchStructuralResult)
        XCTAssertNil(state.lastMutationAnnouncement)
        let preLandingRefreshCount = await refresh.entrantCount()
        XCTAssertEqual(preLandingRefreshCount, 1)

        XCTAssertNil(
            state.saveCurrentNote(),
            "the landed destination remains reserved until refresh publishes")
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            AppState.structuralMutationBusyReason)

        await refresh.releaseOne()
        await task.value
        await state.nativeDocumentRetargetTask?.value

        let save = try XCTUnwrap(
            state.saveCurrentNote(),
            "the preserved Markdown buffer becomes saveable after publication")
        await save.value
        XCTAssertFalse(
            FileManager.default.fileExists(
                atPath: fixture.vault.appendingPathComponent("folder/note.md").path))
        XCTAssertEqual(
            try String(
                contentsOf: fixture.vault.appendingPathComponent("dest/note.md"),
                encoding: .utf8),
            fixture.markdownText)

        guard case .batchMove(let standing, let touched)? = state.treeMutation?.kind else {
            return XCTFail("expected one final batch Move tree landing")
        }
        XCTAssertEqual(standing.count, 3)
        XCTAssertTrue(touched.isEmpty)
        XCTAssertNotNil(state.batchStructuralResult)
        XCTAssertEqual(state.lastMutationAnnouncement, "Moved 3 items to dest.")
        let finalRefreshCount = await refresh.entrantCount()
        XCTAssertEqual(finalRefreshCount, 1)
    }

    func testBatchTrashInvalidatesReportedPathsBeforeRefreshAndBlocksOldPathWrites()
        async throws
    {
        let fixture = try await makeTypedState()
        let state = fixture.state
        let session = try XCTUnwrap(state.currentSession)
        let staging = fixture.vault.deletingLastPathComponent()
            .appendingPathComponent("trash-staging")
        try FileManager.default.createDirectory(at: staging, withIntermediateDirectories: true)
        for path in ["note.md", "board.canvas", "Reading.base"] {
            try FileManager.default.moveItem(
                at: fixture.vault.appendingPathComponent("folder/\(path)"),
                to: staging.appendingPathComponent(path))
        }

        let trashed = trashItems()
        let report = BatchTrashReport(
            envelope: StructuralBatchEnvelope(
                planned: trashed, skipped: [], preflightFailures: []),
            state: .succeeded,
            opId: 700,
            trashed: trashed,
            untrashed: [],
            unknown: [],
            bookkeepingFailures: [],
            requiresRescan: false)
        state.batchTrashRunner = { _, _ in report }
        let refresh = SuspensionGate()
        state.structuralBatchRefreshRunner = { _ in await refresh.enter() }

        let task = try XCTUnwrap(
            state.batchDelete(
                moveItems(),
                preferredFocusPath: "folder/note.md"))
        await refresh.waitForEntrants(1)

        XCTAssertEqual(state.loadedFilePath, "folder/note.md")
        XCTAssertEqual(state.currentNoteText, fixture.markdownText)
        XCTAssertNotNil(state.savedBaselineText)
        XCTAssertTrue(state.hasUnsavedChanges)
        XCTAssertTrue(state.noteLoadError?.contains("moved to Trash") == true)
        XCTAssertNotNil(
            state.missingNoteRecoveryDraft(for: "folder/note.md"),
            "dirty Markdown must remain recoverable while its old path is read-only")
        let staleMarkdownSave = state.saveCurrentNote()
        XCTAssertNil(
            staleMarkdownSave,
            "the trashed Markdown path must not remain writable during refresh")
        await staleMarkdownSave?.value

        XCTAssertTrue(
            state.canvasDocuments["folder/board.canvas"] === fixture.canvas,
            "the handle-less Canvas remains mounted as selectable recovery state")
        XCTAssertNil(fixture.canvas.handle)
        XCTAssertFalse(
            state.canvasApply(
                CanvasAction(
                    name: "must not resurrect trashed canvas",
                    ops: [.setNodeColor(id: "a", color: "2")]),
                to: fixture.canvas))

        let oldBaseKey = BaseDocumentSource.file(path: "folder/Reading.base").key
        XCTAssertTrue(
            state.baseDocuments[oldBaseKey] === fixture.base,
            "the handle-less Base remains mounted as selectable recovery state")
        XCTAssertNil(fixture.base.handle)
        let staleBaseSave = try? fixture.base.saveSortToView(session: session)
        XCTAssertNil(staleBaseSave)

        for path in ["note.md", "board.canvas", "Reading.base"] {
            XCTAssertFalse(
                FileManager.default.fileExists(
                    atPath: fixture.vault.appendingPathComponent("folder/\(path)").path),
                "an old-path edit must not recreate \(path)")
        }
        XCTAssertNil(state.treeMutation, "tree/result landing still waits for the one refresh")
        XCTAssertNil(state.batchStructuralResult)
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            "This canvas is no longer available. Copy any draft before closing.")
        let preLandingRefreshCount = await refresh.entrantCount()
        XCTAssertEqual(preLandingRefreshCount, 1)

        await refresh.releaseOne()
        await task.value

        guard case .batchTrash(let landed)? = state.treeMutation?.kind else {
            return XCTFail("expected one final batch Trash tree landing")
        }
        XCTAssertEqual(landed, trashed)
        XCTAssertNotNil(state.batchStructuralResult)
        XCTAssertEqual(state.lastMutationAnnouncement, "Moved 3 items to Trash.")
        let finalRefreshCount = await refresh.entrantCount()
        XCTAssertEqual(finalRefreshCount, 1)
    }

    func testUnknownTrashOutcomeQuarantinesWritesWithoutDiscardingOpenDocumentState()
        async throws
    {
        let fixture = try await makeTypedState()
        let state = fixture.state
        let session = try XCTUnwrap(state.currentSession)
        let unknownItems = trashItems()
        let unknown = unknownItems.map { uncertain in
            BatchTrashRemainder(
                item: uncertain,
                failure: BatchItemFailure(
                    item: uncertain,
                    stage: .reconciliation,
                    message: "physical Trash verification failed"))
        }
        let report = BatchTrashReport(
            envelope: StructuralBatchEnvelope(
                planned: unknownItems, skipped: [], preflightFailures: []),
            state: .failed,
            opId: nil,
            trashed: [],
            untrashed: [],
            unknown: unknown,
            bookkeepingFailures: [],
            requiresRescan: true)
        state.batchTrashRunner = { _, _ in report }
        let refresh = SuspensionGate()
        state.structuralBatchRefreshRunner = { _ in await refresh.enter() }

        let task = try XCTUnwrap(
            state.batchDelete(
                moveItems(),
                preferredFocusPath: "folder/note.md"))
        await refresh.waitForEntrants(1)

        XCTAssertEqual(state.loadedFilePath, "folder/note.md")
        XCTAssertEqual(state.currentNoteText, fixture.markdownText)
        XCTAssertNotNil(state.savedBaselineText)
        XCTAssertTrue(state.hasUnsavedChanges)
        XCTAssertFalse(state.noteLoadError?.contains("moved to Trash") == true)
        XCTAssertNil(
            state.saveCurrentNote(),
            "an outcome-unknown Markdown path must be read-only until reconciliation")
        XCTAssertEqual(
            try String(
                contentsOf: fixture.vault.appendingPathComponent("folder/note.md"),
                encoding: .utf8),
            "# Original note\n",
            "the dirty buffer must be preserved in memory without writing ambiguous disk state")

        XCTAssertTrue(state.canvasDocuments["folder/board.canvas"] === fixture.canvas)
        XCTAssertNil(fixture.canvas.handle)
        XCTAssertEqual(fixture.canvas.filterText, fixture.canvasFilter)
        XCTAssertFalse(
            state.canvasApply(
                CanvasAction(
                    name: "must not write an outcome-unknown canvas",
                    ops: [.setNodeColor(id: "a", color: "2")]),
                to: fixture.canvas))

        let baseKey = BaseDocumentSource.file(path: "folder/Reading.base").key
        XCTAssertTrue(state.baseDocuments[baseKey] === fixture.base)
        XCTAssertNil(fixture.base.handle)
        XCTAssertEqual(fixture.base.quickFilterText, fixture.baseFilter)
        XCTAssertNil(try? fixture.base.saveSortToView(session: session))

        XCTAssertNil(state.treeMutation, "final reconciliation still waits for refresh")
        XCTAssertNil(state.batchStructuralResult)
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            AppState.batchTrashQuarantineReason)

        await refresh.releaseOne()
        await task.value
        await state.nativeDocumentRetargetTask?.value

        XCTAssertEqual(state.loadedFilePath, "folder/note.md")
        XCTAssertEqual(state.currentNoteText, fixture.markdownText)
        XCTAssertTrue(state.hasUnsavedChanges)
        XCTAssertFalse(state.noteLoadError?.contains("moved to Trash") == true)
        XCTAssertNotNil(fixture.canvas.handle)
        XCTAssertEqual(fixture.canvas.filterText, fixture.canvasFilter)
        XCTAssertNotNil(fixture.base.handle)
        XCTAssertEqual(fixture.base.quickFilterText, fixture.baseFilter)

        let resumedSave = try XCTUnwrap(
            state.saveCurrentNote(),
            "a post-rescan path proven present must become writable again")
        await resumedSave.value
        XCTAssertEqual(
            try String(
                contentsOf: fixture.vault.appendingPathComponent("folder/note.md"),
                encoding: .utf8),
            fixture.markdownText)

        guard case .batchReconcile? = state.treeMutation?.kind else {
            return XCTFail("an unknown core outcome must land as root reconciliation")
        }
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            "Couldn’t verify whether 3 items moved to Trash. Rescan required.")
    }

    func testQuarantinedCanvasUndoIsUnavailableAndPreservesEverySnapshot()
        async throws
    {
        let fixture = try await makeTypedState()
        let state = fixture.state
        let canvasPath = "folder/board.canvas"
        try activateCanvasTab(in: state, path: canvasPath)

        let undo = CanvasAction(
            name: "preserved undo inverse",
            ops: [.setNodeColor(id: "a", color: "2")])
        let redo = CanvasAction(
            name: "preserved redo inverse",
            ops: [.setNodeColor(id: "a", color: "3")])
        fixture.canvas.undoStack = [(name: "preserved undo", inverse: undo)]
        fixture.canvas.redoStack = [(name: "preserved redo", inverse: redo)]

        let fileURL = fixture.vault.appendingPathComponent(canvasPath)
        let diskBefore = try Data(contentsOf: fileURL)
        let outlineBefore = fixture.canvas.outline
        let tableBefore = fixture.canvas.tableRows
        let sceneBefore = fixture.canvas.scene
        let baseHandleBefore = fixture.base.handle
        let baseFilterBefore = fixture.base.quickFilterText
        let noteTextBefore = state.currentNoteText

        let uncertain = StructuralBatchItem(path: canvasPath, isDirectory: false)
        let report = unknownTrashReport([uncertain])
        state.batchTrashRunner = { _, _ in report }
        state.batchTrashPresenceProbeRunner = { _, _ in .indeterminate }
        state.structuralBatchRefreshRunner = { _ in }

        let quarantine = try XCTUnwrap(
            state.batchDelete(
                [.init(path: canvasPath, isDirectory: false)],
                preferredFocusPath: canvasPath))
        await quarantine.value

        XCTAssertTrue(state.undoTargetsCanvas, "the Canvas still owns ⌘Z")
        XCTAssertFalse(
            state.undoMenuItemEnabled,
            "a quarantined Canvas must expose Undo as unavailable")
        XCTAssertFalse(
            state.redoMenuItemEnabled,
            "a quarantined Canvas must expose Redo as unavailable")

        state.canvasUndo()

        XCTAssertEqual(
            state.lastMutationAnnouncement,
            AppState.batchTrashQuarantineReason,
            "defensive invocation must explain the exact unavailable reason")
        XCTAssertEqual(fixture.canvas.undoStack.map(\.name), ["preserved undo"])
        XCTAssertEqual(fixture.canvas.undoStack.map(\.inverse), [undo])
        XCTAssertEqual(fixture.canvas.redoStack.map(\.name), ["preserved redo"])
        XCTAssertEqual(fixture.canvas.redoStack.map(\.inverse), [redo])
        XCTAssertEqual(try Data(contentsOf: fileURL), diskBefore)
        XCTAssertEqual(fixture.canvas.outline, outlineBefore)
        XCTAssertEqual(fixture.canvas.tableRows, tableBefore)
        XCTAssertEqual(fixture.canvas.scene, sceneBefore)
        XCTAssertEqual(fixture.base.handle, baseHandleBefore)
        XCTAssertEqual(fixture.base.quickFilterText, baseFilterBefore)
        XCTAssertEqual(state.currentNoteText, noteTextBefore)
    }

    func testQuarantinedCanvasRedoIsUnavailableAndPreservesEverySnapshot()
        async throws
    {
        let fixture = try await makeTypedState()
        let state = fixture.state
        let canvasPath = "folder/board.canvas"
        try activateCanvasTab(in: state, path: canvasPath)

        let undo = CanvasAction(
            name: "preserved undo inverse",
            ops: [.setNodeColor(id: "a", color: "2")])
        let redo = CanvasAction(
            name: "preserved redo inverse",
            ops: [.setNodeColor(id: "a", color: "3")])
        fixture.canvas.undoStack = [(name: "preserved undo", inverse: undo)]
        fixture.canvas.redoStack = [(name: "preserved redo", inverse: redo)]

        let fileURL = fixture.vault.appendingPathComponent(canvasPath)
        let diskBefore = try Data(contentsOf: fileURL)
        let outlineBefore = fixture.canvas.outline
        let tableBefore = fixture.canvas.tableRows
        let sceneBefore = fixture.canvas.scene
        let baseHandleBefore = fixture.base.handle
        let baseFilterBefore = fixture.base.quickFilterText
        let noteTextBefore = state.currentNoteText

        let uncertain = StructuralBatchItem(path: canvasPath, isDirectory: false)
        let report = unknownTrashReport([uncertain])
        state.batchTrashRunner = { _, _ in report }
        state.batchTrashPresenceProbeRunner = { _, _ in .indeterminate }
        state.structuralBatchRefreshRunner = { _ in }

        let quarantine = try XCTUnwrap(
            state.batchDelete(
                [.init(path: canvasPath, isDirectory: false)],
                preferredFocusPath: canvasPath))
        await quarantine.value

        XCTAssertTrue(state.undoTargetsCanvas, "the Canvas still owns ⇧⌘Z")
        XCTAssertFalse(
            state.undoMenuItemEnabled,
            "a quarantined Canvas must expose Undo as unavailable")
        XCTAssertFalse(
            state.redoMenuItemEnabled,
            "a quarantined Canvas must expose Redo as unavailable")

        state.canvasRedo()

        XCTAssertEqual(
            state.lastMutationAnnouncement,
            AppState.batchTrashQuarantineReason,
            "defensive invocation must explain the exact unavailable reason")
        XCTAssertEqual(fixture.canvas.undoStack.map(\.name), ["preserved undo"])
        XCTAssertEqual(fixture.canvas.undoStack.map(\.inverse), [undo])
        XCTAssertEqual(fixture.canvas.redoStack.map(\.name), ["preserved redo"])
        XCTAssertEqual(fixture.canvas.redoStack.map(\.inverse), [redo])
        XCTAssertEqual(try Data(contentsOf: fileURL), diskBefore)
        XCTAssertEqual(fixture.canvas.outline, outlineBefore)
        XCTAssertEqual(fixture.canvas.tableRows, tableBefore)
        XCTAssertEqual(fixture.canvas.scene, sceneBefore)
        XCTAssertEqual(fixture.base.handle, baseHandleBefore)
        XCTAssertEqual(fixture.base.quickFilterText, baseFilterBefore)
        XCTAssertEqual(state.currentNoteText, noteTextBefore)
    }

    func testPromptOpenedBeforeCanvasQuarantineKeepsDraftAndNeverApplies()
        async throws
    {
        let fixture = try await makeTypedState()
        let state = fixture.state
        let canvasPath = "folder/board.canvas"
        try activateCanvasTab(in: state, path: canvasPath)

        state.canvasPromptNewGroup()
        XCTAssertEqual(state.canvasPrompt, .newGroup)
        state.canvasPromptDraft = "Roadmap"
        var nativeApplyCount = 0
        state.canvasApplyObserverForTesting = { _ in nativeApplyCount += 1 }
        let fileURL = fixture.vault.appendingPathComponent(canvasPath)
        let diskBefore = try Data(contentsOf: fileURL)
        let outlineBefore = fixture.canvas.outline

        let uncertain = StructuralBatchItem(path: canvasPath, isDirectory: false)
        let report = unknownTrashReport([uncertain])
        state.batchTrashRunner = { _, _ in report }
        state.batchTrashPresenceProbeRunner = { _, _ in .indeterminate }
        state.structuralBatchRefreshRunner = { _ in }
        let quarantine = try XCTUnwrap(
            state.batchDelete(
                [.init(path: canvasPath, isDirectory: false)],
                preferredFocusPath: canvasPath))
        await quarantine.value

        let committed = state.commitCanvasPromptMutation {
            state.canvasNewGroup(label: state.canvasPromptDraft)
        }
        if committed {
            state.dismissCanvasPrompt()
        }

        XCTAssertFalse(committed)
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            AppState.batchTrashQuarantineReason)
        XCTAssertEqual(
            state.activeCanvasMutationDisabledReason,
            AppState.batchTrashQuarantineReason)
        XCTAssertEqual(state.canvasPrompt, .newGroup)
        XCTAssertEqual(state.canvasPromptDraft, "Roadmap")
        XCTAssertEqual(nativeApplyCount, 0, "quarantine must stop before canvas_apply")
        XCTAssertTrue(fixture.canvas.undoStack.isEmpty)
        XCTAssertTrue(fixture.canvas.redoStack.isEmpty)
        XCTAssertEqual(fixture.canvas.outline, outlineBefore)
        XCTAssertEqual(try Data(contentsOf: fileURL), diskBefore)
    }

    func testCardPickerOpenedBeforeCanvasQuarantineKeepsRequestAndNeverInvokesPick()
        async throws
    {
        let fixture = try await makeTypedState()
        let state = fixture.state
        let canvasPath = "folder/board.canvas"
        try activateCanvasTab(in: state, path: canvasPath)

        let request = CanvasCardPickerRequest(purpose: .connectTo)
        state.canvasCardPicker = request
        var downstreamPickCount = 0
        var nativeApplyCount = 0
        state.canvasApplyObserverForTesting = { _ in nativeApplyCount += 1 }

        let uncertain = StructuralBatchItem(path: canvasPath, isDirectory: false)
        let report = unknownTrashReport([uncertain])
        state.batchTrashRunner = { _, _ in report }
        state.batchTrashPresenceProbeRunner = { _, _ in .indeterminate }
        state.structuralBatchRefreshRunner = { _ in }
        let quarantine = try XCTUnwrap(
            state.batchDelete(
                [.init(path: canvasPath, isDirectory: false)],
                preferredFocusPath: canvasPath))
        await quarantine.value

        let picked = state.commitCanvasCardPickerSelection(in: fixture.canvas) {
            downstreamPickCount += 1
        }

        XCTAssertFalse(picked)
        XCTAssertEqual(state.canvasCardPicker, request)
        XCTAssertEqual(downstreamPickCount, 0)
        XCTAssertEqual(nativeApplyCount, 0)
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            AppState.batchTrashQuarantineReason)
    }

    func testCardEditorOpenedBeforeCanvasQuarantineKeepsDraftAndNeverApplies()
        async throws
    {
        let fixture = try await makeTypedState()
        let state = fixture.state
        let canvasPath = "folder/board.canvas"
        try activateCanvasTab(in: state, path: canvasPath)
        state.canvasEditCard(nodeId: "a")
        let request = try XCTUnwrap(state.canvasCardEditor)
        state.canvasCardEditorDraft = "Draft kept byte-for-byte — e\u{301}"
        var nativeApplyCount = 0
        state.canvasApplyObserverForTesting = { _ in nativeApplyCount += 1 }
        let diskBefore = try Data(
            contentsOf: fixture.vault.appendingPathComponent(canvasPath))

        let uncertain = StructuralBatchItem(path: canvasPath, isDirectory: false)
        let report = unknownTrashReport([uncertain])
        state.batchTrashRunner = { _, _ in report }
        state.batchTrashPresenceProbeRunner = { _, _ in .indeterminate }
        state.structuralBatchRefreshRunner = { _ in }
        let quarantine = try XCTUnwrap(
            state.batchDelete(
                [.init(path: canvasPath, isDirectory: false)],
                preferredFocusPath: canvasPath))
        await quarantine.value

        XCTAssertEqual(
            state.activeCanvasCardEditorDisabledReason,
            AppState.batchTrashQuarantineReason)
        let committed = state.canvasCommitCardEdit(
            nodeId: request.nodeId,
            newText: state.canvasCardEditorDraft)

        XCTAssertFalse(committed)
        XCTAssertEqual(state.canvasCardEditor, request)
        XCTAssertEqual(
            state.canvasCardEditorDraft,
            "Draft kept byte-for-byte — e\u{301}")
        XCTAssertEqual(nativeApplyCount, 0)
        XCTAssertEqual(try Data(contentsOf: fixture.vault.appendingPathComponent(canvasPath)), diskBefore)
    }

    func testPresentReconciliationReenablesPreservedCanvasCardDraft() async throws {
        let fixture = try await makeTypedState()
        let state = fixture.state
        let canvasPath = "folder/board.canvas"
        try activateCanvasTab(in: state, path: canvasPath)
        state.canvasEditCard(nodeId: "a")
        let request = try XCTUnwrap(state.canvasCardEditor)
        state.canvasCardEditorDraft = "Recovered draft"
        let presence = PresenceBox(.indeterminate)
        let uncertain = StructuralBatchItem(path: canvasPath, isDirectory: false)
        let report = unknownTrashReport([uncertain])
        state.batchTrashRunner = { _, _ in report }
        state.batchTrashPresenceProbeRunner = { _, _ in presence.presence }
        state.structuralBatchRefreshRunner = { _ in }
        let quarantine = try XCTUnwrap(
            state.batchDelete(
                [.init(path: canvasPath, isDirectory: false)],
                preferredFocusPath: canvasPath))
        await quarantine.value

        presence.presence = .present
        let recovery = try XCTUnwrap(state.retryBatchTrashUnknownReconciliation())
        await recovery.value
        await state.nativeDocumentRetargetTask?.value

        XCTAssertNil(state.activeCanvasCardEditorDisabledReason)
        XCTAssertEqual(state.canvasCardEditor, request)
        XCTAssertEqual(state.canvasCardEditorDraft, "Recovered draft")
        let committed = state.canvasCommitCardEdit(
            nodeId: request.nodeId,
            newText: state.canvasCardEditorDraft)
        XCTAssertTrue(committed)
        XCTAssertNil(state.canvasCardEditor)
        XCTAssertEqual(state.canvasCardEditorDraft, "")
    }

    func testLiveMarkdownEditorBecomesSelectableReadOnlyAndRestoresUndoOnPresent()
        async throws
    {
        let fixture = try await makeTypedState()
        let state = fixture.state
        let notePath = "folder/note.md"
        let presence = PresenceBox(.indeterminate)
        let uncertain = StructuralBatchItem(path: notePath, isDirectory: false)
        let report = unknownTrashReport([uncertain])
        state.batchTrashRunner = { _, _ in report }
        state.batchTrashPresenceProbeRunner = { _, _ in presence.presence }
        state.structuralBatchRefreshRunner = { _ in }

        let window = NSWindow(
            contentRect: NSRect(x: 0, y: 0, width: 700, height: 480),
            styleMask: [.titled, .resizable],
            backing: .buffered,
            defer: false)
        window.isReleasedWhenClosed = false
        defer { window.orderOut(nil) }
        let host = NSHostingView(rootView: LiveNoteEditorHost(state: state))
        host.frame = window.contentView?.bounds ?? NSRect(x: 0, y: 0, width: 700, height: 480)
        window.contentView = host
        window.makeKeyAndOrderFront(nil)
        XCTAssertTrue(pumpUntil { Self.findEditor(in: host) != nil })
        let editor = try XCTUnwrap(Self.findEditor(in: host))
        window.makeFirstResponder(editor)
        let insertion = min(5, editor.string.utf16.count)
        editor.setSelectedRange(NSRange(location: insertion, length: 0))
        editor.insertText(" pre-quarantine", replacementRange: editor.selectedRange())
        XCTAssertTrue(pumpUntil { state.currentNoteText == editor.string })
        let textBefore = editor.string
        let selectionBefore = editor.selectedRange()
        XCTAssertTrue(editor.undoManager?.canUndo == true)

        let quarantine = try XCTUnwrap(
            state.batchDelete(
                [.init(path: notePath, isDirectory: false)],
                preferredFocusPath: notePath))
        await quarantine.value
        XCTAssertTrue(pumpUntil { !editor.isEditable })

        XCTAssertEqual(
            state.activeNoteAuthoringDisabledReason,
            AppState.batchTrashQuarantineReason)
        XCTAssertFalse(editor.isEditable)
        XCTAssertTrue(editor.isSelectable)
        XCTAssertFalse(editor.allowsUndo)
        XCTAssertEqual(editor.readOnlyReason, AppState.batchTrashQuarantineReason)
        XCTAssertEqual(editor.accessibilityHelp(), AppState.batchTrashQuarantineReason)
        XCTAssertFalse(state.undoMenuItemEnabled)
        XCTAssertFalse(state.redoMenuItemEnabled)

        editor.insertText(" MUST-NOT-LAND", replacementRange: editor.selectedRange())
        XCTAssertTrue(editor.performKeyEquivalent(with: commandEvent("z", window: window)))
        RunLoop.main.run(until: Date(timeIntervalSinceNow: 0.05))
        XCTAssertEqual(editor.string, textBefore)
        XCTAssertEqual(state.currentNoteText, textBefore)
        XCTAssertEqual(editor.selectedRange(), selectionBefore)

        presence.presence = .present
        let recovery = try XCTUnwrap(state.retryBatchTrashUnknownReconciliation())
        await recovery.value
        XCTAssertTrue(pumpUntil { editor.isEditable })

        XCTAssertNil(state.activeNoteAuthoringDisabledReason)
        XCTAssertTrue(editor.isEditable)
        XCTAssertTrue(editor.allowsUndo)
        XCTAssertTrue(editor.undoManager?.canUndo == true)
        XCTAssertEqual(editor.string, textBefore)
        XCTAssertEqual(editor.selectedRange(), selectionBefore)
        XCTAssertNil(state.noteAuthoringDisabledReason(for: "keep.md"))
    }

    func testQueuedMarkdownBindingWriteIsRejectedThroughoutUnknownQuarantine()
        async throws
    {
        let fixture = try await makeTypedState()
        let state = fixture.state
        let notePath = "folder/note.md"
        let uncertain = StructuralBatchItem(path: notePath, isDirectory: false)
        let report = unknownTrashReport([uncertain])
        state.batchTrashRunner = { _, _ in report }
        state.batchTrashPresenceProbeRunner = { _, _ in .indeterminate }
        state.structuralBatchRefreshRunner = { _ in }
        let quarantine = try XCTUnwrap(
            state.batchDelete(
                [.init(path: notePath, isDirectory: false)],
                preferredFocusPath: notePath))
        await quarantine.value
        let textBefore = state.currentNoteText

        state.noteTextBinding().wrappedValue = "queued delegate write must not land"

        XCTAssertEqual(state.currentNoteText, textBefore)
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            AppState.batchTrashQuarantineReason)
        XCTAssertEqual(
            state.noteAuthoringDisabledReason(for: notePath),
            AppState.batchTrashQuarantineReason)
    }

    func testQuarantinedBuilderDisablesOnlySaveToViewAndKeepsRecoveryDraftRoutes()
        async throws
    {
        let fixture = try await makeTypedState()
        let state = fixture.state
        let basePath = "folder/Reading.base"
        let tab = try XCTUnwrap(
            state.workspace.model.allTabs.first { $0.item == .base(path: basePath) })
        state.activateTab(tab.id)
        state.basesEditViewFilters()
        let model = try XCTUnwrap(state.activeBaseQueryBuilder)
        model.perform(.addCondition)
        let draftBefore = model.draft

        let uncertain = StructuralBatchItem(path: basePath, isDirectory: false)
        let report = unknownTrashReport([uncertain])
        state.batchTrashRunner = { _, _ in report }
        state.batchTrashPresenceProbeRunner = { _, _ in .indeterminate }
        state.structuralBatchRefreshRunner = { _ in }
        let quarantine = try XCTUnwrap(
            state.batchDelete(
                [.init(path: basePath, isDirectory: false)],
                preferredFocusPath: basePath))
        await quarantine.value

        XCTAssertEqual(
            state.baseQueryBuilderSaveToViewDisabledReason,
            AppState.batchTrashQuarantineReason)
        state.basesBuilderSaveToView()
        XCTAssertTrue(state.activeBaseQueryBuilder === model)
        XCTAssertEqual(model.draft, draftBefore)

        let recovered = try XCTUnwrap(
            state.basesBuilderSaveAsBase(path: "Queries/Recovered.base"),
            "Save as .base remains a recovery route to a separately admitted destination")
        await recovered.value
        XCTAssertTrue(
            FileManager.default.fileExists(
                atPath: fixture.vault.appendingPathComponent("Queries/Recovered.base").path))
        XCTAssertEqual(model.draft, draftBefore)
    }

    func testQueuedUnknownTrashBlocksBulkRenameAtSecondAdmissionAndPreservesPreview()
        async throws
    {
        let fixture = try await makeTypedState()
        let state = fixture.state
        let alphaURL = fixture.vault.appendingPathComponent("Notes/Alpha.md")
        let originalAlpha = try String(contentsOf: alphaURL, encoding: .utf8)

        let preview = try XCTUnwrap(
            state.previewPropertyRename(oldKey: "status", newKey: "phase"))
        await preview.value
        let previewPaths = try XCTUnwrap(state.pendingRenameReport).affected.map(\.path)
        XCTAssertEqual(previewPaths, ["Notes/Alpha.md"])

        let secondAdmission = SuspensionGate()
        state.renameApplySecondAdmissionGate = { await secondAdmission.enter() }
        let unknownItem = StructuralBatchItem(
            path: "folder/note.md", isDirectory: false)
        state.renameApplySecondAdmissionUnknownItemsForTesting = { [unknownItem] }
        let apply = try XCTUnwrap(
            state.applyPropertyRename(oldKey: "status", newKey: "phase"))
        await secondAdmission.waitForEntrants(1)

        await secondAdmission.releaseOne()
        await apply.value

        XCTAssertEqual(
            state.bulkRenameApplyDisabledReason,
            AppState.batchTrashQuarantineReason)
        XCTAssertFalse(state.isRenameInFlight)
        XCTAssertNil(state.renameError)
        XCTAssertEqual(state.pendingRenameReport?.affected.map(\.path), previewPaths)
        XCTAssertEqual(
            try String(contentsOf: alphaURL, encoding: .utf8),
            originalAlpha,
            "the second admission must reject before the vault-wide native writer touches even an unrelated path")
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            AppState.batchTrashQuarantineReason)

        let stillAvailablePreview = try XCTUnwrap(
            state.previewPropertyRename(oldKey: "status", newKey: "phase"))
        await stillAvailablePreview.value
        XCTAssertEqual(state.pendingRenameReport?.affected.map(\.path), previewPaths)

        state.batchTrashPresenceProbeRunner = { _, _ in .present }
        let retry = try XCTUnwrap(state.retryBatchTrashUnknownReconciliation())
        await retry.value
        XCTAssertNil(state.bulkRenameApplyDisabledReason)

        state.renameApplySecondAdmissionGate = nil
        state.renameApplySecondAdmissionUnknownItemsForTesting = nil
        let resumedApply = try XCTUnwrap(
            state.applyPropertyRename(oldKey: "status", newKey: "phase"))
        await resumedApply.value
        let renamedAlpha = try String(contentsOf: alphaURL, encoding: .utf8)
        XCTAssertTrue(renamedAlpha.contains("phase: active"))
        XCTAssertFalse(renamedAlpha.contains("status: active"))
    }

    func testBulkRenameApplyRejectsWhileStructuralMutationOwnsTheVault()
        async throws
    {
        let fixture = try await makeTypedState()
        let state = fixture.state
        let alphaURL = fixture.vault.appendingPathComponent("Notes/Alpha.md")
        let originalAlpha = try String(contentsOf: alphaURL, encoding: .utf8)

        let structuralToken = state.beginStructuralMutation()
        defer { state.endStructuralMutation(structuralToken) }
        XCTAssertEqual(
            state.bulkRenameApplyDisabledReason,
            AppState.structuralMutationBusyReason)

        let rejected = state.applyPropertyRename(oldKey: "status", newKey: "phase")
        XCTAssertNil(rejected)
        await rejected?.value
        XCTAssertEqual(
            try String(contentsOf: alphaURL, encoding: .utf8),
            originalAlpha,
            "Apply must not queue behind an in-flight structural Trash operation before its unknown ledger can land")
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            AppState.structuralMutationBusyReason)
    }

    func testBulkRenameApplyOwnsStructuralGateThroughItsNativeDuration()
        async throws
    {
        let fixture = try await makeTypedState()
        let state = fixture.state
        let secondAdmission = SuspensionGate()
        state.renameApplySecondAdmissionGate = { await secondAdmission.enter() }

        let apply = try XCTUnwrap(
            state.applyPropertyRename(oldKey: "status", newKey: "phase"))
        await secondAdmission.waitForEntrants(1)
        XCTAssertTrue(
            state.isMutatingStructure,
            "Apply must own the shared structural gate before any native writer can start")

        let item = StructuralBatchItem(path: "folder/note.md", isDirectory: false)
        let report = unknownTrashReport([item])
        state.batchTrashRunner = { _, _ in report }
        let overlappingTrash = state.batchDelete(
            [.init(path: item.path, isDirectory: false)],
            preferredFocusPath: item.path)
        XCTAssertNil(overlappingTrash)
        await overlappingTrash?.value

        await secondAdmission.releaseOne()
        await apply.value
        XCTAssertFalse(state.isMutatingStructure)
        let renamedAlpha = try String(
            contentsOf: fixture.vault.appendingPathComponent("Notes/Alpha.md"),
            encoding: .utf8)
        XCTAssertTrue(renamedAlpha.contains("phase: active"))
    }

    func testLateOldVaultRenameCannotReleaseNewVaultRenameOwnership()
        async throws
    {
        let fixture = try await makeTypedState()
        let state = fixture.state
        let oldGate = SuspensionGate()
        state.renameApplySecondAdmissionGate = { await oldGate.enter() }
        let oldApply = try XCTUnwrap(
            state.applyPropertyRename(oldKey: "status", newKey: "phase"))
        await oldGate.waitForEntrants(1)

        state.closeVault()
        let replacementVault = fixture.vault.deletingLastPathComponent()
            .appendingPathComponent("replacement-vault")
        try FileManager.default.createDirectory(
            at: replacementVault, withIntermediateDirectories: true)
        try "---\nstatus: replacement\n---\n# Replacement\n".write(
            to: replacementVault.appendingPathComponent("replacement.md"),
            atomically: true,
            encoding: .utf8)
        state.openVault(at: replacementVault)
        await state.scanTask?.value

        let replacementGate = SuspensionGate()
        state.renameApplySecondAdmissionGate = { await replacementGate.enter() }
        let replacementApply = try XCTUnwrap(
            state.applyPropertyRename(oldKey: "status", newKey: "phase"))
        await replacementGate.waitForEntrants(1)
        XCTAssertTrue(state.isRenameInFlight)
        XCTAssertTrue(state.isMutatingStructure)

        await oldGate.releaseOne()
        await oldApply.value
        XCTAssertTrue(
            state.isRenameInFlight,
            "a cancelled old-vault attempt must not clear the replacement attempt's in-flight identity")
        XCTAssertTrue(
            state.isMutatingStructure,
            "the old structural token must not release the replacement attempt's token")

        await replacementGate.releaseOne()
        await replacementApply.value
        XCTAssertFalse(state.isRenameInFlight)
        XCTAssertFalse(state.isMutatingStructure)
        let replacementText = try String(
            contentsOf: replacementVault.appendingPathComponent("replacement.md"),
            encoding: .utf8)
        XCTAssertTrue(replacementText.contains("phase: replacement"))
    }

    func testUnknownTrashOutcomeBecomesDefiniteTrashOnlyAfterRescanProvesAbsence()
        async throws
    {
        let fixture = try await makeTypedState()
        let state = fixture.state
        let uncertain = StructuralBatchItem(
            path: "folder/note.md", isDirectory: false)
        let staging = fixture.vault.deletingLastPathComponent()
            .appendingPathComponent("unknown-trash-staging")
        try FileManager.default.createDirectory(at: staging, withIntermediateDirectories: true)
        try FileManager.default.moveItem(
            at: fixture.vault.appendingPathComponent(uncertain.path),
            to: staging.appendingPathComponent("note.md"))

        let report = BatchTrashReport(
            envelope: StructuralBatchEnvelope(
                planned: [uncertain], skipped: [], preflightFailures: []),
            state: .failed,
            opId: nil,
            trashed: [],
            untrashed: [],
            unknown: [
                BatchTrashRemainder(
                    item: uncertain,
                    failure: BatchItemFailure(
                        item: uncertain,
                        stage: .reconciliation,
                        message: "physical Trash verification failed"))
            ],
            bookkeepingFailures: [],
            requiresRescan: true)
        state.batchTrashRunner = { _, _ in report }
        state.propertiesSourceDraftPath = uncertain.path
        state.propertiesSourceDraft = "title: recovered source draft\n"
        state.preservePropertyDraft(
            .scalarText(ScalarTextKind(kind: "text", value: "recovered row draft")),
            path: uncertain.path,
            key: "title")
        let refresh = SuspensionGate()
        state.structuralBatchRefreshRunner = { _ in await refresh.enter() }

        let task = try XCTUnwrap(
            state.batchDelete(
                [.init(path: uncertain.path, isDirectory: false)],
                preferredFocusPath: uncertain.path))
        await refresh.waitForEntrants(1)

        XCTAssertEqual(state.currentNoteText, fixture.markdownText)
        XCTAssertTrue(state.hasUnsavedChanges)
        XCTAssertFalse(state.noteLoadError?.contains("moved to Trash") == true)
        XCTAssertNil(state.saveCurrentNote())

        await refresh.releaseOne()
        await task.value

        XCTAssertEqual(state.loadedFilePath, uncertain.path)
        XCTAssertEqual(state.currentNoteText, fixture.markdownText)
        XCTAssertNotNil(state.savedBaselineText)
        XCTAssertTrue(state.hasUnsavedChanges)
        XCTAssertTrue(state.noteLoadError?.contains("moved to Trash") == true)
        let recovery = try XCTUnwrap(
            state.missingNoteRecoveryDraft(for: uncertain.path))
        XCTAssertEqual(recovery.body, fixture.markdownText)
        XCTAssertEqual(
            recovery.propertiesSourceDraft,
            "title: recovered source draft\n")
        XCTAssertEqual(recovery.propertyDrafts.map(\.key), ["title"])
        XCTAssertEqual(recovery.propertyDrafts.map(\.value), ["recovered row draft"])
        XCTAssertEqual(
            state.noteAuthoringDisabledReason(for: uncertain.path),
            AppState.missingNoteDraftReason)
        XCTAssertNil(state.saveCurrentNote())
        XCTAssertFalse(
            FileManager.default.fileExists(
                atPath: fixture.vault.appendingPathComponent(uncertain.path).path))
    }

    func testAbsentReconciliationPreservesAndRestoresParkedDirtyNoteDrafts()
        async throws
    {
        let fixture = try await makeTypedState()
        let state = fixture.state
        let path = "folder/note.md"
        let doomedTab = try XCTUnwrap(
            state.workspace.model.allTabs.first { $0.item == .markdown(path: path) })
        state.propertiesSourceDraftPath = path
        state.propertiesSourceDraft = "owner: parked source draft\n"
        state.preservePropertyDraft(
            .list(["parked", "row", "draft"]),
            path: path,
            key: "aliases")

        state.openFile("keep.md", target: .newTab)
        await state.noteLoadTask?.value
        XCTAssertEqual(state.loadedFilePath, "keep.md")
        let parkedBefore = try XCTUnwrap(state.workspace.document(for: doomedTab.id))
        XCTAssertTrue(parkedBefore.hasUnsavedChanges)
        XCTAssertEqual(parkedBefore.text, fixture.markdownText)

        let staging = fixture.vault.deletingLastPathComponent()
            .appendingPathComponent("parked-unknown-trash-staging")
        try FileManager.default.createDirectory(at: staging, withIntermediateDirectories: true)
        try FileManager.default.moveItem(
            at: fixture.vault.appendingPathComponent(path),
            to: staging.appendingPathComponent("note.md"))
        let uncertain = StructuralBatchItem(path: path, isDirectory: false)
        let report = unknownTrashReport([uncertain])
        state.batchTrashRunner = { _, _ in report }
        state.structuralBatchRefreshRunner = { _ in }

        let deletion = try XCTUnwrap(
            state.batchDelete(
                [.init(path: path, isDirectory: false)],
                preferredFocusPath: path))
        await deletion.value

        let recovery = try XCTUnwrap(state.missingNoteRecoveryDraft(for: path))
        XCTAssertEqual(recovery.body, fixture.markdownText)
        XCTAssertEqual(recovery.propertiesSourceDraft, "owner: parked source draft\n")
        XCTAssertEqual(recovery.propertyDrafts.map(\.key), ["aliases"])
        XCTAssertEqual(recovery.propertyDrafts.map(\.value), ["parked\nrow\ndraft"])
        let parkedAfter = try XCTUnwrap(state.workspace.document(for: doomedTab.id))
        XCTAssertTrue(parkedAfter.hasUnsavedChanges)
        XCTAssertTrue(parkedAfter.isMissingFromDisk)
        XCTAssertEqual(parkedAfter.text, fixture.markdownText)

        state.activateTab(doomedTab.id)
        XCTAssertEqual(state.loadedFilePath, path)
        XCTAssertEqual(state.currentNoteText, fixture.markdownText)
        XCTAssertTrue(state.hasUnsavedChanges)
        XCTAssertTrue(state.noteLoadError?.contains("moved to Trash") == true)
        XCTAssertNil(state.saveCurrentNote())
    }

    func testAbsentReconciliationPreservesActivePropertyOnlyDrafts()
        async throws
    {
        let fixture = try await makeTypedState()
        let state = fixture.state
        let path = "folder/note.md"
        let save = try XCTUnwrap(state.saveCurrentNote())
        await save.value
        XCTAssertFalse(state.hasUnsavedChanges)

        state.propertiesSourceDraftPath = path
        state.propertiesSourceDraft = "title: active property-only source\n"
        state.preservePropertyDraft(
            .scalarText(
                ScalarTextKind(kind: "text", value: "active property-only row")),
            path: path,
            key: "title")

        let staging = fixture.vault.deletingLastPathComponent()
            .appendingPathComponent("active-property-only-trash-staging")
        try FileManager.default.createDirectory(at: staging, withIntermediateDirectories: true)
        try FileManager.default.moveItem(
            at: fixture.vault.appendingPathComponent(path),
            to: staging.appendingPathComponent("note.md"))
        let uncertain = StructuralBatchItem(path: path, isDirectory: false)
        let report = unknownTrashReport([uncertain])
        state.batchTrashRunner = { _, _ in report }
        state.structuralBatchRefreshRunner = { _ in }

        let deletion = try XCTUnwrap(
            state.batchDelete(
                [.init(path: path, isDirectory: false)],
                preferredFocusPath: path))
        await deletion.value

        let recovery = try XCTUnwrap(state.missingNoteRecoveryDraft(for: path))
        XCTAssertEqual(recovery.body, fixture.markdownText)
        XCTAssertEqual(
            recovery.propertiesSourceDraft,
            "title: active property-only source\n")
        XCTAssertEqual(recovery.propertyDrafts.map(\.value), ["active property-only row"])
        XCTAssertEqual(state.loadedFilePath, path)
        XCTAssertEqual(state.currentNoteText, fixture.markdownText)
        XCTAssertTrue(
            state.hasUnsavedChanges,
            "property-only recovery must participate in tab/vault dirty-close gates")
        XCTAssertNil(state.saveCurrentNote())
    }

    func testAbsentReconciliationPreservesParkedPropertyOnlyDrafts()
        async throws
    {
        let fixture = try await makeTypedState()
        let state = fixture.state
        let path = "folder/note.md"
        let doomedTab = try XCTUnwrap(
            state.workspace.model.allTabs.first { $0.item == .markdown(path: path) })
        let save = try XCTUnwrap(state.saveCurrentNote())
        await save.value
        XCTAssertFalse(state.hasUnsavedChanges)

        let window = NSWindow(
            contentRect: NSRect(x: 0, y: 0, width: 620, height: 360),
            styleMask: [.titled, .resizable],
            backing: .buffered,
            defer: false)
        window.isReleasedWhenClosed = false
        defer { window.orderOut(nil) }
        let host = NSHostingView(
            rootView: NotePropertiesHeader(workspace: state.workspace)
                .environmentObject(state))
        host.frame = window.contentView?.bounds
            ?? NSRect(x: 0, y: 0, width: 620, height: 360)
        window.contentView = host
        window.makeKeyAndOrderFront(nil)
        host.layoutSubtreeIfNeeded()

        state.togglePropertiesSourceCommand()
        XCTAssertTrue(
            pumpUntil { state.propertiesSourceDraftPath == path },
            "the mounted header must enter source mode for the active note")
        state.propertiesSourceDraft = "aliases: parked property-only source\n"
        state.preservePropertyDraft(
            .list(["parked", "property-only", "row"]),
            path: path,
            key: "aliases")
        state.openFile("keep.md", target: .newTab)
        await state.noteLoadTask?.value
        XCTAssertNil(
            state.propertiesSourceDraftPath,
            "AppState must synchronously move the outgoing Binding into its path registry")
        XCTAssertEqual(state.propertiesSourceDraft, "")
        let parkedBefore = try XCTUnwrap(state.workspace.document(for: doomedTab.id))
        XCTAssertFalse(parkedBefore.hasUnsavedChanges)

        let staging = fixture.vault.deletingLastPathComponent()
            .appendingPathComponent("parked-property-only-trash-staging")
        try FileManager.default.createDirectory(at: staging, withIntermediateDirectories: true)
        try FileManager.default.moveItem(
            at: fixture.vault.appendingPathComponent(path),
            to: staging.appendingPathComponent("note.md"))
        let uncertain = StructuralBatchItem(path: path, isDirectory: false)
        let report = unknownTrashReport([uncertain])
        state.batchTrashRunner = { _, _ in report }
        state.structuralBatchRefreshRunner = { _ in }

        let deletion = try XCTUnwrap(
            state.batchDelete(
                [.init(path: path, isDirectory: false)],
                preferredFocusPath: path))
        await deletion.value

        let recovery = try XCTUnwrap(state.missingNoteRecoveryDraft(for: path))
        XCTAssertEqual(recovery.body, fixture.markdownText)
        XCTAssertEqual(
            recovery.propertiesSourceDraft,
            "aliases: parked property-only source\n")
        XCTAssertEqual(
            recovery.propertyDrafts.map(\.value),
            ["parked\nproperty-only\nrow"])
        let parkedAfter = try XCTUnwrap(state.workspace.document(for: doomedTab.id))
        XCTAssertTrue(parkedAfter.isMissingFromDisk)
        XCTAssertTrue(parkedAfter.hasUnsavedChanges)

        state.activateTab(doomedTab.id)
        XCTAssertEqual(state.currentNoteText, fixture.markdownText)
        XCTAssertTrue(state.hasUnsavedChanges)
        XCTAssertEqual(state.propertiesSourceDraftPath, path)
        XCTAssertEqual(
            state.propertiesSourceDraft,
            "aliases: parked property-only source\n")
        XCTAssertNil(state.saveCurrentNote())
    }

    func testAbsentReconciliationDoesNotInventRecoveryForUnchangedSourceMode()
        async throws
    {
        let fixture = try await makeTypedState()
        let state = fixture.state
        let path = "folder/note.md"
        let save = try XCTUnwrap(state.saveCurrentNote())
        await save.value
        XCTAssertFalse(state.hasUnsavedChanges)

        // Entering source mode seeds the editor with the exact saved
        // frontmatter. With no edit, this is presentation state, not a draft.
        state.propertiesSourceDraftPath = path
        state.propertiesSourceDraft = state.currentNoteFMSource

        let staging = fixture.vault.deletingLastPathComponent()
            .appendingPathComponent("unchanged-source-trash-staging")
        try FileManager.default.createDirectory(at: staging, withIntermediateDirectories: true)
        try FileManager.default.moveItem(
            at: fixture.vault.appendingPathComponent(path),
            to: staging.appendingPathComponent("note.md"))
        let uncertain = StructuralBatchItem(path: path, isDirectory: false)
        let report = unknownTrashReport([uncertain])
        state.batchTrashRunner = { _, _ in report }
        state.structuralBatchRefreshRunner = { _ in }

        let deletion = try XCTUnwrap(
            state.batchDelete(
                [.init(path: path, isDirectory: false)],
                preferredFocusPath: path))
        await deletion.value

        XCTAssertNil(state.missingNoteRecoveryDraft(for: path))
        XCTAssertNil(state.loadedFilePath)
        XCTAssertNil(state.currentNoteText)
        XCTAssertFalse(state.hasUnsavedChanges)
    }

    func testCommittedMountedPropertyRowDoesNotBecomeARecoveryDraft()
        async throws
    {
        let fixture = try await makeTypedState()
        let state = fixture.state
        let path = "folder/note.md"
        let bodySave = try XCTUnwrap(state.saveCurrentNote())
        await bodySave.value
        let initialProperty = try XCTUnwrap(
            state.setProperty(
                path: path,
                key: "title",
                value: .text(value: "before")))
        await initialProperty.value
        XCTAssertTrue(
            state.currentNoteProperties.contains { $0.key == "title" })
        let submittedDraft = PropertyEditDraft.scalarText(
            ScalarTextKind(kind: "text", value: "temporary row draft"))
        state.preservePropertyDraft(
            submittedDraft,
            path: path,
            key: "title")

        let window = NSWindow(
            contentRect: NSRect(x: 0, y: 0, width: 620, height: 360),
            styleMask: [.titled, .resizable],
            backing: .buffered,
            defer: false)
        window.isReleasedWhenClosed = false
        defer { window.orderOut(nil) }
        let host = NSHostingView(
            rootView: NotePropertiesHeader(workspace: state.workspace)
                .environmentObject(state))
        host.frame = window.contentView?.bounds
            ?? NSRect(x: 0, y: 0, width: 620, height: 360)
        window.contentView = host
        window.makeKeyAndOrderFront(nil)
        host.layoutSubtreeIfNeeded()

        XCTAssertTrue(
            pumpUntil {
                Self.containsView(
                    in: host,
                    typeNameContaining: "AppKitTextField")
            },
            "the live property row must be mounted before its refresh is exercised")
        XCTAssertTrue(
            pumpUntil {
                state.preservedPropertyDraft(path: path, key: "title")?.recoveryText
                    == "temporary row draft"
            })

        let committed = try XCTUnwrap(
            state.setProperty(
                path: path,
                key: "title",
                value: .text(value: "committed"),
                submittedDraft: submittedDraft))
        await committed.value
        XCTAssertTrue(
            pumpUntil {
                state.preservedPropertyDraft(path: path, key: "title") == nil
            },
            "the mounted row refresh must clear rather than re-cache its baseline")

        let staging = fixture.vault.deletingLastPathComponent()
            .appendingPathComponent("committed-row-trash-staging")
        try FileManager.default.createDirectory(at: staging, withIntermediateDirectories: true)
        try FileManager.default.moveItem(
            at: fixture.vault.appendingPathComponent(path),
            to: staging.appendingPathComponent("note.md"))
        let uncertain = StructuralBatchItem(path: path, isDirectory: false)
        let report = unknownTrashReport([uncertain])
        state.batchTrashRunner = { _, _ in report }
        state.structuralBatchRefreshRunner = { _ in }
        let deletion = try XCTUnwrap(
            state.batchDelete(
                [.init(path: path, isDirectory: false)],
                preferredFocusPath: path))
        await deletion.value

        XCTAssertNil(state.missingNoteRecoveryDraft(for: path))
        XCTAssertFalse(state.hasUnsavedChanges)
    }

    func testSuccessfulFileRenameRekeysActivePropertyDraftOwnership()
        async throws
    {
        let fixture = try await makeTypedState()
        let state = fixture.state
        let oldPath = "folder/note.md"
        let newPath = "folder/renamed.md"
        let sourceDraft = "title: active source draft\n"
        let rowDraft = PropertyEditDraft.scalarText(
            ScalarTextKind(kind: "text", value: "active row draft"))

        state.propertiesSourceDraftPath = oldPath
        state.propertiesSourceDraft = sourceDraft
        state.preservePropertyDraft(rowDraft, path: oldPath, key: "title")

        let rename = try XCTUnwrap(
            state.renameEntry(
                path: oldPath,
                isDirectory: false,
                to: "renamed.md"))
        await rename.value

        XCTAssertEqual(state.loadedFilePath, newPath)
        XCTAssertEqual(state.propertiesSourceDraftPath, newPath)
        XCTAssertEqual(state.propertiesSourceDraft, sourceDraft)
        XCTAssertNil(state.preservedPropertyDraft(path: oldPath, key: "title"))
        XCTAssertEqual(
            state.preservedPropertyDraft(path: newPath, key: "title"),
            rowDraft)

        // Clearing the mounted slot may expose the path-scoped recovery copy.
        state.clearMountedPropertiesSourceDraft()
        if state.restoreParkedPropertiesSourceDraft(for: newPath) {
            XCTAssertEqual(state.propertiesSourceDraft, sourceDraft)
        }
    }

    func testSuccessfulFileRenameRekeysParkedPropertyDraftOwnership()
        async throws
    {
        let fixture = try await makeTypedState()
        let state = fixture.state
        let oldPath = "folder/a.md"
        let newPath = "folder/parked-renamed.md"
        let sourceDraft = "owner: parked source draft\n"
        let rowDraft = PropertyEditDraft.list(["parked", "row", "draft"])

        state.openFile(oldPath, target: .newTab)
        await state.noteLoadTask?.value
        let parkedTab = try XCTUnwrap(
            state.workspace.model.allTabs.first {
                $0.item == .markdown(path: oldPath)
            })
        state.propertiesSourceDraftPath = oldPath
        state.propertiesSourceDraft = sourceDraft
        state.preservePropertyDraft(rowDraft, path: oldPath, key: "aliases")

        state.openFile("keep.md", target: .newTab)
        await state.noteLoadTask?.value
        XCTAssertNil(state.propertiesSourceDraftPath)

        let rename = try XCTUnwrap(
            state.renameEntry(
                path: oldPath,
                isDirectory: false,
                to: "parked-renamed.md"))
        await rename.value

        XCTAssertNil(state.preservedPropertyDraft(path: oldPath, key: "aliases"))
        XCTAssertEqual(
            state.preservedPropertyDraft(path: newPath, key: "aliases"),
            rowDraft)
        XCTAssertFalse(state.restoreParkedPropertiesSourceDraft(for: oldPath))

        state.activateTab(parkedTab.id)
        await state.noteLoadTask?.value
        XCTAssertEqual(state.loadedFilePath, newPath)
        XCTAssertEqual(state.propertiesSourceDraftPath, newPath)
        XCTAssertEqual(state.propertiesSourceDraft, sourceDraft)
    }

    func testSuccessfulFolderRenameRekeysExactRecoveryCachesFromReportMappings()
        async throws
    {
        let fixture = try await makeTypedState()
        let state = fixture.state
        let oldPath = "folder/note.md"
        let newPath = "renamed/note.md"
        let siblingPath = "folderish/note.md"
        let sourceDraft = "title: source identity draft\n"
        let rowDraft = PropertyEditDraft.scalarText(
            ScalarTextKind(kind: "text", value: "source identity row"))
        let siblingRow = PropertyEditDraft.scalarText(
            ScalarTextKind(kind: "text", value: "prefix sibling row"))

        try FileManager.default.createDirectory(
            at: fixture.vault.appendingPathComponent("folderish"),
            withIntermediateDirectories: true)
        try "# Prefix sibling\n".write(
            to: fixture.vault.appendingPathComponent(siblingPath),
            atomically: true,
            encoding: .utf8)

        // Park a prefix-lookalike sibling. The report's exact file mappings
        // must not treat the textual prefix as an owned descendant.
        state.propertiesSourceDraftPath = siblingPath
        state.propertiesSourceDraft = "title: prefix sibling source\n"
        state.parkPropertiesSourceDraftForTransition()
        state.clearMountedPropertiesSourceDraft()
        state.preservePropertyDraft(siblingRow, path: siblingPath, key: "title")
        state.propertiesSourceDraftPath = oldPath
        state.propertiesSourceDraft = sourceDraft
        state.preservePropertyDraft(rowDraft, path: oldPath, key: "title")

        // Produce a real missing-note recovery payload at the old child path,
        // then restore the physical file so the ancestor rename can succeed.
        let staging = fixture.vault.deletingLastPathComponent()
            .appendingPathComponent("folder-rename-recovery-staging")
        try FileManager.default.createDirectory(
            at: staging,
            withIntermediateDirectories: true)
        let stagedNote = staging.appendingPathComponent("note.md")
        try FileManager.default.moveItem(
            at: fixture.vault.appendingPathComponent(oldPath),
            to: stagedNote)
        let uncertain = StructuralBatchItem(path: oldPath, isDirectory: false)
        let unknownReport = unknownTrashReport([uncertain])
        state.batchTrashRunner = { _, _ in unknownReport }
        state.structuralBatchRefreshRunner = { _ in }
        let deletion = try XCTUnwrap(
            state.batchDelete(
                [.init(path: oldPath, isDirectory: false)],
                preferredFocusPath: oldPath))
        await deletion.value
        let oldRecovery = try XCTUnwrap(
            state.missingNoteRecoveryDraft(for: oldPath))
        try FileManager.default.moveItem(
            at: stagedNote,
            to: fixture.vault.appendingPathComponent(oldPath))

        let rename = try XCTUnwrap(
            state.renameEntry(
                path: "folder",
                isDirectory: true,
                to: "renamed"))
        await rename.value

        XCTAssertNil(state.missingNoteRecoveryDraft(for: oldPath))
        let movedRecovery = try XCTUnwrap(
            state.missingNoteRecoveryDraft(for: newPath))
        XCTAssertEqual(movedRecovery.path, newPath)
        XCTAssertEqual(movedRecovery.body, oldRecovery.body)
        XCTAssertEqual(movedRecovery.propertiesSourceDraft, sourceDraft)
        XCTAssertNil(state.preservedPropertyDraft(path: oldPath, key: "title"))
        XCTAssertEqual(
            state.preservedPropertyDraft(path: newPath, key: "title"),
            rowDraft)
        XCTAssertEqual(
            state.preservedPropertyDraft(path: siblingPath, key: "title"),
            siblingRow,
            "component-prefix siblings must remain byte-for-byte owned")

        XCTAssertEqual(state.propertiesSourceDraftPath, newPath)
        XCTAssertEqual(state.propertiesSourceDraft, sourceDraft)
        state.clearMountedPropertiesSourceDraft()
        XCTAssertTrue(state.restoreParkedPropertiesSourceDraft(for: siblingPath))
        XCTAssertEqual(state.propertiesSourceDraft, "title: prefix sibling source\n")
    }

    func testQuarantinedPropertyRowDraftRemainsSelectableAndCopyable() throws {
        let packageRoot = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
        let source = try String(
            contentsOf: packageRoot.appendingPathComponent(
                "Sources/SlateMac/PropertyEditorRow.swift"),
            encoding: .utf8)

        XCTAssertTrue(source.contains("Copy Property Draft"))
        XCTAssertTrue(source.contains(".textSelection(.enabled)"))
        XCTAssertTrue(source.contains("copyPropertyDraft"))
    }

    func testMissingNoteRecoveryDiscardDialogReturnsAccessibilityFocus() throws {
        let packageRoot = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
        let source = try String(
            contentsOf: packageRoot.appendingPathComponent(
                "Sources/SlateMac/NoteContentView.swift"),
            encoding: .utf8)

        XCTAssertTrue(source.contains("@AccessibilityFocusState private var missingRecoveryFocus"))
        XCTAssertTrue(source.contains("missingRecoveryFocus = .discardButton"))
        XCTAssertTrue(source.contains("missingRecoveryFocus = .errorHeading"))
        XCTAssertTrue(
            source.contains("$missingRecoveryFocus, equals: .discardButton"),
            "Cancel must return to the surviving destructive-action control")
        XCTAssertTrue(
            source.contains("$missingRecoveryFocus, equals: .errorHeading"),
            "destructive completion must return to a target that survives recovery removal")
    }

    func testPropertyPublicationRecoveryAlwaysReturnsKeyboardAndAccessibilityFocus()
        throws
    {
        let packageRoot = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
        let source = try String(
            contentsOf: packageRoot.appendingPathComponent(
                "Sources/SlateMac/NotePropertiesHeader.swift"),
            encoding: .utf8)
        XCTAssertTrue(
            source.contains(
                "@AccessibilityFocusState private var sourceToggleAccessibilityFocused"),
            "the surviving header control must accept VoiceOver focus")
        XCTAssertTrue(
            source.contains(
                "@FocusState private var sourceToggleKeyboardFocused"),
            "accessibility focus alone does not restore macOS keyboard focus")
        XCTAssertTrue(
            source.contains(
                ".accessibilityFocused($sourceToggleAccessibilityFocused)"))
        XCTAssertTrue(source.contains(".focused($sourceToggleKeyboardFocused)"))
        XCTAssertTrue(
            source.contains("private func restoreSourceToggleFocus()"))
        XCTAssertTrue(source.contains("sourceToggleAccessibilityFocused = true"))
        XCTAssertTrue(source.contains("sourceToggleKeyboardFocused = true"))
        XCTAssertTrue(
            source.contains("private func retryPropertyPublicationAndRestoreFocus()"),
            "Check Saved Version and Reload Properties need a focus handoff before their async retry removes the recovery subtree")
        XCTAssertEqual(
            source.components(
                separatedBy: "retryPropertyPublicationAndRestoreFocus()"
            ).count - 1,
            3,
            "the helper declaration plus both retry controls must share the same focus-safe path")
        XCTAssertTrue(
            source.contains(
                "propertyRecoveryFocusContinuity.start(taskWasStarted: task != nil)"))
        XCTAssertTrue(
            source.contains(".onChange(of: appState.isEditingProperty)"),
            "async completion must restore focus again after the recovery subtree is removed")

        let start = try XCTUnwrap(source.range(of: ".confirmationDialog("))
        let suffix = source[start.lowerBound...]
        let end = try XCTUnwrap(suffix.range(of: "} message: {"))
        let dialog = String(suffix[..<end.lowerBound])
        XCTAssertEqual(
            dialog.components(
                separatedBy: "trackPropertyRecoveryTask("
            ).count - 1,
            2,
            "Reapply Mine and Use Current Version must retain a completion focus return")
        XCTAssertTrue(
            dialog.contains("propertyRecoveryFocusContinuity.cancel()"))
        XCTAssertEqual(
            dialog.components(
                separatedBy: "sourceToggleKeyboardFocused = true"
            ).count - 1,
            3,
            "Reapply Mine, Use Current Version, and Cancel must each return macOS keyboard focus immediately")
        XCTAssertEqual(
            dialog.components(
                separatedBy: "sourceToggleAccessibilityFocused = true"
            ).count - 1,
            3,
            "Reapply Mine, Use Current Version, and Cancel must each return VoiceOver focus immediately")
    }

    func testPropertyRecoveryFocusContinuitySurvivesAsyncSubtreeRemoval() {
        var continuity = PropertyRecoveryFocusContinuity()

        continuity.start(taskWasStarted: true)
        XCTAssertTrue(continuity.awaitsAsyncCompletion)
        XCTAssertFalse(
            continuity.consumeCompletionFocusRequest(isEditing: true),
            "focus must not be reasserted while the recovery task still owns the subtree")
        XCTAssertTrue(
            continuity.consumeCompletionFocusRequest(isEditing: false),
            "completion must request focus after the launching recovery subtree disappears")
        XCTAssertFalse(continuity.awaitsAsyncCompletion)
        XCTAssertFalse(
            continuity.consumeCompletionFocusRequest(isEditing: false),
            "one completion may not steal focus more than once")

        continuity.start(taskWasStarted: false)
        XCTAssertFalse(
            continuity.consumeCompletionFocusRequest(isEditing: false),
            "a rejected recovery action has no later completion focus edge")

        continuity.start(taskWasStarted: true)
        continuity.cancel()
        XCTAssertFalse(
            continuity.consumeCompletionFocusRequest(isEditing: false),
            "a note switch cancels the pending return instead of focusing another note")
    }

    func testPropertyRecoverySurfacesStayReachableAndUseTruthfulCopy() throws {
        let packageRoot = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
        let sourceRoot = packageRoot.appendingPathComponent("Sources/SlateMac")
        let header = try String(
            contentsOf: sourceRoot.appendingPathComponent(
                "NotePropertiesHeader.swift"),
            encoding: .utf8)
        let missingNote = try String(
            contentsOf: sourceRoot.appendingPathComponent(
                "NoteContentView.swift"),
            encoding: .utf8)
        let row = try String(
            contentsOf: sourceRoot.appendingPathComponent(
                "PropertyEditorRow.swift"),
            encoding: .utf8)

        XCTAssertTrue(header.contains("ScrollView(.vertical)"))
        XCTAssertTrue(header.contains("maxHeight: 160"))
        XCTAssertTrue(
            header.contains(
                "activePropertyPublicationRecoveryReplacesAllProperties"))
        XCTAssertTrue(
            header.contains("replace all current saved properties"))

        XCTAssertTrue(missingNote.contains("Recovery copy preserved"))
        XCTAssertTrue(missingNote.contains("LazyVGrid("))
        XCTAssertFalse(missingNote.contains("Unsaved draft preserved"))

        XCTAssertFalse(
            row.contains("Saved update awaiting verification"),
            "the path-wide retained update belongs in the header, not every row")
        XCTAssertTrue(row.contains("Uncommitted property draft"))
        XCTAssertTrue(row.contains("Copy Property Draft"))
    }

    func testOpeningUnknownNativeDocumentsDuringRefreshCannotReattachWritableHandles()
        async throws
    {
        let fixture = try await makeTypedState()
        let state = fixture.state
        let canvasPath = "folder/later.canvas"
        let basePath = "folder/Later.base"
        try FileManager.default.copyItem(
            at: fixture.vault.appendingPathComponent("folder/board.canvas"),
            to: fixture.vault.appendingPathComponent(canvasPath))
        try FileManager.default.copyItem(
            at: fixture.vault.appendingPathComponent("folder/Reading.base"),
            to: fixture.vault.appendingPathComponent(basePath))
        let items = [
            StructuralBatchItem(path: canvasPath, isDirectory: false),
            StructuralBatchItem(path: basePath, isDirectory: false),
        ]
        let report = BatchTrashReport(
            envelope: StructuralBatchEnvelope(
                planned: items, skipped: [], preflightFailures: []),
            state: .failed,
            opId: nil,
            trashed: [],
            untrashed: [],
            unknown: items.map { item in
                BatchTrashRemainder(
                    item: item,
                    failure: BatchItemFailure(
                        item: item,
                        stage: .reconciliation,
                        message: "physical Trash verification failed"))
            },
            bookkeepingFailures: [],
            requiresRescan: true)
        state.batchTrashRunner = { _, _ in report }
        let refresh = SuspensionGate()
        state.structuralBatchRefreshRunner = { _ in await refresh.enter() }

        let task = try XCTUnwrap(
            state.batchDelete(
                items.map {
                    .init(path: $0.path, isDirectory: $0.isDirectory)
                },
                preferredFocusPath: canvasPath))
        await refresh.waitForEntrants(1)

        state.openFile(canvasPath, target: .newTab)
        let lateCanvas = try XCTUnwrap(state.canvasDocuments[canvasPath])
        XCTAssertNil(
            lateCanvas.handle,
            "activation during reconciliation must not reopen an ambiguous Canvas")

        state.openFile(basePath, target: .newTab)
        let lateBase = try XCTUnwrap(state.activeBaseDocument)
        XCTAssertNil(
            lateBase.handle,
            "activation during reconciliation must not reopen an ambiguous Base")

        state.dockBaseFileToSidebar(
            path: basePath,
            name: "Later",
            refreshDelayNanoseconds: 0)
        await state.basesDockRefreshTask?.value
        let lateDock = try XCTUnwrap(state.basesDockDocument)
        XCTAssertNil(
            lateDock.handle,
            "dock refresh during reconciliation must not reopen an ambiguous Base")

        await refresh.releaseOne()
        await task.value
        await state.nativeDocumentRetargetTask?.value

        XCTAssertNotNil(lateCanvas.handle)
        XCTAssertNotNil(lateBase.handle)
        XCTAssertNotNil(lateDock.handle)
    }

    func testIndeterminatePostRefreshProbeKeepsDirtyMarkdownQuarantined() async throws {
        let fixture = try await makeTypedState()
        let state = fixture.state
        let folder = fixture.vault.appendingPathComponent("folder")
        try FileManager.default.setAttributes(
            [.posixPermissions: 0], ofItemAtPath: folder.path)
        defer {
            try? FileManager.default.setAttributes(
                [.posixPermissions: 0o700], ofItemAtPath: folder.path)
        }
        let uncertain = StructuralBatchItem(
            path: "folder/note.md", isDirectory: false)
        let report = BatchTrashReport(
            envelope: StructuralBatchEnvelope(
                planned: [uncertain], skipped: [], preflightFailures: []),
            state: .failed,
            opId: nil,
            trashed: [],
            untrashed: [],
            unknown: [
                BatchTrashRemainder(
                    item: uncertain,
                    failure: BatchItemFailure(
                        item: uncertain,
                        stage: .reconciliation,
                        message: "permission denied during physical verification"))
            ],
            bookkeepingFailures: [],
            requiresRescan: true)
        state.batchTrashRunner = { _, _ in report }
        state.structuralBatchRefreshRunner = { _ in }

        let task = try XCTUnwrap(
            state.batchDelete(
                [.init(path: uncertain.path, isDirectory: false)],
                preferredFocusPath: uncertain.path))
        await task.value

        XCTAssertEqual(state.loadedFilePath, uncertain.path)
        XCTAssertEqual(state.currentNoteText, fixture.markdownText)
        XCTAssertTrue(state.hasUnsavedChanges)
        XCTAssertFalse(state.noteLoadError?.contains("moved to Trash") == true)
        XCTAssertNil(
            state.saveCurrentNote(),
            "a permission error is not evidence that the path is present or absent")
    }

    func testPersistentUnknownGateUsesComponentBoundariesAndBlocksOnlyMatchingWrites()
        async throws
    {
        let fixture = try await makeTypedState()
        let state = fixture.state
        let folder = StructuralBatchItem(path: "folder", isDirectory: true)
        let rootFile = StructuralBatchItem(path: "keep.md", isDirectory: false)
        let items = [folder, rootFile]
        let report = unknownTrashReport(items)
        state.batchTrashPresenceProbeRunner = { _, _ in .indeterminate }
        state.batchTrashRunner = { _, _ in report }
        state.structuralBatchRefreshRunner = { _ in }

        let task = try XCTUnwrap(
            state.batchDelete(
                items.map { .init(path: $0.path, isDirectory: $0.isDirectory) },
                preferredFocusPath: "folder/note.md"))
        await task.value

        let reason = AppState.batchTrashQuarantineReason
        XCTAssertEqual(state.batchTrashPathCapability(for: "folder"), .readOnly(reason))
        XCTAssertEqual(
            state.batchTrashPathCapability(for: "folder/note.md"),
            .readOnly(reason),
            "a quarantined folder covers descendants by path component")
        XCTAssertEqual(
            state.batchTrashPathCapability(for: "keep.md"), .readOnly(reason))
        XCTAssertEqual(
            state.batchTrashPathCapability(for: "keep.md/child"), .writable,
            "a quarantined file never covers a textual descendant")
        XCTAssertEqual(
            state.batchTrashPathCapability(for: "folderish/note.md"), .writable,
            "prefix lookalikes are unrelated")

        XCTAssertNil(state.saveCurrentNote())
        XCTAssertEqual(state.lastMutationAnnouncement, reason)

        let propertyTask = state.setProperty(
            path: "folder/note.md", key: "status", value: .text(value: "blocked"))
        XCTAssertNil(propertyTask)
        await propertyTask?.value

        let blockedCreate = state.createNote(in: "folder")
        XCTAssertNil(blockedCreate)
        await blockedCreate?.value
        XCTAssertFalse(
            FileManager.default.fileExists(
                atPath: fixture.vault.appendingPathComponent("folder/Untitled.md").path))

        let allowedCreate = try XCTUnwrap(
            state.createFolder(name: "Unrelated", in: ""),
            "an unrelated root path must stay writable")
        await allowedCreate.value
        XCTAssertTrue(
            FileManager.default.fileExists(
                atPath: fixture.vault.appendingPathComponent("Unrelated").path))
    }

    func testUnknownLedgerKeepsCanonicallyEquivalentPathsByteDistinct()
        async throws
    {
        let fixture = try await makeTypedState()
        let state = fixture.state
        let nfcPath = "unicode/\u{00E9}.md"
        let nfdPath = "unicode/e\u{0301}.md"
        XCTAssertFalse(
            BaseExactIdentity.matches(nfcPath, nfdPath),
            "the fixture paths must differ in their UTF-8 identity")

        let items = [
            StructuralBatchItem(path: nfcPath, isDirectory: false),
            StructuralBatchItem(path: nfdPath, isDirectory: false),
        ]
        let report = unknownTrashReport(items)
        state.batchTrashRunner = { _, _ in report }
        state.batchTrashPresenceProbeRunner = { _, _ in .indeterminate }
        state.structuralBatchRefreshRunner = { _ in }

        let quarantine = try XCTUnwrap(
            state.batchDelete(
                items.map { .init(path: $0.path, isDirectory: false) },
                preferredFocusPath: nfcPath))
        await quarantine.value

        let readOnly = BatchTrashPathCapability.readOnly(
            AppState.batchTrashQuarantineReason)
        XCTAssertEqual(state.batchTrashPathCapability(for: nfcPath), readOnly)
        XCTAssertEqual(state.batchTrashPathCapability(for: nfdPath), readOnly)
        XCTAssertEqual(
            state.batchTrashQuarantineNotice,
            "Slate couldn’t verify whether 2 items moved to Trash. They remain read-only.")
    }

    func testResolvingCurrentUnicodePathDoesNotClearByteDistinctPriorUnknown()
        async throws
    {
        let fixture = try await makeTypedState()
        let state = fixture.state
        let nfcPath = "unicode/\u{00E9}.md"
        let nfdPath = "unicode/e\u{0301}.md"
        XCTAssertFalse(BaseExactIdentity.matches(nfcPath, nfdPath))
        let presence = PresenceBox(.indeterminate)
        state.batchTrashPresenceProbeRunner = { _, _ in presence.presence }
        state.structuralBatchRefreshRunner = { _ in }

        let prior = StructuralBatchItem(path: nfdPath, isDirectory: false)
        let priorReport = unknownTrashReport([prior])
        state.batchTrashRunner = { _, _ in priorReport }
        let first = try XCTUnwrap(
            state.batchDelete(
                [.init(path: nfdPath, isDirectory: false)],
                preferredFocusPath: nfdPath))
        await first.value

        XCTAssertEqual(
            state.batchTrashPathCapability(for: nfdPath),
            .readOnly(AppState.batchTrashQuarantineReason))
        XCTAssertEqual(state.batchTrashPathCapability(for: nfcPath), .writable)

        presence.presence = .present
        let current = StructuralBatchItem(path: nfcPath, isDirectory: false)
        let currentReport = unknownTrashReport([current])
        state.batchTrashRunner = { _, _ in currentReport }
        let second = try XCTUnwrap(
            state.batchDelete(
                [.init(path: nfcPath, isDirectory: false)],
                preferredFocusPath: nfcPath))
        await second.value

        XCTAssertEqual(
            state.batchTrashPathCapability(for: nfdPath),
            .readOnly(AppState.batchTrashQuarantineReason),
            "resolving NFC must not subtract the byte-distinct NFD ledger entry")
        XCTAssertEqual(
            state.batchTrashPathCapability(for: nfcPath),
            .writable,
            "the explicitly resolved NFC path must become writable")
        XCTAssertEqual(
            state.batchTrashQuarantineNotice,
            "Slate couldn’t verify whether 1 item moved to Trash. It remains read-only.")
    }

    func testAncestorFolderTrashCannotBypassUnknownDescendantQuarantine() async throws {
        let fixture = try await makeTypedState()
        let state = fixture.state
        let uncertain = StructuralBatchItem(
            path: "folder/note.md", isDirectory: false)
        let report = unknownTrashReport([uncertain])
        state.batchTrashRunner = { _, _ in report }
        state.batchTrashPresenceProbeRunner = { _, _ in .indeterminate }
        state.structuralBatchRefreshRunner = { _ in }

        let quarantine = try XCTUnwrap(
            state.batchDelete(
                [.init(path: uncertain.path, isDirectory: false)],
                preferredFocusPath: uncertain.path))
        await quarantine.value

        state.requestDeleteEntry(
            path: "folder", isDirectory: true, knownChildCount: 1)

        XCTAssertNil(
            state.pendingFolderDelete,
            "a directory mutation must be denied when it contains a quarantined descendant")
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            AppState.batchTrashQuarantineReason)
        XCTAssertTrue(
            FileManager.default.fileExists(
                atPath: fixture.vault.appendingPathComponent("folder").path))
    }

    func testDuplicateSkipsQuarantinedCandidateWithoutTouchingIt() async throws {
        let fixture = try await makeTypedState()
        let state = fixture.state
        let uncertain = StructuralBatchItem(
            path: "folder/a copy.md", isDirectory: false)
        let report = unknownTrashReport([uncertain])
        state.batchTrashRunner = { _, _ in report }
        state.batchTrashPresenceProbeRunner = { _, _ in .indeterminate }
        state.structuralBatchRefreshRunner = { _ in }

        let quarantine = try XCTUnwrap(
            state.batchDelete(
                [.init(path: uncertain.path, isDirectory: false)],
                preferredFocusPath: uncertain.path))
        await quarantine.value

        let duplicate = try XCTUnwrap(state.duplicateEntry(path: "folder/a.md"))
        await duplicate.value

        XCTAssertFalse(
            FileManager.default.fileExists(
                atPath: fixture.vault.appendingPathComponent(uncertain.path).path),
            "an outcome-unknown candidate must never be recreated or overwritten")
        XCTAssertEqual(
            try String(
                contentsOf: fixture.vault.appendingPathComponent("folder/a copy 2.md"),
                encoding: .utf8),
            "# Duplicate source\n")
    }

    func testFL05ExternalImportReservesDirectQuarantinedTrashIdentity()
        async throws
    {
        let fixture = try await makeTypedState()
        let state = fixture.state
        let uncertain = StructuralBatchItem(
            path: "dest/a.md", isDirectory: false)
        let report = unknownTrashReport([uncertain])
        state.batchTrashRunner = { _, _ in report }
        state.batchTrashPresenceProbeRunner = { _, _ in .indeterminate }
        state.structuralBatchRefreshRunner = { _ in }
        let quarantine = try XCTUnwrap(state.batchDelete(
            [.init(path: uncertain.path, isDirectory: false)],
            preferredFocusPath: uncertain.path))
        await quarantine.value

        let externalRoot = try canonicalTemporaryDirectory()
            .appendingPathComponent("fl05-quarantine-\(UUID().uuidString)")
        tempDirs.append(externalRoot)
        try FileManager.default.createDirectory(
            at: externalRoot, withIntermediateDirectories: false)
        let external = externalRoot.appendingPathComponent("a.md")
        try "new external bytes".write(
            to: external, atomically: true, encoding: .utf8)
        let vault = fixture.vault
        state.importDestinationCreators = { _ in
            SidebarImportDestinationCreators(
                createFile: { path, data in
                    try data.write(to: vault.appendingPathComponent(path))
                },
                createDirectory: { path in
                    try FileManager.default.createDirectory(
                        at: vault.appendingPathComponent(path),
                        withIntermediateDirectories: false)
                })
        }
        state.importInventoryRefreshRunner = { _, _ in }
        let owner = try XCTUnwrap(state.beginImportBatch(
            providers: [fileURLProvider(external)],
            destinationFolder: "dest"))
        await state.startImportBatch(owner).value

        XCTAssertFalse(
            FileManager.default.fileExists(
                atPath: fixture.vault.appendingPathComponent("dest/a.md").path))
        XCTAssertEqual(
            try String(
                contentsOf: fixture.vault.appendingPathComponent("dest/a 2.md"),
                encoding: .utf8),
            "new external bytes")
    }

    func testFL05ExternalFolderImportReservesAncestorOfNestedQuarantine()
        async throws
    {
        let fixture = try await makeTypedState()
        let state = fixture.state
        let uncertain = StructuralBatchItem(
            path: "dest/sub/a.md", isDirectory: false)
        let report = unknownTrashReport([uncertain])
        state.batchTrashRunner = { _, _ in report }
        state.batchTrashPresenceProbeRunner = { _, _ in .indeterminate }
        state.structuralBatchRefreshRunner = { _ in }
        let quarantine = try XCTUnwrap(state.batchDelete(
            [.init(path: uncertain.path, isDirectory: false)],
            preferredFocusPath: uncertain.path))
        await quarantine.value

        let externalRoot = try canonicalTemporaryDirectory()
            .appendingPathComponent("fl05-quarantine-\(UUID().uuidString)")
        tempDirs.append(externalRoot)
        try FileManager.default.createDirectory(
            at: externalRoot, withIntermediateDirectories: false)
        let external = externalRoot.appendingPathComponent("sub", isDirectory: true)
        try FileManager.default.createDirectory(
            at: external, withIntermediateDirectories: false)
        try "new nested bytes".write(
            to: external.appendingPathComponent("new.md"),
            atomically: true,
            encoding: .utf8)
        let vault = fixture.vault
        state.importDestinationCreators = { _ in
            SidebarImportDestinationCreators(
                createFile: { path, data in
                    try data.write(to: vault.appendingPathComponent(path))
                },
                createDirectory: { path in
                    try FileManager.default.createDirectory(
                        at: vault.appendingPathComponent(path),
                        withIntermediateDirectories: false)
                })
        }
        state.importInventoryRefreshRunner = { _, _ in }
        let owner = try XCTUnwrap(state.beginImportBatch(
            providers: [fileURLProvider(external)],
            destinationFolder: "dest"))
        await state.startImportBatch(owner).value

        XCTAssertFalse(
            FileManager.default.fileExists(
                atPath: fixture.vault.appendingPathComponent("dest/sub").path))
        XCTAssertEqual(
            try String(
                contentsOf: fixture.vault.appendingPathComponent(
                    "dest/sub 2/new.md"),
                encoding: .utf8),
            "new nested bytes")
    }

    func testDuplicateQuarantineDoesNotSpendTheCreateRaceCollisionBudget()
        async throws
    {
        let fixture = try await makeTypedState()
        let state = fixture.state
        for ordinal in 1...199 {
            let name =
                ordinal == 1
                ? "a copy.md"
                : "a copy \(ordinal).md"
            try "occupied \(ordinal)\n".write(
                to: fixture.vault.appendingPathComponent("folder/\(name)"),
                atomically: true,
                encoding: .utf8)
        }
        let session = try XCTUnwrap(state.currentSession)
        _ = try session.scanInitial(cancel: CancelToken())

        let uncertain = StructuralBatchItem(
            path: "folder/a copy 200.md", isDirectory: false)
        let report = unknownTrashReport([uncertain])
        state.batchTrashRunner = { _, _ in report }
        state.batchTrashPresenceProbeRunner = { _, _ in .indeterminate }
        state.structuralBatchRefreshRunner = { _ in }
        let quarantine = try XCTUnwrap(
            state.batchDelete(
                [.init(path: uncertain.path, isDirectory: false)],
                preferredFocusPath: uncertain.path))
        await quarantine.value

        let duplicate = try XCTUnwrap(state.duplicateEntry(path: "folder/a.md"))
        await duplicate.value

        XCTAssertFalse(
            FileManager.default.fileExists(
                atPath: fixture.vault.appendingPathComponent(uncertain.path).path))
        XCTAssertEqual(
            try String(
                contentsOf: fixture.vault.appendingPathComponent(
                    "folder/a copy 201.md"),
                encoding: .utf8),
            "# Duplicate source\n",
            "listed collisions and quarantined candidates must not consume race retries")
    }

    func testNewFolderThenMovePreflightsUnknownGateBeforeCreatingFolder()
        async throws
    {
        let fixture = try await makeTypedState()
        let state = fixture.state
        let uncertain = StructuralBatchItem(
            path: "folder/unknown.md", isDirectory: false)
        let report = unknownTrashReport([uncertain])
        state.batchTrashRunner = { _, _ in report }
        state.batchTrashPresenceProbeRunner = { _, _ in .indeterminate }
        state.structuralBatchRefreshRunner = { _ in }

        let quarantine = try XCTUnwrap(
            state.batchDelete(
                [.init(path: uncertain.path, isDirectory: false)],
                preferredFocusPath: uncertain.path))
        await quarantine.value

        XCTAssertNil(
            state.createFolderThenMove(
                newFolderName: "Single Move",
                in: "",
                movePath: "Notes/Alpha.md",
                isDirectory: false))
        XCTAssertNil(
            state.createFolderThenBatchMove(
                newFolderName: "Batch Move",
                in: "",
                items: [.init(path: "Notes/Alpha.md", isDirectory: false)]))
        XCTAssertFalse(
            FileManager.default.fileExists(
                atPath: fixture.vault.appendingPathComponent("Single Move").path))
        XCTAssertFalse(
            FileManager.default.fileExists(
                atPath: fixture.vault.appendingPathComponent("Batch Move").path))
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            AppState.batchTrashQuarantineReason)
    }

    func testPresentAncestorCannotResumeExplicitlyIndeterminateDescendantDocuments()
        async throws
    {
        let fixture = try await makeTypedState()
        let state = fixture.state
        let folder = StructuralBatchItem(path: "folder", isDirectory: true)
        let canvas = StructuralBatchItem(
            path: "folder/board.canvas", isDirectory: false)
        let base = StructuralBatchItem(
            path: "folder/Reading.base", isDirectory: false)
        let items = [folder, canvas, base]
        let report = unknownTrashReport(items)
        state.batchTrashRunner = { _, _ in report }
        state.batchTrashPresenceProbeRunner = { url, _ in
            url.lastPathComponent == "folder" ? .present : .indeterminate
        }
        state.structuralBatchRefreshRunner = { _ in }

        let quarantine = try XCTUnwrap(
            state.batchDelete(
                items.map { .init(path: $0.path, isDirectory: $0.isDirectory) },
                preferredFocusPath: canvas.path))
        await quarantine.value
        await state.nativeDocumentRetargetTask?.value

        XCTAssertNil(
            fixture.canvas.handle,
            "a present ancestor must not reopen an explicitly indeterminate Canvas")
        XCTAssertNil(
            fixture.base.handle,
            "a present ancestor must not reopen an explicitly indeterminate Base")
        XCTAssertEqual(
            state.batchTrashPathCapability(for: canvas.path),
            .readOnly(AppState.batchTrashQuarantineReason))
        XCTAssertEqual(
            state.batchTrashPathCapability(for: base.path),
            .readOnly(AppState.batchTrashQuarantineReason))
    }

    func testQuarantineCanonicalizesCoreEquivalentPathAliasesAndRejectsTraversal()
        async throws
    {
        let fixture = try await makeTypedState()
        let state = fixture.state
        let uncertain = StructuralBatchItem(
            path: "folder/note.md", isDirectory: false)
        let report = unknownTrashReport([uncertain])
        state.batchTrashRunner = { _, _ in report }
        state.batchTrashPresenceProbeRunner = { _, _ in .indeterminate }
        state.structuralBatchRefreshRunner = { _ in }

        let quarantine = try XCTUnwrap(
            state.batchDelete(
                [.init(path: uncertain.path, isDirectory: false)],
                preferredFocusPath: uncertain.path))
        await quarantine.value

        let readOnly = BatchTrashPathCapability.readOnly(
            AppState.batchTrashQuarantineReason)
        XCTAssertEqual(
            state.batchTrashPathCapability(for: "./folder/note.md"), readOnly)
        XCTAssertEqual(
            state.batchTrashPathCapability(for: "folder//note.md"), readOnly)
        XCTAssertEqual(
            state.batchTrashPathCapability(for: "folder/note.md/"), readOnly)
        XCTAssertFalse(state.admitBatchTrashWrite(to: ["../folder/note.md"]))
        XCTAssertFalse(state.admitBatchTrashWrite(to: ["/folder/note.md"]))
    }

    func testPhysicalPresenceTypeMismatchIsNotAcceptedAsTheOriginalItem()
        async throws
    {
        let fixture = try await makeTypedState()
        let state = fixture.state
        let uncertain = StructuralBatchItem(
            path: "folder/note.md", isDirectory: false)
        let path = fixture.vault.appendingPathComponent(uncertain.path)
        try FileManager.default.removeItem(at: path)
        try FileManager.default.createDirectory(at: path, withIntermediateDirectories: false)
        let report = unknownTrashReport([uncertain])
        state.batchTrashRunner = { _, _ in report }
        state.structuralBatchRefreshRunner = { _ in }

        let quarantine = try XCTUnwrap(
            state.batchDelete(
                [.init(path: uncertain.path, isDirectory: false)],
                preferredFocusPath: uncertain.path))
        await quarantine.value

        XCTAssertEqual(
            state.loadedFilePath,
            uncertain.path,
            "an opposite-kind replacement must preserve the dirty original as recovery")
        XCTAssertEqual(state.currentNoteText, fixture.markdownText)
        XCTAssertTrue(state.hasUnsavedChanges)
        XCTAssertNotNil(state.missingNoteRecoveryDraft(for: uncertain.path))
        XCTAssertTrue(state.noteLoadError?.contains("moved to Trash") == true)
        XCTAssertEqual(state.batchTrashPathCapability(for: uncertain.path), .writable)
    }

    func testNewCanvasDoesNotOverblockAnEarlierWritableCandidate() async throws {
        let fixture = try await makeTypedState()
        let state = fixture.state
        let uncertain = StructuralBatchItem(
            path: "Untitled Canvas 2.canvas", isDirectory: false)
        let report = unknownTrashReport([uncertain])
        state.batchTrashRunner = { _, _ in report }
        state.batchTrashPresenceProbeRunner = { _, _ in .indeterminate }
        state.structuralBatchRefreshRunner = { _ in }

        let quarantine = try XCTUnwrap(
            state.batchDelete(
                [.init(path: uncertain.path, isDirectory: false)],
                preferredFocusPath: uncertain.path))
        await quarantine.value

        let create = try XCTUnwrap(state.canvasNewCanvasFile())
        await create.value

        XCTAssertTrue(
            FileManager.default.fileExists(
                atPath: fixture.vault.appendingPathComponent(
                    "Untitled Canvas.canvas").path))
        XCTAssertFalse(
            FileManager.default.fileExists(
                atPath: fixture.vault.appendingPathComponent(uncertain.path).path))
    }

    func testDismissedUnknownReportRemainsRecoverableThroughPersistentCheckAgain()
        async throws
    {
        let fixture = try await makeTypedState()
        let state = fixture.state
        let uncertain = StructuralBatchItem(
            path: "folder/note.md", isDirectory: false)
        let presence = PresenceBox(.indeterminate)
        let report = unknownTrashReport([uncertain])
        state.batchTrashPresenceProbeRunner = { _, _ in presence.presence }
        state.batchTrashRunner = { _, _ in report }
        state.structuralBatchRefreshRunner = { _ in }

        let task = try XCTUnwrap(
            state.batchDelete(
                [.init(path: uncertain.path, isDirectory: false)],
                preferredFocusPath: uncertain.path))
        await task.value
        XCTAssertEqual(
            state.batchTrashPathCapability(for: uncertain.path),
            .readOnly(AppState.batchTrashQuarantineReason))

        let result = try XCTUnwrap(state.batchStructuralResult)
        XCTAssertTrue(state.dismissBatchStructuralResult(id: result.id))
        XCTAssertNotNil(
            state.batchTrashQuarantineNotice,
            "dismissal must leave a persistent, discoverable recovery surface")

        presence.presence = .present
        let refresh = SuspensionGate()
        state.structuralBatchRefreshRunner = { _ in await refresh.enter() }
        let recovery = try XCTUnwrap(state.retryBatchTrashUnknownReconciliation())
        await refresh.waitForEntrants(1)
        XCTAssertEqual(
            state.batchTrashPathCapability(for: uncertain.path),
            .readOnly(AppState.batchTrashQuarantineReason),
            "refresh suspension must not clear the write gate early")

        await refresh.releaseOne()
        await recovery.value
        await state.nativeDocumentRetargetTask?.value

        XCTAssertEqual(state.batchTrashPathCapability(for: uncertain.path), .writable)
        XCTAssertNil(state.batchTrashQuarantineNotice)
        let save = try XCTUnwrap(state.saveCurrentNote())
        await save.value
        XCTAssertEqual(
            try String(
                contentsOf: fixture.vault.appendingPathComponent(uncertain.path),
                encoding: .utf8),
            fixture.markdownText)
    }

    func testUnknownRecoveryCannotPublishIntoAReplacementVault() async throws {
        let fixture = try await makeTypedState()
        let state = fixture.state
        let uncertain = StructuralBatchItem(
            path: "folder/note.md", isDirectory: false)
        let presence = PresenceBox(.indeterminate)
        let report = unknownTrashReport([uncertain])
        state.batchTrashPresenceProbeRunner = { _, _ in presence.presence }
        state.batchTrashRunner = { _, _ in report }
        state.structuralBatchRefreshRunner = { _ in }

        let initial = try XCTUnwrap(
            state.batchDelete(
                [.init(path: uncertain.path, isDirectory: false)],
                preferredFocusPath: uncertain.path))
        await initial.value

        presence.presence = .present
        let refresh = SuspensionGate()
        state.structuralBatchRefreshRunner = { _ in await refresh.enter() }
        let recovery = try XCTUnwrap(state.retryBatchTrashUnknownReconciliation())
        await refresh.waitForEntrants(1)

        let replacement = fixture.vault.deletingLastPathComponent()
            .appendingPathComponent("recovery-replacement")
        try FileManager.default.createDirectory(
            at: replacement, withIntermediateDirectories: true)
        try "# Replacement\n".write(
            to: replacement.appendingPathComponent("note.md"),
            atomically: true,
            encoding: .utf8)
        state.openVault(at: replacement)
        XCTAssertEqual(state.currentVaultURL, fixture.vault)
        XCTAssertEqual(state.pendingNavigation, .closeVault)
        XCTAssertEqual(state.pendingVaultSwitchTarget?.path, replacement.path)
        state.resolvePendingNavigationDiscard()
        await state.scanTask?.value
        let mutationBeforeRelease = state.treeMutation
        let announcementBeforeRelease = state.lastMutationAnnouncement

        await refresh.releaseOne()
        await recovery.value

        XCTAssertEqual(
            state.currentVaultURL?.standardizedFileURL.path,
            replacement.standardizedFileURL.path)
        XCTAssertEqual(state.treeMutation, mutationBeforeRelease)
        XCTAssertEqual(state.lastMutationAnnouncement, announcementBeforeRelease)
        XCTAssertNil(state.batchTrashQuarantineNotice)
        XCTAssertFalse(state.isMutatingStructure)
    }

    func testStaleBatchReportCannotQuarantineMatchingPathsInReplacementVault() async throws {
        let fixture = try await makeTypedState()
        let state = fixture.state
        let runner = SuspensionGate()
        let report = BatchMoveReport(
            envelope: StructuralBatchEnvelope(
                planned: trashItems(), skipped: [], preflightFailures: []),
            state: .succeeded,
            opId: 800,
            standing: [
                BatchPathChange(
                    oldPath: "folder/note.md",
                    newPath: "dest/note.md",
                    isDirectory: false)
            ],
            rolledBack: [],
            failure: nil,
            rollbackFailures: [],
            rewritten: [],
            rewriteFailures: [],
            requiresRescan: false)
        state.batchMoveRunner = { _, _ in
            await runner.enter()
            return report
        }

        let staleTask = try XCTUnwrap(
            state.batchMove(
                moveItems(),
                to: "dest",
                preferredFocusPath: "folder/note.md"))
        await runner.waitForEntrants(1)

        let replacementRoot = fixture.vault.deletingLastPathComponent()
            .appendingPathComponent("replacement")
        try FileManager.default.createDirectory(
            at: replacementRoot.appendingPathComponent("folder"),
            withIntermediateDirectories: true)
        try "# Replacement\n".write(
            to: replacementRoot.appendingPathComponent("folder/note.md"),
            atomically: true,
            encoding: .utf8)
        state.openVault(at: replacementRoot)
        XCTAssertEqual(state.currentVaultURL, fixture.vault)
        XCTAssertEqual(state.pendingNavigation, .closeVault)
        XCTAssertEqual(state.pendingVaultSwitchTarget?.path, replacementRoot.path)
        state.resolvePendingNavigationDiscard()
        await state.scanTask?.value
        // Isolate the stale structural report from the direct-vault-switch
        // note-buffer lifecycle: the replacement tab must start from disk.
        state.clearActiveNoteFields()
        state.openFile("folder/note.md", target: .newTab)
        await state.noteLoadTask?.value
        let replacementTabID = try XCTUnwrap(
            state.workspace.model.activeGroup.activeTabID)

        await runner.releaseOne()
        await staleTask.value

        XCTAssertEqual(
            state.workspace.model.activeGroup.activeTabID,
            replacementTabID)
        XCTAssertEqual(
            state.workspace.model.activeGroup.activeTab?.item,
            .markdown(path: "folder/note.md"))
        XCTAssertEqual(state.loadedFilePath, "folder/note.md")
        XCTAssertEqual(state.currentNoteText, "# Replacement\n")
        XCTAssertNil(state.treeMutation)
        XCTAssertNil(state.batchStructuralResult)
        XCTAssertNil(state.lastMutationAnnouncement)
        XCTAssertFalse(state.isMutatingStructure)
    }
}
