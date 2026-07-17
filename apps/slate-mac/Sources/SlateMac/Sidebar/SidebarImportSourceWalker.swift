// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Darwin
import Foundation

struct SidebarImportSecurityScopeAccess {
  private let startAccess: (URL) -> Bool
  private let stopAccess: (URL) -> Void

  init(
    start: @escaping (URL) -> Bool,
    stop: @escaping (URL) -> Void
  ) {
    startAccess = start
    stopAccess = stop
  }

  func start(_ url: URL) -> Bool {
    startAccess(url)
  }

  func stop(_ url: URL) {
    stopAccess(url)
  }
}

struct SidebarImportSourceLimits {
  let refuseBytes: Int64
  let totalBytes: Int64
  let maximumEntries: Int
  let maximumDepth: Int
}

enum SidebarImportSourceEntryKind: Equatable {
  case directory
  case regularFile
}

struct SidebarImportSourceRelativePath: Equatable {
  let display: String

  fileprivate let components: [String]

  fileprivate init(components: [String]) {
    self.components = components
    display = components.joined(separator: "/")
  }
}

struct SidebarImportSourceEntry: Equatable {
  let relativePath: SidebarImportSourceRelativePath
  let kind: SidebarImportSourceEntryKind
}

enum SidebarImportSourceFailureReason: Equatable {
  case hidden
  case symbolicLink
  case fifo
  case socket
  case characterDevice
  case blockDevice
  case unreadable
  case notDirectory
  case unsupported
  case invalidPath
}

struct SidebarImportSourceFailure: Equatable {
  let relativePath: SidebarImportSourceRelativePath
  let reason: SidebarImportSourceFailureReason
}

struct SidebarImportSourceEntryPairs: Equatable, ExpressibleByArrayLiteral {
  typealias Element = (String, SidebarImportSourceEntryKind)

  private let values: [Element]

  init(arrayLiteral elements: Element...) {
    values = elements
  }

  fileprivate init(_ values: [Element]) {
    self.values = values
  }

  static func == (left: Self, right: Self) -> Bool {
    guard left.values.count == right.values.count else { return false }
    return zip(left.values, right.values).allSatisfy { leftValue, rightValue in
      leftValue.0 == rightValue.0 && leftValue.1 == rightValue.1
    }
  }
}

struct SidebarImportSourceEntries: Equatable, RandomAccessCollection {
  typealias Index = Int

  private let storage: [SidebarImportSourceEntry]

  var startIndex: Int { storage.startIndex }
  var endIndex: Int { storage.endIndex }

  fileprivate init(_ storage: [SidebarImportSourceEntry]) {
    self.storage = storage
  }

  subscript(position: Int) -> SidebarImportSourceEntry {
    storage[position]
  }

  func map(
    _ transform: (SidebarImportSourceEntry) throws -> (String, SidebarImportSourceEntryKind)
  ) rethrows -> SidebarImportSourceEntryPairs {
    try SidebarImportSourceEntryPairs(storage.map(transform))
  }
}

struct SidebarImportSourceManifest: Equatable {
  let entries: SidebarImportSourceEntries
  let failures: [SidebarImportSourceFailure]

  fileprivate init(
    entries: [SidebarImportSourceEntry],
    failures: [SidebarImportSourceFailure]
  ) {
    self.entries = SidebarImportSourceEntries(entries)
    self.failures = failures
  }
}

enum SidebarImportSourceWalkerError: Error {
  case invalidLimits
  case securityScopeDenied
  case rejectedRoot(path: String, reason: SidebarImportSourceFailureReason)
  case sourceContainsVault(path: String)
  case openFailed(path: String, reason: String)
  case inspectFailed(path: String, reason: String)
  case unsupportedEntry(path: String)
  case invalidFileName
  case tooManyEntries(limit: Int)
  case tooDeep(path: String, limit: Int)
  case fileTooLarge(path: String, limitBytes: Int64)
  case totalTooLarge(limitBytes: Int64)
  case preparedSourceClosed
  case notARegularFile(path: String)
  case readFailed(path: String, reason: String)
}

final class SidebarImportPreparedSource {
  let manifest: SidebarImportSourceManifest

  private let rootURL: URL
  private let limits: SidebarImportSourceLimits
  private let scopeAccess: SidebarImportSecurityScopeAccess
  private let scopeWasStarted: Bool
  private let lock = NSLock()
  private var rootFD: Int32

