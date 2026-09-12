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
    internal const string LoadingText = GraphPhrase.LoadingText;
    internal const string LoadingAccessibleName = GraphPhrase.LoadingAccessibleName;
    internal const string EmptyText = GraphPhrase.EmptyText;
    internal const string ErrorAccessiblePrefix = GraphPhrase.ErrorAccessiblePrefix;

    /// <summary>The field's accessibility label and hint — the mac's
    /// (`GraphTableView.swift:147–152`; WPF has no placeholder, so the
    /// placeholder's text is the hint, C-5).</summary>
    internal const string FilterFieldName = GraphPhrase.FilterFieldName;
    internal const string FilterFieldHint = GraphPhrase.FilterFieldHint;
    internal const string FilterSummaryPrefix = GraphPhrase.FilterSummaryPrefix;
    internal const string ClearFilterName = GraphPhrase.ClearFilterName;
    internal const string WhereAmIHeading = GraphPhrase.WhereAmIHeading;
    internal const string WhereAmICloseLabel = GraphPhrase.WhereAmICloseLabel;

    private readonly TextBlock _title;
    private readonly TextBox _filterField;
    private readonly TextBlock _filterSummary;
    private readonly Button _clearFilter;
    private readonly AutomationNamedGroupPanel _switcher;
    private readonly List<RadioButton> _modeChoices = [];
    private readonly TextBlock _stateText;
    private readonly GraphStateHost _stateHost;
    private readonly GraphTableView _table;
    private bool _synchronizingFilter;
    private bool _detached;
    private readonly AutomationNamedGroupPanel _whereAmIPanel;
    private readonly TextBox _whereAmIReadback;
    private readonly Button _whereAmIClose;
    private IInputElement? _whereAmIReturnFocus;
    private GraphNavigator? _navigator;
    private GraphFocusRequest? _deferredRestoration;
    private bool _raisingRestoration;
    private GraphFocusDeparture? _awayBecause;
    private Window? _hostWindow;
    private GraphDocumentViewModel? _observed;

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
            Content = GraphPhrase.ClearLabel,
            Margin = new Thickness(0, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
            TabIndex = 3,
        };
        AutomationProperties.SetAutomationId(_clearFilter, "GraphClearFilter");
        AutomationProperties.SetName(_clearFilter, ClearFilterName);
        _clearFilter.Click += (_, _) => Model?.Navigator?.ClearNameQuery();

        _switcher = new AutomationNamedGroupPanel
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
        // Term F4: the state HOST — a focusable Border with a Group peer named
        // by A-4's accessible name, the landing under EMPTY and ERROR and the
        // provisional seat under LOADING (the leaf's ConnectionsAnchor: a
        // plain Border creates no peer).
        _stateHost = new GraphStateHost
        {
            Child = _stateText,
            Focusable = true,
            Visibility = Visibility.Collapsed,
        };
        KeyboardNavigation.SetTabIndex(_stateHost, 5);
        AutomationProperties.SetAutomationId(_stateHost, "GraphStateHost");
        _table = new GraphTableView { TabIndex = 5 };

        // C-8: the Where-am-I PANEL below the projection — the pull-based
        // twin of the announcement (the canvas's construction): a read-only
        // readback with LiveSetting Off (the announcement speaks; a live
        // region would say it twice) and Close. Not a ModalSurface.
        _whereAmIReadback = new TextBox
        {
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MaxHeight = 120,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        AutomationProperties.SetAutomationId(_whereAmIReadback, "GraphWhereAmIReadback");
        AutomationProperties.SetName(_whereAmIReadback, WhereAmIHeading);
        AutomationProperties.SetLiveSetting(_whereAmIReadback, AutomationLiveSetting.Off);
        var whereAmIHeading = new TextBlock
        {
            Text = WhereAmIHeading,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 4),
        };
        _whereAmIClose = new Button
        {
            Content = WhereAmICloseLabel,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 4, 0, 0),
        };
        AutomationProperties.SetAutomationId(_whereAmIClose, "GraphWhereAmIClose");
        _whereAmIClose.Click += (_, _) => CloseWhereAmI();
        _whereAmIPanel = new AutomationNamedGroupPanel
        {
            Margin = new Thickness(12, 4, 12, 8),
            Visibility = Visibility.Collapsed,
        };
        _whereAmIPanel.Children.Add(whereAmIHeading);
        _whereAmIPanel.Children.Add(_whereAmIReadback);
        _whereAmIPanel.Children.Add(_whereAmIClose);
        AutomationProperties.SetAutomationId(_whereAmIPanel, "GraphWhereAmIPanel");
        AutomationProperties.SetName(_whereAmIPanel, WhereAmIHeading);

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
        DockPanel.SetDock(_stateHost, Dock.Top);
        layout.Children.Add(header);
        layout.Children.Add(_stateHost);
        DockPanel.SetDock(_whereAmIPanel, Dock.Bottom);
        layout.Children.Add(_whereAmIPanel);
        layout.Children.Add(_table);
        Content = layout;

        // C-1: attachment on the false→true edge of the keys; detachment on
        // Unloaded — the route a CLOSE takes.
        IsKeyboardFocusWithinChanged += OnKeyboardFocusWithinChanged;
        Unloaded += (_, _) =>
        {
            _detached = true;
            Model?.Navigator?.DetachPresenter(this);
            ObserveNavigator(null);
            UnhookWindow();
            // The document and the WORKSPACE-scoped view state outlive this
            // element: a surface left subscribed after it leaves the tree is
            // reachable for the workspace's whole life, and a split collapse
            // that re-realises the template adds another one every time —
            // each stale grid rendering every later publication (IPG-12).
            StopObservingModel();
        };
        Loaded += (_, _) =>
        {
            _detached = false;
            // Re-observe what Unloaded dropped, and re-render from the record
            // as it is NOW: nothing reached this view while it was out.
            ObserveModel(Model);
            if (Model is { } model)
            {
                // Nothing reached this view while it was out of the tree:
                // re-render from the record as it is NOW.
                ApplyState(model.Publication);
                RenderFilter(model);
            }
            ObserveNavigator(Model?.Navigator);
            HookWindow();
            TryDeliverFocus();
        };
        // Term F2's triggers: visibility (a false edge is a departure), the
        // owner key changing under a shared document, the grid's containers.
        IsVisibleChanged += OnIsVisibleChanged;
        DataContextChanged += (_, _) => TryDeliverFocus();
        // The grid's containers (Term F2) — and, since IPG-28, the edge that
        // re-asks after the table has bound: the surface may reach the install
        // FIRST after a reload, because WPF raises the parent's Loaded before
        // the child's, so the guard in the READY arm sends it away and this
        // edge brings it back once the rows are actually there.
        _table.ContainersRealized += TryDeliverFocus;
    }

    public GraphDocumentViewModel? Model
    {
        get => (GraphDocumentViewModel?)GetValue(ModelProperty);
        set => SetValue(ModelProperty, value);
    }

    internal GraphTableView TableForTests => _table;

    internal TextBlock StateTextForTests => _stateText;

    internal GraphStateHost StateHostForTests => _stateHost;

    internal GraphFocusRequest? DeferredRestorationForTests => _deferredRestoration;

    internal GraphFocusDeparture? AwayBecauseForTests => _awayBecause;

    internal IReadOnlyList<RadioButton> ModeChoicesForTests => _modeChoices;

    internal TextBox FilterFieldForTests => _filterField;

    internal TextBlock FilterSummaryForTests => _filterSummary;

    internal Button ClearFilterForTests => _clearFilter;

    internal FrameworkElement WhereAmIPanelForTests => _whereAmIPanel;

    internal TextBox WhereAmIReadbackForTests => _whereAmIReadback;

    internal Button WhereAmICloseForTests => _whereAmIClose;

    /// <summary>The pane's identity — the tab this surface shows — which
    /// rule F's request is addressed to (Term F1); a bare surface in a
    /// fact is its own owner.</summary>
    private object Owner => DataContext ?? this;

    // --- The presenter seam (contract C-1) ----------------------------------

    /// <summary>A RESTORATION (Term F3): the surface's own request for a reader
    /// already here — remembered so a departure WITHDRAWS it and a hold keeps
    /// it; the shell's landings are instructions and are never held.</summary>
    public void RequestProjectionFocus()
    {
        if (Model is not { } model)
        {
            return;
        }
        // The raise re-asks synchronously (Term F2's own-change trigger), before
        // the record can be remembered: the flag names it a restoration meanwhile.
        _raisingRestoration = true;
        try
        {
            model.RequestFocusLanding(Owner);
        }
        finally
        {
            _raisingRestoration = false;
        }
        _deferredRestoration = model.FocusRequest;
    }

    public void FocusFilterField()
    {
        if (IsLive)
        {
            _ = _filterField.Focus();
        }
    }

    /// <summary>Escape's rung 0 (C-8): an open Where-am-I panel is dismissed
    /// and the reader re-seated by the panel's rules; false with none open.</summary>
    public bool DismissTransientRegion()
    {
        if (_whereAmIPanel.Visibility != Visibility.Visible)
        {
            return false;
        }
        CloseWhereAmI();
        return true;
    }

    public bool ProjectionHasFocus => _table.IsKeyboardFocusWithin || _stateHost.IsKeyboardFocused;

    public bool FilterRegionHasKeys =>
        _filterField.IsKeyboardFocusWithin || _filterSummary.IsKeyboardFocused || _clearFilter.IsKeyboardFocusWithin;

    public bool IsLive => !_detached && Model is { IsRetired: false };

    private void OnKeyboardFocusWithinChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            Model?.Navigator?.AttachPresenter(this);
            // The reader is BACK, whatever they were away in — the overlay
            // closed, the menu closed, the window came forward: a hold ends
            // and the landing is re-asked (Term F2's hold-ending edge).
            _awayBecause = null;
            TryDeliverFocus();
            return;
        }
        Depart(ClassifyFocusLoss());
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
            view.StopObservingModel(old);
            // The route a REPLACEMENT takes (IGN-12): detach only when the
            // presenter is this surface.
            wasTheAttachedPane = ReferenceEquals(old.Navigator?.PresenterForTests, view);
            old.Navigator?.DetachPresenter(view);
        }
        // Out of the tree this observes NOTHING (IPG-18): a model
        // replacement off-tree re-subscribed the navigator that Unloaded
        // had just dropped, and the new navigator then retained the
        // invisible surface. Loaded re-observes whatever the model is then.
        view.ObserveNavigator(view._detached ? null : (e.NewValue as GraphDocumentViewModel)?.Navigator);
        view._table.Model = e.NewValue as GraphDocumentViewModel;
        if (e.NewValue is GraphDocumentViewModel model)
        {
            view.ObserveModel(model);
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

    /// <summary>The four subscriptions this surface holds on the document
    /// and the workspace's view state, in ONE place (IPG-12): taken when a
    /// model arrives and when the element is loaded, dropped when it is
    /// replaced and when the element leaves the tree. Idempotent — a load
    /// under a model that never left re-asks for nothing. SUBSCRIPTION only:
    /// each caller renders in its own order afterwards, so this cannot
    /// double the work the model-changed route already does (codoki).</summary>
    private void ObserveModel(GraphDocumentViewModel? model)
    {
        // Nothing subscribes while the element is OUT of the tree: a model
        // replacement off-tree re-installed the observers Unloaded had just
        // dropped, and the view was retained by the new document for its
        // whole life (IPG-16). Loaded attaches whatever the model is then.
        if (model is null || _detached || ReferenceEquals(_observed, model))
        {
            return;
        }
        // The model REPLACED another while loaded: the old one goes first.
        StopObservingModel();
        _observed = model;
        model.PropertyChanged += OnModelPropertyChanged;
        // After the table's own rebind (it subscribed first): the landing
        // re-tries once the rows are bound (Term F2's install).
        model.PublicationInstalled += OnPublicationInstalled;
        model.LineageEnded += OnLineageEnded;
        model.ViewState.PropertyChanged += OnViewStateChanged;
    }

    private void StopObservingModel(GraphDocumentViewModel? model = null)
    {
        // The model actually OBSERVED, not whichever one the property holds
        // now: a replacement asks for the old one by name, and an unload
        // after one asks for none (IPG-16).
        GraphDocumentViewModel? observed = model ?? _observed;
        if (observed is null || !ReferenceEquals(observed, _observed))
        {
            return;
        }
        _observed = null;
        observed.PropertyChanged -= OnModelPropertyChanged;
        observed.PublicationInstalled -= OnPublicationInstalled;
        observed.LineageEnded -= OnLineageEnded;
        observed.ViewState.PropertyChanged -= OnViewStateChanged;
    }

    internal bool ObservingForTests => _observed is not null;

    /// <summary>Whether the navigator's own observation stands (IPG-21):
    /// the third subscription an off-tree replacement could re-take.</summary>
    internal bool ObservingNavigatorForTests => _navigator is not null;

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
        // Term F2's trigger here is the REQUEST's own change — a request
        // raised with no load to follow is delivered at once. NOT the
        // publication's property change: the document raises that BEFORE it
        // raises PublicationInstalled, and the table binds the new rows on
        // the INSTALL, so delivering here seated the reader in the grid as it
        // was BEFORE this publication and completed the request against it —
        // then the re-bind dropped that row if the new result excludes it,
        // leaving nobody on a current row (IPG-19). The install's own trigger
        // below runs after the table's re-bind, because the table subscribed
        // first. NOT the lineage edge either: the document clears its token
        // BEFORE the swap, so that edge would show a quiescent OLD record —
        // the terminal kinds arrive through LineageEnded, after it.
        if (e.PropertyName is nameof(GraphDocumentViewModel.FocusRequest))
        {
            TryDeliverFocus();
        }
    }

    private void OnPublicationInstalled(GraphPublicationInstall install) => TryDeliverFocus();

    // --- The Where-am-I panel (contract C-8) -----------------------------------

    /// <summary>The navigator's WhereAmIText drives the panel: one
    /// subscription per navigator, swapped with the model and dropped on
    /// Unloaded.</summary>
    private void ObserveNavigator(GraphNavigator? navigator)
    {
        if (ReferenceEquals(_navigator, navigator))
        {
            return;
        }
        if (_navigator is not null)
        {
            _navigator.PropertyChanged -= OnNavigatorPropertyChanged;
        }
        _navigator = navigator;
        if (navigator is not null)
        {
            navigator.PropertyChanged += OnNavigatorPropertyChanged;
        }
        RenderWhereAmI();
    }

    private void OnNavigatorPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GraphNavigator.WhereAmIText))
        {
            RenderWhereAmI();
        }
    }

    /// <summary>The canvas's RenderWhereAmI: the text shown; on OPENING only
    /// the pane the reader is in takes focus into the readback, remembering
    /// where they came from so Close and Escape can put them back.</summary>
    private void RenderWhereAmI()
    {
        if (_navigator?.WhereAmIText is not { Length: > 0 } text)
        {
            _whereAmIPanel.Visibility = Visibility.Collapsed;
            _whereAmIReadback.Text = string.Empty;
            return;
        }
        bool opening = _whereAmIPanel.Visibility != Visibility.Visible;
        _whereAmIReadback.Text = text;
        _whereAmIPanel.Visibility = Visibility.Visible;
        if (!opening || !IsKeyboardFocusWithin)
        {
            return;
        }
        _whereAmIReturnFocus = Keyboard.FocusedElement;
        UpdateLayout();
        _ = _whereAmIReadback.Focus();
    }

    /// <summary>The canvas's CloseWhereAmI: the text cleared on the navigator
    /// (every pane's panel collapses); the reader restored to the element
    /// they came from only when they were INSIDE the panel — else through
    /// rule F's landing — and a reader elsewhere is not moved.</summary>
    private void CloseWhereAmI()
    {
        bool readerWasInside = _whereAmIPanel.IsKeyboardFocusWithin;
        _navigator?.CloseWhereAmI();
        IInputElement? restore = _whereAmIReturnFocus;
        _whereAmIReturnFocus = null;
        if (!readerWasInside)
        {
            return;
        }
        if (restore is UIElement { IsVisible: true, IsEnabled: true } element && element.Focus())
        {
            return;
        }
        RequestProjectionFocus();
    }

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

    // --- Rule F: the landing (contract C-17) ----------------------------------

    /// <summary>Deliver the pending landing (Terms F2–F5): only when the
    /// request is this pane's, the surface visible and the graph tab
    /// EFFECTIVE; a held restoration waits; with a token in flight a SHELL
    /// request takes a provisional seat (the state host under LOADING with
    /// nothing held, the visible surface otherwise) and stays pending, a
    /// presenter's takes none; quiescent, READY seats the grid's current row
    /// (else the first) and EMPTY or ERROR the state host — silently — and
    /// completes the request; a quiescent LOADING has nothing to land on.</summary>
    private void TryDeliverFocus()
    {
        if (Model is not { FocusRequest: { } request } model
            || !ReferenceEquals(request.Owner, Owner)
            || !IsVisible
            || !model.IsEffective)
        {
            return;
        }
        bool restoration = _raisingRestoration || ReferenceEquals(_deferredRestoration, request);
        if (restoration && RestorationMustWait())
        {
            return;
        }
        GraphPublication publication = model.Publication;
        if (model.IsRequestInFlight)
        {
            // Term F3's provisional seats, for a SHELL route's request alone —
            // the reader must land somewhere; a presenter's reader is already
            // in the surface. The request stays pending: the terminal
            // delivery re-seats.
            if (restoration)
            {
                return;
            }
            if (publication.State is GraphLoadState.Ready && publication.HoldsSnapshot)
            {
                _table.UpdateLayout();
                _ = _table.FocusProjection();
            }
            else
            {
                _ = _stateHost.Focus();
            }
            return;
        }
        bool delivered;
        switch (publication.State)
        {
            case GraphLoadState.Ready:
                if (!ReferenceEquals(_table.BoundPublication, publication))
                {
                    // The grid still holds the PREVIOUS record: seating now
                    // would land the reader on a row this publication may not
                    // contain and complete the request against it (IPG-19,
                    // IPG-28). The table's bind edge re-asks.
                    return;
                }
                // The grid may have been collapsed under EMPTY or ERROR: realise
                // its containers before the seat (Term F2).
                _table.UpdateLayout();
                delivered = _table.FocusProjection();
                break;
            case GraphLoadState.Empty:
            case GraphLoadState.Error:
                delivered = _stateHost.Focus();
                break;
            default:
                // Quiescent LOADING: nothing to land on; the transition's load
                // will end in a terminal state that re-asks.
                return;
        }
        if (delivered)
        {
            model.CompleteFocus(request);
            if (restoration)
            {
                _deferredRestoration = null;
            }
        }
    }

    /// <summary>Term F3's three deliveries: an install and a pair failure
    /// re-ask (the new publication, the ERROR host); a rows-only failure and
    /// a rejection WITHDRAW the pending request — the old publication stands
    /// and is not called current; the reader stays where the keys are.</summary>
    private void OnLineageEnded(GraphLineageEnd end)
    {
        switch (end)
        {
            case GraphLineageEnd.Install:
            case GraphLineageEnd.PairFailure:
                TryDeliverFocus();
                break;
            default:
                WithdrawPending();
                break;
        }
    }

    private void WithdrawPending()
    {
        if (Model is { FocusRequest: { } request } model && ReferenceEquals(request.Owner, Owner))
        {
            model.CompleteFocus(request);
            if (ReferenceEquals(_deferredRestoration, request))
            {
                _deferredRestoration = null;
            }
        }
    }

    // --- Term F2: the departure and the hold (the canvas's Depart) -------------

    private void HookWindow()
    {
        if (_hostWindow is null && Window.GetWindow(this) is { } window)
        {
            _hostWindow = window;
            window.Deactivated += OnWindowDeactivated;
            window.Activated += OnWindowActivated;
            // The third hold-ending edge (Term F2, IPG-11): the keys landing
            // anywhere in this window. Without it a menu or overlay hold that
            // ended by the reader choosing ANOTHER PANE was never observed —
            // the graph's own focus was already false and the window never
            // deactivated — so the restoration stood until some later
            // activation delivered it, stealing the keys from the pane the
            // reader had chosen. The canvas's `:875-903`.
            window.GotKeyboardFocus += OnHostFocusMoved;
        }
    }

    private void UnhookWindow()
    {
        if (_hostWindow is { } window)
        {
            window.Deactivated -= OnWindowDeactivated;
            window.Activated -= OnWindowActivated;
            window.GotKeyboardFocus -= OnHostFocusMoved;
            _hostWindow = null;
        }
    }

    private void OnWindowDeactivated(object? sender, EventArgs e) => Depart(GraphFocusDeparture.WindowDeactivated);

    /// <summary>The window came forward without the keys landing here: the
    /// hold on a deactivation ends and the landing is re-asked (IGP-9).</summary>
    private void OnWindowActivated(object? sender, EventArgs e)
    {
        if (_awayBecause == GraphFocusDeparture.WindowDeactivated)
        {
            _awayBecause = null;
        }
        TryDeliverFocus();
    }

    /// <summary>The keys landed somewhere in this window (Term F2's third
    /// hold-ending edge): with a restoration held and the overlay and the
    /// menu gone, focus outside this surface is the reader CHOOSING another
    /// pane — the classifier withdraws it, exactly as a direct pane change
    /// would (the canvas's <c>OnHostFocusMoved</c>).</summary>
    private void OnHostFocusMoved(object sender, KeyboardFocusChangedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (_deferredRestoration is null)
        {
            return;
        }
        if (Canvas.CanvasSurfaceView.ShellOverlayIsOpen()
            || Canvas.CanvasSurfaceView.FocusIsInAMenu(e.NewFocus))
        {
            // Still layered OVER the tab: the hold stands.
            return;
        }
        if (IsKeyboardFocusWithin)
        {
            // Back here: the delivery is TryDeliverFocus's, through the
            // keyboard-focus-within edge that already re-asks.
            return;
        }
        Depart(GraphFocusDeparture.PaneFocus);
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is false)
        {
            // The tab body stopped being shown: a tab switch, or the close.
            Depart(GraphFocusDeparture.TabSwitch);
            return;
        }
        TryDeliverFocus();
    }

    /// <summary>Which departure a focus loss IS: a shell overlay or an open
    /// menu is layered OVER the tab (the reader comes back); anything else
    /// is the reader leaving for another pane.</summary>
    private static GraphFocusDeparture ClassifyFocusLoss()
    {
        if (Canvas.CanvasSurfaceView.ShellOverlayIsOpen())
        {
            return GraphFocusDeparture.ModalOverlay;
        }
        return Canvas.CanvasSurfaceView.FocusIsInAMenu(Keyboard.FocusedElement)
            ? GraphFocusDeparture.MenuOpen
            : GraphFocusDeparture.PaneFocus;
    }

    /// <summary>The reader LEFT (a pane change, a tab switch): a restoration
    /// this surface deferred for itself is WITHDRAWN, so a load finishing
    /// afterwards seats nobody; behind an overlay, a menu or a deactivated
    /// window the restoration is KEPT and HELD. A shell-raised landing is
    /// untouched: an instruction to put the reader in this tab.</summary>
    private void Depart(GraphFocusDeparture departure)
    {
        if (departure is GraphFocusDeparture.PaneFocus or GraphFocusDeparture.TabSwitch)
        {
            if (_deferredRestoration is { } deferred && Model is { } model)
            {
                model.CompleteFocus(deferred);
                _deferredRestoration = null;
                _awayBecause = null;
            }
            return;
        }
        if (_deferredRestoration is not null)
        {
            _awayBecause = departure;
        }
    }

    /// <summary>Whether a deferred restoration must keep waiting — the reader
    /// is not here to receive it: one edge (the departure not returned from)
    /// and three levels (an overlay already open, a menu already down, the
    /// keys already in another pane).</summary>
    private bool RestorationMustWait() =>
        _awayBecause is not null
        || Canvas.CanvasSurfaceView.ShellOverlayIsOpen()
        || Canvas.CanvasSurfaceView.FocusIsInAMenu(Keyboard.FocusedElement)
        || KeysAreOutsideThisSurface();

    private bool KeysAreOutsideThisSurface() =>
        _hostWindow is { } window
        && window.IsKeyboardFocusWithin
        && !IsKeyboardFocusWithin;

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
                _stateHost.Visibility = Visibility.Collapsed;
                _table.Visibility = Visibility.Visible;
                break;
        }
    }

    private void ShowState(string text, string accessibleName)
    {
        _stateText.Text = text;
        AutomationProperties.SetName(_stateText, accessibleName);
        AutomationProperties.SetName(_stateHost, accessibleName);
        _stateText.Visibility = Visibility.Visible;
        _stateHost.Visibility = Visibility.Visible;
        _table.Visibility = Visibility.Collapsed;
    }
}

