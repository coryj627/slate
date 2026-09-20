// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>W7-6 (#1240) spec §2–§4: a press asks the host where focus is,
/// lands the ring's next region through the host, and announces it —
/// the modal no-op and the fall-through are the view model's, not the
/// window's.</summary>
public sealed class ShellRegionCycleTests
{
    private sealed class FakeHost : IShellRegionHost
    {
        public bool ModalSurfaceOpen { get; set; }
        public bool RightPaneHasContentStop { get; set; } = true;
        public string StatusText { get; set; } = "Scan finished: 2 files indexed.";
        public ShellRegionKind? Focused { get; set; }
        public HashSet<ShellRegionKind> Refuses { get; } = [];
        public List<ShellRegionKind> Landed { get; } = [];

        public ShellRegionKind? FocusedRegion() => Focused;

        public bool TryLand(ShellRegionKind region)
        {
            Landed.Add(region);
            if (Refuses.Contains(region))
            {
                return false;
            }

            Focused = region;
            return true;
        }
    }

    private static (WorkspaceViewModel Workspace, FakeHost Host, List<A11yEvent> Announced, FixtureVault Fixture, VaultSession Session) Open()
    {
        FixtureVault fixture = FixtureVault.Create(2, "shell-region-cycle");
        VaultSession session = VaultSession.OpenFilesystem(fixture.Root);
        using var cancel = new CancelToken();
        session.ScanInitial(cancel);
        var announced = new List<A11yEvent>();
        var workspace = new WorkspaceViewModel(session, fixture.Root, () => [], announced.Add);
        var host = new FakeHost();
        workspace.ShellRegionHost = host;
        return (workspace, host, announced, fixture, session);
    }

    [Fact]
    public void ForwardFromFilesWithATabLandsOnTheTabBarAndAnnouncesIt()
    {
        (WorkspaceViewModel workspace, FakeHost host, List<A11yEvent> announced, FixtureVault fixture, VaultSession session) = Open();
        using (fixture)
        using (session)
        using (workspace)
        {
            workspace.OpenPath("note0.md");
            announced.Clear();
            host.Focused = ShellRegionKind.Files;

            workspace.FocusNextPaneCommand.Execute(null);

            Assert.Equal([ShellRegionKind.TabBar], host.Landed);
            A11yEvent.TabFocused tab = Assert.IsType<A11yEvent.TabFocused>(Assert.Single(announced));
            Assert.Equal("Tab bar. ", tab.Prefix);
            // The tab bar reads like a tab activation: Activate() (Layout.cs)
            // announces TabFocused with tab.Title, which strips the extension.
            Assert.Equal("note0", tab.Filename);
        }
    }

    [Fact]
    public void BackwardFromTheMenuBarWrapsToTheStatusBarWithItsText()
    {
        (WorkspaceViewModel workspace, FakeHost host, List<A11yEvent> announced, FixtureVault fixture, VaultSession session) = Open();
        using (fixture)
        using (session)
        using (workspace)
        {
            host.Focused = ShellRegionKind.MenuBar;

            workspace.FocusPreviousPaneCommand.Execute(null);

            Assert.Equal([ShellRegionKind.StatusBar], host.Landed);
            A11yEvent.ShellRegionFocused region = Assert.IsType<A11yEvent.ShellRegionFocused>(Assert.Single(announced));
            ShellRegion.StatusBar status = Assert.IsType<ShellRegion.StatusBar>(region.Region);
            Assert.Equal("Scan finished: 2 files indexed.", status.Text);
        }
    }

    [Fact]
    public void NoTabsLandsOnTheEmptyEditorPane()
    {
        (WorkspaceViewModel workspace, FakeHost host, List<A11yEvent> announced, FixtureVault fixture, VaultSession session) = Open();
        using (fixture)
        using (session)
        using (workspace)
        {
            host.Focused = ShellRegionKind.Files;

            workspace.FocusNextPaneCommand.Execute(null);

            Assert.Equal([ShellRegionKind.EmptyEditor], host.Landed);
            A11yEvent.ShellRegionFocused region = Assert.IsType<A11yEvent.ShellRegionFocused>(Assert.Single(announced));
            Assert.IsType<ShellRegion.EmptyEditor>(region.Region);
        }
    }

    [Fact]
    public void AHiddenRightPaneIsSkippedNotRevealed()
    {
        (WorkspaceViewModel workspace, FakeHost host, List<A11yEvent> announced, FixtureVault fixture, VaultSession session) = Open();
        using (fixture)
        using (session)
        using (workspace)
        {
            workspace.OpenPath("note0.md");
            workspace.IsRightPaneVisible = false;
            announced.Clear();
            host.Focused = ShellRegionKind.Editor;

            workspace.FocusNextPaneCommand.Execute(null);

            Assert.Equal([ShellRegionKind.StatusBar], host.Landed);
            Assert.False(workspace.IsRightPaneVisible);
        }
    }

    [Fact]
    public void ARefusedLandingFallsThroughToTheNextRegion()
    {
        (WorkspaceViewModel workspace, FakeHost host, List<A11yEvent> announced, FixtureVault fixture, VaultSession session) = Open();
        using (fixture)
        using (session)
        using (workspace)
        {
            workspace.OpenPath("note0.md");
            announced.Clear();
            host.Focused = ShellRegionKind.Editor;
            host.Refuses.Add(ShellRegionKind.RightPaneContent);

            workspace.FocusNextPaneCommand.Execute(null);

            Assert.Equal([ShellRegionKind.RightPaneContent, ShellRegionKind.RightPaneRail], host.Landed);
            A11yEvent.ShellRegionFocused region = Assert.IsType<A11yEvent.ShellRegionFocused>(Assert.Single(announced));
            Assert.IsType<ShellRegion.RightPaneRail>(region.Region);
        }
    }

    [Fact]
    public void EveryLandingRefusedLeavesFocusAndSaysNothing()
    {
        (WorkspaceViewModel workspace, FakeHost host, List<A11yEvent> announced, FixtureVault fixture, VaultSession session) = Open();
        using (fixture)
        using (session)
        using (workspace)
        {
            host.Focused = ShellRegionKind.Files;
            foreach (ShellRegionKind region in Enum.GetValues<ShellRegionKind>())
            {
                host.Refuses.Add(region);
            }

            workspace.FocusNextPaneCommand.Execute(null);

            Assert.Equal(ShellRegionRing.Ring(new ShellRegionLayout(false, true, true)).Count, host.Landed.Count);
            Assert.Empty(announced);
        }
    }

    [Fact]
    public void AModalSurfaceMakesThePressANoOp()
    {
        (WorkspaceViewModel workspace, FakeHost host, List<A11yEvent> announced, FixtureVault fixture, VaultSession session) = Open();
        using (fixture)
        using (session)
        using (workspace)
        {
            host.ModalSurfaceOpen = true;
            host.Focused = ShellRegionKind.Files;

            workspace.FocusNextPaneCommand.Execute(null);

            Assert.Empty(host.Landed);
            Assert.Empty(announced);
        }
    }

    [Fact]
    public void TheVerbsAreAlwaysExecutable()
    {
        (WorkspaceViewModel workspace, FakeHost _, List<A11yEvent> _, FixtureVault fixture, VaultSession session) = Open();
        using (fixture)
        using (session)
        using (workspace)
        {
            Assert.True(workspace.FocusNextPaneCommand.CanExecute(null));
            Assert.True(workspace.FocusPreviousPaneCommand.CanExecute(null));
        }
    }
}
