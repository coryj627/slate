// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// W7-7 PR 0 (codex round 1): the executable spec and its contracts
// document agree on which contract belongs to which PR.
//
// The review protocol drives every codex round from the PR's `R-n`
// numbers, so a spec section that cites the wrong contract sends the
// review to the wrong invariant — and the first draft did exactly that
// from PR 4 onward (arrows cited R-4 instead of R-5, sheets R-5 instead
// of R-6, and so on down the list). A shifted citation is not a typo; it
// is a review that verifies nothing. The rule is mechanical: every `R-n`
// a PR section cites is owned by that PR in contracts 40, and every
// contract is cited by its owning section at least once.
//
// Codex round 2: a duplicate definition is as ambiguous as a shifted one
// (two `R-9` lines owned by different PRs, two `## n. PR 7` sections, two
// `### PR 7` records), so the parse rejects duplicates instead of letting
// the later one win, and the key sets are asserted exactly — thirteen
// contracts, eleven feature PRs, twelve review records — rather than
// floored.

using System.Text.RegularExpressions;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "w7-7-remediation-docs")]
public sealed partial class W77RemediationDocsCensus
{
    private const string SpecDoc = "18_windows_port/specs/w7_7_nvda_matrix_remediation_spec.md";
    private const string ContractsDoc = "40_nvda_matrix_remediation_contracts.md";

    private static readonly int[] ExpectedContracts = Enumerable.Range(1, 13).ToArray();
    private static readonly int[] ExpectedFeaturePrs = Enumerable.Range(1, 11).ToArray();
    private static readonly int[] ExpectedReviewRecords = Enumerable.Range(0, 12).ToArray();

    [Fact]
    public void TheDocumentsDefineExactlyTheWaveOnce()
    {
        Assert.Equal(ExpectedContracts, ContractOwners().Keys.Order().ToArray());
        Assert.Equal(ExpectedFeaturePrs, SpecSections().Keys.Order().ToArray());

        string contracts = ReadPlan(ContractsDoc);
        var records = ReviewRecordHeading().Matches(contracts)
            .Select(m => int.Parse(m.Groups[1].Value))
            .ToArray();
        Assert.Equal(records.Length, records.Distinct().Count());
        Assert.Equal(ExpectedReviewRecords, records.Order().ToArray());
    }

    [Fact]
    public void EveryContractIsCitedByItsOwningSpecSection()
    {
        var owners = ContractOwners();
        var sections = SpecSections();

        var missing = new List<string>();
        foreach ((int contract, int pr) in owners)
        {
            if (!sections.TryGetValue(pr, out var cited) || !cited.Contains(contract))
            {
                missing.Add($"R-{contract} (owned by PR {pr})");
            }
        }

        Assert.True(missing.Count == 0, "Contracts never cited by their owning spec section: " + string.Join(", ", missing));
    }

    [Fact]
    public void NoSpecSectionCitesAnotherPrsContract()
    {
        var owners = ContractOwners();
        var sections = SpecSections();

        var shifted = new List<string>();
        foreach ((int pr, var cited) in sections)
        {
            foreach (int contract in cited)
            {
                if (!owners.TryGetValue(contract, out int owner))
                {
                    shifted.Add($"PR {pr} cites R-{contract}, which contracts 40 does not define");
                }
                else if (owner != pr)
                {
                    shifted.Add($"PR {pr} cites R-{contract}, which belongs to PR {owner}");
                }
            }
        }

        Assert.True(shifted.Count == 0, "Shifted or undefined contract citations: " + string.Join("; ", shifted));
    }

    /// <summary>Contract number → owning PR, from the `**R-n — … (PR m, …)**` lines; a second definition of the same `R-n` fails the parse.</summary>
    private static Dictionary<int, int> ContractOwners()
    {
        string contracts = ReadPlan(ContractsDoc);
        var owners = new Dictionary<int, int>();
        foreach (Match m in ContractHeading().Matches(contracts))
        {
            int contract = int.Parse(m.Groups[1].Value);
            Assert.True(
                owners.TryAdd(contract, int.Parse(m.Groups[2].Value)),
                $"R-{contract} is defined twice in {ContractsDoc}.");
        }

        return owners;
    }

    /// <summary>
    /// PR number → the set of `R-n` the spec's section for that PR cites;
    /// a second section for the same PR fails the parse. The lookbehind
    /// keeps `AR-n` (accepted risks) and `TR-n` (contract 30's template
    /// rules) out of the count.
    /// </summary>
    private static Dictionary<int, HashSet<int>> SpecSections()
    {
        string spec = ReadPlan(SpecDoc);
        var sections = new Dictionary<int, HashSet<int>>();
        var headings = SectionHeading().Matches(spec);
        for (int i = 0; i < headings.Count; i++)
        {
            Match heading = headings[i];
            if (!heading.Groups[1].Success)
            {
                continue;
            }

            int start = heading.Index;
            int end = i + 1 < headings.Count ? headings[i + 1].Index : spec.Length;
            string body = spec.Substring(start, end - start);
            var cited = ContractCitation().Matches(body)
                .Select(m => int.Parse(m.Groups[1].Value))
                .ToHashSet();
            int pr = int.Parse(heading.Groups[1].Value);
            Assert.True(sections.TryAdd(pr, cited), $"PR {pr} has two sections in the spec.");
        }

        return sections;
    }

    private static string ReadPlan(string relative) =>
        File.ReadAllText(Path.Combine(SourceText.RepoRoot(), "docs", "plans", relative));

    [GeneratedRegex(@"^\*\*R-(\d+) — [^\n]*?\(PR (\d+)[,;)]", RegexOptions.Multiline)]
    private static partial Regex ContractHeading();

    // Every level-two heading terminates the previous section; only the
    // `## n. PR m · …` shape carries a PR number.
    [GeneratedRegex(@"^## (?:\d+\. PR (\d+) · )?[^\n]*", RegexOptions.Multiline)]
    private static partial Regex SectionHeading();

    [GeneratedRegex(@"(?<![A-Za-z])R-(\d+)")]
    private static partial Regex ContractCitation();

    [GeneratedRegex(@"^### PR (\d+) — ", RegexOptions.Multiline)]
    private static partial Regex ReviewRecordHeading();
}
