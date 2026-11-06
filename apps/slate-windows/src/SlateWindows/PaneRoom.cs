// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using uniffi.slate_uniffi;

namespace SlateWindows;

/// <summary>DS-14 (#1815): which of the two panes a row of this width shows. A wanted pane
/// shows when it fits, at its narrowest, beside the editor's floor and the other wanted
/// pane. When only one fits, the pane the reader asked for last wins; with no ask, Files,
/// then the right pane. A pane the reader asked for shows even where it doesn't fit beside
/// the editor's floor alone, which then gives way, so a command does what it says.</summary>
internal static class PaneRoom
{
    internal readonly record struct Showing(bool Files, bool Right);

    internal static Showing Decide(
        double rowWidth,
        double filesWidth,
        double rightWidth,
        bool filesWanted,
        bool rightWanted,
        ShellSidebar? lastAsked)
    {
        if (rowWidth <= 0)
        {
            return new Showing(filesWanted, rightWanted);
        }

        double editor = WorkspaceViewModel.MinimumEditorWidth;
        double splitter = WorkspaceViewModel.SplitterWidth;
        if (filesWanted && rightWanted && filesWidth + rightWidth + editor + (2 * splitter) <= rowWidth)
        {
            return new Showing(true, true);
        }

        bool Wanted(ShellSidebar pane) => pane == ShellSidebar.Files ? filesWanted : rightWanted;
        bool Fits(ShellSidebar pane) => (pane == ShellSidebar.Files ? filesWidth : rightWidth) + editor + splitter <= rowWidth;
        Showing Only(ShellSidebar pane) => new(pane == ShellSidebar.Files, pane == ShellSidebar.RightPane);

        ShellSidebar? asked = lastAsked is { } asking && Wanted(asking) ? asking : null;
        if (asked is { } first)
        {
            return Only(first);
        }

        foreach (ShellSidebar pane in new[] { ShellSidebar.Files, ShellSidebar.RightPane })
        {
            if (Wanted(pane) && Fits(pane))
            {
                return Only(pane);
            }
        }

        return new Showing(false, false);
    }
}
