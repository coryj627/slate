// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;
using System.Runtime.CompilerServices;
using static System.FormattableString;

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
/// then. Two things hold an exit that long:
/// <list type="bullet">
/// <item>a live foreground thread, which runs before any exit handler;</item>
/// <item>slow <c>ProcessExit</c> handlers, such as #1555's WPF weak-event
/// tables.</item>
/// </list>
/// </para>
/// <para>
/// The watch tells them apart, and logs into the folder CI uploads
/// (<see cref="StaThread.DumpDirectory"/>, <c>SLATE_STA_DUMP_DIR</c>).
/// After the last test, it rewrites the process's exit handlers in their
/// order:
/// <list type="bullet">
/// <item>its own mark first, before the handlers the module initializers
/// registered;</item>
/// <item>each existing handler, timed, so a slow one is named;</item>
/// <item>its own mark last.</item>
/// </list>
/// It also counts the handlers by kind. <see cref="NoteAfter"/> after the
/// last test, a background thread notes whether the process is still alive,
/// and in which handler, if any.
/// </para>
/// <para>
/// The watch never suspends the process. A full dump would, for about 12.5 s
/// on CI (StaThreadTests), which could push a passing exit past xUnit's
/// limit. A wrapped handler runs and throws exactly as before.
/// </para>
/// </remarks>
public sealed class ExitWatch : IDisposable
{
    /// <summary>The log's name in the results folder.</summary>
    internal const string LogName = "exit-watch.log";

    /// <summary>How long after the last test the watch notes a process still
    /// alive. xUnit forces the exit 11 s after its entry point returns, which
    /// is after the last test.</summary>
    internal static readonly TimeSpan NoteAfter = TimeSpan.FromSeconds(9);

    /// <summary>An exit handler at least this slow is named in the log.</summary>
    internal static readonly TimeSpan SlowHandler = TimeSpan.FromMilliseconds(100);

