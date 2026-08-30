// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;
using System.Runtime.ExceptionServices;
using SlateWindows.Canvas;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W6-1 PR C-unit, task T2: the lease, its close-once record, and
/// obligation I1's ownership transfer.
/// </summary>
/// <remarks>
/// <para>
/// The I-row bar: code plus facts plus a mutation returning the
/// arrangement the finding NAMES. I1's arrangement is a refusal or
/// fault closing a lease a concurrent acceptance has just published,
/// and the mutation below returns exactly that by putting back the
/// observation the frozen design had — "close unless a final slot read
/// names my lease" — with the two steps no longer inside one gate.
/// </para>
/// <para>
/// The walks round 6 probed and could not settle in prose are facts
/// here: two racing loads, a load during a reload, and a refused
/// delivery whose lease is the live publication's.
/// </para>
/// </remarks>
public sealed class CanvasLeaseTransferTests
{
    private static readonly TimeSpan LivenessBudget = TimeSpan.FromSeconds(30);

    // ---------------------------------------------------------------
    // The lease and its close-once record
    // ---------------------------------------------------------------

    /// <summary>The close observation reaches exactly one however many
    /// times close is called — the instrument the frozen plan names for
    /// native release, since a weak reference proves nothing here in
    /// either direction.</summary>
    [Fact]
    public void CloseHappensExactlyOnceHoweverManyCallersAskForIt()
    {
        var closes = 0;
        var lease = new CanvasHandleLease(7, _ => Interlocked.Increment(ref closes));

        Assert.False(
            lease.IsClosed,
            "premise: a fresh lease is open, or the counts below start from the "
            + "wrong state.");

        bool first = lease.Close();
        bool second = lease.Close();
        bool third = lease.Close();

        Assert.True(
            first && !second && !third,
            "the close reports which call did it: the first, and only the first.");

        Assert.True(
            closes == 1,
            $"the handle was closed {closes} times; the close-once record exists "
            + "so that a repeat release is a no-op rather than a double free.");
        Assert.True(lease.IsClosed, "and the lease reports itself closed.");
    }

    /// <summary>Concurrent closes collapse to one, and every caller
    /// returns only after the handle is actually gone — which is why
    /// the exchange sits inside the lock rather than before it.</summary>
    /// <remarks>
    /// The waiting is ESTABLISHED rather than hoped for: the second
    /// caller is observed blocked in the lock — or finished, which is
    /// what the exchange-outside-the-lock mutant does — before the fact
    /// asks whether it returned. A yield in that place passed the
    /// mutant on any run where the second thread had not been scheduled
    /// yet, which the T2 review pointed out.
    /// </remarks>
    [Fact]
    public void ConcurrentClosesCollapseToOneAndAllWaitForIt()
    {
        var closes = 0;
        var bodyReleased = false;
        using var closeStarted = new ManualResetEventSlim(false);
        using var releaseClose = new ManualResetEventSlim(false);
        var lease = new CanvasHandleLease(7, handle =>
        {
            closeStarted.Set();
            bodyReleased = releaseClose.Wait(LivenessBudget);
            _ = Interlocked.Increment(ref closes);
        });

        var first = new Worker(() => _ = lease.Close());
        first.Start();
        Assert.True(
            closeStarted.Wait(LivenessBudget),
            "premise: the first close never entered its body.");

        var secondReturned = 0;
        var second = new Worker(() =>
        {
            _ = lease.Close();
            Volatile.Write(ref secondReturned, 1);
        });
        second.Start();

        // Until the second caller is BLOCKED — on the lock the first
        // holds — or has finished, which only the mutant manages while
        // the first is still inside the handle close.
        var stopwatch = Stopwatch.StartNew();
        while (!second.IsBlockedOrFinished && stopwatch.Elapsed < LivenessBudget)
        {
            Thread.Yield();
        }

        Assert.True(
            second.IsBlockedOrFinished,
            "premise: the second caller neither blocked nor finished, so the "
            + "arrangement did not establish.");
        Assert.True(
            Volatile.Read(ref secondReturned) == 0,
            "a second close returned while the first was still inside the handle "
            + "close; that would make IsClosed mean somebody STARTED closing.");

        releaseClose.Set();
        Assert.True(
            first.Join(LivenessBudget) && second.Join(LivenessBudget),
            "premise: a close thread did not finish.");
        Assert.True(bodyReleased, "premise: the close body was never released.");
        Assert.True(closes == 1, $"the handle closed {closes} times.");
    }

