// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Combine
import Dispatch
import Foundation
import XCTest

@testable import SlateMac

@MainActor
final class SidebarMetadataRefreshTests: XCTestCase {
  private final class FetchCounter: @unchecked Sendable {
    private let lock = NSLock()
    private var storage = 0
    private var observedMainThread = false

    var value: Int {
      lock.lock()
      defer { lock.unlock() }
      return storage
    }

    var ranOnMainThread: Bool {
      lock.lock()
      defer { lock.unlock() }
      return observedMainThread
    }

    func increment() {
      lock.lock()
      storage += 1
      observedMainThread = observedMainThread || Thread.isMainThread
      lock.unlock()
    }
  }

  private final class BlockingSummaryFetcher: @unchecked Sendable {
    private let lock = NSLock()
    private let releaseGate = DispatchSemaphore(value: 0)
    private var enteredStorage = false
    private var ranOnMainThreadStorage = false
    private var releasedStorage = false
    private let result: FileSummary

    init(result: FileSummary) {
      self.result = result
    }

    var entered: Bool {
      lock.lock()
      defer { lock.unlock() }
      return enteredStorage
    }

    var ranOnMainThread: Bool {
      lock.lock()
      defer { lock.unlock() }
      return ranOnMainThreadStorage
    }

    func fetch() -> FileSummary? {
      lock.lock()
      enteredStorage = true
      ranOnMainThreadStorage = Thread.isMainThread
      lock.unlock()
      releaseGate.wait()
      return result
    }

    func release() {
      lock.lock()
      guard !releasedStorage else {
        lock.unlock()
        return
      }
      releasedStorage = true
      lock.unlock()
      releaseGate.signal()
    }
  }

  private var root: URL!

  override func setUpWithError() throws {
    try super.setUpWithError()
    root = FileManager.default.temporaryDirectory
      .appendingPathComponent("slate-sidebar-refresh-\(UUID().uuidString)")
    try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
  }

  override func tearDownWithError() throws {
    try? FileManager.default.removeItem(at: root)
    try super.tearDownWithError()
  }

  func testModifiedEventsCoalesceToOneOffMainTargetedFetchAndPublishLatestSummary() async throws {
    let vault = try makeVault(named: "a")
    let state = makeState()
    defer { state.closeVault() }
    state.openVault(at: vault)
    await state.scanTask?.value
    await state.templateAvailabilityRefreshTaskForTesting?.value
    await state.templateAvailabilityTask?.value
    let session = try XCTUnwrap(state.currentSession)
    let refreshed = summary(
      displayName: "Fresh title",
      mtime: 2,
      wordCount: 42,
      preview: "Fresh preview",
      taskTotal: 3,
      taskOpen: 1)
    let counter = FetchCounter()
    state.sidebarFileSummaryFetcher = { _, path in
      counter.increment()
      return path == refreshed.path ? refreshed : nil
    }
    var appStateInvalidations = 0
    let observation = state.objectWillChange.sink {
      appStateInvalidations += 1
    }
    var deliveredBatches: [[FileSummary]] = []
    var deliveryWasOnMainThread = false
    let deliveryObservation = state.sidebarFileSummaryUpdates.sink { summaries in
      deliveredBatches.append(summaries)
      deliveryWasOnMainThread = Thread.isMainThread
    }

    let event = FileChangeEvent(kind: .modified, path: refreshed.path, previousPath: nil)
    state.handleSidebarFileChange(event, from: session)
    state.handleSidebarFileChange(event, from: session)
    await state.sidebarMetadataRefreshTaskForTesting?.value

    XCTAssertEqual(counter.value, 1, "the same path in one burst is fetched once")
    XCTAssertFalse(counter.ranOnMainThread, "the synchronous FFI lookup must stay off-main")
    XCTAssertEqual(deliveredBatches, [[refreshed]])
    XCTAssertTrue(deliveryWasOnMainThread, "targeted row delivery must return to the main actor")
    XCTAssertEqual(state.sidebarFileSummaryUpdate?.summaries, [refreshed])
    XCTAssertEqual(
      appStateInvalidations,
      0,
      "targeted row delivery must not invalidate every AppState-observing surface")
    withExtendedLifetime(observation) {}
    withExtendedLifetime(deliveryObservation) {}
  }

  func testOldSessionEventIsDroppedBeforeItCanFetchOrPublish() async throws {
    let vaultA = try makeVault(named: "a")
    let vaultB = try makeVault(named: "b")
    let state = makeState()
    defer { state.closeVault() }
    state.openVault(at: vaultA)
    await state.scanTask?.value
    let oldSession = try XCTUnwrap(state.currentSession)

    state.openVault(at: vaultB)
    await state.scanTask?.value
    let counter = FetchCounter()
    state.sidebarFileSummaryFetcher = { _, _ in
      counter.increment()
      return nil
    }

    state.handleSidebarFileChange(
      FileChangeEvent(kind: .modified, path: "note.md", previousPath: nil),
      from: oldSession)

    XCTAssertEqual(counter.value, 0)
    XCTAssertNil(state.sidebarMetadataRefreshTaskForTesting)
    XCTAssertNil(state.sidebarFileSummaryUpdate)
  }

