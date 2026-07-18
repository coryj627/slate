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

struct SidebarImportSourceWalkerHooks {
  let didAcquireReadLease: @Sendable () -> Void
  /// Runs inside an operation lease; never call blocking cancellation inline.
  let didAcquireOperationLease: @Sendable (SidebarImportSourceBoundary) -> Void
  /// Runs inside a winning descriptor-operation lease. Implementations must
  /// request cancellation from another execution context, never inline.
  let didOpenDescriptorBeforeAdoption: @Sendable (String) -> Void
  let didInspectDescendant: @Sendable (String) -> Void
  let didReachBoundary: @Sendable (SidebarImportSourceBoundary) -> Void
  let injectedDirectoryRecords: @Sendable (String) -> [[UInt8]]

  init(
    didAcquireReadLease: @escaping @Sendable () -> Void = {},
    didAcquireOperationLease: @escaping @Sendable (SidebarImportSourceBoundary) -> Void = {
      _ in
    },
    didOpenDescriptorBeforeAdoption: @escaping @Sendable (String) -> Void = { _ in },
    didInspectDescendant: @escaping @Sendable (String) -> Void = { _ in },
    didReachBoundary: @escaping @Sendable (SidebarImportSourceBoundary) -> Void = { _ in },
    injectedDirectoryRecords: @escaping @Sendable (String) -> [[UInt8]] = { _ in [] }
  ) {
    self.didAcquireReadLease = didAcquireReadLease
    self.didAcquireOperationLease = didAcquireOperationLease
    self.didOpenDescriptorBeforeAdoption = didOpenDescriptorBeforeAdoption
    self.didInspectDescendant = didInspectDescendant
    self.didReachBoundary = didReachBoundary
    self.injectedDirectoryRecords = injectedDirectoryRecords
  }
}

enum SidebarImportSourceBoundary: Equatable, Sendable {
  case beforeRootAdmission
  case beforeRootOpen
  case beforeDirectoryRead(String)
  case afterDirectoryRead(String, reachedEnd: Bool)
  case beforeInspect(String)
  case beforeOpen(String)
  case beforeRecursion(String)
  case beforeRead(String, offset: Int64)
  case afterRead(String, offset: Int64, reachedEnd: Bool)
  case afterInterruptedRead(String)
}

final class SidebarImportEngineSignal: @unchecked Sendable {
  let condition = NSCondition()
  private var cancelled = false
  private var activeOperationLeases = 0

  func requestCancellation() {
    condition.lock()
    cancelled = true
    condition.broadcast()
    condition.unlock()
  }

  func cancel() {
    condition.lock()
    cancelled = true
    condition.broadcast()
    while activeOperationLeases > 0 {
      condition.wait()
    }
    condition.unlock()
  }

  var isCancelled: Bool {
    condition.lock()
    defer { condition.unlock() }
    return cancelled
  }

  var isCancelledWithConditionHeld: Bool {
    cancelled
  }

  func beginOperation() -> SidebarImportEngineOperationLease? {
    condition.lock()
    guard !cancelled else {
      condition.unlock()
      return nil
    }
    activeOperationLeases += 1
    condition.unlock()
    return SidebarImportEngineOperationLease(signal: self)
  }

  fileprivate func finishOperation() {
    condition.lock()
    precondition(activeOperationLeases > 0)
    activeOperationLeases -= 1
    if activeOperationLeases == 0 {
      condition.broadcast()
    }
    condition.unlock()
  }
}

typealias SidebarImportSourceCancellationToken = SidebarImportEngineSignal

final class SidebarImportEngineOperationLease: @unchecked Sendable {
  private let lock = NSLock()
  private var signal: SidebarImportEngineSignal?

  fileprivate init(signal: SidebarImportEngineSignal) {
    self.signal = signal
  }

  deinit {
    release()
  }

  func release() {
    lock.lock()
    let ownedSignal = signal
    signal = nil
    lock.unlock()
    ownedSignal?.finishOperation()
  }
}

struct SidebarImportSourceMetricsSnapshot: Equatable, Sendable {
  let currentOwnedDescriptors: Int
  let highWaterOwnedDescriptors: Int
  let activeReads: Int
  let highWaterActiveReads: Int
  let totalReadRequestBytes: Int64
  let maximumReservedCapacity: Int
  let totalReadOperations: Int
  let totalDirectoryReadOperations: Int
}

final class SidebarImportSourceMetrics: @unchecked Sendable {
  private let lock = NSLock()
  private var currentOwnedDescriptors = 0
  private var highWaterOwnedDescriptors = 0
  private var activeReads = 0
  private var highWaterActiveReads = 0
  private var totalReadRequestBytes: Int64 = 0
  private var maximumReservedCapacity = 0
  private var totalReadOperations = 0
  private var totalDirectoryReadOperations = 0

  fileprivate func descriptorOpened() {
    lock.lock()
    currentOwnedDescriptors += 1
    highWaterOwnedDescriptors = max(highWaterOwnedDescriptors, currentOwnedDescriptors)
    lock.unlock()
  }

  fileprivate func descriptorClosed() {
    lock.lock()
    precondition(currentOwnedDescriptors > 0)
    currentOwnedDescriptors -= 1
    lock.unlock()
  }

  fileprivate func readStarted() {
    lock.lock()
    activeReads += 1
    highWaterActiveReads = max(highWaterActiveReads, activeReads)
    lock.unlock()
  }

  fileprivate func readFinished() {
    lock.lock()
    precondition(activeReads > 0)
    activeReads -= 1
    lock.unlock()
  }

  fileprivate func recordReadRequest(offset: Int64, count: Int) {
    lock.lock()
    let (end, overflow) = offset.addingReportingOverflow(Int64(count))
    if !overflow {
      totalReadRequestBytes = max(totalReadRequestBytes, end)
    }
    lock.unlock()
  }

  fileprivate func recordReservedCapacity(_ capacity: Int) {
    lock.lock()
    maximumReservedCapacity = max(maximumReservedCapacity, capacity)
    lock.unlock()
  }

  fileprivate func readOperationStarted() {
    lock.lock()
    totalReadOperations += 1
    lock.unlock()
  }

  fileprivate func directoryReadOperationStarted() {
    lock.lock()
    totalDirectoryReadOperations += 1
    lock.unlock()
  }

  func snapshot() -> SidebarImportSourceMetricsSnapshot {
    lock.lock()
    defer { lock.unlock() }
    return SidebarImportSourceMetricsSnapshot(
      currentOwnedDescriptors: currentOwnedDescriptors,
      highWaterOwnedDescriptors: highWaterOwnedDescriptors,
      activeReads: activeReads,
      highWaterActiveReads: highWaterActiveReads,
      totalReadRequestBytes: totalReadRequestBytes,
      maximumReservedCapacity: maximumReservedCapacity,
      totalReadOperations: totalReadOperations,
      totalDirectoryReadOperations: totalDirectoryReadOperations)
  }
}

