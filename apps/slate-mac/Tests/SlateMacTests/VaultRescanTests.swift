// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import Combine
import XCTest

@testable import SlateMac

/// Plan 43 PR 19 (#1333; contract 40 R-9 on mac): the mac rescan. Files
/// Sidebar Refresh (`slate.sidebar.refresh`, the palette row "Files: Refresh")
/// and the foreground rescan run core's incremental rescan under ONE cancel
/// token, then re-synchronize every surface from the index (the host retains
/// nothing): the files inventory Quick Open lists, the tree, the open tabs by
/// their indexed hashes with PR 10's re-seat, and the active note's leaves.
/// Nothing speaks during the re-sync; one core sentence follows it, on
/// Windows' policy. The names mirror Windows' `RescanTests` where a twin
/// exists.
@MainActor
final class VaultRescanTests: XCTestCase {
    private var tempDirs: [URL] = []

    override func tearDown() {
        for dir in tempDirs {
            Self.restorePermissions(under: dir)
            try? FileManager.default.removeItem(at: dir)
        }
        tempDirs = []
        super.tearDown()
    }

    // MARK: - Harness

    /// Every sentence the announcer was handed, in order.
    final class Spoken: AnnouncementPosting, @unchecked Sendable {
        var lines: [String] = []

        func post(_ message: String, priority: AnnouncementPriority) {
            lines.append(message)
        }
    }

    /// Every rescan core call that takes the run's token: its operation and
    /// the token it carried. Calls arrive on the worker, so the record locks.
    final class CoreCalls: @unchecked Sendable {
        private let lock = NSLock()
        private var calls: [(operation: String, token: CancelToken)] = []

        func record(_ operation: String, _ token: CancelToken) {
            lock.lock()
            calls.append((operation, token))
            lock.unlock()
        }

        var operations: [String] {
            lock.lock()
            defer { lock.unlock() }
            return calls.map(\.operation)
        }

        var tokens: [ObjectIdentifier] {
            lock.lock()
            defer { lock.unlock() }
            return calls.map { ObjectIdentifier($0.token) }
        }

        func count(_ operation: String) -> Int {
            operations.filter { $0 == operation }.count
        }
    }

    final class Clock {
        var now = Date(timeIntervalSince1970: 2_000_000_000)
    }

    /// Parks a rescan inside its `afterScanHook` until released. Only the
    /// first run that reaches it parks; the follow-ups run through.
    actor Gate {
        private var parkedRun: CheckedContinuation<Void, Never>?
        private var parkWaiters: [CheckedContinuation<Void, Never>] = []
        private var remainingParks = 1
        private(set) var parks = 0

        func park() async {
            guard remainingParks > 0 else { return }
            remainingParks -= 1
            parks += 1
            for waiter in parkWaiters { waiter.resume() }
            parkWaiters = []
            await withCheckedContinuation { parkedRun = $0 }
        }

        func untilParked() async {
            guard parks == 0 else { return }
            await withCheckedContinuation { parkWaiters.append($0) }
        }

        func release() {
            parkedRun?.resume()
            parkedRun = nil
        }

        func rearm() {
            remainingParks = 1
            parks = 0
        }
    }

    @MainActor
    struct Harness {
        let state: AppState
        let vault: URL
        let spoken: Spoken
        let clock: Clock

        func write(_ relative: String, _ text: String) throws {
            let url = vault.appendingPathComponent(relative)
            try FileManager.default.createDirectory(
                at: url.deletingLastPathComponent(), withIntermediateDirectories: true)
            try text.write(to: url, atomically: true, encoding: .utf8)
        }

        func remove(_ relative: String) throws {
            try FileManager.default.removeItem(at: vault.appendingPathComponent(relative))
        }

        /// Through the registry's own command, as the palette and the View
        /// menu dispatch it.
        func refresh() async throws {
            let result = try state.dispatchSidebarAction(id: SlateCommandID.sidebarRefresh)
            XCTAssertEqual(result, .completed(actionID: SlateCommandID.sidebarRefresh))
            await state.vaultRescanTask?.value
        }

        func recordCoreCalls() -> CoreCalls {
            let calls = CoreCalls()
            state.rescanRun.coreCallObserver = { operation, token in
                calls.record(operation, token)
            }
            return calls
        }

        func openNote(_ path: String) async throws -> TabID {
            state.selectedFilePath = path
            await state.noteLoadTask?.value
            XCTAssertEqual(state.loadedFilePath, path)
            return try XCTUnwrap(state.workspace.model.activeGroup.activeTabID)
        }

        func openNoteInNewTab(_ path: String) async throws -> TabID {
            state.openFile(path, target: .newTab)
            await state.noteLoadTask?.value
            XCTAssertEqual(state.loadedFilePath, path)
            return try XCTUnwrap(state.workspace.model.activeGroup.activeTabID)
        }

        func indexedHash(_ path: String) throws -> String? {
            let session = try XCTUnwrap(state.currentSession)
            let cancel = CancelToken()
            return try session.indexedContentHashes(paths: [path], cancel: cancel).first ?? nil
        }
    }

