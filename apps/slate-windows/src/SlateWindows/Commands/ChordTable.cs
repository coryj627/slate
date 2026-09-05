// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using uniffi.slate_uniffi;

namespace SlateWindows.Commands;

/// <summary>Where a Windows chord is actually delivered from.</summary>
/// <remarks>
/// Delivery, not advertisement. A menu item's <c>InputGestureText</c> is a
/// display string; the scope records the input surface whose key handler
/// runs. This distinction is what keeps <c>Ctrl+Enter</c> (editor, activate
/// at cursor) and <c>Ctrl+Enter</c> (Quick Open, open in a new tab) from
/// looking like a collision — they are two focus scopes, never live at the
/// same time.
/// </remarks>
internal enum ChordScope
{
    /// <summary>No chord.</summary>
    None,

    /// <summary><c>Window.InputBindings</c> or the shell's window-level
    /// <c>PreviewKeyDown</c> — live whenever the shell has focus.</summary>
    Global,

    /// <summary>The command palette overlay (W5-1), live only while the
    /// palette is open. Its opening chord is <see cref="Global"/>.</summary>
    Palette,

    /// <summary>A pane splitter thumb (<c>WeightedSplitPanel</c>), live only
    /// while the thumb has focus.</summary>
    Splitter,

    /// <summary>The reading view's structural navigator
    /// (<c>Reading/ReadingNavigator.cs</c>), live only with the reading
    /// surface focused.</summary>
    Reading,

    /// <summary>The <c>AccessibleDataGrid</c> substrate's
    /// <c>RoutedCommand</c> gestures, live only inside a grid.</summary>
    Grid,

    /// <summary>The Avalon editor control's own key handling
    /// (<c>SlateTextEditor.OnPreviewKeyDown</c>) or an editor popover.</summary>
    Editor,

    /// <summary>A property-row template's <c>TextBox.InputBindings</c>.</summary>
    PropertyRow,

    /// <summary>The Quick Open overlay's key handler.</summary>
    QuickOpen,

    /// <summary>The vault-search overlay (W5-2), live only while the
    /// overlay is open. Its opening chord is <see cref="Global"/>.</summary>
    SearchOverlay,

    /// <summary>The canvas projections' own key handling (W6-1 #745,
    /// rule R2: typing keys and arrows act only while a canvas
    /// projection has keyboard focus). PR C ships the navigator and its
    /// rows are delivered here, from the surface's tunnelling handler.
    /// PR A's three surface commands remain palette- and menu-only, so
    /// they carry no chord and <see cref="ChordScope.None"/> through
    /// <c>Reg</c>'s own rule.</summary>
    Canvas,

    /// <summary>The graph surface's own key handling (W6-2 #746). DECLARED
    /// by PR A (contract A-12) and first used by PR C's chorded rows; PR
    /// A's one row is chordless and carries <see cref="ChordScope.None"/>
    /// through <c>Reg</c>'s rule.</summary>
    Graph,
}

/// <summary>
/// One row of the chord table. A row is either a <c>slate.*</c> command
/// (registered or explicitly not, per PR-4) or a chord-only surface
/// interaction that no command backs.
/// </summary>
internal sealed record ChordTableEntry
{
    /// <summary>
    /// The row key. A <c>slate.*</c> value is a command id from the
    /// declared catalog (contract P3); anything else is a
    /// <c>windows.</c>-prefixed surface-interaction key that can never be
    /// mistaken for a command id.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>Human label. mac's verbatim for shared capabilities.</summary>
    public required string Label { get; init; }

    /// <summary>Palette accessibility hint. mac's verbatim for shared
    /// capabilities; <see langword="null"/> for chord-only rows.</summary>
    public string? Hint { get; init; }

    public CommandSection Section { get; init; } = CommandSection.View;

    /// <summary>The mac chord in glyph form, or <see langword="null"/> when
    /// mac has none. The mac spoken column is derived from this.</summary>
    public string? MacChord { get; init; }

    /// <summary>
    /// The Windows chord. <b>This is the only place a Windows chord string
    /// is authored</b> (PINV-5): menus, palette rows, spoken hotkeys, and
    /// <c>chords.json</c> all read it from here.
    /// </summary>
    public string? WindowsChord { get; init; }

    public ChordScope Scope { get; init; } = ChordScope.None;

    /// <summary>Whether the bridge registers this id in the core registry.
    /// <see langword="false"/> requires <see cref="Reason"/> (PR-4).</summary>
    public bool IsRegistered { get; init; }

    /// <summary>Why the row is not registered (PR-4 disposition), or why a
    /// chord-only row exists.</summary>
    public string? Reason { get; init; }

    /// <summary>Why the shipped Windows chord differs from what
    /// <see cref="MacToWindowsChordRule.Apply"/> predicts. Recorded, never
    /// rebound.</summary>
    public string? Divergence { get; init; }

    /// <summary>Why the shipped Windows LABEL differs from mac's
    /// byte-identical catalog label (the P3 census's disposition —
    /// W5-4 FD-5 is the first). Recorded, never relabeled; a stale
    /// disposition (labels match again) fails the census.</summary>
    public string? LabelDivergence { get; init; }

    /// <summary>Derived — never authored (mirror of mac's
    /// <c>HotkeySpoken.spoken(for:)</c>).</summary>
    public string? MacSpoken => MacHotkeySpoken.Spoken(MacChord);

    /// <summary>Derived — never authored (contract P12's producer).</summary>
    public string? WindowsSpoken => WindowsHotkeySpoken.Spoken(WindowsChord);

    /// <summary>True when the row names a command id rather than a
    /// surface-scoped interaction.</summary>
    public bool IsCommandId => Id.StartsWith("slate.", StringComparison.Ordinal);
}

/// <summary>
/// The one declarative catalog: command id, label, accessibility hint,
/// palette section, mac chord, and Windows chord, all in one artifact
/// (contracts P3 + P12, PINV-5).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why C# is the authored artifact and <c>chords.json</c> the
/// projection.</b> The registration bridge needs labels, hints, sections,
/// and chords at shell construction; reading them from a JSON file at
/// startup adds a failure mode where a missing or truncated file yields an
/// app with no commands at all. C# also gives compile-time id constants —
/// the twin of mac's <c>SlateCommandID</c> that contract P3 asks for — which
/// a JSON document cannot. <c>chords.json</c> stays on disk because
/// <c>scripts/generate-parity-matrix.py</c> and the W1 chord test read it;
/// <c>ChordTableTests</c> proves the file is exactly this table's
/// projection, so a hand edit to the JSON fails the build rather than
/// silently becoming a second source of truth.
/// </para>
/// <para>
/// <b>Coverage (PR-3).</b> The table records every chord the app delivers,
/// not only the ones a command backs: the 32 <c>ReadingNavigator</c>
/// chords, the grid's <c>Ctrl+F</c>/<c>Ctrl+Alt+S</c>, the editor's
/// <c>Ctrl+E</c>/<c>Ctrl+Enter</c>, the property row's
/// <c>Ctrl+Backspace</c>, <c>F2</c> rename, <c>Ctrl+1</c>–<c>Ctrl+9</c>,
/// and the Quick Open Enter-modifier family. It records what ships; it does
/// not re-adjudicate whether each binding was a good choice.
/// </para>
/// </remarks>
internal static class ChordTable
{
    /// <summary>
    /// Stable <c>slate.*</c> identifiers — the Windows twin of mac's
    /// <c>SlateCommandID</c> (contract P3).
    /// </summary>
    /// <remarks>
    /// Ids are <b>byte-identical to mac's</b> for every capability that
    /// exists on both platforms. A Windows-only capability takes a new id
    /// under the same <c>slate.&lt;section&gt;.&lt;verb&gt;</c> scheme and is
    /// marked as such in its catalog row. Once an id ships, changing it is a
    /// breaking change for recents and future keybindings — only add.
    /// </remarks>
    internal static class Ids
    {
        // Vault
        public const string VaultOpen = "slate.vault.open";
        public const string VaultClose = "slate.vault.close";

        // Navigation
        public const string QuickOpen = "slate.workspace.quickOpen";
        public const string JumpToBibliography = "slate.navigation.jumpToBibliography";

        // Workspace / View
        public const string NewTab = "slate.workspace.newTab";
        public const string CloseTab = "slate.workspace.closeTab";
        public const string ReopenClosedTab = "slate.workspace.reopenClosedTab";
        public const string NextTab = "slate.workspace.nextTab";
        public const string PreviousTab = "slate.workspace.previousTab";
        public const string MoveTabLeft = "slate.workspace.moveTabLeft";
        public const string MoveTabRight = "slate.workspace.moveTabRight";
        public const string SplitRight = "slate.workspace.splitRight";
        public const string SplitDown = "slate.workspace.splitDown";
        public const string FocusPaneLeft = "slate.workspace.focusPaneLeft";
        public const string FocusPaneRight = "slate.workspace.focusPaneRight";
        public const string FocusPaneAbove = "slate.workspace.focusPaneAbove";
        public const string FocusPaneBelow = "slate.workspace.focusPaneBelow";
        public const string GrowPane = "slate.workspace.growPane";
        public const string ShrinkPane = "slate.workspace.shrinkPane";
        public const string ClosePane = "slate.workspace.closePane";
        public const string OpenInNewTab = "slate.workspace.openInNewTab";
        public const string OpenInSplit = "slate.workspace.openInSplit";
        public const string ToggleRightPane = "slate.view.toggleRightPane";
        public const string ToggleSearch = "slate.view.toggleSearch";
        public const string ShowHistoryPanel = "slate.history.showPanel";
        public const string RefreshSyncDiagnostics = "slate.diagnostics.refreshSync";

        // Windows-only workspace capabilities (PR-4 orphans given a surface).
        public const string FocusNextPane = "slate.workspace.focusNextPane";
        public const string FocusPreviousPane = "slate.workspace.focusPreviousPane";

        // Editor
        public const string Save = "slate.editor.save";
        public const string ToggleViewMode = "slate.editor.toggleViewMode";
        public const string CitationSummary = "slate.editor.citationSummary";
        public const string AddProperty = "slate.editor.addProperty";
        public const string BulkRenameProperties = "slate.editor.bulkRenameProperties";
        public const string EditorZoomIn = "slate.editor.zoomIn";
        public const string EditorZoomOut = "slate.editor.zoomOut";
        public const string EditorActualSize = "slate.editor.actualSize";
        public const string ToggleSpellCheck = "slate.editor.toggleSpellCheck";

        // Windows-only editor capabilities.
        public const string ActivateAtCaret = "slate.editor.activateAtCaret";
        public const string PreviewEmbed = "slate.editor.previewEmbed";
        public const string ToggleReadingLinkTarget = "slate.editor.toggleReadingLinkTarget";
        public const string ToggleHistoryChangesSinceOpen =
            "slate.editor.toggleHistoryChangesSinceOpen";
        public const string ReloadBibliography = "slate.editor.reloadBibliography";

        // Tasks
        public const string TasksReview = "slate.tasks.review";

