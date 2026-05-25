// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

/// Persistent store for the welcome screen's recent-vaults list.
///
/// Lives at `~/Library/Application Support/Slate/recent-vaults.json`
/// by default. The location is injected so tests can substitute a
/// temporary URL.
///
/// Concurrency: load/save are synchronous and main-thread-friendly.
/// The vault is single-writer (one user, one process), so we don't
/// need locking — `add` does read → mutate → write each call.
public final class RecentVaultsStore {
    public static let maxEntries = 8

    private let fileURL: URL
    private let fileManager: FileManager

    /// Designated initializer. Production callers should use
    /// `RecentVaultsStore()` which targets the standard Application
    /// Support location; tests pass a temporary URL.
    public init(fileURL: URL, fileManager: FileManager = .default) {
        self.fileURL = fileURL
        self.fileManager = fileManager
    }

    /// Standard location:
    /// `~/Library/Application Support/Slate/recent-vaults.json`.
    public convenience init() throws {
        let dir = try Self.defaultDirectory()
        self.init(fileURL: dir.appendingPathComponent("recent-vaults.json"))
    }

    /// Returns the on-disk list, or an empty list if the file doesn't
    /// exist yet. Malformed JSON is treated as "no recent vaults" — we
    /// don't want a corrupt file to prevent the app from launching.
    ///
    /// The hard cap of `maxEntries` is also enforced on load so an
    /// externally-modified file can't ship a 50-entry list past the
    /// store; downstream UI code can trust the invariant unconditionally.
    public func load() -> [RecentVault] {
        guard fileManager.fileExists(atPath: fileURL.path) else { return [] }
        guard let data = try? Data(contentsOf: fileURL),
            let decoded = try? JSONDecoder().decode([RecentVault].self, from: data)
        else {
            return []
        }
        if decoded.count > Self.maxEntries {
            return Array(decoded.prefix(Self.maxEntries))
        }
        return decoded
    }

    /// Atomically overwrite the on-disk list with `entries`.
    public func save(_ entries: [RecentVault]) throws {
        try ensureDirectoryExists()
        let encoder = JSONEncoder()
        encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
        let data = try encoder.encode(entries)
        try data.write(to: fileURL, options: [.atomic])
    }

    /// Add an entry, moving an existing entry for the same path to the
    /// front (LRU behavior). Returns the updated list. Caps length at
    /// `maxEntries`.
    @discardableResult
    public func add(_ entry: RecentVault) throws -> [RecentVault] {
        var entries = load()
        entries.removeAll { $0.path == entry.path }
        entries.insert(entry, at: 0)
        if entries.count > Self.maxEntries {
            entries = Array(entries.prefix(Self.maxEntries))
        }
        try save(entries)
        return entries
    }

    /// Remove the entry whose path matches `path` (no-op if absent).
    /// Returns the updated list.
    @discardableResult
    public func remove(path: String) throws -> [RecentVault] {
        var entries = load()
        let before = entries.count
        entries.removeAll { $0.path == path }
        if entries.count != before {
            try save(entries)
        }
        return entries
    }

    // MARK: - Private

    private func ensureDirectoryExists() throws {
        let dir = fileURL.deletingLastPathComponent()
        if !fileManager.fileExists(atPath: dir.path) {
            try fileManager.createDirectory(at: dir, withIntermediateDirectories: true)
        }
    }

    private static func defaultDirectory() throws -> URL {
        let appSupport = try FileManager.default.url(
            for: .applicationSupportDirectory,
            in: .userDomainMask,
            appropriateFor: nil,
            create: true
        )
        return appSupport.appendingPathComponent("Slate", isDirectory: true)
    }
}
