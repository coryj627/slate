// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using uniffi.slate_uniffi;

namespace SlateWindows.Graph;

/// <summary>
/// W6-2 PR A (#746), contracts A-4 and A-11: the graph tab body — the
/// header (the kind label, the mode switcher built from core's vector,
/// the Diagram item present and disabled until PR D), the four load
/// states with the mac's labels, and the table projection. W6-2 PR C
/// (C-5, C-7, C-1): the filter field, the count region and Clear in the
/// header in both modes; the Escape ladder in the tunnelling key handler;
/// the presenter seam the navigator reaches this pane through; rule F's
/// landing delivered onto the quiescent lineage (C-17, Term F1 here — the
/// triggers, the seats and the state host land with the Where-am-I slice).
/// A code-built control, like every sibling surface view in this shell.
/// </summary>
internal sealed class GraphSurfaceView : UserControl, IGraphSurfacePresenter
{
    public static readonly DependencyProperty ModelProperty =
        DependencyProperty.Register(
            nameof(Model),
            typeof(GraphDocumentViewModel),
            typeof(GraphSurfaceView),
            new PropertyMetadata(null, OnModelChanged));

    /// <summary>The mac's labels (contract A-4; view text, not templates).</summary>
    internal const string LoadingText = "Loading graph…";
    internal const string LoadingAccessibleName = "Loading graph.";
    internal const string EmptyText = "No notes match the current filters.";
    internal const string ErrorAccessiblePrefix = "Graph error: ";

    /// <summary>The field's accessibility label and hint — the mac's
    /// (`GraphTableView.swift:147–152`; WPF has no placeholder, so the
    /// placeholder's text is the hint, C-5).</summary>
    internal const string FilterFieldName = "Filter graph by note name";
    internal const string FilterFieldHint = "Filter notes";
    internal const string FilterSummaryPrefix = "Filter results: ";
    internal const string ClearFilterName = "Clear filter";

    private readonly TextBlock _title;
    private readonly TextBox _filterField;
    private readonly TextBlock _filterSummary;
    private readonly Button _clearFilter;
    private readonly StackPanel _switcher;
    private readonly List<RadioButton> _modeChoices = [];
    private readonly TextBlock _stateText;
    private readonly GraphTableView _table;
    private bool _synchronizingFilter;
    private bool _detached;

