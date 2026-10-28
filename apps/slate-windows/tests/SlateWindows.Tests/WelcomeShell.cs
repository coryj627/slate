// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SlateWindows.Commands;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// WB-9: the shipped window on its welcome view, no vault open, whose vault
/// open runs through the real lifecycle over the fixture's seams — a counted
/// pick answering <see cref="PickAnswer"/>, the open-session and scan phases
/// each parked on a gate of their own, the recents and palette-recents files
/// and the window placement in a temp state folder (never the user's), a
/// counted missing-recent box that keeps the entry, and every line the
/// window posts. Shown off-screen and never activated, the
/// <see cref="ShownShell"/> shape.
/// </summary>
internal sealed class WelcomeShell : IDisposable
{
    private static readonly TimeSpan ParkBound = TimeSpan.FromSeconds(30);

    private readonly FixtureVault _fixture = FixtureVault.Create(2, "welcome-shell");
    private readonly string _state = Path.Combine(Path.GetTempPath(), $"slate-welcome-shell-{Guid.NewGuid():N}");
    private readonly ManualResetEventSlim _openSessionGate = new(false);
    private readonly ManualResetEventSlim _scanGate = new(false);
    private readonly ManualResetEventSlim _openSessionEntered = new(false);
    private readonly ManualResetEventSlim _scanEntered = new(false);
    private readonly ManualResetEventSlim _treeGate = new(false);
    private readonly ManualResetEventSlim _treeEntered = new(false);
    private bool _parkTheTree;
    private int _pickCount;
    private int _confirmCount;

    /// <summary>In a <c>recents</c> list, the fixture vault itself.</summary>
    public const string TheFixtureVault = "<the fixture vault>";

    /// <param name="recents">Folders the recents file holds before the
    /// window loads it, added in this order (so the last is listed first);
    /// <see cref="TheFixtureVault"/> names the fixture's own vault.</param>
    public WelcomeShell(params string[] recents)
    {
        Assert.Null(Application.Current);
        Directory.CreateDirectory(_state);
        PickAnswer = _fixture.Root;
        RecentVaults = new RecentVaultsStore(Path.Combine(_state, "recent-vaults.json"));
        foreach (string recent in recents)
        {
            _ = RecentVaults.Add(RecentVault.FromPath(recent == TheFixtureVault ? _fixture.Root : recent));
        }

        var seams = new VaultOpenSeams(
            PickVault: () =>
            {
                _ = Interlocked.Increment(ref _pickCount);
                DuringPick?.Invoke();
                return Task.FromResult<string?>(PickAnswer);
            },
            OpenSession: root => Task.Run(() =>
            {
                _openSessionEntered.Set();
                Park(_openSessionGate, "the open-session phase");
                return VaultSession.OpenFilesystem(root);
            }),
            SessionLoad: work => Task.Run(() =>
            {
                _scanEntered.Set();
                Park(_scanGate, "the scan phase");
                return ScanFault is { } fault ? throw fault : work();
            }),
            RecentVaults: RecentVaults,
            ConfirmRemoveMissingRecent: missing =>
            {
                _ = Interlocked.Increment(ref _confirmCount);
                DuringConfirm?.Invoke();
                return Task.FromResult(false);
            },
            ObservePosted: Posted.Add,
            TreeWorker: (work, token) => Task.Run(
                () =>
                {
                    if (Volatile.Read(ref _parkTheTree))
                    {
                        _treeEntered.Set();
                        Park(_treeGate, "the tree's read");
                    }

                    work();
                },
                token));

        // Registers the pack: scheme and its application authority without
        // constructing an Application (TextBoxAccessibilityTests explains).
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(Application).TypeHandle);
        Shell = new MainWindow(
            new CommandPaletteRecentsStore(Path.Combine(_state, "palette-recents.json")),
            paletteLane: null,
            openSeams: seams)
        {
            ShowActivated = false,
            ShowInTaskbar = false,
            Width = 1200,
            Height = 800,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -20_000,
            Top = -20_000,
        };
        System.Reflection.FieldInfo placement = typeof(MainWindow).GetField(
                "_windowPlacement", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?? throw new InvalidOperationException("MainWindow no longer holds its placement in _windowPlacement");
        placement.SetValue(
            Shell, new WindowPlacementManager(Shell, new WindowStateStore(Path.Combine(_state, "window-state.json"))));
        ResourceDictionary[] layers = ThemeManager.DictionariesFor(SlateTheme.Light, highContrast: false);
        for (int index = 0; index < layers.Length; index++)
        {
            Shell.Resources.MergedDictionaries.Insert(index, layers[index]);
        }

        Lifecycle = Assert.IsType<VaultLifecycleViewModel>(Shell.DataContext);
        Lifecycle.OpenRequestedForTests = OpenRequests.Add;
        Shell.Show();
        Settle();
    }

    public MainWindow Shell { get; }

    public VaultLifecycleViewModel Lifecycle { get; }

