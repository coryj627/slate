// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import CoreFoundation
import Darwin
import Foundation

/// Result of reading the vault-authored sidebar presentation file.
///
/// Unsafe input always returns the same minimal defaults used for a missing
/// file, plus a notice that keeps the store read-only. That lets AppState show
/// one useful message without ever interpreting or replacing unknown data.
struct SidebarVaultPrefsReadResult {
  let root: [String: Any]
  let notice: SidebarVaultPrefsNotice?

  var isReadOnly: Bool { notice != nil }
}

/// Concise, user-facing reasons why `.slate/sidebar.json` is using defaults.
enum SidebarVaultPrefsNotice: Error, LocalizedError, Equatable, Sendable {
  case malformed
  case oversized(limitBytes: Int)
  case unreadable
  case newerVersion(found: Int, supported: Int)

  var errorDescription: String? {
    switch self {
    case .malformed:
      return "Sidebar settings are using defaults because .slate/sidebar.json is "
        + "malformed. Slate won’t change this file. Fix it, then choose Retry."
    case .oversized(let limitBytes):
      let mebibytes = limitBytes / (1_024 * 1_024)
      return "Sidebar settings are using defaults because .slate/sidebar.json exceeds "
        + "the \(mebibytes) MiB safety limit. Slate won’t change this file. "
        + "Reduce it, then choose Retry."
    case .unreadable:
      return "Sidebar settings are using defaults because .slate/sidebar.json could not "
        + "be read. Slate won’t change this file. Make it readable, then choose Retry."
    case .newerVersion(let found, let supported):
      return ".slate/sidebar.json uses a newer version (\(found); this Slate supports "
        + "\(supported)). Sidebar settings are using defaults. Slate won’t change this "
        + "file to avoid downgrading it."
    }
  }
}

enum SidebarVaultPrefsStoreError: Error, LocalizedError, Equatable {
  case readOnly(SidebarVaultPrefsNotice)
  case lockUnavailable(reason: String)
  case encodingFailed(reason: String)
  case writeFailed(reason: String)
  /// The atomic rename already took effect — the file's CONTENT is the new
  /// value — but the parent-directory synchronization afterward failed, so
  /// the replacement's durability across a crash is not guaranteed. Callers
  /// must treat the content as committed and report only the durability
  /// concern (FL-06 round-10 finding 1: replaying the mutation later could
  /// wrongly transform paths another process recreated in between).
  case replacedButUnsynced(reason: String)

  var errorDescription: String? {
    switch self {
    case .readOnly(let notice):
      return notice.localizedDescription
    case .lockUnavailable(let reason):
      return "Sidebar settings could not be saved because their writer lock was "
        + "unavailable: \(reason)"
    case .encodingFailed(let reason):
      return "Sidebar settings could not be encoded: \(reason)"
    case .writeFailed(let reason):
      return "Sidebar settings could not be saved: \(reason)"
    case .replacedButUnsynced(let reason):
      return "Sidebar settings were saved, but the change may not survive a "
        + "sudden power loss: \(reason)"
    }
  }
}

/// Generic, vault-authored sidebar preferences at `.slate/sidebar.json`.
///
/// FL-02 intentionally defines only the top-level schema version. Later
/// milestones own their sections and update them through `update`, which keeps
/// unknown sibling keys intact. Every read-modify-write holds a stable sidecar
/// `flock` for the full cycle. All `.slate` operations are relative to pinned
/// directory descriptors, and writes use a same-directory temp + `renameat`, so
/// neither a symlink swap nor a kill can expose a partial or out-of-vault file.
struct SidebarVaultPrefsStore: Sendable {
  static let currentVersion = 1
  static let maxReadBytes = 16 * 1_024 * 1_024

  private static let slateDirectoryName = ".slate"
  private static let sidebarFileName = "sidebar.json"
  private static let lockFileName = "sidebar.json.lock"

  let vaultRoot: URL
  private let sidebarFileOpener: @Sendable (Int32) -> Int32
  private let directorySynchronizer: @Sendable (Int32) -> Int32

