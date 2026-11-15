// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import Combine
import XCTest

@testable import SlateMac

/// Plan 43 PR 29 (spec `docs/plans/43_mac_backlog_delivery/specs/pr29_hot_exit_mac_half.md`):
/// unsaved notes are kept in core's hot-exit store as you type, a quit or a
/// switch closes without asking once they are kept, and the next open
/// restores each one dirty. Each fact runs over its own temp vault and its
/// own `HotExitStoreHost` on a temp device root (the real core store).
/// "A new process" is `simulateProcessDeathForTests()` and a fresh host and
/// `AppState` on the same device root.
@MainActor
final class HotExitTests: XCTestCase {

    final class RecordingAnnouncer: AnnouncementPosting {
        private(set) var messages: [String] = []
        func post(_ message: String, priority: AnnouncementPriority) { messages.append(message) }
        func reset() { messages = [] }
    }

    @MainActor
    final class ReplyRecorder {
        private(set) var values: [Bool] = []
        func record(_ proceed: Bool) { values.append(proceed) }
    }

    /// Holds a store call on its worker until the test opens it.
    final class Gate: @unchecked Sendable {
        private let entered = DispatchSemaphore(value: 0)
        private let released = DispatchSemaphore(value: 0)
        func hold() {
            entered.signal()
            released.wait()
        }
        func waitEntered(_ timeout: TimeInterval = 5) -> Bool {
            entered.wait(timeout: .now() + timeout) == .success
        }
        func open() { released.signal() }
    }

    var tempDir: URL!
    var deviceRoot: URL!
    private var savedDebounce: TimeInterval = 1

    override func setUpWithError() throws {
        try super.setUpWithError()
        tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-hot-exit-\(UUID().uuidString)")
        deviceRoot = tempDir.appendingPathComponent("device", isDirectory: true)
        try FileManager.default.createDirectory(at: deviceRoot, withIntermediateDirectories: true)
        savedDebounce = HotExitWriter.debounce
        HotExitWriter.debounce = 0.05
    }

    override func tearDownWithError() throws {
        HotExitWriter.debounce = savedDebounce
        try? FileManager.default.removeItem(at: tempDir)
        try super.tearDownWithError()
    }

    // MARK: - Fixtures

    func makeHost() -> HotExitStoreHost {
        let host = HotExitStoreHost.install(root: deviceRoot)
        XCTAssertTrue(host.hasStore, "the test device root opens a store")
        return host
    }

    /// The crash shape's "new process": the old host's holds go as the OS
    /// drops them, and a fresh host opens the same device root.
    func newProcess(after host: HotExitStoreHost) -> HotExitStoreHost {
        host.simulateProcessDeathForTests()
        return makeHost()
    }

    func makeState(
        host: HotExitStoreHost, announcer: AnnouncementPosting = RecordingAnnouncer()
    ) -> AppState {
        AppState(
            recentsStore: RecentVaultsStore(
                fileURL: tempDir.appendingPathComponent("recents-\(UUID().uuidString).json")),
            externalOpener: { _ in true },
            announcer: announcer,
            hotExitHost: host)
    }

    /// A vault with `note.md` and `two.md`.
    func makeVault(_ name: String = "A") throws -> URL {
        let vault = tempDir.appendingPathComponent(name)
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        try "# note\n".write(to: vault.appendingPathComponent("note.md"), atomically: true, encoding: .utf8)
        try "# two\n".write(to: vault.appendingPathComponent("two.md"), atomically: true, encoding: .utf8)
        return vault
    }

    func open(_ state: AppState, _ vault: URL) async {
        state.openVault(at: vault)
        await state.scanTask?.value
        XCTAssertNotNil(state.currentSession, "the fixture vault opens")
        await state.noteLoadTask?.value
    }

    func select(_ state: AppState, _ path: String) async {
        state.selectedFilePath = path
        await state.noteLoadTask?.value
        XCTAssertEqual(state.loadedFilePath, path)
    }

    /// The writer's own completion seam: nothing posted, debounced or in
    /// flight, and the store owes nothing for this vault.
    func settle(_ state: AppState) async {
        await state.settleHotExitForTesting()
        XCTAssertTrue(state.hotExitWriter?.isQuiescentForTests ?? true, "the writer settles")
    }

    func key(_ state: AppState) throws -> HotExitVaultKey {
        guard case .key(let key) = try XCTUnwrap(state.currentSession).hotExitKey() else {
            throw XCTSkip("this volume gives no hot-exit key")
        }
        return key
    }

    /// The store's entries, read through a second core store (no hold).
    func storedEntries(_ key: HotExitVaultKey) throws -> [HotExitEntry] {
        try HotExitStore.open(deviceRoot: deviceRoot.path).readBuffers(key: key).entries
    }

    func onDisk(_ vault: URL, _ path: String) throws -> String {
        try String(contentsOf: vault.appendingPathComponent(path), encoding: .utf8)
    }

    func quit(_ state: AppState, _ replies: ReplyRecorder) -> NSApplication.TerminateReply {
        SlateAppDelegate.terminationDecision(appState: state, reply: replies.record)
    }

    /// The tabs the workspace holds for `path`, active or parked.
    func buffers(_ state: AppState, path: String) -> [HotExitBuffer] {
        state.hotExitMarkdownTabs.compactMap { state.hotExitBuffer(for: $0) }
            .filter { $0.path == path }
    }

    // MARK: - Writing as you type (§3)

    func testADirtyTabClaimsAndWritesAtOnce() async throws {
        let host = makeHost()
        let state = makeState(host: host)
        let vault = try makeVault()
        await open(state, vault)
        await select(state, "note.md")
        let key = try key(state)
        XCTAssertTrue(try storedEntries(key).isEmpty)

        state.updateEditorText("# note\n\nkept as typed.\n")
        await settle(state)

        let entries = try storedEntries(key)
        XCTAssertEqual(entries.count, 1)
        XCTAssertEqual(entries.first?.path, "note.md")
        XCTAssertEqual(entries.first?.text, "# note\n\nkept as typed.\n")
        XCTAssertEqual(entries.first?.entryId, state.workspace.activeTab?.id.raw.uuidString)
        XCTAssertEqual(try onDisk(vault, "note.md"), "# note\n", "hot exit never writes the note")
    }

    func testAnEntryHoldsTheComposedNote() async throws {
        let host = makeHost()
        let state = makeState(host: host)
        let vault = try makeVault()
        try "---\ntitle: Ghost\n---\nBody\n".write(
            to: vault.appendingPathComponent("fm.md"), atomically: true, encoding: .utf8)
        await open(state, vault)
        await select(state, "fm.md")
        state.updateEditorText("Body edited\n")
        await settle(state)
        XCTAssertEqual(
            try storedEntries(try key(state)).first?.text, "---\ntitle: Ghost\n---\nBody edited\n")

        let next = makeState(host: newProcess(after: host))
        await open(next, vault)
        await select(next, "fm.md")
        XCTAssertEqual(next.currentNoteText, "Body edited\n")
        XCTAssertEqual(next.currentNoteFMSource, "title: Ghost\n")
        XCTAssertTrue(next.hasUnsavedChanges)
    }

    func testACleanTabRetiresItsEntry() async throws {
        let host = makeHost()
        let state = makeState(host: host)
        let vault = try makeVault()
        await open(state, vault)
        await select(state, "note.md")
        let key = try key(state)
        state.updateEditorText("# note\n\nedited.\n")
        await settle(state)
        XCTAssertEqual(try storedEntries(key).count, 1)

        // Undo to the baseline turns the tab clean: its entry retires.
        state.updateEditorText("# note\n")
        await settle(state)
        XCTAssertTrue(try storedEntries(key).isEmpty)

        // So does a landed save.
        state.updateEditorText("# note\n\nsaved.\n")
        await settle(state)
        XCTAssertEqual(try storedEntries(key).count, 1)
        await state.saveCurrentNote()?.value
        await settle(state)
        XCTAssertFalse(state.hasUnsavedChanges)
        XCTAssertTrue(try storedEntries(key).isEmpty)
    }

    func testTheSnapshotFollowsTheBufferBetweenFieldsAndDocument() async throws {
        let host = makeHost()
        let state = makeState(host: host)
        let vault = try makeVault()
        await open(state, vault)
        await select(state, "note.md")
        let key = try key(state)
        let noteTab = try XCTUnwrap(state.workspace.activeTab?.id)
        state.updateEditorText("# note\n\none.\n")
        await settle(state)
        state.newTab()
        await select(state, "two.md")
        await settle(state)
        // Parked, the note's entry still holds its text.
        XCTAssertEqual(
            try storedEntries(key).first { $0.entryId == noteTab.raw.uuidString }?.text,
            "# note\n\none.\n")
        state.activateTab(noteTab)
        state.updateEditorText("# note\n\none. two.\n")
        await settle(state)
        let entries = try storedEntries(key)
        XCTAssertEqual(entries.count, 1)
        XCTAssertEqual(entries.first?.text, "# note\n\none. two.\n")
        XCTAssertEqual(entries.first?.generation, 2, "one episode, its generations rising")
    }

    // MARK: - Quit (§4)

    func testAKeptQuitRepliesWithoutAPrompt() async throws {
        let host = makeHost()
        let state = makeState(host: host)
        let vault = try makeVault()
        await open(state, vault)
        await select(state, "two.md")
        state.updateEditorText("# two\n\ndirty two.\n")
        state.newTab()
        await select(state, "note.md")
        state.updateEditorText("# note\n\ndirty note.\n")
        let replies = ReplyRecorder()

        XCTAssertEqual(quit(state, replies), .terminateLater)
        await state.hotExitRouteTask?.value

        XCTAssertEqual(replies.values, [true])
        XCTAssertNil(state.pendingVaultClose, "no prompt")
        XCTAssertNil(state.pendingNavigation, "no prompt")
        XCTAssertEqual(try onDisk(vault, "note.md"), "# note\n")
        XCTAssertEqual(try onDisk(vault, "two.md"), "# two\n")
        state.saveWorkspaceLayout()

        let next = makeState(host: newProcess(after: host))
        await open(next, vault)
        XCTAssertEqual(buffers(next, path: "note.md").map(\.body), ["# note\n\ndirty note.\n"])
        XCTAssertEqual(buffers(next, path: "two.md").map(\.body), ["# two\n\ndirty two.\n"])
        XCTAssertTrue(buffers(next, path: "note.md").allSatisfy(\.dirty))
        XCTAssertTrue(buffers(next, path: "two.md").allSatisfy(\.dirty))
    }

    func testACleanQuitIsImmediate() async throws {
        let host = makeHost()
        let state = makeState(host: host)
        await open(state, try makeVault())
        await select(state, "note.md")
        let replies = ReplyRecorder()
        XCTAssertEqual(quit(state, replies), .terminateNow)
        XCTAssertTrue(replies.values.isEmpty)
    }

    func testAnUninstalledHostKeepsTodaysPromptSilently() async throws {
        let announcer = RecordingAnnouncer()
        let state = makeState(host: .uninstalled, announcer: announcer)
        await open(state, try makeVault())
        await select(state, "note.md")
        state.updateEditorText("# note\n\ndirty.\n")
        announcer.reset()
        let replies = ReplyRecorder()
        XCTAssertEqual(quit(state, replies), .terminateLater)
        XCTAssertEqual(state.pendingNavigation, .closeVault, "today's prompt")
        XCTAssertTrue(announcer.messages.isEmpty, "nothing posted")
    }

    // MARK: - More fixtures

    /// `two.md` dirty in a parked tab, `note.md` dirty in the active one.
    func dirtyTwo(_ state: AppState) async {
        await select(state, "two.md")
        state.updateEditorText("# two\n\ndirty two.\n")
        state.newTab()
        await select(state, "note.md")
        state.updateEditorText("# note\n\ndirty note.\n")
        XCTAssertEqual(state.workspace.dirtyParkedDocuments().count, 1)
    }

