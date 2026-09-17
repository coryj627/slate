// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using WpfList = System.Windows.Documents.List;

namespace ContainerSpike;

/// Variant C: `FlowDocument` **plus custom `AutomationPeer`s**, testing
/// whether the three gaps the spike found in the plain flow variant can be
/// closed at all.
///
/// The gaps, and what this attempts for each:
///
///  1. **Links are text ranges, not elements.** NVDA announces them while
///     reading but Tab produces a silent focus stop, because the control
///     tree holds no `Hyperlink` element to report. Attempt: return a
///     `HyperlinkAutomationPeer` per authored `Hyperlink` from the
///     surface peer's children.
///  2. **`HeadingLevel` dies on a `Paragraph`.** Attempt: a
///     `TextElementAutomationPeer` subclass that reports the level it was
///     constructed with.
///  3. **No `List`/`ListItem` control types.** WPF's flow `List`/`ListItem`
///     are layout elements. Attempt: peers that claim those control types.
///
/// This is a VIABILITY probe, not a design. If WPF surfaces these peers,
/// W3-1 can scope the work with evidence; if it silently drops them, the
/// FlowDocument route is disqualified by the silent-focus-stop finding
/// regardless of its Text pattern.
internal sealed class PeeredFlowDocumentViewer : FlowDocumentScrollViewer
{
    protected override AutomationPeer OnCreateAutomationPeer() =>
        new ReadingSurfacePeer(this);
}

/// Appends semantic peers to whatever the base document peer produces.
///
/// Deliberately ADDITIVE: the base children carry the text pattern that
/// makes say-all work, and replacing them would trade the flow variant's
/// one real advantage for the thing being tested.
internal sealed class ReadingSurfacePeer : FlowDocumentScrollViewerAutomationPeer
{
    public ReadingSurfacePeer(FlowDocumentScrollViewer owner) : base(owner)
    {
    }

    protected override List<AutomationPeer> GetChildrenCore()
    {
        List<AutomationPeer> children = base.GetChildrenCore() ?? new List<AutomationPeer>();

        if (((FlowDocumentScrollViewer)Owner).Document is not FlowDocument document)
        {
            return children;
        }

        AppendDocument(document, children, includeLinks: true);
        return children;
    }

    /// Shared by both surface peers so the two variants differ only in
    /// their host control, never in what they expose.
    ///
    /// `includeLinks` exists because the two hosts do not start from the
    /// same place: `FlowDocumentScrollViewer` surfaces NO `Hyperlink`
    /// peers, so they must be added, while `RichTextBox` surfaces them
    /// natively — adding them there produced ten links for five, which is
    /// worse than the gap it was closing.
    internal static void AppendDocument(
        FlowDocument document, List<AutomationPeer> children, bool includeLinks)
    {
        foreach (Block block in document.Blocks)
        {
            AppendBlock(block, children, includeLinks);
        }
    }

    private static void AppendBlock(
        Block block, List<AutomationPeer> children, bool includeLinks)
    {
        switch (block)
        {
            case Paragraph paragraph:
                AutomationHeadingLevel level =
                    AutomationProperties.GetHeadingLevel(paragraph);
                if (level != AutomationHeadingLevel.None)
                {
                    children.Add(new HeadingPeer(paragraph, level));
                }
                if (includeLinks)
                {
                    AppendInlines(paragraph.Inlines, children);
                }
                break;

            case WpfList list:
                children.Add(new SemanticTextPeer(list, AutomationControlType.List, "list"));
                foreach (ListItem item in list.ListItems)
                {
                    children.Add(new SemanticTextPeer(
                        item, AutomationControlType.ListItem, FirstText(item)));
                    foreach (Block inner in item.Blocks)
                    {
                        AppendBlock(inner, children, includeLinks);
                    }
                }
                break;

            case Section section:
                foreach (Block inner in section.Blocks)
                {
                    AppendBlock(inner, children, includeLinks);
                }
                break;
        }
    }

