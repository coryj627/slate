// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// W5-4 (#744) H1: the mutation-harness census — the Windows half of
// the two-oracle mechanism. The scenario driver re-runs in-process
// and its artifacts are byte-compared against the committed goldens
// (crates/slate-core/tests/fixtures/mutation_golden/); the Swift twin
// (MutationHarnessTests.swift) asserts the SAME goldens, so a green
// run on both CIs proves cross-platform byte-identity transitively.
// The driver itself enforces the H5 invariants (checkpoint pair
// equality, untouched sets, typed refusals, S2 idempotence) on every
// run — a violation throws before any byte comparison.

using ParityHarness;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "mutation-harness")]
public class MutationHarnessCensus
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

    private static string GoldenDir => Path.Combine(
        RepoRoot, "crates", "slate-core", "tests", "fixtures", "mutation_golden");

    private static string ScenariosPath =>
        Path.Combine(GoldenDir, "scenarios.json");

    [Fact]
    public void MutationArtifacts_MatchCommittedGoldensByteForByte()
    {
        Assert.True(File.Exists(ScenariosPath), $"scenarios missing at {ScenariosPath}");

        // The fault env vars are process-global; two parallel tests
        // setting different triggers overwrite each other.
        using IDisposable envLock = EnvFaultLock.Acquire();
        IReadOnlyList<(string Name, string Artifact)> produced =
            MutationDriver.RunAll(ScenariosPath);

        var goldenNames = Directory.EnumerateFiles(GoldenDir, "*.json")
            .Select(Path.GetFileName)
            .Where(name => name != "scenarios.json")
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
        Assert.Equal(
            goldenNames,
            produced.Select(item => item.Name + ".json")
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList());

        foreach ((string name, string artifact) in produced)
        {
            byte[] golden = File.ReadAllBytes(
                Path.Combine(GoldenDir, name + ".json"));
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(artifact);
            Assert.True(
                golden.SequenceEqual(bytes),
                $"artifact {name} differs from golden (regenerate deliberately if "
                + "the surface changed: dotnet run --project "
                + "apps/slate-windows/tools/ParityHarness -- --mutations "
                + "--scenarios crates/slate-core/tests/fixtures/mutation_golden/scenarios.json "
                + "--out crates/slate-core/tests/fixtures/mutation_golden)");
        }
    }

    [Fact]
    public void MutationHarnessIsDeterministic_TwoRunsProduceIdenticalBytes()
    {
        using IDisposable envLock = EnvFaultLock.Acquire();
        IReadOnlyList<(string Name, string Artifact)> first =
            MutationDriver.RunAll(ScenariosPath);
        IReadOnlyList<(string Name, string Artifact)> second =
            MutationDriver.RunAll(ScenariosPath);
        Assert.Equal(first.Count, second.Count);
        for (int i = 0; i < first.Count; i++)
        {
            Assert.Equal(first[i].Name, second[i].Name);
            Assert.True(
                first[i].Artifact == second[i].Artifact,
                $"artifact {first[i].Name} not deterministic across runs");
        }
    }

    /// <summary>§H TH-3 (H3, IH-36): the canvas mutation half of §W-A
    /// cannot skip a fixture in silence, and cannot cover one with a
    /// no-op. Every <c>fixtures/canvas/*.canvas</c> is either the fixture
    /// of at least one scenario in <c>canvas_scenario_golden/scenarios.json</c>
    /// or a key of its <c>excluded</c> map with a non-empty reason (and an
    /// exclusion names a file that exists); and every scenario's committed
    /// artifact holds at least one step and a terminal hash that differs
    /// from the original's — a mutation that changed the bytes before the
    /// inverse walk restored them.</summary>
    [Fact]
    public void EveryCanvasFixtureIsScriptedOrExcluded()
    {
        string canvasGolden = Path.Combine(
            RepoRoot, "crates", "slate-core", "tests", "fixtures", "canvas_scenario_golden");
        string scenariosPath = Path.Combine(canvasGolden, "scenarios.json");
        string canvasFixtures = Path.Combine(
            RepoRoot, "crates", "slate-core", "tests", "fixtures", "canvas");
        using var scenarios = System.Text.Json.JsonDocument.Parse(File.ReadAllBytes(scenariosPath));
        var scripted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var names = new List<string>();
        foreach (var scenario in scenarios.RootElement.GetProperty("scenarios").EnumerateArray())
        {
            names.Add(scenario.GetProperty("name").GetString()!);
            scripted.Add(Path.GetFullPath(Path.Combine(
                canvasGolden, scenario.GetProperty("fixture").GetString()!)));
        }
        var excluded = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (scenarios.RootElement.TryGetProperty("excluded", out var map))
        {
            foreach (var entry in map.EnumerateObject())
            {
                string full = Path.GetFullPath(Path.Combine(canvasGolden, entry.Name));
                Assert.True(File.Exists(full), $"the exclusion {entry.Name} names no fixture");
                Assert.False(
                    string.IsNullOrWhiteSpace(entry.Value.GetString()),
                    $"the exclusion {entry.Name} carries no reason");
                excluded[full] = entry.Value.GetString()!;
            }
        }
        foreach (string fixture in Directory.EnumerateFiles(canvasFixtures, "*.canvas"))
        {
            string full = Path.GetFullPath(fixture);
            Assert.True(
                scripted.Contains(full) || excluded.ContainsKey(full),
                $"{Path.GetFileName(fixture)} is neither scripted by a canvas scenario nor excluded with a reason");
            Assert.False(
                scripted.Contains(full) && excluded.ContainsKey(full),
                $"{Path.GetFileName(fixture)} is both scripted and excluded");
        }
        foreach (string name in names)
        {
            using var artifact = System.Text.Json.JsonDocument.Parse(
                File.ReadAllBytes(Path.Combine(canvasGolden, name + ".json")));
            var root = artifact.RootElement;
            Assert.True(
                root.GetProperty("steps").GetArrayLength() >= 1,
                $"{name} has no steps — a scenario that mutates nothing covers nothing");
            Assert.False(
                string.Equals(
                    root.GetProperty("terminalSha256").GetString(),
                    root.GetProperty("originalSha256").GetString(),
                    StringComparison.Ordinal),
                $"{name}'s terminal bytes equal its original — no mutation landed before the inverse walk");
        }
    }

}
