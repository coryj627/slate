// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Combine
import Foundation
import XCTest

@testable import SlateMac

@MainActor
final class SidebarMetadataRefreshTests: XCTestCase {
  private final class FetchCounter: @unchecked Sendable {
    private let lock = NSLock()
    private var storage = 0

    var value: Int {
      lock.lock()
      defer { lock.unlock() }
      return storage
    }

    func increment() {
      lock.lock()
      storage += 1
      lock.unlock()
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

    let event = FileChangeEvent(kind: .modified, path: refreshed.path, previousPath: nil)
    state.handleSidebarFileChange(event, from: session)
    state.handleSidebarFileChange(event, from: session)
    await state.sidebarMetadataRefreshTaskForTesting?.value

    XCTAssertEqual(counter.value, 1, "the same path in one burst is fetched once")
    XCTAssertEqual(state.sidebarFileSummaryUpdate?.summaries, [refreshed])
    XCTAssertEqual(
      appStateInvalidations,
      0,
      "targeted row delivery must not invalidate every AppState-observing surface")
    withExtendedLifetime(observation) {}
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
