// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// File-backed Bases must honor the same component-safe batch-Trash quarantine
/// as Markdown and Canvas. An unknown physical outcome keeps matching native
/// handles detached until the post-refresh probe proves the source exists.
@MainActor
final class BaseBatchTrashQuarantineTests: XCTestCase {
    private actor SuspensionGate {
        private var permits = 0
        private var waiters: [CheckedContinuation<Void, Never>] = []
        private var entrants = 0
        private var entrantWaiters: [(Int, CheckedContinuation<Void, Never>)] = []

        func enter() async {
            entrants += 1
            var remaining: [(Int, CheckedContinuation<Void, Never>)] = []
            for (expected, continuation) in entrantWaiters {
                if entrants >= expected {
                    continuation.resume()
                } else {
                    remaining.append((expected, continuation))
                }
            }
            entrantWaiters = remaining
            if permits > 0 {
                permits -= 1
                return
            }
            await withCheckedContinuation { waiters.append($0) }
        }

        func waitForEntrants(_ expected: Int) async {
            guard entrants < expected else { return }
            await withCheckedContinuation { entrantWaiters.append((expected, $0)) }
        }

        func releaseOne() {
            if waiters.isEmpty {
                permits += 1
            } else {
                waiters.removeFirst().resume()
            }
        }
    }

    private struct Fixture {
        let state: AppState
        let vault: URL
        let session: VaultSession
    }

    private var tempDirs: [URL] = []

    private static var projectRoot: URL {
        URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
    }

    override func tearDown() {
        for directory in tempDirs {
            try? FileManager.default.removeItem(at: directory)
        }
        tempDirs = []
        super.tearDown()
    }