enum SidebarImportSourceNameOrdering {
  private static let invariantLocale = Locale(identifier: "en_US_POSIX")

  static func precedes(_ left: [UInt8], _ right: [UInt8]) -> Bool {
    let leftKey = primaryKey(left)
    let rightKey = primaryKey(right)
    if leftKey != rightKey {
      return leftKey.lexicographicallyPrecedes(rightKey)
    }
    return left.lexicographicallyPrecedes(right)
  }

  fileprivate static func primaryKey(_ rawName: [UInt8]) -> [UInt8] {
    Array(
      String(decoding: rawName, as: UTF8.self)
        .folding(options: .caseInsensitive, locale: invariantLocale)
        .utf8)
  }
}

struct SidebarImportSourceLimits {
  static let bridgeRefuseBytes = Int64(Int32.max) - 4
  static let productionTotalBytes: Int64 = 1_073_741_824
  static let productionMaximumEntries = 10_000
  static let productionMaximumDepth = 64

  let refuseBytes: Int64
  let totalBytes: Int64
  let maximumEntries: Int
  let maximumDepth: Int

  init(
    refuseBytes: Int64,
    totalBytes: Int64,
    maximumEntries: Int,
    maximumDepth: Int
  ) {
    self.refuseBytes = min(refuseBytes, Self.bridgeRefuseBytes)
    self.totalBytes = totalBytes
    self.maximumEntries = maximumEntries
    self.maximumDepth = maximumDepth
  }

  init(
    sessionRefuseBytes: UInt64,
    totalBytes: Int64 = productionTotalBytes,
    maximumEntries: Int = productionMaximumEntries,
    maximumDepth: Int = productionMaximumDepth
  ) {
    refuseBytes = Int64(min(sessionRefuseBytes, UInt64(Self.bridgeRefuseBytes)))
    self.totalBytes = totalBytes
    self.maximumEntries = maximumEntries
    self.maximumDepth = maximumDepth
  }
}

enum SidebarImportSourceEntryKind: Equatable, Sendable {
  case directory
  case regularFile
}

struct SidebarImportSourceRelativePath: Equatable, Sendable {
  let display: String

  var leafName: String? { components.last }

  var parentDisplay: String {
    components.dropLast().joined(separator: "/")
  }

  var directoryAncestorDisplays: [String] {
    guard !components.isEmpty else { return [] }
    return stride(from: components.count - 1, through: 0, by: -1).map {
      components.prefix($0).joined(separator: "/")
    }
  }

  fileprivate let components: [String]

  fileprivate init(components: [String]) {
    self.components = components
    display = components.joined(separator: "/")
  }
}

struct SidebarImportSourceEntry: Equatable, Sendable {
  let relativePath: SidebarImportSourceRelativePath
  let kind: SidebarImportSourceEntryKind
  let advisoryByteCount: UInt64
  let traversalOrdinal: Int
}

struct SidebarImportSourceAdvisoryByteCount: Equatable, Sendable {
  private(set) var totalBytes: UInt64 = 0
  private(set) var overflowed = false
  private(set) var exceedsConfiguredTotal = false

  private let configuredTotalBytes: UInt64

  init(configuredTotalBytes: UInt64) {
    self.configuredTotalBytes = configuredTotalBytes
  }

  mutating func record(_ byteCount: UInt64) {
    let (sum, didOverflow) = totalBytes.addingReportingOverflow(byteCount)
    if didOverflow {
      totalBytes = UInt64.max
      overflowed = true
    } else {
      totalBytes = sum
    }
    if overflowed || totalBytes > configuredTotalBytes {
      exceedsConfiguredTotal = true
    }
  }

  static func == (
    left: SidebarImportSourceAdvisoryByteCount,
    right: SidebarImportSourceAdvisoryByteCount
  ) -> Bool {
    left.totalBytes == right.totalBytes
      && left.overflowed == right.overflowed
      && left.exceedsConfiguredTotal == right.exceedsConfiguredTotal
  }
}

enum SidebarImportSourceFailureReason: Equatable, Sendable {
  case hidden
  case symbolicLink
  case fifo
  case socket
  case characterDevice
  case blockDevice
  case unreadable
  case entryKindChanged
  case notDirectory
  case unsupported
  case invalidPath
  case invalidFileName
  case tooDeep
  case fileTooLarge(limitBytes: Int64)
}

struct SidebarImportSourceFailure: Equatable, Sendable {
  let relativePath: SidebarImportSourceRelativePath
  let reason: SidebarImportSourceFailureReason
  let traversalOrdinal: Int
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
  let advisoryByteCount: SidebarImportSourceAdvisoryByteCount
  let encounteredEntryCount: Int

  fileprivate init(
    entries: [SidebarImportSourceEntry],
    failures: [SidebarImportSourceFailure],
    advisoryByteCount: SidebarImportSourceAdvisoryByteCount,
    encounteredEntryCount: Int
  ) {
    self.entries = SidebarImportSourceEntries(entries)
    self.failures = failures
    self.advisoryByteCount = advisoryByteCount
    self.encounteredEntryCount = encounteredEntryCount
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
  case tooManyEntries(limit: Int)
  case tooDeep(path: String, limit: Int)
  case fileTooLarge(path: String, limitBytes: Int64)
  case preparedSourceClosed
  case notARegularFile(path: String)
  case readFailed(path: String, reason: String)
  case cancelled
}

private typealias SidebarImportOpenResult = (descriptor: Int32, error: Int32)

private func performSidebarImportOpen(
  at boundary: SidebarImportSourceBoundary,
  adoptionPath: String,
  didReachBoundary: @Sendable (SidebarImportSourceBoundary) -> Void,
  didAcquireOperationLease: @Sendable (SidebarImportSourceBoundary) -> Void,
  didOpenDescriptorBeforeAdoption: @Sendable (String) -> Void,
  cancellation: SidebarImportSourceCancellationToken,
  metrics: SidebarImportSourceMetrics,
  checkCancellation: () throws -> Void,
  open: () -> SidebarImportOpenResult,
  adopt: (Int32) -> Void
) throws -> Int32? {
  didReachBoundary(boundary)
  guard let operationLease = cancellation.beginOperation() else {
    try checkCancellation()
    throw SidebarImportSourceWalkerError.cancelled
  }
  didAcquireOperationLease(boundary)
  defer { operationLease.release() }

  let result = open()
  guard result.descriptor >= 0 else { return result.error }

  didOpenDescriptorBeforeAdoption(adoptionPath)
  metrics.descriptorOpened()
  do {
    try checkCancellation()
  } catch {
    Darwin.close(result.descriptor)
    metrics.descriptorClosed()
    throw error
  }
  adopt(result.descriptor)
  return nil
}

private final class SidebarImportPreparedSourceReadAuthority: @unchecked Sendable {
  let descriptor: Int32

