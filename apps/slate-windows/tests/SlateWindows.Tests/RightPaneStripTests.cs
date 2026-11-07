// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// DS-16 (DSD8, DSD19): the right-pane rail is a strip of five icons and a More
/// panels button whose menu holds the other twelve. Every panel is reachable by
/// keyboard, and a reader can tell which is open: from the strip's selection, or
/// from the button's selected look and its name, and from the menu's check.
/// </summary>
public sealed class RightPaneStripTests
{
    private static readonly string[] StripIds = ["outline", "backlinks", "outgoingLinks", "connections", "inspector"];

    [Fact]
    public void TheStripHasFiveAndMoreHoldsTheOtherTwelve() => RunSta(() =>
    {
        using var host = new ShownShell(("note.md", "# Note\n"));
        host.Settle();

        Assert.Equal(StripIds, host.Shell.RightPaneLeavesList.Items.Cast<WorkspaceLeafOption>().Select(leaf => leaf.Id));
        ContextMenu menu = OpenMore(host);
        Assert.Equal(
            WorkspaceViewModel.Leaves.Skip(5).Select(leaf => leaf.Title),
            menu.Items.OfType<MenuItem>().Select(item => (string)item.Header));
        Assert.Equal(
            WorkspaceViewModel.Leaves.Select(leaf => leaf.Id).Order(),
            StripIds.Concat(menu.Items.OfType<MenuItem>().Select(item => AutomationProperties.GetAutomationId(item)[MainWindow.MorePanelsItemIdPrefix.Length..])).Order());
        menu.IsOpen = false;
    });

    /// <summary>Every leaf has one icon, a glyph or a drawing, and a reader meets only
    /// its title: no private-use character names the row or anything under it in the
    /// control view, which is what axe and a reader walk.</summary>
    [Fact]
    public void EveryLeafHasAnIconAndItsNameIsItsTitle() => RunSta(() =>
    {
        using var host = new ShownShell(("note.md", "# Note\n"));
        host.Settle();
        ListBox strip = host.Shell.RightPaneLeavesList;
        strip.UpdateLayout();
        ContextMenu menu = OpenMore(host);

        foreach (WorkspaceLeafOption leaf in WorkspaceViewModel.Leaves)
        {
            Assert.True(
                (leaf.Glyph.Length == 1) ^ (leaf.Drawing is not null),
                $"{leaf.Id} has {(leaf.Drawing is null ? "no icon" : "a glyph and a drawing")}.");
            FrameworkElement row = StripIds.Contains(leaf.Id)
                ? Assert.IsAssignableFrom<ListBoxItem>(strip.ItemContainerGenerator.ContainerFromItem(leaf))
                : menu.Items.OfType<MenuItem>().Single(item => AutomationProperties.GetAutomationId(item) == MainWindow.MorePanelsItemIdPrefix + leaf.Id);
            AutomationPeer peer = UIElementAutomationPeer.CreatePeerForElement(row);
            Assert.Equal(leaf.Title, peer.GetName());
            Assert.Single(ShownShell.Descendants(row).OfType<LeafIcon>());
            foreach (AutomationPeer under in Walk(peer).Where(under => under.IsControlElement()))
            {
                Assert.DoesNotContain(under.GetName(), character => character is >= '\uE000' and <= '\uF8FF');
            }
        }

        menu.IsOpen = false;
    });

    [Fact]
    public void TheSelectedRowDrawsThePill() => RunSta(() =>
    {
        using var host = new ShownShell(("note.md", "# Note\n"));
        host.Workspace.ActiveLeaf = Leaf("backlinks");
        host.Settle();
        ListBox strip = host.Shell.RightPaneLeavesList;
        strip.UpdateLayout();
        var accent = (SolidColorBrush)host.Shell.FindResource("Slate.AccentIndicatorBrush");

        Assert.Equal(accent.Color, PillOf(strip, "backlinks"));
        Assert.Equal(Colors.Transparent, PillOf(strip, "outline"));
    });

    /// <summary>The regression for the null write-back: a panel in the menu leaves the
    /// strip with no selection, and that empty selection never closes the panel.</summary>
    [Fact]
    public void OpeningAMorePanelLeavesActiveLeafAlone() => RunSta(() =>
    {
        using var host = new ShownShell(("note.md", "# Note\n"));
        host.Workspace.ActiveLeaf = Leaf("backlinks");
        host.Settle();

        host.Workspace.ActiveLeaf = Leaf("history");
        host.Settle();
        Assert.Null(host.Shell.RightPaneLeavesList.SelectedItem);
        Assert.Equal("history", host.Workspace.ActiveLeaf.Id);

        host.Shell.RightPaneLeavesList.SelectedItem = null;
        host.Workspace.StripLeaf = null;
        host.Settle();
        Assert.Equal("history", host.Workspace.ActiveLeaf.Id);
    });

