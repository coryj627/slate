// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using uniffi.slate_uniffi;
using WpfList = System.Windows.Documents.List;
using WpfTable = System.Windows.Documents.Table;

namespace SlateWindows.Reading;

/// <summary>
/// Open-list state carried ACROSS chunked builds (§W3-1 item 9). After
/// the surface's merge the list objects live on in the persistent
/// document, so a later chunk appends items into them directly — list
/// structure never depends on where a chunk boundary fell, and the
/// render ceiling can stay absolute.
/// </summary>
internal sealed class ReadingListBuildContext
{
    /// <summary>Stack[d] holds the open list accepting items at depth
    /// d and the last item added to it (the parent of depth d+1).</summary>
    internal List<(WpfList List, ListItem? LastItem)> Stack { get; } = new();

    /// <summary>
    /// The PROJECTION-WIDE highlight budget (W3-4 adversarial round
    /// 5): the per-fence token cap alone lets 250 sub-threshold dense
    /// fences allocate a million Runs in one dispatcher chunk. Each
    /// highlighted fence draws down this shared pool — carried across
    /// chunks exactly like list state — and fences after exhaustion
    /// degrade to plain paragraphs.
    /// </summary>
    internal int RemainingHighlightTokens { get; set; } =
        ReadingDocumentBuilder.ProjectionHighlightTokenBudget;

    /// <summary>
    /// The math analog (W3-2 round 7): the CORE budgets bound MathCAT
    /// work, not WPFMath geometry — 250 sub-threshold formulas in one
    /// chunk would still parse into giant Geometry on the dispatcher.
    /// Each visually rendered formula draws its LaTeX byte length
    /// from this shared pool, carried across chunks exactly like the
    /// highlight pool; formulas after exhaustion keep their FULL
    /// accessibility artifacts (speech, braille, MathML) and degrade
    /// only the visual to source-in-range.
    /// </summary>
    internal int RemainingMathRenderBytes { get; set; } =
        ReadingDocumentBuilder.ProjectionMathRenderByteBudgetOverrideForTests
            ?? ReadingDocumentBuilder.ProjectionMathRenderByteBudget;

    /// <summary>
    /// The diagram analog (W3-3): each decoded SVG draws its byte
    /// length from this shared pool, carried across chunks exactly
    /// like the highlight and math pools; diagrams after exhaustion
    /// keep description and source and degrade only the visual.
    /// </summary>
    internal int RemainingDiagramRenderBytes { get; set; } =
        ReadingDocumentBuilder.ProjectionDiagramRenderByteBudgetOverrideForTests
            ?? ReadingDocumentBuilder.ProjectionDiagramRenderByteBudget;

    /// <summary>
    /// The embed-image analog (W3-5 round 5): the encoded-byte pools
    /// bound FFI transfer, not PIXELS — a small compressed image
    /// repeated across the 2,000-block ceiling decoded once per
    /// OCCURRENCE (~9 GiB of surfaces from one 16 MiB charge). One
    /// frozen ImageSource per resolved key, shared by every
    /// occurrence, drawing decoded pixels from this pool; images
    /// after exhaustion keep header and destination and degrade only
    /// the visual with the honest budget notice.
    /// </summary>
    /// <summary>Per-key TERMINAL outcomes (round 6): a refused or
    /// undecodable key memoizes too, so no key is ever decoded more
    /// than once per projection — an admitted-only cache let an
    /// over-budget key re-decode at every occurrence.</summary>
    internal Dictionary<
        string,
        (System.Windows.Media.ImageSource? Source, bool BudgetRefused)>
        DecodedEmbedImages
    { get; } = new(StringComparer.Ordinal);

    /// <summary>See <see cref="DecodedEmbedImages"/>.</summary>
    internal long RemainingEmbedDecodedPixels { get; set; } =
        ReadingDocumentBuilder.ProjectionEmbedDecodedPixelBudgetOverrideForTests
            ?? ReadingDocumentBuilder.ProjectionEmbedDecodedPixelBudget;
}

/// <summary>The built reading document plus its navigation index.</summary>
internal sealed class ReadingDocumentModel
{
    public ReadingDocumentModel(FlowDocument document, IReadOnlyList<ReadingLandmark> landmarks)
    {
        Document = document;
        Landmarks = landmarks;
    }

    public FlowDocument Document { get; }

    /// <summary>Document-ordered; the navigator's whole input.</summary>
    public IReadOnlyList<ReadingLandmark> Landmarks { get; }
}

/// <summary>
/// Core reading model → <see cref="FlowDocument"/> (W3-1, #728).
///
/// Consumes the zipped output of <c>ReadingBlocksSource</c> +
/// <c>ReadingInlineSegmentsSource</c> exactly as
/// `w3_inline_runs_spec.md` §10 binds it: every semantic decision —
/// what is a link, what it activates with, whether it resolves, what a
/// run's accessible text is — arrived precomputed from core. This file
/// applies attributes and builds WPF structure; it never parses,
/// splits, re-derives, or re-classifies (§10.8 census-enforced).
///
/// Threading: pure WPF object construction — the caller runs the FFI
/// calls off the dispatcher (§10.1) and this on it.
/// </summary>
internal static class ReadingDocumentBuilder
{
    public static ReadingDocumentModel Build(
        IReadOnlyList<(ReadingBlock Block, ReadingBlockInlines Inlines)> model) =>
        Build(
            model,
            new ReadingListBuildContext(),
            Array.Empty<CodeBlock>(),
            Array.Empty<MathBlock>());

    public static ReadingDocumentModel Build(
        IReadOnlyList<(ReadingBlock Block, ReadingBlockInlines Inlines)> model,
        ReadingListBuildContext context) =>
        Build(model, context, Array.Empty<CodeBlock>(), Array.Empty<MathBlock>());

    public static ReadingDocumentModel Build(
        IReadOnlyList<(ReadingBlock Block, ReadingBlockInlines Inlines)> model,
        ReadingListBuildContext context,
        IReadOnlyList<CodeBlock> codeBlocks) =>
        Build(model, context, codeBlocks, Array.Empty<MathBlock>());

    public static ReadingDocumentModel Build(
        IReadOnlyList<(ReadingBlock Block, ReadingBlockInlines Inlines)> model,
        ReadingListBuildContext context,
        IReadOnlyList<CodeBlock> codeBlocks,
        IReadOnlyList<MathBlock> mathBlocks) =>
        Build(model, context, codeBlocks, mathBlocks, Array.Empty<DiagramBlock>());

    public static ReadingDocumentModel Build(
        IReadOnlyList<(ReadingBlock Block, ReadingBlockInlines Inlines)> model,
        ReadingListBuildContext context,
        IReadOnlyList<CodeBlock> codeBlocks,
        IReadOnlyList<MathBlock> mathBlocks,
        IReadOnlyList<DiagramBlock> diagramBlocks) =>
        Build(
            model, context, codeBlocks, mathBlocks, diagramBlocks,
            Array.Empty<ReadingEmbedArtifact>());

    public static ReadingDocumentModel Build(
        IReadOnlyList<(ReadingBlock Block, ReadingBlockInlines Inlines)> model,
        ReadingListBuildContext context,
        IReadOnlyList<CodeBlock> codeBlocks,
        IReadOnlyList<MathBlock> mathBlocks,
        IReadOnlyList<DiagramBlock> diagramBlocks,
        IReadOnlyList<ReadingEmbedArtifact> embeds)
    {
        var document = new FlowDocument
        {
            FontSize = 15,
            PagePadding = new Thickness(16),
            // Column layout off: the reading view is a single flow, and
            // multi-column reading order is a §W-C hazard.
            ColumnWidth = double.PositiveInfinity,
        };
        document.SetResourceReference(FlowDocument.FontFamilyProperty, "Slate.ReadingFontFamily");

        List<(WpfList List, ListItem? LastItem)> stack = context.Stack;

        foreach ((ReadingBlock block, ReadingBlockInlines inlines) in model)
        {
            // §10.5: a paragraph that IS one wikilink embed expands as a
            // card. Detection is core's — BlockEmbedKey, never a string
            // test here.
            if (inlines.BlockEmbedKey is { Length: > 0 } embedKey)
            {
                stack.Clear();
                document.Blocks.Add(EmbedCard(
                    embedKey,
                    embeds.FirstOrDefault(candidate =>
                        string.Equals(candidate.Key, embedKey, StringComparison.Ordinal)),
                    context));
                continue;
            }

            if (block.Kind is ReadingBlockKind.ListItem listKind)
            {
                AppendListItem(document, stack, listKind, block, inlines);
                continue;
            }

            stack.Clear();
            document.Blocks.Add(
                NonListBlock(block, inlines, codeBlocks, mathBlocks, diagramBlocks, context));
        }

        return new ReadingDocumentModel(document, CollectLandmarks(document));
    }