  private let lock = NSLock()
  private var releaseAction: (() -> Void)?

  init(descriptor: Int32, release: @escaping () -> Void) {
    self.descriptor = descriptor
    releaseAction = release
  }

  deinit {
    release()
  }

  func release() {
    lock.lock()
    let action = releaseAction
    releaseAction = nil
    lock.unlock()
    action?()
  }
}

final class SidebarImportReservedRead: @unchecked Sendable {
  private let lock = NSLock()
  private var readAction: (() throws -> SidebarImportReadyRead)?

  fileprivate init(
    read: @escaping () throws -> SidebarImportReadyRead
  ) {
    readAction = read
  }

  func readBytes() throws -> SidebarImportReadyRead {
    lock.lock()
    guard let action = readAction else {
      lock.unlock()
      throw SidebarImportByteSchedulerError.invalidReservation
    }
    readAction = nil
    lock.unlock()
    return try action()
  }
}

final class SidebarImportPreparedSource: @unchecked Sendable {
  let manifest: SidebarImportSourceManifest

  var sourceRootName: String { rootURL.lastPathComponent }

  private let rootURL: URL
  private let limits: SidebarImportSourceLimits
  private let scopeAccess: SidebarImportSecurityScopeAccess
  private let scopeWasStarted: Bool
  private let didAcquireReadLease: @Sendable () -> Void
  private let didAcquireOperationLease: @Sendable (SidebarImportSourceBoundary) -> Void
  private let didOpenDescriptorBeforeAdoption: @Sendable (String) -> Void
  private let didReachBoundary: @Sendable (SidebarImportSourceBoundary) -> Void
  private let cancellation: SidebarImportSourceCancellationToken
  private let metrics: SidebarImportSourceMetrics
  private let lock = NSLock()
  private var rootFD: Int32
  private var isClosed = false
  private var activeReads = 0

  fileprivate init(
    rootURL: URL,
    rootFD: Int32,
    limits: SidebarImportSourceLimits,
    scopeAccess: SidebarImportSecurityScopeAccess,
    scopeWasStarted: Bool,
    didAcquireReadLease: @escaping @Sendable () -> Void,
    didAcquireOperationLease: @escaping @Sendable (SidebarImportSourceBoundary) -> Void,
    didOpenDescriptorBeforeAdoption: @escaping @Sendable (String) -> Void,
    didReachBoundary: @escaping @Sendable (SidebarImportSourceBoundary) -> Void,
    cancellation: SidebarImportSourceCancellationToken,
    metrics: SidebarImportSourceMetrics,
    manifest: SidebarImportSourceManifest
  ) {
    self.rootURL = rootURL
    self.rootFD = rootFD
    self.limits = limits
    self.scopeAccess = scopeAccess
    self.scopeWasStarted = scopeWasStarted
    self.didAcquireReadLease = didAcquireReadLease
    self.didAcquireOperationLease = didAcquireOperationLease
    self.didOpenDescriptorBeforeAdoption = didOpenDescriptorBeforeAdoption
    self.didReachBoundary = didReachBoundary
    self.cancellation = cancellation
    self.metrics = metrics
    self.manifest = manifest
  }

  deinit {
    close()
  }

  func usesSignal(_ signal: SidebarImportEngineSignal) -> Bool {
    cancellation === signal
  }

  func readBytes(for entry: SidebarImportSourceEntry) throws -> Data {
    try throwIfCancelled()
    guard entry.kind == .regularFile else {
      throw SidebarImportSourceWalkerError.notARegularFile(
        path: entry.relativePath.display)
    }

    let authority = try beginRead()
    defer { authority.release() }
    let pinnedRoot = authority.descriptor

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
        metrics.descriptorClosed()
      }
    }

    for component in components.dropLast() {
      let parentFD = directoryFD
      let openError = try performSidebarImportOpen(
        at: .beforeOpen(entry.relativePath.display),
        adoptionPath: entry.relativePath.display,
        didReachBoundary: didReachBoundary,
        didAcquireOperationLease: didAcquireOperationLease,
        didOpenDescriptorBeforeAdoption: didOpenDescriptorBeforeAdoption,
        cancellation: cancellation,
        metrics: metrics,
        checkCancellation: { try throwIfCancelled() },
        open: {
          let descriptor = component.withCString { name in
            openat(
              parentFD,
              name,
              O_RDONLY | O_DIRECTORY | O_NOFOLLOW | O_CLOEXEC
            )
          }
          return (descriptor: descriptor, error: errno)
        },
        adopt: { descriptor in
          if ownsDirectoryFD {
            Darwin.close(directoryFD)
            metrics.descriptorClosed()
          }
          directoryFD = descriptor
          ownsDirectoryFD = true
        })
      if let openError {
        throw SidebarImportSourceWalkerError.openFailed(
          path: entry.relativePath.display,
          reason: Self.posixReason(openError))
      }
      try throwIfCancelled()
    }

    var fileFD: Int32 = -1
    defer {
      if fileFD >= 0 {
        Darwin.close(fileFD)
        metrics.descriptorClosed()
      }
    }
    let openError = try performSidebarImportOpen(
      at: .beforeOpen(entry.relativePath.display),
      adoptionPath: entry.relativePath.display,
      didReachBoundary: didReachBoundary,
      didAcquireOperationLease: didAcquireOperationLease,
      didOpenDescriptorBeforeAdoption: didOpenDescriptorBeforeAdoption,
      cancellation: cancellation,
      metrics: metrics,
      checkCancellation: { try throwIfCancelled() },
      open: {
        let descriptor = fileName.withCString { name in
          openat(
            directoryFD,
            name,
            O_RDONLY | O_NONBLOCK | O_NOFOLLOW | O_CLOEXEC
          )
        }
        return (descriptor: descriptor, error: errno)
      },
      adopt: { fileFD = $0 })
    if let openError {
      throw SidebarImportSourceWalkerError.openFailed(
        path: entry.relativePath.display,
        reason: Self.posixReason(openError))
    }
    try throwIfCancelled()

