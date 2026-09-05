// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Graph;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W6-2 PR A (#746), contract A-10: the Windows GraphAnnouncer — the
/// canvas announcer's sibling. Class membership is pinned from the Rust
/// side (the switch-list tripwire); these facts cover the window,
/// latest-wins, the High flush-and-drop, the relay, RenderLabel posting
/// nothing, and Shutdown dropping a pending line.
/// </summary>
public sealed class GraphAnnouncerTests
{
    private static GraphRowCopy Row(string label) => new(label, GraphNodeKind.Note, 2, 1, 0, false);

    private static string Render(GraphA11yEvent @event) =>
        SlateUniffiMethods.A11yRender(new A11yEvent.Graph(@event)).Text;

    [Fact]
    public void NavigationCoalescesLatestWinsWithinTheWindow()
    {
        PumpedDispatcher.Run(() =>
        {
            var posted = new List<string>();
            var announcer = new GraphAnnouncer(line => posted.Add(line.Text), TimeSpan.FromMinutes(5));
            announcer.Announce(new GraphA11yEvent.GraphRow(GraphVerbosity.Standard, Row("Alpha")));
            announcer.Announce(new GraphA11yEvent.GraphRow(GraphVerbosity.Standard, Row("Beta")));
            Assert.Empty(posted);
            announcer.FlushForTests();
            string beta = Render(new GraphA11yEvent.GraphRow(GraphVerbosity.Standard, Row("Beta")));
            Assert.Equal([beta], posted);
        });
    }

    [Fact]
    public void AnImmediateEventPostsAtOnceAndAHighEventDropsThePendingClasses()
    {
        PumpedDispatcher.Run(() =>
        {
            var posted = new List<string>();
            var announcer = new GraphAnnouncer(line => posted.Add(line.Text), TimeSpan.FromMinutes(5));
            announcer.Announce(new GraphA11yEvent.GraphRow(GraphVerbosity.Standard, Row("Alpha")));
            announcer.Announce(new GraphA11yEvent.GraphFilterCount(3, 9));
            Assert.Equal(2, announcer.PendingForTests);

            var opened = new GraphA11yEvent.GraphStatus(new GraphStatusNote.Opened());
            announcer.Announce(opened);
            Assert.Equal([Render(opened)], posted);
            Assert.Equal(2, announcer.PendingForTests);

            var blocked = new GraphA11yEvent.GraphBlocked(new GraphBlockedReason.LoadFailed("boom"));
            announcer.Announce(blocked);
            Assert.Equal([Render(opened), Render(blocked)], posted);
            Assert.Equal(0, announcer.PendingForTests);
            announcer.FlushForTests();
            Assert.Equal(2, posted.Count);
        });
    }

    [Fact]
    public void RelayPassesAGridEventThroughUncoalesced()
    {
        PumpedDispatcher.Run(() =>
        {
            var posted = new List<RenderedAnnouncement>();
            var announcer = new GraphAnnouncer(posted.Add);
            var sorted = new A11yEvent.GridSorted("Links in", false);
            announcer.Relay(sorted);
            RenderedAnnouncement expected = SlateUniffiMethods.A11yRender(sorted);
            Assert.Single(posted);
            Assert.Equal(expected.Text, posted[0].Text);
            Assert.Equal(expected.Priority, posted[0].Priority);
        });
    }

    [Fact]
    public void RenderLabelRendersWithoutPosting()
    {
        PumpedDispatcher.Run(() =>
        {
            var posted = new List<RenderedAnnouncement>();
            _ = new GraphAnnouncer(posted.Add);
            var row = new GraphA11yEvent.GraphRow(GraphVerbosity.Standard, Row("Alpha"));
            Assert.Equal(Render(row), GraphAnnouncer.RenderLabel(row));
            Assert.Empty(posted);
        });
    }

    [Fact]
    public void ShutdownDropsAPendingLineAndRefusesLaterPosts()
    {
        PumpedDispatcher.Run(() =>
        {
            var posted = new List<string>();
            var announcer = new GraphAnnouncer(line => posted.Add(line.Text), TimeSpan.FromMinutes(5));
            announcer.Announce(new GraphA11yEvent.GraphRow(GraphVerbosity.Standard, Row("Alpha")));
            Assert.Equal(1, announcer.PendingForTests);
            announcer.Shutdown();
            Assert.Equal(0, announcer.PendingForTests);
            announcer.FlushForTests();
            Assert.Empty(posted);
            Assert.True(announcer.IsRetired);
        });
    }

