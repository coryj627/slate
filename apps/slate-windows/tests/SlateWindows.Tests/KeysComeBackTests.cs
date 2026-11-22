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