    /// <summary>An admitted call reaches the handle; a refused one does
    /// not. Admission is the caller's slot-derived predicate and the
    /// lease never consults its own close record to decide it.</summary>
    [Fact]
    public void InvokeRunsOnlyWhenAdmissionSaysSo()
    {
        var uses = 0;
        var lease = new CanvasHandleLease(11, _ => { });

        bool ran = lease.Invoke(() => true, handle =>
        {
            Assert.True(handle == 11, "the call receives the handle it leased.");
            _ = Interlocked.Increment(ref uses);
        });
        Assert.True(ran && uses == 1, "an admitted call runs exactly once.");

        bool refused = lease.Invoke(
            () => false, handle => Interlocked.Increment(ref uses));
        Assert.True(
            !refused && uses == 1,
            $"a refused call invoked the handle anyway ({uses} uses); admission is "
            + "evaluated INSIDE the lock precisely so that a refusal decided while "
            + "waiting for it still counts.");
    }

    // ---------------------------------------------------------------
    // Obligation I1: the ownership transfer
    // ---------------------------------------------------------------

    /// <summary>
    /// A release whose lease the live publication NAMES closes nothing.
    /// This is the round-6 walk — a refused delivery whose lease equals
    /// the live publication's — and the reason release is safe to call
    /// from a finally on the success path.
    /// </summary>
    [Fact]
    public void AReleaseWhoseLeaseThePublicationNamesClosesNothing()
    {
        var closes = 0;
        var slot = new CanvasPublicationSlot(CanvasPublication.Seed());
        var request = new CanvasRequestIdentity("L1");
        var lease = new CanvasHandleLease(1, _ => Interlocked.Increment(ref closes));

        _ = slot.Publish(s => s.WithLoads(s.Loads.Requested(request)));
        Assert.True(
            CanvasLeaseTransfer.TryAccept(slot, request, lease, Population("a", "b")).Accepted,
            "premise: the acceptance must land, or the release below is deciding "
            + "about a lease nobody published.");

        CanvasLeaseRelease outcome = CanvasLeaseTransfer.Release(slot, request, lease);

        Assert.True(
            outcome == CanvasLeaseRelease.Transferred,
            $"the release reported {outcome} for a lease the publication names.");
        Assert.True(
            closes == 0,
            $"the release closed a lease the live publication names ({closes} "
            + "closes). That is obligation I1's arrangement, and the publication "
            + "would now hold a closed handle.");
        Assert.True(
            slot.Current.Names(lease) && !lease.IsClosed,
            "and the publication still names an OPEN lease.");
    }

    /// <summary>A release that wins publishes a terminal state for its
    /// request BEFORE it closes, and that published state is what makes
    /// a later acceptance impossible rather than unlikely.</summary>
    /// <remarks>
    /// The ORDER is pinned by the close itself: the close delegate
    /// reads the slot at the moment it runs. End state alone cannot
    /// tell "published, then closed" from "closed, then published", and
    /// the second is exactly the window obligation I1 exists to remove
    /// — the T2 review found nothing else pinning it.
    /// </remarks>
    [Fact]
    public void AReleasePublishesTerminalStateBeforeItCloses()
    {
        var closes = 0;
        var slot = new CanvasPublicationSlot(CanvasPublication.Seed());
        var request = new CanvasRequestIdentity("L1");
        CanvasLoadDelivery? deliveryAtClose = null;
        var lease = new CanvasHandleLease(1, handle =>
        {
            deliveryAtClose = slot.Current.Loads.Delivery;
            _ = Interlocked.Increment(ref closes);
        });

        _ = slot.Publish(s => s.WithLoads(s.Loads.Requested(request)));
        Assert.True(
            slot.Current.Loads.Admits(request),
            "premise: the request must be admissible before the release, or the "
            + "refusal below proves nothing.");

        CanvasLeaseRelease outcome = CanvasLeaseTransfer.Release(slot, request, lease);

        Assert.True(
            outcome == CanvasLeaseRelease.Closed,
            $"the release reported {outcome} rather than closing.");
        Assert.True(closes == 1, $"the lease closed {closes} times.");
        Assert.True(
            deliveryAtClose == CanvasLoadDelivery.Released,
            $"at the moment the handle closed, the request's delivery read "
            + $"{deliveryAtClose} rather than Released: the close ran BEFORE the "
            + "terminal state was published, which is the window an acceptance "
            + "lands in.");
        Assert.True(
            slot.Current.Loads.Delivery == CanvasLoadDelivery.Released,
            "the terminal state was not published, so an acceptance could still "
            + "follow a close — which is the whole of obligation I1.");

        // The acceptance that would have raced it now cannot land.
        Assert.False(
            CanvasLeaseTransfer.TryAccept(slot, request, lease, Population("a")).Accepted,
            "an acceptance succeeded AFTER its request was released, so the "
            + "terminal publication did not make it impossible.");
        Assert.True(
            slot.Current.Lease is null,
            "and no closed lease reached the publication.");
    }

