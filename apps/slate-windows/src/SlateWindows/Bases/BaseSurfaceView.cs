// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using SlateWindows.Grids;
using uniffi.slate_uniffi;

namespace SlateWindows.Bases;

/// <summary>
/// W4-6 (#738): the `.base` tab body — the mac BaseContainerView twin,
/// phase A scope: header (name, view, count, Refresh), informational
/// banners (contract C4), and the table on the one grid substrate
/// (contract C2). The header controls carry the disabledReason-as-hint
/// pattern; banners are captions, never dialogs, and the whole banner
/// region leaves the UIA tree when empty (the W4-5 axe lesson).
/// </summary>
internal sealed class BaseSurfaceView : UserControl
{
    public static readonly DependencyProperty ModelProperty =
        DependencyProperty.Register(
            nameof(Model),
            typeof(BaseDocumentViewModel),
            typeof(BaseSurfaceView),
            new PropertyMetadata(null, OnModelChanged));

    private readonly TextBlock _title;
    private readonly TextBlock _countReadout;
    private readonly Button _refresh;
    private readonly StackPanel _banners;
    private readonly TextBlock _stateBanner;
    private readonly ItemsControl _warningBanners;
    private readonly TextBlock _emptyState;
    private readonly AccessibleDataGrid _grid;

    public BaseSurfaceView()
    {
        AutomationProperties.SetAutomationId(this, "BaseSurface");

        _title = new TextBlock
        {
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetHeadingLevel(
            _title, AutomationHeadingLevel.Level2);

        _countReadout = new TextBlock
        {
            Margin = new Thickness(12, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        _countReadout.SetResourceReference(
            TextBlock.ForegroundProperty, "Slate.SecondaryTextBrush");
        AutomationProperties.SetAutomationId(_countReadout, "BaseCountReadout");

        _refresh = new Button
        {
            Content = "Refresh",
            Margin = new Thickness(12, 0, 0, 0),
            Padding = new Thickness(10, 2, 10, 2),
        };
        AutomationProperties.SetAutomationId(_refresh, "BaseRefresh");
        AutomationProperties.SetHelpText(
            _refresh, "Reload the base and re-run its current view.");
        _refresh.Click += (_, _) => RefreshFromHeader();

        var header = new DockPanel { Margin = new Thickness(12, 8, 12, 4) };
        DockPanel.SetDock(_refresh, Dock.Right);
        header.Children.Add(_refresh);
        var titleRow = new StackPanel { Orientation = Orientation.Horizontal };
        titleRow.Children.Add(_title);
        titleRow.Children.Add(_countReadout);
        header.Children.Add(titleRow);

        _stateBanner = BannerText();
        AutomationProperties.SetAutomationId(_stateBanner, "BaseStateBanner");
        _warningBanners = new ItemsControl
        {
            Focusable = false,
            ItemTemplate = WarningTemplate(),
        };
        AutomationProperties.SetAutomationId(_warningBanners, "BaseWarningBanners");
        _banners = new StackPanel { Margin = new Thickness(12, 0, 12, 4) };
        _banners.Children.Add(_stateBanner);
        _banners.Children.Add(_warningBanners);

        _emptyState = new TextBlock
        {
            Margin = new Thickness(32),
            HorizontalAlignment = HorizontalAlignment.Center,
            Text = BasePhrase.EmptyResults,
            Visibility = Visibility.Collapsed,
        };
        _emptyState.SetResourceReference(
            TextBlock.ForegroundProperty, "Slate.SecondaryTextBrush");
        AutomationProperties.SetAutomationId(_emptyState, "BaseEmptyState");

        _grid = new AccessibleDataGrid
        {
            GridAutomationId = "BaseTabGrid",
            ExternalSortHandler = OnExternalSort,
        };

        var layout = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(_banners, Dock.Top);
        DockPanel.SetDock(_emptyState, Dock.Top);
        layout.Children.Add(header);
        layout.Children.Add(_banners);
        layout.Children.Add(_emptyState);
        layout.Children.Add(_grid);
        Content = layout;
    }

    public BaseDocumentViewModel? Model
    {
        get => (BaseDocumentViewModel?)GetValue(ModelProperty);
        set => SetValue(ModelProperty, value);
    }

    /// <summary>Substrate probe for facts + FlaUI.</summary>
    internal AccessibleDataGrid GridForTests => _grid;

    private static void OnModelChanged(
        DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (BaseSurfaceView)d;
        if (e.OldValue is BaseDocumentViewModel oldModel)
        {
            oldModel.ResultPublished -= view.OnResultPublished;
            oldModel.PropertyChanged -= view.OnModelPropertyChanged;
        }
        if (e.NewValue is BaseDocumentViewModel model)
        {
            model.ResultPublished += view.OnResultPublished;
            model.PropertyChanged += view.OnModelPropertyChanged;
            view.RenderAll();
        }
    }

    private void RefreshFromHeader()
    {
        if (Model is not { } model)
        {
            return;
        }
        if (model.State == BaseLoadState.Failed)
        {
            // The recovery posture (contract C13): a failed document's
            // header action is a full reopen, not a re-execute on a
            // handle that no longer exists.
            model.Load();
            return;
        }
        model.Refresh();
    }

    private bool OnExternalSort(int columnIndex, bool ascending) =>
        Model?.ApplySortFromGrid(columnIndex, ascending) ?? false;

    private void OnResultPublished(object? sender, EventArgs e) => RenderAll();

    private void OnModelPropertyChanged(
        object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(BaseDocumentViewModel.State)
            or nameof(BaseDocumentViewModel.StateMessage))
        {
            RenderChrome();
        }
    }

    private void RenderAll()
    {
        RenderChrome();
        RenderGrid();
    }

    private void RenderChrome()
    {
        if (Model is not { } model)
        {
            return;
        }
        _title.Text = model.DisplayName;
        AutomationProperties.SetName(_title, $"Base {model.DisplayName}");
        _countReadout.Text = model.Result is { } result
            ? $"{result.ShownCount} of {result.TotalCount}"
            : string.Empty;
        AutomationProperties.SetName(
            _countReadout,
            _countReadout.Text.Length > 0 ? $"Results: {_countReadout.Text}" : string.Empty);
        _refresh.Content =
            model.State == BaseLoadState.Failed ? "Retry" : "Refresh";
        AutomationProperties.SetHelpText(
            _refresh,
            model.State == BaseLoadState.Failed
                ? "Attempts to reopen the Base at its current path."
                : "Reload the base and re-run its current view.");

        _stateBanner.Text = model.StateMessage ?? string.Empty;
        AutomationProperties.SetName(_stateBanner, _stateBanner.Text);
        _stateBanner.Visibility = _stateBanner.Text.Length > 0
            ? Visibility.Visible
            : Visibility.Collapsed;
        string[] warnings = model.Result?.Warnings ?? [];
        _warningBanners.ItemsSource = warnings;
        _warningBanners.Visibility =
            warnings.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        // The whole banner block leaves the tree when empty — an
        // on-screen zero-size region is an axe BoundingRectangle
        // failure (the W4-5 lesson).
        _banners.Visibility =
            _stateBanner.Visibility == Visibility.Visible
            || _warningBanners.Visibility == Visibility.Visible
                ? Visibility.Visible
                : Visibility.Collapsed;

        _emptyState.Visibility =
            model.ShowEmptyState ? Visibility.Visible : Visibility.Collapsed;
        _grid.Visibility = model.ShowEmptyState || model.Result is null
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void RenderGrid()
    {
        if (Model is not { Result: { } result } model)
        {
            return;
        }
        var columns = new List<AccessibleGridColumn>(result.Columns.Length);
        for (int index = 0; index < result.Columns.Length; index++)
        {
            BasesColumn column = result.Columns[index];
            int columnIndex = index;
            columns.Add(new AccessibleGridColumn
            {
                Header = column.Label,
                Cell = row => ((BaseGridRowViewModel)row).DisplayAt(columnIndex),
                IsRowHeader = column.Role == ColumnRole.Primary,
                // Sorting is core's (contract C1/C6); while the
                // document is failed there is no sort affordance at
                // all (contract C13) — Bind is not reached then, the
                // grid is collapsed.
                IsExternallySortable = true,
            });
        }
        var rows = new List<object>(result.Rows.Length);
        foreach (BasesRow row in result.Rows)
        {
            rows.Add(new BaseGridRowViewModel(row));
        }
        _grid.Bind(
            columns,
            rows,
            summary: BaseSummaryFormatter.SummaryText(result, quickFilterActive: false),
            accessibilityLabel: result.AudioSummary,
            rowAudioDescription: static row =>
                ((BaseGridRowViewModel)row).AudioDescription);
        _grid.SetSortIndicator(model.SortState);
    }

    private static TextBlock BannerText()
    {
        var text = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Focusable = true,
            Margin = new Thickness(0, 2, 0, 2),
        };
        text.SetResourceReference(
            TextBlock.ForegroundProperty, "Slate.WarningBrush");
        return text;
    }

    private static DataTemplate WarningTemplate()
    {
        var text = new FrameworkElementFactory(typeof(TextBlock));
        text.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding());
        text.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
        text.SetValue(FocusableProperty, true);
        text.SetResourceReference(
            TextBlock.ForegroundProperty, "Slate.WarningBrush");
        return new DataTemplate { VisualTree = text };
    }
}

/// <summary>One bound row: core's <see cref="BasesRow"/> untransformed
/// (INV-1) — cells are <c>BasesValue.Display</c> verbatim.</summary>
internal sealed class BaseGridRowViewModel
{
    public BaseGridRowViewModel(BasesRow row) => Row = row;

