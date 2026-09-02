// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Media;
using uniffi.slate_uniffi;

namespace SlateWindows.Canvas;

/// <summary>
/// The visual projection (§D TD-6): a FrameworkElement drawing the
/// installed presentation state — DrawingVisual per materialized card,
/// the edge layer, and the screen-space selection ring — with the
/// engine as its ONLY data source (D1: peers and pixels read one
/// installed state; no live authority is consulted in a draw pass).
/// DD-1: DrawingContext, no WebView, no SVG.
/// </summary>
internal sealed class CanvasRendererView : FrameworkElement
{
    private readonly CanvasPresentationEngine _engine;
    private readonly CanvasTextScaleService _textScale;
    private readonly VisualCollection _visuals;
    private readonly DrawingVisual _cards;
    private readonly DrawingVisual _edges;
    private readonly DrawingVisual _ring;
    private CanvasDocumentViewModel? _model;

    public CanvasRendererView()
    {
        Focusable = true;
        System.Windows.Automation.AutomationProperties.SetAutomationId(
            this, "CanvasVisualBoard");
        _engine = new CanvasPresentationEngine();
        _textScale = new CanvasTextScaleService();
        _textScale.Changed += () => _engine.CommitTextScaleRevision(_textScale.Revision);
        _engine.StateInstalled += OnStateInstalled;
        _visuals = new VisualCollection(this);
        _edges = new DrawingVisual();
        _cards = new DrawingVisual();
        _ring = new DrawingVisual();
        _ = _visuals.Add(_edges);
        _ = _visuals.Add(_cards);
        _ = _visuals.Add(_ring);
        SizeChanged += (_, e) => _engine.CommitViewport(
            v => v.WithViewSize(e.NewSize.Width, e.NewSize.Height));
        _tooltipText = new System.Windows.Controls.TextBlock
        {
            Padding = new Thickness(6, 4, 6, 4),
            MaxWidth = 360,
            TextWrapping = TextWrapping.Wrap,
        };
        var tooltipBorder = new System.Windows.Controls.Border
        {
            Child = _tooltipText,
            BorderThickness = new Thickness(1),
        };
        _tooltip = new System.Windows.Controls.Primitives.Popup
        {
            Child = tooltipBorder,
            PlacementTarget = this,
            Placement = System.Windows.Controls.Primitives.PlacementMode.Relative,
            StaysOpen = true,
        };
        // HOVERABLE (1.4.13): the pointer travelling ONTO the tooltip
        // is itself a trigger, so arriving there never closes it.
        tooltipBorder.MouseEnter += (_, _) => _pointerOnTooltip = true;
        tooltipBorder.MouseLeave += (_, _) =>
        {
            _pointerOnTooltip = false;
            UpdateTooltip();
        };
    }

    /// <summary>The pane's document. Attach subscribes the engine to
    /// the post-apply notification; detach unsubscribes and disposes
    /// nothing shared — the engine and the text-scale service belong
    /// to THIS view and die with it.</summary>
    internal CanvasDocumentViewModel? Model
    {
        get => _model;
        set
        {
            if (ReferenceEquals(_model, value))
            {
                return;
            }
            if (_model is { } old)
            {
                old.PublicationApplied -= _engine.OnPublicationApplied;
                old.ModeVisibleChanged -= OnModeVisibleChanged;
            }
            _model = value;
            if (value is not null)
            {
                value.PublicationApplied += _engine.OnPublicationApplied;
                value.ModeVisibleChanged += OnModeVisibleChanged;
                _engine.OnPublicationApplied(
                    value.AppliedPublication ?? CanvasPublication.Seed());
                _engine.CommitTransient(value.Transient);
            }
        }
    }

    /// <summary>§F TF-5 (F10): the ONE aggregate observable's
    /// consumer — every mode-visible transition lands here once and
    /// becomes one derived install: pixels, edges, ring, hit shapes
    /// and a11y frames move together or not at all.</summary>
    private void OnModeVisibleChanged() => _engine.CommitTransient(_model?.Transient);