  fileprivate init(
    rootURL: URL,
    rootFD: Int32,
    limits: SidebarImportSourceLimits,
    scopeAccess: SidebarImportSecurityScopeAccess,
    scopeWasStarted: Bool,
    manifest: SidebarImportSourceManifest
  ) {
    self.rootURL = rootURL
    self.rootFD = rootFD
    self.limits = limits
    self.scopeAccess = scopeAccess
    self.scopeWasStarted = scopeWasStarted
    self.manifest = manifest
  }

  deinit {
    close()
  }

  func readBytes(for entry: SidebarImportSourceEntry) throws -> Data {
    guard entry.kind == .regularFile else {
      throw SidebarImportSourceWalkerError.notARegularFile(
        path: entry.relativePath.display)
    }

    let pinnedRoot = try duplicateRootFD()
    defer { Darwin.close(pinnedRoot) }

    let components = entry.relativePath.components
    if components.isEmpty {
      return try readBytes(from: pinnedRoot, path: entry.relativePath.display)
    }
    let fileName = components[components.index(before: components.endIndex)]

    var directoryFD = pinnedRoot
    var ownsDirectoryFD = false
    defer {
      if ownsDirectoryFD {
        Darwin.close(directoryFD)
      }
    }

    for component in components.dropLast() {
      let nextFD = component.withCString { name in
        openat(
          directoryFD,
          name,
          O_RDONLY | O_DIRECTORY | O_NOFOLLOW | O_CLOEXEC
        )
      }
      guard nextFD >= 0 else {
        throw SidebarImportSourceWalkerError.openFailed(
          path: entry.relativePath.display,
          reason: Self.posixReason())
      }
      if ownsDirectoryFD {
        Darwin.close(directoryFD)
      }
      directoryFD = nextFD
      ownsDirectoryFD = true
    }

    let fileFD = fileName.withCString { name in
      openat(
        directoryFD,
        name,
        O_RDONLY | O_NONBLOCK | O_NOFOLLOW | O_CLOEXEC
      )
    }
    guard fileFD >= 0 else {
      throw SidebarImportSourceWalkerError.openFailed(
        path: entry.relativePath.display,
        reason: Self.posixReason())
    }
    defer { Darwin.close(fileFD) }

    return try readBytes(from: fileFD, path: entry.relativePath.display)
  }

  private func readBytes(from fileFD: Int32, path: String) throws -> Data {
    var metadata = stat()
    guard fstat(fileFD, &metadata) == 0 else {
      throw SidebarImportSourceWalkerError.inspectFailed(
        path: path,
        reason: Self.posixReason())
    }
    guard metadata.st_mode & S_IFMT == S_IFREG else {
      throw SidebarImportSourceWalkerError.notARegularFile(path: path)
    }
    let size = metadata.st_size
    guard size >= 0, size <= limits.refuseBytes else {
      throw SidebarImportSourceWalkerError.fileTooLarge(
        path: path,
        limitBytes: limits.refuseBytes)
    }

    var output = Data()
    if size <= Int.max {
      output.reserveCapacity(Int(size))
    }
    var buffer = [UInt8](repeating: 0, count: 64 * 1_024)
    var offset: off_t = 0
    while true {
      let count = buffer.withUnsafeMutableBytes { bytes in
        Darwin.pread(fileFD, bytes.baseAddress, bytes.count, offset)
      }
      if count > 0 {
        guard Int64(output.count) + Int64(count) <= limits.refuseBytes else {
          throw SidebarImportSourceWalkerError.fileTooLarge(
            path: path,
            limitBytes: limits.refuseBytes)
        }
        buffer.withUnsafeBytes { bytes in
          output.append(bytes.bindMemory(to: UInt8.self).baseAddress!, count: count)
        }
        offset += off_t(count)
      } else if count == 0 {
        return output
      } else if errno != EINTR {
        throw SidebarImportSourceWalkerError.readFailed(
          path: path,
          reason: Self.posixReason())
      }
    }
  }

  func close() {
    var descriptor: Int32 = -1
    lock.lock()
    if rootFD >= 0 {
      descriptor = rootFD
      rootFD = -1
    }
    lock.unlock()

    guard descriptor >= 0 else { return }
    Darwin.close(descriptor)
    if scopeWasStarted {
      scopeAccess.stop(rootURL)
    }
  }

