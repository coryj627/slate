// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;
using uniffi.slate_uniffi;

namespace SlateProbe;

internal static class StressSections
{
    /// <summary>
    /// §W-E GC pressure: thousands of handles through both the Dispose
    /// and the finalizer path, a session disposed while another thread is
    /// mid-call on it, and working-set drift as a leak signal.
    /// </summary>
    public static bool GcPressure(Probe p)
    {
        bool ok = true;
        long rssBefore = Process.GetCurrentProcess().WorkingSet64;

        for (int i = 0; i < 2000; i++)
        {
            var buffer = new DocumentBuffer($"note {i} body with some text");
            buffer.ApplyEdit(0, 0, "x");
            if ((i & 1) == 0) buffer.Dispose(); // odd ones ride the finalizer
            var token = new CancelToken();
            if ((i & 1) == 0) token.Dispose();
            if (i % 500 == 0)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }
        p.Note("ok: 2000 buffers + 2000 tokens through Dispose/finalizer mix");

        using (var vault = FixtureVault.Create(2, "gc"))
        {
            for (int i = 0; i < 60; i++)
            {
                var s = VaultSession.OpenFilesystem(vault.Root);
                if (i % 3 != 2) s.Dispose(); // every third session finalizer-collected
            }
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            using var reopened = VaultSession.OpenFilesystem(vault.Root);
            using var censusToken = new CancelToken();
            reopened.ScanInitial(censusToken);
            ok &= p.Check(reopened.ListFiles(FileFilter.All, new Paging(null, 1)).TotalFiltered == 2,
                "vault healthy after 60 session open/collect cycles");

            // Dispose-while-in-use: uniffi's call counter must keep the
            // native handle alive until the in-flight call returns.
            using var raceVault = FixtureVault.Create(300, "gcrace");
            var racing = VaultSession.OpenFilesystem(raceVault.Root);
            using var token = new CancelToken();
            var scan = Task.Run(() =>
            {
                try
                {
                    racing.ScanInitial(token);
                    return "completed";
                }
                catch (VaultException.Cancelled) { return "cancelled"; }
                catch (Exception ex) { return $"managed:{ex.GetType().Name}"; }
            });
            Thread.Sleep(80); // let the scan get onto the native side
            racing.Dispose();
            token.Cancel();
            bool finished = scan.Wait(TimeSpan.FromSeconds(30));
            ok &= p.Check(finished, "dispose-during-scan: call still terminates");
            if (finished)
            {
                p.Finding($"Dispose while a scan is in flight on another thread -> scan {scan.Result}, no native fault");
            }
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        long rssAfter = Process.GetCurrentProcess().WorkingSet64;
        p.Finding($"working set {rssBefore / 1_048_576} MiB -> {rssAfter / 1_048_576} MiB across GC-pressure loops");
        return ok;
    }

    /// <summary>
    /// §W-E callback concurrency across all three foreign traits at once:
    /// a progress-listening scan, vault-event-generating saves, and
    /// command invocations, on three C# threads against one session.
    /// </summary>
    public static bool CallbackConcurrency(Probe p)
    {
        bool ok = true;
        using var vault = FixtureVault.Create(500, "concurrency");
        using var session = VaultSession.OpenFilesystem(vault.Root);
        var events = new EventRecorder();
        ulong registration = session.RegisterEventListener(events);
        var progress = new ProgressRecorder();
        using var registry = new CommandRegistry();
        int commandInvokes = 0;
        var commandGauge = new ConcurrencyGauge();
        _ = registry.Register(
            new Command("probe.spin", "Spin", null, null, CommandSection.File),
            new ProbeAction(() =>
            {
                using var _ = commandGauge.Enter();
                Interlocked.Increment(ref commandInvokes);
            }));

        Exception? failure = null;
        using var scanToken = new CancelToken();
        var scanTask = Task.Run(() =>
        {
            try { return (object)session.ScanInitialWithProgress(scanToken, progress); }
            catch (Exception ex) { Volatile.Write(ref failure, ex); return ex; }
        });
        var stop = new CancellationTokenSource();
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
            catch (Exception ex) { Volatile.Write(ref failure, ex); return -1; }
        });
        var invokeTask = Task.Run(() =>
        {
            try
            {
                int n = 0;
                while (!stop.IsCancellationRequested)
                {
                    registry.InvokeById("probe.spin");
                    n++;
                }
                return n;
            }
            catch (Exception ex) { Volatile.Write(ref failure, ex); return -1; }
        });

