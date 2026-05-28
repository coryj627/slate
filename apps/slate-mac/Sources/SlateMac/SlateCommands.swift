// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import Foundation

/// Stable identifiers for every core command surfaced through the
/// command palette (Milestone Q #314).
///
/// **Stability contract:** once an id ships, changing it is a
/// breaking change for users' keybindings and recents (#316). Only
/// add new ids; never rename. The drift test in
/// `SlateCommandsTests` asserts every id here resolves to a
/// registered `Command`.
///
/// Naming convention: `slate.<section>.<verb>`. The section
/// matches the corresponding `CommandSection` so a reader can map
/// an id to its palette grouping at a glance.
enum SlateCommandID {
    // File
    static let newFromTemplate = "slate.file.newFromTemplate"

    // Navigation
    static let jumpToBibliography = "slate.navigation.jumpToBibliography"

    // View
    static let toggleSearch = "slate.view.toggleSearch"

    // Vault
    static let openVault = "slate.vault.open"
    static let closeVault = "slate.vault.close"

    // Editor
    static let save = "slate.editor.save"
    static let citationSummary = "slate.editor.citationSummary"
    static let addProperty = "slate.editor.addProperty"
    static let bulkRenameProperties = "slate.editor.bulkRenameProperties"

    // Settings
    static let openSettings = "slate.settings.open"

    // Tasks
    static let tasksReview = "slate.tasks.review"

    /// All core command ids, in the order they're registered. The
    /// drift test consumes this array to enforce that every id has
    /// a matching `Command` in the registry — future menu additions
    /// without a registration here fail the test loudly.
    static let all: [String] = [
        newFromTemplate,
        jumpToBibliography,
        toggleSearch,
        openVault,
        closeVault,
        save,
        citationSummary,
        addProperty,
        bulkRenameProperties,
        openSettings,
        tasksReview,
    ]
}

/// Tiny wrapper that adapts a Swift closure to the FFI
/// `CommandAction` protocol.
///
/// The captured closure is `@MainActor` so it can safely touch
/// `AppState` (which is itself `@MainActor`). `invoke()` is called
/// from the Rust registry on whatever thread invoked `invoke_by_id`
/// — we hop to the main queue when needed and use
/// `MainActor.assumeIsolated` to satisfy the closure's isolation
/// dynamically.
///
/// `@unchecked Sendable` because the Rust `CommandAction` trait is
/// `Send + Sync`. The stored `action` is immutable (`let`), the
/// closure itself is main-actor-isolated, and the only way to call
/// it is through `invoke()`'s dispatch-to-main path — so the
/// "unchecked" claim is satisfied by construction.
///
/// Closures typically weak-capture `appState` to avoid the
/// `appState → registry → action → appState` retain cycle.
final class MenuCommandAction: CommandAction, @unchecked Sendable {
    private let action: @MainActor () -> Void

    init(_ action: @escaping @MainActor () -> Void) {
        self.action = action
    }

    func invoke() throws {
        if Thread.isMainThread {
            MainActor.assumeIsolated { action() }
        } else {
            // Block until the main queue runs the action — matches
            // the synchronous shape of `invoke_by_id` on the Rust
            // side. We never invoke from a background thread in
            // current usage (palette button is main-thread), so
            // this branch is a defensive guard for future callers —
            // the CLI / HTTP API extensibility tiers (V1.x), per
            // `docs/plans/05_locked_architecture_decisions.md` §10
            // (Extensibility model).
            DispatchQueue.main.sync {
                MainActor.assumeIsolated { self.action() }
            }
        }
    }
}

