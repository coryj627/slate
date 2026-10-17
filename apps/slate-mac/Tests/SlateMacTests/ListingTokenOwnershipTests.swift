// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// Plan 43 PR 12 (#1324; locked 05 §4): the vault listings that used to page
/// with a fresh, never-cancelled `CancelToken()` per page now hold ONE token
/// for their whole operation, tied to its task or its owner. Each fact ends
/// the owner and shows the token the listing passed to core was cancelled, so
/// the listing stops instead of running on, under the session mutex, after
/// nothing will read its answer. `CancelTokenCensusTests` keeps a fresh token
/// out of every call; these facts keep the replacement owned.
@MainActor
final class ListingTokenOwnershipTests: XCTestCase {
    private var tempDir: URL!

    override func setUpWithError() throws {
        try super.setUpWithError()
        tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-listing-token-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: tempDir, withIntermediateDirectories: true)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: tempDir)
        try super.tearDownWithError()
    }

    private func makeVault(files: [String]) async throws -> (AppState, VaultSession) {
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
        return (state, try XCTUnwrap(state.currentSession))
    }

    /// A value the native callbacks write from the worker and the test reads.
    private final class TokenProbe: @unchecked Sendable {
        private let lock = NSLock()
        private var stored: [(cursor: String?, token: CancelToken, onMain: Bool)] = []

        func record(cursor: String?, token: CancelToken) {
            lock.lock()
            stored.append((cursor, token, Thread.isMainThread))
            lock.unlock()
        }

        var calls: [(cursor: String?, token: CancelToken, onMain: Bool)] {
            lock.lock()
            defer { lock.unlock() }
            return stored
        }
    }

    // MARK: - Import: the post-scan materialization and the inventory reload

    /// `resolveImportMaterialization` pages `list_dir_children` for every
    /// parent under ONE token, cancelled when the import task awaiting it is:
    /// a cancelled reconciliation ends `.Cancelled` and materializes nothing.
    func testCancellingTheImportReconciliationCancelsItsListingToken() async throws {
        let (_, session) = try await makeVault(files: ["file.md", "folder/inner.md"])
        let candidates = [
            SidebarImportMaterializedResult(providerIndex: 0, path: "file.md", isDirectory: false),
            SidebarImportMaterializedResult(
                providerIndex: 1, path: "folder/inner.md", isDirectory: false),
        ]
        let task = Task { @MainActor in
            try await AppState.resolveImportMaterialization(candidates, session: session)
        }
        // Cancelled before its body runs: the handler cancels the token first,
        // so core's first page sees it.
        task.cancel()
        do {
            let materialized = try await task.value
            XCTFail(
                "a cancelled reconciliation listed on with a token nothing could cancel "
                    + "and materialized \(materialized.map(\.path))")
        } catch VaultError.Cancelled {
            // The listing honoured the import task's cancellation.
        }

        // The same call, not cancelled, still materializes both.
        let live = try await AppState.resolveImportMaterialization(candidates, session: session)
        XCTAssertEqual(live.map(\.path), ["file.md", "folder/inner.md"])
    }

    /// The clean import landing's inventory reload (`reloadImportInventoryOnly`,
    /// the default `importInventoryRefreshRunner`) pages `list_files` under
    /// ONE token, cancelled with the import phase task that awaits it, and a
    /// cancelled reload publishes nothing into the sidebar.
    func testCancellingTheImportInventoryRefreshCancelsItsListingToken() async throws {
        let (state, session) = try await makeVault(files: ["a.md", "b.md"])
        let before = state.files.map(\.path)
        _ = try session.createExclusive(path: "late.md", content: "# Late\n")
        let runner = state.importInventoryRefreshRunner
        let task = Task { @MainActor in try await runner(state, session) }
        task.cancel()
        do {
            try await task.value
            XCTFail("a cancelled inventory reload paged on with a fresh token")
        } catch VaultError.Cancelled {
            // The listing honoured the phase task's cancellation.
        }
        XCTAssertEqual(
            state.files.map(\.path), before,
            "a cancelled reload must not publish its listing")

        try await runner(state, session)
        XCTAssertEqual(state.files.map(\.path), ["a.md", "b.md", "late.md"])
    }

    // MARK: - Files tree: the synchronous seam's first page

    /// The synchronous level load (the pure state-machine seam) used to hand
    /// its first page a fresh token and its continuation another. One level
    /// load is one operation: the first page and every continuation share the
    /// level's token, which the level's revocation (a reload, a rebind)
    /// cancels while the load runs — the production loader's shape
    /// (`scheduleLevelLoad`).
    func testTheTreesFirstPageAndItsContinuationShareTheLevelsToken() async throws {
        let probe = TokenProbe()
        let parked = expectation(description: "the continuation page is in core")
        let released = expectation(description: "the continuation saw its token cancelled")
        let vm = FileTreeViewModel()
        let first = DirListing(
            dirs: [],
            files: FileSummaryPage(
                items: [summary("one.md")], nextCursor: "2", totalFiltered: 2))
        vm.bindForTesting(cancellablePagedFetcher: { _, cursor, cancel in
            probe.record(cursor: cursor, token: cancel)
            guard cursor != nil else { return first }
            parked.fulfill()
            let deadline = Date().addingTimeInterval(10)
            while !cancel.isCancelled(), Date() < deadline {
                Thread.sleep(forTimeInterval: 0.001)
            }
            released.fulfill()
            throw CancellationError()
        })
        XCTAssertEqual(vm.rootLevel.map(\.path), ["one.md"], "the first page publishes at once")
        await fulfillment(of: [parked], timeout: 10)

        let calls = probe.calls
        XCTAssertEqual(calls.map(\.cursor), [nil, "2"])
        XCTAssertTrue(
            calls[0].token === calls[1].token,
            "the first page and its continuation are one operation with one token")
        XCTAssertFalse(calls[0].token.isCancelled())

        // Rebinding revokes the level mid-load: its one token is cancelled,
        // and with it the page still in core.
        vm.bindForTesting { _ in
            DirListing(
                dirs: [], files: FileSummaryPage(items: [], nextCursor: nil, totalFiltered: 0))
        }
        await fulfillment(of: [released], timeout: 10)
        XCTAssertTrue(calls[0].token.isCancelled(), "the level's revocation cancels its token")
        XCTAssertTrue(vm.rootLevel.isEmpty)
    }

    // MARK: - Sidebar list pane: one token per drain, off the main actor

    /// The list pane's folder drain hands every `list_dir_files` page the
    /// drain's own token; superseding the container (or closing the vault)
    /// cancels it, so a page parked in core returns `.Cancelled` and the
    /// drain publishes nothing.
    func testCancellingTheDrainCancelsItsListingToken() async throws {
        let probe = TokenProbe()
        let entered = expectation(description: "first page parked in the listing")
        let released = expectation(description: "the parked page saw its token cancelled")
        let model = SidebarListPaneModel()
        model.bind(
            SidebarListPaneModel.Dependencies(
                performQuery: { _, _, _, _ in throw VaultError.Cancelled },
                performUntagged: { _ in throw VaultError.Cancelled },
                listLevel: { _, paging, cancel in
                    probe.record(cursor: paging.cursor, token: cancel)
                    entered.fulfill()
                    let deadline = Date().addingTimeInterval(10)
                    while !cancel.isCancelled(), Date() < deadline {
                        Thread.sleep(forTimeInterval: 0.001)
                    }
                    released.fulfill()
                    throw VaultError.Cancelled
                }),
            organization: { FileTreeViewModel.OrganizationContext() },
            deviceDefaults: { (2, .standard) })
        model.show(.folder(path: "f"))
        let drain = model.drainTaskForTesting
        await fulfillment(of: [entered], timeout: 10)
        XCTAssertTrue(model.isLoading)

        // A newer container supersedes the drain while its page is in core:
        // the main actor is free to do so because the page runs off it.
        model.show(nil)
        await fulfillment(of: [released], timeout: 10)
        await drain?.value

        let calls = probe.calls
        XCTAssertEqual(calls.count, 1, "the cancelled drain requests no further page")
        XCTAssertTrue(calls[0].token.isCancelled(), "superseding the drain cancels its token")
        XCTAssertFalse(calls[0].onMain, "locked 05 §4: the page runs off the main actor")
        XCTAssertTrue(model.fileSummaries.isEmpty, "a cancelled drain publishes nothing")
        XCTAssertNil(model.loadError, "a cancelled drain is not a failure")
        XCTAssertFalse(model.isLoading)
    }

    /// One drain, one token: every page of a multi-page folder carries the
    /// same token, and the drain still completes.
    func testEveryPageOfADrainCarriesTheDrainsToken() async throws {
        let probe = TokenProbe()
        let model = SidebarListPaneModel()
        model.bind(
            SidebarListPaneModel.Dependencies(
                performQuery: { _, _, _, _ in throw VaultError.Cancelled },
                performUntagged: { _ in throw VaultError.Cancelled },
                listLevel: { _, paging, cancel in
                    probe.record(cursor: paging.cursor, token: cancel)
                    let index = paging.cursor.flatMap(Int.init) ?? 0
                    return DirListing(
                        dirs: [],
                        files: FileSummaryPage(
                            items: [Self.summary("f/\(index).md")],
                            nextCursor: index < 2 ? String(index + 1) : nil,
                            totalFiltered: 3))
                }),
            organization: { FileTreeViewModel.OrganizationContext() },
            deviceDefaults: { (2, .standard) })
        model.show(.folder(path: "f"))
        await model.drainTaskForTesting?.value

        XCTAssertEqual(model.fileSummaries.map(\.path), ["f/0.md", "f/1.md", "f/2.md"])
        let calls = probe.calls
        XCTAssertEqual(calls.map(\.cursor), [nil, "1", "2"])
        XCTAssertTrue(calls.allSatisfy { $0.token === calls[0].token })
        XCTAssertTrue(calls.allSatisfy { !$0.onMain })
    }

    private func summary(_ path: String) -> FileSummary { Self.summary(path) }

    private nonisolated static func summary(_ path: String) -> FileSummary {
        FileSummary(
            path: path, name: (path as NSString).lastPathComponent,
            mtimeMs: 0, sizeBytes: 0, isMarkdown: true, displayName: nil,
            createdDate: nil, createdMs: nil, wordCount: nil, preview: nil,
            taskTotal: 0, taskOpen: 0)
    }
}
