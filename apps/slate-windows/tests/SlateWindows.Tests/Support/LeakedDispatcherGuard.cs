// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections;
using System.Reflection;
using System.Windows.Threading;

namespace SlateWindows.Tests;

/// <summary>
/// Fails any fact that ends with a WPF <see cref="Dispatcher"/> it created
/// abandoned: its thread gone, the dispatcher never shut down. Applied to
/// the whole assembly (<c>AssemblyInfo.cs</c>), so it holds for every
/// current and future fact, whichever helper — or none — started the
/// thread.
/// </summary>
/// <remarks>
/// <para>
/// An abandoned dispatcher is the leak <see cref="StaThread"/> exists to
/// prevent: its thread's windows died with the thread, but the window
/// classes of its <c>HwndWrapper</c>s stay registered — each holding
/// desktop heap — for the rest of the process, and on CI's 768 KB
/// non-interactive desktop heap a few thousand of them fail every later
/// window the run creates.
/// </para>
/// <para>
/// Attribution is exact: only dispatchers that did not exist before the
/// fact started are judged, and only once their thread has ended, so a
/// long-lived thread (the test runner's own, a fixture's) is never blamed
/// and a leak is reported against the fact that made it.
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
    private static HashSet<Dispatcher> _before = new(ReferenceEqualityComparer.Instance);

    /// <summary>Whether WPF's dispatcher registry is where this guard reads it.</summary>
    internal static bool Readable => Registry?.GetValue(null) is IList && RegistryLock?.GetValue(null) is not null;

    public override void Before(MethodInfo methodUnderTest) =>
        _before = new HashSet<Dispatcher>(Snapshot(), ReferenceEqualityComparer.Instance);

    public override void After(MethodInfo methodUnderTest)
    {
        string[] abandoned = [.. Snapshot()
            .Where(dispatcher => !_before.Contains(dispatcher)
                && !dispatcher.Thread.IsAlive
                && !dispatcher.HasShutdownFinished)
            .Select(dispatcher => $"\"{dispatcher.Thread.Name ?? "unnamed"}\" (managed id {dispatcher.Thread.ManagedThreadId})")];
        _before = new(ReferenceEqualityComparer.Instance);
        if (abandoned.Length > 0)
        {
            throw new Xunit.Sdk.XunitException(
                $"{methodUnderTest.DeclaringType?.Name}.{methodUnderTest.Name} abandoned {abandoned.Length} WPF dispatcher(s) — "
                + $"thread(s) {string.Join(", ", abandoned)} ended without a dispatcher shutdown, so their window classes "
                + "stay registered and hold desktop heap for the rest of the run. Run the body through StaThread.Run, "
                + "or shut the dispatcher down before its thread ends.");
        }
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
}
