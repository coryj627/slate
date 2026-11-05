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
            Assert.Contains("\"theme\": \"light\"", File.ReadAllText(Path.Combine(device, "preferences.json")), StringComparison.Ordinal);
            AssertFirstPaint(device, logs, expectLight: true, thenChoose: VirtualKeyShort.DOWN);
            Assert.Contains("\"theme\": \"dark\"", File.ReadAllText(Path.Combine(device, "preferences.json")), StringComparison.Ordinal);
            AssertFirstPaint(device, logs, expectLight: false, thenChoose: null);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        }
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
            AssertWindowIsLight(window, expectLight);
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
            using var automation = new UIA3Automation();
            Window window = WaitForMainWindow(process, automation, Path.Combine(logs, "slate-windows.log"), TimeSpan.FromSeconds(30));
            window.SetForeground();
            AssertWindowIsLight(window, expectLight);
            if (thenChoose is VirtualKeyShort key)
            {
                ChooseThemeByKeys(window, key);
                DateTime deadline = DateTime.UtcNow.AddSeconds(10);
                string preferences = Path.Combine(device, "preferences.json");
                while (!File.ReadAllText(preferences).Contains("\"theme\": \"dark\"", StringComparison.Ordinal) && DateTime.UtcNow < deadline)
                {
                    Thread.Sleep(100);
                }
            }
        }
        finally
        {
            try { process?.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            process?.WaitForExit(10_000);
        }
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

    /// <summary>The window's commonest colour is its background, whatever's drawn on it.</summary>
    private static void AssertWindowIsLight(AutomationElement window, bool expectLight)
    {
        int Background()
        {
            using CaptureImage capture = CapturePhysicalPixels(window);
            var counts = new Dictionary<int, int>();
            Bitmap bitmap = capture.Bitmap;
            for (var y = 0; y < bitmap.Height; y += 4)
            {
                for (var x = 0; x < bitmap.Width; x += 4)
                {
                    int rgb = bitmap.GetPixel(x, y).ToArgb() & 0xFFFFFF;
                    counts[rgb] = counts.GetValueOrDefault(rgb) + 1;
                }
            }

            return counts.MaxBy(pair => pair.Value).Key;
        }

        int color = Background();
        double brightness = ((color >> 16 & 255) + (color >> 8 & 255) + (color & 255)) / 3.0;
        Assert.True(
            expectLight ? brightness > 180 : brightness < 80,
            $"the window's background is #{color:X6}, wanting {(expectLight ? "light" : "dark")}.");
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