    /// <summary>This view's engine — the surface view routes
    /// ViewportCommand here (D7's structural addressing: the peer or
    /// verb acts on the pane it belongs to).</summary>
    internal CanvasPresentationEngine Engine => _engine;

    protected override int VisualChildrenCount => _visuals.Count;

    protected override Visual GetVisualChild(int index) => _visuals[index];

    /// <summary>D1's consumer side: an install redraws EVERYTHING from
    /// the new state — pixels, hit shapes and the ring agree in one
    /// pass, and DD-7 makes every transform a state-jump, so no
    /// intermediate frame exists to disagree.</summary>
    private void OnStateInstalled(
        CanvasPresentationState? was, CanvasPresentationState state)
    {
        _ = was;
        DrawCards(state);
        DrawEdges(state);
        DrawRing(state);
        // The tooltip revalidates on EVERY install: the selection
        // trigger, the truncation set and the content's validity all
        // derive from the state that just landed (ID-9's
        // becomes-invalid arm).
        UpdateTooltip();
    }

    private void DrawCards(CanvasPresentationState state)
    {
        using DrawingContext context = _cards.RenderOpen();
        _truncated.Clear();
        CanvasPopulation? population = state.Source.Loaded?.Population;
        if (population is null)
        {
            return;
        }
        double zoom = state.Viewport.Zoom;
        double panX = state.Viewport.PanX;
        double panY = state.Viewport.PanY;
        double fontSize = 12.0 * _textScale.Factor * zoom;
        var matched = MatchedIds(state);
        foreach (System.Collections.Generic.KeyValuePair<CanvasPeerKey, CanvasPeerPlacement>
            entry in state.Topology.Placements)
        {
            if (entry.Key.IsEdge
                || entry.Value.Cell != CanvasPeerCell.Materialized
                || !population.SceneByNode.TryGetValue(entry.Key.Id, out CanvasSceneNode? node))
            {
                continue;
            }
            var rect = new Rect(
                (entry.Value.X * zoom) + panX,
                (entry.Value.Y * zoom) + panY,
                entry.Value.Width * zoom,
                entry.Value.Height * zoom);
            bool dimmed = matched is not null && !matched.Contains(node.NodeId);
            Brush fill = FillBrush(node);
            context.PushOpacity(dimmed ? 0.35 : 1.0);
            context.DrawRectangle(fill, BorderPen(node), rect);
            DrawTitle(context, node, rect, fontSize);
            context.Pop();
        }
    }

    /// <summary>D4: the filter DIMS and never hides — the applied
    /// unit's matched set drives opacity, and the row inventory never
    /// shrinks. Null when no landed narrowing applies.</summary>
    private static System.Collections.Generic.IReadOnlySet<string>? MatchedIds(
        CanvasPresentationState state) =>
        state.Source.Loaded?.Unit is { Narrowed: true } unit ? unit.Matched : null;

    private void DrawEdges(CanvasPresentationState state)
    {
        using DrawingContext context = _edges.RenderOpen();
        CanvasPopulation? population = state.Source.Loaded?.Population;
        if (population is null)
        {
            return;
        }
        double zoom = state.Viewport.Zoom;
        double panX = state.Viewport.PanX;
        double panY = state.Viewport.PanY;
        var edgePen = new Pen(
            BrushOf("Slate.Canvas.EdgeBrush"), Math.Max(1.0, 1.5 * zoom));
        foreach (CanvasSceneEdge edge in population.SceneEdges)
        {
            if (!population.SceneByNode.TryGetValue(edge.FromNode, out CanvasSceneNode? from)
                || !population.SceneByNode.TryGetValue(edge.ToNode, out CanvasSceneNode? to))
            {
                continue;
            }
            CanvasRect fromRect = state.NodeRect(from);
            CanvasRect toRect = state.NodeRect(to);
            var start = new Point(
                ((fromRect.X + (fromRect.Width / 2)) * zoom) + panX,
                ((fromRect.Y + (fromRect.Height / 2)) * zoom) + panY);
            var end = new Point(
                ((toRect.X + (toRect.Width / 2)) * zoom) + panX,
                ((toRect.Y + (toRect.Height / 2)) * zoom) + panY);
            context.DrawLine(edgePen, start, end);
        }
    }

