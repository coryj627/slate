// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Reading;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W7-7 PR 7, codex PR 7 round 3: each finding reproduced as a fact before
/// its fix — a streamed reading publication that holds the rescan until its
/// last chunk (3), a re-sync operation's failure contained and counted (4),
/// a vault close that waits for an admitted Quick Open rank (5), and a
/// silent tag failure that keeps the status line over a held mutation
/// status (the low).
/// </summary>
public sealed partial class RescanTests
{
    // --- (3) a streamed publication settles at its LAST chunk ----------------

    /// <summary>A host note long enough to stream: its first chunk publishes
    /// in the fetch's turn, the rest in later dispatcher passes.</summary>
    private static string LongHost() =>
        "# Host\n\n![[embedded]]\n\n"
        + string.Concat(Enumerable.Range(0, 600).Select(index => $"Paragraph {index}.\n\n"));

    /// <summary>The rescan's re-projection of a note changed outside Slate:
    /// the embedded note rewritten on disk and rescanned into the index.</summary>
    private static void ChangeTheEmbed(BackgroundReadingWorkspace w)
    {
        File.WriteAllText(Path.Combine(w.Root, "embedded.md"), "Embedded after, changed outside Slate.\n");
        using var cancel = new CancelToken();
        _ = w.Session.Rescan(cancel);
    }

    /// <summary>Round 3, finding 3: a streamed re-projection — parked after
    /// its first chunk, the rest still queued on the dispatcher — has not
    /// published yet: the rescan's waiter stays pending; the second chunk's
    /// failure then settles it WITH that failure, and Reading says nothing
    /// of its own.</summary>
    [Fact]
    public void AStreamedReadingPublicationHoldsTheRescanUntilItsLastChunk() => RunSta(() => PumpedDispatcher.Run(() =>
    {
        using var w = new BackgroundReadingWorkspace("reading-streamed", LongHost());
        ChangeTheEmbed(w);
        int steps = 0;
        bool faultNext = false;
        w.Reading.PublishFaultForTests = () =>
        {
            steps++;
            return faultNext ? new InvalidOperationException("injected chunk failure") : null;
        };
        w.Announced.Clear();
        using var cancellation = new CancellationTokenSource();

        Task projection = w.Reading.NotifyRescanAsync(cancellation.Token);
        Assert.True(PumpedDispatcher.PumpUntil(() => steps >= 1), "the first chunk's publication");

        // Chunk 2 is still queued behind this pass: nothing terminal yet.
        Assert.Equal(1, steps);
        Assert.False(projection.IsCompleted, "the rescan was released by the first chunk");

        faultNext = true;
        Assert.True(PumpedDispatcher.PumpUntil(() => projection.IsCompleted), "the second chunk");
        Assert.True(projection.IsFaulted, "a failed chunk settled the rescan as a success");
        Assert.Empty(w.Announced);
    }));

    /// <summary>Round 3, finding 3, through the re-sync: a streamed
    /// re-projection whose second chunk fails is ONE failed operation of the
    /// dependents — never a success counted after the first chunk.</summary>
    [Fact]
    public void AStreamedReadingFailureIsCountedByTheReSync() => RunSta(() => PumpedDispatcher.Run(() =>
    {
        using var w = new BackgroundReadingWorkspace("reading-streamed-count", LongHost());
        ChangeTheEmbed(w);
        int steps = 0;
        w.Reading.PublishFaultForTests = () =>
            ++steps >= 2 ? new InvalidOperationException("injected chunk failure") : null;
        w.Announced.Clear();
        using var cancellation = new CancellationTokenSource();

        Task<ulong> dependents = w.ReSyncDependentsAsync(cancellation.Token);
        Assert.True(
            PumpedDispatcher.PumpUntil(() => dependents.IsCompleted, TimeSpan.FromSeconds(20)),
            "the re-sync's dependents");

        Assert.True(steps >= 2);
        Assert.Equal(1UL, dependents.Result);
        Assert.Empty(w.Announced);
    }));

    // --- (4) a re-sync operation's failure is contained and counted ----------

    /// <summary>Round 3, finding 4: the re-seat's worker call fails with a
    /// non-cancellation error (its index-hash read, say). The failure is
    /// contained and counted — the rest of the document re-sync still runs,
    /// the run still settles the tree and Quick Open, and it speaks exactly one
    /// "1 error" sentence; before, it escaped and the Refresh ended silently.</summary>
    [Fact]
    public void AFailedReseatWorkerIsCountedAndTheRunStillSpeaks() => RunSta(() =>
    {
        using var h = new Harness("reseat-faults", ("ghost.md", "boo\n"), ("a.md", "alpha\n"));
        if (!h.VolumeAliasesCase())
        {
            return;
        }

        WorkspaceTabViewModel other = h.Open("a.md");
        WorkspaceTabViewModel tab = h.Open("ghost.md");
        File.Move(Path.Combine(h.Root, "ghost.md"), Path.Combine(h.Root, "Ghost.md"));
        h.Write("a.md", "alpha, changed outside Slate\n");
        h.AfterCoreCall = operation =>
        {
            if (operation == "reseat")
            {
                throw new VaultException.Io("injected re-seat failure");
            }
        };

        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        Assert.Equal([OneError], h.Spoken);
        Assert.True(tab.IsMissingFromDisk);
        // The rest of the document re-sync still ran.
        Assert.Equal("alpha, changed outside Slate\n", other.Text);
        Assert.Contains(h.Sidebar.RootNodes, node => node.Path == "Ghost.md");
        Assert.Contains("Ghost.md", h.QuickOpen.FilePathsForTests);
    });

