// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text;

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
    /// and each cap itself.</summary>
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

        foreach (string source in accepted)
        {
            Assert.True(
                Check(source) == nameof(SvgRefusal.None),
                $"refused ({Check(source)}): {source[..Math.Min(source.Length, 200)]}");
        }
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
