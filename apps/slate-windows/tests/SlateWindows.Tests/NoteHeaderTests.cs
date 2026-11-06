// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>DS-15 (#1816): the bar above a note, with its folders and name, the Source and
/// Reading switch and ⋯. Each control acts on its own note whichever pane is active, and
/// nothing a screen reader relies on moves.</summary>
public sealed class NoteHeaderTests
{
    private const string Nested = "learning/maths/Lecture.md";

    [Fact]
    public void TheHeaderIsFortyAndOnlyMarkdownTabsHaveIt() => StaThread.RunPumped(() =>
    {
        using var host = new ShownShell(("Lecture.md", "# Lecture\n"), ("Plan.canvas", "{\"nodes\":[],\"edges\":[]}"));
        host.Workspace.OpenPath("Lecture.md");
        host.Settle();

        NoteHeaderView header = HeaderOf(host, "Lecture.md");
        Assert.Equal(40, header.ActualHeight, 1);
        Assert.Equal(40, header.MinHeight);

        host.Workspace.OpenPath("Plan.canvas");
        host.Settle();
        Assert.DoesNotContain(ShownShell.Descendants(host.Shell).OfType<NoteHeaderView>(), view => view.IsVisible);
    });

    [Fact]
    public void TheHeaderHasNoHistoryButtons() => StaThread.RunPumped(() =>
    {
        using var host = new ShownShell((Nested, "# Lecture\n"));
        host.Workspace.OpenPath(Nested);
        host.Settle();

        string[] names = [.. ShownShell.Descendants(HeaderOf(host, Nested)).OfType<ButtonBase>().Select(AutomationProperties.GetName)];
        Assert.DoesNotContain("Back", names);
        Assert.DoesNotContain("Forward", names);
        Assert.Equal(["learning", "maths", "More note actions"], names.Where(name => name is not ("Source" or "Reading")));
    });

    /// <summary>A folder segment reveals that folder in Files, through the sidebar's reveal
    /// seam (the window's; here a recorder), and the note's name is text, not a stop, since
    /// the tab's name already says it.</summary>
    [Fact]
    public void AFolderSegmentRevealsItsFolder() => StaThread.RunPumped(() =>
    {
        using var host = new ShownShell((Nested, "# Lecture\n"));
        var revealed = new List<string>();
        host.Workspace.GraphRevealInSidebar = revealed.Add;
        host.Workspace.OpenPath(Nested);
        host.Settle();
        NoteHeaderView header = HeaderOf(host, Nested);

        Invoke(Segment(header, "maths"));
        Invoke(Segment(header, "learning"));
        host.Settle();

        Assert.Equal(["learning/maths", "learning"], revealed);
        TextBlock name = ShownShell.Descendants(header).OfType<TextBlock>().Single(text => text.Text == "Lecture");
        Assert.False(name.Focusable);
    });

    [Fact]
    public void ARevealShowsAHiddenSidebar() => StaThread.RunPumped(() =>
    {
        using var host = new ShownShell((Nested, "# Lecture\n"));
        bool shownFirst = false;
        host.Workspace.GraphRevealInSidebar = _ => shownFirst = host.Workspace.IsFilesSidebarVisible;
        host.Workspace.OpenPath(Nested);
        host.Workspace.IsFilesSidebarVisible = false;
        host.Settle();

        Invoke(Segment(HeaderOf(host, Nested), "learning"));
        host.Settle();

        Assert.True(host.Workspace.IsFilesSidebarVisible);
        Assert.True(shownFirst, "the folder was revealed before Files was shown.");
    });

    /// <summary>In a split, ⋯ in the inactive pane acts on that pane's note, reached through
    /// its peer, not a click, so focus never entered the pane.</summary>
    [Theory]
    [InlineData("NoteHeaderAddProperty")]
    [InlineData("NoteHeaderSplitRight")]
    public void AHeaderActionActsOnItsOwnNote(string row) => StaThread.RunPumped(() =>
    {
        using var host = Split(out WorkspaceGroupViewModel first, out WorkspaceGroupViewModel second);
        int groups = host.Workspace.Groups.Count;

        Button more = More(HeaderOf(host, "b.md"));
        Invoke(more);
        host.Settle();
        Invoke(MenuRow(more, row));
        host.Settle();

        if (row == "NoteHeaderAddProperty")
        {
            Assert.Equal("b.md", host.Workspace.AddPropertySheet?.IntentForTests?.Path);
        }
        else
        {
            Assert.Equal(groups + 1, host.Workspace.Groups.Count);
            Assert.Equal("b.md", host.Workspace.ActiveGroup.ActiveTab?.Path);
            Assert.DoesNotContain(host.Workspace.ActiveGroup, new[] { first, second });
        }
    });

