// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.InteropServices;
using SlateWindows.Canvas;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W6-1 PR C-unit, task T1: the publication, the slot, and the
/// transform algebra — with the batteries that discharge obligations
/// I5-runtime, I2 and I7-construction.
/// </summary>
/// <remarks>
/// <para>
/// The I-row discipline is stricter than an ordinary fact's. A
/// discharge is code PLUS a fact PLUS a mutation that returns the
/// arrangement the finding NAMES — a battery that passes while its
/// mutation fails to return the named arrangement is not a discharge.
/// So each obligation below has its mutation implemented and run,
/// not described in a doc comment.
/// </para>
/// <para>
/// The mutants are faithful reimplementations that differ from
/// production in exactly one way, and they live here rather than
/// behind a production flag so that the shipped slot carries no test
/// seam at all. A mutant that had drifted would be a false green, so
/// each one also passes a FAITHFULNESS premise: with its defect
/// disabled it behaves as production does on the same input.
/// </para>
/// </remarks>
public sealed class CanvasPublicationSlotTests
{
    private static readonly TimeSpan LivenessBudget = TimeSpan.FromSeconds(30);

    // ---------------------------------------------------------------
    // The transform algebra
    // ---------------------------------------------------------------

    /// <summary>
    /// Obligation I5, runtime half: every transform ALLOCATES, and the
    /// hard case is setting a field to the value it already holds.
    /// </summary>
    /// <remarks>
    /// A "return this when nothing changed" optimisation is the
    /// identity-return path codex round 7 named, and it is the cheapest
    /// way for a publication reference to come round again. Each arm
    /// below asserts the value did not move — the premise, without
    /// which the arm would pass for the wrong reason — and then that
    /// the reference did.
    /// </remarks>
    [Fact]
    public void EveryTransformAllocatesEvenWhenTheValueDoesNotChange()
    {
        CanvasPublication seed = CanvasPublication.Seed();

        AssertFreshAndUnchanged(
            seed, seed.WithLoadState(seed.LoadState, seed.LoadMessage),
            p => p.LoadState == seed.LoadState && p.LoadMessage == seed.LoadMessage,
            "load state");
        AssertFreshAndUnchanged(
            seed, seed.WithActiveSurface(seed.ActiveSurface),
            p => p.ActiveSurface == seed.ActiveSurface, "active surface");
        AssertFreshAndUnchanged(
            seed, seed.WithSelectedIntent(seed.SelectedIntent),
            p => p.SelectedIntent == seed.SelectedIntent, "selected intent");
        AssertFreshAndUnchanged(
            seed, seed.WithMarkedIntent(seed.MarkedIntent),
            p => p.MarkedIntent.SetEquals(seed.MarkedIntent), "marked intent");
        AssertFreshAndUnchanged(
            seed, seed.WithNeedleIntent(seed.NeedleIntent),
            p => p.NeedleIntent == seed.NeedleIntent, "needle intent");
        AssertFreshAndUnchanged(
            seed, seed.WithLoads(seed.Loads),
            p => ReferenceEquals(p.Loads, seed.Loads), "load schedule");
        AssertFreshAndUnchanged(
            seed, seed.WithFilters(seed.Filters),
            p => ReferenceEquals(p.Filters, seed.Filters), "filter schedule");

        // Retirement has no unchanged case to set — it is one-way — so
        // its freshness is asserted from the retired state instead.
        CanvasPublication retired = seed.WithRetired();
        AssertFreshAndUnchanged(
            retired, retired.WithRetired(), p => p.Retired, "retirement");

        // The three finer-class transforms task T2 added. Same hard
        // case: reinstalling the very references already there. A
        // content memo keyed on the triple is the cheapest identity
        // return these could grow, and it is exactly what I5 names.
        var lease = new CanvasHandleLease(1, _ => { });
        CanvasPopulation population = CanvasPopulation.Empty();
        CanvasProjectionUnit unit = CanvasProjectionUnit.Unfiltered(population);
        CanvasPublication loaded = seed.WithLoaded(lease, population, unit);
        AssertFreshAndUnchanged(
            loaded, loaded.WithLoaded(lease, population, unit),
            p => ReferenceEquals(p.Lease, lease)
                && ReferenceEquals(p.Population, population)
                && ReferenceEquals(p.Unit, unit),
            "loaded");
        AssertFreshAndUnchanged(
            loaded, loaded.WithUnit(unit),
            p => ReferenceEquals(p.Unit, unit), "unit");

        // Terminal is one-way, like retirement, and asserted the same
        // way: from the terminal state, terminalising again.
        CanvasPublication terminal = loaded.WithTerminal();
        AssertFreshAndUnchanged(
            terminal, terminal.WithTerminal(),
            p => p.Retired && p.Lease is null && p.Population is null && p.Unit is null,
            "terminal");
    }

    private static void AssertFreshAndUnchanged(
        CanvasPublication before,
        CanvasPublication after,
        Func<CanvasPublication, bool> unchanged,
        string what)
    {
        Assert.True(
            unchanged(after),
            $"premise: the {what} transform was supposed to set the value already "
            + "there, so a freshness claim about it would be about the wrong "
            + "arrangement.");
        Assert.False(
            ReferenceEquals(before, after),
            $"the {what} transform returned its own input when nothing changed, "
            + "which is obligation I5's identity-return path: a publication "
            + "reference that can come round again lets a stale compare-and-swap "
            + "succeed against a value it should no longer recognise.");
    }

