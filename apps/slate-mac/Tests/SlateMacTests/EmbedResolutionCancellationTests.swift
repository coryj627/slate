// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// #1287 (contract 40 AR-24; locked 05 §4): mac's two production embed
/// walks — the batch in `loadCurrentNoteEmbedResolutions` and the single
/// request in `requestReadingEmbedResolution` — each hold one `CancelToken`
/// per batch or request, cancelled when their task is cancelled or the
/// selection changes, and core's `resolve_embed` stops at its next node.
/// A cancelled walk is a dropped result: it never publishes, and it never
/// becomes an `.unresolved(.readError)` card VoiceOver would read.
///
/// The walks are parked deterministically through
/// `AppState.embedResolveGateForTesting`, which runs on the walk's worker
/// right before each `resolveEmbed`; the cancelled walks still go through
/// the real core resolver, so `.Cancelled` comes from core itself.
@MainActor
final class EmbedResolutionCancellationTests: XCTestCase {
    private var tempDirs: [URL] = []

    override func tearDown() {
        for dir in tempDirs {
            try? FileManager.default.removeItem(at: dir)
        }
        tempDirs = []
        super.tearDown()
    }

    /// Records every resolve the embed walks make, with the token each one
    /// walked under, and parks the first resolve of one (host, target) pair
    /// until that walk's token is cancelled. Every wait is bounded, so a
    /// token nobody cancels fails the fact instead of hanging the lane.
    private actor EmbedWalkProbe {
        struct Resolve {
            let host: String
            let target: String
            let token: CancelToken
        }

        private let parkHost: String?
        private let parkTarget: String?
        private(set) var resolves: [Resolve] = []
        private(set) var parkedToken: CancelToken?
        private(set) var parkTimedOut = false

        init(parkHost: String? = nil, parkTarget: String? = nil) {
            self.parkHost = parkHost
            self.parkTarget = parkTarget
        }

        func gate(host: String, target: String, cancel: CancelToken) async {
            resolves.append(Resolve(host: host, target: target, token: cancel))
            guard host == parkHost, target == parkTarget, parkedToken == nil else { return }
            parkedToken = cancel
            let deadline = ContinuousClock.now + .seconds(10)
            while !cancel.isCancelled() {
                if ContinuousClock.now >= deadline {
                    parkTimedOut = true
                    return
                }
                try? await Task.sleep(for: .milliseconds(2))
            }
        }

        /// The parked walk's token once a walk has parked; nil after the
        /// bound if none did.
        func waitUntilParked() async -> CancelToken? {
            let deadline = ContinuousClock.now + .seconds(10)
            while parkedToken == nil, ContinuousClock.now < deadline {
                try? await Task.sleep(for: .milliseconds(2))
            }
            return parkedToken
        }

        func targets(forHost host: String) -> [String] {
            resolves.filter { $0.host == host }.map(\.target)
        }

        func tokens(forHost host: String) -> [CancelToken] {
            resolves.filter { $0.host == host }.map(\.token)
        }
    }