    return try readBytes(from: fileFD, path: entry.relativePath.display)
  }

  func readBytes(
    for entry: SidebarImportSourceEntry,
    scheduler: SidebarImportByteScheduler
  ) throws -> SidebarImportReadyRead {
    try reserveRead(for: entry, scheduler: scheduler).readBytes()
  }

  func reserveRead(
    for entry: SidebarImportSourceEntry,
    scheduler: SidebarImportByteScheduler
  ) throws -> SidebarImportReservedRead {
    guard scheduler.usesSignal(cancellation) else {
      throw SidebarImportByteSchedulerError.mismatchedCancellationSignal
    }
    try scheduler.checkCancellation()
    try throwIfCancelled()
    guard entry.kind == .regularFile else {
      throw SidebarImportSourceWalkerError.notARegularFile(
        path: entry.relativePath.display)
    }

    let reservation = try scheduler.reserve(advisoryByteCount: entry.advisoryByteCount)
    return SidebarImportReservedRead { [self] in
      do {
        return try readReservedBytes(
          for: entry,
          scheduler: scheduler,
          reservation: reservation)
      } catch {
        reservation.discard()
        throw error
      }
    }
  }

  private func readReservedBytes(
    for entry: SidebarImportSourceEntry,
    scheduler: SidebarImportByteScheduler,
    reservation: SidebarImportByteReservation
  ) throws -> SidebarImportReadyRead {
    try scheduler.checkCancellation()
    try throwIfCancelled()

    let authority = try beginRead()
    let pinnedRoot = authority.descriptor

    do {
      let components = entry.relativePath.components
      if components.isEmpty {
        return try readBytes(
          from: pinnedRoot,
          path: entry.relativePath.display,
          scheduler: scheduler,
          reservation: reservation,
          authority: authority)
      }
      let fileName = components[components.index(before: components.endIndex)]

      var directoryFD = pinnedRoot
      var ownsDirectoryFD = false
      defer {
        if ownsDirectoryFD {
          Darwin.close(directoryFD)
          metrics.descriptorClosed()
        }
      }

      for component in components.dropLast() {
        try scheduler.checkCancellation()
        let parentFD = directoryFD
        let openError = try performSidebarImportOpen(
          at: .beforeOpen(entry.relativePath.display),
          adoptionPath: entry.relativePath.display,
          didReachBoundary: didReachBoundary,
          didAcquireOperationLease: didAcquireOperationLease,
          didOpenDescriptorBeforeAdoption: didOpenDescriptorBeforeAdoption,
          cancellation: cancellation,
          metrics: metrics,
          checkCancellation: { try throwIfCancelled() },
          open: {
            let descriptor = component.withCString { name in
              openat(
                parentFD,
                name,
                O_RDONLY | O_DIRECTORY | O_NOFOLLOW | O_CLOEXEC
              )
            }
            return (descriptor: descriptor, error: errno)
          },
          adopt: { descriptor in
            if ownsDirectoryFD {
              Darwin.close(directoryFD)
              metrics.descriptorClosed()
            }
            directoryFD = descriptor
            ownsDirectoryFD = true
          })
        if let openError {
          throw SidebarImportSourceWalkerError.openFailed(
            path: entry.relativePath.display,
            reason: Self.posixReason(openError))
        }
        try throwIfCancelled()
      }

      try scheduler.checkCancellation()
      var fileFD: Int32 = -1
      defer {
        if fileFD >= 0 {
          Darwin.close(fileFD)
          metrics.descriptorClosed()
        }
      }
      let openError = try performSidebarImportOpen(
        at: .beforeOpen(entry.relativePath.display),
        adoptionPath: entry.relativePath.display,
        didReachBoundary: didReachBoundary,
        didAcquireOperationLease: didAcquireOperationLease,
        didOpenDescriptorBeforeAdoption: didOpenDescriptorBeforeAdoption,
        cancellation: cancellation,
        metrics: metrics,
        checkCancellation: { try throwIfCancelled() },
        open: {
          let descriptor = fileName.withCString { name in
            openat(
              directoryFD,
              name,
              O_RDONLY | O_NONBLOCK | O_NOFOLLOW | O_CLOEXEC
            )
          }
          return (descriptor: descriptor, error: errno)
        },
        adopt: { fileFD = $0 })
      if let openError {
        throw SidebarImportSourceWalkerError.openFailed(
          path: entry.relativePath.display,
          reason: Self.posixReason(openError))
      }
      try scheduler.checkCancellation()
      try throwIfCancelled()

      return try readBytes(
        from: fileFD,
        path: entry.relativePath.display,
        scheduler: scheduler,
        reservation: reservation,
        authority: authority)
    } catch {
      authority.release()
      throw error
    }
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
    let reserve = min(Int(size), 64 * 1_024)
    if reserve > 0 {
      output.reserveCapacity(reserve)
    }
    metrics.recordReservedCapacity(reserve)
    var buffer = [UInt8](repeating: 0, count: 64 * 1_024)
    var offset: off_t = 0
    let probeLimit = limits.refuseBytes + 1
    while true {
      let remaining = probeLimit - Int64(output.count)
      guard remaining > 0 else {
        throw SidebarImportSourceWalkerError.fileTooLarge(
          path: path,
          limitBytes: limits.refuseBytes)
      }
      let requestCount = min(buffer.count, Int(remaining))
      metrics.recordReadRequest(offset: Int64(offset), count: requestCount)
      let readResult = try performOperation(
        at: .beforeRead(path, offset: Int64(offset))
      ) {
        metrics.readOperationStarted()
        let count = buffer.withUnsafeMutableBytes { bytes in
          Darwin.pread(fileFD, bytes.baseAddress, requestCount, offset)
        }
        return (count: count, error: errno)
      }
      let count = readResult.count
      let readError = readResult.error
      try reachBoundary(
        .afterRead(path, offset: Int64(offset), reachedEnd: count == 0))
      if count > 0 {
        let (newCount, overflow) = Int64(output.count).addingReportingOverflow(Int64(count))
        guard !overflow, newCount <= limits.refuseBytes else {
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
      } else if readError == EINTR {
        try reachBoundary(.afterInterruptedRead(path))
      } else {
        throw SidebarImportSourceWalkerError.readFailed(
          path: path,
          reason: Self.posixReason(readError))
      }
    }
  }

  private func readBytes(
    from fileFD: Int32,
    path: String,
    scheduler: SidebarImportByteScheduler,
    reservation: SidebarImportByteReservation,
    authority: SidebarImportPreparedSourceReadAuthority
  ) throws -> SidebarImportReadyRead {
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

    try reservation.resize(toByteCount: UInt64(size))
    var approvedByteCount = UInt64(size)
    var output = Data()
    let reserve = min(Int(size), 64 * 1_024)
    if reserve > 0 {
      output.reserveCapacity(reserve)
    }
    metrics.recordReservedCapacity(reserve)
    var buffer = [UInt8](repeating: 0, count: 64 * 1_024)
    var offset: off_t = 0
    let probeLimit = limits.refuseBytes + 1
    while true {
      try scheduler.checkCancellation()
      let remaining = probeLimit - Int64(output.count)
      guard remaining > 0 else {
        throw SidebarImportSourceWalkerError.fileTooLarge(
          path: path,
          limitBytes: limits.refuseBytes)
      }
      let requestCount = min(buffer.count, Int(remaining))
      metrics.recordReadRequest(offset: Int64(offset), count: requestCount)
      let readResult = try performOperation(
        at: .beforeRead(path, offset: Int64(offset))
      ) {
        metrics.readOperationStarted()
        let count = buffer.withUnsafeMutableBytes { bytes in
          Darwin.pread(fileFD, bytes.baseAddress, requestCount, offset)
        }
        return (count: count, error: errno)
      }
      let count = readResult.count
      let readError = readResult.error
      try reachBoundary(
        .afterRead(path, offset: Int64(offset), reachedEnd: count == 0))
      try scheduler.checkCancellation()
      if count > 0 {
        let (newCount, overflow) = Int64(output.count).addingReportingOverflow(Int64(count))
        guard !overflow, newCount <= limits.refuseBytes else {
          throw SidebarImportSourceWalkerError.fileTooLarge(
            path: path,
            limitBytes: limits.refuseBytes)
        }
        let requestedByteCount = UInt64(newCount)
        if requestedByteCount > approvedByteCount {
          try reservation.resize(toByteCount: requestedByteCount)
          approvedByteCount = requestedByteCount
        }
        buffer.withUnsafeBytes { bytes in
          output.append(bytes.bindMemory(to: UInt8.self).baseAddress!, count: count)
        }
        offset += off_t(count)
      } else if count == 0 {
        try reservation.resize(toByteCount: UInt64(output.count))
        try scheduler.checkCancellation()
        return try reservation.makeReady(
          data: output,
          releaseSourceAuthority: { authority.release() })
      } else if readError == EINTR {
        try reachBoundary(.afterInterruptedRead(path))
      } else {
        throw SidebarImportSourceWalkerError.readFailed(
          path: path,
          reason: Self.posixReason(readError))
      }
    }
  }

  func close() {
    lock.lock()
    isClosed = true
    let authority = takeAuthorityIfUnusedLocked()
    lock.unlock()

    releaseAuthority(authority)
  }

  private func beginRead() throws -> SidebarImportPreparedSourceReadAuthority {
    try throwIfCancelled()
    lock.lock()
    guard !isClosed, rootFD >= 0 else {
      lock.unlock()
      throw SidebarImportSourceWalkerError.preparedSourceClosed
    }
    let descriptor = dup(rootFD)
    guard descriptor >= 0 else {
      lock.unlock()
      throw SidebarImportSourceWalkerError.openFailed(
        path: rootURL.path,
        reason: Self.posixReason())
    }
    activeReads += 1
    metrics.descriptorOpened()
    metrics.readStarted()
    lock.unlock()

    _ = fcntl(descriptor, F_SETFD, FD_CLOEXEC)
    didAcquireReadLease()
    do {
      try throwIfCancelled()
    } catch {
      Darwin.close(descriptor)
      metrics.descriptorClosed()
      finishRead()
      throw error
    }
    return SidebarImportPreparedSourceReadAuthority(
      descriptor: descriptor,
      release: { [self] in
        Darwin.close(descriptor)
        metrics.descriptorClosed()
        finishRead()
      })
  }

  private func finishRead() {
    lock.lock()
    precondition(activeReads > 0)
    activeReads -= 1
    metrics.readFinished()
    let authority = takeAuthorityIfUnusedLocked()
    lock.unlock()

    releaseAuthority(authority)
  }

  private func takeAuthorityIfUnusedLocked() -> (descriptor: Int32, stopScope: Bool)? {
    guard isClosed, activeReads == 0, rootFD >= 0 else { return nil }
    let descriptor = rootFD
    rootFD = -1
    return (descriptor, scopeWasStarted)
  }

  private func releaseAuthority(_ authority: (descriptor: Int32, stopScope: Bool)?) {
    guard let authority else { return }
    Darwin.close(authority.descriptor)
    metrics.descriptorClosed()
    if authority.stopScope {
      scopeAccess.stop(rootURL)
    }
  }

  private static func posixReason(_ code: Int32 = errno) -> String {
    String(cString: strerror(code))
  }

  private func reachBoundary(_ boundary: SidebarImportSourceBoundary) throws {
    didReachBoundary(boundary)
    try throwIfCancelled()
  }

  private func performOperation<Result>(
    at boundary: SidebarImportSourceBoundary,
    _ body: () throws -> Result
  ) throws -> Result {
    didReachBoundary(boundary)
    guard let operationLease = cancellation.beginOperation() else {
      close()
      throw SidebarImportSourceWalkerError.cancelled
    }
    didAcquireOperationLease(boundary)
    defer { operationLease.release() }
    return try body()
  }

  private func throwIfCancelled() throws {
    if cancellation.isCancelled {
      close()
      throw SidebarImportSourceWalkerError.cancelled
    }
  }
}

