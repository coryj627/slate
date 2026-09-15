// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;
using System.Text.RegularExpressions;

namespace SlateWindows.Tests.Censuses;

/// <summary>
/// W6-2 §F (F9): the issue reconciliation is checked, not trusted — the
/// twin of <see cref="CanvasReconciliationCensus"/>. Its PR ledger's
/// merge commits are distinct, each bound to its PR by the commit's own
/// subject, each verified an ancestor of this head by
/// <c>git merge-base --is-ancestor</c>, and F's base verified an ancestor
/// of <c>merge-base origin/main HEAD</c>. Its contract → evidence table
/// carries exactly one row per key AND the evidence columns the
/// generator would write: the keys and both columns are re-derived here
/// by the same grammar <c>scripts/graph_reconciliation.py</c> uses, so a
/// table regenerated over a reduced document, or a cell edited by hand,
/// fails; the key total is pinned as a constant in both, so a lost head
/// is a deliberate bump, never a silent re-blessing. Its registers,
/// decisions and issue list carry the counts F9 pins.
/// </summary>
[Trait("census", "graph-reconciliation")]
public sealed class GraphReconciliationCensus
{
    private const string SectionHeading = "## Issue reconciliation (#746)";

    /// <summary>The pinned key total — the same constant the script
    /// asserts (`EXPECTED_KEYS`).</summary>
    private const int ExpectedKeys = 519;

    /// <summary>The nine series PRs and the four post-implementation and
    /// repair PRs (F9(a), IHA-6).</summary>
    private static readonly int[] SeriesPrs = [1178, 1179, 1180, 1181, 1184, 1185, 1188, 1214, 1215];

    private static readonly int[] RepairPrs = [1182, 1199, 1208, 1209, 1213];

    private static string RepoRoot => SourceText.RepoRoot();

    private static string Doc() =>
        File.ReadAllText(Path.Combine(RepoRoot, "docs", "plans", "35_graph_contracts.md"));

    private static string Reconciliation(string doc)
    {
        int start = doc.IndexOf(SectionHeading, StringComparison.Ordinal);
        Assert.True(start >= 0, "the reconciliation section is missing");
        return doc[start..];
    }

    /// <summary>F9(a): every ledgered merge commit is distinct, bound to
    /// its PR, an ancestor of this head; F's base is E's merge and an
    /// ancestor of the branch's merge-base with main.</summary>
    [Fact]
    public void EveryLedgeredMergeIsAnAncestorOfThisHead()
    {
        string section = Reconciliation(Doc());
        var ledgered = Regex.Matches(section, @"^\| #(\d+) \| [^|]+ \| [^|]+ \| `([0-9a-f]{9,40})` \|", RegexOptions.Multiline)
            .Select(m => (Pr: int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), Sha: m.Groups[2].Value))
            .ToList();
        int[] expected = [.. SeriesPrs, .. RepairPrs];
        Assert.Equal(expected.Length, ledgered.Count);
        Assert.Equal(expected.OrderBy(p => p), ledgered.Select(l => l.Pr).OrderBy(p => p));
        Assert.Equal(expected.Length, ledgered.Select(l => l.Sha).Distinct(StringComparer.Ordinal).Count());

