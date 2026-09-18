// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.RegularExpressions;
using System.Windows.Automation.Peers;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SlateWindows.Commands;

namespace SlateWindows.Tests.Censuses;

/// <summary>N-5/N-6: the map is authored Markdown, checked against the
/// enumerated Mac construction sites and the Windows source/evidence.</summary>
[Trait("census", "at-navigation")]
public sealed class AtNavigationMapCensus
{
    private static string PlanRoot => Path.Combine(SourceText.RepoRoot(), "docs", "plans", "18_windows_port");
    private static string MacRoot => Path.Combine(SourceText.RepoRoot(), "apps", "slate-mac", "Sources", "SlateMac");
    private sealed record Site(string Group, string Source);
    private sealed record Row(string Group, string Affordance, string MacSource, string Mechanism,
        string WindowsSource, string Patterns, string Ids, string Chords, string Status, string Evidence, string Checklist);

    private static readonly string[] NativeAffordances =
    [
        "Reading headings", "Reading links", "Reading lists", "Reading tables",
        "Reading text, embeds, code, math and diagrams", "Form controls", "Landmarks",
        "Editor text and Outline heading navigation", "Panel lists and links", "Data grids and tables", "Sidebar trees",
    ];

    private static readonly Regex Construction = new(
        @"(?<rotor>\.accessibilityRotor\s*\()|(?<action>\.accessibilityAction\s*\(|\.accessibilityActions\s*(?:\(|\{)|\bNSAccessibilityCustomAction\s*\()|(?<content>\.accessibilityCustomContent\s*\(|\bAXCustomContent\s*\()",
        RegexOptions.CultureInvariant);

    private static Row[] ReadRows(string text) => text.Split('\n')
        .SkipWhile(line => !line.StartsWith("| Group |", StringComparison.Ordinal)).Skip(2)
        .TakeWhile(line => line.StartsWith('|'))
        .Select(line =>
        {
            string[] cells = Regex.Split(line.Trim().Trim('|'), @"(?<!\\)\|").Select(cell => cell.Trim()).ToArray();
            Assert.Equal(11, cells.Length);
            Assert.All(cells, cell => Assert.NotEmpty(cell));
            return new Row(cells[0], cells[1], cells[2], cells[3], cells[4], cells[5], cells[6], cells[7], cells[8], cells[9], cells[10]);
        }).ToArray();

    private static Row[] Rows() => ReadRows(File.ReadAllText(Path.Combine(PlanRoot, "at_navigation_map.md")));

    private static Site[] Sites(string path, string source)
    {
        string stripped = SwiftSource.WithoutComments(source);
        return Construction.Matches(stripped).Select(match => new Site(
            match.Groups["rotor"].Success ? "rotor" : match.Groups["action"].Success ? "action" : "content",
            path + ":" + (stripped.AsSpan(0, match.Index).Count('\n') + 1))).ToArray();
    }

    private static Site[] AllSites() => Directory.EnumerateFiles(MacRoot, "*.swift", SearchOption.AllDirectories)
        .SelectMany(path => Sites(Path.GetRelativePath(MacRoot, path).Replace('\\', '/'), File.ReadAllText(path))).ToArray();

    private static string[] CoverageFailures(Row[] rows, Site[] sites)
    {
        Site[] registered = rows.Where(row => row.Group != "native").Select(row => new Site(row.Group, row.MacSource)).ToArray();
        return sites.Except(registered).Select(site => "Missing " + site)
            .Concat(registered.Except(sites).Select(site => "Stale " + site))
            .Concat(registered.GroupBy(site => site).Where(group => group.Count() != 1).Select(group => "Duplicate " + group.Key))
            .ToArray();
    }

    [Fact]
    public void EveryMacConstructionHasOneMapRowAndEveryRowHasALiveConstruction()
    {
        Row[] rows = Rows();
        Site[] sites = AllSites();
        Assert.NotEmpty(sites);
        Assert.Empty(CoverageFailures(rows, sites));
        Assert.Equal(NativeAffordances.Order(), rows.Where(row => row.Group == "native").Select(row => row.Affordance).Order());
        Assert.Equal(new[] { "action", "content", "native", "rotor" }, rows.Select(row => row.Group).Distinct().Order());
        Assert.Equal(rows.Length, rows.Select(row => (row.Group, row.Affordance)).Distinct().Count());
    }

    [Fact]
    public void EveryWindowsClaimHasSourceIdentifiersAndExecutableEvidence()
    {
        Row[] rows = Rows();
        string[] failures = rows.SelectMany(ClaimFailures).ToArray();
        Assert.True(failures.Length == 0, string.Join("\n", failures));
        // A designation must have a source construction too: the coverage
        // test above prevents a retired Mac difference lingering as a waiver.
        Assert.Equal(new[] { "Dashboard Pick replacement", "Dashboard Remove section" },
            rows.Where(row => row.Status == "designated").Select(row => row.Affordance).Order());
    }

