// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.RegularExpressions;

namespace SlateWindows.Tests.Censuses;

/// <summary>
/// W6-2 §F (F4, FD-8, FD-11; §W-D): the graph trigger ledger in the
/// contracts document is TRUE of both source trees — the twin of
/// <see cref="CanvasTriggerParityCensus"/>. Every structural key of the
/// graph vocabulary — each <c>GraphA11yEvent</c> arm, the two discriminant
/// families expanded — has a row carrying its ROLE from 0a's site
/// manifest (posted, or label for the two keys that are never posted);
/// each row's Windows site is a member of the named file whose body
/// constructs the key (for a posted key, for the announcer; for a label
/// key, the render into a name or help text), each mac site is a member
/// of the named Swift file which spells the key, each Windows fact is a
/// test whose body references the key, a posted key names the F1 fact
/// that observes its delivery end to end or is marked site-only with an
/// injected-failure fact, and a platform may lack a site only under a
/// designation the ledger's own list admits.
/// </summary>
[Trait("census", "graph-trigger-parity")]
public sealed class GraphTriggerParityCensus
{
    private static readonly Dictionary<string, string> FamilyOfArm = new()
    {
        ["GraphStatus"] = "GraphStatusNote",
        ["GraphBlocked"] = "GraphBlockedReason",
    };

    /// <summary>The keys 0a's manifest records as never posted (C1, C2):
    /// rendered into a name or custom content, never announced (FD-11).</summary>
    private static readonly HashSet<string> LabelKeys =
    [
        "GraphTierSummary",
        "GraphNeighborsContent",
    ];

    /// <summary>The keys a platform may lack a site for — the ledger's
    /// designations, owner-recorded (FD-8 and the F4 list).</summary>
    private static readonly HashSet<string> WindowsDesignated =
    [
        "GraphStatus/AlreadyOpen",
    ];

    private static readonly HashSet<string> MacDesignated =
    [
        "GraphStatus/NoConnections",
        "GraphStatus/LoadingConnections",
    ];

    /// <summary>The posted keys no healthy session reaches, pinned by a
    /// unit fact that injects the failure (F4): site-only rows.</summary>
    private static readonly HashSet<string> SiteOnly =
    [
        "GraphBlocked/LoadFailed",
        "GraphBlocked/ConnectionsLoadFailed",
    ];

    private const string EndToEndSuite = "GraphEndToEndTests.cs";

    private static readonly Regex Site = new("`([^`#]+)#([^`]+)`", RegexOptions.Compiled);

    private sealed record Row(string Key, string Role, string Mac, string Windows, string Facts, string Observed, string Note);

    private static string RepoRoot => SourceText.RepoRoot();

    private static string Binding => Path.Combine(
        RepoRoot, "apps", "slate-windows", "src", "SlateUniffi", "generated", "slate_uniffi.cs");

    // --- the key set, derived from the binding ----------------------------

    private static List<string> DerivedKeys()
    {
        string binding = File.ReadAllText(Binding);
        var keys = new List<string>();
        foreach (string arm in FamilyArms(binding, "GraphA11yEvent"))
        {
            if (FamilyOfArm.TryGetValue(arm, out string? family))
            {
                keys.AddRange(FamilyArms(binding, family).Select(name => $"{arm}/{name}"));
            }
            else
            {
                keys.Add(arm);
            }
        }
        return keys;
    }

    private static List<string> FamilyArms(string binding, string family)
    {
        int start = binding.IndexOf($"public record {family} {{", StringComparison.Ordinal);
        Assert.True(start >= 0, $"the binding has no record family {family}");
        int end = binding.IndexOf("\n}\n", start, StringComparison.Ordinal);
        return Regex.Matches(binding[start..end], @"public record (\w+)\s*(?:\([^)]*\))?\s*: " + family)
            .Select(m => m.Groups[1].Value)
            .ToList();
    }

