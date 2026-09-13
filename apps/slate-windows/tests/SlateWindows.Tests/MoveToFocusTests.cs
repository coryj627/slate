// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using SlateWindows.Canvas;
using SlateWindows.FileManagement;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>Runs MainWindow's real queued Move-To focus callback, including
/// its live sidebar, picker, and modal-owner reads. The actual named filter
/// is rehosted so a stale request cannot pass merely because its old overlay
/// became invisible. No production guard is duplicated in this fixture.</summary>
public sealed class MoveToFocusTests
{
    [Theory]
    [InlineData("current")]
    [InlineData("sidebar replaced")]
    [InlineData("picker replaced")]
    [InlineData("higher sheet opened")]
    public void QueuedInitialFocusOnlyLandsForItsCurrentOwner(string change) => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize();
        MoveToPickerViewModel picker = NewPicker();
        DispatcherOperation initial = Assert.Single(host.CaptureInputOperations(() =>
            SetPrivateProperty(host.Sidebar, nameof(FilesSidebarViewModel.MoveToSheet), picker)));
        Assert.Equal(DispatcherOperationStatus.Pending, initial.Status);
        Assert.Same(host.Sentinel, Keyboard.FocusedElement);
        Assert.Equal(0, host.FilterFocusAttempts);

        switch (change)
        {
            case "sidebar replaced":
                // Seed the new sidebar before observing it: only the old
                // sidebar's focus request is queued. Both pickers stay open,
                // so the modal guard alone cannot save a missing owner guard.
                SetPrivateProperty(host.Replacement, nameof(FilesSidebarViewModel.MoveToSheet), NewPicker());
                SetPrivateProperty(host.Lifecycle, nameof(VaultLifecycleViewModel.FileSidebar), host.Replacement);
                Assert.Same(picker, host.Sidebar.MoveToSheet);
                Assert.Equal(ModalSurface.MoveTo, host.Shell.OpenModalSurface);
                break;
            case "picker replaced":
                // Both callbacks target the SAME named TextBox. Letting the
                // new callback run would mask an incorrect old focus attempt.
                DispatcherOperation replacement = Assert.Single(host.CaptureInputOperations(() =>
                    SetPrivateProperty(host.Sidebar, nameof(FilesSidebarViewModel.MoveToSheet), NewPicker())));
                Assert.True(replacement.Abort());
                Assert.Equal(DispatcherOperationStatus.Aborted, replacement.Status);
                Assert.Equal(ModalSurface.MoveTo, host.Shell.OpenModalSurface);
                break;
            case "higher sheet opened":
                foreach (DispatcherOperation operation in host.CaptureInputOperations(host.OpenHigherSheet))
                {
                    Assert.True(operation.Abort());
                }
                Assert.Same(picker, host.Sidebar.MoveToSheet);
                Assert.Equal(ModalSurface.CanvasPrompt, host.Shell.OpenModalSurface);
                break;
        }

        Assert.Equal(DispatcherOperationStatus.Pending, initial.Status);
        Assert.True(host.Filter.IsVisible && host.Filter.IsEnabled && host.Filter.Focusable);
        PumpedDispatcher.Drain();
        Assert.Equal(DispatcherOperationStatus.Completed, initial.Status);
        Assert.True(host.Filter.IsVisible && host.Filter.IsEnabled && host.Filter.Focusable);
        Assert.Equal(change == "current" ? 1 : 0, host.FilterFocusAttempts);
        Assert.Same(change == "current" ? host.Filter : host.Sentinel, Keyboard.FocusedElement);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void QueuedDismissalRestoreOnlyLandsForItsCurrentSidebar(bool replaceSidebar) => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize();
        DispatcherOperation initial = Assert.Single(host.CaptureInputOperations(() =>
            SetPrivateProperty(host.Sidebar, nameof(FilesSidebarViewModel.MoveToSheet), NewPicker())));
        Assert.Equal(DispatcherOperationStatus.Pending, initial.Status);
        PumpedDispatcher.Drain();
        Assert.Equal(DispatcherOperationStatus.Completed, initial.Status);
        Assert.Same(host.Filter, Keyboard.FocusedElement);
        Assert.Equal(1, host.FilterFocusAttempts);
        Assert.Equal(0, host.SentinelFocusAttempts);

