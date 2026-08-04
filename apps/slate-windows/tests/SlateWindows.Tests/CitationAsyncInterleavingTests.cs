// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Panels;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W4-5 (#737): the citation suite under PRODUCTION scheduling
/// (startInteractionBackgroundWork: true). Every other citation test
/// runs the synchronous test mode, which orders seeding, citation
/// loads and bibliography loads deterministically and therefore cannot
/// observe the interleavings the app actually ships.
/// </summary>
public sealed class CitationAsyncInterleavingTests : IDisposable
{
    private readonly FixtureVault _fixture;

    public CitationAsyncInterleavingTests()
    {
        _fixture = FixtureVault.Create(0, "citation-async");
        // A REALISTICALLY sized bibliography. The interleaving under
        // test is "did the key lookup run before the entries landed",
        // and with a toy 1-entry file the worker finishes inside the
        // two statements between EnsureLoaded and the lookup — the
        // race resolves the lucky way and hides the defect. Thousands
        // of entries put the load orders of magnitude beyond that gap.
        var bib = new System.Text.StringBuilder(
            "@article{knuth1984,\n  title = {Literate Programming},\n"
                + "  author = {Knuth, Donald E.},\n  year = {1984}\n}\n");
        for (int i = 0; i < 5000; i++)
        {
            _ = bib.Append($"@article{{filler{i},\n  title = {{Filler Study Number {i}}},\n")
                .Append($"  author = {{Author, Some {i}}},\n  year = {{20{i % 100:D2}}}\n}}\n");
        }
        File.WriteAllText(Path.Combine(_fixture.Root, "library.bib"), bib.ToString());
        File.WriteAllText(
            Path.Combine(_fixture.Root, "cited.md"),
            "# Cited\n\nA citation [@knuth1984] and a ghost [@ghostkey].\n");
        File.Copy(
            Path.Combine(RepoRoot, "demo-vault", "csl", "ieee.csl"),
            Path.Combine(_fixture.Root, "ieee.csl"));
        File.WriteAllText(
            Path.Combine(_fixture.Root, "slate.json"),
            "{\"citations\":{\"bibliography\":\"library.bib\",\"cite_style\":\"ieee\"}}");
    }

