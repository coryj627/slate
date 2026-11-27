// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Documents;
using uniffi.slate_uniffi;

namespace SlateWindows.Reading;

/// <summary>
/// Structural markers the builder stamps and the landmark walk + peer
/// tree read back. An attached property rather than <c>Tag</c> so the
/// marker survives any future use of <c>Tag</c> by styling code.
/// </summary>
internal static class ReadingSemantics
{
    private enum Marker
    {
        Heading,
        List,
        ListItem,
        ListMarker,
        Table,
        Embed,
        CodeBlock,
        CodeCopy,
        MathBlock,
        DiagramBlock,
        Quote,
    }

    private static readonly DependencyProperty MathSpeechProperty =
        DependencyProperty.RegisterAttached(
            "ReadingMathSpeech", typeof(string), typeof(ReadingSemantics),
            new PropertyMetadata(null));

    private static readonly DependencyProperty CopyTextProperty =
        DependencyProperty.RegisterAttached(
            "ReadingCopyText", typeof(string), typeof(ReadingSemantics),
            new PropertyMetadata(null));

    private static readonly DependencyProperty DiagramFenceProperty =
        DependencyProperty.RegisterAttached(
            "ReadingDiagramFence", typeof(string), typeof(ReadingSemantics),
            new PropertyMetadata(null));

    private static readonly DependencyProperty DiagramSourceProperty =
        DependencyProperty.RegisterAttached(
            "ReadingDiagramSource", typeof(string), typeof(ReadingSemantics),
            new PropertyMetadata(null));

    private static readonly DependencyProperty HiddenSpeechProperty =
        DependencyProperty.RegisterAttached(
            "ReadingHiddenSpeech", typeof(bool), typeof(ReadingSemantics),
            new PropertyMetadata(false));

    private static readonly DependencyProperty DiagramDescriptionProperty =
        DependencyProperty.RegisterAttached(
            "ReadingDiagramDescription", typeof(string), typeof(ReadingSemantics),
            new PropertyMetadata(null));

    private static readonly DependencyProperty PendingDiagramKeyProperty =
        DependencyProperty.RegisterAttached(
            "ReadingPendingDiagramKey", typeof(string), typeof(ReadingSemantics),
            new PropertyMetadata(null));

    private static readonly DependencyProperty EmbedNameProperty =
        DependencyProperty.RegisterAttached(
            "ReadingEmbedName", typeof(string), typeof(ReadingSemantics),
            new PropertyMetadata(null));

    private static readonly DependencyProperty EmbedKeyProperty =
        DependencyProperty.RegisterAttached(
            "ReadingEmbedKey", typeof(string), typeof(ReadingSemantics),
            new PropertyMetadata(null));

    private static readonly DependencyProperty EmbedJumpPathProperty =
        DependencyProperty.RegisterAttached(
            "ReadingEmbedJumpPath", typeof(string), typeof(ReadingSemantics),
            new PropertyMetadata(null));

    private static readonly DependencyProperty EmbedJumpAnchorProperty =
        DependencyProperty.RegisterAttached(
            "ReadingEmbedJumpAnchor", typeof(string), typeof(ReadingSemantics),
            new PropertyMetadata(null));

    private static readonly DependencyProperty TableSourceProperty =
        DependencyProperty.RegisterAttached(
            "ReadingTableSource", typeof(string), typeof(ReadingSemantics),
            new PropertyMetadata(null));

    private static readonly DependencyProperty MarkerProperty =
        DependencyProperty.RegisterAttached(
            "ReadingMarker", typeof(Marker?), typeof(ReadingSemantics),
            new PropertyMetadata(null));

    private static readonly DependencyProperty HeadingLevelProperty =
        DependencyProperty.RegisterAttached(
            "ReadingHeadingLevel", typeof(byte), typeof(ReadingSemantics),
            new PropertyMetadata((byte)0));

    public static void MarkHeading(Paragraph paragraph, byte level)
    {
        paragraph.SetValue(MarkerProperty, Marker.Heading);
        paragraph.SetValue(HeadingLevelProperty, level);
    }

    public static void MarkList(System.Windows.Documents.List list) =>
        list.SetValue(MarkerProperty, Marker.List);

    public static void MarkListItem(ListItem item) =>
        item.SetValue(MarkerProperty, Marker.ListItem);

    /// <summary>WB-11 (#1422): the run of an ordered item's authored marker
    /// ("3.", "12)"), which leads its first paragraph so the reader sees and
    /// hears what the author wrote; marked so later code can tell it from
    /// the item's content.</summary>
    public static void MarkListMarker(Run run) =>
        run.SetValue(MarkerProperty, Marker.ListMarker);

