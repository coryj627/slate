// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Darwin
import Foundation

/// PR 23 (#944, FL-06 rounds 31 and 32): the durable half of the sidebar's
/// retained-write registry.
///
/// An acknowledged organization write ("Pinned.") is a typed
/// `SidebarWriteIntent`; a rename or move carries a
/// `SidebarStructuralTransform`. Both are recorded here write-ahead — before
/// the acknowledgement is spoken, and before the filesystem operation can
/// commit — in one append-only file per vault identity under Application
/// Support. Never inside the vault: its filesystem is what failed. An entry
/// is settled only after its change commits to `.slate/sidebar.json`, after
/// its failure was told to the user, or after it was surfaced as refused or
/// too old. Until then a quit, a crash or a closed vault leaves it on disk,
/// and the next open of the same vault adopts it into the registry and
/// replays it through the chained drain.
///
/// Identity. The file is named for the vault root's device and inode, and
/// its header also records the root's birth time and its volume's UUID: a
/// replacement vault at the same path has another inode and never finds the
/// file, and a replacement that reuses the inode is told apart by its birth
/// time. A volume mounted again under another device number (an external
/// or network drive) finds its file by volume UUID, inode and birth time,
/// and the file moves under the new key.
///
/// Record format, version 1. Every line is `<crc32> <json>\n` — eight
/// lowercase hex digits of the CRC-32 (IEEE) of the JSON bytes, one space,
/// one JSON object (JSON never holds a raw newline). The first line is the
/// header (format name, version, device, inode, birth time, volume). A file with a
/// newer version is left untouched and never appended to. Later lines are
/// `put` (an entry, or a structural entry's committed form, which takes its
/// place in the replay order), `refuse` (a final refusal nobody heard) and
/// `settle` (tombstones). A line that fails its checksum or lacks its
/// newline — the torn tail a crash mid-append leaves — is dropped and
/// counted; every other line still replays, and the file is rewritten
/// without the damage before the next append.
///
/// Durability. A `put` of an intent or of a pending structural transform is
/// fsync'd on the journal's serial queue — never on the main actor — and
/// the caller waits for it before it acknowledges (an intent) or lets the
/// filesystem operation start (a structural transform). Committed puts,
/// refusals and settles are written without a sync: losing one replays an
/// entry once more, and replays are idempotent. A file whose entries are all
/// settled is removed.
@MainActor
final class SidebarWriteJournal {
  typealias Identity = SidebarVaultPrefsStore.RootIdentity

  nonisolated static let formatName = "slate-sidebar-write-journal"
  nonisolated static let formatVersion = 1
  /// Bounded read at open: a legitimate journal holds a few entries.
  nonisolated static let maxReadBytes = 4 * 1_024 * 1_024
  /// A file grown past this with settled lines is rewritten live-only.
  nonisolated static let compactionThresholdBytes = 256 * 1_024

  /// One live entry.
  struct Entry: Equatable, Sendable {
    enum Content: Codable, Equatable, Sendable {
      /// An acknowledged write and the sentence that acknowledged it (spoken
      /// again when its replay commits).
      case intent(SidebarWriteIntent, acknowledgement: String?)
      /// A rename or move. `committed` is false while the filesystem
      /// operation was not yet known to have committed.
      case structural(SidebarStructuralTransform, committed: Bool)
    }

    let id: UUID
    /// Replay order: file order for entries read at open, then append
    /// order. A structural entry moves to where its committed form landed.
    fileprivate(set) var position: UInt64
    let recordedAt: Date
    fileprivate(set) var content: Content
    /// A final refusal nobody was left to hear; surfaced at the next open.
    fileprivate(set) var refusal: String?

    init(
      id: UUID, position: UInt64, recordedAt: Date, content: Content,
      refusal: String? = nil
    ) {
      self.id = id
      self.position = position
      self.recordedAt = recordedAt
      self.content = content
      self.refusal = refusal
    }

    var acknowledgement: String? {
      if case .intent(_, let acknowledgement) = content { return acknowledgement }
      return nil
    }
  }

