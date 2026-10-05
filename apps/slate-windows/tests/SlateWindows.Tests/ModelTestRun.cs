// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace SlateWindows.Tests;

/// <summary>Explicit process sharding, separate from the local substring filter.
/// Invalid or partial CI configuration must fail instead of narrowing coverage.</summary>
internal sealed record ModelShardConfiguration(int Index, int Count, string[] Only, string? ReportDirectory)
{
    internal static ModelShardConfiguration FromEnvironment() => Parse(Environment.GetEnvironmentVariable);

    internal static ModelShardConfiguration Parse(Func<string, string?> environment)
    {
        string? indexText = environment("SLATE_MODEL_SHARD_INDEX");
        string? countText = environment("SLATE_MODEL_SHARD_COUNT");
        int index = 0;
        int count = 1;
        bool explicitShard = indexText is not null || countText is not null;
        // The composed family has 137 reachable cases. More shards would
        // create empty family reports rather than useful work.
        if (explicitShard
            && (!int.TryParse(indexText, NumberStyles.None, CultureInfo.InvariantCulture, out index)
                || !int.TryParse(countText, NumberStyles.None, CultureInfo.InvariantCulture, out count)
                || count < 1 || count > 137 || index < 0 || index >= count))
        {
            throw new InvalidOperationException("Set both SLATE_MODEL_SHARD_INDEX and SLATE_MODEL_SHARD_COUNT to integers with 0 <= index < count <= 137.");
        }

        string[] only = (environment("SLATE_MODEL_ONLY") ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string? reportDirectory = environment("SLATE_MODEL_REPORT_DIR");
        if (reportDirectory is not null && string.IsNullOrWhiteSpace(reportDirectory))
        {
            throw new InvalidOperationException("SLATE_MODEL_REPORT_DIR must name a directory when set.");
        }
        if (only.Length > 0 && (explicitShard || reportDirectory is not null))
        {
            throw new InvalidOperationException("SLATE_MODEL_ONLY is a local debugging filter and cannot be combined with sharding or reports.");
        }
        return new(index, count, only, reportDirectory);
    }
}

/// <summary>One model family, retaining its complete inventory on every shard.
/// Only execution is partitioned; each case keeps its global reachable ordinal
/// and runs on its caller's dispatcher with its own vault and session.</summary>
internal sealed class ModelTestRun<TCell> : IDisposable
{
    internal sealed record Case(TCell Value, int Ordinal, string Route, string Description);

    private sealed record InventoryEntry(string Cell, string Route, string? UnreachableReason);

    private readonly string _family;
    private readonly ModelShardConfiguration _configuration;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly List<int> _completed = [];
    private readonly HashSet<int> _started = [];
    private readonly Dictionary<int, Case> _selectedByOrdinal;
    private readonly Dictionary<string, RouteTiming> _routes = new(StringComparer.Ordinal);
    private readonly List<SlowCase> _slowestCases = [];
    private readonly object _progressGate = new();
    private readonly TimeSpan _progressInterval;
    private System.Threading.Timer? _progressTimer;
    private Checkpoint? _published;
    private Checkpoint? _written;
    private bool _progressStopped;
    private Case? _activeCase;
    private int _missedProgressWrites;
    private const int SlowCaseLimit = 16;
    private bool _inventoryVerified;
    private bool _success;
    private bool _disposed;

