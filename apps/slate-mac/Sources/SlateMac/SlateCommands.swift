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
    // Sidebar action IDs introduced by FL04-A. Historical `slate.file.*`
    // identifiers below remain stable and are projected into `.sidebar`.
    static let sidebarOpen = "slate.sidebar.open"
    static let sidebarCopyWikilink = "slate.sidebar.copyWikilink"

    // Sidebar organization (FL-06, #658/#659): sort, date grouping, pins.
    static let sidebarPinNote = "slate.sidebar.pinNote"
    static let sidebarUnpinNote = "slate.sidebar.unpinNote"
    static let sidebarUnpinAll = "slate.sidebar.unpinAllInFolder"
    // FL-07 (#660/#661): shortcuts, recents, and navigation polish.
    static let sidebarAddShortcut = "slate.sidebar.addShortcut"
    static let sidebarRemoveShortcut = "slate.sidebar.removeShortcut"
    static let sidebarClearRecents = "slate.sidebar.clearRecents"
    static let sidebarCollapseAll = "slate.sidebar.collapseAll"
    static let sidebarExpandLoaded = "slate.sidebar.expandLoaded"
    static let sidebarHistoryBack = "slate.sidebar.historyBack"
    static let sidebarHistoryForward = "slate.sidebar.historyForward"
    static func sidebarOpenShortcut(_ slot: Int) -> String {
        "slate.sidebar.openShortcut\(slot)"
    }
    static let sidebarOpenShortcutSlots: [String] =
        (1...9).map { sidebarOpenShortcut($0) }
    /// FL-09 (#663): move focus into the top-pinned sidebar filter field.
    static let sidebarFocusFilter = "slate.sidebar.focusFilter"
    /// FL5-3b (#666): batch tag editor invocations on the selection.
    static let sidebarAddTag = "slate.sidebar.addTag"
    static let sidebarRemoveTag = "slate.sidebar.removeTag"
    /// FL7-2 (#669): the public tree/dual-pane layout toggle.
    static let sidebarToggleLayout = "slate.sidebar.toggleLayout"
    /// FL6-1 (#667): folder-note lifecycle on a single selected folder.
    static let createFolderNote = "slate.sidebar.createFolderNote"
    static let openFolderNote = "slate.sidebar.openFolderNote"
    static let deleteFolderNote = "slate.sidebar.deleteFolderNote"
    static let sidebarSortNameAsc = "slate.sidebar.sortNameAsc"
    static let sidebarSortNameDesc = "slate.sidebar.sortNameDesc"
    static let sidebarSortCreatedDesc = "slate.sidebar.sortCreatedDesc"
    static let sidebarSortCreatedAsc = "slate.sidebar.sortCreatedAsc"
    static let sidebarSortModifiedDesc = "slate.sidebar.sortModifiedDesc"
    static let sidebarSortModifiedAsc = "slate.sidebar.sortModifiedAsc"
    static let sidebarToggleDateGrouping = "slate.sidebar.toggleDateGrouping"
    static let sidebarUseVaultDefaultSort = "slate.sidebar.useVaultDefaultSort"

    /// The FL-06 organization command set: enabled/disabled together when
    /// `.slate/sidebar.json` is read-only, and every member routes through
    /// `AppState`'s one organization mutation funnel.
    static let sidebarOrganizationCommands: Set<String> = [
        sidebarPinNote,
        sidebarUnpinNote,
        sidebarUnpinAll,
        sidebarAddShortcut,
        sidebarRemoveShortcut,
        sidebarSortNameAsc,
        sidebarSortNameDesc,
        sidebarSortCreatedDesc,
        sidebarSortCreatedAsc,
        sidebarSortModifiedDesc,
        sidebarSortModifiedAsc,
        sidebarToggleDateGrouping,
        sidebarUseVaultDefaultSort,
    ]

    /// FL-07 navigation family (#660/#661): device/view state only —
    /// never gated by the read-only preferences notice.
    static let sidebarNavigationCommands: Set<String> =
        Set(sidebarOpenShortcutSlots).union([
            sidebarClearRecents,
            sidebarCollapseAll,
            sidebarExpandLoaded,
            sidebarHistoryBack,
            sidebarHistoryForward,
            sidebarFocusFilter,
            sidebarToggleLayout,
        ])

    // File
    static let newFromTemplate = "slate.file.newFromTemplate"
    // File management (U2-5, #463). Act on the tree's selected node.
    static let newNote = "slate.file.newNote"
    static let newFolder = "slate.file.newFolder"
    static let importFilesAndFolders = "slate.file.importFilesAndFolders"
    static let cancelImport = "slate.file.cancelImport"
    static let renameEntry = "slate.file.rename"
    static let moveTo = "slate.file.moveTo"
    static let deleteEntry = "slate.file.delete"
    // Inspection pair (HIG context-menus.md: every context action needs
    // a primary-UI home — these two were context-menu-only).
    static let revealInFinder = "slate.file.revealInFinder"
    static let copyPath = "slate.file.copyPath"
    /// Duplicate the selected FILE (#853) — context menu + File menu +
    /// palette, same three homes the inspection pair got. File-only;
    /// folders are out of #853's scope.
    static let duplicateEntry = "slate.file.duplicate"

    /// Native file-tree mutations serialized by AppState's structural gate.
    /// Palette presentation and registry preflight share this exact catalogue;
    /// inspection commands deliberately stay interactive while a write runs.
    static let structuralMutationCommands: Set<String> = [
        newFromTemplate,
        newNote,
        newFolder,
        importFilesAndFolders,
        renameEntry,
        moveTo,
        deleteEntry,
        duplicateEntry,
        newCanvas,
        canvasConvertToNote,
        basesExportCSV,
        basesExportMarkdown,
    ]

    /// Canvas authoring commands. Read-only outcome-unknown snapshots keep
    /// navigation, zoom, filtering, inspection, marks, and Cancel available;
    /// every command that can open an editor/mode or reach `canvas_apply` uses
    /// this catalogue for one palette availability reason.
    static let canvasMutationCommands: Set<String> = [
        canvasNewCard,
        canvasNewGroup,
        canvasDelete,
        canvasRenameGroup,
        canvasMoveIntoGroup,
        canvasSetColor,
        canvasClearColor,
        canvasPlaceBelow,
        canvasPlaceRightOf,
        canvasPlaceAbove,
        canvasPlaceLeftOf,
        canvasAlignWith,
        canvasMoveMode,
        canvasResizeMode,
        canvasCommitMode,
        canvasResizeDefault,
        canvasResizeFit,
        canvasConnectTo,
        canvasConnectMode,
        canvasDeleteConnection,
        canvasEditConnection,
        canvasEditCard,
        canvasCreateConnectedCard,
        canvasCreateConnectedCardDirectional,
        canvasDuplicate,
        canvasConvertToNote,
        canvasAddNote,
        canvasAddMedia,
        canvasAddLink,
        canvasRemoveFromGroup,
        canvasLocateFile,
        canvasGroupMarked,
        canvasDeleteMarked,
    ]

    /// Note-local authoring commands whose current destination can become an
    /// outcome-unknown Trash path. Inspection, Find, reading-mode toggles,
    /// and source visibility remain available.
    static let noteAuthoringCommands: Set<String> = [
        save,
        addProperty,
    ]

    /// Base interactions that require an attached native document. Retained
    /// snapshots keep inspection and row navigation useful while a definition
    /// reopens, but view/filter/sort commands must expose one shared disabled
    /// reason in the palette before their dispatch backstop is reached.
    static let baseInteractionCommands: Set<String> = [
        basesNextView,
        basesPreviousView,
        basesSortByColumn,
        basesSaveSortToView,
        basesQuickFilter,
    ]

    /// File-backed Edit Filters needs the same attached-handle gate, while a
    /// saved query intentionally opens its handle-independent editor. Keeping
    /// this ID separate lets palette availability follow the active source.
    static let baseDefinitionEditingCommands: Set<String> = [
        basesEditViewFilters,
    ]
    /// Print… (#869). Prints the current note's rendered reading content
    /// via NSPrintOperation (which also gives Save-as-PDF for free). ⌘P
    /// was free — only ⇧⌘P (Command Palette) was claimed. Disabled with
    /// no note open.
    static let printNote = "slate.file.printNote"

    // Navigation
    static let jumpToBibliography = "slate.navigation.jumpToBibliography"
    /// Quick switcher — fuzzy filename quick-open (#495). ⌘O (#863
    /// moved it from ⌘T: Obsidian's default quick-switcher chord is
    /// ⌘O, and ⌘T returned to the tab family). Grouped under
    /// Navigation (it navigates to a file); the palette sorts
    /// Navigation high. The id keeps the `slate.workspace.` prefix
    /// because it's a workspace surface, even though its palette section
    /// is Navigation — id prefix and section don't have to agree.
    static let quickOpen = "slate.workspace.quickOpen"

    // View
    static let toggleSearch = "slate.view.toggleSearch"
    /// Sync diagnostics refresh (M-3, #534). Panel-scoped command —
    /// registry + View menu + palette per the m_spec §M-3 rule (the
    /// registry invariant is menu↔palette unification); O-5's
    /// `slate.history.showPanel` uses the same home by explicit
    /// cross-reference.
    static let refreshSyncDiagnostics = "slate.diagnostics.refreshSync"

    /// History panel reveal (O-5, #543). Same View-menu home as
    /// `refreshSyncDiagnostics` above (cross-referenced in both
    /// specs so the two PRs converge on one menu). Row actions
    /// (Compare/Restore) are NOT commands — they need row context.
    static let showHistoryPanel = "slate.history.showPanel"

    // Graph (Milestone P). Registered under the FFI CommandSection.graph
    // (landed cross-language with P1-3 #556). P1 registers ZERO new
    // chords — palette + View-menu are the paths (T rule R1).

    /// Connections leaf reveal (P1-1 #554): reveals + focuses the
    /// Connections leaf for the active note.
    static let showConnectionsPanel = "slate.graph.showConnections"

    /// Connections depth ±1 (P1-3 #556): the keyboard/palette path to the
    /// leaf's depth stepper.
    static let connectionsDeeper = "slate.graph.connectionsDeeper"
    static let connectionsShallower = "slate.graph.connectionsShallower"

    /// Graph-table presets (P1-3 #556) — parameterizations of the Graph
    /// tab, not new surfaces. Palette keywords: "orphans", "broken
    /// links", "hubs".
    static let graphOrphans = "slate.graph.orphans"
    static let graphUnresolved = "slate.graph.unresolved"
    static let graphMostLinked = "slate.graph.mostLinked"

    /// Right-pane hide/reveal (#882). ⌥⌘I (inspector/utility-pane idiom —
    /// the right pane hosts the panel rail; distinct from ⌃⌘I Canvas Where
    /// Am I, and collision-free against every existing chord). The
    /// menu item's Hide/Show title reflects state; the palette keeps the
    /// static "Toggle Right Pane" noun (the toggleViewMode precedent —
    /// the menu↔palette invariant is CHORD parity, not label parity).
    static let toggleRightPane = "slate.view.toggleRightPane"

    /// Open/activate the global Graph tab (P1-2 #555; moved into
    /// `CommandSection.graph` in P1-3 alongside the presets below).
    static let openGraphTab = "slate.graph.openTab"

    /// Diagram-mode viewport (P2-3 #559). ⌘=/⌘−/⌘0 are palette mirrors of
    /// the focus-routed menu chords (R2 — the chord IS what it does on the
    /// diagram); Fit Graph owns the new ⌥⌘0 chord (R3).
    static let graphZoomIn = "slate.graph.zoomIn"
    static let graphZoomOut = "slate.graph.zoomOut"
    static let graphActualSize = "slate.graph.actualSize"
    static let graphFitGraph = "slate.graph.fitGraph"
    /// Diagram "Where am I?" (⌃⌘I) — a palette mirror of the focus-routed
    /// chord (R2), same pattern as the graph zoom commands. The chord is
    /// owned by the one routed "Where Am I?" menu item.
    static let graphWhereAmI = "slate.graph.whereAmI"

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
    static let canvasMoveMode = "slate.canvas.moveMode"
    static let canvasResizeMode = "slate.canvas.resizeMode"
    static let canvasCommitMode = "slate.canvas.commitMode"
    static let canvasCancelMode = "slate.canvas.cancelMode"
    static let canvasResizeDefault = "slate.canvas.resizeDefaultSize"
    static let canvasResizeFit = "slate.canvas.resizeFitContent"
    static let canvasConnectTo = "slate.canvas.connectTo"
    static let canvasConnectMode = "slate.canvas.connectMode"
    static let canvasDeleteConnection = "slate.canvas.deleteConnection"
    static let canvasEditConnection = "slate.canvas.editConnection"
    static let canvasEditCard = "slate.canvas.editCard"
    static let canvasCreateConnectedCard = "slate.canvas.createConnectedCard"
    static let canvasCreateConnectedCardDirectional = "slate.canvas.createConnectedCardDirectional"
    static let canvasDuplicate = "slate.canvas.duplicate"
    static let canvasConvertToNote = "slate.canvas.convertToNote"
    static let canvasFilterCards = "slate.canvas.filterCards"
    static let canvasClearFilter = "slate.canvas.clearFilter"
    static let canvasAddNote = "slate.canvas.addNote"
    static let canvasAddMedia = "slate.canvas.addMedia"
    static let canvasAddLink = "slate.canvas.addLink"
    static let canvasRemoveFromGroup = "slate.canvas.removeFromGroup"
    static let canvasLocateFile = "slate.canvas.locateFile"
    static let canvasToggleMark = "slate.canvas.toggleMark"
    static let canvasShowMarks = "slate.canvas.showMarks"
    static let canvasClearMarks = "slate.canvas.clearMarks"
    static let canvasGroupMarked = "slate.canvas.groupMarked"
    static let canvasDeleteMarked = "slate.canvas.deleteMarked"

    // Bases (Milestone N, #702). Registered under CommandSection.bases
    // with no global chords; the view owns local table/header keys.
    static let basesOpenViewSwitcher = "slate.bases.openViewSwitcher"
    static let basesNextView = "slate.bases.nextView"
    static let basesPreviousView = "slate.bases.previousView"
    static let basesSortByColumn = "slate.bases.sortByColumn"
    static let basesSaveSortToView = "slate.bases.saveSortToView"
    static let basesViewAsTable = "slate.bases.viewAsTable"
    static let basesViewAsList = "slate.bases.viewAsList"
    static let basesQuickFilter = "slate.bases.quickFilter"
    static let basesWhereAmI = "slate.bases.whereAmI"
    static let basesOpenRow = "slate.bases.openRow"
    static let basesCopyLink = "slate.bases.copyLink"
    static let basesShowBacklinks = "slate.bases.showBacklinks"
    static let basesEditProperty = "slate.bases.editProperty"
    static let basesExportCSV = "slate.bases.exportCsv"
    static let basesExportMarkdown = "slate.bases.exportMarkdown"
    static let basesCopyMarkdown = "slate.bases.copyMarkdown"
    static let basesResultsPopover = "slate.bases.resultsPopover"
    static let basesRefresh = "slate.bases.refresh"
    static let basesNewQuery = "slate.bases.newQuery"
    static let basesEditViewFilters = "slate.bases.editViewFilters"
    static let basesBuilderAddCondition = "slate.bases.builder.addCondition"
    static let basesBuilderAddGroup = "slate.bases.builder.addGroup"
    static let basesBuilderEditCondition = "slate.bases.builder.editCondition"
    static let basesBuilderRemoveCondition = "slate.bases.builder.removeCondition"
    private static let basesRunSavedQueryPrefix = "slate.bases.savedQuery.run."

    static func basesRunSavedQuery(id: String) -> String {
        "\(basesRunSavedQueryPrefix)\(id)"
    }

    static func isBasesRunSavedQuery(_ id: String) -> Bool {
        id.hasPrefix(basesRunSavedQueryPrefix)
    }

    // Workspace tabs (U1-2, #454). Registered under the View section —
    // CommandSection is an FFI enum; adding a `.workspace` case is a
    // cross-language change deferred to U1-5's registry pass. ⌘1…⌘9
    // ordinal selection is menu-only by design (nine palette rows for one
    // gesture would be noise; Next/Previous cover palette navigation).
    static let newTab = "slate.workspace.newTab"
    static let closeTab = "slate.workspace.closeTab"
    /// Reopen Closed Tab (#863). ⇧⌘T — the macOS/Obsidian convention.
    /// Pops the per-vault-session closed-tab stack through the
    /// standard open funnel (dedup + pane placement honored).
    static let reopenClosedTab = "slate.workspace.reopenClosedTab"
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
    /// Find-in-note (#874). ⌘F — Edit ▸ Find ▸ Find…, scoped to the open
    /// editor (the NSTextView find bar). Reallocated from vault search,
    /// which moved to ⇧⌘F (`toggleSearch`) per the Cory-confirmed
    /// 2026-07-12 decision. Routes through `requestFindInFocusedSurface`,
    /// so a focused canvas/base filter still wins ⌘F.
    static let findInNote = "slate.editor.findInNote"
    static let citationSummary = "slate.editor.citationSummary"
    static let addProperty = "slate.editor.addProperty"
    static let bulkRenameProperties = "slate.editor.bulkRenameProperties"
    static let toggleViewMode = "slate.editor.toggleViewMode"
    static let togglePropertiesSource = "slate.editor.togglePropertiesSource"
    // Editor zoom (#848) — palette twins of the focus-routed menu items
    // (red-team F3 on the chords PR: the canvas zoom rows are silent
    // no-ops off-canvas, leaving palette-first users without an editor
    // zoom path). No hotkeyHints: the unified menu items own ⌘=/⌘−/⌘0.
    static let editorZoomIn = "slate.editor.zoomIn"
    static let editorZoomOut = "slate.editor.zoomOut"
    static let editorActualSize = "slate.editor.actualSize"
    /// Opt-in live spell checking (#855). No chord — menu (Edit ▸
    /// Check Spelling While Typing, live checkmark) + palette only.
    static let toggleSpellCheck = "slate.editor.toggleSpellCheck"

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
        sidebarOpen,
        newNote,
        newFolder,
        newFromTemplate,
        importFilesAndFolders,
        cancelImport,
        renameEntry,
        moveTo,
        duplicateEntry,
        revealInFinder,
        copyPath,
        sidebarCopyWikilink,
        sidebarPinNote,
        sidebarUnpinNote,
        sidebarUnpinAll,
        sidebarSortNameAsc,
        sidebarSortNameDesc,
        sidebarSortCreatedDesc,
        sidebarSortCreatedAsc,
        sidebarSortModifiedDesc,
        sidebarSortModifiedAsc,
        sidebarToggleDateGrouping,
        sidebarUseVaultDefaultSort,
        sidebarAddShortcut,
        sidebarRemoveShortcut,
        sidebarClearRecents,
        sidebarCollapseAll,
        sidebarExpandLoaded,
        sidebarHistoryBack,
        sidebarHistoryForward,
        sidebarOpenShortcut(1),
        sidebarOpenShortcut(2),
        sidebarOpenShortcut(3),
        sidebarOpenShortcut(4),
        sidebarOpenShortcut(5),
        sidebarOpenShortcut(6),
        sidebarOpenShortcut(7),
        sidebarOpenShortcut(8),
        sidebarOpenShortcut(9),
        sidebarFocusFilter,
        sidebarToggleLayout,
        sidebarAddTag,
        sidebarRemoveTag,
        createFolderNote,
        openFolderNote,
        deleteFolderNote,
        deleteEntry,
        printNote,
        jumpToBibliography,
        quickOpen,
        toggleSearch,
        refreshSyncDiagnostics,
        showHistoryPanel,
        showConnectionsPanel,
        connectionsDeeper,
        connectionsShallower,
        graphOrphans,
        graphUnresolved,
        graphMostLinked,
        toggleRightPane,
        openGraphTab,
        graphZoomIn,
        graphZoomOut,
        graphActualSize,
        graphFitGraph,
        graphWhereAmI,
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
        canvasMoveMode,
        canvasResizeMode,
        canvasCommitMode,
        canvasCancelMode,
        canvasResizeDefault,
        canvasResizeFit,
        canvasConnectTo,
        canvasConnectMode,
        canvasDeleteConnection,
        canvasEditConnection,
        canvasEditCard,
        canvasCreateConnectedCard,
        canvasCreateConnectedCardDirectional,
        canvasDuplicate,
        canvasConvertToNote,
        canvasFilterCards,
        canvasClearFilter,
        canvasAddNote,
        canvasAddMedia,
        canvasAddLink,
        canvasRemoveFromGroup,
        canvasLocateFile,
        canvasToggleMark,
        canvasShowMarks,
        canvasClearMarks,
        canvasGroupMarked,
        canvasDeleteMarked,
        basesOpenViewSwitcher,
        basesNextView,
        basesPreviousView,
        basesSortByColumn,
        basesSaveSortToView,
        basesViewAsTable,
        basesViewAsList,
        basesQuickFilter,
        basesWhereAmI,
        basesOpenRow,
        basesCopyLink,
        basesShowBacklinks,
        basesEditProperty,
        basesExportCSV,
        basesExportMarkdown,
        basesCopyMarkdown,
        basesResultsPopover,
        basesRefresh,
        basesNewQuery,
        basesEditViewFilters,
        basesBuilderAddCondition,
        basesBuilderAddGroup,
        basesBuilderEditCondition,
        basesBuilderRemoveCondition,
        newTab,
        closeTab,
        reopenClosedTab,
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
        findInNote,
        citationSummary,
        addProperty,
        bulkRenameProperties,
        editorZoomIn,
        editorZoomOut,
        editorActualSize,
        toggleSpellCheck,
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
    private let action: @MainActor () throws -> Void

    init(_ action: @escaping @MainActor () throws -> Void) {
        self.action = action
    }

    func invoke() throws {
        if Thread.isMainThread {
            try MainActor.assumeIsolated { try action() }
        } else {
            // Block until the main queue runs the action — matches
            // the synchronous shape of `invoke_by_id` on the Rust
            // side. We never invoke from a background thread in
            // current usage (palette button is main-thread), so
            // this branch is a defensive guard for future callers —
            // the CLI / HTTP API extensibility tiers (V1.x), per
            // `docs/plans/05_locked_architecture_decisions.md` §10
            // (Extensibility model).
            try DispatchQueue.main.sync {
                try MainActor.assumeIsolated { try self.action() }
            }
        }
    }
}

