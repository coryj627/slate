// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Windows.Threading;

namespace SlateWindows.Tests;

/// <summary>
/// The one way a unit fact runs WPF work on a thread of its own: a fresh
/// STA thread, and once it has ended, the window classes it registered are
/// unregistered. Every per-class <c>RunSta</c>/<c>OnSta</c> helper forwards
/// here, and <c>StaThreadCensus</c> keeps it that way.
/// </summary>
/// <remarks>
/// <para>
/// Why. When a thread ends, Windows destroys its windows but NOT the window
/// classes registered for them. Every WPF <c>HwndWrapper</c> — a
/// Dispatcher's message window, its MediaContext's notification window,
/// each HwndSource, a hidden taskbar owner — registers a uniquely named
/// class that only a WPF-side dispose unregisters, and a fact's thread ends
/// without one: two classes leak per fact that only builds visuals, six per
/// fact that shows a window, for the life of the test process. Each holds
/// roughly 270 bytes of the DESKTOP HEAP. CI's session is non-interactive,
/// so its desktop heap is 768 KB rather than the interactive desktop's
/// 20 MB, and once a few thousand leaked classes fill it every later
/// <c>CreateWindowEx</c> fails — "The operation completed successfully" or
/// "Not enough memory resources are available" out of
/// <c>MediaContext..ctor</c>, <c>Window.SetTaskbarStatus</c> or
/// <c>Dispatcher..ctor</c> — and every later hosted fact fails with it.
/// </para>
/// <para>
/// How. WPF names each class <c>HwndWrapper[app;thread;guid]</c>, so every
/// fact's thread gets a name of its own, and after the thread has ended —
/// its windows destroyed by the system — every class carrying that name is
/// unregistered. A class that will not unregister fails the fact, naming
/// it. Nothing runs on the fact's thread after its body: tearing its
/// windows down while the thread lives (a dispatcher shutdown disposes
/// them) hands the foreground to another process's window, after which the
/// next fact's <c>Window.Activate</c> is refused and every focus-dependent
/// fact after it fails (measured: ReadingFocusTests, 6–9 of 21 facts),
/// whereas a thread that simply ends leaves the foreground for the next
/// fact to take. The fact's thread therefore ends exactly as it always has.
/// </para>
/// <para>
/// Bounded and fail-fast. The thread is a background thread, so a wedged
/// body can never hold the test host open past the run. On timeout its
/// dispatcher is told to shut down, so a pump wedged in a frame unwinds
/// through the body's own disposals, and the fact fails here instead of at
/// the job's limit: a body that unwinds within <see cref="UnwindGrace"/>
/// still has its classes unregistered; one that does not is running beside
/// every later fact, so <see cref="LeakedDispatcherGuardAttribute"/> fails
/// each of them (<see cref="Wedged"/>) until it ends, and then frees its
/// classes.
/// </para>
/// </remarks>
internal static class StaThread
{
    /// <summary>The bound a caller that names none gets.</summary>
    internal static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(2);

    /// <summary>How long a timed-out body gets to unwind once its dispatcher
    /// has been told to shut down.</summary>
    internal static readonly TimeSpan UnwindGrace = TimeSpan.FromSeconds(10);

    /// <summary>Every fact thread's name starts with this; the rest is a
    /// sequence number, so the thread's window classes are its alone.</summary>
    internal const string ThreadNamePrefix = "sta-fact-";

    private static readonly List<(string Name, Func<bool> Ended, string What)> s_wedged = [];

    private static int s_sequence;

    /// <summary>Run <paramref name="body"/> on a fresh STA thread, unregister
    /// the window classes it leaves behind, and rethrow whatever it threw.</summary>
    internal static void Run(
        Action body, TimeSpan? timeout = null, string? timeoutMessage = null, TimeSpan? unwindGrace = null) =>
        Run<object?>(
            () =>
            {
                body();
                return null;
            },
            timeout,
            timeoutMessage,
            unwindGrace);

    /// <summary><see cref="Run(Action, TimeSpan?, string?, TimeSpan?)"/> under
    /// <see cref="PumpedDispatcher.Run"/>: the body's documents capture the
    /// thread's dispatcher as their owner context.</summary>
    internal static void RunPumped(Action body, TimeSpan? timeout = null, string? timeoutMessage = null) =>
        Run(() => PumpedDispatcher.Run(body), timeout, timeoutMessage);

    /// <summary>Run <paramref name="body"/> on a fresh STA thread and answer
    /// its result once the window classes it leaves behind are unregistered.
    /// <paramref name="unwindGrace"/> overrides <see cref="UnwindGrace"/>.</summary>
    internal static T Run<T>(
        Func<T> body, TimeSpan? timeout = null, string? timeoutMessage = null, TimeSpan? unwindGrace = null)
    {
        T result = default!;
        ExceptionDispatchInfo? failure = null;
        Thread? owner = null;
        string name = ThreadNamePrefix + Interlocked.Increment(ref s_sequence).ToString(CultureInfo.InvariantCulture);
        var thread = new Thread(() =>
        {
            Volatile.Write(ref owner, Thread.CurrentThread);
            try
            {
                result = body();
            }
            catch (Exception exception)
            {
                failure = ExceptionDispatchInfo.Capture(exception);
            }
        })
        {
            IsBackground = true,
            Name = name,
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        TimeSpan limit = timeout ?? DefaultTimeout;
        if (!thread.Join(limit))
        {
            if (Volatile.Read(ref owner) is { } started)
            {
                Dispatcher.FromThread(started)?.BeginInvokeShutdown(DispatcherPriority.Send);
            }
            string what = $"{timeoutMessage ?? "The STA fact timed out."} (thread {name}, bound {limit.TotalSeconds:0.#} s; its dispatcher was told to shut down";
            if (thread.Join(unwindGrace ?? UnwindGrace))
            {
                string? left = WindowClasses.Unregister(name);
                throw new Xunit.Sdk.XunitException(what + " and the body unwound" + (left is null ? ".)" : "; " + left + ")"));
            }
            lock (s_wedged)
            {
                s_wedged.Add((name, () => thread.Join(0), timeoutMessage ?? "an STA fact that timed out"));
            }
            throw new Xunit.Sdk.XunitException(
                what + ", but the body has not unwound. It runs on beside the facts after this one, so each of them fails until it ends.)");
        }
        string? residue = WindowClasses.Unregister(name);
        failure?.Throw();
        if (residue is not null)
        {
            throw new Xunit.Sdk.XunitException(residue);
        }
        return result;
    }

    /// <summary>The timed-out fact threads still running (null when none
    /// are): a fact that runs beside one is not isolated, so the leak guard
    /// fails it. A wedged thread that has since ended is dropped here, and
    /// its window classes unregistered.</summary>
    internal static string? Wedged()
    {
        var running = new List<string>();
        var residue = new List<string>();
        lock (s_wedged)
        {
            for (int i = s_wedged.Count - 1; i >= 0; i--)
            {
                (string name, Func<bool> ended, string what) = s_wedged[i];
                if (!ended())
                {
                    running.Add($"{name} ({what})");
                    continue;
                }
                s_wedged.RemoveAt(i);
                if (WindowClasses.Unregister(name) is { } left)
                {
                    residue.Add(left);
                }
            }
        }
        if (running.Count == 0 && residue.Count == 0)
        {
            return null;
        }
        return string.Join(
            " ",
            (running.Count == 0 ? [] : new[] { $"An earlier fact's STA thread never unwound after its timeout and is still running beside this fact: {string.Join(", ", running)}." })
                .Concat(residue));
    }
}