  /// Something the next same-vault open must tell the user.
  enum Notice: Equatable, Sendable {
    /// Lines that failed their checksum or were torn by a crash, or a file
    /// this build cannot read (a damaged header, a newer format).
    case unreadable
    /// The file belongs to a vault that is gone: same device and inode,
    /// another birth time.
    case replacedVault
  }

  /// What a drain takes from the journal at run time.
  struct Adoption {
    var entries: [Entry] = []
    var agedOut: [Entry] = []
    var refused: [Entry] = []
    var notices: [Notice] = []
  }

  /// An appended entry: its id, its replay position, and a task that
  /// finishes once the line is written and fsync'd. False means it could
  /// not be, and the write proceeds without its durable record.
  struct Append {
    let id: UUID
    let position: UInt64
    let durable: Task<Bool, Never>
  }

  private struct VaultLog {
    var entries: [UUID: Entry] = [:]
    /// Entries read from the file at open and not yet adopted: the work of
    /// an earlier launch, which the capacity gate counts.
    var awaitingAdoption: Set<UUID> = []
    var notices: [Notice] = []
    var header = Data()
    var approximateBytes = 0
    /// A newer-format file this build must not touch.
    var isReadOnly = false
  }

  let directory: URL
  /// Test seam: the clock entries are stamped with and aged against.
  var now: () -> Date = { Date() }
  /// An entry older than this when a drain would adopt it is surfaced, not
  /// replayed: the vault has had a month of other changes since.
  var maxEntryAge: TimeInterval = 30 * 24 * 60 * 60

  private var logs: [Identity: VaultLog] = [:]
  private var nextPosition: UInt64 = 0
  let io: SidebarWriteJournalIO

  init(directory: URL, io: SidebarWriteJournalIO = SidebarWriteJournalIO()) {
    self.directory = directory
    self.io = io
  }

  /// `~/Library/Application Support/Slate/sidebar-write-journal`.
  nonisolated static func applicationSupportDirectory() -> URL? {
    guard
      let base = try? FileManager.default.url(
        for: .applicationSupportDirectory, in: .userDomainMask,
        appropriateFor: nil, create: true)
    else { return nil }
    return base.appendingPathComponent("Slate", isDirectory: true)
      .appendingPathComponent("sidebar-write-journal", isDirectory: true)
  }

  /// A private directory for a process that never installed the production
  /// journal (XCTest, previews), so none of them touches the user's files.
  nonisolated static func temporaryDirectory() -> URL {
    FileManager.default.temporaryDirectory.appendingPathComponent(
      "slate-sidebar-write-journal-\(ProcessInfo.processInfo.processIdentifier)"
        + "-\(UUID().uuidString)",
      isDirectory: true)
  }

  func fileURL(for identity: Identity) -> URL {
    directory.appendingPathComponent(
      "\(identity.device)-\(identity.inode).journal", isDirectory: false)
  }

  /// Lines still queued for disk — the quit fence waits for them.
  var hasPendingIO: Bool { io.hasPendingWork }

  func waitUntilIdle() async { await io.waitUntilIdle() }

  // MARK: - Open

