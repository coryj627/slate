// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Reflection;

namespace SlateWindows.Tests;

/// <summary>#1826: the watch over the test process's own exit. xUnit ends a
/// run whose process is still alive 11 s after its entry point returns,
/// which the VSTest adapter reports as a bare
/// <c>[FATAL ERROR] Xunit.Sdk.TestPipelineException</c>. The watch records
/// when the run ended and when its exit handlers ran, and dumps a process
/// that is still alive near that limit.</summary>
public sealed class ExitWatchTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"exit-watch-{Guid.NewGuid():N}");
    private readonly List<EventHandler> _exitHandlers = [];

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private ExitWatch Watch(TimeSpan dumpAfter, Func<string, string> dump) =>
        new(_folder, dumpAfter, dump, _exitHandlers.Add);

    private string[] Log() => File.ReadAllLines(Path.Combine(_folder, ExitWatch.LogName));

    private void RunExitHandlers()
    {
        foreach (EventHandler handler in _exitHandlers)
        {
            handler(null, EventArgs.Empty);
        }
    }

    /// <summary>A foreground thread holds the exit before any exit handler
    /// runs; slow exit handlers sit between the first one and those
    /// registered during the run. The log tells the two apart.</summary>
    [Fact]
    public void TheLogRecordsTheRunsEndAndBothEndsOfItsExitHandlers()
    {
        ExitWatch watch = Watch(Timeout.InfiniteTimeSpan, _ => string.Empty);
        Assert.Single(_exitHandlers);

        watch.Dispose();
        Assert.Equal(2, _exitHandlers.Count);
        RunExitHandlers();

        string[] log = Log();
        Assert.Collection(
            log,
            line => Assert.EndsWith(" the run started", line, StringComparison.Ordinal),
            line => Assert.EndsWith(" the last test finished", line, StringComparison.Ordinal),
            line => Assert.EndsWith(" the process exit began: its first exit handler ran", line, StringComparison.Ordinal),
            line => Assert.EndsWith(" the exit handlers registered during the run have run", line, StringComparison.Ordinal));
        // Each line opens with the UTC time, to set beside the CI log.
        Assert.All(log, line => Assert.True(
            DateTimeOffset.TryParse(line[..line.IndexOf(' ', StringComparison.Ordinal)], out _),
            line));
    }

    /// <summary>A process still alive after the wait is dumped from a
    /// background thread, which can never hold the exit itself.</summary>
    [Fact]
    public void AProcessStillAliveAfterTheWaitIsDumpedFromABackgroundThread()
    {
        using var dumped = new ManualResetEventSlim(false);
        string? name = null;
        ExitWatch watch = Watch(TimeSpan.FromMilliseconds(50), dumpName =>
        {
            name = dumpName;
            dumped.Set();
            return " A full dump of the test process is at X.dmp (1 bytes).";
        });

        watch.Dispose();

        Thread watcher = Assert.IsType<Thread>(watch.Watcher);
        Assert.True(watcher.IsBackground, "the watch would hold the exit it watches");
        Assert.True(dumped.Wait(TimeSpan.FromSeconds(10)), "the watch never dumped");
        Assert.True(watcher.Join(TimeSpan.FromSeconds(10)), "the watch never ended");
        Assert.Equal("exit-watch", name);
        string[] log = Log();
        Assert.Contains(log, line => line.EndsWith(" still alive 0 s after the last test; writing a dump", StringComparison.Ordinal));
        Assert.EndsWith(" A full dump of the test process is at X.dmp (1 bytes).", log[^1], StringComparison.Ordinal);
    }

    /// <summary>A dump that was not written says so in the log.</summary>
    [Fact]
    public void ADumpNotWrittenIsRecordedAsNotWritten()
    {
        ExitWatch watch = Watch(TimeSpan.Zero, _ => string.Empty);

        watch.Dispose();

        Assert.True(Assert.IsType<Thread>(watch.Watcher).Join(TimeSpan.FromSeconds(10)), "the watch never ended");
        Assert.EndsWith(" no dump was written", Log()[^1], StringComparison.Ordinal);
    }

    /// <summary>With no results folder named, the watch writes nothing.</summary>
    [Fact]
    public void WithoutAFolderTheWatchWritesNothing()
    {
        var watch = new ExitWatch(null, TimeSpan.Zero, _ => string.Empty, _exitHandlers.Add);

        watch.Dispose();
        Assert.True(Assert.IsType<Thread>(watch.Watcher).Join(TimeSpan.FromSeconds(10)), "the watch never ended");
        RunExitHandlers();

        Assert.False(Directory.Exists(_folder));
    }

    /// <summary>The suite runs the watch as its assembly fixture, writing to
    /// the folder CI uploads (<c>SLATE_STA_DUMP_DIR</c>), and it waits less
    /// than xUnit's 11 s: a process alive at 9 s fails the run anyway, so the
    /// dump never costs a run that would have passed.</summary>
    [Fact]
    public void TheSuiteRunsTheWatchAsItsAssemblyFixtureBeforeXunitsLimit()
    {
        Assert.Contains(
            typeof(ExitWatch).Assembly.GetCustomAttributes<AssemblyFixtureAttribute>(),
            fixture => fixture.AssemblyFixtureType == typeof(ExitWatch));
        Assert.Equal(TimeSpan.FromSeconds(9), ExitWatch.DumpAfter);
        Assert.True(ExitWatch.DumpAfter < TimeSpan.FromSeconds(11));
    }

    /// <summary>The dump is StaThread's: the runtime's createdump, a full
    /// dump, in StaThread's folder and under its cap.</summary>
    [Fact]
    public void TheWatchsDumpIsStaThreadsCreatedumpDump()
    {
        string? folder = StaThread.DumpDirectory;
        Directory.CreateDirectory(_folder);
        StaThread.DumpDirectory = _folder;
        StaThread.ResetDumpsForTests();
        System.Diagnostics.ProcessStartInfo? started = null;
        StaThread.RunDumperForTests = start =>
        {
            started = start;
            File.WriteAllBytes(start.ArgumentList[2], "MDMP"u8.ToArray());
            return (0, string.Empty);
        };
        try
        {
            string said = StaThread.DumpForExitWatch("exit-watch");

            Assert.NotNull(started);
            string dump = Assert.Single(Directory.GetFiles(_folder, "exit-watch-*.dmp"));
            Assert.Equal(new[] { "--full", "--name", dump }, started.ArgumentList);
            Assert.Contains($"A full dump of the test process is at {dump} (", said, StringComparison.Ordinal);
        }
        finally
        {
            StaThread.RunDumperForTests = null;
            StaThread.DumpDirectory = folder;
            StaThread.ResetDumpsForTests();
        }
    }
}
