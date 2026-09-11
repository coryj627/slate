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
}
