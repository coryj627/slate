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