    // --- the ledger, parsed from the document ------------------------------

    private static List<Row> Ledger()
    {
        string doc = File.ReadAllText(Path.Combine(RepoRoot, "docs", "plans", "35_graph_contracts.md"));
        int head = doc.IndexOf("### The trigger ledger (F4)", StringComparison.Ordinal);
        Assert.True(head >= 0, "the contracts document has no graph trigger ledger");
        int table = doc.IndexOf("| Key | Role | mac site(s) |", head, StringComparison.Ordinal);
        Assert.True(table >= 0, "the trigger ledger has no table");
        var rows = new List<Row>();
        foreach (string line in doc[table..].Split('\n').Skip(2))
        {
            if (!line.StartsWith("| `", StringComparison.Ordinal))
            {
                break;
            }
            string[] cells = line.Split('|').Select(c => c.Trim()).ToArray();
            // cells[0] and the last are the empty ends of the row
            Assert.True(cells.Length == 9, $"a ledger row without seven cells: {line}");
            rows.Add(new Row(cells[1].Trim('`'), cells[2], cells[3], cells[4], cells[5], cells[6], cells[7]));
        }
        return rows;
    }

    private static (string Outer, string? Inner) Split(string key)
    {
        string[] parts = key.Split('/');
        return (parts[0], parts.Length > 1 ? parts[1] : null);
    }

    /// <summary>The tokens a Windows construction of the key carries.</summary>
    private static string[] WindowsTokens(string key)
    {
        (string outer, string? inner) = Split(key);
        return inner is null
            ? [$"GraphA11yEvent.{outer}"]
            : [$"{FamilyOfArm[outer]}.{inner}"];
    }

    private static string LowerCamel(string name) => char.ToLowerInvariant(name[0]) + name[1..];

    private static string[] MacTokens(string key)
    {
        (string outer, string? inner) = Split(key);
        return inner is null
            ? [$".{LowerCamel(outer)}"]
            : [$".{LowerCamel(inner)}"];
    }

    // --- the facts ------------------------------------------------------------

    [Fact]
    public void TheLedgerHasARowForEveryStructuralKeyWithItsRoleAndNoOther()
    {
        List<string> derived = DerivedKeys();
        List<Row> ledger = Ledger();
        Assert.Equal(derived.OrderBy(k => k, StringComparer.Ordinal), ledger.Select(r => r.Key).OrderBy(k => k, StringComparer.Ordinal));
        Assert.Equal(24 /* fifteen unfamilied arms, six status notes, three blocked reasons */, derived.Count);
        foreach (Row row in ledger)
        {
            string expected = LabelKeys.Contains(row.Key) ? "label" : "posted";
            Assert.True(row.Role == expected, $"{row.Key}: the ledger says {row.Role}, 0a's manifest says {expected}");
        }
    }

