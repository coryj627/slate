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
        int commandInvokes = 0;
        _ = registry.Register(
            new Command("census.spin", "Spin", null, null, CommandSection.File),
            new ScriptedAction(() => Interlocked.Increment(ref commandInvokes)));

        // UI-thread simulation: a dedicated thread pumping short work items
        // for the duration of the storm, the load pattern §W-E names.
        var stop = new CancellationTokenSource();
        var uiPump = Task.Factory.StartNew(() =>
        {
            var spin = new System.Diagnostics.Stopwatch();
            while (!stop.IsCancellationRequested)
            {
                spin.Restart();
                while (spin.ElapsedTicks < TimeSpan.TicksPerMillisecond)
                {
                }
                Thread.Yield();
            }
        }, TaskCreationOptions.LongRunning);

        Exception? failure = null;
        using var scanToken = new CancelToken();
        var scanTask = Task.Run(() =>
        {
            try
            {
                session.ScanInitialWithProgress(scanToken, progress);
            }
            catch (Exception ex)
            {
                Volatile.Write(ref failure, ex);
            }
        });
        var saveTask = Task.Run(() =>
        {
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
                    registry.InvokeById("census.spin");
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
        bool sideDone = Task.WaitAll(new Task[] { saveTask, invokeTask, uiPump }, TimeSpan.FromSeconds(30));

        Assert.True(scanDone && sideDone, "storm threads did not terminate (deadlock?)");
        Assert.Null(Volatile.Read(ref failure));
        // Saves create churn*.md mid-walk so the census may exceed the
        // fixture count; completion under load is the requirement here.
        Assert.True(
            progress.Snapshot().OfType<ScanProgress.FileIndexed>().Count() >= notes,
            "scan progress incomplete under concurrent load");
        Assert.True(saveTask.Result > 0, "no saves progressed during the storm");
        Assert.True(invokeTask.Result > 0, "no command invokes progressed during the storm");
        Assert.Equal(invokeTask.Result, commandInvokes);
        Assert.True(events.TotalCount > 0, "no vault events flowed during the storm");

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
            var listener = new EventRecorder();
            ulong reg = session.RegisterEventListener(listener);
            if ((i & 3) == 0)
            {
                Thread.Sleep(1); // let some events land mid-registration
            }
            session.UnregisterEventListener(reg);
            weakRefs.Add(new WeakReference(listener));
        }
        stop.Cancel();
        Assert.True(saveTask.Wait(TimeSpan.FromSeconds(30)), "save churn did not terminate");
        Assert.Null(Volatile.Read(ref churnFailure));

        for (int i = 0; i < 3; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
        int alive = weakRefs.Count(w => w.IsAlive);
        Assert.True(alive < cycles, $"unregistration pins foreign handles ({alive}/{cycles} still alive)");

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