  private func duplicateRootFD() throws -> Int32 {
    lock.lock()
    defer { lock.unlock() }
    guard rootFD >= 0 else {
      throw SidebarImportSourceWalkerError.preparedSourceClosed
    }
    let descriptor = dup(rootFD)
    guard descriptor >= 0 else {
      throw SidebarImportSourceWalkerError.openFailed(
        path: rootURL.path,
        reason: Self.posixReason())
    }
    _ = fcntl(descriptor, F_SETFD, FD_CLOEXEC)
    return descriptor
  }

  private static func posixReason(_ code: Int32 = errno) -> String {
    String(cString: strerror(code))
  }
}

struct SidebarImportSourceWalker {
  let limits: SidebarImportSourceLimits
  let scopeAccess: SidebarImportSecurityScopeAccess

  func prepare(rootURL: URL, vaultURL: URL) throws -> SidebarImportPreparedSource {
    guard limits.refuseBytes >= 0,
      limits.totalBytes >= 0,
      limits.maximumEntries > 0,
      limits.maximumDepth >= 0
    else {
      throw SidebarImportSourceWalkerError.invalidLimits
    }

    let scopeWasStarted = scopeAccess.start(rootURL)
    var transferredScope = false
    defer {
      if scopeWasStarted, !transferredScope {
        scopeAccess.stop(rootURL)
      }
    }

    let filesystemRootFD = open("/", O_RDONLY | O_DIRECTORY | O_CLOEXEC)
    guard filesystemRootFD >= 0 else {
      throw SidebarImportSourceWalkerError.openFailed(
        path: "/",
        reason: Self.posixReason())
    }
    defer { Darwin.close(filesystemRootFD) }

    let rootFD = try Self.openSourceRoot(
      rootURL,
      from: filesystemRootFD)
    var transferredRoot = false
    defer {
      if !transferredRoot {
        Darwin.close(rootFD)
      }
    }

    var rootMetadata = stat()
    guard fstat(rootFD, &rootMetadata) == 0 else {
      throw SidebarImportSourceWalkerError.inspectFailed(
        path: rootURL.path,
        reason: Self.posixReason())
    }

    var state = WalkState(limits: limits)
    switch rootMetadata.st_mode & S_IFMT {
    case S_IFREG:
      try state.append(
        components: [],
        kind: .regularFile,
        depth: 0,
        fileBytes: rootMetadata.st_size)
    case S_IFDIR:
      let vaultFD = try Self.openTrustedDirectory(
        vaultURL,
        from: filesystemRootFD)
      defer { Darwin.close(vaultFD) }
      if try Self.directory(rootFD, contains: vaultFD) {
        throw SidebarImportSourceWalkerError.sourceContainsVault(path: rootURL.path)
      }
      try state.append(
        components: [],
        kind: .directory,
        depth: 0,
        fileBytes: 0)
      let traversalFD = dup(rootFD)
      guard traversalFD >= 0 else {
        throw SidebarImportSourceWalkerError.openFailed(
          path: rootURL.path,
          reason: Self.posixReason())
      }
      _ = fcntl(traversalFD, F_SETFD, FD_CLOEXEC)
      if let reason = try Self.walkOwnedDirectory(
        traversalFD,
        components: [],
        depth: 0,
        includeSelf: false,
        state: &state)
      {
        throw SidebarImportSourceWalkerError.rejectedRoot(
          path: rootURL.path,
          reason: reason)
      }
    default:
      throw SidebarImportSourceWalkerError.rejectedRoot(
        path: rootURL.path,
        reason: .unsupported)
    }

    let prepared = SidebarImportPreparedSource(
      rootURL: rootURL,
      rootFD: rootFD,
      limits: limits,
      scopeAccess: scopeAccess,
      scopeWasStarted: scopeWasStarted,
      manifest: SidebarImportSourceManifest(
        entries: state.entries,
        failures: state.failures))
    transferredRoot = true
    transferredScope = true
    return prepared
  }

  private struct WalkState {
    let limits: SidebarImportSourceLimits
    var entries: [SidebarImportSourceEntry] = []
    var failures: [SidebarImportSourceFailure] = []
    var totalBytes: Int64 = 0