    /// <summary>
    /// Depth-stack list construction: every level nests inside the last
    /// item of the level above it — structure, not indentation, is what
    /// the ListItem peers expose. Ordered-ness is compared AT THE
    /// ITEM'S OWN LEVEL, so an ordered sublist inside a bullet list
    /// closes nothing above it; a marker change at one level replaces
    /// only that level's list (two lists, two quick-nav stops — the AT
    /// convention). A list opened in an earlier chunk continues through
    /// <see cref="ReadingListBuildContext"/>: its object already lives
    /// in the surface's persistent document, and items append into it
    /// directly.
    /// </summary>
    private static void AppendListItem(
        FlowDocument document,
        List<(WpfList List, ListItem? LastItem)> stack,
        ReadingBlockKind.ListItem kind,
        ReadingBlock block,
        ReadingBlockInlines inlines)
    {
        // A level can only open one deeper than the deepest open level;
        // core emits authored depths, so the clamp fires only on input
        // this builder never sees from it (defensive).
        int depth = Math.Min(kind.Depth, stack.Count);

        // Levels deeper than this item close.
        if (stack.Count > depth + 1)
        {
            stack.RemoveRange(depth + 1, stack.Count - depth - 1);
        }

        // Marker change at this level: close and replace this level only.
        if (stack.Count == depth + 1
            && (stack[depth].List.MarkerStyle == TextMarkerStyle.Decimal) != kind.Ordered)
        {
            stack.RemoveAt(depth);
        }

        if (stack.Count == depth)
        {
            var list = new WpfList
            {
                MarkerStyle = kind.Ordered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc,
            };
            ReadingSemantics.MarkList(list);
            if (depth > 0 && stack[depth - 1].LastItem is { } parent)
            {
                parent.Blocks.Add(list);
            }
            else
            {
                document.Blocks.Add(list);
            }
            stack.Add((list, null));
        }

        Paragraph content = InlineParagraph(
            inlines,
            taskRange: kind.Task is not null ? (block.ByteStart, block.ByteEnd) : null);
        var item = new ListItem(content);
        ReadingSemantics.MarkListItem(item);
        stack[depth].List.ListItems.Add(item);
        stack[depth] = (stack[depth].List, item);
    }

    private static Block NonListBlock(
        ReadingBlock block,
        ReadingBlockInlines inlines,
        IReadOnlyList<CodeBlock> codeBlocks,
        IReadOnlyList<MathBlock> mathBlocks,
        IReadOnlyList<DiagramBlock> diagramBlocks,
        ReadingListBuildContext context)
    {
        switch (block.Kind)
        {
            case ReadingBlockKind.Heading heading:
                {
                    Paragraph paragraph = InlineParagraph(inlines, taskRange: null);
                    paragraph.FontSize = heading.Level switch
                    {
                        1 => 26,
                        2 => 21,
                        3 => 18,
                        _ => 16,
                    };
                    paragraph.FontWeight = FontWeights.SemiBold;
                    // Narrator (and any future UIA consumer of the property)
                    // reads this; NVDA does not during linear reading — the
                    // measured limitation G21 records. The chorded commands
                    // are the cross-AT navigation path.
                    AutomationProperties.SetHeadingLevel(paragraph, ToHeadingLevel(heading.Level));
                    ReadingSemantics.MarkHeading(paragraph, heading.Level);
                    return paragraph;
                }

            case ReadingBlockKind.BlockQuote:
                {
                    Paragraph quote = InlineParagraph(inlines, taskRange: null);
                    quote.Padding = new Thickness(12, 2, 0, 2);
                    quote.BorderThickness = new Thickness(3, 0, 0, 0);
                    quote.SetResourceReference(Block.BorderBrushProperty, "Slate.AccentBrush");
                    return quote;
                }

            case ReadingBlockKind.CodeFence fence:
                return CodeFenceBlock(fence, block, codeBlocks, context);

            case ReadingBlockKind.Table:
                return TableBlock(block);

            case ReadingBlockKind.ThematicBreak:
                return new BlockUIContainer(new Separator { Margin = new Thickness(0, 8, 0, 8) });

            case ReadingBlockKind.MathBlock:
                return MathBlockElement(block, mathBlocks, context);

            case ReadingBlockKind.Diagram diagram:
                return DiagramBlockElement(diagram, block, diagramBlocks, context);

            case ReadingBlockKind.Html:
            default:
                {
                    // HTML renders as source per the mac contract: the
                    // block's source stays IN the text range, monospace
                    // — never silently absent.
                    if (inlines.Segments.Length == 0)
                    {
                        return MonospaceParagraph(block.Source.TrimEnd('\n', '\r'));
                    }
                    return InlineParagraph(inlines, taskRange: null);
                }
        }
    }

    /// <summary>
    /// W3-4 code fence: canonical token rendering INSIDE the text range
    /// (the W3-1 spike measured `BlockUIContainer` content as silently
    /// absent from say-all — "landmarks missing: fn main" — and a
    /// reading view that skips code is a correctness failure). The
    /// fence matches its pipeline <see cref="CodeBlock"/> by byte
    /// containment — mac's `codeModel` rule — and renders that block's
    /// source as token-colored runs; a live-buffer drift miss degrades
    /// to the authoritative interior, un-highlighted, with the preamble
    /// still correct. The preamble is CORE's
    /// (<c>CodeBlockPreamble</c> — hoisted for W3-4 so both hosts speak
    /// identical strings) and rides the paragraph's AutomationName for
    /// the peer. The visible Copy button precedes the code (mac #854:
    /// always visible, in layout); its Tag carries the exact source to
    /// copy — a plain string, per the undo-serialization lesson.
    /// </summary>
    private static Block CodeFenceBlock(
        ReadingBlockKind.CodeFence fence,
        ReadingBlock block,
        IReadOnlyList<CodeBlock> codeBlocks,
        ReadingListBuildContext context)
    {
        CodeBlock? matched = codeBlocks.FirstOrDefault(candidate =>
            candidate.ByteOffset >= block.ByteStart
            && candidate.ByteOffset < block.ByteEnd);

        // The LIVE fence interior is authoritative for display, the
        // preamble, and Copy — the saved artifact only contributes
        // TOKENS, and only when it is coherent with the live content
        // (an unsaved edit inside a fence keeps byte containment, and
        // rendering or copying the stale saved source would lie to the
        // reader and the clipboard). Coherence is compared over
        // LF-NORMALIZED saved source (the saved artifact preserves raw
        // CRLF; the reading interior is LF — measured: every CRLF
        // fence silently lost highlighting under an exact compare),
        // modulo the one trailing newline the reading interior strips,
        // plus identical language.
        string normalizedSaved = matched?.Source.Replace("\r\n", "\n") ?? string.Empty;
        bool coherent = matched is not null
            && (string.Equals(normalizedSaved, fence.Interior, StringComparison.Ordinal)
                || string.Equals(
                    normalizedSaved,
                    fence.Interior + "\n",
                    StringComparison.Ordinal))
            && string.Equals(
                matched.Language ?? string.Empty,
                fence.Language,
                StringComparison.Ordinal);

        string source = fence.Interior;
        string? language = fence.Language.Length == 0 ? null : fence.Language;
        string preamble = SlateUniffiMethods.CodeBlockPreamble(language, source);

        // The interior renders VERBATIM — TrimEnd ate authored trailing
        // blank lines, so display disagreed with the preamble's count
        // and with what Copy delivered. Highlighting draws down the
        // projection-wide budget: fences after exhaustion render plain
        // (round 5 — the per-fence cap alone lets many sub-threshold
        // dense fences aggregate into dispatcher-scale Run fan-out).
        // Charge order matters: the per-fence cap and colorability run
        // BEFORE the pool deduction, so a fence that renders plain for
        // its own reasons never drains the budget other fences need
        // (round 6 — two over-cap fences silently un-highlighted the
        // rest of the note).
        bool highlightable = coherent
            && matched!.Tokens.Length <= MaximumHighlightTokens
            && HasColorableToken(matched)
            && matched.Tokens.Length <= context.RemainingHighlightTokens;
        Paragraph paragraph;
        if (highlightable)
        {
            context.RemainingHighlightTokens -= matched!.Tokens.Length;
            paragraph = TokenParagraph(matched, fence.Interior);
        }
        else
        {
            paragraph = MonospaceParagraph(fence.Interior);
        }
        ReadingSemantics.MarkCodeBlock(paragraph);
        AutomationProperties.SetName(paragraph, preamble);

        var copy = new Button
        {
            Content = "Copy code",
            Padding = new Thickness(8, 2, 8, 2),
            HorizontalAlignment = HorizontalAlignment.Right,
            Tag = source,
        };
        ReadingSemantics.MarkCodeCopy(copy);
        AutomationProperties.SetHelpText(
            copy, "Copies the code block source as plain text.");
        var section = new Section();
        section.Blocks.Add(new BlockUIContainer(copy)
        {
            Padding = new Thickness(0),
            Margin = new Thickness(0, 4, 0, 0),
        });
        section.Blocks.Add(paragraph);
        return section;
    }

