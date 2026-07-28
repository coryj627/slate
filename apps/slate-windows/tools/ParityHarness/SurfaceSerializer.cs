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

    /// <summary>
    /// Per-file artifact: spans + headings + reading blocks + inline runs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <c>inline_runs</c> array (#967) is 1:1 with <c>blocks</c> and
    /// closes the §W-A gap that block-only serialization left: wikilink /
    /// embed / tag / citation payloads, per-grammar resolution, and
    /// accessible text are now byte-checked across platforms.
    /// </para>
    /// <para>
    /// <b>Citation join, deliberately empty.</b> The fixture vault ships
    /// no CSL style and no bibliography, so there is nothing deterministic
    /// to render citations against; both twins therefore pass an EMPTY
    /// citation list. Core still emits <c>citation</c> runs — the kind
    /// comes from the span classifier, not from the rendered list — with
    /// the raw text as both display and speech, which is deterministic on
    /// every platform. Matched-citation rendering is covered by the core
    /// unit tests instead; committing a style + <c>.bib</c> fixture to
    /// bring it under §W-A is a recorded W8-4 candidate.
    /// </para>
    /// </remarks>
    public static string FileArtifact(string relPath, string text, VaultSession session)
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
        j.Raw("]");

        j.Raw(",\"inline_runs\":[");
        var inlines = SlateUniffiMethods.ReadingInlineSegmentsSource(
            text, Array.Empty<RenderedCitation>(), session.OutgoingLinks(relPath));
        for (int i = 0; i < inlines.Length; i++)
        {
            if (i > 0)
            {
                j.Raw(",");
            }
            AppendBlockInlines(j, inlines[i]);
        }
        j.Raw("]");

        // W3-4: the READING-side canonical artifact. The span rows above
        // byte-check the editor highlight surface; these rows byte-check
        // {source, syntax_tokens, semantic_spans} + the core preamble —
        // exactly what the WPF reading renderer consumes. Token offsets
        // are block-local.
        j.Raw(",\"code_blocks\":[");
        var codeBlocks = session.GetSyntaxTokens(relPath);
        for (int i = 0; i < codeBlocks.Length; i++)
        {
            var c = codeBlocks[i];
            if (i > 0)
            {
                j.Raw(",");
            }
            j.Raw("{\"line\":").Num(c.Line)
             .Raw(",\"offset\":").Num(c.ByteOffset)
             .Raw(",\"language\":");
            if (c.Language == null)
            {
                j.Raw("null");
            }
            else
            {
                j.Str(c.Language);
            }
            j.Raw(",\"preamble\":").Str(
                SlateUniffiMethods.CodeBlockPreamble(c.Language, c.Source))
             .Raw(",\"source\":").Str(c.Source)
             .Raw(",\"tokens\":[");
            for (int t = 0; t < c.Tokens.Length; t++)
            {
                var token = c.Tokens[t];
                if (t > 0)
                {
                    j.Raw(",");
                }
                j.Raw("{\"start\":").Num(token.StartByte)
                 .Raw(",\"end\":").Num(token.EndByte)
                 .Raw(",\"kind\":").Str(TokenKindName(token.Kind))
                 .Raw("}");
            }
            j.Raw("],\"semantic_spans\":[");
            for (int t = 0; t < c.SemanticSpans.Length; t++)
            {
                var span = c.SemanticSpans[t];
                if (t > 0)
                {
                    j.Raw(",");
                }
                j.Raw("{\"start\":").Num(span.StartByte)
                 .Raw(",\"end\":").Num(span.EndByte)
                 .Raw(",\"kind\":").Str(SemanticKindName(span.Kind))
                 .Raw(",\"name\":").Str(span.Name)
                 .Raw("}");
            }
            j.Raw("]}");
        }
        j.Raw("]");

        // W3-2: the canonical math artifact — byte-checks MathCAT's
        // speech and braille output cross-platform for the first time
        // (both twins call the same core, same default prefs). Braille
        // is hex so byte identity is exact and encoding-agnostic.
        j.Raw(",\"math_blocks\":[");
        var mathBlocks = session.GetMathBlocks(relPath);
        for (int i = 0; i < mathBlocks.Length; i++)
        {
            var m = mathBlocks[i];
            if (i > 0)
            {
                j.Raw(",");
            }
            j.Raw("{\"line\":").Num(m.Line)
             .Raw(",\"offset\":").Num(m.ByteOffset)
             .Raw(",\"display\":").Str(
                m.DisplayStyle == MathDisplayStyle.Block ? "block" : "inline")
             .Raw(",\"source\":").Str(m.Source)
             .Raw(",\"mathml\":").Str(m.Mathml)
             .Raw(",\"speech\":").Str(m.Speech)
             .Raw(",\"braille_hex\":").Str(Convert.ToHexString(m.Braille))
             .Raw("}");
        }
        j.Raw("]");

        // W3-3: the canonical diagram artifact — byte-checks the
        // structured description and render status cross-platform.
        // SVG bytes ride as SHA-256 + length (the same pure-Rust
        // renderer runs on both twins, so bytes are deterministic;
        // the hash keeps artifacts compact and drift loud).
        j.Raw(",\"diagram_blocks\":[");
        var diagramBlocks = session.GetDiagramBlocks(relPath);
        for (int i = 0; i < diagramBlocks.Length; i++)
        {
            var d = diagramBlocks[i];
            if (i > 0)
            {
                j.Raw(",");
            }
            string status = d.RenderStatus switch
            {
                DiagramRenderStatus.Ok => "ok",
                DiagramRenderStatus.UnsupportedDialect u =>
                    "unsupported:" + u.Reason,
                DiagramRenderStatus.RenderFailed f => "failed:" + f.Message,
                _ => throw new InvalidOperationException(
                    $"unmapped DiagramRenderStatus {d.RenderStatus}"),
            };
            byte[] svg = d.Svg ?? Array.Empty<byte>();
            j.Raw("{\"line\":").Num(d.Line)
             .Raw(",\"offset\":").Num(d.ByteOffset)
             .Raw(",\"dialect\":").Str(
                d.Dialect == DiagramDialect.Mermaid ? "mermaid"
                    : throw new InvalidOperationException(
                        $"unmapped DiagramDialect {d.Dialect}"))
             .Raw(",\"source\":").Str(d.Source)
             .Raw(",\"description\":").Str(d.StructuredDescription)
             .Raw(",\"status\":").Str(status)
             .Raw(",\"svg_len\":").Num((ulong)svg.Length)
             .Raw(",\"svg_sha256\":").Str(
                svg.Length == 0
                    ? ""
                    : Convert.ToHexString(
                        System.Security.Cryptography.SHA256.HashData(svg)))
             .Raw("}");
        }
        j.Raw("]}");
        return j + "\n";
    }

    public static string SemanticKindName(SemanticKind kind) => kind switch
    {
        SemanticKind.Function => "function",
        SemanticKind.Type => "type",
        SemanticKind.Variable => "variable",
        _ => throw new InvalidOperationException($"unmapped SemanticKind {kind}"),
    };

    private static void AppendBlockInlines(CanonicalJson j, ReadingBlockInlines inlines)
    {
        j.Raw("{\"embed\":");
        if (inlines.BlockEmbedKey == null)
        {
            j.Null();
        }
        else
        {
            j.Str(inlines.BlockEmbedKey);
        }
        j.Raw(",\"marker\":");
        if (inlines.ListMarker == null)
        {
            j.Null();
        }
        else
        {
            j.Str(inlines.ListMarker);
        }
        j.Raw(",\"segments\":[");
        for (int s = 0; s < inlines.Segments.Length; s++)
        {
            if (s > 0)
            {
                j.Raw(",");
            }
            var segment = inlines.Segments[s];
            j.Raw("{\"content\":").Str(segment.Content).Raw(",\"task\":");
            if (segment.TaskCompleted is bool completed)
            {
                j.Bool(completed);
            }
            else
            {
                j.Null();
            }
            j.Raw(",\"runs\":[");
            for (int r = 0; r < segment.Runs.Length; r++)
            {
                if (r > 0)
                {
                    j.Raw(",");
                }
                AppendInlineRun(j, segment.Runs[r]);
            }
            j.Raw("]}");
        }
        j.Raw("]}");
    }

    private static void AppendInlineRun(CanonicalJson j, ReadingInlineRun run)
    {
        j.Raw("{\"start\":").Num((ulong)run.Start)
         .Raw(",\"end\":").Num((ulong)run.End)
         .Raw(",\"styles\":[");
        for (int i = 0; i < run.Styles.Length; i++)
        {
            if (i > 0)
            {
                j.Raw(",");
            }
            j.Str(StyleName(run.Styles[i]));
        }
        j.Raw("]");
        // Naming convention (both twins): `kind` is the snake_case
        // discriminator with enum-ish scalars colon-joined (mirroring
        // `list_item:{depth}:{ordered}:{task}`); free-text payloads are
        // separate escaped fields, because a URL or target contains `:`
        // and colon-joining would make the value ambiguous.
        switch (run.Kind)
        {
            case ReadingInlineRunKind.Text:
                j.Raw(",\"kind\":").Str("text");
                break;
            case ReadingInlineRunKind.ExternalLink external:
                j.Raw(",\"kind\":").Str("external_link")
                 .Raw(",\"url\":").Str(external.Url);
                break;
            case ReadingInlineRunKind.Wikilink wiki:
                j.Raw(",\"kind\":").Str(
                    $"wikilink:{GrammarName(wiki.Grammar)}:"
                    + (wiki.Resolved ? "resolved" : "unresolved"))
                 .Raw(",\"target\":").Str(wiki.Target)
                 .Raw(",\"base_target\":").Str(wiki.BaseTarget)
                 .Raw(",\"anchor\":");
                if (wiki.Anchor == null)
                {
                    j.Null();
                }
                else
                {
                    j.Str(AnchorName(wiki.Anchor));
                }
                break;
            case ReadingInlineRunKind.Embed embed:
                j.Raw(",\"kind\":").Str("embed").Raw(",\"key\":").Str(embed.Key);
                break;
            case ReadingInlineRunKind.Tag tag:
                j.Raw(",\"kind\":").Str("tag").Raw(",\"name\":").Str(tag.Name);
                break;
            case ReadingInlineRunKind.Citation citation:
                j.Raw(",\"kind\":").Str("citation")
                 .Raw(",\"raw\":").Str(citation.Raw)
                 .Raw(",\"speech\":").Str(citation.Speech);
                break;
            default:
                throw new InvalidOperationException(
                    $"unmapped ReadingInlineRunKind {run.Kind}");
        }
        j.Raw(",\"ax\":");
        if (run.AxText == null)
        {
            j.Null();
        }
        else
        {
            j.Str(run.AxText);
        }
        j.Raw("}");
    }

    public static string StyleName(ReadingInlineStyle style) => style switch
    {
        ReadingInlineStyle.Emphasis => "emphasis",
        ReadingInlineStyle.Strong => "strong",
        ReadingInlineStyle.Strikethrough => "strikethrough",
        ReadingInlineStyle.InlineCode => "inline_code",
        _ => throw new InvalidOperationException($"unmapped ReadingInlineStyle {style}"),
    };

    public static string GrammarName(ReadingWikiGrammar grammar) => grammar switch
    {
        ReadingWikiGrammar.Wikilink => "wikilink",
        ReadingWikiGrammar.MarkdownDestination => "markdown_destination",
        _ => throw new InvalidOperationException($"unmapped ReadingWikiGrammar {grammar}"),
    };

    /// <summary>
    /// `h:&lt;text&gt;` / `b:&lt;text&gt;` — the anchor form both twins emit.
    /// </summary>
    public static string AnchorName(LinkAnchor anchor) =>
        (anchor.Kind == "block" ? "b:" : "h:") + anchor.Text;

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
