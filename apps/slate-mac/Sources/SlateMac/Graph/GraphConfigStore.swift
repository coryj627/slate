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
        guard let any = try? JSONSerialization.jsonObject(with: data, options: []),
            let root = any as? [String: Any]
        else {
            throw PrefsJsonStoreError.parseFailed(
                path: url.path, reason: "expected a JSON object at the top level")
        }
        // Refuse to interpret a FORWARD-version file: a newer Slate may have
        // redefined a known section's schema, so decoding it here and later
        // re-encoding at our version would silently downgrade (destroy) that
        // data. Surface it as a parse failure so the caller marks the config
        // read-only and never rewrites the file (finding 2).
        if let v = root["version"] as? Int, v > GraphConfig.version {
            throw PrefsJsonStoreError.parseFailed(
                path: url.path,
                reason: "graph.json is a newer version (\(v) > \(GraphConfig.version)); "
                    + "not downgrading")
        }
        return Self.decode(root)
    }

    /// Write the config, preserving unknown top-level keys and refusing
    /// to clobber an unparseable existing file. Atomic via temp+rename.
    func write(_ config: GraphConfig) throws {
        try ensureSlateDirExists()
        let url = configURL

        var root: [String: Any] = [:]
        if FileManager.default.fileExists(atPath: url.path) {
            // Read the existing file THROWINGLY: a file that exists but can't
            // be read (permissions, transient I/O) must NOT be treated like a
            // missing file and overwritten — that would clobber whatever it
            // holds. Refuse instead (finding 2). `try?` here was the bug.
            let data: Data
            do {
                data = try Data(contentsOf: url)
            } catch {
                throw PrefsJsonStoreError.writeFailed(
                    path: url.path,
                    reason: "existing graph.json is unreadable; refusing to overwrite: "
                        + error.localizedDescription)
            }
            guard let parsed = try? JSONSerialization.jsonObject(with: data, options: []),
                let dict = parsed as? [String: Any]
            else {
                // The file exists but isn't a JSON object — refuse to
                // clobber it (O-5 rule); a newer schema or hand-edit
                // shouldn't be silently destroyed.
                throw PrefsJsonStoreError.writeFailed(
                    path: url.path,
                    reason: "existing graph.json is unparseable; refusing to overwrite")
            }
            // A FORWARD-version file: refuse to downgrade it (finding 2). We
            // can preserve unknown TOP-LEVEL keys, but a newer version may
            // have changed a KNOWN section's meaning, so re-encoding at our
            // version would corrupt it.
            if let v = dict["version"] as? Int, v > GraphConfig.version {
                throw PrefsJsonStoreError.writeFailed(
                    path: url.path,
                    reason: "existing graph.json is a newer version (\(v) > "
                        + "\(GraphConfig.version)); refusing to downgrade")
            }
            root = dict  // preserve unknown keys
        }

        Self.encode(config, into: &root)

        let out: Data
        do {
            out = try JSONSerialization.data(
                withJSONObject: root, options: [.prettyPrinted, .sortedKeys])
        } catch {
            throw PrefsJsonStoreError.writeFailed(
                path: url.path, reason: "JSON encoding failed: \(error.localizedDescription)")
        }
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

    // MARK: - Codec (manual, for unknown-key preservation + clamping)

    static func decode(_ root: [String: Any]) -> GraphConfig {
        var config = GraphConfig.default
        if let f = root["filters"] as? [String: Any] {
            config.filters = GraphFilterConfig(
                includeAttachments: f["includeAttachments"] as? Bool ?? false,
                includeGhosts: f["includeGhosts"] as? Bool ?? true,
                orphansOnly: f["orphansOnly"] as? Bool ?? false,
                nameQuery: f["nameQuery"] as? String ?? "")
        }
        if let arr = root["groups"] as? [[String: Any]] {
            config.groups = arr.compactMap { g in
                guard let query = g["query"] as? String else { return nil }
                let token = (g["colorToken"] as? String).flatMap(GraphColorToken.init) ?? .blue
                let ring = (g["ringStyle"] as? String).flatMap(GraphRingStyle.init) ?? .solid
                return GraphGroup(query: query, colorToken: token, ringStyle: ring)
            }
        }
        if let d = root["display"] as? [String: Any] {
            config.display = GraphDisplay(
                arrows: d["arrows"] as? Bool ?? false,
                textFadeZoom: clampD(d["textFadeZoom"], 0.1, 4.0, 0.55),
                nodeSizeMultiplier: clampD(d["nodeSizeMultiplier"], 0.5, 2.0, 1.0),
                linkThickness: clampD(d["linkThickness"], 0.5, 4.0, 1.0))
        }
        if let fo = root["forces"] as? [String: Any] {
            config.forces = GraphForcesConfig(
                center: clampD(fo["center"], 0, 1, 0.5),
                repel: clampD(fo["repel"], 0, 1, 0.5),
                link: clampD(fo["link"], 0, 1, 0.5),
                linkDistance: clampD(fo["linkDistance"], 0, 1, 0.5))
        }
        if let m = root["mode"] as? String, let mode = GraphTabMode(rawValue: m) {
            config.mode = mode
        }
        if let depth = root["connectionsDepth"] as? Int {
            config.connectionsDepth = min(3, max(1, depth))
        }
        return config
    }

    static func encode(_ c: GraphConfig, into root: inout [String: Any]) {
        root["version"] = GraphConfig.version
        root["filters"] = [
            "includeAttachments": c.filters.includeAttachments,
            "includeGhosts": c.filters.includeGhosts,
            "orphansOnly": c.filters.orphansOnly,
            "nameQuery": c.filters.nameQuery,
        ]
        root["groups"] = c.groups.map {
            ["query": $0.query, "colorToken": $0.colorToken.rawValue, "ringStyle": $0.ringStyle.rawValue]
        }
        root["display"] = [
            "arrows": c.display.arrows,
            "textFadeZoom": c.display.textFadeZoom,
            "nodeSizeMultiplier": c.display.nodeSizeMultiplier,
            "linkThickness": c.display.linkThickness,
        ]
        root["forces"] = [
            "center": c.forces.center, "repel": c.forces.repel, "link": c.forces.link,
            "linkDistance": c.forces.linkDistance,
        ]
        root["mode"] = c.mode.rawValue
        root["connectionsDepth"] = c.connectionsDepth
    }

    private static func clampD(_ any: Any?, _ lo: Double, _ hi: Double, _ fallback: Double) -> Double
    {
        guard let d = (any as? Double) ?? (any as? Int).map(Double.init), d.isFinite else {
            return fallback
        }
        return min(hi, max(lo, d))
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