    private func openVault(files: [String: String]) async throws -> AppState {
        let dir = FileManager.default.temporaryDirectory
            .appendingPathComponent("embed-cancel-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        tempDirs.append(dir)
        for (path, body) in files {
            try Data(body.utf8).write(to: dir.appendingPathComponent(path))
        }
        let state = AppState(
            recentsStore: nil,
            externalOpener: { _ in true },
            preferencesStore: PreferencesStore(
                defaults: UserDefaults(suiteName: UUID().uuidString)!)
        )
        state.openVault(at: dir)
        await state.scanTask?.value
        return state
    }

    private func install(_ probe: EmbedWalkProbe, on state: AppState) {
        state.embedResolveGateForTesting = { host, target, cancel in
            await probe.gate(host: host, target: target, cancel: cancel)
        }
    }

    /// Select `path` and wait for its links leg and the embed batch it
    /// chains to settle.
    private func select(_ path: String, on state: AppState) async {
        state.selectedFilePath = path
        await state.linksLoadTask?.value
        await state.embedsLoadTask?.value
    }

    private static func unresolvedKeys(_ resolutions: [String: EmbedResolution]) -> [String] {
        resolutions.compactMap { key, resolution in
            if case .unresolved = resolution { return key }
            return nil
        }.sorted()
    }

    // MARK: - The batch

    /// A selection change cancels the token of the batch still walking the
    /// note the user left: the walk stops at its next node (the embeds after
    /// the parked one are never walked), publishes nothing, and leaves no
    /// unresolved row behind; the new note's own batch lands.
    func testASelectionChangeCancelsTheInFlightBatchToken() async throws {
        let state = try await openVault(files: [
            "a.md": "![[t1]]\n\n![[t2]]\n\n![[t3]]\n",
            "b.md": "![[t1]]\n",
            "t1.md": "# T1\n\nbody 1\n",
            "t2.md": "# T2\n\nbody 2\n",
            "t3.md": "# T3\n\nbody 3\n",
        ])
        let probe = EmbedWalkProbe(parkHost: "a.md", parkTarget: "t1")
        install(probe, on: state)

        state.selectedFilePath = "a.md"
        let chainA = state.linksLoadTask
        let parked = await probe.waitUntilParked()
        let token = try XCTUnwrap(parked, "a.md's batch reached its first resolve")
        XCTAssertFalse(token.isCancelled(), "the batch's token is live while it walks")
        XCTAssertTrue(
            state.embedsLoadCancelToken === token,
            "the state holds the in-flight batch's token, so the next load can cancel it")

        state.selectedFilePath = "b.md"
        XCTAssertTrue(
            token.isCancelled(), "the selection change cancelled the in-flight batch's token")
        XCTAssertNil(
            state.embedsLoadCancelToken,
            "the selection change cancelled and released the token itself, not only the task")

        // a.md's links leg awaits its chained batch, so this is that batch's end.
        await chainA?.value
        await state.linksLoadTask?.value
        await state.embedsLoadTask?.value

        let timedOut = await probe.parkTimedOut
        XCTAssertFalse(timedOut, "the parked walk saw its cancel")
        let walkedForA = await probe.targets(forHost: "a.md")
        XCTAssertEqual(
            walkedForA, ["t1"],
            "the cancelled batch stopped at its next node: t2 and t3 were never walked")
        let resolutions = state.currentNoteEmbedResolutions
        XCTAssertEqual(
            Set(resolutions.keys), ["t1"],
            "b.md's own batch landed and a.md's cancelled batch published nothing")
        XCTAssertEqual(
            Self.unresolvedKeys(resolutions), [],
            "a cancelled walk never becomes an unresolved row")
        guard case .fullNote(let targetPath, _, _) = resolutions["t1"] else {
            return XCTFail("expected b.md's t1 card, got \(String(describing: resolutions["t1"]))")
        }
        XCTAssertEqual(targetPath, "t1.md")
        XCTAssertFalse(state.isLoadingEmbeds)
        XCTAssertNil(state.embedsLoadCancelToken, "b.md's batch released its token when it ended")
    }

    /// The batch's own task is the token's other canceller: cancelling the
    /// task cancels the token through `withTaskCancellationHandler`, the walk
    /// stops at its next node, and the rows already on screen stay as they
    /// were — no unresolved row replaces them.
    func testCancellingTheBatchTaskCancelsItsToken() async throws {
        let state = try await openVault(files: [
            "host.md": "![[t1]]\n\n![[t2]]\n",
            "t1.md": "# T1\n\nbody 1\n",
            "t2.md": "# T2\n\nbody 2\n",
        ])
        await select("host.md", on: state)
        let settled = state.currentNoteEmbedResolutions
        XCTAssertEqual(Set(settled.keys), ["t1", "t2"])

        let probe = EmbedWalkProbe(parkHost: "host.md", parkTarget: "t1")
        install(probe, on: state)
        let batch = Task { await state.loadCurrentNoteEmbedResolutions(path: "host.md") }
        let parked = await probe.waitUntilParked()
        let token = try XCTUnwrap(parked, "the batch reached its first resolve")
        XCTAssertTrue(state.embedsLoadCancelToken === token)

        batch.cancel()
        XCTAssertTrue(
            token.isCancelled(), "cancelling the batch's task cancelled its token")
        await batch.value

        let timedOut = await probe.parkTimedOut
        XCTAssertFalse(timedOut, "the parked walk saw its cancel")
        let walked = await probe.targets(forHost: "host.md")
        XCTAssertEqual(walked, ["t1"], "the cancelled batch stopped at its next node")
        XCTAssertEqual(
            state.currentNoteEmbedResolutions, settled,
            "the cancelled batch published nothing over the rows already on screen")
        XCTAssertEqual(Self.unresolvedKeys(state.currentNoteEmbedResolutions), [])
        XCTAssertNil(state.embedsLoadCancelToken, "the cancelled batch released its token")
    }

    /// The next batch cancels the previous batch's token (the state keeps
    /// it for exactly this): the superseded walk stops and drops its rows,
    /// and only the newer batch publishes.
    func testANewBatchCancelsThePreviousBatchToken() async throws {
        let state = try await openVault(files: [
            "host.md": "![[t1]]\n\n![[t2]]\n",
            "t1.md": "# T1\n\nbody 1\n",
            "t2.md": "# T2\n\nbody 2\n",
        ])
        await select("host.md", on: state)

        let probe = EmbedWalkProbe(parkHost: "host.md", parkTarget: "t1")
        install(probe, on: state)
        let first = Task { await state.loadCurrentNoteEmbedResolutions(path: "host.md") }
        let parked = await probe.waitUntilParked()
        let firstToken = try XCTUnwrap(parked, "the first batch reached its first resolve")

        let second = Task { await state.loadCurrentNoteEmbedResolutions(path: "host.md") }
        await second.value
        XCTAssertTrue(
            firstToken.isCancelled(), "the next batch cancelled the previous batch's token")
        await first.value

        let timedOut = await probe.parkTimedOut
        XCTAssertFalse(timedOut, "the parked walk saw its cancel")
        let walked = await probe.targets(forHost: "host.md")
        XCTAssertEqual(
            walked, ["t1", "t1", "t2"],
            "the superseded batch stopped at its next node; the newer one walked both")
        let tokens = await probe.tokens(forHost: "host.md")
        XCTAssertTrue(tokens[1] === tokens[2], "one token for the newer batch")
        XCTAssertFalse(tokens[1] === firstToken, "a new batch walks under a new token")
        XCTAssertFalse(tokens[1].isCancelled())

        let resolutions = state.currentNoteEmbedResolutions
        XCTAssertEqual(Set(resolutions.keys), ["t1", "t2"])
        XCTAssertEqual(Self.unresolvedKeys(resolutions), [])
        XCTAssertFalse(state.isLoadingEmbeds)
        XCTAssertNil(state.embedsLoadCancelToken)
    }

    /// Regression: a batch nobody cancels still publishes every embed shape
    /// (a real unresolved target included) under one live token, and a
    /// single request still merges onto its rows under a token of its own.
    func testEmbedResolutionsStillMergeAfterANormalBatch() async throws {
        let state = try await openVault(files: [
            "host.md": "![[target]]\n\n![[target#Part]]\n\n![[target^blk]]\n\n![[missing]]\n",
            "target.md": "# Part\n\nPlain text. ^blk\n",
            "second.md": "# Second\n\nmore\n",
        ])
        let probe = EmbedWalkProbe()
        install(probe, on: state)
        await select("host.md", on: state)

        var resolutions = state.currentNoteEmbedResolutions
        XCTAssertEqual(
            Set(resolutions.keys),
            ["target", "target#Part", "target^blk", "target#^blk", "missing"])
        guard case .fullNote(let notePath, _, _) = resolutions["target"] else {
            return XCTFail("expected a full-note card, got \(String(describing: resolutions["target"]))")
        }
        XCTAssertEqual(notePath, "target.md")
        guard case .section(_, let heading, _, _) = resolutions["target#Part"] else {
            return XCTFail("expected a section card, got \(String(describing: resolutions["target#Part"]))")
        }
        XCTAssertEqual(heading, "Part")
        guard case .block(_, let blockId, _) = resolutions["target^blk"] else {
            return XCTFail("expected a block card, got \(String(describing: resolutions["target^blk"]))")
        }
        XCTAssertEqual(blockId, "blk")
        XCTAssertEqual(resolutions["target#^blk"], resolutions["target^blk"])
        guard case .unresolved(.targetNotFound(let missing)) = resolutions["missing"] else {
            return XCTFail("expected TargetNotFound, got \(String(describing: resolutions["missing"]))")
        }
        XCTAssertEqual(missing, "missing")
        XCTAssertFalse(state.isLoadingEmbeds)
        XCTAssertNil(state.embedsLoadCancelToken, "the batch released its token when it ended")

        let batchTokens = await probe.tokens(forHost: "host.md")
        XCTAssertEqual(batchTokens.count, 4, "one resolve per authored embed")
        let batchToken = try XCTUnwrap(batchTokens.first)
        XCTAssertTrue(batchTokens.allSatisfy { $0 === batchToken }, "one token per batch")
        XCTAssertFalse(batchToken.isCancelled())

        await state.requestReadingEmbedResolution(target: "second")
        resolutions = state.currentNoteEmbedResolutions
        XCTAssertEqual(
            Set(resolutions.keys),
            ["target", "target#Part", "target^blk", "target#^blk", "missing", "second"],
            "the single request merged onto the batch's rows, replacing none")
        guard case .fullNote(let secondPath, _, _) = resolutions["second"] else {
            return XCTFail("expected the request's card, got \(String(describing: resolutions["second"]))")
        }
        XCTAssertEqual(secondPath, "second.md")
        XCTAssertTrue(state.readingEmbedRequestCancelTokens.isEmpty, "the request released its token")
        let allTokens = await probe.tokens(forHost: "host.md")
        XCTAssertEqual(allTokens.count, 5, "the batch's four resolves and the request's one")
        let requestToken = try XCTUnwrap(allTokens.last)
        XCTAssertFalse(requestToken === batchToken, "the request walks under its own token")
        XCTAssertFalse(requestToken.isCancelled())
    }

    // MARK: - The single request

    /// Cancelling the requesting task — the reading view's placeholder went
    /// away — cancels the request's token, and the cancelled request writes
    /// nothing: no card, and no unresolved row. The same request with a live
    /// token lands the card, so the cancel is the only thing that stopped it.
    func testACancelledRequestIsDroppedNotUnresolved() async throws {
        let state = try await openVault(files: [
            "host.md": "host body\n",
            "target.md": "# Target\n\nbody text\n",
        ])
        await select("host.md", on: state)

        let probe = EmbedWalkProbe(parkHost: "host.md", parkTarget: "target")
        install(probe, on: state)
        let request = Task { await state.requestReadingEmbedResolution(target: "target") }
        let parked = await probe.waitUntilParked()
        let token = try XCTUnwrap(parked, "the request reached its resolve")
        XCTAssertTrue(
            state.readingEmbedRequestCancelTokens.values.contains { $0 === token },
            "the state holds the in-flight request's token")

        request.cancel()
        XCTAssertTrue(token.isCancelled(), "cancelling the requesting task cancelled its token")
        await request.value

        let timedOut = await probe.parkTimedOut
        XCTAssertFalse(timedOut, "the parked walk saw its cancel")
        XCTAssertNil(
            state.currentNoteEmbedResolutions["target"],
            "a cancelled request is dropped, never an unresolved row")
        XCTAssertEqual(Self.unresolvedKeys(state.currentNoteEmbedResolutions), [])
        XCTAssertTrue(state.readingEmbedRequestCancelTokens.isEmpty, "the request released its token")

        state.embedResolveGateForTesting = nil
        await state.requestReadingEmbedResolution(target: "target")
        guard case .fullNote(let targetPath, _, _) = state.currentNoteEmbedResolutions["target"] else {
            return XCTFail(
                "a live request lands the card, got \(String(describing: state.currentNoteEmbedResolutions["target"]))")
        }
        XCTAssertEqual(targetPath, "target.md")
    }

    /// A selection change cancels every in-flight request's token too: the
    /// walk for the note the user left stops, and nothing it would have
    /// resolved lands beside the new note's rows.
    func testASelectionChangeCancelsTheInFlightRequestToken() async throws {
        let state = try await openVault(files: [
            "host.md": "host body\n",
            "target.md": "# Target\n\nbody text\n",
            "other.md": "other body\n",
        ])
        await select("host.md", on: state)

        let probe = EmbedWalkProbe(parkHost: "host.md", parkTarget: "target")
        install(probe, on: state)
        let request = Task { await state.requestReadingEmbedResolution(target: "target") }
        let parked = await probe.waitUntilParked()
        let token = try XCTUnwrap(parked, "the request reached its resolve")

        state.selectedFilePath = "other.md"
        XCTAssertTrue(
            token.isCancelled(), "the selection change cancelled the in-flight request's token")
        XCTAssertTrue(
            state.readingEmbedRequestCancelTokens.isEmpty,
            "the selection change released every request token")
        await request.value
        await state.linksLoadTask?.value
        await state.embedsLoadTask?.value

        let timedOut = await probe.parkTimedOut
        XCTAssertFalse(timedOut, "the parked walk saw its cancel")
        XCTAssertNil(state.currentNoteEmbedResolutions["target"])
        XCTAssertEqual(Self.unresolvedKeys(state.currentNoteEmbedResolutions), [])
    }
}
