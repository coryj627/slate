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
/// that hidden button and never reached the workspace, so Tab stayed where it was. The
/// welcome view still gives Open Vault the keys at a launch with no vault, and on a
/// return to it.
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
    /// Vault, and the window keeps Open Vault as its focused element, which WPF restores
    /// when the window is activated.</summary>
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

    private static void PressTab(ShownShell host)
    {
        InputManager.Current.ProcessInput(new KeyEventArgs(
            Keyboard.PrimaryDevice, PresentationSource.FromVisual(host.Shell)!, Environment.TickCount, Key.Tab)
        {
            RoutedEvent = Keyboard.PreviewKeyDownEvent,
        });
        host.Settle();
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
