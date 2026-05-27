// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest

@testable import SlateMac

/// End-to-end "Milestone Q shipped" coverage. Drives the command
/// palette feature through `AppState` from registry construction
/// to recents persistence across a simulated app restart.
///
/// Closes #317. Same shape as `MilestoneIIntegrationTests` (#171),
/// `MilestoneJIntegrationTests` (#189), `MilestoneKIntegrationTests`
/// (#225), and `MilestoneLIntegrationTests` (#283): single fixture
/// vault, single method, all assertions inline.
///
/// Scope: the SwiftUI-level interaction story (search-field focus,
/// Esc dismissing the sheet, the arrow-key NSEvent monitor
/// intercepting real keystrokes) needs XCUITest infra and rides
/// the existing Milestone Q follow-up issues. This suite exercises
/// the model + AppState + registry + recents store layer
/// end-to-end — every load-bearing piece of the milestone that
/// doesn't require live keystroke routing.
///
/// Wall-clock budget: under 5 seconds on local + CI runners.
@MainActor
final class MilestoneQIntegrationTests: XCTestCase {
    private var tempDir: URL!

    /// Suite names leased by `makeAppState` so `tearDownWithError`
    /// can call `removePersistentDomain` on each — preserves the
    /// MilestoneK/L cleanup invariant even across crashes mid-test.
    private var leasedSuiteNames: [String] = []

    override func setUpWithError() throws {
        try super.setUpWithError()
        tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-milestone-q-\(UUID().uuidString)")
        try FileManager.default.createDirectory(
            at: tempDir,
            withIntermediateDirectories: true
        )
    }

    override func tearDownWithError() throws {
        for suiteName in leasedSuiteNames {
            UserDefaults().removePersistentDomain(forName: suiteName)
        }
        leasedSuiteNames.removeAll()
        try? FileManager.default.removeItem(at: tempDir)
        try super.tearDownWithError()
    }

    /// Construct an AppState wired against temp-dir-backed stores
    /// so the test doesn't pollute the real Application Support
    /// location. Returns the AppState + the recents-store file URL
    /// so the simulated-restart phase can construct a fresh
    /// AppState pointed at the same file.
    ///
    /// Tracks the UserDefaults suite name on `self` for cleanup
    /// in `tearDownWithError`.
    private func makeAppState(
        recentsFileURL: URL? = nil
    ) -> (AppState, URL) {
        let suiteName = "slate.milestone-q.\(UUID().uuidString)"
        leasedSuiteNames.append(suiteName)
        let defaults = UserDefaults(suiteName: suiteName)!
        let preferences = PreferencesStore(defaults: defaults)
        let vaultRecents = RecentVaultsStore(
            fileURL: tempDir.appendingPathComponent("recents.json")
        )
        let paletteRecentsFile = recentsFileURL
            ?? tempDir.appendingPathComponent("command-palette-recents.json")
        let paletteRecents = CommandPaletteRecentsStore(fileURL: paletteRecentsFile)
        let state = AppState(
            recentsStore: vaultRecents,
            externalOpener: { _ in true },
            preferencesStore: preferences,
            commandPaletteRecentsStore: paletteRecents
        )
        return (state, paletteRecentsFile)
    }

