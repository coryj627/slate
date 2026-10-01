// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.Json;

namespace SlateWindows.Tests;

public sealed class ModelTestRunTests
{
    private sealed record Cell(string Name, string Route, string? Exclusion = null);

    private static readonly Cell[] Inventory =
    [
        new("not a state", "Open", "no root"),
        new("first", "Open"),
        new("second", "Close"),
        new("another exclusion", "Close", "no tab"),
        new("third", "Open"),
        new("fourth", "Close"),
        new("fifth", "Open"),
    ];

    private static ModelTestRun<Cell> Run(ModelShardConfiguration configuration, IEnumerable<Cell>? inventory = null) =>
        new("routes", inventory ?? Inventory, cell => cell.Name, cell => cell.Route, cell => cell.Exclusion, configuration);

    private static ModelShardConfiguration Configuration(params (string Name, string? Value)[] values)
    {
        var environment = values.ToDictionary(item => item.Name, item => item.Value, StringComparer.Ordinal);
        return ModelShardConfiguration.Parse(name => environment.GetValueOrDefault(name));
    }

    [Fact]
    public void NoShardConfigurationRunsTheCompleteInventory()
    {
        ModelShardConfiguration configuration = Configuration();
        Assert.Equal(0, configuration.Index);
        Assert.Equal(1, configuration.Count);
        using var run = Run(configuration);
        run.AssertInventory(7, 2, 5);
        Assert.Equal([1, 2, 3, 4, 5], run.SelectedCases.Select(cell => cell.Ordinal));
        foreach (var cell in run.SelectedCases)
        {
            run.RunCase(cell, timing => timing.Complete());
        }
        run.Complete();
    }

    [Theory]
    [InlineData(null, "2")]
    [InlineData("0", null)]
    [InlineData("", "2")]
    [InlineData("zero", "2")]
    [InlineData("0", "2.0")]
    [InlineData("-1", "2")]
    [InlineData("2", "2")]
    [InlineData("0", "0")]
    [InlineData("0", "-2")]
    [InlineData("+0", "2")]
    [InlineData(" 0", "2")]
    [InlineData("0", "2147483648")]
    [InlineData("0", "138")]
    public void PartialMalformedOrOutOfRangeShardsFail(string? index, string? count) =>
        Assert.Throws<InvalidOperationException>(() => Configuration(
            ("SLATE_MODEL_SHARD_INDEX", index), ("SLATE_MODEL_SHARD_COUNT", count)));

    [Theory]
    [InlineData("0", "1", null)]
    [InlineData("1", "2", null)]
    [InlineData(null, null, "reports")]
    public void DebugNarrowingCannotProduceACoverageReportOrShard(string? index, string? count, string? directory) =>
        Assert.Throws<InvalidOperationException>(() => Configuration(
            ("SLATE_MODEL_ONLY", "Open"), ("SLATE_MODEL_SHARD_INDEX", index),
            ("SLATE_MODEL_SHARD_COUNT", count), ("SLATE_MODEL_REPORT_DIR", directory)));

    [Fact]
    public void LocalDebugNarrowingStillChecksTheFullInventoryAndRequiresAMatch()
    {
        using var run = Run(Configuration(("SLATE_MODEL_ONLY", "third")));
        run.AssertInventory(7, 2, 5);
        var cell = Assert.Single(run.SelectedCases);
        Assert.Equal(3, cell.Ordinal);
        run.RunCase(cell, timing => timing.Complete());
        run.Complete();

        using var empty = Run(Configuration(("SLATE_MODEL_ONLY", "absent")));
        empty.AssertInventory(7, 2, 5);
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(empty.Complete);
    }

    [Fact]
    public void ShardsPartitionReachableOrdinalsAndReportTheSameFullInventory()
    {
        using var directory = new ReportDirectory();
        var completed = new List<int>();
        string? inventoryHash = null;
        for (int index = 0; index < 2; index++)
        {
            using (var run = Run(new(index, 2, [], directory.Path)))
            {
                run.AssertInventory(7, 2, 5);
                Assert.Equal(index == 0 ? [1, 3, 5] : [2, 4], run.SelectedCases.Select(cell => cell.Ordinal));
                foreach (var cell in run.SelectedCases)
                {
                    run.RunCase(cell, timing =>
                    {
                        timing.Phase("arrangement");
                        timing.Phase("cleanup");
                        timing.Complete();
                    });
                }
                run.Complete();
            }
            using JsonDocument report = directory.Read(index);
            JsonElement root = report.RootElement;
            Assert.True(root.GetProperty("success").GetBoolean());
            Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
            Assert.Equal("routes", root.GetProperty("family").GetString());
            Assert.Equal(index, root.GetProperty("shardIndex").GetInt32());
            Assert.Equal(2, root.GetProperty("shardCount").GetInt32());
            Assert.Equal(7, root.GetProperty("totalCells").GetInt32());
            Assert.Equal(2, root.GetProperty("unreachableCells").GetInt32());
            Assert.Equal(5, root.GetProperty("reachableCells").GetInt32());
            string? hash = root.GetProperty("inventorySha256").GetString();
            Assert.Matches("^[a-f0-9]{64}$", hash!);
            inventoryHash ??= hash;
            Assert.Equal(inventoryHash, hash);
            int[] selected = [.. root.GetProperty("selectedOrdinals").EnumerateArray().Select(value => value.GetInt32())];
            int[] actual = [.. root.GetProperty("completedOrdinals").EnumerateArray().Select(value => value.GetInt32())];
            Assert.Equal(selected, actual);
            completed.AddRange(actual);
            Assert.True(root.GetProperty("elapsedMilliseconds").GetDouble() >= 0);
            int timedCases = 0;
            foreach (JsonElement route in root.GetProperty("routes").EnumerateArray())
            {
                Assert.Contains(route.GetProperty("route").GetString(), new[] { "Open", "Close" });
                timedCases += route.GetProperty("cases").GetInt32();
                double elapsed = route.GetProperty("elapsedMilliseconds").GetDouble();
                double phases = route.GetProperty("phases").EnumerateObject().Sum(phase => phase.Value.GetDouble());
                Assert.Equal(elapsed, phases, precision: 6);
            }
            Assert.Equal(selected.Length, timedCases);
        }
        Assert.Equal([1, 2, 3, 4, 5], completed.Order());
    }