    private static void AppendInlines(InlineCollection inlines, List<AutomationPeer> children)
    {
        foreach (Inline inline in inlines)
        {
            switch (inline)
            {
                case Hyperlink link:
                    // The whole point: a real element for the link, so
                    // focus has something to announce.
                    children.Add(new HyperlinkAutomationPeer(link));
                    break;
                case Span span:
                    AppendInlines(span.Inlines, children);
                    break;
            }
        }
    }

    private static string FirstText(ListItem item)
    {
        foreach (Block block in item.Blocks)
        {
            if (block is Paragraph paragraph)
            {
                return new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text.Trim();
            }
        }
        return "list item";
    }
}

/// A `Paragraph` that claims its heading level. `AutomationProperties`
/// carries the value onto the element but not, as the spike measured,
/// onto the peer — so the peer has to report it itself.
internal sealed class HeadingPeer : TextElementAutomationPeer
{
    private readonly AutomationHeadingLevel _level;
    private readonly TextElement _element;

    public HeadingPeer(TextElement owner, AutomationHeadingLevel level) : base(owner)
    {
        _element = owner;
        _level = level;
    }

    protected override AutomationHeadingLevel GetHeadingLevelCore() => _level;

    protected override AutomationControlType GetAutomationControlTypeCore() =>
        AutomationControlType.Text;

    protected override string GetNameCore() =>
        new TextRange(_element.ContentStart, _element.ContentEnd).Text.Trim();

    protected override bool IsControlElementCore() => true;
}

/// A text element claiming a specific control type — used for `List` and
/// `ListItem`, which WPF treats as layout only.
internal sealed class SemanticTextPeer : TextElementAutomationPeer
{
    private readonly AutomationControlType _controlType;
    private readonly string _name;

    public SemanticTextPeer(TextElement owner, AutomationControlType controlType, string name)
        : base(owner)
    {
        _controlType = controlType;
        _name = name;
    }

    protected override AutomationControlType GetAutomationControlTypeCore() => _controlType;

    protected override string GetNameCore() => _name;

    protected override bool IsControlElementCore() => true;
}

/// Variant D: the same `FlowDocument` hosted in a **read-only
/// `RichTextBox`** instead of a `FlowDocumentScrollViewer`.
///
/// The manual passes kept getting stuck: arrow keys re-announced the same
/// line forever, because `FlowDocumentScrollViewer` has **no keyboard
/// caret** — WPF gives it mouse-driven selection only. A reader who
/// cannot move the caret cannot review the note, which makes the whole
/// surface untestable and, shipped, unusable.
///
/// A read-only `RichTextBox` is WPF's actual answer for a navigable
/// read-only rich document: real caret, arrow and word navigation,
/// selection, and `IsDocumentEnabled` keeps hyperlinks live. It hosts the
/// identical `FlowDocument`, so everything the container decision rested
/// on — the text pattern, reading order, the semantic peers — carries
/// over unchanged if it works.
internal sealed class PeeredRichTextBox : System.Windows.Controls.RichTextBox
{
    public PeeredRichTextBox()
    {
        IsReadOnly = true;
        // Read-only, but NOT selection-disabled: the caret is the point.
        IsReadOnlyCaretVisible = true;
        IsDocumentEnabled = true;
        AcceptsReturn = false;
        AcceptsTab = false;
        BorderThickness = new Thickness(0);
        VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto;
    }

    protected override AutomationPeer OnCreateAutomationPeer() =>
        new RichReadingSurfacePeer(this);
}

/// Same additive strategy as `ReadingSurfacePeer`, over the RichTextBox
/// peer instead of the document-viewer peer.
internal sealed class RichReadingSurfacePeer
    : System.Windows.Automation.Peers.RichTextBoxAutomationPeer
{
    public RichReadingSurfacePeer(System.Windows.Controls.RichTextBox owner) : base(owner)
    {
    }

    protected override List<AutomationPeer> GetChildrenCore()
    {
        List<AutomationPeer> children = base.GetChildrenCore() ?? new List<AutomationPeer>();
        if (((System.Windows.Controls.RichTextBox)Owner).Document is not FlowDocument document)
        {
            return children;
        }
        // RichTextBox already provides Hyperlink peers.
        ReadingSurfacePeer.AppendDocument(document, children, includeLinks: false);
        return children;
    }
}
