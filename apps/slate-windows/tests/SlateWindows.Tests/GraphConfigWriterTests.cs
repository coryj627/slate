// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Graph;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W6-2 PR C (#746), rule W Terms W1, W2 and W6: the application writer —
/// one key for two spellings of a root, the generation reserved at
/// schedule time and admission atomic, the newest outstanding aggregate
/// served to a reopen (a parked write included), a failed write lost, a
/// straggler never overwriting a reopened workspace's write.
/// </summary>
public sealed class GraphConfigWriterTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"slate-graph-writer-{Guid.NewGuid():N}");

    public GraphConfigWriterTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    /// <summary>Term W1 (IPG-2): the key is the lifecycle's identity — a
    /// trailing separator trimmed, a DRIVE ROOT left rooted. A bare TrimEnd
    /// turned "C:\" into the drive-relative "C:", and the store then wrote
    /// beside the process's current directory instead of the vault.</summary>
    [Fact]
    public void TheKeyTrimsATrailingSeparatorAndLeavesARootRooted()
    {
        string root = Path.GetPathRoot(Path.GetFullPath(_root))!;
        string key = GraphConfigWriter.KeyOf(root);
        Assert.True(Path.IsPathFullyQualified(key), $"the root's key '{key}' is not fully qualified");
        Assert.Equal(
            Path.GetFullPath(Path.Combine(root, ".slate", GraphConfigStore.FileName)),
            Path.GetFullPath(Path.Combine(key, ".slate", GraphConfigStore.FileName)));
        // Every other path loses its trailing separator, and the pair the
        // lifecycle calls one vault is one key.
        Assert.Equal(_root, GraphConfigWriter.KeyOf(_root + Path.DirectorySeparatorChar));
        Assert.Equal(
            GraphConfigWriter.KeyOf(_root).ToUpperInvariant(),
            GraphConfigWriter.KeyOf(_root.ToUpperInvariant()));
    }

    /// <summary>Rule W, Term W6 (IPG-35): a FAILED write leaves nothing
    /// outstanding, so the newest aggregate is never a failed one and the
    /// next read is the file's. The outcome and the removal now happen under
    /// ONE lock; between the two locks they used to take, `Newest` could hand
    /// a reopening workspace the aggregate whose write had just failed.
    /// (The interval itself is not observable in process — this asserts the
    /// post-condition the merge guarantees.)</summary>
    [Fact]
    public void AFailedWriteLeavesNothingOutstandingForTheNextReader()
    {
        var writer = new GraphConfigWriter();
        writer.StoreFor = _ => new GraphConfigStore(_root);
        writer.WriteGateForTests = (_, _) => throw new IOException("the disk went away");
        ulong generation = writer.Reserve(_root);
        Assert.True(writer.Enqueue(_root, WithDepth(3), generation).Wait(TimeSpan.FromSeconds(10)));
        Assert.Equal(1, writer.FailedForTests);
        Assert.Null(writer.Newest(_root));
    }

    /// <summary>Term W6, the other half of IPG-35: an exception the
    /// best-effort filter does NOT name still leaves nothing outstanding.
    /// Moving the removal into the two outcome locks dropped the `finally`
    /// that used to guarantee it, so an unexpected failure stuck the
    /// generation in `Outstanding` for the writer's life and `Newest` went
    /// on offering it (codoki's review of 4a00a3c5).</summary>
    [Fact]
    public void AnUnexpectedWriteFailureAlsoLeavesNothingOutstanding()
    {
        var writer = new GraphConfigWriter();
        writer.StoreFor = _ => new GraphConfigStore(_root);
        writer.WriteGateForTests = (_, _) => throw new InvalidOperationException("not one of the three");
        ulong generation = writer.Reserve(_root);
        Task write = writer.Enqueue(_root, WithDepth(3), generation);
        // It propagates, as an unnamed failure always did — only the three
        // the filter names are swallowed.
        AggregateException faulted = Assert.Throws<AggregateException>(() => write.Wait(TimeSpan.FromSeconds(10)));
        Assert.IsType<InvalidOperationException>(faulted.InnerException);
        Assert.Equal(0, writer.FailedForTests);
        Assert.Null(writer.Newest(_root));
    }

    private static GraphConfig WithDepth(uint depth) => SlateUniffiMethods.GraphConfigDefault() with { ConnectionsDepth = depth };

    private GraphConfig OnDisk() => new GraphConfigStore(_root).Read().Config;

    [Fact]
    public void TwoSpellingsOfOneRootShareOneQueueAndOneGeneration()
    {
        var writer = new GraphConfigWriter();
        string upper = _root.ToUpperInvariant() + Path.DirectorySeparatorChar;
        string lower = _root.ToLowerInvariant();
        Assert.Equal(GraphConfigWriter.KeyOf(upper).ToUpperInvariant(), GraphConfigWriter.KeyOf(lower).ToUpperInvariant());
        Assert.Equal(1UL, writer.Reserve(upper));
        Assert.Equal(2UL, writer.Reserve(lower));
        Assert.Equal(3UL, writer.Reserve(_root));
        writer.Enqueue(upper, WithDepth(2), 2).Wait();
        Assert.Equal(2UL, writer.StateForTests(lower).Admitted);
        Assert.Equal(2UL, writer.StateForTests(lower).LastWritten);
        Assert.Equal(2u, OnDisk().ConnectionsDepth);
    }

    [Fact]
    public void TheGenerationIsReservedAtScheduleTime()
    {
        var writer = new GraphConfigWriter();
        ulong older = writer.Reserve(_root);
        ulong newer = writer.Reserve(_root);
        // The newer hand-off lands first; the older, released after it, is
        // dropped at the call — never queued, never written.
        writer.Enqueue(_root, WithDepth(3), newer).Wait();
        Task dropped = writer.Enqueue(_root, WithDepth(2), older);
        Assert.True(dropped.IsCompleted);
        Assert.Equal(1, writer.DroppedAtEnqueueForTests);
        Assert.Equal(3u, OnDisk().ConnectionsDepth);
        Assert.Equal(newer, writer.StateForTests(_root).LastWritten);
    }

    [Fact]
    public void NewestIsTheHighestOutstandingAndNeverAFailedOrDoomedOne()
    {
        var writer = new GraphConfigWriter();
        using var gate = new ManualResetEventSlim(false);
        using var reached = new ManualResetEventSlim(false);
        writer.WriteGateForTests = (_, aggregate) =>
        {
            if (aggregate.ConnectionsDepth == 1)
            {
                reached.Set();
                gate.Wait(TimeSpan.FromSeconds(10));
            }
            if (aggregate.ConnectionsDepth == 3)
            {
                throw new IOException("disk gone");
            }
        };
        ulong first = writer.Reserve(_root);
        ulong second = writer.Reserve(_root);
        ulong third = writer.Reserve(_root);
        Task one = writer.Enqueue(_root, WithDepth(1), first);
        Assert.True(reached.Wait(TimeSpan.FromSeconds(10)), "the first write never parked");
        Task two = writer.Enqueue(_root, WithDepth(2), second);
        Assert.Equal(2u, writer.Newest(_root)!.ConnectionsDepth);
        // A doomed enqueue — a generation at or below the admitted — changes nothing.
        Task doomed = writer.Enqueue(_root, WithDepth(9), first);
        Assert.True(doomed.IsCompleted);
        Assert.Equal(2u, writer.Newest(_root)!.ConnectionsDepth);
        gate.Set();
        Task.WaitAll([one, two], TimeSpan.FromSeconds(10));
        Assert.Null(writer.Newest(_root));
        Assert.Equal(2u, OnDisk().ConnectionsDepth);
        // A failing write is outstanding until attempted, then gone — never served.
        Task three = writer.Enqueue(_root, WithDepth(3), third);
        three.Wait(TimeSpan.FromSeconds(10));
        Assert.Null(writer.Newest(_root));
        Assert.Equal(1, writer.FailedForTests);
        Assert.Equal(second, writer.StateForTests(_root).LastWritten);
        Assert.Equal(2u, OnDisk().ConnectionsDepth);
    }

    [Fact]
    public void AReopenDuringAnExecutingWriteReadsItsAggregate()
    {
        var writer = new GraphConfigWriter();
        using var gate = new ManualResetEventSlim(false);
        using var reached = new ManualResetEventSlim(false);
        writer.WriteGateForTests = (_, _) =>
        {
            reached.Set();
            gate.Wait(TimeSpan.FromSeconds(10));
        };
        ulong generation = writer.Reserve(_root);
        Task write = writer.Enqueue(_root, WithDepth(3), generation);
        // Parked AFTER the dequeue (IGP-11): still outstanding, still served.
        Assert.True(reached.Wait(TimeSpan.FromSeconds(10)));
        Assert.Equal(3u, writer.Newest(_root)!.ConnectionsDepth);
        PumpedDispatcher.Run(() =>
        {
            var reopened = new GraphPreferencesViewModel(_root, writer);
            Assert.Equal(3u, reopened.CurrentConfig.ConnectionsDepth);
            Assert.True(reopened.IsWritable);
            reopened.Shutdown();
        });
        gate.Set();
        write.Wait(TimeSpan.FromSeconds(10));
        Assert.Equal(3u, OnDisk().ConnectionsDepth);
    }

    /// <summary>Term W6: a reopen during a STRAGGLING write — queued behind
    /// a parked one, not yet dequeued — reads the straggler's aggregate,
    /// the highest outstanding; the parked one is older and not served.</summary>
    [Fact]
    public void AReopenDuringAStragglingWriteReadsTheStragglersAggregate()
    {
        var writer = new GraphConfigWriter();
        using var gate = new ManualResetEventSlim(false);
        using var reached = new ManualResetEventSlim(false);
        writer.WriteGateForTests = (_, aggregate) =>
        {
            if (aggregate.ConnectionsDepth == 1)
            {
                reached.Set();
                gate.Wait(TimeSpan.FromSeconds(10));
            }
        };
        Task parked = writer.Enqueue(_root, WithDepth(1), writer.Reserve(_root));
        Assert.True(reached.Wait(TimeSpan.FromSeconds(10)));
        Task straggler = writer.Enqueue(_root, WithDepth(3), writer.Reserve(_root));
        Assert.False(straggler.IsCompleted);
        Assert.Equal(2, writer.StateForTests(_root).Outstanding);
        Assert.Equal(3u, writer.Newest(_root)!.ConnectionsDepth);
        PumpedDispatcher.Run(() =>
        {
            var reopened = new GraphPreferencesViewModel(_root, writer);
            Assert.Equal(3u, reopened.CurrentConfig.ConnectionsDepth);
            Assert.True(reopened.IsWritable);
            reopened.Shutdown();
        });
        gate.Set();
        Task.WaitAll([parked, straggler], TimeSpan.FromSeconds(10));
        Assert.Null(writer.Newest(_root));
        Assert.Equal(3u, OnDisk().ConnectionsDepth);
    }

    [Fact]
    public void AFailedWriteIsLostAndTheReopenReadsTheFile()
    {
        var writer = new GraphConfigWriter();
        writer.Enqueue(_root, WithDepth(2), writer.Reserve(_root)).Wait();
        writer.WriteGateForTests = (_, _) => throw new IOException("disk gone");
        writer.Enqueue(_root, WithDepth(3), writer.Reserve(_root)).Wait();
        Assert.Null(writer.Newest(_root));
        PumpedDispatcher.Run(() =>
        {
            var reopened = new GraphPreferencesViewModel(_root, writer);
            Assert.Equal(2u, reopened.CurrentConfig.ConnectionsDepth);
            reopened.Shutdown();
        });
    }

    [Fact]
    public void AStragglerFromAClosedWorkspaceNeverOverwritesAReopenedWorkspacesWrite()
    {
        var writer = new GraphConfigWriter();
        using var gate = new ManualResetEventSlim(false);
        using var reached = new ManualResetEventSlim(false);
        writer.WriteGateForTests = (_, aggregate) =>
        {
            if (aggregate.ConnectionsDepth == 1)
            {
                reached.Set();
                gate.Wait(TimeSpan.FromSeconds(10));
            }
        };
        // The closed workspace's straggler, parked in its write.
        Task straggler = writer.Enqueue(_root, WithDepth(1), writer.Reserve(_root));
        Assert.True(reached.Wait(TimeSpan.FromSeconds(10)));
        // The reopened workspace reads the straggler's aggregate (Term W6),
        // edits, and its write is admitted behind the straggler.
        Task reopenedWrite = Task.CompletedTask;
        PumpedDispatcher.Run(() =>
        {
            var reopened = new GraphPreferencesViewModel(_root, writer);
            Assert.Equal(1u, reopened.CurrentConfig.ConnectionsDepth);
            reopened.SetConnectionsDepth(2);
            reopened.Shutdown();
            reopenedWrite = reopened.WhenWritesDrained();
        });
        gate.Set();
        Task.WaitAll([straggler, reopenedWrite], TimeSpan.FromSeconds(10));
        // The queue is serial and the generations follow edit order: the
        // reopened workspace's write is the last one on disk.
        Assert.Equal(2u, OnDisk().ConnectionsDepth);
        Assert.Equal(0, writer.DroppedAtEnqueueForTests);
    }
}