struct SidebarImportSourceWalker {
  let limits: SidebarImportSourceLimits
  let scopeAccess: SidebarImportSecurityScopeAccess
  let hooks: SidebarImportSourceWalkerHooks
  let cancellation: SidebarImportSourceCancellationToken
  let metrics: SidebarImportSourceMetrics

  init(
    limits: SidebarImportSourceLimits,
    scopeAccess: SidebarImportSecurityScopeAccess,
    hooks: SidebarImportSourceWalkerHooks = SidebarImportSourceWalkerHooks(),
    cancellation: SidebarImportSourceCancellationToken =
      SidebarImportSourceCancellationToken(),
    metrics: SidebarImportSourceMetrics = SidebarImportSourceMetrics()
  ) {
    self.limits = limits
    self.scopeAccess = scopeAccess
    self.hooks = hooks
    self.cancellation = cancellation
    self.metrics = metrics
  }

  func prepare(rootURL: URL, vaultURL: URL) throws -> SidebarImportPreparedSource {
    guard limits.refuseBytes >= 0,
      limits.totalBytes >= 0,
      limits.maximumEntries > 0,
      limits.maximumDepth >= 0
    else {
      throw SidebarImportSourceWalkerError.invalidLimits
    }

    try reachBoundary(.beforeRootAdmission)
    var state = WalkState(limits: limits)
    try state.chargeEncounteredEntry()

    let scopeWasStarted = scopeAccess.start(rootURL)
    var transferredScope = false
    defer {
      if scopeWasStarted, !transferredScope {
        scopeAccess.stop(rootURL)
      }
    }

    var filesystemRootFD: Int32 = -1
    defer {
      if filesystemRootFD >= 0 {
        Darwin.close(filesystemRootFD)
        metrics.descriptorClosed()
      }
    }
    let filesystemRootError = try performSidebarImportOpen(
      at: .beforeRootOpen,
      adoptionPath: "/",
      didReachBoundary: hooks.didReachBoundary,
      didAcquireOperationLease: hooks.didAcquireOperationLease,
      didOpenDescriptorBeforeAdoption: hooks.didOpenDescriptorBeforeAdoption,
      cancellation: cancellation,
      metrics: metrics,
      checkCancellation: { try Self.throwIfCancelled(cancellation) },
      open: {
        let descriptor = open("/", O_RDONLY | O_DIRECTORY | O_CLOEXEC)
        return (descriptor: descriptor, error: errno)
      },
      adopt: { filesystemRootFD = $0 })
    if let filesystemRootError {
      throw SidebarImportSourceWalkerError.openFailed(
        path: "/",
        reason: Self.posixReason(filesystemRootError))
    }
    try Self.throwIfCancelled(cancellation)

    let rootFD = try Self.openSourceRoot(
      rootURL,
      from: filesystemRootFD,
      hooks: hooks,
      cancellation: cancellation,
      metrics: metrics)
    var transferredRoot = false
    defer {
      if !transferredRoot {
        Darwin.close(rootFD)
        metrics.descriptorClosed()
      }
    }

    var rootMetadata = stat()
    guard fstat(rootFD, &rootMetadata) == 0 else {
      throw SidebarImportSourceWalkerError.inspectFailed(
        path: rootURL.path,
        reason: Self.posixReason())
    }

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
        from: filesystemRootFD,
        hooks: hooks,
        cancellation: cancellation,
        metrics: metrics)
      defer {
        Darwin.close(vaultFD)
        metrics.descriptorClosed()
      }
      if try Self.directory(
        rootFD,
        contains: vaultFD,
        hooks: hooks,
        cancellation: cancellation,
        metrics: metrics)
      {
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
      metrics.descriptorOpened()
      _ = fcntl(traversalFD, F_SETFD, FD_CLOEXEC)
      if let reason = try Self.walkOwnedDirectory(
        traversalFD,
        components: [],
        depth: 0,
        includeSelf: false,
        hooks: hooks,
        cancellation: cancellation,
        metrics: metrics,
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
      didAcquireReadLease: hooks.didAcquireReadLease,
      didAcquireOperationLease: hooks.didAcquireOperationLease,
      didOpenDescriptorBeforeAdoption: hooks.didOpenDescriptorBeforeAdoption,
      didReachBoundary: hooks.didReachBoundary,
      cancellation: cancellation,
      metrics: metrics,
      manifest: SidebarImportSourceManifest(
        entries: state.entries,
        failures: state.failures,
        advisoryByteCount: state.advisoryByteCount,
        encounteredEntryCount: state.encounteredEntries))
    transferredRoot = true
    transferredScope = true
    return prepared
  }

