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
/// <c>CanvasContainerView</c> twin. Header (title, the surface
/// switcher), the t0 §5 state regions (loading / empty onboarding /
/// degraded banner with its focusable warning rows / parse error /
/// retarget-absent), and the outline projection. The table (PR B) and
/// visual (PR D) arms land in the same body slot, visibility-gated so
/// exactly one projection is ever in the UIA tree.
///
/// A code-built control, not a XAML pair (CD-31): every sibling surface
/// view in this shell — Bases, dashboard, history, sync diagnostics,
/// reading — is built the same way.
/// </summary>
internal sealed class CanvasSurfaceView : UserControl
{
    public static readonly DependencyProperty ModelProperty =
        DependencyProperty.Register(
            nameof(Model),
            typeof(CanvasDocumentViewModel),
            typeof(CanvasSurfaceView),
            new PropertyMetadata(null, OnModelChanged));

    private readonly TextBlock _title;
    private readonly RadioButton _outlineChoice;
    private readonly RadioButton _tableChoice;
    private readonly RadioButton _visualChoice;
    private readonly TextBlock _stateBanner;
    private readonly TextBlock _degradedBanner;
    private readonly ListBox _warningRows;
    private readonly TextBlock _onboarding;
    private readonly CanvasOutlineView _outline;
    private readonly Grid _detailRegion;
    private readonly TextBlock _detailHeading;
    private readonly TextBox _detailText;
    private bool _synchronizingSwitcher;

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
            "CanvasShowTable", CanvasPhrase.TableSurfaceLabel, CanvasPhrase.TableShipsLater);
        _visualChoice = SurfaceChoice(
            "CanvasShowVisual", CanvasPhrase.VisualSurfaceLabel, CanvasPhrase.VisualShipsLater);
        _outlineChoice.Checked += (_, _) => RequestSurface(CanvasSurfaceKind.Outline);

        // A named GROUP, not a bare StackPanel (A18, round 8). A plain
        // panel gets no automation peer, so the AutomationId and Name set
        // below reached no client at all and the three choices appeared
        // flattened under the surface — inert a11y properties, which is
        // the class this and the Invoke defect share.
        var switcher = new AutomationNamedGroupPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(12, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        switcher.Children.Add(_outlineChoice);
        switcher.Children.Add(_tableChoice);
        switcher.Children.Add(_visualChoice);
        AutomationProperties.SetAutomationId(switcher, "CanvasSurfaceSwitcher");
        AutomationProperties.SetName(switcher, CanvasPhrase.SurfaceSwitcherName);

        var header = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(12, 8, 12, 4),
        };
        header.Children.Add(_title);
        header.Children.Add(switcher);

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
        _detailText.PreviewKeyDown += OnDetailKeyDown;
        var detailStack = new StackPanel();
        detailStack.Children.Add(_detailHeading);
        detailStack.Children.Add(_detailText);
        _detailRegion = new Grid
        {
            Margin = new Thickness(12, 4, 12, 8),
            Visibility = Visibility.Collapsed,
        };
        _detailRegion.Children.Add(detailStack);

        // Every condition that can turn a pending request deliverable.
        Loaded += (_, _) => TryDeliverFocus();
        IsVisibleChanged += (_, _) => TryDeliverFocus();
        // The presenter rebinding this surface from tab A to tab B while
        // the Model stays identical (both panes share one document) is
        // NOT caught by OnModelChanged — the model did not change — so a
        // request addressed to B would strand. DataContext IS the owner
        // key, so a change to it is exactly the moment to re-ask (B2).
        DataContextChanged += (_, _) => TryDeliverFocus();
        _outline.ContainersRealized += TryDeliverFocus;

        var layout = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(banners, Dock.Top);
        DockPanel.SetDock(_warningRows, Dock.Bottom);
        DockPanel.SetDock(_detailRegion, Dock.Bottom);
        layout.Children.Add(header);
        layout.Children.Add(banners);
        layout.Children.Add(_warningRows);
        layout.Children.Add(_detailRegion);
        layout.Children.Add(_outline);
        Content = layout;
    }

    public CanvasDocumentViewModel? Model
    {
        get => (CanvasDocumentViewModel?)GetValue(ModelProperty);
        set => SetValue(ModelProperty, value);
    }

    internal CanvasOutlineView OutlineForTests => _outline;

    internal TextBox DetailForTests => _detailText;

    internal ListBox WarningRowsForTests => _warningRows;

    internal TextBlock DegradedBannerForTests => _degradedBanner;

    internal TextBlock OnboardingForTests => _onboarding;

    internal TextBlock StateBannerForTests => _stateBanner;

    internal RadioButton TableChoiceForTests => _tableChoice;

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
        }
        view._outline.Model = e.NewValue as CanvasDocumentViewModel;
        if (e.NewValue is CanvasDocumentViewModel model)
        {
            model.PropertyChanged += view.OnModelPropertyChanged;
            model.OutlinePublished += view.OnOutlinePublished;
            model.Selection.PropertyChanged += view.OnSelectionPropertyChanged;
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
            case CanvasLoadState.Ready when model.Outline.Count == 0:
                delivered = _onboarding.Focus();
                break;
            case CanvasLoadState.Ready:
                delivered = model.FocusLandingNodeFor(request) is { } nodeId
                    && _outline.DeliverFocus(nodeId) is not null;
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
            or nameof(CanvasDocumentViewModel.DetailText))
        {
            Render();
            TryDeliverFocus();
        }
        else if (e.PropertyName == nameof(CanvasDocumentViewModel.FocusRequest))
        {
            TryDeliverFocus();
        }
    }

    private void OnSelectionPropertyChanged(
        object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CanvasSelection.ActiveSurface))
        {
            RenderSwitcher();
        }
    }

    private void RequestSurface(CanvasSurfaceKind surface)
    {
        if (!_synchronizingSwitcher)
        {
            Model?.ShowSurface(surface);
        }
    }

    private void Render()
    {
        RenderSwitcher();
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

        // Exactly one projection in the UIA tree (spec §1): the outline
        // is hidden while the document has no rows to show, so a
        // parse-error pane is a message, never an empty tree.
        _outline.Visibility = model.State == CanvasLoadState.Ready
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

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

    /// <summary>Escape closes the interim detail and returns focus to
    /// the row that opened it (WCAG 2.1.2 — a read-only region with no
    /// keyboard way out is a trap). PR C's Esc ladder subsumes this
    /// rung.</summary>
    private void OnDetailKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || Model is not { } model)
        {
            return;
        }
        e.Handled = true;
        model.CloseDetail();
        // Back to the row that opened it (WCAG 2.1.2/2.4.3).
        if (model.LastActivatedNode is { } row)
        {
            _ = _outline.DeliverFocus(row);
        }
    }
}
