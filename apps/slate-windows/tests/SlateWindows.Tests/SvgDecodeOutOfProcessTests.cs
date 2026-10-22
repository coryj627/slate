// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;
using System.Text;

namespace SlateWindows.Tests;

/// <summary>
/// D4: the whole SVG preview decode, run in a child process
/// (tools/SvgDecodeProbe) because a document that gets past
/// <see cref="SvgPreviewGuard"/> can end the process that decodes it.
/// Each hostile document is the largest of its kind that fits the 8 MiB
/// preview cap: nested or chained deeply enough to exhaust any stack the
/// decode could run on, or (the fan-out and the feImage chain) busy for
/// far longer than the probe's minute. Without the guard the probe dies
/// or runs out its time.
/// </summary>
public sealed class SvgDecodeOutOfProcessTests
{
    private const int PreviewCap = 8 * 1024 * 1024;
    private const string Square = "<rect width='4' height='4' fill='#00f'/>";

    private static readonly Dictionary<string, Func<string>> Hostile = new()
    {
        ["element nesting"] = () => Nested("<g>", "</g>"),
        ["nested svg elements"] = () => Nested("<svg>", "</svg>"),
        ["a use chain"] = () => Linked(
            $"<defs><g id='u0'>{Square}</g>",
            i => $"<use id='u{i}' href='#u{i - 1}'/>",
            n => $"</defs><use href='#u{n}'/>"),
        ["a mask chain"] = () => Linked(
            "<defs><mask id='m0'><rect width='4' height='4'/></mask>",
            i => $"<mask id='m{i}'><rect width='4' height='4' mask='url(#m{i - 1})'/></mask>",
            n => $"</defs><rect width='4' height='4' mask='url(#m{n})'/>"),
        ["a chain of filter inputs"] = () => Linked(
            "<defs><filter id='f'><feOffset in='SourceGraphic' result='r0'/>",
            i => $"<feOffset in='r{i - 1}' result='r{i}'/>",
            _ => "</filter></defs><rect width='4' height='4' filter='url(#f)'/>"),
        ["bracket nesting in a stylesheet"] = () => Nested("calc(", ")", "<style>rect{stroke-width:", "1px}</style>" + Square),
        // Svg.Custom joins <style> elements: no element here nests more than
        // the cap, but the joined stylesheet nests far deeper.
        ["bracket nesting split across style elements"] = () => Nested(
            "<style>" + string.Concat(Enumerable.Repeat("calc(", SvgPreviewGuard.MaxValueNesting)) + "</style>",
            "<style>" + new string(')', SvgPreviewGuard.MaxValueNesting) + "</style>",
            head: "<style>rect{stroke-width:</style>",
            tail: "<style>}</style>" + Square,
            middle: "<style>1px</style>"),
        ["url references in an attribute"] = () => Repeated("url()", "<rect width='4' height='4' fill='", "red'/>"),
        ["exponential use fan-out"] = () => SvgPreviewGuardTests.Svg(
            "<defs><g id='l0'>" + Square + "</g>"
            + string.Concat(Enumerable.Range(1, 9).Select(i =>
                $"<g id='l{i}'>" + string.Concat(Enumerable.Repeat($"<use href='#l{i - 1}'/>", 10)) + "</g>"))
            + "</defs><use href='#l9'/>"),
        ["an feImage chain"] = () => SvgPreviewGuardTests.Svg(
            "<defs><rect id='e0' width='4' height='4'/>"
            + string.Concat(Enumerable.Range(1, 200).Select(i =>
                $"<filter id='f{i}'><feImage href='#e{i - 1}'/></filter><rect id='e{i}' width='4' height='4' filter='url(#f{i})'/>"))
            + "</defs><use href='#e200'/>"),
    };

    public static TheoryData<string> HostileCases() => [.. Hostile.Keys];

    [Theory]
    [MemberData(nameof(HostileCases))]
    public void RefusesAHostileDocumentWithoutEndingTheDecodingProcess(string name)
    {
        string document = Hostile[name]();
        Assert.True(Encoding.UTF8.GetByteCount(document) <= PreviewCap, $"{name} is over the preview cap");

        (int exitCode, string verdict, string error) = RunProbe(document);

        Assert.True(exitCode == 0, $"{name}: the probe ended with {exitCode}: {error}");
        Assert.Equal("refused", verdict);
    }

