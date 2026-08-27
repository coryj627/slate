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

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => slot.Publish(snapshot => snapshot));

        Assert.Contains(
            "returned its own snapshot", error.Message, StringComparison.Ordinal);
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
        Assert.Equal(0, observer.Installs);
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
        var needles = new[] { "a", "b", "a", string.Empty, "b", "a" };
        for (int round = 0; round < 200; round++)
        {
            string needle = needles[round % needles.Length];
            _ = slot.Publish(s => s.WithNeedleIntent(needle));
            _ = slot.Publish(s => s.WithActiveSurface(
                round % 2 == 0 ? CanvasSurfaceKind.Outline : CanvasSurfaceKind.Table));
            _ = slot.Publish(s => s.WithSelectedIntent(round % 3 == 0 ? "n1" : null));
            _ = slot.Publish(s => s.WithMarkedIntent(round % 2 == 0 ? ["n1"] : []));
        }

        Assert.True(
            observer.Installs == 800,
            $"premise: the run was supposed to install 800 publications and "
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
        var stopwatch = Stopwatch.StartNew();
        var victim = new Thread(() =>
        {
            // 200 publications, each of which the frozen design's retry
            // loop would have had to win against four contenders.
            for (var i = 0; i < 200; i++)
            {
                _ = retiring
                    ? slot.Publish(s =>
                    {
                        _ = Interlocked.Increment(ref victimDecisions);
                        return s.Retired ? null : s.WithRetired();
                    })
                    : slot.Publish(s =>
                    {
                        _ = Interlocked.Increment(ref victimDecisions);
                        return s.WithMarkedIntent([$"m{i}"]);
                    });
                _ = Interlocked.Increment(ref victimCompleted);
            }
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

        Assert.True(
            Volatile.Read(ref contenderPublications) > 0,
            "premise: no contender ever published, so this measured an "
            + "uncontended slot and says nothing about obligation I2.");
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
        Assert.Equal(200, Volatile.Read(ref victimCompleted));

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
            Volatile.Read(ref victimDecisions) == 200,
            $"the victim decided {Volatile.Read(ref victimDecisions)} times for 200 "
            + "publications, so it re-decided at least once. That is obligation "
            + "I2's cost model — every loss re-runs the full transform — and the "
            + "gate exists so the count and the call count are the same number.");
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
        var mutant = new OptimisticSlotMutant(CanvasPublication.Seed());

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
        var competitor = 0;
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
                return s.WithMarkedIntent([$"m{transforms}"]);
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

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => slot.Publish(outer =>
            {
                _ = slot.Publish(inner => inner.WithNeedleIntent("inner"));
                return outer.WithNeedleIntent("outer");
            }));

        Assert.Contains("reentrantly", error.Message, StringComparison.Ordinal);
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

        Assert.True(
            Volatile.Read(ref reads) > 0,
            "premise: the reader never ran, so nothing was observed.");
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
        Assert.Equal(2, copied.Length);

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
