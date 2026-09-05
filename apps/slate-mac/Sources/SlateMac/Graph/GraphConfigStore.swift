// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

/// `.slate/graph.json` reader + atomic writer (Milestone P, P2-4 #560).
///
/// Write discipline mirrors the O-5 `history_prefs.rs` pattern: atomic
/// temp-file + rename (a kill mid-write never leaves torn JSON),
/// unknown future top-level keys preserved verbatim on rewrite
/// (forward-compat), and **refuse-to-clobber-unparseable** — if the file
/// exists but isn't a JSON object, the write throws rather than
/// destroying whatever a newer Slate (or a human) put there.
///
/// SINGLE-WRITER: only the Mac app writes `graph.json`, so — unlike
/// `prefs.json` (which the Rust history-prefs writer co-writes and
/// therefore flocks) — no lock is taken. A separate file from
/// `prefs.json` precisely to avoid contending on its `.lock`.
struct GraphConfigStore {
    let vaultRoot: URL

    var configURL: URL {
        vaultRoot.appendingPathComponent(".slate", isDirectory: true)
            .appendingPathComponent("graph.json", isDirectory: false)
    }

    /// Read the config. Missing file → `.default`. Malformed JSON throws
    /// (the caller surfaces it and leaves the file untouched).
    func read() throws -> GraphConfig {
        let url = configURL
        guard FileManager.default.fileExists(atPath: url.path) else { return .default }
        let data: Data
        do {
            data = try Data(contentsOf: url)
        } catch {
            throw PrefsJsonStoreError.readFailed(path: url.path, reason: error.localizedDescription)
        }
        // The codec is core's (W6-2 PR 0b, contracts doc 0b-12): a non-object
        // root, a version that cannot be classified (0b-D8) and a FORWARD
        // version all surface as parse failures, so the caller marks the
        // config read-only and never rewrites the file.
        let text = String(decoding: data, as: UTF8.self)
        do {
            return try graphConfigDecode(json: text).config
        } catch let error as GraphConfigError {
            throw PrefsJsonStoreError.parseFailed(path: url.path, reason: Self.reason(of: error))
        }
    }

    /// The host-facing reason for each core error (the adapter's mapping).
    static func reason(of error: GraphConfigError) -> String {
        switch error {
        case .Unparseable(let reason): return reason
        case .NewerVersion(let version):
            return "graph.json is a newer version (\(version)); not downgrading"
        }
    }

    /// Write the config, preserving unknown top-level keys and refusing
    /// to clobber an unparseable existing file. Atomic via temp+rename.
    func write(_ config: GraphConfig) throws {
        try ensureSlateDirExists()
        let url = configURL

        var existing: String?
        if FileManager.default.fileExists(atPath: url.path) {
            // Read the existing file THROWINGLY: a file that exists but can't
            // be read (permissions, transient I/O) must NOT be treated like a
            // missing file and overwritten — that would clobber whatever it
            // holds. Refuse instead (finding 2). `try?` here was the bug.
            do {
                existing = String(decoding: try Data(contentsOf: url), as: UTF8.self)
            } catch {
                throw PrefsJsonStoreError.writeFailed(
                    path: url.path,
                    reason: "existing graph.json is unreadable; refusing to overwrite: "
                        + error.localizedDescription)
            }
        }

        // The merge policy is core's (W6-2 PR 0b, contracts doc 0b-12): an
        // unparseable existing file is never clobbered, an unclassifiable or
        // newer version never downgraded, every unknown top-level key
        // preserved, the bytes canonical (0b-D5).
        let text: String
        do {
            text = try graphConfigEncode(config: config, existingJson: existing)
        } catch let error as GraphConfigError {
            throw PrefsJsonStoreError.writeFailed(path: url.path, reason: Self.reason(of: error))
        }
        let out = Data(text.utf8)
        do {
            try out.write(to: url, options: .atomic)
        } catch {
            throw PrefsJsonStoreError.writeFailed(path: url.path, reason: error.localizedDescription)
        }
    }

    private func ensureSlateDirExists() throws {
        let dir = vaultRoot.appendingPathComponent(".slate", isDirectory: true)
        guard !FileManager.default.fileExists(atPath: dir.path) else { return }
        do {
            try FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        } catch {
            throw PrefsJsonStoreError.writeFailed(path: dir.path, reason: error.localizedDescription)
        }
    }

}

/// Serializes every `graph.json` write app-wide (Milestone P, P2-4 #560,
/// finding 3). The atomic temp+rename already prevents TORN JSON, but two
/// debounced saves whose read-merge-write cycles overlap could still lose
/// an update (both read the same base, the slower rename wins with the
/// older payload). Funnelling all writes through one actor makes the
/// read-merge-write atomic w.r.t. other writes in THIS process — a `write`
/// runs to completion before the next queued one starts.
///
/// MONOTONIC: each write carries a per-vault, strictly-increasing
/// `generation`; the actor records the newest generation it has written
/// per vault and DROPS any write whose generation is older. So even if the
/// actor executor delivers two queued writes out of order, a stale snapshot
/// can never clobber a newer one (finding 3 round-3 — the check must live
/// in the actor, not after the write on the caller side).
///
/// SINGLE-INSTANCE scope: Slate's Mac app is single-instance, so this is
/// the only writer of any vault's `graph.json`; cross-PROCESS contention
/// (two app instances on one vault) is out of scope and not locked (unlike
/// `prefs.json`, which the Rust core co-writes and therefore flocks).
/// Persistence is best-effort — a failed write (e.g. the refuse-to-clobber
/// guard) is swallowed here; the caller's `graphConfigWritable` gate is the
/// authoritative protection.
actor GraphConfigWriter {
    static let shared = GraphConfigWriter()

    /// Newest generation actually written per vault (monotonic gate).
    private var written: [URL: Int] = [:]

    func write(vault: URL, config: GraphConfig, generation: Int) {
        // Reject a superseded snapshot regardless of delivery order.
        if let seen = written[vault], generation < seen { return }
        written[vault] = generation
        // Best-effort persistence, but LOG failures rather than swallowing
        // them (Codoki review): a permission / disk-full / refuse-to-clobber
        // error must not crash or propagate (the caller's `graphConfigWritable`
        // gate is the authoritative protection), yet a silent drop makes a
        // field "my graph settings won't save" report undiagnosable.
        do {
            try GraphConfigStore(vaultRoot: vault).write(config)
        } catch {
            // Log the FULL vault path so the failing vault is uniquely
            // identifiable (two vaults can share a folder name) — matching
            // the AppState persistence-log convention. Fixed format string:
            // a path / error can legally contain `%`, which NSLog would
            // otherwise read as an unsupplied format specifier (corrupting
            // the message, e.g. `%@` → `(null)`).
            let message = "Failed to persist graph.json for vault '\(vault.path)': \(error)"
            NSLog("%@", message)
        }
    }
}
