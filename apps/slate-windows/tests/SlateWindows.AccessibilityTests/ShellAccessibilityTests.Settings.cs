// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;
using System.Drawing;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;

namespace SlateWindows.AccessibilityTests;

/// <summary>DS-10 (#1811): the Settings sheet in the launched app, and a theme choice that
/// outlives the process.</summary>
public sealed partial class ShellAccessibilityTests
{
    /// <summary>What a launch's first capture shows is the first paint's theme, so the
    /// preference has to be read before it. Both directions, so the fact tells whichever
    /// way this machine's Windows is set.</summary>
    [Fact]
    public void ThemePreference_ChoosingLightStaysLightAcrossARestart()
    {
        string root = Path.Combine(Path.GetTempPath(), "slate-theme-" + Guid.NewGuid().ToString("N"));
        string device = Path.Combine(root, "device");
        string logs = Path.Combine(root, "logs");
        Directory.CreateDirectory(device);
        try
        {
            Choose(root, device, logs, "Light", VirtualKeyShort.HOME, expectLight: true);
            Assert.Contains("\"theme\": \"light\"", GateFile.ReadText(Path.Combine(device, "preferences.json")), StringComparison.Ordinal);
            AssertFirstPaint(device, logs, expectLight: true, thenChoose: VirtualKeyShort.DOWN);
            Assert.Contains("\"theme\": \"dark\"", GateFile.ReadText(Path.Combine(device, "preferences.json")), StringComparison.Ordinal);
            AssertFirstPaint(device, logs, expectLight: false, thenChoose: null);
        }
        catch (Exception)
        {
            PreserveEvidence(nameof(ThemePreference_ChoosingLightStaysLightAcrossARestart), logs);
            throw;
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        }
    }

    /// <summary>DS-11 (#1812): the manager's half, which no hosted fact can build. Windows accent
    /// is a choice in the app, so this needs no system setting changed. The sheet's open category
    /// shows the selected surface and the pill, and gold's are exact, so their pixels say which
    /// accent drew. It takes both: a Windows accent can blend to gold's surface exactly
    /// (#EEAC05 at 12% over white is #FDF5E1), and its pill is a different step of Windows'
    /// palette. This reads Windows' accent as it is (DSD16).</summary>
    [Fact]
    public void AccentColor_WindowsAccentDrawsAtOnceAndAcrossARestart()
    {
        const int GoldSelectedSurface = 0xFDF5E1;
        const int GoldPill = 0xA57A00;
        string root = Path.Combine(Path.GetTempPath(), "slate-accent-" + Guid.NewGuid().ToString("N"));
        string device = Path.Combine(root, "device");
        string logs = Path.Combine(root, "logs");
        string preferences = Path.Combine(device, "preferences.json");
        Directory.CreateDirectory(device);
        File.WriteAllText(preferences, "{\"theme\":\"light\",\"accentColor\":\"gold\"}");
        Process? process = null;

        Window? OpenSheet(UIA3Automation automation)
        {
            process = LaunchWith(device, logs);
            if (!HasInteractiveDesktop(process, "accent-preference")) { return null; }
            Window window = WaitForMainWindow(process, automation, Path.Combine(logs, "slate-windows.log"), TimeSpan.FromSeconds(30));
            window.SetForeground();
            ReassertForegroundForAChord(window);
            PressChord(VirtualKeyShort.CONTROL, VirtualKeyShort.OEM_COMMA);
            AssertEventuallyFocused(
                WaitForElement(window, "SettingsCategories", TimeSpan.FromSeconds(10)).FindFirstChild()!,
                "the sheet didn't land on its open category.");
            return window;
        }

        static bool GoldShows(Window window) => HasPixel(window, GoldSelectedSurface) && HasPixel(window, GoldPill);

        static void WaitForGold(Window window, bool expected)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(10);
            while (GoldShows(window) != expected && DateTime.UtcNow < deadline)
            {
                Thread.Sleep(100);
            }

            Assert.Equal(expected, GoldShows(window));
        }

