// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SlateWindows.Canvas;
using SlateWindows.Commands;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// #1275 codex round 2, finding 2 — contract P9's modal-surface invariant
/// in the shipped shell: a palette command that opens its own surface
/// leaves that surface the ONE open modal when Enter's dispatcher turn
/// ends, and keyboard focus goes straight into its field, visibly, while
/// the palette's recents write is still parked on the lane.
/// </summary>
public sealed partial class CommandPaletteTests
{
    /// <summary>
    /// Enter on the palette's Quick Open or Search row, with the palette's
    /// own lane busy so the recents write cannot land. At every step the
    /// fact asserts which modal is open — exactly one — and where keyboard
    /// focus is: the palette's field before Enter; the picker, alone, in
    /// the same turn as Enter; the picker's field, on screen, once the
    /// queued focus has run, with no stop on the way (not the collapsed
    /// palette, not the window); and unchanged after the write lands.
    /// </summary>
    [Theory]
    [InlineData(ChordTable.Ids.QuickOpen)]
    [InlineData(ChordTable.Ids.ToggleSearch)]
    public void APaletteOpenedPickerIsTheOneOpenModalWhileTheRecentsWriteIsParked(string commandId) =>
        RunSta(() =>
        {
            using var host = new PaletteShellHost();
            bool quickOpen = commandId == ChordTable.Ids.QuickOpen;
            TextBox pickerField = quickOpen
                ? host.Shell.QuickSwitcherSearchTextBox
                : host.Shell.SearchOverlaySearchTextBox;
            ModalSurface picker = quickOpen ? ModalSurface.QuickOpen : ModalSurface.SearchOverlay;

            // Before Enter: the palette is the one modal, its field focused.
            host.OpenThePalette();
            host.AssertTheOneOpenModalIs(ModalSurface.CommandPalette);
            host.AssertFocusIsOn(host.Shell.CommandPaletteSearchTextBox);
            host.Palette.Select(Assert.Single(host.Palette.Rows, row => row.Id == commandId));

            // Enter, with the lane parked: the write cannot land.
            ManualResetEventSlim lane = host.ParkTheLane();
            host.PressEnterInThePalette();

            // The same dispatcher turn: the palette has retired and the
            // picker is the one modal; the write has not landed.
            Assert.False(host.Palette.IsOpen);
            host.AssertTheOneOpenModalIs(picker);
            Assert.False(host.Palette.RecordCompletion.IsCompleted);
            Assert.DoesNotContain(commandId, host.PersistedRecents());

            // The queued focus runs: the picker's field, on screen, and
            // nothing in between.
            host.Settle();
            host.AssertTheOneOpenModalIs(picker);
            host.AssertFocusIsOn(pickerField);
            Assert.Equal([pickerField], host.FocusChangesSinceEnter);

            // The write lands: the recent exists, and nothing moves.
            lane.Set();
            PumpedDispatcher.PumpUntilDrained(host.Palette.RecordCompletion);
            host.Settle();
            Assert.Contains(commandId, host.PersistedRecents());
            host.AssertTheOneOpenModalIs(picker);
            host.AssertFocusIsOn(pickerField);
            Assert.Equal([pickerField], host.FocusChangesSinceEnter);
        });

    /// <summary>
    /// The shipped shell — the real <see cref="MainWindow"/>, its XAML,
    /// lifecycle, handlers, registry and commands — over a scanned fixture
    /// vault, with the palette's recents file inside the fixture and its
    /// production lane in the fact's hands. The unshown MainWindow has no
    /// HWND and cannot hold keyboard focus, so its content is lifted into
    /// an off-screen window that is shown and activated (MoveToFocusTests'
    /// technique, applied to the whole shell). Nothing reaches the user's
    /// %LOCALAPPDATA%: the vault is attached without OpenVaultAsync, so no
    /// recent-vaults write, and the unshown shell persists no placement.
    /// </summary>
    private sealed class PaletteShellHost : IDisposable
    {
        private readonly FixtureVault _fixture = FixtureVault.Create(1, "palette-shell");
        private readonly Func<bool> _priorOverlayProbe = CanvasSurfaceView.ShellOverlayIsOpen;
        private readonly List<ManualResetEventSlim> _gates = [];
        private readonly VaultSession _session;
        private readonly QuickSwitcherViewModel _switcher;
        private readonly VaultLifecycleViewModel _lifecycle;
        private readonly Window _window;
        private readonly string _recentsPath;

