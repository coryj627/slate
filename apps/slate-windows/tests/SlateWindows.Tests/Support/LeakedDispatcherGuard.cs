// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections;
using System.Reflection;
using System.Windows.Threading;

namespace SlateWindows.Tests;

/// <summary>
/// Fails any fact that leaves WPF resources behind it: a window class it
/// registered that is still registered, or a dispatcher it created that is
/// still running or was abandoned. Applied to the whole assembly
/// (<c>AssemblyInfo.cs</c>), so it holds for every current and future
/// fact, whichever helper — or none — started its threads.
/// </summary>
/// <remarks>
/// <para>
/// What it protects: CI's 768 KB non-interactive desktop heap. A thread that
/// ends without WPF disposing its <c>HwndWrapper</c>s leaves their window
/// classes registered for the rest of the process (see
/// <see cref="StaThread"/>), and a few thousand of them fail every later
/// window the run creates.
/// </para>
/// <para>
/// Two witnesses, compared against a baseline taken before the fact
/// (<see cref="FactBaseline"/>). The window classes this process has
/// registered (<see cref="WindowClasses"/>) are the leak itself, and
/// survive the garbage collector — a dispatcher whose thread has ended is
/// collectable, and WPF's registry holds it only weakly, so a registry read
/// alone misses a leak once a collection runs. The registry names the
/// dispatcher still running after its fact, the case a class check cannot
/// tell from a live window.
/// </para>
/// <para>
/// Ownership outlives the fact. A dispatcher still running when its fact
/// ends fails that fact, and stays owned by it: if its thread later ends
/// without a shutdown, the fact running then fails, naming the owner.
/// </para>
/// <para>
/// Not judged: the runtime's own worker threads (named <c>.NET …</c> — the
/// pool's and <c>LongRunning</c> tasks', which xUnit runs facts on).
/// Production code that captures <c>Dispatcher.CurrentDispatcher</c> in a
/// constructor gives the runner's thread a dispatcher when a plain fact
/// builds it there; the thread is MTA, so its dispatcher hosts no window
/// and costs one class — about a hundred per full run, against the
/// thousands the STA facts leaked — and the thread is the runner's to end.
/// Nor are the dispatchers of <see cref="StaThread"/>'s threads, abandoned
/// by design: the runner frees their classes (which the class witness
/// still checks), and every fact that runs beside one of its threads that
/// never unwound fails (<see cref="StaThread.Wedged"/>).
/// </para>
/// <para>
/// WPF keeps its dispatchers in a private registry; this reads it by
/// reflection. <see cref="Readable"/> is asserted by
/// <c>StaThreadCensus</c>, so a framework change that renames it fails one
/// census loudly instead of silently disarming the guard.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Assembly)]
internal sealed class LeakedDispatcherGuardAttribute : Xunit.Sdk.BeforeAfterTestAttribute
{
    private static readonly FieldInfo? Registry =
        typeof(Dispatcher).GetField("_dispatchers", BindingFlags.NonPublic | BindingFlags.Static);

    private static readonly FieldInfo? RegistryLock =
        typeof(Dispatcher).GetField("_globalLock", BindingFlags.NonPublic | BindingFlags.Static);

    // The facts run serially (AssemblyInfo.cs), so one baseline suffices.
    private static FactBaseline? s_baseline;

    // Dispatchers still running when their fact ended, with that fact.
    private static readonly List<(Dispatcher Dispatcher, string Fact)> s_outstanding = [];

    /// <summary>Whether WPF's dispatcher registry is where this guard reads it.</summary>
    internal static bool Readable => Registry?.GetValue(null) is IList && RegistryLock?.GetValue(null) is not null;

    public override void Before(MethodInfo methodUnderTest)
    {
        s_baseline = FactBaseline.Capture();
        if (StaThread.Wedged() is { } wedged)
        {
            throw new Xunit.Sdk.XunitException(wedged);
        }
    }

    public override void After(MethodInfo methodUnderTest)
    {
        string fact = $"{methodUnderTest.DeclaringType?.Name}.{methodUnderTest.Name}";
        FactBaseline baseline = s_baseline ?? FactBaseline.Capture();
        s_baseline = null;
        (List<string> problems, List<Dispatcher> outliving) = baseline.Judge();
        problems.AddRange(Settle(s_outstanding));
        s_outstanding.AddRange(outliving.Select(dispatcher => (dispatcher, fact)));
        if (problems.Count > 0)
        {
            throw new Xunit.Sdk.XunitException(
                $"{fact} left WPF resources behind — each window class holds desktop heap for the rest of the run: "
                + string.Join(" ", problems)
                + " Run the body through StaThread.Run, or shut the dispatcher down (and join its thread) before the fact ends.");
        }
    }

