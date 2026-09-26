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

    /// <summary>
    /// The issues each contract closes, fixed with the ownership (codex
    /// round 18): the owner clause's `#issue` list must match exactly, so
    /// a wrong or missing secondary issue cannot pass as a well-formed
    /// heading.
    /// </summary>
    private static readonly IReadOnlyDictionary<int, int[]> ExpectedIssues =
        new Dictionary<int, int[]>
        {
            [1] = [1244],
            [2] = [1245],
            [3] = [1250],
            [4] = [1246],
            [5] = [1247],
            [6] = [1248],
            [7] = [1249],
            [8] = [1251],
            [9] = [1252],
            [10] = [1253],
            [11] = [1254],
            [12] = [1255, 1256],
            [13] = [1257],
        };

    /// <summary>
    /// The issues a contract's body may cite beyond its own owner clause —
    /// a fixed allow-list, so an ownership statement cannot hide in the body
    /// that shares the heading's line (codex round 23).
    /// </summary>
    private static readonly IReadOnlyDictionary<int, int[]> AllowedCrossReferences =
        new Dictionary<int, int[]>
        {
            [6] = [1118],
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
    public void EveryContractNamesExactlyItsIssues()
    {
        string contracts = ReadPlan(ContractsDoc);
        var issues = new Dictionary<int, int[]>();
        foreach (Match m in ContractHeading().Matches(contracts))
        {
            int contract = int.Parse(m.Groups[1].Value);
            Assert.True(issues.TryAdd(contract, IssueList(m.Groups[3].Value)), $"R-{contract} is defined twice in {ContractsDoc}.");
        }

        Assert.Equal(
            ExpectedIssues.OrderBy(pair => pair.Key).Select(pair => $"R-{pair.Key}→#{string.Join(", #", pair.Value)}"),
            issues.OrderBy(pair => pair.Key).Select(pair => $"R-{pair.Key}→#{string.Join(", #", pair.Value)}"));
    }

    /// <summary>
    /// A contract's body shares its heading's line and may be reflowed onto
    /// the lines after it, so the heading cannot be anchored to the line's
    /// end; the whole contract paragraph is policed instead (codex rounds 23
    /// and 25): it names exactly one `PR n` — its owner — and no issue
    /// outside its owner clause except the fixed cross-reference allow-list.
    /// </summary>
    [Fact]
    public void EveryContractParagraphNamesOnlyItsOwnPrAndIssues()
    {
        var defects = new List<string>();
        foreach ((Match heading, string paragraph) in ContractParagraphs(ReadPlan(ContractsDoc)))
        {
            if (ContractParagraphDefect(paragraph, heading) is { } defect)
            {
                defects.Add($"R-{heading.Groups[1].Value} {defect}");
            }
        }

        Assert.True(defects.Count == 0, "Contract paragraphs naming more than their owner: " + string.Join("; ", defects));
    }

    [Theory]
    [InlineData("**R-1 — Title (PR 1, #1244).** Body.\nContinued ownership: PR 9, #9999.")]
    [InlineData("**R-1 — Title (PR 1, #1244).** Body.\nContinued ownership: PR\n9.")]
    [InlineData("**R-1 — Title (PR 1, #1244).** Body.\nContinued ownership: PR [9](https://example.test/pull/9).")]
    [InlineData("**R-1 — Title (PR 1, #1244).** Body.\nContinued ownership: PR *9*.")]
    [InlineData("**R-1 — Title (PR 1, #1244).** Body.\nShared with PRs 9 and 10.")]
    [InlineData("**R-1 — Title (PR 1, #1244).** Body.\nAlso #1244.")]
    [InlineData("**R-1 — Title (PR 1, #1244).** Body.\nnaming #9999.")]
    [InlineData("**R-1 — Title (PR 1, #1244).** Body.\n#9999 is named too.")]
    [InlineData("**R-6 — Title (PR 5, #1248).** Body.\nciting #1118x.")]
    public void AReflowedBodyIsCheckedAsAWhole(string document)
    {
        var (heading, paragraph) = Assert.Single(ContractParagraphs(document));
        Assert.NotNull(ContractParagraphDefect(paragraph, heading));
    }

    [Fact]
    public void AContractParagraphEndsAtABlankLineOrAHeading()
    {
        var paragraphs = ContractParagraphs(
            "**R-1 — Title (PR 1, #1244).** Body.\n\nUnrelated PR 9, #9999.\n**R-6 — Title (PR 5, #1248).** Body.\n## Next PR 9, #9999")
            .ToList();
        Assert.Equal(2, paragraphs.Count);
        Assert.All(paragraphs, p => Assert.Null(ContractParagraphDefect(p.Paragraph, p.Heading)));
    }

    /// <summary>Each strict contract heading with its complete paragraph: the
    /// heading's line and every following line up to a blank line, the next
    /// heading-shaped line or a Markdown heading (codex round 25: a reflowed
    /// body keeps its later lines under the check).</summary>
    private static IEnumerable<(Match Heading, string Paragraph)> ContractParagraphs(string document)
    {
        string[] lines = document.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            Match heading = ContractHeading().Match(lines[i]);
            if (!heading.Success)
            {
                continue;
            }

            var paragraph = new List<string> { lines[i] };
            for (int j = i + 1;
                j < lines.Length
                    && !string.IsNullOrWhiteSpace(lines[j])
                    && !LooseContractHeading().IsMatch(lines[j])
                    && !MarkdownHeading().IsMatch(lines[j]);
                j++)
            {
                paragraph.Add(lines[j]);
            }

            yield return (heading, string.Join('\n', paragraph));
        }
    }

    [Theory]
    [InlineData("**R-1 — Title (PR 1, #1244).** Body with an extra owner PR 9, #9999.")]
    [InlineData("**R-1 — Title (PR 1, #1244).** Body naming #9999.")]
    [InlineData("**R-1 — Title (PR 1, #1244).** Body naming #9999x.")]
    [InlineData("**R-1 — Title (PR 1, #1244).** Body repeating #1244.")]
    [InlineData("**R-6 — Title (PR 5, #1248).** Body citing #1118x.")]
    [InlineData("**R-6 — Title (PR 5, #1248).** Body citing #1118².")]
    [InlineData("**R-6 — Title (PR 5, #1248).** Body citing #1118\U0001D4B3.")]
    public void AnOwnershipStatementAfterTheHeadingIsCaught(string line)
    {
        Match heading = ContractHeading().Match(line);
        Assert.True(heading.Success);
        Assert.NotNull(ContractParagraphDefect(line, heading));
    }

    /// <summary>The allow-list is what a body MAY cite (codex round 24): a
    /// listed reference is optional, never required.</summary>
    [Theory]
    [InlineData("**R-6 — Title (PR 5, #1248).** Body citing #1118.")]
    [InlineData("**R-6 — Title (PR 5, #1248).** Body citing nothing.")]
    public void AContractLineCitingOnlyItsAllowedCrossReferenceIsClean(string line)
    {
        Match heading = ContractHeading().Match(line);
        Assert.True(heading.Success);
        Assert.Null(ContractParagraphDefect(line, heading));
    }

    /// <summary>The malformed-heading mutations, kept as parser tests (codex
    /// round 23): the loose detector sees each line and the strict parse
    /// refuses it, so the document-level loose/strict fact would fail.</summary>
    [Theory]
    [InlineData("**R-1 — Title (PR 1, #1244x).** Body.")]
    [InlineData("**R-1 — Title (PR 1, #1244é).** Body.")]
    [InlineData("**R-1 — Title (PR 1, #1244²).** Body.")]
    [InlineData("**R-1 — Title (PR 1, #1244\U0001D4B3).** Body.")]
    [InlineData("**R-1 — Title #1299 (PR 1, #1244).** Body.")]
    [InlineData("**R-1 — Title (PR 1, #1244; see #1299).** Body.")]
    [InlineData("**R-1 – Title (PR 1, #1244).** Body.")]
    [InlineData("**R-1 — Title (PR 1).** Body.")]
    [InlineData("**R-1 — Title (PR 1 nonsense).** Body.")]
    [InlineData("**R-1 — Title **bold** (PR 1, #1244).** Body.")]
    public void TheStrictParseRefusesAMalformedContractHeading(string line)
    {
        Assert.Single(LooseContractHeading().Matches(line));
        Assert.Empty(ContractHeading().Matches(line));
    }

    [Theory]
    [InlineData("## 2. PR 1 · #1244x #1244 — title", new[] { 1244 })]
    [InlineData("## 2. PR 1 · #1244é — title", new[] { 1244 })]
    [InlineData("## 2. PR 1 · #1244² — title", new[] { 1244 })]
    [InlineData("## 2. PR 1 · #1244\U0001D4B3 — title", new[] { 1244 })]
    [InlineData("## 2. PR 1 · #1244 + #1244 — title", new[] { 1244 })]
    [InlineData("### PR 1 — #1244 #1244x announcements", new[] { 1244 })]
    [InlineData("### PR 0 — docs #9999 (this document)", new int[0])]
    public void AHeadingWhoseIssuesDriftIsCaught(string heading, int[] expected) =>
        Assert.False(IssueTokens(heading).SequenceEqual(expected));

    /// <summary>A token glued to a letter, digit or number character on
    /// either side is not a citation of the contract it resembles (codex
    /// round 24), so replacing a section's citations with such forms leaves
    /// the contract uncited and the owning-section fact fails.</summary>
    [Theory]
    [InlineData("see AR-6 here")]
    [InlineData("see TR-6 here")]
    [InlineData("see R-6x here")]
    [InlineData("see _R-6 here")]
    [InlineData("see R-6é here")]
    [InlineData("see éR-6 here")]
    [InlineData("see R-6² here")]
    [InlineData("see R-6\U0001D4B3 here")]
    [InlineData("see \U0001D4B3R-6 here")]
    public void ATokenGluedToAnIdentifierIsNotACitation(string text) =>
        Assert.Empty(ContractCitation().Matches(text));

    [Theory]
    [InlineData("see R-6 here")]
    [InlineData("(R-6)")]
    [InlineData("R-6, R-7")]
    [InlineData("R-6.")]
    [InlineData("R-6’s rule")]
    public void AWellBoundedTokenIsACitation(string text) =>
        Assert.Equal("6", ContractCitation().Matches(text)[0].Groups[1].Value);

    [Fact]
    public void ACleanHeadingYieldsExactlyItsIssues() =>
        Assert.Equal(new[] { 1245, 1250 }, IssueTokens("## 3. PR 2 · #1245 + #1250 — Files sidebar"));

    /// <summary>
    /// What a contract paragraph names beyond its owner: a count of `PR n`
    /// tokens other than one, a malformed issue token anywhere outside the
    /// owner clause, or a well-formed one the allow-list does not name. Only
    /// the owner clause's own span is exempt — the same issue repeated in the
    /// body is outside it (codex round 24). The paragraph starts with the
    /// heading's line, so the heading's spans index it directly. Null when
    /// the paragraph is clean.
    /// </summary>
    private static string? ContractParagraphDefect(string paragraph, Match heading)
    {
        // Everything but the owner clause itself: no `PR n` token, and no
        // standalone `PR` / `PRs` word either, since `PR [9](…)` or `PR *9*`
        // names a PR without forming a token (codex round 27).
        Group clause = heading.Groups["clause"];
        string outside = string.Concat(paragraph.AsSpan(0, clause.Index), paragraph.AsSpan(clause.Index + clause.Length));
        if (PrMarker().Match(outside) is { Success: true } marker)
        {
            return $"names a PR outside its owner clause (`{marker.Value}`)";
        }

        if (MalformedIssueToken().Match(outside) is { Success: true } malformed)
        {
            return $"carries the malformed issue token `{malformed.Value}`";
        }

        int contract = int.Parse(heading.Groups[1].Value);
        int[] allowed = AllowedCrossReferences.GetValueOrDefault(contract, []);
        int[] unexpected = IssueToken().Matches(outside)
            .Select(m => int.Parse(m.Groups[1].Value))
            .Where(issue => !allowed.Contains(issue))
            .Distinct()
            .Order()
            .ToArray();
        return unexpected.Length == 0
            ? null
            : $"names #{string.Join(", #", unexpected)} outside its owner clause";
    }

    private static int[] IssueList(string ownerList) =>
        ownerList
            .Split(',', StringSplitOptions.TrimEntries)
            .Select(token => int.Parse(token.TrimStart('#')))
            .ToArray();

    /// <summary>
    /// The issues a PR's spec heading (`## n. PR m · #a + #b — …`) and its
    /// review-record heading (`### PR m — #a + #b …`) display are exactly
    /// the union of the issues its contracts close (codex round 19), so a
    /// heading cannot drift from the ownership the census fixes.
    /// </summary>
    [Fact]
    public void EveryPrHeadingNamesItsContractsIssues()
    {
        var expected = ExpectedOwners
            .GroupBy(pair => pair.Value)
            .ToDictionary(
                group => group.Key,
                group => group.SelectMany(pair => ExpectedIssues[pair.Key]).Distinct().Order().ToArray());

        var specHeadings = SectionHeading().Matches(ReadPlan(SpecDoc))
            .Where(m => m.Groups[1].Success)
            .ToDictionary(m => int.Parse(m.Groups[1].Value), m => IssueTokens(m.Value));
        var recordHeadings = ReviewRecordHeading().Matches(ReadPlan(ContractsDoc))
            .ToDictionary(m => int.Parse(m.Groups[1].Value), m => IssueTokens(m.Value));

        // PR 0 (the docs PR) owns no contract and closes no issue, yet its
        // record heading is required: it is held to an explicit empty set
        // (codex round 20).
        expected[0] = [];

        var drifted = new List<string>();
        foreach ((int pr, int[] issues) in expected.OrderBy(pair => pair.Key))
        {
            if (pr != 0)
            {
                specHeadings.TryGetValue(pr, out int[]? specIssues);
                if (specIssues is null || !specIssues.SequenceEqual(issues))
                {
                    drifted.Add($"spec heading for PR {pr} shows #{string.Join(", #", specIssues ?? [])}, expected #{string.Join(", #", issues)}");
                }
            }

            recordHeadings.TryGetValue(pr, out int[]? recordIssues);
            if (recordIssues is null || !recordIssues.SequenceEqual(issues))
            {
                drifted.Add($"record heading for PR {pr} shows #{string.Join(", #", recordIssues ?? [])}, expected #{string.Join(", #", issues)}");
            }
        }

        Assert.True(drifted.Count == 0, "PR headings whose issues drift from their contracts: " + string.Join("; ", drifted));
    }

    // Every token counts — a duplicated issue in a heading is a drift too
    // (codex round 20), so no Distinct() here — and an issue-shaped
    // substring the grammar does not fully consume (`#1244x` beside a valid
    // `#1244`) makes the whole heading drift instead of vanishing (codex
    // round 22): the sentinel never equals an expected set.
    private static int[] IssueTokens(string headingLine) =>
        MalformedIssueToken().IsMatch(headingLine)
            ? [-1]
            : IssueToken().Matches(headingLine).Select(m => int.Parse(m.Groups[1].Value)).Order().ToArray();

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
    // owner clause in parentheses — `(PR n, #issue[, #issue]…` with the
    // issue list captured (codex rounds 17 and 18) — and the heading's
    // closing `.**`: a full stop then the bold delimiter, followed by
    // whitespace, with no other asterisk before it, so an interior bold
    // span cannot pose as the closing delimiter (codex rounds 6 and 7).
    // No `#issue` token anywhere outside the owner clause's canonical list
    // — not in its suffix, not in the title, not after the clause — so an
    // extra issue cannot hide anywhere in the heading (codex rounds 19–20).
    // Every issue token ends at a Unicode identifier boundary, so `#1244x`
    // and `#1244é` are malformed headings rather than issue 1244 (codex
    // rounds 21–22).
    // Round 23: the issue list must be followed by `;` or `)`, so any other
    // character after the digits — a letter, a superscript, a supplementary
    // character — refuses the heading.
    // The named `clause` group spans the whole owner clause, `(PR n, …)`;
    // being named, it leaves the numbered groups (R-n, PR n, the issue
    // list) where they were (codex round 27).
    [GeneratedRegex(@"^\*\*R-(\d+) — [^\n*#]*?(?<clause>\(PR (\d+), (#\d+(?:, #\d+)*)(?=[;)])[^)\n*#]*\))[^\n*#]*?\.\*\*(?=\s)", RegexOptions.Multiline)]
    private static partial Regex ContractHeading();

    // Anything that starts a line like a contract definition, however it
    // is indented or punctuated: the strict parse must account for every
    // one of them.
    [GeneratedRegex(@"^[ \t]{0,3}\*\*[ \t]*R\p{Pd}\d+", RegexOptions.Multiline)]
    private static partial Regex LooseContractHeading();

    // Every level-two heading, however indented or spaced (CommonMark allows
    // up to three leading spaces), terminates the previous section; only the
    // canonical `## n. PR m · …` shape carries a PR number.
    [GeneratedRegex(@"^[ \t]{0,3}##[ \t]+(?:(?<=^## )\d+\. PR (\d+) · )?[^\n]*", RegexOptions.Multiline)]
    private static partial Regex SectionHeading();

    // Anything that starts a heading like a PR section, however it is
    // indented, spaced or punctuated after the PR number.
    [GeneratedRegex(@"^[ \t]{0,3}##[ \t]+\d+\.[ \t]+PR[ \t]+\d+\b", RegexOptions.Multiline)]
    private static partial Regex LoosePrSectionHeading();

    // Unicode-aware boundaries on both sides (codex round 24): a letter,
    // digit, connector, other-number or letter-number character, or a
    // surrogate (half of a supplementary character) next to `R-n` makes it
    // a different token — `AR-6`, `TR-6`, `R-6x`, `R-6é`, `éR-6`, `R-6²`
    // and `R-6` beside a supplementary letter are none of them citations.
    [GeneratedRegex(@"(?<![\w\p{No}\p{Nl}\p{Cs}])R-(\d+)(?![\w\p{No}\p{Nl}\p{Cs}])")]
    private static partial Regex ContractCitation();

    [GeneratedRegex(@"^### PR (\d+) — [^\n]*", RegexOptions.Multiline)]
    private static partial Regex ReviewRecordHeading();

    // A token is `#` plus digits ending at whitespace, `,`, `;`, `:`, `.`,
    // `)` or the end of the line (codex rounds 21–23): an allow-list of
    // terminators, so `#1244x`, `#1244é`, `#1244²` and a supplementary
    // character after the digits are all malformed.
    [GeneratedRegex(@"#(\d+)(?=[\s,;:.)]|$)")]
    private static partial Regex IssueToken();

    // An issue-shaped substring whose digits are followed by anything but a
    // digit or an allowed terminator. The digit in the negated class stops
    // the digits backtracking into a false "malformed" match on `#1244`.
    [GeneratedRegex(@"#\d+(?![\d\s,;:.)]|$)")]
    private static partial Regex MalformedIssueToken();

    // Any standalone `PR` or `PRs` word — with or without a number after
    // it, across a soft wrap, a link or emphasis — so a formatted foreign
    // reference cannot hide outside the owner clause (codex rounds 26–27).
    [GeneratedRegex(@"(?<![A-Za-z0-9_])PRs?(?![A-Za-z0-9_])")]
    private static partial Regex PrMarker();

    // A Markdown ATX heading line, which ends a contract paragraph. `#` must
    // be followed by a space or the line end, so a continuation line that
    // starts with `#1244` stays inside the paragraph.
    [GeneratedRegex(@"^[ \t]{0,3}#{1,6}(?:[ \t]|$)")]
    private static partial Regex MarkdownHeading();

    // Anything that starts a heading like a review record, however it is
    // indented, spaced or punctuated after the PR number.
    [GeneratedRegex(@"^[ \t]{0,3}###[ \t]+PR[ \t]+\d+\b", RegexOptions.Multiline)]
    private static partial Regex LooseReviewRecordHeading();
}