    private static IEnumerable<string> ClaimFailures(Row row)
    {
        var failures = new List<string>();
        void Fail(string message) => failures.Add(row.Affordance + ": " + message);
        foreach (string cell in new[] { row.Patterns, row.Ids, row.Chords })
        {
            if (!Regex.IsMatch(cell, @"^(?:-|`[^`]+`(?:, `[^`]+`)*)$")) { Fail("malformed identifier list " + cell); }
        }
        string macSource = row.MacSource.Split(':')[0];
        if (!SafeSourcePath(MacRoot, macSource, out _)) { Fail("missing Mac source " + macSource); }
        if (!SafeSourcePath(SourceText.ShellSourceRoot(), row.WindowsSource, out string windowsPath))
        {
            Fail("missing Windows source " + row.WindowsSource);
        }
        else
        {
            HashSet<string> patterns = SourcePatterns(windowsPath);
            foreach (string pattern in Tokens(row.Patterns))
            {
                if (!Enum.TryParse(pattern, out PatternInterface _) || !patterns.Contains(pattern))
                {
                    Fail("source does not declare native/custom pattern " + pattern);
                }
            }
        }
        HashSet<string> ids = AuthoredIds.Value;
        foreach (string id in Tokens(row.Ids)) { if (!ids.Contains(id)) { Fail("missing automation ID " + id); } }
        foreach (string id in Tokens(row.Chords)) { if (ChordTable.Find(id) is null) { Fail("missing chord row " + id); } }
        string[] evidence = Tokens(row.Evidence);
        if (evidence.Length == 0) { Fail("no executable evidence"); }
        foreach (string name in evidence)
        {
            if (!TestEvidenceCompilation.Projects.Any(project => project.HasTestEvidence(name))) { Fail("no runnable evidence " + name); }
        }
        if (row.Status is not ("verified" or "pending-AT" or "designated")) { Fail("unknown status " + row.Status); }
        if (row.Status == "designated")
        {
            if (!row.Evidence.Contains("../25_bases_grid_contracts.md", StringComparison.Ordinal)
                || !row.Mechanism.Contains("D-19", StringComparison.Ordinal)
                || !File.ReadAllText(Path.Combine(PlanRoot, "..", "25_bases_grid_contracts.md")).Contains("**D-19**", StringComparison.Ordinal))
            {
                Fail("designation has no recorded owner reason");
            }
        }
        Match checklist = Regex.Match(row.Checklist, @"^(reports/[a-z0-9_]+\.md)#([1-9][0-9]*)$");
        if (!checklist.Success || !SafeSourcePath(PlanRoot, checklist.Groups[1].Value, out string checklistPath)
            || !Regex.IsMatch(File.ReadAllText(checklistPath), @"^\| " + checklist.Groups[2].Value + @" \|", RegexOptions.Multiline))
        {
            Fail("missing numbered checklist item " + row.Checklist);
        }
        return failures;
    }

    private static bool SafeSourcePath(string root, string relative, out string path)
    {
        path = Path.GetFullPath(Path.Combine(root, relative));
        return path.StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            && File.Exists(path);
    }

    private static string[] Tokens(string cell) => Regex.Matches(cell, "`([^`]+)`").Select(match => match.Groups[1].Value).ToArray();

    private static readonly Lazy<HashSet<string>> AuthoredIds = new(() => AutomationIdInventory.Read()
        .SelectMany(site => site.Source.EndsWith(".xaml", StringComparison.Ordinal)
            ? site.Expression.StartsWith('{') ? Array.Empty<string>() : [site.Expression]
            : Microsoft.CodeAnalysis.CSharp.SyntaxFactory.ParseExpression(site.Expression) is LiteralExpressionSyntax literal
                && literal.Token.Value is string value ? new[] { value } : Array.Empty<string>())
        .ToHashSet(StringComparer.Ordinal));

    // These are WPF's native substrate promises, not a second list of map
    // rows. The actual source must construct/inherit one of these controls;
    // arbitrary prose mentioning a pattern cannot satisfy the check. The
    // named behavioral facts are still required for the mapped operation.
    private static readonly Dictionary<string, string[]> NativePatterns = new(StringComparer.Ordinal)
    {
        ["Button"] = ["Invoke"],
        ["MenuItem"] = ["Invoke", "ExpandCollapse"],
        ["CheckBox"] = ["Toggle"],
        ["TextBox"] = ["Text", "Value"],
        ["RichTextBox"] = ["Text"],
        ["ListBox"] = ["Selection"],
        ["ListBoxItem"] = ["SelectionItem"],
        ["RadioButton"] = ["SelectionItem"],
        ["TreeView"] = ["Selection"],
        ["TreeViewItem"] = ["SelectionItem", "ExpandCollapse"],
        ["DataGrid"] = ["Grid", "Table", "Selection", "VirtualizedItem"],
    };

