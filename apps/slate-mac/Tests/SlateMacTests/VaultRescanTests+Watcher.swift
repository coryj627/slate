// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import XCTest

@testable import SlateMac

/// Plan 43 PR 20c (#1267; design
/// `docs/plans/43_mac_backlog_delivery/20_vault_watcher_design.md` §4.7): the
/// mac host of core's vault watcher. A burst of changes made outside Slate
/// reaches the open vault's `VaultEventAdapter` as one `onExternalChange()`,
/// and AppState answers it with a Watcher rescan — the run Refresh makes, on
/// the foreground rescan's sentence policy, behind the watcher's own gate (at
/// most one sentence per `watcherSentenceSpacingMs()`, none under a modal
/// surface), and never while the app is inactive.
///
/// Most facts make the watcher's call themselves, through the real adapter and
/// from a thread of their own (`Harness.signalExternalChange`), so its timing
/// is theirs. Six lift the test switch (`VaultWatcherSwitch`) and run the
/// real watcher end to end — FSEvents, core's coalescer and its "slate-watch"
/// thread — and are CI's only FSEvents witnesses (owner decision Q7): the
/// first fact below, `testSlateOwnSaveDoesNotStartAWatcherRun`,
/// `testAChangeMadeDuringTheOpenScanArrivesAsABurst`,
/// `testCloseStopsTheWatchAndDropsALateSignal`,
/// `testTheBaselineIsUnchangedWhileTheWatcherIsLive` and plan 43 PR 22a's
/// `testARenamedVaultFolderRearmsTheWatcherAtItsNewPath`. That last one
/// follows core's re-arm switch, `vaultWatchRearmEnabled()`, and is skipped
/// while the re-arm is off (until #1513). Their waits are bounded (30 s for a
/// call that must come, 12 s for one that must not) and end early. Two more arm a real watch without waiting on it: the comparison
/// open in `testAnUnavailableWatcherLeavesTheBaselineUnchanged` and
/// `testTheTestSwitchKeepsTheWatcherOffForTheWholeRun`. The names are the
/// Windows twins' (`RescanTests.Watcher.cs`, PR 20b) in Swift form. The
/// Watcher reason renders the shared "Files refreshed. …" sentence (owner
/// decision Q4).
extension VaultRescanTests {
    static let oneNew = "Files refreshed. 1 new or changed, 0 removed."
    static let noChanges = "Files refreshed. No changes."
    static let oneError = "Files refreshed with errors. 1 error; results may be incomplete."

    // MARK: - The watcher's run and its sentence

    /// The real watcher, end to end on FSEvents: the open armed the watch, a
    /// file made outside Slate reached core's coalescer, the adapter was
    /// called, and the Watcher run put the file in the tree and in Quick
    /// Open's list — with no Refresh — and said so in the one shared
    /// sentence.
    func testAWatcherDrivenChangeReachesTheTreeWithoutARefresh() async throws {
        let h = try await open(["alpha.md": "# Alpha\n"], watching: true)
        let session = try XCTUnwrap(h.state.currentSession)
        let tree = FileTreeViewModel()
        tree.bind(to: session)
        _ = await tree.settleLevelLoadsForTesting()
        let registration = h.state.rescanRun.registerTreeRefresher { await tree.refreshForRescan() }
        defer { h.state.rescanRun.unregisterTreeRefresher(registration) }

        try h.write("late.md", "# Late\n")
        await h.until("the watcher's run listing late.md", timeout: 30) {
            h.state.files.contains { $0.path == "late.md" }
                && tree.rootLevel.contains { $0.path == "late.md" } && !h.state.isRescanning
        }
        await h.state.vaultRescanTask?.value

        XCTAssertFalse(h.runs.reasons.isEmpty, "no rescan ran")
        XCTAssertEqual(
            Set(h.runs.reasons), [.watcher], "Watcher runs only: no Refresh, no foreground rescan")
        XCTAssertEqual(h.spoken.lines, [Self.oneNew])
    }

    /// §4.5: a Watcher run that found nothing posts nothing, exactly like an
    /// unchanged foreground rescan.
    func testAWatcherRunIsSilentWhenNothingChanged() async throws {
        let h = try await open(["alpha.md": "# Alpha\n"])
        let calls = h.recordCoreCalls()

        await h.signalAndAwaitTheWatcherRun()

        XCTAssertEqual(calls.count("scan"), 1)
        XCTAssertEqual(h.runs.reasons, [.watcher])
        XCTAssertEqual(h.spoken.lines, [])
    }