    // --- (5) a close waits for an ADMITTED Quick Open rank -------------------

    /// <summary>Round 3, finding 5: the re-sync's silent re-rank, parked INSIDE
    /// its native call — past the coordinator's admission, where its token no
    /// longer reaches. A vault close waits for it to return before it
    /// finishes, and the process-wide rank lane is free at once for the next
    /// vault.</summary>
    [Fact]
    public void AVaultCloseWaitsForAnAdmittedRankAndTheNextVaultRanksAtOnce() => RunSta(() =>
    {
        using var h = new Harness("quickopen-admitted", ("alpha.md", "# Alpha\n"));
        h.QuickOpen.Open();
        h.Context.RunUntil(() => !h.QuickOpen.IsRanking && h.QuickOpen.Results.Count > 0, "the open's rank");
        using var parked = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        h.QuickOpen.RankDelayForTests = _ => Task.CompletedTask;
        h.QuickOpen.InsideRankForTests = () =>
        {
            parked.Set();
            _ = release.Wait(TimeSpan.FromSeconds(30));
        };
        h.Write("late.md", "# Late\n");
        h.Events.Clear();

        Task run = h.Lifecycle.RescanAsync(RescanReason.Explicit);
        h.Context.RunUntil(() => parked.IsSet, "the re-rank parked inside its native call");
        long releasedAt = 0;
        _ = Task.Run(async () =>
        {
            await Task.Delay(1500);
            Volatile.Write(ref releasedAt, h.Clock.ElapsedMilliseconds);
            release.Set();
        });
        h.Lifecycle.CloseVault();
        long closedAt = h.Clock.ElapsedMilliseconds;
        h.Context.Await(run, "the closed rescan");

        long released = Volatile.Read(ref releasedAt);
        Assert.True(
            released > 0 && closedAt >= released,
            $"the close returned at {closedAt} ms, before the admitted rank returned at {released} ms");
        Task<SwitcherRankPage> next = QuickSwitcherRankCoordinator.Shared.RankAsync(
            () => SlateUniffiMethods.SwitcherRankTop([], string.Empty, [], 1),
            CancellationToken.None);
        Assert.True(next.Wait(TimeSpan.FromSeconds(1)), "the next vault's rank waited on the closed vault's");
        Assert.DoesNotContain(h.Events, e => e is A11yEvent.VaultRescanFinished or A11yEvent.VaultRescanIncomplete);
    });

    // --- the low: a silent tag failure keeps the status line ------------------

    /// <summary>Round 3 (low): a sidebar create during the rescan's pending
    /// tree refresh holds its own status ("Created note …") for that
    /// publication; the replacement refresh's tag-tree failure is silent and
    /// counted — and it, not the held create, is what the status line shows
    /// last.</summary>
    [Fact]
    public void ASidebarCreateDuringTheRescansTreeLeavesTheTagFailureOnTheStatusLine() => RunSta(() =>
    {
        using Harness h = Harness.WithAsyncTree("tree-create-status", ("alpha.md", "# Alpha\n\n#topic\n"));
        h.Context.RunUntil(() => !h.Sidebar.IsRefreshingTree, "the open's tree");
        using var read = new ManualResetEventSlim(false);
        using var unpark = new ManualResetEventSlim(false);
        int treeReads = 0;
        h.TreeWorker = (work, token) => Interlocked.Increment(ref treeReads) == 1
            ? Task.Run(
                () =>
                {
                    work();
                    read.Set();
                    _ = unpark.Wait(TimeSpan.FromSeconds(30));
                },
                CancellationToken.None)
            : Task.Run(work, token);

        Task run = h.Lifecycle.RescanAsync(RescanReason.Explicit);
        h.Context.RunUntil(() => read.IsSet, "the rescan's tree snapshot");
        h.Sidebar.AfterTagTreeForTests = () => throw new VaultException.Io("injected tag tree failure");
        h.Sidebar.CreateNoteCommand.Execute(null);
        unpark.Set();
        h.Context.Await(run);
        h.Context.RunUntil(() => !h.Sidebar.IsRefreshingTree, "the replacement's publication");

        Assert.Contains(OneError, h.Spoken);
        Assert.DoesNotContain(h.Spoken, line => line.StartsWith("Could not load tags", StringComparison.Ordinal));
        Assert.StartsWith("Could not load tags", h.Sidebar.Status, StringComparison.Ordinal);
    });
}
