// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using uniffi.slate_uniffi;

namespace SlateWindows.Canvas;

/// <summary>
/// The visual projection's container peer (§D D3): Group control
/// type, named "Canvas visual view", exposing THREE patterns — the
/// read-only VALUE whose text is "Zoom N percent" (DD-5, raised from
/// the engine's old/new pair), SELECTION over the single selected
/// card, and ITEM-CONTAINER so an unrealized card or labelled edge is
/// reachable by property search (the platform's virtualization door).
/// Children are the materialized topology's peers in document order:
/// the cards, then the labelled edges (#1277).
/// </summary>
internal sealed class CanvasRendererAutomationPeer :
    FrameworkElementAutomationPeer,
    IValueProvider,
    ISelectionProvider,
    IItemContainerProvider
{
    private readonly CanvasRendererView _view;

    public CanvasRendererAutomationPeer(CanvasRendererView view)
        : base(view)
    {
        _view = view;
    }

    protected override AutomationControlType GetAutomationControlTypeCore() =>
        AutomationControlType.Group;

    protected override string GetClassNameCore() => nameof(CanvasRendererView);

    protected override string GetNameCore() => CanvasPhrase.VisualBoardName;

    public override object? GetPattern(PatternInterface patternInterface) =>
        patternInterface is PatternInterface.Value
            or PatternInterface.Selection
            or PatternInterface.ItemContainer
            ? this
            : base.GetPattern(patternInterface);

    /// <summary>Children only while the board is the ACTIVE projection
    /// (its own Visibility, the projection cluster's signal). The
    /// collapsed board's engine still installs (its unlaid-out 0×0
    /// window can swallow origin nodes), and exposing those peers put
    /// offscreen, pattern-less Buttons in the UIA tree — the axe
    /// gate's ButtonShouldHavePatterns catch. A hidden surface has no
    /// automation children; the topology's cells are untouched.</summary>
    protected override System.Collections.Generic.List<AutomationPeer> GetChildrenCore() =>
        _view.Visibility is System.Windows.Visibility.Visible
            ? _view.MaterializedPeers()
            : [];

    /// <summary>The peer under a screen point: a card before any edge, and
    /// only inside the board. A labelled edge's rectangle is the union of its
    /// endpoints' (#1277), so the platform's last-child-first walk would
    /// answer the edge for every point on an endpoint card; and peers
    /// materialize up to a view beyond the board, so an unclipped walk would
    /// answer a card or an edge for a point over the sidebar or the editor
    /// beside it. Each child is tested against its rectangle clipped to the
    /// board's; an edge answers only a point on the board no card
    /// covers.</summary>
    protected override AutomationPeer GetPeerFromPointCore(System.Windows.Point point)
    {
        System.Windows.Rect board = GetBoundingRectangle();
        if (IsOffscreen() || !board.Contains(point))
        {
            return this;
        }
        System.Collections.Generic.List<AutomationPeer> children = GetChildren() ?? [];
        foreach (AutomationPeer child in Enumerable.Reverse(children.OfType<CanvasCardAutomationPeer>())
            .Concat<AutomationPeer>(Enumerable.Reverse(children.OfType<CanvasEdgeAutomationPeer>())))
        {
            System.Windows.Rect visible = child.GetBoundingRectangle();
            visible.Intersect(board);
            if (!visible.IsEmpty && visible.Contains(point))
            {
                return child;
            }
        }
        return this;
    }

    /// <summary>D3: a structure change raises the container's one
    /// children-invalidated event. It names only the container, so no card's
    /// provider (which holds the card's peer) reaches UIA by being
    /// announced.</summary>
    internal void RaiseChildrenInvalidated()
    {
        ChildrenInvalidatedForTests++;
        if (ListenerExists(AutomationEvents.StructureChanged)
            && ProviderFromPeer(this) is IRawElementProviderFragment container)
        {
            AutomationInteropProvider.RaiseStructureChangedEvent(
                container,
                new StructureChangedEventArgs(
                    StructureChangeType.ChildrenInvalidated, container.GetRuntimeId()));
        }
    }

    /// <summary>Changed child lists announced on this thread — the facts'
    /// count, listener or not.</summary>
    [ThreadStatic]
    internal static int ChildrenInvalidatedForTests;

    // --- Value (DD-5): the zoom a reader polls -------------------------

    bool IValueProvider.IsReadOnly => true;

    string IValueProvider.Value =>
        $"Zoom {_view.Engine.CommittedViewport.ZoomPercent} percent";

    void IValueProvider.SetValue(string value) =>
        throw new InvalidOperationException("the zoom value is read-only (DD-5).");

    // --- Selection: single, not required (D6's matrix) -----------------

    bool ISelectionProvider.CanSelectMultiple => false;

    bool ISelectionProvider.IsSelectionRequired => false;

    IRawElementProviderSimple[] ISelectionProvider.GetSelection()
    {
        string? selected = _view.Engine.Current?.Selection;
        return selected is not null
            && _view.PeerFor(CanvasPeerKey.Card(selected)) is { } peer
                ? [ProviderFromPeer(peer)]
                : [];
    }

    // --- ItemContainer: the unrealized cell's door (D3) ----------------

    IRawElementProviderSimple? IItemContainerProvider.FindItemByProperty(
        IRawElementProviderSimple? startAfter, int propertyId, object? value)
    {
        // The descriptor index answers a NAME search without a peer
        // existing yet; realization mints the placeholder (D3's
        // first-touch cell). Only the Name property participates —
        // the platform sends 0 for "next item".
        if (propertyId != 0
            && propertyId != AutomationElementIdentifiers.NameProperty.Id)
        {
            return null;
        }
        CanvasPeerKey? key = _view.FindByName(
            startAfter is null ? null : PeerFromProvider(startAfter),
            propertyId == 0 ? null : value as string);
        return key is { } found && _view.RealizePeer(found) is { } peer
            ? ProviderFromPeer(peer)
            : null;
    }
}

