// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

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
/// W7-7 PR 4 (#1247, contract R-5; the owner's S1 and the completeness
/// sweep's G8): a sheet keeps its arrows. The shell's overlays are focus
/// scopes that fence Tab (R-6) but had no directional boundary, and the
/// shell behind them stays enabled: Right on a sheet's right-most button,
/// Down on its bottom row or Up on its first field was judged in the
/// window's group and reached the shell behind the modal — the splitter,
/// the rail, the status bar, a tab. The shipped Add property sheet is
/// lifted from the window's own XAML, bound to a real workspace, and set
/// among buttons on every side, so an arrow that leaves it lands somewhere
/// observable. Keys are real presses through the input manager.
/// </summary>
public sealed class SheetArrowContainmentTests
{
    [Theory]
    [InlineData("AddPropertyKey", Key.Up)]
    [InlineData("AddPropertyConfirm", Key.Left)]
    [InlineData("AddPropertyConfirm", Key.Down)]
    [InlineData("AddPropertyCancel", Key.Right)]
    [InlineData("AddPropertyCancel", Key.Down)]
    public void AnArrowFromASheetsEdgeStaysInTheSheet(string stop, Key key) => RunSta(() =>
    {
        using var host = new Host();
        UIElement from = host.Element(stop);
        Assert.True(from.Focus(), $"premise: {stop} refused the keys.");

        host.Press(key);

        Assert.True(
            host.Sheet.IsKeyboardFocusWithin,
            $"{key} from {stop} left the sheet, to {Keyboard.FocusedElement}");
    });

    private sealed class Host : IDisposable
    {
        private readonly FixtureVault _fixture = FixtureVault.Create(1, "sheet-arrows");
        private readonly Func<bool> _priorOverlayProbe = CanvasSurfaceView.ShellOverlayIsOpen;
        private readonly VaultSession _session;
        private readonly WorkspaceViewModel _workspace;
        private readonly VaultLifecycleViewModel _lifecycle;
        private readonly MainWindow _shell;
        private readonly Window _window;

        public Host()
        {
            Assert.Null(Application.Current);
            _session = VaultSession.OpenFilesystem(_fixture.Root);
            using (var cancel = new CancelToken())
            {
                _session.ScanInitial(cancel);
            }

            _workspace = new WorkspaceViewModel(
                _session,
                _fixture.Root,
                () => [],
                _ => { },
                startInteractionBackgroundWork: false,
                preferencesStore: new AppPreferencesStore(Path.Combine(_fixture.Root, "preferences.json")));
            _shell = new MainWindow();
            _lifecycle = Assert.IsType<VaultLifecycleViewModel>(_shell.DataContext);
            SetWorkspace(_workspace);
            _workspace.OpenAddPropertySheet(synchronousForTests: true);

            Sheet = Assert.IsAssignableFrom<FrameworkElement>(_shell.FindName("AddPropertyOverlay"));
            Assert.IsAssignableFrom<Panel>(Sheet.Parent).Children.Remove(Sheet);
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

            Grid.SetRow(Sheet, 1);
            Grid.SetColumn(Sheet, 1);
            grid.Children.Add(Sheet);
            _window = new Window
            {
                Content = grid,
                DataContext = _lifecycle,
                Width = 1400,
                Height = 900,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                ShowActivated = false,
            };
            _window.Show();
            _window.UpdateLayout();
            PumpedDispatcher.Drain();
            Assert.True(Sheet.IsVisible, "premise: the lifted sheet is not shown.");
        }

        public FrameworkElement Sheet { get; }

        public UIElement Element(string automationId) =>
            Descendants(Sheet).OfType<UIElement>()
                .First(element => AutomationProperties.GetAutomationId(element) == automationId);

        public void Press(Key key)
        {
            InputManager.Current.ProcessInput(new KeyEventArgs(
                Keyboard.PrimaryDevice, PresentationSource.FromVisual(_window)!, Environment.TickCount, key)
            {
                RoutedEvent = Keyboard.PreviewKeyDownEvent,
            });
            PumpedDispatcher.Drain();
        }

        private void SetWorkspace(WorkspaceViewModel? workspace) =>
            (typeof(VaultLifecycleViewModel).GetProperty(nameof(VaultLifecycleViewModel.Workspace))
                ?? throw new InvalidOperationException("Workspace is gone"))
                .SetValue(_lifecycle, workspace);

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

        public void Dispose()
        {
            try
            {
                SetWorkspace(null);
                _window.Close();
                _shell.Close();
                _workspace.Dispose();
                _session.Dispose();
                _fixture.Dispose();
            }
            finally
            {
                CanvasSurfaceView.ShellOverlayIsOpen = _priorOverlayProbe;
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
