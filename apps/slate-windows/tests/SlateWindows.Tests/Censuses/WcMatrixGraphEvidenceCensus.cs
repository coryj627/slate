// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.RegularExpressions;

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
            ["GraphSurface", "GraphSurfaceSwitcher", "GraphStateText", "GraphTableGrid"],
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

    private static string TestText() => TreeText("apps", "slate-windows", "tests");

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
                if (!shell.Contains($"\"{id}\"", StringComparison.Ordinal))
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
        string tests = TestText();
        string shell = ShellText();
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
                bool method = Regex.IsMatch(tests, @"\b(void|Task)\s+" + Regex.Escape(name) + @"\s*\(");
                bool type = Regex.IsMatch(tests, @"\bclass\s+" + Regex.Escape(name) + @"\b");
                bool axe = surface.AxeLabels.Contains(name);
                bool pathLike = name.Contains('/', StringComparison.Ordinal);
                bool testFile = pathLike && Directory
                    .EnumerateFiles(Path.Combine(RepoRoot, "apps", "slate-windows", "tests"), "*.cs", SearchOption.AllDirectories)
                    .Any(p => p.Replace('\\', '/').EndsWith("/" + name + ".cs", StringComparison.Ordinal));
                bool fixture = !pathLike && Directory
                    .EnumerateFiles(Path.Combine(RepoRoot, "crates", "slate-core", "tests", "fixtures"), name + ".*", SearchOption.AllDirectories)
                    .Any();
                if (!method && !type && !axe && !testFile && !fixture)
                {
                    failures.Add($"{surface.Title}: `{name}` resolves to no fact, journey, test class, axe label or fixture");
                }
            }
            foreach (string label in surface.AxeLabels)
            {
                if (!evidence.Contains($"`{label}`", StringComparison.Ordinal))
                {
                    failures.Add($"{surface.Title}: the evidence cell lacks axe label `{label}`");
                }
                if (!tests.Contains($"AssertAxeClean(process, \"{label}\")", StringComparison.Ordinal))
                {
                    failures.Add($"{surface.Title}: no journey scans `{label}`");
                }
            }
        }
        _ = shell;
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }
}
