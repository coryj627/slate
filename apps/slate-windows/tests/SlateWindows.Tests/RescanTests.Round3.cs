// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Bases;
using SlateWindows.Reading;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W7-7 PR 7, codex AR-18 implementation review round 2: each finding
/// reproduced as a fact before its fix — the case-only re-seat on the
/// worker (1), every rescan core call drained by a close (3), a cancelled
/// run that stops awaiting its dependents at once and a teardown that
/// drains the re-sync's workers (4), a reading failure counted and silent (5), a
/// replacement tree refresh that keeps the rescan's silence (6) and a
/// canonical-path failure counted (7).
/// </summary>
public sealed partial class RescanTests
{
    // --- (1) the case-only re-seat ----------------------------------------------

    /// <summary>Round 2, finding 1: a case-only rename's re-seat resolves the
    /// stored spelling and reads the note ON THE RESCAN WORKER, through the
    /// seam — parked there, the tab is not re-seated yet — and applies it
    /// back on the dispatcher.</summary>
    [Fact]
    public void ACaseOnlyReseatResolvesAndReadsOnTheRescanWorker() => RunSta(() =>
    {
        using var h = new Harness("reseat-worker", ("ghost.md", "boo\n"));
        if (!h.VolumeAliasesCase())
        {
            return;
        }

        WorkspaceTabViewModel tab = h.Open("ghost.md");
        File.Move(Path.Combine(h.Root, "ghost.md"), Path.Combine(h.Root, "Ghost.md"));
        using var parked = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        h.AfterCoreCall = operation =>
        {
            if (operation == "reseat")
            {
                parked.Set();
                _ = release.Wait(TimeSpan.FromSeconds(30));
            }
        };

        Task run = h.Lifecycle.RescanAsync(RescanReason.Explicit);
        h.Context.RunUntil(() => parked.IsSet, "the re-seat's worker call");
        Assert.Equal("ghost.md", tab.Path);
        release.Set();
        h.Context.Await(run);

        Assert.Equal("Ghost.md", tab.Path);
        Assert.False(tab.IsMissingFromDisk);
        Assert.Equal("boo\n", tab.Text);
        Assert.Contains(h.CoreCalls, call => call.Operation == "reseat" && call.Pool);
        Assert.Equal(["Files refreshed. 1 new or changed, 1 removed."], h.Spoken);
    });

    /// <summary>Round 2, finding 1: the re-seat never installs bytes the
    /// index does not vouch for. When the renamed file changes again after
    /// the index hashes were read, the tab keeps its own buffer, stays
    /// missing, and the run says "1 error"; the next Refresh re-seats it
    /// with the indexed bytes.</summary>
    [Fact]
    public void ACaseOnlyReseatNeverInstallsBytesTheIndexDoesNotVouchFor() => RunSta(() =>
    {
        using var h = new Harness("reseat-unvouched", ("ghost.md", "boo\n"));
        if (!h.VolumeAliasesCase())
        {
            return;
        }

        WorkspaceTabViewModel tab = h.Open("ghost.md");
        File.Move(Path.Combine(h.Root, "ghost.md"), Path.Combine(h.Root, "Ghost.md"));
        bool moved = false;
        h.AfterCoreCall = operation =>
        {
            if (operation == "hashes" && !moved)
            {
                moved = true;
                h.Write("Ghost.md", "boo, changed again\n");
            }
        };

        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        Assert.True(moved);
        Assert.Equal("boo\n", tab.Text);
        Assert.True(tab.IsMissingFromDisk);
        Assert.Equal([OneError], h.Spoken);

        h.AfterCoreCall = null;
        h.Events.Clear();
        h.Now += TimeSpan.FromMinutes(1);
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
        Assert.Equal("Ghost.md", tab.Path);
        Assert.False(tab.IsMissingFromDisk);
        Assert.Equal("boo, changed again\n", tab.Text);
    });

    // --- (3) the close drains every rescan core call ----------------------------

