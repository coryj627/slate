// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// Round-trip tests for the Milestone Q recents store (#316).
/// Mirrors the shape of `RecentVaultsStoreTests` since the two
/// stores share a design.
final class CommandPaletteRecentsStoreTests: XCTestCase {

    private var tempDir: URL!
    private var fileURL: URL!

    override func setUpWithError() throws {
        tempDir = FileManager.default.temporaryDirectory.appendingPathComponent(
            "slate-recents-test-\(UUID().uuidString)"
        )
        try FileManager.default.createDirectory(at: tempDir, withIntermediateDirectories: true)
        fileURL = tempDir.appendingPathComponent("command-palette-recents.json")
    }

    override func tearDownWithError() throws {
        if FileManager.default.fileExists(atPath: tempDir.path) {
            try FileManager.default.removeItem(at: tempDir)
        }
    }

    private func makeStore() -> CommandPaletteRecentsStore {
        CommandPaletteRecentsStore(fileURL: fileURL)
    }

    // MARK: - Load

    func testLoadOnMissingFileReturnsEmpty() {
        XCTAssertEqual(makeStore().load(), [])
    }

    func testLoadOnMalformedJSONReturnsEmpty() throws {
        try "this is not json".write(to: fileURL, atomically: true, encoding: .utf8)
        XCTAssertEqual(makeStore().load(), [], "malformed JSON must not crash; treat as empty")
    }

    /// Hand-edited file containing duplicate ids must surface as a
    /// deduped list — surfacing both would render Command twice in
    /// Recent and let arrow nav cycle the same id twice. Codoki
    /// follow-up from #326 review.
    func testLoadDedupesExternallyEditedDuplicates() throws {
        let withDupes = ["a", "b", "a", "c", "b", "a"]
        let data = try JSONEncoder().encode(withDupes)
        try data.write(to: fileURL)
        XCTAssertEqual(
            makeStore().load(),
            ["a", "b", "c"],
            "dedupe preserves first-seen order"
        )
    }

    func testLoadCapsExternallyOversizedFile() throws {
        // Simulate someone editing the file by hand and saving 20
        // entries. Loader must hard-cap to maxEntries.
        let oversized = (0..<20).map { "slate.test.\($0)" }
        let data = try JSONEncoder().encode(oversized)
        try data.write(to: fileURL)
        let loaded = makeStore().load()
        XCTAssertEqual(loaded.count, CommandPaletteRecentsStore.maxEntries)
        XCTAssertEqual(loaded.first, "slate.test.0")
    }

    // MARK: - Save round-trip

    func testSaveAndLoadRoundTrip() throws {
        let store = makeStore()
        let ids = ["slate.file.openVault", "slate.editor.save", "slate.tasks.review"]
        try store.save(ids)
        XCTAssertEqual(store.load(), ids)
    }

    func testSaveCreatesMissingParentDirectory() throws {
        try FileManager.default.removeItem(at: tempDir)
        let store = makeStore()
        try store.save(["slate.editor.save"])
        XCTAssertTrue(FileManager.default.fileExists(atPath: fileURL.path))
    }

    // MARK: - Add (LRU)

    func testAddInsertsAtFront() throws {
        let store = makeStore()
        _ = try store.add("a")
        _ = try store.add("b")
        let result = try store.add("c")
        XCTAssertEqual(result, ["c", "b", "a"])
        XCTAssertEqual(store.load(), ["c", "b", "a"])
    }

    func testAddMovesExistingEntryToFront() throws {
        let store = makeStore()
        _ = try store.add("a")
        _ = try store.add("b")
        _ = try store.add("c")
        let result = try store.add("a")
        XCTAssertEqual(result, ["a", "c", "b"], "re-adding 'a' moves it to the front")
    }

    func testAddCapsAtMaxEntries() throws {
        let store = makeStore()
        for i in 0..<(CommandPaletteRecentsStore.maxEntries + 5) {
            _ = try store.add("slate.test.\(i)")
        }
        let loaded = store.load()
        XCTAssertEqual(loaded.count, CommandPaletteRecentsStore.maxEntries)
        // Latest add ('maxEntries + 4') sits at the front.
        XCTAssertEqual(loaded.first, "slate.test.\(CommandPaletteRecentsStore.maxEntries + 4)")
    }

