// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Reflection;
using System.Windows.Threading;

[assembly: AssemblyFixture(typeof(SlateWindows.Tests.RunnerThreadShutDownListeners))]

namespace SlateWindows.Tests;

/// <summary>
/// Lets the test process exit. Runs once, after the last fact, as an xunit
/// assembly fixture.
/// </summary>
/// <remarks>
/// <para>
/// WPF registers a process-exit listener for each weak-event table (the store
/// behind <c>PropertyChangedEventManager</c>, <c>CommandManager</c> and the
/// like). At exit the listener marshals its clean-up to the table's own
/// dispatcher with a synchronous <c>Dispatcher.Invoke</c>, which waits for
/// that thread to pump. A plain fact that builds a view model on one of the
/// runtime's pool threads gives that thread a dispatcher that never pumps
/// (<see cref="LeakedDispatcherGuardAttribute"/> lets those be, as the
/// runner's to end), and a thread that ended without shutting its dispatcher
/// down never pumps again. Either way the finalizer thread, which raises
/// process exit, waits forever. xunit v3 runs the suite in its own process
/// and fails the run once it is still alive ten seconds after the last fact
/// ("Foreground threads were left running").
/// </para>
/// <para>
/// The fixture stops those listeners only: a table on a dispatcher that has
/// shut down needs no clean-up, and one whose thread can still pump answers
/// by itself. The process is ending, so the tables' clean-up is moot.
/// WPF keeps the listeners private; <see cref="Readable"/> is asserted by
/// <c>RunnerThreadShutDownListenersTests</c>, so a framework change fails a
/// fact instead of the run's exit.
/// </para>
/// </remarks>
public sealed class RunnerThreadShutDownListeners : IDisposable
{
    private static readonly FieldInfo? ProcessExit =
        typeof(AppDomain).GetField("ProcessExit", BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly Type? ListenerType =
        typeof(Dispatcher).Assembly.GetType("MS.Internal.ShutDownListener");

    private static readonly MethodInfo? StopListening =
        ListenerType?.GetMethod("StopListening", BindingFlags.NonPublic | BindingFlags.Instance);

    /// <summary>Whether WPF's listeners are where this fixture reads them.</summary>
    internal static bool Readable => ProcessExit is not null && ListenerType is not null && StopListening is not null;

    public void Dispose() => Release();

    /// <summary>Stop every listener whose dispatcher can never answer it.</summary>
    internal static void Release()
    {
        foreach (WeakReference listener in Blocking())
        {
            StopListening!.Invoke(listener, null);
        }
    }

    /// <summary>The dispatchers whose process-exit listeners would block exit.</summary>
    internal static List<Dispatcher> BlockingOwners() =>
        [.. Blocking().Select(listener => ((DispatcherObject)listener.Target!).Dispatcher)];

    private static List<WeakReference> Blocking()
    {
        var blocking = new List<WeakReference>();
        if (!Readable || ProcessExit!.GetValue(AppDomain.CurrentDomain) is not EventHandler exit)
        {
            return blocking;
        }
        foreach (Delegate handler in exit.GetInvocationList())
        {
            if (handler.Target is WeakReference { Target: DispatcherObject { Dispatcher: { } dispatcher } } listener
                && ListenerType!.IsInstanceOfType(listener)
                && !dispatcher.HasShutdownStarted
                && (!dispatcher.Thread.IsAlive
                    || LeakedDispatcherGuardAttribute.RuntimeWorker(dispatcher.Thread.Name)))
            {
                blocking.Add(listener);
            }
        }
        return blocking;
    }
}
