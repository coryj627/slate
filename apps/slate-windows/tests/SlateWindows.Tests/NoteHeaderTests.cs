// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
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
        using var host = Roomy((Nested, "# Lecture\n"));
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
        using var host = Roomy((Nested, "# Lecture\n"));
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

    /// <summary>A folder name a path repeats (notes/notes) tells its segments apart by their
    /// paths, the shell's sibling rule (R-4), so a reader can tell where each goes and no two
    /// sibling buttons share a name; a name the path holds once stays the folder's name.</summary>
    [Fact]
    public void RepeatedFolderNamesAreToldApart() => StaThread.RunPumped(() =>
    {
        using var host = Roomy(("notes/notes/topics/Note.md", "# Note\n"));
        var revealed = new List<string>();
        host.Workspace.GraphRevealInSidebar = revealed.Add;
        host.Workspace.OpenPath("notes/notes/topics/Note.md");
        host.Settle();
        NoteHeaderView header = HeaderOf(host, "notes/notes/topics/Note.md");

        Button[] segments = [.. ShownShell.Descendants(header).OfType<Button>().Where(button => AutomationProperties.GetAutomationId(button) == "NoteHeaderFolder")];
        Assert.Equal(["notes, notes", "notes, notes/notes", "topics"], segments.Select(AutomationProperties.GetName));
        Assert.Equal(["notes", "notes", "topics"], segments.Select(button => ((TextBlock)button.Content).Text));

        // The told-apart name shows on hover, from the label, so it isn't heard twice.
        Assert.Equal("notes, notes/notes", ((TextBlock)segments[1].Content).ToolTip);
        Assert.All(segments, segment => Assert.Equal(string.Empty, HelpTextOf(segment)));
        Invoke(segments[1]);
        host.Settle();
        Assert.Equal(["notes/notes"], revealed);
    });

    /// <summary>No two stops in the bar sound alike: a folder named like one of the bar's own
    /// controls, or like another folder but for its case, is told apart by the shell's
    /// sibling rule, and the controls keep their own names.</summary>
    [Theory]
    [InlineData("More note actions/Note.md")]
    [InlineData("Source/Reading/Note.md")]
    [InlineData("notes/Notes/Note.md")]
    public void NoTwoStopsInTheBarSoundAlike(string path) => StaThread.RunPumped(() =>
    {
        using var host = new ShownShell((path, "# Note\n"));
        host.Workspace.OpenPath(path);
        host.Settle();
        NoteHeaderView header = HeaderOf(host, path);

        string[] names = [.. ShownShell.Descendants(header).OfType<ButtonBase>().Where(button => button.IsVisible).Select(AutomationProperties.GetName)];
        Assert.Equal(names.Length, names.Select(name => SpeechKey.Of(name)).Distinct().Count());
        Assert.Contains("More note actions", names);
        Assert.Contains("Source", names);
        Assert.Contains("Reading", names);
    });

    /// <summary>The compact Folders menu's rows follow the same sibling rule: no two sound
    /// alike, a path whose words are another's (notes/_) included.</summary>
    [Theory]
    [InlineData("notes/_/Note.md")]
    [InlineData("notes/notes/Note.md")]
    [InlineData("notes/Notes/Note.md")]
    public void CompactFolderRowsDontSoundAlike(string path) => StaThread.RunPumped(() =>
    {
        using var host = new ShownShell((path, "# Note\n"));
        host.Workspace.OpenPath(path);
        host.Shell.Width = 500;
        host.Settle();
        NoteHeaderView header = HeaderOf(host, path);
        Assert.True(header.IsCompact);

        Button folders = ShownShell.Descendants(header).OfType<Button>().Single(button => AutomationProperties.GetAutomationId(button) == "NoteHeaderFolders");
        string[] names = [.. folders.ContextMenu!.Items.OfType<MenuItem>().Select(item => AutomationProperties.GetName(item))];
        Assert.Equal(2, names.Length);
        Assert.Equal(2, names.Select(name => SpeechKey.Of(name)).Distinct().Count());
    });

    /// <summary>Folders that don't fit collapse into "…", as when compact, so no stop is clipped
    /// out of sight; once the bar is wide enough they come back.</summary>
    [Fact]
    public void FoldersThatDontFitCollapseRatherThanHideStops() => StaThread.RunPumped(() =>
    {
        // Too long for a 560 bar, short enough for the shell's whole width: a window can't be
        // wider than its screen, and a CI VM's is small, so the bar is sized, not the window.
        string longFolder = string.Concat(Enumerable.Repeat("long-folder-name-", 3));
        string path = $"{longFolder}/middle/last/Note.md";
        using var host = Roomy((path, "# Note\n"));
        host.Workspace.OpenPath(path);
        host.Settle();
        NoteHeaderView header = HeaderOf(host, path);
        header.Width = 560;
        host.Settle();
        host.Settle();
        Assert.False(header.IsCompact);

        Assert.Empty(VisibleSegments(header));
        Assert.Single(ShownShell.Descendants(header).OfType<Button>(), button => button.IsVisible && AutomationProperties.GetAutomationId(button) == "NoteHeaderFolders");
        AssertEveryStopIsOnScreen(header);

        header.Width = double.NaN;
        host.Workspace.IsFilesSidebarVisible = false;
        host.Settle();
        host.Settle();
        Assert.Equal(3, VisibleSegments(header).Length);
        AssertEveryStopIsOnScreen(header);
    });

    /// <summary>A path that fits gets its segments back at once: the collapse belongs to the
    /// folders that didn't fit, not to the bar.</summary>
    [Fact]
    public void AShorterPathGetsItsSegmentsBack() => StaThread.RunPumped(() =>
    {
        string path = $"{string.Concat(Enumerable.Repeat("long-folder-name-", 12))}/last/Note.md";
        using var host = Roomy((path, "# Note\n"));
        host.Workspace.OpenPath(path);
        host.Settle();
        host.Settle();
        NoteHeaderView header = HeaderOf(host, path);
        Assert.Empty(VisibleSegments(header));

        ((WorkspaceTabViewModel)header.DataContext).RetargetPath("learning/maths/Note.md");
        host.Settle();
        host.Settle();
        Assert.Equal(["learning", "maths"], VisibleSegments(header).Select(AutomationProperties.GetName));
        AssertEveryStopIsOnScreen(header);
    });

    /// <summary>The folders give way before the name does (the design's order): a long name
    /// collapses them into "…", which keeps its whole width, and the name is what's cut.</summary>
    [Theory]
    [InlineData(1200)]
    [InlineData(500)]
    public void ALongNameNeverSqueezesTheFolders(double shellWidth) => StaThread.RunPumped(() =>
    {
        string path = $"folder/{string.Concat(Enumerable.Repeat("long-note-name-", 12))}.md";
        using var host = new ShownShell((path, "# Note\n"));
        host.Workspace.OpenPath(path);
        host.Shell.Width = shellWidth;
        host.Settle();
        host.Settle();
        NoteHeaderView header = HeaderOf(host, path);

        Assert.Empty(VisibleSegments(header));
        Assert.Single(ShownShell.Descendants(header).OfType<Button>(), button => button.IsVisible && AutomationProperties.GetAutomationId(button) == "NoteHeaderFolders");
        AssertEveryStopIsOnScreen(header);
    });

    /// <summary>A pane can be narrower than the bar's stops (a split has no floor), so they give
    /// way when one can't be whole: the folders first, then ⋯, then the switch. Each comes back
    /// when there's room.</summary>
    [Fact]
    public void ANarrowPaneShowsOnlyWholeStops() => StaThread.RunPumped(() =>
    {
        using var host = new ShownShell(("a/a.md", "# A\n"), ("a/b.md", "# B\n"));
        host.Workspace.OpenPath("a/a.md");
        host.Workspace.OpenPath("a/b.md", WorkspaceOpenTarget.SplitRight);
        host.Shell.Width = 500;
        host.Settle();
        host.Settle();
        foreach (string path in new[] { "a/a.md", "a/b.md" })
        {
            AssertStops(HeaderOf(host, path), folders: false, more: true, switcher: true);
        }

        host.Shell.Width = 3200;
        host.Settle();
        host.Settle();
        foreach (string path in new[] { "a/a.md", "a/b.md" })
        {
            AssertStops(HeaderOf(host, path), folders: true, more: true, switcher: true);
        }
    });

    /// <summary>Narrower still, ⋯ goes, then the switch, and they come back in turn. Keys on a
    /// stop that gives way go to the bar's first stop that stays, the switch's checked option,
    /// else into the note: never onto nothing.</summary>
    [Fact]
    public void AStopThatGivesWayHandsOnTheKeys() => StaThread.RunPumped(() =>
    {
        using var host = new ShownShell((Nested, "# Lecture\n"));
        host.Workspace.OpenPath(Nested);
        host.Settle();
        NoteHeaderView header = HeaderOf(host, Nested);
        header.Width = 500;
        host.Settle();
        host.Settle();
        RadioButton source = Choice(header, "NoteHeaderSource");
        double switcher = ((FrameworkElement)VisualTreeHelper.GetParent(VisualTreeHelper.GetParent(source))).DesiredSize.Width;
        Button more = ShownShell.Descendants(header).OfType<Button>().Single(button => AutomationProperties.GetAutomationId(button) == "NoteHeaderMore");
        double moreWidth = more.DesiredSize.Width;

        Button folders = ShownShell.Descendants(header).OfType<Button>().Single(button => button.IsVisible && AutomationProperties.GetAutomationId(button) == "NoteHeaderFolders");
        Assert.True(folders.Focus());
        header.Width = switcher + moreWidth + 4;
        host.Settle();
        host.Settle();
        AssertStops(header, folders: false, more: true, switcher: true);
        Assert.Same(source, Keyboard.FocusedElement);

        Assert.True(more.Focus());
        header.Width = switcher + 4;
        host.Settle();
        host.Settle();
        AssertStops(header, folders: false, more: false, switcher: true);
        Assert.Same(source, Keyboard.FocusedElement);

        header.Width = switcher / 2;
        host.Settle();
        host.Settle();
        AssertStops(header, folders: false, more: false, switcher: false);
        Assert.True(IsInside(Keyboard.FocusedElement as DependencyObject, EditorHostOf(ContentOf(header))), $"The keys are on {Keyboard.FocusedElement}.");

        header.Width = switcher + 4;
        host.Settle();
        host.Settle();
        AssertStops(header, folders: false, more: false, switcher: true);
        header.Width = double.NaN;
        host.Settle();
        host.Settle();
        AssertStops(header, folders: true, more: true, switcher: true);
    });

    private static void AssertStops(NoteHeaderView header, bool folders, bool more, bool switcher)
    {
        ButtonBase[] shown = [.. ShownShell.Descendants(header).OfType<ButtonBase>().Where(button => button.IsVisible)];
        string said = $"A {header.ActualWidth:F0} bar shows {string.Join(", ", shown.Select(AutomationProperties.GetAutomationId))}.";
        Assert.True(folders == shown.Any(button => AutomationProperties.GetAutomationId(button) is "NoteHeaderFolder" or "NoteHeaderFolders"), said);
        Assert.True(more == shown.Any(button => AutomationProperties.GetAutomationId(button) == "NoteHeaderMore"), said);
        Assert.True(switcher == shown.Any(button => AutomationProperties.GetAutomationId(button) is "NoteHeaderSource" or "NoteHeaderReading"), said);
        AssertEveryStopIsOnScreen(header);
    }

    /// <summary>A shell whose bar isn't compact on any screen. A window can't be wider than its
    /// screen, and on a CI VM's, Files and the right pane leave the editor under 520 (the bar's
    /// compact width), so the right pane is put aside.</summary>
    private static ShownShell Roomy(params (string Path, string Text)[] notes)
    {
        var host = new ShownShell(notes);
        host.Workspace.IsRightPaneVisible = false;
        return host;
    }

    /// <summary>Whether a menu ⋯ opened without the mouse landed on its first row. A desktop
    /// whose session has no foreground window (the Namespace runners, #1680) doesn't let a
    /// menu's popup keep the keys, and WPF hands them back to ⋯: there the fact asserts that
    /// refusal and has nothing further to claim, as CanvasNavigatorTests' TryPutTheKeysInTheMenu
    /// does. A desktop that lets a row take the keys must have landed them there.</summary>
    private static bool LandedOnTheFirstRow(Button more)
    {
        MenuItem first = MenuRow(more, "NoteHeaderSplitRight");
        if (ReferenceEquals(first, Keyboard.FocusedElement))
        {
            return true;
        }

        Assert.False(first.Focus(), "the menu didn't land on its first row, though this desktop lets a row take the keys");
        Assert.Same(more, Keyboard.FocusedElement);
        more.ContextMenu!.IsOpen = false;
        return false;
    }

    private static Button[] VisibleSegments(NoteHeaderView header) =>
        [.. ShownShell.Descendants(header).OfType<Button>().Where(button => button.IsVisible && AutomationProperties.GetAutomationId(button) == "NoteHeaderFolder")];

    /// <summary>Every stop in the bar is whole inside the bar and inside each clip on its way
    /// up, its own included: an element's bounds when it clips to them, and the layout clip an
    /// element gets when it's arranged smaller than it wants. So none is a stop a reader can
    /// Tab to and nobody can see.</summary>
    private static void AssertEveryStopIsOnScreen(NoteHeaderView header)
    {
        foreach (ButtonBase stop in ShownShell.Descendants(header).OfType<ButtonBase>().Where(button => button.IsVisible))
        {
            Rect box = stop.TransformToAncestor(header).TransformBounds(new Rect(stop.RenderSize));
            Assert.True(box.Right <= header.ActualWidth + 0.5 && box.Left >= -0.5, $"{AutomationProperties.GetName(stop)} is at {box} in a {header.ActualWidth:F0} bar.");
            for (DependencyObject? at = stop; at is not null && at != header; at = VisualTreeHelper.GetParent(at))
            {
                if (at is not FrameworkElement element)
                {
                    continue;
                }

                Rect? clip = LayoutInformation.GetLayoutClip(element)?.Bounds ?? (element.ClipToBounds ? new Rect(element.RenderSize) : null);
                if (clip is { } bounds)
                {
                    Rect shown = element.TransformToAncestor(header).TransformBounds(bounds);
                    Assert.True(box.Left >= shown.Left - 0.5 && box.Right <= shown.Right + 0.5, $"{AutomationProperties.GetName(stop)} is at {box}, clipped to {shown} by a {element.GetType().Name}.");
                }
            }
        }
    }

    /// <summary>A rename the watcher brings keeps what didn't change: the note's own rename
    /// leaves the folders, and the keys, where they were; a folder's rename rebuilds them and
    /// keeps the keys on the segment in the same place.</summary>
    [Fact]
    public void ARenameKeepsTheKeysOnTheirFolder() => StaThread.RunPumped(() =>
    {
        using var host = Roomy((Nested, "# Lecture\n"));
        host.Workspace.OpenPath(Nested);
        host.Settle();
        NoteHeaderView header = HeaderOf(host, Nested);
        var tab = (WorkspaceTabViewModel)header.DataContext;
        Button maths = Segment(header, "maths");
        Assert.True(maths.Focus());

        tab.RetargetPath("learning/maths/Lecture two.md");
        host.Settle();
        Assert.Same(maths, Keyboard.FocusedElement);
        Assert.Contains(ShownShell.Descendants(header).OfType<TextBlock>(), text => text.Text == "Lecture two");

        tab.RetargetPath("learning/algebra/Lecture two.md");
        host.Settle();
        Assert.Equal("algebra", AutomationProperties.GetName((DependencyObject)Keyboard.FocusedElement));
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

    /// <summary>#1793: in a split, the Properties header's buttons in the inactive pane act on
    /// that pane's note when invoked through their peers, so focus never entered the pane.</summary>
    [Theory]
    [InlineData("PropertiesAddButton")]
    [InlineData("PropertiesBulkRenameButton")]
    public void APropertiesHeaderButtonActsOnItsOwnNote(string id) => StaThread.RunPumped(() =>
    {
        using var host = Split(out _, out WorkspaceGroupViewModel second);

        Invoke(Named(ContentOf(HeaderOf(host, "b.md")), id));
        host.Settle();

        Assert.Same(second, host.Workspace.ActiveGroup);
        if (id == "PropertiesAddButton")
        {
            Assert.Equal("b.md", host.Workspace.AddPropertySheet?.IntentForTests?.Path);
        }
        else
        {
            Assert.NotNull(host.Workspace.BulkRenameSheet);
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
        using var host = Roomy((Nested, "# Lecture\n"));
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
            System.Windows.Documents.Hyperlink link = FirstReadingLink(host, content);
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
        // #1884: a reading view still projecting holds the landing, the keys
        // waiting on the tab's item, so the fact waits for it to land.
        Assert.True(
            PumpedDispatcher.PumpUntil(
                () => IsInside(Keyboard.FocusedElement as DependencyObject, content), TimeSpan.FromSeconds(10)),
            $"the keys left the note for {Keyboard.FocusedElement}.");
    });

    /// <summary>#1884: a flip from the editor whose reading view is still
    /// projecting (the reader edited the note since its last visit, and the
    /// fetch is slow) holds the landing until the projection arrives: the keys
    /// leave the hidden editor at once and land in the reading view when it
    /// does. A fact that looks once, before the projection, finds them
    /// waiting.</summary>
    [Fact]
    public void AFlipFromTheEditorWhileTheReadingViewProjectsLandsWhenItArrives() => StaThread.RunPumped(() =>
    {
        using var host = new ShownShell(("Lecture.md", "# Lecture\n\nSee [[Other]].\n"), ("Other.md", "# Other\n"));
        host.Workspace.OpenPath("Lecture.md");
        host.Settle();
        NoteHeaderView header = HeaderOf(host, "Lecture.md");
        FrameworkElement content = ContentOf(header);
        WorkspaceTabViewModel tab = host.Workspace.ActiveGroup.ActiveTab!;
        // A first visit makes the reading view; an edit makes the next one project again.
        host.Workspace.ToggleReadingModeCommand.Execute(null);
        _ = FirstReadingLink(host, content);
        host.Workspace.ToggleReadingModeCommand.Execute(null);
        host.Settle();
        tab.Text += "\nAn edit since the last visit.\n";
        using var gate = new ManualResetEventSlim(false);
        tab.Reading!.FetchFaultForTests = () =>
        {
            _ = gate.Wait(TimeSpan.FromSeconds(30));
            return null;
        };
        Assert.True(EditorOf(content).Focus());
        FrameworkElement leaving = EditorHostOf(content);
        try
        {
            Select(Choice(header, "NoteHeaderReading"));
            host.Settle();

            Assert.True(tab.IsReadingMode);
            Assert.True(tab.Reading.RefreshInFlight, "premise: the reading view is still projecting");
            Assert.False(IsInside(Keyboard.FocusedElement as DependencyObject, leaving), $"the keys stayed in the hidden editor, on {Keyboard.FocusedElement}.");
            // They wait on the tab's own item, as the CI failure found them
            // (the landing's park while the reading view projects).
            _ = Assert.IsAssignableFrom<TabItem>(Keyboard.FocusedElement);
            string waiting = Keyboard.FocusedElement?.ToString() ?? "nothing";

            gate.Set();
            RichTextBox surface = ReadingSurfaceOf(content);
            Assert.True(
                PumpedDispatcher.PumpUntil(
                    () => IsInside(Keyboard.FocusedElement as DependencyObject, surface),
                    TimeSpan.FromSeconds(10)),
                $"the keys waited on {waiting} and never landed in the reading view; they are on {Keyboard.FocusedElement}.");
        }
        finally
        {
            gate.Set();
            tab.Reading.FetchFaultForTests = null;
        }
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

    /// <summary>A row says its chord as the menus' rows do: NVDA reads a menu item's
    /// AcceleratorKey, not the gesture text it shows (the NVDA pass heard "Split Right, 1 of 3").</summary>
    [Fact]
    public void TheMoreMenusRowsSayTheirChords() => StaThread.RunPumped(() =>
    {
        using var host = new ShownShell(("Lecture.md", "# Lecture\n"));
        host.Workspace.OpenPath("Lecture.md");
        host.Settle();
        Button more = More(HeaderOf(host, "Lecture.md"));

        foreach (MenuItem row in more.ContextMenu!.Items.OfType<MenuItem>().Where(row => row.InputGestureText.Length > 0))
        {
            Assert.Equal(row.InputGestureText, AutomationProperties.GetAcceleratorKey(row));
        }

        Assert.Contains(more.ContextMenu.Items.OfType<MenuItem>(), row => row.InputGestureText.Length > 0);
    });

    /// <summary>⋯ opened by the keyboard or through its Invoke pattern lands on its first row,
    /// as a Windows menu does, so a reader hears a row rather than "menu".</summary>
    [Fact]
    public void MoreOpenedWithoutTheMouseLandsOnItsFirstRow() => StaThread.RunPumped(() =>
    {
        using var host = new ShownShell(("Lecture.md", "# Lecture\n"));
        host.Workspace.OpenPath("Lecture.md");
        host.Settle();
        Button more = More(HeaderOf(host, "Lecture.md"));
        Assert.True(more.Focus());

        Invoke(more);
        host.Settle();
        host.Settle();

        Assert.True(more.ContextMenu!.IsOpen);
        if (!LandedOnTheFirstRow(more))
        {
            return;
        }

        more.ContextMenu.IsOpen = false;
        host.Settle();

        // After the mouse: the most recent input device stays the mouse through an Invoke, and
        // a pointer's mouse-up on ⋯ itself is forgotten once that input is done.
        // Only raw input moves the most recent device, so the fact sets it as a pointer would.
        (typeof(InputManager).GetProperty(nameof(InputManager.MostRecentInputDevice))?.GetSetMethod(nonPublic: true)
            ?? throw new InvalidOperationException("InputManager.MostRecentInputDevice has no setter"))
            .Invoke(InputManager.Current, [Mouse.PrimaryDevice]);
        more.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseLeftButtonUpEvent });
        host.Settle();
        Assert.IsAssignableFrom<MouseDevice>(InputManager.Current.MostRecentInputDevice);
        Assert.True(more.Focus());
        Invoke(more);
        host.Settle();
        host.Settle();
        Assert.True(LandedOnTheFirstRow(more));
        more.ContextMenu.IsOpen = false;
        host.Settle();

        // A pointer's click, its mouse-up and Click in one input, opens the menu as WPF does.
        Assert.True(more.Focus());
        more.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseLeftButtonUpEvent });
        more.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        host.Settle();
        host.Settle();
        Assert.True(more.ContextMenu.IsOpen);
        Assert.IsNotType<MenuItem>(Keyboard.FocusedElement);
        more.ContextMenu.IsOpen = false;
        host.Settle();

        // The other pane's ⋯, with the keys in this one: its pane goes active, and the keys
        // land on its first row all the same.
        host.Dispose();
        using ShownShell split = Split(out _, out WorkspaceGroupViewModel second);
        Button other = More(HeaderOf(split, "b.md"));
        Invoke(other);
        split.Settle();
        split.Settle();
        Assert.Same(second, split.Workspace.ActiveGroup);
        Assert.True(LandedOnTheFirstRow(other));
        other.ContextMenu!.IsOpen = false;
        split.Settle();
    });

    /// <summary>Below 520 the switch drops its labels for tooltips, and the folders collapse
    /// into a "…" button with a Folders menu. The bar is sized, not the window, so the fact
    /// holds on a screen too small for the window's default width.</summary>
    [Fact]
    public void BelowFiveHundredTwentyTheHeaderCompacts() => StaThread.RunPumped(() =>
    {
        using var host = new ShownShell((Nested, "# Lecture\n"));
        host.Workspace.OpenPath(Nested);
        host.Settle();
        NoteHeaderView header = HeaderOf(host, Nested);
        header.Width = 600;
        host.Settle();
        Assert.False(header.IsCompact);
        RadioButton reading = Choice(header, "NoteHeaderReading");
        Assert.Contains("Reading", ShownText(reading));

        header.Width = 500;
        host.Settle();

        Assert.True(header.ActualWidth < NoteHeaderView.CompactBelow);
        Assert.True(header.IsCompact);
        Assert.DoesNotContain("Reading", ShownText(reading));
        Assert.Equal("Reading", AutomationProperties.GetName(reading));

        // The tooltips are on the glyphs, which have no peer, so a choice's help text isn't its
        // name again: Narrator read the radio's tooltip as "Source, radio button, selected,
        // Source view".
        RadioButton source = Choice(header, "NoteHeaderSource");
        Assert.Equal("Source view", GlyphOf(source).ToolTip);
        Assert.Equal("Switch to reading view (Ctrl+Shift+E)", GlyphOf(reading).ToolTip);
        Assert.Equal(string.Empty, HelpTextOf(source));
        Assert.Equal(string.Empty, HelpTextOf(reading));
        Assert.DoesNotContain(ShownShell.Descendants(header).OfType<Button>(), button => button.IsVisible && AutomationProperties.GetName(button) == "maths");
        Button folders = ShownShell.Descendants(header).OfType<Button>().Single(button => button.IsVisible && AutomationProperties.GetAutomationId(button) == "NoteHeaderFolders");
        Assert.Equal("Folders", AutomationProperties.GetName(folders));
        Assert.Equal("learning › maths", HelpTextOf(folders));
        Assert.Equal(["learning", "maths"], folders.ContextMenu!.Items.OfType<MenuItem>().Select(item => AutomationProperties.GetName(item)));
    });

    /// <summary>A compact bar collapses its folders because it's compact, not because they
    /// don't fit: a short path that arrives while it's compact goes behind "…" too.</summary>
    [Fact]
    public void ACompactBarCollapsesFoldersThatWouldFit() => StaThread.RunPumped(() =>
    {
        using var host = new ShownShell((Nested, "# Lecture\n"));
        host.Workspace.OpenPath(Nested);
        host.Shell.Width = 500;
        host.Settle();
        NoteHeaderView header = HeaderOf(host, Nested);
        Assert.True(header.IsCompact);

        ((WorkspaceTabViewModel)header.DataContext).RetargetPath("a/Lecture.md");
        host.Settle();
        host.Settle();
        Assert.Empty(VisibleSegments(header));
        Button folders = ShownShell.Descendants(header).OfType<Button>().Single(button => button.IsVisible && AutomationProperties.GetAutomationId(button) == "NoteHeaderFolders");
        Assert.Equal(["a"], folders.ContextMenu!.Items.OfType<MenuItem>().Select(item => AutomationProperties.GetName(item)));
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
        header.Width = 600;
        host.Settle();
        Assert.True(Segment(header, "maths").Focus());

        header.Width = 500;
        host.Settle();
        Assert.True(header.IsCompact);
        Assert.Equal("NoteHeaderFolders", AutomationProperties.GetAutomationId((DependencyObject)Keyboard.FocusedElement));

        header.Width = 600;
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

    /// <summary>#1859: the reading view projects its document asynchronously, so
    /// one settle can come before it on a slow runner and leave no link yet. This
    /// settles until the first link is there.</summary>
    private static System.Windows.Documents.Hyperlink FirstReadingLink(ShownShell host, FrameworkElement content)
    {
        System.Windows.Documents.Hyperlink? link = null;
        Assert.True(
            SpinWait.SpinUntil(
                () =>
                {
                    host.Settle();
                    link = ShownShell.Descendants(content).OfType<SlateWindows.Reading.ReadingSurface>().Any()
                        ? ReadingLinks(content).FirstOrDefault()
                        : null;
                    return link is not null;
                },
                TimeSpan.FromSeconds(10)),
            "the reading view never projected the note's link");
        return link!;
    }

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

    private static string HelpTextOf(UIElement element) =>
        UIElementAutomationPeer.CreatePeerForElement(element).GetHelpText();

    private static FrameworkElement GlyphOf(RadioButton choice) =>
        ShownShell.Descendants(choice).OfType<AutomationPresentationTextBlock>().First();

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