    mutating func append(
      components: [String],
      kind: SidebarImportSourceEntryKind,
      depth: Int,
      fileBytes: Int64
    ) throws {
      let display = components.joined(separator: "/")
      guard depth <= limits.maximumDepth else {
        throw SidebarImportSourceWalkerError.tooDeep(
          path: display,
          limit: limits.maximumDepth)
      }
      guard entries.count < limits.maximumEntries else {
        throw SidebarImportSourceWalkerError.tooManyEntries(
          limit: limits.maximumEntries)
      }
      guard fileBytes >= 0, fileBytes <= limits.refuseBytes else {
        throw SidebarImportSourceWalkerError.fileTooLarge(
          path: display,
          limitBytes: limits.refuseBytes)
      }
      let (newTotal, overflow) = totalBytes.addingReportingOverflow(fileBytes)
      guard !overflow, newTotal <= limits.totalBytes else {
        throw SidebarImportSourceWalkerError.totalTooLarge(
          limitBytes: limits.totalBytes)
      }
      totalBytes = newTotal
      entries.append(
        SidebarImportSourceEntry(
          relativePath: SidebarImportSourceRelativePath(components: components),
          kind: kind))
    }

    mutating func reject(
      components: [String],
      reason: SidebarImportSourceFailureReason
    ) {
      failures.append(
        SidebarImportSourceFailure(
          relativePath: SidebarImportSourceRelativePath(components: components),
          reason: reason))
    }
  }

  private static func walkOwnedDirectory(
    _ directoryFD: Int32,
    components: [String],
    depth: Int,
    includeSelf: Bool,
    state: inout WalkState
  ) throws -> SidebarImportSourceFailureReason? {
    guard let directory = fdopendir(directoryFD) else {
      Darwin.close(directoryFD)
      return .unreadable
    }
    defer { closedir(directory) }

    var names: [String] = []
    errno = 0
    while let entry = readdir(directory) {
      let length = Int(entry.pointee.d_namlen)
      let name = withUnsafePointer(to: &entry.pointee.d_name) { pointer -> String? in
        pointer.withMemoryRebound(to: UInt8.self, capacity: length) { bytes in
          String(
            bytes: UnsafeBufferPointer(start: bytes, count: length),
            encoding: .utf8)
        }
      }
      guard let name else {
        throw SidebarImportSourceWalkerError.invalidFileName
      }
      if name != ".", name != ".." {
        names.append(name)
      }
      errno = 0
    }
    if errno != 0 {
      return .unreadable
    }

    if includeSelf {
      try state.append(
        components: components,
        kind: .directory,
        depth: depth,
        fileBytes: 0)
    }

    names.sort { left, right in
      left.utf8.lexicographicallyPrecedes(right.utf8)
    }

    let parentFD = dirfd(directory)
    for name in names {
      let childComponents = components + [name]
      if name.hasPrefix(".") {
        state.reject(components: childComponents, reason: .hidden)
        continue
      }

      var metadata = stat()
      let inspectResult = name.withCString { component in
        fstatat(parentFD, component, &metadata, AT_SYMLINK_NOFOLLOW)
      }
      guard inspectResult == 0 else {
        state.reject(components: childComponents, reason: .unreadable)
        continue
      }
      if let reason = rejectionReason(for: metadata) {
        state.reject(components: childComponents, reason: reason)
        continue
      }

      let childDepth = depth + 1
      switch metadata.st_mode & S_IFMT {
      case S_IFDIR:
        let childFD = name.withCString { component in
          openat(
            parentFD,
            component,
            O_RDONLY | O_DIRECTORY | O_NOFOLLOW | O_CLOEXEC
          )
        }
        guard childFD >= 0 else {
          state.reject(
            components: childComponents,
            reason: rejectionReasonAfterOpenFailure(parentFD: parentFD, name: name))
          continue
        }
        var openedMetadata = stat()
        guard fstat(childFD, &openedMetadata) == 0 else {
          Darwin.close(childFD)
          state.reject(components: childComponents, reason: .unreadable)
          continue
        }
        guard openedMetadata.st_mode & S_IFMT == S_IFDIR,
          rejectionReason(for: openedMetadata) == nil
        else {
          Darwin.close(childFD)
          state.reject(components: childComponents, reason: .unsupported)
          continue
        }
        if let reason = try walkOwnedDirectory(
          childFD,
          components: childComponents,
          depth: childDepth,
          includeSelf: true,
          state: &state)
        {
          state.reject(components: childComponents, reason: reason)
        }
      case S_IFREG:
        let childFD = name.withCString { component in
          openat(
            parentFD,
            component,
            O_RDONLY | O_NONBLOCK | O_NOFOLLOW | O_CLOEXEC
          )
        }
        guard childFD >= 0 else {
          state.reject(
            components: childComponents,
            reason: rejectionReasonAfterOpenFailure(parentFD: parentFD, name: name))
          continue
        }
        var openedMetadata = stat()
        let inspectOpenedResult = fstat(childFD, &openedMetadata)
        Darwin.close(childFD)
        guard inspectOpenedResult == 0 else {
          state.reject(components: childComponents, reason: .unreadable)
          continue
        }
        guard openedMetadata.st_mode & S_IFMT == S_IFREG,
          rejectionReason(for: openedMetadata) == nil
        else {
          state.reject(components: childComponents, reason: .unsupported)
          continue
        }
        try state.append(
          components: childComponents,
          kind: .regularFile,
          depth: childDepth,
          fileBytes: openedMetadata.st_size)
      default:
        state.reject(components: childComponents, reason: .unsupported)
      }
    }
    return nil
  }

