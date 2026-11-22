// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using SlateWindows.Canvas;

namespace SlateWindows.Tests;

/// <summary>
/// Plan 47 CB-17b (#1866, CD11): the marks list's Clear All Marks. Its command read the open
/// prompt and nothing raised it, so the button was off from the start, on every open (CB-17a
/// review M1). It is now always on: it shows only on the marks list, and its press clears the
/// marks, which closes the list. It is never the list's opening landing: with no row to take
/// the keys, the sheet holds them. The real window, over a canvas with a marked card.
/// </summary>
public sealed class CanvasClearMarksKeysTests
{
    private const string Board = """
        {
          "nodes": [
            {"id":"question","type":"text","text":"# Core question","x":0,"y":0,"width":240,"height":140},
            {"id":"evidence","type":"text","text":"Evidence so far","x":260,"y":0,"width":220,"height":140}
          ],
          "edges": []
        }
        """;

    /// <summary>CB-17a review M1's probe, kept: Clear All Marks is on once Show Marked Cards
    /// opens the list.</summary>
    [Fact]
    public void ClearAllMarksIsOnOnceTheMarksListOpens() => RunSta(() =>
    {
        using var shell = new ShownShell(("board.canvas", Board));
        using var sidebar = new ShownShellSidebar(shell);
        _ = MarkAndShowTheMarks(shell);

        var sheet = Assert.IsType<CanvasMarksListPrompt>(shell.Workspace.CanvasPromptSheet);
        Assert.True(sheet.ShowsClearMarks, "premise: the marks list offers Clear All Marks");
        Button clear = shell.Shell.CanvasPromptClearMarksButton;
        Assert.True(clear.IsVisible, "premise: Clear All Marks shows on the marks list");
        Assert.True(shell.Workspace.CanvasPromptClearMarksCommand.CanExecute(null), "premise: the command can run");
        Assert.True(clear.IsEnabled, "Clear All Marks is off on the open marks list: nothing raised its command's CanExecuteChanged");
    });

    /// <summary>Clear All Marks pressed (by the peer's Invoke: the prompt's Enter submits the
    /// list) with the keys on it: it never goes off, the marks are cleared, the list closes,
    /// and the keys land on the canvas. Its row is exempt as always on
    /// (CanvasPromptClearMarksCommand); this fact holds its press.</summary>
    [Fact]
    public void ClearAllMarksStaysOnAsItsPressClosesTheListAndTheKeysLandOnTheCanvas() => RunSta(() =>
    {
        using var shell = new ShownShell(("board.canvas", Board));
        using var sidebar = new ShownShellSidebar(shell);
        CanvasDocumentViewModel document = MarkAndShowTheMarks(shell);
        Button clear = shell.Shell.CanvasPromptClearMarksButton;
        Assert.True(
            clear.Focus() && ReferenceEquals(clear, Keyboard.FocusedElement),
            $"Clear All Marks could not take the keys (it is {(clear.IsEnabled ? "on" : "off")}; the keys are on {Describe(Keyboard.FocusedElement)})");

        KeysFirstRecorder.AssertItStaysOn("Clear All Marks", clear, () =>
        {
            ((IInvokeProvider)new ButtonAutomationPeer(clear).GetPattern(PatternInterface.Invoke)).Invoke();
            shell.Settle();
            shell.Settle();
        }, () => shell.Workspace.CanvasPromptSheet is null);

        Assert.Empty(document.AppliedPublication!.MarkedIntent);
        CanvasSurfaceView canvas = ShownShell.Descendants(shell.Shell).OfType<CanvasSurfaceView>().Single(view => view.IsVisible);
        Assert.True(
            canvas.IsKeyboardFocusWithin,
            $"after the list closed the keys were on {Describe(Keyboard.FocusedElement)}, not on the canvas");
    });

