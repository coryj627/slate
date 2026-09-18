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

    internal static string Spoken(string id) => ChordTable.WindowsSpokenFor(id)
        ?? throw new InvalidOperationException($"Navigation help requires a chord-table row with a Windows chord: '{id}'.");
}
