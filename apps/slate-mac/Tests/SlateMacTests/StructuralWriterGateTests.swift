// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// FL-03 writer census: creation funnels that bypass the ordinary tree
/// mutation entry point still share its admission, ownership, and refresh.
@MainActor
final class StructuralWriterGateTests: XCTestCase {
    private var tempDirs: [URL] = []

    private static let sourceRoot = URL(fileURLWithPath: #filePath)
        .deletingLastPathComponent()
        .deletingLastPathComponent()
        .deletingLastPathComponent()
        .appendingPathComponent("Sources/SlateMac")

    override func tearDown() {
        for dir in tempDirs {
            try? FileManager.default.removeItem(at: dir)
        }
        tempDirs = []
        super.tearDown()
    }

    private actor SuspensionGate {
        private var entered = false
        private var entranceWaiters: [CheckedContinuation<Void, Never>] = []
        private var releaseWaiter: CheckedContinuation<Void, Never>?

        func enter() async {
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
            releaseWaiter?.resume()
            releaseWaiter = nil
        }
    }

    private actor Counter {
        private var value = 0

        func increment() { value += 1 }
        func count() -> Int { value }
    }

    private final class ThreadRecorder: @unchecked Sendable {
        private let lock = NSLock()
        private var storage: [Bool] = []

        func append(_ ranOnMainThread: Bool) {
            lock.lock()
            storage.append(ranOnMainThread)
            lock.unlock()
        }

        func values() -> [Bool] {
            lock.lock()
            defer { lock.unlock() }
            return storage
        }
    }

    private final class BlockingNativeGate: @unchecked Sendable {
        private let condition = NSCondition()
        private var enteredStorage = false
        private var released = false

        func run<T>(_ work: () -> T) -> T {
            condition.lock()
            enteredStorage = true
            condition.broadcast()
            while !released { condition.wait() }
            condition.unlock()
            return work()
        }

        var entered: Bool {
            condition.lock()
            defer { condition.unlock() }
            return enteredStorage
        }

        func release() {
            condition.lock()
            released = true
            condition.broadcast()
            condition.unlock()
        }
    }

    private static let minimalSavedQueryJSON = #"{"source":{"Folder":"Notes"},"row_source":"Files","filters":null,"formulas":[],"custom_summaries":[],"group_by":null,"sort":[],"columns":[{"id":"file.name","display_name":null}],"summaries":[],"limit":null,"view":{"Table":{"fallback_from":null}}}"#

    private func makeVault(files: [String: String]) async throws -> (AppState, URL) {
        let dir = try makeVaultDirectory(files: files)
        let state = AppState(
            recentsStore: nil,
            externalOpener: { _ in true },
            preferencesStore: PreferencesStore(
                defaults: UserDefaults(suiteName: UUID().uuidString)!))
        state.openVault(at: dir)
        await state.scanTask?.value
        return (state, dir)
    }

    private func makeVaultDirectory(files: [String: String]) throws -> URL {
        let dir = FileManager.default.temporaryDirectory
            .appendingPathComponent("structural-writers-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        tempDirs.append(dir)
        for (path, contents) in files {
            let url = dir.appendingPathComponent(path)
            try FileManager.default.createDirectory(
                at: url.deletingLastPathComponent(), withIntermediateDirectories: true)
            try contents.write(to: url, atomically: true, encoding: .utf8)
        }
        return dir
    }

    private func source(_ relativePath: String) throws -> String {
        try String(
            contentsOf: Self.sourceRoot.appendingPathComponent(relativePath),
            encoding: .utf8)
    }

    private func eventually(
        timeout: TimeInterval = 1,
        _ condition: () -> Bool
    ) async -> Bool {
        let deadline = Date().addingTimeInterval(timeout)
        while Date() < deadline {
            if condition() { return true }
            try? await Task.sleep(nanoseconds: 10_000_000)
        }
        return condition()
    }

    private func functionBody(_ name: String, in source: String) -> Substring? {
        let stripped = SwiftSourceStripping.strippingCommentsAndStrings(source)
        guard let declaration = stripped.range(of: "func \(name)") else { return nil }
        guard
            let open = stripped.range(
                of: "{", range: declaration.upperBound..<stripped.endIndex)
        else { return nil }
        var depth = 0
        var cursor = open.lowerBound
        while cursor < stripped.endIndex {
            switch stripped[cursor] {
            case "{": depth += 1
            case "}":
                depth -= 1
                if depth == 0 {
                    let end = stripped.index(after: cursor)
                    return source[open.lowerBound..<end]
                }
            default: break
            }
            cursor = stripped.index(after: cursor)
        }
        return nil
    }

    private func blockingMove(on state: AppState) async throws
        -> (task: Task<Void, Never>, gate: SuspensionGate)
    {
        let gate = SuspensionGate()
        let item = StructuralBatchItem(path: "blocker.md", isDirectory: false)
        let report = BatchMoveReport(
            envelope: StructuralBatchEnvelope(
                planned: [item], skipped: [], preflightFailures: []),
            state: .succeeded,
            opId: 404,
            standing: [
                BatchPathChange(
                    oldPath: "blocker.md", newPath: "dest/blocker.md",
                    isDirectory: false)
            ],
            rolledBack: [],
            failure: nil,
            rollbackFailures: [],
            rewritten: [],
            rewriteFailures: [],
            requiresRescan: false)
        state.batchMoveRunner = { _, _ in
            await gate.enter()
            return report
        }
        state.structuralBatchRefreshRunner = { _ in }
        let selection = AppState.TreeSelection(
            path: item.path, isDirectory: item.isDirectory)
        let task = try XCTUnwrap(
            state.batchMove(
                [selection], to: "dest", preferredFocusPath: "blocker.md"))
        await gate.waitUntilEntered()
        XCTAssertTrue(state.isMutatingStructure)

        return (task, gate)
    }

    func testBusyBatchRejectsCanvasConvertBeforeCreatingOrRetargeting() async throws {
        let canvas = #"{"nodes":[{"id":"b","type":"text","text":"Beta","x":0,"y":0,"width":240,"height":120}],"edges":[]}"#
        let (state, vault) = try await makeVault(files: [
            "board.canvas": canvas,
            "blocker.md": "# blocker\n",
            "dest/keep.md": "# keep\n",
        ])
        state.openFile("board.canvas", target: .currentTab)
        let document = try XCTUnwrap(state.activeCanvasDocument)
        let blocker = try await blockingMove(on: state)

        state.canvasConvertToNote(nodeId: "b", path: "Beta.md")

        XCTAssertFalse(
            FileManager.default.fileExists(
                atPath: vault.appendingPathComponent("Beta.md").path))
        XCTAssertEqual(document.outline.first { $0.nodeId == "b" }?.kind, "text")
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            AppState.structuralMutationBusyReason)

        await blocker.gate.release()
        await blocker.task.value
    }

    func testCanvasConvertOwnsGateThroughSharedRefreshAndThenRetargets() async throws {
        let canvas = #"{"nodes":[{"id":"b","type":"text","text":"Beta","x":0,"y":0,"width":240,"height":120}],"edges":[]}"#
        let (state, vault) = try await makeVault(files: ["board.canvas": canvas])
        state.openFile("board.canvas", target: .currentTab)
        let document = try XCTUnwrap(state.activeCanvasDocument)
        let refresh = SuspensionGate()
        state.structuralBatchRefreshRunner = { _ in await refresh.enter() }

        state.canvasConvertToNote(nodeId: "b", path: "Beta.md")
        let task = try XCTUnwrap(state.pendingStructuralTaskForTesting)
        await refresh.waitUntilEntered()

        XCTAssertTrue(
            FileManager.default.fileExists(
                atPath: vault.appendingPathComponent("Beta.md").path),
            "native creation completes before the shared tree refresh")
        XCTAssertTrue(state.isMutatingStructure)
        XCTAssertEqual(
            document.outline.first { $0.nodeId == "b" }?.kind, "text",
            "Swift document state must not land until the refresh suspension returns")

        state.canvasConvertToNote(nodeId: "b", path: "Must Wait.md")
        XCTAssertFalse(
            FileManager.default.fileExists(
                atPath: vault.appendingPathComponent("Must Wait.md").path))
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            AppState.structuralMutationBusyReason)

        await refresh.release()
        await task.value

        XCTAssertEqual(document.outline.first { $0.nodeId == "b" }?.kind, "file")
        XCTAssertEqual(document.target(of: "b"), "Beta.md")
        XCTAssertFalse(state.isMutatingStructure)
    }