    /// §4.5: a Watcher run that found changes says so in ONE sentence with
    /// its own counts; the tree and Quick Open's list show the new file and
    /// drop the deleted one.
    func testAWatcherRunSpeaksWhenSomethingChanged() async throws {
        let h = try await open(["alpha.md": "# Alpha\n", "gone.md": "# Gone\n"])
        let session = try XCTUnwrap(h.state.currentSession)
        let tree = FileTreeViewModel()
        tree.bind(to: session)
        _ = await tree.settleLevelLoadsForTesting()
        let registration = h.state.rescanRun.registerTreeRefresher { await tree.refreshForRescan() }
        defer { h.state.rescanRun.unregisterTreeRefresher(registration) }
        try h.write("late.md", "# Late\n")
        try h.remove("gone.md")

        await h.signalAndAwaitTheWatcherRun()

        XCTAssertEqual(h.runs.reasons, [.watcher])
        XCTAssertEqual(h.spoken.lines, ["Files refreshed. 1 new or changed, 1 removed."])
        XCTAssertTrue(h.state.files.contains { $0.path == "late.md" })
        XCTAssertFalse(h.state.files.contains { $0.path == "gone.md" })
        XCTAssertTrue(tree.rootLevel.contains { $0.path == "late.md" })
        XCTAssertFalse(tree.rootLevel.contains { $0.path == "gone.md" })
    }

    /// §4.5: a partial Watcher run speaks the incomplete sentence, never
    /// "No changes" and never silence — and the sentence window covers it
    /// too: the next incomplete Watcher run inside the window posts nothing,
    /// so a file that keeps failing during a sync is not repeated.
    func testAnIncompleteWatcherRunSpeaksTheIncompleteSentence() async throws {
        let h = try await open(["alpha.md": "# Alpha\n"])
        try h.write("locked.md", "# Locked\n")
        let locked = h.vault.appendingPathComponent("locked.md").path
        try FileManager.default.setAttributes([.posixPermissions: 0o000], ofItemAtPath: locked)
        if FileManager.default.isReadableFile(atPath: locked) {
            throw XCTSkip("This process reads a mode-000 file (root?): no read can fail here.")
        }

        await h.signalAndAwaitTheWatcherRun()
        XCTAssertEqual(h.spoken.lines, [Self.oneError])

        h.spoken.lines.removeAll()
        h.clock.now += 10
        await h.signalAndAwaitTheWatcherRun()
        XCTAssertEqual(h.spoken.lines, [], "inside the window the next one posts nothing")
        XCTAssertEqual(h.runs.reasons, [.watcher, .watcher])
    }

    /// §4.5 item 2 (owner decision Q2): at most one watcher sentence per
    /// `watcherSentenceSpacingMs()` — core's one number, 60 s, read through
    /// the FFI. A Watcher run inside the window reconciles and posts
    /// nothing; the first one past it speaks its own run's counts, never the
    /// silent run's carried over.
    func testASecondWatcherSentenceInsideTheWindowIsSilent() async throws {
        XCTAssertEqual(watcherSentenceSpacingMs(), 60_000)
        XCTAssertEqual(AppState.watcherSentenceSpacing, 60)
        let h = try await open(["alpha.md": "# Alpha\n"])

        try h.write("one.md", "# One\n")
        await h.signalAndAwaitTheWatcherRun()
        XCTAssertEqual(h.spoken.lines, [Self.oneNew])

        h.spoken.lines.removeAll()
        h.clock.now += AppState.watcherSentenceSpacing - 1
        try h.write("two.md", "# Two\n")
        try h.write("three.md", "# Three\n")
        await h.signalAndAwaitTheWatcherRun()
        XCTAssertEqual(h.spoken.lines, [], "inside the window: silent")
        XCTAssertTrue(h.state.files.contains { $0.path == "two.md" }, "and reconciled")
        XCTAssertTrue(h.state.files.contains { $0.path == "three.md" })

        h.clock.now += 1
        try h.write("four.md", "# Four\n")
        await h.signalAndAwaitTheWatcherRun()
        XCTAssertEqual(h.spoken.lines, [Self.oneNew], "its own count, not the silent run's")
        XCTAssertEqual(h.runs.reasons, [.watcher, .watcher, .watcher])
    }

    /// §4.5 item 3: no watcher sentence while a modal surface is open (the
    /// foreground route's predicate: the palette, Quick Open, the search
    /// overlay or a sheet) — the run still reconciles — and a sentence held
    /// back that way is dropped, not deferred, and starts no window: once
    /// the surface closes, the next Watcher run speaks at once.
    func testAWatcherSentenceIsNotPostedWhileAModalSurfaceIsOpen() async throws {
        let h = try await open(["alpha.md": "# Alpha\n"])
        h.state.isCommandPaletteOpen = true
        try h.write("one.md", "# One\n")
        await h.signalAndAwaitTheWatcherRun()
        XCTAssertEqual(h.spoken.lines, [])
        XCTAssertTrue(h.state.files.contains { $0.path == "one.md" })

        h.state.isCommandPaletteOpen = false
        h.state.isSearchOpen = true
        try h.write("two.md", "# Two\n")
        await h.signalAndAwaitTheWatcherRun()
        XCTAssertEqual(h.spoken.lines, [], "the search overlay is modal too")
        h.state.isSearchOpen = false

        try h.write("three.md", "# Three\n")
        await h.signalAndAwaitTheWatcherRun()
        XCTAssertEqual(h.spoken.lines, [Self.oneNew], "the dropped sentences started no window")
    }

    // MARK: - The lattice, and a request that is never dropped

