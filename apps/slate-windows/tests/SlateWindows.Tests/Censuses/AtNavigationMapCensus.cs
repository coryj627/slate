// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.RegularExpressions;
using System.Windows.Automation.Peers;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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
        @"(?<rotor>\.accessibilityRotor\s*\()|(?<action>\.accessibilityAction\s*(?:\(|\{)|\.accessibilityActions\s*(?:\(|\{)|\bNSAccessibilityCustomAction\s*\()|(?<content>\.accessibilityCustomContent\s*\(|\bAXCustomContent\s*\()",
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
            .Concat(sites.GroupBy(site => site).Where(group => group.Count() != 1).Select(group => "Multiple constructions on one line " + group.Key))
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
        var patterns = new HashSet<string>(StringComparer.Ordinal);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (string anchor in row.WindowsSource.Split("; "))
        {
            if (!ReadScope(anchor, patterns, ids)) { Fail("missing or ambiguous Windows source anchor " + anchor); }
        }
        foreach (string pattern in Tokens(row.Patterns))
        {
            if (!Enum.TryParse(pattern, out PatternInterface _) || !patterns.Contains(pattern))
            {
                Fail("selected scope does not declare native/custom pattern " + pattern);
            }
        }
        foreach (string id in Tokens(row.Ids)) { if (!ids.Contains(id)) { Fail("selected scope does not author automation ID " + id); } }
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
        ["ListBox"] = ["Selection", "SelectionItem"],
        ["ListBoxItem"] = ["SelectionItem"],
        ["RadioButton"] = ["SelectionItem"],
        ["TreeView"] = ["Selection", "SelectionItem", "ExpandCollapse"],
        ["TreeViewItem"] = ["SelectionItem", "ExpandCollapse"],
        ["DataGrid"] = ["Grid", "Table", "Selection", "VirtualizedItem"],
    };

    private static bool ReadScope(string anchor, HashSet<string> patterns, HashSet<string> ids)
    {
        Match selector = Regex.Match(anchor, @"^([^#]+)#(id|class|menu|key):([A-Za-z_][A-Za-z0-9_]*)$");
        if (!selector.Success || !SafeSourcePath(SourceText.ShellSourceRoot(), selector.Groups[1].Value, out string path)) { return false; }
        string name = selector.Groups[3].Value;
        if (selector.Groups[2].Value is "id" or "menu" or "key" && Path.GetExtension(path) == ".xaml")
        {
            XElement[] matches = XDocument.Load(path).Descendants().Where(element => selector.Groups[2].Value switch
            {
                "id" => (string?)element.Attribute("AutomationProperties.AutomationId") == name,
                "menu" => element.Name.LocalName == "MenuItem" && (string?)element.Attribute("Header") == name,
                "key" => (string?)element.Attribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml")) == name,
                _ => false,
            }).ToArray();
            if (matches.Length != 1) { return false; }
            foreach (XElement element in matches[0].DescendantsAndSelf())
            {
                patterns.UnionWith(XamlNativePatterns(element));
                XAttribute? id = element.Attribute("AutomationProperties.AutomationId");
                if (id is not null && !id.Value.StartsWith('{')) { ids.Add(id.Value); }
            }
        }
        else if (selector.Groups[2].Value == "class" && Path.GetExtension(path) == ".cs")
        {
            string relative = Path.GetRelativePath(SourceText.ShellSourceRoot(), path).Replace('\\', '/');
            CSharpSource? source = ShellCompilation.Sources.Where(item => item.Relative == relative).Select(item => item.Source).SingleOrDefault();
            if (source is null) { return false; }
            SemanticModel model = ShellCompilation.ModelFor(source);
            ClassDeclarationSyntax[] matches = source.Root.DescendantNodes()
                .OfType<ClassDeclarationSyntax>().Where(type => type.Identifier.ValueText == name).ToArray();
            if (matches.Length != 1) { return false; }
            SyntaxNode[] nodes = matches[0].DescendantNodes(node => node == matches[0] || node is not ClassDeclarationSyntax).ToArray();
            patterns.UnionWith(PatternNames(nodes, model));
            ids.UnionWith(AutomationIds(nodes, model));
            IEnumerable<TypeSyntax> constructedTypes = nodes.SelectMany(node => node switch
            {
                ObjectCreationExpressionSyntax created => new[] { created.Type },
                BaseTypeSyntax inherited => new[] { inherited.Type },
                VariableDeclarationSyntax variable when variable.Variables.Any(item => item.Initializer?.Value is ImplicitObjectCreationExpressionSyntax)
                    => new[] { variable.Type },
                _ => Array.Empty<TypeSyntax>(),
            });
            foreach (TypeSyntax type in constructedTypes)
            {
                patterns.UnionWith(FrameworkPatterns(model.GetTypeInfo(type).Type));
            }
        }
        else { return false; }
        return true;
    }

    private static IEnumerable<string> XamlNativePatterns(XElement element) =>
        element.Name.NamespaceName == "http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            && NativePatterns.TryGetValue(element.Name.LocalName, out string[]? patterns) ? patterns : [];

    private static bool IsFrameworkType(ITypeSymbol type, string fullName) =>
        type.ToDisplayString() == fullName && type.ContainingAssembly.Name is "PresentationCore" or "PresentationFramework";

    private static IEnumerable<string> FrameworkPatterns(ITypeSymbol? type)
    {
        for (INamedTypeSymbol? current = type as INamedTypeSymbol; current is not null; current = current.BaseType)
        {
            if (current.ContainingAssembly.Name is not ("PresentationCore" or "PresentationFramework")) { continue; }
            string ns = current.ContainingNamespace.ToDisplayString();
            if (ns is not ("System.Windows.Controls" or "System.Windows.Automation.Peers")) { continue; }
            string control = current.Name.EndsWith("AutomationPeer", StringComparison.Ordinal)
                ? current.Name[..^"AutomationPeer".Length] : current.Name;
            if (NativePatterns.TryGetValue(control, out string[]? supported))
            {
                foreach (string pattern in supported) { yield return pattern; }
            }
        }
    }

    private static IEnumerable<string> AutomationIds(SyntaxNode[] nodes, SemanticModel model)
    {
        foreach (InvocationExpressionSyntax call in nodes.OfType<InvocationExpressionSyntax>())
        {
            if (model.GetSymbolInfo(call).Symbol is IMethodSymbol { Name: "SetAutomationId" } method
                && IsFrameworkType(method.ContainingType, "System.Windows.Automation.AutomationProperties")
                && call.ArgumentList.Arguments.LastOrDefault()?.Expression is { } expression
                && model.GetConstantValue(expression) is { HasValue: true, Value: string value }) { yield return value; }
        }
        foreach (MethodDeclarationSyntax method in nodes.OfType<MethodDeclarationSyntax>()
            .Where(method => method.Identifier.ValueText == "GetAutomationIdCore"))
        {
            IMethodSymbol? overridden = model.GetDeclaredSymbol(method)?.OverriddenMethod;
            while (overridden is not null && !IsFrameworkType(overridden.ContainingType, "System.Windows.Automation.Peers.AutomationPeer"))
            {
                overridden = overridden.OverriddenMethod;
            }
            if (overridden is null) { continue; }
            IEnumerable<ExpressionSyntax?> values = method.ExpressionBody is { } arrow ? [arrow.Expression]
                : method.DescendantNodes(node => node is not LocalFunctionStatementSyntax and not AnonymousFunctionExpressionSyntax)
                    .OfType<ReturnStatementSyntax>().Select(statement => statement.Expression);
            foreach (ExpressionSyntax? expression in values)
            {
                if (expression is not null && model.GetConstantValue(expression) is { HasValue: true, Value: string value }) { yield return value; }
            }
        }
    }

    private static IEnumerable<string> PatternNames(IEnumerable<SyntaxNode> nodes, SemanticModel model) =>
        nodes.OfType<IdentifierNameSyntax>().Select(node => model.GetSymbolInfo(node).Symbol)
            .OfType<IFieldSymbol>()
            .Where(field => IsFrameworkType(field.ContainingType, "System.Windows.Automation.Peers.PatternInterface"))
            .Select(field => field.Name);

    [Theory]
    [InlineData("PatternInterface.Text")]
    [InlineData("global::System.Windows.Automation.Peers.PatternInterface.Text")]
    [InlineData("P.Text")]
    [InlineData("Text")]
    public void ClassPatternClaimsBindQualifiedAliasedAndStaticEnumFields(string expression)
    {
        Assert.Equal(new[] { "Text" }, FixturePatternNames(expression));
    }

    [Fact]
    public void UnrelatedSameNamedFieldsCannotManufacturePatternClaims() =>
        Assert.Empty(FixturePatternNames("Other.Text"));

    private static string[] FixturePatternNames(string expression)
    {
        SyntaxTree tree = CSharpSyntaxTree.ParseText("""
            using System.Windows.Automation.Peers;
            using P = System.Windows.Automation.Peers.PatternInterface;
            using static System.Windows.Automation.Peers.PatternInterface;
            enum Other { Text }
            class Fixture { object Read() => (
            """ + expression + "); }");
        CSharpCompilation compilation = ShellCompilation.Compilation.RemoveAllSyntaxTrees().AddSyntaxTrees(tree);
        Assert.DoesNotContain(compilation.GetDiagnostics(), diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        return PatternNames(tree.GetRoot().DescendantNodes(), compilation.GetSemanticModel(tree)).Distinct().ToArray();
    }

    [Fact]
    public void OnlyFrameworkControlsAndRealPeerOverridesSupplyNativeEvidence()
    {
        SyntaxTree tree = CSharpSyntaxTree.ParseText("""
            namespace Other { class Button {} class ButtonAutomationPeer {} }
            class DerivedButton : System.Windows.Controls.Button {}
            class Ordinary { string GetAutomationIdCore() => "InventedId"; }
            class ActualPeer : System.Windows.Automation.Peers.FrameworkElementAutomationPeer {
                public ActualPeer() : base(new System.Windows.FrameworkElement()) {}
                protected override string GetAutomationIdCore() => "RealId";
            }
            """);
        CSharpCompilation compilation = ShellCompilation.Compilation.RemoveAllSyntaxTrees().AddSyntaxTrees(tree);
        Assert.DoesNotContain(compilation.GetDiagnostics(), diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        Assert.Empty(FrameworkPatterns(compilation.GetTypeByMetadataName("Other.Button")));
        Assert.Empty(FrameworkPatterns(compilation.GetTypeByMetadataName("Other.ButtonAutomationPeer")));
        Assert.Contains("Invoke", FrameworkPatterns(compilation.GetTypeByMetadataName("DerivedButton")));
        Assert.Equal(new[] { "RealId" }, AutomationIds(tree.GetRoot().DescendantNodes().ToArray(), compilation.GetSemanticModel(tree)));
        Assert.Empty(XamlNativePatterns(XElement.Parse("<Button xmlns='clr-namespace:Other'/>")));
        Assert.Contains("Invoke", XamlNativePatterns(XElement.Parse("<Button xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'/>")));
    }

    [Theory]
    [InlineData("string Prefix() { return \"Graph\"; } return Prefix() + \"TierSummary\";")]
    [InlineData("System.Func<string> prefix = () => { return \"Graph\"; }; return prefix() + \"TierSummary\";")]
    [InlineData("System.Func<string> prefix = delegate { return \"Graph\"; }; return prefix() + \"TierSummary\";")]
    public void NestedFunctionReturnsCannotManufactureAnAutomationId(string body)
    {
        Assert.Empty(FixtureAutomationIds(body));
        Assert.Equal(new[] { "First", "Second" }, FixtureAutomationIds("if (System.Environment.TickCount > 0) { return \"First\"; } return \"Second\";"));
    }

    private static string[] FixtureAutomationIds(string body)
    {
        SyntaxTree tree = CSharpSyntaxTree.ParseText("""
            class ActualPeer : System.Windows.Automation.Peers.FrameworkElementAutomationPeer {
                public ActualPeer() : base(new System.Windows.FrameworkElement()) {}
                protected override string GetAutomationIdCore() {
            """ + body + "} }");
        CSharpCompilation compilation = ShellCompilation.Compilation.RemoveAllSyntaxTrees().AddSyntaxTrees(tree);
        Assert.DoesNotContain(compilation.GetDiagnostics(), diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        return AutomationIds(tree.GetRoot().DescendantNodes().ToArray(), compilation.GetSemanticModel(tree)).ToArray();
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
            .accessibilityAction { }
            .accessibilityActions { Button("Delete") {} }
            NSAccessibilityCustomAction(name: name) { }
            .accessibilityCustomContent("Source", value)
            AXCustomContent(label: "Connects to", value: value)
            // .accessibilityActions { }
            """;
        Assert.Equal(new[] { "rotor", "action", "action", "action", "action", "content", "content" }, Sites("View.swift", source).Select(site => site.Group));
        Assert.Equal(Enumerable.Range(4, 7).Select(line => "View.swift:" + line), Sites("View.swift", source).Select(site => site.Source));
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
        Assert.NotEmpty(CoverageFailures(rows, [.. sites, .. Sites("NewView.swift", ".accessibilityAction { }")]));
        Assert.NotEmpty(CoverageFailures(rows, [.. sites, sites.First()]));
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
    public void GeneratedFilesOutsideTheAuthoredSourceSetAreInvalidScopes()
    {
        string root = SourceText.ShellSourceRoot();
        string generated = Directory.EnumerateFiles(Path.Combine(root, "obj"), "*.g.cs", SearchOption.AllDirectories).First();
        string relative = Path.GetRelativePath(root, generated).Replace('\\', '/');
        Assert.False(ReadScope(relative + "#class:App", new(StringComparer.Ordinal), new(StringComparer.Ordinal)));
    }

    [Fact]
    public void ExistingButUnrelatedIdsAndPatternsCannotSatisfyTheMappedRoute()
    {
        Row row = Rows().Single(row => row.Affordance == "Sidebar trees");
        Assert.Empty(ClaimFailures(row));
        foreach (Row broken in new[]
        {
            row with { Ids = "`CommandPaletteSearch`" },
            row with { Patterns = "`Value`" },
            row with { Patterns = "`Toggle`" },
            row with { Patterns = "`Invoke`" },
            row with { WindowsSource = "MainWindow.xaml#id:CommandPaletteSearch" },
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