    // MARK: - Remove

    func testRemoveDropsMatchingEntry() throws {
        let store = makeStore()
        _ = try store.add("a")
        _ = try store.add("b")
        let result = try store.remove("a")
        XCTAssertEqual(result, ["b"])
        XCTAssertEqual(store.load(), ["b"])
    }

    func testRemoveUnknownIdIsNoop() throws {
        let store = makeStore()
        _ = try store.add("a")
        let result = try store.remove("not-there")
        XCTAssertEqual(result, ["a"])
    }

    // MARK: - Cross-instance persistence (simulated app restart)

    func testRecentsSurviveAcrossNewStoreInstance() throws {
        let first = makeStore()
        _ = try first.add("a")
        _ = try first.add("b")

        // Fresh store pointed at the same file = next app launch.
        let second = makeStore()
        XCTAssertEqual(second.load(), ["b", "a"])
    }

    // MARK: - AppState integration: in-memory consistency on failure

    /// `AppState.recordCommandInvocation` must keep the in-memory
    /// `commandPaletteRecents` consistent with what the user saw
    /// during this session, even when the persistence layer is
    /// unwritable. Red-team finding P2 #1 from #316.
    @MainActor
    func testRecordCommandInvocationKeepsInMemoryConsistentOnPersistFailure() async throws {
        // Point the store at a path inside a read-only directory
        // — `save` will throw, but the in-memory list must still
        // reflect the invocation.
        let readOnlyDir = tempDir.appendingPathComponent("readonly")
        try FileManager.default.createDirectory(at: readOnlyDir, withIntermediateDirectories: true)
        try FileManager.default.setAttributes(
            [.posixPermissions: 0o500], // r-x for owner, no write
            ofItemAtPath: readOnlyDir.path
        )
        defer {
            // Restore writability so tearDown can delete the dir.
            try? FileManager.default.setAttributes(
                [.posixPermissions: 0o700],
                ofItemAtPath: readOnlyDir.path
            )
        }
        let failingStore = CommandPaletteRecentsStore(
            fileURL: readOnlyDir.appendingPathComponent("recents.json")
        )

        let appState = AppState(commandPaletteRecentsStore: failingStore)
        XCTAssertEqual(appState.commandPaletteRecents, [])

        appState.recordCommandInvocation(id: "slate.editor.save")

        // The in-memory list MUST contain the new id — that's what
        // the user expects to see in Recent during this session.
        // The disk write failed and was NSLog'd; the session view
        // stays consistent.
        XCTAssertEqual(
            appState.commandPaletteRecents,
            ["slate.editor.save"],
            "in-memory recents must reflect the invocation even when persistence fails"
        )
    }

    @MainActor
    func testRecordCommandInvocationLRUOrdering() async throws {
        let store = makeStore()
        let appState = AppState(commandPaletteRecentsStore: store)
        appState.recordCommandInvocation(id: "a")
        appState.recordCommandInvocation(id: "b")
        appState.recordCommandInvocation(id: "a") // re-invoke
        XCTAssertEqual(
            appState.commandPaletteRecents,
            ["a", "b"],
            "re-invoking 'a' moves it to the front; b sinks"
        )
    }

    @MainActor
    func testRecordCommandInvocationCapsAtMaxEntries() async throws {
        let store = makeStore()
        let appState = AppState(commandPaletteRecentsStore: store)
        for i in 0..<(CommandPaletteRecentsStore.maxEntries + 3) {
            appState.recordCommandInvocation(id: "slate.test.\(i)")
        }
        XCTAssertEqual(appState.commandPaletteRecents.count, CommandPaletteRecentsStore.maxEntries)
        XCTAssertEqual(
            appState.commandPaletteRecents.first,
            "slate.test.\(CommandPaletteRecentsStore.maxEntries + 2)"
        )
    }
}