    /// <param name="progressInterval">How often the checkpoint flusher runs;
    /// one second unless a fact supplies another (an infinite interval leaves
    /// flushing to <see cref="FlushProgress"/>).</param>
    internal ModelTestRun(
        string family,
        IEnumerable<TCell> cells,
        Func<TCell, string> describe,
        Func<TCell, string> route,
        Func<TCell, string?> unreachable,
        ModelShardConfiguration configuration,
        TimeSpan? progressInterval = null)
    {
        _progressInterval = progressInterval ?? TimeSpan.FromSeconds(1);
        if (family is not ("routes" or "reroot" or "composed"))
        {
            throw new ArgumentException("Unknown model family.", nameof(family));
        }
        _family = family;
        _configuration = configuration;
        var inventory = new List<InventoryEntry>();
        var selected = new List<Case>();
        int reachable = 0;
        foreach (TCell cell in cells)
        {
            string description = describe(cell);
            string routeName = route(cell);
            string? reason = unreachable(cell);
            inventory.Add(new(description, routeName, reason));
            if (reason is not null)
            {
                continue;
            }
            int ordinal = ++reachable;
            if ((ordinal - 1) % configuration.Count == configuration.Index
                && configuration.Only.All(term => description.Contains(term, StringComparison.Ordinal)))
            {
                selected.Add(new(cell, ordinal, routeName, description));
            }
        }
        TotalCells = inventory.Count;
        ReachableCells = reachable;
        UnreachableCells = TotalCells - reachable;
        InventorySha256 = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(inventory)));
        SelectedCases = selected.AsReadOnly();
        _selectedByOrdinal = selected.ToDictionary(modelCase => modelCase.Ordinal);
    }

    internal int TotalCells { get; }
    internal int ReachableCells { get; }
    internal int UnreachableCells { get; }
    internal string InventorySha256 { get; }
    internal IReadOnlyList<Case> SelectedCases { get; }

    /// <summary>Pin the census and its digest. Counts alone let a renamed
    /// route or a reworded exclusion reason pass locally and on both shards,
    /// failing only later in the Linux aggregator, which pins the same
    /// digests independently (scripts/verify_windows_model_shards.py).</summary>
    internal void AssertInventory(int total, int unreachable, int reachable, string inventorySha256)
    {
        Assert.Equal(total, TotalCells);
        Assert.Equal(unreachable, UnreachableCells);
        Assert.Equal(reachable, ReachableCells);
        Assert.True(
            string.Equals(inventorySha256, InventorySha256, StringComparison.Ordinal),
            $"The {_family} inventory digest is {InventorySha256}, pinned {inventorySha256}: a cell, "
            + "route name or exclusion reason changed. Review the change, then update this pin and "
            + "INVENTORY_SHA256 in scripts/verify_windows_model_shards.py.");
        _inventoryVerified = true;
    }

    /// <summary>The body returns only after its using declarations dispose.
    /// A failed arrangement, thrown assertion, or cleanup never records a
    /// completed case. Timings still survive a failure for diagnosis.</summary>
    internal void RunCase(Case modelCase, Action<CaseTiming> body)
    {
        Assert.True(_inventoryVerified, "Verify the full model inventory before executing cases.");
        Assert.True(_selectedByOrdinal.TryGetValue(modelCase.Ordinal, out Case? selected) && selected == modelCase,
            $"Case {modelCase.Ordinal} does not belong to this shard.");
        Assert.True(_started.Add(modelCase.Ordinal), $"Case {modelCase.Ordinal} ran twice.");
        if (!_routes.TryGetValue(modelCase.Route, out RouteTiming? route))
        {
            route = new(modelCase.Route);
            _routes.Add(modelCase.Route, route);
        }
        _activeCase = modelCase;
        PublishProgress("fixtureSetup");
        var timing = new CaseTiming(route, PublishProgress);
        bool returned = false;
        try
        {
            body(timing);
            returned = true;
            if (timing.Completed)
            {
                _completed.Add(modelCase.Ordinal);
            }
        }
        finally
        {
            timing.Dispose();
            _slowestCases.Add(new(modelCase.Ordinal, modelCase.Description, modelCase.Route,
                returned && timing.Completed, timing.ElapsedMilliseconds,
                new Dictionary<string, double>(timing.Phases, StringComparer.Ordinal)));
            _slowestCases.Sort((left, right) =>
            {
                int elapsed = right.ElapsedMilliseconds.CompareTo(left.ElapsedMilliseconds);
                return elapsed != 0 ? elapsed : left.Ordinal.CompareTo(right.Ordinal);
            });
            if (_slowestCases.Count > SlowCaseLimit)
            {
                _slowestCases.RemoveAt(SlowCaseLimit);
            }
            bool completed = returned && timing.Completed;
            if (completed)
            {
                // Only a completed case stops being the active one. A case
                // that threw stays named, so the failed checkpoint Dispose
                // writes still attributes the failure to it.
                _activeCase = null;
            }
            PublishProgress(completed ? "caseCompleted" : "caseFailed");
        }
    }

    internal void Complete()
    {
        Assert.True(_inventoryVerified, "The full model inventory was not verified.");
        if (_configuration.Only.Length > 0)
        {
            Assert.NotEmpty(SelectedCases);
        }
        Assert.Equal(SelectedCases.Select(modelCase => modelCase.Ordinal), _completed);
        _success = true;
        _activeCase = null;
        PublishProgress("complete");
    }

    /// <summary>Record where the run is, with no I/O: a transition costs one
    /// small allocation, and no phase's timing includes a checkpoint write.
    /// Writing synchronously at every case start, phase and case end cost
    /// about 52,000 file replacements a shard (2.5-3.9% of its time), billed
    /// to the phase being entered.</summary>
    private void PublishProgress(string phase)
    {
        if (_configuration.ReportDirectory is null)
        {
            return;
        }
        Volatile.Write(ref _published, Snapshot(phase));
        _progressTimer ??= new System.Threading.Timer(
            _ => FlushProgress(), null, _progressInterval, _progressInterval);
    }

    private Checkpoint Snapshot(string phase) => new(
        phase, _activeCase?.Ordinal, _activeCase?.Description, _completed.Count, _success,
        _clock.Elapsed.TotalMilliseconds);

    /// <summary>The flusher's tick: write the latest checkpoint if it changed
    /// since the last write. It runs on its own thread, so a case stalled in
    /// a phase still reaches the file even though its thread writes
    /// nothing.</summary>
    internal void FlushProgress()
    {
        lock (_progressGate)
        {
            if (!_progressStopped
                && Volatile.Read(ref _published) is { } checkpoint
                && !ReferenceEquals(checkpoint, _written))
            {
                WriteCheckpoint(checkpoint);
            }
        }
    }

    /// <summary>One atomically replaced checkpoint, not an unbounded log.
    /// It identifies a stalled cell even if the process is killed before
    /// Dispose can write its final coverage report. The .txt extension
    /// keeps this diagnostic separate from the verifier's JSON evidence.
    /// A monitor can briefly deny replacement on Windows; such a diagnostic
    /// failure must not abort a case or weaken final coverage reporting.
    /// Callers hold the progress gate.</summary>
    private void WriteCheckpoint(Checkpoint checkpoint)
    {
        string directory = _configuration.ReportDirectory!;
        string path = Path.Combine(directory, $"{_family}-shard-{_configuration.Index}.progress.txt");
        var progress = new
        {
            family = _family,
            shardIndex = _configuration.Index,
            shardCount = _configuration.Count,
            selectedCases = SelectedCases.Count,
            completedCases = checkpoint.CompletedCases,
            activeOrdinal = checkpoint.ActiveOrdinal,
            activeCell = checkpoint.ActiveCell,
            phase = checkpoint.Phase,
            success = checkpoint.Success,
            elapsedMilliseconds = checkpoint.ElapsedMilliseconds,
        };
        string temporary = path + ".tmp";
        string contents = JsonSerializer.Serialize(progress);
        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(temporary, contents);
            File.Move(temporary, path, overwrite: true);
            _written = checkpoint;
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            // Reuse this same temporary file on the next checkpoint. The final
            // JSON remains strict and records any missed diagnostic writes.
            _missedProgressWrites++;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _clock.Stop();
        lock (_progressGate)
        {
            _progressStopped = true;
        }
        _progressTimer?.Dispose();
        int missedProgressWrites;
        lock (_progressGate)
        {
            if (_configuration.ReportDirectory is not null)
            {
                WriteCheckpoint(Snapshot(_success ? "complete" : "failed"));
            }
            missedProgressWrites = _missedProgressWrites;
        }
        if (_configuration.ReportDirectory is not { } directory)
        {
            return;
        }
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, $"{_family}-shard-{_configuration.Index}.json");
        var report = new
        {
            schemaVersion = 1,
            family = _family,
            shardIndex = _configuration.Index,
            shardCount = _configuration.Count,
            totalCells = TotalCells,
            unreachableCells = UnreachableCells,
            reachableCells = ReachableCells,
            inventorySha256 = InventorySha256,
            selectedOrdinals = SelectedCases.Select(modelCase => modelCase.Ordinal).ToArray(),
            completedOrdinals = _completed.ToArray(),
            success = _success,
            missedProgressWrites,
            elapsedMilliseconds = _clock.Elapsed.TotalMilliseconds,
            routes = _routes.Values.OrderBy(route => route.Route, StringComparer.Ordinal),
            slowestCases = _slowestCases,
        };
        File.WriteAllText(path, JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        }));
    }

    private sealed record SlowCase(int Ordinal, string Cell, string Route, bool Completed,
        double ElapsedMilliseconds, IReadOnlyDictionary<string, double> Phases);

    private sealed record Checkpoint(string Phase, int? ActiveOrdinal, string? ActiveCell,
        int CompletedCases, bool Success, double ElapsedMilliseconds);

    internal sealed class RouteTiming(string route)
    {
        public string Route { get; } = route;
        public int Cases { get; set; }
        public double ElapsedMilliseconds { get; set; }
        public Dictionary<string, double> Phases { get; } = new(StringComparer.Ordinal);
    }

    internal sealed class CaseTiming : IDisposable
    {
        private readonly RouteTiming _route;
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private readonly Action<string> _progress;
        private readonly Dictionary<string, double> _phases = new(StringComparer.Ordinal);
        private string _phase = "fixtureSetup";
        private double _phaseStarted;
        private bool _disposed;

        internal CaseTiming(RouteTiming route, Action<string> progress)
        {
            _route = route;
            _progress = progress;
            _route.Cases++;
        }

        internal bool Completed { get; private set; }
        internal double ElapsedMilliseconds => _clock.Elapsed.TotalMilliseconds;
        internal IReadOnlyDictionary<string, double> Phases => _phases;

        internal void Phase(string phase)
        {
            RecordPhase();
            _phase = phase;
            _progress(phase);
        }

        internal void Complete() => Completed = true;

        private void RecordPhase()
        {
            double elapsed = _clock.Elapsed.TotalMilliseconds;
            double phaseElapsed = elapsed - _phaseStarted;
            _route.Phases.TryGetValue(_phase, out double previous);
            _route.Phases[_phase] = previous + phaseElapsed;
            _phases.TryGetValue(_phase, out double casePrevious);
            _phases[_phase] = casePrevious + phaseElapsed;
            _phaseStarted = elapsed;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            _clock.Stop();
            RecordPhase();
            _route.ElapsedMilliseconds += _clock.Elapsed.TotalMilliseconds;
        }
    }
}