        bool scanDone = scanTask.Wait(TimeSpan.FromSeconds(120));
        stop.Cancel();
        bool sideDone = Task.WaitAll(new Task[] { saveTask, invokeTask }, TimeSpan.FromSeconds(30));
        ok &= p.Check(scanDone && sideDone, "all three threads terminated (no deadlock)");
        ok &= p.Check(Volatile.Read(ref failure) == null,
            failure == null ? "no thread saw an exception" : $"thread failed: {failure!.GetType().Name}: {failure!.Message}");
        // The save thread creates churn*.md mid-walk, so the census may
        // exceed the fixture count — exact choreography is scan-progress's
        // job; here we only require completion under load.
        int indexedUnderLoad = progress.Snapshot().OfType<ScanProgress.FileIndexed>().Count();
        ok &= p.Check(indexedUnderLoad >= 500,
            $"scan progress complete under concurrent load ({indexedUnderLoad} files)");
        ok &= p.Check(saveTask.Result > 0 && invokeTask.Result > 0 && commandInvokes == invokeTask.Result,
            $"saves ({saveTask.Result}) and command invokes ({invokeTask.Result}) both progressed");
        ok &= p.Check(events.TotalCount > 0, "vault events flowed during the storm");
        p.Finding($"concurrency storm: {progress.Snapshot().Count} progress cbs, {events.TotalCount} vault events, " +
                  $"{commandInvokes} command invokes; peak in-flight — progress {progress.Gauge.Max}, " +
                  $"events {events.Gauge.Max}, commands {commandGauge.Max}");
        session.UnregisterEventListener(registration);
        return ok;
    }

    /// <summary>
    /// §W-E listener registration/unregistration lifetime: churn under
    /// fire, foreign handles released after unregister (WeakReference
    /// collectability), and a still-registered listener keeps receiving.
    /// </summary>
    public static bool ListenerLifetime(Probe p)
    {
        bool ok = true;
        using var vault = FixtureVault.Create(4, "listeners");
        using var session = VaultSession.OpenFilesystem(vault.Root);
        using var token = new CancelToken();
        session.ScanInitial(token);

        var stop = new CancellationTokenSource();
        var churnFailure = (Exception?)null;
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
            catch (Exception ex) { Volatile.Write(ref churnFailure, ex); }
        });

        var weakRefs = new List<WeakReference>();
        for (int i = 0; i < 400; i++)
        {
            var listener = new EventRecorder();
            ulong reg = session.RegisterEventListener(listener);
            if ((i & 3) == 0) Thread.Sleep(1); // let some events land mid-registration
            session.UnregisterEventListener(reg);
            weakRefs.Add(new WeakReference(listener));
        }
        stop.Cancel();
        ok &= p.Check(saveTask.Wait(TimeSpan.FromSeconds(30)) && churnFailure == null,
            "save churn survived 400 register/unregister cycles");

        for (int i = 0; i < 3; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
        int alive = weakRefs.Count(w => w.IsAlive);
        p.Finding($"unregistered listeners still strongly held after GC: {alive}/400 " +
                  $"(0 = foreign handles released eagerly)");
        ok &= p.Check(alive < 400, "unregistration releases foreign handles (not all pinned)");

        // A listener registered across the churn still hears its events.
        var survivor = new EventRecorder();
        ulong survivorReg = session.RegisterEventListener(survivor);
        session.SaveText("final.md", "for the survivor\n", null);
        ok &= p.Check(EventSections.WaitFor(() => survivor.Locked(r => r.FileChanges.Count) > 0, 5000),
            "freshly registered listener receives events after the churn");
        session.UnregisterEventListener(survivorReg);
        return ok;
    }
}