  private struct FileIdentity: Equatable {
    let device: dev_t
    let inode: ino_t
  }

  private static func openSourceRoot(
    _ rootURL: URL,
    from filesystemRootFD: Int32
  ) throws -> Int32 {
    let requestedPath = rootURL.path
    let components = try absoluteComponents(of: rootURL)
    let initialFD = dup(filesystemRootFD)
    guard initialFD >= 0 else {
      throw SidebarImportSourceWalkerError.openFailed(
        path: requestedPath,
        reason: posixReason())
    }
    _ = fcntl(initialFD, F_SETFD, FD_CLOEXEC)

    var currentFD = initialFD
    do {
      for (index, component) in components.enumerated() {
        if component.hasPrefix(".") {
          throw SidebarImportSourceWalkerError.rejectedRoot(
            path: requestedPath,
            reason: .hidden)
        }

        var metadata = stat()
        let inspectResult = component.withCString { name in
          fstatat(currentFD, name, &metadata, AT_SYMLINK_NOFOLLOW)
        }
        guard inspectResult == 0 else {
          if errno == EACCES || errno == EPERM {
            throw SidebarImportSourceWalkerError.rejectedRoot(
              path: requestedPath,
              reason: .unreadable)
          }
          throw SidebarImportSourceWalkerError.openFailed(
            path: requestedPath,
            reason: posixReason())
        }
        if let reason = rejectionReason(for: metadata) {
          throw SidebarImportSourceWalkerError.rejectedRoot(
            path: requestedPath,
            reason: reason)
        }

        let isFinal = index == components.index(before: components.endIndex)
        if !isFinal, metadata.st_mode & S_IFMT != S_IFDIR {
          throw SidebarImportSourceWalkerError.rejectedRoot(
            path: requestedPath,
            reason: .notDirectory)
        }
        let flags =
          metadata.st_mode & S_IFMT == S_IFDIR
          ? O_RDONLY | O_DIRECTORY | O_NOFOLLOW | O_CLOEXEC
          : O_RDONLY | O_NONBLOCK | O_NOFOLLOW | O_CLOEXEC
        let nextFD = component.withCString { name in
          openat(currentFD, name, flags)
        }
        guard nextFD >= 0 else {
          let reason = rejectionReasonAfterOpenFailure(
            parentFD: currentFD,
            name: component)
          throw SidebarImportSourceWalkerError.rejectedRoot(
            path: requestedPath,
            reason: reason)
        }
        Darwin.close(currentFD)
        currentFD = nextFD

        var openedMetadata = stat()
        guard fstat(currentFD, &openedMetadata) == 0 else {
          throw SidebarImportSourceWalkerError.rejectedRoot(
            path: requestedPath,
            reason: .unreadable)
        }
        if let reason = rejectionReason(for: openedMetadata) {
          throw SidebarImportSourceWalkerError.rejectedRoot(
            path: requestedPath,
            reason: reason)
        }
        if !isFinal, openedMetadata.st_mode & S_IFMT != S_IFDIR {
          throw SidebarImportSourceWalkerError.rejectedRoot(
            path: requestedPath,
            reason: .notDirectory)
        }
      }
      return currentFD
    } catch {
      Darwin.close(currentFD)
      throw error
    }
  }

