// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using SlateWindows.Grids;
using uniffi.slate_uniffi;

namespace SlateWindows.Bases;

/// <summary>The per-tab renderer choice (the mac BaseRendererMode
/// twin): a TRANSIENT override wins, else the view's own type. Never
/// persisted anywhere (contract C4).</summary>
internal enum BaseRendererOverride
{
    Table,
    List,
}

/// <summary>
/// W4-6 (#738): the `.base` tab body — the mac BaseContainerView twin.
/// Header (name, view picker, count, quick filter, Refresh/Retry),
/// informational banners (contract C4), and the content pair: the
/// table on the one grid substrate (contract C2) or the row-navigation
/// list — exactly one of the two is ever in the UIA tree. The quick
/// filter is transient four ways (contract C5); Ctrl+F reaches it
/// through the substrate's grid-scoped FilterRequested hook.
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
    private readonly ComboBox _viewPicker;
    private readonly TextBlock _countReadout;
    private readonly TextBox _quickFilter;
    private readonly Button _refresh;
    private readonly StackPanel _banners;
    private readonly TextBlock _stateBanner;
    private readonly ItemsControl _warningBanners;
    private readonly TextBlock _emptyState;
    private readonly AccessibleDataGrid _grid;
    private readonly ListBox _list;
    private readonly DispatcherTimer _filterDebounce;
    private BaseRendererOverride? _rendererOverride;
    private bool _synchronizingPicker;

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

        _viewPicker = new ComboBox
        {
            Margin = new Thickness(12, 0, 0, 0),
            MinWidth = 120,
            VerticalAlignment = VerticalAlignment.Center,
            DisplayMemberPath = nameof(BaseViewSummary.Name),
        };
        AutomationProperties.SetAutomationId(_viewPicker, "BaseViewPicker");
        AutomationProperties.SetName(_viewPicker, "Base view");
        AutomationProperties.SetHelpText(
            _viewPicker, "Switch the active view in this base.");
        _viewPicker.SelectionChanged += OnViewPicked;

        _countReadout = new TextBlock
        {
            Margin = new Thickness(12, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        _countReadout.SetResourceReference(
            TextBlock.ForegroundProperty, "Slate.SecondaryTextBrush");
        AutomationProperties.SetAutomationId(_countReadout, "BaseCountReadout");

        _quickFilter = new TextBox
        {
            Margin = new Thickness(12, 0, 0, 0),
            MinWidth = 160,
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(6, 2, 6, 2),
        };
        AutomationProperties.SetAutomationId(_quickFilter, "BaseQuickFilter");
        // The transiency is IN the accessible name (mac verbatim,
        // contract C5).
        AutomationProperties.SetName(
            _quickFilter, "Quick filter — temporary, does not change the base");
        AutomationProperties.SetHelpText(
            _quickFilter, "Temporarily filter the visible Base results.");
        _quickFilter.TextChanged += OnQuickFilterTextChanged;
        _quickFilter.PreviewKeyDown += OnQuickFilterKeyDown;

        _filterDebounce = new DispatcherTimer
        {
            // The mac cadence: 150 ms between last keystroke and the
            // core re-execute.
            Interval = TimeSpan.FromMilliseconds(150),
        };
        _filterDebounce.Tick += (_, _) =>
        {
            _filterDebounce.Stop();
            Model?.ApplyQuickFilter();
        };

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
        titleRow.Children.Add(_viewPicker);
        titleRow.Children.Add(_countReadout);
        titleRow.Children.Add(_quickFilter);
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
        _grid.FilterRequested += FocusQuickFilter;

        _list = new ListBox
        {
            Visibility = Visibility.Collapsed,
            SelectionMode = SelectionMode.Single,
        };
        AutomationProperties.SetAutomationId(_list, "BaseTabList");
        ScrollViewer.SetHorizontalScrollBarVisibility(
            _list, ScrollBarVisibility.Disabled);
        // The list renderer participates in selection and activation
        // like the grid (red team round 1: without these, every row
        // command in list mode answered "select a row first" — or
        // worse, acted on a stale grid-era row).
        _list.SelectionChanged += OnListSelectionChanged;
        _list.MouseDoubleClick += OnListDoubleClick;
        _list.KeyDown += OnListKeyDown;

        var layout = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(_banners, Dock.Top);
        DockPanel.SetDock(_emptyState, Dock.Top);
        layout.Children.Add(header);
        layout.Children.Add(_banners);
        layout.Children.Add(_emptyState);
        var contentHost = new Grid();
        contentHost.Children.Add(_grid);
        contentHost.Children.Add(_list);
        layout.Children.Add(contentHost);
        Content = layout;
    }

    public BaseDocumentViewModel? Model
    {
        get => (BaseDocumentViewModel?)GetValue(ModelProperty);
        set => SetValue(ModelProperty, value);
    }

    /// <summary>The dock/read-only posture (the mac
    /// BaseReadOnlyResultView): no editing seam, no row actions, no
    /// activation — navigation and the quick filter remain. The
    /// read-only surface carries its own automation ids so AT (and
    /// journeys) can tell it apart from the tab surface (the D-12
    /// substrate rule).</summary>
    public bool IsReadOnlySurface
    {
        get => _isReadOnlySurface;
        set
        {
            _isReadOnlySurface = value;
            _grid.GridAutomationId = value ? "BasesDockGrid" : "BaseTabGrid";
            AutomationProperties.SetAutomationId(
                _list, value ? "BasesDockList" : "BaseTabList");
        }
    }

    private bool _isReadOnlySurface;

    /// <summary>The transient per-TAB renderer override (mac keys it
    /// by tab, not by document — two tabs on one source may render
    /// differently). Set by the viewAsTable/viewAsList commands.</summary>
    internal BaseRendererOverride? RendererOverride
    {
        get => _rendererOverride;
        set
        {
            _rendererOverride = value;
            RenderAll();
        }
    }

    internal AccessibleDataGrid GridForTests => _grid;

    internal ListBox ListForTests => _list;

    internal TextBox QuickFilterForTests => _quickFilter;

    private static void OnModelChanged(
        DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (BaseSurfaceView)d;
        if (e.OldValue is BaseDocumentViewModel oldModel)
        {
            oldModel.ResultPublished -= view.OnResultPublished;
            oldModel.PropertyChanged -= view.OnModelPropertyChanged;
            oldModel.QuickFilterFocusRequested -= view.FocusQuickFilter;
            oldModel.RendererOverrideRequested -= view.OnRendererOverrideRequested;
            oldModel.SortCurrentColumnRequested -= view.OnSortCurrentColumnRequested;
            oldModel.EditSelectedPropertyRequested -= view.OnEditSelectedPropertyRequested;
        }
        if (e.NewValue is BaseDocumentViewModel model)
        {
            model.ResultPublished += view.OnResultPublished;
            model.PropertyChanged += view.OnModelPropertyChanged;
            model.QuickFilterFocusRequested += view.FocusQuickFilter;
            model.RendererOverrideRequested += view.OnRendererOverrideRequested;
            model.SortCurrentColumnRequested += view.OnSortCurrentColumnRequested;
            model.EditSelectedPropertyRequested += view.OnEditSelectedPropertyRequested;
            view.RenderAll();
        }
    }

    private void RefreshFromHeader()
    {
        if (Model is not { } model)
        {
            return;
        }
        // The mac shape: Refresh is a FULL reload (close, reopen,
        // views, execute) and announces "Base refreshed."; Retry is
        // the same reload from the failed posture, where the state
        // banners speak for themselves.
        bool wasFailed = model.State == BaseLoadState.Failed;
        model.Load();
        if (!wasFailed)
        {
            model.AnnounceRefreshed();
        }
    }

    private bool OnExternalSort(int columnIndex, bool ascending) =>
        Model?.ApplySortFromGrid(columnIndex, ascending) ?? false;

    private void OnRendererOverrideRequested(BaseRendererOverride mode) =>
        RendererOverride = mode;

    /// <summary>slate.bases.sortByColumn: toggle from the FOCUSED
    /// column — the substrate's Ctrl+Alt+S seam, invoked from the
    /// command/menu route so both speak the same sentence. The clamp
    /// and the toggle read the SAME index (red team round 1: the raw
    /// −1 fed the toggle, so a grid with no current cell sorted
    /// column 0 ascending instead of toggling it).</summary>
    private void OnSortCurrentColumnRequested()
    {
        int column = Math.Max(0, _grid.CurrentColumnIndexForTests());
        _ = _grid.ApplySort(column, AscendingToggleFor(column));
    }

    private bool AscendingToggleFor(int columnIndex) =>
        Model?.SortState is not { } sort
        || sort.ColumnIndex != columnIndex
        || !sort.Ascending;

    /// <summary>slate.bases.editProperty (the mac shape): a list
    /// renderer switches to table first, then the selected column or
    /// the first EDITABLE column begins the edit.</summary>
    private void OnEditSelectedPropertyRequested()
    {
        if (Model is not { Result: { } result } model)
        {
            return;
        }
        if (model.SelectedRow is null)
        {
            model.AnnounceForSurface(new A11yEvent.BasesRowSelectionNeeded());
            return;
        }
        if (_list.Visibility == Visibility.Visible)
        {
            RendererOverride = BaseRendererOverride.Table;
        }
        // The CURRENT column when it is editable, else the first
        // editable column (the documented order — red team round 1:
        // the first cut always took the first).
        int editable = -1;
        int current = _grid.CurrentColumnIndexForTests();
        if (current >= 0
            && current < result.Columns.Length
            && BaseCellEditPolicy.PropertyKey(result.Columns[current]) is not null)
        {
            editable = current;
        }
        else
        {
            for (int index = 0; index < result.Columns.Length; index++)
            {
                if (BaseCellEditPolicy.PropertyKey(result.Columns[index]) is not null)
                {
                    editable = index;
                    break;
                }
            }
        }
        if (editable < 0)
        {
            model.AnnounceForSurface(new A11yEvent.BasesNoEditableProperty());
            return;
        }
        object? row = _grid.CurrentRowForTests()
            ?? (result.Rows.Length > 0 ? FirstBoundRow() : null);
        if (row is not null)
        {
            _ = _grid.BeginEditAt(row, editable);
        }
    }

    private object? FirstBoundRow() => _grid.FirstItemForTests();

    private void OnViewPicked(object sender, SelectionChangedEventArgs e)
    {
        if (_synchronizingPicker
            || Model is not { } model
            || _viewPicker.SelectedIndex < 0
            || _viewPicker.SelectedIndex == model.ActiveViewIndex)
        {
            return;
        }
        int before = model.ActiveViewIndex;
        model.SelectView(_viewPicker.SelectedIndex);
        if (model.ActiveViewIndex != before && model.ActiveViewName is { } name)
        {
            model.AnnounceViewSelected(name);
        }
        else if (model.ActiveViewIndex != _viewPicker.SelectedIndex)
        {
            // The switch was refused (loading/failed): the picker must
            // fall back to reality instead of announcing a switch that
            // did not happen (red team round 1).
            RenderChrome();
        }
    }

    private void OnQuickFilterTextChanged(object sender, TextChangedEventArgs e)
    {
        if (Model is not { } model)
        {
            return;
        }
        model.QuickFilterText = _quickFilter.Text;
        _filterDebounce.Stop();
        _filterDebounce.Start();
    }

    private void OnQuickFilterKeyDown(object sender, KeyEventArgs e)
    {
        // Escape clears ONLY when the field is focused or a filter is
        // active, then returns focus to the content (the mac shape,
        // contract C5).
        if (e.Key != Key.Escape || Model is not { } model)
        {
            return;
        }
        if (_quickFilter.Text.Length == 0 && !model.QuickFilterActive)
        {
            return;
        }
        e.Handled = true;
        _filterDebounce.Stop();
        // Detached around the programmatic set (the RenderChrome
        // pattern): the TextChanged handler restarts the debounce, and
        // Escape would otherwise execute AND announce twice — once
        // here, once 150 ms later (red team round 1).
        _quickFilter.TextChanged -= OnQuickFilterTextChanged;
        _quickFilter.Text = string.Empty;
        _quickFilter.TextChanged += OnQuickFilterTextChanged;
        model.QuickFilterText = string.Empty;
        model.ApplyQuickFilter();
        FocusContent();
    }

    private void FocusQuickFilter()
    {
        _ = _quickFilter.Focus();
        _quickFilter.SelectAll();
    }

    private void FocusContent()
    {
        if (_grid.Visibility == Visibility.Visible)
        {
            _ = _grid.FocusFirstCell();
        }
        else if (_list.Visibility == Visibility.Visible)
        {
            _ = _list.Focus();
        }
    }

    private void OnResultPublished(object? sender, EventArgs e) => RenderAll();

    private void OnModelPropertyChanged(
        object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(BaseDocumentViewModel.State)
            or nameof(BaseDocumentViewModel.StateMessage)
            or nameof(BaseDocumentViewModel.QuickFilterActive))
        {
            RenderChrome();
        }
    }

    private void RenderAll()
    {
        RenderChrome();
        RenderContent();
    }

    private void RenderChrome()
    {
        if (Model is not { } model)
        {
            return;
        }
        _title.Text = model.DisplayName;
        AutomationProperties.SetName(_title, $"Base {model.DisplayName}");

        _synchronizingPicker = true;
        try
        {
            _viewPicker.ItemsSource = model.Views;
            _viewPicker.SelectedIndex =
                model.Views.Count > 0 ? model.ActiveViewIndex : -1;
            _viewPicker.Visibility = model.Views.Count > 1
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
        finally
        {
            _synchronizingPicker = false;
        }

        if (model.Result is { } result)
        {
            // The filtered denominator is the UNFILTERED shown count
            // (mac verbatim); unfiltered shows the total.
            ulong denominator = model.QuickFilterActive
                ? result.UnfilteredShownCount
                : result.TotalCount;
            _countReadout.Text = $"{result.ShownCount} of {denominator}";
        }
        else
        {
            _countReadout.Text = string.Empty;
        }
        AutomationProperties.SetName(
            _countReadout,
            _countReadout.Text.Length > 0 ? $"Results: {_countReadout.Text}" : string.Empty);

        bool interactive = model.State is BaseLoadState.Ready or BaseLoadState.Degraded;
        _quickFilter.IsEnabled = interactive;
        _viewPicker.IsEnabled = interactive;
        // C13's disabled reason, delivered as a STATIC HINT (the
        // TaskStatusPhrase label category — commands gray out through
        // CanExecute; the hint says why, with no announcement).
        string unavailableReason = model.State switch
        {
            BaseLoadState.Loading => "This Base is still opening.",
            BaseLoadState.Failed => "This Base failed to open. Retry reloads it.",
            _ => string.Empty,
        };
        AutomationProperties.SetHelpText(_quickFilter, unavailableReason);
        AutomationProperties.SetHelpText(_viewPicker, unavailableReason);
        if (!string.Equals(_quickFilter.Text, model.QuickFilterText, StringComparison.Ordinal))
        {
            // Transiency: a Load/SelectView cleared the model's filter;
            // the field follows without re-triggering the debounce
            // into a spurious execute.
            _filterDebounce.Stop();
            _quickFilter.TextChanged -= OnQuickFilterTextChanged;
            _quickFilter.Text = model.QuickFilterText;
            _quickFilter.TextChanged += OnQuickFilterTextChanged;
        }

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
        _banners.Visibility =
            _stateBanner.Visibility == Visibility.Visible
            || _warningBanners.Visibility == Visibility.Visible
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    /// <summary>The mac content-shape rules (contract C4): no rows →
    /// empty placeholder; no columns → row-only list REGARDLESS of
    /// renderer mode; else the resolved renderer. Exactly one of
    /// grid/list/empty is in the tree.</summary>
    private void RenderContent()
    {
        if (Model is not { } model)
        {
            return;
        }
        if (model.Result is not { } result || model.State == BaseLoadState.Failed)
        {
            _grid.Visibility = Visibility.Collapsed;
            _list.Visibility = Visibility.Collapsed;
            _emptyState.Visibility = model.State == BaseLoadState.Failed
                ? Visibility.Collapsed
                : model.State == BaseLoadState.Loading
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            return;
        }
        if (result.Rows.Length == 0)
        {
            _grid.Visibility = Visibility.Collapsed;
            _list.Visibility = Visibility.Collapsed;
            _emptyState.Visibility = Visibility.Visible;
            return;
        }
        _emptyState.Visibility = Visibility.Collapsed;
        bool asList = result.Columns.Length == 0 || ResolvedRenderer(model) ==
            BaseRendererOverride.List;
        if (asList)
        {
            _grid.Visibility = Visibility.Collapsed;
            _list.Visibility = Visibility.Visible;
            RenderList(result);
        }
        else
        {
            _list.Visibility = Visibility.Collapsed;
            _grid.Visibility = Visibility.Visible;
            RenderGrid(model, result);
        }
    }

    private BaseRendererOverride ResolvedRenderer(BaseDocumentViewModel model)
    {
        if (_rendererOverride is { } overridden)
        {
            return overridden;
        }
        string? viewType = model.Views.Count > model.ActiveViewIndex
            ? model.Views[model.ActiveViewIndex].ViewType
            : null;
        return string.Equals(viewType, "list", StringComparison.Ordinal)
            ? BaseRendererOverride.List
            : BaseRendererOverride.Table;
    }

    private void RenderGrid(BaseDocumentViewModel model, BasesResultSet result)
    {
        var columns = new List<AccessibleGridColumn>(result.Columns.Length);
        for (int index = 0; index < result.Columns.Length; index++)
        {
            BasesColumn column = result.Columns[index];
            int columnIndex = index;
            bool editable = BaseCellEditPolicy.PropertyKey(column) is not null;
            columns.Add(new AccessibleGridColumn
            {
                Header = column.Label,
                Cell = row => ((BaseGridRowViewModel)row).DisplayAt(columnIndex),
                IsRowHeader = column.Role == ColumnRole.Primary,
                IsExternallySortable = true,
                // The read-only reason as a STATIC hint (contract C7):
                // the same event the refusal announces, rendered once.
                AccessibilityHint = editable
                    ? null
                    : _ => BaseCellEditPolicy.ReadOnlyHint(column),
            });
        }
        var rows = new List<object>(result.Rows.Length);
        foreach (BasesRow row in result.Rows)
        {
            rows.Add(new BaseGridRowViewModel(row));
        }
        if (IsReadOnlySurface)
        {
            _grid.Bind(
                columns,
                rows,
                summary: BaseSummaryFormatter.SummaryText(result, model.QuickFilterActive),
                accessibilityLabel: result.AudioSummary,
                rowAudioDescription: static row =>
                    ((BaseGridRowViewModel)row).AudioDescription);
            _grid.SetSortIndicator(model.SortState);
            _grid.CurrentRowChanged -= OnCurrentRowChanged;
            _grid.CurrentRowChanged += OnCurrentRowChanged;
            ReconcileGridSelection(model, rows);
            return;
        }
        var rowActions = new List<SlateWindows.Grids.AccessibleGridRowAction>
        {
            new()
            {
                Name = "Open",
                Execute = row => RowCommand(row, (m, r) => m.OpenRowFromSurface?.Invoke(r)),
            },
            new()
            {
                Name = "Copy link",
                Execute = row => RowCommand(row, (m, r) => m.CopyLinkFromSurface?.Invoke(r)),
            },
            new()
            {
                Name = "Show backlinks",
                Execute = row => RowCommand(row, (m, r) => m.ShowBacklinksFromSurface?.Invoke(r)),
            },
            new()
            {
                Name = "Edit property",
                Execute = _ => OnEditSelectedPropertyRequested(),
                IsVisible = _ => result.Columns.Any(column =>
                    BaseCellEditPolicy.PropertyKey(column) is not null),
            },
        };
        _grid.Bind(
            columns,
            rows,
            summary: BaseSummaryFormatter.SummaryText(result, model.QuickFilterActive),
            accessibilityLabel: result.AudioSummary,
            rowAudioDescription: static row =>
                ((BaseGridRowViewModel)row).AudioDescription,
            rowActions: rowActions,
            // No exportProducer: export/copy route through the menu
            // commands, which own the C14 scope prompt and compose off
            // the dispatcher — a synchronous producer here could do
            // neither (red team round 1), and nothing subscribes the
            // substrate's ExportProduced in production.
            rowActivated: row => RowCommand(row, (m, r) => m.OpenRowFromSurface?.Invoke(r)));
        _grid.SetSortIndicator(model.SortState);
        _grid.CurrentRowChanged -= OnCurrentRowChanged;
        _grid.CurrentRowChanged += OnCurrentRowChanged;
        // The edit seam re-configures per bind so the closures always
        // hold THIS publish's columns (a stale closure over replaced
        // columns is the dangling-reference class INV-3 forbids).
        _grid.ConfigureEditing(
            editDraft: (row, columnIndex) =>
                columnIndex >= 0
                && columnIndex < result.Columns.Length
                && BaseCellEditPolicy.PropertyKey(result.Columns[columnIndex]) is not null
                    ? BaseCellEditPolicy.DraftText(
                        ((BaseGridRowViewModel)row).Row.Values[columnIndex])
                    : null,
            editCommit: (row, columnIndex, text, navigation) =>
                CommitCellEdit(model, result, row, columnIndex, text, navigation),
            editCancel: () =>
                model.AnnounceForSurface(new A11yEvent.BasesCellEditCanceled()),
            editRefused: (row, columnIndex) =>
            {
                if (columnIndex >= 0 && columnIndex < result.Columns.Length)
                {
                    model.AnnounceForSurface(
                        BaseCellEditPolicy.ReadOnlyEvent(result.Columns[columnIndex]));
                }
            });
        ReconcileGridSelection(model, rows);
    }

    /// <summary>C9 selection preservation by IDENTITY (FilePath,
    /// TaskOrdinal): after a republish the selection follows the same
    /// note-row (fresh payload), and a vanished row drops it. Focus
    /// moves only when the grid already held it — a background funnel
    /// publish must never steal keyboard focus.</summary>
    private void ReconcileGridSelection(
        BaseDocumentViewModel model, IReadOnlyList<object> rows)
    {
        if (model.SelectedRow is not { } selected)
        {
            return;
        }
        BaseGridRowViewModel? match = rows
            .OfType<BaseGridRowViewModel>()
            .FirstOrDefault(candidate =>
                string.Equals(
                    candidate.Row.FilePath, selected.FilePath, StringComparison.Ordinal)
                && candidate.Row.TaskOrdinal == selected.TaskOrdinal);
        if (match is null)
        {
            model.SelectedRow = null;
            return;
        }
        model.SelectedRow = match.Row;
        if (_grid.IsKeyboardFocusWithin)
        {
            _ = _grid.FocusRow(candidate => ReferenceEquals(candidate, match));
        }
    }

    private void OnCurrentRowChanged(object? row)
    {
        if (Model is { } model)
        {
            model.SelectedRow = (row as BaseGridRowViewModel)?.Row;
        }
    }

    /// <summary>The mac commitEdit shape (contract C7): empty draft
    /// deletes the property; a validation refusal announces core's
    /// sentence and RE-ARMS the editor with the user's text intact;
    /// a typed value dispatches through the workspace coordinator
    /// (contract C8) and moves per the committed navigation.</summary>
    private void CommitCellEdit(
        BaseDocumentViewModel model,
        BasesResultSet result,
        object row,
        int columnIndex,
        string text,
        GridEditCommitNavigation navigation)
    {
        if (columnIndex < 0 || columnIndex >= result.Columns.Length)
        {
            return;
        }
        BasesColumn column = result.Columns[columnIndex];
        BasesRow basesRow = ((BaseGridRowViewModel)row).Row;
        if (text.Trim().Length == 0)
        {
            model.ApplyPropertyEdit?.Invoke(basesRow, column, null);
            _grid.MoveCurrentCell(navigation);
            return;
        }
        PropertyValue? value = BaseCellEditPolicy.PropertyValueFor(
            text, column.ValueKind, out A11yEvent? refusal);
        if (value is null)
        {
            if (refusal is not null)
            {
                model.AnnounceForSurface(refusal);
            }
            _ = _grid.BeginEditAt(row, columnIndex, draftOverride: text);
            return;
        }
        model.ApplyPropertyEdit?.Invoke(basesRow, column, value);
        _grid.MoveCurrentCell(navigation);
    }

    /// <summary>Row navigation (the mac list renderer): one item per
    /// row named by core's audio description; groups become header
    /// items carrying the substrate's canonical group heading.</summary>
    private void RenderList(BasesResultSet result)
    {
        var items = new List<object>();
        if (result.Groups.Length > 0)
        {
            foreach (BasesGroup group in result.Groups)
            {
                items.Add(new BaseListHeaderViewModel(
                    AccessibleDataGrid.ComposeGroupHeading(
                        group.Label,
                        (uint)group.RowCount,
                        GroupSummaryText(result, group))));
                ulong end = group.RowStart + group.RowCount;
                for (ulong index = group.RowStart;
                    index < end && index < (ulong)result.Rows.Length;
                    index++)
                {
                    items.Add(new BaseListItemViewModel(result.Rows[index]));
                }
            }
        }
        else
        {
            foreach (BasesRow row in result.Rows)
            {
                items.Add(new BaseListItemViewModel(row));
            }
        }
        _list.ItemsSource = items;
        AutomationProperties.SetName(_list, result.AudioSummary);
        _list.ItemContainerStyle ??= BuildListItemStyle();
        ReconcileListSelection(items);
    }

    /// <summary>C9 selection preservation by IDENTITY (FilePath,
    /// TaskOrdinal): a republish keeps the selection on the same
    /// note-row when it survived and drops it when it did not — a
    /// retained stale row is the dangling-reference class INV-3
    /// forbids.</summary>
    private void ReconcileListSelection(IReadOnlyList<object> items)
    {
        if (Model is not { SelectedRow: { } selected } model)
        {
            return;
        }
        BaseListItemViewModel? match = items
            .OfType<BaseListItemViewModel>()
            .FirstOrDefault(item => item.Row is { } row
                && string.Equals(
                    row.FilePath, selected.FilePath, StringComparison.Ordinal)
                && row.TaskOrdinal == selected.TaskOrdinal);
        if (match is null)
        {
            model.SelectedRow = null;
            _list.SelectedItem = null;
        }
        else
        {
            // Selecting the fresh item republishes the fresh row
            // payload through SelectionChanged.
            _list.SelectedItem = match;
        }
    }

    private void OnListSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Model is { } model
            && _list.SelectedItem is BaseListItemViewModel { Row: { } row })
        {
            model.SelectedRow = row;
        }
    }

    private void OnListDoubleClick(object sender, MouseButtonEventArgs e) =>
        _ = ActivateListRow();

    private void OnListKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && ActivateListRow())
        {
            e.Handled = true;
        }
    }

    private bool ActivateListRow()
    {
        if (IsReadOnlySurface
            || Model is not { } model
            || _list.SelectedItem is not BaseListItemViewModel { Row: { } row })
        {
            return false;
        }
        model.SelectedRow = row;
        model.OpenRowFromSurface?.Invoke(row);
        return true;
    }

    private void RowCommand(
        object row, Action<BaseDocumentViewModel, BasesRow> body)
    {
        if (Model is { } model && row is BaseGridRowViewModel bound)
        {
            model.SelectedRow = bound.Row;
            body(model, bound.Row);
        }
    }

    private static string? GroupSummaryText(BasesResultSet result, BasesGroup group) =>
        group.Summaries.Length == 0
            ? null
            : string.Join(
                ", ",
                group.Summaries.Select(cell =>
                    $"{BaseSummaryFormatter.LabelFor(result, cell.ColumnId)} "
                    + $"{cell.Summary}: "
                    + (cell.Value.Display.Length > 0 ? cell.Value.Display : "empty")));

    private static Style BuildListItemStyle()
    {
        // Group headers are separators, not selectable rows: they stay
        // readable in the tree but never take selection (mac lists skip
        // section rows in Home/End the same way).
        var style = new Style(typeof(ListBoxItem));
        style.Setters.Add(new Setter(
            AutomationProperties.NameProperty,
            new System.Windows.Data.Binding(nameof(BaseListItemViewModel.AccessibleName))));
        var headerTrigger = new DataTrigger
        {
            Binding = new System.Windows.Data.Binding(nameof(BaseListItemViewModel.IsHeader)),
            Value = true,
        };
        headerTrigger.Setters.Add(new Setter(IsEnabledProperty, false));
        style.Triggers.Add(headerTrigger);
        return style;
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

/// <summary>A list row: core's audio description IS the accessible
/// name and the visible text (INV-1).</summary>
internal class BaseListItemViewModel
{
    public BaseListItemViewModel(BasesRow row)
    {
        Row = row;
        AccessibleName = row.AudioDescription;
    }

    protected BaseListItemViewModel(string headerText)
    {
        Row = null;
        AccessibleName = headerText;
    }

    public BasesRow? Row { get; }

    public string AccessibleName { get; }

    public bool IsHeader => Row is null;

    public override string ToString() => AccessibleName;
}

/// <summary>A group heading item — the substrate's canonical
/// GridGroup render (one grammar across hosts and renderers).</summary>
internal sealed class BaseListHeaderViewModel : BaseListItemViewModel
{
    public BaseListHeaderViewModel(string headingText) : base(headingText)
    {
    }
}

/// <summary>One bound grid row: core's <see cref="BasesRow"/>
/// untransformed (INV-1) — cells are <c>BasesValue.Display</c>
/// verbatim.</summary>
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
/// "filtered" prefix while a quick filter is active. Grouped results
/// append the canonical group headings (contract C2: table mode has
/// no interleaved section rows, so grouping surfaces in the summary
/// region — red team round 1 found it silently dropped).</summary>
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
        if (result.Groups.Length > 0)
        {
            string groups = string.Join(
                "; ",
                result.Groups.Select(group =>
                    AccessibleDataGrid.ComposeGroupHeading(
                        group.Label, (uint)group.RowCount, summary: null)));
            body = $"{body} Groups: {groups}";
        }
        return quickFilterActive ? $"Summaries: filtered — {body}" : body;
    }

    internal static string LabelFor(BasesResultSet result, string columnId)
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
