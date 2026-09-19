// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;
using System.Text.Json;
using System.Xml.Linq;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;

namespace SlateWindows.AccessibilityTests;

public sealed partial class ShellAccessibilityTests
{
    [Fact]
    public void SpokenChords_MenusPaletteAndOverlays_MatchTheTable()
    {
        string root = Path.Combine(Path.GetTempPath(), "slate-spoken-chords-" + Guid.NewGuid().ToString("N"));
        string vault = Path.Combine(root, "Vault");
        string logs = Path.Combine(root, "logs");
        Directory.CreateDirectory(vault);
        File.WriteAllText(Path.Combine(vault, "alpha.md"), "# Alpha\n\n[Example](https://example.org)\n\n- Item\n");
        using JsonDocument table = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "chords.json")));
        var rows = table.RootElement.GetProperty("commands").EnumerateArray()
            .Concat(table.RootElement.GetProperty("chordSurface").EnumerateArray())
            .ToDictionary(row => row.GetProperty("id").GetString()!, row => row);
        XDocument xaml = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "fixtures", "MainWindow.xaml"));
        XDocument templates = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "fixtures", "WorkspaceTemplates.xaml"));
        string Speech(string id) => rows[id].GetProperty("windowsSpoken").GetString()
            ?? throw new Xunit.Sdk.XunitException("No spoken chord for " + id);
        string LiteralHelp(string id) => xaml.Descendants().Single(element =>
            (string?)element.Attribute("AutomationProperties.AutomationId") == id)
            .Attribute("AutomationProperties.HelpText")!.Value;

        Process? process = null;
        try
        {
            var start = new ProcessStartInfo(SlateWindowsExe()) { UseShellExecute = false };
            start.ArgumentList.Add(vault);
            start.Environment["SLATE_CENSUS_INSTANCE_ID"] = "spoken-chords-" + Guid.NewGuid().ToString("N");
            start.Environment["SLATE_LOG_DIR"] = logs;
            process = Process.Start(start) ?? throw new Xunit.Sdk.XunitException("Slate did not start.");
            if (!HasInteractiveDesktop(process, "spoken-chords")) { return; }
            using var automation = new UIA3Automation();
            Window window = WaitForMainWindow(process, automation, Path.Combine(logs, "slate-windows.log"), TimeSpan.FromSeconds(30));
            window.SetForeground();
            WaitForElement(window, "RightPaneLeaves", TimeSpan.FromSeconds(30));
            WaitForElement(window, "FilesTree", TimeSpan.FromSeconds(10)).Focus();
            PressChord(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_O);
            AutomationElement quick = WaitForElement(window, "QuickSwitcherSearch", TimeSpan.FromSeconds(10));
            Assert.Equal("Up and Down move selection. Enter opens. "
                + $"{Speech("windows.quickOpen.openNewTab")} opens a new tab. "
                + $"{Speech("windows.quickOpen.openSplitRight")} opens a right split. "
                + $"{Speech("windows.quickOpen.openSplitDown")} opens a down split. "
                + "Escape closes Quick Open.", quick.Properties.HelpText.Value);
            quick.Patterns.Value.Pattern.SetValue("alpha");
            Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(250));
            AssertAxeClean(process, "spoken-chords-quick-open");
            PressKey(VirtualKeyShort.RETURN);
            AssertElementDisappears(window, automation, "QuickSwitcherSearch");
            WaitForElement(window, "MarkdownEditor", TimeSpan.FromSeconds(10));

            // Every declared accelerator is checked against ITS row, including
            // disabled commands. Opening each top-level menu realizes its
            // children without invoking any potentially destructive action.
            XElement[] menuItems = xaml.Descendants().Where(element => element.Name.LocalName == "MenuItem"
                && element.Attribute("InputGestureText") is not null).ToArray();
            Assert.NotEmpty(menuItems);
            int checkedMenus = 0;
            foreach (IGrouping<string, XElement> group in menuItems.GroupBy(element =>
                element.Ancestors().First(ancestor => ancestor.Name.LocalName == "MenuItem").Attribute("Header")!.Value.Replace("_", "")))
            {
                AutomationElement mainMenu = WaitForElement(window, "MainMenu", TimeSpan.FromSeconds(10));
                AutomationElement? menu = mainMenu.FindFirstChild(automation.ConditionFactory.ByName(group.Key));
                Assert.NotNull(menu);
                menu.Patterns.ExpandCollapse.Pattern.Expand();
                foreach (XElement declared in group)
                {
                    string name = declared.Attribute("Header")!.Value.Replace("_", "");
                    string id = ChordTextId(declared);
                    // The popup realizes its peers on its own schedule: poll,
                    // as every other Expand() in the journeys does.
                    AutomationElement? item = FindDescendantWithin(menu,
                        automation.ConditionFactory.ByControlType(ControlType.MenuItem).And(automation.ConditionFactory.ByName(name)),
                        TimeSpan.FromSeconds(10));
                    Assert.True(item is not null, "Menu item did not appear: " + group.Key + " > " + name);
                    Assert.Equal(rows[id].GetProperty("windows").GetString(), item.Properties.AcceleratorKey.Value);
                    checkedMenus++;
                }
                menu.Patterns.ExpandCollapse.Pattern.Collapse();
            }
            Assert.Equal(menuItems.Length, checkedMenus);
            PressKey(VirtualKeyShort.ESCAPE);

            // The editor context menu's accelerators come from the templates
            // fixture, as the main menu's come from MainWindow.xaml: a third
            // chorded item is audited live the day it is declared.
            (string AutomationId, string CommandId)[] contextItems = [.. templates.Descendants()
                .Where(element => element.Name.LocalName == "MenuItem" && element.Attribute("InputGestureText") is not null)
                .Select(element => ((string)element.Attribute("AutomationProperties.AutomationId")!, ChordTextId(element)))];
            Assert.NotEmpty(contextItems);
            Assert.All(contextItems, pair => Assert.False(string.IsNullOrEmpty(pair.AutomationId)));
            window.SetForeground();
            WaitForElement(window, "MarkdownEditor", TimeSpan.FromSeconds(10)).Focus();
            PressChord(VirtualKeyShort.SHIFT, VirtualKeyShort.F10);
            AutomationElement desktop = automation.GetDesktop();
            foreach ((string automationId, string commandId) in contextItems)
            {
                AutomationElement? item = FindDescendantWithin(desktop, automation.ConditionFactory.ByAutomationId(automationId), TimeSpan.FromSeconds(10));
                Assert.True(item is not null, "Editor context-menu item did not appear: " + automationId);
                Assert.Equal(rows[commandId].GetProperty("windows").GetString(), item.Properties.AcceleratorKey.Value);
            }
            PressKey(VirtualKeyShort.ESCAPE);
            WaitForElement(window, "MarkdownEditor", TimeSpan.FromSeconds(10)).Focus();
            PressChord(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_P);
            AutomationElement palette = WaitForElement(window, "CommandPaletteSearch", TimeSpan.FromSeconds(10));
            Assert.Equal(LiteralHelp("CommandPaletteSearch"), palette.Properties.HelpText.Value);
            AutomationElement results = WaitForElement(window, "CommandPaletteResults", TimeSpan.FromSeconds(10));
            JsonElement[] registered = rows.Values.Where(row => row.GetProperty("registered").GetBoolean()).ToArray();
            Assert.NotEmpty(registered);
            // Filtering realizes every row independently; offscreen virtual
            // item peers must not let this check silently cover only page one.
            foreach (JsonElement row in registered)
            {
                string label = row.GetProperty("label").GetString()!;
                string? spoken = row.GetProperty("windowsSpoken").GetString();
                string expected = spoken is null ? label : label + ", " + spoken;
                palette.Patterns.Value.Pattern.SetValue(label);
                // One provider-side FindFirst per poll, not every row's Name
                // read cross-process on every spin, for each of the rows.
                AutomationElement? found = FindDescendantWithin(results,
                    automation.ConditionFactory.ByControlType(ControlType.ListItem).And(automation.ConditionFactory.ByName(expected)),
                    TimeSpan.FromSeconds(5));
                Assert.True(found is not null, "No palette row with its composed Name: " + expected);
            }
            AssertAxeClean(process, "spoken-chords-palette");
            PressKey(VirtualKeyShort.ESCAPE);
            AssertElementDisappears(window, automation, "CommandPaletteSearch");

            window.SetForeground();
            PressChord(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_F);
            AutomationElement search = WaitForElement(window, "SearchOverlaySearch", TimeSpan.FromSeconds(10));
            Assert.Equal(LiteralHelp("SearchOverlaySearch"), search.Properties.HelpText.Value);
            search.Patterns.Value.Pattern.SetValue("Alpha");
            AssertAxeClean(process, "spoken-chords-search");
            PressKey(VirtualKeyShort.ESCAPE);
            AssertElementDisappears(window, automation, "SearchOverlaySearch");

            window.SetForeground();
            PressChord(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_E);
            AutomationElement reading = WaitForElement(window, "ReadingSurface", TimeSpan.FromSeconds(10));
            foreach (string id in new[] { "nextH", "previousH", "nextK", "nextL", "nextT" })
            {
                Assert.Contains(Speech("windows.reading." + id), reading.Properties.HelpText.Value);
            }
            AssertAxeClean(process, "spoken-chords-reading");
        }
        finally
        {
            if (process is not null)
            {
                if (!process.HasExited)
                {
                    process.CloseMainWindow();
                    if (!process.WaitForExit(5_000)) { process.Kill(entireProcessTree: true); }
                }
                process.Dispose();
            }
            try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        }
    }

    /// <summary>The command id a declared <c>{cmd:ChordText id}</c> gesture names.</summary>
    private static string ChordTextId(XElement declared)
    {
        string expression = declared.Attribute("InputGestureText")!.Value;
        Assert.StartsWith("{cmd:ChordText ", expression);
        return expression[15..^1];
    }

    /// <summary>A descendant UIA finds for the condition, polled with an
    /// interval: each attempt is one cross-process FindFirst evaluated on
    /// the provider side, and a transient fault — a popup still realizing
    /// its peers, a list row the filter is re-realizing (CI's gate failed
    /// this journey on two branches on 2026-09-19 with
    /// PropertyNotSupportedException('Name')) — answers "not yet" and the
    /// wait retries instead of the fault escaping it.</summary>
    private static AutomationElement? FindDescendantWithin(AutomationElement scope, ConditionBase condition, TimeSpan timeout)
    {
        AutomationElement? found = null;
        SpinWait.SpinUntil(() =>
        {
            try { found = scope.FindFirstDescendant(condition); }
            catch (Exception exception) when (IsTransientUiaFault(exception)) { found = null; }
            if (found is null) { Thread.Sleep(25); }
            return found is not null;
        }, timeout);
        return found;
    }
}