    /// Dirties `note.md`, quits kept, saves the layout as the
    /// `willTerminateNotification` observer does, and returns the next
    /// process's host.
    func keptQuit(_ state: AppState, host: HotExitStoreHost) async -> HotExitStoreHost {
        let replies = ReplyRecorder()
        XCTAssertEqual(quit(state, replies), .terminateLater)
        await state.hotExitRouteTask?.value
        XCTAssertEqual(replies.values, [true], "a kept quit")
        state.saveWorkspaceLayout()
        return newProcess(after: host)
    }

    /// Writes an entry through the core store directly, as another holder
    /// would (no hold): the "store edited by hand" case.
    @discardableResult
    func craftEntry(
        _ key: HotExitVaultKey, id: String, path: String, text: String, baseline: String?
    ) throws -> HotExitClaim {
        let store = try HotExitStore.open(deviceRoot: deviceRoot.path)
        let epoch = try store.currentEpoch(key: key)
        guard case .claimed(let claim) = try store.claimEntry(
            key: key, epoch: epoch, entryId: id, expected: nil,
            incarnation: try newHotExitIncarnation())
        else {
            XCTFail("the crafted claim lands")
            throw XCTSkip("no claim")
        }
        _ = try store.writeBuffer(
            key: key, claim: claim, generation: 1,
            entry: HotExitEntryWrite(path: path, text: text, baselineHash: baseline, baselineFileId: nil))
        return claim
    }

    /// Changes the note's bytes keeping the same file (and identity).
    func overwriteInPlace(_ vault: URL, _ path: String, _ text: String) throws {
        let handle = try FileHandle(forWritingTo: vault.appendingPathComponent(path))
        try handle.truncate(atOffset: 0)
        try handle.write(contentsOf: Data(text.utf8))
        try handle.close()
    }

    func replaceWithSameBytes(_ vault: URL, _ path: String) throws {
        let url = vault.appendingPathComponent(path)
        let bytes = try Data(contentsOf: url)
        try FileManager.default.removeItem(at: url)
        try bytes.write(to: url)
    }

    // MARK: - Restoring (§5)

    func testARestoredStaleTabsFirstSaveConflicts() async throws {
        let host = makeHost()
        let state = makeState(host: host)
        let vault = try makeVault()
        await open(state, vault)
        await select(state, "note.md")
        state.updateEditorText("# note\n\nmine.\n")
        let next = await keptQuit(state, host: host)
        try overwriteInPlace(vault, "note.md", "# note\n\ntheirs.\n")

        let announcer = RecordingAnnouncer()
        let reopened = makeState(host: next, announcer: announcer)
        await open(reopened, vault)
        XCTAssertEqual(reopened.loadedFilePath, "note.md")
        XCTAssertEqual(reopened.currentNoteText, "# note\n\nmine.\n")
        XCTAssertTrue(reopened.hasUnsavedChanges)
        reopened.announceVaultOpened(vaultTitle: "A", sidebarNotice: "")
        XCTAssertEqual(
            Array(announcer.messages.dropFirst()),
            [a11yRender(event: .unsavedNotesRestored(count: 1)).text,
             a11yRender(event: .restoredNoteChangedOnDisk(name: "note.md")).text])

        await reopened.saveCurrentNote()?.value
        XCTAssertNotNil(reopened.currentSaveConflict, "the first save conflicts")
        XCTAssertEqual(try onDisk(vault, "note.md"), "# note\n\ntheirs.\n")
    }

    func testTheActiveRestoredTabNeverReadsTheDisk() async throws {
        let host = makeHost()
        let state = makeState(host: host)
        let vault = try makeVault()
        await open(state, vault)
        await select(state, "note.md")
        state.updateEditorText("# note\n\nkept.\n")
        let next = await keptQuit(state, host: host)
        try FileManager.default.removeItem(at: vault.appendingPathComponent("note.md"))

        let reopened = makeState(host: next)
        await open(reopened, vault)
        XCTAssertNil(reopened.noteLoadError, "no load failure first")
        XCTAssertEqual(reopened.currentNoteText, "# note\n\nkept.\n")
        XCTAssertTrue(reopened.hasUnsavedChanges)
        // Gone: its first save creates the note.
        await reopened.saveCurrentNote()?.value
        XCTAssertNil(reopened.currentSaveConflict)
        XCTAssertEqual(try onDisk(vault, "note.md"), "# note\n\nkept.\n")
    }

    func testAReplacedFileIsNeverOverwritten() async throws {
        // A file replaced with the same bytes after the buffer was kept and
        // after the open classified it: the identity check refuses the save.
        let host = makeHost()
        let state = makeState(host: host)
        let vault = try makeVault()
        await open(state, vault)
        await select(state, "note.md")
        state.updateEditorText("# note\n\nmine.\n")
        let next = await keptQuit(state, host: host)

        let reopened = makeState(host: next)
        await open(reopened, vault)
        XCTAssertTrue(reopened.currentNoteBaselineIdentity.identityEnforced)
        try replaceWithSameBytes(vault, "note.md")
        await reopened.saveCurrentNote()?.value
        XCTAssertNotNil(reopened.currentSaveConflict, "a replaced file is refused")
        XCTAssertEqual(try onDisk(vault, "note.md"), "# note\n")

        // Keep Mine is the reader's explicit overwrite: it lands.
        await reopened.resolveSaveConflictKeepMine()?.value
        XCTAssertNil(reopened.currentSaveConflict)
        XCTAssertEqual(try onDisk(vault, "note.md"), "# note\n\nmine.\n")
        XCTAssertFalse(reopened.currentNoteBaselineIdentity.identityEnforced)
    }

    func testAFileReplacedWhileClosedRestoresAsANewNote() async throws {
        let host = makeHost()
        let state = makeState(host: host)
        let vault = try makeVault()
        await open(state, vault)
        await select(state, "note.md")
        state.updateEditorText("# note\n\nmine.\n")
        let next = await keptQuit(state, host: host)
        try replaceWithSameBytes(vault, "note.md")

        let announcer = RecordingAnnouncer()
        let reopened = makeState(host: next, announcer: announcer)
        await open(reopened, vault)
        XCTAssertEqual(reopened.currentNoteText, "# note\n\nmine.\n")
        XCTAssertNil(reopened.currentNoteContentHash, "a new note: its save is create-exclusive")
        reopened.announceVaultOpened(vaultTitle: "A", sidebarNotice: "")
        XCTAssertTrue(announcer.messages.contains(
            a11yRender(event: .restoredNoteExistsOnDisk(name: "note.md")).text))
        await reopened.saveCurrentNote()?.value
        XCTAssertNotNil(reopened.currentSaveConflict)
        XCTAssertEqual(try onDisk(vault, "note.md"), "# note\n")
    }

    /// Codex adversarial review, round 9: a restored note with no known
    /// baseline (a file now at its path) takes no property write until a
    /// save resolves the path, since a property write without a hash would
    /// overwrite the file the save refuses.
    func testARestoredNoteWithAFileNowAtItsPathTakesNoPropertyWrite() async throws {
        let host = makeHost()
        let state = makeState(host: host)
        let vault = try makeVault()
        await open(state, vault)
        await select(state, "note.md")
        state.updateEditorText("# note\n\nmine.\n")
        let next = await keptQuit(state, host: host)
        try replaceWithSameBytes(vault, "note.md")

        let reopened = makeState(host: next)
        await open(reopened, vault)
        XCTAssertNil(reopened.currentNoteContentHash)
        XCTAssertNotNil(reopened.activePropertyAuthoringDisabledReason)
        let task = reopened.applyPropertiesSource("title: Mine\n")
        await task?.value
        XCTAssertNil(task, "the write is refused")
        XCTAssertEqual(try onDisk(vault, "note.md"), "# note\n", "the file at the path is untouched")
        XCTAssertNil(reopened.setProperty(path: "note.md", key: "title", value: .text(value: "Mine")))
        XCTAssertNil(reopened.deleteProperty(path: "note.md", key: "title"))
        XCTAssertEqual(try onDisk(vault, "note.md"), "# note\n")
    }

    /// Codex adversarial review, round 10: a restored note whose kept
    /// baseline still guards its identity takes no property write either. A
    /// file replaced with the same bytes after the restore passes a hash
    /// check, and the refresh after a property write would drop the guard
    /// the next save relies on.
    func testARestoredNoteStillGuardingItsIdentityTakesNoPropertyWrite() async throws {
        let host = makeHost()
        let state = makeState(host: host)
        let vault = try makeVault()
        await open(state, vault)
        await select(state, "note.md")
        state.updateEditorText("# note\n\nmine.\n")
        let next = await keptQuit(state, host: host)

        let reopened = makeState(host: next)
        await open(reopened, vault)
        XCTAssertTrue(reopened.currentNoteBaselineIdentity.identityEnforced)
        try replaceWithSameBytes(vault, "note.md")
        XCTAssertNotNil(reopened.activePropertyAuthoringDisabledReason)
        let task = reopened.applyPropertiesSource("title: Mine\n")
        await task?.value
        XCTAssertNil(task, "the write is refused")
        XCTAssertEqual(try onDisk(vault, "note.md"), "# note\n", "the replacement is untouched")
        XCTAssertTrue(reopened.currentNoteBaselineIdentity.identityEnforced, "the guard stands")
        await reopened.saveCurrentNote()?.value
        XCTAssertNotNil(reopened.currentSaveConflict, "the save still refuses the replacement")
        XCTAssertEqual(try onDisk(vault, "note.md"), "# note\n")
    }

    func testEmptyingARestoredBufferStaysDirty() async throws {
        let host = makeHost()
        let state = makeState(host: host)
        let vault = try makeVault()
        await open(state, vault)
        await select(state, "note.md")
        state.updateEditorText("# note\n\nmine.\n")
        let next = await keptQuit(state, host: host)
        try overwriteInPlace(vault, "note.md", "# note\n\ntheirs.\n")

        let reopened = makeState(host: next)
        await open(reopened, vault)
        let key = try key(reopened)
        reopened.updateEditorText("")
        await settle(reopened)
        XCTAssertTrue(reopened.hasUnsavedChanges, "the baseline text is unknown")
        XCTAssertEqual(try storedEntries(key).map(\.text), [""])
    }

    func testARestoreSeedsSamePathSiblingsWithoutEntries() async throws {
        let host = makeHost()
        let state = makeState(host: host)
        let vault = try makeVault()
        await open(state, vault)
        await select(state, "note.md")
        let first = try XCTUnwrap(state.workspace.activeTab?.id)
        state.newTab()
        let second = try XCTUnwrap(state.workspace.activeTab?.id)
        XCTAssertNotEqual(first, second)
        state.saveWorkspaceLayout()
        let key = try key(state)
        let baseline = try XCTUnwrap(state.currentNoteContentHash)
        let next = newProcess(after: host)
        try craftEntry(
            key, id: first.raw.uuidString, path: "note.md", text: "# note\n\nkept.\n",
            baseline: baseline)

        let reopened = makeState(host: next)
        await open(reopened, vault)
        let siblings = buffers(reopened, path: "note.md")
        XCTAssertEqual(siblings.count, 2)
        XCTAssertTrue(siblings.allSatisfy { $0.body == "# note\n\nkept.\n" && $0.dirty })
        // The sibling's edit mirrors into the restored tab, never disk text
        // over it.
        reopened.activateTab(second)
        XCTAssertEqual(reopened.currentNoteText, "# note\n\nkept.\n")
        reopened.updateEditorText("# note\n\nkept. more.\n")
        XCTAssertEqual(
            reopened.workspace.document(for: first)?.text, "# note\n\nkept. more.\n")
    }

