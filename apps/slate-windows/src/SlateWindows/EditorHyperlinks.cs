// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using ICSharpCode.AvalonEdit.Document;
using uniffi.slate_uniffi;

namespace SlateWindows;

/// <summary>E-9: revision-local validation over an edit-tracking interval index.
/// AppliedRange is authoritative coverage, never an edit invalidation delta.</summary>
internal sealed class EditorHyperlinkTree
{
    private readonly SlateTextEditor _editor;
    private readonly EditorSemanticTextProvider _provider;
    private readonly AvalonDocumentBufferSession _session;
    private readonly SlateTextEditorAutomationPeer _root;
    private readonly TextSegmentCollection<EditorLinkSegment> _segments;
    private readonly HashSet<EditorHyperlinkPeer> _members = [];
    private long _validatedRevision = -1;
    private int _validatedStart;
    private int _validatedEnd;
    private long _fullRevision = -1;
    private int _nextId;
    internal bool WasExposed { get; private set; }

    internal EditorHyperlinkTree(SlateTextEditor editor, EditorSemanticTextProvider provider,
        AvalonDocumentBufferSession session, SlateTextEditorAutomationPeer root)
    {
        _editor = editor; _provider = provider; _session = session; _root = root;
        _segments = new(session.Document);
    }

    internal static bool IsLink(EditorSemanticSpan span) => span.Kind is
        EditorSpanKind.Wikilink or EditorSpanKind.Link or EditorSpanKind.Embed
        or EditorSpanKind.Image or EditorSpanKind.Tag or EditorSpanKind.Citation;

    private void Ensure(int start, int end)
    {
        if (!_provider.CanRead) { return; }
        WasExposed = true;
        long revision = _session.Revision;
        if (_fullRevision == revision || _validatedRevision == revision
            && start >= _validatedStart && end <= _validatedEnd) { return; }

        EditorHighlightWindow window = _provider.Inspect(start, end);
        var candidates = _segments.FindOverlappingSegments(window.AppliedStartUtf16,
            window.AppliedLengthUtf16).ToArray();
        var previous = new Dictionary<(int Start, int End, Type Kind), EditorHyperlinkPeer>();
        foreach (EditorLinkSegment candidate in candidates)
        {
            if (candidate.Length > 0)
            { previous.TryAdd((candidate.StartOffset, candidate.EndOffset, candidate.Peer.Kind), candidate.Peer); }
        }
        var retained = new HashSet<EditorHyperlinkPeer>();
        var ancestors = new Stack<EditorHyperlinkPeer>();
        foreach (EditorSemanticSpan span in window.Spans.Where(IsLink)
            .OrderBy(span => span.StartUtf16).ThenByDescending(span => span.LengthUtf16)
            .ThenBy(span => span.Kind.GetType().Name, StringComparer.Ordinal))
        {
            int right = span.StartUtf16 + span.LengthUtf16;
            while (ancestors.Count > 0 && ancestors.Peek().End <= span.StartUtf16) { ancestors.Pop(); }
            // UIA needs a tree: equal/crossing intervals cannot be siblings and
            // containers simultaneously. Keep the first canonical projection.
            if (ancestors.TryPeek(out EditorHyperlinkPeer? enclosing)
                && (right > enclosing.End || span.StartUtf16 == enclosing.Start && right == enclosing.End)) { continue; }
            var key = (span.StartUtf16, right, span.Kind.GetType());
            if (!previous.TryGetValue(key, out EditorHyperlinkPeer? peer))
            {
                peer = new(this, _editor, _provider, _session.Document, span, ++_nextId);
                _segments.Add(peer.Segment);
                _members.Add(peer);
            }
            peer.Validate(span, revision, ancestors.TryPeek(out enclosing) ? enclosing : _root);
            retained.Add(peer);
            ancestors.Push(peer);
        }
        foreach (EditorLinkSegment old in candidates)
        {
            if (!retained.Contains(old.Peer))
            {
                _segments.Remove(old);
                _members.Remove(old.Peer);
            }
        }
        _validatedRevision = revision;
        _validatedStart = window.AppliedStartUtf16;
        _validatedEnd = window.AppliedEndUtf16;
        if (_validatedStart == 0 && _validatedEnd == _provider.Length) { _fullRevision = revision; }
    }

    internal bool Contains(EditorHyperlinkPeer peer)
    {
        if (!_provider.CanRead || !_members.Contains(peer) || peer.Start >= peer.End) { return false; }
        if (peer.ValidatedRevision != _session.Revision) { Ensure(peer.Start, peer.End); }
        return _members.Contains(peer) && peer.ValidatedRevision == _session.Revision;
    }