  func testDistinctModifiedPathsPublishOneCompleteBatchThroughTreeUpdateSeam() async throws {
    let vault = try makeVault(named: "batch")
    try "Second old body\n".write(
      to: vault.appendingPathComponent("second.md"),
      atomically: true,
      encoding: .utf8)
    let state = makeState()
    defer { state.closeVault() }
    state.openVault(at: vault)
    await state.scanTask?.value
    let session = try XCTUnwrap(state.currentSession)
    let original = state.files.filter { ["note.md", "second.md"].contains($0.path) }
    XCTAssertEqual(original.count, 2)

    let tree = FileTreeViewModel()
    tree.bindForTesting { parentPath in
      XCTAssertEqual(parentPath, "")
      return DirListing(
        dirs: [],
        files: FileSummaryPage(
          items: original,
          nextCursor: nil,
          totalFiltered: UInt64(original.count)))
    }

    let first = summary(
      path: "note.md", displayName: "First fresh", mtime: 2,
      wordCount: 10, preview: "First preview", taskTotal: 1, taskOpen: 0)
    let second = summary(
      path: "second.md", displayName: "Second fresh", mtime: 3,
      wordCount: 20, preview: "Second preview", taskTotal: 2, taskOpen: 1)
    let counter = FetchCounter()
    state.sidebarFileSummaryFetcher = { _, path in
      counter.increment()
      return [first.path: first, second.path: second][path]
    }

    state.handleSidebarFileChange(
      FileChangeEvent(kind: .modified, path: first.path, previousPath: nil),
      from: session)
    state.handleSidebarFileChange(
      FileChangeEvent(kind: .modified, path: second.path, previousPath: nil),
      from: session)
    await state.sidebarMetadataRefreshTaskForTesting?.value

    let update = try XCTUnwrap(state.sidebarFileSummaryUpdate)
    XCTAssertEqual(update.token, 1, "the burst is delivered through one SwiftUI edge")
    XCTAssertEqual(update.summaries, [first, second])
    XCTAssertEqual(counter.value, 2, "each distinct path is fetched exactly once")
    XCTAssertEqual(tree.replaceFileSummaries(update.summaries), 2)
    XCTAssertEqual(tree.fileSummary(forPath: first.path), first)
    XCTAssertEqual(tree.fileSummary(forPath: second.path), second)
  }

  func testVaultSwitchWhileMetadataFetchIsInFlightDropsOldBatch() async throws {
    let vaultA = try makeVault(named: "a")
    let vaultB = try makeVault(named: "b")
    let state = makeState()
    defer { state.closeVault() }
    state.openVault(at: vaultA)
    await state.scanTask?.value
    let sessionA = try XCTUnwrap(state.currentSession)
    let stale = summary(
      displayName: "Stale vault A title",
      mtime: 2,
      wordCount: 42,
      preview: "Must never reach vault B",
      taskTotal: 1,
      taskOpen: 1)
    let gate = BlockingSummaryFetcher(result: stale)
    defer { gate.release() }
    state.sidebarFileSummaryFetcher = { _, _ in gate.fetch() }
    var deliveredBatches: [[FileSummary]] = []
    let deliveryObservation = state.sidebarFileSummaryUpdates.sink { summaries in
      deliveredBatches.append(summaries)
    }

    state.handleSidebarFileChange(
      FileChangeEvent(kind: .modified, path: stale.path, previousPath: nil),
      from: sessionA)
    let oldTask = try XCTUnwrap(state.sidebarMetadataRefreshTaskForTesting)

    for _ in 0..<200 where !gate.entered {
      try await Task.sleep(for: .milliseconds(5))
    }
    XCTAssertTrue(gate.entered, "the old-vault fetch must be in flight before switching")

    state.openVault(at: vaultB)
    await state.scanTask?.value
    let sessionB = try XCTUnwrap(state.currentSession)
    gate.release()
    await oldTask.value

    XCTAssertFalse(gate.ranOnMainThread, "the blocking lookup must execute off-main")
    XCTAssertTrue(deliveredBatches.isEmpty, "vault A metadata must never publish into vault B")
    XCTAssertNil(state.sidebarFileSummaryUpdate)
    XCTAssertTrue(state.sidebarMetadataPendingPaths.isEmpty)
    XCTAssertTrue(state.currentSession === sessionB)
    withExtendedLifetime(deliveryObservation) {}
  }

  private func makeVault(named name: String) throws -> URL {
    let vault = root.appendingPathComponent(name, isDirectory: true)
    try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
    try "Old body\n".write(
      to: vault.appendingPathComponent("note.md"),
      atomically: true,
      encoding: .utf8)
    return vault
  }

  private func makeState() -> AppState {
    AppState(
      recentsStore: RecentVaultsStore(
        fileURL: root.appendingPathComponent("recents-\(UUID().uuidString).json")),
      externalOpener: { _ in true })
  }

  private func summary(
    path: String = "note.md",
    displayName: String?,
    mtime: Int64,
    wordCount: UInt32?,
    preview: String?,
    taskTotal: UInt32,
    taskOpen: UInt32
  ) -> FileSummary {
    FileSummary(
      path: path,
      name: (path as NSString).lastPathComponent,
      mtimeMs: mtime,
      sizeBytes: 99,
      isMarkdown: true,
      displayName: displayName,
      createdDate: nil,
      createdMs: nil,
      wordCount: wordCount,
      preview: preview,
      taskTotal: taskTotal,
      taskOpen: taskOpen)
  }
}
