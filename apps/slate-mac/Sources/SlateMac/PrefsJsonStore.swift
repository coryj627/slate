// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import Foundation

/// `.slate/prefs.json` reader + atomic writer.
///
/// Per-vault config file introduced by Milestone L (#278 on the Rust
/// side defines the schema; this helper persists changes from the
/// Settings UI). Symmetric with the Rust parser in
/// `slate_core::citations::prefs` — same field names, same forward-
/// compat rule (unknown top-level keys preserved verbatim on write).
///
/// Storage shape:
/// ```json
/// {
///   "bibliography": {
///     "sources": [
///       { "path": "library.bib", "format": "BibTeX", "watch": true }
///     ],
///     "default_style": "styles/apa-7th.csl",
///     "additional_styles": ["styles/chicago.csl", "styles/ieee.csl"]
///   }
/// }
/// ```
///
/// Writes go through a temp-file + rename so a kill mid-write never
/// leaves the on-disk file half-populated.
struct PrefsJsonStore {
    /// Vault root — `prefs.json` lives at `<root>/.slate/prefs.json`.
    let vaultRoot: URL

    /// Read the bibliography section. Missing file or missing
    /// section returns `.empty`. Throws `PrefsJsonStoreError` on
    /// malformed JSON — callers surface this to the user.
    func readBibliographyPrefs() throws -> BibliographyPrefs {
        let url = prefsURL
        guard FileManager.default.fileExists(atPath: url.path) else {
            return .empty
        }
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
                path: url.path,
                reason: "expected a JSON object at the top level"
            )
        }
        guard let bibAny = root["bibliography"] else {
            return .empty
        }
        guard let bib = bibAny as? [String: Any] else {
            throw PrefsJsonStoreError.parseFailed(
                path: url.path,
                reason: "`bibliography` must be a JSON object"
            )
        }
        var sources: [BibliographySource] = []
        if let arr = bib["sources"] as? [[String: Any]] {
            for raw in arr {
                if let source = parseSource(raw) {
                    sources.append(source)
                }
            }
        }
        let defaultStyle = bib["default_style"] as? String
        let additionalStyles = (bib["additional_styles"] as? [String]) ?? []
        return BibliographyPrefs(
            sources: sources,
            defaultStyle: defaultStyle,
            additionalStyles: additionalStyles
        )
    }

    /// Write the bibliography section. Preserves any unknown top-
    /// level keys from the existing file so prefs added by future
    /// Slate versions aren't dropped. Atomic via temp-file rename.
    func writeBibliographyPrefs(_ prefs: BibliographyPrefs) throws {
        try ensureSlateDirExists()
        let url = prefsURL

        // Read existing top-level object to preserve unrelated keys.
        var root: [String: Any] = [:]
        if FileManager.default.fileExists(atPath: url.path) {
            if let data = try? Data(contentsOf: url),
                let parsed = try? JSONSerialization.jsonObject(with: data, options: []),
                let dict = parsed as? [String: Any]
            {
                root = dict
            }
        }

        root["bibliography"] = encodeBibliographySection(prefs)

        let data: Data
        do {
            data = try JSONSerialization.data(
                withJSONObject: root,
                options: [.prettyPrinted, .sortedKeys]
            )
        } catch {
            throw PrefsJsonStoreError.writeFailed(
                path: url.path,
                reason: "JSON encoding failed: \(error.localizedDescription)"
            )
        }

        // Atomic temp + rename. `Data.write(to:options:.atomic)` does
        // exactly this on POSIX, so a kill mid-write never leaves the
        // file half-populated.
        do {
            try data.write(to: url, options: .atomic)
        } catch {
            throw PrefsJsonStoreError.writeFailed(
                path: url.path,
                reason: error.localizedDescription
            )
        }
    }

    // MARK: - Internals

    var prefsURL: URL {
        vaultRoot.appendingPathComponent(".slate", isDirectory: true)
            .appendingPathComponent("prefs.json", isDirectory: false)
    }

    private func ensureSlateDirExists() throws {
        let slateDir = vaultRoot.appendingPathComponent(".slate", isDirectory: true)
        if FileManager.default.fileExists(atPath: slateDir.path) {
            return
        }
        do {
            try FileManager.default.createDirectory(
                at: slateDir,
                withIntermediateDirectories: true
            )
        } catch {
            throw PrefsJsonStoreError.writeFailed(
                path: slateDir.path,
                reason: error.localizedDescription
            )
        }
    }

    private func parseSource(_ obj: [String: Any]) -> BibliographySource? {
        guard let path = obj["path"] as? String else { return nil }
        let formatRaw = (obj["format"] as? String) ?? "BibTeX"
        let format: BibFormat
        switch formatRaw {
        case "BibLaTeX", "biblatex": format = .bibLaTeX
        case "CslJson", "csl-json", "CSL-JSON": format = .cslJson
        default: format = .bibTeX
        }
        let watch = (obj["watch"] as? Bool) ?? false
        return BibliographySource(path: path, format: format, watch: watch)
    }

    private func encodeBibliographySection(_ prefs: BibliographyPrefs) -> [String: Any] {
        var bib: [String: Any] = [:]
        bib["sources"] = prefs.sources.map { source in
            var s: [String: Any] = [
                "path": source.path,
                "format": formatString(source.format),
                "watch": source.watch,
            ]
            // For deterministic output ordering (used in tests).
            _ = s.removeValue(forKey: "_placeholder")
            return s
        }
        if let defaultStyle = prefs.defaultStyle {
            bib["default_style"] = defaultStyle
        }
        bib["additional_styles"] = prefs.additionalStyles
        return bib
    }

    private func formatString(_ format: BibFormat) -> String {
        switch format {
        case .bibTeX: return "BibTeX"
        case .bibLaTeX: return "BibLaTeX"
        case .cslJson: return "CSL-JSON"
        }
    }
}

/// Swift-side mirror of `slate_core::citations::prefs::CitationsPrefs`.
/// The FFI doesn't surface the Rust struct directly (the bibliography
/// settings UI lives entirely on the Swift side); this is its
/// in-memory shape.
struct BibliographyPrefs: Equatable {
    var sources: [BibliographySource]
    var defaultStyle: String?
    var additionalStyles: [String]

    static let empty = BibliographyPrefs(
        sources: [],
        defaultStyle: nil,
        additionalStyles: []
    )
}

enum PrefsJsonStoreError: Error, LocalizedError, Equatable {
    case readFailed(path: String, reason: String)
    case parseFailed(path: String, reason: String)
    case writeFailed(path: String, reason: String)

    var errorDescription: String? {
        switch self {
        case .readFailed(let path, let reason):
            return "Couldn't read preferences at \(path): \(reason)"
        case .parseFailed(let path, let reason):
            return "Preferences at \(path) couldn't be parsed: \(reason)"
        case .writeFailed(let path, let reason):
            return "Couldn't write preferences at \(path): \(reason)"
        }
    }
}
