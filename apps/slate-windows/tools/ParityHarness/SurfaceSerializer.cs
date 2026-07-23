// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// §W-A harness skeleton — surface serialization (w0_spec §W0-3 item 5).
// Serializes the skeleton's read-side surfaces (editor spans, headings,
// reading blocks, search, links) into canonical artifacts. The Swift twin
// (ParityHarnessTests.swift) mirrors every rule here; the committed
// goldens under crates/slate-core/tests/fixtures/parity_golden/ arbitrate
// byte-identity. W8-4 grows this to the full read-side surface and the
// two-platform CI pipeline.
//
// Path rule: artifact contents always use forward-slash relative paths
// (the one normalization the skeleton owns; list lives here per §W-A).

using uniffi.slate_uniffi;

namespace ParityHarness;

public static class SurfaceSerializer
{
    public static readonly string[] PinnedSearchQueries = { "fixture", "heading", "parity" };

    /// <summary>Per-file artifact: spans + headings + reading blocks.</summary>
    public static string FileArtifact(string relPath, string text)
    {
        var j = new CanonicalJson();
        j.Raw("{\"file\":").Str(relPath);

        j.Raw(",\"spans\":[");
        AppendSpans(j, SlateUniffiMethods.EditorHighlightSpans(text));
        j.Raw("]");

        j.Raw(",\"span_windows\":");
        AppendSpanWindows(j, text);

        j.Raw(",\"headings\":[");
        var headings = SlateUniffiMethods.ExtractHeadings(text);
        for (int i = 0; i < headings.Length; i++)
        {
            var h = headings[i];
            if (i > 0)
            {
                j.Raw(",");
            }
            j.Raw("{\"level\":").Num((ulong)h.Level)
             .Raw(",\"text\":").Str(h.Text)
             .Raw(",\"ordinal\":").Num((ulong)h.Ordinal)
             .Raw(",\"anchor\":").Str(h.AnchorId)
             .Raw(",\"offset\":").Num((ulong)h.ByteOffset)
             .Raw("}");
        }
        j.Raw("]");

        j.Raw(",\"blocks\":[");
        var blocks = SlateUniffiMethods.ReadingBlocksSource(text);
        for (int i = 0; i < blocks.Length; i++)
        {
            var b = blocks[i];
            if (i > 0)
            {
                j.Raw(",");
            }
            j.Raw("{\"kind\":").Str(BlockKindName(b.Kind))
             .Raw(",\"start\":").Num(b.ByteStart)
             .Raw(",\"end\":").Num(b.ByteEnd)
             .Raw(",\"source\":").Str(b.Source)
             .Raw("}");
        }
        j.Raw("]}");
        return j + "\n";
    }

    /// <summary>
    /// Editor-scale §W-A artifact. Sources are deterministic ASCII fixtures
    /// generated identically by both harness twins; only canonical window
    /// results are serialized, so the golden remains small at the 8 MiB tier.
    /// </summary>
    public static string EditorScaleArtifact()
    {
        var j = new CanonicalJson();
        j.Raw("{\"sizes\":[");
        int[] sizes = [100 * 1024, 1024 * 1024, 8 * 1024 * 1024];
        for (int index = 0; index < sizes.Length; index++)
        {
            if (index > 0)
            {
                j.Raw(",");
            }

            string text = EditorScaleFixture(sizes[index]);
            j.Raw("{\"bytes\":").Num((ulong)sizes[index])
             .Raw(",\"span_windows\":");
            AppendSpanWindows(j, text);
            j.Raw("}");
        }
        j.Raw("]}");
        return j + "\n";
    }

