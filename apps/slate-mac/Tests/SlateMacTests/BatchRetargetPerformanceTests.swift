// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// FL-03 Task 5: landing a native batch report must stay a bounded Swift-state
/// operation. Path-bound Canvas/Base handles are detached synchronously, but
/// every close/open/query runs away from the main actor and parked documents
/// remain lazy until activation.
@MainActor
final class BatchRetargetPerformanceTests: XCTestCase {
    private final class CanvasNativeRecorder: @unchecked Sendable {
        private let lock = NSLock()
        private var preparedPathsStorage: [String] = []
        private var eventsStorage: [CanvasNewFileNativeExecutionEvent] = []
        private var baseEventsStorage: [BaseRetargetNativeExecutionEvent] = []

        func recordPreparedPath(_ path: String) {
            lock.lock()
            preparedPathsStorage.append(path)
            lock.unlock()
        }

        func recordEvent(_ event: CanvasNewFileNativeExecutionEvent) {
            lock.lock()
            eventsStorage.append(event)
            lock.unlock()
        }

        func recordBaseEvent(_ event: BaseRetargetNativeExecutionEvent) {
            lock.lock()
            baseEventsStorage.append(event)
            lock.unlock()
        }

        var preparedPaths: [String] {
            lock.lock()
            defer { lock.unlock() }
            return preparedPathsStorage
        }

        var events: [CanvasNewFileNativeExecutionEvent] {
            lock.lock()
            defer { lock.unlock() }
            return eventsStorage
        }

        var baseEvents: [BaseRetargetNativeExecutionEvent] {
            lock.lock()
            defer { lock.unlock() }
            return baseEventsStorage
        }
    }

    private final class NativeConcurrencyGate: @unchecked Sendable {
        private let condition = NSCondition()
        private var released = false
        private var entriesStorage = 0
        private var inFlightStorage = 0
        private var maxInFlightStorage = 0

        func run<T>(_ work: () -> T) -> T {
            condition.lock()
            entriesStorage += 1
            inFlightStorage += 1
            maxInFlightStorage = max(maxInFlightStorage, inFlightStorage)
            condition.broadcast()
            while !released { condition.wait() }
            condition.unlock()

            let value = work()

            condition.lock()
            inFlightStorage -= 1
            condition.broadcast()
            condition.unlock()
            return value
        }

        func snapshot() -> (entries: Int, inFlight: Int, maxInFlight: Int) {
            condition.lock()
            defer { condition.unlock() }
            return (entriesStorage, inFlightStorage, maxInFlightStorage)
        }

        func release() {
            condition.lock()
            released = true
            condition.broadcast()
            condition.unlock()
        }
    }

    private final class FirstTwoBlockingNativeGate: @unchecked Sendable {
        private let condition = NSCondition()
        private var released = false
        private var entriesStorage = 0
        private var inFlightStorage = 0
        private var maxInFlightStorage = 0

        func run<T>(_ work: () -> T) -> T {
            condition.lock()
            let ordinal = entriesStorage
            entriesStorage += 1
            inFlightStorage += 1
            maxInFlightStorage = max(maxInFlightStorage, inFlightStorage)
            condition.broadcast()
            while ordinal < 2, !released { condition.wait() }
            condition.unlock()

            let value = work()

            condition.lock()
            inFlightStorage -= 1
            condition.broadcast()
            condition.unlock()
            return value
        }

        func snapshot() -> (entries: Int, inFlight: Int, maxInFlight: Int) {
            condition.lock()
            defer { condition.unlock() }
            return (entriesStorage, inFlightStorage, maxInFlightStorage)
        }

        func releaseFirstTwo() {
            condition.lock()
            released = true
            condition.broadcast()
            condition.unlock()
        }
    }

    private final class LockedFlag: @unchecked Sendable {
        private let lock = NSLock()
        private var storage = false

        func set() {
            lock.lock()
            storage = true
            lock.unlock()
        }

        var value: Bool {
            lock.lock()
            defer { lock.unlock() }
            return storage
        }
    }

    private var tempDirs: [URL] = []

    override func tearDown() {
        for directory in tempDirs {
            try? FileManager.default.removeItem(at: directory)
        }
        tempDirs = []
        super.tearDown()
    }

