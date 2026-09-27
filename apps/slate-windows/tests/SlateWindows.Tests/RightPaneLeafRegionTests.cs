// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Specialized;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using SlateWindows.Canvas;
using SlateWindows.Panels;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W7-7 PR 4 (#1247, contract R-5; codex PR 4 round 6's highs, reproduced on
/// <c>claude/w7-7-pr4-r6-repro</c> and inverted here into witnesses): the
/// right pane's leaves on the shipped leaf host — lifted from the window's
/// own XAML, bound to a real workspace over a real vault, with buttons on
/// every side so an arrow that leaves a leaf lands somewhere observable.
/// Keys are real presses through the input manager.
/// </summary>
public sealed class RightPaneLeafRegionTests
{
    /// <summary>
    /// Round 6 high 1 (Tasks), the owner's decision, and the completeness
    /// sweep's G22: an arrow in the review's filter group commits the filter
    /// it reaches as ONE spoken outcome. The destination is CHECKED when it
    /// takes the keys, no authored line ("Filter set to …") is posted on top
    /// of the focus speech, and no chip is renamed while it holds the keys
    /// except by a count arriving that was published: the chip the arrow
    /// leaves keeps its name until the new page publishes, and a chip whose
    /// filter was published before names that total from the start — never
    /// the "0 tasks" of the page cleared for the load.
    /// </summary>
    [Fact]
    public void AnArrowInTheReviewFiltersCommitsAsOneUtterance() => RunSta(() =>
    {
        using var host = new Host();
        // The review's loads on the pool, publishing through the dispatcher,
        // as in the app: the check, the focus and the publication are three
        // moments, and what a chip is named between them is the fact.
        host.Initialize("tasksReview", backgroundWork: true, ("todo.md", "- [ ] first\n- [ ] second\n"));
        TasksReviewViewModel review = host.Workspace.TasksReview;
        review.EnsureLoaded();
        Assert.True(PumpedDispatcher.PumpUntil(() => review.Rows.Count == 2 && !review.IsLoading), "premise: the review never loaded.");
        RadioButton all = host.ElementWithId<RadioButton>("PanelReviewFilterAll");
        RadioButton dueToday = host.ElementWithId<RadioButton>("PanelReviewFilterDueToday");
        Assert.Equal("All, 2 tasks", AutomationProperties.GetName(all));
        Assert.True(all.Focus());
        var checkedWhenFocused = new List<string>();
        var renamedWhileFocused = new List<string>();
        foreach (RadioButton radio in new[] { all, dueToday })
        {
            radio.GotKeyboardFocus += (_, _) => checkedWhenFocused.Add($"{radio.Name}={radio.IsChecked}");
            DependencyPropertyDescriptor.FromProperty(AutomationProperties.NameProperty, typeof(RadioButton))
                .AddValueChanged(radio, (_, _) =>
                {
                    if (radio.IsKeyboardFocused)
                    {
                        renamedWhileFocused.Add(AutomationProperties.GetName(radio));
                    }
                });
        }

        host.ForgetFocusAndSpeech();

        host.Press(Key.Right);
        Assert.True(PumpedDispatcher.PumpUntil(() => !review.IsLoading), "the Due today page never published.");
        PumpedDispatcher.Drain();

        Assert.Same(dueToday, Keyboard.FocusedElement);
        Assert.Equal(TaskReviewFilter.DueToday, review.ActiveFilter);
        Assert.Equal(["PanelReviewFilterDueToday=True"], checkedWhenFocused);
        Assert.True(host.Announced.Count == 0, $"the arrow posted authored lines: {host.Order}");

        // Back: All's total was published before, so its chip names it from
        // the moment it is checked — before it takes the keys.
        checkedWhenFocused.Clear();
        host.Press(Key.Left);
        Assert.True(PumpedDispatcher.PumpUntil(() => !review.IsLoading), "the All page never published.");
        PumpedDispatcher.Drain();

        Assert.Same(all, Keyboard.FocusedElement);
        Assert.Equal(["PanelReviewFilterAll=True"], checkedWhenFocused);
        Assert.True(host.Announced.Count == 0, $"the arrow posted authored lines: {host.Order}");
        // The only rename under the keys is Due today's first count
        // arriving — a published one. "All" was never renamed while it
        // held them, going or coming back.
        Assert.Equal(["Due today, 0 tasks"], renamedWhileFocused);
    });

    /// <summary>The owner's S2 and the completeness sweep's G2: the
    /// Bibliography segments are a Windows radio group. Right on "Entries"
    /// moved focus to "Unresolved" without checking it, the Entries grid
    /// stayed on screen, and the next Right left the leaf. Now the arrow
    /// checks the segment it reaches before it takes the keys, the leaf
    /// switches to it, nothing is announced on top (segment switches never
    /// are, §2.6), and every further arrow stays in the group.</summary>
    [Fact]
    public void AnArrowOnTheBibliographySegmentsSwitchesTheSegment() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize("bibliography", ("a.md", "# A\n"));
        host.AttachWorkspaceToTheWindow();
        RadioButton entries = host.ElementWithId<RadioButton>("BibliographySegmentEntries");
        RadioButton unresolved = host.ElementWithId<RadioButton>("BibliographySegmentUnresolved");
        Assert.True(entries.IsChecked, "premise: Entries is not the checked segment.");
        Assert.True(entries.Focus());
        bool? checkedWhenFocused = null;
        unresolved.GotKeyboardFocus += (_, _) => checkedWhenFocused ??= unresolved.IsChecked;
        host.ForgetFocusAndSpeech();

        host.Press(Key.Right);

        Assert.Same(unresolved, Keyboard.FocusedElement);
        Assert.True(checkedWhenFocused, "the segment took the keys unchecked");
        Assert.Equal(Panels.BibliographySegment.Unresolved, host.Workspace.Bibliography.Segment);
        Assert.True(host.Announced.Count == 0, $"the arrow posted authored lines: {host.Order}");

        host.Press(Key.Right);
        Assert.Same(entries, Keyboard.FocusedElement);
        Assert.Equal(Panels.BibliographySegment.Entries, host.Workspace.Bibliography.Segment);
    });

    /// <summary>The History segments likewise (S2, G2): Right on "This note"
    /// focused "Deleted" unchecked while the version list stayed, and from
    /// either end an arrow walked out of the leaf. The arrow checks the
    /// segment it reaches first, the view switches, and the next arrow wraps
    /// in the group.</summary>
    [Fact]
    public void AnArrowOnTheHistorySegmentsSwitchesTheSegment() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize("history", ("a.md", "# A\n"));
        RadioButton thisNote = host.ElementWithId<RadioButton>("HistorySegmentThisNote");
        RadioButton deleted = host.ElementWithId<RadioButton>("HistorySegmentDeleted");
        Panels.HistorySurfaceView view = host.Descendant<Panels.HistorySurfaceView>();
        Assert.True(thisNote.Focus());
        bool? checkedWhenFocused = null;
        deleted.GotKeyboardFocus += (_, _) => checkedWhenFocused ??= deleted.IsChecked;
        host.ForgetFocusAndSpeech();

        host.Press(Key.Right);

        Assert.Same(deleted, Keyboard.FocusedElement);
        Assert.True(checkedWhenFocused, "the segment took the keys unchecked");
        Assert.True(DeletedSegmentActive(view), "the view did not switch to the Deleted segment");
        Assert.True(host.Announced.Count == 0, $"the arrow posted authored lines: {host.Order}");

        host.Press(Key.Right);
        Assert.Same(thisNote, Keyboard.FocusedElement);
        Assert.False(DeletedSegmentActive(view));
        Assert.True(host.VisibleLeafBody().IsKeyboardFocusWithin);
    });

    /// <summary>The completeness sweep's G15: a leaf's landing on a radio
    /// group is its CHECKED radio. Ctrl+R with "Overdue" checked put the
    /// reader on the unchecked "All", and the next arrow committed a filter
    /// one step from the wrong place.</summary>
    [Fact]
    public void ALeafRevealLandsOnTheCheckedFilter() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize("tasksReview", ("todo.md", "- [ ] late one 📅 2020-01-01\n"));
        TasksReviewViewModel review = host.Workspace.TasksReview;
        review.EnsureLoaded();
        review.OverdueFilterActive = true;
        Assert.True(PumpedDispatcher.PumpUntil(() => !review.IsLoading && review.OverdueFilterActive), "premise: the Overdue page never published.");
        RadioButton overdue = host.ElementWithId<RadioButton>("PanelReviewFilterOverdue");
        Assert.True(overdue.IsChecked);

        host.Shell.LandInRightPane();

        Assert.Same(overdue, Keyboard.FocusedElement);
    });

    private static bool DeletedSegmentActive(Panels.HistorySurfaceView view) =>
        (bool)(typeof(Panels.HistorySurfaceView).GetField("_deletedSegmentActive", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("_deletedSegmentActive is gone")).GetValue(view)!;

    public static TheoryData<string, string, Key> LeafStops()
    {
        var data = new TheoryData<string, string, Key>();
        foreach ((string leaf, string stop) in new[]
                 {
                     ("connections", "ConnectionsLeafSurface"),
                     ("queries", "QueriesNewDashboard"),
                     ("queries", "QueriesRefresh"),
                     ("bibliography", "BibliographySearch"),
                 })
        {
            foreach (Key key in new[] { Key.Right, Key.Left, Key.Up, Key.Down })
            {
                data.Add(leaf, stop, key);
            }
        }

        return data;
    }

    /// <summary>
    /// The owner's S1 and the completeness sweep's G1: every leaf body is a
    /// boundary that keeps its arrows. Seven bodies were not: from the
    /// Connections anchor (the leaf with no note, the F4 transcript site)
    /// Right reached the rail and Left the "Resize right pane" splitter;
    /// from Queries' header buttons and the Bibliography search field an
    /// arrow walked out the same way. From each stop, each arrow keeps the
    /// keys in the leaf.
    /// </summary>
    [Theory]
    [MemberData(nameof(LeafStops))]
    public void EveryArrowFromALeafStopStaysInTheLeaf(string leaf, string stop, Key key) => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize(leaf, ("a.md", "# A\n"));
        FrameworkElement body = host.VisibleLeafBody();
        body.UpdateLayout();
        if (host.Shell.FindName(stop) is Graph.ConnectionsLeafView connections)
        {
            Assert.True(connections.FocusAnchor(), "premise: the Connections anchor refused the keys.");
        }
        else
        {
            UIElement element = host.ElementWithId<UIElement>(stop);
            Assert.True(element.Focus(), $"premise: {stop} refused the keys.");
        }

        IInputElement from = Keyboard.FocusedElement;
        host.ForgetFocusAndSpeech();

        host.Press(key);

        Assert.True(
            body.IsKeyboardFocusWithin,
            $"{key} from {Describe(from)} left the {leaf} leaf, to {Describe(Keyboard.FocusedElement)}; order: {host.Order}");
    });

    /// <summary>Round 6 high 4 by its keyboard route (the repro's R6_4,
    /// inverted; the owner's point fix 4): under the Overdue filter the only
    /// row is toggled done (Space) and page one re-queries to ZERO rows, so
    /// the focused row goes away under the reader. With the review body a
    /// boundary, every arrow from wherever the publication left the keys
    /// stays in the leaf.</summary>
    [Theory]
    [InlineData(Key.Right)]
    [InlineData(Key.Left)]
    [InlineData(Key.Up)]
    [InlineData(Key.Down)]
    public void AReviewEmptiedUnderTheReaderKeepsItsArrowsInTheLeaf(Key key) => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize("tasksReview", ("todo.md", "- [ ] late one 📅 2020-01-01\n"));
        host.AttachWorkspaceToTheWindow();
        TasksReviewViewModel review = host.Workspace.TasksReview;
        review.EnsureLoaded();
        Assert.True(PumpedDispatcher.PumpUntil(() => review.Rows.Count == 1), "premise: the review never loaded its row.");
        review.OverdueFilterActive = true;
        Assert.True(PumpedDispatcher.PumpUntil(() => review.Rows.Count == 1 && review.OverdueFilterActive), "premise: the Overdue filter lost the row.");
        ListBox list = host.ElementWithId<ListBox>("PanelReviewList");
        list.SelectedIndex = 0;
        list.UpdateLayout();
        Assert.True(((UIElement)list.ItemContainerGenerator.ContainerFromIndex(0)).Focus());
        host.ForgetFocusAndSpeech();

        host.Press(Key.Space);
        Assert.True(
            PumpedDispatcher.PumpUntil(() => review.Rows.Count == 0
                && File.ReadAllText(host.PathOf("todo.md")).Contains("- [x] late one", StringComparison.Ordinal)),
            "premise: the toggle never emptied the Overdue page.");
        PumpedDispatcher.Drain();
        IInputElement afterPublication = Keyboard.FocusedElement;

        host.Press(key);

        Assert.True(
            host.VisibleLeafBody().IsKeyboardFocusWithin,
            $"after the emptying publication the keys were on {Describe(afterPublication)}; {key} then left the leaf, to {Describe(Keyboard.FocusedElement)}; order: {host.Order}");
    });

    private static string Describe(IInputElement? element) => element switch
    {
        null => "nothing",
        FrameworkElement framework => $"{framework.GetType().Name} '{AutomationProperties.GetAutomationId(framework)}' {AutomationProperties.GetName(framework)}".TrimEnd(),
        _ => element.GetType().Name,
    };

    private sealed class Host : IDisposable
    {
        private readonly FixtureVault _fixture = FixtureVault.Create(0, "pr4-leaf-region");
        private readonly Func<bool> _priorOverlayProbe = CanvasSurfaceView.ShellOverlayIsOpen;
        private readonly List<string> _order = [];
        private VaultSession? _session;
        private WorkspaceViewModel? _workspace;
        private Window? _window;
        private Grid _leafHost = null!;

        public MainWindow Shell { get; private set; } = null!;

        public WorkspaceViewModel Workspace => _workspace!;

        public List<A11yEvent> Announced { get; } = [];

        public string Order => string.Join(" → ", _order);

        public void Log(string entry) => _order.Add(entry);

        public string PathOf(string relative) => Path.Combine(_fixture.Root, relative);

        public void Initialize(string leaf, params (string Path, string Text)[] files) =>
            Initialize(leaf, backgroundWork: false, files);

        /// <param name="backgroundWork">The panels' work on the pool,
        /// publishing through the dispatcher as in the app; false runs it
        /// inline.</param>
        public void Initialize(string leaf, bool backgroundWork, params (string Path, string Text)[] files)
        {
            Assert.Null(Application.Current);
            foreach ((string relative, string text) in files)
            {
                File.WriteAllText(Path.Combine(_fixture.Root, relative), text);
            }

            _session = VaultSession.OpenFilesystem(_fixture.Root);
            using (var cancel = new CancelToken())
            {
                _session.ScanInitial(cancel);
            }

            _workspace = new WorkspaceViewModel(
                _session,
                _fixture.Root,
                () => [],
                line =>
                {
                    Announced.Add(line);
                    _order.Add("announce " + line.GetType().Name);
                },
                startInteractionBackgroundWork: backgroundWork);
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

            foreach ((string text, int row, int column) in new[] { ("Above", 0, 1), ("Below", 2, 1), ("Left", 1, 0), ("Right", 1, 2) })
            {
                var button = new Button { Content = text };
                Grid.SetRow(button, row);
                Grid.SetColumn(button, column);
                grid.Children.Add(button);
            }

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
            Keyboard.AddGotKeyboardFocusHandler(_window, (_, e) => _order.Add("focus " + Describe(e.NewFocus)));
            Keyboard.AddLostKeyboardFocusHandler(_window, (_, e) =>
            {
                if (e.NewFocus is null or Window || (e.NewFocus is Visual visual && PresentationSource.FromVisual(visual) is null))
                {
                    _order.Add("stranded on " + Describe(e.NewFocus));
                }
            });
        }

        /// <summary>A saved query over the vault's base, as BasesQueriesTests
        /// seeds one.</summary>
        public string SaveQuery(string name)
        {
            ulong scratch = _session!.OpenBase("Notes.base");
            string json;
            try
            {
                json = _session.BaseViewQueryJson(scratch, 0);
            }
            finally
            {
                _session.CloseBase(scratch);
            }

            return _session.SaveQuery(name, description: null, json, SavedQuerySourceSyntax.Builder);
        }

        /// <summary>The window's Bases handlers reach the workspace through
        /// the lifecycle model, as in the app.</summary>
        public void AttachWorkspaceToTheWindow()
        {
            var lifecycle = Assert.IsType<VaultLifecycleViewModel>(Shell.DataContext);
            (typeof(VaultLifecycleViewModel).GetProperty(nameof(VaultLifecycleViewModel.Workspace))
                ?? throw new InvalidOperationException("Workspace is gone"))
                .SetValue(lifecycle, _workspace);
        }

        public void ForgetFocusAndSpeech()
        {
            _order.Clear();
            Announced.Clear();
        }

        public T ElementWithId<T>(string automationId)
            where T : DependencyObject =>
            Descendants(_leafHost).OfType<T>().First(element => AutomationProperties.GetAutomationId(element) == automationId
                || (element is FrameworkElement { Name: { } name } && name == automationId));

        public T Descendant<T>()
            where T : DependencyObject =>
            Descendants(_leafHost).OfType<T>().First();

        public FrameworkElement VisibleLeafBody() =>
            _leafHost.Children.OfType<FrameworkElement>().Single(body => Grid.GetColumn(body) == 0 && body.IsVisible);

        public void Press(Key key)
        {
            InputManager.Current.ProcessInput(new KeyEventArgs(
                Keyboard.PrimaryDevice, PresentationSource.FromVisual(_window!)!, Environment.TickCount, key)
            {
                RoutedEvent = Keyboard.PreviewKeyDownEvent,
            });
            PumpedDispatcher.Drain();
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
