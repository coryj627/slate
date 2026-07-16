// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// N3-5 (#706): embedded Bases share one renderer across wikilink embeds
/// and `base` / `slate-query` / `dataview` fences. These tests stay at the
/// document/request layer so they can prove `this` routing and failure
/// honesty without depending on SwiftUI snapshots.
@MainActor
final class BaseEmbedTests: XCTestCase {
    private var tempDir: URL!
    private var vaultURL: URL!

    private static var projectRoot: URL {
        URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
    }

    private static func sourceFile(_ relativePath: String) throws -> String {
        try String(
            contentsOf: projectRoot.appendingPathComponent(relativePath),
            encoding: .utf8)
    }

    override func setUpWithError() throws {
        try super.setUpWithError()
        tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-base-embeds-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: tempDir, withIntermediateDirectories: true)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: tempDir)
        try super.tearDownWithError()
    }

    private func makeAppState() async throws -> AppState {
        let vault = tempDir.appendingPathComponent("vault-\(UUID().uuidString)")
        vaultURL = vault
        try FileManager.default.createDirectory(
            at: vault.appendingPathComponent("Queries"), withIntermediateDirectories: true)
        try FileManager.default.createDirectory(
            at: vault.appendingPathComponent("Notes"), withIntermediateDirectories: true)
        try Data(
            #"""
            formulas:
              host: "this.file.name"
            views:
              - type: table
                name: First
                filters: "file.inFolder(\"Notes\")"
                order:
                  - file.name
              - type: table
                name: This Check
                filters: "file.inFolder(\"Notes\")"
                order:
                  - file.name
                  - formula.host
            """#.utf8
        ).write(to: vault.appendingPathComponent("Queries/Reading.base"))
        try Data("---\nstatus: host\n---\n# Host\n".utf8)
            .write(to: vault.appendingPathComponent("Notes/Host.md"))
        try Data("---\nstatus: other\n---\n# Other\n".utf8)
            .write(to: vault.appendingPathComponent("Notes/Other.md"))

        let store = RecentVaultsStore(
            fileURL: tempDir.appendingPathComponent("recents-\(UUID().uuidString).json"))
        let state = AppState(recentsStore: store, externalOpener: { _ in true })
        state.openVault(at: vault)
        await state.scanTask?.value
        return state
    }

    private var savedQueryJSON: String {
        #"""
        {
          "source": { "Folder": "Notes" },
          "row_source": "Files",
          "filters": null,
          "formulas": [],
          "custom_summaries": [],
          "group_by": null,
          "sort": [],
          "columns": [
            { "id": "file.name", "display_name": null }
          ],
          "summaries": [],
          "limit": null,
          "view": { "Table": { "fallback_from": null } }
        }
        """#
    }

    func testRequestParsesWikilinkAndFenceForms() {
        let wikilink = BaseEmbedRequest.wikilinkTarget("Queries/Reading.base#This Check")
        XCTAssertEqual(wikilink?.targetPath, "Queries/Reading.base")
        XCTAssertEqual(wikilink?.viewName, "This Check")
        XCTAssertEqual(
            wikilink?.accessibilityLabel,
            "Embedded base: Reading, view This Check")

        XCTAssertNil(
            BaseEmbedRequest.wikilinkTarget("Notes/Host.md"),
            "ordinary note embeds must keep the existing EmbedView path")

        let baseFence = BaseEmbedRequest.codeFence(
            language: "base",
            source: "```base\nviews:\n  - type: table\n```")
        XCTAssertEqual(baseFence?.kind, .inlineBase)
        XCTAssertTrue(baseFence?.inlineSource.contains("views:") == true)

        let slateQuery = BaseEmbedRequest.codeFence(
            language: "slate-query",
            source: "```slate-query\nquery: Saved Notes\nview: Main\n```")
        XCTAssertEqual(slateQuery?.kind, .savedQuery)
        XCTAssertEqual(slateQuery?.savedQueryReference, "Saved Notes")
        XCTAssertEqual(slateQuery?.viewName, "Main")

        let inlineSlateQuery = BaseEmbedRequest.codeFence(
            language: "slate-query",
            source: "```slate-query\nviews:\n  - type: table\n```")
        XCTAssertEqual(inlineSlateQuery?.kind, .inlineBase)

        let dataview = BaseEmbedRequest.codeFence(
            language: "dataview",
            source: "```dataview\nTABLE file.name\n```")
        XCTAssertEqual(dataview?.kind, .dataview)
        XCTAssertTrue(dataview?.inlineSource.contains("TABLE file.name") == true)

        XCTAssertNil(
            BaseEmbedRequest.codeFence(
                language: "dataviewjs",
                source: "```dataviewjs\nconsole.log('no renderer')\n```"),
            "DataviewJS stays an ordinary code block")

        let previews = BaseEmbedRequest.previews(
            in: "Intro\n\n![[Queries/Reading.base]]\n\n```dataview\nTABLE file.name\n```\n")
        XCTAssertEqual(previews.map(\.sourceLine), [3, 5])
        XCTAssertEqual(previews.map(\.request.kind), [.file, .dataview])
    }

    func testEmbedIdentityKeepsCanonicallyEquivalentRequestsDistinct() throws {
        let composed = "é"
        let decomposed = "e\u{301}"
        let composedRequest = try XCTUnwrap(
            BaseEmbedRequest.codeFence(
                language: "slate-query",
                source: "```slate-query\nquery: \(composed)\n```"))
        let decomposedRequest = try XCTUnwrap(
            BaseEmbedRequest.codeFence(
                language: "slate-query",
                source: "```slate-query\nquery: \(decomposed)\n```"))
        let composedKey = BaseEmbedCacheKey(
            request: composedRequest, thisPath: "Notes/\(composed).md")
        let decomposedKey = BaseEmbedCacheKey(
            request: decomposedRequest, thisPath: "Notes/\(decomposed).md")

        XCTAssertNotEqual(composedRequest.cacheKey, decomposedRequest.cacheKey)
        XCTAssertNotEqual(composedKey, decomposedKey)
        XCTAssertNotEqual(composedKey.exactIdentityKey, decomposedKey.exactIdentityKey)
    }

    func testSlateQueryReferenceUsesFullYAMLScalarParsing() throws {
        let cases: [(body: String, query: String, view: String?)] = [
            ("{query: Saved Notes, view: Main}", "Saved Notes", "Main"),
            (#"query: "Saved Notes" # authored comment"#, "Saved Notes", nil),
            ("query: 'Saved ''Notes'''", "Saved 'Notes'", nil),
            ("query: |-\n  Saved Notes", "Saved Notes", nil),
        ]

        for testCase in cases {
            let request = try XCTUnwrap(
                BaseEmbedRequest.codeFence(
                    language: "slate-query",
                    source: "```slate-query\n\(testCase.body)\n```"))
            XCTAssertEqual(request.kind, .savedQuery, testCase.body)
            XCTAssertEqual(request.savedQueryReference, testCase.query, testCase.body)
            XCTAssertEqual(request.viewName, testCase.view, testCase.body)
        }
    }

    func testFenceBodiesPreserveYAMLChompingAndInlineBaseBoundaryBytes() throws {
        let scalarBodies: [(body: String, expected: String)] = [
            ("query: |\n  Saved Notes\n", "Saved Notes\n"),
            ("query: |+\n  Saved Notes\n\n", "Saved Notes\n\n"),
        ]
        for testCase in scalarBodies {
            let direct = try classifySlateQueryFence(source: testCase.body)
            let request = try XCTUnwrap(
                BaseEmbedRequest.codeFence(
                    language: "slate-query",
                    source: "```slate-query\n\(testCase.body)```"))
            XCTAssertEqual(direct.query, testCase.expected)
            XCTAssertEqual(request.savedQueryReference, direct.query)
        }

        let inlineBody = "views:\n  - type: table\n"
        let inline = try XCTUnwrap(
            BaseEmbedRequest.codeFence(
                language: "base", source: "```base\n\(inlineBody)```"))
        XCTAssertTrue(BaseExactIdentity.matches(inline.inlineSource, inlineBody))
    }

    func testInvalidSlateQueryReferenceFailsLoudInsteadOfBecomingInlineBase() async throws {
        let state = try await makeAppState()
        let session = try XCTUnwrap(state.currentSession)
        let request = try XCTUnwrap(
            BaseEmbedRequest.codeFence(
                language: "slate-query",
                source: "```slate-query\nquery: [not, scalar]\n```"))
        let document = BaseEmbedDocument(request: request, thisPath: "Notes/Host.md")

        document.load(session: session)

        guard case .failed(let message) = document.state else {
            return XCTFail("invalid saved-query reference must fail, got \(document.state)")
        }
        XCTAssertTrue(message.contains("query must be a scalar"), message)
    }

    func testFileEmbedSelectsNamedViewAndUsesEmbeddingNoteForThis() async throws {
        let state = try await makeAppState()
        let session = try XCTUnwrap(state.currentSession)
        let request = try XCTUnwrap(
            BaseEmbedRequest.wikilinkTarget("Queries/Reading.base#This Check"))
        let doc = BaseEmbedDocument(request: request, thisPath: "Notes/Host.md")

        doc.load(session: session)

        XCTAssertEqual(doc.state, .ready)
        XCTAssertEqual(doc.activeViewName, "This Check")
        XCTAssertEqual(doc.result?.columns.map(\.label), ["file.name", "formula.host"])
        XCTAssertEqual(
            Set(doc.result?.rows.map { $0.values[1].display } ?? []),
            ["Host.md"],
            "embedded .base files must resolve `this` to the embedding note")
        XCTAssertEqual(
            doc.cellEditingAccessibilityHint,
            "read-only in embeds — open the base file in a tab to edit")
    }

    func testInlineBaseAndDataviewFencesExecuteWithEmbeddingThis() async throws {
        let state = try await makeAppState()
        let session = try XCTUnwrap(state.currentSession)
        let inline = try XCTUnwrap(
            BaseEmbedRequest.codeFence(
                language: "base",
                source:
                    #"""
                    ```base
                    formulas:
                      host: "this.file.name"
                    views:
                      - type: table
                        name: Inline
                        filters: "file.inFolder(\"Notes\")"
                        order:
                          - file.name
                          - formula.host
                    ```
                    """#))
        let inlineDoc = BaseEmbedDocument(request: inline, thisPath: "Notes/Host.md")

        inlineDoc.load(session: session)

        XCTAssertEqual(inlineDoc.state, .ready)
        XCTAssertEqual(inlineDoc.activeViewName, "Inline")
        XCTAssertEqual(
            Set(inlineDoc.result?.rows.map { $0.values[1].display } ?? []),
            ["Host.md"])

        let dql = try XCTUnwrap(
            BaseEmbedRequest.codeFence(
                language: "dataview",
                source:
                    #"""
                    ```dataview
                    TABLE WITHOUT ID file.name AS "Name"
                    FROM "Notes"
                    ```
                    """#))
        let dqlDoc = BaseEmbedDocument(request: dql, thisPath: "Notes/Host.md")

        dqlDoc.load(session: session)

        XCTAssertEqual(dqlDoc.state, .ready)
        XCTAssertEqual(dqlDoc.result?.columns.map(\.label), ["Name"])
        XCTAssertEqual(
            dqlDoc.result?.rows.map { $0.values[0].display },
            ["Host", "Other"])
    }

    func testSavedQueryFenceResolvesByNameAndUnknownListsAvailableQueries() async throws {
        let state = try await makeAppState()
        let session = try XCTUnwrap(state.currentSession)
        let id = try session.saveQuery(
            name: "Saved Notes",
            description: nil,
            queryJson: savedQueryJSON,
            sourceSyntax: .builder)

        let request = try XCTUnwrap(
            BaseEmbedRequest.codeFence(
                language: "slate-query",
                source: "```slate-query\nquery: Saved Notes\n```"))
        let doc = BaseEmbedDocument(request: request, thisPath: "Notes/Host.md")

        doc.load(session: session)

        XCTAssertEqual(id.count, 36)
        XCTAssertEqual(doc.state, .ready)
        XCTAssertEqual(doc.result?.rows.map(\.filePath), ["Notes/Host.md", "Notes/Other.md"])

        let missing = try XCTUnwrap(
            BaseEmbedRequest.codeFence(
                language: "slate-query",
                source: "```slate-query\nquery: Missing\n```"))
        let missingDoc = BaseEmbedDocument(request: missing, thisPath: "Notes/Host.md")

        missingDoc.load(session: session)

        XCTAssertEqual(
            missingDoc.state,
            .failed("Unknown saved query Missing. Available saved queries: Saved Notes."))
    }

    func testSavedQueryFenceUsesDisplayViewOverride() async throws {
        let state = try await makeAppState()
        let session = try XCTUnwrap(state.currentSession)
        _ = try session.saveQuery(
            name: "Saved Notes",
            description: nil,
            queryJson: savedQueryJSON,
            sourceSyntax: .builder)
        let request = try XCTUnwrap(
            BaseEmbedRequest.codeFence(
                language: "slate-query",
                source: "```slate-query\nquery: Saved Notes\nview: Main\n```"))
        let doc = BaseEmbedDocument(request: request, thisPath: "Notes/Host.md")

        doc.load(session: session)

        XCTAssertEqual(doc.state, .ready)
        XCTAssertEqual(doc.activeViewName, "Main")
        XCTAssertEqual(doc.result?.rows.map(\.filePath), ["Notes/Host.md", "Notes/Other.md"])
    }

    func testViewSwitchClearsQuickFilterBeforeExecutingNewView() async throws {
        let state = try await makeAppState()
        let session = try XCTUnwrap(state.currentSession)
        let request = try XCTUnwrap(BaseEmbedRequest.wikilinkTarget("Queries/Reading.base"))
        let doc = BaseEmbedDocument(request: request, thisPath: "Notes/Host.md")
        doc.load(session: session)
        _ = doc.applyQuickFilter("Other", session: session)
        XCTAssertEqual(doc.result?.rows.map(\.filePath), ["Notes/Other.md"])

        doc.selectView(index: 1, session: session)

        XCTAssertEqual(doc.activeViewName, "This Check")
        XCTAssertEqual(doc.quickFilterText, "")
        XCTAssertFalse(doc.quickFilterActive)
        XCTAssertEqual(
            doc.result?.rows.map(\.filePath),
            ["Notes/Host.md", "Notes/Other.md"],
            "view switching must execute the destination view without the previous view's filter")
    }

    func testEscapeClearRemovesEmbedQuickFilterAndRestoresUnfilteredResult() async throws {
        let state = try await makeAppState()
        let session = try XCTUnwrap(state.currentSession)
        let request = try XCTUnwrap(BaseEmbedRequest.wikilinkTarget("Queries/Reading.base"))
        let doc = BaseEmbedDocument(request: request, thisPath: "Notes/Host.md")
        doc.load(session: session)
        _ = doc.applyQuickFilter("Other", session: session)
        doc.quickFilterText = ""
        XCTAssertEqual(
            doc.result?.rows.map(\.filePath),
            ["Notes/Other.md"],
            "typing the field empty precedes the debounced unfiltered execution")

        let announcement = doc.clearQuickFilter(session: session)

        XCTAssertEqual(doc.quickFilterText, "")
        XCTAssertEqual(doc.result?.rows.map(\.filePath), ["Notes/Host.md", "Notes/Other.md"])
        XCTAssertEqual(announcement, "2 of 2 results")
    }

    func testRequestedViewUsesByteExactNameIdentity() async throws {
        let state = try await makeAppState()
        let session = try XCTUnwrap(state.currentSession)
        let composed = "é"
        let decomposed = "e\u{301}"
        let source = """
        views:
          - type: table
            name: "\(composed)"
            order: [file.name]
          - type: table
            name: "\(decomposed)"
            order: [file.name]
        """
        try source.write(
            to: try XCTUnwrap(vaultURL).appendingPathComponent("Queries/Reading.base"),
            atomically: true,
            encoding: .utf8)
        let request = try XCTUnwrap(
            BaseEmbedRequest.wikilinkTarget("Queries/Reading.base#\(decomposed)"))
        let document = BaseEmbedDocument(request: request, thisPath: "Notes/Host.md")

        document.load(session: session)

        XCTAssertEqual(document.views.count, 2)
        XCTAssertEqual(document.activeViewIndex, 1)
        XCTAssertTrue(BaseExactIdentity.matches(try XCTUnwrap(document.activeViewName), decomposed))
    }

    func testSavedQueryReferenceUsesByteExactNameAndGloballyPrioritizesID() async throws {
        let state = try await makeAppState()
        let session = try XCTUnwrap(state.currentSession)
        let decomposedName = "e\u{301}"
        let composedName = "é"
        _ = try session.saveQuery(
            name: decomposedName,
            description: nil,
            queryJson: savedQueryJSON,
            sourceSyntax: .builder)
        let composedID = try session.saveQuery(
            name: composedName,
            description: nil,
            queryJson: savedQueryJSON,
            sourceSyntax: .builder)
        let nameRequest = try XCTUnwrap(
            BaseEmbedRequest.codeFence(
                language: "slate-query",
                source: "```slate-query\nquery: \(composedName)\n```"))
        let nameDocument = BaseEmbedDocument(request: nameRequest, thisPath: "Notes/Host.md")

        nameDocument.load(session: session)

        XCTAssertEqual(
            nameDocument.recoveryAction?.destination,
            .savedQuery(reference: composedID),
            "canonically equivalent saved-query names are distinct SQLite identities")

        let targetID = try session.saveQuery(
            name: "zz ID target",
            description: nil,
            queryJson: savedQueryJSON,
            sourceSyntax: .builder)
        _ = try session.saveQuery(
            name: targetID,
            description: nil,
            queryJson: savedQueryJSON,
            sourceSyntax: .builder)
        let idRequest = try XCTUnwrap(
            BaseEmbedRequest.codeFence(
                language: "slate-query",
                source: "```slate-query\nquery: \(targetID)\n```"))
        let idDocument = BaseEmbedDocument(request: idRequest, thisPath: "Notes/Host.md")

        idDocument.load(session: session)

        XCTAssertEqual(
            idDocument.recoveryAction?.destination,
            .savedQuery(reference: targetID),
            "an exact stable ID must win globally before any user-chosen name")
        _ = await state.refreshBaseQueries()?.value
        state.openBaseEmbedDestination(try XCTUnwrap(idDocument.recoveryAction?.destination))
        XCTAssertEqual(state.workspace.activeTab?.item, .savedQuery(id: targetID, name: "zz ID target"))
    }

    func testEscapeClearRestoresPreFilterSelectionAndGridFocus() throws {
        var selection = BaseEmbedQuickFilterSelectionState()
        selection.beginIfNeeded(currentRowID: "Notes/Host.md")

        XCTAssertEqual(selection.preferredRowID(currentRowID: nil), "Notes/Host.md")
        XCTAssertEqual(
            BaseSelectionRestorer.restoredSelection(
                previous: selection.preferredRowID(currentRowID: nil),
                availableIDs: ["Notes/Other.md"]),
            "Notes/Other.md",
            "a hidden pre-filter row falls back while the filter remains active")
        XCTAssertEqual(selection.finish(currentRowID: "Notes/Other.md"), "Notes/Host.md")
        XCTAssertFalse(selection.isActive)

        let source = try Self.sourceFile(
            "apps/slate-mac/Sources/SlateMac/Bases/BaseEmbedView.swift")
        XCTAssertTrue(source.contains("focusRequest: resultFocusToken"))
        XCTAssertTrue(source.contains("resultFocusToken &+= 1"))
    }

    func testDebouncedEmptyFilterFinishesSelectionCycleBeforeNextFilter() {
        var selection = BaseEmbedQuickFilterSelectionState()
        selection.beginIfNeeded(currentRowID: "Notes/Alpha.md")

        selection.finishAfterApplying(filterText: "  \n")
        selection.beginIfNeeded(currentRowID: "Notes/Beta.md")

        XCTAssertTrue(selection.isActive)
        XCTAssertEqual(
            selection.preferredRowID(currentRowID: nil),
            "Notes/Beta.md",
            "clearing by typing to empty must not reuse the prior filter's row anchor")
    }

    func testSavedQueryEmbedExposesOpenInTabDestination() async throws {
        let state = try await makeAppState()
        let session = try XCTUnwrap(state.currentSession)
        let savedID = try session.saveQuery(
            name: "Saved Notes",
            description: nil,
            queryJson: savedQueryJSON,
            sourceSyntax: .builder)
        _ = await state.refreshBaseQueries()?.value
        let request = try XCTUnwrap(
            BaseEmbedRequest.codeFence(
                language: "slate-query",
                source: "```slate-query\nquery: Saved Notes\n```"))

        XCTAssertEqual(
            request.recoveryAction(thisPath: "Notes/Host.md"),
            BaseEmbedRecoveryAction(
                title: "Open saved query in tab",
                destination: .savedQuery(reference: "Saved Notes"),
                accessibilityHint: "Opens the saved query in a tab where its query can be edited."))
        XCTAssertEqual(
            request.readOnlyHint(thisPath: "Notes/Host.md"),
            "read-only in embeds — open the saved query in a tab to edit")

        let document = BaseEmbedDocument(request: request, thisPath: "Notes/Host.md")
        document.load(session: session)
        XCTAssertEqual(document.recoveryAction?.destination, .savedQuery(reference: savedID))

        state.openBaseEmbedDestination(
            try XCTUnwrap(document.recoveryAction?.destination))
        XCTAssertEqual(
            state.workspace.activeTab?.item,
            .savedQuery(id: savedID, name: "Saved Notes"))
    }

    func testInlineAndDataviewHintsNameAvailableRecoveryAction() async throws {
        let inline = try XCTUnwrap(
            BaseEmbedRequest.codeFence(language: "base", source: "```base\nviews: []\n```"))
        let dql = try XCTUnwrap(
            BaseEmbedRequest.codeFence(
                language: "dataview",
                source: "```dataview\nTABLE file.name\n```"))

        for request in [inline, dql] {
            XCTAssertEqual(
                request.recoveryAction(thisPath: "Notes/Host.md")?.destination,
                .sourceNote(path: "Notes/Host.md"))
            XCTAssertEqual(
                request.recoveryAction(thisPath: "Notes/Host.md")?.title,
                "Edit source note")
            XCTAssertTrue(request.readOnlyHint(thisPath: "Notes/Host.md").contains("source note"))
            XCTAssertFalse(request.readOnlyHint(thisPath: "Notes/Host.md").contains("open in tab"))
        }
        XCTAssertTrue(dql.readOnlyHint(thisPath: nil).contains("convert"))
        XCTAssertTrue(inline.readOnlyHint(thisPath: nil).contains("source block"))

        let state = try await makeAppState()
        state.openFile("Notes/Host.md", target: .currentTab)
        await state.noteLoadTask?.value
        state.setViewMode(.reading)
        state.openBaseEmbedDestination(
            try XCTUnwrap(inline.recoveryAction(thisPath: "Notes/Host.md")?.destination))
        XCTAssertEqual(state.loadedFilePath, "Notes/Host.md")
        XCTAssertEqual(state.activeViewMode, .editing)
    }

    func testSavedQueryEmbedSelectionFollowsColumnIdentityAcrossReload() async throws {
        let state = try await makeAppState()
        let session = try XCTUnwrap(state.currentSession)
        _ = try session.saveQuery(
            name: "Saved Notes",
            description: nil,
            queryJson: savedQueryJSON,
            sourceSyntax: .builder)
        let request = try XCTUnwrap(
            BaseEmbedRequest.codeFence(
                language: "slate-query",
                source: "```slate-query\nquery: Saved Notes\n```"))
        let doc = BaseEmbedDocument(request: request, thisPath: "Notes/Host.md")
        doc.load(session: session)
        var initial = try XCTUnwrap(doc.result)
        initial.columns.append(
            BasesColumn(id: "status", label: "Status", valueKind: "text", role: .metadata))
        for index in initial.rows.indices {
            initial.rows[index].values.append(initial.rows[index].values[0])
        }
        let rowID = BaseGridRow.id(for: try XCTUnwrap(initial.rows.first))
        var interaction = BaseGridInteractionState()
        interaction.setCellPosition(.init(rowID: rowID, columnIndex: 1), in: initial)
        interaction.setSortState(
            DataGridSortState(columnIndex: 1, ascending: false),
            in: initial)

        var reordered = initial
        reordered.columns.swapAt(0, 1)
        for index in reordered.rows.indices { reordered.rows[index].values.swapAt(0, 1) }
        interaction.reconcile(with: reordered)

        XCTAssertEqual(interaction.cellPosition(in: reordered)?.columnIndex, 0)
        XCTAssertEqual(
            interaction.sortState(in: reordered),
            DataGridSortState(columnIndex: 0, ascending: false))

        reordered.columns = []
        reordered.rows = reordered.rows.map { row in
            var row = row
            row.values = []
            return row
        }
        interaction.reconcile(with: reordered)
        XCTAssertNil(interaction.selectedCell)
        XCTAssertNil(interaction.sortSelection)
    }

    func testOffscreenEmbedDefersExecutionWithoutRemovingSemanticPlaceholder() throws {
        var visibility = BaseEmbedVisibilityState()
        visibility.observe(isVisible: false)
        XCTAssertFalse(visibility.hasBecomeVisible)
        visibility.observe(isVisible: true)
        XCTAssertTrue(visibility.hasBecomeVisible)
        visibility.observe(isVisible: false)
        XCTAssertTrue(
            visibility.hasBecomeVisible,
            "once visible, the heavy child remains mounted instead of re-executing while scrolling")

        let readingSource = try Self.sourceFile("apps/slate-mac/Sources/SlateMac/Reading/ReadingView.swift")
        let embedSource = try Self.sourceFile("apps/slate-mac/Sources/SlateMac/Bases/BaseEmbedView.swift")

        XCTAssertTrue(readingSource.contains("VisibilityGatedBaseEmbed("))
        XCTAssertTrue(embedSource.contains(".onScrollVisibilityChange"))
        XCTAssertTrue(embedSource.contains("Deferred embedded base"))
        XCTAssertTrue(embedSource.contains(".accessibilityElement(children: .contain)"))
        XCTAssertFalse(
            readingSource.contains("LazyVStack"),
            "the whole reading tree must remain structurally enumerable to VoiceOver")
    }

    func testDuplicateEmbedDocumentsShareHandleButKeepQuickFilterIndependent() async throws {
        let state = try await makeAppState()
        let session = try XCTUnwrap(state.currentSession)
        let request = try XCTUnwrap(
            BaseEmbedRequest.wikilinkTarget("Queries/Reading.base#This Check"))

        let shared = state.baseEmbedHandle(for: request, thisPath: "Notes/Host.md")
        let first = BaseEmbedDocument(
            request: request, thisPath: "Notes/Host.md", sharedHandle: shared)
        let duplicate = BaseEmbedDocument(
            request: request, thisPath: "Notes/Host.md", sharedHandle: shared)
        let otherShared = state.baseEmbedHandle(for: request, thisPath: "Notes/Other.md")

        XCTAssertFalse(first === duplicate)
        XCTAssertFalse(shared === otherShared)
        XCTAssertEqual(state.baseEmbedHandles.count, 2)

        first.load(session: session)
        XCTAssertNotNil(duplicate.handle)
        XCTAssertTrue(duplicate.needsInitialLoad)
        duplicate.load(session: session)

        XCTAssertEqual(first.handle, duplicate.handle)
        XCTAssertEqual(duplicate.quickFilterText, "")

        _ = first.applyQuickFilter("Other", session: session)

        XCTAssertEqual(first.quickFilterText, "Other")
        XCTAssertEqual(duplicate.quickFilterText, "")
        XCTAssertEqual(first.result?.shownCount, 1)
        XCTAssertEqual(duplicate.result?.shownCount, 2)

        first.setTransientSort(
            DataGridSortState(columnIndex: 0, ascending: false), session: session)
        XCTAssertEqual(first.result?.rows.map(\.filePath), ["Notes/Other.md"])
        _ = duplicate.applyQuickFilter("", session: session)
        XCTAssertEqual(
            duplicate.result?.rows.map(\.filePath),
            ["Notes/Host.md", "Notes/Other.md"],
            "each embed execution must re-establish its own sort on a shared handle")

        state.releaseAllBaseEmbedDocuments()

        XCTAssertNil(first.handle)
        XCTAssertNil(duplicate.handle)
        XCTAssertTrue(state.baseEmbedHandles.isEmpty)
    }

    func testMountedLazyPlaceholderKeepsRegistryLeaseUntilUnmounted() async throws {
        let state = try await makeAppState()
        let request = try XCTUnwrap(
            BaseEmbedRequest.wikilinkTarget("Queries/Reading.base#This Check"))
        let handle = state.baseEmbedHandle(for: request, thisPath: "Notes/Host.md")

        handle.acquireMountedLease()
        state.releaseUnleasedBaseEmbedDocuments()

        XCTAssertEqual(state.baseEmbedHandles.count, 1)
        XCTAssertTrue(handle.hasLiveLease)
        XCTAssertNil(handle.handle, "an offscreen placeholder must retain ownership without opening")

        handle.releaseMountedLease()
        state.releaseUnleasedBaseEmbedDocuments()

        XCTAssertFalse(handle.hasLiveLease)
        XCTAssertTrue(state.baseEmbedHandles.isEmpty)
    }

    func testRegisteredEmbedRefreshPreservesViewQuickFilterAndSortAfterNoteWrite() async throws {
        let state = try await makeAppState()
        let session = try XCTUnwrap(state.currentSession)
        let request = try XCTUnwrap(
            BaseEmbedRequest.codeFence(
                language: "base",
                source:
                    #"""
                    ```base
                    views:
                      - type: table
                        name: All
                        filters: "file.inFolder(\"Notes\")"
                        order: [file.name, status]
                      - type: table
                        name: Host status
                        filters:
                          and:
                            - "file.inFolder(\"Notes\")"
                            - "status == \"host\""
                        order: [file.name, status]
                    ```
                    """#))
        let document = BaseEmbedDocument(
            request: request,
            thisPath: "Notes/Host.md",
            sharedHandle: state.baseEmbedHandle(
                for: request,
                thisPath: "Notes/Host.md"))
        document.load(session: session)
        document.selectView(index: 1, session: session)
        _ = document.applyQuickFilter("o", session: session)
        document.setTransientSort(
            DataGridSortState(columnIndex: 0, ascending: false),
            session: session)
        XCTAssertEqual(document.result?.rows.map(\.filePath), ["Notes/Host.md"])

        _ = try session.setProperty(
            path: "Notes/Other.md",
            key: "status",
            value: .text(value: "host"),
            expectedContentHash: nil)
        _ = await state.refreshVisibleBasesAfterInAppWrite(
            session: session,
            changedPath: "Notes/Other.md")?.value

        XCTAssertEqual(document.activeViewName, "Host status")
        XCTAssertEqual(document.quickFilterText, "o")
        XCTAssertEqual(
            document.sortState,
            DataGridSortState(columnIndex: 0, ascending: false))
        XCTAssertEqual(
            document.result?.rows.map(\.filePath),
            ["Notes/Other.md", "Notes/Host.md"])
    }

    func testUnknownNamedViewFailsWithAvailableViews() async throws {
        let state = try await makeAppState()
        let session = try XCTUnwrap(state.currentSession)
        let request = try XCTUnwrap(
            BaseEmbedRequest.wikilinkTarget("Queries/Reading.base#Missing"))
        let doc = BaseEmbedDocument(request: request, thisPath: "Notes/Host.md")

        doc.load(session: session)

        XCTAssertEqual(
            doc.state,
            .failed("Unknown base view Missing. Available views: First, This Check."))
    }
}