    private func makeFixture() async throws -> Fixture {
        let root = FileManager.default.temporaryDirectory
            .appendingPathComponent("base-trash-quarantine-\(UUID().uuidString)")
        tempDirs.append(root)
        let vault = root.appendingPathComponent("vault")
        try FileManager.default.createDirectory(
            at: vault.appendingPathComponent("Queries"),
            withIntermediateDirectories: true)
        try FileManager.default.createDirectory(
            at: vault.appendingPathComponent("Notes"),
            withIntermediateDirectories: true)

        let definition = Data(
            #"""
            views:
              - type: table
                name: Reading
                filters: "file.inFolder(\"Notes\")"
                order:
                  - file.name
                  - status
              - type: table
                name: Everything
                order:
                  - file.name
            """#.utf8)
        try definition.write(to: vault.appendingPathComponent("Queries/Unknown.base"))
        try definition.write(to: vault.appendingPathComponent("Queries/Other.base"))
        try Data("---\nstatus: active\n---\n# Alpha\n".utf8)
            .write(to: vault.appendingPathComponent("Notes/Alpha.md"))

        let state = AppState(
            recentsStore: RecentVaultsStore(
                fileURL: root.appendingPathComponent("recents.json")),
            externalOpener: { _ in true })
        state.openVault(at: vault)
        await state.scanTask?.value
        return Fixture(
            state: state,
            vault: vault,
            session: try XCTUnwrap(state.currentSession))
    }

    private func beginUnknownTrash(
        in state: AppState,
        path: String,
        gate: SuspensionGate
    ) async throws -> Task<Void, Never> {
        let item = StructuralBatchItem(path: path, isDirectory: false)
        let report = unknownTrashReport([item])
        state.batchTrashRunner = { _, _ in report }
        state.structuralBatchRefreshRunner = { _ in await gate.enter() }
        let task = try XCTUnwrap(
            state.batchDelete(
                [.init(path: path, isDirectory: false)],
                preferredFocusPath: path))
        await gate.waitForEntrants(1)
        return task
    }

    private func unknownTrashReport(
        _ items: [StructuralBatchItem]
    ) -> BatchTrashReport {
        BatchTrashReport(
            envelope: StructuralBatchEnvelope(
                planned: items, skipped: [], preflightFailures: []),
            state: .failed,
            opId: nil,
            trashed: [],
            untrashed: [],
            unknown: items.map { item in
                BatchTrashRemainder(
                    item: item,
                    failure: BatchItemFailure(
                        item: item,
                        stage: .reconciliation,
                        message: "physical Trash verification failed"))
            },
            bookkeepingFailures: [],
            requiresRescan: true)
    }

    func testDirectBaseActionsCannotReloadQuarantinedFileButUnrelatedBaseStillWorks()
        async throws
    {
        let fixture = try await makeFixture()
        let state = fixture.state
        state.openFile("Queries/Unknown.base", target: .currentTab)
        let unknown = try XCTUnwrap(state.activeBaseDocument)
        state.basesEditViewFilters()
        XCTAssertNotNil(state.activeBaseQueryBuilder)

        let gate = SuspensionGate()
        let trash = try await beginUnknownTrash(
            in: state, path: "Queries/Unknown.base", gate: gate)
        XCTAssertNil(unknown.handle)

        state.basesBuilderSaveToView()
        XCTAssertNil(
            unknown.handle,
            "builder save must not reopen an outcome-unknown Base")

        unknown.close(session: fixture.session)
        state.basesCloseQueryBuilder()
        state.basesEditViewFilters()
        XCTAssertNil(
            unknown.handle,
            "Edit filters must not reopen an outcome-unknown Base")
        XCTAssertNil(state.activeBaseQueryBuilder)

        state.basesRefresh()
        XCTAssertNil(
            unknown.handle,
            "manual refresh must not reopen an outcome-unknown Base")

        state.openFile("Queries/Other.base", target: .newTab)
        let unrelated = try XCTUnwrap(state.activeBaseDocument)
        XCTAssertNotNil(
            unrelated.handle,
            "the component gate must not disable an unrelated Base")
        state.basesRefresh()
        XCTAssertNotNil(unrelated.handle)

        await gate.releaseOne()
        await trash.value
    }

    func testPostWriteRefreshSkipsQuarantinedBaseAndRefreshesUnrelatedBase() async throws {
        let fixture = try await makeFixture()
        let state = fixture.state
        state.openFile("Queries/Unknown.base", target: .currentTab)
        let unknown = try XCTUnwrap(state.activeBaseDocument)
        state.openFile("Queries/Other.base", target: .newTab)
        let unrelated = try XCTUnwrap(state.activeBaseDocument)

        let gate = SuspensionGate()
        let trash = try await beginUnknownTrash(
            in: state, path: "Queries/Unknown.base", gate: gate)
        XCTAssertNil(unknown.handle)
        XCTAssertNotNil(unrelated.handle)

        _ = await state.refreshVisibleBasesAfterInAppWrite(
            session: fixture.session,
            changedPath: "Notes/Alpha.md")?.value

        XCTAssertNil(
            unknown.handle,
            "the broad post-write refresh must not install a quarantined handle")
        XCTAssertNotNil(
            unrelated.handle,
            "unrelated visible Bases must continue refreshing")

        await gate.releaseOne()
        await trash.value
    }

    func testBaseRowPropertyWriterDelegatesToUnknownTrashWriteGate() async throws {
        let fixture = try await makeFixture()
        let state = fixture.state
        state.openFile("Queries/Other.base", target: .currentTab)
        let result = try XCTUnwrap(state.activeBaseDocument?.result)
        let row = try XCTUnwrap(
            result.rows.first { $0.filePath == "Notes/Alpha.md" })
        let status = try XCTUnwrap(result.columns.first { $0.id == "status" })
        let before = try String(
            contentsOf: fixture.vault.appendingPathComponent("Notes/Alpha.md"),
            encoding: .utf8)

        let gate = SuspensionGate()
        let trash = try await beginUnknownTrash(
            in: state, path: "Notes/Alpha.md", gate: gate)

        let announcement = await state.basesSetProperty(
            row: row,
            column: status,
            value: .text(value: "must-not-write"))

        XCTAssertNil(announcement)
        XCTAssertEqual(
            try String(
                contentsOf: fixture.vault.appendingPathComponent("Notes/Alpha.md"),
                encoding: .utf8),
            before,
            "Base-row edits must not write a path with an unknown Trash outcome")

        await gate.releaseOne()
        await trash.value
    }

    func testFileBackedEmbedsDetachDuringUnknownOutcomeAndResumeOnlyWhenPresent()
        async throws
    {
        let fixture = try await makeFixture()
        let state = fixture.state
        let unknownRequest = try XCTUnwrap(
            BaseEmbedRequest.wikilinkTarget("Queries/Unknown.base"))
        let otherRequest = try XCTUnwrap(
            BaseEmbedRequest.wikilinkTarget("Queries/Other.base"))
        let unknownHandle = state.baseEmbedHandle(
            for: unknownRequest, thisPath: "Notes/Alpha.md")
        let otherHandle = state.baseEmbedHandle(
            for: otherRequest, thisPath: "Notes/Alpha.md")
        let unknownDocument = BaseEmbedDocument(
            request: unknownRequest,
            thisPath: "Notes/Alpha.md",
            sharedHandle: unknownHandle)
        let otherDocument = BaseEmbedDocument(
            request: otherRequest,
            thisPath: "Notes/Alpha.md",
            sharedHandle: otherHandle)
        unknownDocument.load(session: fixture.session)
        otherDocument.load(session: fixture.session)
        let preservedRows = unknownDocument.result?.rows.map(\.filePath)
        XCTAssertNotNil(unknownHandle.handle)
        XCTAssertNotNil(otherHandle.handle)

        let gate = SuspensionGate()
        let trash = try await beginUnknownTrash(
            in: state, path: "Queries/Unknown.base", gate: gate)

        XCTAssertNil(
            unknownHandle.handle,
            "an already-open matching embed handle must detach synchronously")
        XCTAssertEqual(
            unknownDocument.result?.rows.map(\.filePath),
            preservedRows,
            "detaching a read-only embed must preserve its visible snapshot")
        XCTAssertNotNil(
            otherHandle.handle,
            "an unrelated embed handle must remain usable")

        let lateDocument = BaseEmbedDocument(
            request: unknownRequest,
            thisPath: "Notes/Alpha.md",
            sharedHandle: unknownHandle)
        XCTAssertTrue(lateDocument.needsInitialLoad)

        _ = await state.refreshVisibleBasesAfterInAppWrite(
            session: fixture.session,
            changedPath: "Notes/Alpha.md")?.value
        XCTAssertNil(
            unknownHandle.handle,
            "post-write refresh must not reopen a quarantined embed")
        XCTAssertNotNil(otherHandle.handle)

        await gate.releaseOne()
        await trash.value

        XCTAssertNotNil(
            unknownHandle.handle,
            "a post-refresh probe that proves presence must resume the embed")
        XCTAssertFalse(
            lateDocument.needsInitialLoad,
            "a late mounted embed must load when its source is proven present")
        XCTAssertNotNil(lateDocument.result)
    }

    func testLateEmbedLoadStaysBlockedAfterIndeterminateProbeWhileUnrelatedEmbedLoads()
        async throws
    {
        let fixture = try await makeFixture()
        let state = fixture.state
        state.batchTrashPresenceProbeRunner = { _, _ in .indeterminate }
        let gate = SuspensionGate()
        let trash = try await beginUnknownTrash(
            in: state, path: "Queries/Unknown.base", gate: gate)
        await gate.releaseOne()
        await trash.value

        let unknownRequest = try XCTUnwrap(
            BaseEmbedRequest.wikilinkTarget("Queries/Unknown.base"))
        let unknownDocument = BaseEmbedDocument(
            request: unknownRequest,
            thisPath: "Notes/Alpha.md",
            sharedHandle: state.baseEmbedHandle(
                for: unknownRequest,
                thisPath: "Notes/Alpha.md"))
        XCTAssertFalse(
            state.loadBaseEmbedDocumentIfAllowed(
                unknownDocument,
                session: fixture.session))
        XCTAssertNil(
            unknownDocument.handle,
            "indeterminate reconciliation must keep late embeds detached")

        let otherRequest = try XCTUnwrap(
            BaseEmbedRequest.wikilinkTarget("Queries/Other.base"))
        let unrelatedDocument = BaseEmbedDocument(
            request: otherRequest,
            thisPath: "Notes/Alpha.md",
            sharedHandle: state.baseEmbedHandle(
                for: otherRequest,
                thisPath: "Notes/Alpha.md"))
        XCTAssertTrue(
            state.loadBaseEmbedDocumentIfAllowed(
                unrelatedDocument,
                session: fixture.session))
        XCTAssertNotNil(unrelatedDocument.handle)
    }

    func testEmbedSnapshotBecomesDefinitelyUnavailableOnlyAfterProbeProvesAbsence()
        async throws
    {
        let fixture = try await makeFixture()
        let state = fixture.state
        let request = try XCTUnwrap(
            BaseEmbedRequest.wikilinkTarget("Queries/Unknown.base"))
        let document = BaseEmbedDocument(
            request: request,
            thisPath: "Notes/Alpha.md",
            sharedHandle: state.baseEmbedHandle(
                for: request,
                thisPath: "Notes/Alpha.md"))
        document.load(session: fixture.session)
        let preservedRows = document.result?.rows.map(\.filePath)
        XCTAssertNotNil(preservedRows)

        state.batchTrashPresenceProbeRunner = { _, _ in .absent }
        let gate = SuspensionGate()
        let trash = try await beginUnknownTrash(
            in: state, path: "Queries/Unknown.base", gate: gate)

        XCTAssertNil(document.handle)
        XCTAssertEqual(
            document.result?.rows.map(\.filePath),
            preservedRows,
            "ambiguity must preserve the visible embed snapshot")

        await gate.releaseOne()
        await trash.value

        XCTAssertNil(document.handle)
        XCTAssertNil(
            document.result,
            "only a definite absent probe may replace the preserved snapshot")
        XCTAssertEqual(
            document.state,
            .failed("Unknown.base was moved to Trash and is no longer available."))
    }

    func testPresentAncestorCannotResumeExplicitlyIndeterminateEmbed() async throws {
        let fixture = try await makeFixture()
        let state = fixture.state
        let request = try XCTUnwrap(
            BaseEmbedRequest.wikilinkTarget("Queries/Unknown.base"))
        let document = BaseEmbedDocument(
            request: request,
            thisPath: "Notes/Alpha.md",
            sharedHandle: state.baseEmbedHandle(
                for: request, thisPath: "Notes/Alpha.md"))
        document.load(session: fixture.session)
        let preservedRows = document.result?.rows.map(\.filePath)
        let folder = StructuralBatchItem(path: "Queries", isDirectory: true)
        let exact = StructuralBatchItem(
            path: "Queries/Unknown.base", isDirectory: false)
        let items = [folder, exact]
        let report = unknownTrashReport(items)
        state.batchTrashRunner = { _, _ in report }
        state.batchTrashPresenceProbeRunner = { url, _ in
            url.lastPathComponent == "Queries" ? .present : .indeterminate
        }
        state.structuralBatchRefreshRunner = { _ in }

        let trash = try XCTUnwrap(
            state.batchDelete(
                items.map { .init(path: $0.path, isDirectory: $0.isDirectory) },
                preferredFocusPath: exact.path))
        await trash.value
        await state.nativeDocumentRetargetTask?.value

        XCTAssertNil(
            document.handle,
            "a present ancestor must not reopen an explicitly indeterminate embed")
        XCTAssertEqual(document.result?.rows.map(\.filePath), preservedRows)
        XCTAssertEqual(
            state.batchTrashPathCapability(for: exact.path),
            .readOnly(AppState.batchTrashQuarantineReason))
    }

    func testQuarantinedBaseAndEmbedInteractionsCannotErasePreservedSnapshots()
        async throws
    {
        let fixture = try await makeFixture()
        let state = fixture.state
        state.openFile("Queries/Unknown.base", target: .currentTab)
        let base = try XCTUnwrap(state.activeBaseDocument)
        let request = try XCTUnwrap(
            BaseEmbedRequest.wikilinkTarget("Queries/Unknown.base"))
        let embed = BaseEmbedDocument(
            request: request,
            thisPath: "Notes/Alpha.md",
            sharedHandle: state.baseEmbedHandle(
                for: request, thisPath: "Notes/Alpha.md"))
        embed.load(session: fixture.session)
        let baseRows = base.result?.rows.map(\.filePath)
        let embedRows = embed.result?.rows.map(\.filePath)
        let baseView = base.activeViewIndex
        let embedView = embed.activeViewIndex

        state.batchTrashPresenceProbeRunner = { _, _ in .indeterminate }
        let gate = SuspensionGate()
        let trash = try await beginUnknownTrash(
            in: state, path: "Queries/Unknown.base", gate: gate)
        await gate.releaseOne()
        await trash.value
        await state.nativeDocumentRetargetTask?.value
        XCTAssertNil(base.handle)
        XCTAssertNil(embed.handle)

        base.selectView(index: 1, session: fixture.session)
        _ = base.applyQuickFilter("must not apply", session: fixture.session)
        embed.selectView(index: 1, session: fixture.session)
        _ = embed.applyQuickFilter("must not apply", session: fixture.session)

        XCTAssertEqual(base.activeViewIndex, baseView)
        XCTAssertEqual(embed.activeViewIndex, embedView)
        XCTAssertEqual(base.result?.rows.map(\.filePath), baseRows)
        XCTAssertEqual(embed.result?.rows.map(\.filePath), embedRows)
        XCTAssertEqual(base.quickFilterText, "")
        XCTAssertEqual(embed.quickFilterText, "")
    }

    func testFreeFormBaseWritersCannotBypassQuarantineWithPathAliases()
        async throws
    {
        let fixture = try await makeFixture()
        let state = fixture.state
        let item = StructuralBatchItem(
            path: "Queries/Unknown.base", isDirectory: false)
        let original = try Data(
            contentsOf: fixture.vault.appendingPathComponent(item.path))
        let report = unknownTrashReport([item])
        state.batchTrashRunner = { _, _ in report }
        state.batchTrashPresenceProbeRunner = { _, _ in .indeterminate }
        state.structuralBatchRefreshRunner = { _ in }
        let trash = try XCTUnwrap(
            state.batchDelete(
                [.init(path: item.path, isDirectory: false)],
                preferredFocusPath: item.path))
        await trash.value

        let queryID = try fixture.session.saveQuery(
            name: "All Files",
            description: nil,
            queryJson: #"{"source":{"Folder":"Notes"},"row_source":"Files","filters":null,"formulas":[],"custom_summaries":[],"group_by":null,"sort":[],"columns":[{"id":"file.name","display_name":null}],"summaries":[],"limit":null,"view":{"Table":{"fallback_from":null}}}"#,
            sourceSyntax: .builder)
        let export = state.exportSavedQuery(
            id: queryID, path: "./Queries/Unknown.base")
        XCTAssertNil(export)
        await export?.value

        state.activeBaseQueryBuilder = BaseQueryBuilderModel()
        let builder = state.basesBuilderSaveAsBase(
            path: "Queries//Unknown.base")
        XCTAssertNil(builder)
        await builder?.value

        XCTAssertEqual(
            try Data(contentsOf: fixture.vault.appendingPathComponent(item.path)),
            original,
            "core-equivalent aliases must never overwrite or reach an unknown path")
        XCTAssertEqual(
            state.lastMutationAnnouncement,
            AppState.batchTrashQuarantineReason)
    }

    func testAliasedBaseEmbedDetachesAndCannotReopenWhileCanonicalPathIsUnknown()
        async throws
    {
        let fixture = try await makeFixture()
        let state = fixture.state
        let request = try XCTUnwrap(
            BaseEmbedRequest.wikilinkTarget("./Queries//Unknown.base"))
        let document = BaseEmbedDocument(
            request: request,
            thisPath: "Notes/Alpha.md",
            sharedHandle: state.baseEmbedHandle(
                for: request, thisPath: "Notes/Alpha.md"))
        document.load(session: fixture.session)
        let preservedRows = document.result?.rows.map(\.filePath)
        XCTAssertNotNil(document.handle)

        state.batchTrashPresenceProbeRunner = { _, _ in .indeterminate }
        let gate = SuspensionGate()
        let trash = try await beginUnknownTrash(
            in: state, path: "Queries/Unknown.base", gate: gate)

        XCTAssertNil(
            document.handle,
            "core-equivalent dot/repeated-separator aliases must detach immediately")
        XCTAssertEqual(document.result?.rows.map(\.filePath), preservedRows)

        await gate.releaseOne()
        await trash.value
        XCTAssertFalse(
            state.loadBaseEmbedDocumentIfAllowed(
                document, session: fixture.session))
        XCTAssertNil(document.handle)
        XCTAssertEqual(document.result?.rows.map(\.filePath), preservedRows)
    }

    func testSwiftUISurfacesRouteInitialLoadsThroughQuarantineAwareFunnels() throws {
        let baseContainer = try String(
            contentsOf: Self.projectRoot.appendingPathComponent(
                "apps/slate-mac/Sources/SlateMac/Bases/BaseContainerView.swift"),
            encoding: .utf8)
        let baseEmbed = try String(
            contentsOf: Self.projectRoot.appendingPathComponent(
                "apps/slate-mac/Sources/SlateMac/Bases/BaseEmbedView.swift"),
            encoding: .utf8)

        XCTAssertTrue(
            baseContainer.contains("appState.loadBaseDocumentIfAllowed("),
            "BaseContainerView.onAppear must use the central component gate")
        XCTAssertFalse(
            baseContainer.contains("document.load(session: session)"),
            "BaseContainerView must not retain a direct load bypass")
        XCTAssertTrue(baseContainer.contains("batchTrashInteractionDisabledReason"))
        XCTAssertTrue(
            baseContainer.contains(
                ".disabled(batchTrashInteractionDisabledReason != nil)"))
        XCTAssertTrue(baseContainer.contains("batchTrashPathCapability(for: path)"))
        XCTAssertTrue(
            baseEmbed.contains("appState.loadBaseEmbedDocumentIfAllowed("),
            "BaseEmbedView.onAppear must use the central component gate")
        XCTAssertFalse(
            baseEmbed.contains("document.load(session: session)"),
            "BaseEmbedView must not retain a direct load bypass")
        XCTAssertTrue(baseEmbed.contains("batchTrashInteractionDisabledReason"))
        XCTAssertTrue(
            baseEmbed.contains(
                ".disabled(batchTrashInteractionDisabledReason != nil)"))
        XCTAssertTrue(baseEmbed.contains("batchTrashPathCapability(for: path)"))
    }
}