        // Bases
        public const string BasesOpenViewSwitcher = "slate.bases.openViewSwitcher";
        public const string BasesNextView = "slate.bases.nextView";
        public const string BasesPreviousView = "slate.bases.previousView";
        public const string BasesSortByColumn = "slate.bases.sortByColumn";
        public const string BasesSaveSortToView = "slate.bases.saveSortToView";
        public const string BasesViewAsTable = "slate.bases.viewAsTable";
        public const string BasesViewAsList = "slate.bases.viewAsList";
        public const string BasesQuickFilter = "slate.bases.quickFilter";
        public const string BasesWhereAmI = "slate.bases.whereAmI";
        public const string BasesOpenRow = "slate.bases.openRow";
        public const string BasesCopyLink = "slate.bases.copyLink";
        public const string BasesShowBacklinks = "slate.bases.showBacklinks";
        public const string BasesEditProperty = "slate.bases.editProperty";
        public const string BasesExportCsv = "slate.bases.exportCsv";
        public const string BasesExportMarkdown = "slate.bases.exportMarkdown";
        public const string BasesCopyMarkdown = "slate.bases.copyMarkdown";
        public const string BasesResultsPopover = "slate.bases.resultsPopover";
        public const string BasesRefresh = "slate.bases.refresh";
        public const string BasesNewQuery = "slate.bases.newQuery";
        public const string BasesEditViewFilters = "slate.bases.editViewFilters";
        public const string BasesBuilderAddCondition = "slate.bases.builder.addCondition";
        public const string BasesBuilderAddGroup = "slate.bases.builder.addGroup";
        public const string BasesBuilderEditCondition = "slate.bases.builder.editCondition";
        public const string BasesBuilderRemoveCondition = "slate.bases.builder.removeCondition";

        // Graph (W6-2 #746, PR A). Ids are byte-identical to mac's.
        public const string GraphOpenTab = "slate.graph.openTab";

        // Graph (W6-2 #746, PR B slice B1, contract B-14): the Connections
        // leaf's three chordless rows.
        public const string GraphShowConnections = "slate.graph.showConnections";
        public const string GraphConnectionsDeeper = "slate.graph.connectionsDeeper";
        public const string GraphConnectionsShallower = "slate.graph.connectionsShallower";

        // Canvas (W6-1 #745). Ids are byte-identical to mac's.
        public const string CanvasShowOutline = "slate.canvas.showOutline";
        public const string CanvasShowTable = "slate.canvas.showTable";
        public const string CanvasShowVisual = "slate.canvas.showVisual";
        public const string CanvasWhereAmI = "slate.canvas.whereAmI";
        public const string CanvasNextCard = "slate.canvas.nextCard";
        public const string CanvasPreviousCard = "slate.canvas.previousCard";
        public const string CanvasEnterGroup = "slate.canvas.enterGroup";
        public const string CanvasExitGroup = "slate.canvas.exitGroup";
        public const string CanvasFollowConnectionForward =
            "slate.canvas.followConnectionForward";
        public const string CanvasFollowConnectionBack =
            "slate.canvas.followConnectionBack";
        public const string CanvasTracePath = "slate.canvas.tracePath";
        public const string CanvasToggleMark = "slate.canvas.toggleMark";
        public const string CanvasDelete = "slate.canvas.delete";
        public const string CanvasEditCard = "slate.canvas.editCard";
        public const string CanvasRenameGroup = "slate.canvas.renameGroup";
        public const string CanvasSetColor = "slate.canvas.setColor";
        public const string CanvasClearColor = "slate.canvas.clearColor";
        public const string CanvasNewGroup = "slate.canvas.newGroup";
        public const string CanvasAddLink = "slate.canvas.addLink";
        public const string CanvasMoveIntoGroup = "slate.canvas.moveIntoGroup";
        public const string CanvasEditConnection = "slate.canvas.editConnection";
        public const string CanvasDeleteConnection = "slate.canvas.deleteConnection";
        public const string CanvasAddNote = "slate.canvas.addNote";
        public const string CanvasAddMedia = "slate.canvas.addMedia";
        public const string CanvasLocateFile = "slate.canvas.locateFile";
        public const string CanvasRemoveFromGroup = "slate.canvas.removeFromGroup";
        public const string CanvasCreateConnectedCard = "slate.canvas.createConnectedCard";
        public const string CanvasCreateConnectedCardDirectional =
            "slate.canvas.createConnectedCardDirectional";
        public const string CanvasDuplicate = "slate.canvas.duplicate";
        public const string CanvasConvertToNote = "slate.canvas.convertToNote";

        public const string CanvasClearMarks = "slate.canvas.clearMarks";

        public const string CanvasShowMarks = "slate.canvas.showMarks";

        public const string CanvasGroupMarked = "slate.canvas.groupMarked";

        public const string CanvasDeleteMarked = "slate.canvas.deleteMarked";

        public const string CanvasColorMarked = "slate.canvas.colorMarked";

        public const string CanvasConnectTo = "slate.canvas.connectTo";

        public const string CanvasConnectMode = "slate.canvas.connectMode";

        public const string CanvasPlaceBelow = "slate.canvas.placeBelow";

        public const string CanvasPlaceRightOf = "slate.canvas.placeRightOf";

        public const string CanvasPlaceAbove = "slate.canvas.placeAbove";

        public const string CanvasPlaceLeftOf = "slate.canvas.placeLeftOf";

        public const string CanvasAlignWith = "slate.canvas.alignWith";

        public const string CanvasMoveMode = "slate.canvas.moveMode";

        public const string CanvasResizeMode = "slate.canvas.resizeMode";

        public const string CanvasResizeDefaultSize = "slate.canvas.resizeDefaultSize";

        public const string CanvasResizeFitContent = "slate.canvas.resizeFitContent";

        public const string CanvasNewCard = "slate.canvas.newCard";
        public const string CanvasFilterCards = "slate.canvas.filterCards";
        public const string CanvasClearFilter = "slate.canvas.clearFilter";
        public const string CanvasCommitMode = "slate.canvas.commitMode";
        public const string CanvasCancelMode = "slate.canvas.cancelMode";
        public const string CanvasZoomIn = "slate.canvas.zoomIn";
        public const string CanvasZoomOut = "slate.canvas.zoomOut";
        public const string CanvasActualSize = "slate.canvas.actualSize";
        public const string CanvasFitCanvas = "slate.canvas.fitCanvas";
        public const string CanvasZoomToSelection = "slate.canvas.zoomToSelection";
        public const string CanvasToggleFollowSelection =
            "slate.canvas.toggleFollowSelection";

        // Sidebar / file management (mac projects these into CommandSection.sidebar).
        public const string SidebarOpen = "slate.sidebar.open";
        public const string NewNote = "slate.file.newNote";
        public const string NewCanvas = "slate.file.newCanvas";
        public const string NewFromTemplate = "slate.file.newFromTemplate";
        public const string NewFolder = "slate.file.newFolder";
        public const string ImportFilesAndFolders = "slate.file.importFilesAndFolders";
        public const string CancelImport = "slate.file.cancelImport";
        public const string RenameEntry = "slate.file.rename";
        public const string MoveTo = "slate.file.moveTo";
        public const string DeleteEntry = "slate.file.delete";
        public const string DuplicateEntry = "slate.file.duplicate";
        public const string CopyPath = "slate.file.copyPath";
        public const string RevealInFinder = "slate.file.revealInFinder";
        public const string CopyWikilink = "slate.sidebar.copyWikilink";
        public const string PinNote = "slate.sidebar.pinNote";
        public const string UnpinNote = "slate.sidebar.unpinNote";
        public const string UnpinAllInFolder = "slate.sidebar.unpinAllInFolder";
        public const string UseVaultDefaultSort = "slate.sidebar.useVaultDefaultSort";
        public const string AddShortcut = "slate.sidebar.addShortcut";
        public const string RemoveShortcut = "slate.sidebar.removeShortcut";
        public const string ClearRecents = "slate.sidebar.clearRecents";
        public const string CollapseAll = "slate.sidebar.collapseAll";
        public const string ExpandLoaded = "slate.sidebar.expandLoaded";
        public const string HistoryBack = "slate.sidebar.historyBack";
        public const string HistoryForward = "slate.sidebar.historyForward";
        public const string FocusFilter = "slate.sidebar.focusFilter";
        public const string ToggleLayout = "slate.sidebar.toggleLayout";
        public const string AddTag = "slate.sidebar.addTag";
        public const string RemoveTag = "slate.sidebar.removeTag";
        public const string CreateFolderNote = "slate.sidebar.createFolderNote";
        public const string DeleteFolderNote = "slate.sidebar.deleteFolderNote";

        // Windows-only sidebar capabilities.
        public const string SidebarRefresh = "slate.sidebar.refresh";
        public const string SidebarClearFilter = "slate.sidebar.clearFilter";
        public const string SidebarToggleTags = "slate.sidebar.toggleTags";
        public const string SidebarTrashSelected = "slate.sidebar.trashSelected";

        public static string OpenShortcut(int slot) => $"slate.sidebar.openShortcut{slot}";
    }

    /// <summary>Reason text shared by every row a parameterized command
    /// makes unregisterable.</summary>
    private const string ParameterizedReason =
        "PR-4: parameterized — the action needs a CommandParameter the palette "
        + "cannot supply, so it is a surface interaction, not an app command.";

    /// <summary>Reason text shared by the overlay-scoped interactions.</summary>
    private const string OverlayReason =
        "PR-4: overlay-scoped interaction of an already-open surface (the arrow-"
        + "keys-in-a-list rule), not an app command.";

    private static readonly ChordTableEntry[] Rows = BuildRows();

    /// <summary>Every row: commands and chord-only surface interactions.</summary>
    public static IReadOnlyList<ChordTableEntry> Entries => Rows;

    /// <summary>The declared id catalog (contract P3) — every row that names
    /// a <c>slate.*</c> command id, registered or not.</summary>
    public static IReadOnlyList<ChordTableEntry> CommandRows { get; } =
        Rows.Where(row => row.IsCommandId).ToArray();

    /// <summary>The rows the registration bridge actually registers.</summary>
    public static IReadOnlyList<ChordTableEntry> RegisteredRows { get; } =
        Rows.Where(row => row.IsRegistered).ToArray();

    private static readonly Dictionary<string, ChordTableEntry> ById =
        Rows.ToDictionary(row => row.Id, StringComparer.Ordinal);

    public static ChordTableEntry? Find(string id) =>
        ById.TryGetValue(id, out ChordTableEntry? row) ? row : null;

    /// <summary>The Windows chord for a command id, or <see langword="null"/>.
    /// Menus, palette rows, and spoken hotkeys all read it from here.</summary>
    public static string? WindowsChordFor(string id) => Find(id)?.WindowsChord;

    /// <summary>The spoken Windows chord for a command id, or
    /// <see langword="null"/> — what a palette row appends to its accessible
    /// name (contract P6).</summary>
    public static string? WindowsSpokenFor(string id) => Find(id)?.WindowsSpoken;