    func testDivergentPeersAreReconciledIntoARecoveredNote() async throws {
        let host = makeHost()
        let state = makeState(host: host)
        let vault = try makeVault()
        await open(state, vault)
        await select(state, "note.md")
        let first = try XCTUnwrap(state.workspace.activeTab?.id)
        state.newTab()
        let second = try XCTUnwrap(state.workspace.activeTab?.id)
        state.saveWorkspaceLayout()
        let key = try key(state)
        let baseline = try XCTUnwrap(state.currentNoteContentHash)
        let next = newProcess(after: host)
        try craftEntry(key, id: first.raw.uuidString, path: "note.md", text: "older\n", baseline: baseline)
        try craftEntry(key, id: second.raw.uuidString, path: "note.md", text: "newer\n", baseline: baseline)

        let reopened = makeState(host: next)
        await open(reopened, vault)
        XCTAssertEqual(buffers(reopened, path: "note.md").map(\.body), ["newer\n", "newer\n"])
        let recovered = buffers(reopened, path: "note (recovered).md")
        XCTAssertEqual(recovered.map(\.body), ["older\n"])
        // The recovered note keeps the older entry's id; the layout tab that
        // held it took a fresh one.
        let recoveredTab = try XCTUnwrap(reopened.hotExitMarkdownTabs.first {
            reopened.hotExitTabPath($0) == "note (recovered).md"
        })
        XCTAssertEqual(recoveredTab, first)
        XCTAssertFalse(reopened.hotExitMarkdownTabs.filter {
            reopened.hotExitTabPath($0) == "note.md"
        }.contains(first))
    }

    func testAnOrphanClaimIsInheritedByItsLayoutTab() async throws {
        let host = makeHost()
        let state = makeState(host: host)
        let vault = try makeVault()
        await open(state, vault)
        await select(state, "note.md")
        let tab = try XCTUnwrap(state.workspace.activeTab?.id)
        state.saveWorkspaceLayout()
        let key = try key(state)
        let next = newProcess(after: host)
        // A claim whose first write never landed.
        let store = try HotExitStore.open(deviceRoot: deviceRoot.path)
        let orphan = try newHotExitIncarnation()
        _ = try store.claimEntry(
            key: key, epoch: try store.currentEpoch(key: key), entryId: tab.raw.uuidString,
            expected: nil, incarnation: orphan)

        let reopened = makeState(host: next)
        await open(reopened, vault)
        XCTAssertFalse(reopened.hasUnsavedChanges, "a claim with no payload holds no text")
        reopened.updateEditorText("# note\n\nedited.\n")
        await settle(reopened)
        XCTAssertTrue(reopened.hotExitWriter?.isKeptForTests(tab) ?? false,
            "it claims expecting the orphan and is kept")
        XCTAssertEqual(try storedEntries(key).map(\.text), ["# note\n\nedited.\n"])
    }

    /// Codex adversarial review, round 6: opening another note into a clean
    /// tab that inherited an orphan claim retires the orphan first, so the
    /// next note's claim is never refused against it.
    func testOpeningAnotherNoteIntoATabWithAnOrphanClaimKeepsTheNewNote() async throws {
        let host = makeHost()
        let state = makeState(host: host)
        let vault = try makeVault()
        await open(state, vault)
        await select(state, "note.md")
        let tab = try XCTUnwrap(state.workspace.activeTab?.id)
        state.saveWorkspaceLayout()
        let key = try key(state)
        let next = newProcess(after: host)
        let store = try HotExitStore.open(deviceRoot: deviceRoot.path)
        _ = try store.claimEntry(
            key: key, epoch: try store.currentEpoch(key: key), entryId: tab.raw.uuidString,
            expected: nil, incarnation: try newHotExitIncarnation())

        let reopened = makeState(host: next)
        await open(reopened, vault)
        XCTAssertFalse(reopened.hasUnsavedChanges)
        await select(reopened, "two.md")
        reopened.updateEditorText("# two\n\nedited after the orphan.\n")
        await settle(reopened)
        let current = try XCTUnwrap(reopened.workspace.activeTab?.id)
        XCTAssertTrue(reopened.hotExitWriter?.isKeptForTests(current) ?? false, "the new note is kept")
        XCTAssertEqual(try storedEntries(key).map(\.text), ["# two\n\nedited after the orphan.\n"])

        reopened.saveWorkspaceLayout()
        let again = makeState(host: newProcess(after: next))
        await open(again, vault)
        XCTAssertEqual(
            buffers(again, path: "two.md").map(\.body), ["# two\n\nedited after the orphan.\n"])
    }

    /// Codex adversarial review, round 6: a kept edit reverted and quit in
    /// the same turn, before the writer's queued look at the buffers runs.
    /// The quit reconciles first, so the reverted text never comes back.
    func testARevertQuitInTheSameTurnRetiresTheKeptCopy() async throws {
        let host = makeHost()
        let state = makeState(host: host)
        let vault = try makeVault()
        await open(state, vault)
        await select(state, "note.md")
        let key = try key(state)
        state.updateEditorText("# note\n\nreverted later.\n")
        await settle(state)
        XCTAssertEqual(try storedEntries(key).count, 1)

        state.updateEditorText("# note\n")
        XCTAssertFalse(state.hasUnsavedChanges)
        let replies = ReplyRecorder()
        XCTAssertEqual(quit(state, replies), .terminateLater, "the retirement is settled first")
        await state.hotExitRouteTask?.value
        XCTAssertEqual(replies.values, [true])
        state.saveWorkspaceLayout()
        XCTAssertTrue(try storedEntries(key).isEmpty)
        let reopened = makeState(host: newProcess(after: host))
        await open(reopened, vault)
        XCTAssertFalse(reopened.hasUnsavedChanges, "the reverted text never comes back")
    }

    func testARevertClosedInTheSameTurnRetiresTheKeptCopy() async throws {
        let host = makeHost()
        let state = makeState(host: host)
        let vault = try makeVault()
        await open(state, vault)
        await select(state, "note.md")
        let key = try key(state)
        state.updateEditorText("# note\n\nreverted later.\n")
        await settle(state)
        XCTAssertEqual(try storedEntries(key).count, 1)

        state.updateEditorText("# note\n")
        state.closeVaultFromUserAction()
        await state.hotExitRouteTask?.value
        XCTAssertNil(state.currentSession, "a clean vault closes")
        XCTAssertTrue(try storedEntries(key).isEmpty, "the reverted text never comes back")
    }

    /// Codex adversarial review, round 7: a quit that starts clean, with
    /// sidebar work pending, waits on that fence while the editor stays
    /// writable. What is typed in the wait is kept before the reply.
    func testACleanQuitBehindTheSidebarFenceKeepsAnEditMadeWhileItWaits() async throws {
        let host = makeHost()
        let state = makeState(host: host)
        let vault = try makeVault()
        await open(state, vault)
        await select(state, "note.md")
        let key = try key(state)
        XCTAssertFalse(state.hasUnsavedChanges)
        let sidebar = Gate()
        defer { sidebar.open() }
        state.enqueueSidebarOrganizationWriteForTesting { _ in sidebar.hold() }
        let sidebarEntered = await entered(sidebar)
        XCTAssertTrue(sidebarEntered, "a sidebar write holds the fence")
        let replies = ReplyRecorder()
        XCTAssertEqual(quit(state, replies), .terminateLater, "the fence holds the quit")

        let gates = CallGates()
        host.beforeCallForTests = { gates.hook($0) }
        let claim = gates.arm(.claim)
        defer { claim.open() }
        state.updateEditorText("# note\n\ntyped while the quit waited.\n")
        let claimEntered = await entered(claim)
        XCTAssertTrue(claimEntered, "the edit's claim is issued")
        sidebar.open()
        try await Task.sleep(for: .milliseconds(500))
        XCTAssertEqual(replies.values, [], "no reply while the edit is not kept")

        claim.open()
        await state.hotExitRouteTask?.value
        XCTAssertEqual(replies.values, [true])
        XCTAssertEqual(try storedEntries(key).map(\.text), ["# note\n\ntyped while the quit waited.\n"])
        _ = vault
    }

    /// Codex adversarial review, round 7: an episode that ends before its
    /// claim reaches the store queues its retirement after that claim, so
    /// the claim never lands as an orphan that refuses the next episode.
    func testAnEpisodeEndedBeforeItsClaimRunsLeavesTheNextEditKept() async throws {
        let host = makeHost()
        let state = makeState(host: host)
        await open(state, try makeVault())
        await select(state, "note.md")
        let key = try key(state)
        let tab = try XCTUnwrap(state.workspace.activeTab?.id)
        // One turn: the edit's claim is issued, then the revert ends the
        // episode, before any task runs.
        state.updateEditorText("# note\n\nbriefly.\n")
        state.hotExitWriter?.reconcileNow()
        state.updateEditorText("# note\n")
        state.hotExitWriter?.reconcileNow()
        await settle(state)
        XCTAssertTrue(
            try HotExitStore.open(deviceRoot: deviceRoot.path).readBuffers(key: key).claims.isEmpty,
            "the ended episode's claim never lands after its retirement")

        state.updateEditorText("# note\n\nagain.\n")
        await settle(state)
        XCTAssertTrue(state.hotExitWriter?.isKeptForTests(tab) ?? false, "the next edit is kept")
        XCTAssertEqual(try storedEntries(key).map(\.text), ["# note\n\nagain.\n"])
    }

    /// Holds an async step (a property write's publish) until released.
    actor AsyncHold {
        private var entered = false
        private var released = false
        private var entranceWaiters: [CheckedContinuation<Void, Never>] = []
        private var releaseWaiter: CheckedContinuation<Void, Never>?

        func suspend() async {
            guard !released else { return }
            entered = true
            for waiter in entranceWaiters { waiter.resume() }
            entranceWaiters = []
            await withCheckedContinuation { releaseWaiter = $0 }
        }

        func waitUntilEntered() async {
            guard !entered else { return }
            await withCheckedContinuation { entranceWaiters.append($0) }
        }

        func release() {
            released = true
            releaseWaiter?.resume()
            releaseWaiter = nil
        }
    }

    /// Codex adversarial review, round 8: a property write started while the
    /// quit settles has no recovery payload, so the quit never answers yes
    /// while it runs.
    func testAQuitNeverAnswersYesWhileAPropertyWriteRuns() async throws {
        let host = makeHost()
        let state = makeState(host: host)
        let vault = try makeVault()
        try "---\ntitle: Props\n---\n# props\n".write(
            to: vault.appendingPathComponent("props.md"), atomically: true, encoding: .utf8)
        await open(state, vault)
        await select(state, "two.md")
        state.updateEditorText("# two\n\ndirty and parked.\n")
        state.newTab()
        await select(state, "props.md")
        await settle(state)

        let gates = CallGates()
        host.beforeCallForTests = { gates.hook($0) }
        let epoch = gates.arm(.epoch)
        defer { epoch.open() }
        let replies = ReplyRecorder()
        XCTAssertEqual(quit(state, replies), .terminateLater)
        let epochEntered = await entered(epoch)
        XCTAssertTrue(epochEntered, "the quit's settle reads the epoch")

        let publish = AsyncHold()
        state.basesPostWritePublishGate = { await publish.suspend() }
        let edit = try XCTUnwrap(state.deleteProperty(path: "props.md", key: "title"))
        await publish.waitUntilEntered()
        XCTAssertTrue(state.isEditingProperty)
        epoch.open()
        try await Task.sleep(for: .milliseconds(500))
        XCTAssertEqual(replies.values, [], "no yes while the property write runs")

        await publish.release()
        await edit.value
        state.basesPostWritePublishGate = nil
        await state.hotExitRouteTask?.value
        XCTAssertEqual(replies.values, [true], "the quit goes ahead once the write is done")
        XCTAssertFalse(try onDisk(vault, "props.md").contains("title:"))
    }

    // MARK: - Discards (§3, §4)

    func testATabsOwnDiscardRetiresItsEntry() async throws {
        let host = makeHost()
        let state = makeState(host: host)
        await open(state, try makeVault())
        await select(state, "note.md")
        let key = try key(state)
        state.updateEditorText("# note\n\ndirty.\n")
        await settle(state)
        XCTAssertEqual(try storedEntries(key).count, 1)
        state.requestCloseTab()
        XCTAssertNotNil(state.pendingTabClose)
        state.resolveTabCloseDiscard()
        await settle(state)
        XCTAssertTrue(try storedEntries(key).isEmpty)
    }