    /// <summary>The switch names its own tab, so it flips that one and leaves the active
    /// pane and the keys where they were.</summary>
    [Fact]
    public void TheSwitchActsOnItsOwnTabAndLeavesTheActivePaneAlone() => StaThread.RunPumped(() =>
    {
        using var host = Split(out WorkspaceGroupViewModel first, out WorkspaceGroupViewModel second);
        IInputElement keys = Keyboard.FocusedElement;
        var persisted = new List<WorkspaceSnapshot>();
        host.Workspace.PersistedForTests = persisted.Add;

        Select(Choice(HeaderOf(host, "b.md"), "NoteHeaderReading"));
        host.Settle();

        Assert.True(second.ActiveTab!.IsReadingMode);
        Assert.False(first.ActiveTab!.IsReadingMode);
        Assert.Same(first, host.Workspace.ActiveGroup);
        Assert.Same(keys, Keyboard.FocusedElement);
        WorkspaceTabState saved = TabsIn(Assert.Single(persisted).Root).Single(tab => tab.Item.Path == "b.md");
        Assert.Equal("reading", saved.Mode);
    });

    /// <summary>DSD11: the bar's stops come before Properties and the editor, so ⋯ hands Tab
    /// on to the Properties header.</summary>
    [Fact]
    public void TheBarsStopsComeBeforeProperties() => StaThread.RunPumped(() =>
    {
        using var host = new ShownShell(("Lecture.md", "---\nkey: value\n---\n# Lecture\n"));
        host.Workspace.OpenPath("Lecture.md");
        host.Settle();
        NoteHeaderView header = HeaderOf(host, "Lecture.md");

        IInputElement? next = MoveFrom(More(header), FocusNavigationDirection.Next);

        Assert.True(IsInside(next as DependencyObject, Named(ContentOf(header), "PropertiesHeader")), $"Tab went from ⋯ to {next}.");
    });

    [Fact]
    public void TheMenusAndChordsStillFollowTheActivePane() => StaThread.RunPumped(() =>
    {
        using var host = Split(out WorkspaceGroupViewModel first, out WorkspaceGroupViewModel second);

        host.Workspace.ToggleReadingModeCommand.Execute(null);
        host.Settle();

        Assert.True(first.ActiveTab!.IsReadingMode);
        Assert.False(second.ActiveTab!.IsReadingMode);
        Assert.True(Choice(HeaderOf(host, "a.md"), "NoteHeaderReading").IsChecked);
        Assert.True(Choice(HeaderOf(host, "b.md"), "NoteHeaderSource").IsChecked);
    });

    /// <summary>An arrow moves the choice and leaves the keys on the switch, the mode
    /// persists, and nothing is posted: the radio's own focus speech says what happened.</summary>
    [Fact]
    public void AnArrowKeepsFocusInTheSwitch() => StaThread.RunPumped(() =>
    {
        using var host = new ShownShell(("Lecture.md", "# Lecture\n"));
        host.Workspace.OpenPath("Lecture.md");
        host.Settle();
        NoteHeaderView header = HeaderOf(host, "Lecture.md");
        RadioButton source = Choice(header, "NoteHeaderSource");
        RadioButton reading = Choice(header, "NoteHeaderReading");
        Assert.True(source.Focus());
        host.Announced.Clear();

        foreach ((Key key, RadioButton expected, bool readingMode) in new[]
        {
            (Key.Right, reading, true), (Key.Left, source, false), (Key.Down, reading, true), (Key.Up, source, false),
        })
        {
            Arrow(source.IsKeyboardFocused ? source : reading, key);
            host.Settle();
            Assert.True(expected.IsChecked);
            Assert.True(expected.IsKeyboardFocused, $"{key} left the keys on {Keyboard.FocusedElement}.");
            Assert.Equal(readingMode, host.Workspace.ActiveGroup.ActiveTab!.IsReadingMode);
        }

        Assert.Empty(host.Announced);
        Assert.Equal(More(header), MoveFrom(source, FocusNavigationDirection.Next));
    });