  /// Read the identity's file once per process — bounded and synchronous,
  /// the budget the store's own admission read has — and keep it only if
  /// it belongs to THIS vault.
  func open(_ identity: Identity, vaultRoot: URL) {
    guard logs[identity] == nil else { return }
    let fingerprint = Self.fingerprint(of: vaultRoot, expecting: identity)
    var log = VaultLog()
    log.header =
      Self.encode(
        Line.header(
          identity: identity, birth: fingerprint.birth, volume: fingerprint.volume))
      ?? Data()
    let url = fileURL(for: identity)
    defer { logs[identity] = log }

    var data: Data
    var relocatedFrom: URL?
    switch Self.readBounded(url) {
    case .missing:
      // A volume mounted again (an external or network drive) can come
      // back under another device number. Its journal is found by what
      // does not change — the volume's UUID, the root's inode and its birth
      // time — and moved under the new key.
      guard
        let relocated = relocatedJournal(
          inode: identity.inode, fingerprint: fingerprint, excluding: url)
      else { return }
      data = relocated.data
      relocatedFrom = relocated.url
    case .unreadable:
      log.notices.append(.unreadable)
      io.enqueue { _ in SidebarWriteJournalIO.quarantine(url) }
      return
    case .data(let bytes):
      // A crash between creating the file and its first write leaves it
      // empty; the next append writes the header into it.
      guard !bytes.isEmpty else { return }
      data = bytes
    }
    let parsed = Self.parse(data)
    guard let header = parsed.header,
      header.format == Self.formatName,
      let version = header.version
    else {
      log.notices.append(.unreadable)
      io.enqueue { _ in SidebarWriteJournalIO.quarantine(url) }
      return
    }
    guard version <= Self.formatVersion else {
      // A newer Slate wrote this; it can still replay it. Never touch it.
      log.isReadOnly = true
      log.notices.append(.unreadable)
      return
    }
    guard relocatedFrom != nil || header.device == String(identity.device),
      header.inode == String(identity.inode)
    else {
      log.notices.append(.unreadable)
      io.enqueue { _ in SidebarWriteJournalIO.quarantine(url) }
      return
    }
    if let birth = fingerprint.birth, let recorded = header.birth,
      recorded != String(birth)
    {
      // The inode was reused by a replacement vault: these entries belong
      // to a vault that no longer exists and can never replay.
      log.notices.append(.replacedVault)
      io.enqueue { _ in SidebarWriteJournalIO.remove(url) }
      return
    }
    if relocatedFrom != nil {
      // Rewritten below under this mount's key (forcing the rewrite).
      data = Data()
    }

    var unreadable = parsed.unreadableLines
    for line in parsed.lines {
      switch line.op {
      case .put:
        guard let id = line.id, let content = line.content, let at = line.at else {
          unreadable += 1
          continue
        }
        let existing = log.entries[id]
        log.entries[id] = Entry(
          id: id, position: takePosition(),
          recordedAt: existing?.recordedAt ?? Date(milliseconds: at),
          content: content, refusal: existing?.refusal)
      case .refuse:
        guard let id = line.id, let reason = line.reason else {
          unreadable += 1
          continue
        }
        log.entries[id]?.refusal = reason
      case .settle:
        guard let ids = line.ids else {
          unreadable += 1
          continue
        }
        for id in ids { log.entries[id] = nil }
      case .header:
        unreadable += 1
      }
    }
    if unreadable > 0 { log.notices.append(.unreadable) }
    log.awaitingAdoption = Set(log.entries.keys)
    let live = Self.liveLines(log)
    log.approximateBytes = live.count
    if log.entries.isEmpty {
      io.enqueue { _ in SidebarWriteJournalIO.remove(url) }
    } else if unreadable > 0 || live.count != data.count {
      // Drop the damage and the settled lines before the next append, so a
      // torn tail never fuses with the line written after it.
      io.enqueue { _ in SidebarWriteJournalIO.rewrite(live, at: url) }
    }
    if let relocatedFrom {
      // Only once the new key's file is durable does the old one go.
      io.enqueue { _ in SidebarWriteJournalIO.remove(relocatedFrom) }
    }
  }

  /// The journal this vault wrote while its volume had another device
  /// number: same volume UUID, inode and birth time. Nil when any of the
  /// three is unknown — then only the device-and-inode key can match.
  private func relocatedJournal(
    inode: UInt64, fingerprint: (birth: Int64?, volume: String?), excluding url: URL
  ) -> (url: URL, data: Data)? {
    guard let birth = fingerprint.birth, let volume = fingerprint.volume,
      let names = try? FileManager.default.contentsOfDirectory(atPath: directory.path)
    else { return nil }
    let suffix = "-\(inode).journal"
    for name in names where name.hasSuffix(suffix) && name != url.lastPathComponent {
      let candidate = directory.appendingPathComponent(name, isDirectory: false)
      guard case .data(let data) = Self.readBounded(candidate),
        let header = Self.parse(data).header,
        header.format == Self.formatName,
        header.inode == String(inode),
        header.volume == volume,
        header.birth == String(birth)
      else { continue }
      return (candidate, data)
    }
    return nil
  }

  // MARK: - Writes

