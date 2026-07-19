// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// AppState-level tests for the quick switcher (#495): the open gate,
/// file-recency recording at the `openFile` choke point (and NOT on
/// launch restore), the three activation chords routing to the right
/// `OpenTarget`, per-vault recents persistence, and the vault-close
/// reset. Uses the same real-vault harness as `WorkspaceOpenTargetsTests`.
@MainActor
final class QuickSwitcherActivationTests: XCTestCase {

    private var tempDir: URL!

    override func setUpWithError() throws {
        tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("quick-switcher-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: tempDir, withIntermediateDirectories: true)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: tempDir)
    }

    private func makeVault() throws -> URL {
        let vault = tempDir.appendingPathComponent("vault-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        for name in ["alpha.md", "beta.md", "gamma.md"] {
            try "# \(name)\n".write(
                to: vault.appendingPathComponent(name), atomically: true, encoding: .utf8)
        }
        return vault
    }

    private func makeOpenState(vault: URL) async throws -> AppState {
        let store = RecentVaultsStore(fileURL: tempDir.appendingPathComponent("recents.json"))
        let state = AppState(recentsStore: store, externalOpener: { _ in true })
        state.openVault(at: vault)
        await state.scanTask?.value
        state.selectedFilePath = "alpha.md"
        await state.noteLoadTask?.value
        return state
    }

    // MARK: - Open gate

    func testOpenQuickSwitcherWithNoVaultFallsThroughToVaultPicker() {
        // #863 nicety: ⌘O on the welcome screen must never be dead —
        // with no vault, openQuickSwitcher() routes to the vault
        // picker instead of flipping the (unmounted) sheet's bool.
        let state = AppState()
        XCTAssertFalse(state.isVaultOpen)
        var pickerInvocations = 0
        state.vaultPicker = {
            pickerInvocations += 1
            return nil  // user cancels the panel
        }
        state.openQuickSwitcher()
        XCTAssertEqual(pickerInvocations, 1, "no vault → the picker is the fallthrough")
        XCTAssertFalse(
            state.isQuickSwitcherOpen,
            "the switcher bool must never flip with no vault (the #313/#328 stuck-bool hazard)")
    }

    func testIsQuickSwitcherOpenDefaultsToFalse() {
        XCTAssertFalse(AppState().isQuickSwitcherOpen)
    }

    func testOpenQuickSwitcherWithVaultOpens() async throws {
        let state = try await makeOpenState(vault: try makeVault())
        state.openQuickSwitcher()
        XCTAssertTrue(state.isQuickSwitcherOpen)
    }

    // MARK: - Recording at the openFile choke point

    func testOpeningAFileRecordsItInRecents() async throws {
        let state = try await makeOpenState(vault: try makeVault())
        // Recording happens synchronously inside openFile.
        state.openFile("beta.md", target: .currentTab)
        XCTAssertEqual(
            state.fileRecents.first, "beta.md",
            "an open records the path at the front of file-recents")
    }

    func testReopeningMovesToFrontWithoutDuplicating() async throws {
        let state = try await makeOpenState(vault: try makeVault())
        state.openFile("beta.md", target: .currentTab)
        state.openFile("gamma.md", target: .newTab)
        state.openFile("beta.md", target: .currentTab)
        XCTAssertEqual(
            state.fileRecents, ["beta.md", "gamma.md"],
            "re-opening beta moves it to the front; no duplicate entry")
    }

    /// Launch-time workspace restore uses `activateTab`, not `openFile`,
    /// so it must NOT churn recency. First session persists a workspace
    /// layout whose active tab is `beta.md`, but seeds the file-recents
    /// with a DIFFERENT order (gamma-first). On reopen, the restore
    /// activates beta.md — if it wrongly recorded, beta would jump to the
    /// front. The recents must instead reload exactly as persisted.
    func testLaunchRestoreDoesNotRecord() async throws {
        let vault = try makeVault()
        // First session: make beta.md the active/restored tab, then
        // deliberately set the file-recents to a gamma-first order that
        // does NOT lead with beta.
        let first = try await makeOpenState(vault: vault)
        first.openFile("beta.md", target: .newTab)
        await first.noteLoadTask?.value
        first.saveWorkspaceLayout()
        first.closeVault()
        // Overwrite the persisted recents so the "did restore prepend
        // beta?" signal is unambiguous (beta is NOT first here).
        var info = stat()
        XCTAssertEqual(vault.path.withCString { stat($0, &info) }, 0)
        FileRecentsStore(
            vaultRoot: vault,
            identity: .init(
                device: UInt64(info.st_dev), inode: UInt64(info.st_ino))
        ).save(["gamma.md", "alpha.md"])

        let second = AppState(
            recentsStore: RecentVaultsStore(
                fileURL: tempDir.appendingPathComponent("recents2.json")),
            externalOpener: { _ in true })
        second.openVault(at: vault)
        await second.scanTask?.value
        XCTAssertEqual(
            second.fileRecents, ["gamma.md", "alpha.md"],
            "restoring the persisted beta.md tab via activateTab must not record it — "
                + "the recents reload exactly as persisted, with beta not prepended")
    }

    // MARK: - Activation chords → OpenTarget

    func testCurrentTabChordReplacesInPlace() async throws {
        let state = try await makeOpenState(vault: try makeVault())
        state.openFile("beta.md", target: .currentTab)
        await state.noteLoadTask?.value
        XCTAssertEqual(state.loadedFilePath, "beta.md")
        XCTAssertEqual(state.workspace.model.allTabs.count, 1, "current-tab reuses the tab")
    }

    func testNewTabChordOpensSecondTab() async throws {
        let state = try await makeOpenState(vault: try makeVault())
        state.openFile("beta.md", target: .newTab)
        await state.noteLoadTask?.value
        XCTAssertEqual(state.workspace.model.allTabs.count, 2)
        XCTAssertEqual(state.loadedFilePath, "beta.md")
    }

    func testNewSplitChordOpensSecondPane() async throws {
        let state = try await makeOpenState(vault: try makeVault())
        state.openFile("beta.md", target: .newSplit(.horizontal))
        await state.noteLoadTask?.value
        XCTAssertEqual(state.workspace.model.groupsInOrder.count, 2)
        XCTAssertEqual(state.loadedFilePath, "beta.md", "the new pane shows the opened file")
    }

    // MARK: - Per-vault persistence

    func testRecentsPersistPerVaultAndReloadOnReopen() async throws {
        let vault = try makeVault()
        let first = try await makeOpenState(vault: vault)
        first.openFile("beta.md", target: .currentTab)
        first.openFile("gamma.md", target: .currentTab)
        await first.noteLoadTask?.value
        first.closeVault()
        XCTAssertEqual(first.fileRecents, [], "closeVault clears the in-memory list")

        // Reopen the same vault → its file-recents.json reloads.
        let second = AppState(
            recentsStore: RecentVaultsStore(
                fileURL: tempDir.appendingPathComponent("recents2.json")),
            externalOpener: { _ in true })
        second.openVault(at: vault)
        await second.scanTask?.value
        XCTAssertEqual(
            second.fileRecents, ["gamma.md", "beta.md"],
            "reopening the vault reloads its persisted, most-recent-first file recents")
    }

    // MARK: - Vault-close reset

    func testCloseVaultResetsIsQuickSwitcherOpen() {
        let state = AppState()
        state.isQuickSwitcherOpen = true
        state.closeVault()
        XCTAssertFalse(
            state.isQuickSwitcherOpen,
            "closeVault resets the switcher bool so the next vault open doesn't auto-present it")
    }
}
