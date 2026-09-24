// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SlateWindows.Canvas;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W7-7 PR 4 (#1247, contract R-5; spec review round 23): the Files
/// region's landing, on the shipped Files pane. Nine landings in the window
/// were <c>FilesTree.Focus()</c> — the ring's, the Files boundary's, a
/// rename's, a mutation's restore, Move To's, the empty editor's last
/// resort — and all of them now go through
/// <see cref="MainWindow.LandOnFilesTree"/>: a row, the selected file's else
/// the first, and the filter field when no row can take the keys. The pane
/// is the real one — built by the window's own XAML, bound to a sidebar
/// over a real vault — lifted into a window of its own with a button above
/// it and one beside it, so an arrow that leaves the region lands
/// somewhere observable. Keys are real presses through the input manager.
/// </summary>
public sealed class FilesRegionLandingTests
{
    [Fact]
    public void WithNothingSelectedTheFilesTreeLandsOnItsFirstRow() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize();
        FileTreeNodeViewModel first = host.Sidebar.RootNodes[0];
        Assert.True(host.Above.Focus());

        Assert.True(host.Shell.LandOnFilesTree());

        Assert.Same(first, FocusedNode());
        host.AssertTreeNeverFocused();
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

        Assert.True(host.Shell.LandOnFilesTree());

        Assert.Same(selected, FocusedNode());
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

        Assert.Same(host.Shell.FindName("SidebarFilterTextBox"), Keyboard.FocusedElement);
        host.AssertTreeNeverFocused();
    });

    /// <summary>The arrow witness for the nine landings: from the landed
    /// row — and from the filter field, the fallback — each arrow keeps the
    /// keys in the Files region.</summary>
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
        IInputElement landed = Keyboard.FocusedElement;

        host.Press(key);

        Assert.True(
            host.Pane.IsKeyboardFocusWithin,
            $"{key} took the keys out of the Files region, from {landed} to {Keyboard.FocusedElement}");
        host.AssertTreeNeverFocused();
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
        private VaultSession? _session;
        private Window? _window;

        public MainWindow Shell { get; private set; } = null!;

        public FilesSidebarViewModel Sidebar { get; private set; } = null!;

        public FrameworkElement Pane { get; private set; } = null!;

        public TreeView Tree { get; private set; } = null!;

        public Button Above { get; private set; } = null!;

        public void Initialize()
        {
            Assert.Null(Application.Current);
            _session = VaultSession.OpenFilesystem(_fixture.Root);
            using (var cancel = new CancelToken())
            {
                _session.ScanInitial(cancel);
            }

            Sidebar = new FilesSidebarViewModel(_session, _ => { }, localAppDataRoot: _fixture.Root);
            PumpedDispatcher.PumpUntilDrained(Sidebar.TreeRefreshCompletion);
            Assert.True(Sidebar.RootNodes.Count >= 3, "premise: the vault's notes are the tree's rows.");

            Shell = new MainWindow();
            var lifecycle = Assert.IsType<VaultLifecycleViewModel>(Shell.DataContext);
            SetState(lifecycle, nameof(VaultLifecycleViewModel.FileSidebar), Sidebar);
            Pane = Assert.IsAssignableFrom<FrameworkElement>(Shell.FindName("FilesPaneBorder"));
            Tree = Assert.IsType<TreeView>(Shell.FindName("FilesTree"));
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
                new KeyboardFocusChangedEventHandler((_, e) => _focusChanges.Add(e.NewFocus)),
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

        public void AssertTreeNeverFocused() =>
            Assert.True(
                !_focusChanges.Any(focus => ReferenceEquals(focus, Tree)),
                "the bare Files tree took the keys; focus went "
                + string.Join(" → ", _focusChanges.Select(focus => focus.GetType().Name)));

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
