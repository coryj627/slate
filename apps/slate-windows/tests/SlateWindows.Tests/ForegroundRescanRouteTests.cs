// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// Plan 43 PR 20b (#1267; design §4.5 item 4, §4.7): the foreground route
/// and core's vault watcher. No Watcher run starts while the main window is
/// away: the request waits for the next activation, where it joins the
/// route's foreground request when the route forwards one — one run,
/// Foreground reason, so the return still says what changed while the user
/// was away — and otherwise runs on its own. The route's other rules
/// (W7-7 PR 7) are <c>RescanTests</c>' "foreground route" facts.
/// </summary>
public sealed class ForegroundRescanRouteTests
{
    private const string OneNew = "Files refreshed. 1 new or changed, 0 removed.";

    /// <summary>The route's half, with fakes: every activation — the first,
    /// and a forwarded return alike — asks for the deferred watcher request
    /// after its own decision, so a forwarded foreground request is made
    /// first and takes it; a route that forwards nothing returns the
    /// deferred request's run.</summary>
    [Fact]
    public void EveryActivationServesTheDeferredWatcherRequestAfterTheRoutesOwnDecision()
    {
        DateTimeOffset now = DateTimeOffset.UnixEpoch;
        var calls = new List<string>();
        var route = new ForegroundRescanRoute(
            reason =>
            {
                calls.Add(reason.ToString());
                return Task.CompletedTask;
            },
            () => false,
            () => now,
            () =>
            {
                calls.Add("deferred");
                return null;
            });

        Assert.Null(route.OnActivated());
        route.OnDeactivated();
        now += ForegroundRescanRoute.MinimumAway;
        Assert.NotNull(route.OnActivated());
        Assert.Equal(["deferred", nameof(RescanReason.Foreground), "deferred"], calls);

        var served = new TaskCompletionSource();
        var alone = new ForegroundRescanRoute(
            _ => throw new Xunit.Sdk.XunitException("the first activation forwards no foreground request"),
            () => false,
            () => now,
            () => served.Task);
        Assert.Same(served.Task, alone.OnActivated());
    }

    /// <summary>The whole path, through the real lifecycle: a watcher request
    /// while the window is away runs nothing and says nothing; a return after
    /// two seconds makes ONE run, Foreground reason, that speaks what changed
    /// while away; a return the route does not forward (under two seconds)
    /// runs the deferred request on its own, a Watcher run, a second after
    /// the last scan (core spaced it, so no cooldown); and an activation with
    /// nothing deferred runs nothing.</summary>
    [Fact]
    public void AWatcherRequestWhileInactiveRunsOnActivation() => StaThread.Run(
        () =>
        {
            using var h = new RescanTests.Harness("watcher-away", ("alpha.md", "# Alpha\n"));
            var route = new ForegroundRescanRoute(
                h.Lifecycle.RescanAsync,
                () => false,
                () => h.Now,
                h.Lifecycle.ServeWatcherRequestOnActivation);
            Assert.Null(route.OnActivated());
            int scans = h.ScanCalls;

            h.WindowActive = false;
            route.OnDeactivated();
            h.Write("away.md", "# Away\n");
            h.SignalExternalChange();
            h.Settle();
            Assert.Equal(scans, h.ScanCalls);
            Assert.Empty(h.Events);

            h.Now += TimeSpan.FromSeconds(10);
            h.WindowActive = true;
            Task? returned = route.OnActivated();
            Assert.NotNull(returned);
            h.Context.Await(returned!);
            Assert.Equal(scans + 1, h.ScanCalls);
            Assert.Equal(
                RescanReason.Foreground,
                Assert.IsType<A11yEvent.VaultRescanFinished>(Assert.Single(h.Events)).Reason);
            Assert.Equal([OneNew], h.Spoken);
            Assert.Contains(h.Sidebar.RootNodes, node => node.Path == "away.md");

            h.Events.Clear();
            h.WindowActive = false;
            route.OnDeactivated();
            h.Write("brief.md", "# Brief\n");
            h.SignalExternalChange();
            h.Settle();
            Assert.Equal(scans + 1, h.ScanCalls);
            h.Now += TimeSpan.FromSeconds(1);
            h.WindowActive = true;
            Task? alone = route.OnActivated();
            Assert.NotNull(alone);
            h.Context.Await(alone!);
            Assert.Equal(scans + 2, h.ScanCalls);
            Assert.Equal(
                RescanReason.Watcher,
                Assert.IsType<A11yEvent.VaultRescanFinished>(Assert.Single(h.Events)).Reason);
            Assert.Equal([OneNew], h.Spoken);
            Assert.Contains(h.Sidebar.RootNodes, node => node.Path == "brief.md");

            h.Events.Clear();
            h.WindowActive = false;
            route.OnDeactivated();
            h.Now += TimeSpan.FromSeconds(10);
            h.WindowActive = true;
            Task? quiet = route.OnActivated();
            Assert.NotNull(quiet);
            h.Context.Await(quiet!);
            h.WindowActive = false;
            route.OnDeactivated();
            h.Now += TimeSpan.FromSeconds(1);
            h.WindowActive = true;
            Assert.Null(route.OnActivated());
            Assert.Equal(scans + 3, h.ScanCalls);
            Assert.Empty(h.Events);
        },
        TimeSpan.FromSeconds(120),
        "STA test body timed out.");
}