    [Fact]
    public void AMorePanelIsMarkedInTheMenuAndOnTheButton() => RunSta(() =>
    {
        using var host = new ShownShell(("note.md", "# Note\n"));
        Button more = host.Shell.RightPaneMorePanelsButton;
        host.Workspace.ActiveLeaf = Leaf("tasks");
        host.Settle();

        Assert.Equal(SlateUniffiMethods.MorePanelsName("Tasks"), AutomationProperties.GetName(more));
        Assert.Equal(Visibility.Visible, PillOf(more).Visibility);
        ContextMenu menu = OpenMore(host);
        Assert.Equal(["tasks"], menu.Items.OfType<MenuItem>().Where(item => item.IsChecked).Select(Id));
        menu.IsOpen = false;
        host.Settle();

        host.Workspace.ActiveLeaf = Leaf("outline");
        host.Settle();
        Assert.Equal(SlateUniffiMethods.MorePanelsName(null), AutomationProperties.GetName(more));
        Assert.Equal(Visibility.Collapsed, PillOf(more).Visibility);
    });

    [Fact]
    public void ACommandOpeningAMorePanelMarksTheButton() => RunSta(() =>
    {
        using var host = new ShownShell(("note.md", "# Note\n"));
        host.Settle();

        host.Workspace.OpenTasksReview();
        host.Settle();

        Assert.Equal(SlateUniffiMethods.MorePanelsName("Tasks Review"), AutomationProperties.GetName(host.Shell.RightPaneMorePanelsButton));
        Assert.Null(host.Shell.RightPaneLeavesList.SelectedItem);
    });