    /// <summary>The schedules are values, and their transitions are the
    /// pure functions the design's tables describe. Task T6 drives the
    /// filter machine; T1 owns that its arithmetic is right.</summary>
    [Fact]
    public void ScheduleTransitionsAreThePureFunctionsTheTablesDescribe()
    {
        var r1 = new CanvasRequestIdentity("R1");
        var r2 = new CanvasRequestIdentity("R2");
        var r3 = new CanvasRequestIdentity("R3");

        CanvasFilterSchedule idle = CanvasFilterSchedule.Idle;
        Assert.True(
            idle.Running is null && idle.Queued is null,
            "premise: an idle schedule holds neither, or the transitions below "
            + "start from a state the table does not describe.");

        CanvasFilterSchedule running = idle.Typed(r1);
        Assert.True(
            ReferenceEquals(running.Running, r1) && running.Queued is null,
            "a keystroke with nothing running starts it.");

        CanvasFilterSchedule queued = running.Typed(r2);
        Assert.True(
            ReferenceEquals(queued.Running, r1) && ReferenceEquals(queued.Queued, r2),
            "a keystroke while R1 runs queues rather than starting.");

        CanvasFilterSchedule replaced = queued.Typed(r3);
        Assert.True(
            ReferenceEquals(replaced.Running, r1) && ReferenceEquals(replaced.Queued, r3),
            "a third keystroke REPLACES the queued request; R2 is dropped before "
            + "it ever started, which is what bounds a burst to two matches.");

        CanvasFilterSchedule promoted = replaced.Finished();
        Assert.True(
            ReferenceEquals(promoted.Running, r3) && promoted.Queued is null,
            "a completion WITH a queue promotes the queued request — and the "
            + "caller discards the finishing answer, because publishing R1's rows "
            + "under R3's needle is the defect this cell exists to prevent.");

        Assert.True(
            promoted.Finished().Running is null,
            "a completion with no queue goes idle.");

        Assert.True(
            CanvasFilterSchedule.Idle.Finished().Running is null
                && CanvasFilterSchedule.Idle.Finished().Queued is null,
            "a completion arriving with nothing running cannot invent a running "
            + "request — the value-level half of the machine's non-running "
            + "completion cell, whose refusal-and-publish-nothing behaviour is "
            + "task T6's.");

        Assert.True(
            replaced.Reseeded(null).Running is null
                && replaced.Reseeded(null).Queued is null,
            "a reload with an empty rebased needle retires BOTH entries; a "
            + "preserved schedule is what left a dead running request occupying "
            + "the slot with no callback able to clear it.");
        Assert.True(
            ReferenceEquals(replaced.Reseeded(r2).Running, r2)
                && replaced.Reseeded(r2).Queued is null,
            "a reload with a needle seeds a NEW request against the new "
            + "population, never the dead machine's.");
    }

    /// <summary>
    /// The load schedule's arithmetic — the state that makes the
    /// publication itself the one-shot latch.
    /// </summary>
    /// <remarks>
    /// It shipped in T1 with no fact of its own, which left the value
    /// underneath task T3's refusal contract unasserted. The refusal is
    /// T3's behaviour; these are the numbers it will rest on.
    /// </remarks>
    [Fact]
    public void TheLoadScheduleRecordsLatestAndConsumedSeparately()
    {
        var l1 = new CanvasRequestIdentity("L1");
        var l2 = new CanvasRequestIdentity("L2");

        CanvasLoadSchedule idle = CanvasLoadSchedule.Idle;
        Assert.True(
            idle.Latest is null && !idle.Consumed,
            "premise: a document that has never requested a load has no latest "
            + "request and nothing consumed.");

        CanvasLoadSchedule requested = idle.Requested(l1);
        Assert.True(
            ReferenceEquals(requested.Latest, l1) && !requested.Consumed,
            "a requested load is latest and PENDING — pending is what a delivery "
            + "checks, so a request that arrived already consumed could never be "
            + "accepted.");

        CanvasLoadSchedule consumed = requested.ConsumedBy(l1);
        Assert.True(
            ReferenceEquals(consumed.Latest, l1) && consumed.Consumed,
            "acceptance marks the SAME request consumed rather than clearing it; "
            + "clearing would leave a repeat delivery reading a schedule that no "
            + "longer mentions the request it is delivering.");

        CanvasLoadSchedule superseded = consumed.Requested(l2);
        Assert.True(
            ReferenceEquals(superseded.Latest, l2) && !superseded.Consumed,
            "a newer request supersedes and is pending again; the consumed marker "
            + "belongs to the request that earned it, not to the schedule.");

        CanvasLoadSchedule released = requested.ReleasedBy(l1);
        Assert.True(
            ReferenceEquals(released.Latest, l1)
                && released.Delivery == CanvasLoadDelivery.Released
                && !released.Consumed
                && !released.Admits(l1),
            "a released request is still latest — nothing newer arrived — but it "
            + "is terminal: not consumed, and not admissible, which is the state "
            + "obligation I1's cleanup publishes to make an acceptance impossible.");
        Assert.True(
            requested.Admits(l1)
                && !consumed.Admits(l1)
                && !superseded.Admits(l1)
                && superseded.Admits(l2),
            "admission is exactly latest-and-pending: a consumed, released or "
            + "superseded request is refused and the newest pending one is not.");

        Assert.True(
            !ReferenceEquals(idle, requested)
                && !ReferenceEquals(requested, consumed)
                && !ReferenceEquals(consumed, superseded)
                && !ReferenceEquals(requested, released),
            "every schedule transition allocates, for the same reason every "
            + "publication transform does.");
        Assert.True(
            idle.Latest is null && !requested.Consumed,
            "and no transition mutated the value it was derived from.");
    }

