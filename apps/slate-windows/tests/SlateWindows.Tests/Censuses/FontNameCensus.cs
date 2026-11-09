// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.RegularExpressions;

namespace SlateWindows.Tests.Censuses;

/// <summary>DS-1: a font is named in one place, the type layer. A name anywhere
/// else draws a font the app doesn't ship on a machine that happens to have it, and
/// a composite family answers false to <c>TryGetGlyphTypeface</c>, so that call can't
/// be used on what the editor draws.</summary>
[Trait("census", "font-names")]
public sealed partial class FontNameCensus
{
    /// <summary>The two places that may name a font.</summary>
    private static readonly string[] TypeLayer = ["Themes", "Fonts"];

    [GeneratedRegex(@"\bConsolas\b|\bCascadia\b|\bnew\s+(?:System\.Windows\.Media\.)?FontFamily\s*\(|\bTryGetGlyphTypeface\b")]
    private static partial Regex FontNaming();

    [Fact]
    public void FontNamesLiveInTheTypeLayer()
    {
        string root = SourceText.ShellSourceRoot();
        string[] hits = [.. Directory
            .EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
            .Where(file => file.EndsWith(".cs", StringComparison.Ordinal) || file.EndsWith(".xaml", StringComparison.Ordinal))
            .Select(file => (File: file, Relative: Path.GetRelativePath(root, file)))
            .Where(source => !IsBuildOutput(source.Relative) && !TypeLayer.Contains(source.Relative.Split(Path.DirectorySeparatorChar)[0]))
            .SelectMany(source => File.ReadLines(source.File)
                .Select((line, index) => (Line: line, Number: index + 1))
                .Where(line => FontNaming().IsMatch(line.Line))
                .Select(line => $"{source.Relative}:{line.Number}: {line.Line.Trim()}"))];

        Assert.True(hits.Length == 0, "A font named outside the type layer:\n" + string.Join('\n', hits));
    }

    private static bool IsBuildOutput(string relative)
    {
        string first = relative.Split(Path.DirectorySeparatorChar)[0];
        return first is "bin" or "obj";
    }
}