  private func reachBoundary(_ boundary: SidebarImportSourceBoundary) throws {
    hooks.didReachBoundary(boundary)
    if cancellation.isCancelled {
      throw SidebarImportSourceWalkerError.cancelled
    }
  }

  private struct WalkState {
    let limits: SidebarImportSourceLimits
    var entries: [SidebarImportSourceEntry] = []
    var failures: [SidebarImportSourceFailure] = []
    var advisoryByteCount: SidebarImportSourceAdvisoryByteCount
    var encounteredEntries = 0
    var nextTraversalOrdinal = 0

    init(limits: SidebarImportSourceLimits) {
      self.limits = limits
      advisoryByteCount = SidebarImportSourceAdvisoryByteCount(
        configuredTotalBytes: UInt64(limits.totalBytes))
    }

    mutating func chargeEncounteredEntry() throws {
      guard encounteredEntries < limits.maximumEntries else {
        throw SidebarImportSourceWalkerError.tooManyEntries(
          limit: limits.maximumEntries)
      }
      encounteredEntries += 1
    }

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
      guard fileBytes >= 0, fileBytes <= limits.refuseBytes else {
        throw SidebarImportSourceWalkerError.fileTooLarge(
          path: display,
          limitBytes: limits.refuseBytes)
      }
      advisoryByteCount.record(UInt64(fileBytes))
      let traversalOrdinal = nextTraversalOrdinal
      nextTraversalOrdinal += 1
      entries.append(
        SidebarImportSourceEntry(
          relativePath: SidebarImportSourceRelativePath(components: components),
          kind: kind,
          advisoryByteCount: UInt64(fileBytes),
          traversalOrdinal: traversalOrdinal))
    }

