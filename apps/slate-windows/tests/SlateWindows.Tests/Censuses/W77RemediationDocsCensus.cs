// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// W7-7 PR 0: the executable spec and its contracts document agree on which
// contract belongs to which PR, and the review protocol routes every codex
// round by those numbers — so a spec section that cites the wrong contract
// sends the review to the wrong invariant. The first draft did exactly that
// from PR 4 onward (codex round 1), which is why this census exists.
//
// The routing is fixed HERE, as a registry of exact heading strings. The
// earlier form of this file parsed the headings out of the Markdown with a
// grammar, and thirty adversarial rounds showed that a parser of free-form
// Markdown can always be fed one more formatting variant (Unicode suffixes,
// soft wraps, links, emphasis, blank-line reflows — codex rounds 17–30). A
// registry has no such surface: a heading either equals its registered text
// or the census fails, and nothing about ownership is inferred from prose.
// What the prose may still say beside a heading is not policed (AR-17).
//
// The one remaining parse is the spec's contract citations (`R-n` tokens in
// each PR section), which the routing needs; it reads normalized text with
// Unicode-aware boundaries and is pinned by theories below.

using System.Text.RegularExpressions;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "w7-7-remediation-docs")]
public sealed partial class W77RemediationDocsCensus
{
    private const string SpecDoc = "18_windows_port/specs/w7_7_nvda_matrix_remediation_spec.md";
    private const string ContractsDoc = "40_nvda_matrix_remediation_contracts.md";

    private sealed record Contract(int Number, int Pr, string Heading);

    private sealed record FeaturePr(int Number, string? SpecHeading, string RecordHeading);

    /// <summary>The wave's contracts: number, owning PR, and the heading's
    /// exact text as the contracts document must carry it (its body follows
    /// on the same line). A renumbering is a deliberate edit to this table
    /// and the documents together, never to the documents alone.</summary>
    private static readonly Contract[] Contracts =
    [
        new(1, 1, "**R-1 — Announcements reach a listening client from the first frame (PR 1, #1244).**"),
        new(2, 2, "**R-2 — Keyboard selection never takes focus out of the Files tree (PR 2, #1245).**"),
        new(3, 2, "**R-3 — Tag activation composes core's grammar (PR 2, #1250).**"),
        new(4, 3, "**R-4 — Every reachable item is named; layout containers are not control elements (PR 3, #1246).**"),
        new(5, 4, "**R-5 — Arrow keys stay in their region (PR 4, #1247).**"),
        new(6, 5, "**R-6 — A sheet fences Tab (PR 5, #1248).**"),
        new(7, 6, "**R-7 — A failed save speaks a sentence, never a diagnostic (PR 6, #1249; amends contract 38 D-10).**"),
        new(8, 6, "**R-8 — A popover or sheet announces its outcome when it opens (PR 6, #1251).**"),
        new(9, 7, "**R-9 — A rescan is the reconciliation, and it says what it found (PR 7, #1252; amends contract 38 D-3).**"),
        new(10, 8, "**R-10 — The reading surface is the editor stop (PR 8, #1253).**"),
        new(11, 9, "**R-11 — The palette announces one selection per query change and renders at typing speed (PR 9, #1254).**"),
        new(12, 10, "**R-12 — The board's arrows move the seat and its cards have a menu (PR 10, #1255, #1256; amends contract 34 D15 and lifts G2D-12).**"),
        new(13, 11, "**R-13 — The Connections leaf documents its activation truthfully (PR 11, #1257).**"),
    ];