  /// Journal an acknowledged write before its acknowledgement.
  func appendIntent(
    _ intent: SidebarWriteIntent, acknowledgement: String?,
    for identity: Identity, vaultRoot: URL
  ) -> Append? {
    let entry = Entry(
      id: UUID(), position: takePosition(), recordedAt: now(),
      content: .intent(intent, acknowledgement: acknowledgement), refusal: nil)
    return append(entry, for: identity, vaultRoot: vaultRoot)
  }

  /// Journal a rename's or move's anticipated transform before the
  /// filesystem operation can commit (round 32 (1)). The entry's id is the
  /// transform's.
  func appendPendingStructural(
    _ transform: SidebarStructuralTransform,
    for identity: Identity, vaultRoot: URL
  ) -> Append? {
    let entry = Entry(
      id: transform.id, position: takePosition(), recordedAt: now(),
      content: .structural(transform, committed: false), refusal: nil)
    return append(entry, for: identity, vaultRoot: vaultRoot)
  }

  /// The operation finished: record what actually changed — it may be a
  /// subset of the anticipated transform — so its replay needs no
  /// verification, or settle the entry when nothing changed.
  func resolveStructural(
    id: UUID, committed transform: SidebarStructuralTransform,
    for identity: Identity
  ) {
    guard transform.isEmpty == false else {
      settle([id], for: identity)
      return
    }
    guard var log = logs[identity], var entry = log.entries[id], !log.isReadOnly
    else { return }
    var committed = transform
    committed.id = id
    entry.content = .structural(committed, committed: true)
    entry.position = takePosition()
    log.entries[id] = entry
    write(.put(entry), to: &log, identity: identity, sync: false)
    logs[identity] = log
  }

  /// A final refusal that landed after its session was torn down: kept so
  /// the next open can say why the change did not happen.
  func refuse(_ id: UUID, reason: String, for identity: Identity) {
    guard var log = logs[identity], log.entries[id] != nil, !log.isReadOnly
    else { return }
    log.entries[id]?.refusal = reason
    write(.refuse(id: id, reason: reason), to: &log, identity: identity, sync: false)
    logs[identity] = log
  }

  /// Tombstone entries whose change committed, or whose outcome reached the
  /// user. Removes the file once nothing is live.
  func settle(_ ids: some Sequence<UUID>, for identity: Identity) {
    guard var log = logs[identity], !log.isReadOnly else { return }
    let live = Set(ids).filter { log.entries[$0] != nil }
    guard !live.isEmpty else { return }
    for id in live {
      log.entries[id] = nil
      log.awaitingAdoption.remove(id)
    }
    let url = fileURL(for: identity)
    if log.entries.isEmpty {
      log.approximateBytes = 0
      io.enqueue { _ in SidebarWriteJournalIO.remove(url) }
    } else {
      write(
        .settle(ids: live.sorted { $0.uuidString < $1.uuidString }),
        to: &log, identity: identity, sync: false)
      if log.approximateBytes > Self.compactionThresholdBytes {
        let compacted = Self.liveLines(log)
        log.approximateBytes = compacted.count
        io.enqueue { _ in SidebarWriteJournalIO.rewrite(compacted, at: url) }
      }
    }
    logs[identity] = log
  }

  /// A replay position for a registry entry that is not journaled.
  func reservePosition() -> UInt64 { takePosition() }

  // MARK: - Reads

  /// Entries an earlier launch left that no drain has adopted yet.
  func awaitingAdoptionCount(for identity: Identity) -> Int {
    logs[identity]?.awaitingAdoption.count ?? 0
  }

  func liveEntryCount(for identity: Identity) -> Int {
    logs[identity]?.entries.count ?? 0
  }

  func entry(_ id: UUID, for identity: Identity) -> Entry? {
    logs[identity]?.entries[id]
  }

  /// True when a drain has something to adopt or to say.
  func needsDrain(for identity: Identity, excluding held: Set<UUID>) -> Bool {
    guard let log = logs[identity] else { return false }
    return !log.notices.isEmpty
      || log.entries.keys.contains { !held.contains($0) }
  }

