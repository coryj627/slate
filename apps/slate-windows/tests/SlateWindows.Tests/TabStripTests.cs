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
        Assert.Equal(new[] { model.Title, $"Close {model.Title}" }, ReaderOrder(tab));

        model.Text = "# Changed\n";
        host.Settle();

        Assert.Equal("Unsaved changes", AutomationProperties.GetItemStatus(tab));
        TextBlock marker = ShownShell.Descendants(tab).OfType<TextBlock>().Single(text => AutomationProperties.GetName(text) == "Unsaved changes");
        Assert.True(marker.IsVisible, "the dirty marker isn't showing");
        Assert.Equal(new[] { model.Title, "Unsaved changes", $"Close {model.Title}" }, ReaderOrder(tab));
    });

    /// <summary>What a reader walks in a tab's header, in the order it walks it: visual
    /// order, which is what UIA's tree follows. Only the header, because a selected tab's own
    /// peer also holds its content once that's been realized.</summary>
    private static string[] ReaderOrder(TabItem tab)
    {
        DockPanel header = ShownShell.Descendants(tab).OfType<DockPanel>().First();
        return Enumerable.Range(0, VisualTreeHelper.GetChildrenCount(header))
            .Select(index => (UIElement)VisualTreeHelper.GetChild(header, index))
            .Where(child => child.IsVisible)
            .Select(UIElementAutomationPeer.CreatePeerForElement)
            .Where(peer => peer is not null && (peer.IsControlElement() || peer.IsContentElement()))
            .Select(peer => peer.GetName())
            .ToArray();
    }

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

    /// <summary>Opening another kind of item in the current tab reuses the tab.</summary>
    [Fact]
    public void AReusedTabChangesItsIcon() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "# A\n"), ("c.base", "# A\n"));
        host.Workspace.OpenPath("a.md");
        host.Settle();
        AssertTabIcon(Tabs(host).Single(), 0xE8A5);

        host.Workspace.OpenPath("c.base", WorkspaceOpenTarget.CurrentTab);
        host.Settle();

        AssertTabIcon(Tabs(host).Single(), 0xE8A9);
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

    /// <summary>Tabs that don't fit wrap to another row. WPF's TabPanel stretches a wrapped
    /// row's cells, so a tab's limit left gaps, and the open tab's flares and its neighbours'
    /// dividers stopped meeting: the rows are packed instead, with the open one next to the
    /// note, and a row's first tab has nothing to divide itself from.</summary>
    [Fact]
    public void WrappedTabsArePackedWithTheOpenRowNextToTheNote() => RunSta(() =>
    {
        (string, string)[] notes = Enumerable.Range(0, 12).Select(index => ($"note-{index}-with-a-longer-name.md", "# N\n")).ToArray();
        using var host = new ShownShell(notes);
        foreach ((string path, string _) in notes)
        {
            host.Workspace.OpenPath(path, WorkspaceOpenTarget.NewTab);
        }

        host.Settle();

        TabPanel strip = Strip(host);
        Assert.IsType<TabStripPanel>(strip);
        Assert.True(strip.ActualHeight >= 32 * 2, $"the strip is {strip.ActualHeight}");
        AssertRowsArePacked(host);

        // Choosing a tab in another row brings that row down to the note.
        WorkspaceTabViewModel first = host.Workspace.ActiveGroup.Tabs[0];
        host.Workspace.ActiveGroup.ActiveTab = first;
        host.Settle();

        TabItem open = Tabs(host).Single(tab => tab.IsSelected);
        Assert.Same(first, open.DataContext);
        AssertRowsArePacked(host);
    });

    private static void AssertRowsArePacked(ShownShell host)
    {
        TabPanel strip = Strip(host);
        TabItem[] tabs = Tabs(host);
        double lastRow = tabs.Max(tab => tab.TranslatePoint(default, strip).Y);
        Assert.Equal(lastRow, tabs.Single(tab => tab.IsSelected).TranslatePoint(default, strip).Y, 1);
        foreach (IGrouping<double, TabItem> row in tabs.GroupBy(tab => tab.TranslatePoint(default, strip).Y))
        {
            double edge = 0;
            foreach (TabItem tab in row.OrderBy(tab => tab.TranslatePoint(default, strip).X))
            {
                double left = tab.TranslatePoint(default, strip).X;
                Assert.Equal(edge, left, 1);
                bool starts = edge == 0;
                Assert.Equal(starts, TabStripPanel.GetIsRowStart(tab));
                Assert.Equal(
                    starts || tab.IsSelected ? Visibility.Collapsed : Visibility.Visible,
                    Divider(tab).Visibility);
                edge = left + tab.ActualWidth;
            }

            Assert.True(edge <= strip.ActualWidth + 0.5, $"a row runs to {edge} in a strip of {strip.ActualWidth}");
        }
    }

    /// <summary>A split can be narrower than a tab's floor. The tab fits the strip then, and
    /// the close button keeps its place ahead of the title, so a pointer can still reach it
    /// at the narrowest share a split allows.</summary>
    [Fact]
    public void ANarrowSplitKeepsTheCloseButtonInReach() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "# A\n"), ("b.md", "# B\n"));
        host.Workspace.OpenPath("a.md");
        host.Workspace.OpenPath("b.md", WorkspaceOpenTarget.SplitRight);
        host.Settle();
        for (int step = 0; step < 20; step++)
        {
            host.Workspace.ShrinkPaneCommand.Execute(null);
        }

        host.Settle();

        TabPanel strip = ShownShell.Descendants(host.Shell.ContentPaneBorder).OfType<TabPanel>()
            .Where(panel => panel.IsVisible)
            .OrderBy(panel => panel.ActualWidth)
            .First();
        TabItem tab = ShownShell.Descendants(strip).OfType<TabItem>().First();
        Assert.True(strip.ActualWidth < 100, $"premise: the narrower strip is {strip.ActualWidth}, wide enough for a tab");
        Assert.True(tab.ActualWidth <= strip.ActualWidth + 0.5, $"the tab is {tab.ActualWidth} in a strip of {strip.ActualWidth}");

        Button close = CloseButton(tab);
        Point left = close.TranslatePoint(default, strip);
        Assert.True(left.X >= -0.5 && left.X + close.ActualWidth <= strip.ActualWidth + 0.5, $"the button runs {left.X} to {left.X + close.ActualWidth} in a strip of {strip.ActualWidth}");
        var middle = new Point(left.X + (close.ActualWidth / 2), left.Y + (close.ActualHeight / 2));
        DependencyObject? hit = strip.InputHitTest(middle) as DependencyObject;
        Assert.NotNull(hit);
        Assert.True(
            ReferenceEquals(hit, close) || ShownShell.Descendants(close).Contains(hit),
            $"a pointer at the button's middle hits {hit}");
    });

    /// <summary>A tab's only as wide as it needs, however few share its row.</summary>
    [Fact]
    public void ALoneTabIsItsOwnWidth() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "# A\n"));
        host.Workspace.OpenPath("a.md");
        host.Settle();

        TabItem tab = Tabs(host).Single();

        Assert.Equal(0, tab.TranslatePoint(default, Strip(host)).X, 1);
        Assert.True(tab.ActualWidth < 240, $"the tab is {tab.ActualWidth}");
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

    /// <summary>The canvas sheets return from the window's key route early, so a guard after
    /// them would miss them, and closing the tab retires the document a card editor is
    /// holding a draft for.</summary>
    [Theory]
    [InlineData(Key.F4, "prompt")]
    [InlineData(Key.W, "prompt")]
    [InlineData(Key.F4, "picker")]
    [InlineData(Key.W, "picker")]
    [InlineData(Key.F4, "card editor")]
    [InlineData(Key.W, "card editor")]
    public void ACloseChordDoesNothingBehindACanvasSheet(Key key, string sheet) => RunSta(() =>
    {
        const string Board = """{"nodes":[{"id":"question","type":"text","text":"Core question","x":0,"y":0,"width":200,"height":100}],"edges":[]}""";
        using var host = new ShownShell(("board.canvas", Board), ("note1.md", "# N\n"));
        WithSidebar(host, sidebar =>
        {
            WorkspaceViewModel workspace = host.Workspace;
            workspace.CanvasNoteCreator = sidebar;
            workspace.OpenPath("board.canvas");
            CanvasDocumentViewModel document = Assert.IsType<CanvasDocumentViewModel>(workspace.ActiveGroup.ActiveTab!.Canvas);
            Assert.True(
                PumpedDispatcher.PumpUntil(() => document.AppliedPublication?.Loaded is not null, TimeSpan.FromSeconds(30)),
                "premise: the canvas never loaded");
            host.Settle();
            document.SeatSelectionSilently("question");
            (sheet switch
            {
                "prompt" => workspace.CanvasConvertToNoteCommand,
                "picker" => workspace.CanvasAddNoteCommand,
                _ => workspace.CanvasNewCardCommand,
            }).Execute(null);
            object? Current() => sheet switch
            {
                "prompt" => workspace.CanvasPromptSheet,
                "picker" => workspace.CanvasCardPickerSheet,
                _ => workspace.CanvasCardEditorSheet,
            };
            Assert.True(PumpedDispatcher.PumpUntil(() => Current() is not null, TimeSpan.FromSeconds(30)), $"premise: the {sheet} never opened");
            host.Settle();
            object open = Current()!;
            Assert.NotNull(host.Shell.OpenModalSurface);

            CommandPaletteTests.ThreadModifiers.Hold(ModifierKeys.Control, () => RaiseKey(host, key));
            PumpedDispatcher.Drain();

            Assert.Single(workspace.ActiveGroup.Tabs);
            Assert.Same(open, Current());
            workspace.CloseCanvasCardEditor();
            workspace.CloseCanvasCardPicker();
            workspace.CloseCanvasPrompt();
            host.Settle();
        });
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
    public void CtrlF4IsAShellChordThePickersSwallow() => Assert.True(
        MainWindow.IsUnderlyingShellShortcutForTests(Key.F4, ModifierKeys.Control),
        "Ctrl+F4 isn't on the list of shell chords the pickers swallow, so it would close a tab behind one.");

    /// <summary>A close chord is a bare KeyBinding, which a sheet doesn't stop by itself:
    /// neither a clean tab's close nor a dirty tab's prompt may run behind one.</summary>
    [Theory]
    [InlineData(Key.F4, false)]
    [InlineData(Key.W, false)]
    [InlineData(Key.F4, true)]
    [InlineData(Key.W, true)]
    public void ACloseChordDoesNothingBehindASheet(Key key, bool dirty) => RunSta(() =>
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
        if (dirty)
        {
            ((WorkspaceTabViewModel)Tabs(host).Single().DataContext).Text = "changed\n";
            host.Settle();
        }

        host.Workspace.OpenAddPropertySheet(synchronousForTests: true);
        host.Settle();
        host.Settle();
        Assert.NotNull(host.Shell.OpenModalSurface);
        Assert.True(host.Shell.IsKeyboardFocusWithin, "premise: the sheet took no keys");

        CommandPaletteTests.ThreadModifiers.Hold(ModifierKeys.Control, () => RaiseKey(host, key));
        PumpedDispatcher.Drain();

        Assert.Single(host.Workspace.ActiveGroup.Tabs);
        Assert.Equal(0, asked);
        Assert.NotNull(host.Shell.OpenModalSurface);
        host.Workspace.CloseAddPropertySheetCommand.Execute(null);
        host.Settle();
    });

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
        UIElement target = Keyboard.FocusedElement as UIElement ?? host.Shell;
        var preview = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
        target.RaiseEvent(preview);
        if (!preview.Handled)
        {
            target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key) { RoutedEvent = Keyboard.KeyDownEvent });
        }
    }

    /// <summary>A real sidebar over the shown shell, as the lifecycle attaches one.</summary>
    private static void WithSidebar(ShownShell host, Action<FilesSidebarViewModel> body)
    {
        string appData = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"slate-ds8-{Guid.NewGuid():N}");
        System.Reflection.PropertyInfo slot = typeof(VaultLifecycleViewModel).GetProperty(nameof(VaultLifecycleViewModel.FileSidebar))
            ?? throw new InvalidOperationException("FileSidebar is gone");
        var sidebar = new FilesSidebarViewModel(host.Session, _ => { }, localAppDataRoot: appData);
        try
        {
            PumpedDispatcher.PumpUntilDrained(sidebar.TreeRefreshCompletion);
            slot.SetValue(host.Shell.DataContext, sidebar);
            host.Settle();
            body(sidebar);
        }
        finally
        {
            slot.SetValue(host.Shell.DataContext, null);
            try
            {
                Directory.Delete(appData, recursive: true);
            }
            catch (IOException)
            {
            }
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
