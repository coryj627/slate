// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;

namespace SlateWindows.Graph;

/// <summary>
/// W6-2 PR D (#746), Term T2: the diagram's container peer — Group, Name
/// "Graph, visual diagram" (T61) — whose children are tier A's COMPLETE node
/// peers in VisibleIds order whatever the viewport (DD-Q1), or tier B's one
/// summary peer (Term T3); Selection (single, not required, the selected
/// node's peer) and a read-only Value "Zoom N percent" over the viewport's
/// percent — the same number the Where-am-I clause carries (Term V6).
/// </summary>
internal sealed class GraphDiagramAutomationPeer : FrameworkElementAutomationPeer, IValueProvider, ISelectionProvider
{
    private readonly GraphDiagramView _view;

    public GraphDiagramAutomationPeer(GraphDiagramView view)
        : base(view)
    {
        _view = view;
    }

    /// <summary>W6-2 §F (F6, FD-7): the container's Value is core's own
    /// render of <c>GraphZoom{fit: false}</c> with its terminal period
    /// stripped — a Value is a phrase, not a sentence — never a host
    /// re-spelling of the template (the canvas's row I precedent).</summary>
    internal static string ZoomValue(uint percent) =>
        GraphAnnouncer.RenderLabel(new uniffi.slate_uniffi.GraphA11yEvent.GraphZoom(false, percent)).TrimEnd('.');

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

    protected override string GetClassNameCore() => nameof(GraphDiagramView);

    protected override string GetNameCore() => GraphPhrase.DiagramName;

    public override object? GetPattern(PatternInterface patternInterface) =>
        patternInterface is PatternInterface.Value or PatternInterface.Selection
            ? this
            : base.GetPattern(patternInterface);

    /// <summary>Children only while the diagram is the ACTIVE projection (the
    /// cluster's Visibility, the canvas board's rule).</summary>
    protected override List<AutomationPeer> GetChildrenCore() =>
        _view.Visibility == Visibility.Visible ? _view.PeersInOrder() : [];

    bool IValueProvider.IsReadOnly => true;

    string IValueProvider.Value => ZoomValue(_view.Viewport.ZoomPercent);

    void IValueProvider.SetValue(string value) =>
        throw new InvalidOperationException("the zoom value is read-only (Term V6).");

    bool ISelectionProvider.CanSelectMultiple => false;

    bool ISelectionProvider.IsSelectionRequired => false;

    IRawElementProviderSimple[] ISelectionProvider.GetSelection() =>
        _view.SelectedId is { } id && _view.PeerFor(id) is { } peer ? [ProviderFromPeer(peer)] : [];
}

/// <summary>
/// Term T2: one visible node's peer — Button; Name = core's render of the row
/// copy at the LIVE verbosity; HelpText = "Connects to: " over core's
/// neighbour render, empty when core renders nothing; ItemStatus "pinned"
/// while pinned (T64); AutomationId "GraphNode:" + the stable key; the
/// rectangle the node's circle through the viewport in screen coordinates at
/// READ time; Invoke = Term N5's activation; SelectionItem = Term N4's
/// announced select, the canvas's single-selection matrix; NOT
/// keyboard-focusable (DD-Q2). Minted once per (model, id) and reused across
/// rebuilds within a model.
/// </summary>
internal sealed class GraphNodeAutomationPeer : AutomationPeer, IInvokeProvider, ISelectionItemProvider
{
    private readonly GraphDiagramView _view;

    internal GraphNodeAutomationPeer(GraphDiagramView view, ulong id)
    {
        _view = view;
        Id = id;
    }

    internal ulong Id { get; }

    protected override string GetNameCore() => _view.PeerName(Id);

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

    protected override string GetClassNameCore() => "GraphNode";

    protected override Rect GetBoundingRectangleCore() => _view.NodeScreenRect(Id);

    public override object? GetPattern(PatternInterface patternInterface) =>
        patternInterface is PatternInterface.Invoke or PatternInterface.SelectionItem ? this : null;

    void IInvokeProvider.Invoke() => _ = _view.ActivateNode(Id);

    // The selected id AND the renderer's current peer for it (IPH-1-2): a
    // client holding this peer across the tier edge or the id's departure
    // asks a peer the container no longer exposes — it reports nothing.
    bool ISelectionItemProvider.IsSelected => _view.SelectedId == Id && ReferenceEquals(_view.PeerFor(Id), this);

    IRawElementProviderSimple? ISelectionItemProvider.SelectionContainer =>
        UIElementAutomationPeer.FromElement(_view) is { } container ? ProviderFromPeer(container) : null;