    func testMilestoneQEndToEndCommandPalette() async throws {
        let start = Date()

        // ============================================================
        // === Phase 1 — Registry populated from menu bridge (#314) ===
        // ============================================================

        let (state, recentsFileURL) = makeAppState()

        let registered = state.commandRegistry.list().map(\.id)
        XCTAssertEqual(
            Set(registered),
            Set(SlateCommandID.all),
            "Phase 1: Registry must mirror SlateCommandID.all exactly; menu drift would fail this"
        )
        XCTAssertEqual(
            registered.count,
            SlateCommandID.all.count,
            "Phase 1: no duplicate registrations"
        )

        // Each registered command must have non-empty label + a
        // well-formed id matching the `slate.<section>.<verb>`
        // convention.
        for command in state.commandRegistry.list() {
            XCTAssertFalse(
                command.label.isEmpty,
                "Phase 1: \(command.id) has empty label"
            )
            XCTAssertTrue(
                command.id.hasPrefix("slate."),
                "Phase 1: \(command.id) doesn't follow slate.<section>.<verb> convention"
            )
        }

        // ============================================================
        // === Phase 2 — Palette open / close lifecycle (#313) ===
        // ============================================================

        XCTAssertFalse(
            state.isCommandPaletteOpen,
            "Phase 2: palette starts closed"
        )
        state.isCommandPaletteOpen = true
        XCTAssertTrue(
            state.isCommandPaletteOpen,
            "Phase 2: palette opens via state mutation"
        )

        // closeVault must reset the palette flag so a vault-close
        // mid-palette doesn't re-trigger on next vault open
        // (red-team finding from #319; lock it in here).
        state.closeVault()
        XCTAssertFalse(
            state.isCommandPaletteOpen,
            "Phase 2: closeVault resets the palette flag so re-open doesn't auto-present"
        )

        // ============================================================
        // === Phase 3 — Fuzzy filter end-to-end (#315) ===
        // ============================================================

        let model = CommandPaletteModel()
        model.loadCommands(
            state.commandRegistry.list(),
            recents: state.commandPaletteRecents
        )

        // Empty query: all commands visible, sorted by section.
        XCTAssertEqual(
            model.displayOrder.count,
            SlateCommandID.all.count,
            "Phase 3: empty query surfaces every registered command"
        )

        // "save" filters to the Save command (and possibly others
        // with 's' + 'a' + 'v' + 'e' subsequence).
        model.query = "save"
        model.handleQueryChange()
        XCTAssertTrue(
            model.filteredCommands.contains { $0.id == SlateCommandID.save },
            "Phase 3: 'save' query must match the Save command"
        )
        XCTAssertEqual(
            model.filteredCommands.first?.id,
            SlateCommandID.save,
            "Phase 3: Save is the strongest match (prefix bonus)"
        )

        // Filter announcement matches the expected template.
        XCTAssertNotNil(
            model.filterAnnouncement,
            "Phase 3: filter change announces"
        )
        XCTAssertTrue(
            model.filterAnnouncement!.contains("matching \"save\""),
            "Phase 3: announcement template; got \(model.filterAnnouncement ?? "nil")"
        )

        // ============================================================
        // === Phase 4 — Section grouping (#316) ===
        // ============================================================

        model.query = "" // reset
        model.handleQueryChange()

        let sections = model.sections
        // No recents yet → no Recent section.
        XCTAssertFalse(
            sections.contains { $0.title == "Recent" },
            "Phase 4: empty recents → no Recent section"
        )
        // Native sections appear in declared order.
        let nativeTitles = sections.map(\.title)
        XCTAssertEqual(
            nativeTitles.first,
            "File",
            "Phase 4: first section is File (declared first in CommandSection)"
        )

        // ============================================================
        // === Phase 5 — Invoke + recents persistence (#314 + #316) ===
        // ============================================================

        XCTAssertEqual(
            state.commandPaletteRecents, [],
            "Phase 5: no recents at start"
        )

        // Invoking AddProperty flips the corresponding bool — the
        // observable side-effect of the menu bridge actually
        // wiring through to AppState.
        XCTAssertFalse(state.isAddPropertySheetOpen)
        try state.commandRegistry.invokeById(id: SlateCommandID.addProperty)
        XCTAssertTrue(
            state.isAddPropertySheetOpen,
            "Phase 5: registry.invokeById fires the same side-effect as the menu item"
        )

        // Recording the invocation surfaces it in recents + writes
        // to disk.
        state.recordCommandInvocation(id: SlateCommandID.addProperty)
        XCTAssertEqual(
            state.commandPaletteRecents,
            [SlateCommandID.addProperty],
            "Phase 5: recordCommandInvocation updates in-memory recents"
        )
        XCTAssertTrue(
            FileManager.default.fileExists(atPath: recentsFileURL.path),
            "Phase 5: recents file written to disk"
        )

        // ============================================================
        // === Phase 6 — Simulated app restart ===
        // ============================================================

        // Drop the original AppState; construct a fresh one
        // pointed at the same recents file. The new instance
        // should see the persisted recent.
        let (state2, _) = makeAppState(recentsFileURL: recentsFileURL)
        XCTAssertEqual(
            state2.commandPaletteRecents,
            [SlateCommandID.addProperty],
            "Phase 6: recents must survive across AppState instances"
        )

        // Second model snapshot sees the same recent.
        let model2 = CommandPaletteModel()
        model2.loadCommands(
            state2.commandRegistry.list(),
            recents: state2.commandPaletteRecents
        )
        let secondSections = model2.sections
        XCTAssertEqual(
            secondSections.first?.title,
            "Recent",
            "Phase 6: Recent section now appears at the top"
        )
        XCTAssertEqual(
            secondSections.first?.commands.first?.id,
            SlateCommandID.addProperty,
            "Phase 6: the previously-invoked command is the only Recent entry"
        )

        // ============================================================
        // === Phase 7 — Wall-clock budget ===
        // ============================================================

        let elapsed = Date().timeIntervalSince(start)
        XCTAssertLessThan(
            elapsed,
            5.0,
            "Phase 7: MilestoneQ integration must complete inside the 5-second budget; took \(elapsed)s"
        )
    }

