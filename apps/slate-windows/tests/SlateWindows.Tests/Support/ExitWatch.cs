// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace SlateWindows.Tests;

/// <summary>
/// #1826: the suite's assembly fixture, a watch over the test process's own
/// exit.
/// </summary>
/// <remarks>
/// <para>
/// xUnit v3's in-process runner (<c>ConsoleRunner.Run</c>) waits 1 s after
/// its entry point returns, then 10 s more. If the process is still alive,
/// it reports <c>TestPipelineException("Foreground threads were left
/// running, forcing process exit")</c> and calls <c>Environment.Exit(1)</c>.
/// Through the VSTest adapter, CI's log shows only
/// <c>[FATAL ERROR] Xunit.Sdk.TestPipelineException</c>, since the adapter
/// writes the message at informational level. Every test has passed by
/// then. A live foreground thread holds the exit there, and so do slow
/// <c>ProcessExit</c> handlers (#1555: WPF's weak-event tables).
/// </para>
/// <para>
/// The watch tells those apart. Into the folder CI uploads
/// (<see cref="StaThread.DumpDirectory"/>, <c>SLATE_STA_DUMP_DIR</c>), it
/// logs:
/// <list type="bullet">
/// <item>the end of the last test;</item>
/// <item>the first <c>ProcessExit</c> handler (registered when the run
/// starts);</item>
/// <item>the handler it registers after the last test, which runs after
/// every handler registered during the run.</item>
/// </list>
/// A process still alive <see cref="DumpAfter"/> after the last test is
/// dumped through <see cref="StaThread"/>'s createdump. It is close enough
/// to xUnit's limit that only a run already failing is dumped.
/// </para>
/// </remarks>
public sealed class ExitWatch : IDisposable
{
    /// <summary>The log's name in the results folder.</summary>
    internal const string LogName = "exit-watch.log";

    /// <summary>How long after the last test the watch waits before it
    /// dumps a process still alive. xUnit forces the exit 11 s after its
    /// entry point returns, which is after the last test.</summary>
    internal static readonly TimeSpan DumpAfter = TimeSpan.FromSeconds(9);

    private readonly string? _folder;
    private readonly TimeSpan _dumpAfter;
    private readonly Func<string, string> _dump;
    private readonly Action<EventHandler> _onProcessExit;
    private readonly object _gate = new();

    public ExitWatch()
        : this(
            StaThread.DumpDirectory,
            DumpAfter,
            StaThread.DumpForExitWatch,
            handler => AppDomain.CurrentDomain.ProcessExit += handler)
    {
    }

    internal ExitWatch(
        string? folder,
        TimeSpan dumpAfter,
        Func<string, string> dump,
        Action<EventHandler> onProcessExit)
    {
        _folder = folder;
        _dumpAfter = dumpAfter;
        _dump = dump;
        _onProcessExit = onProcessExit;
        Note("the run started");
        _onProcessExit((_, _) => Note("the process exit began: its first exit handler ran"));
    }

    /// <summary>The thread that dumps a process still alive; null until the
    /// last test finished.</summary>
    internal Thread? Watcher { get; private set; }

    /// <summary>The run's last test has finished.</summary>
    public void Dispose()
    {
        Note("the last test finished");
        _onProcessExit((_, _) => Note("the exit handlers registered during the run have run"));
        Watcher = new Thread(() =>
        {
            Thread.Sleep(_dumpAfter);
            // Written before the dump: xUnit's exit can follow it at once.
            Note($"still alive {_dumpAfter.TotalSeconds:0} s after the last test; writing a dump");
            string said = _dump("exit-watch").Trim();
            Note(said.Length > 0 ? said : "no dump was written");
        })
        {
            // A foreground watch would hold the very exit it watches.
            IsBackground = true,
            Name = "Slate exit watch",
        };
        Watcher.Start();
    }

    private void Note(string what)
    {
        if (_folder is not { Length: > 0 } folder)
        {
            return;
        }
        try
        {
            lock (_gate)
            {
                Directory.CreateDirectory(folder);
                File.AppendAllText(
                    Path.Combine(folder, LogName),
                    $"{DateTimeOffset.UtcNow:O} {what}{Environment.NewLine}");
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A diagnostic never changes how the run ends.
        }
    }
}