    /// <summary>
    /// The LIVE interior as token-colored runs — plain attribute
    /// application over core-computed byte ranges (§10.8: nothing is
    /// re-derived; gaps keep the default text brush, exactly as mac's
    /// attributed-string base layer does). Token offsets are UTF-8
    /// into the RAW saved source, which may carry CRLF the interior
    /// normalizes away — each offset shifts left by the count of
    /// CR-in-CRLF bytes before it (coherence guarantees the normalized
    /// forms are identical), then clamps to the rendered length.
    /// </summary>
    /// <summary>
    /// W3-2 math block (gap G23's guaranteed layer): the canonical
    /// artifact matches by byte containment, BLOCK display style only
    /// (the mac codeModel rule), and renders through WPFMath into a
    /// focusable <see cref="ReadingMathElement"/> whose Name is the
    /// MathCAT speech — spoken on focus/Tab/object navigation in stock
    /// NVDA and JAWS, with the MathML convention property riding the
    /// peer. Unmatched blocks and TexException coverage gaps (the
    /// documented WPFMath subset) degrade to the source IN the text
    /// range — never silently absent — with the block still a nav
    /// stop; the landing announcement then carries the speech when the
    /// artifact matched, or the raw source otherwise (content either
    /// way, composition never).
    /// </summary>
    private static Block MathBlockElement(
        ReadingBlock block,
        IReadOnlyList<MathBlock> mathBlocks,
        ReadingListBuildContext context)
    {
        MathBlock? matched = mathBlocks.FirstOrDefault(candidate =>
            candidate.DisplayStyle == MathDisplayStyle.Block
            && candidate.ByteOffset >= block.ByteStart
            && candidate.ByteOffset < block.ByteEnd);

        // LIVE-source coherence (the W3-4 lesson, round-1 finding
        // here): the reading block is parsed from the live buffer, the
        // artifact from the SAVED file, and a same-position unsaved
        // edit ($$x$$ -> $$y$$) keeps byte containment. Speaking the
        // old equation would be an outright lie to an AT user, so the
        // artifact only applies when its source equals the live fence
        // interior (delimiters stripped host-side — the mac
        // strippedMathDelimiters precedent; a core interior field for
        // the reading MathBlock variant is the recorded follow-up).
        if (matched is not null
            && !string.Equals(
                matched.Source.Trim(),
                StripMathDelimiters(block.Source),
                StringComparison.Ordinal))
        {
            matched = null;
        }

        string speech = matched is { Speech.Length: > 0 } speechful
            ? speechful.Speech.Trim()
            : "Math expression.";
        string fallbackSource = block.Source.TrimEnd('\n', '\r');

        if (matched is null)
        {
            Paragraph unmatchedParagraph = MonospaceParagraph(fallbackSource);
            ReadingSemantics.MarkMathBlock(unmatchedParagraph, fallbackSource);
            return unmatchedParagraph;
        }

        // Core's degradation verdict gates the HOST renderer too
        // (round 6): budget-rejected and unconvertible formulas carry
        // empty MathML, and feeding their raw source to WPFMath would
        // re-do on the dispatcher exactly the unbounded work the core
        // budget refused — supported oversized TeX parses into a giant
        // Geometry. The conservative cost: a formula pulldown-latex
        // cannot convert loses its visual even if WPFMath could have
        // drawn it; it keeps source-in-range + the artifact element.
        //
        // Round 7 completes the W3-4 shape: within core's budgets a
        // formula is still WPFMath work proportional to its LaTeX, so
        // the HOST charges a per-formula cap plus the projection-wide
        // pool before entering the renderer. Charged on entry — a
        // failed parse burned the dispatcher too.
        System.Windows.UIElement? visual = null;
        int renderCost = System.Text.Encoding.UTF8.GetByteCount(matched.Source);
        if (matched.Mathml.Length > 0
            && renderCost <= MaximumRenderedFormulaSourceBytes
            && renderCost <= context.RemainingMathRenderBytes)
        {
            context.RemainingMathRenderBytes -= renderCost;
            visual = TryRenderFormula(matched.Source);
        }
        if (visual is null)
        {
            // Coverage gap (documented WPFMath subset, matrix-rowed):
            // source stays readable in range — and the CORE artifacts
            // stay retrievable (round 4: a host-only rendering failure
            // must not hide valid braille or MathML). A zero-size
            // focusable element at the fence end carries Name,
            // ItemStatus, and the MathML property exactly like the
            // rendered path; WPF text ranges blank embedded objects, so
            // it costs the in-range source nothing.
            Paragraph gapParagraph = MonospaceParagraph(fallbackSource);
            gapParagraph.Inlines.Add(new InlineUIContainer(
                new ReadingMathElement(
                    speech,
                    matched.Mathml,
                    matched.Source,
                    DecodeBraille(matched.Braille))));
            ReadingSemantics.MarkMathBlock(gapParagraph, speech);
            return gapParagraph;
        }

        var element = new ReadingMathElement(
            speech, matched.Mathml, matched.Source, DecodeBraille(matched.Braille))
        {
            Content = visual,
            Margin = new Thickness(0, 4, 0, 4),
        };
        var paragraph = new Paragraph(new InlineUIContainer(element))
        {
            TextAlignment = TextAlignment.Center,
        };
        ReadingSemantics.MarkMathBlock(paragraph, speech);
        return paragraph;
    }

    /// <summary>The braille artifact decodes as UTF-8 (Nemeth is
    /// ASCII, UEB is Unicode cells — MathCAT emits per the session
    /// pref); undecodable or absent bytes become empty, which the
    /// element treats as "no braille".</summary>
    private static string DecodeBraille(byte[] braille)
    {
        try
        {
            return System.Text.Encoding.UTF8.GetString(braille).Trim();
        }
        catch (ArgumentException)
        {
            return string.Empty;
        }
    }

    /// <summary>The live fence interior for coherence comparison:
    /// trims, then strips the $$/$ delimiter pair the reading block's
    /// raw slice carries (mac ReadingPrintComposer precedent).</summary>
    private static string StripMathDelimiters(string source)
    {
        string trimmed = source.Trim();
        if (trimmed.StartsWith("$$", StringComparison.Ordinal)
            && trimmed.EndsWith("$$", StringComparison.Ordinal)
            && trimmed.Length >= 4)
        {
            return trimmed[2..^2].Trim();
        }
        if (trimmed.StartsWith('$') && trimmed.EndsWith('$') && trimmed.Length >= 2)
        {
            return trimmed[1..^1].Trim();
        }
        return trimmed;
    }

    /// <summary>
    /// LaTeX to a themed vector Path via WPFMath; null on the parser's
    /// documented coverage boundary (TexException family) or renderer
    /// failure — callers degrade to source-in-range.
    /// </summary>
    /// <summary>Fires with the LaTeX whenever the WPFMath renderer is
    /// entered — the round-6 pin that core-degraded artifacts never
    /// reach it asserts this stays silent.</summary>
    internal static Action<string>? FormulaRenderProbeForTests;

