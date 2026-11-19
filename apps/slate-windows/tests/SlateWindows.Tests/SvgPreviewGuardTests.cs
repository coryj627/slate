// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// D4: <see cref="SvgPreviewGuard"/> bounds what an SVG preview hands
/// Svg.Skia. The guard reads the document with an XmlReader and never
/// parses it with Svg.Custom, so these verdicts are safe to take in
/// process; <see cref="SvgDecodeOutOfProcessTests"/> runs the whole
/// decode in a child process.
/// </summary>
public sealed class SvgPreviewGuardTests
{
    private const string Ns =
        "xmlns='http://www.w3.org/2000/svg' xmlns:xlink='http://www.w3.org/1999/xlink'";
    private const string Square = "<rect width='16' height='16' fill='#00f'/>";

    internal static string Svg(string body) => $"<svg {Ns} width='16' height='16'>{body}</svg>";

    private static string Check(string source) =>
        SvgPreviewGuard.Check(source, 8 * 1024 * 1024).ToString();

    private static string Repeat(string text, int count) =>
        new StringBuilder(text.Length * count).Insert(0, text, count).ToString();

    private static string Chain(int count, Func<int, string> link)
    {
        var builder = new StringBuilder();
        for (int i = 1; i <= count; i++)
        {
            builder.Append(link(i));
        }
        return builder.ToString();
    }

