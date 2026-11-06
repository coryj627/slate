// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>DS-14 (#1815): the pure rule for which panes a row shows, over numbers. At their
/// narrowest the two panes and the editor need 760 together, Files and the editor 475,
/// and the right pane and the editor 555.</summary>
public sealed class PaneRoomTests
{
    [Theory]
    [InlineData(1000, true, true, null, true, true)]
    [InlineData(760, true, true, null, true, true)]
    [InlineData(759, true, true, null, true, false)]
    [InlineData(759, true, true, ShellSidebar.Files, true, false)]
    [InlineData(759, true, true, ShellSidebar.RightPane, false, true)]
    [InlineData(500, true, true, null, true, false)]
    [InlineData(500, true, true, ShellSidebar.RightPane, false, true)]
    [InlineData(474, true, true, null, false, false)]
    [InlineData(474, true, true, ShellSidebar.Files, true, false)]
    [InlineData(600, false, true, null, false, true)]
    [InlineData(554, false, true, null, false, false)]
    [InlineData(554, false, true, ShellSidebar.RightPane, false, true)]
    [InlineData(600, true, false, ShellSidebar.RightPane, true, false)]
    [InlineData(1000, false, false, ShellSidebar.Files, false, false)]
    [InlineData(0, true, true, null, true, true)]
    [InlineData(0, false, true, ShellSidebar.Files, false, true)]
    public void PaneRoomDecides(double row, bool filesWanted, bool rightWanted, ShellSidebar? lastAsked, bool files, bool right)
    {
        PaneRoom.Showing showing = PaneRoom.Decide(
            row,
            WorkspaceViewModel.MinimumSidebarWidth,
            WorkspaceViewModel.MinimumRightPaneWidth,
            filesWanted,
            rightWanted,
            lastAsked);

        Assert.Equal(new PaneRoom.Showing(files, right), showing);
    }

    /// <summary>An ask holds across the next width change: the rule reads the same last ask
    /// at every width, so a narrower row can't hand the room back to the other pane.</summary>
    [Fact]
    public void TheNextWidthChangeDoesntUndoAnAsk()
    {
        foreach (double row in new double[] { 759, 700, 600, 555, 500 })
        {
            Assert.Equal(
                new PaneRoom.Showing(false, true),
                PaneRoom.Decide(row, WorkspaceViewModel.MinimumSidebarWidth, WorkspaceViewModel.MinimumRightPaneWidth, true, true, ShellSidebar.RightPane));
        }
    }
}
