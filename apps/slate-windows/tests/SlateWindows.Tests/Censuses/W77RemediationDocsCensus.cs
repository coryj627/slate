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
// Codex round 2: a duplicate definition is as ambiguous as a shifted one,
// so the parse rejects duplicates instead of letting the later one win.
// Codex round 3: the contracts document is mutable, so it cannot be the
// only oracle — the R → PR map is fixed HERE, and every heading-shaped
// line must match the strict parse so a look-alike definition cannot
// vanish from the count. Codex round 5: the look-alike detectors are
// CommonMark-tolerant (up to three leading spaces, any run of spaces
// after the marker), the strict contract form requires its closing bold
// on the same line, and a citation needs a boundary on BOTH sides so
// `R-10x` and `_R-10` are neither citations nor near-misses. Fenced-code
// headings and negated prose are not distinguished (AR-11): neither
// document carries either, and a fenced heading would still have to
// match the strict form to count.

using System.Text.RegularExpressions;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "w7-7-remediation-docs")]
public sealed partial class W77RemediationDocsCensus
{
    private const string SpecDoc = "18_windows_port/specs/w7_7_nvda_matrix_remediation_spec.md";
    private const string ContractsDoc = "40_nvda_matrix_remediation_contracts.md";

    /// <summary>
    /// The wave's ownership, fixed at the docs PR: contract → PR. A
    /// renumbering is a deliberate edit to this table and the two
    /// documents together, never to the documents alone.
    /// </summary>
    private static readonly IReadOnlyDictionary<int, int> ExpectedOwners =
        new Dictionary<int, int>
        {
            [1] = 1,
            [2] = 2,
            [3] = 2,
            [4] = 3,
            [5] = 4,
            [6] = 5,
            [7] = 6,
            [8] = 6,
            [9] = 7,
            [10] = 8,
            [11] = 9,
            [12] = 10,
            [13] = 11,
        };

    private static readonly int[] ExpectedFeaturePrs = Enumerable.Range(1, 11).ToArray();
    private static readonly int[] ExpectedReviewRecords = Enumerable.Range(0, 12).ToArray();

    [Fact]
    public void TheContractsDocumentDefinesExactlyTheFixedOwnership()
    {
        var owners = ContractOwners();
        Assert.Equal(
            ExpectedOwners.OrderBy(pair => pair.Key).Select(pair => $"R-{pair.Key}→PR {pair.Value}"),
            owners.OrderBy(pair => pair.Key).Select(pair => $"R-{pair.Key}→PR {pair.Value}"));
    }

    [Fact]
    public void TheSpecHasOneSectionPerFeaturePrAndTheRecordOneSectionPerPr()
    {
        Assert.Equal(ExpectedFeaturePrs, SpecSections().Keys.Order().ToArray());

        string contracts = ReadPlan(ContractsDoc);
        var records = ReviewRecordHeading().Matches(contracts)
            .Select(m => int.Parse(m.Groups[1].Value))
            .ToArray();
        Assert.Equal(records.Length, records.Distinct().Count());
        Assert.Equal(ExpectedReviewRecords, records.Order().ToArray());
    }

    [Fact]
    public void EveryHeadingShapedLineMatchesTheStrictParse()
    {
        string contracts = ReadPlan(ContractsDoc);
        int looseContracts = LooseContractHeading().Matches(contracts).Count;
        int strictContracts = ContractHeading().Matches(contracts).Count;
        Assert.True(
            looseContracts == strictContracts,
            $"{looseContracts - strictContracts} contract-shaped line(s) in {ContractsDoc} do not parse as `**R-n — … (PR m, …) …**`.");

        int looseRecords = LooseReviewRecordHeading().Matches(contracts).Count;
        int strictRecords = ReviewRecordHeading().Matches(contracts).Count;
        Assert.True(
            looseRecords == strictRecords,
            $"{looseRecords - strictRecords} review-record heading(s) in {ContractsDoc} do not parse as `### PR n — …`.");

        string spec = ReadPlan(SpecDoc);
        int loosePrSections = LoosePrSectionHeading().Matches(spec).Count;
        int strictPrSections = SectionHeading().Matches(spec).Count(m => m.Groups[1].Success);
        Assert.True(
            loosePrSections == strictPrSections,
            $"{loosePrSections - strictPrSections} PR-section heading(s) in the spec do not parse as `## n. PR m · …`.");
    }

    [Fact]
    public void EveryContractIsCitedByItsOwningSpecSection()
    {
        var sections = SpecSections();

        var missing = new List<string>();
        foreach ((int contract, int pr) in ExpectedOwners)
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
        var sections = SpecSections();

        var shifted = new List<string>();
        foreach ((int pr, var cited) in sections)
        {
            foreach (int contract in cited)
            {
                if (!ExpectedOwners.TryGetValue(contract, out int owner))
                {
                    shifted.Add($"PR {pr} cites R-{contract}, which the wave does not define");
                }
                else if (owner != pr)
                {
                    shifted.Add($"PR {pr} cites R-{contract}, which belongs to PR {owner}");
                }
            }
        }

        Assert.True(shifted.Count == 0, "Shifted or undefined contract citations: " + string.Join("; ", shifted));
    }

    /// <summary>Contract number → owning PR, from the `**R-n — … (PR m, …) …**` lines; a second definition of the same `R-n` fails the parse.</summary>
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
    /// a second section for the same PR fails the parse. The two-sided
    /// boundary keeps `AR-n` (accepted risks), `TR-n` (contract 30's
    /// template rules) and malformed tokens out of the count.
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

    // The canonical definition: bold from column zero, an em dash, the
    // owner in parentheses, and the heading's closing `.**` — a full stop
    // then the bold delimiter, followed by whitespace — with no other
    // asterisk before it, so an interior bold span cannot pose as the
    // closing delimiter (codex rounds 6 and 7).
    [GeneratedRegex(@"^\*\*R-(\d+) — [^\n*]*?\(PR (\d+)[,;)][^\n*]*?\.\*\*(?=\s)", RegexOptions.Multiline)]
    private static partial Regex ContractHeading();

    // Anything that starts a line like a contract definition, however it
    // is indented or punctuated: the strict parse must account for every
    // one of them.
    [GeneratedRegex(@"^[ \t]{0,3}\*\*[ \t]*R\p{Pd}\d+", RegexOptions.Multiline)]
    private static partial Regex LooseContractHeading();

    // Every canonical level-two heading terminates the previous section;
    // only the `## n. PR m · …` shape carries a PR number.
    [GeneratedRegex(@"^## (?:\d+\. PR (\d+) · )?[^\n]*", RegexOptions.Multiline)]
    private static partial Regex SectionHeading();

    // Anything that starts a heading like a PR section, however it is
    // indented, spaced or punctuated after the PR number.
    [GeneratedRegex(@"^[ \t]{0,3}##[ \t]+\d+\.[ \t]+PR[ \t]+\d+\b", RegexOptions.Multiline)]
    private static partial Regex LoosePrSectionHeading();

    [GeneratedRegex(@"(?<![A-Za-z0-9_])R-(\d+)(?![A-Za-z0-9_])")]
    private static partial Regex ContractCitation();

    [GeneratedRegex(@"^### PR (\d+) — ", RegexOptions.Multiline)]
    private static partial Regex ReviewRecordHeading();

    // Anything that starts a heading like a review record, however it is
    // indented, spaced or punctuated after the PR number.
    [GeneratedRegex(@"^[ \t]{0,3}###[ \t]+PR[ \t]+\d+\b", RegexOptions.Multiline)]
    private static partial Regex LooseReviewRecordHeading();
}
