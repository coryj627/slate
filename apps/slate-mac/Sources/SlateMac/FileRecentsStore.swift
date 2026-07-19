// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

/// ONE device-local file-recency history per vault (FL3-3 #660),
/// shared by the Quick Switcher and the sidebar Recents section.
///
/// Storage moved (from `<vault>/.slate/file-recents.json`) to bounded
/// UserDefaults data keyed by the vault's physical identity — recency is
/// device state, not vault content, so it must not sync with the vault
/// or dirty it. The legacy in-vault file migrates once: it is merged,
/// written to defaults, verified by read-back, and only then removed.
/// Missing, malformed, oversized, or repeated migration is safe; the
/// two stores are never in active use together.
///
/// Keying: `(device, inode)` from the session's admission anchor keeps
/// the history stable across renames/moves of the vault folder. When no
/// identity was observable (degraded filesystems) a path-derived key
/// keeps recents functional, merely spelling-stable instead.
struct FileRecentsStore {
    /// Retained history depth (Quick Switcher consumes all of it; the
    /// sidebar displays the first 10 eligible).
    static let maxEntries = 50

    /// Sidebar display slice (spec FL3-3.3).
    static let sidebarDisplayCount = 10

    /// Bounded read for the LEGACY file during migration (mirrors the
    /// palette store's #341 guard); an oversized legacy file migrates
    /// as empty and is still retired.
    static let maxFileBytes: Int = 1 << 18  // 256 KiB

    /// Bounded defaults payload: 50 paths at 4 KiB ceiling each.
    static let maxDefaultsEntryLength = 4_096

    let vaultRoot: URL
    let identity: SidebarVaultPrefsStore.RootIdentity?
    let defaults: UserDefaults

    init(
        vaultRoot: URL,
        identity: SidebarVaultPrefsStore.RootIdentity? = nil,
        defaults: UserDefaults = .standard
    ) {
        self.vaultRoot = vaultRoot
        self.identity = identity
        self.defaults = defaults
    }

    /// Stable defaults key. Physical identity when observed; a
    /// path-derived fallback otherwise.
    var defaultsKey: String {
        if let identity {
            return "slate.fileRecents.v2.\(identity.device)-\(identity.inode)"
        }
        let sanitized = vaultRoot.path.replacingOccurrences(of: ".", with: "_")
        return "slate.fileRecents.v2.path.\(sanitized)"
    }

    /// `<vault>/.slate/file-recents.json` — the LEGACY location, read
    /// only by the one-time migration (and by tests seeding it).
    var legacyFileURL: URL {
        vaultRoot.appendingPathComponent(".slate/file-recents.json")
    }

    /// The history (most-recent-first). Runs the one-time legacy
    /// migration when this vault's defaults slot has never been written.
    func load() -> [String] {
        migrateLegacyIfNeeded()
        return sanitized(defaults.stringArray(forKey: defaultsKey) ?? [])
    }

    /// Overwrite the history (bounded, deduped).
    func save(_ paths: [String]) {
        defaults.set(sanitized(paths), forKey: defaultsKey)
    }

    /// Add a path, moving an existing entry to the front (LRU).
    @discardableResult
    func add(_ path: String) -> [String] {
        var entries = load()
        entries.removeAll { $0 == path }
        entries.insert(path, at: 0)
        let bounded = sanitized(entries)
        defaults.set(bounded, forKey: defaultsKey)
        return bounded
    }

    /// Clear Recents: the shared history empties (the slot stays
    /// written, so migration never resurrects the legacy file).
    func clear() {
        defaults.set([String](), forKey: defaultsKey)
    }

    private func sanitized(_ paths: [String]) -> [String] {
        var seen = Set<String>()
        var deduped: [String] = []
        deduped.reserveCapacity(Self.maxEntries)
        for path in paths where path.count <= Self.maxDefaultsEntryLength {
            if seen.insert(path).inserted {
                deduped.append(path)
                if deduped.count >= Self.maxEntries { break }
            }
        }
        return deduped
    }

    /// One-shot: a written defaults slot (even an empty one) means the
    /// migration already happened. The legacy file is removed only after
    /// the defaults write proves durable by read-back, so a failed write
    /// retries on the next load; a malformed/oversized legacy file
    /// migrates as empty and is retired the same way.
    private func migrateLegacyIfNeeded() {
        guard defaults.object(forKey: defaultsKey) == nil else { return }
        let legacy = readLegacy()
        defaults.set(legacy, forKey: defaultsKey)
        guard defaults.stringArray(forKey: defaultsKey) != nil else { return }
        try? FileManager.default.removeItem(at: legacyFileURL)
    }

    private func readLegacy() -> [String] {
        guard let handle = try? FileHandle(forReadingFrom: legacyFileURL) else {
            return []
        }
        defer { try? handle.close() }
        guard let data = try? handle.read(upToCount: Self.maxFileBytes + 1),
            data.count <= Self.maxFileBytes,
            let decoded = try? JSONDecoder().decode([String].self, from: data)
        else { return [] }
        return sanitized(decoded)
    }
}
