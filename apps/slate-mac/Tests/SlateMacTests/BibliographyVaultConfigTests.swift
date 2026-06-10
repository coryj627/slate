// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// #411 end-to-end at the app layer: a vault shipping its citation
/// config at the root as `slate.json` (the demo-vault contract) must
/// produce a WORKING bibliography after open — sources pushed into
/// the session's index, Settings surface populated, default style
/// active — without the app writing `.slate/prefs.json` (vault
/// config must not be frozen into the app-written file by merely
/// opening the vault).
///
/// This is the layer the red-team audit caught missing: the Rust
/// core merged the config but it stayed passive; nothing on the app
/// side pushed the sources, so the demo vault still showed zero
/// resolved citations.
@MainActor
final class BibliographyVaultConfigTests: XCTestCase {
    private var tempDir: URL!

    override func setUpWithError() throws {
        try super.setUpWithError()
        tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-bib-vaultcfg-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: tempDir, withIntermediateDirectories: true)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: tempDir)
        try super.tearDownWithError()
    }

    private static let bibFixture = """
        @article{smith2020,
          title = {On the Nature of Reading},
          author = {Smith, Alice},
          journal = {Journal of Knowledge},
          year = {2020},
        }

        @book{jones2019,
          title = {A Survey of Surveys},
          author = {Jones, Robert and Lee, Hana},
          year = {2019},
          publisher = {Academic Press},
        }
        """

    func testVaultShippedSlateJsonProducesWorkingBibliographyWithoutWritingPrefs()
        async throws
    {
        let vault = tempDir.appendingPathComponent("vault")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        try Data(Self.bibFixture.utf8).write(to: vault.appendingPathComponent("library.bib"))
        try Data(
            """
            {
              "citations": {
                "bibliography": "library.bib",
                "cite_style": "ieee",
                "available_styles": ["ieee", "apa"],
                "csl_directory": "csl"
              }
            }
            """.utf8
        ).write(to: vault.appendingPathComponent("slate.json"))
        try Data("# Note\n\nSee [@smith2020].\n".utf8)
            .write(to: vault.appendingPathComponent("note.md"))

        let store = RecentVaultsStore(
            fileURL: tempDir.appendingPathComponent("recents.json")
        )
        let state = AppState(recentsStore: store, externalOpener: { _ in true })
        state.openVault(at: vault)
        await state.scanTask?.value
        // openVault fires adoptSessionCitationsConfig on a detached
        // Task; drive it directly here for determinism (idempotent).
        await state.adoptSessionCitationsConfig()

        // Settings surface reflects the vault-shipped config.
        XCTAssertEqual(state.bibliographyPrefs.sources.map(\.path), ["library.bib"])
        XCTAssertEqual(state.bibliographyPrefs.defaultStyle, "csl/ieee.csl")
        XCTAssertEqual(state.activeStyleId, "ieee")

        // The bibliography index actually loaded — the user-visible
        // symptom #411 was filed against.
        XCTAssertEqual(
            state.bibliographyEntries.count, 2,
            "library.bib entries must be indexed after open"
        )

        // prefs.json must NOT have been created — the vault file
        // stays the live source of truth until the user edits in
        // Settings.
        XCTAssertFalse(
            FileManager.default.fileExists(
                atPath: vault.appendingPathComponent(".slate/prefs.json").path
            ),
            "adopting vault config must not write .slate/prefs.json"
        )
    }

    func testExplicitEmptyPrefsJsonStillMasksVaultConfigAtAppLayer() async throws {
        let vault = tempDir.appendingPathComponent("vault-masked")
        try FileManager.default.createDirectory(
            at: vault.appendingPathComponent(".slate"), withIntermediateDirectories: true
        )
        try Data(Self.bibFixture.utf8).write(to: vault.appendingPathComponent("refs.bib"))
        try Data(
            #"{ "citations": { "bibliography": "refs.bib" } }"#.utf8
        ).write(to: vault.appendingPathComponent("slate.json"))
        // Explicit empty bibliography in the app-written prefs: the
        // user removed all sources; the vault must not resurrect
        // them.
        try Data(
            #"{ "bibliography": { "sources": [] } }"#.utf8
        ).write(to: vault.appendingPathComponent(".slate/prefs.json"))

        let store = RecentVaultsStore(
            fileURL: tempDir.appendingPathComponent("recents2.json")
        )
        let state = AppState(recentsStore: store, externalOpener: { _ in true })
        state.openVault(at: vault)
        await state.scanTask?.value
        await state.adoptSessionCitationsConfig()

        XCTAssertTrue(
            state.bibliographyPrefs.sources.isEmpty,
            "explicit-empty prefs.json bibliography must mask the vault config end-to-end"
        )
        XCTAssertTrue(state.bibliographyEntries.isEmpty)
    }
}