    // ================================================================
    // === Definition-of-Done sweep — per the #317 issue body ===
    // ================================================================
    //
    // "Every menu item reachable via palette" is asserted by Phase 1
    // of the main test above + the drift check in `SlateCommandsTests`
    // — no dedicated DoD method (it would be a strict subset of
    // Phase 1, adding zero coverage).
    //
    // "Palette fully keyboard-navigable; focus + announcements
    // verified" rides XCUITest infra not yet in the project.
    //
    // The two DoD checkboxes that DO benefit from dedicated tests:

    /// **Recents persist across launches** — full round-trip
    /// through disk via the LRU `add` semantics.
    func testDoDRecentsPersistAcrossSimulatedRestart() async throws {
        let (state, recentsFileURL) = makeAppState()
        state.recordCommandInvocation(id: SlateCommandID.save)
        state.recordCommandInvocation(id: SlateCommandID.toggleSearch)

        let (state2, _) = makeAppState(recentsFileURL: recentsFileURL)
        XCTAssertEqual(
            state2.commandPaletteRecents,
            [SlateCommandID.toggleSearch, SlateCommandID.save],
            "DoD: recents survive in MRU order"
        )
    }

    /// **`CommandRegistry` accepts arbitrary runtime registration**
    /// — the load-bearing FFI-callback shape that V1.x plugin
    /// commands will use to extend the palette.
    ///
    /// Note: this exercises the Swift → Rust registry → Swift
    /// action roundtrip with an arbitrary (non-core) command id.
    /// It does NOT exercise a Rust-side plugin loader path
    /// because no such loader exists yet (V1.x). Renamed from
    /// "PluginStyleRegistration" per #317 red-team finding so the
    /// scope claim matches what the test actually verifies.
    func testDoDRegistryAcceptsArbitraryRuntimeRegistration() async throws {
        let (state, _) = makeAppState()
        let action = StubAction()
        let replaced = state.commandRegistry.register(
            command: Command(
                id: "test.runtime.example",
                label: "Example Runtime Command",
                accessibilityHint: nil,
                hotkeyHint: nil,
                section: .plugins
            ),
            action: action
        )
        XCTAssertFalse(replaced, "DoD: fresh runtime registration does not replace")
        try state.commandRegistry.invokeById(id: "test.runtime.example")
        XCTAssertEqual(
            action.invocationCount,
            1,
            "DoD: runtime-registered action fires through the FFI callback surface"
        )
    }

    // MARK: - Fixtures

    final class StubAction: CommandAction, @unchecked Sendable {
        private let lock = NSLock()
        private var _invocationCount: Int = 0

        var invocationCount: Int {
            lock.lock(); defer { lock.unlock() }
            return _invocationCount
        }

        func invoke() throws {
            lock.lock()
            _invocationCount += 1
            lock.unlock()
        }
    }
}
