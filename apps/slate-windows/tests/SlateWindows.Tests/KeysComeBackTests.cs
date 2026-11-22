// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;

namespace SlateWindows.Tests;

/// <summary>
/// #1866 (CD11), keys that came back to nothing are landed. A control that goes off while
/// the window is away (the reader in another program, or a box of the window's own up)
/// holds no keys then, so nothing moves them first. When the window comes back WPF offers
/// the keys to the element that had them, an element that is off refuses, and they rest on
/// the window: no element holds them, and no chord has a target (the record's B4). On
/// activation the window now lands them in the region the element was in.
/// </summary>
/// <remarks>
/// The reader's leaving and return are sent to the window as Windows sends them, in
/// process: WM_ACTIVATE and WM_KILLFOCUS, then WM_ACTIVATE and WM_SETFOCUS
/// (<see cref="WindowActivation.GoAway"/>, <see cref="WindowActivation.ComeBack"/>), so
/// the window's own handlers and WPF's own restore run. No other program or real box is
/// involved.
/// </remarks>
public sealed class KeysComeBackTests
{
    /// <summary>A command button of the editor region, turned off while the window is
    /// away: on its return the keys are on a live element of the same region.</summary>
    [Fact]
    public void AButtonTurnedOffWhileTheWindowIsAwayLandsTheKeysInItsRegion() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "---\nstatus: draft\n---\n# A\n"));
        host.Workspace.OpenPath("a.md");
        host.Settle();
        host.Shell.Activate();
        host.Settle();
        var region = (FrameworkElement)host.Shell.FindName("ContentPaneBorder");
        Button button = ShownShell.Descendants(region).OfType<Button>()
            .First(candidate => AutomationProperties.GetAutomationId(candidate) == "PropertiesAddButton" && candidate.IsVisible);
        Assert.NotNull(button.Command);
        TakeTheKeys(button);
        host.Settle();

        WindowActivation.GoAway(host.Shell);
        Assert.True(Keyboard.FocusedElement is null, $"premise: away, the keys are still on {Describe(Keyboard.FocusedElement)}");
        button.SetCurrentValue(UIElement.IsEnabledProperty, false);
        host.Settle();
        WindowActivation.ComeBack(host.Shell);
        Assert.True(
            Keyboard.FocusedElement is null or Window,
            $"premise: WPF's own restore put the keys on {Describe(Keyboard.FocusedElement)}");
        host.Settle();

        AssertTheKeysAreOnALiveElementIn(region, "the editor region");
    });

    /// <summary>The same with a sheet open: its focused button went off while the window
    /// was away, and on its return the keys land inside the sheet, never behind it.</summary>
    [Fact]
    public void AButtonOfAnOpenSheetTurnedOffWhileTheWindowIsAwayLandsTheKeysInTheSheet() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "---\nstatus: draft\n---\n# A\n"));
        host.Workspace.OpenPath("a.md");
        host.Settle();
        host.Shell.Activate();
        host.Settle();
        Assert.True(host.Land(ShellRegionKind.Editor), "premise: the editor did not take the keys");
        host.Settle();
        host.Workspace.OpenBulkRenameSheet();
        host.Settle();
        var sheet = host.Workspace.BulkRenameSheet!;
        sheet.OldKey = "status";
        sheet.NewKey = "state";
        host.Settle();
        var overlay = (FrameworkElement)host.Shell.FindName("BulkRenameOverlay");
        Button preview = ShownShell.Descendants(overlay).OfType<Button>()
            .Single(candidate => AutomationProperties.GetAutomationId(candidate) == "BulkRenamePreviewButton");
        TakeTheKeys(preview);
        host.Settle();

        WindowActivation.GoAway(host.Shell);
        Assert.True(Keyboard.FocusedElement is null, $"premise: away, the keys are still on {Describe(Keyboard.FocusedElement)}");
        preview.SetCurrentValue(UIElement.IsEnabledProperty, false);
        host.Settle();
        WindowActivation.ComeBack(host.Shell);
        Assert.True(
            Keyboard.FocusedElement is null or Window,
            $"premise: WPF's own restore put the keys on {Describe(Keyboard.FocusedElement)}");
        host.Settle();

        Assert.NotNull(host.Workspace.BulkRenameSheet);
        AssertTheKeysAreOnALiveElementIn(overlay, "the open sheet");
    });

    /// <summary>A window that comes back to an element that can still hold the keys is
    /// left to WPF: the keys are on that element, and nothing else is landed.</summary>
    [Fact]
    public void AWindowThatComesBackToALiveElementKeepsTheKeysOnIt() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "---\nstatus: draft\n---\n# A\n"));
        host.Workspace.OpenPath("a.md");
        host.Settle();
        host.Shell.Activate();
        host.Settle();
        var region = (FrameworkElement)host.Shell.FindName("ContentPaneBorder");
        Button button = ShownShell.Descendants(region).OfType<Button>()
            .First(candidate => AutomationProperties.GetAutomationId(candidate) == "PropertiesAddButton" && candidate.IsVisible);
        TakeTheKeys(button);
        host.Settle();

        WindowActivation.GoAway(host.Shell);
        host.Settle();
        WindowActivation.ComeBack(host.Shell);
        List<IInputElement> changes = host.RecordFocusChanges();
        host.Settle();

        Assert.Same(button, Keyboard.FocusedElement);
        Assert.Empty(changes);
    });

    /// <summary>Keys that are on a live element when the window's step runs are never
    /// taken: something else, the reader or a route, has placed them since. They are put
    /// on the menu bar here, a focus scope of its own, so the window's own focused element
    /// is still the button and a step that did not look would hand them back to it.</summary>
    [Fact]
    public void KeysPlacedSinceTheWindowCameBackAreNeverTaken() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "---\nstatus: draft\n---\n# A\n"));
        host.Workspace.OpenPath("a.md");
        host.Settle();
        host.Shell.Activate();
        host.Settle();
        var region = (FrameworkElement)host.Shell.FindName("ContentPaneBorder");
        Button button = ShownShell.Descendants(region).OfType<Button>()
            .First(candidate => AutomationProperties.GetAutomationId(candidate) == "PropertiesAddButton" && candidate.IsVisible);
        TakeTheKeys(button);
        host.Settle();

        WindowActivation.GoAway(host.Shell);
        WindowActivation.ComeBack(host.Shell);
        MenuItem placed = host.Shell.MainMenu.Items.OfType<MenuItem>().First();
        KeysFirstRecorder.TakeTheMenuKeys(placed);
        List<IInputElement> changes = host.RecordFocusChanges();
        host.Settle();

        Assert.Same(placed, Keyboard.FocusedElement);
        Assert.Empty(changes);
    });

    /// <summary>A window whose focused element never held the keys is left alone: at
    /// launch the window's first landing is its own route's, and this step neither lands
    /// in the editor ahead of it nor speaks the pane.</summary>
    [Fact]
    public void AWindowWhoseFocusedElementNeverHeldTheKeysIsLeftAlone() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "---\nstatus: draft\n---\n# A\n"));
        host.Workspace.OpenPath("a.md");
        host.Settle();
        var region = (FrameworkElement)host.Shell.FindName("ContentPaneBorder");
        Button button = ShownShell.Descendants(region).OfType<Button>()
            .First(candidate => AutomationProperties.GetAutomationId(candidate) == "PropertiesAddButton" && candidate.IsVisible);
        // The keys on nothing, with nothing for WPF to hand them back to, and the window's
        // focused element one that is off and never held them.
        Keyboard.ClearFocus();
        button.SetCurrentValue(UIElement.IsEnabledProperty, false);
        FocusManager.SetFocusedElement(host.Shell, button);
        host.Settle();
        Assert.Same(button, FocusManager.GetFocusedElement(host.Shell));
        Assert.False(RegionFocusGuard.HeldTheKeys(button), "premise: the button has held the keys");
        Assert.True(Keyboard.FocusedElement is null or Window, $"premise: the keys are on {Describe(Keyboard.FocusedElement)}");
        host.Announced.Clear();

        WindowActivation.ComeBack(host.Shell);
        host.Settle();

        Assert.True(
            Keyboard.FocusedElement is null or Window,
            $"the window landed the keys on {Describe(Keyboard.FocusedElement)} by an element that never held them");
        Assert.DoesNotContain(host.Announced, line => line is uniffi.slate_uniffi.A11yEvent.EditorPaneFocused);
    });

    /// <summary>While a modal loop runs over the window (a box of its own, a picker), the
    /// box owns the keys and nothing is landed beneath it. The activation that follows the
    /// loop's end lands them.</summary>
    [Fact]
    public void NothingIsLandedUnderAModalLoopAndTheNextActivationLands() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "---\nstatus: draft\n---\n# A\n"));
        host.Workspace.OpenPath("a.md");
        host.Settle();
        host.Shell.Activate();
        host.Settle();
        var region = (FrameworkElement)host.Shell.FindName("ContentPaneBorder");
        Button button = ShownShell.Descendants(region).OfType<Button>()
            .First(candidate => AutomationProperties.GetAutomationId(candidate) == "PropertiesAddButton" && candidate.IsVisible);
        TakeTheKeys(button);
        host.Settle();

        WindowActivation.GoAway(host.Shell);
        button.SetCurrentValue(UIElement.IsEnabledProperty, false);
        host.Settle();
        System.Windows.Interop.ComponentDispatcher.PushModal();
        try
        {
            Assert.True(host.Shell.ModalLoops.IsModalLoopActive, "premise: the window does not see the modal loop");
            WindowActivation.ComeBack(host.Shell);
            host.Settle();

            Assert.True(
                Keyboard.FocusedElement is null or Window,
                $"under a modal loop the keys were landed on {Describe(Keyboard.FocusedElement)}");
        }
        finally
        {
            System.Windows.Interop.ComponentDispatcher.PopModal();
        }

        WindowActivation.GoAway(host.Shell);
        WindowActivation.ComeBack(host.Shell);
        host.Settle();

        AssertTheKeysAreOnALiveElementIn(region, "the editor region");
    });

    private static void AssertTheKeysAreOnALiveElementIn(FrameworkElement region, string what)
    {
        IInputElement? keys = Keyboard.FocusedElement;
        Assert.True(
            keys is UIElement { IsVisible: true, IsEnabled: true, Focusable: true } and not Window,
            $"the window came back with the keys on {Describe(keys)}, which holds no keys");
        Assert.True(KeysFirstRecorder.TheKeysAreIn(region), $"the window came back with the keys on {Describe(keys)}, outside {what}");
    }

    private static void TakeTheKeys(UIElement element) => Assert.True(
        element.Focus() && ReferenceEquals(element, Keyboard.FocusedElement),
        $"keyboard premise: {element} could not take the keys (they are on {Describe(Keyboard.FocusedElement)})");

    private static string Describe(object? element) => element switch
    {
        null => "nothing",
        FrameworkElement shown => $"{shown.GetType().Name} {(shown.Name.Length > 0 ? shown.Name : AutomationProperties.GetAutomationId(shown))} \"{AutomationProperties.GetName(shown)}\"",
        _ => element.GetType().Name,
    };

    private static void RunSta(Action body) =>
        StaThread.RunPumped(body, TimeSpan.FromSeconds(60), "STA test body timed out.");
}