        public PaletteShellHost()
        {
            Assert.Null(Application.Current);
            _session = VaultSession.OpenFilesystem(_fixture.Root);
            using (var cancel = new CancelToken())
            {
                _session.ScanInitial(cancel);
            }

            _recentsPath = Path.Combine(_fixture.Root, "device-state", "command-palette-recents.json");
            Lane = new CommandPaletteWorkLane();
            Shell = new MainWindow(new CommandPaletteRecentsStore(_recentsPath), Lane);
            _lifecycle = Assert.IsType<VaultLifecycleViewModel>(Shell.DataContext);
            _switcher = new QuickSwitcherViewModel(
                _session,
                _fixture.Root,
                _ => { },
                localAppDataRoot: _fixture.Root);

            // A vault, as far as the palette, Quick Open and Search ask: the
            // session and root the search source reads, the flag the palette
            // and the availability gate read, and the switcher the Quick
            // Open command resolves — each through the lifecycle's own
            // setter where it has one, so the shell observes the change.
            SetField("_session", _session);
            SetField("_vaultPath", _fixture.Root);
            SetProperty(nameof(VaultLifecycleViewModel.IsVaultOpen), true);
            SetProperty(nameof(VaultLifecycleViewModel.QuickSwitcher), _switcher);

            UIElement content = Assert.IsAssignableFrom<UIElement>(Shell.Content);
            Shell.Content = null;
            Sentinel = new TextBox { Text = "Focus starts here" };
            var root = new Grid();
            root.Children.Add(Sentinel);
            root.Children.Add(content);
            _window = new Window
            {
                Content = root,
                DataContext = _lifecycle,
                Width = 1120,
                Height = 720,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -20_000,
                Top = -20_000,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
            };
            _window.AddHandler(
                Keyboard.GotKeyboardFocusEvent,
                new KeyboardFocusChangedEventHandler(Window_GotKeyboardFocus),
                handledEventsToo: true);
            _window.Show();
            _window.Activate();
            Settle();
            Assert.True(
                Sentinel.Focus() && ReferenceEquals(Sentinel, Keyboard.FocusedElement),
                "keyboard premise: the host window could not take focus — another "
                + "process holds the foreground");
        }

        public MainWindow Shell { get; }

        public CommandPaletteWorkLane Lane { get; }

        public TextBox Sentinel { get; }

        public CommandPaletteViewModel Palette => _lifecycle.Palette;

        /// <summary>Every element that took keyboard focus since the last
        /// <see cref="PressEnterInThePalette"/>, in order.</summary>
        public List<IInputElement> FocusChangesSinceEnter { get; } = [];

        /// <summary>Open, pump until the open's rows have published, and let
        /// the palette's queued focus run.</summary>
        public void OpenThePalette()
        {
            Palette.Open();
            Assert.True(
                PumpedDispatcher.PumpUntil(() => !Palette.IsRankPending, TimeSpan.FromSeconds(10)),
                "the palette's rows never published");
            Settle();
        }

        /// <summary>Hands the palette's lane an item that holds it until the
        /// fact releases the returned gate; returns once the item runs, so
        /// anything handed over afterwards waits behind it.</summary>
        public ManualResetEventSlim ParkTheLane()
        {
            var gate = new ManualResetEventSlim(false);
            _gates.Add(gate);
            using var running = new ManualResetEventSlim(false);
            _ = Lane.Run(
                () =>
                {
                    running.Set();
                    return gate.Wait(TimeSpan.FromSeconds(30));
                },
                CancellationToken.None);
            Assert.True(running.Wait(TimeSpan.FromSeconds(10)), "the lane never picked up the parking item");
            return gate;
        }