    /// <summary>
    /// The model's values are compared by REFERENCE and never by value —
    /// the guard for one of the two decisions T1 made.
    /// </summary>
    /// <remarks>
    /// Currency here is derived by comparing against what the slot
    /// holds, and the install is a compare-and-swap: both are reference
    /// operations. Converting any of these three to a record would add
    /// a value equality beside that, so one currency question would
    /// have two answers depending on which operator a caller reached
    /// for. Nothing else in the battery would notice — the freshness
    /// fact uses ReferenceEquals and the observer uses an explicit
    /// reference comparer — so the decision needs its own guard, the
    /// way request identity already has one.
    /// </remarks>
    [Fact]
    public void ModelValuesAreComparedByReferenceAndNeverByValue()
    {
        CanvasPublication p1 = CanvasPublication.Seed();
        CanvasPublication p2 = CanvasPublication.Seed();
        Assert.False(
            ReferenceEquals(p1, p2),
            "premise: two seeds are two objects, or the comparison below is "
            + "trivially true.");
        Assert.False(
            p1.Equals(p2),
            "two publications carrying identical values compared EQUAL, so this "
            + "type has acquired value equality — and every currency question in "
            + "the model now has two answers depending on whether the caller used "
            + "the operator or ReferenceEquals.");

        var request = new CanvasRequestIdentity("L1");
        Assert.False(
            CanvasLoadSchedule.Idle.Requested(request)
                .Equals(CanvasLoadSchedule.Idle.Requested(request)),
            "the load schedule acquired value equality; it is compared by "
            + "reference inside a publication and must stay that way.");
        Assert.False(
            CanvasFilterSchedule.Idle.Typed(request)
                .Equals(CanvasFilterSchedule.Idle.Typed(request)),
            "the filter schedule acquired value equality.");
    }

    /// <summary>Reference identity, and no counter: two requests with the
    /// same label are two requests. A counter is the ABA shape this
    /// branch retired.</summary>
    [Fact]
    public void RequestIdentityIsReferenceIdentity()
    {
        var a = new CanvasRequestIdentity("same");
        var b = new CanvasRequestIdentity("same");
        Assert.False(
            ReferenceEquals(a, b),
            "premise: two separately constructed identities are separate objects.");
        Assert.False(
            a.Equals(b),
            "two requests carrying the same label are two requests; equality here "
            + "would reintroduce the ABA shape a generation counter carried.");
    }

    // ---------------------------------------------------------------
    // Obligation I5, runtime half: no publication is installed twice
    // ---------------------------------------------------------------

    /// <summary>The slot refuses a transform that hands back its own
    /// snapshot, which is the narrow half of freshness it can see from
    /// where it stands.</summary>
    [Fact]
    public void TheSlotRefusesATransformThatReturnsItsOwnSnapshot()
    {
        var slot = new CanvasPublicationSlot(CanvasPublication.Seed());
        CanvasPublication before = slot.Current;

        CanvasPublicationRefusedException error =
            Assert.Throws<CanvasPublicationRefusedException>(
                () => slot.Publish(snapshot => snapshot));

        Assert.True(
            error.Reason == CanvasPublicationRefusal.IdentityReturn,
            $"the slot refused for {error.Reason} rather than the identity return; "
            + "the reason is asserted rather than the sentence so that rewording a "
            + "message cannot silently weaken this fact.");
        Assert.True(
            ReferenceEquals(slot.Current, before),
            "a refused publication must leave the slot exactly where it was.");
    }

    /// <summary>A transform with nothing to say DECLINES, and a decline
    /// is not an install — the outcome says so without the caller
    /// re-reading the slot to find out.</summary>
    [Fact]
    public void ADecliningTransformInstallsNothingAndSaysSo()
    {
        var observer = new CanvasPublicationInstallObserver();
        var slot = new CanvasPublicationSlot(CanvasPublication.Seed(), observer);
        CanvasPublication before = slot.Current;

        CanvasPublicationOutcome outcome = slot.Publish(_ => null);

        Assert.False(outcome.Installed, "a decline did not install.");
        Assert.True(
            ReferenceEquals(outcome.Predecessor, before)
                && ReferenceEquals(outcome.Successor, before),
            "a decline reports the snapshot on both sides, so a caller reading the "
            + "outcome never has to ask the slot what happened.");
        Assert.True(
            observer.Installs == 0,
            $"a decline installed {observer.Installs} publications; a transform "
            + "that declines has published nothing, so the observer must not have "
            + "seen a reference.");
    }

