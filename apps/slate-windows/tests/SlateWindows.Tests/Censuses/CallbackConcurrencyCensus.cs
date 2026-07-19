// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// §W-E callback concurrency (w0_spec §W0-3 item 2, #715): all three
// foreign traits at once — a progress-listening scan, vault-event-
// generating saves, and command invocations on separate threads against
// one session, with a UI-thread-simulated dispatcher pumping work
// throughout. Plus listener registration/unregistration lifetime under
// fire. Seeded from the W0-1 probe's stress sections.

using SlateWindows.Tests.Support;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "callback-concurrency")]
public class CallbackConcurrencyCensus
{
    [Fact]
    public void AllThreeForeignTraits_ConcurrentlyAgainstOneSession_NoDeadlockNoLoss()
    {
        int notes = CensusTier.Scale(250, 600);
        using var vault = FixtureVault.Create(notes);
        using var session = VaultSession.OpenFilesystem(vault.Root);
        var events = new EventRecorder();
        ulong registration = session.RegisterEventListener(events);
        var progress = new ProgressRecorder();
        using var registry = new CommandRegistry();
        var action = new ScriptedAction(() => { });
        _ = registry.Register(
            new Command("census.spin", "Spin", null, null, CommandSection.File), action);

        // The "UI thread" is a real single-threaded work pump: command
        // invocations execute as posted work items ON that thread while
        // the scan and save storms run on background threads — the
        // UI-thread-simulated load §W-E names, with thread identities
        // recorded so per-trait dispatch affinity is asserted below.
        using var pump = new WorkPump();
        var stop = new CancellationTokenSource();

        Exception? failure = null;
        int scanThreadId = 0;
        using var scanToken = new CancelToken();
        var scanTask = Task.Run(() =>
        {
            Volatile.Write(ref scanThreadId, Environment.CurrentManagedThreadId);
            try
            {
                session.ScanInitialWithProgress(scanToken, progress);
            }
            catch (Exception ex)
            {
                Volatile.Write(ref failure, ex);
            }
        });
        int saveThreadId = 0;
        var saveTask = Task.Run(() =>
        {
            Volatile.Write(ref saveThreadId, Environment.CurrentManagedThreadId);
            try
            {
                int n = 0;
                while (!stop.IsCancellationRequested)
                {
                    session.SaveText($"churn{n % 8}.md", $"save {n}\n", null);
                    n++;
                }
                return n;
            }
            catch (Exception ex)
            {
                Volatile.Write(ref failure, ex);
                return -1;
            }
        });
        var invokeTask = Task.Run(() =>
        {
            try
            {
                int n = 0;
                while (!stop.IsCancellationRequested)
                {
                    // A timed-out pump item is a stuck pump — fatal, never
                    // counted as progress.
                    if (!pump.Post(() => registry.InvokeById("census.spin"))
                            .Wait(TimeSpan.FromSeconds(30)))
                    {
                        throw new TimeoutException("pump work item did not complete within 30s");
                    }
                    n++;
                }
                return n;
            }
            catch (Exception ex)
            {
                Volatile.Write(ref failure, ex);
                return -1;
            }
        });

        bool scanDone = scanTask.Wait(TimeSpan.FromSeconds(180));
        stop.Cancel();
        bool sideDone = Task.WaitAll(new Task[] { saveTask, invokeTask }, TimeSpan.FromSeconds(30));

        Assert.True(scanDone && sideDone, "storm threads did not terminate (deadlock?)");
        Assert.Null(Volatile.Read(ref failure));

        // Full progress choreography must survive the load, not merely
        // "some events": exactly one Started, a monotonic 1..N FileIndexed
        // counter, exactly one terminal Finished, no spurious terminal.
        // (Saves create churn*.md mid-walk so N may exceed the fixture
        // count.)
        var progressEvents = progress.Snapshot();
        Assert.Equal(1, progressEvents.Count(e => e is ScanProgress.Started));
        Assert.Equal(1, progressEvents.Count(e => e is ScanProgress.Finished));
        Assert.IsType<ScanProgress.Finished>(progressEvents[^1]);
        Assert.DoesNotContain(progressEvents, e => e is ScanProgress.Cancelled or ScanProgress.Failed);
        var indexed = progressEvents.OfType<ScanProgress.FileIndexed>().ToList();
        Assert.True(indexed.Count >= notes, "scan progress incomplete under concurrent load");
        for (int i = 0; i < indexed.Count; i++)
        {
            Assert.Equal((ulong)(i + 1), indexed[i].Indexed);
        }

        Assert.True(saveTask.Result > 0, "no saves progressed during the storm");
        Assert.True(invokeTask.Result > 0, "no command invokes progressed during the storm");
        Assert.Equal(invokeTask.Result, action.InvocationCount);

        // VaultEventListener multi-method delivery under load: the initial
        // scan drives the index-phase choreography and the save churn
        // drives file-change events. (on_error delivery is covered
        // deterministically by EventKindsCensus.)
        // Under churn extra phase events may interleave, so assert the
        // scan's four-phase choreography as an in-order subsequence; the
        // quiet-session exact sequence is EventKindsCensus's job.
        var phases = events.Locked(r => r.IndexPhases.Select(p => p.Phase).ToList());
        var expectedOrder = new[]
        {
            IndexPhase.ScanStarted, IndexPhase.ReconcileStarted,
            IndexPhase.ReconcileFinished, IndexPhase.ScanFinished,
        };
        int cursor = 0;
        foreach (var phase in phases)
        {
            if (cursor < expectedOrder.Length && phase == expectedOrder[cursor])
            {
                cursor++;
            }
        }
        Assert.True(
            cursor == expectedOrder.Length,
            $"index-phase choreography incomplete under load (matched {cursor}/4: {string.Join(",", phases)})");
        Assert.True(
            events.Locked(r => r.FileChanges.Count) > 0,
            "no file-change events flowed during the storm");

        // Per-trait dispatch affinity (the actual binding contract, from
        // the W0-1 probe evidence):
        // - ScanProgressListener dispatches inline on the thread that
        //   called scan_initial_with_progress — nothing else.
        // - CommandAction re-enters on its invoking thread, which here is
        //   the pump ("UI") thread for every invocation.
        // - VaultEventListener file-change dispatch reaches managed code
        //   from the save-calling thread (background workers may add
        //   more, e.g. compaction), and never from the pump thread.
        Assert.Equal(new[] { Volatile.Read(ref scanThreadId) }, progress.ThreadIds.ToArray());
        Assert.Equal(new[] { pump.ManagedThreadId }, action.ThreadIds.ToArray());
        Assert.Contains(Volatile.Read(ref saveThreadId), events.ThreadIds);
        Assert.DoesNotContain(pump.ManagedThreadId, events.ThreadIds);

        session.UnregisterEventListener(registration);
    }