    /// §4.2 item 2: the pending-reason lattice's join is Explicit >
    /// Foreground > Watcher, over every pending reason and every arrival;
    /// and through the coalescer, requests joined to a running rescan make
    /// ONE follow-up of the joined reason — Watcher ⊔ Watcher a Watcher run,
    /// Foreground ⊔ Watcher a Foreground run (it speaks where a Watcher run
    /// inside the window would not), Explicit ⊔ Watcher an Explicit run ("No
    /// changes" is still said).
    func testWatcherJoinsTheLattice() async throws {
        let joins: [(RescanReason?, RescanReason, RescanReason)] = [
            (nil, .explicit, .explicit), (nil, .foreground, .foreground), (nil, .watcher, .watcher),
            (.explicit, .explicit, .explicit), (.explicit, .foreground, .explicit),
            (.explicit, .watcher, .explicit),
            (.foreground, .explicit, .explicit), (.foreground, .foreground, .foreground),
            (.foreground, .watcher, .foreground),
            (.watcher, .explicit, .explicit), (.watcher, .foreground, .foreground),
            (.watcher, .watcher, .watcher),
        ]
        for (pending, arriving, joined) in joins {
            XCTAssertEqual(
                RescanReason.joined(pending, arriving), joined,
                "\(pending.map { "\($0)" } ?? "nothing") joined by \(arriving)")
        }

        // Watcher ⊔ Watcher: two signals during a parked foreground run make
        // one Watcher follow-up.
        do {
            let h = try await open(["alpha.md": "# Alpha\n"])
            let calls = h.recordCoreCalls()
            let gate = Gate()
            h.state.rescanRun.afterScanHook = { await gate.park() }
            let run = try XCTUnwrap(h.state.requestRescan(.foreground))
            await gate.untilParked()
            try h.write("late.md", "# Late\n")
            await h.signalExternalChange()
            await h.signalExternalChange()
            XCTAssertEqual(h.state.rescanRun.pendingReason, .watcher)
            await gate.release()
            await run.value

            XCTAssertEqual(calls.count("scan"), 2, "one follow-up for both signals")
            XCTAssertEqual(h.runs.reasons, [.foreground, .watcher])
            XCTAssertEqual(h.spoken.lines, [Self.oneNew])
        }

        // Foreground ⊔ Watcher: with a watcher sentence just spoken, a
        // Watcher follow-up would be silent; the joined one speaks, so it is
        // Foreground.
        do {
            let h = try await open(["alpha.md": "# Alpha\n"])
            try h.write("first.md", "# First\n")
            await h.signalAndAwaitTheWatcherRun()
            XCTAssertEqual(h.spoken.lines, [Self.oneNew])
            h.spoken.lines.removeAll()
            h.runs.reasons.removeAll()
            let gate = Gate()
            h.state.rescanRun.afterScanHook = { await gate.park() }
            await h.signalExternalChange()
            let run = try XCTUnwrap(h.state.vaultRescanTask)
            await gate.untilParked()
            try h.write("late.md", "# Late\n")
            await h.signalExternalChange()
            XCTAssertEqual(h.state.requestRescan(.foreground), run)
            XCTAssertEqual(h.state.rescanRun.pendingReason, .foreground)
            await gate.release()
            await run.value

            XCTAssertEqual(h.runs.reasons, [.watcher, .foreground])
            XCTAssertEqual(h.spoken.lines, [Self.oneNew], "the Foreground follow-up spoke")
        }

        // Explicit ⊔ Watcher: a Refresh during a parked Watcher run makes an
        // Explicit follow-up, which always speaks.
        do {
            let h = try await open(["alpha.md": "# Alpha\n"])
            let gate = Gate()
            h.state.rescanRun.afterScanHook = { await gate.park() }
            await h.signalExternalChange()
            let run = try XCTUnwrap(h.state.vaultRescanTask)
            await gate.untilParked()
            await h.signalExternalChange()
            XCTAssertEqual(h.state.requestRescan(.explicit), run)
            XCTAssertEqual(h.state.rescanRun.pendingReason, .explicit)
            await gate.release()
            await run.value

            XCTAssertEqual(h.runs.reasons, [.watcher, .explicit])
            XCTAssertEqual(h.spoken.lines, [Self.noChanges])
        }
    }

