// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// §W-A differential-harness skeleton, Windows side (w0_spec §W0-3 item 5,
// #715): serialize → artifact → (diff elsewhere). Copies the fixture
// corpus into a temp vault (scans write .slate/ cache — fixtures stay
// pristine), runs the skeleton surfaces, and writes one canonical
// artifact per fixture plus vault-level search + links artifacts.
//
//   dotnet run --project apps/slate-windows/tools/ParityHarness -- \
//     --fixtures crates/slate-core/tests/fixtures/markdown --out <dir> \
//     [--canvas-fixtures crates/slate-core/tests/fixtures/canvas]
//     [--graph-fixtures crates/slate-core/tests/fixtures/graph_vault]
//
// `--canvas-fixtures` adds the W6-1 PR 0b `canvas_queries` section
// (§W-A). The canvas corpus gets its OWN temp vault: mixing it into the
// markdown vault would change what every other artifact sees.
//
// Diff against another platform's artifacts (or the committed goldens)
// with scripts/diff-parity-artifacts.py.

using ParityHarness;
using uniffi.slate_uniffi;

string? fixtures = null;
string? outDir = null;
string? scenarios = null;
string? canvasFixtures = null;
string? canvasScenarios = null;
string? graphFixtures = null;
bool mutations = args.Contains("--mutations");
for (int i = 0; i < args.Length - 1; i++)
{
    if (args[i] == "--fixtures")
    {
        fixtures = args[i + 1];
    }
    if (args[i] == "--out")
    {
        outDir = args[i + 1];
    }
    if (args[i] == "--scenarios")
    {
        scenarios = args[i + 1];
    }
    if (args[i] == "--canvas-fixtures")
    {
        canvasFixtures = args[i + 1];
    }
    if (args[i] == "--canvas-scenarios")
    {
        canvasScenarios = args[i + 1];
    }
    if (args[i] == "--graph-fixtures")
    {
        graphFixtures = args[i + 1];
    }
}

// W6-1 §E TE-11d (E18): the canvas scenario mode — scripts are data
// (shared verbatim with the Swift twin (SlateMacTests/CanvasScenarioTests.swift)); one artifact per scenario;
// the driver enforces E18's round-trip rules itself.
//
//   dotnet run --project apps/slate-windows/tools/ParityHarness -- //     --canvas-scenarios crates/slate-core/tests/fixtures/canvas_scenario_golden/scenarios.json //     --out crates/slate-core/tests/fixtures/canvas_scenario_golden
if (canvasScenarios != null)
{
    if (outDir == null)
    {
        Console.Error.WriteLine(
            "usage: ParityHarness --canvas-scenarios <scenarios.json> --out <dir>");
        return 2;
    }

    Directory.CreateDirectory(outDir);
    var canvasProduced = CanvasScenarioDriver.RunAll(canvasScenarios);
    foreach ((string name, string artifact) in canvasProduced)
    {
        File.WriteAllBytes(
            Path.Combine(outDir, name + ".json"),
            System.Text.Encoding.UTF8.GetBytes(artifact));
    }

    Console.WriteLine(
        $"canvas-scenario-harness: {canvasProduced.Count} artifacts -> {outDir}");
    return 0;
}

// W5-4 (#744) H1: the mutation mode — scenario scripts are data
// (shared verbatim with the Swift twin); one artifact per scenario.
//
//   dotnet run --project apps/slate-windows/tools/ParityHarness -- \
//     --mutations \
//     --scenarios crates/slate-core/tests/fixtures/mutation_golden/scenarios.json \
//     --out crates/slate-core/tests/fixtures/mutation_golden
if (mutations)
{
    if (scenarios == null || outDir == null)
    {
        Console.Error.WriteLine(
            "usage: ParityHarness --mutations --scenarios <scenarios.json> --out <dir>");
        return 2;
    }

    Directory.CreateDirectory(outDir);
    var produced = MutationDriver.RunAll(scenarios);
    foreach ((string name, string artifact) in produced)
    {
        File.WriteAllBytes(
            Path.Combine(outDir, name + ".json"),
            System.Text.Encoding.UTF8.GetBytes(artifact));
    }

    Console.WriteLine($"mutation-harness: {produced.Count} artifacts -> {outDir}");
    return 0;
}

if (fixtures == null || outDir == null)
{
    Console.Error.WriteLine("usage: ParityHarness --fixtures <dir> --out <dir>");
    return 2;
}

// EVERY fixture file enters the vault (W3-5: attachment and .canvas
// targets make embed resolution exercisable); artifacts are still
// generated per-.md only.
var allFiles = Directory.EnumerateFiles(fixtures)
    .Select(Path.GetFileName)
    .Where(f => f != null)
    .Select(f => f!)
    .OrderBy(f => f, StringComparer.Ordinal)
    .ToList();
var files = allFiles
    .Where(f => f.EndsWith(".md", StringComparison.Ordinal))
    .ToList();
if (files.Count == 0)
{
    Console.Error.WriteLine($"no .md fixtures under {fixtures}");
    return 2;
}

