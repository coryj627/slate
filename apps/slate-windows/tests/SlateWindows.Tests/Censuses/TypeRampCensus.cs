// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.RegularExpressions;

namespace SlateWindows.Tests.Censuses;

/// <summary>DS-2: a size in the shell is a step of the design's ramp, so text size can
/// scale it. A literal is one of the sites named here, with its reason, or a glyph
/// beside SymbolThemeFontFamily, which doesn't scale. A named site that's gone fails
/// too, so the list can't go stale.</summary>
[Trait("census", "type-ramp")]
public sealed partial class TypeRampCensus
{
    private static readonly (string File, string Fragment, string Reason)[] Exceptions =
    [
        ("EditorInteractions.cs", "ActualFontSize = 14", "the reader's own size preference"),
        ("EditorInteractions.cs", "MinimumFontSize = 8", "that preference's bounds"),
        ("EditorInteractions.cs", "MaximumFontSize = 40", "that preference's bounds"),
        (@"Graph\GraphDiagramView.cs", "LabelFontSize = 11", "drawn labels, already scaled by text size and zoom"),
        (@"Reading\ReadingDocumentBuilder.cs", "FontSize = 15,", "the reading view's scale is DS-3's"),
        (@"Reading\ReadingDocumentBuilder.cs", "PreambleCaptionFontSize = 12", "the reading view's scale is DS-3's"),
        (@"Reading\ReadingSurface.cs", "FontSize = 15,", "the reading view's scale is DS-3's"),
    ];

    /// <summary>The app's one window that never shows text: it reads a system
    /// metric at startup and closes.</summary>
    private const string ProbeWindowFile = "App.xaml.cs";

    /// <summary>A size as an attribute, in a setter either way round, as a property
    /// element, assigned, or passed with its dependency property.</summary>
    [GeneratedRegex(@"FontSize=""\d|Property=""[\w.]*FontSize""\s+Value=""\d|Value=""\d[^""]*""\s+Property=""[\w.]*FontSize""|\.FontSize>\s*\d|\b\w*FontSize\s*=\s*\d|FontSizeProperty\s*,\s*\d")]
    private static partial Regex LiteralSize();

    /// <summary>The census's teeth: each way of writing a size is a hit, and each
    /// way of reading one from the ramp isn't.</summary>
    [Theory]
    [InlineData("<TextBlock FontSize=\"11\" />", true)]
    [InlineData("<Run TextElement.FontSize=\"11\" />", true)]
    [InlineData("<Setter Property=\"FontSize\" Value=\"11\" />", true)]
    [InlineData("<Setter Value=\"11\" Property=\"FontSize\" />", true)]
    [InlineData("<Setter Property=\"TextBlock.FontSize\"\n        Value=\"11\" />", true)]
    [InlineData("<TextBlock.FontSize>11</TextBlock.FontSize>", true)]
    [InlineData("text.FontSize = 11;", true)]
    [InlineData("new TextBlock { FontSize = 11.5 }", true)]
    [InlineData("internal const double LabelFontSize = 11;", true)]
    [InlineData("snippet.SetValue(TextBlock.FontSizeProperty, 11.0);", true)]
    [InlineData("new Setter(Control.FontSizeProperty, 11d)", true)]
    [InlineData("<TextBlock FontSize=\"{DynamicResource Slate.FontSizeBody}\" />", false)]
    [InlineData("<Setter Property=\"FontSize\" Value=\"{DynamicResource Slate.FontSizeBody}\" />", false)]
    [InlineData("window.SetResourceReference(Control.FontSizeProperty, Body.SizeKey);", false)]
    [InlineData("text.FontSize = item.FontSize;", false)]
    public void TheCensusSeesEveryWayOfWritingASize(string source, bool literal) =>
        Assert.Equal(literal, LiteralSize().IsMatch(source));

    /// <summary>Nothing above a window of its own sets a size, so one that forgot would
    /// draw at the system's 12 in plain Segoe UI.</summary>
    [Fact]
    public void EveryWindowTakesTheRamp()
    {
        string root = SourceText.ShellSourceRoot();
        string[] without = [.. Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(file => !IsBuildOutput(Path.GetRelativePath(root, file)) && Path.GetFileName(file) != ProbeWindowFile)
            .Where(file =>
            {
                string source = File.ReadAllText(file);
                return Regex.IsMatch(source, @"\bnew Window\b") && !source.Contains("TypeRamp.UseBody(", StringComparison.Ordinal);
            })
            .Select(file => Path.GetRelativePath(root, file))];
        Assert.True(without.Length == 0, "A window with no ramp: " + string.Join(", ", without));
    }

    private static bool IsBuildOutput(string relative) =>
        relative.Split(Path.DirectorySeparatorChar)[0] is "bin" or "obj";

    [Fact]
    public void EverySizeIsAStepOfTheRamp()
    {
        string root = SourceText.ShellSourceRoot();
        var seen = new HashSet<(string, string)>();
        var literals = new List<string>();
        foreach (string file in Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.Ordinal) || path.EndsWith(".xaml", StringComparison.Ordinal)))
        {
            string relative = Path.GetRelativePath(root, file);
            if (IsBuildOutput(relative))
            {
                continue;
            }

            string source = File.ReadAllText(file);
            foreach (Match match in LiteralSize().Matches(source))
            {
                string line = LineAt(source, match.Index);
                (string File, string Fragment, string Reason)[] named = [.. Exceptions.Where(exception => exception.File == relative && line.Contains(exception.Fragment, StringComparison.Ordinal))];
                if (named.Length > 0)
                {
                    seen.Add((named[0].File, named[0].Fragment));
                }
                else if (!IsGlyph(source, match.Index))
                {
                    literals.Add($"{relative}: {line.Trim()}");
                }
            }
        }

        Assert.True(literals.Count == 0, $"{literals.Count} literal sizes:\n" + string.Join('\n', literals));
        string[] stale = [.. Exceptions.Where(exception => !seen.Contains((exception.File, exception.Fragment))).Select(exception => $"{exception.File}: {exception.Fragment}")];
        Assert.True(stale.Length == 0, "Named here and gone from the source:\n" + string.Join('\n', stale));
    }

    private static string LineAt(string source, int index)
    {
        int start = source.LastIndexOf('\n', index) + 1;
        int end = source.IndexOf('\n', index);
        return source[start..(end < 0 ? source.Length : end)];
    }

    /// <summary>Whether the XAML tag holding this attribute draws a glyph.</summary>
    private static bool IsGlyph(string source, int index)
    {
        int start = source.LastIndexOf('<', index);
        int end = source.IndexOf('>', index);
        return start >= 0 && end >= 0 && source[start..end].Contains("SymbolThemeFontFamily", StringComparison.Ordinal);
    }
}
