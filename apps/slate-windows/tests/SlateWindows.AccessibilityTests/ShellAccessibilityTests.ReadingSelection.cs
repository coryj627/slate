// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// #1755: a RichTextBox draws its selection as an overlay on top of the text,
// whatever the text box switch says. Too faint and the reader can't see what
// is selected; opaque and it paints over the words. Only pixels can tell those
// apart, so this reads them from the real reading view in the real app. The
// selection needs the keyboard focus, which is why the fact lives on the
// interactive lane.

using System.Drawing;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using Microsoft.Win32;

namespace SlateWindows.AccessibilityTests;

public sealed partial class ShellAccessibilityTests
{
    /// <summary>The overlay composited over the reading surface, in the
    /// appearance Windows has chosen: #A57A00 at 20% over white, #E2AD24 at 24%
    /// over #1E1E1E (<c>Slate.ReadingSelectionColor</c>).</summary>
    private static Color ReadingHighlight() =>
        Registry.GetValue(
            @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
            "AppsUseLightTheme",
            1) is 0
            ? Color.FromArgb(0x4D, 0x40, 0x1F)
            : Color.FromArgb(0xED, 0xE4, 0xCC);

    [Fact]
    public void ReadingView_ASelectionStandsOutAndKeepsItsText() =>
        RunFindInNoteJourney("reading-selection", (process, window, automation, editor) =>
        {
            editor.Focus();
            using (Keyboard.Pressing(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT))
            {
                Keyboard.Type(VirtualKeyShort.KEY_E);
            }

            AutomationElement reading = WaitForElement(window, "ReadingSurface", TimeSpan.FromSeconds(10));
            Assert.True(
                SpinWait.SpinUntil(
                    () => automation.FocusedElement()?.AutomationId == "ReadingSurface",
                    TimeSpan.FromSeconds(10)),
                "the switch to reading mode did not land on the reading surface");
            Color highlight = ReadingHighlight();
            Thread.Sleep(300);
            int before = Measure(reading, highlight).Highlight;
            Assert.True(before < 50, $"premise: {before} pixels are the highlight with nothing selected.");

            using (Keyboard.Pressing(VirtualKeyShort.CONTROL))
            {
                Keyboard.Type(VirtualKeyShort.KEY_A);
            }

            Assert.True(
                SpinWait.SpinUntil(
                    () => SelectionText(reading).Length > 0,
                    TimeSpan.FromSeconds(5)),
                "Ctrl+A selected nothing in the reading view");
            Thread.Sleep(300);

            (int selected, double inkContrast) = Measure(reading, highlight);
            Assert.True(
                selected > 500,
                $"only {selected} pixels are the highlight {highlight}: the selection isn't drawn in the overlay. "
                + $"Selected '{SelectionText(reading).Length}' characters; focus on '{automation.FocusedElement()?.AutomationId}'; "
                + $"most common colours {MostCommonColors(reading)}.");
            Assert.True(
                inkContrast >= 4.5,
                $"the selected text's strongest ink measures {inkContrast:F2}:1 on the highlight: the overlay hides it.");

            AssertAxeClean(process, "reading-selection");
        });

    /// <summary>How many of the surface's pixels are the highlight, within the
    /// renderer's rounding, and the contrast of the pixel furthest from it,
    /// which is the core of the selected text's strokes.</summary>
    private static (int Highlight, double InkContrast) Measure(AutomationElement surface, Color highlight)
    {
        using CaptureImage capture = CapturePhysicalPixels(surface);
        Bitmap bitmap = capture.Bitmap;
        var count = 0;
        var strongest = 1.0;
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                Color pixel = bitmap.GetPixel(x, y);
                if (Math.Abs(pixel.R - highlight.R) <= 2
                    && Math.Abs(pixel.G - highlight.G) <= 2
                    && Math.Abs(pixel.B - highlight.B) <= 2)
                {
                    count++;
                }

                strongest = Math.Max(strongest, Contrast(pixel, highlight));
            }
        }

        return (count, strongest);
    }

    /// <summary>The test host isn't DPI aware, so on a scaled display the
    /// element's bounds and the screen copy disagree and the capture lands on
    /// the wrong part of the window. Awareness on this thread alone leaves the
    /// rest of the process as it was.</summary>
    private static CaptureImage CapturePhysicalPixels(AutomationElement surface)
    {
        IntPtr previous = SetThreadDpiAwarenessContext(DpiAwarenessContextPerMonitorAwareV2);
        try
        {
            return Capture.Element(surface);
        }
        finally
        {
            SetThreadDpiAwarenessContext(previous);
        }
    }

    private static readonly IntPtr DpiAwarenessContextPerMonitorAwareV2 = new(-4);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);

    private static string MostCommonColors(AutomationElement surface)
    {
        using CaptureImage capture = CapturePhysicalPixels(surface);
        Bitmap bitmap = capture.Bitmap;
        var counts = new Dictionary<int, int>();
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                int color = bitmap.GetPixel(x, y).ToArgb() & 0xFFFFFF;
                counts[color] = counts.GetValueOrDefault(color) + 1;
            }
        }

        return string.Join(", ", counts.OrderByDescending(pair => pair.Value).Take(4).Select(pair => $"#{pair.Key:X6}={pair.Value}"));
    }

    private static double Contrast(Color first, Color second)
    {
        static double Channel(byte value)
        {
            double c = value / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        static double Luminance(Color c) =>
            (0.2126 * Channel(c.R)) + (0.7152 * Channel(c.G)) + (0.0722 * Channel(c.B));

        double a = Luminance(first);
        double b = Luminance(second);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }
}
