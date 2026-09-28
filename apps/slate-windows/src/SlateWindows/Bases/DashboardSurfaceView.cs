// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using SlateWindows.Grids;
using uniffi.slate_uniffi;

namespace SlateWindows.Bases;

/// <summary>
/// W4-6 (#738, contract C12): the dashboard tab body — the mac
/// DashboardContainerView twin. H2 title (the surface-title level the
/// Base tab header uses — H1 belongs to the note title convention),
/// then per-section H3 + read-only grid or list per the section's
/// view override (no row actions, no editing, no activation);
/// missing/degraded/failed sections banner with the mac wording and
/// keep their siblings rendering.
/// </summary>
internal sealed class DashboardSurfaceView : UserControl
{
    public static readonly DependencyProperty ModelProperty =
        DependencyProperty.Register(
            nameof(Model),
            typeof(DashboardViewModel),
            typeof(DashboardSurfaceView),
            new PropertyMetadata(null, OnModelChanged));

    private readonly TextBlock _title;
    private readonly StackPanel _sections;
    private readonly TextBlock _emptyState;
    private string _automationIdRoot = "Dashboard";

    /// <summary>Distinct-id root (the D-12 substrate rule): the DOCK
    /// instance overrides this so docking a dashboard beside its own
    /// open tab never puts duplicate AutomationIds in one window
    /// (red team round 2).</summary>
    public string AutomationIdRoot
    {
        get => _automationIdRoot;
        set
        {
            _automationIdRoot = value;
            AutomationProperties.SetAutomationId(this, value + "Surface");
            AutomationProperties.SetAutomationId(_emptyState, value + "EmptyState");
        }
    }

    public DashboardSurfaceView()
    {
        AutomationProperties.SetAutomationId(this, "DashboardSurface");
        _title = new TextBlock
        {
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(12, 8, 12, 4),
        };
        AutomationProperties.SetHeadingLevel(
            _title, AutomationHeadingLevel.Level2);

        _emptyState = new TextBlock
        {
            Margin = new Thickness(32),
            HorizontalAlignment = HorizontalAlignment.Center,
            Text = "No dashboard sections. Add a saved query section to show results.",
            Visibility = Visibility.Collapsed,
        };
        _emptyState.SetResourceReference(
            TextBlock.ForegroundProperty, "Slate.SecondaryTextBrush");
        AutomationProperties.SetAutomationId(_emptyState, "DashboardEmptyState");

        _sections = new StackPanel { Margin = new Thickness(12, 0, 12, 12) };

        // Not a stop of its own (W7-7 PR 4b, the sweep's G19): a focusable
        // scroll viewer was the docked dashboard's unnamed first stop, and
        // took the keys of a rebuilt section's cell.
        var scroll = new ScrollViewer
        {
            Focusable = false,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = _sections,
        };

        var layout = new DockPanel();
        DockPanel.SetDock(_title, Dock.Top);
        DockPanel.SetDock(_emptyState, Dock.Top);
        layout.Children.Add(_title);
        layout.Children.Add(_emptyState);
        layout.Children.Add(scroll);
        Content = layout;
        RegionFocusGuard.SetLanding(this, LandInSurface);
    }

    /// <summary>The sections' stops — a banner, a grid, a list — in order,
    /// with the section each is in, as the last render built them. A section
    /// is named by what it shows — its saved query, its heading, and which
    /// of the sections showing those it is — not by its index, which the
    /// dashboard editor's reorder or insert hands to another section (codex
    /// PR 4b r1 F6).</summary>
    private readonly List<(string Section, UIElement Stop)> _sectionStops = [];

    /// <summary>Where the reader was when a render replaced the sections:
    /// the section, and the row of its grid or list.</summary>
    private (string Section, BasesRow? Row)? _readerAt;

