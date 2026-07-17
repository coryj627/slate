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

  func testPublicProviderIntakeLoadsOnceAndPreservesProviderOrder() async {
    let first = ProviderStub()
    let second = ProviderStub()
    let intake = SidebarImportProviderIntake(providers: [first, second])
    let firstURL = URL(fileURLWithPath: "/tmp/first.md")
    let secondURL = URL(fileURLWithPath: "/tmp/second.md")

    let resultTask = Task { await intake.load() }
    while first.loadCount == 0 || second.loadCount == 0 {
      await Task.yield()
    }
    second.complete(with: secondURL)
    first.complete(with: firstURL)

    let result = await resultTask.value
    XCTAssertEqual(
      result,
      [
        SidebarImportProviderSlot(providerIndex: 0, outcome: .url(firstURL)),
        SidebarImportProviderSlot(providerIndex: 1, outcome: .url(secondURL)),
      ])
    XCTAssertEqual(first.loadCount, 1)
    XCTAssertEqual(second.loadCount, 1)
  }

  func testPublicProviderCancellationTerminalizesNeverCallbackAndIgnoresLateCallbacks() async {
    let provider = ProviderStub()
    let intake = SidebarImportProviderIntake(providers: [provider])
    let returned = expectation(description: "aggregate returned without provider callback")
    returned.expectedFulfillmentCount = 1
    let slots = SlotBox()
    Task {
      slots.store(await intake.load())
      returned.fulfill()
    }
    while provider.loadsStarted() == 0 {
      await Task.yield()
    }

    intake.cancel()
    await fulfillment(of: [returned], timeout: 0.2)
    provider.complete(with: URL(fileURLWithPath: "/tmp/late.md"))
    provider.complete(with: URL(fileURLWithPath: "/tmp/duplicate.md"))
    await Task.yield()

    XCTAssertEqual(
      slots.load(),
      [SidebarImportProviderSlot(providerIndex: 0, outcome: .failure(.cancelled))])
    XCTAssertTrue(provider.progress.isCancelled)
    XCTAssertEqual(provider.loadsStarted(), 1)
  }

  func testPublicProviderIntakeSkipsUnsupportedProvidersAndKeepsOriginalIndices() async {
    let unsupported = ProviderStub(registeredTypeIdentifiers: ["public.text"])
    let supported = ProviderStub()
    let intake = SidebarImportProviderIntake(providers: [unsupported, supported])
    let supportedURL = URL(fileURLWithPath: "/tmp/supported.md")

    let resultTask = Task { await intake.load() }
    while supported.loadsStarted() == 0 {
      await Task.yield()
    }
    supported.complete(with: supportedURL)
    unsupported.complete(with: URL(fileURLWithPath: "/tmp/unsupported.md"))

    let result = await resultTask.value
    XCTAssertEqual(
      result,
      [SidebarImportProviderSlot(providerIndex: 1, outcome: .url(supportedURL))])
    XCTAssertEqual(unsupported.loadsStarted(), 0)
    XCTAssertEqual(supported.loadsStarted(), 1)
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
}