    /// <summary>D8: the ring is SCREEN-SPACE — minimum two
    /// device-independent pixels at any zoom, never scaled into
    /// sub-pixelhood — and derives from the same installed state as
    /// the pixels it rings.</summary>
    private void DrawRing(CanvasPresentationState state)
    {
        using DrawingContext context = _ring.RenderOpen();
        string? selection = state.Selection;
        CanvasPopulation? population = state.Source.Loaded?.Population;
        if (selection is null
            || population is null
            || !population.SceneByNode.TryGetValue(selection, out CanvasSceneNode? node))
        {
            return;
        }
        double zoom = state.Viewport.Zoom;
        CanvasRect nodeRect = state.NodeRect(node);
        var rect = new Rect(
            (nodeRect.X * zoom) + state.Viewport.PanX - 2,
            (nodeRect.Y * zoom) + state.Viewport.PanY - 2,
            (nodeRect.Width * zoom) + 4,
            (nodeRect.Height * zoom) + 4);
        var pen = new Pen(
            BrushOf("Slate.Canvas.SelectionRingBrush"),
            Math.Max(2.0, 2.0));
        context.DrawRectangle(null, pen, rect);
    }

    /// <summary>Theme lookup with an honest fallback: an unthemed
    /// host (the windowed test harness) draws transparent rather than
    /// crashing the dispatcher — key integrity is the token-drift
    /// census's job (TD-7), not a draw-time throw's.</summary>
    private Brush BrushOf(string key) =>
        TryFindResource(key) as Brush ?? Brushes.Transparent;

    private Brush FillBrush(CanvasSceneNode node)
    {
        if (node.Kind == "group")
        {
            return BrushOf("Slate.Canvas.GroupFillBrush");
        }
        if (CanvasPalette.PresetTint(node.Color) is { } _ && node.Color is { Length: 1 })
        {
            return BrushOf($"Slate.Canvas.Fill{node.Color}Brush");
        }
        if (node.Color is { } raw && CanvasPalette.Hex(raw) is { } tint)
        {
            // The hostile hex path: the SAME arithmetic the static
            // tokens precompute, at runtime (D13's hex row gates it).
            Color surface = (BrushOf("Slate.SurfaceBrush") as SolidColorBrush)?.Color ?? Colors.Transparent;
            return new SolidColorBrush(
                CanvasPalette.Blend(tint, CanvasPalette.FillTintFraction, surface));
        }
        return BrushOf("Slate.SurfaceBrush");
    }

    private Pen BorderPen(CanvasSceneNode node) =>
        new(
            node.Color is { Length: 1 }
                ? BrushOf($"Slate.Canvas.Border{node.Color}Brush")
                : BrushOf("Slate.BorderBrush"),
            1.0);

