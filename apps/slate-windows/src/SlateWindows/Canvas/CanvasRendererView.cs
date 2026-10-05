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
        IsVisibleChanged += OnIsVisibleChanged;
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
        // R-12 (#1256, OD-3): the board's card menu EXISTS from here and
        // is refilled per request (see OnMenuOpening) — WPF opens the menu
        // that exists when a request arrives.
        ContextMenu = _menu;
        AddHandler(
            ContextMenuOpeningEvent,
            new System.Windows.Controls.ContextMenuEventHandler(OnMenuOpening),
            handledEventsToo: false);
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
                LeaveDocument(old);
            }
            // A reveal owed to the old document's seat is not this one's
            // (#1271, review round 2); a teardown sets null and lands here too.
            _owedReveal = null;
            _model = value;
            if (value is not null)
            {
                value.PublicationApplied += _engine.OnPublicationApplied;
                value.ModeVisibleChanged += OnModeVisibleChanged;
                _engine.OnPublicationApplied(
                    value.AppliedPublication ?? CanvasPublication.Seed());
                _engine.CommitTransient(value.Transient);
                ReturnToDocument(value);
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

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == VisibilityProperty)
        {
            RefreshAutomationChildren();
        }
    }

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
        RefreshAutomationChildren();
        RetireReleasedTombstones(state);
        // The tooltip revalidates on EVERY install: the selection
        // trigger, the truncation set and the content's validity all
        // derive from the state that just landed (ID-9's
        // becomes-invalid arm).
        UpdateTooltip();
        // A reveal owed to a card the previous state lacked is paid by
        // the first install that has it (#1271, review round 2).
        PayOwedReveal();
    }

    /// <summary>WPF caches the board's children. Refresh after the winning
    /// install or its own visibility change, before a selection provider
    /// needs to connect a newly materialized card through that child list.
    /// Keep the existing peer identities and do not create a peer merely
    /// because the renderer changed.</summary>
    /// <remarks>
    /// Not <c>ResetChildrenCache</c>: with a structure-changed listener
    /// present, its per-child diff hands each newly windowed card's provider
    /// to UIA, and a provider holds its peer strongly, so every card the
    /// board ever windowed stayed live and became a retained tombstone at the
    /// next realization. The cache is rebuilt without that diff, and a
    /// changed child list raises the container's one children-invalidated
    /// event instead (contract 34 D3), after which clients re-walk and hold
    /// only what they keep.
    /// </remarks>
    private void RefreshAutomationChildren()
    {
        if (System.Windows.Automation.Peers.UIElementAutomationPeer.FromElement(this)
            is not CanvasRendererAutomationPeer board)
        {
            return;
        }
        System.Collections.Generic.List<System.Windows.Automation.Peers.AutomationPeer> before =
            board.GetChildren() ?? [];
        WpfEditorPeerConnection.InvalidateChildren(board);
        if (!before.SequenceEqual(board.GetChildren() ?? []))
        {
            board.RaiseChildrenInvalidated();
        }
    }

    /// <summary>D3's retirement rule, the state half: a retained key whose
    /// peer nothing holds any more loses its tombstone in the next install,
    /// rather than riding every later derive and draw. Runs after the
    /// children refresh, which holds every windowed card's peer.</summary>
    private void RetireReleasedTombstones(CanvasPresentationState state)
    {
        if (state.Retained.All(PeerIsLive))
        {
            return;
        }
        _engine.CommitRetained(
            System.Collections.Immutable.ImmutableHashSet.CreateRange(state.Retained.Where(PeerIsLive)));
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

    /// <summary>
    /// The registry holds weak identity only (D3's retirement rule). A peer
    /// stays the same object for as long as anything holds it — a client's
    /// provider, the board's cached children — and a key nothing holds is
    /// reclaimed and minted afresh on its next use, which no holder can
    /// observe. Every install refreshes the board's children and mints a
    /// peer per windowed card, so a strong registry kept every card the
    /// board ever windowed, and a realization made them all tombstones.
    /// </summary>
    private readonly System.Collections.Generic.Dictionary<CanvasPeerKey, WeakReference<CanvasCardAutomationPeer>>
        _peers = [];

    /// <summary>The registry size that triggers the next sweep of reclaimed
    /// identities: twice the entries the last sweep kept, so sweeping costs
    /// amortized constant time per mint.</summary>
    private int _peerSweepAt = PeerSweepFloor;

    private const int PeerSweepFloor = 64;

    protected override System.Windows.Automation.Peers.AutomationPeer OnCreateAutomationPeer() =>
        new CanvasRendererAutomationPeer(this);

    /// <summary>The container's children: one peer per MATERIALIZED
    /// placement, document order — minted on demand and identity-stable
    /// per key within this renderer while anything holds the peer
    /// (D3).</summary>
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

    /// <summary>The registry read: a live peer, or mint one for a key the
    /// current population knows. Identity commits only with use — a key
    /// nobody asked for holds nothing (the retirement rule's registry
    /// half).</summary>
    internal CanvasCardAutomationPeer? PeerFor(CanvasPeerKey key)
    {
        if (_peers.TryGetValue(key, out WeakReference<CanvasCardAutomationPeer>? held)
            && held.TryGetTarget(out CanvasCardAutomationPeer? existing))
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
        if (held is not null)
        {
            held.SetTarget(peer);
            return peer;
        }
        _peers[key] = new WeakReference<CanvasCardAutomationPeer>(peer);
        if (_peers.Count >= _peerSweepAt)
        {
            foreach (CanvasPeerKey reclaimed in _peers
                .Where(entry => !entry.Value.TryGetTarget(out _))
                .Select(entry => entry.Key)
                .ToArray())
            {
                _ = _peers.Remove(reclaimed);
            }
            _peerSweepAt = Math.Max(PeerSweepFloor, _peers.Count * 2);
        }
        return peer;
    }

    /// <summary>Whether a client or the board still holds the key's
    /// peer.</summary>
    private bool PeerIsLive(CanvasPeerKey key) =>
        _peers.TryGetValue(key, out WeakReference<CanvasCardAutomationPeer>? held)
        && held.TryGetTarget(out _);

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
            _peers.Keys.Where(PeerIsLive)).Add(key);
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
        RevealNode(nodeId, CanvasMoveOrigin.OnSurface);
    }

    /// <summary>
    /// The pan that brings the seat into the window (D4): the peer door's
    /// above, and since R-12 (#1255) the presenter's, which has already
    /// decided by <see cref="RevealsMoveFrom"/> that this move reveals.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A reveal is a DEBT this board owes the reader until the state that has
    /// the card is installed (R-12 follow-up #1271, review round 2). The seat
    /// moves the moment the document publishes — New Card and Duplicate seat
    /// a card that exists only in the successor — while this board's engine
    /// builds and installs that successor later, off the dispatcher; a reveal
    /// computed against the installed predecessor finds no card to contain.
    /// So the pan is paid now when the installed state has the card, and is
    /// otherwise owed and paid by the install that brings it
    /// (<see cref="PayOwedReveal"/>).
    /// </para>
    /// <para>
    /// The same debt carries a reveal across a HIDDEN spell (owner decision,
    /// review round 3): a board behind another tab, or under the outline or
    /// the table, owes the reveal and pays it once it is showing again — on
    /// the first install once visible, or as soon as it is shown if no install
    /// comes (<see cref="OnIsVisibleChanged"/>).
    /// </para>
    /// <para>
    /// The rules, one slot per board: a newer reveal supersedes the owed one,
    /// paid or owed in turn. An owed reveal is paid only while it is still
    /// payable, judged by TOKENS taken when it was owed, never by the state
    /// alone (review round 3: a seat that left and came back, or a toggle
    /// turned off and on, looks unchanged to the state): the selection's
    /// <see cref="CanvasSelection.Revision"/> must not have moved — a later
    /// selection change voids the debt, so it is never replayed — and a move
    /// made elsewhere needs Follow Selection on AND the viewport's
    /// <see cref="CanvasViewportState.FollowLapses"/> unchanged — it lapses the
    /// moment the board stops following, for good — while a move made on the
    /// board stands, toggle or no toggle. It is paid once and cleared. When
    /// the board's document changes it is set aside with that document, never
    /// paid against another, and settled if the board returns to it — a tab
    /// switch rebinds the board rather than hiding it (codex's final check;
    /// <see cref="ReturnToDocument"/>).
    /// </para>
    /// <para>
    /// It is paid only FROM an installed state whose population is the one
    /// the document has applied (review round 4; <see cref="TryPanToContain"/>):
    /// a card the predecessor has under the same id is not yet the seat's
    /// card. That population is read when the debt is paid, not stamped when
    /// it is owed: a later reload the seat survives (same revision) owes the
    /// reveal against the later population, which may be the only one this
    /// board's engine ever installs.
    /// </para>
    /// </remarks>
    internal void RevealNode(string nodeId, CanvasMoveOrigin origin)
    {
        _owedReveal = new OwedReveal(
            nodeId,
            origin,
            _model?.Selection.Revision ?? 0,
            _engine.CommittedViewport.FollowLapses);
        PayOwedReveal();
    }

    /// <summary>The reveal this board owes (see <see cref="RevealNode"/>);
    /// null when it owes none.</summary>
    private OwedReveal? _owedReveal;

    /// <summary>A reveal owed, with the tokens its validity is judged by:
    /// the selection revision and the viewport's follow-lapse count at the
    /// moment it was owed.</summary>
    private sealed record OwedReveal(
        string NodeId, CanvasMoveOrigin Origin, long SelectionRevision, long FollowLapses);

    /// <summary>Pay the owed reveal if it is still payable
    /// (<see cref="RevealNode"/>'s rules) and the board is showing; an
    /// unpayable debt is written off, a payable one waits while the board is
    /// hidden or its installed state is not yet the document's population with
    /// the card.</summary>
    private void PayOwedReveal()
    {
        if (_owedReveal is not { } owed)
        {
            return;
        }
        if (!StillPayable(owed))
        {
            _owedReveal = null;
            return;
        }
        if (!IsShowing)
        {
            return;
        }
        if (TryPanToContain(owed.NodeId))
        {
            _owedReveal = null;
        }
    }

    /// <summary>Whether an owed reveal is still owed: the seat has not moved
    /// since (the selection revision), and a move made elsewhere still
    /// reveals — Follow Selection on, and never turned off since (the lapse
    /// count). A move made on the board needs only the first.</summary>
    private bool StillPayable(OwedReveal owed)
    {
        if (_model is not { } model || model.Selection.Revision != owed.SelectionRevision)
        {
            return false;
        }
        if (owed.Origin == CanvasMoveOrigin.OnSurface)
        {
            return true;
        }
        CanvasViewportState view = _engine.CommittedViewport;
        return view.FollowSelection && view.FollowLapses == owed.FollowLapses;
    }

    /// <summary>Whether the board is on screen with a laid-out view — the
    /// only time a pan means anything to the reader.</summary>
    private bool IsShowing =>
        IsVisible && _engine.CommittedViewport is { ViewWidth: > 0, ViewHeight: > 0 };

    /// <summary>
    /// What this board last saw of each document it has shown and left — the
    /// selection revision, whether it followed and its follow-lapse count, and
    /// the reveal it still owed — keyed weakly, so a closed document's entry
    /// goes with it.
    /// </summary>
    /// <remarks>
    /// Shown again across a TAB SWITCH (codex's final check on #1271): the
    /// workspace's pane is one selected-content TabControl, so switching tabs
    /// does not hide this board, it REBINDS it — the pane's canvas surface
    /// takes the other tab's document (none, for a note) and this board stops
    /// hearing the one it left; switching back binds it again. The board's
    /// view outlives the rebinding, so what it owed that view must too.
    /// </remarks>
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<
        CanvasDocumentViewModel, AwaySpell> _awaySpells = new();

    /// <summary>What the board saw of a document when it left it.</summary>
    private sealed record AwaySpell(
        long SelectionRevision, bool Followed, long FollowLapses, OwedReveal? Owed);

    /// <summary>The board is leaving <paramref name="document"/> (a tab
    /// switch, a retarget or a teardown): keep what it saw and owed.</summary>
    private void LeaveDocument(CanvasDocumentViewModel document)
    {
        CanvasViewportState view = _engine.CommittedViewport;
        _awaySpells.AddOrUpdate(
            document,
            new AwaySpell(document.Selection.Revision, view.FollowSelection, view.FollowLapses, _owedReveal));
    }

    /// <summary>
    /// The board is bound to <paramref name="document"/> again: settle the
    /// spell it was away, under the rules a hidden board is held to
    /// (<see cref="RevealNode"/>). If the seat has not moved since, the reveal
    /// it owed is owed again, its own tokens judging it. If the seat moved
    /// while it was away, a board that followed the selection the whole time —
    /// on when it left, on now, and never turned off in between (the lapse
    /// count) — owes the seat as a move made elsewhere; one that did not owes
    /// nothing, since it cannot tell a move made while it followed from one
    /// made while it did not. A document it has never shown owes nothing.
    /// </summary>
    private void ReturnToDocument(CanvasDocumentViewModel document)
    {
        if (!_awaySpells.TryGetValue(document, out AwaySpell? spell))
        {
            return;
        }
        _ = _awaySpells.Remove(document);
        CanvasViewportState view = _engine.CommittedViewport;
        if (document.Selection.Revision == spell.SelectionRevision)
        {
            _owedReveal = spell.Owed;
        }
        else if (document.Selection.Selected is { } seat
            && spell.Followed
            && view.FollowSelection
            && view.FollowLapses == spell.FollowLapses)
        {
            _owedReveal = new OwedReveal(
                seat, CanvasMoveOrigin.Elsewhere, document.Selection.Revision, view.FollowLapses);
        }
        PayOwedReveal();
    }

    /// <summary>Shown again (owner decision, review round 3): a reveal owed
    /// while hidden is paid once the board is visible — after the layout pass
    /// that gives it its size, since showing it may bring no install.</summary>
    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true && _owedReveal is not null)
        {
            _ = Dispatcher.BeginInvoke(
                new Action(PayOwedReveal), System.Windows.Threading.DispatcherPriority.Loaded);
        }
    }

    /// <summary>Commit the pan that contains the card in the INSTALLED
    /// state, answering whether that state could pay: it must carry the
    /// population the document has APPLIED — the one the seat was resolved
    /// against — and have the card in it (review round 4). The seat moves
    /// during an apply, before this board's engine has the successor, and a
    /// card that survives a reload under the same id stands in the
    /// predecessor at the place it has left, so an id alone never pays.</summary>
    private bool TryPanToContain(string nodeId)
    {
        if (_engine.Current?.Source.Loaded?.Population is not { } population
            || !ReferenceEquals(population, _model?.AppliedPublication?.Population)
            || !population.SceneByNode.TryGetValue(nodeId, out CanvasSceneNode? node))
        {
            return false;
        }
        _engine.CommitViewport(v => PanToContain(v, node));
        return true;
    }

    /// <summary>D4's pan rule against this board's committed viewport
    /// (R-12 follow-up #1271): a move made on the board always reveals,
    /// one made elsewhere only while Follow Selection is on.</summary>
    internal bool RevealsMoveFrom(CanvasMoveOrigin origin) =>
        _engine.CommittedViewport.RevealsMoveFrom(origin);

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

    // --- The card menu (W7-7 R-12, #1256; owner decision OD-3) -----------

    /// <summary>The board's card menu: PERSISTENT from construction and
    /// refilled per request, never replaced (the grid's rule). G2D-12's
    /// "the renderer carries no context menu" is lifted and contract 34
    /// E17's "renderer card" delivered.</summary>
    private readonly System.Windows.Controls.ContextMenu _menu = new();

    /// <summary>
    /// WPF's request for the board's menu: the Applications key or
    /// Shift+F10 while the board holds the keys, or a right-click on it
    /// (R-12, #1256; OD-3).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The board had no menu at all, so the NVDA pass's Shift+F10 on it
    /// climbed to the workspace tab's (Duplicate Tab … Close Pane — F13).
    /// The menu now exists from construction and is refilled here, because
    /// WPF opens the menu that exists when a request arrives.
    /// </para>
    /// <para>
    /// WPF raises the event on the element under the pointer with the
    /// cursor relative to it (the popup service reads the position against
    /// that element); the board hosts no child elements, so a pointer
    /// request's cursor is in the view space <see cref="HitTest"/> reads,
    /// and a keyboard request carries −1, −1 — the diagram's reading of the
    /// same event (<c>GraphDiagramView</c>).
    /// </para>
    /// <para>
    /// No card to answer for — nothing under the pointer, no seat, a seat
    /// the document no longer knows — is answered HERE with no menu, so
    /// the request never climbs to the tab's. A KEYBOARD request with no
    /// seat is a keypress that does nothing, so it also says so (contract
    /// 34 C3/C4/E8a, #1283): the document's C4 door speaks the existing
    /// <c>Nothing selected.</c> arm — the sentence the board's Right and
    /// Left already speak for the same seatless state — where it used to
    /// swallow the press in silence.
    /// </para>
    /// <para>
    /// A pointer request SEATS the hit card, silently, before its menu
    /// opens — contract 34 G2-12's rule for every context consumer (a row
    /// is seated silently before its verb), so the card the menu acts on
    /// and the selected card are one card while the menu is up, as the
    /// outline and the grid consumers keep them. A keyboard request needs
    /// no seat: it opens on the seat already.
    /// </para>
    /// </remarks>
    private void OnMenuOpening(object sender, System.Windows.Controls.ContextMenuEventArgs e)
    {
        bool pointerRequest = e.CursorLeft >= 0 || e.CursorTop >= 0;
        if (MenuTargetFor(pointerRequest, e.CursorLeft, e.CursorTop) is not { } nodeId)
        {
            if (!pointerRequest)
            {
                _ = _model?.AnsweredMissingSelection();
            }
            e.Handled = true;
            return;
        }
        if (!RebuildMenu(nodeId))
        {
            e.Handled = true;
            return;
        }
        if (pointerRequest)
        {
            _model?.SeatSelectionSilently(nodeId);
        }
    }

    /// <summary>OD-3's target rule, the diagram's: a pointer request
    /// opens on the card HIT at the view point, and on nothing over empty
    /// space; a keyboard request opens on the SEAT — the document's
    /// selection, the card the arrows moved to.</summary>
    internal string? MenuTargetFor(bool pointerRequest, double left, double top) =>
        pointerRequest ? HitTest(new Point(left, top)) : _model?.Selection.Selected;

    /// <summary>
    /// The persistent menu refilled with the plan's card menu for ONE
    /// card, every row acting on that card through the one dispatch.
    /// False, with the menu emptied, for a card the document does not
    /// know.
    /// </summary>
    /// <remarks>
    /// The card is the seat by the time a row runs — a keyboard request
    /// opened on it, a pointer request seated it (G2-12) — and the dispatch
    /// seats it silently again before its verb (TG-0), so a menu action
    /// changes that card and no other (R-12). Open runs the document's one
    /// activation seam, the table's route: a group has nothing to expand
    /// on the board, so its Open does nothing there, as on the table.
    /// </remarks>
    internal bool RebuildMenu(string nodeId)
    {
        if (_model is not { } model || model.RowFor(nodeId) is not { } row)
        {
            _menu.Items.Clear();
            return false;
        }
        var target = new CanvasContextTarget.Node(row.NodeId, row.Kind, row.GroupPath.Length > 0);
        object? owner = DataContext;
        return CanvasContextMenuBuilder.Refill(
            _menu,
            BuildMenuFromPlan(
                target,
                verb => CanvasContextDispatch.Execute(
                    model, target, verb, owner, () => _ = model.Activate(row))));
    }

    /// <summary>The ONE plan-to-menu mapping over the RENDERER's
    /// projection — the opening handler and the census fact share it
    /// (IE-31).</summary>
    internal static System.Windows.Controls.ContextMenu BuildMenuFromPlan(
        CanvasContextTarget target, Action<CanvasContextVerb> execute) =>
        CanvasContextMenuBuilder.Build(CanvasContextSurface.Renderer, target, execute);

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
