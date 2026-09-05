// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Panels;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W4-8 (#740): the sync-diagnostics document under PRODUCTION
/// scheduling (synchronousForTests: false). Synchronous mode orders
/// every probe deterministically, which makes the SD5 publish guard
/// dead code under every other sync fact — these facts hold a worker
/// at the VM's interleave seam so the race is a deterministic
/// schedule, never a timing bet (the history/citations async-suite
/// lesson).
///
/// The VM is constructed with a NULL SynchronizationContext so
/// publishes run inline on the worker: after a drain, every publish
/// has been applied (with xunit's context they would still be
/// queued).
/// </summary>
public sealed class SyncDiagnosticsAsyncTests : IDisposable
{
    private readonly string _root;
    private readonly VaultSession _session;

    public SyncDiagnosticsAsyncTests()
    {
        _root = Path.Combine(
            Path.GetTempPath(),
            $"slate-windows-test-sync-async-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "seed.md"), "# Seed\n");
        _session = VaultSession.OpenFilesystem(_root);
        using var cancel = new CancelToken();
        _session.ScanInitial(cancel);
    }

    public void Dispose()
    {
        _session.Dispose();
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private SyncDiagnosticsViewModel NewAsyncPanel()
    {
        SynchronizationContext? previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(null);
        try
        {
            return new SyncDiagnosticsViewModel(_session, synchronousForTests: false);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    private static async Task QuiesceAsync(SyncDiagnosticsViewModel panel)
    {
        for (int round = 0; round < 10; round++)
        {
            await panel.DrainForTests();
            await Task.Delay(2);
        }
    }

    [Fact]
    public async Task AStaleProbeNeverClobbersANewerReport()
    {
        Directory.CreateDirectory(Path.Combine(_root, ".git"));
        SyncDiagnosticsViewModel panel = NewAsyncPanel();
        using var parked = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        int hits = 0;
        // Only the FIRST load parks: the seam runs after both FFI
        // calls, so load one's report is fixed at {Git} while it waits.
        panel.LoadInterleaveForTests = () =>
        {
            if (Interlocked.Increment(ref hits) == 1)
            {
                parked.Set();
                _ = release.Wait(TimeSpan.FromSeconds(10));
            }
        };

        panel.Reload();
        Assert.True(parked.Wait(TimeSpan.FromSeconds(10)));
        // The vault gains a marker while load one is parked, so the
        // two probes see genuinely different worlds.
        Directory.CreateDirectory(Path.Combine(_root, ".stfolder"));
        panel.Reload();
        Assert.True(SpinWait.SpinUntil(
            () => panel.Report is { } report && report.Providers.Length == 2,
            TimeSpan.FromSeconds(10)));

        release.Set();
        panel.LoadInterleaveForTests = null;
        await QuiesceAsync(panel);

        // SDINV-3: load one resumed LAST and its publish was dropped by
        // the sequence guard. Without the bump it would overwrite the
        // marker-positive report with its own one-provider snapshot —
        // the vault would look clean until the next refresh.
        Assert.Equal(2, panel.Report!.Providers.Length);
        Assert.Contains(
            panel.Report.Providers, p => p.Kind == SyncProviderKind.Syncthing);
        panel.Shutdown();
    }

    [Fact]
    public async Task ShutdownDropsAPublishThatIsAlreadyInFlight()
    {
        Directory.CreateDirectory(Path.Combine(_root, ".git"));
        SyncDiagnosticsViewModel panel = NewAsyncPanel();
        int publishes = 0;
        panel.Published += (_, _) => publishes++;
        using var parked = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        panel.LoadInterleaveForTests = () =>
        {
            parked.Set();
            _ = release.Wait(TimeSpan.FromSeconds(10));
        };

        panel.Reload();
        Assert.True(parked.Wait(TimeSpan.FromSeconds(10)));
        // Teardown lands while the probe holds a completed report.
        panel.Shutdown();
        release.Set();
        panel.LoadInterleaveForTests = null;
        await QuiesceAsync(panel);

        // SDINV-8: nothing publishes into a dying UI. (IsLoading stays
        // set — the publish that would have cleared it was the one
        // dropped; nothing renders a shut-down leaf.)
        Assert.Equal(0, publishes);
        Assert.Null(panel.Report);
    }

    [Fact]
    public async Task AReloadAfterShutdownNeverStartsWork()
    {
        SyncDiagnosticsViewModel panel = NewAsyncPanel();
        panel.Shutdown();

        panel.Reload();
        await QuiesceAsync(panel);

        Assert.Null(panel.Report);
        Assert.False(panel.IsLoading);
    }
}
