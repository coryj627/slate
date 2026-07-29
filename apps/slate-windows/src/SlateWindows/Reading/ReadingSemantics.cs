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
        Table,
        Embed,
        CodeBlock,
        CodeCopy,
        MathBlock,
        DiagramBlock,
    }

    private static readonly DependencyProperty MathSpeechProperty =
        DependencyProperty.RegisterAttached(
            "ReadingMathSpeech", typeof(string), typeof(ReadingSemantics),
            new PropertyMetadata(null));

    private static readonly DependencyProperty DiagramDescriptionProperty =
        DependencyProperty.RegisterAttached(
            "ReadingDiagramDescription", typeof(string), typeof(ReadingSemantics),
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

    public static void MarkTable(System.Windows.Documents.Table table) =>
        table.SetValue(MarkerProperty, Marker.Table);

    public static void MarkEmbed(BlockUIContainer container) =>
        container.SetValue(MarkerProperty, Marker.Embed);

    public static void MarkCodeBlock(Paragraph paragraph) =>
        paragraph.SetValue(MarkerProperty, Marker.CodeBlock);

    /// <summary>0 when the paragraph is not a heading.</summary>
    public static byte HeadingLevelOf(Paragraph paragraph) =>
        Equals(paragraph.GetValue(MarkerProperty), Marker.Heading)
            ? (byte)paragraph.GetValue(HeadingLevelProperty)
            : (byte)0;

    public static bool IsCodeBlock(Paragraph paragraph) =>
        Equals(paragraph.GetValue(MarkerProperty), Marker.CodeBlock);

    public static bool IsList(System.Windows.Documents.List list) =>
        Equals(list.GetValue(MarkerProperty), Marker.List);

    public static bool IsEmbed(BlockUIContainer container) =>
        Equals(container.GetValue(MarkerProperty), Marker.Embed);

    /// <summary>The code block's Copy button — distinguished by marker,
    /// not Tag shape, so the click router never guesses (embed-card
    /// buttons also carry string Tags).</summary>
    public static void MarkCodeCopy(System.Windows.Controls.Button button) =>
        button.SetValue(MarkerProperty, Marker.CodeCopy);

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
