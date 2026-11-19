// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using SlateWindows.Canvas;

namespace SlateWindows.Tests;

/// <summary>
/// #2016: Tab on the window's last stop wraps to its first, as Shift+Tab on the first
/// wraps to the last. The welcome view, collapsed under an open vault, named its Open
/// Vault button as its focused element; WPF's search for the window's first stop took
/// that hidden button and never reached the workspace, so Tab stayed where it was. A
/// launch with no vault still puts the keys on Open Vault, and so does a return to the
/// welcome view. The same dead end had kept the empty pane's arrows on its stop, which
/// WPF judges in the window's group; the stop keeps them on purpose now.
/// </summary>
public sealed class WindowTabWrapTests
{
    /// <summary>The last stop is More panels with the right pane open, and the canvas
    /// outline with it closed (a canvas, unlike a note's editor, leaves Tab to the window).
    /// Tab goes through the input manager, so KeyboardNavigation moves the keys as it does
    /// for a key the reader presses.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TabOnTheWindowsLastStopWrapsToItsFirst(bool rightPaneOpen) => RunSta(() =>
    {
        using var host = new ShownShell(("board.canvas", "{\"nodes\":[],\"edges\":[]}\n"));
        OpenTheBoard(host);
        host.Workspace.RightPaneToggle = rightPaneOpen;
        host.Settle();
        Assert.Equal(rightPaneOpen, host.Shell.RightPaneBorder.IsVisible);
        Button first = NewNote(host);
        Assert.True(first.Focus(), "premise: New note refused the keys");
        host.Settle();
        Assert.True(
            first.MoveFocus(new TraversalRequest(FocusNavigationDirection.Previous)),
            "premise: Shift+Tab from the window's first stop moved nowhere");
        host.Settle();
        var last = Assert.IsAssignableFrom<FrameworkElement>(Keyboard.FocusedElement);
        Assert.Equal(rightPaneOpen ? "RightPaneMorePanels" : "CanvasOutlineTree", AutomationProperties.GetAutomationId(last));
        List<IInputElement> changes = host.RecordFocusChanges();

        PressTab(host);

        Assert.True(
            ReferenceEquals(first, Keyboard.FocusedElement),
            $"Tab on {AutomationProperties.GetAutomationId(last)} left the keys on {Describe(Keyboard.FocusedElement)}");
        Assert.Equal([first], changes);
    });

    /// <summary>A launch with no vault opens on the welcome view with the keys on Open
    /// Vault, and Open Vault is the window's own focused element, where WPF sends the keys
    /// when it focuses the window itself (a first activation, a menu's dismissal).</summary>
    [Fact]
    public void ALaunchWithNoVaultPutsTheKeysOnOpenVault() => RunSta(() =>
    {
        using var shell = new WelcomeShell();

        Assert.True(shell.Shell.WelcomeRoot.IsVisible, "premise: the launch did not show the welcome view");
        Assert.True(
            ReferenceEquals(shell.Shell.OpenVaultButton, Keyboard.FocusedElement),
            $"the launch left the keys on {Describe(Keyboard.FocusedElement)}");
        Assert.Same(shell.Shell.OpenVaultButton, FocusManager.GetFocusedElement(shell.Shell));
    });

    /// <summary>Closing a vault returns to the welcome view with the keys on Open Vault.</summary>
    [Fact]
    public void AReturnToTheWelcomeViewPutsTheKeysOnOpenVault() => RunSta(() =>
    {
        using var shell = new WelcomeShell();
        shell.ReleaseParks();
        shell.Lifecycle.OpenVaultCommand.Execute(null);
        shell.WaitForTheOpenToEnd();
        Assert.True(shell.Lifecycle.IsVaultOpen, "premise: the vault did not open");
        Assert.True(
            PumpedDispatcher.PumpUntil(() => shell.Shell.WorkspaceRoot.IsKeyboardFocusWithin, TimeSpan.FromSeconds(10)),
            $"premise: the open left the keys on {Describe(Keyboard.FocusedElement)}");

        shell.Lifecycle.CloseVault();
        shell.Settle();

        Assert.True(shell.Shell.WelcomeRoot.IsVisible, "premise: the close did not show the welcome view");
        Assert.True(
            ReferenceEquals(shell.Shell.OpenVaultButton, Keyboard.FocusedElement),
            $"the return left the keys on {Describe(Keyboard.FocusedElement)}");
    });

    /// <summary>The empty pane's stop is the content pane's border itself, and WPF judges a
    /// stop's arrows in its parent's directional group, which for the border is the
    /// window's. While the hidden Open Vault button ended WPF's search, no arrow found a
    /// target; without it Left reached the Files pane and Right the right pane (the shell
    /// gate's RegionStops_ArrowsStayInRegion). Every arrow stays on the stop.</summary>
    [Theory]
    [InlineData(Key.Left)]
    [InlineData(Key.Right)]
    [InlineData(Key.Up)]
    [InlineData(Key.Down)]
    public void TheEmptyPanesStopKeepsItsArrows(Key key) => RunSta(() =>
    {
        using var host = new ShownShell(("note.md", "# Note\n"));
        Assert.Empty(host.Workspace.ActiveGroup.Tabs);
        Assert.True(host.Land(ShellRegionKind.EmptyEditor), "premise: the empty pane's stop took no keys");
        Assert.Same(host.Shell.ContentPaneBorder, Keyboard.FocusedElement);
        List<IInputElement> changes = host.RecordFocusChanges();

        Press(host, key);

        Assert.True(
            ReferenceEquals(host.Shell.ContentPaneBorder, Keyboard.FocusedElement),
            $"{key} on the empty pane's stop moved the keys to {Describe(Keyboard.FocusedElement)}");
        Assert.Empty(changes);
    });

    /// <summary>The menu bar's logo is the bar's one header: Up and Down open its menu, and
    /// Left and Right have nowhere to go. The bar's None mode refuses an arrow coming in but
    /// not one going out, so WPF judged them in the window's group; the hidden Open Vault
    /// button's dead end had kept them, and without it Right reached the editor panes and
    /// Left the rail. Both stay on the logo.</summary>
    [Theory]
    [InlineData(Key.Left)]
    [InlineData(Key.Right)]
    public void TheMenuBarsLogoKeepsLeftAndRight(Key key) => RunSta(() =>
    {
        using var host = new ShownShell(("note.md", "# Note\n"));
        host.Workspace.OpenPath("note.md");
        host.Settle();
        if (((IShellRegionHost)host.Shell).TryLand(ShellRegionKind.MenuBar, () => { }, () => { }) == ShellRegionLanding.Refused)
        {
            Assert.Skip("this desktop refused the menu the keys (#1680), so the logo's arrows weren't measured on it");
        }

        host.Settle();
        Assert.Same(host.Shell.AppMenuItem, Keyboard.FocusedElement);
        List<IInputElement> changes = host.RecordFocusChanges();

        Press(host, key);

        Assert.True(
            ReferenceEquals(host.Shell.AppMenuItem, Keyboard.FocusedElement),
            $"{key} on the menu bar's logo moved the keys to {Describe(Keyboard.FocusedElement)}");
        Assert.Empty(changes);
        Assert.False(host.Shell.AppMenuItem.IsSubmenuOpen);
    });

    /// <summary>Ctrl+Alt+arrows on the logo are still the window's pane chords: the logo's
    /// key handler keeps only what WPF's navigation would take, and Ctrl+Alt+Left goes on to
    /// the window's binding, which asks for the Files pane, the edge to the left.</summary>
    [Fact]
    public void ThePaneChordsStillRunFromTheLogo() => RunSta(() =>
    {
        using var host = new ShownShell(("note.md", "# Note\n"));
        host.Workspace.OpenPath("note.md");
        host.Settle();
        if (((IShellRegionHost)host.Shell).TryLand(ShellRegionKind.MenuBar, () => { }, () => { }) == ShellRegionLanding.Refused)
        {
            Assert.Skip("this desktop refused the menu the keys (#1680), so the logo's chords weren't measured on it");
        }

        host.Settle();
        Assert.Same(host.Shell.AppMenuItem, Keyboard.FocusedElement);
        WorkspaceFocusBoundary? boundary = null;
        host.Workspace.FocusBoundaryRequested += (_, requested) => boundary = requested;

        CommandPaletteTests.ThreadModifiers.Hold(ModifierKeys.Control | ModifierKeys.Alt, () => Press(host, Key.Left));

        Assert.Equal(WorkspaceFocusBoundary.Files, boundary);
    });

    /// <summary>Codex round 2: Ctrl+Alt+Shift+Left and Right on the logo still move the active
    /// tab, through the window's bindings, in both directions.</summary>
    [Theory]
    [InlineData(Key.Left)]
    [InlineData(Key.Right)]
    public void MoveTabStillRunsFromTheLogo(Key key) => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "# A\n"), ("b.md", "# B\n"));
        host.Workspace.OpenPath("a.md");
        host.Workspace.OpenPath("b.md", WorkspaceOpenTarget.NewTab);
        host.Settle();
        WorkspaceTabViewModel[] before = [.. host.Workspace.ActiveGroup.Tabs];
        Assert.Equal(2, before.Length);
        WorkspaceTabViewModel moving = key == Key.Left ? before[1] : before[0];
        host.Workspace.ActiveGroup.ActiveTab = moving;
        host.Settle();
        if (((IShellRegionHost)host.Shell).TryLand(ShellRegionKind.MenuBar, () => { }, () => { }) == ShellRegionLanding.Refused)
        {
            Assert.Skip("this desktop refused the menu the keys (#1680), so the logo's chords weren't measured on it");
        }

        host.Settle();
        Assert.Same(host.Shell.AppMenuItem, Keyboard.FocusedElement);

        CommandPaletteTests.ThreadModifiers.Hold(ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift, () => Press(host, key));

        Assert.Equal([before[1], before[0]], host.Workspace.ActiveGroup.Tabs);
    });

    /// <summary>Codex round 2: an arrow chord no window binding takes stays on the logo, as a
    /// bare Left or Right does, whatever its modifiers: WPF's navigation takes those too.
    /// (Up and Down, with any modifiers, open the logo's menu.)</summary>
    [Theory]
    [InlineData(ModifierKeys.Control | ModifierKeys.Shift, Key.Left)]
    [InlineData(ModifierKeys.Control, Key.Right)]
    public void TheLogoKeepsAnUnboundArrowChord(ModifierKeys modifiers, Key key) => RunSta(() =>
    {
        using var host = new ShownShell(("note.md", "# Note\n"));
        host.Workspace.OpenPath("note.md");
        host.Settle();
        if (((IShellRegionHost)host.Shell).TryLand(ShellRegionKind.MenuBar, () => { }, () => { }) == ShellRegionLanding.Refused)
        {
            Assert.Skip("this desktop refused the menu the keys (#1680), so the logo's chords weren't measured on it");
        }

        host.Settle();
        Assert.Same(host.Shell.AppMenuItem, Keyboard.FocusedElement);

        CommandPaletteTests.ThreadModifiers.Hold(modifiers, () => Press(host, key));

        Assert.True(
            ReferenceEquals(host.Shell.AppMenuItem, Keyboard.FocusedElement),
            $"{modifiers}+{key} on the logo moved the keys to {Describe(Keyboard.FocusedElement)}");
    });

    /// <summary>Codex round 2: on the empty pane's stop, a chord no window binding runs stays
    /// too: Move Tab is bound to Ctrl+Alt+Shift+Left but has no tab to move, and
    /// Ctrl+Shift+Right is bound to nothing.</summary>
    [Theory]
    [InlineData(ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift, Key.Left)]
    [InlineData(ModifierKeys.Control | ModifierKeys.Shift, Key.Right)]
    public void TheEmptyPanesStopKeepsAChordNoBindingRuns(ModifierKeys modifiers, Key key) => RunSta(() =>
    {
        using var host = new ShownShell(("note.md", "# Note\n"));
        Assert.True(host.Land(ShellRegionKind.EmptyEditor), "premise: the empty pane's stop took no keys");
        Assert.Same(host.Shell.ContentPaneBorder, Keyboard.FocusedElement);

        CommandPaletteTests.ThreadModifiers.Hold(modifiers, () => Press(host, key));

        Assert.True(
            ReferenceEquals(host.Shell.ContentPaneBorder, Keyboard.FocusedElement),
            $"{modifiers}+{key} on the empty pane's stop moved the keys to {Describe(Keyboard.FocusedElement)}");
    });

    /// <summary>Ctrl+Alt+arrows on the empty pane's stop are still the window's pane chords
    /// (its preview route takes them before the stop's own key handler): Ctrl+Alt+Left asks
    /// for the Files pane, the edge to its left.</summary>
    [Fact]
    public void ThePaneChordsStillRunFromTheEmptyPanesStop() => RunSta(() =>
    {
        using var host = new ShownShell(("note.md", "# Note\n"));
        Assert.True(host.Land(ShellRegionKind.EmptyEditor), "premise: the empty pane's stop took no keys");
        WorkspaceFocusBoundary? boundary = null;
        host.Workspace.FocusBoundaryRequested += (_, requested) => boundary = requested;

        CommandPaletteTests.ThreadModifiers.Hold(ModifierKeys.Control | ModifierKeys.Alt, () => Press(host, Key.Left));

        Assert.Equal(WorkspaceFocusBoundary.Files, boundary);
    });

    /// <summary>An arrow from inside the content pane that no element takes is still WPF's
    /// to move within the pane: Down from an empty canvas's onboarding line reaches the
    /// canvas outline.</summary>
    [Fact]
    public void AnArrowFromInsideThePaneStillMovesWithinIt() => RunSta(() =>
    {
        using var host = new ShownShell(("board.canvas", "{\"nodes\":[],\"edges\":[]}\n"));
        OpenTheBoard(host);
        FrameworkElement onboarding = ShownShell.Descendants(host.Shell.ContentPaneBorder).OfType<FrameworkElement>()
            .Single(element => AutomationProperties.GetAutomationId(element) == "CanvasEmptyOnboarding" && element.IsVisible);
        Assert.True(onboarding.Focus(), "premise: the onboarding line refused the keys");
        host.Settle();

        Press(host, Key.Down);

        var landed = Assert.IsAssignableFrom<FrameworkElement>(Keyboard.FocusedElement);
        Assert.True(
            AutomationProperties.GetAutomationId(landed) == "CanvasOutlineTree",
            $"Down on the onboarding line left the keys on {Describe(landed)}");
    });

    /// <summary>The rail's ends: Down on More panels, the rail's foot, and Up on the bare strip
    /// when its first row is its current one, its head, have nowhere to go in the rail. The
    /// window's Cycle wrapped them to the rail's other end once the hidden Open Vault button no
    /// longer ended WPF's search; the rail's own Contained group keeps them. A vault opened
    /// through the lifecycle, so the rail is laid out as it is in use.</summary>
    [Theory]
    [InlineData("More panels", Key.Down)]
    [InlineData("the strip", Key.Up)]
    public void TheRailKeepsTheArrowsAtItsEnds(string stop, Key key) => RunSta(() =>
    {
        using var shell = OpenTheFixtureVault();
        UIElement held = stop == "More panels" ? shell.Shell.RightPaneMorePanelsButton : shell.Shell.RightPaneLeavesList;
        if (held is ListBox strip)
        {
            // The bare strip's own Up moves to its first row and makes it the current one.
            Assert.True(strip.Focus(), "premise: the strip refused the keys");
            shell.Settle();
            Press(shell.Shell, shell.Settle, Key.Up);
            Assert.True(
                ReferenceEquals(strip.ItemContainerGenerator.ContainerFromIndex(0), Keyboard.FocusedElement),
                $"premise: Up on the bare strip went to {Describe(Keyboard.FocusedElement)}, not its first row");
        }

        Assert.True(held.Focus() && ReferenceEquals(held, Keyboard.FocusedElement), $"premise: {stop} refused the keys");
        shell.Settle();

        Press(shell.Shell, shell.Settle, key);

        Assert.True(
            ReferenceEquals(held, Keyboard.FocusedElement),
            $"{key} on {stop} moved the keys to {Describe(Keyboard.FocusedElement)}");
    });

    /// <summary>Up on More panels still enters the strip, on its open panel's row, as it did
    /// before #2016: the rail's group keeps its ends, not its own arrows.</summary>
    [Fact]
    public void UpOnMorePanelsStillEntersTheStrip() => RunSta(() =>
    {
        using var shell = OpenTheFixtureVault();
        Assert.True(shell.Shell.RightPaneMorePanelsButton.Focus(), "premise: More panels refused the keys");
        shell.Settle();

        Press(shell.Shell, shell.Settle, Key.Up);

        Assert.True(
            Keyboard.FocusedElement is ListBoxItem && shell.Shell.RightPaneLeavesList.IsKeyboardFocusWithin,
            $"Up on More panels left the keys on {Describe(Keyboard.FocusedElement)}");
    });

    private static WelcomeShell OpenTheFixtureVault()
    {
        var shell = new WelcomeShell();
        shell.ReleaseParks();
        shell.Lifecycle.OpenVaultCommand.Execute(null);
        shell.WaitForTheOpenToEnd();
        Assert.True(shell.Lifecycle.IsVaultOpen, "premise: the fixture vault did not open");
        Assert.True(shell.Shell.RightPaneMorePanelsButton.IsVisible, "premise: the rail is not shown");
        return shell;
    }

    private static void OpenTheBoard(ShownShell host)
    {
        host.Workspace.OpenPath("board.canvas");
        CanvasDocumentViewModel document = Assert.IsType<CanvasDocumentViewModel>(
            Assert.IsType<WorkspaceTabViewModel>(host.Workspace.ActiveGroup.ActiveTab).Canvas);
        Assert.True(
            PumpedDispatcher.PumpUntil(() => document.State == CanvasLoadState.Ready, TimeSpan.FromSeconds(10)),
            $"premise: the canvas never loaded ({document.State})");
        host.Settle();
    }

    private static Button NewNote(ShownShell host) =>
        Assert.Single(
            ShownShell.Descendants(host.Shell).OfType<Button>(),
            button => AutomationProperties.GetAutomationId(button) == "SidebarNewNote");

    private static void PressTab(ShownShell host) => Press(host, Key.Tab);

    private static void Press(ShownShell host, Key key) => Press(host.Shell, host.Settle, key);

    private static void Press(MainWindow shell, Action settle, Key key)
    {
        InputManager.Current.ProcessInput(new KeyEventArgs(
            Keyboard.PrimaryDevice, PresentationSource.FromVisual(shell)!, Environment.TickCount, key)
        {
            RoutedEvent = Keyboard.PreviewKeyDownEvent,
        });
        settle();
    }

    private static string Describe(IInputElement? element) => element is FrameworkElement framework
        ? $"{framework.GetType().Name} '{AutomationProperties.GetAutomationId(framework)}' '{AutomationProperties.GetName(framework)}'"
        : element?.GetType().Name ?? "nothing";

    private static void RunSta(Action body)
    {
        Func<bool> priorOverlayProbe = CanvasSurfaceView.ShellOverlayIsOpen;
        try
        {
            StaThread.RunPumped(body, TimeSpan.FromSeconds(90), "STA test body timed out.");
        }
        finally
        {
            CanvasSurfaceView.ShellOverlayIsOpen = priorOverlayProbe;
        }
    }
}
