// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

final class RecentVaultsStoreTests: XCTestCase {
    private var tempDir: URL!

    override func setUpWithError() throws {
        try super.setUpWithError()
        tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-recents-test-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: tempDir, withIntermediateDirectories: true)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: tempDir)
        try super.tearDownWithError()
    }

    private func makeStore() -> RecentVaultsStore {
        let file = tempDir.appendingPathComponent("recent-vaults.json")
        return RecentVaultsStore(fileURL: file)
    }

    // MARK: - Round-trip

    func testLoadFromMissingFileReturnsEmpty() {
        let store = makeStore()
        XCTAssertEqual(store.load(), [])
    }

    func testSaveAndLoadRoundTripsEntries() throws {
        let store = makeStore()
        let entries = [
            RecentVault(path: "/tmp/alpha", displayName: "alpha", lastOpenedMs: 1_700_000_000_000),
            RecentVault(path: "/tmp/beta", displayName: "beta", lastOpenedMs: 1_700_000_500_000),
        ]
        try store.save(entries)
        XCTAssertEqual(store.load(), entries)
    }

    func testLoadFromMalformedFileReturnsEmpty() throws {
        let store = makeStore()
        let file = tempDir.appendingPathComponent("recent-vaults.json")
        try Data("not json".utf8).write(to: file)
        // Corrupt file shouldn't prevent the app from launching with an
        // empty recents list.
        XCTAssertEqual(store.load(), [])
    }

    func testSaveCreatesMissingParentDirectory() throws {
        // Place the file two levels deep so save() must create both
        // intermediate directories. Mirrors the real Application
        // Support layout on first launch.
        let nested = tempDir.appendingPathComponent("a/b/recent.json")
        let store = RecentVaultsStore(fileURL: nested)
        try store.save([
            RecentVault(path: "/x", displayName: "x", lastOpenedMs: 1)
        ])
        XCTAssertTrue(FileManager.default.fileExists(atPath: nested.path))
    }

    // MARK: - LRU / max-entries

    func testAddPrependsAndPersists() throws {
        let store = makeStore()
        let first = RecentVault(path: "/tmp/alpha", displayName: "alpha", lastOpenedMs: 100)
        let second = RecentVault(path: "/tmp/beta", displayName: "beta", lastOpenedMs: 200)

        _ = try store.add(first)
        let after = try store.add(second)

        XCTAssertEqual(after.map(\.path), ["/tmp/beta", "/tmp/alpha"])
        XCTAssertEqual(store.load(), after, "in-memory return value should match what's on disk")
    }

    func testReAddingSamePathMovesEntryToFront() throws {
        let store = makeStore()
        _ = try store.add(RecentVault(path: "/tmp/alpha", displayName: "alpha", lastOpenedMs: 100))
        _ = try store.add(RecentVault(path: "/tmp/beta", displayName: "beta", lastOpenedMs: 200))

        // Re-add alpha with a newer timestamp.
        let refreshed = try store.add(
            RecentVault(path: "/tmp/alpha", displayName: "alpha", lastOpenedMs: 300)
        )

        XCTAssertEqual(refreshed.map(\.path), ["/tmp/alpha", "/tmp/beta"])
        XCTAssertEqual(refreshed[0].lastOpenedMs, 300)
    }

    func testAddCapsAtMaxEntries() throws {
        let store = makeStore()
        for i in 0..<(RecentVaultsStore.maxEntries + 3) {
            _ = try store.add(
                RecentVault(
                    path: "/tmp/vault-\(i)",
                    displayName: "vault-\(i)",
                    lastOpenedMs: Int64(i)
                )
            )
        }
        let final = store.load()
        XCTAssertEqual(final.count, RecentVaultsStore.maxEntries)
        // Most recently added entry is at the front; oldest survivors
        // are the most-recently-added subset.
        XCTAssertEqual(final.first?.path, "/tmp/vault-\(RecentVaultsStore.maxEntries + 2)")
        XCTAssertEqual(
            final.last?.path,
            "/tmp/vault-\(RecentVaultsStore.maxEntries + 2 - (RecentVaultsStore.maxEntries - 1))"
        )
    }

    // MARK: - Removal

    func testRemoveDropsMatchingEntry() throws {
        let store = makeStore()
        _ = try store.add(RecentVault(path: "/tmp/alpha", displayName: "alpha", lastOpenedMs: 100))
        _ = try store.add(RecentVault(path: "/tmp/beta", displayName: "beta", lastOpenedMs: 200))

        let after = try store.remove(path: "/tmp/alpha")
        XCTAssertEqual(after.map(\.path), ["/tmp/beta"])
        XCTAssertEqual(store.load().map(\.path), ["/tmp/beta"])
    }

    func testRemoveUnknownPathIsNoop() throws {
        let store = makeStore()
        _ = try store.add(RecentVault(path: "/tmp/alpha", displayName: "alpha", lastOpenedMs: 100))
        let after = try store.remove(path: "/tmp/missing")
        XCTAssertEqual(after.map(\.path), ["/tmp/alpha"])
    }

    func testLoadTruncatesEntriesAboveCap() throws {
        // Guard against an externally-modified file containing more
        // than maxEntries — downstream UI code should be able to trust
        // the invariant unconditionally.
        let file = tempDir.appendingPathComponent("recent-vaults.json")
        let oversized = (0..<(RecentVaultsStore.maxEntries + 5)).map { i in
            RecentVault(
                path: "/tmp/vault-\(i)",
                displayName: "vault-\(i)",
                lastOpenedMs: Int64(i)
            )
        }
        let data = try JSONEncoder().encode(oversized)
        try data.write(to: file)

        let store = RecentVaultsStore(fileURL: file)
        XCTAssertEqual(store.load().count, RecentVaultsStore.maxEntries)
    }
}