    internal List<AutomationPeer> RootChildren()
    {
        if (!_provider.CanRead) { return []; }
        Ensure(0, _provider.Length);
        return _segments.Where(segment => ReferenceEquals(segment.Peer.Parent, _root))
            .Select(segment => (AutomationPeer)segment.Peer).ToList();
    }

    private EditorHyperlinkPeer[] Overlapping(int start, int end)
    {
        if (!_provider.CanRead) { return []; }
        Ensure(start, end);
        return _segments.FindOverlappingSegments(start, end - start)
            .Select(segment => segment.Peer)
            .Where(peer => peer.ValidatedRevision == _session.Revision
                && peer.Start < end && peer.End > start).ToArray();
    }

    internal EditorHyperlinkPeer? Enclosing(int start, int end)
    {
        int probeEnd = start == end ? Math.Min(_provider.Length, start + 1) : end;
        return Overlapping(start, probeEnd).Where(peer => peer.Start <= start && peer.End >= end)
            .MinBy(peer => peer.End - peer.Start);
    }

    internal IRawElementProviderSimple[] Children(int start, int end)
    {
        EditorHyperlinkPeer? enclosing = Enclosing(start, end);
        AutomationPeer parent = enclosing is null ? _root : enclosing;
        return Overlapping(start, end).Where(peer => ReferenceEquals(peer.Parent, parent))
            .Select(peer => peer.Provider).ToArray();
    }

    internal List<AutomationPeer> Children(EditorHyperlinkPeer parent)
    {
        parent.VerifyLive();
        return Overlapping(parent.Start, parent.End).Where(peer => ReferenceEquals(peer.Parent, parent))
            .Select(peer => (AutomationPeer)peer).ToList();
    }

    internal ITextRangeProvider RangeFromChild(IRawElementProviderSimple child)
    {
        if (child is null || _root.ChildPeerFromProvider(child) is not EditorHyperlinkPeer peer || !Contains(peer))
        { throw new ArgumentException("Not a current child of this editor.", nameof(child)); }
        return _provider.Range(peer.Start, peer.End);
    }

    internal void InvalidateChildren()
    {
        // No semantic parse here: this is the stable publication boundary.
        WpfEditorPeerConnection.InvalidateChildren(_root);
        foreach (EditorLinkSegment segment in _segments) { WpfEditorPeerConnection.InvalidateChildren(segment.Peer); }
    }
}

internal sealed class EditorLinkSegment(EditorHyperlinkPeer peer, int start, int length) : TextSegment
{
    internal EditorHyperlinkPeer Peer { get; } = peer;
    internal void Initialize() { StartOffset = start; Length = length; }
}

/// <summary>One native child; text, selection, units and geometry stay with AvalonEdit.</summary>
internal sealed class EditorHyperlinkPeer : AutomationPeer, IInvokeProvider, IValueProvider
{
    private readonly EditorHyperlinkTree _tree;
    private readonly SlateTextEditor _editor;
    private readonly EditorSemanticTextProvider _provider;
    private readonly TextDocument _document;
    private readonly int _id;
    private EditorSemanticSpan _span;
    internal EditorLinkSegment Segment { get; }
    internal Type Kind { get; }
    internal int Start => Segment.StartOffset;
    internal int End => Segment.EndOffset;
    internal long ValidatedRevision { get; private set; }
    internal AutomationPeer Parent { get; private set; } = null!;

    internal EditorHyperlinkPeer(EditorHyperlinkTree tree, SlateTextEditor editor,
        EditorSemanticTextProvider provider, TextDocument document, EditorSemanticSpan span, int id)
    {
        _tree = tree; _editor = editor; _provider = provider; _document = document; _span = span; _id = id;
        Kind = span.Kind.GetType();
        Segment = new(this, span.StartUtf16, span.LengthUtf16);
        Segment.Initialize();
    }

    internal void Validate(EditorSemanticSpan span, long revision, AutomationPeer parent)
    {
        _span = span; ValidatedRevision = revision; Parent = parent;
    }

    internal void VerifyLive()
    {
        if (!_tree.Contains(this)) { throw new ElementNotAvailableException("The editor link is no longer available."); }
    }

    internal IRawElementProviderSimple Provider
    {
        get
        {
            VerifyLive();
            if (Parent is EditorHyperlinkPeer link) { _ = link.Provider; }
            else { _ = _provider.EnclosingElement; }
            WpfEditorPeerConnection.Connect(this, Parent);
            return ProviderFromPeer(this);
        }
    }