    /// <summary>
    /// The sidebar action catalog's id order (contract P16), mirroring mac's
    /// <c>SidebarActionCatalog.actions.map(\.id)</c> so the palette's Sidebar
    /// section renders in catalog order rather than alphabetically. Data, not
    /// policy. Windows-only additions follow mac's order, at the end.
    /// </summary>
    public static string[] SidebarPinnedOrder { get; } =
    [
        Ids.SidebarOpen,
        Ids.NewNote,
        Ids.NewFolder,
        Ids.NewFromTemplate,
        Ids.ImportFilesAndFolders,
        Ids.RenameEntry,
        Ids.MoveTo,
        Ids.DuplicateEntry,
        Ids.RevealInFinder,
        Ids.CopyPath,
        Ids.CopyWikilink,
        Ids.PinNote,
        Ids.UnpinNote,
        Ids.UnpinAllInFolder,
        Ids.UseVaultDefaultSort,
        Ids.AddShortcut,
        Ids.RemoveShortcut,
        Ids.ClearRecents,
        Ids.CollapseAll,
        Ids.ExpandLoaded,
        Ids.HistoryBack,
        Ids.HistoryForward,
        Ids.OpenShortcut(1),
        Ids.OpenShortcut(2),
        Ids.OpenShortcut(3),
        Ids.OpenShortcut(4),
        Ids.OpenShortcut(5),
        Ids.OpenShortcut(6),
        Ids.OpenShortcut(7),
        Ids.OpenShortcut(8),
        Ids.OpenShortcut(9),
        Ids.ToggleLayout,
        Ids.AddTag,
        Ids.RemoveTag,
        Ids.CreateFolderNote,
        Ids.DeleteFolderNote,
        Ids.DeleteEntry,
        Ids.CancelImport,
        Ids.SidebarRefresh,
        Ids.SidebarClearFilter,
        Ids.SidebarToggleTags,
        Ids.SidebarTrashSelected,
    ];

    /// <summary>The <c>chords.json</c> schema this table projects.</summary>
    /// <remarks>
    /// Bumped from 1 in W5-1: rows gained <c>label</c>, <c>section</c>,
    /// <c>hint</c>, <c>macChord</c>, <c>scope</c>, <c>registered</c>,
    /// <c>divergence</c>, and <c>reason</c>, and the file gained the
    /// <c>chordSurface</c> array. Bumped to 3 in W5-4: rows gained the
    /// optional <c>labelDivergence</c> (FD-5's recorded label
    /// disposition). <c>deliveryEvidence</c> is untouched —
    /// <c>scripts/generate-parity-matrix.py</c> performs twelve fatal
    /// validations on that object and its shape must survive unchanged.
    /// </remarks>
    public const int SchemaVersion = 3;

    /// <summary>
    /// Overwrite <paramref name="root"/>'s <c>schemaVersion</c>,
    /// <c>commands</c>, and <c>chordSurface</c> with this table's
    /// projection, leaving every other property — notably
    /// <c>deliveryEvidence</c> — exactly as it was.
    /// </summary>
    /// <remarks>
    /// <c>commands</c> holds every row that names a <c>slate.*</c> id;
    /// <c>chordSurface</c> holds the focus-scoped interactions that no
    /// command backs. Splitting them is what keeps the W1 chord test's
    /// "no two active Windows chords collide" invariant meaningful: two
    /// surfaces that are never focused at the same time may legitimately
    /// share a chord string (<c>Ctrl+Enter</c> in the editor and in Quick
    /// Open), and folding those into one flat list would turn a correct
    /// design into a false collision.
    /// </remarks>
    public static JsonObject Project(JsonObject root)
    {
        ArgumentNullException.ThrowIfNull(root);
        root["schemaVersion"] = SchemaVersion;

        var commands = new JsonArray();
        var surface = new JsonArray();
        foreach (ChordTableEntry row in Rows)
        {
            (row.IsCommandId ? commands : surface).Add(ProjectRow(row));
        }

        root["commands"] = commands;
        root["chordSurface"] = surface;
        return root;
    }

    private static JsonObject ProjectRow(ChordTableEntry row)
    {
        var node = new JsonObject
        {
            ["id"] = row.Id,
            ["label"] = row.Label,
            ["section"] = row.Section.ToString(),
            ["hint"] = row.Hint,
            ["macChord"] = row.MacChord,
            ["mac"] = row.MacSpoken,
            ["windows"] = row.WindowsChord,
            ["windowsSpoken"] = row.WindowsSpoken,
            ["scope"] = row.Scope.ToString(),
            ["registered"] = row.IsRegistered,
        };
        if (row.Divergence is not null)
        {
            node["divergence"] = row.Divergence;
        }

        if (row.LabelDivergence is not null)
        {
            node["labelDivergence"] = row.LabelDivergence;
        }

        if (row.Reason is not null)
        {
            node["reason"] = row.Reason;
        }

        return node;
    }

    private static ChordTableEntry Reg(
        string id,
        string label,
        CommandSection section,
        string hint,
        string? mac = null,
        string? win = null,
        ChordScope scope = ChordScope.Global,
        string? divergence = null,
        string? labelDivergence = null) =>
        new()
        {
            Id = id,
            Label = label,
            Hint = hint,
            Section = section,
            MacChord = mac,
            WindowsChord = win,
            Scope = win is null ? ChordScope.None : scope,
            IsRegistered = true,
            Divergence = divergence,
            LabelDivergence = labelDivergence,
        };

    private static ChordTableEntry Unreg(
        string id,
        string label,
        CommandSection section,
        string reason,
        string? mac = null,
        string? win = null,
        ChordScope scope = ChordScope.None,
        string? divergence = null) =>
        new()
        {
            Id = id,
            Label = label,
            Section = section,
            MacChord = mac,
            WindowsChord = win,
            Scope = win is null ? ChordScope.None : scope,
            IsRegistered = false,
            Reason = reason,
            Divergence = divergence,
        };

    private static ChordTableEntry Chord(
        string key,
        string label,
        string win,
        ChordScope scope,
        string reason,
        string? mac = null,
        string? divergence = null) =>
        new()
        {
            Id = key,
            Label = label,
            Section = CommandSection.View,
            MacChord = mac,
            WindowsChord = win,
            Scope = scope,
            IsRegistered = false,
            Reason = reason,
            Divergence = divergence,
        };

    private static ChordTableEntry[] BuildRows()
    {
        var rows = new List<ChordTableEntry>();
        rows.AddRange(VaultRows());
        rows.AddRange(NavigationRows());
        rows.AddRange(WorkspaceRows());
        rows.AddRange(EditorRows());
        rows.AddRange(BasesRows());
        rows.AddRange(CanvasRows());
        rows.AddRange(GraphRows());
        rows.AddRange(SidebarRows());
        rows.AddRange(SurfaceChordRows());
        return [.. rows];
    }

    private static IEnumerable<ChordTableEntry> VaultRows() =>
    [
        Reg(Ids.VaultOpen, "Open Vault…", CommandSection.Vault,
            "Show the open-folder picker.", "⇧⌘O", "Ctrl+Shift+O"),
        Reg(Ids.VaultClose, "Close Vault", CommandSection.Vault,
            "Close the current vault and return to the welcome screen."),
        Unreg("windows.vault.openRecent", "Open Recent Vault", CommandSection.Vault,
            ParameterizedReason),
    ];

    private static IEnumerable<ChordTableEntry> NavigationRows() =>
    [
        Reg(Ids.QuickOpen, "Quick Open…", CommandSection.Navigation,
            "Fuzzy-find a note by name and open it. Return opens it in the current tab.",
            "⌘O", "Ctrl+O"),
        Reg(Ids.JumpToBibliography, "Jump to Bibliography", CommandSection.Navigation,
            "Filter the Bibliography sidebar to the expanded citation's key.",
            "⌘J", "Ctrl+J"),
    ];

    private static IEnumerable<ChordTableEntry> WorkspaceRows() =>
    [
        Reg(Ids.NewTab, "Duplicate Tab", CommandSection.View,
            "Duplicate the current note into a new tab.", "⌘T", "Ctrl+T"),
        Reg(Ids.CloseTab, "Close Tab", CommandSection.View,
            "Close the active tab. Prompts if it has unsaved changes.", "⌘W", "Ctrl+W"),
        Reg(Ids.ReopenClosedTab, "Reopen Closed Tab", CommandSection.View,
            "Reopen the most recently closed tab. Files that no longer exist are skipped.",
            "⇧⌘T", "Ctrl+Shift+T"),
        Reg(Ids.NextTab, "Show Next Tab", CommandSection.View,
            "Activate the tab to the right, wrapping at the end.", "⇧⌘]", "Ctrl+Shift+]"),
        Reg(Ids.PreviousTab, "Show Previous Tab", CommandSection.View,
            "Activate the tab to the left, wrapping at the start.", "⇧⌘[", "Ctrl+Shift+["),
        Reg(Ids.MoveTabLeft, "Move Tab Left", CommandSection.View,
            "Reorder the active tab one position left.", "⌃⌘←", "Ctrl+Alt+Shift+Left",
            divergence:
            "G18: the rule maps ⌃⌘← to Ctrl+Alt+Left, which focusPaneLeft (⌥⌘←) "
            + "already owns. Shift disambiguates. Shipped in W1-3; recorded, not rebound."),
        Reg(Ids.MoveTabRight, "Move Tab Right", CommandSection.View,
            "Reorder the active tab one position right.", "⌃⌘→", "Ctrl+Alt+Shift+Right",
            divergence:
            "G18: the rule maps ⌃⌘→ to Ctrl+Alt+Right, which focusPaneRight (⌥⌘→) "
            + "already owns. Shift disambiguates. Shipped in W1-3; recorded, not rebound."),
        Reg(Ids.SplitRight, "Split Right", CommandSection.View,
            "Split the focused pane side-by-side; the new pane shows the same note.",
            "⌘\\", "Ctrl+\\"),
        Reg(Ids.SplitDown, "Split Down", CommandSection.View,
            "Split the focused pane top-and-bottom; the new pane shows the same note.",
            "⌥⌘\\", "Ctrl+Alt+\\"),
        Reg(Ids.FocusPaneLeft, "Focus Pane Left", CommandSection.View,
            "Move focus to the pane to the left.", "⌥⌘←", "Ctrl+Alt+Left"),
        Reg(Ids.FocusPaneRight, "Focus Pane Right", CommandSection.View,
            "Move focus to the pane to the right.", "⌥⌘→", "Ctrl+Alt+Right"),
        Reg(Ids.FocusPaneAbove, "Focus Pane Above", CommandSection.View,
            "Move focus to the pane above.", "⌥⌘↑", "Ctrl+Alt+Up"),
        Reg(Ids.FocusPaneBelow, "Focus Pane Below", CommandSection.View,
            "Move focus to the pane below.", "⌥⌘↓", "Ctrl+Alt+Down"),
        Reg(Ids.GrowPane, "Grow Pane", CommandSection.View,
            "Make the focused pane larger.", "⌥⌘=", "Ctrl+Alt+="),
        Reg(Ids.ShrinkPane, "Shrink Pane", CommandSection.View,
            "Make the focused pane smaller.", "⌥⌘-", "Ctrl+Alt+-"),
        Reg(Ids.ClosePane, "Close Pane", CommandSection.View,
            "Close the focused pane's tabs, prompting for unsaved changes."),
        Reg(Ids.OpenInNewTab, "Open Selected File in New Tab", CommandSection.View,
            "Open the sidebar's selected file in a new tab."),
        Reg(Ids.OpenInSplit, "Open Selected File in Split", CommandSection.View,
            "Open the sidebar's selected file in a new split pane."),
        Reg(Ids.ToggleRightPane, "Toggle Right Pane", CommandSection.View,
            "Hide or show the right pane (the panel rail). Control-Alt-I.",
            "⌥⌘I", "Ctrl+Alt+I"),
        // W5-2 close-out (#742): the vault-search overlay's toggle,
        // registered with mac's exact label and hint. The resolved
        // ICommand is UNGUARDED — mac's palette action calls
        // toggleSearchOverlay() with no modal gate
        // (SlateCommands.swift:1483-1494), and the palette invokes
        // BEFORE dismissing (P9), so a modal-decision-aware guard
        // would refuse every palette invocation. The modal gate stays
        // on the chord path only: Ctrl+Shift+F is still delivered
        // imperatively from MainWindow.Window_PreviewKeyDown, where
        // TryClearTheWayForSearch lives (the ChordTableTests
        // imperative allow-list records why no KeyBinding exists).
        Reg(Ids.ToggleSearch, "Search Vault", CommandSection.View,
            "Toggle the vault-wide search overlay.",
            "⇧⌘F", "Ctrl+Shift+F"),
        Reg(Ids.ShowHistoryPanel, "Show History Panel", CommandSection.View,
            "Open the History leaf in the right pane."),
        Reg(Ids.RefreshSyncDiagnostics, "Refresh Sync Diagnostics", CommandSection.View,
            "Re-run sync-system detection and reload the LiveSync config."),

        // PR-4 orphans: real capabilities with no binding at all. The
        // disposition rule registers them so the palette becomes their surface.
        Reg(Ids.FocusNextPane, "Focus Next Pane", CommandSection.View,
            "Move focus to the next pane in layout order, wrapping at the end."),
        Reg(Ids.FocusPreviousPane, "Focus Previous Pane", CommandSection.View,
            "Move focus to the previous pane in layout order, wrapping at the start."),

        Unreg("windows.workspace.closeSpecificTab", "Close Tab (by tab)",
            CommandSection.View,
            ParameterizedReason + " slate.workspace.closeTab is the palette surface."),
    ];

