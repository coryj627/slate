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

        var scroll = new ScrollViewer
        {
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
    }

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
        _sections.Children.Clear();
        _emptyState.Visibility = model.Sections.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
        int index = 0;
        foreach (DashboardSectionViewModel section in model.Sections)
        {
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
            }
            if (section.Result is { } result
                && section.State is DashboardSectionState.Ready
                    or DashboardSectionState.Degraded)
            {
                // The section's authored renderer choice (red team
                // round 1: the editor persisted ViewOverride but
                // nothing consumed it). Case-insensitive: the value
                // may be hand-authored.
                _sections.Children.Add(
                    string.Equals(
                        section.Status.ViewOverride, "list",
                        StringComparison.OrdinalIgnoreCase)
                        ? BuildSectionList(_automationIdRoot, index, result)
                        : BuildSectionGrid(_automationIdRoot, index, result));
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
