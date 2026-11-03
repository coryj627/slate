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
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using Microsoft.Win32;

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

    /// <summary>#1763: the editor's selection in the launched app. It's the solid
    /// pair, drawn under the text, so the words stay readable on it.</summary>
    [Fact]
    public void ThemedShell_TheEditorSelectionStandsOutAndKeepsItsText() =>
        RunFindInNoteJourney("editor-selection", (process, window, automation, editor) =>
        {
            editor.Focus();
            Color highlight = Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme",
                1) is 0
                ? Color.FromArgb(0x6B, 0x4D, 0x00)
                : Color.FromArgb(0xF9, 0xE7, 0xB8);
            Thread.Sleep(300);
            int before = Measure(editor, highlight).Highlight;
            Assert.True(before < 50, $"premise: {before} pixels are the highlight with nothing selected.");

            using (Keyboard.Pressing(VirtualKeyShort.CONTROL))
            {
                Keyboard.Type(VirtualKeyShort.KEY_A);
            }

            Thread.Sleep(400);
            (int selected, double ink) = Measure(editor, highlight);
            Assert.True(
                selected > 500,
                $"only {selected} pixels are the highlight {highlight}. Most common colours {MostCommonColors(editor)}.");
            Assert.True(ink >= 4.5, $"the selected text's strongest ink measures {ink:F2}:1 on the highlight.");

            AssertAxeClean(process, "editor-selection");
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