    /// §4.2 item 3: a watcher request made while the open scan, an import or
    /// a Trash operation runs is recorded, not refused, and runs once they
    /// settle. The open's request: its scan parked inside its walk here,
    /// after it in `testAWatcherRequestBeforeTheWorkspaceRunsAfterTheOpen`.
    func testAWatcherRequestWhileBlockedRunsWhenTheBlockersSettle() async throws {
        do {
            let h = try prepare(["alpha.md": "# Alpha\n"])
            let parking = ScanParking { event in
                if case .started = event { return true }
                return false
            }
            h.state.scanProgressHookForTesting = parking.hook
            let opening = Task { await h.openVault(clearing: false) }
            await h.until("the open scan parking inside its walk") { parking.isParked }
            await h.signalExternalChange()
            XCTAssertTrue(h.state.isScanning)
            XCTAssertEqual(h.state.rescanRun.pendingReason, .watcher, "recorded, not refused")
            XCTAssertEqual(h.runs.reasons, [], "no rescan while the open scan runs")
            parking.release()
            await opening.value
            await h.until("the recorded watcher run") {
                h.runs.reasons == [.watcher] && !h.state.isRescanning
            }
            await h.state.vaultRescanTask?.value
            XCTAssertFalse(
                h.spoken.lines.contains { $0.hasPrefix("Files refreshed") },
                "nothing changed after the open's walk")
        }

        do {
            let h = try await open(["alpha.md": "# Alpha\n"])
            let provider = NSItemProvider()
            provider.registerDataRepresentation(
                forTypeIdentifier: FileTreeSidebar.fileURLUTType, visibility: .all
            ) { _ in nil }
            let importBatch = try XCTUnwrap(
                h.state.beginImportBatch(providers: [provider], destinationFolder: ""))
            try h.write("late.md", "# Late\n")
            await h.signalExternalChange()
            XCTAssertEqual(h.runs.reasons, [], "no rescan while the import runs")
            XCTAssertEqual(h.state.rescanRun.pendingReason, .watcher)

            h.state.cancelImportBatch(importBatch)
            await h.until("the recorded watcher run after the import") {
                h.runs.reasons == [.watcher] && !h.state.isRescanning
            }
            await h.state.vaultRescanTask?.value
            XCTAssertEqual(h.spoken.lines, [Self.oneNew])
            XCTAssertTrue(h.state.files.contains { $0.path == "late.md" })
        }

        do {
            let h = try await open(["alpha.md": "# Alpha\n", "beta.md": "# Beta\n"])
            let trashGate = TrashGate()
            h.state.cancellableSingleTrashRunner = { _, _, _, _, _ in await trashGate.enter() }
            XCTAssertTrue(h.state.requestDeleteEntry(path: "beta.md", isDirectory: false))
            let trash = try XCTUnwrap(h.state.pendingStructuralTaskForTesting)
            await trashGate.untilEntered()
            XCTAssertNotNil(h.state.trashProgress)
            try h.write("late.md", "# Late\n")
            await h.signalExternalChange()
            XCTAssertEqual(h.runs.reasons, [], "no rescan while the Trash operation runs")
            XCTAssertEqual(h.state.rescanRun.pendingReason, .watcher)

            await trashGate.release()
            await trash.value
            await h.until("the recorded watcher run after the Trash operation") {
                h.runs.reasons == [.watcher] && !h.state.isRescanning
            }
            await h.state.vaultRescanTask?.value
            // The Trash's own listing scan indexed late.md first (mac reloads
            // the inventory with a scan after a Trash), so the Watcher run
            // found it already there.
            XCTAssertTrue(h.state.files.contains { $0.path == "late.md" })
        }
    }

    /// §4.2 item 3, on mac's open: before the open has published its
    /// listing (`files`, what Quick Open lists — the workspace the request
    /// waits for), with the open parked after its walk, a watcher request
    /// is recorded, and the end of the open serves it: one Watcher run, which
    /// finds the file written after the open's walk and says so.
    func testAWatcherRequestBeforeTheWorkspaceRunsAfterTheOpen() async throws {
        let h = try prepare(["alpha.md": "# Alpha\n"])
        let parking = ScanParking { event in
            if case .finished = event { return true }
            return false
        }
        h.state.scanProgressHookForTesting = parking.hook
        let opening = Task { await h.openVault(clearing: false) }
        await h.until("the open scan parking after its walk") { parking.isParked }
        try h.write("late.md", "# Late\n")
        await h.signalExternalChange()
        XCTAssertTrue(h.state.isScanning, "the open is still running")
        XCTAssertTrue(h.state.files.isEmpty, "its listing has not been published")
        XCTAssertEqual(h.state.rescanRun.pendingReason, .watcher)
        XCTAssertEqual(h.runs.reasons, [])

        parking.release()
        await opening.value
        await h.until("the recorded watcher run") {
            h.runs.reasons == [.watcher] && !h.state.isRescanning
        }
        await h.state.vaultRescanTask?.value
        XCTAssertEqual(h.spoken.lines.filter { $0.hasPrefix("Files refreshed") }, [Self.oneNew])
        XCTAssertTrue(h.state.files.contains { $0.path == "late.md" })
    }

