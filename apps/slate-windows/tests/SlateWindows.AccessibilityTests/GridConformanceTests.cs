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

            // Headers announced on entry (§8.7): assert the header
            // ELEMENTS in the tree and the PER-CELL TableItem
            // associations — the routes AT actually reads on entry.
            // The grid-LEVEL ColumnHeaders list is deliberately not
            // load-bearing: the Server gate runner answers it empty
            // while header item×20 sit realized in the same tree
            // (captured census, 2026-08-01) — a WPF peer quirk, not a
            // header failure.
            AutomationElement[] headerItems = Array.Empty<AutomationElement>();
            bool headersAppeared = SpinWait.SpinUntil(
                () =>
                {
                    headerItems = grid.FindAllDescendants(
                        cf => cf.ByControlType(ControlType.HeaderItem));
                    string[] names = headerItems
                        .Select(item => item.Properties.Name.ValueOrDefault ?? "")
                        .ToArray();
                    return names.Contains("Name")
                        && names.Contains("Status")
                        && names.Contains("Notes")
                        && names.Contains("Note 00000");
                },
                TimeSpan.FromSeconds(15));
            if (!headersAppeared)
            {
                CaptureForDiagnostics(window, "grid-headers-missing");
                string kinds = string.Join(
                    ", ",
                    grid.FindAllDescendants()
                        .GroupBy(d => d.Properties.LocalizedControlType.ValueOrDefault ?? "?")
                        .Select(group => $"{group.Key}×{group.Count()}")
                        .OrderBy(s => s, StringComparer.Ordinal));
                Assert.Fail(
                    "column + row header items did not materialize. grid holds: " + kinds);
            }

            // Cell labels carry the "Header: value" contract.
            var firstCell = grid.Patterns.Grid.Pattern.GetItem(0, 0);
            Assert.Equal("Name: Note 00000", firstCell.Name);
            var statusCell = grid.Patterns.Grid.Pattern.GetItem(1, 1);
            Assert.Equal("Status: Done", statusCell.Name);

            // The per-cell association: entering THIS cell, AT resolves
            // its column header and its row identity.
            Assert.True(
                statusCell.Patterns.TableItem.IsSupported,
                "TableItem pattern missing on a cell");
            var tableItem = statusCell.Patterns.TableItem.Pattern;
            Assert.Contains(
                tableItem.ColumnHeaderItems.Value ?? Array.Empty<AutomationElement>(),
                header => header.Name == "Status");
            Assert.Contains(
                tableItem.RowHeaderItems.Value ?? Array.Empty<AutomationElement>(),
                header => header.Name == "Note 00001");

            // Keyboard sort: focus a cell, Ctrl+Alt+S twice = the
            // second toggle flips to DESCENDING, reordering row 0.
            // UIA SetFocus, not a mouse click — the pointer path is
            // position-fragile on a hosted runner — and the window is
            // FORCED foreground first: synthesized input goes to the
            // foreground queue, and keyboard focus alone does not make
            // a window foreground on the gate runner.
            EnsureForeground(window);
            FocusCell(firstCell);
            AutomationElement actionLog = WaitForElement(
                window, "GridActionLog", TimeSpan.FromSeconds(5));
            PressChord(VirtualKeyShort.KEY_S);
            Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(500));
            PressChord(VirtualKeyShort.KEY_S);
            Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(500));
            Assert.True(
                SpinWait.SpinUntil(
                    () => grid.Patterns.Grid.Pattern.GetItem(0, 0).Name
                        == "Name: Note 00199",
                    TimeSpan.FromSeconds(5)),
                // The host mirrors grid events into the log: "a11y:
                // GridSorted" proves the chord fired and the reorder
                // stalled; anything else means input never arrived.
                "descending sort did not reorder row 0; host log: "
                    + (actionLog.Properties.Name.ValueOrDefault ?? "<empty>"));

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
            Keyboard.Type(VirtualKeyShort.APPS);
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
                    () => (log.Properties.Name.ValueOrDefault ?? "")
                        .StartsWith("opened:", StringComparison.Ordinal),
                    TimeSpan.FromSeconds(5)),
                "the Open row action did not execute; host log: "
                    + (log.Properties.Name.ValueOrDefault ?? "<empty>"));

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

            // The ItemContainerPattern path — the named crash class is
            // THIS provider route, distinct from GridPattern.GetItem:
            // enumerate containers in order without realizing the whole
            // list, and realize a stretch through VirtualizedItem.
            Assert.True(
                grid.Patterns.ItemContainer.IsSupported,
                "ItemContainerPattern missing");
            var containers = grid.Patterns.ItemContainer.Pattern;
            AutomationElement? cursor = null;
            int walked = 0;
            int realized = 0;
            AutomationElement? lastRealized = null;
            for (int i = 0; i < 40; i++)
            {
                cursor = containers.FindItemByProperty(cursor, null, null);
                if (cursor is null)
                {
                    break;
                }
                walked++;
                if (cursor.Patterns.VirtualizedItem.IsSupported)
                {
                    cursor.Patterns.VirtualizedItem.Pattern.Realize();
                    realized++;
                    lastRealized = cursor;
                }
            }
            Assert.True(walked >= 30, $"ItemContainer walk stalled at {walked} items");
            // Realization is MANDATORY evidence, not opportunistic
            // (adversarial round 2): a provider that stops offering
            // VirtualizedItem must fail here, and a realized row must
            // answer with real content.
            Assert.True(
                realized >= 1,
                $"no virtualized container offered VirtualizedItem across {walked} items");
            Assert.True(
                SpinWait.SpinUntil(
                    () => lastRealized!.FindAllChildren().Any(
                        cell => (cell.Properties.Name.ValueOrDefault ?? "")
                            .StartsWith("Name: Note", StringComparison.Ordinal)),
                    TimeSpan.FromSeconds(10)),
                "the realized row never produced its cells");

            EnsureForeground(window);
            FocusCell(grid.Patterns.Grid.Pattern.GetItem(0, 0));
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.END);
            Assert.True(
                SpinWait.SpinUntil(
                    () => (grid.Patterns.Selection.Pattern.Selection.Value
                            ?? Array.Empty<AutomationElement>())
                        // Ctrl+End lands on the LAST COLUMN of the last
                        // row — "Notes: fixture row 9999" — so match
                        // the unpadded row number.
                        .Any(item => item.Name.Contains("9999", StringComparison.Ordinal)),
                    // Generous: deferred scrolling over 10k Standard-mode
                    // rows realizes containers on a cold runner's pace.
                    TimeSpan.FromSeconds(60)),
                "Ctrl+End did not reach the last row");

            Assert.False(process.HasExited, "the host died under UIA load");
        });
    }

    /// <summary>W4-1 reading-table window (G28): F2 in the host opens
    /// a markdown table on the substrate. Entry must land on the FIRST
    /// CELL (headers + cell announced — round 1 found MoveFocus(First)
    /// landing on the summary), and Escape must close back to the
    /// host.</summary>
    [Fact]
    public void ReadingTableWindowFocusesFirstCellAndEscapeReturns()
    {
        RunHost(5, (automation, window, process) =>
        {
            _ = WaitForElement(window, "AccessibleDataGrid", TimeSpan.FromSeconds(10));
            AutomationElement actionLog = WaitForElement(
                window, "GridActionLog", TimeSpan.FromSeconds(5));
            EnsureForeground(window);
            Keyboard.Type(VirtualKeyShort.F2);
            AutomationElement? tableWindow = null;
            bool windowAppeared = SpinWait.SpinUntil(
                () =>
                {
                    tableWindow = automation.GetDesktop().FindFirstChild(
                        cf => cf.ByAutomationId("ReadingTableGridWindow"));
                    return tableWindow is not null;
                },
                TimeSpan.FromSeconds(15));
            // The host's F2 handler mirrors its outcome into the log:
            // "table-shown:True" (window lost?), "table-error:…" (the
            // FFI or window path threw), or anything else (F2 never
            // arrived).
            Assert.True(
                windowAppeared,
                "the table window never appeared; host log: "
                    + (actionLog.Properties.Name.ValueOrDefault ?? "<empty>"));

            // Initial keyboard focus is the first CELL, by label.
            Assert.True(
                SpinWait.SpinUntil(
                    () => automation.FocusedElement()?.Name == "Name: alpha",
                    TimeSpan.FromSeconds(10)),
                $"first cell not focused; focus is on "
                    + $"'{automation.FocusedElement()?.Name ?? "<none>"}'");

            Keyboard.Type(VirtualKeyShort.ESCAPE);
            Assert.True(
                SpinWait.SpinUntil(
                    () => automation.GetDesktop().FindFirstChild(
                        cf => cf.ByAutomationId("ReadingTableGridWindow")) is null,
                    TimeSpan.FromSeconds(10)),
                "Escape did not close the table window");
            // Focus RESTORATION, not just closure (adversarial round
            // 2): the owner window must hold keyboard focus again —
            // focus stranded on the desktop or another process would
            // otherwise pass.
            Assert.True(
                SpinWait.SpinUntil(
                    () => automation.FocusedElement()?.Properties.ProcessId.ValueOrDefault
                        == process.Id,
                    TimeSpan.FromSeconds(10)),
                "Escape did not return keyboard focus to the host");
            Assert.False(process.HasExited, "the host died with the table window");
        });
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    /// <summary>
    /// Make the window the FOREGROUND window, verified — synthesized
    /// keyboard input goes to the foreground queue, and Windows denies
    /// SetForegroundWindow to background processes unless they own
    /// recent input. The Alt tap grants exactly that (the documented
    /// unlock), then the claim is verified against
    /// GetForegroundWindow, not assumed.
    /// </summary>
    private static void EnsureForeground(Window window)
    {
        IntPtr handle = new(window.Properties.NativeWindowHandle.Value.ToInt64());
        for (int attempt = 0; attempt < 5; attempt++)
        {
            if (GetForegroundWindow() == handle)
            {
                return;
            }
            // A CONTROL tap, deliberately not Alt: any synthesized key
            // grants the calling process the recent-input credential
            // SetForegroundWindow requires, but a bare Alt tap drops
            // the target window into system-menu mode — the NEXT key
            // (a chord letter, F2) gets eaten by menu navigation.
            Keyboard.Press(VirtualKeyShort.CONTROL);
            Keyboard.Release(VirtualKeyShort.CONTROL);
            window.Focus();
            Wait.UntilInputIsProcessed();
            if (SpinWait.SpinUntil(
                    () => GetForegroundWindow() == handle,
                    TimeSpan.FromSeconds(2)))
            {
                return;
            }
        }
        Assert.Fail("the host window could not take the foreground");
    }

    /// <summary>UIA SetFocus on a cell — deterministic on hosted
    /// runners, where pointer-position clicks are fragile.</summary>
    private static void FocusCell(AutomationElement cell)
    {
        cell.Focus();
        Wait.UntilInputIsProcessed();
        Assert.True(
            SpinWait.SpinUntil(
                () => cell.Properties.HasKeyboardFocus.ValueOrDefault,
                TimeSpan.FromSeconds(5)),
            $"cell '{cell.Name}' did not take keyboard focus");
    }

    private static void CaptureForDiagnostics(Window window, string name)
    {
        try
        {
            string root = Environment.GetEnvironmentVariable("RUNNER_TEMP")
                ?? Path.GetTempPath();
            string path = Path.Combine(
                root, "slate-accessibility-results", $"{name}.png");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            FlaUI.Core.Capturing.Capture.Element(window).ToFile(path);
        }
        catch (Exception)
        {
            // Diagnostics only — a capture failure must not mask the
            // assertion that requested it.
        }
    }

    private static AutomationElement WaitForDesktopElement(
        UIA3Automation automation, string automationId, TimeSpan timeout)
    {
        AutomationElement? found = null;
        Assert.True(
            SpinWait.SpinUntil(
                () =>
                {
                    found = automation.GetDesktop().FindFirstChild(
                        cf => cf.ByAutomationId(automationId));
                    return found is not null;
                },
                timeout),
            $"no desktop element with AutomationId {automationId} appeared");
        return found!;
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
            // The body drives real keyboard input; whatever test ran
            // before this one owned the foreground.
            EnsureForeground(window!);
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
        // Explicit press/release ordering with settle time between
        // steps — TypeSimultaneously's burst can land the letter
        // before the modifiers register on a loaded runner.
        Keyboard.Press(VirtualKeyShort.CONTROL);
        Keyboard.Press(VirtualKeyShort.ALT);
        Wait.UntilInputIsProcessed();
        Keyboard.Type(key);
        Wait.UntilInputIsProcessed();
        Keyboard.Release(VirtualKeyShort.ALT);
        Keyboard.Release(VirtualKeyShort.CONTROL);
        Wait.UntilInputIsProcessed();
    }
}
