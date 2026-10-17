// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// Plan 43 PR 12 (#1324; locked 05 §4): a Base query runs under ONE token
/// per operation, never a fresh `CancelToken()` per call.
///
/// - The prepared refresh that runs off the main actor (the post-write
///   refresh of every visible Base, dashboard section and embedded base) is
///   owned by the document that reserved it: its reservation carries the
///   token into the preparation, and the owner's teardown or next
///   reservation cancels it while the query is in core. The refresh's task
///   cancels every token it holds, too: the vault closing, or the newer
///   refresh that supersedes it.
/// - A document's own execute runs synchronously on the main actor (#1436),
///   so nothing can cancel it while it runs. It holds its token only for the
///   call; once #1436 moves it off the main actor, the teardown that already
///   cancels an execute in flight will reach it.
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

    private func notesQuery(_ session: VaultSession) throws -> String {
        var draft = BaseQueryBuilderDraft()
        draft.source = .folder("Notes")
        return try session.saveQuery(
            name: "Notes", description: nil, queryJson: try draft.queryJSON(),
            sourceSyntax: .builder)
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

    /// A preparation that parks in "core" until its token is cancelled (or
    /// ten seconds pass), then runs the real loader with that token.
    private func installParkingRunner(
        on state: AppState, probe: TokenProbe, entered: XCTestExpectation,
        parksOnlyFirst: Bool = false
    ) {
        state.baseRetargetPreloadRunner = { session, request, observer, cancel in
            let parks = !parksOnlyFirst || probe.tokens.isEmpty
            probe.record(cancel)
            if parks {
                entered.fulfill()
                let deadline = Date().addingTimeInterval(10)
                while !cancel.isCancelled(), Date() < deadline {
                    Thread.sleep(forTimeInterval: 0.001)
                }
            }
            return BasePreparedLoader.prepare(
                session: session, request: request, observer: observer, cancel: cancel)
        }
    }

    // MARK: - The owner's teardown cancels its prepared refresh

    /// The spec's per-owner fact for a document type: a Base's post-write
    /// refresh is prepared off the main actor under a token the document
    /// owns. Closing the document while that query is in core cancels it;
    /// the refresh then publishes nothing into the closed document.
    func testCancellingTheOwnerCancelsItsListingToken() async throws {
        let (state, session) = try await makeAppState()
        state.openBaseFile("Queries/Reading.base")
        let document = state.baseDocument(for: "Queries/Reading.base")
        XCTAssertEqual(document.result?.rows.count, 2)

        let probe = TokenProbe()
        let entered = expectation(description: "the preparation is in core")
        installParkingRunner(on: state, probe: probe, entered: entered)
        _ = try session.createExclusive(path: "Notes/Gamma.md", content: "# Gamma\n")
        let refresh = try XCTUnwrap(
            state.refreshVisibleBasesAfterInAppWrite(
                session: session, changedPath: "Notes/Gamma.md"))
        await fulfillment(of: [entered], timeout: 10)
        let token = try XCTUnwrap(probe.tokens.first)
        XCTAssertTrue(
            document.contentRefreshCancelTokenForTesting === token,
            "the preparation's token is the document's own")
        XCTAssertFalse(token.isCancelled())

        document.close(session: session)
        XCTAssertTrue(token.isCancelled(), "closing the document cancels the query in core")
        XCTAssertNil(document.contentRefreshCancelTokenForTesting)
        let announcements = await refresh.value

        XCTAssertEqual(announcements, [], "a cancelled refresh says nothing")
        XCTAssertNil(document.handle, "the closed document stays closed")
    }

    /// Every owner of a prepared refresh ends it in its teardown, and its
    /// next reservation replaces it: a Base (a load, a retarget, a move to
    /// Trash, close), a dashboard section (close, which every load runs) and
    /// an embedded base's shared handle (close).
    func testEveryOwnerEndsItsPreparedRefreshInItsTeardown() async throws {
        let (_, session) = try await makeAppState()

        let base = BaseDocument(path: "Queries/Reading.base")
        base.load(session: session)
        let first = base.beginContentRefresh(thisPath: nil)
        let second = base.beginContentRefresh(thisPath: nil)
        XCTAssertTrue(first.cancel.isCancelled(), "the next reservation replaces the previous")
        XCTAssertFalse(second.cancel.isCancelled())
        base.load(session: session)
        XCTAssertTrue(second.cancel.isCancelled(), "a load ends the refresh in flight")
        let third = base.beginContentRefresh(thisPath: nil)
        base.retarget(to: "Queries/Reading.base", session: session)
        XCTAssertTrue(third.cancel.isCancelled(), "a retarget ends it")
        let fourth = base.beginContentRefresh(thisPath: nil)
        base.markMovedToTrash(session: session)
        XCTAssertTrue(fourth.cancel.isCancelled(), "a move to Trash ends it")
        XCTAssertNil(base.contentRefreshCancelTokenForTesting)

        let section = DashboardSectionDocument(
            index: 0,
            status: DashboardSectionStatus(
                savedQueryId: try notesQuery(session), savedQueryName: "Notes",
                headingOverride: nil, viewOverride: nil, missing: false))
        section.load(session: session)
        XCTAssertEqual(section.state, .ready)
        let older = try XCTUnwrap(section.beginContentRefresh(thisPath: nil))
        let newer = try XCTUnwrap(section.beginContentRefresh(thisPath: nil))
        XCTAssertTrue(older.cancel.isCancelled())
        section.close(session: session)
        XCTAssertTrue(newer.cancel.isCancelled(), "closing the section ends its refresh")
        XCTAssertNil(section.contentRefreshCancelTokenForTesting)

        let request = try XCTUnwrap(BaseEmbedRequest.wikilinkTarget("Queries/Reading.base"))
        let handle = BaseEmbedHandle(request: request, thisPath: "Notes/Alpha.md")
        let embed = BaseEmbedDocument(
            request: request, thisPath: "Notes/Alpha.md", sharedHandle: handle)
        embed.load(session: session)
        XCTAssertEqual(embed.state, .ready)
        let embedOlder = try XCTUnwrap(handle.beginContentRefresh())
        let embedNewer = try XCTUnwrap(handle.beginContentRefresh())
        XCTAssertTrue(embedOlder.cancel.isCancelled())
        handle.close(session: session)
        XCTAssertTrue(embedNewer.cancel.isCancelled(), "closing the handle ends its refresh")
        XCTAssertNil(handle.contentRefreshCancelTokenForTesting)
    }

    /// The apply ends the operation: a refresh that landed leaves no token
    /// behind on its document.
    func testAnAppliedRefreshLeavesNoTokenBehind() async throws {
        let (state, session) = try await makeAppState()
        state.openBaseFile("Queries/Reading.base")
        let document = state.baseDocument(for: "Queries/Reading.base")
        _ = try session.createExclusive(path: "Notes/Gamma.md", content: "# Gamma\n")
        let refresh = try XCTUnwrap(
            state.refreshVisibleBasesAfterInAppWrite(
                session: session, changedPath: "Notes/Gamma.md"))
        _ = await refresh.value
        XCTAssertEqual(document.result?.rows.count, 3)
        XCTAssertNil(document.contentRefreshCancelTokenForTesting)
    }

    // MARK: - A document's own execute

    /// A document's execute runs synchronously on the main actor (#1436), so
    /// nothing can cancel it mid-query; it holds its token for the call only
    /// and leaves none behind, on a Base, an embedded base and a dashboard
    /// section alike.
    func testAnExecuteHoldsItsTokenOnlyWhileItRuns() async throws {
        let (_, session) = try await makeAppState()
        let base = BaseDocument(path: "Queries/Reading.base")
        base.load(session: session)
        XCTAssertEqual(base.state, .ready)
        XCTAssertNil(base.executeCancelTokenForTesting)
        base.selectView(index: 1, session: session)
        XCTAssertEqual(base.activeViewName, "Done")
        XCTAssertNil(base.executeCancelTokenForTesting)

        let request = try XCTUnwrap(BaseEmbedRequest.wikilinkTarget("Queries/Reading.base"))
        let embed = BaseEmbedDocument(request: request, thisPath: "Notes/Alpha.md")
        embed.load(session: session)
        XCTAssertEqual(embed.state, .ready)
        XCTAssertNil(embed.executeCancelTokenForTesting)

        let section = DashboardSectionDocument(
            index: 0,
            status: DashboardSectionStatus(
                savedQueryId: try notesQuery(session), savedQueryName: "Notes",
                headingOverride: nil, viewOverride: nil, missing: false))
        section.load(session: session)
        XCTAssertEqual(section.state, .ready)
        XCTAssertNil(section.executeCancelTokenForTesting)
    }

    // MARK: - The refresh's task

    /// The vault closing cancels the refresh's task, and its handler cancels
    /// every preparation's token: a preparation parked in core stops, and
    /// the cancelled refresh publishes nothing.
    func testCancellingAVisibleBasesRefreshCancelsItsPreparationToken() async throws {
        let (state, session) = try await makeAppState()
        state.openBaseFile("Queries/Reading.base")
        let document = state.baseDocument(for: "Queries/Reading.base")
        XCTAssertEqual(document.result?.rows.count, 2)

        let probe = TokenProbe()
        let entered = expectation(description: "the preparation is in core")
        installParkingRunner(on: state, probe: probe, entered: entered)
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
        installParkingRunner(on: state, probe: probe, entered: entered, parksOnlyFirst: true)
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
        XCTAssertFalse(tokens[1] === tokens[0], "one token per preparation")
        XCTAssertEqual(supersededAnnouncements, [], "a superseded refresh says nothing")
        XCTAssertEqual(document.result?.rows.count, 3, "the newer refresh's rows land")
        XCTAssertEqual(document.state, .ready)
    }
}