    /// Codex adversarial review, round 2: Save on the tab-close prompt saves
    /// and closes the tab in one turn, before the writer sees the clean
    /// buffer. The saved note leaves nothing behind to come back unsaved,
    /// even with a later revision still on its way to the store.
    func testSavingThroughTheTabClosePromptRetiresTheEntry() async throws {
        let host = makeHost()
        let state = makeState(host: host)
        let vault = try makeVault()
        await open(state, vault)
        await select(state, "note.md")
        let key = try key(state)
        state.updateEditorText("# note\n\none.\n")
        await settle(state)
        XCTAssertEqual(try storedEntries(key).map(\.text), ["# note\n\none.\n"])

        let gates = CallGates()
        host.beforeCallForTests = { gates.hook($0) }
        let write = gates.arm(.write)
        defer { write.open() }
        state.updateEditorText("# note\n\ntwo.\n")
        let writeEntered = await entered(write)
        XCTAssertTrue(writeEntered, "the next revision is on its way")
        state.requestCloseTab()
        XCTAssertNotNil(state.pendingTabClose)
        state.resolveTabCloseSave()
        await state.saveTask?.value
        XCTAssertEqual(try onDisk(vault, "note.md"), "# note\n\ntwo.\n")
        XCTAssertFalse(state.workspace.model.allTabs.contains { tab in
            if case .markdown(let path) = tab.item { return path == "note.md" }
            return false
        }, "the save closed the tab")

        write.open()
        await settle(state)
        XCTAssertTrue(try storedEntries(key).isEmpty, "nothing comes back unsaved")
    }

    /// Codex adversarial review, round 3: a rename builds the parked tab a
    /// new document whose change counter starts again, so two renames in a
    /// row can look unchanged to the writer. The kept entry follows each.
    func testAParkedDirtyTabRenamedTwiceIsKeptAtItsNewestPath() async throws {
        let host = makeHost()
        let state = makeState(host: host)
        await open(state, try makeVault())
        await select(state, "two.md")
        let key = try key(state)
        state.updateEditorText("# two\n\nparked and dirty.\n")
        state.newTab()
        await select(state, "note.md")
        await settle(state)
        XCTAssertEqual(try storedEntries(key).map(\.path), ["two.md"])

        await state.renameEntry(path: "two.md", isDirectory: false, to: "three.md")?.value
        await state.nativeDocumentRetargetTask?.value
        await settle(state)
        XCTAssertEqual(try storedEntries(key).map(\.path), ["three.md"])
        await state.renameEntry(path: "three.md", isDirectory: false, to: "four.md")?.value
        await state.nativeDocumentRetargetTask?.value
        await settle(state)
        XCTAssertEqual(try storedEntries(key).map(\.path), ["four.md"], "kept at its newest path")
    }

    func testAnUngatedDropIsAbandonedNotRetired() async throws {
        // A canvas opened into a dirty note's tab drops the buffer without
        // the dirty gate (#1932): the store keeps it, and the next open
        // restores it as a new tab.
        let host = makeHost()
        let state = makeState(host: host)
        let vault = try makeVault()
        try #"{"nodes":[],"edges":[]}"#.write(
            to: vault.appendingPathComponent("board.canvas"), atomically: true, encoding: .utf8)
        await open(state, vault)
        await select(state, "note.md")
        let key = try key(state)
        state.updateEditorText("# note\n\ndropped.\n")
        await settle(state)
        state.openCanvasFile("board.canvas", target: .currentTab)
        await settle(state)
        XCTAssertEqual(try storedEntries(key).map(\.text), ["# note\n\ndropped.\n"])
        state.saveWorkspaceLayout()