    private static string RepoRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "demo-vault")))
            {
                dir = dir.Parent;
            }
            return dir?.FullName ?? throw new InvalidOperationException("repo root not found");
        }
    }

    public void Dispose() => _fixture.Dispose();

    private VaultSession OpenScanned()
    {
        var session = VaultSession.OpenFilesystem(_fixture.Root);
        using var cancel = new CancelToken();
        session.ScanInitial(cancel);
        return session;
    }

    /// <summary>The shipping configuration: background work ON.</summary>
    private WorkspaceViewModel MakeAsyncWorkspace(
        VaultSession session, List<A11yEvent> announced) =>
        new(session, _fixture.Root, () => [], announced.Add,
            startInteractionBackgroundWork: true);

    /// <summary>Run the citation panels to a fixed point: drain tracked
    /// worker tasks, then let the publishes they POSTED actually run.
    /// Repeats because a drained worker can post a publish that queues
    /// further work. Task.Delay, not Task.Yield: a worker's publish is
    /// handed to the SynchronizationContext captured at construction,
    /// and yielding does not give that callback a chance to run — a
    /// drained worker is NOT the same as an applied publish.</summary>
    private static async Task QuiesceAsync(WorkspaceViewModel workspace)
    {
        for (int round = 0; round < 40; round++)
        {
            await Task.WhenAll(
                workspace.Citations.DrainForTests(),
                workspace.Bibliography.DrainForTests());
            await Task.Delay(2);
        }
    }

    /// <summary>The workspace constructor starts the first note's
    /// citation load (SyncPanels) and the vault's source seeding
    /// (SetBibliographySources) as two independent background
    /// operations. A citation render that wins that race sees no
    /// sources and marks every key unresolved — and because a
    /// same-path NoteChanged is a no-op and ApplySeedOutcome does not
    /// re-query, nothing ever repairs those rows.</summary>
    [Fact]
    public async Task CitationsResolveWhenSourceSeedingCompletesAfterTheFirstLoad()
    {
        var announced = new List<A11yEvent>();
        using VaultSession session = OpenScanned();
        using var workspace = MakeAsyncWorkspace(session, announced);
        workspace.OpenPath("cited.md");
        await QuiesceAsync(workspace);

        Assert.Equal(2, workspace.Citations.Rows.Count);
        Assert.Null(workspace.Citations.LoadError);
        // knuth1984 IS in the bibliography; only ghostkey is not.
        Assert.Single(workspace.Citations.Rows, row => !row.IsUnresolved);
    }

    /// <summary>Ctrl+J on the FIRST use, before the bibliography rail
    /// has ever been revealed: EnsureLoaded only STARTS the entries
    /// load, so the key lookup must not be answered from the empty
    /// pre-load list. The user pressed a key that means "take me to
    /// this entry" and the entry exists.</summary>
    [Fact]
    public async Task JumpToBibliographyLandsOnAKeyWhoseEntriesLoadAsynchronously()
    {
        var announced = new List<A11yEvent>();
        using VaultSession session = OpenScanned();
        using var workspace = MakeAsyncWorkspace(session, announced);
        workspace.OpenPath("cited.md");
        await QuiesceAsync(workspace);

        workspace.OpenCitationDetails(
            workspace.Citations.Rows.First(row => !row.IsUnresolved));

        // The bibliography has never been revealed, so this is the
        // first EnsureLoaded: the entries load STARTS here.
        Assert.Empty(workspace.Bibliography.Entries);

        workspace.JumpToBibliography();
        // NOTE: no "the load has not published yet" assertion here.
        // That is a claim about SCHEDULING, and without a controllable
        // seam it is a coin flip — a cold run can stall the test thread
        // long enough for the worker to finish, which failed this suite
        // once in three. The interleaving assertion returns with the
        // manually-released scheduler seam; until then these tests pin
        // the END STATE only.
        await QuiesceAsync(workspace);

        Assert.Equal("knuth1984", workspace.Bibliography.ConsumeKeyFocusRequest());
        Assert.Contains(
            announced.OfType<A11yEvent.HostComposed>(),
            e => e.Text == "Jumped to bibliography entry: knuth1984.");
        Assert.DoesNotContain(
            announced.OfType<A11yEvent.HostComposed>(),
            e => e.Text == "Searching bibliography for: knuth1984.");
    }

    /// <summary>The focus request is now settled ASYNCHRONOUSLY on the
    /// first press, so a view cannot invoke the command and read a
    /// property straight afterwards — it has to be told. This is the
    /// contract the W4-5 XAML will bind against.</summary>
    [Fact]
    public async Task ADeferredJumpNotifiesTheViewAndIsConsumableExactlyOnce()
    {
        var announced = new List<A11yEvent>();
        using VaultSession session = OpenScanned();
        using var workspace = MakeAsyncWorkspace(session, announced);
        workspace.OpenPath("cited.md");
        await QuiesceAsync(workspace);
        workspace.OpenCitationDetails(
            workspace.Citations.Rows.First(row => !row.IsUnresolved));

        int notifications = 0;
        workspace.Bibliography.KeyFocusRequested += (_, _) => notifications++;

        workspace.JumpToBibliography();
        // See the sibling test: no pre-quiescence scheduling assertion.
        await QuiesceAsync(workspace);

        Assert.Equal(1, notifications);
        Assert.Equal("knuth1984", workspace.Bibliography.ConsumeKeyFocusRequest());
        // Consumed exactly once: a stale value must never steal focus
        // from a later interaction.
        Assert.Null(workspace.Bibliography.ConsumeKeyFocusRequest());
    }

    /// <summary>A jump that never resolves must not leave a live focus
    /// request behind for whatever binds next.</summary>
    [Fact]
    public async Task ShutdownDropsAnUnconsumedFocusRequest()
    {
        var announced = new List<A11yEvent>();
        using VaultSession session = OpenScanned();
        using var workspace = MakeAsyncWorkspace(session, announced);
        workspace.OpenPath("cited.md");
        await QuiesceAsync(workspace);
        workspace.OpenCitationDetails(
            workspace.Citations.Rows.First(row => !row.IsUnresolved));

        workspace.JumpToBibliography();
        await QuiesceAsync(workspace);
        workspace.Bibliography.Shutdown();

        Assert.Null(workspace.Bibliography.ConsumeKeyFocusRequest());
    }
}