    private void DrawTitle(
        DrawingContext context, CanvasSceneNode node, Rect rect, double fontSize)
    {
        var text = new FormattedText(
            node.Title,
            System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            fontSize,
            BrushOf("Slate.Canvas.TextBrush"),
            VisualTreeHelper.GetDpi(this).PixelsPerDip)
        {
            MaxTextWidth = Math.Max(0, rect.Width - 8),
            MaxLineCount = 2,
            Trimming = TextTrimming.CharacterEllipsis,
        };
        // The truncation set feeds the tooltip (ID-9): a card whose
        // full text does not fit its two constrained lines carries
        // the FULL title in the peer Name and in the tooltip.
        var unconstrained = new FormattedText(
            node.Title,
            System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            fontSize,
            Brushes.Transparent,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        if (unconstrained.WidthIncludingTrailingWhitespace > text.MaxTextWidth
            || unconstrained.Height > text.Height)
        {
            _ = _truncated.Add(node.NodeId);
        }
        context.DrawText(text, new Point(rect.X + 4, rect.Y + 4));
    }

    /// <summary>D9: hit-testing is core's DOCUMENT order — topmost
    /// wins by walking the scene order backwards; groups sit behind
    /// members because core inserts them so. The renderer never
    /// re-sorts.</summary>
    internal string? HitTest(Point view)
    {
        if (_engine.Current is not { } state
            || state.Source.Loaded?.Population is not { } population)
        {
            return null;
        }
        double zoom = state.Viewport.Zoom;
        double panX = state.Viewport.PanX;
        double panY = state.Viewport.PanY;
        for (int index = population.SceneNodes.Length - 1; index >= 0; index--)
        {
            CanvasSceneNode node = population.SceneNodes[index];
            CanvasRect hitRect = state.NodeRect(node);
            var rect = new Rect(
                (hitRect.X * zoom) + panX,
                (hitRect.Y * zoom) + panY,
                hitRect.Width * zoom,
                hitRect.Height * zoom);
            if (rect.Contains(view))
            {
                return node.NodeId;
            }
        }
        return null;
    }

    /// <summary>The viewport verbs, on THIS pane's engine (§D D14).
    /// Zoom verbs are centre-preserving on the view's centre; fit and
    /// zoom-to-selection compute their bounds from the installed
    /// state. The navigator has already answered the no-selection
    /// case; every arrival here acts.</summary>
    internal CanvasViewportOutcome Viewport(CanvasViewportVerb verb)
    {
        double centreX = ActualWidth / 2;
        double centreY = ActualHeight / 2;
        switch (verb)
        {
            case CanvasViewportVerb.ZoomIn:
                _engine.CommitViewport(v => v.ZoomedIn(centreX, centreY));
                return Zoomed(null);
            case CanvasViewportVerb.ZoomOut:
                _engine.CommitViewport(v => v.ZoomedOut(centreX, centreY));
                return Zoomed(null);
            case CanvasViewportVerb.ActualSize:
                _engine.CommitViewport(v => v.AtActualSize(centreX, centreY));
                return Zoomed(null);
            case CanvasViewportVerb.FitCanvas:
                return FitTo(AllBounds(), CanvasViewportState.FitPadding, CanvasZoomContext.FitCanvas);
            case CanvasViewportVerb.ZoomToSelection:
                return FitTo(
                    SelectionBounds(), CanvasViewportState.FitSelectionPadding, CanvasZoomContext.ZoomedToSelection);
            case CanvasViewportVerb.ToggleFollowSelection:
                _engine.CommitViewport(v => v.WithFollowSelection(!v.FollowSelection));
                return new CanvasViewportOutcome.FollowChanged(_engine.CommittedViewport.FollowSelection);
            default:
                return CanvasViewportOutcome.Refused;
        }
    }

    /// <summary>§H TH-5: the committed zoom as the percent core renders,
    /// with the verb's context — the payload the navigator speaks.</summary>
    private CanvasViewportOutcome Zoomed(CanvasZoomContext? context) =>
        new CanvasViewportOutcome.Zoomed(_engine.CommittedViewport.ZoomPercent, context);

    /// <summary>§H TH-7 (§W-G row H, IH-42): the canvas's extent is CORE's
    /// <c>canvas_bounds</c> through the document, under the lease — the
    /// host union that stood here was the row's Windows half, reopened
    /// and closed. Null is an empty canvas or a retired lease: nothing
    /// to fit.</summary>
    private System.Windows.Rect? AllBounds() =>
        Model?.CurrentBounds() is { } bounds
            ? new System.Windows.Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height)
            : null;

    private System.Windows.Rect? SelectionBounds() =>
        _engine.Current is { } state
            && state.Selection is { } selected
            && state.Source.Loaded?.Population is { } population
            && population.SceneByNode.TryGetValue(selected, out CanvasSceneNode? node)
            ? new System.Windows.Rect(node.X, node.Y, node.Width, node.Height)
            : null;