  /// The drain's run-time adoption: every live entry no in-memory owner
  /// holds and recorded before the drain was scheduled, in replay order.
  /// Refused and aged-out entries are settled here and returned to be
  /// surfaced; the rest move into the registry. With nobody to tell
  /// (`surfacing` false), refused and aged-out entries and the notices
  /// stay for the next open, and only replayable entries are adopted.
  func adopt(
    for identity: Identity, excluding held: Set<UUID>, before bound: UInt64,
    surfacing: Bool
  ) -> Adoption {
    guard var log = logs[identity] else { return Adoption() }
    var adoption = Adoption()
    if surfacing {
      adoption.notices = log.notices
      log.notices = []
    }
    let current = now()
    let candidates = log.entries.values
      .filter { !held.contains($0.id) && $0.position < bound }
      .sorted { $0.position < $1.position }
    for entry in candidates {
      if entry.refusal != nil {
        guard surfacing else { continue }
        adoption.refused.append(entry)
      } else if current.timeIntervalSince(entry.recordedAt) > maxEntryAge {
        guard surfacing else { continue }
        adoption.agedOut.append(entry)
      } else {
        adoption.entries.append(entry)
      }
      log.awaitingAdoption.remove(entry.id)
    }
    logs[identity] = log
    settle(
      adoption.refused.map(\.id) + adoption.agedOut.map(\.id), for: identity)
    return adoption
  }

  // MARK: - Test seams

  /// Simulate a new process: wait for queued lines, then forget everything
  /// read or written, so the next open reads the files fresh.
  func forgetLoadedStateForTesting() async {
    await io.waitUntilIdle()
    logs = [:]
  }

  /// Parse a journal file straight from disk.
  nonisolated static func entriesOnDiskForTesting(
    at url: URL
  ) -> (entries: [UUID: Entry.Content], unreadableLines: Int)? {
    guard case .data(let data) = readBounded(url) else { return nil }
    let parsed = parse(data)
    guard parsed.header != nil else { return nil }
    var entries: [UUID: Entry.Content] = [:]
    for line in parsed.lines {
      switch line.op {
      case .put:
        if let id = line.id, let content = line.content { entries[id] = content }
      case .settle:
        for id in line.ids ?? [] { entries[id] = nil }
      case .refuse, .header:
        break
      }
    }
    return (entries, parsed.unreadableLines)
  }

  // MARK: - Internals

  private func takePosition() -> UInt64 {
    defer { nextPosition &+= 1 }
    return nextPosition
  }

  private func append(_ entry: Entry, for identity: Identity, vaultRoot: URL) -> Append? {
    open(identity, vaultRoot: vaultRoot)
    guard var log = logs[identity], !log.isReadOnly,
      let line = Self.encode(.put(entry))
    else { return nil }
    log.entries[entry.id] = entry
    log.approximateBytes += line.count
    let header = log.header
    logs[identity] = log
    let url = fileURL(for: identity)
    let durable = io.submit { io in
      io.appendLine(line, to: url, header: header, sync: true)
    }
    return Append(id: entry.id, position: entry.position, durable: durable)
  }

  private func write(_ line: Line, to log: inout VaultLog, identity: Identity, sync: Bool) {
    guard let bytes = Self.encode(line) else { return }
    log.approximateBytes += bytes.count
    let header = log.header
    let url = fileURL(for: identity)
    io.enqueue { io in
      _ = io.appendLine(bytes, to: url, header: header, sync: sync)
    }
  }

  private static func liveLines(_ log: VaultLog) -> Data {
    var data = log.header
    for entry in log.entries.values.sorted(by: { $0.position < $1.position }) {
      if let line = encode(.put(entry)) { data.append(line) }
      if let reason = entry.refusal, let line = encode(.refuse(id: entry.id, reason: reason)) {
        data.append(line)
      }
    }
    return data
  }

