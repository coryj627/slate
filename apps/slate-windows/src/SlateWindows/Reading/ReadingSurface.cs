// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using WpfList = System.Windows.Documents.List;

namespace SlateWindows.Reading;

/// <summary>
/// The reading view's document host (W3-1, #728): a read-only
/// <see cref="RichTextBox"/>.
///
/// The container spike measured the alternatives out:
/// <c>FlowDocumentScrollViewer</c> has no keyboard caret (arrows
/// re-announce one line forever), and an <c>ItemsControl</c> of blocks
/// exposes no Text pattern at all. The read-only RichTextBox keeps the
/// document Text pattern, adds a real caret and native table semantics,
/// and supplies <c>Hyperlink</c> peers natively — which is why the
/// structural peer walk below adds headings and lists but never links.
///
/// AutomationId <c>ReadingSurface</c> is the identity contract
/// (`w3_spec` §W3-1): externally-dependable API that the W-E7 AT-idiom
/// layers key on. Renaming it is a breaking change to shipped
/// assistive-technology configuration, not a refactor.
/// </summary>
internal sealed class ReadingSurface : RichTextBox
{
    public ReadingSurface()
    {
        IsReadOnly = true;
        IsReadOnlyCaretVisible = true;
        // A read-only viewer records no undo — and it MUST not: undo
        // preservation XamlWriter-serializes removed content, and the
        // first post-click merge died on the task checkbox's stamped
        // payload ("Cannot serialize a generic type" — field root
        // cause, 2026-07-26: every toggle became "could not load" and
        // the caret went dead).
        IsUndoEnabled = false;
        // Keeps hyperlinks live in a read-only document.
        IsDocumentEnabled = true;
        AcceptsReturn = false;
        AcceptsTab = false;
        BorderThickness = new Thickness(0);
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        // The reading surface renders on the surface token per §10.4 —
        // the APCA pairs (#1050) are gated against it.
        this.SetResourceReference(BackgroundProperty, "Slate.SurfaceBrush");
        this.SetResourceReference(ForegroundProperty, "Slate.TextBrush");
        SpellCheck.IsEnabled = false;

        AutomationProperties.SetAutomationId(this, "ReadingSurface");
        AutomationProperties.SetName(this, "Reading view");

        // ONE document for the surface's whole life. Replacing the
        // Document property on a live RichTextBox breaks the UIA
        // binding: the automation peer stays wired to the original text
        // container, so a screen reader sees "blank" forever and caret
        // moves go silent while the visual caret works — the 2026-07-27
        // manual-pass finding. Content arrives by MERGING blocks into
        // this document instead (ApplyBuiltDocument).
        Document = new FlowDocument
        {
            FontSize = 15,
            PagePadding = new Thickness(16),
            ColumnWidth = double.PositiveInfinity,
        };
        Document.SetResourceReference(
            FlowDocument.FontFamilyProperty, "Slate.ReadingFontFamily");

        // Activation: clicks and Enter on links/cards route by the run
        // kind the builder STAMPED on the element — never by parsing
        // anything back out of a URI (§10.3/§10.8). Handled at the
        // surface so the builder stays a pure projector.
        AddHandler(
            System.Windows.Documents.Hyperlink.ClickEvent,
            new System.Windows.RoutedEventHandler((_, args) =>
            {
                if (args.Source is System.Windows.Documents.Hyperlink
                    {
                        Tag: uniffi.slate_uniffi.ReadingInlineRunKind kind
                    }
                    && _model is { } model)
                {
                    model.Activate(kind);
                    args.Handled = true;
                }
            }));
        AddHandler(
            System.Windows.Controls.Primitives.ButtonBase.ClickEvent,
            new System.Windows.RoutedEventHandler((_, args) =>
            {
                // Copy buttons FIRST: both they and embed cards are
                // Buttons with string Tags, and only the marker tells
                // them apart (W3-4).
                if (args.Source is System.Windows.Controls.Button
                    {
                        Tag: string codeSource
                    } copyButton
                    && ReadingSemantics.IsCodeCopy(copyButton)
                    && _model is { } copyModel)
                {
                    copyModel.CopyCode(codeSource);
                    args.Handled = true;
                    return;
                }
                // Jump buttons whose target core ALREADY RESOLVED
                // navigate directly (W3-5 round 1: nested targets are
                // absent from the host's record snapshot, so a
                // re-match would dead-end on content the card shows).
                if (args.Source is System.Windows.Controls.Button jumpButton
                    && ReadingSemantics.TryGetEmbedJump(
                        jumpButton, out string jumpPath, out var jumpAnchor)
                    && _model is { } jumpModel)
                {
                    jumpModel.ActivateResolvedEmbedSource(jumpPath, jumpAnchor);
                    args.Handled = true;
                    return;
                }
                if (args.Source is System.Windows.Controls.Button
                    {
                        Tag: string embedKey
                    }
                    && _model is { } model)
                {
                    model.Activate(
                        new uniffi.slate_uniffi.ReadingInlineRunKind.Embed(embedKey));
                    args.Handled = true;
                    return;
                }

                // Task checkbox (mouse, Space, or a UIA TogglePattern
                // call — all raise Click). The checkbox never holds
                // state: revert WPF's optimistic flip and route the
                // stamped source range through the core task command;
                // the re-projection renders the real outcome, and the
                // canonical announcements ("Task completed." /
                // conflict / unsaved refusal) narrate it.
                if (args.Source is System.Windows.Controls.CheckBox box
                    && ReadingSemantics.TryDecodeTaskRange(
                        box.Tag, out ulong taskStart, out ulong taskEnd)
                    && _model is { } taskModel)
                {
                    box.IsChecked = box.IsChecked != true;
                    taskModel.ToggleTaskAt(taskStart, taskEnd);
                    // The click focused the checkbox — an element the
                    // re-projection is about to destroy. Return focus
                    // to the document so the reader stays in caret
                    // context and WPF never recovers focus from a
                    // removed element.
                    _ = Focus();
                    args.Handled = true;
                }
            }));

        // Entering reading mode swaps this surface in for the editor;
        // focus must land in the document or the mode toggle strands
        // keyboard users on a hidden control. (IsVisibleChanged is an
        // event, not a virtual, on UIElement.)
        IsVisibleChanged += (_, args) =>
        {
            if (args.NewValue is true && _lastMerged is not null)
            {
                _ = Focus();
            }
        };
    }

