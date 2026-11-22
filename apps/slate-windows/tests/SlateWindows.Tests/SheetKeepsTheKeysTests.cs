// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace SlateWindows.Tests;

/// <summary>
/// #1866 (CD11), a sheet keeps the keys. Every sheet and overlay of the window is a focus
/// scope. WPF's button takes the mouse capture on Space down and on a click, and when it
/// loses the capture it asks for the keys to go back to the main focus scope if it is in
/// another one. So Space, or a click, on any button of a surface that stays open put the
/// keys on the element behind it, before the Click ran: #1866's two Bulk Rename comments.
/// <see cref="RegionFocusGuard"/> now declines that request while a live element inside a
/// surface holds the keys.
/// </summary>
/// <remarks>
/// The theory plants a button that does nothing in each surface, so what it measures is the
/// press and not what a product button does next. It shows each surface by setting its
/// <c>Visibility</c>, the means <c>OverlayElevationTests</c> uses, not by its product route:
/// the rule reads the surface's focus scope, whether it is on screen and its guard landing,
/// none of which the route changes. The Bulk Rename facts below open the real sheet.
/// The mouse capture is a premise of every press here: a desktop that refuses it (#1701)
/// measured nothing, and the fact says so by skipping. None of these facts is a witness in
/// the focus integrity manifest.
/// </remarks>
public sealed class SheetKeepsTheKeysTests
{
    /// <summary>The focus scopes the theory covers, by name.
    /// <see cref="TheTheoryCoversEveryFocusScopeTheWindowHas"/> holds this list to the
    /// window's own.</summary>
    private static readonly string[] Surfaces =
    [
        "QuickSwitcherOverlay",
        "SearchOverlay",
        "CommandPaletteOverlay",
        "AddPropertyOverlay",
        "BulkRenameOverlay",
        "CitationDetailsOverlay",
        "CitationSummaryOverlay",
        "FilesCitingOverlay",
        "DashboardEditorOverlay",
        "BaseQueryBuilderOverlay",
        "TemplatePickerOverlay",
        "TemplateFlowOverlay",
        "MoveToOverlay",
        "CanvasCardEditorOverlay",
        "CanvasCardPickerOverlay",
        "CanvasPromptOverlay",
        "SettingsOverlay",
    ];

    public static TheoryData<string, string> Presses()
    {
        var rows = new TheoryData<string, string>();
        foreach (string surface in Surfaces)
        {
            rows.Add(surface, "Space");
            rows.Add(surface, "click");
        }

        return rows;
    }

    /// <summary>The theory's rows are every focus scope the shown window registers a guard
    /// landing for, found as the window's own registration finds them (every focus scope in
    /// its logical tree but a menu), so a surface added to the window fails here until the
    /// theory has its row.</summary>
    [Fact]
    public void TheTheoryCoversEveryFocusScopeTheWindowHas() => RunSta(() =>
    {
        using var host = new ShownShell();
        FrameworkElement[] scopes = [.. FocusScopes(host.Shell).OfType<FrameworkElement>()];

        Assert.All(scopes, scope => Assert.True(RegionFocusGuard.HasLanding(scope), $"{scope.Name} owns no guard landing"));
        Assert.Equal(Surfaces.Order(StringComparer.Ordinal), scopes.Select(scope => scope.Name).Order(StringComparer.Ordinal));
    });