    private static System.Windows.UIElement? TryRenderFormula(string latex)
    {
        FormulaRenderProbeForTests?.Invoke(latex);
        try
        {
            XamlMath.TexFormula formula =
                WpfMath.Parsers.WpfTeXFormulaParser.Instance.Parse(latex);
            XamlMath.TexEnvironment environment =
                WpfMath.Rendering.WpfTeXEnvironment.Create(
                    XamlMath.TexStyle.Display, 20.0, "Arial");
            System.Windows.Media.Geometry geometry =
                WpfMath.Rendering.WpfTeXFormulaExtensions.RenderToGeometry(
                    formula, environment);
            var path = new System.Windows.Shapes.Path
            {
                Data = geometry,
                Stretch = System.Windows.Media.Stretch.None,
            };
            path.SetResourceReference(
                System.Windows.Shapes.Path.FillProperty, "Slate.TextBrush");
            return path;
        }
        catch (XamlMath.Exceptions.TexException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// The run-budget preflight: the loop below creates a WPF Run per
    /// token plus one per gap, and core's 256 KiB BYTE cap does not
    /// bound token DENSITY — a valid dense JSON fence under the cap
    /// can carry six-figure token counts, and dispatcher-scale Run
    /// fan-out freezes the reading view. Over budget degrades to the
    /// plain paragraph, exactly like oversized. (A core-side token
    /// ceiling as defense in depth would change the §W-A artifact and
    /// both hosts — recorded option, not this PR.)
    /// </summary>
    internal const int MaximumHighlightTokens = 4_000;

    /// <summary>The projection-wide pool the per-fence cap draws from
    /// (see <see cref="ReadingListBuildContext.RemainingHighlightTokens"/>):
    /// three maximal fences, or dozens of ordinary ones — bounded Run
    /// fan-out no matter how many fences a note holds.</summary>
    internal const int ProjectionHighlightTokenBudget = 12_000;

    /// <summary>Host visual cap per formula (W3-2 round 7): core's
    /// 16 KiB budget bounds MathCAT, but WPFMath geometry complexity
    /// scales with the LaTeX too, and no legitimate DISPLAYED formula
    /// approaches 4 KiB of source. Over-cap formulas keep speech,
    /// braille, and MathML — only the visual degrades.</summary>
    internal const int MaximumRenderedFormulaSourceBytes = 4 * 1024;

    /// <summary>The projection-wide pool the per-formula cap draws
    /// from (see
    /// <see cref="ReadingListBuildContext.RemainingMathRenderBytes"/>)
    /// — the exact shape of <see cref="ProjectionHighlightTokenBudget"/>:
    /// sixteen maximal formulas, or hundreds of ordinary ones.</summary>
    internal const int ProjectionMathRenderByteBudget = 64 * 1024;

    /// <summary>Budget override so the projection-pool exhaustion test
    /// doesn't need hundreds of MathCAT-heavy fixtures. Null in
    /// production.</summary>
    internal static int? ProjectionMathRenderByteBudgetOverrideForTests;

    /// <summary>Core degrades oversized (>256 KiB) and
    /// unknown-language blocks to tokens that carry no color — those
    /// build the plain paragraph with zero offset machinery and must
    /// never charge the highlight pool.</summary>
    private static bool HasColorableToken(CodeBlock matched)
    {
        foreach (SyntaxToken probe in matched.Tokens)
        {
            if (TokenBrushKey(probe.Kind) is not null)
            {
                return true;
            }
        }
        return false;
    }

    private static Paragraph TokenParagraph(CodeBlock matched, string interior)
    {
        // Defense in depth: callers gate on the per-fence cap and
        // colorability before charging the pool, but this stays safe
        // standalone.
        if (matched.Tokens.Length > MaximumHighlightTokens
            || !HasColorableToken(matched))
        {
            return MonospaceParagraph(interior);
        }

        string display = interior;
        byte[] utf8 = Encoding.UTF8.GetBytes(display);
        byte[] raw = Encoding.UTF8.GetBytes(matched.Source);
        // Sparse CRLF remap: byte positions of each CR-in-CRLF in the
        // raw saved source; an offset shifts left by the count of
        // positions before it (binary search) — never an int per byte.
        var carriageReturns = new List<int>();
        for (int i = 0; i + 1 < raw.Length; i++)
        {
            if (raw[i] == (byte)'\r' && raw[i + 1] == (byte)'\n')
            {
                carriageReturns.Add(i);
            }
        }
        int Normalized(int rawOffset)
        {
            int index = carriageReturns.BinarySearch(rawOffset);
            return rawOffset - (index >= 0 ? index : ~index);
        }
        var paragraph = new Paragraph
        {
            FontFamily = new FontFamily("Cascadia Mono, Consolas, monospace"),
            Padding = new Thickness(8),
        };
        paragraph.SetResourceReference(Block.BackgroundProperty, "Slate.RaisedSurfaceBrush");

        int cursor = 0;
        foreach (SyntaxToken token in matched.Tokens)
        {
            int rawStart = Math.Min((int)token.StartByte, raw.Length);
            int rawEnd = Math.Min((int)token.EndByte, raw.Length);
            int start = Math.Clamp(Normalized(rawStart), cursor, utf8.Length);
            int end = Math.Clamp(Normalized(rawEnd), start, utf8.Length);
            if (start > cursor)
            {
                paragraph.Inlines.Add(
                    new Run(Encoding.UTF8.GetString(utf8, cursor, start - cursor)));
            }
            if (end > start)
            {
                var run = new Run(Encoding.UTF8.GetString(utf8, start, end - start));
                if (TokenBrushKey(token.Kind) is { } key)
                {
                    run.SetResourceReference(TextElement.ForegroundProperty, key);
                }
                paragraph.Inlines.Add(run);
            }
            cursor = end;
        }
        if (cursor < utf8.Length)
        {
            paragraph.Inlines.Add(
                new Run(Encoding.UTF8.GetString(utf8, cursor, utf8.Length - cursor)));
        }
        return paragraph;
    }

    /// <summary>
    /// Token kind → theme brush key (mac `CodeTokenTheme` parity, APCA
    /// gated in `ThemeTokenContrastTests`). Null keeps the default text
    /// brush: identifier/operator/other match mac's label-color rule,
    /// and `function` gets its OWN gated color rather than mac's
    /// accent — accent/raised is the recorded #1051 near-miss.
    /// </summary>
    private static string? TokenBrushKey(TokenKind kind) => kind switch
    {
        TokenKind.Keyword => "Slate.CodeKeywordBrush",
        TokenKind.String => "Slate.CodeStringBrush",
        TokenKind.Number => "Slate.CodeNumberBrush",
        TokenKind.Comment => "Slate.CodeCommentBrush",
        TokenKind.Type => "Slate.CodeTypeBrush",
        TokenKind.Function => "Slate.CodeFunctionBrush",
        TokenKind.Punctuation => "Slate.SecondaryTextBrush",
        _ => null,
    };

    /// <summary>
    /// W3-3 diagram block: the canonical SVG artifact rendered through
    /// the W2-3 hardened Svg.Skia path, with the CORE structured
    /// description as the entire AT surface (mac contract). The fence
    /// matches its pipeline <see cref="DiagramBlock"/> by byte
    /// containment; a live-buffer drift miss degrades to
    /// source-in-range (mac's unmatched fallback — never a fabricated
    /// failure status). Every failure branch keeps the source IN the
    /// text range and appends a zero-size focusable element carrying
    /// the description, so Tab and object navigation always speak the
    /// canonical content (the W3-2 round-4 lesson applied at design
    /// time).
    /// </summary>
    private static Block DiagramBlockElement(
        ReadingBlockKind.Diagram diagram,
        ReadingBlock block,
        IReadOnlyList<DiagramBlock> diagramBlocks,
        ReadingListBuildContext context)
    {
        DiagramBlock? matched = diagramBlocks.FirstOrDefault(candidate =>
            candidate.ByteOffset >= block.ByteStart
            && candidate.ByteOffset < block.ByteEnd);

        // LIVE-source coherence (the W3-4/W3-2 lesson): the reading
        // block is parsed from the live buffer, the artifact from the
        // SAVED file, and a same-position unsaved edit keeps byte
        // containment. Diagram fences are code-shaped, so the code
        // precedent applies: LF-normalized interior equality.
        if (matched is not null)
        {
            string normalized = matched.Source.Replace("\r\n", "\n");
            if (!string.Equals(normalized, diagram.Interior, StringComparison.Ordinal)
                && !string.Equals(normalized, diagram.Interior + "\n", StringComparison.Ordinal)
                && !string.Equals(normalized + "\n", diagram.Interior, StringComparison.Ordinal))
            {
                matched = null;
            }
        }

        string fallbackSource = diagram.Interior.TrimEnd('\n', '\r');
        if (matched is null)
        {
            // Mac's unmatched fallback: raw source, no fabricated
            // status. The landing announcement carries the source —
            // the math unmatched precedent.
            Paragraph unmatchedParagraph = MonospaceParagraph(fallbackSource);
            ReadingSemantics.MarkDiagramBlock(unmatchedParagraph, fallbackSource);
            return unmatchedParagraph;
        }

        // The description is the entire primary AT surface — verbatim
        // core content, never composed; "Mermaid diagram." is the mac
        // defensive fallback for an empty artifact.
        string description = matched.StructuredDescription.Trim();
        if (description.Length == 0)
        {
            description = "Mermaid diagram.";
        }
        // Landing text: trailing period stripped so the canonical
        // vocabulary's own punctuation composes ("…3 steps, diagram.").
        string landing = description.TrimEnd('.');

        System.Windows.Media.ImageSource? visual = null;
        string? failureHeader = null;
        string failureReason = string.Empty;
        switch (matched.RenderStatus)
        {
            case DiagramRenderStatus.UnsupportedDialect unsupported:
                failureHeader = "Diagram dialect not supported";
                failureReason = unsupported.Reason;
                break;
            case DiagramRenderStatus.RenderFailed failed:
                failureHeader = "Diagram could not be rendered";
                failureReason = failed.Message;
                break;
            default:
                if (matched.Svg is not { Length: > 0 } svg)
                {
                    // Mac audit #254 L1: Ok status with nil OR EMPTY
                    // bytes routes to the decode-failure fallback.
                    failureHeader = "Diagram could not be rendered";
                    failureReason = "diagram rendered but image could not be decoded";
                    break;
                }
                // Host visual budgets (the W3-2 round-7 shape): core
                // bounds the RENDERER's work, not the host decoder's —
                // per-diagram SVG cap plus the projection-wide pool,
                // charged on entry (a failed decode burned the
                // dispatcher too). Over-budget keeps the description
                // and source; only the visual degrades.
                if (svg.Length > MaximumRenderedDiagramSvgBytes
                    || svg.Length > context.RemainingDiagramRenderBytes)
                {
                    failureHeader = "Diagram could not be rendered";
                    failureReason = "diagram image exceeds the display budget";
                    break;
                }
                context.RemainingDiagramRenderBytes -= svg.Length;
                DiagramRenderProbeForTests?.Invoke(svg.Length);
                visual = EditorInteractionCoordinator.DecodeImage(svg, "image/svg+xml");
                if (visual is null)
                {
                    failureHeader = "Diagram could not be rendered";
                    failureReason = "diagram rendered but image could not be decoded";
                }
                break;
        }

        if (visual is not null)
        {
            var element = new ReadingDiagramElement(description, matched.Source)
            {
                Content = new ReadingDiagramImage
                {
                    Source = visual,
                    Stretch = System.Windows.Media.Stretch.Uniform,
                    StretchDirection = StretchDirection.DownOnly,
                    // Mac caps at a Dynamic-Type-scaled 600pt (audit
                    // #254 M1); WPF has no text-size metric to scale
                    // by, so the base cap ships and the scaling is a
                    // recorded matrix note.
                    MaxHeight = 600,
                },
                Margin = new Thickness(0, 4, 0, 4),
            };
            var renderedParagraph = new Paragraph(new InlineUIContainer(element))
            {
                TextAlignment = TextAlignment.Center,
            };
            ReadingSemantics.MarkDiagramBlock(renderedParagraph, landing);
            return renderedParagraph;
        }

        // Failure presentation, mac shape: header, reason, then the
        // full source — all IN the text range — plus the zero-size
        // focusable element so the description stays Tab-reachable.
        var paragraph = new Paragraph { Padding = new Thickness(8) };
        paragraph.SetResourceReference(Block.BackgroundProperty, "Slate.RaisedSurfaceBrush");
        paragraph.Inlines.Add(new Run(failureHeader)
        {
            FontWeight = FontWeights.SemiBold,
        });
        if (failureReason.Length > 0)
        {
            paragraph.Inlines.Add(new LineBreak());
            paragraph.Inlines.Add(new Run(failureReason));
        }
        if (fallbackSource.Length > 0)
        {
            paragraph.Inlines.Add(new LineBreak());
            paragraph.Inlines.Add(new Run(fallbackSource)
            {
                FontFamily = new FontFamily("Cascadia Mono, Consolas, monospace"),
            });
        }
        paragraph.Inlines.Add(new InlineUIContainer(
            new ReadingDiagramElement(description, matched.Source)));
        ReadingSemantics.MarkDiagramBlock(paragraph, landing);
        return paragraph;
    }

    /// <summary>Host visual cap per diagram (W3-3): core's budgets
    /// bound the mermaid renderer, but Svg.Skia decode + raster work
    /// scales with the SVG too. Real diagram SVGs are tens of
    /// kilobytes; over-cap diagrams keep description and source —
    /// only the visual degrades.</summary>
    internal const int MaximumRenderedDiagramSvgBytes = 512 * 1024;

    /// <summary>The projection-wide pool the per-diagram cap draws
    /// from (see
    /// <see cref="ReadingListBuildContext.RemainingDiagramRenderBytes"/>)
    /// — the exact shape of <see cref="ProjectionMathRenderByteBudget"/>:
    /// four maximal diagrams, or dozens of ordinary ones.</summary>
    internal const int ProjectionDiagramRenderByteBudget = 2 * 1024 * 1024;

    /// <summary>Budget override so the pool-exhaustion test doesn't
    /// need megabytes of fixtures. Null in production.</summary>
    internal static int? ProjectionDiagramRenderByteBudgetOverrideForTests;

    /// <summary>Projection-wide DECODED-pixel pool for embed images
    /// (W3-5 round 5): ~16 MP ≈ 64 MB of BGRA surfaces — a dozen
    /// maximum-dimension images or hundreds of ordinary ones. The
    /// per-image dimension cap (1120 px, DecodeImage) bounds the
    /// transient overshoot of the one decode that discovers
    /// exhaustion.</summary>
    internal const long ProjectionEmbedDecodedPixelBudget = 16_000_000;

    /// <summary>Budget override for the pixel-pool tests. Null in
    /// production.</summary>
    internal static long? ProjectionEmbedDecodedPixelBudgetOverrideForTests;

    /// <summary>Fires with the embed key whenever the image decoder
    /// is entered — the pin that duplicate occurrences share one
    /// decode and over-budget images never allocate.</summary>
    internal static Action<string>? EmbedImageDecodeProbeForTests;

    /// <summary>Fires with the SVG byte length whenever the Svg.Skia
    /// decode path is entered — the pin that degraded or over-budget
    /// artifacts never reach it.</summary>
    internal static Action<int>? DiagramRenderProbeForTests;

    private static Paragraph MonospaceParagraph(string text)
    {
        var run = new Run(text);
        var paragraph = new Paragraph(run)
        {
            FontFamily = new FontFamily("Cascadia Mono, Consolas, monospace"),
            Padding = new Thickness(8),
        };
        paragraph.SetResourceReference(Block.BackgroundProperty, "Slate.RaisedSurfaceBrush");
        return paragraph;
    }

    private static Block TableBlock(ReadingBlock block)
    {
        // Plain accessible table until W4-1's grid substrate (§10.7).
        ReadingTableCells? cells = SlateUniffiMethods.ReadingTableCells(block.Source);
        var table = new WpfTable();
        ReadingSemantics.MarkTable(table);
        var group = new TableRowGroup();
        table.RowGroups.Add(group);

        if (cells is null || (cells.Header.Length == 0 && cells.Rows.Length == 0))
        {
            group.Rows.Add(Row(new[] { block.Source }, header: false));
            return table;
        }

        int columns = Math.Max(
            cells.Header.Length,
            cells.Rows.Length == 0 ? 1 : cells.Rows.Max(r => r.Length));
        for (int i = 0; i < columns; i++)
        {
            table.Columns.Add(new TableColumn());
        }
        if (cells.Header.Length > 0)
        {
            group.Rows.Add(Row(cells.Header, header: true));
        }
        foreach (string[] row in cells.Rows)
        {
            group.Rows.Add(Row(row, header: false));
        }
        return table;
    }

    private static TableRow Row(IEnumerable<string> cells, bool header)
    {
        var row = new TableRow();
        foreach (string cell in cells)
        {
            var paragraph = new Paragraph(new Run(cell));
            if (header)
            {
                paragraph.FontWeight = FontWeights.SemiBold;
            }
            row.Cells.Add(new TableCell(paragraph) { Padding = new Thickness(6, 2, 6, 2) });
        }
        return row;
    }

    /// <summary>
    /// The embed card (W3-5, #598/#511 parity): a <see cref="Section"/>
    /// whose content lives IN the document text range — the W3-1 spike
    /// measured <c>BlockUIContainer</c> content as silently absent from
    /// say-all, and an embed a reader cannot hear is a correctness
    /// failure. The header carries the mac EmbedView name shape, the
    /// "Jump to source" button keeps the W3-1 activation contract
    /// (string Tag through the surface's click router), and the body
    /// renders the CORE-resolved content: text with nested embeds
    /// spliced as header-only child cards (mac renders nested cards
    /// collapsed by default; Windows renders exactly that initial
    /// state, with activation opening the source — recorded
    /// divergence: no in-place nested expansion), images through the
    /// hardened decode path, and unresolved shapes with mac's exact
    /// strings. A null artifact (per-key degraded fetch) is a
    /// header-only card that still activates — never a dead block.
    /// </summary>
    private static Block EmbedCard(
        string key, ReadingEmbedArtifact? artifact, ReadingListBuildContext context)
    {
        var section = new Section
        {
            Padding = new Thickness(10, 6, 10, 6),
        };
        section.SetResourceReference(Block.BackgroundProperty, "Slate.RaisedSurfaceBrush");

        EmbedResolution? resolution = artifact?.Resolution?.Resolution;
        // A null resolution never claims a kind it cannot know (round
        // 1 [medium]): the neutral label says only what is true.
        string headerName = resolution is null
            ? $"Embed: {key}"
            : EmbedHeaderName(resolution, artifact?.Alt);
        string headerSuffix = resolution is null
            ? string.Empty
            : EmbedHeaderAccessibilitySuffix(resolution);
        (string Path, string? AnchorKind, string? AnchorText)? jump =
            ResolvedJump(resolution);

        var header = new Paragraph
        {
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0),
        };
        header.Inlines.Add(new Run(headerName));
        header.Inlines.Add(new Run("  "));
        header.Inlines.Add(new InlineUIContainer(
            JumpToSourceButton(key, headerSuffix, jump)));
        ReadingSemantics.MarkEmbedHeader(header, key);
        if (jump is { } headerJump)
        {
            ReadingSemantics.MarkEmbedJump(
                header, headerJump.Path, headerJump.AnchorKind, headerJump.AnchorText);
        }
        section.Blocks.Add(header);

        switch (resolution)
        {
            case EmbedResolution.FullNote fullNote:
                AppendEmbedText(section, fullNote.Text, fullNote.Nested);
                break;
            case EmbedResolution.Section sectionResolution:
                AppendEmbedText(
                    section, sectionResolution.Text, sectionResolution.Nested);
                break;
            case EmbedResolution.Block block:
                AppendEmbedText(section, block.Text, Array.Empty<NestedEmbed>());
                break;
            case EmbedResolution.Image image when artifact is { ImageBudgetRefused: true }:
                // The artifact resolved — the header keeps its true
                // image identity and destination; only the PAYLOAD was
                // refused by the note-wide budget, and the notice says
                // exactly that (never the decode-failure lie).
                section.Blocks.Add(EmbedBodyParagraph(
                    "Image not displayed: over this note's embedded-image "
                    + "budget. Open the source note to view it."));
                break;
            case EmbedResolution.Image image:
                AppendEmbedImage(section, image, key, context);
                break;
            case EmbedResolution.Unresolved:
                // The header name IS the mac unresolved string; the
                // AX suffix rides the button's HelpText. No body.
                break;
            case null:
                section.Blocks.Add(EmbedBodyParagraph(
                    "Embed preview unavailable. Activate to open the source."));
                break;
        }
        if (artifact?.Resolution is { Truncated: true })
        {
            section.Blocks.Add(EmbedBodyParagraph(
                "Preview truncated. Open the source note for the full content."));
        }

        ReadingSemantics.MarkEmbed(section, headerName);
        return section;
    }

    /// <summary>The mac EmbedView name shapes, verbatim.</summary>
    private static string EmbedHeaderName(EmbedResolution resolution, string? alt) =>
        resolution switch
        {
            EmbedResolution.FullNote fullNote =>
                $"Embedded note: {fullNote.TargetPath}",
            EmbedResolution.Section section =>
                $"Embedded section: {section.Heading} from {section.TargetPath}",
            EmbedResolution.Block block =>
                $"Embedded block from {block.TargetPath}",
            EmbedResolution.Image image =>
                $"Embedded image: {ImageDescriptor(image, alt)}",
            EmbedResolution.Unresolved unresolved =>
                UnresolvedEmbedText(unresolved.Reason),
            _ => "Embedded note",
        };

    /// <summary>Alt-or-filename (mac audits #196/#198/#419): trimmed
    /// authored alt when present, else the target's filename.</summary>
    private static string ImageDescriptor(EmbedResolution.Image image, string? alt)
    {
        string? trimmed = (alt ?? image.Alt)?.Trim();
        if (!string.IsNullOrEmpty(trimmed))
        {
            return trimmed;
        }
        int slash = image.TargetPath.LastIndexOf('/');
        return slash >= 0 ? image.TargetPath[(slash + 1)..] : image.TargetPath;
    }

    /// <summary>The mac visible unresolved strings, verbatim.</summary>
    private static string UnresolvedEmbedText(EmbedUnresolvedReason reason) =>
        reason switch
        {
            EmbedUnresolvedReason.TargetNotFound notFound =>
                $"Unresolved embed: {notFound.Target}",
            EmbedUnresolvedReason.HeadingNotFound heading =>
                $"Unresolved embed: {heading.TargetPath}#{heading.Heading}",
            EmbedUnresolvedReason.BlockNotFound block =>
                $"Unresolved embed: {block.TargetPath}^{block.BlockId}",
            EmbedUnresolvedReason.DepthLimitReached =>
                "Unresolved embed: depth limit reached.",
            EmbedUnresolvedReason.ReadError readError =>
                $"Unresolved embed: read error — {readError.Message}",
            _ => "Unresolved embed",
        };

    /// <summary>The mac AX-only explanatory suffixes, delivered as the
    /// Jump button's HelpText (on request, never in the reading
    /// flow).</summary>
    private static string EmbedHeaderAccessibilitySuffix(EmbedResolution resolution) =>
        resolution switch
        {
            EmbedResolution.Unresolved { Reason: EmbedUnresolvedReason.TargetNotFound } =>
                "The target note or attachment doesn't exist in this vault.",
            EmbedResolution.Unresolved { Reason: EmbedUnresolvedReason.HeadingNotFound } =>
                "The heading was not found in the target note.",
            EmbedResolution.Unresolved { Reason: EmbedUnresolvedReason.BlockNotFound } =>
                "The block anchor was not found in the target note.",
            EmbedResolution.Unresolved { Reason: EmbedUnresolvedReason.DepthLimitReached } =>
                "Further nested embeds inside this one are not rendered.",
            EmbedResolution.Unresolved { Reason: EmbedUnresolvedReason.ReadError } =>
                "Reading the target failed.",
            _ => string.Empty,
        };

    /// <summary>The destination core already resolved, when it did —
    /// the mac <c>openEmbedTarget(path)</c> route, and the only
    /// correct one for nested targets absent from the host's record
    /// snapshot (round 1 [high]).</summary>
    private static (string Path, string? AnchorKind, string? AnchorText)? ResolvedJump(
        EmbedResolution? resolution) =>
        resolution switch
        {
            EmbedResolution.FullNote fullNote => (fullNote.TargetPath, null, null),
            EmbedResolution.Section section =>
                (section.TargetPath, "heading", section.Heading),
            EmbedResolution.Block block => (block.TargetPath, "block", block.BlockId),
            EmbedResolution.Image image => (image.TargetPath, null, null),
            // Missing-ANCHOR reasons still name an existing file
            // (round 2 [high]): Jump opens it — top of file, no
            // anchor — instead of dead-ending a nested card on the
            // host's record snapshot. Record matching remains only
            // where core resolved NO path at all.
            EmbedResolution.Unresolved
            {
                Reason: EmbedUnresolvedReason.HeadingNotFound heading,
            } => (heading.TargetPath, null, null),
            EmbedResolution.Unresolved
            {
                Reason: EmbedUnresolvedReason.BlockNotFound block,
            } => (block.TargetPath, null, null),
            _ => null,
        };

    private static Button JumpToSourceButton(
        string key,
        string helpText,
        (string Path, string? AnchorKind, string? AnchorText)? jump)
    {
        var button = new Button
        {
            Content = "Jump to source",
            Padding = new Thickness(6, 0, 6, 0),
            Tag = key,
        };
        AutomationProperties.SetName(button, $"Jump to source: {key}");
        AutomationProperties.SetAutomationId(button, "ReadingBlockEmbed");
        if (helpText.Length > 0)
        {
            AutomationProperties.SetHelpText(button, helpText);
        }
        if (jump is { } resolved)
        {
            ReadingSemantics.MarkEmbedJump(
                button, resolved.Path, resolved.AnchorKind, resolved.AnchorText);
        }
        return button;
    }

    /// <summary>
    /// Body text with nested embeds spliced at their core-supplied
    /// byte offsets (the host never reconstructs embed grammar):
    /// plain-text paragraphs split on blank lines, and each nested
    /// embed as an indented header-only child card.
    /// </summary>
    private static void AppendEmbedText(
        Section section, string text, IReadOnlyList<NestedEmbed> nested)
    {
        byte[] utf8 = System.Text.Encoding.UTF8.GetBytes(text);
        int cursor = 0;
        foreach (NestedEmbed child in nested.OrderBy(child => child.ByteOffsetInParent))
        {
            int start = (int)Math.Min(child.ByteOffsetInParent, (uint)utf8.Length);
            int end = (int)Math.Min(child.ByteEndInParent, (uint)utf8.Length);
            if (start > cursor)
            {
                AppendEmbedPlainText(
                    section,
                    System.Text.Encoding.UTF8.GetString(utf8, cursor, start - cursor));
            }
            section.Blocks.Add(NestedEmbedHeader(child));
            cursor = Math.Max(cursor, end);
        }
        if (cursor < utf8.Length)
        {
            AppendEmbedPlainText(
                section,
                System.Text.Encoding.UTF8.GetString(utf8, cursor, utf8.Length - cursor));
        }
    }

    private static void AppendEmbedPlainText(Section section, string text)
    {
        foreach (string group in text.Replace("\r\n", "\n").Split(
            "\n\n", StringSplitOptions.RemoveEmptyEntries))
        {
            string trimmed = group.Trim('\n');
            if (trimmed.Trim().Length == 0)
            {
                continue;
            }
            section.Blocks.Add(EmbedBodyParagraph(trimmed));
        }
    }

    private static Paragraph EmbedBodyParagraph(string text)
    {
        var paragraph = new Paragraph { Margin = new Thickness(0, 2, 0, 2) };
        string[] lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (i > 0)
            {
                paragraph.Inlines.Add(new LineBreak());
            }
            paragraph.Inlines.Add(new Run(lines[i]));
        }
        return paragraph;
    }

