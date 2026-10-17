// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation
import XCTest

@testable import SlateMac

/// PR 23 (#944, FL-06 rounds 31 and 32): an acknowledged sidebar write that
/// failed survives a quit or a crash. It is journaled as a typed intent
/// before it is acknowledged — and a rename's or move's transform before the
/// filesystem operation can commit — in a write-ahead journal keyed by the
/// vault's identity under Application Support, replayed by the next
/// same-vault open through the chained drain, never evicted, and surfaced
/// rather than dropped when it is too old or refused.
///
/// "Relaunch" is `AppState.simulateRelaunchForTesting()`: the old
/// `AppState` is gone, the statics a new process would not have are reset,
/// and only the journal's files remain — as they would on disk.
@MainActor
final class SidebarWriteJournalTests: XCTestCase {
  private var root: URL!
  private var journal: SidebarWriteJournal!
  private var originalCap = 0

  private final class RecordingAnnouncer: AnnouncementPosting, @unchecked Sendable {
    private(set) var messages: [String] = []
    /// Runs inside `post`, so a test can look at the world at the moment a
    /// sentence is spoken.
    var onPost: ((String) -> Void)?

    func post(_ message: String, priority: AnnouncementPriority) {
      messages.append(message)
      onPost?(message)
    }
  }

  /// Holds work on a worker until the test opens it.
  private final class GateBox: @unchecked Sendable {
    private let semaphore = DispatchSemaphore(value: 0)
    func wait() { semaphore.wait() }
    func open() { semaphore.signal() }
  }

  /// A value a worker sets and the test reads.
  private final class Box<Value>: @unchecked Sendable {
    private let lock = NSLock()
    private var stored: Value

    init(_ value: Value) { stored = value }

    var value: Value { lock.withLock { stored } }

    func set(_ value: Value) { lock.withLock { stored = value } }

    func update(_ change: (inout Value) -> Void) { lock.withLock { change(&stored) } }
  }

  override func setUpWithError() throws {
    root = FileManager.default.temporaryDirectory
      .appendingPathComponent("slate-sidebar-write-journal-\(UUID().uuidString)")
    try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
    journal = SidebarWriteJournal(
      directory: root.appendingPathComponent("journal", isDirectory: true))
    AppState.installSidebarWriteJournalForTesting(journal)
    originalCap = AppState.sidebarUnflushedWriteRetriesCap
  }

  override func tearDown() async throws {
    AppState.sidebarUnflushedWriteRetriesCap = originalCap
    journal?.io.setBeforeSyncHookForTesting(nil)
    await journal?.waitUntilIdle()
    // Nothing this class parked may leak into a later test's registry.
    await AppState.simulateRelaunchForTesting()
    AppState.installSidebarWriteJournalForTesting(
      SidebarWriteJournal(directory: SidebarWriteJournal.temporaryDirectory()))
    if let root {
      if let enumerator = FileManager.default.enumerator(atPath: root.path) {
        for case let path as String in enumerator {
          try? FileManager.default.setAttributes(
            [.posixPermissions: 0o755],
            ofItemAtPath: root.appendingPathComponent(path).path)
        }
      }
      try? FileManager.default.removeItem(at: root)
    }
  }

  // MARK: - Fixtures

  private func makeVault(
    named name: String, files: [String], folders: [String] = []
  ) throws -> URL {
    let vault = root.appendingPathComponent(name, isDirectory: true)
    try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
    for folder in folders {
      try FileManager.default.createDirectory(
        at: vault.appendingPathComponent(folder), withIntermediateDirectories: true)
    }
    for path in files {
      let url = vault.appendingPathComponent(path)
      try FileManager.default.createDirectory(
        at: url.deletingLastPathComponent(), withIntermediateDirectories: true)
      try "# \((path as NSString).lastPathComponent)\n".write(
        to: url, atomically: true, encoding: .utf8)
    }
    return vault
  }

  private func open(_ vault: URL, announcer: AnnouncementPosting) throws -> AppState {
    let state = AppState(
      recentsStore: RecentVaultsStore(
        fileURL: root.appendingPathComponent("recents-\(UUID().uuidString).json")),
      externalOpener: { _ in true },
      announcer: announcer)
    state.openVault(at: vault)
    _ = try XCTUnwrap(state.currentSession).scanInitial(cancel: CancelToken())
    return state
  }

  private func select(_ state: AppState, _ path: String, folder: String) throws {
    let session = try XCTUnwrap(state.currentSession)
    _ = state.publishSidebarSelectionSnapshot(
      SidebarSelectionSnapshot(
        sessionIdentity: ObjectIdentifier(session),
        items: [SidebarSelectionItem(path: path, isDirectory: false, isMarkdown: true)],
        focusedPath: path,
        creationParent: folder))
  }

  /// No selection: organization commands target the vault default.
  private func selectNothing(_ state: AppState) throws {
    let session = try XCTUnwrap(state.currentSession)
    _ = state.publishSidebarSelectionSnapshot(
      SidebarSelectionSnapshot(
        sessionIdentity: ObjectIdentifier(session), items: [], focusedPath: nil,
        creationParent: ""))
  }

  private func sidebarFile(_ vault: URL) -> [String: Any]? {
    guard
      let data = try? Data(contentsOf: vault.appendingPathComponent(".slate/sidebar.json"))
    else { return nil }
    return try? JSONSerialization.jsonObject(with: data) as? [String: Any]
  }

  private func writeSidebarFile(_ vault: URL, _ json: String) throws {
    let slate = vault.appendingPathComponent(".slate", isDirectory: true)
    try FileManager.default.createDirectory(at: slate, withIntermediateDirectories: true)
    try Data(json.utf8).write(to: slate.appendingPathComponent("sidebar.json"))
  }

