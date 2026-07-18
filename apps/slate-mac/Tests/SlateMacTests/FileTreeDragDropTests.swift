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
        tempDir = FileManager.default.temporaryDirectory
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

    private func batchItem(_ path: String, dir: Bool = false) -> StructuralBatchItem {
        StructuralBatchItem(path: path, isDirectory: dir)
    }

    private func batchMoveReport(
        state: BatchMoveState,
        planned: [StructuralBatchItem],
        opID: Int64? = nil,
        standing: [BatchPathChange] = [],
        requiresRescan: Bool = false
    ) -> BatchMoveReport {
        BatchMoveReport(
            envelope: StructuralBatchEnvelope(
                planned: planned, skipped: [], preflightFailures: []),
            state: state,
            opId: opID,
            standing: standing,
            rolledBack: [],
            failure: nil,
            rollbackFailures: [],
            rewritten: [],
            rewriteFailures: [],
            requiresRescan: requiresRescan)
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
        XCTAssertEqual(action, .none)
    }

    func testFolderDropIntoOwnSubtreeIsNoOp() {
        let vault = URL(fileURLWithPath: "/Vaults/demo")
        let folder = vault.appendingPathComponent("parent")
        // Dropping "parent" into "parent/child" is a folder-into-own-subtree.
        let action = AppState.fileURLDropAction(
            url: folder, vaultURL: vault, destinationFolder: "parent/child", isDirectory: true)
        XCTAssertEqual(action, .none)
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
            .none,
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
