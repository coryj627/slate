// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace SlateWindows.Commands;

/// <summary>Short entry-point help, composed from the same rows that
/// advertise and deliver the chords (W7-3 N-1/N-4).</summary>
internal static class NavigationHelp
{
    public static string QuickOpen =>
        "Up and Down move selection. Enter opens. "
        + $"{Spoken("windows.quickOpen.openNewTab")} opens a new tab. "
        + $"{Spoken("windows.quickOpen.openSplitRight")} opens a right split. "
        + $"{Spoken("windows.quickOpen.openSplitDown")} opens a down split. "
        + "Escape closes Quick Open.";

    public static string Reading =>
        $"Next heading: {Spoken("windows.reading.nextH")}. "
        + $"Previous heading: {Spoken("windows.reading.previousH")}. "
        + $"Next link: {Spoken("windows.reading.nextK")}. "
        + $"Next list: {Spoken("windows.reading.nextL")}. "
        + $"Next table: {Spoken("windows.reading.nextT")}. "
        + "Tab moves through links and embedded controls. Enter activates.";

    public static string HorizontalSplitter =>
        $"Drag, or use {Spoken("windows.splitter.growLeading")} and {Spoken("windows.splitter.growTrailing")}, to resize adjacent editor panes.";

    public static string VerticalSplitter =>
        $"Drag, or use {Spoken("windows.splitter.growAbove")} and {Spoken("windows.splitter.growBelow")}, to resize adjacent editor panes.";

    public static string Shell =>
        $"{Spoken("slate.workspace.focusNextPane")} moves to the next region: menu bar, files, tab bar, editor, right pane, status bar. "
        + $"{Spoken("slate.workspace.focusPreviousPane")} moves back.";

    /// <summary>W7-7 (R-2, OD-2): arrows only show a note; the three row
    /// gestures are spoken from their own rows.</summary>
    public static string FilesTree =>
        "Up and Down move through files and folders, and a selected note is shown. "
        + $"{Spoken("windows.filesTree.openSelected")} opens it and moves focus into it. "
        + $"{Spoken("windows.filesTree.openSelectedInNewTab")} opens it in a new tab. "
        + $"{Spoken("windows.filesTree.toggleBatchSelection")} checks or unchecks it for batch actions.";

    /// <summary>W7-7 (R-3): the Files filter field's grammar and its
    /// clear routes, the key spoken from its own row.</summary>
    public static string SidebarFilter =>
        "Filter by words, #tag, path:, ext:, has:task, or @date. "
        + $"{Spoken("windows.sidebarFilter.clear")} or the Clear filter button clears the filter.";

    internal static string Spoken(string id) => ChordTable.WindowsSpokenFor(id)
        ?? throw new InvalidOperationException($"Navigation help requires a chord-table row with a Windows chord: '{id}'.");
}