    /// <summary>Every PR of the wave with its exact spec-section heading (PR 0,
    /// the docs PR, has none) and its exact review-record heading.</summary>
    private static readonly FeaturePr[] Prs =
    [
        new(0, null, "### PR 0 — docs (this document and the spec)"),
        new(1, "## 2. PR 1 · #1244 — announcements from launch", "### PR 1 — #1244 announcements from launch"),
        new(2, "## 3. PR 2 · #1245 + #1250 — Files sidebar: tree keys and the tag filter", "### PR 2 — #1245 + #1250 Files sidebar"),
        new(3, "## 4. PR 3 · #1246 — accessible names for every item", "### PR 3 — #1246 accessible names"),
        new(4, "## 5. PR 4 · #1247 — arrows never leave the region", "### PR 4 — #1247 arrows stay in the region"),
        new(5, "## 6. PR 5 · #1248 — sheets fence the keyboard", "### PR 5 — #1248 sheet keyboard fence"),
        new(6, "## 7. PR 6 · #1249 + #1251 — what a failed save and a popover say", "### PR 6 — #1249 + #1251 failed-save sentence, popover outcomes"),
        new(7, "## 8. PR 7 · #1252 — files created outside Slate appear (OD-1)", "### PR 7 — #1252 rescan on Refresh and foreground"),
        new(8, "## 9. PR 8 · #1253 — reading view is the editor stop", "### PR 8 — #1253 reading surface is the editor stop"),
        new(9, "## 10. PR 9 · #1254 — the palette answers at typing speed", "### PR 9 — #1254 palette selection and speed"),
        new(10, "## 11. PR 10 · #1255 + #1256 — the canvas board", "### PR 10 — #1255 + #1256 canvas board arrows and menus"),
        new(11, "## 12. PR 11 · #1257 — the Connections leaf says what Enter does", "### PR 11 — #1257 Connections leaf documentation"),
    ];

    /// <summary>The registry agrees with itself: every contract's heading
    /// names its own number and owner, every PR heading names its own number,
    /// and the issues a PR's headings display are exactly the union of the
    /// issues its contracts' owner clauses name.</summary>
    [Fact]
    public void TheRegistryIsConsistent()
    {
        foreach (Contract c in Contracts)
        {
            Assert.StartsWith($"**R-{c.Number} — ", c.Heading);
            Assert.Contains($"(PR {c.Pr}, #", c.Heading);
            Assert.Contains(c.Pr, Prs.Select(p => p.Number));
        }

        foreach (FeaturePr pr in Prs)
        {
            int[] owned = Contracts.Where(c => c.Pr == pr.Number).SelectMany(c => IssuesOf(c)).Distinct().Order().ToArray();
            if (pr.SpecHeading is { } spec)
            {
                Assert.Matches($@"^## \d+\. PR {pr.Number} · ", spec);
                Assert.Equal(owned, IssueNumbers(spec));
            }

            Assert.StartsWith($"### PR {pr.Number} — ", pr.RecordHeading);
            Assert.Equal(owned, IssueNumbers(pr.RecordHeading));
        }
    }

    [Fact]
    public void EveryContractHeadingIsItsRegisteredTextExactlyOnce()
    {
        string[] lines = Lines(ReadPlan(ContractsDoc));
        var defects = new List<string>();
        foreach (Contract c in Contracts)
        {
            int n = lines.Count(line => IsHeadingLine(line, c.Heading));
            if (n != 1)
            {
                defects.Add($"R-{c.Number} appears {n} times as its registered heading");
            }
        }

        int shaped = lines.Count(line => LooseContractHeading().IsMatch(line));
        if (shaped != Contracts.Length)
        {
            defects.Add($"{shaped} contract-shaped lines for {Contracts.Length} registered contracts");
        }

        Assert.True(defects.Count == 0, $"{ContractsDoc}: " + string.Join("; ", defects));
    }

    [Fact]
    public void EverySpecPrHeadingIsItsRegisteredTextExactlyOnce()
    {
        string[] lines = Lines(ReadPlan(SpecDoc));
        var defects = new List<string>();
        foreach (FeaturePr pr in Prs.Where(p => p.SpecHeading is not null))
        {
            int n = lines.Count(line => line == pr.SpecHeading);
            if (n != 1)
            {
                defects.Add($"PR {pr.Number}'s section heading appears {n} times");
            }
        }

        int shaped = lines.Count(line => LoosePrSectionHeading().IsMatch(line));
        int registered = Prs.Count(p => p.SpecHeading is not null);
        if (shaped != registered)
        {
            defects.Add($"{shaped} PR-section-shaped headings for {registered} registered sections");
        }

        Assert.True(defects.Count == 0, "spec: " + string.Join("; ", defects));
    }

