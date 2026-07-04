// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

/// Per-vault persistence for the quick switcher's file recency order
/// (U1-5 follow-up #495).
///
/// Structurally a `CommandPaletteRecentsStore` sibling — LRU `add`,
/// dedupe-on-load, `maxEntries` cap, `maxFileBytes` bounded read,
/// malformed-tolerant load — but two things differ deliberately:
///
///  - **Entries are vault-relative file paths** (`notes/foo.md`), not
///    command ids, and there can legitimately be many more of them, so
///    the cap is 50 rather than the palette's 10.
///  - **Persisted per vault** at `<vault>/.slate/file-recents.json`,
///    the same directory `WorkspaceStore` writes `workspace.json` to.
///    We reuse `WorkspaceStore`'s write discipline: temp file in the
///    same `.slate/` directory + `replaceItemAt` rename (atomic on the
///    same volume), so a crash mid-write can't leave a half-written
///    recents file. (`CommandPaletteRecentsStore` used a global
///    Application-Support path + `Data.write(.atomic)`; the per-vault
///    placement is the WorkspaceStore precedent, not the palette one.)
///
/// A malformed / oversized / vanished file always degrades to an empty
/// list — the quick switcher must open regardless of recents state
/// (it falls back to the alphabetical file list).
///
/// Concurrency: load/save are synchronous and main-thread-friendly.
/// Single-writer (one user, one process), so `add` does
/// read → mutate → write each call with no locking.
struct FileRecentsStore {
    /// Hard cap on persisted recents. Larger than the palette's 10:
    /// a vault has many files and the empty-query list surfaces the
    /// recents first, so a deeper history is useful. Enforced on load
    /// (dedupe short-circuit) and on `add`.
    static let maxEntries = 50

    /// Upper bound on the recents file `load()` will read (mirrors the
    /// palette store's #341 guard). 50 vault-relative paths of a few
    /// hundred bytes each stay well under this; a file over it is
    /// corrupt or hostile and is treated as malformed (→ empty list).
    static let maxFileBytes: Int = 1 << 18  // 256 KiB

    let vaultRoot: URL

    /// `<vault>/.slate/file-recents.json` — same `.slate/` directory
    /// WorkspaceStore uses for `workspace.json`.
    var fileURL: URL {
        vaultRoot.appendingPathComponent(".slate/file-recents.json")
    }

    /// The on-disk list (most-recent-first), or empty on a missing /
    /// malformed / oversized / unreadable file. Dedupes on load and
    /// short-circuits at `maxEntries` — same bounded-read + dedupe
    /// discipline as `CommandPaletteRecentsStore.load()`; see that
    /// type for the full rationale on why one byte past the cap is
    /// read and why every failure arm falls to the empty list.
    func load() -> [String] {
        guard let handle = try? FileHandle(forReadingFrom: fileURL) else { return [] }
        defer { try? handle.close() }
        guard let data = try? handle.read(upToCount: Self.maxFileBytes + 1) else {
            return []
        }
        if data.count > Self.maxFileBytes {
            NSLog(
                "FileRecentsStore: recents file exceeds the "
                + "\(Self.maxFileBytes)-byte threshold; treating as malformed.")
            return []
        }
        guard let decoded = try? JSONDecoder().decode([String].self, from: data) else {
            return []
        }
        var seen = Set<String>()
        var deduped: [String] = []
        deduped.reserveCapacity(Self.maxEntries)
        for path in decoded {
            if seen.insert(path).inserted {
                deduped.append(path)
                if deduped.count >= Self.maxEntries { break }
            }
        }
        return deduped
    }

    /// Atomically overwrite the on-disk list. Temp-file-then-rename in
    /// `.slate/` (same volume ⇒ atomic) — the WorkspaceStore write
    /// discipline, not the palette store's `Data.write(.atomic)`.
    func save(_ paths: [String]) throws {
        let dir = fileURL.deletingLastPathComponent()
        try FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        let encoder = JSONEncoder()
        encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
        let data = try encoder.encode(paths)
        let tmp = dir.appendingPathComponent("file-recents.json.tmp-\(UUID().uuidString)")
        try data.write(to: tmp, options: [])
        _ = try FileManager.default.replaceItemAt(fileURL, withItemAt: tmp)
    }

    /// Add a path, moving an existing entry to the front (LRU).
    /// Returns the updated list, capped at `maxEntries`.
    @discardableResult
    func add(_ path: String) throws -> [String] {
        var entries = load()
        entries.removeAll { $0 == path }
        entries.insert(path, at: 0)
        if entries.count > Self.maxEntries {
            entries = Array(entries.prefix(Self.maxEntries))
        }
        try save(entries)
        return entries
    }
}