/// <summary>
/// What every peer on the board shares (§D D3): its discriminated key, its
/// cell in the installed state, the rectangle in SCREEN coordinates from
/// the placement, and the virtualized-item door, exposed ALWAYS — as a
/// PLACEHOLDER the peer answers identifying properties from the descriptor
/// index and refuses action patterns until realization. The SAME peer
/// object serves its key across materialization round trips (stable
/// identity within its renderer); a key gone from the document, or a
/// closed pane, answers element-not-available.
/// </summary>
internal abstract class CanvasBoardAutomationPeer :
    AutomationPeer,
    IVirtualizedItemProvider
{
    private protected CanvasBoardAutomationPeer(
        CanvasRendererView view, CanvasDocumentViewModel document, CanvasPeerKey key)
    {
        View = view;
        Document = document;
        Key = key;
    }

    internal CanvasPeerKey Key { get; }

    private protected CanvasRendererView View { get; }

    /// <summary>The document this peer was minted for (D3's identity is
    /// renderer, document and key).</summary>
    private protected CanvasDocumentViewModel Document { get; }

    /// <summary>The installed scene, while the board shows this peer's
    /// document AND has installed that document's state; null after the
    /// pane closes, while the board shows another canvas, and while a switch
    /// back has not installed yet, so a held peer never answers from
    /// another canvas's scene (codex round 2).</summary>
    private protected CanvasPopulation? Population =>
        ReferenceEquals(View.Model, Document) && ReferenceEquals(View.InstalledDocument, Document)
            ? View.Engine.Current?.Source.Loaded?.Population
            : null;

    /// <summary>A client may keep a pattern interface it was handed and call
    /// it after the peer changed cell, so every action rechecks at call
    /// time (codex round 2): a peer whose card or edge is gone — or whose
    /// document the board no longer shows — refuses as unavailable, and an
    /// action pattern refuses off the window, the placeholder's rule.</summary>
    private protected void RequireAvailable(bool materialized)
    {
        if (!Exists
            || Document.AppliedPublication?.Loaded?.Population is not { } applied
            || !ExistsIn(applied)
            || (materialized && !Materialized))
        {
            throw new ElementNotAvailableException();
        }
    }

    /// <summary>Whether the card or edge stands in a population. An action
    /// checks the document's APPLIED population as well as the installed
    /// one: the board installs a delete, an unlabelling or a reload a moment
    /// after the document applies it, and an action in that moment must not
    /// reach what the document already dropped (codex round 3).</summary>
    private protected abstract bool ExistsIn(CanvasPopulation population);

    /// <summary>Whether the card or edge this peer stands for is on the
    /// board now. A peer whose card or edge is gone — deleted, unlabelled,
    /// another document shown, the pane closed — exposes no pattern, not
    /// even the realize door, and reads offscreen (codex).</summary>
    private protected abstract bool Exists { get; }

    private protected bool Materialized =>
        Exists
        && View.Engine.Current?.Topology.Placements.TryGetValue(
            Key, out CanvasPeerPlacement? placement) == true
        && placement.Cell == CanvasPeerCell.Materialized;

    protected override System.Windows.Rect GetBoundingRectangleCore()
    {
        if (!Materialized || View.Engine.Current is not { } state
            || state.Topology.Placements.TryGetValue(Key, out CanvasPeerPlacement? placement) != true
            || placement.Cell != CanvasPeerCell.Materialized)
        {
            return System.Windows.Rect.Empty;
        }
        double zoom = state.Viewport.Zoom;
        var view = new System.Windows.Rect(
            (placement.X * zoom) + state.Viewport.PanX,
            (placement.Y * zoom) + state.Viewport.PanY,
            placement.Width * zoom,
            placement.Height * zoom);
        return View.ViewToScreen(view);
    }

    void IVirtualizedItemProvider.Realize()
    {
        RequireAvailable(materialized: false);
        View.Realize(Key);
    }

    protected override bool IsControlElementCore() => true;

    protected override bool IsContentElementCore() => true;

    protected override bool IsEnabledCore() => true;

    protected override bool IsKeyboardFocusableCore() => false;

    protected override bool HasKeyboardFocusCore() => false;

    protected override bool IsOffscreenCore() => !Materialized;

    protected override string GetAutomationIdCore() =>
        (Key.IsEdge ? "CanvasEdge:" : "CanvasCard:") + Key.Id;

    // The AutomationPeer abstract surface no board peer uses.
    protected override string GetAcceleratorKeyCore() => string.Empty;

    protected override string GetAccessKeyCore() => string.Empty;

    protected override string GetItemTypeCore() => string.Empty;

    protected override System.Collections.Generic.List<AutomationPeer>? GetChildrenCore() =>
        null;

    protected override System.Windows.Point GetClickablePointCore() => default;

    protected override AutomationOrientation GetOrientationCore() =>
        AutomationOrientation.None;

    protected override bool IsPasswordCore() => false;

    protected override bool IsRequiredForFormCore() => false;

    protected override System.Windows.Automation.Peers.AutomationPeer GetLabeledByCore() =>
        null!;

    protected override void SetFocusCore()
    {
    }
}

