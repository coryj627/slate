// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation
import XCTest

@testable import SlateMac

final class SidebarImportCoordinatorTests: XCTestCase {
  private var tempDirectory: URL!

  override func setUpWithError() throws {
    tempDirectory = FileManager.default.temporaryDirectory
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
    private(set) var starts = 0
    private(set) var stops = 0

    func access() -> SidebarImportSecurityScopeAccess {
      SidebarImportSecurityScopeAccess(
        start: { [weak self] _ in
          self?.lock.lock()
          self?.starts += 1
          self?.lock.unlock()
          return true
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
}