    /// <summary>Ctrl+Shift+E keeps R-10's landing: the keys go into the note, both ways, the
    /// switch follows, and it's one flip.</summary>
    [Fact]
    public void CtrlShiftEStillLandsInTheNote() => StaThread.RunPumped(() =>
    {
        using var host = new ShownShell(("Lecture.md", "# Lecture\n"));
        host.Workspace.OpenPath("Lecture.md");
        host.Settle();
        NoteHeaderView header = HeaderOf(host, "Lecture.md");
        int landings = 0;
        host.Workspace.EditorPaneFocusRequested += (_, _) => landings++;

        var persisted = new List<WorkspaceSnapshot>();
        host.Workspace.PersistedForTests = persisted.Add;

        host.Workspace.ToggleReadingModeCommand.Execute(null);
        host.Settle();
        Assert.True(host.Workspace.ActiveGroup.ActiveTab!.IsReadingMode);
        Assert.True(Choice(header, "NoteHeaderReading").IsChecked);
        Assert.Equal(1, landings);
        Assert.Equal("reading", TabsIn(Assert.Single(persisted).Root).Single().Mode);

        host.Workspace.ToggleReadingModeCommand.Execute(null);
        host.Settle();
        Assert.False(host.Workspace.ActiveGroup.ActiveTab!.IsReadingMode);
        Assert.True(Choice(header, "NoteHeaderSource").IsChecked);
        Assert.Equal(2, landings);
    });

    /// <summary>Tab reaches the checked option only.</summary>
    [Fact]
    public void TheSwitchHasOneStop() => StaThread.RunPumped(() =>
    {
        using var host = new ShownShell((Nested, "# Lecture\n"));
        host.Workspace.OpenPath(Nested);
        host.Settle();
        NoteHeaderView header = HeaderOf(host, Nested);
        Button maths = Segment(header, "maths");

        Assert.Equal(Choice(header, "NoteHeaderSource"), MoveFrom(maths, FocusNavigationDirection.Next));

        host.Workspace.ToggleReadingModeCommand.Execute(null);
        host.Settle();
        Assert.Equal(Choice(header, "NoteHeaderReading"), MoveFrom(maths, FocusNavigationDirection.Next));
    });