  init(vaultRoot: URL) {
    self.init(
      vaultRoot: vaultRoot,
      sidebarFileOpener: { Self.openSidebarFile(in: $0) },
      directorySynchronizer: { fsync($0) }
    )
  }

  init(
    vaultRoot: URL,
    sidebarFileOpener: @escaping @Sendable (Int32) -> Int32,
    directorySynchronizer: @escaping @Sendable (Int32) -> Int32 = { fsync($0) }
  ) {
    self.vaultRoot = vaultRoot
    self.sidebarFileOpener = sidebarFileOpener
    self.directorySynchronizer = directorySynchronizer
  }

  var fileURL: URL {
    vaultRoot.appendingPathComponent(".slate", isDirectory: true)
      .appendingPathComponent("sidebar.json", isDirectory: false)
  }

  var lockURL: URL {
    fileURL.appendingPathExtension("lock")
  }

  /// Reads without creating `.slate`, the preference file, or its lock.
  /// Missing input is writable; every unsafe existing input is read-only.
  func read() -> SidebarVaultPrefsReadResult {
    let slateDirectoryFD: Int32
    switch openSlateDirectory(createIfMissing: false) {
    case .missing:
      return SidebarVaultPrefsReadResult(root: Self.defaultRoot, notice: nil)
    case .failed:
      return SidebarVaultPrefsReadResult(root: Self.defaultRoot, notice: .unreadable)
    case .opened(let fd):
      slateDirectoryFD = fd
    }
    defer { close(slateDirectoryFD) }

    switch loadExisting(in: slateDirectoryFD) {
    case .missing:
      return SidebarVaultPrefsReadResult(root: Self.defaultRoot, notice: nil)
    case .loaded(var root):
      // An absent version is treated as the v1 generic shape, matching
      // the repository's other vault JSON stores. The next authored
      // update persists the explicit version while preserving all keys.
      if root["version"] == nil {
        root["version"] = Self.currentVersion
      }
      return SidebarVaultPrefsReadResult(root: root, notice: nil)
    case .blocked(let notice):
      return SidebarVaultPrefsReadResult(root: Self.defaultRoot, notice: notice)
    }
  }

  /// Applies one generic JSON mutation while holding the cross-process lock
  /// across read, merge, encode, and atomic replacement.
  ///
  /// Existing malformed, oversized, unreadable, or forward-version files are
  /// never overwritten. A missing file is created only by this write path.
  func update(_ mutation: (inout [String: Any]) throws -> Void) throws {
    let slateDirectoryFD: Int32
    switch openSlateDirectory(createIfMissing: true) {
    case .opened(let fd):
      slateDirectoryFD = fd
    case .missing:
      throw SidebarVaultPrefsStoreError.writeFailed(
        reason: "the vault root is unavailable"
      )
    case .failed(let reason):
      throw SidebarVaultPrefsStoreError.writeFailed(reason: reason)
    }
    defer { close(slateDirectoryFD) }

    let lockFD = Self.lockFileName.withCString { name in
      openat(
        slateDirectoryFD,
        name,
        O_CREAT | O_RDWR | O_CLOEXEC | O_NOFOLLOW,
        0o600
      )
    }
    guard lockFD >= 0 else {
      throw SidebarVaultPrefsStoreError.lockUnavailable(reason: Self.posixReason())
    }
    defer { close(lockFD) }

    var lockMetadata = stat()
    guard fstat(lockFD, &lockMetadata) == 0,
      lockMetadata.st_mode & S_IFMT == S_IFREG
    else {
      throw SidebarVaultPrefsStoreError.lockUnavailable(
        reason: "the lock path is not a regular file"
      )
    }

    guard Self.lockExclusively(lockFD) else {
      throw SidebarVaultPrefsStoreError.lockUnavailable(reason: Self.posixReason())
    }
    defer { _ = flock(lockFD, LOCK_UN) }

    var root: [String: Any]
    switch loadExisting(in: slateDirectoryFD) {
    case .missing:
      root = Self.defaultRoot
    case .loaded(let existing):
      root = existing
    case .blocked(let notice):
      throw SidebarVaultPrefsStoreError.readOnly(notice)
    }

    // Baseline for the no-op check below: what an unchanged root would look
    // like after the store's own version stamp.
    var unchangedBaseline = root
    unchangedBaseline["version"] = Self.currentVersion

    try mutation(&root)
    // The store, not callers, owns the schema marker. This also prevents a
    // mutation from accidentally manufacturing a forward-version file.
    root["version"] = Self.currentVersion

    // A mutation that leaves the root unchanged performs no physical
    // replacement (FL-06 round-13): repeated no-op cycles must not churn the
    // file's bytes, mtime, or sync state.
    if (root as NSDictionary).isEqual(to: unchangedBaseline) {
      return
    }

    guard JSONSerialization.isValidJSONObject(root) else {
      throw SidebarVaultPrefsStoreError.encodingFailed(
        reason: "the update contains a value that JSON cannot represent"
      )
    }

    let output: Data
    do {
      output = try JSONSerialization.data(
        withJSONObject: root,
        options: [.prettyPrinted, .sortedKeys]
      )
    } catch {
      throw SidebarVaultPrefsStoreError.encodingFailed(reason: error.localizedDescription)
    }
    guard output.count <= Self.maxReadBytes else {
      throw SidebarVaultPrefsStoreError.encodingFailed(
        reason: "the update would exceed the 16 MiB safety limit"
      )
    }

    try writeAtomically(output, in: slateDirectoryFD)
  }