        /// <summary>Enter through the palette field's own key route.</summary>
        public void PressEnterInThePalette()
        {
            FocusChangesSinceEnter.Clear();
            TextBox field = Shell.CommandPaletteSearchTextBox;
            PresentationSource source = PresentationSource.FromVisual(field)
                ?? throw new InvalidOperationException("the palette field has no presentation source");
            var press = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, Key.Enter)
            {
                RoutedEvent = Keyboard.PreviewKeyDownEvent,
            };
            field.RaiseEvent(press);
            Assert.True(press.Handled, "the palette's key route did not take Enter");
        }

        /// <summary>Exactly one of the three surfaces this shell can open is
        /// open — in its view model and on screen — and it is the shell's
        /// topmost modal.</summary>
        public void AssertTheOneOpenModalIs(ModalSurface expected)
        {
            var open = new List<ModalSurface>();
            if (Palette.IsOpen)
            {
                open.Add(ModalSurface.CommandPalette);
            }

            if (_switcher.IsOpen)
            {
                open.Add(ModalSurface.QuickOpen);
            }

            if (_lifecycle.Search.IsOpen)
            {
                open.Add(ModalSurface.SearchOverlay);
            }

            Assert.Equal([expected], open);
            Assert.Equal(expected, Shell.OpenModalSurface);
            Assert.Equal(
                Palette.IsOpen ? Visibility.Visible : Visibility.Collapsed,
                Shell.CommandPaletteOverlay.Visibility);
            Assert.Equal(
                _switcher.IsOpen ? Visibility.Visible : Visibility.Collapsed,
                Shell.QuickSwitcherOverlay.Visibility);
            Assert.Equal(
                _lifecycle.Search.IsOpen ? Visibility.Visible : Visibility.Collapsed,
                Shell.SearchOverlay.Visibility);
        }

        public void AssertFocusIsOn(TextBox field)
        {
            Assert.Same(field, Keyboard.FocusedElement);
            Assert.True(field.IsVisible, $"{field.Name} holds focus but is not on screen");
        }

        /// <summary>The recents the file on disk holds.</summary>
        public string[] PersistedRecents() => new CommandPaletteRecentsStore(_recentsPath).Load();

        /// <summary>Layout, then everything queued — the Input-priority
        /// focus hand-offs included — twice over.</summary>
        public void Settle()
        {
            _window.UpdateLayout();
            PumpedDispatcher.Drain();
            _window.UpdateLayout();
            PumpedDispatcher.Drain();
        }

        public void Dispose()
        {
            foreach (ManualResetEventSlim gate in _gates)
            {
                gate.Set();
            }

            try
            {
                _window.RemoveHandler(
                    Keyboard.GotKeyboardFocusEvent,
                    new KeyboardFocusChangedEventHandler(Window_GotKeyboardFocus));
                // Close what the fact opened, so their background work stops
                // with them, then detach the borrowed vault state before the
                // shell's own teardown runs.
                _switcher.Dismiss();
                _lifecycle.Search.Close();
                SetProperty(nameof(VaultLifecycleViewModel.QuickSwitcher), null);
                SetProperty(nameof(VaultLifecycleViewModel.IsVaultOpen), false);
                SetField("_session", null);
                SetField("_vaultPath", string.Empty);
                _window.Close();
                Shell.Close();
                PumpedDispatcher.Drain();
                _switcher.Dispose();
                _session.Dispose();
                _fixture.Dispose();
            }
            finally
            {
                CanvasSurfaceView.ShellOverlayIsOpen = _priorOverlayProbe;
                foreach (ManualResetEventSlim gate in _gates)
                {
                    gate.Dispose();
                }
            }
        }

        private void Window_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) =>
            FocusChangesSinceEnter.Add(e.NewFocus);

        private void SetField(string name, object? value) =>
            (typeof(VaultLifecycleViewModel).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException($"VaultLifecycleViewModel.{name} is gone"))
            .SetValue(_lifecycle, value);

        private void SetProperty(string name, object? value) =>
            (typeof(VaultLifecycleViewModel).GetProperty(name, BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException($"VaultLifecycleViewModel.{name} is gone"))
            .SetValue(_lifecycle, value);
    }
}
