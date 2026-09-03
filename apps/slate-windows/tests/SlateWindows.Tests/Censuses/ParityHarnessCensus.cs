// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// §W-A harness skeleton census (w0_spec §W0-3 item 5, #715): the Windows
// harness output over the markdown fixture corpus is byte-identical to
// the committed goldens (crates/slate-core/tests/fixtures/parity_golden/).
// The mac twin (ParityHarnessTests.swift) asserts the same goldens, so a
// green run on both CIs proves cross-platform byte-identity transitively
// — the W8-4 three-job pipeline replaces this with a direct diff over the
// full surface. Line endings are inside the corpus on purpose (CRLF and
// mixed fixtures) and are never normalized (§W-A / decision 9).

using ParityHarness;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "parity-skeleton")]
public class ParityHarnessCensus
{
    private static string RepoRoot
    {
        get
        {
            // tests/.../bin/<cfg>/net10.0 -> repo root is six levels above
            // apps/slate-windows/tests/SlateWindows.Tests.
            string dir = AppContext.BaseDirectory;
            for (int i = 0; i < 8; i++)
            {
                dir = Path.GetDirectoryName(dir)!;
            }
            return dir;
        }
    }

    private static string FixturesDir =>
        Path.Combine(RepoRoot, "crates", "slate-core", "tests", "fixtures", "markdown");

    private static string CanvasFixturesDir =>
        Path.Combine(RepoRoot, "crates", "slate-core", "tests", "fixtures", "canvas");

    private static string GraphFixturesDir =>
        Path.Combine(RepoRoot, "crates", "slate-core", "tests", "fixtures", "graph_vault");

    private static string GoldenDir =>
        Path.Combine(RepoRoot, "crates", "slate-core", "tests", "fixtures", "parity_golden");