    [Fact]
    public void EveryWindowsSiteConstructsItsKeyAndEveryFactAssertsIt()
    {
        string shell = SourceText.ShellSourceRoot();
        string tests = Path.Combine(RepoRoot, "apps", "slate-windows", "tests");
        var failures = new List<string>();
        foreach (Row row in Ledger())
        {
            string[] tokens = WindowsTokens(row.Key);
            MatchCollection sites = Site.Matches(row.Windows);
            if (WindowsDesignated.Contains(row.Key))
            {
                if (!row.Note.Contains("designated", StringComparison.Ordinal))
                {
                    failures.Add($"{row.Key}: designated in the census but not in the ledger's note");
                }
                continue;
            }
            if (sites.Count == 0)
            {
                failures.Add($"{row.Key}: no Windows site and no admitted designation");
                continue;
            }
            foreach (Match site in sites)
            {
                string? path = Directory.GetFiles(shell, site.Groups[1].Value, SearchOption.AllDirectories)
                    .FirstOrDefault(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
                if (path is null)
                {
                    failures.Add($"{row.Key}: no shell file {site.Groups[1].Value}");
                    continue;
                }
                string member = site.Groups[2].Value;
                string? span = MemberSpan(File.ReadAllText(path), member);
                if (span is null)
                {
                    failures.Add($"{row.Key}: {site.Value} names no member");
                }
                else if (!tokens.Any(t => span.Contains(t, StringComparison.Ordinal)))
                {
                    failures.Add($"{row.Key}: {site.Value} does not construct {string.Join("/", tokens)}");
                }
            }
            MatchCollection facts = Site.Matches(row.Facts);
            if (facts.Count == 0)
            {
                failures.Add($"{row.Key}: no Windows fact");
            }
            foreach (Match fact in facts)
            {
                string? span = TestMemberSpan(tests, fact.Groups[1].Value, fact.Groups[2].Value);
                if (span is null)
                {
                    failures.Add($"{row.Key}: the fact {fact.Value} does not exist");
                }
                else if (!tokens.Any(t => span.Contains(t, StringComparison.Ordinal)))
                {
                    failures.Add($"{row.Key}: the fact {fact.Value} does not assert {string.Join("/", tokens)}");
                }
            }
        }
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    /// <summary>F4 (IGZ-7): a posted key names the F1 fact that observes
    /// its delivery — a member of GraphEndToEndTests whose body constructs
    /// the key — or is one of the two site-only failure arms, whose column
    /// names the injected-failure fact instead; a label key names the
    /// grammar fact that reads the peer.</summary>
    [Fact]
    public void EveryPostedKeyIsObservedEndToEndOrMarkedSiteOnly()
    {
        string tests = Path.Combine(RepoRoot, "apps", "slate-windows", "tests");
        var failures = new List<string>();
        foreach (Row row in Ledger())
        {
            if (WindowsDesignated.Contains(row.Key))
            {
                if (!row.Observed.StartsWith("—", StringComparison.Ordinal))
                {
                    failures.Add($"{row.Key}: designated on Windows yet claims an observation");
                }
                continue;
            }
            string[] tokens = WindowsTokens(row.Key);
            if (SiteOnly.Contains(row.Key))
            {
                if (!row.Observed.StartsWith("site-only:", StringComparison.Ordinal))
                {
                    failures.Add($"{row.Key}: a failure arm must be marked site-only");
                    continue;
                }
                Match injected = Site.Match(row.Observed);
                string? span = injected.Success ? TestMemberSpan(tests, injected.Groups[1].Value, injected.Groups[2].Value) : null;
                if (span is null || !tokens.Any(t => span.Contains(t, StringComparison.Ordinal)))
                {
                    failures.Add($"{row.Key}: the site-only row names no injected-failure fact that constructs the key");
                }
                continue;
            }
            if (row.Observed.StartsWith("site-only:", StringComparison.Ordinal))
            {
                failures.Add($"{row.Key}: marked site-only, but it is not one of the two failure arms");
                continue;
            }
            string factName = row.Observed.Split(' ')[0];
            string? e2e = TestMemberSpan(tests, EndToEndSuite, factName);
            if (e2e is null)
            {
                failures.Add($"{row.Key}: the observing fact {factName} is not a member of {EndToEndSuite}");
            }
            else if (!tokens.Any(t => e2e.Contains(t, StringComparison.Ordinal)))
            {
                failures.Add($"{row.Key}: {factName} does not construct {string.Join("/", tokens)}");
            }
        }
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    /// <summary>F4 (FD-11; codoki's note on the F head): a LABEL key is
    /// rendered into a peer's Name or HelpText and never posted. Its ledger
    /// row names the grammar fact and the peer property that fact reads;
    /// every construction of the key in the shell is the argument of
    /// GraphAnnouncer.RenderLabel, never of Announce; and the grammar fact
    /// (with the end-to-end helpers it calls) reads that property and
    /// asserts the rendered text is absent from the announced lines. The
    /// membership assertion of the posted-key fact applies to label rows
    /// too, deliberately: the grammar fact IS a member of the suite.</summary>
    [Fact]
    public void EveryLabelKeyIsRenderedIntoAPeerAndNeverPosted()
    {
        string shell = SourceText.ShellSourceRoot();
        string tests = Path.Combine(RepoRoot, "apps", "slate-windows", "tests");
        string shellText = string.Concat(Directory.GetFiles(shell, "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}generated{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(File.ReadAllText));
        List<Row> labels = Ledger().Where(r => r.Role == "label").ToList();
        Assert.Equal(LabelKeys.Count, labels.Count);
        var failures = new List<string>();
        foreach (Row row in labels)
        {
            string token = WindowsTokens(row.Key).Single();
            Match read = Regex.Match(row.Observed, @"^AnnouncementGrammarConformsPerVerbosity \(the peer's (Name|HelpText)\)$");
            if (!read.Success)
            {
                failures.Add($"{row.Key}: a label row names the grammar fact and the peer property it reads; got '{row.Observed}'");
                continue;
            }
            string property = read.Groups[1].Value;
            string rendered = $"GraphAnnouncer.RenderLabel(new {token}(";
            int constructions = 0;
            foreach (Match construction in Regex.Matches(shellText, @"[^\n]*new " + Regex.Escape(token) + @"\("))
            {
                constructions++;
                if (!construction.Value.Contains(rendered, StringComparison.Ordinal))
                {
                    failures.Add($"{row.Key}: constructed outside RenderLabel — '{construction.Value.Trim()}'");
                }
            }
            if (constructions == 0)
            {
                failures.Add($"{row.Key}: the shell never renders the key");
            }
            string? reader = GrammarFactWithItsHelpers(tests);
            if (reader is null)
            {
                failures.Add($"{row.Key}: the grammar fact is not a member of {EndToEndSuite}");
            }
            else
            {
                if (!reader.Contains($".Get{property}()", StringComparison.Ordinal))
                {
                    failures.Add($"{row.Key}: the grammar fact reads no peer's {property}");
                }
                if (!reader.Contains(token, StringComparison.Ordinal) || !reader.Contains("Assert.DoesNotContain(", StringComparison.Ordinal))
                {
                    failures.Add($"{row.Key}: the grammar fact does not hold the rendered label out of the announced lines");
                }
            }
        }
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    /// <summary>The grammar fact's span joined with the span of every
    /// end-to-end member it calls by name (one level: the tier-B helper).</summary>
    private static string? GrammarFactWithItsHelpers(string tests)
    {
        string? span = TestMemberSpan(tests, EndToEndSuite, "AnnouncementGrammarConformsPerVerbosity");
        if (span is null)
        {
            return null;
        }
        var joined = new System.Text.StringBuilder(span);
        foreach (string callee in Regex.Matches(span, @"\b([A-Z]\w+)\(").Select(m => m.Groups[1].Value).Distinct())
        {
            if (callee != "AnnouncementGrammarConformsPerVerbosity" && TestMemberSpan(tests, EndToEndSuite, callee) is string helper)
            {
                joined.Append('\n').Append(helper);
            }
        }
        return joined.ToString();
    }

    [Fact]
    public void EveryMacSiteSpellsItsKey()
    {
        string mac = Path.Combine(RepoRoot, "apps", "slate-mac", "Sources");
        var failures = new List<string>();
        foreach (Row row in Ledger())
        {
            MatchCollection sites = Site.Matches(row.Mac);
            if (sites.Any(site => site.Groups[1].Value == "slate_uniffi.swift"))
            {
                failures.Add($"{row.Key}: generated bindings cannot prove a host trigger");
            }
            if (MacDesignated.Contains(row.Key))
            {
                if (!row.Note.Contains("mac designated", StringComparison.Ordinal))
                {
                    failures.Add($"{row.Key}: mac-designated in the census but not in the ledger's note");
                }
                continue;
            }
            if (sites.Count == 0)
            {
                failures.Add($"{row.Key}: no mac site and no admitted designation");
                continue;
            }
            string[] tokens = MacTokens(row.Key);
            foreach (Match site in sites)
            {
                string? path = Directory.GetFiles(mac, site.Groups[1].Value, SearchOption.AllDirectories).FirstOrDefault();
                if (path is null)
                {
                    failures.Add($"{row.Key}: no mac file {site.Groups[1].Value}");
                    continue;
                }
                string source = File.ReadAllText(path);
                string member = site.Groups[2].Value;
                string? span = member == "?" ? null : MemberSpan(source, member, swift: true);
                if (span is null)
                {
                    failures.Add($"{row.Key}: {site.Value} names no member");
                }
                else if (!tokens.Any(t => span.Contains(t, StringComparison.Ordinal)))
                {
                    failures.Add($"{row.Key}: {site.Value} does not spell {string.Join("/", tokens)} inside {member}");
                }
            }
        }
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    /// <summary>FD-8's Windows designation is a fact, not an unaudited
    /// string: the graph tab is excluded from Duplicate, so no Windows
    /// site can speak AlreadyOpen the way the mac's Duplicate Tab does.</summary>
    [Fact]
    public void TheGraphTabIsExcludedFromDuplicateSoAlreadyOpenHasNoTrigger()
    {
        string shell = SourceText.ShellSourceRoot();
        string text = string.Concat(Directory.GetFiles(shell, "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(File.ReadAllText));
        Assert.DoesNotContain("GraphStatusNote.AlreadyOpen", text);
    }

    // --- the member span ------------------------------------------------------

    private static string? TestMemberSpan(string tests, string file, string member)
    {
        string? path = Directory.GetFiles(tests, file, SearchOption.AllDirectories)
            .FirstOrDefault(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
        return path is null ? null : MemberSpan(File.ReadAllText(path), member);
    }

    private static readonly Regex MemberDeclaration = new(
        @"^\s+(?:public|internal|private|protected)\s+(?:static\s+|override\s+|async\s+|virtual\s+|sealed\s+|new\s+|readonly\s+)*(?:[\w<>?,\[\]\.]+\s+)?(\w+)\s*(?:\(|=>|=|\{|$)",
        RegexOptions.Compiled);

    private static readonly Regex SwiftMemberDeclaration = new(
        @"^ {0,4}(?:@\w+\s+)*(?:(?:private|fileprivate|internal|public|open)(?:\(set\))?\s+)?(?:static\s+|final\s+|override\s+|mutating\s+)*(?:func|var|let|init|subscript)\s+(\w+)",
        RegexOptions.Compiled);

    /// <summary>The source of EVERY declaration bearing the member's name,
    /// each from its declaration to the next declaration at the same or a
    /// shallower indentation — joined; null when no declaration bears the
    /// name (the canvas census's span, mirrored).</summary>
    private static string? MemberSpan(string source, string member, bool swift = false)
    {
        Regex declaration = swift ? SwiftMemberDeclaration : MemberDeclaration;
        string[] lines = source.Split('\n');
        var spans = new System.Text.StringBuilder();
        bool found = false;
        for (int i = 0; i < lines.Length; i++)
        {
            Match m = declaration.Match(lines[i]);
            if (!m.Success || m.Groups[1].Value != member)
            {
                continue;
            }
            found = true;
            int indent = lines[i].Length - lines[i].TrimStart().Length;
            for (int j = i; j < lines.Length; j++)
            {
                if (j > i)
                {
                    Match next = declaration.Match(lines[j]);
                    int nextIndent = lines[j].Length - lines[j].TrimStart().Length;
                    if (next.Success && nextIndent <= indent)
                    {
                        break;
                    }
                }
                spans.Append(lines[j]).Append('\n');
            }
        }
        return found ? spans.ToString() : null;
    }
}