    /// <summary>A nested embed rendered in mac's initial (collapsed)
    /// state: the header line with its own Jump button — activation
    /// opens the nested source. Deeper resolutions (including the
    /// core depth-limit marker) surface through the header name.</summary>
    private static Paragraph NestedEmbedHeader(NestedEmbed child)
    {
        string name = EmbedHeaderName(child.Resolution, alt: null);
        (string Path, string? AnchorKind, string? AnchorText)? jump =
            ResolvedJump(child.Resolution);
        var paragraph = new Paragraph
        {
            Margin = new Thickness(24, 2, 0, 2),
            FontWeight = FontWeights.SemiBold,
        };
        paragraph.Inlines.Add(new Run(name));
        paragraph.Inlines.Add(new Run("  "));
        paragraph.Inlines.Add(new InlineUIContainer(JumpToSourceButton(
            child.RawTarget,
            EmbedHeaderAccessibilitySuffix(child.Resolution),
            jump)));
        ReadingSemantics.MarkEmbedHeader(paragraph, child.RawTarget);
        if (jump is { } resolvedJump)
        {
            ReadingSemantics.MarkEmbedJump(
                paragraph, resolvedJump.Path, resolvedJump.AnchorKind,
                resolvedJump.AnchorText);
        }
        return paragraph;
    }

