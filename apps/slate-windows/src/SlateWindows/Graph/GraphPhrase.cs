// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace SlateWindows.Graph;

/// <summary>
/// W6-2 PR C (#746), contract C-13: the graph surface's label inventory
/// byte for byte — the mac's strings (<c>GraphTableView.swift</c>,
/// <c>SlateCommands.swift</c>), the canvas's mac strings REUSED, and the
/// Windows-authored additions named — the <see cref="ConnectionsPhrase"/>
/// shape. Labels, never announced: every spoken line is core's through the
/// relay, and the three verbosity titles are core's vector, never typed.
/// </summary>
internal static class GraphPhrase
{
    // --- The mac's ------------------------------------------------------------

    /// <summary>The filter field's Name (C-5; the mac's label).</summary>
    public const string FilterFieldName = "Filter graph by note name";

    /// <summary>The filter field's HelpText (C-5; the mac's hint).</summary>
    public const string FilterFieldHint = "Filter notes";

    /// <summary>The three presets' labels and hints (C-3; `SlateCommands.swift`).</summary>
    public const string OrphansLabel = "Graph: Orphaned Notes";
    public const string OrphansHint = "Open the graph filtered to orphans — notes with no links in or out.";
    public const string UnresolvedLabel = "Graph: Unresolved Links";
    public const string UnresolvedHint = "Open the graph filtered to unresolved targets — broken links.";
    public const string MostLinkedLabel = "Graph: Most Linked Notes";
    public const string MostLinkedHint = "Open the graph sorted by links in — the most-linked notes, the hubs.";

    /// <summary>Where-am-I's label and hint (C-8; `SlateCommands.swift:1613–1618`).</summary>
    public const string WhereAmILabel = "Graph: Where Am I?";
    public const string WhereAmIHint = "Read the selected node's row copy, its component, the zoom level, and the active filters.";

    // --- The canvas's mac strings, reused --------------------------------------

    /// <summary>The panel's heading and the readback's Name (C-8).</summary>
    public const string WhereAmIHeading = "Where am I?";

    /// <summary>The panel's Close (C-8).</summary>
    public const string WhereAmICloseLabel = "Close";

    /// <summary>Clear's visible content and its Name (C-5).</summary>
    public const string ClearLabel = "Clear";
    public const string ClearFilterName = "Clear filter";

    // --- Windows-authored ------------------------------------------------------

    /// <summary>The count region's Name prefix (C-5; the canvas's C14 precedent).</summary>
    public const string FilterSummaryPrefix = "Filter results: ";

    /// <summary>The Graph menu's and its Verbosity submenu's headers (C-12; menu chrome).</summary>
    public const string GraphMenuHeader = "Graph";
    public const string VerbosityMenuHeader = "Verbosity";

    /// <summary>A-4's state names — the state host's accessible names (C-17).</summary>
    public const string LoadingText = "Loading graph…";
    public const string LoadingAccessibleName = "Loading graph.";
    public const string EmptyText = "No notes match the current filters.";
    public const string ErrorAccessiblePrefix = "Graph error: ";

    // --- W6-2 PR D (the diagram; contract D-12) ---------------------------------

    /// <summary>The diagram state host's text and accessible name while a
    /// layout builds — the mac's T20 (`GraphTableView.swift:138`, `:142`).</summary>
    public const string LoadingDiagramText = "Laying out graph…";
    public const string LoadingDiagramAccessibleName = "Laying out graph.";

    /// <summary>The diagram state host's accessible-name prefix over a failed
    /// build's message — the mac's T19 (`GraphTableView.swift:134`).</summary>
    public const string DiagramErrorPrefix = "Graph diagram error: ";

    /// <summary>T61: the renderer's container peer name (the mac's).</summary>
    public const string DiagramName = "Graph, visual diagram";

    /// <summary>T62/T63: the tier-B summary's action name (the mac's).</summary>
    public const string SwitchToTable = "Switch to Table";

    /// <summary>T64: a pinned node's ItemStatus (the mac's value).</summary>
    public const string PinnedStatus = "pinned";

    /// <summary>T67: the diagram-only menu item, by state (the mac's).</summary>
    public const string PinLabel = "Pin";
    public const string UnpinLabel = "Unpin";

    /// <summary>T68: the tooltip's composed shape — label, " — ", in, " in / ",
    /// out, " out" (the inventory's; a visual 1.4.13 tooltip, never announced).</summary>
    public const string TooltipSeparator = " — ";
    public const string TooltipInSuffix = " in / ";
    public const string TooltipOutSuffix = " out";

    /// <summary>The Windows-authored prefix over core's neighbour render — the
    /// node peer's HelpText (Term T2; the mac's AXCustomContent label).</summary>
    public const string ConnectsToPrefix = "Connects to: ";

    /// <summary>Term V3: the four viewport verbs' labels and hints — the mac's
    /// byte for byte (`SlateCommands.swift:1583–1611`; MacCatalogParityTests' P3).</summary>
    public const string ZoomInLabel = "Graph: Zoom In";
    public const string ZoomInHint = "Zoom the visual diagram in. The zoom level is announced.";
    public const string ZoomOutLabel = "Graph: Zoom Out";
    public const string ZoomOutHint = "Zoom the visual diagram out.";
    public const string ActualSizeLabel = "Graph: Actual Size";
    public const string ActualSizeHint = "Reset the visual diagram zoom to 100 percent.";
    public const string FitGraphLabel = "Graph: Fit Graph";
    public const string FitGraphHint = "Zoom so every node is visible. Option-Command-0 on the diagram.";

