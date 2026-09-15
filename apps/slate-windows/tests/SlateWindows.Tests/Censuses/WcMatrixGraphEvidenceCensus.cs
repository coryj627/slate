// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SlateWindows.Tests.Censuses;

/// <summary>
/// W6-2 PR A (#746), contract A-12 (§W-C): the graph rows of
/// <c>w_c_matrix.md</c> are TRUE of the implemented surface — the twin of
/// <see cref="WcMatrixCanvasEvidenceCensus"/> with a one-surface manifest
/// that PR B–E extend. Every row has exactly ten cells, names the shell's
/// automation ids and the substrate's control types and patterns, every
/// backticked evidence name resolves in the test tree, every axe label
/// is scanned by a journey, and the human cells stay Pending until a
/// named run.
/// </summary>
[Trait("census", "w-c-matrix-graph")]
public sealed class WcMatrixGraphEvidenceCensus
{
    private sealed record Surface(
        string Title,
        string[] Ids,
        string[] NativeControlTypes,
        string[] NativePatterns,
        string[] NameSources,
        string[] Evidence,
        string[] AxeLabels);

    private static readonly Surface[] Manifest =
    [
        new(
            "Graph table (W6-2 PR A)",
            ["GraphSurface", "GraphSurfaceSwitcher", "GraphMode.", "GraphStateText", "GraphTableGrid"],
            ["Grid", "DataGrid", "Group", "Text"],
            ["Grid", "Table", "Selection", "Invoke", "SelectionItem"],
            ["graph_table_rows", "graph_table_columns", "GraphRow", "audio_summary"],
            ["GraphDocumentTests", "GraphTableTests", "GraphAnnouncerTests", "GraphSurfaces_TableSortSelectionAndActivation_AreClean", "TheDocumentsRowsAndSummaryEqualTheArtifactsUnderTheArtifactsFilter"],
            ["graph-table"]),
        // W6-2 PR B, slice B1 (B-20): the Connections leaf.
        new(
            "Graph connections leaf (W6-2 PR B)",
            ["ConnectionsLeafBody", "ConnectionsHeading", "ConnectionsSummary", "ConnectionsDepth", "ConnectionsStateText", "ConnectionsLeaf", "ConnectionsTree"],
            ["Tree", "TreeItem", "ComboBox", "Text"],
            ["Tree", "SelectionItem", "ExpandCollapse", "ScrollItem", "Invoke", "Selection"],
            ["GraphRow", "GraphNeighborhoodSummary", "ConnectionsPhrase"],
            ["ConnectionsLeafTests", "ConnectionsLeafViewTests", "GraphConnections_LeafWalkDepthAndReRoot_AreClean", "TheLeafsTreeIsTheSessionsRecordFieldByFieldForEveryPinnedPair"],
            ["graph-connections"]),
        // W6-2 PR C (C-16): the navigator, the filter, Where-am-I, the menu.
        new(
            "Graph navigator, filter and Where-am-I (W6-2 PR C)",
            ["GraphFilterField", "GraphFilterSummary", "GraphClearFilter", "GraphWhereAmIPanel", "GraphWhereAmIReadback", "GraphWhereAmIClose", "GraphStateHost", "GraphMenu", "GraphOpenTabMenuItem", "GraphOrphansMenuItem", "GraphUnresolvedMenuItem", "GraphMostLinkedMenuItem", "GraphWhereAmIMenuItem", "GraphVerbosityMenu", "GraphVerbosity."],
            ["Edit", "Text", "Button", "Group", "MenuItem"],
            ["Value", "Invoke", "Toggle", "ExpandCollapse"],
            ["GraphPhrase", "GraphFilterCount", "GraphWhereAmI", "GraphRow", "graph_verbosities"],
            ["GraphNavigatorTests", "GraphTableTests", "GraphMenuTests", "GraphPreferencesTests", "GraphConfigStoreTests", "GraphConfigWriterTests", "Censuses/GraphNavigatorCensus", "GraphSurfaces_NavigatorFilterAndWhereAmI_AreClean"],
            ["graph-navigator"]),
        // W6-2 PR D (D-19): the diagram — the container, the per-node peers,
        // the tier-B summary, the four verbs' menu items.
        new(
            "Graph diagram (W6-2 PR D)",
            ["GraphDiagram", "GraphNode:", "GraphTierSummary", "GraphZoomInMenuItem", "GraphZoomOutMenuItem", "GraphActualSizeMenuItem", "GraphFitGraphMenuItem"],
            ["Group", "Button", "MenuItem"],
            ["Value", "Selection", "SelectionItem", "Invoke", "ExpandCollapse"],
            ["GraphPhrase", "GraphRow", "GraphNeighborsContent", "GraphWhereAmI"],
            ["GraphDiagramTests", "HandleLifetimeCensus", "Censuses/GraphNavigatorCensus", "ThemeTokenContrastTests", "ParityHarnessCensus", "TheLayoutSectionIsTheSessionsSixtiethTickQuantised", "TwoLayoutsOverOneVaultAreBitIdentical", "GraphMenuTests", "ChordTableTests", "GraphSurfaces_DiagramPeersTiersAndZoom_AreClean"],
            ["graph-diagram"]),
        // W6-2 PR E (E-14): the inspector — the header's toggle, the leaf
        // body, the pane, the notices, the four sections and their controls.
        new(
            "Graph inspector (W6-2 PR E)",
            ["GraphInspectorToggle", "GraphInspectorBody", "GraphInspector", "GraphInspectorInactive", "GraphInspectorReadOnly", "GraphInspectorFilters", "GraphInspectorGroups", "GraphInspectorDisplay", "GraphInspectorForces", "GraphInspectorNameQuery", "GraphInspectorAttachments", "GraphInspectorGhosts", "GraphInspectorOrphans", "GraphInspectorNoGroups", "GraphInspectorAddGroup", "GraphInspectorGroupQuery:", "GraphInspectorGroupColour:", "GraphInspectorGroupRing:", "GraphInspectorRemoveGroup:", "GraphInspectorArrows", "GraphInspectorTextFade", "GraphInspectorNodeSize", "GraphInspectorLinkThickness", "GraphInspectorCenter", "GraphInspectorRepel", "GraphInspectorLink", "GraphInspectorLinkDistance", "GraphInspectorTextFadeValue", "GraphInspectorNodeSizeValue", "GraphInspectorLinkThicknessValue", "GraphInspectorCenterValue", "GraphInspectorRepelValue", "GraphInspectorLinkValue", "GraphInspectorLinkDistanceValue"],
            ["Group", "Button", "CheckBox", "Edit", "ComboBox", "Slider", "Text"],
            ["Toggle", "Invoke", "Value", "RangeValue", "Selection", "ExpandCollapse"],
            ["GraphPhrase", "GraphForceValue", "GraphLayoutSettled", "GraphFilterCount", "graph_color_tokens", "graph_ring_styles"],
            ["GraphInspectorTests", "GraphInspectorViewTests", "GraphPreferencesTests", "GraphDiagramTests", "GraphTableTests", "GraphAnnouncerTests", "MacCatalogParityTests", "Censuses/GraphNavigatorCensus", "Censuses/GraphAnnouncerCensus", "GraphInspector_FiltersGroupsAndForces_AreClean"],
            ["graph-inspector"]),
    ];