    /// <summary>
    /// The view-model attach point: XAML binds <c>Model</c>
    /// (RichTextBox.Document is a plain CLR property, so it cannot be
    /// the binding target itself). The surface subscribes for document
    /// swaps, keeps its navigator's landmark index current, and owns
    /// the navigator lifetime.
    /// </summary>
    public static readonly DependencyProperty ModelProperty =
        DependencyProperty.Register(
            nameof(Model), typeof(object), typeof(ReadingSurface),
            new PropertyMetadata(null, OnModelChanged));

    private ReadingNavigator? _navigator;
    private ReadingContentViewModel? _model;

    public object? Model
    {
        get => GetValue(ModelProperty);
        set => SetValue(ModelProperty, value);
    }

    private static void OnModelChanged(
        DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var surface = (ReadingSurface)d;
        if (surface._model is { } previous)
        {
            previous.PropertyChanged -= surface.Model_PropertyChanged;
            previous.BlocksAppended -= surface.Model_BlocksAppended;
            // Kill the outgoing model's stream BEFORE anything else:
            // its chunk continuations hold list objects that live in
            // THIS surface's document and would keep growing them
            // under whatever binds next.
            previous.OnSurfaceDetached();
        }
        surface._model = e.NewValue as ReadingContentViewModel;
        if (surface._model is { } model)
        {
            surface._navigator ??= new ReadingNavigator(surface, model.Announce);
            model.PropertyChanged += surface.Model_PropertyChanged;
            model.BlocksAppended += surface.Model_BlocksAppended;
            // A model swap is a different projection (navigation, tab
            // switch): the merge's caret preservation is for SAME-note
            // re-projections only, so park the caret before applying.
            surface.CaretPosition = surface.Document.ContentStart;
            if (ReferenceEquals(surface._lastMerged, model.Document)
                && model.Document is not null
                && model.ProjectionComplete)
            {
                // The persistent document already shows this exact,
                // COMPLETE projection (reading tab → editor tab →
                // back): free. Completeness matters — a stream this
                // surface's own detach canceled leaves _lastMerged
                // pointing at a torso that must re-project instead.
                return;
            }
            // Any other rebind is a DIFFERENT note (or an untrusted
            // projection): the outgoing content leaves the tree NOW —
            // waiting for the incoming publish would let readers and
            // AT read the previous note under the new tab's identity.
            // An accessible placeholder stands in until the projection
            // (or its failure notice) arrives through PropertyChanged.
            surface.ClearForModelSwitch();
            model.EnsureProjected();
        }
    }