    /// <summary>Round 2, finding 3: the re-sync runs Quick Open's listing and
    /// the workspace's hash read CONCURRENTLY. With the listing parked inside
    /// its page loop after the hash read finished, a close waits for the
    /// listing to end — cancelled by the run's token — before it disposes
    /// the session; the listing never runs on a disposed session.</summary>
    [Fact]
    public void ACloseWaitsForEveryRescanCoreCallInFlight() => RunSta(() =>
    {
        using var h = new Harness("close-drains", ("alpha.md", "# Alpha\n"));
        using var parked = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        h.Lifecycle.SwitcherPageLoadingForTests = token =>
        {
            parked.Set();
            _ = release.Wait(TimeSpan.FromSeconds(30));
        };

        Task run = h.Lifecycle.RescanAsync(RescanReason.Explicit);
        h.Context.RunUntil(
            () => parked.IsSet && h.CoreCallOutcomes.Any(outcome => outcome.Operation == "hashes"),
            "the listing parked and the hash read done");

        long releasedAt = 0;
        _ = Task.Run(async () =>
        {
            await Task.Delay(800);
            Volatile.Write(ref releasedAt, h.Clock.ElapsedMilliseconds);
            release.Set();
        });
        h.Lifecycle.CloseVault();
        long closedAt = h.Clock.ElapsedMilliseconds;
        h.Context.Await(run, "the closed rescan");

        (string Operation, Exception? Failure, long At) listing =
            Assert.Single(h.CoreCallOutcomes, outcome => outcome.Operation == "list");
        Assert.True(listing.At <= closedAt, $"the close returned at {closedAt} ms, before the listing ended at {listing.At} ms");
        Assert.True(Volatile.Read(ref releasedAt) > 0);
        Assert.True(
            listing.Failure is null or VaultException.Cancelled,
            $"the listing ran on a closed session: {listing.Failure?.GetType().Name}");
        Assert.DoesNotContain(h.Events, e => e is A11yEvent.VaultRescanFinished or A11yEvent.VaultRescanIncomplete);
    });

    // --- (4) a cancelled run stops awaiting at once -----------------------------

    /// <summary>Round 2, finding 4: a close while a dependent's publication is
    /// parked ends the rescan AT ONCE — the run stops awaiting its
    /// dependents when its token is cancelled — and it says nothing.</summary>
    [Fact]
    public void ACloseDuringAParkedDependentEndsTheRescanAtOnce() => RunSta(() =>
    {
        using Harness h = DependentsHarness("close-parked-dependent");
        var never = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool started = false;
        h.Workspace.RescanPublicationForTests = async (publishing, _, publish) =>
        {
            if (publishing == "bases")
            {
                started = true;
                await never.Task;
            }

            await publish();
        };
        h.Write("embedded.md", "Embedded after, changed outside Slate.\n");

        Task run = h.Lifecycle.RescanAsync(RescanReason.Explicit);
        h.PumpUntil(() => started, "the bases publication parking");
        h.Lifecycle.CloseVault();
        h.PumpUntil(() => run.IsCompleted, "the closed rescan ending");

        Assert.DoesNotContain(h.Events, e => e is A11yEvent.VaultRescanFinished or A11yEvent.VaultRescanIncomplete);
    });

