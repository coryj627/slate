// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using uniffi.slate_uniffi;

namespace SlateWindows;

/// <summary>
/// Plan 47 CB-23 (#1800, CD15): the line a right-pane panel shows while Windows has no body
/// for it. Which leaves those are is this host's fact; the words are core's
/// (<c>empty_panel_line</c>), and the reading view's shortcut in them is the chord table's row
/// for the toggle, the row the menu and the note header's switch read. <c>ChordTableTests</c>
/// proves that the window has a key binding for that row's chord, not which command the
/// binding runs; <c>MainWindow.xaml</c> binds it to the reading-view toggle.
/// </summary>
internal sealed partial class WorkspaceViewModel
{
    /// <summary>The leaves Windows offers before it has built them, and the panel core words
    /// each one as. <c>RightPaneLeafBodyCensus</c> holds this to the leaves the placeholder
    /// shows for.</summary>
    internal static IReadOnlyDictionary<string, EmptyPanel> PanelsWithoutBody { get; } =
        new Dictionary<string, EmptyPanel>(StringComparer.Ordinal)
        {
            ["math"] = EmptyPanel.Math,
            ["code"] = EmptyPanel.Code,
            ["diagrams"] = EmptyPanel.Diagrams,
        };

    /// <summary>The open panel's line as it is shown, with the reading view's display chord
    /// ("Ctrl+Shift+E"); null for a panel with a body.</summary>
    public string? ActiveLeafEmptyLine =>
        EmptyLineFor(Commands.ChordTable.WindowsChordFor(Commands.ChordTable.Ids.ToggleViewMode));

    /// <summary>The same line as a screen reader speaks it, with the chord in the table's
    /// spoken form ("Control Shift E", contract P6); the line's accessible name.</summary>
    public string? ActiveLeafEmptyLineName =>
        EmptyLineFor(Commands.ChordTable.WindowsSpokenFor(Commands.ChordTable.Ids.ToggleViewMode));

    /// <summary>Where the reader is: a note in view in its editor or its reading view, or no
    /// note (no tab, or a tab that is not a note).</summary>
    internal EmptyPanelContext EmptyPanelContextNow =>
        ActiveGroup.ActiveTab is { IsMarkdown: true } tab
            ? tab.IsReadingMode ? EmptyPanelContext.NoteInReadingView : EmptyPanelContext.NoteInEditor
            : EmptyPanelContext.NoNote;

    private string? EmptyLineFor(string? chord) =>
        PanelsWithoutBody.TryGetValue(_activeLeaf.Id, out EmptyPanel panel)
            ? SlateUniffiMethods.EmptyPanelLine(panel, EmptyPanelContextNow, chord ?? string.Empty)
            : null;

    /// <summary>The line follows the open leaf, the active tab and that tab's view.</summary>
    private void NotifyEmptyPanelLine()
    {
        OnPropertyChanged(nameof(ActiveLeafEmptyLine));
        OnPropertyChanged(nameof(ActiveLeafEmptyLineName));
    }
}
