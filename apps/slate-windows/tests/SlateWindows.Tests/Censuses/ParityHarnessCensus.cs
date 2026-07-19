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
        var files = Directory.EnumerateFiles(FixturesDir, "*.md")
            .Select(Path.GetFileName)
            .Select(f => f!)
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();
        Assert.NotEmpty(files);

        string vaultRoot = Path.Combine(Path.GetTempPath(), $"parity-vault-{Guid.NewGuid():N}");
        Directory.CreateDirectory(vaultRoot);
        try
        {
            foreach (var f in files)
            {
                File.Copy(Path.Combine(FixturesDir, f), Path.Combine(vaultRoot, f));
            }
            Directory.CreateDirectory(outDir);
            using var session = VaultSession.OpenFilesystem(vaultRoot);
            using var cancel = new CancelToken();
            session.ScanInitial(cancel);

            foreach (var f in files)
            {
                string text = System.Text.Encoding.UTF8.GetString(
                    File.ReadAllBytes(Path.Combine(vaultRoot, f)));
                File.WriteAllBytes(
                    Path.Combine(outDir, f + ".json"),
                    System.Text.Encoding.UTF8.GetBytes(SurfaceSerializer.FileArtifact(f, text)));
            }
            File.WriteAllBytes(
                Path.Combine(outDir, "search.json"),
                System.Text.Encoding.UTF8.GetBytes(SurfaceSerializer.SearchArtifact(session, cancel)));
            File.WriteAllBytes(
                Path.Combine(outDir, "links.json"),
                System.Text.Encoding.UTF8.GetBytes(SurfaceSerializer.LinksArtifact(session, files)));
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
    }
}
