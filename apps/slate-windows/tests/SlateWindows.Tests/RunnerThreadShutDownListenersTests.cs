// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.ComponentModel;
using System.Windows.Threading;

namespace SlateWindows.Tests;

/// <summary>
/// The end-of-run fixture that lets the test process exit (xunit v3 fails a
/// run whose process is still alive ten seconds after the last fact).
/// </summary>
public sealed class RunnerThreadShutDownListenersTests
{
    [Fact]
    public void TheFixtureIsRegisteredAndWpfsShutDownListenersAreWhereItReadsThem()
    {
        Assert.Contains(
            typeof(RunnerThreadShutDownListenersTests).Assembly.GetCustomAttributes(typeof(AssemblyFixtureAttribute), inherit: false),
            attribute => ((AssemblyFixtureAttribute)attribute).AssemblyFixtureType == typeof(RunnerThreadShutDownListeners));
        Assert.True(
            RunnerThreadShutDownListeners.Readable,
            "WPF's ShutDownListener, its StopListening, or AppDomain.ProcessExit moved: the fixture can no longer release them, and a full run will fail at exit");
    }

    /// <summary>
    /// A weak-event table made on a runtime pool thread (a view model built
    /// in a plain fact) registers a process-exit listener that would block
    /// exit on the pool thread's never-pumped dispatcher; the fixture
    /// releases it and leaves a listener on a dispatcher that can still
    /// answer alone.
    /// </summary>
    [Fact]
    public async Task APoolThreadsWeakEventListenerIsReleasedAndAPumpedOneIsKept()
    {
        Dispatcher poolDispatcher = await Task.Run(() =>
        {
            var source = new Notifier();
            PropertyChangedEventManager.AddHandler(source, (_, _) => { }, string.Empty);
            GC.KeepAlive(source);
            return Dispatcher.CurrentDispatcher;
        });
        Assert.StartsWith(".NET ", poolDispatcher.Thread.Name ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains(poolDispatcher, RunnerThreadShutDownListeners.BlockingOwners());

        RunnerThreadShutDownListeners.Release();

        Assert.DoesNotContain(poolDispatcher, RunnerThreadShutDownListeners.BlockingOwners());
    }

    private sealed class Notifier : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { }
            remove { }
        }
    }
}