/// <summary>
/// One card's peer (§D D3): Button control type, Name from core's
/// speakable name, INVOKE selects through the announced door, and
/// SELECTION-ITEM per D6's matrix.
/// </summary>
internal sealed class CanvasCardAutomationPeer :
    CanvasBoardAutomationPeer,
    IInvokeProvider,
    ISelectionItemProvider
{
    internal CanvasCardAutomationPeer(
        CanvasRendererView view, CanvasDocumentViewModel document, CanvasPeerKey key)
        : base(view, document, key)
    {
    }

    private CanvasSceneNode? Node =>
        Population is { } population
            && population.SceneByNode.TryGetValue(Key.Id, out CanvasSceneNode? node)
            ? node
            : null;

    private protected override bool Exists => Node is not null;

    private protected override bool ExistsIn(CanvasPopulation population) =>
        population.SceneByNode.ContainsKey(Key.Id);

    protected override string GetNameCore() => Node?.SpeakableName ?? string.Empty;

    protected override AutomationControlType GetAutomationControlTypeCore() =>
        AutomationControlType.Button;

    protected override string GetClassNameCore() => "CanvasCard";

    protected override string GetLocalizedControlTypeCore() => "card";

    public override object? GetPattern(PatternInterface patternInterface)
    {
        if (patternInterface == PatternInterface.VirtualizedItem)
        {
            // ALWAYS exposed while the card exists (round 3's placeholder
            // table): the realize door must exist precisely when the card
            // is not materialized.
            return Exists ? this : null;
        }
        if (patternInterface is PatternInterface.Invoke or PatternInterface.SelectionItem)
        {
            // Action patterns are unavailable until realization —
            // the placeholder cell's rule.
            return Materialized ? this : null;
        }
        return null;
    }

    void IInvokeProvider.Invoke()
    {
        RequireAvailable(materialized: true);
        View.SelectAnnounced(Key.Id);
    }

    // --- SelectionItem, cell by cell (D6's matrix) ---------------------

    bool ISelectionItemProvider.IsSelected =>
        Exists && string.Equals(View.Engine.Current?.Selection, Key.Id, StringComparison.Ordinal);

    IRawElementProviderSimple? ISelectionItemProvider.SelectionContainer => null;

    void ISelectionItemProvider.Select()
    {
        RequireAvailable(materialized: true);
        View.SelectAnnounced(Key.Id);
    }

    void ISelectionItemProvider.AddToSelection()
    {
        RequireAvailable(materialized: true);
        if (((ISelectionItemProvider)this).IsSelected)
        {
            return;
        }
        if (View.Engine.Current?.Selection is not null)
        {
            // Single selection: adding a second is the platform's
            // invalid-operation answer, never a silent replace —
            // marks are §G's vocabulary, not this pattern's.
            throw new InvalidOperationException(
                "the canvas selection is single; AddToSelection with another "
                + "card selected is not a replace (§D D6).");
        }
        View.SelectAnnounced(Key.Id);
    }

    void ISelectionItemProvider.RemoveFromSelection()
    {
        RequireAvailable(materialized: true);
        if (((ISelectionItemProvider)this).IsSelected)
        {
            View.ClearSelectionAnnounced();
        }
    }

    protected override string GetHelpTextCore() => Node?.Title ?? string.Empty;

    protected override string GetItemStatusCore() =>
        View.Model?.Selection.IsMarked(Key.Id) == true ? "marked" : string.Empty;
}

