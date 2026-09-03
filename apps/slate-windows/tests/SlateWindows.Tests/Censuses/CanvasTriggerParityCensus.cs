// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.RegularExpressions;

namespace SlateWindows.Tests.Censuses;

/// <summary>
/// W6-1 §H TH-4 (H4, IH-37, IH-38; §W-D): the trigger ledger in the
/// contracts document is TRUE of both source trees. Every structural key
/// of the canvas vocabulary — each <c>CanvasA11yEvent</c> arm, the four
/// discriminant families expanded — has a row; each row's Windows site
/// is a member of the named file whose body constructs the key, each
/// mac site is a member of the named Swift file which spells the key,
/// each Windows fact is a test whose body references the key, and a
/// platform may lack a site only under a designation the ledger's own
/// list admits. A member that stops constructing the event fails here,
/// not in a reader's ear.
/// </summary>
[Trait("census", "canvas-trigger-parity")]
public sealed class CanvasTriggerParityCensus
{
    private static readonly string[] Families =
        ["CanvasStatusNote", "CanvasBlockedReason", "CanvasFailedAction", "CanvasMutationRefusal"];

    private static readonly Dictionary<string, string> FamilyOfArm = new()
    {
        ["CanvasStatus"] = "CanvasStatusNote",
        ["CanvasBlocked"] = "CanvasBlockedReason",
        ["CanvasActionFailed"] = "CanvasFailedAction",
        ["CanvasMutationRefused"] = "CanvasMutationRefusal",
    };

    /// <summary>The keys a platform may lack a site for — the ledger's
    /// designations, owner-recorded (HD-D3 and the H4 list).</summary>
    private static readonly HashSet<string> WindowsDesignated =
    [
        "CanvasUndoMenuTitle",
        "CanvasHistoryQuarantinedTitle",
        "CanvasMutationRefused/Reopening",
        "CanvasMutationRefused/CardEditorUnavailable",
    ];

    private static readonly HashSet<string> MacDesignated =
    [
        "CanvasViewportNoPane",
        "CanvasHistoryQuarantinedTitle",
        "CanvasBlocked/UndoQuarantined",
        "CanvasBlocked/RedoQuarantined",
        "CanvasMutationRefused/RefreshPending",
    ];

    private static readonly Regex Site = new("`([^`#]+)#([^`]+)`", RegexOptions.Compiled);

    private sealed record Row(string Key, string Mac, string Windows, string Facts, string Note);

    private static string RepoRoot => SourceText.RepoRoot();

    private static string Binding => Path.Combine(
        RepoRoot, "apps", "slate-windows", "src", "SlateUniffi", "generated", "slate_uniffi.cs");

    // --- the key set, derived from the binding ----------------------------

    private static List<string> DerivedKeys()
    {
        string binding = File.ReadAllText(Binding);
        var keys = new List<string>();
        foreach (string arm in FamilyArms(binding, "CanvasA11yEvent"))
        {
            if (FamilyOfArm.TryGetValue(arm, out string? family))
            {
                IEnumerable<string> inner = family is "CanvasFailedAction" or "CanvasMutationRefusal"
                    ? EnumArms(binding, family)
                    : FamilyArms(binding, family);
                keys.AddRange(inner.Select(name => $"{arm}/{name}"));
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

    private static List<string> EnumArms(string binding, string family)
    {
        Match m = Regex.Match(binding, @"public enum " + family + @": int \{([^}]*)\}");
        Assert.True(m.Success, $"the binding has no enum {family}");
        return m.Groups[1].Value.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
    }

    // --- the ledger, parsed from the document ------------------------------

    private static List<Row> Ledger()
    {
        string doc = File.ReadAllText(Path.Combine(RepoRoot, "docs", "plans", "34_canvas_contracts.md"));
        int head = doc.IndexOf("### The trigger ledger (H4)", StringComparison.Ordinal);
        Assert.True(head >= 0, "the contracts document has no trigger ledger");
        int table = doc.IndexOf("| Key | mac site(s) |", head, StringComparison.Ordinal);
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
            Assert.True(cells.Length == 7, $"a ledger row without five cells: {line}");
            rows.Add(new Row(cells[1].Trim('`'), cells[2], cells[3], cells[4], cells[5]));
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
            ? [$"CanvasA11yEvent.{outer}"]
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
    public void TheLedgerHasARowForEveryStructuralKeyAndNoOther()
    {
        List<string> derived = DerivedKeys();
        List<string> listed = Ledger().Select(r => r.Key).ToList();
        Assert.Equal(derived.OrderBy(k => k, StringComparer.Ordinal), listed.OrderBy(k => k, StringComparer.Ordinal));
        Assert.Equal(113, derived.Count);
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
                string? path = Directory.GetFiles(tests, fact.Groups[1].Value, SearchOption.AllDirectories)
                    .FirstOrDefault(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
                string? span = path is null ? null : MemberSpan(File.ReadAllText(path), fact.Groups[2].Value);
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

    [Fact]
    public void EveryMacSiteSpellsItsKey()
    {
        string mac = Path.Combine(RepoRoot, "apps", "slate-mac", "Sources");
        var failures = new List<string>();
        foreach (Row row in Ledger())
        {
            MatchCollection sites = Site.Matches(row.Mac);
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
                // Review round 1 (IH-56): the key must be spelled INSIDE the
                // Swift member the row names — a type-level func/var/let —
                // so a row naming a local, or a key that moved to another
                // member of the same file, fails.
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

    /// <summary>The two menu-title arms are designated on Windows because
    /// the shell has no Edit menu — a fact, not an unaudited string
    /// (IH-38).</summary>
    [Fact]
    public void TheWindowsShellHasNoEditMenuForTheTitleArms()
    {
        string xaml = File.ReadAllText(Path.Combine(SourceText.ShellSourceRoot(), "MainWindow.xaml"));
        Assert.DoesNotContain("AutomationId=\"EditMenu\"", xaml);
        foreach (string line in xaml.Split('\n'))
        {
            if (line.Contains("<MenuItem", StringComparison.Ordinal))
            {
                Assert.False(
                    Regex.IsMatch(line, "Header=\"_?(Undo|Redo)\""),
                    $"a menu item titled Undo/Redo exists: {line.Trim()}");
            }
        }
    }

    // --- the member span ------------------------------------------------------

    private static readonly Regex MemberDeclaration = new(
        @"^\s+(?:public|internal|private|protected)\s+(?:static\s+|override\s+|async\s+|virtual\s+|sealed\s+|new\s+|readonly\s+)*(?:[\w<>?,\[\]\.]+\s+)?(\w+)\s*(?:\(|=>|=|\{|$)",
        RegexOptions.Compiled);

    /// <summary>A Swift member is declared at the type's own indentation —
    /// four spaces inside <c>extension X {</c> — with func/var/let; a
    /// deeper let/var is a local of the member above it (the generator's
    /// MEMBER_SWIFT, mirrored; review round 1, IH-56).</summary>
    private static readonly Regex SwiftMemberDeclaration = new(
        @"^ {0,4}(?:@\w+\s+)*(?:(?:private|fileprivate|internal|public|open)(?:\(set\))?\s+)?(?:static\s+|final\s+|override\s+|mutating\s+)*(?:func|var|let|init|subscript)\s+(\w+)",
        RegexOptions.Compiled);

    /// <summary>The source of EVERY declaration bearing the member's name
    /// (overloads, and the same name in sibling classes), each from its
    /// declaration to the next declaration at the same or a shallower
    /// indentation — joined, so a token in any of them counts; null when
    /// no declaration bears the name.</summary>
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
