// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using uniffi.slate_uniffi;

namespace SlateWindows.Canvas;

/// <summary>
/// W6-1 PR A (#745): the `.canvas` tab body — the mac
/// <c>CanvasContainerView</c> twin. Header (title, the surface switcher,
/// the PR C filter field and mode controls), the t0 §5 state regions
/// (loading / empty onboarding / degraded banner with its focusable
/// warning rows / parse error / retarget-absent), the outline and table
/// projections, and PR C's Where-am-I panel. The visual projection (PR D)
/// lands in the same body slot, visibility-gated so exactly one
/// projection is ever in the UIA tree.
///
/// A code-built control, not a XAML pair (CD-31): every sibling surface
/// view in this shell — Bases, dashboard, history, sync diagnostics,
/// reading — is built the same way.
///
/// W6-1 PR C: this is also the canvas's KEY SURFACE. Every
/// <c>ChordScope.Canvas</c> row is delivered from
/// <see cref="OnPreviewKeyDown"/> into <see cref="CanvasNavigator"/>, and
/// the Esc ladder (t0 §2 M5) is implemented HERE rather than in
/// <c>Window_PreviewKeyDown</c> — so the shell's own Escape keeps working
/// exactly as it does with no canvas open whenever the ladder does not
/// consume (contract C6). The surface implements
/// <see cref="ICanvasSurfacePresenter"/>, which is the whole of what the
/// navigator knows about views.
/// </summary>
internal sealed class CanvasSurfaceView : UserControl, ICanvasSurfacePresenter
{
    public static readonly DependencyProperty ModelProperty =
        DependencyProperty.Register(
            nameof(Model),
            typeof(CanvasDocumentViewModel),
            typeof(CanvasSurfaceView),
            new PropertyMetadata(null, OnModelChanged));

    /// <summary>
    /// The shell's "is a modal overlay open" question — M4's one
    /// keep-alive arm (contract C8).
    /// </summary>
    /// <remarks>
    /// A static seam because this control is built by a XAML template
    /// with no injection point, and defaulted to "no overlay" so a bare
    /// test host behaves like the shell with nothing open.
    /// <c>MainWindow</c> installs the real answer;
    /// <c>TheShellInstallsTheModalOverlayAnswerForModeCancellation</c>
    /// pins that it does, because a default left in place would silently
    /// turn every palette open into a mode cancellation.
    /// </remarks>
    internal static Func<bool> ShellOverlayIsOpen { get; set; } = static () => false;

    private readonly TextBlock _title;
    private readonly AutomationNamedGroupPanel _switcher;
    private readonly RadioButton _outlineChoice;
    private readonly RadioButton _tableChoice;
    private readonly RadioButton _visualChoice;
    private readonly TextBox _filterField;
    private readonly TextBlock _filterSummary;
    private readonly Button _filterClear;
    private readonly Button _modeCommit;
    private readonly Button _modeCancel;
    private readonly TextBlock _stateBanner;
    private readonly TextBlock _degradedBanner;
    private readonly ListBox _warningRows;
    private readonly TextBlock _onboarding;
    private readonly CanvasOutlineView _outline;
    private readonly CanvasTableView _table;
    private readonly Grid _detailRegion;
    private readonly TextBlock _detailHeading;
    private readonly TextBox _detailText;
    private readonly AutomationNamedGroupPanel _whereAmIPanel;
    private readonly TextBox _whereAmIReadback;
    private bool _synchronizingSwitcher;
    private bool _synchronizingFilter;
    private int _filterFocusToken;
    private IInputElement? _whereAmIReturnFocus;
    private Window? _hostWindow;