    /// <summary>The guard keeps real documents: an Illustrator-style
    /// stylesheet and Mermaid-style markers decode. And Svg.Skia's secure
    /// mode draws a nested SVG image as a placeholder without parsing it:
    /// the nested document here is deep enough to end the probe if a
    /// future Svg.Skia began to parse it past the guard.</summary>
    [Fact]
    public void DecodesRealDocumentsAndDrawsANestedSvgImageAsAPlaceholder()
    {
        string illustrator = SvgPreviewGuardTests.Svg(
            "<style>.st0{fill:url(#g)}.st1{clip-path:url(#c)}</style>"
            + "<defs><linearGradient id='g'><stop offset='0' stop-color='red'/></linearGradient>"
            + "<clipPath id='c'><path d='M0 0h16v16z'/></clipPath></defs>"
            + "<g class='st1'><rect class='st0' width='16' height='16'/></g>");
        string mermaid = SvgPreviewGuardTests.Svg(
            "<defs><marker id='arrow-0' markerWidth='8' markerHeight='8'><path d='M0 0L8 4L0 8z'/></marker></defs>"
            + "<path d='M1 1L15 15' stroke='#000' marker-end='url(#arrow-0)'/>");
        int depth = 700_000;
        string inner = SvgPreviewGuardTests.Svg(
            string.Concat(Enumerable.Repeat("<g>", depth)) + Square + string.Concat(Enumerable.Repeat("</g>", depth)));
        string nested = SvgPreviewGuardTests.Svg(
            "<image width='16' height='16' href='data:image/svg+xml;base64,"
            + Convert.ToBase64String(Encoding.UTF8.GetBytes(inner)) + "'/>");
        Assert.True(Encoding.UTF8.GetByteCount(nested) <= PreviewCap);

        foreach ((string name, string document) in new[] { ("illustrator", illustrator), ("mermaid", mermaid), ("nested", nested) })
        {
            (int exitCode, string verdict, string error) = RunProbe(document);
            Assert.True(exitCode == 0, $"{name}: the probe ended with {exitCode}: {error}");
            Assert.True(verdict == "decoded", $"{name}: {verdict}");
        }
    }

    private static string Nested(
        string open, string close, string head = "", string tail = "", string middle = Square)
    {
        int fixedBytes = SvgPreviewGuardTests.Svg(head + middle + tail).Length;
        int count = (PreviewCap - fixedBytes) / (open.Length + close.Length);
        var body = new StringBuilder(PreviewCap);
        body.Append(head).Insert(body.Length, open, count).Append(middle).Insert(body.Length, close, count).Append(tail);
        return SvgPreviewGuardTests.Svg(body.ToString());
    }

    private static string Repeated(string unit, string head, string tail)
    {
        int fixedBytes = SvgPreviewGuardTests.Svg(head + tail).Length;
        int count = (PreviewCap - fixedBytes) / unit.Length;
        return SvgPreviewGuardTests.Svg(new StringBuilder(PreviewCap).Append(head).Insert(head.Length, unit, count).Append(tail).ToString());
    }

    private static string Linked(string head, Func<int, string> link, Func<int, string> tail)
    {
        int fixedBytes = SvgPreviewGuardTests.Svg(head).Length + 64;
        var body = new StringBuilder(PreviewCap).Append(head);
        int count = 0;
        while (true)
        {
            string next = link(count + 1);
            if (fixedBytes + body.Length + next.Length + tail(count + 1).Length > PreviewCap)
            {
                break;
            }
            body.Append(next);
            count++;
        }
        return SvgPreviewGuardTests.Svg(body.Append(tail(count)).ToString());
    }

    private static (int ExitCode, string Verdict, string Error) RunProbe(string document)
    {
        string probe = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "tools", "SvgDecodeProbe", "bin", BuildConfiguration(), "net10.0-windows", "SvgDecodeProbe.dll"));
        Assert.True(File.Exists(probe), $"The declared SvgDecodeProbe project dependency did not build {probe}");

        string file = Path.Combine(Path.GetTempPath(), $"slate-svg-probe-{Guid.NewGuid():N}.svg");
        File.WriteAllText(file, document, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        try
        {
            var start = new ProcessStartInfo("dotnet")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            start.ArgumentList.Add(probe);
            start.ArgumentList.Add(file);
            using Process process = Process.Start(start)!;
            Task<string> output = process.StandardOutput.ReadToEndAsync();
            Task<string> error = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(60_000))
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
                return (-1, "timed out", "the probe ran out its minute");
            }
            string errorText = error.Result;
            return (process.ExitCode, output.Result.Trim(), errorText[..Math.Min(errorText.Length, 400)]);
        }
        finally
        {
            File.Delete(file);
        }
    }

    private static string BuildConfiguration()
    {
#if DEBUG
        return "Debug";
#else
        return "Release";
#endif
    }
}