    /// <summary>Round 2, finding 4: a worker the re-sync started — a reading
    /// model's fetch, parked on the pool under production scheduling (a
    /// dispatcher-hosted workspace with background work) — outlives the
    /// cancelled re-sync, which stops awaiting it at once; the workspace's
    /// teardown then waits for it before returning, so the session the
    /// lifecycle disposes next is never under a fetch still in a core call
    /// (the fetch stops at its cancellation boundary instead).</summary>
    [Fact]
    public void TheTeardownDrainsTheReSyncsWorkersBeforeTheSessionGoes() => RunSta(() => PumpedDispatcher.Run(() =>
    {
        using var w = new BackgroundReadingWorkspace("drain");
        using var parked = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        w.Reading.FetchFaultForTests = () =>
        {
            parked.Set();
            _ = release.Wait(TimeSpan.FromSeconds(30));
            return null;
        };
        using var cancellation = new CancellationTokenSource();
        Task dependents = w.ReSyncDependentsAsync(cancellation.Token);
        Assert.True(PumpedDispatcher.PumpUntil(() => parked.IsSet), "the reading fetch parking");

        // The close cancels the run: the re-sync stops awaiting at once.
        cancellation.Cancel();
        Assert.True(PumpedDispatcher.PumpUntil(() => dependents.IsCompleted), "the cancelled re-sync ending");
        Assert.True(dependents.IsCanceled);
        Assert.False(w.Reading.WhenRefreshWorkDrained().IsCompleted);

        var clock = System.Diagnostics.Stopwatch.StartNew();
        long releasedAt = 0;
        _ = Task.Run(async () =>
        {
            await Task.Delay(800);
            Volatile.Write(ref releasedAt, clock.ElapsedMilliseconds);
            release.Set();
        });
        w.DisposeWorkspace();
        long returnedAt = clock.ElapsedMilliseconds;
        // What the lifecycle does next.
        w.Session.Dispose();
        Assert.True(PumpedDispatcher.PumpUntil(() => w.Reading.WhenRefreshWorkDrained().IsCompleted));

        long released = Volatile.Read(ref releasedAt);
        Assert.True(
            released > 0 && returnedAt >= released,
            $"the teardown returned at {returnedAt} ms, before the fetch was released at {released} ms");
        Assert.Null(w.Reading.LastTerminalFailureForTests);
    }));

    // --- (5) a reading failure is counted, and silent ---------------------------

    /// <summary>Round 2, finding 5: a visible reading model whose re-projection
    /// FAILS during the re-sync — through the real fetch seam — is a failed
    /// operation: the run says "1 error", and Reading's own High failure line
    /// is not spoken beside it.</summary>
    [Fact]
    public void AReadingFailureDuringTheReSyncIsCountedAndSilent() => RunSta(() =>
    {
        using Harness h = DependentsHarness("reading-fails");
        ReadingContentViewModel reading = Assert.IsType<ReadingContentViewModel>(
            h.AllTabs.Single(tab => tab.Path == "host.md").Reading);
        reading.BlocksAppended += _ => { };
        reading.FetchFaultForTests = () => new IOException("injected reading fetch failure");
        h.Write("embedded.md", "Embedded after, changed outside Slate.\n");

        Task run = h.Lifecycle.RescanAsync(RescanReason.Explicit);
        h.PumpUntil(() => run.IsCompleted, "the rescan");

        Assert.Equal([OneError], h.Spoken);
    });

    /// <summary>Round 2, finding 5, under production scheduling: a reading
    /// fetch that fails on its WORKER — past its retries — faults the
    /// re-sync's reading operation (the dependents count one failure) and
    /// Reading speaks nothing of its own.</summary>
    [Fact]
    public void AReadingFailureOnItsWorkerIsCountedAndSilent() => RunSta(() => PumpedDispatcher.Run(() =>
    {
        using var w = new BackgroundReadingWorkspace("reading-fails-bg");
        w.Reading.FetchFaultForTests = () => new IOException("injected reading fetch failure");
        w.Announced.Clear();
        using var cancellation = new CancellationTokenSource();

        Task<ulong> dependents = w.ReSyncDependentsAsync(cancellation.Token);
        Assert.True(
            PumpedDispatcher.PumpUntil(() => dependents.IsCompleted, TimeSpan.FromSeconds(20)),
            "the re-sync's dependents");

        Assert.Equal(1UL, dependents.Result);
        Assert.IsType<IOException>(w.Reading.LastTerminalFailureForTests);
        Assert.Empty(w.Announced);
    }));

    /// <summary>A workspace hosted on a pumped WPF dispatcher with
    /// production (background) scheduling — the Rescan harness runs every
    /// dependent inline — showing <c>host.md</c> in reading mode over an
    /// embed of <c>embedded.md</c>, its first projection published.</summary>
    private sealed class BackgroundReadingWorkspace : IDisposable
    {
        private bool _workspaceDisposed;

