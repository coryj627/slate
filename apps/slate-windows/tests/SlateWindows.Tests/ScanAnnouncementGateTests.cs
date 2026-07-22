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
