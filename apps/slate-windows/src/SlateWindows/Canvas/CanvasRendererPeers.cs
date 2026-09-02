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
/// card, and ITEM-CONTAINER so an unrealized card is reachable by
/// property search (the platform's virtualization door). Children are
/// the materialized topology's peers, in document order.
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
/// One card's peer (§D D3): Button control type, Name from core's
/// speakable name, INVOKE selects through the announced door,
/// SELECTION-ITEM per D6's matrix, and the rectangle in SCREEN
/// coordinates from the installed state. The SAME peer object serves
/// the card across materialization round trips (stable identity
/// within its renderer); as a PLACEHOLDER it answers identifying
/// properties from the descriptor index and refuses action patterns;
/// a card gone from the document, or a closed pane, answers
/// element-not-available.
/// </summary>
internal sealed class CanvasCardAutomationPeer :
    AutomationPeer,
    IInvokeProvider,
    ISelectionItemProvider,
    IVirtualizedItemProvider
{
    private readonly CanvasRendererView _view;

    internal CanvasCardAutomationPeer(CanvasRendererView view, CanvasPeerKey key)
    {
        _view = view;
        Key = key;
    }

    internal CanvasPeerKey Key { get; }

    private CanvasSceneNode? Node =>
        _view.Engine.Current?.Source.Loaded?.Population is { } population
            && population.SceneByNode.TryGetValue(Key.Id, out CanvasSceneNode? node)
            ? node
            : null;

    private bool Materialized =>
        _view.Engine.Current?.Topology.Placements.TryGetValue(
            Key, out CanvasPeerPlacement? placement) == true
        && placement.Cell == CanvasPeerCell.Materialized;

    protected override string GetNameCore() => Node?.SpeakableName ?? string.Empty;

    protected override AutomationControlType GetAutomationControlTypeCore() =>
        AutomationControlType.Button;

    protected override string GetClassNameCore() => "CanvasCard";

    protected override System.Windows.Rect GetBoundingRectangleCore()
    {
        if (_view.Engine.Current is not { } state
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
        return _view.ViewToScreen(view);
    }

    public override object? GetPattern(PatternInterface patternInterface)
    {
        if (patternInterface == PatternInterface.VirtualizedItem)
        {
            // ALWAYS exposed (round 3's placeholder table): the
            // realize door must exist precisely when the card is not
            // materialized.
            return this;
        }
        if (patternInterface is PatternInterface.Invoke or PatternInterface.SelectionItem)
        {
            // Action patterns are unavailable until realization —
            // the placeholder cell's rule.
            return Materialized ? this : null;
        }
        return null;
    }

    void IInvokeProvider.Invoke() => _view.SelectAnnounced(Key.Id);

    // --- SelectionItem, cell by cell (D6's matrix) ---------------------

    bool ISelectionItemProvider.IsSelected =>
        string.Equals(_view.Engine.Current?.Selection, Key.Id, StringComparison.Ordinal);

    IRawElementProviderSimple? ISelectionItemProvider.SelectionContainer => null;

    void ISelectionItemProvider.Select() => _view.SelectAnnounced(Key.Id);

    void ISelectionItemProvider.AddToSelection()
    {
        if (((ISelectionItemProvider)this).IsSelected)
        {
            return;
        }
        if (_view.Engine.Current?.Selection is not null)
        {
            // Single selection: adding a second is the platform's
            // invalid-operation answer, never a silent replace —
            // marks are §G's vocabulary, not this pattern's.
            throw new InvalidOperationException(
                "the canvas selection is single; AddToSelection with another "
                + "card selected is not a replace (§D D6).");
        }
        _view.SelectAnnounced(Key.Id);
    }

    void ISelectionItemProvider.RemoveFromSelection()
    {
        if (((ISelectionItemProvider)this).IsSelected)
        {
            _view.ClearSelectionAnnounced();
        }
    }

    void IVirtualizedItemProvider.Realize() => _view.Realize(Key);

    protected override bool IsControlElementCore() => true;

    protected override bool IsContentElementCore() => true;

    protected override bool IsEnabledCore() => true;

    protected override bool IsKeyboardFocusableCore() => false;

    protected override bool HasKeyboardFocusCore() => false;

    protected override bool IsOffscreenCore() => !Materialized;

    // The AutomationPeer abstract surface the card does not use.
    protected override string GetAutomationIdCore() =>
        (Key.IsEdge ? "CanvasEdge:" : "CanvasCard:") + Key.Id;

    protected override string GetAcceleratorKeyCore() => string.Empty;

    protected override string GetAccessKeyCore() => string.Empty;

    protected override string GetHelpTextCore() => Node?.Title ?? string.Empty;

    protected override string GetItemStatusCore() =>
        _view.Model?.Selection.IsMarked(Key.Id) == true ? "marked" : string.Empty;

    protected override string GetItemTypeCore() => string.Empty;

    protected override System.Collections.Generic.List<AutomationPeer>? GetChildrenCore() =>
        null;

    protected override System.Windows.Point GetClickablePointCore() => default;

    protected override string GetLocalizedControlTypeCore() => "card";

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
