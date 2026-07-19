// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Darwin
import Foundation
import XCTest

@testable import SlateMac

/// FL3-3 (#660): ONE device-local recents history per vault, stored as
/// bounded UserDefaults data keyed by physical vault identity, with a
/// one-time migration from the legacy `.slate/file-recents.json`.
final class FileRecentsStoreTests: XCTestCase {
  private var vault: URL!
  private var suite: String!
  private var defaults: UserDefaults!

  override func setUpWithError() throws {
    try super.setUpWithError()
    vault = FileManager.default.temporaryDirectory
      .appendingPathComponent("slate-recents-\(UUID().uuidString)", isDirectory: true)
    try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
    suite = "slate.recents-tests.\(UUID().uuidString)"
    defaults = UserDefaults(suiteName: suite)
  }

  override func tearDownWithError() throws {
    defaults.removePersistentDomain(forName: suite)
    try? FileManager.default.removeItem(at: vault)
    try super.tearDownWithError()
  }

  private func identity() -> SidebarVaultPrefsStore.RootIdentity {
    var info = stat()
    XCTAssertEqual(vault.path.withCString { stat($0, &info) }, 0)
    return .init(device: UInt64(info.st_dev), inode: UInt64(info.st_ino))
  }

  private func makeStore(
    identity explicit: SidebarVaultPrefsStore.RootIdentity? = nil
  ) -> FileRecentsStore {
    FileRecentsStore(
      vaultRoot: vault, identity: explicit ?? identity(), defaults: defaults)
  }

  private func writeLegacy(_ json: String) throws {
    let slate = vault.appendingPathComponent(".slate")
    try FileManager.default.createDirectory(at: slate, withIntermediateDirectories: true)
    try json.write(
      to: slate.appendingPathComponent("file-recents.json"),
      atomically: true, encoding: .utf8)
  }

  func testDefaultsKeyUsesPhysicalIdentityWithPathFallback() {
    let anchored = makeStore()
    XCTAssertTrue(anchored.defaultsKey.hasPrefix("slate.fileRecents.v2."))
    XCTAssertFalse(anchored.defaultsKey.contains("path."))
    let fallback = FileRecentsStore(
      vaultRoot: vault, identity: nil, defaults: defaults)
    XCTAssertTrue(fallback.defaultsKey.contains(".path."))
  }

  func testLoadOnFreshVaultIsEmptyAndAddIsLRUFrontWithCap() {
    let store = makeStore()
    XCTAssertEqual(store.load(), [])
    store.add("a.md")
    store.add("b.md")
    store.add("a.md")
    XCTAssertEqual(store.load(), ["a.md", "b.md"])
    for index in 0..<60 {
      store.add("bulk-\(index).md")
    }
    let entries = store.load()
    XCTAssertEqual(entries.count, FileRecentsStore.maxEntries)
    XCTAssertEqual(entries.first, "bulk-59.md")
  }

  func testSaveDedupesCapsAndDropsOversizedEntries() {
    let store = makeStore()
    let long = String(repeating: "a", count: 4_097)
    store.save(["x.md", "x.md", long, "y.md"])
    XCTAssertEqual(store.load(), ["x.md", "y.md"])
  }

  func testClearEmptiesTheSharedHistoryWithoutResurrectingLegacy() throws {
    try writeLegacy(#"["old.md"]"#)
    let store = makeStore()
    XCTAssertEqual(store.load(), ["old.md"], "migration merges the legacy file")
    store.clear()
    XCTAssertEqual(store.load(), [])
    // A legacy file that reappears AFTER migration is never re-read.
    try writeLegacy(#"["ghost.md"]"#)
    XCTAssertEqual(store.load(), [])
  }

  func testMigrationMovesLegacyOnceAndRetiresTheFile() throws {
    try writeLegacy(#"["one.md", "two.md", "one.md"]"#)
    let store = makeStore()
    XCTAssertEqual(store.load(), ["one.md", "two.md"])
    XCTAssertFalse(
      FileManager.default.fileExists(atPath: store.legacyFileURL.path),
      "the legacy source retires after the durable defaults write")
    XCTAssertEqual(store.load(), ["one.md", "two.md"], "idempotent")
  }

  func testMalformedAndOversizedLegacyMigrateAsEmptyAndRetire() throws {
    try writeLegacy("{not json")
    let store = makeStore()
    XCTAssertEqual(store.load(), [])
    XCTAssertFalse(FileManager.default.fileExists(atPath: store.legacyFileURL.path))

    // Oversized variant on a second identity slot.
    let other = FileRecentsStore(
      vaultRoot: vault,
      identity: .init(device: 0xF00D, inode: 0xBEEF),
      defaults: defaults)
    try writeLegacy(
      "[\"" + String(repeating: "x", count: FileRecentsStore.maxFileBytes) + "\"]")
    XCTAssertEqual(other.load(), [])
    XCTAssertFalse(FileManager.default.fileExists(atPath: other.legacyFileURL.path))
  }

  func testDistinctIdentitiesKeepIndependentHistories() {
    let a = makeStore(identity: .init(device: 1, inode: 1))
    let b = makeStore(identity: .init(device: 1, inode: 2))
    a.add("only-a.md")
    XCTAssertEqual(a.load(), ["only-a.md"])
    XCTAssertEqual(b.load(), [])
  }

  func testHistorySurvivesANewStoreInstance() {
    makeStore().add("kept.md")
    XCTAssertEqual(makeStore().load(), ["kept.md"])
  }
}
