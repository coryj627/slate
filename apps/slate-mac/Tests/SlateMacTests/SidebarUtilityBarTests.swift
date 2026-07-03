// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import SwiftUI
import XCTest

@testable import SlateMac

/// U4-3 (#472): the bottom-left utility bar — Settings, Help, and the vault
/// switcher.
///
/// The behavioural core is unit-testable and lives here: Help routes its URL
/// through a recording `externalOpener`; the vault switcher's recents-menu
/// state (checkmark + disabled on the current vault) is a pure predicate; the
/// switch-to-recent close-then-open goes through the dirty gate and a cancelled
/// prompt cancels the switch. What has no XCTest-introspectable surface — the
/// rendered SwiftUI AX tree (labels / traits / help), Dynamic-Type reflow — is
/// pinned structurally against the source (the technique `RightPaneViewTests`
/// and `CloseVaultSheetParityTests` already trust) and covered behaviourally by
/// the VoiceOver runbook. The `PresentationReady` harness renders the bar in
/// both appearances and measures its contrast. This split is honest by design —
/// a fake AX-tree assertion would give false confidence.
@MainActor
final class SidebarUtilityBarTests: XCTestCase {

    private var tempDir: URL!

    override func setUpWithError() throws {
        try super.setUpWithError()
        tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-utilbar-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: tempDir, withIntermediateDirectories: true)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: tempDir)
        try super.tearDownWithError()
    }

    // MARK: - Fixtures

    /// A vault directory with a single note (enough for `openVault` to scan and
    /// for a recents entry to be recorded).
    private func makeVault(_ name: String) throws -> URL {
        let vault = tempDir.appendingPathComponent(name)
        try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        try "# \(name)\n".write(
            to: vault.appendingPathComponent("note.md"), atomically: true, encoding: .utf8)
        return vault
    }

    private func makeState(externalOpener: @escaping (URL) -> Bool = { _ in true }) -> AppState {
        AppState(
            recentsStore: RecentVaultsStore(
                fileURL: tempDir.appendingPathComponent("recents-\(UUID().uuidString).json")),
            externalOpener: externalOpener)
    }

    // MARK: - Help action routing (recording externalOpener)

    /// The Help button / `slate.help.open` command hands the repository README
    /// URL to the injected `externalOpener` — the same hand-off outgoing links
    /// use — and records success in `lastActivatedLinkOutcome`. The recording
    /// closure stands in for `NSWorkspace.open` so no browser is spawned.
    func testOpenHelpRoutesReadmeURLThroughExternalOpener() {
        var opened: [URL] = []
        let state = makeState(externalOpener: { url in
            opened.append(url)
            return true
        })

        state.openHelp()

        XCTAssertEqual(opened, [AppState.helpURL], "Help must open exactly the README URL")
        XCTAssertEqual(AppState.helpURL.absoluteString, "https://github.com/coryj627/slate#readme")
        XCTAssertEqual(
            state.lastActivatedLinkOutcome, .openedExternal(AppState.helpURL.absoluteString))
    }

    /// A failed open (the opener returns false — no registered handler) records
    /// the failure outcome rather than a phantom success.
    func testOpenHelpRecordsFailureWhenOpenerReturnsFalse() {
        let state = makeState(externalOpener: { _ in false })
        state.openHelp()
        XCTAssertEqual(
            state.lastActivatedLinkOutcome, .externalOpenFailed(AppState.helpURL.absoluteString))
    }

    /// Routing through the command registry hits the same implementation as the
    /// button (one implementation, two entry points): invoking `slate.help.open`
    /// opens the same URL through the same spy.
    func testHelpCommandInvocationRoutesThroughSameOpener() throws {
        var opened: [URL] = []
        let state = makeState(externalOpener: { url in
            opened.append(url)
            return true
        })

        try state.commandRegistry.invokeById(id: SlateCommandID.openHelp)

        XCTAssertEqual(opened, [AppState.helpURL])
    }

    // MARK: - Vault switcher: recents-menu state

    /// The menu lists the recents; the current vault's row is the one whose path
    /// matches `currentVaultURL` — the view checkmarks + disables exactly that
    /// row. Asserted as the predicate the view uses (`entry.path ==
    /// currentVaultURL?.path`) over a two-vault recents list.
    func testVaultSwitcherMarksTheCurrentVaultRow() async throws {
        let vaultA = try makeVault("A")
        let vaultB = try makeVault("B")
        let state = makeState()

        state.openVault(at: vaultA)
        await state.scanTask?.value
        state.openVault(at: vaultB)  // B is now current; both are in recents
        await state.scanTask?.value

        XCTAssertEqual(
            Set(state.recentVaults.map(\.path)), Set([vaultA.path, vaultB.path]),
            "both opened vaults are in recents")

        let current = state.recentVaults.filter { $0.path == state.currentVaultURL?.path }
        XCTAssertEqual(current.map(\.path), [vaultB.path], "exactly the open vault is 'current'")
        // The other recent is NOT current → it stays enabled (a switch target).
        let others = state.recentVaults.filter { $0.path != state.currentVaultURL?.path }
        XCTAssertEqual(others.map(\.path), [vaultA.path])
    }

    // MARK: - Vault switcher: switch through the dirty gate

    /// Switching to a recent while the editor is CLEAN closes the current vault
    /// and opens the target immediately — no prompt.
    func testSwitchToRecentCleanClosesThenOpens() async throws {
        let vaultA = try makeVault("A")
        let vaultB = try makeVault("B")
        let state = makeState()

        state.openVault(at: vaultA)
        await state.scanTask?.value
        // Seed a recents entry for B without making it current.
        let entryB = RecentVault(url: vaultB)

        XCTAssertFalse(state.hasUnsavedChanges, "clean editor precondition")
        state.switchToRecent(entryB)
        await state.scanTask?.value

        XCTAssertEqual(state.currentVaultURL?.path, vaultB.path, "switched to B")
        XCTAssertNil(state.pendingVaultSwitchTarget, "the parked target is consumed")
        XCTAssertNil(state.pendingNavigation, "no gate prompt for a clean switch")
    }

    /// Load `note.md` in the open vault and dirty it through the REAL editor
    /// path (`updateEditorText`) so `hasUnsavedChanges` reflects genuine unsaved
    /// state — the setter is `private(set)`, so tests can't fake it.
    private func loadAndDirty(_ state: AppState) async {
        state.selectedFilePath = "note.md"
        await state.noteLoadTask?.value
        state.updateEditorText("# edited\n\nunsaved body.\n")
        XCTAssertTrue(state.hasUnsavedChanges, "note must be dirty for the gate to engage")
    }

    /// Switching while DIRTY raises the "Save changes?" gate and parks the
    /// target; the current vault is still open (nothing closed yet).
    func testSwitchToRecentDirtyRaisesGateAndParksTarget() async throws {
        let vaultA = try makeVault("A")
        let vaultB = try makeVault("B")
        let state = makeState()

        state.openVault(at: vaultA)
        await state.scanTask?.value
        await loadAndDirty(state)

        state.switchToRecent(RecentVault(url: vaultB))

        XCTAssertEqual(state.pendingNavigation, .closeVault, "dirty switch raises the close gate")
        XCTAssertEqual(
            state.pendingVaultSwitchTarget?.path, vaultB.path, "the switch target is parked")
        XCTAssertEqual(state.currentVaultURL?.path, vaultA.path, "nothing closed while the gate is up")
    }

    /// Cancelling the gate cancels the whole switch: the parked target is
    /// dropped, the current vault stays open, and the dirty buffer is intact.
    func testCancellingGateCancelsTheSwitch() async throws {
        let vaultA = try makeVault("A")
        let vaultB = try makeVault("B")
        let state = makeState()

        state.openVault(at: vaultA)
        await state.scanTask?.value
        await loadAndDirty(state)
        state.switchToRecent(RecentVault(url: vaultB))
        XCTAssertEqual(state.pendingNavigation, .closeVault)

        state.resolvePendingNavigationCancel()

        XCTAssertNil(state.pendingVaultSwitchTarget, "cancel drops the switch target")
        XCTAssertNil(state.pendingNavigation, "the gate is dismissed")
        XCTAssertEqual(state.currentVaultURL?.path, vaultA.path, "still on A")
        XCTAssertTrue(state.hasUnsavedChanges, "the unsaved buffer survives the cancel")
    }

    /// After a cancelled switch, a subsequent PLAIN Close Vault must not inherit
    /// the dropped target and silently reopen a vault — it just closes.
    func testPlainCloseAfterCancelledSwitchDoesNotReopen() async throws {
        let vaultA = try makeVault("A")
        let vaultB = try makeVault("B")
        let state = makeState()

        state.openVault(at: vaultA)
        await state.scanTask?.value
        await loadAndDirty(state)
        state.switchToRecent(RecentVault(url: vaultB))
        state.resolvePendingNavigationCancel()

        // Revert the edit to baseline (clears the dirty flag through the real
        // path) and plain-close: the close must land on the welcome screen, not
        // silently reopen B via a stale parked target.
        state.updateEditorText(state.savedBaselineText ?? "")
        XCTAssertFalse(state.hasUnsavedChanges, "reverting to baseline clears dirty")
        state.closeVaultFromUserAction()

        XCTAssertNil(state.currentVaultURL, "plain close returns to welcome, not into B")
    }

    /// Switching to the already-open vault is a no-op (the menu also disables
    /// that row; this guards the programmatic path).
    func testSwitchToCurrentVaultIsNoOp() async throws {
        let vaultA = try makeVault("A")
        let state = makeState()
        state.openVault(at: vaultA)
        await state.scanTask?.value

        state.switchToRecent(RecentVault(url: vaultA))

        XCTAssertEqual(state.currentVaultURL?.path, vaultA.path, "still on A")
        XCTAssertNil(state.pendingVaultSwitchTarget)
        XCTAssertNil(state.pendingNavigation)
    }

    // MARK: - AX + action wiring (structural)

    /// The bar is one AX container labeled "Vault utilities"; each control is
    /// labeled and carries a `.help` tooltip; the Settings button sends the
    /// exact `showSettingsWindow:` selector the command action uses; the vault
    /// switcher is a keyboard-operable `Menu`. No rendered-AX-tree API exists,
    /// so this pins the source expressions that produce those — the technique
    /// the panel-stack / rail wiring tests use.
    func testUtilityBarSourceCarriesContainerLabelsAndActions() throws {
        let source = try utilityBarSource()

        // Container.
        XCTAssertTrue(source.contains("accessibilityElement(children: .contain)"))
        XCTAssertTrue(source.contains(#"accessibilityLabel("Vault utilities")"#))

        // Every control labeled + tooltip'd.
        for label in ["Settings", "Help", "Switch vault"] {
            XCTAssertTrue(
                source.contains(#"accessibilityLabel("\#(label)")"#),
                "missing accessibilityLabel for \(label)")
            XCTAssertTrue(
                source.contains(#".help("\#(label)"#), "missing help tooltip for \(label)")
        }

        // Settings sends the SAME selector the command action sends.
        XCTAssertTrue(
            source.contains(#"Selector(("showSettingsWindow:"))"#),
            "Settings must send showSettingsWindow: — one implementation, two entry points")

        // Help routes through AppState (→ externalOpener), not a raw NSWorkspace
        // call the tests couldn't spy on.
        XCTAssertTrue(source.contains("appState.openHelp()"))

        // Vault switcher is a Menu (keyboard-operable) and lists recents +
        // Open Other + Close Vault (through the unchanged dirty-gate helper).
        XCTAssertTrue(source.contains("Menu {"))
        XCTAssertTrue(source.contains("appState.switchToRecent(entry)"))
        XCTAssertTrue(source.contains("appState.pickAndOpenVault()"))
        XCTAssertTrue(source.contains("appState.closeVaultFromUserAction()"))
        XCTAssertTrue(
            source.contains(#"Open Other Vault…"#) && source.contains(#"Close Vault"#))
    }

    /// The glyph sizing is the Dynamic-Type-safe idiom the a11y gate requires
    /// (semantic style + `.imageScale`), never a frozen `.system(size:)`, and
    /// the targets are ≥ 36×32.
    func testUtilityBarUsesDynamicTypeSafeGlyphSizing() throws {
        let source = try utilityBarSource()
        XCTAssertTrue(source.contains(".font(.title2)"), "must use a semantic type style")
        XCTAssertTrue(source.contains(".imageScale(.large)"), "must scale with Dynamic Type")
        // No frozen-point-size ACTUATION (`.font(.system(size:…))`). The doc
        // comment mentions the anti-pattern by name, so match the real call
        // shape, not a bare substring.
        XCTAssertFalse(
            source.contains(".font(.system(size:"),
            "a frozen point size is rejected by the a11y gate")
        // 36pt-tall bar (spec §U4-3). The height is a named constant, so assert
        // the constant's value rather than a magic-number literal.
        XCTAssertTrue(
            source.contains("barHeight: CGFloat = 36"),
            "the bar's fixed height must be 36pt")
    }

    // MARK: - PresentationReady (§D contrast + §E render, both appearances)

    /// The bar's rest glyph tint (`textSecondary`) on its `surface` background
    /// clears the project APCA floor in both appearances.
    func testUtilityBarGlyphClearsContrastFloor() {
        PresentationReady.assertContrastFloor([
            ("utility glyph (textSecondary on surface)", .tokenTextSecondary, .tokenSurface)
        ])
    }

    /// The bar renders to a finite, non-empty size in both appearances — a
    /// smoke test over the real view that catches per-appearance crashes.
    func testUtilityBarRendersInBothAppearances() {
        let state = makeState()
        let view = SidebarUtilityBar().environmentObject(state)
        PresentationReady.assertRendersInBothAppearances(view)
    }

    // MARK: - internals

    /// Locate and read `SidebarUtilityBar.swift` from the test file's path (the
    /// module doesn't ship its own source; walk up like the sidebar/rail wiring
    /// tests do).
    private func utilityBarSource() throws -> String {
        var cursor = URL(fileURLWithPath: #filePath).deletingLastPathComponent()
        for _ in 0..<8 {
            let candidate = cursor.appendingPathComponent(
                "Sources/SlateMac/SidebarUtilityBar.swift")
            if FileManager.default.fileExists(atPath: candidate.path) {
                return try String(contentsOf: candidate, encoding: .utf8)
            }
            cursor = cursor.deletingLastPathComponent()
        }
        XCTFail("Could not locate SidebarUtilityBar.swift from \(#filePath)")
        return ""
    }
}
