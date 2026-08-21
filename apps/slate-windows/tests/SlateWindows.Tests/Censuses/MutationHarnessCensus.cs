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
}