    private static IEnumerable<ChordTableEntry> EditorRows() =>
    [
        Reg(Ids.Save, "Save", CommandSection.Editor,
            "Save the current note to disk.", "⌘S", "Ctrl+S"),
        Reg(Ids.ToggleViewMode, "Toggle Reading Mode", CommandSection.Editor,
            "Switch the current note between editing and reading mode.",
            "⇧⌘E", "Ctrl+Shift+E"),
        Reg(Ids.CitationSummary, "Citation Summary", CommandSection.Editor,
            "Open the citation summary for the current note.", "⇧⌘J", "Ctrl+Shift+J"),
        Reg(Ids.AddProperty, "Add Property…", CommandSection.Editor,
            "Add a new frontmatter property to the current note."),
        Reg(Ids.BulkRenameProperties, "Bulk Rename Properties…", CommandSection.Editor,
            "Open the bulk-rename sheet to rename a property across the vault.",
            "⇧⌘R", "Ctrl+Shift+R"),
        Reg(Ids.EditorZoomIn, "Editor: Zoom In", CommandSection.Editor,
            "Increase the editing surfaces' text size one step."),
        Reg(Ids.EditorZoomOut, "Editor: Zoom Out", CommandSection.Editor,
            "Decrease the editing surfaces' text size one step."),
        Reg(Ids.EditorActualSize, "Editor: Actual Size", CommandSection.Editor,
            "Reset the editing surfaces' text size to 100 percent."),
        Reg(Ids.ToggleSpellCheck, "Check Spelling While Typing", CommandSection.Editor,
            "Toggle live spell checking in the note editor. Off by default for Markdown source."),

        // Windows-only editor capabilities. Both chords are delivered by the
        // Avalon control itself (SlateTextEditor.OnPreviewKeyDown:170,177) and
        // only advertised by the Editor menu's InputGestureText.
        Reg(Ids.ActivateAtCaret, "Activate at Cursor", CommandSection.Editor,
            "Activate the link, tag, citation, embed, or task under the cursor.",
            win: "Ctrl+Enter", scope: ChordScope.Editor),
        Reg(Ids.PreviewEmbed, "Preview Embed", CommandSection.Editor,
            "Open the embed under the cursor in the interaction popover.",
            win: "Ctrl+E", scope: ChordScope.Editor),
        Reg(Ids.ToggleReadingLinkTarget, "Open Reading Links in New Tab",
            CommandSection.Editor,
            "Choose whether activating a reading-mode link opens a new tab or reuses the current one."),
        Reg(Ids.ToggleHistoryChangesSinceOpen, "Show Changes Since Last Open",
            CommandSection.Editor,
            "Show what changed in a note since it was last opened. Takes effect at the next note activation."),
        Reg(Ids.ReloadBibliography, "Reload Bibliography", CommandSection.Editor,
            "Re-read the vault's bibliography sources after fixing them."),

        Reg(Ids.TasksReview, "Tasks Review", CommandSection.Tasks,
            "Open the vault-wide Tasks Review leaf in the right pane.", "⌘R", "Ctrl+R"),

        Unreg("windows.editor.setMathSpeechStyle", "Math Speech Style",
            CommandSection.Editor, ParameterizedReason),
        Unreg("windows.editor.setMathVerbosity", "Math Verbosity",
            CommandSection.Editor, ParameterizedReason),
        Unreg("windows.editor.setMathBrailleCode", "Math Braille Code",
            CommandSection.Editor, ParameterizedReason),
        Unreg("windows.editor.setCodePreambleVerbosity", "Code Preamble Verbosity",
            CommandSection.Editor, ParameterizedReason),
        Unreg("windows.editor.openCitationLink", "Open Citation Link",
            CommandSection.Editor, ParameterizedReason),
        Unreg("windows.editor.pickWikilinkTarget", "Pick Wikilink Target",
            CommandSection.Editor, ParameterizedReason),
        Unreg("windows.editor.closeAddPropertySheet", "Close Add Property Sheet",
            CommandSection.Editor, OverlayReason),
        Unreg("windows.editor.closeBulkRenameSheet", "Close Bulk Rename Sheet",
            CommandSection.Editor, OverlayReason),
        Unreg("windows.editor.closeCitationDetails", "Close Citation Details",
            CommandSection.Editor, OverlayReason),
        Unreg("windows.editor.closeCitationSummary", "Close Citation Summary",
            CommandSection.Editor, OverlayReason),
        Unreg("windows.editor.closeFilesCiting", "Close Files Citing",
            CommandSection.Editor, OverlayReason),
        Unreg("windows.editor.openPopoverSource", "Open Popover Source",
            CommandSection.Editor, OverlayReason),
        Unreg("windows.propertyRow.editorVerbs", "Property row editor verbs",
            CommandSection.Editor,
            "PR-4: the eleven PropertyRowViewModel commands (commit, revert, delete, step "
            + "up/down, add item, toggle boolean, remove item, and the three list-row "
            + "pass-throughs) are row actions — they need a row context the palette "
            + "cannot supply. Mac draws the same line for History's Compare/Restore."),
        Unreg("windows.grid.routedCommands", "AccessibleDataGrid routed commands",
            CommandSection.Editor,
            "PR-4: the four static RoutedCommands (export CSV, export Markdown, toggle "
            + "sort, filter) are the grid substrate's, scoped to a focused grid. The "
            + "app-level capabilities are the registered slate.bases.* ids."),
    ];

    private static IEnumerable<ChordTableEntry> BasesRows() =>
    [
        Reg(Ids.BasesOpenViewSwitcher, "Bases: Open View Switcher", CommandSection.Bases,
            "List the views in the active base."),
        Reg(Ids.BasesNextView, "Bases: Next View", CommandSection.Bases,
            "Switch to the next view in the active base."),
        Reg(Ids.BasesPreviousView, "Bases: Previous View", CommandSection.Bases,
            "Switch to the previous view in the active base."),
        Reg(Ids.BasesSortByColumn, "Bases: Sort by Column", CommandSection.Bases,
            "Sort the active base table from the focused column.",
            win: "Ctrl+Alt+S", scope: ChordScope.Grid),
        Reg(Ids.BasesSaveSortToView, "Bases: Save Sort to View", CommandSection.Bases,
            "Persist the current base table sort to the active view."),
        Reg(Ids.BasesViewAsTable, "Bases: View as Table", CommandSection.Bases,
            "Temporarily render the active base with table cell navigation."),
        Reg(Ids.BasesViewAsList, "Bases: View as List", CommandSection.Bases,
            "Temporarily render the active base with row navigation."),
        Reg(Ids.BasesQuickFilter, "Bases: Quick Filter", CommandSection.Bases,
            "Focus the active base's temporary quick filter field.",
            win: "Ctrl+F", scope: ChordScope.Grid),
        Reg(Ids.BasesWhereAmI, "Bases: Where Am I?", CommandSection.Bases,
            "Read the active base, view, and temporary quick filter."),
        Reg(Ids.BasesOpenRow, "Bases: Open Row", CommandSection.Bases,
            "Open the selected base result row."),
        Reg(Ids.BasesCopyLink, "Bases: Copy Link", CommandSection.Bases,
            "Copy a wikilink to the selected base result row."),
        Reg(Ids.BasesShowBacklinks, "Bases: Show Backlinks", CommandSection.Bases,
            "Show backlinks for the selected base result row."),
        Reg(Ids.BasesEditProperty, "Bases: Edit Property", CommandSection.Bases,
            "Edit the selected editable base property cell."),
        Reg(Ids.BasesExportCsv, "Bases: Export View as CSV", CommandSection.Bases,
            "Export the active base view as CSV."),
        Reg(Ids.BasesExportMarkdown, "Bases: Export View as Markdown Table",
            CommandSection.Bases, "Export the active base view as a Markdown table."),
        Reg(Ids.BasesCopyMarkdown, "Bases: Copy View as Markdown", CommandSection.Bases,
            "Copy the active base view as a Markdown table."),
        Reg(Ids.BasesResultsPopover, "Bases: Results", CommandSection.Bases,
            "Read the result count and summary for the active base."),
        Reg(Ids.BasesRefresh, "Bases: Refresh", CommandSection.Bases,
            "Reload the active base and re-run its current view."),
        Reg(Ids.BasesNewQuery, "Bases: New Query", CommandSection.Bases,
            "Open the structured Bases query builder."),
        Reg(Ids.BasesEditViewFilters, "Bases: Edit View Filters", CommandSection.Bases,
            "Open the active base view in the structured query builder."),

        // The four builder ids exist on mac and are declared here for id
        // stability, but the Windows builder surfaces them as sheet buttons
        // on the open builder rather than as ICommand objects.
        Unreg(Ids.BasesBuilderAddCondition, "Bases: Add Condition", CommandSection.Bases,
            OverlayReason + " The Windows builder sheet owns the row verbs."),
        Unreg(Ids.BasesBuilderAddGroup, "Bases: Add Group", CommandSection.Bases,
            OverlayReason + " The Windows builder sheet owns the row verbs."),
        Unreg(Ids.BasesBuilderEditCondition, "Bases: Edit Condition", CommandSection.Bases,
            OverlayReason + " The Windows builder sheet owns the row verbs."),
        Unreg(Ids.BasesBuilderRemoveCondition, "Bases: Remove Condition",
            CommandSection.Bases,
            OverlayReason + " The Windows builder sheet owns the row verbs."),
    ];