    /// <summary>The backing field of <see cref="AppDomain.ProcessExit"/>,
    /// which every <c>ProcessExit +=</c> in the process adds to, by
    /// reference, so the watch's swap is the same compare-and-swap.</summary>
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = nameof(AppDomain.ProcessExit))]
    private static extern ref EventHandler? ProcessExitOf(AppDomain domain);

    private static readonly Lazy<bool> s_readable = new(() =>
    {
        try
        {
            _ = Volatile.Read(ref ProcessExitOf(AppDomain.CurrentDomain));
            return true;
        }
        catch (MissingFieldException)
        {
            return false;
        }
    });

    private readonly string? _folder;
    private readonly TimeSpan _noteAfter;
    private readonly Func<EventHandler?> _read;
    private readonly Func<EventHandler?, EventHandler, bool> _replace;
    private readonly object _gate = new();
    private int _handlerCount;
    private volatile bool _wrapped;

    /// <summary>Each kind's handlers run so far, and their summed stopwatch
    /// ticks.</summary>
    private readonly Dictionary<string, (int Count, long Ticks)> _kinds = new(StringComparer.Ordinal);
    private long _exitBegan;
    private string? _running;
    private long _runningSince;

    public ExitWatch()
        : this(StaThread.DumpDirectory, NoteAfter, ReadProcessExit, ReplaceProcessExit)
    {
    }

    internal ExitWatch(
        string? folder,
        TimeSpan noteAfter,
        Func<EventHandler?> read,
        Func<EventHandler?, EventHandler, bool> replace)
    {
        _folder = folder is { Length: > 0 } ? folder : null;
        _noteAfter = noteAfter;
        _read = read;
        _replace = replace;
        Note("the run started");
    }

    /// <summary>Whether this runtime keeps its exit handlers where the watch
    /// reads them.</summary>
    internal static bool ProcessExitHandlersReadable => s_readable.Value;

    /// <summary>The thread that notes a process still alive; null until the
    /// last test finished, with no folder, and with no late note asked for.</summary>
    internal Thread? Watcher { get; private set; }

    internal static EventHandler? ReadProcessExit() =>
        ProcessExitHandlersReadable ? Volatile.Read(ref ProcessExitOf(AppDomain.CurrentDomain)) : null;

    /// <summary>Replaces the exit handlers, unless they changed since
    /// <paramref name="expected"/> was read.</summary>
    private static bool ReplaceProcessExit(EventHandler? expected, EventHandler replacement) =>
        ProcessExitHandlersReadable
        && ReplaceIfUnchanged(ref ProcessExitOf(AppDomain.CurrentDomain), expected, replacement);

    /// <summary>Puts <paramref name="replacement"/> in <paramref name="field"/>
    /// only if it still holds <paramref name="expected"/>: a compare-and-swap,
    /// as the runtime's own <c>ProcessExit +=</c> and <c>-=</c> are, so a
    /// registration or removal made since the read is never lost.</summary>
    internal static bool ReplaceIfUnchanged(ref EventHandler? field, EventHandler? expected, EventHandler replacement) =>
        ReferenceEquals(Interlocked.CompareExchange(ref field, replacement, expected), expected);

    /// <summary>A handler's name: the type of the object it runs on (WPF's
    /// shut-down listeners differ only there), else its method's type, and
    /// its method.</summary>
    internal static string NameOf(Delegate handler) =>
        $"{(handler.Target?.GetType() ?? handler.Method.DeclaringType)?.FullName}.{handler.Method.Name}";

    /// <summary>The run's last test has finished.</summary>
    public void Dispose()
    {
        if (_folder is null)
        {
            return;
        }
        Note("the last test finished");
        WrapExitHandlers();
        if (_noteAfter == Timeout.InfiniteTimeSpan)
        {
            // No late note asked for: no thread sleeps forever.
            return;
        }
        Watcher = new Thread(WatchTheExit)
        {
            // A foreground watch would hold the very exit it watches.
            IsBackground = true,
            Name = "Slate exit watch",
        };
        Watcher.Start();
    }

    private void WrapExitHandlers()
    {
        EventHandler? current = _read();
        Delegate[] handlers = current?.GetInvocationList() ?? [];
        _handlerCount = handlers.Length;
        string kinds = string.Join(
            ", ",
            handlers.GroupBy(NameOf)
                .OrderByDescending(kind => kind.Count())
                .ThenBy(kind => kind.Key, StringComparer.Ordinal)
                .Select(kind => Invariant($"{kind.Count()} x {kind.Key}")));
        Note(Invariant($"{handlers.Length} exit handlers are registered: {(kinds.Length > 0 ? kinds : "none")}"));
        EventHandler wrapped = ExitBegan;
        for (int index = 0; index < handlers.Length; index++)
        {
            wrapped += Timed((EventHandler)handlers[index], index + 1);
        }
        wrapped += ExitHandlersRan;
        _wrapped = _replace(current, wrapped);
        if (!_wrapped)
        {
            Note("the exit handlers were not wrapped, and were left as they were: they changed while the watch read them, or this runtime keeps them elsewhere");
        }
    }

    private void ExitBegan(object? sender, EventArgs e)
    {
        Volatile.Write(ref _exitBegan, Stopwatch.GetTimestamp());
        Note("the process exit began: the watch's first exit handler ran");
    }

    /// <summary>The closing line totals each kind's time, slowest kind first:
    /// a CI run spent 5.56 s in 4,674 handlers, nearly all of them under
    /// <see cref="SlowHandler"/> (#1826).</summary>
    private void ExitHandlersRan(object? sender, EventArgs e)
    {
        string kinds;
        lock (_kinds)
        {
            kinds = string.Join(
                ", ",
                _kinds.OrderByDescending(kind => kind.Value.Ticks)
                    .ThenBy(kind => kind.Key, StringComparer.Ordinal)
                    .Select(kind => Invariant($"{kind.Value.Count} x {kind.Key} {Stopwatch.GetElapsedTime(0, kind.Value.Ticks).TotalSeconds:0.000} s")));
        }
        Note(Invariant($"the exit handlers ran: {_handlerCount} in {Stopwatch.GetElapsedTime(Volatile.Read(ref _exitBegan)).TotalSeconds:0.000} s; by kind: {kinds}"));
    }

    private EventHandler Timed(EventHandler handler, int position)
    {
        string kind = NameOf(handler);
        string name = Invariant($"exit handler {position} of {_handlerCount} ({kind})");
        return (sender, e) =>
        {
            long started = Stopwatch.GetTimestamp();
            Volatile.Write(ref _runningSince, started);
            Volatile.Write(ref _running, name);
            try
            {
                handler(sender, e);
            }
            finally
            {
                Volatile.Write(ref _running, null);
                long ended = Stopwatch.GetTimestamp();
                lock (_kinds)
                {
                    _kinds[kind] = _kinds.TryGetValue(kind, out (int Count, long Ticks) sum)
                        ? (sum.Count + 1, sum.Ticks + (ended - started))
                        : (1, ended - started);
                }
                TimeSpan took = Stopwatch.GetElapsedTime(started, ended);
                if (took >= SlowHandler)
                {
                    Note(Invariant($"{name} took {took.TotalSeconds:0.000} s"));
                }
            }
        };
    }

    private void WatchTheExit()
    {
        Thread.Sleep(_noteAfter);
        string alive = Invariant($"still alive {_noteAfter.TotalSeconds:0} s after the last test");
        int threads;
        using (Process self = Process.GetCurrentProcess())
        {
            threads = self.Threads.Count;
        }
        if (!_wrapped)
        {
            // No mark is in place: the exit's phase cannot be seen.
            Note(Invariant($"{alive}, and the exit handlers were not wrapped, so where the exit stands is unknown ({threads} threads)"));
        }
        else if (Volatile.Read(ref _exitBegan) == 0)
        {
            Note(Invariant($"{alive}, and no exit handler has started: a foreground thread or the runner holds the exit ({threads} threads)"));
        }
        else if (Volatile.Read(ref _running) is { } running)
        {
            TimeSpan since = Stopwatch.GetElapsedTime(Volatile.Read(ref _runningSince));
            Note(Invariant($"{alive}, inside {running}, for {since.TotalSeconds:0.000} s ({threads} threads)"));
        }
        else
        {
            Note(Invariant($"{alive}, between exit handlers ({threads} threads)"));
        }
    }

    private void Note(string what)
    {
        if (_folder is not { } folder)
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
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // A diagnostic never changes how the run ends: the marks run
            // inside the exit handlers, where a throw would cut the rest
            // short. A folder that is not a path throws ArgumentException.
        }
    }
}