    [Fact]
    public void ArrowsStayInTheStripAndMoreIsTheNextTabStop() => RunSta(() =>
    {
        using var host = new ShownShell(("note.md", "# Note\n"));
        host.Workspace.ActiveLeaf = Leaf("inspector");
        host.Settle();
        ListBox strip = host.Shell.RightPaneLeavesList;
        strip.UpdateLayout();
        var last = Assert.IsAssignableFrom<ListBoxItem>(strip.ItemContainerGenerator.ContainerFromItem(Leaf("inspector")));
        Assert.True(last.Focus(), "premise: the strip's last row refused the keys");

        Press(host, Key.Down);
        Assert.Same(last, Keyboard.FocusedElement);
        Assert.Equal("inspector", host.Workspace.ActiveLeaf.Id);

        Assert.True(last.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)));
        Assert.Same(host.Shell.RightPaneMorePanelsButton, Keyboard.FocusedElement);
    });

    [Fact]
    public void PickingFromMoreSwitchesThePanelOnceAndReturnsFocus() => RunSta(() =>
    {
        using var host = new ShownShell(("note.md", "# Note\n"));
        host.Workspace.ActiveLeaf = Leaf("outline");
        host.Settle();
        Assert.True(host.Shell.RightPaneMorePanelsButton.Focus(), "premise: More panels refused the keys");
        ContextMenu menu = OpenMore(host);
        Assert.Equal("embeds", Id(Assert.IsAssignableFrom<MenuItem>(Keyboard.FocusedElement)));
        int shown = host.Announced.OfType<A11yEvent.LeafPanelShown>().Count();

        MenuItem tasks = menu.Items.OfType<MenuItem>().Single(item => Id(item) == "tasks");
        Toggle(tasks);
        host.Settle();
        Assert.False(menu.IsOpen, "a pick didn't close More panels");

        Assert.Equal("tasks", host.Workspace.ActiveLeaf.Id);
        Assert.Equal(shown + 1, host.Announced.OfType<A11yEvent.LeafPanelShown>().Count());
        Assert.Same(host.Shell.RightPaneMorePanelsButton, Keyboard.FocusedElement);

        // Picking the open panel again changes nothing, and it keeps its check.
        menu = OpenMore(host);
        tasks = menu.Items.OfType<MenuItem>().Single(item => Id(item) == "tasks");
        Assert.Same(tasks, Keyboard.FocusedElement);
        Toggle(tasks);
        Assert.True(tasks.IsChecked);
        Assert.False(menu.IsOpen);
        Assert.Equal("tasks", host.Workspace.ActiveLeaf.Id);
        Assert.Equal(shown + 1, host.Announced.OfType<A11yEvent.LeafPanelShown>().Count());
    });

    [Fact]
    public void EscapeLeavesThePanelAlone() => RunSta(() =>
    {
        using var host = new ShownShell(("note.md", "# Note\n"));
        host.Workspace.ActiveLeaf = Leaf("connections");
        host.Settle();
        Assert.True(host.Shell.RightPaneMorePanelsButton.Focus(), "premise: More panels refused the keys");
        ContextMenu menu = OpenMore(host);

        Press(host, Key.Escape);
        host.Settle();

        Assert.False(menu.IsOpen);
        Assert.Equal("connections", host.Workspace.ActiveLeaf.Id);
        Assert.Same(host.Shell.RightPaneMorePanelsButton, Keyboard.FocusedElement);
    });

    /// <summary>Codex round 1: a reader's Invoke opens the menu with the keys somewhere
    /// else. A pick, and Escape, still bring them back to More panels.</summary>
    [Fact]
    public void AMenuOpenedFromElsewhereReturnsTheKeysToMorePanels() => RunSta(() =>
    {
        using var host = new ShownShell(("note.md", "# Note\n"));
        host.Workspace.ActiveLeaf = Leaf("outline");
        host.Settle();
        Button more = host.Shell.RightPaneMorePanelsButton;

        Assert.True(host.Shell.FilesTree.Focus(), "premise: the Files tree refused the keys");
        ContextMenu menu = OpenMore(host);
        Press(host, Key.Escape);
        host.Settle();
        Assert.False(menu.IsOpen);
        Assert.Same(more, Keyboard.FocusedElement);

        Assert.True(host.Shell.FilesTree.Focus(), "premise: the Files tree refused the keys");
        menu = OpenMore(host);
        Toggle(menu.Items.OfType<MenuItem>().Single(item => Id(item) == "history"));
        host.Settle();
        Assert.Equal("history", host.Workspace.ActiveLeaf.Id);
        Assert.Same(more, Keyboard.FocusedElement);
    });

    /// <summary>Codex round 1: a focus restore whose token is a strip row or More
    /// panels lands where the rail does now, whichever the token was.</summary>
    [Fact]
    public void ARestoreOntoTheRailLandsOnTheOpenPanelsPlace() => RunSta(() =>
    {
        using var host = new ShownShell(("note.md", "# Note\n"));
        host.Workspace.ActiveLeaf = Leaf("history");
        host.Settle();
        ListBox strip = host.Shell.RightPaneLeavesList;
        strip.UpdateLayout();
        var outline = Assert.IsAssignableFrom<ListBoxItem>(strip.ItemContainerGenerator.ContainerFromItem(Leaf("outline")));

        Assert.True(host.Shell.FilesTree.Focus(), "premise: the Files tree refused the keys");
        Assert.True(SelectorFocus.LandOnStop(outline));
        Assert.Same(host.Shell.RightPaneMorePanelsButton, Keyboard.FocusedElement);

        host.Workspace.ActiveLeaf = Leaf("connections");
        host.Settle();
        Assert.True(host.Shell.FilesTree.Focus(), "premise: the Files tree refused the keys");
        Assert.True(SelectorFocus.LandOnStop(host.Shell.RightPaneMorePanelsButton));
        var row = Assert.IsAssignableFrom<ListBoxItem>(Keyboard.FocusedElement);
        Assert.Equal("connections", ((WorkspaceLeafOption)row.DataContext).Id);
    });

    /// <summary>Codex round 1: WPF runs a menu's pick after it closes, so a workspace
    /// replaced in between (a vault closed) takes nothing meant for the old one.</summary>
    [Fact]
    public void APickForAReplacedWorkspaceDoesNothing() => RunSta(() =>
    {
        using var host = new ShownShell(("note.md", "# Note\n"));
        WorkspaceViewModel old = host.Workspace;
        old.ActiveLeaf = Leaf("outline");
        host.Settle();
        ContextMenu menu = OpenMore(host);
        MenuItem tasks = menu.Items.OfType<MenuItem>().Single(item => Id(item) == "tasks");
        int shown = host.Announced.OfType<A11yEvent.LeafPanelShown>().Count();

        host.ShowWelcomeWithoutAWorkspace();
        Toggle(tasks);
        host.Settle();

        Assert.Equal("outline", old.ActiveLeaf.Id);
        Assert.Equal(shown, host.Announced.OfType<A11yEvent.LeafPanelShown>().Count());
        menu.IsOpen = false;
    });

    [Fact]
    public void F6LandsOnTheOpenPanelsRowOrOnMore() => RunSta(() =>
    {
        using var host = new ShownShell(("note.md", "# Note\n"));
        host.Workspace.ActiveLeaf = Leaf("connections");
        host.Settle();
        Assert.True(host.Land(ShellRegionKind.RightPaneRail));
        var row = Assert.IsAssignableFrom<ListBoxItem>(Keyboard.FocusedElement);
        Assert.Equal("connections", ((WorkspaceLeafOption)row.DataContext).Id);
        Assert.Equal(ShellRegionKind.RightPaneRail, ((IShellRegionHost)host.Shell).FocusedRegion());

        host.Workspace.ActiveLeaf = Leaf("citations");
        host.Settle();
        Assert.True(host.Shell.FilesTree.Focus(), "premise: the Files tree refused the keys");
        Assert.True(host.Land(ShellRegionKind.RightPaneRail));
        Assert.Same(host.Shell.RightPaneMorePanelsButton, Keyboard.FocusedElement);
        Assert.Equal(ShellRegionKind.RightPaneRail, ((IShellRegionHost)host.Shell).FocusedRegion());

        // The boundary route lands the same way.
        Assert.True(host.Shell.FilesTree.Focus(), "premise: the Files tree refused the keys");
        Assert.True(host.Shell.LandOnRail());
        Assert.Same(host.Shell.RightPaneMorePanelsButton, Keyboard.FocusedElement);
    });

    [Fact]
    public void AShortWindowKeepsMoreReachable() => RunSta(() =>
    {
        using var host = new ShownShell(("note.md", "# Note\n"));
        host.Shell.Height = 360;
        host.Settle();
        host.Settle();
        var strip = (ScrollViewer)host.Shell.FindName("RightPaneRailStrip");
        Button more = host.Shell.RightPaneMorePanelsButton;

        Assert.True(more.Focus(), "More panels refused the keys in a short window");
        host.Settle();
        Point at = more.TranslatePoint(default, strip);
        Assert.True(at.Y >= -0.5 && at.Y + more.ActualHeight <= strip.ViewportHeight + 0.5, $"More panels is at {at.Y:F0} in a viewport of {strip.ViewportHeight:F0}.");
    });

    [Fact]
    public void TheDefaultWidthsAreTheDesigns() => RunSta(() =>
    {
        using var host = new ShownShell(("note.md", "# Note\n"));
        Assert.Equal(260, host.Workspace.FilesSidebarWidth);
        Assert.Equal(300, host.Workspace.RightPaneWidth);
    });

    private static WorkspaceLeafOption Leaf(string id) =>
        WorkspaceViewModel.Leaves.Single(leaf => string.Equals(leaf.Id, id, StringComparison.Ordinal));

    private static string Id(MenuItem item) =>
        AutomationProperties.GetAutomationId(item)[MainWindow.MorePanelsItemIdPrefix.Length..];

    private static ContextMenu OpenMore(ShownShell host)
    {
        host.Shell.OpenMorePanels();
        host.Settle();
        ContextMenu menu = Assert.IsType<ContextMenu>(host.Shell.MorePanelsMenu);
        Assert.True(menu.IsOpen, "More panels didn't open");
        return menu;
    }

    /// <summary>The menu's check, as a reader's toggle reaches it.</summary>
    private static void Toggle(MenuItem item)
    {
        AutomationPeer peer = UIElementAutomationPeer.CreatePeerForElement(item);
        ((IToggleProvider)peer.GetPattern(PatternInterface.Toggle)!).Toggle();
        PumpedDispatcher.Drain();
    }

    private static Color PillOf(ListBox strip, string id)
    {
        var row = Assert.IsAssignableFrom<ListBoxItem>(strip.ItemContainerGenerator.ContainerFromItem(Leaf(id)));
        Rectangle pill = ShownShell.Descendants(row).OfType<Rectangle>().Single(shape => shape.Width == 3);
        return Assert.IsType<SolidColorBrush>(pill.Fill).Color;
    }

    private static Rectangle PillOf(Button more) =>
        Assert.IsType<Rectangle>(more.Template.FindName("Pill", more));

    private static IEnumerable<AutomationPeer> Walk(AutomationPeer peer)
    {
        foreach (AutomationPeer child in peer.GetChildren() ?? [])
        {
            yield return child;
            foreach (AutomationPeer under in Walk(child))
            {
                yield return under;
            }
        }
    }

    /// <summary>A press on the focused element, as WPF raises it.</summary>
    private static void Press(ShownShell host, Key key)
    {
        var target = Assert.IsAssignableFrom<UIElement>(Keyboard.FocusedElement);
        PresentationSource source = PresentationSource.FromVisual(host.Shell)!;
        var preview = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
        target.RaiseEvent(preview);
        if (!preview.Handled)
        {
            target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key) { RoutedEvent = Keyboard.KeyDownEvent });
        }

        PumpedDispatcher.Drain();
    }

    private static void RunSta(Action body) =>
        StaThread.RunPumped(body, TimeSpan.FromSeconds(60), "STA test body timed out.");
}
