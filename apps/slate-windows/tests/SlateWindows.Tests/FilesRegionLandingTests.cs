// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SlateWindows.Canvas;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W7-7 PR 4 (#1247, contract R-5; spec review round 23; codex round 5):
/// the Files region's landing, on the shipped Files pane. Nine landings in
/// the window were <c>FilesTree.Focus()</c> — the ring's, the Files
/// boundary's, a rename's, a mutation's restore, Move To's, the empty
/// editor's last resort — and all of them, and a restore whose token is the
/// tree, now go through <see cref="MainWindow.LandOnFilesTree"/>: the
/// selected file's row; else the region's stable stop, the filter field
/// (R-5's "else the region's stable stop"). Never the bare tree, a
/// populated container, and never a row that is not selected: a row selects
/// itself as it takes the keys, and selecting a file opens it (OD-2). The
/// pane is the real one — built by the window's own XAML, bound to a
/// sidebar over a real vault — lifted into a window of its own with a
/// button above it and one beside it, so an arrow that leaves the region
/// lands somewhere observable. Keys are real presses through the input
/// manager.
/// </summary>
public sealed class FilesRegionLandingTests
{
    /// <summary>Codex round 5's ruling: with nothing selected there is no
    /// row to land on — a row would select itself as it took the keys, and
    /// selecting a file opens it — and the bare tree is a populated
    /// container R-5 names, so the landing is the region's stable stop,
    /// the filter field. It selects nothing, opens nothing, says nothing,
    /// and the tree never holds the keys.</summary>
    [Fact]
    public void WithNothingSelectedTheFilterFieldIsTheStop() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize();
        var opened = new List<string>();
        host.Sidebar.OpenTargetRequested += (_, request) => opened.Add(request.Path);
        Assert.True(host.Above.Focus());
        host.Announced.Clear();

        Assert.True(host.Shell.LandOnFilesTree());