    func testCanvasConvertCommandAndPromptRejectBeforeStagingWhileBusy() async throws {
        let canvas = #"{"nodes":[{"id":"b","type":"text","text":"Beta","x":0,"y":0,"width":240,"height":120}],"edges":[]}"#
        let (state, _) = try await makeVault(files: [
            "board.canvas": canvas,
            "blocker.md": "# blocker\n",
            "dest/keep.md": "# keep\n",
        ])
        state.openFile("board.canvas", target: .currentTab)
        let document = try XCTUnwrap(state.activeCanvasDocument)
        state.canvasSelect(nodeId: "b", in: document, announce: false)
        let blocker = try await blockingMove(on: state)

        XCTAssertTrue(
            SlateCommandID.structuralMutationCommands.contains(
                SlateCommandID.canvasConvertToNote))
        XCTAssertThrowsError(
            try state.commandRegistry.invokeById(
                id: SlateCommandID.canvasConvertToNote)
        ) { error in
            XCTAssertEqual(
                error as? CommandError,
                .ActionFailed(message: AppState.structuralMutationBusyReason))
        }
        state.canvasPromptConvertToNote()
        XCTAssertNil(state.canvasPrompt)
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            AppState.structuralMutationBusyReason)

        await blocker.gate.release()
        await blocker.task.value
    }

    func testBaseExportPaletteCommandsRejectThroughStructuralRegistryWhileBusy()
        async throws
    {
        let (state, _) = try await makeVault(files: [
            "blocker.md": "# blocker\n",
            "dest/keep.md": "# keep\n",
        ])
        let blocker = try await blockingMove(on: state)

        for id in [SlateCommandID.basesExportCSV, SlateCommandID.basesExportMarkdown] {
            XCTAssertTrue(SlateCommandID.structuralMutationCommands.contains(id))
            XCTAssertThrowsError(try state.commandRegistry.invokeById(id: id)) { error in
                XCTAssertEqual(
                    error as? CommandError,
                    .ActionFailed(message: AppState.structuralMutationBusyReason))
            }
        }

        await blocker.gate.release()
        await blocker.task.value
    }

    func testCanvasConvertPromptAndContextExposeBusyReasonAndKeepRejectedPrompt() throws {
        let prompt = try source("Canvas/CanvasPromptSheet.swift")
        XCTAssertTrue(prompt.contains("if appState.canvasConvertToNote"))
        XCTAssertTrue(prompt.contains("appState.structuralMutationDisabledReason"))
        XCTAssertTrue(
            prompt.contains(".disabled(disabledReason != nil || mutationDisabledReason != nil)"))
        XCTAssertTrue(prompt.contains("Text(disabledReason)"))

        let outline = try source("Canvas/CanvasOutlineView.swift")
        XCTAssertTrue(outline.contains("appState.structuralMutationDisabledReason"))
        XCTAssertTrue(outline.contains(".disabled(convertDisabledReason != nil)"))
        XCTAssertTrue(outline.contains("Text(convertDisabledReason)"))
    }

    func testBusyBatchRejectsSavedQueryExportAndBuilderSaveBeforeWrite() async throws {
        let (state, vault) = try await makeVault(files: [
            "blocker.md": "# blocker\n",
            "dest/keep.md": "# keep\n",
        ])
        let session = try XCTUnwrap(state.currentSession)
        let queryID = try session.saveQuery(
            name: "All Files",
            description: nil,
            queryJson: Self.minimalSavedQueryJSON,
            sourceSyntax: .builder)
        state.activeBaseQueryBuilder = BaseQueryBuilderModel()
        let blocker = try await blockingMove(on: state)

        state.exportSavedQuery(id: queryID, path: "Saved.base")
        state.basesBuilderSaveAsBase(path: "Builder.base")

        XCTAssertFalse(
            FileManager.default.fileExists(
                atPath: vault.appendingPathComponent("Saved.base").path))
        XCTAssertFalse(
            FileManager.default.fileExists(
                atPath: vault.appendingPathComponent("Builder.base").path))
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            AppState.structuralMutationBusyReason)

        await blocker.gate.release()
        await blocker.task.value
    }

    func testSavedQueryExportAndBuilderSaveUseTrackedGateAndSharedRefresh() async throws {
        let (state, vault) = try await makeVault(files: [:])
        let session = try XCTUnwrap(state.currentSession)
        let queryID = try session.saveQuery(
            name: "All Files",
            description: nil,
            queryJson: Self.minimalSavedQueryJSON,
            sourceSyntax: .builder)
        state.activeBaseQueryBuilder = BaseQueryBuilderModel()
        let refreshes = Counter()
        state.structuralBatchRefreshRunner = { _ in await refreshes.increment() }

        state.exportSavedQuery(id: queryID, path: "Saved.base")
        let exportTask = try XCTUnwrap(state.pendingStructuralTaskForTesting)
        XCTAssertTrue(state.isMutatingStructure)
        await exportTask.value

        XCTAssertTrue(
            FileManager.default.fileExists(
                atPath: vault.appendingPathComponent("Saved.base").path))
        XCTAssertFalse(state.isMutatingStructure)

        state.basesBuilderSaveAsBase(path: "Builder.base")
        let builderTask = try XCTUnwrap(state.pendingStructuralTaskForTesting)
        XCTAssertTrue(state.isMutatingStructure)
        await builderTask.value

        XCTAssertTrue(
            FileManager.default.fileExists(
                atPath: vault.appendingPathComponent("Builder.base").path))
        let refreshCount = await refreshes.count()
        XCTAssertEqual(refreshCount, 2)
        XCTAssertFalse(state.isMutatingStructure)
    }

    func testSavedQueryExportRefreshesOverwrittenOpenBaseBeforeReleasingOwner()
        async throws
    {
        let existingBase = #"""
            views:
              - type: table
                name: Old
                filters: 'file.inFolder("Old")'
                order: [file.name]
            """#
        let (state, _) = try await makeVault(files: [
            "Old/Before.md": "# Before\n",
            "Notes/After.md": "# After\n",
            "Saved.base": existingBase,
        ])
        let session = try XCTUnwrap(state.currentSession)
        let queryID = try session.saveQuery(
            name: "Notes",
            description: nil,
            queryJson: Self.minimalSavedQueryJSON,
            sourceSyntax: .builder)
        state.openFile("Saved.base", target: .currentTab)
        let document = try XCTUnwrap(state.activeBaseDocument)
        let oldHandle = try XCTUnwrap(document.handle)
        XCTAssertEqual(document.result?.rows.map(\.filePath), ["Old/Before.md"])

        await state.exportSavedQuery(id: queryID, path: "Saved.base")?.value

        XCTAssertNotEqual(document.handle, oldHandle)
        XCTAssertEqual(document.result?.rows.map(\.filePath), ["Notes/After.md"])
        XCTAssertFalse(state.isMutatingStructure)
    }

    func testBuilderSaveNormalizesExtensionlessPathBeforeWritingAndRefreshing() async throws {
        let (state, vault) = try await makeVault(files: [
            "Queries/keep.md": "# keep\n",
        ])
        state.activeBaseQueryBuilder = BaseQueryBuilderModel()
        let refreshes = Counter()
        state.structuralBatchRefreshRunner = { _ in await refreshes.increment() }

        try await XCTUnwrap(
            state.basesBuilderSaveAsBase(path: "  Queries/New Query  ")
        ).value

        XCTAssertFalse(
            FileManager.default.fileExists(
                atPath: vault.appendingPathComponent("Queries/New Query").path),
            "the unnormalized extensionless path must never be written")
        XCTAssertTrue(
            FileManager.default.fileExists(
                atPath: vault.appendingPathComponent("Queries/New Query.base").path),
            "an extensionless builder destination gains .base")
        let refreshCount = await refreshes.count()
        XCTAssertEqual(refreshCount, 1)
        XCTAssertEqual(state.lastMutationAnnouncement, "Saved query as Queries/New Query.base.")
    }

    func testBuilderSaveRejectsWrongExtensionBeforeAdmissionOrNativeWrite() async throws {
        let (state, vault) = try await makeVault(files: [:])
        let model = BaseQueryBuilderModel()
        state.activeBaseQueryBuilder = model
        let refreshes = Counter()
        state.structuralBatchRefreshRunner = { _ in await refreshes.increment() }

        let task = state.basesBuilderSaveAsBase(path: "Queries/New Query.md")

        XCTAssertNil(task)
        XCTAssertFalse(state.isMutatingStructure)
        XCTAssertFalse(
            FileManager.default.fileExists(
                atPath: vault.appendingPathComponent("Queries/New Query.md").path))
        let refreshCount = await refreshes.count()
        XCTAssertEqual(refreshCount, 0)
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            "Base paths must end in .base.")
        XCTAssertEqual(state.baseQueryBuilderSaveError, "Base paths must end in .base.")
        XCTAssertTrue(state.activeBaseQueryBuilder === model)
    }

    func testBuilderSaveCollisionPreservesOccupantAndSkipsRefresh() async throws {
        let occupant = "views:\n  - type: table\n    name: Existing\n"
        let (state, vault) = try await makeVault(files: ["Builder.base": occupant])
        let model = BaseQueryBuilderModel()
        state.activeBaseQueryBuilder = model
        let refreshes = Counter()
        state.structuralBatchRefreshRunner = { _ in await refreshes.increment() }

        try await XCTUnwrap(
            state.basesBuilderSaveAsBase(path: "Builder.base")
        ).value

        XCTAssertEqual(
            try String(
                contentsOf: vault.appendingPathComponent("Builder.base"),
                encoding: .utf8),
            occupant,
            "exclusive builder save must not replace an existing Base")
        let refreshCount = await refreshes.count()
        XCTAssertEqual(refreshCount, 0)
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            "A file already exists at Builder.base. Choose a different Base path.")
        XCTAssertEqual(
            state.baseQueryBuilderSaveError,
            "A file already exists at Builder.base. Choose a different Base path.")
        XCTAssertTrue(state.activeBaseQueryBuilder === model)
    }

    func testBuilderSaveFailureStaysVisibleAndAReplacementBuilderClearsIt() async throws {
        let (state, _) = try await makeVault(files: [:])
        let model = BaseQueryBuilderModel()
        state.activeBaseQueryBuilder = model

        XCTAssertNil(
            state.basesBuilderSaveAsBase(path: "../Query.base"),
            "invalid vault-relative paths fail before native I/O")

        let message = try XCTUnwrap(state.baseQueryBuilderSaveError)
        XCTAssertEqual(
            message,
            "Choose a vault-relative path without parent-directory references.")
        XCTAssertEqual(state.lastMutationAnnouncement, message)
        XCTAssertTrue(state.activeBaseQueryBuilder === model)

        let replacement = BaseQueryBuilderModel()
        state.activeBaseQueryBuilder = replacement
        XCTAssertNil(state.baseQueryBuilderSaveError)
        XCTAssertTrue(state.activeBaseQueryBuilder === replacement)
    }

    func testBuilderSheetShowsPathErrorBesideFieldAndClearsItWhenPathChanges() throws {
        let builder = try source("Bases/BaseQueryBuilderSheet.swift")
        let start = try XCTUnwrap(builder.range(of: "private var footer"))
        let end = try XCTUnwrap(
            builder.range(of: "private func hydrateSavedQueryFields", range: start.upperBound..<builder.endIndex))
        let body = String(builder[start.lowerBound..<end.lowerBound])

        XCTAssertTrue(body.contains("appState.baseQueryBuilderSaveError"), body)
        XCTAssertTrue(body.contains("Tokens.ColorRole.destructiveText"), body)
        XCTAssertTrue(body.contains("Base path error"), body)
        XCTAssertTrue(body.contains("onChange(of: saveAsBasePath)"), body)
        XCTAssertTrue(body.contains("clearBaseQueryBuilderSaveError"), body)
    }

    func testBuilderSaveKeepsStructuralOwnerThroughSuspendedVisibleRefresh() async throws {
        let base = #"""
            views:
              - type: table
                name: All
                filters: 'file.inFolder("Notes")'
                order: [file.name]
            """#
        let (state, vault) = try await makeVault(files: [
            "Notes/Alpha.md": "# Alpha\n",
            "Queries/Existing.base": base,
        ])
        state.openFile("Queries/Existing.base", target: .currentTab)
        state.activeBaseQueryBuilder = BaseQueryBuilderModel()
        let gate = BlockingNativeGate()
        state.baseRetargetPreloadRunner = { session, request, observer in
            gate.run {
                BasePreparedLoader.prepare(
                    session: session, request: request, observer: observer)
            }
        }

        let task = try XCTUnwrap(
            state.basesBuilderSaveAsBase(path: "Queries/Builder.base"))
        guard await eventually({ gate.entered }) else {
            return XCTFail("builder refresh never entered the native suspension gate")
        }

        XCTAssertTrue(state.isMutatingStructure)
        XCTAssertNil(state.basesBuilderSaveAsBase(path: "Queries/Must Wait.base"))
        XCTAssertFalse(
            FileManager.default.fileExists(
                atPath: vault.appendingPathComponent("Queries/Must Wait.base").path))
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            AppState.structuralMutationBusyReason)

        gate.release()
        await task.value

        XCTAssertFalse(state.isMutatingStructure)
        XCTAssertTrue(
            FileManager.default.fileExists(
                atPath: vault.appendingPathComponent("Queries/Builder.base").path))
        XCTAssertNil(state.baseQueryBuilderSaveError)
    }

    func testSavedQueryExportStaleRefreshCannotLandOrReleaseNewVaultOwner() async throws {
        let (state, vaultA) = try await makeVault(files: [:])
        let sessionA = try XCTUnwrap(state.currentSession)
        let queryID = try sessionA.saveQuery(
            name: "All Files",
            description: nil,
            queryJson: Self.minimalSavedQueryJSON,
            sourceSyntax: .builder)
        let vaultB = try makeVaultDirectory(files: [
            "blocker.md": "# blocker\n",
            "dest/keep.md": "# keep\n",
        ])
        let oldRefresh = SuspensionGate()
        state.structuralBatchRefreshRunner = { _ in await oldRefresh.enter() }
        let oldExport = try XCTUnwrap(
            state.exportSavedQuery(id: queryID, path: "Saved.base"))
        await oldRefresh.waitUntilEntered()
        XCTAssertTrue(
            FileManager.default.fileExists(
                atPath: vaultA.appendingPathComponent("Saved.base").path))

        state.openVault(at: vaultB)
        await state.scanTask?.value
        state.lastBaseActionAnnouncement = nil
        let newOwner = try await blockingMove(on: state)

        await oldRefresh.release()
        await oldExport.value

        XCTAssertTrue(state.isMutatingStructure)
        XCTAssertNil(state.lastBaseActionAnnouncement)
        XCTAssertFalse(
            FileManager.default.fileExists(
                atPath: vaultB.appendingPathComponent("Saved.base").path))

        await newOwner.gate.release()
        await newOwner.task.value
        XCTAssertFalse(state.isMutatingStructure)
    }

    func testBuilderSaveStaleRefreshCannotLandOrReleaseNewVaultOwner() async throws {
        let (state, vaultA) = try await makeVault(files: [:])
        state.activeBaseQueryBuilder = BaseQueryBuilderModel()
        let vaultB = try makeVaultDirectory(files: [
            "blocker.md": "# blocker\n",
            "dest/keep.md": "# keep\n",
        ])
        let oldRefresh = SuspensionGate()
        state.structuralBatchRefreshRunner = { _ in await oldRefresh.enter() }
        let oldSave = try XCTUnwrap(
            state.basesBuilderSaveAsBase(path: "Builder.base"))
        await oldRefresh.waitUntilEntered()
        XCTAssertTrue(
            FileManager.default.fileExists(
                atPath: vaultA.appendingPathComponent("Builder.base").path))

        state.openVault(at: vaultB)
        await state.scanTask?.value
        let newOwner = try await blockingMove(on: state)

        await oldRefresh.release()
        await oldSave.value

        XCTAssertTrue(state.isMutatingStructure)
        XCTAssertNil(state.lastMutationAnnouncement)
        XCTAssertFalse(
            FileManager.default.fileExists(
                atPath: vaultB.appendingPathComponent("Builder.base").path))

        await newOwner.gate.release()
        await newOwner.task.value
        XCTAssertFalse(state.isMutatingStructure)
    }

    func testCanvasConvertStaleRefreshCannotLandOrReleaseNewVaultOwner() async throws {
        let canvas = #"{"nodes":[{"id":"b","type":"text","text":"Beta","x":0,"y":0,"width":240,"height":120}],"edges":[]}"#
        let (state, vaultA) = try await makeVault(files: ["board.canvas": canvas])
        let vaultB = try makeVaultDirectory(files: [
            "blocker.md": "# blocker\n",
            "dest/keep.md": "# keep\n",
        ])
        var canvasMessages: [String] = []
        state.canvasAnnouncer = CanvasAnnouncer(
            verbosity: .standard, coalesceWindow: 60
        ) { message, _ in
            canvasMessages.append(message)
        }
        state.openFile("board.canvas", target: .currentTab)
        let oldDocument = try XCTUnwrap(state.activeCanvasDocument)
        let oldRefresh = SuspensionGate()
        state.structuralBatchRefreshRunner = { _ in await oldRefresh.enter() }
        let oldConvert = try XCTUnwrap(
            state.canvasConvertToNote(nodeId: "b", path: "Beta.md"))
        await oldRefresh.waitUntilEntered()
        XCTAssertTrue(
            FileManager.default.fileExists(
                atPath: vaultA.appendingPathComponent("Beta.md").path))

        state.openVault(at: vaultB)
        await state.scanTask?.value
        let newOwner = try await blockingMove(on: state)

        await oldRefresh.release()
        await oldConvert.value

        XCTAssertTrue(state.isMutatingStructure)
        XCTAssertEqual(
            oldDocument.outline.first { $0.nodeId == "b" }?.kind,
            "text",
            "vault A's stale completion must not land Swift canvas state")
        XCTAssertFalse(
            FileManager.default.fileExists(
                atPath: vaultB.appendingPathComponent("Beta.md").path))
        state.canvasAnnouncer.flushForTests()
        XCTAssertFalse(canvasMessages.contains { $0.contains("Converted to note") })

        await newOwner.gate.release()
        await newOwner.task.value
        XCTAssertFalse(state.isMutatingStructure)
    }

    func testSavePanelWriterRejectsWhileBusyBeforeTouchingDestination() async throws {
        let (state, vault) = try await makeVault(files: [
            "blocker.md": "# blocker\n",
            "dest/keep.md": "# keep\n",
        ])
        let session = try XCTUnwrap(state.currentSession)
        let blocker = try await blockingMove(on: state)
        let destination = vault.appendingPathComponent("Export.csv")

        XCTAssertNil(
            state.performBaseSavePanelWrite(
                text: "name\nAlpha\n",
                to: destination,
                originSession: session,
                successMessage: "Exported base view.",
                failurePrefix: "Base view could not be exported"))
        XCTAssertFalse(FileManager.default.fileExists(atPath: destination.path))
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            AppState.structuralMutationBusyReason)

        await blocker.gate.release()
        await blocker.task.value
    }

    func testSavePanelWriterRefreshesAndBarriersOnlyForNewVaultPaths() async throws {
        let (state, vault) = try await makeVault(files: [
            "b.md": "# b\n",
            "dest/keep.md": "# keep\n",
            "Existing.csv": "old\n",
        ])
        let session = try XCTUnwrap(state.currentSession)
        let refreshes = Counter()
        state.structuralBatchRefreshRunner = { _ in await refreshes.increment() }

        await state.moveEntry(
            path: "b.md", isDirectory: false, to: "dest")?.value
        XCTAssertEqual(state.structuralUndoStack.count, 1)
        let created = try XCTUnwrap(
            state.performBaseSavePanelWrite(
                text: "name\nAlpha\n",
                to: vault.appendingPathComponent("Created.csv"),
                originSession: session,
                successMessage: "Exported base view.",
                failurePrefix: "Base view could not be exported"))
        await created.value
        XCTAssertTrue(state.structuralUndoStack.isEmpty)
        let afterCreateRefreshes = await refreshes.count()
        XCTAssertEqual(afterCreateRefreshes, 1)

        await state.moveEntry(
            path: "dest/b.md", isDirectory: false, to: "")?.value
        XCTAssertEqual(state.structuralUndoStack.count, 1)
        let overwritten = try XCTUnwrap(
            state.performBaseSavePanelWrite(
                text: "new\n",
                to: vault.appendingPathComponent("Existing.csv"),
                originSession: session,
                successMessage: "Exported base view.",
                failurePrefix: "Base view could not be exported"))
        await overwritten.value
        XCTAssertEqual(
            state.structuralUndoStack.count, 1,
            "an in-vault overwrite must retain unrelated structural undo")
        let afterOverwriteRefreshes = await refreshes.count()
        XCTAssertEqual(afterOverwriteRefreshes, 2)

        let outsideDirectory = try makeVaultDirectory(files: [:])
        let outside = outsideDirectory.appendingPathComponent("Outside.csv")
        let external = try XCTUnwrap(
            state.performBaseSavePanelWrite(
                text: "external\n",
                to: outside,
                originSession: session,
                successMessage: "Exported base view.",
                failurePrefix: "Base view could not be exported"))
        await external.value
        XCTAssertEqual(
            state.structuralUndoStack.count, 1,
            "an external destination must not mutate vault undo history")
        let afterExternalRefreshes = await refreshes.count()
        XCTAssertEqual(
            afterExternalRefreshes, 2,
            "an external write does not need to refresh the vault tree")
        XCTAssertEqual(
            try String(contentsOf: outside, encoding: .utf8), "external\n")
    }

    func testInVaultSavePanelWriteRefreshesVisibleBaseBeforeReleasingOwner()
        async throws
    {
        let base = #"""
            views:
              - type: table
                name: Notes
                filters: 'file.inFolder("Notes")'
                order: [file.name]
            """#
        let (state, vault) = try await makeVault(files: [
            "Notes/Alpha.md": "# Alpha\n",
            "Queries/Notes.base": base,
        ])
        let session = try XCTUnwrap(state.currentSession)
        state.openFile("Queries/Notes.base", target: .currentTab)
        let document = try XCTUnwrap(state.activeBaseDocument)
        let oldHandle = try XCTUnwrap(document.handle)
        XCTAssertEqual(document.result?.rows.map(\.filePath), ["Notes/Alpha.md"])

        await state.performBaseSavePanelWrite(
            text: "# Beta\n",
            to: vault.appendingPathComponent("Notes/Beta.md"),
            originSession: session,
            successMessage: "Exported note.",
            failurePrefix: "Note could not be exported")?.value

        XCTAssertNotEqual(document.handle, oldHandle)
        XCTAssertEqual(
            document.result?.rows.map(\.filePath),
            ["Notes/Alpha.md", "Notes/Beta.md"])
        XCTAssertFalse(state.isMutatingStructure)
    }

    func testSavePanelWriterStaleRefreshCannotLandOrReleaseNewVaultOwner() async throws {
        let (state, vaultA) = try await makeVault(files: [:])
        let sessionA = try XCTUnwrap(state.currentSession)
        let vaultB = try makeVaultDirectory(files: [
            "blocker.md": "# blocker\n",
            "dest/keep.md": "# keep\n",
        ])
        let oldRefresh = SuspensionGate()
        state.structuralBatchRefreshRunner = { _ in await oldRefresh.enter() }
        let oldWrite = try XCTUnwrap(
            state.performBaseSavePanelWrite(
                text: "from A\n",
                to: vaultA.appendingPathComponent("Export.csv"),
                originSession: sessionA,
                successMessage: "Exported base view.",
                failurePrefix: "Base view could not be exported"))
        await oldRefresh.waitUntilEntered()

        state.openVault(at: vaultB)
        await state.scanTask?.value
        state.lastBaseActionAnnouncement = nil
        let newOwner = try await blockingMove(on: state)

        await oldRefresh.release()
        await oldWrite.value

        XCTAssertTrue(state.isMutatingStructure)
        XCTAssertNil(
            state.lastBaseActionAnnouncement,
            "vault A's stale save-panel completion must not announce in vault B")
        XCTAssertFalse(
            FileManager.default.fileExists(
                atPath: vaultB.appendingPathComponent("Export.csv").path))

        await newOwner.gate.release()
        await newOwner.task.value
        XCTAssertFalse(state.isMutatingStructure)
    }

    func testSavePanelOpenedInVaultACannotStartAfterSwitchingToVaultB() async throws {
        let (state, vaultA) = try await makeVault(files: [:])
        let sessionA = try XCTUnwrap(state.currentSession)
        let vaultB = try makeVaultDirectory(files: [:])
        let destination = vaultA.appendingPathComponent("Deferred.csv")

        state.openVault(at: vaultB)
        await state.scanTask?.value

        XCTAssertNil(
            state.performBaseSavePanelWrite(
                text: "from stale panel\n",
                to: destination,
                originSession: sessionA,
                successMessage: "Exported base view.",
                failurePrefix: "Base view could not be exported"))
        XCTAssertFalse(FileManager.default.fileExists(atPath: destination.path))
        XCTAssertFalse(state.isMutatingStructure)
    }

    func testAllWriterNativeAndFilesystemCallsRunOffMainThread() async throws {
        let canvas = #"{"nodes":[{"id":"b","type":"text","text":"Beta","x":0,"y":0,"width":240,"height":120}],"edges":[]}"#
        let (state, vault) = try await makeVault(files: ["board.canvas": canvas])
        let session = try XCTUnwrap(state.currentSession)
        let queryID = try session.saveQuery(
            name: "All Files",
            description: nil,
            queryJson: Self.minimalSavedQueryJSON,
            sourceSyntax: .builder)
        let recorder = ThreadRecorder()
        let observe: @Sendable (Bool) -> Void = { recorder.append($0) }
        state.structuralBatchRefreshRunner = { _ in }

        state.openFile("board.canvas", target: .currentTab)
        try await XCTUnwrap(
            state.canvasConvertToNote(
                nodeId: "b", path: "Beta.md", nativeThreadObserver: observe)
        ).value

        try await XCTUnwrap(
            state.exportSavedQuery(
                id: queryID, path: "Saved.base", nativeThreadObserver: observe)
        ).value

        state.activeBaseQueryBuilder = BaseQueryBuilderModel()
        try await XCTUnwrap(
            state.basesBuilderSaveAsBase(
                path: "Builder.base", nativeThreadObserver: observe)
        ).value

        try await XCTUnwrap(
            state.performBaseSavePanelWrite(
                text: "name\nAlpha\n",
                to: vault.appendingPathComponent("Export.csv"),
                originSession: session,
                successMessage: "Exported base view.",
                failurePrefix: "Base view could not be exported",
                nativeThreadObserver: observe)
        ).value

        let values = recorder.values()
        XCTAssertEqual(
            values.count, 6,
            "Canvas read/create/retarget plus the three Base writers must report")
        XCTAssertTrue(
            values.allSatisfy { !$0 },
            "all native and filesystem writer calls must run away from the main thread")
    }

    func testBaseListingExportAndVisibleRefreshNativeCallsRunOffMainThread() async throws {
        let base = #"""
            views:
              - type: table
                name: All
                filters: 'file.inFolder("Notes")'
                order: [file.name]
            """#
        let (state, _) = try await makeVault(files: [
            "Notes/Alpha.md": "# Alpha\n",
            "Queries/All.base": base,
        ])
        state.openFile("Queries/All.base", target: .currentTab)
        let recorder = ThreadRecorder()
        state.baseRetargetNativeExecutionObserverForTesting = { event in
            recorder.append(event.ranOnMainThread)
        }

        _ = await state.refreshBaseQueries()?.value
        let listingEvents = recorder.values()
        XCTAssertGreaterThanOrEqual(
            listingEvents.count, 3,
            "saved queries, Base files, and dashboards must all report their native boundary")
        XCTAssertTrue(listingEvents.allSatisfy { !$0 })

        let beforeExport = recorder.values().count
        _ = try await state.basesExportText(format: .csv)
        let exportEvents = Array(recorder.values().dropFirst(beforeExport))
        XCTAssertEqual(exportEvents.count, 1)
        XCTAssertTrue(exportEvents.allSatisfy { !$0 })

        let beforeRefresh = recorder.values().count
        _ = await state.refreshVisibleBasesAfterInAppWrite(
            session: try XCTUnwrap(state.currentSession),
            changedPath: "Notes/Alpha.md")?.value
        let refreshEvents = Array(recorder.values().dropFirst(beforeRefresh))
        XCTAssertFalse(refreshEvents.isEmpty)
        XCTAssertTrue(refreshEvents.allSatisfy { !$0 })
    }

    func testSavePanelAndDirectWriterSurfacesExposeTheSharedBusyReason() throws {
        let appStateBases = try source("Bases/AppState+Bases.swift")
        XCTAssertTrue(appStateBases.contains("performBaseSavePanelWrite("))
        XCTAssertTrue(appStateBases.contains("guard currentSession === originSession"))
        XCTAssertTrue(appStateBases.contains("await Task.detached"))

        let panel = try source("Bases/BaseQueriesPanel.swift")
        XCTAssertTrue(panel.contains("appState.structuralMutationDisabledReason"))
        XCTAssertTrue(panel.contains(".disabled(exportDisabledReason != nil)"))
        XCTAssertTrue(panel.contains("Text(exportDisabledReason)"))

        let builder = try source("Bases/BaseQueryBuilderSheet.swift")
        XCTAssertTrue(builder.contains("appState.structuralMutationDisabledReason"))
        XCTAssertTrue(builder.contains(".disabled(saveBaseDisabledReason != nil)"))
        XCTAssertTrue(builder.contains("Text(saveBaseDisabledReason)"))

        let embed = try source("Bases/BaseEmbedView.swift")
        XCTAssertTrue(embed.contains("@EnvironmentObject private var appState: AppState"))
        XCTAssertTrue(embed.contains("appState.structuralMutationDisabledReason"))
        XCTAssertTrue(embed.contains("performBaseSavePanelWrite("))
        XCTAssertFalse(embed.contains("onWroteSaveDestination(url, existedBefore)"))
    }

    func testEverySavePanelEntrypointCapturesVaultSessionBeforePresentation() throws {
        let appStateBases = try source("Bases/AppState+Bases.swift")
        let savedQueryPanel = try XCTUnwrap(
            functionBody("exportSavedQueryUsingSavePanel", in: appStateBases))
        let savedQueryText = String(savedQueryPanel)
        let savedQueryAdmission = try XCTUnwrap(
            savedQueryText.range(of: "guard admitStructuralMutationRequest()"))
        let savedQueryPresentation = try XCTUnwrap(
            savedQueryText.range(of: "let panel = NSSavePanel()"))
        XCTAssertLessThan(savedQueryAdmission.lowerBound, savedQueryPresentation.lowerBound)
        XCTAssertTrue(savedQueryText.contains("guard let originSession = currentSession"))
        XCTAssertTrue(savedQueryText.contains("self.currentSession === originSession"))
        XCTAssertTrue(savedQueryText.contains("vaultURL: originVaultURL"))

        let viewExport = try XCTUnwrap(
            functionBody("basesExportToSavePanel", in: appStateBases))
        let viewExportText = String(viewExport)
        let viewExportAdmission = try XCTUnwrap(
            viewExportText.range(of: "guard admitStructuralMutationRequest()"))
        let viewExportPresentation = try XCTUnwrap(
            viewExportText.range(of: "let panel = NSSavePanel()"))
        XCTAssertLessThan(viewExportAdmission.lowerBound, viewExportPresentation.lowerBound)
        XCTAssertTrue(viewExportText.contains("let originSession = currentSession"))
        XCTAssertTrue(viewExportText.contains("self.currentSession === originSession"))
        XCTAssertTrue(viewExportText.contains("performBaseSavePanelWrite("))

        let embedSource = try source("Bases/BaseEmbedView.swift")
        let conversion = try XCTUnwrap(functionBody("convertDataview", in: embedSource))
        let conversionText = String(conversion)
        let conversionAdmission = try XCTUnwrap(
            conversionText.range(of: "appState.admitStructuralMutationRequest()"))
        let conversionPresentation = try XCTUnwrap(
            conversionText.range(of: "let panel = NSSavePanel()"))
        XCTAssertLessThan(conversionAdmission.lowerBound, conversionPresentation.lowerBound)
        XCTAssertTrue(conversionText.contains("appState.currentSession === originSession"))
        XCTAssertTrue(
            conversionText.contains("Task.detached"),
            "DQL conversion must not call the synchronous native parser on the main actor")
        XCTAssertTrue(conversionText.contains("performBaseSavePanelWrite("))
    }
}