/// Register the exact shared Sidebar catalog once. The injected stable-ID
/// executor keeps this owner independently testable while production routes
/// every entry through AppState's live projection and frozen dispatcher.
@MainActor
func registerSidebarCommands(
    into registry: CommandRegistry,
    invokeID: @escaping @MainActor (String) throws -> Void
) {
    for definition in SidebarActionCatalog.actions {
        let hotkey: String?
        switch definition.id {
        case SlateCommandID.newNote: hotkey = "⌘N"
        case SlateCommandID.newFromTemplate: hotkey = "⇧⌘N"
        case SlateCommandID.renameEntry: hotkey = "⌥⌘R"
        case SlateCommandID.moveTo: hotkey = "⇧⌘M"
        case SlateCommandID.sidebarHistoryBack: hotkey = "⌃⌘["
        case SlateCommandID.sidebarHistoryForward: hotkey = "⌃⌘]"
        case SlateCommandID.sidebarFocusFilter: hotkey = "⌥⌘F"
        // ⌃1–⌃9 are focus-scoped view chords (FL3-3.2): with the sidebar
        // unfocused they are intentionally inert, so the registry must not
        // advertise them as menu-reachable hints (#422 dead-zone gate).
        default: hotkey = nil
        }
        let replaced = registry.register(
            command: Command(
                id: definition.id,
                label: definition.label,
                accessibilityHint: definition.accessibilityHint,
                hotkeyHint: hotkey,
                section: .sidebar),
            action: MenuCommandAction {
                try invokeID(definition.id)
            })
        assert(!replaced, "duplicate Sidebar command id: \(definition.id)")
    }
}

