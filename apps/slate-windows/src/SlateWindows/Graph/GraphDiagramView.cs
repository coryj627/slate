// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Input;
using System.Windows.Media;
using SlateWindows.Canvas;
using uniffi.slate_uniffi;

namespace SlateWindows.Graph;

/// <summary>
/// W6-2 PR D (#746), rules T, V and N — the diagram's visual projection: a
/// custom FrameworkElement drawing core's positions from the ONE model the
/// document owns (rule G). Three DrawingVisuals — the edges, the nodes (or
/// the tier-B dots), the selection ring — redrawn from the installed
/// positions and the accepted topology on every frame, epoch, viewport or
/// theme change (Term T6); positions are layout space and the transform is
/// applied at draw. The visible set is the accepted topology's ids the model
/// knows (Term T1); tier A's peers are COMPLETE, one per visible node
/// whatever the viewport (Term T2, DD-Q1); tier B is one summary peer (Term
/// T3). The viewport IS <see cref="CanvasViewportState"/> (Term V1), held
/// here and reset with the model. The selection is DERIVED from the shared
/// key (Term N1): no selection of its own.
/// </summary>
internal sealed class GraphDiagramView : FrameworkElement
{
    /// <summary>Term T7: the hit grid's cell, in layout units (the mac's <c>gridCell</c>).</summary>
    internal const double GridCell = 64;

    /// <summary>Term V2: the fit's padding — the mac's, not the canvas's 40 (D-D8).</summary>
    internal const double FitPadding = 60;

    /// <summary>Term V2: a zero-size bounds is inflated by this before the fit (the mac's <c>:77–85</c>).</summary>
    internal const double EmptyBoundsInflation = 100;

    /// <summary>Term V5: the margin a selected node is kept inside (the mac's <c>:1108–1121</c>).</summary>
    internal const double ScrollMargin = 48;

    /// <summary>Term T4: the label's size in units, before the text scale and the zoom.</summary>
    internal const double LabelFontSize = 11;

    /// <summary>Term T5: the selection ring sits this far outside the node.</summary>
    internal const double SelectionRingGap = 4;

    /// <summary>Term T5: the tokens, in core's <c>graph_color_tokens()</c> order.</summary>
    internal static readonly string[] GroupBrushKeys =
    [
        "Slate.Graph.Group1Brush",
        "Slate.Graph.Group2Brush",
        "Slate.Graph.Group3Brush",
        "Slate.Graph.Group4Brush",
        "Slate.Graph.Group5Brush",
        "Slate.Graph.Group6Brush",
        "Slate.Graph.Group7Brush",
        "Slate.Graph.Group8Brush",
    ];

    internal const string RingBrushKey = "Slate.Graph.RingBrush";
    internal const string NoteBrushKey = "Slate.Graph.NoteBrush";
    internal const string AttachmentBrushKey = "Slate.Graph.AttachmentBrush";
    internal const string SurfaceBrushKey = "Slate.Graph.SurfaceBrush";
    internal const string OutlineBrushKey = "Slate.Graph.OutlineBrush";
    internal const string EdgeBrushKey = "Slate.Graph.EdgeBrush";
    internal const string LabelBrushKey = "Slate.Graph.LabelBrush";
    internal const string AccentBrushKey = "Slate.Graph.AccentBrush";

    /// <summary>Every token the renderer looks up (the token-drift census, D-15 x).</summary>
    internal static IReadOnlyList<string> TokenKeys { get; } =
        [.. GroupBrushKeys, RingBrushKey, NoteBrushKey, AttachmentBrushKey, SurfaceBrushKey, OutlineBrushKey, EdgeBrushKey, LabelBrushKey, AccentBrushKey];

    private readonly VisualCollection _visuals;
    private readonly DrawingVisual _edges;
    private readonly DrawingVisual _nodes;
    private readonly DrawingVisual _ring;
    private readonly Dictionary<ulong, GraphNodeAutomationPeer> _peers = [];
    private readonly Dictionary<ulong, GraphTopologyNode> _entries = [];
    private readonly Dictionary<string, ulong> _entriesByKey = new(StringComparer.Ordinal);
    private readonly Dictionary<(long X, long Y), List<ulong>> _grid = [];
    private readonly HashSet<ulong> _labelled = [];
    private readonly Dictionary<ulong, string> _fillKeys = [];
    private readonly Dictionary<ulong, (double Width, double[]? Dash)> _ringStyles = [];
    private CanvasTextScaleService? _textScale;
    private GraphDocumentViewModel? _model;
    private GraphDiagramModel? _diagram;
    private CanvasViewportState _viewport = CanvasViewportState.Seed();
    private ulong[] _visibleIds = [];
    private HashSet<ulong> _visibleSet = [];
    private GraphEdge[] _visibleEdges = [];
    private GraphTierSummaryAutomationPeer? _summaryPeer;
    private bool _lastTierB;
    private bool _didInitialFit;
    private bool _labelsShown;