        Assert.NotEqual("true", Git("rev-parse", "--is-shallow-repository").Trim());
        foreach ((int pr, string sha) in ledgered)
        {
            Assert.True(
                IsAncestor(sha, "HEAD"),
                $"the ledger names {sha} as PR #{pr}'s merge commit, and it is not an ancestor of this head");
            string subject = Git("log", "-1", "--format=%s", sha).Trim();
            // A merge commit names its PR in the subject; a squash carries
            // "(#N)" for the PR or "(#746)" for the issue — the series'
            // squashes were titled by slice with the issue, and their PR
            // binding is the API's mergeCommit recorded at generation.
            bool bound = subject.StartsWith($"Merge pull request #{pr} ", StringComparison.Ordinal)
                || subject.Contains($"(#{pr})", StringComparison.Ordinal)
                || subject.Contains("(#746)", StringComparison.Ordinal);
            Assert.True(bound, $"the ledger's row for PR #{pr} names {sha}, whose subject is \"{subject}\"");
        }
        string baseSha = Regex.Match(section, @"E's\s+merge\s+commit\s+`([0-9a-f]{9,40})`\s+is\s+an\s+ancestor").Groups[1].Value;
        Assert.Equal(ledgered.Single(l => l.Pr == 1215).Sha, baseSha);
        string mergeBase = Git("merge-base", MainRef(), "HEAD").Trim();
        Assert.True(mergeBase.Length >= 9, "git merge-base with main answered nothing");
        Assert.True(IsAncestor(baseSha, mergeBase), "F's base is not an ancestor of the branch's merge-base with main");
    }

    /// <summary>F9(b): one row per key, the keys re-derived here; the
    /// evidence columns re-derived too; the total pinned.</summary>
    [Fact]
    public void TheEvidenceTableCarriesOneRowPerKey()
    {
        string doc = Doc();
        string section = Reconciliation(doc);
        var expected = Keys(doc);
        Assert.Equal(ExpectedKeys, expected.Count);
        var rows = Regex.Matches(section, @"^\| §([0-9A-Za-z-]+) \| ([a-z]+) \| ((?:Term )?[0-9A-Za-z-]+) \| (.+?) \| (.+?) \|$", RegexOptions.Multiline)
            .Select(m => (Section: m.Groups[1].Value, Kind: m.Groups[2].Value, Id: m.Groups[3].Value, By: m.Groups[4].Value, Pins: m.Groups[5].Value))
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

    /// <summary>F9(c)/(d)/(e): the divergence and risk indexes carry every
    /// key of those kinds, the eight owner decisions are resolved, the
    /// mac issues are five and the residue re-list is seven.</summary>
    [Fact]
    public void TheRegistersDecisionsAndIssuesArePinned()
    {
        string doc = Doc();
        string section = Reconciliation(doc);
        var keys = Keys(doc);
        int divergences = keys.Count(k => k.Kind == "divergence");
        int risks = keys.Count(k => k.Kind == "risk");
        Assert.Equal(divergences, Regex.Matches(section, @"^\| (?:0a|0b|B2|[A-Z]|FD)-D\d+ \| ", RegexOptions.Multiline).Count);
        Assert.Equal(risks, Regex.Matches(section, @"^\| (?:0a|0b|[A-Z])R-\d+ \| ", RegexOptions.Multiline).Count);
        Assert.Equal(8, Regex.Matches(section, @"^\| D-\d+ \| ", RegexOptions.Multiline).Count);
        Assert.Equal(15, Regex.Matches(section, @"^\| (?:CD|DD|ED)-Q\d+ \| ", RegexOptions.Multiline).Count);
        var issues = Regex.Matches(section, @"^\| mac-(\d+) \| #(\d+) \| ", RegexOptions.Multiline)
            .Select(m => m.Groups[2].Value)
            .ToList();
        Assert.Equal(5, issues.Count);
        Assert.Equal(5, issues.Distinct().Count());
        Assert.Equal(7, Regex.Matches(section, @"^\| #(118[9]|119[0-5]) \| ", RegexOptions.Multiline).Count);
    }

    // --- the script's grammar, mirrored -------------------------------------

    private static readonly (string Label, string Prefix)[] PrSections =
    [
        ("0a", "## PR 0a — "), ("0b", "## PR 0b — "), ("A", "## PR A — "), ("B", "## PR B — "),
        ("B2", "## PR B2 — "), ("C", "## PR C — "), ("D", "## PR D — "), ("E", "## PR E — "),
        ("F", "## PR F — "),
    ];

    private static List<(string Section, string Kind, string Id, string Text)> Keys(string doc)
    {
        var keys = new List<(string, string, string, string)>();
        foreach ((string label, string prefix) in PrSections)
        {
            string section = Section(doc, prefix);
            string text = KeyBearing(section);
            var counts = Regex.Matches(text, @"^(?:- )?\*\*((?:Term )?[0-9A-Za-z][0-9A-Za-z-]*?) — ", RegexOptions.Multiline)
                .Select(m => m.Groups[1].Value)
                .GroupBy(id => id, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
            foreach ((string id, int n) in counts)
            {
                if (!id.Any(char.IsDigit))
                {
                    continue;
                }
                if (id.StartsWith("IG", StringComparison.Ordinal) || id.StartsWith("IP", StringComparison.Ordinal)
                    || id.StartsWith("TG", StringComparison.Ordinal) || id.StartsWith("R-", StringComparison.Ordinal)
                    || id is "BLOCKER" or "MAJOR" or "MINOR")
                {
                    continue;
                }
                string kind = KindOf(id);
                if (kind == "head")
                {
                    continue;
                }
                Assert.True(n == 1, $"§{label}: the head {id} occurs {n} times");
                keys.Add((label, kind, id, section));
            }
        }
        return keys;
    }

    private static string KindOf(string id)
    {
        if (id.StartsWith("Term ", StringComparison.Ordinal))
        {
            return "term";
        }
        if (Regex.IsMatch(id, @"^(0a|0b|[A-Z])R-\d+$"))
        {
            return "risk";
        }
        Match m = Regex.Match(id, @"^(0a|0b|B2|[A-Z])(D?)-(D?)(Q?)(\d+)[a-z]?$");
        if (!m.Success)
        {
            return Regex.IsMatch(id, @"^F\d+$") ? "contract" : "head";
        }
        if (m.Groups[4].Value.Length > 0)
        {
            return "question";
        }
        if (m.Groups[3].Value.Length > 0)
        {
            return "divergence";
        }
        return m.Groups[2].Value.Length > 0 ? "decision" : "contract";
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
        @"^### (?:TG[0-9A-Za-z]*-\d+[a-z]? — |Task loop — records|Tests that pin|Post-implementation passes|.*close-out|Close-out)");

    private static readonly Regex NotKeyHeading = new(
        @"^### (?:Round \d|THE FREEZE|The rounds — the ledger|What stands today|The mac, traced|The mac's re-root, traced|Mac details recorded)");

    private static readonly Regex LongName = new("`([A-Z][A-Za-z0-9_]{14,})`");

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
            return (pinning ? $"unevidenced by id — §{section}'s pinning list is not keyed per id" : "unevidenced", "—");
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
