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
        _outline.ContainersRealized += TryDeliverFocus;
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

    /// <summary>The tab this surface is showing (contract C10/A14): the
    /// key both durable requests are addressed by.</summary>
    public object? Owner => DataContext;

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

    /// <summary>§D D7 / task TD-5: this pane's visual surface answers
    /// a viewport verb. FALSE until TD-6 mounts the renderer — no
    /// visual arm exists to address, so the navigator speaks the
    /// no-pane refusal, which is the honest sentence today and the
    /// rare one after TD-6.</summary>
    public bool ViewportCommand(CanvasViewportVerb verb) => false;

    public bool FocusProjection()
    {
        // The projections are COLLAPSED unless the state renders rows
        // (`Render`'s own condition), so asking them first only works
        // when they are there to ask — and an EMPTY one is not somewhere
        // to put a reader either. Both implementations take focus while
        // holding nothing (`TreeView.Focus`, and the grid's own), so a
        // canvas with no cards used to seat the reader on a silent empty
        // control with the onboarding text unread beside it. Rows first,
        // then whatever this state is actually SHOWING.
        if (Model is { RendersRetainedSnapshot: true, FilteredOutline.Count: > 0 })
        {
            bool seated = Projection == CanvasSurfaceKind.Table
                ? _table.FocusGrid()
                : _outline.FocusTree();
            if (seated)
            {
                return true;
            }
        }
        // Whatever this state DOES show. The empty-canvas onboarding and
        // the failure banner are both focusable exactly when they are the
        // thing on screen (the banner is a tab stop only in the error
        // states — a transient "Opening canvas…" is not somewhere to put
        // a reader).
        if (_onboarding is { IsVisible: true } onboarding && onboarding.Focus())
        {
            return true;
        }
        if (_stateBanner is { IsVisible: true, Focusable: true } banner
            && banner.Focus())
        {
            return true;
        }
        // Ready, with a needle that matched nothing. This arm was once
        // removed as unreachable, on the strength of "every caller is an
        // Escape rung" — false. The LADDER callers cannot present a
        // needle (rung 2 clears it before asking for a seat, and rung 3
        // cannot have the press while one exists), but `CloseWhereAmI`
        // is also reached from the CD-47 PRE-LADDER path, which by
        // design dismisses the panel "leaving an active filter untouched"
        // and therefore runs with a live needle. On a canvas that HAS
        // cards the onboarding region is hidden and `Ready` has no
        // focusable banner, so without this arm the seat falls through
        // to a deferred landing with the panel already collapsed — and
        // whether the reader is rescued then depends on the delivery
        // path's own hold conditions. The field holding the needle is
        // the one control on this surface that can change the answer,
        // and seating them here needs nothing to go right afterwards.
        if (Model is { RendersRetainedSnapshot: true, FilteredOutline.Count: 0 }
            && _filterField is { IsVisible: true } field
            && field.Focus())
        {
            return true;
        }
        // Nothing on this surface can hold the reader right now —
        // `Loading` with no rows is the honest case. Leave a DURABLE,
        // addressed landing rather than dropping them on the window
        // root: the publish that ends the load is one of the conditions
        // that re-asks it (contract A14).
        if (Model is { } model && Owner is { } owner)
        {
            model.RequestFocusLanding(owner);
            // REMEMBERED, because this landing is a RESTORATION and not
            // an instruction. The shell raises landings to put the
            // reader into a tab; this one exists only because the reader
            // was already here with nowhere to sit. If they leave before
            // it can be delivered, it is moot — and delivering it then
            // would drag them back out of wherever they chose to go,
            // which is the class A14 exists to prevent.
            _deferredRestoration = model.FocusRequest;
        }
        return false;
    }

    /// <summary>
    /// Put the reader in the filter field, reporting whether it took the
    /// keys — a request nobody could satisfy must not be marked
    /// satisfied (contract C10/A14).
    /// </summary>
    /// <remarks>Not a presenter member (see
    /// <see cref="ICanvasSurfacePresenter"/>): the verb raises the
    /// document's ADDRESSED request and each surface decides for itself
    /// whether the reader is in IT.</remarks>
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
            _hostWindow.GotKeyboardFocus += OnHostFocusMoved;
            // The other half of the deactivation: a restoration held
            // while the window was away is deliverable again the moment
            // it comes back, and nothing else re-asks on that edge.
            _hostWindow.Activated += OnWindowActivated;
        }
        TryDeliverPending();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // NO owner-departure call here, though the brief asked for one.
        // Every closure path this shell can produce — removing the pane,
        // closing the window — drives `IsVisible` to false first, and
        // that already departs through the classifier with ownership
        // applied. Written and removed: its mutation could not be made
        // to fail, while `AModeDoesNotOutliveThePaneThatOwnsIt`'s Closed
        // and WindowClosed arms assert the INVARIANT, so a future change
        // that stopped raising the visibility edge is still caught.
        // Third belt this wave produced and could not earn.
        if (_hostWindow is { } window)
        {
            window.Deactivated -= OnWindowDeactivated;
            window.Activated -= OnWindowActivated;
            window.GotKeyboardFocus -= OnHostFocusMoved;
            _hostWindow = null;
        }
        Model?.Navigator.DetachPresenter(this);
    }

    private void OnWindowDeactivated(object? sender, EventArgs e) =>
        Depart(CanvasFocusDeparture.WindowDeactivated);

    private void OnWindowActivated(object? sender, EventArgs e)
    {
        // The window came forward without the keys landing in this
        // surface — the reader is back in the shell, so the hold on a
        // deactivation ends here rather than waiting for a focus change
        // that may never come.
        if (_awayBecause == CanvasFocusDeparture.WindowDeactivated)
        {
            _awayBecause = null;
        }
        TryDeliverPending();
    }

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
            // The reader is BACK, whatever they were away in — the
            // overlay closed, the menu closed, the window came forward.
            _awayBecause = null;
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
    private static bool FocusIsInAMenu() =>
        FocusIsInAMenu(Keyboard.FocusedElement);

    /// <summary>
    /// Whether THIS element sits inside a menu — the same walk the
    /// classification uses, exposed so a test can establish its premise
    /// with the production predicate instead of a second copy of it.
    /// </summary>
    /// <remarks>
    /// Widened from private for one reason, and it is worth stating so
    /// nobody widens the next one casually: a canvas fact has to know
    /// whether the keys it just moved landed somewhere this code calls a
    /// menu, and asking with a re-implemented walk is how a test comes to
    /// disagree with production about the thing it is testing. That
    /// happened — on a runner desktop, an arrangement reported "the keys
    /// would not go into a menu" while focus had in fact landed on the
    /// menu's own control. No behaviour is added here and no state is
    /// exposed; it is the existing pure predicate, asked by name.
    /// </remarks>
    internal static bool FocusIsInAMenu(IInputElement? focused)
    {
        for (DependencyObject? node = focused as DependencyObject;
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

    private void Depart(CanvasFocusDeparture departure)
    {
        // The reader LEFT — not a window deactivation, not an overlay or
        // a menu layered over this tab, both of which they are coming
        // back from. A restoration this surface deferred for itself is
        // withdrawn here, so the load that finishes afterwards seats
        // nobody. A shell-raised landing is untouched: it is an
        // instruction to put the reader in this tab, and the departures
        // above are not the reader declining it.
        if (departure is (CanvasFocusDeparture.PaneFocus or CanvasFocusDeparture.TabSwitch)
            && _deferredRestoration is { } deferred
            && Model is { } model)
        {
            model.CompleteFocusLanding(deferred);
            _deferredRestoration = null;
            // Nothing is left for the hold to govern, and leaving a
            // stale departure behind is how the reclassification below
            // would later read a withdrawal as a wait.
            _awayBecause = null;
        }
        else if (_deferredRestoration is not null
            && departure is CanvasFocusDeparture.ModalOverlay
                or CanvasFocusDeparture.MenuOpen
                or CanvasFocusDeparture.WindowDeactivated)
        {
            // The OTHER half, and the one that was missing: the reader is
            // coming back, so the restoration is KEPT — and held, because
            // delivering it now would seat them in a canvas they are not
            // looking at. Taken from the same classification that decides
            // the withdrawal, so the two halves cannot disagree about
            // which departures are which — and spelled POSITIVELY rather
            // than as a bare `else`, which would also fire when the
            // withdrawal condition failed for a reason that is not the
            // classification (a null `Model` during a swap) and record a
            // withdrawal-class departure as a hold.
            _awayBecause = departure;
        }
        // The mode stack is DOCUMENT-shared, so this call needs the same
        // question the window watch needed: a departure from a pane that
        // is not running the mode is not that mode's departure. Hiding a
        // second pane in a split — which never touches the keys — ended a
        // mode the reader was running in the first one, and so did that
        // pane being retargeted or closed. Same class as codex round 5's
        // blocker, one altitude down: the watcher was taught ownership
        // and the classifier it routes through was not.
        //
        // No "or when nobody owns it" arm. That was written as a safety
        // net and was the defect back: `Owner` reads null both for "no
        // mode" and for "a mode nobody owns", so the arm forwarded a
        // peer's departure into a mode unrelated to it. An owner is
        // REQUIRED at entry now, so the second reading no longer exists
        // and the first needs no call — `HandleFocusDeparture` answers
        // false with no active mode anyway.
        if (Model is { Modes: { } modes } && ReferenceEquals(modes.Owner, this))
        {
            _ = modes.HandleFocusDeparture(departure);
        }
    }

    /// <summary>
    /// The reader's keys landed somewhere in this WINDOW. If a held
    /// restoration is waiting on a cause that has now ended, this is
    /// where it is re-decided (contract C6/A14).
    /// </summary>
    /// <remarks>
    /// <para>
    /// A departure is written when THIS surface loses focus, so the move
    /// that ENDS the cause is invisible to it: a reader who opens a menu
    /// and then clicks into another pane raises no second event here. Two
    /// things were then held for the pane's lifetime — a deferred
    /// restoration, never delivered, withdrawn or completed; and an
    /// ACTIVE MODE, kept alive by the same classification (the M4 table
    /// survives `ModalOverlay` and `MenuOpen`) with its Commit and Cancel
    /// controls showing in a pane the reader had left. Starvation, and
    /// the exact mirror of the theft the hold was added to prevent.
    /// </para>
    /// <para>
    /// BOTH constituencies, therefore, and that is the gate: a departure
    /// classification holds a restoration and a mode alive on the same
    /// evidence, so a rule that released only one of them would leave the
    /// other in the shape it was written to end. The first version of
    /// this handler was gated on the restoration alone and the records
    /// claimed the mode with it — codex round 4's M2, and its scoped
    /// review, which is where the difference between the claim and the
    /// code was found.
    /// </para>
    /// <para>
    /// Watching the WINDOW is what makes the destination observable. The
    /// cause still being up is not a decision — arrowing between menu
    /// items raises this repeatedly — so it returns; otherwise the
    /// destination decides, exactly as `Depart` would have decided had it
    /// been able to see the move.
    /// </para>
    /// <para>
    /// There is deliberately NO "the keys came back here" arm. One was
    /// written and removed: the keys returning to this surface is a
    /// false→true `IsKeyboardFocusWithin` transition, which already
    /// clears the hold and re-asks, and the two paths cannot disagree
    /// because only one of them can be first and the other is then a
    /// no-op. Provably redundant, and therefore impossible to pin —
    /// deleting it turned nothing red, which by this branch's own
    /// standard makes it an unearned guard rather than a belt.
    /// </para>
    /// <para>
    /// BOUNDARY, recorded rather than implied: only same-window
    /// destinations are observable here. A reader who leaves the menu for
    /// a different top-level window deactivates this one instead, and the
    /// resolution waits until it comes forward again — at which point the
    /// keys land somewhere and this handler decides. That is a DEFERRED
    /// answer, not the held-forever shape, because the gate below no
    /// longer requires the edge that `OnWindowActivated` clears.
    /// </para>
    /// </remarks>
    private void OnHostFocusMoved(object sender, KeyboardFocusChangedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        // Both constituencies are asked about THIS surface. The landing
        // is surface-local by construction; the MODE is not — one
        // controller serves every pane showing the document, so a
        // sibling reading `IsActive` as its own reclassified departures
        // on behalf of a mode it was not running and cancelled it out
        // from under the reader who was (codex round 5). Ownership is
        // captured at `Enter` and only the owner answers here.
        bool ownsTheMode = Model is { Modes: { IsActive: true } modes }
            && ReferenceEquals(modes.Owner, this);
        if (_deferredRestoration is null && !ownsTheMode)
        {
            return;
        }
        if (ShellOverlayIsOpen() || FocusIsInAMenu(e.NewFocus))
        {
            return;
        }
        if (IsKeyboardFocusWithin)
        {
            return;
        }
        // The cause ended and the keys went ELSEWHERE. Routed through the
        // one classifier rather than acting here, so the restoration and
        // the mode stack hear the same thing from the same place.
        Depart(CanvasFocusDeparture.PaneFocus);
    }

    /// <summary>The A14 landing THIS surface deferred for itself when it
    /// had nowhere to put the reader (contract C6/A14). Null when the
    /// pending landing — if any — came from the shell.</summary>
    private CanvasFocusRequest? _deferredRestoration;

    /// <summary>
    /// Whether a deferred RESTORATION has to keep waiting, because the
    /// reader is not in this surface to receive it (contract A14/C6).
    /// </summary>
    /// <remarks>
    /// One EDGE and three LEVELS, for `TryDeliverFocus`'s own reason:
    /// none of them is "the" moment. The edge is the departure the reader
    /// has not returned from — the three <see cref="Depart"/> retains on,
    /// because they are all things layered over this tab rather than
    /// places the reader went. The levels catch the case with no
    /// departure at all: a restoration RECORDED while an overlay was
    /// already open, a menu already down, or the keys already in another
    /// pane. No departure will ever arrive to withdraw those, and a
    /// level answers without one.
    /// </remarks>
    private bool RestorationMustWait() =>
        _awayBecause is not null
        || ShellOverlayIsOpen()
        || FocusIsInAMenu()
        || KeysAreOutsideThisSurface();

    /// <summary>
    /// Whether the keys are somewhere else in this WINDOW.
    /// </summary>
    /// <remarks>
    /// Deliberately not <see cref="AnyOtherPaneHasFocus"/>, which the
    /// filter-focus delivery uses: that one requires an ACTIVE window,
    /// because a merely-visible surface must not steal keys from a live
    /// pane. Holding a restoration wants the opposite default — an
    /// inactive window is a reader who is not here, and holding is the
    /// safe answer either way, so the activity clause would only make
    /// this miss.
    /// </remarks>
    private bool KeysAreOutsideThisSurface() =>
        _hostWindow is { } window
        && window.IsKeyboardFocusWithin
        && !IsKeyboardFocusWithin;

    /// <summary>The departure the reader has not yet come back from, or
    /// null. Only ever one of the three <see cref="Depart"/> retains
    /// on.</summary>
    /// <remarks>
    /// CLEARED on withdrawal (<see cref="Depart"/>'s first arm), on the
    /// keys returning to this surface, and on the window activating after
    /// a deactivation. What still does not clear it is a MODEL SWAP and
    /// an unload — and that is contained rather than accidental: the hold
    /// is only ever consulted for the request that IS
    /// <see cref="_deferredRestoration"/> (a reference match in
    /// <c>TryDeliverFocus</c>), and that field is nulled on withdrawal
    /// and on delivery, so a departure left over from a document that is
    /// gone governs nothing. Said here so the next reader does not have
    /// to re-derive the containment.
    /// </remarks>
    private CanvasFocusDeparture? _awayBecause;

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
        bool wasTheAttachedPane = false;
        if (e.OldValue is CanvasDocumentViewModel oldModel)
        {
            // BEFORE the detach below, because after it this surface is
            // no longer reachable from the old document and the mode it
            // may be running would have nobody entitled to end it.
            _ = oldModel.Modes.HandleOwnerDeparture(view);
            oldModel.PropertyChanged -= view.OnModelPropertyChanged;
            oldModel.OutlinePublished -= view.OnOutlinePublished;
            oldModel.Selection.PropertyChanged -= view.OnSelectionPropertyChanged;
            oldModel.Modes.PropertyChanged -= view.OnModePropertyChanged;
            wasTheAttachedPane = oldModel.Navigator.DetachPresenter(view);
        }
        view._outline.Model = e.NewValue as CanvasDocumentViewModel;
        view._table.Model = e.NewValue as CanvasDocumentViewModel;
        if (e.NewValue is CanvasDocumentViewModel model)
        {
            model.PropertyChanged += view.OnModelPropertyChanged;
            model.OutlinePublished += view.OnOutlinePublished;
            model.Selection.PropertyChanged += view.OnSelectionPropertyChanged;
            model.Modes.PropertyChanged += view.OnModePropertyChanged;
            // REBIND the affinity the detach above just severed. A model
            // REPLACEMENT is not a fresh pane: an external rename
            // retargets this tab from document X to document Y (CD-32)
            // while the reader is sitting in it, and the new navigator
            // has never seen this surface. Attachment otherwise waits for
            // a false→true keyboard-focus edge or a canvas chord — and a
            // reader whose keys never leave the filter field produces
            // neither, so every palette movement verb afterwards would
            // move the selection and SPEAK the motion while
            // `_presenter?.FocusRow` did nothing at all. The reader and
            // the selection disagreeing IS the broken contract (CD-40),
            // and an ordinary rename plus one palette command was the
            // whole recipe.
            //
            // Two clauses, because the reader can be in this pane in two
            // senses: they own the keys NOW, or they owned them last and
            // something transient (a palette, a menu) is holding them
            // while the replacement lands.
            if (wasTheAttachedPane || view.IsKeyboardFocusWithin)
            {
                model.Navigator.AttachPresenter(view);
            }
            view.TryDeliverFilterFocus();
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
        // A RESTORATION is not an INSTRUCTION (contract A14/C6). The
        // shell raises a landing to PUT the reader into this tab, and it
        // is delivered the moment it can be. This one exists only because
        // the reader was already here with nowhere to sit — so delivering
        // it while they are somewhere else takes the keys off wherever
        // they actually are, which is the class A14 exists to prevent.
        //
        // The WITHDRAWAL side of this distinction already existed, in
        // `Depart`. The delivery side did not, and that made it half a
        // distinction: the departures a reader COMES BACK from (a window
        // deactivation, an overlay, an open menu) deliberately do not
        // withdraw, so they were precisely the states in which a
        // finishing load could seat the reader on top of them. Retained
        // rather than dropped — every condition below ends, and every
        // ending re-asks.
        if (ReferenceEquals(_deferredRestoration, request) && RestorationMustWait())
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
            if (ReferenceEquals(_deferredRestoration, request))
            {
                _deferredRestoration = null;
            }
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
        else if (e.PropertyName == nameof(CanvasDocumentViewModel.FilterFocusRequest))
        {
            TryDeliverFilterFocus();
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

    /// <summary>Both durable requests, asked together — a focus landing
    /// (A14) and a filter-field focus (C10). Every condition that can
    /// change the answer calls this.</summary>
    private void TryDeliverPending()
    {
        TryDeliverFocus();
        TryDeliverFilterFocus();
    }

    /// <summary>
    /// Ctrl+F, or the palette's Filter Cards, reached the document. Only
    /// the surface it was ADDRESSED to takes focus, and only when it can
    /// (contract C10).
    /// </summary>
    /// <remarks>
    /// <para>
    /// DURABLE and ADDRESSED, the A14 focus request's shape, and it took
    /// both halves to be correct. The request used to be acknowledged
    /// before eligibility was even asked, so the PALETTE route — the one
    /// a keyboard-only reader takes when they do not know the chord —
    /// consumed it and did nothing: the palette owns the keys while it
    /// closes, every surface reads as ineligible, and nothing retried.
    /// Ctrl+F worked and Filter Cards did not.
    /// </para>
    /// <para>
    /// Acknowledging only on success fixed that and opened the other
    /// half: two panes share one document, so the pane that could not
    /// satisfy the request kept it pending and pulled the reader into
    /// ITS filter field the next time it saw the keys. The request is
    /// addressed to a tab now and COMPLETED on the document when it
    /// lands, so a peer neither steals it nor holds it.
    /// </para>
    /// <para>
    /// Asked from a list this comment keeps exact, because a list that
    /// is nearly right is how the paragraph this one replaced went
    /// wrong — and because it went nearly-right again: the window
    /// activation below arrived two waves after the list said "three
    /// places". Through `TryDeliverPending`: `Loaded`,
    /// `IsVisibleChanged`, `DataContextChanged`, the host WINDOW
    /// activating, and — the one that matters here — keyboard focus
    /// ARRIVING, which is the moment a closing palette hands the keys
    /// back. Directly: the model changing, and the request property
    /// itself changing. It is deliberately NOT the whole of A14's list:
    /// `Render` never hides the filter field (only the summary and Clear
    /// follow the needle, and the projections follow `ready`), so no
    /// publish, state change or container realization can turn an
    /// unsatisfiable request into a satisfiable one — which is why the
    /// outline's realization asks the A14 landing alone.
    /// </para>
    /// </remarks>
    private void TryDeliverFilterFocus()
    {
        if (Model is not { FilterFocusRequest: { } request } model)
        {
            return;
        }
        // Addressed to this tab, or to nobody — the second case is a
        // document no surface has ever held the keys on, where the first
        // eligible one takes it rather than letting the verb evaporate.
        if (request.Owner is not null && !ReferenceEquals(request.Owner, DataContext))
        {
            return;
        }
        if (!IsVisible || (!IsKeyboardFocusWithin && AnyOtherPaneHasFocus()))
        {
            // Not ours to take YET. The request stays pending, so the
            // next condition that changes asks again.
            return;
        }
        if (FocusFilterField())
        {
            model.CompleteFilterFocus(request);
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
        // ASYNC since task T6: an active needle's summary comes from the
        // applied unit — the previous answer while a match is in flight,
        // an empty region for a first match — and an inactive one has
        // nothing to summarise. The in-flight frame renders what the
        // rows themselves show.
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
    /// <c>Visual</c> is not a projection until §D lands, and a persisted
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
