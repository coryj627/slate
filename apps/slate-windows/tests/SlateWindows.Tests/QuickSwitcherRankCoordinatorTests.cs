// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Concurrent;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

public sealed class QuickSwitcherRankCoordinatorTests
{
    [Fact]
    public async Task SupersededQueriesSerializeAndPublishOnlyTheNewestResult()
    {
        using FixtureVault fixture = FixtureVault.Create(1, "quick-rank-supersede");
        using VaultSession session = VaultSession.OpenFilesystem(fixture.Root);
        using var firstStarted = new ManualResetEventSlim();
        using var releaseFirst = new ManualResetEventSlim();
        using var secondStarted = new ManualResetEventSlim();
        var context = new PumpSynchronizationContext();
        var coordinator = new QuickSwitcherRankCoordinator();
        var announcements = new List<A11yEvent>();
        var enteredQueries = new ConcurrentQueue<string>();
        int active = 0;
        int maximum = 0;

        SwitcherRankPage Rank(SwitcherFile[] _, string query, string[] __)
        {
            enteredQueries.Enqueue(query);
            int current = Interlocked.Increment(ref active);
            UpdateMaximum(ref maximum, current);
            try
            {
                if (query.Length == 0)
                {
                    firstStarted.Set();
                    releaseFirst.Wait();
                    throw new InvalidOperationException("sensitive stale rank failure");
                }

                secondStarted.Set();
                return Page("latest.md");
            }
            finally
            {
                Interlocked.Decrement(ref active);
            }
        }

        using QuickSwitcherViewModel quick = CreateQuick(
            session,
            fixture.Root,
            context,
            announcements.Add,
            coordinator,
            Rank);
        try
        {
            quick.Open();
            Task first = quick.RankCompletion;
            Assert.True(firstStarted.Wait(TimeSpan.FromSeconds(5)));
            Assert.True(quick.IsRanking);
            Assert.Empty(quick.Results);

            var completions = new List<Task> { first };
            for (int index = 0; index < 20; index++)
            {
                quick.Query = $"obsolete-{index}";
                completions.Add(quick.RankCompletion);
            }
            quick.Query = "latest";
            Task second = quick.RankCompletion;
            completions.Add(second);
            Assert.False(secondStarted.Wait(TimeSpan.FromMilliseconds(200)));
            Assert.Equal(1, Volatile.Read(ref maximum));

            releaseFirst.Set();
            await Task.WhenAll(completions).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(secondStarted.IsSet);
            context.Drain();

            Assert.Equal(1, Volatile.Read(ref maximum));
            Assert.Equal([string.Empty, "latest"], enteredQueries.ToArray());
            Assert.Equal("latest.md", Assert.Single(quick.Results).Path);
            Assert.False(quick.IsRanking);
            A11yEvent.QuickSwitcherCount count = Assert.IsType<A11yEvent.QuickSwitcherCount>(
                Assert.Single(announcements));
            Assert.Equal("latest", count.Query);
        }
        finally
        {
            releaseFirst.Set();
        }
    }

    [Fact]
    public async Task DefaultCoordinatorSerializesAcrossDisposedViewModelLifetimes()
    {
        using FixtureVault fixture = FixtureVault.Create(1, "quick-rank-lifetimes");
        using VaultSession session = VaultSession.OpenFilesystem(fixture.Root);
        using var firstStarted = new ManualResetEventSlim();
        using var releaseFirst = new ManualResetEventSlim();
        using var secondStarted = new ManualResetEventSlim();
        var context = new PumpSynchronizationContext();
        var firstAnnouncements = new List<A11yEvent>();
        var secondAnnouncements = new List<A11yEvent>();
        int active = 0;
        int maximum = 0;

        SwitcherRankPage FirstRank(SwitcherFile[] _, string __, string[] ___)
        {
            int current = Interlocked.Increment(ref active);
            UpdateMaximum(ref maximum, current);
            try
            {
                firstStarted.Set();
                releaseFirst.Wait();
                return Page("first.md");
            }
            finally
            {
                Interlocked.Decrement(ref active);
            }
        }

        SwitcherRankPage SecondRank(SwitcherFile[] _, string __, string[] ___)
        {
            int current = Interlocked.Increment(ref active);
            UpdateMaximum(ref maximum, current);
            try
            {
                secondStarted.Set();
                return Page("second.md");
            }
            finally
            {
                Interlocked.Decrement(ref active);
            }
        }

        QuickSwitcherViewModel first = CreateQuick(
            session,
            fixture.Root,
            context,
            firstAnnouncements.Add,
            rankTop: FirstRank);
        using QuickSwitcherViewModel second = CreateQuick(
            session,
            fixture.Root,
            context,
            secondAnnouncements.Add,
            rankTop: SecondRank);
        try
        {
            first.Open();
            Task firstCompletion = first.RankCompletion;
            Assert.True(firstStarted.Wait(TimeSpan.FromSeconds(5)));
            first.Dispose();

            second.Open();
            Task secondCompletion = second.RankCompletion;
            Assert.False(secondStarted.Wait(TimeSpan.FromMilliseconds(200)));
            Assert.Equal(1, Volatile.Read(ref maximum));

            releaseFirst.Set();
            await Task.WhenAll(firstCompletion, secondCompletion)
                .WaitAsync(TimeSpan.FromSeconds(5));
            context.Drain();

            Assert.True(secondStarted.IsSet);
            Assert.Equal(1, Volatile.Read(ref maximum));
            Assert.Empty(first.Results);
            Assert.False(first.IsRanking);
            Assert.Equal("No matching files", first.ResultSummary);
            Assert.Empty(firstAnnouncements);
            Assert.Equal("second.md", Assert.Single(second.Results).Path);
            Assert.Single(secondAnnouncements);
        }
        finally
        {
            releaseFirst.Set();
            first.Dispose();
        }
    }