    /// <summary>Fit: choose the zoom that contains the bounds plus
    /// padding, clamped as every zoom is, and centre them.</summary>
    private CanvasViewportOutcome FitTo(
        System.Windows.Rect? bounds, double padding, CanvasZoomContext context)
    {
        if (bounds is not { } target
            || ActualWidth <= 0
            || ActualHeight <= 0
            || target.Width <= 0
            || target.Height <= 0)
        {
            return CanvasViewportOutcome.Silent;
        }
        _engine.CommitViewport(v =>
        {
            double zoom = Math.Clamp(
                Math.Min(
                    (v.ViewWidth - (padding * 2)) / target.Width,
                    (v.ViewHeight - (padding * 2)) / target.Height),
                CanvasViewportState.MinZoom,
                CanvasViewportState.MaxZoom);
            double panX = (v.ViewWidth / 2) - ((target.X + (target.Width / 2)) * zoom);
            double panY = (v.ViewHeight / 2) - ((target.Y + (target.Height / 2)) * zoom);
            return v.WithZoom(zoom, 0, 0).PannedTo(panX, panY);
        });
        return Zoomed(context);
    }

    /// <summary>The view's teardown half: detach the model, dispose
    /// the owned service — the lifecycle facts pin that handler counts
    /// return to baseline.</summary>
    internal void Shutdown()
    {
        Model = null;
        // The engine's own teardown guard (codoki on this PR): the
        // review round ADDED the guard and its record said the
        // renderer calls it — this call is where that sentence
        // becomes true, so an in-flight or queued install can never
        // draw into a detached view.
        _engine.Shutdown();
        _textScale.Dispose();
    }

    // --- The peer surface (§D D3): identity-stable, state-read -----------

    private readonly System.Collections.Generic.Dictionary<CanvasPeerKey, CanvasCardAutomationPeer>
        _peers = [];

    protected override System.Windows.Automation.Peers.AutomationPeer OnCreateAutomationPeer() =>
        new CanvasRendererAutomationPeer(this);

    /// <summary>The container's children: one peer per MATERIALIZED
    /// placement, document order — minted on demand, identity-stable
    /// per key within this renderer (D3), retained in the registry as
    /// long as materialized or externally realized.</summary>
    internal System.Collections.Generic.List<System.Windows.Automation.Peers.AutomationPeer>
        MaterializedPeers()
    {
        var children = new System.Collections.Generic.List<
            System.Windows.Automation.Peers.AutomationPeer>();
        if (_engine.Current is not { } state
            || state.Source.Loaded?.Population is not { } population)
        {
            return children;
        }
        foreach (CanvasSceneNode node in population.SceneNodes)
        {
            CanvasPeerKey key = CanvasPeerKey.Card(node.NodeId);
            if (state.Topology.Placements.TryGetValue(key, out CanvasPeerPlacement? placement)
                && placement.Cell == CanvasPeerCell.Materialized)
            {
                children.Add(PeerFor(key)!);
            }
        }
        return children;
    }

    /// <summary>The registry read: an existing peer, or mint one for a
    /// key the current population knows. Identity commits only with
    /// use — a key nobody asked for holds nothing (the retirement
    /// rule's registry half).</summary>
    internal CanvasCardAutomationPeer? PeerFor(CanvasPeerKey key)
    {
        if (_peers.TryGetValue(key, out CanvasCardAutomationPeer? existing))
        {
            return existing;
        }
        if (_engine.Current?.Source.Loaded?.Population is not { } population
            || key.IsEdge
            || !population.SceneByNode.ContainsKey(key.Id))
        {
            return null;
        }
        var peer = new CanvasCardAutomationPeer(this, key);
        _peers[key] = peer;
        return peer;
    }

