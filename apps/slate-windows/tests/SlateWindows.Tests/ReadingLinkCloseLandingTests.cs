// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using SlateWindows.Canvas;
using SlateWindows.Reading;

namespace SlateWindows.Tests;

/// <summary>
/// W7-7 PR 4b round 3 (codex r2 F1; R-5 (h), "never … the window"): the
/// reading view's links take the keys by Tab, and a link is a content
/// element, not a visual. Ctrl+W on a pane's LAST tab with the keys on one
/// took the whole tab content out of the tree; WPF's re-evaluation found no
/// focusable ancestor and handed the keys to the MainWindow, and the guard
/// looked only at visuals. The keys now land on the empty editor, in that
/// re-evaluation — one focus change. (A close with a successor tab never
/// needed the guard: the tab control moves the keys itself.)
/// </summary>
public sealed class ReadingLinkCloseLandingTests
{
    [Fact]
    public void ClosingTheLastTabWithTheKeysOnALinkLandsThemOnTheEmptyEditor() => RunSta(() =>
    {
        using var host = new ShownShell(
            ("a.md", "A line with [a link](https://example.com/) and [[b]] in it.\n"),
            ("b.md", "The other note.\n"));
        host.Workspace.OpenPath("a.md");
        host.Settle();
        host.Workspace.ToggleReadingModeCommand.Execute(null);
        host.Settle();
        Hyperlink? link = null;
        Assert.True(
            PumpedDispatcher.PumpUntil(() => (link = LinkShown(host)) is not null, TimeSpan.FromSeconds(30)),
            "premise: the reading view shows no link");
        Assert.True(link!.Focus(), "premise: the link refused the keys");
        Assert.Same(link, Keyboard.FocusedElement);
        List<IInputElement> changes = host.RecordFocusChanges();

        host.Workspace.CloseActiveTabCommand.Execute(null);
        PumpedDispatcher.Drain();

        Assert.Empty(host.Workspace.ActiveGroup.Tabs);
        Assert.IsNotType<MainWindow>(Keyboard.FocusedElement);
        Assert.Same(host.Shell.ContentPaneBorder, Keyboard.FocusedElement);
        Assert.Equal(ShellRegionKind.EmptyEditor, ((IShellRegionHost)host.Shell).FocusedRegion());
        Assert.Single(changes);
    });

    /// <summary>The first link in the shown reading view, once it has
    /// one.</summary>
    private static Hyperlink? LinkShown(ShownShell host) =>
        ShownShell.Descendants(host.Shell).OfType<ReadingSurface>().FirstOrDefault(surface => surface.IsVisible) is { } surface
            ? Links(surface.Document).FirstOrDefault()
            : null;

    private static IEnumerable<Hyperlink> Links(DependencyObject root)
    {
        foreach (object child in LogicalTreeHelper.GetChildren(root))
        {
            if (child is Hyperlink link)
            {
                yield return link;
            }

            if (child is DependencyObject nested)
            {
                foreach (Hyperlink inner in Links(nested))
                {
                    yield return inner;
                }
            }
        }
    }

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