    [Fact]
    public void ListenerChurnUnderFire_UnregisterReleasesForeignHandles()
    {
        using var vault = FixtureVault.Create(4);
        using var session = VaultSession.OpenFilesystem(vault.Root);
        using var token = new CancelToken();
        session.ScanInitial(token);

        var stop = new CancellationTokenSource();
        Exception? churnFailure = null;
        var saveTask = Task.Run(() =>
        {
            try
            {
                int n = 0;
                while (!stop.IsCancellationRequested)
                {
                    session.SaveText("churn.md", $"rev {n++}\n", null);
                }
            }
            catch (Exception ex)
            {
                Volatile.Write(ref churnFailure, ex);
            }
        });

        int cycles = CensusTier.Scale(200, 500);
        var weakRefs = new List<WeakReference>();
        for (int i = 0; i < cycles; i++)
        {
            weakRefs.Add(RegisterUnregisterOnce(session, pauseMidRegistration: (i & 3) == 0));
        }
        stop.Cancel();
        Assert.True(saveTask.Wait(TimeSpan.FromSeconds(30)), "save churn did not terminate");
        Assert.Null(Volatile.Read(ref churnFailure));

        for (int i = 0; i < 3; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
        // Unregistration must release the foreign handle for every
        // listener — a leak here is unbounded retention under real churn.
        // Listener creation lives in a non-inlined helper so no JIT-held
        // local can pin one; at most one straggler is tolerated for an
        // in-flight dispatch that unregister raced with.
        int alive = weakRefs.Count(w => w.IsAlive);
        Assert.True(alive <= 1, $"unregistration pins foreign handles ({alive}/{cycles} still alive after GC)");

        RunSurvivorCheck(session);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference RegisterUnregisterOnce(VaultSession session, bool pauseMidRegistration)
    {
        var listener = new EventRecorder();
        ulong reg = session.RegisterEventListener(listener);
        if (pauseMidRegistration)
        {
            Thread.Sleep(1); // let some events land mid-registration
        }
        session.UnregisterEventListener(reg);
        return new WeakReference(listener);
    }

    private static void RunSurvivorCheck(VaultSession session)
    {
        // A listener registered across the churn still hears its events,
        // and unregister seals the stream.
        var survivor = new EventRecorder();
        ulong survivorReg = session.RegisterEventListener(survivor);
        session.SaveText("final.md", "for the survivor\n", null);
        Assert.True(
            Waiting.WaitFor(() => survivor.Locked(r => r.FileChanges.Count) > 0, 5000),
            "freshly registered listener received nothing after the churn");
        session.UnregisterEventListener(survivorReg);
        int sealedCount = survivor.TotalCount;
        session.SaveText("post-unregister.md", "silent\n", null);
        Thread.Sleep(400);
        Assert.Equal(sealedCount, survivor.TotalCount);
    }
}
