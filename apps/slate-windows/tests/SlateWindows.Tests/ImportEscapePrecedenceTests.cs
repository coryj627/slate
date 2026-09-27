// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using SlateWindows.Canvas;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// #1272 (codex round 2 on the follow-up): the window cancels a running
/// import on Escape in its TUNNELLING handler, ahead of every focused
/// control — the position contract 34 documents for the canvas (C16: "an
/// import in flight keeps Escape and the ladder never sees it") — so every
/// owner of the key is yielded to by name. These facts run the shipped
/// shell's own elements and handler with an import in flight: an open
/// template sheet (contract 30 T3: Esc cancels from every state; TR-7) and
/// WPF menu mode (W7-6 §4), each taking the key through its own route while
/// the import keeps running. The Files owners and the fall-through are
/// SidebarTreeKeysTests' import facts.
/// </summary>
public sealed class ImportEscapePrecedenceTests
{
    /// <summary>
    /// During a long import, the template picker — opened by its own
    /// command, which has no import guard — owns Escape: the key, pressed in
    /// the sheet and routed through the shipped MainWindow, closes the
    /// sheet, and the import keeps running (the source it is handed
    /// afterwards is imported).
    /// </summary>
    [Fact]
    public void EscapeInTheTemplatePickerDuringAnImport_ClosesTheSheetNotTheImport() => RunSta(() =>
    {
        using var import = new PendingImport();
        using var host = new ShellHost(import);
        host.Sidebar.ImportCommand.Execute(null);
        Assert.True(host.Sidebar.IsImporting);
        host.Workspace.NewFromTemplateCommand.Execute(null);
        Assert.NotNull(host.Workspace.TemplatePickerSheet);
        Assert.Equal(ModalSurface.TemplatePicker, host.Shell.OpenModalSurface);

        Assert.True(
            host.Press(host.Element<UIElement>("TemplatePickerCancelButton"), Key.Escape),
            "Escape in the template picker went unhandled.");

        Assert.Null(host.Workspace.TemplatePickerSheet);
        Assert.Null(host.Shell.OpenModalSurface);
        Assert.True(host.Sidebar.IsImporting);
        import.HandOverTheSource();
        PumpedDispatcher.PumpUntilDrained(host.Sidebar.ImportCompletion);
        Assert.False(host.Sidebar.IsImporting);
        Assert.Equal(1, import.WorkerRuns);
    });

    /// <summary>
    /// During a long import, WPF menu mode on the shipped menu bar owns
    /// Escape, through the real input pipeline: with File highlighted,
    /// Escape leaves menu mode and hands focus back; with the File menu
    /// open, Escape closes it and menu mode stays. Either way the import
    /// keeps running (the source it is handed afterwards is imported).
    /// </summary>
    [Theory]
    [InlineData("File highlighted")]
    [InlineData("File menu open")]
    public void EscapeInMenuModeDuringAnImport_LeavesTheMenuNotTheImport(string menu) => RunSta(() =>
    {
        using var import = new PendingImport();
        using var host = new MenuHost(import);
        host.Sidebar.ImportCommand.Execute(null);
        Assert.True(host.Sidebar.IsImporting);
        Assert.True(host.Sentinel.Focus());
        Assert.True(host.FileMenu.Focus());
        PumpedDispatcher.Drain();
        Assert.True(host.IsMenuMode, "Focus on the File menu item did not enter menu mode.");
        if (menu == "File menu open")
        {
            // Down on the highlighted File opens its menu with focus on
            // the first item, as the keyboard opens it.
            host.Press(Key.Down);
            Assert.True(
                PumpedDispatcher.PumpUntil(() => host.FileMenu.IsSubmenuOpen
                    && Keyboard.FocusedElement is MenuItem item
                    && !ReferenceEquals(item, host.FileMenu)),
                "Down on File did not open the File menu with focus on its first item.");
        }

        host.Press(Key.Escape);

        if (menu == "File menu open")
        {
            Assert.False(host.FileMenu.IsSubmenuOpen, "Escape did not close the open File menu.");
            Assert.True(host.IsMenuMode, "Closing the File menu also left menu mode.");
        }
        else
        {
            Assert.False(host.IsMenuMode, "Escape did not leave menu mode.");
            Assert.Same(host.Sentinel, Keyboard.FocusedElement);
        }

        Assert.True(host.Sidebar.IsImporting);
        import.HandOverTheSource();
        PumpedDispatcher.PumpUntilDrained(host.Sidebar.ImportCompletion);
        Assert.False(host.Sidebar.IsImporting);
        Assert.Equal(1, import.WorkerRuns);
    });