    /// <summary>The shapes real documents use stay inside every cap:
    /// Illustrator's class rules, Mermaid's markers, an icon sheet's
    /// reuse, a filter's chained inputs, a stylesheet's gradient fills
    /// and each cap itself. #1610 (CB-29): each, and the repository's own
    /// SVG, is estimated at under a tenth of the decode limit.</summary>
    [Fact]
    public void AcceptsTheShapesRealDocumentsUse()
    {
        int depthToCap = SvgPreviewGuard.MaxRenderDepth - 2;
        string[] accepted =
        [
            Svg(Square),
            Svg("<style>.st0{fill:url(#g)}.st1{clip-path:url(#c)}</style>"
                + "<defs><linearGradient id='g'><stop offset='0' stop-color='red'/></linearGradient>"
                + "<clipPath id='c'><path d='M0 0h16v16z'/></clipPath></defs>"
                + "<g class='st1'><rect class='st0' width='16' height='16'/></g>"),
            Svg("<defs><marker id='arrow-0' markerWidth='8' markerHeight='8'><path d='M0 0L8 4L0 8z'/></marker></defs>"
                + "<path d='M1 1L15 15' stroke='#000' marker-end='url(#arrow-0)'/>"),
            Svg("<defs><symbol id='icon'><path d='M0 0h4v4z'/><circle r='2'/></symbol></defs>"
                + Repeat("<use href='#icon'/>", 300)),
            Svg("<defs><filter id='f'><feGaussianBlur in='SourceAlpha' stdDeviation='1' result='blur'/>"
                + Repeat("<feOffset dx='0' dy='0'/>", 10)
                + "<feComposite in='SourceGraphic' in2='blur' operator='over'/></filter></defs>"
                + "<rect width='16' height='16' filter='url(#f)'/>"),
            Svg("<style>svg .bars rect{fill:url(#g)}</style>"
                + "<defs><linearGradient id='g'><stop offset='1' stop-color='blue'/></linearGradient></defs>"
                + "<g class='bars'>" + Square + "</g>"),
            Svg(Repeat("<g>", depthToCap) + Square + Repeat("</g>", depthToCap)),
            Svg($"<rect width='16' height='16' fill='{Repeat("url(#a)", SvgPreviewGuard.MaxUrlReferencesPerValue)}'/>"),
            // The rule's own brace is one level of the stylesheet's nesting.
            Svg("<style>rect{stroke-width:" + Repeat("calc(", SvgPreviewGuard.MaxValueNesting - 1)
                + "1px" + Repeat(")", SvgPreviewGuard.MaxValueNesting - 1) + "}</style>" + Square),
            Svg("<g id='s'><a href='#s'>" + Square + "</a></g>"),
            Svg("<defs><linearGradient id='g0'><stop offset='0' stop-color='red'/></linearGradient>"
                + Chain(1000, i => $"<linearGradient id='g{i}' href='#g{i - 1}'/>")
                + "</defs><rect width='16' height='16' fill='url(#g1000)'/>"),
            // CSS strings and comments are not structure: braces in a string
            // and references in a comment count for nothing.
            Svg("<style>text::before{content:\"" + new string('{', SvgPreviewGuard.MaxValueNesting + 1)
                + "\"}</style>" + Square),
            Svg("<style>/* " + Repeat("url(#a)", SvgPreviewGuard.MaxUrlReferencesPerValue + 1)
                + " */rect{fill:red}</style>" + Square),
        ];

        string pie = File.ReadAllText(
            Path.Combine(SourceText.RepoRoot(), "demo-vault", "attachments", "pie.svg"));
        foreach (string source in accepted.Append(pie))
        {
            Assert.True(
                Check(source) == nameof(SvgRefusal.None),
                $"refused ({Check(source)}): {source[..Math.Min(source.Length, 200)]}");
            double estimate = EstimateOf(source).Milliseconds;
            Assert.True(
                estimate < SvgPreviewGuard.MaxEstimatedDecodeMilliseconds / 10.0,
                $"estimated at {estimate:F1} ms: {source[..Math.Min(source.Length, 200)]}");
        }
    }

    private static SvgDecodeEstimate EstimateOf(string source) =>
        SvgPreviewGuard.Estimate(source, 8 * 1024 * 1024);

    public static TheoryData<string> SlowDocumentCases() => [.. SlowDocuments.Keys];

    /// <summary>
    /// #1610 (CB-29): a document that would hold a decoder for many seconds
    /// is refused for its cost before anything decodes it. Each row is sized
    /// to estimate past 10 seconds, so it is refused at any limit the owner
    /// picks from the calibration table: #1581's four reproductions, a
    /// plotting library's scatter, a one-rule stylesheet over many uses,
    /// markers inherited from a group, each selector shape whose query is not
    /// linear (descendant chains over nested groups, general siblings,
    /// of-type pseudo-classes), a <c>var()</c> fallback that re-resolves past
    /// the depth limit, declarations on elements that draw nothing, and a
    /// marker set through <c>var()</c>. The two rows that expand
    /// exponentially (four <c>var()</c> references per level at 15 levels,
    /// about 1.07e9 lookups; six descendant steps over 254 nested groups,
    /// hours by arithmetic) cost the guard itself under 100 ms on this
    /// fact's own stopwatch: the median of <see cref="WallClockBudget"/>'s
    /// warm readings, so a loaded runner does not reach it.
    /// </summary>
    [Theory]
    [MemberData(nameof(SlowDocumentCases))]
    public void RefusesEachSlowDocumentForItsCost(string name)
    {
        (Func<string> build, bool timed) = SlowDocuments[name];
        string source = build();
        Assert.Equal(nameof(SvgRefusal.DecodeCost), Check(source));
        double estimate = EstimateOf(source).Milliseconds;
        Assert.True(estimate > 10_000, $"{name}: estimated at {estimate:F0} ms");
        if (timed)
        {
            TimeSpan guardTime = WallClockBudget.WarmMedian(() =>
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                SvgRefusal verdict = SvgPreviewGuard.Check(source, 8 * 1024 * 1024);
                stopwatch.Stop();
                Assert.Equal(SvgRefusal.DecodeCost, verdict);
                return stopwatch.Elapsed;
            });
            Assert.True(
                guardTime < TimeSpan.FromMilliseconds(100),
                $"{name}: the guard took {guardTime.TotalMilliseconds:F1} ms");
        }
    }

    internal static readonly Dictionary<string, (Func<string> Build, bool Timed)> SlowDocuments = new()
    {
        // #1581's four rows, reproduced by step 0 (40.5 s, 135.6 s, 34.8 s, 50.0 s).
        ["#1581 A: 3,000 universal rules over 13,000 groups"] = (() => SlowSvgDocuments.Generate("A_star_g R=3000 E=13000"), false),
        ["#1581 B: 15,000 universal rules over 8,000 rects"] = (() => SlowSvgDocuments.Generate("A_star_rect R=15000 E=8000"), false),
        ["#1581 C: var() doubling at 15 levels over 5,000 rects"] = (() => SlowSvgDocuments.Generate("C_double D=15 E=5000"), false),
        ["#1581 D: a 2,000-rect marker on a 10,000-vertex polyline"] = (() => SlowSvgDocuments.Generate("D_marker M=2000 V=10000"), false),
        // 17.4 s; one global rule and a use per point.
        ["a plotting scatter of 10,000 points"] = (() => SlowSvgDocuments.Generate("G_scatter N=10000"), false),
        // 30.3 s; the same uses without the rule decode in 2.0 s.
        ["one rule over 10,000 uses"] = (() => SlowSvgDocuments.Generate("A_use R=1 G=4 U=10000"), false),
        // Estimated at 1.8 ms a rect: 7,000 passes 10 s (1,000 decode in 2.6 s).
        ["a 100-rect marker inherited from a group by 7,000 rects"] = (() => SlowSvgDocuments.Generate("D_rects_inherit M=100 N=7000"), false),
        // The follow-up measured each: 33.0 s, past 60 s, 20.6 s.
        ["four descendant steps over 128 nested groups"] = (() => SlowSvgDocuments.Generate("N_desc K=4 R=1 D=128"), false),
        ["two general-sibling steps over 3,000 rects"] = (() => SlowSvgDocuments.Generate("N_sib K=3 R=1 N=3000"), false),
        ["ten first-of-type rules over 10,000 rects"] = (() => SlowSvgDocuments.Generate("N_nth Q=1 R=10 N=10000"), false),
        // Each level's fallback names the level below once more, so a chain
        // past the depth limit is resolved again from every level: 0.4 to
        // 0.9 ms a rect in the follow-up.
        ["a var() fallback chain past the depth limit over 30,000 rects"] = (() => SlowSvgDocuments.Generate("C_fb F=1 D=16 E=30000"), false),
        // 3.7 s at 10,000; desc draws nothing, and still takes every declaration.
        ["1,000 custom properties on 25,000 desc elements"] = (() => SlowSvgDocuments.Generate("N_unstyled T=1 Q=2 D=1000 N=25000"), false),
        // The follow-up: marker-mid:var(--m) costs what the attribute does.
        ["a 2,000-rect marker set through var() on a 10,000-vertex polyline"] = (() => SlowSvgDocuments.Generate("D_var M=2000 V=10000"), false),
        ["four var() references per level at 15 levels"] = (() => SlowSvgDocuments.Generate("C_kary K=4 D=15 E=1"), true),
        ["six descendant steps over 254 nested groups"] = (
            () => Svg("<style>g g g g g g rect{fill:red}</style>" + Repeat("<g>", 254) + Square + Repeat("</g>", 254)),
            true),
    };

    public static TheoryData<string, double> CalibratedDocuments() => new()
    {
        { "A_class R=5000 E=6000", 2834.6 },
        { "A_list R=2500 S=4 E=10000", 9373.4 },
        { "A_type R=1000 E=3000", 3375.2 },
        { "A_use R=1 G=4 U=3000", 2569.8 },
        { "C_double D=15 E=1000", 6291.7 },
        { "D_marker M=500 V=1000", 1326.1 },
        // The follow-up's selector counts, each its count x its fitted rate
        // (f2-est-depth.csv): the depth bound x 0.05985 us (13.4 s to
        // decode), sibling-list reads x 0.01437 us (1.9 s), and rules x the
        // sibling-list size per match x 0.01993 us (1.9 s).
        { "N_desc K=3 R=10 D=128", 13820.7 },
        { "N_sib K=2 R=1 N=10000", 1437.3 },
        { "N_nth Q=1 R=10 N=3000", 1794.3 },
    };

    /// <summary>#1610 (CB-29): one document per count, each estimated within
    /// 10% of the estimate the committed calibration records for it (the
    /// <c>est2Ms</c> column of
    /// <c>docs/plans/47_windows_core_closeout/calibration/cb29/est.csv</c>,
    /// and for the selector counts the follow-up's count times its fitted
    /// rate, <c>followup2.md</c> and <c>f2-est-depth.csv</c> there), so a
    /// later change to the counting is seen against the calibration. A_list's
    /// rules have four selectors each, so a count of rules in place of
    /// branches is a quarter of it.</summary>
    [Theory]
    [MemberData(nameof(CalibratedDocuments))]
    public void EstimatesTrackTheCalibration(string label, double calibratedMilliseconds)
    {
        double estimate = EstimateOf(SlowSvgDocuments.Generate(label)).Milliseconds;
        Assert.True(
            Math.Abs(estimate - calibratedMilliseconds) <= calibratedMilliseconds * 0.10,
            $"{label}: estimated at {estimate:F1} ms; the calibration records {calibratedMilliseconds:F1} ms");
    }

    public static TheoryData<string, double> QuickDocuments() => new()
    {
        // Attribute selectors filed by attribute: about 89 ms (under * every
        // rule would land on every rect, about 1 s).
        { "A_attr R=300 E=3000", 300 },
        // Past the depth limit Svg.Custom gives up at once: 31 ms to decode.
        { "C_double D=16 E=1000", 50 },
        // The scatter at 500 points with its rule: about 76 ms.
        { "G_scatter N=500", 250 },
        // No <style>, so no use term: 2.0 s to decode, all of it drawing.
        { "A_use_nostyle G=4 U=10000", 50 },
        // Markers at a line's start and end only: about 4 ms (as marker-mid, 1.3 s).
        { "D_start M=500 V=1000", 200 },
        // A marker inside a marker is not placed (the follow-up: 157 ms with
        // the inner marker, 135 ms without): about 199 ms.
        { "D_nested M=10 VO=2000 VI=200", 500 },
        // nth-child decodes in linear time (the follow-up: 164 ms): about 112 ms.
        { "N_nth Q=2 R=10 N=10000", 500 },
        // A property repeated in one rule is applied once (the follow-up: 70 ms).
        { "N_unstyled T=1 Q=1 D=1000 N=10000", 300 },
        // A reference that resolves never reaches its fallback: two fallback
        // references per level at 15 levels over 100 rects decode in 7 ms
        // (counting every var( as a copy gives 275 s).
        { "C_fb F=2 D=15 E=100", 200 },
        // The follow-up's selector counts admit what decodes under the
        // limit: four descendant steps over 64 nested groups, 1.09 s to
        // decode, about 1.12 s; one general-sibling step over 10,000 rects,
        // 1.89 s, about 1.45 s; ten first-of-type rules over 3,000 rects,
        // 1.90 s, about 1.83 s.
        { "N_desc K=4 R=1 D=64", 1500 },
        { "N_sib K=2 R=1 N=10000", 1800 },
        { "N_nth Q=1 R=10 N=3000", 1950 },
    };

    /// <summary>#1610 (CB-29): the shapes the estimate's corrections exist
    /// for are admitted, each estimated under its own bound, which sits
    /// between the row's estimate and the decode limit.</summary>
    [Theory]
    [MemberData(nameof(QuickDocuments))]
    public void AdmitsWhatDecodesQuickly(string label, double boundMilliseconds)
    {
        string source = SlowSvgDocuments.Generate(label);
        Assert.Equal(nameof(SvgRefusal.None), Check(source));
        double estimate = EstimateOf(source).Milliseconds;
        Assert.True(estimate < boundMilliseconds, $"{label}: estimated at {estimate:F1} ms");
    }

    /// <summary>
    /// #1610 (CB-29): core's renders of the dialects whose edges carry
    /// markers, the only cost a rendered diagram has, sized to render in
    /// under a second each so core's 5-second budget never cuts them, are
    /// estimated at under a thirtieth of the decode limit. The marker term
    /// grows with the edges, so the largest diagram core renders (a
    /// sequence of 1,093 messages, 76 ms in the committed calibration)
    /// stays near a tenth of it.
    /// </summary>
    [Fact]
    public void RealDiagramsStayFarBelowTheLimit()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "svg-guard-real-diagrams");
        string[] sources = [SequenceSource(300), ClassSource(150), StateSource(150)];
        File.WriteAllText(
            Path.Combine(fixture.Root, "d.md"),
            string.Concat(sources.Select(source => "```mermaid\n" + source + "```\n\n")));
        using VaultSession session = VaultSession.OpenFilesystem(fixture.Root);
        using (var cancel = new CancelToken())
        {
            session.ScanInitial(cancel);
        }
        DiagramBlock[] blocks = session.GetDiagramBlocks("d.md");
        Assert.Equal(3, blocks.Length);
        foreach (DiagramBlock block in blocks)
        {
            Assert.True(block.RenderStatus is DiagramRenderStatus.Ok, $"not rendered: {block.RenderStatus}");
            string svg = Encoding.UTF8.GetString(block.Svg!);
            Assert.Equal(nameof(SvgRefusal.None), Check(svg));
            double estimate = EstimateOf(svg).Milliseconds;
            Assert.True(
                estimate < SvgPreviewGuard.MaxEstimatedDecodeMilliseconds / 30.0,
                $"{block.Source.Split('\n')[0]}: estimated at {estimate:F1} ms");
        }
    }

    private static string SequenceSource(int messages)
    {
        var source = new StringBuilder("sequenceDiagram\n");
        for (int participant = 0; participant < 6; participant++)
        {
            source.Append($"  participant P{participant}\n");
        }
        for (int i = 0; i < messages; i++)
        {
            source.Append($"  P{i % 6}->>P{(i + 1 + (i / 6)) % 6}: message {i}\n");
            if (i % 7 == 6)
            {
                source.Append($"  P{(i + 2) % 6}-->>P{i % 6}: reply {i}\n");
            }
        }
        return source.ToString();
    }

    private static string ClassSource(int classes)
    {
        var source = new StringBuilder("classDiagram\n");
        for (int i = 0; i < classes; i++)
        {
            source.Append($"  class C{i} {{\n    +int field{i}\n    +String name\n    +method{i}() bool\n  }}\n");
            if (i > 0)
            {
                source.Append(i % 3 == 0 ? $"  C{i / 2} <|-- C{i}\n" : $"  C{i - 1} --> C{i} : uses\n");
            }
        }
        return source.ToString();
    }

    private static string StateSource(int states)
    {
        var source = new StringBuilder("stateDiagram-v2\n  [*] --> S0\n");
        for (int i = 0; i < states; i++)
        {
            source.Append($"  S{i} --> S{i + 1} : event{i}\n");
            if (i % 4 == 3)
            {
                source.Append($"  S{i} --> S{i - 2} : back{i}\n");
            }
        }
        source.Append($"  S{states} --> [*]\n");
        return source.ToString();
    }

    public static TheoryData<string> RefusalCases() => [.. Refusals.Keys];

    /// <summary>One document per cap, each one step past it, refused for
    /// that cap's reason.</summary>
    [Theory]
    [MemberData(nameof(RefusalCases))]
    public void RefusesEachDocumentPastACap(string name)
    {
        (Func<string> build, SvgRefusal expected) = Refusals[name];
        Assert.Equal(expected.ToString(), Check(build()));
    }

    internal static readonly Dictionary<string, (Func<string> Build, SvgRefusal Expected)> Refusals = new()
    {
        ["url references in an attribute"] = (
            () => Svg($"<rect width='16' height='16' fill='{Repeat("url(#a)", SvgPreviewGuard.MaxUrlReferencesPerValue + 1)}'/>"),
            SvgRefusal.UrlReferences),
        ["url references in a style declaration"] = (
            () => Svg($"<rect width='16' height='16' style='stroke:red;fill:{Repeat("url(#a)", SvgPreviewGuard.MaxUrlReferencesPerValue + 1)}'/>"),
            SvgRefusal.UrlReferences),
        ["url references in a stylesheet declaration"] = (
            () => Svg($"<style>rect{{fill:{Repeat("url(#a)", SvgPreviewGuard.MaxUrlReferencesPerValue + 1)}}}</style>" + Square),
            SvgRefusal.UrlReferences),
        ["bracket nesting in an attribute"] = (
            () => Svg($"<rect width='16' height='16' fill='{Repeat("var(--a,", SvgPreviewGuard.MaxValueNesting + 1)}red{Repeat(")", SvgPreviewGuard.MaxValueNesting + 1)}'/>"),
            SvgRefusal.ValueNesting),
        ["bracket nesting in a stylesheet"] = (
            () => Svg("<style>rect{stroke-width:" + Repeat("calc(", SvgPreviewGuard.MaxValueNesting) + "1px"
                + Repeat(")", SvgPreviewGuard.MaxValueNesting) + "}</style>" + Square),
            SvgRefusal.ValueNesting),
        // Svg.Custom joins every <style> element into one stylesheet: each
        // element here is under the cap, the joined text is not.
        ["bracket nesting across style elements"] = (
            () => Svg("<style>rect{stroke-width:" + Repeat("calc(", SvgPreviewGuard.MaxValueNesting / 2)
                + "</style><style>" + Repeat("calc(", SvgPreviewGuard.MaxValueNesting / 2) + "1px"
                + Repeat(")", SvgPreviewGuard.MaxValueNesting) + "}</style>" + Square),
            SvgRefusal.ValueNesting),
        ["block nesting in a stylesheet"] = (
            () => Svg("<style>" + Repeat("@media all{", SvgPreviewGuard.MaxValueNesting)
                + "rect{fill:red}" + Repeat("}", SvgPreviewGuard.MaxValueNesting) + "</style>" + Square),
            SvgRefusal.ValueNesting),
        // A string's brackets are its own: a ")" inside one closes nothing.
        ["brackets hidden behind strings in a stylesheet"] = (
            () => Svg("<style>rect" + Repeat(":not([a=\")\"]", SvgPreviewGuard.MaxValueNesting)
                + Repeat(")", SvgPreviewGuard.MaxValueNesting) + "{fill:red}</style>" + Square),
            SvgRefusal.ValueNesting),
        ["brackets hidden behind strings in a style attribute"] = (
            () => Svg("<rect width='16' height='16' style='stroke-width:"
                + Repeat("calc(\"x)\"", SvgPreviewGuard.MaxValueNesting + 1) + "1px"
                + Repeat(")", SvgPreviewGuard.MaxValueNesting + 1) + "'/>"),
            SvgRefusal.ValueNesting),
        // An escaped comment marker opens no comment.
        ["an escaped comment marker"] = (
            () => Svg("<style>\\/\\*rect{stroke-width:" + Repeat("calc(", SvgPreviewGuard.MaxValueNesting)
                + "1px" + Repeat(")", SvgPreviewGuard.MaxValueNesting) + "}\\*\\/</style>" + Square),
            SvgRefusal.ValueNesting),
        // An escaped comma separates no selectors: the selector is not a
        // simple one, so the rule applies to every element, the mask's own
        // content included.
        ["an escaped comma in a selector"] = (
            () => Svg("<style>.a\\,b{mask:url(#m)}</style><defs><mask id='m'>"
                + "<rect class='a,b' width='16' height='16' fill='#fff'/></mask></defs>" + Square),
            SvgRefusal.ReferenceCycle),
        ["element nesting"] = (
            () => Svg(Repeat("<g>", SvgPreviewGuard.MaxRenderDepth - 1) + Square + Repeat("</g>", SvgPreviewGuard.MaxRenderDepth - 1)),
            SvgRefusal.RenderDepth),
        ["nested svg elements"] = (
            () => Svg(Repeat("<svg>", 300) + Square + Repeat("</svg>", 300)),
            SvgRefusal.RenderDepth),
        ["a use chain"] = (
            () => Svg($"<defs><g id='u0'>{Square}</g>" + Chain(300, i => $"<use id='u{i}' href='#u{i - 1}'/>") + "</defs><use href='#u300'/>"),
            SvgRefusal.RenderDepth),
        ["a mask chain"] = (
            () => Svg("<defs><mask id='m0'><rect width='16' height='16' fill='#fff'/></mask>"
                + Chain(150, i => $"<mask id='m{i}'><rect width='16' height='16' fill='#fff' mask='url(#m{i - 1})'/></mask>")
                + "</defs><rect width='16' height='16' mask='url(#m150)'/>"),
            SvgRefusal.RenderDepth),
        ["a clip path chain"] = (
            () => Svg("<defs><clipPath id='c0'><rect width='16' height='16'/></clipPath>"
                + Chain(300, i => $"<clipPath id='c{i}' clip-path='url(#c{i - 1})'><rect width='16' height='16'/></clipPath>")
                + "</defs><rect width='16' height='16' clip-path='url(#c300)'/>"),
            SvgRefusal.RenderDepth),
        ["a chain of named filter inputs"] = (
            () => Svg("<defs><filter id='f'><feOffset in='SourceGraphic' result='r0'/>"
                + Chain(300, i => $"<feOffset in='r{i - 1}' result='r{i}'/>")
                + "</filter></defs><rect width='16' height='16' filter='url(#f)'/>"),
            SvgRefusal.RenderDepth),
        ["a chain of implicit filter inputs"] = (
            () => Svg("<defs><filter id='f'>" + Repeat("<feOffset dx='0'/>", 300)
                + "</filter></defs><rect width='16' height='16' filter='url(#f)'/>"),
            SvgRefusal.RenderDepth),
        ["a pattern content chain"] = (
            () => Svg("<defs><pattern id='p0' width='4' height='4'>" + Square + "</pattern>"
                + Chain(300, i => $"<pattern id='p{i}' width='4' height='4'><rect width='4' height='4' fill='url(#p{i - 1})'/></pattern>")
                + "</defs><rect width='16' height='16' fill='url(#p300)'/>"),
            SvgRefusal.RenderDepth),
        ["a mask chain applied through class rules"] = (
            () => Svg("<style>" + Chain(150, i => $".k{i}{{mask:url(#m{i - 1})}}") + "</style>"
                + "<defs><mask id='m0'><rect width='16' height='16' fill='#fff'/></mask>"
                + Chain(150, i => $"<mask id='m{i}'><rect class='k{i}' width='16' height='16' fill='#fff'/></mask>")
                + "</defs><rect width='16' height='16' mask='url(#m150)'/>"),
            SvgRefusal.RenderDepth),
        ["exponential use fan-out"] = (
            () => Svg("<defs><g id='l0'>" + Square + "</g>"
                + Chain(6, i => $"<g id='l{i}'>" + Repeat($"<use href='#l{i - 1}'/>", 10) + "</g>")
                + "</defs><use href='#l6'/>"),
            SvgRefusal.RenderedElements),
        ["exponential pattern fan-out"] = (
            () => Svg("<defs><pattern id='q0' width='4' height='4'>" + Square + "</pattern>"
                + Chain(6, i => $"<pattern id='q{i}' width='4' height='4'>" + Repeat($"<rect width='4' height='4' fill='url(#q{i - 1})'/>", 10) + "</pattern>")
                + "</defs><rect width='16' height='16' fill='url(#q6)'/>"),
            SvgRefusal.RenderedElements),
        ["filter inputs that double"] = (
            () => Svg("<defs><filter id='f'><feOffset in='SourceGraphic' result='r0'/>"
                + Chain(20, i => $"<feComposite in='r{i - 1}' in2='r{i - 1}' result='r{i}'/>")
                + "</filter></defs><rect width='16' height='16' filter='url(#f)'/>"),
            SvgRefusal.RenderedElements),
        ["too many elements"] = (
            () => Svg(Repeat("<g/>", (int)SvgPreviewGuard.MaxRenderedElements)),
            SvgRefusal.RenderedElements),
        ["an feImage that draws an element"] = (
            () => Svg("<defs><filter id='f'><feImage href='#r'/></filter></defs><rect id='r' width='16' height='16'/>"
                + "<rect width='16' height='16' filter='url(#f)'/>"),
            SvgRefusal.ElementImage),
        ["a use cycle"] = (
            () => Svg("<defs><use id='a' href='#b'/><use id='b' href='#a'/></defs><use href='#a'/>"),
            SvgRefusal.ReferenceCycle),
        ["a descendant rule that masks the mask's own content"] = (
            () => Svg("<style>svg rect{mask:url(#m)}</style><defs><mask id='m'><rect width='16' height='16' fill='#fff'/></mask></defs>" + Square),
            SvgRefusal.ReferenceCycle),
        ["a DOCTYPE"] = (
            () => "<!DOCTYPE svg [<!ENTITY a 'b'>]>" + Svg(Square),
            SvgRefusal.Markup),
        ["a script"] = (
            () => Svg("<script>1</script>" + Square),
            SvgRefusal.Markup),
        ["an xml-stylesheet instruction"] = (
            () => "<?xml-stylesheet href='a.css'?>" + Svg(Square),
            SvgRefusal.Markup),
        ["an external href"] = (
            () => Svg("<image href='https://example.invalid/a.png' width='4' height='4'/>"),
            SvgRefusal.ExternalReference),
        ["malformed XML"] = (
            () => Svg("<g>" + Square),
            SvgRefusal.Malformed),
    };
}