    /// <summary>Image embed body: the hardened decode path (8 MiB /
    /// 1120 px caps; the note-wide byte pool was charged at fetch),
    /// peer-suppressed like the diagram image — the header name IS
    /// the announcement (mac hides the image from AT). Decode failure
    /// shows mac's exact string. DECODED surfaces are bounded (round
    /// 5): one frozen ImageSource per key shared by every occurrence,
    /// drawing pixels from the projection pool — encoded-byte
    /// accounting alone let one small compressed image repeated
    /// across the block ceiling allocate gigabytes of surfaces.</summary>
    private static void AppendEmbedImage(
        Section section,
        EmbedResolution.Image image,
        string key,
        ReadingListBuildContext context)
    {
        if (!context.DecodedEmbedImages.TryGetValue(key, out var outcome))
        {
            if (context.RemainingEmbedDecodedPixels <= 0)
            {
                outcome = (null, true);
            }
            else
            {
                EmbedImageDecodeProbeForTests?.Invoke(key);
                System.Windows.Media.ImageSource? decoded =
                    EditorInteractionCoordinator.DecodeImage(image.Bytes, image.Mime);
                if (decoded is null)
                {
                    outcome = (null, false);
                }
                else
                {
                    long pixels =
                        decoded is System.Windows.Media.Imaging.BitmapSource bitmap
                            ? (long)bitmap.PixelWidth * bitmap.PixelHeight
                            // Non-bitmap sources cannot report
                            // dimensions; charge the decode cap
                            // conservatively.
                            : 1120L * 1120L;
                    if (pixels > context.RemainingEmbedDecodedPixels)
                    {
                        // Exhaustion discovered (round 6): DRAIN the
                        // pool — every later image refuses pre-decode
                        // — and memoize the refusal, so this is the
                        // projection's LAST decode. The discovering
                        // surface is discarded unreferenced, bounded
                        // by the per-image dimension cap.
                        context.RemainingEmbedDecodedPixels = 0;
                        outcome = (null, true);
                    }
                    else
                    {
                        context.RemainingEmbedDecodedPixels -= pixels;
                        outcome = (decoded, false);
                    }
                }
            }
            context.DecodedEmbedImages[key] = outcome;
        }
        if (outcome.Source is null)
        {
            section.Blocks.Add(EmbedBodyParagraph(
                outcome.BudgetRefused
                    ? "Image not displayed: over this note's embedded-image "
                        + "budget. Open the source note to view it."
                    : $"Could not decode image. MIME: {image.Mime}. "
                        + "The file may be corrupt or an unsupported codec."));
            return;
        }
        var visual = new ReadingDiagramImage
        {
            Source = outcome.Source,
            Stretch = System.Windows.Media.Stretch.Uniform,
            StretchDirection = StretchDirection.DownOnly,
            MaxHeight = 600,
        };
        section.Blocks.Add(new BlockUIContainer(visual));
    }