    /// <summary>The shipped shell, unshown (SheetKeyboardFenceTests' shape):
    /// the real MainWindow — its XAML, lifecycle and handlers — over a
    /// scanned fixture vault's workspace and a Files sidebar whose import
    /// the fact holds open. With no HWND and no Application nothing is
    /// persisted; a key press is raised on its target element inside the
    /// shell, so it tunnels from the MainWindow itself, and is promoted to
    /// KeyDown when the preview goes unhandled, as the keyboard device
    /// does.</summary>
    private sealed class ShellHost : IDisposable
    {
        private readonly FixtureVault _fixture = FixtureVault.Create(1, "import-escape-shell");
        private readonly Func<bool> _priorOverlayProbe = CanvasSurfaceView.ShellOverlayIsOpen;
        private readonly VaultSession _session;
        private readonly VaultLifecycleViewModel _lifecycle;
        private readonly Window _inputSource;

        public ShellHost(PendingImport import)
        {
            Assert.Null(Application.Current);
            _session = VaultSession.OpenFilesystem(_fixture.Root);
            using (var cancel = new CancelToken())
            {
                _session.ScanInitial(cancel);
            }

            Workspace = new WorkspaceViewModel(
                _session,
                _fixture.Root,
                () => [],
                _ => { },
                startInteractionBackgroundWork: false,
                preferencesStore: new AppPreferencesStore(Path.Combine(_fixture.Root, "preferences.json")));
            Sidebar = NewSidebar(_session, _fixture.Root, import);
            Shell = new MainWindow();
            _lifecycle = Assert.IsType<VaultLifecycleViewModel>(Shell.DataContext);
            SetState(_lifecycle, nameof(VaultLifecycleViewModel.Workspace), Workspace);
            SetState(_lifecycle, nameof(VaultLifecycleViewModel.FileSidebar), Sidebar);
            // KeyEventArgs needs a live PresentationSource; the unshown
            // shell has none, so an offscreen stub lends its own.
            _inputSource = OffscreenWindow(new Border());
            _inputSource.Show();
        }

        public MainWindow Shell { get; }

        public WorkspaceViewModel Workspace { get; }

        public FilesSidebarViewModel Sidebar { get; }

        public T Element<T>(string name)
            where T : class =>
            Assert.IsAssignableFrom<T>(Shell.FindName(name));

        public bool Press(UIElement target, Key key)
        {
            AwaitNoHeldModifier();
            PresentationSource source = PresentationSource.FromVisual(_inputSource)!;
            var preview = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key)
            {
                RoutedEvent = Keyboard.PreviewKeyDownEvent,
            };
            target.RaiseEvent(preview);
            bool handled = preview.Handled;
            if (!handled)
            {
                var down = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key)
                {
                    RoutedEvent = Keyboard.KeyDownEvent,
                };
                target.RaiseEvent(down);
                handled = down.Handled;
            }