    /// <summary>A second release is a no-op and says so, without asking
    /// the close record who released it.</summary>
    [Fact]
    public void ASecondReleaseIsANoOpAndReportsIt()
    {
        var closes = 0;
        var slot = new CanvasPublicationSlot(CanvasPublication.Seed());
        var request = new CanvasRequestIdentity("L1");
        var lease = new CanvasHandleLease(1, _ => Interlocked.Increment(ref closes));

        _ = slot.Publish(s => s.WithLoads(s.Loads.Requested(request)));
        Assert.True(
            CanvasLeaseTransfer.Release(slot, request, lease) == CanvasLeaseRelease.Closed,
            "premise: the first release must close, or the second is not a repeat.");

        CanvasLeaseRelease again = CanvasLeaseTransfer.Release(slot, request, lease);

        Assert.True(
            again == CanvasLeaseRelease.AlreadyReleased,
            $"the second release reported {again}.");
        Assert.True(closes == 1, $"the handle closed {closes} times across two releases.");

        // And once a NEWER request has moved the schedule on, a third
        // release of the old one still reports what it did — nothing —
        // rather than re-deriving "closed" from a schedule that no
        // longer mentions it.
        _ = slot.Publish(s => s.WithLoads(s.Loads.Requested(new CanvasRequestIdentity("L2"))));
        CanvasLeaseRelease later = CanvasLeaseTransfer.Release(slot, request, lease);
        Assert.True(
            later == CanvasLeaseRelease.AlreadyReleased,
            $"the third release reported {later} once a newer request was latest.");
        Assert.True(closes == 1, $"the handle closed {closes} times across three releases.");
    }

    /// <summary>A superseded release closes its own lease and does NOT
    /// clobber the newer request's schedule entry.</summary>
    [Fact]
    public void ASupersededReleaseClosesWithoutClobberingTheNewerRequest()
    {
        var closes = 0;
        var slot = new CanvasPublicationSlot(CanvasPublication.Seed());
        var first = new CanvasRequestIdentity("L1");
        var second = new CanvasRequestIdentity("L2");
        var staleLease = new CanvasHandleLease(1, _ => Interlocked.Increment(ref closes));

        _ = slot.Publish(s => s.WithLoads(s.Loads.Requested(first)));
        _ = slot.Publish(s => s.WithLoads(s.Loads.Requested(second)));
        Assert.True(
            ReferenceEquals(slot.Current.Loads.Latest, second),
            "premise: the newer request must be latest before the stale release.");

        CanvasLeaseRelease outcome =
            CanvasLeaseTransfer.Release(slot, first, staleLease);

        Assert.True(
            outcome == CanvasLeaseRelease.Closed && closes == 1,
            $"the stale release reported {outcome} with {closes} closes; it owns "
            + "the lease it opened and nobody else will.");
        Assert.True(
            ReferenceEquals(slot.Current.Loads.Latest, second)
                && slot.Current.Loads.Admits(second),
            "the stale release overwrote the newer request's schedule entry with "
            + "an older request's name, so a live delivery has been refused by a "
            + "delivery that lost.");
    }