    private void ClearForModelSwitch()
    {
        Document.Blocks.Clear();
        var placeholder = new Paragraph(new Run("Loading reading view…"))
        {
            FontStyle = System.Windows.FontStyles.Italic,
        };
        System.Windows.Automation.AutomationProperties.SetAutomationId(
            placeholder, "ReadingLoadingNotice");
        Document.Blocks.Add(placeholder);
        // Park the caret at the placeholder's start: a leftover offset
        // would be "preserved" into the incoming note by the next
        // merge, skipping its focus-on-content path.
        CaretPosition = Document.ContentStart;
        _landmarks = Array.Empty<ReadingLandmark>();
        _navigator?.SetLandmarks(_landmarks);
        _lastMerged = null;
    }

    private void Model_PropertyChanged(
        object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ReadingContentViewModel.Document))
        {
            ApplyModel();
        }
    }

    /// <summary>
    /// Streamed chunk arrival (§W3-1 item 9): append the fragment's
    /// blocks to the persistent document and refresh the landmark
    /// index so chords cover everything rendered so far. The caret is
    /// untouched — appends only ever grow the tail.
    /// </summary>
    private void Model_BlocksAppended(FlowDocument fragment)
    {
        while (fragment.Blocks.FirstBlock is { } block)
        {
            fragment.Blocks.Remove(block);
            Document.Blocks.Add(block);
        }
        _landmarks = ReadingDocumentBuilder.CollectLandmarks(Document);
        _navigator?.SetLandmarks(_landmarks);
    }

    private FlowDocument? _lastMerged;
    private IReadOnlyList<ReadingLandmark> _landmarks = Array.Empty<ReadingLandmark>();

    internal IReadOnlyList<ReadingLandmark> LandmarksForTests => _landmarks;

    private void ApplyModel()
    {
        if (_model is not { } model)
        {
            return;
        }
        if (model.Document is { } built && !ReferenceEquals(_lastMerged, built))
        {
            ApplyBuiltDocument(built);
            _lastMerged = built;
        }
    }

    /// <summary>
    /// Move the built document's blocks into the surface's persistent
    /// document, then re-collect landmarks over the LIVE container — the
    /// built model's own pointers aim into a document this merge just
    /// emptied, and a navigator holding them would throw on the first
    /// caret comparison.
    /// </summary>
    internal void ApplyBuiltDocument(FlowDocument built)
    {
        // An embedded child (a task checkbox the user just clicked) may
        // hold keyboard focus, and this merge is about to destroy it —
        // WPF's focus recovery from a removed element is undefined
        // territory (measured in the field, 2026-07-26: dead caret
        // after checkbox clicks). Reclaim focus for the document FIRST.
        if (IsKeyboardFocusWithin && !IsKeyboardFocused)
        {
            _ = Focus();
        }

        // A re-projection replaces every block, which would collapse a
        // caret inside the old content to the document start — throwing
        // the reader to the top after every task toggle. Preserve the
        // position by symbol offset: a toggle changes one status char
        // the projection never renders, so the same offset lands on the
        // same content. Caret state is REPAIRABLE UI state: any failure
        // reading or restoring it degrades to the document start and
        // must never abort the content merge into a terminal failure.
        int caretOffset;
        try
        {
            caretOffset = Document.ContentStart.GetOffsetToPosition(CaretPosition);
        }
        catch (InvalidOperationException)
        {
            caretOffset = 0;
        }

        Document.Blocks.Clear();
        while (built.Blocks.FirstBlock is { } block)
        {
            built.Blocks.Remove(block);
            Document.Blocks.Add(block);
        }
        _landmarks = ReadingDocumentBuilder.CollectLandmarks(Document);
        _navigator?.SetLandmarks(_landmarks);

        if (caretOffset > 0)
        {
            try
            {
                CaretPosition = Document.ContentStart.GetPositionAtOffset(caretOffset)
                    ?? Document.ContentEnd;
            }
            catch (InvalidOperationException)
            {
                CaretPosition = Document.ContentStart;
            }
            return;
        }

        // Focus lands only once real content exists: focusing the empty
        // surface made NVDA announce "Reading view document blank" and
        // the arriving content was never re-announced. Presence of
        // BLOCKS is the condition — a prose-only note has content but
        // zero landmarks, and gating on landmarks stranded its readers
        // on the collapsed editor.
        if (ClaimsFocusAfterApply(IsVisible, IsKeyboardFocusWithin, Document.Blocks.Count))
        {
            CaretPosition = Document.ContentStart;
            _ = Focus();
        }
    }

    /// <summary>The focus-claim rule, extracted so the prose-only-note
    /// regression is pinned without a presentation source.</summary>
    internal static bool ClaimsFocusAfterApply(
        bool isVisible, bool isKeyboardFocusWithin, int blockCount) =>
        isVisible && !isKeyboardFocusWithin && blockCount > 0;

    /// <summary>
    /// Activate the link the CARET is inside. A caret position is not
    /// element focus — measured 2026-07-27: Enter with the caret inside
    /// a link did nothing, because the Hyperlink never had keyboard
    /// focus and so never raised Click. Containment is tested against
    /// the landmark index's own elements rather than by walking pointer
    /// parents, which misses at normalized boundary positions — exactly
    /// where a chord landing puts the caret.
    /// </summary>
    internal bool TryActivateAtCaret() => TryActivateAtCaret(brailleRequested: false);

    internal bool TryActivateAtCaret(bool brailleRequested)
    {
        if (_model is not { } model || CaretPosition is not { } caret)
        {
            return false;
        }
        foreach (ReadingLandmark landmark in _landmarks)
        {
            if (landmark.Kind == ReadingLandmarkKind.Link
                && landmark.Element is Hyperlink
                {
                    Tag: uniffi.slate_uniffi.ReadingInlineRunKind kind
                } link
                // A document with live links keeps the caret OFF the
                // interactive interior: a chord landing rests the caret
                // immediately BEFORE the hyperlink element (measured —
                // compare(ElementStart) == -1 at one symbol's distance).
                // "At the link" therefore means: inside its element
                // range, or within one symbol before it.
                && caret.CompareTo(link.ElementEnd) <= 0
                && (caret.CompareTo(link.ElementStart) >= 0
                    || caret.GetOffsetToPosition(link.ElementStart) is >= 0 and <= 1))
            {
                model.Activate(kind);
                return true;
            }
        }
        // No link at the caret: a task checkbox on the caret's LINE is
        // the other activatable thing. The caret can never rest inside
        // an InlineUIContainer, so containment is by paragraph.
        if (TryToggleTaskAtCaret())
        {
            return true;
        }
        // Math block at the caret (W3-2): Enter speaks the canonical
        // MathCAT speech through the landed vocabulary — "{speech},
        // math." — content, never composition. This is the guaranteed
        // layer's on-demand read; the W-E7 appModule upgrades Enter to
        // NVDA's full math interaction later.
        if (CaretPosition?.Paragraph is { } mathParagraph
            && ReadingSemantics.IsMathBlock(mathParagraph)
            && _model is { } mathModel)
        {
            // Ctrl+Enter reads the BRAILLE artifact — the accessible
            // value the Nemeth/UEB pref selects (round 3: a persisted
            // setting must change something a user can retrieve).
            // Plain Enter reads the speech. Both announce core content.
            if (brailleRequested)
            {
                ReadingMathElement? mathElement = mathParagraph.Inlines
                    .OfType<InlineUIContainer>()
                    .Select(container => container.Child)
                    .OfType<ReadingMathElement>()
                    .FirstOrDefault();
                mathModel.Announce(new uniffi.slate_uniffi.A11yEvent.HostComposed(
                    mathElement is { Braille.Length: > 0 }
                        ? mathElement.Braille
                        : "Braille not available.",
                    uniffi.slate_uniffi.A11yPriority.Medium));
                return true;
            }
            mathModel.Announce(new uniffi.slate_uniffi.A11yEvent.ReadingNavLanded(
                new uniffi.slate_uniffi.ReadingNavTarget.Math(),
                ReadingSemantics.MathSpeechOf(mathParagraph)));
            return true;
        }
        // Diagram block at the caret (W3-3): Enter (and Ctrl+Enter —
        // diagrams have no braille-analog artifact) re-reads the
        // canonical structured description through the landed
        // vocabulary — content, never composition.
        if (CaretPosition?.Paragraph is { } diagramParagraph
            && ReadingSemantics.IsDiagramBlock(diagramParagraph)
            && _model is { } diagramModel)
        {
            diagramModel.Announce(new uniffi.slate_uniffi.A11yEvent.ReadingNavLanded(
                new uniffi.slate_uniffi.ReadingNavTarget.Diagram(),
                ReadingSemantics.DiagramDescriptionOf(diagramParagraph)));
            return true;
        }
        // Embed card header at the caret (W3-5): Enter activates the
        // card — a chord landing rests the caret ON the header, and a
        // caret position is not element focus (the W3-1 lesson), so
        // the Jump button's own key handling never fires for it. A
        // core-resolved destination navigates directly; only cards
        // without one fall back to the record match.
        if (CaretPosition?.Paragraph is { } embedParagraph
            && ReadingSemantics.EmbedHeaderKeyOf(embedParagraph) is { } embedKey
            && _model is { } embedModel)
        {
            if (ReadingSemantics.TryGetEmbedJump(
                embedParagraph, out string embedJumpPath, out var embedJumpAnchor))
            {
                embedModel.ActivateResolvedEmbedSource(embedJumpPath, embedJumpAnchor);
                return true;
            }
            embedModel.Activate(new uniffi.slate_uniffi.ReadingInlineRunKind.Embed(embedKey));
            return true;
        }
        return false;
    }

    /// <summary>
    /// Toggle the task checkbox on the caret's line (field gap,
    /// 2026-07-26: Space and Enter at the caret did nothing — a caret
    /// position is not element focus, so the checkbox never saw the
    /// key. Mirrors the editor's activate-at-cursor task semantics).
    /// </summary>
    internal bool TryToggleTaskAtCaret()
    {
        if (_model is not { } model || CaretPosition?.Paragraph is not { } paragraph)
        {
            return false;
        }
        foreach (System.Windows.Controls.CheckBox candidate in paragraph.Inlines
            .OfType<InlineUIContainer>()
            .Select(container => container.Child)
            .OfType<System.Windows.Controls.CheckBox>())
        {
            if (ReadingSemantics.TryDecodeTaskRange(
                candidate.Tag, out ulong start, out ulong end))
            {
                model.ToggleTaskAt(start, end);
                return true;
            }
        }
        return false;
    }

    protected override AutomationPeer OnCreateAutomationPeer() =>
        new ReadingSurfacePeer(this);
}