    /// §4.5 item 4: no Watcher run while the app is inactive. The request
    /// waits for the next activation, where the foreground route decides
    /// first: a return it does not forward (under two seconds away) runs
    /// the deferred request on its own, as a Watcher run; a return it does
    /// forward takes it — one run, Foreground reason — even inside the
    /// foreground cooldown, and that run speaks on the foreground policy
    /// inside the watcher window, so the return still says what changed
    /// while the user was away. A plain return inside the cooldown still
    /// rescans nothing.
    func testAWatcherRequestWhileInactiveRunsOnActivation() async throws {
        let h = try await open(["alpha.md": "# Alpha\n"])
        let calls = h.recordCoreCalls()
        h.state.beginForegroundRescanObservation(appIsActive: true)

        h.state.applicationDidResignActive()
        try h.write("late.md", "# Late\n")
        await h.signalExternalChange()
        XCTAssertEqual(calls.count("scan"), 0, "no Watcher run while the app is inactive")
        XCTAssertTrue(h.state.rescanRun.watcherRequestWhileInactive)
        h.clock.now += ForegroundRescanRoute.minimumAway - 1
        let deferred = try XCTUnwrap(
            h.state.applicationDidBecomeActive(), "the deferred watcher request runs on return")
        await deferred.value
        XCTAssertEqual(h.runs.reasons, [.watcher])
        XCTAssertEqual(h.spoken.lines, [Self.oneNew])
        XCTAssertFalse(h.state.rescanRun.watcherRequestWhileInactive)

        h.spoken.lines.removeAll()
        h.runs.reasons.removeAll()
        h.state.applicationDidResignActive()
        try h.write("away.md", "# Away\n")
        await h.signalExternalChange()
        XCTAssertEqual(calls.count("scan"), 1)
        h.clock.now += ForegroundRescanRoute.minimumAway + 1
        let joined = try XCTUnwrap(
            h.state.applicationDidBecomeActive(), "inside the cooldown, carrying the watcher's")
        await joined.value
        XCTAssertEqual(h.runs.reasons, [.foreground], "one run, Foreground reason")
        XCTAssertEqual(calls.count("scan"), 2)
        XCTAssertEqual(h.spoken.lines, [Self.oneNew], "spoken inside the watcher window")

        h.spoken.lines.removeAll()
        h.state.applicationDidResignActive()
        h.clock.now += ForegroundRescanRoute.minimumAway + 1
        XCTAssertNil(h.state.applicationDidBecomeActive(), "a plain return inside the cooldown")
        XCTAssertEqual(calls.count("scan"), 2)
    }

    /// §4.5 item 4: a Watcher follow-up due while the app is inactive waits
    /// too: a signal joined to a run, which then ends after the app resigned,
    /// runs on the next activation, not in the background.
    func testAWatcherFollowUpDueWhileInactiveWaitsForTheReturn() async throws {
        let h = try await open(["alpha.md": "# Alpha\n"])
        let calls = h.recordCoreCalls()
        h.state.beginForegroundRescanObservation(appIsActive: true)
        let gate = Gate()
        h.state.rescanRun.afterScanHook = { await gate.park() }
        let run = try XCTUnwrap(h.state.requestRescan(.explicit))
        await gate.untilParked()
        try h.write("late.md", "# Late\n")
        await h.signalExternalChange()
        XCTAssertEqual(h.state.rescanRun.pendingReason, .watcher)
        h.state.applicationDidResignActive()
        await gate.release()
        await run.value
        XCTAssertEqual(calls.count("scan"), 1, "the Watcher follow-up did not run in the background")
        XCTAssertTrue(h.state.rescanRun.watcherRequestWhileInactive)

        h.clock.now += 1
        let returned = try XCTUnwrap(h.state.applicationDidBecomeActive())
        await returned.value
        XCTAssertEqual(h.runs.reasons, [.explicit, .watcher])
        XCTAssertEqual(h.spoken.lines, [Self.noChanges, Self.oneNew])
    }

    /// §4.2 item 3: core already spaced a watcher request, so the foreground
    /// cooldown never applies to it — one second after a rescan a
    /// foreground request does not even scan, and a watcher request does.
    func testAWatcherRequestIsNotSubjectToTheForegroundCooldown() async throws {
        let h = try await open(["alpha.md": "# Alpha\n"])
        let calls = h.recordCoreCalls()
        await h.state.requestRescan(.explicit)?.value
        h.spoken.lines.removeAll()
        h.runs.reasons.removeAll()
        h.clock.now += 1
        try h.write("late.md", "# Late\n")

        XCTAssertNil(h.state.requestRescan(.foreground), "inside the cooldown")
        XCTAssertEqual(calls.count("scan"), 1)

        await h.signalAndAwaitTheWatcherRun()
        XCTAssertEqual(calls.count("scan"), 2)
        XCTAssertEqual(h.runs.reasons, [.watcher])
        XCTAssertEqual(h.spoken.lines, [Self.oneNew])
    }

    /// §4.6: an overflow — the platform dropped events — reaches the host as
    /// one catch-up call, like any burst, and makes ONE full rescan however
    /// many files it covers; more calls while that run is in flight make one
    /// follow-up, never one each.
    func testAnOverflowSignalRunsOneRescan() async throws {
        let h = try await open(["alpha.md": "# Alpha\n"])
        let calls = h.recordCoreCalls()
        for n in 0..<50 {
            try h.write(String(format: "burst/%02d.md", n), "# Burst \(n)\n")
        }
        await h.signalAndAwaitTheWatcherRun()
        XCTAssertEqual(calls.count("scan"), 1)
        XCTAssertEqual(h.spoken.lines, ["Files refreshed. 50 new or changed, 0 removed."])

        h.spoken.lines.removeAll()
        h.clock.now += AppState.watcherSentenceSpacing
        let gate = Gate()
        h.state.rescanRun.afterScanHook = { await gate.park() }
        await h.signalExternalChange()
        let run = try XCTUnwrap(h.state.vaultRescanTask)
        await gate.untilParked()
        try h.write("late.md", "# Late\n")
        await h.signalExternalChange()
        await h.signalExternalChange()
        await h.signalExternalChange()
        await gate.release()
        await run.value

        XCTAssertEqual(calls.count("scan"), 3, "the parked run and ONE follow-up")
        XCTAssertEqual(h.spoken.lines, [Self.oneNew])
    }