  /// What identifies the root beyond its device and inode: its birth time
  /// in nanoseconds and its volume's UUID — read only while the path still
  /// resolves to the admitted identity, nil where unknown.
  nonisolated static func fingerprint(
    of vaultRoot: URL, expecting identity: Identity
  ) -> (birth: Int64?, volume: String?) {
    var info = stat()
    guard vaultRoot.path.withCString({ stat($0, &info) }) == 0,
      UInt64(info.st_dev) == identity.device,
      UInt64(info.st_ino) == identity.inode
    else { return (nil, nil) }
    let birth = info.st_birthtimespec
    let volume = try? vaultRoot.resourceValues(forKeys: [.volumeUUIDStringKey])
      .volumeUUIDString
    return (Int64(birth.tv_sec) * 1_000_000_000 + Int64(birth.tv_nsec), volume)
  }

  // MARK: Lines

  struct Line: Codable, Equatable {
    enum Op: String, Codable {
      case header
      case put
      case refuse
      case settle
    }

    var op: Op
    var format: String?
    var version: Int?
    var device: String?
    var inode: String?
    var birth: String?
    var volume: String?
    var id: UUID?
    var at: Int64?
    var content: Entry.Content?
    var reason: String?
    var ids: [UUID]?

    static func header(identity: Identity, birth: Int64?, volume: String? = nil) -> Line {
      Line(
        op: .header, format: SidebarWriteJournal.formatName,
        version: SidebarWriteJournal.formatVersion,
        device: String(identity.device), inode: String(identity.inode),
        birth: birth.map(String.init), volume: volume)
    }

    static func put(_ entry: Entry) -> Line {
      Line(
        op: .put, id: entry.id, at: entry.recordedAt.milliseconds,
        content: entry.content)
    }

    static func refuse(id: UUID, reason: String) -> Line {
      Line(op: .refuse, id: id, reason: reason)
    }

    static func settle(ids: [UUID]) -> Line {
      Line(op: .settle, ids: ids)
    }
  }

  struct Parsed {
    var header: Line?
    var lines: [Line] = []
    var unreadableLines = 0
  }

  /// `<crc32> <json>\n`.
  nonisolated static func encode(_ line: Line) -> Data? {
    let encoder = JSONEncoder()
    encoder.outputFormatting = [.sortedKeys, .withoutEscapingSlashes]
    guard let json = try? encoder.encode(line) else { return nil }
    var data = Data(String(format: "%08x ", SidebarWriteJournalCRC32.checksum(json)).utf8)
    data.append(json)
    data.append(0x0A)
    return data
  }

  /// Split, verify and decode. The first readable line must be the header;
  /// any line that fails — the unterminated last one included — is counted
  /// and skipped.
  nonisolated static func parse(_ data: Data) -> Parsed {
    var parsed = Parsed()
    let bytes = [UInt8](data)
    let decoder = JSONDecoder()
    var sawFirstLine = false
    func take(_ range: Range<Int>) {
      defer { sawFirstLine = true }
      guard range.count > 9, bytes[range.lowerBound + 8] == 0x20,
        let crc = UInt32(
          String(decoding: bytes[range.lowerBound..<range.lowerBound + 8], as: UTF8.self),
          radix: 16)
      else {
        parsed.unreadableLines += 1
        return
      }
      let json = bytes[(range.lowerBound + 9)..<range.upperBound]
      guard SidebarWriteJournalCRC32.checksum(json) == crc,
        let line = try? decoder.decode(Line.self, from: Data(json))
      else {
        parsed.unreadableLines += 1
        return
      }
      if !sawFirstLine {
        if line.op == .header { parsed.header = line } else { parsed.unreadableLines += 1 }
        return
      }
      parsed.lines.append(line)
    }
    var start = 0
    for index in bytes.indices where bytes[index] == 0x0A {
      take(start..<index)
      start = index + 1
    }
    if start < bytes.count {
      // The torn tail: a crash mid-append left a line without its newline.
      parsed.unreadableLines += 1
    }
    return parsed
  }

  private enum ReadResult {
    case missing
    case unreadable
    case data(Data)
  }

