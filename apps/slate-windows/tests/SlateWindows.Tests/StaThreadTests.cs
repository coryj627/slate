// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace SlateWindows.Tests;

/// <summary>
/// <see cref="StaThread"/>'s timeout path: a body that outlives its bound
/// still leaves no window class behind when it unwinds within the grace,
/// and one that never unwinds fails every fact that runs beside it until it
/// ends — then its classes are freed.
/// </summary>
public sealed class StaThreadTests
{
    /// <summary>The body neither pumps nor returns before its bound, so the
    /// shutdown the runner asks for never runs; it returns inside the grace,
    /// and its thread's classes — a dispatcher's and a visual's at least —
    /// are unregistered before the timeout is reported.</summary>
    [Fact]
    public void ABodyThatTimesOutAndThenUnwindsLeavesNoWindowClassBehind()
    {
        string? name = null;
        int registered = 0;
        Xunit.Sdk.XunitException timedOut = Assert.Throws<Xunit.Sdk.XunitException>(() => StaThread.Run(
            () =>
            {
                name = Thread.CurrentThread.Name;
                _ = Dispatcher.CurrentDispatcher;
                _ = new Border { Visibility = Visibility.Collapsed };
                registered = WindowClasses.RegisteredFor(name!).Count;
                Thread.Sleep(TimeSpan.FromMilliseconds(800));
            },
            TimeSpan.FromMilliseconds(200),
            "the body outlived its bound",
            unwindGrace: TimeSpan.FromSeconds(10)));

        Assert.Contains("the body unwound", timedOut.Message, StringComparison.Ordinal);
        Assert.NotNull(name);
        Assert.True(registered >= 2, $"premise: the body's thread registered {registered} WPF classes, expected a dispatcher's and a visual's");
        Assert.Empty(WindowClasses.RegisteredFor(name));
    }

    /// <summary>A body that never unwinds runs on beside the facts after its
    /// own: <see cref="StaThread.Wedged"/> names it — and the leak guard's
    /// <c>Before</c> fails the next fact on it — until it ends, when its
    /// classes are unregistered and the run is clean again.</summary>
    [Fact]
    public void ABodyThatNeverUnwindsFailsTheFactsBesideItUntilItEnds()
    {
        using var release = new ManualResetEventSlim(false);
        string? name = null;
        Xunit.Sdk.XunitException timedOut = Assert.Throws<Xunit.Sdk.XunitException>(() => StaThread.Run(
            () =>
            {
                name = Thread.CurrentThread.Name;
                _ = new Border { Visibility = Visibility.Collapsed };
                _ = release.Wait(TimeSpan.FromSeconds(60));
            },
            TimeSpan.FromMilliseconds(200),
            "the body never returned",
            unwindGrace: TimeSpan.FromMilliseconds(200)));
        try
        {
            Assert.Contains("has not unwound", timedOut.Message, StringComparison.Ordinal);
            Assert.NotNull(name);
            string? wedged = StaThread.Wedged();
            Assert.NotNull(wedged);
            Assert.Contains(name, wedged, StringComparison.Ordinal);
            MethodInfo next = typeof(StaThreadTests).GetMethod(nameof(ABodyThatTimesOutAndThenUnwindsLeavesNoWindowClassBehind))!;
            Xunit.Sdk.XunitException refused = Assert.Throws<Xunit.Sdk.XunitException>(
                () => new LeakedDispatcherGuardAttribute().Before(next));
            Assert.Contains(name, refused.Message, StringComparison.Ordinal);
        }
        finally
        {
            release.Set();
            Assert.True(
                SpinWait.SpinUntil(() => StaThread.Wedged() is null, TimeSpan.FromSeconds(10)),
                "the released body's thread never ended");
        }
        Assert.Empty(WindowClasses.RegisteredFor(name));
    }
}