    public GraphSurfaceView()
    {
        AutomationProperties.SetAutomationId(this, "GraphSurface");
        KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.Local);
        _title = new TextBlock
        {
            Text = "Graph",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 8, 12, 8),
        };
        AutomationProperties.SetHeadingLevel(_title, AutomationHeadingLevel.Level2);

        // C-5: the FIELD — an Edit peer with the mac's label and hint.
        _filterField = new TextBox
        {
            Width = 220,
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center,
            TabIndex = 1,
        };
        AutomationProperties.SetAutomationId(_filterField, "GraphFilterField");
        AutomationProperties.SetName(_filterField, FilterFieldName);
        AutomationProperties.SetHelpText(_filterField, FilterFieldHint);
        _filterField.TextChanged += OnFilterTextChanged;

        // C-5: the COUNT REGION — its own Tab stop, read on demand; a
        // label prefix over core's rendered count (A-7's region shape).
        _filterSummary = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
            Visibility = Visibility.Collapsed,
            Focusable = true,
        };
        KeyboardNavigation.SetIsTabStop(_filterSummary, true);
        KeyboardNavigation.SetTabIndex(_filterSummary, 2);
        _filterSummary.SetResourceReference(TextBlock.ForegroundProperty, "Slate.SecondaryTextBrush");
        AutomationProperties.SetAutomationId(_filterSummary, "GraphFilterSummary");

        // C-5: CLEAR — visible exactly while the RAW needle is non-empty.
        _clearFilter = new Button
        {
            Content = "Clear",
            Margin = new Thickness(0, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
            TabIndex = 3,
        };
        AutomationProperties.SetAutomationId(_clearFilter, "GraphClearFilter");
        AutomationProperties.SetName(_clearFilter, ClearFilterName);
        _clearFilter.Click += (_, _) => Model?.Navigator?.ClearNameQuery();

        _switcher = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(12, 4, 12, 4),
        };
        KeyboardNavigation.SetTabNavigation(_switcher, KeyboardNavigationMode.Once);
        KeyboardNavigation.SetTabIndex(_switcher, 4);
        AutomationProperties.SetName(_switcher, "Graph surface");
        AutomationProperties.SetAutomationId(_switcher, "GraphSurfaceSwitcher");

        _stateText = new TextBlock
        {
            Margin = new Thickness(32),
            HorizontalAlignment = HorizontalAlignment.Center,
            Visibility = Visibility.Collapsed,
            TextWrapping = TextWrapping.Wrap,
        };
        KeyboardNavigation.SetTabIndex(_stateText, 5);
        _stateText.SetResourceReference(TextBlock.ForegroundProperty, "Slate.SecondaryTextBrush");
        AutomationProperties.SetAutomationId(_stateText, "GraphStateText");
        _table = new GraphTableView { TabIndex = 5 };

        var filterRegion = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
        };
        filterRegion.Children.Add(_filterField);
        filterRegion.Children.Add(_filterSummary);
        filterRegion.Children.Add(_clearFilter);

        var header = new DockPanel();
        DockPanel.SetDock(_switcher, Dock.Right);
        header.Children.Add(_switcher);
        header.Children.Add(_title);
        header.Children.Add(filterRegion);

        var layout = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(_stateText, Dock.Top);
        layout.Children.Add(header);
        layout.Children.Add(_stateText);
        layout.Children.Add(_table);
        Content = layout;

        // C-1: attachment on the false→true edge of the keys; detachment on
        // Unloaded — the route a CLOSE takes.
        IsKeyboardFocusWithinChanged += OnKeyboardFocusWithinChanged;
        Unloaded += (_, _) =>
        {
            _detached = true;
            Model?.Navigator?.DetachPresenter(this);
        };
        Loaded += (_, _) =>
        {
            _detached = false;
            TryDeliverFocus();
        };
        IsVisibleChanged += (_, _) => TryDeliverFocus();
    }

    public GraphDocumentViewModel? Model
    {
        get => (GraphDocumentViewModel?)GetValue(ModelProperty);
        set => SetValue(ModelProperty, value);
    }

    internal GraphTableView TableForTests => _table;

    internal TextBlock StateTextForTests => _stateText;

    internal IReadOnlyList<RadioButton> ModeChoicesForTests => _modeChoices;

    internal TextBox FilterFieldForTests => _filterField;

    internal TextBlock FilterSummaryForTests => _filterSummary;

    internal Button ClearFilterForTests => _clearFilter;

    /// <summary>The pane's identity — the tab this surface shows — which
    /// rule F's request is addressed to (Term F1); a bare surface in a
    /// fact is its own owner.</summary>
    private object Owner => DataContext ?? this;

    // --- The presenter seam (contract C-1) ----------------------------------

    public void RequestProjectionFocus() => Model?.RequestFocusLanding(Owner);

    public void FocusFilterField()
    {
        if (IsLive)
        {
            _ = _filterField.Focus();
        }
    }

    /// <summary>The Where-am-I panel's dismissal lands with C-8.</summary>
    public bool DismissTransientRegion() => false;

    public bool ProjectionHasFocus => _table.IsKeyboardFocusWithin || _stateText.IsKeyboardFocused;

    public bool FilterRegionHasKeys =>
        _filterField.IsKeyboardFocusWithin || _filterSummary.IsKeyboardFocused || _clearFilter.IsKeyboardFocusWithin;

    public bool IsLive => !_detached && Model is { IsRetired: false };

    private void OnKeyboardFocusWithinChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true && Model is { } model)
        {
            model.Navigator?.AttachPresenter(this);
        }
    }

    // --- The Escape ladder and the chord scope (contracts C-7, C-11) -------

    /// <summary>The tunnelling handler: the graph's chords are delivered
    /// here and nowhere else, live exactly while the surface has the keys
    /// (rule R2); the navigator's map answers with the exact modifiers.</summary>
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnPreviewKeyDown(e);
        if (e.Handled || Model is not { } model || model.Navigator is not { } navigator)
        {
            return;
        }
        // Alt-modified keys arrive as Key.System carrying the real key.
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (navigator.HandleKey(key, Keyboard.Modifiers, this))
        {
            e.Handled = true;
        }
    }

    private static void OnModelChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var view = (GraphSurfaceView)sender;
        bool wasTheAttachedPane = false;
        if (e.OldValue is GraphDocumentViewModel old)
        {
            old.PropertyChanged -= view.OnModelPropertyChanged;
            old.PublicationInstalled -= view.OnPublicationInstalled;
            old.ViewState.PropertyChanged -= view.OnViewStateChanged;
            // The route a REPLACEMENT takes (IGN-12): detach only when the
            // presenter is this surface.
            wasTheAttachedPane = ReferenceEquals(old.Navigator?.PresenterForTests, view);
            old.Navigator?.DetachPresenter(view);
        }
        view._table.Model = e.NewValue as GraphDocumentViewModel;
        if (e.NewValue is GraphDocumentViewModel model)
        {
            model.PropertyChanged += view.OnModelPropertyChanged;
            // After the table's own rebind (it subscribed first): the
            // landing re-tries once the rows are bound (Term F2's install).
            model.PublicationInstalled += view.OnPublicationInstalled;
            model.ViewState.PropertyChanged += view.OnViewStateChanged;
            view.BuildSwitcher(model);
            view.ApplyState(model.Publication);
            view.RenderFilter(model);
            // A replacement re-attaches when this surface held the keys or
            // was the attached pane (the canvas's rule).
            if (wasTheAttachedPane || view.IsKeyboardFocusWithin)
            {
                model.Navigator?.AttachPresenter(view);
            }
            view.TryDeliverFocus();
        }
        else
        {
            view.RenderFilterEmpty();
        }
    }

    /// <summary>Contract A-11: the mode switcher's items are core's vector
    /// in order; only Table is selectable in PR A.</summary>
    private void BuildSwitcher(GraphDocumentViewModel model)
    {
        _switcher.Children.Clear();
        _modeChoices.Clear();
        foreach (GraphSurfaceModeSpec spec in model.SurfaceModes)
        {
            var choice = new RadioButton
            {
                Content = spec.Title,
                GroupName = "GraphSurfaceMode",
                Margin = new Thickness(0, 0, 8, 0),
                IsChecked = spec.Mode == model.ViewState.Mode,
                IsEnabled = spec.Mode == GraphSurfaceMode.Table,
                Tag = spec.Mode,
            };
            AutomationProperties.SetAutomationId(choice, "GraphMode." + spec.Tag);
            AutomationProperties.SetName(choice, spec.Title);
            _switcher.Children.Add(choice);
            _modeChoices.Add(choice);
        }
    }

    private void OnViewStateChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (Model is not { } model)
        {
            return;
        }
        if (e.PropertyName == nameof(GraphViewState.Mode))
        {
            foreach (RadioButton choice in _modeChoices)
            {
                choice.IsChecked = (GraphSurfaceMode)choice.Tag == model.ViewState.Mode;
            }
        }
        if (e.PropertyName is nameof(GraphViewState.NameQuery) or nameof(GraphViewState.Filter) or nameof(GraphViewState.KindOnly))
        {
            RenderFilter(model);
        }
    }

    private void OnModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (Model is not { } model)
        {
            return;
        }
        if (e.PropertyName == nameof(GraphDocumentViewModel.Publication))
        {
            ApplyState(model.Publication);
        }
        if (e.PropertyName is nameof(GraphDocumentViewModel.Publication)
            or nameof(GraphDocumentViewModel.IsRequestInFlight)
            or nameof(GraphDocumentViewModel.FilterCountText))
        {
            RenderFilter(model);
        }
        if (e.PropertyName is nameof(GraphDocumentViewModel.FocusRequest)
            or nameof(GraphDocumentViewModel.Publication)
            or nameof(GraphDocumentViewModel.IsRequestInFlight))
        {
            TryDeliverFocus();
        }
    }

    private void OnPublicationInstalled(GraphPublicationInstall install) => TryDeliverFocus();

    // --- The filter region (contracts C-5, C-6) -----------------------------

    private void OnFilterTextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_synchronizingFilter)
        {
            Model?.Navigator?.SetNameQuery(_filterField.Text);
        }
    }

    /// <summary>The field follows the state (the canvas's RenderFilter
    /// shape); Clear shows for any raw needle; the count region shows only
    /// while the PUBLISHED query narrows and the publication is CURRENT
    /// (Term Q7).</summary>
    private void RenderFilter(GraphDocumentViewModel model)
    {
        string needle = model.ViewState.NameQuery;
        if (!string.Equals(_filterField.Text, needle, StringComparison.Ordinal))
        {
            _synchronizingFilter = true;
            try
            {
                _filterField.Text = needle;
            }
            finally
            {
                _synchronizingFilter = false;
            }
        }
        _clearFilter.Visibility = needle.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

        GraphPublication publication = model.Publication;
        bool narrows = GraphDocumentViewModel.NeedleNarrows(publication.Query.NameQuery) || publication.Query.KindOnly is not null;
        bool current = !model.IsRequestInFlight
            && publication.Query == new GraphVisibilityQuery(model.ViewState.Filter, model.ViewState.NameQuery, model.ViewState.KindOnly)
            && publication.State is GraphLoadState.Ready or GraphLoadState.Empty;
        if (narrows && current)
        {
            _filterSummary.Text = model.FilterCountText;
            AutomationProperties.SetName(_filterSummary, FilterSummaryPrefix + model.FilterCountText);
            _filterSummary.Visibility = Visibility.Visible;
        }
        else
        {
            _filterSummary.Visibility = Visibility.Collapsed;
        }
    }

    private void RenderFilterEmpty()
    {
        _synchronizingFilter = true;
        try
        {
            _filterField.Text = string.Empty;
        }
        finally
        {
            _synchronizingFilter = false;
        }
        _clearFilter.Visibility = Visibility.Collapsed;
        _filterSummary.Visibility = Visibility.Collapsed;
    }

    // --- Rule F, the landing (contract C-17; Term F1 here) -----------------

    /// <summary>Deliver the pending landing when it is this pane's, the
    /// lineage is quiescent and the publication READY — onto the grid's
    /// current row, silently; the other arms and seats land with C-8.</summary>
    private void TryDeliverFocus()
    {
        if (Model is not { } model || model.FocusRequest is not { } request)
        {
            return;
        }
        if (!ReferenceEquals(request.Owner, Owner) || !IsVisible || model.IsRequestInFlight)
        {
            return;
        }
        if (model.Publication.State != GraphLoadState.Ready)
        {
            return;
        }
        // The grid may have been collapsed under EMPTY or ERROR: realise its
        // containers before the seat (Term F2's container realisation).
        _table.UpdateLayout();
        if (_table.FocusProjection())
        {
            model.CompleteFocus(request);
        }
    }

    /// <summary>Contract A-4: LOADING, ERROR, EMPTY, READY in that
    /// precedence — the label visible, the accessible name the mac's.</summary>
    private void ApplyState(GraphPublication publication)
    {
        switch (publication.State)
        {
            case GraphLoadState.Loading:
                ShowState(LoadingText, LoadingAccessibleName);
                break;
            case GraphLoadState.Error:
                string message = publication.Error ?? string.Empty;
                ShowState(message, ErrorAccessiblePrefix + message);
                break;
            case GraphLoadState.Empty:
                ShowState(EmptyText, EmptyText);
                break;
            default:
                _stateText.Visibility = Visibility.Collapsed;
                _table.Visibility = Visibility.Visible;
                break;
        }
    }

    private void ShowState(string text, string accessibleName)
    {
        _stateText.Text = text;
        AutomationProperties.SetName(_stateText, accessibleName);
        _stateText.Visibility = Visibility.Visible;
        _table.Visibility = Visibility.Collapsed;
    }
}
