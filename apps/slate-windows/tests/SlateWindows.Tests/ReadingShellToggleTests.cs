// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Media;
using SlateWindows.Reading;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// The shell accessibility gate's two math-and-diagram reading journeys (the
/// identity contract and RangeFromChild), in process: the real MainWindow —
/// shown off-screen, never activated, no input injected — over a workspace
/// with production scheduling (the reading fetch on the pool), the note opened
/// from a Files selection, and reading mode toggled through the Editor menu's
/// item the way a UIA client invokes it (the menu expanded, the item's Invoke
/// pattern), with the client walking the window before the toggle and while
/// the content arrives. PR 8's readiness (R-10) and #1279's retirements run for
/// real: the toggle starts two refreshes — the surface's bind and the tab's
/// activation — and the first is retired at a stage boundary by the second.
/// </summary>
public sealed class ReadingShellToggleTests
{
    private const string Note =
        "# Identity note\n\nBody text.\n\n$$x^2 + 1$$\n\n```mermaid\nflowchart LR\nA --> B\n```\n";

    /// <summary>The note's first projection reaches the surface: its math and
    /// diagram elements are in the automation tree, the loading placeholder is
    /// gone, and the refresh behind it settled, not failed.</summary>
    [Fact]
    public void TheEditorMenusToggleProjectsAMathAndDiagramNote() => StaThread.RunPumped(() =>
    {
        using FixtureVault fixture = FixtureVault.Create(0, "reading-shell-toggle");
        File.WriteAllText(Path.Combine(fixture.Root, "note.md"), Note);
        using VaultSession session = VaultSession.OpenFilesystem(fixture.Root);
        using (var cancel = new CancelToken())
        {
            session.ScanInitial(cancel);
        }
        // Registers the pack: scheme and its application authority without
        // constructing an Application (TextBoxAccessibilityTests explains).
        RuntimeHelpers.RunClassConstructor(typeof(Application).TypeHandle);
        var shell = new MainWindow();
        var lifecycle = Assert.IsType<VaultLifecycleViewModel>(shell.DataContext);
        WorkspaceViewModel? workspace = null;
        try
        {
            FieldInfo placement = typeof(MainWindow).GetField("_windowPlacement", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new Xunit.Sdk.XunitException("MainWindow no longer holds its placement in _windowPlacement; "
                    + "the fact must not show the shell against the user's placement store.");
            placement.SetValue(shell, new WindowPlacementManager(shell, new WindowStateStore(Path.Combine(fixture.Root, "window-state.json"))));
            ResourceDictionary[] layers = ThemeManager.DictionariesFor(SlateTheme.Light, highContrast: false);
            for (int index = 0; index < layers.Length; index++)
            {
                shell.Resources.MergedDictionaries.Insert(index, layers[index]);
            }
            shell.WindowStartupLocation = WindowStartupLocation.Manual;
            shell.Left = -20_000;
            shell.Top = -20_000;
            shell.ShowActivated = false;
            shell.ShowInTaskbar = false;
            workspace = new WorkspaceViewModel(
                session,
                fixture.Root,
                () => [],
                _ => { },
                startInteractionBackgroundWork: true,
                preferencesStore: new AppPreferencesStore(Path.Combine(fixture.Root, "preferences.json")));
            SetProperty(lifecycle, nameof(VaultLifecycleViewModel.Workspace), workspace);
            SetProperty(lifecycle, nameof(VaultLifecycleViewModel.IsVaultOpen), true);
            shell.Show();
            PumpedDispatcher.Drain();

            // A Files selection shows the note; the client waits for its editor.
            workspace.OpenPath("note.md", fromSelection: true);
            WorkspaceTabViewModel? tab = null;
            Assert.True(
                PumpedDispatcher.PumpUntil(() =>
                {
                    tab = workspace.ActiveGroup.ActiveTab;
                    return tab is { Path: "note.md" }
                        && Descendants<SlateTextEditor>(shell).Any(editor => ReferenceEquals(editor.DataContext, tab) && editor.IsVisible);
                }),
                "the note's editor never showed");
            WalkAutomationTree(shell);

            // The Editor menu, expanded, and its Toggle Reading Mode item invoked.
            MenuItem editorMenu = Assert.Single(
                Descendants<MenuItem>(shell),
                item => AutomationProperties.GetAutomationId(item) == "EditorMenu");
            ((IExpandCollapseProvider)UIElementAutomationPeer.CreatePeerForElement(editorMenu)
                .GetPattern(PatternInterface.ExpandCollapse)).Expand();
            PumpedDispatcher.Drain();
            MenuItem toggle = Assert.Single(
                editorMenu.Items.OfType<MenuItem>(),
                item => AutomationProperties.GetAutomationId(item) == "EditorToggleReadingModeMenuItem");
            ((IInvokeProvider)UIElementAutomationPeer.CreatePeerForElement(toggle)
                .GetPattern(PatternInterface.Invoke)).Invoke();

            ReadingSurface? surface = null;
            bool arrived = PumpedDispatcher.PumpUntil(
                () =>
                {
                    WalkAutomationTree(shell);
                    surface = Descendants<ReadingSurface>(shell)
                        .SingleOrDefault(candidate => ReferenceEquals(candidate.DataContext, tab) && candidate.IsVisible);
                    return surface is not null && HasPeer(surface, "math") && HasPeer(surface, "diagram");
                },
                TimeSpan.FromSeconds(60));
            Assert.True(
                arrived,
                "the note's math and diagram never reached the reading surface; it reads: "
                + (surface is null ? "<no surface shown>" : UiaText(surface)));
            Assert.DoesNotContain("Loading reading view", UiaText(surface!), StringComparison.Ordinal);
            ReadingContentViewModel model = Assert.IsType<ReadingContentViewModel>(tab!.Reading);
            Assert.False(model.RefreshInFlight);
            Assert.False(model.LastRefreshFailed);
            Assert.False(model.PublishedFailureNotice);
        }
        finally
        {
            workspace?.Panels.Shutdown();
            SetProperty(lifecycle, nameof(VaultLifecycleViewModel.IsVaultOpen), false);
            SetProperty(lifecycle, nameof(VaultLifecycleViewModel.Workspace), null);
            shell.Close();
            workspace?.Dispose();
        }
    }, TimeSpan.FromSeconds(120), "The reading shell fixture timed out.");

    /// <summary>A UIA client's walk of the window: every peer's name and
    /// children, as a client's descendant search asks for them.</summary>
    private static void WalkAutomationTree(UIElement root) =>
        Walk(UIElementAutomationPeer.CreatePeerForElement(root), 0);

    private static void Walk(AutomationPeer node, int depth)
    {
        if (depth > 40)
        {
            return;
        }
        _ = node.GetName();
        foreach (AutomationPeer child in node.GetChildren() ?? [])
        {
            Walk(child, depth + 1);
        }
    }

    private static bool HasPeer(ReadingSurface surface, string localizedControlType)
    {
        return Find(UIElementAutomationPeer.CreatePeerForElement(surface), 0);

        bool Find(AutomationPeer node, int depth)
        {
            if (depth > 12)
            {
                return false;
            }
            if (string.Equals(node.GetLocalizedControlType(), localizedControlType, StringComparison.Ordinal))
            {
                return true;
            }
            foreach (AutomationPeer child in node.GetChildren() ?? [])
            {
                if (Find(child, depth + 1))
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>What a screen reader reads: the surface peer's Text pattern.</summary>
    private static string UiaText(ReadingSurface surface)
    {
        AutomationPeer peer = UIElementAutomationPeer.CreatePeerForElement(surface);
        var text = Assert.IsAssignableFrom<ITextProvider>(peer.GetPattern(PatternInterface.Text));
        return text.DocumentRange.GetText(-1);
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
            {
                yield return match;
            }
            foreach (T descendant in Descendants<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private static void SetProperty(object target, string name, object? value) =>
        (target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Missing property: {name}"))
        .SetValue(target, value);
}