    private func open(_ files: [String: String]) async throws -> Harness {
        let dir = FileManager.default.temporaryDirectory
            .appendingPathComponent("vault-rescan-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        tempDirs.append(dir)
        for (relative, text) in files {
            let url = dir.appendingPathComponent(relative)
            try FileManager.default.createDirectory(
                at: url.deletingLastPathComponent(), withIntermediateDirectories: true)
            try text.write(to: url, atomically: true, encoding: .utf8)
        }
        let spoken = Spoken()
        let clock = Clock()
        let state = AppState(
            recentsStore: RecentVaultsStore(
                fileURL: dir.appendingPathComponent(".recents-for-test.json")),
            externalOpener: { _ in true },
            preferencesStore: PreferencesStore(
                defaults: UserDefaults(suiteName: UUID().uuidString)!),
            announcer: spoken)
        state.scanClock = { clock.now }
        // The facts hear the sentence at once; the settle has its own fact.
        state.rescanRun.sentenceSettle = 0
        state.openVault(at: dir)
        await state.scanTask?.value
        spoken.lines.removeAll()
        return Harness(state: state, vault: dir, spoken: spoken, clock: clock)
    }

    /// The Windows fact's probe for a volume that aliases spellings.
    private func volumeAliasesSpellings() throws -> Bool {
        let dir = FileManager.default.temporaryDirectory
            .appendingPathComponent("vault-rescan-probe-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: dir) }
        try "p".write(to: dir.appendingPathComponent("Probe.md"), atomically: true, encoding: .utf8)
        return FileManager.default.fileExists(atPath: dir.appendingPathComponent("probe.md").path)
    }

    private static func restorePermissions(under dir: URL) {
        guard
            let paths = FileManager.default.enumerator(atPath: dir.path)?.allObjects as? [String]
        else { return }
        for relative in paths {
            try? FileManager.default.setAttributes(
                [.posixPermissions: 0o755],
                ofItemAtPath: dir.appendingPathComponent(relative).path)
        }
    }

    // MARK: - The nine facts

    /// Windows' `RefreshSpeaksExactlyOneCompletion`: a file a sync client
    /// drops into the open vault is invisible until something rescans. Files
    /// Sidebar Refresh, the registry's own command, indexes it, re-syncs the
    /// inventory Quick Open lists and the tree from the index, and the whole
    /// run speaks ONE core sentence with this scan's counts: no scan-family
    /// line (the reason-aware progress policy) and no strip left behind. An
    /// explicit Refresh with nothing new still answers.
    func testAnExplicitRefreshIndexesAFileDroppedIntoTheVaultAndSpeaksTheCount() async throws {
        let h = try await open(["alpha.md": "# Alpha\n"])
        let session = try XCTUnwrap(h.state.currentSession)
        let tree = FileTreeViewModel()
        tree.bind(to: session)
        _ = await tree.settleLevelLoadsForTesting()
        let registration = h.state.rescanRun.registerTreeRefresher { await tree.refreshForRescan() }
        defer { h.state.rescanRun.unregisterTreeRefresher(registration) }

        try h.write("late.md", "# Late\n")
        XCTAssertFalse(h.state.files.contains { $0.path == "late.md" })
        XCTAssertFalse(tree.rootLevel.contains { $0.path == "late.md" })

        let command = try XCTUnwrap(
            h.state.commandRegistry.findById(id: SlateCommandID.sidebarRefresh))
        XCTAssertEqual(command.label, "Files: Refresh")
        XCTAssertEqual(command.section, .sidebar)
        let scanLines = h.state.scanAnnouncementCount

        try await h.refresh()

        XCTAssertEqual(h.spoken.lines, ["Files refreshed. 1 new or changed, 0 removed."])
        XCTAssertEqual(
            h.state.scanAnnouncementCount, scanLines,
            "a rescan never speaks VaultScanStarted, progress or VaultScanFinished")
        XCTAssertTrue(
            h.state.files.contains { $0.path == "late.md" },
            "Quick Open's list is the index's openable documents")
        XCTAssertTrue(
            tree.rootLevel.contains { $0.path == "late.md" },
            "the tree re-synced from the index, awaited to its publication")
        XCTAssertNil(h.state.scanProgress, "the strip an explicit Refresh shows is gone")
        XCTAssertFalse(h.state.isRescanning)

        h.spoken.lines.removeAll()
        try await h.refresh()
        XCTAssertEqual(h.spoken.lines, ["Files refreshed. No changes."])
    }

    /// Windows' `ForegroundRescanIsThrottledAndSilentWhenNothingChanged` and
    /// `ATouchedUnchangedVaultSaysNoChanges`: a foreground rescan that finds
    /// nothing posts NOTHING, a touched but unchanged file included (core's
    /// count is hash-authoritative); and a second foreground request inside
    /// the 5 s cooldown starts no run at all, even with a change on disk.
    func testAForegroundRescanIsSilentWhenNothingChanged() async throws {
        let h = try await open(["alpha.md": "# Alpha\n", "beta.md": "# Beta\n"])
        let calls = h.recordCoreCalls()

        await h.state.requestRescan(.foreground)?.value
        XCTAssertEqual(h.spoken.lines, [])
        XCTAssertEqual(calls.count("scan"), 1, "it scanned")

        // A touched but unchanged file is no change.
        h.clock.now += AppState.foregroundRescanCooldown + 1
        try FileManager.default.setAttributes(
            [.modificationDate: Date().addingTimeInterval(120)],
            ofItemAtPath: h.vault.appendingPathComponent("beta.md").path)
        await h.state.requestRescan(.foreground)?.value
        XCTAssertEqual(h.spoken.lines, [])
        XCTAssertEqual(calls.count("scan"), 2)

        // Inside the cooldown no run starts, even with a change on disk.
        h.clock.now += AppState.foregroundRescanCooldown - 1
        try h.write("late.md", "# Late\n")
        XCTAssertNil(h.state.requestRescan(.foreground))
        XCTAssertEqual(calls.count("scan"), 2, "the cooldown applies to starting a run")
        XCTAssertEqual(h.spoken.lines, [])
        XCTAssertFalse(h.state.files.contains { $0.path == "late.md" })
    }

    /// The other half of the policy: a foreground rescan that finds a new,
    /// changed or removed file speaks the same core sentence an explicit
    /// Refresh would, with this scan's counts.
    func testAForegroundRescanSpeaksWhenSomethingChanged() async throws {
        let h = try await open(["alpha.md": "# Alpha\n", "beta.md": "# Beta\n"])

        try h.write("late.md", "# Late\n")
        await h.state.requestRescan(.foreground)?.value
        XCTAssertEqual(h.spoken.lines, ["Files refreshed. 1 new or changed, 0 removed."])
        XCTAssertTrue(h.state.files.contains { $0.path == "late.md" })

        h.spoken.lines.removeAll()
        h.clock.now += AppState.foregroundRescanCooldown + 1
        try h.remove("alpha.md")
        try h.write("beta.md", "# Beta\n\nchanged outside Slate\n")
        await h.state.requestRescan(.foreground)?.value
        XCTAssertEqual(h.spoken.lines, ["Files refreshed. 1 new or changed, 1 removed."])
        XCTAssertFalse(h.state.files.contains { $0.path == "alpha.md" })
    }

    /// Windows' `AnUnreadableFileMakesTheRescanIncomplete` and
    /// `ATagTreeFailureIsCountedSilently`: a file the scan cannot read makes
    /// the run incomplete, so its one sentence is `VaultRescanIncomplete` with
    /// an honest count, never "No changes", for either reason; and a re-sync
    /// operation that fails (here the tree's refetch, counted, never spoken)
    /// counts into that same sentence.
    func testAnIncompleteRescanSpeaksTheIncompleteSentence() async throws {
        let h = try await open(["alpha.md": "# Alpha\n"])
        try h.write("locked.md", "# Locked\n")
        let locked = h.vault.appendingPathComponent("locked.md").path
        try FileManager.default.setAttributes([.posixPermissions: 0o000], ofItemAtPath: locked)
        if FileManager.default.isReadableFile(atPath: locked) {
            throw XCTSkip("This process reads a mode-000 file (root?): no read can fail here.")
        }

        try await h.refresh()
        XCTAssertEqual(
            h.spoken.lines,
            ["Files refreshed with errors. 1 error; results may be incomplete."])

        h.spoken.lines.removeAll()
        h.clock.now += AppState.foregroundRescanCooldown + 1
        await h.state.requestRescan(.foreground)?.value
        XCTAssertEqual(
            h.spoken.lines,
            ["Files refreshed with errors. 1 error; results may be incomplete."],
            "a foreground rescan speaks an incomplete run although nothing changed")

        // A clean scan whose re-sync fails twice says so, with both counted.
        try FileManager.default.setAttributes([.posixPermissions: 0o644], ofItemAtPath: locked)
        try await h.refresh()
        h.spoken.lines.removeAll()
        let failing = h.state.rescanRun.registerTreeRefresher { 2 }
        defer { h.state.rescanRun.unregisterTreeRefresher(failing) }
        try await h.refresh()
        XCTAssertEqual(
            h.spoken.lines,
            ["Files refreshed with errors. 2 errors; results may be incomplete."])
    }

    /// Windows' `ARescanReconcilesOpenTabs`, on mac's tabs: an open note
    /// changed outside Slate reloads in its tab when the tab is clean (the
    /// active one in place, never through the loading state, so the editor
    /// keeps its place; a parked one in its parked document) and is never
    /// reloaded over unsaved words, in either position. Silently: the one
    /// completion sentence is all the run says.
    func testACleanOpenTabReloadsWhenItsIndexedHashChangedAndADirtyOneDoesNot() async throws {
        let h = try await open(["clean.md": "clean one\n", "dirty.md": "dirty one\n"])
        let cleanTab = try await h.openNote("clean.md")
        let dirtyTab = try await h.openNoteInNewTab("dirty.md")
        h.state.updateEditorText("my unsaved words\n")
        XCTAssertTrue(h.state.hasUnsavedChanges)

        try h.write("clean.md", "clean two\n")
        try h.write("dirty.md", "dirty two\n")
        try await h.refresh()

        XCTAssertEqual(h.spoken.lines, ["Files refreshed. 2 new or changed, 0 removed."])
        XCTAssertEqual(h.state.workspace.model.activeGroup.activeTabID, dirtyTab)
        XCTAssertEqual(h.state.currentNoteText, "my unsaved words\n", "a dirty tab is never reloaded")
        XCTAssertTrue(h.state.hasUnsavedChanges)
        let parkedClean = try XCTUnwrap(h.state.workspace.document(for: cleanTab))
        XCTAssertEqual(parkedClean.text, "clean two\n", "the parked clean tab reloaded")
        XCTAssertFalse(parkedClean.hasUnsavedChanges)
        XCTAssertEqual(parkedClean.contentHash, try h.indexedHash("clean.md"))

        // The other way round: the active tab is the clean one.
        h.state.selectTab(id: cleanTab)
        await h.state.noteLoadTask?.value
        XCTAssertEqual(h.state.currentNoteText, "clean two\n")
        var loadingStates: [Bool] = []
        let watch = h.state.$isLoadingNote.sink { loadingStates.append($0) }
        defer { watch.cancel() }
        h.spoken.lines.removeAll()
        try h.write("clean.md", "clean three\n")
        try h.write("dirty.md", "dirty three\n")
        try await h.refresh()

        XCTAssertEqual(h.spoken.lines, ["Files refreshed. 2 new or changed, 0 removed."])
        XCTAssertEqual(h.state.currentNoteText, "clean three\n", "the active clean tab reloaded")
        XCTAssertFalse(h.state.hasUnsavedChanges)
        XCTAssertEqual(h.state.currentNoteContentHash, try h.indexedHash("clean.md"))
        XCTAssertFalse(
            loadingStates.contains(true),
            "an in-place reload never shows the loading state, which would unmount the editor")
        let parkedDirty = try XCTUnwrap(h.state.workspace.document(for: dirtyTab))
        XCTAssertEqual(parkedDirty.text, "my unsaved words\n", "a parked dirty tab keeps its words")
        XCTAssertTrue(parkedDirty.hasUnsavedChanges)
    }

    /// PR 10's rule through the rescan (Windows' `ACaseOnlyRenameInOnePage
    /// ReseatsItsTab` and the re-seat half of `ARescanReconcilesOpenTabs`): a
    /// tab whose note was moved to Trash in Slate stays missing until the note
    /// comes back; a Refresh that indexes it re-seats the tab, which reloads.
    /// And on a volume that aliases spellings, a case-only rename outside
    /// Slate (core's Deleted plus Created) re-seats the tab onto the stored
    /// spelling instead of marking it missing.
    func testAMissingTabReseatsOnRescan() async throws {
        let h = try await open(["Ghost.md": "ghost\n", "other.md": "other\n"])
        let ghost = try await h.openNote("Ghost.md")
        await h.state.deleteEntry(path: "Ghost.md", isDirectory: false)?.value
        XCTAssertNotNil(h.state.noteLoadError, "the tab is missing from disk")
        h.spoken.lines.removeAll()

        try h.write("Ghost.md", "back\n")
        try await h.refresh()
        await h.state.noteLoadTask?.value

        XCTAssertEqual(h.spoken.lines, ["Files refreshed. 1 new or changed, 0 removed."])
        XCTAssertEqual(h.state.workspace.model.activeGroup.activeTabID, ghost)
        XCTAssertNil(h.state.noteLoadError, "the re-seat brought the tab back")
        XCTAssertEqual(h.state.loadedFilePath, "Ghost.md")
        XCTAssertEqual(h.state.currentNoteText, "back\n")

        guard try volumeAliasesSpellings() else { return }
        let renamed = try await open(["Case.md": "case\n"])
        let tab = try await renamed.openNote("Case.md")
        try FileManager.default.moveItem(
            at: renamed.vault.appendingPathComponent("Case.md"),
            to: renamed.vault.appendingPathComponent("case.md"))
        try await renamed.refresh()
        await renamed.state.noteLoadTask?.value

        XCTAssertEqual(renamed.spoken.lines, ["Files refreshed. 1 new or changed, 1 removed."])
        XCTAssertEqual(renamed.state.workspace.model.tab(tab)?.item, .markdown(path: "case.md"))
        XCTAssertEqual(renamed.state.loadedFilePath, "case.md")
        XCTAssertNil(renamed.state.noteLoadError, "a respelled note is never marked missing")
        XCTAssertEqual(renamed.state.currentNoteText, "case\n")
    }

    /// Windows' `ExplicitRefreshDuringAForegroundRescanStillAnnounces`,
    /// `TwoForegroundRequestsStayForeground`, `AnActivationJoinsTheLattice
    /// EvenInsideTheCooldown` and `AFollowUpRunsAndAnImportRequestedMeanwhile
    /// NeverStarts`: a request while a rescan runs joins its pending-reason
    /// lattice (Explicit dominates Foreground), never refused and never
    /// subject to the cooldown, and ONE follow-up serves every joined request,
    /// finding the change the running scan's snapshot missed. A follow-up that
    /// an import began meanwhile blocks stays pending and runs when the
    /// import settles: a request is never dropped.
    func testARequestDuringARunningRescanJoinsAndIsNeverDropped() async throws {
        let h = try await open(["alpha.md": "# Alpha\n"])
        let calls = h.recordCoreCalls()
        let gate = Gate()
        h.state.rescanRun.afterScanHook = { await gate.park() }

        let run = try XCTUnwrap(h.state.requestRescan(.foreground))
        await gate.untilParked()
        try h.write("late.md", "# Late\n")
        XCTAssertEqual(h.state.requestRescan(.foreground), run, "joins, inside the cooldown too")
        XCTAssertEqual(h.state.requestRescan(.explicit), run)
        XCTAssertEqual(h.state.requestRescan(.foreground), run)
        XCTAssertEqual(h.state.rescanRun.pendingReason, .explicit, "Explicit dominates Foreground")
        await gate.release()
        await run.value

        XCTAssertEqual(calls.count("scan"), 2, "one follow-up served every joined request")
        XCTAssertEqual(
            h.spoken.lines, ["Files refreshed. 1 new or changed, 0 removed."],
            "the foreground run found nothing and was silent; the explicit follow-up spoke")
        XCTAssertNil(h.state.rescanRun.pendingReason)

        // Foreground ⊔ Foreground = Foreground: one silent follow-up.
        h.spoken.lines.removeAll()
        await gate.rearm()
        let quiet = try XCTUnwrap(h.state.requestRescan(.explicit))
        await gate.untilParked()
        XCTAssertEqual(h.state.requestRescan(.foreground), quiet)
        XCTAssertEqual(h.state.requestRescan(.foreground), quiet)
        XCTAssertEqual(h.state.rescanRun.pendingReason, .foreground)
        await gate.release()
        await quiet.value
        XCTAssertEqual(calls.count("scan"), 4)
        XCTAssertEqual(h.spoken.lines, ["Files refreshed. No changes."], "only the explicit run spoke")

        // A follow-up blocked by an import that began meanwhile stays pending.
        // (A foreground run from idle waits out the cooldown first.)
        h.spoken.lines.removeAll()
        await gate.rearm()
        XCTAssertNil(h.state.requestRescan(.foreground), "inside the cooldown, from idle")
        h.clock.now += AppState.foregroundRescanCooldown + 1
        let blocked = try XCTUnwrap(h.state.requestRescan(.foreground))
        await gate.untilParked()
        let provider = NSItemProvider()
        provider.registerDataRepresentation(
            forTypeIdentifier: FileTreeSidebar.fileURLUTType, visibility: .all
        ) { _ in nil }
        let importBatch = try XCTUnwrap(
            h.state.beginImportBatch(providers: [provider], destinationFolder: ""))
        XCTAssertEqual(h.state.requestRescan(.explicit), blocked)
        await gate.release()
        await blocked.value
        XCTAssertEqual(calls.count("scan"), 5, "the follow-up waits for the import")
        XCTAssertEqual(h.state.rescanRun.pendingReason, .explicit, "never dropped")
        XCTAssertEqual(h.spoken.lines, [])

        h.state.cancelImportBatch(importBatch)
        for _ in 0..<50 where calls.count("scan") < 6 {
            await Task.yield()
            try await Task.sleep(nanoseconds: 10_000_000)
        }
        await h.state.vaultRescanTask?.value
        XCTAssertEqual(calls.count("scan"), 6, "the import settled and the follow-up ran")
        XCTAssertEqual(h.spoken.lines, ["Files refreshed. No changes."])
        XCTAssertNil(h.state.rescanRun.pendingReason)
    }

    /// Windows' `TheForegroundRouteForwardsOnlyAReturnAfterBeingAway`, then
    /// the route on AppState: never on the first activation (the open scan
    /// just ran), never after under 2 s away, never under the palette, Quick
    /// Open or any sheet, and never inside the 5 s cooldown of the last
    /// rescan; otherwise the return to Slate requests a foreground rescan.
    func testTheForegroundRouteSkipsTheFirstActivationShortAbsencesAndModalSurfaces() async throws {
        let away = ForegroundRescanRoute.minimumAway
        let start = Date(timeIntervalSince1970: 2_000_000_000)
        var route = ForegroundRescanRoute()
        XCTAssertFalse(route.onBecomeActive(at: start, modalSurfaceOpen: false), "first activation")
        route.onResignActive(at: start)
        XCTAssertFalse(
            route.onBecomeActive(at: start + away - 0.001, modalSurfaceOpen: false),
            "under two seconds away")
        route.onResignActive(at: start + away)
        XCTAssertFalse(
            route.onBecomeActive(at: start + 2 * away, modalSurfaceOpen: true), "a modal surface")
        route.onResignActive(at: start + 2 * away)
        XCTAssertTrue(route.onBecomeActive(at: start + 3 * away, modalSurfaceOpen: false))
        XCTAssertFalse(
            route.onBecomeActive(at: start + 9 * away, modalSurfaceOpen: false),
            "an activation with no absence before it")

        // Opened behind another app, however long away: its first activation
        // still forwards nothing.
        var behind = ForegroundRescanRoute()
        behind.onResignActive(at: start)
        XCTAssertFalse(behind.onBecomeActive(at: start + 100, modalSurfaceOpen: false))

        // Observation that starts while the app is already active has seen
        // the first activation.
        var late = ForegroundRescanRoute()
        late.beginObserving(appIsActive: true)
        late.onResignActive(at: start)
        XCTAssertTrue(late.onBecomeActive(at: start + away, modalSurfaceOpen: false))

        // On AppState, the palette and Quick Open are modal surfaces, and the
        // cooldown starts from the last rescan's end.
        let h = try await open(["alpha.md": "# Alpha\n"])
        let calls = h.recordCoreCalls()
        h.state.beginForegroundRescanObservation(appIsActive: false)
        XCTAssertNil(h.state.applicationDidBecomeActive(), "the first activation")

        h.state.applicationDidResignActive()
        h.clock.now += away + 1
        h.state.isCommandPaletteOpen = true
        XCTAssertNil(h.state.applicationDidBecomeActive(), "the palette is open")
        h.state.isCommandPaletteOpen = false

        h.state.applicationDidResignActive()
        h.clock.now += away + 1
        h.state.isQuickSwitcherOpen = true
        XCTAssertNil(h.state.applicationDidBecomeActive(), "Quick Open is open")
        h.state.isQuickSwitcherOpen = false

        h.state.applicationDidResignActive()
        h.clock.now += away - 1
        XCTAssertNil(h.state.applicationDidBecomeActive(), "one second away")

        h.state.applicationDidResignActive()
        h.clock.now += away + 1
        let run = try XCTUnwrap(h.state.applicationDidBecomeActive(), "a real return")
        await run.value
        XCTAssertEqual(calls.count("scan"), 1)
        XCTAssertEqual(h.spoken.lines, [], "nothing changed, so the return was silent")

        h.state.applicationDidResignActive()
        h.clock.now += away + 1
        XCTAssertNil(h.state.applicationDidBecomeActive(), "inside the cooldown")
        h.state.applicationDidResignActive()
        h.clock.now += AppState.foregroundRescanCooldown
        let next = try XCTUnwrap(h.state.applicationDidBecomeActive(), "past the cooldown")
        await next.value
        XCTAssertEqual(calls.count("scan"), 2)
    }

    /// PR 12's rule for the rescan (locked decision 05 §4): ONE token for the
    /// run, carried by the scan, every inventory page and every indexed-hash
    /// chunk; a close cancels it, and the cancelled run publishes and speaks
    /// nothing. The source census stays green with an empty allowlist.
    func testOneTokenCoversTheScanAndEveryPage() async throws {
        let h = try await open([
            "a.md": "a\n", "b.md": "b\n", "c.md": "c\n", "d.md": "d\n", "e.md": "e\n",
        ])
        _ = try await h.openNote("a.md")
        _ = try await h.openNoteInNewTab("b.md")
        _ = try await h.openNoteInNewTab("c.md")
        let calls = h.recordCoreCalls()
        h.state.rescanRun.inventoryPageLimit = 2
        h.state.rescanRun.indexedHashChunkLimit = 2

        try await h.refresh()

        XCTAssertEqual(calls.operations.first, "scan")
        XCTAssertGreaterThanOrEqual(calls.count("inventory"), 3, "five documents in pages of two")
        XCTAssertGreaterThanOrEqual(calls.count("hashes"), 2, "three tabs in chunks of two")
        XCTAssertEqual(Set(calls.tokens).count, 1, "one token for the whole run")
        XCTAssertEqual(h.spoken.lines, ["Files refreshed. No changes."])

        // A close cancels the parked run's token; the run says nothing.
        h.spoken.lines.removeAll()
        let gate = Gate()
        h.state.rescanRun.afterScanHook = { await gate.park() }
        let run = try XCTUnwrap(h.state.requestRescan(.explicit))
        await gate.untilParked()
        let token = try XCTUnwrap(h.state.rescanRun.cancelToken)
        XCTAssertFalse(token.isCancelled())
        XCTAssertTrue(h.state.closeVault())
        XCTAssertTrue(token.isCancelled(), "the close cancels the run's one token")
        XCTAssertFalse(h.state.isRescanning)
        await gate.release()
        await run.value
        XCTAssertFalse(
            h.spoken.lines.contains { $0.hasPrefix("Files refreshed") },
            "a cancelled run speaks nothing")
        XCTAssertTrue(h.state.files.isEmpty, "nor publishes into the closed vault")

        let census = try CancelTokenCensusTests.productionCensus()
        XCTAssertTrue(
            census.sites.isEmpty,
            "no fresh token is passed straight into a call: \(census.sites)")
    }

    // MARK: - Supporting facts

    /// VoiceOver drops an announcement posted while a sheet closes or while
    /// it reads the window a return to Slate brings back (found in this PR's
    /// VoiceOver check: a palette Refresh of an unchanged vault and a
    /// foreground rescan were both silent). A Refresh chosen from the palette
    /// holds its sentence until the palette is gone and the settle has
    /// passed; a foreground rescan's sentence follows the activation by the
    /// settle. A close meanwhile still silences the run.
    func testTheSentenceWaitsForThePaletteItWasChosenFromAndForTheReturnsReading() async throws {
        let h = try await open(["alpha.md": "# Alpha\n"])
        h.state.rescanRun.sentenceSettle = 0.2
        h.state.isCommandPaletteOpen = true
        try h.write("late.md", "# Late\n")
        _ = try h.state.dispatchSidebarAction(id: SlateCommandID.sidebarRefresh)
        let run = try XCTUnwrap(h.state.vaultRescanTask)
        try await Task.sleep(nanoseconds: 400_000_000)
        XCTAssertEqual(h.spoken.lines, [], "nothing is said while the palette is still up")
        XCTAssertTrue(h.state.isRescanning, "the run holds its sentence")
        h.state.isCommandPaletteOpen = false
        let closed = ProcessInfo.processInfo.systemUptime
        await run.value
        XCTAssertGreaterThanOrEqual(ProcessInfo.processInfo.systemUptime - closed, 0.15)
        XCTAssertEqual(h.spoken.lines, ["Files refreshed. 1 new or changed, 0 removed."])

        h.spoken.lines.removeAll()
        h.state.beginForegroundRescanObservation(appIsActive: true)
        h.state.applicationDidResignActive()
        h.clock.now += 10
        try h.write("later.md", "# Later\n")
        let returned = ProcessInfo.processInfo.systemUptime
        let foreground = try XCTUnwrap(h.state.applicationDidBecomeActive())
        await foreground.value
        XCTAssertGreaterThanOrEqual(ProcessInfo.processInfo.systemUptime - returned, 0.15)
        XCTAssertEqual(h.spoken.lines, ["Files refreshed. 1 new or changed, 0 removed."])

        h.spoken.lines.removeAll()
        h.state.isCommandPaletteOpen = true
        try h.write("lost.md", "# Lost\n")
        _ = try h.state.dispatchSidebarAction(id: SlateCommandID.sidebarRefresh)
        let closing = try XCTUnwrap(h.state.vaultRescanTask)
        try await Task.sleep(nanoseconds: 300_000_000)
        h.state.isCommandPaletteOpen = false
        XCTAssertTrue(h.state.closeVault())
        await closing.value
        XCTAssertFalse(
            h.spoken.lines.contains { $0.hasPrefix("Files refreshed") },
            "a run closed while it held its sentence says nothing")
    }

    /// Windows' `RefreshIsUnavailableDuringAnImportAndSaysWhy` and
    /// `RefreshIsUnavailableDuringATrashAndSaysWhy`: while an import or a
    /// Trash operation runs (or the open scan does) a rescan would be refused,
    /// so Refresh is unavailable and says why, in the structural gate's words,
    /// and a foreground request is refused silently.
    func testRefreshIsUnavailableDuringAnImportOrATrashAndSaysWhy() async throws {
        let h = try await open(["alpha.md": "# Alpha\n", "beta.md": "# Beta\n"])
        func reason() -> String? {
            h.state.sidebarActionProjection(surface: .commandPalette)
                .first { $0.id == SlateCommandID.sidebarRefresh }?.disabledReason
        }
        XCTAssertNil(reason())

        let provider = NSItemProvider()
        provider.registerDataRepresentation(
            forTypeIdentifier: FileTreeSidebar.fileURLUTType, visibility: .all
        ) { _ in nil }
        let importBatch = try XCTUnwrap(
            h.state.beginImportBatch(providers: [provider], destinationFolder: ""))
        XCTAssertEqual(reason(), AppState.structuralMutationBusyReason)
        XCTAssertThrowsError(try h.state.dispatchSidebarAction(id: SlateCommandID.sidebarRefresh))
        XCTAssertNil(h.state.requestRescan(.foreground))
        h.state.cancelImportBatch(importBatch)
        XCTAssertNil(reason())

        let trashGate = TrashGate()
        h.state.cancellableSingleTrashRunner = { _, _, _, _, _ in await trashGate.enter() }
        XCTAssertTrue(h.state.requestDeleteEntry(path: "beta.md", isDirectory: false))
        let trash = try XCTUnwrap(h.state.pendingStructuralTaskForTesting)
        await trashGate.untilEntered()
        XCTAssertNotNil(h.state.trashProgress)
        XCTAssertEqual(reason(), AppState.structuralMutationBusyReason)
        XCTAssertNil(h.state.requestRescan(.explicit))
        await trashGate.release()
        await trash.value
        XCTAssertNil(reason())
    }

    actor TrashGate {
        private var entered = false
        private var waiters: [CheckedContinuation<Void, Never>] = []
        private var held: CheckedContinuation<Void, Never>?

        func enter() async {
            entered = true
            for waiter in waiters { waiter.resume() }
            waiters = []
            await withCheckedContinuation { held = $0 }
        }

        func untilEntered() async {
            guard !entered else { return }
            await withCheckedContinuation { waiters.append($0) }
        }

        func release() {
            held?.resume()
            held = nil
        }
    }

    /// Refresh acts on the vault, not on the selection: a selected file that
    /// was deleted outside Slate (the very case Refresh exists for) never
    /// refuses it as a stale selection.
    func testRefreshRunsWhenTheSelectedFileWasDeletedOutsideSlate() async throws {
        let h = try await open(["alpha.md": "# Alpha\n", "gone.md": "# Gone\n"])
        let session = try XCTUnwrap(h.state.currentSession)
        _ = h.state.publishSidebarSelectionSnapshot(
            SidebarSelectionSnapshot(
                sessionIdentity: ObjectIdentifier(session),
                items: [SidebarSelectionItem(path: "gone.md", isDirectory: false, isMarkdown: true)],
                focusedPath: "gone.md",
                creationParent: ""))
        try h.remove("gone.md")

        try await h.refresh()
        XCTAssertEqual(h.spoken.lines, ["Files refreshed. 0 new or changed, 1 removed."])
        XCTAssertFalse(h.state.files.contains { $0.path == "gone.md" })
    }

    /// The lattice orders the reasons: the join takes the higher rank, and
    /// nothing joins down.
    func testThePendingReasonLatticeIsOrderedAndExplicitDominates() {
        XCTAssertEqual(RescanReason.joined(nil, .foreground), .foreground)
        XCTAssertEqual(RescanReason.joined(nil, .explicit), .explicit)
        XCTAssertEqual(RescanReason.joined(.foreground, .foreground), .foreground)
        XCTAssertEqual(RescanReason.joined(.foreground, .explicit), .explicit)
        XCTAssertEqual(RescanReason.joined(.explicit, .foreground), .explicit)
        XCTAssertLessThan(RescanReason.foreground, RescanReason.explicit)
    }

    /// The tree's own refetch for a rescan: awaited to its publication,
    /// silent, and a level it cannot list comes back counted (the root here);
    /// the next refetch that can list it publishes it and counts nothing.
    func testTheTreeRefetchPublishesSilentlyAndCountsAFailedLevel() async throws {
        let tree = FileTreeViewModel()
        let failRoot = Box(false)
        let names = Box(["one.md"])
        tree.bindAsynchronouslyForTesting(cancellablePagedFetcher: { _, _, _ in
            if failRoot.value { throw FileTreeLevelPageUnavailable() }
            return DirListing(
                dirs: [],
                files: FileSummaryPage(
                    items: names.value.map { Self.summary($0) }, nextCursor: nil,
                    totalFiltered: UInt64(names.value.count)))
        })
        _ = await tree.settleLevelLoadsForTesting()
        XCTAssertEqual(tree.rootLevel.map(\.path), ["one.md"])

        names.value = ["one.md", "two.md"]
        let failures = await tree.refreshForRescan()
        XCTAssertEqual(failures, 0)
        XCTAssertEqual(tree.rootLevel.map(\.path), ["one.md", "two.md"])

        failRoot.value = true
        let failed = await tree.refreshForRescan()
        XCTAssertEqual(failed, 1, "the root level could not be listed: one failed operation")
    }

    final class Box<Value>: @unchecked Sendable {
        var value: Value
        init(_ value: Value) { self.value = value }
    }

    private static func summary(_ path: String) -> FileSummary {
        FileSummary(
            path: path, name: path, mtimeMs: 0, sizeBytes: 1, isMarkdown: true,
            displayName: nil, createdDate: nil, createdMs: nil, wordCount: nil,
            preview: nil, taskTotal: 0, taskOpen: 0)
    }

    /// A removed selected row lands the keys on its nearest survivor, the
    /// tree's own removal landing (the one an in-app Trash uses): the removed
    /// identity leaves the selection, a surviving neighbour takes the focus,
    /// and a row that survived keeps it.
    func testARemovedSelectedRowLandsTheKeysOnTheNearestSurvivor() throws {
        typealias Row = FileTreeSidebar.SelectionRow
        func file(_ path: String) -> Row {
            Row(identity: .node(.file(path: path)), path: path, isDirectory: false, isMarkdown: true)
        }
        let before = [file("a.md"), file("b.md"), file("c.md")]
        let after = [file("a.md"), file("c.md")]
        var model = FileTreeSidebar.SelectionModel()
        model.reveal(file("b.md"))

        let landed = try XCTUnwrap(
            FileTreeSidebar.rescanLanding(model: model, before: before, after: after))
        let focus = try XCTUnwrap(landed.focused)
        XCTAssertNotEqual(focus, file("b.md").identity, "the removed row lost the keys")
        XCTAssertTrue(after.contains { $0.identity == focus }, "a surviving neighbour took them")
        XCTAssertFalse(landed.selected.contains(file("b.md").identity))

        var kept = FileTreeSidebar.SelectionModel()
        kept.reveal(file("a.md"))
        XCTAssertNil(
            FileTreeSidebar.rescanLanding(model: kept, before: before, after: after),
            "keys on a row that survived never move")
    }
}
