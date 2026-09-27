// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using SlateWindows.Canvas;
using SlateWindows.Commands;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// #1275 codex round 5: the shell's own key route — <c>MainWindow</c>'s
/// window-level <c>PreviewKeyDown</c> — must respect the palette's seal,
/// not only the palette's key handler. These facts raise REAL chords
/// through that route on the shipped shell (unshown, its key presses
/// raised on their target with a borrowed input source — the
/// <c>SheetKeyboardFenceTests</c> shape), so the shell's carve-outs are
/// exercised, not the view model's methods.
/// </summary>
public sealed partial class CommandPaletteTests
{
    /// <summary>
    /// Close Vault run from the palette over a dirty tab raises the real
    /// unsaved-changes message box inside the command. Inside that loop
    /// the real Ctrl+Shift+N goes through the shell's key route: it is
    /// taken and ignored — the palette stays open, sealed and silent, and
    /// no template picker opens. Cancel on the prompt ends the command;
    /// only that invocation completes: its recent is written, the palette
    /// dismisses, and no template sheet ever appeared.
    /// </summary>
    [Fact]
    public void TheTemplateChordThroughTheShellIsRefusedWhileAPaletteCommandRuns() => RunSta(() =>
    {
        using var host = new UnshownShellHost();
        WorkspaceViewModel workspace = host.AttachDirtyWorkspace();
        CommandPaletteViewModel palette = host.Palette;
        palette.Open();
        Assert.True(
            PumpedDispatcher.PumpUntil(() => !palette.IsRankPending, TimeSpan.FromSeconds(10)),
            "the palette's rows never published");
        PumpedDispatcher.Drain();
        palette.Select(Assert.Single(palette.Rows, row => row.Id == ChordTable.Ids.VaultClose));
        host.Heard.Clear();
        int published = 0;
        palette.PropertyChanged += (_, change) =>
        {
            if (change.PropertyName == nameof(CommandPaletteViewModel.Rows))
            {
                published++;
            }
        };

        (bool Open, bool Sealed, bool Handled, bool PickerOpen, int Published, int Heard)? inside = null;
        int ticks = 0;
        var timer = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(50) };
        timer.Tick += (_, _) =>
        {
            Action? close = Win32DialogCloser();
            if (close is null && ++ticks < 200)
            {
                return;
            }

            timer.Stop();
            if (close is null)
            {
                return;
            }

            // The unsaved-changes prompt is up, inside Close Vault.
            bool handled = host.Press(
                host.Shell.CommandPaletteSearchTextBox,
                Key.N,
                ModifierKeys.Control | ModifierKeys.Shift);
            PumpedDispatcher.Drain();
            inside = (
                palette.IsOpen,
                palette.IsSealed,
                handled,
                workspace.TemplatePickerSheet is not null,
                published,
                host.Heard.Count);
            close();
        };
        timer.Start();
        bool enter = host.Press(host.Shell.CommandPaletteSearchTextBox, Key.Enter, ModifierKeys.None);
        timer.Stop();

        Assert.True(enter, "the palette's key route did not take Enter");
        Assert.NotNull(inside);
        Assert.True(inside.Value.Handled, "the shell let Ctrl+Shift+N through under the running command");
        Assert.True(
            inside.Value is (true, true, _, false, 0, 0),
            $"inside the running command the palette was open={inside.Value.Open}, sealed={inside.Value.Sealed}; "
            + $"a template picker opened={inside.Value.PickerOpen}; the palette published "
            + $"{inside.Value.Published} time(s) and made {inside.Value.Heard} announcement(s)");

        // Only the original invocation completed: Close Vault (cancelled at
        // its prompt) returned, its recent was written and the palette
        // dismissed; no template sheet ever opened and the vault is open.
        Assert.False(palette.IsSealed);
        Assert.False(palette.IsOpen);
        Assert.Null(workspace.TemplatePickerSheet);
        Assert.Null(workspace.TemplateFlowSheet);
        Assert.True(host.Lifecycle.IsVaultOpen);
        PumpedDispatcher.PumpUntilDrained(palette.RecordCompletion);
        Assert.Equal([ChordTable.Ids.VaultClose], host.PersistedRecents());
        Assert.Empty(host.Heard);
    });

    /// <summary>
    /// The audit's second find: with the palette closed but sealed — here a
    /// thread-modal section over the shell — the palette's own chord used to
    /// clear the way first, dismissing an open Quick Open, and only then
    /// have its open refused. Through the real key route, the chord is taken
    /// and nothing moves: Quick Open stays open, the palette stays shut.
    /// </summary>
    [Fact]
    public void ThePaletteChordThroughTheShellLeavesQuickOpenAloneWhileThePaletteIsSealed() => RunSta(() =>
    {
        using var host = new UnshownShellHost();
        QuickSwitcherViewModel switcher = host.AttachQuickOpen();
        switcher.Open();
        Assert.Equal(ModalSurface.QuickOpen, host.Shell.OpenModalSurface);

        ComponentDispatcher.PushModal();
        try
        {
            Assert.True(host.Palette.IsSealed);
            bool handled = host.Press(
                host.Shell.QuickSwitcherSearchTextBox,
                Key.P,
                ModifierKeys.Control | ModifierKeys.Shift);

            Assert.True(handled, "the shell let Ctrl+Shift+P through");
            Assert.True(switcher.IsOpen, "the refused palette open dismissed Quick Open first");
            Assert.False(host.Palette.IsOpen);
        }
        finally
        {
            ComponentDispatcher.PopModal();
        }

        Assert.False(host.Palette.IsSealed);
        Assert.True(switcher.IsOpen);
    });

    /// <summary>
    /// The shipped shell, unshown — MoveToFocusTests' and
    /// SheetKeyboardFenceTests' shape: the real MainWindow, its XAML,
    /// lifecycle, handlers, registry and commands, with a fixture vault
    /// attached through the lifecycle's own setters and the palette's
    /// recents file in the fixture. Key presses are raised on their target
    /// element with an off-screen window's presentation source, so the
    /// window-level route runs as it does for a real key.
    /// </summary>
    private sealed class UnshownShellHost : IDisposable
    {
        private readonly FixtureVault _fixture = FixtureVault.Create(1, "palette-shell-keys");
        private readonly Func<bool> _priorOverlayProbe = CanvasSurfaceView.ShellOverlayIsOpen;
        private readonly VaultSession _session;
        private readonly VaultLifecycleViewModel _lifecycle;
        private readonly Window _inputSource;
        private readonly string _recentsPath;
        private WorkspaceViewModel? _workspace;
        private QuickSwitcherViewModel? _switcher;

        public UnshownShellHost()
        {
            Assert.Null(Application.Current);
            _session = VaultSession.OpenFilesystem(_fixture.Root);
            using (var cancel = new CancelToken())
            {
                _session.ScanInitial(cancel);
            }

            _recentsPath = Path.Combine(_fixture.Root, "device-state", "command-palette-recents.json");
            Shell = new MainWindow(new CommandPaletteRecentsStore(_recentsPath), paletteLane: null);
            _lifecycle = Assert.IsType<VaultLifecycleViewModel>(Shell.DataContext);
            SetField("_session", _session);
            SetField("_vaultPath", _fixture.Root);
            SetProperty(nameof(VaultLifecycleViewModel.IsVaultOpen), true);

            FieldInfo announce = typeof(CommandPaletteViewModel).GetField(
                    "_announce", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("CommandPaletteViewModel._announce is gone");
            var funnel = (Action<A11yEvent>)announce.GetValue(Palette)!;
            announce.SetValue(Palette, (Action<A11yEvent>)(announcement =>
            {
                Heard.Add(announcement);
                funnel(announcement);
            }));

            // KeyEventArgs needs a live PresentationSource; the unshown
            // shell has none, so an off-screen stub lends its own.
            _inputSource = new Window
            {
                Content = new Border(),
                Width = 200,
                Height = 100,
                ShowInTaskbar = false,
                ShowActivated = false,
                WindowStyle = WindowStyle.None,
                Left = -10_000,
                Top = -10_000,
                WindowStartupLocation = WindowStartupLocation.Manual,
            };
            _inputSource.Show();
        }

        public MainWindow Shell { get; }

        public VaultLifecycleViewModel Lifecycle => _lifecycle;

        public CommandPaletteViewModel Palette => _lifecycle.Palette;

        /// <summary>What the palette has announced, in order.</summary>
        public List<A11yEvent> Heard { get; } = [];

        public WorkspaceViewModel AttachDirtyWorkspace()
        {
            _workspace = new WorkspaceViewModel(
                _session,
                _fixture.Root,
                () => [],
                _ => { },
                startInteractionBackgroundWork: false,
                preferencesStore: new AppPreferencesStore(Path.Combine(_fixture.Root, "preferences.json")));
            SetProperty(nameof(VaultLifecycleViewModel.Workspace), _workspace);
            _workspace.OpenPath("note0.md");
            _workspace.ActiveGroup.ActiveTab!.Text += "\nUnsaved.";
            Assert.True(_workspace.HasDirtyTabs);
            return _workspace;
        }

        public QuickSwitcherViewModel AttachQuickOpen()
        {
            _switcher = new QuickSwitcherViewModel(_session, _fixture.Root, _ => { }, localAppDataRoot: _fixture.Root);
            SetProperty(nameof(VaultLifecycleViewModel.QuickSwitcher), _switcher);
            return _switcher;
        }

        public string[] PersistedRecents() => new CommandPaletteRecentsStore(_recentsPath).Load();

        /// <summary>A real key press on <paramref name="target"/>: Preview,
        /// then — unhandled — KeyDown, with exactly
        /// <paramref name="modifiers"/> held in the thread's key state.
        /// Returns whether the press was handled.</summary>
        public bool Press(UIElement target, Key key, ModifierKeys modifiers)
        {
            bool handled = false;
            ThreadModifiers.Hold(modifiers, () =>
            {
                PresentationSource source = PresentationSource.FromVisual(_inputSource)!;
                var preview = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key)
                {
                    RoutedEvent = Keyboard.PreviewKeyDownEvent,
                };
                target.RaiseEvent(preview);
                handled = preview.Handled;
                if (!handled)
                {
                    var down = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key)
                    {
                        RoutedEvent = Keyboard.KeyDownEvent,
                    };
                    target.RaiseEvent(down);
                    handled = down.Handled;
                }
            });
            return handled;
        }

        public void Dispose()
        {
            try
            {
                // Detach the dirty workspace first: closing the shell asks
                // PrepareForApplicationClose, which would raise the real
                // unsaved-changes prompt over a dirty tab.
                if (_workspace is not null)
                {
                    SetProperty(nameof(VaultLifecycleViewModel.Workspace), null);
                    _workspace.Dispose();
                }

                if (_switcher is not null)
                {
                    _switcher.Dismiss();
                    SetProperty(nameof(VaultLifecycleViewModel.QuickSwitcher), null);
                    _switcher.Dispose();
                }

                SetProperty(nameof(VaultLifecycleViewModel.IsVaultOpen), false);
                SetField("_session", null);
                SetField("_vaultPath", string.Empty);
                _inputSource.Close();
                Shell.Close();
                PumpedDispatcher.Drain();
                _session.Dispose();
                _fixture.Dispose();
            }
            finally
            {
                CanvasSurfaceView.ShellOverlayIsOpen = _priorOverlayProbe;
            }
        }

        private void SetField(string name, object? value) =>
            (typeof(VaultLifecycleViewModel).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException($"VaultLifecycleViewModel.{name} is gone"))
            .SetValue(_lifecycle, value);

        private void SetProperty(string name, object? value) =>
            (typeof(VaultLifecycleViewModel).GetProperty(name, BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException($"VaultLifecycleViewModel.{name} is gone"))
            .SetValue(_lifecycle, value);
    }

    /// <summary>
    /// Holds exactly the given modifiers down in the thread's key state —
    /// what <c>Keyboard.Modifiers</c> reads — for the length of a press,
    /// re-applying until it reads back exactly (another process's injected
    /// input can resynchronize it on a shared desktop), then restores it.
    /// SheetKeyboardFenceTests' <c>WithModifiers</c>, for these facts.
    /// </summary>
    private static class ThreadModifiers
    {
        private const int VkShift = 0x10;
        private const int VkControl = 0x11;
        private const int VkLeftShift = 0xA0;
        private const int VkLeftControl = 0xA2;
        private const byte KeyDown = 0x80;
        private static readonly int[] AllModifierKeys =
            [0x10, 0x11, 0x12, 0xA0, 0xA1, 0xA2, 0xA3, 0xA4, 0xA5, 0x5B, 0x5C];

        public static void Hold(ModifierKeys modifiers, Action press)
        {
            byte[] saved = new byte[256];
            Assert.True(GetKeyboardState(saved));
            byte[] pressed = (byte[])saved.Clone();
            foreach (int modifier in AllModifierKeys)
            {
                pressed[modifier] &= unchecked((byte)~KeyDown);
            }

            if (modifiers.HasFlag(ModifierKeys.Shift))
            {
                pressed[VkShift] = pressed[VkLeftShift] = KeyDown;
            }

            if (modifiers.HasFlag(ModifierKeys.Control))
            {
                pressed[VkControl] = pressed[VkLeftControl] = KeyDown;
            }

            try
            {
                for (int attempt = 0; ; attempt++)
                {
                    Assert.True(SetKeyboardState(pressed));
                    if (Keyboard.Modifiers == modifiers)
                    {
                        break;
                    }

                    Assert.True(
                        attempt < 40,
                        $"environmental: the thread's key state would not hold {modifiers} "
                        + $"(read back {Keyboard.Modifiers}) — another process is holding modifiers");
                    Thread.Sleep(25);
                }

                press();
            }
            finally
            {
                _ = SetKeyboardState(saved);
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetKeyboardState(byte[] keyState);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetKeyboardState(byte[] keyState);
    }
}
