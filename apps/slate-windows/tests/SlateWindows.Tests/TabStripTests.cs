// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using SlateWindows.Canvas;
using SlateWindows.Commands;

namespace SlateWindows.Tests;

/// <summary>
/// DS-8 (#1809): open notes sit in a 36-high strip of 32-high tabs, 100 to 240 wide, with
/// a kind icon and a close glyph, and the open tab joined to the note below. What a screen
/// reader hears about a tab stays as it was: this is a drawing, not a change of contract.
/// </summary>
public sealed class TabStripTests
{
    [Fact]
    public void ATabIsThirtyTwoAndTheStripThirtySix() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "# A\n"));
        host.Workspace.OpenPath("a.md");
        host.Settle();

        TabItem tab = Tabs(host).Single();
        TabControl tabs = ItemsControlOf(tab);

        Assert.Equal(32, tab.ActualHeight, 1);
        Assert.Equal(4, tab.TranslatePoint(default, tabs).Y, 1);
        // The rule under the strip is its last row, so the strip is 36 with the rule.
        Assert.Equal(36, RuleOf(tab).TranslatePoint(default, tabs).Y + 1, 1);
    });

    /// <summary>An empty pane has nothing to strip: its hint sits on the pane.</summary>
    [Fact]
    public void AnEmptyPaneHasNoStrip() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "# A\n"));
        host.Workspace.OpenPath("a.md");
        host.Settle();
        TabPanel strip = Strip(host);
        Assert.True(strip.IsVisible, "premise: the strip isn't showing with a tab open");

        host.Workspace.CloseActiveTabCommand.Execute(null);
        host.Settle();

        Assert.Empty(host.Workspace.ActiveGroup.Tabs);
        Assert.False(Strip(host).IsVisible, "the strip stayed with nothing to show");
    });

    [Fact]
    public void ATabsWidthStaysBetweenOneHundredAndTwoHundredForty() => RunSta(() =>
    {
        string longName = new string('w', 120) + ".md";
        using var host = new ShownShell(("a.md", "# A\n"), (longName, "# Long\n"));
        host.Workspace.OpenPath("a.md");
        host.Workspace.OpenPath(longName, WorkspaceOpenTarget.NewTab);
        host.Settle();

        TabItem[] tabs = Tabs(host);
        TabItem shortTab = tabs.Single(tab => ((WorkspaceTabViewModel)tab.DataContext).Path == "a.md");
        TabItem longTab = tabs.Single(tab => ((WorkspaceTabViewModel)tab.DataContext).Path == longName);

        Assert.True(shortTab.ActualWidth >= 100, $"the short tab is {shortTab.ActualWidth}");
        // A cut title measures a little short of its column, so this is near 240, not at it.
        Assert.InRange(longTab.ActualWidth, 200, 240);
        TextBlock title = ShownShell.Descendants(longTab).OfType<TextBlock>().Single(text =>
            text.Text == ((WorkspaceTabViewModel)longTab.DataContext).Title);
        Assert.Equal(TextTrimming.CharacterEllipsis, title.TextTrimming);
        title.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Assert.True(title.ActualWidth < title.DesiredSize.Width, "the long title isn't cut");
        // The close button keeps its target whatever the title does.
        Assert.Equal(40, CloseButton(longTab).ActualWidth, 1);
    });

    [Fact]
    public void TheCloseButtonDrawsAnIconAndKeepsItsName() => RunSta(() =>
    {
        using var host = new ShownShell(("Lecture.md", "# A\n"));
        host.Workspace.OpenPath("Lecture.md");
        host.Settle();

        TabItem tab = Tabs(host).Single();
        Button close = CloseButton(tab);

        Assert.Equal(((char)0xE711).ToString(), close.Content);
        Assert.Equal(40, close.ActualWidth, 1);
        Assert.Equal(32, close.ActualHeight, 1);
        Assert.Equal($"Close {((WorkspaceTabViewModel)tab.DataContext).Title}", AutomationProperties.GetName(close));
        TextBlock glyph = ShownShell.Descendants(close).OfType<TextBlock>().Single();
        Assert.IsType<AutomationPresentationTextBlock>(glyph);
        Assert.Equal(12, glyph.FontSize);
        // What a reader reaches: a glyph's own peer is in neither view.
        foreach (AutomationPeer peer in PeersUnder(UIElementAutomationPeer.CreatePeerForElement(tab))
            .Where(peer => peer.IsControlElement() || peer.IsContentElement()))
        {
            Assert.DoesNotContain(peer.GetName() ?? string.Empty, IsPrivateUse);
        }
    });

    /// <summary>Everything a reader hears about the tab, kept as W7-7 settled it.</summary>
    [Fact]
    public void ATabReadsAsItDid() => RunSta(() =>
    {
        using var host = new ShownShell(("Lecture.md", "# A\n"));
        host.Workspace.OpenPath("Lecture.md");
        host.Settle();

        TabItem tab = Tabs(host).Single();
        var model = (WorkspaceTabViewModel)tab.DataContext;
        Assert.Equal("Saved", AutomationProperties.GetItemStatus(tab));
        Assert.StartsWith(model.Title, AutomationProperties.GetName(tab), StringComparison.Ordinal);

        model.Text = "# Changed\n";
        host.Settle();

        Assert.Equal("Unsaved changes", AutomationProperties.GetItemStatus(tab));
        TextBlock marker = ShownShell.Descendants(tab).OfType<TextBlock>().Single(text => AutomationProperties.GetName(text) == "Unsaved changes");
        Assert.True(marker.IsVisible, "the dirty marker isn't showing");
    });

    [Theory]
    [InlineData("a.md", 0xE8A5)]
    [InlineData("b.canvas", 0xECA5)]
    [InlineData("c.base", 0xE8A9)]
    public void TheTabKindPicksItsIcon(string path, int glyph) => RunSta(() =>
    {
        using var host = new ShownShell((path, path.EndsWith(".canvas", StringComparison.Ordinal) ? "{}" : "# A\n"));
        host.Workspace.OpenPath(path);
        host.Settle();

        AssertTabIcon(Tabs(host).Single(), glyph);
    });

    [Fact]
    public void AGraphTabHasItsOwnIcon() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "Links to [[b]].\n"), ("b.md", "Links to [[a]].\n"));
        host.Workspace.OpenGraph();
        PumpedDispatcher.PumpUntilDrained(host.Workspace.GraphDocument!.WhenAllWorkDrained());
        host.Settle();

        AssertTabIcon(Tabs(host).Single(), 0xE9E9);
    });

    /// <summary>Selection isn't colour and tone alone: a line along the top, and the tab
    /// reaching down over the rule under the strip, so it joins the note.</summary>
    [Fact]
    public void TheOpenTabJoinsTheNoteAndCarriesALine() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "# A\n"), ("b.md", "# B\n"));
        host.Workspace.OpenPath("a.md");
        host.Workspace.OpenPath("b.md", WorkspaceOpenTarget.NewTab);
        host.Settle();

        TabItem[] tabs = Tabs(host);
        TabItem open = tabs.Single(tab => tab.IsSelected);
        TabItem other = tabs.Single(tab => !tab.IsSelected);
        Assert.Equal(1, Panel.GetZIndex(open));
        Assert.Equal(Visibility.Visible, Indicator(open).Visibility);
        Assert.Equal(Visibility.Collapsed, Indicator(other).Visibility);
        Assert.Equal(2, Indicator(open).ActualHeight, 1);
        Assert.Same(host.Shell.TryFindResource("Slate.AccentIndicatorBrush"), Indicator(open).Background);
        Assert.All(Flares(open), flare => Assert.Equal(Visibility.Visible, flare.Visibility));
        Assert.All(Flares(other), flare => Assert.Equal(Visibility.Collapsed, flare.Visibility));
        Assert.Equal(new CornerRadius(8, 8, 0, 0), Chrome(open).CornerRadius);
        Assert.Same(host.Shell.TryFindResource("Slate.SurfaceBrush"), Chrome(open).Background);
        Assert.Equal(Brushes.Transparent.Color, ((SolidColorBrush)Chrome(other).Background).Color);

        // The tab's foot covers the rule, which is the note's top edge.
        TabControl control = ItemsControlOf(open);
        double foot = open.TranslatePoint(new Point(0, open.ActualHeight), control).Y;
        double rule = RuleOf(open).TranslatePoint(default, control).Y;
        Assert.Equal(rule + 1, foot, 1);
    });

    /// <summary>A divider between unselected tabs, and none beside the open one, because
    /// that's drawn above its neighbours, or ahead of the first.</summary>
    [Fact]
    public void ADividerSeparatesOnlyUnselectedTabs() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "# A\n"), ("b.md", "# B\n"), ("c.md", "# C\n"));
        host.Workspace.OpenPath("a.md");
        host.Workspace.OpenPath("b.md", WorkspaceOpenTarget.NewTab);
        host.Workspace.OpenPath("c.md", WorkspaceOpenTarget.NewTab);
        host.Settle();

        TabItem[] tabs = Tabs(host).OrderBy(tab => tab.TranslatePoint(default, Strip(host)).X).ToArray();
        Assert.True(tabs[2].IsSelected, "premise: the last tab opened is the open one");
        Assert.Equal(Visibility.Collapsed, Divider(tabs[0]).Visibility);
        Assert.Equal(Visibility.Visible, Divider(tabs[1]).Visibility);
        Assert.Equal(Visibility.Collapsed, Divider(tabs[2]).Visibility);
        Assert.Equal(1, Divider(tabs[1]).ActualWidth, 1);
        Assert.Equal(16, Divider(tabs[1]).ActualHeight, 1);
        Assert.Same(host.Shell.TryFindResource("Slate.SubtleBorderBrush"), Divider(tabs[1]).Background);
    });

    /// <summary>Tabs that don't fit wrap to a second row, which is WPF's TabPanel.</summary>
    [Fact]
    public void TabsThatDoNotFitWrapToASecondRow() => RunSta(() =>
    {
        (string, string)[] notes = Enumerable.Range(0, 12).Select(index => ($"note-{index}-with-a-longer-name.md", "# N\n")).ToArray();
        using var host = new ShownShell(notes);
        foreach ((string path, string _) in notes)
        {
            host.Workspace.OpenPath(path, WorkspaceOpenTarget.NewTab);
        }

        host.Settle();

        Assert.True(Strip(host).ActualHeight >= 32 * 2, $"the strip is {Strip(host).ActualHeight}");
    });

    [Theory]
    [InlineData("Delete")]
    [InlineData("Ctrl+F4")]
    public void AFocusedTabClosesOnDeleteAndOnCtrlF4(string chord) => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "Just a line of text.\n"), ("b.md", "Another line.\n"));
        host.Workspace.OpenPath("a.md");
        host.Workspace.OpenPath("b.md", WorkspaceOpenTarget.NewTab);
        host.Settle();
        Assert.True(Tabs(host).Single(tab => tab.IsSelected).Focus(), "premise: the tab took no keys");

        Press(host, chord);
        PumpedDispatcher.Drain();

        WorkspaceTabViewModel left = Assert.Single(host.Workspace.ActiveGroup.Tabs);
        Assert.Equal("a.md", left.Path);
        Assert.IsNotType<MainWindow>(Keyboard.FocusedElement);
        Assert.True(host.Shell.ContentPaneBorder.IsKeyboardFocusWithin, $"the keys are on {Keyboard.FocusedElement}");
    });

    /// <summary>A pane's last tab: the keys go to the empty pane, not the window.</summary>
    [Fact]
    public void DeleteOnTheLastTabLandsOnTheEmptyPane() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "Just a line of text.\n"));
        host.Workspace.OpenPath("a.md");
        host.Settle();
        Assert.True(Tabs(host).Single().Focus(), "premise: the tab took no keys");

        Press(host, "Delete");
        PumpedDispatcher.Drain();

        Assert.Empty(host.Workspace.ActiveGroup.Tabs);
        Assert.IsNotType<MainWindow>(Keyboard.FocusedElement);
        Assert.Equal(ShellRegionKind.EmptyEditor, ((IShellRegionHost)host.Shell).FocusedRegion());
    });

    /// <summary>The same command as the close button, so an unsaved tab's prompt runs the
    /// same way, and an answer of cancel leaves it open.</summary>
    [Theory]
    [InlineData("Delete")]
    [InlineData("Ctrl+F4")]
    public void ADirtyTabPromptsBeforeClosing(string chord) => RunSta(() =>
    {
        int asked = 0;
        using var host = new ShownShell(
            _ =>
            {
                asked++;
                return WorkspaceDirtyNavigationDecision.Cancel;
            },
            ("a.md", "Just a line of text.\n"));
        host.Workspace.OpenPath("a.md");
        host.Settle();
        ((WorkspaceTabViewModel)Tabs(host).Single().DataContext).Text = "changed\n";
        host.Settle();
        Assert.True(Tabs(host).Single().Focus(), "premise: the tab took no keys");

        Press(host, chord);
        PumpedDispatcher.Drain();

        Assert.Equal(1, asked);
        Assert.Single(host.Workspace.ActiveGroup.Tabs);
    });

    /// <summary>Delete is the tab's only while the tab has the keys: in the editor it's
    /// text, and on the close button or in a sheet it isn't this.</summary>
    [Fact]
    public void DeleteElsewhereClosesNothing() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "Just a line of text.\n"));
        host.Workspace.OpenPath("a.md");
        host.Settle();
        Assert.True(host.Land(ShellRegionKind.Editor), "premise: the editor took no keys");

        Press(host, "Delete");
        PumpedDispatcher.Drain();

        Assert.Single(host.Workspace.ActiveGroup.Tabs);
    });

    [Fact]
    public void CtrlF4IsSwallowedUnderASheet() => Assert.True(
        MainWindow.IsUnderlyingShellShortcutForTests(Key.F4, ModifierKeys.Control),
        "Ctrl+F4 isn't on the list of shell chords a sheet fences, so it would close a tab behind one.");

    [Fact]
    public void TheTableKnowsBothChords()
    {
        Assert.Contains(ChordTable.Entries, row => row.Id == "windows.workspace.closeTabCtrlF4" && row.WindowsChord == "Ctrl+F4");
        Assert.Contains(ChordTable.Entries, row => row.Id == "windows.workspace.closeFocusedTab" && row.WindowsChord == "Delete" && row.Scope == ChordScope.TabStrip);
    }

    private static void Press(ShownShell host, string chord)
    {
        (Key key, ModifierKeys modifiers) = chord == "Delete" ? (Key.Delete, ModifierKeys.None) : (Key.F4, ModifierKeys.Control);
        if (modifiers == ModifierKeys.None)
        {
            RaiseKey(host, key);
        }
        else
        {
            CommandPaletteTests.ThreadModifiers.Hold(modifiers, () => RaiseKey(host, key));
        }
    }

    /// <summary>Through the keys' own route: the tunnelling handler first, then, if nothing
    /// took it, the window's bindings.</summary>
    private static void RaiseKey(ShownShell host, Key key)
    {
        PresentationSource source = PresentationSource.FromVisual(host.Shell)!;
        var target = (UIElement)Keyboard.FocusedElement;
        var preview = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
        target.RaiseEvent(preview);
        if (!preview.Handled)
        {
            target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key) { RoutedEvent = Keyboard.KeyDownEvent });
        }
    }

    private static void AssertTabIcon(TabItem tab, int glyph)
    {
        TextBlock icon = ShownShell.Descendants(tab).OfType<AutomationPresentationTextBlock>()
            .First(text => !ShownShell.Descendants(CloseButton(tab)).Contains(text));
        Assert.Equal(((char)glyph).ToString(), icon.Text);
        Assert.Equal(16, icon.FontSize);
    }

    private static bool IsPrivateUse(char character) => character is >= (char)0xE000 and <= (char)0xF8FF;

    private static IEnumerable<AutomationPeer> PeersUnder(AutomationPeer root)
    {
        foreach (AutomationPeer peer in root.GetChildren() ?? new List<AutomationPeer>())
        {
            yield return peer;
            foreach (AutomationPeer nested in PeersUnder(peer))
            {
                yield return nested;
            }
        }
    }

    private static TabItem[] Tabs(ShownShell host) =>
        ShownShell.Descendants(host.Shell.ContentPaneBorder).OfType<TabItem>().ToArray();

    private static TabControl ItemsControlOf(TabItem tab) =>
        (TabControl)ItemsControl.ItemsControlFromItemContainer(tab);

    /// <summary>The border round the note, whose top edge is the rule under the strip.</summary>
    private static FrameworkElement RuleOf(TabItem tab) =>
        (FrameworkElement)ShownShell.Descendants(ItemsControlOf(tab)).OfType<ContentPresenter>()
            .Single(presenter => presenter.Name == "PART_SelectedContentHost").Parent;

    private static TabPanel Strip(ShownShell host) =>
        ShownShell.Descendants(host.Shell.ContentPaneBorder).OfType<TabPanel>().First();

    private static Button CloseButton(TabItem tab) =>
        ShownShell.Descendants(tab).OfType<Button>().Single();

    private static Border Named(TabItem tab, string name) =>
        (Border)(tab.Template.FindName(name, tab) ?? throw new InvalidOperationException($"the tab's template has no {name}"));

    private static Border Chrome(TabItem tab) => Named(tab, "Chrome");

    private static Border Indicator(TabItem tab) => Named(tab, "Indicator");

    private static Border Divider(TabItem tab) => Named(tab, "Divider");

    private static System.Windows.Shapes.Path[] Flares(TabItem tab) =>
        ShownShell.Descendants(tab).OfType<System.Windows.Shapes.Path>().ToArray();

    private static void RunSta(Action body)
    {
        Func<bool> priorOverlayProbe = CanvasSurfaceView.ShellOverlayIsOpen;
        try
        {
            StaThread.RunPumped(body, TimeSpan.FromSeconds(90), "STA test body timed out.");
        }
        finally
        {
            CanvasSurfaceView.ShellOverlayIsOpen = priorOverlayProbe;
        }
    }
}