    /// The close stops the watch FIRST (`closeVault`, before the rescan state
    /// resets and the listener goes). With the real watcher live — a
    /// listener of the fact's own on the same session hears core's call for
    /// a file made outside — the close stops it: a file made after the close
    /// reaches no listener. A signal already queued for the main actor when
    /// the vault closes, one through the closed vault's adapter after it,
    /// and one from a vault a direct switch replaced, start no run.
    func testCloseStopsTheWatchAndDropsALateSignal() async throws {
        do {
            let h = try await open(["alpha.md": "# Alpha\n"], watching: true)
            let session = try XCTUnwrap(h.state.currentSession)
            let outside = ExternalChangeRecorder()
            let token = session.registerEventListener(listener: outside)
            defer { session.unregisterEventListener(token: token) }
            try h.write("control.md", "# Control\n")
            await h.until("the live watch calling both listeners", timeout: 30) {
                outside.externalChanges >= 1
                    && h.state.files.contains { $0.path == "control.md" } && !h.state.isRescanning
            }
            await h.state.vaultRescanTask?.value
            let heard = outside.externalChanges

            XCTAssertTrue(h.state.closeVault())
            try h.write("outside.md", "# Made outside after the close\n")
            // A live watch would call within its quiet window and its
            // spacing after the last scan; this one must not, in 12 s.
            let deadline = Date(timeIntervalSinceNow: 12)
            while outside.externalChanges == heard, Date() < deadline {
                try await Task.sleep(nanoseconds: 50_000_000)
            }
            XCTAssertEqual(outside.externalChanges, heard, "the close stopped the watch")
        }

        do {
            let late = try await open(["alpha.md": "# Alpha\n"])
            let calls = late.recordCoreCalls()
            let adapter = try XCTUnwrap(late.state.vaultEventAdapter)
            let handled = late.state.rescanRun.watcherSignalsHandled
            adapter.onExternalChange()  // queued behind this main-actor turn
            XCTAssertTrue(late.state.closeVault())
            await Self.callFromAnotherThread { adapter.onExternalChange() }
            await Self.drainMainActor()
            XCTAssertEqual(
                late.state.rescanRun.watcherSignalsHandled, handled,
                "a closed vault's signals never reach the rescan")
            XCTAssertEqual(calls.count("scan"), 0)
            XCTAssertFalse(late.state.isRescanning)
        }

        do {
            let h = try await open(["alpha.md": "# Alpha\n"])
            let replaced = try XCTUnwrap(h.state.vaultEventAdapter)
            let other = FileManager.default.temporaryDirectory
                .appendingPathComponent("vault-rescan-\(UUID().uuidString)")
            try FileManager.default.createDirectory(at: other, withIntermediateDirectories: true)
            tempDirs.append(other)
            try "# Beta\n".write(
                to: other.appendingPathComponent("beta.md"), atomically: true, encoding: .utf8)
            h.state.openVault(at: other)
            await h.state.scanTask?.value
            let calls = h.recordCoreCalls()
            let handled = h.state.rescanRun.watcherSignalsHandled
            await Self.callFromAnotherThread { replaced.onExternalChange() }
            await Self.drainMainActor()
            XCTAssertEqual(
                h.state.rescanRun.watcherSignalsHandled, handled,
                "the replaced vault's signal never reaches the vault that replaced it")
            XCTAssertEqual(calls.count("scan"), 0)

            await h.signalAndAwaitTheWatcherRun()
            XCTAssertEqual(calls.count("scan"), 1, "the open vault's own signal is answered")
        }
    }

    /// Plan 43 PR 22a (#940; design `22_descriptor_relative_session_design.md`
    /// §4.3, owner decisions Q3 and Q7): CI's FSEvents witness of core's
    /// re-arm. The open vault's folder renamed outside Slate re-arms the real
    /// watcher at the held root's verified current path: its catch-up call
    /// reaches the session's listeners, and so does a file made in the renamed
    /// folder afterwards, and nothing recreates the old path. Only the
    /// watcher's calls are asserted: the Watcher runs they start still read
    /// the vault by the path it was opened at (core's reads follow the held
    /// root in a follow-up; the host half is PR 22b).
    ///
    /// Gated by core's one switch, read through the FFI: the re-arm is off
    /// until #1513 (the owner's decision on #1518), so this fact is skipped,
    /// and core's `a_legitimate_root_rename_ends_the_watch_while_the_rearm_is_off`
    /// holds instead. Turning the switch on brings this fact back unchanged.
    func testARenamedVaultFolderRearmsTheWatcherAtItsNewPath() async throws {
        try XCTSkipUnless(
            vaultWatchRearmEnabled(),
            "Core's vault watch re-arm is off until #1513: a renamed vault folder ends the watch.")
        let h = try await open(["alpha.md": "# Alpha\n"], watching: true)
        let session = try XCTUnwrap(h.state.currentSession)
        let outside = ExternalChangeRecorder()
        let token = session.registerEventListener(listener: outside)
        defer { session.unregisterEventListener(token: token) }
        let moved = h.vault.deletingLastPathComponent()
            .appendingPathComponent(h.vault.lastPathComponent + "-moved")
        tempDirs.append(moved)

        try FileManager.default.moveItem(at: h.vault, to: moved)
        await h.until("the re-armed watch's catch-up call", timeout: 30) {
            outside.externalChanges >= 1
        }
        let caughtUp = outside.externalChanges
        try "# Late\n".write(
            to: moved.appendingPathComponent("late.md"), atomically: true, encoding: .utf8)
        await h.until("a call for the file made in the renamed folder", timeout: 30) {
            outside.externalChanges > caughtUp
        }
        XCTAssertFalse(
            FileManager.default.fileExists(atPath: h.vault.path),
            "nothing recreated the vault's old path")
        await h.state.vaultRescanTask?.value
        XCTAssertTrue(h.state.closeVault())
    }

