// SPDX-License-Identifier: MIT
using System.Text.RegularExpressions;
using Xunit;

namespace SlateWindows.Tests.Censuses;

/// <summary>§K (F2, IPJ-6-1): the W6-2 roll-up in <c>BENCHMARKS.md</c>
/// keeps its shape — the section heading, the six budgeted rows each
/// marked PASS with a number and a budget, and the end-to-end row's five
/// measures — so the evidence the owner reads cannot lose a row or a
/// verdict without the lane noticing (the lane now runs on the file).</summary>
public sealed class BenchmarksRollupCensus
{
    private const string Heading = "## Milestone W6-2 — graph through the C# binding — 2026-09-15 (#746)";

    private static readonly string[] BudgetedWorkloads =
    [
        "OpenToPublication", "OpenToPublication", "WarmTick", "FirstRebuild", "PanHop", "SpatialStep",
    ];

    private static readonly string[] EndToEndMeasures =
    [
        "Open to the installed publication", "Warm tick", "First rebuild", "Per-pan hop", "Per-step spatial traversal",
    ];

    private static string Section()
    {
        string text = File.ReadAllText(Path.Combine(SourceText.RepoRoot(), "BENCHMARKS.md"));
        int start = text.IndexOf(Heading, StringComparison.Ordinal);
        Assert.True(start >= 0, "BENCHMARKS.md has no W6-2 roll-up section");
        int end = text.IndexOf("\n## ", start + Heading.Length, StringComparison.Ordinal);
        return end < 0 ? text[start..] : text[start..end];
    }

    [Fact]
    public void TheSixBudgetedRowsEachPassWithANumberAndABudget()
    {
        string section = Section();
        var passes = Regex.Matches(section, @"^\| `(\w+)` \| (?:[\d,]+ \| )?\*\*[\d.]+ ms\*\* \| \d+ ms \| PASS \|", RegexOptions.Multiline)
            .Select(m => m.Groups[1].Value)
            .ToList();
        Assert.Equal(BudgetedWorkloads.OrderBy(w => w, StringComparer.Ordinal), passes.OrderBy(w => w, StringComparer.Ordinal));
        Assert.DoesNotContain("| FAIL |", section);
    }

    [Fact]
    public void TheEndToEndRowCarriesItsFiveMeasures()
    {
        string section = Section();
        foreach (string measure in EndToEndMeasures)
        {
            Assert.True(Regex.IsMatch(section, @"^\| " + Regex.Escape(measure) + @"[^|]*\| \*\*[\d.]+ ms\*\* \| \d+ ms \|", RegexOptions.Multiline),
                $"the end-to-end row lacks the measure '{measure}' with a number and a budget");
        }
    }
}
