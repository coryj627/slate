// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.RegularExpressions;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// #1123: core's typed create outcome, consumed. A create-if-absent write
/// that lands its bytes and then fails to index is a LANDED create —
/// the file is real and readable (core serves reads from disk) and the
/// next scan indexes it. Before this, every host caller saw one
/// <c>VaultException</c> for both "refused before publish" and "failed
/// after publish", re-presented a committed file as "nothing was
/// written", and advanced the unique-name sequence into a duplicate.
/// The <c>SLATE_TEST_FAULT_AFTER_WRITE</c> seam (a path substring)
/// trips the post-publish arm deterministically.
/// </summary>
public sealed class CreateOutcomeTests
{
    [Fact]
    public void TheReportingCreateSeparatesTheLandedArmFromARefusal()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "create-outcome-arms");
        File.WriteAllText(Path.Combine(fixture.Root, "taken.md"), "occupied\n");
        using VaultSession session = OpenScanned(fixture.Root);

        // Committed: no caveat.
        Assert.Null(CreateOutcomes.CreateReporting(session, "fresh.md", "hello\n", "fresh.md"));
        Assert.Equal("hello\n", File.ReadAllText(Path.Combine(fixture.Root, "fresh.md")));

        // Refused: the typed exception, nothing written — the
        // unique-name advance signal keeps working.
        Assert.Throws<VaultException.DestinationExists>(
            () => CreateOutcomes.CreateReporting(session, "taken.md", "clobber?\n", "taken.md"));
        Assert.Equal("occupied\n", File.ReadAllText(Path.Combine(fixture.Root, "taken.md")));

        // Landed but unindexed: the caveat, the bytes on disk, readable,
        // and a retry is a refusal (the disk gate) — never a duplicate.
        string? caveat;
        using (EnvFaultLock.Acquire())
        {
            Environment.SetEnvironmentVariable("SLATE_TEST_FAULT_AFTER_WRITE", "landed-outcome");
            try
            {
                caveat = CreateOutcomes.CreateReporting(
                    session, "landed-outcome.md", "landed\n", "landed-outcome.md");
            }
            finally
            {
                Environment.SetEnvironmentVariable("SLATE_TEST_FAULT_AFTER_WRITE", null);
            }
        }

        Assert.NotNull(caveat);
        Assert.StartsWith("landed-outcome.md was written but not indexed:", caveat);
        Assert.Contains("do not recreate it", caveat);
        Assert.Equal("landed\n", File.ReadAllText(Path.Combine(fixture.Root, "landed-outcome.md")));
        Assert.Equal("landed\n", session.ReadText("landed-outcome.md"));
        Assert.Throws<VaultException.DestinationExists>(
            () => CreateOutcomes.CreateReporting(session, "landed-outcome.md", "retry\n", "x"));
    }

    /// <summary>Every create funnel in the shell consumes the typed
    /// outcome: the one-error <c>CreateExclusive(</c> call has no
    /// production caller left (the bytes sibling <c>CreateExclusiveBytes</c>
    /// is a different primitive and keeps its shape).</summary>
    [Fact]
    public void NoProductionCallerUsesTheOneErrorCreateShape()
    {
        string root = SourceText.ShellSourceRoot();
        var regex = new Regex(@"\.CreateExclusive\(", RegexOptions.Compiled);
        var offenders = new List<string>();
        foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            {
                continue;
            }

            string[] lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                if (regex.IsMatch(lines[i]))
                {
                    offenders.Add($"{Path.GetFileName(file)}:{i + 1}");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "production callers of the one-error CreateExclusive( — route them through "
            + "CreateOutcomes.CreateReporting (#1123): " + string.Join(", ", offenders));
    }

    private static VaultSession OpenScanned(string root)
    {
        VaultSession session = VaultSession.OpenFilesystem(root);
        using var cancel = new CancelToken();
        session.ScanInitial(cancel);
        return session;
    }
}
