// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import Combine
import UniformTypeIdentifiers
import XCTest

@testable import SlateMac

/// #870: file-URL drag flavors — drag notes OUT to Finder and accept file
/// drops IN (external ⇒ import, in-vault ⇒ move).
///
/// The NSItemProvider plumbing and the `.onDrop` wiring aren't drivable from
/// XCTest, so these tests pin the extracted, load-bearing seams:
///  - `makeDragProvider` registers BOTH the private type AND `public.file-url`,
///  - the pure `fileURLDropAction` import-vs-move decision (+ its no-op guards),
///  - `importEntry` copies an external file in (reusing the collision surface),
///  - and an in-vault file-URL drop resolves to a move that lands on disk.
@MainActor
final class FileTreeDragDropTests: XCTestCase {

    private var tempDir: URL!

    override func setUpWithError() throws {
        let temporaryDirectoryPath = try XCTUnwrap(
            try FileManager.default.temporaryDirectory
                .resourceValues(forKeys: [.canonicalPathKey])
                .canonicalPath)
        tempDir = URL(
            fileURLWithPath: temporaryDirectoryPath, isDirectory: true)
            .appendingPathComponent("dnd-fileurl-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: tempDir, withIntermediateDirectories: true)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: tempDir)
    }

    private func makeVault(files: [String]) async throws -> (AppState, URL) {
        let vault = tempDir.appendingPathComponent("vault")
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
                fileURL: tempDir.appendingPathComponent("recents.json")),
            externalOpener: { _ in true })
        state.openVault(at: vault)
        await state.scanTask?.value
        return (state, vault)
    }

    private func makeVault(contents: [String: String]) async throws -> (AppState, URL) {
        let vault = tempDir.appendingPathComponent("vault")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        for (rel, contents) in contents {
            let url = vault.appendingPathComponent(rel)
            try FileManager.default.createDirectory(
                at: url.deletingLastPathComponent(), withIntermediateDirectories: true)
            try contents.write(to: url, atomically: true, encoding: .utf8)
        }
        let state = AppState(
            recentsStore: RecentVaultsStore(
                fileURL: tempDir.appendingPathComponent("recents.json")),
            externalOpener: { _ in true })
        state.openVault(at: vault)
        await state.scanTask?.value
        return (state, vault)
    }

    private func exists(_ vault: URL, _ rel: String) -> Bool {
        FileManager.default.fileExists(atPath: vault.appendingPathComponent(rel).path)
    }

    private func fileRow(_ path: String) -> FileTreeSidebar.RowID {
        .node(.file(path: path))
    }

    /// Deterministic suspension for structural-busy admission tests. The first
    /// batch occupies AppState's structural gate until the test explicitly
    /// releases it; no timing sleeps or filesystem races are involved.
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
            await withCheckedContinuation { continuation in
                waiters.append(continuation)
            }
        }

        func waitForEntrants(_ expected: Int) async {
            guard entrants < expected else { return }
            await withCheckedContinuation { continuation in
                entrantWaiters.append((expected, continuation))
            }
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

    private final class SecondPreparationGate: @unchecked Sendable {
        private let lock = NSLock()
        private var rootAdmissionCount = 0
        private let secondEntered = DispatchSemaphore(value: 0)
        private let releaseSecond = DispatchSemaphore(value: 0)

        func reach(_ boundary: SidebarImportSourceBoundary) {
            guard case .beforeRootAdmission = boundary else { return }
            lock.lock()
            rootAdmissionCount += 1
            let shouldBlock = rootAdmissionCount == 2
            lock.unlock()
            guard shouldBlock else { return }
            secondEntered.signal()
            releaseSecond.wait()
        }

        func waitForSecondRoot() async {
            await Task.detached { self.secondEntered.wait() }.value
        }

        func release() {
            releaseSecond.signal()
        }
    }

    private actor BatchMoveProbe {
        private(set) var requests: [BatchMoveRequest] = []
        let report: BatchMoveReport

        init(report: BatchMoveReport) {
            self.report = report
        }

        func run(_ request: BatchMoveRequest) -> BatchMoveReport {
            requests.append(request)
            return report
        }

        func lastRequest() -> BatchMoveRequest? { requests.last }
        func callCount() -> Int { requests.count }
    }

    private actor ImportRefreshProbe {
        private(set) var listRefreshCount = 0
        private(set) var scanCount = 0

        func recordListRefresh() { listRefreshCount += 1 }
        func recordScan() { scanCount += 1 }
        func counts() -> (list: Int, scan: Int) {
            (listRefreshCount, scanCount)
        }
    }

    private final class ProviderLoadCounter: @unchecked Sendable {
        private let lock = NSLock()
        private var counts: [Int: Int] = [:]

        func record(_ index: Int) {
            lock.lock()
            counts[index, default: 0] += 1
            lock.unlock()
        }

        func count(for index: Int) -> Int {
            lock.lock()
            defer { lock.unlock() }
            return counts[index, default: 0]
        }
    }

    /// Drives the same two app-to-tree edges used by `FileTreeSidebar`: path
    /// changes are neutral mirrors, while an explicit navigation revision is a
    /// monotone user-intent edge. Applying the eventual import mutation through
    /// this bridge makes the deferred-selection race deterministic without
    /// depending on SwiftUI scheduling.
    @MainActor
    private final class ImportSelectionBridge {
        typealias Model = SidebarSelectionModel<FileTreeSidebar.RowID>
        typealias Row = Model.VisibleRow

        private let state: AppState
        private var rows: [Row] = []
        private var cancellables = Set<AnyCancellable>()
        private(set) var model = Model()

        init(state: AppState, initialPath: String) {
            self.state = state
            mirror(path: initialPath)
            state.$sidebarSelectionIntentRevision
                .dropFirst()
                .sink { [weak self] _ in self?.noteExplicitNavigation() }
                .store(in: &cancellables)
            state.$selectedFilePath
                .dropFirst()
                .sink { [weak self] path in self?.mirror(path: path) }
                .store(in: &cancellables)
        }

        var focusedPath: String? {
            model.focused.flatMap { focused in
                rows.first(where: { $0.identity == focused })?.path
            }
        }

        func applyImportMutation(_ mutation: AppState.TreeMutation) -> Bool {
            guard let expectedRevision = mutation.selectionRevision,
                case let .importBatch(materialized, _, _) = mutation.kind
            else { return false }
            let importedRows = materialized.map { row(path: $0.path) }
            let outcome = model.selectImportedResults(
                importedRows,
                ifSelectionRevisionIs: expectedRevision)
            if outcome.handled { publish() }
            return outcome.handled
        }

        private func noteExplicitNavigation() {
            model.noteExternalNavigationIntent()
            publish()
        }

        private func mirror(path: String?) {
            model.reveal(path.map(row(path:)))
            publish()
        }

        private func row(path: String) -> Row {
            if let existing = rows.first(where: { $0.path == path }) {
                return existing
            }
            let row = Row(
                identity: .node(.file(path: path)),
                path: path,
                isDirectory: false)
            rows.append(row)
            return row
        }

        private func publish() {
            guard let session = state.currentSession else { return }
            _ = state.publishSidebarSelectionSnapshot(
                SidebarSelectionSnapshot.capture(
                    sessionIdentity: ObjectIdentifier(session),
                    model: model,
                    visibleRows: rows))
        }
    }

    private func assertRunningImportPreservesExplicitNavigation(
        state: AppState,
        initialPath: String,
        expectedPath: String,
        navigate: () -> Void,
        file: StaticString = #filePath,
        line: UInt = #line
    ) async throws {
        XCTAssertEqual(state.selectedFilePath, initialPath, file: file, line: line)
        let bridge = ImportSelectionBridge(state: state, initialPath: initialPath)
        let capturedSelectionRevision = bridge.model.selectionRevision
        let appRevisionBeforeNavigation = state.sidebarSelectionIntentRevision
        let source = tempDir.appendingPathComponent(
            "source-\(UUID().uuidString).md")
        try Data("# Imported\n".utf8).write(to: source)
        let refreshGate = SuspensionGate()
        state.importInventoryRefreshRunner = { _, _ in
            await refreshGate.enter()
        }
        let owner = try XCTUnwrap(
            state.beginImportBatch(
                providers: [fileURLProvider(source)],
                destinationFolder: "",
                selectionRevision: capturedSelectionRevision),
            file: file,
            line: line)
        let importTask = state.startImportBatch(owner)
        await refreshGate.waitForEntrants(1)

        navigate()

        XCTAssertEqual(
            state.sidebarSelectionIntentRevision,
            appRevisionBeforeNavigation &+ 1,
            "each explicit navigation must advance exactly once",
            file: file,
            line: line)
        XCTAssertEqual(state.selectedFilePath, expectedPath, file: file, line: line)
        XCTAssertEqual(bridge.focusedPath, expectedPath, file: file, line: line)
        XCTAssertEqual(state.treeSelectedNode?.path, expectedPath, file: file, line: line)

        await refreshGate.releaseOne()
        await importTask.value

        let mutation = try XCTUnwrap(state.treeMutation, file: file, line: line)
        XCTAssertEqual(
            mutation.selectionRevision,
            capturedSelectionRevision,
            "the import must retain the selection revision captured at admission",
            file: file,
            line: line)
        XCTAssertFalse(
            bridge.applyImportMutation(mutation),
            "completion must reject its whole deferred landing after newer navigation",
            file: file,
            line: line)
        XCTAssertEqual(bridge.focusedPath, expectedPath, file: file, line: line)
        XCTAssertEqual(
            state.treeSelectedNode?.path,
            expectedPath,
            "the sidebar command target must remain on the newer navigation",
            file: file,
            line: line)
    }

    private func batchItem(_ path: String, dir: Bool = false) -> StructuralBatchItem {
        StructuralBatchItem(path: path, isDirectory: dir)
    }

    private func batchMoveReport(
        state: BatchMoveState,
        planned: [StructuralBatchItem],
        opID: Int64? = nil,
        standing: [BatchPathChange] = [],
        rolledBack: [BatchPathChange] = [],
        skipped: [BatchSkippedItem] = [],
        preflightFailures: [BatchItemFailure] = [],
        failure: BatchItemFailure? = nil,
        rollbackFailures: [BatchItemFailure] = [],
        rewriteFailures: [RewriteFailure] = [],
        requiresRescan: Bool = false
    ) -> BatchMoveReport {
        BatchMoveReport(
            envelope: StructuralBatchEnvelope(
                planned: planned,
                skipped: skipped,
                preflightFailures: preflightFailures),
            state: state,
            opId: opID,
            standing: standing,
            rolledBack: rolledBack,
            failure: failure,
            rollbackFailures: rollbackFailures,
            rewritten: [],
            rewriteFailures: rewriteFailures,
            requiresRescan: requiresRescan)
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

    private func seedBatchMoveHistory(
        in state: AppState,
        path: String,
        destination: String,
        opID: Int64
    ) async throws -> AppState.StructuralUndoOp {
        let standing = [BatchPathChange(
            oldPath: path,
            newPath: destination + "/" + (path as NSString).lastPathComponent,
            isDirectory: false)]
        let report = batchMoveReport(
            state: .succeeded,
            planned: [batchItem(path)],
            opID: opID,
            standing: standing)
        state.batchMoveRunner = { _, _ in report }
        state.structuralBatchRefreshRunner = { _ in }
        await state.batchMove(
            [AppState.TreeSelection(path: path, isDirectory: false)],
            to: destination,
            preferredFocusPath: path)?.value
        return try XCTUnwrap(state.structuralUndoStack.last)
    }

    private func parkStructuralMutation(
        in state: AppState,
        gate: SuspensionGate,
        path: String = "busy.md"
    ) async throws -> Task<Void, Never> {
        let report = batchMoveReport(
            state: .noOp, planned: [batchItem(path)])
        state.batchMoveRunner = { _, _ in
            await gate.enter()
            return report
        }
        state.structuralBatchRefreshRunner = { _ in }
        let task = try XCTUnwrap(
            state.batchMove(
                [AppState.TreeSelection(path: path, isDirectory: false)],
                to: "busy-destination", preferredFocusPath: nil))
        await gate.waitForEntrants(1)
        return task
    }

    /// Anchor source-contract checks to the compiler-emitted test-file path so
    /// they are independent of the test process's current working directory.
    /// The `Sources/SlateMac` layout is intentionally part of the pin: a source
    /// refactor must fail these checks until the wiring contract is re-audited.
    private static func source(_ filename: String) throws -> String {
        let url = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()  // SlateMacTests
            .deletingLastPathComponent()  // Tests
            .deletingLastPathComponent()  // slate-mac
            .appendingPathComponent("Sources/SlateMac/")
            .appendingPathComponent(filename)
        return try String(contentsOf: url, encoding: .utf8)
    }

    private func assertBusyDropRejectsBeforeSessionEndOrProviderLoad(
        _ preferred: FileTreeSidebar.PreferredDropProvider,
        state: AppState,
        busyTask: Task<Void, Never>,
        gate: SuspensionGate,
        file: StaticString = #filePath,
        line: UInt = #line
    ) async {
        var announcements: [String] = []
        let cancellable = state.$lastMutationAnnouncement
            .dropFirst()
            .compactMap { $0 }
            .sink { announcements.append($0) }
        var endSessionCount = 0
        var providerLoadCount = 0

        let accepted = FileTreeSidebar.performAdmittedDrop(
            preferred, appState: state
        ) { _ in
            endSessionCount += 1
            providerLoadCount += 1
            return true
        }

        XCTAssertFalse(accepted, "busy drops must report rejection", file: file, line: line)
        XCTAssertEqual(endSessionCount, 0, file: file, line: line)
        XCTAssertEqual(providerLoadCount, 0, file: file, line: line)
        XCTAssertEqual(
            announcements, [AppState.structuralMutationBusyReason],
            "busy-at-drop announces exactly once", file: file, line: line)

        withExtendedLifetime(cancellable) {}
        await gate.releaseOne()
        await busyTask.value
    }

    // MARK: - FL-05 aggregate import ownership

    func testFL05RunningImportCannotOverwriteExplicitExistingTabSelection()
        async throws
    {
        let (state, _) = try await makeVault(files: ["alpha.md", "beta.md"])
        state.openFile("alpha.md", target: .currentTab)
        await state.noteLoadTask?.value
        let alphaTab = try XCTUnwrap(state.workspace.model.activeGroup.activeTabID)
        state.openFile("beta.md", target: .newTab)
        await state.noteLoadTask?.value

        try await assertRunningImportPreservesExplicitNavigation(
            state: state,
            initialPath: "beta.md",
            expectedPath: "alpha.md"
        ) {
            state.selectTab(id: alphaTab)
        }
    }

    func testFL05RunningImportCannotOverwriteExplicitKeyboardPaneFocus()
        async throws
    {
        let (state, _) = try await makeVault(files: ["alpha.md", "beta.md"])
        state.openFile("alpha.md", target: .currentTab)
        await state.noteLoadTask?.value
        state.splitActivePane(axis: .horizontal)
        state.openFile("beta.md", target: .currentTab)
        await state.noteLoadTask?.value

        try await assertRunningImportPreservesExplicitNavigation(
            state: state,
            initialPath: "beta.md",
            expectedPath: "alpha.md"
        ) {
            state.focusPane(.left)
        }
    }

    func testFL05RunningImportCannotOverwriteExplicitCloseSuccessor()
        async throws
    {
        let (state, _) = try await makeVault(files: ["alpha.md", "beta.md"])
        state.openFile("alpha.md", target: .currentTab)
        await state.noteLoadTask?.value
        state.openFile("beta.md", target: .newTab)
        await state.noteLoadTask?.value

        try await assertRunningImportPreservesExplicitNavigation(
            state: state,
            initialPath: "beta.md",
            expectedPath: "alpha.md"
        ) {
            state.requestCloseTab()
        }
    }

    func testFL05RunningImportCannotOverwriteDifferentPathTaskRowIntent()
        async throws
    {
        let (state, _) = try await makeVault(contents: [
            "alpha.md": "# Alpha\n",
            "tasks.md": "# Tasks\n- [ ] Review import selection\n",
        ])
        state.openFile("alpha.md", target: .currentTab)
        await state.noteLoadTask?.value
        state.openTasksReview()
        await state.vaultTasksLoadTask?.value
        let row = try XCTUnwrap(
            state.vaultTasks.first { $0.path == "tasks.md" })

        try await assertRunningImportPreservesExplicitNavigation(
            state: state,
            initialPath: "alpha.md",
            expectedPath: "tasks.md"
        ) {
            state.openTaskRowInEditor(row)
        }
        await state.noteLoadTask?.value
        await state.taskRowActivationTask?.value
    }

    func testFL05RunningImportCannotOverwriteSamePathTaskRowIntent()
        async throws
    {
        let (state, _) = try await makeVault(contents: [
            "tasks.md": "# Tasks\n- [ ] Review import selection\n",
        ])
        state.openFile("tasks.md", target: .currentTab)
        await state.noteLoadTask?.value
        state.openTasksReview()
        await state.vaultTasksLoadTask?.value
        let row = try XCTUnwrap(
            state.vaultTasks.first { $0.path == "tasks.md" })

        try await assertRunningImportPreservesExplicitNavigation(
            state: state,
            initialPath: "tasks.md",
            expectedPath: "tasks.md"
        ) {
            state.openTaskRowInEditor(row)
        }
    }

    func testFL05RunningImportCannotOverwriteSameRootConnectionsIntent()
        async throws
    {
        let (state, _) = try await makeVault(files: ["a.md"])
        state.openFile("a.md", target: .currentTab)
        await state.noteLoadTask?.value
        state.connectionsRootPath = "a.md"

        try await assertRunningImportPreservesExplicitNavigation(
            state: state,
            initialPath: "a.md",
            expectedPath: "a.md"
        ) {
            state.reRootConnections(on: "a.md")
        }

        XCTAssertEqual(
            state.graphSelectedNodeKey,
            GraphNodeKey.make(path: "a.md", label: ""),
            "the explicit graph target must remain on the same-root command")
        XCTAssertEqual(state.workspace.activeLeaf, .connections)
    }

    func testFL05ConnectionsIntentAdvancesExactlyOnceForSameAndDifferentRoots()
        async throws
    {
        let (state, _) = try await makeVault(files: ["a.md", "b.md"])
        state.openFile("a.md", target: .currentTab)
        await state.noteLoadTask?.value
        state.connectionsRootPath = "a.md"

        var revision = state.sidebarSelectionIntentRevision
        state.reRootConnections(on: "a.md")
        XCTAssertEqual(
            state.sidebarSelectionIntentRevision,
            revision &+ 1,
            "same-root reassertion is still an explicit navigation intent")
        XCTAssertEqual(state.selectedFilePath, "a.md")

        revision = state.sidebarSelectionIntentRevision
        state.reRootConnections(on: "b.md")
        XCTAssertEqual(
            state.sidebarSelectionIntentRevision,
            revision &+ 1,
            "different-root intent must record once, not once plus nested openFile")
        XCTAssertEqual(state.selectedFilePath, "b.md")
        XCTAssertEqual(state.connectionsRootPath, "b.md")
    }

    func testFL05ExplicitTabIntentCoversCanvasAndBaseBeforeTypeDispatchExactlyOnce()
        async throws
    {
        let (state, _) = try await makeVault(contents: [
            "note.md": "# Note\n",
            "board.canvas":
                #"{"nodes":[],"edges":[]}"#,
            "Queries/Reading.base":
                "views:\n  - type: table\n    name: Reading\n",
        ])
        state.openFile("note.md", target: .currentTab)
        await state.noteLoadTask?.value

        var revision = state.sidebarSelectionIntentRevision
        state.openCanvasFile("board.canvas", target: .newTab)
        XCTAssertEqual(state.sidebarSelectionIntentRevision, revision &+ 1)
        let canvasTab = try XCTUnwrap(state.workspace.model.activeGroup.activeTabID)

        revision = state.sidebarSelectionIntentRevision
        state.openBaseFile("Queries/Reading.base", target: .newTab)
        XCTAssertEqual(state.sidebarSelectionIntentRevision, revision &+ 1)
        let baseTab = try XCTUnwrap(state.workspace.model.activeGroup.activeTabID)

        revision = state.sidebarSelectionIntentRevision
        state.activateTab(canvasTab)
        XCTAssertEqual(
            state.sidebarSelectionIntentRevision,
            revision,
            "the type-specific activation mechanism remains internal and neutral")

        state.selectTab(id: baseTab)
        XCTAssertEqual(
            state.sidebarSelectionIntentRevision,
            revision &+ 1,
            "the explicit wrapper records once before Base dispatch")

        revision = state.sidebarSelectionIntentRevision
        state.openFile("board.canvas", target: .newTab)
        XCTAssertEqual(
            state.sidebarSelectionIntentRevision,
            revision &+ 1,
            "openFile plus nested Canvas activation must not double-advance")
    }

    func testFL05FileTreeAndInternalNavigationPathsRemainRevisionNeutral()
        async throws
    {
        let (state, _) = try await makeVault(files: ["alpha.md", "beta.md"])
        state.openFile("alpha.md", target: .currentTab)
        await state.noteLoadTask?.value
        let alphaTab = try XCTUnwrap(state.workspace.model.activeGroup.activeTabID)
        state.openFile("beta.md", target: .newTab)
        await state.noteLoadTask?.value

        let revision = state.sidebarSelectionIntentRevision
        state.activateTab(alphaTab)
        XCTAssertEqual(state.sidebarSelectionIntentRevision, revision)

        state.openFile(
            "beta.md",
            target: .currentTab,
            advancesSidebarSelectionRevision: false)
        XCTAssertEqual(
            state.sidebarSelectionIntentRevision,
            revision,
            "FileTree owns its own model revision and must not double-advance AppState")

        let activeTab = try XCTUnwrap(state.workspace.model.activeGroup.activeTabID)
        state.performCloseTab(activeTab)
        XCTAssertEqual(
            state.sidebarSelectionIntentRevision,
            revision,
            "internal close/mutation/layout continuations remain neutral")
        XCTAssertTrue(state.closeVault())
        XCTAssertEqual(
            state.sidebarSelectionIntentRevision,
            revision,
            "vault teardown remains neutral")
    }

    func testFL05CloseIntentAdvancesOnceAcrossDirtyPromptCancelButInactiveCloseIsNeutral()
        async throws
    {
        let (state, _) = try await makeVault(files: ["alpha.md", "beta.md"])
        state.openFile("alpha.md", target: .currentTab)
        await state.noteLoadTask?.value
        let alphaTab = try XCTUnwrap(state.workspace.model.activeGroup.activeTabID)
        state.openFile("beta.md", target: .newTab)
        await state.noteLoadTask?.value
        state.updateEditorText("# Beta\nDirty\n")

        var revision = state.sidebarSelectionIntentRevision
        state.requestCloseTab()
        XCTAssertEqual(state.sidebarSelectionIntentRevision, revision &+ 1)
        XCTAssertNotNil(state.pendingTabClose)
        state.resolveTabCloseCancel()
        XCTAssertEqual(
            state.sidebarSelectionIntentRevision,
            revision &+ 1,
            "rollback/cancel must not add a second navigation edge")

        revision = state.sidebarSelectionIntentRevision
        state.requestCloseTab(alphaTab)
        XCTAssertEqual(
            state.sidebarSelectionIntentRevision,
            revision,
            "closing an inactive clean tab does not change or reassert selection")
    }

    func testFL05InactivePanePointerAndModeToggleUseExplicitTabBoundaryByInspection()
        throws
    {
        let sourceRoot = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .appendingPathComponent("Sources/SlateMac")
        let workspaceView = try String(
            contentsOf: sourceRoot.appendingPathComponent("Workspace/WorkspaceView.swift"),
            encoding: .utf8)
        let tabBar = try String(
            contentsOf: sourceRoot.appendingPathComponent("Workspace/TabBarView.swift"),
            encoding: .utf8)

        XCTAssertFalse(workspaceView.contains("appState.activateTab(tab.id)"))
        XCTAssertGreaterThanOrEqual(
            workspaceView.components(separatedBy: "appState.selectTab(id: tab.id)").count - 1,
            2,
            "both loaded and placeholder inactive-pane pointer paths use the explicit seam")
        XCTAssertTrue(tabBar.contains("appState.selectTab(id: activeTabID)"))
        XCTAssertFalse(tabBar.contains("appState.activateTab(activeTabID)"))
    }

    func testFL05BeginImportBatchClaimsStructuralOwnershipBeforeProviderLoadAndRejectsSecondBatch()
        async throws
    {
        let (state, _) = try await makeVault(files: ["destination/keep.md"])
        let session = try XCTUnwrap(state.currentSession)
        let provider = NSItemProvider()
        provider.registerDataRepresentation(
            forTypeIdentifier: FileTreeSidebar.fileURLUTType,
            visibility: .all
        ) { _ in
            XCTFail("beginImportBatch must not start provider loading synchronously")
            return nil
        }

        let owner = try XCTUnwrap(
            state.beginImportBatch(
                providers: [provider],
                destinationFolder: "destination"))

        XCTAssertTrue(state.isMutatingStructure)
        XCTAssertTrue(owner.session === session)
        XCTAssertEqual(owner.destinationFolder, "destination")
        XCTAssertEqual(owner.supportedProviderCount, 1)
        XCTAssertTrue(state.currentImportBatchOwner === owner)

        XCTAssertNil(
            state.beginImportBatch(
                providers: [provider],
                destinationFolder: "other"),
            "a second batch must lose admission during the old provider-load gap")
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            AppState.structuralMutationBusyReason)

        state.cancelImportBatch(owner)
        XCTAssertFalse(state.isMutatingStructure)
        XCTAssertNil(state.currentImportBatchOwner)
    }

    func testFL05ProviderLimitRejectsBeforeOwnershipOrAnyProviderLoad()
        async throws
    {
        let (state, _) = try await makeVault(files: [])
        let counter = ProviderLoadCounter()
        let limit = SidebarImportProviderIntake.maximumProviderCount
        let providers = (0...limit).map { index in
            let provider = NSItemProvider()
            provider.registerDataRepresentation(
                forTypeIdentifier: FileTreeSidebar.fileURLUTType,
                visibility: .all
            ) { completion in
                counter.record(index)
                completion(
                    FileManager.default.temporaryDirectory
                        .appendingPathComponent("item-\(index).md")
                        .dataRepresentation,
                    nil)
                return nil
            }
            return provider
        }

        let accepted = try XCTUnwrap(state.beginImportBatch(
            providers: Array(providers.prefix(limit)),
            destinationFolder: ""))
        state.cancelImportBatch(accepted)
        XCTAssertNil(state.beginImportBatch(
            providers: providers,
            destinationFolder: ""))
        XCTAssertNil(state.currentImportBatchOwner)
        XCTAssertFalse(state.isMutatingStructure)
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            "Choose \(limit) or fewer files and folders to import at once.")
        XCTAssertEqual(
            state.sidebarActionBackgroundFailure,
            AppState.importProviderLimitReason)
        for index in 0...limit {
            XCTAssertEqual(counter.count(for: index), 0)
        }
    }

    func testFL05AcceptedCancellationPublishesCancellingPhaseOnce() async throws {
        let (state, _) = try await makeVault(files: [])
        let owner = try XCTUnwrap(state.beginImportBatch(
            providers: [fileURLProvider(tempDir.appendingPathComponent("pending.md"))],
            destinationFolder: ""))

        XCTAssertTrue(state.canRequestImportBatchCancellation)
        XCTAssertEqual(owner.progress.phase, .importing)
        XCTAssertTrue(state.requestImportBatchCancellation())
        XCTAssertEqual(owner.progress.phase, .cancelling)
        XCTAssertFalse(owner.progress.canRequestCancellation)
        XCTAssertFalse(state.canRequestImportBatchCancellation)
        XCTAssertFalse(state.requestImportBatchCancellation())
        XCTAssertEqual(owner.progress.phase, .cancelling)

        state.cancelImportBatch(owner)
    }

    func testFL05CancelImportCommandContractProjectsAndPerformsLiveLifecycle()
        async throws
    {
        let (state, _) = try await makeVault(files: [])

        XCTAssertEqual(
            CancelImportCommandContract.id, SlateCommandID.cancelImport)
        XCTAssertEqual(CancelImportCommandContract.label, "Cancel Import")
        XCTAssertEqual(CancelImportCommandContract.section, .sidebar)
        XCTAssertEqual(CancelImportCommandContract.hotkeyHint, "⌘.")
        XCTAssertEqual(
            CancelImportCommandContract.availableHint,
            SidebarImportProgressStrip.cancelAccessibilityHint)
        XCTAssertEqual(
            CancelImportCommandContract.keyboardShortcut.key.character, ".")
        XCTAssertEqual(
            CancelImportCommandContract.keyboardShortcut.modifiers, [.command])

        func assertProjection(
            disabledReason: String?,
            hint: String,
            file: StaticString = #filePath,
            line: UInt = #line
        ) {
            let projection = CancelImportCommandContract.projection(for: state)
            XCTAssertEqual(
                projection.disabledReason, disabledReason,
                file: file, line: line)
            XCTAssertEqual(
                projection.isEnabled, disabledReason == nil,
                file: file, line: line)
            XCTAssertEqual(projection.hint, hint, file: file, line: line)
        }

        assertProjection(
            disabledReason: SidebarImportProgressStrip.noImportInProgressHint,
            hint: SidebarImportProgressStrip.noImportInProgressHint)
        XCTAssertThrowsError(
            try CancelImportCommandContract.perform(on: state)
        ) { error in
            XCTAssertEqual(
                error as? CommandError,
                .ActionFailed(
                    message: SidebarImportProgressStrip.noImportInProgressHint))
        }

        let owner = try XCTUnwrap(state.beginImportBatch(
            providers: [
                fileURLProvider(tempDir.appendingPathComponent("pending.md"))
            ],
            destinationFolder: ""))
        assertProjection(
            disabledReason: nil,
            hint: SidebarImportProgressStrip.cancelAccessibilityHint)

        try CancelImportCommandContract.perform(on: state)
        XCTAssertTrue(owner.isCancellationRequested)
        XCTAssertEqual(owner.progress.phase, .cancelling)

        let cancellingReason = SidebarImportProgressStrip.cancellationHint(
            phase: .cancelling, available: false)
        assertProjection(
            disabledReason: cancellingReason,
            hint: cancellingReason)
        XCTAssertThrowsError(
            try CancelImportCommandContract.perform(on: state)
        ) { error in
            XCTAssertEqual(
                error as? CommandError,
                .ActionFailed(message: cancellingReason))
        }

        state.cancelImportBatch(owner)
        assertProjection(
            disabledReason: SidebarImportProgressStrip.noImportInProgressHint,
            hint: SidebarImportProgressStrip.noImportInProgressHint)
    }

    func testFL05RegisteredCancelCommandUsesLiveLifecycleAvailability()
        async throws
    {
        let (state, _) = try await makeVault(files: [])
        let command = try XCTUnwrap(state.commandRegistry.list().first {
            $0.id == SlateCommandID.cancelImport
        })
        var pickerInvocationCount = 0
        state.importSourcePicker = {
            pickerInvocationCount += 1
            return nil
        }
        func paletteReason() -> String? {
            CommandPaletteView.disabledReason(
                for: command,
                structuralMutationDisabledReason:
                    state.structuralMutationDisabledReason,
                importCancellationDisabledReason:
                    state.importCancellationDisabledReason)
        }
        func assertRegistryRejects(_ expectedReason: String) {
            XCTAssertThrowsError(
                try state.commandRegistry.invokeById(
                    id: SlateCommandID.cancelImport)
            ) { error in
                XCTAssertEqual(
                    error as? CommandError,
                    .ActionFailed(message: expectedReason))
            }
        }

        XCTAssertEqual(
            state.importCancellationDisabledReason,
            SidebarImportProgressStrip.noImportInProgressHint)
        XCTAssertEqual(
            paletteReason(),
            SidebarImportProgressStrip.noImportInProgressHint)
        assertRegistryRejects(SidebarImportProgressStrip.noImportInProgressHint)

        let owner = try XCTUnwrap(state.beginImportBatch(
            providers: [fileURLProvider(tempDir.appendingPathComponent("pending.md"))],
            destinationFolder: ""))
        XCTAssertNil(state.importCancellationDisabledReason)
        XCTAssertNil(paletteReason())

        owner.installActivePhaseCancellation(
            phase: .moving, cancellableByUser: false) {}
        let movingReason = SidebarImportProgressStrip.cancellationHint(
            phase: .moving, available: false)
        XCTAssertEqual(state.importCancellationDisabledReason, movingReason)
        XCTAssertEqual(paletteReason(), movingReason)
        assertRegistryRejects(movingReason)
        XCTAssertFalse(owner.isCancellationRequested)

        owner.installActivePhaseCancellation(
            phase: .finishing, cancellableByUser: false) {}
        let finishingReason = SidebarImportProgressStrip.cancellationHint(
            phase: .finishing, available: false)
        XCTAssertEqual(state.importCancellationDisabledReason, finishingReason)
        XCTAssertEqual(paletteReason(), finishingReason)
        assertRegistryRejects(finishingReason)
        XCTAssertFalse(owner.isCancellationRequested)

        owner.installActivePhaseCancellation(
            phase: .importing, cancellableByUser: true) {}
        XCTAssertNil(state.importCancellationDisabledReason)
        XCTAssertNil(paletteReason())
        try state.commandRegistry.invokeById(id: SlateCommandID.cancelImport)

        XCTAssertTrue(owner.isCancellationRequested)
        XCTAssertEqual(owner.progress.phase, .cancelling)
        XCTAssertEqual(pickerInvocationCount, 0)
        let cancellingReason = SidebarImportProgressStrip.cancellationHint(
            phase: .cancelling, available: false)
        XCTAssertEqual(state.importCancellationDisabledReason, cancellingReason)
        XCTAssertEqual(paletteReason(), cancellingReason)
        assertRegistryRejects(cancellingReason)

        state.cancelImportBatch(owner)
        XCTAssertEqual(
            state.importCancellationDisabledReason,
            SidebarImportProgressStrip.noImportInProgressHint)
        XCTAssertEqual(pickerInvocationCount, 0)
    }

    func testFL05StaleOwnerCancellationCannotClobberReplacementAvailability()
        async throws
    {
        let (state, _) = try await makeVault(files: [])
        let stale = try XCTUnwrap(state.beginImportBatch(
            providers: [fileURLProvider(tempDir.appendingPathComponent("stale.md"))],
            destinationFolder: ""))
        state.finishImportBatch(stale)

        let replacement = try XCTUnwrap(state.beginImportBatch(
            providers: [fileURLProvider(tempDir.appendingPathComponent("replacement.md"))],
            destinationFolder: ""))
        XCTAssertTrue(state.currentImportBatchOwner === replacement)
        XCTAssertTrue(state.canRequestImportBatchCancellation)

        state.cancelImportBatch(stale)

        XCTAssertFalse(stale.isCancellationRequested)
        XCTAssertTrue(state.currentImportBatchOwner === replacement)
        XCTAssertTrue(state.canRequestImportBatchCancellation)
        XCTAssertTrue(replacement.canRequestUserCancellation)
        state.cancelImportBatch(replacement)
    }

    func testFL05StaleFinishingOwnerCannotClearReplacementAvailability()
        async throws
    {
        let external = tempDir.appendingPathComponent("owner-a.md")
        try Data("owner a".utf8).write(to: external)
        let (state, _) = try await makeVault(files: [])
        let refreshGate = SuspensionGate()
        state.importInventoryRefreshRunner = { _, _ in
            await refreshGate.enter()
        }
        let stale = try XCTUnwrap(state.beginImportBatch(
            providers: [fileURLProvider(external)],
            destinationFolder: ""))
        let staleTask = state.startImportBatch(stale)
        await refreshGate.waitForEntrants(1)
        XCTAssertEqual(stale.progress.phase, .finishing)

        XCTAssertTrue(state.closeVault())
        let replacementVault = tempDir.appendingPathComponent("replacement-vault")
        try FileManager.default.createDirectory(
            at: replacementVault, withIntermediateDirectories: true)
        state.openVault(at: replacementVault)
        await state.scanTask?.value
        let replacement = try XCTUnwrap(state.beginImportBatch(
            providers: [fileURLProvider(tempDir.appendingPathComponent("owner-b.md"))],
            destinationFolder: ""))
        XCTAssertTrue(state.canRequestImportBatchCancellation)

        await refreshGate.releaseOne()
        await staleTask.value

        XCTAssertTrue(state.currentImportBatchOwner === replacement)
        XCTAssertTrue(state.canRequestImportBatchCancellation)
        XCTAssertTrue(replacement.canRequestUserCancellation)
        state.cancelImportBatch(replacement)
    }

    func testFL05PreparationFailureAdvancesProgressBeforeSecondRootFinishes()
        async throws
    {
        let hidden = tempDir.appendingPathComponent(".hidden.md")
        let slow = tempDir.appendingPathComponent("slow.md")
        try Data("hidden".utf8).write(to: hidden)
        try Data("slow".utf8).write(to: slow)
        let (state, _) = try await makeVault(files: [])
        let gate = SecondPreparationGate()
        state.importSourceWalkerHooks = SidebarImportSourceWalkerHooks(
            didReachBoundary: { gate.reach($0) })
        let owner = try XCTUnwrap(state.beginImportBatch(
            providers: [fileURLProvider(hidden), fileURLProvider(slow)],
            destinationFolder: ""))

        let task = state.startImportBatch(owner)
        await gate.waitForSecondRoot()
        await Task.yield()

        XCTAssertEqual(owner.completedProviderCount, 1)
        XCTAssertEqual(owner.progress.accessibilityValue, "1 of 2")
        XCTAssertTrue(state.currentImportBatchOwner === owner)

        gate.release()
        await task.value
        XCTAssertEqual(owner.completedProviderCount, 2)
    }

    func testFL05FailedDirectVaultSwitchCancelsImportBeforeClearingSessionAndReleasesGate()
        async throws
    {
        let (state, _) = try await makeVault(files: [])
        let provider = NSItemProvider()
        provider.registerDataRepresentation(
            forTypeIdentifier: FileTreeSidebar.fileURLUTType,
            visibility: .all
        ) { _ in nil }
        let owner = try XCTUnwrap(
            state.beginImportBatch(
                providers: [provider],
                destinationFolder: ""))

        state.openVault(at: tempDir.appendingPathComponent("missing-vault"))

        XCTAssertNil(state.currentSession)
        XCTAssertTrue(
            owner.isCancellationRequested,
            "provider/engine cancellation must precede every session-clearing catch path")
        XCTAssertNil(state.currentImportBatchOwner)
        XCTAssertFalse(
            state.isMutatingStructure,
            "a failed direct switch must not leave the structural gate wedged")
    }

    func testFL05CloseVaultCancelsImportBeforeSessionTeardownAndReleasesGate()
        async throws
    {
        let (state, _) = try await makeVault(files: [])
        let provider = NSItemProvider()
        provider.registerDataRepresentation(
            forTypeIdentifier: FileTreeSidebar.fileURLUTType,
            visibility: .all
        ) { _ in nil }
        let owner = try XCTUnwrap(
            state.beginImportBatch(
                providers: [provider],
                destinationFolder: ""))

        XCTAssertTrue(state.closeVault())

        XCTAssertTrue(owner.isCancellationRequested)
        XCTAssertNil(state.currentSession)
        XCTAssertNil(state.currentImportBatchOwner)
        XCTAssertFalse(state.isMutatingStructure)
    }

    func testFL05AggregateExternalBatchLoadsEachProviderOnceAndPublishesOneLanding()
        async throws
    {
        let firstURL = tempDir.appendingPathComponent("first.txt")
        let secondURL = tempDir.appendingPathComponent("second.bin")
        let firstBytes = Data("one".utf8)
        let secondBytes = Data([0x00, 0xFF, 0x7F])
        try firstBytes.write(to: firstURL)
        try secondBytes.write(to: secondURL)
        let (state, vault) = try await makeVault(files: [])
        let counter = ProviderLoadCounter()

        func provider(index: Int, url: URL) -> NSItemProvider {
            let provider = NSItemProvider()
            provider.registerDataRepresentation(
                forTypeIdentifier: FileTreeSidebar.fileURLUTType,
                visibility: .all
            ) { completion in
                counter.record(index)
                completion(url.dataRepresentation, nil)
                return nil
            }
            return provider
        }

        let owner = try XCTUnwrap(
            state.beginImportBatch(
                providers: [
                    provider(index: 0, url: firstURL),
                    provider(index: 1, url: secondURL),
                ],
                destinationFolder: "",
                selectionRevision: 41))
        XCTAssertEqual(owner.vaultURL, vault)
        XCTAssertEqual(owner.selectionRevision, 41)

        let task = state.startImportBatch(owner)
        await task.value

        XCTAssertEqual(counter.count(for: 0), 1)
        XCTAssertEqual(counter.count(for: 1), 1)
        XCTAssertNil(state.lastError)
        XCTAssertEqual(
            try Data(contentsOf: vault.appendingPathComponent("first.txt")),
            firstBytes)
        XCTAssertEqual(
            try Data(contentsOf: vault.appendingPathComponent("second.bin")),
            secondBytes)
        XCTAssertEqual(owner.completedProviderCount, 2)
        XCTAssertNil(state.currentImportBatchOwner)
        XCTAssertNil(state.importBatchProgress)
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            "Copied 2 files to the vault root.")
        guard case let .importBatch(created, standing, touched)? = state.treeMutation?.kind else {
            return XCTFail("one aggregate import mutation must own the final landing")
        }
        XCTAssertEqual(created.map(\.providerIndex), [0, 1])
        XCTAssertTrue(standing.isEmpty)
        XCTAssertTrue(touched.isEmpty)
    }

    func testFL05PureNativeImportRunsOneBatchRecordsOneInverseAndRefreshesOnce()
        async throws
    {
        let (state, vault) = try await makeVault(files: ["a.md", "dest/keep.md"])
        let planned = [batchItem("a.md")]
        let standing = [
            BatchPathChange(
                oldPath: "a.md", newPath: "dest/a.md", isDirectory: false),
        ]
        let probe = BatchMoveProbe(report: batchMoveReport(
            state: .succeeded,
            planned: planned,
            opID: 501,
            standing: standing))
        let refresh = ImportRefreshProbe()
        state.batchMoveRunner = { _, request in await probe.run(request) }
        state.importInventoryRefreshRunner = { _, _ in
            await refresh.recordListRefresh()
        }
        state.importScanRefreshRunner = { _, _ in
            await refresh.recordScan()
        }

        let provider = NSItemProvider()
        provider.registerDataRepresentation(
            forTypeIdentifier: FileTreeSidebar.fileURLUTType,
            visibility: .all
        ) { completion in
            completion(vault.appendingPathComponent("a.md").dataRepresentation, nil)
            return nil
        }
        let owner = try XCTUnwrap(state.beginImportBatch(
            providers: [provider],
            destinationFolder: "dest",
            selectionRevision: 7))

        await state.startImportBatch(owner).value

        let callCount = await probe.callCount()
        let request = await probe.lastRequest()
        let refreshCounts = await refresh.counts()
        XCTAssertEqual(callCount, 1)
        XCTAssertEqual(request?.items, planned)
        XCTAssertEqual(refreshCounts.list, 1)
        XCTAssertEqual(refreshCounts.scan, 0)
        XCTAssertEqual(
            state.structuralUndoStack,
            [.batchMove(opId: 501, entries: standing)])
        guard case let .importBatch(materialized, publishedStanding, touched)? =
            state.treeMutation?.kind
        else {
            return XCTFail("pure move must use the aggregate import landing")
        }
        XCTAssertEqual(materialized.map(\.providerIndex), [0])
        XCTAssertEqual(materialized.map(\.path), ["dest/a.md"])
        XCTAssertEqual(publishedStanding, standing)
        XCTAssertTrue(touched.isEmpty)
        XCTAssertEqual(state.lastMutationAnnouncement, "Moved 1 item to dest.")
    }

    func testFL05MixedImportCopiesFirstReservesMoveNameAndLeavesOnlyNewInverse()
        async throws
    {
        let external = tempDir.appendingPathComponent("a.md")
        try Data("new".utf8).write(to: external)
        let (state, vault) = try await makeVault(files: [
            "a.md", "seed.md", "archive/keep.md", "dest/keep.md",
        ])
        _ = try await seedBatchMoveHistory(
            in: state, path: "seed.md", destination: "archive", opID: 400)
        let standing = [BatchPathChange(
            oldPath: "a.md", newPath: "dest/a.md", isDirectory: false)]
        let probe = BatchMoveProbe(report: batchMoveReport(
            state: .succeeded,
            planned: [batchItem("a.md")],
            opID: 401,
            standing: standing))
        let refresh = ImportRefreshProbe()
        state.batchMoveRunner = { _, request in await probe.run(request) }
        state.importInventoryRefreshRunner = { _, _ in
            await refresh.recordListRefresh()
        }
        state.importScanRefreshRunner = { _, _ in await refresh.recordScan() }

        let owner = try XCTUnwrap(state.beginImportBatch(
            providers: [
                fileURLProvider(external),
                fileURLProvider(vault.appendingPathComponent("a.md")),
            ],
            destinationFolder: "dest",
            selectionRevision: 12))
        await state.startImportBatch(owner).value

        XCTAssertEqual(
            try Data(contentsOf: vault.appendingPathComponent("dest/a 2.md")),
            Data("new".utf8),
            "external creates finish first while the move basename stays reserved")
        let nativeRequest = await probe.lastRequest()
        let refreshCounts = await refresh.counts()
        XCTAssertEqual(nativeRequest?.items, [batchItem("a.md")])
        XCTAssertEqual(refreshCounts.list, 1)
        XCTAssertEqual(refreshCounts.scan, 0)
        XCTAssertEqual(
            state.structuralUndoStack,
            [.batchMove(opId: 401, entries: standing)],
            "the external barrier clears old history before the native inverse lands")
        guard case let .importBatch(materialized, publishedStanding, _)? =
            state.treeMutation?.kind
        else { return XCTFail("mixed import needs one aggregate landing") }
        XCTAssertEqual(materialized.map(\.providerIndex), [0, 1])
        XCTAssertEqual(materialized.map(\.path), ["dest/a 2.md", "dest/a.md"])
        XCTAssertEqual(publishedStanding, standing)
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            "Copied 1 file and moved 1 item to dest.")
    }

    func testFL05NativeNonSuccessHistoryMatrixPreservesOrClearsExactly()
        async throws
    {
        struct Case {
            let state: BatchMoveState
            let standing: [BatchPathChange]
            let rolledBack: [BatchPathChange]
            let preservesOldHistory: Bool
        }
        let change = BatchPathChange(
            oldPath: "a.md", newPath: "dest/a.md", isDirectory: false)
        let cases = [
            Case(state: .rejected, standing: [], rolledBack: [], preservesOldHistory: true),
            Case(state: .noOp, standing: [], rolledBack: [], preservesOldHistory: true),
            Case(state: .rolledBack, standing: [], rolledBack: [change], preservesOldHistory: true),
            Case(
                state: .rollbackIncomplete,
                standing: [change],
                rolledBack: [],
                preservesOldHistory: false),
        ]

        for testCase in cases {
            let (state, vault) = try await makeVault(files: [
                "a.md", "seed.md", "archive/keep.md", "dest/keep.md",
            ])
            let oldInverse = try await seedBatchMoveHistory(
                in: state, path: "seed.md", destination: "archive", opID: 600)
            let report = batchMoveReport(
                state: testCase.state,
                planned: [batchItem("a.md")],
                standing: testCase.standing,
                rolledBack: testCase.rolledBack)
            state.batchMoveRunner = { _, _ in report }
            state.importInventoryRefreshRunner = { _, _ in }
            state.importScanRefreshRunner = { _, _ in }
            let mutationTokenBeforeImport = state.treeMutation?.token
            let owner = try XCTUnwrap(state.beginImportBatch(
                providers: [fileURLProvider(vault.appendingPathComponent("a.md"))],
                destinationFolder: "dest"))
            await state.startImportBatch(owner).value

            if testCase.preservesOldHistory {
                XCTAssertEqual(
                    state.structuralUndoStack, [oldInverse],
                    "\(testCase.state) must preserve an unrelated inverse")
            } else {
                XCTAssertTrue(
                    state.structuralUndoStack.isEmpty,
                    "\(testCase.state) leaves unsafe standing state and must clear history")
            }
            if testCase.state == .rejected || testCase.state == .noOp {
                XCTAssertEqual(
                    state.treeMutation?.token,
                    mutationTokenBeforeImport,
                    "clean no-effect reports must not publish a fake root mutation")
            }
        }
    }

    func testFL05CleanExternalFailureStillPermitsNativeAndPreservesOldHistory()
        async throws
    {
        let hidden = tempDir.appendingPathComponent(".hidden.md")
        try Data("hidden".utf8).write(to: hidden)
        let (state, vault) = try await makeVault(files: [
            "a.md", "seed.md", "archive/keep.md", "dest/keep.md",
        ])
        let oldInverse = try await seedBatchMoveHistory(
            in: state, path: "seed.md", destination: "archive", opID: 700)
        let standing = [BatchPathChange(
            oldPath: "a.md", newPath: "dest/a.md", isDirectory: false)]
        let report = batchMoveReport(
            state: .succeeded,
            planned: [batchItem("a.md")],
            opID: 701,
            standing: standing)
        state.batchMoveRunner = { _, _ in report }
        state.importInventoryRefreshRunner = { _, _ in }
        state.importScanRefreshRunner = { _, _ in }

        let owner = try XCTUnwrap(state.beginImportBatch(
            providers: [
                fileURLProvider(hidden),
                fileURLProvider(vault.appendingPathComponent("a.md")),
            ],
            destinationFolder: "dest"))
        await state.startImportBatch(owner).value

        XCTAssertEqual(
            state.structuralUndoStack,
            [oldInverse, .batchMove(opId: 701, entries: standing)],
            "a clean walker rejection is not a physical history barrier")
        guard case .result(let result)? = state.activeBatchAlertPresentation,
            case .importBatch(let failureReport) = result.payload
        else { return XCTFail("the walker rejection needs one mounted import alert") }
        XCTAssertEqual(failureReport.terminalFailureCount, 1)
        XCTAssertEqual(failureReport.totalDetailCount, 1)
        XCTAssertEqual(failureReport.details.first?.scope, .provider(0))
    }

    func testFL05NativeInfrastructureFailureClearsHistoryAndRunsOneScan()
        async throws
    {
        struct ExpectedFailure: LocalizedError {
            var errorDescription: String? { "native unavailable" }
        }
        let (state, vault) = try await makeVault(files: [
            "a.md", "seed.md", "archive/keep.md", "dest/keep.md",
        ])
        _ = try await seedBatchMoveHistory(
            in: state, path: "seed.md", destination: "archive", opID: 800)
        let refresh = ImportRefreshProbe()
        state.batchMoveRunner = { _, _ in throw ExpectedFailure() }
        state.importInventoryRefreshRunner = { _, _ in
            await refresh.recordListRefresh()
        }
        state.importScanRefreshRunner = { _, _ in await refresh.recordScan() }

        let owner = try XCTUnwrap(state.beginImportBatch(
            providers: [fileURLProvider(vault.appendingPathComponent("a.md"))],
            destinationFolder: "dest"))
        await state.startImportBatch(owner).value

        let counts = await refresh.counts()
        XCTAssertTrue(state.structuralUndoStack.isEmpty)
        XCTAssertEqual(counts.list, 0)
        XCTAssertEqual(counts.scan, 1)
        XCTAssertEqual(state.treeMutation?.requiresRescan, true)
    }

    func testFL05PostScanMaterializationRequiresExpectedFileOrDirectoryKind()
        async throws
    {
        let (state, _) = try await makeVault(files: ["file.md"])
        let session = try XCTUnwrap(state.currentSession)
        try session.createFolderExclusive(path: "empty")
        let candidates = [
            SidebarImportMaterializedResult(
                providerIndex: 0, path: "file.md", isDirectory: true),
            SidebarImportMaterializedResult(
                providerIndex: 1, path: "empty", isDirectory: false),
            SidebarImportMaterializedResult(
                providerIndex: 2, path: "file.md", isDirectory: false),
            SidebarImportMaterializedResult(
                providerIndex: 3, path: "empty", isDirectory: true),
        ]

        let materialized = try await AppState.resolveImportMaterialization(
            candidates, session: session)

        XCTAssertEqual(materialized.map(\.providerIndex), [2, 3])
        XCTAssertEqual(materialized.map(\.path), ["file.md", "empty"])
    }

    func testFL05FailedAuthoritativeScanNeverPromotesUnknownCopyCandidate()
        async throws
    {
        struct UnknownCreate: LocalizedError {
            var errorDescription: String? { "create outcome unknown" }
        }
        struct ScanFailure: LocalizedError {
            var errorDescription: String? { "scan unavailable" }
        }
        let external = tempDir.appendingPathComponent("uncertain.bin")
        try Data([0x01]).write(to: external)
        let (state, _) = try await makeVault(files: [])
        let refresh = ImportRefreshProbe()
        state.importDestinationCreators = { _ in
            SidebarImportDestinationCreators(
                createFile: { _, _ in throw UnknownCreate() },
                createDirectory: { _ in throw UnknownCreate() })
        }
        state.importScanRefreshRunner = { _, _ in
            await refresh.recordScan()
            throw ScanFailure()
        }
        state.importInventoryRefreshRunner = { _, _ in
            await refresh.recordListRefresh()
        }

        let owner = try XCTUnwrap(state.beginImportBatch(
            providers: [fileURLProvider(external)],
            destinationFolder: ""))
        await state.startImportBatch(owner).value

        let counts = await refresh.counts()
        XCTAssertEqual(counts.scan, 1)
        XCTAssertEqual(counts.list, 0)
        guard case let .importBatch(materialized, standing, touched)? =
            state.treeMutation?.kind
        else { return XCTFail("uncertainty still needs one root reconcile") }
        XCTAssertTrue(materialized.isEmpty)
        XCTAssertTrue(standing.isEmpty)
        XCTAssertTrue(touched.isEmpty)
        XCTAssertEqual(state.treeMutation?.requiresRescan, true)
    }

    func testFL05CancelDuringUnknownCreateStillRunsMandatoryRealScan()
        async throws
    {
        struct UnknownCreate: LocalizedError {
            var errorDescription: String? { "create outcome unknown" }
        }
        let external = tempDir.appendingPathComponent("uncertain.md")
        try Data("source".utf8).write(to: external)
        let (state, vault) = try await makeVault(files: [])
        let owner = try XCTUnwrap(state.beginImportBatch(
            providers: [fileURLProvider(external)],
            destinationFolder: ""))
        let cancellationSignal = owner.cancellationSignal
        state.importDestinationCreators = { _ in
            SidebarImportDestinationCreators(
                createFile: { _, _ in
                    try Data("marker".utf8).write(
                        to: vault.appendingPathComponent("scan-marker.md"))
                    cancellationSignal.requestCancellation()
                    throw UnknownCreate()
                },
                createDirectory: { _ in throw UnknownCreate() })
        }

        await state.startImportBatch(owner).value

        XCTAssertTrue(
            state.files.contains(where: { $0.path == "scan-marker.md" }),
            "the uncancelled aggregate owner must finish one real authoritative scan")
        guard case let .importBatch(materialized, _, _)? = state.treeMutation?.kind else {
            return XCTFail("unknown create cancellation needs one root reconcile")
        }
        XCTAssertFalse(
            materialized.contains(where: { $0.path == "uncertain.md" }),
            "a stale pre-scan candidate must never be promoted")
        XCTAssertTrue(owner.cancellationSignal.isCancelled)
    }

    func testFL05SuccessfulNativeRewriteFailureIsWarningNotUnimported()
        async throws
    {
        let (state, vault) = try await makeVault(files: ["a.md", "dest/keep.md"])
        let standing = [BatchPathChange(
            oldPath: "a.md", newPath: "dest/a.md", isDirectory: false)]
        let nativeReport = batchMoveReport(
            state: .succeeded,
            planned: [batchItem("a.md")],
            opID: 910,
            standing: standing,
            rewriteFailures: [RewriteFailure(
                path: "dest/a.md",
                kind: RewriteFailureKind(
                    kind: "write_conflict", detail: "changed on disk"))])
        state.batchMoveRunner = { _, _ in nativeReport }
        state.importInventoryRefreshRunner = { _, _ in }

        let owner = try XCTUnwrap(state.beginImportBatch(
            providers: [fileURLProvider(vault.appendingPathComponent("a.md"))],
            destinationFolder: "dest"))
        await state.startImportBatch(owner).value

        guard case .result(let result)? = state.activeBatchAlertPresentation,
            case .importBatch(let report) = result.payload
        else { return XCTFail("the link-update warning needs one bounded alert") }
        XCTAssertEqual(report.terminalFailureCount, 0)
        XCTAssertEqual(report.warningCount, 1)
        XCTAssertEqual(report.details.first?.scope, .provider(0))
        let attention = AppState.BatchStructuralCopy.attention(for: result)
        XCTAssertEqual(attention.title, "Import Completed with Warnings")
        XCTAssertFalse(attention.message.contains("could not be imported"))
        XCTAssertFalse(state.lastMutationAnnouncement?.contains("not imported") == true)
        XCTAssertTrue(
            state.lastMutationAnnouncement?.contains("1 import warning needs attention") == true)
    }

    func testFL05NativeUncertaintyAndIncompleteRollbackAreBatchWarnings()
        async throws
    {
        do {
            let (state, vault) = try await makeVault(files: ["a.md", "dest/keep.md"])
            let nativeReport = batchMoveReport(
                state: .noOp,
                planned: [batchItem("a.md")],
                requiresRescan: true)
            state.batchMoveRunner = { _, _ in nativeReport }
            state.importScanRefreshRunner = { _, _ in }
            let owner = try XCTUnwrap(state.beginImportBatch(
                providers: [fileURLProvider(vault.appendingPathComponent("a.md"))],
                destinationFolder: "dest"))
            await state.startImportBatch(owner).value
            guard case .result(let result)? = state.activeBatchAlertPresentation,
                case .importBatch(let report) = result.payload
            else { return XCTFail("native rescan uncertainty must surface") }
            XCTAssertEqual(report.terminalFailureCount, 0)
            XCTAssertEqual(report.warningCount, 1)
            XCTAssertEqual(
                report.details.first?.scope,
                .batch(stage: "Move reconciliation"))
        }

        do {
            let (state, vault) = try await makeVault(files: ["a.md", "dest/keep.md"])
            let standing = [BatchPathChange(
                oldPath: "a.md", newPath: "dest/a.md", isDirectory: false)]
            let nativeReport = batchMoveReport(
                state: .rollbackIncomplete,
                planned: [batchItem("a.md")],
                standing: standing)
            state.batchMoveRunner = { _, _ in nativeReport }
            state.importInventoryRefreshRunner = { _, _ in }
            let owner = try XCTUnwrap(state.beginImportBatch(
                providers: [fileURLProvider(vault.appendingPathComponent("a.md"))],
                destinationFolder: "dest"))
            await state.startImportBatch(owner).value
            guard case .result(let result)? = state.activeBatchAlertPresentation,
                case .importBatch(let report) = result.payload
            else { return XCTFail("incomplete rollback must surface") }
            XCTAssertEqual(report.terminalFailureCount, 0)
            XCTAssertEqual(report.warningCount, 1)
            XCTAssertEqual(
                report.details.first?.scope,
                .batch(stage: "Move reconciliation"))
        }
    }

    func testFL05DuplicateNativeProviderDiagnosticUsesSecondOccurrence()
        async throws
    {
        let (state, vault) = try await makeVault(files: ["a.md", "dest/keep.md"])
        let standing = [BatchPathChange(
            oldPath: "a.md", newPath: "dest/a.md", isDirectory: false)]
        let nativeReport = batchMoveReport(
            state: .succeeded,
            planned: [batchItem("a.md")],
            opID: 920,
            standing: standing,
            skipped: [BatchSkippedItem(
                item: batchItem("a.md"),
                reason: .duplicate,
                detail: "the same path was selected twice")])
        state.batchMoveRunner = { _, _ in nativeReport }
        state.importInventoryRefreshRunner = { _, _ in }
        let url = vault.appendingPathComponent("a.md")
        let owner = try XCTUnwrap(state.beginImportBatch(
            providers: [fileURLProvider(url), fileURLProvider(url)],
            destinationFolder: "dest"))
        await state.startImportBatch(owner).value

        guard case .result(let result)? = state.activeBatchAlertPresentation,
            case .importBatch(let report) = result.payload
        else { return XCTFail("duplicate provider skip must surface") }
        XCTAssertEqual(report.details.first?.scope, .provider(1))
        XCTAssertEqual(owner.completedProviderCount, 2)
    }

    func testFL05ListRefreshFallbackScanPreservesPureAndMixedNativeInverse()
        async throws
    {
        struct ListFailure: LocalizedError {
            var errorDescription: String? { "list refresh unavailable" }
        }

        do {
            let (state, vault) = try await makeVault(files: ["a.md", "dest/keep.md"])
            let standing = [BatchPathChange(
                oldPath: "a.md", newPath: "dest/a.md", isDirectory: false)]
            let nativeReport = batchMoveReport(
                state: .succeeded,
                planned: [batchItem("a.md")],
                opID: 930,
                standing: standing)
            state.batchMoveRunner = { _, _ in nativeReport }
            state.importInventoryRefreshRunner = { _, _ in throw ListFailure() }
            state.importScanRefreshRunner = { _, _ in }
            let owner = try XCTUnwrap(state.beginImportBatch(
                providers: [fileURLProvider(vault.appendingPathComponent("a.md"))],
                destinationFolder: "dest"))
            await state.startImportBatch(owner).value
            XCTAssertEqual(
                state.structuralUndoStack,
                [.batchMove(opId: 930, entries: standing)])
        }

        do {
            let external = tempDir.appendingPathComponent("external.md")
            try Data("external".utf8).write(to: external)
            let (state, vault) = try await makeVault(files: [
                "a.md", "seed.md", "archive/keep.md", "dest/keep.md",
            ])
            _ = try await seedBatchMoveHistory(
                in: state, path: "seed.md", destination: "archive", opID: 939)
            let standing = [BatchPathChange(
                oldPath: "a.md", newPath: "dest/a.md", isDirectory: false)]
            let nativeReport = batchMoveReport(
                state: .succeeded,
                planned: [batchItem("a.md")],
                opID: 940,
                standing: standing)
            state.batchMoveRunner = { _, _ in nativeReport }
            state.importInventoryRefreshRunner = { _, _ in throw ListFailure() }
            state.importScanRefreshRunner = { _, _ in }
            let owner = try XCTUnwrap(state.beginImportBatch(
                providers: [
                    fileURLProvider(external),
                    fileURLProvider(vault.appendingPathComponent("a.md")),
                ],
                destinationFolder: "dest"))
            await state.startImportBatch(owner).value
            XCTAssertEqual(
                state.structuralUndoStack,
                [.batchMove(opId: 940, entries: standing)])
        }
    }

    func testFL05NativeDiagnosticsCountDistinctFailedProviders() async throws {
        let (state, vault) = try await makeVault(files: [
            "a.md", "b.md", "c.md", "dest/keep.md",
        ])
        let a = batchItem("a.md")
        let nativeReport = batchMoveReport(
            state: .rejected,
            planned: [a, batchItem("b.md"), batchItem("c.md")],
            preflightFailures: [
                BatchItemFailure(
                    item: a,
                    stage: .preflight,
                    message: "destination already exists"),
                BatchItemFailure(
                    item: a,
                    stage: .linkRewrite,
                    message: "rename preflight failed"),
            ])
        state.batchMoveRunner = { _, _ in nativeReport }
        let owner = try XCTUnwrap(state.beginImportBatch(
            providers: ["a.md", "b.md", "c.md"].map {
                fileURLProvider(vault.appendingPathComponent($0))
            },
            destinationFolder: "dest"))

        await state.startImportBatch(owner).value

        guard case .result(let result)? = state.activeBatchAlertPresentation,
            case .importBatch(let report) = result.payload
        else { return XCTFail("rejected native providers need one aggregate alert") }
        XCTAssertEqual(report.terminalFailureCount, 3)
        XCTAssertEqual(
            report.details.compactMap { detail -> Int? in
                guard case .provider(let index) = detail.scope else { return nil }
                return index
            },
            [0, 0, 1, 2])
        XCTAssertTrue(
            state.lastMutationAnnouncement?.contains("3 items were not imported") == true)
    }

    func testFL05RollbackIncompleteStandingProviderIsWarningNotFailure()
        async throws
    {
        let (state, vault) = try await makeVault(files: [
            "a.md", "b.md", "c.md", "dest/keep.md",
        ])
        let a = batchItem("a.md")
        let b = batchItem("b.md")
        let c = batchItem("c.md")
        let standing = BatchPathChange(
            oldPath: "a.md", newPath: "dest/a.md", isDirectory: false)
        let restored = BatchPathChange(
            oldPath: "b.md", newPath: "dest/b.md", isDirectory: false)
        let nativeReport = batchMoveReport(
            state: .rollbackIncomplete,
            planned: [a, b, c],
            standing: [standing],
            rolledBack: [restored],
            failure: BatchItemFailure(
                item: c,
                stage: .move,
                message: "forward move failed"),
            rollbackFailures: [BatchItemFailure(
                item: a,
                stage: .rollback,
                message: "restore failed; item remains at destination")])
        state.batchMoveRunner = { _, _ in nativeReport }
        state.importInventoryRefreshRunner = { _, _ in }
        let owner = try XCTUnwrap(state.beginImportBatch(
            providers: ["a.md", "b.md", "c.md"].map {
                fileURLProvider(vault.appendingPathComponent($0))
            },
            destinationFolder: "dest"))

        await state.startImportBatch(owner).value

        guard case .result(let result)? = state.activeBatchAlertPresentation,
            case .importBatch(let report) = result.payload
        else { return XCTFail("incomplete recovery needs one aggregate alert") }
        XCTAssertEqual(report.terminalFailureCount, 2)
        XCTAssertGreaterThanOrEqual(report.warningCount, 1)
        XCTAssertTrue(report.allDetails.contains {
            $0.scope == .provider(0) && $0.kind == .warning
        })
        XCTAssertTrue(
            state.lastMutationAnnouncement?.contains("Moved 1 item") == true)
        XCTAssertTrue(
            state.lastMutationAnnouncement?.contains("2 items were not imported") == true)
    }

    func testFL05NativeRunnerThrowReportsEveryProvider() async throws {
        struct NativeFailure: LocalizedError {
            var errorDescription: String? { "native move unavailable" }
        }
        let (state, vault) = try await makeVault(files: [
            "a.md", "b.md", "dest/keep.md",
        ])
        state.batchMoveRunner = { _, _ in throw NativeFailure() }
        state.importScanRefreshRunner = { _, _ in }
        let owner = try XCTUnwrap(state.beginImportBatch(
            providers: ["a.md", "b.md"].map {
                fileURLProvider(vault.appendingPathComponent($0))
            },
            destinationFolder: "dest"))

        await state.startImportBatch(owner).value

        guard case .result(let result)? = state.activeBatchAlertPresentation,
            case .importBatch(let report) = result.payload
        else { return XCTFail("native infrastructure failure needs one aggregate alert") }
        XCTAssertEqual(report.terminalFailureCount, 2)
        XCTAssertEqual(report.details.map(\.scope), [.provider(0), .provider(1)])
    }

    func testFL05CancellationIsInertAfterAllProvidersCompleteDuringRefresh()
        async throws
    {
        let external = tempDir.appendingPathComponent("finalizing.md")
        try Data("done".utf8).write(to: external)
        let (state, _) = try await makeVault(files: [])
        let refreshGate = SuspensionGate()
        state.importInventoryRefreshRunner = { _, _ in
            await refreshGate.enter()
        }
        let owner = try XCTUnwrap(state.beginImportBatch(
            providers: [fileURLProvider(external)],
            destinationFolder: ""))
        let task = state.startImportBatch(owner)
        await refreshGate.waitForEntrants(1)

        XCTAssertEqual(owner.completedProviderCount, 1)
        XCTAssertFalse(state.requestImportBatchCancellation())
        XCTAssertFalse(owner.isCancellationRequested)

        await refreshGate.releaseOne()
        await task.value
        XCTAssertFalse(
            state.lastMutationAnnouncement?.localizedCaseInsensitiveContains("cancel") == true)
    }

    func testFL05NativeMovePhaseDisablesCancellationAndBlocksVaultTransitions()
        async throws
    {
        let (state, vault) = try await makeVault(files: ["a.md", "dest/keep.md"])
        let originalSession = try XCTUnwrap(state.currentSession)
        let gate = SuspensionGate()
        let standing = BatchPathChange(
            oldPath: "a.md", newPath: "dest/a.md", isDirectory: false)
        let report = batchMoveReport(
            state: .succeeded,
            planned: [batchItem("a.md")],
            opID: 995,
            standing: [standing])
        state.batchMoveRunner = { _, _ in
            await gate.enter()
            return report
        }
        state.importInventoryRefreshRunner = { _, _ in }
        let owner = try XCTUnwrap(state.beginImportBatch(
            providers: [fileURLProvider(vault.appendingPathComponent("a.md"))],
            destinationFolder: "dest"))

        let task = state.startImportBatch(owner)
        await gate.waitForEntrants(1)

        XCTAssertEqual(owner.progress.phase, .moving)
        XCTAssertFalse(owner.canRequestUserCancellation)
        XCTAssertFalse(owner.progress.canRequestCancellation)
        XCTAssertFalse(state.canRequestImportBatchCancellation)
        XCTAssertFalse(state.requestImportBatchCancellation())
        XCTAssertFalse(owner.isCancellationRequested)

        let replacement = tempDir.appendingPathComponent("replacement-vault")
        try FileManager.default.createDirectory(
            at: replacement, withIntermediateDirectories: false)
        state.openVault(at: replacement)
        XCTAssertTrue(state.currentSession === originalSession)
        XCTAssertFalse(state.closeVault())
        XCTAssertTrue(state.currentSession === originalSession)
        state.switchToRecent(RecentVault(url: replacement))
        XCTAssertTrue(state.currentSession === originalSession)
        XCTAssertEqual(
            state.sidebarActionBackgroundFailure,
            AppState.importNativeMoveVaultTransitionReason)
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            AppState.importNativeMoveVaultTransitionReason)

        await gate.releaseOne()
        await task.value

        XCTAssertTrue(state.currentSession === originalSession)
        XCTAssertNil(state.sidebarActionBackgroundFailure)
        XCTAssertFalse(
            state.lastMutationAnnouncement?.localizedCaseInsensitiveContains("cancel") == true)
    }

    func testFL05DirtySwitchParkedDuringImportCannotDiscardIntoNativeMove()
        async throws
    {
        let (state, vault) = try await makeVault(files: [
            "note.md", "a.md", "dest/keep.md",
        ])
        state.selectedFilePath = "note.md"
        await state.noteLoadTask?.value
        state.updateEditorText("dirty single note")
        let replacement = tempDir.appendingPathComponent("single-switch-target")
        try FileManager.default.createDirectory(
            at: replacement, withIntermediateDirectories: false)

        let gate = SuspensionGate()
        let standing = BatchPathChange(
            oldPath: "a.md", newPath: "dest/a.md", isDirectory: false)
        let report = batchMoveReport(
            state: .succeeded,
            planned: [batchItem("a.md")],
            opID: 996,
            standing: [standing])
        state.batchMoveRunner = { _, _ in
            await gate.enter()
            return report
        }
        state.importInventoryRefreshRunner = { _, _ in }
        let owner = try XCTUnwrap(state.beginImportBatch(
            providers: [fileURLProvider(vault.appendingPathComponent("a.md"))],
            destinationFolder: "dest"))
        XCTAssertEqual(owner.progress.phase, .importing)

        state.switchToRecent(RecentVault(url: replacement))
        XCTAssertEqual(state.pendingNavigation, .closeVault)
        XCTAssertEqual(state.pendingVaultSwitchTarget?.path, replacement.path)

        let task = state.startImportBatch(owner)
        await gate.waitForEntrants(1)
        XCTAssertEqual(owner.progress.phase, .moving)
        state.resolvePendingNavigationDiscard()

        XCTAssertNil(state.pendingVaultSwitchTarget)
        XCTAssertEqual(state.pendingNavigation, .closeVault)
        XCTAssertTrue(state.hasUnsavedChanges)
        XCTAssertEqual(state.currentVaultURL?.path, vault.path)

        await gate.releaseOne()
        await task.value
        state.resolvePendingNavigationDiscard()
        XCTAssertNil(state.currentVaultURL)
        XCTAssertNotEqual(state.currentVaultURL?.path, replacement.path)
    }

    func testFL05MultiDirtySwitchParkedDuringImportCannotDiscardAllIntoNativeMove()
        async throws
    {
        let (state, vault) = try await makeVault(files: [
            "note.md", "two.md", "a.md", "dest/keep.md",
        ])
        state.selectedFilePath = "two.md"
        await state.noteLoadTask?.value
        state.updateEditorText("dirty two")
        state.newTab()
        state.selectedFilePath = "note.md"
        await state.noteLoadTask?.value
        state.updateEditorText("dirty note")
        XCTAssertEqual(state.workspace.dirtyParkedDocuments().count, 1)
        let replacement = tempDir.appendingPathComponent("multi-switch-target")
        try FileManager.default.createDirectory(
            at: replacement, withIntermediateDirectories: false)

        let gate = SuspensionGate()
        let standing = BatchPathChange(
            oldPath: "a.md", newPath: "dest/a.md", isDirectory: false)
        let report = batchMoveReport(
            state: .succeeded,
            planned: [batchItem("a.md")],
            opID: 997,
            standing: [standing])
        state.batchMoveRunner = { _, _ in
            await gate.enter()
            return report
        }
        state.importInventoryRefreshRunner = { _, _ in }
        let owner = try XCTUnwrap(state.beginImportBatch(
            providers: [fileURLProvider(vault.appendingPathComponent("a.md"))],
            destinationFolder: "dest"))

        state.switchToRecent(RecentVault(url: replacement))
        XCTAssertEqual(state.pendingVaultClose, 2)
        XCTAssertEqual(state.pendingVaultSwitchTarget?.path, replacement.path)

        let task = state.startImportBatch(owner)
        await gate.waitForEntrants(1)
        state.resolveVaultCloseDiscardAll()

        XCTAssertNil(state.pendingVaultSwitchTarget)
        XCTAssertEqual(state.pendingVaultClose, 2)
        XCTAssertTrue(state.hasUnsavedChanges)
        XCTAssertEqual(state.workspace.dirtyParkedDocuments().count, 1)
        XCTAssertEqual(state.currentVaultURL?.path, vault.path)

        await gate.releaseOne()
        await task.value
        state.resolveVaultCloseDiscardAll()
        XCTAssertNil(state.currentVaultURL)
        XCTAssertNotEqual(state.currentVaultURL?.path, replacement.path)
    }

    func testFL05ScanConfirmedUnknownCopyIsWarningAndReconciliationResult()
        async throws
    {
        struct UnknownCreate: LocalizedError {
            var errorDescription: String? { "create outcome unknown" }
        }
        let external = tempDir.appendingPathComponent("uncertain.md")
        let bytes = Data("confirmed".utf8)
        try bytes.write(to: external)
        let (state, vault) = try await makeVault(files: [])
        state.importDestinationCreators = { _ in
            SidebarImportDestinationCreators(
                createFile: { path, data in
                    try data.write(to: vault.appendingPathComponent(path))
                    throw UnknownCreate()
                },
                createDirectory: { path in
                    try FileManager.default.createDirectory(
                        at: vault.appendingPathComponent(path),
                        withIntermediateDirectories: false)
                    throw UnknownCreate()
                })
        }
        let owner = try XCTUnwrap(state.beginImportBatch(
            providers: [fileURLProvider(external)],
            destinationFolder: ""))

        await state.startImportBatch(owner).value

        guard case .result(let result)? = state.activeBatchAlertPresentation,
            case .importBatch(let report) = result.payload,
            case .importBatch(let materialized, _, _)? = state.treeMutation?.kind
        else { return XCTFail("reconciled uncertainty needs warning and tree landing") }
        XCTAssertEqual(report.terminalFailureCount, 0)
        XCTAssertEqual(report.warningCount, 1)
        XCTAssertEqual(materialized.map(\.path), ["uncertain.md"])
        XCTAssertTrue(
            state.lastMutationAnnouncement?.contains(
                "Found 1 destination item during reconciliation") == true)
        XCTAssertFalse(
            state.lastMutationAnnouncement?.contains("No items were imported") == true)
    }

    func testFL05RecursiveExternalFailuresCountDistinctEntriesWithinOneProvider()
        async throws
    {
        let source = tempDir.appendingPathComponent("source", isDirectory: true)
        try FileManager.default.createDirectory(
            at: source,
            withIntermediateDirectories: false)
        try FileManager.default.createSymbolicLink(
            at: source.appendingPathComponent("first-link"),
            withDestinationURL: tempDir.appendingPathComponent("missing-first"))
        try FileManager.default.createSymbolicLink(
            at: source.appendingPathComponent("second-link"),
            withDestinationURL: tempDir.appendingPathComponent("missing-second"))
        let (state, _) = try await makeVault(files: [])
        let owner = try XCTUnwrap(state.beginImportBatch(
            providers: [fileURLProvider(source)],
            destinationFolder: ""))

        await state.startImportBatch(owner).value

        guard case .result(let result)? = state.activeBatchAlertPresentation,
            case .importBatch(let report) = result.payload
        else { return XCTFail("recursive source failures need one bounded alert") }
        XCTAssertEqual(report.terminalFailureCount, 2)
        XCTAssertEqual(report.details.map(\.scope), [.provider(0), .provider(0)])
        XCTAssertTrue(
            state.lastMutationAnnouncement?.contains("2 items were not imported") == true)
    }

    // MARK: - Versioned private batch payload

    func testPrivateDragPayloadRoundTripsOrderAndKindDeterministically() throws {
        let items = [
            FileTreeSidebar.DragPayloadItem(path: "folder", isDirectory: true),
            FileTreeSidebar.DragPayloadItem(path: "folder/note.md", isDirectory: false),
            FileTreeSidebar.DragPayloadItem(path: "other.md", isDirectory: false),
        ]
        let first = try XCTUnwrap(FileTreeSidebar.encodeDragPayload(items))
        let second = try XCTUnwrap(FileTreeSidebar.encodeDragPayload(items))

        XCTAssertEqual(first, second)
        XCTAssertEqual(FileTreeSidebar.decodeDragPayload(first), items)
    }

    func testPrivateDragPayloadRejectsEmptyMalformedUnsafeAndDuplicateBatches() {
        let invalidPayloads = [
            #"{"version":1,"items":[]}"#,
            #"{"version":2,"items":[{"path":"a.md","isDirectory":false}]}"#,
            #"{"version":1,"items":[{"path":"","isDirectory":false}]}"#,
            #"{"version":1,"items":[{"path":"/tmp/a.md","isDirectory":false}]}"#,
            #"{"version":1,"items":[{"path":"a/../b.md","isDirectory":false}]}"#,
            #"{"version":1,"items":[{"path":"a.md","isDirectory":false},{"path":"a.md","isDirectory":true}]}"#,
            "not-json",
            "legacy.md",
        ]

        for payload in invalidPayloads {
            XCTAssertNil(
                FileTreeSidebar.decodeDragPayload(Data(payload.utf8)),
                "must fail closed: \(payload)")
        }
        XCTAssertNil(FileTreeSidebar.encodeDragPayload([]))
    }

    func testFirstPrivateProviderWinsGloballyAndNeverFallsBackToPublic() async throws {
        let (state, _) = try await makeVault(files: [])
        let publicOnly = NSItemProvider()
        publicOnly.registerDataRepresentation(
            forTypeIdentifier: FileTreeSidebar.fileURLUTType,
            visibility: .all
        ) { _ in
            XCTFail("an earlier public-only provider must never load")
            return nil
        }
        let firstPrivate = NSItemProvider()
        let privateLoad = expectation(description: "first private loaded once")
        privateLoad.assertForOverFulfill = true
        firstPrivate.registerDataRepresentation(
            forTypeIdentifier: FileTreeSidebar.nodeUTType,
            visibility: .ownProcess
        ) { completion in
            privateLoad.fulfill()
            completion(Data("malformed".utf8), nil)
            return nil
        }
        firstPrivate.registerDataRepresentation(
            forTypeIdentifier: FileTreeSidebar.fileURLUTType,
            visibility: .all
        ) { _ in
            XCTFail("the selected private provider's public flavor must never load")
            return nil
        }
        let secondPrivate = NSItemProvider()
        secondPrivate.registerDataRepresentation(
            forTypeIdentifier: FileTreeSidebar.nodeUTType,
            visibility: .ownProcess
        ) { _ in
            XCTFail("only the first private provider may load")
            return nil
        }

        let preferred = FileTreeSidebar.preferredDropProvider(
            in: [publicOnly, firstPrivate, secondPrivate])
        guard case let .privatePayload(selected) = preferred else {
            return XCTFail("private data must win; invalid private data must not become an import")
        }
        XCTAssertTrue(selected === firstPrivate)
        let privateDispatch = expectation(description: "malformed private dispatch")
        privateDispatch.isInverted = true
        XCTAssertTrue(
            FileTreeSidebar.loadAdmittedDropProvider(
                preferred,
                appState: state,
                onPrivate: { _, _ in privateDispatch.fulfill() },
                onFileURL: { _ in XCTFail("private bytes must not fall through") }))
        await fulfillment(of: [privateLoad], timeout: 1)
        await fulfillment(of: [privateDispatch], timeout: 0.2)
    }

    func testForgedPrivatePayloadCannotDispatchAnInAppMove() async throws {
        let (state, _) = try await makeVault(files: ["a.md", "dest/keep.md"])
        let forged = try XCTUnwrap(
            FileTreeSidebar.encodeDragPayload([
                .init(path: "a.md", isDirectory: false),
            ], preferredFocusPath: "a.md"))
        let provider = NSItemProvider()
        provider.registerDataRepresentation(
            forTypeIdentifier: FileTreeSidebar.nodeUTType,
            visibility: .all
        ) { completion in
            completion(forged, nil)
            return nil
        }
        let privateDispatch = expectation(description: "forged private dispatch")
        privateDispatch.isInverted = true

        XCTAssertTrue(
            FileTreeSidebar.loadAdmittedDropProvider(
                .privatePayload(provider),
                appState: state,
                onPrivate: { _, _ in privateDispatch.fulfill() },
                onFileURL: { _ in XCTFail("private bytes must not fall through as a URL") }))

        await fulfillment(of: [privateDispatch], timeout: 0.2)
    }

    func testRegisteredPrivatePayloadCannotCrossVaults() async throws {
        let (state, _) = try await makeVault(files: ["dest/keep.md"])
        let originVault = tempDir.appendingPathComponent("other-vault")
        let originURL = originVault.appendingPathComponent("a.md")
        let provider = FileTreeSidebar.makeDragProvider(
            items: [.init(path: "a.md", isDirectory: false)],
            originFileURL: originURL,
            preferredFocusPath: "a.md",
            originSession: state.currentSession)
        XCTAssertTrue(
            provider.registeredTypeIdentifiers.contains(FileTreeSidebar.fileURLUTType),
            "cross-app interoperability must retain the public file URL")
        let privateDispatch = expectation(description: "cross-vault private dispatch")
        privateDispatch.isInverted = true

        XCTAssertTrue(
            FileTreeSidebar.loadAdmittedDropProvider(
                .privatePayload(provider),
                appState: state,
                onPrivate: { _, _ in privateDispatch.fulfill() },
                onFileURL: { _ in XCTFail("the preferred private flavor must not fall through") }))

        await fulfillment(of: [privateDispatch], timeout: 0.2)
    }

    func testRegisteredPrivatePayloadDispatchesWithinItsOriginVault() async throws {
        let (state, vault) = try await makeVault(files: ["a.md", "dest/keep.md"])
        let source = FileTreeSidebar.makeDragProvider(
            items: [.init(path: "a.md", isDirectory: false)],
            originFileURL: vault.appendingPathComponent("a.md"),
            preferredFocusPath: "a.md",
            originSession: state.currentSession)
        let registeredData: Data = try await withCheckedThrowingContinuation { continuation in
            source.loadDataRepresentation(
                forTypeIdentifier: FileTreeSidebar.nodeUTType
            ) { data, error in
                if let data { continuation.resume(returning: data) }
                else {
                    continuation.resume(
                        throwing: error ?? URLError(.cannotDecodeRawData))
                }
            }
        }
        let provider = NSItemProvider()
        provider.registerDataRepresentation(
            forTypeIdentifier: FileTreeSidebar.nodeUTType,
            visibility: .ownProcess
        ) { completion in
            completion(registeredData, nil)
            completion(registeredData, nil)
            return nil
        }
        provider.registerDataRepresentation(
            forTypeIdentifier: FileTreeSidebar.fileURLUTType,
            visibility: .all
        ) { _ in
            XCTFail("a private callback must never load public fallback data")
            return nil
        }
        let privateDispatch = expectation(description: "same-vault private dispatch")
        privateDispatch.assertForOverFulfill = true

        XCTAssertTrue(
            FileTreeSidebar.loadAdmittedDropProvider(
                .privatePayload(provider),
                appState: state,
                onPrivate: { items, preferredFocusPath in
                    XCTAssertEqual(items, [.init(path: "a.md", isDirectory: false)])
                    XCTAssertEqual(preferredFocusPath, "a.md")
                    privateDispatch.fulfill()
                },
                onFileURL: { _ in XCTFail("the preferred private flavor must not fall through") }))

        await fulfillment(of: [privateDispatch], timeout: 1)

        let replay = NSItemProvider()
        replay.registerDataRepresentation(
            forTypeIdentifier: FileTreeSidebar.nodeUTType,
            visibility: .ownProcess
        ) { completion in
            completion(registeredData, nil)
            return nil
        }
        let replayDispatch = expectation(description: "consumed capability replay")
        replayDispatch.isInverted = true
        XCTAssertTrue(
            FileTreeSidebar.loadAdmittedDropProvider(
                .privatePayload(replay),
                appState: state,
                onPrivate: { _, _ in replayDispatch.fulfill() },
                onFileURL: { _ in XCTFail("private replay must not fall through") }))
        await fulfillment(of: [replayDispatch], timeout: 0.2)
    }

    func testRegisteredPrivatePayloadErrorFailsClosedAndConsumesCapabilityOnce()
        async throws
    {
        let (state, vault) = try await makeVault(files: ["a.md", "dest/keep.md"])
        let source = FileTreeSidebar.makeDragProvider(
            items: [.init(path: "a.md", isDirectory: false)],
            originFileURL: vault.appendingPathComponent("a.md"),
            preferredFocusPath: "a.md",
            originSession: state.currentSession)
        let registeredData: Data = try await withCheckedThrowingContinuation { continuation in
            source.loadDataRepresentation(
                forTypeIdentifier: FileTreeSidebar.nodeUTType
            ) { data, error in
                if let data { continuation.resume(returning: data) }
                else {
                    continuation.resume(
                        throwing: error ?? URLError(.cannotDecodeRawData))
                }
            }
        }

        let selected = NSItemProvider()
        var publicLoads = 0
        selected.registerDataRepresentation(
            forTypeIdentifier: FileTreeSidebar.nodeUTType,
            visibility: .ownProcess
        ) { completion in
            completion(
                registeredData,
                NSError(
                    domain: "FileTreeDragDropTests",
                    code: 1,
                    userInfo: [NSLocalizedDescriptionKey: "private load failed"]))
            completion(registeredData, nil)
            return nil
        }
        selected.registerDataRepresentation(
            forTypeIdentifier: FileTreeSidebar.fileURLUTType,
            visibility: .all
        ) { completion in
            publicLoads += 1
            completion(vault.appendingPathComponent("a.md").dataRepresentation, nil)
            return nil
        }
        let privateDispatch = expectation(description: "errored private dispatch")
        privateDispatch.isInverted = true

        XCTAssertTrue(
            FileTreeSidebar.loadAdmittedDropProvider(
                .privatePayload(selected),
                appState: state,
                onPrivate: { _, _ in privateDispatch.fulfill() },
                onFileURL: { _ in XCTFail("private failure must not fall through") }))

        await fulfillment(of: [privateDispatch], timeout: 0.2)
        XCTAssertEqual(publicLoads, 0)
    }

    func testTreeDropMoveIntentUsesSingleAndBatchFunnels() async throws {
        do {
            let (state, vault) = try await makeVault(files: ["a.md", "dest/keep.md"])
            await state.moveTreeSelection(
                [AppState.TreeSelection(path: "a.md", isDirectory: false)],
                to: "dest")?.value
            XCTAssertTrue(exists(vault, "dest/a.md"))
            XCTAssertEqual(state.lastMutationAnnouncement, "Moved a.md to dest.")
        }

        try FileManager.default.removeItem(at: tempDir.appendingPathComponent("vault"))
        let (state, vault) = try await makeVault(files: ["a.md", "b.md", "dest/keep.md"])
        await state.moveTreeSelection(
            [
                AppState.TreeSelection(path: "a.md", isDirectory: false),
                AppState.TreeSelection(path: "b.md", isDirectory: false),
            ],
            to: "dest")?.value
        XCTAssertTrue(exists(vault, "dest/a.md"))
        XCTAssertTrue(exists(vault, "dest/b.md"))
        XCTAssertEqual(state.lastMutationAnnouncement, "Moved 2 items to dest.")
    }

    // MARK: - Drag payload carries public.file-url (drag OUT)

    func testDragProviderCarriesBothPrivateTypeAndFileURL() {
        let fileURL = URL(fileURLWithPath: "/Vaults/demo/Notes/idea.md")
        let provider = FileTreeSidebar.makeDragProvider(
            nodePath: "Notes/idea.md", fileURL: fileURL)
        let ids = provider.registeredTypeIdentifiers
        XCTAssertTrue(
            ids.contains(FileTreeSidebar.nodeUTType),
            "the private own-process type is still present for precise intra-tree moves")
        XCTAssertTrue(
            ids.contains(UTType.fileURL.identifier),
            "public.file-url is carried so the item can be dragged OUT to Finder")
        XCTAssertEqual(
            provider.suggestedName, "idea.md", "the drop gets a sensible file name")
    }

    func testDragProviderPrivateFlavorCarriesSelfDescribingOrderedBatch() async throws {
        let items = [
            FileTreeSidebar.DragPayloadItem(path: "folder", isDirectory: true),
            FileTreeSidebar.DragPayloadItem(path: "other.md", isDirectory: false),
        ]
        let originURL = URL(fileURLWithPath: "/Vaults/demo/folder")
        let provider = FileTreeSidebar.makeDragProvider(
            items: items, originFileURL: originURL)

        let data: Data = try await withCheckedThrowingContinuation { continuation in
            provider.loadDataRepresentation(
                forTypeIdentifier: FileTreeSidebar.nodeUTType
            ) { data, error in
                if let data { continuation.resume(returning: data) }
                else { continuation.resume(throwing: error ?? URLError(.cannotDecodeRawData)) }
            }
        }

        XCTAssertEqual(FileTreeSidebar.decodeDragPayload(data), items)
        XCTAssertEqual(provider.suggestedName, "folder")
    }

    func testDragProviderProjectsSelectionButKeepsOriginPublicFileURL() async throws {
        let a = fileRow("a.md")
        let b = fileRow("nested/b.md")
        let rows = [
            FileTreeSidebar.SelectionRow(identity: a, path: "a.md", isDirectory: false),
            FileTreeSidebar.SelectionRow(
                identity: b, path: "nested/b.md", isDirectory: false),
        ]
        let model = FileTreeSidebar.SelectionModel(
            focused: b,
            selected: [b, a],
            selectionPathSnapshots: [a: "a.md", b: "nested/b.md"],
            rangeAnchor: a,
            rangeAnchorPathSnapshot: "a.md")
        let vaultURL = URL(fileURLWithPath: "/Vaults/demo")

        let provider = FileTreeSidebar.makeDragProvider(
            origin: rows[1], from: model, visibleRows: rows, vaultURL: vaultURL)
        let data: Data = try await withCheckedThrowingContinuation { continuation in
            provider.loadDataRepresentation(
                forTypeIdentifier: FileTreeSidebar.nodeUTType
            ) { data, error in
                if let data { continuation.resume(returning: data) }
                else { continuation.resume(throwing: error ?? URLError(.cannotDecodeRawData)) }
            }
        }

        XCTAssertEqual(
            FileTreeSidebar.decodeDragPayload(data)?.map(\.path), ["a.md", "nested/b.md"])
        XCTAssertEqual(provider.suggestedName, "b.md")
        let publicURL: URL = try await withCheckedThrowingContinuation { continuation in
            _ = provider.loadObject(ofClass: URL.self) { url, error in
                if let url { continuation.resume(returning: url) }
                else { continuation.resume(throwing: error ?? URLError(.badURL)) }
            }
        }
        XCTAssertEqual(publicURL.standardizedFileURL.path, "/Vaults/demo/nested/b.md")
    }

    func testC2PrivateDragPayloadCarriesTheOriginAsPreferredFocus() async throws {
        let a = fileRow("a.md")
        let b = fileRow("nested/b.md")
        let rows = [
            FileTreeSidebar.SelectionRow(identity: a, path: "a.md", isDirectory: false),
            FileTreeSidebar.SelectionRow(
                identity: b, path: "nested/b.md", isDirectory: false),
        ]
        let model = FileTreeSidebar.SelectionModel(
            focused: b,
            selected: [b, a],
            selectionPathSnapshots: [a: "a.md", b: "nested/b.md"],
            rangeAnchor: a,
            rangeAnchorPathSnapshot: "a.md")
        let provider = FileTreeSidebar.makeDragProvider(
            origin: rows[1], from: model, visibleRows: rows,
            vaultURL: URL(fileURLWithPath: "/Vaults/demo"))

        let data: Data = try await withCheckedThrowingContinuation { continuation in
            provider.loadDataRepresentation(
                forTypeIdentifier: FileTreeSidebar.nodeUTType
            ) { data, error in
                if let data { continuation.resume(returning: data) }
                else {
                    continuation.resume(
                        throwing: error ?? URLError(.cannotDecodeRawData))
                }
            }
        }
        let object = try XCTUnwrap(
            JSONSerialization.jsonObject(with: data) as? [String: Any])
        XCTAssertEqual(
            object["preferredFocusPath"] as? String,
            "nested/b.md",
            "the initiating row must survive decode as the batch focus locus")
    }

    func testC2PrivateDragPayloadRejectsPreferredFocusOutsideItsItems() {
        let invalid = Data(
            #"{"version":1,"items":[{"path":"a.md","isDirectory":false}],"preferredFocusPath":"missing.md"}"#.utf8)
        XCTAssertNil(
            FileTreeSidebar.decodeDragPayload(invalid),
            "an injected focus path that was not dragged must fail closed")
    }

    /// The file-URL flavor round-trips the real on-disk URL (what Finder reads
    /// to copy the referenced file).
    func testDragProviderFileURLLoadsBackTheURL() async throws {
        let fileURL = URL(fileURLWithPath: "/Vaults/demo/idea.md")
        let provider = FileTreeSidebar.makeDragProvider(nodePath: "idea.md", fileURL: fileURL)

        let loaded: URL = try await withCheckedThrowingContinuation { cont in
            _ = provider.loadObject(ofClass: URL.self) { url, err in
                if let url { cont.resume(returning: url) }
                else { cont.resume(throwing: err ?? URLError(.badURL)) }
            }
        }
        XCTAssertEqual(loaded.standardizedFileURL.path, fileURL.standardizedFileURL.path)
    }

    /// No vault URL (welcome screen edge) → the file-URL flavor is simply
    /// omitted; the private type still registers so nothing crashes.
    func testDragProviderWithoutFileURLStillRegistersPrivateType() {
        let provider = FileTreeSidebar.makeDragProvider(nodePath: "a.md", fileURL: nil)
        XCTAssertEqual(provider.registeredTypeIdentifiers, [FileTreeSidebar.nodeUTType])
    }

    // MARK: - Pure drop decision: import vs move

    func testExternalFileURLResolvesToImport() {
        let vault = URL(fileURLWithPath: "/Vaults/demo")
        let external = URL(fileURLWithPath: "/Users/me/Downloads/clip.md")
        let action = AppState.fileURLDropAction(
            url: external, vaultURL: vault, destinationFolder: "Notes", isDirectory: false)
        XCTAssertEqual(action, .importFile(url: external, into: "Notes"))
    }

    func testInVaultFileURLResolvesToMove() {
        let vault = URL(fileURLWithPath: "/Vaults/demo")
        let inside = vault.appendingPathComponent("a.md")
        let action = AppState.fileURLDropAction(
            url: inside, vaultURL: vault, destinationFolder: "dest", isDirectory: false)
        XCTAssertEqual(action, .move(path: "a.md", isDirectory: false, to: "dest"))
    }

    func testInVaultDropAlreadyInDestinationIsNoOp() {
        let vault = URL(fileURLWithPath: "/Vaults/demo")
        let inside = vault.appendingPathComponent("dest/a.md")
        // Already directly in "dest" → no-op (same guard as the private path).
        let action = AppState.fileURLDropAction(
            url: inside, vaultURL: vault, destinationFolder: "dest", isDirectory: false)
        XCTAssertEqual(
            action,
            .none(reason: AppState.sameParentImportNoOpReason))
    }

    func testFolderDropIntoOwnSubtreeIsNoOp() {
        let vault = URL(fileURLWithPath: "/Vaults/demo")
        let folder = vault.appendingPathComponent("parent")
        // Dropping "parent" into "parent/child" is a folder-into-own-subtree.
        let action = AppState.fileURLDropAction(
            url: folder, vaultURL: vault, destinationFolder: "parent/child", isDirectory: true)
        XCTAssertEqual(
            action,
            .none(reason: AppState.ownSubtreeImportNoOpReason))
    }

    func testVaultRelativePathClassification() {
        let vault = URL(fileURLWithPath: "/Vaults/demo")
        XCTAssertEqual(
            AppState.vaultRelativePath(
                of: vault.appendingPathComponent("Notes/a.md"), vaultURL: vault),
            "Notes/a.md")
        XCTAssertNil(
            AppState.vaultRelativePath(
                of: URL(fileURLWithPath: "/elsewhere/a.md"), vaultURL: vault),
            "an external file is not vault-relative")
        XCTAssertNil(
            AppState.vaultRelativePath(of: vault, vaultURL: vault),
            "the vault root itself is not a movable entry")
        XCTAssertNil(
            AppState.vaultRelativePath(of: vault.appendingPathComponent("a.md"), vaultURL: nil),
            "no open vault → nothing is vault-relative")
    }

    /// #870 Codex round 1 (F3): containment is FILESYSTEM-aware — a file
    /// reached through a symlinked path still classifies as in-vault (→ an
    /// undoable move), not external (→ a duplicate import). Uses real files so
    /// symlink resolution has something to resolve.
    func testVaultRelativePathResolvesSymlinkedContainment() throws {
        let realVault = tempDir.appendingPathComponent("realvault")
        try FileManager.default.createDirectory(
            at: realVault, withIntermediateDirectories: true)
        try "# a\n".write(
            to: realVault.appendingPathComponent("a.md"),
            atomically: true, encoding: .utf8)
        let link = tempDir.appendingPathComponent("linkvault")
        try FileManager.default.createSymbolicLink(at: link, withDestinationURL: realVault)

        XCTAssertEqual(
            AppState.vaultRelativePath(
                of: link.appendingPathComponent("a.md"), vaultURL: realVault),
            "a.md",
            "a file reached via a symlink to the vault is in-vault, not external")
    }

    /// #870 Codex round 2 (F3): an EXTERNAL symlink FILE that points INTO the
    /// vault must classify as external (→ import a copy), NOT be dereferenced
    /// to its in-vault target (→ a move of the real note, breaking the link).
    /// Only the container is symlink-resolved; the dropped item's own final
    /// component is preserved.
    func testExternalSymlinkFileIsNotDereferencedToVaultTarget() throws {
        let realVault = tempDir.appendingPathComponent("realvault2")
        try FileManager.default.createDirectory(
            at: realVault, withIntermediateDirectories: true)
        try "# a\n".write(
            to: realVault.appendingPathComponent("a.md"),
            atomically: true, encoding: .utf8)
        // An external symlink FILE (outside the vault) pointing at vault/a.md.
        let externalLink = tempDir.appendingPathComponent("shortcut.md")
        try FileManager.default.createSymbolicLink(
            at: externalLink, withDestinationURL: realVault.appendingPathComponent("a.md"))

        XCTAssertNil(
            AppState.vaultRelativePath(of: externalLink, vaultURL: realVault),
            "an external symlink file is external (import), not its vault target")
    }

    func testFinalSymlinkInsideVaultStaysExternalForNoFollowWalker() throws {
        let vault = tempDir.appendingPathComponent("final-link-vault")
        try FileManager.default.createDirectory(
            at: vault, withIntermediateDirectories: true)
        let target = vault.appendingPathComponent("target.md")
        try "target".write(to: target, atomically: true, encoding: .utf8)
        let link = vault.appendingPathComponent("shortcut.md")
        try FileManager.default.createSymbolicLink(
            at: link, withDestinationURL: target)

        XCTAssertNil(AppState.vaultRelativePath(of: link, vaultURL: vault))
        XCTAssertEqual(
            AppState.fileURLDropAction(
                url: link,
                vaultURL: vault,
                destinationFolder: "dest",
                isDirectory: false),
            .importFile(url: link, into: "dest"),
            "a dropped final symlink must reach the no-follow import walker")
    }

    func testSymlinkToVaultRootIsNotMistakenForActualRootNoOp() throws {
        let vault = tempDir.appendingPathComponent("actual-root")
        try FileManager.default.createDirectory(
            at: vault, withIntermediateDirectories: true)
        let rootLink = tempDir.appendingPathComponent("root-shortcut")
        try FileManager.default.createSymbolicLink(
            at: rootLink, withDestinationURL: vault)

        XCTAssertEqual(
            AppState.fileURLDropAction(
                url: rootLink,
                vaultURL: vault,
                destinationFolder: "",
                isDirectory: true),
            .importFile(url: rootLink, into: ""),
            "only the actual vault directory is the root no-op")
    }

    /// #870 Codex round 3 (F3): dragging the CURRENT VAULT ROOT onto its own
    /// tree is a no-op, NOT an external import (both the root and an external
    /// URL map to a nil vault-relative path — `fileURLDropAction` must
    /// distinguish them and return `.none` for the root).
    func testDroppingVaultRootIsNoOpNotImport() throws {
        let vault = tempDir.appendingPathComponent("rootdrop")
        try FileManager.default.createDirectory(
            at: vault, withIntermediateDirectories: true)

        XCTAssertEqual(
            AppState.fileURLDropAction(
                url: vault, vaultURL: vault, destinationFolder: "", isDirectory: true),
            .none(reason: AppState.vaultRootImportNoOpReason),
            "the vault root dropped onto itself is a no-op, not a text import")
    }

    /// Codoki: the extracted `urlIsDirectory` seam classifies real directories
    /// vs files correctly (the drop router feeds this into `fileURLDropAction`).
    func testUrlIsDirectoryClassifiesDirectoriesAndFiles() throws {
        let dir = tempDir.appendingPathComponent("a-folder")
        try FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        let file = tempDir.appendingPathComponent("a-file.md")
        try "# hi\n".write(to: file, atomically: true, encoding: .utf8)

        XCTAssertTrue(AppState.urlIsDirectory(dir), "a real directory reads as a directory")
        XCTAssertFalse(AppState.urlIsDirectory(file), "a real file does not")
        XCTAssertFalse(
            AppState.urlIsDirectory(tempDir.appendingPathComponent("does-not-exist")),
            "an unreadable URL falls back to false (safe file default)")
    }

    // MARK: - Import (external drop) end-to-end

    func testExternalFileDropImportsIntoVault() async throws {
        let (state, vault) = try await makeVault(files: ["a.md"])
        // A file OUTSIDE the vault, dropped onto the root.
        let external = tempDir.appendingPathComponent("outside.md")
        try "# outside\nbody\n".write(to: external, atomically: true, encoding: .utf8)

        let action = AppState.fileURLDropAction(
            url: external, vaultURL: vault, destinationFolder: "", isDirectory: false)
        XCTAssertEqual(action, .importFile(url: external, into: ""))

        await state.importEntry(externalURL: external, into: "")?.value

        XCTAssertTrue(exists(vault, "outside.md"), "the external file was copied in")
        XCTAssertEqual(
            try String(contentsOf: vault.appendingPathComponent("outside.md"), encoding: .utf8),
            "# outside\nbody\n", "content preserved")
        XCTAssertEqual(state.lastMutationAnnouncement, "Imported outside.md.")
        // A copy — the original stays put outside the vault.
        XCTAssertTrue(FileManager.default.fileExists(atPath: external.path))
    }

    /// An import that collides with an existing vault name reuses the SAME
    /// no-clobber collision surface as a colliding move (`lastError` +
    /// "Could not import …"), never silently overwriting.
    func testImportCollisionSurfacesTheSharedFailurePath() async throws {
        let (state, vault) = try await makeVault(files: ["dupe.md"])
        let original = try String(
            contentsOf: vault.appendingPathComponent("dupe.md"), encoding: .utf8)

        let external = tempDir.appendingPathComponent("dupe.md")
        try "DIFFERENT CONTENT\n".write(to: external, atomically: true, encoding: .utf8)

        await state.importEntry(externalURL: external, into: "")?.value

        XCTAssertNotNil(state.lastError, "a name collision surfaces an error")
        let announcement = try XCTUnwrap(state.lastMutationAnnouncement)
        XCTAssertTrue(
            announcement.hasPrefix("Could not import dupe.md: "),
            "failure form matches the shared 'Could not <verb> <name>: …' — got \(announcement)")
        XCTAssertEqual(
            try String(contentsOf: vault.appendingPathComponent("dupe.md"), encoding: .utf8),
            original, "the existing vault file is NOT clobbered")
    }

    /// #910: a binary / non-UTF-8 external drop imports as a byte-for-byte
    /// copy (via `createExclusiveBytes`) instead of the pre-PR text-only
    /// clean failure — same "Imported <name>." announcement as the text path.
    func testBinaryExternalDropImportsByteForByte() async throws {
        let (state, vault) = try await makeVault(files: ["a.md"])
        // A payload no valid UTF-8 string can hold (lone 0xFF/0xFE + 0xC0/0xC1).
        let bytes: [UInt8] = [0xFF, 0xFE, 0x00, 0x01, 0x80, 0xC0, 0xC1]
        let external = tempDir.appendingPathComponent("photo.png")
        try Data(bytes).write(to: external)

        await state.importEntry(externalURL: external, into: "")?.value

        XCTAssertTrue(exists(vault, "photo.png"), "the binary file was copied in")
        XCTAssertEqual(
            try Data(contentsOf: vault.appendingPathComponent("photo.png")), Data(bytes),
            "bytes round-trip identically, including the non-UTF-8 bytes")
        XCTAssertEqual(state.lastMutationAnnouncement, "Imported photo.png.")
        XCTAssertNil(state.lastError, "a successful binary import surfaces no error")
    }

    /// #910 red-team Medium: an oversized external drop is refused GRACEFULLY
    /// (via the shared `FileTooLarge` failure path) instead of crashing when
    /// its >2 GiB `Data`/`String` would trap in the FFI's `Int32(count)`
    /// converter. The pre-read size guard trips first. Driven with a SPARSE
    /// file one byte past the refuse ceiling — `truncate` sets the logical
    /// size without writing gigabytes, so the guard sees the over-cap size and
    /// the bytes are never allocated (let alone lowered across the FFI).
    func testOversizedExternalDropIsRefusedGracefullyNotCrashed() async throws {
        let (state, vault) = try await makeVault(files: ["a.md"])
        let refuse = try XCTUnwrap(state.currentSession).largeFileRefuseBytes()
        let big = tempDir.appendingPathComponent("huge.bin")
        XCTAssertTrue(FileManager.default.createFile(atPath: big.path, contents: nil))
        let handle = try FileHandle(forWritingTo: big)
        try handle.truncate(atOffset: refuse + 1)
        try handle.close()

        await state.importEntry(externalURL: big, into: "")?.value

        XCTAssertFalse(exists(vault, "huge.bin"), "the oversized file was not imported")
        XCTAssertNotNil(state.lastError, "the refusal surfaced an error")
        let announcement = try XCTUnwrap(state.lastMutationAnnouncement)
        XCTAssertTrue(
            announcement.hasPrefix("Could not import huge.bin: "),
            "refusal routes through the shared 'Could not import …' path — got \(announcement)")
    }

    /// #910 (Codex follow-up): the byte-ceiling decision does NOT trust a
    /// missing or stale preflight — the actual read count is the definitive
    /// gate, so a nil-metadata source whose bytes exceed the ceiling is still
    /// refused (the exact crash path the earlier metadata-only guard missed).
    func testImportOverCeilingGatesOnActualBytesWhenMetadataMissingOrStale() {
        let cap: UInt64 = 10
        // Pre-read call (no bytes yet), metadata unavailable → proceed.
        XCTAssertNil(
            AppState.importOverCeiling(metadataSize: nil, readByteCount: nil, refuseBytes: cap))
        // Metadata unavailable (nil), but the bytes IN HAND exceed the cap →
        // REFUSE. This is the nil-preflight / package-source crash path.
        XCTAssertEqual(
            AppState.importOverCeiling(metadataSize: nil, readByteCount: 11, refuseBytes: cap), 11)
        // A file that grew past the cap after a passing/absent stat (TOCTOU) is
        // caught by the post-read count.
        XCTAssertEqual(
            AppState.importOverCeiling(metadataSize: nil, readByteCount: 5_000, refuseBytes: cap),
            5_000)
        // Within-limit under both signals → proceed (boundary: exactly at cap).
        XCTAssertNil(
            AppState.importOverCeiling(metadataSize: 10, readByteCount: 10, refuseBytes: cap))
        // A preflight over the cap fast-rejects before any read.
        XCTAssertEqual(
            AppState.importOverCeiling(metadataSize: 20, readByteCount: nil, refuseBytes: cap), 20)
    }

    /// #910: the bounded reader never loads more than `cap + 1` bytes, so a
    /// nil-metadata multi-GB source can't be fully read into memory (nor reach
    /// the FFI). A within-cap file is returned in full, byte-identical.
    func testReadImportBytesCapsAtCeilingPlusOne() throws {
        // A file well past the cap → the reader returns exactly cap + 1 bytes,
        // not the whole 60.
        let big = tempDir.appendingPathComponent("cap-big.bin")
        try Data(repeating: 0xAB, count: 60).write(to: big)
        XCTAssertEqual(
            try AppState.readImportBytes(from: big, cap: 10).count, 11,
            "reads at most cap + 1, never the whole oversized file")

        // A within-cap file (incl. non-UTF-8 bytes) is returned verbatim.
        let small = tempDir.appendingPathComponent("cap-small.bin")
        let payload = Data([0xFF, 0xFE, 0x00, 0x01, 0x80])
        try payload.write(to: small)
        XCTAssertEqual(try AppState.readImportBytes(from: small, cap: 10), payload)
    }

    /// #910 (Codex rounds 2–3): the effective transport ceiling clamps the
    /// engine threshold to `Int32.max - 4` — the 4 being the RustBuffer length
    /// prefix, so the OUTER FFI buffer conversion `Int32(payload.count + 4)` (not
    /// just the inner `Int32(value.count)`) cannot trap. Even a pathological
    /// >2 GiB `large_file_refuse_bytes` config cannot let a buffer whose
    /// serialized length exceeds `Int32.max` reach the FFI.
    func testTransportCeilingClampsBelowFfiInt32Limit() {
        let int32Max = UInt64(Int32.max)  // 2_147_483_647
        // (a) A >2 GiB config clamps to Int32.max - 4; Int32.max itself clamps
        //     to Int32.max - 4 (min with the strictly-smaller bound).
        XCTAssertEqual(
            AppState.importTransportCeiling(refuseBytes: int32Max + 1000), int32Max - 4)
        XCTAssertEqual(
            AppState.importTransportCeiling(refuseBytes: int32Max), int32Max - 4)
        // The serialized buffer (payload + 4-byte length prefix) fits in Int32,
        // so neither the inner nor the outer converter conversion can trap.
        let clamped = AppState.importTransportCeiling(refuseBytes: int32Max + 1000)
        XCTAssertLessThanOrEqual(
            clamped + 4, int32Max,
            "payload.count + 4 (the RustBuffer length) must be representable as Int32")
        // The ~50 MiB default is far below the limit → passes through unchanged.
        let fiftyMiB: UInt64 = 50 * 1024 * 1024
        XCTAssertEqual(AppState.importTransportCeiling(refuseBytes: fiftyMiB), fiftyMiB)
        // (c) The clamped ceiling is Int-safe, so the reader's `cap + 1`
        //     sentinel can never overflow Int.
        XCTAssertLessThan(
            AppState.importTransportCeiling(refuseBytes: int32Max + 1_000_000), UInt64(Int.max))

        // (b) Under the clamped ceiling, a buffer AT Int32.max — whose serialized
        //     length WOULD trap the FFI converter — is REFUSED by the definitive
        //     gate, so it never reaches `createExclusive*`. The largest ALLOWED
        //     payload is exactly the ceiling (serialized length == Int32.max);
        //     one byte more is refused.
        XCTAssertNil(
            AppState.importOverCeiling(
                metadataSize: nil, readByteCount: Int(int32Max - 4), refuseBytes: clamped),
            "a payload at the ceiling (serialized length == Int32.max) is allowed")
        XCTAssertEqual(
            AppState.importOverCeiling(
                metadataSize: nil, readByteCount: Int(int32Max - 3), refuseBytes: clamped),
            int32Max - 3,
            "one byte past the ceiling is refused before it can trap the converter")
        XCTAssertEqual(
            AppState.importOverCeiling(
                metadataSize: nil, readByteCount: Int(Int32.max), refuseBytes: clamped),
            int32Max,
            "an Int32.max-byte buffer is refused before it can trap the FFI converter")
    }

    /// #910 (Codex round 3): a ByInspection guard that `importEntry` threads the
    /// CLAMPED `importTransportCeiling(...)` result — never the raw
    /// `session.largeFileRefuseBytes()` — into ALL THREE size checks (preflight,
    /// bounded read, definitive gate). The pure-helper tests above only exercise
    /// pre-clamped values, so they would not catch a regression that passed the
    /// raw threshold to one of the three sites; this reads the source and fails
    /// if that happens.
    func testImportEntryThreadsClampedCeilingIntoAllThreeSizeChecks() throws {
        let appStateURL = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()  // SlateMacTests
            .deletingLastPathComponent()  // Tests
            .deletingLastPathComponent()  // slate-mac
            .appendingPathComponent("Sources/SlateMac/AppState.swift")
        let source = try String(contentsOf: appStateURL, encoding: .utf8)

        // Scope to importEntry's body (up to the first helper that follows it).
        guard let start = source.range(of: "func importEntry(externalURL"),
            let end = source.range(
                of: "nonisolated static func importTransportCeiling",
                range: start.upperBound..<source.endIndex)
        else {
            return XCTFail("could not locate importEntry in AppState.swift")
        }
        // Whitespace-normalize so the assertions survive line-wrapping.
        let flat = source[start.lowerBound..<end.lowerBound]
            .split(whereSeparator: { $0.isWhitespace }).joined(separator: " ")

        // The raw engine threshold is read exactly ONCE, and only to feed the
        // clamp — never handed to a size check directly.
        XCTAssertEqual(
            flat.components(separatedBy: "largeFileRefuseBytes()").count - 1, 1,
            "the raw threshold must be read once and immediately clamped")
        XCTAssertTrue(
            flat.contains("importTransportCeiling( refuseBytes: session.largeFileRefuseBytes())"),
            "the single raw-threshold read must feed importTransportCeiling")
        // All three size checks consume the CLAMPED ceiling.
        XCTAssertTrue(
            flat.contains("readImportBytes(from: externalURL, cap: ceiling)"),
            "the bounded read must cap at the clamped ceiling, not the raw threshold")
        XCTAssertEqual(
            flat.components(separatedBy: "refuseBytes: ceiling").count - 1, 2,
            "both the preflight and the definitive gate must pass the clamped ceiling")
    }

    // MARK: - C2 structural-busy drop admission

    func testC2BusyAtDropRejectsPrivatePayloadBeforeSessionEndOrProviderLoad()
        async throws
    {
        let (state, _) = try await makeVault(
            files: ["busy.md", "busy-destination/keep.md"])
        let gate = SuspensionGate()
        let busyTask = try await parkStructuralMutation(in: state, gate: gate)
        let provider = FileTreeSidebar.makeDragProvider(
            items: [
                .init(path: "a.md", isDirectory: false),
                .init(path: "b.md", isDirectory: false),
            ],
            originFileURL: URL(fileURLWithPath: "/Vaults/demo/b.md"))

        await assertBusyDropRejectsBeforeSessionEndOrProviderLoad(
            .privatePayload(provider), state: state, busyTask: busyTask, gate: gate)
    }

    func testC2BusyAtDropRejectsInVaultFileURLBeforeSessionEndOrProviderLoad()
        async throws
    {
        let (state, vault) = try await makeVault(
            files: ["a.md", "busy.md", "busy-destination/keep.md"])
        let gate = SuspensionGate()
        let busyTask = try await parkStructuralMutation(in: state, gate: gate)
        let provider = NSItemProvider()
        provider.registerDataRepresentation(
            forTypeIdentifier: FileTreeSidebar.fileURLUTType,
            visibility: .all
        ) { completion in
            completion(vault.appendingPathComponent("a.md").dataRepresentation, nil)
            return nil
        }

        await assertBusyDropRejectsBeforeSessionEndOrProviderLoad(
            .fileURL(provider), state: state, busyTask: busyTask, gate: gate)
    }

    func testC2BusyAtDropRejectsExternalFileURLBeforeSessionEndOrProviderLoad()
        async throws
    {
        let external = tempDir.appendingPathComponent("outside.md")
        try "# outside\n".write(to: external, atomically: true, encoding: .utf8)
        let (state, _) = try await makeVault(
            files: ["busy.md", "busy-destination/keep.md"])
        let gate = SuspensionGate()
        let busyTask = try await parkStructuralMutation(in: state, gate: gate)
        let provider = NSItemProvider()
        provider.registerDataRepresentation(
            forTypeIdentifier: FileTreeSidebar.fileURLUTType,
            visibility: .all
        ) { completion in
            completion(external.dataRepresentation, nil)
            return nil
        }

        await assertBusyDropRejectsBeforeSessionEndOrProviderLoad(
            .fileURL(provider), state: state, busyTask: busyTask, gate: gate)
    }

    func testC2UnsupportedProviderRemainsRejectedWithoutAdmissionAnnouncement() async throws {
        let (state, _) = try await makeVault(files: [])
        var performed = false
        let accepted = FileTreeSidebar.performAdmittedDrop(
            .none, appState: state
        ) { _ in
            performed = true
            return true
        }

        XCTAssertFalse(accepted)
        XCTAssertFalse(performed)
        XCTAssertNil(state.lastMutationAnnouncement)
    }

    func testC2BusyDropTargetPolicySuppressesRowWashRootRingAndSpringArming() {
        XCTAssertTrue(FileTreeSidebar.dropTargetIsActive(true, busy: false))
        XCTAssertFalse(FileTreeSidebar.dropTargetIsActive(false, busy: false))
        XCTAssertFalse(
            FileTreeSidebar.dropTargetIsActive(true, busy: true),
            "busy row/root targets must neither display acceptance nor arm spring-loading")
    }

    func testC2DelayedPrivateAndPublicCallbacksIgnoreReplacementSessionBeforeDispatch()
        async throws
    {
        let (state, vault) = try await makeVault(files: ["a.md", "dest/keep.md"])

        let privateProvider = NSItemProvider()
        var finishPrivate: ((Data?, Error?) -> Void)?
        let privateLoadStarted = expectation(description: "private load started")
        privateProvider.registerDataRepresentation(
            forTypeIdentifier: FileTreeSidebar.nodeUTType,
            visibility: .ownProcess
        ) { completion in
            finishPrivate = completion
            privateLoadStarted.fulfill()
            return nil
        }
        var privateDispatches = 0
        let privateStale = expectation(description: "private stale owner rejected")
        XCTAssertTrue(
            FileTreeSidebar.loadAdmittedDropProvider(
                .privatePayload(privateProvider),
                appState: state,
                onPrivate: { _, _ in privateDispatches += 1 },
                onFileURL: { _ in XCTFail("private flavor must not classify as a URL") },
                onStaleSession: { privateStale.fulfill() }))
        await fulfillment(of: [privateLoadStarted], timeout: 1)

        let replacement = tempDir.appendingPathComponent("replacement-private")
        try FileManager.default.createDirectory(
            at: replacement, withIntermediateDirectories: true)
        state.openVault(at: replacement)
        await state.scanTask?.value
        let privatePayload = try XCTUnwrap(
            FileTreeSidebar.encodeDragPayload([
                .init(path: "a.md", isDirectory: false),
            ]))
        try XCTUnwrap(finishPrivate)(privatePayload, nil)
        await fulfillment(of: [privateStale], timeout: 1)
        XCTAssertEqual(privateDispatches, 0)
        XCTAssertTrue(exists(vault, "a.md"), "the stale private callback cannot mutate vault A")

        let publicProvider = NSItemProvider()
        var finishPublic: ((Data?, Error?) -> Void)?
        let publicLoadStarted = expectation(description: "public load started")
        publicProvider.registerDataRepresentation(
            forTypeIdentifier: FileTreeSidebar.fileURLUTType,
            visibility: .all
        ) { completion in
            finishPublic = completion
            publicLoadStarted.fulfill()
            return nil
        }
        var publicDispatches = 0
        let publicStale = expectation(description: "public stale owner rejected")
        XCTAssertTrue(
            FileTreeSidebar.loadAdmittedDropProvider(
                .fileURL(publicProvider),
                appState: state,
                onPrivate: { _, _ in XCTFail("public flavor must not decode privately") },
                onFileURL: { _ in publicDispatches += 1 },
                onStaleSession: { publicStale.fulfill() }))
        await fulfillment(of: [publicLoadStarted], timeout: 1)

        let secondReplacement = tempDir.appendingPathComponent("replacement-public")
        try FileManager.default.createDirectory(
            at: secondReplacement, withIntermediateDirectories: true)
        state.openVault(at: secondReplacement)
        await state.scanTask?.value
        try XCTUnwrap(finishPublic)(vault.appendingPathComponent("a.md").dataRepresentation, nil)
        await fulfillment(of: [publicStale], timeout: 1)
        XCTAssertEqual(publicDispatches, 0)
        XCTAssertTrue(exists(vault, "a.md"), "the stale public callback cannot mutate vault A")
    }

    func testC2PrivateIllegalAndNoOpDropsAnnounceWithoutRequestOrSelectionChange()
        async throws
    {
        let (state, _) = try await makeVault(
            files: [
                "dest/a.md", "dest/b.md", "folder/child/keep.md",
            ])
        state.selectedFilePath = "dest/a.md"
        state.treeSelectedNode = .init(path: "dest/a.md", isDirectory: false)
        let originalFile = state.selectedFilePath
        let originalTreeNode = state.treeSelectedNode
        let probe = BatchMoveProbe(
            report: batchMoveReport(state: .noOp, planned: []))
        state.batchMoveRunner = { _, request in await probe.run(request) }

        let cases: [([FileTreeSidebar.DragPayloadItem], String, String)] = [
            (
                [.init(path: "dest/a.md", isDirectory: false)],
                "dest",
                "Nothing moved. The item is already in this folder."
            ),
            (
                [.init(path: "folder", isDirectory: true)],
                "folder",
                "Nothing moved. A folder can’t be moved into itself."
            ),
            (
                [.init(path: "folder", isDirectory: true)],
                "folder/child",
                "Nothing moved. A folder can’t be moved into itself."
            ),
            (
                [
                    .init(path: "dest/a.md", isDirectory: false),
                    .init(path: "dest/b.md", isDirectory: false),
                ],
                "dest",
                "Nothing moved. The selected items can’t be moved to this folder."
            ),
        ]

        for (items, destination, message) in cases {
            XCTAssertFalse(
                FileTreeSidebar.performDecodedPrivateDrop(
                    items,
                    preferredFocusPath: items.last?.path,
                    into: destination,
                    appState: state))
            XCTAssertEqual(state.lastMutationAnnouncement, message)
            XCTAssertEqual(state.selectedFilePath, originalFile)
            XCTAssertEqual(state.treeSelectedNode, originalTreeNode)
        }
        let nativeRequests = await probe.callCount()
        XCTAssertEqual(nativeRequests, 0, "all-invalid private drops never reach native batch move")
    }

    func testC2BusyDuringPrivateMultiDecodeRejectsOnceWithoutRunnerOrWrite()
        async throws
    {
        let (state, vault) = try await makeVault(
            files: [
                "a.md", "b.md", "busy.md", "dest/keep.md",
                "busy-destination/keep.md",
            ])
        let provider = FileTreeSidebar.makeDragProvider(
            items: [
                .init(path: "a.md", isDirectory: false),
                .init(path: "b.md", isDirectory: false),
            ],
            originFileURL: vault.appendingPathComponent("b.md"),
            preferredFocusPath: "b.md")
        var providerLoadCount = 0
        var decodedDispatch: (() -> Task<Void, Never>?)?
        XCTAssertTrue(
            FileTreeSidebar.performAdmittedDrop(
                .privatePayload(provider), appState: state
            ) { _ in
                providerLoadCount += 1
                decodedDispatch = {
                    state.moveTreeSelection(
                        [
                            .init(path: "a.md", isDirectory: false),
                            .init(path: "b.md", isDirectory: false),
                        ],
                        to: "dest",
                        preferredFocusPath: "b.md")
                }
                return true
            })
        XCTAssertEqual(providerLoadCount, 1, "the provider load was initially accepted")
        let gate = SuspensionGate()
        let busyTask = try await parkStructuralMutation(in: state, gate: gate)
        var announcements: [String] = []
        let cancellable = state.$lastMutationAnnouncement
            .dropFirst()
            .compactMap { $0 }
            .sink { announcements.append($0) }

        let rejected = try XCTUnwrap(decodedDispatch)()

        XCTAssertNil(rejected, "a decoded private payload must not start a second runner")
        XCTAssertEqual(
            state.lastMutationAnnouncement, AppState.structuralMutationBusyReason)
        XCTAssertEqual(
            announcements, [AppState.structuralMutationBusyReason],
            "the decode-race rejection is announced exactly once")
        let entrantCount = await gate.entrantCount()
        XCTAssertEqual(entrantCount, 1, "only the parked runner entered")
        XCTAssertTrue(exists(vault, "a.md"))
        XCTAssertTrue(exists(vault, "b.md"))
        XCTAssertFalse(exists(vault, "dest/a.md"))
        XCTAssertFalse(exists(vault, "dest/b.md"))

        withExtendedLifetime(cancellable) {}
        await gate.releaseOne()
        await busyTask.value
    }

    func testC2BusyDuringInVaultFileURLDecodeRejectsOnceWithoutMoveOrWrite()
        async throws
    {
        let (state, vault) = try await makeVault(
            files: [
                "a.md", "busy.md", "dest/keep.md", "busy-destination/keep.md",
            ])
        let provider = NSItemProvider()
        var providerLoadCount = 0
        var decodedDispatch: (() -> Task<Void, Never>?)?
        XCTAssertTrue(
            FileTreeSidebar.performAdmittedDrop(
                .fileURL(provider), appState: state
            ) { _ in
                providerLoadCount += 1
                decodedDispatch = {
                    state.handleFileURLDrop(
                        vault.appendingPathComponent("a.md"),
                        into: "dest")
                }
                return true
            })
        XCTAssertEqual(providerLoadCount, 1, "the provider load was initially accepted")
        let gate = SuspensionGate()
        let busyTask = try await parkStructuralMutation(in: state, gate: gate)
        var announcements: [String] = []
        let cancellable = state.$lastMutationAnnouncement
            .dropFirst()
            .compactMap { $0 }
            .sink { announcements.append($0) }

        let rejected = try XCTUnwrap(decodedDispatch)()

        XCTAssertNil(rejected, "a decoded in-vault URL must not start a move while busy")
        XCTAssertEqual(
            state.lastMutationAnnouncement, AppState.structuralMutationBusyReason)
        XCTAssertEqual(announcements, [AppState.structuralMutationBusyReason])
        let entrantCount = await gate.entrantCount()
        XCTAssertEqual(entrantCount, 1)
        XCTAssertTrue(exists(vault, "a.md"))
        XCTAssertFalse(exists(vault, "dest/a.md"))

        withExtendedLifetime(cancellable) {}
        await gate.releaseOne()
        await busyTask.value
    }

    func testC2BusyDuringExternalFileURLDecodeRejectsOnceWithoutImportOrWrite()
        async throws
    {
        let external = tempDir.appendingPathComponent("external.md")
        try "# external\n".write(to: external, atomically: true, encoding: .utf8)
        let (state, vault) = try await makeVault(
            files: ["busy.md", "busy-destination/keep.md"])
        let provider = NSItemProvider()
        var providerLoadCount = 0
        var decodedDispatch: (() -> Task<Void, Never>?)?
        XCTAssertTrue(
            FileTreeSidebar.performAdmittedDrop(
                .fileURL(provider), appState: state
            ) { _ in
                providerLoadCount += 1
                decodedDispatch = { state.handleFileURLDrop(external, into: "") }
                return true
            })
        XCTAssertEqual(providerLoadCount, 1, "the provider load was initially accepted")
        let gate = SuspensionGate()
        let busyTask = try await parkStructuralMutation(in: state, gate: gate)
        var announcements: [String] = []
        let cancellable = state.$lastMutationAnnouncement
            .dropFirst()
            .compactMap { $0 }
            .sink { announcements.append($0) }

        let rejected = try XCTUnwrap(decodedDispatch)()

        XCTAssertNil(rejected, "a decoded external URL must not start an import while busy")
        XCTAssertEqual(
            state.lastMutationAnnouncement, AppState.structuralMutationBusyReason)
        XCTAssertEqual(announcements, [AppState.structuralMutationBusyReason])
        let entrantCount = await gate.entrantCount()
        XCTAssertEqual(entrantCount, 1)
        XCTAssertFalse(exists(vault, "external.md"))

        withExtendedLifetime(cancellable) {}
        await gate.releaseOne()
        await busyTask.value
    }

    func testC2ExplicitMultiDropKeepsFolderAndDescendantInOneOrderedNativeRequest()
        async throws
    {
        let items = [
            batchItem("folder", dir: true),
            batchItem("folder/child.md"),
        ]
        let report = batchMoveReport(
            state: .rejected,
            planned: items,
            requiresRescan: true)
        let probe = BatchMoveProbe(report: report)
        let (state, _) = try await makeVault(
            files: ["folder/child.md", "dest/keep.md"])
        state.batchMoveRunner = { _, request in await probe.run(request) }
        state.structuralBatchRefreshRunner = { _ in }

        await state.moveTreeSelection(
            [
                AppState.TreeSelection(path: "folder", isDirectory: true),
                AppState.TreeSelection(path: "folder/child.md", isDirectory: false),
            ],
            to: "dest",
            preferredFocusPath: "folder/child.md")?.value

        let callCount = await probe.callCount()
        let request = await probe.lastRequest()
        XCTAssertEqual(callCount, 1)
        XCTAssertEqual(
            request?.items,
            items,
            "Swift must not erase core-owned CoveredBySelectedFolder skip facts")
        XCTAssertEqual(
            state.treeMutation?.preferredFocusPath,
            "folder/child.md",
            "the drag origin must survive the native batch landing")
    }

    func testC2DropSourceWiringRequiresEarlyAdmissionDecodeRecheckAndBusyFeedbackCleanup()
        throws
    {
        let sidebar = try Self.source("FileTreeSidebar.swift")
        let appState = try Self.source("AppState.swift")

        XCTAssertTrue(
            sidebar.contains("Self.loadAdmittedDropProvider("),
            "the instance handler needs a behavior-testable admission coordinator")
        XCTAssertTrue(
            sidebar.contains("appState.admitStructuralDropRequest()"),
            "supported providers must re-use the exact shared admission reason")
        XCTAssertTrue(
            sidebar.contains("Self.dropTargetIsActive("),
            "row/root targeting and spring timers need one busy-aware policy")
        XCTAssertTrue(
            sidebar.contains(".onChange(of: appState.isMutatingStructure)"),
            "a mutation beginning mid-hover must cancel stale target/spring state")
        XCTAssertTrue(
            sidebar.contains("guard appState.currentSession === capturedSession"),
            "both delayed provider callbacks must retain the admitted session identity")
        XCTAssertTrue(
            sidebar.contains("preferredFocusPath: preferredFocusPath"),
            "the decoded private origin must reach the native batch landing")
        XCTAssertTrue(
            sidebar.contains("appState.moveTreeSelection("),
            "private decode must re-enter an admission-aware AppState funnel")
        XCTAssertTrue(
            sidebar.contains("appState.handleFileURLDrop("),
            "both decoded file-URL branches need one admission-aware AppState funnel")
        XCTAssertTrue(
            appState.contains("func admitStructuralDropRequest() -> Bool"))
        XCTAssertTrue(
            appState.contains("func handleFileURLDrop("))
    }

    // MARK: - In-vault file-URL drop → move end-to-end

    func testInVaultFileURLDropMovesOnDisk() async throws {
        let (state, vault) = try await makeVault(files: ["a.md", "dest/x.md"])
        // A vault file dragged in from Finder arrives as a file URL.
        let inVaultURL = vault.appendingPathComponent("a.md")

        let action = AppState.fileURLDropAction(
            url: inVaultURL, vaultURL: vault, destinationFolder: "dest", isDirectory: false)
        guard case .move(let path, let isDir, let dest) = action else {
            return XCTFail("an in-vault file URL must resolve to a move, got \(action)")
        }
        await state.moveEntry(path: path, isDirectory: isDir, to: dest)?.value

        XCTAssertTrue(exists(vault, "dest/a.md"), "the in-vault drop moved the file")
        XCTAssertFalse(exists(vault, "a.md"))
        // And — being a move — it is undoable (#871 integration).
        XCTAssertEqual(state.structuralUndoStack.count, 1)
    }
}
