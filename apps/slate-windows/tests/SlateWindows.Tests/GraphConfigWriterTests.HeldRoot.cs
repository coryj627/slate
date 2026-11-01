// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.IO;
using SlateWindows.Graph;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// Plan 44 WB-28 PR 2, open item 1: the writer keys a pending write by the
/// ROOT it was made for and writes it through that root, pinned at
/// admission — never by a spelling, and never by a weak identity two
/// folders can share — and one queue orders every write.
/// </summary>
public sealed partial class GraphConfigWriterTests
{
    private const string WeakSeam = "SLATE_TEST_ROOT_FILE_ID_INFO_UNSUPPORTED";
    private const string CollideSeam = "SLATE_TEST_ROOT_FILE_INDEX_COLLIDES";

    [Theory]
    [InlineData("retarget")]
    [InlineData("alias")]
    [InlineData("weak-collision")]
    [InlineData("weak-reopen")]
    public void APendingWriteNeverReachesAnotherRootsPreferences(string row)
    {
        string token = $"wb28g{Guid.NewGuid():N}";
        using FixtureVault first = FixtureVault.Create(0, token + "-a");
        using FixtureVault second = FixtureVault.Create(0, token + "-b");
        string spelling = Path.Combine(Path.GetTempPath(), $"slate-graph-spelling-{token}");
        string? priorWeak = Environment.GetEnvironmentVariable(WeakSeam);
        string? priorCollide = Environment.GetEnvironmentVariable(CollideSeam);
        GraphConfig pending = WithDepth(1);
        GraphConfig later = WithDepth(2);
        using var parked = new ManualResetEventSlim(false);
        var writer = new GraphConfigWriter
        {
            WriteGateForTests = (_, aggregate) =>
            {
                if (ReferenceEquals(aggregate, pending))
                {
                    Assert.True(parked.Wait(TimeSpan.FromSeconds(30)), "the parked write was never released");
                }
            },
        };
        VaultSession? a = null;
        VaultSession? b = null;
        try
        {
            switch (row)
            {
                case "retarget":
                    // (a) The spelling T\vault named A when the write was
                    // made, and names B when B's workspace reads.
                    Junctions.Create(spelling, first.Root);
                    a = VaultSession.OpenFilesystem(spelling);
                    Task straggler = writer.Enqueue(VaultRootAnchor.Of(a), pending, writer.Reserve(VaultRootAnchor.Of(a)));
                    a.Dispose();
                    a = null;
                    Junctions.Retarget(spelling, second.Root);
                    b = VaultSession.OpenFilesystem(spelling);
                    Assert.Null(writer.Newest(VaultRootAnchor.Of(b)));
                    parked.Set();
                    Assert.True(straggler.Wait(TimeSpan.FromSeconds(10)));
                    Assert.Equal(0, writer.FailedForTests);
                    Assert.False(File.Exists(GraphJson(second.Root)), "A's write reached B's preferences");
                    Assert.Equal(1U, new GraphConfigStore(first.Root).Read().Config.ConnectionsDepth);
                    break;
                case "alias":
                    // (b) Another spelling of A is A: one scope, so a reopen
                    // through it reads the write still pending.
                    Junctions.Create(spelling, first.Root);
                    a = VaultSession.OpenFilesystem(first.Root);
                    Task aliased = writer.Enqueue(VaultRootAnchor.Of(a), pending, writer.Reserve(VaultRootAnchor.Of(a)));
                    a.Dispose();
                    a = null;
                    b = VaultSession.OpenFilesystem(spelling);
                    Assert.Same(pending, writer.Newest(VaultRootAnchor.Of(b)));
                    parked.Set();
                    Assert.True(aliased.Wait(TimeSpan.FromSeconds(10)));
                    Assert.Equal(0, writer.FailedForTests);
                    Assert.Equal(1U, new GraphConfigStore(first.Root).Read().Config.ConnectionsDepth);
                    break;
                case "weak-collision":
                    // (c) Two folders whose weak identities collide are two
                    // scopes.
                    Environment.SetEnvironmentVariable(WeakSeam, token);
                    Environment.SetEnvironmentVariable(CollideSeam, token);
                    a = VaultSession.OpenFilesystem(first.Root);
                    b = VaultSession.OpenFilesystem(second.Root);
                    Assert.False(a.RootIdentity()!.Persistent, "premise: a weak identity");
                    Assert.Equal(a.RootIdentity(), b.RootIdentity());
                    Task collided = writer.Enqueue(VaultRootAnchor.Of(a), pending, writer.Reserve(VaultRootAnchor.Of(a)));
                    Assert.Null(writer.Newest(VaultRootAnchor.Of(b)));
                    parked.Set();
                    Assert.True(collided.Wait(TimeSpan.FromSeconds(10)));
                    Assert.Equal(0, writer.FailedForTests);
                    Assert.False(File.Exists(GraphJson(second.Root)), "A's write reached B's preferences");
                    break;
                case "weak-reopen":
                    // (d) A weak root reopened is a new scope; the one queue
                    // still lands the reopen's write after the straggler's.
                    Environment.SetEnvironmentVariable(WeakSeam, token);
                    a = VaultSession.OpenFilesystem(first.Root);
                    Assert.False(a.RootIdentity()!.Persistent, "premise: a weak identity");
                    Task old = writer.Enqueue(VaultRootAnchor.Of(a), pending, writer.Reserve(VaultRootAnchor.Of(a)));
                    a.Dispose();
                    a = null;
                    b = VaultSession.OpenFilesystem(first.Root);
                    Task reopened = writer.Enqueue(VaultRootAnchor.Of(b), later, writer.Reserve(VaultRootAnchor.Of(b)));
                    Assert.False(reopened.Wait(TimeSpan.FromMilliseconds(300)), "the reopen's write overtook the straggler");
                    parked.Set();
                    Assert.True(Task.WhenAll(old, reopened).Wait(TimeSpan.FromSeconds(10)));
                    Assert.Equal(0, writer.FailedForTests);
                    Assert.Equal(2U, new GraphConfigStore(first.Root).Read().Config.ConnectionsDepth);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(row), row, null);
            }

        }
        finally
        {
            parked.Set();
            a?.Dispose();
            b?.Dispose();
            Environment.SetEnvironmentVariable(WeakSeam, priorWeak);
            Environment.SetEnvironmentVariable(CollideSeam, priorCollide);
            Junctions.Remove(spelling);
        }
    }

    /// <summary>The store reads and writes the folder its session holds,
    /// whatever its spelling names now (Term W7's read included: main read
    /// the file by its path).</summary>
    [Fact]
    public void TheStoreReadsAndWritesTheHeldRootNotItsSpelling()
    {
        string token = $"wb28s{Guid.NewGuid():N}";
        using FixtureVault first = FixtureVault.Create(0, token + "-a");
        using FixtureVault second = FixtureVault.Create(0, token + "-b");
        string spelling = Path.Combine(Path.GetTempPath(), $"slate-graph-spelling-{token}");
        new GraphConfigStore(second.Root).Write(WithDepth(2));
        Junctions.Create(spelling, first.Root);
        try
        {
            using VaultSession session = VaultSession.OpenFilesystem(spelling);
            var store = new GraphConfigStore(spelling, VaultRootAnchor.Of(session));
            Junctions.Retarget(spelling, second.Root);

            store.Write(WithDepth(1));
            Assert.Equal(1U, store.Read().Config.ConnectionsDepth);
            Assert.Equal(1U, new GraphConfigStore(first.Root).Read().Config.ConnectionsDepth);
            Assert.Equal(2U, new GraphConfigStore(second.Root).Read().Config.ConnectionsDepth);
        }
        finally
        {
            Junctions.Remove(spelling);
        }
    }

    /// <summary>A write queued while its session lived lands in the root it
    /// was edited in, though the session closed and the spelling now names
    /// another folder; the root pinned for it is released with the write
    /// (core's duplicate does not block a rename once the session's own hold
    /// is gone, so the release is read from the pin itself).</summary>
    [Fact]
    public void AQueuedWriteLandsInTheRootItWasEditedIn()
    {
        string token = $"wb28q{Guid.NewGuid():N}";
        using FixtureVault first = FixtureVault.Create(0, token + "-a");
        using FixtureVault second = FixtureVault.Create(0, token + "-b");
        string spelling = Path.Combine(Path.GetTempPath(), $"slate-graph-spelling-{token}");
        using var entered = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        bool released = false;
        VaultRootAnchor? pinned = null;
        var writer = new GraphConfigWriter
        {
            StoreFor = root =>
            {
                pinned = root;
                return new GraphConfigStore(root.DisplayPath, root);
            },
            WriteGateForTests = (_, _) =>
            {
                entered.Set();
                released = release.Wait(TimeSpan.FromSeconds(10));
            },
        };
        Junctions.Create(spelling, first.Root);
        try
        {
            Task write;
            using (VaultSession session = VaultSession.OpenFilesystem(spelling))
            {
                write = writer.Enqueue(VaultRootAnchor.Of(session), WithDepth(1), writer.Reserve(VaultRootAnchor.Of(session)));
                Assert.True(entered.Wait(TimeSpan.FromSeconds(10)), "the write never reached its gate");
            }

            Junctions.Retarget(spelling, second.Root);
            release.Set();
            Assert.True(write.Wait(TimeSpan.FromSeconds(10)));
            Assert.True(released, "the gate timed out");
            Assert.Equal(0, writer.FailedForTests);
            Assert.Equal(1U, new GraphConfigStore(first.Root).Read().Config.ConnectionsDepth);
            Assert.False(File.Exists(GraphJson(second.Root)), "the write reached the decoy");
            Assert.True(Assert.IsType<VaultRootAnchor>(pinned).IsDisposedForTests, "the pin outlived its write");
        }
        finally
        {
            release.Set();
            Junctions.Remove(spelling);
        }
    }

    /// <summary>The workspace's preferences key and write by the root their
    /// session holds, not by the spelling they were built with.</summary>
    [Fact]
    public void ThePreferencesWriteTheRootTheirSessionHolds()
    {
        string token = $"wb28p{Guid.NewGuid():N}";
        using FixtureVault first = FixtureVault.Create(0, token + "-a");
        using FixtureVault second = FixtureVault.Create(0, token + "-b");
        string spelling = Path.Combine(Path.GetTempPath(), $"slate-graph-spelling-{token}");
        Junctions.Create(spelling, first.Root);
        try
        {
            using VaultSession session = VaultSession.OpenFilesystem(spelling);
            PumpedDispatcher.Run(() =>
            {
                var writer = new GraphConfigWriter();
                var preferences = new GraphPreferencesViewModel(spelling, writer, root: VaultRootAnchor.Of(session));
                Junctions.Retarget(spelling, second.Root);
                preferences.SetMode(GraphSurfaceMode.Table);
                preferences.Shutdown();
                Assert.True(preferences.WhenWritesDrained().Wait(TimeSpan.FromSeconds(10)));
                Assert.Equal(0, writer.FailedForTests);
            });
            Assert.Equal(GraphSurfaceMode.Table, new GraphConfigStore(first.Root).Read().Config.Mode);
            Assert.False(File.Exists(GraphJson(second.Root)), "the preferences wrote through their spelling");
        }
        finally
        {
            Junctions.Remove(spelling);
        }
    }

    private static string GraphJson(string vaultRoot) =>
        Path.Combine(vaultRoot, ".slate", GraphConfigStore.FileName);
}
