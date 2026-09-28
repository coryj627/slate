// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SlateWindows.Canvas;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// #1272 (codex round 2 on the follow-up): the window cancels a running
/// import on Escape in its TUNNELLING handler, ahead of every focused
/// control — the position contract 34 documents for the canvas (C16: "an
/// import in flight keeps Escape and the ladder never sees it") — so every
/// owner of the key is yielded to by name. These facts run the shipped,
/// unshown MainWindow — its own elements and handler — with an import in
/// flight: an open template sheet (contract 30 T3: Esc cancels from every
/// state; TR-7) takes the key through its own route, and a key from WPF
/// menu mode (W7-6 §4) is left to the menu, while the import keeps
/// running. The Files owners and the fall-through are SidebarTreeKeysTests'
/// import facts.
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
    /// During a long import, WPF menu mode owns Escape — witnessed at the
    /// handler, where no foreground is needed. Menu mode itself cannot be
    /// entered on CI: WPF's menu takes the mouse capture when focus enters
    /// it and backs out of menu mode when a window that is not in the
    /// foreground cannot get it, and the CI session is non-interactive. So
    /// Escape's PreviewKeyDown is raised with a menu item as its source —
    /// what a key pressed in menu mode is — and tunnels from the shipped,
    /// unshown MainWindow through the shipped <c>Window_PreviewKeyDown</c>.
    /// From the File menu item, and from an item of its submenu, the handler
    /// leaves the key unhandled (for the menu's own KeyDown, which closes the
    /// menu or leaves menu mode) and the import keeps running: the source it
    /// is handed afterwards is imported. The same key from the Files tree is
    /// the handler's, and the import is cancelled: the source is never
    /// imported.
    /// </summary>
    [Theory]
    [InlineData("FileMenu", true)]
    [InlineData("OpenVaultMenuItem", true)]
    [InlineData("FilesTree", false)]
    public void EscapeFromAMenuItemDuringAnImport_IsLeftToTheMenu(string automationId, bool fromTheMenu) => RunSta(() =>
    {
        using var import = new PendingImport();
        using var host = new ShellHost(import);
        host.Sidebar.ImportCommand.Execute(null);
        Assert.True(host.Sidebar.IsImporting);
        UIElement source = host.ByAutomationId(automationId);
        Assert.Equal(fromTheMenu, source is MenuItem);

        bool handled = host.PressPreview(source, Key.Escape);

        Assert.Equal(!fromTheMenu, handled);
        Assert.True(host.Sidebar.IsImporting);
        import.HandOverTheSource();
        PumpedDispatcher.PumpUntilDrained(host.Sidebar.ImportCompletion);
        Assert.False(host.Sidebar.IsImporting);
        Assert.Equal(fromTheMenu ? 1 : 0, import.WorkerRuns);
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

        /// <summary>The tunnelling half of a key press alone: the preview is
        /// raised on <paramref name="target"/>, so it tunnels from the
        /// MainWindow through <c>Window_PreviewKeyDown</c>; the answer is
        /// whether anything on that route handled it.</summary>
        public bool PressPreview(UIElement target, Key key)
        {
            AwaitNoHeldModifier();
            var preview = new KeyEventArgs(
                Keyboard.PrimaryDevice,
                PresentationSource.FromVisual(_inputSource)!,
                Environment.TickCount,
                key)
            {
                RoutedEvent = Keyboard.PreviewKeyDownEvent,
            };
            target.RaiseEvent(preview);
            PumpedDispatcher.Drain();
            return preview.Handled;
        }

        /// <summary>The shell's element carrying <paramref name="automationId"/>
        /// — the menu items have no x:Name.</summary>
        public UIElement ByAutomationId(string automationId) =>
            Assert.Single(
                LogicalDescendants(Shell).OfType<UIElement>(),
                element => System.Windows.Automation.AutomationProperties.GetAutomationId(element) == automationId);

        private static IEnumerable<DependencyObject> LogicalDescendants(DependencyObject root)
        {
            foreach (object child in LogicalTreeHelper.GetChildren(root))
            {
                if (child is DependencyObject node)
                {
                    yield return node;
                    foreach (DependencyObject descendant in LogicalDescendants(node))
                    {
                        yield return descendant;
                    }
                }
            }
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

    private static void RunSta(Action body) =>
        StaThread.RunPumped(body, TimeSpan.FromSeconds(120), "The import-Escape shell fixture timed out.");
}