  private func pinsOnDisk(_ vault: URL, folder: String) -> [String]? {
    guard
      let data = try? Data(contentsOf: vault.appendingPathComponent(".slate/sidebar.json")),
      let json = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
      let pins = json["pins"] as? [String: Any]
    else { return nil }
    return pins[folder] as? [String]
  }

  private func journalEntries(
    _ identity: AppState.SidebarVaultRootIdentity
  ) -> [UUID: SidebarWriteJournal.Entry.Content] {
    SidebarWriteJournal.entriesOnDiskForTesting(at: journal.fileURL(for: identity))?
      .entries ?? [:]
  }

  private static func pin(
    _ path: String, in folder: String
  ) -> SidebarWriteJournal.Entry.Content {
    .intent(.pin(path: path, folder: folder), acknowledgement: "Pinned.")
  }

  private func eventually(
    _ condition: () -> Bool, attempts: Int = 1_000
  ) async -> Bool {
    for _ in 0..<attempts {
      if condition() { return true }
      try? await Task.sleep(nanoseconds: 5_000_000)
    }
    return condition()
  }

  /// Pin each of `paths`, acknowledged ("Pinned."), and make their writes
  /// fail after the vault closed — `.slate` refuses writes — so they are
  /// parked with nobody left to hear the failure. The `AppState` is gone on
  /// return and `.slate` is writable again.
  private func parkAcknowledgedPins(
    named name: String, paths: [String], folder: String,
    folders: [String] = []
  ) async throws -> (vault: URL, identity: AppState.SidebarVaultRootIdentity) {
    try await parkAcknowledged(
      named: name, files: paths, folders: folders,
      acknowledgements: Array(repeating: "Pinned.", count: paths.count)
    ) { state in
      for path in paths {
        try self.select(state, path, folder: folder)
        _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarPinNote)
        await state.sidebarOrganizationAcknowledgementTaskForTesting?.value
      }
    }
  }

  /// `dispatch` runs acknowledged commands behind a gated writer; their
  /// writes then fail after the vault closed, as above.
  private func parkAcknowledged(
    named name: String, files: [String], folders: [String] = [],
    acknowledgements: [String],
    dispatch: (AppState) async throws -> Void
  ) async throws -> (vault: URL, identity: AppState.SidebarVaultRootIdentity) {
    let vault = try makeVault(named: name, files: files, folders: folders)
    let announcer = RecordingAnnouncer()
    let state = try open(vault, announcer: announcer)
    let identity = try XCTUnwrap(state.sidebarVaultRootIdentity)
    let gate = GateBox()
    // Hold the chain so the commands' writes run after the close.
    state.enqueueSidebarOrganizationWriteForTesting { _ in gate.wait() }
    try await dispatch(state)
    XCTAssertEqual(
      announcer.messages.filter { acknowledgements.contains($0) }.count,
      acknowledgements.count, "each command was acknowledged")
    let queued = state.sidebarOrganizationPersistTaskForTesting
    state.closeVault()
    let slate = vault.appendingPathComponent(".slate", isDirectory: true)
    try FileManager.default.createDirectory(at: slate, withIntermediateDirectories: true)
    try FileManager.default.setAttributes(
      [.posixPermissions: 0o500], ofItemAtPath: slate.path)
    gate.open()
    await queued?.value
    try FileManager.default.setAttributes(
      [.posixPermissions: 0o755], ofItemAtPath: slate.path)
    XCTAssertFalse(
      FileManager.default.fileExists(
        atPath: slate.appendingPathComponent("sidebar.json").path),
      "the acknowledged writes failed")
    await journal.waitUntilIdle()
    return (vault, identity)
  }

  // MARK: - Facts

  func testAParkedIntentSurvivesARelaunchAndReplays() async throws {
    let (vault, identity) = try await parkAcknowledgedPins(
      named: "relaunch", paths: ["Projects/note.md"], folder: "Projects",
      folders: ["Projects"])
    XCTAssertEqual(
      Array(journalEntries(identity).values),
      [Self.pin("Projects/note.md", in: "Projects")],
      "the parked pin is on disk in the journal, not only in memory")

    // Quit. A new process has none of the old one's statics.
    await AppState.simulateRelaunchForTesting()

    let announcer = RecordingAnnouncer()
    let relaunched = try open(vault, announcer: announcer)
    let replayed = await eventually {
      self.pinsOnDisk(vault, folder: "Projects") == ["Projects/note.md"]
    }
    XCTAssertTrue(replayed, "the journaled pin replays on the next same-vault open")
    await relaunched.sidebarOrganizationPersistTaskForTesting?.value
    XCTAssertTrue(
      relaunched.sidebarOrganization.pins.isPinned(
        "Projects/note.md", inFolder: "Projects"))
    XCTAssertTrue(
      announcer.messages.contains("Pinned."),
      "the replay speaks through the existing acknowledgement")
    await journal.waitUntilIdle()
    XCTAssertTrue(journalEntries(identity).isEmpty, "a committed replay settles its entry")
    XCTAssertFalse(
      FileManager.default.fileExists(atPath: journal.fileURL(for: identity).path),
      "a journal with nothing live is removed")
  }

  func testTheJournalIsWrittenBeforeAcknowledgementAndBeforeAStructuralCommit()
    async throws
  {
    let announcer = RecordingAnnouncer()
    let vault = try makeVault(
      named: "write-ahead", files: ["Projects/note.md"], folders: ["Projects"])
    let state = try open(vault, announcer: announcer)
    let identity = try XCTUnwrap(state.sidebarVaultRootIdentity)
    let journalURL = journal.fileURL(for: identity)

    // 1. An intent is fsync'd, off the main actor, before it is
    // acknowledged: hold the sync and nothing is spoken.
    let syncGate = GateBox()
    let syncs = Box<[Bool]>([])
    journal.io.setBeforeSyncHookForTesting { _ in
      syncs.update { $0.append(Thread.isMainThread) }
      syncGate.wait()
    }
    let durableWhenSpoken = Box<Bool?>(nil)
    announcer.onPost = { message in
      guard message == "Pinned." else { return }
      let entries = SidebarWriteJournal.entriesOnDiskForTesting(at: journalURL)?.entries
      durableWhenSpoken.set(
        entries?.values.contains(Self.pin("Projects/note.md", in: "Projects")) == true)
    }
    try select(state, "Projects/note.md", folder: "Projects")
    _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarPinNote)
    XCTAssertTrue(
      state.sidebarOrganization.pins.isPinned("Projects/note.md", inFolder: "Projects"),
      "the tree shows the pin at once")
    let syncing = await eventually { !syncs.value.isEmpty }
    XCTAssertTrue(syncing)
    XCTAssertFalse(
      announcer.messages.contains("Pinned."),
      "no acknowledgement while its journal line is not yet durable")
    journal.io.setBeforeSyncHookForTesting { _ in
      syncs.update { $0.append(Thread.isMainThread) }
    }
    syncGate.open()
    await state.sidebarOrganizationAcknowledgementTaskForTesting?.value
    XCTAssertTrue(announcer.messages.contains("Pinned."))
    XCTAssertEqual(
      durableWhenSpoken.value, true, "the journal held the intent when it was spoken")
    await state.sidebarOrganizationPersistTaskForTesting?.value
    XCTAssertEqual(pinsOnDisk(vault, folder: "Projects"), ["Projects/note.md"])

    // 2. A rename's transform is durable before the filesystem operation
    // can commit: the native runner sees it on disk when it starts.
    let journaledAtCommit = Box<Bool?>(nil)
    state.structuralRenameRunner = { session, path, _, newName in
      let entries = SidebarWriteJournal.entriesOnDiskForTesting(at: journalURL)?.entries
      journaledAtCommit.set(
        entries?.values.contains { content in
          guard case .structural(let transform, committed: false) = content else {
            return false
          }
          return transform.renames.contains {
            $0.oldPath == "Projects/note.md" && $0.newPath == "Projects/renamed.md"
          }
        } == true)
      return try session.renameFile(path: path, newName: newName)
    }
    let rename = try XCTUnwrap(
      state.renameEntry(path: "Projects/note.md", isDirectory: false, to: "renamed.md"))
    await rename.value
    XCTAssertEqual(
      journaledAtCommit.value, true,
      "the anticipated transform was journaled before the rename could commit")
    await state.sidebarOrganizationPersistTaskForTesting?.value
    XCTAssertEqual(pinsOnDisk(vault, folder: "Projects"), ["Projects/renamed.md"])
    await journal.waitUntilIdle()
    XCTAssertTrue(
      journalEntries(identity).isEmpty,
      "the rename's entry settles with the sidebar write that drained it")
    XCTAssertFalse(syncs.value.isEmpty)
    XCTAssertFalse(syncs.value.contains(true), "no journal fsync ran on the main thread")
  }

  func testCapacityRefusesNewAcknowledgementsAndEvictsNothing() async throws {
    let (vault, identity) = try await parkAcknowledgedPins(
      named: "capacity", paths: ["a.md", "b.md"], folder: "")
    try "# c\n".write(
      to: vault.appendingPathComponent("c.md"), atomically: true, encoding: .utf8)
    AppState.sidebarUnflushedWriteRetriesCap = 2
    await AppState.simulateRelaunchForTesting()

    let announcer = RecordingAnnouncer()
    let relaunched = try open(vault, announcer: announcer)
    // The earlier launch's entries count until a drain adopts them: a new
    // acknowledgement is refused up front with the typed dispatch failure.
    try select(relaunched, "c.md", folder: "")
    XCTAssertThrowsError(
      try relaunched.dispatchSidebarAction(id: SlateCommandID.sidebarPinNote)
    ) { error in
      XCTAssertEqual(
        error.sidebarActionAnnouncement, AppState.sidebarRetainedWritesCapacityReason)
    }
    XCTAssertFalse(
      relaunched.sidebarOrganization.pins.isPinned("c.md", inFolder: ""),
      "refused before any optimistic reflect")
    XCTAssertEqual(
      journalEntries(identity).count, 2,
      "the refusal journals nothing and evicts nothing")

    let landed = await eventually {
      self.pinsOnDisk(vault, folder: "") == ["a.md", "b.md"]
    }
    XCTAssertTrue(landed, "every acknowledged pin still lands, in order")
    await relaunched.sidebarOrganizationPersistTaskForTesting?.value
    _ = try relaunched.dispatchSidebarAction(id: SlateCommandID.sidebarPinNote)
    await relaunched.sidebarOrganizationPersistTaskForTesting?.value
    XCTAssertEqual(
      pinsOnDisk(vault, folder: ""), ["a.md", "b.md", "c.md"],
      "capacity clears once the drain settles")
  }

  func testAReplacedVaultNeverMatchesItsJournal() async throws {
    let (vault, identity) = try await parkAcknowledgedPins(
      named: "replaced", paths: ["Projects/note.md"], folder: "Projects",
      folders: ["Projects"])
    await AppState.simulateRelaunchForTesting()

    // Another vault takes the path: same files, another directory.
    try FileManager.default.removeItem(at: vault)
    _ = try makeVault(
      named: "replaced", files: ["Projects/note.md"], folders: ["Projects"])
    let announcer = RecordingAnnouncer()
    let replacement = try open(vault, announcer: announcer)
    let replacementIdentity = try XCTUnwrap(replacement.sidebarVaultRootIdentity)
    XCTAssertNotEqual(replacementIdentity, identity)
    await replacement.sidebarOrganizationPersistTaskForTesting?.value
    try? await Task.sleep(nanoseconds: 200_000_000)
    XCTAssertNil(
      pinsOnDisk(vault, folder: "Projects"), "another vault's journal never replays here")
    XCTAssertFalse(announcer.messages.contains("Pinned."))
    XCTAssertEqual(
      Array(journalEntries(identity).values),
      [Self.pin("Projects/note.md", in: "Projects")],
      "the original vault's entry is left for that vault")

    // A journal that claims this vault's device and inode but another birth
    // time belongs to a vault that is gone (its inode was reused): it is
    // never replayed, the discard is said, and the file is removed.
    replacement.closeVault()
    let forgedURL = journal.fileURL(for: replacementIdentity)
    var forged = try XCTUnwrap(
      SidebarWriteJournal.encode(.header(identity: replacementIdentity, birth: 42)))
    forged.append(
      try XCTUnwrap(
        SidebarWriteJournal.encode(
          .put(
            SidebarWriteJournal.Entry(
              id: UUID(), position: 0, recordedAt: Date(),
              content: Self.pin("Projects/note.md", in: "Projects"))))))
    try forged.write(to: forgedURL)
    await AppState.simulateRelaunchForTesting()

    let reopenedAnnouncer = RecordingAnnouncer()
    let reopened = try open(vault, announcer: reopenedAnnouncer)
    let discardSaid = await eventually {
      reopenedAnnouncer.messages.contains(
        "The vault at this location changed, so a queued sidebar settings write "
          + "was discarded.")
    }
    XCTAssertTrue(discardSaid, "the foreign journal is surfaced, not silently dropped")
    await reopened.sidebarOrganizationPersistTaskForTesting?.value
    XCTAssertNil(pinsOnDisk(vault, folder: "Projects"))
    XCTAssertFalse(reopenedAnnouncer.messages.contains("Pinned."))
    await journal.waitUntilIdle()
    XCTAssertFalse(
      FileManager.default.fileExists(atPath: forgedURL.path),
      "the foreign journal is removed")
  }

  func testAJournalFollowsItsVaultWhenItsVolumeReturnsUnderAnotherDeviceNumber()
    async throws
  {
    let (vault, identity) = try await parkAcknowledgedPins(
      named: "remount", paths: ["Projects/note.md"], folder: "Projects",
      folders: ["Projects"])
    let fingerprint = SidebarWriteJournal.fingerprint(of: vault, expecting: identity)
    let birth = try XCTUnwrap(fingerprint.birth)
    let volume = try XCTUnwrap(fingerprint.volume)
    // The journal as an earlier mount of the same volume wrote it: another
    // device number, the same volume UUID, inode and birth time.
    let original = journal.fileURL(for: identity)
    let bytes = try Data(contentsOf: original)
    let firstNewline = try XCTUnwrap(bytes.firstIndex(of: 0x0A))
    let earlierMount = AppState.SidebarVaultRootIdentity(
      device: identity.device &+ 1, inode: identity.inode)
    let moved = journal.fileURL(for: earlierMount)
    try (try XCTUnwrap(
      SidebarWriteJournal.encode(
        .header(identity: earlierMount, birth: birth, volume: volume)))
      + bytes[(firstNewline + 1)...]).write(to: moved)
    // A journal for the same inode on ANOTHER volume never matches.
    let foreignMount = AppState.SidebarVaultRootIdentity(
      device: identity.device &+ 2, inode: identity.inode)
    let foreign = journal.fileURL(for: foreignMount)
    try (try XCTUnwrap(
      SidebarWriteJournal.encode(
        .header(identity: foreignMount, birth: birth, volume: UUID().uuidString)))
      + bytes[(firstNewline + 1)...]).write(to: foreign)
    try FileManager.default.removeItem(at: original)
    await AppState.simulateRelaunchForTesting()

    let announcer = RecordingAnnouncer()
    let relaunched = try open(vault, announcer: announcer)
    let replayed = await eventually {
      self.pinsOnDisk(vault, folder: "Projects") == ["Projects/note.md"]
    }
    XCTAssertTrue(replayed, "the earlier mount's journal is found and replayed")
    await relaunched.sidebarOrganizationPersistTaskForTesting?.value
    XCTAssertTrue(announcer.messages.contains("Pinned."))
    await journal.waitUntilIdle()
    XCTAssertFalse(
      FileManager.default.fileExists(atPath: moved.path),
      "it moved under this mount's key")
    XCTAssertTrue(
      FileManager.default.fileExists(atPath: foreign.path),
      "another volume's journal is never touched")
  }

  func testAnAgedOutIntentIsSurfacedNotDropped() async throws {
    let clock = Box(Date())
    journal.now = { clock.value }
    let (vault, identity) = try await parkAcknowledgedPins(
      named: "aged", paths: ["Projects/note.md"], folder: "Projects",
      folders: ["Projects"])
    await AppState.simulateRelaunchForTesting()

    // The vault stays closed longer than the journal replays.
    clock.set(clock.value.addingTimeInterval(journal.maxEntryAge + 60))
    let announcer = RecordingAnnouncer()
    let state = try open(vault, announcer: announcer)
    let expected = AppState.sidebarAgedOutChangesReason(
      count: 1, days: 30, acknowledgements: ["Pinned."])
    let surfaced = await eventually { state.sidebarActionBackgroundFailure == expected }
    XCTAssertTrue(surfaced, "an aged-out intent is surfaced through the typed dispatch failure")
    XCTAssertTrue(announcer.messages.contains(expected), "…and spoken")
    await state.sidebarOrganizationPersistTaskForTesting?.value
    XCTAssertNil(pinsOnDisk(vault, folder: "Projects"), "it is not replayed")
    XCTAssertFalse(announcer.messages.contains("Pinned."))
    await journal.waitUntilIdle()
    XCTAssertTrue(
      journalEntries(identity).isEmpty, "once surfaced it is settled — never said twice")
  }

  func testTheTerminationFenceStillSettlesWithinItsBound() async throws {
    let announcer = RecordingAnnouncer()
    let vault = try makeVault(
      named: "fence", files: ["Projects/note.md"], folders: ["Projects"])
    let state = try open(vault, announcer: announcer)
    let identity = try XCTUnwrap(state.sidebarVaultRootIdentity)
    let gate = GateBox()
    // A writer wedged past the deadline (a blocking-flock analog) holds the
    // chain the acknowledged pin waits behind.
    state.enqueueSidebarOrganizationWriteForTesting { _ in gate.wait() }
    try select(state, "Projects/note.md", folder: "Projects")
    _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarPinNote)
    await state.sidebarOrganizationAcknowledgementTaskForTesting?.value
    XCTAssertTrue(AppState.hasPendingSidebarWorkAtTermination)

    let clock = ContinuousClock()
    let start = clock.now
    await AppState.settleSidebarWriterChainsForTermination()
    let elapsed = clock.now - start
    XCTAssertGreaterThan(elapsed, .seconds(4), "the fence waits for the wedged writer")
    XCTAssertLessThan(elapsed, .seconds(20), "…but keeps its five-second bound")
    XCTAssertEqual(
      Array(journalEntries(identity).values),
      [Self.pin("Projects/note.md", in: "Projects")],
      "the bound is safe: the write it gave up on is already durable")

    gate.open()
    await state.sidebarOrganizationPersistTaskForTesting?.value
    XCTAssertEqual(pinsOnDisk(vault, folder: "Projects"), ["Projects/note.md"])
    await journal.waitUntilIdle()
    XCTAssertTrue(journalEntries(identity).isEmpty)
  }

  func testATornTailIsDroppedAndSurfacedAndEarlierRecordsStillReplay() async throws {
    let (vault, identity) = try await parkAcknowledgedPins(
      named: "torn", paths: ["Projects/note.md"], folder: "Projects",
      folders: ["Projects"])
    let url = journal.fileURL(for: identity)
    // A crash mid-append: the last line lost its end and its newline.
    let handle = try FileHandle(forWritingTo: url)
    try handle.seekToEnd()
    try handle.write(contentsOf: Data(#"0badf00d {"op":"put","id":"#.utf8))
    try handle.close()
    XCTAssertEqual(SidebarWriteJournal.entriesOnDiskForTesting(at: url)?.unreadableLines, 1)
    await AppState.simulateRelaunchForTesting()

    let announcer = RecordingAnnouncer()
    let state = try open(vault, announcer: announcer)
    let replayed = await eventually {
      self.pinsOnDisk(vault, folder: "Projects") == ["Projects/note.md"]
    }
    XCTAssertTrue(replayed, "every record before the torn line still replays")
    await state.sidebarOrganizationPersistTaskForTesting?.value
    XCTAssertTrue(
      announcer.messages.contains(AppState.sidebarUnreadableJournalReason),
      "the torn tail is surfaced, not dropped silently")
    XCTAssertTrue(announcer.messages.contains("Pinned."))
    await journal.waitUntilIdle()
    XCTAssertFalse(
      FileManager.default.fileExists(atPath: url.path),
      "rewritten without the damage, then removed once nothing is live")
  }

  func testTheRecordFormatIsChecksummedLineDelimitedAndVersioned() throws {
    // CRC-32 (IEEE) check value.
    XCTAssertEqual(SidebarWriteJournalCRC32.checksum(Array("123456789".utf8)), 0xCBF4_3926)

    let identity = AppState.SidebarVaultRootIdentity(device: 7, inode: 99)
    let entry = SidebarWriteJournal.Entry(
      id: UUID(), position: 0, recordedAt: Date(timeIntervalSince1970: 1_700_000_000),
      content: .intent(
        .setSort(container: "Projects", sort: .init(field: .modified, direction: .desc)),
        acknowledgement: "Sorted by modified, newest first."))
    let header = try XCTUnwrap(SidebarWriteJournal.encode(.header(identity: identity, birth: 5)))
    let put = try XCTUnwrap(SidebarWriteJournal.encode(.put(entry)))
    // `<8 hex> <json>\n`, one line per record.
    let text = try XCTUnwrap(String(data: put, encoding: .utf8))
    XCTAssertTrue(text.hasSuffix("\n"))
    XCTAssertEqual(text.dropLast().filter { $0 == "\n" }.count, 0)
    XCTAssertEqual(text.split(separator: " ", maxSplits: 1).first?.count, 8)

    // A damaged line in the middle is dropped and counted; its neighbours stay.
    var damaged = put
    damaged[20] ^= 0x01
    let settle = try XCTUnwrap(SidebarWriteJournal.encode(.settle(ids: [UUID()])))
    let parsed = SidebarWriteJournal.parse(header + damaged + put + settle)
    XCTAssertEqual(parsed.header?.version, SidebarWriteJournal.formatVersion)
    XCTAssertEqual(parsed.unreadableLines, 1)
    XCTAssertEqual(parsed.lines.map(\.op), [.put, .settle])
    XCTAssertEqual(parsed.lines.first?.content, entry.content)

    // A damaged line ahead of the header is skipped like any other: the
    // first line that verifies is the header, and the entries still load.
    let skipped = SidebarWriteJournal.parse(damaged + header + put)
    XCTAssertEqual(skipped.header?.op, .header)
    XCTAssertEqual(skipped.unreadableLines, 1)
    XCTAssertEqual(skipped.lines.map(\.op), [.put])
    // …but a file whose first verified line is not a header has none.
    XCTAssertNil(SidebarWriteJournal.parse(put + header).header)

    // A newer format is never replayed and never touched.
    let vaultRoot = root.appendingPathComponent("newer", isDirectory: true)
    try FileManager.default.createDirectory(at: vaultRoot, withIntermediateDirectories: true)
    var newer = SidebarWriteJournal.Line.header(identity: identity, birth: nil)
    newer.version = SidebarWriteJournal.formatVersion + 1
    let newerFile = try XCTUnwrap(SidebarWriteJournal.encode(newer)) + put
    try FileManager.default.createDirectory(
      at: journal.directory, withIntermediateDirectories: true)
    try newerFile.write(to: journal.fileURL(for: identity))
    journal.open(identity, vaultRoot: vaultRoot)
    XCTAssertEqual(journal.liveEntryCount(for: identity), 0)
    XCTAssertNil(
      journal.appendIntent(
        .pin(path: "a.md", folder: ""), acknowledgement: "Pinned.", for: identity,
        vaultRoot: vaultRoot),
      "a newer file is never appended to")
    XCTAssertEqual(try Data(contentsOf: journal.fileURL(for: identity)), newerFile)
  }

  func testSettledLinesAreReclaimedAndOnlyLiveEntriesStay() async throws {
    let vaultRoot = root.appendingPathComponent("compaction", isDirectory: true)
    try FileManager.default.createDirectory(at: vaultRoot, withIntermediateDirectories: true)
    let identity = AppState.SidebarVaultRootIdentity(device: 11, inode: 1_234)
    let padding = String(repeating: "p", count: 1_000)
    var ids: [UUID] = []
    for index in 0..<300 {
      let append = try XCTUnwrap(
        journal.appendIntent(
          .pin(path: "n\(index).md", folder: ""), acknowledgement: padding,
          for: identity, vaultRoot: vaultRoot))
      ids.append(append.id)
    }
    await journal.waitUntilIdle()
    let url = journal.fileURL(for: identity)
    let grown = try XCTUnwrap(
      try FileManager.default.attributesOfItem(atPath: url.path)[.size] as? Int)
    XCTAssertGreaterThan(grown, SidebarWriteJournal.compactionThresholdBytes)

    // Settling all but the last makes the file mostly dead lines: it is
    // rewritten with the header and the one live entry.
    journal.settle(ids.dropLast(), for: identity)
    await journal.waitUntilIdle()
    let compacted = try XCTUnwrap(
      try FileManager.default.attributesOfItem(atPath: url.path)[.size] as? Int)
    XCTAssertLessThan(compacted, 4 * 1_024, "settled lines are reclaimed")
    let onDisk = try XCTUnwrap(SidebarWriteJournal.entriesOnDiskForTesting(at: url))
    XCTAssertEqual(Array(onDisk.entries.keys), [ids[ids.count - 1]])
    XCTAssertEqual(onDisk.unreadableLines, 0)
    XCTAssertEqual(journal.liveEntryCount(for: identity), 1)

    journal.settle([ids[ids.count - 1]], for: identity)
    await journal.waitUntilIdle()
    XCTAssertFalse(
      FileManager.default.fileExists(atPath: url.path), "an all-settled journal is removed")
  }

  func testARenameThatCommitsAfterItsVaultClosedReplaysOnTheNextLaunch() async throws {
    let vault = try makeVault(
      named: "late-rename", files: ["Projects/note.md"], folders: ["Projects"])
    let identity: AppState.SidebarVaultRootIdentity
    do {
      let state = try open(vault, announcer: RecordingAnnouncer())
      identity = try XCTUnwrap(state.sidebarVaultRootIdentity)
      try select(state, "Projects/note.md", folder: "Projects")
      _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarPinNote)
      await state.sidebarOrganizationPersistTaskForTesting?.value
      XCTAssertEqual(pinsOnDisk(vault, folder: "Projects"), ["Projects/note.md"])

      // The native rename commits on its worker only after the vault closed,
      // so its landing never publishes the sidebar transform (round 32 (1)).
      let gate = GateBox()
      let entered = Box(false)
      state.structuralRenameRunner = { _, path, _, newName in
        entered.set(true)
        await Task.detached { gate.wait() }.value
        try FileManager.default.moveItem(
          at: vault.appendingPathComponent(path),
          to: vault.appendingPathComponent("Projects/\(newName)"))
        return StructuralReport(opId: 1, undoOpIds: [1], moved: [], rewritten: [], failed: [])
      }
      let rename = try XCTUnwrap(
        state.renameEntry(path: "Projects/note.md", isDirectory: false, to: "renamed.md"))
      let started = await eventually { entered.value }
      XCTAssertTrue(started)
      state.closeVault()
      gate.open()
      await rename.value
      XCTAssertEqual(
        pinsOnDisk(vault, folder: "Projects"), ["Projects/note.md"],
        "nothing followed the rename in this launch")
    }
    await AppState.simulateRelaunchForTesting()

    let relaunched = try open(vault, announcer: RecordingAnnouncer())
    let followed = await eventually {
      self.pinsOnDisk(vault, folder: "Projects") == ["Projects/renamed.md"]
    }
    XCTAssertTrue(
      followed, "the transform journaled before the rename committed replays on relaunch")
    await relaunched.sidebarOrganizationPersistTaskForTesting?.value
    await journal.waitUntilIdle()
    XCTAssertTrue(journalEntries(identity).isEmpty)
  }

  func testAPendingRenameThatNeverCommittedIsNotReplayed() async throws {
    // A crash between the journal line and the native rename: the entry is
    // still pending, the source still exists, so the replay verifies and
    // skips it — the pin stays where the file is.
    let vault = try makeVault(
      named: "never-committed", files: ["Projects/note.md"], folders: ["Projects"])
    let identity: AppState.SidebarVaultRootIdentity
    do {
      let state = try open(vault, announcer: RecordingAnnouncer())
      identity = try XCTUnwrap(state.sidebarVaultRootIdentity)
      try select(state, "Projects/note.md", folder: "Projects")
      _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarPinNote)
      await state.sidebarOrganizationPersistTaskForTesting?.value
      let pending = SidebarStructuralTransform(
        renames: [
          .init(
            oldPath: "Projects/note.md", newPath: "Projects/renamed.md", isDirectory: false)
        ])
      let append = try XCTUnwrap(
        journal.appendPendingStructural(pending, for: identity, vaultRoot: vault))
      _ = await append.durable.value
      state.closeVault()
    }
    await AppState.simulateRelaunchForTesting()

    let relaunched = try open(vault, announcer: RecordingAnnouncer())
    await relaunched.sidebarOrganizationPersistTaskForTesting?.value
    await journal.waitUntilIdle()
    XCTAssertEqual(pinsOnDisk(vault, folder: "Projects"), ["Projects/note.md"])
    XCTAssertTrue(journalEntries(identity).isEmpty, "the verified-away entry is settled")
  }

  func testAReplayRefusedWhenItLandsIsSurfacedAndTheRestStillLand() async throws {
    let sortAcknowledgement = SidebarOrganizationChoice(
      sort: .init(field: .created, direction: .asc), grouping: .none
    ).sortAnnouncement
    let (vault, identity) = try await parkAcknowledged(
      named: "refused-on-replay", files: ["Projects/note.md"], folders: ["Projects"],
      acknowledgements: [sortAcknowledgement, "Pinned."]
    ) { state in
      try self.selectNothing(state)
      _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarSortCreatedAsc)
      await state.sidebarOrganizationAcknowledgementTaskForTesting?.value
      try self.select(state, "Projects/note.md", folder: "Projects")
      _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarPinNote)
      await state.sidebarOrganizationAcknowledgementTaskForTesting?.value
    }
    // While Slate was closed the vault gained date grouping: the parked
    // oldest-first sort can no longer take effect.
    try writeSidebarFile(vault, #"{"version": 1, "grouping": "dateBuckets"}"#)
    await AppState.simulateRelaunchForTesting()

    let announcer = RecordingAnnouncer()
    let state = try open(vault, announcer: announcer)
    let landed = await eventually {
      self.pinsOnDisk(vault, folder: "Projects") == ["Projects/note.md"]
    }
    XCTAssertTrue(landed, "the pin parked behind the refused sort still lands")
    await state.sidebarOrganizationPersistTaskForTesting?.value
    let expected = AppState.sidebarRefusedReplayReason(
      AppState.sidebarGroupedSortInertReason)
    XCTAssertEqual(state.sidebarActionBackgroundFailure, expected)
    XCTAssertTrue(announcer.messages.contains(expected), "the refusal is spoken, not dropped")
    XCTAssertTrue(announcer.messages.contains("Pinned."), "the replayed pin speaks")
    XCTAssertFalse(announcer.messages.contains(sortAcknowledgement))
    XCTAssertNil(sidebarFile(vault)?["sort"], "the refused sort is not applied")
    await journal.waitUntilIdle()
    XCTAssertTrue(journalEntries(identity).isEmpty, "both entries settled")
  }

  func testARefusalNobodyHeardIsSurfacedAtTheNextOpen() async throws {
    let vault = try makeVault(named: "unheard-refusal", files: ["note.md"])
    let identity: AppState.SidebarVaultRootIdentity
    do {
      let state = try open(vault, announcer: RecordingAnnouncer())
      identity = try XCTUnwrap(state.sidebarVaultRootIdentity)
      let gate = GateBox()
      state.enqueueSidebarOrganizationWriteForTesting { _ in gate.wait() }
      try selectNothing(state)
      _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarSortCreatedAsc)
      await state.sidebarOrganizationAcknowledgementTaskForTesting?.value
      let queued = state.sidebarOrganizationPersistTaskForTesting
      state.closeVault()
      // Another device turned on date grouping before the queued sort
      // reached the file: its first attempt is refused with nobody to tell.
      try writeSidebarFile(vault, #"{"version": 1, "grouping": "dateBuckets"}"#)
      gate.open()
      await queued?.value
    }
    XCTAssertEqual(journalEntries(identity).count, 1, "the refusal is kept, not dropped")
    await AppState.simulateRelaunchForTesting()

    let announcer = RecordingAnnouncer()
    let state = try open(vault, announcer: announcer)
    let expected = AppState.sidebarRefusedReplayReason(
      AppState.sidebarGroupedSortInertReason)
    let surfaced = await eventually { state.sidebarActionBackgroundFailure == expected }
    XCTAssertTrue(surfaced, "the next open says why the change did not happen")
    XCTAssertTrue(announcer.messages.contains(expected))
    await state.sidebarOrganizationPersistTaskForTesting?.value
    XCTAssertNil(sidebarFile(vault)?["sort"])
    await journal.waitUntilIdle()
    XCTAssertTrue(journalEntries(identity).isEmpty, "once said, it is settled")
  }

  func testAPendingBatchReplaysOnlyTheRenamesThatStood() async throws {
    // A crash after one of two renames committed: the entry is pending, so
    // each rename is verified against the disk on its own.
    let vault = try makeVault(named: "partial-batch", files: ["a.md", "b.md"])
    let identity: AppState.SidebarVaultRootIdentity
    do {
      let state = try open(vault, announcer: RecordingAnnouncer())
      identity = try XCTUnwrap(state.sidebarVaultRootIdentity)
      for path in ["a.md", "b.md"] {
        try select(state, path, folder: "")
        _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarPinNote)
        await state.sidebarOrganizationPersistTaskForTesting?.value
      }
      let pending = SidebarStructuralTransform(
        renames: [
          .init(oldPath: "a.md", newPath: "a2.md", isDirectory: false),
          .init(oldPath: "b.md", newPath: "b2.md", isDirectory: false),
        ])
      let append = try XCTUnwrap(
        journal.appendPendingStructural(pending, for: identity, vaultRoot: vault))
      _ = await append.durable.value
      state.closeVault()
    }
    try FileManager.default.moveItem(
      at: vault.appendingPathComponent("a.md"), to: vault.appendingPathComponent("a2.md"))
    await AppState.simulateRelaunchForTesting()

    let relaunched = try open(vault, announcer: RecordingAnnouncer())
    let replayed = await eventually { self.pinsOnDisk(vault, folder: "") == ["a2.md", "b.md"] }
    XCTAssertTrue(replayed, "only the rename whose source is gone replays")
    await relaunched.sidebarOrganizationPersistTaskForTesting?.value
    await journal.waitUntilIdle()
    XCTAssertTrue(journalEntries(identity).isEmpty)
  }

  func testAFolderRenameWithItsNoteSettlesItsEntryWhenTheFixUpCommits() async throws {
    let announcer = RecordingAnnouncer()
    let vault = try makeVault(
      named: "folder-note", files: ["Projects/Projects.md", "Projects/plan.md"],
      folders: ["Projects"])
    let state = try open(vault, announcer: announcer)
    let identity = try XCTUnwrap(state.sidebarVaultRootIdentity)
    try select(state, "Projects/Projects.md", folder: "Projects")
    _ = try state.dispatchSidebarAction(id: SlateCommandID.sidebarPinNote)
    await state.sidebarOrganizationPersistTaskForTesting?.value

    // The compound rename moves the folder and renames its note to the
    // new stem: two transforms, and the journal entry rides the second.
    let rename = try XCTUnwrap(
      state.renameEntry(path: "Projects", isDirectory: true, to: "Work"))
    await rename.value
    await state.sidebarOrganizationPersistTaskForTesting?.value
    XCTAssertEqual(pinsOnDisk(vault, folder: "Work"), ["Work/Work.md"])
    XCTAssertNil(pinsOnDisk(vault, folder: "Projects"))
    // The rename's own sentence is the last thing said (its sidebar writes
    // are silent), and the pin is not spoken again.
    XCTAssertEqual(
      state.lastMutationAnnouncement.map { $0.hasPrefix("Renamed Projects to Work") },
      true)
    XCTAssertEqual(announcer.messages.filter { $0 == "Pinned." }.count, 1)
    await journal.waitUntilIdle()
    XCTAssertTrue(
      journalEntries(identity).isEmpty, "the entry settled with the fix-up's commit")
  }

  /// The PR's timing figures: what the write-ahead fsync costs a rename,
  /// and that it never runs on the main thread. The bound is generous; the
  /// numbers are printed for the record.
  func testTheRenameWriteAheadCostIsMeasuredOffTheMainThread() async throws {
    let count = 20
    let vault = try makeVault(
      named: "timing", files: (0..<count).map { "n\($0).md" })
    let state = try open(vault, announcer: RecordingAnnouncer())
    let onMain = Box(false)
    journal.io.setBeforeSyncHookForTesting { _ in
      if Thread.isMainThread { onMain.set(true) }
    }
    let clock = ContinuousClock()
    var renames: [Duration] = []
    for index in 0..<count {
      let started = clock.now
      let task = try XCTUnwrap(
        state.renameEntry(path: "n\(index).md", isDirectory: false, to: "m\(index).md"))
      await task.value
      renames.append(clock.now - started)
    }
    await state.sidebarOrganizationPersistTaskForTesting?.value
    await journal.waitUntilIdle()
    let syncs = journal.io.syncDurationsForTesting.sorted()
    XCTAssertGreaterThanOrEqual(syncs.count, count, "one durable line per rename")
    XCTAssertFalse(onMain.value, "no journal fsync ran on the main thread")
    guard !syncs.isEmpty, !renames.isEmpty else { return }
    func ms(_ duration: Duration) -> Double {
      Double(duration.components.seconds) * 1_000
        + Double(duration.components.attoseconds) / 1e15
    }
    let p50 = syncs[syncs.count / 2]
    let p95 = syncs[min(syncs.count - 1, syncs.count * 95 / 100)]
    let renameMedian = renames.sorted()[renames.count / 2]
    print(
      String(
        format: "PR23-TIMING fsync n=%d p50=%.3fms p95=%.3fms max=%.3fms rename-median=%.3fms",
        syncs.count, ms(p50), ms(p95), ms(syncs.last ?? .zero), ms(renameMedian)))
    XCTAssertLessThan(p95, .milliseconds(250))
  }
}