  private static func openTrustedDirectory(
    _ url: URL,
    from filesystemRootFD: Int32
  ) throws -> Int32 {
    let components = try absoluteComponents(of: url)
    let initialFD = dup(filesystemRootFD)
    guard initialFD >= 0 else {
      throw SidebarImportSourceWalkerError.openFailed(
        path: url.path,
        reason: posixReason())
    }
    _ = fcntl(initialFD, F_SETFD, FD_CLOEXEC)

    var currentFD = initialFD
    do {
      for component in components {
        let nextFD = component.withCString { name in
          openat(currentFD, name, O_RDONLY | O_DIRECTORY | O_CLOEXEC)
        }
        guard nextFD >= 0 else {
          throw SidebarImportSourceWalkerError.openFailed(
            path: url.path,
            reason: posixReason())
        }
        Darwin.close(currentFD)
        currentFD = nextFD
      }
      return currentFD
    } catch {
      Darwin.close(currentFD)
      throw error
    }
  }

  private static func absoluteComponents(of url: URL) throws -> [String] {
    let path = url.path
    guard url.isFileURL, path.hasPrefix("/") else {
      throw SidebarImportSourceWalkerError.rejectedRoot(
        path: path,
        reason: .invalidPath)
    }
    let components = path.split(separator: "/", omittingEmptySubsequences: true).map {
      String($0)
    }
    guard components.allSatisfy({ $0 != "." && $0 != ".." }) else {
      throw SidebarImportSourceWalkerError.rejectedRoot(
        path: path,
        reason: .invalidPath)
    }
    return components
  }

  private static func directory(
    _ sourceFD: Int32,
    contains vaultFD: Int32
  ) throws -> Bool {
    let sourceIdentity = try identity(of: sourceFD, path: "source root")
    var currentFD = dup(vaultFD)
    guard currentFD >= 0 else {
      throw SidebarImportSourceWalkerError.openFailed(
        path: "vault",
        reason: posixReason())
    }
    _ = fcntl(currentFD, F_SETFD, FD_CLOEXEC)
    defer { Darwin.close(currentFD) }

    while true {
      let currentIdentity = try identity(of: currentFD, path: "vault")
      if currentIdentity == sourceIdentity {
        return true
      }

      let parentFD = "..".withCString { name in
        openat(
          currentFD,
          name,
          O_RDONLY | O_DIRECTORY | O_NOFOLLOW | O_CLOEXEC
        )
      }
      guard parentFD >= 0 else {
        throw SidebarImportSourceWalkerError.openFailed(
          path: "vault ancestor",
          reason: posixReason())
      }
      let parentIdentity: FileIdentity
      do {
        parentIdentity = try identity(of: parentFD, path: "vault ancestor")
      } catch {
        Darwin.close(parentFD)
        throw error
      }
      if parentIdentity == currentIdentity {
        Darwin.close(parentFD)
        return false
      }
      Darwin.close(currentFD)
      currentFD = parentFD
    }
  }

  private static func identity(of descriptor: Int32, path: String) throws -> FileIdentity {
    var metadata = stat()
    guard fstat(descriptor, &metadata) == 0 else {
      throw SidebarImportSourceWalkerError.inspectFailed(
        path: path,
        reason: posixReason())
    }
    return FileIdentity(device: metadata.st_dev, inode: metadata.st_ino)
  }

  private static func rejectionReason(
    for metadata: stat
  ) -> SidebarImportSourceFailureReason? {
    switch metadata.st_mode & S_IFMT {
    case S_IFREG:
      return hasAnyPermission(metadata.st_mode, S_IRUSR | S_IRGRP | S_IROTH)
        ? nil : .unreadable
    case S_IFDIR:
      let readable = hasAnyPermission(metadata.st_mode, S_IRUSR | S_IRGRP | S_IROTH)
      let searchable = hasAnyPermission(metadata.st_mode, S_IXUSR | S_IXGRP | S_IXOTH)
      return readable && searchable ? nil : .unreadable
    case S_IFLNK:
      return .symbolicLink
    case S_IFIFO:
      return .fifo
    case S_IFSOCK:
      return .socket
    case S_IFCHR:
      return .characterDevice
    case S_IFBLK:
      return .blockDevice
    default:
      return .unsupported
    }
  }

  private static func rejectionReasonAfterOpenFailure(
    parentFD: Int32,
    name: String
  ) -> SidebarImportSourceFailureReason {
    var metadata = stat()
    let result = name.withCString { component in
      fstatat(parentFD, component, &metadata, AT_SYMLINK_NOFOLLOW)
    }
    guard result == 0 else { return .unreadable }
    return rejectionReason(for: metadata) ?? .unreadable
  }

  private static func hasAnyPermission(_ mode: mode_t, _ permissions: mode_t) -> Bool {
    mode & permissions != 0
  }

  private static func posixReason(_ code: Int32 = errno) -> String {
    String(cString: strerror(code))
  }
}
