// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// W7-7 PR 4b (#1247, contract R-5; the owner's S3): every region root the
// shell's XAML declares (RegionBoundaryCensus's roots — the Files pane, the
// editor, each right-pane leaf, the rail, the status bar, the welcome view,
// every sheet) owns a RegionFocusGuard landing in the constructed window: the
// element holding the keys in it can be disabled, collapsed or rebuilt, and
// the keys must land in the region, not on the window. The menu bar is the
// one root without: its items are in their own popups, and it takes no
// arrows (MenuBarCensus). The runtime roots are found by the same rules the
// XAML census reads, and the two counts must agree, so a root added to the
// XAML without a guard fails here.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Xml.Linq;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "region-guards")]
public sealed class RegionGuardCensus
{
    [Fact]
    public void EveryRegionRootOwnsAGuardLanding()
    {
        XDocument xaml = RegionBoundaryCensus.Shell();
        int declared = RegionBoundaryCensus.RegionRoots(xaml)
            .Count(root => (string?)root.Root.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) != "MainMenu");
        var offenders = new List<string>();
        int found = 0;
        RunSta(() =>
        {
            var shell = new MainWindow();
            try
            {
                UIElement[] roots = [.. RuntimeRoots(shell)];
                found = roots.Length;
                offenders.AddRange(Unguarded(roots));
            }
            finally
            {
                shell.Close();
            }
        });

        Assert.True(found >= 30, $"only {found} region roots were found in the constructed window; the scrape is broken.");
        Assert.Equal(declared, found);
        Assert.True(offenders.Count == 0, "Region roots with no focus-guard landing (S3):\n  " + string.Join("\n  ", offenders));
    }

    /// <summary>The census's own witness: a root with no landing is
    /// reported, one with a landing is not.</summary>
    [Fact]
    public void AnUnguardedRootIsCaught() => RunSta(() =>
    {
        var guarded = new Border { Name = "Guarded" };
        var bare = new Border { Name = "Bare" };
        RegionFocusGuard.SetLanding(guarded, () => true);

        Assert.Equal(["Border Bare"], Unguarded([guarded, bare]));
    });

    private static IEnumerable<string> Unguarded(IEnumerable<UIElement> roots) =>
        roots.Where(root => !RegionFocusGuard.HasLanding(root))
            .Select(root => $"{root.GetType().Name} {(root as FrameworkElement)?.Name}".TrimEnd());

    /// <summary>The roots RegionBoundaryCensus reads in the XAML, found in
    /// the constructed window by the same rules.</summary>
    private static IEnumerable<UIElement> RuntimeRoots(MainWindow shell)
    {
        foreach (string name in new[] { "FilesPaneBorder", "ContentPaneBorder", "RightPaneLeavesList", "WelcomeRoot" })
        {
            yield return (UIElement)(shell.FindName(name) ?? throw new Xunit.Sdk.XunitException($"{name} is gone"));
        }

        var host = (Grid)shell.FindName("RightPaneLeafHost")!;
        object placeholder = shell.FindName("RightPaneDockedPlaceholder")!;
        foreach (UIElement body in host.Children.OfType<UIElement>()
            .Where(child => Grid.GetColumn(child) == 0 && !ReferenceEquals(child, placeholder)))
        {
            yield return body;
        }

        yield return (UIElement)LogicalTreeHelper.GetParent((DependencyObject)shell.FindName("ShellStatusBar")!);
        foreach (UIElement sheet in FocusScopes(shell))
        {
            yield return sheet;
        }
    }

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

    private static void RunSta(Action body) =>
        StaThread.RunPumped(body, TimeSpan.FromSeconds(60), "the region guard census timed out.");
}
