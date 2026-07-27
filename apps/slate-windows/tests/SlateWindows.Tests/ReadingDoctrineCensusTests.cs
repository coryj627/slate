// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.RegularExpressions;

namespace SlateWindows.Tests;

/// <summary>
/// The §W-G doctrine census for the reading surface, as
/// `w3_inline_runs_spec.md` §10.8 requires: the boundary is
/// machine-checked, not review-checked. Every semantic decision on this
/// path is core's; a match below means C# has started re-deciding one.
/// </summary>
[Trait("gate", "W-G")]
public sealed class ReadingDoctrineCensusTests
{
    private static string ReadingDirectory =>
        Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "SlateWindows", "Reading"));

    /// <summary>
    /// (pattern, why it is forbidden). Patterns are matched against
    /// comment-stripped source so prose explaining the rule cannot trip
    /// it.
    /// </summary>
    private static readonly (string Pattern, string Why)[] Forbidden =
    {
        ("Markdig", "no C# markdown parsing on this path (§10.8)"),
        (@"\[\[", "wikilink syntax knowledge is core's (§10.8)"),
        (@"\]\]", "wikilink syntax knowledge is core's (§10.8)"),
        (@"!\[", "embed/image syntax knowledge is core's (§10.8)"),
        ("candidateKey", "candidate-key derivation is core's ReadingMatchLink (§10.3)"),
        (@"IndexOf\('#'\)", "anchor cutting is core's per-grammar logic (§10.3)"),
        (@"IndexOf\('\^'\)", "anchor cutting is core's per-grammar logic (§10.3)"),
        (@"""https?""", "scheme allowlists decide activability core-side (§10.3)"),
        (@"""javascript""", "never-activatable classification is core's (§10.3)"),
        (@"""Unresolved link""", "AX strings arrive as core AxText, never composed (§10.8)"),
        (@"Segments\[0\]\.Content\.Substring", "run slicing is byte-offset based (§10.2)"),
    };

    public static IEnumerable<object[]> ReadingSources() =>
        Directory.EnumerateFiles(ReadingDirectory, "*.cs")
            .Select(path => new object[] { Path.GetFileName(path) });

    [Theory]
    [MemberData(nameof(ReadingSources))]
    public void ReadingSurfaceContainsNoRederivedSemantics(string fileName)
    {
        string source = File.ReadAllText(Path.Combine(ReadingDirectory, fileName));
        string code = StripComments(source);

        foreach ((string pattern, string why) in Forbidden)
        {
            Assert.False(
                Regex.IsMatch(code, pattern),
                $"{fileName} matches forbidden pattern `{pattern}` — {why}");
        }
    }

    [Fact]
    public void CensusScansTheRealDirectory()
    {
        // The census is itself census-checked: if the Reading folder
        // moves and this path silently matches nothing, every theory
        // above passes vacuously.
        Assert.True(
            Directory.Exists(ReadingDirectory),
            $"Reading source directory not found at {ReadingDirectory}");
        Assert.True(
            Directory.EnumerateFiles(ReadingDirectory, "*.cs").Count() >= 4,
            "expected at least the four W3-1 reading sources");
    }

    [Fact]
    public void ForbiddenPatternsCanActuallyMatch()
    {
        // Prove the detector is live: each pattern must hit a synthetic
        // violation, or a regex typo silently disables the census.
        string[] violations =
        {
            "using Markdig;",
            "if (text.Contains(\"[[\"))",
            "if (text.Contains(\"]]\"))",
            "if (text.StartsWith(\"![\"))",
            "var candidateKeys = candidateKey(target);",
            "target.IndexOf('#')",
            "target.IndexOf('^')",
            "scheme == \"https\"",
            "scheme == \"javascript\"",
            "SetHelpText(link, \"Unresolved link\");",
            "segment.Segments[0].Content.Substring(2)",
        };
        foreach ((string pattern, _) in Forbidden)
        {
            Assert.True(
                violations.Any(violation => Regex.IsMatch(violation, pattern)),
                $"pattern `{pattern}` matched none of the synthetic violations — it is inert");
        }
    }

    private static string StripComments(string source)
    {
        source = Regex.Replace(source, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        return Regex.Replace(source, @"//[^\r\n]*", string.Empty);
    }
}