    mutating func reject(
      components: [String],
      reason: SidebarImportSourceFailureReason
    ) {
      let traversalOrdinal = nextTraversalOrdinal
      nextTraversalOrdinal += 1
      failures.append(
        SidebarImportSourceFailure(
          relativePath: SidebarImportSourceRelativePath(components: components),
          reason: reason,
          traversalOrdinal: traversalOrdinal))
    }
  }

  private static func walkOwnedDirectory(
    _ directoryFD: Int32,
    components: [String],
    depth: Int,
    includeSelf: Bool,
    hooks: SidebarImportSourceWalkerHooks,
    cancellation: SidebarImportSourceCancellationToken,
    metrics: SidebarImportSourceMetrics,
    state: inout WalkState
  ) throws -> SidebarImportSourceFailureReason? {
    guard let directory = fdopendir(directoryFD) else {
      Darwin.close(directoryFD)
      metrics.descriptorClosed()
      return .unreadable
    }
    defer {
      closedir(directory)
      metrics.descriptorClosed()
    }

    var names: [CapturedDirectoryName] = []
    while true {
      let path = components.joined(separator: "/")
      let readResult = try performOperation(
        at: .beforeDirectoryRead(path),
        hooks: hooks,
        cancellation: cancellation
      ) { () -> (rawName: [UInt8]?, error: Int32) in
        metrics.directoryReadOperationStarted()
        errno = 0
        guard let entry = readdir(directory) else {
          return (rawName: nil, error: errno)
        }
        let length = Int(entry.pointee.d_namlen)
        let rawName = withUnsafePointer(to: &entry.pointee.d_name) { pointer -> [UInt8] in
          pointer.withMemoryRebound(to: UInt8.self, capacity: length) { bytes in
            Array(UnsafeBufferPointer(start: bytes, count: length))
          }
        }
        return (rawName: rawName, error: errno)
      }
      try reachBoundary(
        .afterDirectoryRead(
          path,
          reachedEnd: readResult.rawName == nil),
        hooks: hooks,
        cancellation: cancellation)
      guard let rawName = readResult.rawName else {
        if readResult.error != 0 {
          return .unreadable
        }
        break
      }
      try state.chargeEncounteredEntry()
      if rawName != [46], rawName != [46, 46]
      {
        names.append(CapturedDirectoryName(rawName: rawName))
      }
    }
    for rawName in hooks.injectedDirectoryRecords(components.joined(separator: "/")) {
      try state.chargeEncounteredEntry()
      names.append(CapturedDirectoryName(rawName: rawName))
    }

    if includeSelf {
      try state.append(
        components: components,
        kind: .directory,
        depth: depth,
        fileBytes: 0)
    }

    names.sort { left, right in
      if left.primaryKey != right.primaryKey {
        return left.primaryKey.lexicographicallyPrecedes(right.primaryKey)
      }
      return left.rawName.lexicographicallyPrecedes(right.rawName)
    }

    let parentFD = dirfd(directory)
    for capturedName in names {
      let rawName = capturedName.rawName
      let decodedName = String(bytes: rawName, encoding: .utf8)
      let displayName = decodedName ?? String(decoding: rawName, as: UTF8.self)
      let failureComponents = components + [displayName]
      guard let name = decodedName else {
        state.reject(components: failureComponents, reason: .invalidFileName)
        continue
      }
      let childComponents = components + [name]
      if name.hasPrefix(".") {
        state.reject(components: childComponents, reason: .hidden)
        continue
      }

      let childDepth = depth + 1
      if childDepth > state.limits.maximumDepth {
        state.reject(components: childComponents, reason: .tooDeep)
        continue
      }

      let childPath = childComponents.joined(separator: "/")
      try reachBoundary(
        .beforeInspect(childPath),
        hooks: hooks,
        cancellation: cancellation)
      var metadata = stat()
      let inspectResult = withRawName(rawName) { component in
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
      hooks.didInspectDescendant(childPath)

      switch metadata.st_mode & S_IFMT {
      case S_IFDIR:
        var childFD: Int32 = -1
        var ownsChildFD = false
        defer {
          if ownsChildFD {
            Darwin.close(childFD)
            metrics.descriptorClosed()
          }
        }
        let openError = try performSidebarImportOpen(
          at: .beforeOpen(childPath),
          adoptionPath: childPath,
          didReachBoundary: hooks.didReachBoundary,
          didAcquireOperationLease: hooks.didAcquireOperationLease,
          didOpenDescriptorBeforeAdoption: hooks.didOpenDescriptorBeforeAdoption,
          cancellation: cancellation,
          metrics: metrics,
          checkCancellation: { try throwIfCancelled(cancellation) },
          open: {
            let descriptor = withRawName(rawName) { component in
              openat(
                parentFD,
                component,
                O_RDONLY | O_DIRECTORY | O_NOFOLLOW | O_CLOEXEC
              )
            }
            return (descriptor: descriptor, error: errno)
          },
          adopt: { descriptor in
            childFD = descriptor
            ownsChildFD = true
          })
        if openError != nil {
          state.reject(
            components: childComponents,
            reason: rejectionReasonAfterOpenFailure(parentFD: parentFD, name: name))
          continue
        }
        try throwIfCancelled(cancellation)
        var openedMetadata = stat()
        guard fstat(childFD, &openedMetadata) == 0 else {
          state.reject(components: childComponents, reason: .unreadable)
          continue
        }
        if let reason = rejectionReason(for: openedMetadata) {
          state.reject(components: childComponents, reason: reason)
          continue
        }
        guard openedMetadata.st_mode & S_IFMT == S_IFDIR else {
          state.reject(components: childComponents, reason: .entryKindChanged)
          continue
        }
        try reachBoundary(
          .beforeRecursion(childPath),
          hooks: hooks,
          cancellation: cancellation)
        ownsChildFD = false
        if let reason = try walkOwnedDirectory(
          childFD,
          components: childComponents,
          depth: childDepth,
          includeSelf: true,
          hooks: hooks,
          cancellation: cancellation,
          metrics: metrics,
          state: &state)
        {
          state.reject(components: childComponents, reason: reason)
        }
      case S_IFREG:
        var childFD: Int32 = -1
        var ownsChildFD = false
        defer {
          if ownsChildFD {
            Darwin.close(childFD)
            metrics.descriptorClosed()
          }
        }
        let openError = try performSidebarImportOpen(
          at: .beforeOpen(childPath),
          adoptionPath: childPath,
          didReachBoundary: hooks.didReachBoundary,
          didAcquireOperationLease: hooks.didAcquireOperationLease,
          didOpenDescriptorBeforeAdoption: hooks.didOpenDescriptorBeforeAdoption,
          cancellation: cancellation,
          metrics: metrics,
          checkCancellation: { try throwIfCancelled(cancellation) },
          open: {
            let descriptor = withRawName(rawName) { component in
              openat(
                parentFD,
                component,
                O_RDONLY | O_NONBLOCK | O_NOFOLLOW | O_CLOEXEC
              )
            }
            return (descriptor: descriptor, error: errno)
          },
          adopt: { descriptor in
            childFD = descriptor
            ownsChildFD = true
          })
        if openError != nil {
          state.reject(
            components: childComponents,
            reason: rejectionReasonAfterOpenFailure(parentFD: parentFD, name: name))
          continue
        }
        try throwIfCancelled(cancellation)
        var openedMetadata = stat()
        let inspectOpenedResult = fstat(childFD, &openedMetadata)
        Darwin.close(childFD)
        metrics.descriptorClosed()
        ownsChildFD = false
        guard inspectOpenedResult == 0 else {
          state.reject(components: childComponents, reason: .unreadable)
          continue
        }
        if let reason = rejectionReason(for: openedMetadata) {
          state.reject(components: childComponents, reason: reason)
          continue
        }
        guard openedMetadata.st_mode & S_IFMT == S_IFREG else {
          state.reject(components: childComponents, reason: .entryKindChanged)
          continue
        }
        do {
          try state.append(
            components: childComponents,
            kind: .regularFile,
            depth: childDepth,
            fileBytes: openedMetadata.st_size)
        } catch SidebarImportSourceWalkerError.fileTooLarge(_, let limitBytes) {
          state.reject(
            components: childComponents,
            reason: .fileTooLarge(limitBytes: limitBytes))
        }
      default:
        state.reject(components: childComponents, reason: .unsupported)
      }
    }
    return nil
  }

  private struct CapturedDirectoryName {
    let rawName: [UInt8]
    let primaryKey: [UInt8]

    init(rawName: [UInt8]) {
      self.rawName = rawName
      primaryKey = SidebarImportSourceNameOrdering.primaryKey(rawName)
    }
  }

  private struct FileIdentity: Equatable {
    let device: dev_t
    let inode: ino_t
  }

  private static func openSourceRoot(
    _ rootURL: URL,
    from filesystemRootFD: Int32,
    hooks: SidebarImportSourceWalkerHooks,
    cancellation: SidebarImportSourceCancellationToken,
    metrics: SidebarImportSourceMetrics
  ) throws -> Int32 {
    let requestedPath = rootURL.path
    let components = try absoluteComponents(of: rootURL)
    let initialFD = dup(filesystemRootFD)
    guard initialFD >= 0 else {
      throw SidebarImportSourceWalkerError.openFailed(
        path: requestedPath,
        reason: posixReason())
    }
    metrics.descriptorOpened()
    _ = fcntl(initialFD, F_SETFD, FD_CLOEXEC)

    var currentFD = initialFD
    do {
      for (index, component) in components.enumerated() {
        try reachBoundary(
          .beforeInspect(requestedPath),
          hooks: hooks,
          cancellation: cancellation)
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
        let parentFD = currentFD
        let openError = try performSidebarImportOpen(
          at: .beforeOpen(requestedPath),
          adoptionPath: requestedPath,
          didReachBoundary: hooks.didReachBoundary,
          didAcquireOperationLease: hooks.didAcquireOperationLease,
          didOpenDescriptorBeforeAdoption: hooks.didOpenDescriptorBeforeAdoption,
          cancellation: cancellation,
          metrics: metrics,
          checkCancellation: { try throwIfCancelled(cancellation) },
          open: {
            let descriptor = component.withCString { name in
              openat(parentFD, name, flags)
            }
            return (descriptor: descriptor, error: errno)
          },
          adopt: { descriptor in
            Darwin.close(currentFD)
            metrics.descriptorClosed()
            currentFD = descriptor
          })
        if openError != nil {
          let reason = rejectionReasonAfterOpenFailure(
            parentFD: currentFD,
            name: component)
          throw SidebarImportSourceWalkerError.rejectedRoot(
            path: requestedPath,
            reason: reason)
        }
        try throwIfCancelled(cancellation)

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
      metrics.descriptorClosed()
      throw error
    }
  }

  private static func openTrustedDirectory(
    _ url: URL,
    from filesystemRootFD: Int32,
    hooks: SidebarImportSourceWalkerHooks,
    cancellation: SidebarImportSourceCancellationToken,
    metrics: SidebarImportSourceMetrics
  ) throws -> Int32 {
    let components = try absoluteComponents(of: url)
    let initialFD = dup(filesystemRootFD)
    guard initialFD >= 0 else {
      throw SidebarImportSourceWalkerError.openFailed(
        path: url.path,
        reason: posixReason())
    }
    metrics.descriptorOpened()
    _ = fcntl(initialFD, F_SETFD, FD_CLOEXEC)

    var currentFD = initialFD
    do {
      for component in components {
        let parentFD = currentFD
        let openError = try performSidebarImportOpen(
          at: .beforeOpen(url.path),
          adoptionPath: url.path,
          didReachBoundary: hooks.didReachBoundary,
          didAcquireOperationLease: hooks.didAcquireOperationLease,
          didOpenDescriptorBeforeAdoption: hooks.didOpenDescriptorBeforeAdoption,
          cancellation: cancellation,
          metrics: metrics,
          checkCancellation: { try throwIfCancelled(cancellation) },
          open: {
            let descriptor = component.withCString { name in
              openat(parentFD, name, O_RDONLY | O_DIRECTORY | O_CLOEXEC)
            }
            return (descriptor: descriptor, error: errno)
          },
          adopt: { descriptor in
            Darwin.close(currentFD)
            metrics.descriptorClosed()
            currentFD = descriptor
          })
        if let openError {
          throw SidebarImportSourceWalkerError.openFailed(
            path: url.path,
            reason: posixReason(openError))
        }
        try throwIfCancelled(cancellation)
      }
      return currentFD
    } catch {
      Darwin.close(currentFD)
      metrics.descriptorClosed()
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
    contains vaultFD: Int32,
    hooks: SidebarImportSourceWalkerHooks,
    cancellation: SidebarImportSourceCancellationToken,
    metrics: SidebarImportSourceMetrics
  ) throws -> Bool {
    let sourceIdentity = try identity(of: sourceFD, path: "source root")
    var currentFD = dup(vaultFD)
    guard currentFD >= 0 else {
      throw SidebarImportSourceWalkerError.openFailed(
        path: "vault",
        reason: posixReason())
    }
    metrics.descriptorOpened()
    _ = fcntl(currentFD, F_SETFD, FD_CLOEXEC)
    defer {
      Darwin.close(currentFD)
      metrics.descriptorClosed()
    }

    while true {
      try throwIfCancelled(cancellation)
      let currentIdentity = try identity(of: currentFD, path: "vault")
      if currentIdentity == sourceIdentity {
        return true
      }

      var parentFD: Int32 = -1
      var ownsParentFD = false
      defer {
        if ownsParentFD {
          Darwin.close(parentFD)
          metrics.descriptorClosed()
        }
      }
      let openError = try performSidebarImportOpen(
        at: .beforeOpen("vault ancestor"),
        adoptionPath: "vault ancestor",
        didReachBoundary: hooks.didReachBoundary,
        didAcquireOperationLease: hooks.didAcquireOperationLease,
        didOpenDescriptorBeforeAdoption: hooks.didOpenDescriptorBeforeAdoption,
        cancellation: cancellation,
        metrics: metrics,
        checkCancellation: { try throwIfCancelled(cancellation) },
        open: {
          let descriptor = "..".withCString { name in
            openat(
              currentFD,
              name,
              O_RDONLY | O_DIRECTORY | O_NOFOLLOW | O_CLOEXEC
            )
          }
          return (descriptor: descriptor, error: errno)
        },
        adopt: { descriptor in
          parentFD = descriptor
          ownsParentFD = true
        })
      if let openError {
        throw SidebarImportSourceWalkerError.openFailed(
          path: "vault ancestor",
          reason: posixReason(openError))
      }
      try throwIfCancelled(cancellation)
      let parentIdentity = try identity(of: parentFD, path: "vault ancestor")
      if parentIdentity == currentIdentity {
        return false
      }
      Darwin.close(currentFD)
      metrics.descriptorClosed()
      currentFD = parentFD
      ownsParentFD = false
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

  private static func withRawName<Result>(
    _ rawName: [UInt8],
    _ body: (UnsafePointer<CChar>) -> Result
  ) -> Result {
    var terminated = rawName + [0]
    return terminated.withUnsafeMutableBytes { bytes in
      body(bytes.bindMemory(to: CChar.self).baseAddress!)
    }
  }

  private static func reachBoundary(
    _ boundary: SidebarImportSourceBoundary,
    hooks: SidebarImportSourceWalkerHooks,
    cancellation: SidebarImportSourceCancellationToken
  ) throws {
    hooks.didReachBoundary(boundary)
    try throwIfCancelled(cancellation)
  }

  private static func performOperation<Result>(
    at boundary: SidebarImportSourceBoundary,
    hooks: SidebarImportSourceWalkerHooks,
    cancellation: SidebarImportSourceCancellationToken,
    _ body: () throws -> Result
  ) throws -> Result {
    hooks.didReachBoundary(boundary)
    guard let operationLease = cancellation.beginOperation() else {
      throw SidebarImportSourceWalkerError.cancelled
    }
    hooks.didAcquireOperationLease(boundary)
    defer { operationLease.release() }
    return try body()
  }

  private static func throwIfCancelled(
    _ cancellation: SidebarImportSourceCancellationToken
  ) throws {
    if cancellation.isCancelled {
      throw SidebarImportSourceWalkerError.cancelled
    }
  }

  private static func posixReason(_ code: Int32 = errno) -> String {
    String(cString: strerror(code))
  }
}