  // MARK: - Bounded load

  private enum LoadResult {
    case missing
    case loaded([String: Any])
    case blocked(SidebarVaultPrefsNotice)
  }

  private static var defaultRoot: [String: Any] {
    ["version": currentVersion]
  }

  private func loadExisting(in slateDirectoryFD: Int32) -> LoadResult {
    switch readBoundedData(in: slateDirectoryFD) {
    case .missing:
      return .missing
    case .blocked(let notice):
      return .blocked(notice)
    case .data(let data):
      guard
        let object = try? JSONSerialization.jsonObject(with: data, options: []),
        let root = object as? [String: Any]
      else {
        return .blocked(.malformed)
      }

      if let rawVersion = root["version"] {
        guard let version = Self.nonnegativeJSONInteger(rawVersion) else {
          return .blocked(.malformed)
        }
        if version > Self.currentVersion {
          return .blocked(
            .newerVersion(found: version, supported: Self.currentVersion)
          )
        }
      }
      return .loaded(root)
    }
  }

  private enum BoundedDataResult {
    case missing
    case data(Data)
    case blocked(SidebarVaultPrefsNotice)
  }

  /// POSIX read capped at `maxReadBytes + 1`. `fstat` rejects an already
  /// oversized regular file without allocating for it; the extra byte also
  /// catches a file that grows after `fstat`.
  private func readBoundedData(in slateDirectoryFD: Int32) -> BoundedDataResult {
    let fd: Int32
    var canRetryFirstCreate = true
    while true {
      let openedFD = sidebarFileOpener(slateDirectoryFD)
      if openedFD >= 0 {
        fd = openedFD
        break
      }
      let openError = errno
      guard openError == ENOENT else {
        return .blocked(.unreadable)
      }
      switch Self.entryState(named: Self.sidebarFileName, in: slateDirectoryFD) {
      case .missing:
        return .missing
      case .regular where canRetryFirstCreate:
        // A cooperating writer may have atomically created the first file
        // after our failed open but before this no-follow inspection. Retry
        // once; a second race remains bounded and reports honestly.
        canRetryFirstCreate = false
        continue
      case .directory, .regular, .other, .failed:
        return .blocked(.unreadable)
      }
    }
    defer { close(fd) }

    var metadata = stat()
    guard fstat(fd, &metadata) == 0 else {
      return .blocked(.unreadable)
    }
    guard metadata.st_mode & S_IFMT == S_IFREG else {
      return .blocked(.unreadable)
    }
    guard metadata.st_size <= off_t(Self.maxReadBytes) else {
      return .blocked(.oversized(limitBytes: Self.maxReadBytes))
    }

    var output = Data()
    if metadata.st_size > 0 {
      output.reserveCapacity(Int(metadata.st_size))
    }
    var buffer = [UInt8](repeating: 0, count: 64 * 1_024)

    while output.count <= Self.maxReadBytes {
      let remaining = Self.maxReadBytes + 1 - output.count
      let requested = min(buffer.count, remaining)
      let count = buffer.withUnsafeMutableBytes { bytes in
        Darwin.read(fd, bytes.baseAddress, requested)
      }
      if count == 0 {
        return .data(output)
      }
      if count < 0 {
        if errno == EINTR { continue }
        return .blocked(.unreadable)
      }
      output.append(contentsOf: buffer.prefix(count))
      if output.count > Self.maxReadBytes {
        return .blocked(.oversized(limitBytes: Self.maxReadBytes))
      }
    }

    return .blocked(.oversized(limitBytes: Self.maxReadBytes))
  }