        try
        {
            using var automation = new UIA3Automation();
            if (OpenSheet(automation) is not Window window) { return; }
            WaitForGold(window, expected: true);

            PressKey(VirtualKeyShort.TAB);
            PressKey(VirtualKeyShort.TAB);
            AutomationElement combo = WaitForElement(window, "SettingsAccent", TimeSpan.FromSeconds(5));
            AssertEventuallyFocused(combo, "Tab didn't reach the accent.");
            Assert.Equal("Slate gold", ComboValue(combo));
            PressKey(VirtualKeyShort.END);
            Retry.WhileFalse(() => ComboValue(combo) == "Windows accent", TimeSpan.FromSeconds(5));
            Assert.Equal("Windows accent", ComboValue(combo));
            WaitForGold(window, expected: false);
            AssertEventuallyFocused(combo, "the accent's change took focus off the combo.");
            AssertAxeClean(process!, "settings-sheet-accent");
            Assert.Contains("\"accentColor\": \"windows\"", GateFile.ReadText(preferences), StringComparison.Ordinal);
            process!.Kill(entireProcessTree: true);
            process.WaitForExit(10_000);

            window = OpenSheet(automation)!;
            WaitForGold(window, expected: false);

            PressKey(VirtualKeyShort.TAB);
            PressKey(VirtualKeyShort.TAB);
            combo = WaitForElement(window, "SettingsAccent", TimeSpan.FromSeconds(5));
            AssertEventuallyFocused(combo, "Tab didn't reach the accent.");
            PressKey(VirtualKeyShort.UP);
            Retry.WhileFalse(() => ComboValue(combo) == "Slate gold", TimeSpan.FromSeconds(5));
            WaitForGold(window, expected: true);
        }
        catch (Exception)
        {
            PreserveEvidence(nameof(AccentColor_WindowsAccentDrawsAtOnceAndAcrossARestart), logs);
            throw;
        }
        finally
        {
            try { process?.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            process?.WaitForExit(10_000);
            try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        }
    }