    /// <summary>
    /// A verb mac gives a chord and Windows does not. Recorded so the mac
    /// column shows the real chord instead of reading as "mac has none",
    /// which is what hid the New Note and Move To gaps.
    /// </summary>
    private const string UnboundOnWindows =
        "Windows binds no chord: the verb is menu- and palette-only here. "
        + "Recorded so the mac chord stays visible rather than reading as "
        + "an absence.";

    /// <summary>
    /// W6-1 PR C (#745), contract C15: the Where-am-I chord's recorded
    /// divergence. The rule maps ⌃⌘I to Ctrl+Alt+I, which
    /// <c>slate.view.toggleRightPane</c> holds GLOBALLY — a scope that is
    /// live while a canvas is focused — so the two would be a real
    /// collision rather than a disjoint pair. Owner decision D-2 resolves
    /// it with the G18 disambiguation precedent: add Shift.
    /// </summary>
    private const string WhereAmIShiftDisambiguation =
        "D-2: the rule predicts Ctrl+Alt+I, which slate.view.toggleRightPane holds "
        + "at Global scope — live at the same moment as a focused canvas, so this is "
        + "a collision and not a disjoint pair. Disambiguated with Shift per the G18 "
        + "precedent.";

    /// <summary>
    /// W6-1 PR A (#745), contract A18 + PR C, contract C15. The three
    /// surface commands register with no chord — the switcher is a
    /// visible control and the palette is always a path (rule R1) — and
    /// PR C adds the navigator, filter, Where-am-I and mode rows.
    ///
    /// The chorded ones are delivered at <c>ChordScope.Canvas</c> from
    /// <c>CanvasNavigator</c>'s own map, which
    /// <c>ChordTableTests.CanvasChords</c> scrapes in both directions.
    /// Rows without a Windows chord get <c>ChordScope.None</c> through
    /// <c>Reg</c>'s own rule; their mac chord is still recorded, so the
    /// mac column shows the real binding rather than reading as an
    /// absence.
    /// </summary>
    /// <summary>
    /// W6-2 PR A (#746, contract A-12): the graph's one row — chordless,
    /// section Graph, scope None by <c>Reg</c>'s rule (the canvas surface
    /// rows' template). PR B–E add the leaf's, the navigator's and the
    /// diagram's rows; PR C's chorded rows are the first in
    /// <see cref="ChordScope.Graph"/>.
    /// </summary>
    private static IEnumerable<ChordTableEntry> GraphRows() =>
    [
        // The label is the mac's, byte-identical (P3; MacCatalogParityTests).
        Reg(Ids.GraphOpenTab, "Open Graph", CommandSection.Graph,
            "Open the vault's graph in its tab, as a sortable table of notes."),
        // W6-2 PR B (B-14): chordless — ChordScope.None through Reg's rule —
        // the labels and hints the mac's byte for byte
        // (SlateCommands.swift:1516–1535). Show reveals the leaf for the note
        // in view (the palette's meaning; the row action's re-root is B2);
        // Deeper and Shallower are always enabled, a bound a no-op through
        // core's clamp.
        Reg(Ids.GraphShowConnections, "Show Connections", CommandSection.Graph,
            "Open the Connections leaf — the active note's local graph."),
        Reg(Ids.GraphConnectionsDeeper, "Connections: Deeper", CommandSection.Graph,
            "Increase the Connections leaf depth by one (up to 3 links away)."),
        Reg(Ids.GraphConnectionsShallower, "Connections: Shallower", CommandSection.Graph,
            "Decrease the Connections leaf depth by one (down to direct links)."),
    ];

