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

  fileprivate init(entries: [SidebarImportSourceEntry]) {
    self.entries = SidebarImportSourceEntries(entries)
  }
}

enum SidebarImportSourceWalkerError: Error {
  case invalidLimits
  case securityScopeDenied
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
  private let lock = NSLock()
  private var rootFD: Int32

  fileprivate init(
    rootURL: URL,
    rootFD: Int32,
    limits: SidebarImportSourceLimits,
    scopeAccess: SidebarImportSecurityScopeAccess,
    manifest: SidebarImportSourceManifest
  ) {
    self.rootURL = rootURL
    self.rootFD = rootFD
    self.limits = limits
    self.scopeAccess = scopeAccess
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
    guard let fileName = components.last else {
      throw SidebarImportSourceWalkerError.notARegularFile(path: "")
    }

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

    var metadata = stat()
    guard fstat(fileFD, &metadata) == 0 else {
      throw SidebarImportSourceWalkerError.inspectFailed(
        path: entry.relativePath.display,
        reason: Self.posixReason())
    }
    guard metadata.st_mode & S_IFMT == S_IFREG else {
      throw SidebarImportSourceWalkerError.notARegularFile(
        path: entry.relativePath.display)
    }
    let size = metadata.st_size
    guard size >= 0, size <= limits.refuseBytes else {
      throw SidebarImportSourceWalkerError.fileTooLarge(
        path: entry.relativePath.display,
        limitBytes: limits.refuseBytes)
    }

    var output = Data()
    if size <= Int.max {
      output.reserveCapacity(Int(size))
    }
    var buffer = [UInt8](repeating: 0, count: 64 * 1_024)
    while true {
      let count = buffer.withUnsafeMutableBytes { bytes in
        Darwin.read(fileFD, bytes.baseAddress, bytes.count)
      }
      if count > 0 {
        guard Int64(output.count) + Int64(count) <= limits.refuseBytes else {
          throw SidebarImportSourceWalkerError.fileTooLarge(
            path: entry.relativePath.display,
            limitBytes: limits.refuseBytes)
        }
        buffer.withUnsafeBytes { bytes in
          output.append(bytes.bindMemory(to: UInt8.self).baseAddress!, count: count)
        }
      } else if count == 0 {
        return output
      } else if errno != EINTR {
        throw SidebarImportSourceWalkerError.readFailed(
          path: entry.relativePath.display,
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
    scopeAccess.stop(rootURL)
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
    _ = vaultURL
    guard limits.refuseBytes >= 0,
      limits.totalBytes >= 0,
      limits.maximumEntries > 0,
      limits.maximumDepth >= 0
    else {
      throw SidebarImportSourceWalkerError.invalidLimits
    }
    guard scopeAccess.start(rootURL) else {
      throw SidebarImportSourceWalkerError.securityScopeDenied
    }

    var shouldStopScope = true
    defer {
      if shouldStopScope {
        scopeAccess.stop(rootURL)
      }
    }

    let rootFD = open(
      rootURL.path,
      O_RDONLY | O_DIRECTORY | O_NOFOLLOW | O_CLOEXEC
    )
    guard rootFD >= 0 else {
      throw SidebarImportSourceWalkerError.openFailed(
        path: rootURL.path,
        reason: Self.posixReason())
    }

    do {
      var state = WalkState(limits: limits)
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
      try Self.walkOwnedDirectory(
        traversalFD,
        components: [],
        depth: 0,
        state: &state)

      let prepared = SidebarImportPreparedSource(
        rootURL: rootURL,
        rootFD: rootFD,
        limits: limits,
        scopeAccess: scopeAccess,
        manifest: SidebarImportSourceManifest(entries: state.entries))
      shouldStopScope = false
      return prepared
    } catch {
      Darwin.close(rootFD)
      throw error
    }
  }

  private struct WalkState {
    let limits: SidebarImportSourceLimits
    var entries: [SidebarImportSourceEntry] = []
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
  }

  private static func walkOwnedDirectory(
    _ directoryFD: Int32,
    components: [String],
    depth: Int,
    state: inout WalkState
  ) throws {
    guard let directory = fdopendir(directoryFD) else {
      let code = errno
      Darwin.close(directoryFD)
      throw SidebarImportSourceWalkerError.openFailed(
        path: components.joined(separator: "/"),
        reason: posixReason(code))
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
      throw SidebarImportSourceWalkerError.readFailed(
        path: components.joined(separator: "/"),
        reason: posixReason())
    }

    names.sort { left, right in
      left.utf8.lexicographicallyPrecedes(right.utf8)
    }

    let parentFD = dirfd(directory)
    for name in names {
      var metadata = stat()
      let inspectResult = name.withCString { component in
        fstatat(parentFD, component, &metadata, AT_SYMLINK_NOFOLLOW)
      }
      guard inspectResult == 0 else {
        throw SidebarImportSourceWalkerError.inspectFailed(
          path: (components + [name]).joined(separator: "/"),
          reason: posixReason())
      }

      let childComponents = components + [name]
      let childDepth = depth + 1
      switch metadata.st_mode & S_IFMT {
      case S_IFDIR:
        try state.append(
          components: childComponents,
          kind: .directory,
          depth: childDepth,
          fileBytes: 0)
        let childFD = name.withCString { component in
          openat(
            parentFD,
            component,
            O_RDONLY | O_DIRECTORY | O_NOFOLLOW | O_CLOEXEC
          )
        }
        guard childFD >= 0 else {
          throw SidebarImportSourceWalkerError.openFailed(
            path: childComponents.joined(separator: "/"),
            reason: posixReason())
        }
        try walkOwnedDirectory(
          childFD,
          components: childComponents,
          depth: childDepth,
          state: &state)
      case S_IFREG:
        try state.append(
          components: childComponents,
          kind: .regularFile,
          depth: childDepth,
          fileBytes: metadata.st_size)
      default:
        throw SidebarImportSourceWalkerError.unsupportedEntry(
          path: childComponents.joined(separator: "/"))
      }
    }
  }

  private static func posixReason(_ code: Int32 = errno) -> String {
    String(cString: strerror(code))
  }
}