    private static Paragraph InlineParagraph(
        ReadingBlockInlines inlines,
        (ulong Start, ulong End)? taskRange)
    {
        var paragraph = new Paragraph();
        ReadingInlineSegment? segment = inlines.Segments.FirstOrDefault();
        if (segment is null)
        {
            return paragraph;
        }

        if (taskRange is { } range)
        {
            var box = new CheckBox
            {
                IsChecked = segment.TaskCompleted ?? false,
                // The stamped source range is how activation finds the
                // core TaskItem (checkbox byte within the block — the
                // same positional rule mac's taskRow applies by line).
                // The checkbox itself never holds state: the surface
                // reverts WPF's optimistic flip and routes the toggle
                // through the SAME core task command the editor and
                // Tasks panel use; the re-projection renders the
                // outcome. STRING-encoded because WPF text machinery
                // may XamlWriter-serialize document content, and
                // generic payloads (ValueTuple) make it throw — the
                // 2026-07-26 field root cause.
                Tag = ReadingSemantics.EncodeTaskRange(range.Start, range.End),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0),
            };
            AutomationProperties.SetName(box, ReadingRunText(segment, 0, segment.Content.Length));
            paragraph.Inlines.Add(new InlineUIContainer(box));
        }

        byte[] utf8 = Encoding.UTF8.GetBytes(segment.Content);
        foreach (ReadingInlineRun run in segment.Runs)
        {
            int start = (int)run.Start;
            int length = (int)(run.End - run.Start);
            if (start < 0 || length <= 0 || start + length > utf8.Length)
            {
                continue;
            }
            paragraph.Inlines.Add(
                BuildInline(Encoding.UTF8.GetString(utf8, start, length), run));
        }
        return paragraph;
    }

    private static string ReadingRunText(ReadingInlineSegment segment, int start, int end)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(segment.Content);
        int clampedEnd = Math.Min(end, utf8.Length);
        return start >= clampedEnd
            ? string.Empty
            : Encoding.UTF8.GetString(utf8, start, clampedEnd - start);
    }

    private static Inline BuildInline(string text, ReadingInlineRun run)
    {
        var body = new Run(text);
        ApplyStyles(body, run.Styles);

        Inline inline = run.Kind is ReadingInlineRunKind.Text
            ? body
            : Activatable(body, run.Kind);

        // §10.3: per-run accessible text is core's string, stamped via
        // the host AX mechanism — never composed here.
        if (run.AxText is { Length: > 0 } axText)
        {
            AutomationProperties.SetHelpText(inline, axText);
        }
        return inline;
    }

    private static void ApplyStyles(Run body, ReadingInlineStyle[] styles)
    {
        foreach (ReadingInlineStyle style in styles)
        {
            switch (style)
            {
                case ReadingInlineStyle.Emphasis:
                    body.FontStyle = FontStyles.Italic;
                    break;
                case ReadingInlineStyle.Strong:
                    body.FontWeight = FontWeights.Bold;
                    break;
                case ReadingInlineStyle.Strikethrough:
                    body.TextDecorations = TextDecorations.Strikethrough;
                    break;
                case ReadingInlineStyle.InlineCode:
                    body.FontFamily = new FontFamily("Cascadia Mono, Consolas, monospace");
                    body.SetResourceReference(TextElement.ForegroundProperty, "Slate.EditorCodeBrush");
                    break;
            }
        }
    }

    /// <summary>
    /// Every activatable run: accent (or warning) token PLUS underline —
    /// the affordance is never colour-only (WCAG 1.4.1). Unresolved
    /// wikilinks take <c>Slate.WarningBrush</c> — the #849 distinction,
    /// a state, not an error.
    /// </summary>
    private static Inline Activatable(Run body, ReadingInlineRunKind kind)
    {
        var link = new Hyperlink(body)
        {
            TextDecorations = TextDecorations.Underline,
            Tag = kind,
        };
        link.SetResourceReference(
            TextElement.ForegroundProperty,
            kind is ReadingInlineRunKind.Wikilink { Resolved: false }
                ? "Slate.WarningBrush"
                : "Slate.AccentBrush");

        // A destination is REQUIRED: without NavigateUri NVDA announces
        // "Link has no apparent destination" on every link (measured).
        Uri? destination = ReadingRouting.RoutingUri(kind);
        if (destination is not null)
        {
            link.NavigateUri = destination;
        }
        return link;
    }

    private static AutomationHeadingLevel ToHeadingLevel(byte level) => level switch
    {
        1 => AutomationHeadingLevel.Level1,
        2 => AutomationHeadingLevel.Level2,
        3 => AutomationHeadingLevel.Level3,
        4 => AutomationHeadingLevel.Level4,
        5 => AutomationHeadingLevel.Level5,
        _ => AutomationHeadingLevel.Level6,
    };

    /// <summary>
    /// Landmarks are collected AFTER assembly, by walking the finished
    /// document: a <see cref="TextPointer"/> taken before an element
    /// joins the document belongs to the wrong text container, and a
    /// walk guarantees document order without bookkeeping during build.
    /// </summary>
    /// <summary>
    /// Landmarks store INSERTION positions, matching how RichTextBox
    /// normalizes its caret. Raw ContentStart compares strictly before a
    /// caret sitting on that very line, so backward navigation would
    /// "find" the line the user is already on instead of announcing the
    /// miss — measured as a wrap-like phantom hit in the never-wraps
    /// test.
    /// </summary>
    private static TextPointer Insertion(TextPointer position) =>
        position.GetInsertionPosition(LogicalDirection.Forward) ?? position;

    internal static IReadOnlyList<ReadingLandmark> CollectLandmarks(FlowDocument document)
    {
        var landmarks = new List<ReadingLandmark>();
        foreach (Block block in document.Blocks)
        {
            WalkBlock(block, landmarks);
        }
        return landmarks;
    }

    private static void WalkBlock(Block block, List<ReadingLandmark> landmarks)
    {
        switch (block)
        {
            case Paragraph paragraph:
                if (ReadingSemantics.HeadingLevelOf(paragraph) is byte level and > 0)
                {
                    landmarks.Add(new ReadingLandmark(
                        ReadingLandmarkKind.Heading,
                        Insertion(paragraph.ContentStart),
                        level,
                        ElementText(paragraph)));
                }
                else if (ReadingSemantics.IsCodeBlock(paragraph))
                {
                    landmarks.Add(new ReadingLandmark(
                        ReadingLandmarkKind.CodeBlock,
                        Insertion(paragraph.ContentStart),
                        text: ElementText(paragraph)));
                }
                else if (ReadingSemantics.IsMathBlock(paragraph))
                {
                    landmarks.Add(new ReadingLandmark(
                        ReadingLandmarkKind.Math,
                        Insertion(paragraph.ContentStart),
                        text: ReadingSemantics.MathSpeechOf(paragraph)));
                }
                else if (ReadingSemantics.IsDiagramBlock(paragraph))
                {
                    landmarks.Add(new ReadingLandmark(
                        ReadingLandmarkKind.Diagram,
                        Insertion(paragraph.ContentStart),
                        text: ReadingSemantics.DiagramDescriptionOf(paragraph)));
                }
                WalkInlines(paragraph.Inlines, landmarks);
                break;

            case WpfList list:
                landmarks.Add(new ReadingLandmark(
                    ReadingLandmarkKind.List,
                    Insertion(list.ContentStart),
                    text: FirstItemText(list)));
                foreach (ListItem item in list.ListItems)
                {
                    foreach (Block inner in item.Blocks)
                    {
                        WalkBlock(inner, landmarks);
                    }
                }
                break;

            case WpfTable table:
                landmarks.Add(new ReadingLandmark(
                    ReadingLandmarkKind.Table,
                    Insertion(table.ContentStart),
                    text: FirstCellText(table)));
                break;

            case BlockUIContainer container when ReadingSemantics.IsEmbed(container):
                landmarks.Add(new ReadingLandmark(
                    ReadingLandmarkKind.Embed,
                    Insertion(container.ContentStart),
                    text: container.Child is System.Windows.UIElement child
                        ? AutomationProperties.GetName(child) ?? string.Empty
                        : string.Empty));
                break;

            case Section section when ReadingSemantics.IsEmbedSection(section):
                // ONE landmark per card, carrying the mac-shaped
                // header name; the body is plain in-range text and is
                // deliberately not walked (nested headers are not
                // separate chord stops — one card, one stop).
                landmarks.Add(new ReadingLandmark(
                    ReadingLandmarkKind.Embed,
                    Insertion(section.ContentStart),
                    text: ReadingSemantics.EmbedNameOf(section)));
                break;

            case Section section:
                foreach (Block inner in section.Blocks)
                {
                    WalkBlock(inner, landmarks);
                }
                break;
        }
    }

    private static void WalkInlines(InlineCollection inlines, List<ReadingLandmark> landmarks)
    {
        foreach (Inline inline in inlines)
        {
            switch (inline)
            {
                case Hyperlink link:
                    landmarks.Add(new ReadingLandmark(
                        ReadingLandmarkKind.Link,
                        Insertion(link.ContentStart),
                        text: ElementText(link),
                        element: link));
                    break;
                case Span span:
                    WalkInlines(span.Inlines, landmarks);
                    break;
            }
        }
    }

    /// <summary>Announcement payloads are bounded: a landing must never
    /// read a whole pasted page.</summary>
    private const int LandmarkTextLimit = 120;

    private static string ElementText(TextElement element)
    {
        string text = new TextRange(element.ContentStart, element.ContentEnd).Text;
        int newline = text.IndexOfAny(new[] { '\r', '\n' });
        if (newline >= 0)
        {
            text = text[..newline];
        }
        text = text.Trim();
        return text.Length <= LandmarkTextLimit ? text : text[..LandmarkTextLimit];
    }

    /// <summary>
    /// The first ITEM PARAGRAPH's text, not the item's: a rendered list
    /// item's own range includes the marker glyph, and the landing
    /// announcement said "•	first bullet, list." (caught by the
    /// live-document test).
    /// </summary>
    private static string FirstItemText(WpfList list)
    {
        if (list.ListItems.FirstListItem?.Blocks.OfType<Paragraph>().FirstOrDefault()
            is not { } paragraph)
        {
            return string.Empty;
        }
        string text = ElementText(paragraph);
        // The rendered marker ("•	", "12.	") is materialized INSIDE
        // the paragraph's text range in a hosted document — the spike's
        // "list arrives as document text" finding, now in announcement
        // form: the landing said "•	first bullet, list.". The marker is
        // presentation, not content; a short prefix ending in a tab is
        // exactly it.
        int tab = text.IndexOf('	');
        return tab >= 0 && tab <= 4 ? text[(tab + 1)..].TrimStart() : text;
    }

    private static string FirstCellText(WpfTable table)
    {
        TableRowGroup? group = table.RowGroups.FirstOrDefault();
        TableRow? row = group?.Rows.FirstOrDefault();
        TableCell? cell = row?.Cells.FirstOrDefault();
        return cell is null ? string.Empty : ElementText(cell);
    }
}