/// One production contract for every global Cancel Import surface.
///
/// The File menu consumes this contract's typed shortcut extension; the
/// command registry consumes its stable palette metadata; both ask for a fresh
/// projection and route through the same fallible action. Keeping the
/// projection live avoids a stale enabled item during importing -> cancelling.
@MainActor
enum CancelImportCommandContract {
    struct Projection: Equatable {
        let disabledReason: String?
        let hint: String

        var isEnabled: Bool { disabledReason == nil }
    }

    static let id = SlateCommandID.cancelImport
    static let label = "Cancel Import"
    static let section: CommandSection = .sidebar
    static let hotkeyHint = "⌘."
    static let availableHint =
        SidebarImportProgressStrip.cancelAccessibilityHint

    static func projection(for appState: AppState) -> Projection {
        let disabledReason = appState.importCancellationDisabledReason
        return Projection(
            disabledReason: disabledReason,
            hint: disabledReason ?? availableHint)
    }

    static func perform(on appState: AppState) throws {
        if let reason = projection(for: appState).disabledReason {
            throw CommandError.ActionFailed(message: reason)
        }
        guard appState.requestImportBatchCancellation() else {
            throw CommandError.ActionFailed(
                message: projection(for: appState).disabledReason
                    ?? SidebarImportProgressStrip.noImportInProgressHint)
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
        action: @escaping @MainActor () throws -> Void
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

    /// Structural registry actions remain invokable for keyboard, palette, and
    /// future external callers, so their admission must be synchronous and
    /// fallible. Busy work throws the same exact reason the UI displays before
    /// any command wrapper can stage a sheet, field, alert, runner, or write.
    func registerStructural(
        _ id: String,
        label: String,
        section: CommandSection,
        hotkey: String? = nil,
        hint: String? = nil,
        action: @escaping @MainActor (AppState) -> Void
    ) {
        register(
            id,
            label: label,
            section: section,
            hotkey: hotkey,
            hint: hint
        ) { [weak appState] in
            guard let appState else { return }
            if let reason = appState.structuralMutationDisabledReason {
                throw CommandError.ActionFailed(message: reason)
            }
            action(appState)
        }
    }

    registerSidebarCommands(into: registry) { [weak appState] id in
        guard let appState else { return }
        _ = try appState.dispatchSidebarAction(id: id)
    }

    // FL05 lifecycle control is global rather than selection-scoped: it stays
    // visible in the stable palette inventory and must remain invokable while
    // the structural gate is occupied by the import it cancels.
    register(
        CancelImportCommandContract.id,
        label: CancelImportCommandContract.label,
        section: CancelImportCommandContract.section,
        hotkey: CancelImportCommandContract.hotkeyHint,
        hint: CancelImportCommandContract.availableHint
    ) { [weak appState] in
        guard let appState else { return }
        try CancelImportCommandContract.perform(on: appState)
    }

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

    // Viewport (#520). ⌘=/⌘-/⌘0 bind in the menu as the unified,
    // focus-routed Zoom In / Zoom Out / Actual Size items (#848):
    // the chords drive the canvas viewport when a canvas tab is
    // active and editor text zoom otherwise, Undo/Redo-style. These
    // canvas-scoped palette rows keep the chords as hotkeyHints —
    // they ARE what the chord does on a canvas — and remain the
    // always-available equivalents (R1); ⇧1/⇧2 are typing keys on
    // the visual surface only (R2).
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

    // Spatial modes (#521, t0 §2). Palette rows are the M6 visible-
    // control path: enter, commit, and cancel are all reachable
    // without the keyboard-only chords.
    register(
        SlateCommandID.canvasMoveMode,
        label: "Canvas: Move Mode",
        section: .canvas,
        hotkey: "⌃⌘G",
        hint: "Grab the selection. Arrows nudge on the grid, Shift for big steps, Return places, Escape cancels."
    ) { [weak appState] in appState?.canvasEnterMoveMode() }

    register(
        SlateCommandID.canvasResizeMode,
        label: "Canvas: Resize Mode",
        section: .canvas,
        hotkey: "⌃⌘R",
        hint: "Resize the selected card. Left and Right change width, Up and Down change height."
    ) { [weak appState] in appState?.canvasCommitOrEnterResize() }

    register(
        SlateCommandID.canvasCommitMode,
        label: "Canvas: Commit Mode",
        section: .canvas,
        hint: "Apply the active move or resize (same as Return)."
    ) { [weak appState] in
        guard let appState, let doc = appState.activeCanvasDocument else { return }
        _ = appState.canvasModeController(for: doc).commit()
    }

    register(
        SlateCommandID.canvasCancelMode,
        label: "Canvas: Cancel Mode",
        section: .canvas,
        hint: "Cancel the active move or resize and restore the prior position (same as Escape)."
    ) { [weak appState] in
        guard let appState, let doc = appState.activeCanvasDocument else { return }
        _ = appState.canvasModeController(for: doc).cancel()
    }

    register(
        SlateCommandID.canvasResizeDefault,
        label: "Canvas: Resize to Default Size",
        section: .canvas,
        hint: "In resize mode, set the card to the default 260 by 140."
    ) { [weak appState] in appState?.canvasResizeDefaultSize() }

    register(
        SlateCommandID.canvasResizeFit,
        label: "Canvas: Resize to Fit Content",
        section: .canvas,
        hint: "In resize mode, size the card to its text."
    ) { [weak appState] in appState?.canvasResizeFitContent() }

    // Connect flow (#523): picker primary, mode secondary; edits and
    // deletes reachable from the palette (R1) and the outline's
    // connection rows.
    register(
        SlateCommandID.canvasConnectTo,
        label: "Canvas: Connect To…",
        section: .canvas,
        hotkey: "⌃⌘C",
        hint: "Connect the selected card to a card you pick, with an optional label. Sides are automatic."
    ) { [weak appState] in appState?.canvasOpenConnectPicker() }

    register(
        SlateCommandID.canvasConnectMode,
        label: "Canvas: Connect Mode",
        section: .canvas,
        hint: "Remember the selected card, navigate to a target with the usual movements, press Return to connect."
    ) { [weak appState] in appState?.canvasEnterConnectMode() }

    register(
        SlateCommandID.canvasDeleteConnection,
        label: "Canvas: Delete Connection…",
        section: .canvas,
        hint: "Delete one of the selected card's connections. Undo with Command-Z."
    ) { [weak appState] in appState?.canvasPromptDeleteConnection() }

    register(
        SlateCommandID.canvasEditConnection,
        label: "Canvas: Edit Connection…",
        section: .canvas,
        hint: "Change one of the selected card's connection labels or direction."
    ) { [weak appState] in appState?.canvasPromptEditConnection() }

    // #525 parity extras: the mind-mapping loop, keyboard-first.
    register(
        SlateCommandID.canvasCreateConnectedCard,
        label: "Canvas: Create Connected Card",
        section: .canvas,
        hotkey: "⌃⌥⌘N",
        hint: "New text card below the selection, already connected, ready to type."
    ) { [weak appState] in appState?.canvasCreateConnectedCard() }

    register(
        SlateCommandID.canvasCreateConnectedCardDirectional,
        label: "Canvas: Create Connected Card (Choose Direction)…",
        section: .canvas,
        hint: "Pick the side first, then the connected card is created there."
    ) { [weak appState] in appState?.canvasPromptConnectedDirection() }

    register(
        SlateCommandID.canvasDuplicate,
        label: "Canvas: Duplicate",
        section: .canvas,
        hint: "Duplicate the selected card or the marked set — one action, one undo."
    ) { [weak appState] in appState?.canvasDuplicate() }

    registerStructural(
        SlateCommandID.canvasConvertToNote,
        label: "Canvas: Convert Card to Note…",
        section: .canvas,
        hint: "Create a vault note from the text card; the card then points at it."
    ) { appState in appState.canvasPromptConvertToNote() }

    // #373: in-canvas filter (a view, never a mutation).
    register(
        SlateCommandID.canvasFilterCards,
        label: "Canvas: Filter Cards…",
        section: .canvas,
        hint: "Focus the filter field (⌘F on a canvas): narrows by title, type, group, or target."
    ) { [weak appState] in appState?.canvasFocusFilter() }

    register(
        SlateCommandID.canvasClearFilter,
        label: "Canvas: Clear Filter",
        section: .canvas,
        hint: "Show every card again (also the Escape rung while filtering)."
    ) { [weak appState] in appState?.canvasClearFilter() }

    // #368 part 2: editor + creation for every card kind + repoint.
    register(
        SlateCommandID.canvasEditCard,
        label: "Canvas: Edit Card Text…",
        section: .canvas,
        hint: "Open the selected text card in the editor. Escape saves and returns."
    ) { [weak appState] in appState?.canvasEditCard() }

    register(
        SlateCommandID.canvasAddNote,
        label: "Canvas: Add Note to Canvas…",
        section: .canvas,
        hint: "Pick a vault note; a file card is placed next to your selection."
    ) { [weak appState] in appState?.canvasOpenAddNote() }

    register(
        SlateCommandID.canvasAddMedia,
        label: "Canvas: Add Media…",
        section: .canvas,
        hint: "Pick a vault media file; a file card is placed next to your selection."
    ) { [weak appState] in appState?.canvasOpenAddMedia() }

    register(
        SlateCommandID.canvasAddLink,
        label: "Canvas: Add Link Card…",
        section: .canvas,
        hint: "Paste or type a URL; a link card is placed next to your selection."
    ) { [weak appState] in appState?.canvasOpenAddLink() }

    register(
        SlateCommandID.canvasRemoveFromGroup,
        label: "Canvas: Remove from Group",
        section: .canvas,
        hint: "Move the selected card out of its enclosing group, placed by the engine."
    ) { [weak appState] in appState?.canvasRemoveFromGroup() }

    register(
        SlateCommandID.canvasLocateFile,
        label: "Canvas: Locate File…",
        section: .canvas,
        hint: "Repoint the selected file card at a different vault file."
    ) { [weak appState] in appState?.canvasOpenLocate() }

    // Mark-then-act (#524, decision 4: no shift-range selection).
    register(
        SlateCommandID.canvasToggleMark,
        label: "Canvas: Toggle Mark",
        section: .canvas,
        hotkey: "⌃⌘M",
        hint: "Mark or unmark the selected card. Bulk actions apply to every marked card at once."
    ) { [weak appState] in appState?.canvasToggleMark() }

    register(
        SlateCommandID.canvasShowMarks,
        label: "Canvas: Show Marked Cards",
        section: .canvas,
        hint: "Open the marks list: jump to or unmark any marked card."
    ) { [weak appState] in appState?.canvasShowMarksList() }

    register(
        SlateCommandID.canvasClearMarks,
        label: "Canvas: Clear All Marks",
        section: .canvas,
        hint: "Unmark every marked card."
    ) { [weak appState] in appState?.canvasClearMarks() }

    register(
        SlateCommandID.canvasGroupMarked,
        label: "Canvas: Group Marked Cards…",
        section: .canvas,
        hint: "Wrap the marked cards in a new labeled group — one action, one undo."
    ) { [weak appState] in appState?.canvasPromptGroupMarked() }

    register(
        SlateCommandID.canvasDeleteMarked,
        label: "Canvas: Delete Marked Cards",
        section: .canvas,
        hint: "Delete every marked card and its connections — one summary, one undo."
    ) { [weak appState] in appState?.canvasDeleteMarked() }

    registerStructural(
        SlateCommandID.newCanvas,
        label: "New Canvas",
        section: .file,
        hint: "Create an empty canvas file in the vault and open it."
    ) { appState in appState.canvasNewCanvasFile() }

    // ----- Bases (Milestone N, #702) -----

    register(
        SlateCommandID.basesOpenViewSwitcher,
        label: "Bases: Open View Switcher",
        section: .bases,
        hint: "List the views in the active base."
    ) { [weak appState] in appState?.basesOpenViewSwitcher() }

    register(
        SlateCommandID.basesNextView,
        label: "Bases: Next View",
        section: .bases,
        hint: "Switch to the next view in the active base."
    ) { [weak appState] in appState?.basesSelectNextView() }

    register(
        SlateCommandID.basesPreviousView,
        label: "Bases: Previous View",
        section: .bases,
        hint: "Switch to the previous view in the active base."
    ) { [weak appState] in appState?.basesSelectPreviousView() }

    register(
        SlateCommandID.basesSortByColumn,
        label: "Bases: Sort by Column",
        section: .bases,
        hint: "Sort the active base table from the focused column."
    ) { [weak appState] in appState?.basesSortByColumn() }

    register(
        SlateCommandID.basesSaveSortToView,
        label: "Bases: Save Sort to View",
        section: .bases,
        hint: "Persist the current base table sort to the active view."
    ) { [weak appState] in appState?.basesSaveSortToView() }

    register(
        SlateCommandID.basesViewAsTable,
        label: "Bases: View as Table",
        section: .bases,
        hint: "Temporarily render the active base with table cell navigation."
    ) { [weak appState] in appState?.basesViewAsTable() }

    register(
        SlateCommandID.basesViewAsList,
        label: "Bases: View as List",
        section: .bases,
        hint: "Temporarily render the active base with row navigation."
    ) { [weak appState] in appState?.basesViewAsList() }

    register(
        SlateCommandID.basesQuickFilter,
        label: "Bases: Quick Filter",
        section: .bases,
        hint: "Focus the active base's temporary quick filter field."
    ) { [weak appState] in appState?.basesFocusQuickFilter() }

    register(
        SlateCommandID.basesWhereAmI,
        label: "Bases: Where Am I?",
        section: .bases,
        hint: "Read the active base, view, and temporary quick filter."
    ) { [weak appState] in _ = appState?.basesWhereAmI() }

    register(
        SlateCommandID.basesOpenRow,
        label: "Bases: Open Row",
        section: .bases,
        hint: "Open the selected base result row."
    ) { [weak appState] in appState?.basesOpenSelectedRow() }

    register(
        SlateCommandID.basesCopyLink,
        label: "Bases: Copy Link",
        section: .bases,
        hint: "Copy a wikilink to the selected base result row."
    ) { [weak appState] in appState?.basesCopySelectedLink() }

    register(
        SlateCommandID.basesShowBacklinks,
        label: "Bases: Show Backlinks",
        section: .bases,
        hint: "Show backlinks for the selected base result row."
    ) { [weak appState] in appState?.basesShowSelectedBacklinks() }

    register(
        SlateCommandID.basesEditProperty,
        label: "Bases: Edit Property",
        section: .bases,
        hint: "Edit the selected editable base property cell."
    ) { [weak appState] in appState?.basesEditSelectedProperty() }

    registerStructural(
        SlateCommandID.basesExportCSV,
        label: "Bases: Export View as CSV",
        section: .bases,
        hint: "Export the active base view as CSV."
    ) { $0.basesExportCSV() }

    registerStructural(
        SlateCommandID.basesExportMarkdown,
        label: "Bases: Export View as Markdown Table",
        section: .bases,
        hint: "Export the active base view as a Markdown table."
    ) { $0.basesExportMarkdown() }

    register(
        SlateCommandID.basesCopyMarkdown,
        label: "Bases: Copy View as Markdown",
        section: .bases,
        hint: "Copy the active base view as a Markdown table."
    ) { [weak appState] in _ = appState?.basesCopyViewAsMarkdown() }

    register(
        SlateCommandID.basesResultsPopover,
        label: "Bases: Results",
        section: .bases,
        hint: "Read the result count and summary for the active base."
    ) { [weak appState] in appState?.basesResultsPopover() }

    register(
        SlateCommandID.basesRefresh,
        label: "Bases: Refresh",
        section: .bases,
        hint: "Reload the active base and re-run its current view."
    ) { [weak appState] in appState?.basesRefresh() }

    register(
        SlateCommandID.basesNewQuery,
        label: "Bases: New Query",
        section: .bases,
        hint: "Open the structured Bases query builder."
    ) { [weak appState] in appState?.basesNewQuery() }

    register(
        SlateCommandID.basesEditViewFilters,
        label: "Bases: Edit View Filters",
        section: .bases,
        hint: "Open the active base view in the structured query builder."
    ) { [weak appState] in appState?.basesEditViewFilters() }

    register(
        SlateCommandID.basesBuilderAddCondition,
        label: "Bases: Add Condition",
        section: .bases,
        hint: "Add a condition row to the open query builder."
    ) { [weak appState] in appState?.basesBuilderAddCondition() }

    register(
        SlateCommandID.basesBuilderAddGroup,
        label: "Bases: Add Group",
        section: .bases,
        hint: "Add a one-level condition group to the open query builder."
    ) { [weak appState] in appState?.basesBuilderAddGroup() }

    register(
        SlateCommandID.basesBuilderEditCondition,
        label: "Bases: Edit Condition",
        section: .bases,
        hint: "Edit the selected condition row in the open query builder."
    ) { [weak appState] in appState?.basesBuilderEditCondition() }

    register(
        SlateCommandID.basesBuilderRemoveCondition,
        label: "Bases: Remove Condition",
        section: .bases,
        hint: "Remove the selected condition row from the open query builder."
    ) { [weak appState] in appState?.basesBuilderRemoveCondition() }

    register(
        SlateCommandID.printNote,
        // Matches the File ▸ Print… menu item verbatim (menu↔palette
        // label parity here — the label is state-free, unlike the
        // show/hide toggles). ⌘P as the palette hotkeyHint mirrors the
        // menu chord (the menu↔palette CHORD-parity invariant).
        label: "Print…",
        section: .file,
        hotkey: "⌘P",
        hint: "Print the current note's rendered reading content. Also offers Save as PDF."
    ) { [weak appState] in appState?.printCurrentNote() }

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
        // ⌘O (#863; was ⌘T) — menu↔palette chord parity with the
        // File ▸ Quick Open… item.
        hotkey: "⌘O",
        hint: "Fuzzy-find a note by name and open it. Return opens it in the current tab."
    ) { [weak appState] in appState?.openQuickSwitcher() }

    // ----- View -----

    register(
        SlateCommandID.toggleSearch,
        label: "Search Vault",
        section: .view,
        // #874: moved from ⌘F to ⇧⌘F. Vault-wide search is the shifted
        // "search all files" chord (Obsidian / VS Code); bare ⌘F is now
        // find-in-note (`findInNote`). The menu item ("Search Vault…")
        // carries the same chord — CHORD parity is the menu↔palette
        // invariant.
        hotkey: "⇧⌘F",
        hint: "Toggle the vault-wide search overlay."
    ) { [weak appState] in appState?.toggleSearchOverlay() }

    // ----- Sync diagnostics (M-3, #534) -----

    register(
        SlateCommandID.refreshSyncDiagnostics,
        // Title-style capitalization (menus.md) — was sentence-case
        // among ~100 Title-Case siblings.
        label: "Refresh Sync Diagnostics",
        section: .view,
        hint: "Re-run sync-system detection and reload the LiveSync config."
    ) { [weak appState] in appState?.refreshSyncDiagnostics() }

    register(
        SlateCommandID.showHistoryPanel,
        label: "Show History Panel",
        section: .view,
        hint: "Open the History leaf in the right pane."
    ) { [weak appState] in appState?.showHistoryPanel() }

    // ----- Graph (Milestone P). CommandSection.graph; zero new chords. -----

    register(
        SlateCommandID.showConnectionsPanel,
        label: "Show Connections",
        section: .graph,
        hint: "Open the Connections leaf — the active note's local graph."
    ) { [weak appState] in appState?.showConnectionsPanel() }

    register(
        SlateCommandID.connectionsDeeper,
        label: "Connections: Deeper",
        section: .graph,
        hint: "Increase the Connections leaf depth by one (up to 3 links away)."
    ) { [weak appState] in appState?.connectionsDeeper() }

    register(
        SlateCommandID.connectionsShallower,
        label: "Connections: Shallower",
        section: .graph,
        hint: "Decrease the Connections leaf depth by one (down to direct links)."
    ) { [weak appState] in appState?.connectionsShallower() }

    register(
        SlateCommandID.graphOrphans,
        label: "Graph: Orphaned Notes",
        section: .graph,
        hint: "Open the graph filtered to orphans — notes with no links in or out."
    ) { [weak appState] in appState?.openGraphPreset(.orphans) }

    register(
        SlateCommandID.graphUnresolved,
        label: "Graph: Unresolved Links",
        section: .graph,
        hint: "Open the graph filtered to unresolved targets — broken links."
    ) { [weak appState] in appState?.openGraphPreset(.unresolved) }

    register(
        SlateCommandID.graphMostLinked,
        label: "Graph: Most Linked Notes",
        section: .graph,
        hint: "Open the graph sorted by links in — the most-linked notes, the hubs."
    ) { [weak appState] in appState?.openGraphPreset(.mostLinked) }

    register(
        SlateCommandID.toggleRightPane,
        // Static noun for the palette (searchable, state-free); the menu
        // item owns the Hide/Show state-reflecting title. ⌥⌘I is the
        // single chord owner (menu-bar-homed — the #422 lesson).
        label: "Toggle Right Pane",
        section: .view,
        hotkey: "⌥⌘I",
        hint: "Hide or show the right pane (the panel rail). Option-Command-I."
    ) { [weak appState] in appState?.toggleRightPane() }

    register(
        SlateCommandID.openGraphTab,
        label: "Open Graph",
        section: .graph,
        hint: "Open the global graph as a sortable table."
    ) { [weak appState] in appState?.openGraphTab() }

    // Diagram-mode viewport (P2-3 #559). ⌘=/⌘−/⌘0 mirror the focus-routed
    // menu chords (canvas program rule R2 — palette-mirrored per surface);
    // these palette rows carry the chords as hotkeyHints and drive the
    // graph viewport when the diagram is active. Fit Graph is the new
    // ⌥⌘0 chord (R3): registered here + owned by the menu item.
    register(
        SlateCommandID.graphZoomIn,
        label: "Graph: Zoom In",
        section: .graph,
        hotkey: "⌘=",
        hint: "Zoom the visual diagram in. The zoom level is announced."
    ) { [weak appState] in appState?.graphDiagramZoomIn() }

    register(
        SlateCommandID.graphZoomOut,
        label: "Graph: Zoom Out",
        section: .graph,
        hotkey: "⌘-",
        hint: "Zoom the visual diagram out."
    ) { [weak appState] in appState?.graphDiagramZoomOut() }

    register(
        SlateCommandID.graphActualSize,
        label: "Graph: Actual Size",
        section: .graph,
        hotkey: "⌘0",
        hint: "Reset the visual diagram zoom to 100 percent."
    ) { [weak appState] in appState?.graphDiagramActualSize() }

    register(
        SlateCommandID.graphFitGraph,
        label: "Graph: Fit Graph",
        section: .graph,
        hotkey: "⌥⌘0",
        hint: "Zoom so every node is visible. Option-Command-0 on the diagram."
    ) { [weak appState] in appState?.graphDiagramFit() }

    register(
        SlateCommandID.graphWhereAmI,
        label: "Graph: Where Am I?",
        section: .graph,
        hotkey: "⌃⌘I",
        hint: "Read the selected node's row copy, its component, the zoom level, and the active filters."
    ) { [weak appState] in appState?.graphDiagramWhereAmI() }

    // ----- Workspace tabs (U1-2, #454) -----

    register(
        SlateCommandID.newTab,
        label: "Duplicate Tab",
        section: .view,
        // ⌘T (#863): the chord returned to the tab family when Quick
        // Open moved to ⌘O (Obsidian's actual quick-switcher default).
        // Duplicate Tab is Slate's "new tab" verb — a tab always hosts
        // an item (u1_spec §U1-2).
        hotkey: "⌘T",
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
        SlateCommandID.reopenClosedTab,
        label: "Reopen Closed Tab",
        section: .view,
        // ⇧⌘T (#863): the macOS/Obsidian convention, next to Close
        // Tab in the File menu.
        hotkey: "⇧⌘T",
        hint: "Reopen the most recently closed tab. Files that no longer exist are skipped."
    ) { [weak appState] in appState?.reopenClosedTab() }

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
        // ⇧⌘O (#863; was ⌘O — bare ⌘O is Quick Open now).
        hotkey: "⇧⌘O",
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
    ) { [weak appState] in
        guard let appState else { return }
        if let reason = appState.activeNoteSaveDisabledReason {
            throw CommandError.ActionFailed(message: reason)
        }
        appState.saveCurrentNote()
    }