/// <summary>
/// The surface peer: base RichTextBox children (text pattern, native
/// hyperlinks, embedded controls) plus structural peers for the
/// semantics WPF drops — heading level, list, list item.
///
/// The tree is HIERARCHICAL: list items are children of their list's
/// peer, never flattened into the root collection. The perf spike
/// measured the flat version at 43,848 root children and ~720 ms per
/// screen-reader walk on a 6.9 MB note; hierarchy is what lets a client
/// enumerate a section instead of the note.
/// </summary>
internal sealed class ReadingSurfacePeer : RichTextBoxAutomationPeer
{
    public ReadingSurfacePeer(ReadingSurface owner) : base(owner)
    {
    }

    /// <summary>
    /// The Text pattern is served through the StyleId decorator so NVDA
    /// hears heading levels during ordinary line reading — WPF's own
    /// provider never emits the one attribute NVDA's linear reading
    /// keys on. Every other pattern is the base's.
    /// </summary>
    public override object GetPattern(PatternInterface patternInterface)
    {
        object pattern = base.GetPattern(patternInterface);
        if (patternInterface == PatternInterface.Text
            && pattern is System.Windows.Automation.Provider.ITextProvider text)
        {
            return new HeadingStyleTextProvider(text);
        }
        return pattern;
    }

