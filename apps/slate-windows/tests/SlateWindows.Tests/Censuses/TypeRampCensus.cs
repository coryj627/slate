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

    [GeneratedRegex(@"FontSize=""\d|Property=""FontSize""\s+Value=""\d|\b\w*FontSize\s*=\s*\d")]
    private static partial Regex LiteralSize();

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
            if (relative.Split(Path.DirectorySeparatorChar)[0] is "bin" or "obj")
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
