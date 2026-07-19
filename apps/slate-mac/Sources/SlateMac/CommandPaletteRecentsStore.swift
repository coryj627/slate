// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

/// Thin host adapter for the command palette's "Recent" section
/// (Milestone Q #316; policy core-owned since W0.5-1 #717).
///
/// This class owns exactly what the storage boundary assigns the host:
/// the platform path (`~/Library/Application Support/Slate/…` — global,
/// device-local app state, never per-vault prefs) and atomic file I/O
/// with a bounded read. Everything semantic — the byte format, the
/// malformed-tolerant decode, dedupe + cap, and the LRU transitions —
/// lives in `slate_core::palette` behind the FFI, shared verbatim with
/// the Windows host (`%LOCALAPPDATA%\Slate\…`).
///
/// Concurrency: load/save are synchronous and main-thread-friendly.
/// Single-writer (one user, one process), so no locking — `add` does
/// read → mutate → write each call.
public final class CommandPaletteRecentsStore {
    /// Hard cap on persisted recents — mirror of the core policy
    /// constant (`palette::RECENTS_MAX_ENTRIES`), kept host-side only
    /// so tests can name the boundary they exercise.
    public static let maxEntries = 10

    /// Upper bound on the recents file size `load()` will read (#341).
    /// Mirror of core's `palette::RECENTS_MAX_FILE_BYTES`: the bounded
    /// read below uses it to avoid ever materializing a huge file, and
    /// core's decode independently enforces the same cap (the host
    /// bound is I/O hygiene; core is the authority).
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
    /// doesn't exist yet. Decoding is core's
    /// (`palette::recents_decode`): malformed or oversized bytes are
    /// treated as "no recents" — a corrupt file must never block the
    /// palette opening — duplicates dedupe to first-seen order, and
    /// the result is capped.
    ///
    /// **Bounded read (#341).** The host's one I/O responsibility
    /// beyond existence: instead of stat-then-read (a TOCTOU window —
    /// the file could grow or be swapped between check and read, #352
    /// review), open once and read at most `maxFileBytes + 1` bytes.
    /// One byte past the cap distinguishes "at or under the limit"
    /// from "over it" without ever materializing an enormous file;
    /// core's decode then independently rejects over-cap input. Any
    /// open / read failure (including deletion after the `fileExists`
    /// check) falls to the empty list.
    public func load() -> [String] {
        guard fileManager.fileExists(atPath: fileURL.path) else { return [] }
        guard let handle = try? FileHandle(forReadingFrom: fileURL) else {
            return []
        }
        defer { try? handle.close() }
        // `read(upToCount:)` on a regular file returns
        // `min(requested, bytesRemaining)`, so asking for one byte
        // past the cap yields exactly `maxFileBytes + 1` bytes when
        // the file is larger — the over-threshold signal core's
        // decode maps to the empty list.
        guard let data = try? handle.read(upToCount: Self.maxFileBytes + 1) else {
            return []
        }
        return paletteRecentsDecode(bytes: data)
    }

    /// Atomically overwrite the on-disk list with `ids`, in core's
    /// canonical byte format (`palette::recents_encode`).
    public func save(_ ids: [String]) throws {
        try ensureDirectoryExists()
        try paletteRecentsEncode(ids: ids).write(to: fileURL, options: [.atomic])
    }

    /// Add an id via core's LRU transition (`palette::recents_add`):
    /// an existing entry for the same id moves to the front, length is
    /// capped. Returns the updated list.
    @discardableResult
    public func add(_ id: String) throws -> [String] {
        let entries = paletteRecentsAdd(ids: load(), id: id)
        try save(entries)
        return entries
    }

    /// Remove every id matching `id` (no-op if absent) via core's
    /// transition (`palette::recents_remove`). Returns the updated
    /// list. Used when the palette wires up the future "Forget this
    /// command" affordance (#316 V1.x or #322 territory); for now
    /// it's exercised only by tests.
    @discardableResult
    public func remove(_ id: String) throws -> [String] {
        let before = load()
        let entries = paletteRecentsRemove(ids: before, id: id)
        if entries.count != before.count {
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