    /// <summary>
    /// I5's runtime discharge: across a long run of every transform, no
    /// publication reference is ever installed twice.
    /// </summary>
    [Fact]
    public void NoPublicationReferenceIsEverInstalledTwice()
    {
        var observer = new CanvasPublicationInstallObserver();
        var slot = new CanvasPublicationSlot(CanvasPublication.Seed(), observer);

        // Deliberately cycles values — a needle of "a", then "b", then
        // "a" again — because a content-keyed cache is exactly what
        // would hand back a record installed earlier.
        // ALL ELEVEN transforms, not the four the value cycling needs.
        // A memoising defect in an undriven transform would fail nothing:
        // the per-transform fact above compares a result only against its
        // own input, so a previously-built record passes it, and only the
        // observer can see that record arriving twice.
        var needles = new[] { "a", "b", "a", string.Empty, "b", "a" };
        // The schedule VALUES are hoisted and cycled, not rebuilt each
        // round. Rebuilding them hands every transform a key it has
        // never seen, so a content-keyed memo inside the transform would
        // never hit and this run would report freshness it did not test
        // — which is the same "cycle the content" reasoning the needles
        // above are here for, and it is easy to lose on a value whose
        // identity is fresh even when its meaning repeats.
        CanvasLoadSchedule[] loadSchedules =
        [
            CanvasLoadSchedule.Idle.Requested(new CanvasRequestIdentity("L-a")),
            CanvasLoadSchedule.Idle.Requested(new CanvasRequestIdentity("L-b")),
        ];
        CanvasFilterSchedule[] filterSchedules =
        [
            CanvasFilterSchedule.Idle.Typed(new CanvasRequestIdentity("F-a")),
            CanvasFilterSchedule.Idle.Typed(new CanvasRequestIdentity("F-b")),
        ];
        // The finer classes, hoisted for the same reason: two leases,
        // two populations, two units, cycled, so that a memo keyed on
        // the triple — or on the unit alone — would hit.
        CanvasHandleLease[] leases =
        [
            new CanvasHandleLease(1, _ => { }),
            new CanvasHandleLease(2, _ => { }),
        ];
        CanvasPopulation[] populations = [CanvasPopulation.Empty(), CanvasPopulation.Empty()];
        CanvasProjectionUnit[] units =
        [
            CanvasProjectionUnit.Unfiltered(populations[0]),
            CanvasProjectionUnit.Unfiltered(populations[1]),
        ];

        for (int round = 0; round < 200; round++)
        {
            string needle = needles[round % needles.Length];
            _ = slot.Publish(s => s.WithNeedleIntent(needle));
            _ = slot.Publish(s => s.WithActiveSurface(
                round % 2 == 0 ? CanvasSurfaceKind.Outline : CanvasSurfaceKind.Table));
            _ = slot.Publish(s => s.WithSelectedIntent(round % 3 == 0 ? "n1" : null));
            _ = slot.Publish(s => s.WithMarkedIntent(round % 2 == 0 ? ["n1"] : []));
            _ = slot.Publish(s => s.WithRetired());
            _ = slot.Publish(s => s.WithLoadState(
                round % 2 == 0 ? CanvasLoadState.Loading : CanvasLoadState.Ready,
                round % 2 == 0 ? null : "ready"));
            _ = slot.Publish(s => s.WithLoads(loadSchedules[round % 2]));
            _ = slot.Publish(s => s.WithFilters(filterSchedules[round % 2]));
            _ = slot.Publish(s => s.WithLoaded(
                leases[round % 2], populations[round % 2], units[round % 2]));
            _ = slot.Publish(s => s.WithUnit(units[round % 2]));
            _ = slot.Publish(s => s.WithTerminal());
        }

        Assert.True(
            observer.Installs == 2_200,
            $"premise: the run was supposed to install 2200 publications and "
            + $"installed {observer.Installs}, so a freshness claim over it would "
            + "be measuring a shorter run than it says.");
        Assert.True(
            observer.RepeatedInstalls == 0,
            $"{observer.RepeatedInstalls} publication references were installed "
            + "more than once. That is obligation I5's arrangement: a reference "
            + "that comes round again lets a stale compare-and-swap expecting the "
            + "earlier value succeed against the later one.");
    }

    /// <summary>
    /// I5's MUTATION: memoise successors by content and the named
    /// arrangement returns — E to B to E, with a stale swap expecting E
    /// succeeding after the slot has moved on.
    /// </summary>
    /// <remarks>
    /// The cache lives in the mutant because production's transform
    /// algebra cannot express it: every transform allocates and the
    /// slot refuses an identity return. That unrepresentability IS the
    /// discharge — this mutation is what proves the alternative would
    /// have been observable rather than theoretical.
    /// </remarks>
    [Fact]
    public void MutationI5_AMemoisingAlgebraLetsAStaleSwapSucceed()
    {
        var observer = new CanvasPublicationInstallObserver();
        var mutant = new MemoisingSlotMutant(CanvasPublication.Seed(), observer);

        CanvasPublication e = mutant.Current;

        // FAITHFULNESS premise: with distinct content the mutant behaves
        // as production does — fresh records, no repeats.
        _ = mutant.Publish(s => s.WithNeedleIntent("one"));
        _ = mutant.Publish(s => s.WithNeedleIntent("two"));
        Assert.True(
            observer.RepeatedInstalls == 0,
            "premise: the mutant must match production until its defect is "
            + "exercised, or it is not a faithful copy and its failure below "
            + "would prove nothing about the design.");

        // E to B to E: the third publication asks for content the cache
        // has already produced, so the reference comes round again.
        CanvasPublication b = mutant.Publish(_ => e.WithNeedleIntent("B")).Successor;
        CanvasPublication eAgain = mutant.Publish(_ => e.WithNeedleIntent("E")).Successor;
        CanvasPublication andBackToB = mutant.Publish(_ => e.WithNeedleIntent("B")).Successor;

        Assert.True(
            ReferenceEquals(b, andBackToB),
            "premise: the memoising algebra was supposed to hand back the record "
            + "it built earlier, and did not, so the arrangement below never "
            + "established.");
        Assert.True(
            observer.RepeatedInstalls > 0,
            "premise: the observer was supposed to see the repeat.");

        // A publisher that read B before the slot moved to E and back
        // now swaps successfully against a slot it should no longer
        // recognise.
        Assert.True(
            ReferenceEquals(mutant.Current, andBackToB),
            "premise: the slot holds the re-installed reference.");
        bool staleSwapSucceeded = mutant.SwapExpecting(b, eAgain.WithNeedleIntent("late"));
        Assert.True(
            staleSwapSucceeded,
            "the named arrangement did not return: a stale swap expecting B was "
            + "supposed to succeed after the slot went B, E, B — which is "
            + "obligation I5's interleaving, and the reason a publication "
            + "reference is never installed twice.");
    }