    private static HashSet<string> SourcePatterns(string path)
    {
        var patterns = new HashSet<string>(StringComparer.Ordinal);
        var types = new HashSet<string>(StringComparer.Ordinal);
        if (Path.GetExtension(path) == ".xaml")
        {
            XDocument document = XDocument.Load(path);
            foreach (XElement element in document.Descendants())
            {
                types.Add(element.Name.LocalName);
                // Native generated item peers come from the ItemsControl's
                // item-container style, even before any rows are realized.
                XAttribute? target = element.Attribute("TargetType");
                if (target is not null) { types.Add(target.Value.Replace("{x:Type ", "").TrimEnd('}')); }
            }
        }
        else
        {
            CompilationUnitSyntax root = CSharpSource.LoadPath(path).Root;
            foreach (MemberAccessExpressionSyntax member in CSharpSource.MemberAccesses(root, "PatternInterface"))
            {
                patterns.Add(member.Name.Identifier.ValueText);
            }
            IEnumerable<TypeSyntax> declaredTypes = root.DescendantNodes().SelectMany(node => node switch
            {
                ObjectCreationExpressionSyntax created => new[] { created.Type },
                BaseTypeSyntax inherited => new[] { inherited.Type },
                VariableDeclarationSyntax variable => new[] { variable.Type },
                _ => Array.Empty<TypeSyntax>(),
            });
            foreach (TypeSyntax type in declaredTypes)
            {
                string name = type.GetLastToken().ValueText;
                types.Add(name.EndsWith("AutomationPeer", StringComparison.Ordinal) ? name[..^"AutomationPeer".Length] : name);
            }
        }
        foreach ((string control, string[] supported) in NativePatterns)
        {
            if (types.Contains(control)) { patterns.UnionWith(supported); }
        }
        return patterns;
    }

    [Fact]
    public void TheSourceSweepSeesPluralDefaultAndAppKitFormsAndPreservesLines()
    {
        string source = """
            /* a multiline
               nested /* comment */
            */
            .accessibilityRotor("Cards") { }
            .accessibilityAction(.default) { }
            .accessibilityActions { Button("Delete") {} }
            NSAccessibilityCustomAction(name: name) { }
            .accessibilityCustomContent("Source", value)
            AXCustomContent(label: "Connects to", value: value)
            // .accessibilityActions { }
            """;
        Assert.Equal(new[] { "rotor", "action", "action", "action", "content", "content" }, Sites("View.swift", source).Select(site => site.Group));
        Assert.Equal(Enumerable.Range(4, 6).Select(line => "View.swift:" + line), Sites("View.swift", source).Select(site => site.Source));
    }

    [Fact]
    public void MissingStaleDuplicateAndNewFileSitesFailTheSameCoverageCheck()
    {
        Row[] rows = Rows();
        Site[] sites = AllSites();
        Row custom = rows.First(row => row.Group != "native");
        Assert.NotEmpty(CoverageFailures(rows.Where(row => row != custom).ToArray(), sites));
        Assert.NotEmpty(CoverageFailures(rows, sites.Where(site => site.Source != custom.MacSource).ToArray()));
        Assert.NotEmpty(CoverageFailures([.. rows, custom], sites));
        Assert.NotEmpty(CoverageFailures(rows, [.. sites, .. Sites("NewView.swift", ".accessibilityActions { }")]));
    }

    [Fact]
    public void InvalidIdentifiersPatternsStatusesAndEvidenceCannotStayGreen()
    {
        Row row = Rows().First();
        foreach (Row broken in new[]
        {
            row with { Ids = "`InventedAutomationId`" }, row with { Chords = "`missing.chord`" },
            row with { Patterns = "`RangeValue`" }, row with { Status = "passed" },
            row with { Evidence = "`TestThatDoesNotExist`" }, row with { Checklist = "reports/w1_shell_at_checklist.md#999" },
            row with { WindowsSource = "Missing.cs" }, row with { Status = "designated" },
        })
        {
            Assert.NotEmpty(ClaimFailures(broken));
        }
    }

    [Fact]
    public void HandoffAndAllReadingNavigatorRowsRemainExplicit()
    {
        string map = File.ReadAllText(Path.Combine(PlanRoot, "at_navigation_map.md"));
        foreach (string required in new[] { "W8-6", "#756", "chords.json", "drift test", "docs/help/" }) { Assert.Contains(required, map); }
        string[] reading = Rows().SelectMany(row => Tokens(row.Chords)).Where(id => id.StartsWith("windows.reading.", StringComparison.Ordinal)).ToArray();
        Assert.Equal(ChordTable.Entries.Where(row => row.Id.StartsWith("windows.reading.", StringComparison.Ordinal)).Select(row => row.Id).Order(), reading.Order());
    }
}
