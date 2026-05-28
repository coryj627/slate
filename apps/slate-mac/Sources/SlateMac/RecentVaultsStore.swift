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

    /// Upper bound on the recent-vaults file size `load()` will read
    /// (#353, mirroring the #341 guard on `CommandPaletteRecentsStore`).
    /// A well-formed file holds at most `maxEntries` (8) `RecentVault`
    /// entries; each is a path (≤ ~1 KiB even for a deep macOS path,
    /// bounded by `PATH_MAX`), a display name, and a millisecond
    /// timestamp, plus JSON structure + pretty-print whitespace —
    /// generously ~1.7 KiB per entry, so a full list is ~14 KiB.
    /// 64 KiB is ~4× that headroom while still refusing a malformed
    /// or hand-crafted huge file before it's read into memory and
    /// decoded into a large `[RecentVault]`. Files larger than this
    /// are treated as malformed (→ empty list).
    public static let maxFileBytes: Int = 1 << 16  // 64 KiB

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
    /// The hard cap of `maxEntries` is also enforced on load (via a
    /// trailing `prefix(maxEntries)`) so an externally-modified file
    /// can't ship a 50-entry list past the store; downstream UI code
    /// can trust the invariant unconditionally. Unlike
    /// `CommandPaletteRecentsStore`, there's no load-time dedupe —
    /// entries are path-keyed and `add` already removes any prior
    /// entry for the same path, so duplicates don't accumulate.
    ///
    /// **File-size guard, bounded read (#353).** Mirrors the
    /// `CommandPaletteRecentsStore` guard: open the file once and read
    /// at most `maxFileBytes + 1` bytes. A regular-file read returns
    /// `min(requested, remaining)`, so a result strictly larger than
    /// `maxFileBytes` means the file is over the limit (→ empty list)
    /// and we never allocate more than `maxFileBytes + 1` bytes no
    /// matter how huge the on-disk file is. A single read (rather than
    /// stat-then-read) means there's no TOCTOU window between a size
    /// check and the read. Any open/read failure (incl. a delete race
    /// after `fileExists`) falls to the empty list, same as a
    /// malformed decode.
    public func load() -> [RecentVault] {
        guard fileManager.fileExists(atPath: fileURL.path) else { return [] }
        guard let handle = try? FileHandle(forReadingFrom: fileURL) else {
            return []
        }
        defer { try? handle.close() }
        guard let data = try? handle.read(upToCount: Self.maxFileBytes + 1) else {
            return []
        }
        if data.count > Self.maxFileBytes {
            NSLog(
                "RecentVaultsStore: recent-vaults file exceeds the "
                + "\(Self.maxFileBytes)-byte threshold; treating as malformed."
            )
            return []
        }
        guard let decoded = try? JSONDecoder().decode([RecentVault].self, from: data) else {
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
