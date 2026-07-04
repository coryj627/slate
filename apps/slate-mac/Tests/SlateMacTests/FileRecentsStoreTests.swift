// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// Round-trip tests for the per-vault file-recents store (#495).
/// Mirrors `CommandPaletteRecentsStoreTests` (shared design) plus the
/// per-vault path-shape assertions unique to this store.
final class FileRecentsStoreTests: XCTestCase {

    private var vaultRoot: URL!

    override func setUpWithError() throws {
        vaultRoot = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-file-recents-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: vaultRoot, withIntermediateDirectories: true)
    }

    override func tearDownWithError() throws {
        if FileManager.default.fileExists(atPath: vaultRoot.path) {
            try FileManager.default.removeItem(at: vaultRoot)
        }
    }

    private func makeStore() -> FileRecentsStore {
        FileRecentsStore(vaultRoot: vaultRoot)
    }

    // MARK: - Path shape

    func testFileURLIsUnderVaultDotSlate() {
        let store = makeStore()
        XCTAssertEqual(
            store.fileURL.path,
            vaultRoot.appendingPathComponent(".slate/file-recents.json").path,
            "recents live at <vault>/.slate/file-recents.json, beside workspace.json")
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
        let dupes = ["a.md", "b.md", "a.md", "c.md", "b.md"]
        try JSONEncoder().encode(dupes).write(to: store.fileURL)
        XCTAssertEqual(
            store.load(), ["a.md", "b.md", "c.md"], "dedupe preserves first-seen order")
    }

    func testLoadCapsExternallyOversizedFile() throws {
        let store = makeStore()
        try FileManager.default.createDirectory(
            at: store.fileURL.deletingLastPathComponent(), withIntermediateDirectories: true)
        let oversized = (0..<(FileRecentsStore.maxEntries + 10)).map { "note\($0).md" }
        try JSONEncoder().encode(oversized).write(to: store.fileURL)
        let loaded = store.load()
        XCTAssertEqual(loaded.count, FileRecentsStore.maxEntries)
        XCTAssertEqual(loaded.first, "note0.md", "the first maxEntries unique ids survive")
    }

    /// A file strictly larger than `maxFileBytes` is refused before the
    /// decode, even when its contents are valid JSON.
    func testLoadRefusesFileLargerThanThreshold() throws {
        let store = makeStore()
        try FileManager.default.createDirectory(
            at: store.fileURL.deletingLastPathComponent(), withIntermediateDirectories: true)
        let prefix = "[\"a.md\""  // valid-JSON opener
        let suffix = "]"
        let padCount = (FileRecentsStore.maxFileBytes + 1) - prefix.utf8.count - suffix.utf8.count
        let json = prefix + String(repeating: " ", count: padCount) + suffix
        try json.data(using: .utf8)!.write(to: store.fileURL)
        // Sanity: the fixture IS valid JSON, so only the size guard can
        // reject it.
        XCTAssertEqual(
            try JSONDecoder().decode([String].self, from: json.data(using: .utf8)!), ["a.md"])
        XCTAssertEqual(
            store.load(), [],
            "a file one byte over maxFileBytes is refused before decode")
    }

    // MARK: - Save round-trip

    func testSaveAndLoadRoundTrip() throws {
        let store = makeStore()
        let paths = ["notes/a.md", "b.md", "sub/c.md"]
        try store.save(paths)
        XCTAssertEqual(store.load(), paths)
    }

    func testSaveCreatesDotSlateDirectory() throws {
        let store = makeStore()
        XCTAssertFalse(
            FileManager.default.fileExists(
                atPath: vaultRoot.appendingPathComponent(".slate").path))
        try store.save(["a.md"])
        XCTAssertTrue(FileManager.default.fileExists(atPath: store.fileURL.path))
    }

    // MARK: - Add (LRU)

    func testAddInsertsAtFront() throws {
        let store = makeStore()
        _ = try store.add("a.md")
        _ = try store.add("b.md")
        let result = try store.add("c.md")
        XCTAssertEqual(result, ["c.md", "b.md", "a.md"])
        XCTAssertEqual(store.load(), ["c.md", "b.md", "a.md"])
    }

    func testAddMovesExistingEntryToFront() throws {
        let store = makeStore()
        _ = try store.add("a.md")
        _ = try store.add("b.md")
        let result = try store.add("a.md")
        XCTAssertEqual(result, ["a.md", "b.md"], "re-adding moves to front, not duplicates")
    }

    func testAddCapsAtMaxEntries() throws {
        let store = makeStore()
        for i in 0..<(FileRecentsStore.maxEntries + 5) {
            _ = try store.add("note\(i).md")
        }
        let loaded = store.load()
        XCTAssertEqual(loaded.count, FileRecentsStore.maxEntries)
        XCTAssertEqual(loaded.first, "note\(FileRecentsStore.maxEntries + 4).md")
    }

    // MARK: - Cross-instance persistence (app restart)

    func testRecentsSurviveAcrossNewStoreInstance() throws {
        _ = try makeStore().add("a.md")
        _ = try makeStore().add("b.md")
        XCTAssertEqual(makeStore().load(), ["b.md", "a.md"])
    }
}