// Temp vault: fixtures copied byte-exact so the scan's .slate/ cache never
// lands in the checkout.
string vaultRoot = Path.Combine(Path.GetTempPath(), $"parity-harness-{Guid.NewGuid():N}");
Directory.CreateDirectory(vaultRoot);
try
{
    foreach (var f in allFiles)
    {
        File.Copy(Path.Combine(fixtures, f), Path.Combine(vaultRoot, f));
    }

    Directory.CreateDirectory(outDir);
    using var session = VaultSession.OpenFilesystem(vaultRoot);
    using var cancel = new CancelToken();
    session.ScanInitial(cancel);

    // W4-5: seed the bibliography from the vault's own citation config
    // BEFORE serializing anything — citation rendering, the per-file
    // `citations` sections, and the vault artifact all read the loaded
    // entries. This is the harness's only write and it lands in the temp
    // vault's cache DB. The Swift twin performs the identical call.
    BibLoadWarning[] bibWarnings =
        session.SetBibliographySources(session.CitationsPrefs().Sources);

    foreach (var f in files)
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine(vaultRoot, f));
        string text = System.Text.Encoding.UTF8.GetString(bytes);
        WriteArtifact(
            Path.Combine(outDir, f + ".json"),
            SurfaceSerializer.FileArtifact(f, text, session));
    }
    WriteArtifact(Path.Combine(outDir, "search.json"), SurfaceSerializer.SearchArtifact(session, cancel));
    WriteArtifact(Path.Combine(outDir, "links.json"), SurfaceSerializer.LinksArtifact(session, files));
    WriteArtifact(Path.Combine(outDir, "editor_scale.json"), SurfaceSerializer.EditorScaleArtifact());
    WriteArtifact(Path.Combine(outDir, "tasks.json"), SurfaceSerializer.TasksArtifact(session));
    WriteArtifact(
        Path.Combine(outDir, "properties.json"),
        SurfaceSerializer.PropertiesArtifact(session));
    WriteArtifact(
        Path.Combine(outDir, "bibliography.json"),
        SurfaceSerializer.BibliographyArtifact(session, bibWarnings));

    int artifacts = files.Count + 6;

    // W6-1 PR 0b (§W-A `canvas_queries`): the canvas corpus lives in a
    // DIFFERENT fixture directory, and it gets its own temp vault — a
    // canvas dropped into the markdown vault would change what every
    // other artifact sees (search hits, the file list) and move goldens
    // this section has no business touching.
    if (canvasFixtures != null)
    {
        (string queries, string read) = CanvasSections(canvasFixtures);
        WriteArtifact(Path.Combine(outDir, "canvas_queries.json"), queries);
        // W6-1 PR A (§W-A `canvas_read`, contract A20): the READ side of
        // the same corpus, from the same temp vault.
        WriteArtifact(Path.Combine(outDir, "canvas_read.json"), read);
        artifacts += 2;
    }

    // W6-2 PR 0b (§W-A `graph_queries`, contract 0b-13): the graph vault
    // is its own corpus with its own temp vault (0bD-4 revised) — the
    // witnesses the section needs are not in the markdown corpus, and
    // adding them there would perturb every earlier artifact.
    if (graphFixtures != null)
    {
        WriteArtifact(Path.Combine(outDir, "graph_queries.json"), GraphSection(graphFixtures));
        artifacts += 1;
    }

    Console.WriteLine($"parity-harness: {artifacts} artifacts -> {outDir}");
    return 0;
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
    catch (UnauthorizedAccessException)
    {
    }
}

static (string Queries, string Read) CanvasSections(string canvasFixtures)
{
    var canvasFiles = Directory.EnumerateFiles(canvasFixtures, "*.canvas")
        .Select(Path.GetFileName)
        .Where(f => f != null && !SurfaceSerializer.CanvasArtifactExclusions.Contains(f))
        .Select(f => f!)
        .OrderBy(f => f, StringComparer.Ordinal)
        .ToList();
    if (canvasFiles.Count == 0)
    {
        throw new InvalidOperationException($"no .canvas fixtures under {canvasFixtures}");
    }

    string root = Path.Combine(Path.GetTempPath(), $"parity-canvas-{Guid.NewGuid():N}");
    Directory.CreateDirectory(root);
    try
    {
        foreach (var f in canvasFiles)
        {
            File.Copy(Path.Combine(canvasFixtures, f), Path.Combine(root, f));
        }
        using var session = VaultSession.OpenFilesystem(root);
        using var cancel = new CancelToken();
        session.ScanInitial(cancel);
        return (
            SurfaceSerializer.CanvasQueriesArtifact(session, canvasFiles),
            SurfaceSerializer.CanvasReadArtifact(session, canvasFiles));
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

static string GraphSection(string graphFixtures)
{
    string root = Path.Combine(Path.GetTempPath(), $"parity-graph-{Guid.NewGuid():N}");
    try
    {
        SurfaceSerializer.CopyTree(graphFixtures, root);
        using var session = VaultSession.OpenFilesystem(root);
        using var cancel = new CancelToken();
        session.ScanInitial(cancel);
        return SurfaceSerializer.GraphQueriesArtifact(session);
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

static void WriteArtifact(string path, string content)
{
    // Byte-exact: UTF-8, no BOM, LF only (the serializer never emits \r).
    File.WriteAllBytes(path, System.Text.Encoding.UTF8.GetBytes(content));
}