    [Fact]
    public async Task CancelledQueuedRankNeverEntersNativeWork()
    {
        var coordinator = new QuickSwitcherRankCoordinator();
        using var firstStarted = new ManualResetEventSlim();
        using var releaseFirst = new ManualResetEventSlim();
        int queuedEntries = 0;

        Task<SwitcherRankPage> first = coordinator.RankAsync(
            () =>
            {
                firstStarted.Set();
                releaseFirst.Wait();
                return Page("first.md");
            },
            CancellationToken.None);

        try
        {
            Assert.True(firstStarted.Wait(TimeSpan.FromSeconds(5)));
            using var cancellation = new CancellationTokenSource();
            Task<SwitcherRankPage> queued = coordinator.RankAsync(
                () =>
                {
                    Interlocked.Increment(ref queuedEntries);
                    return Page("cancelled.md");
                },
                cancellation.Token);
            Assert.False(queued.IsCompleted);

            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                async () => await queued.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        finally
        {
            releaseFirst.Set();
            await first.WaitAsync(TimeSpan.FromSeconds(5));
        }

        Assert.Equal(0, Volatile.Read(ref queuedEntries));
    }

    [Fact]
    public async Task RankFailureReleasesTheProcessLane()
    {
        var coordinator = new QuickSwitcherRankCoordinator();

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await coordinator.RankAsync(
                () => throw new InvalidOperationException("simulated rank failure"),
                CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5)));

        SwitcherRankPage recovered = await coordinator.RankAsync(
            () => Page("recovered.md"),
            CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("recovered.md", Assert.Single(recovered.Rows).Path);
    }

    [Fact]
    public async Task ActiveRankFailurePublishesGenericTerminalError()
    {
        using FixtureVault fixture = FixtureVault.Create(1, "quick-rank-failure");
        using VaultSession session = VaultSession.OpenFilesystem(fixture.Root);
        var context = new PumpSynchronizationContext();
        var announcements = new List<A11yEvent>();
        using QuickSwitcherViewModel quick = CreateQuick(
            session,
            fixture.Root,
            context,
            announcements.Add,
            new QuickSwitcherRankCoordinator(),
            (_, _, _) => throw new InvalidOperationException("sensitive rank detail"));

        quick.Open();
        await quick.RankCompletion.WaitAsync(TimeSpan.FromSeconds(5));
        context.Drain();

        const string expected = "Quick Open could not rank files.";
        Assert.False(quick.IsRanking);
        Assert.Empty(quick.Results);
        Assert.Equal(expected, quick.ResultSummary);
        Assert.DoesNotContain("sensitive rank detail", quick.ResultSummary, StringComparison.Ordinal);
        A11yEvent.HostComposed failure = Assert.IsType<A11yEvent.HostComposed>(
            Assert.Single(announcements));
        Assert.Equal(expected, failure.Text);
        Assert.Equal(A11yPriority.High, failure.Priority);
        Assert.DoesNotContain("sensitive rank detail", failure.Text, StringComparison.Ordinal);
    }

    private static QuickSwitcherViewModel CreateQuick(
        VaultSession session,
        string vaultRoot,
        SynchronizationContext context,
        Action<A11yEvent> announce,
        QuickSwitcherRankCoordinator? coordinator = null,
        Func<SwitcherFile[], string, string[], SwitcherRankPage>? rankTop = null)
    {
        SynchronizationContext? previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            return new QuickSwitcherViewModel(
                session,
                vaultRoot,
                announce,
                [new SwitcherFile("note.md", "note.md")],
                Path.Combine(vaultRoot, $"device-state-{Guid.NewGuid():N}"),
                rankCoordinator: coordinator,
                rankTop: rankTop);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    private static SwitcherRankPage Page(string path) =>
        new(
            [new SwitcherRow(path, path, Path.GetFileNameWithoutExtension(path), 0, [])],
            1);

    private static void UpdateMaximum(ref int maximum, int candidate)
    {
        int observed;
        do
        {
            observed = Volatile.Read(ref maximum);
            if (candidate <= observed)
            {
                return;
            }
        }
        while (Interlocked.CompareExchange(ref maximum, candidate, observed) != observed);
    }

    private sealed class PumpSynchronizationContext : SynchronizationContext
    {
        private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _queue = [];

        public override void Post(SendOrPostCallback callback, object? state) =>
            _queue.Enqueue((callback, state));

        public void Drain()
        {
            while (_queue.TryDequeue(out var work))
            {
                work.Callback(work.State);
            }
        }
    }
}