  // MARK: - Filesystem helpers

  private enum SlateDirectoryOpenResult {
    case missing
    case opened(Int32)
    case failed(reason: String)
  }

  private enum EntryState {
    case missing
    case directory
    case regular
    case other
    case failed(error: Int32)
  }

  /// Opens the vault root once, then resolves `.slate` exactly once relative
  /// to that descriptor with `O_NOFOLLOW | O_DIRECTORY`. The returned fd pins
  /// the validated directory for all later lock/read/temp/rename operations.
  private func openSlateDirectory(createIfMissing: Bool) -> SlateDirectoryOpenResult {
    let rootFD = open(vaultRoot.path, O_RDONLY | O_DIRECTORY | O_CLOEXEC)
    guard rootFD >= 0 else {
      let code = errno
      if !createIfMissing, code == ENOENT {
        return .missing
      }
      return .failed(
        reason: "the vault root could not be opened: \(Self.posixReason(code))"
      )
    }
    defer { close(rootFD) }

    func openPinnedSlateDirectory() -> Int32 {
      Self.slateDirectoryName.withCString { name in
        openat(
          rootFD,
          name,
          O_RDONLY | O_DIRECTORY | O_NOFOLLOW | O_CLOEXEC
        )
      }
    }

    let firstFD = openPinnedSlateDirectory()
    if firstFD >= 0 {
      return .opened(firstFD)
    }

    switch Self.entryState(named: Self.slateDirectoryName, in: rootFD) {
    case .directory:
      // A cooperating writer may have created the directory after our first
      // open. Retry with the same no-follow flags; a symlink swap still fails.
      let retryFD = openPinnedSlateDirectory()
      guard retryFD >= 0 else {
        return .failed(
          reason: ".slate could not be pinned: \(Self.posixReason())"
        )
      }
      return .opened(retryFD)
    case .regular, .other:
      return .failed(reason: ".slate is not a directly contained directory")
    case .failed(let code):
      return .failed(
        reason: ".slate could not be inspected: \(Self.posixReason(code))"
      )
    case .missing:
      guard createIfMissing else { return .missing }
    }

    let createResult = Self.slateDirectoryName.withCString { name in
      mkdirat(rootFD, name, 0o755)
    }
    if createResult != 0, errno != EEXIST {
      return .failed(
        reason: ".slate could not be created: \(Self.posixReason())"
      )
    }

    let createdFD = openPinnedSlateDirectory()
    guard createdFD >= 0 else {
      return .failed(
        reason: ".slate could not be pinned after creation: \(Self.posixReason())"
      )
    }
    return .opened(createdFD)
  }