    /// <summary>The item-container search (D3's first-touch cell): by
    /// Name against the descriptor index, or the next card in document
    /// order after the given peer.</summary>
    internal CanvasPeerKey? FindByName(
        System.Windows.Automation.Peers.AutomationPeer? after, string? name)
    {
        if (_engine.Current?.Source.Loaded?.Population is not { } population)
        {
            return null;
        }
        string? afterId = (after as CanvasCardAutomationPeer)?.Key.Id;
        var passed = afterId is null;
        foreach (CanvasSceneNode node in population.SceneNodes)
        {
            if (!passed)
            {
                passed = node.NodeId == afterId;
                continue;
            }
            if (name is null || string.Equals(node.SpeakableName, name, StringComparison.Ordinal))
            {
                return CanvasPeerKey.Card(node.NodeId);
            }
        }
        return null;
    }

    /// <summary>Realization (D3): commit the key to the retained
    /// authority — the engine rebuilds, the pan materializes the
    /// target — and hand back the SAME peer object, promoted by the
    /// next installed state.</summary>
    internal CanvasCardAutomationPeer? RealizePeer(CanvasPeerKey key)
    {
        if (PeerFor(key) is not { } peer)
        {
            return null;
        }
        Realize(key);
        return peer;
    }

    /// <summary>The realize half: retained-commit plus the pan that
    /// brings the card into the window (D4 — a realization is a
    /// selection made ON this surface for pan purposes: it always
    /// scrolls into view).</summary>
    internal void Realize(CanvasPeerKey key)
    {
        if (_engine.Current?.Source.Loaded?.Population is not { } population
            || !population.SceneByNode.TryGetValue(key.Id, out CanvasSceneNode? node))
        {
            return;
        }
        var retained = System.Collections.Immutable.ImmutableHashSet.CreateRange(
            _peers.Keys).Add(key);
        _engine.CommitRetained(retained);
        _engine.CommitViewport(v => PanToContain(v, node));
    }

    private static CanvasViewportState PanToContain(
        CanvasViewportState viewport, CanvasSceneNode node)
    {
        double zoom = viewport.Zoom;
        double viewX = (node.X * zoom) + viewport.PanX;
        double viewY = (node.Y * zoom) + viewport.PanY;
        double panX = viewport.PanX;
        double panY = viewport.PanY;
        if (viewX < 0)
        {
            panX -= viewX;
        }
        else if (viewX + (node.Width * zoom) > viewport.ViewWidth)
        {
            panX -= viewX + (node.Width * zoom) - viewport.ViewWidth;
        }
        if (viewY < 0)
        {
            panY -= viewY;
        }
        else if (viewY + (node.Height * zoom) > viewport.ViewHeight)
        {
            panY -= viewY + (node.Height * zoom) - viewport.ViewHeight;
        }
        return viewport.PannedTo(panX, panY);
    }

    /// <summary>D6's announced door, from a peer operation: the
    /// document's one narrating selection mutation, then the
    /// origin-sensitive pan (a selection made ON this surface always
    /// scrolls into view).</summary>
    internal void SelectAnnounced(string nodeId)
    {
        if (_model is not { } model)
        {
            return;
        }
        model.SelectNode(nodeId);
        if (_engine.Current?.Source.Loaded?.Population is { } population
            && population.SceneByNode.TryGetValue(nodeId, out CanvasSceneNode? node))
        {
            _engine.CommitViewport(v => PanToContain(v, node));
        }
    }

    /// <summary>The matrix's clear cell: RemoveFromSelection on the
    /// selected card clears it, announced.</summary>
    internal void ClearSelectionAnnounced() => _model?.SelectNode(null);

    /// <summary>View-space to screen for the peer rectangles — the
    /// classic stale-frame failure is prevented by computing at READ
    /// time from the installed state.</summary>
    internal System.Windows.Rect ViewToScreen(System.Windows.Rect view)
    {
        if (PresentationSource.FromVisual(this) is null)
        {
            return view;
        }
        System.Windows.Point topLeft = PointToScreen(view.TopLeft);
        System.Windows.Point bottomRight = PointToScreen(view.BottomRight);
        return new System.Windows.Rect(topLeft, bottomRight);
    }

    // --- The truncated-label tooltip (§D D11, obligation ID-9) ----------