    public static bool IsListMarker(Run run) =>
        Equals(run.GetValue(MarkerProperty), Marker.ListMarker);

    public static void MarkTable(System.Windows.Documents.Table table) =>
        table.SetValue(MarkerProperty, Marker.Table);

    /// <summary>Table with a grid destination (W4-1): the marker
    /// carries the block's SOURCE so Enter-at-caret can open the same
    /// table on the substrate — core re-derives the cells there, the
    /// host never re-parses. String-only (the W3-1 Tag-serialization
    /// lesson). Degenerate tables take the source-less overload and
    /// stay linear-only.</summary>
    public static void MarkTable(System.Windows.Documents.Table table, string source)
    {
        table.SetValue(MarkerProperty, Marker.Table);
        table.SetValue(TableSourceProperty, source);
    }

    public static string? TableSourceOf(System.Windows.Documents.Table table) =>
        table.GetValue(TableSourceProperty) as string;

    /// <summary>Embed card (W3-5): the marker carries the mac-shaped
    /// header name so the landmark walk announces content, not a
    /// cache key.</summary>
    public static void MarkEmbed(Section section, string name)
    {
        section.SetValue(MarkerProperty, Marker.Embed);
        section.SetValue(EmbedNameProperty, name);
    }

    public static bool IsEmbedSection(Section section) =>
        Equals(section.GetValue(MarkerProperty), Marker.Embed);

    public static string EmbedNameOf(Section section) =>
        section.GetValue(EmbedNameProperty) as string ?? string.Empty;

    /// <summary>An embed card's header paragraph carries its cache
    /// key so Enter-at-caret can activate the card (a caret position
    /// is not element focus — the W3-1 lesson).</summary>
    public static void MarkEmbedHeader(Paragraph paragraph, string key) =>
        paragraph.SetValue(EmbedKeyProperty, key);

    public static string? EmbedHeaderKeyOf(Paragraph paragraph) =>
        paragraph.GetValue(EmbedKeyProperty) as string;

    /// <summary>
    /// The Jump destination when core ALREADY RESOLVED the target
    /// (W3-5, round 1): activation navigates directly — the mac
    /// `openEmbedTarget(path)` contract — because the host note's
    /// record snapshot cannot contain NESTED targets, and re-matching
    /// there dead-ends on content the card is displaying. String-only
    /// values (the W3-1 Tag-serialization lesson); the anchor rides a
    /// "kind:text" codec, kinds "heading"/"block" per core.
    /// </summary>
    public static void MarkEmbedJump(
        DependencyObject element, string path, string? anchorKind, string? anchorText)
    {
        element.SetValue(EmbedJumpPathProperty, path);
        if (anchorKind is not null && anchorText is not null)
        {
            element.SetValue(EmbedJumpAnchorProperty, anchorKind + ":" + anchorText);
        }
    }

    public static bool TryGetEmbedJump(
        DependencyObject element,
        out string path,
        out LinkAnchor? anchor)
    {
        path = string.Empty;
        anchor = null;
        if (element.GetValue(EmbedJumpPathProperty) is not string jumpPath)
        {
            return false;
        }
        path = jumpPath;
        if (element.GetValue(EmbedJumpAnchorProperty) is string encoded)
        {
            int split = encoded.IndexOf(':', StringComparison.Ordinal);
            if (split > 0)
            {
                anchor = new LinkAnchor(encoded[..split], encoded[(split + 1)..]);
            }
        }
        return true;
    }

    public static void MarkCodeBlock(Paragraph paragraph) =>
        paragraph.SetValue(MarkerProperty, Marker.CodeBlock);

    /// <summary>Block quote (field, 2026-07-30): linear reading gave
    /// no structure cue — the StyleId decorator answers
    /// <c>StyleId_Quote</c> for marked paragraphs, the same mechanism
    /// heading levels ride (mac parity: an AX value announces the
    /// quote).</summary>
    public static void MarkQuote(Paragraph paragraph) =>
        paragraph.SetValue(MarkerProperty, Marker.Quote);

    public static bool IsQuote(Paragraph paragraph) =>
        Equals(paragraph.GetValue(MarkerProperty), Marker.Quote);

    /// <summary>0 when the paragraph is not a heading.</summary>
    public static byte HeadingLevelOf(Paragraph paragraph) =>
        Equals(paragraph.GetValue(MarkerProperty), Marker.Heading)
            ? (byte)paragraph.GetValue(HeadingLevelProperty)
            : (byte)0;

