// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI
import XCTest

@testable import SlateMac

/// U1-4 (#456): the center-column migration is structural, not behavioral.
/// These tests pin the mirror contract (selection ⇄ workspace model) and the
/// presentation-ready render gates for the workspace region; the rest of the
/// suite is the behavioral regression harness.
@MainActor
final class WorkspaceViewMigrationTests: XCTestCase {

    private var tempDir: URL!

    override func setUpWithError() throws {
        tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("workspace-migration-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: tempDir, withIntermediateDirectories: true)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: tempDir)
    }

    private func makeAppState() -> AppState {
        let store = RecentVaultsStore(
            fileURL: tempDir.appendingPathComponent("recents.json"))
        return AppState(recentsStore: store, externalOpener: { _ in true })
    }

    private func makeVault(files: [String]) throws -> URL {
        let vault = tempDir.appendingPathComponent("vault-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        for name in files {
            try "# \(name)\n".write(
                to: vault.appendingPathComponent(name), atomically: true, encoding: .utf8)
        }
        return vault
    }

    // MARK: Mirror contract

    func testSelectionMirrorsIntoWorkspaceModel() async throws {
        let state = makeAppState()
        let vault = try makeVault(files: ["alpha.md", "beta.md"])
        state.openVault(at: vault)
        await state.scanTask?.value

        XCTAssertTrue(state.workspace.model.isEmpty, "no selection → empty workspace")

        state.selectedFilePath = "alpha.md"
        await state.noteLoadTask?.value
        XCTAssertEqual(
            state.workspace.model.activeGroup.activeTab?.item,
            .markdown(path: "alpha.md"))
        XCTAssertEqual(state.workspace.model.allTabs.count, 1, "mirror is single-tab")

        state.selectedFilePath = "beta.md"
        await state.noteLoadTask?.value
        XCTAssertEqual(
            state.workspace.model.activeGroup.activeTab?.item,
            .markdown(path: "beta.md"),
            "selection replaces the tab item in place")
        XCTAssertEqual(state.workspace.model.allTabs.count, 1)

        state.selectedFilePath = nil
        XCTAssertTrue(state.workspace.model.isEmpty, "deselect closes the mirror tab")
        XCTAssertTrue(state.workspace.model.validate().isEmpty)
    }

    func testDirtyGateRollbackDoesNotTouchWorkspace() async throws {
        let state = makeAppState()
        let vault = try makeVault(files: ["alpha.md", "beta.md"])
        state.openVault(at: vault)
        await state.scanTask?.value

        state.selectedFilePath = "alpha.md"
        await state.noteLoadTask?.value
        state.updateEditorText("# alpha\nedited, unsaved")
        XCTAssertTrue(state.hasUnsavedChanges)

        // Attempt to navigate away while dirty: the gate parks it.
        state.selectedFilePath = "beta.md"
        XCTAssertNotNil(state.pendingNavigation, "gate engaged")
        XCTAssertEqual(
            state.workspace.model.activeGroup.activeTab?.item,
            .markdown(path: "alpha.md"),
            "parked navigation never reaches the workspace model")
    }

    func testVaultCloseEmptiesWorkspace() async throws {
        let state = makeAppState()
        let vault = try makeVault(files: ["alpha.md"])
        state.openVault(at: vault)
        await state.scanTask?.value
        state.selectedFilePath = "alpha.md"
        await state.noteLoadTask?.value
        XCTAssertFalse(state.workspace.model.isEmpty)

        state.closeVault()
        XCTAssertTrue(state.workspace.model.isEmpty, "close vault clears the mirror")
        XCTAssertTrue(state.workspace.model.validate().isEmpty)
    }

    // MARK: Presentation-ready render gates (DoD §D smoke)

    func testWorkspaceViewRendersInBothAppearancesEmptyState() {
        let state = makeAppState()
        PresentationReady.assertRendersInBothAppearances(
            WorkspaceView().environmentObject(state))
    }

    func testWorkspaceViewRendersInBothAppearancesPopulated() async throws {
        let state = makeAppState()
        let vault = try makeVault(files: ["alpha.md"])
        state.openVault(at: vault)
        await state.scanTask?.value
        state.selectedFilePath = "alpha.md"
        await state.noteLoadTask?.value
        PresentationReady.assertRendersInBothAppearances(
            WorkspaceView().environmentObject(state))
    }
}
