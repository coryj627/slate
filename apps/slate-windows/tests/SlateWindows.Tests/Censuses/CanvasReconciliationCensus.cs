// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;
using System.Text.RegularExpressions;

namespace SlateWindows.Tests.Censuses;

/// <summary>
/// W6-1 §H TH-12 (H9, IH-48…IH-51), tightened by PR review round 1
/// (IH-59, IH-63): the issue reconciliation is checked, not trusted. Its
/// PR ledger's merge commits are distinct, each bound to its PR by the
/// commit's own subject where the commit is a merge commit, each verified
/// an ancestor of this head by <c>git merge-base --is-ancestor</c>, and
/// H's base verified an ancestor of <c>merge-base origin/main HEAD</c> —
/// the gate as its own fact. Its contract → evidence table carries
/// exactly one row per key AND the evidence columns the generator would
/// write: the keys and both columns are re-derived here by the same
/// grammar <c>scripts/canvas_reconciliation.py</c> uses, so a table
/// regenerated over a reduced document, or a cell edited by hand, fails;
/// the key total and the "Verified during implementation" count are
/// pinned as constants in both, so a lost bullet is a deliberate bump,
/// never a silent re-blessing. Its issue list carries the ten records
/// H9(e) pins over the fifteen inputs, each numbered.
/// </summary>
[Trait("census", "canvas-reconciliation")]
public sealed class CanvasReconciliationCensus
{
    private const string SectionHeading = "## Issue reconciliation (#745)";

    /// <summary>The pinned key total and Verified count — the same two
    /// constants the script asserts (`EXPECTED_KEYS`, `EXPECTED_VERIFIED`).</summary>
    private const int ExpectedKeys = 328; // 315 at PR H + PR E13's 12 heads + VA-3
    private const int ExpectedVerified = 60;

    private static string RepoRoot => SourceText.RepoRoot();

    private static string Doc() =>
        File.ReadAllText(Path.Combine(RepoRoot, "docs", "plans", "34_canvas_contracts.md"));

    private static string Reconciliation(string doc)
    {
        int start = doc.IndexOf(SectionHeading, StringComparison.Ordinal);
        Assert.True(start >= 0, "the reconciliation section is missing");
        return doc[start..];
    }

    /// <summary>H9(a): every ledgered merge commit is distinct, bound to
    /// its PR, an ancestor of this head; H's base is G2's merge and an
    /// ancestor of the branch's merge-base with main.</summary>
    [Fact]
    public void EveryLedgeredMergeIsAnAncestorOfThisHead()
    {
        string section = Reconciliation(Doc());
        var ledgered = Regex.Matches(section, @"^\| #(\d+) \| [^|]+ \| [^|]+ \| `([0-9a-f]{9,40})` \|", RegexOptions.Multiline)
            .Select(m => (Pr: int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), Sha: m.Groups[2].Value))
            .ToList();
        Assert.Equal(12, ledgered.Count);
        Assert.Equal(Enumerable.Range(1151, 14).Except([1152, 1153]), ledgered.Select(l => l.Pr));
        Assert.Equal(12, ledgered.Select(l => l.Sha).Distinct(StringComparer.Ordinal).Count());