    /// <summary>
    /// Round 6's walk: two loads race, only the latest lands, and the
    /// loser's lease is closed exactly once.
    /// </summary>
    [Fact]
    public void TwoRacingLoadsLeaveOnePublicationAndOneClosedLease()
    {
        var slot = new CanvasPublicationSlot(CanvasPublication.Seed());
        var first = new CanvasRequestIdentity("L1");
        var second = new CanvasRequestIdentity("L2");
        var firstCloses = 0;
        var secondCloses = 0;
        var firstLease = new CanvasHandleLease(1, _ => Interlocked.Increment(ref firstCloses));
        var secondLease = new CanvasHandleLease(2, _ => Interlocked.Increment(ref secondCloses));

        // Both workers opened a handle; the second request superseded
        // the first before either delivered.
        _ = slot.Publish(s => s.WithLoads(s.Loads.Requested(first)));
        _ = slot.Publish(s => s.WithLoads(s.Loads.Requested(second)));

        bool firstAccepted = Deliver(slot, first, firstLease, Population("old"));
        bool secondAccepted = Deliver(slot, second, secondLease, Population("new"));

        Assert.True(
            !firstAccepted && secondAccepted,
            $"the stale delivery was accepted ({firstAccepted}) or the latest was "
            + $"refused ({secondAccepted}); only the latest request may land.");
        Assert.True(
            firstCloses == 1 && secondCloses == 0,
            $"the loser's lease closed {firstCloses} times and the winner's "
            + $"{secondCloses}; a refused delivery closes the handle it opened and "
            + "an accepted one hands it to the publication.");
        Assert.True(
            slot.Current.Names(secondLease)
                && slot.Current.Population!.Outline[0].NodeId == "new",
            "the publication names the winner's lease and its population.");
    }

    /// <summary>Round 6's other walk: a load delivering DURING a reload
    /// is refused, and the reload's own lease survives.</summary>
    [Fact]
    public void ALoadDeliveringDuringAReloadIsRefused()
    {
        var slot = new CanvasPublicationSlot(CanvasPublication.Seed());
        var original = new CanvasRequestIdentity("L1");
        var reload = new CanvasRequestIdentity("L2");
        var originalCloses = 0;
        var reloadCloses = 0;
        var originalLease = new CanvasHandleLease(1, _ => Interlocked.Increment(ref originalCloses));
        var reloadLease = new CanvasHandleLease(2, _ => Interlocked.Increment(ref reloadCloses));

        _ = slot.Publish(s => s.WithLoads(s.Loads.Requested(original)));
        Assert.True(
            Deliver(slot, original, originalLease, Population("first")),
            "premise: the first load must land, or there is no reload to deliver "
            + "during.");

        // The reload is requested; the ORIGINAL delivery arrives again.
        _ = slot.Publish(s => s.WithLoads(s.Loads.Requested(reload)));
        bool lateOriginal = Deliver(slot, original, originalLease, Population("stale"));

        Assert.False(
            lateOriginal,
            "a delivery for a superseded request landed during a reload.");
        Assert.True(
            slot.Current.Names(originalLease),
            "premise: the publication still names the first lease until the reload "
            + "lands, which is what makes the release below the interesting case.");
        Assert.True(
            originalCloses == 0,
            $"the late delivery closed the lease the publication still names "
            + $"({originalCloses} closes) — obligation I1's arrangement arriving "
            + "through the reload door.");

        Assert.True(
            Deliver(slot, reload, reloadLease, Population("second")),
            "the reload lands.");
        Assert.True(
            reloadCloses == 0 && slot.Current.Names(reloadLease),
            "and the reload's lease is the live one.");
        Assert.True(
            originalCloses == 1 && originalLease.IsClosed,
            $"the reload displaced the first lease and closed it {originalCloses} "
            + "times; the swap that un-named it is the only thing that can still "
            + "reach it, so the swap's effect step owns that close.");
    }

