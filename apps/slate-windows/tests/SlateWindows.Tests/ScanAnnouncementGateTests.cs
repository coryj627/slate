// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

public sealed class ScanAnnouncementGateTests
{
    private DateTimeOffset _now = DateTimeOffset.UnixEpoch;

    [Fact]
    public void StartedAndFinishedAreForcedCanonicalMediumAnnouncements()
    {
        var gate = new ScanAnnouncementGate(() => _now);

        RenderedAnnouncement started = Render(gate.Started(1));
        RenderedAnnouncement finished = Render(gate.Finished(1));

        Assert.Equal("Scanning vault. 1 file to index.", started.Text);
        Assert.Equal(A11yPriority.Medium, started.Priority);
        Assert.Equal("Scan complete. 1 file indexed.", finished.Text);
        Assert.Equal(A11yPriority.Medium, finished.Priority);
    }

    [Fact]
    public void FileProgressIsLimitedToAboutThreeAnnouncementsPerSecond()
    {
        var gate = new ScanAnnouncementGate(() => _now);
        var announcements = new List<A11yEvent> { gate.Started(30) };

        for (ulong index = 1; index <= 30; index++)
        {
            _now += TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 30);
            A11yEvent? announcement = gate.FileIndexed(index, 30);
            if (announcement is not null)
            {
                announcements.Add(announcement);
            }
        }

        Assert.InRange(announcements.Count, 2, 4);
        Assert.All(announcements, announcement =>
            Assert.Equal(A11yPriority.Medium, Render(announcement).Priority));
    }

    [Fact]
    public void ProgressInsideCooldownIsSilentAndLatestEligibleCountIsSpoken()
    {
        var gate = new ScanAnnouncementGate(() => _now);
        _ = gate.Started(10);

        _now += ScanAnnouncementGate.MinimumInterval - TimeSpan.FromMilliseconds(1);
        Assert.Null(gate.FileIndexed(6, 10));

        _now += TimeSpan.FromMilliseconds(1);
        A11yEvent announcement = Assert.IsAssignableFrom<A11yEvent>(gate.FileIndexed(7, 10));
        Assert.Equal("Indexed 7 of 10 files.", Render(announcement).Text);
    }

    [Fact]
    public void FinishedBypassesCooldownAndResetRearmsProgress()
    {
        var gate = new ScanAnnouncementGate(() => _now);
        _ = gate.Started(5);

        Assert.Equal("Scan complete. 5 files indexed.", Render(gate.Finished(5)).Text);

        gate.Reset();
        A11yEvent announcement = Assert.IsAssignableFrom<A11yEvent>(gate.FileIndexed(1, 5));
        Assert.Equal("Indexed 1 of 5 files.", Render(announcement).Text);
    }

    private static RenderedAnnouncement Render(A11yEvent @event) =>
        SlateUniffiMethods.A11yRender(@event);
}

public sealed class UiProgressListenerTests
{
    [Fact]
    public void BoundedDrainPreservesStartLatestProgressAndTerminalInOrder()
    {
        var queued = new List<Action>();
        var emitted = new List<ScanProgress>();
        var listener = new UiProgressListener(queued.Add, emitted.Add);
        var report = new ScanReport(2, 2, 0, 32, []);

        listener.OnProgress(new ScanProgress.Started(2));
        listener.OnProgress(new ScanProgress.FileIndexed("a.md", 1, 2));
        listener.OnProgress(new ScanProgress.FileIndexed("b.md", 2, 2));
        listener.OnProgress(new ScanProgress.Finished(report));

        Action drain = Assert.Single(queued);
        Assert.Empty(emitted);
        drain();

        Assert.Collection(
            emitted,
            @event => Assert.Equal(2UL, Assert.IsType<ScanProgress.Started>(@event).TotalFiles),
            @event =>
            {
                ScanProgress.FileIndexed indexed = Assert.IsType<ScanProgress.FileIndexed>(@event);
                Assert.Equal("b.md", indexed.Path);
                Assert.Equal(2UL, indexed.Indexed);
                Assert.Equal(2UL, indexed.Total);
            },
            @event => Assert.Equal(report, Assert.IsType<ScanProgress.Finished>(@event).Report));

        listener.OnProgress(new ScanProgress.FileIndexed("late.md", 3, 3));
        Assert.Single(queued);
        Assert.Equal(3, emitted.Count);
    }