    public GraphDiagramView()
    {
        // Term N2: the projection's ONE focus stop; the node peers take no keys.
        Focusable = true;
        KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.None);
        AutomationProperties.SetAutomationId(this, "GraphDiagram");
        _visuals = new VisualCollection(this);
        _edges = new DrawingVisual();
        _nodes = new DrawingVisual();
        _ring = new DrawingVisual();
        _ = _visuals.Add(_edges);
        _ = _visuals.Add(_nodes);
        _ = _visuals.Add(_ring);
        SizeChanged += (_, e) =>
        {
            _viewport = _viewport.WithViewSize(e.NewSize.Width, e.NewSize.Height);
            Redraw();
        };
        // The container's children follow the cluster's visibility (Term M5):
        // a collapsed board exposes none, so the peer's cache is reset here.
        IsVisibleChanged += (_, _) => RaiseStructureChanged();
        WireMenu();
    }

    // --- The model (rule G's owner is the document; this view reads) ---------------

    /// <summary>The document; the live diagram model is read from it and
    /// re-bound on its <c>HasLiveDiagram</c> edges.</summary>
    internal GraphDocumentViewModel? Model
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
                old.PropertyChanged -= OnDocumentChanged;
                old.DiagramTopologyChanged -= OnTopologyChanged;
                old.ViewState.PropertyChanged -= OnViewStateChanged;
                old.DiagramZoomPercent = null;
            }
            _model = value;
            if (value is not null)
            {
                value.PropertyChanged += OnDocumentChanged;
                value.DiagramTopologyChanged += OnTopologyChanged;
                value.ViewState.PropertyChanged += OnViewStateChanged;
                BindDiagram(value.HasLiveDiagram ? value.DiagramModel : null);
            }
            else
            {
                BindDiagram(null);
            }
        }
    }

    /// <summary>The surface's unload: the model dropped, the owned text-scale
    /// service disposed (Term T4: one per renderer, disposed at Shutdown).</summary>
    internal void Shutdown()
    {
        Model = null;
        _textScale?.Dispose();
        _textScale = null;
    }

    private CanvasTextScaleService TextScale
    {
        get
        {
            if (_textScale is null)
            {
                _textScale = new CanvasTextScaleService();
                _textScale.Changed += Redraw;
            }
            return _textScale;
        }
    }

    private void OnDocumentChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_model is not { } model)
        {
            return;
        }
        if (e.PropertyName == nameof(GraphDocumentViewModel.HasLiveDiagram))
        {
            BindDiagram(model.HasLiveDiagram ? model.DiagramModel : null);
        }
        else if (e.PropertyName == nameof(GraphDocumentViewModel.Verbosity))
        {
            // C-9: a verbosity change re-names every peer without a load.
            RenamePeers();
        }
    }

    private void BindDiagram(GraphDiagramModel? diagram)
    {
        if (ReferenceEquals(_diagram, diagram))
        {
            return;
        }
        if (_diagram is { } old)
        {
            old.Driver.FrameApplied -= OnFrameApplied;
        }
        _diagram = diagram;
        if (_model is { } document)
        {
            // Term V6: the readback's clause and the container's Value read ONE number.
            document.DiagramZoomPercent = diagram is null ? null : () => _viewport.ZoomPercent;
        }
        // Term V1: the viewport is reset with the model; Term T3: the tier
        // latch resets with the model; Term T2: the peers are per model.
        _viewport = CanvasViewportState.Seed().WithViewSize(ActualWidth, ActualHeight);
        _didInitialFit = false;
        _lastTierB = false;
        _peers.Clear();
        _summaryPeer = null;
        ModelsBoundForTests++;
        if (diagram is not null)
        {
            diagram.Driver.FrameApplied += OnFrameApplied;
        }
        RebuildVisibleSet();
        Redraw();
    }

    private void OnTopologyChanged()
    {
        RebuildVisibleSet();
        Redraw();
    }

    /// <summary>Term G5's renderer half: an applied frame refreshes the
    /// bounds, fits ONCE on the first non-empty frame of a model, rebuilds
    /// the hit grid and redraws.</summary>
    private void OnFrameApplied(LayoutFrame frame)
    {
        _ = frame;
        if (_diagram is null)
        {
            return;
        }
        if (!_didInitialFit && _diagram.Positions.Count > 0)
        {
            _didInitialFit = true;
            FitVisible();
            FitsForTests++;
        }
        BuildGrid();
        Redraw();
    }

    private void OnViewStateChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GraphViewState.SelectedKey))
        {
            // Term N4: an OUTSIDE write of the key moves the ring and scrolls
            // into view with no line.
            if (SelectedId is { } id)
            {
                ScrollIntoView(id);
            }
            DrawRing();
            UpdatePeerSelection();
        }
    }

    // --- Term T1: the visible set and the tier ---------------------------------------

    /// <summary>The live model this view draws, null between models.</summary>
    internal GraphDiagramModel? Diagram => _diagram;

    /// <summary>VisibleIds: the accepted topology's ids, in its order, filtered
    /// to ids the model knows.</summary>
    internal IReadOnlyList<ulong> VisibleIds => _visibleIds;

    internal bool IsTierB => _lastTierB;

    internal IReadOnlyDictionary<ulong, GraphTopologyNode> Entries => _entries;

    private void RebuildVisibleSet()
    {
        _entries.Clear();
        _entriesByKey.Clear();
        GraphTopology? topology = _diagram?.Topology;
        if (_diagram is null || topology is null)
        {
            _visibleIds = [];
            _visibleSet = [];
            _visibleEdges = [];
            _peers.Clear();
            _summaryPeer = null;
            _grid.Clear();
            RaiseStructureChanged();
            return;
        }
        var visible = new List<ulong>(topology.Nodes.Length);
        foreach (GraphTopologyNode entry in topology.Nodes)
        {
            if (_diagram.NodesById.ContainsKey(entry.Id))
            {
                visible.Add(entry.Id);
                _entries[entry.Id] = entry;
                _entriesByKey[entry.StableKey] = entry.Id;
            }
        }
        _visibleIds = [.. visible];
        _visibleSet = [.. visible];
        _visibleEdges = topology.Edges;
        // The tier is decided on the VISIBLE count, inclusive at the threshold.
        bool tierB = _visibleIds.Length > GraphCoreConstants.Once.TierBThreshold;
        if (tierB)
        {
            _peers.Clear();
            _summaryPeer ??= new GraphTierSummaryAutomationPeer(this);
        }
        else
        {
            _summaryPeer = null;
            foreach (ulong stale in _peers.Keys.Where(id => !_visibleSet.Contains(id)).ToArray())
            {
                _ = _peers.Remove(stale);
            }
            foreach (ulong id in _visibleIds)
            {
                if (!_peers.ContainsKey(id))
                {
                    _peers[id] = new GraphNodeAutomationPeer(this, id);
                }
            }
        }
        // Term T3: GraphTierEntered once on the A→B edge of a model, never on
        // B→A, never on a rebuild that stays in B.
        if (tierB && !_lastTierB)
        {
            _model?.AnnounceTierEntered();
        }
        _lastTierB = tierB;
        BuildGrid();
        RaiseStructureChanged();
    }

    private void RaiseStructureChanged()
    {
        if (UIElementAutomationPeer.FromElement(this) is GraphDiagramAutomationPeer peer)
        {
            peer.ResetChildrenCache();
            if (AutomationPeer.ListenerExists(AutomationEvents.StructureChanged))
            {
                peer.RaiseAutomationEvent(AutomationEvents.StructureChanged);
            }
        }
    }

    // --- Term N1: the derived selection ---------------------------------------------

    /// <summary>Selection = the visible id whose topology entry's key equals the
    /// shared key; none while the key is hidden, absent or gone.</summary>
    internal ulong? SelectedId =>
        _model?.DiagramSelectedEntry() is { } entry && _visibleSet.Contains(entry.Id) ? entry.Id : null;

    /// <summary>Term N4: a select, announced or silent — the entry's key through
    /// the document's ONE guarded writer (refused: the ring does not move),
    /// the scroll into view, then the row line through the document's seam.</summary>
    internal bool SelectNode(ulong id, bool announce)
    {
        if (_model is not { } model || !_entries.TryGetValue(id, out GraphTopologyNode? entry))
        {
            return false;
        }
        if (!model.SelectRow(entry.StableKey))
        {
            return false;
        }
        ScrollIntoView(id);
        if (announce)
        {
            model.AnnounceRow(GraphDocumentViewModel.RowCopyOf(entry));
        }
        return true;
    }

    /// <summary>The SelectionItem pattern's RemoveFromSelection: the shared key
    /// cleared through the document under the same guard.</summary>
    internal bool ClearSelection() => _model?.ClearSelectionFromSurface() ?? false;

    /// <summary>Term N5 through the document: a ghost creates, else Open.</summary>
    internal bool ActivateNode(ulong id) =>
        _model is { } model && _entries.TryGetValue(id, out GraphTopologyNode? entry) && model.ActivateFromDiagram(entry);

    /// <summary>The summary peer's Invoke (Term T3): Term M1's switch.</summary>
    internal bool SwitchToTable() => _model?.SetMode(GraphSurfaceMode.Table) ?? false;

    // --- The peers' readings (Term T2) --------------------------------------------------

    /// <summary>Tier A's children in VisibleIds order; tier B's one summary.</summary>
    internal List<AutomationPeer> PeersInOrder()
    {
        var children = new List<AutomationPeer>();
        if (_summaryPeer is { } summary)
        {
            children.Add(summary);
            return children;
        }
        foreach (ulong id in _visibleIds)
        {
            if (_peers.TryGetValue(id, out GraphNodeAutomationPeer? peer))
            {
                children.Add(peer);
            }
        }
        return children;
    }

    internal GraphNodeAutomationPeer? PeerFor(ulong id) => _peers.GetValueOrDefault(id);

    internal GraphTierSummaryAutomationPeer? SummaryPeer => _summaryPeer;

    internal int VisibleCount => _visibleIds.Length;

    /// <summary>Name = core's render of the row copy at the LIVE verbosity.</summary>
    internal string PeerName(ulong id) =>
        _model is { } model && _entries.TryGetValue(id, out GraphTopologyNode? entry)
            ? GraphAnnouncer.RenderLabel(new GraphA11yEvent.GraphRow(model.Verbosity, GraphDocumentViewModel.RowCopyOf(entry)))
            : string.Empty;

    /// <summary>HelpText = the prefix over core's neighbour render, EMPTY when
    /// core renders nothing (0a-14: the cap is core's).</summary>
    internal string PeerHelp(ulong id)
    {
        if (!_entries.TryGetValue(id, out GraphTopologyNode? entry))
        {
            return string.Empty;
        }
        string rendered = GraphAnnouncer.RenderLabel(new GraphA11yEvent.GraphNeighborsContent([.. entry.Neighbors.Select(n => n.Label)]));
        return rendered.Length == 0 ? string.Empty : GraphPhrase.ConnectsToPrefix + rendered;
    }

    internal string PeerStatus(ulong id) =>
        _diagram is { } diagram && diagram.Pinned.Contains(id) ? GraphPhrase.PinnedStatus : string.Empty;

    internal string PeerKey(ulong id) =>
        _entries.TryGetValue(id, out GraphTopologyNode? entry) ? entry.StableKey : string.Empty;

    internal string SummaryName() =>
        GraphAnnouncer.RenderLabel(new GraphA11yEvent.GraphTierSummary((uint)_visibleIds.Length));

    private void RenamePeers()
    {
        if (!AutomationPeer.ListenerExists(AutomationEvents.PropertyChanged))
        {
            return;
        }
        foreach (GraphNodeAutomationPeer peer in _peers.Values)
        {
            peer.RaisePropertyChangedEvent(AutomationElementIdentifiers.NameProperty, null, peer.GetName());
        }
    }

    private void UpdatePeerSelection()
    {
        if (!AutomationPeer.ListenerExists(AutomationEvents.SelectionItemPatternOnElementSelected))
        {
            return;
        }
        if (SelectedId is { } id && _peers.TryGetValue(id, out GraphNodeAutomationPeer? peer))
        {
            peer.RaiseAutomationEvent(AutomationEvents.SelectionItemPatternOnElementSelected);
        }
    }

    /// <summary>The node's circle in VIEW coordinates (the scaled diameter
    /// through the viewport), empty when it has no position or is not visible.</summary>
    internal Rect NodeViewRect(ulong id)
    {
        if (_diagram is null || !_visibleSet.Contains(id) || !_diagram.Positions.TryGetValue(id, out GraphPoint? point))
        {
            return Rect.Empty;
        }
        double diameter = ScaledDiameter(id) * _viewport.Zoom;
        Point centre = ToView(point);
        return new Rect(centre.X - (diameter / 2), centre.Y - (diameter / 2), diameter, diameter);
    }

    /// <summary>The peer's rectangle, computed at READ time in screen coordinates.</summary>
    internal Rect NodeScreenRect(ulong id)
    {
        Rect view = NodeViewRect(id);
        return view.IsEmpty ? Rect.Empty : ViewToScreen(view);
    }

    internal Rect ViewToScreen(Rect view)
    {
        if (PresentationSource.FromVisual(this) is null)
        {
            return view;
        }
        Point topLeft = PointToScreen(view.TopLeft);
        Point bottomRight = PointToScreen(view.BottomRight);
        return new Rect(topLeft, bottomRight);
    }

    /// <summary>Whether the node's circle lies outside the view.</summary>
    internal bool IsNodeOffscreen(ulong id)
    {
        Rect view = NodeViewRect(id);
        return view.IsEmpty || !view.IntersectsWith(new Rect(0, 0, ActualWidth, ActualHeight));
    }

    // --- The viewport (rule V) ---------------------------------------------------------

    internal CanvasViewportState Viewport => _viewport;

    /// <summary>Term V2: the four verbs. Refused with no live model; the zoom
    /// verbs act on the view's centre; FitGraph fits the VISIBLE nodes and
    /// speaks the fit line in every case — over an empty set the fit is a
    /// no-op and the percent is unchanged (IGQ-4).</summary>
    internal GraphViewportOutcome ViewportCommand(GraphViewportVerb verb)
    {
        if (_diagram is null)
        {
            return GraphViewportOutcome.Refused;
        }
        double centreX = ActualWidth / 2;
        double centreY = ActualHeight / 2;
        switch (verb)
        {
            case GraphViewportVerb.ZoomIn:
                Commit(_viewport.ZoomedIn(centreX, centreY));
                return new GraphViewportOutcome.Zoomed(_viewport.ZoomPercent, false);
            case GraphViewportVerb.ZoomOut:
                Commit(_viewport.ZoomedOut(centreX, centreY));
                return new GraphViewportOutcome.Zoomed(_viewport.ZoomPercent, false);
            case GraphViewportVerb.ActualSize:
                Commit(_viewport.AtActualSize(centreX, centreY));
                return new GraphViewportOutcome.Zoomed(_viewport.ZoomPercent, false);
            case GraphViewportVerb.FitGraph:
                FitVisible();
                return new GraphViewportOutcome.Zoomed(_viewport.ZoomPercent, true);
            default:
                return GraphViewportOutcome.Refused;
        }
    }

    /// <summary>Term V5: a pan by the wheel's delta or a drag — instant.</summary>
    internal void PanBy(double dx, double dy) => Commit(_viewport.PannedTo(_viewport.PanX + dx, _viewport.PanY + dy));

    /// <summary>Term V5: one zoom step centre-preserving on the pointer.</summary>
    internal void ZoomStepAt(Point view, bool zoomIn) =>
        Commit(zoomIn ? _viewport.ZoomedIn(view.X, view.Y) : _viewport.ZoomedOut(view.X, view.Y));

    private void Commit(CanvasViewportState viewport)
    {
        uint before = _viewport.ZoomPercent;
        _viewport = viewport;
        Redraw();
        if (before != viewport.ZoomPercent && UIElementAutomationPeer.FromElement(this) is GraphDiagramAutomationPeer peer
            && AutomationPeer.ListenerExists(AutomationEvents.PropertyChanged))
        {
            peer.RaisePropertyChangedEvent(ValuePatternIdentifiers.ValueProperty, GraphDiagramAutomationPeer.ZoomValue(before), GraphDiagramAutomationPeer.ZoomValue(viewport.ZoomPercent));
        }
    }

    /// <summary>The layout-space bounds of the VISIBLE nodes' positions; null
    /// when none has a position.</summary>
    internal Rect? VisibleBounds()
    {
        if (_diagram is null)
        {
            return null;
        }
        bool any = false;
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        foreach (ulong id in _visibleIds)
        {
            if (!_diagram.Positions.TryGetValue(id, out GraphPoint? point))
            {
                continue;
            }
            any = true;
            minX = Math.Min(minX, point.X);
            minY = Math.Min(minY, point.Y);
            maxX = Math.Max(maxX, point.X);
            maxY = Math.Max(maxY, point.Y);
        }
        return any ? new Rect(minX, minY, maxX - minX, maxY - minY) : null;
    }

    /// <summary>The mac's fit: a zero-size bounds inflated, the padding added,
    /// the zoom the smaller of the two ratios (clamped), the bounds centred.</summary>
    private void FitVisible()
    {
        if (VisibleBounds() is not { } bounds || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }
        Rect target = bounds;
        if (target.Width <= 0 && target.Height <= 0)
        {
            target.Inflate(EmptyBoundsInflation, EmptyBoundsInflation);
        }
        target.Inflate(FitPadding, FitPadding);
        double zoom = Math.Clamp(
            Math.Min(ActualWidth / Math.Max(target.Width, 1), ActualHeight / Math.Max(target.Height, 1)),
            CanvasViewportState.MinZoom,
            CanvasViewportState.MaxZoom);
        double centreX = target.X + (target.Width / 2);
        double centreY = target.Y + (target.Height / 2);
        double panX = (ActualWidth / 2) - (centreX * zoom);
        double panY = (ActualHeight / 2) - (centreY * zoom);
        Commit(_viewport.WithZoom(zoom, 0, 0).PannedTo(panX, panY));
    }

    /// <summary>Term V5: the pan that keeps the node's centre inside the
    /// margin, silent; nothing when it already is.</summary>
    internal void ScrollIntoView(ulong id)
    {
        if (_diagram is null || !_diagram.Positions.TryGetValue(id, out GraphPoint? point) || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }
        Point centre = ToView(point);
        double dx = 0;
        double dy = 0;
        if (centre.X < ScrollMargin)
        {
            dx = ScrollMargin - centre.X;
        }
        else if (centre.X > ActualWidth - ScrollMargin)
        {
            dx = (ActualWidth - ScrollMargin) - centre.X;
        }
        if (centre.Y < ScrollMargin)
        {
            dy = ScrollMargin - centre.Y;
        }
        else if (centre.Y > ActualHeight - ScrollMargin)
        {
            dy = (ActualHeight - ScrollMargin) - centre.Y;
        }
        if (dx != 0 || dy != 0)
        {
            PanBy(dx, dy);
            ScrollsForTests++;
        }
    }

    private Point ToView(GraphPoint point) =>
        new((point.X * _viewport.Zoom) + _viewport.PanX, (point.Y * _viewport.Zoom) + _viewport.PanY);

    private Point? ToLayout(Point view) =>
        _viewport.Zoom == 0 ? null : new Point((view.X - _viewport.PanX) / _viewport.Zoom, (view.Y - _viewport.PanY) / _viewport.Zoom);

    // --- Term T4/T5: the sizes and the styles ------------------------------------------

    private GraphDisplay Display => _model?.DiagramDisplay ?? DefaultDisplay.Value;

    private static readonly Lazy<GraphDisplay> DefaultDisplay = new(() => SlateUniffiMethods.GraphConfigDefault().Display);

    /// <summary>The topology entry's diameter (core's curve through the record)
    /// times the display multiplier, in layout units.</summary>
    internal double ScaledDiameter(ulong id) =>
        (_entries.TryGetValue(id, out GraphTopologyNode? entry) ? entry.Diameter : 0) * Display.NodeSizeMultiplier;

    private Brush BrushOf(string key) => TryFindResource(key) as Brush ?? Brushes.Transparent;

    private GraphGroup? GroupOf(GraphTopologyNode entry)
    {
        if (entry.Group is not { } index || _model is null)
        {
            return null;
        }
        IReadOnlyList<GraphGroup> groups = _model.ViewState.Groups;
        return index < groups.Count ? groups[(int)index] : null;
    }

    private static double[]? DashOf(GraphRingStyle style) => style switch
    {
        GraphRingStyle.Dashed => DashedDash,
        GraphRingStyle.Dotted => DottedDash,
        _ => null,
    };

    // The dash patterns are SHARED instances: RedrawStyles keys its pens by
    // the array's identity, never by a culture-bound rendering of its numbers.
    private static readonly double[] DashedDash = [4, 2];
    private static readonly double[] DottedDash = [1, 2];

    /// <summary>Term T5: the fill and the ring — never colour alone. A grouped
    /// node takes the group's token and a heavier, patterned ring; an
    /// ungrouped note, attachment or hollow dashed ghost is ringed thin in
    /// the outline token.</summary>
    private (Brush Fill, Pen Ring, string FillKey, double Width, double[]? Dash) StyleOf(GraphTopologyNode entry, RedrawStyles styles)
    {
        if (GroupOf(entry) is { } group)
        {
            string key = GroupBrushKeys[(int)group.ColorToken];
            double width = group.RingStyle == GraphRingStyle.Double ? 4 : 3;
            double[]? dash = DashOf(group.RingStyle);
            return (styles.Brush(key), styles.Pen(RingBrushKey, width, dash), key, width, dash);
        }
        switch (entry.Kind)
        {
            case GraphNodeKind.Ghost:
                return (styles.Brush(SurfaceBrushKey), styles.Pen(OutlineBrushKey, 1.5, GhostDash), SurfaceBrushKey, 1.5, GhostDash);
            case GraphNodeKind.Attachment:
                return (styles.Brush(AttachmentBrushKey), styles.Pen(OutlineBrushKey, 1.5, null), AttachmentBrushKey, 1.5, null);
            default:
                return (styles.Brush(NoteBrushKey), styles.Pen(OutlineBrushKey, 1.5, null), NoteBrushKey, 1.5, null);
        }
    }

    private static readonly double[] GhostDash = [3, 2];

    /// <summary>The pen key's equality: the token by value, the width by
    /// value, the dash by REFERENCE — the patterns are the renderer's shared
    /// statics, so identity is exact and no number is ever formatted.</summary>
    private sealed class PenKeyComparer : IEqualityComparer<(string Key, double Width, double[]? Dash)>
    {
        public static readonly PenKeyComparer Instance = new();

        public bool Equals((string Key, double Width, double[]? Dash) x, (string Key, double Width, double[]? Dash) y) =>
            string.Equals(x.Key, y.Key, StringComparison.Ordinal) && x.Width == y.Width && ReferenceEquals(x.Dash, y.Dash);

        public int GetHashCode((string Key, double Width, double[]? Dash) obj) =>
            HashCode.Combine(obj.Key, obj.Width, obj.Dash is null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj.Dash));
    }

    /// <summary>One redraw's resources: each token looked up ONCE per pass
    /// (the theme's brush, or a frozen copy of it), each (token, width,
    /// dash) pen built once and frozen — so a tier-A pass over 1,500 nodes
    /// shares a handful of frozen resources instead of carrying a live pen
    /// and two dictionary walks per node (§K's pan budget, D-18). A pass
    /// never outlives its redraw, so a theme swap is seen on the next one.</summary>
    private sealed class RedrawStyles(GraphDiagramView view)
    {
        private readonly Dictionary<string, Brush> _brushes = new(StringComparer.Ordinal);
        private readonly Dictionary<(string Key, double Width, double[]? Dash), Pen> _pens = new(PenKeyComparer.Instance);

        public Brush Brush(string key)
        {
            if (!_brushes.TryGetValue(key, out Brush? brush))
            {
                Brush found = view.BrushOf(key);
                brush = found.IsFrozen ? found : found.CloneCurrentValue();
                if (!brush.IsFrozen && brush.CanFreeze)
                {
                    brush.Freeze();
                }
                _brushes[key] = brush;
            }
            return brush;
        }

        public Pen Pen(string key, double width, double[]? dash)
        {
            (string, double, double[]?) id = (key, width, dash);
            if (!_pens.TryGetValue(id, out Pen? pen))
            {
                pen = new Pen(Brush(key), width);
                if (dash is not null)
                {
                    pen.DashStyle = new DashStyle(dash, 0);
                }
                if (pen.CanFreeze)
                {
                    pen.Freeze();
                }
                _pens[id] = pen;
            }
            return pen;
        }
    }

    // --- Term T6: the drawing ----------------------------------------------------------

    private void Redraw()
    {
        var styles = new RedrawStyles(this);
        DrawEdges(styles);
        DrawNodes(styles);
        DrawRing(styles);
        RedrawsForTests++;
    }

    private static readonly double[] ArrowSpreads = [Math.PI * 0.85, -Math.PI * 0.85];

    private void DrawEdges(RedrawStyles styles)
    {
        using DrawingContext dc = _edges.RenderOpen();
        if (_diagram is null || _visibleEdges.Length == 0)
        {
            return;
        }
        GraphDisplay display = Display;
        double thickness = Math.Max(0.5, display.LinkThickness) * _viewport.Zoom;
        Pen pen = styles.Pen(EdgeBrushKey, thickness, null);
        double arrow = 6 * _viewport.Zoom;
        foreach (GraphEdge edge in _visibleEdges)
        {
            if (!_diagram.Positions.TryGetValue(edge.SourceId, out GraphPoint? from) || !_diagram.Positions.TryGetValue(edge.TargetId, out GraphPoint? to))
            {
                continue;
            }
            Point a = ToView(from);
            Point b = ToView(to);
            dc.DrawLine(pen, a, b);
            if (display.Arrows)
            {
                double angle = Math.Atan2(b.Y - a.Y, b.X - a.X);
                foreach (double spread in ArrowSpreads)
                {
                    dc.DrawLine(pen, b, new Point(b.X + (arrow * Math.Cos(angle + spread)), b.Y + (arrow * Math.Sin(angle + spread))));
                }
            }
        }
    }

    private void DrawNodes(RedrawStyles styles)
    {
        _labelled.Clear();
        _fillKeys.Clear();
        _ringStyles.Clear();
        using DrawingContext dc = _nodes.RenderOpen();
        if (_diagram is null || _visibleIds.Length == 0)
        {
            return;
        }
        if (_lastTierB)
        {
            // Term T3: the visible dots batched into ONE visual — no labels, no
            // group tint (1.4.1: a sub-pixel dot cannot carry the ring).
            Brush dots = styles.Brush(NoteBrushKey);
            var geometry = new StreamGeometry();
            using (StreamGeometryContext sgc = geometry.Open())
            {
                foreach (ulong id in _visibleIds)
                {
                    if (!_diagram.Positions.TryGetValue(id, out GraphPoint? point))
                    {
                        continue;
                    }
                    Point centre = ToView(point);
                    sgc.BeginFigure(new Point(centre.X - 2, centre.Y), true, true);
                    sgc.ArcTo(new Point(centre.X + 2, centre.Y), new Size(2, 2), 0, false, SweepDirection.Clockwise, false, false);
                    sgc.ArcTo(new Point(centre.X - 2, centre.Y), new Size(2, 2), 0, false, SweepDirection.Clockwise, false, false);
                }
            }
            geometry.Freeze();
            dc.DrawGeometry(dots, null, geometry);
            return;
        }
        GraphDisplay display = Display;
        bool showLabels = _viewport.Zoom >= display.TextFadeZoom;
        _labelsShown = showLabels;
        double fontSize = LabelFontSize * TextScale.Factor * _viewport.Zoom;
        Brush labelBrush = styles.Brush(LabelBrushKey);
        var typeface = new Typeface(SystemFonts.MessageFontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        double dip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        foreach (ulong id in _visibleIds)
        {
            if (!_entries.TryGetValue(id, out GraphTopologyNode? entry) || !_diagram.Positions.TryGetValue(id, out GraphPoint? point))
            {
                continue;
            }
            double diameter = ScaledDiameter(id) * _viewport.Zoom;
            Point centre = ToView(point);
            (Brush fill, Pen ring, string fillKey, double width, double[]? dash) = StyleOf(entry, styles);
            _fillKeys[id] = fillKey;
            _ringStyles[id] = (width, dash);
            dc.DrawEllipse(fill, ring, centre, diameter / 2, diameter / 2);
            if (showLabels && entry.Labeled && fontSize > 0)
            {
                var text = new FormattedText(entry.Label, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface, fontSize, labelBrush, dip)
                {
                    MaxTextWidth = Math.Max(120 * _viewport.Zoom, fontSize),
                    MaxLineCount = 1,
                    Trimming = TextTrimming.CharacterEllipsis,
                    TextAlignment = TextAlignment.Center,
                };
                dc.DrawText(text, new Point(centre.X - (text.MaxTextWidth / 2), centre.Y + (diameter / 2) + 1));
                _ = _labelled.Add(id);
            }
        }
    }

    private void DrawRing(RedrawStyles? styles = null)
    {
        using DrawingContext dc = _ring.RenderOpen();
        if (_diagram is null || SelectedId is not { } selected || !_diagram.Positions.TryGetValue(selected, out GraphPoint? point))
        {
            return;
        }
        styles ??= new RedrawStyles(this);
        double radius = (ScaledDiameter(selected) * _viewport.Zoom / 2) + SelectionRingGap;
        Point centre = ToView(point);
        dc.DrawEllipse(null, styles.Pen(RingBrushKey, 3, null), centre, radius, radius);
        dc.DrawEllipse(null, styles.Pen(AccentBrushKey, 1.5, null), centre, radius, radius);
    }

    protected override int VisualChildrenCount => _visuals.Count;

    protected override Visual GetVisualChild(int index) => _visuals[index];

    protected override AutomationPeer OnCreateAutomationPeer() => new GraphDiagramAutomationPeer(this);

    // --- Term T7: the hit test ---------------------------------------------------------

    private void BuildGrid()
    {
        _grid.Clear();
        if (_diagram is null)
        {
            return;
        }
        foreach (ulong id in _visibleIds)
        {
            if (_diagram.Positions.TryGetValue(id, out GraphPoint? point))
            {
                (long X, long Y) cell = CellOf(point.X, point.Y);
                if (!_grid.TryGetValue(cell, out List<ulong>? bucket))
                {
                    _grid[cell] = bucket = [];
                }
                bucket.Add(id);
            }
        }
    }

    private static (long X, long Y) CellOf(double x, double y) =>
        ((long)Math.Floor(x / GridCell), (long)Math.Floor(y / GridCell));

    /// <summary>The nearest visible node whose scaled radius plus two contains
    /// the point, scanning the 3×3 block around its cell; both tiers.</summary>
    internal ulong? HitTest(Point view)
    {
        if (_diagram is null || ToLayout(view) is not { } layout)
        {
            return null;
        }
        (long X, long Y) cell = CellOf(layout.X, layout.Y);
        ulong? best = null;
        double bestDistance = double.MaxValue;
        for (long dx = -1; dx <= 1; dx++)
        {
            for (long dy = -1; dy <= 1; dy++)
            {
                if (!_grid.TryGetValue((cell.X + dx, cell.Y + dy), out List<ulong>? bucket))
                {
                    continue;
                }
                foreach (ulong id in bucket)
                {
                    if (!_visibleSet.Contains(id) || !_diagram.Positions.TryGetValue(id, out GraphPoint? point))
                    {
                        continue;
                    }
                    double radius = (ScaledDiameter(id) / 2) + 2;
                    double distance = Math.Sqrt(((point.X - layout.X) * (point.X - layout.X)) + ((point.Y - layout.Y) * (point.Y - layout.Y)));
                    if (distance <= radius && distance < bestDistance)
                    {
                        best = id;
                        bestDistance = distance;
                    }
                }
            }
        }
        return best;
    }

    // --- Term N3: the moves, all through core -----------------------------------------------

    private string _typeAheadBuffer = string.Empty;
    private DateTime _typeAheadStamp = DateTime.MinValue;

    /// <summary>The arrows: no selection → the first visible node; else core's
    /// spatial step over every visible id's position with the entry's
    /// neighbour ids (0b-10) — a null step moves nothing.</summary>
    internal bool SpatialMove(double dx, double dy)
    {
        if (_diagram is null || _visibleIds.Length == 0)
        {
            return false;
        }
        if (SelectedId is not { } current || !_diagram.Positions.ContainsKey(current))
        {
            return SelectNode(_visibleIds[0], announce: true);
        }
        var points = new List<GraphPoint>(_visibleIds.Length);
        foreach (ulong id in _visibleIds)
        {
            if (_diagram.Positions.TryGetValue(id, out GraphPoint? point))
            {
                points.Add(point);
            }
        }
        ulong[] neighbours = _entries.TryGetValue(current, out GraphTopologyNode? entry) ? [.. entry.Neighbors.Select(n => n.Id)] : [];
        _diagram.Count("graph_spatial_step");
        ulong? best = SlateUniffiMethods.GraphSpatialStep([.. points], neighbours, current, dx, dy);
        return best is { } next && SelectNode(next, announce: true);
    }

    /// <summary>Tab / Shift+Tab: core's structural step over the visible order,
    /// wrapping; consumed only while the visible set is non-empty.</summary>
    internal bool StructuralMove(bool forward)
    {
        if (_diagram is null || _visibleIds.Length == 0)
        {
            return false;
        }
        _diagram.Count("graph_structural_step");
        ulong? next = SlateUniffiMethods.GraphStructuralStep(_visibleIds, SelectedId, forward);
        if (next is { } id)
        {
            _ = SelectNode(id, announce: true);
        }
        return true;
    }

    /// <summary>A bare letter or digit: the one-second buffer, the first visible
    /// id whose label starts with it (OrdinalIgnoreCase; the mac's
    /// <c>lowercased().hasPrefix</c>, recorded D-D6).</summary>
    internal bool TypeAhead(string text, DateTime now)
    {
        if (_diagram is null || string.IsNullOrEmpty(text))
        {
            return false;
        }
        if ((now - _typeAheadStamp) > TimeSpan.FromSeconds(1))
        {
            _typeAheadBuffer = string.Empty;
        }
        _typeAheadStamp = now;
        _typeAheadBuffer += text;
        foreach (ulong id in _visibleIds)
        {
            if (_entries.TryGetValue(id, out GraphTopologyNode? entry) && entry.Label.StartsWith(_typeAheadBuffer, StringComparison.OrdinalIgnoreCase))
            {
                _ = SelectNode(id, announce: true);
                return true;
            }
        }
        return true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || _diagram is null)
        {
            return;
        }
        // The four viewport chords and Ctrl+Alt+Shift+I are the navigator's,
        // delivered by the surface's tunnelling handler before this; Escape is
        // the surface's ladder and bubbles (rung 3); Menu / Shift+F10 are WPF's
        // route to the persistent context menu.
        switch (e.Key)
        {
            case Key.Down:
                e.Handled = SpatialMove(0, 1) || true;
                break;
            case Key.Up:
                e.Handled = SpatialMove(0, -1) || true;
                break;
            case Key.Right:
                e.Handled = SpatialMove(1, 0) || true;
                break;
            case Key.Left:
                e.Handled = SpatialMove(-1, 0) || true;
                break;
            case Key.Tab:
                e.Handled = StructuralMove(forward: !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
                break;
            case Key.Enter:
                if (SelectedId is { } selected)
                {
                    _ = ActivateNode(selected);
                    e.Handled = true;
                }
                break;
            default:
                break;
        }
    }

    protected override void OnTextInput(TextCompositionEventArgs e)
    {
        base.OnTextInput(e);
        if (e.Handled || _diagram is null || string.IsNullOrEmpty(e.Text))
        {
            return;
        }
        char first = e.Text[0];
        if ((char.IsLetter(first) || char.IsDigit(first))
            && (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt)) == 0)
        {
            e.Handled = TypeAhead(e.Text, DateTime.UtcNow);
        }
    }

    // --- Term N4 / Term V5: the pointer ------------------------------------------------------

    private Point? _dragOrigin;
    private ulong? _hoverNode;
    private bool _pointerOnTooltip;

    /// <summary>A click selects (announced); a double-click selects silently and
    /// activates; empty space begins a drag pan.</summary>
    internal void PointerPressed(Point view, int clickCount)
    {
        _ = Focus();
        if (HitTest(view) is { } hit)
        {
            if (clickCount >= 2)
            {
                _ = SelectNode(hit, announce: false);
                _ = ActivateNode(hit);
            }
            else
            {
                _ = SelectNode(hit, announce: true);
            }
            return;
        }
        _dragOrigin = view;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        ArgumentNullException.ThrowIfNull(e);
        PointerPressed(e.GetPosition(this), e.ClickCount);
        if (_dragOrigin is not null)
        {
            _ = CaptureMouse();
        }
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (_dragOrigin is not null)
        {
            _dragOrigin = null;
            ReleaseMouseCapture();
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        ArgumentNullException.ThrowIfNull(e);
        Point view = e.GetPosition(this);
        if (_dragOrigin is { } origin && e.LeftButton == MouseButtonState.Pressed)
        {
            PanBy(view.X - origin.X, view.Y - origin.Y);
            _dragOrigin = view;
            return;
        }
        ulong? hovered = HitTest(view);
        if (hovered != _hoverNode)
        {
            _hoverNode = hovered;
            UpdateTooltip();
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _hoverNode = null;
        UpdateTooltip();
    }

    /// <summary>Term V5: the wheel pans by the delta; Ctrl+wheel zooms one step
    /// per notch, centre-preserving on the pointer; Shift+wheel pans across.</summary>
    internal void Wheel(Point view, int delta, ModifierKeys modifiers)
    {
        if (_diagram is null || delta == 0)
        {
            return;
        }
        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            ZoomStepAt(view, delta > 0);
            return;
        }
        if (modifiers.HasFlag(ModifierKeys.Shift))
        {
            PanBy(delta, 0);
            return;
        }
        PanBy(0, delta);
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        ArgumentNullException.ThrowIfNull(e);
        if (_diagram is null)
        {
            return;
        }
        Wheel(e.GetPosition(this), e.Delta, Keyboard.Modifiers);
        e.Handled = true;
    }

    // --- T68: the tooltip (a visual 1.4.13 tooltip, never announced) ---------------------

    private System.Windows.Controls.Primitives.Popup? _tooltip;
    private System.Windows.Controls.TextBlock? _tooltipText;
    private ulong? _tooltipSubject;

    private System.Windows.Controls.Primitives.Popup Tooltip
    {
        get
        {
            if (_tooltip is null)
            {
                _tooltipText = new System.Windows.Controls.TextBlock
                {
                    Padding = new Thickness(6, 4, 6, 4),
                    MaxWidth = 360,
                    TextWrapping = TextWrapping.Wrap,
                };
                var border = new System.Windows.Controls.Border { Child = _tooltipText, BorderThickness = new Thickness(1) };
                border.SetResourceReference(System.Windows.Controls.Border.BackgroundProperty, "Slate.SurfaceBrush");
                border.SetResourceReference(System.Windows.Controls.Border.BorderBrushProperty, "Slate.BorderBrush");
                _tooltipText.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "Slate.TextBrush");
                // HOVERABLE (1.4.13): the pointer travelling onto the tooltip
                // is itself a trigger, so arriving there never closes it.
                border.MouseEnter += (_, _) => _pointerOnTooltip = true;
                border.MouseLeave += (_, _) =>
                {
                    _pointerOnTooltip = false;
                    UpdateTooltip();
                };
                _tooltip = new System.Windows.Controls.Primitives.Popup
                {
                    Child = border,
                    PlacementTarget = this,
                    Placement = System.Windows.Controls.Primitives.PlacementMode.Relative,
                    StaysOpen = true,
                };
            }
            return _tooltip;
        }
    }

    /// <summary>The composed label — label, " — ", in, " in / ", out, " out"
    /// — over the topology entry (T68).</summary>
    internal string TooltipTextFor(ulong id) =>
        _entries.TryGetValue(id, out GraphTopologyNode? entry)
            ? entry.Label + GraphPhrase.TooltipSeparator + entry.InLinks.ToString(CultureInfo.InvariantCulture) + GraphPhrase.TooltipInSuffix
                + entry.OutLinks.ToString(CultureInfo.InvariantCulture) + GraphPhrase.TooltipOutSuffix
            : string.Empty;

    private void UpdateTooltip()
    {
        ulong? subject = _hoverNode is { } hovered && _entries.ContainsKey(hovered)
            ? hovered
            : _pointerOnTooltip && _tooltip is { IsOpen: true } ? _tooltipSubject : null;
        if (subject is not { } id || _diagram is null || !_diagram.Positions.ContainsKey(id))
        {
            if (_tooltip is not null)
            {
                _tooltip.IsOpen = false;
            }
            _tooltipSubject = null;
            return;
        }
        _tooltipSubject = id;
        _ = Tooltip;
        _tooltipText!.Text = TooltipTextFor(id);
        Rect rect = NodeViewRect(id);
        Tooltip.HorizontalOffset = rect.X;
        Tooltip.VerticalOffset = rect.Bottom + 2;
        Tooltip.IsOpen = true;
    }

    /// <summary>Escape's answer inside the surface's rung: the open tooltip
    /// closes; false when none is open.</summary>
    internal bool DismissTooltip()
    {
        if (_tooltip is not { IsOpen: true })
        {
            return false;
        }
        _tooltip.IsOpen = false;
        _hoverNode = null;
        _pointerOnTooltip = false;
        _tooltipSubject = null;
        return true;
    }

    // --- Term N5: the actions menu; Term N7: the pin ---------------------------------------

    private readonly System.Windows.Controls.ContextMenu _menu = new();
    private bool _menuWired;

    private void WireMenu()
    {
        if (_menuWired)
        {
            return;
        }
        _menuWired = true;
        // The menu EXISTS from construction and is MUTATED per request, never
        // replaced (the grid's and the leaf's rule): WPF opens on the Menu key,
        // Shift+F10 or a right-click by the menu that exists when the request
        // arrives.
        ContextMenu = _menu;
        AddHandler(System.Windows.Controls.ContextMenuService.ContextMenuOpeningEvent, new System.Windows.Controls.ContextMenuEventHandler(OnMenuOpening), handledEventsToo: false);
    }

    private void OnMenuOpening(object sender, System.Windows.Controls.ContextMenuEventArgs e)
    {
        // WPF raises the opening event on the element under the pointer with
        // the cursor RELATIVE to that element (PopupControlService:
        // e.GetPosition(OriginalSource)); the renderer hosts no child elements,
        // so the cursor is in the view's space — the hit grid's. A keyboard
        // request carries -1, -1.
        bool pointerRequest = e.CursorLeft >= 0 || e.CursorTop >= 0;
        if (MenuTargetFor(pointerRequest, e.CursorLeft, e.CursorTop) is not { } id || !RebuildMenu(id))
        {
            e.Handled = true;
        }
    }

    /// <summary>Term N5's target rule (IPH-1-3): a pointer request opens on
    /// the HIT node at the view point, none over empty space; a keyboard
    /// request opens on the selection.</summary>
    internal ulong? MenuTargetFor(bool pointerRequest, double left, double top) =>
        pointerRequest ? HitTest(new Point(left, top)) : SelectedId;

    /// <summary>The persistent menu mutated to the node's actions: core's
    /// per-kind titles in core's order with a disabled item's reason as its
    /// HelpText (A-8's shape), then a separator and Pin / Unpin (T67;
    /// diagram-only, exempt from §P-B parity).</summary>
    internal bool RebuildMenu(ulong id)
    {
        if (_model is not { } model || !_entries.TryGetValue(id, out GraphTopologyNode? entry))
        {
            return false;
        }
        _menu.Items.Clear();
        foreach (GraphRowActionSpec spec in model.ActionSpecs(entry.Kind))
        {
            var item = new System.Windows.Controls.MenuItem { Header = spec.Title, IsEnabled = model.IsDiagramActionEnabled(spec.Action, entry) };
            string? reason = model.ActionDisabledReason(spec.Action);
            AutomationProperties.SetHelpText(item, reason ?? spec.Title);
            if (!item.IsEnabled && reason is { Length: > 0 })
            {
                item.ToolTip = reason;
                System.Windows.Controls.ToolTipService.SetShowOnDisabled(item, true);
            }
            GraphRowAction action = spec.Action;
            item.Click += (_, _) => _ = model.ExecuteFromDiagram(action, entry);
            _ = _menu.Items.Add(item);
        }
        _ = _menu.Items.Add(new System.Windows.Controls.Separator());
        bool pinned = _diagram is { } diagram && diagram.Pinned.Contains(id);
        var pin = new System.Windows.Controls.MenuItem { Header = pinned ? GraphPhrase.UnpinLabel : GraphPhrase.PinLabel };
        AutomationProperties.SetHelpText(pin, (string)pin.Header);
        pin.Click += (_, _) => _ = TogglePin(id);
        _ = _menu.Items.Add(pin);
        return true;
    }

    /// <summary>The menu's titles for a node, in order — the drift fact's read.</summary>
    internal IReadOnlyList<string> MenuTitlesForTests(ulong id) =>
        RebuildMenu(id) ? [.. _menu.Items.OfType<System.Windows.Controls.MenuItem>().Select(item => (string)item.Header)] : [];

    internal System.Windows.Controls.ContextMenu MenuForTests => _menu;

    /// <summary>Term N7: the model's set and the session's PinNode at the node's
    /// CURRENT layout position, or UnpinNode, THROUGH the gate — a retired
    /// model refuses and nothing is spoken; else GraphPinned through the
    /// document's seam, the peer's status and the menu re-read.</summary>
    internal bool TogglePin(ulong id)
    {
        if (_diagram is null || _model is null || !_visibleSet.Contains(id) || !_diagram.Positions.TryGetValue(id, out GraphPoint? point))
        {
            return false;
        }
        if (!_diagram.TogglePin(id, (float)point.X, (float)point.Y))
        {
            return false;
        }
        _model.AnnouncePinned(_diagram.Pinned.Contains(id));
        if (_peers.TryGetValue(id, out GraphNodeAutomationPeer? peer) && AutomationPeer.ListenerExists(AutomationEvents.PropertyChanged))
        {
            peer.RaisePropertyChangedEvent(AutomationElementIdentifiers.ItemStatusProperty, null, peer.GetItemStatus());
        }
        return true;
    }

    // --- Test seams ------------------------------------------------------------------------

    internal void PointerEnteredForTests(ulong id)
    {
        _hoverNode = id;
        UpdateTooltip();
    }

    internal void PointerLeftForTests()
    {
        _hoverNode = null;
        _pointerOnTooltip = false;
        UpdateTooltip();
    }

    /// <summary>The pointer left the NODE (onto the tooltip, or away): the hover
    /// trigger alone departs.</summary>
    internal void PointerLeftNodeForTests()
    {
        _hoverNode = null;
        UpdateTooltip();
    }

    internal void PointerOnTooltipForTests(bool on)
    {
        _pointerOnTooltip = on;
        UpdateTooltip();
    }

    internal bool TooltipIsOpenForTests => _tooltip is { IsOpen: true };

    internal string TooltipTextForTests => _tooltipText?.Text ?? string.Empty;

    internal int RedrawsForTests { get; private set; }

    internal int FitsForTests { get; private set; }

    internal int ScrollsForTests { get; private set; }

    internal int ModelsBoundForTests { get; private set; }

    internal bool LabelsShownForTests => _labelsShown;

    internal IReadOnlySet<ulong> LabelledForTests => _labelled;

    internal string? FillKeyForTests(ulong id) => _fillKeys.GetValueOrDefault(id);

    internal (double Width, double[]? Dash)? RingStyleForTests(ulong id) =>
        _ringStyles.TryGetValue(id, out (double Width, double[]? Dash) style) ? style : null;

    internal int GridCellCountForTests => _grid.Count;
}

/// <summary>Term V2: the four verbs the navigator routes to the active projection.</summary>
internal enum GraphViewportVerb
{
    ZoomIn,
    ZoomOut,
    ActualSize,
    FitGraph,
}

/// <summary>Term V2: what the projection committed — Zoomed(percent, fit), or
/// Refused (no live model, Table mode); the canvas's Silent arm has no graph use.</summary>
internal abstract record GraphViewportOutcome
{
    private GraphViewportOutcome()
    {
    }

    internal static GraphViewportOutcome Refused { get; } = new RefusedOutcome();

    internal sealed record Zoomed(uint Percent, bool Fit) : GraphViewportOutcome;

    internal sealed record RefusedOutcome : GraphViewportOutcome;
}