    public static bool IsCodeBlock(Paragraph paragraph) =>
        Equals(paragraph.GetValue(MarkerProperty), Marker.CodeBlock);

    public static bool IsList(System.Windows.Documents.List list) =>
        Equals(list.GetValue(MarkerProperty), Marker.List);

    /// <summary>The code block's Copy button — distinguished by marker,
    /// not Tag shape, so the click router never guesses (embed-card
    /// buttons also carry string Tags).</summary>
    public static void MarkCodeCopy(DependencyObject element) =>
        element.SetValue(MarkerProperty, Marker.CodeCopy);

    /// <summary>Math block (W3-2): the marker carries the canonical
    /// MathCAT speech so the landmark walk and the caret activation
    /// path announce content, not composition.</summary>
    public static void MarkMathBlock(Paragraph paragraph, string speech)
    {
        paragraph.SetValue(MarkerProperty, Marker.MathBlock);
        paragraph.SetValue(MathSpeechProperty, speech);
    }

    public static bool IsMathBlock(Paragraph paragraph) =>
        Equals(paragraph.GetValue(MarkerProperty), Marker.MathBlock);

    public static string MathSpeechOf(Paragraph paragraph) =>
        paragraph.GetValue(MathSpeechProperty) as string ?? string.Empty;

    /// <summary>#1595 and #1708: a paragraph whose text range is speech for
    /// an object it holds (a drawn formula, a rendered diagram) carries the
    /// fenced source a copy puts in that speech's place.</summary>
    public static void MarkCopyText(Paragraph paragraph, string fencedSource) =>
        paragraph.SetValue(CopyTextProperty, fencedSource);

    /// <summary>The fenced source a copy substitutes for this paragraph;
    /// null when its range already holds what it shows (a math block with
    /// no core artifact, a pending or failed diagram).</summary>
    public static string? CopyTextOf(Paragraph paragraph) =>
        paragraph.GetValue(CopyTextProperty) as string;

    public static void ClearCopyText(Paragraph paragraph) =>
        paragraph.ClearValue(CopyTextProperty);

    /// <summary>#1708: every diagram paragraph, in every state, carries its
    /// block's fenced source, so a completion in place, which has only the
    /// outcome, can give the rendered shape its copy text. Never read by a
    /// copy itself: a pending or failed paragraph copies what it shows.</summary>
    public static void MarkDiagramFence(Paragraph paragraph, string fencedSource) =>
        paragraph.SetValue(DiagramFenceProperty, fencedSource);

    public static string? DiagramFenceOf(Paragraph paragraph) =>
        paragraph.GetValue(DiagramFenceProperty) as string;

    /// <summary>Plan 47 CB-20b: every diagram paragraph, in every state,
    /// carries its fence's interior as the note holds it, which the reading
    /// view's source chord speaks (core's <c>DiagramSourceSpoken</c>). A
    /// completion in place keeps the paragraph, so the source survives it.</summary>
    public static void MarkDiagramSource(Paragraph paragraph, string source) =>
        paragraph.SetValue(DiagramSourceProperty, source);

    public static string? DiagramSourceOf(Paragraph paragraph) =>
        paragraph.GetValue(DiagramSourceProperty) as string;

    /// <summary>#1595 and #1708: a run in the text range for line reading
    /// only: a formula's speech, a rendered diagram's description, a pending
    /// diagram's name, an embedded image's name. It is drawn transparent at a
    /// sliver of a point; find skips it and a copy leaves it out.</summary>
    public static void MarkHiddenSpeech(Run run) =>
        run.SetValue(HiddenSpeechProperty, true);

    public static bool IsHiddenSpeech(Run run) =>
        (bool)run.GetValue(HiddenSpeechProperty);

    /// <summary>Diagram block (W3-3): the marker carries the canonical
    /// structured description so the landmark walk and the caret
    /// activation path announce content, not composition.</summary>
    public static void MarkDiagramBlock(Paragraph paragraph, string description)
    {
        paragraph.SetValue(MarkerProperty, Marker.DiagramBlock);
        paragraph.SetValue(DiagramDescriptionProperty, description);
    }

    public static bool IsDiagramBlock(Paragraph paragraph) =>
        Equals(paragraph.GetValue(MarkerProperty), Marker.DiagramBlock);

    public static string DiagramDescriptionOf(Paragraph paragraph) =>
        paragraph.GetValue(DiagramDescriptionProperty) as string ?? string.Empty;

