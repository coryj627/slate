// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// Tests for `PrefsJsonStore` — the Swift-side reader/writer of
/// `.slate/prefs.json` (Milestone L #281). Symmetric with the Rust
/// parser in `slate_core::citations::prefs`; they share the same
/// schema and forward-compatibility rules.
final class PrefsJsonStoreTests: XCTestCase {

    private func tempVault() -> URL {
        let dir = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-prefs-test-\(UUID().uuidString)")
        try? FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        return dir
    }

    // MARK: - Read

    func testReadMissingFileReturnsEmptyPrefs() throws {
        let vault = tempVault()
        defer { try? FileManager.default.removeItem(at: vault) }
        let store = PrefsJsonStore(vaultRoot: vault)
        let prefs = try store.readBibliographyPrefs()
        XCTAssertEqual(prefs, .empty)
    }

    func testReadMissingBibliographyKeyReturnsEmptyPrefs() throws {
        let vault = tempVault()
        defer { try? FileManager.default.removeItem(at: vault) }
        try writePrefsJson(
            in: vault,
            contents: #"{ "ui": { "theme": "dark" } }"#
        )
        let store = PrefsJsonStore(vaultRoot: vault)
        let prefs = try store.readBibliographyPrefs()
        XCTAssertEqual(prefs, .empty)
    }

    func testReadFullBibliographySection() throws {
        let vault = tempVault()
        defer { try? FileManager.default.removeItem(at: vault) }
        try writePrefsJson(
            in: vault,
            contents: """
                {
                  "bibliography": {
                    "sources": [
                      { "path": "library.bib", "format": "BibTeX", "watch": true },
                      { "path": "extra.json", "format": "CSL-JSON" }
                    ],
                    "default_style": "styles/apa.csl",
                    "additional_styles": ["styles/chicago.csl", "styles/ieee.csl"]
                  }
                }
                """
        )
        let store = PrefsJsonStore(vaultRoot: vault)
        let prefs = try store.readBibliographyPrefs()
        XCTAssertEqual(prefs.sources.count, 2)
        XCTAssertEqual(prefs.sources[0].path, "library.bib")
        XCTAssertEqual(prefs.sources[0].format, .bibTeX)
        XCTAssertTrue(prefs.sources[0].watch)
        XCTAssertEqual(prefs.sources[1].format, .cslJson)
        XCTAssertFalse(prefs.sources[1].watch)
        XCTAssertEqual(prefs.defaultStyle, "styles/apa.csl")
        XCTAssertEqual(prefs.additionalStyles.count, 2)
    }

    func testReadMalformedJsonThrowsParseFailed() throws {
        let vault = tempVault()
        defer { try? FileManager.default.removeItem(at: vault) }
        try writePrefsJson(in: vault, contents: "{ not valid json")
        let store = PrefsJsonStore(vaultRoot: vault)
        XCTAssertThrowsError(try store.readBibliographyPrefs()) { error in
            guard case PrefsJsonStoreError.parseFailed = error else {
                XCTFail("expected parseFailed, got \(error)")
                return
            }
        }
    }

    // MARK: - Write

    func testWriteRoundTripsAllFields() throws {
        let vault = tempVault()
        defer { try? FileManager.default.removeItem(at: vault) }
        let store = PrefsJsonStore(vaultRoot: vault)
        let original = BibliographyPrefs(
            sources: [
                BibliographySource(path: "library.bib", format: .bibLaTeX, watch: true),
                BibliographySource(path: "extra.json", format: .cslJson, watch: false),
            ],
            defaultStyle: "styles/apa.csl",
            additionalStyles: ["styles/chicago.csl"]
        )
        try store.writeBibliographyPrefs(original)
        let read = try store.readBibliographyPrefs()
        XCTAssertEqual(read, original)
    }

    func testWritePreservesUnknownTopLevelKeys() throws {
        let vault = tempVault()
        defer { try? FileManager.default.removeItem(at: vault) }
        try writePrefsJson(
            in: vault,
            contents: """
                {
                  "ui": { "theme": "dark" },
                  "bibliography": { "sources": [] }
                }
                """
        )
        let store = PrefsJsonStore(vaultRoot: vault)
        let newPrefs = BibliographyPrefs(
            sources: [
                BibliographySource(path: "lib.bib", format: .bibTeX, watch: false)
            ],
            defaultStyle: nil,
            additionalStyles: []
        )
        try store.writeBibliographyPrefs(newPrefs)
        // Re-read the raw JSON: the ui key must still be present.
        let data = try Data(contentsOf: store.prefsURL)
        let any = try JSONSerialization.jsonObject(with: data)
        let root = any as! [String: Any]
        XCTAssertNotNil(root["ui"])
        let ui = root["ui"] as! [String: Any]
        XCTAssertEqual(ui["theme"] as? String, "dark")
    }

    func testWriteCreatesSlateDirectoryWhenMissing() throws {
        let vault = tempVault()
        defer { try? FileManager.default.removeItem(at: vault) }
        // No .slate dir exists yet — writer should create it.
        let store = PrefsJsonStore(vaultRoot: vault)
        let prefs = BibliographyPrefs(
            sources: [], defaultStyle: nil, additionalStyles: []
        )
        try store.writeBibliographyPrefs(prefs)
        XCTAssertTrue(FileManager.default.fileExists(atPath: store.prefsURL.path))
    }

    // MARK: - Helpers

    private func writePrefsJson(in vault: URL, contents: String) throws {
        let slate = vault.appendingPathComponent(".slate")
        try FileManager.default.createDirectory(
            at: slate, withIntermediateDirectories: true
        )
        try contents.write(
            to: slate.appendingPathComponent("prefs.json"),
            atomically: true,
            encoding: .utf8
        )
    }
}
