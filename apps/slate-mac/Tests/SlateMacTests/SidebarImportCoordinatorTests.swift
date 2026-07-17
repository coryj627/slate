// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Darwin
import Foundation
import XCTest

@testable import SlateMac

final class SidebarImportCoordinatorTests: XCTestCase {
  private var tempDirectory: URL!

  override func setUpWithError() throws {
    tempDirectory = URL(fileURLWithPath: "/private/tmp", isDirectory: true)
      .appendingPathComponent("sidebar-import-\(UUID().uuidString)", isDirectory: true)
    try FileManager.default.createDirectory(
      at: tempDirectory, withIntermediateDirectories: true)
  }

  override func tearDownWithError() throws {
    try? FileManager.default.removeItem(at: tempDirectory)
  }

  private final class ProviderStub: SidebarImportProviderLoading, @unchecked Sendable {
    private let lock = NSLock()
    private var completion: (@Sendable (Data?, Error?) -> Void)?
    private(set) var loadCount = 0

    let registeredTypeIdentifiers: [String]
    let progress = Progress(totalUnitCount: 1)

    init(
      registeredTypeIdentifiers: [String] = [
        SidebarImportProviderIntake.fileURLTypeIdentifier
      ]
    ) {
      self.registeredTypeIdentifiers = registeredTypeIdentifiers
    }

    func hasItemConformingToTypeIdentifier(_ typeIdentifier: String) -> Bool {
      registeredTypeIdentifiers.contains(typeIdentifier)
    }

    func loadDataRepresentation(
      forTypeIdentifier typeIdentifier: String,
      completionHandler: @escaping @Sendable (Data?, Error?) -> Void
    ) -> Progress {
      lock.lock()
      loadCount += 1
      completion = completionHandler
      lock.unlock()
      return progress
    }

    func complete(with url: URL) {
      complete(data: url.dataRepresentation, error: nil)
    }

    func complete(data: Data?, error: Error?) {
      lock.lock()
      let callback = completion
      lock.unlock()
      callback?(data, error)
    }

    func loadsStarted() -> Int {
      lock.lock()
      defer { lock.unlock() }
      return loadCount
    }
  }

  private final class SlotBox: @unchecked Sendable {
    private let lock = NSLock()
    private var value: [SidebarImportProviderSlot]?

    func store(_ value: [SidebarImportProviderSlot]) {
      lock.lock()
      self.value = value
      lock.unlock()
    }

    func load() -> [SidebarImportProviderSlot]? {
      lock.lock()
      defer { lock.unlock() }
      return value
    }
  }

  private final class WeakProgressBox: @unchecked Sendable {
    private let lock = NSLock()
    private weak var value: Progress?

    func store(_ value: Progress) {
      lock.lock()
      self.value = value
      lock.unlock()
    }

    func load() -> Progress? {
      lock.lock()
      defer { lock.unlock() }
      return value
    }
  }

  private final class EphemeralProgressProvider:
    SidebarImportProviderLoading, @unchecked Sendable
  {
    typealias ProviderCompletion = @Sendable (Data?, Error?) -> Void

    private let lock = NSLock()
    private var completion: ProviderCompletion?
    private var loadCount = 0
    private var progressCancellationCount = 0
    private let onStart: @Sendable (ProviderCompletion) -> Void
    let registeredTypeIdentifiers = [SidebarImportProviderIntake.fileURLTypeIdentifier]
    let progressBox = WeakProgressBox()

    init(
      onStart: @escaping @Sendable (ProviderCompletion) -> Void = { _ in }
    ) {
      self.onStart = onStart
    }

    func hasItemConformingToTypeIdentifier(_ typeIdentifier: String) -> Bool {
      registeredTypeIdentifiers.contains(typeIdentifier)
    }

    func loadDataRepresentation(
      forTypeIdentifier typeIdentifier: String,
      completionHandler: @escaping @Sendable (Data?, Error?) -> Void
    ) -> Progress {
      lock.lock()
      loadCount += 1
      completion = completionHandler
      lock.unlock()
      let progress = Progress(totalUnitCount: 1)
      progress.cancellationHandler = { [weak self] in
        self?.lock.lock()
        self?.progressCancellationCount += 1
        self?.lock.unlock()
      }
      progressBox.store(progress)
      onStart(completionHandler)
      return progress
    }

    func complete(with url: URL) {
      lock.lock()
      let callback = completion
      lock.unlock()
      callback?(url.dataRepresentation, nil)
    }

    func loadsStarted() -> Int {
      lock.lock()
      defer { lock.unlock() }
      return loadCount
    }

    func progressCancellations() -> Int {
      lock.lock()
      defer { lock.unlock() }
      return progressCancellationCount
    }
  }

  private final class IntakeBox: @unchecked Sendable {
    private let lock = NSLock()
    private var value: SidebarImportProviderIntake?

    func store(_ value: SidebarImportProviderIntake) {
      lock.lock()
      self.value = value
      lock.unlock()
    }

    func cancel() {
      lock.lock()
      let value = value
      lock.unlock()
      value?.cancel()
    }
  }

  private final class ScopeProbe: @unchecked Sendable {
    private let lock = NSLock()
    private let startResult: Bool
    private(set) var starts = 0
    private(set) var stops = 0

    init(startResult: Bool = true) {
      self.startResult = startResult
    }

    func access() -> SidebarImportSecurityScopeAccess {
      SidebarImportSecurityScopeAccess(
        start: { [weak self] _ in
          self?.lock.lock()
          self?.starts += 1
          self?.lock.unlock()
          return self?.startResult ?? false
        },
        stop: { [weak self] _ in
          self?.lock.lock()
          self?.stops += 1
          self?.lock.unlock()
        })
    }

    func counts() -> (starts: Int, stops: Int) {
      lock.lock()
      defer { lock.unlock() }
      return (starts, stops)
    }
  }

  private final class BlockingGate: @unchecked Sendable {
    private let lock = NSLock()
    private let entered = DispatchSemaphore(value: 0)
    private let release = DispatchSemaphore(value: 0)
    private var shouldBlock = true

    func block() {
      lock.lock()
      guard shouldBlock else {
        lock.unlock()
        return
      }
      shouldBlock = false
      lock.unlock()
      entered.signal()
      release.wait()
    }

    func waitUntilBlocked(timeout: TimeInterval = 1) -> Bool {
      entered.wait(timeout: .now() + timeout) == .success
    }

    func unblock() {
      release.signal()
    }
  }

  private final class TwoReadGate: @unchecked Sendable {
    private let lock = NSLock()
    private let entered = DispatchSemaphore(value: 0)
    private let release = DispatchSemaphore(value: 0)
    private var entrants = 0

    func block() {
      lock.lock()
      entrants += 1
      lock.unlock()
      entered.signal()
      release.wait()
    }

    func waitForBoth(timeout: TimeInterval = 1) -> Bool {
      entered.wait(timeout: .now() + timeout) == .success
        && entered.wait(timeout: .now() + timeout) == .success
    }

    func unblockBoth() {
      release.signal()
      release.signal()
    }
  }

  private final class BoundaryProbe: @unchecked Sendable {
    private let lock = NSLock()
    private var storage: [SidebarImportSourceBoundary] = []

    func record(_ boundary: SidebarImportSourceBoundary) {
      lock.lock()
      storage.append(boundary)
      lock.unlock()
    }

    func count(where predicate: (SidebarImportSourceBoundary) -> Bool) -> Int {
      lock.lock()
      defer { lock.unlock() }
      return storage.filter(predicate).count
    }

    func values() -> [SidebarImportSourceBoundary] {
      lock.lock()
      defer { lock.unlock() }
      return storage
    }
  }

  private final class ResultBox<Value>: @unchecked Sendable {
    private let lock = NSLock()
    private var value: Result<Value, Error>?

    func store(_ value: Result<Value, Error>) {
      lock.lock()
      self.value = value
      lock.unlock()
    }

    func load() -> Result<Value, Error>? {
      lock.lock()
      defer { lock.unlock() }
      return value
    }
  }

  private final class ValueBox<Value>: @unchecked Sendable {
    private let lock = NSLock()
    private var value: Value?

    func store(_ value: Value) {
      lock.lock()
      self.value = value
      lock.unlock()
    }

    func load() -> Value? {
      lock.lock()
      defer { lock.unlock() }
      return value
    }
  }

  private func waitForScheduler(
    timeout: TimeInterval = 1,
    _ predicate: () -> Bool
  ) -> Bool {
    let deadline = Date().addingTimeInterval(timeout)
    repeat {
      if predicate() { return true }
      usleep(1_000)
    } while Date() < deadline
    return predicate()
  }

  private final class PostInspectionSwapProbe: @unchecked Sendable {
    private let lock = NSLock()
    private let directoryURL: URL
    private let fifoURL: URL
    private var errors: [String] = []

    init(directoryURL: URL, fifoURL: URL) {
      self.directoryURL = directoryURL
      self.fifoURL = fifoURL
    }

    func swap(relativePath: String) {
      do {
        switch relativePath {
        case "directory-swap":
          try FileManager.default.removeItem(at: directoryURL)
          try FileManager.default.createDirectory(
            at: directoryURL,
            withIntermediateDirectories: false)
        case "fifo-swap":
          try FileManager.default.removeItem(at: fifoURL)
          guard Darwin.mkfifo(fifoURL.path, 0o600) == 0 else {
            throw POSIXError(POSIXErrorCode(rawValue: errno) ?? .EIO)
          }
        default:
          return
        }
      } catch {
        lock.lock()
        errors.append(error.localizedDescription)
        lock.unlock()
      }
    }

    func recordedErrors() -> [String] {
      lock.lock()
      defer { lock.unlock() }
      return errors
    }
  }

  private final class OneShotGrowthProbe: @unchecked Sendable {
    private let lock = NSLock()
    private let fileURL: URL
    private var didGrow = false
    private var errors: [String] = []

    init(fileURL: URL) {
      self.fileURL = fileURL
    }

    func growOnFirstRead(offset: Int64) {
      guard offset == 0 else { return }
      lock.lock()
      guard !didGrow else {
        lock.unlock()
        return
      }
      didGrow = true
      lock.unlock()
      do {
        let handle = try FileHandle(forWritingTo: fileURL)
        try handle.seekToEnd()
        try handle.write(contentsOf: Data([0xFF]))
        try handle.close()
      } catch {
        lock.lock()
        errors.append(error.localizedDescription)
        lock.unlock()
      }
    }

    func recordedErrors() -> [String] {
      lock.lock()
      defer { lock.unlock() }
      return errors
    }
  }

  private func makeWalker(scope: ScopeProbe) -> SidebarImportSourceWalker {
    SidebarImportSourceWalker(
      limits: SidebarImportSourceLimits(
        refuseBytes: 1_024,
        totalBytes: 4_096,
        maximumEntries: 100,
        maximumDepth: 8),
      scopeAccess: scope.access())
  }