    // ---------------------------------------------------------------
    // Obligation I2: publication terminates under sustained contention
    // ---------------------------------------------------------------

    /// <summary>
    /// I2's discharge, order one: contenders are already running when
    /// the victim starts.
    /// </summary>
    [Fact]
    public void PublicationTerminatesWhenContendersAreAlreadyRunning() =>
        AssertVictimCompletesUnderContention(contendersFirst: true, retiring: false);

    /// <summary>I2's discharge, order two: the victim starts first and
    /// the contenders arrive on top of it.</summary>
    [Fact]
    public void PublicationTerminatesWhenContendersArriveAfterTheVictim() =>
        AssertVictimCompletesUnderContention(contendersFirst: false, retiring: false);

    /// <summary>
    /// I2's second half, named separately in the finding: teardown's
    /// unconditional retirement must not starve before it installs the
    /// absorbing state.
    /// </summary>
    [Fact]
    public void RetirementTerminatesUnderSustainedContention() =>
        AssertVictimCompletesUnderContention(contendersFirst: true, retiring: true);

    private static void AssertVictimCompletesUnderContention(
        bool contendersFirst, bool retiring)
    {
        var slot = new CanvasPublicationSlot(CanvasPublication.Seed());
        using var stop = new CancellationTokenSource();
        var contenderPublications = 0;

        Thread[] contenders = [.. Enumerable.Range(0, 4).Select(index => new Thread(() =>
        {
            var local = 0;
            while (!stop.IsCancellationRequested)
            {
                // The finding's own contenders: keystrokes, selections,
                // and other intents winning successive swaps.
                _ = slot.Publish(s => s.WithNeedleIntent($"k{index}-{local}"));
                _ = slot.Publish(s => s.WithSelectedIntent($"n{local}"));
                local++;
                _ = Interlocked.Increment(ref contenderPublications);
            }
        })
        { IsBackground = true })];

        void StartContenders()
        {
            foreach (Thread contender in contenders)
            {
                contender.Start();
            }
        }

        var victimCompleted = 0;
        var victimDecisions = 0;
        var victimInstalls = 0;

        // The overlap PROOF, and it is the victim's own observation
        // rather than a counter sampled after the join. A publication
        // whose predecessor is not the successor this victim received
        // last time is a foreign publication that landed in between.
        var foreignPublicationsObserved = 0;

        var stopwatch = Stopwatch.StartNew();
        var victim = new Thread(() =>
        {
            CanvasPublication? previousSuccessor = null;
            var i = 0;

            // Runs until BOTH legs hold: two hundred publications, and
            // at least one observed foreign publication. Looping on the
            // second leg is what stops the after-the-victim arm passing
            // on a run where the contenders never got a window —
            // establishing the arrangement rather than hoping for it.
            while ((i < 200 || Volatile.Read(ref foreignPublicationsObserved) == 0)
                && stopwatch.Elapsed < LivenessBudget)
            {
                CanvasPublicationOutcome outcome = retiring
                    ? slot.Publish(s =>
                    {
                        _ = Interlocked.Increment(ref victimDecisions);

                        // UNCONDITIONAL, which is the word the finding
                        // uses for teardown's retirement — so every call
                        // installs and the decision count below has real
                        // discriminating power rather than counting 199
                        // declines.
                        return s.WithRetired();
                    })
                    : slot.Publish(s =>
                    {
                        _ = Interlocked.Increment(ref victimDecisions);
                        return s.WithMarkedIntent([$"m{i}"]);
                    });

                if (outcome.Installed)
                {
                    _ = Interlocked.Increment(ref victimInstalls);
                }

                if (previousSuccessor is not null
                    && !ReferenceEquals(outcome.Predecessor, previousSuccessor))
                {
                    _ = Interlocked.Increment(ref foreignPublicationsObserved);
                }

                previousSuccessor = outcome.Successor;
                i++;
            }

            Volatile.Write(ref victimCompleted, i);
        })
        { IsBackground = true };

        if (contendersFirst)
        {
            StartContenders();
            SpinWait.SpinUntil(
                () => Volatile.Read(ref contenderPublications) > 50, LivenessBudget);
            victim.Start();
        }
        else
        {
            victim.Start();
            StartContenders();
        }

        bool finished = victim.Join(LivenessBudget);
        stop.Cancel();
        foreach (Thread contender in contenders)
        {
            _ = contender.Join(LivenessBudget);
        }
        stopwatch.Stop();

        // The premise is the victim's OWN observation, so it cannot be
        // satisfied by contention that happened after the victim
        // finished. Asserted before the liveness verdict, because a
        // liveness claim over an uncontended run says nothing.
        Assert.True(
            Volatile.Read(ref foreignPublicationsObserved) > 0,
            $"premise: the victim never observed a foreign publication in "
            + $"{Volatile.Read(ref victimCompleted)} attempts, so this measured an "
            + "UNCONTENDED slot whatever the contender counter says, and a "
            + "termination claim over it would be about the wrong arrangement.");
        Assert.True(
            Volatile.Read(ref contenderPublications) > 0,
            "premise: no contender ever published.");
        Assert.True(
            finished,
            $"a publisher did not terminate under sustained contention: "
            + $"{Volatile.Read(ref victimCompleted)} of 200 publications completed "
            + $"in {stopwatch.Elapsed}. That is obligation I2's arrangement — a "
            + "publisher losing to other intents indefinitely, re-deciding each "
            + "time and never reaching a terminal answer"
            + (retiring
                ? ", here for teardown's unconditional retirement, which starves "
                + "before it can install the absorbing retired state."
                : "."));

        int completed = Volatile.Read(ref victimCompleted);
        Assert.True(
            completed >= 200,
            $"premise: the victim completed {completed} publications rather than "
            + "the 200 the arrangement calls for.");

        // The teeth. "It finished" is weak evidence — a retry loop under
        // real contention finishes too, usually, which is exactly why
        // obligation I2 is about what is GUARANTEED. The structural
        // property is that no publisher ever RE-DECIDES: the decision and
        // the install are one critical section, so a transform runs once
        // per call however many writers are competing. Under the frozen
        // design's optimistic loop this count would exceed the call count
        // whenever a swap lost, which with four contenders is not a rare
        // event.
        Assert.True(
            Volatile.Read(ref victimDecisions) == completed,
            $"the victim decided {Volatile.Read(ref victimDecisions)} times for "
            + $"{completed} publications, so it re-decided at least once. That is "
            + "obligation I2's cost model — every loss re-runs the full transform "
            + "— and the gate exists so the count and the call count are the same "
            + "number.");

        // And the teeth only bite if every call INSTALLED. A decline
        // never retries under the optimistic loop either, so an arm made
        // mostly of declines would report the same number for the wrong
        // reason — which is why the retiring victim is unconditional.
        Assert.True(
            Volatile.Read(ref victimInstalls) == completed,
            $"only {Volatile.Read(ref victimInstalls)} of {completed} victim "
            + "publications installed; a declining attempt does not exercise the "
            + "loss path the decision count is supposed to discriminate on.");

        if (retiring)
        {
            Assert.True(
                slot.Current.Retired,
                "the absorbing retired state was never installed, which is exactly "
                + "what obligation I2 says starvation would look like for "
                + "teardown.");
        }
    }