    /// <summary>The fixture vault the pick answers by default.</summary>
    public string VaultRoot => _fixture.Root;

    /// <summary>A folder under the fixture's temp state (created on request).</summary>
    public string StateFolder(string name, bool create = true)
    {
        string path = Path.Combine(_state, name);
        if (create)
        {
            Directory.CreateDirectory(path);
        }

        return path;
    }

    public RecentVaultsStore RecentVaults { get; }

    /// <summary>The recents file's text, or null while it does not exist.</summary>
    public string? RecentVaultsFileText()
    {
        string path = Path.Combine(_state, "recent-vaults.json");
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    /// <summary>What the folder pick answers.</summary>
    public string? PickAnswer { get; set; }

    public int PickCount => Volatile.Read(ref _pickCount);

    public int ConfirmCount => Volatile.Read(ref _confirmCount);

    /// <summary>Runs inside the folder pick, as if the picker were up.</summary>
    public Action? DuringPick { get; set; }

    /// <summary>Runs inside the missing-recent box, as if the box were up.</summary>
    public Action? DuringConfirm { get; set; }

    /// <summary>When set, the scan phase throws it once released.</summary>
    public Exception? ScanFault { get; set; }

    /// <summary>Every path <c>OpenVaultAsync</c> was asked to open, in order.</summary>
    public List<string> OpenRequests { get; } = [];

    /// <summary>Every line the window posted, as rendered, in order.</summary>
    public List<RenderedAnnouncement> Posted { get; } = [];

    public void Settle()
    {
        Shell.UpdateLayout();
        PumpedDispatcher.Drain();
    }

    /// <summary>Let both phases run at once, for a fact that parks neither.</summary>
    public void ReleaseParks()
    {
        _openSessionGate.Set();
        _scanGate.Set();
    }

    public void ReleaseOpenSession() => _openSessionGate.Set();

    /// <summary>Park the next open's two phases again (after a fact's first
    /// open ran with the parks released).</summary>
    public void ReArmParks()
    {
        _openSessionEntered.Reset();
        _scanEntered.Reset();
        _openSessionGate.Reset();
        _scanGate.Reset();
    }

    public void ReleaseScan() => _scanGate.Set();

    /// <summary>#1684: park the Files tree's reads on their worker from here
    /// until <see cref="ReleaseTheTree"/>.</summary>
    public void ParkTheTree() => Volatile.Write(ref _parkTheTree, true);

    public void ReleaseTheTree()
    {
        Volatile.Write(ref _parkTheTree, false);
        _treeGate.Set();
    }

    /// <summary>Pump until a Files tree read waits at its gate.</summary>
    public void WaitForTheTreePark() =>
        Assert.True(
            PumpedDispatcher.PumpUntil(() => _treeEntered.IsSet, ParkBound),
            "the Files tree never read on its worker");

    /// <summary>Pump until the open's session phase waits at its gate.</summary>
    public void WaitForTheOpenSessionPark() =>
        Assert.True(
            PumpedDispatcher.PumpUntil(() => _openSessionEntered.IsSet, ParkBound),
            "the open never reached its open-session phase");

    /// <summary>Pump until the open's scan phase waits at its gate.</summary>
    public void WaitForTheScanPark() =>
        Assert.True(
            PumpedDispatcher.PumpUntil(() => _scanEntered.IsSet, ParkBound),
            "the open never reached its scan phase");

    /// <summary>Pump until no open runs, then settle.</summary>
    public void WaitForTheOpenToEnd()
    {
        Assert.True(
            PumpedDispatcher.PumpUntil(() => !Lifecycle.IsBusy && !Lifecycle.IsOpening, ParkBound),
            "the open never ended");
        Settle();
    }

    /// <summary>The recent vault button for <paramref name="path"/>.</summary>
    public Button RecentButton(string path) =>
        Assert.Single(
            ShownShell.Descendants(Shell).OfType<Button>(),
            button => button.DataContext is RecentVault recent
                && string.Equals(recent.Path, path, StringComparison.OrdinalIgnoreCase));

    /// <summary>Every keyboard focus change in the shell from here.</summary>
    public List<IInputElement> RecordFocusChanges()
    {
        var changes = new List<IInputElement>();
        Keyboard.AddGotKeyboardFocusHandler(Shell, (_, e) => changes.Add(e.NewFocus));
        return changes;
    }

    private static void Park(ManualResetEventSlim gate, string what)
    {
        if (!gate.Wait(ParkBound))
        {
            throw new TimeoutException($"{what} was never released");
        }
    }

    public void Dispose()
    {
        try
        {
            ReleaseParks();
            ReleaseTheTree();
            // A window whose open still runs refuses its own close.
            _ = PumpedDispatcher.PumpUntil(() => !Lifecycle.IsBusy, ParkBound);
            Shell.Close();
        }
        finally
        {
            _fixture.Dispose();
            try
            {
                Directory.Delete(_state, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }
}
