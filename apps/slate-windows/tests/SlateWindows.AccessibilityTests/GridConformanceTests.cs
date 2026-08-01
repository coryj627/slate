// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;
using System.Runtime.ExceptionServices;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;

namespace SlateWindows.AccessibilityTests;

/// <summary>
/// W4-1 FlaUI conformance suite — the reusable §W-C gate every
/// substrate-consuming surface inherits (w4_spec §W4-1 item 3). Runs
/// the 05 §8.7 matrix against the GridConformanceHost fixture and
/// probes the UIA-virtualization trap at 10,000 rows (the WPF
/// Recycling-mode crash class the substrate pins out, dotnet/wpf
/// #8528/#5428/#11519).
/// </summary>
public sealed class GridConformanceTests
{
    /// <summary>The §8.7 matrix over the 200-row fixture: identity,
    /// Grid/Table patterns, headers, cell labels, keyboard sort,
    /// type-ahead, row actions with disabled reasons, and the
    /// separately-focusable summary region.</summary>
    [Fact]
    public void MatrixConformanceHolds()
    {
        RunHost(200, (automation, window, process) =>
        {
            AutomationElement grid = WaitForElement(
                window, "AccessibleDataGrid", TimeSpan.FromSeconds(10));
            Assert.True(grid.Patterns.Grid.IsSupported, "Grid pattern missing");
            Assert.True(grid.Patterns.Table.IsSupported, "Table pattern missing");

            // Headers announced on entry ride the native Table pattern:
            // three addressable column headers.
            var headers = grid.Patterns.Table.Pattern.ColumnHeaders.Value;
            Assert.Equal(
                new[] { "Name", "Status", "Notes" },
                headers.Select(header => header.Name).ToArray());

            // Cell labels carry the "Header: value" contract.
            var firstCell = grid.Patterns.Grid.Pattern.GetItem(0, 0);
            Assert.Equal("Name: Note 00000", firstCell.Name);
            Assert.Equal(
                "Status: Done",
                grid.Patterns.Grid.Pattern.GetItem(1, 1).Name);

            // Keyboard sort: focus a cell, Ctrl+Alt+S twice = the
            // second toggle flips to DESCENDING, reordering row 0.
            firstCell.AsGridCell().Click();
            Wait.UntilInputIsProcessed();
            PressChord(VirtualKeyShort.KEY_S);
            Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(250));
            PressChord(VirtualKeyShort.KEY_S);
            Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(250));
            Assert.True(
                SpinWait.SpinUntil(
                    () => grid.Patterns.Grid.Pattern.GetItem(0, 0).Name
                        == "Name: Note 00199",
                    TimeSpan.FromSeconds(5)),
                "descending sort did not reorder row 0");

            // Type-ahead: prefix matching on the FIRST column.
            Keyboard.Type("note 00042");
            Assert.True(
                SpinWait.SpinUntil(
                    () => (grid.Patterns.Selection.Pattern.Selection.Value
                            ?? Array.Empty<AutomationElement>())
                        .Any(item => item.Name.Contains("Note 00042", StringComparison.Ordinal)),
                    TimeSpan.FromSeconds(5)),
                "type-ahead did not select the prefixed row");

            // Row actions: the Menu key opens the actions menu; the
            // disabled action stays listed WITH its reason.
            Keyboard.Press(VirtualKeyShort.APPS);
            AutomationElement open = WaitForNamedDescendant(
                automation, "Open", TimeSpan.FromSeconds(5));
            AutomationElement edit = WaitForNamedDescendant(
                automation, "Edit property", TimeSpan.FromSeconds(5));
            Assert.False(edit.Properties.IsEnabled.ValueOrDefault);
            Assert.Equal(
                "Read-only fixture",
                edit.Properties.HelpText.ValueOrDefault);
            open.AsMenuItem().Invoke();
            AutomationElement log = WaitForElement(
                window, "GridActionLog", TimeSpan.FromSeconds(5));
            Assert.True(
                SpinWait.SpinUntil(
                    () => (log.Properties.Name.ValueOrDefault ?? "") == "Action log"
                        && (log.AsLabel().Text ?? "").StartsWith("opened:", StringComparison.Ordinal),
                    TimeSpan.FromSeconds(5)),
                "the Open row action did not execute");

            // The summary region: separately addressable and named.
            AutomationElement summary = WaitForElement(
                window, "AccessibleDataGridSummary", TimeSpan.FromSeconds(5));
            Assert.Equal(
                "Summary: 200 rows, 3 columns.",
                summary.Properties.Name.ValueOrDefault);

            ShellAccessibilityTests.AssertAxeClean(process, "grid-conformance");
        });
    }

    /// <summary>The virtualization trap probe: 10,000 rows, realize the
    /// LAST item through the Grid pattern (the ItemContainerPattern
    /// path UIA clients take), jump Ctrl+End, and require the process
    /// to survive — the Recycling-mode NRE class dies here.</summary>
    [Fact]
    public void VirtualizationTrapProbeSurvivesTenThousandRows()
    {
        RunHost(10_000, (automation, window, process) =>
        {
            AutomationElement grid = WaitForElement(
                window, "AccessibleDataGrid", TimeSpan.FromSeconds(20));

            var last = grid.Patterns.Grid.Pattern.GetItem(9_999, 0);
            Assert.Equal("Name: Note 09999", last.Name);

            var middle = grid.Patterns.Grid.Pattern.GetItem(5_000, 2);
            Assert.Equal("Notes: fixture row 5000", middle.Name);

            grid.Patterns.Grid.Pattern.GetItem(0, 0).AsGridCell().Click();
            Wait.UntilInputIsProcessed();
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.END);
            Assert.True(
                SpinWait.SpinUntil(
                    () => (grid.Patterns.Selection.Pattern.Selection.Value
                            ?? Array.Empty<AutomationElement>())
                        .Any(item => item.Name.Contains("09999", StringComparison.Ordinal)),
                    TimeSpan.FromSeconds(10)),
                "Ctrl+End did not reach the last row");

            Assert.False(process.HasExited, "the host died under UIA load");
        });
    }

    private static void RunHost(
        int rowCount,
        Action<UIA3Automation, Window, Process> body)
    {
        Process? process = null;
        try
        {
            var startInfo = new ProcessStartInfo(HostExe())
            {
                UseShellExecute = false,
            };
            startInfo.ArgumentList.Add(
                rowCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
            process = Process.Start(startInfo)
                ?? throw new Xunit.Sdk.XunitException("GridConformanceHost did not start.");

            if (!Environment.UserInteractive)
            {
                // Session-0 fallback (the shell-gate shape): the UIA
                // half needs a desktop; the host must still survive
                // startup.
                Assert.False(
                    process.WaitForExit(3_000),
                    "GridConformanceHost exited during startup smoke.");
                return;
            }

            using var automation = new UIA3Automation();
            Window? window = null;
            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        window = automation.GetDesktop()
                            .FindFirstChild(cf => cf.ByProcessId(process.Id))?.AsWindow();
                        return window is not null;
                    },
                    TimeSpan.FromSeconds(30)),
                "the host window never appeared");
            body(automation, window!, process);
        }
        finally
        {
            if (process is not null && !process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
    }

    private static string HostExe()
    {
        string exe = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "tools", "GridConformanceHost", "bin",
#if DEBUG
            "Debug",
#else
            "Release",
#endif
            "net10.0-windows", "GridConformanceHost.exe");
        exe = Path.GetFullPath(exe);
        Assert.True(File.Exists(exe), $"GridConformanceHost.exe not built at {exe}.");
        return exe;
    }

    private static AutomationElement WaitForElement(
        Window window, string automationId, TimeSpan timeout)
    {
        AutomationElement? found = null;
        Assert.True(
            SpinWait.SpinUntil(
                () =>
                {
                    found = window.FindFirstDescendant(cf => cf.ByAutomationId(automationId));
                    return found is not null;
                },
                timeout),
            $"no element with AutomationId {automationId} appeared");
        return found!;
    }

    private static AutomationElement WaitForNamedDescendant(
        UIA3Automation automation, string name, TimeSpan timeout)
    {
        AutomationElement? found = null;
        Assert.True(
            SpinWait.SpinUntil(
                () =>
                {
                    found = automation.GetDesktop()
                        .FindFirstDescendant(cf => cf.ByName(name));
                    return found is not null;
                },
                timeout),
            $"no element named {name} appeared");
        return found!;
    }

    private static void PressChord(VirtualKeyShort key)
    {
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.ALT, key);
    }
}