/// <summary>Term F2's departures: two the reader LEAVES by (a restoration
/// withdrawn) and three layered OVER the tab (a restoration held).</summary>
internal enum GraphFocusDeparture
{
    PaneFocus,
    TabSwitch,
    ModalOverlay,
    MenuOpen,
    WindowDeactivated,
}

/// <summary>Term F4's state host: the landing under EMPTY and ERROR and the
/// provisional seat under LOADING — focusable and, unlike the Border it is,
/// projected to UI Automation as a Group with its id, the state's accessible
/// name and its keyboard focus (the leaf's ConnectionsAnchor, IPC-1).</summary>
internal sealed class GraphStateHost : Border
{
    protected override System.Windows.Automation.Peers.AutomationPeer OnCreateAutomationPeer() =>
        new GraphStateHostAutomationPeer(this);
}

internal sealed class GraphStateHostAutomationPeer : System.Windows.Automation.Peers.FrameworkElementAutomationPeer
{
    public GraphStateHostAutomationPeer(GraphStateHost owner)
        : base(owner)
    {
    }

    protected override System.Windows.Automation.Peers.AutomationControlType GetAutomationControlTypeCore() =>
        System.Windows.Automation.Peers.AutomationControlType.Group;

    protected override string GetClassNameCore() => nameof(GraphStateHost);

    protected override bool IsControlElementCore() => true;

    protected override bool IsContentElementCore() => true;

    protected override bool IsKeyboardFocusableCore() => Owner is UIElement { Focusable: true, IsEnabled: true, IsVisible: true };
}