        Assert.Same(host.FilterField, Keyboard.FocusedElement);
        Assert.Null(host.Sidebar.SelectedNode);
        Assert.DoesNotContain(host.Sidebar.RootNodes, node => node.IsSelected);
        Assert.Empty(opened);
        Assert.Empty(host.Announced);
        host.AssertTreeNeverFocused();
    });

    /// <summary>An overlay's restore token captured on the bare tree — where
    /// a click on its empty area leaves the keys — restores through the
    /// Files region's own landing (the window's registration with
    /// <see cref="SelectorFocus.SetOwnLanding"/>): nothing selected, so the
    /// filter field, never the bare tree again and never a row. Nothing is
    /// selected, opened or said.</summary>
    [Fact]
    public void ARestoreTokenOnTheBareFilesTreeLandsOnTheFilterField() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize();
        var opened = new List<string>();
        host.Sidebar.OpenTargetRequested += (_, request) => opened.Add(request.Path);
        Assert.True(host.Tree.Focus());
        IInputElement token = Keyboard.FocusedElement;
        Assert.Same(host.Tree, token);
        Assert.True(host.Above.Focus());
        host.ForgetFocusChanges();
        host.Announced.Clear();

        Assert.True(host.Shell.TryFocus(token));

        Assert.Same(host.FilterField, Keyboard.FocusedElement);
        Assert.Null(host.Sidebar.SelectedNode);
        Assert.DoesNotContain(host.Sidebar.RootNodes, node => node.IsSelected);
        Assert.Empty(opened);
        Assert.Empty(host.Announced);
        host.AssertTreeNeverFocused();
    });

    /// <summary>Codex round 5: the Tags tree's selection APPLIES a tag
    /// filter (R-3). A restore token captured on the bare Tags tree lands on
    /// its selected tag, else the Files region's stable stop — never on a
    /// first tag, whose focus would select it and filter the files.</summary>
    [Fact]
    public void ARestoreTokenOnTheBareTagsTreeAppliesNoTag() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize(tagged: true);
        host.Sidebar.ShowTags = true;
        host.Pane.UpdateLayout();
        TreeView tags = host.ElementWithId<TreeView>("SidebarTagTree");
        Assert.True(PumpedDispatcher.PumpUntil(() => tags.HasItems), "premise: the Tags tree never listed the fixture's tag.");
        host.Pane.UpdateLayout();
        Assert.True(tags.Focus());
        IInputElement token = Keyboard.FocusedElement;
        Assert.Same(tags, token);
        Assert.True(host.Above.Focus());
        host.Announced.Clear();

        Assert.True(host.Shell.TryFocus(token));
        PumpedDispatcher.Drain();

        Assert.Same(host.FilterField, Keyboard.FocusedElement);
        Assert.Null(tags.SelectedItem);
        Assert.False(host.Sidebar.IsFilterActive, "the restore applied a tag filter.");
        Assert.Empty(host.Announced);
    });

    [Fact]
    public void TheSelectedFilesRowTakesTheKeys() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize();
        FileTreeNodeViewModel selected = host.Sidebar.RootNodes[^1];
        selected.IsSelected = true;
        host.Sidebar.SelectedNode = selected;
        host.Pane.UpdateLayout();
        Assert.True(host.Above.Focus());
        var opened = new List<string>();
        host.Sidebar.OpenTargetRequested += (_, request) => opened.Add(request.Path);
        host.Announced.Clear();

        Assert.True(host.Shell.LandOnFilesTree());

        Assert.Same(selected, FocusedNode());
        Assert.Same(selected, host.Sidebar.SelectedNode);
        Assert.Empty(opened);
        Assert.Empty(host.Announced);
        host.AssertTreeNeverFocused();
    });

    /// <summary>Codex rounds 4-5: a selected file beneath a COLLAPSED folder
    /// has no row to land on. The landing must not change what is selected,
    /// expand the folder or open anything — F6 is navigation, not a
    /// selection — so the keys go to the region's stable stop, the filter
    /// field, and the selection stays on the hidden note.</summary>
    [Fact]
    public void AHiddenSelectedFileLandsOnTheFilterFieldAndKeepsItsSelection() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize(nested: true);
        FileTreeNodeViewModel folder = Assert.Single(host.Sidebar.RootNodes, node => node.IsDirectory);
        folder.IsExpanded = true;
        Assert.True(
            PumpedDispatcher.PumpUntil(() => folder.Children.Any(child => child is { IsPlaceholder: false, IsDirectory: false })),
            "premise: the folder's note never loaded.");
        FileTreeNodeViewModel inner = folder.Children.Single(child => child is { IsPlaceholder: false, IsDirectory: false });
        host.Pane.UpdateLayout();
        folder.IsExpanded = false;
        host.Pane.UpdateLayout();
        // Selected while its folder is collapsed — the sidebar's own route
        // when a filter result is chosen and the filter then cleared. (A
        // collapse through the tree moves WPF's selection to the folder, so
        // the collapse comes first.)
        inner.IsSelected = true;
        host.Sidebar.SelectedNode = inner;
        host.Pane.UpdateLayout();
        PumpedDispatcher.Drain();
        Assert.True(host.Above.Focus());
        Assert.Same(inner, host.Sidebar.SelectedNode);
        Assert.False(folder.IsExpanded);
        var opened = new List<string>();
        host.Sidebar.OpenTargetRequested += (_, request) => opened.Add(request.Path);
        host.Announced.Clear();
        host.ForgetFocusChanges();

        Assert.True(host.Shell.LandOnFilesTree());
        host.Pane.UpdateLayout();
        PumpedDispatcher.Drain();

        Assert.Same(host.FilterField, Keyboard.FocusedElement);
        Assert.Same(inner, host.Sidebar.SelectedNode);
        Assert.False(folder.IsSelected, "the landing selected the collapsed folder.");
        Assert.False(folder.IsExpanded, "the landing expanded the collapsed folder.");
        Assert.Empty(opened);
        Assert.Empty(host.Announced);
        host.AssertTreeNeverFocused();
    });

    /// <summary>With the filter active the tree is replaced by the results
    /// and no row of it can take the keys: the region's stable stop, the
    /// filter field, does.</summary>
    [Fact]
    public void AFilesTreeTheFilterReplacedLandsOnTheFilterField() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize();
        host.Sidebar.FilterText = "note";
        host.Pane.UpdateLayout();
        Assert.False(host.Tree.IsVisible);
        Assert.True(host.Above.Focus());

        Assert.True(host.Shell.LandOnFilesTree());

        Assert.Same(host.FilterField, Keyboard.FocusedElement);
        host.AssertTreeNeverFocused();
    });

    /// <summary>The arrow witness for the landing with no row: the filter
    /// field — whether the tree shows with nothing selected or the filter
    /// has replaced it — keeps every arrow.</summary>
    [Theory]
    [InlineData(Key.Left, false)]
    [InlineData(Key.Right, false)]
    [InlineData(Key.Up, false)]
    [InlineData(Key.Down, false)]
    [InlineData(Key.Left, true)]
    [InlineData(Key.Right, true)]
    [InlineData(Key.Up, true)]
    [InlineData(Key.Down, true)]
    public void FromTheFilesLandingEveryArrowStaysInTheRegion(Key key, bool filtered) => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize();
        if (filtered)
        {
            host.Sidebar.FilterText = "note";
            host.Pane.UpdateLayout();
        }

        Assert.True(host.Above.Focus());
        Assert.True(host.Shell.LandOnFilesTree());
        Assert.Same(host.FilterField, Keyboard.FocusedElement);

        host.Press(key);

        Assert.True(
            ReferenceEquals(host.FilterField, Keyboard.FocusedElement),
            $"{key} took the keys off the filter field, to {Keyboard.FocusedElement}");
    });

    /// <summary>The arrow witness for the landing with a file selected: from
    /// the selected file's row each arrow keeps the keys in the tree — at
    /// the first row and a root (Up, Left) and at the last row and a leaf
    /// (Down, Right), where the tree has no row further to go. There the
    /// arrow reaches the row's own check box (the batch-trash mark), which
    /// is inside the row, never out of the region.</summary>
    [Theory]
    [InlineData(Key.Left, 0)]
    [InlineData(Key.Up, 0)]
    [InlineData(Key.Right, -1)]
    [InlineData(Key.Down, -1)]
    public void FromTheSelectedFilesRowEveryArrowStaysInTheRegion(Key key, int row) => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize();
        FileTreeNodeViewModel selected = row < 0 ? host.Sidebar.RootNodes[^1] : host.Sidebar.RootNodes[row];
        selected.IsSelected = true;
        host.Sidebar.SelectedNode = selected;
        host.Pane.UpdateLayout();
        Assert.True(host.Above.Focus());
        Assert.True(host.Shell.LandOnFilesTree());
        Assert.Same(selected, FocusedNode());

        host.Press(key);

        Assert.True(
            host.Tree.IsKeyboardFocusWithin,
            $"{key} took the keys out of the Files tree, to {Keyboard.FocusedElement}");
    });

    /// <summary>Codex round 5: the filter's result list is its own stop
    /// while it is empty (AR-6) — the ring's Files landing with the filter
    /// active and nothing found yet. Results published under the keys land
    /// them on the first result's row, unselected (a selection would open
    /// the note), never left on the bare populated list.</summary>
    [Fact]
    public void ResultsPublishedUnderTheKeysLandThemOnARow() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize();
        var opened = new List<string>();
        host.Sidebar.OpenTargetRequested += (_, request) => opened.Add(request.Path);
        var results = Assert.IsType<ListBox>(host.Shell.FindName("FilterResultsList"));
        host.Sidebar.FilterText = "zzz-nothing-matches";
        Assert.True(
            PumpedDispatcher.PumpUntil(() => host.Sidebar.IsFilterActive && results.IsVisible && !results.HasItems),
            "premise: the filter never showed its empty result list.");
        host.Pane.UpdateLayout();
        Assert.True(results.Focus());
        host.ForgetFocusChanges();
        host.Announced.Clear();

        host.Sidebar.FilterText = "note";
        Assert.True(PumpedDispatcher.PumpUntil(() => results.HasItems), "the results never published.");
        PumpedDispatcher.Drain();

        Assert.IsType<ListBoxItem>(Keyboard.FocusedElement);
        Assert.Same(results.ItemContainerGenerator.ContainerFromIndex(0), Keyboard.FocusedElement);
        Assert.Equal(-1, results.SelectedIndex);
        Assert.Empty(opened);
        host.AssertNeverFocusedPopulated(results);
    });

    private static FileTreeNodeViewModel? FocusedNode() =>
        (Keyboard.FocusedElement as TreeViewItem)?.DataContext as FileTreeNodeViewModel;

    private static void SetState(object target, string name, object? value) =>
        (target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Missing state property: {name}"))
        .SetValue(target, value);

    /// <summary>The shipped window's Files pane over a three-note vault,
    /// lifted into a shown window of its own. No Application and no shown
    /// MainWindow (the MoveToFocusTests fixture's reasons): its constructor
    /// still builds the shipped XAML and wires the pane's bindings.</summary>
    private sealed class Host : IDisposable
    {
        private readonly FixtureVault _fixture = FixtureVault.Create(3, "files-landing");
        private readonly Func<bool> _priorOverlayProbe = CanvasSurfaceView.ShellOverlayIsOpen;
        private readonly List<IInputElement> _focusChanges = [];
        private readonly List<IInputElement> _populatedFocus = [];
        private VaultSession? _session;
        private Window? _window;

        public MainWindow Shell { get; private set; } = null!;

        public FilesSidebarViewModel Sidebar { get; private set; } = null!;

        public FrameworkElement Pane { get; private set; } = null!;

        public TreeView Tree { get; private set; } = null!;

        public TextBox FilterField { get; private set; } = null!;

        public Button Above { get; private set; } = null!;

        public List<A11yEvent> Announced { get; } = [];

        /// <param name="nested">Adds a folder holding one note, so a selected
        /// file can sit beneath a collapsed row.</param>
        /// <param name="tagged">Adds a note carrying a tag, so the Tags tree
        /// has a row.</param>
        public void Initialize(bool nested = false, bool tagged = false)
        {
            Assert.Null(Application.Current);
            if (nested)
            {
                Directory.CreateDirectory(Path.Combine(_fixture.Root, "folder"));
                File.WriteAllText(Path.Combine(_fixture.Root, "folder", "inner.md"), "# Inner\n");
            }

            if (tagged)
            {
                File.WriteAllText(Path.Combine(_fixture.Root, "tagged.md"), "# Tagged\n\n#alpha\n");
            }

            _session = VaultSession.OpenFilesystem(_fixture.Root);
            using (var cancel = new CancelToken())
            {
                _session.ScanInitial(cancel);
            }

            Sidebar = new FilesSidebarViewModel(_session, Announced.Add, localAppDataRoot: _fixture.Root);
            PumpedDispatcher.PumpUntilDrained(Sidebar.TreeRefreshCompletion);
            Assert.True(Sidebar.RootNodes.Count >= 3, "premise: the vault's notes are the tree's rows.");

            Shell = new MainWindow();
            var lifecycle = Assert.IsType<VaultLifecycleViewModel>(Shell.DataContext);
            SetState(lifecycle, nameof(VaultLifecycleViewModel.FileSidebar), Sidebar);
            Pane = Assert.IsAssignableFrom<FrameworkElement>(Shell.FindName("FilesPaneBorder"));
            Tree = Assert.IsType<TreeView>(Shell.FindName("FilesTree"));
            FilterField = Assert.IsType<TextBox>(Shell.FindName("SidebarFilterTextBox"));
            Assert.IsAssignableFrom<Panel>(Pane.Parent).Children.Remove(Pane);

            Above = new Button { Content = "Above" };
            var beside = new Button { Content = "Beside" };
            var root = new DockPanel { DataContext = lifecycle };
            DockPanel.SetDock(Above, Dock.Top);
            DockPanel.SetDock(beside, Dock.Right);
            DockPanel.SetDock(Pane, Dock.Left);
            root.Children.Add(Above);
            root.Children.Add(beside);
            root.Children.Add(Pane);
            _window = new Window
            {
                Content = root,
                Width = 700,
                Height = 600,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                ShowActivated = false,
            };
            _window.Show();
            _window.UpdateLayout();
            PumpedDispatcher.Drain();
            Assert.True(Tree.IsVisible, "premise: the lifted pane shows its tree.");
            _window.AddHandler(
                Keyboard.GotKeyboardFocusEvent,
                new KeyboardFocusChangedEventHandler((_, e) =>
                {
                    _focusChanges.Add(e.NewFocus);
                    if (e.NewFocus is ItemsControl { HasItems: true })
                    {
                        _populatedFocus.Add(e.NewFocus);
                    }
                }),
                handledEventsToo: true);
        }

        public void Press(Key key)
        {
            InputManager.Current.ProcessInput(new KeyEventArgs(
                Keyboard.PrimaryDevice, PresentationSource.FromVisual(_window!)!, Environment.TickCount, key)
            {
                RoutedEvent = Keyboard.PreviewKeyDownEvent,
            });
            PumpedDispatcher.Drain();
        }

        public T ElementWithId<T>(string automationId)
            where T : DependencyObject =>
            Descendants(Pane).OfType<T>().Single(element => AutomationProperties.GetAutomationId(element) == automationId);

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

        public void AssertTreeNeverFocused() =>
            Assert.True(
                !_focusChanges.Any(focus => ReferenceEquals(focus, Tree)),
                "the bare Files tree took the keys; focus went "
                + string.Join(" → ", _focusChanges.Select(focus => focus.GetType().Name)));

        /// <summary>The list never held the keys itself while it had rows —
        /// judged at each focus change, by the rows it had then.</summary>
        public void AssertNeverFocusedPopulated(ItemsControl list) =>
            Assert.True(
                !_populatedFocus.Contains(list),
                "a populated list took the keys itself; focus went "
                + string.Join(" → ", _focusChanges.Select(focus => focus.GetType().Name)));

        /// <summary>Forget the focus changes so far — a fact's own setup
        /// may put the keys anywhere.</summary>
        public void ForgetFocusChanges()
        {
            _focusChanges.Clear();
            _populatedFocus.Clear();
        }

        public void Dispose()
        {
            var failures = new List<Exception>();
            void CleanUp(Action action)
            {
                try
                {
                    action();
                }
                catch (Exception exception)
                {
                    failures.Add(exception);
                }
            }

            try
            {
                if (Shell?.DataContext is VaultLifecycleViewModel lifecycle)
                {
                    CleanUp(() => SetState(lifecycle, nameof(VaultLifecycleViewModel.FileSidebar), null));
                }

                if (Sidebar is not null)
                {
                    CleanUp(() => PumpedDispatcher.PumpUntilDrained(Sidebar.BeginSessionShutdownAndCaptureWork().SessionWork));
                }

                CleanUp(() => _window?.Close());
                CleanUp(() => Shell?.Close());
                CleanUp(() => _session?.Dispose());
                CleanUp(_fixture.Dispose);
            }
            finally
            {
                CanvasSurfaceView.ShellOverlayIsOpen = _priorOverlayProbe;
            }

            if (failures.Count > 0)
            {
                throw new AggregateException("Files landing fixture cleanup failed.", failures);
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
        Assert.True(thread.Join(TimeSpan.FromSeconds(120)), "Files landing fixture timed out.");
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