  nonisolated private static func readBounded(_ url: URL) -> ReadResult {
    let fd = url.path.withCString { Darwin.open($0, O_RDONLY | O_CLOEXEC | O_NOFOLLOW) }
    guard fd >= 0 else { return errno == ENOENT ? .missing : .unreadable }
    let handle = FileHandle(fileDescriptor: fd, closeOnDealloc: true)
    let data: Data
    do {
      data = try handle.read(upToCount: maxReadBytes + 1) ?? Data()
    } catch {
      return .unreadable
    }
    guard data.count <= maxReadBytes else { return .unreadable }
    return .data(data)
  }
}

/// The journal's file work on one serial utility queue: no fsync ever runs
/// on the main actor, and lines land in the order they were submitted.
final class SidebarWriteJournalIO: @unchecked Sendable {
  private let queue = DispatchQueue(
    label: "com.startingblind.slate.sidebar-write-journal", qos: .utility)
  private let lock = NSLock()
  private var pending = 0
  private var idleWaiters: [CheckedContinuation<Void, Never>] = []
  private var beforeSyncHook: (@Sendable (URL) -> Void)?
  private var syncDurations: [Duration] = []

  /// Test seam: runs on the journal queue just before each fsync.
  func setBeforeSyncHookForTesting(_ hook: (@Sendable (URL) -> Void)?) {
    lock.withLock { beforeSyncHook = hook }
  }

  /// Wall time of every fsync'd append so far (the PR's timing figures).
  var syncDurationsForTesting: [Duration] { lock.withLock { syncDurations } }

  var hasPendingWork: Bool { lock.withLock { pending > 0 } }

  /// Run `work` on the queue; the task resolves to its result.
  func submit(_ work: @escaping @Sendable (SidebarWriteJournalIO) -> Bool) -> Task<Bool, Never> {
    let completion = SidebarWriteJournalCompletion()
    lock.withLock { pending += 1 }
    queue.async {
      completion.resolve(work(self))
      self.finishOne()
    }
    return Task { await completion.value }
  }

  func enqueue(_ work: @escaping @Sendable (SidebarWriteJournalIO) -> Void) {
    lock.withLock { pending += 1 }
    queue.async {
      work(self)
      self.finishOne()
    }
  }

  func waitUntilIdle() async {
    await withCheckedContinuation { (continuation: CheckedContinuation<Void, Never>) in
      let idle: Bool = lock.withLock {
        if pending == 0 { return true }
        idleWaiters.append(continuation)
        return false
      }
      if idle { continuation.resume() }
    }
  }

  private func finishOne() {
    let waiters: [CheckedContinuation<Void, Never>] = lock.withLock {
      pending -= 1
      guard pending == 0 else { return [] }
      defer { idleWaiters = [] }
      return idleWaiters
    }
    for waiter in waiters { waiter.resume() }
  }

  // MARK: File operations (journal queue only)

  /// Append one line, writing the header first into an empty file. A short
  /// write is truncated away, so the file never ends mid-line by our hand.
  func appendLine(_ line: Data, to url: URL, header: Data, sync: Bool) -> Bool {
    guard Self.ensureDirectory(url.deletingLastPathComponent()) else { return false }
    let fd = url.path.withCString {
      Darwin.open($0, O_WRONLY | O_APPEND | O_CREAT | O_CLOEXEC | O_NOFOLLOW, 0o600)
    }
    guard fd >= 0 else { return false }
    defer { close(fd) }
    var info = stat()
    guard fstat(fd, &info) == 0 else { return false }
    let originalSize = info.st_size
    var bytes = Data()
    if originalSize == 0 { bytes.append(header) }
    bytes.append(line)
    guard Self.writeAll(fd, bytes) else {
      _ = ftruncate(fd, originalSize)
      return false
    }
    guard sync else { return true }
    let hook = lock.withLock { beforeSyncHook }
    hook?(url)
    let clock = ContinuousClock()
    let started = clock.now
    guard fsync(fd) == 0 else { return false }
    if originalSize == 0 {
      // A new file's directory entry must be durable too.
      Self.syncDirectory(url.deletingLastPathComponent())
    }
    let elapsed = clock.now - started
    lock.withLock { syncDurations.append(elapsed) }
    return true
  }