    /// <summary>Drop the owned dispatchers that have shut down; report, and
    /// drop, those whose thread has ended without a shutdown.</summary>
    internal static List<string> Settle(List<(Dispatcher Dispatcher, string Fact)> outstanding)
    {
        var problems = new List<string>();
        for (int i = outstanding.Count - 1; i >= 0; i--)
        {
            (Dispatcher dispatcher, string fact) = outstanding[i];
            if (dispatcher.HasShutdownFinished)
            {
                outstanding.RemoveAt(i);
            }
            else if (!dispatcher.Thread.IsAlive)
            {
                outstanding.RemoveAt(i);
                problems.Add($"The dispatcher {fact} left running on thread {Describe(dispatcher)} has since ended without a shutdown.");
            }
        }
        return problems;
    }

    internal static List<Dispatcher> Snapshot()
    {
        var live = new List<Dispatcher>();
        if (Registry?.GetValue(null) is not IList registry || RegistryLock?.GetValue(null) is not { } gate)
        {
            return live;
        }
        lock (gate)
        {
            foreach (object? entry in registry)
            {
                if (entry is WeakReference { Target: Dispatcher dispatcher })
                {
                    live.Add(dispatcher);
                }
            }
        }
        return live;
    }

    /// <summary>Whether a thread of this name is the runtime's own worker,
    /// whose dispatcher and classes the guard does not judge.</summary>
    internal static bool RuntimeWorker(string? threadName) =>
        threadName?.StartsWith(".NET ", StringComparison.Ordinal) == true;

    /// <summary>Whether the guard does not judge a dispatcher on a thread of
    /// this name: the runtime's workers, and <see cref="StaThread"/>'s own
    /// (abandoned by design — the runner frees their classes, which the
    /// class witness still checks).</summary>
    internal static bool ExemptDispatcher(string? threadName) =>
        RuntimeWorker(threadName)
        || threadName?.StartsWith(StaThread.ThreadNamePrefix, StringComparison.Ordinal) == true;

    internal static string Describe(Dispatcher dispatcher) =>
        $"\"{dispatcher.Thread.Name ?? "unnamed"}\" (managed id {dispatcher.Thread.ManagedThreadId})";
}

/// <summary>What existed before a fact — WPF dispatchers and this process's
/// WPF window classes — so what the fact left behind can be judged.</summary>
internal sealed class FactBaseline
{
    private readonly HashSet<Dispatcher> _dispatchers;
    private readonly HashSet<string> _classes;

    private FactBaseline(HashSet<Dispatcher> dispatchers, HashSet<string> classes)
    {
        _dispatchers = dispatchers;
        _classes = classes;
    }

    internal static FactBaseline Capture() => new(
        new HashSet<Dispatcher>(LeakedDispatcherGuardAttribute.Snapshot(), ReferenceEqualityComparer.Instance),
        WindowClasses.Registered());

    /// <summary>What the fact left behind: the problems, and the dispatchers
    /// it left running (which the caller keeps owning).</summary>
    internal (List<string> Problems, List<Dispatcher> Outliving) Judge()
    {
        var problems = new List<string>();
        var outliving = new List<Dispatcher>();
        foreach (Dispatcher dispatcher in LeakedDispatcherGuardAttribute.Snapshot())
        {
            if (_dispatchers.Contains(dispatcher)
                || dispatcher.HasShutdownFinished
                || LeakedDispatcherGuardAttribute.ExemptDispatcher(dispatcher.Thread.Name))
            {
                continue;
            }
            if (dispatcher.Thread.IsAlive)
            {
                outliving.Add(dispatcher);
                problems.Add($"A dispatcher on thread {LeakedDispatcherGuardAttribute.Describe(dispatcher)} is still running after the fact.");
            }
            else
            {
                problems.Add($"Thread {LeakedDispatcherGuardAttribute.Describe(dispatcher)} ended without shutting its dispatcher down.");
            }
        }
        string[] classes = [.. WindowClasses.Registered()
            .Where(name => !_classes.Contains(name) && !LeakedDispatcherGuardAttribute.RuntimeWorker(WindowClasses.ThreadOf(name)))
            .Order(StringComparer.Ordinal)];
        if (classes.Length > 0)
        {
            problems.Add($"{classes.Length} window class(es) registered during the fact are still registered: {string.Join(", ", classes)}.");
        }
        return (problems, outliving);
    }
}
