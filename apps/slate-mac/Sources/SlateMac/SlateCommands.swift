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
    // File management (U2-5, #463). Act on the tree's selected node.
    static let newNote = "slate.file.newNote"
    static let newFolder = "slate.file.newFolder"
    static let renameEntry = "slate.file.rename"
    static let moveTo = "slate.file.moveTo"
    static let deleteEntry = "slate.file.delete"

    // Navigation
    static let jumpToBibliography = "slate.navigation.jumpToBibliography"
    /// Quick switcher — fuzzy filename quick-open (#495). ⌘T. Grouped
    /// under Navigation (it navigates to a file); the palette sorts
    /// Navigation high. The id keeps the `slate.workspace.` prefix
    /// because it's a workspace surface, even though its palette section
    /// is Navigation — id prefix and section don't have to agree.
    static let quickOpen = "slate.workspace.quickOpen"

    // View
    static let toggleSearch = "slate.view.toggleSearch"

    // Canvas (Milestone T, #369). Registered under the FFI
    // CommandSection.canvas (landed cross-language with this issue).
    // Program rule R1: every canvas action is a registry command; these
    // three are the surface switchers the container mirrors.
    static let canvasShowOutline = "slate.canvas.showOutline"
    static let canvasShowTable = "slate.canvas.showTable"
    static let canvasShowVisual = "slate.canvas.showVisual"
    static let canvasWhereAmI = "slate.canvas.whereAmI"
    static let canvasNextCard = "slate.canvas.nextCard"
    static let canvasPreviousCard = "slate.canvas.previousCard"
    static let canvasEnterGroup = "slate.canvas.enterGroup"
    static let canvasExitGroup = "slate.canvas.exitGroup"
    static let canvasFollowForward = "slate.canvas.followConnectionForward"
    static let canvasFollowBack = "slate.canvas.followConnectionBack"
    static let canvasTracePath = "slate.canvas.tracePath"
    static let canvasZoomIn = "slate.canvas.zoomIn"
    static let canvasZoomOut = "slate.canvas.zoomOut"
    static let canvasActualSize = "slate.canvas.actualSize"
    static let canvasFitCanvas = "slate.canvas.fitCanvas"
    static let canvasZoomToSelection = "slate.canvas.zoomToSelection"
    static let canvasToggleFollowSelection = "slate.canvas.toggleFollowSelection"
    static let canvasNewCard = "slate.canvas.newCard"
    static let canvasNewGroup = "slate.canvas.newGroup"
    static let canvasDelete = "slate.canvas.delete"
    static let canvasRenameGroup = "slate.canvas.renameGroup"
    static let canvasMoveIntoGroup = "slate.canvas.moveIntoGroup"
    static let canvasSetColor = "slate.canvas.setColor"
    static let canvasClearColor = "slate.canvas.clearColor"
    static let newCanvas = "slate.file.newCanvas"
    static let canvasPlaceBelow = "slate.canvas.placeBelow"
    static let canvasPlaceRightOf = "slate.canvas.placeRightOf"
    static let canvasPlaceAbove = "slate.canvas.placeAbove"
    static let canvasPlaceLeftOf = "slate.canvas.placeLeftOf"
    static let canvasAlignWith = "slate.canvas.alignWith"

    // Workspace tabs (U1-2, #454). Registered under the View section —
    // CommandSection is an FFI enum; adding a `.workspace` case is a
    // cross-language change deferred to U1-5's registry pass. ⌘1…⌘9
    // ordinal selection is menu-only by design (nine palette rows for one
    // gesture would be noise; Next/Previous cover palette navigation).
    static let newTab = "slate.workspace.newTab"
    static let closeTab = "slate.workspace.closeTab"
    static let nextTab = "slate.workspace.nextTab"
    static let previousTab = "slate.workspace.previousTab"
    static let moveTabLeft = "slate.workspace.moveTabLeft"
    static let moveTabRight = "slate.workspace.moveTabRight"
    static let splitRight = "slate.workspace.splitRight"
    static let splitDown = "slate.workspace.splitDown"
    static let focusPaneLeft = "slate.workspace.focusPaneLeft"
    static let focusPaneRight = "slate.workspace.focusPaneRight"
    static let focusPaneAbove = "slate.workspace.focusPaneAbove"
    static let focusPaneBelow = "slate.workspace.focusPaneBelow"
    static let growPane = "slate.workspace.growPane"
    static let shrinkPane = "slate.workspace.shrinkPane"
    static let closePane = "slate.workspace.closePane"
    static let openInNewTab = "slate.workspace.openInNewTab"
    static let openInSplit = "slate.workspace.openInSplit"

    // Vault
    static let openVault = "slate.vault.open"
    static let closeVault = "slate.vault.close"

    // Editor
    static let save = "slate.editor.save"
    static let citationSummary = "slate.editor.citationSummary"
    static let addProperty = "slate.editor.addProperty"
    static let bulkRenameProperties = "slate.editor.bulkRenameProperties"
    static let toggleViewMode = "slate.editor.toggleViewMode"
    static let togglePropertiesSource = "slate.editor.togglePropertiesSource"

    // Settings
    static let openSettings = "slate.settings.open"

    // Help (U4-3, #472). Grouped under the Settings section — Help and
    // Settings are the app-meta utilities the bottom-left utility bar
    // surfaces, and `CommandSection` (an FFI enum) has no dedicated `.help`
    // case; adding one is a cross-language change out of this PR's scope.
    static let openHelp = "slate.help.open"

    // Tasks
    static let tasksReview = "slate.tasks.review"

    /// All core command ids, in the order they're registered. The
    /// drift test consumes this array to enforce that every id has
    /// a matching `Command` in the registry — future menu additions
    /// without a registration here fail the test loudly.
    static let all: [String] = [
        newFromTemplate,
        newNote,
        newFolder,
        renameEntry,
        moveTo,
        deleteEntry,
        jumpToBibliography,
        quickOpen,
        toggleSearch,
        canvasShowOutline,
        canvasShowTable,
        canvasShowVisual,
        canvasWhereAmI,
        canvasNextCard,
        canvasPreviousCard,
        canvasEnterGroup,
        canvasExitGroup,
        canvasFollowForward,
        canvasFollowBack,
        canvasTracePath,
        canvasZoomIn,
        canvasZoomOut,
        canvasActualSize,
        canvasFitCanvas,
        canvasZoomToSelection,
        canvasToggleFollowSelection,
        canvasNewCard,
        canvasNewGroup,
        canvasDelete,
        canvasRenameGroup,
        canvasMoveIntoGroup,
        canvasSetColor,
        canvasClearColor,
        newCanvas,
        canvasPlaceBelow,
        canvasPlaceRightOf,
        canvasPlaceAbove,
        canvasPlaceLeftOf,
        canvasAlignWith,
        newTab,
        closeTab,
        nextTab,
        previousTab,
        moveTabLeft,
        moveTabRight,
        splitRight,
        splitDown,
        focusPaneLeft,
        focusPaneRight,
        focusPaneAbove,
        focusPaneBelow,
        growPane,
        shrinkPane,
        closePane,
        openInNewTab,
        openInSplit,
        openVault,
        closeVault,
        save,
        citationSummary,
        addProperty,
        bulkRenameProperties,
        toggleViewMode,
        togglePropertiesSource,
        openSettings,
        openHelp,
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

    // ----- Canvas (Milestone T, #369) -----

    register(
        SlateCommandID.canvasShowOutline,
        label: "Canvas: Show Outline",
        section: .canvas,
        hint: "Show the active canvas as a structured outline."
    ) { [weak appState] in appState?.showCanvasSurface(.outline) }

    register(
        SlateCommandID.canvasShowTable,
        label: "Canvas: Show Table",
        section: .canvas,
        hint: "Show the active canvas as a sortable table."
    ) { [weak appState] in appState?.showCanvasSurface(.table) }

    register(
        SlateCommandID.canvasShowVisual,
        label: "Canvas: Show Visual",
        section: .canvas,
        hint: "Show the active canvas as the visual spatial view."
    ) { [weak appState] in appState?.showCanvasSurface(.visual) }

    register(
        SlateCommandID.canvasWhereAmI,
        label: "Canvas: Where Am I?",
        section: .canvas,
        hotkey: "⌃⌘I",
        hint: "Read the selected card's full context: position, group, connections, color, and marks."
    ) { [weak appState] in appState?.canvasWhereAmI() }

    // Navigator movements (#364). Plain arrows work while a canvas
    // surface has focus (rule R2); these palette rows are the
    // always-available equivalents (VO Quick Nav intercepts arrows).
    register(
        SlateCommandID.canvasNextCard,
        label: "Canvas: Next Card",
        section: .canvas,
        hint: "Select the next card in reading order."
    ) { [weak appState] in appState?.canvasSelectAdjacent(offset: 1) }

    register(
        SlateCommandID.canvasPreviousCard,
        label: "Canvas: Previous Card",
        section: .canvas,
        hint: "Select the previous card in reading order."
    ) { [weak appState] in appState?.canvasSelectAdjacent(offset: -1) }

    register(
        SlateCommandID.canvasEnterGroup,
        label: "Canvas: Enter Group",
        section: .canvas,
        hint: "Move into the selected group's first card."
    ) { [weak appState] in appState?.canvasEnterGroup() }

    register(
        SlateCommandID.canvasExitGroup,
        label: "Canvas: Exit Group",
        section: .canvas,
        hint: "Move out to the containing group."
    ) { [weak appState] in appState?.canvasExitGroup() }

    register(
        SlateCommandID.canvasFollowForward,
        label: "Canvas: Follow Connection Forward",
        section: .canvas,
        hint: "Jump along the selected card's first outgoing connection."
    ) { [weak appState] in appState?.canvasFollowConnection(forward: true) }

    register(
        SlateCommandID.canvasFollowBack,
        label: "Canvas: Follow Connection Back",
        section: .canvas,
        hint: "Jump along the selected card's first incoming connection."
    ) { [weak appState] in appState?.canvasFollowConnection(forward: false) }

    register(
        SlateCommandID.canvasTracePath,
        label: "Canvas: Trace Path from Selected Card",
        section: .canvas,
        hint: "Walk the outgoing chain, announcing each hop and the visited count."
    ) { [weak appState] in appState?.canvasTracePath() }

    // Viewport (#520). ⌘=/⌘-/⌘0 bind in the menu scoped to canvas
    // tabs; ⇧1/⇧2 are typing keys on the visual surface only (R2) —
    // these palette rows are the always-available equivalents.
    register(
        SlateCommandID.canvasZoomIn,
        label: "Canvas: Zoom In",
        section: .canvas,
        hotkey: "⌘=",
        hint: "Zoom the visual canvas in. The zoom level is announced."
    ) { [weak appState] in appState?.canvasZoomIn() }

    register(
        SlateCommandID.canvasZoomOut,
        label: "Canvas: Zoom Out",
        section: .canvas,
        hotkey: "⌘-",
        hint: "Zoom the visual canvas out."
    ) { [weak appState] in appState?.canvasZoomOut() }

    register(
        SlateCommandID.canvasActualSize,
        label: "Canvas: Actual Size",
        section: .canvas,
        hotkey: "⌘0",
        hint: "Reset the visual canvas zoom to 100 percent."
    ) { [weak appState] in appState?.canvasActualSize() }

    register(
        SlateCommandID.canvasFitCanvas,
        label: "Canvas: Fit Canvas",
        section: .canvas,
        hint: "Zoom so every card is visible. Shift-1 on the visual surface."
    ) { [weak appState] in appState?.canvasFitCanvas() }

    register(
        SlateCommandID.canvasZoomToSelection,
        label: "Canvas: Zoom to Selection",
        section: .canvas,
        hint: "Zoom to the selected card. Shift-2 on the visual surface."
    ) { [weak appState] in appState?.canvasZoomToSelection() }

    register(
        SlateCommandID.canvasToggleFollowSelection,
        label: "Canvas: Toggle Viewport Follows Selection",
        section: .canvas,
        hint: "When on (the default), the visual view pans to keep the selection visible."
    ) { [weak appState] in appState?.canvasToggleFollowSelection() }

    // Authoring verbs (#368). Prompt-driven ones open container
    // sheets (M6 visible controls); direct ones commit through the
    // one mutation pipeline.
    register(
        SlateCommandID.canvasNewCard,
        label: "Canvas: New Card",
        section: .canvas,
        hotkey: "⌥⌘N",
        hint: "Create a text card next to the selection — placement is automatic and announced."
    ) { [weak appState] in appState?.canvasNewCard() }

    register(
        SlateCommandID.canvasNewGroup,
        label: "Canvas: New Group…",
        section: .canvas,
        hint: "Create a labeled group next to the selection."
    ) { [weak appState] in appState?.canvasPromptNewGroup() }

    register(
        SlateCommandID.canvasDelete,
        label: "Canvas: Delete Selection",
        section: .canvas,
        hint: "Delete the selected card, or ungroup the selected group keeping its cards. Undo with Command-Z."
    ) { [weak appState] in appState?.canvasDeleteSelection() }

    register(
        SlateCommandID.canvasRenameGroup,
        label: "Canvas: Rename Group…",
        section: .canvas,
        hint: "Rename the selected group — group labels are the skeleton of the reading order."
    ) { [weak appState] in appState?.canvasPromptRenameGroup() }

    register(
        SlateCommandID.canvasMoveIntoGroup,
        label: "Canvas: Move into Group…",
        section: .canvas,
        hint: "Move the selected card inside a group by name — no dragging or coordinates."
    ) { [weak appState] in appState?.canvasPromptMoveIntoGroup() }

    register(
        SlateCommandID.canvasSetColor,
        label: "Canvas: Set Color…",
        section: .canvas,
        hint: "Color the selected card by name: red, orange, yellow, green, cyan, or purple."
    ) { [weak appState] in appState?.canvasPromptSetColor() }

    register(
        SlateCommandID.canvasClearColor,
        label: "Canvas: Clear Color",
        section: .canvas,
        hint: "Remove the selected card's color."
    ) { [weak appState] in appState?.canvasSetColor(preset: nil) }

    // Structural placement (#522): spatial arrangement with zero
    // coordinates — the picker names a reference card, the engine
    // computes the slot. Marked sets move as a rigid unit.
    register(
        SlateCommandID.canvasPlaceBelow,
        label: "Canvas: Place Below…",
        section: .canvas,
        hint: "Move the selected card (or the marked set) just below a card you pick."
    ) { [weak appState] in appState?.canvasOpenCardPicker(.placeBelow) }

    register(
        SlateCommandID.canvasPlaceRightOf,
        label: "Canvas: Place Right Of…",
        section: .canvas,
        hint: "Move the selection just right of a card you pick."
    ) { [weak appState] in appState?.canvasOpenCardPicker(.placeRightOf) }

    register(
        SlateCommandID.canvasPlaceAbove,
        label: "Canvas: Place Above…",
        section: .canvas,
        hint: "Move the selection just above a card you pick."
    ) { [weak appState] in appState?.canvasOpenCardPicker(.placeAbove) }

    register(
        SlateCommandID.canvasPlaceLeftOf,
        label: "Canvas: Place Left Of…",
        section: .canvas,
        hint: "Move the selection just left of a card you pick."
    ) { [weak appState] in appState?.canvasOpenCardPicker(.placeLeftOf) }

    register(
        SlateCommandID.canvasAlignWith,
        label: "Canvas: Align With…",
        section: .canvas,
        hint: "Align the selected card's top edge with a card you pick. Overlaps are refused, never silent."
    ) { [weak appState] in appState?.canvasOpenCardPicker(.alignWith) }

    register(
        SlateCommandID.newCanvas,
        label: "New Canvas",
        section: .file,
        hint: "Create an empty canvas file in the vault and open it."
    ) { [weak appState] in appState?.canvasNewCanvasFile() }

    // ----- File management (U2-5, #463) -----

    register(
        SlateCommandID.newNote,
        label: "New Note",
        section: .file,
        hotkey: "⌘N",
        hint: "Create an untitled note in the selected folder, then rename it."
    ) { [weak appState] in appState?.newNoteCommand() }

    register(
        SlateCommandID.newFolder,
        label: "New Folder…",
        section: .file,
        // No global shortcut (spec §U2-5) — context menu + palette only.
        hint: "Create a new folder in the selected folder, then rename it."
    ) { [weak appState] in appState?.newFolderCommand() }

    register(
        SlateCommandID.renameEntry,
        label: "Rename…",
        section: .file,
        hotkey: "⌥⌘R",
        hint: "Rename the selected file or folder in place."
    ) { [weak appState] in appState?.renameSelectedCommand() }

    register(
        SlateCommandID.moveTo,
        label: "Move to…",
        section: .file,
        hotkey: "⇧⌘M",
        hint: "Move the selected file or folder to another folder."
    ) { [weak appState] in appState?.moveSelectedCommand() }

    register(
        SlateCommandID.deleteEntry,
        label: "Move to Trash",
        section: .file,
        // No hotkeyHint: ⌘⌫ is tree-focused-only (spec §U2-5), delivered by the
        // file tree's own key handling — not a menu-bar chord. Registering a
        // hotkeyHint here would make it a drift-check orphan (no menu binding)
        // AND collide with PropertyEditorRow's sheet-scoped ⌘⌫ in
        // `deliberatelyUnregisteredChords`. The palette row invokes on Return.
        hint: "Move the selected file or folder to the Trash."
    ) { [weak appState] in appState?.deleteSelectedCommand() }

    // ----- Navigation -----

    register(
        SlateCommandID.jumpToBibliography,
        label: "Jump to Bibliography",
        section: .navigation,
        hotkey: "⌘J",
        hint: "Filter the Bibliography sidebar to the expanded citation's key."
    ) { [weak appState] in appState?.jumpToBibliographyFromExpandedCitation() }

    register(
        SlateCommandID.quickOpen,
        label: "Quick Open…",
        section: .navigation,
        hotkey: "⌘T",
        hint: "Fuzzy-find a note by name and open it. Return opens it in the current tab."
    ) { [weak appState] in appState?.openQuickSwitcher() }

    // ----- View -----

    register(
        SlateCommandID.toggleSearch,
        label: "Search",
        section: .view,
        hotkey: "⌘F",
        hint: "Toggle the vault-wide search overlay."
    ) { [weak appState] in appState?.toggleSearchOverlay() }

    // ----- Workspace tabs (U1-2, #454) -----

    register(
        SlateCommandID.newTab,
        label: "Duplicate Tab",
        section: .view,
        // ⌘T moved to Quick Open (#495); this keeps its duplicate-tab
        // behavior under the clearer "Duplicate Tab" label with no
        // hotkey. The New-Tab menu item in SlateMacApp likewise dropped
        // its ⌘T binding.
        hint: "Duplicate the current note into a new tab."
    ) { [weak appState] in appState?.newTab() }

    register(
        SlateCommandID.closeTab,
        label: "Close Tab",
        section: .view,
        hotkey: "⌘W",
        hint: "Close the active tab. Prompts if it has unsaved changes."
    ) { [weak appState] in appState?.requestCloseTab() }

    register(
        SlateCommandID.nextTab,
        label: "Show Next Tab",
        section: .view,
        hotkey: "⇧⌘]",
        hint: "Activate the tab to the right, wrapping at the end."
    ) { [weak appState] in appState?.selectNextTab() }

    register(
        SlateCommandID.previousTab,
        label: "Show Previous Tab",
        section: .view,
        hotkey: "⇧⌘[",
        hint: "Activate the tab to the left, wrapping at the start."
    ) { [weak appState] in appState?.selectPreviousTab() }

    register(
        SlateCommandID.moveTabLeft,
        label: "Move Tab Left",
        section: .view,
        hotkey: "⌃⌘←",
        hint: "Reorder the active tab one position left."
    ) { [weak appState] in appState?.moveActiveTabLeft() }

    register(
        SlateCommandID.moveTabRight,
        label: "Move Tab Right",
        section: .view,
        hotkey: "⌃⌘→",
        hint: "Reorder the active tab one position right."
    ) { [weak appState] in appState?.moveActiveTabRight() }

    // ----- Split panes (U1-3, #455) -----

    register(
        SlateCommandID.splitRight,
        label: "Split Right",
        section: .view,
        hotkey: "⌘\\",
        hint: "Split the focused pane side-by-side; the new pane shows the same note."
    ) { [weak appState] in appState?.splitActivePane(axis: .horizontal) }

    register(
        SlateCommandID.splitDown,
        label: "Split Down",
        section: .view,
        hotkey: "⌥⌘\\",
        hint: "Split the focused pane top-and-bottom; the new pane shows the same note."
    ) { [weak appState] in appState?.splitActivePane(axis: .vertical) }

    register(
        SlateCommandID.focusPaneLeft,
        label: "Focus Pane Left",
        section: .view,
        hotkey: "⌥⌘←",
        hint: "Move focus to the pane to the left."
    ) { [weak appState] in appState?.focusPane(.left) }

    register(
        SlateCommandID.focusPaneRight,
        label: "Focus Pane Right",
        section: .view,
        hotkey: "⌥⌘→",
        hint: "Move focus to the pane to the right."
    ) { [weak appState] in appState?.focusPane(.right) }

    register(
        SlateCommandID.focusPaneAbove,
        label: "Focus Pane Above",
        section: .view,
        hotkey: "⌥⌘↑",
        hint: "Move focus to the pane above."
    ) { [weak appState] in appState?.focusPane(.up) }

    register(
        SlateCommandID.focusPaneBelow,
        label: "Focus Pane Below",
        section: .view,
        hotkey: "⌥⌘↓",
        hint: "Move focus to the pane below."
    ) { [weak appState] in appState?.focusPane(.down) }

    register(
        SlateCommandID.growPane,
        label: "Grow Pane",
        section: .view,
        hotkey: "⌥⌘=",
        hint: "Make the focused pane larger."
    ) { [weak appState] in appState?.growFocusedPane() }

    register(
        SlateCommandID.shrinkPane,
        label: "Shrink Pane",
        section: .view,
        hotkey: "⌥⌘-",
        hint: "Make the focused pane smaller."
    ) { [weak appState] in appState?.shrinkFocusedPane() }

    register(
        SlateCommandID.closePane,
        label: "Close Pane",
        section: .view,
        hint: "Close the focused pane's tabs, prompting for unsaved changes."
    ) { [weak appState] in appState?.closeActivePane() }

    register(
        SlateCommandID.openInNewTab,
        label: "Open Selected File in New Tab",
        section: .view,
        hint: "Open the sidebar's selected file in a new tab."
    ) { [weak appState] in
        if let path = appState?.selectedFilePath {
            appState?.openFile(path, target: .newTab)
        }
    }

    register(
        SlateCommandID.openInSplit,
        label: "Open Selected File in Split",
        section: .view,
        hint: "Open the sidebar's selected file in a new split pane."
    ) { [weak appState] in
        if let path = appState?.selectedFilePath {
            appState?.openFile(path, target: .newSplit(.horizontal))
        }
    }

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
        SlateCommandID.toggleViewMode,
        label: "Toggle Reading Mode",
        section: .editor,
        hotkey: "⇧⌘E",
        hint: "Switch the current note between editing and reading mode."
    ) { [weak appState] in appState?.toggleViewMode() }

    register(
        SlateCommandID.togglePropertiesSource,
        label: "Show Properties Source",
        section: .editor,
        hotkey: "⇧⌘D",
        hint: "Switch the properties widget between fields and YAML source."
    ) { [weak appState] in appState?.togglePropertiesSourceCommand() }

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
        // We send the same selector the menu item does.
        // `@Environment(\.openSettings)` (macOS 14+) is the SwiftUI
        // replacement, but it's only reachable from a View's
        // environment — this command action runs in the registry,
        // outside any View — and the selector path also dodges the
        // test-runner `NSApp`-nil crash described below, so it stays.
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

    // ----- Help -----

    register(
        SlateCommandID.openHelp,
        label: "Help",
        section: .settings,
        hint: "Open the project README in your default browser."
    ) { [weak appState] in
        // Same implementation the SidebarUtilityBar "Help" button calls —
        // routes through AppState's injected `externalOpener` (gap G13) so
        // both surfaces open one URL and tests can spy on the hand-off.
        appState?.openHelp()
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
