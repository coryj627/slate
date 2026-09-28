// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W7-7 PR 7, codex PR 7 round 3 finding 6: the Connections leaf's reload
/// that a rescan's graph probe issues — started by the probe's apply, after
/// the probe itself completed — is drained by the workspace teardown to its
/// end, past the ordinary bounded drain, before the session is disposed.
/// </summary>
public sealed partial class ConnectionsLeafTests
{
    [Fact]
    public void TheTeardownDrainsTheReloadARescanProbeIssued()
    {
        using GraphVault vault = GraphVault.Copy("rescan-drain");
        PumpedDispatcher.Run(() =>
        {
            var host = new Host(vault.Root);
            bool disposed = false;
            try
            {
                host.ActivateLeaf();
                host.OpenNote(Hub);
                host.Settle();
                int loads = host.Loads;
                // Another session's link into hub bumps the graph generation.
                File.WriteAllText(Path.Combine(vault.Root, "rescan-new.md"), "[[hub]]\n");
                using (var scan = new CancelToken())
                {
                    _ = host.Session.Rescan(scan);
                }

                using var parked = new ManualResetEventSlim(false);
                using var release = new ManualResetEventSlim(false);
                host.Leaf.FetchGateForTests = () =>
                {
                    parked.Set();
                    _ = release.Wait(TimeSpan.FromSeconds(30));
                };
                using var cancellation = new CancellationTokenSource();
                Task dependents = host.Workspace.ReSyncDependentsAsync(
                    new WorkspaceViewModel.RescanDocumentsOutcome(
                        0,
                        new HashSet<string>(StringComparer.Ordinal),
                        new HashSet<Bases.BaseDocumentViewModel>()),
                    new Dictionary<string, WorkspaceViewModel.IndexedPath>(StringComparer.Ordinal),
                    cancellation.Token);
                Assert.True(PumpedDispatcher.PumpUntil(() => parked.IsSet), "the probe's reload parking");
                Assert.Equal(loads + 1, host.Loads);

                // The close cancels the run; the reload is still in its fetch.
                cancellation.Cancel();
                Assert.True(PumpedDispatcher.PumpUntil(() => dependents.IsCompleted), "the cancelled re-sync");

                var clock = System.Diagnostics.Stopwatch.StartNew();
                long releasedAt = 0;
                _ = Task.Run(async () =>
                {
                    await Task.Delay(6000);
                    Volatile.Write(ref releasedAt, clock.ElapsedMilliseconds);
                    release.Set();
                });
                host.Workspace.Dispose();
                disposed = true;
                long returnedAt = clock.ElapsedMilliseconds;

                long released = Volatile.Read(ref releasedAt);
                Assert.True(
                    released > 0 && returnedAt >= released,
                    $"the teardown returned at {returnedAt} ms, before the reload was released at {released} ms");
            }
            finally
            {
                if (!disposed)
                {
                    host.Workspace.Dispose();
                }

                host.Session.Dispose();
            }
        });
    }

    /// <summary>A leaf rooted at the hub whose tree query, issued by a rescan's
    /// probe, parks inside its native call until the query's token is
    /// cancelled — as a long whole-graph query honouring its token would.</summary>
    private static (Task Dependents, ManualResetEventSlim Parked, Func<bool> Observed) ParkTheProbesTreeQuery(
        Host host,
        GraphVault vault,
        CancellationToken cancellation)
    {
        host.ActivateLeaf();
        host.OpenNote(Hub);
        host.Settle();
        File.WriteAllText(Path.Combine(vault.Root, "rescan-probe.md"), "[[hub]]\n");
        using (var scan = new CancelToken())
        {
            _ = host.Session.Rescan(scan);
        }

        var parked = new ManualResetEventSlim(false);
        bool observed = false;
        host.Leaf.BeforeTreeQueryForTests = cancel =>
        {
            parked.Set();
            var clock = System.Diagnostics.Stopwatch.StartNew();
            while (!cancel.IsCancelled() && clock.Elapsed < TimeSpan.FromSeconds(30))
            {
                Thread.Sleep(10);
            }

            Volatile.Write(ref observed, cancel.IsCancelled());
        };
        Task dependents = host.Workspace.ReSyncDependentsAsync(
            new WorkspaceViewModel.RescanDocumentsOutcome(
                0,
                new HashSet<string>(StringComparer.Ordinal),
                new HashSet<Bases.BaseDocumentViewModel>()),
            new Dictionary<string, WorkspaceViewModel.IndexedPath>(StringComparer.Ordinal),
            cancellation);
        Assert.True(PumpedDispatcher.PumpUntil(() => parked.IsSet), "the probe's tree query parking");
        return (dependents, parked, () => Volatile.Read(ref observed));
    }

