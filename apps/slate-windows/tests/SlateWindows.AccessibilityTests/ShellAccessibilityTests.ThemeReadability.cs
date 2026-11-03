// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// #1758: on main, the launched app in the dark theme drew light menu text on
// WPF's fixed light bar, close to unreadable, and every property a hosted test
// could read looked right, because the brushes were each valid and only the
// pair was wrong. So this reads the pixels of the real window, in whichever
// theme Windows has chosen: the surface's most common colour is its background,
// and its strongest ink has to stand off that.

using System.Drawing;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace SlateWindows.AccessibilityTests;

public sealed partial class ShellAccessibilityTests
{
    [Fact]
    public void ThemedShell_TheMenuBarAndTheOpenTabAreReadable() =>
        RunFindInNoteJourney("theme-readability", (process, window, automation, editor) =>
        {
            AutomationElement menu = window.FindFirstDescendant(cf => cf.ByName("Application commands"))
                ?? throw new Xunit.Sdk.XunitException("no menu bar in the window.");
            AutomationElement tabs = WaitForElement(window, "WorkspaceTabs", TimeSpan.FromSeconds(10));
            AutomationElement tab = tabs.FindFirstChild(cf => cf.ByControlType(ControlType.TabItem))
                ?? throw new Xunit.Sdk.XunitException("no open tab in the window.");
            Thread.Sleep(500);

            AssertReadable(menu, "the menu bar");
            AssertReadable(tab, "the open tab");

            AssertAxeClean(process, "theme-readability");
        });

    private static void AssertReadable(AutomationElement surface, string name)
    {
        Color background = MostCommonColor(surface);
        (_, double ink) = Measure(surface, background);
        Assert.True(
            ink >= 4.5,
            $"{name}'s strongest ink measures {ink:F2}:1 on its background {background}. "
            + $"Most common colours {MostCommonColors(surface)}.");
    }

    private static Color MostCommonColor(AutomationElement surface)
    {
        using FlaUI.Core.Capturing.CaptureImage capture = CapturePhysicalPixels(surface);
        Bitmap bitmap = capture.Bitmap;
        var counts = new Dictionary<int, int>();
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                int color = bitmap.GetPixel(x, y).ToArgb();
                counts[color] = counts.GetValueOrDefault(color) + 1;
            }
        }

        return Color.FromArgb(counts.MaxBy(pair => pair.Value).Key);
    }
}
