// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SlateWindows.Canvas;
using SlateWindows.Panels;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W7-7 PR 4 (#1247, contract R-5, spec §5.2.2; codex round 4): an EMPTY
/// right-pane leaf's stop is its notice. In the no-note, empty and failure
/// states the landing into a leaf — the reveal's, which is the ring's
/// right-pane content landing — puts the keys on the notice, the notice's
/// name is its sentence, and every arrow from it stays in the leaf.
/// </summary>
/// <remarks>
/// The leaves are the shipped window's, built by its own XAML, lifted with
/// the rail into a window of their own between buttons on every side and
/// bound to a real workspace over a real vault. The failure states are
/// injected into the leaf models' own error properties — a core read
/// failure is not reachable from a healthy vault. Keys are real presses
/// through the input manager, so WPF's directional navigation answers them.
/// </remarks>
public sealed class RightPaneNoticeLandingTests
{
    public static TheoryData<string, string> Cases()
    {
        var cases = new TheoryData<string, string>();
        foreach (string leaf in new[] { "backlinks", "outgoingLinks", "outline", "embeds", "tasks", "citations" })
        {
            foreach (string state in new[] { "no note", "empty", "failed" })
            {
                cases.Add(leaf, state);
            }
        }

        return cases;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void AnEmptyLeafLandsOnItsNoticeAndKeepsEveryArrow(string leaf, string state) => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize(leaf, state);
        (TextBlock notice, string expected) = host.ExpectedNotice(leaf, state);

        foreach (Key key in new[] { Key.Left, Key.Right, Key.Up, Key.Down })
        {
            Assert.True(host.Beside.Focus());
            host.Shell.LandInRightPane();

            Assert.Same(notice, Keyboard.FocusedElement);
            Assert.Equal(expected, notice.Text);
            Assert.Equal(expected, UIElementAutomationPeer.CreatePeerForElement(notice).GetName());

            host.Press(key);

            FrameworkElement body = host.VisibleLeafBody();
            Assert.True(
                body.IsKeyboardFocusWithin,
                $"{leaf} ({state}): {key} took the keys out of the leaf, to {Keyboard.FocusedElement}");
        }
    });

    /// <summary>A LOADING sentence is not a stop — it gives way to the rows
    /// within moments — while the final sentences are. The leaf models'
    /// own loading flags, set the way their loaders set them.</summary>
    [Theory]
    [InlineData("backlinks")]
    [InlineData("outgoingLinks")]
    [InlineData("outline")]
    [InlineData("embeds")]
    [InlineData("tasks")]
    public void ALoadingNoticeIsNotAStop(string leaf) => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize(leaf, "empty");
        RightPanePanelsViewModel panels = host.Panels;
        Assert.True(IsAStop(panels, leaf), $"premise: {leaf}'s empty sentence is a stop.");

        Loading(panels, leaf);

        Assert.StartsWith(leaf == "embeds" ? "Resolving" : "Loading", Message(panels, leaf), StringComparison.Ordinal);
        Assert.False(IsAStop(panels, leaf));
    });

    public static TheoryData<string> ListLeaves() => ["backlinks", "outgoingLinks", "outline", "tasks"];

    /// <summary>
    /// Codex round 5 (R-5; W7-6 §4): while a leaf loads its notice is not a
    /// stop, so its landing — the ring's, a reveal's — is the EMPTY list
    /// (AR-6). The rows published under the keys re-land them once on row
    /// 0: one focus change, nothing said, and the populated list never
    /// holds them.
    /// </summary>
    [Theory]
    [MemberData(nameof(ListLeaves))]
    public void RowsPublishedUnderTheKeysLandThemOnTheFirstRow(string leaf) => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize(leaf, "empty");
        host.AwaitFinalNotice(leaf);
        SetLoading(host.Panels, leaf, loading: true);
        ListBox list = host.List(leaf);
        Assert.True(host.Beside.Focus());
        host.Shell.LandInRightPane();
        Assert.Same(list, Keyboard.FocusedElement);
        host.ForgetFocusAndSpeech();

        Publish(host.Panels, leaf, rows: 2);
        PumpedDispatcher.Drain();

        object row = list.ItemContainerGenerator.ContainerFromIndex(0);
        Assert.IsType<ListBoxItem>(row);
        Assert.Same(row, Keyboard.FocusedElement);
        Assert.Equal([row], host.FocusChanges);
        Assert.Empty(host.Announced);
    });

    /// <summary>Codex round 5: a final notice holding the keys stops being a
    /// stop when its sentence turns to "Loading…" — a note switch, a save's
    /// refresh. It hands them to its list, the loading leaf's stop, before
    /// WPF's own re-evaluation strands them; the rows that follow land them
    /// on row 0. Nothing is said on the way.</summary>
    [Theory]
    [MemberData(nameof(ListLeaves))]
    public void ANoticeHoldingTheKeysHandsThemToItsListWhenLoadingStarts(string leaf) => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize(leaf, "empty");
        host.AwaitFinalNotice(leaf);
        TextBlock notice = host.Notice(leaf);
        ListBox list = host.List(leaf);
        Assert.True(host.Beside.Focus());
        host.Shell.LandInRightPane();
        Assert.Same(notice, Keyboard.FocusedElement);
        host.ForgetFocusAndSpeech();

        SetLoading(host.Panels, leaf, loading: true);
        PumpedDispatcher.Drain();

        Assert.False(notice.Focusable, "premise: the loading sentence is not a stop.");
        Assert.Same(list, Keyboard.FocusedElement);

        Publish(host.Panels, leaf, rows: 2);
        PumpedDispatcher.Drain();

        object row = list.ItemContainerGenerator.ContainerFromIndex(0);
        Assert.Same(row, Keyboard.FocusedElement);
        Assert.Equal([list, row], host.FocusChanges);
        Assert.Empty(host.Announced);
    });

    /// <summary>Codex round 5 (spec §5.2.2): a load that ends EMPTY under the
    /// keys lands them on the leaf's final notice, its stop — not left on
    /// the empty list.</summary>
    [Theory]
    [MemberData(nameof(ListLeaves))]
    public void AnEmptyPublicationUnderTheKeysLandsThemOnTheNotice(string leaf) => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize(leaf, "empty");
        host.AwaitFinalNotice(leaf);
        SetLoading(host.Panels, leaf, loading: true);
        ListBox list = host.List(leaf);
        TextBlock notice = host.Notice(leaf);
        Assert.True(host.Beside.Focus());
        host.Shell.LandInRightPane();
        Assert.Same(list, Keyboard.FocusedElement);
        host.ForgetFocusAndSpeech();

        Publish(host.Panels, leaf, rows: 0);
        PumpedDispatcher.Drain();

        Assert.Same(notice, Keyboard.FocusedElement);
        Assert.Equal([notice], host.FocusChanges);
        Assert.Empty(host.Announced);
    });

    /// <summary>Codex round 5: a refresh that republishes the rows under the
    /// reader — a save's outline, a task toggle's tasks — destroys the row
    /// holding the keys; they land on the new first row, never on the bare
    /// populated list and never stranded on the window.</summary>
    [Theory]
    [InlineData("outline")]
    [InlineData("tasks")]
    public void ARepublishUnderTheReaderLandsTheKeysOnARow(string leaf) => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize(leaf, "empty");
        host.AwaitFinalNotice(leaf);
        ListBox list = host.List(leaf);
        Publish(host.Panels, leaf, rows: 3);
        PumpedDispatcher.Drain();
        Assert.True(host.Beside.Focus());
        host.Shell.LandInRightPane();
        host.Press(Key.Down);
        Assert.Same(list.ItemContainerGenerator.ContainerFromIndex(1), Keyboard.FocusedElement);
        host.ForgetFocusAndSpeech();

        Publish(host.Panels, leaf, rows: 3);
        PumpedDispatcher.Drain();

        Assert.Same(list.ItemContainerGenerator.ContainerFromIndex(0), Keyboard.FocusedElement);
        Assert.Equal([Keyboard.FocusedElement], host.FocusChanges);
        host.AssertNeverOnAPopulatedList();
        host.AssertNeverStranded();
        Assert.Empty(host.Announced);
    });

    public static TheoryData<string, string, Key> LandedRowActions()
    {
        var data = new TheoryData<string, string, Key>();
        foreach (string leaf in new[] { "backlinks", "outgoingLinks", "outline", "tasks" })
        {
            foreach (string landing in new[] { "boundary", "empty-area click", "publication" })
            {
                data.Add(leaf, landing, Key.Enter);
                if (leaf == "tasks")
                {
                    data.Add(leaf, landing, Key.Space);
                }
            }
        }

        return data;
    }

    /// <summary>
    /// Codex PR 4 round 7 finding 2: a landing puts the keys on a row
    /// WITHOUT selecting it — the leaf's boundary landing, a click on the
    /// list's empty area, a publication under the reader — and the leaf's
    /// keys act on that row with ONE press. Every handler read the list's
    /// selection: Enter did nothing, and Space on a task only selected it.
    /// The actions are recorded where the leaf model hands them on.
    /// </summary>
    [Theory]
    [MemberData(nameof(LandedRowActions))]
    public void OnePressActsOnTheRowALandingFocused(string leaf, string landing, Key key) => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize(leaf, "empty");
        host.AwaitFinalNotice(leaf);
        host.AttachWorkspaceToTheWindow();
        List<string> acted = RecordActions(host.Panels);
        ListBox list = host.List(leaf);
        Publish(host.Panels, leaf, rows: 3);
        PumpedDispatcher.Drain();
        Assert.True(host.Beside.Focus());
        switch (landing)
        {
            case "boundary":
                host.Shell.LandInRightPane();
                break;
            case "empty-area click":
                SelectorFocus.RegisterClickRule();
                list.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                {
                    RoutedEvent = UIElement.MouseDownEvent,
                });
                break;
            default:
                // The reader on row 1 (the arrow selects it), the rows
                // republished under them: the keeper lands them on a row.
                host.Shell.LandInRightPane();
                host.Press(Key.Down);
                Publish(host.Panels, leaf, rows: 3);
                PumpedDispatcher.Drain();
                break;
        }

        var row = Assert.IsType<ListBoxItem>(Keyboard.FocusedElement);
        Assert.Same(list, ItemsControl.ItemsControlFromItemContainer(row));
        Assert.False(row.IsSelected, $"premise: the {landing} landing selected its row");
        string expected = row.DataContext switch
        {
            BacklinkRowViewModel backlink => backlink.SourcePath,
            OutgoingLinkRowViewModel outgoing => outgoing.Link.TargetPath!,
            OutlineRowViewModel heading => heading.AnchorId,
            NoteTaskRowViewModel task => (key == Key.Space ? "toggle " : "open ") + task.Task.Text,
            _ => throw new Xunit.Sdk.XunitException($"an unexpected row {row.DataContext}"),
        };
        acted.Clear();

        host.Press(key);

        Assert.Equal([expected], acted);
    });

    /// <summary>
    /// W7-7 PR 4b (the owner's S5; the completeness sweep's G14): a note whose
    /// tasks are all done — the Tasks leaf's landing is the first POPULATED
    /// list's row, the Done list's, not the empty "Open tasks" list above it
    /// (AR-6 made that list its own stop while the rows sat one list below).
    /// </summary>
    [Fact]
    public void ATasksLeafWhoseTasksAreAllDoneLandsOnTheDoneList() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize("tasks", "empty");
        host.AwaitFinalNotice("tasks");
        PublishTasks(host.Panels, (false, 0), (true, 2));
        PumpedDispatcher.Drain();
        ListBox done = host.ElementWithId<ListBox>("PanelTasksDoneList");
        Assert.False(host.List("tasks").HasItems, "premise: the Open list has rows");
        Assert.True(host.Beside.Focus());
        host.ForgetFocusAndSpeech();

        host.Shell.LandInRightPane();

        var row = Assert.IsType<ListBoxItem>(Keyboard.FocusedElement);
        Assert.Same(done, ItemsControl.ItemsControlFromItemContainer(row));
        Assert.Equal([row], host.FocusChanges);
    });

    /// <summary>
    /// The sweep's G14 (b): Space on the last open task moves it to the Done
    /// list, and its row's keys went to the now-empty Open list (no stop
    /// notice there) — "Open tasks, list" with the rows one list below. The
    /// hand-over is declined, and the keys land on the Done list's row, once.
    /// </summary>
    [Fact]
    public void TheLastOpenTaskDoneLandsTheKeysOnTheDoneList() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize("tasks", "empty");
        host.AwaitFinalNotice("tasks");
        PublishTasks(host.Panels, (false, 1), (true, 1));
        PumpedDispatcher.Drain();
        ListBox open = host.List("tasks");
        ListBox done = host.ElementWithId<ListBox>("PanelTasksDoneList");
        open.UpdateLayout();
        Assert.True(((UIElement)open.ItemContainerGenerator.ContainerFromIndex(0)).Focus());
        host.ForgetFocusAndSpeech();

        PublishTasks(host.Panels, (false, 0), (true, 2));
        PumpedDispatcher.Drain();

        var row = Assert.IsType<ListBoxItem>(Keyboard.FocusedElement);
        Assert.Same(done, ItemsControl.ItemsControlFromItemContainer(row));
        Assert.DoesNotContain(open, host.FocusChanges);
        Assert.Equal([row], host.FocusChanges);
        host.AssertNeverStranded();
    });

    /// <summary>
    /// The sweep's G20, through the shell's own restore
    /// (<see cref="MainWindow.LandToken"/>, every dismissal's): the keys left
    /// a task's row for a dialog, and while it was up the task was done — its
    /// row gone. The restore lands in the leaf the row was in, on the Done
    /// list's row, in one focus change — it used to fall back to the editor.
    /// </summary>
    [Fact]
    public void ARestoreWhoseRowIsGoneLandsInItsLeaf() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize("tasks", "empty");
        host.AwaitFinalNotice("tasks");
        PublishTasks(host.Panels, (false, 1), (true, 1));
        PumpedDispatcher.Drain();
        ListBox open = host.List("tasks");
        ListBox done = host.ElementWithId<ListBox>("PanelTasksDoneList");
        open.UpdateLayout();
        Assert.True(((UIElement)open.ItemContainerGenerator.ContainerFromIndex(0)).Focus());
        IInputElement token = Keyboard.FocusedElement;
        Assert.True(host.Beside.Focus());
        PublishTasks(host.Panels, (false, 0), (true, 2));
        PumpedDispatcher.Drain();
        Assert.Same(host.Beside, Keyboard.FocusedElement);
        Assert.Null(PresentationSource.FromVisual((Visual)token));
        host.ForgetFocusAndSpeech();

        Assert.True(host.Shell.LandToken(token), "the restore took nothing");

        var row = Assert.IsType<ListBoxItem>(Keyboard.FocusedElement);
        Assert.Same(done, ItemsControl.ItemsControlFromItemContainer(row));
        Assert.Equal([row], host.FocusChanges);
    });

    /// <summary>
    /// W7-7 PR 4b (the completeness sweep's G11): a Citations republish under
    /// the reader — any save — destroyed the row holding the keys, and its
    /// hand-over put them on the bare list (a UIA focus change on "Citations,
    /// list") before the leaf's own restore moved them to a row: two changes,
    /// the first on the bare Selector. The hand-over is declined, and the
    /// restore lands them on the reader's row: one focus change.
    /// </summary>
    [Fact]
    public void ACitationsRepublishUnderTheReaderIsOneFocusChange() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize("citations", "empty", note: "Cites [@knuth1984] and [@ghostkey].\n");
        // The window's own publish restore (MainWindow.RestoreCitationFocus)
        // observes the workspace's leaf.
        host.AttachWorkspaceToTheWindow();
        ListBox list = host.ElementWithId<ListBox>("PanelCitationsList");
        Assert.True(PumpedDispatcher.PumpUntil(() => list.Items.Count >= 2 && list.IsVisible), "premise: the leaf never listed the note's citations.");
        list.UpdateLayout();
        Assert.True(((UIElement)list.ItemContainerGenerator.ContainerFromIndex(1)).Focus());
        PumpedDispatcher.Drain();
        host.ForgetFocusAndSpeech();

        host.Citations.Refresh();
        Assert.True(PumpedDispatcher.PumpUntil(() => list.Items.Count >= 2), "the republish never listed the citations again.");
        PumpedDispatcher.Drain();

        var row = Assert.IsType<ListBoxItem>(Keyboard.FocusedElement);
        Assert.Same(list, ItemsControl.ItemsControlFromItemContainer(row));
        Assert.Equal([row], host.FocusChanges);
        host.AssertNeverOnAPopulatedList();
    });

    /// <summary>A Tasks leaf publication with <paramref name="open"/>'s and
    /// <paramref name="done"/>'s counts of open and done tasks.</summary>
    private static void PublishTasks(RightPanePanelsViewModel panels, (bool Completed, int Count) open, (bool Completed, int Count) done)
    {
        const BindingFlags any = BindingFlags.NonPublic | BindingFlags.Instance;
        int requestId = (int)(typeof(RightPanePanelsViewModel).GetField("_tasksRequestId", any)
            ?? throw new InvalidOperationException("_tasksRequestId is gone")).GetValue(panels)!;
        TaskItem[] tasks =
        [
            .. Enumerable.Range(0, open.Count).Select(index => Task(index, open.Completed)),
            .. Enumerable.Range(open.Count, done.Count).Select(index => Task(index, done.Completed)),
        ];
        panels.PublishTasks(
            panels.LoadGenerationForTests,
            requestId,
            new NoteTasksPage(tasks, (uint)tasks.Length, (uint)tasks.Length, "hash"),
            failure: null);

        static TaskItem Task(int index, bool completed) => new(
            Ordinal: (uint)index, Text: $"Task {index}", StatusChar: completed ? "x" : " ", Completed: completed,
            DueMs: null, ScheduledMs: null, Priority: null, Recurrence: null,
            Line: (uint)(index + 1), ByteOffset: 0, CheckboxStartByte: 2, CheckboxEndByte: 5);
    }

    /// <summary>
    /// Codex PR 4's final check: a double-click on the Citations list's EMPTY
    /// area — below the rows — expands nothing. Its first press lands the
    /// keys on the first row (the click rule), and the double-click expanded
    /// that landed row: a citation never clicked. A double-click on a row
    /// expands that row.
    /// </summary>
    [Fact]
    public void ADoubleClickOnTheCitationsEmptyAreaExpandsNothing() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize("citations", "empty", note: "Cites [@knuth1984].\n", bibliography: true);
        host.AttachWorkspaceToTheWindow();
        ListBox list = host.ElementWithId<ListBox>("PanelCitationsList");
        Assert.True(
            PumpedDispatcher.PumpUntil(() => list.IsVisible && list.Items.OfType<CitationRowViewModel>().Any(row => row.CanExpand)),
            "premise: the leaf never listed an expandable citation.");
        list.UpdateLayout();
        SelectorFocus.RegisterClickRule();
        list.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
        {
            RoutedEvent = UIElement.MouseDownEvent,
        });
        Assert.IsType<ListBoxItem>(Keyboard.FocusedElement);

        list.RaiseEvent(DoubleClick(list));
        PumpedDispatcher.Drain();

        Assert.Null(host.Workspace.CitationDetails);

        var row = (ListBoxItem)list.ItemContainerGenerator.ContainerFromItem(
            list.Items.OfType<CitationRowViewModel>().First(candidate => candidate.CanExpand));
        list.RaiseEvent(DoubleClick(VisualTreeHelper.GetChild(row, 0)));
        PumpedDispatcher.Drain();

        Assert.NotNull(host.Workspace.CitationDetails);

        static MouseButtonEventArgs DoubleClick(object hit) =>
            new(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
            {
                RoutedEvent = Control.MouseDoubleClickEvent,
                Source = hit,
            };
    });

    /// <summary>The leaf model's hand-offs — open, scroll, toggle —
    /// replaced by recorders.</summary>
    private static List<string> RecordActions(RightPanePanelsViewModel panels)
    {
        var acted = new List<string>();
        const BindingFlags any = BindingFlags.NonPublic | BindingFlags.Instance;
        void Replace(string field, object recorder) =>
            (typeof(RightPanePanelsViewModel).GetField(field, any)
                ?? throw new InvalidOperationException($"{field} is gone"))
            .SetValue(panels, recorder);

        Replace("_openInternal", new Func<string, WorkspaceOpenTarget, bool>((path, _) =>
        {
            acted.Add(path);
            return false;
        }));
        Replace("_scrollToAnchor", new Action<LinkAnchor, string?>((anchor, _) => acted.Add(anchor.Text)));
        Replace("_scrollToTask", new Action<TaskItem, string>((task, _) => acted.Add("open " + task.Text)));
        Replace("_toggleTask", new Func<TaskItem, string, bool>((task, _) =>
        {
            acted.Add("toggle " + task.Text);
            return true;
        }));
        return acted;
    }

    /// <summary>Codex round 5: the Embeds leaf's host and its cards'
    /// renderers are layout, not stops. The landing is inside a card —
    /// here its Jump to source button — never the populated host; while
    /// the embeds resolve the leaf has no stop, and a notice holding the
    /// keys hands them to the pane's stable stop, the rail's row.</summary>
    [Fact]
    public void TheEmbedsLeafLandsInsideACardAndHasNoStopWhileResolving() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize("embeds", "empty");
        host.AwaitFinalNotice("embeds");
        TextBlock notice = host.Notice("embeds");
        ItemsControl cards = host.ElementWithId<ItemsControl>("PanelEmbedsList");
        Assert.True(host.Beside.Focus());
        host.Shell.LandInRightPane();
        Assert.Same(notice, Keyboard.FocusedElement);
        host.ForgetFocusAndSpeech();

        SetLoading(host.Panels, "embeds", loading: true);
        PumpedDispatcher.Drain();

        Assert.IsType<ListBoxItem>(Keyboard.FocusedElement);
        Assert.Same(
            host.ElementWithId<ListBox>("RightPaneLeaves"),
            ItemsControl.ItemsControlFromItemContainer((DependencyObject)Keyboard.FocusedElement));
        host.AssertNeverStranded();

        host.Panels.Embeds.Add(EmbedRowViewModel.OverBudget(new OutgoingLink(
            TargetPath: "target.md", TargetRaw: "target", TargetAnchor: null,
            Kind: "wiki", IsEmbed: true, IsExternal: false, IsUnresolved: false,
            Snippet: "", Ordinal: 0, SpanStart: 0, SpanEnd: 0, DisplayText: null)));
        SetLoading(host.Panels, "embeds", loading: false);
        PumpedDispatcher.Drain();
        Assert.True(host.Beside.Focus());
        host.ForgetFocusAndSpeech();

        host.Shell.LandInRightPane();

        var jump = Assert.IsType<Button>(Keyboard.FocusedElement);
        Assert.Equal("Jump to source: target.md", AutomationProperties.GetName(jump));
        Assert.True(cards.IsAncestorOf(jump));
        Assert.DoesNotContain(host.FocusChanges, focus => focus is ItemsControl or EditorEmbedPreviewView);
    });

    /// <summary>Codex round 5: a Citations notice holding the keys collapses
    /// when a reload starts; it hands them to the list — the loading leaf's
    /// stop, whose publish restore (MainWindow.RestoreCitationFocus) then
    /// lands them on a row or the notice — instead of stranding
    /// them.</summary>
    [Fact]
    public void ACitationNoticeHoldingTheKeysHandsThemToTheListWhenAReloadStarts() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize("citations", "empty");
        (TextBlock notice, _) = host.ExpectedNotice("citations", "empty");
        ListBox list = host.ElementWithId<ListBox>("PanelCitationsList");
        Assert.True(host.Beside.Focus());
        host.Shell.LandInRightPane();
        Assert.Same(notice, Keyboard.FocusedElement);
        host.ForgetFocusAndSpeech();

        const BindingFlags any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        typeof(CitationsPanelViewModel).GetProperty(nameof(CitationsPanelViewModel.IsLoading), any)!
            .GetSetMethod(nonPublic: true)!.Invoke(host.Citations, [true]);
        PumpedDispatcher.Drain();

        Assert.False(notice.IsVisible, "premise: the empty notice collapses while the leaf reloads.");
        Assert.Same(list, Keyboard.FocusedElement);
        host.AssertNeverStranded();
    });

    private static bool IsAStop(RightPanePanelsViewModel panels, string leaf) => leaf switch
    {
        "backlinks" => panels.BacklinksNoticeIsAStop,
        "outgoingLinks" => panels.OutgoingLinksNoticeIsAStop,
        "outline" => panels.OutlineNoticeIsAStop,
        "embeds" => panels.EmbedsNoticeIsAStop,
        _ => panels.TasksNoticeIsAStop,
    };

    private static string? Message(RightPanePanelsViewModel panels, string leaf) => leaf switch
    {
        "backlinks" => panels.BacklinksEmptyMessage,
        "outgoingLinks" => panels.OutgoingLinksEmptyMessage,
        "outline" => panels.OutlineEmptyMessage,
        "embeds" => panels.EmbedsEmptyMessage,
        _ => panels.TasksEmptyMessage,
    };

    /// <summary>The loading flag, set the way the leaf's loader sets it —
    /// its own notification included.</summary>
    private static void SetLoading(RightPanePanelsViewModel panels, string leaf, bool loading)
    {
        const BindingFlags any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        if (leaf == "tasks")
        {
            typeof(RightPanePanelsViewModel).GetField("_isLoadingTasks", any)!.SetValue(panels, loading);
            RaiseHeaderChanges(panels);
            return;
        }

        string property = leaf switch
        {
            "outline" => nameof(RightPanePanelsViewModel.IsLoadingOutline),
            "embeds" => nameof(RightPanePanelsViewModel.IsResolvingEmbeds),
            _ => nameof(RightPanePanelsViewModel.IsLoadingLinks),
        };
        typeof(RightPanePanelsViewModel).GetProperty(property, any)!.GetSetMethod(nonPublic: true)!.Invoke(panels, [loading]);
    }

    private static void RaiseHeaderChanges(RightPanePanelsViewModel panels) =>
        (typeof(RightPanePanelsViewModel).GetMethod(
                "RaiseHeaderChanges", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("RaiseHeaderChanges is gone"))
        .Invoke(panels, null);

    /// <summary>A leaf's publication, as its loader makes it: the rows
    /// first, then the loading flag and the notifications (round 3's
    /// order). The outline's and the tasks' through their own publish
    /// methods, as a save's refresh arrives.</summary>
    private static void Publish(RightPanePanelsViewModel panels, string leaf, int rows)
    {
        switch (leaf)
        {
            case "backlinks":
                panels.Backlinks.Clear();
                for (int index = 0; index < rows; index++)
                {
                    panels.Backlinks.Add(new BacklinkRowViewModel(new Backlink(
                        $"source{index}.md", "links to plain", (uint)index, "wiki", false)));
                }

                SetLoading(panels, leaf, loading: false);
                RaiseHeaderChanges(panels);
                break;
            case "outgoingLinks":
                panels.OutgoingLinks.Clear();
                for (int index = 0; index < rows; index++)
                {
                    panels.OutgoingLinks.Add(new OutgoingLinkRowViewModel(new OutgoingLink(
                        TargetPath: $"target{index}.md", TargetRaw: $"target{index}", TargetAnchor: null,
                        Kind: "wiki", IsEmbed: false, IsExternal: false, IsUnresolved: false,
                        Snippet: "", Ordinal: (uint)index, SpanStart: 0, SpanEnd: 0, DisplayText: null)));
                }

                SetLoading(panels, leaf, loading: false);
                RaiseHeaderChanges(panels);
                break;
            case "outline":
                panels.PublishOutline(
                    panels.NotePath!,
                    panels.LoadGenerationForTests,
                    panels.OutlineRequestIdForTests,
                    [.. Enumerable.Range(0, rows).Select(index => new Heading(
                        Level: 1, Text: $"Heading {index}", Ordinal: (uint)index,
                        AnchorId: $"heading-{index}", ByteOffset: (uint)(index * 16)))],
                    total: rows,
                    announceCount: false);
                break;
            default:
                const BindingFlags any = BindingFlags.NonPublic | BindingFlags.Instance;
                int requestId = (int)(typeof(RightPanePanelsViewModel).GetField("_tasksRequestId", any)
                    ?? throw new InvalidOperationException("_tasksRequestId is gone")).GetValue(panels)!;
                panels.PublishTasks(
                    panels.LoadGenerationForTests,
                    requestId,
                    new NoteTasksPage(
                        [.. Enumerable.Range(0, rows).Select(index => new TaskItem(
                            Ordinal: (uint)index, Text: $"Task {index}", StatusChar: " ", Completed: false,
                            DueMs: null, ScheduledMs: null, Priority: null, Recurrence: null,
                            Line: (uint)(index + 1), ByteOffset: 0, CheckboxStartByte: 2, CheckboxEndByte: 5))],
                        (uint)rows,
                        (uint)rows,
                        "hash"),
                    failure: null);
                break;
        }
    }

    private static void Loading(RightPanePanelsViewModel panels, string leaf)
    {
        const BindingFlags any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        if (leaf == "tasks")
        {
            typeof(RightPanePanelsViewModel).GetField("_isLoadingTasks", any)!.SetValue(panels, true);
            return;
        }

        string property = leaf switch
        {
            "outline" => nameof(RightPanePanelsViewModel.IsLoadingOutline),
            "embeds" => nameof(RightPanePanelsViewModel.IsResolvingEmbeds),
            _ => nameof(RightPanePanelsViewModel.IsLoadingLinks),
        };
        typeof(RightPanePanelsViewModel).GetProperty(property, any)!.GetSetMethod(nonPublic: true)!.Invoke(panels, [true]);
    }

    private sealed class Host : IDisposable
    {
        private readonly FixtureVault _fixture = FixtureVault.Create(0, "leaf-notices");
        private readonly Func<bool> _priorOverlayProbe = CanvasSurfaceView.ShellOverlayIsOpen;
        private readonly List<IInputElement?> _stranded = [];
        private readonly List<IInputElement> _populatedFocus = [];
        private VaultSession? _session;
        private WorkspaceViewModel? _workspace;
        private Window? _window;
        private Grid _leafHost = null!;

        public MainWindow Shell { get; private set; } = null!;

        public Button Beside { get; private set; } = null!;

        public RightPanePanelsViewModel Panels => _workspace!.Panels;

        public CitationsPanelViewModel Citations => _workspace!.Citations;

        public WorkspaceViewModel Workspace => _workspace!;

        /// <summary>Every keyboard focus change in the hosted window.</summary>
        public List<IInputElement> FocusChanges { get; } = [];

        /// <summary>Everything the workspace said.</summary>
        public List<A11yEvent> Announced { get; } = [];

        public void ForgetFocusAndSpeech()
        {
            FocusChanges.Clear();
            _stranded.Clear();
            _populatedFocus.Clear();
            Announced.Clear();
        }

        /// <summary>Wait out the real loads the note's opening started, so
        /// their late publications cannot land on a fact's own.</summary>
        public void AwaitFinalNotice(string leaf) =>
            Assert.True(
                PumpedDispatcher.PumpUntil(() => IsAStop(Panels, leaf)),
                $"premise: {leaf}'s load never finished with its final notice.");

        public ListBox List(string leaf) => ElementWithId<ListBox>(leaf switch
        {
            "backlinks" => "PanelBacklinksList",
            "outgoingLinks" => "PanelOutgoingLinksList",
            "outline" => "PanelOutlineList",
            _ => "PanelTasksOpenList",
        });

        public TextBlock Notice(string leaf) => ElementWithId<TextBlock>(leaf switch
        {
            "backlinks" => "PanelBacklinksNotice",
            "outgoingLinks" => "PanelOutgoingLinksNotice",
            "outline" => "PanelOutlineNotice",
            "embeds" => "PanelEmbedsNotice",
            _ => "PanelTasksNotice",
        });

        public T ElementWithId<T>(string automationId)
            where T : DependencyObject =>
            Descendants(_leafHost).OfType<T>().Single(element => AutomationProperties.GetAutomationId(element) == automationId);

        /// <summary>The keys never went nowhere — to no element, the window,
        /// or a detached one — since the last forget.</summary>
        public void AssertNeverStranded() =>
            Assert.True(
                _stranded.Count == 0,
                "the keys were stranded; focus went "
                + string.Join(" → ", FocusChanges.Select(focus => focus?.GetType().Name ?? "nothing")));

        /// <summary>No list held the keys itself while it had rows since the
        /// last forget — judged at each focus change.</summary>
        public void AssertNeverOnAPopulatedList() =>
            Assert.True(
                _populatedFocus.Count == 0,
                "a populated list took the keys itself; focus went "
                + string.Join(" → ", FocusChanges.Select(focus => focus.GetType().Name)));

        /// <param name="note">The open note's text.</param>
        /// <param name="bibliography">Adds a bibliography and a citation
        /// style, so a citation of <c>knuth1984</c> resolves and
        /// expands.</param>
        public void Initialize(string leaf, string state, string note = "Just a line of text.\n", bool bibliography = false)
        {
            Assert.Null(Application.Current);
            File.WriteAllText(Path.Combine(_fixture.Root, "plain.md"), note);
            if (bibliography)
            {
                File.WriteAllText(
                    Path.Combine(_fixture.Root, "library.bib"),
                    "@article{knuth1984,\n  title = {Literate Programming},\n  author = {Knuth, Donald E.},\n"
                        + "  year = {1984},\n  journal = {The Computer Journal}\n}\n");
                File.Copy(Path.Combine(SourceText.RepoRoot(), "demo-vault", "csl", "ieee.csl"), Path.Combine(_fixture.Root, "ieee.csl"));
                File.WriteAllText(
                    Path.Combine(_fixture.Root, "slate.json"),
                    "{\"citations\":{\"bibliography\":\"library.bib\",\"cite_style\":\"ieee\"}}");
            }

            _session = VaultSession.OpenFilesystem(_fixture.Root);
            using (var cancel = new CancelToken())
            {
                _session.ScanInitial(cancel);
            }

            _workspace = new WorkspaceViewModel(
                _session, _fixture.Root, () => [], Announced.Add, startInteractionBackgroundWork: false);
            if (state != "no note")
            {
                _workspace.OpenPath("plain.md");
            }

            if (state == "failed")
            {
                Fail(_workspace, leaf);
            }

            _workspace.ActiveLeaf = WorkspaceViewModel.Leaves.First(option => option.Id == leaf);

            Shell = new MainWindow();
            _leafHost = Assert.IsType<Grid>(Shell.FindName("RightPaneLeafHost"));
            var border = Assert.IsAssignableFrom<Decorator>(_leafHost.Parent);
            border.Child = null;
            _leafHost.DataContext = _workspace;

            var grid = new Grid();
            for (int index = 0; index < 3; index++)
            {
                grid.RowDefinitions.Add(new RowDefinition());
                grid.ColumnDefinitions.Add(new ColumnDefinition());
            }

            Button Place(string text, int row, int column)
            {
                var button = new Button { Content = text };
                Grid.SetRow(button, row);
                Grid.SetColumn(button, column);
                grid.Children.Add(button);
                return button;
            }

            Beside = Place("Above", 0, 1);
            _ = Place("Below", 2, 1);
            _ = Place("Left", 1, 0);
            _ = Place("Right", 1, 2);
            Grid.SetRow(_leafHost, 1);
            Grid.SetColumn(_leafHost, 1);
            grid.Children.Add(_leafHost);
            _window = new Window
            {
                Content = grid,
                Width = 1100,
                Height = 700,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                ShowActivated = false,
            };
            _window.Show();
            _window.UpdateLayout();
            PumpedDispatcher.Drain();
            Keyboard.AddGotKeyboardFocusHandler(_window, (_, e) =>
            {
                FocusChanges.Add(e.NewFocus);
                if (e.NewFocus is ItemsControl { HasItems: true })
                {
                    _populatedFocus.Add(e.NewFocus);
                }
            });
            Keyboard.AddLostKeyboardFocusHandler(_window, (_, e) =>
            {
                if (e.NewFocus is null or Window
                    || (e.NewFocus is Visual visual && PresentationSource.FromVisual(visual) is null))
                {
                    _stranded.Add(e.NewFocus);
                }
            });
        }

        /// <summary>The leaf's notice for this state, and the sentence its
        /// model says it shows.</summary>
        public (TextBlock Notice, string Text) ExpectedNotice(string leaf, string state)
        {
            WorkspaceViewModel workspace = _workspace!;
            (string id, string? text) = leaf switch
            {
                "backlinks" => ("PanelBacklinksNotice", workspace.Panels.BacklinksEmptyMessage),
                "outgoingLinks" => ("PanelOutgoingLinksNotice", workspace.Panels.OutgoingLinksEmptyMessage),
                "outline" => ("PanelOutlineNotice", workspace.Panels.OutlineEmptyMessage),
                "embeds" => ("PanelEmbedsNotice", workspace.Panels.EmbedsEmptyMessage),
                "tasks" => ("PanelTasksNotice", workspace.Panels.TasksEmptyMessage),
                _ => state switch
                {
                    "no note" => ("PanelCitationsNoFile", workspace.Citations.NoFileText),
                    "empty" => ("PanelCitationsEmpty", workspace.Citations.EmptyText),
                    _ => ("PanelCitationsError", workspace.Citations.ErrorSpoken),
                },
            };
            Assert.False(string.IsNullOrEmpty(text), $"premise: {leaf} ({state}) shows no notice.");
            TextBlock notice = Descendants(_leafHost).OfType<TextBlock>()
                .Single(block => AutomationProperties.GetAutomationId(block) == id);
            Assert.True(notice.IsVisible, $"premise: {id} is not shown.");
            return (notice, text!);
        }

        public FrameworkElement VisibleLeafBody() =>
            _leafHost.Children.OfType<DockPanel>().Single(body => Grid.GetColumn(body) == 0 && body.IsVisible);

        public void Press(Key key)
        {
            InputManager.Current.ProcessInput(new KeyEventArgs(
                Keyboard.PrimaryDevice, PresentationSource.FromVisual(_window!)!, Environment.TickCount, key)
            {
                RoutedEvent = Keyboard.PreviewKeyDownEvent,
            });
            PumpedDispatcher.Drain();
        }

        /// <summary>The window's leaf handlers reach the leaves' models
        /// through the lifecycle model, as in the app.</summary>
        public void AttachWorkspaceToTheWindow()
        {
            var lifecycle = Assert.IsType<VaultLifecycleViewModel>(Shell.DataContext);
            (typeof(VaultLifecycleViewModel).GetProperty(nameof(VaultLifecycleViewModel.Workspace))
                ?? throw new InvalidOperationException("Workspace is gone"))
                .SetValue(lifecycle, _workspace);
        }

        public void Dispose()
        {
            try
            {
                if (Shell?.DataContext is VaultLifecycleViewModel lifecycle && lifecycle.Workspace is not null)
                {
                    typeof(VaultLifecycleViewModel).GetProperty(nameof(VaultLifecycleViewModel.Workspace))!.SetValue(lifecycle, null);
                }

                _window?.Close();
                Shell?.Close();
                _workspace?.Dispose();
                _session?.Dispose();
                _fixture.Dispose();
            }
            finally
            {
                CanvasSurfaceView.ShellOverlayIsOpen = _priorOverlayProbe;
            }
        }

        /// <summary>A core read failure, set on the leaf model's own error
        /// state the way its loader sets it.</summary>
        private static void Fail(WorkspaceViewModel workspace, string leaf)
        {
            const BindingFlags any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            void Set(object model, string property) =>
                (model.GetType().GetProperty(property, any)?.GetSetMethod(nonPublic: true)
                    ?? throw new InvalidOperationException($"{property} has no setter"))
                .Invoke(model, ["the index could not be read"]);

            switch (leaf)
            {
                case "backlinks" or "outgoingLinks":
                    Set(workspace.Panels, nameof(RightPanePanelsViewModel.LinksLoadError));
                    break;
                case "outline":
                    Set(workspace.Panels, nameof(RightPanePanelsViewModel.OutlineLoadError));
                    break;
                case "embeds":
                    Set(workspace.Panels, nameof(RightPanePanelsViewModel.EmbedsLoadError));
                    break;
                case "tasks":
                    (typeof(RightPanePanelsViewModel).GetField("_tasksLoadError", any)
                        ?? throw new InvalidOperationException("_tasksLoadError is gone"))
                        .SetValue(workspace.Panels, "the index could not be read");
                    (typeof(RightPanePanelsViewModel).GetMethod("RaiseHeaderChanges", any)
                        ?? throw new InvalidOperationException("RaiseHeaderChanges is gone"))
                        .Invoke(workspace.Panels, null);
                    break;
                default:
                    Set(workspace.Citations, nameof(CitationsPanelViewModel.LoadError));
                    break;
            }
        }

        private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
        {
            for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, index);
                yield return child;
                foreach (DependencyObject nested in Descendants(child))
                {
                    yield return nested;
                }
            }
        }
    }

    private static void RunSta(Action body)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                PumpedDispatcher.Run(body);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(120)), "STA test body timed out.");
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
