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
}