  private func makeVault(named name: String = "vault") throws -> URL {
    let vault = tempDirectory.appendingPathComponent(name, isDirectory: true)
    try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: false)
    return vault
  }

  private func bindUnixSocket(at url: URL) throws -> Int32 {
    let descriptor = Darwin.socket(AF_UNIX, SOCK_STREAM, 0)
    guard descriptor >= 0 else {
      throw POSIXError(POSIXErrorCode(rawValue: errno) ?? .EIO)
    }

    var address = sockaddr_un()
    address.sun_family = sa_family_t(AF_UNIX)
    let pathBytes = Array(url.path.utf8CString)
    let pathCapacity = MemoryLayout.size(ofValue: address.sun_path)
    guard pathBytes.count <= pathCapacity else {
      Darwin.close(descriptor)
      throw POSIXError(.ENAMETOOLONG)
    }
    withUnsafeMutablePointer(to: &address.sun_path) { pathPointer in
      pathPointer.withMemoryRebound(to: CChar.self, capacity: pathCapacity) { bytes in
        for (index, byte) in pathBytes.enumerated() {
          bytes[index] = byte
        }
      }
    }
    let result = withUnsafePointer(to: &address) { pointer in
      pointer.withMemoryRebound(to: sockaddr.self, capacity: 1) { socketAddress in
        Darwin.bind(descriptor, socketAddress, socklen_t(MemoryLayout<sockaddr_un>.size))
      }
    }
    guard result == 0 else {
      let code = errno
      Darwin.close(descriptor)
      throw POSIXError(POSIXErrorCode(rawValue: code) ?? .EIO)
    }
    return descriptor
  }

  private func assertRootRejected(
    _ root: URL,
    vault: URL,
    as expectedReason: SidebarImportSourceFailureReason,
    scope: ScopeProbe,
    file: StaticString = #filePath,
    line: UInt = #line
  ) {
    XCTAssertThrowsError(
      try makeWalker(scope: scope).prepare(rootURL: root, vaultURL: vault),
      file: file,
      line: line
    ) { error in
      guard case let SidebarImportSourceWalkerError.rejectedRoot(path, reason) = error else {
        XCTFail("expected typed root rejection, got \(error)", file: file, line: line)
        return
      }
      XCTAssertEqual(path, root.path, file: file, line: line)
      XCTAssertEqual(reason, expectedReason, file: file, line: line)
    }
  }

  func testPublicProviderIntakeLoadsOncePreservesOriginalOrderAndAliasURL() async throws {
    let unsupportedFirst = ProviderStub(registeredTypeIdentifiers: ["public.text"])
    let first = ProviderStub()
    let unsupportedMiddle = ProviderStub(registeredTypeIdentifiers: ["public.image"])
    let second = ProviderStub()
    let third = ProviderStub()
    let intake = SidebarImportProviderIntake(
      providers: [unsupportedFirst, first, unsupportedMiddle, second, third])
    let realContainer = tempDirectory.appendingPathComponent("real-container", isDirectory: true)
    try FileManager.default.createDirectory(
      at: realContainer, withIntermediateDirectories: false)
    let aliasContainer = tempDirectory.appendingPathComponent("alias-container", isDirectory: true)
    try FileManager.default.createSymbolicLink(
      at: aliasContainer, withDestinationURL: realContainer)
    let firstURL = aliasContainer.appendingPathComponent("first.md")
    let secondURL = URL(fileURLWithPath: "/tmp/second.md")
    let thirdURL = URL(fileURLWithPath: "/tmp/third.md")

    let resultTask = Task { await intake.load() }
    while first.loadsStarted() == 0 || second.loadsStarted() == 0
      || third.loadsStarted() == 0
    {
      await Task.yield()
    }
    third.complete(with: thirdURL)
    first.complete(with: firstURL)
    second.complete(with: secondURL)

    let result = await resultTask.value
    XCTAssertEqual(
      result,
      [
        SidebarImportProviderSlot(providerIndex: 1, outcome: .url(firstURL)),
        SidebarImportProviderSlot(providerIndex: 3, outcome: .url(secondURL)),
        SidebarImportProviderSlot(providerIndex: 4, outcome: .url(thirdURL)),
      ])
    guard case let .url(decodedAlias) = result[0].outcome else {
      return XCTFail("expected the alias-spelled file URL")
    }
    XCTAssertEqual(decodedAlias.absoluteString, firstURL.absoluteString)
    XCTAssertEqual(unsupportedFirst.loadsStarted(), 0)
    XCTAssertEqual(unsupportedMiddle.loadsStarted(), 0)
    XCTAssertEqual(first.loadsStarted(), 1)
    XCTAssertEqual(second.loadsStarted(), 1)
    XCTAssertEqual(third.loadsStarted(), 1)
  }

  func testPublicProviderCancellationTerminalizesNeverCallbackAndIgnoresLateCallbacks() async {
    let succeeded = ProviderStub()
    let failed = ProviderStub()
    let pending = ProviderStub()
    let intake = SidebarImportProviderIntake(providers: [succeeded, failed, pending])
    let returned = expectation(description: "aggregate returned without provider callback")
    returned.expectedFulfillmentCount = 1
    let slots = SlotBox()
    Task {
      slots.store(await intake.load())
      returned.fulfill()
    }
    while succeeded.loadsStarted() == 0 || failed.loadsStarted() == 0
      || pending.loadsStarted() == 0
    {
      await Task.yield()
    }

    let completedURL = URL(fileURLWithPath: "/tmp/completed.md")
    succeeded.complete(with: completedURL)
    failed.complete(
      data: nil,
      error: NSError(
        domain: "SidebarImportCoordinatorTests",
        code: 2,
        userInfo: [NSLocalizedDescriptionKey: "finished failure"]))
    intake.cancel()
    intake.cancel()
    await fulfillment(of: [returned], timeout: 0.2)
    pending.complete(with: URL(fileURLWithPath: "/tmp/late.md"))
    pending.complete(with: URL(fileURLWithPath: "/tmp/duplicate.md"))
    await Task.yield()

    XCTAssertEqual(
      slots.load(),
      [
        SidebarImportProviderSlot(providerIndex: 0, outcome: .url(completedURL)),
        SidebarImportProviderSlot(
          providerIndex: 1,
          outcome: .failure(.loadFailed("finished failure"))),
        SidebarImportProviderSlot(providerIndex: 2, outcome: .failure(.cancelled)),
      ])
    XCTAssertFalse(succeeded.progress.isCancelled)
    XCTAssertFalse(failed.progress.isCancelled)
    XCTAssertTrue(pending.progress.isCancelled)
  }

  func testPublicProviderIntakeEmptyAndUnsupportedOnlyCompleteWithoutLoads() async {
    let unsupported = ProviderStub(registeredTypeIdentifiers: ["public.text"])

    let empty = await SidebarImportProviderIntake(providers: []).load()
    let unsupportedOnly = await SidebarImportProviderIntake(
      providers: [unsupported]
    ).load()
    XCTAssertEqual(empty, [])
    XCTAssertEqual(unsupportedOnly, [])
    XCTAssertEqual(unsupported.loadsStarted(), 0)
  }

  func testPublicProviderOutcomesUseTypedFailurePrecedence() async {
    let providers = (0..<6).map { _ in ProviderStub() }
    let intake = SidebarImportProviderIntake(providers: providers)
    let resultTask = Task { await intake.load() }
    while providers.contains(where: { $0.loadsStarted() == 0 }) {
      await Task.yield()
    }

    let validURL = URL(fileURLWithPath: "/tmp/valid.md")
    providers[0].complete(with: validURL)
    providers[1].complete(
      data: URL(fileURLWithPath: "/tmp/ignored.md").dataRepresentation,
      error: NSError(
        domain: "SidebarImportCoordinatorTests",
        code: 1,
        userInfo: [NSLocalizedDescriptionKey: "provider failed"]))
    providers[2].complete(
      data: nil,
      error: NSError(
        domain: "SidebarImportCoordinatorTests",
        code: 2,
        userInfo: [NSLocalizedDescriptionKey: "nil provider failed"]))
    providers[3].complete(data: nil, error: nil)
    providers[4].complete(data: Data("not a URL".utf8), error: nil)
    providers[5].complete(
      data: URL(string: "https://example.com/not-a-file")?.dataRepresentation,
      error: nil)

    let result = await resultTask.value
    XCTAssertEqual(
      result,
      [
        SidebarImportProviderSlot(providerIndex: 0, outcome: .url(validURL)),
        SidebarImportProviderSlot(
          providerIndex: 1,
          outcome: .failure(.loadFailed("provider failed"))),
        SidebarImportProviderSlot(
          providerIndex: 2,
          outcome: .failure(.loadFailed("nil provider failed"))),
        SidebarImportProviderSlot(providerIndex: 3, outcome: .failure(.missingData)),
        SidebarImportProviderSlot(providerIndex: 4, outcome: .failure(.invalidFileURL)),
        SidebarImportProviderSlot(providerIndex: 5, outcome: .failure(.invalidFileURL)),
      ])
  }

  func testPublicProviderCancellationBeforeLoadAndPrecancelledTaskStartZeroLoads() async {
    let explicitProvider = ProviderStub()
    let explicit = SidebarImportProviderIntake(providers: [explicitProvider])
    explicit.cancel()
    let explicitResult = await explicit.load()
    XCTAssertEqual(
      explicitResult,
      [SidebarImportProviderSlot(providerIndex: 0, outcome: .failure(.cancelled))])
    XCTAssertEqual(explicitProvider.loadsStarted(), 0)

    let taskProvider = ProviderStub()
    let taskIntake = SidebarImportProviderIntake(providers: [taskProvider])
    let task = Task {
      while !Task.isCancelled { await Task.yield() }
      return await taskIntake.load()
    }
    task.cancel()
    let taskResult = await task.value
    XCTAssertEqual(
      taskResult,
      [SidebarImportProviderSlot(providerIndex: 0, outcome: .failure(.cancelled))])
    XCTAssertEqual(taskProvider.loadsStarted(), 0)
  }

  func testPublicProviderProgressIsRetainedOnlyUntilItsSlotTerminates() async {
    let completedProvider = EphemeralProgressProvider()
    let completedIntake = SidebarImportProviderIntake(providers: [completedProvider])
    let completedResultTask = Task { await completedIntake.load() }
    while completedProvider.loadsStarted() == 0 {
      await Task.yield()
    }
    while completedProvider.progressBox.load() == nil {
      await Task.yield()
    }

    XCTAssertNotNil(
      completedProvider.progressBox.load(),
      "the intake must retain outstanding provider progress")
    completedProvider.complete(with: URL(fileURLWithPath: "/tmp/retained.md"))
    _ = await completedResultTask.value
    for _ in 0..<20 where completedProvider.progressBox.load() != nil {
      await Task.yield()
    }

    XCTAssertNil(
      completedProvider.progressBox.load(),
      "terminal slots must release their retained progress")

    let cancelledProvider = EphemeralProgressProvider()
    let cancelledIntake = SidebarImportProviderIntake(providers: [cancelledProvider])
    let cancelledResultTask = Task { await cancelledIntake.load() }
    while cancelledProvider.loadsStarted() == 0 {
      await Task.yield()
    }
    while cancelledProvider.progressBox.load() == nil {
      await Task.yield()
    }

    cancelledIntake.cancel()
    let cancelledResult = await cancelledResultTask.value
    for _ in 0..<20 where cancelledProvider.progressBox.load() != nil {
      await Task.yield()
    }

    XCTAssertEqual(
      cancelledResult,
      [SidebarImportProviderSlot(providerIndex: 0, outcome: .failure(.cancelled))])
    XCTAssertEqual(cancelledProvider.progressCancellations(), 1)
    XCTAssertNil(
      cancelledProvider.progressBox.load(),
      "cancelled slots must release their retained progress")
  }

  func testPublicProviderSynchronousDuplicateCallbackResumesOnceAndReleasesProgress() async {
    let firstURL = URL(fileURLWithPath: "/tmp/synchronous.md")
    let intakeBox = IntakeBox()
    let provider = EphemeralProgressProvider(onStart: { completion in
      completion(firstURL.dataRepresentation, nil)
      intakeBox.cancel()
      completion(URL(fileURLWithPath: "/tmp/duplicate.md").dataRepresentation, nil)
    })
    let intake = SidebarImportProviderIntake(providers: [provider])
    intakeBox.store(intake)

    let result = await intake.load()
    for _ in 0..<20 where provider.progressBox.load() != nil {
      await Task.yield()
    }

    XCTAssertEqual(
      result,
      [SidebarImportProviderSlot(providerIndex: 0, outcome: .url(firstURL))])
    XCTAssertEqual(provider.loadsStarted(), 1)
    XCTAssertEqual(provider.progressCancellations(), 0)
    XCTAssertNil(
      provider.progressBox.load(),
      "a Progress returned after a synchronous terminal callback must not be retained")
  }

  func testPublicProviderCancellationDuringFirstStartStopsLaterLoadsAndCancelsReturnedProgress()
    async
  {
    let intakeBox = IntakeBox()
    let lateURL = URL(fileURLWithPath: "/tmp/cancel-lost-race.md")
    let first = EphemeralProgressProvider(onStart: { completion in
      intakeBox.cancel()
      completion(lateURL.dataRepresentation, nil)
    })
    let later = ProviderStub()
    let intake = SidebarImportProviderIntake(providers: [first, later])
    intakeBox.store(intake)

    let result = await intake.load()
    for _ in 0..<20 where first.progressBox.load() != nil {
      await Task.yield()
    }

    XCTAssertEqual(
      result,
      [
        SidebarImportProviderSlot(providerIndex: 0, outcome: .failure(.cancelled)),
        SidebarImportProviderSlot(providerIndex: 1, outcome: .failure(.cancelled)),
      ])
    XCTAssertEqual(first.loadsStarted(), 1)
    XCTAssertEqual(first.progressCancellations(), 1)
    XCTAssertEqual(later.loadsStarted(), 0)
    XCTAssertNil(
      first.progressBox.load(),
      "the just-returned cancelled Progress must be released")
  }

  func testWalkerBuildsRegularAndEmptyTreeManifestAndReadsBinaryBytes() throws {
    let vault = tempDirectory.appendingPathComponent("vault", isDirectory: true)
    let root = tempDirectory.appendingPathComponent("source", isDirectory: true)
    try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: false)
    try FileManager.default.createDirectory(at: root, withIntermediateDirectories: false)
    try FileManager.default.createDirectory(
      at: root.appendingPathComponent("empty", isDirectory: true),
      withIntermediateDirectories: false)
    let nested = root.appendingPathComponent("nested", isDirectory: true)
    try FileManager.default.createDirectory(at: nested, withIntermediateDirectories: false)
    let binary = Data([0x00, 0xFF, 0x80, 0x41, 0x0A])
    try binary.write(to: nested.appendingPathComponent("raw.bin"))
    let scope = ScopeProbe()
    let walker = SidebarImportSourceWalker(
      limits: SidebarImportSourceLimits(
        refuseBytes: 1_024,
        totalBytes: 4_096,
        maximumEntries: 100,
        maximumDepth: 8),
      scopeAccess: scope.access())

    let prepared = try walker.prepare(rootURL: root, vaultURL: vault)
    XCTAssertEqual(scope.counts().starts, 1)
    XCTAssertEqual(scope.counts().stops, 0, "the scope stays active for later reads")
    XCTAssertEqual(
      prepared.manifest.entries.map { ($0.relativePath.display, $0.kind) },
      [
        ("", .directory),
        ("empty", .directory),
        ("nested", .directory),
        ("nested/raw.bin", .regularFile),
      ])
    let file = try XCTUnwrap(
      prepared.manifest.entries.first { $0.relativePath.display == "nested/raw.bin" })
    XCTAssertEqual(try prepared.readBytes(for: file), binary)

    prepared.close()
    prepared.close()
    XCTAssertEqual(scope.counts().starts, 1)
    XCTAssertEqual(scope.counts().stops, 1, "close is balanced and idempotent")
  }

  func testWalkerEntryBudgetChargesRootAndRejectedDirectoryRecords() throws {
    let vault = try makeVault()
    let root = tempDirectory.appendingPathComponent("source", isDirectory: true)
    try FileManager.default.createDirectory(at: root, withIntermediateDirectories: false)
    try Data("hidden".utf8).write(to: root.appendingPathComponent(".hidden"))
    let scope = ScopeProbe()
    let walker = SidebarImportSourceWalker(
      limits: SidebarImportSourceLimits(
        refuseBytes: 1_024,
        totalBytes: 4_096,
        maximumEntries: 3,
        maximumDepth: 8),
      scopeAccess: scope.access())

    XCTAssertThrowsError(try walker.prepare(rootURL: root, vaultURL: vault)) { error in
      guard case let SidebarImportSourceWalkerError.tooManyEntries(limit) = error else {
        XCTFail("expected terminal entry exhaustion, got \(error)")
        return
      }
      XCTAssertEqual(limit, 3)
    }
    XCTAssertEqual(scope.counts().starts, 1)
    XCTAssertEqual(scope.counts().stops, 1)
  }

  func testSourceLimitsUseProductionDefaultsAndClampBridgeCap() {
    let belowBridge = SidebarImportSourceLimits(sessionRefuseBytes: 123)
    XCTAssertEqual(belowBridge.refuseBytes, 123)
    XCTAssertEqual(belowBridge.totalBytes, 1_073_741_824)
    XCTAssertEqual(belowBridge.maximumEntries, 10_000)
    XCTAssertEqual(belowBridge.maximumDepth, 64)

    let aboveBridge = SidebarImportSourceLimits(sessionRefuseBytes: UInt64.max)
    XCTAssertEqual(aboveBridge.refuseBytes, Int64(Int32.max) - 4)
  }

  func testWalkerRejectsInvalidInjectedLimits() throws {
    let vault = try makeVault()
    let root = tempDirectory.appendingPathComponent("source.bin")
    try Data([0x01]).write(to: root)

    for limits in [
      SidebarImportSourceLimits(
        refuseBytes: -1, totalBytes: 1, maximumEntries: 1, maximumDepth: 0),
      SidebarImportSourceLimits(
        refuseBytes: 1, totalBytes: -1, maximumEntries: 1, maximumDepth: 0),
      SidebarImportSourceLimits(
        refuseBytes: 1, totalBytes: 1, maximumEntries: 0, maximumDepth: 0),
      SidebarImportSourceLimits(
        refuseBytes: 1, totalBytes: 1, maximumEntries: 1, maximumDepth: -1),
    ] {
      let scope = ScopeProbe()
      XCTAssertThrowsError(
        try SidebarImportSourceWalker(limits: limits, scopeAccess: scope.access())
          .prepare(rootURL: root, vaultURL: vault)
      ) { error in
        guard case SidebarImportSourceWalkerError.invalidLimits = error else {
          XCTFail("expected invalidLimits, got \(error)")
          return
        }
      }
      XCTAssertEqual(scope.counts().starts, 0)
      XCTAssertEqual(scope.counts().stops, 0)
    }
  }

  func testWalkerOversizedDescendantIsTypedFailureAndKeepsSafeSiblings() throws {
    let vault = try makeVault()
    let root = tempDirectory.appendingPathComponent("source", isDirectory: true)
    try FileManager.default.createDirectory(at: root, withIntermediateDirectories: false)
    let firstBytes = Data([0x41, 0x42])
    let lastBytes = Data([0x5A])
    try firstBytes.write(to: root.appendingPathComponent("a-safe.bin"))
    try Data([0x00, 0x01, 0x02, 0x03]).write(
      to: root.appendingPathComponent("m-oversized.bin"))
    try lastBytes.write(to: root.appendingPathComponent("z-safe.bin"))
    let scope = ScopeProbe()

    let prepared: SidebarImportPreparedSource
    do {
      prepared = try SidebarImportSourceWalker(
        limits: SidebarImportSourceLimits(
          refuseBytes: 3, totalBytes: 100, maximumEntries: 100, maximumDepth: 8),
        scopeAccess: scope.access()
      ).prepare(rootURL: root, vaultURL: vault)
    } catch {
      XCTFail("an oversized descendant must not abort safe siblings: \(error)")
      return
    }

    XCTAssertEqual(
      prepared.manifest.entries.map { ($0.relativePath.display, $0.kind) },
      [
        ("", .directory),
        ("a-safe.bin", .regularFile),
        ("z-safe.bin", .regularFile),
      ])
    XCTAssertEqual(prepared.manifest.failures.count, 1)
    XCTAssertEqual(
      prepared.manifest.failures.first?.relativePath.display,
      "m-oversized.bin")
    XCTAssertEqual(
      prepared.manifest.failures.first?.reason,
      .fileTooLarge(limitBytes: 3))
    let first = try XCTUnwrap(
      prepared.manifest.entries.first { $0.relativePath.display == "a-safe.bin" })
    let last = try XCTUnwrap(
      prepared.manifest.entries.first { $0.relativePath.display == "z-safe.bin" })
    XCTAssertEqual(try prepared.readBytes(for: first), firstBytes)
    XCTAssertEqual(try prepared.readBytes(for: last), lastBytes)

    prepared.close()
    XCTAssertEqual(scope.counts().starts, 1)
    XCTAssertEqual(scope.counts().stops, 1)
  }

  func testWalkerPerFileAndAdvisoryTotalExactBoundaries() throws {
    let vault = try makeVault()
    let root = tempDirectory.appendingPathComponent("source", isDirectory: true)
    try FileManager.default.createDirectory(at: root, withIntermediateDirectories: false)
    try Data([0x01, 0x02]).write(to: root.appendingPathComponent("a.bin"))
    try Data([0x03, 0x04, 0x05]).write(to: root.appendingPathComponent("b.bin"))
    let exactScope = ScopeProbe()
    let exact = try SidebarImportSourceWalker(
      limits: SidebarImportSourceLimits(
        refuseBytes: 3, totalBytes: 5, maximumEntries: 100, maximumDepth: 8),
      scopeAccess: exactScope.access()
    ).prepare(rootURL: root, vaultURL: vault)
    XCTAssertEqual(exact.manifest.entries.count, 3)
    XCTAssertEqual(exact.manifest.advisoryByteCount.totalBytes, 5)
    XCTAssertFalse(exact.manifest.advisoryByteCount.overflowed)
    XCTAssertFalse(exact.manifest.advisoryByteCount.exceedsConfiguredTotal)
    exact.close()

    let totalScope = ScopeProbe()
    let advisoryOverage = try SidebarImportSourceWalker(
      limits: SidebarImportSourceLimits(
        refuseBytes: 3, totalBytes: 4, maximumEntries: 100, maximumDepth: 8),
      scopeAccess: totalScope.access()
    ).prepare(rootURL: root, vaultURL: vault)
    XCTAssertEqual(advisoryOverage.manifest.entries.count, 3)
    XCTAssertEqual(advisoryOverage.manifest.failures, [])
    XCTAssertEqual(advisoryOverage.manifest.advisoryByteCount.totalBytes, 5)
    XCTAssertFalse(advisoryOverage.manifest.advisoryByteCount.overflowed)
    XCTAssertTrue(advisoryOverage.manifest.advisoryByteCount.exceedsConfiguredTotal)
    advisoryOverage.close()
    XCTAssertEqual(totalScope.counts().stops, 1)

    let rootOverageScope = ScopeProbe()
    let regularRoot = try SidebarImportSourceWalker(
      limits: SidebarImportSourceLimits(
        refuseBytes: 3, totalBytes: 2, maximumEntries: 100, maximumDepth: 8),
      scopeAccess: rootOverageScope.access()
    ).prepare(rootURL: root.appendingPathComponent("b.bin"), vaultURL: vault)
    XCTAssertEqual(regularRoot.manifest.entries.count, 1)
    XCTAssertTrue(regularRoot.manifest.advisoryByteCount.exceedsConfiguredTotal)
    regularRoot.close()
    XCTAssertEqual(rootOverageScope.counts().stops, 1)

    let oversized = tempDirectory.appendingPathComponent("oversized.bin")
    try Data([0x00, 0x01, 0x02, 0x03]).write(to: oversized)
    let fileScope = ScopeProbe()
    XCTAssertThrowsError(
      try SidebarImportSourceWalker(
        limits: SidebarImportSourceLimits(
          refuseBytes: 3, totalBytes: 100, maximumEntries: 100, maximumDepth: 8),
        scopeAccess: fileScope.access()
      ).prepare(rootURL: oversized, vaultURL: vault)
    ) { error in
      guard case SidebarImportSourceWalkerError.fileTooLarge(_, limitBytes: 3) = error else {
        XCTFail("expected fileTooLarge, got \(error)")
        return
      }
    }
    XCTAssertEqual(fileScope.counts().stops, 1)
  }

  func testWalkerAdvisoryByteCountSaturatesOnOverflowWithoutCreatingCapacity() {
    var advisory = SidebarImportSourceAdvisoryByteCount(configuredTotalBytes: UInt64.max)

    advisory.record(UInt64.max)
    advisory.record(1)

    XCTAssertEqual(advisory.totalBytes, UInt64.max)
    XCTAssertTrue(advisory.overflowed)
    XCTAssertTrue(advisory.exceedsConfiguredTotal)
  }

  func testWalkerGrowthProbeReadsAtMostCapPlusOneWithoutAdvertisedReserve() throws {
    let vault = try makeVault()
    let root = tempDirectory.appendingPathComponent("growing.bin")
    let cap = 128 * 1_024
    try Data(repeating: 0x41, count: cap).write(to: root)
    let scope = ScopeProbe()
    let metrics = SidebarImportSourceMetrics()
    let growth = OneShotGrowthProbe(fileURL: root)
    let walker = SidebarImportSourceWalker(
      limits: SidebarImportSourceLimits(
        refuseBytes: Int64(cap),
        totalBytes: Int64(cap),
        maximumEntries: 100,
        maximumDepth: 8),
      scopeAccess: scope.access(),
      hooks: SidebarImportSourceWalkerHooks(
        didReachBoundary: { boundary in
          if case let .beforeRead(_, offset) = boundary {
            growth.growOnFirstRead(offset: offset)
          }
        }),
      metrics: metrics)

    let prepared = try walker.prepare(rootURL: root, vaultURL: vault)
    let entry = try XCTUnwrap(prepared.manifest.entries.first)
    XCTAssertThrowsError(try prepared.readBytes(for: entry)) { error in
      guard case SidebarImportSourceWalkerError.fileTooLarge(_, limitBytes: Int64(cap)) = error
      else {
        XCTFail("expected grown-file rejection, got \(error)")
        return
      }
    }
    XCTAssertEqual(growth.recordedErrors(), [])
    let snapshot = metrics.snapshot()
    XCTAssertEqual(snapshot.totalReadRequestBytes, Int64(cap + 1))
    XCTAssertLessThanOrEqual(snapshot.maximumReservedCapacity, 64 * 1_024)
    prepared.close()
    XCTAssertEqual(metrics.snapshot().currentOwnedDescriptors, 0)
    XCTAssertEqual(scope.counts().stops, 1)
  }

  func testWalkerEntryBudgetExactBoundaryKeepsRejectedFailure() throws {
    let vault = try makeVault()
    let root = tempDirectory.appendingPathComponent("source", isDirectory: true)
    try FileManager.default.createDirectory(at: root, withIntermediateDirectories: false)
    try Data("hidden".utf8).write(to: root.appendingPathComponent(".hidden"))
    let scope = ScopeProbe()
    let prepared = try SidebarImportSourceWalker(
      limits: SidebarImportSourceLimits(
        refuseBytes: 1_024, totalBytes: 4_096, maximumEntries: 4, maximumDepth: 8),
      scopeAccess: scope.access()
    ).prepare(rootURL: root, vaultURL: vault)

    XCTAssertEqual(prepared.manifest.entries.count, 1)
    XCTAssertEqual(prepared.manifest.failures.map(\.reason), [.hidden])
    prepared.close()
    XCTAssertEqual(scope.counts().stops, 1)
  }

  func testWalkerRejectedEntryStressTerminalizesWithoutDescriptorGrowth() throws {
    let vault = try makeVault()
    let root = tempDirectory.appendingPathComponent("source", isDirectory: true)
    try FileManager.default.createDirectory(at: root, withIntermediateDirectories: false)
    for index in 0..<10_005 {
      let path = root.appendingPathComponent(String(format: ".rejected-%05d", index))
      XCTAssertTrue(FileManager.default.createFile(atPath: path.path, contents: Data()))
    }
    let scope = ScopeProbe()
    let metrics = SidebarImportSourceMetrics()
    XCTAssertThrowsError(
      try SidebarImportSourceWalker(
        limits: SidebarImportSourceLimits(sessionRefuseBytes: 1_024),
        scopeAccess: scope.access(),
        metrics: metrics
      ).prepare(rootURL: root, vaultURL: vault)
    ) { error in
      guard case SidebarImportSourceWalkerError.tooManyEntries(limit: 10_000) = error else {
        XCTFail("expected default entry-cap exhaustion, got \(error)")
        return
      }
    }
    let snapshot = metrics.snapshot()
    XCTAssertEqual(snapshot.currentOwnedDescriptors, 0)
    XCTAssertLessThanOrEqual(snapshot.highWaterOwnedDescriptors, 8)
    XCTAssertEqual(scope.counts().stops, 1)
  }

  func testRawNameComparatorIsInvariantAndUsesRawBytesForTies() {
    let fixtures: [[UInt8]] = [
      Array("a".utf8),
      Array("A".utf8),
      Array("ı".utf8),
      Array("İ".utf8),
      Array("i".utf8),
      Array("I".utf8),
      Array("é".utf8),
      Array("e\u{301}".utf8),
    ]
    let expected = ["A", "a", "e\u{301}", "I", "i", "İ", "é", "ı"]
      .map { Array($0.utf8) }

    for _ in 0..<20 {
      XCTAssertEqual(
        fixtures.sorted(by: SidebarImportSourceNameOrdering.precedes),
        expected)
    }
    XCTAssertTrue(
      SidebarImportSourceNameOrdering.precedes(Array("A".utf8), Array("a".utf8)),
      "equal folded keys must be broken by original raw bytes")

    let source = try? String(
      contentsOf: URL(fileURLWithPath: #filePath)
        .deletingLastPathComponent()
        .deletingLastPathComponent()
        .deletingLastPathComponent()
        .appendingPathComponent("Sources/SlateMac/Sidebar/SidebarImportSourceWalker.swift"),
      encoding: .utf8)
    XCTAssertFalse(source?.contains("Locale.current") ?? true)
    XCTAssertFalse(source?.contains("localizedCompare") ?? true)
  }

  func testWalkerInvalidUTF8NameIsTypedFailureAndKeepsSafeSibling() throws {
    let vault = try makeVault()
    let root = tempDirectory.appendingPathComponent("source", isDirectory: true)
    try FileManager.default.createDirectory(at: root, withIntermediateDirectories: false)
    try Data([0x42]).write(to: root.appendingPathComponent("safe.bin"))
    let scope = ScopeProbe()
    let prepared = try SidebarImportSourceWalker(
      limits: SidebarImportSourceLimits(
        refuseBytes: 1_024,
        totalBytes: 4_096,
        maximumEntries: 100,
        maximumDepth: 8),
      scopeAccess: scope.access(),
      hooks: SidebarImportSourceWalkerHooks(
        injectedDirectoryRecords: { path in path.isEmpty ? [[0xFF]] : [] })
    ).prepare(rootURL: root, vaultURL: vault)

    XCTAssertEqual(
      prepared.manifest.entries.map { ($0.relativePath.display, $0.kind) },
      [
        ("", .directory),
        ("safe.bin", .regularFile),
      ])
    XCTAssertEqual(prepared.manifest.failures.count, 1)
    XCTAssertEqual(prepared.manifest.failures.first?.reason, .invalidFileName)
    prepared.close()
  }

  func testWalkerDepth64SucceedsAndDepth65FailsWithoutOpenWhileSiblingRemains() throws {
    let vault = try makeVault()
    let root = tempDirectory.appendingPathComponent("source", isDirectory: true)
    try FileManager.default.createDirectory(at: root, withIntermediateDirectories: false)
    try Data([0x41]).write(to: root.appendingPathComponent("safe.bin"))
    var parent = root
    var components: [String] = []
    for depth in 1...65 {
      let component = String(format: "d%02d", depth)
      components.append(component)
      parent = parent.appendingPathComponent(component, isDirectory: true)
      try FileManager.default.createDirectory(at: parent, withIntermediateDirectories: false)
    }
    let rejectedPath = components.joined(separator: "/")
    let acceptedPath = components.dropLast().joined(separator: "/")
    let probe = BoundaryProbe()
    let scope = ScopeProbe()
    let prepared = try SidebarImportSourceWalker(
      limits: SidebarImportSourceLimits(
        refuseBytes: 1_024,
        totalBytes: 4_096,
        maximumEntries: 1_000,
        maximumDepth: 64),
      scopeAccess: scope.access(),
      hooks: SidebarImportSourceWalkerHooks(
        didReachBoundary: { probe.record($0) })
    ).prepare(rootURL: root, vaultURL: vault)

    XCTAssertTrue(
      prepared.manifest.entries.contains { $0.relativePath.display == acceptedPath })
    XCTAssertTrue(
      prepared.manifest.entries.contains { $0.relativePath.display == "safe.bin" })
    XCTAssertEqual(
      prepared.manifest.failures.first { $0.relativePath.display == rejectedPath }?.reason,
      .tooDeep)
    XCTAssertEqual(
      probe.count {
        if case .beforeOpen(rejectedPath) = $0 { return true }
        return false
      },
      0,
      "depth 65 must be rejected before opening the component")
    prepared.close()
  }

  func testWalkerLargeFlatManifestKeepsDescriptorsBounded() throws {
    let vault = try makeVault()
    let root = tempDirectory.appendingPathComponent("source", isDirectory: true)
    try FileManager.default.createDirectory(at: root, withIntermediateDirectories: false)
    for index in 0..<300 {
      let path = root.appendingPathComponent(String(format: "file-%03d.bin", index))
      XCTAssertTrue(FileManager.default.createFile(atPath: path.path, contents: Data([0x41])))
    }
    let scope = ScopeProbe()
    let metrics = SidebarImportSourceMetrics()
    let prepared = try SidebarImportSourceWalker(
      limits: SidebarImportSourceLimits(
        refuseBytes: 1_024,
        totalBytes: 4_096,
        maximumEntries: 1_000,
        maximumDepth: 8),
      scopeAccess: scope.access(),
      metrics: metrics
    ).prepare(rootURL: root, vaultURL: vault)

    XCTAssertEqual(prepared.manifest.entries.count, 301)
    XCTAssertEqual(metrics.snapshot().currentOwnedDescriptors, 1)
    XCTAssertLessThanOrEqual(metrics.snapshot().highWaterOwnedDescriptors, 8)
    let last = try XCTUnwrap(prepared.manifest.entries.last)
    XCTAssertEqual(try prepared.readBytes(for: last), Data([0x41]))
    XCTAssertEqual(metrics.snapshot().currentOwnedDescriptors, 1)
    XCTAssertEqual(metrics.snapshot().highWaterActiveReads, 1)
    prepared.close()
    XCTAssertEqual(metrics.snapshot().currentOwnedDescriptors, 0)
    XCTAssertEqual(metrics.snapshot().activeReads, 0)
  }

  func testWalkerCancellationBeforePrepareNeverStartsScope() throws {
    let vault = try makeVault()
    let root = tempDirectory.appendingPathComponent("source.bin")
    try Data([0x41]).write(to: root)
    let scope = ScopeProbe()
    let metrics = SidebarImportSourceMetrics()
    let cancellation = SidebarImportSourceCancellationToken()
    cancellation.cancel()
    let probe = BoundaryProbe()

    XCTAssertThrowsError(
      try SidebarImportSourceWalker(
        limits: SidebarImportSourceLimits(sessionRefuseBytes: 1_024),
        scopeAccess: scope.access(),
        hooks: SidebarImportSourceWalkerHooks(
          didReachBoundary: { probe.record($0) }),
        cancellation: cancellation,
        metrics: metrics
      ).prepare(rootURL: root, vaultURL: vault)
    ) { error in
      guard case SidebarImportSourceWalkerError.cancelled = error else {
        XCTFail("expected typed cancellation, got \(error)")
        return
      }
    }
    XCTAssertEqual(probe.values(), [.beforeRootAdmission])
    XCTAssertEqual(scope.counts().starts, 0)
    XCTAssertEqual(scope.counts().stops, 0)
    XCTAssertEqual(metrics.snapshot().currentOwnedDescriptors, 0)
  }

  func testWalkerCancellationMidReaddirClosesDescriptorsAndScope() throws {
    let vault = try makeVault()
    let root = tempDirectory.appendingPathComponent("source", isDirectory: true)
    try FileManager.default.createDirectory(at: root, withIntermediateDirectories: false)
    for index in 0..<20 {
      try Data([UInt8(index)]).write(
        to: root.appendingPathComponent(String(format: "file-%02d", index)))
    }
    let scope = ScopeProbe()
    let metrics = SidebarImportSourceMetrics()
    let cancellation = SidebarImportSourceCancellationToken()
    let probe = BoundaryProbe()
    let hooks = SidebarImportSourceWalkerHooks(didReachBoundary: { boundary in
      probe.record(boundary)
      if case .beforeDirectoryRead = boundary,
        probe.count(where: {
          if case .beforeDirectoryRead = $0 { return true }
          return false
        }) == 4
      {
        cancellation.cancel()
      }
    })

    XCTAssertThrowsError(
      try SidebarImportSourceWalker(
        limits: SidebarImportSourceLimits(sessionRefuseBytes: 1_024),
        scopeAccess: scope.access(),
        hooks: hooks,
        cancellation: cancellation,
        metrics: metrics
      ).prepare(rootURL: root, vaultURL: vault)
    ) { error in
      guard case SidebarImportSourceWalkerError.cancelled = error else {
        XCTFail("expected typed cancellation, got \(error)")
        return
      }
    }
    XCTAssertEqual(scope.counts().stops, 1)
    XCTAssertEqual(metrics.snapshot().currentOwnedDescriptors, 0)
  }

  func testWalkerCancellationBeforeRecursionEntersNoLaterBoundary() throws {
    let vault = try makeVault()
    let root = tempDirectory.appendingPathComponent("source", isDirectory: true)
    let child = root.appendingPathComponent("child", isDirectory: true)
    try FileManager.default.createDirectory(at: child, withIntermediateDirectories: true)
    try Data([0x41]).write(to: child.appendingPathComponent("nested.bin"))
    let scope = ScopeProbe()
    let metrics = SidebarImportSourceMetrics()
    let cancellation = SidebarImportSourceCancellationToken()
    let probe = BoundaryProbe()
    let hooks = SidebarImportSourceWalkerHooks(didReachBoundary: { boundary in
      probe.record(boundary)
      if case .beforeRecursion("child") = boundary {
        cancellation.cancel()
      }
    })

    XCTAssertThrowsError(
      try SidebarImportSourceWalker(
        limits: SidebarImportSourceLimits(sessionRefuseBytes: 1_024),
        scopeAccess: scope.access(),
        hooks: hooks,
        cancellation: cancellation,
        metrics: metrics
      ).prepare(rootURL: root, vaultURL: vault)
    ) { error in
      guard case SidebarImportSourceWalkerError.cancelled = error else {
        XCTFail("expected typed cancellation, got \(error)")
        return
      }
    }
    XCTAssertEqual(
      probe.count {
        if case .beforeDirectoryRead("child") = $0 { return true }
        return false
      },
      0)
    XCTAssertEqual(scope.counts().stops, 1)
    XCTAssertEqual(metrics.snapshot().currentOwnedDescriptors, 0)
  }

  func testWalkerCancellationBeforeAndMidReadReleasesLeases() throws {
    let vault = try makeVault()
    let root = tempDirectory.appendingPathComponent("source.bin")
    try Data(repeating: 0x41, count: 128 * 1_024).write(to: root)
    let scope = ScopeProbe()
    let metrics = SidebarImportSourceMetrics()
    let cancellation = SidebarImportSourceCancellationToken()
    let probe = BoundaryProbe()
    let prepared = try SidebarImportSourceWalker(
      limits: SidebarImportSourceLimits(
        refuseBytes: 256 * 1_024,
        totalBytes: 256 * 1_024,
        maximumEntries: 10,
        maximumDepth: 8),
      scopeAccess: scope.access(),
      hooks: SidebarImportSourceWalkerHooks(didReachBoundary: { boundary in
        probe.record(boundary)
        if case let .beforeRead(_, offset) = boundary, offset > 0 {
          cancellation.cancel()
        }
      }),
      cancellation: cancellation,
      metrics: metrics
    ).prepare(rootURL: root, vaultURL: vault)
    let entry = try XCTUnwrap(prepared.manifest.entries.first)

    XCTAssertThrowsError(try prepared.readBytes(for: entry)) { error in
      guard case SidebarImportSourceWalkerError.cancelled = error else {
        XCTFail("expected typed cancellation, got \(error)")
        return
      }
    }
    XCTAssertGreaterThan(
      probe.count {
        if case .beforeRead = $0 { return true }
        return false
      },
      1)
    XCTAssertEqual(metrics.snapshot().activeReads, 0)
    XCTAssertEqual(metrics.snapshot().currentOwnedDescriptors, 0)
    XCTAssertEqual(scope.counts().stops, 1)

    XCTAssertThrowsError(try prepared.readBytes(for: entry)) { error in
      guard case SidebarImportSourceWalkerError.cancelled = error else {
        XCTFail("expected cancellation before a new lease, got \(error)")
        return
      }
    }
    XCTAssertEqual(metrics.snapshot().activeReads, 0)
    XCTAssertEqual(metrics.snapshot().currentOwnedDescriptors, 0)
    XCTAssertEqual(scope.counts().stops, 1)
  }

  func testPreparedReadCancellationAfterPreadEOFClosesAuthorityWithoutCallerClose() throws {
    let vault = try makeVault()
    let root = tempDirectory.appendingPathComponent("empty.bin")
    try Data().write(to: root)
    let scope = ScopeProbe()
    let metrics = SidebarImportSourceMetrics()
    let cancellation = SidebarImportSourceCancellationToken()
    let prepared = try SidebarImportSourceWalker(
      limits: SidebarImportSourceLimits(sessionRefuseBytes: 1_024),
      scopeAccess: scope.access(),
      hooks: SidebarImportSourceWalkerHooks(didReachBoundary: { boundary in
        if case .afterRead(_, _, true) = boundary {
          cancellation.cancel()
        }
      }),
      cancellation: cancellation,
      metrics: metrics
    ).prepare(rootURL: root, vaultURL: vault)
    let entry = try XCTUnwrap(prepared.manifest.entries.first)

    XCTAssertThrowsError(try prepared.readBytes(for: entry)) { error in
      guard case SidebarImportSourceWalkerError.cancelled = error else {
        XCTFail("expected cancellation after the EOF syscall, got \(error)")
        return
      }
    }
    XCTAssertEqual(metrics.snapshot().activeReads, 0)
    XCTAssertEqual(metrics.snapshot().currentOwnedDescriptors, 0)
    XCTAssertEqual(scope.counts().stops, 1)
  }

  func testWalkerCancellationAfterReaddirEOFReleasesAuthority() throws {
    let vault = try makeVault()
    let root = tempDirectory.appendingPathComponent("empty-source", isDirectory: true)
    try FileManager.default.createDirectory(at: root, withIntermediateDirectories: false)
    let scope = ScopeProbe()
    let metrics = SidebarImportSourceMetrics()
    let cancellation = SidebarImportSourceCancellationToken()

    XCTAssertThrowsError(
      try SidebarImportSourceWalker(
        limits: SidebarImportSourceLimits(sessionRefuseBytes: 1_024),
        scopeAccess: scope.access(),
        hooks: SidebarImportSourceWalkerHooks(didReachBoundary: { boundary in
          if case .afterDirectoryRead(_, true) = boundary {
            cancellation.cancel()
          }
        }),
        cancellation: cancellation,
        metrics: metrics
      ).prepare(rootURL: root, vaultURL: vault)
    ) { error in
      guard case SidebarImportSourceWalkerError.cancelled = error else {
        XCTFail("expected cancellation after the EOF syscall, got \(error)")
        return
      }
    }
    XCTAssertEqual(metrics.snapshot().currentOwnedDescriptors, 0)
    XCTAssertEqual(scope.counts().starts, 1)
    XCTAssertEqual(scope.counts().stops, 1)
  }

  func testPreparedCloseAndCancellationDrainTwoReadLeasesOnce() throws {
    let vault = try makeVault()
    let root = tempDirectory.appendingPathComponent("source.bin")
    let bytes = Data(repeating: 0x41, count: 4_096)
    try bytes.write(to: root)
    let scope = ScopeProbe()
    let metrics = SidebarImportSourceMetrics()
    let cancellation = SidebarImportSourceCancellationToken()
    let gate = TwoReadGate()
    let prepared = try SidebarImportSourceWalker(
      limits: SidebarImportSourceLimits(sessionRefuseBytes: 8_192),
      scopeAccess: scope.access(),
      hooks: SidebarImportSourceWalkerHooks(didAcquireReadLease: { gate.block() }),
      cancellation: cancellation,
      metrics: metrics
    ).prepare(rootURL: root, vaultURL: vault)
    let entry = try XCTUnwrap(prepared.manifest.entries.first)
    let first = ResultBox<Data>()
    let second = ResultBox<Data>()
    let finished = expectation(description: "both leased reads finish")
    finished.expectedFulfillmentCount = 2

    DispatchQueue.global().async {
      first.store(Result { try prepared.readBytes(for: entry) })
      finished.fulfill()
    }
    DispatchQueue.global().async {
      second.store(Result { try prepared.readBytes(for: entry) })
      finished.fulfill()
    }
    XCTAssertTrue(gate.waitForBoth())
    XCTAssertEqual(metrics.snapshot().activeReads, 2)
    XCTAssertEqual(metrics.snapshot().highWaterActiveReads, 2)

    prepared.close()
    XCTAssertEqual(scope.counts().stops, 0)
    XCTAssertThrowsError(try prepared.readBytes(for: entry)) { error in
      guard case SidebarImportSourceWalkerError.preparedSourceClosed = error else {
        XCTFail("expected immediate close admission failure, got \(error)")
        return
      }
    }
    cancellation.cancel()
    gate.unblockBoth()
    wait(for: [finished], timeout: 2)

    for result in [first.load(), second.load()] {
      XCTAssertThrowsError(try XCTUnwrap(result).get()) { error in
        guard case SidebarImportSourceWalkerError.cancelled = error else {
          XCTFail("expected leased-read cancellation, got \(error)")
          return
        }
      }
    }
    XCTAssertEqual(metrics.snapshot().activeReads, 0)
    XCTAssertEqual(metrics.snapshot().currentOwnedDescriptors, 0)
    XCTAssertEqual(scope.counts().stops, 1)
    prepared.close()
    XCTAssertEqual(scope.counts().stops, 1)
  }

  func testWalkerPreparesRegularFileRootAndPreservesBinaryBytes() throws {
    let vault = tempDirectory.appendingPathComponent("vault", isDirectory: true)
    try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: false)
    let root = tempDirectory.appendingPathComponent("raw.bin")
    let binary = Data([0x00, 0xFF, 0x80, 0x41, 0x0A])
    try binary.write(to: root)
    let scope = ScopeProbe()
    let walker = SidebarImportSourceWalker(
      limits: SidebarImportSourceLimits(
        refuseBytes: 1_024,
        totalBytes: 4_096,
        maximumEntries: 100,
        maximumDepth: 8),
      scopeAccess: scope.access())

    let prepared = try walker.prepare(rootURL: root, vaultURL: vault)
    XCTAssertEqual(
      prepared.manifest.entries.map { ($0.relativePath.display, $0.kind) },
      [("", .regularFile)])
    let entry = try XCTUnwrap(prepared.manifest.entries.first)
    XCTAssertEqual(try prepared.readBytes(for: entry), binary)
    XCTAssertEqual(try prepared.readBytes(for: entry), binary)

    prepared.close()
    XCTAssertEqual(scope.counts().starts, 1)
    XCTAssertEqual(scope.counts().stops, 1)
  }

  func testWalkerRejectsEverySymlinkPositionWithoutDereferencing() throws {
    let vault = try makeVault()
    let target = tempDirectory.appendingPathComponent("target", isDirectory: true)
    try FileManager.default.createDirectory(at: target, withIntermediateDirectories: false)
    try Data("secret".utf8).write(to: target.appendingPathComponent("secret.txt"))

    let finalLink = tempDirectory.appendingPathComponent("final-link", isDirectory: true)
    try FileManager.default.createSymbolicLink(at: finalLink, withDestinationURL: target)

    let container = tempDirectory.appendingPathComponent("container", isDirectory: true)
    let source = container.appendingPathComponent("source", isDirectory: true)
    try FileManager.default.createDirectory(at: source, withIntermediateDirectories: true)
    let containerLink = tempDirectory.appendingPathComponent("container-link", isDirectory: true)
    try FileManager.default.createSymbolicLink(at: containerLink, withDestinationURL: container)
    let intermediateLink = containerLink.appendingPathComponent("source", isDirectory: true)

    let dangling = tempDirectory.appendingPathComponent("dangling")
    try FileManager.default.createSymbolicLink(
      atPath: dangling.path,
      withDestinationPath: tempDirectory.appendingPathComponent("missing").path)
    let loopA = tempDirectory.appendingPathComponent("loop-a")
    let loopB = tempDirectory.appendingPathComponent("loop-b")
    try FileManager.default.createSymbolicLink(atPath: loopA.path, withDestinationPath: loopB.path)
    try FileManager.default.createSymbolicLink(atPath: loopB.path, withDestinationPath: loopA.path)

    for root in [finalLink, intermediateLink, dangling, loopA] {
      let scope = ScopeProbe()
      assertRootRejected(root, vault: vault, as: .symbolicLink, scope: scope)
      XCTAssertEqual(scope.counts().starts, 1)
      XCTAssertEqual(scope.counts().stops, 1)
    }
  }

  func testWalkerKeepsSafeSiblingsAndReportsTypedDescendantFailures() throws {
    let vault = try makeVault()
    let root = tempDirectory.appendingPathComponent("source", isDirectory: true)
    try FileManager.default.createDirectory(at: root, withIntermediateDirectories: false)
    let safeBytes = Data([0x00, 0xFF, 0x41])
    try safeBytes.write(to: root.appendingPathComponent("safe.bin"))
    try Data("hidden".utf8).write(to: root.appendingPathComponent(".hidden"))

    let externalFile = tempDirectory.appendingPathComponent("external.txt")
    let externalDirectory = tempDirectory.appendingPathComponent("external-dir", isDirectory: true)
    try Data("external".utf8).write(to: externalFile)
    try FileManager.default.createDirectory(
      at: externalDirectory, withIntermediateDirectories: false)
    try FileManager.default.createSymbolicLink(
      at: root.appendingPathComponent("file-link"), withDestinationURL: externalFile)
    try FileManager.default.createSymbolicLink(
      at: root.appendingPathComponent("directory-link", isDirectory: true),
      withDestinationURL: externalDirectory)
    try FileManager.default.createSymbolicLink(
      atPath: root.appendingPathComponent("dangling").path,
      withDestinationPath: root.appendingPathComponent("missing").path)
    try FileManager.default.createSymbolicLink(
      atPath: root.appendingPathComponent("loop-a").path,
      withDestinationPath: root.appendingPathComponent("loop-b").path)
    try FileManager.default.createSymbolicLink(
      atPath: root.appendingPathComponent("loop-b").path,
      withDestinationPath: root.appendingPathComponent("loop-a").path)

    let fifo = root.appendingPathComponent("fifo")
    XCTAssertEqual(Darwin.mkfifo(fifo.path, 0o600), 0)
    let socketURL = root.appendingPathComponent("socket")
    let socketFD = try bindUnixSocket(at: socketURL)
    defer { Darwin.close(socketFD) }
    let unreadable = root.appendingPathComponent("unreadable")
    try Data("no access".utf8).write(to: unreadable)
    XCTAssertEqual(Darwin.chmod(unreadable.path, 0), 0)

    let scope = ScopeProbe()
    let prepared = try makeWalker(scope: scope).prepare(rootURL: root, vaultURL: vault)
    XCTAssertEqual(
      prepared.manifest.entries.map { ($0.relativePath.display, $0.kind) },
      [
        ("", .directory),
        ("safe.bin", .regularFile),
      ])
    XCTAssertEqual(
      prepared.manifest.failures.map { $0.relativePath.display },
      [
        ".hidden",
        "dangling",
        "directory-link",
        "fifo",
        "file-link",
        "loop-a",
        "loop-b",
        "socket",
        "unreadable",
      ])
    XCTAssertEqual(
      prepared.manifest.failures.map { $0.reason },
      [
        .hidden,
        .symbolicLink,
        .symbolicLink,
        .fifo,
        .symbolicLink,
        .symbolicLink,
        .symbolicLink,
        .socket,
        .unreadable,
      ])
    let safe = try XCTUnwrap(
      prepared.manifest.entries.first { $0.relativePath.display == "safe.bin" })
    XCTAssertEqual(try prepared.readBytes(for: safe), safeBytes)

    XCTAssertEqual(scope.counts().stops, 0)
    prepared.close()
    XCTAssertEqual(scope.counts().stops, 1)
  }

  func testWalkerRejectsHiddenUnreadableAndSpecialRootsWithTypedReasons() throws {
    let vault = try makeVault()
    let hidden = tempDirectory.appendingPathComponent(".hidden-root", isDirectory: true)
    try FileManager.default.createDirectory(at: hidden, withIntermediateDirectories: false)
    let unreadable = tempDirectory.appendingPathComponent("unreadable-root")
    try Data("no access".utf8).write(to: unreadable)
    XCTAssertEqual(Darwin.chmod(unreadable.path, 0), 0)
    let fifo = tempDirectory.appendingPathComponent("fifo-root")
    XCTAssertEqual(Darwin.mkfifo(fifo.path, 0o600), 0)
    let socketURL = tempDirectory.appendingPathComponent("socket-root")
    let socketFD = try bindUnixSocket(at: socketURL)
    defer { Darwin.close(socketFD) }

    for (root, reason) in [
      (hidden, SidebarImportSourceFailureReason.hidden),
      (unreadable, .unreadable),
      (fifo, .fifo),
      (socketURL, .socket),
      (URL(fileURLWithPath: "/dev/null"), .characterDevice),
    ] {
      let scope = ScopeProbe()
      assertRootRejected(root, vault: vault, as: reason, scope: scope)
      XCTAssertEqual(scope.counts().starts, 1)
      XCTAssertEqual(scope.counts().stops, 1)
    }
  }

  func testWalkerRejectsVaultAndItsAncestorsByOpenedIdentity() throws {
    let container = tempDirectory.appendingPathComponent("container", isDirectory: true)
    let vault = container.appendingPathComponent("vault", isDirectory: true)
    try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
    let vaultLink = tempDirectory.appendingPathComponent("vault-link", isDirectory: true)
    try FileManager.default.createSymbolicLink(at: vaultLink, withDestinationURL: vault)

    for (root, trustedVault) in [
      (vault, vault),
      (container, vault),
      (vault, vaultLink),
      (container, vaultLink),
    ] {
      let scope = ScopeProbe()
      XCTAssertThrowsError(
        try makeWalker(scope: scope).prepare(rootURL: root, vaultURL: trustedVault)
      ) { error in
        guard case let SidebarImportSourceWalkerError.sourceContainsVault(path) = error else {
          XCTFail("expected sourceContainsVault, got \(error)")
          return
        }
        XCTAssertEqual(path, root.path)
      }
      XCTAssertEqual(scope.counts().starts, 1)
      XCTAssertEqual(scope.counts().stops, 1)
    }
  }

  func testWalkerTreatsFalseScopeStartAsSuccessAndBalancesAcquiredScopesOnce() throws {
    let vault = try makeVault()
    let root = tempDirectory.appendingPathComponent("source", isDirectory: true)
    try FileManager.default.createDirectory(at: root, withIntermediateDirectories: false)
    try Data("safe".utf8).write(to: root.appendingPathComponent("safe.txt"))

    let inactiveScope = ScopeProbe(startResult: false)
    let inactivePrepared = try makeWalker(scope: inactiveScope).prepare(
      rootURL: root,
      vaultURL: vault)
    inactivePrepared.close()
    inactivePrepared.close()
    XCTAssertEqual(inactiveScope.counts().starts, 1)
    XCTAssertEqual(inactiveScope.counts().stops, 0)

    let deinitScope = ScopeProbe()
    var deinitialized: SidebarImportPreparedSource? = try makeWalker(scope: deinitScope).prepare(
      rootURL: root,
      vaultURL: vault)
    XCTAssertNotNil(deinitialized)
    deinitialized = nil
    XCTAssertEqual(deinitScope.counts().starts, 1)
    XCTAssertEqual(deinitScope.counts().stops, 1)

    let readFailureScope = ScopeProbe()
    var readFailurePrepared: SidebarImportPreparedSource? = try makeWalker(
      scope: readFailureScope
    ).prepare(rootURL: root, vaultURL: vault)
    let safeEntry = try XCTUnwrap(
      readFailurePrepared?.manifest.entries.first { $0.relativePath.display == "safe.txt" })
    try FileManager.default.removeItem(at: root.appendingPathComponent("safe.txt"))
    try FileManager.default.createSymbolicLink(
      at: root.appendingPathComponent("safe.txt"),
      withDestinationURL: tempDirectory.appendingPathComponent("missing"))
    XCTAssertThrowsError(try readFailurePrepared?.readBytes(for: safeEntry))
    XCTAssertEqual(readFailureScope.counts().stops, 0)
    readFailurePrepared = nil
    XCTAssertEqual(readFailureScope.counts().starts, 1)
    XCTAssertEqual(readFailureScope.counts().stops, 1)

    let rejectedScope = ScopeProbe(startResult: false)
    let missing = tempDirectory.appendingPathComponent("missing-source")
    XCTAssertThrowsError(
      try makeWalker(scope: rejectedScope).prepare(rootURL: missing, vaultURL: vault))
    XCTAssertEqual(rejectedScope.counts().starts, 1)
    XCTAssertEqual(rejectedScope.counts().stops, 0)
  }

  func testPreparedCloseDefersAuthorityUntilLeasedDescendantReadFinishes() throws {
    let vault = try makeVault()
    let root = tempDirectory.appendingPathComponent("source", isDirectory: true)
    try FileManager.default.createDirectory(at: root, withIntermediateDirectories: false)
    let bytes = Data([0x00, 0xFF, 0x41, 0x42])
    try bytes.write(to: root.appendingPathComponent("safe.bin"))
    let scope = ScopeProbe()
    let gate = BlockingGate()
    let walker = SidebarImportSourceWalker(
      limits: SidebarImportSourceLimits(
        refuseBytes: 1_024,
        totalBytes: 4_096,
        maximumEntries: 100,
        maximumDepth: 8),
      scopeAccess: scope.access(),
      hooks: SidebarImportSourceWalkerHooks(
        didAcquireReadLease: { gate.block() }))
    let prepared = try walker.prepare(rootURL: root, vaultURL: vault)
    let entry = try XCTUnwrap(
      prepared.manifest.entries.first { $0.relativePath.display == "safe.bin" })
    let result = ResultBox<Data>()
    let readFinished = expectation(description: "leased read finished")

    DispatchQueue.global().async {
      result.store(Result { try prepared.readBytes(for: entry) })
      readFinished.fulfill()
    }
    XCTAssertTrue(gate.waitUntilBlocked(), "read must own a lease before close")

    prepared.close()
    XCTAssertEqual(scope.counts().stops, 0, "active read retains scope authority")
    XCTAssertThrowsError(try prepared.readBytes(for: entry)) { error in
      guard case SidebarImportSourceWalkerError.preparedSourceClosed = error else {
        XCTFail("expected preparedSourceClosed, got \(error)")
        return
      }
    }

    gate.unblock()
    wait(for: [readFinished], timeout: 1)
    XCTAssertEqual(try XCTUnwrap(result.load()).get(), bytes)
    XCTAssertEqual(scope.counts().stops, 1)
    prepared.close()
    XCTAssertEqual(scope.counts().stops, 1)
  }

  func testWalkerClassifiesPostInspectionKindSwapsFromOpenedMetadata() throws {
    let vault = try makeVault()
    let root = tempDirectory.appendingPathComponent("source", isDirectory: true)
    try FileManager.default.createDirectory(at: root, withIntermediateDirectories: false)
    let directorySwap = root.appendingPathComponent("directory-swap")
    let fifoSwap = root.appendingPathComponent("fifo-swap")
    try Data("regular".utf8).write(to: directorySwap)
    try Data("regular".utf8).write(to: fifoSwap)
    let probe = PostInspectionSwapProbe(
      directoryURL: directorySwap,
      fifoURL: fifoSwap)
    let scope = ScopeProbe()
    let walker = SidebarImportSourceWalker(
      limits: SidebarImportSourceLimits(
        refuseBytes: 1_024,
        totalBytes: 4_096,
        maximumEntries: 100,
        maximumDepth: 8),
      scopeAccess: scope.access(),
      hooks: SidebarImportSourceWalkerHooks(
        didInspectDescendant: { probe.swap(relativePath: $0) }))

    let prepared = try walker.prepare(rootURL: root, vaultURL: vault)

    XCTAssertEqual(probe.recordedErrors(), [])
    XCTAssertEqual(
      prepared.manifest.entries.map { ($0.relativePath.display, $0.kind) },
      [("", .directory)])
    XCTAssertEqual(
      prepared.manifest.failures.map { $0.relativePath.display },
      ["directory-swap", "fifo-swap"])
    XCTAssertEqual(
      prepared.manifest.failures.map { $0.reason },
      [
        .entryKindChanged,
        .fifo,
      ])
    prepared.close()
    XCTAssertEqual(scope.counts().stops, 1)
  }

  func testReservableReadKeepsOpenedSizeTentativeWhileReadyBytesAreHeld() throws {
    let vault = try makeVault()
    let root = tempDirectory.appendingPathComponent("source.bin")
    let bytes = Data([0x00, 0xFF, 0x41, 0x42])
    try bytes.write(to: root)
    let scope = ScopeProbe()
    let prepared = try makeWalker(scope: scope).prepare(rootURL: root, vaultURL: vault)
    defer { prepared.close() }
    let entry = try XCTUnwrap(prepared.manifest.entries.first)
    let scheduler = SidebarImportByteScheduler(
      totalLimitBytes: 16,
      normalResidentLimitBytes: 8,
      cancellation: SidebarImportSourceCancellationToken())

    let ready = try prepared.readBytes(for: entry, scheduler: scheduler)

    XCTAssertEqual(ready.data, bytes)
    XCTAssertEqual(scheduler.snapshot().tentativeBytes, UInt64(bytes.count))
    XCTAssertEqual(scheduler.snapshot().normalResidentBytes, UInt64(bytes.count))
    ready.discard()
    XCTAssertEqual(scheduler.snapshot().tentativeBytes, 0)
    XCTAssertEqual(scheduler.snapshot().normalResidentBytes, 0)
  }

  func testReservableReadKeepsOpenedReservationUntilEOF() throws {
    let vault = try makeVault()
    let root = tempDirectory.appendingPathComponent("source.bin")
    let openedSize = 70 * 1_024
    try Data(repeating: 0x41, count: openedSize).write(to: root)
    let cancellation = SidebarImportSourceCancellationToken()
    let scheduler = SidebarImportByteScheduler(
      totalLimitBytes: 100 * 1_024,
      normalResidentLimitBytes: 100 * 1_024,
      cancellation: cancellation)
    let observed = ValueBox<SidebarImportByteSchedulerSnapshot>()
    let scope = ScopeProbe()
    let prepared = try SidebarImportSourceWalker(
      limits: SidebarImportSourceLimits(sessionRefuseBytes: 100 * 1_024),
      scopeAccess: scope.access(),
      hooks: SidebarImportSourceWalkerHooks(didReachBoundary: { boundary in
        if case .beforeRead(_, offset: 64 * 1_024) = boundary {
          observed.store(scheduler.snapshot())
          scheduler.cancel()
        }
      }),
      cancellation: cancellation
    ).prepare(rootURL: root, vaultURL: vault)
    let entry = try XCTUnwrap(prepared.manifest.entries.first)

    XCTAssertThrowsError(try prepared.readBytes(for: entry, scheduler: scheduler)) { error in
      guard case SidebarImportSourceWalkerError.cancelled = error else {
        XCTFail("expected shared cancellation, got \(error)")
        return
      }
    }
    let beforeSecondChunk = try XCTUnwrap(observed.load())
    XCTAssertEqual(beforeSecondChunk.tentativeBytes, UInt64(openedSize))
    XCTAssertEqual(beforeSecondChunk.normalResidentBytes, UInt64(openedSize))
    XCTAssertEqual(beforeSecondChunk.normalPlannedBytes, UInt64(openedSize))
    XCTAssertEqual(scheduler.snapshot().tentativeBytes, 0)
    XCTAssertEqual(scope.counts().stops, 1)
  }

  func testByteSchedulerCommitsExactTotalAndRejectsOneByteBeyond() throws {
    let scheduler = SidebarImportByteScheduler(
      totalLimitBytes: 8,
      normalResidentLimitBytes: 8,
      cancellation: SidebarImportSourceCancellationToken())
    let exact = try scheduler.reserve(advisoryByteCount: 8)
    let ready = try exact.makeReady(data: Data(repeating: 0x41, count: 8))

    ready.publishSucceeded()
    ready.publishSucceeded()
    ready.discard()
    XCTAssertEqual(scheduler.snapshot().committedBytes, 8)
    XCTAssertEqual(scheduler.snapshot().tentativeBytes, 0)
    let beyond = try scheduler.reserve(advisoryByteCount: 1)
    XCTAssertThrowsError(try beyond.resize(toByteCount: 1)) { error in
      XCTAssertEqual(
        error as? SidebarImportByteSchedulerError,
        .capacityExceeded(limitBytes: 8))
    }
    beyond.discard()
  }

  func testByteSchedulerCapacityFailureDoesNotBlockLaterPermittedEntry() throws {
    let scheduler = SidebarImportByteScheduler(
      totalLimitBytes: 10,
      normalResidentLimitBytes: 100,
      cancellation: SidebarImportSourceCancellationToken())
    let first = try scheduler.reserve(advisoryByteCount: 8)
    let held = try first.makeReady(data: Data(repeating: 0x41, count: 8))

    let rejected = try scheduler.reserve(advisoryByteCount: 3)
    XCTAssertThrowsError(try rejected.resize(toByteCount: 3)) { error in
      XCTAssertEqual(
        error as? SidebarImportByteSchedulerError,
        .capacityExceeded(limitBytes: 10))
    }
    rejected.discard()
    let later = try scheduler.reserve(advisoryByteCount: 2)
    try later.resize(toByteCount: 2)
    later.discard()
    held.discard()
    XCTAssertEqual(scheduler.snapshot().tentativeBytes, 0)
    XCTAssertEqual(scheduler.snapshot().normalResidentBytes, 0)
  }

  func testByteSchedulerOverflowShapedReservationCannotBypassCapacity() throws {
    let scheduler = SidebarImportByteScheduler(
      totalLimitBytes: UInt64.max - 1,
      normalResidentLimitBytes: 8,
      cancellation: SidebarImportSourceCancellationToken())

    let reservation = try scheduler.reserve(advisoryByteCount: UInt64.max)
    XCTAssertThrowsError(try reservation.resize(toByteCount: UInt64.max)) { error in
      XCTAssertEqual(
        error as? SidebarImportByteSchedulerError,
        .capacityExceeded(limitBytes: UInt64.max - 1))
    }
    reservation.discard()
    XCTAssertEqual(scheduler.snapshot().tentativeBytes, 0)
  }

  func testByteSchedulerTwoReadyNormalsOverlapAndThirdWaitsForDiscard() throws {
    let scheduler = SidebarImportByteScheduler(
      totalLimitBytes: 100,
      normalResidentLimitBytes: 10,
      cancellation: SidebarImportSourceCancellationToken())
    let firstReservation = try scheduler.reserve(advisoryByteCount: 4)
    let first = try firstReservation.makeReady(data: Data(repeating: 0x41, count: 4))
    defer { first.discard() }
    let secondReservation = try scheduler.reserve(advisoryByteCount: 6)
    let second = try secondReservation.makeReady(data: Data(repeating: 0x42, count: 6))
    defer { second.discard() }
    let thirdResult = ResultBox<SidebarImportByteReservation>()
    let thirdFinished = expectation(description: "third normal admitted")

    DispatchQueue.global().async {
      thirdResult.store(Result { try scheduler.reserve(advisoryByteCount: 1) })
      thirdFinished.fulfill()
    }
    XCTAssertTrue(waitForScheduler { scheduler.snapshot().waitingRequests == 1 })
    XCTAssertNil(thirdResult.load())
    XCTAssertEqual(scheduler.snapshot().activeReadyPermits, 2)
    XCTAssertEqual(scheduler.snapshot().normalResidentBytes, 10)
    XCTAssertEqual(scheduler.snapshot().highWaterActivePermits, 2)

    first.discard()
    wait(for: [thirdFinished], timeout: 1)
    let third = try XCTUnwrap(thirdResult.load()).get()
    defer { third.discard() }
    try third.resize(toByteCount: 1)
    XCTAssertEqual(scheduler.snapshot().activeNormalPermits, 2)
  }

  func testByteSchedulerExactNormalBoundaryBackpressuresAnotherNormal() throws {
    let scheduler = SidebarImportByteScheduler(
      totalLimitBytes: 100,
      normalResidentLimitBytes: 8,
      cancellation: SidebarImportSourceCancellationToken())
    let boundaryReservation = try scheduler.reserve(advisoryByteCount: 8)
    let boundary = try boundaryReservation.makeReady(data: Data(repeating: 0x41, count: 8))
    defer { boundary.discard() }
    let nextResult = ResultBox<SidebarImportByteReservation>()
    let nextFinished = expectation(description: "normal after boundary admitted")

    DispatchQueue.global().async {
      nextResult.store(Result { try scheduler.reserve(advisoryByteCount: 1) })
      nextFinished.fulfill()
    }
    let didBackpressure = waitForScheduler { scheduler.snapshot().waitingRequests == 1 }
    XCTAssertTrue(didBackpressure)
    XCTAssertFalse(scheduler.snapshot().hasExclusivePermit)
    XCTAssertEqual(scheduler.snapshot().normalResidentBytes, 8)
    XCTAssertNil(nextResult.load())

    boundary.discard()
    wait(for: [nextFinished], timeout: 1)
    let next = try XCTUnwrap(nextResult.load()).get()
    next.discard()
  }

  func testByteSchedulerOversizedReadyPermitBlocksEveryOtherAdmission() throws {
    let scheduler = SidebarImportByteScheduler(
      totalLimitBytes: 100,
      normalResidentLimitBytes: 8,
      cancellation: SidebarImportSourceCancellationToken())
    let oversizedReservation = try scheduler.reserve(advisoryByteCount: 9)
    let oversized = try oversizedReservation.makeReady(data: Data(repeating: 0x41, count: 9))
    defer { oversized.discard() }
    let normalResult = ResultBox<SidebarImportByteReservation>()
    let normalFinished = expectation(description: "normal after oversized admitted")

    DispatchQueue.global().async {
      normalResult.store(Result { try scheduler.reserve(advisoryByteCount: 4) })
      normalFinished.fulfill()
    }
    XCTAssertTrue(waitForScheduler { scheduler.snapshot().waitingRequests == 1 })
    XCTAssertTrue(scheduler.snapshot().hasExclusivePermit)
    XCTAssertEqual(scheduler.snapshot().oversizedResidentBytes, 9)
    XCTAssertNil(normalResult.load())

    oversized.discard()
    wait(for: [normalFinished], timeout: 1)
    let normal = try XCTUnwrap(normalResult.load()).get()
    normal.discard()
  }

  func testByteSchedulerAuthoritativeOversizedShrinkStaysExclusiveUntilDiscard() throws {
    let scheduler = SidebarImportByteScheduler(
      totalLimitBytes: 100,
      normalResidentLimitBytes: 8,
      cancellation: SidebarImportSourceCancellationToken())
    let oversizedReservation = try scheduler.reserve(advisoryByteCount: 9)
    try oversizedReservation.resize(toByteCount: 9)
    try oversizedReservation.resize(toByteCount: 7)
    let oversized = try oversizedReservation.makeReady(
      data: Data(repeating: 0x41, count: 7))
    defer { oversized.discard() }
    let normalResult = ResultBox<SidebarImportByteReservation>()
    let normalFinished = expectation(description: "normal after shrunken oversized admitted")

    DispatchQueue.global().async {
      normalResult.store(Result { try scheduler.reserve(advisoryByteCount: 1) })
      normalFinished.fulfill()
    }
    XCTAssertTrue(waitForScheduler { scheduler.snapshot().waitingRequests == 1 })
    XCTAssertTrue(scheduler.snapshot().hasExclusivePermit)
    XCTAssertEqual(scheduler.snapshot().oversizedResidentBytes, 7)
    XCTAssertEqual(scheduler.snapshot().normalResidentBytes, 0)
    XCTAssertNil(normalResult.load())

    oversized.discard()
    wait(for: [normalFinished], timeout: 1)
    let normal = try XCTUnwrap(normalResult.load()).get()
    normal.discard()
  }

  func testByteSchedulerAuthoritativeShrinkAndStaleGrowthUpdateEveryLedger() throws {
    let scheduler = SidebarImportByteScheduler(
      totalLimitBytes: 100,
      normalResidentLimitBytes: 8,
      cancellation: SidebarImportSourceCancellationToken())
    let reservation = try scheduler.reserve(advisoryByteCount: 6)
    defer { reservation.discard() }

    try reservation.resize(toByteCount: 6)
    XCTAssertEqual(scheduler.snapshot().tentativeBytes, 6)
    XCTAssertEqual(scheduler.snapshot().normalResidentBytes, 6)
    XCTAssertEqual(scheduler.snapshot().normalPlannedBytes, 6)

    try reservation.resize(toByteCount: 3)
    XCTAssertEqual(scheduler.snapshot().tentativeBytes, 3)
    XCTAssertEqual(scheduler.snapshot().normalResidentBytes, 3)
    XCTAssertEqual(scheduler.snapshot().normalPlannedBytes, 3)

    try reservation.resize(toByteCount: 7)
    XCTAssertEqual(scheduler.snapshot().tentativeBytes, 7)
    XCTAssertEqual(scheduler.snapshot().normalResidentBytes, 7)
    XCTAssertEqual(scheduler.snapshot().normalPlannedBytes, 7)
    XCTAssertEqual(scheduler.snapshot().highWaterNormalResidentBytes, 7)

    let competing = try scheduler.reserve(advisoryByteCount: 1)
    defer { competing.discard() }
    XCTAssertThrowsError(try reservation.resize(toByteCount: 8)) { error in
      XCTAssertEqual(
        error as? SidebarImportByteSchedulerError,
        .capacityExceeded(limitBytes: 8))
    }
    XCTAssertEqual(scheduler.snapshot().normalResidentBytes, 7)
    XCTAssertEqual(scheduler.snapshot().normalPlannedBytes, 8)
  }

  func testReadyPermitRetainsSchedulerOwnerUntilDiscard() throws {
    weak var retainedScheduler: SidebarImportByteScheduler?
    var ready: SidebarImportReadyRead?

    do {
      let scheduler = SidebarImportByteScheduler(
        totalLimitBytes: 8,
        normalResidentLimitBytes: 8,
        cancellation: SidebarImportSourceCancellationToken())
      retainedScheduler = scheduler
      let reservation = try scheduler.reserve(advisoryByteCount: 4)
      ready = try reservation.makeReady(data: Data(repeating: 0x41, count: 4))
    }

    XCTAssertNotNil(retainedScheduler)
    ready?.discard()
    ready = nil
    XCTAssertNil(retainedScheduler)
  }

  func testByteSchedulerSoleNormalPermitPromotesToOversized() throws {
    let scheduler = SidebarImportByteScheduler(
      totalLimitBytes: 100,
      normalResidentLimitBytes: 8,
      cancellation: SidebarImportSourceCancellationToken())
    let reservation = try scheduler.reserve(advisoryByteCount: 7)
    defer { reservation.discard() }
    try reservation.resize(toByteCount: 7)

    try reservation.resize(toByteCount: 9)

    XCTAssertTrue(scheduler.snapshot().hasExclusivePermit)
    XCTAssertEqual(scheduler.snapshot().activeNormalPermits, 0)
    XCTAssertEqual(scheduler.snapshot().normalResidentBytes, 0)
    XCTAssertEqual(scheduler.snapshot().normalPlannedBytes, 0)
    XCTAssertEqual(scheduler.snapshot().oversizedResidentBytes, 9)
    XCTAssertEqual(scheduler.snapshot().tentativeBytes, 9)
  }

  func testByteSchedulerNormalWinnerMakesCompetingPromotionFailOnlyRequester() throws {
    let scheduler = SidebarImportByteScheduler(
      totalLimitBytes: 100,
      normalResidentLimitBytes: 8,
      cancellation: SidebarImportSourceCancellationToken())
    let growing = try scheduler.reserve(advisoryByteCount: 7)
    defer { growing.discard() }
    try growing.resize(toByteCount: 7)
    let competing = try scheduler.reserve(advisoryByteCount: 1)
    defer { competing.discard() }

    XCTAssertThrowsError(try growing.resize(toByteCount: 9)) { error in
      XCTAssertEqual(
        error as? SidebarImportByteSchedulerError,
        .promotionRequiresExclusiveAccess)
    }
    XCTAssertEqual(scheduler.snapshot().tentativeBytes, 7)
    XCTAssertEqual(scheduler.snapshot().normalResidentBytes, 7)
    XCTAssertEqual(scheduler.snapshot().normalPlannedBytes, 8)
    XCTAssertEqual(scheduler.snapshot().activeNormalPermits, 2)
    XCTAssertFalse(scheduler.snapshot().hasExclusivePermit)

    growing.discard()
    try competing.resize(toByteCount: 1)
    XCTAssertEqual(scheduler.snapshot().tentativeBytes, 1)
  }

  func testReservableReadCancellationBeforeAdmissionNeverOpensAReadLease() throws {
    let vault = try makeVault()
    let root = tempDirectory.appendingPathComponent("source.bin")
    try Data([0x41]).write(to: root)
    let cancellation = SidebarImportSourceCancellationToken()
    let scheduler = SidebarImportByteScheduler(
      totalLimitBytes: 8,
      normalResidentLimitBytes: 8,
      cancellation: cancellation)
    let metrics = SidebarImportSourceMetrics()
    let boundaries = BoundaryProbe()
    let scope = ScopeProbe()
    let prepared = try SidebarImportSourceWalker(
      limits: SidebarImportSourceLimits(sessionRefuseBytes: 8),
      scopeAccess: scope.access(),
      hooks: SidebarImportSourceWalkerHooks(didReachBoundary: { boundaries.record($0) }),
      cancellation: cancellation,
      metrics: metrics
    ).prepare(rootURL: root, vaultURL: vault)
    defer { prepared.close() }
    let entry = try XCTUnwrap(prepared.manifest.entries.first)
    let opensBeforeCancellation = boundaries.count { boundary in
      if case .beforeOpen = boundary { return true }
      return false
    }

    scheduler.cancel()
    scheduler.cancel()
    XCTAssertThrowsError(try prepared.readBytes(for: entry, scheduler: scheduler)) { error in
      XCTAssertEqual(error as? SidebarImportByteSchedulerError, .cancelled)
    }
    XCTAssertEqual(metrics.snapshot().activeReads, 0)
    XCTAssertEqual(
      boundaries.count { boundary in
        if case .beforeOpen = boundary { return true }
        return false
      },
      opensBeforeCancellation)
    XCTAssertEqual(scheduler.snapshot().tentativeBytes, 0)
    XCTAssertEqual(scheduler.snapshot().waitingRequests, 0)
  }

  func testByteSchedulerCancellationWakesAndRemovesFIFOAdmissionWaiter() throws {
    let cancellation = SidebarImportSourceCancellationToken()
    let scheduler = SidebarImportByteScheduler(
      totalLimitBytes: 100,
      normalResidentLimitBytes: 8,
      cancellation: cancellation)
    let heldReservation = try scheduler.reserve(advisoryByteCount: 8)
    let held = try heldReservation.makeReady(data: Data(repeating: 0x41, count: 8))
    defer { held.discard() }
    let waitingResult = ResultBox<SidebarImportByteReservation>()
    let waitingFinished = expectation(description: "cancelled waiter finished")

    DispatchQueue.global().async {
      waitingResult.store(Result { try scheduler.reserve(advisoryByteCount: 1) })
      waitingFinished.fulfill()
    }
    XCTAssertTrue(waitForScheduler { scheduler.snapshot().waitingRequests == 1 })
    scheduler.cancel()
    scheduler.cancel()
    wait(for: [waitingFinished], timeout: 1)

    XCTAssertThrowsError(try XCTUnwrap(waitingResult.load()).get()) { error in
      XCTAssertEqual(error as? SidebarImportByteSchedulerError, .cancelled)
    }
    XCTAssertEqual(scheduler.snapshot().waitingRequests, 0)
    held.discard()
    XCTAssertEqual(scheduler.snapshot().tentativeBytes, 0)
    XCTAssertEqual(scheduler.snapshot().normalResidentBytes, 0)
    XCTAssertEqual(scheduler.snapshot().normalPlannedBytes, 0)
  }

  func testReservableReadCancellationDuringChunkUnwindsLedgerDescriptorsAndScope() throws {
    let vault = try makeVault()
    let root = tempDirectory.appendingPathComponent("source.bin")
    try Data(repeating: 0x41, count: 70 * 1_024).write(to: root)
    let cancellation = SidebarImportSourceCancellationToken()
    let scheduler = SidebarImportByteScheduler(
      totalLimitBytes: 100 * 1_024,
      normalResidentLimitBytes: 100 * 1_024,
      cancellation: cancellation)
    let metrics = SidebarImportSourceMetrics()
    let scope = ScopeProbe()
    let prepared = try SidebarImportSourceWalker(
      limits: SidebarImportSourceLimits(sessionRefuseBytes: 100 * 1_024),
      scopeAccess: scope.access(),
      hooks: SidebarImportSourceWalkerHooks(didReachBoundary: { boundary in
        if case .afterRead(_, offset: 0, reachedEnd: false) = boundary {
          scheduler.cancel()
        }
      }),
      cancellation: cancellation,
      metrics: metrics
    ).prepare(rootURL: root, vaultURL: vault)
    let entry = try XCTUnwrap(prepared.manifest.entries.first)

    XCTAssertThrowsError(try prepared.readBytes(for: entry, scheduler: scheduler)) { error in
      guard case SidebarImportSourceWalkerError.cancelled = error else {
        XCTFail("expected shared cancellation, got \(error)")
        return
      }
    }
    XCTAssertEqual(scheduler.snapshot().tentativeBytes, 0)
    XCTAssertEqual(scheduler.snapshot().normalResidentBytes, 0)
    XCTAssertEqual(scheduler.snapshot().normalPlannedBytes, 0)
    XCTAssertEqual(metrics.snapshot().activeReads, 0)
    XCTAssertEqual(metrics.snapshot().currentOwnedDescriptors, 0)
    XCTAssertEqual(scope.counts().stops, 1)
  }

  func testReservableReadCancellationAfterReadyDisposesBytesAndLedgerOnce() throws {
    let vault = try makeVault()
    let root = tempDirectory.appendingPathComponent("source.bin")
    try Data([0x41, 0x42, 0x43, 0x44]).write(to: root)
    let cancellation = SidebarImportSourceCancellationToken()
    let scheduler = SidebarImportByteScheduler(
      totalLimitBytes: 8,
      normalResidentLimitBytes: 8,
      cancellation: cancellation)
    let metrics = SidebarImportSourceMetrics()
    let scope = ScopeProbe()
    let prepared = try SidebarImportSourceWalker(
      limits: SidebarImportSourceLimits(sessionRefuseBytes: 8),
      scopeAccess: scope.access(),
      cancellation: cancellation,
      metrics: metrics
    ).prepare(rootURL: root, vaultURL: vault)
    defer { prepared.close() }
    let entry = try XCTUnwrap(prepared.manifest.entries.first)
    let ready = try prepared.readBytes(for: entry, scheduler: scheduler)
    XCTAssertEqual(scheduler.snapshot().tentativeBytes, 4)

    scheduler.cancel()
    scheduler.cancel()
    ready.discard()
    ready.discard()

    XCTAssertEqual(scheduler.snapshot().tentativeBytes, 0)
    XCTAssertEqual(scheduler.snapshot().normalResidentBytes, 0)
    XCTAssertEqual(scheduler.snapshot().normalPlannedBytes, 0)
    XCTAssertEqual(scheduler.snapshot().activeReadyPermits, 0)
    prepared.close()
    XCTAssertEqual(metrics.snapshot().activeReads, 0)
    XCTAssertEqual(metrics.snapshot().currentOwnedDescriptors, 0)
    XCTAssertEqual(scope.counts().stops, 1)
  }
}
