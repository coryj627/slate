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
using System.Drawing.Imaging;
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
            WaitForSettledPixels(reading, "the reading surface");
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
            WaitForSettledPixels(reading, "the reading surface's selection");

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

    /// <summary>Waits until <paramref name="surface"/> has settled
    /// (<see cref="SettledPixels"/>: at rest, or blinking between two frames
    /// as a caret makes it): what a journey does between the state that
    /// starts a repaint and its one measurement. The frames are the
    /// surface's part of its own window's pixels (<see cref="OwnPixels"/>),
    /// so nothing outside the surface, and no other window lying on top of
    /// it, decides whether it has settled. A caret inside the surface is in
    /// its frames: the palette's field, the editor, the reading surface and
    /// the search overlay's field each draw one, which is why a blink counts
    /// as settled. With <paramref name="onScreen"/> the frames are the
    /// screen under the surface instead: for a surface in a popup, whose
    /// window the main window's pixels do not hold, and for one that waits
    /// on a popup going away from over it. A capture that cannot be read, or
    /// that is all black, counts for nothing; a surface that can never be
    /// read fails at the wait's own bound. Each wait leaves a line in the
    /// evidence folder's <c>settled-pixels.log</c> with its captures and the
    /// time they took.</summary>
    private static void WaitForSettledPixels(AutomationElement surface, string what, bool onScreen = false)
    {
        IntPtr window = IntPtr.Zero;
        SettledPixels.Outcome outcome = SettledPixels.Wait(() =>
        {
            if (onScreen)
            {
                return ScreenPixelsOf(surface);
            }

            if (window == IntPtr.Zero)
            {
                window = OwnWindowOf(surface);
            }

            return window == IntPtr.Zero ? null : OwnPixelsOf(window, surface);
        });
        NoteSettle(what, onScreen ? "the screen" : "own pixels", outcome);
        Assert.True(outcome.Settled, $"{what} never settled: {outcome.Why}");
    }

    /// <summary>Waits until a popup's <paramref name="surface"/> is on the
    /// screen: the screen in its rectangle differs from the main window's
    /// own pixels there (<see cref="SettledPixels.WaitUntilCovered"/>). UI
    /// Automation hands out a popup's row before the popup has painted and
    /// while it is still moving into place, and the screen in the row's
    /// rectangle is then the main window, at rest. A journey waits here
    /// first, then for a settled frame, then measures.</summary>
    private static void WaitForPopupOnScreen(AutomationElement surface, string what)
    {
        IntPtr window = IntPtr.Zero;
        SettledPixels.Outcome outcome = SettledPixels.WaitUntilCovered(() =>
        {
            if (window == IntPtr.Zero)
            {
                window = OwnWindowOf(surface);
            }

            // The part of the row that lies over the main window: only there can the
            // screen be set against that window's own pixels. A row with no such part
            // cannot be witnessed, and the wait says so at its bound.
            if (window == IntPtr.Zero
                || PhysicalBoundsOf(surface) is not { } bounds
                || PartOver(window, bounds) is not { } part)
            {
                return (null, null);
            }

            return (ScreenPixelsOf(part), OwnPixelsOf(window, part, whole: true));
        });
        NoteSettle(what + " (the popup's paint)", "the screen against own pixels", outcome);
        Assert.True(outcome.Settled, $"{what} never showed on the screen: {outcome.Why}");
    }

    /// <summary>A line for the run's evidence: how a wait on pixels ended,
    /// in how many captures and how long. Best effort.</summary>
    private static void NoteSettle(string what, string frames, SettledPixels.Outcome outcome)
    {
        string? directory = Environment.GetEnvironmentVariable("SLATE_ACCESSIBILITY_EVIDENCE_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(directory);
            File.AppendAllText(
                Path.Combine(directory, "settled-pixels.log"),
                $"{what}: {(outcome.Settled ? outcome.How : "NOT SETTLED")} on {frames}, {outcome.Captures} "
                + $"{(outcome.Captures == 1 ? "capture" : "captures")} in {outcome.Took.TotalMilliseconds:0} ms\n");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>The window <paramref name="element"/> belongs to: the
    /// nearest element at or above it that has a window handle, or zero when
    /// UI Automation faulted on the way up. For an element in a WPF popup (a
    /// menu, a combo box's list) that is still the main window: UI
    /// Automation hangs a popup's elements under the element that opened it
    /// and gives the popup's own window no element, so a popup's surface is
    /// watched on the screen.</summary>
    private static IntPtr OwnWindowOf(AutomationElement element)
    {
        try
        {
            for (AutomationElement? at = element; at is not null; at = at.Parent)
            {
                IntPtr handle = at.Properties.NativeWindowHandle.ValueOrDefault;
                if (handle != IntPtr.Zero)
                {
                    return handle;
                }
            }
        }
        catch (Exception exception) when (IsTransientUiaFault(exception))
        {
        }

        return IntPtr.Zero;
    }

    /// <summary><paramref name="surface"/>'s rectangle in physical pixels,
    /// or null when UI Automation faulted or it has none.</summary>
    private static Rectangle? PhysicalBoundsOf(AutomationElement surface)
    {
        IntPtr previous = SetThreadDpiAwarenessContext(DpiAwarenessContextPerMonitorAwareV2);
        try
        {
            Rectangle bounds = surface.BoundingRectangle;
            return bounds.Width > 0 && bounds.Height > 0 ? bounds : null;
        }
        catch (Exception exception) when (IsTransientUiaFault(exception))
        {
            return null;
        }
        finally
        {
            SetThreadDpiAwarenessContext(previous);
        }
    }

    /// <summary>The part of <paramref name="bounds"/> (a rectangle on the
    /// screen) that lies over <paramref name="window"/>'s rectangle, or null
    /// when none does or the window has no rectangle.</summary>
    private static Rectangle? PartOver(IntPtr window, Rectangle bounds)
    {
        IntPtr previous = SetThreadDpiAwarenessContext(DpiAwarenessContextPerMonitorAwareV2);
        try
        {
            if (!NativePrint.GetWindowRect(window, out NativePrint.Rect frame))
            {
                return null;
            }

            Rectangle part = Rectangle.Intersect(bounds, Rectangle.FromLTRB(frame.Left, frame.Top, frame.Right, frame.Bottom));
            return part.Width > 0 && part.Height > 0 ? part : null;
        }
        finally
        {
            SetThreadDpiAwarenessContext(previous);
        }
    }

    /// <summary><paramref name="surface"/>'s part of <paramref name="window"/>'s
    /// own pixels, or null when the surface cannot be read, lies outside the
    /// window, or the window has drawn nothing there yet (pure black, which
    /// no Slate theme paints).</summary>
    private static int[]? OwnPixelsOf(IntPtr window, AutomationElement surface) =>
        PhysicalBoundsOf(surface) is { } bounds ? OwnPixelsOf(window, bounds, whole: false) : null;

    /// <summary>The part of <paramref name="window"/>'s own pixels that
    /// <paramref name="bounds"/> (a rectangle on the screen) covers. With
    /// <paramref name="whole"/> the answer is null unless the whole
    /// rectangle lies inside the window, so it can be set against a screen
    /// capture of the same rectangle pixel for pixel; without it the part
    /// inside the window is answered. Null too when the window cannot be
    /// read (it refused the print, or has no rectangle) or has drawn nothing
    /// there yet.</summary>
    private static int[]? OwnPixelsOf(IntPtr window, Rectangle bounds, bool whole)
    {
        int[] pixels;
        Size size;
        NativePrint.Rect frame;
        IntPtr previous = SetThreadDpiAwarenessContext(DpiAwarenessContextPerMonitorAwareV2);
        try
        {
            if (!NativePrint.GetWindowRect(window, out frame))
            {
                return null;
            }

            (pixels, size) = OwnPixels(window);
        }
        catch (Xunit.Sdk.XunitException)
        {
            // OwnPixels asserts on a window that refused the print: inside a
            // wait that is a capture that could not be read, not the end of
            // the journey.
            return null;
        }
        finally
        {
            SetThreadDpiAwarenessContext(previous);
        }

        var asked = new Rectangle(bounds.X - frame.Left, bounds.Y - frame.Top, bounds.Width, bounds.Height);
        Rectangle part = Rectangle.Intersect(asked, new Rectangle(Point.Empty, size));
        if (part.Width <= 0 || part.Height <= 0 || (whole && part != asked))
        {
            return null;
        }

        var cropped = new int[part.Width * part.Height];
        for (var y = 0; y < part.Height; y++)
        {
            Array.Copy(pixels, ((part.Y + y) * size.Width) + part.X, cropped, y * part.Width, part.Width);
        }

        return Array.Exists(cropped, pixel => (pixel & 0xFFFFFF) != 0) ? cropped : null;
    }

    /// <summary>The screen under <paramref name="surface"/>, one value a
    /// pixel, or null when the surface cannot be read or the capture is all
    /// black.</summary>
    private static int[]? ScreenPixelsOf(AutomationElement surface) =>
        PhysicalBoundsOf(surface) is { } bounds ? ScreenPixelsOf(bounds) : null;

    /// <summary>The screen in <paramref name="bounds"/>, one value a pixel,
    /// or null when it cannot be captured or the capture is all black (a
    /// locked or sleeping desktop, which is no frame of anything).</summary>
    private static int[]? ScreenPixelsOf(Rectangle bounds)
    {
        IntPtr previous = SetThreadDpiAwarenessContext(DpiAwarenessContextPerMonitorAwareV2);
        try
        {
            using CaptureImage capture = Capture.Rectangle(bounds);
            Bitmap bitmap = capture.Bitmap;
            var pixels = new int[bitmap.Width * bitmap.Height];
            BitmapData data = bitmap.LockBits(
                new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                System.Runtime.InteropServices.Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
            }
            finally
            {
                bitmap.UnlockBits(data);
            }

            return Array.Exists(pixels, pixel => (pixel & 0xFFFFFF) != 0) ? pixels : null;
        }
        catch (Exception exception) when (
            IsTransientUiaFault(exception)
            || exception is System.ComponentModel.Win32Exception or System.Runtime.InteropServices.ExternalException or ArgumentException)
        {
            return null;
        }
        finally
        {
            SetThreadDpiAwarenessContext(previous);
        }
    }

    /// <summary>A wait on a UI Automation read: a transient fault is "not
    /// yet", and a read that keeps faulting fails at the wait's own bound
    /// with the caller's message.</summary>
    private static bool EventuallyByUia(Func<bool> read, TimeSpan timeout) =>
        SpinWait.SpinUntil(
            () =>
            {
                try
                {
                    return read();
                }
                catch (Exception exception) when (IsTransientUiaFault(exception))
                {
                    return false;
                }
            },
            timeout);

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