  /// Writes a complete temp file and atomically renames it within the same
  /// pinned `.slate` directory. No absolute path is re-resolved after pinning.
  private func writeAtomically(_ output: Data, in slateDirectoryFD: Int32) throws {
    let temporaryName = ".sidebar.json.\(UUID().uuidString).tmp"
    let temporaryFD = temporaryName.withCString { name in
      openat(
        slateDirectoryFD,
        name,
        O_WRONLY | O_CREAT | O_EXCL | O_CLOEXEC | O_NOFOLLOW,
        0o600
      )
    }
    guard temporaryFD >= 0 else {
      throw SidebarVaultPrefsStoreError.writeFailed(
        reason: "the temporary file could not be created: \(Self.posixReason())"
      )
    }

    var removeTemporary = true
    defer {
      close(temporaryFD)
      if removeTemporary {
        temporaryName.withCString { name in
          _ = unlinkat(slateDirectoryFD, name, 0)
        }
      }
    }

    var writeFailure: Int32?
    output.withUnsafeBytes { bytes in
      var offset = 0
      while offset < bytes.count {
        let written = Darwin.write(
          temporaryFD,
          bytes.baseAddress!.advanced(by: offset),
          bytes.count - offset
        )
        if written > 0 {
          offset += written
        } else if written < 0, errno == EINTR {
          continue
        } else {
          writeFailure = written == 0 ? EIO : errno
          break
        }
      }
    }
    if let code = writeFailure {
      throw SidebarVaultPrefsStoreError.writeFailed(
        reason: "the temporary file could not be written: \(Self.posixReason(code))"
      )
    }

    guard fsync(temporaryFD) == 0 else {
      throw SidebarVaultPrefsStoreError.writeFailed(
        reason: "the temporary file could not be synchronized: \(Self.posixReason())"
      )
    }

    let renameResult = temporaryName.withCString { sourceName in
      Self.sidebarFileName.withCString { destinationName in
        renameat(
          slateDirectoryFD,
          sourceName,
          slateDirectoryFD,
          destinationName
        )
      }
    }
    guard renameResult == 0 else {
      throw SidebarVaultPrefsStoreError.writeFailed(
        reason: "the preference file could not be replaced: \(Self.posixReason())"
      )
    }
    removeTemporary = false

    // The temp file is durable before rename; synchronizing the pinned parent
    // now makes the replacement directory entry durable as well. Report a
    // failure honestly even though the atomic rename has already taken effect.
    guard directorySynchronizer(slateDirectoryFD) == 0 else {
      throw SidebarVaultPrefsStoreError.replacedButUnsynced(
        reason: "the preference directory could not be synchronized after replacement: "
          + Self.posixReason()
      )
    }
  }

  private static func entryState(named name: String, in directoryFD: Int32) -> EntryState {
    var metadata = stat()
    let result = name.withCString { component in
      fstatat(directoryFD, component, &metadata, AT_SYMLINK_NOFOLLOW)
    }
    guard result == 0 else {
      let code = errno
      return code == ENOENT ? .missing : .failed(error: code)
    }
    switch metadata.st_mode & S_IFMT {
    case S_IFDIR:
      return .directory
    case S_IFREG:
      return .regular
    default:
      return .other
    }
  }

  private static func openSidebarFile(in slateDirectoryFD: Int32) -> Int32 {
    sidebarFileName.withCString { name in
      openat(
        slateDirectoryFD,
        name,
        O_RDONLY | O_CLOEXEC | O_NONBLOCK | O_NOFOLLOW
      )
    }
  }

  private static func lockExclusively(_ fd: Int32) -> Bool {
    while flock(fd, LOCK_EX) != 0 {
      if errno != EINTR { return false }
    }
    return true
  }

  private static func nonnegativeJSONInteger(_ value: Any) -> Int? {
    guard let number = value as? NSNumber else { return nil }
    guard CFGetTypeID(number) != CFBooleanGetTypeID() else { return nil }
    // `Int(number.doubleValue)` can trap near Int.max because Double rounds
    // the boundary upward. Parsing NSNumber's normalized decimal string is
    // exact, rejects fractional/scientific versions, and fails safely when
    // the value is outside this process's integer range.
    guard let integer = Int(number.stringValue), integer >= 0 else { return nil }
    return integer
  }

  private static func posixReason(_ code: Int32 = errno) -> String {
    String(cString: strerror(code))
  }
}