    register(
        SlateCommandID.findInNote,
        // Matches the Edit ▸ Find ▸ Find… menu item (menu↔palette
        // unification).
        label: "Find…",
        section: .editor,
        // #874: ⌘F, Edit ▸ Find ▸ Find… scoped to the open editor —
        // reveals the note editor's find bar (searching.md:29). Routes
        // through `requestFindInFocusedSurface`, the same method the menu
        // item invokes, so a focused canvas/base filter keeps ⌘F.
        hotkey: "⌘F",
        hint: "Find and highlight text within the current note."
    ) { [weak appState] in appState?.requestFindInFocusedSurface() }

    // #868: the MENU items for these two are changeable labels
    // (Enter/Exit Reading Mode, Show/Hide Properties Source — see
    // SlateMacApp); the palette deliberately keeps the static nouns
    // below. A palette row is found by typing its name, so a
    // state-dependent label would make the command unfindable in
    // half its states — and the menu↔palette registry invariant is
    // CHORD parity, not label parity (the Tasks-Review verb/noun
    // split precedent).
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
    ) { [weak appState] in appState?.requestAddPropertySheet() }

    register(
        SlateCommandID.bulkRenameProperties,
        label: "Bulk Rename Properties…",
        section: .editor,
        hotkey: "⇧⌘R",
        hint: "Open the bulk-rename sheet to rename a property across the vault."
    ) { [weak appState] in appState?.isBulkRenameSheetOpen = true }

    register(
        SlateCommandID.editorZoomIn,
        label: "Editor: Zoom In",
        section: .editor,
        hint: "Increase the editing surfaces' text size one step."
    ) { [weak appState] in appState?.editorZoomIn() }

    register(
        SlateCommandID.editorZoomOut,
        label: "Editor: Zoom Out",
        section: .editor,
        hint: "Decrease the editing surfaces' text size one step."
    ) { [weak appState] in appState?.editorZoomOut() }

    register(
        SlateCommandID.editorActualSize,
        label: "Editor: Actual Size",
        section: .editor,
        hint: "Reset the editing surfaces' text size to 100 percent."
    ) { [weak appState] in appState?.editorActualSize() }

    register(
        SlateCommandID.toggleSpellCheck,
        // Label matches the Edit-menu item verbatim (menu↔palette
        // unification). No chord (#855) — menu + palette only.
        label: "Check Spelling While Typing",
        section: .editor,
        hint: "Toggle live spell checking in the note editor. Off by default for Markdown source."
    ) { [weak appState] in appState?.toggleEditorSpellCheck() }

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
        // ⌘R (#863; was ⇧⌘T, freed for Reopen Closed Tab). R = Review;
        // bare ⌘R was unbound and has no system-wide macOS claim in a
        // non-browser app.
        hotkey: "⌘R",
        // #879: reveals the vault-wide Tasks Review leaf in the right pane
        // (no longer a modal sheet).
        hint: "Open the vault-wide Tasks Review leaf in the right pane."
    ) { [weak appState] in appState?.openTasksReview() }
}
