// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// #1455: the Windows suite never moves its fixtures into the Recycle Bin of
// whoever runs it. ScratchTrashBin's module initializer routes this test
// process's Trash into a scratch bin of its own as the assembly loads; these
// facts check, before anything is trashed, that the routing is in place, then
// count the bin's entries, never the Recycle Bin's. They also hold the shipping
// shell and the tools to the system Trash: no source of theirs routes it.

using uniffi.slate_uniffi;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "scratch-trash")]
public class ScratchTrashCensus
{
    /// <summary>
    /// The bin this process routes into. It throws, failing the fact, unless
    /// core reports the routing: no fact here trashes anything before that.
    /// </summary>
    private static string RoutedBin()
    {
        string bin = ScratchTrashBin.Folder;
        if (bin.Length == 0
            || SlateUniffiMethods.ScratchTrashBinForTests() != bin
            || !Directory.Exists(bin))
        {
            throw new InvalidOperationException(
                $"#1455: this test process's Trash is not routed into its scratch bin ('{bin}'), so no fact may trash anything");
        }
        return bin;
    }

    /// <summary>
    /// The source files under <paramref name="root"/>: build output and the
    /// generated bindings, which define the functions and call nothing, excluded.
    /// </summary>
    private static List<string> SourceFiles(string root)
    {
        List<string> files =
        [
            .. Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(file => !Path.GetRelativePath(root, file).Split(Path.DirectorySeparatorChar)
                    .Any(segment => segment is "bin" or "obj" or "generated"))
                .OrderBy(file => file, StringComparer.Ordinal),
        ];
        return files;
    }

    [Fact]
    public void TheModuleInitializerRoutedThisProcessIntoAScratchBin()
    {
        string bin = RoutedBin();
        Assert.StartsWith(
            $"slate-scratch-trash-{Environment.ProcessId}-",
            Path.GetFileName(bin),
            StringComparison.Ordinal);
        Assert.Equal(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())),
            Path.GetDirectoryName(bin));
    }

    /// <summary>
    /// A note trashed through a session lands in the bin, not the Recycle Bin.
    /// The shell's own open and a test's own session are routed alike.
    /// </summary>
    [Fact]
    public void ATrashThroughASessionLandsInTheScratchBin()
    {
        string bin = RoutedBin();
        using FixtureVault fixture = FixtureVault.Create(0, "scratch-trash-census");
        string note = $"census-1455-{Guid.NewGuid():N}.md";
        File.WriteAllText(Path.Combine(fixture.Root, note), "# Census\n");
        using VaultSession session = VaultSession.OpenFilesystem(fixture.Root);
        using var cancel = new CancelToken();
        session.ScanInitial(cancel);

        session.DeleteFile(note);

        Assert.False(File.Exists(Path.Combine(fixture.Root, note)), "the note is still in the vault");
        Assert.True(
            File.Exists(Path.Combine(bin, note)),
            $"the note did not land in the scratch bin {bin}");
    }

    /// <summary>
    /// The shipping shell and the tools keep the system Trash: no source of
    /// theirs routes the Trash or asks where it is routed.
    /// </summary>
    [Fact]
    public void NoShellOrToolSourceRoutesTheTrash()
    {
        string windows = Path.Combine(SourceText.RepoRoot(), "apps", "slate-windows");
        var offenders = new List<string>();
        int scanned = 0;
        foreach (string tree in new[] { "src", "tools" })
        {
            string root = Path.Combine(windows, tree);
            foreach (string file in SourceFiles(root))
            {
                scanned++;
                string text = File.ReadAllText(file);
                if (text.Contains("RouteTrashToScratchBinForTests", StringComparison.Ordinal)
                    || text.Contains("ScratchTrashBinForTests", StringComparison.Ordinal))
                {
                    offenders.Add(Path.GetRelativePath(windows, file).Replace('\\', '/'));
                }
            }
        }
        Assert.True(scanned > 100, $"the walk must see the shell and the tools: {scanned} files");
        Assert.Empty(offenders);
    }

    /// <summary>
    /// The module initializer alone routes the process, once, as it loads: a
    /// fact that routed again could only fail or point the bin elsewhere.
    /// </summary>
    [Fact]
    public void OnlyTheModuleInitializerRoutesTheTrash()
    {
        string tests = Path.Combine(SourceText.RepoRoot(), "apps", "slate-windows", "tests");
        List<string> routing =
        [
            .. SourceFiles(tests)
                .Where(file => Path.GetFileName(file) != "ScratchTrashCensus.cs")
                .Where(file => File.ReadAllText(file)
                    .Contains("RouteTrashToScratchBinForTests(", StringComparison.Ordinal))
                .Select(file => Path.GetFileName(file)),
        ];
        Assert.Equal(["ScratchTrashBin.cs"], routing);
    }
}
