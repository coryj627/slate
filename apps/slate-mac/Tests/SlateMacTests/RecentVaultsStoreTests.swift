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

    // MARK: - File-size guard (#353)

    /// A `[RecentVault]` JSON fixture, padded with legal whitespace
    /// before the closing `]` to an exact byte length. Decodes to a
    /// single entry `RecentVault(path: "/x", displayName: "x",
    /// lastOpenedMs: 1)`.
    private func paddedRecentVaultsJSON(toByteCount target: Int) -> Data {
        let prefix = #"[{"path":"/x","displayName":"x","lastOpenedMs":1}"#
        let suffix = "]"
        let padCount = target - prefix.utf8.count - suffix.utf8.count
        let json = prefix + String(repeating: " ", count: max(0, padCount)) + suffix
        return json.data(using: .utf8)!
    }

    /// A file strictly larger than `maxFileBytes` is refused before
    /// the decode — even when its contents are valid JSON. A valid-
    /// JSON oversized file proves the guard fires pre-decode (a valid
    /// file would otherwise decode to one entry), not via the
    /// malformed path.
    func testLoadRefusesFileLargerThanThreshold() throws {
        let file = tempDir.appendingPathComponent("recent-vaults.json")
        let data = paddedRecentVaultsJSON(toByteCount: RecentVaultsStore.maxFileBytes + 1)
        XCTAssertEqual(data.count, RecentVaultsStore.maxFileBytes + 1)
        // Sanity: the fixture is itself valid JSON, so only the size
        // guard can reject it.
        XCTAssertEqual(
            try JSONDecoder().decode([RecentVault].self, from: data),
            [RecentVault(path: "/x", displayName: "x", lastOpenedMs: 1)]
        )
        try data.write(to: file)

        XCTAssertEqual(
            RecentVaultsStore(fileURL: file).load(), [],
            "a file 1 byte over maxFileBytes must be refused before decode, even though its JSON is valid"
        )
    }

    /// A file at EXACTLY `maxFileBytes` is accepted (the guard uses
    /// `>` not `>=`). Proven by decoding to the expected entry rather
    /// than the empty list.
    func testLoadAcceptsFileAtExactlyThreshold() throws {
        let file = tempDir.appendingPathComponent("recent-vaults.json")
        let data = paddedRecentVaultsJSON(toByteCount: RecentVaultsStore.maxFileBytes)
        XCTAssertEqual(data.count, RecentVaultsStore.maxFileBytes)
        try data.write(to: file)

        XCTAssertEqual(
            RecentVaultsStore(fileURL: file).load(),
            [RecentVault(path: "/x", displayName: "x", lastOpenedMs: 1)],
            "a file at exactly maxFileBytes must pass the guard and decode normally"
        )
    }

    /// Open/read-failure path: the bounded read opens the file with a
    /// `FileHandle`; if that fails on a file that passed `fileExists`
    /// (unreadable perms, or a delete race), `load()` must fall to the
    /// empty list — the welcome screen must never be blocked by an
    /// unreadable recents file.
    func testLoadReturnsEmptyWhenFileUnreadable() throws {
        let file = tempDir.appendingPathComponent("recent-vaults.json")
        try JSONEncoder().encode(
            [RecentVault(path: "/x", displayName: "x", lastOpenedMs: 1)]
        ).write(to: file)
        try FileManager.default.setAttributes(
            [.posixPermissions: 0o000], ofItemAtPath: file.path
        )
        defer {
            try? FileManager.default.setAttributes(
                [.posixPermissions: 0o600], ofItemAtPath: file.path
            )
        }
        try XCTSkipIf(
            FileManager.default.isReadableFile(atPath: file.path),
            "file still readable (running as root?); can't exercise the open-failure path"
        )

        XCTAssertEqual(
            RecentVaultsStore(fileURL: file).load(), [],
            "an unreadable recent-vaults file must load as empty, not crash or block"
        )
    }
}
