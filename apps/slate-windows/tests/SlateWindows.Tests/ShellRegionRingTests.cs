// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace SlateWindows.Tests;

/// <summary>W7-6 (#1240) spec §1: the F6 ring is a pure function of the
/// shell's layout, so every shape is pinned here without a window.</summary>
public sealed class ShellRegionRingTests
{
    private static readonly ShellRegionLayout Full = new(HasTabs: true, RightPaneVisible: true, RightPaneHasContentStop: true);

    [Fact]
    public void TheFullRingHasSevenRegionsInSpecOrder() =>
        Assert.Equal(
            [
                ShellRegionKind.MenuBar,
                ShellRegionKind.Files,
                ShellRegionKind.TabBar,
                ShellRegionKind.Editor,
                ShellRegionKind.RightPaneContent,
                ShellRegionKind.RightPaneRail,
                ShellRegionKind.StatusBar,
            ],
            ShellRegionRing.Ring(Full));

    [Fact]
    public void NoTabsReplacesTabBarAndEditorWithTheEmptyPane() =>
        Assert.Equal(
            [
                ShellRegionKind.MenuBar,
                ShellRegionKind.Files,
                ShellRegionKind.EmptyEditor,
                ShellRegionKind.RightPaneContent,
                ShellRegionKind.RightPaneRail,
                ShellRegionKind.StatusBar,
            ],
            ShellRegionRing.Ring(Full with { HasTabs = false }));

    [Fact]
    public void AHiddenRightPaneDropsBothRightStops() =>
        Assert.Equal(
            [ShellRegionKind.MenuBar, ShellRegionKind.Files, ShellRegionKind.TabBar, ShellRegionKind.Editor, ShellRegionKind.StatusBar],
            ShellRegionRing.Ring(Full with { RightPaneVisible = false }));

    [Fact]
    public void ALeafWithoutAStopDropsOnlyTheContentRegion() =>
        Assert.Equal(
            [ShellRegionKind.MenuBar, ShellRegionKind.Files, ShellRegionKind.TabBar, ShellRegionKind.Editor, ShellRegionKind.RightPaneRail, ShellRegionKind.StatusBar],
            ShellRegionRing.Ring(Full with { RightPaneHasContentStop = false }));

    [Fact]
    public void NextWrapsInBothDirections()
    {
        Assert.Equal(ShellRegionKind.MenuBar, ShellRegionRing.Next(Full, ShellRegionKind.StatusBar, +1));
        Assert.Equal(ShellRegionKind.StatusBar, ShellRegionRing.Next(Full, ShellRegionKind.MenuBar, -1));
        Assert.Equal(ShellRegionKind.TabBar, ShellRegionRing.Next(Full, ShellRegionKind.Files, +1));
        Assert.Equal(ShellRegionKind.Files, ShellRegionRing.Next(Full, ShellRegionKind.TabBar, -1));
    }

    [Fact]
    public void AnUnknownOriginIsBeforeTheFirstRegion()
    {
        Assert.Equal(ShellRegionKind.MenuBar, ShellRegionRing.Next(Full, null, +1));
        Assert.Equal(ShellRegionKind.StatusBar, ShellRegionRing.Next(Full, null, -1));
    }

    [Fact]
    public void AnOriginAbsentFromTheRingResolvesToItsNeighbour()
    {
        // Focus was in the right-pane content when the leaf lost its stop:
        // forward goes to the rail, backward to the editor.
        ShellRegionLayout layout = Full with { RightPaneHasContentStop = false };
        Assert.Equal(ShellRegionKind.RightPaneRail, ShellRegionRing.Next(layout, ShellRegionKind.RightPaneContent, +1));
        Assert.Equal(ShellRegionKind.Editor, ShellRegionRing.Next(layout, ShellRegionKind.RightPaneContent, -1));
        // Focus was on the tab bar when the last tab closed.
        ShellRegionLayout noTabs = Full with { HasTabs = false };
        Assert.Equal(ShellRegionKind.EmptyEditor, ShellRegionRing.Next(noTabs, ShellRegionKind.TabBar, +1));
        Assert.Equal(ShellRegionKind.Files, ShellRegionRing.Next(noTabs, ShellRegionKind.Editor, -1));
    }
}