        // Dismissal uses the real setter/notification and the actual sentinel
        // captured when the picker opened; no synthetic restore token is set.
        // Retiring the picker also changes command enabled states, which can
        // queue WPF InputManager hit-test invalidation. Run the entire batch
        // unmodified instead of assuming the shell restore is the only Input
        // operation. The positive focus outcome proves that restore actually
        // ran; the swapped-owner case must skip it even after all work drains.
        List<DispatcherOperation> restores = host.CaptureInputOperations(() =>
            SetPrivateProperty(host.Sidebar, nameof(FilesSidebarViewModel.MoveToSheet), null));
        Assert.NotEmpty(restores);
        Assert.All(restores, operation => Assert.Equal(DispatcherOperationStatus.Pending, operation.Status));
        if (replaceSidebar)
        {
            SetPrivateProperty(host.Lifecycle, nameof(VaultLifecycleViewModel.FileSidebar), host.Replacement);
        }
        Assert.Null(host.Shell.OpenModalSurface);
        Assert.True(host.Sentinel.IsVisible && host.Sentinel.IsEnabled && host.Sentinel.Focusable);
        Assert.Same(host.Filter, Keyboard.FocusedElement);
        Assert.Equal(0, host.SentinelFocusAttempts);
        Assert.All(restores, operation => Assert.Equal(DispatcherOperationStatus.Pending, operation.Status));

