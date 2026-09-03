// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;
using System.Text.RegularExpressions;

namespace SlateWindows.Tests.Censuses;

/// <summary>
/// W6-1 §H TH-12 (H9, IH-48…IH-51): the issue reconciliation is checked,
/// not trusted. Its PR ledger's merge commits are each verified an
/// ancestor of this head by <c>git merge-base --is-ancestor</c> — the
/// gate as its own fact, so a ledger naming a commit that never merged
/// fails here rather than reading as history. Its contract → evidence
/// table carries exactly one row per key — the keys re-derived from the
/// document by the same head grammar <c>scripts/canvas_reconciliation.py</c>
/// uses, (section, kind, id) qualified, C-unit's implementation-record
/// heads keyed as records beside its obligations as the script keys
/// them — so an omitted row or two collapsed keys fail. Its issue list carries the ten records H9(e) pins over the
/// fifteen inputs, each numbered.
/// </summary>
[Trait("census", "canvas-reconciliation")]
public sealed class CanvasReconciliationCensus
{
    private const string SectionHeading = "## Issue reconciliation (#745)";

    private static string RepoRoot => SourceText.RepoRoot();

    private static string Doc() =>
        File.ReadAllText(Path.Combine(RepoRoot, "docs", "plans", "34_canvas_contracts.md"));

    private static string Reconciliation(string doc)
    {
        int start = doc.IndexOf(SectionHeading, StringComparison.Ordinal);
        Assert.True(start >= 0, "the reconciliation section is missing");
        return doc[start..];
    }

    /// <summary>H9(a): every ledgered merge commit is an ancestor of this
    /// head, and H's base — the merged main after G2 — is too.</summary>
    [Fact]
    public void EveryLedgeredMergeIsAnAncestorOfThisHead()
    {
        string section = Reconciliation(Doc());
        var ledgered = Regex.Matches(section, @"^\| #(\d+) \| [^|]+ \| [^|]+ \| `([0-9a-f]{9,40})` \|", RegexOptions.Multiline)
            .Select(m => (Pr: int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), Sha: m.Groups[2].Value))
            .ToList();
        Assert.Equal(12, ledgered.Count);
        Assert.Equal(Enumerable.Range(1151, 14).Except([1152, 1153]), ledgered.Select(l => l.Pr));

        Assert.NotEqual("true", Git("rev-parse", "--is-shallow-repository").Trim());
        foreach ((int pr, string sha) in ledgered)
        {
            Assert.True(
                IsAncestor(sha),
                $"the ledger names {sha} as PR #{pr}'s merge commit, and it is not an ancestor of this head");
        }
        string baseSha = Regex.Match(section, @"G2's merge commit `([0-9a-f]{9,40})` is an ancestor").Groups[1].Value;
        Assert.Equal(ledgered.Last().Sha, baseSha);
        Assert.True(IsAncestor(baseSha), "H's base is not an ancestor of this head");
    }

    /// <summary>H9(b): one row per key, the keys re-derived here.</summary>
    [Fact]
    public void TheEvidenceTableCarriesOneRowPerKey()
    {
        string doc = Doc();
        string section = Reconciliation(doc);
        var expected = Keys(doc);
        var rows = Regex.Matches(section, @"^\| (§[0-9A-Za-z-]+|Verified during implementation) \| ([a-z]+) \| ([0-9A-Za-z-]+) \| ", RegexOptions.Multiline)
            .Select(m => (Section: m.Groups[1].Value == "Verified during implementation" ? "Verified" : m.Groups[1].Value[1..], Kind: m.Groups[2].Value, Id: m.Groups[3].Value))
            .ToList();
        Assert.Equal(expected.Count, rows.Count);
        var missing = expected.Except(rows).ToList();
        var extra = rows.Except(expected).ToList();
        Assert.True(missing.Count == 0, "keys without a row: " + string.Join(", ", missing.Select(k => $"§{k.Section} {k.Kind} {k.Id}")));
        Assert.True(extra.Count == 0, "rows without a key: " + string.Join(", ", extra.Select(k => $"§{k.Section} {k.Kind} {k.Id}")));
        Assert.Equal(expected.Count, expected.Distinct().Count());
        Assert.Contains($"{expected.Count} keys, one row each", section);
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

    private static List<(string Section, string Kind, string Id)> Keys(string doc)
    {
        (string Label, string Prefix)[] prs =
        [
            ("0a", "## PR 0a — "), ("0b", "## PR 0b — "), ("A", "## PR A — "), ("B", "## PR B — "),
            ("C", "## PR C — "), ("C-unit", "## PR C-unit — "), ("D", "## PR D — "), ("E", "## PR E — "),
            ("F", "## PR F — "), ("G", "## PR G — "), ("G2", "## PR G2 — "), ("H", "## PR H — "),
            ("VA", "## Vocabulary additions"),
        ];
        var keys = new List<(string, string, string)>();
        foreach ((string label, string prefix) in prs)
        {
            string text = KeyBearing(Section(doc, prefix));
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
                keys.Add((label, KindOf(label, id), id));
            }
            if (label == "C-unit")
            {
                // C-unit's implementation record numbers its tasks
                // independently of its obligations: the record's T1 is
                // a key of its own kind (H9's example).
                string[] parts = Regex.Split(Section(doc, prefix), @"^(### .+)$", RegexOptions.Multiline);
                for (int i = 1; i + 1 < parts.Length; i += 2)
                {
                    if (!parts[i].StartsWith("### Implementation record", StringComparison.Ordinal))
                    {
                        continue;
                    }
                    foreach (Match m in Regex.Matches(parts[i + 1], @"^(?:- )?\*\*(T\d+) — ", RegexOptions.Multiline))
                    {
                        keys.Add((label, "record", m.Groups[1].Value));
                    }
                }
            }
        }
        string verified = Section(doc, "## Verified during implementation");
        int bullets = Regex.Matches(verified, @"^- \*\*(.+?)\*\*", RegexOptions.Multiline).Count;
        for (int i = 1; i <= bullets; i++)
        {
            keys.Add(("Verified", "verified", $"V-{i}"));
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

    private static string KeyBearing(string text)
    {
        string[] parts = Regex.Split(text, @"^(### .+)$", RegexOptions.Multiline);
        var kept = new List<string> { parts[0] };
        for (int i = 1; i < parts.Length; i += 2)
        {
            string heading = parts[i];
            string body = i + 1 < parts.Length ? parts[i + 1] : "";
            if (RecordHeading.IsMatch(heading) || NotKeyHeading.IsMatch(heading))
            {
                continue;
            }
            kept.Add(body);
        }
        return string.Join("\n", kept);
    }

    private static string Prefix(string id)
    {
        if (id is "IN" or "OUT")
        {
            return id;
        }
        Match m = Regex.Match(id, @"^(HD-D|G2D|G2|0a|0b|[A-Z]+)-?\d");
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
            "0a" or "0b" or "A" or "B" or "C" or "U" or "E" or "F" or "G" or "G2" or "H" => "contract",
            "DD" or "G2D" or "HD" => "decision",
            "HD-D" => "divergence",
            "HR" => "risk",
            "ID" => "obligation",
            "TD" or "TE" or "TF" => "task",
            "VA" => "vocabulary",
            "IN" or "OUT" => "scope",
            _ => "head",
        };
    }

    private static bool IsAncestor(string sha)
    {
        using var process = Process.Start(new ProcessStartInfo("git", $"merge-base --is-ancestor {sha} HEAD")
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