    /// <summary>W7-7 PR 7 (codex PR 7 round 4, finding 5): the rescan's token
    /// reaches the native tree query its graph probe issued — cancelling the
    /// run cancels the query in flight.</summary>
    [Fact]
    public void TheRescansTokenReachesTheProbesTreeQuery()
    {
        using GraphVault vault = GraphVault.Copy("rescan-token-route");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            using var cancellation = new CancellationTokenSource();
            (_, ManualResetEventSlim parked, Func<bool> observed) = ParkTheProbesTreeQuery(host, vault, cancellation.Token);
            using (parked)
            {
                cancellation.Cancel();
                var clock = System.Diagnostics.Stopwatch.StartNew();
                while (!observed() && clock.Elapsed < TimeSpan.FromSeconds(5))
                {
                    Thread.Sleep(10);
                }

                Assert.True(observed(), "the native query never saw the rescan's cancellation");
            }
        });
    }

    /// <summary>W7-7 PR 7 (codex PR 7 round 4, finding 5): a close with the
    /// probe's native tree query in flight — the rescan not yet cancelled —
    /// cancels it through the leaf's retirement, so the teardown drains at
    /// once instead of waiting the query out.</summary>
    [Fact]
    public void AClosesTeardownCancelsTheProbesTreeQueryAndDrainsAtOnce()
    {
        using GraphVault vault = GraphVault.Copy("rescan-close-cancels");
        PumpedDispatcher.Run(() =>
        {
            var host = new Host(vault.Root);
            try
            {
                using var cancellation = new CancellationTokenSource();
                (_, ManualResetEventSlim parked, Func<bool> observed) = ParkTheProbesTreeQuery(host, vault, cancellation.Token);
                using (parked)
                {
                    var clock = System.Diagnostics.Stopwatch.StartNew();
                    host.Workspace.Dispose();
                    TimeSpan took = clock.Elapsed;

                    Assert.True(observed(), "the teardown never cancelled the native query");
                    Assert.True(took < TimeSpan.FromSeconds(5), $"the teardown froze for {took}");
                }
            }
            finally
            {
                host.Session.Dispose();
            }
        });
    }

    /// <summary>The graph tab open and loaded; a rescan's graph probe then
    /// issues the document's superseding pair, whose native snapshot query
    /// parks until its token is cancelled.</summary>
    private static (ManualResetEventSlim Parked, Func<bool> Observed) ParkTheProbesGraphQuery(
        Host host,
        GraphVault vault,
        CancellationToken cancellation)
    {
        host.Workspace.OpenGraph();
        Graph.GraphDocumentViewModel document = Assert.IsType<Graph.GraphDocumentViewModel>(host.Workspace.GraphDocument);
        PumpedDispatcher.PumpUntilDrained(document.WhenAllWorkDrained());
        PumpedDispatcher.Drain();
        File.WriteAllText(Path.Combine(vault.Root, "rescan-graph.md"), "[[hub]]\n");
        using (var scan = new CancelToken())
        {
            _ = host.Session.Rescan(scan);
        }

        var parked = new ManualResetEventSlim(false);
        bool observed = false;
        document.BeforeGraphQueryForTests = cancel =>
        {
            parked.Set();
            var clock = System.Diagnostics.Stopwatch.StartNew();
            while (!cancel.IsCancelled() && clock.Elapsed < TimeSpan.FromSeconds(30))
            {
                Thread.Sleep(10);
            }

            Volatile.Write(ref observed, cancel.IsCancelled());
        };
        _ = host.Workspace.ReSyncDependentsAsync(
            new WorkspaceViewModel.RescanDocumentsOutcome(
                0,
                new HashSet<string>(StringComparer.Ordinal),
                new HashSet<Bases.BaseDocumentViewModel>()),
            new Dictionary<string, WorkspaceViewModel.IndexedPath>(StringComparer.Ordinal),
            cancellation);
        Assert.True(PumpedDispatcher.PumpUntil(() => parked.IsSet), "the probe's graph query parking");
        return (parked, () => Volatile.Read(ref observed));
    }

    /// <summary>W7-7 PR 7 (codex PR 7 round 4, finding 5), the graph
    /// document's arm: the rescan's token reaches the native snapshot query
    /// its probe issued — cancelling the run cancels the query in flight.</summary>
    [Fact]
    public void TheRescansTokenReachesTheProbesGraphQuery()
    {
        using GraphVault vault = GraphVault.Copy("rescan-graph-token");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            using var cancellation = new CancellationTokenSource();
            (ManualResetEventSlim parked, Func<bool> observed) = ParkTheProbesGraphQuery(host, vault, cancellation.Token);
            using (parked)
            {
                cancellation.Cancel();
                var clock = System.Diagnostics.Stopwatch.StartNew();
                while (!observed() && clock.Elapsed < TimeSpan.FromSeconds(5))
                {
                    Thread.Sleep(10);
                }

                Assert.True(observed(), "the native graph query never saw the rescan's cancellation");
            }
        });
    }

    /// <summary>W7-7 PR 7 (codex PR 7 round 4, finding 5), the graph
    /// document's arm: Close Vault with the probe's native graph query in
    /// flight cancels it through the document's retirement — the teardown
    /// drains at once, the dispatcher never frozen behind the query.</summary>
    [Fact]
    public void AClosesTeardownCancelsTheProbesGraphQueryAndDrainsAtOnce()
    {
        using GraphVault vault = GraphVault.Copy("rescan-graph-close");
        PumpedDispatcher.Run(() =>
        {
            var host = new Host(vault.Root);
            try
            {
                using var cancellation = new CancellationTokenSource();
                (ManualResetEventSlim parked, Func<bool> observed) = ParkTheProbesGraphQuery(host, vault, cancellation.Token);
                using (parked)
                {
                    var clock = System.Diagnostics.Stopwatch.StartNew();
                    host.Workspace.Dispose();
                    TimeSpan took = clock.Elapsed;

                    Assert.True(observed(), "the teardown never cancelled the native graph query");
                    Assert.True(took < TimeSpan.FromSeconds(5), $"the teardown froze for {took}");
                }
            }
            finally
            {
                host.Session.Dispose();
            }
        });
    }
}