/// Wire every existing menu item exposed by `MainSplitView`,
/// `SlateMacApp`, and `PropertiesPanel` into the `CommandRegistry`
/// so the palette mirrors the menus. Called once from
/// `AppState.init` after `commandRegistry` is initialized.
///
/// Each registration calls into the same `appState` method the
/// menu item already invokes — the menu and the palette are now
/// two surfaces over one action vocabulary. The drift-check test
/// (`SlateCommandsTests.testEveryDeclaredCommandIDIsRegistered`)
/// enforces that the registry stays in sync with `SlateCommandID`.
///
/// Skipped intentionally: `slate.view.showCommandPalette` — having
/// the palette list itself is self-referential and adds no value.
@MainActor
func registerCoreCommands(into registry: CommandRegistry, appState: AppState) {
    // Helper that registers and asserts non-replacement. A `true`
    // return here would mean a duplicate id within this function
    // (programmer error) — crash loudly in debug so the regression
    // surfaces during the first run, not silently in production.
    func register(
        _ id: String,
        label: String,
        section: CommandSection,
        hotkey: String? = nil,
        hint: String? = nil,
        action: @escaping @MainActor () -> Void
    ) {
        let replaced = registry.register(
            command: Command(
                id: id,
                label: label,
                accessibilityHint: hint,
                hotkeyHint: hotkey,
                section: section
            ),
            action: MenuCommandAction(action)
        )
        assert(!replaced, "duplicate command id during core registration: \(id)")
    }

    // ----- File -----

    register(
        SlateCommandID.newFromTemplate,
        label: "New from Template…",
        section: .file,
        hotkey: "⇧⌘N",
        hint: "Open the template picker to create a new note."
    ) { [weak appState] in appState?.openTemplatePicker() }

    // ----- Navigation -----

    register(
        SlateCommandID.jumpToBibliography,
        label: "Jump to Bibliography",
        section: .navigation,
        hotkey: "⌘J",
        hint: "Filter the Bibliography sidebar to the expanded citation's key."
    ) { [weak appState] in appState?.jumpToBibliographyFromExpandedCitation() }

    // ----- View -----

    register(
        SlateCommandID.toggleSearch,
        label: "Search",
        section: .view,
        hotkey: "⌘F",
        hint: "Toggle the vault-wide search overlay."
    ) { [weak appState] in appState?.toggleSearchOverlay() }

    // ----- Vault -----

    register(
        SlateCommandID.openVault,
        label: "Open Vault…",
        section: .vault,
        hotkey: "⌘O",
        hint: "Show the open-folder picker."
    ) { [weak appState] in appState?.pickAndOpenVault() }

    register(
        SlateCommandID.closeVault,
        label: "Close Vault",
        section: .vault,
        hint: "Close the current vault and return to the welcome screen."
    ) { [weak appState] in
        // Shared helper with the MainSplitView toolbar button so
        // both surfaces post the same VoiceOver announcement and
        // route the dirty path identically.
        appState?.closeVaultFromUserAction()
    }

    // ----- Editor -----

    register(
        SlateCommandID.save,
        label: "Save",
        section: .editor,
        hotkey: "⌘S",
        hint: "Save the current note to disk."
    ) { [weak appState] in appState?.saveCurrentNote() }

    register(
        SlateCommandID.citationSummary,
        label: "Citation Summary",
        section: .editor,
        hotkey: "⇧⌘J",
        hint: "Open the citation summary for the current note."
    ) { [weak appState] in appState?.isCitationSummaryOpen = true }

    register(
        SlateCommandID.addProperty,
        label: "Add Property…",
        section: .editor,
        hint: "Add a new frontmatter property to the current note."
    ) { [weak appState] in appState?.isAddPropertySheetOpen = true }

    register(
        SlateCommandID.bulkRenameProperties,
        label: "Bulk Rename Properties…",
        section: .editor,
        hotkey: "⇧⌘R",
        hint: "Open the bulk-rename sheet to rename a property across the vault."
    ) { [weak appState] in appState?.isBulkRenameSheetOpen = true }

    // ----- Settings -----

    register(
        SlateCommandID.openSettings,
        label: "Settings…",
        section: .settings,
        hotkey: "⌘,",
        hint: "Open the Settings window."
    ) {
        // SwiftUI's `Settings { ... }` scene auto-installs the
        // "Slate ▸ Settings…" menu item + ⌘, chord and registers
        // an `NSApplication` responder for `showSettingsWindow:`.
        // macOS 13 (our deployment target) has no public API to
        // open that window programmatically, so we send the same
        // selector the menu item does. `@Environment(\.openSettings)`
        // is the macOS 14+ replacement; tracked as a future cleanup
        // when the deployment target bumps.
        //
        // Uses `NSApplication.shared` rather than the `NSApp`
        // global. They reference the same singleton — but `NSApp`
        // is `NSApplication!` (implicitly-unwrapped) that reads
        // nil until `NSApplication.shared` is first called, which
        // sets it as a side-effect of constructing the singleton.
        // `swift test` doesn't go through the `@main App` entry
        // point so nobody has touched `.shared` yet; reading
        // `NSApp` there force-unwraps nil and crashes. Going
        // through `.shared` forces lazy creation and works in
        // both production and the test runner.
        // No appState dependency — no weak capture needed.
        NSApplication.shared.sendAction(
            Selector(("showSettingsWindow:")),
            to: nil,
            from: nil
        )
    }

    // ----- Tasks -----

    register(
        SlateCommandID.tasksReview,
        label: "Tasks Review",
        section: .tasks,
        hotkey: "⇧⌘T",
        hint: "Open the vault-wide tasks review."
    ) { [weak appState] in appState?.openTasksReview() }
}