    public CanvasSurfaceView()
    {
        AutomationProperties.SetAutomationId(this, "CanvasSurface");

        _title = new TextBlock
        {
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetHeadingLevel(_title, AutomationHeadingLevel.Level2);

        _outlineChoice = SurfaceChoice(
            "CanvasShowOutline", CanvasPhrase.OutlineSurfaceLabel, null);
        _tableChoice = SurfaceChoice(
            "CanvasShowTable", CanvasPhrase.TableSurfaceLabel, null);
        _visualChoice = SurfaceChoice(
            "CanvasShowVisual", CanvasPhrase.VisualSurfaceLabel, CanvasPhrase.VisualShipsLater);
        _outlineChoice.Checked += (_, _) => RequestSurface(CanvasSurfaceKind.Outline);
        _tableChoice.Checked += (_, _) => RequestSurface(CanvasSurfaceKind.Table);

        // A named GROUP, not a bare StackPanel (A18, round 8). A plain
        // panel gets no automation peer, so the AutomationId and Name set
        // below reached no client at all and the three choices appeared
        // flattened under the surface — inert a11y properties, which is
        // the class this and the Invoke defect share.
        _switcher = new AutomationNamedGroupPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(12, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        _switcher.Children.Add(_outlineChoice);
        _switcher.Children.Add(_tableChoice);
        _switcher.Children.Add(_visualChoice);
        AutomationProperties.SetAutomationId(_switcher, "CanvasSurfaceSwitcher");
        AutomationProperties.SetName(_switcher, CanvasPhrase.SurfaceSwitcherName);
        // ONE Tab stop for the whole group, arrows within it — the WPF
        // radio-group convention (W6-1 PR B, contract A18). Without it
        // Tab visited all three choices and the surface's own
        // documentation — and PR D's "one focus stop after the surface
        // switcher" — described a keyboard route the code did not have.
        // `Once` also degrades correctly when the CHECKED choice is
        // disabled (a persisted "visual" token before PR D ships the
        // renderer): WPF lands on the first FOCUSABLE child rather than
        // stranding focus on an unreachable one.
        KeyboardNavigation.SetTabNavigation(_switcher, KeyboardNavigationMode.Once);
        // Arrows stay INSIDE the group and wrap, which is the other half
        // of the convention: with the default (Continue) an arrow press
        // walks straight out of the switcher into the projection, and
        // "arrows move within the group" would be the same kind of
        // untrue sentence the Tab claim was.
        KeyboardNavigation.SetDirectionalNavigation(
            _switcher, KeyboardNavigationMode.Cycle);

        // The filter field (t0 §3: the filter's state is READABLE — the
        // field's value plus a result summary element — never
        // announcement-only). An Edit peer with mac's label and hint.
        _filterField = new TextBox
        {
            Width = 220,
            Margin = new Thickness(12, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetAutomationId(_filterField, "CanvasFilterField");
        AutomationProperties.SetName(_filterField, CanvasPhrase.FilterFieldName);
        AutomationProperties.SetHelpText(_filterField, CanvasPhrase.FilterFieldHint);
        _filterField.TextChanged += OnFilterTextChanged;

        _filterSummary = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
            Visibility = Visibility.Collapsed,
            // t0 §3 wants this READ ON DEMAND, so it is its own focus
            // stop rather than a decoration beside the field.
            Focusable = true,
        };
        KeyboardNavigation.SetIsTabStop(_filterSummary, true);
        _filterSummary.SetResourceReference(
            TextBlock.ForegroundProperty, "Slate.SecondaryTextBrush");
        AutomationProperties.SetAutomationId(_filterSummary, "CanvasFilterSummary");

        _filterClear = new Button
        {
            Content = CanvasPhrase.ClearFilterLabel,
            Margin = new Thickness(0, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
        };
        AutomationProperties.SetAutomationId(_filterClear, "CanvasClearFilter");
        AutomationProperties.SetName(_filterClear, CanvasPhrase.ClearFilterName);
        _filterClear.Click += (_, _) => Model?.Navigator.ClearFilter();

        // M6: every mode transition has a VISIBLE control, so Switch
        // Control and Voice Control never depend on the keyboard path.
        _modeCommit = ModeButton(
            "CanvasCommitMode", CanvasPhrase.ModeCommitLabel, CanvasPhrase.ModeCommitName);
        _modeCancel = ModeButton(
            "CanvasCancelMode", CanvasPhrase.ModeCancelLabel, CanvasPhrase.ModeCancelName);
        _modeCommit.Click += (_, _) => Model?.Navigator.CommitMode();
        _modeCancel.Click += (_, _) => Model?.Navigator.CancelMode();

        var header = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(12, 8, 12, 4),
        };
        header.Children.Add(_title);
        header.Children.Add(_switcher);
        header.Children.Add(_filterField);
        header.Children.Add(_filterSummary);
        header.Children.Add(_filterClear);
        header.Children.Add(_modeCommit);
        header.Children.Add(_modeCancel);

        _stateBanner = BannerText("CanvasStateBanner");
        _degradedBanner = BannerText("CanvasDegradedBanner");
        _onboarding = BannerText("CanvasEmptyOnboarding");
        // t0 §3: the onboarding copy is a focusable region, not a
        // decoration a keyboard user cannot reach.
        _onboarding.Focusable = true;
        KeyboardNavigation.SetIsTabStop(_onboarding, true);

        // t0 §5's "focusable detail row in the outline footer listing
        // warnings": a real list, so each preserved item is its own
        // navigable element.
        _warningRows = new ListBox
        {
            Margin = new Thickness(12, 0, 12, 4),
            MaxHeight = 96,
            Visibility = Visibility.Collapsed,
        };
        AutomationProperties.SetAutomationId(_warningRows, "CanvasWarningRows");
        AutomationProperties.SetName(_warningRows, CanvasPhrase.WarningsRegionName);

        var banners = new StackPanel { Margin = new Thickness(12, 0, 12, 4) };
        banners.Children.Add(_stateBanner);
        banners.Children.Add(_degradedBanner);
        banners.Children.Add(_onboarding);

        _outline = new CanvasOutlineView();
        _outline.DetailRequested += FocusDetail;
        // PR B's projection, in the SAME body slot: exactly one of them
        // is ever visible, so exactly one is ever in the UIA tree
        // (spec §1).
        _table = new CanvasTableView { Visibility = Visibility.Collapsed };
        _table.DetailRequested += FocusDetail;
        // Same reason as the outline's: a request for a row the panel
        // had virtualized away is deliverable once containers exist
        // (A14.3).
        _table.ContainersRealized += TryDeliverFocus;

        _detailHeading = new TextBlock
        {
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 4),
        };
        _detailText = new TextBox
        {
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MaxHeight = 180,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        AutomationProperties.SetAutomationId(_detailText, "CanvasCardDetail");
        var detailStack = new StackPanel();
        detailStack.Children.Add(_detailHeading);
        detailStack.Children.Add(_detailText);
        _detailRegion = new Grid
        {
            Margin = new Thickness(12, 4, 12, 8),
            Visibility = Visibility.Collapsed,
        };
        _detailRegion.Children.Add(detailStack);

        // t0 §1.4: the transient, focusable Where-am-I panel — the
        // PULL-based counterpart to the announcement, so a braille user
        // reads the same string at leisure. NOT a `ModalSurface`
        // (contract C11): it takes no keys away from anything, the canvas
        // behind it stays live, and registering it as one would put it
        // through the #1118 chord admission it has no business being in.
        _whereAmIReadback = new TextBox
        {
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MaxHeight = 120,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        AutomationProperties.SetAutomationId(_whereAmIReadback, "CanvasWhereAmIReadback");
        AutomationProperties.SetName(_whereAmIReadback, CanvasPhrase.WhereAmIHeading);
        // PULL, not push (spec §PR C Builds): the panel appears because
        // the user asked, and the ANNOUNCEMENT is what speaks. A live
        // region here would say the same sentence twice.
        AutomationProperties.SetLiveSetting(
            _whereAmIReadback, AutomationLiveSetting.Off);
        var whereAmIHeading = new TextBlock
        {
            Text = CanvasPhrase.WhereAmIHeading,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 4),
        };
        var whereAmIClose = new Button
        {
            Content = CanvasPhrase.WhereAmICloseLabel,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 4, 0, 0),
        };
        AutomationProperties.SetAutomationId(whereAmIClose, "CanvasWhereAmIClose");
        whereAmIClose.Click += (_, _) => CloseWhereAmI();
        _whereAmIPanel = new AutomationNamedGroupPanel
        {
            Margin = new Thickness(12, 4, 12, 8),
            Visibility = Visibility.Collapsed,
        };
        _whereAmIPanel.Children.Add(whereAmIHeading);
        _whereAmIPanel.Children.Add(_whereAmIReadback);
        _whereAmIPanel.Children.Add(whereAmIClose);
        AutomationProperties.SetAutomationId(_whereAmIPanel, "CanvasWhereAmIPanel");
        AutomationProperties.SetName(_whereAmIPanel, CanvasPhrase.WhereAmIHeading);

        // Every condition that can turn a pending request deliverable.
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        IsVisibleChanged += OnIsVisibleChanged;
        // The presenter rebinding this surface from tab A to tab B while
        // the Model stays identical (both panes share one document) is
        // NOT caught by OnModelChanged — the model did not change — so a
        // request addressed to B would strand. DataContext IS the owner
        // key, so a change to it is exactly the moment to re-ask (B2).
        DataContextChanged += (_, _) => TryDeliverPending();
        _outline.ContainersRealized += TryDeliverPending;
        // M4's pane arm, and the navigator's "which surface are the keys
        // coming from" answer, in one subscription.
        IsKeyboardFocusWithinChanged += OnKeyboardFocusWithinChanged;

        var layout = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(banners, Dock.Top);
        DockPanel.SetDock(_warningRows, Dock.Bottom);
        DockPanel.SetDock(_whereAmIPanel, Dock.Bottom);
        DockPanel.SetDock(_detailRegion, Dock.Bottom);
        layout.Children.Add(header);
        layout.Children.Add(banners);
        layout.Children.Add(_warningRows);
        layout.Children.Add(_whereAmIPanel);
        layout.Children.Add(_detailRegion);
        // Both projections share the fill slot; Render() gates them.
        var projections = new Grid();
        projections.Children.Add(_outline);
        projections.Children.Add(_table);
        layout.Children.Add(projections);
        Content = layout;
    }

    public CanvasDocumentViewModel? Model
    {
        get => (CanvasDocumentViewModel?)GetValue(ModelProperty);
        set => SetValue(ModelProperty, value);
    }

    internal CanvasOutlineView OutlineForTests => _outline;

    internal CanvasTableView TableForTests => _table;

    internal TextBox DetailForTests => _detailText;

    internal ListBox WarningRowsForTests => _warningRows;

    internal TextBlock DegradedBannerForTests => _degradedBanner;

    internal TextBlock OnboardingForTests => _onboarding;

    internal TextBlock StateBannerForTests => _stateBanner;

    internal RadioButton OutlineChoiceForTests => _outlineChoice;

    internal RadioButton TableChoiceForTests => _tableChoice;

    internal RadioButton VisualChoiceForTests => _visualChoice;

    internal FrameworkElement SwitcherForTests => _switcher;

    internal TextBox FilterFieldForTests => _filterField;

    internal TextBlock FilterSummaryForTests => _filterSummary;

    internal Button ClearFilterForTests => _filterClear;

    internal Button CommitModeForTests => _modeCommit;

    internal Button CancelModeForTests => _modeCancel;

    internal FrameworkElement WhereAmIPanelForTests => _whereAmIPanel;

    internal TextBox WhereAmIReadbackForTests => _whereAmIReadback;

    // --- ICanvasSurfacePresenter (contract C2) ---------------------------

    public CanvasSurfaceKind Projection =>
        Model is { } model && TableIsTheProjection(model)
            ? CanvasSurfaceKind.Table
            : CanvasSurfaceKind.Outline;

    public bool ProjectionHasFocus =>
        Projection == CanvasSurfaceKind.Table
            ? _table.HasKeyboardFocus
            : _outline.HasKeyboardFocus;

    public bool CanMoveWithinProjection(bool forward) =>
        Projection == CanvasSurfaceKind.Table
            ? _table.CanMoveRow(forward)
            : _outline.CanMoveFocus(forward);

    /// <summary>
    /// Returns whether the row actually TOOK focus — a row that is gone,
    /// filtered out, or unrealizable answers false, and a caller with
    /// nowhere else to put the reader must not treat that as done (m6).
    /// </summary>
    public bool FocusRow(string nodeId) =>
        Projection == CanvasSurfaceKind.Table
            ? _table.DeliverFocus(nodeId)
            : _outline.DeliverFocus(nodeId) is not null;

    public void FocusProjection()
    {
        if (Projection == CanvasSurfaceKind.Table)
        {
            _table.FocusGrid();
        }
        else
        {
            _outline.FocusTree();
        }
    }

    /// <summary>Not a presenter member (see
    /// <see cref="ICanvasSurfacePresenter"/>): Ctrl+F raises the
    /// document's focus token and each surface decides for itself
    /// whether the reader is in IT.</summary>
    /// <summary>Put the reader in the filter field, reporting whether
    /// it took the keys — a request nobody could satisfy must not be
    /// marked satisfied (contract C10/A14).</summary>
    private bool FocusFilterField()
    {
        if (!_filterField.Focus())
        {
            return false;
        }
        _filterField.SelectAll();
        return true;
    }

    /// <summary>
    /// Escape's third rung (contract C6): the canvas's own transient
    /// regions, innermost first, each handing focus back to somewhere the
    /// reader can navigate from.
    /// </summary>
    public bool DismissTransientRegion()
    {
        if (_whereAmIPanel.Visibility == Visibility.Visible)
        {
            CloseWhereAmI();
            return true;
        }
        // The READ-ONLY interim detail (PR A / t2 #362), where Escape
        // closes a view and there is nothing to keep. PR E replaces this
        // region with the real card editor, which t0 §2 M8 carves OUT of
        // the mode stack: Escape COMMITS there and the sheet owns its own
        // key, so it must not become a rung of this ladder. Porting this
        // arm forward would throw away a user's typing (contract C6).
        if (Model is { DetailText: not null } model)
        {
            model.CloseDetail();
            // Back to the row that opened it (WCAG 2.1.2/2.4.3) — on the
            // projection that opened it, which is the one still showing.
            // The FALLBACK is load-bearing and mirrors CloseWhereAmI's: a
            // row can be gone by now (an external edit plus a reload),
            // and a delivery that quietly fails would drop focus on the
            // window root — a keyboard user with nowhere to go, which is
            // the trap this Escape exists to prevent (m6).
            if (model.LastActivatedNode is not { } row || !FocusRow(row))
            {
                FocusProjection();
            }
            return true;
        }
        if (_filterField.IsKeyboardFocusWithin || _filterSummary.IsKeyboardFocusWithin)
        {
            FocusProjection();
            return true;
        }
        return false;
    }

    // --- Key delivery (contract C6) --------------------------------------

    /// <summary>
    /// Every <c>ChordScope.Canvas</c> row, delivered in the TUNNELLING
    /// phase from the surface — never from
    /// <c>MainWindow.Window_PreviewKeyDown</c>.
    /// </summary>
    /// <remarks>
    /// Two things follow from the site. A canvas chord is live exactly
    /// while the canvas surface has focus, which is what rule R2 asks
    /// for; and an Escape the ladder does NOT consume keeps its ordinary
    /// meaning for the shell, so cancel-import and every overlay dismissal
    /// behave with a canvas open exactly as they do without one.
    /// Tunnelling rather than bubbling because the projections' own
    /// controls (a <c>TreeView</c>, a <c>DataGrid</c>, a <c>TextBox</c>)
    /// consume arrows and Escape on the way up — the reading navigator's
    /// recorded lesson, one surface over.
    /// </remarks>
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnPreviewKeyDown(e);
        if (e.Handled || Model is not { } model)
        {
            return;
        }
        // Alt-modified keys arrive as Key.System carrying the real key.
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (EscapeBelongsToTheOpenPanel(key))
        {
            CloseWhereAmI();
            e.Handled = true;
            return;
        }
        if (model.Navigator.HandleKey(key, Keyboard.Modifiers, this))
        {
            e.Handled = true;
        }
    }

    /// <summary>
    /// Escape dismisses an OPEN Where-am-I panel, ahead of the ladder
    /// (contract C6, CD-47).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The key is the panel being OPEN, not the reader standing in it —
    /// mac's <c>.keyboardShortcut(.cancelAction)</c> on the panel's Close
    /// button is WINDOW-scoped, so it resolves at the key-equivalent
    /// phase before the container's ladder however focus is arranged.
    /// Keying on focus instead left a real hole: an open-but-unfocused
    /// panel plus an Escape from the projection destroyed a typed filter
    /// needle AND left the panel sitting there, which is the same defect
    /// this pre-emption exists to close, one focus arrangement over.
    /// </para>
    /// <para>
    /// Focus RESTORE is the part that is locus-dependent, and
    /// <see cref="CloseWhereAmI"/> owns that: the reader is put back only
    /// if they were inside the panel, because moving focus for someone
    /// who was already in the projection would be a jump they did not
    /// ask for.
    /// </para>
    /// </remarks>
    private bool EscapeBelongsToTheOpenPanel(Key key) =>
        key == Key.Escape
        && Keyboard.Modifiers == ModifierKeys.None
        && _whereAmIPanel.Visibility == Visibility.Visible;

    // --- Lifecycle -------------------------------------------------------

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_hostWindow is null && Window.GetWindow(this) is { } window)
        {
            _hostWindow = window;
            _hostWindow.Deactivated += OnWindowDeactivated;
        }
        TryDeliverPending();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_hostWindow is { } window)
        {
            window.Deactivated -= OnWindowDeactivated;
            _hostWindow = null;
        }
        Model?.Navigator.DetachPresenter(this);
    }

    private void OnWindowDeactivated(object? sender, EventArgs e) =>
        Depart(CanvasFocusDeparture.WindowDeactivated);

    private void OnIsVisibleChanged(
        object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is false)
        {
            // The tab body stopped being shown — the workspace moved to
            // another tab, or this one closed. M4's clearest case.
            Depart(CanvasFocusDeparture.TabSwitch);
            return;
        }
        TryDeliverPending();
    }

    private void OnKeyboardFocusWithinChanged(
        object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            // The navigator's palette-invoked verbs move focus on the
            // pane the reader is actually in, so the surface says which
            // one that is as soon as it has the keys.
            Model?.Navigator.AttachPresenter(this);
            // …and this is the moment a palette-driven Ctrl+F becomes
            // deliverable: the palette has given the keys back.
            TryDeliverPending();
            return;
        }
        Depart(ClassifyFocusLoss());
    }

    /// <summary>
    /// Which M4 departure a focus loss IS (contract C8).
    /// </summary>
    /// <remarks>
    /// Two of the three answers keep the mode alive, and both are things
    /// layered OVER the canvas tab rather than places the reader went:
    /// a shell overlay, and an open menu. The menu arm is not a nicety —
    /// opening a top-level menu moves keyboard focus onto its
    /// <c>MenuItem</c>, so without it this shell's own Canvas menu would
    /// cancel the mode the instant it opened and its Commit Mode and
    /// Cancel Mode items would be dead before the pointer reached them
    /// (and PR E/F's context menus, which are the M6 visible controls for
    /// every mode verb, would inherit that).
    /// </remarks>
    private static CanvasFocusDeparture ClassifyFocusLoss()
    {
        if (ShellOverlayIsOpen())
        {
            return CanvasFocusDeparture.ModalOverlay;
        }
        return FocusIsInAMenu()
            ? CanvasFocusDeparture.MenuOpen
            : CanvasFocusDeparture.PaneFocus;
    }

    /// <summary>
    /// Whether the keyboard focus now sits inside an open menu.
    /// </summary>
    /// <remarks>
    /// Walked rather than type-tested on the focused element alone: a
    /// submenu header, a checkable item and a templated item part are all
    /// different elements, and a <c>ContextMenu</c> lives in its own
    /// popup so the VISUAL tree is where its chain continues once the
    /// logical one runs out. <see cref="MenuBase"/> covers both
    /// <c>Menu</c> and <c>ContextMenu</c>, which is exactly the pair this
    /// arm is about.
    /// </remarks>
    private static bool FocusIsInAMenu()
    {
        for (DependencyObject? node = Keyboard.FocusedElement as DependencyObject;
            node is not null;
            node = LogicalTreeHelper.GetParent(node)
                ?? (node is System.Windows.Media.Visual
                    or System.Windows.Media.Media3D.Visual3D
                    ? System.Windows.Media.VisualTreeHelper.GetParent(node)
                    : null))
        {
            if (node is System.Windows.Controls.Primitives.MenuBase)
            {
                return true;
            }
        }
        return false;
    }

    private void Depart(CanvasFocusDeparture departure) =>
        _ = Model?.Modes.HandleFocusDeparture(departure);

    private static Button ModeButton(string automationId, string content, string name)
    {
        var button = new Button
        {
            Content = content,
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
        };
        AutomationProperties.SetAutomationId(button, automationId);
        AutomationProperties.SetName(button, name);
        return button;
    }

    private static RadioButton SurfaceChoice(
        string automationId, string label, string? disabledHint)
    {
        var choice = new RadioButton
        {
            Content = label,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
            GroupName = "CanvasSurface",
            IsEnabled = disabledHint is null,
        };
        AutomationProperties.SetAutomationId(choice, automationId);
        AutomationProperties.SetName(choice, label);
        if (disabledHint is not null)
        {
            AutomationProperties.SetHelpText(choice, disabledHint);
        }
        return choice;
    }

    private static TextBlock BannerText(string automationId)
    {
        var text = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Visibility = Visibility.Collapsed,
        };
        text.SetResourceReference(
            TextBlock.ForegroundProperty, "Slate.SecondaryTextBrush");
        AutomationProperties.SetAutomationId(text, automationId);
        return text;
    }

    private static void OnModelChanged(
        DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (CanvasSurfaceView)d;
        if (e.OldValue is CanvasDocumentViewModel oldModel)
        {
            oldModel.PropertyChanged -= view.OnModelPropertyChanged;
            oldModel.OutlinePublished -= view.OnOutlinePublished;
            oldModel.Selection.PropertyChanged -= view.OnSelectionPropertyChanged;
            oldModel.Modes.PropertyChanged -= view.OnModePropertyChanged;
            oldModel.Navigator.DetachPresenter(view);
        }
        view._outline.Model = e.NewValue as CanvasDocumentViewModel;
        view._table.Model = e.NewValue as CanvasDocumentViewModel;
        if (e.NewValue is CanvasDocumentViewModel model)
        {
            model.PropertyChanged += view.OnModelPropertyChanged;
            model.OutlinePublished += view.OnOutlinePublished;
            model.Selection.PropertyChanged += view.OnSelectionPropertyChanged;
            model.Modes.PropertyChanged += view.OnModePropertyChanged;
            view._filterFocusToken = model.FilterFocusToken;
        }
        view.Render();
        view.TryDeliverFocus();
    }

    private void OnOutlinePublished(object? sender, EventArgs e)
    {
        Render();
        TryDeliverFocus();
    }

    /// <summary>
    /// Deliver a pending focus request if it is ours and everything it
    /// needs now exists (contract A14).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Called from every condition that can change the answer: the model
    /// changing, a publish landing, this view loading, this view
    /// becoming visible, the request itself being raised, and the tree
    /// realizing containers. None of them is "the" moment — that was the
    /// edge-triggered design's mistake — so each simply asks again, and
    /// only a real delivery consumes the request.
    /// </para>
    /// <para>
    /// Every non-Ready state has somewhere to put focus, which is what
    /// lets <c>MainWindow.FocusEditorPane</c> step aside for canvas tabs
    /// entirely: a failure state focuses its (focusable) banner, an
    /// empty canvas focuses its onboarding region, and Loading simply
    /// stays pending until the publish arrives.
    /// </para>
    /// </remarks>
    private void TryDeliverFocus()
    {
        if (Model is not { FocusRequest: { } request } model
            || !ReferenceEquals(request.Owner, DataContext)
            || !IsVisible)
        {
            return;
        }
        bool delivered;
        switch (model.State)
        {
            case CanvasLoadState.Loading:
                // Nothing to land on yet; the publish will call back.
                return;
            case CanvasLoadState.Ready when model.FilteredOutline.Count == 0:
                delivered = _onboarding.IsVisible
                    ? _onboarding.Focus()
                    : _filterField.Focus();
                break;
            case CanvasLoadState.Ready:
                // Whichever projection is SHOWING is the one that can
                // deliver: a row in a collapsed view has no container to
                // realize and no focus to take (A14, PR B's arm).
                delivered = model.FocusLandingNodeFor(request) is { } nodeId
                    && (TableIsTheProjection(model)
                        ? _table.DeliverFocus(nodeId)
                        : _outline.DeliverFocus(nodeId) is not null);
                break;
            default:
                delivered = _stateBanner.Focus();
                break;
        }
        if (delivered)
        {
            model.CompleteFocusLanding(request);
        }
    }

    private void OnModelPropertyChanged(
        object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CanvasDocumentViewModel.State)
            or nameof(CanvasDocumentViewModel.StateMessage)
            or nameof(CanvasDocumentViewModel.Warnings)
            // FilterText is deliberately absent: its setter already
            // raises OutlinePublished (the displayed rows changed), and
            // rendering on both signals would rebuild the projection
            // twice per keystroke.
            or nameof(CanvasDocumentViewModel.DetailText)
            or nameof(CanvasDocumentViewModel.WhereAmIText))
        {
            Render();
            TryDeliverFocus();
        }
        else if (e.PropertyName == nameof(CanvasDocumentViewModel.FocusRequest))
        {
            TryDeliverFocus();
        }
        else if (e.PropertyName == nameof(CanvasDocumentViewModel.FilterFocusToken))
        {
            OnFilterFocusRequested();
        }
    }

    private void OnModePropertyChanged(
        object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CanvasModeController.Active)
            or nameof(CanvasModeController.IsActive)
            or nameof(CanvasModeController.ContainerValue))
        {
            RenderMode();
        }
    }

    private void OnSelectionPropertyChanged(
        object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CanvasSelection.ActiveSurface))
        {
            // The switcher AND the projections: a surface change that
            // moved only the radio button would leave the reader on the
            // old projection while the control claimed the new one.
            Render();
            TryDeliverFocus();
        }
    }

    private void RequestSurface(CanvasSurfaceKind surface)
    {
        if (!_synchronizingSwitcher)
        {
            Model?.ShowSurface(surface);
        }
    }

    // --- Filter ----------------------------------------------------------

    private void OnFilterTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_synchronizingFilter || Model is not { } model)
        {
            return;
        }
        model.FilterText = _filterField.Text;
        // Debounced by the announcer's FILTER coalescing class (t0 §1.5),
        // so a keystroke burst collapses into one count.
        model.Navigator.AnnounceFilterCount();
    }

    /// <summary>
    /// Ctrl+F reached the document. Only the surface the reader is
    /// looking at takes focus: the token is per DOCUMENT, and two panes
    /// on one canvas would otherwise both grab it (the mac Codoki #626
    /// rule, restated for panes).
    /// </summary>
    /// <remarks>
    /// <para>
    /// DURABLE, like the A14 focus request beside it, and for the same
    /// reason. The token used to be acknowledged before eligibility was
    /// even asked, so the PALETTE route — the one a keyboard-only reader
    /// takes when they do not know the chord — consumed it and did
    /// nothing: the palette owns the keys while it is closing, every
    /// surface reads as ineligible, and nothing retried. Ctrl+F worked
    /// and Filter Cards did not, which is the shape §W-D exists to
    /// prevent.
    /// </para>
    /// <para>
    /// So the token is acknowledged only when the field ACTUALLY took
    /// focus, and every condition that can turn a pending request
    /// deliverable re-asks — the same list `TryDeliverFocus` is on, which
    /// is why this rides it rather than growing a second one.
    /// </para>
    /// </remarks>
    /// <summary>Both durable requests, asked together — a focus landing
    /// (A14) and a filter-field focus (C10). Every condition that can
    /// change the answer calls this.</summary>
    private void TryDeliverPending()
    {
        TryDeliverFocus();
        OnFilterFocusRequested();
    }

    private void OnFilterFocusRequested()
    {
        if (Model is not { } model || model.FilterFocusToken == _filterFocusToken)
        {
            return;
        }
        if (!IsVisible || (!IsKeyboardFocusWithin && AnyOtherPaneHasFocus()))
        {
            // Not ours to take YET. The token stays unacknowledged, so
            // the next condition that changes asks again.
            return;
        }
        if (FocusFilterField())
        {
            _filterFocusToken = model.FilterFocusToken;
        }
    }

    /// <summary>Whether some OTHER element in this window holds the keys,
    /// so a surface that is merely visible does not steal them.</summary>
    private bool AnyOtherPaneHasFocus() =>
        _hostWindow is { IsActive: true } window
        && window.IsKeyboardFocusWithin
        && !IsKeyboardFocusWithin;

    private void CloseWhereAmI()
    {
        // Asked BEFORE the panel goes away, because closing it is what
        // takes the focus off it.
        bool readerWasInside = _whereAmIPanel.IsKeyboardFocusWithin;
        if (Model is { } model)
        {
            model.WhereAmIText = null;
        }
        IInputElement? restore = _whereAmIReturnFocus;
        _whereAmIReturnFocus = null;
        if (!readerWasInside)
        {
            // The panel was open but the reader was elsewhere — dismissing
            // it must not MOVE them. Restoring here would be a jump they
            // did not ask for, which is the mirror of the defect the
            // restore exists to prevent (CD-47).
            return;
        }
        // Escape returns focus to the element the reader came from (spec
        // §PR C Builds). A stale or unfocusable token falls back to the
        // projection rather than leaving focus nowhere.
        if (restore is UIElement { IsVisible: true, IsEnabled: true } element
            && element.Focus())
        {
            return;
        }
        FocusProjection();
    }

    private void Render()
    {
        RenderSwitcher();
        RenderMode();
        if (Model is not { } model)
        {
            return;
        }
        _title.Text = model.DisplayName;
        AutomationProperties.SetName(_title, $"Canvas {model.DisplayName}");

        string state = model.State switch
        {
            CanvasLoadState.Loading => CanvasPhrase.Loading,
            CanvasLoadState.Ready => string.Empty,
            _ => model.StateMessage ?? string.Empty,
        };
        SetBanner(_stateBanner, state);
        // t0 §3's error-region discipline: a failure a user cannot
        // reach with the keyboard is as good as unreported. ParseError,
        // Failed and RetargetAbsent make the banner a tab stop; Loading
        // does not (it is transient, and a stop that vanishes under the
        // cursor is its own defect).
        bool errorState = model.State
            is CanvasLoadState.ParseError
            or CanvasLoadState.Failed
            or CanvasLoadState.RetargetAbsent;
        _stateBanner.Focusable = errorState;
        KeyboardNavigation.SetIsTabStop(_stateBanner, errorState);
        AutomationProperties.SetLiveSetting(
            _stateBanner,
            errorState ? AutomationLiveSetting.Assertive : AutomationLiveSetting.Off);
        SetBanner(_degradedBanner, model.DegradedBannerText ?? string.Empty);
        SetBanner(_onboarding, model.EmptyOnboardingText ?? string.Empty);

        RenderFilter(model);
        RenderWhereAmI(model);

        // EVERY warning, not just the skipped entries (spec §PR A
        // behavior 2: "a focusable detail row in the outline footer
        // listing `warnings`"). The BANNER's count stays core's
        // skipped-entry count, because that is the parameter the
        // vocabulary takes; the list is wider on purpose — a dangling
        // connection or an ignored side is a fact about the user's file
        // that no other surface in this PR reports.
        // ...and only under a READY load. A parse error's own message
        // IS its single ParseFailed detail (contract A3), so listing it
        // again below would say the same sentence twice: the states are
        // "a message" or "a banner with its rows", never both.
        string[] warnings = model.State == CanvasLoadState.Ready
            ? model.Warnings.Select(warning => warning.Detail).ToArray()
            : [];
        _warningRows.ItemsSource = warnings;
        _warningRows.Visibility = warnings.Length > 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        _detailHeading.Text = model.DetailTitle ?? string.Empty;
        _detailText.Text = model.DetailText ?? string.Empty;
        AutomationProperties.SetName(
            _detailText,
            model.DetailTitle is { Length: > 0 } title
                ? $"{CanvasPhrase.DetailRegionName}: {title}"
                : CanvasPhrase.DetailRegionName);
        _detailRegion.Visibility = model.DetailText is null
            ? Visibility.Collapsed
            : Visibility.Visible;

        // Exactly one projection in the UIA tree (spec §1): the arm that
        // is not showing is COLLAPSED, which is what keeps it out of the
        // tree entirely rather than merely off screen — and both are
        // hidden while the document has no rows to show, so a
        // parse-error pane is a message, never an empty tree or an empty
        // grid.
        bool ready = model.State == CanvasLoadState.Ready;
        bool table = TableIsTheProjection(model);
        _outline.Visibility = ready && !table ? Visibility.Visible : Visibility.Collapsed;
        _table.Visibility = ready && table ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RenderFilter(CanvasDocumentViewModel model)
    {
        if (!string.Equals(_filterField.Text, model.FilterText, StringComparison.Ordinal))
        {
            _synchronizingFilter = true;
            try
            {
                _filterField.Text = model.FilterText;
            }
            finally
            {
                _synchronizingFilter = false;
            }
        }
        // SYNCHRONOUS (contract C10 interim): the match runs on this
        // frame, so an active needle always has a count to show and an
        // inactive one has nothing to summarise. There is no in-flight
        // frame to render, which is what the async form needed a null for.
        bool filtering = model.FilterActive;
        string summary = filtering ? model.Navigator.FilterSummaryText() : string.Empty;
        _filterSummary.Text = summary;
        // The region's own name carries its value, the interim card
        // detail's idiom: a bare Name would REPLACE the text for a
        // screen reader, and the text is the whole point.
        AutomationProperties.SetName(
            _filterSummary,
            summary.Length > 0
                ? $"{CanvasPhrase.FilterSummaryName}: {summary}"
                : CanvasPhrase.FilterSummaryName);
        _filterSummary.Visibility = filtering ? Visibility.Visible : Visibility.Collapsed;
        // Clear and the summary follow the same condition here, because
        // the match is SYNCHRONOUS (contract C10 interim): there is no
        // frame in which the needle is set and the answer is not. The
        // rationale that made this its own line — Clear must not wait for
        // an answer that arrives later, or the reader is briefly unable
        // to undo what they typed — is the ASYNC form's, and it travels
        // with it. Written out rather than collapsed into `filtering`
        // because that is the line the redesign PR has to un-collapse.
        _filterClear.Visibility = model.FilterActive
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void RenderWhereAmI(CanvasDocumentViewModel model)
    {
        if (model.WhereAmIText is not { Length: > 0 } text)
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
        // Only the pane the reader is in takes focus into the panel, and
        // it remembers where they came from so Escape can put them back.
        _whereAmIReturnFocus = Keyboard.FocusedElement;
        UpdateLayout();
        _ = _whereAmIReadback.Focus();
    }

    private void RenderMode()
    {
        string? value = Model?.Modes.ContainerValue;
        // M3: the active mode is INSPECTABLE from the container's own
        // state, never announcement-only (t0 §3, the braille rule). The
        // surface is a UserControl and therefore peered, so this reaches
        // a client — the inert-property census's jurisdiction.
        AutomationProperties.SetItemStatus(this, value ?? string.Empty);
        Visibility visible = value is null ? Visibility.Collapsed : Visibility.Visible;
        _modeCommit.Visibility = visible;
        _modeCancel.Visibility = visible;
    }

    /// <summary>
    /// Which projection the body shows. Only the table asks for itself:
    /// <c>Visual</c> is not a projection until PR D, and a persisted
    /// <c>"visual"</c> token — which PR A already round-trips — must land
    /// on a real surface rather than an empty pane, so it falls back to
    /// the outline exactly as the absent token does.
    /// </summary>
    private static bool TableIsTheProjection(CanvasDocumentViewModel model) =>
        model.Selection.ActiveSurface == CanvasSurfaceKind.Table;

    private void RenderSwitcher()
    {
        _synchronizingSwitcher = true;
        try
        {
            CanvasSurfaceKind surface =
                Model?.Selection.ActiveSurface ?? CanvasSurfaceKind.Outline;
            _outlineChoice.IsChecked = surface == CanvasSurfaceKind.Outline;
            _tableChoice.IsChecked = surface == CanvasSurfaceKind.Table;
            _visualChoice.IsChecked = surface == CanvasSurfaceKind.Visual;
        }
        finally
        {
            _synchronizingSwitcher = false;
        }
    }

    private static void SetBanner(TextBlock banner, string text)
    {
        banner.Text = text;
        AutomationProperties.SetName(banner, text);
        banner.Visibility = text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void FocusDetail() => _ = _detailText.Focus();
}