    /// <summary>
    /// A closed lease is not resurrectable through a retained unit: the
    /// admission predicate refuses because the publication no longer
    /// names it, so nothing reaches the handle.
    /// </summary>
    [Fact]
    public void ARetainedUnitCannotResurrectAClosedLease()
    {
        var slot = new CanvasPublicationSlot(CanvasPublication.Seed());
        var first = new CanvasRequestIdentity("L1");
        var second = new CanvasRequestIdentity("L2");
        var firstLease = new CanvasHandleLease(1, _ => { });
        var secondLease = new CanvasHandleLease(2, _ => { });

        _ = slot.Publish(s => s.WithLoads(s.Loads.Requested(first)));
        Assert.True(
            Deliver(slot, first, firstLease, Population("a", "b")),
            "premise: the first load must land so there is a unit to retain.");

        CanvasProjectionUnit retained = slot.Current.Unit!;
        Assert.True(
            retained.VisibleCount == 2,
            "premise: the retained unit must actually project the first "
            + "population.");

        // A reload replaces the lease; the loser is the old one.
        _ = slot.Publish(s => s.WithLoads(s.Loads.Requested(second)));
        Assert.True(
            Deliver(slot, second, secondLease, Population("c")),
            "premise: the reload must land.");
        CanvasLeaseRelease released =
            CanvasLeaseTransfer.Release(slot, first, firstLease);
        Assert.True(
            released is CanvasLeaseRelease.Closed or CanvasLeaseRelease.AlreadyReleased,
            $"premise: the superseded lease must end up closed, not {released}.");
        Assert.True(firstLease.IsClosed, "premise: and it is closed.");

        var uses = 0;
        bool ran = firstLease.Invoke(
            () => slot.Current.Names(firstLease),
            handle => Interlocked.Increment(ref uses));

        Assert.True(
            !ran && uses == 0,
            $"a retained unit's query reached a CLOSED handle ({uses} uses). "
            + "Admission is the publication naming the lease, and a lease is "
            + "closed only when it does not — so the closed handle has to be "
            + "unreachable through admission rather than guarded by a flag.");
        Assert.True(
            retained.VisibleCount == 2,
            "and the retained unit still reads as the coherent past it is; "
            + "refusing a query is not erasing the value.");
    }

    /// <summary>
    /// The two orders, under real concurrency: an acceptance and a
    /// release racing on one request and one lease leave either a
    /// published open lease or a closed unpublished one, never a
    /// published closed one.
    /// </summary>
    [Fact]
    public void AnAcceptanceRacingItsOwnReleaseNeverPublishesAClosedLease()
    {
        for (var round = 0; round < 200; round++)
        {
            var slot = new CanvasPublicationSlot(CanvasPublication.Seed());
            var request = new CanvasRequestIdentity($"L{round}");
            var closes = 0;
            var lease = new CanvasHandleLease(
                (ulong)round, _ => Interlocked.Increment(ref closes));
            _ = slot.Publish(s => s.WithLoads(s.Loads.Requested(request)));

            using var start = new ManualResetEventSlim(false);
            var accepted = false;
            var releaseOutcome = CanvasLeaseRelease.Transferred;

            var acceptor = new Worker(() =>
            {
                start.Wait(LivenessBudget);
                accepted = CanvasLeaseTransfer.TryAccept(
                    slot, request, lease, Population("a")).Accepted;
            });
            var releaser = new Worker(() =>
            {
                start.Wait(LivenessBudget);
                releaseOutcome = CanvasLeaseTransfer.Release(slot, request, lease);
            });

            acceptor.Start();
            releaser.Start();
            start.Set();
            Assert.True(
                acceptor.Join(LivenessBudget) && releaser.Join(LivenessBudget),
                $"premise: a thread did not finish in round {round}.");

            if (accepted)
            {
                Assert.True(
                    releaseOutcome == CanvasLeaseRelease.Transferred && closes == 0,
                    $"round {round}: the acceptance landed but the release still "
                    + $"reported {releaseOutcome} with {closes} closes — the "
                    + "published lease was closed underneath the publication, "
                    + "which is obligation I1's arrangement exactly.");
                Assert.True(
                    slot.Current.Names(lease) && !lease.IsClosed,
                    $"round {round}: the publication names a closed lease.");
            }
            else
            {
                Assert.True(
                    releaseOutcome == CanvasLeaseRelease.Closed && closes == 1,
                    $"round {round}: the acceptance was refused but the lease was "
                    + $"not closed ({releaseOutcome}, {closes} closes), so an "
                    + "unpublished handle leaked.");
                Assert.True(
                    slot.Current.Lease is null,
                    $"round {round}: a refused acceptance published a lease.");
            }
        }
    }