  /// Atomic rewrite: temp file, fsync, rename, directory fsync.
  static func rewrite(_ contents: Data, at url: URL) {
    let temporary = url.appendingPathExtension("tmp")
    let fd = temporary.path.withCString {
      Darwin.open($0, O_WRONLY | O_CREAT | O_TRUNC | O_CLOEXEC | O_NOFOLLOW, 0o600)
    }
    guard fd >= 0 else { return }
    let written = writeAll(fd, contents) && fsync(fd) == 0
    close(fd)
    guard written, rename(temporary.path, url.path) == 0 else {
      unlink(temporary.path)
      return
    }
    syncDirectory(url.deletingLastPathComponent())
  }

  static func remove(_ url: URL) {
    unlink(url.path)
  }

  /// Set an unreadable file aside so new entries start a clean file; the
  /// copy keeps the damage inspectable.
  static func quarantine(_ url: URL) {
    let aside = url.appendingPathExtension("unreadable")
    unlink(aside.path)
    rename(url.path, aside.path)
  }

  private static func ensureDirectory(_ directory: URL) -> Bool {
    var isDirectory: ObjCBool = false
    if FileManager.default.fileExists(atPath: directory.path, isDirectory: &isDirectory) {
      return isDirectory.boolValue
    }
    do {
      try FileManager.default.createDirectory(
        at: directory, withIntermediateDirectories: true,
        attributes: [.posixPermissions: 0o700])
    } catch {
      return false
    }
    syncDirectory(directory.deletingLastPathComponent())
    return true
  }

  private static func syncDirectory(_ directory: URL) {
    let fd = directory.path.withCString { Darwin.open($0, O_RDONLY | O_DIRECTORY | O_CLOEXEC) }
    guard fd >= 0 else { return }
    _ = fsync(fd)
    close(fd)
  }

  private static func writeAll(_ fd: Int32, _ data: Data) -> Bool {
    data.withUnsafeBytes { buffer -> Bool in
      guard var pointer = buffer.baseAddress else { return true }
      var remaining = buffer.count
      while remaining > 0 {
        let written = Darwin.write(fd, pointer, remaining)
        if written < 0 {
          if errno == EINTR { continue }
          return false
        }
        remaining -= written
        pointer = pointer.advanced(by: written)
      }
      return true
    }
  }
}

/// One submitted append's outcome, awaitable from any task.
final class SidebarWriteJournalCompletion: @unchecked Sendable {
  private let lock = NSLock()
  private var result: Bool?
  private var waiters: [CheckedContinuation<Bool, Never>] = []

  func resolve(_ value: Bool) {
    let waiting: [CheckedContinuation<Bool, Never>] = lock.withLock {
      result = value
      defer { waiters = [] }
      return waiters
    }
    for waiter in waiting { waiter.resume(returning: value) }
  }

  var value: Bool {
    get async {
      await withCheckedContinuation { (continuation: CheckedContinuation<Bool, Never>) in
        let ready: Bool? = lock.withLock {
          if let result { return result }
          waiters.append(continuation)
          return nil
        }
        if let ready { continuation.resume(returning: ready) }
      }
    }
  }
}

/// CRC-32 (IEEE 802.3, reflected, polynomial 0xEDB88320) — the per-line
/// checksum that tells a torn or damaged line from a whole one.
enum SidebarWriteJournalCRC32 {
  private static let table: [UInt32] = (0..<256).map { index in
    var value = UInt32(index)
    for _ in 0..<8 {
      value = value & 1 == 1 ? 0xEDB8_8320 ^ (value >> 1) : value >> 1
    }
    return value
  }

  static func checksum<Bytes: Sequence>(_ bytes: Bytes) -> UInt32 where Bytes.Element == UInt8 {
    var crc: UInt32 = 0xFFFF_FFFF
    for byte in bytes {
      crc = table[Int((crc ^ UInt32(byte)) & 0xFF)] ^ (crc >> 8)
    }
    return crc ^ 0xFFFF_FFFF
  }
}

extension Date {
  fileprivate init(milliseconds: Int64) {
    self.init(timeIntervalSince1970: TimeInterval(milliseconds) / 1_000)
  }

  fileprivate var milliseconds: Int64 {
    Int64((timeIntervalSince1970 * 1_000).rounded())
  }
}
