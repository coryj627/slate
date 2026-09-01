// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using ParityHarness;

namespace SlateWindows.Tests.Censuses;

/// <summary>
/// W6-1 §E TE-11d (E18): the canvas scenario goldens, asserted the
/// mutation harness's way — the same scripts execute on both platforms
/// (the scenarios file is shared DATA), Windows is the regen authority,
/// and both lanes green proves cross-platform byte-identity
/// transitively. The driver enforces E18's rules itself (canonical
/// fixtures round-trip byte-identical; foreign formatting dies on the
/// first write; the inverse walk restores core's OWN canonical
/// serialization of the original content), so this census only has to
/// pin the artifacts.
/// </summary>
public sealed class CanvasScenarioCensus
{
    private static string RepoRoot
    {
        get
        {
            string dir = AppContext.BaseDirectory;
            for (int i = 0; i < 8; i++)
            {
                dir = Path.GetDirectoryName(dir)!;
            }

            return dir;
        }
    }

    private static string GoldenDir => Path.Combine(
        RepoRoot, "crates", "slate-core", "tests", "fixtures",
        "canvas_scenario_golden");

    private static string ScenariosPath =>
        Path.Combine(GoldenDir, "scenarios.json");

    [Fact]
    public void CanvasScenarioArtifacts_MatchCommittedGoldensByteForByte()
    {
        Assert.True(File.Exists(ScenariosPath), $"scenarios missing at {ScenariosPath}");
        IReadOnlyList<(string Name, string Artifact)> produced =
            CanvasScenarioDriver.RunAll(ScenariosPath);

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
                + "apps/slate-windows/tools/ParityHarness -- "
                + "--canvas-scenarios "
                + "crates/slate-core/tests/fixtures/canvas_scenario_golden/scenarios.json "
                + "--out crates/slate-core/tests/fixtures/canvas_scenario_golden)");
        }
    }

    /// <summary>§E TE-11d: the driver's E18 gates have TEETH. A
    /// canonical fixture mislabeled foreign must make the driver throw
    /// the foreign-survived error - proving the round-trip comparison
    /// really runs, not just the artifact serialization.</summary>
    [Fact]
    public void TheForeignGateRefusesACanonicalFixtureMarkedForeign()
    {
        string tampered = Path.Combine(
            Path.GetTempPath(), $"canvas-tamper-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tampered);
        try
        {
            string canvasDir = Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(ScenariosPath)!, "..", "canvas"))
                .Replace('\\', '/');
            string goldenDir = Path.GetFullPath(
                Path.GetDirectoryName(ScenariosPath)!).Replace('\\', '/');
            string json = File.ReadAllText(ScenariosPath)
                .Replace("../canvas/", canvasDir + "/", StringComparison.Ordinal)
                .Replace(
                    "\"fixture\": \"foreign.canvas\"",
                    "\"fixture\": \"" + goldenDir + "/foreign.canvas\"",
                    StringComparison.Ordinal)
                .Replace(
                    "\"name\": \"c2_nested_groups_geometry\",",
                    "\"name\": \"c2_nested_groups_geometry\", \"foreign\": true,",
                    StringComparison.Ordinal);
            string path = Path.Combine(tampered, "scenarios.json");
            File.WriteAllText(path, json);
            CanvasScenarioDriverException thrown =
                Assert.Throws<CanvasScenarioDriverException>(
                    () => CanvasScenarioDriver.RunAll(path));
            Assert.Contains("foreign formatting survived", thrown.Message);
        }
        finally
        {
            Directory.Delete(tampered, recursive: true);
        }
    }

    /// <summary>The determinism twin: two in-process runs must produce
    /// identical bytes, or the goldens pin luck.</summary>
    [Fact]
    public void CanvasScenarioHarnessIsDeterministic_TwoRunsProduceIdenticalBytes()
    {
        IReadOnlyList<(string Name, string Artifact)> first =
            CanvasScenarioDriver.RunAll(ScenariosPath);
        IReadOnlyList<(string Name, string Artifact)> second =
            CanvasScenarioDriver.RunAll(ScenariosPath);
        Assert.Equal(
            first.Select(item => item.Name), second.Select(item => item.Name));
        foreach (((_, string a), (_, string b)) in first.Zip(second))
        {
            Assert.Equal(a, b);
        }
    }
}
