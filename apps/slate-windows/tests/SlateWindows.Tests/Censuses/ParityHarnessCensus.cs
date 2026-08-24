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
}