    private func makeVault() async throws -> (state: AppState, url: URL) {
        let vault = FileManager.default.temporaryDirectory
            .appendingPathComponent("batch-retarget-\(UUID().uuidString)")
        try FileManager.default.createDirectory(
            at: vault.appendingPathComponent("folder"), withIntermediateDirectories: true)
        try FileManager.default.createDirectory(
            at: vault.appendingPathComponent("dest"), withIntermediateDirectories: true)
        try FileManager.default.createDirectory(
            at: vault.appendingPathComponent("Notes"), withIntermediateDirectories: true)
        tempDirs.append(vault)

        let canvas = #"{"nodes":[{"id":"a","type":"text","text":"Alpha","x":0,"y":0,"width":100,"height":50}],"edges":[]}"#
        for name in ["parked.canvas", "live.canvas"] {
            try canvas.write(
                to: vault.appendingPathComponent("folder/\(name)"),
                atomically: true,
                encoding: .utf8)
        }

        let base = #"""
            views:
              - type: table
                name: Reading
                filters: "file.inFolder(\"Notes\")"
                order:
                  - file.name
                  - status
            """#
        for name in ["Parked.base", "Live.base"] {
            try base.write(
                to: vault.appendingPathComponent("folder/\(name)"),
                atomically: true,
                encoding: .utf8)
        }
        try "---\nstatus: active\n---\n# Alpha\n".write(
            to: vault.appendingPathComponent("Notes/Alpha.md"),
            atomically: true,
            encoding: .utf8)

        let state = AppState(
            recentsStore: nil,
            externalOpener: { _ in true },
            preferencesStore: PreferencesStore(
                defaults: UserDefaults(suiteName: UUID().uuidString)!))
        state.openVault(at: vault)
        await state.scanTask?.value
        return (state, vault)
    }

    private func eventually(
        timeout: TimeInterval = 1.0,
        _ condition: () -> Bool
    ) async -> Bool {
        let deadline = Date().addingTimeInterval(timeout)
        while Date() < deadline {
            if condition() { return true }
            try? await Task.sleep(nanoseconds: 10_000_000)
        }
        return condition()
    }

    func testBatchFolderRetargetKeepsParkedNativeDocumentsLazyAndRunsCanvasNativeWorkOffMain()
        async throws
    {
        let (state, _) = try await makeVault()
        let session = try XCTUnwrap(state.currentSession)

        state.openFile("folder/parked.canvas", target: .currentTab)
        let parkedCanvasTab = try XCTUnwrap(state.workspace.model.activeGroup.activeTabID)
        let parkedCanvas = try XCTUnwrap(state.canvasDocuments["folder/parked.canvas"])
        let parkedCanvasHandle = try XCTUnwrap(parkedCanvas.handle)
        parkedCanvas.selection.selected = "a"
        parkedCanvas.selection.marked = ["a"]
        parkedCanvas.filterText = "Alpha"
        parkedCanvas.viewport.scale = 1.75
        parkedCanvas.viewport.offset = CGPoint(x: 41, y: 19)

        state.openFile("folder/Parked.base", target: .newTab)
        let parkedBaseTab = try XCTUnwrap(state.workspace.model.activeGroup.activeTabID)
        let parkedBase = try XCTUnwrap(
            state.baseDocuments[BaseDocumentSource.file(path: "folder/Parked.base").key])
        let parkedBaseHandle = try XCTUnwrap(parkedBase.handle)
        _ = parkedBase.applyQuickFilter("Alpha", session: session)
        parkedBase.focusColumn(1)
        _ = parkedBase.sortFocusedColumn(session: session)
        let parkedBaseState = (
            quickFilter: parkedBase.quickFilterText,
            sort: parkedBase.sortState,
            focus: parkedBase.focusedColumnIndex,
            view: parkedBase.activeViewName,
            result: parkedBase.result)

        state.openFile("folder/live.canvas", target: .newTab)
        let liveCanvas = try XCTUnwrap(state.canvasDocuments["folder/live.canvas"])
        let liveCanvasHandle = try XCTUnwrap(liveCanvas.handle)
        state.openFile("folder/Live.base", target: .newSplit(.horizontal))
        let liveBase = try XCTUnwrap(
            state.baseDocuments[BaseDocumentSource.file(path: "folder/Live.base").key])
        let liveBaseHandle = try XCTUnwrap(liveBase.handle)

        XCTAssertEqual(state.workspace.model.groupsInOrder.count, 2)
        XCTAssertEqual(
            Set(state.workspace.model.groupsInOrder.compactMap { $0.activeTab?.item.path }),
            Set(["folder/live.canvas", "folder/Live.base"]))

        let recorder = CanvasNativeRecorder()
        state.canvasNewFileNativeExecutionObserverForTesting = { event in
            recorder.recordEvent(event)
        }
        state.baseRetargetNativeExecutionObserverForTesting = { event in
            recorder.recordBaseEvent(event)
        }
        state.canvasNewFilePreloadRunner = { session, path, observer in
            recorder.recordPreparedPath(path)
            return CanvasPreparedLoader.prepare(
                session: session, path: path, observer: observer)
        }

        let task = try XCTUnwrap(
            state.batchMove(
                [.init(path: "folder", isDirectory: true)],
                to: "dest",
                preferredFocusPath: "folder/Live.base"))
        await task.value

        let parkedCanvasAfter = try XCTUnwrap(
            state.canvasDocuments["dest/folder/parked.canvas"])
        let liveCanvasAfter = try XCTUnwrap(
            state.canvasDocuments["dest/folder/live.canvas"])
        let parkedBaseAfter = try XCTUnwrap(
            state.baseDocuments[
                BaseDocumentSource.file(path: "dest/folder/Parked.base").key])
        let liveBaseAfter = try XCTUnwrap(
            state.baseDocuments[
                BaseDocumentSource.file(path: "dest/folder/Live.base").key])

        XCTAssertTrue(parkedCanvasAfter === parkedCanvas)
        XCTAssertTrue(liveCanvasAfter === liveCanvas)
        XCTAssertTrue(parkedBaseAfter === parkedBase)
        XCTAssertTrue(liveBaseAfter === liveBase)
        XCTAssertNil(
            parkedCanvasAfter.handle,
            "an inactive tab must not reopen its path-bound Canvas handle at batch landing")
        XCTAssertNil(
            parkedBaseAfter.handle,
            "an inactive tab must not reopen its path-bound Base handle at batch landing")
        XCTAssertNotEqual(parkedCanvasAfter.handle, parkedCanvasHandle)
        XCTAssertNotEqual(parkedBaseAfter.handle, parkedBaseHandle)

        XCTAssertEqual(parkedCanvasAfter.selection.selected, "a")
        XCTAssertEqual(parkedCanvasAfter.selection.marked, ["a"])
        XCTAssertEqual(parkedCanvasAfter.filterText, "Alpha")
        XCTAssertEqual(parkedCanvasAfter.viewport.scale, 1.75)
        XCTAssertEqual(parkedCanvasAfter.viewport.offset, CGPoint(x: 41, y: 19))
        XCTAssertEqual(parkedBaseAfter.quickFilterText, parkedBaseState.quickFilter)
        XCTAssertEqual(parkedBaseAfter.sortState, parkedBaseState.sort)
        XCTAssertEqual(parkedBaseAfter.focusedColumnIndex, parkedBaseState.focus)
        XCTAssertEqual(parkedBaseAfter.activeViewName, parkedBaseState.view)
        XCTAssertEqual(parkedBaseAfter.result, parkedBaseState.result)

        guard await eventually({
            recorder.preparedPaths == ["dest/folder/live.canvas"]
                && liveCanvasAfter.handle != nil
                && liveBaseAfter.handle != nil
        }) else {
            return XCTFail(
                "only the visible Canvas should prepare after landing; got "
                    + "\(recorder.preparedPaths)")
        }
        XCTAssertNotEqual(liveCanvasAfter.handle, liveCanvasHandle)
        XCTAssertNotEqual(liveBaseAfter.handle, liveBaseHandle)

        let landingEvents = recorder.events
        XCTAssertFalse(landingEvents.isEmpty, "the injected native probe must observe retarget work")
        XCTAssertFalse(
            landingEvents.contains(where: \.ranOnMainThread),
            "batch landing must perform zero Canvas close/open/query work on the main actor")
        XCTAssertEqual(
            landingEvents.filter { $0.phase == .closeReplaced }.count,
            2,
            "each old Canvas handle closes off-main exactly once, including parked documents")
        XCTAssertEqual(landingEvents.filter { $0.phase == .open }.count, 1)
        let landingBaseEvents = recorder.baseEvents
        XCTAssertFalse(landingBaseEvents.isEmpty)
        XCTAssertFalse(
            landingBaseEvents.contains(where: \.ranOnMainThread),
            "batch landing must perform zero Base close/open/query work on the main actor")
        XCTAssertEqual(
            landingBaseEvents.filter { $0.phase == .closeReplaced }.count,
            2,
            "each old Base handle closes off-main exactly once, including parked documents")
        XCTAssertEqual(landingBaseEvents.filter { $0.phase == .open }.count, 1)

        state.activateTab(parkedCanvasTab)
        XCTAssertNil(
            parkedCanvasAfter.handle,
            "activating a retarget-pending Canvas must not fall back to synchronous loading")
        guard await eventually({
            recorder.preparedPaths.sorted()
                == ["dest/folder/live.canvas", "dest/folder/parked.canvas"]
                && parkedCanvasAfter.handle != nil
        }) else {
            return XCTFail(
                "parked Canvas activation did not enter the async prepare path; got "
                    + "\(recorder.preparedPaths)")
        }
        XCTAssertEqual(
            recorder.events.filter { $0.phase == .closeReplaced }.count,
            2,
            "activation must not close the already-detached old handle a second time")
        XCTAssertFalse(recorder.events.contains(where: \.ranOnMainThread))

        state.activateTab(parkedBaseTab)
        XCTAssertNil(
            parkedBaseAfter.handle,
            "activating a retarget-pending Base must not fall back to synchronous loading")
        guard await eventually({
            recorder.baseEvents.filter { $0.phase == .open }.count == 2
                && parkedBaseAfter.handle != nil
        }) else {
            return XCTFail("parked Base activation did not enter the async prepare path")
        }
        XCTAssertEqual(
            recorder.baseEvents.filter { $0.phase == .closeReplaced }.count,
            2,
            "Base activation must not close the detached old handle twice")
        XCTAssertFalse(recorder.baseEvents.contains(where: \.ranOnMainThread))
    }

    func testBatchRetargetSourceHasNoSynchronousCanvasOrBaseNativeReloadInLandingHelpers()
        throws
    {
        let sourceRoot = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .appendingPathComponent("Sources/SlateMac")
        let canvasSource = try String(
            contentsOf: sourceRoot.appendingPathComponent("Canvas/AppState+Canvas.swift"),
            encoding: .utf8)
        let baseSource = try String(
            contentsOf: sourceRoot.appendingPathComponent("Bases/AppState+Bases.swift"),
            encoding: .utf8)

        let canvasStart = try XCTUnwrap(canvasSource.range(of: "func rekeyCanvasDocument("))
        let canvasEnd = try XCTUnwrap(
            canvasSource.range(
                of: "func releaseAllCanvasDocuments()",
                range: canvasStart.upperBound..<canvasSource.endIndex))
        let canvasLanding = canvasSource[canvasStart.lowerBound..<canvasEnd.lowerBound]

        let baseStart = try XCTUnwrap(baseSource.range(of: "func rekeyBaseDocumentIfRetargeted("))
        let baseEnd = try XCTUnwrap(
            baseSource.range(
                of: "func invalidateBaseDocument(",
                range: baseStart.upperBound..<baseSource.endIndex))
        let baseLanding = baseSource[baseStart.lowerBound..<baseEnd.lowerBound]

        XCTAssertFalse(
            canvasLanding.contains("document.retarget(to: newPath, session: currentSession)"),
            "batch Canvas landing must detach/rekey only; native reload belongs off-main")
        XCTAssertFalse(
            baseLanding.contains("doc.retarget(to: newPath, session: currentSession)"),
            "batch Base landing must detach/rekey only; native reload belongs off-main")
        XCTAssertFalse(
            baseLanding.contains("dock.close(session: session)"),
            "docked Base cleanup must not block the main actor")
        XCTAssertFalse(
            baseLanding.contains("refreshBasesDockTarget("),
            "docked Base loading/querying must use the async prepared path")
    }

    func testStaleCanvasPreparationAfterVaultSwitchIsReleasedOnceAndNeverApplied()
        async throws
    {
        let (state, _) = try await makeVault()
        state.openFile("folder/live.canvas", target: .currentTab)
        let oldDocument = try XCTUnwrap(state.canvasDocuments["folder/live.canvas"])
        let oldSession = try XCTUnwrap(state.currentSession)
        let recorder = CanvasNativeRecorder()
        let gate = NativeConcurrencyGate()
        state.canvasNewFileNativeExecutionObserverForTesting = { event in
            recorder.recordEvent(event)
        }
        state.canvasNewFilePreloadRunner = { session, path, observer in
            gate.run {
                CanvasPreparedLoader.prepare(
                    session: session, path: path, observer: observer)
            }
        }

        let batchTask = try XCTUnwrap(
            state.batchMove(
                [.init(path: "folder", isDirectory: true)],
                to: "dest",
                preferredFocusPath: "folder/live.canvas"))
        await batchTask.value
        guard await eventually({ gate.snapshot().entries == 1 }) else {
            return XCTFail("retarget preparation never reached the injected suspension gate")
        }
        let retargetTask = try XCTUnwrap(state.nativeDocumentRetargetTask)
        XCTAssertNil(oldDocument.handle)

        let replacementVault = FileManager.default.temporaryDirectory
            .appendingPathComponent("batch-retarget-replacement-\(UUID().uuidString)")
        try FileManager.default.createDirectory(
            at: replacementVault, withIntermediateDirectories: true)
        try "# replacement\n".write(
            to: replacementVault.appendingPathComponent("replacement.md"),
            atomically: true,
            encoding: .utf8)
        tempDirs.append(replacementVault)
        state.openVault(at: replacementVault)
        await state.scanTask?.value
        let replacementSession = try XCTUnwrap(state.currentSession)
        XCTAssertFalse(replacementSession === oldSession)

        gate.release()
        await retargetTask.value
        guard await eventually({
            recorder.events.filter { $0.phase == .closePrepared }.count == 1
        }) else {
            return XCTFail("stale prepared Canvas handle was not released")
        }

        XCTAssertTrue(state.currentSession === replacementSession)
        XCTAssertTrue(state.canvasDocuments.isEmpty)
        XCTAssertNil(oldDocument.handle)
        XCTAssertEqual(
            recorder.events.filter { $0.phase == .closeReplaced }.count,
            1,
            "the old path handle closes once before preparation")
        XCTAssertEqual(
            recorder.events.filter { $0.phase == .closePrepared }.count,
            1,
            "the stale replacement handle closes once after its rejected apply")
        XCTAssertFalse(recorder.events.contains(where: \.ranOnMainThread))
    }

    func testOverlappingBatchAndParkedActivationShareOneTwoWideSchedulerAndTaskLifetime()
        async throws
    {
        let vault = FileManager.default.temporaryDirectory
            .appendingPathComponent("batch-retarget-overlap-\(UUID().uuidString)")
        try FileManager.default.createDirectory(
            at: vault.appendingPathComponent("folder"), withIntermediateDirectories: true)
        try FileManager.default.createDirectory(
            at: vault.appendingPathComponent("dest"), withIntermediateDirectories: true)
        tempDirs.append(vault)
        let canvas = #"{"nodes":[{"id":"a","type":"text","text":"A","x":0,"y":0,"width":100,"height":50}],"edges":[]}"#
        for name in ["parked.canvas", "visible-a.canvas", "visible-b.canvas"] {
            try canvas.write(
                to: vault.appendingPathComponent("folder/\(name)"),
                atomically: true,
                encoding: .utf8)
        }

        let state = AppState(
            recentsStore: nil,
            externalOpener: { _ in true },
            preferencesStore: PreferencesStore(
                defaults: UserDefaults(suiteName: UUID().uuidString)!))
        state.openVault(at: vault)
        await state.scanTask?.value
        state.openFile("folder/parked.canvas", target: .currentTab)
        let parkedTab = try XCTUnwrap(state.workspace.model.activeGroup.activeTabID)
        state.openFile("folder/visible-a.canvas", target: .newTab)
        state.openFile("folder/visible-b.canvas", target: .newSplit(.horizontal))

        let gate = FirstTwoBlockingNativeGate()
        state.canvasNewFilePreloadRunner = { session, path, observer in
            gate.run {
                CanvasPreparedLoader.prepare(
                    session: session, path: path, observer: observer)
            }
        }

        await state.batchMove(
            [.init(path: "folder", isDirectory: true)],
            to: "dest",
            preferredFocusPath: "folder/visible-b.canvas")?.value
        guard await eventually({ gate.snapshot().entries == 2 }) else {
            return XCTFail("the two visible retarget preparations did not suspend")
        }
        let originalSchedulerTask = try XCTUnwrap(state.nativeDocumentRetargetTask)

        state.activateTab(parkedTab)
        let aggregateTask = try XCTUnwrap(state.nativeDocumentRetargetTask)
        let aggregateCompleted = LockedFlag()
        let aggregateWaiter = Task {
            await aggregateTask.value
            aggregateCompleted.set()
        }
        let thirdStartedWhileTwoBlocked = await eventually(timeout: 0.25) {
            gate.snapshot().entries >= 3
        }
        _ = await eventually(timeout: 0.25) { aggregateCompleted.value }

        XCTAssertFalse(
            thirdStartedWhileTwoBlocked,
            "parked activation must wait for a shared native-preparation permit")
        XCTAssertLessThanOrEqual(gate.snapshot().maxInFlight, 2)
        XCTAssertFalse(
            aggregateCompleted.value,
            "the retained scheduler task must not finish while earlier admitted work is still blocked")

        gate.releaseFirstTwo()
        await originalSchedulerTask.value
        await aggregateWaiter.value

        XCTAssertLessThanOrEqual(gate.snapshot().maxInFlight, 2)
        for name in ["parked.canvas", "visible-a.canvas", "visible-b.canvas"] {
            XCTAssertNotNil(state.canvasDocuments["dest/folder/\(name)"]?.handle)
        }
    }

    func testCanvasRetargetAndVisibleBaseRefreshShareTheSameTwoPermits()
        async throws
    {
        let vault = FileManager.default.temporaryDirectory
            .appendingPathComponent("native-preparation-cross-funnel-\(UUID().uuidString)")
        try FileManager.default.createDirectory(
            at: vault.appendingPathComponent("folder"), withIntermediateDirectories: true)
        try FileManager.default.createDirectory(
            at: vault.appendingPathComponent("dest"), withIntermediateDirectories: true)
        try FileManager.default.createDirectory(
            at: vault.appendingPathComponent("Queries"), withIntermediateDirectories: true)
        try FileManager.default.createDirectory(
            at: vault.appendingPathComponent("Notes"), withIntermediateDirectories: true)
        tempDirs.append(vault)
        let canvas = #"{"nodes":[{"id":"a","type":"text","text":"A","x":0,"y":0,"width":100,"height":50}],"edges":[]}"#
        for name in ["visible-a.canvas", "visible-b.canvas"] {
            try canvas.write(
                to: vault.appendingPathComponent("folder/\(name)"),
                atomically: true,
                encoding: .utf8)
        }
        let base = #"""
            views:
              - type: table
                name: Notes
                filters: 'file.inFolder("Notes")'
                order: [file.name]
            """#
        try base.write(
            to: vault.appendingPathComponent("Queries/Notes.base"),
            atomically: true,
            encoding: .utf8)
        try "# Alpha\n".write(
            to: vault.appendingPathComponent("Notes/Alpha.md"),
            atomically: true,
            encoding: .utf8)

        let state = AppState(
            recentsStore: nil,
            externalOpener: { _ in true },
            preferencesStore: PreferencesStore(
                defaults: UserDefaults(suiteName: UUID().uuidString)!))
        state.openVault(at: vault)
        await state.scanTask?.value
        let session = try XCTUnwrap(state.currentSession)
        state.openFile("folder/visible-a.canvas", target: .currentTab)
        state.openFile("folder/visible-b.canvas", target: .newSplit(.horizontal))
        state.openFile("Queries/Notes.base", target: .newSplit(.horizontal))
        let baseDocument = try XCTUnwrap(state.activeBaseDocument)
        XCTAssertEqual(baseDocument.result?.rows.map(\.filePath), ["Notes/Alpha.md"])

        let gate = FirstTwoBlockingNativeGate()
        state.canvasNewFilePreloadRunner = { session, path, observer in
            gate.run {
                CanvasPreparedLoader.prepare(
                    session: session, path: path, observer: observer)
            }
        }
        state.baseRetargetPreloadRunner = { session, request, observer in
            gate.run {
                BasePreparedLoader.prepare(
                    session: session, request: request, observer: observer)
            }
        }

        await state.batchMove(
            [.init(path: "folder", isDirectory: true)],
            to: "dest",
            preferredFocusPath: "folder/visible-b.canvas")?.value
        guard await eventually({ gate.snapshot().entries == 2 }) else {
            return XCTFail("the two Canvas retarget preparations did not suspend")
        }
        _ = try session.saveText(
            path: "Notes/Beta.md",
            contents: "# Beta\n",
            expectedContentHash: nil)
        let refreshTask = try XCTUnwrap(
            state.refreshVisibleBasesAfterInAppWrite(
                session: session,
                changedPath: "Notes/Beta.md"))
        let refreshCompleted = LockedFlag()
        let refreshWaiter = Task {
            _ = await refreshTask.value
            refreshCompleted.set()
        }

        let baseEnteredWhileCanvasBlocked = await eventually(timeout: 0.25) {
            gate.snapshot().entries >= 3
        }
        _ = await eventually(timeout: 0.25) { refreshCompleted.value }
        XCTAssertFalse(
            baseEnteredWhileCanvasBlocked,
            "visible Base refresh must share the Canvas retarget preparation permits")
        XCTAssertLessThanOrEqual(gate.snapshot().maxInFlight, 2)
        XCTAssertFalse(refreshCompleted.value)

        gate.releaseFirstTwo()
        await state.nativeDocumentRetargetTask?.value
        await refreshWaiter.value

        XCTAssertLessThanOrEqual(gate.snapshot().maxInFlight, 2)
        XCTAssertEqual(
            baseDocument.result?.rows.map(\.filePath),
            ["Notes/Alpha.md", "Notes/Beta.md"])
        XCTAssertNotNil(state.canvasDocuments["dest/folder/visible-a.canvas"]?.handle)
        XCTAssertNotNil(state.canvasDocuments["dest/folder/visible-b.canvas"]?.handle)
    }

    func testSixVisibleCanvasRetargetsNeverExceedTwoConcurrentPreparations()
        async throws
    {
        let vault = FileManager.default.temporaryDirectory
            .appendingPathComponent("batch-retarget-many-\(UUID().uuidString)")
        try FileManager.default.createDirectory(
            at: vault.appendingPathComponent("folder"), withIntermediateDirectories: true)
        try FileManager.default.createDirectory(
            at: vault.appendingPathComponent("dest"), withIntermediateDirectories: true)
        tempDirs.append(vault)
        let canvas = #"{"nodes":[{"id":"a","type":"text","text":"A","x":0,"y":0,"width":100,"height":50}],"edges":[]}"#
        for index in 0..<6 {
            try canvas.write(
                to: vault.appendingPathComponent("folder/board-\(index).canvas"),
                atomically: true,
                encoding: .utf8)
        }

        let state = AppState(
            recentsStore: nil,
            externalOpener: { _ in true },
            preferencesStore: PreferencesStore(
                defaults: UserDefaults(suiteName: UUID().uuidString)!))
        state.openVault(at: vault)
        await state.scanTask?.value
        state.openFile("folder/board-0.canvas", target: .currentTab)
        for index in 1..<6 {
            state.openFile(
                "folder/board-\(index).canvas",
                target: .newSplit(.horizontal))
        }
        XCTAssertEqual(state.workspace.model.groupsInOrder.count, 6)

        let recorder = CanvasNativeRecorder()
        let gate = NativeConcurrencyGate()
        state.canvasNewFileNativeExecutionObserverForTesting = { event in
            recorder.recordEvent(event)
        }
        state.canvasNewFilePreloadRunner = { session, path, observer in
            recorder.recordPreparedPath(path)
            return gate.run {
                CanvasPreparedLoader.prepare(
                    session: session, path: path, observer: observer)
            }
        }

        let batchTask = try XCTUnwrap(
            state.batchMove(
                [.init(path: "folder", isDirectory: true)],
                to: "dest",
                preferredFocusPath: "folder/board-5.canvas"))
        await batchTask.value
        guard await eventually({ gate.snapshot().entries == 2 }) else {
            return XCTFail("the first bounded preparation pair did not start")
        }
        let blocked = gate.snapshot()
        XCTAssertEqual(blocked.entries, 2)
        XCTAssertEqual(blocked.inFlight, 2)
        XCTAssertEqual(blocked.maxInFlight, 2)

        gate.release()
        await state.nativeDocumentRetargetTask?.value
        XCTAssertEqual(gate.snapshot().entries, 6)
        XCTAssertEqual(gate.snapshot().maxInFlight, 2)
        XCTAssertEqual(
            Set(recorder.preparedPaths),
            Set((0..<6).map { "dest/folder/board-\($0).canvas" }))
        XCTAssertEqual(recorder.events.filter { $0.phase == .closeReplaced }.count, 6)
        XCTAssertEqual(recorder.events.filter { $0.phase == .open }.count, 6)
        XCTAssertFalse(recorder.events.contains(where: \.ranOnMainThread))
        for index in 0..<6 {
            XCTAssertNotNil(
                state.canvasDocuments["dest/folder/board-\(index).canvas"]?.handle)
        }
    }

    func testSixVisibleBaseRefreshesKeepOldSnapshotsAndNeverExceedTwoNativePreparations()
        async throws
    {
        let vault = FileManager.default.temporaryDirectory
            .appendingPathComponent("visible-base-refresh-many-\(UUID().uuidString)")
        try FileManager.default.createDirectory(
            at: vault.appendingPathComponent("Queries"), withIntermediateDirectories: true)
        try FileManager.default.createDirectory(
            at: vault.appendingPathComponent("Notes"), withIntermediateDirectories: true)
        tempDirs.append(vault)
        let base = #"""
            views:
              - type: table
                name: All
                filters: 'file.inFolder("Notes")'
                order: [file.name]
            """#
        for index in 0..<6 {
            try base.write(
                to: vault.appendingPathComponent("Queries/All-\(index).base"),
                atomically: true,
                encoding: .utf8)
        }
        try "# Alpha\n".write(
            to: vault.appendingPathComponent("Notes/Alpha.md"),
            atomically: true,
            encoding: .utf8)

        let state = AppState(
            recentsStore: nil,
            externalOpener: { _ in true },
            preferencesStore: PreferencesStore(
                defaults: UserDefaults(suiteName: UUID().uuidString)!))
        state.openVault(at: vault)
        await state.scanTask?.value
        state.openFile("Queries/All-0.base", target: .currentTab)
        for index in 1..<6 {
            state.openFile(
                "Queries/All-\(index).base",
                target: .newSplit(.horizontal))
        }

        var documents: [BaseDocument] = []
        for index in 0..<6 {
            documents.append(
                try XCTUnwrap(
                    state.baseDocuments[
                        BaseDocumentSource.file(path: "Queries/All-\(index).base").key]))
        }
        let oldHandles = documents.map(\.handle)
        let oldResults = documents.map(\.result)
        let recorder = CanvasNativeRecorder()
        let gate = NativeConcurrencyGate()
        state.baseRetargetNativeExecutionObserverForTesting = { event in
            recorder.recordBaseEvent(event)
        }
        state.baseRetargetPreloadRunner = { session, request, observer in
            gate.run {
                BasePreparedLoader.prepare(
                    session: session, request: request, observer: observer)
            }
        }

        let refreshTask = try XCTUnwrap(
            state.refreshVisibleBasesAfterInAppWrite(
                session: try XCTUnwrap(state.currentSession),
                changedPath: "Notes/Alpha.md"))
        guard await eventually({ gate.snapshot().entries == 2 }) else {
            return XCTFail("the first bounded Base refresh pair did not start")
        }

        XCTAssertEqual(gate.snapshot().inFlight, 2)
        XCTAssertEqual(gate.snapshot().maxInFlight, 2)
        XCTAssertEqual(
            documents.map(\.handle),
            oldHandles,
            "visible Base handles must remain installed while replacements prepare")
        XCTAssertEqual(
            documents.map(\.result),
            oldResults,
            "visible Base rows must not blank while replacements prepare")

        gate.release()
        _ = await refreshTask.value

        XCTAssertEqual(gate.snapshot().entries, 6)
        XCTAssertEqual(gate.snapshot().maxInFlight, 2)
        XCTAssertEqual(recorder.baseEvents.filter { $0.phase == .open }.count, 6)
        XCTAssertEqual(
            recorder.baseEvents.filter { $0.phase == .closeReplaced }.count,
            6)
        XCTAssertFalse(recorder.baseEvents.contains(where: \.ranOnMainThread))
        for (index, document) in documents.enumerated() {
            XCTAssertNotNil(document.handle)
            XCTAssertNotEqual(document.handle, oldHandles[index])
            XCTAssertEqual(document.result?.rows.map(\.filePath), ["Notes/Alpha.md"])
        }
    }

    func testVisibleBaseRefreshAfterVaultSwitchReleasesPreparedHandleOnceAndNeverApplies()
        async throws
    {
        let (state, _) = try await makeVault()
        state.openFile("folder/Live.base", target: .currentTab)
        let oldDocument = try XCTUnwrap(state.activeBaseDocument)
        let oldSession = try XCTUnwrap(state.currentSession)
        let recorder = CanvasNativeRecorder()
        let gate = NativeConcurrencyGate()
        state.baseRetargetNativeExecutionObserverForTesting = { event in
            recorder.recordBaseEvent(event)
        }
        state.baseRetargetPreloadRunner = { session, request, observer in
            gate.run {
                BasePreparedLoader.prepare(
                    session: session, request: request, observer: observer)
            }
        }

        let refreshTask = try XCTUnwrap(
            state.refreshVisibleBasesAfterInAppWrite(
                session: oldSession,
                changedPath: "Notes/Alpha.md"))
        guard await eventually({ gate.snapshot().entries == 1 }) else {
            return XCTFail("visible Base refresh never reached its suspension gate")
        }

        let replacementVault = FileManager.default.temporaryDirectory
            .appendingPathComponent("visible-base-refresh-replacement-\(UUID().uuidString)")
        try FileManager.default.createDirectory(
            at: replacementVault, withIntermediateDirectories: true)
        try "# Replacement\n".write(
            to: replacementVault.appendingPathComponent("Replacement.md"),
            atomically: true,
            encoding: .utf8)
        tempDirs.append(replacementVault)
        state.openVault(at: replacementVault)
        await state.scanTask?.value
        let replacementSession = try XCTUnwrap(state.currentSession)
        XCTAssertFalse(replacementSession === oldSession)
        XCTAssertNil(oldDocument.handle)

        gate.release()
        let staleAnnouncements = await refreshTask.value
        XCTAssertTrue(staleAnnouncements.isEmpty)

        XCTAssertTrue(state.currentSession === replacementSession)
        XCTAssertTrue(state.baseDocuments.isEmpty)
        XCTAssertNil(oldDocument.handle)
        XCTAssertEqual(
            recorder.baseEvents.filter { $0.phase == .closePrepared }.count,
            1,
            "the stale replacement handle must close exactly once")
        XCTAssertEqual(
            recorder.baseEvents.filter { $0.phase == .closeReplaced }.count,
            0,
            "the old vault teardown owns its original handle")
        XCTAssertFalse(recorder.baseEvents.contains(where: \.ranOnMainThread))
    }

    func testSavedQueryUpdateCannotSupersedeBroadRefreshAndLeaveUnrelatedBaseStale()
        async throws
    {
        let vault = FileManager.default.temporaryDirectory
            .appendingPathComponent("visible-base-refresh-composition-\(UUID().uuidString)")
        try FileManager.default.createDirectory(
            at: vault.appendingPathComponent("Queries"), withIntermediateDirectories: true)
        try FileManager.default.createDirectory(
            at: vault.appendingPathComponent("Notes"), withIntermediateDirectories: true)
        tempDirs.append(vault)
        let base = #"""
            views:
              - type: table
                name: All
                filters: 'file.inFolder("Notes")'
                order: [file.name]
            """#
        try base.write(
            to: vault.appendingPathComponent("Queries/All.base"),
            atomically: true,
            encoding: .utf8)
        try "# Alpha\n".write(
            to: vault.appendingPathComponent("Notes/Alpha.md"),
            atomically: true,
            encoding: .utf8)

        let state = AppState(
            recentsStore: nil,
            externalOpener: { _ in true },
            preferencesStore: PreferencesStore(
                defaults: UserDefaults(suiteName: UUID().uuidString)!))
        state.openVault(at: vault)
        await state.scanTask?.value
        let session = try XCTUnwrap(state.currentSession)
        let savedQueryID = try session.saveQuery(
            name: "Saved Notes",
            description: nil,
            queryJson: #"{"source":{"Folder":"Notes"},"row_source":"Files","filters":null,"formulas":[],"custom_summaries":[],"group_by":null,"sort":[],"columns":[{"id":"file.name","display_name":null}],"summaries":[],"limit":null,"view":{"Table":{"fallback_from":null}}}"#,
            sourceSyntax: .builder)
        _ = await state.refreshBaseQueries()?.value
        state.openFile("Queries/All.base", target: .currentTab)
        let unrelatedBase = try XCTUnwrap(state.activeBaseDocument)
        state.openSavedQuery(id: savedQueryID, name: "Saved Notes", target: .newSplit(.horizontal))
        XCTAssertEqual(unrelatedBase.result?.rows.map(\.filePath), ["Notes/Alpha.md"])

        _ = try session.saveText(
            path: "Notes/Beta.md",
            contents: "# Beta\n",
            expectedContentHash: nil)
        let gate = NativeConcurrencyGate()
        state.baseRetargetPreloadRunner = { session, request, observer in
            gate.run {
                BasePreparedLoader.prepare(
                    session: session, request: request, observer: observer)
            }
        }

        let broadRefresh = try XCTUnwrap(
            state.refreshVisibleBasesAfterInAppWrite(
                session: session,
                changedPath: "Notes/Beta.md"))
        guard await eventually({ gate.snapshot().entries == 2 }) else {
            return XCTFail("the broad refresh did not suspend both consumers")
        }
        let broadGeneration = state.visibleBasesRefreshGeneration

        state.editSavedQueryInBuilder(id: savedQueryID)
        let update = try XCTUnwrap(state.basesBuilderUpdateSavedQuery())
        guard await eventually({
            state.visibleBasesRefreshGeneration > broadGeneration
        }) else {
            return XCTFail("the saved-query update did not queue its replacement refresh")
        }

        gate.release()
        _ = await broadRefresh.value
        await update.value

        XCTAssertEqual(
            unrelatedBase.result?.rows.map(\.filePath),
            ["Notes/Alpha.md", "Notes/Beta.md"],
            "a narrow saved-query refresh must not invalidate an older broad committed-write refresh")
    }
}
