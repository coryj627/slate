// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// End-to-end "Milestone L shipped" coverage. One fixture vault
/// exercises the citations pipeline through `AppState` — from the
/// disk-side prefs.json read through `setBibliographySources`,
/// the citation index, the per-note rendering, and the
/// vault-wide query surface.
///
/// Closes #283. Same shape as `MilestoneIIntegrationTests` (#171),
/// `MilestoneJIntegrationTests` (#189), and
/// `MilestoneKIntegrationTests` (#225): single fixture vault,
/// single method, all assertions inline.
///
/// Scope: the CSL hayagriva-render path is covered by the
/// Rust-side `slate-core` tests (#277). Here we exercise the
/// AppState placeholder-rendering path that fires when no CSL
/// style file is on disk — sufficient to verify the data flow
/// + AT labels end-to-end without bundling .csl XML in the test
/// fixture.
///
/// Wall-clock budget: under 5 seconds on local + CI runners.
@MainActor
final class MilestoneLIntegrationTests: XCTestCase {
    private var tempDir: URL!

    override func setUpWithError() throws {
        try super.setUpWithError()
        tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-milestone-l-\(UUID().uuidString)")
        try FileManager.default.createDirectory(
            at: tempDir,
            withIntermediateDirectories: true
        )
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: tempDir)
        try super.tearDownWithError()
    }

    private func makeAppState() -> (AppState, UserDefaults, String) {
        let suiteName = "slate.milestone-l.\(UUID().uuidString)"
        let isolated = UserDefaults(suiteName: suiteName)!
        let preferences = PreferencesStore(defaults: isolated)
        let recents = RecentVaultsStore(
            fileURL: tempDir.appendingPathComponent("recents.json")
        )
        let state = AppState(
            recentsStore: recents,
            externalOpener: { _ in true },
            preferencesStore: preferences
        )
        return (state, isolated, suiteName)
    }

    func testMilestoneLEndToEndCitationsPipeline() async throws {
        let vault = tempDir.appendingPathComponent("milestone-l-integration")
        try FileManager.default.createDirectory(
            at: vault,
            withIntermediateDirectories: true
        )

        // === Fixture vault layout ===
        //
        // library.bib    — three BibTeX entries (smith2020 / jones2019
        //                  / web2024).
        // library.json   — one CSL-JSON entry (extra2022) so the
        //                  multi-source merge path runs.
        // paper.md       — every Pandoc syntax form from §6.5 plus
        //                  a fence-protected `[@notacite]` that must
        //                  NOT extract, plus an unresolved `[@missing]`.
        // notes.md       — separate note also citing smith2020, used
        //                  to verify `listFilesCiting` returns both
        //                  files.
        // .slate/prefs.json — configures both sources; no default
        //                  style (we deliberately exercise the
        //                  placeholder-render path that fires when
        //                  activeStyleId is empty, so the integration
        //                  test doesn't need a real .csl on disk).

        let bibBody = """
            @article{smith2020,
              title  = {On the Nature of Reading},
              author = {Smith, Alice},
              journal = {Journal of Knowledge},
              year   = {2020},
              doi    = {10.1234/abc},
            }

            @book{jones2019,
              title     = {A Survey of Surveys},
              author    = {Jones, Robert and Lee, Hana},
              year      = {2019},
              publisher = {Academic Press},
            }

            @online{web2024,
              title  = {A Web Resource},
              author = {Walker, Maya},
              year   = {2024},
              url    = {https://example.com/notes},
            }
            """
        try bibBody.data(using: .utf8)!
            .write(to: vault.appendingPathComponent("library.bib"))

        let cslJsonBody = """
            [
              {
                "id": "extra2022",
                "type": "article-journal",
                "title": "An Extra Source",
                "author": [{ "family": "Walker", "given": "Maya" }],
                "issued": { "date-parts": [[2022]] }
              }
            ]
            """
        try cslJsonBody.data(using: .utf8)!
            .write(to: vault.appendingPathComponent("library.json"))

        let paperBody = """
            # Citation fixture

            Bracketed: [@smith2020].
            With locator: [@smith2020, p. 23].
            Multi: [@smith2020; @jones2019].
            In-text: @smith2020 said so.
            Author-suppressed: [-@smith2020].
            Prefix: [see @smith2020, p. 23].
            Unresolved: [@missing].

            Fence-protected (must not extract):

            ```
            [@notacite]
            ```

            """
        try paperBody.data(using: .utf8)!
            .write(to: vault.appendingPathComponent("paper.md"))

        let notesBody = """
            # Notes

            Also cites [@smith2020].
            """
        try notesBody.data(using: .utf8)!
            .write(to: vault.appendingPathComponent("notes.md"))

        let prefsJson = #"""
            {
              "bibliography": {
                "sources": [
                  { "path": "library.bib", "format": "BibTeX", "watch": false },
                  { "path": "library.json", "format": "CSL-JSON", "watch": false }
                ],
                "default_style": "",
                "additional_styles": []
              }
            }
            """#
        try FileManager.default.createDirectory(
            at: vault.appendingPathComponent(".slate"),
            withIntermediateDirectories: true
        )
        try prefsJson.data(using: .utf8)!
            .write(to: vault.appendingPathComponent(".slate/prefs.json"))

        let (state, isolated, suiteName) = makeAppState()
        defer { isolated.removePersistentDomain(forName: suiteName) }
        state.openVault(at: vault)
        await state.scanTask?.value
        // Bibliography load fires via openVault when prefs has
        // sources; wait for the entry list to populate.
        for _ in 0..<50 {
            if !state.bibliographyEntries.isEmpty { break }
            try await Task.sleep(nanoseconds: 100_000_000)
        }

        // ============================================================
        // === Bibliography state ===
        // ============================================================

        XCTAssertEqual(
            state.bibliographyPrefs.sources.count, 2,
            "prefs.json's two sources must load into AppState"
        )
        XCTAssertEqual(state.bibliographyPrefs.sources[0].path, "library.bib")
        XCTAssertEqual(state.bibliographyPrefs.sources[0].format, .bibTeX)
        XCTAssertEqual(state.bibliographyPrefs.sources[1].format, .cslJson)

        // Four entries merged across the two sources: smith2020,
        // jones2019, web2024 (from .bib) + extra2022 (from .json).
        let entryKeys = Set(state.bibliographyEntries.map(\.key))
        XCTAssertTrue(
            entryKeys.contains("smith2020"),
            "smith2020 missing — got \(entryKeys)"
        )
        XCTAssertTrue(entryKeys.contains("jones2019"))
        XCTAssertTrue(entryKeys.contains("web2024"))
        XCTAssertTrue(
            entryKeys.contains("extra2022"),
            "extra2022 from CSL-JSON missing — multi-source merge failed"
        )
        XCTAssertEqual(state.bibliographyEntries.count, 4)

        // ============================================================
        // === Per-note citation index (paper.md) ===
        // ============================================================
        state.selectedFilePath = "paper.md"
        await state.noteLoadTask?.value
        await state.citationsLoadTask?.value

        // Seven citation sites from the fixture (one per Pandoc
        // form). The fence-protected `[@notacite]` is invisible to
        // the extractor — an eighth here would mean fence protection
        // regressed.
        XCTAssertEqual(
            state.currentNoteCitations.count, 7,
            "expected 7 citations from paper.md; got "
                + "\(state.currentNoteCitations.map(\.raw))"
        )

        // Speech form for the bracketed-single case: per §6.5, the
        // placeholder renderer (no CSL style) emits
        // "Citation: <key>". The first row is `[@smith2020]`.
        let firstSpeech = state.currentNoteCitations[0].speechText
        XCTAssertTrue(
            firstSpeech.hasPrefix("Citation:"),
            "bracketed citation speech form must start with 'Citation:'; "
                + "got '\(firstSpeech)'"
        )
        // None of the speech forms contain parens or square brackets
        // — the entire point of the differentiator. Test this
        // invariant across the whole render output.
        for rendered in state.currentNoteCitations {
            let speech = rendered.speechText
            XCTAssertFalse(
                speech.contains("("),
                "speech_text must never contain '(' — got '\(speech)'"
            )
            XCTAssertFalse(
                speech.contains("["),
                "speech_text must never contain '[' — got '\(speech)'"
            )
        }

        // In-text mode skips the "Citation: " prefix. The fourth
        // entry in paper.md is `@smith2020 said so.` — find it by
        // raw form.
        let inTextRendered = state.currentNoteCitations.first {
            $0.raw == "@smith2020"
        }
        XCTAssertNotNil(inTextRendered, "couldn't find in-text @smith2020 row")
        if let inText = inTextRendered {
            XCTAssertFalse(
                inText.speechText.hasPrefix("Citation:"),
                "in-text mode must drop the 'Citation:' prefix; "
                    + "got '\(inText.speechText)'"
            )
        }

        // ============================================================
        // === list_files_citing — both files cite smith2020 ===
        // ============================================================
        let citing = try state.currentSession!.listFilesCiting(
            citationKey: "smith2020"
        )
        XCTAssertEqual(
            Set(citing), Set(["notes.md", "paper.md"]),
            "smith2020 is cited from both files"
        )

        // ============================================================
        // === Unresolved citations ===
        // ============================================================
        XCTAssertEqual(
            state.unresolvedCitations.count, 1,
            "expected one unresolved citation (`missing`); got "
                + "\(state.unresolvedCitations.map(\.key))"
        )
        XCTAssertEqual(state.unresolvedCitations[0].path, "paper.md")
        XCTAssertEqual(state.unresolvedCitations[0].key, "missing")

        // ============================================================
        // === Search ===
        // ============================================================
        let titleHits = try state.currentSession!.searchBibliography(
            query: "Reading"
        )
        XCTAssertEqual(titleHits.count, 1)
        XCTAssertEqual(titleHits[0].key, "smith2020")

        let authorHits = try state.currentSession!.searchBibliography(
            query: "Jones"
        )
        XCTAssertEqual(authorHits.count, 1)
        XCTAssertEqual(authorHits[0].key, "jones2019")

        // ============================================================
        // === Filter helper used by BibliographyPanel ===
        // ============================================================
        state.bibliographySearchText = "walker"
        let filtered = state.filteredBibliographyEntries()
        XCTAssertEqual(
            filtered.count, 2,
            "Walker appears in web2024 (.bib) and extra2022 (.json)"
        )

        // ============================================================
        // === Citation summary helpers ===
        // ============================================================
        // The summary sheet counts citations + unique sources. With
        // our 7 paper.md citations, the unique keys are:
        // smith2020, jones2019, missing → 3 sources. The
        // multi-citation site `[@smith2020; @jones2019]` contributes
        // both keys via the structured `currentNoteCitationRefs`.
        var keys = Set<String>()
        for ref in state.currentNoteCitationRefs {
            for item in ref.citations {
                keys.insert(item.key)
            }
        }
        XCTAssertEqual(
            keys.count, 3,
            "expected 3 unique sources in paper.md; got \(keys)"
        )

        // ============================================================
        // === Jump-to-Bibliography wiring ===
        // ============================================================
        if let first = state.currentNoteCitations.first(where: {
            $0.bibEntry != nil
        }) {
            state.expandedCitation = first
            state.jumpToBibliographyFromExpandedCitation()
            XCTAssertNotNil(state.pendingBibliographyKeyFocus)
            XCTAssertEqual(state.expandedCitation, nil)
        }

        // ============================================================
        // === Style-switching invariant ===
        // ============================================================
        // With no CSL files configured, both empty and any-name
        // styleId hit the placeholder render. Speech form is
        // identical across switches because it's built from
        // structured data, not from the visual rendering.
        state.activeStyleId = "apa"
        await state.citationsLoadTask?.value
        let apaSpeech = state.currentNoteCitations.first?.speechText
        state.activeStyleId = "chicago"
        await state.citationsLoadTask?.value
        let chicagoSpeech = state.currentNoteCitations.first?.speechText
        XCTAssertEqual(
            apaSpeech, chicagoSpeech,
            "speech_text must be invariant across style switches "
                + "— it's built from structured data"
        )
    }
}