        PumpedDispatcher.Drain();
        Assert.All(restores, operation => Assert.Equal(DispatcherOperationStatus.Completed, operation.Status));
        Assert.Equal(replaceSidebar ? 0 : 1, host.SentinelFocusAttempts);
        Assert.Equal(1, host.FilterFocusAttempts);
        Assert.Same(replaceSidebar ? host.Filter : host.Sentinel, Keyboard.FocusedElement);
    });

    private static MoveToPickerViewModel NewPicker() => new(
        [], rootIsLegal: true, "file", _ => { }, _ => { }, () => { },
        _ => true, _ => { }, loading: true);

    // Invoke the real state setters, including their notifications and the
    // window's normal subscription management; never call a copied callback.
    private static void SetPrivateProperty(object target, string name, object? value) =>
        (target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Missing state property: {name}"))
        .SetValue(target, value);

    private sealed class Host : IDisposable
    {
        private readonly FixtureVault _fixture = FixtureVault.Create(0, "move-to-focus");
        private readonly Func<bool> _priorOverlayProbe = CanvasSurfaceView.ShellOverlayIsOpen;
        private readonly List<DispatcherOperation> _operations = [];
        private VaultSession? _session;
        private Window? _focusWindow;
        private WorkspaceViewModel? _workspace;
        private CanvasDocumentViewModel? _canvas;

        // Separate initialization keeps the using/finally active even when a
        // constructor, resource load, or focus assertion fails partway through.
        public void Initialize()
        {
            Assert.Null(Application.Current);
            File.WriteAllText(Path.Combine(_fixture.Root, "focus.canvas"), "{\"nodes\":[],\"edges\":[]}");
            _session = VaultSession.OpenFilesystem(_fixture.Root);
            using (var cancel = new CancelToken()) { _session.ScanInitial(cancel); }
            Sidebar = new FilesSidebarViewModel(_session, _ => { }, localAppDataRoot: _fixture.Root);
            Replacement = new FilesSidebarViewModel(_session, _ => { }, localAppDataRoot: _fixture.Root);
            PumpedDispatcher.PumpUntilDrained(Sidebar.TreeRefreshCompletion);
            PumpedDispatcher.PumpUntilDrained(Replacement.TreeRefreshCompletion);
            _workspace = new WorkspaceViewModel(_session, _fixture.Root, () => [], _ => { },
                startInteractionBackgroundWork: false,
                preferencesStore: new AppPreferencesStore(Path.Combine(_fixture.Root, "preferences.json")));
            _canvas = new CanvasDocumentViewModel(_session, "focus.canvas", new CanvasAnnouncer(_ => { }),
                synchronousForTests: true);

            // No Application or shown MainWindow: this avoids app-wide resource
            // state, Jump Lists, vault opening/recents writes, and native window
            // placement. Its constructor still initializes the shipped XAML,
            // lifecycle, and event subscriptions. Closing an unshown shell has
            // no HWND, so placement Save returns without persisting anything.
            Shell = new MainWindow();
            Lifecycle = Assert.IsType<VaultLifecycleViewModel>(Shell.DataContext);
            SetPrivateProperty(Lifecycle, nameof(VaultLifecycleViewModel.FileSidebar), Sidebar);
            SetPrivateProperty(Lifecycle, nameof(VaultLifecycleViewModel.Workspace), _workspace);
            Filter = Assert.IsType<TextBox>(Shell.FindName("MoveToFilterTextBox"));
            Assert.IsAssignableFrom<Panel>(Filter.Parent).Children.Remove(Filter);
            Sentinel = new TextBox { Text = "Focus stays here when ownership changes" };
            var content = new StackPanel();
            content.Children.Add(Sentinel);
            content.Children.Add(Filter);
            _focusWindow = new Window
            {
                Content = content,
                Width = 420,
                Height = 140,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
            };
            _focusWindow.Show();
            _focusWindow.Activate();
            _focusWindow.UpdateLayout();
            PumpedDispatcher.Drain();
            Assert.True(Sentinel.Focus());
            Assert.Same(Sentinel, Keyboard.FocusedElement);
            Filter.PreviewGotKeyboardFocus += Filter_PreviewGotKeyboardFocus;
            Sentinel.PreviewGotKeyboardFocus += Sentinel_PreviewGotKeyboardFocus;
        }

        public MainWindow Shell { get; private set; } = null!;
        public VaultLifecycleViewModel Lifecycle { get; private set; } = null!;
        public FilesSidebarViewModel Sidebar { get; private set; } = null!;
        public FilesSidebarViewModel Replacement { get; private set; } = null!;
        public TextBox Filter { get; private set; } = null!;
        public TextBox Sentinel { get; private set; } = null!;
        public int FilterFocusAttempts { get; private set; }
        public int SentinelFocusAttempts { get; private set; }

        public void OpenHigherSheet() => SetPrivateProperty(_workspace!,
            nameof(WorkspaceViewModel.CanvasPromptSheet),
            CanvasPromptViewModel.RenameGroup(_canvas!, "group", "Group"));

        public List<DispatcherOperation> CaptureInputOperations(Action change)
        {
            var operations = new List<DispatcherOperation>();
            DispatcherHooks hooks = Dispatcher.CurrentDispatcher.Hooks;
            void Posted(object? sender, DispatcherHookEventArgs args)
            {
                if (args.Operation.Priority == DispatcherPriority.Input)
                {
                    operations.Add(args.Operation);
                    _operations.Add(args.Operation);
                }
            }
            hooks.OperationPosted += Posted;
            try { change(); }
            finally { hooks.OperationPosted -= Posted; }
            return operations;
        }

        private void Filter_PreviewGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs args)
        {
            if (ReferenceEquals(args.NewFocus, Filter)) { FilterFocusAttempts++; }
        }

        private void Sentinel_PreviewGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs args)
        {
            if (ReferenceEquals(args.NewFocus, Sentinel)) { SentinelFocusAttempts++; }
        }

        public void Dispose()
        {
            var failures = new List<Exception>();
            void CleanUp(Action action)
            {
                try { action(); }
                catch (Exception exception) { failures.Add(exception); }
            }
            try
            {
                foreach (DispatcherOperation operation in _operations)
                {
                    if (operation.Status == DispatcherOperationStatus.Pending) { operation.Abort(); }
                }
                if (Filter is not null) { Filter.PreviewGotKeyboardFocus -= Filter_PreviewGotKeyboardFocus; }
                if (Sentinel is not null) { Sentinel.PreviewGotKeyboardFocus -= Sentinel_PreviewGotKeyboardFocus; }
                if (Lifecycle is not null)
                {
                    CleanUp(() => SetPrivateProperty(Lifecycle, nameof(VaultLifecycleViewModel.FileSidebar), null));
                    CleanUp(() => SetPrivateProperty(Lifecycle, nameof(VaultLifecycleViewModel.Workspace), null));
                }
                if (Sidebar is not null)
                {
                    CleanUp(() => PumpedDispatcher.PumpUntilDrained(Sidebar.BeginSessionShutdownAndCaptureWork().SessionWork));
                }
                if (Replacement is not null)
                {
                    CleanUp(() => PumpedDispatcher.PumpUntilDrained(Replacement.BeginSessionShutdownAndCaptureWork().SessionWork));
                }
                CleanUp(() => _workspace?.Dispose());
                if (_canvas is not null)
                {
                    CleanUp(_canvas.Shutdown);
                    CleanUp(() => PumpedDispatcher.PumpUntilDrained(_canvas.WhenAllWorkDrained()));
                }
                CleanUp(() => _focusWindow?.Close());
                CleanUp(() => Shell?.Close());
                CleanUp(() => _session?.Dispose());
                CleanUp(_fixture.Dispose);
            }
            finally { CanvasSurfaceView.ShellOverlayIsOpen = _priorOverlayProbe; }
            if (failures.Count > 0) { throw new AggregateException("Move-To focus fixture cleanup failed.", failures); }
        }
    }

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
        Assert.True(thread.Join(TimeSpan.FromSeconds(120)), "Move-To focus fixture timed out.");
        if (failure is not null) { ExceptionDispatchInfo.Capture(failure).Throw(); }
    }
}