            PumpedDispatcher.Drain();
            return handled;
        }

        public void Dispose()
        {
            var failures = new List<Exception>();
            void CleanUp(Action action)
            {
                try { action(); }
                catch (Exception exception) { failures.Add(exception); }
            }

            CleanUp(() => SetState(_lifecycle, nameof(VaultLifecycleViewModel.FileSidebar), null));
            CleanUp(() => PumpedDispatcher.PumpUntilDrained(Sidebar.BeginSessionShutdownAndCaptureWork().SessionWork));
            CleanUp(() => SetState(_lifecycle, nameof(VaultLifecycleViewModel.Workspace), null));
            CleanUp(Workspace.Dispose);
            CleanUp(_inputSource.Close);
            CleanUp(Shell.Close);
            CleanUp(_session.Dispose);
            CleanUp(_fixture.Dispose);
            CanvasSurfaceView.ShellOverlayIsOpen = _priorOverlayProbe;
            if (failures.Count > 0)
            {
                throw new AggregateException("The shell fixture's cleanup failed.", failures);
            }
        }
    }

    /// <summary>The shipped menu bar — the MainWindow's own
    /// <c>MainMenu</c>, with its items, commands and handlers — in a shown
    /// offscreen window, because menu mode needs keyboard focus and so a
    /// live HWND, which the unshown MainWindow cannot have (and showing the
    /// real MainWindow would open the user's recent vaults and write window
    /// placement). The shipped <c>MainWindow.Window_PreviewKeyDown</c>
    /// stands at the root of the window's key route, where it runs in the
    /// shell, and keys arrive through the real input pipeline
    /// (<see cref="InputManager"/>), so WPF's own menu handling answers
    /// them.</summary>
    private sealed class MenuHost : IDisposable
    {
        private static readonly PropertyInfo MenuModeProperty =
            typeof(MenuBase).GetProperty("IsMenuMode", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("MenuBase.IsMenuMode is gone; the menu-mode fact needs another witness.");

        private readonly FixtureVault _fixture = FixtureVault.Create(1, "import-escape-menu");
        private readonly Func<bool> _priorOverlayProbe = CanvasSurfaceView.ShellOverlayIsOpen;
        private readonly VaultSession _session;
        private readonly VaultLifecycleViewModel _lifecycle;
        private readonly Window _window;
        private readonly Menu _menu;

        public MenuHost(PendingImport import)
        {
            Assert.Null(Application.Current);
            _session = VaultSession.OpenFilesystem(_fixture.Root);
            using (var cancel = new CancelToken())
            {
                _session.ScanInitial(cancel);
            }

            Sidebar = NewSidebar(_session, _fixture.Root, import);
            Shell = new MainWindow();
            _lifecycle = Assert.IsType<VaultLifecycleViewModel>(Shell.DataContext);
            SetState(_lifecycle, nameof(VaultLifecycleViewModel.FileSidebar), Sidebar);
            _menu = Assert.IsType<Menu>(Shell.FindName("MainMenu"));
            Assert.IsAssignableFrom<Panel>(_menu.Parent).Children.Remove(_menu);
            FileMenu = Assert.Single(
                _menu.Items.OfType<MenuItem>(),
                item => System.Windows.Automation.AutomationProperties.GetAutomationId(item) == "FileMenu");
            Sentinel = new TextBox { Text = "Focus before menu mode" };
            var root = new DockPanel();
            DockPanel.SetDock(_menu, Dock.Top);
            root.Children.Add(_menu);
            root.Children.Add(Sentinel);
            _window = OffscreenWindow(root);
            _window.DataContext = _lifecycle;
            _window.ShowActivated = true;
            MethodInfo handler = typeof(MainWindow).GetMethod(
                "Window_PreviewKeyDown",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("MainWindow.Window_PreviewKeyDown is gone.");
            _window.PreviewKeyDown += handler.CreateDelegate<KeyEventHandler>(Shell);
            _window.Show();
            _window.Activate();
            _window.UpdateLayout();
            PumpedDispatcher.Drain();
        }

        public MainWindow Shell { get; }

        public FilesSidebarViewModel Sidebar { get; }

        public MenuItem FileMenu { get; }

        public TextBox Sentinel { get; }

        public bool IsMenuMode => (bool)MenuModeProperty.GetValue(_menu)!;

        /// <summary>One key press through the input system, delivered to
        /// the keyboard focus the way a physical press is.</summary>
        public void Press(Key key)
        {
            AwaitNoHeldModifier();
            InputManager.Current.ProcessInput(new KeyEventArgs(
                Keyboard.PrimaryDevice,
                PresentationSource.FromVisual(_window)!,
                Environment.TickCount,
                key)
            {
                RoutedEvent = Keyboard.PreviewKeyDownEvent,
            });
            PumpedDispatcher.Drain();
        }

        public void Dispose()
        {
            var failures = new List<Exception>();
            void CleanUp(Action action)
            {
                try { action(); }
                catch (Exception exception) { failures.Add(exception); }
            }

            CleanUp(() => FileMenu.IsSubmenuOpen = false);
            CleanUp(() => SetState(_lifecycle, nameof(VaultLifecycleViewModel.FileSidebar), null));
            CleanUp(() => PumpedDispatcher.PumpUntilDrained(Sidebar.BeginSessionShutdownAndCaptureWork().SessionWork));
            CleanUp(_window.Close);
            CleanUp(Shell.Close);
            CleanUp(_session.Dispose);
            CleanUp(_fixture.Dispose);
            CanvasSurfaceView.ShellOverlayIsOpen = _priorOverlayProbe;
            if (failures.Count > 0)
            {
                throw new AggregateException("The menu fixture's cleanup failed.", failures);
            }
        }
    }

    private static FilesSidebarViewModel NewSidebar(VaultSession session, string root, PendingImport import)
    {
        var sidebar = new FilesSidebarViewModel(
            session,
            _ => { },
            vaultRoot: root,
            pickImportSources: import.PickSources,
            localAppDataRoot: Path.Combine(root, "device-state"),
            importWorker: import.Run);
        PumpedDispatcher.PumpUntilDrained(sidebar.TreeRefreshCompletion);
        return sidebar;
    }

    /// <summary>The lifecycle's real setters, so the shell's own
    /// observation wires what it observes.</summary>
    private static void SetState(object target, string name, object? value) =>
        (target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Missing state property: {name}"))
        .SetValue(target, value);

    private static Window OffscreenWindow(UIElement content) => new()
    {
        Content = content,
        Width = 520,
        Height = 360,
        ShowInTaskbar = false,
        ShowActivated = false,
        WindowStyle = WindowStyle.None,
        Left = -10_000,
        Top = -10_000,
        WindowStartupLocation = WindowStartupLocation.Manual,
    };

    /// <summary>Escape is pressed unmodified, and WPF reads the modifiers
    /// off the real keyboard: a modifier held elsewhere on a shared desktop
    /// is waited out, and one still held fails here, by name.</summary>
    private static void AwaitNoHeldModifier() =>
        Assert.True(
            PumpedDispatcher.PumpUntil(
                () => Keyboard.Modifiers == ModifierKeys.None,
                TimeSpan.FromSeconds(5)),
            $"A real modifier key is held on this desktop ({Keyboard.Modifiers}); these facts press unmodified keys.");

    private static void RunSta(Action body)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { PumpedDispatcher.Run(body); }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(120)), "The import-Escape shell fixture timed out.");
        if (failure is not null) { ExceptionDispatchInfo.Capture(failure).Throw(); }
    }
}
