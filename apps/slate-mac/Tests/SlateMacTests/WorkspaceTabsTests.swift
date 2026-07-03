// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import SwiftUI
import XCTest

@testable import SlateMac

/// U1-2 (#454): tab lifecycle over the parked-document architecture —
/// snapshot/restore byte-fidelity, dirty travel, same-path mirroring, the
/// tab-close and vault-close gates, and the AX value strings.
@MainActor
final class WorkspaceTabsTests: XCTestCase {

    private var tempDir: URL!

    override func setUpWithError() throws {
        tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("workspace-tabs-\(UUID().uuidString)")
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

    /// Vault with alpha/beta/gamma; opens it, scans, selects alpha.
    private func makeOpenState() async throws -> (AppState, URL) {
        let vault = tempDir.appendingPathComponent("vault-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        for name in ["alpha.md", "beta.md", "gamma.md"] {
            try "# \(name)\nbody of \(name)\n".write(
                to: vault.appendingPathComponent(name), atomically: true, encoding: .utf8)
        }
        let state = makeAppState()
        state.openVault(at: vault)
        await state.scanTask?.value
        state.selectedFilePath = "alpha.md"
        await state.noteLoadTask?.value
        return (state, vault)
    }

    /// The U1-2 multi-file flow: duplicate the active tab (⌘T), then select
    /// another file — the duplicate's item is replaced in place, yielding
    /// two tabs over two files.
    private func openSecondFile(_ state: AppState, path: String) async {
        state.newTab()
        state.selectedFilePath = path
        await state.noteLoadTask?.value
    }

    // MARK: New tab

    func testNewTabDuplicatesActiveItem() async throws {
        let (state, _) = try await makeOpenState()
        state.newTab()
        XCTAssertEqual(state.workspace.model.allTabs.count, 2)
        XCTAssertEqual(
            state.workspace.model.allTabs.map(\.item),
            [.markdown(path: "alpha.md"), .markdown(path: "alpha.md")])
        // Buffer unchanged — same file, fields intact.
        XCTAssertEqual(state.loadedFilePath, "alpha.md")
        XCTAssertNotNil(state.currentNoteText)
    }

    func testNewTabWithoutVaultIsNoOp() {
        let state = makeAppState()
        state.newTab()
        XCTAssertTrue(state.workspace.model.isEmpty)
    }

    // MARK: Snapshot / restore

    func testTabSwitchRestoresDirtyBufferByteIdentical() async throws {
        let (state, _) = try await makeOpenState()
        await openSecondFile(state, path: "beta.md")
        XCTAssertEqual(state.workspace.model.allTabs.count, 2)

        // Dirty beta (active), then switch back to alpha's tab.
        let dirtyBeta = "# beta.md\nEDITED, unsaved ✏️\n"
        state.updateEditorText(dirtyBeta)
        XCTAssertTrue(state.hasUnsavedChanges)
        state.selectPreviousTab()
        await state.noteLoadTask?.value

        XCTAssertEqual(state.loadedFilePath, "alpha.md")
        XCTAssertFalse(state.hasUnsavedChanges, "alpha's tab is clean")

        // Switch forward: beta's dirty buffer restored byte-identical, no
        // disk read (the parked restore path leaves noteLoadTask nil).
        state.selectNextTab()
        XCTAssertEqual(state.loadedFilePath, "beta.md")
        XCTAssertEqual(state.currentNoteText, dirtyBeta)
        XCTAssertTrue(state.hasUnsavedChanges, "dirty state travels with the tab")
    }

    func testTabSwitchBypassesDirtyGate() async throws {
        let (state, _) = try await makeOpenState()
        await openSecondFile(state, path: "beta.md")
        state.updateEditorText("# beta.md\ndirty")
        state.selectPreviousTab()
        XCTAssertNil(
            state.pendingNavigation,
            "switching tabs never prompts — the buffer parks with its tab")
        XCTAssertEqual(state.loadedFilePath, "alpha.md")
    }

    func testSidebarSelectionOfOpenPathSwitchesTabInsteadOfReplacing() async throws {
        let (state, _) = try await makeOpenState()
        await openSecondFile(state, path: "beta.md")
        // Sidebar re-selects alpha (open in the other tab): tab count must
        // stay 2 — this is a switch, not an in-place replace.
        state.selectedFilePath = "alpha.md"
        await state.noteLoadTask?.value
        XCTAssertEqual(state.workspace.model.allTabs.count, 2)
        XCTAssertEqual(state.loadedFilePath, "alpha.md")
        XCTAssertEqual(
            state.workspace.model.activeGroup.activeTab?.item,
            .markdown(path: "alpha.md"))
    }

    // MARK: Same-path mirroring

    func testSamePathTabsMirrorEditsAndSaves() async throws {
        let (state, vault) = try await makeOpenState()
        state.newTab()  // duplicate of alpha
        let parkedID = try XCTUnwrap(
            state.workspace.model.allTabs.first(where: {
                $0.id != state.workspace.model.activeGroup.activeTabID
            })?.id)

        let edited = "# alpha.md\nmirrored edit\n"
        state.updateEditorText(edited)
        let parked = try XCTUnwrap(state.workspace.document(for: parkedID))
        XCTAssertEqual(parked.text, edited, "duplicate tab renders live bytes")
        XCTAssertTrue(parked.hasUnsavedChanges)

        state.saveCurrentNote()
        await state.saveTask?.value
        XCTAssertFalse(parked.hasUnsavedChanges, "save clears the duplicate too")
        XCTAssertEqual(parked.savedBaselineText, edited)
        let onDisk = try String(
            contentsOf: vault.appendingPathComponent("alpha.md"), encoding: .utf8)
        XCTAssertEqual(onDisk, edited)
    }

    // MARK: Close gates

    func testCleanTabClosesImmediatelyAndFocusesSuccessor() async throws {
        let (state, _) = try await makeOpenState()
        await openSecondFile(state, path: "beta.md")
        state.requestCloseTab()
        XCTAssertNil(state.pendingTabClose)
        XCTAssertEqual(state.workspace.model.allTabs.count, 1)
        await state.noteLoadTask?.value
        XCTAssertEqual(state.loadedFilePath, "alpha.md", "left neighbor takes focus")
    }

    func testDirtyTabCloseGatesAndDiscardCloses() async throws {
        let (state, _) = try await makeOpenState()
        await openSecondFile(state, path: "beta.md")
        state.updateEditorText("# beta.md\ndirty")
        state.requestCloseTab()
        XCTAssertNotNil(state.pendingTabClose, "dirty close prompts")
        XCTAssertEqual(state.workspace.model.allTabs.count, 2, "nothing closed yet")

        state.resolveTabCloseDiscard()
        XCTAssertEqual(state.workspace.model.allTabs.count, 1)
        await state.noteLoadTask?.value
        XCTAssertEqual(state.loadedFilePath, "alpha.md")
        XCTAssertFalse(state.hasUnsavedChanges, "discarded buffer is gone")
    }

    func testDirtyTabCloseSaveSavesThenCloses() async throws {
        let (state, vault) = try await makeOpenState()
        await openSecondFile(state, path: "beta.md")
        let dirty = "# beta.md\nsaved on close\n"
        state.updateEditorText(dirty)
        state.requestCloseTab()
        state.resolveTabCloseSave()
        await state.saveTask?.value

        XCTAssertEqual(state.workspace.model.allTabs.count, 1, "closed after save")
        let onDisk = try String(
            contentsOf: vault.appendingPathComponent("beta.md"), encoding: .utf8)
        XCTAssertEqual(onDisk, dirty)
        await state.noteLoadTask?.value
        XCTAssertEqual(state.loadedFilePath, "alpha.md")
    }

    func testDirtyParkedTabCloseGatesToo() async throws {
        let (state, _) = try await makeOpenState()
        await openSecondFile(state, path: "beta.md")
        state.updateEditorText("# beta.md\ndirty parked")
        let betaTab = try XCTUnwrap(state.workspace.model.activeGroup.activeTabID)
        state.selectPreviousTab()  // park dirty beta
        await state.noteLoadTask?.value

        state.requestCloseTab(betaTab)
        XCTAssertEqual(state.pendingTabClose, betaTab, "parked dirty close prompts")
        state.resolveTabCloseCancel()
        XCTAssertEqual(state.workspace.model.allTabs.count, 2)
    }

    func testPendingCloseAfterSaveDoesNotLeakAcrossTabSwitch() async throws {
        // Codoki #492 (High): choose Save on the close prompt, switch tabs
        // before the save lands, come back, save again — the tab must NOT
        // close on that later unrelated save.
        let (state, _) = try await makeOpenState()
        await openSecondFile(state, path: "beta.md")
        state.updateEditorText("# beta.md\ndirty")
        let betaTab = try XCTUnwrap(state.workspace.model.activeGroup.activeTabID)

        state.requestCloseTab()
        state.resolveTabCloseSave()
        // Switch away BEFORE awaiting the in-flight save (the save's
        // main-actor continuation can't run until we yield).
        state.selectPreviousTab()
        await state.saveTask?.value
        await state.noteLoadTask?.value

        XCTAssertEqual(
            state.workspace.model.allTabs.count, 2,
            "close skipped — the user moved on mid-save")
        XCTAssertNil(state.pendingTabCloseAfterSave, "scope cleared on switch")

        // Return to beta and do an ordinary save: the tab must survive.
        state.selectTab(id: betaTab)
        state.updateEditorText("# beta.md\nlater edit")
        state.saveCurrentNote()
        await state.saveTask?.value
        XCTAssertEqual(
            state.workspace.model.allTabs.count, 2,
            "a later unrelated save must not close the tab")
    }

    // MARK: Vault-close aggregation

    func testVaultCloseAggregatesParkedDirtyTabs() async throws {
        let (state, _) = try await makeOpenState()
        await openSecondFile(state, path: "beta.md")
        state.updateEditorText("# beta.md\ndirty")
        state.selectPreviousTab()  // park dirty beta; alpha active clean
        await state.noteLoadTask?.value

        state.closeVaultFromUserAction()
        XCTAssertEqual(state.pendingVaultClose, 1, "one dirty tab found")
        XCTAssertNotNil(state.currentSession, "vault still open behind the prompt")
        state.resolveVaultCloseCancel()
        XCTAssertNil(state.pendingVaultClose)
    }

    func testVaultCloseSaveAllSavesEveryDirtyTabThenCloses() async throws {
        let (state, vault) = try await makeOpenState()
        await openSecondFile(state, path: "beta.md")
        state.updateEditorText("# beta.md\nsave-all beta\n")
        state.selectPreviousTab()
        await state.noteLoadTask?.value
        state.updateEditorText("# alpha.md\nsave-all alpha\n")

        state.closeVaultFromUserAction()
        XCTAssertEqual(state.pendingVaultClose, 2)
        state.resolveVaultCloseSaveAll()
        await state.vaultCloseSaveAllTask?.value

        XCTAssertNil(state.currentSession, "vault closed after save-all")
        XCTAssertTrue(state.workspace.model.isEmpty)
        let alpha = try String(
            contentsOf: vault.appendingPathComponent("alpha.md"), encoding: .utf8)
        let beta = try String(
            contentsOf: vault.appendingPathComponent("beta.md"), encoding: .utf8)
        XCTAssertEqual(alpha, "# alpha.md\nsave-all alpha\n")
        XCTAssertEqual(beta, "# beta.md\nsave-all beta\n")
    }

    // MARK: Ordinal + reorder commands

    func testOrdinalAndReorderCommands() async throws {
        let (state, _) = try await makeOpenState()
        await openSecondFile(state, path: "beta.md")
        await openSecondFile(state, path: "gamma.md")
        XCTAssertEqual(state.workspace.model.allTabs.count, 3)

        state.selectTab(ordinal: 1)
        await state.noteLoadTask?.value
        XCTAssertEqual(state.loadedFilePath, "alpha.md")

        state.selectTab(ordinal: 9)
        await state.noteLoadTask?.value
        XCTAssertEqual(state.loadedFilePath, "gamma.md", "9 = last")

        state.moveActiveTabLeft()
        XCTAssertEqual(
            state.workspace.model.activeGroup.tabs.map(\.item),
            [
                .markdown(path: "alpha.md"), .markdown(path: "gamma.md"),
                .markdown(path: "beta.md"),
            ])
        state.moveActiveTabRight()
        XCTAssertEqual(
            state.workspace.model.activeGroup.tabs.map(\.item),
            [
                .markdown(path: "alpha.md"), .markdown(path: "beta.md"),
                .markdown(path: "gamma.md"),
            ])
    }

    // MARK: AX strings + render gates

    func testTabAccessibilityValueStrings() {
        XCTAssertEqual(
            TabBarView.accessibilityValue(index: 1, count: 5, isDirty: false),
            "tab 2 of 5")
        XCTAssertEqual(
            TabBarView.accessibilityValue(index: 0, count: 1, isDirty: true),
            "tab 1 of 1, edited")
    }

    func testTabBarRendersInBothAppearances() async throws {
        let (state, _) = try await makeOpenState()
        await openSecondFile(state, path: "beta.md")
        state.updateEditorText("# beta.md\ndirty")
        PresentationReady.assertRendersInBothAppearances(
            TabBarView(group: state.workspace.model.activeGroup)
                .environmentObject(state))
    }

    func testTabBarContrastPairings() {
        // The strip's text-on-surface pairings ride the token registry —
        // re-assert the floor here so a strip-specific token change can't
        // slip below it (DoD §D).
        PresentationReady.assertContrastFloor([
            ("tab title on strip", .tokenTextPrimary, .tokenSurfaceSecondary),
            ("inactive tab title on strip", .tokenTextSecondary, .tokenSurfaceSecondary),
            ("active tab title on tab fill", .tokenTextPrimary, .tokenSurface),
        ])
    }
}