    /// <summary>
    /// I1's MUTATION: put back the observation the frozen design had —
    /// decide by reading the slot, then close — with the read and the
    /// close no longer inside one gate. The named arrangement returns.
    /// </summary>
    /// <remarks>
    /// Deterministic rather than racing: the barrier lands the
    /// acceptance exactly between the mutant's read and its close,
    /// which is the interleaving the finding describes and which a
    /// probabilistic version would only sometimes produce.
    /// </remarks>
    [Fact]
    public void MutationI1_ObservingInsteadOfPublishingClosesAnAcceptedLease()
    {
        var slot = new CanvasPublicationSlot(CanvasPublication.Seed());
        var request = new CanvasRequestIdentity("L1");
        var closes = 0;
        var lease = new CanvasHandleLease(1, _ => Interlocked.Increment(ref closes));
        _ = slot.Publish(s => s.WithLoads(s.Loads.Requested(request)));

        // FAITHFULNESS premise: with no acceptance in the window, the
        // observing release behaves exactly as production's does.
        Assert.True(
            ObservingRelease(slot, lease, betweenReadAndClose: null)
                == CanvasLeaseRelease.Closed,
            "premise: the mutant must close an unpublished lease just as "
            + "production does, or it is not a faithful copy of the design it is "
            + "testing.");
        Assert.True(closes == 1, "premise: and exactly once.");

        // The arrangement. A second delivery of the same request, with
        // the acceptance landing between the read and the close.
        var slot2 = new CanvasPublicationSlot(CanvasPublication.Seed());
        var request2 = new CanvasRequestIdentity("L2");
        var closes2 = 0;
        var lease2 = new CanvasHandleLease(2, _ => Interlocked.Increment(ref closes2));
        _ = slot2.Publish(s => s.WithLoads(s.Loads.Requested(request2)));

        var acceptedInWindow = false;
        CanvasLeaseRelease outcome = ObservingRelease(
            slot2,
            lease2,
            betweenReadAndClose: () => acceptedInWindow = CanvasLeaseTransfer.TryAccept(
                slot2, request2, lease2, Population("a")).Accepted);

        Assert.True(
            acceptedInWindow,
            "premise: the acceptance did not land inside the window, so the "
            + "arrangement never established.");
        Assert.True(
            outcome == CanvasLeaseRelease.Closed && closes2 == 1,
            $"the named arrangement did not return: the observing release reported "
            + $"{outcome} with {closes2} closes, and it was supposed to close on "
            + "the strength of a read the acceptance had already invalidated.");
        Assert.True(
            slot2.Current.Names(lease2) && lease2.IsClosed,
            "and the live publication was supposed to end up naming a CLOSED "
            + "handle, which is obligation I1's sentence. Production cannot reach "
            + "this state because its decision and its publication are the same "
            + "critical section.");
    }

    /// <summary>
    /// The frozen design's release: a slot read, then a close, with no
    /// publication between them to make the acceptance impossible.
    /// </summary>
    private static CanvasLeaseRelease ObservingRelease(
        CanvasPublicationSlot slot, CanvasHandleLease lease, Action? betweenReadAndClose)
    {
        // "close unless a final slot read names my lease" — an
        // observation, which is precisely what round 7 said it was.
        bool named = slot.Current.Names(lease);
        betweenReadAndClose?.Invoke();
        if (named)
        {
            return CanvasLeaseRelease.Transferred;
        }

        _ = lease.Close();
        return CanvasLeaseRelease.Closed;
    }

    /// <summary>A reload closes the lease it displaced, exactly once,
    /// from the decision snapshot — the only place that lease is still
    /// reachable once the swap has un-named it.</summary>
    [Fact]
    public void AReloadClosesTheLeaseItDisplaced()
    {
        var slot = new CanvasPublicationSlot(CanvasPublication.Seed());
        var first = new CanvasRequestIdentity("L1");
        var second = new CanvasRequestIdentity("L2");
        var firstCloses = 0;
        var secondCloses = 0;
        var firstLease = new CanvasHandleLease(1, _ => Interlocked.Increment(ref firstCloses));
        var secondLease = new CanvasHandleLease(2, _ => Interlocked.Increment(ref secondCloses));

        _ = slot.Publish(s => s.WithLoads(s.Loads.Requested(first)));
        Assert.True(
            Deliver(slot, first, firstLease, Population("a")),
            "premise: the first load must land, or there is nothing to displace.");
        Assert.True(
            firstCloses == 0 && slot.Current.Names(firstLease),
            "premise: and its lease is live and open.");

        _ = slot.Publish(s => s.WithLoads(s.Loads.Requested(second)));
        Assert.True(
            Deliver(slot, second, secondLease, Population("b")),
            "premise: the reload must land.");

        Assert.True(
            slot.Current.Names(secondLease) && secondCloses == 0,
            "the reload's lease is the live one, and open.");
        Assert.True(
            firstCloses == 1 && firstLease.IsClosed,
            $"the displaced lease closed {firstCloses} times. Nothing names it "
            + "and nothing else can reach it, so a reload that does not close it "
            + "leaks one native handle per reload.");

        // A late release of the displaced lease finds it already closed
        // and says so.
        Assert.True(
            CanvasLeaseTransfer.Release(slot, first, firstLease)
                == CanvasLeaseRelease.AlreadyReleased,
            "a late release of the displaced lease re-closed or re-published it.");
        Assert.True(firstCloses == 1, $"and the handle still closed {firstCloses} times.");
    }