    private string? Destination => _editor.InteractionSession?.DestinationFor(_span);
    public override object? GetPattern(PatternInterface patternInterface)
    {
        VerifyLive();
        return patternInterface == PatternInterface.Invoke
            || patternInterface == PatternInterface.Value && Destination is not null ? this : null;
    }
    public bool IsReadOnly { get { VerifyLive(); return true; } }
    public string Value { get { VerifyLive(); return Destination ?? string.Empty; } }
    public void SetValue(string value) { VerifyLive(); throw new InvalidOperationException("Link destinations are read-only."); }
    public void Invoke()
    {
        VerifyLive();
        if (!_editor.IsEnabled) { throw new ElementNotEnabledException(); }
        EditorInteractionCoordinator? interactions = _editor.InteractionSession;
        long revision = ValidatedRevision;
        _editor.Dispatcher.BeginInvoke(() =>
        {
            if (!_tree.Contains(this) || ValidatedRevision != revision || !_editor.IsEnabled || interactions is null
                || !ReferenceEquals(interactions, _editor.InteractionSession) || interactions.IsDisposed) { return; }
            if (interactions.ActivateSpan(_span)) { RaiseAutomationEvent(AutomationEvents.InvokePatternOnInvoked); }
        });
    }

    protected override string GetNameCore() { VerifyLive(); return _document.GetText(Start, Math.Min(End - Start, 512)); }
    protected override AutomationControlType GetAutomationControlTypeCore() { VerifyLive(); return AutomationControlType.Hyperlink; }
    protected override string GetClassNameCore() { VerifyLive(); return "EditorHyperlink"; }
    protected override string GetAutomationIdCore() { VerifyLive(); return "EditorLink" + _id; }
    protected override Rect GetBoundingRectangleCore()
    {
        VerifyLive();
        double[] rectangles = _provider.Range(Start, End).GetBoundingRectangles();
        Rect result = Rect.Empty;
        for (int index = 0; index + 3 < rectangles.Length; index += 4)
        { result.Union(new Rect(rectangles[index], rectangles[index + 1], rectangles[index + 2], rectangles[index + 3])); }
        return result;
    }
    protected override bool IsOffscreenCore() => GetBoundingRectangleCore().IsEmpty;
    protected override bool IsEnabledCore() { VerifyLive(); return _editor.IsEnabled; }
    protected override bool IsControlElementCore() { VerifyLive(); return true; }
    protected override bool IsContentElementCore() { VerifyLive(); return true; }
    protected override bool IsKeyboardFocusableCore() { VerifyLive(); return false; }
    protected override bool HasKeyboardFocusCore() { VerifyLive(); return false; }
    protected override string GetAcceleratorKeyCore() { VerifyLive(); return ""; }
    protected override string GetAccessKeyCore() { VerifyLive(); return ""; }
    protected override string GetHelpTextCore() { VerifyLive(); return ""; }
    protected override string GetItemStatusCore() { VerifyLive(); return ""; }
    protected override string GetItemTypeCore() { VerifyLive(); return ""; }
    protected override List<AutomationPeer> GetChildrenCore() => _tree.Children(this);
    protected override Point GetClickablePointCore() { VerifyLive(); return new(double.NaN, double.NaN); }
    protected override AutomationOrientation GetOrientationCore() { VerifyLive(); return AutomationOrientation.None; }
    protected override bool IsPasswordCore() { VerifyLive(); return false; }
    protected override bool IsRequiredForFormCore() { VerifyLive(); return false; }
    protected override AutomationPeer GetLabeledByCore() { VerifyLive(); return null!; }
    protected override void SetFocusCore()
    {
        VerifyLive();
        if (!_editor.IsEnabled) { throw new ElementNotEnabledException(); }
        _editor.CaretOffset = Start;
        _editor.FocusInputOwner();
    }
}

/// <summary>A-7: connect a known canonical descendant without walking the entire document.</summary>
internal static class WpfEditorPeerConnection
{
    private static readonly MethodInfo SetParent = typeof(AutomationPeer).GetMethod("TrySetParentInfo",
        BindingFlags.Instance | BindingFlags.NonPublic, [typeof(AutomationPeer)])
        ?? throw new InvalidOperationException("WPF's peer connection contract changed.");
    private static readonly PropertyInfo ChildrenValid = typeof(AutomationPeer).GetProperty("ChildrenValid",
        BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("WPF's child-cache contract changed.");
    // ResetChildrenCache eagerly rebuilds the entire subtree. Marking WPF's
    // existing validity bit defers that work until an actual navigation request.
    internal static void InvalidateChildren(AutomationPeer peer) => ChildrenValid.SetValue(peer, false);
    internal static void Connect(AutomationPeer child, AutomationPeer parent) => SetParent.Invoke(child, [parent]);
}