    /// <summary>Vault-level artifact: pinned full-text-search queries.</summary>
    public static string SearchArtifact(VaultSession session, CancelToken cancel)
    {
        var j = new CanonicalJson();
        j.Raw("{\"queries\":[");
        for (int q = 0; q < PinnedSearchQueries.Length; q++)
        {
            if (q > 0)
            {
                j.Raw(",");
            }
            var rs = session.FullTextSearch(PinnedSearchQueries[q], new SearchScope.Vault(), cancel);
            var rows = rs.Rows
                .OrderBy(r => r.Path, StringComparer.Ordinal)
                .ThenBy(r => r.Snippet, StringComparer.Ordinal)
                .ToArray();
            j.Raw("{\"query\":").Str(PinnedSearchQueries[q]).Raw(",\"rows\":[");
            for (int i = 0; i < rows.Length; i++)
            {
                if (i > 0)
                {
                    j.Raw(",");
                }
                j.Raw("{\"path\":").Str(Slash(rows[i].Path))
                 .Raw(",\"snippet\":").Str(rows[i].Snippet)
                 .Raw(",\"score\":").Num(rows[i].Score)
                 .Raw("}");
            }
            j.Raw("]}");
        }
        j.Raw("]}");
        return j + "\n";
    }

    /// <summary>Vault-level artifact: outgoing links + backlinks per file.</summary>
    public static string LinksArtifact(VaultSession session, IReadOnlyList<string> relPaths)
    {
        var j = new CanonicalJson();
        j.Raw("{\"files\":[");
        for (int f = 0; f < relPaths.Count; f++)
        {
            if (f > 0)
            {
                j.Raw(",");
            }
            string rel = relPaths[f];
            j.Raw("{\"file\":").Str(Slash(rel));

            j.Raw(",\"outgoing\":[");
            var outgoing = session.OutgoingLinks(rel);
            for (int i = 0; i < outgoing.Length; i++)
            {
                var o = outgoing[i];
                if (i > 0)
                {
                    j.Raw(",");
                }
                j.Raw("{\"target\":");
                if (o.TargetPath == null)
                {
                    j.Null();
                }
                else
                {
                    j.Str(Slash(o.TargetPath));
                }
                j.Raw(",\"raw\":").Str(o.TargetRaw)
                 .Raw(",\"kind\":").Str(o.Kind)
                 .Raw(",\"embed\":").Bool(o.IsEmbed)
                 .Raw(",\"external\":").Bool(o.IsExternal)
                 .Raw(",\"unresolved\":").Bool(o.IsUnresolved)
                 .Raw(",\"ordinal\":").Num((ulong)o.Ordinal)
                 .Raw("}");
            }
            j.Raw("]");

            j.Raw(",\"backlinks\":[");
            var backlinks = session.Backlinks(rel, new Paging(null, 500)).Items;
            for (int i = 0; i < backlinks.Length; i++)
            {
                var b = backlinks[i];
                if (i > 0)
                {
                    j.Raw(",");
                }
                j.Raw("{\"source\":").Str(Slash(b.SourcePath))
                 .Raw(",\"snippet\":").Str(b.Snippet)
                 .Raw(",\"ordinal\":").Num((ulong)b.Ordinal)
                 .Raw(",\"kind\":").Str(b.Kind)
                 .Raw(",\"embed\":").Bool(b.IsEmbed)
                 .Raw("}");
            }
            j.Raw("]}");
        }
        j.Raw("]}");
        return j + "\n";
    }

    public static string SpanKindName(EditorSpanKind kind) => kind switch
    {
        EditorSpanKind.Heading h => $"heading:{h.Level}",
        EditorSpanKind.Emphasis => "emphasis",
        EditorSpanKind.Strong => "strong",
        EditorSpanKind.Strikethrough => "strikethrough",
        EditorSpanKind.InlineCode => "inline_code",
        EditorSpanKind.CodeFence => "code_fence",
        EditorSpanKind.Link => "link",
        EditorSpanKind.Image => "image",
        EditorSpanKind.BlockQuote => "block_quote",
        EditorSpanKind.Wikilink => "wikilink",
        EditorSpanKind.Embed => "embed",
        EditorSpanKind.Tag => "tag",
        EditorSpanKind.Citation => "citation",
        EditorSpanKind.Comment => "comment",
        EditorSpanKind.Frontmatter => "frontmatter",
        EditorSpanKind.Code c => $"code:{TokenKindName(c.Token)}",
        _ => throw new InvalidOperationException($"unmapped EditorSpanKind {kind}"),
    };

