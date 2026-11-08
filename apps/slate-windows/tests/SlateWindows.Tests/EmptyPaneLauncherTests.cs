// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
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
        // The design's icons (EmptyStateLauncher.prompt.md), drawn before each label.
        Assert.Equal(["\uE8E5", "\uE710", "\uE721"], actions.Select(LauncherRowPanel.GetGlyph));
        Assert.All(actions, button => Assert.Contains(
            ShownShell.Descendants(button).OfType<TextBlock>(),
            text => text.Text == LauncherRowPanel.GetGlyph(button) && text.IsVisible));
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
            Assert.Equal(320, button.ActualWidth, 1);
        });
    });

    /// <summary>#1862: the logo is drawn at 48, so it's decoded at a size that covers 48 at
    /// 300% and no more. The source PNG is 1800 square: decoded whole it's about 13 MB a
    /// pane, which ran the CI test host out of memory.</summary>
    [Fact]
    public void TheLogoIsDecodedAtTheSizeItIsDrawn() => RunSta(() =>
    {
        using var host = new ShownShell(("note.md", "# Note\n"));
        host.Settle();
        Border logo = Launcher(host).Children.OfType<Border>().First();
        var brush = Assert.IsType<System.Windows.Media.ImageBrush>(logo.Background);
        var image = Assert.IsAssignableFrom<System.Windows.Media.Imaging.BitmapSource>(brush.ImageSource);

        Assert.Equal(48, logo.ActualWidth, 1);
        Assert.InRange(image.PixelWidth, 48, 144);
    });

    /// <summary>The launcher caps its rows at 320, so in a narrow pane a row narrows with
    /// it, and each chord stays whole and inside its row.</summary>
    [Fact]
    public void ANarrowPaneKeepsEveryChordInsideItsRow() => RunSta(() =>
    {
        using var host = new ShownShell(("note.md", "# Note\n"));
        host.Settle();
        host.Shell.ContentPaneBorder.Width = 270;
        host.Settle();
        StackPanel launcher = Launcher(host);

        Assert.All(launcher.Children.OfType<Button>(), button =>
        {
            string name = AutomationProperties.GetName(button);
            Rect row = button.TransformToAncestor(host.Shell.ContentPaneBorder)
                .TransformBounds(new Rect(button.RenderSize));
            Assert.True(row.Left >= 0 && row.Right <= 270, $"{name} spills out of the pane: {row}.");
            TextBlock chord = ShownShell.Descendants(button).OfType<TextBlock>()
                .Single(text => text.Text == (string)button.Tag);
            Rect drawn = chord.TransformToAncestor(button).TransformBounds(new Rect(chord.RenderSize));
            Assert.True(drawn.Left >= 0 && drawn.Right <= button.ActualWidth, $"{name}'s chord is outside its row: {drawn}.");
            Assert.Equal(chord.DesiredSize.Width, chord.ActualWidth, 1);
            TextBlock label = ShownShell.Descendants(button).OfType<TextBlock>().Single(text => text.Text == name);
            Rect labelDrawn = label.TransformToAncestor(button).TransformBounds(new Rect(label.RenderSize));
            Rect overlap = Rect.Intersect(labelDrawn, drawn);
            Assert.True(overlap.IsEmpty || overlap.Width * overlap.Height < 1, $"{name}'s label runs into its chord: {labelDrawn} and {drawn}.");
        });
    });

    /// <summary>A modal surface owns the keys, so the launcher's actions are off under each
    /// kind: an overlay the workspace doesn't cover, and a sheet.</summary>
    [Fact]
    public void TheActionsAreOffUnderEveryModalSurface() => RunSta(() =>
    {
        using var host = new ShownShell(("note.md", "# Note\n"));
        host.Settle();
        Button[] actions = [.. Launcher(host).Children.OfType<Button>()];

        host.Lifecycle.OpenSettingsCommand.Execute(null);
        host.Settle();
        Assert.Equal(ModalSurface.Settings, host.Shell.OpenModalSurface);
        Assert.All(actions, button => Assert.False(button.IsEnabled, $"{AutomationProperties.GetName(button)} is on under Settings."));

        host.Lifecycle.CloseSettings();
        host.Settle();
        host.Workspace.NewFromTemplateCommand.Execute(null);
        host.Settle();
        Assert.Equal(ModalSurface.TemplatePicker, host.Shell.OpenModalSurface);
        Assert.All(actions, button => Assert.False(button.IsEnabled, $"{AutomationProperties.GetName(button)} is on under the template picker."));
    });

    /// <summary>An action that holds the keys when a modal surface opens goes off, and the
    /// keys go to the surface, not stay on the disabled action.</summary>
    [Fact]
    public void AModalTakesTheKeysFromAnAction() => RunSta(() =>
    {
        using var host = new ShownShell(("note.md", "# Note\n"));
        host.Settle();
        Button search = Launcher(host).Children.OfType<Button>().Last();
        Assert.Same(search.FindResource("Slate.LauncherRowStyle"), search.Style);
        Assert.True(search.Focus(), "premise: Search Vault refused the keys");

        host.Lifecycle.OpenSettingsCommand.Execute(null);
        host.Settle();

        Assert.Equal(ModalSurface.Settings, host.Shell.OpenModalSurface);
        Assert.False(search.IsEnabled, "Search Vault is on under Settings.");
        Assert.False(search.IsKeyboardFocused, "the keys stayed on the disabled action.");
        var focused = Assert.IsAssignableFrom<UIElement>(Keyboard.FocusedElement);
        Assert.True(host.Shell.SettingsOverlay.IsKeyboardFocusWithin, $"the keys are on a {focused.GetType().Name} outside Settings.");
        Assert.True(focused.IsVisible && focused.IsEnabled, $"the keys are on an unusable {focused.GetType().Name}.");
    });

    /// <summary>The workspace host leaves the Tab order only while the pane is empty: with
    /// a note open, Tab from the pane and Shift+Tab back pass through it as they did.</summary>
    [Fact]
    public void WithANoteOpenTheEditorsTabOrderIsUnchanged() => RunSta(() =>
    {
        using var host = new ShownShell(("note.md", "# Note\n"));
        host.Settle();
        ContentControl panes = ShownShell.Descendants(host.Shell.ContentPaneBorder).OfType<ContentControl>()
            .First(control => AutomationProperties.GetName(control) == "Workspace editor panes");
        Assert.False(KeyboardNavigation.GetIsTabStop(panes), "premise: the empty pane's host is a stop");

        host.Workspace.OpenPath("note.md");
        host.Settle();
        Assert.True(KeyboardNavigation.GetIsTabStop(panes), "with a note open the host left the Tab order");
        Assert.True(host.Shell.ContentPaneBorder.Focus(), "premise: the pane refused the keys");

        Assert.True(host.Shell.ContentPaneBorder.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)));
        Assert.Same(panes, Keyboard.FocusedElement);
        Assert.True(panes.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)));
        var after = (UIElement)Keyboard.FocusedElement;
        Assert.NotSame(panes, after);
        Assert.True(after.MoveFocus(new TraversalRequest(FocusNavigationDirection.Previous)));
        Assert.Same(panes, Keyboard.FocusedElement);
    });

    /// <summary>Opening a note from an action takes the launcher away and leaves the keys
    /// on a live element, never on the collapsed action.</summary>
    [Fact]
    public void TheActionsGoAwayWithATab() => RunSta(() =>
    {
        using var host = new ShownShell(("note.md", "# Note\n"));
        host.Settle();
        Button quickOpen = Launcher(host).Children.OfType<Button>().First();
        Assert.True(quickOpen.Focus(), "premise: Quick Open refused the keys");

        host.Workspace.OpenPath("note.md");
        host.Settle();

        Assert.DoesNotContain(
            ShownShell.Descendants(host.Shell).OfType<StackPanel>(),
            panel => panel.Name == "EmptyPaneLauncher" && panel.IsVisible);
        Assert.DoesNotContain(
            ShownShell.Descendants(host.Shell).OfType<ScrollViewer>(),
            scroll => scroll.Name == "EmptyPaneLauncherScroll" && scroll.Visibility == Visibility.Visible);
        var focused = Assert.IsAssignableFrom<UIElement>(Keyboard.FocusedElement);
        Assert.True(focused.IsVisible, $"the keys are on a hidden {focused.GetType().Name}.");
        Assert.False(quickOpen.IsKeyboardFocused, "the keys stayed on the collapsed action.");
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

    /// <summary>An action is the empty pane too, so F6 and Shift+F6 from it move on to the
    /// next and previous regions, as they do from the pane's own stop, rather than
    /// re-landing on the pane it's in.</summary>
    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(0, false)]
    [InlineData(2, false)]
    public void F6FromALauncherActionMovesOn(int index, bool forward) => RunSta(() =>
    {
        using var host = new ShownShell(("note.md", "# Note\n"));
        host.Settle();
        var regions = (IShellRegionHost)host.Shell;
        // The window answers F6 for its workspace, as WorkspaceReady sets it in the app.
        host.Workspace.ShellRegionHost = host.Shell;

        Assert.True(host.Land(ShellRegionKind.EmptyEditor));
        Step(host, forward);
        ShellRegionKind? expected = regions.FocusedRegion();
        Assert.NotNull(expected);
        Assert.NotEqual(ShellRegionKind.EmptyEditor, expected);

        Button action = Launcher(host).Children.OfType<Button>().ElementAt(index);
        Assert.True(action.Focus(), $"premise: {AutomationProperties.GetName(action)} refused the keys");
        Assert.Equal(ShellRegionKind.EmptyEditor, regions.FocusedRegion());
        Step(host, forward);

        Assert.Equal(expected, regions.FocusedRegion());
        Assert.False(Launcher(host).IsKeyboardFocusWithin, "the keys stayed in the launcher.");
    });

    private static void Step(ShownShell host, bool forward)
    {
        (forward ? host.Workspace.FocusNextPaneCommand : host.Workspace.FocusPreviousPaneCommand).Execute(null);
        host.Settle();
    }

    [Fact]
    public void TabFromTheEmptyPaneReachesTheFirstAction() => RunSta(() =>
    {
        using var host = new ShownShell(("note.md", "# Note\n"));
        host.Settle();
        Assert.True(host.Land(ShellRegionKind.EmptyEditor));

        Assert.True(host.Shell.ContentPaneBorder.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)));
        Assert.Same(Launcher(host).Children.OfType<Button>().First(), Keyboard.FocusedElement);
    });

    /// <summary>A reader's object navigation under the launcher meets only its three named
    /// actions: the scroll viewer around them, and its scroll bar once it overflows, publish
    /// no element of their own.</summary>
    [Fact]
    public void TheLauncherPublishesOnlyItsActions() => RunSta(() =>
    {
        using var host = new ShownShell(("note.md", "# Note\n"));
        host.Settle();

        AssertOnlyTheActionsAreControls(host);
    });

    /// <summary>The control-view elements under the launcher's scroll viewer: each a named
    /// action, and no scroll bar part. Shared with the text-size facts, which overflow it.</summary>
    internal static void AssertOnlyTheActionsAreControls(ShownShell host)
    {
        ScrollViewer scroll = Assert.Single(
            ShownShell.Descendants(host.Shell).OfType<ScrollViewer>(),
            viewer => viewer.Name == "EmptyPaneLauncherScroll");
        AutomationPeer peer = UIElementAutomationPeer.CreatePeerForElement(scroll);
        Assert.False(peer.IsControlElement(), "the launcher's scroll viewer is a control element");
        Assert.False(peer.IsContentElement(), "the launcher's scroll viewer is a content element");

        var controls = new List<AutomationPeer>();
        var pending = new Stack<AutomationPeer>(peer.GetChildren() ?? []);
        while (pending.Count > 0)
        {
            AutomationPeer child = pending.Pop();
            if (child.IsControlElement())
            {
                controls.Add(child);
            }

            foreach (AutomationPeer grandchild in child.GetChildren() ?? [])
            {
                pending.Push(grandchild);
            }
        }

        Assert.DoesNotContain(controls, control => control.GetAutomationControlType() is AutomationControlType.ScrollBar or AutomationControlType.Thumb);
        Assert.Equal(
            ["New Note from Template…", "Quick Open…", "Search Vault…"],
            controls.Select(control => control.GetName()).Order());
    }

    private static StackPanel Launcher(ShownShell host) =>
        Assert.Single(ShownShell.Descendants(host.Shell).OfType<StackPanel>(), panel => panel.Name == "EmptyPaneLauncher");

    private static string Chord(string id) => (string)new ChordTextExtension(id).ProvideValue(null!);

    private static void RunSta(Action body) =>
        StaThread.RunPumped(body, TimeSpan.FromSeconds(60), "STA test body timed out.");
}