    /// <summary>A release arriving after the TERMINAL publication
    /// closes its lease and publishes nothing: acceptance was already
    /// impossible, and the terminal record has to stay the last
    /// one.</summary>
    [Fact]
    public void AReleaseAfterTheTerminalPublicationClosesAndPublishesNothing()
    {
        var closes = 0;
        var observer = new CanvasPublicationInstallObserver();
        var slot = new CanvasPublicationSlot(CanvasPublication.Seed(), observer);
        var request = new CanvasRequestIdentity("L1");
        var lease = new CanvasHandleLease(1, _ => Interlocked.Increment(ref closes));

        _ = slot.Publish(s => s.WithLoads(s.Loads.Requested(request)));
        _ = slot.Publish(s => s.WithTerminal());
        CanvasPublication terminal = slot.Current;
        int installsBefore = observer.Installs;

        Assert.False(
            CanvasLeaseTransfer.TryAccept(slot, request, lease, Population("a")).Accepted,
            "premise: an acceptance after the terminal publication must be refused.");

        CanvasLeaseRelease outcome = CanvasLeaseTransfer.Release(slot, request, lease);

        Assert.True(
            outcome == CanvasLeaseRelease.Closed && closes == 1,
            $"the release reported {outcome} with {closes} closes; the lease is the "
            + "caller's and nobody else will close it.");
        Assert.True(
            ReferenceEquals(slot.Current, terminal) && observer.Installs == installsBefore,
            "a release published OVER the terminal publication. Every holder "
            + "treats that record as the last one, and a later, different "
            + "reference in the slot says it was not.");
    }

    /// <summary>Acceptance reseeds the filter machine from the CARRIED
    /// needle: both schedule entries retired, a fresh request seeded,
    /// the unit pending on it — the frozen "a reload cannot strand the
    /// filter machine" row, with the request T6 must start read from
    /// the outcome rather than from a second look at the slot.</summary>
    [Fact]
    public void AnAcceptanceReseedsTheFilterMachineFromTheCarriedNeedle()
    {
        var slot = new CanvasPublicationSlot(CanvasPublication.Seed());
        var request = new CanvasRequestIdentity("L1");
        var running = new CanvasRequestIdentity("F1");
        var queued = new CanvasRequestIdentity("F2");
        var lease = new CanvasHandleLease(1, _ => { });

        // A needle typed, one match running and another queued behind
        // it, when the load lands.
        _ = slot.Publish(s => s.WithNeedleIntent("q"));
        _ = slot.Publish(s => s.WithFilters(s.Filters.Typed(running).Typed(queued)));
        _ = slot.Publish(s => s.WithLoads(s.Loads.Requested(request)));

        CanvasLoadAcceptance acceptance =
            CanvasLeaseTransfer.TryAccept(slot, request, lease, Population("a", "b"));

        Assert.True(acceptance.Accepted, "premise: the load must land.");
        CanvasPublication published = slot.Current;
        Assert.True(
            acceptance.Reseeded is not null
                && ReferenceEquals(published.Filters.Running, acceptance.Reseeded)
                && published.Filters.Queued is null
                && !ReferenceEquals(published.Filters.Running, running),
            "the dead machine survived the reload: a running request whose job "
            + "can never publish against this population occupies the slot, and "
            + "every later keystroke queues behind it.");
        Assert.True(
            published.Unit!.Answer == CanvasAnswerState.Pending
                && ReferenceEquals(published.Unit.Request, acceptance.Reseeded)
                && published.Unit.Needle == "q"
                && published.Unit.VisibleCount == 2,
            "the unit is pending on the reseeded request, over the new rows, "
            + "under the carried needle.");
        Assert.True(
            published.NeedleIntent == "q",
            "and the needle INTENT is carried, not reset — it is document-class.");

        // With no needle there is nothing to seed: idle, and unfiltered.
        var slot2 = new CanvasPublicationSlot(CanvasPublication.Seed());
        var request2 = new CanvasRequestIdentity("L2");
        _ = slot2.Publish(s => s.WithFilters(s.Filters.Typed(running)));
        _ = slot2.Publish(s => s.WithLoads(s.Loads.Requested(request2)));
        CanvasLoadAcceptance quiet = CanvasLeaseTransfer.TryAccept(
            slot2, request2, new CanvasHandleLease(2, _ => { }), Population("a"));
        Assert.True(
            quiet.Accepted
                && quiet.Reseeded is null
                && slot2.Current.Filters.Running is null
                && slot2.Current.Unit!.Answer == CanvasAnswerState.Unfiltered,
            "an empty needle reseeds to idle, and the unit is unfiltered.");
    }

