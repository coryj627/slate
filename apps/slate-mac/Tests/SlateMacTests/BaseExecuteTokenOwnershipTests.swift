// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// Plan 43 PR 12 (#1324; locked 05 §4): a Base query runs under ONE token
/// per operation, never a fresh `CancelToken()` per call.
///
/// - A document's own execute (a Base tab, an embedded base, a dashboard
///   section) holds its token on the document: the next execute replaces and
///   cancels it, and the document's teardown (close, retarget, a move to
///   Trash) cancels it. These executes still run synchronously on the main
///   actor (#1436), so the token cannot interrupt one already running; it
///   ends with its owner.
/// - The prepared loads that run off the main actor (the post-write refresh
///   of every visible Base, and a retarget's preparation) carry the token of
///   the task that runs them, cancelled through `withTaskCancellationHandler`
///   — by the vault closing, and for a refresh by the newer refresh that
///   supersedes it.
@MainActor
final class BaseExecuteTokenOwnershipTests: XCTestCase {
    private var tempDirectory: URL!

    override func setUpWithError() throws {
        try super.setUpWithError()
        tempDirectory = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-base-token-\(UUID().uuidString)")
        try FileManager.default.createDirectory(
            at: tempDirectory, withIntermediateDirectories: true)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: tempDirectory)
        try super.tearDownWithError()
    }

    /// One two-view Base over two notes.
    private func makeAppState() async throws -> (AppState, VaultSession) {
        let vault = tempDirectory.appendingPathComponent("vault-\(UUID().uuidString)")
        try FileManager.default.createDirectory(
            at: vault.appendingPathComponent("Queries"), withIntermediateDirectories: true)
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
              - type: table
                name: Done
                filters: "status == \"done\""
                order:
                  - file.name
            """#.utf8
        ).write(to: vault.appendingPathComponent("Queries/Reading.base"))
        try Data("---\nstatus: active\n---\n# Alpha\n".utf8)
            .write(to: vault.appendingPathComponent("Notes/Alpha.md"))
        try Data("---\nstatus: done\n---\n# Beta\n".utf8)
            .write(to: vault.appendingPathComponent("Notes/Beta.md"))
        let store = RecentVaultsStore(
            fileURL: tempDirectory.appendingPathComponent("recents-\(UUID().uuidString).json"))
        let state = AppState(recentsStore: store, externalOpener: { _ in true })
        state.openVault(at: vault)
        await state.scanTask?.value
        return (state, try XCTUnwrap(state.currentSession))
    }

    /// Tokens the native runner saw, written from the worker.
    private final class TokenProbe: @unchecked Sendable {
        private let lock = NSLock()
        private var stored: [CancelToken] = []

        func record(_ token: CancelToken) {
            lock.lock()
            stored.append(token)
            lock.unlock()
        }

        var tokens: [CancelToken] {
            lock.lock()
            defer { lock.unlock() }
            return stored
        }
    }

    // MARK: - A document's own execute

    /// The spec's per-owner fact for a document type: the token a Base tab's
    /// execute passed to core is the document's, the next execute replaces
    /// and cancels it, and closing the document cancels the current one.
    func testCancellingTheOwnerCancelsItsListingToken() async throws {
        let (_, session) = try await makeAppState()
        let document = BaseDocument(path: "Queries/Reading.base")
        document.load(session: session)
        XCTAssertEqual(document.state, .ready)
        XCTAssertEqual(document.result?.rows.count, 2)

        let first = try XCTUnwrap(
            document.executeCancelTokenForTesting, "an execute holds its token on its document")
        XCTAssertFalse(first.isCancelled())

        document.selectView(index: 1, session: session)
        let second = try XCTUnwrap(document.executeCancelTokenForTesting)
        XCTAssertFalse(second === first, "one token per execute")
        XCTAssertTrue(first.isCancelled(), "the next execute cancels the previous token")
        XCTAssertFalse(second.isCancelled())

        document.close(session: session)
        XCTAssertTrue(second.isCancelled(), "closing the document cancels its token")
        XCTAssertNil(document.executeCancelTokenForTesting)
    }

    /// A retarget and a move to Trash end the document's operation too.
    func testRetargetAndTrashCancelTheDocumentsToken() async throws {
        let (_, session) = try await makeAppState()
        let document = BaseDocument(path: "Queries/Reading.base")
        document.load(session: session)
        let loaded = try XCTUnwrap(document.executeCancelTokenForTesting)

        document.retarget(to: "Queries/Reading.base", session: session)
        XCTAssertTrue(loaded.isCancelled(), "a retarget ends the old operation")
        let reloaded = try XCTUnwrap(document.executeCancelTokenForTesting)
        XCTAssertFalse(reloaded.isCancelled(), "the retarget's own load holds a live token")

        document.markMovedToTrash(session: session)
        XCTAssertTrue(reloaded.isCancelled(), "a move to Trash ends the operation")
        XCTAssertNil(document.executeCancelTokenForTesting)
    }

    func testClosingAnEmbeddedBaseCancelsItsExecuteToken() async throws {
        let (_, session) = try await makeAppState()
        let request = try XCTUnwrap(BaseEmbedRequest.wikilinkTarget("Queries/Reading.base"))
        let document = BaseEmbedDocument(request: request, thisPath: "Notes/Alpha.md")
        document.load(session: session)
        XCTAssertEqual(document.state, .ready)
        let first = try XCTUnwrap(document.executeCancelTokenForTesting)

        document.selectView(index: 1, session: session)
        let second = try XCTUnwrap(document.executeCancelTokenForTesting)
        XCTAssertTrue(first.isCancelled(), "the next execute cancels the previous token")
        XCTAssertFalse(second.isCancelled())

        document.close(session: session)
        XCTAssertTrue(second.isCancelled(), "closing the embed cancels its token")
        XCTAssertNil(document.executeCancelTokenForTesting)
    }

    func testClosingADashboardSectionCancelsItsExecuteToken() async throws {
        let (_, session) = try await makeAppState()
        var draft = BaseQueryBuilderDraft()
        draft.source = .folder("Notes")
        let queryID = try session.saveQuery(
            name: "Notes", description: nil, queryJson: try draft.queryJSON(),
            sourceSyntax: .builder)
        let section = DashboardSectionDocument(
            index: 0,
            status: DashboardSectionStatus(
                savedQueryId: queryID, savedQueryName: "Notes",
                headingOverride: nil, viewOverride: nil, missing: false))
        section.load(session: session)
        XCTAssertEqual(section.state, .ready)
        let first = try XCTUnwrap(section.executeCancelTokenForTesting)

        section.refresh(session: session)
        let second = try XCTUnwrap(section.executeCancelTokenForTesting)
        XCTAssertTrue(first.isCancelled(), "the next execute cancels the previous token")
        XCTAssertFalse(second.isCancelled())

        section.close(session: session)
        XCTAssertTrue(second.isCancelled(), "closing the section cancels its token")
        XCTAssertNil(section.executeCancelTokenForTesting)
    }

    // MARK: - The prepared loads off the main actor

    /// The post-write refresh prepares every visible Base under the refresh
    /// task's ONE token; the vault closing cancels the task, the handler
    /// cancels the token, and a preparation parked in core stops. The
    /// cancelled refresh publishes nothing.
    func testCancellingAVisibleBasesRefreshCancelsItsPreparationToken() async throws {
        let (state, session) = try await makeAppState()
        state.openBaseFile("Queries/Reading.base")
        let document = state.baseDocument(for: "Queries/Reading.base")
        XCTAssertEqual(document.result?.rows.count, 2)

        let probe = TokenProbe()
        let entered = expectation(description: "the preparation is in core")
        state.baseRetargetPreloadRunner = { session, request, observer, cancel in
            probe.record(cancel)
            entered.fulfill()
            let deadline = Date().addingTimeInterval(10)
            while !cancel.isCancelled(), Date() < deadline {
                Thread.sleep(forTimeInterval: 0.001)
            }
            return BasePreparedLoader.prepare(
                session: session, request: request, observer: observer, cancel: cancel)
        }
        _ = try session.createExclusive(path: "Notes/Gamma.md", content: "# Gamma\n")
        let refresh = try XCTUnwrap(
            state.refreshVisibleBasesAfterInAppWrite(
                session: session, changedPath: "Notes/Gamma.md"))
        await fulfillment(of: [entered], timeout: 10)

        state.resetBaseRefreshTasksForVaultTransition()
        let announcements = await refresh.value

        let token = try XCTUnwrap(probe.tokens.first)
        XCTAssertTrue(token.isCancelled(), "cancelling the refresh cancels its token")
        XCTAssertEqual(announcements, [], "a cancelled refresh says nothing")
        XCTAssertEqual(document.state, .ready, "a cancelled refresh is not a failure")
        XCTAssertEqual(document.result?.rows.count, 2, "and publishes no rows")
    }

    /// A newer refresh supersedes the older one, whose rows could never
    /// publish: it cancels the older refresh's token, and the newer one's
    /// rows land.
    func testANewerRefreshCancelsTheSupersededRefreshsToken() async throws {
        let (state, session) = try await makeAppState()
        state.openBaseFile("Queries/Reading.base")
        let document = state.baseDocument(for: "Queries/Reading.base")

        let probe = TokenProbe()
        let entered = expectation(description: "the first preparation is in core")
        state.baseRetargetPreloadRunner = { session, request, observer, cancel in
            let isFirst = probe.tokens.isEmpty
            probe.record(cancel)
            if isFirst {
                entered.fulfill()
                let deadline = Date().addingTimeInterval(10)
                while !cancel.isCancelled(), Date() < deadline {
                    Thread.sleep(forTimeInterval: 0.001)
                }
            }
            return BasePreparedLoader.prepare(
                session: session, request: request, observer: observer, cancel: cancel)
        }
        _ = try session.createExclusive(path: "Notes/Gamma.md", content: "# Gamma\n")
        let superseded = try XCTUnwrap(
            state.refreshVisibleBasesAfterInAppWrite(
                session: session, changedPath: "Notes/Gamma.md"))
        await fulfillment(of: [entered], timeout: 10)

        let newer = try XCTUnwrap(
            state.refreshVisibleBasesAfterInAppWrite(
                session: session, changedPath: "Notes/Gamma.md"))
        let supersededAnnouncements = await superseded.value
        _ = await newer.value

        let tokens = probe.tokens
        XCTAssertEqual(tokens.count, 2)
        XCTAssertTrue(tokens[0].isCancelled(), "the newer refresh cancels the older token")
        XCTAssertFalse(tokens[1] === tokens[0], "one token per refresh")
        XCTAssertEqual(supersededAnnouncements, [], "a superseded refresh says nothing")
        XCTAssertEqual(document.result?.rows.count, 3, "the newer refresh's rows land")
        XCTAssertEqual(document.state, .ready)
    }
}
