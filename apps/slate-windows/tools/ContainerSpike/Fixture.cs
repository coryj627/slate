// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using uniffi.slate_uniffi;

namespace ContainerSpike;

/// The one note both containers render.
///
/// Every element W3-1 must place in reading order appears exactly once, so
/// a UIA probe can name what it found rather than counting duplicates. The
/// list nesting and the task rows are deliberate: they are the two shapes
/// the container choice actually turns on (native `List`/`ListItem`
/// exposure, and a focusable non-text child interleaved with prose).
internal static class Fixture
{
    /// Authored markdown. Kept verbatim — chrome stripping and inline
    /// classification are core's, and the spike must not pre-digest either.
    public const string Markdown =
        "# Reading container spike\n"
        + "\n"
        + "A paragraph with a [[resolved note]] link, an [[absent note]] link, "
        + "a #tag, a citation [@smith2020], and **bold** plus `code` spans.\n"
        + "\n"
        + "## Second level heading\n"
        + "\n"
        + "- first bullet\n"
        + "- second bullet\n"
        + "  - nested bullet\n"
        + "\n"
        + "1. ordered one\n"
        + "2. ordered two\n"
        + "\n"
        + "- [ ] an open task\n"
        + "- [x] a done task\n"
        + "\n"
        + "> a block quote with a [[resolved note]] link\n"
        + "\n"
        + "```rust\n"
        + "fn main() { println!(\"hi\"); }\n"
        + "```\n"
        + "\n"
        + "| header a | header b |\n"
        + "| --- | --- |\n"
        + "| cell 1 | cell 2 |\n"
        + "\n"
        + "---\n"
        + "\n"
        + "![[resolved note]]\n";

    /// The citation join input. `raw` is the join key core matches on.
    public static RenderedCitation[] Citations { get; } =
    {
        new RenderedCitation(
            Raw: "[@smith2020]",
            VisualText: "(Smith, 2020)",
            SpeechText: "Smith, two thousand twenty.",
            BibEntry: null,
            StyleId: "spike"),
    };

    /// Resolution input: one resolved target and one deliberately absent,
    /// so the spike renders BOTH link treatments and a probe can tell the
    /// unresolved announcement apart from the resolved one.
    public static OutgoingLink[] Records { get; } =
    {
        new OutgoingLink(
            TargetPath: "resolved note.md",
            TargetRaw: "resolved note",
            TargetAnchor: null,
            Kind: "wikilink",
            IsEmbed: false,
            IsExternal: false,
            IsUnresolved: false,
            Snippet: "",
            Ordinal: 0,
            SpanStart: 0,
            SpanEnd: 0,
            DisplayText: null),
        new OutgoingLink(
            TargetPath: null,
            TargetRaw: "absent note",
            TargetAnchor: null,
            Kind: "wikilink",
            IsEmbed: false,
            IsExternal: false,
            IsUnresolved: true,
            Snippet: "",
            Ordinal: 1,
            SpanStart: 0,
            SpanEnd: 0,
            DisplayText: null),
    };

    /// The blocks and their core-computed inline runs, zipped 1:1 as
    /// §10.1 requires. Both containers consume THIS — the block model is
    /// shared so the only variable in the spike is the container.
    public static IReadOnlyList<(ReadingBlock Block, ReadingBlockInlines Inlines)> Load()
    {
        ReadingBlock[] blocks = SlateUniffiMethods.ReadingBlocksSource(Markdown);
        ReadingBlockInlines[] inlines = SlateUniffiMethods.ReadingInlineSegmentsSource(
            Markdown, Citations, Records);

        var zipped = new List<(ReadingBlock, ReadingBlockInlines)>(blocks.Length);
        for (int i = 0; i < blocks.Length && i < inlines.Length; i++)
        {
            zipped.Add((blocks[i], inlines[i]));
        }
        return zipped;
    }
}
