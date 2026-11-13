// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Globalization;
using System.Reflection;

namespace SlateWindows.Tests;

/// <summary>#1826: the watch over the test process's own exit. xUnit ends a
/// run whose process is still alive 11 s after its entry point returns,
/// which the VSTest adapter reports as a bare
/// <c>[FATAL ERROR] Xunit.Sdk.TestPipelineException</c>. The watch logs
/// when the run ended, times every exit handler, and says what the process
/// is doing if it is still alive near that limit. It never suspends the
/// process or changes how the exit ends.</summary>
public sealed class ExitWatchTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"exit-watch-{Guid.NewGuid():N}");

    /// <summary>The fake's exit handlers, standing in for the runtime's.</summary>
    private EventHandler? _exitHandlers;

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private ExitWatch Watch(TimeSpan noteAfter, string? folder = null) =>
        new(folder ?? _folder, noteAfter, () => _exitHandlers, Replace);

    private bool Replace(EventHandler? expected, EventHandler replacement) =>
        ExitWatch.ReplaceIfUnchanged(ref _exitHandlers, expected, replacement);

    private string LogPath => Path.Combine(_folder, ExitWatch.LogName);

    private string[] Log() => File.ReadAllLines(LogPath);

    private void Exit() => _exitHandlers?.Invoke(null, EventArgs.Empty);

    private static Thread Watcher(ExitWatch watch)
    {
        Thread watcher = Assert.IsType<Thread>(watch.Watcher);
        Assert.True(watcher.IsBackground, "the watch would hold the exit it watches");
        return watcher;
    }

    /// <summary>Codex round 4: the log reads the same in every culture, so
    /// the duration facts run where a decimal separator is a comma.</summary>
    private static void InGerman(Action body)
    {
        CultureInfo culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
        try
        {
            body();
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }
    /// <summary>Codex: handlers registered before the watch (the module
    /// initializers' clean-ups) run after its first mark, so a stall in one
    /// reads as a stall in the exit handlers, not before them.</summary>
    [Fact]
    public void TheWatchsMarksSurroundEveryExitHandlerAlreadyRegistered() => InGerman(() =>
    {
        bool beganFirst = false;
        _exitHandlers += (_, _) => beganFirst = File.ReadAllText(LogPath).Contains(" the process exit began", StringComparison.Ordinal);
        ExitWatch watch = Watch(Timeout.InfiniteTimeSpan);

        watch.Dispose();
        Exit();

        Assert.True(beganFirst, "an earlier exit handler ran before the watch's first mark");
        string[] log = Log();
        Assert.EndsWith(" the run started", log[0], StringComparison.Ordinal);
        Assert.EndsWith(" the last test finished", log[1], StringComparison.Ordinal);
        Assert.Contains(" 1 exit handlers are registered: ", log[2], StringComparison.Ordinal);
        Assert.EndsWith(" the process exit began: the watch's first exit handler ran", log[3], StringComparison.Ordinal);
        Assert.Matches(@" the exit handlers ran: 1 in \d+\.\d{3} s$", log[^1]);
        // Each line opens with the UTC time, to set beside the CI log.
        Assert.All(log, line => Assert.True(
            DateTimeOffset.TryParse(line[..line.IndexOf(' ', StringComparison.Ordinal)], out _),
            line));
    });

    /// <summary>A slow exit handler is named, with how long it took.</summary>
    [Fact]
    public void ASlowExitHandlerIsNamedWithHowLongItTook() => InGerman(() =>
    {
        _exitHandlers += (_, _) => { };
        _exitHandlers += SlowCleanUp;
        ExitWatch watch = Watch(Timeout.InfiniteTimeSpan);

        watch.Dispose();
        Exit();

        string slow = Assert.Single(Log(), line => line.Contains(" took ", StringComparison.Ordinal));
        Assert.Matches(@" exit handler 2 of 2 \(SlateWindows\.Tests\.ExitWatchTests\.SlowCleanUp\) took \d+\.\d{3} s$", slow);
    });

    private static void SlowCleanUp(object? sender, EventArgs e) =>
        Thread.Sleep(ExitWatch.SlowHandler + TimeSpan.FromMilliseconds(50));

    /// <summary>The wrapping changes nothing an exit handler does: what it
    /// throws reaches the runtime unchanged.</summary>
    [Fact]
    public void AnExitHandlersExceptionIsUnchanged()
    {
        var thrown = new InvalidOperationException("clean-up failed");
        _exitHandlers += (_, _) => throw thrown;
        ExitWatch watch = Watch(Timeout.InfiniteTimeSpan);

        watch.Dispose();

        Assert.Same(thrown, Assert.Throws<InvalidOperationException>(Exit));
    }

    /// <summary>A process still alive with no exit handler started is held
    /// before its exit handlers: a foreground thread, or the runner.</summary>
    [Fact]
    public void StillAliveBeforeAnyExitHandlerSaysSo()
    {
        ExitWatch watch = Watch(TimeSpan.Zero);

        watch.Dispose();

        Assert.True(Watcher(watch).Join(TimeSpan.FromSeconds(10)), "the watch never ended");
        Assert.Contains(" still alive 0 s after the last test, and no exit handler has started", Log()[^1], StringComparison.Ordinal);
    }

    /// <summary>A process still alive inside an exit handler names it.</summary>
    [Fact]
    public void StillAliveInsideAnExitHandlerNamesIt()
    {
        using var entered = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        _exitHandlers += (_, _) => { };
        _exitHandlers += (_, _) =>
        {
            entered.Set();
            release.Wait(TimeSpan.FromSeconds(30));
        };
        ExitWatch watch = Watch(TimeSpan.FromMilliseconds(300));
        watch.Dispose();
        var exiting = new Thread(Exit) { IsBackground = true };
        exiting.Start();
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10)), "the exit handler never ran");
            Assert.True(Watcher(watch).Join(TimeSpan.FromSeconds(10)), "the watch never ended");
        }
        finally
        {
            release.Set();
            Assert.True(exiting.Join(TimeSpan.FromSeconds(10)), "the exit never ended");
        }

        Assert.Single(Log(), line => line.Contains(" still alive 0 s after the last test, inside exit handler 2 of 2 (", StringComparison.Ordinal));
    }

    /// <summary>With no results folder named, the watch changes nothing.</summary>
    [Fact]
    public void WithoutAFolderTheWatchChangesNothing()
    {
        EventHandler original = (_, _) => { };
        _exitHandlers = original;
        var watch = new ExitWatch(null, TimeSpan.Zero, () => _exitHandlers, Replace);

        watch.Dispose();

        Assert.Same(original, _exitHandlers);
        Assert.Null(watch.Watcher);
        Assert.False(Directory.Exists(_folder));
    }

    /// <summary>Codex round 2: a handler registered between the watch's read
    /// and its swap is kept. The swap is a compare-and-swap, as the runtime's
    /// own <c>+=</c> is, and the list is left as it was.</summary>
    [Fact]
    public void AHandlerRegisteredWhileTheWatchReadsIsKept()
    {
        EventHandler original = (_, _) => { };
        EventHandler late = (_, _) => { };
        _exitHandlers = original;
        var watch = new ExitWatch(
            _folder,
            Timeout.InfiniteTimeSpan,
            () =>
            {
                EventHandler? read = _exitHandlers;
                _exitHandlers += late;
                return read;
            },
            Replace);

        watch.Dispose();

        Assert.Equal(new Delegate[] { original, late }, Assert.IsType<EventHandler>(_exitHandlers).GetInvocationList());
        Assert.EndsWith(" the exit handlers were not wrapped, and were left as they were: they changed while the watch read them, or this runtime keeps them elsewhere", Log()[^1], StringComparison.Ordinal);
    }

    /// <summary>The compare-and-swap replaces only the list it was given.</summary>
    [Fact]
    public void TheSwapReplacesOnlyTheListItWasGiven()
    {
        EventHandler first = (_, _) => { };
        EventHandler second = (_, _) => { };
        EventHandler replacement = (_, _) => { };
        EventHandler? field = first;

        Assert.False(ExitWatch.ReplaceIfUnchanged(ref field, second, replacement));
        Assert.Same(first, field);
        Assert.True(ExitWatch.ReplaceIfUnchanged(ref field, first, replacement));
        Assert.Same(replacement, field);
    }

    /// <summary>Codex round 2: with the handlers left unwrapped, the watch
    /// cannot see the exit's phase, and says so instead of blaming a
    /// foreground thread.</summary>
    [Fact]
    public void StillAliveWithTheHandlersUnwrappedSaysThePhaseIsUnknown()
    {
        _exitHandlers = (_, _) => { };
        var watch = new ExitWatch(_folder, TimeSpan.Zero, () => _exitHandlers, (_, _) => false);

        watch.Dispose();

        Assert.True(Watcher(watch).Join(TimeSpan.FromSeconds(10)), "the watch never ended");
        string late = Log()[^1];
        Assert.Contains(" still alive 0 s after the last test, and the exit handlers were not wrapped, so where the exit stands is unknown", late, StringComparison.Ordinal);
        Assert.DoesNotContain("foreground", late, StringComparison.Ordinal);
    }

    /// <summary>This runtime's exit handlers are readable where the watch
    /// reads them, and the module initializers' clean-ups are among them.
    /// Read only: no fact replaces the real handlers.</summary>
    [Fact]
    public void TheRuntimesExitHandlersAreReadable()
    {
        Assert.True(ExitWatch.ProcessExitHandlersReadable, "AppDomain's ProcessExit backing field moved");
        EventHandler handlers = Assert.IsType<EventHandler>(ExitWatch.ReadProcessExit(), exactMatch: false);
        // ScratchTrashBin registers its clean-up on every load;
        // DeviceStateForTests skips its own under a preset state root.
        Assert.Contains(
            handlers.GetInvocationList(),
            handler => ExitWatch.NameOf(handler).Contains(nameof(ScratchTrashBin), StringComparison.Ordinal));
    }

    /// <summary>The suite runs the watch as its assembly fixture, writing to
    /// the folder CI uploads (<c>SLATE_STA_DUMP_DIR</c>), with its late note
    /// before xUnit forces the exit 11 s after its entry point returns.</summary>
    [Fact]
    public void TheSuiteRunsTheWatchAsItsAssemblyFixtureBeforeXunitsLimit()
    {
        Assert.Contains(
            typeof(ExitWatch).Assembly.GetCustomAttributes<AssemblyFixtureAttribute>(),
            fixture => fixture.AssemblyFixtureType == typeof(ExitWatch));
        Assert.Equal(TimeSpan.FromSeconds(9), ExitWatch.NoteAfter);
    }
}
