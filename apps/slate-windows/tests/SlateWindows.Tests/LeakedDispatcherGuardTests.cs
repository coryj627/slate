// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Runtime.CompilerServices;
using System.Windows.Threading;

namespace SlateWindows.Tests;

/// <summary>
/// The assembly-wide leak guard's two blind spots, closed: a dispatcher the
/// garbage collector took before the fact ended, and one still running
/// when it ended. Each scenario runs on a thread of its own named for this
/// witness, is judged against a baseline of its own, and is tidied (its
/// class unregistered, its dispatcher left for the collector) so the real
/// guard around the fact sees nothing.
/// </summary>
public sealed class LeakedDispatcherGuardTests
{
    /// <summary>A thread that touches <c>Dispatcher.CurrentDispatcher</c> and
    /// ends leaves its dispatcher collectable — WPF's registry holds it
    /// weakly — but its window class registered. After a collection the
    /// registry no longer shows it; the class witness still names it.</summary>
    [Fact]
    public void AnAbandonedDispatcherIsCaughtAfterTheCollectorTakesIt()
    {
        string name = "guard-witness-" + Guid.NewGuid().ToString("N");
        FactBaseline baseline = FactBaseline.Capture();
        try
        {
            AbandonADispatcherOn(name);
            Collect();
            Assert.DoesNotContain(LeakedDispatcherGuardAttribute.Snapshot(), dispatcher => dispatcher.Thread.Name == name);
            Assert.NotEmpty(WindowClasses.RegisteredFor(name));

            (List<string> problems, List<Dispatcher> outliving) = baseline.Judge();

            Assert.Empty(outliving);
            Assert.Contains(problems, problem => problem.Contains(name, StringComparison.Ordinal));
        }
        finally
        {
            Assert.Null(WindowClasses.Unregister(name));
        }
    }

    /// <summary>A dispatcher still running when its fact ends fails that
    /// fact, and stays owned by it: while it runs, settling reports nothing
    /// and keeps it; once its thread ends without a shutdown, settling
    /// reports it against the fact that left it running. One shut down
    /// properly is dropped silently.</summary>
    [Fact]
    public void ADispatcherThatOutlivesItsFactFailsItAndStaysOwned()
    {
        string name = "guard-witness-" + Guid.NewGuid().ToString("N");
        (List<string> atFactEnd, List<string> whileRunning, int ownedWhileRunning, List<string> afterEnd, int ownedAfterEnd, List<string> shutDown) =
            OutliveAFact(name);
        Collect();

        Assert.Contains(atFactEnd, problem => problem.Contains(name, StringComparison.Ordinal) && problem.Contains("still running", StringComparison.Ordinal));
        Assert.Empty(whileRunning);
        Assert.Equal(1, ownedWhileRunning);
        Assert.Contains(afterEnd, problem => problem.Contains("the witness fact", StringComparison.Ordinal) && problem.Contains("without a shutdown", StringComparison.Ordinal));
        Assert.Equal(0, ownedAfterEnd);
        Assert.Empty(shutDown);
        Assert.DoesNotContain(LeakedDispatcherGuardAttribute.Snapshot(), dispatcher => dispatcher.Thread.Name?.StartsWith(name, StringComparison.Ordinal) == true);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void AbandonADispatcherOn(string name)
    {
        var thread = new Thread(() => _ = Dispatcher.CurrentDispatcher) { IsBackground = true, Name = name };
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "the witness thread never ended");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (List<string>, List<string>, int, List<string>, int, List<string>) OutliveAFact(string name)
    {
        FactBaseline baseline = FactBaseline.Capture();
        using var release = new ManualResetEventSlim(false);
        using var ready = new ManualResetEventSlim(false);
        var abandoning = new Thread(() =>
        {
            _ = Dispatcher.CurrentDispatcher;
            ready.Set();
            _ = release.Wait(TimeSpan.FromSeconds(30));
        })
        { IsBackground = true, Name = name };
        abandoning.Start();
        Assert.True(ready.Wait(TimeSpan.FromSeconds(10)), "the witness thread never started");
        var outstanding = new List<(Dispatcher Dispatcher, string Fact)>();
        List<string> afterEnd;
        List<string> atFactEnd;
        List<string> whileRunning;
        int ownedWhileRunning;
        try
        {
            (atFactEnd, List<Dispatcher> outliving) = baseline.Judge();
            outstanding.AddRange(outliving.Select(dispatcher => (dispatcher, "the witness fact")));
            whileRunning = LeakedDispatcherGuardAttribute.Settle(outstanding);
            ownedWhileRunning = outstanding.Count;
        }
        finally
        {
            release.Set();
            Assert.True(abandoning.Join(TimeSpan.FromSeconds(10)), "the witness thread never ended");
        }
        afterEnd = LeakedDispatcherGuardAttribute.Settle(outstanding);
        int ownedAfterEnd = outstanding.Count;
        Assert.Null(WindowClasses.Unregister(name));

        // The well-behaved twin: running at the fact's end, then shut down.
        Dispatcher? twin = null;
        using var twinReady = new ManualResetEventSlim(false);
        var shuttingDown = new Thread(() =>
        {
            twin = Dispatcher.CurrentDispatcher;
            twinReady.Set();
            Dispatcher.Run();
        })
        { IsBackground = true, Name = name + "-twin" };
        shuttingDown.Start();
        Assert.True(twinReady.Wait(TimeSpan.FromSeconds(10)), "the twin never started");
        var owned = new List<(Dispatcher Dispatcher, string Fact)> { (twin!, "the twin fact") };
        twin!.InvokeShutdown();
        Assert.True(shuttingDown.Join(TimeSpan.FromSeconds(10)), "the twin never shut down");
        List<string> shutDown = LeakedDispatcherGuardAttribute.Settle(owned);
        Assert.Empty(owned);
        return (atFactEnd, whileRunning, ownedWhileRunning, afterEnd, ownedAfterEnd, shutDown);
    }

    private static void Collect()
    {
        for (int pass = 0; pass < 3; pass++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }
}