    /// <summary>
    /// I2's MUTATION: restore the frozen design's optimistic retry loop
    /// and the named arrangement returns — the publisher re-decides
    /// forever, each loss costing another full transform, and no loss
    /// producing a terminal refusal.
    /// </summary>
    /// <remarks>
    /// Deterministic rather than probabilistic: the interleave hook
    /// lands one competing publication between the victim's decision
    /// and its swap, every time. A racing version of the same mutant
    /// would usually let the victim through eventually, which is
    /// precisely why the finding is about what is GUARANTEED rather
    /// than what is likely.
    /// </remarks>
    [Fact]
    public void MutationI2_TheOptimisticRetryLoopNeverTerminates()
    {
        // The victim is a real LOAD DELIVERY, because the finding's core
        // sentence is that losing "does not make this delivery consumed,
        // superseded, or retired" — and a victim with no request has no
        // consumption state, so that half would be unfalsifiable by
        // construction rather than true.
        var request = new CanvasRequestIdentity("L1");
        CanvasPublication seed = CanvasPublication.Seed()
            .WithLoads(CanvasLoadSchedule.Idle.Requested(request));
        var mutant = new OptimisticSlotMutant(seed);

        Assert.True(
            ReferenceEquals(mutant.Current.Loads.Latest, request)
                && !mutant.Current.Loads.Consumed,
            "premise: the delivery must start live, latest and pending, or the "
            + "assertions below are about a schedule the finding does not "
            + "describe.");

        // FAITHFULNESS premise: uncontended, the mutant publishes
        // exactly as production does, in one attempt.
        Assert.True(
            mutant.TryPublish(s => s.WithNeedleIntent("uncontended"), attemptCap: 8),
            "premise: the mutant must publish normally when nothing competes, or "
            + "it is not a faithful copy of the design it is testing.");
        Assert.True(
            mutant.Attempts == 1,
            $"premise: an uncontended publication took {mutant.Attempts} attempts "
            + "rather than one, so the mutant is not modelling the loop faithfully.");

        var transforms = 0;
        var sawLiveLatestPending = 0;
        var competitor = 0;

        // The finding's own contenders: keystrokes and selections, which
        // move the publication without touching the load schedule.
        mutant.BeforeSwap = () => mutant.CompetingPublish(
            s => s.WithSelectedIntent($"keystroke{Interlocked.Increment(ref competitor)}"));

        mutant.ResetAttempts();
        const int Cap = 64;
        bool published = mutant.TryPublish(
            s =>
            {
                // Each loss costs another full re-decision — the
                // finding's "another full rebase".
                _ = Interlocked.Increment(ref transforms);
                if (!s.Loads.Admits(request))
                {
                    // The only terminal refusal this delivery has. The
                    // finding says no loss ever produces it.
                    return null;
                }

                _ = Interlocked.Increment(ref sawLiveLatestPending);
                return s.WithLoads(s.Loads.ConsumedBy(request));
            },
            attemptCap: Cap);

        Assert.False(
            published,
            "the named arrangement did not return: the optimistic loop was "
            + "supposed to lose every swap to a competing publication and never "
            + "terminate, which is obligation I2.");
        Assert.True(
            mutant.Attempts == Cap,
            $"premise: the loop ran {mutant.Attempts} attempts rather than the "
            + $"{Cap} cap, so it terminated for some other reason.");
        Assert.True(
            transforms == Cap,
            $"each loss must cost a full re-decision; {transforms} transforms ran "
            + $"for {Cap} attempts.");

        // The semantic half of the finding, which is the half a
        // request-free victim cannot express.
        Assert.True(
            sawLiveLatestPending == Cap,
            $"the delivery read a live, latest, pending request on only "
            + $"{sawLiveLatestPending} of {Cap} attempts; the finding's "
            + "arrangement is that it reads one REPEATEDLY, losing every time.");
        Assert.True(
            !mutant.Current.Loads.Consumed,
            "losing sixty-four swaps left the delivery CONSUMED, which would mean "
            + "the loop produced a terminal answer after all.");
        Assert.True(
            ReferenceEquals(mutant.Current.Loads.Latest, request),
            "losing sixty-four swaps left the delivery SUPERSEDED, which would "
            + "mean the loop produced a terminal answer after all.");
        Assert.True(
            !mutant.Current.Retired,
            "losing sixty-four swaps left the document RETIRED, which would mean "
            + "the loop produced a terminal answer after all. Consumed, superseded "
            + "and retired are the three the finding names, and losing produced "
            + "none of them.");
    }