    /// <summary>Space on a button inside a surface, and the half of a click that moves the
    /// keys (the button taking the mouse capture and letting it go): the keys stay on the
    /// button, and Space runs its Click once. A real click was not driven: an in-process
    /// mouse event carries the mouse's real button state, which is up, so WPF's button
    /// neither captures nor clicks on one.</summary>
    [Theory]
    [MemberData(nameof(Presses))]
    public void APressOnAButtonInsideASurfaceKeepsTheKeysInIt(string surface, string press) => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "# A\n"));
        host.Workspace.OpenPath("a.md");
        host.Settle();
        host.Shell.Activate();
        host.Settle();
        Assert.True(host.Land(ShellRegionKind.Editor), "premise: the editor did not take the keys");
        host.Settle();
        var scope = host.Shell.FindName(surface) as FrameworkElement
            ?? throw new Xunit.Sdk.XunitException($"{surface} is not in the window");
        Assert.True(FocusManager.GetIsFocusScope(scope), $"premise: {surface} is not a focus scope");
        Assert.True(RegionFocusGuard.HasLanding(scope), $"premise: {surface} owns no guard landing");
        scope.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Visible);
        host.Settle();
        Panel panel = ShownShell.Descendants(scope).OfType<Panel>().FirstOrDefault(candidate => candidate.IsVisible && !candidate.IsItemsHost)
            ?? throw new Xunit.Sdk.XunitException($"premise: {surface} shows no panel to plant a button in");
        int clicks = 0;
        var button = new Button { Content = "Does nothing" };
        button.Click += (_, _) => clicks++;
        panel.Children.Add(button);
        host.Settle();
        Assert.True(button.IsVisible && button.IsEnabled, $"premise: the planted button is not on screen and enabled in {surface}");
        Assert.Same(scope, FocusManager.GetFocusScope(button));
        Assert.True(
            button.Focus() && ReferenceEquals(button, Keyboard.FocusedElement),
            $"keyboard premise: the planted button could not take the keys (they are on {Describe(Keyboard.FocusedElement)})");

        if (press == "Space")
        {
            Send(host, Key.Space, Keyboard.PreviewKeyDownEvent);
            TheCaptureIsAPremise(button, "Space down");
            Send(host, Key.Space, Keyboard.PreviewKeyUpEvent);
            Assert.Equal(1, clicks);
        }
        else
        {
            _ = button.CaptureMouse();
            TheCaptureIsAPremise(button, "the button's own CaptureMouse");
            button.ReleaseMouseCapture();
        }

        Assert.True(
            ReferenceEquals(button, Keyboard.FocusedElement),
            $"the press sent the keys out of {surface} to {Describe(Keyboard.FocusedElement)}");
        host.Settle();
        Assert.True(
            ReferenceEquals(button, Keyboard.FocusedElement),
            $"after the press settled the keys were on {Describe(Keyboard.FocusedElement)}, not on the pressed button in {surface}");
    });

    /// <summary>Space on Preview, on the real Bulk Rename sheet: the sheet stays open and
    /// the keys stay inside it (#1866's comment 7062230594, the record's C1).</summary>
    [Fact]
    public void SpaceOnPreviewLeavesTheKeysInsideTheBulkRenameSheet() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "---\nstatus: draft\n---\n# A\n"));
        (FrameworkElement overlay, _) = OpenBulkRename(host);
        var sheet = host.Workspace.BulkRenameSheet!;
        sheet.OldKey = "status";
        sheet.NewKey = "state";
        host.Settle();
        Button preview = ButtonOf(overlay, "BulkRenamePreviewButton");
        int clicks = 0;
        preview.Click += (_, _) => clicks++;
        Assert.True(
            preview.Focus() && ReferenceEquals(preview, Keyboard.FocusedElement),
            $"keyboard premise: Preview could not take the keys (they are on {Describe(Keyboard.FocusedElement)})");

        Send(host, Key.Space, Keyboard.PreviewKeyDownEvent);
        TheCaptureIsAPremise(preview, "Space down");
        Send(host, Key.Space, Keyboard.PreviewKeyUpEvent);

        Assert.Equal(1, clicks);
        Assert.True(
            KeysFirstRecorder.TheKeysAreIn(overlay),
            $"Space on Preview sent the keys behind the open sheet, to {Describe(Keyboard.FocusedElement)}");
        Assert.True(PumpedDispatcher.PumpUntil(() => !sheet.WorkInFlight, TimeSpan.FromSeconds(30)), "the preview never finished");
        host.Settle();
        Assert.NotNull(host.Workspace.BulkRenameSheet);
        Assert.True(
            KeysFirstRecorder.TheKeysAreIn(overlay),
            $"after the preview the keys were behind the open sheet, on {Describe(Keyboard.FocusedElement)}");
    });

    /// <summary>Space on Close, on the real Bulk Rename sheet: the sheet closes, and its
    /// close returns the keys to the element that held them before it opened. A sheet that
    /// is closing does not keep the keys.</summary>
    [Fact]
    public void SpaceOnCloseClosesTheBulkRenameSheetAndTheKeysReturn() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "---\nstatus: draft\n---\n# A\n"));
        (FrameworkElement overlay, IInputElement before) = OpenBulkRename(host);
        Button close = ButtonOf(overlay, "BulkRenameClose");
        Assert.True(
            close.Focus() && ReferenceEquals(close, Keyboard.FocusedElement),
            $"keyboard premise: Close could not take the keys (they are on {Describe(Keyboard.FocusedElement)})");

        Send(host, Key.Space, Keyboard.PreviewKeyDownEvent);
        TheCaptureIsAPremise(close, "Space down");
        Send(host, Key.Space, Keyboard.PreviewKeyUpEvent);
        host.Settle();

        Assert.Null(host.Workspace.BulkRenameSheet);
        Assert.Null(host.Shell.OpenModalSurface);
        Assert.True(
            ReferenceEquals(before, Keyboard.FocusedElement),
            $"the sheet's close left the keys on {Describe(Keyboard.FocusedElement)}, not where they were before it opened ({Describe(before)})");
    });

    /// <summary>The real Bulk Rename sheet over a note, opened from the editor: the sheet,
    /// and the element that held the keys before it opened.</summary>
    private static (FrameworkElement Overlay, IInputElement Before) OpenBulkRename(ShownShell host)
    {
        host.Workspace.OpenPath("a.md");
        host.Settle();
        host.Shell.Activate();
        host.Settle();
        Assert.True(host.Land(ShellRegionKind.Editor), "premise: the editor did not take the keys");
        host.Settle();
        IInputElement before = Keyboard.FocusedElement;
        host.Workspace.OpenBulkRenameSheet();
        host.Settle();
        var overlay = (FrameworkElement)host.Shell.FindName("BulkRenameOverlay");
        Assert.True(overlay.IsKeyboardFocusWithin, $"premise: the sheet did not take the keys (they are on {Describe(Keyboard.FocusedElement)})");
        return (overlay, before);
    }

    private static Button ButtonOf(FrameworkElement overlay, string id) =>
        ShownShell.Descendants(overlay).OfType<Button>().Single(candidate => AutomationProperties.GetAutomationId(candidate) == id);

    /// <summary>A key through WPF's input pipeline, to the element holding the keys.</summary>
    private static void Send(ShownShell host, Key key, RoutedEvent direction) =>
        InputManager.Current.ProcessInput(
            new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(host.Shell)!, Environment.TickCount, key)
            {
                RoutedEvent = direction,
            });

    /// <summary>WPF's button presses only if it took the mouse capture. A desktop that
    /// refuses the capture (#1701) measured nothing about the keys.</summary>
    private static void TheCaptureIsAPremise(Button button, string how)
    {
        if (!button.IsMouseCaptured)
        {
            Assert.Skip($"this desktop refused the button the mouse capture on {how} (#1701), so nothing about the keys staying in the surface was measured on it");
        }
    }

    /// <summary>Every focus scope in the window's logical tree but a menu, as
    /// <c>MainWindow.GuardRegions</c> finds the surfaces it registers.</summary>
    private static IEnumerable<UIElement> FocusScopes(DependencyObject root)
    {
        foreach (object child in LogicalTreeHelper.GetChildren(root))
        {
            if (child is not DependencyObject element)
            {
                continue;
            }

            if (element is UIElement scope and not MenuBase && FocusManager.GetIsFocusScope(scope))
            {
                yield return scope;
            }

            foreach (UIElement nested in FocusScopes(element))
            {
                yield return nested;
            }
        }
    }

    private static string Describe(object? element) => element switch
    {
        null => "nothing",
        FrameworkElement shown => $"{shown.GetType().Name} {(shown.Name.Length > 0 ? shown.Name : AutomationProperties.GetAutomationId(shown))} \"{AutomationProperties.GetName(shown)}\"",
        _ => element.GetType().Name,
    };

    private static void RunSta(Action body) =>
        StaThread.RunPumped(body, TimeSpan.FromSeconds(60), "STA test body timed out.");
}