    private static IEnumerable<ChordTableEntry> CanvasRows() =>
    [
        Reg(Ids.CanvasShowOutline, "Canvas: Show Outline", CommandSection.Canvas,
            "Show the canvas as an outline of its cards, groups and connections."),
        Reg(Ids.CanvasShowTable, "Canvas: Show Table", CommandSection.Canvas,
            "Show the canvas as a sortable table of its cards."),
        Reg(Ids.CanvasShowVisual, "Canvas: Show Visual", CommandSection.Canvas,
            "Show the canvas as its spatial board."),

        Reg(Ids.CanvasWhereAmI, "Canvas: Where Am I?", CommandSection.Canvas,
            "Read the selected card's full context: position, group, connections, "
            + "color, and marks. Also rendered in a panel you can read at leisure.",
            "⌃⌘I", "Ctrl+Alt+Shift+I", ChordScope.Canvas,
            divergence: WhereAmIShiftDisambiguation),

        // The movements. The arrows are delivered by the PROJECTION the
        // reader is in — the tree's own Up/Down and the grid's own
        // Up/Down already move the reader and land on the document's one
        // narrating selection mutation — and the navigator answers the
        // boundary they cannot speak to (contract C3). The palette rows
        // are the always-available card-to-card equivalents (rule R1).
        Reg(Ids.CanvasNextCard, "Canvas: Next Card", CommandSection.Canvas,
            "Select the next card in reading order.",
            "↓", "Down", ChordScope.Canvas),
        Chord("windows.canvas.modeStepLargeDown",
            "Canvas mode: large step down",
            "Shift+Down", ChordScope.Canvas,
            "§F TF-3 (FD-3): the large grid step while a move or "
            + "resize transient holds the arrows; without a mode the "
            + "chord is unbound and typing keeps Shift+arrows.",
            mac: "⇧↓"),
        Reg(Ids.CanvasPreviousCard, "Canvas: Previous Card", CommandSection.Canvas,
            "Select the previous card in reading order.",
            "↑", "Up", ChordScope.Canvas),
        Chord("windows.canvas.modeStepLargeUp",
            "Canvas mode: large step up",
            "Shift+Up", ChordScope.Canvas,
            "§F TF-3 (FD-3): the large grid step while a move or "
            + "resize transient holds the arrows; without a mode the "
            + "chord is unbound and typing keeps Shift+arrows.",
            mac: "⇧↑"),
        Reg(Ids.CanvasEnterGroup, "Canvas: Enter Group", CommandSection.Canvas,
            "Move into the selected group's first card."),
        Reg(Ids.CanvasExitGroup, "Canvas: Exit Group", CommandSection.Canvas,
            "Move out to the containing group."),
        Reg(Ids.CanvasFollowConnectionForward, "Canvas: Follow Connection Forward",
            CommandSection.Canvas,
            "Jump along the selected card's first outgoing connection.",
            "→", "Right", ChordScope.Canvas),
        Chord("windows.canvas.modeStepLargeRight",
            "Canvas mode: large step right",
            "Shift+Right", ChordScope.Canvas,
            "§F TF-3 (FD-3): the large grid step while a move or "
            + "resize transient holds the arrows; without a mode the "
            + "chord is unbound and typing keeps Shift+arrows.",
            mac: "⇧→"),
        Reg(Ids.CanvasFollowConnectionBack, "Canvas: Follow Connection Back",
            CommandSection.Canvas,
            "Jump along the selected card's first incoming connection.",
            "←", "Left", ChordScope.Canvas),
        Chord("windows.canvas.modeStepLargeLeft",
            "Canvas mode: large step left",
            "Shift+Left", ChordScope.Canvas,
            "§F TF-3 (FD-3): the large grid step while a move or "
            + "resize transient holds the arrows; without a mode the "
            + "chord is unbound and typing keeps Shift+arrows.",
            mac: "⇧←"),
        Reg(Ids.CanvasTracePath, "Canvas: Trace Path from Selected Card",
            CommandSection.Canvas,
            "Walk the outgoing chain, announcing each hop and the visited count."),

        // The viewport (§D D14, task TD-5). Chorded per mac's mapping
        // (⌘→Ctrl); fit and zoom-to-selection are VISUAL SURFACE ONLY
        // (rule R2) and the navigator's own map gates them there.
        Reg(Ids.CanvasZoomIn, "Canvas: Zoom In", CommandSection.Canvas,
            "Zoom the visual board in one step.",
            "⌘=", "Ctrl+=", ChordScope.Canvas),
        Reg(Ids.CanvasZoomOut, "Canvas: Zoom Out", CommandSection.Canvas,
            "Zoom the visual board out one step.",
            "⌘-", "Ctrl+-", ChordScope.Canvas),
        Reg(Ids.CanvasActualSize, "Canvas: Actual Size", CommandSection.Canvas,
            "Zoom the visual board to 100 percent.",
            "⌘0", "Ctrl+0", ChordScope.Canvas),
        Reg(Ids.CanvasFitCanvas, "Canvas: Fit Canvas", CommandSection.Canvas,
            "Fit every card in the visual board's view.",
            "⇧1", "Shift+1", ChordScope.Canvas),
        Reg(Ids.CanvasZoomToSelection, "Canvas: Zoom to Selection",
            CommandSection.Canvas,
            "Fill the visual board's view with the selected cards.",
            "⇧2", "Shift+2", ChordScope.Canvas),

            // §E TE-11 (E19): the ONE new verb chord - mac's opt-cmd-N
            // maps to Ctrl+Alt+N (Ctrl+N stays free for notes, mac's own
            // allocation rule #368). Every other mutation verb is
            // palette/menu/context-menu only (R1).
            // §F TF-4 (F9/M6): the spatial mode front doors, labels
            // byte-identical to mac (P3). R is mac's quick loop: during
            // resize it commits. The presets are palette rows - mac
            // allocates them no chord and neither do we.
            // §F TF-7 (F5/F6/F9): structural placement — the picker
            // names a reference card, the ENGINE computes the slot.
            // Labels and hints byte-identical to mac (P3); no chords,
            // as mac allocates none.
            // §F TF-8 (F7): the staged connect flow's front door.
            Reg(Ids.CanvasConnectTo, "Canvas: Connect To…", CommandSection.Canvas,
                "Draw a connection from the selected card to a card you pick, "
                + "with an optional label.",
                "⌃⌘C", "Ctrl+Alt+C", ChordScope.Canvas),
            // §G TG-0 (G7): the mark verbs' front doors, labels
            // byte-identical to mac (P3). Ctrl+Alt+M is mac's ⌃⌘M in
            // FD-3's family; Clear All has no chord, as mac allocates it.
            Reg(Ids.CanvasToggleMark, "Canvas: Toggle Mark", CommandSection.Canvas,
                "Mark or unmark the selected card. Bulk actions apply to every "
                + "marked card at once.",
                "⌃⌘M", "Ctrl+Alt+M", ChordScope.Canvas),
            Reg(Ids.CanvasShowMarks, "Canvas: Show Marked Cards", CommandSection.Canvas,
                "Open the marks list: jump to or unmark any marked card."),
            Reg(Ids.CanvasGroupMarked, "Canvas: Group Marked Cards…", CommandSection.Canvas,
                "Wrap the marked cards in a new labeled group - one action, one undo."),
            Reg(Ids.CanvasDeleteMarked, "Canvas: Delete Marked Cards", CommandSection.Canvas,
                "Delete every marked card and its connections - one summary, one undo."),
            // §G TG-4 (GD-5): mac's verb has no front door; this row is
            // Windows' — a recorded divergence assigned to the mac lane.
            Reg(Ids.CanvasColorMarked, "Canvas: Color Marked Cards…", CommandSection.Canvas,
                "Set every marked card's color at once - one action, one undo."),
            Reg(Ids.CanvasClearMarks, "Canvas: Clear All Marks", CommandSection.Canvas,
                "Unmark every marked card."),
            // §G2 TG2-0 (G2-1): the front doors over §E's shipped verbs —
            // labels byte-identical to mac (P3), hints mac's with the
            // undo chord spelled for Windows. No chords: mac allocates
            // none (R1 — a chord is a convenience, never the only path).
            Reg(Ids.CanvasDelete, "Canvas: Delete Selection", CommandSection.Canvas,
                "Delete the selected card, or ungroup the selected group keeping its "
                + "cards. Undo with Ctrl+Z."),
            Reg(Ids.CanvasEditCard, "Canvas: Edit Card Text…", CommandSection.Canvas,
                "Open the selected text card in the editor. Escape saves and returns."),
            Reg(Ids.CanvasRenameGroup, "Canvas: Rename Group…", CommandSection.Canvas,
                "Rename the selected group — group labels are the skeleton of the "
                + "reading order."),
            Reg(Ids.CanvasSetColor, "Canvas: Set Color…", CommandSection.Canvas,
                "Color the selected card by name: red, orange, yellow, green, cyan, "
                + "or purple."),
            Reg(Ids.CanvasClearColor, "Canvas: Clear Color", CommandSection.Canvas,
                "Remove the selected card's color."),
            // §G2 TG2-1: the two text prompts' front doors.
            Reg(Ids.CanvasNewGroup, "Canvas: New Group…", CommandSection.Canvas,
                "Create a labeled group next to the selection."),
            Reg(Ids.CanvasAddLink, "Canvas: Add Link Card…", CommandSection.Canvas,
                "Paste or type a URL; a link card is placed next to your selection."),
            // §G2 TG2-2: the choices pickers' front doors.
            Reg(Ids.CanvasMoveIntoGroup, "Canvas: Move into Group…", CommandSection.Canvas,
                "Move the selected card inside a group by name — no dragging or "
                + "coordinates."),
            Reg(Ids.CanvasEditConnection, "Canvas: Edit Connection…", CommandSection.Canvas,
                "Change one of the selected card's connection labels or direction."),
            Reg(Ids.CanvasDeleteConnection, "Canvas: Delete Connection…", CommandSection.Canvas,
                "Delete one of the selected card's connections. Undo with Ctrl+Z."),
            // §G2 TG2-3: the vault file picker's front doors.
            Reg(Ids.CanvasAddNote, "Canvas: Add Note to Canvas…", CommandSection.Canvas,
                "Pick a vault note; a file card is placed next to your selection."),
            Reg(Ids.CanvasAddMedia, "Canvas: Add Media…", CommandSection.Canvas,
                "Pick a vault media file; a file card is placed next to your selection."),
            Reg(Ids.CanvasLocateFile, "Canvas: Locate File…", CommandSection.Canvas,
                "Repoint the selected file card at a different vault file."),
            // §G2 TG2-4: Remove from Group and the connected-card pair. The
            // series' second verb chord: mac's ⌃⌥⌘N as Ctrl+Alt+Shift+N (the
            // spec's §7 allocation, free on Windows), delivered by the
            // navigator's map.
            Reg(Ids.CanvasRemoveFromGroup, "Canvas: Remove from Group", CommandSection.Canvas,
                "Move the selected card out of its enclosing group, placed by the engine."),
            Reg(Ids.CanvasCreateConnectedCard, "Canvas: Create Connected Card",
                CommandSection.Canvas,
                "New text card below the selection, already connected, ready to type.",
                "⌃⌥⌘N", "Ctrl+Alt+Shift+N", ChordScope.Canvas,
                divergence: "mac's ⌃⌥⌘N; the modifier rule maps it onto Ctrl+Alt+N, "
                    + "which New Card owns (mac's ⌥⌘N), so Shift disambiguates — the "
                    + "spec's §7 allocation, free on Windows."),
            Reg(Ids.CanvasCreateConnectedCardDirectional,
                "Canvas: Create Connected Card (Choose Direction)…", CommandSection.Canvas,
                "Pick the side first, then the connected card is created there."),
            // §G2 TG2-5: Duplicate — the selection or the marked set, one action.
            Reg(Ids.CanvasDuplicate, "Canvas: Duplicate", CommandSection.Canvas,
                "Duplicate the selected card or the marked set — one action, one undo."),
            // §G2 TG2-6: Convert — mac registers it structural; here the
            // structural half is the sidebar's seam behind the verb.
            Reg(Ids.CanvasConvertToNote, "Canvas: Convert Card to Note…", CommandSection.Canvas,
                "Create a vault note from the text card; the card then points at it."),
            Reg(Ids.CanvasConnectMode, "Canvas: Connect Mode", CommandSection.Canvas,
                "Remember the selected card, navigate to a target with the "
                + "usual movements, press Return to connect."),
            Reg(Ids.CanvasPlaceBelow, "Canvas: Place Below…", CommandSection.Canvas,
                "Move the selected card (or the marked set) just below a card you pick."),
            Reg(Ids.CanvasPlaceRightOf, "Canvas: Place Right Of…", CommandSection.Canvas,
                "Move the selection just right of a card you pick."),
            Reg(Ids.CanvasPlaceAbove, "Canvas: Place Above…", CommandSection.Canvas,
                "Move the selection just above a card you pick."),
            Reg(Ids.CanvasPlaceLeftOf, "Canvas: Place Left Of…", CommandSection.Canvas,
                "Move the selection just left of a card you pick."),
            Reg(Ids.CanvasAlignWith, "Canvas: Align With…", CommandSection.Canvas,
                "Align the selected card's top edge with a card you pick. Overlaps "
                + "are refused, never silent."),
            Reg(Ids.CanvasMoveMode, "Canvas: Move Mode", CommandSection.Canvas,
                "Grab the selection. Arrows nudge on the grid, Shift for big "
                + "steps, Return places, Escape cancels.",
                "⌃⌘G", "Ctrl+Alt+G", ChordScope.Canvas),
            Reg(Ids.CanvasResizeMode, "Canvas: Resize Mode", CommandSection.Canvas,
                "Resize the selected card. Left and Right change width, Up and "
                + "Down change height.",
                "⌃⌘R", "Ctrl+Alt+R", ChordScope.Canvas),
            Reg(Ids.CanvasResizeDefaultSize, "Canvas: Resize to Default Size",
                CommandSection.Canvas,
                "Set the card being resized back to the default card size."),
            Reg(Ids.CanvasResizeFitContent, "Canvas: Resize to Fit Content",
                CommandSection.Canvas,
                "Size the card being resized to its text."),
            Reg(Ids.CanvasNewCard, "Canvas: New Card", CommandSection.Canvas,
                "Create a text card next to the selection - placement is "
                + "automatic and announced.",
                "⌥⌘N", "Ctrl+Alt+N", ChordScope.Canvas),
        Reg(Ids.CanvasFilterCards, "Canvas: Filter Cards…", CommandSection.Canvas,
            "Focus the filter field (Ctrl+F on a canvas): narrows by title, type, "
            + "group, or target.",
            "⌘F", "Ctrl+F", ChordScope.Canvas),
        Reg(Ids.CanvasClearFilter, "Canvas: Clear Filter", CommandSection.Canvas,
            "Show every card again (also the Escape rung while filtering)."),

        // Return and Escape carry no mac GLYPH: mac's palette rows
        // declare no hotkey and its container handles the two keys
        // directly, and the mac column's per-character glyph walk has no
        // spelling for a named key. The Windows delivery site is the
        // canvas surface's own handler, which is what ChordScope.Canvas
        // records.
        Reg(Ids.CanvasCommitMode, "Canvas: Commit Mode", CommandSection.Canvas,
            "Apply the active move or resize (same as Enter).",
            win: "Enter", scope: ChordScope.Canvas),
        Reg(Ids.CanvasCancelMode, "Canvas: Cancel Mode", CommandSection.Canvas,
            "Cancel the active move or resize and restore the prior position "
            + "(same as Escape).",
            win: "Escape", scope: ChordScope.Canvas),

        Reg(Ids.CanvasToggleFollowSelection, "Canvas: Toggle Viewport Follows Selection",
            CommandSection.Canvas,
            "When on (the default), the visual view pans to keep the selection visible."),

        // The verbosity values (t0 §1.2), one row each. Unregistered for
        // the math-verbosity reason (PR-4): the menu supplies the value
        // as a CommandParameter, which a palette row has no way to
        // provide — a bare "Set Canvas Verbosity" command could not know
        // WHICH level. Verified against mac: SlateCommands.swift declares
        // no id for canvas verbosity either, so there is no twin to be
        // out of parity with.
        Unreg("windows.canvas.setVerbosityTerse", "Canvas Verbosity: Terse",
            CommandSection.Canvas, CanvasVerbosityReason),
        Unreg("windows.canvas.setVerbosityStandard", "Canvas Verbosity: Standard",
            CommandSection.Canvas, CanvasVerbosityReason),
        Unreg("windows.canvas.setVerbosityVerbose", "Canvas Verbosity: Verbose",
            CommandSection.Canvas, CanvasVerbosityReason),
    ];

    private const string CanvasVerbosityReason =
        "PR-4, the math-verbosity precedent: the three canvas verbosity levels are "
        + "selected by one parameterized setter the Canvas menu binds with the level "
        + "as its CommandParameter. Each level is a row so the catalog records the "
        + "closed set, and none is registered because a palette row cannot supply "
        + "the parameter. mac declares no SlateCommandID for these either.";