    public static string TokenKindName(TokenKind token) => token switch
    {
        TokenKind.Keyword => "keyword",
        TokenKind.String => "string",
        TokenKind.Number => "number",
        TokenKind.Comment => "comment",
        TokenKind.Identifier => "identifier",
        TokenKind.Type => "type",
        TokenKind.Function => "function",
        TokenKind.Operator => "operator",
        TokenKind.Punctuation => "punctuation",
        TokenKind.Other o => $"other:{o.Label}",
        _ => throw new InvalidOperationException($"unmapped TokenKind {token}"),
    };

    private static void AppendSpanWindows(CanonicalJson j, string text)
    {
        using var buffer = new DocumentBuffer(text);
        int length = text.Length;
        int[] anchors = [0, length / 2, length];
        j.Raw("[");
        for (int index = 0; index < anchors.Length; index++)
        {
            if (index > 0)
            {
                j.Raw(",");
            }

            int start = Math.Max(0, anchors[index] - 32);
            int end = Math.Min(length, anchors[index] + 32);
            RangedHighlight ranged = buffer.HighlightInRange(
                checked((uint)start),
                checked((uint)end));
            j.Raw("{\"request_start_utf16\":").Num((ulong)start)
             .Raw(",\"request_end_utf16\":").Num((ulong)end)
             .Raw(",\"applied_start\":").Num(ranged.AppliedStart)
             .Raw(",\"applied_end\":").Num(ranged.AppliedEnd)
             .Raw(",\"spans\":[");
            AppendSpans(j, ranged.Spans);
            j.Raw("]}");
        }
        j.Raw("]");
    }

    private static void AppendSpans(CanonicalJson j, IReadOnlyList<EditorSpan> spans)
    {
        for (int i = 0; i < spans.Count; i++)
        {
            if (i > 0)
            {
                j.Raw(",");
            }
            j.Raw("{\"start\":").Num(spans[i].StartByte)
             .Raw(",\"end\":").Num(spans[i].EndByte)
             .Raw(",\"kind\":").Str(SpanKindName(spans[i].Kind))
             .Raw("}");
        }
    }

    private static string EditorScaleFixture(int targetBytes)
    {
        const string block =
            "## Section\n\nProse with [[Wikilink]] and #tag plus `code` and [@citation].\n\n";
        var text = new System.Text.StringBuilder(targetBytes + block.Length);
        while (text.Length < targetBytes)
        {
            text.Append(block);
        }

        return text.ToString(0, targetBytes);
    }

    public static string BlockKindName(ReadingBlockKind kind) => kind switch
    {
        ReadingBlockKind.Heading h => $"heading:{h.Level}",
        ReadingBlockKind.Paragraph => "paragraph",
        ReadingBlockKind.ListItem l =>
            $"list_item:{l.Depth}:{(l.Ordered ? "ordered" : "unordered")}:{l.Task ?? "-"}",
        ReadingBlockKind.BlockQuote q => $"block_quote:{q.Depth}",
        ReadingBlockKind.CodeFence c => $"code_fence:{c.Language}",
        ReadingBlockKind.MathBlock => "math_block",
        ReadingBlockKind.Diagram d => $"diagram:{d.Dialect}",
        ReadingBlockKind.Table => "table",
        ReadingBlockKind.ThematicBreak => "thematic_break",
        ReadingBlockKind.Html => "html",
        _ => throw new InvalidOperationException($"unmapped ReadingBlockKind {kind}"),
    };

    private static string Slash(string path) => path.Replace('\\', '/');
}