    // MARK: - Slate's own writes, the open's arm, and the fallback

    /// Design §4.7 and §4.2's echo ledger, on real FSEvents: Slate's own
    /// save of a note reaches FSEvents and is dropped by core as an echo, so
    /// it starts no Watcher run. The watch is proved live first by a file
    /// made outside Slate, which does start one.
    func testSlateOwnSaveDoesNotStartAWatcherRun() async throws {
        let h = try await open(["alpha.md": "# Alpha\n"], watching: true)
        try h.write("outside.md", "# Outside\n")
        await h.until("the watcher's run listing outside.md", timeout: 30) {
            h.state.files.contains { $0.path == "outside.md" } && !h.state.isRescanning
        }
        await h.state.vaultRescanTask?.value
        XCTAssertTrue(h.runs.reasons.contains(.watcher), "the watch is live")

        _ = try await h.openNote("alpha.md")
        let runs = h.runs.reasons.count
        h.state.updateEditorText("# Alpha\n\nSaved by Slate.\n")
        await h.state.saveCurrentNote()?.value
        XCTAssertFalse(h.state.hasUnsavedChanges, "Slate saved the note")
        // An outside write of the same file would start a run within core's
        // quiet window and its spacing; Slate's own must not, in 12 s.
        let deadline = Date(timeIntervalSinceNow: 12)
        while h.runs.reasons.count == runs, Date() < deadline {
            try await Task.sleep(nanoseconds: 50_000_000)
        }
        XCTAssertEqual(h.runs.reasons.count, runs, "Slate's own save started a Watcher run")
    }

    /// #638's arm-then-probe rule, on real FSEvents: the open arms the watch
    /// BEFORE its scan's walk, so a file made during the open scan, after its
    /// walk has passed (inside the scan, at its Finished event), arrives as a
    /// burst and a Watcher run lists it with no Refresh. A watch armed after
    /// the scan would never hear it.
    func testAChangeMadeDuringTheOpenScanArrivesAsABurst() async throws {
        let h = try prepare(["alpha.md": "# Alpha\n"])
        let made = Locked(false)
        let during = h.vault.appendingPathComponent("during.md")
        h.state.scanProgressHookForTesting = { event in
            guard case .finished = event, !made.value else { return }
            made.mutate { $0 = true }
            try? "# During\n".write(to: during, atomically: true, encoding: .utf8)
        }
        await h.openVault(watching: true, clearing: false)
        XCTAssertTrue(made.value, "the open scan reached its Finished event")
        h.state.scanProgressHookForTesting = nil

        await h.until("the watcher's run listing during.md", timeout: 30) {
            h.state.files.contains { $0.path == "during.md" } && !h.state.isRescanning
        }
        await h.state.vaultRescanTask?.value
        XCTAssertEqual(Set(h.runs.reasons), [.watcher])
        XCTAssertEqual(h.spoken.lines.filter { $0.hasPrefix("Files refreshed") }, [Self.oneNew])
    }

    /// §4.6: the baseline never turns off. With the real watcher live,
    /// Refresh still runs its explicit rescan and speaks its own count, the
    /// foreground route still rescans a return after being away and says
    /// what changed — and the watch was live throughout: a file made outside
    /// Slate at the end reaches Quick Open's list with no Refresh.
    func testTheBaselineIsUnchangedWhileTheWatcherIsLive() async throws {
        let h = try await open(["alpha.md": "# Alpha\n"], watching: true)
        h.state.beginForegroundRescanObservation(appIsActive: true)

        // The Refresh starts within milliseconds of the write, long before
        // core's quiet window closes, so its count is its own.
        try h.write("refreshed.md", "# Refreshed\n")
        try await h.refresh()
        XCTAssertEqual(h.spoken.lines, [Self.oneNew])
        XCTAssertEqual(h.runs.reasons.first, .explicit)

        // Away: no Watcher run starts while the app is inactive, so the
        // return's foreground rescan is the one that finds away.md.
        h.spoken.lines.removeAll()
        h.state.applicationDidResignActive()
        h.clock.now += 60
        try h.write("away.md", "# Away\n")
        let returned = try XCTUnwrap(h.state.applicationDidBecomeActive())
        await returned.value
        XCTAssertEqual(h.spoken.lines, [Self.oneNew])
        XCTAssertTrue(h.runs.reasons.contains(.foreground))

        h.spoken.lines.removeAll()
        try h.write("live.md", "# Live\n")
        await h.until("the watcher's run listing live.md", timeout: 30) {
            h.state.files.contains { $0.path == "live.md" } && !h.state.isRescanning
        }
        await h.state.vaultRescanTask?.value
        XCTAssertEqual(h.runs.reasons.last, .watcher)
        XCTAssertEqual(h.spoken.lines, [Self.oneNew])
    }

