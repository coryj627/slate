// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W7-7 PR 7 (#1252, R-9) merged with PR 6 follow-up B (#1279, #1280): a
/// reading refresh's core walk is retired when a newer refresh, a detach or
/// a disposal supersedes it, and a note's save writes off the dispatcher.
/// The rescan's silent re-projection still settles when its fetch is
/// retired, the re-sync never reloads a tab over a save it admitted, and a
/// close during a rescan speaks the close's one line and nothing of the
/// rescan's.
/// </summary>
public sealed partial class RescanTests
{
    /// <summary>The rescan's re-projection parked on its fetch worker, the
    /// reader switching away (the surface detached) meanwhile: follow-up B's
    /// stage check retires the fetch, and with no newer refresh live the
    /// rescan's waiter settles — the run's reading operation completes, not
    /// failed, and nothing is said. It used to wait for a publication that
    /// was never coming.</summary>
    [Fact]
    public void ARescansReprojectionRetiredByADetachStillSettles() => RunSta(() => PumpedDispatcher.Run(() =>
    {
        using var w = new BackgroundReadingWorkspace("retired-by-detach");
        using var parked = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        ParkTheFirstStage(w.Reading, parked, release);
        w.Announced.Clear();
        using var cancellation = new CancellationTokenSource();

        Task<ulong> dependents = w.ReSyncDependentsAsync(cancellation.Token);
        Assert.True(PumpedDispatcher.PumpUntil(() => parked.IsSet), "the rescan's fetch parking");
        w.Reading.OnSurfaceDetached();
        release.Set();

        Assert.True(
            PumpedDispatcher.PumpUntil(() => dependents.IsCompleted, TimeSpan.FromSeconds(20)),
            "the retired re-projection never settled the rescan");
        Assert.Equal(0UL, dependents.Result);
        Assert.Equal(1, w.Reading.FetchesCancelledForTests);
        Assert.Empty(w.Announced);
    }));

    /// <summary>The rescan's re-projection parked on its fetch worker, a
    /// newer refresh (the reader's own) started meanwhile: the rescan's fetch
    /// is retired and its waiter settles with the newer refresh's
    /// publication.</summary>
    [Fact]
    public void ARescansReprojectionSupersededByANewerRefreshSettlesWithIt() => RunSta(() => PumpedDispatcher.Run(() =>
    {
        using var w = new BackgroundReadingWorkspace("retired-by-refresh");
        using var parked = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        ParkTheFirstStage(w.Reading, parked, release);
        w.Announced.Clear();
        using var cancellation = new CancellationTokenSource();

        Task<ulong> dependents = w.ReSyncDependentsAsync(cancellation.Token);
        Assert.True(PumpedDispatcher.PumpUntil(() => parked.IsSet), "the rescan's fetch parking");
        w.Reading.Refresh();
        release.Set();

        Assert.True(
            PumpedDispatcher.PumpUntil(() => dependents.IsCompleted, TimeSpan.FromSeconds(20)),
            "the superseded re-projection never settled the rescan");
        Assert.Equal(0UL, dependents.Result);
        Assert.Equal(1, w.Reading.FetchesCancelledForTests);
        Assert.True(PumpedDispatcher.PumpUntil(() => w.Reading.WhenRefreshWorkDrained().IsCompleted));
        Assert.Empty(w.Announced);
    }));

