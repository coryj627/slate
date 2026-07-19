// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Shared recording listeners for the §W-E censuses. Ported from the W0-1
// probe (examples/csharp-probe, retired by W0-3) so the census evidence
// keeps the spike's instrumentation: thread-safe capture, a concurrency
// gauge, and the callback-thread census.

using uniffi.slate_uniffi;

namespace SlateWindows.Tests.Support;

/// <summary>
/// Recording <see cref="ScanProgressListener"/>: thread-safe event capture
/// plus a concurrency gauge and the set of managed thread ids the
/// callbacks arrived on.
/// </summary>
internal sealed class ProgressRecorder : ScanProgressListener
{
    private readonly object _lock = new();
    private readonly List<ScanProgress> _events = new();
    public readonly ConcurrencyGauge Gauge = new();
    public readonly HashSet<int> ThreadIds = new();
    public Action<ScanProgress>? OnEvent;

    public void OnProgress(ScanProgress @event)
    {
        using var _ = Gauge.Enter();
        lock (_lock)
        {
            _events.Add(@event);
            ThreadIds.Add(Environment.CurrentManagedThreadId);
        }
        OnEvent?.Invoke(@event);
    }

    public List<ScanProgress> Snapshot()
    {
        lock (_lock)
        {
            return new List<ScanProgress>(_events);
        }
    }
}

/// <summary>
/// Recording <see cref="VaultEventListener"/> capturing all three event
/// kinds.
/// </summary>
internal sealed class EventRecorder : VaultEventListener
{
    private readonly object _lock = new();
    public readonly List<(EventErrorCode Code, string Path, string Message)> Errors = new();
    public readonly List<FileChangeEvent> FileChanges = new();
    public readonly List<(IndexPhase Phase, ulong FilesSeen)> IndexPhases = new();
    public readonly ConcurrencyGauge Gauge = new();
    public readonly HashSet<int> ThreadIds = new();

    public int TotalCount
    {
        get
        {
            lock (_lock)
            {
                return Errors.Count + FileChanges.Count + IndexPhases.Count;
            }
        }
    }

    public void OnError(EventErrorCode @code, string @path, string @message)
    {
        using var _ = Gauge.Enter();
        lock (_lock)
        {
            Errors.Add((@code, @path, @message));
            ThreadIds.Add(Environment.CurrentManagedThreadId);
        }
    }

    public void OnFileChange(FileChangeEvent @event)
    {
        using var _ = Gauge.Enter();
        lock (_lock)
        {
            FileChanges.Add(@event);
            ThreadIds.Add(Environment.CurrentManagedThreadId);
        }
    }

    public void OnIndexPhase(IndexPhase @phase, ulong @filesSeen)
    {
        using var _ = Gauge.Enter();
        lock (_lock)
        {
            IndexPhases.Add((@phase, @filesSeen));
            ThreadIds.Add(Environment.CurrentManagedThreadId);
        }
    }

    public T Locked<T>(Func<EventRecorder, T> read)
    {
        lock (_lock)
        {
            return read(this);
        }
    }
}

/// <summary>
/// Foreign <see cref="CommandAction"/> with a scriptable body — success,
/// typed failure, arbitrary .NET exception, or registry re-entry.
/// </summary>
internal sealed class ScriptedAction : CommandAction
{
    private readonly Action _body;
    public int InvocationCount;

    public ScriptedAction(Action body)
    {
        _body = body;
    }

    public void Invoke()
    {
        Interlocked.Increment(ref InvocationCount);
        _body();
    }
}

/// <summary>
/// Tracks how many scopes are open concurrently and the high-water mark.
/// </summary>
internal sealed class ConcurrencyGauge
{
    private int _current;
    private int _max;

    public int Max => Volatile.Read(ref _max);

    public Scope Enter() => new(this);

    internal readonly struct Scope : IDisposable
    {
        private readonly ConcurrencyGauge _gauge;

        public Scope(ConcurrencyGauge gauge)
        {
            _gauge = gauge;
            int now = Interlocked.Increment(ref gauge._current);
            int seen;
            while (now > (seen = Volatile.Read(ref gauge._max)))
            {
                Interlocked.CompareExchange(ref gauge._max, now, seen);
            }
        }

        public void Dispose() => Interlocked.Decrement(ref _gauge._current);
    }
}

internal static class Waiting
{
    /// <summary>Poll <paramref name="condition"/> until true or timeout.</summary>
    public static bool WaitFor(Func<bool> condition, int timeoutMs)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            if (condition())
            {
                return true;
            }
            Thread.Sleep(25);
        }
        return condition();
    }
}

/// <summary>
/// Census sizing: moderate defaults for the per-PR lane, full tier under
/// SLATE_CENSUS_FULL=1 (repo census convention — nightly runs the full
/// tier).
/// </summary>
internal static class CensusTier
{
    public static bool Full { get; } =
        Environment.GetEnvironmentVariable("SLATE_CENSUS_FULL") == "1";

    public static int Scale(int prSize, int fullSize) => Full ? fullSize : prSize;
}
