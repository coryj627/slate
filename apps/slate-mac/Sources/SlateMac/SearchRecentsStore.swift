// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

/// Per-vault persistence for the search overlay's recent-query list
/// (#876; HIG macos/searching.md:37 — "show recent searches … before
/// and during typing", :38 privacy — offer a way to clear them).
///
/// Structurally a `FileRecentsStore` sibling — LRU `add`, dedupe-on-load,
/// `maxEntries` cap, `maxFileBytes` bounded read, malformed-tolerant load,
/// WorkspaceStore temp-file-then-rename write discipline — with two
/// deliberate differences:
///
///  - **Entries are raw query strings** (`"budget 2026"`), not file paths
///    or command ids. Queries are vault-content-specific, so — like
///    `FileRecentsStore` and unlike the global `CommandPaletteRecentsStore`
///    — the store is **per vault**, at `<vault>/.slate/search-recents.json`
///    (beside `workspace.json` / `file-recents.json`, NOT the CLI-shared
///    `prefs.json`).
///  - **`clear()`** wipes the whole list in one call — the idle-state
///    "Clear" affordance the privacy note calls for. (`FileRecentsStore`
///    never needed a bulk clear; `CommandPaletteRecentsStore` exposes a
///    single-id `remove` instead.)
///
/// A malformed / oversized / vanished file always degrades to an empty
/// list — the overlay must open regardless of recents state (it falls
/// back to the "Type to search." idle hint).
///
/// Concurrency: load/save are synchronous and main-thread-friendly.
/// Single-writer (one user, one process), so `add` does
/// read → mutate → write each call with no locking.
struct SearchRecentsStore {
    /// Hard cap on persisted recent queries. Smaller than the file
    /// store's 50 (a compact overlay list, not a deep navigation
    /// history) but larger than a single screenful so a little scroll-
    /// back survives. Enforced on load (dedupe short-circuit) and on
    /// `add`.
    static let maxEntries = 20

    /// Upper bound on the recents file `load()` will read (mirrors the
    /// file/palette stores' #341 guard). 20 short query strings stay
    /// well under this; a file over it is corrupt or hostile and is
    /// treated as malformed (→ empty list).
    static let maxFileBytes: Int = 1 << 16  // 64 KiB

    let vaultRoot: URL

    /// `<vault>/.slate/search-recents.json` — same `.slate/` directory
    /// WorkspaceStore uses for `workspace.json`.
    var fileURL: URL {
        vaultRoot.appendingPathComponent(".slate/search-recents.json")
    }

    /// The on-disk list (most-recent-first), or empty on a missing /
    /// malformed / oversized / unreadable file. Dedupes on load and
    /// short-circuits at `maxEntries` — the same bounded-read + dedupe
    /// discipline as `FileRecentsStore.load()`; see
    /// `CommandPaletteRecentsStore.load()` for the full rationale on why
    /// one byte past the cap is read and why every failure arm falls to
    /// the empty list.
    func load() -> [String] {
        guard let handle = try? FileHandle(forReadingFrom: fileURL) else { return [] }
        defer { try? handle.close() }
        guard let data = try? handle.read(upToCount: Self.maxFileBytes + 1) else {
            return []
        }
        if data.count > Self.maxFileBytes {
            let message =
                "SearchRecentsStore: recents file exceeds the "
                + "\(Self.maxFileBytes)-byte threshold; treating as malformed."
            NSLog("%@", message)
            return []
        }
        guard let decoded = try? JSONDecoder().decode([String].self, from: data) else {
            return []
        }
        var seen = Set<String>()
        var deduped: [String] = []
        deduped.reserveCapacity(Self.maxEntries)
        for query in decoded {
            if seen.insert(query).inserted {
                deduped.append(query)
                if deduped.count >= Self.maxEntries { break }
            }
        }
        return deduped
    }

    /// Atomically overwrite the on-disk list. Temp-file-then-rename in
    /// `.slate/` (same volume ⇒ atomic) — the WorkspaceStore write
    /// discipline `FileRecentsStore` uses.
    func save(_ queries: [String]) throws {
        let dir = fileURL.deletingLastPathComponent()
        try FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        let encoder = JSONEncoder()
        encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
        let data = try encoder.encode(queries)
        let tmp = dir.appendingPathComponent("search-recents.json.tmp-\(UUID().uuidString)")
        try data.write(to: tmp, options: [])
        _ = try FileManager.default.replaceItemAt(fileURL, withItemAt: tmp)
    }

    /// Add a query, moving an existing entry to the front (LRU).
    /// Returns the updated list, capped at `maxEntries`.
    @discardableResult
    func add(_ query: String) throws -> [String] {
        var entries = load()
        entries.removeAll { $0 == query }
        entries.insert(query, at: 0)
        if entries.count > Self.maxEntries {
            entries = Array(entries.prefix(Self.maxEntries))
        }
        try save(entries)
        return entries
    }

    /// Forget every remembered query (the idle-state "Clear" affordance,
    /// searching.md:38). Persists an empty list rather than deleting the
    /// file so a subsequent `load()` path stays identical.
    func clear() throws {
        try save([])
    }
}