    public BasesRow Row { get; }

    public string DisplayAt(int columnIndex) =>
        columnIndex >= 0 && columnIndex < Row.Values.Length
            ? Row.Values[columnIndex].Display
            : string.Empty;

    public string AudioDescription => Row.AudioDescription;
}

/// <summary>The mac BaseSummaryFormatter twin: custom summary cells,
/// else core's audio summary, else the counted fallback — with the
/// "filtered" prefix while a quick filter is active (phase B).</summary>
internal static class BaseSummaryFormatter
{
    public static string SummaryText(BasesResultSet result, bool quickFilterActive)
    {
        string body = result.Summaries.Length > 0
            ? string.Join(
                ", ",
                result.Summaries.Select(cell =>
                    $"{LabelFor(result, cell.ColumnId)} {cell.Summary}: "
                    + (cell.Value.Display.Length > 0 ? cell.Value.Display : "empty")))
            : result.AudioSummary.Length > 0
                ? result.AudioSummary
                : $"Base table: {result.ShownCount} of {result.TotalCount} rows.";
        return quickFilterActive ? $"Summaries: filtered — {body}" : body;
    }

    private static string LabelFor(BasesResultSet result, string columnId)
    {
        foreach (BasesColumn column in result.Columns)
        {
            if (string.Equals(column.Id, columnId, StringComparison.Ordinal))
            {
                return column.Label;
            }
        }
        return columnId;
    }
}
