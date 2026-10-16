// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import XCTest

@testable import SlateMac

/// PR 15b (#784): ⌘Q with unsaved edits behaves like Close Vault — the same
/// refusals, the same dirty aggregation, the same prompts and the same
/// resolvers — and never discards silently.
///
/// `SlateAppDelegate.terminationDecision` is the delegate's decision
/// function; `reply` stands in for `NSApp.reply(toApplicationShouldTerminate:)`,
/// which is owed exactly once after every `.terminateLater`: true once Save
/// All or Discard All (or the single note's Save or Discard) succeeds and the
/// sidebar fence settles, false on Cancel, on a save conflict or error, or
/// when another vault transition takes the prompt over. A quit adds no line
/// of its own: the Close Vault lines end "Returned to the welcome screen",
/// which a quit never does (the Windows application-close route is silent
/// for the same reason).
@MainActor
final class TerminationFlowTests: XCTestCase {

    private final class RecordingAnnouncer: AnnouncementPosting {
        private(set) var messages: [String] = []

        func post(_ message: String, priority: AnnouncementPriority) {
            messages.append(message)
        }

        func reset() {
            messages = []
        }
    }

    /// What the quit replied to AppKit, in order.
    @MainActor
    private final class ReplyRecorder {
        private(set) var values: [Bool] = []

        func record(_ proceed: Bool) {
            values.append(proceed)
        }
    }

    /// Holds a queued sidebar write on its worker until the test opens it.
    private final class GateBox: @unchecked Sendable {
        private let semaphore = DispatchSemaphore(value: 0)
        func wait() { semaphore.wait() }
        func open() { semaphore.signal() }
    }

    private var tempDir: URL!

