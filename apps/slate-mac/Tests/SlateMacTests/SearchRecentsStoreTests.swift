// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// Round-trip tests for the per-vault search-recents store (#876).
/// Mirrors `FileRecentsStoreTests` (shared design) plus the `clear()`
/// affordance unique to this store (the privacy note, searching.md:38).
final class SearchRecentsStoreTests: XCTestCase {

    private var vaultRoot: URL!

    override func setUpWithError() throws {
        vaultRoot = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-search-recents-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: vaultRoot, withIntermediateDirectories: true)
    }

    override func tearDownWithError() throws {
        if FileManager.default.fileExists(atPath: vaultRoot.path) {
            try FileManager.default.removeItem(at: vaultRoot)
        }
    }

    private func makeStore() -> SearchRecentsStore {
        SearchRecentsStore(vaultRoot: vaultRoot)
    }

    // MARK: - Path shape

    func testFileURLIsUnderVaultDotSlate() {
        let store = makeStore()
        XCTAssertEqual(
            store.fileURL.path,
            vaultRoot.appendingPathComponent(".slate/search-recents.json").path,
            "recents live at <vault>/.slate/search-recents.json, beside workspace.json")
    }

    // MARK: - Load

    func testLoadOnMissingFileReturnsEmpty() {
        XCTAssertEqual(makeStore().load(), [])
    }

    func testLoadOnMalformedJSONReturnsEmpty() throws {
        try FileManager.default.createDirectory(
            at: vaultRoot.appendingPathComponent(".slate"), withIntermediateDirectories: true)
        try "not json".write(to: makeStore().fileURL, atomically: true, encoding: .utf8)
        XCTAssertEqual(makeStore().load(), [], "malformed JSON degrades to empty")
    }

    func testLoadDedupesExternallyEditedDuplicates() throws {
        let store = makeStore()
        try FileManager.default.createDirectory(
            at: store.fileURL.deletingLastPathComponent(), withIntermediateDirectories: true)
        let dupes = ["budget", "meeting", "budget", "recipe", "meeting"]
        try JSONEncoder().encode(dupes).write(to: store.fileURL)
        XCTAssertEqual(
            store.load(), ["budget", "meeting", "recipe"],
            "dedupe preserves first-seen (most-recent-first) order")
    }

    func testLoadCapsExternallyOversizedFile() throws {
        let store = makeStore()
        try FileManager.default.createDirectory(
            at: store.fileURL.deletingLastPathComponent(), withIntermediateDirectories: true)
        let oversized = (0..<(SearchRecentsStore.maxEntries + 10)).map { "query\($0)" }
        try JSONEncoder().encode(oversized).write(to: store.fileURL)
        let loaded = store.load()
        XCTAssertEqual(loaded.count, SearchRecentsStore.maxEntries)
        XCTAssertEqual(loaded.first, "query0", "the first maxEntries unique queries survive")
    }

    /// A file strictly larger than `maxFileBytes` is refused before the
    /// decode, even when its contents are valid JSON.
    func testLoadRefusesFileLargerThanThreshold() throws {
        let store = makeStore()
        try FileManager.default.createDirectory(
            at: store.fileURL.deletingLastPathComponent(), withIntermediateDirectories: true)
        let prefix = "[\"a\""  // valid-JSON opener
        let suffix = "]"
        let padCount = (SearchRecentsStore.maxFileBytes + 1) - prefix.utf8.count - suffix.utf8.count
        let json = prefix + String(repeating: " ", count: padCount) + suffix
        try json.data(using: .utf8)!.write(to: store.fileURL)
        XCTAssertEqual(
            try JSONDecoder().decode([String].self, from: json.data(using: .utf8)!), ["a"])
        XCTAssertEqual(
            store.load(), [],
            "a file one byte over maxFileBytes is refused before decode")
    }

    // MARK: - Save round-trip

    func testSaveAndLoadRoundTrip() throws {
        let store = makeStore()
        let queries = ["budget 2026", "meeting notes", "recipe"]
        try store.save(queries)
        XCTAssertEqual(store.load(), queries)
    }

    func testSaveCreatesDotSlateDirectory() throws {
        let store = makeStore()
        XCTAssertFalse(
            FileManager.default.fileExists(
                atPath: vaultRoot.appendingPathComponent(".slate").path))
        try store.save(["budget"])
        XCTAssertTrue(FileManager.default.fileExists(atPath: store.fileURL.path))
    }

    // MARK: - Add (LRU)

    func testAddInsertsAtFront() throws {
        let store = makeStore()
        _ = try store.add("alpha")
        _ = try store.add("beta")
        let result = try store.add("gamma")
        XCTAssertEqual(result, ["gamma", "beta", "alpha"], "most-recent-first")
        XCTAssertEqual(store.load(), ["gamma", "beta", "alpha"])
    }

    func testAddMovesExistingEntryToFront() throws {
        let store = makeStore()
        _ = try store.add("alpha")
        _ = try store.add("beta")
        let result = try store.add("alpha")
        XCTAssertEqual(result, ["alpha", "beta"], "re-adding moves to front, not duplicates")
    }

    func testAddCapsAtMaxEntries() throws {
        let store = makeStore()
        for i in 0..<(SearchRecentsStore.maxEntries + 5) {
            _ = try store.add("query\(i)")
        }
        let loaded = store.load()
        XCTAssertEqual(loaded.count, SearchRecentsStore.maxEntries)
        XCTAssertEqual(loaded.first, "query\(SearchRecentsStore.maxEntries + 4)")
    }

    // MARK: - Clear (the privacy affordance, searching.md:38)

    func testClearEmptiesTheList() throws {
        let store = makeStore()
        _ = try store.add("alpha")
        _ = try store.add("beta")
        XCTAssertFalse(store.load().isEmpty)
        try store.clear()
        XCTAssertEqual(store.load(), [], "clear forgets every remembered query")
    }

    func testClearOnEmptyStoreIsSafe() throws {
        try makeStore().clear()
        XCTAssertEqual(makeStore().load(), [])
    }

    // MARK: - Cross-instance persistence (app restart)

    func testRecentsSurviveAcrossNewStoreInstance() throws {
        _ = try makeStore().add("alpha")
        _ = try makeStore().add("beta")
        XCTAssertEqual(makeStore().load(), ["beta", "alpha"])
    }
}