    /// <summary>A note's save has written and waits to publish when the reader
    /// takes the edit back — the tab clean against the baseline the save has
    /// not replaced yet — and a rescan runs: the index already holds the
    /// save's bytes, but the re-sync never reloads the tab under the save it
    /// admitted. The save then publishes as the tab's baseline, and the
    /// reader's text stays, an unsaved change behind it.</summary>
    [Fact]
    public void ARescanNeverReloadsOverAnAdmittedSave() => RunSta(() =>
    {
        using var h = new Harness("rescan-over-save", ("x.md", "x0\n"));
        WorkspaceTabViewModel x = h.Open("x.md");
        x.Text = "x0\nsaved\n";
        using var written = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        x.SaveWrittenHookForTests = () =>
        {
            written.Set();
            _ = release.Wait(TimeSpan.FromSeconds(30));
        };
        try
        {
            Task<bool> save = x.SaveAsync();
            h.Context.RunUntil(() => written.IsSet, "the save's write landing");
            x.Text = "x0\n";
            Assert.False(x.IsDirty);
            Assert.True(x.HasPendingSaves);
            h.Events.Clear();

            h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

            Assert.Equal("x0\n", x.Text);
            release.Set();
            h.PumpUntil(() => save.IsCompleted, "the save's publication");
            Assert.True(save.Result);
            Assert.Equal("x0\n", x.Text);
            Assert.True(x.IsDirty, "the reader's text lost its place behind the saved bytes");
            Assert.False(x.IsExternallyStale);
            Assert.Equal("x0\nsaved\n", File.ReadAllText(Path.Combine(h.Root, "x.md")));
            Assert.DoesNotContain(h.Events, e => e is A11yEvent.VaultRescanIncomplete);
        }
        finally
        {
            release.Set();
            x.SaveWrittenHookForTests = null;
        }
    });

    /// <summary>A note renamed outside Slate (case only) while its save waits
    /// to write, the reader having taken the edit back: the rescan's re-seat
    /// treats the tab as it treats a dirty one — it takes the stored spelling
    /// and keeps its buffer and its document, so the save publishes to the
    /// tab it was admitted for. It used to replace the tab's item under the
    /// save, which then took no state.</summary>
    [Fact]
    public void ARescansReseatKeepsATabWithAnAdmittedSave() => RunSta(() =>
    {
        using var h = new Harness("reseat-over-save", ("x.md", "x0\n"));
        if (!h.VolumeAliasesCase())
        {
            return;
        }

        WorkspaceTabViewModel x = h.Open("x.md");
        x.Text = "x0\nsaved\n";
        using var writing = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        x.SaveWriteHookForTests = () =>
        {
            writing.Set();
            _ = release.Wait(TimeSpan.FromSeconds(30));
        };
        try
        {
            Task<bool> save = x.SaveAsync();
            h.Context.RunUntil(() => writing.IsSet, "the save parking before its write");
            x.Text = "x0\n";
            Assert.False(x.IsDirty);
            int document = x.ItemIdentity;
            File.Move(Path.Combine(h.Root, "x.md"), Path.Combine(h.Root, "X.md"));

            h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

            Assert.Equal("X.md", x.Path);
            Assert.False(x.IsMissingFromDisk);
            Assert.Equal(document, x.ItemIdentity);
            Assert.Equal("x0\n", x.Text);
            release.Set();
            h.PumpUntil(() => save.IsCompleted, "the save's publication");
            Assert.True(save.Result);
            Assert.Equal("x0\nsaved\n", File.ReadAllText(Path.Combine(h.Root, "X.md")));
        }
        finally
        {
            release.Set();
            x.SaveWriteHookForTests = null;
        }
    });

    /// <summary>Follow-up B's one close line (OD-14): Close Vault during a
    /// rescan — its Bases publication parked — speaks exactly the close's
    /// line, VaultClosed, and nothing of the rescan's: no completion, and no
    /// close-family line of the run's own.</summary>
    [Fact]
    public void ACloseDuringARescanSpeaksOnlyTheClosesLine() => RunSta(() =>
    {
        using Harness h = DependentsHarness("close-line");
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
        h.Events.Clear();

        Task run = h.Lifecycle.RescanAsync(RescanReason.Explicit);
        h.PumpUntil(() => started, "the bases publication parking");
        h.Lifecycle.CloseVault();
        h.PumpUntil(() => run.IsCompleted, "the closed rescan ending");

        A11yEvent spoken = Assert.Single(h.Events);
        Assert.IsType<A11yEvent.VaultClosed>(spoken);
    });

    /// <summary>Park a reading fetch at its first stage, once.</summary>
    private static void ParkTheFirstStage(
        Reading.ReadingContentViewModel reading,
        ManualResetEventSlim parked,
        ManualResetEventSlim release) =>
        reading.FetchStageHookForTests = stage =>
        {
            if (!parked.IsSet)
            {
                parked.Set();
                _ = release.Wait(TimeSpan.FromSeconds(30));
            }
        };
}
