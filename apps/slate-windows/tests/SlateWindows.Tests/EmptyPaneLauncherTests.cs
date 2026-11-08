// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using SlateWindows.Commands;

namespace SlateWindows.Tests;

/// <summary>
/// DS-19: with no tab open, the editor pane offers its next actions, each with its chord
/// from the table, as the menus show it. The pane's own stop and its line are unchanged.
/// </summary>
public sealed class EmptyPaneLauncherTests
{
    [Fact]
    public void AnEmptyPaneOffersItsActions() => RunSta(() =>
    {
        using var host = new ShownShell(("note.md", "# Note\n"));
        host.Settle();
        StackPanel launcher = Launcher(host);

        Assert.True(launcher.IsVisible, "the empty pane shows no launcher");
        Button[] actions = [.. launcher.Children.OfType<Button>()];
        Assert.Equal(
            ["Quick Open…", "New Note from Template…", "Search Vault…"],
            actions.Select(button => AutomationProperties.GetName(button)));
        Assert.Equal(
            [Chord("slate.workspace.quickOpen"), Chord("slate.file.newFromTemplate"), Chord("slate.view.toggleSearch")],
            actions.Select(button => (string)button.Tag));
        // Each bound to the command its menu item and chord run (the hosted shell has no
        // Quick Open switcher, so that one is read as its binding).
        Assert.Equal(
            ["DataContext.QuickSwitcher.OpenCommand", "DataContext.Workspace.NewFromTemplateCommand", "DataContext.ToggleSearchCommand"],
            actions.Select(button => BindingOperations.GetBinding(button, Button.CommandProperty)!.Path.Path));
        Assert.All(actions, button =>
        {
            Assert.False(string.IsNullOrEmpty((string)button.Tag), $"{AutomationProperties.GetName(button)} has no chord.");
            Assert.Equal((string)button.Tag, AutomationProperties.GetAcceleratorKey(button));
            Assert.Equal(32, button.ActualHeight, 1);
        });
    });

    [Fact]
    public void TheActionsGoAwayWithATab() => RunSta(() =>
    {
        using var host = new ShownShell(("note.md", "# Note\n"));
        host.Settle();
        Assert.True(Launcher(host).IsVisible, "premise: the empty pane shows no launcher");

        host.Workspace.OpenPath("note.md");
        host.Settle();

        Assert.DoesNotContain(
            ShownShell.Descendants(host.Shell).OfType<StackPanel>(),
            panel => panel.Name == "EmptyPaneLauncher" && panel.IsVisible);
    });

    [Fact]
    public void F6StillLandsOnTheEmptyPane() => RunSta(() =>
    {
        using var host = new ShownShell(("note.md", "# Note\n"));
        host.Settle();

        Assert.True(host.Land(ShellRegionKind.EmptyEditor));
        Assert.Same(host.Shell.ContentPaneBorder, Keyboard.FocusedElement);
        Assert.Equal(ShellRegionKind.EmptyEditor, ((IShellRegionHost)host.Shell).FocusedRegion());
    });

    /// <summary>An action is the empty pane too, so F6 from it moves on to the next
    /// region rather than re-landing on the pane it's in.</summary>
    [Fact]
    public void F6FromALauncherActionMovesOn() => RunSta(() =>
    {
        using var host = new ShownShell(("note.md", "# Note\n"));
        host.Settle();
        Button quickOpen = Launcher(host).Children.OfType<Button>().First();

        Assert.True(quickOpen.Focus(), "premise: Quick Open refused the keys");
        Assert.Equal(ShellRegionKind.EmptyEditor, ((IShellRegionHost)host.Shell).FocusedRegion());
    });

    [Fact]
    public void TabFromTheEmptyPaneReachesTheFirstAction() => RunSta(() =>
    {
        using var host = new ShownShell(("note.md", "# Note\n"));
        host.Settle();
        Assert.True(host.Land(ShellRegionKind.EmptyEditor));

        Assert.True(host.Shell.ContentPaneBorder.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)));
        Assert.Same(Launcher(host).Children.OfType<Button>().First(), Keyboard.FocusedElement);
    });

    private static StackPanel Launcher(ShownShell host) =>
        Assert.Single(ShownShell.Descendants(host.Shell).OfType<StackPanel>(), panel => panel.Name == "EmptyPaneLauncher");

    private static string Chord(string id) => (string)new ChordTextExtension(id).ProvideValue(null!);

    private static void RunSta(Action body) =>
        StaThread.RunPumped(body, TimeSpan.FromSeconds(60), "STA test body timed out.");
}