    [Fact]
    public void EveryRecordHeadingIsItsRegisteredTextExactlyOnce()
    {
        string[] lines = Lines(ReadPlan(ContractsDoc));
        var defects = new List<string>();
        foreach (FeaturePr pr in Prs)
        {
            int n = lines.Count(line => line == pr.RecordHeading);
            if (n != 1)
            {
                defects.Add($"PR {pr.Number}'s record heading appears {n} times");
            }
        }

        int shaped = lines.Count(line => LooseReviewRecordHeading().IsMatch(line));
        if (shaped != Prs.Length)
        {
            defects.Add($"{shaped} record-shaped headings for {Prs.Length} registered records");
        }

        Assert.True(defects.Count == 0, $"{ContractsDoc}: " + string.Join("; ", defects));
    }

    [Fact]
    public void EveryContractIsCitedByItsOwningSpecSection()
    {
        var sections = SpecSections();
        var missing = Contracts
            .Where(c => !sections.TryGetValue(c.Pr, out var cited) || !cited.Contains(c.Number))
            .Select(c => $"R-{c.Number} (owned by PR {c.Pr})")
            .ToList();
        Assert.True(missing.Count == 0, "Contracts never cited by their owning spec section: " + string.Join(", ", missing));
    }

    [Fact]
    public void NoSpecSectionCitesAnotherPrsContract()
    {
        var owners = Contracts.ToDictionary(c => c.Number, c => c.Pr);
        var shifted = new List<string>();
        foreach ((int pr, var cited) in SpecSections())
        {
            foreach (int contract in cited)
            {
                if (!owners.TryGetValue(contract, out int owner))
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

    /// <summary>A heading that merely resembles its registered text — a
    /// different dash, an extra issue, a malformed token, an interior bold, a
    /// changed owner — is contract-shaped for the loose detector but never
    /// equals the registry, so the exact-once fact fails on it.</summary>
    [Theory]
    [InlineData("**R-1 – Announcements reach a listening client from the first frame (PR 1, #1244).** Body.")]
    [InlineData("**R-1 — Announcements reach a listening client from the first frame (PR 1, #1244x).** Body.")]
    [InlineData("**R-1 — Announcements reach a listening client from the first frame (PR 1, #1244, #9999).** Body.")]
    [InlineData("**R-1 — Announcements reach a listening client from the first frame (PR 9, #1244).** Body.")]
    [InlineData("**R-1 — Announcements **reach** a listening client from the first frame (PR 1, #1244).** Body.")]
    [InlineData("**R-1 — Announcements reach a listening client from the first frame (PR 1, #1244).**Body.")]
    [InlineData(" **R-1 — Announcements reach a listening client from the first frame (PR 1, #1244).** Body.")]
    public void ALookAlikeHeadingIsNotItsRegisteredText(string line)
    {
        Assert.Matches(LooseContractHeading(), line.TrimStart());
        Assert.False(IsHeadingLine(line, Contracts[0].Heading));
    }

    [Fact]
    public void TheRegisteredHeadingMatchesWithItsBodyOnTheSameLine() =>
        Assert.True(IsHeadingLine(Contracts[0].Heading + " The one production raiser …", Contracts[0].Heading));

    /// <summary>A token glued to a letter, digit or number character on
    /// either side is not a citation of the contract it resembles, so a
    /// section whose only citation is such a form leaves the contract
    /// uncited; ordinary inline Markdown around a citation does not hide it.</summary>
    [Theory]
    [InlineData("see AR-6 here")]
    [InlineData("see TR-6 here")]
    [InlineData("see R-6x here")]
    [InlineData("see xR-6 here")]
    [InlineData("see R-6é here")]
    [InlineData("see éR-6 here")]
    [InlineData("see R-6² here")]
    [InlineData("see R-6\U0001D4B3 here")]
    public void ATokenGluedToAnIdentifierIsNotACitation(string text) =>
        Assert.Empty(ContractCitation().Matches(NormalizeInlineMarkdown(text)));

    [Theory]
    [InlineData("see R-6 here")]
    [InlineData("(R-6)")]
    [InlineData("R-6, R-7")]
    [InlineData("R-6.")]
    [InlineData("R-6’s rule")]
    [InlineData("see R-[6](https://example.test/contracts#r-6) here")]
    [InlineData("see R-*6* here")]
    [InlineData("see _R-6_ here")]
    public void AWellBoundedOrFormattedTokenIsACitation(string text) =>
        Assert.Equal("6", ContractCitation().Matches(NormalizeInlineMarkdown(text))[0].Groups[1].Value);

    /// <summary>PR number → the set of `R-n` the spec's section for that PR
    /// cites. A section starts at its registered heading and ends at the next
    /// level-two heading of any kind.</summary>
    private static Dictionary<int, HashSet<int>> SpecSections()
    {
        string[] lines = Lines(ReadPlan(SpecDoc));
        var sections = new Dictionary<int, HashSet<int>>();
        foreach (FeaturePr pr in Prs.Where(p => p.SpecHeading is not null))
        {
            int start = Array.IndexOf(lines, pr.SpecHeading);
            if (start < 0)
            {
                sections[pr.Number] = [];
                continue;
            }

            int end = start + 1;
            while (end < lines.Length && !LooseLevelTwoHeading().IsMatch(lines[end]))
            {
                end++;
            }

            string body = NormalizeInlineMarkdown(string.Join('\n', lines, start, end - start));
            sections[pr.Number] = ContractCitation().Matches(body).Select(m => int.Parse(m.Groups[1].Value)).ToHashSet();
        }

        return sections;
    }

    /// <summary>The line is the heading exactly, or the heading followed by a
    /// space and its body.</summary>
    private static bool IsHeadingLine(string line, string heading) =>
        line == heading || line.StartsWith(heading + " ", StringComparison.Ordinal);

    private static int[] IssuesOf(Contract c) =>
        IssueNumbers(c.Heading[c.Heading.IndexOf("(PR ", StringComparison.Ordinal)..]);

    private static int[] IssueNumbers(string registryText) =>
        IssueToken().Matches(registryText).Select(m => int.Parse(m.Groups[1].Value)).Distinct().Order().ToArray();

    private static string[] Lines(string document) => document.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();

    private static string ReadPlan(string relative) =>
        File.ReadAllText(Path.Combine(SourceText.RepoRoot(), "docs", "plans", relative));

    /// <summary>Inline Markdown that can wrap a citation without changing
    /// what a reader sees: a link becomes its text and emphasis markers are
    /// dropped.</summary>
    private static string NormalizeInlineMarkdown(string text) =>
        MarkdownLink().Replace(text, "$1").Replace("*", string.Empty).Replace("_", string.Empty);

    // Anything that starts a line like a contract definition, however it is
    // punctuated: counted against the registry, never parsed.
    [GeneratedRegex(@"^[ \t]{0,3}\*\*[ \t]*R\p{Pd}\d+", RegexOptions.Multiline)]
    private static partial Regex LooseContractHeading();

    // Anything that starts a heading like a PR section, however it is
    // spaced or punctuated after the PR number.
    [GeneratedRegex(@"^[ \t]{0,3}##[ \t]+\d+\.[ \t]+PR[ \t]+\d+\b")]
    private static partial Regex LoosePrSectionHeading();

    // Anything that starts a heading like a review record.
    [GeneratedRegex(@"^[ \t]{0,3}###[ \t]+PR[ \t]+\d+\b")]
    private static partial Regex LooseReviewRecordHeading();

    // Any level-two heading (CommonMark allows up to three leading spaces),
    // which terminates a PR section.
    [GeneratedRegex(@"^[ \t]{0,3}##[ \t]")]
    private static partial Regex LooseLevelTwoHeading();

    // A citation with Unicode-aware boundaries on both sides: a letter,
    // digit, connector, other-number or letter-number character, or a
    // surrogate next to `R-n` makes it a different token.
    [GeneratedRegex(@"(?<![\w\p{No}\p{Nl}\p{Cs}])R-(\d+)(?![\w\p{No}\p{Nl}\p{Cs}])")]
    private static partial Regex ContractCitation();

    // An issue number in a REGISTRY string (never in the documents).
    [GeneratedRegex(@"#(\d+)")]
    private static partial Regex IssueToken();

    // An inline Markdown link, `[text](destination)`, reduced to its text.
    [GeneratedRegex(@"\[([^\]\n]*)\]\([^)\n]*\)")]
    private static partial Regex MarkdownLink();
}