    private sealed record Row(string Title, string[] Cells);

    private static bool CellHas(string cell, string token) =>
        Regex.IsMatch(cell, @"(?<![A-Za-z0-9_])" + Regex.Escape(token) + @"(?![A-Za-z0-9_])");

    private static string RepoRoot => SourceText.RepoRoot();

    private static List<Row> GraphRows()
    {
        string matrix = File.ReadAllText(Path.Combine(RepoRoot, "docs", "plans", "18_windows_port", "w_c_matrix.md"));
        var rows = new List<Row>();
        foreach (string line in matrix.Split('\n'))
        {
            if (!line.StartsWith("| Graph ", StringComparison.Ordinal))
            {
                continue;
            }
            string[] cells = line.Trim().Trim('|').Split('|').Select(c => c.Trim()).ToArray();
            rows.Add(new Row(cells[0], cells));
        }
        return rows;
    }

    private static string TreeText(params string[] segments)
    {
        string root = Path.Combine([RepoRoot, .. segments]);
        var all = new System.Text.StringBuilder();
        foreach (string path in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(root, "*.xaml", SearchOption.AllDirectories)))
        {
            if (path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }
            all.Append(File.ReadAllText(path)).Append('\n');
        }
        return all.ToString();
    }

    private static string ShellText() => TreeText("apps", "slate-windows", "src", "SlateWindows");

    [Fact]
    public void EveryManifestSurfaceIsARowOfTenCellsAndNoGraphRowIsUnknown()
    {
        List<Row> rows = GraphRows();
        Assert.Equal(
            Manifest.Select(s => s.Title).OrderBy(t => t, StringComparer.Ordinal),
            rows.Select(r => r.Title).OrderBy(t => t, StringComparer.Ordinal));
        foreach (Row row in rows)
        {
            Assert.True(row.Cells.Length == 10, $"{row.Title}: {row.Cells.Length} cells, not ten — a column is lost on render");
            for (int i = 1; i < 7; i++)
            {
                Assert.False(string.IsNullOrWhiteSpace(row.Cells[i]), $"{row.Title}: cell {i} is empty");
            }
            foreach (int human in (int[])[7, 8, 9])
            {
                Assert.True(
                    row.Cells[human] == "Pending" || row.Cells[human].Contains("verified", StringComparison.OrdinalIgnoreCase),
                    $"{row.Title}: human cell {human} is neither Pending nor a recorded run: {row.Cells[human]}");
            }
        }
    }

    [Fact]
    public void TheIdsControlTypesPatternsAndNameSourcesAreTheSourcesOwn()
    {
        string shell = ShellText();
        var failures = new List<string>();
        foreach (Surface surface in Manifest)
        {
            Row row = Assert.Single(GraphRows(), r => r.Title == surface.Title);
            string controlCell = row.Cells[1];
            string nameCell = row.Cells[2];
            string patternCell = row.Cells[3];
            foreach (string id in surface.Ids)
            {
                // A `⟨slider id⟩Value` id is composed by SliderRow alone: the
                // allowance is the set its calls compose, not any literal
                // plus "Value" found anywhere in the shell (IPJ-1-6).
                bool composedValue = SliderValueIds().Contains(id);
                if (!shell.Contains($"\"{id}\"", StringComparison.Ordinal) && !composedValue)
                {
                    failures.Add($"{surface.Title}: the shell sets no automation id {id}");
                }
                if (!row.Cells.Any(c => c.Contains($"`{id}`", StringComparison.Ordinal)))
                {
                    failures.Add($"{surface.Title}: the row does not name `{id}`");
                }
            }
            foreach (string type in surface.NativeControlTypes)
            {
                if (!CellHas(controlCell, type))
                {
                    failures.Add($"{surface.Title}: the control-type cell lacks {type}");
                }
            }
            foreach (string pattern in surface.NativePatterns)
            {
                if (!CellHas(patternCell, pattern))
                {
                    failures.Add($"{surface.Title}: the patterns cell lacks {pattern}");
                }
            }
            foreach (string source in surface.NameSources)
            {
                if (!CellHas(nameCell, source))
                {
                    failures.Add($"{surface.Title}: the Name/HelpText cell lacks {source}");
                }
            }
        }
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void EveryEvidenceNameResolvesAndEveryAxeLabelIsScanned()
    {
        IReadOnlyList<TestEvidence> projects = TestEvidenceCompilation.Projects;
        var failures = new List<string>();
        foreach (Surface surface in Manifest)
        {
            Row row = Assert.Single(GraphRows(), r => r.Title == surface.Title);
            string evidence = row.Cells[6];
            foreach (string name in surface.Evidence)
            {
                if (!evidence.Contains($"`{name}`", StringComparison.Ordinal))
                {
                    failures.Add($"{surface.Title}: the evidence cell lacks `{name}`");
                }
            }
            foreach (Match backticked in Regex.Matches(evidence, "`([^`]+)`"))
            {
                string name = backticked.Groups[1].Value;
                bool test = projects.Any(project => project.HasTestEvidence(name));
                bool axe = surface.AxeLabels.Contains(name);
                bool fixture = TestEvidence.HasFixture(
                    Path.Combine(RepoRoot, "crates", "slate-core", "tests", "fixtures"), name);
                if (!test && !axe && !fixture)
                {
                    failures.Add($"{surface.Title}: `{name}` resolves to no executable xUnit test, class/file containing one, axe label or fixture");
                }
            }
            foreach (string label in surface.AxeLabels)
            {
                if (!evidence.Contains($"`{label}`", StringComparison.Ordinal))
                {
                    failures.Add($"{surface.Title}: the evidence cell lacks axe label `{label}`");
                }
                if (!projects.Any(project => project.HasAxeLabel(label)))
                {
                    failures.Add($"{surface.Title}: no executable test reaches a bound AssertAxeClean call for `{label}`");
                }
            }
        }
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }
    // --- W6-2 §F (F5, IGZ-3, IHB-2): every graph automation id has a row ------

    /// <summary>The one reviewed exclusion: the shell's right pane, W1's
    /// row, not a graph surface.</summary>
    private static readonly HashSet<string> ExcludedXamlIds = ["InspectorPane"];

    private static readonly Regex SetterLiteral = new(@"SetAutomationId\([^,]+,\s*""([^""]+)""\s*\)", RegexOptions.Compiled);

    /// <summary>A composed id: a literal prefix followed by `+` (the
    /// per-node, per-group, switcher and verbosity ids).</summary>
    private static readonly Regex SetterPrefix = new(@"SetAutomationId\([^,]+,\s*""([^""]+)""\s*\+", RegexOptions.Compiled);

    /// <summary>A helper that takes the id as its argument and sets it
    /// inside (the inspector's Section, Flag and SliderRow): the literal
    /// reaches the setter through the helper's `id` parameter.</summary>
    private static readonly Regex HelperLiteral = new(@"\b(?:Section|Flag|SliderRow)\([^,]+,\s*""([^""]+)""", RegexOptions.Compiled);

    private static readonly Regex XamlId = new(@"AutomationId=""((?:Graph|Connections)[^""]*)""", RegexOptions.Compiled);

    /// <summary>The ids the graph shell sets, in three forms: setter
    /// literals (through helpers too), composed prefixes, and the slider
    /// value peers (`⟨slider id⟩Value`, composed in SliderRow), plus the
    /// XAML hosts and menu items.</summary>
    private static (HashSet<string> Literals, HashSet<string> Prefixes) ShellIds()
    {
        var literals = new HashSet<string>(StringComparer.Ordinal);
        var prefixes = new HashSet<string>(StringComparer.Ordinal);
        string graph = Path.Combine(RepoRoot, "apps", "slate-windows", "src", "SlateWindows", "Graph");
        foreach (string path in GraphSources(graph))
        {
            string text = File.ReadAllText(path);
            foreach (Match m in SetterLiteral.Matches(text))
            {
                literals.Add(m.Groups[1].Value);
            }
            foreach (Match m in SetterPrefix.Matches(text))
            {
                prefixes.Add(m.Groups[1].Value);
            }
            foreach (Match m in HelperLiteral.Matches(text))
            {
                literals.Add(m.Groups[1].Value);
                if (m.Value.StartsWith("SliderRow(", StringComparison.Ordinal))
                {
                    literals.Add(m.Groups[1].Value + "Value");
                }
            }
        }
        // the verbosity menu composes its items from a named prefix constant
        string verbosity = File.ReadAllText(Path.Combine(graph, "GraphVerbosityMenu.cs"));
        Match prefix = Regex.Match(verbosity, @"AutomationIdPrefix = ""([^""]+)""");
        Assert.True(prefix.Success, "GraphVerbosityMenu no longer names its id prefix");
        prefixes.Add(prefix.Groups[1].Value);
        string xaml = File.ReadAllText(Path.Combine(RepoRoot, "apps", "slate-windows", "src", "SlateWindows", "MainWindow.xaml"));
        foreach (Match m in XamlId.Matches(xaml))
        {
            literals.Add(m.Groups[1].Value);
        }
        return (literals, prefixes);
    }

    /// <summary>The graph shell's sources, subfolders included and obj
    /// excluded (codoki's note on 045bc23c: a subfolder refactor must not
    /// hide an id from the census).</summary>
    private static IEnumerable<string> GraphSources(string graph) =>
        Directory.EnumerateFiles(graph, "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    /// <summary>The value peer's composition, <c>id + "Value"</c>, with or
    /// without spaces around the plus (codoki's note on 045bc23c).</summary>
    private static readonly Regex ComposesValue = new(@"\bid\s*\+\s*""Value""", RegexOptions.Compiled);

    /// <summary>The value-peer ids the inspector's SliderRow calls compose
    /// (<c>id + "Value"</c> inside the helper): one per
    /// <c>SliderRow(section, "id", …)</c> call in a file that composes.</summary>
    private static HashSet<string> SliderValueIds()
    {
        string graph = Path.Combine(RepoRoot, "apps", "slate-windows", "src", "SlateWindows", "Graph");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (string path in GraphSources(graph))
        {
            ids.UnionWith(ComposedValueIds(File.ReadAllText(path)));
        }
        return ids;
    }

    /// <summary>The value-peer ids one source text composes, bound in its
    /// syntax tree (IPJ-5-2): none unless the text DECLARES a SliderRow
    /// method whose body composes <c>id + "Value"</c>; else one per real
    /// SliderRow invocation whose second argument is a string literal —
    /// however the first argument nests, and never from a comment or a
    /// string.</summary>
    internal static HashSet<string> ComposedValueIds(string text)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        SyntaxNode root = CSharpSyntaxTree.ParseText(text).GetRoot();
        bool composes = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Any(method => method.Identifier.ValueText == "SliderRow" && ComposesValue.IsMatch(method.ToString()));
        if (!composes)
        {
            return ids;
        }
        foreach (InvocationExpressionSyntax invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            string callee = invocation.Expression switch
            {
                IdentifierNameSyntax name => name.Identifier.ValueText,
                MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
                _ => string.Empty,
            };
            if (callee != "SliderRow" || invocation.ArgumentList.Arguments.Count < 2)
            {
                continue;
            }
            if (invocation.ArgumentList.Arguments[1].Expression is LiteralExpressionSyntax literal
                && literal.RawKind == (int)SyntaxKind.StringLiteralExpression)
            {
                ids.Add(literal.Token.ValueText + "Value");
            }
        }
        return ids;
    }

    /// <summary>Codoki's third-round note and IPJ-5-2: the derivation over
    /// the shapes a refactor could take — a call spanning lines, a first
    /// argument that is itself a call with commas, one nested two deep,
    /// the composition without spaces — and over what must NOT count: a
    /// call in a comment, a call inside a string, a text whose SliderRow
    /// method does not compose, and a text where the composition sits in
    /// some other method.</summary>
    [Fact]
    public void ComposedValueIdsReadEveryCallShapeAndNothingWithoutTheComposition()
    {
        const string Composes = "AutomationProperties.SetAutomationId(valueText, id+\"Value\");";
        const string Declaration = "private Slider SliderRow(Panel section, string id, string title) { " + Composes + " }";
        string calls = string.Join("\n",
            "class V { void Build() {",
            "_a = SliderRow(_display, \"GraphInspectorA\", title, hint, 0, 1, v => Set(v));",
            "_b = SliderRow(",
            "    _forces,",
            "    \"GraphInspectorB\", title, hint, 0, 1, v => Set(v));",
            "_c = SliderRow(Section(_root, 2), \"GraphInspectorC\", title, hint, 0, 1, v => Set(v));",
            "_d = this.SliderRow(Section(Panel(_root, 1), Row(2, 3)), \"GraphInspectorD\", title, hint, 0, 1, v => Set(v));",
            "// _e = SliderRow(_forces, \"GraphInspectorE\", title, hint, 0, 1, v => Set(v));",
            "string f = \"SliderRow(_forces, \\\"GraphInspectorF\\\", title)\";",
            "} ");
        string[] expected = new[] { "GraphInspectorAValue", "GraphInspectorBValue", "GraphInspectorCValue", "GraphInspectorDValue" };
        Assert.Equal(expected, ComposedValueIds(calls + Declaration + " }").OrderBy(id => id, StringComparer.Ordinal).ToArray());
        // the SliderRow method does not compose
        Assert.Empty(ComposedValueIds(calls + Declaration.Replace(Composes, "AutomationProperties.SetAutomationId(valueText, id);") + " }"));
        // the composition sits in another method, not SliderRow's
        Assert.Empty(ComposedValueIds(calls + "private Slider SliderRow(Panel section, string id) { return null; } void Other(string id) { " + Composes + " } }"));
    }

    [Fact]
    public void EveryGraphAutomationIdIsInAManifestRow()
    {
        (HashSet<string> literals, HashSet<string> prefixes) = ShellIds();
        Assert.True(literals.Count >= 40, $"the id walk found only {literals.Count} literals — a regex moved");
        var manifestIds = Manifest.SelectMany(s => s.Ids).ToHashSet(StringComparer.Ordinal);
        var failures = new List<string>();
        foreach (string id in literals.Except(ExcludedXamlIds).OrderBy(i => i, StringComparer.Ordinal))
        {
            if (!manifestIds.Contains(id))
            {
                failures.Add($"the shell sets automation id {id} and no manifest row lists it");
            }
        }
        foreach (string prefix in prefixes.OrderBy(p => p, StringComparer.Ordinal))
        {
            if (!manifestIds.Contains(prefix))
            {
                failures.Add($"the shell composes ids on the prefix {prefix} and no manifest row lists it");
            }
        }
        foreach (string id in ExcludedXamlIds)
        {
            Assert.DoesNotContain(id, manifestIds);
        }
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }
}