    // --- W6-2 PR E (E-11): the inspector — the mac's, byte for byte -----------------

    /// <summary>T29: the header's toggle (<c>GraphTableView.swift:179</c>).</summary>
    public const string InspectorLabel = "Inspector";

    /// <summary>T30: the toggle's AX label and help (<c>GraphTableView.swift:181–182</c>).</summary>
    public const string InspectorToggleName = "Toggle graph inspector";

    public const string InspectorToggleHint = "Show the graph inspector — filters, colour groups, display, and forces.";

    /// <summary>T37: the pane's group (<c>GraphInspectorView.swift:26</c>).</summary>
    public const string InspectorName = "Graph inspector";

    /// <summary>T38, T43, T50, T55: the four sections.</summary>
    public const string InspectorFiltersSection = "Filters";

    public const string InspectorGroupsSection = "Groups";

    public const string InspectorDisplaySection = "Display";

    public const string InspectorForcesSection = "Forces";

    /// <summary>T39: the inspector's name field; its AX label IS <see cref="FilterFieldName"/> (C-5, one constant).</summary>
    public const string InspectorNameFieldLabel = "Filter by name";

    /// <summary>T40–T42: the three toggles and their hints (the header's T26–T28 twins).</summary>
    public const string InspectorAttachmentsLabel = "Attachments";

    public const string InspectorAttachmentsHint = "Include attachment nodes.";

    public const string InspectorUnresolvedLabel = "Unresolved";

    public const string InspectorUnresolvedHint = "Include unresolved link targets.";

    public const string InspectorOrphansLabel = "Orphans only";

    public const string InspectorOrphansHint = "Show only notes with no links in or out.";

    /// <summary>T44: the empty text.</summary>
    public const string InspectorNoGroupsText = "No groups. Add one to colour matching nodes.";

    /// <summary>T45: the add button and its hint.</summary>
    public const string InspectorAddGroupLabel = "Add Group";

    public const string InspectorAddGroupHint = "Add a colour rule that highlights nodes whose name matches a query.";

    /// <summary>T46–T49: a row's controls — the visible labels and the
    /// composed AX names (the mac's <c>index + 1</c>; <see cref="InspectorGroupQueryName"/> and its siblings).</summary>
    public const string InspectorGroupQueryLabel = "Query";

    public const string InspectorGroupColourLabel = "Colour";

    public const string InspectorGroupRingLabel = "Ring";

    public const string InspectorGroupQueryFormat = "Group {0} query";

    public const string InspectorGroupColourFormat = "Group {0} colour";

    public const string InspectorGroupRingFormat = "Group {0} ring style";

    public const string InspectorRemoveGroupFormat = "Remove group {0}";

    /// <summary>T51–T54: the display's controls and their hints.</summary>
    public const string InspectorArrowsLabel = "Arrows";

    public const string InspectorArrowsHint = "Draw arrowheads on directed links.";

    public const string InspectorTextFadeLabel = "Text fade";

    public const string InspectorTextFadeHint = "Zoom level below which node labels hide.";

    public const string InspectorNodeSizeLabel = "Node size";

    public const string InspectorNodeSizeHint = "Multiplier on node circle size.";

    public const string InspectorLinkThicknessLabel = "Link thickness";

    public const string InspectorLinkThicknessHint = "Edge line width.";

    /// <summary>T56–T59: the forces' sliders and their hints.</summary>
    public const string InspectorCenterLabel = "Center";

    public const string InspectorCenterHint = "Gravity pulling the graph toward the centre.";

    public const string InspectorRepelLabel = "Repel";

    public const string InspectorRepelHint = "How strongly nodes push each other apart.";

    public const string InspectorLinkForceLabel = "Link force";

    public const string InspectorLinkForceHint = "How strongly linked nodes pull together.";

    public const string InspectorLinkDistanceLabel = "Link distance";

    public const string InspectorLinkDistanceHint = "The ideal length of a link.";

    /// <summary>T60: a slider's value text — the mac's <c>String(format: "%.2f")</c>,
    /// a dot under every culture (IGU-5).</summary>
    public const string InspectorSliderValueFormat = "F2";

    public static string InspectorSliderValue(double value) =>
        value.ToString(InspectorSliderValueFormat, System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>T46–T49's composed names, the 1-based index formatted invariantly.</summary>
    public static string InspectorGroupQueryName(int index) =>
        string.Format(System.Globalization.CultureInfo.InvariantCulture, InspectorGroupQueryFormat, index);

    public static string InspectorGroupColourName(int index) =>
        string.Format(System.Globalization.CultureInfo.InvariantCulture, InspectorGroupColourFormat, index);

    public static string InspectorGroupRingName(int index) =>
        string.Format(System.Globalization.CultureInfo.InvariantCulture, InspectorGroupRingFormat, index);

    public static string InspectorRemoveGroupName(int index) =>
        string.Format(System.Globalization.CultureInfo.InvariantCulture, InspectorRemoveGroupFormat, index);

    // --- W6-2 PR E: Windows-authored, never announced -------------------------------------

    /// <summary>E-D6 (Term Y6): the read-only notice's prefix; the preferences' load failure follows it.</summary>
    public const string InspectorReadOnlyPrefix = "Graph settings are read-only: ";

    /// <summary>E-D8 (Term I7): the inactive notice while the graph is not effective.</summary>
    public const string InspectorInactiveText = "Open the graph to change these settings.";
}