    private static IEnumerable<ChordTableEntry> SidebarRows()
    {
        var rows = new List<ChordTableEntry>
        {
            Reg(Ids.SidebarOpen, "Open", CommandSection.Sidebar,
                "Open the selected files."),
            Reg(Ids.NewNote, "New Note", CommandSection.Sidebar,
                "Create an untitled note in the selected location, then rename it.",
                "⌘N", divergence: UnboundOnWindows),
            Reg(Ids.NewFolder, "New Folder", CommandSection.Sidebar,
                "Create a new folder in the selected location, then rename it."),
            // W5-3 (#743): a clean ⌘→Ctrl mapping — Ctrl+Shift+N is
            // unclaimed here and by Windows convention in-app. Recorded
            // beside the deliberately chord-UNBOUND NewNote: the
            // asymmetry is mac's own (⌘N exists but Windows declined it
            // for the inline-rename flow; the template flow has no such
            // conflict). Contracts doc TD-6.
            Reg(Ids.NewFromTemplate, "New Note from Template…", CommandSection.Sidebar,
                "Choose a template for a new note.", "⇧⌘N", "Ctrl+Shift+N"),
            // §E TE-10 (IE-21): New Canvas is FILE-level on both hosts
            // (mac registers section .file, chordless) — a vault-scoped
            // create, deliberately outside the canvas section whose
            // verbs need an open document.
            Reg(Ids.NewCanvas, "New Canvas", CommandSection.File,
                "Create an empty canvas file in the vault and open it."),
            Reg(Ids.ImportFilesAndFolders, "Import Files and Folders…",
                CommandSection.Sidebar,
                "Choose files and folders. External items are copied into the selected "
                + "location; items already in this vault are moved."),
            Reg(Ids.CancelImport, "Cancel Import", CommandSection.Sidebar,
                "Stops remaining imports. Completed copies remain in the vault.",
                "⌘.", "Escape",
                divergence:
                "The rule maps ⌘. to Ctrl+. ; Windows cancels with Escape. Escape is "
                + "the Windows cancellation convention and stays scoped to the running "
                + "import (PR-2: overlay dismissal keeps precedence outside import)."),
            Reg(Ids.RenameEntry, "Rename…", CommandSection.Sidebar,
                "Rename the selected file or folder in place.", "⌥⌘R", "F2",
                divergence:
                "The rule maps ⌥⌘R to Ctrl+Alt+R; Windows uses F2, the platform rename "
                + "convention shared by Explorer, list views, and grids (decision 12: "
                + "platform convention governs input)."),
            Reg(Ids.MoveTo, "Move To…", CommandSection.Sidebar,
                "Move the selected files or folders to another folder.",
                "⇧⌘M", divergence: UnboundOnWindows),
            // The LABEL stays mac-byte-identical (the P3 census
            // admits no divergence for a shared id; FD-3's Recycle
            // Bin rule governs sentences and Windows-authored text —
            // the register records the carve-out). The HINT is
            // Windows-authored, so it names the real destination.
            Reg(Ids.DeleteEntry, "Move to Trash", CommandSection.Sidebar,
                "Move the selected files or folders to the Recycle Bin."),
            // W5-4 (F5/F7/F8): the three verbs mac's sidebar catalog
            // carries and W4-4's census dispositioned as gaps.
            Reg(Ids.DuplicateEntry, "Duplicate", CommandSection.Sidebar,
                "Duplicate the selected file as a copy next to it."),
            Reg(Ids.CopyPath, "Copy Path", CommandSection.Sidebar,
                "Copy the selected item's path."),
            Reg(Ids.RevealInFinder, "Reveal in File Explorer", CommandSection.Sidebar,
                "Show the selected file or folder in File Explorer.",
                labelDivergence:
                "FD-5: mac's label is \"Reveal in Finder\" — Finder does not "
                + "exist on Windows, and the platform surface it names does. "
                + "The id stays shared (the capability is the same verb); "
                + "the label follows the OS. Recorded, never relabeled."),
            Reg(Ids.CopyWikilink, "Copy Wikilink", CommandSection.Sidebar,
                "Copy a wikilink to the selected Markdown file."),
            Reg(Ids.PinNote, "Pin to Top of Folder", CommandSection.Sidebar,
                "Pin the selected note to the top of its folder."),
            Reg(Ids.UnpinNote, "Unpin", CommandSection.Sidebar,
                "Remove the selected note from its folder's pinned section."),
            Reg(Ids.UnpinAllInFolder, "Unpin All in Folder", CommandSection.Sidebar,
                "Remove every pinned note from the selected folder."),
            Reg(Ids.UseVaultDefaultSort, "Use Vault Default Sort", CommandSection.Sidebar,
                "Remove the selected folder's sort override."),
            Reg(Ids.AddShortcut, "Add to Shortcuts", CommandSection.Sidebar,
                "Add the selected file or folder to the Shortcuts section."),
            Reg(Ids.RemoveShortcut, "Remove from Shortcuts", CommandSection.Sidebar,
                "Remove the selected file or folder from the Shortcuts section."),
            Reg(Ids.ClearRecents, "Clear Recents", CommandSection.Sidebar,
                "Clear the shared recent-files history for this vault."),
            Reg(Ids.CollapseAll, "Collapse All Folders", CommandSection.Sidebar,
                "Collapse every folder except the current selection's ancestors."),
            Reg(Ids.ExpandLoaded, "Expand Loaded Folders", CommandSection.Sidebar,
                "Expand already-loaded folders, fetching at most one level deeper."),
            Reg(Ids.HistoryBack, "Back in Sidebar History", CommandSection.Sidebar,
                "Select the previous sidebar selection from this window's history.",
                "⌃⌘[", "Ctrl+Alt+["),
            Reg(Ids.HistoryForward, "Forward in Sidebar History", CommandSection.Sidebar,
                "Select the next sidebar selection from this window's history.",
                "⌃⌘]", "Ctrl+Alt+]"),
        };

        for (int slot = 1; slot <= 9; slot++)
        {
            rows.Add(Reg(
                Ids.OpenShortcut(slot),
                $"Open Shortcut {slot}",
                CommandSection.Sidebar,
                $"Activate shortcut {slot} in the Shortcuts section.",
                $"⌃{slot}",
                $"Ctrl+{slot}",
                divergence:
                $"The rule maps ⌃ to Alt, so it predicts Alt+{slot}. Alt+digit is the "
                + "Windows menu-mnemonic space — Alt alone activates the menu bar — "
                + "while Ctrl+digit is the platform's positional-activation convention "
                + "(browser tabs, Explorer). Shipped in W1-2; recorded, not rebound."));
        }

        rows.AddRange(
        [
            Unreg(Ids.FocusFilter, "Focus Sidebar Filter", CommandSection.Sidebar,
                "No command object: the chord and the Files ▸ Focus Filter menu item both "
                + "route through MainWindow code-behind (FocusFilter_Click), which owns the "
                + "TextBox. Registering it needs a focus seam on the shell — integration "
                + "scope, not the command layer's.",
                "⌥⌘F", "Ctrl+Alt+F", ChordScope.Global),
            Reg(Ids.ToggleLayout, "Toggle Sidebar Layout", CommandSection.Sidebar,
                "Switch the sidebar between the tree and dual-pane layouts."),
            Reg(Ids.AddTag, "Add Tag…", CommandSection.Sidebar,
                "Add a tag to the selected files' frontmatter."),
            Reg(Ids.RemoveTag, "Remove Tag…", CommandSection.Sidebar,
                "Remove a tag from the selected files' frontmatter."),
            Reg(Ids.CreateFolderNote, "Create Folder Note", CommandSection.Sidebar,
                "Create and open this folder's note."),
            Reg(Ids.DeleteFolderNote, "Delete Folder Note", CommandSection.Sidebar,
                "Move this folder's note to the Recycle Bin."),

            // Windows-only sidebar capabilities. Refresh and Clear Filter are
            // menu/UI-homed; Toggle Tags is a PR-4 orphan with no binding at
            // all, registered so the palette becomes its surface.
            Reg(Ids.SidebarRefresh, "Refresh File Tree", CommandSection.Sidebar,
                "Re-read the vault's file tree and report the visible count."),
            Reg(Ids.SidebarClearFilter, "Clear Sidebar Filter", CommandSection.Sidebar,
                "Clear the sidebar filter field and show every file again."),
            Reg(Ids.SidebarToggleTags, "Toggle Tag Column", CommandSection.Sidebar,
                "Show or hide each file's tags in the sidebar tree."),
            // W5-4 F6: targeting unified — slate.file.delete now acts
            // on the tree selection (mac parity), so this Windows-only
            // id names the batch-checkbox flow it previously mirrored.
            // Windows-only id, so FD-3's Recycle Bin rule applies to
            // the LABEL too (red team, contracts 4a).
            Reg(Ids.SidebarTrashSelected, "Move Checked Items to the Recycle Bin",
                CommandSection.Sidebar,
                "Move every batch-checked item to the Recycle Bin."),
        ]);

        // Mac's sort family. Windows ships the capability, but through a
        // bound ComboBox and CheckBox (FilesSidebarViewModel.SortMode /
        // GroupByDate, MainWindow.xaml:510,524) rather than command objects,
        // so there is nothing to register and no chord to record. Declared
        // here so the ids are accounted for rather than silently missing.
        const string boundControlReason =
            "Windows delivers this through the sidebar's sort ComboBox / Group by date "
            + "checkbox, not a command object. Adding a command is new capability, "
            + "beyond W5-1's registration bridge.";
        rows.AddRange(
        [
            Unreg("slate.sidebar.sortNameAsc", "Sort by Name (A to Z)",
                CommandSection.Sidebar, boundControlReason),
            Unreg("slate.sidebar.sortNameDesc", "Sort by Name (Z to A)",
                CommandSection.Sidebar, boundControlReason),
            Unreg("slate.sidebar.sortCreatedDesc", "Sort by Created (Newest First)",
                CommandSection.Sidebar, boundControlReason),
            Unreg("slate.sidebar.sortCreatedAsc", "Sort by Created (Oldest First)",
                CommandSection.Sidebar, boundControlReason),
            Unreg("slate.sidebar.sortModifiedDesc", "Sort by Modified (Newest First)",
                CommandSection.Sidebar, boundControlReason),
            Unreg("slate.sidebar.sortModifiedAsc", "Sort by Modified (Oldest First)",
                CommandSection.Sidebar, boundControlReason),
            Unreg("slate.sidebar.toggleDateGrouping", "Group by Date",
                CommandSection.Sidebar, boundControlReason),
            Unreg("slate.sidebar.openFolderNote", "Open Folder Note",
                CommandSection.Sidebar,
                "Not shipped on Windows: the sidebar creates and deletes folder notes but "
                + "has no separate open verb — selecting the note opens it. Recorded so "
                + "the id is accounted for, not silently missing."),
        ]);

        return rows;
    }

