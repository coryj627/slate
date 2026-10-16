// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import SwiftUI
import XCTest

@testable import SlateMac

/// #786 (plan 43 PR 15): a saved `.base` whose definition is empty or
/// incomplete, or whose selected view cannot load, explains what failed and
/// keeps its definition editable, so the user can repair it instead of
/// facing a bare placeholder with Edit filters disabled. Only a definition
/// the app cannot reach — missing, read-only, still reopening — disables the
/// edit, with the reason the data interactions already give.
@MainActor
final class BaseContainerRecoveryTests: XCTestCase {
    private var tempDirectory: URL!
    private var vaultURL: URL!

    override func setUpWithError() throws {
        try super.setUpWithError()
        tempDirectory = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-base-recovery-\(UUID().uuidString)")
        try FileManager.default.createDirectory(
            at: tempDirectory, withIntermediateDirectories: true)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: tempDirectory)
        try super.tearDownWithError()
    }

    /// A vault with one working two-view Base (`Queries/Reading.base`), two
    /// notes it lists, and any extra `.base` definitions a fact needs.
    private func makeAppState(bases: [String: String] = [:]) async throws -> AppState {
        let vault = tempDirectory.appendingPathComponent("vault-\(UUID().uuidString)")
        vaultURL = vault
        try FileManager.default.createDirectory(
            at: vault.appendingPathComponent("Queries"), withIntermediateDirectories: true)
        try FileManager.default.createDirectory(
            at: vault.appendingPathComponent("Notes"), withIntermediateDirectories: true)
        try Data(
            #"""
            views:
              - type: table
                name: Reading
                filters: "file.inFolder(\"Notes\")"
                order:
                  - file.name
                  - status
              - type: table
                name: Done
                filters: "status == \"done\""
                order:
                  - file.name
            """#.utf8
        ).write(to: vault.appendingPathComponent("Queries/Reading.base"))
        for (path, definition) in bases {
            try Data(definition.utf8).write(to: vault.appendingPathComponent(path))
        }
        try Data("---\nstatus: active\n---\n# Alpha\n".utf8)
            .write(to: vault.appendingPathComponent("Notes/Alpha.md"))
        try Data("---\nstatus: done\n---\n# Beta\n".utf8)
            .write(to: vault.appendingPathComponent("Notes/Beta.md"))

        let store = RecentVaultsStore(
            fileURL: tempDirectory.appendingPathComponent("recents-\(UUID().uuidString).json"))
        let state = AppState(recentsStore: store, externalOpener: { _ in true })
        state.openVault(at: vault)
        await state.scanTask?.value
        return state
    }

    private static let noExecutableViews = "No executable base views were found."

    func testASavedEmptyBaseOpensInARecoverableStateWithEditingEnabled() async throws {
        let state = try await makeAppState(bases: ["Queries/Empty.base": ""])

        state.openBaseFile("Queries/Empty.base")
        let document = state.baseDocument(for: "Queries/Empty.base")

        XCTAssertNotNil(
            document.handle,
            "core reads an empty definition: the file is present and writable")
        XCTAssertEqual(
            document.state, .failed(Self.noExecutableViews),
            "an empty definition loads as failed, so the surface explains it")
        XCTAssertEqual(
            state.baseLoadFailure(for: document),
            BaseLoadFailure(message: Self.noExecutableViews, editDisabledReason: nil))
        XCTAssertNil(state.baseDefinitionEditingDisabledReason(for: document))
        XCTAssertNil(
            state.activeBaseDefinitionEditingDisabledReason,
            "the toolbar's Edit filters and the palette's Edit View Filters stay available")
    }

    func testAnIncompleteDefinitionShowsTheErrorAndKeepsEditingEnabled() async throws {
        let definitions = [
            // Saved before any view was added.
            "Queries/FiltersOnly.base": #"filters: 'file.inFolder("Notes")'"# + "\n",
            // Saved mid-edit: the view list never closed.
            "Queries/Truncated.base": "views:\n  - type: table\n    name: [Reading\n",
        ]
        let state = try await makeAppState(bases: definitions)

        for path in definitions.keys.sorted() {
            state.openBaseFile(path)
            let document = state.baseDocument(for: path)

            XCTAssertNotNil(document.handle, path)
            let failure = try XCTUnwrap(
                state.baseLoadFailure(for: document),
                "\(path) must render the error view, not a bare placeholder")
            XCTAssertEqual(failure.message, Self.noExecutableViews, path)
            XCTAssertFalse(
                failure.message.contains("YAML"),
                "\(path): the message is core's, never parser internals")
            XCTAssertNil(failure.editDisabledReason, path)
            XCTAssertNil(state.activeBaseDefinitionEditingDisabledReason, path)
        }
    }

    func testAnUnloadableSelectedViewExplainsItselfAndKeepsEditingEnabled() async throws {
        let state = try await makeAppState()
        let session = try XCTUnwrap(state.currentSession)
        state.openBaseFile("Queries/Reading.base")
        let document = state.baseDocument(for: "Queries/Reading.base")
        XCTAssertEqual(document.state, .ready)

        document.executeFaultForTests = {
            VaultError.InvalidQuery(message: "unknown function frobnicate")
        }
        document.selectView(index: 1, session: session)

        XCTAssertEqual(document.activeViewName, "Done")
        XCTAssertNotNil(document.handle, "the definition stays open")
        let failure = try XCTUnwrap(state.baseLoadFailure(for: document))
        XCTAssertEqual(
            failure,
            BaseLoadFailure(
                message: "Reading could not be opened: unknown function frobnicate",
                editDisabledReason: nil))
        XCTAssertFalse(
            failure.message.contains("VaultError"),
            "core's message, never the Swift debug description (#1364)")
        XCTAssertNil(
            state.baseDocumentInteractionDisabledReason(for: document),
            "the view picker stays usable: another view may load")
    }

    func testEditFromTheErrorStateOpensTheBuilder() async throws {
        let state = try await makeAppState(bases: ["Queries/Empty.base": ""])
        let session = try XCTUnwrap(state.currentSession)
        state.openBaseFile("Queries/Empty.base")
        let empty = state.baseDocument(for: "Queries/Empty.base")
        XCTAssertNotNil(state.baseLoadFailure(for: empty))

        // The error view's Edit button runs the toolbar's Edit filters action.
        let container = try sourceFile("Bases/BaseContainerView.swift")
        let failedArm = try XCTUnwrap(
            Self.section(of: container, from: "case .failed", to: "case .ready"))
        XCTAssertTrue(failedArm.contains("BaseLoadFailedView("), failedArm)
        XCTAssertTrue(failedArm.contains("appState.basesEditViewFilters()"), failedArm)

        state.basesEditViewFilters()

        let builder = try XCTUnwrap(
            state.activeBaseQueryBuilder,
            "Edit from the error state opens the builder")
        XCTAssertEqual(
            builder.editingBaseView,
            EditingBaseView(source: empty.source, viewIndex: 0))
        XCTAssertEqual(state.lastBaseActionAnnouncement, "Editing filters for active view.")

        // Its save repairs the definition: the builder's view becomes the first.
        state.basesBuilderSaveToView()
        XCTAssertEqual(state.lastBaseActionAnnouncement, "Saved builder changes to view.")
        _ = await state.visibleBasesRefreshTask?.value
        let saved = try String(
            contentsOf: try XCTUnwrap(vaultURL).appendingPathComponent("Queries/Empty.base"),
            encoding: .utf8)
        XCTAssertTrue(saved.contains("views:"), saved)
        XCTAssertEqual(empty.views.count, 1)
        XCTAssertEqual(empty.state, .ready)
        XCTAssertNil(state.baseLoadFailure(for: empty))
        state.basesCloseQueryBuilder()

        // A definition detached after core read it (the C6 rollback detach)
        // keeps its data interactions unavailable but reopens for the edit.
        state.openBaseFile("Queries/Reading.base")
        let reading = state.baseDocument(for: "Queries/Reading.base")
        struct Fault: Error {}
        reading.executeFaultForTests = { Fault() }
        reading.rollbackFaultForTests = { Fault() }
        XCTAssertFalse(
            reading.setTransientSort(
                DataGridSortState(columnIndex: 0, ascending: true), session: session))
        reading.executeFaultForTests = nil
        reading.rollbackFaultForTests = nil
        XCTAssertNil(reading.handle)
        XCTAssertNotNil(state.baseLoadFailure(for: reading))
        XCTAssertEqual(
            state.baseDocumentInteractionDisabledReason(for: reading),
            AppState.baseDocumentUnavailableDisabledReason,
            "sorting and cell edits still need the native handle")
        XCTAssertNil(
            state.baseDefinitionEditingDisabledReason(for: reading),
            "the definition is present, so it stays editable")

        state.basesEditViewFilters()

        XCTAssertNotNil(reading.handle, "the edit reopens the definition first")
        XCTAssertEqual(
            state.activeBaseQueryBuilder?.editingBaseView,
            EditingBaseView(source: reading.source, viewIndex: 0))
        XCTAssertEqual(state.lastBaseActionAnnouncement, "Editing filters for Reading.")
    }

    func testAMissingBaseFileStillDisablesEditingWithTheUnavailableReason() async throws {
        let state = try await makeAppState(bases: ["Queries/Trashed.base": "views: []\n"])
        let unavailable = AppState.baseDocumentUnavailableDisabledReason

        // Deleted on disk, then reopened.
        state.openBaseFile("Queries/Reading.base")
        let reading = state.baseDocument(for: "Queries/Reading.base")
        try FileManager.default.removeItem(
            at: try XCTUnwrap(vaultURL).appendingPathComponent("Queries/Reading.base"))
        state.basesRefresh()
        XCTAssertNil(reading.handle)
        XCTAssertEqual(
            state.baseLoadFailure(for: reading),
            BaseLoadFailure(
                message: "Reading could not be read — it may have been moved or deleted.",
                editDisabledReason: unavailable))
        XCTAssertEqual(state.baseDefinitionEditingDisabledReason(for: reading), unavailable)
        XCTAssertEqual(state.activeBaseDefinitionEditingDisabledReason, unavailable)
        state.basesEditViewFilters()
        XCTAssertNil(state.activeBaseQueryBuilder)
        XCTAssertEqual(state.lastMutationAnnouncement, unavailable)

        // Detached after a read, then deleted: the edit's reopen fails, says
        // why, and leaves the edit disabled with the unavailable reason.
        let session = try XCTUnwrap(state.currentSession)
        try Data("views:\n  - type: table\n    name: Reading\n    order: [file.name]\n".utf8)
            .write(
                to: try XCTUnwrap(vaultURL).appendingPathComponent("Queries/Reading.base"))
        reading.load(session: session)
        XCTAssertNotNil(reading.handle)
        struct Fault: Error {}
        reading.executeFaultForTests = { Fault() }
        reading.rollbackFaultForTests = { Fault() }
        XCTAssertFalse(
            reading.setTransientSort(
                DataGridSortState(columnIndex: 0, ascending: true), session: session))
        reading.executeFaultForTests = nil
        reading.rollbackFaultForTests = nil
        XCTAssertNil(reading.handle)
        XCTAssertNil(state.baseDefinitionEditingDisabledReason(for: reading))
        try FileManager.default.removeItem(
            at: try XCTUnwrap(vaultURL).appendingPathComponent("Queries/Reading.base"))
        state.basesEditViewFilters()
        XCTAssertNil(state.activeBaseQueryBuilder)
        XCTAssertEqual(
            state.lastBaseActionAnnouncement,
            "Base filters could not be opened in the builder: "
                + "Reading could not be read — it may have been moved or deleted.")
        XCTAssertEqual(state.baseDefinitionEditingDisabledReason(for: reading), unavailable)

        // Moved to Trash: the batch-Trash reconciliation's definite absence.
        state.openBaseFile("Queries/Trashed.base")
        let trashed = state.baseDocument(for: "Queries/Trashed.base")
        XCTAssertNotNil(trashed.handle)
        try FileManager.default.removeItem(
            at: try XCTUnwrap(vaultURL).appendingPathComponent("Queries/Trashed.base"))
        state.invalidateBaseDocument(path: "Queries/Trashed.base")
        XCTAssertNil(trashed.handle)
        XCTAssertEqual(
            state.baseLoadFailure(for: trashed)?.editDisabledReason, unavailable)
        XCTAssertEqual(state.baseDefinitionEditingDisabledReason(for: trashed), unavailable)

        // A reopen that failed keeps its own reason, not the unavailable one.
        try Data("views: []\n".utf8).write(
            to: try XCTUnwrap(vaultURL).appendingPathComponent("Queries/Trashed.base"))
        trashed.load(session: session)
        let reservation = trashed.beginBatchRetarget(to: trashed.source)
        if let replaced = reservation.replacedHandle {
            session.closeBase(handle: replaced)
        }
        let generation = try XCTUnwrap(trashed.claimRetargetPreparation())
        XCTAssertTrue(
            trashed.applyRetargetPreparation(
                .failed("Injected Base reopen failure."),
                generation: generation,
                source: trashed.source))
        XCTAssertEqual(
            state.baseDefinitionEditingDisabledReason(for: trashed),
            AppState.baseDocumentRetargetFailedDisabledReason)
    }

    func testNoBlankSurfaceForAFailedBase() async throws {
        let state = try await makeAppState(bases: ["Queries/Empty.base": ""])
        let session = try XCTUnwrap(state.currentSession)

        // Every failed flavour reaches the error view with something to say.
        state.openBaseFile("Queries/Empty.base")
        let empty = state.baseDocument(for: "Queries/Empty.base")
        state.openBaseFile("Queries/Missing.base")
        let missing = state.baseDocument(for: "Queries/Missing.base")
        state.openBaseFile("Queries/Reading.base")
        let unloadable = state.baseDocument(for: "Queries/Reading.base")
        unloadable.executeFaultForTests = { VaultError.Db(message: "database is locked") }
        unloadable.executeActiveView(session: session)
        for document in [empty, missing, unloadable] {
            let failure = try XCTUnwrap(
                state.baseLoadFailure(for: document),
                "\(document.displayName) must render the error view")
            XCTAssertFalse(failure.message.isEmpty, document.displayName)
        }
        XCTAssertNil(
            state.baseLoadFailure(for: state.baseDocument(for: "Queries/Other.base")),
            "a Base still loading shows the loading line, not the error view")

        // The failed arm renders the error view, never `placeholder(message)`.
        let container = try sourceFile("Bases/BaseContainerView.swift")
        let failedArm = try XCTUnwrap(
            Self.section(of: container, from: "case .failed", to: "case .ready"))
        XCTAssertTrue(failedArm.contains("BaseLoadFailedView("), failedArm)
        XCTAssertFalse(failedArm.contains("placeholder("), failedArm)

        // The error view: a traversable group led by a heading, core's
        // message, the repair (or why it is unavailable), and the Edit button
        // whose hint says what it opens.
        let view = try XCTUnwrap(
            Self.section(
                of: container,
                from: "struct BaseLoadFailedView",
                to: "enum BaseContainerTabRouting"))
        XCTAssertTrue(view.contains(#"Text("This base could not be loaded")"#), view)
        XCTAssertTrue(view.contains(".accessibilityAddTraits(.isHeader)"), view)
        XCTAssertTrue(view.contains("Text(failure.message)"), view)
        XCTAssertTrue(view.contains(#"Text("Edit the base definition to repair it.")"#), view)
        XCTAssertTrue(
            view.contains("if let reason = failure.editDisabledReason")
                && view.contains("Text(reason)"),
            "where Edit is unavailable its reason replaces the repair sentence")
        XCTAssertTrue(view.contains(#"Button("Edit""#), view)
        XCTAssertTrue(
            view.contains(#""Opens the base definition in the query builder.""#), view)
        XCTAssertTrue(view.contains(".disabled(failure.editDisabledReason != nil)"), view)
        XCTAssertTrue(view.contains(".accessibilityElement(children: .contain)"), view)

        // And it mounts: the hosted surface lays out for a failed document.
        let tabID = try XCTUnwrap(state.workspace.activeTab?.id)
        let host = NSHostingView(
            rootView: BaseContainerView(document: empty, tabID: tabID)
                .environmentObject(state))
        host.frame = NSRect(x: 0, y: 0, width: 640, height: 480)
        host.layoutSubtreeIfNeeded()
        XCTAssertGreaterThan(host.fittingSize.height, 0)
    }

    private static func section(of source: String, from start: String, to end: String)
        -> String?
    {
        guard let lower = source.range(of: start),
            let upper = source.range(of: end, range: lower.upperBound..<source.endIndex)
        else { return nil }
        return String(source[lower.lowerBound..<upper.lowerBound])
    }

    private func sourceFile(_ relativePath: String) throws -> String {
        let root = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
        return try String(
            contentsOf: root
                .appendingPathComponent("apps/slate-mac/Sources/SlateMac")
                .appendingPathComponent(relativePath),
            encoding: .utf8)
    }
}
