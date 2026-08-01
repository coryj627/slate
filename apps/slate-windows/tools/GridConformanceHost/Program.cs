// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using SlateWindows.Grids;
using uniffi.slate_uniffi;

namespace GridConformanceHost;

/// <summary>
/// The W4-1 conformance host: a minimal window carrying ONE
/// AccessibleDataGrid over a generated fixture, driven by the FlaUI
/// conformance suite (the reusable §W-C gate every consuming surface
/// inherits). `args[0]` sets the row count — the suite runs the §8.7
/// matrix at 200 rows and the UIA-virtualization trap probe at 10,000.
/// </summary>
internal static class Program
{
    private sealed record FixtureRow(int Index, string Name, string Status, string Notes);

    [STAThread]
    private static void Main(string[] args)
    {
        // The Fluent addendum (w4_spec baseline): the conformance suite
        // must validate the FLUENT-restyled grid — the same explicit
        // dictionary layering the app ships — never Aero defaults. The
        // validation seam throws loudly if any dictionary fails to
        // resolve, so a silent fallback cannot fake a green matrix.
        AppContext.SetSwitch(
            "Switch.System.Windows.Appearance.DisableFluentThemeWindowBackdrop",
            true);
        Action<string>? actionLogHolder = null;
        var app = new Application();
        // A dispatcher exception would otherwise kill the host with no
        // trace the suite can read: log it and keep the process alive
        // so the failure message carries the evidence.
        app.DispatcherUnhandledException += (_, e) =>
        {
            actionLogHolder?.Invoke(
                $"dispatcher-error:{e.Exception.GetType().Name}:{e.Exception.Message}");
            e.Handled = true;
        };
        SlateWindows.ThemeManager.ValidateResourceDictionaries();
        using var theme = new SlateWindows.ThemeManager(
            app, SlateWindows.ThemeManager.ReadSystemTheme());

        int rowCount = args.Length > 0
            && int.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : 200;

        var rows = new List<object>(rowCount);
        for (int i = 0; i < rowCount; i++)
        {
            rows.Add(new FixtureRow(
                i,
                $"Note {i:D5}",
                i % 3 == 0 ? "Open" : "Done",
                $"fixture row {i}"));
        }

        var actionLog = new TextBlock
        {
            Text = "none",
            Focusable = false,
            Margin = new Thickness(8, 2, 8, 2),
        };
        // No explicit automation Name: the TextBlock peer's Name IS the
        // text content, which is how the suite reads outcomes back
        // cross-process (an explicit Name masked the content).
        AutomationProperties.SetAutomationId(actionLog, "GridActionLog");
        actionLogHolder = text => actionLog.Text = text;

        var grid = new AccessibleDataGrid();
        grid.Bind(
            new[]
            {
                new AccessibleGridColumn
                {
                    Header = "Name",
                    Cell = row => ((FixtureRow)row).Name,
                    Sort = Comparer<object>.Create((x, y) =>
                        string.CompareOrdinal(((FixtureRow)x).Name, ((FixtureRow)y).Name)),
                    IsRowHeader = true,
                },
                new AccessibleGridColumn
                {
                    Header = "Status",
                    Cell = row => ((FixtureRow)row).Status,
                    Sort = Comparer<object>.Create((x, y) =>
                        string.CompareOrdinal(((FixtureRow)x).Status, ((FixtureRow)y).Status)),
                },
                new AccessibleGridColumn
                {
                    Header = "Notes",
                    Cell = row => ((FixtureRow)row).Notes,
                    AccessibilityHint = _ => "read-only: fixture",
                },
            },
            rows,
            $"{rowCount} rows, 3 columns.",
            "Conformance fixture, data grid",
            rowAudioDescription: row =>
                $"{((FixtureRow)row).Name}. Status: {((FixtureRow)row).Status}",
            rowActions: new[]
            {
                new AccessibleGridRowAction
                {
                    Name = "Open",
                    Execute = row => actionLog.Text = $"opened:{((FixtureRow)row).Index}",
                },
                new AccessibleGridRowAction
                {
                    Name = "Edit property",
                    Execute = _ => { },
                    IsEnabled = _ => false,
                    DisabledReason = "Read-only fixture",
                },
            },
            exportProducer: format => format == ExportFormat.Csv
                ? $"Name,Status,Notes\r\nfixture,{rowCount},rows"
                : $"|Name|Status|Notes|\n|fixture|{rowCount}|rows|");
        grid.ExportProduced += (format, text) =>
            actionLog.Text = $"exported:{format}:{text.Length}";
        // The suite drives the menu by keyboard and reads its
        // lifecycle here — popup items are not reliably enumerable
        // through desktop UIA on a starved session, and "opening" is
        // not "open": keys sent between the two land on the grid
        // underneath (measured: Down+Enter walked the selection). The
        // substrate's menu is persistent, so the hooks attach once.
        grid.Grid.ContextMenuOpening += (_, _) => actionLog.Text = "menu-opening";
        if (grid.Grid.ContextMenu is { } persistentMenu)
        {
            persistentMenu.Opened += (_, _) => actionLog.Text = "menu-open";
            persistentMenu.Closed += (_, _) => actionLog.Text = "menu-closed";
        }
        // The suite reads grid events cross-process through the log —
        // the observable that separates "input never arrived" from
        // "the sort never fired" on a hosted runner.
        grid.Announce = @event =>
            actionLog.Text = $"a11y:{SlateUniffiMethods.A11yRender(@event).Text}";

        var layout = new DockPanel();
        DockPanel.SetDock(actionLog, Dock.Bottom);
        layout.Children.Add(actionLog);
        layout.Children.Add(grid);

        var window = new Window
        {
            Title = "Slate Grid Conformance Host",
            Width = 900,
            Height = 600,
            Content = layout,
        };
        AutomationProperties.SetAutomationId(window, "Slate.GridConformanceHost");
        // F2 opens a markdown table on the substrate — the reading-
        // table window's conformance hook (G28): the suite pins
        // first-cell initial focus and Escape-returns.
        window.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.F2)
            {
                e.Handled = true;
                try
                {
                    System.Windows.Window? tableWindow =
                        SlateWindows.Reading.ReadingTableGrid.Show(
                            "| Name | Status |\n| --- | --- |\n| alpha | Open |\n| beta | Done |\n",
                            window);
                    actionLog.Text = $"table-shown:{tableWindow is not null}";
                    if (tableWindow is not null)
                    {
                        // The suite reads this if the window vanishes
                        // between Show and its first desktop poll.
                        tableWindow.Closed += (_, _) =>
                            actionLog.Text = "table-closed";
                    }
                }
                catch (Exception exception)
                {
                    // The suite reads this on a missing-window timeout
                    // — an exception here must be diagnosable, never a
                    // silent absence.
                    actionLog.Text =
                        $"table-error:{exception.GetType().Name}:{exception.Message}";
                }
            }
        };

        app.Run(window);
    }
}
