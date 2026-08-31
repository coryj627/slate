// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Canvas;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>An in-memory source: handles are integers, the close
/// observation is a counter, and each FFI call can be told to throw.
/// The order of calls is logged so a fact can assert "closed before
/// opened" rather than only "closed".</summary>
internal sealed class CanvasFakeLoadSource : ICanvasLoadSource
{
    internal const string ParseErrorMessage = "not a canvas";
    private readonly object _gate = new();
    private readonly List<string> _log = [];
    private ulong _next;
    private int _opens;
    private int _closes;

    internal string[] Rows { get; set; } = ["a", "b"];

    internal Dictionary<string, string> Subpaths { get; } = new(StringComparer.Ordinal);

    internal bool Degraded { get; init; }

    internal Exception? OpenFault { get; set; }

    internal Exception? ReadFault { get; init; }

    internal Exception? CloseFault { get; set; }

    internal int Opens => Volatile.Read(ref _opens);

    internal int TotalCloses => Volatile.Read(ref _closes);

    internal string Trace
    {
        get
        {
            lock (_gate)
            {
                return string.Join(" ", _log);
            }
        }
    }

    internal int IndexOf(string op, int occurrence)
    {
        lock (_gate)
        {
            var seen = 0;
            for (var i = 0; i < _log.Count; i++)
            {
                if (_log[i].StartsWith(op, StringComparison.Ordinal) && seen++ == occurrence)
                {
                    return i;
                }
            }

            return -1;
        }
    }

    public CanvasOpenInfo Open()
    {
        if (OpenFault is { } fault)
        {
            throw fault;
        }

        ulong handle = Interlocked.Increment(ref _next);
        _ = Interlocked.Increment(ref _opens);
        Record($"open:{handle}");
        return new CanvasOpenInfo(handle, (uint)Rows.Length, 0, Degraded, []);
    }

    public void Close(ulong handle)
    {
        _ = Interlocked.Increment(ref _closes);
        Record($"close:{handle}");
        if (CloseFault is { } fault)
        {
            throw fault;
        }
    }

    public CanvasOutlineRow[] Outline(ulong handle)
    {
        if (ReadFault is { } fault)
        {
            throw fault;
        }

        Record($"outline:{handle}");
        return [.. Rows.Select((id, i) => new CanvasOutlineRow(
            id, 0, "text", id, id, [], (uint)(i + 1), (uint)Rows.Length, 0, null))];
    }

    public CanvasTableRow[] TableRows(ulong handle) =>
        [.. Rows.Select(id => new CanvasTableRow(id, "text", id, id, [], string.Empty, 0, null))];

    public CanvasScene Scene(ulong handle) =>
        new(
            [.. Subpaths.Select(pair => new CanvasSceneNode(
                pair.Key, "file", pair.Key, pair.Key, 0, 0, 0, 0, null, null, pair.Value))],
            []);

    public CanvasLoadFailure FailureFor(Exception exception) =>
        new(CanvasLoadState.Failed, $"failed: {exception.Message}");

    public CanvasLoadFailure ParseError(IReadOnlyList<CanvasLoadWarning> warnings) =>
        new(CanvasLoadState.ParseError, ParseErrorMessage);

    private void Record(string entry)
    {
        lock (_gate)
        {
            _log.Add(entry);
        }
    }
}