    /// <summary>
    /// W7-7 PR 4b (#1247, R-5; the sweep's G10): the dashboard's landing —
    /// where the keys go when the element holding them goes away
    /// (<see cref="RegionFocusGuard"/>). Every vault change reloads an open
    /// dashboard and rebuilds its sections, destroying the grid or list the
    /// reader was in: the keys land in the SAME section — on the same note's
    /// row, silently, when it is still there, else on that section's current
    /// or first row — else, the section gone, on the first section's stop.
    /// </summary>
    internal bool LandInSurface()
    {
        string? section = _readerAt?.Section;
        BasesRow? row = _readerAt?.Row;
        IEnumerable<UIElement> order = _sectionStops
            .OrderBy(entry => entry.Section == section ? 0 : 1)
            .ThenBy(entry => row is not null && entry.Stop is AccessibleDataGrid or ListBox ? 0 : 1)
            .Select(entry => entry.Stop);
        foreach (UIElement stop in order)
        {
            if (!stop.IsVisible)
            {
                continue;
            }

            if (stop is AccessibleDataGrid grid)
            {
                if ((row is not null && grid.SelectRow(
                        item => item is BaseGridRowViewModel candidate && SameRow(candidate.Row, row),
                        moveFocus: true))
                    || SelectorFocus.LandOnStop(grid.Grid))
                {
                    return true;
                }

                continue;
            }

            if (row is not null
                && stop is ListBox list
                && list.Items.OfType<ListBoxItem>().FirstOrDefault(item => item.Tag is BasesRow candidate && SameRow(candidate, row)) is { } match)
            {
                // The reader's row, selected again as the arrows had it (a
                // dashboard list activates nothing), with the keys.
                list.SelectedItem = match;
                if (SelectorFocus.FocusItem(list, match))
                {
                    return true;
                }
            }

            if (SelectorFocus.LandOnStop(stop))
            {
                return true;
            }
        }

        return false;
    }

    private static bool SameRow(BasesRow candidate, BasesRow row) =>
        string.Equals(candidate.FilePath, row.FilePath, StringComparison.Ordinal)
            && candidate.TaskOrdinal == row.TaskOrdinal;

    /// <summary>The section — and the grid's or list's row — holding the
    /// keys, before a render replaces them.</summary>
    private void CaptureReader()
    {
        (string Section, UIElement Stop) held = _sectionStops.FirstOrDefault(entry => entry.Stop.IsKeyboardFocusWithin);
        _readerAt = held.Stop is null
            ? null
            : (held.Section, held.Stop switch
            {
                AccessibleDataGrid grid when grid.CurrentRowForTests() is BaseGridRowViewModel current => current.Row,
                ListBox list when (SelectorFocus.FocusedItem(list) ?? list.SelectedItem) is ListBoxItem { Tag: BasesRow listed } => listed,
                _ => null,
            });
    }

    /// <summary>A section's name for <see cref="_sectionStops"/>.</summary>
    private static string SectionKey(DashboardSectionStatus status, int occurrence) =>
        $"{status.SavedQueryId}\u001f{status.HeadingOverride}\u001f{occurrence}";

    public DashboardViewModel? Model
    {
        get => (DashboardViewModel?)GetValue(ModelProperty);
        set => SetValue(ModelProperty, value);
    }

    internal StackPanel SectionsForTests => _sections;

    private static void OnModelChanged(
        DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (DashboardSurfaceView)d;
        if (e.OldValue is DashboardViewModel oldModel)
        {
            oldModel.SectionsPublished -= view.OnSectionsPublished;
        }
        if (e.NewValue is DashboardViewModel model)
        {
            model.SectionsPublished += view.OnSectionsPublished;
            view.Render();
        }
    }

    private void OnSectionsPublished(object? sender, EventArgs e) => Render();

