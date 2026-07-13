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
        return Self.decode(root)
    }

    /// Write the config, preserving unknown top-level keys and refusing
    /// to clobber an unparseable existing file. Atomic via temp+rename.
    func write(_ config: GraphConfig) throws {
        try ensureSlateDirExists()
        let url = configURL

        var root: [String: Any] = [:]
        if FileManager.default.fileExists(atPath: url.path) {
            let data = try? Data(contentsOf: url)
            let parsed = data.flatMap { try? JSONSerialization.jsonObject(with: $0, options: []) }
            if let dict = parsed as? [String: Any] {
                root = dict  // preserve unknown keys
            } else if data != nil {
                // The file exists but isn't a JSON object — refuse to
                // clobber it (O-5 rule); a newer schema or hand-edit
                // shouldn't be silently destroyed.
                throw PrefsJsonStoreError.writeFailed(
                    path: url.path,
                    reason: "existing graph.json is unparseable; refusing to overwrite")
            }
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