    /// <summary>
    /// Chords the app delivers that no registered command backs. These are
    /// surface interactions in the PR-4 sense — they exist only while their
    /// surface has focus, which is why several of them legitimately re-use a
    /// chord string that a global command also uses.
    /// </summary>
    private static IEnumerable<ChordTableEntry> SurfaceChordRows()
    {
        const string readingReason =
            "PR-3: structural navigation delivered by Reading/ReadingNavigator.cs while "
            + "the reading surface has focus. No mac twin (mac uses VoiceOver rotor "
            + "navigation); no command object.";
        const string quickOpenReason =
            OverlayReason + " Delivered by the Quick Open overlay's key handler "
            + "(MainWindow.xaml.cs:543-555).";

        var rows = new List<ChordTableEntry>();

        (string Key, string Label)[] landmarks =
        [
            ("H", "heading"),
            ("K", "link"),
            ("L", "list"),
            ("U", "list (alias for L)"),
            ("T", "table"),
            ("E", "embedded object"),
            ("C", "code block"),
            ("M", "math block"),
            ("D", "diagram"),
            ("G", "diagram (alias for D)"),
        ];
        foreach ((string key, string label) in landmarks)
        {
            rows.Add(Chord(
                $"windows.reading.next{key}",
                $"Reading: next {label}",
                $"Ctrl+Alt+{key}",
                ChordScope.Reading,
                readingReason));
            rows.Add(Chord(
                $"windows.reading.previous{key}",
                $"Reading: previous {label}",
                $"Ctrl+Alt+Shift+{key}",
                ChordScope.Reading,
                readingReason));
        }

        for (int level = 1; level <= 6; level++)
        {
            rows.Add(Chord(
                $"windows.reading.nextHeadingLevel{level}",
                $"Reading: next level-{level} heading",
                $"Ctrl+Alt+{level}",
                ChordScope.Reading,
                readingReason));
            rows.Add(Chord(
                $"windows.reading.previousHeadingLevel{level}",
                $"Reading: previous level-{level} heading",
                $"Ctrl+Alt+Shift+{level}",
                ChordScope.Reading,
                readingReason));
        }

        const string PropertyRowReason =
            "A property-row editor interaction, live only while a numeric row's "
            + "TextBox has focus (WorkspaceTemplates.xaml). Delivered by a "
            + "KeyBinding inside the row DataTemplate.";
        const string SplitterReason =
            "A pane-splitter interaction, live only while a splitter thumb has "
            + "focus (WeightedSplitPanel). Resizes by 5% per press.";

        const string PaletteReason =
            "W5-1: a command-palette overlay interaction, live only while the "
            + "palette is open. Delivered imperatively from "
            + "MainWindow.HandleCommandPaletteKey, not a KeyBinding.";
        const string SearchOverlayInteractionReason =
            "W5-2: a vault-search overlay interaction, live only while the "
            + "overlay is open. Delivered imperatively from "
            + "MainWindow.HandleSearchOverlayKey, not a KeyBinding.";
        const string SearchOverlayArrowDivergence =
            "SD-1: Windows navigates results with the arrow keys where mac has "
            + "no arrow handling at all; mac converges under #1113.";
        const string PaletteKeysDivergence =
            "PD-1: Windows navigates with the Home/End/Page keys where mac "
            + "deliberately handles none of them; mac converges under #1105.";

        rows.AddRange(
        [
            Chord("windows.quickOpen.openCurrentTab", "Quick Open: open in the current tab",
                "Enter", ChordScope.QuickOpen, quickOpenReason),
            Chord("windows.quickOpen.openNewTab", "Quick Open: open in a new tab",
                "Ctrl+Enter", ChordScope.QuickOpen, quickOpenReason),
            Chord("windows.quickOpen.openSplitRight", "Quick Open: open in a right split",
                "Ctrl+Alt+Enter", ChordScope.QuickOpen, quickOpenReason),
            Chord("windows.quickOpen.openSplitDown", "Quick Open: open in a down split",
                "Ctrl+Alt+Shift+Enter", ChordScope.QuickOpen, quickOpenReason),
            Chord("windows.quickOpen.moveNext", "Quick Open: next result",
                "Down", ChordScope.QuickOpen, quickOpenReason),
            Chord("windows.quickOpen.movePrevious", "Quick Open: previous result",
                "Up", ChordScope.QuickOpen, quickOpenReason),
            Chord("windows.quickOpen.dismiss", "Quick Open: dismiss",
                "Escape", ChordScope.QuickOpen,
                quickOpenReason + " PR-2 records its place in the Escape chain."),

            // §E TE-11 (E19/ED-1, D-6 adopted): the canvas history
            // domain. Chord-only rows like the structural pair below: mac
            // routes cmd-Z through the responder chain and the
            // undoTargetsCanvas gate, not the registry, so on both
            // platforms these are surface interactions, not commands. No
            // Ctrl+Shift+Z alias - the editor host registers none (ED-1).
            Chord("windows.canvas.undoHistory",
                "Canvas: undo the last canvas action",
                "Ctrl+Z", ChordScope.Canvas,
                "§E ED-1: canvas-focus-scoped delivery through the "
                + "navigator ladder; the two-phase checkout keeps a raced "
                + "mutation from losing or doubling the entry.",
                mac: "⌘Z"),
            Chord("windows.canvas.redoHistory",
                "Canvas: redo the last undone canvas action",
                "Ctrl+Y", ChordScope.Canvas,
                "§E ED-1: the redo twin. mac uses shift-cmd-Z; Windows "
                + "keeps the structural pair's Ctrl+Y convention.",
                mac: "⇧⌘Z",
                divergence: "mac uses ⇧⌘Z; Windows keeps the structural "
                + "pair's Ctrl+Y convention, and ED-1 registers no "
                + "Ctrl+Shift+Z alias because the editor host has none."),

            // W5-4 (F10/FD-1): the structural undo domain. Delivered
            // imperatively from Window_PreviewKeyDown, TREE-SCOPED —
            // the editor owns Ctrl+Z/Ctrl+Y everywhere else (mac's
            // undoTargetsStructural focus gate; mac routes ⌘Z through
            // the responder chain, not the registry, so these are
            // chord-only surface interactions on both platforms).
            Chord("windows.sidebar.undoStructural",
                "Files tree: undo the last structural operation",
                "Ctrl+Z", ChordScope.Global,
                "W5-4 (F10): tree-scoped structural undo — renames and "
                + "moves re-run their inverse through the forward FFI; "
                + "batch moves ride UndoBatchMove. Live only while the "
                + "files tree owns focus; the editor keeps Ctrl+Z "
                + "everywhere else (FD-1).",
                mac: "⌘Z"),
            Chord("windows.sidebar.redoStructural",
                "Files tree: redo the last undone structural operation",
                "Ctrl+Y", ChordScope.Global,
                "W5-4 (F10): tree-scoped structural redo. mac uses ⇧⌘Z; "
                + "Ctrl+Y is the Windows redo convention (decision 12).",
                mac: "⇧⌘Z",
                divergence:
                "The rule maps ⇧⌘Z to Ctrl+Shift+Z, but Ctrl+Y is the "
                + "platform redo convention (Notepad, Office, Explorer's "
                + "rename field). Recorded as the deliberate Windows "
                + "binding — W5-4 decision 12."),
            Chord("windows.sidebar.deleteSelected",
                "Files tree: move the selection to the Recycle Bin",
                "Delete", ChordScope.Global,
                "W5-4 (F6, FD-4): tree-scoped Delete riding the same "
                + "imperative MainWindow delivery as F2-rename — the "
                + "selection verb, with Finder-parity confirmation "
                + "staging. mac scopes ⌘⌫ to its file tree identically.",
                mac: "⌘⌫",
                divergence:
                "The rule maps ⌘⌫ to Ctrl+Backspace, but the bare Delete "
                + "key is the platform deletion convention (Explorer, "
                + "list views). Recorded as the deliberate Windows "
                + "binding — W5-4 decision 12 / FD-4."),
            Chord("windows.view.showCommandPalette", "Show the command palette",
                "Ctrl+Shift+P", ChordScope.Global,
                "W5-1 (PD-2): the palette's own opening chord, delivered "
                + "imperatively from MainWindow.Window_PreviewKeyDown rather than a "
                + "KeyBinding because the palette exposes methods, not ICommands "
                + "(PR-4). Non-toggling. mac homes ⇧⌘P in a menu CommandGroup; "
                + "Windows has no menu item for it yet, so it emits no UIA "
                + "AcceleratorKey — same class as the PR-5 chords.",
                mac: "⇧⌘P"),
            Chord("windows.palette.invoke", "Command palette: run the selected command",
                "Enter", ChordScope.Palette, PaletteReason),
            Chord("windows.palette.dismiss", "Command palette: dismiss",
                "Escape", ChordScope.Palette,
                PaletteReason + " PR-2 records its place in the Escape chain."),
            Chord("windows.palette.moveNext", "Command palette: next result",
                "Down", ChordScope.Palette, PaletteReason),
            Chord("windows.palette.movePrevious", "Command palette: previous result",
                "Up", ChordScope.Palette, PaletteReason),
            Chord("windows.palette.moveFirst", "Command palette: first result",
                "Home", ChordScope.Palette, PaletteReason + " " + PaletteKeysDivergence),
            Chord("windows.palette.moveLast", "Command palette: last result",
                "End", ChordScope.Palette, PaletteReason + " " + PaletteKeysDivergence),
            Chord("windows.palette.pageDown", "Command palette: next page of results",
                "PageDown", ChordScope.Palette, PaletteReason + " " + PaletteKeysDivergence),
            Chord("windows.palette.pageUp", "Command palette: previous page of results",
                "PageUp", ChordScope.Palette, PaletteReason + " " + PaletteKeysDivergence),

            Chord("windows.searchOverlay.activate", "Search overlay: open the selected result",
                "Enter", ChordScope.SearchOverlay, SearchOverlayInteractionReason),
            Chord("windows.searchOverlay.dismiss", "Search overlay: dismiss",
                "Escape", ChordScope.SearchOverlay,
                SearchOverlayInteractionReason
                + " PR-2 records its place in the Escape chain."),
            Chord("windows.searchOverlay.moveNext", "Search overlay: next result",
                "Down", ChordScope.SearchOverlay,
                SearchOverlayInteractionReason + " " + SearchOverlayArrowDivergence),
            Chord("windows.searchOverlay.movePrevious", "Search overlay: previous result",
                "Up", ChordScope.SearchOverlay,
                SearchOverlayInteractionReason + " " + SearchOverlayArrowDivergence),

            Chord("windows.propertyRow.stepUp", "Property row: increment a numeric value",
                "Up", ChordScope.PropertyRow, PropertyRowReason),
            Chord("windows.propertyRow.stepDown", "Property row: decrement a numeric value",
                "Down", ChordScope.PropertyRow, PropertyRowReason),
            Chord("windows.splitter.growLeading", "Pane splitter: grow the leading pane",
                "Left", ChordScope.Splitter, SplitterReason),
            Chord("windows.splitter.growTrailing", "Pane splitter: grow the trailing pane",
                "Right", ChordScope.Splitter, SplitterReason),
            Chord("windows.splitter.growAbove", "Pane splitter: grow the pane above",
                "Up", ChordScope.Splitter, SplitterReason),
            Chord("windows.splitter.growBelow", "Pane splitter: grow the pane below",
                "Down", ChordScope.Splitter, SplitterReason),

            Chord("windows.editor.closePopover", "Editor: close the interaction popover",
                "Escape", ChordScope.Editor,
                OverlayReason + " Bound at window level (MainWindow.xaml:55) but gated on "
                + "an open popover, so it is popover-scoped in effect. PR-2 records its "
                + "place in the Escape chain."),

            Chord("windows.propertyRow.commit", "Property row: commit the draft",
                "Enter", ChordScope.PropertyRow,
                "PR-3: delivered by the property-row TextBox templates "
                + "(WorkspaceTemplates.xaml:77,112,149,183)."),
            Chord("windows.propertyRow.revert", "Property row: revert the draft",
                "Escape", ChordScope.PropertyRow,
                "PR-3: delivered by the property-row TextBox templates. PR-2 records its "
                + "place in the Escape chain."),
            Chord("windows.propertyRow.delete", "Property row: delete the property",
                "Ctrl+Backspace", ChordScope.PropertyRow,
                "PR-3: delivered by the property-row TextBox templates. Neither "
                + "WorkspaceTemplates.xaml's comment nor w_c_matrix.md's claim that this "
                + "lives in chords.json was true before W5-1.",
                mac: "⌘⌫"),

            // The grid substrate's two chord-bearing RoutedCommands. The
            // registered app-level capabilities are slate.bases.sortByColumn
            // and slate.bases.quickFilter; these rows record the substrate
            // gestures that deliver them, so neither gesture is orphaned.
            Chord("windows.grid.toggleSort", "Grid: toggle the column sort",
                "Ctrl+Alt+S", ChordScope.Grid,
                "PR-3: AccessibleDataGrid.ToggleSortCommand's KeyGesture "
                + "(Grids/AccessibleDataGrid.cs:82)."),
            Chord("windows.grid.filter", "Grid: focus the filter",
                "Ctrl+F", ChordScope.Grid,
                "PR-3: AccessibleDataGrid.FilterCommand's KeyGesture "
                + "(Grids/AccessibleDataGrid.cs:90)."),
        ]);

        return rows;
    }
}