        public BackgroundReadingWorkspace(string label, string hostText = "# Host\n\n![[embedded]]\n")
        {
            Root = Path.Combine(Path.GetTempPath(), $"slate-windows-rescan-{label}-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Root);
            File.WriteAllText(Path.Combine(Root, "host.md"), hostText);
            File.WriteAllText(Path.Combine(Root, "embedded.md"), "Embedded before.\n");
            Session = VaultSession.OpenFilesystem(Root);
            using (var scan = new CancelToken())
            {
                Session.ScanInitial(scan);
            }

            Workspace = new WorkspaceViewModel(
                Session, Root, () => [], Announced.Add, startInteractionBackgroundWork: true);
            Workspace.OpenPath("host.md");
            WorkspaceTabViewModel host = Assert.Single(
                Workspace.Groups.SelectMany(group => group.Tabs), tab => tab.Path == "host.md");
            host.ToggleViewMode();
            Reading = Assert.IsType<ReadingContentViewModel>(host.Reading);
            Reading.BlocksAppended += _ => { };
            Assert.True(
                PumpedDispatcher.PumpUntil(() => Reading.HasOtherFileDependencies),
                "the embed's first render");
            Assert.True(PumpedDispatcher.PumpUntil(() => Reading.WhenRefreshWorkDrained().IsCompleted));
        }

        public string Root { get; }

        public VaultSession Session { get; }

        public WorkspaceViewModel Workspace { get; }

        public ReadingContentViewModel Reading { get; }

        public List<A11yEvent> Announced { get; } = [];

        /// <summary>The dependents of a run that changed no open document
        /// and read no index hash.</summary>
        public Task<ulong> ReSyncDependentsAsync(CancellationToken cancellation) =>
            Workspace.ReSyncDependentsAsync(
                new WorkspaceViewModel.RescanDocumentsOutcome(
                    0,
                    new HashSet<string>(StringComparer.Ordinal),
                    new HashSet<BaseDocumentViewModel>()),
                new Dictionary<string, WorkspaceViewModel.IndexedPath>(StringComparer.Ordinal),
                cancellation);

        public void DisposeWorkspace()
        {
            if (!_workspaceDisposed)
            {
                _workspaceDisposed = true;
                Workspace.Dispose();
            }
        }

        public void Dispose()
        {
            DisposeWorkspace();
            Session.Dispose();
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    // --- (6) a replacement tree refresh keeps the rescan's silence --------------

    /// <summary>Round 2, finding 6: a Slate write that replaces the rescan's
    /// pending tree refresh (F2) carries the rescan's silence into the
    /// replacement: a tag-tree failure in that replacement is counted — the
    /// one sentence is "1 error" — and never spoken on its own.</summary>
    [Fact]
    public void AReplacementTreeRefreshKeepsTheRescansSilence() => RunSta(() =>
    {
        using Harness h = Harness.WithAsyncTree("tree-replacement-silent", ("alpha.md", "# Alpha\n\n#topic\n"));
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
        // The replacement's tag tree fails.
        h.Sidebar.AfterTagTreeForTests = () => throw new VaultException.Io("injected tag tree failure");
        h.CreateInCore("made.md", "# Made\n");
        h.Context.RunUntil(() => h.QuickOpen.FilePathsForTests.Contains("made.md"), "the Slate write's event");
        unpark.Set();
        h.Context.Await(run);

        Assert.Equal([OneError], h.Spoken);
        Assert.Contains(h.Sidebar.RootNodes, node => node.Path == "made.md");
    });

    // --- (7) a canonical-path failure is counted --------------------------------

    /// <summary>Round 2, finding 7: when the index has no row for an open
    /// note and the probe of its spelling on disk FAILS, the tab is kept
    /// (never marked missing on a guess) and the failure is counted: the one
    /// sentence is "1 error", never a clean "Files refreshed".</summary>
    [Fact]
    public void ACanonicalPathFailureKeepsTheTabAndIsCounted() => RunSta(() =>
    {
        using var h = new Harness("canonical-fails", ("gone.md", "gone\n"));
        WorkspaceTabViewModel gone = h.Open("gone.md");
        h.Delete("gone.md");
        h.Lifecycle.CanonicalPathForTests = _ => throw new VaultException.Io("injected probe failure");

        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        Assert.False(gone.IsMissingFromDisk);
        Assert.Equal([OneError], h.Spoken);
    });
}