    // --- A-10 as amended (W6-2 PR B, BD-12): the stored fire-time gate ------

    /// <summary>0a-9's fire-time gate: the filter count's gate is stored
    /// with the pending line and re-checked when the window elapses — a
    /// count queued while the graph tab was effective is DROPPED if the
    /// tab left effective before it spoke (the mac's `:150`, `:194–207`).</summary>
    [Fact]
    public void TheFilterCountsGateIsStoredWithTheLineAndReadAtFire()
    {
        PumpedDispatcher.Run(() =>
        {
            var posted = new List<string>();
            var announcer = new GraphAnnouncer(line => posted.Add(line.Text), TimeSpan.FromSeconds(60));
            bool effective = true;

            // Queued effective; left effective before the fire: dropped.
            announcer.AnnounceGatedFilterCount(new GraphA11yEvent.GraphFilterCount(3, 10), () => effective);
            Assert.Equal(1, announcer.PendingForTests);
            effective = false;
            announcer.FlushForTests();
            Assert.Empty(posted);
            Assert.Equal(1, announcer.DroppedAtFireForTests);
            Assert.Equal(0, announcer.PendingForTests);

            // Queued and still effective at the fire: posted.
            effective = true;
            announcer.AnnounceGatedFilterCount(new GraphA11yEvent.GraphFilterCount(3, 10), () => effective);
            announcer.FlushForTests();
            Assert.Equal([Render(new GraphA11yEvent.GraphFilterCount(3, 10))], posted);
            Assert.Equal(1, announcer.DroppedAtFireForTests);

            // Latest wins carries the LATEST gate: an ungated re-queue of the
            // same class replaces a gated line, and posts.
            posted.Clear();
            effective = false;
            announcer.AnnounceGatedFilterCount(new GraphA11yEvent.GraphFilterCount(1, 10), () => effective);
            announcer.Announce(new GraphA11yEvent.GraphFilterCount(2, 10));
            announcer.FlushForTests();
            Assert.Equal([Render(new GraphA11yEvent.GraphFilterCount(2, 10))], posted);
        });
    }

    /// <summary>One relay for both surfaces (A-10 as amended): a High from
    /// EITHER surface drops the other's pending classes — the table's
    /// pending count dies to the leaf's failure line, as the mac's one
    /// announcer has it (`GraphAnnouncer.swift:183–188`).</summary>
    [Fact]
    public void AHighFromTheOtherSurfaceDropsThePendingCount()
    {
        PumpedDispatcher.Run(() =>
        {
            var posted = new List<string>();
            var announcer = new GraphAnnouncer(line => posted.Add(line.Text), TimeSpan.FromSeconds(60));
            announcer.AnnounceGatedFilterCount(new GraphA11yEvent.GraphFilterCount(3, 10), () => true);
            Assert.Equal(1, announcer.PendingForTests);

            announcer.Announce(new GraphA11yEvent.GraphBlocked(new GraphBlockedReason.ConnectionsLoadFailed("io error")));

            Assert.Equal(0, announcer.PendingForTests);
            announcer.FlushForTests();
            Assert.Equal(
                [Render(new GraphA11yEvent.GraphBlocked(new GraphBlockedReason.ConnectionsLoadFailed("io error")))],
                posted);
        });
    }

    /// <summary>A-1 as amended: a document's retirement drops its pending
    /// classes and leaves the relay LIVE for the other surface.</summary>
    [Fact]
    public void DropAllPendingLeavesTheRelayLive()
    {
        PumpedDispatcher.Run(() =>
        {
            var posted = new List<string>();
            var announcer = new GraphAnnouncer(line => posted.Add(line.Text), TimeSpan.FromSeconds(60));
            announcer.Announce(new GraphA11yEvent.GraphRow(GraphVerbosity.Standard, Row("Alpha")));
            Assert.Equal(1, announcer.PendingForTests);

            announcer.DropAllPending();

            Assert.Equal(0, announcer.PendingForTests);
            Assert.False(announcer.IsRetired);
            announcer.Announce(new GraphA11yEvent.GraphStatus(new GraphStatusNote.ConnectionsPanel()));
            Assert.Equal([Render(new GraphA11yEvent.GraphStatus(new GraphStatusNote.ConnectionsPanel()))], posted);
            Assert.Equal(0, announcer.RefusedAfterShutdownForTests);
        });
    }
}