    override func setUpWithError() throws {
        try super.setUpWithError()
        tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-termination-\(UUID().uuidString)")
        try FileManager.default.createDirectory(
            at: tempDir, withIntermediateDirectories: true)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: tempDir)
        try super.tearDownWithError()
    }

    // MARK: - Fixtures

    private func makeState(announcer: AnnouncementPosting) -> AppState {
        AppState(
            recentsStore: RecentVaultsStore(
                fileURL: tempDir.appendingPathComponent(
                    "recents-\(UUID().uuidString).json")),
            externalOpener: { _ in true },
            announcer: announcer)
    }

    /// A vault with `note.md` and `two.md`.
    private func makeVault(_ name: String) throws -> URL {
        let vault = tempDir.appendingPathComponent(name)
        try FileManager.default.createDirectory(
            at: vault, withIntermediateDirectories: true)
        try "# note\n".write(
            to: vault.appendingPathComponent("note.md"),
            atomically: true, encoding: .utf8)
        try "# two\n".write(
            to: vault.appendingPathComponent("two.md"),
            atomically: true, encoding: .utf8)
        return vault
    }

    private func openVault(
        _ name: String = "A",
        announcer: AnnouncementPosting
    ) async throws -> (AppState, URL) {
        let vault = try makeVault(name)
        let state = makeState(announcer: announcer)
        state.openVault(at: vault)
        await state.scanTask?.value
        XCTAssertNotNil(state.currentSession, "the fixture vault opens")
        return (state, vault)
    }

    /// `two.md` dirty in a parked tab, `note.md` dirty in the active one,
    /// through the real editor path.
    private func dirtyTwoNotes(_ state: AppState) async {
        state.selectedFilePath = "two.md"
        await state.noteLoadTask?.value
        state.updateEditorText("# two\n\ndirty two.\n")
        state.newTab()
        state.selectedFilePath = "note.md"
        await state.noteLoadTask?.value
        state.updateEditorText("# note\n\ndirty note.\n")
        XCTAssertTrue(state.hasUnsavedChanges)
        XCTAssertEqual(state.workspace.dirtyParkedDocuments().count, 1)
    }

    /// `note.md` dirty in the only tab.
    private func dirtyOneNote(_ state: AppState) async {
        state.selectedFilePath = "note.md"
        await state.noteLoadTask?.value
        state.updateEditorText("# note\n\ndirty note.\n")
        XCTAssertTrue(state.hasUnsavedChanges)
        XCTAssertTrue(state.workspace.dirtyParkedDocuments().isEmpty)
    }

    private func quit(
        _ state: AppState,
        _ replies: ReplyRecorder
    ) -> NSApplication.TerminateReply {
        SlateAppDelegate.terminationDecision(
            appState: state, reply: replies.record)
    }

    private func onDisk(_ vault: URL, _ path: String) throws -> String {
        try String(
            contentsOf: vault.appendingPathComponent(path), encoding: .utf8)
    }

    // MARK: - The seven facts (spec §Tests)

    func testQuitWithADirtyTabPresentsThePromptAndNeverDiscardsSilently()
        async throws
    {
        let announcer = RecordingAnnouncer()
        let (state, vault) = try await openVault(announcer: announcer)
        await dirtyTwoNotes(state)
        announcer.reset()
        let replies = ReplyRecorder()

        XCTAssertEqual(
            quit(state, replies), .terminateLater,
            "a quit with unsaved edits waits for the person's answer")
        XCTAssertEqual(
            state.pendingVaultClose, 2,
            "the Close Vault prompt asks about both notes")
        XCTAssertTrue(state.isTerminationPending)
        XCTAssertEqual(replies.values, [], "nothing is replied while the prompt is up")
        XCTAssertTrue(state.hasUnsavedChanges, "the active edit is still in its editor")
        XCTAssertEqual(
            state.workspace.dirtyParkedDocuments().count, 1,
            "the parked edit is still in its tab")
        XCTAssertNotNil(state.currentSession, "the vault stays open behind the prompt")
        XCTAssertEqual(try onDisk(vault, "note.md"), "# note\n")
        XCTAssertEqual(try onDisk(vault, "two.md"), "# two\n")
        XCTAssertEqual(announcer.messages, [], "the prompt is the quit's only output")
    }

    func testQuitSaveAllSavesEveryDirtyTabThenReplies() async throws {
        let announcer = RecordingAnnouncer()
        let (state, vault) = try await openVault(announcer: announcer)
        await dirtyTwoNotes(state)
        announcer.reset()
        let replies = ReplyRecorder()
        XCTAssertEqual(quit(state, replies), .terminateLater)

        state.resolveVaultCloseSaveAll()
        await state.vaultCloseSaveAllTask?.value
        await state.terminationFenceTaskForTesting?.value

        XCTAssertEqual(try onDisk(vault, "note.md"), "# note\n\ndirty note.\n")
        XCTAssertEqual(try onDisk(vault, "two.md"), "# two\n\ndirty two.\n")
        XCTAssertEqual(
            replies.values, [true],
            "the quit goes ahead once every note is saved")
        XCTAssertFalse(state.isTerminationPending)
        XCTAssertNil(state.pendingVaultClose)
        XCTAssertNotNil(
            state.currentSession,
            "AppKit terminates with the vault open, as a clean quit does")
        XCTAssertFalse(
            announcer.messages.contains { $0.contains("welcome screen") },
            "a quit never says it returned to the welcome screen")
    }

    func testQuitDiscardAllRepliesWithoutSaving() async throws {
        let announcer = RecordingAnnouncer()
        let (state, vault) = try await openVault(announcer: announcer)
        await dirtyTwoNotes(state)
        announcer.reset()
        let replies = ReplyRecorder()
        XCTAssertEqual(quit(state, replies), .terminateLater)

        state.resolveVaultCloseDiscardAll()
        await state.terminationFenceTaskForTesting?.value

        XCTAssertEqual(replies.values, [true], "Discard All lets the quit go ahead")
        XCTAssertEqual(try onDisk(vault, "note.md"), "# note\n", "nothing was saved")
        XCTAssertEqual(try onDisk(vault, "two.md"), "# two\n", "nothing was saved")
        XCTAssertFalse(state.isTerminationPending)
        XCTAssertNil(state.pendingVaultClose)
        XCTAssertEqual(
            announcer.messages, [],
            "a quit never says it returned to the welcome screen")
    }

    func testQuitCancelKeepsTheAppRunning() async throws {
        let (state, vault) = try await openVault(announcer: RecordingAnnouncer())
        await dirtyTwoNotes(state)
        let replies = ReplyRecorder()
        XCTAssertEqual(quit(state, replies), .terminateLater)

        state.resolveVaultCloseCancel()

        XCTAssertEqual(replies.values, [false], "Cancel keeps the app running")
        XCTAssertFalse(state.isTerminationPending)
        XCTAssertNil(state.pendingVaultClose, "the prompt is dismissed")
        XCTAssertNotNil(state.currentSession, "the vault stays open")
        XCTAssertTrue(state.hasUnsavedChanges, "the active edit survives the cancel")
        XCTAssertEqual(state.workspace.dirtyParkedDocuments().count, 1)
        XCTAssertEqual(try onDisk(vault, "note.md"), "# note\n")

        // Nothing was forgotten: the next quit asks again.
        XCTAssertEqual(quit(state, replies), .terminateLater)
        XCTAssertEqual(state.pendingVaultClose, 2)
        state.resolveVaultCloseCancel()
        XCTAssertEqual(replies.values, [false, false])

        // The single note's "Save changes?" Cancel answers the same way.
        let (single, _) = try await openVault("B", announcer: RecordingAnnouncer())
        await dirtyOneNote(single)
        let singleReplies = ReplyRecorder()
        XCTAssertEqual(quit(single, singleReplies), .terminateLater)
        XCTAssertEqual(single.pendingNavigation, .closeVault)
        single.resolvePendingNavigationCancel()
        XCTAssertEqual(singleReplies.values, [false])
        XCTAssertNil(single.pendingNavigation)
        XCTAssertTrue(single.hasUnsavedChanges)
    }

    func testQuitWithCleanTabsTerminatesImmediately() async throws {
        let (state, _) = try await openVault(announcer: RecordingAnnouncer())
        state.selectedFilePath = "note.md"
        await state.noteLoadTask?.value
        XCTAssertFalse(state.hasUnsavedChanges)
        XCTAssertFalse(
            AppState.hasPendingSidebarWorkAtTermination,
            "precondition: the sidebar fence is clear")
        let replies = ReplyRecorder()

        XCTAssertEqual(quit(state, replies), .terminateNow)
        XCTAssertEqual(replies.values, [], ".terminateNow owes no reply")
        XCTAssertNil(state.pendingVaultClose)
        XCTAssertNil(state.pendingNavigation)
        XCTAssertFalse(state.isTerminationPending)

        // With no vault open there is nothing to ask about either.
        let welcome = makeState(announcer: RecordingAnnouncer())
        XCTAssertEqual(quit(welcome, replies), .terminateNow)
        XCTAssertEqual(replies.values, [])
    }

    func testTheSidebarFenceStillSettlesAfterTheDirtyTabsResolve() async throws {
        let gate = GateBox()
        defer { gate.open() }
        let (state, vault) = try await openVault(announcer: RecordingAnnouncer())
        await dirtyTwoNotes(state)
        state.enqueueSidebarOrganizationWriteForTesting { root in
            gate.wait()
            root["landedBeforeQuit"] = true
        }
        XCTAssertTrue(
            AppState.hasPendingSidebarWorkAtTermination,
            "a queued sidebar write holds the fence")
        let replies = ReplyRecorder()

        XCTAssertEqual(quit(state, replies), .terminateLater)
        XCTAssertEqual(
            state.pendingVaultClose, 2,
            "the dirty tabs are asked about first")
        XCTAssertNil(
            state.terminationFenceTaskForTesting,
            "the fence waits behind the prompt")

        state.resolveVaultCloseDiscardAll()
        XCTAssertEqual(
            replies.values, [],
            "the queued sidebar write still holds the quit")
        let fence = try XCTUnwrap(state.terminationFenceTaskForTesting)
        gate.open()
        await fence.value

        XCTAssertEqual(replies.values, [true], "the quit goes ahead after the fence")
        let data = try Data(
            contentsOf: vault.appendingPathComponent(".slate/sidebar.json"))
        let json = try XCTUnwrap(
            JSONSerialization.jsonObject(with: data) as? [String: Any])
        XCTAssertEqual(
            json["landedBeforeQuit"] as? Bool, true,
            "the queued write landed before the reply")
    }

    func testASaveConflictDuringQuitKeepsTheAppRunning() async throws {
        let (state, vault) = try await openVault(announcer: RecordingAnnouncer())
        await dirtyTwoNotes(state)
        // The active note changed on disk: its save conflicts.
        try "external\n".write(
            to: vault.appendingPathComponent("note.md"),
            atomically: true, encoding: .utf8)
        let replies = ReplyRecorder()
        XCTAssertEqual(quit(state, replies), .terminateLater)

        state.resolveVaultCloseSaveAll()
        await state.vaultCloseSaveAllTask?.value

        XCTAssertEqual(replies.values, [false], "a conflict keeps the app running")
        XCTAssertNotNil(
            state.currentSaveConflict,
            "the standard conflict dialog owns recovery")
        XCTAssertFalse(state.isTerminationPending)
        XCTAssertNil(
            state.pendingVaultClose,
            "no prompt is left over to become a plain Close Vault")
        XCTAssertNotNil(state.currentSession, "the vault stays open")
        XCTAssertTrue(state.hasUnsavedChanges, "the conflicting edit is kept")
        XCTAssertEqual(try onDisk(vault, "note.md"), "external\n")
        XCTAssertEqual(
            try onDisk(vault, "two.md"), "# two\n",
            "Save All stops at the conflict")

        // The single note's Save meets a conflict the same way.
        let (single, singleVault) = try await openVault(
            "B", announcer: RecordingAnnouncer())
        await dirtyOneNote(single)
        try "external\n".write(
            to: singleVault.appendingPathComponent("note.md"),
            atomically: true, encoding: .utf8)
        let singleReplies = ReplyRecorder()
        XCTAssertEqual(quit(single, singleReplies), .terminateLater)
        XCTAssertEqual(single.pendingNavigation, .closeVault)

        await single.resolvePendingNavigationSave()?.value

        XCTAssertEqual(singleReplies.values, [false])
        XCTAssertNotNil(single.currentSaveConflict)
        XCTAssertNil(
            single.pendingNavigation,
            "no prompt is left over to become a plain Close Vault")
        XCTAssertFalse(single.isTerminationPending)
        XCTAssertNotNil(single.currentSession)
    }

    // MARK: - The single-note prompt

    func testQuitWithOneDirtyNoteSavesThroughTheSaveChangesPrompt() async throws {
        let announcer = RecordingAnnouncer()
        let (state, vault) = try await openVault(announcer: announcer)
        await dirtyOneNote(state)
        announcer.reset()
        let replies = ReplyRecorder()

        XCTAssertEqual(quit(state, replies), .terminateLater)
        XCTAssertEqual(
            state.pendingNavigation, .closeVault,
            "one dirty note gets Close Vault's single-note prompt")
        XCTAssertNil(state.pendingVaultClose)

        await state.resolvePendingNavigationSave()?.value
        await state.terminationFenceTaskForTesting?.value

        XCTAssertEqual(replies.values, [true])
        XCTAssertEqual(try onDisk(vault, "note.md"), "# note\n\ndirty note.\n")
        XCTAssertNil(state.pendingNavigation)
        XCTAssertFalse(state.isTerminationPending)
        XCTAssertNotNil(state.currentSession)
        XCTAssertFalse(announcer.messages.contains { $0.contains("welcome screen") })
    }

    func testQuitWithOneDirtyNoteDiscardRepliesWithoutSaving() async throws {
        let (state, vault) = try await openVault(announcer: RecordingAnnouncer())
        await dirtyOneNote(state)
        let replies = ReplyRecorder()
        XCTAssertEqual(quit(state, replies), .terminateLater)

        state.resolvePendingNavigationDiscard()
        await state.terminationFenceTaskForTesting?.value

        XCTAssertEqual(replies.values, [true])
        XCTAssertEqual(try onDisk(vault, "note.md"), "# note\n")
        XCTAssertNil(state.pendingNavigation)
        XCTAssertFalse(state.isTerminationPending)
    }

    // MARK: - Every exit replies exactly once (spec §Risks)

    func testAVaultSwitchStartedWhileTheQuitPromptIsUpEndsTheQuitFirst()
        async throws
    {
        let (state, _) = try await openVault(announcer: RecordingAnnouncer())
        let vaultB = try makeVault("B")
        await dirtyTwoNotes(state)
        let replies = ReplyRecorder()
        XCTAssertEqual(quit(state, replies), .terminateLater)

        state.switchToRecent(RecentVault(url: vaultB))

        XCTAssertEqual(
            replies.values, [false],
            "the newer request owns the prompt; the quit is answered first")
        XCTAssertFalse(state.isTerminationPending)
        XCTAssertEqual(state.pendingVaultClose, 2, "the switch's own prompt")
        XCTAssertEqual(state.pendingVaultSwitchTarget?.path, vaultB.path)

        state.resolveVaultCloseCancel()
        XCTAssertEqual(replies.values, [false], "answered exactly once")
    }

    func testASecondQuitWhileTheFirstWaitsIsRefusedAndTheFirstRepliesOnce()
        async throws
    {
        let (state, _) = try await openVault(announcer: RecordingAnnouncer())
        await dirtyTwoNotes(state)
        let first = ReplyRecorder()
        let second = ReplyRecorder()
        XCTAssertEqual(quit(state, first), .terminateLater)

        XCTAssertEqual(
            quit(state, second), .terminateCancel,
            "AppKit never waits on two replies")
        XCTAssertEqual(state.pendingVaultClose, 2, "the first quit's prompt stays up")

        state.resolveVaultCloseDiscardAll()
        await state.terminationFenceTaskForTesting?.value

        XCTAssertEqual(first.values, [true])
        XCTAssertEqual(second.values, [])
    }

    func testQuitWhileASaveIsInFlightIsRefusedWithTheCloseVaultReason()
        async throws
    {
        let (state, _) = try await openVault(announcer: RecordingAnnouncer())
        await dirtyOneNote(state)
        let replies = ReplyRecorder()
        let save = try XCTUnwrap(state.saveCurrentNote())

        XCTAssertEqual(
            quit(state, replies), .terminateCancel,
            "Close Vault refuses while a save is in flight; so does quit")
        XCTAssertEqual(
            state.lastMutationAnnouncement, AppState.saveInProgressReason,
            "the refusal is spoken with Close Vault's reason")
        XCTAssertNil(state.pendingNavigation)
        XCTAssertFalse(state.isTerminationPending)

        await save.value
        XCTAssertFalse(state.hasUnsavedChanges)
        XCTAssertEqual(quit(state, replies), .terminateNow, "the saved note quits at once")
        XCTAssertEqual(replies.values, [])
    }
}