    /// <summary>
    /// A transform that publishes is not a pure function of its
    /// snapshot, and a monitor is reentrant, so nothing but this
    /// refusal would stop the inner publication installing against a
    /// snapshot the outer attempt is still holding.
    /// </summary>
    [Fact]
    public void AReentrantPublicationIsRefused()
    {
        var slot = new CanvasPublicationSlot(CanvasPublication.Seed());
        CanvasPublication before = slot.Current;

        CanvasPublicationRefusedException error =
            Assert.Throws<CanvasPublicationRefusedException>(
                () => slot.Publish(outer =>
                {
                    _ = slot.Publish(inner => inner.WithNeedleIntent("inner"));
                    return outer.WithNeedleIntent("outer");
                }));

        Assert.True(
            error.Reason == CanvasPublicationRefusal.Reentrant,
            $"the slot refused for {error.Reason} rather than reentrancy.");
        Assert.True(
            ReferenceEquals(slot.Current, before),
            "a refused reentrant publication must leave the slot untouched — "
            + "neither the inner nor the outer successor may survive.");
    }

    /// <summary>Free-threaded readers see one publication or the next,
    /// never a mixture, because a publication is a value and the slot
    /// is one reference.</summary>
    [Fact]
    public void ReadersUnderContentionNeverSeeAMixedPublication()
    {
        var slot = new CanvasPublicationSlot(CanvasPublication.Seed());
        using var stop = new CancellationTokenSource();
        var reads = 0;
        var mixed = 0;
        var distinctNeedles = new HashSet<string>(StringComparer.Ordinal);

        var writer = new Thread(() =>
        {
            for (var i = 0; i < 5_000; i++)
            {
                var tag = $"v{i}";
                _ = slot.Publish(s => s
                    .WithNeedleIntent(tag)
                    .WithSelectedIntent(tag)
                    .WithMarkedIntent([tag]));
            }
        })
        { IsBackground = true };

        var reader = new Thread(() =>
        {
            while (!stop.IsCancellationRequested)
            {
                CanvasPublication snapshot = slot.Current;
                _ = Interlocked.Increment(ref reads);
                if (snapshot.NeedleIntent.Length == 0)
                {
                    continue;
                }

                lock (distinctNeedles)
                {
                    _ = distinctNeedles.Add(snapshot.NeedleIntent);
                }
                if (snapshot.SelectedIntent != snapshot.NeedleIntent
                    || !snapshot.MarkedIntent.Contains(snapshot.NeedleIntent))
                {
                    _ = Interlocked.Increment(ref mixed);
                }
            }
        })
        { IsBackground = true };

        reader.Start();
        writer.Start();
        Assert.True(
            writer.Join(LivenessBudget), "premise: the writer did not finish.");
        stop.Cancel();
        _ = reader.Join(LivenessBudget);

        int seen;
        lock (distinctNeedles)
        {
            seen = distinctNeedles.Count;
        }

        Assert.True(
            seen > 1,
            $"premise: the reader saw {seen} distinct publications, so it did not "
            + "OVERLAP the writer — a read count alone can be satisfied entirely "
            + "after the writer finished, which would make the coherence claim "
            + "below about a slot nobody was writing.");
        Assert.True(
            Volatile.Read(ref mixed) == 0,
            $"{Volatile.Read(ref mixed)} reads saw a publication whose three "
            + "fields disagreed, which cannot happen while a publication is one "
            + "immutable value behind one reference.");
    }

    // ---------------------------------------------------------------
    // Obligation I7, construction half: copy, never alias
    // ---------------------------------------------------------------

    /// <summary>
    /// A caller who keeps the collection it handed in cannot move a
    /// published snapshot afterwards.
    /// </summary>
    [Fact]
    public void ACallerRetainedCollectionCannotMoveAPublishedSnapshot()
    {
        var callerRetained = new HashSet<string>(StringComparer.Ordinal) { "n1" };
        var slot = new CanvasPublicationSlot(CanvasPublication.Seed());
        _ = slot.Publish(s => s.WithMarkedIntent(callerRetained));

        CanvasPublication published = slot.Current;
        Assert.True(
            published.MarkedIntent.Count == 1,
            "premise: the publication was supposed to take the one mark it was "
            + "given.");

        _ = callerRetained.Add("n2");
        _ = callerRetained.Remove("n1");

        Assert.True(
            published.MarkedIntent.Count == 1 && published.MarkedIntent.Contains("n1"),
            "the published snapshot moved when the caller mutated the collection it "
            + "had handed in. That is obligation I7's arrangement: an aliased "
            + "backing store makes a snapshot mutate in place, which takes rebase "
            + "repeatability and decision stability with it.");
        Assert.True(
            ReferenceEquals(slot.Current, published),
            "and no publication was installed by the caller's mutation, because a "
            + "mutation is not a publication.");
    }