    [Fact]
    public void InventoryDigestIncludesOrderAndExcludedReasons()
    {
        using var original = Run(Configuration());
        using var reordered = Run(Configuration(), Inventory.Reverse());
        Cell[] changed = [.. Inventory];
        changed[0] = changed[0] with { Exclusion = "a different reason" };
        using var changedReason = Run(Configuration(), changed);
        Assert.NotEqual(original.InventorySha256, reordered.InventorySha256);
        Assert.NotEqual(original.InventorySha256, changedReason.InventorySha256);
    }

    [Fact]
    public void ACheckpointIdentifiesTheCurrentCellAndPhaseBeforeFinalCoverageExists()
    {
        using var directory = new ReportDirectory();
        using (var run = Run(new(0, 1, [], directory.Path)))
        {
            run.AssertInventory(7, 2, 5);
            var cell = run.SelectedCases[0];
            run.RunCase(cell, timing =>
            {
                using (JsonDocument started = directory.ReadProgress())
                {
                    Assert.Equal(cell.Ordinal, started.RootElement.GetProperty("activeOrdinal").GetInt32());
                    Assert.Equal(cell.Value.Name, started.RootElement.GetProperty("activeCell").GetString());
                    Assert.Equal("fixtureSetup", started.RootElement.GetProperty("phase").GetString());
                    Assert.Equal(0, started.RootElement.GetProperty("completedCases").GetInt32());
                    Assert.False(started.RootElement.GetProperty("success").GetBoolean());
                }
                timing.Phase("drive");
                using JsonDocument driving = directory.ReadProgress();
                Assert.Equal("drive", driving.RootElement.GetProperty("phase").GetString());
                Assert.False(File.Exists(System.IO.Path.Combine(directory.Path, "routes-shard-0.json")));
                timing.Complete();
            });
            using JsonDocument checkpoint = directory.ReadProgress();
            Assert.Equal(1, checkpoint.RootElement.GetProperty("completedCases").GetInt32());
            Assert.Equal("caseCompleted", checkpoint.RootElement.GetProperty("phase").GetString());
            Assert.False(checkpoint.RootElement.GetProperty("success").GetBoolean());
        }
        using JsonDocument failed = directory.ReadProgress();
        Assert.Equal("failed", failed.RootElement.GetProperty("phase").GetString());
        Assert.False(failed.RootElement.GetProperty("success").GetBoolean());
        Assert.Single(Directory.GetFiles(directory.Path, "*.json"));
        Assert.Empty(Directory.GetFiles(directory.Path, "*.tmp"));
    }

