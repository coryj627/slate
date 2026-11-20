// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text;
using System.Text.RegularExpressions;
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
            // Custom properties that name each other in a cycle are one node,
            // never a render cycle (Codex's review, stage 4).
            Svg("<style>svg{--a:var(--b, url(#p));--b:var(--a)}</style>"
                + "<defs><pattern id='p' width='4' height='4'>" + Square + "</pattern></defs>"
                + "<rect width='16' height='16' fill='var(--a)'/>"),
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
    /// the depth limit, declarations on elements that draw nothing, a
    /// marker set through <c>var()</c>, and a pattern that paints many
    /// shapes, however it reaches them. The two rows that expand
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
        ["six descendant steps over 254 nested groups"] = (() => DeepDescendant("", ""), true),
        // The red team's M1: the stylesheet and a style attribute are read as
        // ExCSS reads them. A comment marker inside a string or a url()
        // argument opens no comment; a comment, even one holding a
        // semicolon, separates nothing; an escape names what it resolves to.
        ["six descendant steps after a string that holds /*"] = (() => DeepDescendant("rect{x:\"/*\"} ", " /*\"*/"), false),
        ["six descendant steps after a url() that holds /*"] = (() => DeepDescendant("rect{fill:url(/*)} ", " /**/"), false),
        ["a var() fallback chain defined after a string that holds /*"] = (
            () => SlowSvgDocuments.Generate("C_fb F=1 D=16 E=30000")
                .Replace("<style>svg{", "<style>a{b:\"/*\"} svg{", StringComparison.Ordinal)
                .Replace("}</style>", "} /**/</style>", StringComparison.Ordinal),
            false),
        ["a marker set after an empty comment in a style attribute"] = (() => StyledMarker("/**/marker-mid:url(#m)"), false),
        ["a marker set after a comment that holds ; in a style attribute"] = (() => StyledMarker("/* ; */marker-mid:url(#m)"), false),
        ["a marker set by an escaped property name in a style attribute"] = (() => StyledMarker("m\\61rker-mid:url(#m)"), false),
        // The red team's m4 and m6: vertices from arcs with compact flags
        // and from a CSS d:path(), the widest of two markers sharing an id,
        // and a marker set through var() in a style attribute.
        ["a 2,000-rect marker on 10,000 arcs written with compact flags"] = (
            () => MarkedShape("<path fill='none' stroke='black' marker-mid='url(#m)' d='M0 0"
                + string.Concat(Enumerable.Range(0, 10_000).Select(i => $" A1 1 0 00{i % 400} {i / 400}")) + "'/>"),
            false),
        ["a 2,000-rect marker on a path a CSS d:path() draws through 10,000 vertices"] = (
            () => MarkedShape("<path fill='none' stroke='black' marker-mid='url(#m)' style='d:path(\"M0 0"
                + string.Concat(Enumerable.Range(1, 10_000).Select(i => $" L{i % 400} {i / 400}")) + "\")'/>"),
            false),
        ["a 2,000-rect marker on a path a stylesheet d:path() draws through 10,000 vertices"] = (
            () => MarkedShape("<style>path{d:path(\"M0 0"
                + string.Concat(Enumerable.Range(1, 10_000).Select(i => $" L{i % 400} {i / 400}"))
                + "\")}</style><path fill='none' stroke='black' marker-mid='url(#m)'/>"),
            false),
        ["a 2,000-rect marker named by the second of two elements with its id"] = (
            () => SlowSvgDocuments.Generate("D_marker M=2000 V=10000")
                .Replace("<defs>", "<defs><marker id='m'><rect width='1' height='1'/></marker>", StringComparison.Ordinal),
            false),
        ["a 2,000-rect marker set through var() in a style attribute"] = (() => SlowSvgDocuments.Generate("D_varinline M=2000 V=10000"), false),
        // Codex's review (stage 4): a var() whose name nothing defines
        // resolves its fallback, so a marker named in a fallback, at any
        // depth, or in a declared property's own fallback, is placed at
        // full cost, whichever route sets it.
        ["a 2,000-rect marker set by a var() fallback in a presentation attribute"] = (
            () => MarkerSetBy(" marker-mid='var(--undefined, url(#m))'"),
            false),
        ["a 2,000-rect marker set by a var() fallback in a style attribute"] = (
            () => StyledMarker("marker-mid:var(--undefined, url(#m))"),
            false),
        ["a 2,000-rect marker set by a var() fallback in a stylesheet rule"] = (
            () => MarkerSetBy("", "polyline{marker-mid:var(--undefined, url(#m))}"),
            false),
        ["a 2,000-rect marker set by a var() fallback nested one level"] = (
            () => MarkerSetBy(" marker-mid='var(--a, var(--b, url(#m)))'"),
            false),
        ["a 2,000-rect marker named by a declared property's own var() fallback"] = (
            () => MarkerSetBy(" marker-mid='var(--p)'", "svg{--p:var(--undefined, url(#m))}"),
            false),
        // Codex's round 1 (stage 4), equivalent inputs whose estimate fell:
        // path geometry a custom property draws, inline, in a stylesheet and
        // behind a definition's own fallback;
        ["a 2,000-rect marker on a path whose d a style attribute's custom property draws"] = (
            () => MarkedShape("<path fill='none' stroke='black' marker-mid='url(#m)' style='--p:path(\"" + LongPathData + "\");d:var(--p)'/>"),
            false),
        ["a 2,000-rect marker on a path whose d a stylesheet's custom property draws"] = (
            () => MarkedShape("<style>svg{--p:path(\"" + LongPathData + "\")} path{d:var(--p)}</style>"
                + "<path fill='none' stroke='black' marker-mid='url(#m)'/>"),
            false),
        ["a 2,000-rect marker on a path whose d a custom property's own fallback draws"] = (
            () => MarkedShape("<path fill='none' stroke='black' marker-mid='url(#m)' style='--p:var(--undefined, path(\""
                + LongPathData + "\"));d:var(--p)'/>"),
            false),
        // a marker a use sets, which the shapes of its instance inherit;
        ["a 2,000-rect marker on a path whose d a chain of two custom properties draws"] = (
            () => MarkedShape("<path fill='none' stroke='black' marker-mid='url(#m)' style='--q:path(\""
                + LongPathData + "\");--p:var(--q);d:var(--p)'/>"),
            false),
        ["a 2,000-rect marker a use sets on the polyline it draws"] = (
            () => MarkedShape("<defs><polyline id='p' fill='none' stroke='black' points='" + LongPoints + "'/></defs>"
                + "<use href='#p' marker-mid='url(#m)'/>"),
            false),
        ["a 2,000-rect marker a use sets on the polyline of the group it draws"] = (
            () => MarkedShape("<defs><g id='p'><polyline fill='none' stroke='black' points='" + LongPoints + "'/></g></defs>"
                + "<use href='#p' marker-mid='url(#m)'/>"),
            false),
        // a fallback chain beside another element's literal definitions of
        // its names, which do not erase the chain's fallback cost;
        ["a var() fallback chain beside a group that defines its names as literals"] = (
            () => BeforeEnd(
                SlowSvgDocuments.Generate("C_fb F=1 D=16 E=30000"),
                "<g style='" + string.Concat(Enumerable.Range(1, 16).Select(k => $"--v{k}:1;")) + "'/>"),
            false),
        // and custom-property names that end in a valid escaped character,
        // which name the same property wherever they are written.
        ["var() doubling at 15 levels over names ending in an escaped private-use character"] = (
            () => Regex.Replace(SlowSvgDocuments.Generate("C_double D=15 E=2000"), @"--v(\d+)", @"--v$1\E000 "),
            false),
        ["a 2,000-rect marker named through an escaped private-use character in its id"] = (
            () => StyledMarker("marker-mid:url(#m\\E000 )").Replace("<marker id='m'", "<marker id='m&#xE000;'", StringComparison.Ordinal),
            false),
        ["a 2,000-rect marker named through an escaped supplementary character in its id"] = (
            () => StyledMarker("marker-mid:url(#m\\1F600 )").Replace("<marker id='m'", "<marker id='m&#x1F600;'", StringComparison.Ordinal),
            false),
        ["var() doubling at 15 levels over names ending in an escaped semicolon"] = (
            () => Regex.Replace(SlowSvgDocuments.Generate("C_double D=15 E=2000"), @"--v(\d+)", @"--v$1\;"),
            false),
        // Codex's round 2: a use's instance runs every selector branch again.
        ["one rule of 3,000 class branches over a symbol of 100 rects that 500 uses draw"] = (
            () => SlowSvgDocuments.Generate("U_branches B=3000 T=100 U=500 S=1"),
            false),
        // Measured: each instance runs the descendant chains again over its
        // own nesting, about 0.35 s an instance here (18.1 s in all).
        ["ten rules of four descendant steps over a symbol of 32 nested groups that 50 uses draw"] = (
            () => SlowSvgDocuments.Generate("U_desc K=4 R=10 D=32 U=50"),
            false),
        // Codex's round 3: marker content recompiles at every placement, a
        // use's stylesheet scope in it included, and each instance takes
        // the declarations its rules land.
        ["a marker holding a use under 1,000 rules on a 4,000-vertex polyline"] = (
            () => SlowSvgDocuments.Generate("M_use R=1000 E=1000 V=4000 U=1"),
            false),
        ["a marker holding a use of a group that holds a use, under 1,000 rules on a 4,000-vertex polyline"] = (
            () => SlowSvgDocuments.Generate("M_use R=1000 E=1000 V=4000 U=1 N=2"),
            false),
        // Pins: content cloned through a pattern (measured: a pattern's use
        // costs again for each painted element, 784 ms at 200 against 60 ms
        // at 1) and a marker on a shape inside a use's instance.
        ["a pattern holding a use under 1,000 rules that 3,000 rects are painted with"] = (
            () => SlowSvgDocuments.Generate("P_use R=1000 K=3000 U=1"),
            false),
        ["a 2,000-rect marker on a 1,000-vertex polyline in a symbol that 10 uses draw"] = (
            () => Svg("<defs><marker id='m' markerWidth='40' markerHeight='40' refX='0' refY='0' markerUnits='userSpaceOnUse'>"
                + string.Concat(Enumerable.Range(0, 2_000).Select(i => $"<rect x='{i % 40}' y='{i / 40 % 40}' width='1' height='1'/>"))
                + "</marker><symbol id='s'><polyline fill='none' stroke='black' marker-mid='url(#m)' points='"
                + string.Concat(Enumerable.Range(0, 1_000).Select(i => $"{i % 400},{i / 400} ")) + "'/></symbol></defs>"
                + Repeat("<use href='#s'/>", 10)),
            false),
        ["one rule of 1,000 custom properties over a symbol of 100 rects that 300 uses draw"] = (
            () => SlowSvgDocuments.Generate("U_decls D=1000 T=100 U=300"),
            false),
        ["var() doubling at 15 levels over names ending in an escaped supplementary character"] = (
            () => Regex.Replace(SlowSvgDocuments.Generate("C_double D=15 E=2000"), @"--v(\d+)", @"--v$1\1F600 "),
            false),
        // The merge judge's register check (stage 7): each shape a pattern
        // paints costs about 0.34 us per element of the document, whatever
        // the pattern holds, so a swatch an exporter writes is slow on many
        // shapes. Measured, one decode each: 36.7 s, 33.2 s, 14.4 s, 11.0 s,
        // 10.3 s and 39.2 s.
        ["a pattern swatch that 10,000 rects fill"] = (() => SlowSvgDocuments.Generate("P_paint K=10000"), false),
        ["a pattern swatch that 10,000 rects inherit from their group"] = (
            () => SlowSvgDocuments.Generate("P_paint K=10000 I=1"),
            false),
        ["a pattern swatch that 3,000 rects fill among 10,000 plain rects"] = (
            () => SlowSvgDocuments.Generate("P_paint K=3000 X=10000"),
            false),
        ["a pattern swatch a stylesheet rule sets on 3,000 rects among 10,000 plain rects"] = (
            () => SlowSvgDocuments.Generate("P_paint K=3000 X=10000 S=1"),
            false),
        ["a pattern swatch 3,000 rects fill through var() among 10,000 plain rects"] = (
            () => SlowSvgDocuments.Generate("P_paint K=3000 X=10000 V=1"),
            false),
        ["a symbol of 100 rects filling a pattern swatch that 100 uses draw, among 10,000 plain rects"] = (
            () => SlowSvgDocuments.Generate("P_paint K=100 U=100 X=10000"),
            false),
        // The same rects inherit the swatch from the 100 uses that draw
        // them: 41.2 s, added after a mutation that drops a use's paint
        // escaped the rows above.
        ["a symbol of 100 rects that inherit a pattern swatch from each of 100 uses, among 10,000 plain rects"] = (
            () => SlowSvgDocuments.Generate("P_paint K=100 U=100 X=10000 I=1"),
            false),
    };

    /// <summary>Path data of 10,001 vertices.</summary>
    private static readonly string LongPathData =
        "M0 0" + string.Concat(Enumerable.Range(1, 10_000).Select(i => $" L{i % 400} {i / 400}"));

    /// <summary>A polyline's 10,000 points.</summary>
    private static readonly string LongPoints =
        string.Concat(Enumerable.Range(0, 10_000).Select(i => $"{i % 400},{i / 400} "));

    /// <summary><paramref name="source"/> with <paramref name="markup"/>
    /// before its closing tag.</summary>
    private static string BeforeEnd(string source, string markup)
    {
        int end = source.LastIndexOf("</svg>", StringComparison.Ordinal);
        return source[..end] + markup + source[end..];
    }

    /// <summary>#1581's marker row (a 2,000-rect marker on a 10,000-vertex
    /// polyline) with its marker-mid attribute replaced by
    /// <paramref name="attribute"/>, under a stylesheet of
    /// <paramref name="stylesheet"/> when it is not empty.</summary>
    private static string MarkerSetBy(string attribute, string stylesheet = "")
    {
        string source = SlowSvgDocuments.Generate("D_marker M=2000 V=10000")
            .Replace(" marker-mid='url(#m)'", attribute, StringComparison.Ordinal);
        return stylesheet.Length == 0
            ? source
            : source.Replace("<defs>", "<style>" + stylesheet + "</style><defs>", StringComparison.Ordinal);
    }

    /// <summary>Six descendant steps over 254 nested groups, the rule placed
    /// between <paramref name="before"/> and <paramref name="after"/>.</summary>
    private static string DeepDescendant(string before, string after) =>
        Svg("<style>" + before + "g g g g g g rect{fill:red}" + after + "</style>"
            + Repeat("<g>", 254) + Square + Repeat("</g>", 254));

    /// <summary>#1581's marker row with the marker set by a style attribute
    /// of <paramref name="style"/> in place of the marker-mid attribute.</summary>
    private static string StyledMarker(string style) =>
        SlowSvgDocuments.Generate("D_marker M=2000 V=10000")
            .Replace(" marker-mid='url(#m)'", $" style='{style}'", StringComparison.Ordinal);

    /// <summary>A marker of 2,000 rects with the id <c>m</c>, then
    /// <paramref name="shape"/>.</summary>
    private static string MarkedShape(string shape) =>
        Svg("<defs><marker id='m' markerWidth='40' markerHeight='40' refX='0' refY='0' markerUnits='userSpaceOnUse'>"
            + string.Concat(Enumerable.Range(0, 2_000).Select(i => $"<rect x='{i % 40}' y='{i / 40 % 40}' width='1' height='1' fill='red'/>"))
            + "</marker></defs>" + shape);

    public static TheoryData<string, string> HostileShapes() => new()
    {
        { "T_varrule B=320000 V=32000", nameof(SvgRefusal.None) },
        { "T_dupid N=16000", nameof(SvgRefusal.RenderedElements) },
        { "T_prelude K=20000 U=1000", nameof(SvgRefusal.None) },
        // Codex's round 2: 50,000 gradients sharing one id that 50,000
        // distinct custom properties name (2 MB).
        { "T_gradvar N=50000", nameof(SvgRefusal.None) },
        // Codex's round 3: one custom property whose quoted url() argument
        // holds 128,000 more url( openings (512 KB).
        { "T_urlnest N=128000", nameof(SvgRefusal.None) },
    };

    /// <summary>
    /// #1610 (the red team's B1): the guard runs on the caller, so its own
    /// time is bounded on the shapes that made it super-linear: one rule of
    /// 320,000 branches and 32,000 var() declarations (1.1 MB, 16.7 s of
    /// guard), 16,000 references to an id 16,000 elements share (480 KB,
    /// 2.7 s), and one block of 20,000 classes whose 1,000 declarations
    /// name url() targets (the same product through a stylesheet's keys,
    /// found on the way). Each is judged in under a second, the median of
    /// <see cref="WallClockBudget"/>'s warm readings.
    /// </summary>
    [Theory]
    [MemberData(nameof(HostileShapes))]
    public void TheGuardsOwnTimeStaysBounded(string label, string verdict)
    {
        string source = SlowSvgDocuments.Generate(label);
        TimeSpan guardTime = WallClockBudget.WarmMedian(() =>
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            string said = Check(source);
            stopwatch.Stop();
            Assert.Equal(verdict, said);
            return stopwatch.Elapsed;
        });
        Assert.True(
            guardTime < TimeSpan.FromSeconds(1),
            $"{label}: the guard took {guardTime.TotalMilliseconds:F1} ms");
    }

    /// <summary>#1610 (the red team's B1): past its step budget the guard
    /// stops counting and refuses for the document's cost, naming the guard
    /// as the term, so any path that grows past linear ends in bounded time
    /// on the caller.</summary>
    [Fact]
    public void PastItsStepBudgetTheGuardRefusesForTheCost()
    {
        string source = SlowSvgDocuments.Generate("G_scatter N=500");
        Assert.Equal(nameof(SvgRefusal.None), Check(source));
        SvgPreviewGuard.StepBudgetForTests = 1_000;
        try
        {
            SvgRefusal refusal = SvgPreviewGuard.Check(source, 8 * 1024 * 1024, out SvgDecodeEstimate estimate);
            Assert.Equal(SvgRefusal.DecodeCost, refusal);
            Assert.True(estimate.StepsSpent);
            Assert.Equal("guard", estimate.LargestTerm);
        }
        finally
        {
            SvgPreviewGuard.StepBudgetForTests = null;
        }
    }

    /// <summary>#1610 (the red team's B1): a quarter of the step budget admits
    /// 8 MiB of rects, 6,313,640 steps (guard-estimates.csv): the budget only
    /// ends a check no real or calibration document comes near. The most
    /// steps any of them takes, 12,958,636 on 8 MiB of stylesheet, stays
    /// inside half the budget
    /// (<see cref="EachScanStaysInsideTheStepBudgetAtTheCap"/>).</summary>
    [Fact]
    public void TheStepBudgetAdmitsEightMebibytesOfRects()
    {
        string source = SlowSvgDocuments.Generate("Big_rects N=150092");
        SvgPreviewGuard.StepBudgetForTests = SvgPreviewGuard.MaxGuardSteps / 4;
        try
        {
            SvgRefusal refusal = SvgPreviewGuard.Check(source, 8 * 1024 * 1024, out SvgDecodeEstimate estimate);
            Assert.False(estimate.StepsSpent);
            Assert.Equal(SvgRefusal.None, refusal);
        }
        finally
        {
            SvgPreviewGuard.StepBudgetForTests = null;
        }
    }

    /// <summary>#1610 (Codex's round 2): each use render re-applies the
    /// stylesheet to its instance, and every selector branch queries the
    /// instance's elements, so one rule of 1,000 class branches over a
    /// symbol 500 uses draw weighs about what 1,000 one-branch rules
    /// do.</summary>
    [Fact]
    public void AUseInstanceQueriesEveryBranchOfARule()
    {
        double branches = EstimateOf(SlowSvgDocuments.Generate("U_branches B=1000 T=100 U=500 S=1")).Milliseconds;
        double rules = EstimateOf(SlowSvgDocuments.Generate("U_branches B=1000 T=100 U=500 S=0")).Milliseconds;
        Assert.True(
            branches / rules is >= 0.5 and <= 2,
            $"one rule of 1,000 branches: {branches:F0} ms; 1,000 rules: {rules:F0} ms");
    }

    public static TheoryData<string> CombinatorKinds() =>
    [
        "U_desc K=4 R=10 D=32",
        "U_flat Q=1 R=50 T=300",
        "U_flat Q=2 R=50 T=300",
    ];

    /// <summary>#1610 (Codex's round 2): each use render re-runs the
    /// stylesheet's combinator work over its instance (measured for
    /// descendant chains, about 0.35 s an instance of a symbol whose own
    /// estimate is 0.5 s), so 20 uses of a symbol whose stylesheet work is
    /// mostly descendant chains, sibling reads or of-type reads add at least
    /// half of 20 times the document's own stylesheet term.</summary>
    [Theory]
    [MemberData(nameof(CombinatorKinds))]
    public void AUseInstanceTakesItsSubtreesCombinatorWork(string shape)
    {
        SvgDecodeEstimate alone = EstimateOf(SlowSvgDocuments.Generate(shape + " U=0"));
        SvgDecodeEstimate used = EstimateOf(SlowSvgDocuments.Generate(shape + " U=20"));
        double added = used.Milliseconds - alone.Milliseconds;
        double floor = 20 * 0.5 * alone.StylesheetMicroseconds / 1000;
        Assert.True(added >= floor, $"{shape}: 20 uses add {added:F0} ms, under {floor:F0} ms");
    }

    /// <summary>#1610 (Codex's round 3): each use render applies its rules'
    /// declarations to the instance's elements again, so with the rules and
    /// branches fixed, 990 more custom properties in the one rule over a
    /// symbol of 100 rects that 150 uses draw add at least half of what they
    /// land on every instance.</summary>
    [Fact]
    public void AUseInstanceTakesTheDeclarationsItsRulesLand()
    {
        double few = EstimateOf(SlowSvgDocuments.Generate("U_decls D=10 T=100 U=150")).Milliseconds;
        double many = EstimateOf(SlowSvgDocuments.Generate("U_decls D=1000 T=100 U=150")).Milliseconds;
        double floor = 0.5 * 150 * 100 * 990 * SvgDecodeRates.MatchedCustomDeclaration / 1000;
        Assert.True(many - few >= floor, $"990 more declarations add {many - few:F0} ms, under {floor:F0} ms");
    }

    public static TheoryData<string> LargestAdmittedInputs() =>
    [
        "stylesheet", "attributes", "points", "path data", "style attributes", "url arguments",
        "css path", "custom properties", "comments and strings", "escapes", "class lists",
    ];

    /// <summary>
    /// #1610 (stage 6, the lead's closure of the guard-time class): every
    /// scan charges the characters it touches, so the guard's own time is
    /// linear in its input; fed its largest admitted input (a document of
    /// about the 8 MiB cap made of what the scan reads), each scan's
    /// check stays inside half the step budget, and charges every
    /// character it reads.
    /// </summary>
    [Theory]
    [MemberData(nameof(LargestAdmittedInputs))]
    public void EachScanStaysInsideTheStepBudgetAtTheCap(string scan)
    {
        string source = LargestInput(scan);
        Assert.InRange(source.Length, 7 * 1024 * 1024, 8 * 1024 * 1024);
        SvgPreviewGuard.Check(source, 8 * 1024 * 1024, out SvgDecodeEstimate estimate);
        Assert.False(estimate.StepsSpent, $"{scan}: the step budget was spent");
        Assert.True(
            estimate.Steps <= SvgPreviewGuard.MaxGuardSteps / 2,
            $"{scan}: {estimate.Steps:N0} steps of {SvgPreviewGuard.MaxGuardSteps:N0}");
        // Every character read is charged, at most 16 to a step.
        Assert.True(
            estimate.Steps * 16 >= source.Length,
            $"{scan}: {estimate.Steps:N0} steps for {source.Length:N0} characters");
    }

    /// <summary>A document of about the 8 MiB cap made of what one scan
    /// reads.</summary>
    private static string LargestInput(string scan)
    {
        const int Cap = (8 * 1024 * 1024) - 4096;
        static string Fill(string head, string unit, string tail)
        {
            var text = new StringBuilder(Cap);
            text.Append(head);
            while (text.Length + unit.Length + tail.Length < Cap)
            {
                text.Append(unit);
            }
            return text.Append(tail).ToString();
        }
        return scan switch
        {
            "stylesheet" => SlowSvgDocuments.Generate("Big_style N=383126"),
            "attributes" => SlowSvgDocuments.Generate("Big_rects N=150092"),
            "points" => SlowSvgDocuments.Generate("Big_points N=1119023"),
            "path data" => Svg(Fill("<path d='M0 0", " A1 1 0 0 1 3 4", "'/>")),
            "style attributes" => Svg(Fill("", "<rect width='1' height='1' style='fill:red;stroke:blue;stroke-width:2'/>", "")),
            "url arguments" => Svg(Fill(
                "<defs><linearGradient id='g'><stop offset='0'/></linearGradient></defs>",
                "<rect width='1' height='1' fill='url(#g)'/>",
                "")),
            "css path" => Svg(Fill("<path style='d:path(\"M0 0", " L1 2", "\")'/>")),
            "custom properties" => Svg(Fill("<style>svg{", "--p:url(#g) var(--q, 1);", "}</style><rect width='1' height='1' fill='var(--p)'/>")),
            "comments and strings" => Svg(Fill("<style>", "/* a comment */ a{content:\"a string\"}", "</style>" + Square)),
            "escapes" => Svg(Fill("<style>", ".a\\62 c{fill:\\72 ed}", "</style>" + Square)),
            "class lists" => Svg(Fill("<style>.a{fill:red}</style>", "<rect class='a b c d e f g h' width='1' height='1'/>", "")),
            _ => throw new ArgumentException(scan, nameof(scan)),
        };
    }

    public static TheoryData<char> WalkingCombinators() => ['>', '+'];

    /// <summary>#1610 (the red team's m1): a child or adjacent-sibling step
    /// is weighed as one more walk of the document per pass, wide: ExCSS
    /// reads a level or a sibling for each element the walk reaches.</summary>
    [Theory]
    [MemberData(nameof(WalkingCombinators))]
    public void AChildOrAdjacentStepWeighsOneMoreWalk(char combinator)
    {
        static string Document(string selector) =>
            Svg("<style>" + Repeat(selector + "{fill:red}", 100) + "</style><g>"
                + Repeat("<rect width='1' height='1'/>", 1_000) + "</g>");
        double plain = EstimateOf(Document("rect")).Microseconds;
        double stepped = EstimateOf(Document("g" + combinator + "rect")).Microseconds;
        double walk = 100 * 1_003 * SvgDecodeRates.QueryPerBranchElement;
        Assert.InRange(stepped - plain, walk * 0.99, walk * 1.01);
    }

    public static TheoryData<string> RareLeftCompounds() =>
    [
        // Ten general-sibling rules whose left compound one rect of 10,000
        // carries: the wide count reads every sibling list for every rect
        // (14 s); the left compound's matches read one (1.4 ms).
        "sibling",
        // Ten descendant rules whose left compound only the innermost of 200
        // nested groups carries, 50 rects in each: the depth bound counts
        // every ancestor of every rect (0.6 s); the left compound's
        // descendants are 50.
        "descendant",
    ];

    /// <summary>#1610 (the red team's m2): a branch of one step counts only
    /// what its left compound's matches read: for a descendant step their
    /// descendants, for a general-sibling step their sibling lists.</summary>
    [Theory]
    [MemberData(nameof(RareLeftCompounds))]
    public void AOneStepBranchCountsWhatItsLeftCompoundMatchesRead(string shape)
    {
        string source = shape == "sibling"
            ? Svg("<style>" + Repeat(".a ~ rect{fill:red}", 10) + "</style><rect class='a' width='1' height='1'/>"
                + Repeat("<rect width='1' height='1'/>", 9_999))
            : Svg("<style>" + Repeat(".a rect{fill:red}", 10) + "</style>"
                + string.Concat(Enumerable.Range(1, 200).Select(depth =>
                    (depth == 200 ? "<g class='a'>" : "<g>") + Repeat("<rect width='1' height='1'/>", 50)))
                + Repeat("</g>", 200));
        Assert.Equal(nameof(SvgRefusal.None), Check(source));
        double estimate = EstimateOf(source).Milliseconds;
        Assert.True(estimate < 200, $"{shape}: estimated at {estimate:F1} ms");
    }

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
        // The follow-up's selector counts admit what decodes quickly: ten
        // rules of four descendant steps over 32 nested groups, 363 ms to
        // decode, about 370 ms; one general-sibling step over 3,000 rects,
        // 241 ms, about 133 ms; one first-of-type rule over 3,000 rects,
        // 223 ms, about 182 ms.
        { "N_desc K=4 R=10 D=32", 600 },
        { "N_sib K=2 R=1 N=3000", 600 },
        { "N_nth Q=1 R=1 N=3000", 600 },
        // The red team's M2: Svg.Custom reads @media and skips every other
        // block at-rule, so keyframe stops are not rules filed under every
        // element: 300 animated rects among 1,000.
        { "A_keyframes A=300 E=1000", 300 },
        // Stage 7: a pattern swatch on 1,000 rects decodes in about 0.45 s, and
        // one that paints ten shapes among 20,000 plain rects in 0.46 s, its
        // drawing; neither the swatch's content nor an unpainted shape counts
        // as a paint. A custom property that holds the pattern paints only
        // the shapes that use it, not the element that defines it, in a
        // stylesheet or in a style attribute around every shape.
        { "P_paint K=1000", 600 },
        { "P_paint K=10 X=20000", 200 },
        { "P_paint K=10 X=5000 V=1", 200 },
        { "P_paint K=10 X=5000 V=2", 200 },
    };

    /// <summary>#1610 (CB-29): the shapes the estimate's corrections exist
    /// for are admitted, each estimated under its own bound, which sits
    /// above the row's estimate and under a second, so the row holds at any
    /// limit the owner picks.</summary>
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
    /// #1610 (the merge judge's minor 1): the decode limit is the
    /// calibration's 2,000 ms placeholder until the owner picks one (CD20's
    /// second step), and it is where the verdict turns: a plotting scatter
    /// estimated within 1% under it is admitted, and the scatter of one
    /// point more, within 1% over it, is refused. When the owner picks, the
    /// limit and the pair's two sizes are the only numbers that move (the
    /// largest scatter admitted at 3 s has 3,425 points, at 5 s 4,440).
    /// </summary>
    [Fact]
    public void TheDecodeLimitIsWhereTheVerdictTurns()
    {
        const int limit = 2_000;
        const int points = 2_785;
        Assert.Equal(limit, SvgPreviewGuard.MaxEstimatedDecodeMilliseconds);
        string under = SlowSvgDocuments.Generate($"G_scatter N={points}");
        string over = SlowSvgDocuments.Generate($"G_scatter N={points + 1}");
        double below = EstimateOf(under).Milliseconds;
        double above = EstimateOf(over).Milliseconds;
        Assert.True(below > limit * 0.99 && below <= limit, $"{points} points: estimated at {below:F1} ms");
        Assert.True(above > limit && above <= limit * 1.01, $"{points + 1} points: estimated at {above:F1} ms");
        Assert.Equal(nameof(SvgRefusal.None), Check(under));
        Assert.Equal(nameof(SvgRefusal.DecodeCost), Check(over));
    }

    /// <summary>A drawing program's pattern swatch of ten children.</summary>
    private const string Swatch =
        "<pattern id='pat' width='12' height='12' patternUnits='userSpaceOnUse'>"
        + "<rect width='12' height='12' fill='#f4f1e8'/>"
        + "<path d='M 0 12 L 12 0' stroke='#5b4a3a' stroke-width='0.8'/>"
        + "<path d='M -3 3 L 3 -3' stroke='#5b4a3a' stroke-width='0.8'/>"
        + "<path d='M 9 15 L 15 9' stroke='#5b4a3a' stroke-width='0.8'/>"
        + "<path d='M 0 6 L 6 0' stroke='#8a7a6a' stroke-width='0.4'/>"
        + "<circle cx='3' cy='9' r='1' fill='#2f6f8f'/>"
        + "<circle cx='9' cy='3' r='1' fill='#2f6f8f'/>"
        + "<circle cx='9' cy='9' r='0.6' fill='#8f2f4f'/>"
        + "<circle cx='3' cy='3' r='0.6' fill='#8f2f4f'/>"
        + "<polygon points='5,5 7,5 6,7' fill='#3a5b4a'/>"
        + "</pattern>";

    /// <summary>3,000 rects in a group, each with <paramref name="rect"/>'s
    /// attributes for its index, under <paramref name="stylesheet"/>, beside
    /// <see cref="Swatch"/>: the documents stage 8 decoded. With every rect
    /// painting the swatch, the pattern term is about 3.07 s.</summary>
    private static string PaintedGroup(string stylesheet, string group, Func<int, string> rect)
    {
        var s = new StringBuilder($"<svg {Ns} width='800' height='800'><defs>{Swatch}</defs>");
        if (stylesheet.Length > 0)
        {
            s.Append("<style>").Append(stylesheet).Append("</style>");
        }
        s.Append($"<g{group}>");
        for (int i = 0; i < 3_000; i++)
        {
            s.Append($"<rect x='{i % 100 * 8}' y='{i / 100 % 100 * 8}' width='7' height='7'{rect(i)}/>");
        }
        return s.Append("</g></svg>").ToString();
    }

    private static string PaintedGroup(string stylesheet, string group, string rect) =>
        PaintedGroup(stylesheet, group, _ => rect);

    public static TheoryData<string> PaintCascadeCases() => [.. PaintCascade.Keys];

    /// <summary>
    /// #1610 (Codex's round 5, stage 8): a shape's own fill and stroke follow
    /// the cascade the decoder uses, each property on its own. Each case
    /// either leaves the pattern term at 0 with the verdict None, or counts
    /// the pattern. Its decode, one each in stage 8, is in its row's
    /// comment.
    /// </summary>
    [Theory]
    [MemberData(nameof(PaintCascadeCases))]
    public void AShapesOwnPaintFollowsTheDecodersCascade(string name)
    {
        (Func<string> build, bool counted) = PaintCascade[name];
        string source = build();
        SvgPreviewGuard.Check(source, 8 * 1024 * 1024, out SvgDecodeEstimate estimate);
        double pattern = estimate.PatternMicroseconds / 1000;
        if (counted)
        {
            Assert.True(pattern > 1_000, $"{name}: the pattern term is {pattern:F1} ms");
        }
        else
        {
            Assert.True(pattern == 0, $"{name}: the pattern term is {pattern:F1} ms");
            Assert.Equal(nameof(SvgRefusal.None), Check(source));
        }
    }

    private const string PatternFill = " fill='url(#pat)'";

    internal static readonly Dictionary<string, (Func<string> Build, bool Counted)> PaintCascade = new()
    {
        // Each source of a colour clears the group's pattern: 44, 108, 52
        // and 66 ms to decode, where the rects that keep it take 3.3 s.
        ["a colour in the presentation attribute"] = (() => PaintedGroup("", PatternFill, " fill='red'"), false),
        ["a colour in the style attribute"] = (() => PaintedGroup("", PatternFill, " style='fill:red'"), false),
        ["a colour from a type rule"] = (() => PaintedGroup("rect{fill:red}", PatternFill, ""), false),
        ["a colour from a class rule"] = (() => PaintedGroup(".s{fill:red}", PatternFill, " class='s'"), false),
        // none, a missing reference and a defined var(): 47, 41 and 73 ms.
        ["none"] = (() => PaintedGroup("", PatternFill, " fill='none'"), false),
        ["a reference to no element"] = (() => PaintedGroup("", PatternFill, " fill='url(#missing)'"), false),
        ["a var() the root defines as a colour"] = (() => PaintedGroup("svg{--c:red}", PatternFill, " fill='var(--c)'"), false),
        ["a var() with a colour fallback"] = (() => PaintedGroup("", PatternFill, " fill='var(--undefined, red)'"), false),
        // Stroke on its own, and both: 33 and 47 ms; the kept stroke, 3.2 s.
        ["a stroke of none under a pattern stroke"] = (() => PaintedGroup("", " stroke='url(#pat)'", " stroke='none'"), false),
        ["a pattern stroke kept"] = (() => PaintedGroup("", " stroke='url(#pat)'", ""), true),
        ["both cleared under a pattern fill and stroke"] = (
            () => PaintedGroup("", " fill='url(#pat)' stroke='url(#pat)'", " fill='red' stroke='red'"),
            false),
        // What keeps the parent's paint: inherit (3.2 s), a var() nothing
        // defines (3.2 s), and currentColor, which the lead's rule keeps
        // although this decoder draws it as the colour (81 ms).
        ["inherit"] = (() => PaintedGroup("", PatternFill, " fill='inherit'"), true),
        ["a var() nothing defines"] = (() => PaintedGroup("", PatternFill, " fill='var(--undefined)'"), true),
        ["a var() nothing defines, in the style attribute"] = (
            () => PaintedGroup("", PatternFill, " style='fill:var(--undefined)'"),
            true),
        ["currentColor"] = (() => PaintedGroup("", PatternFill, " fill='currentColor'"), true),
        // The reverse: a rule sets the pattern under a solid group (2.5 s).
        ["a class rule that sets the pattern under a red group"] = (
            () => PaintedGroup(".p{fill:url(#pat)}", " fill='red'", " class='p'"),
            true),
        // The cascade: the style attribute beats a rule (46 ms), a rule beats
        // the presentation attribute (61 ms), and an !important rule beats
        // the style attribute (2.6 s).
        ["the style attribute over a pattern rule"] = (
            () => PaintedGroup(".p{fill:url(#pat)}", "", " class='p' style='fill:red'"),
            false),
        ["a colour rule over a pattern attribute"] = (
            () => PaintedGroup(".p{fill:red}", "", " class='p' fill='url(#pat)'"),
            false),
        ["an !important pattern rule over the style attribute"] = (
            () => PaintedGroup(".p{fill:url(#pat) !important}", "", " class='p' style='fill:red'"),
            true),
        // Specificity over source order (46 ms), then source order (2.6 s).
        ["a compound colour rule over a later class pattern rule"] = (
            () => PaintedGroup("rect.p{fill:red} .p{fill:url(#pat)}", "", " class='p'"),
            false),
        ["a later pattern rule over an earlier colour rule of the same specificity"] = (
            () => PaintedGroup(".p{fill:red} .p{fill:url(#pat)}", "", " class='p'"),
            true),
        // Where the guard cannot tell a rule matches, it counts the pattern:
        // a selector with a combinator (55 ms), and an id many elements
        // share, under a pattern group with a colour rule (3.7 to 3.9 s) and
        // under a red group with a pattern rule (2.0 s).
        ["a colour rule whose selector has a combinator"] = (() => PaintedGroup("g > rect{fill:red}", PatternFill, ""), true),
        ["a pattern rule naming an id 3,000 rects share, under a red group"] = (
            () => PaintedGroup("#i{fill:url(#pat)}", " fill='red'", " id='i'"),
            true),
        ["a colour rule naming an id 3,000 rects share"] = (() => PaintedGroup("#i{fill:red}", PatternFill, " id='i'"), true),
        // A use's own colour clears the group's pattern for its instance
        // (160 ms; 23.1 s where the instance keeps it).
        ["a colour a use sets on its instance"] = (
            () => Svg("<defs>" + Swatch + "<symbol id='s'>" + Repeat("<rect width='7' height='7'/>", 100)
                + "</symbol></defs><g fill='url(#pat)'>" + Repeat("<use href='#s' fill='red'/>", 3_000 / 100)
                + "</g>" + Repeat("<rect width='1' height='1' fill='red'/>", 20_000)),
            false),
        ["a use's instance that keeps the group's pattern"] = (
            () => Svg("<defs>" + Swatch + "<symbol id='s'>" + Repeat("<rect width='7' height='7'/>", 100)
                + "</symbol></defs><g fill='url(#pat)'>" + Repeat("<use href='#s'/>", 3_000 / 100)
                + "</g>" + Repeat("<rect width='1' height='1' fill='red'/>", 20_000)),
            true),
    };

    /// <summary>#1610 (Codex's round 5, stage 8): under a group whose fill is
    /// the pattern, the rects that set a colour clear it and the rest keep
    /// it, so the term counts the keepers alone: half of 3,000 here, each
    /// across the document's 3,014 elements (1.54 s; 1.63 s to
    /// decode).</summary>
    [Fact]
    public void AGroupsPatternCountsOnlyTheShapesThatKeepIt()
    {
        string source = PaintedGroup("", PatternFill, i => i % 2 == 0 ? " fill='red'" : "");
        SvgPreviewGuard.Check(source, 8 * 1024 * 1024, out SvgDecodeEstimate estimate);
        double expected = SvgDecodeRates.PatternPaintElement * 1_500 * 3_014;
        Assert.True(
            Math.Abs(estimate.PatternMicroseconds - expected) < 1,
            $"the pattern term is {estimate.PatternMicroseconds:F0} us; 1,500 keepers give {expected:F0} us");
        Assert.Equal(nameof(SvgRefusal.None), Check(source));
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

    /// <summary>A pattern of 40 rects that fills 10,000 rects, each set as
    /// <paramref name="fill"/>, under a stylesheet of
    /// <paramref name="stylesheet"/>; drawn through
    /// <c>fill='url(#pat)'</c> it renders 420,000 elements.</summary>
    private static string PatternFills(string stylesheet, string fill, string open = "", string close = "") =>
        Svg((stylesheet.Length == 0 ? "" : "<style>" + stylesheet + "</style>")
            + "<defs><pattern id='pat' width='4' height='4' patternUnits='userSpaceOnUse'>"
            + Repeat("<rect width='1' height='1'/>", 40) + "</pattern></defs>"
            + open + Repeat($"<rect width='4' height='4'{fill}/>", 10_000) + close);

    internal static readonly Dictionary<string, (Func<string> Build, SvgRefusal Expected)> Refusals = new()
    {
        // Codex's review (stage 4): an element reaches every url() a var()
        // in its paint can resolve to, through each custom property it
        // names, so a pattern drawn through one counts as drawn through
        // url(): one per filled element.
        ["a pattern drawn through a custom property in an attribute"] = (
            () => PatternFills("svg{--p:url(#pat)}", " fill='var(--p)'"),
            SvgRefusal.RenderedElements),
        ["a pattern drawn through a custom property in a style attribute"] = (
            () => PatternFills("svg{--p:url(#pat)}", " style='fill:var(--p)'"),
            SvgRefusal.RenderedElements),
        ["a pattern drawn through a custom property in a stylesheet rule"] = (
            () => PatternFills("svg{--p:url(#pat)} .u{fill:var(--p)}", " class='u'"),
            SvgRefusal.RenderedElements),
        ["a pattern drawn through a chain of two custom properties"] = (
            () => PatternFills("svg{--q:url(#pat);--p:var(--q)}", " fill='var(--p)'"),
            SvgRefusal.RenderedElements),
        ["a pattern drawn through a declared property's own var() fallback"] = (
            () => PatternFills("svg{--p:var(--undefined, url(#pat))}", " fill='var(--p)'"),
            SvgRefusal.RenderedElements),
        ["a pattern drawn through a custom property a group's style attribute defines"] = (
            () => PatternFills("", " fill='var(--p)'", "<g style='--p:url(#pat)'>", "</g>"),
            SvgRefusal.RenderedElements),
        // Pins: a fallback in the element's own value or in the rule's own
        // declaration is a url() the guard already reads.
        ["a pattern drawn through a var() fallback in an attribute"] = (
            () => PatternFills("", " fill='var(--undefined, url(#pat))'"),
            SvgRefusal.RenderedElements),
        ["a pattern drawn through a var() fallback in a stylesheet rule"] = (
            () => PatternFills(".u{fill:var(--undefined, url(#pat))}", " class='u'"),
            SvgRefusal.RenderedElements),
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