    /// §4.6: a watcher that cannot start — here the test switch, which every
    /// other fact runs under — is a diagnostic in the unified log and
    /// nothing else: the open says exactly what an open with a live watch
    /// says, and Refresh and the foreground rescan reconcile and speak as
    /// before.
    func testAnUnavailableWatcherLeavesTheBaselineUnchanged() async throws {
        let h = try await open(["alpha.md": "# Alpha\n"])
        XCTAssertEqual(h.watchArms.value, [false], "the open asked, and core armed nothing")
        let watched = try await open(["alpha.md": "# Alpha\n"], watching: true)
        XCTAssertEqual(
            h.openLines, watched.openLines, "the open says the same with or without a watch")
        XCTAssertFalse(h.openLines.contains { $0.hasPrefix("Files refreshed") })

        try h.write("late.md", "# Late\n")
        try await h.refresh()
        XCTAssertEqual(h.spoken.lines, [Self.oneNew])

        h.spoken.lines.removeAll()
        h.clock.now += 60
        try h.write("later.md", "# Later\n")
        await h.state.requestRescan(.foreground)?.value
        XCTAssertEqual(h.spoken.lines, [Self.oneNew])
        XCTAssertEqual(h.runs.reasons, [.explicit, .foreground])
    }

    /// §4.7's test switch: `SlateMacTestBootstrap` set it for this whole
    /// process as the test bundle loaded, so every fact's vault keeps the
    /// watcher off and a Refresh's counts stay its own; a session opened
    /// under it arms no watch, and the same session arms once it is lifted.
    func testTheTestSwitchKeepsTheWatcherOffForTheWholeRun() throws {
        XCTAssertEqual(VaultWatcherSwitch.value, "1", "the bootstrap set the switch")
        let dir = FileManager.default.temporaryDirectory
            .appendingPathComponent("vault-rescan-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        tempDirs.append(dir)
        let session = try VaultSession.openFilesystem(rootPath: dir.path)
        XCTAssertFalse(session.startWatching(), "the switch keeps the watcher off")
        VaultWatcherSwitch.lift()
        defer { VaultWatcherSwitch.restore() }
        XCTAssertTrue(session.startWatching(), "lifted, the same session arms")
        session.stopWatching()
    }

    // MARK: - Support

    /// A listener of a fact's own on a session: how often core's watcher
    /// called it.
    final class ExternalChangeRecorder: VaultEventListener, @unchecked Sendable {
        private let lock = NSLock()
        private var calls = 0

        var externalChanges: Int { lock.withLock { calls } }

        func onError(code: EventErrorCode, path: String, message: String) {}
        func onFileChange(event: FileChangeEvent) {}
        func onIndexPhase(phase: IndexPhase, filesSeen: UInt64) {}
        func onExternalChange() { lock.withLock { calls += 1 } }
    }

    /// Parks a `loadFiles` scan on its scanner thread at the first progress
    /// event `matches` takes, until `release()`: the open stays inside its
    /// scan, `isScanning`, while a fact acts. Bounded, so a fact that fails
    /// before releasing never wedges the scan.
    final class ScanParking: @unchecked Sendable {
        private let lock = NSLock()
        private let semaphore = DispatchSemaphore(value: 0)
        private var parked = false
        private let matches: @Sendable (ScanProgress) -> Bool

        init(_ matches: @escaping @Sendable (ScanProgress) -> Bool) {
            self.matches = matches
        }

        var isParked: Bool { lock.withLock { parked } }

        var hook: @Sendable (ScanProgress) -> Void {
            { [self] event in
                guard matches(event) else { return }
                let first = lock.withLock { () -> Bool in
                    guard !parked else { return false }
                    parked = true
                    return true
                }
                guard first else { return }
                _ = semaphore.wait(timeout: .now() + 30)
            }
        }

        func release() {
            semaphore.signal()
        }
    }

    /// Run `body` on a thread of its own and wait for it to return (it only
    /// queues work for the main actor).
    static func callFromAnotherThread(_ body: @escaping @Sendable () -> Void) async {
        await withCheckedContinuation { (done: CheckedContinuation<Void, Never>) in
            DispatchQueue.global(qos: .utility).async {
                body()
                done.resume()
            }
        }
    }

    /// Let every job already queued for the main actor run (it is FIFO).
    static func drainMainActor() async {
        for _ in 0..<20 { await Task.yield() }
    }
}