        let reopened = makeState(host: newProcess(after: host))
        await open(reopened, vault)
        XCTAssertEqual(buffers(reopened, path: "note.md").map(\.body), ["# note\n\ndropped.\n"])
    }

    func testCloseVaultStillAsksAndDiscardAllFencesTheStore() async throws {
        let host = makeHost()
        let state = makeState(host: host)
        let vault = try makeVault()
        await open(state, vault)
        await dirtyTwo(state)
        let key = try key(state)
        await settle(state)
        XCTAssertEqual(try storedEntries(key).count, 2)

        state.closeVaultFromUserAction()
        XCTAssertEqual(state.pendingVaultClose, 2, "Close Vault still asks")
        state.resolveVaultCloseDiscardAll()
        await state.hotExitRouteTask?.value
        XCTAssertNil(state.currentSession, "the vault closed")
        XCTAssertTrue(try storedEntries(key).isEmpty)

        let reopened = makeState(host: newProcess(after: host))
        await open(reopened, vault)
        XCTAssertFalse(reopened.hasUnsavedChanges)
        XCTAssertTrue(reopened.workspace.dirtyParkedDocuments().isEmpty)
    }

    func testAFailedDiscardKeepsEveryBufferAsTheReaderLeftIt() async throws {
        let host = makeHost()
        let state = makeState(host: host)
        await open(state, try makeVault())
        await dirtyTwo(state)
        await settle(state)
        host.beforeCallForTests = { $0.kind == .discard ? .io : nil }
        state.closeVaultFromUserAction()
        state.resolveVaultCloseDiscardAll()
        await state.hotExitRouteTask?.value

        let prompt = try XCTUnwrap(state.pendingHotExitDiscard, "the discard-pending prompt")
        let choices = hotExitDiscardPendingChoices()
        XCTAssertEqual(prompt.message, a11yRender(event: .unsavedNotesNotDiscarded).text)
        XCTAssertEqual(
            [prompt.tryAgain, prompt.closeKeepingCopies, prompt.cancel],
            [choices.tryAgain, choices.closeKeepingCopies, choices.cancel])
        state.resolveHotExitDiscardPending(.cancel)
        XCTAssertNotNil(state.currentSession, "Cancel keeps the vault open")
        XCTAssertEqual(state.currentNoteText, "# note\n\ndirty note.\n")
        XCTAssertTrue(state.hasUnsavedChanges, "nothing reverted before a commit")
        XCTAssertEqual(state.workspace.dirtyParkedDocuments().count, 1)
    }

    func testTryAgainClosesOnceTheDiscardCommits() async throws {
        let host = makeHost()
        let state = makeState(host: host)
        let vault = try makeVault()
        await open(state, vault)
        await dirtyTwo(state)
        let key = try key(state)
        await settle(state)
        host.beforeCallForTests = { $0.kind == .discard ? .io : nil }
        state.closeVaultFromUserAction()
        state.resolveVaultCloseDiscardAll()
        await state.hotExitRouteTask?.value
        XCTAssertNotNil(state.pendingHotExitDiscard)

        host.beforeCallForTests = nil
        state.resolveHotExitDiscardPending(.tryAgain)
        await state.hotExitRouteTask?.value
        XCTAssertNil(state.currentSession, "closed once the discard committed")
        XCTAssertTrue(try storedEntries(key).isEmpty)
    }

    /// Codex adversarial review, round 2: Try again dismisses the alert and
    /// waits on the store, and a direct open can replace the vault meanwhile.
    /// The old vault's retry then decides nothing for the new one.
    func testATryAgainOutlivedByAnotherVaultNeverActsOnIt() async throws {
        let host = makeHost()
        host.retirementRetryDelay = 60
        let state = makeState(host: host)
        let vaultA = try makeVault("A")
        let vaultB = try makeVault("B")
        await open(state, vaultA)
        await select(state, "note.md")
        state.updateEditorText("# note\n\ndirty.\n")
        await settle(state)
        host.beforeCallForTests = { $0.kind == .retire ? .io : nil }
        await state.saveCurrentNote()?.value
        state.closeVaultFromUserAction()
        await state.hotExitRouteTask?.value
        XCTAssertNotNil(state.pendingHotExitDiscard, "the retirement is unconfirmed")

        let gates = CallGates()
        host.beforeCallForTests = { gates.hook($0) }
        let retire = gates.arm(.retire)
        defer { retire.open() }
        state.resolveHotExitDiscardPending(.tryAgain)
        let retryTask = state.hotExitRouteTask
        let retireEntered = await entered(retire)
        XCTAssertTrue(retireEntered, "Try again waits on the store")

        state.openVault(at: vaultB)
        await state.scanTask?.value
        XCTAssertEqual(state.currentVaultURL?.lastPathComponent, "B")
        retire.open()
        await retryTask?.value
        await state.hotExitRouteTask?.value
        XCTAssertEqual(state.currentVaultURL?.lastPathComponent, "B", "B stays open")
        XCTAssertNil(state.pendingHotExitDiscard, "A's alert never shows over B")
    }

    func testCloseAndKeepTheCopiesAfterAFailedDiscard() async throws {
        let host = makeHost()
        let state = makeState(host: host)
        let vault = try makeVault()
        await open(state, vault)
        await dirtyTwo(state)
        await settle(state)
        state.saveWorkspaceLayout()
        host.beforeCallForTests = { $0.kind == .discard || $0.kind == .retire ? .io : nil }
        state.closeVaultFromUserAction()
        state.resolveVaultCloseDiscardAll()
        await state.hotExitRouteTask?.value
        state.resolveHotExitDiscardPending(.closeKeepingCopies)
        XCTAssertNil(state.currentSession, "the vault closed")

        let reopened = makeState(host: newProcess(after: host))
        await open(reopened, vault)
        XCTAssertEqual(
            Set(reopened.hotExitMarkdownTabs.compactMap { reopened.hotExitBuffer(for: $0)?.body }),
            ["# note\n\ndirty note.\n", "# two\n\ndirty two.\n"],
            "the copies come back unsaved")
    }

    func testDiscardAllOnTheQuitPromptFencesTheStoreBeforeTheReply() async throws {
        let host = makeHost()
        let state = makeState(host: host)
        let vault = try makeVault()
        await open(state, vault)
        await dirtyTwo(state)
        let key = try key(state)
        await settle(state)
        // A write that fails makes the quit fall back to today's prompt.
        state.updateEditorText("# note\n\ndirty note, later.\n")
        host.beforeCallForTests = { $0.kind == .write ? .io : nil }
        let replies = ReplyRecorder()
        XCTAssertEqual(quit(state, replies), .terminateLater)
        await state.hotExitRouteTask?.value
        XCTAssertEqual(state.pendingVaultClose, 2, "today's prompt")
        XCTAssertTrue(replies.values.isEmpty)
        host.beforeCallForTests = nil

        state.resolveVaultCloseDiscardAll()
        await state.hotExitRouteTask?.value
        XCTAssertEqual(replies.values, [true])
        XCTAssertTrue(try storedEntries(key).isEmpty, "the discard committed before the reply")
        _ = vault
    }

    /// Arms gates for store calls from the main actor; the hook reads them on
    /// the store's worker.
    final class CallGates: @unchecked Sendable {
        private let lock = NSLock()
        private var armed: [HotExitCallKind: Gate] = [:]
        func arm(_ kind: HotExitCallKind) -> Gate {
            let gate = Gate()
            lock.withLock { armed[kind] = gate }
            return gate
        }
        func hook(_ call: HotExitStoreCall) -> HotExitFault? {
            let gate = lock.withLock { armed.removeValue(forKey: call.kind) }
            gate?.hold()
            return nil
        }
    }

    /// Waits for a gate without blocking the main actor the held work needs.
    func entered(_ gate: Gate, within seconds: Double = 5) async -> Bool {
        let deadline = Date().addingTimeInterval(seconds)
        while Date() < deadline {
            if gate.waitEntered(0) { return true }
            try? await Task.sleep(for: .milliseconds(10))
        }
        return false
    }

    /// Codex adversarial review, round 2: sidebar or Trash work admitted
    /// during the quit's settle adds a wait after the last re-check, and the
    /// editor stays writable through it. An edit made in that wait is kept
    /// before the quit is answered, never left behind.
    func testAnEditDuringTheQuitsLastSidebarWaitIsKeptBeforeTheReply() async throws {
        let host = makeHost()
        let state = makeState(host: host)
        let vault = try makeVault()
        await open(state, vault)
        await select(state, "note.md")
        let key = try key(state)
        state.updateEditorText("# note\n\none.\n")
        await settle(state)

        let gates = CallGates()
        host.beforeCallForTests = { gates.hook($0) }
        let epoch = gates.arm(.epoch)
        let sidebar = Gate()
        defer { sidebar.open() }
        let replies = ReplyRecorder()
        XCTAssertEqual(quit(state, replies), .terminateLater)
        let epochEntered = await entered(epoch)
        XCTAssertTrue(epochEntered, "the quit's settle reads the epoch")
        state.enqueueSidebarOrganizationWriteForTesting { _ in sidebar.hold() }
        let sidebarEntered = await entered(sidebar)
        XCTAssertTrue(sidebarEntered, "a sidebar write holds the fence")
        epoch.open()
        // The settle decides within a frame or two; the fence then waits.
        try await Task.sleep(for: .milliseconds(500))
        XCTAssertEqual(replies.values, [], "the fence holds the reply")

        let write = gates.arm(.write)
        state.updateEditorText("# note\n\ntwo.\n")
        let writeEntered = await entered(write)
        XCTAssertTrue(writeEntered, "the edit's write is issued")
        sidebar.open()
        try await Task.sleep(for: .milliseconds(500))
        XCTAssertEqual(replies.values, [], "no reply while the edit is not kept")

        write.open()
        await state.hotExitRouteTask?.value
        XCTAssertEqual(replies.values, [true])
        XCTAssertEqual(try storedEntries(key).map(\.text), ["# note\n\ntwo.\n"])
        _ = vault
    }

    /// Codex adversarial review, round 3: Close Vault's Discard All waits on
    /// the store, and a second Close Vault is asked and cancelled meanwhile.
    /// The first discard's completion then closes nothing.
    func testACancelledNewerCloseStopsTheOlderDiscardAll() async throws {
        let host = makeHost()
        let state = makeState(host: host)
        await open(state, try makeVault())
        await dirtyTwo(state)
        await settle(state)
        let gates = CallGates()
        host.beforeCallForTests = { gates.hook($0) }
        let discard = gates.arm(.discard)
        defer { discard.open() }
        state.closeVaultFromUserAction()
        XCTAssertEqual(state.pendingVaultClose, 2)
        state.resolveVaultCloseDiscardAll()
        let discardTask = state.hotExitRouteTask
        let discardEntered = await entered(discard)
        XCTAssertTrue(discardEntered, "the discard waits on the store")

        state.closeVaultFromUserAction()
        XCTAssertEqual(state.pendingVaultClose, 2, "the newer close asks")
        state.resolveVaultCloseCancel()
        discard.open()
        await discardTask?.value
        await state.hotExitRouteTask?.value
        XCTAssertNotNil(state.currentSession, "the vault stays open")
        XCTAssertEqual(state.currentNoteText, "# note\n\ndirty note.\n")
        XCTAssertEqual(state.workspace.dirtyParkedDocuments().count, 1)
    }

    /// Codex adversarial review, round 2 (second pass): the fallback
    /// prompt's Discard waits on the store, and a newer request answers the
    /// quit meanwhile. The discard's completion then closes nothing.
    func testASupersededQuitsHeldDiscardNeverClosesTheVault() async throws {
        let host = makeHost()
        let state = makeState(host: host)
        await open(state, try makeVault())
        await select(state, "note.md")
        host.beforeCallForTests = { $0.kind == .write || $0.kind == .claim ? .io : nil }
        state.updateEditorText("# note\n\nunkept.\n")
        let replies = ReplyRecorder()
        XCTAssertEqual(quit(state, replies), .terminateLater)
        await state.hotExitRouteTask?.value
        XCTAssertEqual(state.pendingNavigation, .closeVault, "the fallback prompt")

        let gates = CallGates()
        host.beforeCallForTests = { gates.hook($0) }
        let discard = gates.arm(.discard)
        defer { discard.open() }
        state.resolvePendingNavigationDiscard()
        let discardTask = state.hotExitRouteTask
        let discardEntered = await entered(discard)
        XCTAssertTrue(discardEntered, "the discard waits on the store")

        state.selectedFilePath = "two.md"
        XCTAssertEqual(state.pendingNavigation, .selectFile("two.md"))
        XCTAssertEqual(replies.values, [false], "the newer request answers the quit")
        state.resolvePendingNavigationCancel()
        discard.open()
        await discardTask?.value
        await state.hotExitRouteTask?.value
        XCTAssertNotNil(state.currentSession, "the vault stays open")
        XCTAssertEqual(state.currentNoteText, "# note\n\nunkept.\n")
        XCTAssertEqual(replies.values, [false])
    }

    /// Codex adversarial review, round 2 (second pass): Close and keep the
    /// copies on a quit waits on the sidebar fence before it answers, and the
    /// editor stays writable meanwhile. Text typed in that wait goes back to
    /// the quit's prompt (owner call 3), never closed over.
    func testAKeepCopiesQuitAsksAgainAboutAnEditDuringItsLastWait() async throws {
        let host = makeHost()
        let state = makeState(host: host)
        await open(state, try makeVault())
        await select(state, "note.md")
        host.beforeCallForTests = { $0.kind == .write || $0.kind == .claim ? .io : nil }
        state.updateEditorText("# note\n\nunkept.\n")
        let replies = ReplyRecorder()
        XCTAssertEqual(quit(state, replies), .terminateLater)
        await state.hotExitRouteTask?.value
        XCTAssertEqual(state.pendingNavigation, .closeVault, "the fallback prompt")
        host.beforeCallForTests = { $0.kind == .discard ? .io : nil }
        state.resolvePendingNavigationDiscard()
        await state.hotExitRouteTask?.value
        XCTAssertNotNil(state.pendingHotExitDiscard, "the discard is unconfirmed")

        let sidebar = Gate()
        defer { sidebar.open() }
        state.enqueueSidebarOrganizationWriteForTesting { _ in sidebar.hold() }
        let sidebarEntered = await entered(sidebar)
        XCTAssertTrue(sidebarEntered, "a sidebar write holds the fence")
        state.resolveHotExitDiscardPending(.closeKeepingCopies)
        XCTAssertEqual(replies.values, [], "the fence holds the reply")
        state.updateEditorText("# note\n\nunkept, then more.\n")
        sidebar.open()
        await state.terminationFenceTaskForTesting?.value
        await state.hotExitRouteTask?.value
        XCTAssertEqual(replies.values, [], "no yes over text typed after the choice")
        XCTAssertEqual(state.pendingNavigation, .closeVault, "the quit's prompt asks again")
        state.resolvePendingNavigationCancel()
        XCTAssertEqual(replies.values, [false])
        XCTAssertNotNil(state.currentSession)
    }

    func testAQuitThatCannotKeepFallsBackWithItsReason() async throws {
        for fault in [HotExitFault.io, .refused(.vaultFull)] {
            let host = makeHost()
            let announcer = RecordingAnnouncer()
            let state = makeState(host: host, announcer: announcer)
            await open(state, try makeVault("V\(UUID().uuidString.prefix(6))"))
            await select(state, "note.md")
            host.beforeCallForTests = { $0.kind == .write || $0.kind == .claim ? fault : nil }
            state.updateEditorText("# note\n\nunkept.\n")
            announcer.reset()
            let replies = ReplyRecorder()
            XCTAssertEqual(quit(state, replies), .terminateLater)
            await state.hotExitRouteTask?.value
            XCTAssertEqual(state.pendingNavigation, .closeVault, "today's prompt")
            let reason: String = {
                if case .refused(let refusal) = fault { return hotExitRefusalReason(refusal: refusal) }
                return hotExitReasonStoreUnwritable()
            }()
            XCTAssertEqual(
                announcer.messages, [a11yRender(event: .unsavedNotesNotKept(reason: reason)).text])
            state.resolvePendingNavigationCancel()
            XCTAssertEqual(replies.values, [false])
            XCTAssertTrue(state.hasUnsavedChanges)
        }
    }

    /// The VoiceOver field pass (2026-11-14): VoiceOver reads the prompt the
    /// moment it opens, over the not-kept sentence posted just before it, so
    /// the prompt says it too, under its question, and only while it shows.
    func testAQuitThatCannotKeepShowsItsReasonInThePrompt() async throws {
        let host = makeHost()
        let state = makeState(host: host)
        await open(state, try makeVault())
        await select(state, "note.md")
        host.beforeCallForTests = { $0.kind == .write || $0.kind == .claim ? .io : nil }
        state.updateEditorText("# note\n\nunkept.\n")
        let replies = ReplyRecorder()
        XCTAssertEqual(quit(state, replies), .terminateLater)
        await state.hotExitRouteTask?.value
        XCTAssertEqual(state.pendingNavigation, .closeVault)
        XCTAssertEqual(
            state.hotExitPromptNotice,
            a11yRender(event: .unsavedNotesNotKept(reason: hotExitReasonStoreUnwritable())).text)
        state.resolvePendingNavigationCancel()
        XCTAssertNil(state.hotExitPromptNotice, "the next prompt never carries it")
    }

    func testAnInertVaultsQuitPromptShowsCoresReason() async throws {
        let host = makeHost()
        let state = makeState(host: host)
        let vault = try makeVault()
        await open(state, vault)
        await select(state, "note.md")
        state.updateEditorText("# note\n\nkept.\n")
        let next = await keptQuit(state, host: host)
        let vaults = deviceRoot.appendingPathComponent("hot-exit/v1/vaults")
        let dirs = try FileManager.default.contentsOfDirectory(atPath: vaults.path)
        try FileManager.default.removeItem(
            at: vaults.appendingPathComponent(dirs[0]).appendingPathComponent("epoch"))
        let reopened = makeState(host: next)
        await open(reopened, vault)
        let reason = try XCTUnwrap(reopened.hotExitWriter?.open.reason)
        await select(reopened, "note.md")
        reopened.updateEditorText("# note\n\nnew.\n")
        let replies = ReplyRecorder()
        _ = quit(reopened, replies)
        XCTAssertEqual(reopened.pendingNavigation, .closeVault)
        XCTAssertEqual(
            reopened.hotExitPromptNotice,
            a11yRender(event: .unsavedNotesNotKept(reason: reason)).text)
        reopened.resolvePendingNavigationCancel()
        XCTAssertNil(reopened.hotExitPromptNotice)
    }

    func testAStoreThatCannotOpenKeepsThePromptWithItsReason() async throws {
        let announcer = RecordingAnnouncer()
        let host = HotExitStoreHost.install(root: deviceRoot, syncParent: { _ in false })
        XCTAssertFalse(host.hasStore)
        XCTAssertEqual(host.unavailableReason, hotExitReasonStoreUnwritable())
        let state = makeState(host: host, announcer: announcer)
        await open(state, try makeVault())
        await select(state, "note.md")
        state.updateEditorText("# note\n\ndirty.\n")
        announcer.reset()
        let replies = ReplyRecorder()
        XCTAssertEqual(quit(state, replies), .terminateLater)
        XCTAssertEqual(state.pendingNavigation, .closeVault)
        XCTAssertEqual(
            announcer.messages,
            [a11yRender(event: .unsavedNotesNotKept(reason: hotExitReasonStoreUnwritable())).text])
    }

    func testDraftsThatAreNotBuffersKeepThePrompt() async throws {
        let host = makeHost()
        let announcer = RecordingAnnouncer()
        let state = makeState(host: host, announcer: announcer)
        await open(state, try makeVault())
        await select(state, "note.md")
        state.updateEditorText("# note\n\ndirty.\n")
        state.propertiesSourceDraftPath = "note.md"
        state.propertiesSourceDraft = "owner: draft\n"
        XCTAssertFalse(state.uncommittedNoteDraftPaths().isEmpty)
        announcer.reset()
        let replies = ReplyRecorder()
        XCTAssertEqual(quit(state, replies), .terminateLater)
        await state.hotExitRouteTask?.value
        XCTAssertNotNil(state.pendingVaultClose, "today's prompt")
        XCTAssertEqual(
            announcer.messages,
            [a11yRender(event: .unsavedNotesNotKept(reason: hotExitReasonDraftsNotKept())).text])
        XCTAssertTrue(state.hotExitWriter.map {
            $0.isKeptForTests(state.workspace.activeTab!.id) } ?? false,
            "the tab buffer is still kept")
    }

    // MARK: - Switching (§4)

    func testAKeptSwitchOpensTheTargetWithoutAPrompt() async throws {
        let host = makeHost()
        let state = makeState(host: host)
        let vaultA = try makeVault("A")
        let vaultB = try makeVault("B")
        await open(state, vaultA)
        await dirtyTwo(state)
        state.switchToRecent(RecentVault(url: vaultB))
        await state.hotExitRouteTask?.value
        XCTAssertEqual(state.currentVaultURL?.standardizedFileURL, vaultB.standardizedFileURL)
        XCTAssertNil(state.pendingVaultClose)
        XCTAssertNil(state.pendingNavigation)
        await state.scanTask?.value

        state.switchToRecent(RecentVault(url: vaultA))
        await state.scanTask?.value
        await state.noteLoadTask?.value
        XCTAssertEqual(state.currentVaultURL?.standardizedFileURL, vaultA.standardizedFileURL)
        XCTAssertEqual(buffers(state, path: "note.md").map(\.body), ["# note\n\ndirty note.\n"])
        XCTAssertEqual(buffers(state, path: "two.md").map(\.body), ["# two\n\ndirty two.\n"])
    }

    // MARK: - The re-check (§4)

    func testTypingDuringASettleIsAskedAboutNotDropped() async throws {
        let host = makeHost()
        let state = makeState(host: host)
        await open(state, try makeVault())
        await select(state, "note.md")
        state.updateEditorText("# note\n\nfirst.\n")
        await settle(state)
        // An edit lands inside the quit's settle (on its epoch read).
        host.beforeCallForTests = { call in
            if call.kind == .epoch {
                DispatchQueue.main.async {
                    MainActor.assumeIsolated {
                        state.updateEditorText("# note\n\nfirst. typed during the settle.\n")
                    }
                }
                Thread.sleep(forTimeInterval: 0.1)
            }
            return nil
        }
        let replies = ReplyRecorder()
        XCTAssertEqual(quit(state, replies), .terminateLater)
        await state.hotExitRouteTask?.value
        XCTAssertTrue(replies.values.isEmpty, "the quit does not close over new text")
        XCTAssertEqual(state.pendingNavigation, .closeVault, "it asks")
        XCTAssertEqual(state.currentNoteText, "# note\n\nfirst. typed during the settle.\n")
    }

    func testEditingAPropertyDraftDuringADiscardAsksAgain() async throws {
        let host = makeHost()
        let state = makeState(host: host)
        await open(state, try makeVault())
        await dirtyTwo(state)
        await settle(state)
        state.propertiesSourceDraftPath = "note.md"
        state.propertiesSourceDraft = "owner: before\n"
        state.closeVaultFromUserAction()
        XCTAssertNotNil(state.pendingVaultClose)
        host.beforeCallForTests = { call in
            if call.kind == .discard {
                DispatchQueue.main.async {
                    MainActor.assumeIsolated { state.propertiesSourceDraft = "owner: during\n" }
                }
                Thread.sleep(forTimeInterval: 0.1)
            }
            return nil
        }
        state.resolveVaultCloseDiscardAll()
        await state.hotExitRouteTask?.value
        XCTAssertNotNil(state.currentSession, "the vault stays open")
        XCTAssertNotNil(state.pendingVaultClose, "Close Vault's prompt opens again")
        XCTAssertEqual(state.propertiesSourceDraft, "owner: during\n")
    }

    // MARK: - Reopening (§2)

    func testAReopenWithEarlierWorkPendingHasNoHotExit() async throws {
        let host = makeHost()
        host.retirementRetryDelay = 60
        let announcer = RecordingAnnouncer()
        let state = makeState(host: host, announcer: announcer)
        let vault = try makeVault()
        await open(state, vault)
        await select(state, "note.md")
        state.updateEditorText("# note\n\ndirty.\n")
        await settle(state)
        // A retirement the store cannot confirm is left by Close and keep.
        host.beforeCallForTests = { $0.kind == .retire ? .io : nil }
        await state.saveCurrentNote()?.value
        state.closeVaultFromUserAction()
        await state.hotExitRouteTask?.value
        state.resolveHotExitDiscardPending(.closeKeepingCopies)
        XCTAssertNil(state.currentSession)

        await open(state, vault)
        XCTAssertEqual(state.hotExitWriter?.isInert, true)
        XCTAssertEqual(state.hotExitWriter?.open.reason, hotExitReasonEarlierWorkPending())
        await select(state, "note.md")
        state.updateEditorText("# note\n\nagain.\n")
        announcer.reset()
        let replies = ReplyRecorder()
        XCTAssertEqual(quit(state, replies), .terminateLater)
        XCTAssertEqual(state.pendingNavigation, .closeVault)
        XCTAssertEqual(
            announcer.messages,
            [a11yRender(event: .unsavedNotesNotKept(reason: hotExitReasonEarlierWorkPending())).text])
    }

    func testInstallationSyncsTheDeviceRootIntoItsParent() throws {
        var synced: [URL] = []
        let root = tempDir.appendingPathComponent("fresh-root", isDirectory: true)
        let host = HotExitStoreHost.install(root: root, syncParent: { synced.append($0); return true })
        XCTAssertTrue(host.hasStore)
        XCTAssertEqual(synced, [root])
        XCTAssertTrue(FileManager.default.fileExists(atPath: root.path))
        // Again, on a root this call did not make.
        _ = HotExitStoreHost.install(root: root, syncParent: { synced.append($0); return true })
        XCTAssertEqual(synced, [root, root])
        XCTAssertTrue(HotExitStoreHost.fullSyncParent(root), "the real sync on this volume")
    }

    // MARK: - Ordering and ownership (W§0)

    /// Lets the main actor run until a held store call enters its hook.
    func waitEntered(_ gate: Gate, timeout: TimeInterval = 5) async -> Bool {
        let deadline = Date().addingTimeInterval(timeout)
        while Date() < deadline {
            if gate.waitEntered(0.005) { return true }
            await HotExitWriter.frame()
        }
        return false
    }

    func testAnErroredClaimIsRetriedWithItsOwnIncarnation() async throws {
        let host = makeHost()
        let state = makeState(host: host)
        await open(state, try makeVault())
        await select(state, "note.md")
        let key = try key(state)
        let lock = NSLock()
        var claims: [String] = []
        var failedOnce = false
        host.beforeCallForTests = { call in
            if call.kind == .claim { lock.withLock { claims.append(call.incarnations[0]) } }
            return nil
        }
        // The first claim reaches the disk, then errors.
        host.afterCallForTests = { call in
            lock.withLock {
                guard call.kind == .claim, !failedOnce else { return nil }
                failedOnce = true
                return .io
            }
        }
        state.updateEditorText("# note\n\none.\n")
        await settle(state)
        XCTAssertFalse(state.hotExitWriter?.isKeptForTests(state.workspace.activeTab!.id) ?? true)
        state.updateEditorText("# note\n\none. two.\n")
        await settle(state)
        XCTAssertEqual(lock.withLock { claims.count }, 2)
        XCTAssertEqual(lock.withLock { Set(claims).count }, 1, "the retry carries its own incarnation")
        XCTAssertEqual(try storedEntries(key).map(\.text), ["# note\n\none. two.\n"])
    }

    func testTwoDirtyPeersAreTwoEntriesAndAPeersCloseKeepsTheOther() async throws {
        let host = makeHost()
        let state = makeState(host: host)
        let vault = try makeVault()
        await open(state, vault)
        await select(state, "note.md")
        let key = try key(state)
        state.updateEditorText("# note\n\npeers.\n")
        state.newTab()
        await settle(state)
        XCTAssertEqual(try storedEntries(key).map(\.text), ["# note\n\npeers.\n", "# note\n\npeers.\n"])
        // The sibling holds the same loaded buffer, so its close asks
        // nothing and retires only its own entry.
        state.requestCloseTab()
        XCTAssertNil(state.pendingTabClose)
        await settle(state)
        XCTAssertEqual(try storedEntries(key).count, 1)
        state.saveWorkspaceLayout()
        let reopened = makeState(host: newProcess(after: host))
        await open(reopened, vault)
        XCTAssertEqual(buffers(reopened, path: "note.md").map(\.body), ["# note\n\npeers.\n"])
    }

    /// Codex adversarial review, round 4: a closing peer's entry retires
    /// only when the surviving peer has durably kept the same text, never on
    /// matching text alone.
    func testClosingAPeerKeepsItsEntryWhileTheSurvivorIsNotKept() async throws {
        let host = makeHost()
        let state = makeState(host: host)
        let vault = try makeVault()
        await open(state, vault)
        await select(state, "note.md")
        let key = try key(state)
        state.updateEditorText("# note\n\npeers.\n")
        await settle(state)
        let original = try XCTUnwrap(state.workspace.activeTab?.id)
        XCTAssertEqual(try storedEntries(key).count, 1)

        host.beforeCallForTests = {
            $0.kind == .claim || $0.kind == .write ? .refused(.vaultFull) : nil
        }
        state.newTab()
        await settle(state)
        XCTAssertEqual(try storedEntries(key).count, 1, "the peer could not be kept")
        state.requestCloseTab(original)
        XCTAssertNil(state.pendingTabClose, "a peer holds the text, so nothing asks")
        await settle(state)
        XCTAssertEqual(
            try storedEntries(key).map(\.text), ["# note\n\npeers.\n"],
            "the closed peer's entry stays until the survivor is kept")

        state.saveWorkspaceLayout()
        let reopened = makeState(host: newProcess(after: host))
        await open(reopened, vault)
        // The closed peer's entry is abandoned like an ungated drop, so it
        // comes back as a tab of its own beside the survivor: the text twice,
        // never lost.
        let restored = buffers(reopened, path: "note.md")
        XCTAssertFalse(restored.isEmpty)
        XCTAssertTrue(restored.allSatisfy { $0.body == "# note\n\npeers.\n" && $0.dirty })
    }

    /// Codex adversarial review, round 5: pointing a tab at another note in
    /// place drops its buffer without the dirty gate while a peer holds the
    /// text. The note's entry stays until that peer has kept it itself.
    func testPointingAPeerAtAnotherNoteKeepsItsEntryWhileTheSurvivorIsNotKept() async throws {
        let host = makeHost()
        let state = makeState(host: host)
        let vault = try makeVault()
        await open(state, vault)
        await select(state, "note.md")
        let key = try key(state)
        state.updateEditorText("# note\n\npeers.\n")
        await settle(state)
        let original = try XCTUnwrap(state.workspace.activeTab?.id)

        host.beforeCallForTests = {
            $0.kind == .claim || $0.kind == .write ? .refused(.vaultFull) : nil
        }
        state.newTab()
        await settle(state)
        state.activateTab(original)
        await select(state, "two.md")
        XCTAssertNil(state.pendingNavigation, "a peer holds the text, so nothing asks")
        await settle(state)
        XCTAssertEqual(
            try storedEntries(key).map(\.text), ["# note\n\npeers.\n"],
            "the entry stays until the survivor is kept")

        state.saveWorkspaceLayout()
        let reopened = makeState(host: newProcess(after: host))
        await open(reopened, vault)
        let restored = buffers(reopened, path: "note.md")
        XCTAssertFalse(restored.isEmpty)
        XCTAssertTrue(restored.allSatisfy { $0.body == "# note\n\npeers.\n" && $0.dirty })
    }

    func testASaveNeverWaitsOnARecoveryWriteAndALateWriteIsRetired() async throws {
        let host = makeHost()
        let state = makeState(host: host)
        let vault = try makeVault()
        await open(state, vault)
        await select(state, "note.md")
        let key = try key(state)
        let gate = Gate()
        host.beforeCallForTests = { call in
            if call.kind == .write { gate.hold() }
            return nil
        }
        state.updateEditorText("# note\n\nsaved while a write is held.\n")
        let entered = await waitEntered(gate)
        XCTAssertTrue(entered, "the write is held")
        await state.saveCurrentNote()?.value
        XCTAssertFalse(state.hasUnsavedChanges, "the save landed with the write held")
        XCTAssertEqual(try onDisk(vault, "note.md"), "# note\n\nsaved while a write is held.\n")
        host.beforeCallForTests = nil
        gate.open()
        await settle(state)
        XCTAssertTrue(try storedEntries(key).isEmpty, "the late write found no claim")
    }

    func testAnEpochMovedElsewhereNeverReKeepsUnchangedText() async throws {
        let host = makeHost()
        let announcer = RecordingAnnouncer()
        let state = makeState(host: host, announcer: announcer)
        await open(state, try makeVault())
        await select(state, "note.md")
        let key = try key(state)
        state.updateEditorText("# note\n\nkept.\n")
        await settle(state)
        // Another holder discards the vault's copies (the store edited by
        // hand).
        _ = try HotExitStore.open(deviceRoot: deviceRoot.path).discardVault(key: key)
        announcer.reset()
        let replies = ReplyRecorder()
        XCTAssertEqual(quit(state, replies), .terminateLater)
        await state.hotExitRouteTask?.value
        XCTAssertEqual(state.pendingNavigation, .closeVault, "the close asks")
        XCTAssertEqual(
            announcer.messages,
            [a11yRender(event: .unsavedNotesNotKept(reason: hotExitReasonTakenOver())).text])
        XCTAssertTrue(try storedEntries(key).isEmpty, "nothing re-kept on its own")
        state.resolvePendingNavigationCancel()
        XCTAssertEqual(replies.values, [false])

        // An edit after it starts a fresh episode, kept.
        state.updateEditorText("# note\n\nkept. later.\n")
        let again = ReplyRecorder()
        XCTAssertEqual(quit(state, again), .terminateLater)
        await state.hotExitRouteTask?.value
        XCTAssertEqual(again.values, [true])
        XCTAssertEqual(try storedEntries(key).map(\.text), ["# note\n\nkept. later.\n"])
    }

    func testAVaultHeldByAnotherProcessHasNoHotExit() async throws {
        let vault = try makeVault()
        let holder = makeState(host: makeHost())
        await open(holder, vault)
        let announcer = RecordingAnnouncer()
        let second = makeState(host: makeHost(), announcer: announcer)
        await open(second, vault)
        XCTAssertEqual(second.hotExitWriter?.isInert, true)
        XCTAssertEqual(second.hotExitWriter?.open.reason, hotExitReasonHeldElsewhere())
        await select(second, "note.md")
        second.updateEditorText("# note\n\nheld elsewhere.\n")
        announcer.reset()
        let replies = ReplyRecorder()
        XCTAssertEqual(quit(second, replies), .terminateLater)
        XCTAssertEqual(second.pendingNavigation, .closeVault)
        XCTAssertEqual(
            announcer.messages,
            [a11yRender(event: .unsavedNotesNotKept(reason: hotExitReasonHeldElsewhere())).text])
    }

    func testADamagedReadKeepsThePromptAndDiscardAllRepairs() async throws {
        let host = makeHost()
        let state = makeState(host: host)
        let vault = try makeVault()
        await open(state, vault)
        await select(state, "note.md")
        let key = try key(state)
        state.updateEditorText("# note\n\nkept.\n")
        let next = await keptQuit(state, host: host)
        let vaults = deviceRoot.appendingPathComponent("hot-exit/v1/vaults")
        let dirs = try FileManager.default.contentsOfDirectory(atPath: vaults.path)
        XCTAssertEqual(dirs.count, 1)
        try FileManager.default.removeItem(
            at: vaults.appendingPathComponent(dirs[0]).appendingPathComponent("epoch"))

        let announcer = RecordingAnnouncer()
        let reopened = makeState(host: next, announcer: announcer)
        await open(reopened, vault)
        XCTAssertEqual(reopened.hotExitWriter?.isInert, true)
        let reason = try XCTUnwrap(reopened.hotExitWriter?.open.reason)
        await select(reopened, "note.md")
        XCTAssertFalse(reopened.hasUnsavedChanges, "nothing restored from a damaged store")
        reopened.updateEditorText("# note\n\nnew.\n")
        announcer.reset()
        let replies = ReplyRecorder()
        XCTAssertEqual(quit(reopened, replies), .terminateLater)
        XCTAssertEqual(
            announcer.messages, [a11yRender(event: .unsavedNotesNotKept(reason: reason)).text])
        // The single note's Discard on the quit repairs the store.
        reopened.resolvePendingNavigationDiscard()
        await reopened.hotExitRouteTask?.value
        XCTAssertEqual(replies.values, [true])
        let repaired = makeState(host: newProcess(after: next))
        await open(repaired, vault)
        XCTAssertEqual(repaired.hotExitWriter?.isInert, false, "the store is usable again")
        XCTAssertTrue(try storedEntries(key).isEmpty)
    }

    func testAQuitSupersededDuringItsSettleIsAnsweredFalse() async throws {
        let host = makeHost()
        let state = makeState(host: host)
        await open(state, try makeVault())
        await dirtyTwo(state)
        await settle(state)
        let gate = Gate()
        host.beforeCallForTests = { call in
            if call.kind == .epoch { gate.hold() }
            return nil
        }
        let replies = ReplyRecorder()
        XCTAssertEqual(quit(state, replies), .terminateLater)
        let entered = await waitEntered(gate)
        XCTAssertTrue(entered, "the quit's settle is under way")
        state.closeVaultFromUserAction()
        XCTAssertEqual(replies.values, [false], "a newer request answers the quit")
        XCTAssertEqual(state.pendingVaultClose, 2, "Close Vault asks")
        host.beforeCallForTests = nil
        gate.open()
        await state.hotExitRouteTask?.value
        XCTAssertEqual(replies.values, [false], "answered once")
        state.resolveVaultCloseCancel()
        let again = ReplyRecorder()
        XCTAssertNotEqual(quit(state, again), .terminateCancel, "a later quit is admitted")
        await state.hotExitRouteTask?.value
    }

    func testTheRestoreSpeaksAfterTheOpeningLineOnADirectOpen() async throws {
        let host = makeHost()
        let vaultA = try makeVault("A")
        let vaultB = try makeVault("B")
        let first = makeState(host: host)
        await open(first, vaultB)
        await select(first, "note.md")
        first.updateEditorText("# note\n\nkept in B.\n")
        let next = await keptQuit(first, host: host)

        let announcer = RecordingAnnouncer()
        let state = makeState(host: next, announcer: announcer)
        await open(state, vaultA)
        announcer.reset()
        state.openVault(at: vaultB)
        await state.scanTask?.value
        let opened = a11yRender(event: .vaultOpened(vaultTitle: "B", sidebarNotice: "")).text
        let restored = a11yRender(event: .unsavedNotesRestored(count: 1)).text
        XCTAssertEqual(Array(announcer.messages.filter { $0 == opened || $0 == restored }), [opened, restored])
    }

    // MARK: - The launch announcement waits for the app to settle (field pass)

    /// Stands in for `LaunchAnnouncementSettle.production`: the test says
    /// when the app has settled.
    @MainActor
    final class ManualSettle {
        private var bodies: [(id: Int, body: @MainActor () -> Void)] = []
        private var nextId = 0
        var waiting: Int { bodies.count }

        func install(on state: AppState) {
            state.launchAnnouncementSettle = { [unowned self] body in
                let id = self.nextId
                self.nextId += 1
                self.bodies.append((id, body))
                return AnyCancellable { [weak self] in self?.bodies.removeAll { $0.id == id } }
            }
        }

        func settle() {
            let due = bodies
            bodies = []
            for entry in due { entry.body() }
        }
    }

    /// The VoiceOver field pass (2026-11-14): VoiceOver drops an
    /// announcement a newly launched app posts before it has read the app's
    /// window, so the launch's opening line and restore sentences wait for
    /// the app to settle, then speak once, in order.
    func testTheLaunchAnnouncementWaitsForTheAppToSettle() async throws {
        let host = makeHost()
        let vault = try makeVault()
        let first = makeState(host: host)
        await open(first, vault)
        await select(first, "note.md")
        first.updateEditorText("# note\n\nkept.\n")
        let next = await keptQuit(first, host: host)

        let announcer = RecordingAnnouncer()
        let state = makeState(host: next, announcer: announcer)
        let settle = ManualSettle()
        settle.install(on: state)
        await open(state, vault)
        announcer.reset()
        state.announceVaultOpenedOnMount(vaultTitle: "A", sidebarNotice: "")
        XCTAssertEqual(announcer.messages, [], "nothing speaks before the app settles")
        XCTAssertEqual(settle.waiting, 1)

        settle.settle()
        let opened = a11yRender(event: .vaultOpened(vaultTitle: "A", sidebarNotice: "")).text
        let restored = a11yRender(event: .unsavedNotesRestored(count: 1)).text
        XCTAssertEqual(announcer.messages, [opened, restored])
        settle.settle()
        XCTAssertEqual(announcer.messages, [opened, restored], "spoken once")
    }

    func testAMountAfterTheFirstAnnouncesAtOnce() async throws {
        let announcer = RecordingAnnouncer()
        let state = makeState(host: makeHost(), announcer: announcer)
        let settle = ManualSettle()
        settle.install(on: state)
        await open(state, try makeVault())
        state.announceVaultOpenedOnMount(vaultTitle: "A", sidebarNotice: "")
        settle.settle()
        announcer.reset()

        state.announceVaultOpenedOnMount(vaultTitle: "A", sidebarNotice: "")
        XCTAssertEqual(settle.waiting, 0)
        XCTAssertEqual(
            announcer.messages,
            [a11yRender(event: .vaultOpened(vaultTitle: "A", sidebarNotice: "")).text])
    }

    func testALaunchAnnouncementStillHeldWhenTheVaultClosesIsNeverSpoken() async throws {
        let announcer = RecordingAnnouncer()
        let state = makeState(host: makeHost(), announcer: announcer)
        let settle = ManualSettle()
        settle.install(on: state)
        await open(state, try makeVault())
        announcer.reset()
        state.announceVaultOpenedOnMount(vaultTitle: "A", sidebarNotice: "")
        XCTAssertTrue(state.closeVault())
        XCTAssertEqual(settle.waiting, 0, "the close cancels the held announcement")
        settle.settle()
        let opened = a11yRender(event: .vaultOpened(vaultTitle: "A", sidebarNotice: "")).text
        XCTAssertFalse(announcer.messages.contains(opened))
    }

    // MARK: - Restore, then the reader (W§5)

    /// `note.md` kept dirty by a kept quit; the next process opened on it.
    func restoredNote(_ text: String) async throws -> (AppState, URL, HotExitStoreHost) {
        let host = makeHost()
        let state = makeState(host: host)
        let vault = try makeVault()
        await open(state, vault)
        await select(state, "note.md")
        state.updateEditorText(text)
        let next = await keptQuit(state, host: host)
        let reopened = makeState(host: next)
        await open(reopened, vault)
        XCTAssertEqual(reopened.currentNoteText, text)
        XCTAssertTrue(reopened.hasUnsavedChanges)
        return (reopened, vault, next)
    }

    func testRestoreThenEditThenReopenGetsTheEdit() async throws {
        let (state, vault, host) = try await restoredNote("# note\n\nkept.\n")
        state.updateEditorText("# note\n\nkept. edited.\n")
        await settle(state)
        state.saveWorkspaceLayout()
        // The crash shape: no route, a new process.
        let reopened = makeState(host: newProcess(after: host))
        await open(reopened, vault)
        XCTAssertEqual(reopened.currentNoteText, "# note\n\nkept. edited.\n")
    }

    func testRestoreThenAdoptThenCrashStillRestores() async throws {
        let (state, vault, host) = try await restoredNote("# note\n\nkept.\n")
        await settle(state)
        let reopened = makeState(host: newProcess(after: host))
        await open(reopened, vault)
        XCTAssertEqual(reopened.currentNoteText, "# note\n\nkept.\n", "an abandoned episode is kept")
    }

    func testRestoreThenSaveThenReopenGetsNothing() async throws {
        let (state, vault, host) = try await restoredNote("# note\n\nkept.\n")
        await state.saveCurrentNote()?.value
        XCTAssertFalse(state.hasUnsavedChanges)
        await settle(state)
        XCTAssertTrue(try storedEntries(try key(state)).isEmpty)
        let reopened = makeState(host: newProcess(after: host))
        await open(reopened, vault)
        XCTAssertFalse(reopened.hasUnsavedChanges)
        XCTAssertEqual(try onDisk(vault, "note.md"), "# note\n\nkept.\n")
    }

    func testRestoreThenDiscardThenReopenGetsNothing() async throws {
        let (state, vault, host) = try await restoredNote("# note\n\nkept.\n")
        let key = try key(state)
        state.requestCloseTab()
        XCTAssertNotNil(state.pendingTabClose)
        state.resolveTabCloseDiscard()
        await settle(state)
        XCTAssertTrue(try storedEntries(key).isEmpty, "the inherited text retired")
        state.saveWorkspaceLayout()
        let reopened = makeState(host: newProcess(after: host))
        await open(reopened, vault)
        XCTAssertTrue(reopened.hotExitMarkdownTabs.allSatisfy {
            reopened.hotExitBuffer(for: $0)?.dirty != true
        })
    }

    func testAClosedTabRetiresWhatItInherited() async throws {
        let host = makeHost()
        let state = makeState(host: host)
        let vault = try makeVault()
        await open(state, vault)
        await select(state, "note.md")
        let tab = try XCTUnwrap(state.workspace.activeTab?.id)
        state.saveWorkspaceLayout()
        let key = try key(state)
        let next = newProcess(after: host)
        let store = try HotExitStore.open(deviceRoot: deviceRoot.path)
        _ = try store.claimEntry(
            key: key, epoch: try store.currentEpoch(key: key), entryId: tab.raw.uuidString,
            expected: nil, incarnation: try newHotExitIncarnation())

        let reopened = makeState(host: next)
        await open(reopened, vault)
        reopened.requestCloseTab()
        XCTAssertNil(reopened.pendingTabClose, "a clean tab closes")
        await settle(reopened)
        XCTAssertTrue(
            try HotExitStore.open(deviceRoot: deviceRoot.path).readBuffers(key: key).claims.isEmpty,
            "the carried orphan claim retired")
    }

    // MARK: - A late discard (O6, owner call 3)

    func testALateDiscardNeverReKeepsTheTextItDiscarded() async throws {
        let host = makeHost()
        let announcer = RecordingAnnouncer()
        let state = makeState(host: host, announcer: announcer)
        await open(state, try makeVault())
        await dirtyTwo(state)
        let key = try key(state)
        await settle(state)
        let gate = Gate()
        host.beforeCallForTests = { call in
            if call.kind == .discard { gate.hold() }
            return nil
        }
        state.closeVaultFromUserAction()
        state.resolveVaultCloseDiscardAll()
        await state.hotExitRouteTask?.value  // 4 s, then the prompt
        XCTAssertNotNil(state.pendingHotExitDiscard)
        state.resolveHotExitDiscardPending(.cancel)
        host.beforeCallForTests = nil
        gate.open()
        await settle(state)
        XCTAssertTrue(try storedEntries(key).isEmpty, "the discard committed late")

        // Unchanged, the covered buffers are not kept again: the next quit asks.
        announcer.reset()
        let replies = ReplyRecorder()
        XCTAssertEqual(quit(state, replies), .terminateLater)
        await state.hotExitRouteTask?.value
        XCTAssertEqual(state.pendingVaultClose, 2)
        XCTAssertEqual(
            announcer.messages,
            [a11yRender(event: .unsavedNotesNotKept(reason: hotExitReasonDiscarded())).text])
        XCTAssertTrue(try storedEntries(key).isEmpty)
        state.resolveVaultCloseCancel()

        // Edited later, a covered buffer starts a fresh episode, kept.
        state.updateEditorText("# note\n\ndirty note, edited after.\n")
        await settle(state)
        XCTAssertEqual(try storedEntries(key).map(\.text), ["# note\n\ndirty note, edited after.\n"])
    }

    func testAHoldOutlivesTheVaultUntilItsObligationsResolve() async throws {
        let host = makeHost()
        host.retirementRetryDelay = 0.2
        let state = makeState(host: host)
        let vault = try makeVault()
        await open(state, vault)
        await select(state, "note.md")
        let key = try key(state)
        state.updateEditorText("# note\n\ndirty.\n")
        await settle(state)
        let lock = NSLock()
        var failing = true
        host.beforeCallForTests = { call in
            lock.withLock { call.kind == .retire && failing ? .io : nil }
        }
        await state.saveCurrentNote()?.value
        state.closeVaultFromUserAction()
        await state.hotExitRouteTask?.value
        state.resolveHotExitDiscardPending(.closeKeepingCopies)
        XCTAssertNil(state.currentSession)
        XCTAssertTrue(host.holdsVaultForTests(key), "the hold outlives the vault")
        let other = makeHost()
        XCTAssertFalse(other.acquireHold(key).held, "another process cannot take it yet")
        lock.withLock { failing = false }
        let deadline = Date().addingTimeInterval(10)
        while host.holdsVaultForTests(key), Date() < deadline { await HotExitWriter.frame() }
        XCTAssertFalse(host.holdsVaultForTests(key), "released once the retirement lands")
        XCTAssertTrue(other.acquireHold(key).held)
        _ = vault
    }

    // MARK: - Red team PR 29.2

    func testCloseAndKeepTheCopiesRevertsNothingAndSpeaksAPlainClose() async throws {
        let host = makeHost()
        let announcer = RecordingAnnouncer()
        let state = makeState(host: host, announcer: announcer)
        await open(state, try makeVault())
        await dirtyTwo(state)
        await settle(state)
        host.beforeCallForTests = { $0.kind == .discard || $0.kind == .retire ? .io : nil }
        state.closeVaultFromUserAction()
        state.resolveVaultCloseDiscardAll()
        await state.hotExitRouteTask?.value
        announcer.reset()
        state.resolveHotExitDiscardPending(.closeKeepingCopies)
        XCTAssertNil(state.currentSession)
        XCTAssertEqual(announcer.messages, [a11yRender(event: .vaultClosed).text])
    }

    func testTextTypedWhileADiscardWaitsIsNeverClosedOver() async throws {
        let host = makeHost()
        let state = makeState(host: host)
        await open(state, try makeVault())
        await dirtyTwo(state)
        await settle(state)
        // The discard fails, and the reader types while it was out.
        host.beforeCallForTests = { call in
            if call.kind == .discard {
                DispatchQueue.main.async {
                    MainActor.assumeIsolated {
                        state.updateEditorText("# note\n\ndirty note, typed during the wait.\n")
                    }
                }
                Thread.sleep(forTimeInterval: 0.1)
                return .io
            }
            return call.kind == .retire ? .io : nil
        }
        state.closeVaultFromUserAction()
        state.resolveVaultCloseDiscardAll()
        await state.hotExitRouteTask?.value
        XCTAssertNotNil(state.pendingHotExitDiscard)
        state.resolveHotExitDiscardPending(.closeKeepingCopies)
        XCTAssertNotNil(state.currentSession, "the close does not run over new text")
        XCTAssertNotNil(state.pendingVaultClose, "Close Vault asks again")
        XCTAssertEqual(state.currentNoteText, "# note\n\ndirty note, typed during the wait.\n")
    }

    func testAQuitOvertakenByAnOlderPromptsDiscardAllIsAnswered() async throws {
        let host = makeHost()
        let state = makeState(host: host)
        await open(state, try makeVault())
        await dirtyTwo(state)
        await settle(state)
        state.closeVaultFromUserAction()
        XCTAssertEqual(state.pendingVaultClose, 2)
        let gate = Gate()
        host.beforeCallForTests = { call in
            if call.kind == .epoch { gate.hold() }
            return nil
        }
        let replies = ReplyRecorder()
        XCTAssertEqual(quit(state, replies), .terminateLater)
        let entered = await waitEntered(gate)
        XCTAssertTrue(entered)
        // The Close Vault prompt still on screen answers the quit first.
        state.resolveVaultCloseDiscardAll()
        XCTAssertEqual(replies.values, [false])
        host.beforeCallForTests = nil
        gate.open()
        await state.hotExitRouteTask?.value
        XCTAssertEqual(replies.values, [false], "answered once")
        XCTAssertNil(state.currentSession, "the Discard All closed")
    }

    func testTheQuitsOwnDiscardPendingPromptAnswersANewerRequest() async throws {
        let host = makeHost()
        host.retirementRetryDelay = 60
        let state = makeState(host: host)
        await open(state, try makeVault())
        await select(state, "note.md")
        state.updateEditorText("# note\n\ndirty.\n")
        await settle(state)
        host.beforeCallForTests = { $0.kind == .retire ? .io : nil }
        await state.saveCurrentNote()?.value
        let replies = ReplyRecorder()
        XCTAssertEqual(quit(state, replies), .terminateLater)
        await state.hotExitRouteTask?.value
        XCTAssertNotNil(state.pendingHotExitDiscard, "a retirement unconfirmed")
        XCTAssertTrue(state.isTerminationPending, "the reply waits where a newer request finds it")
        state.closeVaultFromUserAction()
        XCTAssertEqual(replies.values, [false])
    }

    func testANoteOpenedBackIntoADroppedTabIsKept() async throws {
        let host = makeHost()
        let state = makeState(host: host)
        let vault = try makeVault()
        try #"{"nodes":[],"edges":[]}"#.write(
            to: vault.appendingPathComponent("board.canvas"), atomically: true, encoding: .utf8)
        await open(state, vault)
        await select(state, "note.md")
        let tab = try XCTUnwrap(state.workspace.activeTab?.id)
        let key = try key(state)
        state.updateEditorText("# note\n\ndropped.\n")
        await settle(state)
        state.openCanvasFile("board.canvas", target: .currentTab)
        await settle(state)
        await select(state, "two.md")
        XCTAssertEqual(state.workspace.activeTab?.id, tab, "the same tab holds a note again")
        state.updateEditorText("# two\n\nin the old tab.\n")
        await settle(state)
        XCTAssertTrue(state.hotExitWriter?.isKeptForTests(tab) ?? false, "kept, never taken over")
        XCTAssertEqual(
            Set(try storedEntries(key).map(\.text)),
            ["# note\n\ndropped.\n", "# two\n\nin the old tab.\n"])
    }

    func testOnlyBuffersRestoredUnsavedAreCounted() async throws {
        let host = makeHost()
        let state = makeState(host: host)
        let vault = try makeVault()
        await open(state, vault)
        await select(state, "note.md")
        let first = try XCTUnwrap(state.workspace.activeTab?.id)
        let noteHash = try XCTUnwrap(state.currentNoteContentHash)
        state.saveWorkspaceLayout()
        let key = try key(state)
        let next = newProcess(after: host)
        // One entry equal to the disk (restores clean), one dirty.
        try craftEntry(key, id: first.raw.uuidString, path: "note.md", text: "# note\n", baseline: noteHash)
        try craftEntry(
            key, id: UUID().uuidString, path: "two.md", text: "# two\n\ndirty.\n",
            baseline: try XCTUnwrap(state.currentSession).readNoteParts(path: "two.md").contentHash)

        let announcer = RecordingAnnouncer()
        let reopened = makeState(host: next, announcer: announcer)
        await open(reopened, vault)
        reopened.announceVaultOpened(vaultTitle: "A", sidebarNotice: "")
        XCTAssertTrue(announcer.messages.contains(a11yRender(event: .unsavedNotesRestored(count: 1)).text))
        await settle(reopened)
        XCTAssertEqual(try storedEntries(key).map(\.path), ["two.md"], "the clean one retired")
    }

    func testAFailingRetirementIsRetriedOncePerSettle() async throws {
        let host = makeHost()
        host.retirementRetryDelay = 60
        let state = makeState(host: host)
        await open(state, try makeVault())
        await select(state, "note.md")
        state.updateEditorText("# note\n\ndirty.\n")
        await settle(state)
        let lock = NSLock()
        var retires = 0
        host.beforeCallForTests = { call in
            guard call.kind == .retire else { return nil }
            lock.withLock { retires += 1 }
            return .io
        }
        await state.saveCurrentNote()?.value
        state.closeVaultFromUserAction()
        await state.hotExitRouteTask?.value
        XCTAssertNotNil(state.pendingHotExitDiscard)
        XCTAssertLessThanOrEqual(lock.withLock { retires }, 3, "no retry storm during a settle")
    }
}