    /// <summary>The marks list opened while its rows cannot take the keys (a row not landed
    /// yet): the sheet holds them, never Clear All Marks, even when the sheet's own scope last
    /// held them on Clear All Marks; before, the keys stayed behind the open sheet on the
    /// canvas, since the stuck button refused them.</summary>
    [Fact]
    public void TheMarksListNeverOpensWithTheKeysOnClearAllMarks() => RunSta(() =>
    {
        using var shell = new ShownShell(("board.canvas", Board));
        using var sidebar = new ShownShellSidebar(shell);
        _ = MarkAndShowTheMarks(shell);
        Button clear = shell.Shell.CanvasPromptClearMarksButton;
        // The list closed with the keys on Clear All Marks, where they can be, so the sheet's
        // scope remembers it: a landing on the sheet would hand them on to it.
        _ = clear.Focus();
        shell.Workspace.CloseCanvasPrompt();
        shell.Settle();
        Assert.True(shell.Land(ShellRegionKind.Editor), "premise: the canvas did not take the keys again");
        shell.Settle();

        shell.Workspace.CanvasShowMarksCommand.Execute(null);
        // A row that cannot be landed yet: the list refuses the keys in the opening turn.
        shell.Shell.CanvasPromptChoicesList.SetCurrentValue(UIElement.FocusableProperty, false);
        shell.Settle();

        FrameworkElement overlay = shell.Shell.CanvasPromptOverlay;
        Assert.IsType<CanvasMarksListPrompt>(shell.Workspace.CanvasPromptSheet);
        Assert.False(
            ReferenceEquals(clear, Keyboard.FocusedElement),
            "the marks list opened with the keys on Clear All Marks");
        Assert.True(
            overlay.IsKeyboardFocusWithin,
            $"the marks list opened with the keys on {Describe(Keyboard.FocusedElement)}, outside the sheet");

        // Again, once the sheet has let the keys go: it is a stop only while it holds them.
        shell.Workspace.CloseCanvasPrompt();
        shell.Settle();
        Assert.True(shell.Land(ShellRegionKind.Editor), "premise: the canvas did not take the keys a third time");
        shell.Settle();
        shell.Workspace.CanvasShowMarksCommand.Execute(null);
        shell.Shell.CanvasPromptChoicesList.SetCurrentValue(UIElement.FocusableProperty, false);
        shell.Settle();

        Assert.True(
            overlay.IsKeyboardFocusWithin,
            $"the marks list opened a second time with the keys on {Describe(Keyboard.FocusedElement)}, outside the sheet");
    });

    /// <summary>The board open on the canvas, its first card marked, the keys on the canvas,
    /// and the marks list opened by Show Marked Cards.</summary>
    private static CanvasDocumentViewModel MarkAndShowTheMarks(ShownShell shell)
    {
        shell.Workspace.OpenPath("board.canvas");
        CanvasDocumentViewModel document = Assert.IsType<CanvasDocumentViewModel>(
            Assert.IsType<WorkspaceTabViewModel>(shell.Workspace.ActiveGroup.ActiveTab).Canvas);
        Assert.True(PumpedDispatcher.PumpUntil(() => document.State == CanvasLoadState.Ready, TimeSpan.FromSeconds(10)), "premise: the canvas never loaded");
        shell.Settle();
        Assert.True(shell.Land(ShellRegionKind.Editor), "premise: the canvas did not take the keys");
        shell.Settle();
        document.SeatSelectionSilently("question");
        shell.Workspace.CanvasToggleMarkCommand.Execute(null);
        shell.Settle();
        Assert.Contains("question", document.AppliedPublication!.MarkedIntent);

        shell.Workspace.CanvasShowMarksCommand.Execute(null);
        shell.Settle();
        Assert.IsType<CanvasMarksListPrompt>(shell.Workspace.CanvasPromptSheet);
        return document;
    }

    private static string Describe(object? element) => element switch
    {
        null => "nothing",
        FrameworkElement shown => $"{shown.GetType().Name} {(shown.Name.Length > 0 ? shown.Name : AutomationProperties.GetAutomationId(shown))} \"{AutomationProperties.GetName(shown)}\"",
        _ => element.GetType().Name,
    };

    private static void RunSta(Action body)
    {
        Func<bool> priorOverlayProbe = CanvasSurfaceView.ShellOverlayIsOpen;
        try
        {
            StaThread.RunPumped(body, TimeSpan.FromSeconds(120), "STA test body timed out.");
        }
        finally
        {
            CanvasSurfaceView.ShellOverlayIsOpen = priorOverlayProbe;
        }
    }
}
