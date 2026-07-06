// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// #638: live sync-marker re-detection — the bounded directory watch
/// (SyncMarkerWatcher unit behavior) and the AppState wiring that
/// turns a mid-session marker appearance into a refreshed report and
/// exactly one assertive announcement for newly-risky states.
///
/// Real-FS DispatchSource events need generous timeouts on loaded CI
/// runners; every wait here is an upper BOUND (expectations fulfill
/// early), so green runs stay fast.
@MainActor
final class SyncMarkerWatcherTests: XCTestCase {

    private var tempDir: URL!

    override func setUpWithError() throws {
        try super.setUpWithError()
        tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-marker-watch-\(UUID().uuidString)")
        try FileManager.default.createDirectory(
            at: tempDir, withIntermediateDirectories: true)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: tempDir)
        try super.tearDownWithError()
    }

    private func makeVaultDir(_ name: String) throws -> URL {
        let url = tempDir.appendingPathComponent(name)
        try FileManager.default.createDirectory(at: url, withIntermediateDirectories: true)
        return url
    }

    // MARK: - Watcher unit behavior

    func testRootMarkerCreationFiresDebouncedCallback() throws {
        let vault = try makeVaultDir("root-marker")
        let fired = expectation(description: "onChange fired")
        fired.assertForOverFulfill = false
        let watcher = SyncMarkerWatcher(root: vault, debounceInterval: 0.2) {
            fired.fulfill()
        }
        watcher.start()
        // Give the watch a beat to arm before mutating.
        Thread.sleep(forTimeInterval: 0.2)
        try Data().write(to: vault.appendingPathComponent(".stignore"))
        wait(for: [fired], timeout: 10)
        watcher.stop()
    }

    func testEventBurstCoalescesIntoOneCallback() throws {
        let vault = try makeVaultDir("burst")
        let counter = CallbackCounter()
        let watcher = SyncMarkerWatcher(root: vault, debounceInterval: 0.5) {
            counter.increment()
        }
        watcher.start()
        Thread.sleep(forTimeInterval: 0.2)
        // A burst of entry-list churn well inside one debounce window
        // (the note-save temp+rename pattern).
        for i in 0..<8 {
            try Data("x".utf8).write(to: vault.appendingPathComponent("note-\(i).md"))
        }
        // One debounce window + slack: exactly one callback.
        let settled = expectation(description: "debounce settled")
        DispatchQueue.main.asyncAfter(deadline: .now() + 1.5) { settled.fulfill() }
        wait(for: [settled], timeout: 10)
        XCTAssertEqual(counter.count, 1, "burst must coalesce into one callback")
        watcher.stop()
    }

    func testPluginsDirCreatedMidSessionIsPickedUpByRearm() throws {
        // The chain: root fires on `.obsidian` creation → re-arm picks
        // up `.obsidian` → fires on `plugins` creation → re-arm picks
        // up `plugins` → fires on the LiveSync dir landing inside it.
        // Three sequential debounced callbacks prove each hop armed.
        let vault = try makeVaultDir("chain")
        let counter = CallbackCounter()
        let watcher = SyncMarkerWatcher(root: vault, debounceInterval: 0.2) {
            counter.increment()
        }
        watcher.start()
        Thread.sleep(forTimeInterval: 0.2)

        let fm = FileManager.default
        try fm.createDirectory(
            at: vault.appendingPathComponent(".obsidian"),
            withIntermediateDirectories: false)
        try waitUntil("root hop fired") { counter.count >= 1 }

        try fm.createDirectory(
            at: vault.appendingPathComponent(".obsidian/plugins"),
            withIntermediateDirectories: false)
        try waitUntil(".obsidian hop fired") { counter.count >= 2 }

        try fm.createDirectory(
            at: vault.appendingPathComponent(".obsidian/plugins/obsidian-livesync"),
            withIntermediateDirectories: false)
        try waitUntil("plugins hop fired") { counter.count >= 3 }

        watcher.stop()
    }

    func testStopSuppressesFurtherCallbacks() throws {
        let vault = try makeVaultDir("stopped")
        let counter = CallbackCounter()
        let watcher = SyncMarkerWatcher(root: vault, debounceInterval: 0.2) {
            counter.increment()
        }
        watcher.start()
        Thread.sleep(forTimeInterval: 0.2)
        watcher.stop()
        try Data().write(to: vault.appendingPathComponent(".stignore"))
        let settled = expectation(description: "quiet after stop")
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.8) { settled.fulfill() }
        wait(for: [settled], timeout: 10)
        XCTAssertEqual(counter.count, 0, "stop() must suppress callbacks")
    }

    func testWatcherDoesNotRetainItselfAfterStop() throws {
        let vault = try makeVaultDir("dealloc")
        var watcher: SyncMarkerWatcher? = SyncMarkerWatcher(
            root: vault, debounceInterval: 0.2
        ) {}
        weak var weakRef = watcher
        watcher?.start()
        Thread.sleep(forTimeInterval: 0.2)
        watcher?.stop()
        watcher = nil
        XCTAssertNil(weakRef, "sources/timer must not retain the watcher")
    }

    // MARK: - Anti-starvation ceiling (#638 adversarial)

    /// Continuous sub-interval churn (a busy sync tool, a Cmd+S habit)
    /// used to reset the trailing timer forever — the callback never
    /// fired. The max-latency ceiling forces a callback within a fixed
    /// bound of the FIRST event even while churn keeps coming.
    func testMaxLatencyCeilingFiresUnderContinuousChurn() throws {
        let vault = try makeVaultDir("starve")
        let fired = expectation(description: "ceiling forced a callback")
        fired.assertForOverFulfill = false
        // Debounce 0.3s, ceiling 0.6s. We churn every ~0.1s (well under
        // the debounce) for ~1s straight; a pure trailing debounce
        // would never settle, so any fulfillment proves the ceiling.
        let watcher = SyncMarkerWatcher(
            root: vault, debounceInterval: 0.3, maxLatency: 0.6
        ) {
            fired.fulfill()
        }
        watcher.start()
        Thread.sleep(forTimeInterval: 0.2)  // let the root watch arm

        // Drive churn on a background queue so the main thread is free
        // to service the wait; keep it up past the ceiling.
        let churn = DispatchQueue(label: "test.churn")
        var stop = false
        let stopLock = NSLock()
        func shouldStop() -> Bool { stopLock.lock(); defer { stopLock.unlock() }; return stop }
        churn.async {
            var i = 0
            while !shouldStop() {
                try? Data("x".utf8).write(
                    to: vault.appendingPathComponent("churn-\(i).md"))
                i += 1
                Thread.sleep(forTimeInterval: 0.1)
            }
        }
        // Ceiling is 0.6s from the first event; 5s is a generous BOUND.
        wait(for: [fired], timeout: 5)
        stopLock.lock(); stop = true; stopLock.unlock()
        watcher.stop()
    }

    // MARK: - AppState wiring (#638 end-to-end)

    private final class RecordingAnnouncer: AnnouncementPosting {
        private(set) var posts: [(message: String, priority: AnnouncementPriority)] = []
        func post(_ message: String, priority: AnnouncementPriority) {
            posts.append((message, priority))
        }
    }

    private func makeAppState(announcer: AnnouncementPosting) -> AppState {
        let state = AppState(
            recentsStore: RecentVaultsStore(
                fileURL: tempDir.appendingPathComponent("recents-\(UUID().uuidString).json")),
            externalOpener: { _ in true },
            announcer: announcer
        )
        state.syncMarkerWatcherDebounce = 0.2
        return state
    }

    /// git init mid-session shows up in the report without a manual
    /// refresh — the issue's headline scenario.
    func testMarkerAppearingMidSessionRefreshesReport() async throws {
        let vault = try makeVaultDir("live-git")
        try Data("# hi".utf8).write(to: vault.appendingPathComponent("note.md"))
        let state = makeAppState(announcer: RecordingAnnouncer())
        state.openVault(at: vault)
        await state.scanTask?.value
        XCTAssertEqual(state.syncReport?.providers.count, 0, "clean at open")

        try FileManager.default.createDirectory(
            at: vault.appendingPathComponent(".git"), withIntermediateDirectories: false)

        try await waitUntilAsync("report shows Git") {
            state.syncReport?.providers.map(\.kind) == [.git]
        }
        state.closeVault()
    }

    /// A HIGH-risk system arriving mid-session announces assertively,
    /// exactly once — the announce-once gate needs no watcher-side
    /// state because it keys on "condition first true for this vault".
    func testNewlyRiskyMidSessionAnnouncesExactlyOnce() async throws {
        let vault = try makeVaultDir("live-livesync")
        try Data("# hi".utf8).write(to: vault.appendingPathComponent("note.md"))
        let announcer = RecordingAnnouncer()
        let state = makeAppState(announcer: announcer)
        state.openVault(at: vault)
        await state.scanTask?.value
        XCTAssertTrue(announcer.posts.isEmpty, "clean vault stays silent")

        // LiveSync lands mid-session (High risk).
        let plugin = vault.appendingPathComponent(".obsidian/plugins/obsidian-livesync")
        try FileManager.default.createDirectory(
            at: plugin, withIntermediateDirectories: true)
        try Data("{}".utf8).write(to: plugin.appendingPathComponent("manifest.json"))

        try await waitUntilAsync("LiveSync detected live") {
            state.syncReport?.providers.map(\.kind) == [.liveSync]
        }
        try await waitUntilAsync("announced once") { announcer.posts.count == 1 }
        XCTAssertEqual(announcer.posts.first?.priority, .high)

        // Further marker churn must not re-announce (gate holds).
        try Data().write(to: vault.appendingPathComponent(".stignore"))
        try await waitUntilAsync("second marker detected") {
            state.syncReport?.providers.count == 2
        }
        XCTAssertEqual(announcer.posts.count, 1, "announce-once gate holds under live churn")
        state.closeVault()
    }

    /// closeVault tears the watcher down: post-close marker churn
    /// publishes nothing.
    func testCloseVaultStopsLiveRedetection() async throws {
        let vault = try makeVaultDir("live-close")
        try Data("# hi".utf8).write(to: vault.appendingPathComponent("note.md"))
        let state = makeAppState(announcer: RecordingAnnouncer())
        state.openVault(at: vault)
        await state.scanTask?.value
        state.closeVault()
        XCTAssertNil(state.syncReport)

        try FileManager.default.createDirectory(
            at: vault.appendingPathComponent(".git"), withIntermediateDirectories: false)
        let settled = expectation(description: "quiet after close")
        DispatchQueue.main.asyncAfter(deadline: .now() + 1.0) { settled.fulfill() }
        await fulfillment(of: [settled], timeout: 10)
        XCTAssertNil(state.syncReport, "no publish after close")
    }

    // MARK: - Helpers

    /// Thread-safe counter for callbacks that arrive on the main queue
    /// while the test blocks in `wait(for:)`.
    private final class CallbackCounter: @unchecked Sendable {
        private let lock = NSLock()
        private var value = 0
        var count: Int {
            lock.lock()
            defer { lock.unlock() }
            return value
        }
        func increment() {
            lock.lock()
            defer { lock.unlock() }
            value += 1
        }
    }

    /// Spin the main run loop until `condition` (sync variant for the
    /// pure-watcher tests, where callbacks land via the main queue).
    private func waitUntil(
        _ what: String, timeout: TimeInterval = 10,
        condition: () -> Bool
    ) throws {
        let deadline = Date().addingTimeInterval(timeout)
        while !condition() {
            if Date() > deadline {
                XCTFail("timed out waiting for \(what)")
                return
            }
            RunLoop.main.run(until: Date().addingTimeInterval(0.05))
        }
    }

    /// Async-context variant: yields to the main actor so published
    /// AppState mutations (which hop through Tasks) can land.
    private func waitUntilAsync(
        _ what: String, timeout: TimeInterval = 15,
        condition: @MainActor () -> Bool
    ) async throws {
        let deadline = Date().addingTimeInterval(timeout)
        while !condition() {
            if Date() > deadline {
                XCTFail("timed out waiting for \(what)")
                return
            }
            try await Task.sleep(nanoseconds: 50_000_000)
        }
    }
}