    /// <summary>The ordered copy has the same property, and it is the
    /// one the population's rows will use in task T2.</summary>
    [Fact]
    public void TheOrderedCopyDoesNotAliasItsSource()
    {
        var source = new List<string> { "a", "b" };
        ImmutableArray<string> copied = CanvasModelCopy.Ordered(source);
        Assert.True(
            copied.Length == 2,
            $"premise: the copy took {copied.Length} of the two elements it was "
            + "given, so the aliasing comparison below has the wrong subject.");

        source[0] = "mutated";
        source.Add("c");

        Assert.True(
            copied.Length == 2 && copied[0] == "a",
            "the copy tracked its source, so it was an alias and not a copy.");
    }

    /// <summary>
    /// I7's MUTATION: build the same immutable value over a
    /// caller-retained array through the marshal, and the named
    /// arrangement returns — a nominally trusted immutable collection
    /// with a live external alias, mutating in place.
    /// </summary>
    /// <remarks>
    /// The mutation is at the construction helper rather than through a
    /// publication, because a publication's constructor is private and
    /// every path into it copies — the aliasing publication is
    /// unrepresentable, which IS the discharge. What this proves is
    /// that the alternative would have been observable rather than
    /// theoretical, and it is the shape task T4's analyzer has to
    /// forbid by name.
    /// </remarks>
    [Fact]
    public void MutationI7_TheMarshalProducesATrustedTypeOverALiveAlias()
    {
        var callerRetained = new[] { "a", "b" };

        // FAITHFULNESS premise: the two paths agree before the alias is
        // exercised, so the difference below is the defect and not the
        // construction.
        ImmutableArray<string> copied = CanvasModelCopy.Ordered(callerRetained);
        ImmutableArray<string> aliased =
            ImmutableCollectionsMarshal.AsImmutableArray(callerRetained);
        Assert.True(
            copied.SequenceEqual(aliased),
            "premise: the copying and aliasing constructions were supposed to "
            + "produce equal values before the source moved.");

        callerRetained[0] = "mutated";

        Assert.True(
            copied[0] == "a",
            "premise: the copying construction must not move, or this comparison "
            + "has nothing to contrast.");
        Assert.True(
            aliased[0] == "mutated",
            "the named arrangement did not return: an immutable array built over "
            + "a caller-retained array was supposed to mutate in place, which is "
            + "obligation I7 — nominal trust in an immutable collection type does "
            + "not establish that its backing storage was copied.");
    }

    // ---------------------------------------------------------------
    // The mutants
    // ---------------------------------------------------------------

    /// <summary>
    /// The frozen design's optimistic retry loop, with no gate: read,
    /// decide, swap, and on failure re-decide. Obligation I2 is that
    /// this does not terminate.
    /// </summary>
    private sealed class OptimisticSlotMutant(CanvasPublication seed)
    {
        private CanvasPublication _current = seed;

        /// <summary>The deterministic interleave: a competing
        /// publication lands here, between the decision and the
        /// swap.</summary>
        internal Action? BeforeSwap { get; set; }

        internal int Attempts { get; private set; }

        internal void ResetAttempts() => Attempts = 0;

        internal CanvasPublication Current => Volatile.Read(ref _current);

        internal bool TryPublish(
            Func<CanvasPublication, CanvasPublication?> transform, int attemptCap)
        {
            for (var attempt = 0; attempt < attemptCap; attempt++)
            {
                Attempts++;
                CanvasPublication snapshot = Volatile.Read(ref _current);
                CanvasPublication? successor = transform(snapshot);
                if (successor is null)
                {
                    return true;
                }

                BeforeSwap?.Invoke();

                if (ReferenceEquals(
                    Interlocked.CompareExchange(ref _current, successor, snapshot),
                    snapshot))
                {
                    return true;
                }
            }

            return false;
        }

        internal void CompetingPublish(
            Func<CanvasPublication, CanvasPublication> transform)
        {
            CanvasPublication snapshot = Volatile.Read(ref _current);
            _ = Interlocked.CompareExchange(
                ref _current, transform(snapshot), snapshot);
        }
    }

    /// <summary>
    /// A slot whose transform algebra memoises successors by content,
    /// which is the cached-record half of obligation I5 that the
    /// identity-return refusal cannot see.
    /// </summary>
    private sealed class MemoisingSlotMutant(
        CanvasPublication seed, CanvasPublicationInstallObserver observer)
    {
        private readonly Dictionary<string, CanvasPublication> _cache =
            new(StringComparer.Ordinal);

        private CanvasPublication _current = seed;

        internal CanvasPublication Current => Volatile.Read(ref _current);

        internal CanvasPublicationOutcome Publish(
            Func<CanvasPublication, CanvasPublication> transform)
        {
            CanvasPublication snapshot = Volatile.Read(ref _current);
            CanvasPublication built = transform(snapshot);

            // The defect: one record per distinct content, reused.
            if (_cache.TryGetValue(built.NeedleIntent, out CanvasPublication? cached))
            {
                built = cached;
            }
            else
            {
                _cache[built.NeedleIntent] = built;
            }

            _ = Interlocked.CompareExchange(ref _current, built, snapshot);
            observer.Installed(built);
            return new CanvasPublicationOutcome(true, snapshot, built);
        }

        /// <summary>A publisher that decided against <paramref
        /// name="expected"/> earlier, swapping now.</summary>
        internal bool SwapExpecting(
            CanvasPublication expected, CanvasPublication successor) =>
            ReferenceEquals(
                Interlocked.CompareExchange(ref _current, successor, expected),
                expected);
    }
}
