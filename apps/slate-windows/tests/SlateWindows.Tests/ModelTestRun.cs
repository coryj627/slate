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
    internal sealed record Case(TCell Value, int Ordinal, string Route);

    private sealed record InventoryEntry(string Cell, string Route, string? UnreachableReason);

    private readonly string _family;
    private readonly ModelShardConfiguration _configuration;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly List<int> _completed = [];
    private readonly HashSet<int> _started = [];
    private readonly Dictionary<int, Case> _selectedByOrdinal;
    private readonly Dictionary<string, RouteTiming> _routes = new(StringComparer.Ordinal);
    private bool _inventoryVerified;
    private bool _success;
    private bool _disposed;

    internal ModelTestRun(
        string family,
        IEnumerable<TCell> cells,
        Func<TCell, string> describe,
        Func<TCell, string> route,
        Func<TCell, string?> unreachable,
        ModelShardConfiguration configuration)
    {
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
                selected.Add(new(cell, ordinal, routeName));
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

    internal void AssertInventory(int total, int unreachable, int reachable)
    {
        Assert.Equal(total, TotalCells);
        Assert.Equal(unreachable, UnreachableCells);
        Assert.Equal(reachable, ReachableCells);
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
        using var timing = new CaseTiming(route);
        body(timing);
        if (timing.Completed)
        {
            _completed.Add(modelCase.Ordinal);
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
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _clock.Stop();
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
            elapsedMilliseconds = _clock.Elapsed.TotalMilliseconds,
            routes = _routes.Values.OrderBy(route => route.Route, StringComparer.Ordinal),
        };
        File.WriteAllText(path, JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        }));
    }

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
        private string _phase = "fixtureSetup";
        private double _phaseStarted;
        private bool _disposed;

        internal CaseTiming(RouteTiming route)
        {
            _route = route;
            _route.Cases++;
        }

        internal bool Completed { get; private set; }

        internal void Phase(string phase)
        {
            RecordPhase();
            _phase = phase;
        }

        internal void Complete() => Completed = true;

        private void RecordPhase()
        {
            double elapsed = _clock.Elapsed.TotalMilliseconds;
            _route.Phases.TryGetValue(_phase, out double previous);
            _route.Phases[_phase] = previous + elapsed - _phaseStarted;
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