    [Fact]
    public void SlowCellSamplesAreBoundedAndRetainPerCellPhaseAccounting()
    {
        using var directory = new ReportDirectory();
        Cell[] cells = [.. Enumerable.Range(1, 40).Select(index => new Cell($"cell-{index}", "Open"))];
        using (var run = Run(new(0, 1, [], directory.Path), cells))
        {
            run.AssertInventory(40, 0, 40);
            foreach (var cell in run.SelectedCases)
            {
                run.RunCase(cell, timing =>
                {
                    timing.Phase("drive");
                    timing.Phase("cleanup");
                    timing.Complete();
                });
            }
            run.Complete();
        }
        using JsonDocument report = directory.Read(0);
        JsonElement samples = report.RootElement.GetProperty("slowestCases");
        Assert.Equal(16, samples.GetArrayLength());
        double previous = double.PositiveInfinity;
        foreach (JsonElement sample in samples.EnumerateArray())
        {
            Assert.InRange(sample.GetProperty("ordinal").GetInt32(), 1, 40);
            Assert.True(sample.GetProperty("completed").GetBoolean());
            Assert.StartsWith("cell-", sample.GetProperty("cell").GetString());
            Assert.Equal("Open", sample.GetProperty("route").GetString());
            double elapsed = sample.GetProperty("elapsedMilliseconds").GetDouble();
            Assert.True(elapsed <= previous);
            previous = elapsed;
            Assert.Equal(elapsed, sample.GetProperty("phases").EnumerateObject().Sum(phase => phase.Value.GetDouble()), precision: 6);
        }
        using JsonDocument checkpoint = directory.ReadProgress();
        Assert.Equal("complete", checkpoint.RootElement.GetProperty("phase").GetString());
        Assert.True(checkpoint.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(JsonValueKind.Null, checkpoint.RootElement.GetProperty("activeOrdinal").ValueKind);
    }

    [Fact]
    public void AReaderHoldingTheCheckpointCannotAbortModelCoverage()
    {
        using var directory = new ReportDirectory();
        using (var run = Run(new(0, 1, [], directory.Path)))
        {
            run.AssertInventory(7, 2, 5);
            foreach (var cell in run.SelectedCases)
            {
                run.RunCase(cell, timing =>
                {
                    if (cell.Ordinal == 1)
                    {
                        // Windows denies replacement while a reader lacks
                        // delete sharing, just like a live checkpoint monitor.
                        using var reader = new FileStream(directory.ProgressPath,
                            FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        timing.Phase("drive");
                    }
                    timing.Complete();
                });
            }
            run.Complete();
        }
        using JsonDocument report = directory.Read(0);
        Assert.True(report.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal([1, 2, 3, 4, 5], report.RootElement.GetProperty("completedOrdinals")
            .EnumerateArray().Select(value => value.GetInt32()));
        Assert.Equal(1, report.RootElement.GetProperty("missedProgressWrites").GetInt32());
        using JsonDocument progress = directory.ReadProgress();
        Assert.True(progress.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("complete", progress.RootElement.GetProperty("phase").GetString());
        Assert.Empty(Directory.GetFiles(directory.Path, "*.tmp"));
    }

    [Fact]
    public void ALockedFinalCoverageReportStillFailsTheRun()
    {
        using var directory = new ReportDirectory();
        using var run = Run(new(0, 1, [], directory.Path));
        run.AssertInventory(7, 2, 5);
        foreach (var cell in run.SelectedCases)
        {
            run.RunCase(cell, timing => timing.Complete());
        }
        run.Complete();
        using var lockedReport = new FileStream(System.IO.Path.Combine(directory.Path, "routes-shard-0.json"),
            FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
        Assert.Throws<IOException>(run.Dispose);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AbortedOrCleanupFailedCasesAreNotReportedAsCompleted(bool cleanupFailure)
    {
        using var directory = new ReportDirectory();
        using (var run = Run(new(0, 1, [], directory.Path)))
        {
            run.AssertInventory(7, 2, 5);
            run.RunCase(run.SelectedCases[0], timing => timing.Complete());
            Assert.Throws<InvalidOperationException>(() => run.RunCase(run.SelectedCases[1], timing =>
            {
                using var cleanup = new FailingCleanup(cleanupFailure);
                if (!cleanupFailure)
                {
                    throw new InvalidOperationException("arrangement failed");
                }
                timing.Phase("cleanup");
                timing.Complete();
            }));
            Assert.ThrowsAny<Xunit.Sdk.XunitException>(run.Complete);
        }
        using JsonDocument report = directory.Read(0);
        Assert.False(report.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal([1], report.RootElement.GetProperty("completedOrdinals").EnumerateArray().Select(value => value.GetInt32()));
        Assert.Equal(5, report.RootElement.GetProperty("selectedOrdinals").GetArrayLength());
    }

    [Fact]
    public void ReturningAfterAHandledFailureAndInventoryDriftCannotPass()
    {
        using var directory = new ReportDirectory();
        using (var run = Run(new(0, 1, [], directory.Path)))
        {
            run.AssertInventory(7, 2, 5);
            foreach (var cell in run.SelectedCases)
            {
                run.RunCase(cell, _ => { });
            }
            Assert.ThrowsAny<Xunit.Sdk.XunitException>(run.Complete);
        }
        using JsonDocument report = directory.Read(0);
        Assert.False(report.RootElement.GetProperty("success").GetBoolean());
        Assert.Empty(report.RootElement.GetProperty("completedOrdinals").EnumerateArray());

        using var drift = Run(Configuration());
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => drift.AssertInventory(7, 1, 6));
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => drift.RunCase(drift.SelectedCases[0], timing => timing.Complete()));
    }

    private sealed class FailingCleanup(bool fails) : IDisposable
    {
        public void Dispose()
        {
            if (fails)
            {
                throw new InvalidOperationException("cleanup failed");
            }
        }
    }

    private sealed class ReportDirectory : IDisposable
    {
        internal string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"slate-model-report-tests-{Guid.NewGuid():N}");

        internal string ProgressPath => System.IO.Path.Combine(Path, "routes-shard-0.progress.txt");

        internal JsonDocument Read(int index) =>
            JsonDocument.Parse(File.ReadAllText(System.IO.Path.Combine(Path, $"routes-shard-{index}.json")));

        internal JsonDocument ReadProgress() =>
            JsonDocument.Parse(File.ReadAllText(ProgressPath));

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