    /// <summary>A flip under the keys lands them in the note: a UIA Select can flip the
    /// surface the keys are on, and they mustn't stay inside the hidden one.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AFlipUnderTheKeysLandsThemInTheNote(bool fromReading) => StaThread.RunPumped(() =>
    {
        using var host = new ShownShell(("Lecture.md", "# Lecture\n\nSee [[Other]].\n"), ("Other.md", "# Other\n"));
        host.Workspace.OpenPath("Lecture.md");
        host.Settle();
        NoteHeaderView header = HeaderOf(host, "Lecture.md");
        FrameworkElement content = ContentOf(header);
        if (fromReading)
        {
            host.Workspace.ToggleReadingModeCommand.Execute(null);
            host.Settle();
            System.Windows.Documents.Hyperlink link = ReadingLinks(content).First();
            Assert.True(((IInputElement)link).Focus());
        }
        else
        {
            Assert.True(EditorOf(content).Focus());
        }

        FrameworkElement leaving = fromReading ? ReadingSurfaceOf(content) : EditorHostOf(content);
        Select(Choice(header, fromReading ? "NoteHeaderSource" : "NoteHeaderReading"));
        host.Settle();

        Assert.Equal(!fromReading, host.Workspace.ActiveGroup.ActiveTab!.IsReadingMode);
        Assert.False(IsInside(Keyboard.FocusedElement as DependencyObject, leaving), $"the keys stayed in the hidden surface, on {Keyboard.FocusedElement}.");
        Assert.True(IsInside(Keyboard.FocusedElement as DependencyObject, content), $"the keys left the note for {Keyboard.FocusedElement}.");
    });

    [Theory]
    [InlineData("FilesTree")]
    [InlineData("PropertiesHeader")]
    public void AFlipWithTheKeysElsewhereLeavesThemThere(string where) => StaThread.RunPumped(() =>
    {
        using var host = new ShownShell(("Lecture.md", "---\nkey: value\n---\n# Lecture\n"));
        host.Workspace.OpenPath("Lecture.md");
        host.Settle();
        NoteHeaderView header = HeaderOf(host, "Lecture.md");
        FrameworkElement target = where == "FilesTree" ? host.Body("FilesTree") : Named(ContentOf(header), "PropertiesHeader");
        Assert.True(target.Focus() || target.IsKeyboardFocusWithin);
        IInputElement keys = Keyboard.FocusedElement;

        Select(Choice(header, "NoteHeaderReading"));
        host.Settle();

        Assert.True(host.Workspace.ActiveGroup.ActiveTab!.IsReadingMode);
        Assert.Same(keys, Keyboard.FocusedElement);
    });

    [Fact]
    public void TheMoreMenuIsTheSameCommands() => StaThread.RunPumped(() =>
    {
        using var host = new ShownShell(("Lecture.md", "# Lecture\n"));
        host.Workspace.OpenPath("Lecture.md");
        host.Settle();
        Button more = More(HeaderOf(host, "Lecture.md"));

        Assert.Equal("More note actions", AutomationProperties.GetName(more));
        Assert.Same(host.Workspace.SplitRightCommand, MenuRow(more, "NoteHeaderSplitRight").Command);
        Assert.Same(host.Workspace.OpenAddPropertySheetCommand, MenuRow(more, "NoteHeaderAddProperty").Command);
        Assert.Same(host.Workspace.OpenBulkRenameSheetCommand, MenuRow(more, "NoteHeaderBulkRename").Command);
        Assert.Equal(
            ["Split Right", "Add Property…", "Bulk Rename Properties…"],
            more.ContextMenu!.Items.OfType<MenuItem>().Select(item => AutomationProperties.GetName(item)));
        Assert.False(string.IsNullOrEmpty(MenuRow(more, "NoteHeaderSplitRight").InputGestureText));
        Assert.False(string.IsNullOrEmpty(MenuRow(more, "NoteHeaderBulkRename").InputGestureText));
    });

    /// <summary>Below 520 the switch drops its labels for tooltips, and the folders collapse
    /// into a "…" button with a Folders menu.</summary>
    [Fact]
    public void BelowFiveHundredTwentyTheHeaderCompacts() => StaThread.RunPumped(() =>
    {
        using var host = new ShownShell((Nested, "# Lecture\n"));
        host.Workspace.OpenPath(Nested);
        host.Settle();
        NoteHeaderView header = HeaderOf(host, Nested);
        Assert.False(header.IsCompact);
        RadioButton reading = Choice(header, "NoteHeaderReading");
        Assert.Contains("Reading", ShownText(reading));

        host.Shell.Width = 500;
        host.Settle();

        Assert.True(header.ActualWidth < NoteHeaderView.CompactBelow);
        Assert.True(header.IsCompact);
        Assert.DoesNotContain("Reading", ShownText(reading));
        Assert.Equal("Reading", AutomationProperties.GetName(reading));
        Assert.NotNull(reading.ToolTip);
        Assert.DoesNotContain(ShownShell.Descendants(header).OfType<Button>(), button => button.IsVisible && AutomationProperties.GetName(button) == "maths");
        Button folders = ShownShell.Descendants(header).OfType<Button>().Single(button => button.IsVisible && AutomationProperties.GetAutomationId(button) == "NoteHeaderFolders");
        Assert.Equal("Folders", AutomationProperties.GetName(folders));
        Assert.Equal(["learning", "learning/maths"], folders.ContextMenu!.Items.OfType<MenuItem>().Select(item => AutomationProperties.GetName(item)));
    });

    /// <summary>The path rebuilds when the bar goes compact or comes back, and when the note
    /// moves. With the keys on a segment, they stay on the rebuilt path, not on nothing
    /// (<c>NoteHeaderView.ShowPath</c>, the focus manifest's witness).</summary>
    [Fact]
    public void ARebuiltPathKeepsTheKeysOnIt() => StaThread.RunPumped(() =>
    {
        using var host = new ShownShell((Nested, "# Lecture\n"));
        host.Workspace.OpenPath(Nested);
        host.Settle();
        NoteHeaderView header = HeaderOf(host, Nested);
        Assert.True(Segment(header, "maths").Focus());

        host.Shell.Width = 500;
        host.Settle();
        Assert.True(header.IsCompact);
        Assert.Equal("NoteHeaderFolders", AutomationProperties.GetAutomationId((DependencyObject)Keyboard.FocusedElement));

        host.Shell.Width = 1200;
        host.Settle();
        Assert.False(header.IsCompact);
        Assert.Equal("NoteHeaderFolder", AutomationProperties.GetAutomationId((DependencyObject)Keyboard.FocusedElement));
        Assert.True(IsInside(Keyboard.FocusedElement as DependencyObject, header));
    });

    /// <summary>A header out of the tree lets go of its tab, so a tab that lives on (moved to
    /// another pane, say) doesn't keep it, and it follows the tab again when it's back.</summary>
    [Fact]
    public void AnUnloadedHeaderLetsGoOfItsTab() => StaThread.RunPumped(() =>
    {
        using var host = new ShownShell(("Lecture.md", "# Lecture\n"));
        host.Workspace.OpenPath("Lecture.md");
        host.Settle();
        NoteHeaderView header = HeaderOf(host, "Lecture.md");
        var tab = (WorkspaceTabViewModel)header.DataContext;
        var content = (Panel)header.Parent;
        Assert.Contains(Listeners(tab), target => ReferenceEquals(target, header));

        content.Children.Remove(header);
        host.Settle();
        Assert.DoesNotContain(Listeners(tab), target => ReferenceEquals(target, header));

        content.Children.Insert(0, header);
        host.Settle();
        Assert.Contains(Listeners(tab), target => ReferenceEquals(target, header));
        host.Workspace.ToggleReadingModeCommand.Execute(null);
        host.Settle();
        Assert.True(Choice(header, "NoteHeaderReading").IsChecked);
    });

    private static object?[] Listeners(System.ComponentModel.INotifyPropertyChanged source)
    {
        for (Type? type = source.GetType(); type is not null; type = type.BaseType)
        {
            if (type.GetField("PropertyChanged", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    ?.GetValue(source) is Delegate handlers)
            {
                return [.. handlers.GetInvocationList().Select(handler => handler.Target)];
            }
        }

        return [];
    }

    [Fact]
    public void F6StillLandsInTheEditor() => StaThread.RunPumped(() =>
    {
        using var host = new ShownShell((Nested, "# Lecture\n"));
        host.Workspace.OpenPath(Nested);
        host.Settle();

        Assert.True(host.Land(ShellRegionKind.Editor));
        Assert.False(IsInside(Keyboard.FocusedElement as DependencyObject, HeaderOf(host, Nested)));
        Assert.True(IsInside(Keyboard.FocusedElement as DependencyObject, EditorHostOf(ContentOf(HeaderOf(host, Nested)))));
    });

    // --- Helpers ------------------------------------------------------------------------

    private static IEnumerable<WorkspaceTabState> TabsIn(WorkspaceNodeState node) => node switch
    {
        WorkspaceGroupState group => group.Tabs,
        WorkspaceSplitState split => split.Children.SelectMany(TabsIn),
        _ => [],
    };

    private static ShownShell Split(out WorkspaceGroupViewModel first, out WorkspaceGroupViewModel second)
    {
        var host = new ShownShell(("a.md", "# A\n"), ("b.md", "# B\n"));
        host.Workspace.OpenPath("a.md");
        host.Workspace.OpenPath("b.md", WorkspaceOpenTarget.SplitRight);
        host.Settle();
        first = host.Workspace.Groups.First(group => group.ActiveTab?.Path == "a.md");
        second = host.Workspace.Groups.First(group => group.ActiveTab?.Path == "b.md");
        Assert.True(EditorOf(ContentOf(HeaderOf(host, "a.md"))).Focus());
        host.Settle();
        Assert.Same(first, host.Workspace.ActiveGroup);
        return host;
    }

    private static NoteHeaderView HeaderOf(ShownShell host, string path) =>
        ShownShell.Descendants(host.Shell).OfType<NoteHeaderView>()
            .Single(view => view.IsVisible && view.DataContext is WorkspaceTabViewModel tab && tab.Path == path);

    private static FrameworkElement ContentOf(NoteHeaderView header) => (FrameworkElement)header.Parent;

    private static UIElement EditorOf(FrameworkElement content) =>
        ShownShell.Descendants(content).OfType<ICSharpCode.AvalonEdit.Editing.TextArea>().First(area => area.IsVisible);

    private static FrameworkElement EditorHostOf(FrameworkElement content) =>
        ShownShell.Descendants(content).OfType<ICSharpCode.AvalonEdit.TextEditor>().First();

    private static RichTextBox ReadingSurfaceOf(FrameworkElement content) =>
        ShownShell.Descendants(content).OfType<SlateWindows.Reading.ReadingSurface>().First();

    private static IEnumerable<System.Windows.Documents.Hyperlink> ReadingLinks(FrameworkElement content) =>
        ReadingSurfaceOf(content).Document.Blocks.SelectMany(Inlines).OfType<System.Windows.Documents.Hyperlink>();

    private static IEnumerable<System.Windows.Documents.Inline> Inlines(System.Windows.Documents.Block block) => block switch
    {
        System.Windows.Documents.Paragraph paragraph => paragraph.Inlines.SelectMany(Flatten),
        System.Windows.Documents.Section section => section.Blocks.SelectMany(Inlines),
        System.Windows.Documents.List list => list.ListItems.SelectMany(item => item.Blocks).SelectMany(Inlines),
        _ => [],
    };

    private static IEnumerable<System.Windows.Documents.Inline> Flatten(System.Windows.Documents.Inline inline) =>
        inline is System.Windows.Documents.Span span ? new[] { inline }.Concat(span.Inlines.SelectMany(Flatten)) : [inline];

    private static FrameworkElement Named(DependencyObject root, string id) =>
        ShownShell.Descendants(root).OfType<FrameworkElement>().First(element => AutomationProperties.GetAutomationId(element) == id);

    private static Button Segment(NoteHeaderView header, string name) =>
        ShownShell.Descendants(header).OfType<Button>().Single(button => button.IsVisible && AutomationProperties.GetName(button) == name);

    private static RadioButton Choice(NoteHeaderView header, string id) =>
        ShownShell.Descendants(header).OfType<RadioButton>().Single(radio => AutomationProperties.GetAutomationId(radio) == id);

    private static Button More(NoteHeaderView header) =>
        ShownShell.Descendants(header).OfType<Button>().Single(button => AutomationProperties.GetAutomationId(button) == "NoteHeaderMore");

    private static MenuItem MenuRow(Button more, string id) =>
        more.ContextMenu!.Items.OfType<MenuItem>().Single(item => AutomationProperties.GetAutomationId(item) == id);

    private static void Invoke(UIElement element) =>
        ((IInvokeProvider)UIElementAutomationPeer.CreatePeerForElement(element).GetPattern(PatternInterface.Invoke)).Invoke();

    private static void Select(UIElement element) =>
        ((ISelectionItemProvider)UIElementAutomationPeer.CreatePeerForElement(element).GetPattern(PatternInterface.SelectionItem)).Select();

    private static void Arrow(UIElement target, Key key)
    {
        var args = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(target)!, 0, key)
        {
            RoutedEvent = Keyboard.PreviewKeyDownEvent,
        };
        target.RaiseEvent(args);
        if (!args.Handled)
        {
            args.RoutedEvent = Keyboard.KeyDownEvent;
            target.RaiseEvent(args);
        }
    }

    private static IInputElement? MoveFrom(UIElement from, FocusNavigationDirection direction)
    {
        Assert.True(from.Focus());
        from.MoveFocus(new TraversalRequest(direction));
        return Keyboard.FocusedElement;
    }

    private static string[] ShownText(DependencyObject root) =>
        [.. ShownShell.Descendants(root).OfType<TextBlock>().Where(text => text.IsVisible).Select(text => text.Text)];

    private static bool IsInside(DependencyObject? element, DependencyObject ancestor)
    {
        for (DependencyObject? node = element; node is not null; node = node is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D
                 ? System.Windows.Media.VisualTreeHelper.GetParent(node)
                 : LogicalTreeHelper.GetParent(node))
        {
            if (ReferenceEquals(node, ancestor))
            {
                return true;
            }
        }

        return false;
    }
}
