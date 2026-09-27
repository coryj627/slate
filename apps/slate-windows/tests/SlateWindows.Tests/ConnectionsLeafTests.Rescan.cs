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
}