    protected override List<AutomationPeer> GetChildrenCore()
    {
        List<AutomationPeer> children = base.GetChildrenCore() ?? new List<AutomationPeer>();
        if (((RichTextBox)Owner).Document is not FlowDocument document)
        {
            return children;
        }
        foreach (Block block in document.Blocks)
        {
            AppendStructural(block, children);
        }
        return children;
    }

    private static void AppendStructural(Block block, List<AutomationPeer> children)
    {
        switch (block)
        {
            case Paragraph paragraph
                when ReadingSemantics.HeadingLevelOf(paragraph) is byte level and > 0:
                children.Add(new ReadingHeadingPeer(paragraph, level));
                break;

            case Paragraph code when ReadingSemantics.IsCodeBlock(code):
                children.Add(new ReadingCodeBlockPeer(code));
                break;

            case WpfList list when ReadingSemantics.IsList(list):
                children.Add(new ReadingListPeer(list));
                break;

            case Section section:
                foreach (Block inner in section.Blocks)
                {
                    AppendStructural(inner, children);
                }
                break;
        }
    }

    /// <summary>Shared by the list peer for its nested lists.</summary>
    internal static void AppendStructuralForTest(Block block, List<AutomationPeer> children) =>
        AppendStructural(block, children);
}

/// <summary>
/// A heading paragraph's peer. `AutomationProperties.HeadingLevel` on
/// the element does not survive onto a stock text peer (measured — the
/// spike's §10.6 item 2 answer), so the peer reports it itself.
/// </summary>
internal sealed class ReadingHeadingPeer : TextElementAutomationPeer
{
    private readonly Paragraph _paragraph;
    private readonly byte _level;