/// <summary>
/// One labelled edge's peer (contract 34 D5, #1277): a Custom element a
/// reader hears as "connection", named by core's traversal phrase with
/// both endpoints, windowed like a card. INVOKE selects the endpoint D5's
/// end-style table names, through the cards' announced door; there is no
/// SELECTION-ITEM, since the board's seat is a node. An unlabelled edge
/// never has one (DD-2).
/// </summary>
internal sealed class CanvasEdgeAutomationPeer :
    CanvasBoardAutomationPeer,
    IInvokeProvider
{
    internal CanvasEdgeAutomationPeer(
        CanvasRendererView view, CanvasDocumentViewModel document, CanvasPeerKey key)
        : base(view, document, key)
    {
    }

    private CanvasSceneEdge? Edge =>
        Population is { } population ? CanvasRendererView.NamedEdge(population, Key.Id) : null;

    private protected override bool Exists => Edge is not null;

    private protected override bool ExistsIn(CanvasPopulation population) =>
        CanvasRendererView.NamedEdge(population, Key.Id) is not null;

    protected override string GetNameCore() => Edge?.SpeakableName ?? string.Empty;

    protected override AutomationControlType GetAutomationControlTypeCore() =>
        AutomationControlType.Custom;

    protected override string GetClassNameCore() => "CanvasEdge";

    protected override string GetLocalizedControlTypeCore() => "connection";

    public override object? GetPattern(PatternInterface patternInterface)
    {
        if (patternInterface == PatternInterface.VirtualizedItem)
        {
            // ALWAYS exposed while the edge is a peer, as a card's is
            // (D3's placeholder cell).
            return Exists ? this : null;
        }
        // Invoke only once materialized; no SelectionItem — the board's
        // seat is a node.
        return patternInterface == PatternInterface.Invoke && Materialized ? this : null;
    }

    /// <summary>D5: select the end-style table's endpoint — read from the
    /// document's APPLIED edge, so a direction change already applied is the
    /// one obeyed — and say where Invoke landed even when that card is
    /// already the seat (codex round 3).</summary>
    void IInvokeProvider.Invoke()
    {
        RequireAvailable(materialized: true);
        CanvasSceneEdge applied = CanvasRendererView.NamedEdge(
            Document.AppliedPublication!.Loaded!.Population, Key.Id)!;
        View.SelectAnnounced(CanvasRendererView.DesignatedEndpoint(applied), announceWhenSeated: true);
    }

    protected override string GetHelpTextCore() => string.Empty;

    protected override string GetItemStatusCore() => string.Empty;
}