    private void Render()
    {
        if (Model is not { } model)
        {
            return;
        }
        _title.Text = model.Name;
        AutomationProperties.SetName(_title, $"Dashboard {model.Name}");
        CaptureReader();
        _sectionStops.Clear();
        _sections.Children.Clear();
        _emptyState.Visibility = model.Sections.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
        int index = 0;
        var shown = new Dictionary<(string Query, string? Heading), int>();
        foreach (DashboardSectionViewModel section in model.Sections)
        {
            (string, string?) shows = (section.Status.SavedQueryId, section.Status.HeadingOverride);
            int occurrence = shown.GetValueOrDefault(shows);
            shown[shows] = occurrence + 1;
            string key = SectionKey(section.Status, occurrence);
            var header = new TextBlock
            {
                Text = section.Title,
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 12, 0, 4),
            };
            AutomationProperties.SetHeadingLevel(
                header, AutomationHeadingLevel.Level3);
            _sections.Children.Add(header);

            if (section.Message is { Length: > 0 } message)
            {
                var banner = new TextBlock
                {
                    Text = message,
                    TextWrapping = TextWrapping.Wrap,
                    Focusable = true,
                    Margin = new Thickness(0, 0, 0, 4),
                };
                banner.SetResourceReference(
                    TextBlock.ForegroundProperty, "Slate.WarningBrush");
                AutomationProperties.SetAutomationId(
                    banner, $"{_automationIdRoot}Section{index}Banner");
                _sections.Children.Add(banner);
                _sectionStops.Add((key, banner));
            }
            if (section.Result is { } result
                && section.State is DashboardSectionState.Ready
                    or DashboardSectionState.Degraded)
            {
                // The section's authored renderer choice (red team
                // round 1: the editor persisted ViewOverride but
                // nothing consumed it). Case-insensitive: the value
                // may be hand-authored.
                UIElement content = string.Equals(
                        section.Status.ViewOverride, "list",
                        StringComparison.OrdinalIgnoreCase)
                        ? BuildSectionList(_automationIdRoot, index, result)
                        : BuildSectionGrid(_automationIdRoot, index, result);
                _sections.Children.Add(content);
                _sectionStops.Add((key, content));
            }
            index++;
        }
    }

    /// <summary>A READ-ONLY thin grid configuration (contract C2): no
    /// editing seam, no row actions, no activation — the mac
    /// BaseReadOnlyResultView.</summary>
    private static AccessibleDataGrid BuildSectionGrid(
        string idRoot, int index, BasesResultSet result)
    {
        var grid = new AccessibleDataGrid
        {
            GridAutomationId = $"{idRoot}Section{index}Grid",
            MaxHeight = 320,
        };
        var columns = new List<AccessibleGridColumn>(result.Columns.Length);
        for (int columnIndex = 0; columnIndex < result.Columns.Length; columnIndex++)
        {
            int captured = columnIndex;
            columns.Add(new AccessibleGridColumn
            {
                Header = result.Columns[columnIndex].Label,
                Cell = row => ((BaseGridRowViewModel)row).DisplayAt(captured),
                IsRowHeader = result.Columns[columnIndex].Role == ColumnRole.Primary,
            });
        }
        var rows = new List<object>(result.Rows.Length);
        foreach (BasesRow row in result.Rows)
        {
            rows.Add(new BaseGridRowViewModel(row));
        }
        grid.Bind(
            columns,
            rows,
            summary: BaseSummaryFormatter.SummaryText(result, quickFilterActive: false),
            accessibilityLabel: result.AudioSummary,
            rowAudioDescription: static row =>
                ((BaseGridRowViewModel)row).AudioDescription);
        return grid;
    }

    /// <summary>The "list" view override: core's row readbacks in a
    /// keyboard-navigable read-only list (the thin twin of the Base
    /// tab's list renderer — no actions, no activation).</summary>
    private static UIElement BuildSectionList(
        string idRoot, int index, BasesResultSet result)
    {
        var list = new ListBox
        {
            MaxHeight = 320,
        };
        AutomationProperties.SetAutomationId(list, $"{idRoot}Section{index}List");
        AutomationProperties.SetName(list, result.AudioSummary);
        foreach (BasesRow row in result.Rows)
        {
            list.Items.Add(new ListBoxItem
            {
                // The row it reads back: the landing finds the reader's row
                // in the rebuilt list by it.
                Tag = row,
                Content = new TextBlock
                {
                    Text = row.AudioDescription,
                    TextWrapping = TextWrapping.Wrap,
                },
            });
        }
        return list;
    }
}