    [Fact]
    public void DrainSchedulesEventsAdmittedReentrantlyDuringEmission()
    {
        var queued = new List<Action>();
        var emitted = new List<ScanProgress>();
        var report = new ScanReport(2, 0, 2, 0, []);
        UiProgressListener listener = null!;
        listener = new UiProgressListener(
            queued.Add,
            @event =>
            {
                emitted.Add(@event);
                if (@event is ScanProgress.Started)
                {
                    listener.OnProgress(new ScanProgress.FileIndexed("b.md", 2, 2));
                    listener.OnProgress(new ScanProgress.Finished(report));
                }
            });

        listener.OnProgress(new ScanProgress.Started(2));
        Assert.Single(queued)();

        Assert.Single(emitted);
        Assert.Equal(2, queued.Count);
        queued[1]();

        Assert.Collection(
            emitted,
            @event => Assert.IsType<ScanProgress.Started>(@event),
            @event => Assert.IsType<ScanProgress.FileIndexed>(@event),
            @event => Assert.Equal(report, Assert.IsType<ScanProgress.Finished>(@event).Report));
    }

    [Fact]
    public async Task FastCachedScanStillPublishesTruthfulFinalRangeAndAnnouncements()
    {
        using FixtureVault fixture = FixtureVault.Create(2, "progress-mailbox");
        using (VaultSession warmSession = VaultSession.OpenFilesystem(fixture.Root))
        using (var warmCancel = new CancelToken())
        {
            ScanReport warmReport = warmSession.ScanInitial(warmCancel);
            Assert.Equal(2UL, warmReport.FilesSeen);
            Assert.Equal(2UL, warmReport.FilesIndexed);
        }

        string deviceStateRoot = fixture.Root + "-device-state";
        try
        {
            var queued = new List<Action>();
            var announcements = new List<A11yEvent>();
            using var lifecycle = new VaultLifecycleViewModel(
                pickVault: () => Task.FromResult<string?>(fixture.Root),
                enqueueUi: queued.Add,
                recentVaultsStore: new RecentVaultsStore(
                    Path.Combine(deviceStateRoot, "recent-vaults.json")),
                announce: announcements.Add,
                scanClock: () => DateTimeOffset.UnixEpoch,
                sessionLoadWorker: work => Task.FromResult(work()));

            await lifecycle.OpenVaultAsync(fixture.Root);

            Assert.NotEmpty(queued);
            Assert.Empty(announcements);
            Assert.Equal(2, lifecycle.ProgressMaximum);
            Assert.Equal(2, lifecycle.ProgressValue);
            Assert.False(lifecycle.IsProgressIndeterminate);

            foreach (Action pendingUiWork in queued.ToArray())
            {
                pendingUiWork();
            }

            Assert.Equal(2, lifecycle.ProgressMaximum);
            Assert.Equal(2, lifecycle.ProgressValue);
            Assert.False(lifecycle.IsProgressIndeterminate);
            Assert.Equal(
                [
                    "Scanning vault. 2 files to index.",
                    "Scan complete. 0 files indexed.",
                ],
                announcements
                    .Select(SlateUniffiMethods.A11yRender)
                    .Select(announcement => announcement.Text)
                    .ToArray());
        }
        finally
        {
            if (Directory.Exists(deviceStateRoot))
            {
                Directory.Delete(deviceStateRoot, recursive: true);
            }
        }
    }
}
