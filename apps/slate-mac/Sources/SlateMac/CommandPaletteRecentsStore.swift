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
    /// reaches the cap, the loop breaks — work is bounded at
    /// `O(maxEntries)` regardless of input size. A hand-edited
    /// (or maliciously crafted) file with millions of entries
    /// stops being scanned after the first `maxEntries` uniques.
    public func load() -> [String] {
        guard fileManager.fileExists(atPath: fileURL.path) else { return [] }
        guard let data = try? Data(contentsOf: fileURL),
              let decoded = try? JSONDecoder().decode([String].self, from: data)
        else {
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
