// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

// THROWAWAY W7-1 experiment. Only the isolated prototype branch includes this.
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using ICSharpCode.AvalonEdit.Document;
using uniffi.slate_uniffi;

namespace SlateWindows;

internal sealed class EditorHyperlinkPrototypeTree(
    SlateTextEditor editor, EditorSemanticTextProvider provider,
    AvalonDocumentBufferSession session, AutomationPeer parent)
{
    private long _revision = -1;
    private int _nextId;
    private List<EditorHyperlinkPrototypePeer> _links = [];

    internal IReadOnlyList<EditorHyperlinkPrototypePeer> Links()
    {
        if (!provider.CanRead) { return []; }
        if (_revision == session.Revision) { return _links; }
        var next = new List<EditorHyperlinkPrototypePeer>();
        foreach (EditorSemanticSpan span in session.InspectInRange(0, session.Document.TextLength).Spans)
        {
            if (span.Kind is not (EditorSpanKind.Wikilink or EditorSpanKind.Link
                or EditorSpanKind.Embed or EditorSpanKind.Image or EditorSpanKind.Tag or EditorSpanKind.Citation))
            { continue; }
            int end = span.StartUtf16 + span.LengthUtf16;
            var retained = _links.FirstOrDefault(link => link.Alive
                && link.Start == span.StartUtf16 && link.End == end && link.Kind == span.Kind.GetType());
            next.Add(retained ?? new EditorHyperlinkPrototypePeer(
                this, editor, provider, session.Document, parent, span, ++_nextId));
        }
        _links = next;
        _revision = session.Revision;
        return _links;
    }

    internal bool Contains(EditorHyperlinkPrototypePeer peer) => Links().Contains(peer);
    internal EditorHyperlinkPrototypePeer? Enclosing(int start, int end) => Links()
        .Where(link => start >= link.Start && end <= link.End && start < link.End)
        .OrderBy(link => link.End - link.Start).FirstOrDefault();
    internal IRawElementProviderSimple[] Children(int start, int end)
    {
        if (Enclosing(start, end) is not null) { return []; }
        return Links().Where(link => link.Start < end && link.End > start)
            .Select(link => link.Provider).ToArray();
    }

    internal ITextRangeProvider RangeFromChild(IRawElementProviderSimple child)
    {
        if (((SlateTextEditorAutomationPeer)parent).PrototypePeerFromProvider(child) is not EditorHyperlinkPrototypePeer link
            || !Contains(link)) { throw new ArgumentException("Not a live child of this editor.", nameof(child)); }
        return provider.Range(link.Start, link.End);
    }
}

internal sealed class EditorHyperlinkPrototypePeer : AutomationPeer, IInvokeProvider
{
    private readonly EditorHyperlinkPrototypeTree _tree;
    private readonly SlateTextEditor _editor;
    private readonly EditorSemanticTextProvider _provider;
    private readonly TextDocument _document;
    private readonly AutomationPeer _parent;
    private readonly TextAnchor _start;
    private readonly TextAnchor _end;
    private readonly int _id;

    internal EditorHyperlinkPrototypePeer(EditorHyperlinkPrototypeTree tree,
        SlateTextEditor editor, EditorSemanticTextProvider provider, TextDocument document,
        AutomationPeer parent, EditorSemanticSpan span, int id)
    {
        _tree = tree; _editor = editor; _provider = provider; _document = document; _parent = parent;
        Kind = span.Kind.GetType(); _id = id;
        _start = document.CreateAnchor(span.StartUtf16);
        _end = document.CreateAnchor(span.StartUtf16 + span.LengthUtf16);
        _start.MovementType = AnchorMovementType.AfterInsertion;
        _end.MovementType = AnchorMovementType.BeforeInsertion;
    }

    internal Type Kind { get; }
    internal bool Alive => !_start.IsDeleted && !_end.IsDeleted && _start.Offset < _end.Offset;
    internal int Start => _start.Offset;
    internal int End => _end.Offset;
    internal IRawElementProviderSimple Provider
    {
        get { _parent.GetChildren(); return ProviderFromPeer(this); }
    }
    private void VerifyLive()
    {
        if (!Alive || !_tree.Contains(this)) { throw new ElementNotAvailableException(); }
    }
    public override object? GetPattern(PatternInterface patternInterface) =>
        patternInterface == PatternInterface.Invoke ? this : null;
    public void Invoke()
    {
        VerifyLive();
        _editor.Dispatcher.BeginInvoke(() =>
        {
            VerifyLive();
            if (_editor.InteractionSession is { } interactions)
            {
                interactions.ActivateAt(Start, EditorInteractionOrigin.Keyboard);
            }
            else { _editor.PrototypeLinkInvoked?.Invoke(Start); }
        });
    }
    protected override string GetNameCore() => Alive ? _document.GetText(Start, End - Start) : "Removed link";
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Hyperlink;
    protected override string GetClassNameCore() => "PrototypeEditorHyperlink";
    protected override string GetAutomationIdCore() => "PrototypeLink" + _id;
    protected override Rect GetBoundingRectangleCore()
    {
        if (!Alive || !_provider.CanRead) { return Rect.Empty; }
        double[] rectangles = _provider.Range(Start, End).GetBoundingRectangles();
        Rect result = Rect.Empty;
        for (int i = 0; i + 3 < rectangles.Length; i += 4)
        { result.Union(new Rect(rectangles[i], rectangles[i + 1], rectangles[i + 2], rectangles[i + 3])); }
        return result;
    }
    protected override bool IsOffscreenCore() => GetBoundingRectangleCore().IsEmpty;
    protected override bool IsEnabledCore() => Alive && _editor.IsEnabled;
    protected override bool IsControlElementCore() => true;
    protected override bool IsContentElementCore() => true;
    protected override bool IsKeyboardFocusableCore() => false;
    protected override bool HasKeyboardFocusCore() => false;
    protected override string GetAcceleratorKeyCore() => "Control+Enter";
    protected override string GetAccessKeyCore() => "";
    protected override string GetHelpTextCore() => "Prototype link from the canonical editor spans";
    protected override string GetItemStatusCore() => "";
    protected override string GetItemTypeCore() => "";
    protected override List<AutomationPeer>? GetChildrenCore() => null;
    protected override Point GetClickablePointCore() => new(double.NaN, double.NaN);
    protected override AutomationOrientation GetOrientationCore() => AutomationOrientation.None;
    protected override bool IsPasswordCore() => false;
    protected override bool IsRequiredForFormCore() => false;
    protected override AutomationPeer GetLabeledByCore() => null!;
    protected override void SetFocusCore()
    {
        VerifyLive(); _editor.CaretOffset = Start; _editor.FocusInputOwner();
    }
}
