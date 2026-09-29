// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Media;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>The shipped window, shown off-screen and never activated,
/// over a real workspace attached through the lifecycle's own setters —
/// with the theme's application layers beneath its own dictionaries (the
/// editor colours a link from them) and its placement kept in the fixture,
/// the <c>EmbedTitleRealizedSurfaceTests</c> shape.</summary>
internal sealed class ShownShell : IDisposable
{
    private readonly FixtureVault _fixture = FixtureVault.Create(0, "shown-shell");
    private readonly VaultSession _session;
    private readonly VaultLifecycleViewModel _lifecycle;
    private readonly System.Reflection.PropertyInfo _slot =
        typeof(VaultLifecycleViewModel).GetProperty(nameof(VaultLifecycleViewModel.Workspace))
        ?? throw new InvalidOperationException("Workspace is gone");
    private readonly System.Reflection.PropertyInfo _open =
        typeof(VaultLifecycleViewModel).GetProperty(nameof(VaultLifecycleViewModel.IsVaultOpen))
        ?? throw new InvalidOperationException("IsVaultOpen is gone");

    public ShownShell(params (string Path, string Text)[] notes)
    {
        Assert.Null(Application.Current);
        foreach ((string path, string text) in notes)
        {
            File.WriteAllText(Path.Combine(_fixture.Root, path), text);
        }

        _session = VaultSession.OpenFilesystem(_fixture.Root);
        using (var cancel = new CancelToken())
        {
            _session.ScanInitial(cancel);
        }

        Workspace = new WorkspaceViewModel(
            _session, _fixture.Root, () => [], _ => { }, startInteractionBackgroundWork: true);
        // Registers the pack: scheme and its application authority without
        // constructing an Application (TextBoxAccessibilityTests explains).
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(Application).TypeHandle);
        Shell = new MainWindow
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
            Shell, new WindowPlacementManager(Shell, new WindowStateStore(Path.Combine(_fixture.Root, "window-state.json"))));
        ResourceDictionary[] layers = ThemeManager.DictionariesFor(SlateTheme.Light, highContrast: false);
        for (int index = 0; index < layers.Length; index++)
        {
            Shell.Resources.MergedDictionaries.Insert(index, layers[index]);
        }

        _lifecycle = Assert.IsType<VaultLifecycleViewModel>(Shell.DataContext);
        _slot.SetValue(_lifecycle, Workspace);
        _open.SetValue(_lifecycle, true);
        Shell.Show();
        Settle();
    }

    public MainWindow Shell { get; }

    /// <summary>A note's file rewritten on disk, as another program would.</summary>
    public void Rewrite(string path, string text) => File.WriteAllText(Path.Combine(_fixture.Root, path), text);

    /// <summary>A note's file as it is on disk.</summary>
    public string Read(string path) => File.ReadAllText(Path.Combine(_fixture.Root, path));

    public WorkspaceViewModel Workspace { get; }

    public void Settle()
    {
        Shell.UpdateLayout();
        PumpedDispatcher.Drain();
    }

    /// <summary>The vault opened or closed as the lifecycle marks it —
    /// the welcome view and the workspace swap.</summary>
    public void SetVaultOpen(bool open)
    {
        _open.SetValue(_lifecycle, open);
        Settle();
    }

    /// <summary>A leaf body, by its name or its automation id.</summary>
    public FrameworkElement Body(string name) =>
        Shell.FindName(name) as FrameworkElement
            ?? Descendants(Shell).OfType<FrameworkElement>().Single(element => AutomationProperties.GetAutomationId(element) == name);

    public static IEnumerable<DependencyObject> Descendants(DependencyObject root)
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

    /// <summary>The ring's landing of <paramref name="region"/>, silently —
    /// no line, no fall-through (W7-7 PR 8's tri-state answer): whether the
    /// keys are in it now.</summary>
    public bool Land(ShellRegionKind region) => LandRegion(Shell, region);

    /// <summary><see cref="Land"/> for any shell.</summary>
    public static bool LandRegion(MainWindow shell, ShellRegionKind region) =>
        ((IShellRegionHost)shell).TryLand(region, static () => { }, static () => { }) == ShellRegionLanding.Landed;

    /// <summary>Every keyboard focus change in the shell from here.</summary>
    public List<IInputElement> RecordFocusChanges()
    {
        var changes = new List<IInputElement>();
        Keyboard.AddGotKeyboardFocusHandler(Shell, (_, e) => changes.Add(e.NewFocus));
        return changes;
    }

    public void Dispose()
    {
        try
        {
            _open.SetValue(_lifecycle, false);
            _slot.SetValue(_lifecycle, null);
            Shell.Close();
            Workspace.Dispose();
        }
        finally
        {
            _session.Dispose();
            _fixture.Dispose();
        }
    }
}