    [Fact]
    public void HarnessArtifacts_MatchCommittedGoldensByteForByte()
    {
        Assert.True(Directory.Exists(FixturesDir), $"fixtures missing at {FixturesDir}");
        Assert.True(Directory.Exists(GoldenDir), $"goldens missing at {GoldenDir}");

        string outDir = Path.Combine(Path.GetTempPath(), $"parity-census-{Guid.NewGuid():N}");
        try
        {
            RunHarness(outDir);

            var goldenFiles = Directory.EnumerateFiles(GoldenDir, "*.json")
                .Select(Path.GetFileName)
                .OrderBy(f => f, StringComparer.Ordinal)
                .ToList();
            var producedFiles = Directory.EnumerateFiles(outDir, "*.json")
                .Select(Path.GetFileName)
                .OrderBy(f => f, StringComparer.Ordinal)
                .ToList();
            Assert.Equal(goldenFiles, producedFiles);

            foreach (var name in goldenFiles)
            {
                byte[] golden = File.ReadAllBytes(Path.Combine(GoldenDir, name!));
                byte[] produced = File.ReadAllBytes(Path.Combine(outDir, name!));
                Assert.True(
                    golden.SequenceEqual(produced),
                    $"artifact {name} differs from golden (regenerate deliberately if the surface changed: " +
                    "dotnet run --project apps/slate-windows/tools/ParityHarness -- " +
                    "--fixtures crates/slate-core/tests/fixtures/markdown " +
                    "--canvas-fixtures crates/slate-core/tests/fixtures/canvas " +
                    "--out crates/slate-core/tests/fixtures/parity_golden)");
            }
        }
        finally
        {
            try
            {
                Directory.Delete(outDir, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    [Fact]
    public void HarnessIsDeterministic_TwoRunsProduceIdenticalBytes()
    {
        string outA = Path.Combine(Path.GetTempPath(), $"parity-a-{Guid.NewGuid():N}");
        string outB = Path.Combine(Path.GetTempPath(), $"parity-b-{Guid.NewGuid():N}");
        try
        {
            RunHarness(outA);
            RunHarness(outB);
            foreach (var file in Directory.EnumerateFiles(outA, "*.json"))
            {
                string name = Path.GetFileName(file);
                Assert.True(
                    File.ReadAllBytes(file).SequenceEqual(File.ReadAllBytes(Path.Combine(outB, name))),
                    $"artifact {name} not deterministic across runs");
            }
        }
        finally
        {
            foreach (var d in new[] { outA, outB })
            {
                try
                {
                    Directory.Delete(d, recursive: true);
                }
                catch (IOException)
                {
                }
            }
        }
    }

    private static void RunHarness(string outDir)
    {
        // EVERY fixture file enters the vault (W3-5: attachment and
        // .canvas targets make embed resolution exercisable);
        // artifacts are still generated per-.md only.
        var allFiles = Directory.EnumerateFiles(FixturesDir)
            .Select(Path.GetFileName)
            .Select(f => f!)
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();
        var files = allFiles
            .Where(f => f.EndsWith(".md", StringComparison.Ordinal))
            .ToList();
        Assert.NotEmpty(files);

        string vaultRoot = Path.Combine(Path.GetTempPath(), $"parity-vault-{Guid.NewGuid():N}");
        Directory.CreateDirectory(vaultRoot);
        try
        {
            foreach (var f in allFiles)
            {
                File.Copy(Path.Combine(FixturesDir, f), Path.Combine(vaultRoot, f));
            }
            Directory.CreateDirectory(outDir);
            using var session = VaultSession.OpenFilesystem(vaultRoot);
            using var cancel = new CancelToken();
            session.ScanInitial(cancel);

            // W4-5: identical to the harness — the bibliography must be
            // seeded before any artifact, or citation renders degrade and
            // the census would disagree with the committed goldens.
            BibLoadWarning[] bibWarnings =
                session.SetBibliographySources(session.CitationsPrefs().Sources);

            foreach (var f in files)
            {
                string text = System.Text.Encoding.UTF8.GetString(
                    File.ReadAllBytes(Path.Combine(vaultRoot, f)));
                File.WriteAllBytes(
                    Path.Combine(outDir, f + ".json"),
                    System.Text.Encoding.UTF8.GetBytes(
                        SurfaceSerializer.FileArtifact(f, text, session)));
            }
            File.WriteAllBytes(
                Path.Combine(outDir, "search.json"),
                System.Text.Encoding.UTF8.GetBytes(SurfaceSerializer.SearchArtifact(session, cancel)));
            File.WriteAllBytes(
                Path.Combine(outDir, "links.json"),
                System.Text.Encoding.UTF8.GetBytes(SurfaceSerializer.LinksArtifact(session, files)));
            File.WriteAllBytes(
                Path.Combine(outDir, "editor_scale.json"),
                System.Text.Encoding.UTF8.GetBytes(SurfaceSerializer.EditorScaleArtifact()));
            File.WriteAllBytes(
                Path.Combine(outDir, "tasks.json"),
                System.Text.Encoding.UTF8.GetBytes(SurfaceSerializer.TasksArtifact(session)));
            File.WriteAllBytes(
                Path.Combine(outDir, "properties.json"),
                System.Text.Encoding.UTF8.GetBytes(SurfaceSerializer.PropertiesArtifact(session)));
            File.WriteAllBytes(
                Path.Combine(outDir, "bibliography.json"),
                System.Text.Encoding.UTF8.GetBytes(
                    SurfaceSerializer.BibliographyArtifact(session, bibWarnings)));
        }
        finally
        {
            try
            {
                Directory.Delete(vaultRoot, recursive: true);
            }
            catch (IOException)
            {
            }
        }

        RunCanvasQueries(outDir);
        RunGraphQueries(outDir);
    }

    /// <summary>
    /// W6-2 PR 0b (§W-A `graph_queries`, contract 0b-13), identical to
    /// <c>Program.cs</c>'s graph pass: the graph vault is its own corpus
    /// with its own temp vault (0bD-4 revised).
    /// </summary>
    private static void RunGraphQueries(string outDir)
    {
        string root = Path.Combine(Path.GetTempPath(), $"parity-graph-census-{Guid.NewGuid():N}");
        try
        {
            SurfaceSerializer.CopyTree(GraphFixturesDir, root);
            using var session = VaultSession.OpenFilesystem(root);
            using var cancel = new CancelToken();
            session.ScanInitial(cancel);
            File.WriteAllBytes(
                Path.Combine(outDir, "graph_queries.json"),
                System.Text.Encoding.UTF8.GetBytes(SurfaceSerializer.GraphQueriesArtifact(session)));
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    /// <summary>
    /// W6-1 PR 0b (§W-A `canvas_queries`), identical to
    /// <c>Program.cs</c>'s canvas pass: the canvas corpus lives in a
    /// different fixture directory and gets its OWN temp vault, because
    /// a canvas dropped into the markdown vault would change what every
    /// other artifact sees.
    /// </summary>
    private static void RunCanvasQueries(string outDir)
    {
        var canvasFiles = Directory.EnumerateFiles(CanvasFixturesDir, "*.canvas")
            .Select(Path.GetFileName)
            .Where(f => f != null && !SurfaceSerializer.CanvasArtifactExclusions.Contains(f))
            .Select(f => f!)
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();
        Assert.NotEmpty(canvasFiles);

        string root = Path.Combine(Path.GetTempPath(), $"parity-canvas-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            foreach (var f in canvasFiles)
            {
                File.Copy(Path.Combine(CanvasFixturesDir, f), Path.Combine(root, f));
            }
            using var session = VaultSession.OpenFilesystem(root);
            using var cancel = new CancelToken();
            session.ScanInitial(cancel);
            File.WriteAllBytes(
                Path.Combine(outDir, "canvas_queries.json"),
                System.Text.Encoding.UTF8.GetBytes(
                    SurfaceSerializer.CanvasQueriesArtifact(session, canvasFiles)));
            // W6-1 PR A (§W-A `canvas_read`, contract A20).
            File.WriteAllBytes(
                Path.Combine(outDir, "canvas_read.json"),
                System.Text.Encoding.UTF8.GetBytes(
                    SurfaceSerializer.CanvasReadArtifact(session, canvasFiles)));
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    /// <summary>The graph vault's stable-key inventory (contract 0b-13),
    /// fixture-owned: the artifact is compared against THIS list, never
    /// against itself, so a serializer that drops a node fails here.</summary>
    public static readonly string[] GraphVaultInventory =
    {
        "g:café",
        "g:ghost%20one",
        "g:ghost%20two",
        "g:i%CC%87stanbul",
        "p:010.md",
        "p:10.md",
        "p:2.md",
        "p:hub.md",
        "p:notes/nested/deep.md",
        "p:orphan.md",
        "p:pic.png",
        "p:self.md",
    };

    /// <summary>W6-2 PR 0b (contract 0b-13, design C): the graph artifact's
    /// snapshot LIST equals the fixture-owned inventory, and the topology's
    /// and every table sort's key SETS equal it — equality, not containment,
    /// never the artifact against itself.</summary>
    [Fact]
    public void GraphQueriesArtifactMatchesTheVaultInventory()
    {
        using var document = System.Text.Json.JsonDocument.Parse(
            File.ReadAllBytes(Path.Combine(GoldenDir, "graph_queries.json")));
        var root = document.RootElement;
        var inventory = GraphVaultInventory.ToHashSet(StringComparer.Ordinal);
        Assert.Equal(GraphVaultInventory.Length, inventory.Count);
        var snapshotKeys = root.GetProperty("snapshot").EnumerateArray()
            .Select(entry => entry.GetProperty("key").GetString()!)
            .ToList();
        Assert.Equal(GraphVaultInventory, snapshotKeys);
        // Exact sorted LISTS (design C): a duplicate or a missing entry
        // fails; the inventory is byte-ordered, so the sort is UTF-8's.
        var utf8 = Comparer<string>.Create(SurfaceSerializer.CompareUtf8);
        var topologyKeys = root.GetProperty("topology").GetProperty("nodes").EnumerateArray()
            .Select(entry => entry.GetProperty("key").GetString()!)
            .OrderBy(k => k, utf8)
            .ToList();
        Assert.Equal(GraphVaultInventory, topologyKeys);
        foreach (var sort in root.GetProperty("table").EnumerateArray())
        {
            var rowKeys = sort.GetProperty("rows").EnumerateArray()
                .Select(row => row.GetProperty("key").GetString()!)
                .OrderBy(k => k, utf8)
                .ToList();
            Assert.Equal(GraphVaultInventory, rowKeys);
        }
    }

    /// <summary>The artifact's eleven sections, in order (contract 0b-13).</summary>
    public static readonly string[] GraphArtifactSections =
    {
        "snapshot", "visibility", "topology", "table", "connections", "ghost_paths",
        "spatial", "structural", "constants", "actions", "config",
    };

    /// <summary>W6-2 PR 0b (contract 0b-13, design C): the top-level key set is
    /// exactly the eleven sections, each section's length equals its pinned
    /// vector's, and each entry's pin fields equal the pin in order — so a
    /// dropped section, a dropped vector entry or a reordered section fails
    /// here.</summary>
    [Fact]
    public void GraphQueriesArtifactCarriesThePinnedVectors()
    {
        using var document = System.Text.Json.JsonDocument.Parse(
            File.ReadAllBytes(Path.Combine(GoldenDir, "graph_queries.json")));
        var root = document.RootElement;
        Assert.Equal(GraphArtifactSections, root.EnumerateObject().Select(p => p.Name).ToArray());
        // The committed example carries the same shape (0b-13).
        using var example = System.Text.Json.JsonDocument.Parse(
            File.ReadAllBytes(Path.Combine(RepoRoot, "crates", "slate-core", "tests", "fixtures", "graph_queries.example.json")));
        Assert.Equal(GraphArtifactSections, example.RootElement.EnumerateObject().Select(p => p.Name).ToArray());

        var visibility = root.GetProperty("visibility").EnumerateArray().ToList();
        Assert.Equal(SurfaceSerializer.PinnedGraphQueries.Select(q => q.Name).ToArray(),
            visibility.Select(v => v.GetProperty("query").GetString()!).ToArray());

        var table = root.GetProperty("table").EnumerateArray().ToList();
        Assert.Equal(SurfaceSerializer.PinnedGraphTableSorts.Select(s => s.Name).ToArray(),
            table.Select(t => t.GetProperty("sort").GetString()!).ToArray());
        Assert.All(table, t => Assert.Equal("all", t.GetProperty("query").GetString()));

        var connections = root.GetProperty("connections").EnumerateArray().ToList();
        Assert.Equal(SurfaceSerializer.PinnedGraphConnections.Select(c => $"{c.Path}@{c.Depth}").ToArray(),
            connections.Select(c => $"{c.GetProperty("path").GetString()}@{c.GetProperty("depth").GetUInt32()}").ToArray());

        var ghosts = root.GetProperty("ghost_paths").EnumerateArray().ToList();
        Assert.Equal(SurfaceSerializer.PinnedGraphGhostTargets,
            ghosts.Select(g => g.GetProperty("target").GetString()!).ToArray());

        var spatial = root.GetProperty("spatial").EnumerateArray().ToList();
        Assert.Equal(SurfaceSerializer.PinnedGraphSpatialDirections.Select(d => $"{d.Dx},{d.Dy}").ToArray(),
            spatial.Select(s => $"{s.GetProperty("dx").GetInt64()},{s.GetProperty("dy").GetInt64()}").ToArray());
        Assert.All(spatial, s => Assert.Equal("a", s.GetProperty("from").GetString()));

        var structural = root.GetProperty("structural").EnumerateArray().ToList();
        var expectedStructural = SurfaceSerializer.PinnedGraphStructuralOrder.Select(l => (string?)l).Append(null)
            .SelectMany(from => new[] { $"{from ?? "-"}:true", $"{from ?? "-"}:false" })
            .ToArray();
        Assert.Equal(expectedStructural,
            structural.Select(s => $"{s.GetProperty("from").GetString() ?? "-"}:{(s.GetProperty("forward").GetBoolean() ? "true" : "false")}").ToArray());

        Assert.Equal(new[] { "note", "attachment", "ghost" },
            root.GetProperty("actions").EnumerateArray().Select(a => a.GetProperty("kind").GetString()!).ToArray());
        Assert.Equal(GraphVaultInventory.Length, root.GetProperty("topology").GetProperty("nodes").GetArrayLength());
        Assert.Equal(GraphConstantsFields,
            root.GetProperty("constants").EnumerateObject().Select(p => p.Name).ToArray());
        Assert.Equal(SurfaceSerializer.PinnedGraphConfigInput, root.GetProperty("config").GetProperty("input").GetString());
        Assert.Equal(GraphConfigFields,
            root.GetProperty("config").EnumerateObject().Select(p => p.Name).ToArray());
    }

    /// <summary>The constants section's field names, pinned (0b-13).</summary>
    public static readonly string[] GraphConstantsFields =
    {
        "tier_b_threshold", "label_cap", "connections_depth_min", "connections_depth_max",
        "node_diameter_min", "node_diameter_max", "neighbor_label_cap", "diameter_at_0", "diameter_at_1000000",
    };

    /// <summary>The config section's field names, pinned (0b-13).</summary>
    public static readonly string[] GraphConfigFields =
    {
        "input", "unknown_json", "include_attachments", "name_query", "group_count", "group_tags",
        "palette", "ring_styles", "modes", "verbosities", "connections_depth", "mode", "verbosity",
        "repel_x100", "center_x100", "link_x100", "node_size_x100", "encoded", "encoded_fresh",
    };

    /// <summary>The names a number may sit under anywhere in the artifact
    /// (design C's schema walk); a number under any other name is an id in
    /// disguise.</summary>
    public static readonly HashSet<string> GraphNumericNames = new(StringComparer.Ordinal)
    {
        "in_links", "out_links", "in_embeds", "out_embeds", "component", "total", "level", "references",
        "depth", "tree_depth", "note_count", "diameter_x100", "group", "dx", "dy", "group_count",
        "connections_depth", "repel_x100", "center_x100", "link_x100", "node_size_x100",
        "tier_b_threshold", "label_cap", "connections_depth_min", "connections_depth_max",
        "node_diameter_min", "node_diameter_max", "neighbor_label_cap", "diameter_at_0", "diameter_at_1000000",
    };

    /// <summary>The names whose STRING value must be an inventory key, and the
    /// array names whose every element must be one (design C).</summary>
    public static readonly HashSet<string> GraphKeyNames = new(StringComparer.Ordinal)
    {
        "key", "center_key", "from_key", "to_key",
    };

    public static readonly HashSet<string> GraphKeyListNames = new(StringComparer.Ordinal)
    {
        "visible", "labeled", "neighbors",
    };

    /// <summary>W6-2 PR 0b (contract 0b-13, design C): the schema walk — every
    /// value of the golden is validated by the name it sits under: a key-bearing
    /// value is an inventory key, an occurrence is `in|out` plus encoded
    /// inventory keys, a number sits under an allow-listed name, and nothing
    /// else is a number. A numeric id under any name fails here.</summary>
    [Fact]
    public void GraphQueriesArtifactMatchesItsSchema()
    {
        using var document = System.Text.Json.JsonDocument.Parse(
            File.ReadAllBytes(Path.Combine(GoldenDir, "graph_queries.json")));
        var inventory = GraphVaultInventory.ToHashSet(StringComparer.Ordinal);
        var encoded = GraphVaultInventory.ToDictionary(k => GraphPercentEncode(k), k => k, StringComparer.Ordinal);
        int keyValues = 0;
        int numbers = 0;
        void Walk(System.Text.Json.JsonElement element, string name, string path)
        {
            switch (element.ValueKind)
            {
                case System.Text.Json.JsonValueKind.Object:
                    foreach (var property in element.EnumerateObject())
                    {
                        Walk(property.Value, property.Name, path + "/" + property.Name);
                    }
                    break;
                case System.Text.Json.JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                    {
                        Walk(item, GraphKeyListNames.Contains(name) ? "key" : name + "[]", path + "[]");
                    }
                    break;
                case System.Text.Json.JsonValueKind.Number:
                    numbers++;
                    Assert.True(GraphNumericNames.Contains(name), $"a number under {path}: an id in disguise");
                    break;
                case System.Text.Json.JsonValueKind.String:
                    string text = element.GetString()!;
                    if (GraphKeyNames.Contains(name))
                    {
                        keyValues++;
                        Assert.True(inventory.Contains(text), $"{path} = {text} is not an inventory key");
                    }
                    else if (name == "occurrence" || name == "parent")
                    {
                        keyValues++;
                        string[] segments = text.Split('/');
                        Assert.True(segments[0] == "in" || segments[0] == "out", $"{path} = {text}");
                        Assert.True(segments.Length >= 2, $"{path} = {text} names no key");
                        foreach (string segment in segments.Skip(1))
                        {
                            Assert.True(encoded.ContainsKey(segment), $"{path}: {segment} is not an encoded inventory key");
                        }
                    }
                    break;
                default:
                    break;
            }
        }
        Walk(document.RootElement, "", "");
        Assert.True(keyValues > 100, $"the walk saw only {keyValues} key-bearing values");
        Assert.True(numbers > 50, $"the walk saw only {numbers} numbers");
        // The parent rule (design C): null exactly at level 1; otherwise an
        // EARLIER occurrence of the same list that prefixes the row's own.
        int rows = 0;
        foreach (var tree in document.RootElement.GetProperty("connections").EnumerateArray())
        {
            foreach (string list in new[] { "incoming", "outgoing" })
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var row in tree.GetProperty(list).EnumerateArray())
                {
                    rows++;
                    string occurrence = row.GetProperty("occurrence").GetString()!;
                    uint level = row.GetProperty("level").GetUInt32();
                    var parent = row.GetProperty("parent");
                    if (level == 1)
                    {
                        Assert.Equal(System.Text.Json.JsonValueKind.Null, parent.ValueKind);
                    }
                    else
                    {
                        string parentId = parent.GetString()!;
                        Assert.True(seen.Contains(parentId), $"{occurrence}: the parent {parentId} did not appear earlier");
                        Assert.StartsWith(parentId + "/", occurrence, StringComparison.Ordinal);
                    }
                    Assert.True(seen.Add(occurrence), $"duplicate occurrence {occurrence}");
                }
            }
        }
        Assert.True(rows > 10, $"the walk saw only {rows} tree rows");
    }

    /// <summary>0b-3's percent-encoder, for the occurrence ids: every UTF-8
    /// byte of a non-alphanumeric character as `%XX`.</summary>
    public static string GraphPercentEncode(string key)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var rune in key.EnumerateRunes())
        {
            if (System.Text.Rune.IsLetterOrDigit(rune))
            {
                sb.Append(rune.ToString());
            }
            else
            {
                foreach (byte b in System.Text.Encoding.UTF8.GetBytes(rune.ToString()))
                {
                    sb.Append('%').Append(b.ToString("X2"));
                }
            }
        }
        return sb.ToString();
    }

    /// <summary>The property names a scan-order index could hide behind — every
    /// numeric-id field the query records carry; the artifact must carry none
    /// (contract 0b-13, 0bR-2, design C).</summary>
    public static readonly string[] GraphForbiddenProperties =
    {
        "id", "node_id", "parent_id", "center_id", "source_id", "target_id", "generation",
    };

    /// <summary>W6-2 PR 0b (contract 0b-13): the graph artifact carries no
    /// numeric node id under any name — every identity is a stable key — and
    /// the tree rows are keyed by occurrence.</summary>
    [Fact]
    public void GraphQueriesArtifactCarriesNoNodeIds()
    {
        string text = File.ReadAllText(Path.Combine(GoldenDir, "graph_queries.json"));
        foreach (string property in GraphForbiddenProperties)
        {
            Assert.DoesNotContain("\"" + property + "\":", text);
        }
        Assert.Contains("\"occurrence\":", text);
        Assert.Contains("\"parent\":", text);
    }

    /// <summary>§H TH-3 (H3, IH-36): the read half of §W-A cannot skip a
    /// canvas fixture in silence — every <c>fixtures/canvas/*.canvas</c>
    /// not in <c>CanvasArtifactExclusions</c> is an entry of BOTH committed
    /// canvas artifacts, and every exclusion is a file that exists (a
    /// stale name in the list would hide nothing and mean nothing).</summary>
    [Fact]
    public void EveryCanvasFixtureIsReadOrExcluded()
    {
        var fixtures = Directory.EnumerateFiles(CanvasFixturesDir, "*.canvas")
            .Select(Path.GetFileName)
            .Select(name => name!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
        Assert.NotEmpty(fixtures);
        foreach (string excluded in SurfaceSerializer.CanvasArtifactExclusions)
        {
            Assert.Contains(excluded, fixtures);
        }
        foreach (string artifact in (string[])["canvas_read.json", "canvas_queries.json"])
        {
            using var document = System.Text.Json.JsonDocument.Parse(
                File.ReadAllBytes(Path.Combine(GoldenDir, artifact)));
            var listed = document.RootElement.GetProperty("canvases")
                .EnumerateArray()
                .Select(entry => entry.GetProperty("file").GetString()!)
                .ToHashSet(StringComparer.Ordinal);
            foreach (string fixture in fixtures)
            {
                bool excluded = SurfaceSerializer.CanvasArtifactExclusions.Contains(fixture);
                Assert.True(
                    listed.Contains(fixture) == !excluded,
                    excluded
                        ? $"{artifact} lists the excluded fixture {fixture}"
                        : $"{artifact} skips the fixture {fixture} in silence — regenerate the goldens");
            }
        }
    }

}