    /// <summary>A close whose delegate throws is issued once and not
    /// retried: the record reports the attempt and the fault reaches
    /// the caller. Re-arming would hand the next caller a second close
    /// of a handle whose first may have half-completed.</summary>
    [Fact]
    public void AThrowingCloseIsIssuedOnceAndNotRetried()
    {
        var attempts = 0;
        var lease = new CanvasHandleLease(9, handle =>
        {
            _ = Interlocked.Increment(ref attempts);
            throw new InvalidOperationException("the session is gone");
        });

        InvalidOperationException fault =
            Assert.Throws<InvalidOperationException>(() => { _ = lease.Close(); });
        Assert.True(
            fault.Message == "the session is gone",
            "the delegate's own fault reaches the caller, unwrapped.");
        Assert.True(lease.IsClosed, "the one close call was issued, and the record says so.");

        Assert.False(lease.Close(), "a second close after a faulting first re-issued it.");
        Assert.True(attempts == 1, $"the delegate ran {attempts} times.");
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    /// <summary>
    /// A background thread whose fault reaches the fact instead of the
    /// test host: an assertion failing on a raw thread aborts the whole
    /// run. The suite's capture-and-rethrow convention, applied here.
    /// </summary>
    private sealed class Worker
    {
        private readonly Thread _thread;
        private ExceptionDispatchInfo? _fault;

        internal Worker(Action body)
        {
            _thread = new Thread(() =>
            {
                try
                {
                    body();
                }
                catch (Exception fault)
                {
                    _fault = ExceptionDispatchInfo.Capture(fault);
                }
            })
            { IsBackground = true };
        }

        /// <summary>Blocked in a wait, sleep or join — a contended lock
        /// reports as one — or already finished.</summary>
        internal bool IsBlockedOrFinished =>
            (_thread.ThreadState & (System.Threading.ThreadState.WaitSleepJoin | System.Threading.ThreadState.Stopped)) != 0;

        internal void Start() => _thread.Start();

        /// <summary>Join, then rethrow whatever the body threw.</summary>
        internal bool Join(TimeSpan budget)
        {
            bool finished = _thread.Join(budget);
            _fault?.Throw();
            return finished;
        }
    }

    /// <summary>One delivery: accept, and release in a finally whatever
    /// happened — the shape task T3 will build the real pipeline
    /// around.</summary>
    private static bool Deliver(
        CanvasPublicationSlot slot,
        CanvasRequestIdentity request,
        CanvasHandleLease lease,
        CanvasPopulation population)
    {
        var accepted = false;
        try
        {
            accepted = CanvasLeaseTransfer.TryAccept(slot, request, lease, population).Accepted;
            return accepted;
        }
        finally
        {
            _ = CanvasLeaseTransfer.Release(slot, request, lease);
        }
    }

    private static CanvasPopulation Population(params string[] nodeIds) => new(
        nodeIds.Select(id => new CanvasOutlineRow(
            id, 0, "text", id, id, [], 1, (uint)nodeIds.Length, 0, null)),
        null,
        null,
        0,
        null);
}
