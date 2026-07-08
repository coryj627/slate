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
            "read-only in embeds — open in tab to edit")
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
            ["Host.md", "Other.md"])
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

        state.releaseAllBaseEmbedDocuments()

        XCTAssertNil(first.handle)
        XCTAssertNil(duplicate.handle)
        XCTAssertTrue(state.baseEmbedHandles.isEmpty)
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