    private readonly System.Windows.Controls.Primitives.Popup _tooltip;
    private readonly System.Windows.Controls.TextBlock _tooltipText;
    private readonly System.Collections.Generic.HashSet<string> _truncated =
        new(StringComparer.Ordinal);
    private string? _hoverNode;
    private bool _pointerOnTooltip;

    /// <summary>The three 1.4.13 conditions, as ONE rule (round 4's
    /// matrix): the tooltip is OPEN while any trigger is active — the
    /// SELECTION on a truncated card (the keyboard trigger: cards are
    /// peers, and the arrows' selection is the keyboard's presence) or
    /// the POINTER on the card or on the tooltip itself (hoverable) —
    /// and it closes only when every trigger has departed, Esc
    /// dismisses it through the surface rung (DD-3), or the content
    /// stops being valid (the card gone, or no longer
    /// truncated).</summary>
    private void UpdateTooltip()
    {
        string? subject = TooltipSubject();
        if (subject is null)
        {
            _tooltip.IsOpen = false;
            _tooltipTextSubject = null;
            return;
        }
        if (_engine.Current?.Source.Loaded?.Population is not { } population
            || !population.SceneByNode.TryGetValue(subject, out CanvasSceneNode? node))
        {
            _tooltip.IsOpen = false;
            return;
        }
        _tooltipTextSubject = subject;
        _tooltipText.Text = node.Title;
        double zoom = _engine.Current.Viewport.Zoom;
        _tooltip.HorizontalOffset = (node.X * zoom) + _engine.Current.Viewport.PanX;
        _tooltip.VerticalOffset =
            ((node.Y + node.Height) * zoom) + _engine.Current.Viewport.PanY;
        _tooltip.IsOpen = true;
    }

    /// <summary>The active trigger's card, or null: the selection on a
    /// truncated card wins; else the hovered truncated card; else —
    /// PERSISTENT — the pointer sitting on the open tooltip keeps its
    /// current subject alive.</summary>
    private string? TooltipSubject()
    {
        string? selection = _engine.Current?.Selection;
        if (selection is not null && _truncated.Contains(selection))
        {
            return selection;
        }
        if (_hoverNode is { } hovered && _truncated.Contains(hovered))
        {
            return hovered;
        }
        if (_pointerOnTooltip && _tooltip.IsOpen && _tooltipText.Text.Length > 0)
        {
            return _tooltipTextSubject;
        }
        return null;
    }

    private string? _tooltipTextSubject;

    /// <summary>Esc's answer, called by the surface rung (DD-3: the
    /// tooltip is the most transient — first inside the rung, after
    /// CD-47's panel pre-emption).</summary>
    internal bool DismissTooltip()
    {
        if (!_tooltip.IsOpen)
        {
            return false;
        }
        _tooltip.IsOpen = false;
        _hoverNode = null;
        _pointerOnTooltip = false;
        return true;
    }

    protected override void OnMouseMove(System.Windows.Input.MouseEventArgs e)
    {
        base.OnMouseMove(e);
        string? hovered = HitTest(e.GetPosition(this));
        if (!string.Equals(hovered, _hoverNode, StringComparison.Ordinal))
        {
            _hoverNode = hovered;
            UpdateTooltip();
        }
    }

    protected override void OnMouseLeave(System.Windows.Input.MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _hoverNode = null;
        UpdateTooltip();
    }

    /// <summary>The hover trigger's seams — dispositioned instruments:
    /// a windowed fact cannot steer the real pointer reliably, and the
    /// journey owns the physical half.</summary>
    internal void PointerEnteredForTests(string nodeId)
    {
        _hoverNode = nodeId;
        UpdateTooltip();
    }

    internal void PointerLeftForTests()
    {
        _hoverNode = null;
        _pointerOnTooltip = false;
        UpdateTooltip();
    }

    internal bool TooltipIsOpenForTests => _tooltip.IsOpen;

    internal string TooltipTextForTests => _tooltipText.Text;
}
