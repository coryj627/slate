// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using uniffi.slate_uniffi;

namespace SlateProbe;

/// <summary>
/// Recording ScanProgressListener: thread-safe event capture plus a
/// concurrency gauge (how many callbacks were in flight at once) and the
/// set of managed thread ids the callbacks arrived on.
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
        lock (_lock) return new List<ScanProgress>(_events);
    }
}

/// <summary>
/// Recording VaultEventListener capturing all three event kinds.
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
        get { lock (_lock) return Errors.Count + FileChanges.Count + IndexPhases.Count; }
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
        lock (_lock) return read(this);
    }
}

/// <summary>
/// Foreign CommandAction with a scriptable body — success, typed failure,
/// arbitrary .NET exception, or registry re-entry.
/// </summary>
internal sealed class ProbeAction : CommandAction
{
    private readonly Action _body;
    public int InvocationCount;

    public ProbeAction(Action body) => _body = body;

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