    void ISelectionItemProvider.Select() => _ = _view.SelectNode(Id, announce: true);

    void ISelectionItemProvider.AddToSelection()
    {
        if (((ISelectionItemProvider)this).IsSelected)
        {
            return;
        }
        if (_view.SelectedId is not null)
        {
            // Single selection: adding a second is the platform's
            // invalid-operation answer, never a silent replace (the canvas's D6).
            throw new InvalidOperationException("the diagram selection is single; AddToSelection with another node selected is not a replace (Term T2).");
        }
        _ = _view.SelectNode(Id, announce: true);
    }

    void ISelectionItemProvider.RemoveFromSelection()
    {
        if (((ISelectionItemProvider)this).IsSelected)
        {
            _ = _view.ClearSelection();
        }
    }

    protected override bool IsControlElementCore() => true;

    protected override bool IsContentElementCore() => true;

    protected override bool IsEnabledCore() => true;

    protected override bool IsKeyboardFocusableCore() => false;

    protected override bool HasKeyboardFocusCore() => false;

    protected override bool IsOffscreenCore() => _view.IsNodeOffscreen(Id);

    protected override string GetAutomationIdCore() => "GraphNode:" + _view.PeerKey(Id);

    protected override string GetAcceleratorKeyCore() => string.Empty;

    protected override string GetAccessKeyCore() => string.Empty;

    protected override string GetHelpTextCore() => _view.PeerHelp(Id);

    protected override string GetItemStatusCore() => _view.PeerStatus(Id);

    protected override string GetItemTypeCore() => string.Empty;

    protected override List<AutomationPeer>? GetChildrenCore() => null;

    protected override Point GetClickablePointCore()
    {
        Rect rect = _view.NodeScreenRect(Id);
        return rect.IsEmpty ? default : new Point(rect.X + (rect.Width / 2), rect.Y + (rect.Height / 2));
    }

    protected override string GetLocalizedControlTypeCore() => "button";

    protected override AutomationOrientation GetOrientationCore() => AutomationOrientation.None;

    protected override bool IsPasswordCore() => false;

    protected override bool IsRequiredForFormCore() => false;

    protected override AutomationPeer? GetLabeledByCore() => null;

    protected override void SetFocusCore()
    {
        // Not keyboard-focusable (DD-Q2): the renderer is the one focus stop.
    }
}

/// <summary>
/// Term T3: tier B's ONE child — Button; Name = core's render of
/// <c>GraphTierSummary{count}</c> (LABEL class, never posted); HelpText
/// "Switch to Table" (T62/T63); Invoke = Term M1's switch; the rectangle the
/// renderer's.
/// </summary>
internal sealed class GraphTierSummaryAutomationPeer : AutomationPeer, IInvokeProvider
{
    private readonly GraphDiagramView _view;

    internal GraphTierSummaryAutomationPeer(GraphDiagramView view)
    {
        _view = view;
    }

    protected override string GetNameCore() => _view.SummaryName();

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

    protected override string GetClassNameCore() => "GraphTierSummary";

    protected override Rect GetBoundingRectangleCore() =>
        _view.ViewToScreen(new Rect(0, 0, _view.ActualWidth, _view.ActualHeight));

    public override object? GetPattern(PatternInterface patternInterface) =>
        patternInterface == PatternInterface.Invoke ? this : null;

    void IInvokeProvider.Invoke() => _ = _view.SwitchToTable();

    protected override bool IsControlElementCore() => true;

    protected override bool IsContentElementCore() => true;

    protected override bool IsEnabledCore() => true;

    protected override bool IsKeyboardFocusableCore() => false;

    protected override bool HasKeyboardFocusCore() => false;

    protected override bool IsOffscreenCore() => false;

    protected override string GetAutomationIdCore() => "GraphTierSummary";

    protected override string GetAcceleratorKeyCore() => string.Empty;

    protected override string GetAccessKeyCore() => string.Empty;

    protected override string GetHelpTextCore() => GraphPhrase.SwitchToTable;

    protected override string GetItemStatusCore() => string.Empty;

    protected override string GetItemTypeCore() => string.Empty;

    protected override List<AutomationPeer>? GetChildrenCore() => null;

    protected override Point GetClickablePointCore() => default;

    protected override string GetLocalizedControlTypeCore() => "button";

    protected override AutomationOrientation GetOrientationCore() => AutomationOrientation.None;

    protected override bool IsPasswordCore() => false;

    protected override bool IsRequiredForFormCore() => false;

    protected override AutomationPeer? GetLabeledByCore() => null;

    protected override void SetFocusCore()
    {
    }
}