        Assert.NotEqual("true", Git("rev-parse", "--is-shallow-repository").Trim());
        foreach ((int pr, string sha) in ledgered)
        {
            Assert.True(
                IsAncestor(sha, "HEAD"),
                $"the ledger names {sha} as PR #{pr}'s merge commit, and it is not an ancestor of this head");
            string subject = Git("log", "-1", "--format=%s", sha).Trim();
            bool mergeCommit = subject.StartsWith("Merge pull request #", StringComparison.Ordinal);
            bool bound = mergeCommit
                ? subject.StartsWith($"Merge pull request #{pr} ", StringComparison.Ordinal)
                // The first six landed by squash: their subjects carry the
                // issue, not the PR; the PR binding is the API's mergeCommit
                // recorded at generation. Distinctness and ancestry are what
                // this lane can verify for them.
                : pr <= 1158 && subject.Contains("(#745)", StringComparison.Ordinal);
            Assert.True(bound, $"the ledger's row for PR #{pr} names {sha}, whose subject is \"{subject}\"");
        }
        string baseSha = Regex.Match(section, @"G2's merge commit `([0-9a-f]{9,40})` is an ancestor").Groups[1].Value;
        Assert.Equal(ledgered.Last().Sha, baseSha);
        string mergeBase = Git("merge-base", MainRef(), "HEAD").Trim();
        Assert.True(mergeBase.Length >= 9, "git merge-base with main answered nothing");
        Assert.True(IsAncestor(baseSha, mergeBase), "H's base is not an ancestor of the branch's merge-base with main");
    }

    /// <summary>H9(b): one row per key, the keys re-derived here; the
    /// evidence columns re-derived too; the totals pinned.</summary>
    [Fact]
    public void TheEvidenceTableCarriesOneRowPerKey()
    {
        string doc = Doc();
        string section = Reconciliation(doc);
        var expected = Keys(doc);
        Assert.Equal(ExpectedKeys, expected.Count);
        Assert.Equal(ExpectedVerified, expected.Count(k => k.Section == "Verified"));
        var rows = Regex.Matches(section, @"^\| (§[0-9A-Za-z-]+|Verified during implementation) \| ([a-z]+) \| ([0-9A-Za-z-]+) \| (.+?) \| (.+?) \|$", RegexOptions.Multiline)
            .Select(m => (Section: m.Groups[1].Value == "Verified during implementation" ? "Verified" : m.Groups[1].Value[1..], Kind: m.Groups[2].Value, Id: m.Groups[3].Value, By: m.Groups[4].Value, Pins: m.Groups[5].Value))
            .ToList();
        Assert.Equal(expected.Count, rows.Count);
        var keyed = rows.Select(r => (r.Section, r.Kind, r.Id)).ToList();
        var missing = expected.Select(k => (k.Section, k.Kind, k.Id)).Except(keyed).ToList();
        var extra = keyed.Except(expected.Select(k => (k.Section, k.Kind, k.Id))).ToList();
        Assert.True(missing.Count == 0, "keys without a row: " + string.Join(", ", missing.Select(k => $"§{k.Section} {k.Kind} {k.Id}")));
        Assert.True(extra.Count == 0, "rows without a key: " + string.Join(", ", extra.Select(k => $"§{k.Section} {k.Kind} {k.Id}")));
        Assert.Equal(expected.Count, keyed.Distinct().Count());
        Assert.Contains($"{expected.Count} keys, one row each", section);

        HashSet<string> declared = DeclaredNames();
        var wrong = new List<string>();
        foreach (var key in expected)
        {
            var row = rows.Single(r => r.Section == key.Section && r.Kind == key.Kind && r.Id == key.Id);
            (string by, string pins) = Evidence(key.Section, key.Id, key.Text, declared);
            if (row.By != by || row.Pins != pins)
            {
                wrong.Add($"§{key.Section} {key.Kind} {key.Id}: expected | {by} | {pins} | found | {row.By} | {row.Pins} |");
            }
        }
        Assert.True(wrong.Count == 0, "evidence cells that are not what the records derive:\n" + string.Join("\n", wrong));
    }

    /// <summary>H9(c)/(d)/(e): the registers re-listed whole, the seven
    /// decisions resolved, the ten issue records numbered over the
    /// fifteen inputs.</summary>
    [Fact]
    public void TheRegistersDecisionsAndIssuesArePinned()
    {
        string section = Reconciliation(Doc());
        Assert.Equal(48, Regex.Matches(section, @"^\| CD-\d+ \| ", RegexOptions.Multiline).Count);
        Assert.Equal(5, Regex.Matches(section, @"^\| CR-\d+ \| ", RegexOptions.Multiline).Count);
        Assert.Equal(7, Regex.Matches(section, @"^\| D-\d+ \| ", RegexOptions.Multiline).Count);
        var issues = Regex.Matches(section, @"^\| (mac|t0|core)-(\d+) \| #(\d+) \| ", RegexOptions.Multiline)
            .Select(m => (Target: m.Groups[1].Value, Number: m.Groups[3].Value))
            .ToList();
        Assert.Equal(10, issues.Count);
        Assert.Equal(6, issues.Count(i => i.Target == "mac"));
        Assert.Single(issues, i => i.Target == "t0");
        Assert.Equal(3, issues.Count(i => i.Target == "core"));
        Assert.Equal(10, issues.Select(i => i.Number).Distinct().Count());
        Assert.Matches(@"(?i)fifteen inputs, ten issue records", section);
    }

    // --- the script's grammar, mirrored -------------------------------------

    private static readonly (string Label, string Prefix)[] PrSections =
    [
        ("0a", "## PR 0a — "), ("0b", "## PR 0b — "), ("A", "## PR A — "), ("B", "## PR B — "),
        ("C", "## PR C — "), ("C-unit", "## PR C-unit — "), ("D", "## PR D — "), ("E", "## PR E — "),
        ("F", "## PR F — "), ("G", "## PR G — "), ("G2", "## PR G2 — "), ("H", "## PR H — "),
        ("E13", "## PR E13 — "),
        ("VA", "## Vocabulary additions"),
    ];

    private static List<(string Section, string Kind, string Id, string Text)> Keys(string doc)
    {
        var keys = new List<(string, string, string, string)>();
        foreach ((string label, string prefix) in PrSections)
        {
            string section = Section(doc, prefix);
            string text = KeyBearing(section);
            var counts = Regex.Matches(text, @"^(?:- )?\*\*([0-9A-Za-z][0-9A-Za-z-]*?) — ", RegexOptions.Multiline)
                .Select(m => m.Groups[1].Value)
                .GroupBy(id => id, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
            foreach ((string id, int n) in counts)
            {
                if (id.StartsWith("IH-", StringComparison.Ordinal) || id.StartsWith("IG2-", StringComparison.Ordinal)
                    || id.StartsWith("IF-", StringComparison.Ordinal) || id.StartsWith("IE-", StringComparison.Ordinal)
                    || id.StartsWith("R-", StringComparison.Ordinal) || id is "BLOCKER" or "MAJOR" or "MINOR")
                {
                    continue;
                }
                if (!(id is "IN" or "OUT" || id.Any(char.IsDigit)))
                {
                    continue;
                }
                Assert.True(n == 1, $"§{label}: the head {id} occurs {n} times");
                keys.Add((label, KindOf(label, id), id, section));
            }
            if (label == "C-unit")
            {
                // C-unit's implementation record numbers its tasks
                // independently of its obligations: the record's T1 is
                // a key of its own kind (H9's example).
                foreach ((string heading, string body) in Subsections(section))
                {
                    if (!heading.StartsWith("### Implementation record", StringComparison.Ordinal))
                    {
                        continue;
                    }
                    foreach (Match m in Regex.Matches(body, @"^(?:- )?\*\*(T\d+) — ", RegexOptions.Multiline))
                    {
                        keys.Add((label, "record", m.Groups[1].Value, section));
                    }
                }
            }
        }
        string verified = Section(doc, "## Verified during implementation");
        var bullets = Regex.Matches(verified, @"^- \*\*(.+?)\*\*", RegexOptions.Multiline);
        for (int i = 1; i <= bullets.Count; i++)
        {
            keys.Add(("Verified", "verified", $"V-{i}", bullets[i - 1].Groups[1].Value));
        }
        return keys;
    }

    private static string Section(string doc, string prefix)
    {
        var heads = Regex.Matches(doc, @"^## .+$", RegexOptions.Multiline).ToList();
        for (int i = 0; i < heads.Count; i++)
        {
            if (heads[i].Value.StartsWith(prefix, StringComparison.Ordinal))
            {
                int end = i + 1 < heads.Count ? heads[i + 1].Index : doc.Length;
                return doc[heads[i].Index..end];
            }
        }
        throw new Xunit.Sdk.XunitException($"section not found: {prefix}");
    }

    private static readonly Regex RecordHeading = new(
        @"^### (?:T[A-Z0-9]*-\d+[a-z]? — |Implementation record|Tests that pin|Verification plan|§\S* close-out|The task loop|C-lite reconciliation|.*close-out|PR review round)");

    private static readonly Regex NotKeyHeading = new(
        @"Round record|THE FREEZE|The ledger|The IF ledger|The record of how it got here|Codoki round|CI round|The sweep|Hand-off|Red-team round|What carries out");

    private static readonly Regex LongName = new("`([A-Z][A-Za-z0-9_]{14,})`");

    /// <summary>(heading, body) pairs; the preamble before the first ### has
    /// an empty heading — the script's `subsections`.</summary>
    private static List<(string Heading, string Body)> Subsections(string text)
    {
        string[] parts = Regex.Split(text, @"^(### .+)$", RegexOptions.Multiline);
        var result = new List<(string, string)> { ("", parts[0]) };
        for (int i = 1; i < parts.Length; i += 2)
        {
            result.Add((parts[i], i + 1 < parts.Length ? parts[i + 1] : ""));
        }
        return result;
    }

    private static string KeyBearing(string text)
    {
        var kept = new List<string>();
        foreach ((string heading, string body) in Subsections(text))
        {
            if (heading.Length > 0 && (RecordHeading.IsMatch(heading) || NotKeyHeading.IsMatch(heading)))
            {
                continue;
            }
            kept.Add(body);
        }
        return string.Join("\n", kept);
    }

    /// <summary>The script's `evidence`: the record subsections citing the
    /// id, and the long names their citing paragraphs backtick, rendered
    /// exactly as the script renders them.</summary>
    private static (string By, string Pins) Evidence(string section, string id, string text, HashSet<string> declared)
    {
        if (section == "Verified")
        {
            return ("its own bullet", "—");
        }
        var token = new Regex(@"(?<![A-Za-z0-9-])" + Regex.Escape(id) + @"(?![A-Za-z0-9-])");
        var records = new List<string>();
        var names = new List<string>();
        foreach ((string heading, string body) in Subsections(text))
        {
            if (heading.Length == 0 || !RecordHeading.IsMatch(heading))
            {
                continue;
            }
            bool cited = false;
            foreach (string paragraph in Regex.Split(body, @"\n\s*\n"))
            {
                if (!token.IsMatch(paragraph))
                {
                    continue;
                }
                cited = true;
                foreach (Match name in LongName.Matches(paragraph))
                {
                    if (!names.Contains(name.Groups[1].Value, StringComparer.Ordinal))
                    {
                        names.Add(name.Groups[1].Value);
                    }
                }
            }
            if (cited)
            {
                string shortHeading = Regex.Replace(heading, "^### ", "");
                shortHeading = Regex.Replace(shortHeading, " — .*$", "");
                records.Add(shortHeading);
            }
        }
        if (records.Count == 0)
        {
            bool pinning = Subsections(text).Any(s => s.Heading.StartsWith("### Tests that pin", StringComparison.Ordinal));
            return (pinning ? $"unevidenced by id — §{section}'s pinning list is not keyed per contract" : "unevidenced", "—");
        }
        var rendered = names.Take(8).Select(n => declared.Contains(n) ? $"`{n}`" : $"{n} (not in the tree)").ToList();
        if (names.Count > 8)
        {
            rendered.Add($"+{names.Count - 8} more");
        }
        return (string.Join(", ", records), rendered.Count > 0 ? string.Join(", ", rendered) : "—");
    }

    private static readonly Regex Comment = new(@"//[^\n]*|/\*.*?\*/", RegexOptions.Singleline);

    private static readonly Regex Declaration = new(
        @"\b(?:class|record|struct|interface|enum|namespace)\s+([A-Z][A-Za-z0-9_]{14,})\b"
        + @"|\b([A-Z][A-Za-z0-9_]{14,})\s*(?:\(|\{|=>|;|\s=\s)"
        + @"|\bvar\s+([A-Z][A-Za-z0-9_]{14,})\s*=");

    /// <summary>The script's `declared_names`: every long PascalCase name
    /// declared under apps/slate-windows, read syntactically with comments
    /// stripped — the same regex, the same walk.</summary>
    private static HashSet<string> DeclaredNames()
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        string root = Path.Combine(RepoRoot, "apps", "slate-windows");
        foreach (string file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(root, file);
            string[] parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (parts.Any(p => p is "bin" or "obj" or ".vs"))
            {
                continue;
            }
            string text = Comment.Replace(File.ReadAllText(file), "");
            foreach (Match m in Declaration.Matches(text))
            {
                string name = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Value;
                _ = names.Add(name);
            }
        }
        return names;
    }

    private static string Prefix(string id)
    {
        if (id is "IN" or "OUT")
        {
            return id;
        }
        Match m = Regex.Match(id, @"^(HD-D|G2D|G2|E13D|E13R|E13|0a|0b|[A-Z]+)-?\d");
        return m.Success ? m.Groups[1].Value : id;
    }

    private static string KindOf(string section, string id)
    {
        string p = Prefix(id);
        if (section == "C-unit")
        {
            return p switch
            {
                "U" => "contract",
                "D" => "design",
                "T" => "task",
                "I" => "invariant",
                "IN" or "OUT" => "scope",
                _ => "head",
            };
        }
        if (section == "D" && p == "D")
        {
            return "contract";
        }
        return p switch
        {
            "0a" or "0b" or "A" or "B" or "C" or "U" or "E" or "F" or "G" or "G2" or "H" or "E13" => "contract",
            "DD" or "G2D" or "HD" or "E13D" => "decision",
            "HD-D" => "divergence",
            "HR" or "E13R" => "risk",
            "ID" => "obligation",
            "TD" or "TE" or "TF" => "task",
            "VA" => "vocabulary",
            "IN" or "OUT" => "scope",
            _ => "head",
        };
    }

    // --- git ------------------------------------------------------------------

    private static string MainRef() =>
        Git("rev-parse", "--verify", "--quiet", "origin/main").Trim().Length > 0 ? "origin/main" : "main";

    private static bool IsAncestor(string sha, string descendant)
    {
        using var process = Process.Start(new ProcessStartInfo("git", $"merge-base --is-ancestor {sha} {descendant}")
        {
            WorkingDirectory = RepoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        })!;
        process.WaitForExit();
        return process.ExitCode == 0;
    }

    private static string Git(params string[] args)
    {
        using var process = Process.Start(new ProcessStartInfo("git", string.Join(' ', args))
        {
            WorkingDirectory = RepoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        })!;
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output;
    }
}
