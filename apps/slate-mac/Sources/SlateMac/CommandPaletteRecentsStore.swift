// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

/// Persistent store for the command palette's "Recent" section
/// (Milestone Q #316).
///
/// Mirrors the `RecentVaultsStore` shape: file-backed JSON, atomic
/// write, malformed-tolerant load, LRU `add` that moves an existing
/// entry to the front. Cap is enforced on every load so an
/// externally-modified file can't push more than `maxEntries`
/// downstream.
///
/// Lives at
/// `~/Library/Application Support/Slate/command-palette-recents.json`
/// by default; the location is injected so tests can substitute a
/// temporary URL.
///
/// Concurrency: load/save are synchronous and main-thread-friendly.
/// Single-writer (one user, one process), so no locking — `add` does
/// read → mutate → write each call.
public final class CommandPaletteRecentsStore {
    /// Hard cap on persisted recents. The palette UI shows the
    /// "Recent" section ordered by most-recent-first; trimming past
    /// this point keeps the panel from growing unboundedly.
    public static let maxEntries = 10

    /// Upper bound on the recents file size `load()` will read
    /// (#341). A well-formed file holds at most `maxEntries` ids of
    /// ~50 bytes each (including JSON quoting + commas) — under
    /// 1 KiB. 64 KiB is generously above that while still refusing
    /// a malformed or hand-crafted huge file before it's read into
    /// memory and decoded. Files strictly larger than this are
    /// treated as malformed (→ empty list).
    public static let maxFileBytes: Int = 1 << 16  // 64 KiB

    private let fileURL: URL
    private let fileManager: FileManager

    /// Designated initialiser. Production callers should use
    /// `CommandPaletteRecentsStore()` which targets the standard
    /// Application Support location; tests pass a temporary URL.
    public init(fileURL: URL, fileManager: FileManager = .default) {
        self.fileURL = fileURL
        self.fileManager = fileManager
    }

    /// Standard location:
    /// `~/Library/Application Support/Slate/command-palette-recents.json`.
    public convenience init() throws {
        let dir = try Self.defaultDirectory()
        self.init(fileURL: dir.appendingPathComponent("command-palette-recents.json"))
    }

    /// Returns the on-disk list, or an empty list if the file
    /// doesn't exist yet. Malformed JSON is treated as "no recents"
    /// — we never want a corrupt file to prevent the app launching
    /// or the palette opening.
    ///
    /// **Dedupes on load.** Our own `add` already removes a prior
    /// occurrence before insert, but a hand-edited file can contain
    /// duplicate ids; surfacing both would render the same command
    /// in Recent twice and cause arrow-nav to cycle the same id
    /// twice. The dedupe preserves first-seen order so the most-
    /// recent-first invariant survives external edits.
    ///
    /// **Short-circuits at `maxEntries`.** Once the deduped result
    /// reaches the cap, the loop breaks — bounding the typical
    /// case to ~`maxEntries` iterations even when the input is
    /// huge. Note the worst case is still O(`decoded.count`):
    /// if the file has fewer than `maxEntries` unique ids, the
    /// loop walks every entry looking for the next unique.
    ///
    /// **File-size guard (#341), bounded read.** Instead of stat-
    /// then-read (which leaves a TOCTOU window — the file could grow
    /// or be swapped between the size check and the read, #352
    /// review), we open the file once and read at most
    /// `maxFileBytes + 1` bytes. Reading one byte past the cap lets
    /// us distinguish "at or under the limit" from "over it" without
    /// ever allocating more than `maxFileBytes + 1` bytes, even if
    /// the on-disk file is enormous. A result strictly larger than
    /// `maxFileBytes` is treated as malformed (→ empty list). There
    /// is no separate stat, so nothing can change between check and
    /// use. Single-user local file, so this is defensive hardening
    /// rather than a live attack vector (our own `add` caps at
    /// `maxEntries`).
    ///
    /// Any open / read failure (including the file being deleted
    /// after the `fileExists` check) falls to the empty list, same
    /// as a malformed decode — a corrupt or vanished file must
    /// never block the palette opening.
    public func load() -> [String] {
        guard fileManager.fileExists(atPath: fileURL.path) else { return [] }
        guard let handle = try? FileHandle(forReadingFrom: fileURL) else {
            return []
        }
        defer { try? handle.close() }
        // `read(upToCount:)` on a regular file returns
        // `min(requested, bytesRemaining)`, so asking for one byte
        // past the cap yields exactly `maxFileBytes + 1` bytes when
        // the file is larger — our over-threshold signal — and the
        // whole file otherwise. (A short read on an oversized file
        // would still fail safe: the truncated buffer fails JSON
        // decode below, also → empty list.)
        guard let data = try? handle.read(upToCount: Self.maxFileBytes + 1) else {
            return []
        }
        if data.count > Self.maxFileBytes {
            NSLog(
                "CommandPaletteRecentsStore: recents file exceeds the "
                + "\(Self.maxFileBytes)-byte threshold; treating as malformed."
            )
            return []
        }
        guard let decoded = try? JSONDecoder().decode([String].self, from: data) else {
            return []
        }
        var seen = Set<String>()
        var deduped: [String] = []
        deduped.reserveCapacity(Self.maxEntries)
        for id in decoded {
            if seen.insert(id).inserted {
                deduped.append(id)
                // `>=` not `==` — defensive. The loop appends one
                // id per match today, so `==` would suffice, but
                // any future refactor that batches appends could
                // overshoot the cap if the check is sharp-edged.
                if deduped.count >= Self.maxEntries { break }
            }
        }
        return deduped
    }

    /// Atomically overwrite the on-disk list with `ids`.
    public func save(_ ids: [String]) throws {
        try ensureDirectoryExists()
        let encoder = JSONEncoder()
        encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
        let data = try encoder.encode(ids)
        try data.write(to: fileURL, options: [.atomic])
    }

    /// Add an id, moving an existing entry for the same id to the
    /// front (LRU). Returns the updated list. Caps length at
    /// `maxEntries`.
    @discardableResult
    public func add(_ id: String) throws -> [String] {
        var entries = load()
        entries.removeAll { $0 == id }
        entries.insert(id, at: 0)
        if entries.count > Self.maxEntries {
            entries = Array(entries.prefix(Self.maxEntries))
        }
        try save(entries)
        return entries
    }

    /// Remove every id matching `id` (no-op if absent). Returns the
    /// updated list. Used when the palette wires up the future
    /// "Forget this command" affordance (#316 V1.x or #322
    /// territory); for now it's exercised only by tests.
    @discardableResult
    public func remove(_ id: String) throws -> [String] {
        var entries = load()
        let before = entries.count
        entries.removeAll { $0 == id }
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
