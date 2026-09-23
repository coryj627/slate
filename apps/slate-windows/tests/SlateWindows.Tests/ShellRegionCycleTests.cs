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
        public HashSet<ShellRegionKind> Holds { get; } = [];
        public List<ShellRegionKind> Landed { get; } = [];
        public List<(Action Announce, Action FallThrough)> Held { get; } = [];
        public int Withdrawals { get; private set; }

        /// <summary>What the next withdrawal answers: whether the region
        /// still held its landing.</summary>
        public bool StillHeld { get; set; } = true;

        public bool WithdrawHeldLanding()
        {
            Withdrawals++;
            return StillHeld;
        }

        public ShellRegionKind? FocusedRegion() => Focused;

        public ShellRegionLanding TryLand(ShellRegionKind region, Action announceWhenLanded, Action fallThroughWhenRefused)
        {
            Landed.Add(region);
            if (Refuses.Contains(region))
            {
                return ShellRegionLanding.Refused;
            }

            if (Holds.Contains(region))
            {
                Held.Add((announceWhenLanded, fallThroughWhenRefused));
                return ShellRegionLanding.Pending;
            }

            Focused = region;
            return ShellRegionLanding.Landed;
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

            // W7-7 R-10: a press whose landings were answered at once holds
            // nothing, so the next press has nothing to withdraw.
            workspace.FocusNextPaneCommand.Execute(null);
            Assert.Equal(0, host.Withdrawals);
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
    public void LandingOnFilesAnnouncesFilesRegionFocused()
    {
        (WorkspaceViewModel workspace, FakeHost host, List<A11yEvent> announced, FixtureVault fixture, VaultSession session) = Open();
        using (fixture)
        using (session)
        using (workspace)
        {
            host.Focused = ShellRegionKind.EmptyEditor;

            workspace.FocusPreviousPaneCommand.Execute(null);

            Assert.Equal([ShellRegionKind.Files], host.Landed);
            Assert.IsType<A11yEvent.FilesRegionFocused>(Assert.Single(announced));
        }
    }

    [Fact]
    public void LandingOnTheEditorAnnouncesTheActivePane()
    {
        (WorkspaceViewModel workspace, FakeHost host, List<A11yEvent> announced, FixtureVault fixture, VaultSession session) = Open();
        using (fixture)
        using (session)
        using (workspace)
        {
            workspace.OpenPath("note0.md");
            announced.Clear();
            host.Focused = ShellRegionKind.TabBar;

            workspace.FocusNextPaneCommand.Execute(null);

            Assert.Equal([ShellRegionKind.Editor], host.Landed);
            // AnnounceActivePane (Layout.cs) speaks the pane, not the region:
            // ordinal of total, then the active tab's title (extension stripped).
            A11yEvent.EditorPaneFocused pane =
                Assert.IsType<A11yEvent.EditorPaneFocused>(Assert.Single(announced));
            Assert.Equal("note0", pane.Title);
        }
    }

    [Fact]
    public void LandingOnRightPaneContentAnnouncesTheLeaf()
    {
        (WorkspaceViewModel workspace, FakeHost host, List<A11yEvent> announced, FixtureVault fixture, VaultSession session) = Open();
        using (fixture)
        using (session)
        using (workspace)
        {
            workspace.OpenPath("note0.md");
            announced.Clear();
            host.Focused = ShellRegionKind.Editor;

            workspace.FocusNextPaneCommand.Execute(null);

            Assert.Equal([ShellRegionKind.RightPaneContent], host.Landed);
            A11yEvent.LeafPanelShown leaf =
                Assert.IsType<A11yEvent.LeafPanelShown>(Assert.Single(announced));
            Assert.Equal(workspace.ActiveLeaf.Title, leaf.Title);
        }
    }

    [Fact]
    public void LandingOnTheMenuBarAnnouncesIt()
    {
        (WorkspaceViewModel workspace, FakeHost host, List<A11yEvent> announced, FixtureVault fixture, VaultSession session) = Open();
        using (fixture)
        using (session)
        using (workspace)
        {
            host.Focused = ShellRegionKind.StatusBar;

            workspace.FocusNextPaneCommand.Execute(null);

            Assert.Equal([ShellRegionKind.MenuBar], host.Landed);
            A11yEvent.ShellRegionFocused region =
                Assert.IsType<A11yEvent.ShellRegionFocused>(Assert.Single(announced));
            Assert.IsType<ShellRegion.MenuBar>(region.Region);
        }
    }

    /// <summary>W7-7 PR 8 (R-10): a region that holds the landing for
    /// content still arriving (a reading surface) is neither announced nor
    /// passed over — the W7-6 contract speaks a region only once focus is in
    /// it. The host speaks the same line when focus arrives, through the
    /// callback the ring handed it.</summary>
    [Fact]
    public void AHeldLandingIsSpokenOnlyWhenFocusArrives()
    {
        (WorkspaceViewModel workspace, FakeHost host, List<A11yEvent> announced, FixtureVault fixture, VaultSession session) = Open();
        using (fixture)
        using (session)
        using (workspace)
        {
            workspace.OpenPath("note0.md");
            announced.Clear();
            host.Focused = ShellRegionKind.TabBar;
            host.Holds.Add(ShellRegionKind.Editor);

            workspace.FocusNextPaneCommand.Execute(null);

            Assert.Equal([ShellRegionKind.Editor], host.Landed);
            Assert.Empty(announced);

            Assert.Single(host.Held).Announce();

            A11yEvent.EditorPaneFocused pane = Assert.IsType<A11yEvent.EditorPaneFocused>(Assert.Single(announced));
            Assert.Equal("note0", pane.Title);

            // Spoken, the landing is done: the next press withdraws nothing
            // and goes on from where focus arrived.
            host.Focused = ShellRegionKind.Editor;
            workspace.FocusNextPaneCommand.Execute(null);
            Assert.Equal(0, host.Withdrawals);
            Assert.Equal([ShellRegionKind.Editor, ShellRegionKind.RightPaneContent], host.Landed);
        }
    }

    /// <summary>A held landing that turns out untakeable (its focus refused,
    /// or only the load-failure notice arrived) resumes the SAME press past
    /// it: the next region receives focus once and speaks its own line, the
    /// held region never. A modal surface that opened meanwhile owns the keys,
    /// so nothing resumes.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ARefusedHeldLandingResumesThePressPastIt(bool modalOpenedMeanwhile)
    {
        (WorkspaceViewModel workspace, FakeHost host, List<A11yEvent> announced, FixtureVault fixture, VaultSession session) = Open();
        using (fixture)
        using (session)
        using (workspace)
        {
            workspace.OpenPath("note0.md");
            announced.Clear();
            host.Focused = ShellRegionKind.TabBar;
            host.Holds.Add(ShellRegionKind.Editor);
            workspace.FocusNextPaneCommand.Execute(null);
            Assert.Equal([ShellRegionKind.Editor], host.Landed);
            host.ModalSurfaceOpen = modalOpenedMeanwhile;

            Assert.Single(host.Held).FallThrough();

            if (modalOpenedMeanwhile)
            {
                Assert.Equal([ShellRegionKind.Editor], host.Landed);
                Assert.Empty(announced);
                return;
            }

            Assert.Equal([ShellRegionKind.Editor, ShellRegionKind.RightPaneContent], host.Landed);
            Assert.IsType<A11yEvent.LeafPanelShown>(Assert.Single(announced));
        }
    }

    /// <summary>R-10: a second press while a landing is held CANCELS it — the
    /// host lets go of it, once — and starts a fresh traversal FROM THE HELD
    /// REGION'S RING POSITION in the new press's direction. Focus on the tab
    /// bar, the editor held: F6 goes on to the right pane's content (itself
    /// held here, so the replacement is a token of its own); Shift+F6 goes
    /// back to the tab bar. The cancelled token, completed after its
    /// replacement was issued, does nothing — no line, no second traversal,
    /// no focus move — and the replacement still completes: one landing, one
    /// line.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ASecondPressCancelsAHeldLanding(bool backward)
    {
        (WorkspaceViewModel workspace, FakeHost host, List<A11yEvent> announced, FixtureVault fixture, VaultSession session) = Open();
        using (fixture)
        using (session)
        using (workspace)
        {
            workspace.OpenPath("note0.md");
            announced.Clear();
            host.Focused = ShellRegionKind.TabBar;
            host.Holds.Add(ShellRegionKind.Editor);
            host.Holds.Add(ShellRegionKind.RightPaneContent);
            workspace.FocusNextPaneCommand.Execute(null);
            (Action announceFirst, Action fallThroughFirst) = Assert.Single(host.Held);

            (backward ? workspace.FocusPreviousPaneCommand : workspace.FocusNextPaneCommand).Execute(null);

            A11yEvent tabBar = new A11yEvent.TabFocused("Tab bar. ", "note0", 1, 1);
            Assert.Equal(1, host.Withdrawals);
            if (backward)
            {
                Assert.Equal([ShellRegionKind.Editor, ShellRegionKind.TabBar], host.Landed);
                Assert.Equal(ShellRegionKind.TabBar, host.Focused);
                Assert.Equal([tabBar], announced);
                Assert.Single(host.Held);
            }
            else
            {
                Assert.Equal([ShellRegionKind.Editor, ShellRegionKind.RightPaneContent], host.Landed);
                Assert.Equal(ShellRegionKind.TabBar, host.Focused);
                Assert.Equal(2, host.Held.Count);
                Assert.NotSame(announceFirst, host.Held[1].Announce);
                Assert.NotSame(fallThroughFirst, host.Held[1].FallThrough);
                Assert.Empty(announced);
            }

            // The cancelled token completes after its replacement was issued.
            announceFirst();
            fallThroughFirst();

            Assert.Equal(1, host.Withdrawals);
            if (backward)
            {
                Assert.Equal([ShellRegionKind.Editor, ShellRegionKind.TabBar], host.Landed);
                Assert.Equal(ShellRegionKind.TabBar, host.Focused);
                Assert.Equal([tabBar], announced);
                return;
            }

            Assert.Equal([ShellRegionKind.Editor, ShellRegionKind.RightPaneContent], host.Landed);
            Assert.Equal(ShellRegionKind.TabBar, host.Focused);
            Assert.Empty(announced);
            host.Held[1].Announce();
            Assert.Equal([new A11yEvent.LeafPanelShown(workspace.ActiveLeaf.Title)], announced);
        }
    }

    /// <summary>R-10: the held region is the ring's position only while its
    /// landing is still held and focus is where the held press left it. A
    /// landing the region already let go of, or one the reader walked away
    /// from, holds no position: the next press starts from where focus is —
    /// from the tab bar the editor, asked again; from the Files tree the tab
    /// bar.</summary>
    [Theory]
    [InlineData("the region let go")]
    [InlineData("the reader moved")]
    public void APressAfterTheHeldLandingWasLetGoStartsFromFocus(string why)
    {
        (WorkspaceViewModel workspace, FakeHost host, List<A11yEvent> announced, FixtureVault fixture, VaultSession session) = Open();
        using (fixture)
        using (session)
        using (workspace)
        {
            workspace.OpenPath("note0.md");
            announced.Clear();
            host.Focused = ShellRegionKind.TabBar;
            host.Holds.Add(ShellRegionKind.Editor);
            workspace.FocusNextPaneCommand.Execute(null);
            Assert.Single(host.Held);
            if (why == "the region let go")
            {
                host.StillHeld = false;
            }
            else
            {
                host.Focused = ShellRegionKind.Files;
            }

            workspace.FocusNextPaneCommand.Execute(null);

            Assert.Equal(1, host.Withdrawals);
            if (why == "the region let go")
            {
                Assert.Equal([ShellRegionKind.Editor, ShellRegionKind.Editor], host.Landed);
                Assert.Equal(2, host.Held.Count);
                Assert.Empty(announced);
                return;
            }

            Assert.Equal([ShellRegionKind.Editor, ShellRegionKind.TabBar], host.Landed);
            Assert.Equal(ShellRegionKind.TabBar, host.Focused);
            Assert.Equal([new A11yEvent.TabFocused("Tab bar. ", "note0", 1, 1)], announced);
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
