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

    /// <summary>The digest of <see cref="Inventory"/>, pinned as the model
    /// families pin theirs.</summary>
    private const string InventorySha256 = "577e047fb07bb252991fcce08bade6609c512ea8e6c13f797d2c080288555066";

    private static ModelTestRun<Cell> Run(ModelShardConfiguration configuration, IEnumerable<Cell>? inventory = null,
        TimeSpan? progressInterval = null, int? stallTicks = null, Action<string>? onStall = null) =>
        new("routes", inventory ?? Inventory, cell => cell.Name, cell => cell.Route, cell => cell.Exclusion, configuration,
            progressInterval, stallTicks, onStall);

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
        run.AssertInventory(7, 2, 5, InventorySha256);
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
        run.AssertInventory(7, 2, 5, InventorySha256);
        var cell = Assert.Single(run.SelectedCases);
        Assert.Equal(3, cell.Ordinal);
        run.RunCase(cell, timing => timing.Complete());
        run.Complete();

        using var empty = Run(Configuration(("SLATE_MODEL_ONLY", "absent")));
        empty.AssertInventory(7, 2, 5, InventorySha256);
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
                run.AssertInventory(7, 2, 5, InventorySha256);
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

    /// <summary>The flusher's tick is driven by hand here; the stalled-case
    /// fact below lets the real timer write.</summary>
    [Fact]
    public void ACheckpointIdentifiesTheCurrentCellAndPhaseBeforeFinalCoverageExists()
    {
        using var directory = new ReportDirectory();
        using (var run = Run(new(0, 1, [], directory.Path), progressInterval: Timeout.InfiniteTimeSpan))
        {
            run.AssertInventory(7, 2, 5, InventorySha256);
            var cell = run.SelectedCases[0];
            run.RunCase(cell, timing =>
            {
                Assert.False(File.Exists(directory.ProgressPath), "a transition wrote the checkpoint itself.");
                run.FlushProgress();
                using (JsonDocument started = directory.ReadProgress())
                {
                    Assert.Equal(cell.Ordinal, started.RootElement.GetProperty("activeOrdinal").GetInt32());
                    Assert.Equal(cell.Value.Name, started.RootElement.GetProperty("activeCell").GetString());
                    Assert.Equal("fixtureSetup", started.RootElement.GetProperty("phase").GetString());
                    Assert.Equal(0, started.RootElement.GetProperty("completedCases").GetInt32());
                    Assert.False(started.RootElement.GetProperty("success").GetBoolean());
                }
                timing.Phase("drive");
                run.FlushProgress();
                using JsonDocument driving = directory.ReadProgress();
                Assert.Equal("drive", driving.RootElement.GetProperty("phase").GetString());
                Assert.False(File.Exists(System.IO.Path.Combine(directory.Path, "routes-shard-0.json")));
                timing.Complete();
            });
            run.FlushProgress();
            using JsonDocument checkpoint = directory.ReadProgress();
            Assert.Equal(1, checkpoint.RootElement.GetProperty("completedCases").GetInt32());
            Assert.Equal("caseCompleted", checkpoint.RootElement.GetProperty("phase").GetString());
            Assert.False(checkpoint.RootElement.GetProperty("success").GetBoolean());
            Assert.Equal(JsonValueKind.Null, checkpoint.RootElement.GetProperty("activeOrdinal").ValueKind);
        }
        // The run stopped without Complete, after a case that completed: the
        // failed checkpoint names no case, rather than the last one that ran.
        using JsonDocument failed = directory.ReadProgress();
        Assert.Equal("failed", failed.RootElement.GetProperty("phase").GetString());
        Assert.False(failed.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(JsonValueKind.Null, failed.RootElement.GetProperty("activeOrdinal").ValueKind);
        Assert.Equal(JsonValueKind.Null, failed.RootElement.GetProperty("activeCell").ValueKind);
        Assert.Single(Directory.GetFiles(directory.Path, "*.json"));
        Assert.Empty(Directory.GetFiles(directory.Path, "*.tmp"));
    }

    /// <summary>The checkpoint's purpose: where a stopped process last made
    /// progress. The flusher writes on its own thread, so a case stalled in
    /// a phase is visible though its thread writes nothing.</summary>
    [Fact]
    public void AStalledCaseIsVisibleThoughItsThreadWritesNothing()
    {
        using var directory = new ReportDirectory();
        using var run = Run(new(0, 1, [], directory.Path), progressInterval: TimeSpan.FromMilliseconds(20));
        run.AssertInventory(7, 2, 5, InventorySha256);
        var cell = run.SelectedCases[2];
        using var stalled = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        Task running = Task.Run(() => run.RunCase(cell, timing =>
        {
            timing.Phase("settleAndVerify");
            stalled.Set();
            Assert.True(release.Wait(TimeSpan.FromSeconds(30)), "the fact never released the stalled case.");
            timing.Complete();
        }));
        try
        {
            Assert.True(stalled.Wait(TimeSpan.FromSeconds(10)), "the case never reached its stall.");
            var wait = System.Diagnostics.Stopwatch.StartNew();
            string? seen = null;
            while (wait.Elapsed < TimeSpan.FromSeconds(10))
            {
                try
                {
                    using JsonDocument progress = directory.ReadProgress();
                    JsonElement root = progress.RootElement;
                    seen = $"{root.GetProperty("phase").GetString()} @ {root.GetProperty("activeOrdinal")}";
                    if (seen == $"settleAndVerify @ {cell.Ordinal}")
                    {
                        break;
                    }
                }
                catch (Exception failure) when (failure is IOException or JsonException)
                {
                    // Not written yet, or caught mid-replacement.
                }
                Thread.Sleep(10);
            }
            Assert.Equal($"settleAndVerify @ {cell.Ordinal}", seen);
        }
        finally
        {
            release.Set();
            running.GetAwaiter().GetResult();
        }
    }

    /// <summary>The stall rule (#1624): a checkpoint the flusher reads
    /// unchanged for the bound's count of ticks is reported once, with its
    /// case and phase, and the file says so. The ticks are driven by hand, so
    /// no fact waits for one.</summary>
    [Fact]
    public void ACaseThatStopsAdvancingIsReportedAsStalledWithItsCellAndPhase()
    {
        using var directory = new ReportDirectory();
        var stalls = new List<string>();
        var onDiskAtTheCall = new List<string?>();
        using var run = Run(new(0, 1, [], directory.Path), progressInterval: Timeout.InfiniteTimeSpan,
            stallTicks: 3, onStall: sentence =>
            {
                stalls.Add(sentence);
                // What a process ended by this call leaves behind.
                using JsonDocument atTheCall = directory.ReadProgress();
                onDiskAtTheCall.Add(atTheCall.RootElement.GetProperty("phase").GetString());
            });
        run.AssertInventory(7, 2, 5, InventorySha256);
        run.RunCase(run.SelectedCases[0], timing => timing.Complete());
        var cell = run.SelectedCases[1];
        run.RunCase(cell, timing =>
        {
            timing.Phase("arrangement");
            run.FlushProgress();
            run.FlushProgress();
            Assert.Empty(stalls);
            double entered;
            using (JsonDocument advancing = directory.ReadProgress())
            {
                Assert.Equal("arrangement", advancing.RootElement.GetProperty("phase").GetString());
                Assert.False(advancing.RootElement.TryGetProperty("stalledPhase", out _),
                    "a checkpoint that is not stalled carries a stalled phase.");
                entered = advancing.RootElement.GetProperty("elapsedMilliseconds").GetDouble();
            }

            run.FlushProgress();
            string sentence = Assert.Single(stalls);
            // The stalled checkpoint is written before the callback runs:
            // the default callback ends the process.
            Assert.Equal(["stalled"], onDiskAtTheCall);
            Assert.Contains("The routes model stalled on shard 0 of 1", sentence, StringComparison.Ordinal);
            Assert.Contains($"case {cell.Ordinal} ({cell.Value.Name})", sentence, StringComparison.Ordinal);
            Assert.Contains("phase arrangement", sentence, StringComparison.Ordinal);
            Assert.Contains("3 checkpoint ticks", sentence, StringComparison.Ordinal);
            Assert.Contains("1 of 5 cases completed", sentence, StringComparison.Ordinal);
            using (JsonDocument stalled = directory.ReadProgress())
            {
                JsonElement root = stalled.RootElement;
                Assert.Equal("stalled", root.GetProperty("phase").GetString());
                Assert.Equal("arrangement", root.GetProperty("stalledPhase").GetString());
                Assert.Equal(cell.Ordinal, root.GetProperty("activeOrdinal").GetInt32());
                Assert.Equal(cell.Value.Name, root.GetProperty("activeCell").GetString());
                Assert.Equal(1, root.GetProperty("completedCases").GetInt32());
                Assert.False(root.GetProperty("success").GetBoolean());
                // The family clock read at the stall, and how much of it the
                // phase has held: their difference is when the phase began,
                // which is the time the advancing checkpoint carried.
                double elapsed = root.GetProperty("elapsedMilliseconds").GetDouble();
                double stalledFor = root.GetProperty("stalledMilliseconds").GetDouble();
                Assert.True(elapsed >= entered, $"the stall's clock reads {elapsed} ms, before the phase began at {entered} ms.");
                Assert.Equal(entered, elapsed - stalledFor, precision: 6);
            }

            // The rule speaks once, and later ticks leave the stalled
            // checkpoint in the file.
            run.FlushProgress();
            run.FlushProgress();
            Assert.Single(stalls);
            using (JsonDocument still = directory.ReadProgress())
            {
                Assert.Equal("stalled", still.RootElement.GetProperty("phase").GetString());
            }

            // A run that a recording callback lets go on writes its next
            // transition as before, and is not reported a second time.
            timing.Phase("drive");
            run.FlushProgress();
            run.FlushProgress();
            run.FlushProgress();
            Assert.Single(stalls);
            using JsonDocument driving = directory.ReadProgress();
            Assert.Equal("drive", driving.RootElement.GetProperty("phase").GetString());
            Assert.False(driving.RootElement.TryGetProperty("stalledPhase", out _));
            timing.Complete();
        });
    }

    [Fact]
    public void ARunThatKeepsAdvancingIsNeverReportedAsStalled()
    {
        using var directory = new ReportDirectory();
        var stalls = new List<string>();
        using var run = Run(new(0, 1, [], directory.Path), progressInterval: Timeout.InfiniteTimeSpan,
            stallTicks: 2, onStall: stalls.Add);
        // The rule is armed: silence below means the run advanced, not that
        // nothing was counting.
        Assert.Equal(2, run.StallTicks);
        run.AssertInventory(7, 2, 5, InventorySha256);
        foreach (var cell in run.SelectedCases)
        {
            run.RunCase(cell, timing =>
            {
                // A transition before every tick: no checkpoint is read twice.
                run.FlushProgress();
                foreach (string phase in new[] { "sessionSetup", "arrangement", "drive", "settleAndVerify", "cleanup" })
                {
                    timing.Phase(phase);
                    run.FlushProgress();
                }
                timing.Complete();
            });
            run.FlushProgress();
        }
        run.Complete();
        // A completed family is not a stalled one, however long its process
        // goes on to the next fact.
        for (int tick = 0; tick < 5; tick++)
        {
            run.FlushProgress();
        }
        Assert.Empty(stalls);
        using JsonDocument complete = directory.ReadProgress();
        Assert.Equal("complete", complete.RootElement.GetProperty("phase").GetString());
        Assert.False(complete.RootElement.TryGetProperty("stalledPhase", out _));
    }

    [Fact]
    public void AStallBetweenCasesSaysNoCaseIsActive()
    {
        using var directory = new ReportDirectory();
        var stalls = new List<string>();
        using var run = Run(new(0, 1, [], directory.Path), progressInterval: Timeout.InfiniteTimeSpan,
            stallTicks: 2, onStall: stalls.Add);
        run.AssertInventory(7, 2, 5, InventorySha256);
        run.RunCase(run.SelectedCases[0], timing => timing.Complete());
        run.FlushProgress();
        Assert.Empty(stalls);
        run.FlushProgress();
        string sentence = Assert.Single(stalls);
        Assert.Contains("The routes model stalled on shard 0 of 1 with no case active", sentence, StringComparison.Ordinal);
        Assert.Contains("phase caseCompleted", sentence, StringComparison.Ordinal);
        Assert.Contains("2 checkpoint ticks", sentence, StringComparison.Ordinal);
        Assert.Contains("1 of 5 cases completed", sentence, StringComparison.Ordinal);
        Assert.DoesNotContain("case 1", sentence, StringComparison.Ordinal);
        using JsonDocument stalled = directory.ReadProgress();
        JsonElement root = stalled.RootElement;
        Assert.Equal("stalled", root.GetProperty("phase").GetString());
        Assert.Equal("caseCompleted", root.GetProperty("stalledPhase").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("activeOrdinal").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("activeCell").ValueKind);
        Assert.Equal(1, root.GetProperty("completedCases").GetInt32());
    }

    /// <summary>Five minutes of the flusher's own ticks, whatever its
    /// interval, rounded up so the rule is never early; a flusher driven by
    /// hand has no bound unless a fact gives one.</summary>
    [Fact]
    public void TheDefaultStallBoundIsFiveMinutesOfTicks()
    {
        Assert.Equal(TimeSpan.FromMinutes(5), ModelTestRun<Cell>.StallBound);
        using var everySecond = Run(Configuration());
        Assert.Equal(300, everySecond.StallTicks);
        using var fast = Run(Configuration(), progressInterval: TimeSpan.FromMilliseconds(20));
        Assert.Equal(15_000, fast.StallTicks);
        using var uneven = Run(Configuration(), progressInterval: TimeSpan.FromSeconds(7));
        Assert.Equal(43, uneven.StallTicks);
        using var byHand = Run(Configuration(), progressInterval: Timeout.InfiniteTimeSpan);
        Assert.Null(byHand.StallTicks);
        // A bound below one tick would report a run that has not been read
        // twice.
        Assert.Throws<ArgumentOutOfRangeException>(() => Run(Configuration(), stallTicks: 0));
    }

    /// <summary>The timer queues one callback for each interval whether or
    /// not the pool runs it, so a pool that ran none for a while runs them
    /// all at once, against a checkpoint that may be milliseconds old. Where
    /// the flusher has an interval, the checkpoint must also have stood for
    /// the bound on the family clock. The hour here is an interval no fact
    /// waits for: every tick below is a callback that arrived late.</summary>
    [Fact]
    public void TicksThatArriveTogetherDoNotReportACheckpointYoungerThanTheBound()
    {
        using var directory = new ReportDirectory();
        var stalls = new List<string>();
        using var run = Run(new(0, 1, [], directory.Path), progressInterval: TimeSpan.FromHours(1),
            stallTicks: 3, onStall: stalls.Add);
        run.AssertInventory(7, 2, 5, InventorySha256);
        run.RunCase(run.SelectedCases[0], timing =>
        {
            timing.Phase("arrangement");
            for (int tick = 0; tick < 10; tick++)
            {
                run.FlushProgress();
            }
            Assert.Empty(stalls);
            using JsonDocument young = directory.ReadProgress();
            Assert.Equal("arrangement", young.RootElement.GetProperty("phase").GetString());
            timing.Complete();
        });
    }

    /// <summary>The age and the ticks together still report a checkpoint
    /// that is held: the real timer, five ticks of 20 ms, and a case that
    /// waits for the report. The case holds in the phase the run publishes
    /// as it starts and publishes no other before the report, so the rule
    /// has one checkpoint to name however the threads are scheduled, and
    /// the stalled checkpoint is read from whichever file took it.</summary>
    [Fact]
    public void AHeldCheckpointIsReportedOnceItsAgeAndItsTicksAreBothReached()
    {
        using var directory = new ReportDirectory();
        var stalls = new System.Collections.Concurrent.ConcurrentQueue<string>();
        using var reported = new ManualResetEventSlim();
        using var run = Run(new(0, 1, [], directory.Path), progressInterval: TimeSpan.FromMilliseconds(20),
            stallTicks: 5, onStall: sentence =>
            {
                stalls.Enqueue(sentence);
                reported.Set();
            });
        run.AssertInventory(7, 2, 5, InventorySha256);
        run.RunCase(run.SelectedCases[0], timing =>
        {
            Assert.True(reported.Wait(TimeSpan.FromSeconds(30)), "the held checkpoint was never reported.");
            using JsonDocument stalled = directory.ReadStalled();
            Assert.Equal("stalled", stalled.RootElement.GetProperty("phase").GetString());
            Assert.Equal("fixtureSetup", stalled.RootElement.GetProperty("stalledPhase").GetString());
            // Four intervals between the first of five ticks and the last.
            Assert.True(stalled.RootElement.GetProperty("stalledMilliseconds").GetDouble() >= 80,
                "the checkpoint was reported younger than the bound.");
            timing.Complete();
        });
        Assert.Contains("phase fixtureSetup", Assert.Single(stalls), StringComparison.Ordinal);
    }

    /// <summary>The three model facts give no callback, so the default is
    /// what a stalled shard gets.</summary>
    [Fact]
    public void ARunGivenNoCallbackEndsTheProcessOnAStall()
    {
        using var byDefault = Run(Configuration());
        Assert.Equal<Action<string>>(ModelTestRun<Cell>.EndTheProcess, byDefault.OnStall);
        Action<string> recording = _ => { };
        using var recorded = Run(Configuration(), onStall: recording);
        Assert.Same(recording, recorded.OnStall);
    }

    /// <summary>A reader without delete sharing refuses the checkpoint's
    /// replacement, as a monitor reading the file does. The stalled
    /// checkpoint is the one the job log prints, so its write is tried again
    /// before the callback ends the process.</summary>
    [Fact]
    public void AStalledCheckpointAReaderHeldIsOnDiskAtTheCallbackOnceTheReaderLetsGo()
    {
        using var directory = new ReportDirectory();
        var stalls = new List<string>();
        var onDiskAtTheCall = new List<string?>();
        using var run = Run(new(0, 1, [], directory.Path), progressInterval: Timeout.InfiniteTimeSpan,
            stallTicks: 2, onStall: sentence =>
            {
                stalls.Add(sentence);
                using JsonDocument atTheCall = directory.ReadProgress();
                onDiskAtTheCall.Add(atTheCall.RootElement.GetProperty("phase").GetString());
            });
        run.AssertInventory(7, 2, 5, InventorySha256);
        run.RunCase(run.SelectedCases[0], timing =>
        {
            timing.Phase("drive");
            run.FlushProgress();
            FileStream? reader = new(directory.ProgressPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var waits = new List<TimeSpan>();
            run.WaitBetweenStalledWrites = wait =>
            {
                waits.Add(wait);
                reader?.Dispose();
                reader = null;
            };
            try
            {
                run.FlushProgress();
            }
            finally
            {
                reader?.Dispose();
            }
            Assert.Single(stalls);
            Assert.Equal(["stalled"], onDiskAtTheCall);
            Assert.Single(waits);
            Assert.False(File.Exists(directory.StalledPath), "the checkpoint took the stall, and a second file was written beside it.");
            timing.Complete();
        });
    }

    /// <summary>A reader that holds the checkpoint through every try leaves
    /// the old phase in it. The process is about to end, and the job log
    /// prints only what is on disk, so the stalled checkpoint goes to a file
    /// of its own beside the held one, under a name no reader holds.</summary>
    [Fact]
    public void AStalledCheckpointAReaderNeverLetsGoIsWrittenBesideItBeforeTheCallback()
    {
        using var directory = new ReportDirectory();
        var stalls = new List<string>();
        var onDiskAtTheCall = new List<string?>();
        var besideAtTheCall = new List<string?>();
        using var run = Run(new(0, 1, [], directory.Path), progressInterval: Timeout.InfiniteTimeSpan,
            stallTicks: 2, onStall: sentence =>
            {
                stalls.Add(sentence);
                using JsonDocument atTheCall = directory.ReadProgress();
                onDiskAtTheCall.Add(atTheCall.RootElement.GetProperty("phase").GetString());
                besideAtTheCall.Add(File.Exists(directory.StalledPath) ? File.ReadAllText(directory.StalledPath) : null);
            });
        run.AssertInventory(7, 2, 5, InventorySha256);
        var cell = run.SelectedCases[0];
        run.RunCase(cell, timing =>
        {
            timing.Phase("drive");
            run.FlushProgress();
            var waits = new List<TimeSpan>();
            run.WaitBetweenStalledWrites = waits.Add;
            using (var reader = new FileStream(directory.ProgressPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                run.FlushProgress();
                // Every try was refused: the file keeps the phase, and the
                // sentence carries the case all the same.
                string sentence = Assert.Single(stalls);
                Assert.Contains($"case {cell.Ordinal} ({cell.Value.Name})", sentence, StringComparison.Ordinal);
                Assert.Contains("phase drive", sentence, StringComparison.Ordinal);
                Assert.Equal(["drive"], onDiskAtTheCall);
                // Five tries, a wait between each two, well under a few
                // seconds in all.
                Assert.Equal(4, waits.Count);
                Assert.InRange(waits.Aggregate(TimeSpan.Zero, (sum, wait) => sum + wait), TimeSpan.Zero, TimeSpan.FromSeconds(2));
                // What the callback found beside the held file: the whole
                // stalled checkpoint, under the name the job's print step
                // also matches.
                Assert.EndsWith(".stalled.progress.txt", directory.StalledPath, StringComparison.Ordinal);
                string? beside = Assert.Single(besideAtTheCall);
                Assert.NotNull(beside);
                using JsonDocument besideIt = JsonDocument.Parse(beside);
                JsonElement root = besideIt.RootElement;
                Assert.Equal("stalled", root.GetProperty("phase").GetString());
                Assert.Equal("drive", root.GetProperty("stalledPhase").GetString());
                Assert.Equal(cell.Ordinal, root.GetProperty("activeOrdinal").GetInt32());
                Assert.Equal(cell.Value.Name, root.GetProperty("activeCell").GetString());
                Assert.Equal("routes", root.GetProperty("family").GetString());
                Assert.Empty(Directory.GetFiles(directory.Path, "*.stalled.progress.txt.tmp"));
            }
            // A run that goes on writes the stalled checkpoint on its next
            // tick, is not reported again, and keeps no second copy.
            run.FlushProgress();
            Assert.Single(stalls);
            using JsonDocument stalled = directory.ReadProgress();
            Assert.Equal("stalled", stalled.RootElement.GetProperty("phase").GetString());
            Assert.False(File.Exists(directory.StalledPath), "the checkpoint took the stall, and the copy beside it stayed.");
            timing.Complete();
        });
    }

    /// <summary>A case thread never waits on the flusher, so it can publish
    /// while a tick that has decided on a stall is still writing and waiting
    /// for a reader. A family that advanced is not stalled: the tick reads
    /// the published checkpoint again before the callback and withdraws.
    /// The wait seam is where each fact below lets the case move.</summary>
    [Fact]
    public void AStallIsWithdrawnWhenTheCaseAdvancesBeforeTheCallback()
    {
        using var directory = new ReportDirectory();
        var stalls = new List<string>();
        using var run = Run(new(0, 1, [], directory.Path), progressInterval: Timeout.InfiniteTimeSpan,
            stallTicks: 2, onStall: stalls.Add);
        run.AssertInventory(7, 2, 5, InventorySha256);
        run.RunCase(run.SelectedCases[0], timing =>
        {
            timing.Phase("drive");
            run.FlushProgress();
            int waits = 0;
            run.WaitBetweenStalledWrites = _ =>
            {
                waits++;
                timing.Phase("settleAndVerify");
            };
            using (var reader = new FileStream(directory.ProgressPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                run.FlushProgress();
                Assert.Empty(stalls);
                // One wait: the tick stops trying once the case has moved.
                Assert.Equal(1, waits);
                Assert.False(File.Exists(directory.StalledPath), "a withdrawn stall left its checkpoint beside the held file.");
            }
            run.FlushProgress();
            Assert.Empty(stalls);
            using JsonDocument advanced = directory.ReadProgress();
            Assert.Equal("settleAndVerify", advanced.RootElement.GetProperty("phase").GetString());
            Assert.False(advanced.RootElement.TryGetProperty("stalledPhase", out _));
            Assert.False(File.Exists(directory.StalledPath));
            timing.Complete();
        });
    }

    [Fact]
    public void AStallIsWithdrawnWhenTheFamilyCompletesBeforeTheCallback()
    {
        using var directory = new ReportDirectory();
        var stalls = new List<string>();
        using var run = Run(new(0, 1, [], directory.Path), progressInterval: Timeout.InfiniteTimeSpan,
            stallTicks: 2, onStall: stalls.Add);
        run.AssertInventory(7, 2, 5, InventorySha256);
        foreach (var cell in run.SelectedCases)
        {
            run.RunCase(cell, timing => timing.Complete());
        }
        // Between the last case and Complete, which the wait seam runs.
        run.FlushProgress();
        run.WaitBetweenStalledWrites = _ => run.Complete();
        using (var reader = new FileStream(directory.ProgressPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            run.FlushProgress();
            Assert.Empty(stalls);
        }
        run.Dispose();
        Assert.Empty(stalls);
        using JsonDocument complete = directory.ReadProgress();
        Assert.Equal("complete", complete.RootElement.GetProperty("phase").GetString());
        Assert.True(complete.RootElement.GetProperty("success").GetBoolean());
        Assert.False(complete.RootElement.TryGetProperty("stalledPhase", out _));
        Assert.False(File.Exists(directory.StalledPath));
        using JsonDocument report = directory.Read(0);
        Assert.True(report.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(5, report.RootElement.GetProperty("completedOrdinals").GetArrayLength());
    }

    /// <summary>A family whose fact has begun to dispose it is ending, not
    /// stalled. The seam disposes on the tick's own thread, which stands for
    /// a fact thread that has entered Dispose and waits for the gate.</summary>
    [Fact]
    public void AStallIsWithdrawnWhenTheRunIsBeingDisposedBeforeTheCallback()
    {
        using var directory = new ReportDirectory();
        var stalls = new List<string>();
        using var run = Run(new(0, 1, [], directory.Path), progressInterval: Timeout.InfiniteTimeSpan,
            stallTicks: 2, onStall: stalls.Add);
        run.AssertInventory(7, 2, 5, InventorySha256);
        run.RunCase(run.SelectedCases[0], timing => timing.Complete());
        run.FlushProgress();
        run.WaitBetweenStalledWrites = _ => run.Dispose();
        using (var reader = new FileStream(directory.ProgressPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            run.FlushProgress();
        }
        Assert.Empty(stalls);
        Assert.False(File.Exists(directory.StalledPath));
        using JsonDocument report = directory.Read(0);
        Assert.False(report.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal([1], report.RootElement.GetProperty("completedOrdinals").EnumerateArray().Select(value => value.GetInt32()));
        using JsonDocument last = directory.ReadProgress();
        Assert.False(last.RootElement.TryGetProperty("stalledPhase", out _));
    }

    /// <summary>Only a recording callback lets a stalled run reach its end.
    /// When one does, the stalled copy beside a held checkpoint goes with
    /// the run, whether or not the reader lets the last checkpoint in.</summary>
    [Fact]
    public void AStalledCopyBesideAHeldCheckpointDoesNotOutliveTheRun()
    {
        using var directory = new ReportDirectory();
        var stalls = new List<string>();
        using var run = Run(new(0, 1, [], directory.Path), progressInterval: Timeout.InfiniteTimeSpan,
            stallTicks: 2, onStall: stalls.Add);
        run.AssertInventory(7, 2, 5, InventorySha256);
        run.RunCase(run.SelectedCases[0], timing => timing.Complete());
        run.FlushProgress();
        run.WaitBetweenStalledWrites = _ => { };
        using (var reader = new FileStream(directory.ProgressPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            run.FlushProgress();
            Assert.Single(stalls);
            Assert.True(File.Exists(directory.StalledPath), "the stall a reader held was not written beside the checkpoint.");
            run.Dispose();
            Assert.False(File.Exists(directory.StalledPath), "the stalled copy outlived its run.");
        }
        using JsonDocument report = directory.Read(0);
        Assert.False(report.RootElement.GetProperty("success").GetBoolean());
        Assert.Single(stalls);
    }

    [Fact]
    public void ARunWhoseStallWasWithdrawnIsStillReportedOnceWhenALaterCheckpointStands()
    {
        using var directory = new ReportDirectory();
        var stalls = new List<string>();
        using var run = Run(new(0, 1, [], directory.Path), progressInterval: Timeout.InfiniteTimeSpan,
            stallTicks: 2, onStall: stalls.Add);
        run.AssertInventory(7, 2, 5, InventorySha256);
        var cell = run.SelectedCases[0];
        run.RunCase(cell, timing =>
        {
            timing.Phase("drive");
            run.FlushProgress();
            bool moved = false;
            run.WaitBetweenStalledWrites = _ =>
            {
                if (!moved)
                {
                    moved = true;
                    timing.Phase("settleAndVerify");
                }
            };
            using (var reader = new FileStream(directory.ProgressPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                run.FlushProgress();
            }
            Assert.Empty(stalls);
            // The later checkpoint now stands for its two ticks.
            run.FlushProgress();
            Assert.Empty(stalls);
            run.FlushProgress();
            string sentence = Assert.Single(stalls);
            Assert.Contains($"case {cell.Ordinal} ({cell.Value.Name})", sentence, StringComparison.Ordinal);
            Assert.Contains("phase settleAndVerify", sentence, StringComparison.Ordinal);
            Assert.Contains("2 checkpoint ticks", sentence, StringComparison.Ordinal);
            using (JsonDocument stalled = directory.ReadProgress())
            {
                Assert.Equal("stalled", stalled.RootElement.GetProperty("phase").GetString());
                Assert.Equal("settleAndVerify", stalled.RootElement.GetProperty("stalledPhase").GetString());
            }
            run.FlushProgress();
            run.FlushProgress();
            Assert.Single(stalls);
            timing.Complete();
        });
    }

    [Fact]
    public void SlowCellSamplesAreBoundedAndRetainPerCellPhaseAccounting()
    {
        using var directory = new ReportDirectory();
        Cell[] cells = [.. Enumerable.Range(1, 40).Select(index => new Cell($"cell-{index}", "Open"))];
        using (var run = Run(new(0, 1, [], directory.Path), cells))
        {
            run.AssertInventory(40, 0, 40, "e3a9138c8ad7a19424d9ba35154be189ef84c3b972e34769928a9e383d5d85da");
            foreach (var cell in run.SelectedCases)
            {
                run.RunCase(cell, timing =>
                {
                    timing.Phase("drive");
                    // Later cases run longer, so the slowest sixteen arrive
                    // last: an eviction from the wrong end, or before the
                    // sort, keeps fast cases instead.
                    var spin = System.Diagnostics.Stopwatch.StartNew();
                    while (spin.Elapsed < TimeSpan.FromMilliseconds(cell.Ordinal / 2.0))
                    {
                        Thread.SpinWait(64);
                    }
                    timing.Phase("cleanup");
                    timing.Complete();
                });
            }
            run.Complete();
        }
        using JsonDocument report = directory.Read(0);
        JsonElement samples = report.RootElement.GetProperty("slowestCases");
        Assert.Equal(16, samples.GetArrayLength());
        // Kept means slowest, whatever the measured durations were: no kept
        // case may be faster than the average of the cases not kept.
        JsonElement route = Assert.Single(report.RootElement.GetProperty("routes").EnumerateArray());
        double[] kept = [.. samples.EnumerateArray()
            .Select(sample => sample.GetProperty("elapsedMilliseconds").GetDouble())];
        double droppedAverage = (route.GetProperty("elapsedMilliseconds").GetDouble() - kept.Sum())
            / (route.GetProperty("cases").GetInt32() - kept.Length);
        Assert.True(kept.Min() >= droppedAverage - 1e-6,
            $"the fastest kept case took {kept.Min():F3} ms; the dropped cases averaged {droppedAverage:F3} ms.");
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
        using (var run = Run(new(0, 1, [], directory.Path), progressInterval: Timeout.InfiniteTimeSpan))
        {
            run.AssertInventory(7, 2, 5, InventorySha256);
            foreach (var cell in run.SelectedCases)
            {
                run.RunCase(cell, timing =>
                {
                    if (cell.Ordinal == 1)
                    {
                        run.FlushProgress();
                        // Windows denies replacement while a reader lacks
                        // delete sharing, just like a live checkpoint monitor.
                        using var reader = new FileStream(directory.ProgressPath,
                            FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        timing.Phase("drive");
                        run.FlushProgress();
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
        run.AssertInventory(7, 2, 5, InventorySha256);
        foreach (var cell in run.SelectedCases)
        {
            run.RunCase(cell, timing => timing.Complete());
        }
        run.Complete();
        // No checkpoint has been flushed yet, so nothing has made the
        // directory the locked report sits in.
        Directory.CreateDirectory(directory.Path);
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
            run.AssertInventory(7, 2, 5, InventorySha256);
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
        // The case that threw stays the active one through the failure.
        using JsonDocument failed = directory.ReadProgress();
        Assert.Equal("failed", failed.RootElement.GetProperty("phase").GetString());
        Assert.Equal(2, failed.RootElement.GetProperty("activeOrdinal").GetInt32());
    }

    [Fact]
    public void ReturningAfterAHandledFailureAndInventoryDriftCannotPass()
    {
        using var directory = new ReportDirectory();
        using (var run = Run(new(0, 1, [], directory.Path)))
        {
            run.AssertInventory(7, 2, 5, InventorySha256);
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
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => drift.AssertInventory(7, 1, 6, InventorySha256));
        // The counts can hold while the inventory changes: a renamed route
        // or reworded exclusion reason moves only the digest.
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => drift.AssertInventory(7, 2, 5, new string('0', 64)));
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

        /// <summary>Where a stalled checkpoint goes when a reader holds the
        /// checkpoint's own file.</summary>
        internal string StalledPath => System.IO.Path.Combine(Path, "routes-shard-0.stalled.progress.txt");

        internal JsonDocument ReadProgress() =>
            JsonDocument.Parse(File.ReadAllText(ProgressPath));

        /// <summary>The stalled checkpoint, from whichever file took it.</summary>
        internal JsonDocument ReadStalled() =>
            JsonDocument.Parse(File.ReadAllText(File.Exists(StalledPath) ? StalledPath : ProgressPath));

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