    private static bool HasPixel(AutomationElement window, int rgb)
    {
        using CaptureImage capture = CapturePhysicalPixels(window);
        Bitmap bitmap = capture.Bitmap;
        for (var y = 0; y < bitmap.Height; y += 2)
        {
            for (var x = 0; x < bitmap.Width; x += 2)
            {
                if ((bitmap.GetPixel(x, y).ToArgb() & 0xFFFFFF) == rgb)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void Choose(string root, string device, string logs, string label, VirtualKeyShort key, bool expectLight)
    {
        Process? process = null;
        try
        {
            process = LaunchWith(device, logs);
            if (!HasInteractiveDesktop(process, "theme-preference")) { return; }
            using var automation = new UIA3Automation();
            Window window = WaitForMainWindow(process, automation, Path.Combine(logs, "slate-windows.log"), TimeSpan.FromSeconds(30));
            window.SetForeground();
            ChooseThemeByKeys(window, key);
            AutomationElement combo = WaitForElement(window, "SettingsTheme", TimeSpan.FromSeconds(5));
            DateTime deadline = DateTime.UtcNow.AddSeconds(10);
            while (ComboValue(combo) != label && DateTime.UtcNow < deadline)
            {
                Thread.Sleep(100);
            }

            Assert.Equal(label, ComboValue(combo));
            AssertEventuallyFocused(combo, "the theme change took focus off the combo.");
            AssertFirstFrameIsLight(window.Properties.NativeWindowHandle.Value, expectLight);
            AssertAxeClean(process, "settings-sheet-light");
        }
        finally
        {
            try { process?.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            process?.WaitForExit(10_000);
        }
    }

    private static void AssertFirstPaint(string device, string logs, bool expectLight, VirtualKeyShort? thenChoose)
    {
        Process? process = null;
        try
        {
            process = LaunchWith(device, logs);
            if (!HasInteractiveDesktop(process, "theme-preference")) { return; }
            // The first frame is read from the moment the window is visible, before UI
            // Automation finds it, which can be seconds later: a shorter flash would be gone.
            IntPtr handle = WaitForVisibleMainWindowHandle(process);
            AssertFirstFrameIsLight(handle, expectLight);
            using var automation = new UIA3Automation();
            Window window = WaitForMainWindow(process, automation, Path.Combine(logs, "slate-windows.log"), TimeSpan.FromSeconds(30));
            window.SetForeground();
            if (thenChoose is VirtualKeyShort key)
            {
                ChooseThemeByKeys(window, key);
                // The preference is saved once, and a read open at that instant would
                // refuse the save and lose it (GateFile). So the file is not opened until
                // the app shows what follows the save: the combo reads the choice and the
                // window is drawn in it (DevicePreference.Choose saves, then applies).
                AutomationElement combo = WaitForElement(window, "SettingsTheme", TimeSpan.FromSeconds(5));
                // A UI Automation fault on the combo's read is "not yet", and the
                // window's handle is the one the launch already gave: no one-shot
                // read stands between the key and the file.
                if (!EventuallyByUia(() => ComboValue(combo) == "Dark", TimeSpan.FromSeconds(10)))
                {
                    string shown;
                    try
                    {
                        shown = ComboValue(combo) ?? "nothing";
                    }
                    catch (Exception exception) when (IsTransientUiaFault(exception))
                    {
                        shown = "<unreadable>";
                    }

                    Assert.Fail($"the key did not choose Dark; the theme reads '{shown}'.");
                }

                WaitForAppliedTheme(handle, light: false);
                _ = ExpectFileText(
                    Path.Combine(device, "preferences.json"),
                    text => text.Contains("\"theme\": \"dark\"", StringComparison.Ordinal),
                    "the Dark choice never reached preferences.json.",
                    nameof(ThemePreference_ChoosingLightStaysLightAcrossARestart),
                    logs,
                    TimeSpan.FromSeconds(10));
            }
        }
        finally
        {
            try { process?.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            process?.WaitForExit(10_000);
        }
    }

    /// <summary>Waits until the window's own pixels are the chosen theme's. The app applies
    /// a choice after it has saved it, so this is what a journey waits for before it reads
    /// the preferences file. Pure black is the window before its first render, not a
    /// theme, and a capture the window refused is no frame at all: the wait goes on
    /// past both and fails at its own bound.</summary>
    private static void WaitForAppliedTheme(IntPtr handle, bool light)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(10);
        int color = 0;
        int refused = 0;
        while (true)
        {
            try
            {
                (int[] pixels, Size size) = OwnPixels(handle);
                color = CommonestColor(pixels, size);
                double brightness = ((color >> 16 & 255) + (color >> 8 & 255) + (color & 255)) / 3.0;
                if (color != 0x000000 && (light ? brightness > 180 : brightness < 80))
                {
                    return;
                }
            }
            catch (Xunit.Sdk.XunitException)
            {
                refused++;
            }

            if (DateTime.UtcNow >= deadline)
            {
                break;
            }

            Thread.Sleep(GateFile.PollInterval);
        }

        Assert.Fail(
            $"the window never drew the {(light ? "light" : "dark")} theme; its commonest colour is #{color:X6}, "
            + $"and {refused} captures were refused.");
    }

    /// <summary>What a reader hears as a closed combo box's value: a combo that can't be
    /// typed in has no Value pattern.</summary>
    private static string? ComboValue(AutomationElement combo) =>
        combo.Patterns.Selection.Pattern.Selection.Value.FirstOrDefault()?.Name;

    private static Process LaunchWith(string device, string logs)
    {
        var start = new ProcessStartInfo(SlateWindowsExe()) { UseShellExecute = false };
        ShellLaunch.ApplyDeviceState(start);
        start.Environment[ShellLaunch.Variable] = device;
        start.Environment["SLATE_CENSUS_INSTANCE_ID"] = "theme-" + Guid.NewGuid().ToString("N");
        start.Environment["SLATE_LOG_DIR"] = logs;
        return Process.Start(start) ?? throw new Xunit.Sdk.XunitException("Slate did not start.");
    }

    /// <summary>The sheet by its chord, then one key on the closed combo, which applies.</summary>
    private static void ChooseThemeByKeys(Window window, VirtualKeyShort key)
    {
        ReassertForegroundForAChord(window);
        PressChord(VirtualKeyShort.CONTROL, VirtualKeyShort.OEM_COMMA);
        AssertEventuallyFocused(
            WaitForElement(window, "SettingsCategories", TimeSpan.FromSeconds(10)).FindFirstChild(),
            "the sheet didn't land on its open category.");
        PressKey(VirtualKeyShort.TAB);
        AssertEventuallyFocused(WaitForElement(window, "SettingsTheme", TimeSpan.FromSeconds(5)), "Tab didn't reach the theme.");
        PressKey(key);
        Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(500));
    }

    /// <summary>The theme of this app's first rendered frame, read from the window's own pixels
    /// (#1943). The fact used to capture the screen under the window, which shows whatever lies
    /// on top: on job VMs it read greys no Slate theme paints and failed the dark step, and a
    /// light window over ours passed both light steps. <see cref="OwnPixels"/> asks Windows for
    /// the window's own content, so nothing on top can stand in for it. Until the first render
    /// that content is pure black, which no Slate theme paints (the dark window is #121212), so
    /// the fact samples every 50 ms, skips black, and judges the first frame that isn't
    /// (<see cref="FirstRenderedFrame"/>), whatever its colour: a launch that first paints the
    /// other theme, or any grey or white, and corrects itself a frame later still fails, which
    /// is DS-10's first-paint promise. A window that never renders within 10 seconds fails too,
    /// saying so.</summary>
    private static void AssertFirstFrameIsLight(IntPtr handle, bool expectLight)
    {
        var seen = new List<string>();
        int[]? lastPixels = null;
        Size lastSize = Size.Empty;
        DateTime deadline = DateTime.UtcNow.AddSeconds(10);

        IEnumerable<int> Samples()
        {
            while (true)
            {
                (lastPixels, lastSize) = OwnPixels(handle);
                int color = CommonestColor(lastPixels, lastSize);
                seen.Add($"#{color:X6}");
                yield return color;
                if (DateTime.UtcNow >= deadline)
                {
                    yield break;
                }

                Thread.Sleep(50);
            }
        }

        int? first = FirstRenderedFrame(Samples());
        string samples = $"Samples 50 ms apart: {RunsOf(seen)}.";
        if (first is not int color)
        {
            Assert.Fail(
                $"the window never rendered a frame within 10 seconds, wanting {(expectLight ? "light" : "dark")}. "
                + samples + SaveThemeEvidence(lastPixels, lastSize));
            return;
        }

        double brightness = ((color >> 16 & 255) + (color >> 8 & 255) + (color & 255)) / 3.0;
        if (!(expectLight ? brightness > 180 : brightness < 80))
        {
            Assert.Fail(
                $"the window's first frame is #{color:X6}, wanting {(expectLight ? "light" : "dark")}. "
                + samples + SaveThemeEvidence(lastPixels, lastSize));
        }
    }

    /// <summary>The launch's main window as soon as Windows shows it, polled every 5 ms.</summary>
    private static IntPtr WaitForVisibleMainWindowHandle(Process process)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            Assert.False(process.HasExited, "Slate exited before showing its window.");
            process.Refresh();
            IntPtr handle = process.MainWindowHandle;
            if (handle != IntPtr.Zero && NativePrint.IsWindowVisible(handle))
            {
                return handle;
            }

            Thread.Sleep(5);
        }

        throw new Xunit.Sdk.XunitException("Slate showed no window within 30 seconds.");
    }

    /// <summary>#1943: the first sample that isn't pure black, the window's own content before
    /// its first render; null when every sample is.</summary>
    internal static int? FirstRenderedFrame(IEnumerable<int> commonestColors)
    {
        foreach (int color in commonestColors)
        {
            if (color != 0x000000)
            {
                return color;
            }
        }

        return null;
    }

    /// <summary>Codex on #1943: a launch's first rendered frame is judged though the frames
    /// after it are right, whatever its colour.</summary>
    [Fact]
    public void FirstRenderedFrame_IsTheFirstThatIsNotBlack()
    {
        // A grey, a white or the other theme first, then the right theme: the first is judged.
        Assert.Equal(0xC3BEC4, FirstRenderedFrame([0x000000, 0xC3BEC4, 0x121212, 0x121212]));
        Assert.Equal(0xFFFFFF, FirstRenderedFrame([0x000000, 0x000000, 0xFFFFFF, 0x121212]));
        Assert.Equal(0xF7F7F7, FirstRenderedFrame([0xF7F7F7, 0x121212]));
        // Slate's dark window background isn't black.
        Assert.Equal(0x121212, FirstRenderedFrame([0x000000, 0x121212]));
        // Never rendered.
        Assert.Null(FirstRenderedFrame([0x000000, 0x000000]));
    }

    /// <summary>The commonest colour of a frame, every fourth pixel each way.</summary>
    private static int CommonestColor(int[] pixels, Size size)
    {
        var counts = new Dictionary<int, int>();
        for (var y = 0; y < size.Height; y += 4)
        {
            for (var x = 0; x < size.Width; x += 4)
            {
                int rgb = pixels[(y * size.Width) + x] & 0xFFFFFF;
                counts[rgb] = counts.GetValueOrDefault(rgb) + 1;
            }
        }

        return counts.Count == 0 ? 0 : counts.MaxBy(pair => pair.Value).Key;
    }

    /// <summary>The window's own content in physical pixels, as Windows renders it for
    /// <c>PrintWindow</c> with <c>PW_RENDERFULLCONTENT</c>: what the app drew, not what the
    /// screen shows there.</summary>
    private static (int[] Pixels, Size Size) OwnPixels(IntPtr window)
    {
        IntPtr previous = SetThreadDpiAwarenessContext(DpiAwarenessContextPerMonitorAwareV2);
        IntPtr dc = IntPtr.Zero;
        IntPtr dib = IntPtr.Zero;
        IntPtr old = IntPtr.Zero;
        try
        {
            Assert.True(NativePrint.GetWindowRect(window, out NativePrint.Rect rect), "the window has no rectangle.");
            var size = new Size(rect.Right - rect.Left, rect.Bottom - rect.Top);
            Assert.True(size.Width > 0 && size.Height > 0, $"the window's rectangle is {size}.");
            var header = new NativePrint.BitmapInfoHeader
            {
                Size = 40,
                Width = size.Width,
                Height = -size.Height,
                Planes = 1,
                BitCount = 32,
            };
            dc = NativePrint.CreateCompatibleDC(IntPtr.Zero);
            dib = NativePrint.CreateDIBSection(dc, ref header, 0, out IntPtr bits, IntPtr.Zero, 0);
            Assert.True(dib != IntPtr.Zero, "no bitmap for the window's own pixels.");
            old = NativePrint.SelectObject(dc, dib);
            Assert.True(NativePrint.PrintWindow(window, dc, NativePrint.RenderFullContent), "PrintWindow failed.");
            var pixels = new int[size.Width * size.Height];
            System.Runtime.InteropServices.Marshal.Copy(bits, pixels, 0, pixels.Length);
            return (pixels, size);
        }
        finally
        {
            if (old != IntPtr.Zero)
            {
                _ = NativePrint.SelectObject(dc, old);
            }

            if (dib != IntPtr.Zero)
            {
                _ = NativePrint.DeleteObject(dib);
            }

            if (dc != IntPtr.Zero)
            {
                _ = NativePrint.DeleteDC(dc);
            }

            SetThreadDpiAwarenessContext(previous);
        }
    }

    /// <summary>On a failure, the last frame of the window's own pixels goes to the folder the
    /// gate uploads. Best effort: a save error never replaces the failure.</summary>
    private static string SaveThemeEvidence(int[]? pixels, Size size)
    {
        string? directory = Environment.GetEnvironmentVariable("SLATE_ACCESSIBILITY_EVIDENCE_DIR");
        if (pixels is null || string.IsNullOrWhiteSpace(directory))
        {
            return string.Empty;
        }

        try
        {
            string destination = Path.Combine(directory, "theme-first-paint");
            Directory.CreateDirectory(destination);
            string file = DateTime.UtcNow.ToString("HHmmssfff", System.Globalization.CultureInfo.InvariantCulture) + "-window.png";
            using var bitmap = new Bitmap(size.Width, size.Height, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
            System.Drawing.Imaging.BitmapData data = bitmap.LockBits(
                new Rectangle(Point.Empty, size), System.Drawing.Imaging.ImageLockMode.WriteOnly, bitmap.PixelFormat);
            try
            {
                System.Runtime.InteropServices.Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
            }
            finally
            {
                bitmap.UnlockBits(data);
            }

            bitmap.Save(Path.Combine(destination, file), System.Drawing.Imaging.ImageFormat.Png);
            return $" The last frame is theme-first-paint\\{file}.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Runtime.InteropServices.ExternalException)
        {
            return string.Empty;
        }
    }

    /// <summary>Repeated samples as one entry with a count, so a long wait stays readable.</summary>
    private static string RunsOf(List<string> samples)
    {
        var runs = new List<string>();
        for (var start = 0; start < samples.Count;)
        {
            int end = start;
            while (end + 1 < samples.Count && samples[end + 1] == samples[start])
            {
                end++;
            }

            int count = end - start + 1;
            runs.Add(count == 1 ? samples[start] : $"{samples[start]} x{count}");
            start = end + 1;
        }

        return string.Join(", ", runs);
    }

    private static class NativePrint
    {
        internal const uint RenderFullContent = 2;

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        internal struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        internal struct BitmapInfoHeader
        {
            public int Size;
            public int Width;
            public int Height;
            public short Planes;
            public short BitCount;
            public int Compression;
            public int SizeImage;
            public int XPelsPerMeter;
            public int YPelsPerMeter;
            public int ClrUsed;
            public int ClrImportant;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        internal static extern bool GetWindowRect(IntPtr window, out Rect rect);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        internal static extern bool IsWindowVisible(IntPtr window);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        internal static extern bool PrintWindow(IntPtr window, IntPtr dc, uint flags);

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        internal static extern IntPtr CreateCompatibleDC(IntPtr dc);

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        internal static extern IntPtr CreateDIBSection(
            IntPtr dc, ref BitmapInfoHeader header, uint usage, out IntPtr bits, IntPtr section, uint offset);

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        internal static extern IntPtr SelectObject(IntPtr dc, IntPtr gdiObject);

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        internal static extern bool DeleteObject(IntPtr gdiObject);

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        internal static extern bool DeleteDC(IntPtr dc);
    }

    [Fact]
    public void SettingsSheet_NamesFocusAndAxe_AreClean()
    {
        string root = Path.Combine(Path.GetTempPath(), "slate-settings-" + Guid.NewGuid().ToString("N"));
        string vault = Path.Combine(root, "Vault");
        string logs = Path.Combine(root, "logs");
        Directory.CreateDirectory(vault);
        File.WriteAllText(Path.Combine(vault, "alpha.md"), "# alpha\n\nBody.\n");
        Process? process = null;
        try
        {
            var start = new ProcessStartInfo(SlateWindowsExe()) { UseShellExecute = false };
            ShellLaunch.ApplyDeviceState(start);
            start.Environment[ShellLaunch.Variable] = Path.Combine(root, "device");
            start.ArgumentList.Add(vault);
            start.Environment["SLATE_CENSUS_INSTANCE_ID"] = "settings-" + Guid.NewGuid().ToString("N");
            start.Environment["SLATE_LOG_DIR"] = logs;
            process = Process.Start(start) ?? throw new Xunit.Sdk.XunitException("Slate did not start.");
            if (!HasInteractiveDesktop(process, "settings-sheet")) { return; }
            using var automation = new UIA3Automation();
            Window window = WaitForMainWindow(process, automation, Path.Combine(logs, "slate-windows.log"), TimeSpan.FromSeconds(30));
            window.SetForeground();
            AutomationElement tree = WaitForElement(window, "FilesTree", TimeSpan.FromSeconds(30));
            AutomationElement row = WaitForTreeItemStartingWith(tree, automation, "alpha.md");
            ReassertForegroundForAChord(window);
            row.Focus();
            AssertEventuallyFocused(row, "the tree's row took no keys.");

            PressChord(VirtualKeyShort.CONTROL, VirtualKeyShort.OEM_COMMA);
            AutomationElement sheet = WaitForElement(window, "SettingsSheet", TimeSpan.FromSeconds(10));
            Assert.Equal("Settings", sheet.Name);
            AutomationElement category = WaitForElement(window, "SettingsCategories", TimeSpan.FromSeconds(5)).FindFirstChild();
            Assert.Equal("Appearance", category.Name);
            AssertEventuallyFocused(category, "the sheet didn't land on its open category.");
            AssertAxeClean(process, "settings-sheet");

            PressKey(VirtualKeyShort.TAB);
            AutomationElement combo = WaitForElement(window, "SettingsTheme", TimeSpan.FromSeconds(5));
            AssertEventuallyFocused(combo, "Tab didn't reach the theme.");
            Assert.Equal("Theme", combo.Name);
            Assert.Equal("Light, Dark, or what Windows uses.", combo.HelpText);
            Assert.Equal("Use Windows setting", ComboValue(combo));

            // The real chord, which reaches WPF as Key.System: the sheet's route lets Alt by.
            PressChord(VirtualKeyShort.ALT, VirtualKeyShort.DOWN);
            Retry.WhileFalse(
                () => combo.Patterns.ExpandCollapse.Pattern.ExpandCollapseState.Value == ExpandCollapseState.Expanded,
                TimeSpan.FromSeconds(5));
            Assert.Equal(ExpandCollapseState.Expanded, combo.Patterns.ExpandCollapse.Pattern.ExpandCollapseState.Value);
            PressKey(VirtualKeyShort.ESCAPE);
            Retry.WhileFalse(
                () => combo.Patterns.ExpandCollapse.Pattern.ExpandCollapseState.Value == ExpandCollapseState.Collapsed,
                TimeSpan.FromSeconds(5));
            Assert.Equal(ExpandCollapseState.Collapsed, combo.Patterns.ExpandCollapse.Pattern.ExpandCollapseState.Value);
            Assert.NotNull(window.FindFirstDescendant(condition => condition.ByAutomationId("SettingsSheet")));
            AssertEventuallyFocused(combo, "closing the list took the keys from the combo.");

            PressKey(VirtualKeyShort.TAB);
            AutomationElement accent = WaitForElement(window, "SettingsAccent", TimeSpan.FromSeconds(5));
            AssertEventuallyFocused(accent, "Tab didn't reach the accent.");
            Assert.Equal("Accent color", accent.Name);
            Assert.Equal("Automatic uses Slate gold unless you’ve chosen an accent color in Windows.", accent.HelpText);
            Assert.Equal("Automatic", ComboValue(accent));

            PressKey(VirtualKeyShort.TAB);
            AutomationElement close = WaitForElement(window, "SettingsClose", TimeSpan.FromSeconds(5));
            AssertEventuallyFocused(close, "Tab didn't reach Close.");
            Assert.Equal("Close settings", close.Name);

            PressKey(VirtualKeyShort.TAB);
            AssertEventuallyFocused(category, "Tab left the sheet.");

            PressKey(VirtualKeyShort.ESCAPE);
            AssertEventuallyFocused(row, "Esc didn't give focus back to the row.");
            Assert.Null(window.FindFirstDescendant(condition => condition.ByAutomationId("SettingsSheet")));

            // Switch Theme…, in the palette: the three themes, the current one marked.
            ReassertForegroundForAChord(window);
            PressChord(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_P);
            AutomationElement search = WaitForElement(window, "CommandPaletteSearch", TimeSpan.FromSeconds(10));
            AssertEventuallyFocused(search, "the palette's field took no keys.");
            Keyboard.Type("Switch Theme");
            AutomationElement results = WaitForElement(window, "CommandPaletteResults", TimeSpan.FromSeconds(5));
            Retry.WhileFalse(
                () => Array.Exists(
                    results.FindAllDescendants(condition => condition.ByControlType(ControlType.ListItem)),
                    item => item.Name.StartsWith("Switch Theme", StringComparison.Ordinal) && item.Patterns.SelectionItem.Pattern.IsSelected.Value),
                TimeSpan.FromSeconds(5));
            PressKey(VirtualKeyShort.ENTER);
            Retry.WhileFalse(() => search.Name == "Select a theme", TimeSpan.FromSeconds(5));
            Assert.Equal("Select a theme", search.Name);
            AutomationElement[] Choices() => results.FindAllDescendants(condition => condition.ByControlType(ControlType.ListItem));
            Retry.WhileFalse(() => Choices().Length == 3, TimeSpan.FromSeconds(5));
            Assert.Equal(new[] { "Light", "Dark", "Use Windows setting" }, Choices().Select(item => item.Name));
            Assert.Equal(
                new[] { string.Empty, string.Empty, "Current theme" },
                Choices().Select(item => item.Properties.ItemStatus.ValueOrDefault ?? string.Empty));
            AssertEventuallyFocused(search, "the list of themes took the keys from the field.");
            AssertAxeClean(process, "switch-theme-palette");

            PressKey(VirtualKeyShort.ESCAPE);
            Retry.WhileFalse(() => search.Name == "Search commands", TimeSpan.FromSeconds(5));
            Assert.Equal("Search commands", search.Name);

            PressKey(VirtualKeyShort.ESCAPE);
            AssertEventuallyFocused(row, "Esc didn't give focus back to the row.");
        }
        finally
        {
            try { process?.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        }
    }
}