    /// <summary>A diagram fence a stage-1 projection left pending (WB-20,
    /// #1317): <paramref name="key"/> is the fence's block byte start, which
    /// the diagram stage's outcome names when it completes the paragraph in
    /// place.</summary>
    public static void MarkDiagramPending(Paragraph paragraph, string key) =>
        paragraph.SetValue(PendingDiagramKeyProperty, key);

    /// <summary>The pending fence's key, or null once completed.</summary>
    public static string? PendingDiagramKeyOf(Paragraph paragraph) =>
        paragraph.GetValue(PendingDiagramKeyProperty) as string;

    /// <summary>Clears the pending mark when the stage completes the
    /// paragraph.</summary>
    public static void ClearDiagramPending(Paragraph paragraph) =>
        paragraph.ClearValue(PendingDiagramKeyProperty);

    public static bool IsCodeCopy(DependencyObject element) =>
        Equals(element.GetValue(MarkerProperty), Marker.CodeCopy);

    /// <summary>
    /// Task-range Tag codec. WPF text machinery XamlWriter-serializes
    /// document content (undo preservation especially), and generic
    /// payloads make it throw — so the stamped range is a plain string.
    /// A HOST-INTERNAL address, not core semantics: decoding it back is
    /// not the §10.8 re-derivation the census forbids.
    /// </summary>
    public static string EncodeTaskRange(ulong start, ulong end) =>
        string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{start}:{end}");

    public static bool TryDecodeTaskRange(object? tag, out ulong start, out ulong end)
    {
        start = 0;
        end = 0;
        if (tag is not string encoded)
        {
            return false;
        }
        int split = encoded.IndexOf(':', StringComparison.Ordinal);
        return split > 0
            && ulong.TryParse(
                encoded.AsSpan(0, split),
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out start)
            && ulong.TryParse(
                encoded.AsSpan(split + 1),
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out end);
    }
}

/// <summary>
/// Run kind → routing URI — the same scheme table the mac applier's
/// <c>activationURL(for:)</c> uses, so both hosts route the same value
/// and the authored grammar rides the scheme (`slate-wiki` vs
/// `slate-wikimd`; `^` is an anchor in one grammar and a path character
/// in the other). Construction only: activation resolves through core's
/// <c>ReadingMatchLink</c> in the interaction layer, and this file never
/// examines a destination to decide activability — core already decided
/// (`Text` runs carry no kind payload to route).
/// </summary>
internal static class ReadingRouting
{
    public const string WikiScheme = "slate-wiki";
    public const string WikiMarkdownScheme = "slate-wikimd";
    public const string EmbedScheme = "slate-embed";
    public const string TagScheme = "slate-tag";
    public const string CiteScheme = "slate-cite";

    public static Uri? RoutingUri(ReadingInlineRunKind kind)
    {
        switch (kind)
        {
            case ReadingInlineRunKind.ExternalLink external:
                return Uri.TryCreate(external.Url, UriKind.Absolute, out Uri? url)
                    ? url : null;
            case ReadingInlineRunKind.Wikilink wiki:
                return Routed(
                    wiki.Grammar == ReadingWikiGrammar.Wikilink
                        ? WikiScheme : WikiMarkdownScheme,
                    wiki.Target);
            case ReadingInlineRunKind.Embed embed:
                return Routed(EmbedScheme, embed.Key);
            case ReadingInlineRunKind.Tag tag:
                return Routed(TagScheme, tag.Name);
            case ReadingInlineRunKind.Citation citation:
                return Routed(CiteScheme, citation.Raw);
            default:
                return null;
        }
    }

    /// <summary>
    /// The target rides the PATH (`scheme:///escaped`), never the
    /// authority: .NET rejects percent-encoded bracket bytes in a host,
    /// so an authority-form URI silently loses destinations for targets
    /// like a citation's `[@key]` — measured as `NavigateUri == null`,
    /// which NVDA announces as "Link has no apparent destination". The
    /// routed VALUE (scheme + decoded target) is the mac-parity surface;
    /// the component layout is host-local.
    /// </summary>
    private static Uri? Routed(string scheme, string target) =>
        Uri.TryCreate(
            $"{scheme}:///{Uri.EscapeDataString(target)}",
            UriKind.Absolute,
            out Uri? routed) ? routed : null;

    /// <summary>Recover the routed target from a routing URI.</summary>
    public static string RoutedTarget(Uri uri) =>
        Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
}