    public ReadingHeadingPeer(Paragraph paragraph, byte level) : base(paragraph)
    {
        _paragraph = paragraph;
        _level = level;
    }

    protected override AutomationHeadingLevel GetHeadingLevelCore() => _level switch
    {
        1 => AutomationHeadingLevel.Level1,
        2 => AutomationHeadingLevel.Level2,
        3 => AutomationHeadingLevel.Level3,
        4 => AutomationHeadingLevel.Level4,
        5 => AutomationHeadingLevel.Level5,
        _ => AutomationHeadingLevel.Level6,
    };

    protected override AutomationControlType GetAutomationControlTypeCore() =>
        AutomationControlType.Text;

    protected override string GetNameCore() =>
        new TextRange(_paragraph.ContentStart, _paragraph.ContentEnd).Text.Trim();

    protected override bool IsControlElementCore() => true;

    protected override bool IsContentElementCore() => true;
}

/// <summary>
/// A code block's peer (W3-4). Its Name is CORE's preamble ("Code
/// block, rust, 3 lines.") — the K contract's "AT preamble behind a
/// UIA peer": object navigation announces the summary, while the
/// interior stays readable through the document's text range (never
/// through this peer, which would double-speak it).
/// </summary>
internal sealed class ReadingCodeBlockPeer : TextElementAutomationPeer
{
    private readonly Paragraph _paragraph;

    public ReadingCodeBlockPeer(Paragraph paragraph) : base(paragraph)
    {
        _paragraph = paragraph;
    }

    protected override AutomationControlType GetAutomationControlTypeCore() =>
        AutomationControlType.Group;

    protected override string GetNameCore() =>
        AutomationProperties.GetName(_paragraph) ?? string.Empty;

    protected override bool IsControlElementCore() => true;

    protected override bool IsContentElementCore() => true;
}

/// <summary>
/// A list's peer, parenting one <see cref="ReadingListItemPeer"/> per
/// item — the native `List`/`ListItem` exposure the G20 owner call
/// requires and WPF's flow elements do not provide.
/// </summary>
internal sealed class ReadingListPeer : TextElementAutomationPeer
{
    private readonly WpfList _list;

    public ReadingListPeer(WpfList list) : base(list)
    {
        _list = list;
    }

    protected override AutomationControlType GetAutomationControlTypeCore() =>
        AutomationControlType.List;

    protected override string GetNameCore() => "list";

    protected override bool IsControlElementCore() => true;

    protected override bool IsContentElementCore() => true;

    protected override List<AutomationPeer> GetChildrenCore()
    {
        var children = new List<AutomationPeer>();
        foreach (ListItem item in _list.ListItems)
        {
            children.Add(new ReadingListItemPeer(item));
            // Nested lists stay under their owning item's sibling peer —
            // structure mirrors the document, so depth is enumerable.
            foreach (Block inner in item.Blocks)
            {
                if (inner is WpfList nested && ReadingSemantics.IsList(nested))
                {
                    children.Add(new ReadingListPeer(nested));
                }
            }
        }
        return children;
    }
}

/// <summary>One list item's peer.</summary>
internal sealed class ReadingListItemPeer : TextElementAutomationPeer
{
    private readonly ListItem _item;

    public ReadingListItemPeer(ListItem item) : base(item)
    {
        _item = item;
    }

    protected override AutomationControlType GetAutomationControlTypeCore() =>
        AutomationControlType.ListItem;

    protected override string GetNameCore() =>
        new TextRange(_item.ContentStart, _item.ContentEnd).Text.Trim();

    protected override bool IsControlElementCore() => true;

    protected override bool IsContentElementCore() => true;

    /// <summary>
    /// Not focusable: the caret owns focus in the document, and NVDA's
    /// list quick-nav condition requires non-focusable list items (the
    /// browse-mode research's verified condition for `l`).
    /// </summary>
    protected override bool IsKeyboardFocusableCore() => false;
}
