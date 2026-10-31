// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Concurrent;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// WB-25 stage 2 (#1290; locked decision 05 §9.3.1): Quick Open ranks
/// against core's index. The host holds and marshals no file list: a
/// keystroke hands core its query, the recents and a token, and a
/// superseded keystroke's token stops its query inside core.
/// </summary>
public sealed class QuickSwitcherViewModelTests
{
    /// <summary>The rank seam receives only the query, the recents and a
    /// token — there is no file list to pass (the switcher's old
    /// <c>FilePathsForTests</c> list is gone, which the compiler holds) —
    /// and the default seam ranks the vault's index: a scanned note is a
    /// result though nothing listed it to the host.</summary>
    [Fact]
    public void AKeystrokeRanksThroughCoreWithNoHostFileList()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "quick-open-indexed");
        File.WriteAllText(Path.Combine(fixture.Root, "alpha.md"), "alpha\n");
        File.WriteAllText(Path.Combine(fixture.Root, "beta.md"), "beta\n");
        using VaultSession session = VaultSession.OpenFilesystem(fixture.Root);
        using (var cancel = new CancelToken())
        {
            session.ScanInitial(cancel);
        }

        var calls = new List<(string Query, string[] Recents, CancelToken Token)>();
        using var seamed = new QuickSwitcherViewModel(
            session,
            fixture.Root,
            _ => { },
            Path.Combine(fixture.Root, "device-state-seam"),
            debounceRanking: false,
            rankTop: (query, recents, token) =>
            {
                calls.Add((query, recents, token));
                return session.SwitcherRankTopIndexed(query, recents, QuickSwitcherViewModel.DisplayCap, token);
            });
        seamed.RecordOpen("beta.md");

        seamed.Open();
        seamed.Query = "alp";

        Assert.Equal(["", "alp"], calls.Select(call => call.Query));
        Assert.All(calls, call => Assert.Equal(["beta.md"], call.Recents));
        Assert.All(calls, call => Assert.NotNull(call.Token));
        Assert.Equal("alpha.md", Assert.Single(seamed.Results).Path);

        using var indexed = new QuickSwitcherViewModel(
            session,
            fixture.Root,
            _ => { },
            Path.Combine(fixture.Root, "device-state-index"),
            debounceRanking: false);
        indexed.Open();
        Assert.Equal(["alpha.md", "beta.md"], indexed.Results.Select(row => row.Path).Order(StringComparer.Ordinal));
        Assert.Equal(2, indexed.TotalResults);
    }

    /// <summary>A keystroke that supersedes a rank still running in core
    /// cancels that rank's core token, so the stale query stops inside core
    /// rather than running to its end.</summary>
    [Fact]
    public async Task ASupersededKeystrokeCancelsTheCoreQuery()
    {
        using FixtureVault fixture = FixtureVault.Create(1, "quick-open-supersede-core");
        using VaultSession session = VaultSession.OpenFilesystem(fixture.Root);
        using var firstInCore = new ManualResetEventSlim();
        var context = new PumpSynchronizationContext();
        bool firstSawCancel = false;
        bool secondCancelled = true;

        SwitcherRankPage Rank(string query, string[] _, CancelToken token)
        {
            if (query == "first")
            {
                firstInCore.Set();
                // Core polls its token per row: here, until it is cancelled.
                firstSawCancel = SpinWait.SpinUntil(token.IsCancelled, TimeSpan.FromSeconds(10));
            }

            else if (query == "second")
            {
                secondCancelled = token.IsCancelled();
            }

            return new SwitcherRankPage([], 0);
        }

        SynchronizationContext? previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(context);
        QuickSwitcherViewModel quick;
        try
        {
            quick = new QuickSwitcherViewModel(
                session,
                fixture.Root,
                _ => { },
                Path.Combine(fixture.Root, "device-state"),
                rankCoordinator: new QuickSwitcherRankCoordinator(),
                rankTop: Rank,
                rankDelay: _ => Task.CompletedTask);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        using (quick)
        {
            quick.Open();
            await quick.RankCompletion.WaitAsync(TimeSpan.FromSeconds(5));
            context.Drain();

            quick.Query = "first";
            Task first = quick.RankCompletion;
            Assert.True(firstInCore.Wait(TimeSpan.FromSeconds(5)), "the first rank never reached core");

            quick.Query = "second";
            Task second = quick.RankCompletion;
            await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(15));
            context.Drain();

            Assert.True(firstSawCancel, "the superseded keystroke's core query was never cancelled");
            Assert.False(secondCancelled, "the current keystroke's core query was cancelled");
        }
    }

    /// <summary>A change-feed page's records are already in the index, so a
    /// page that names a file re-ranks an open switcher silently, awaited to
    /// that publication; a folder-only page, or a closed switcher, ranks
    /// nothing.</summary>
    [Fact]
    public async Task AChangeFeedPageNamingAFileReRanksAnOpenSwitcher()
    {
        using FixtureVault fixture = FixtureVault.Create(1, "quick-open-feed-page");
        using VaultSession session = VaultSession.OpenFilesystem(fixture.Root);
        int ranks = 0;
        using var quick = new QuickSwitcherViewModel(
            session,
            fixture.Root,
            _ => { },
            Path.Combine(fixture.Root, "device-state"),
            debounceRanking: false,
            rankTop: (_, _, _) =>
            {
                ranks++;
                return new SwitcherRankPage([], 0);
            });
        ChangeRecord File(ChangeEntry entry) => new(
            "made.md", entry, ChangeEffect.Present, null, null, ChangeCount.Created, null, null, true, 1);

        await quick.ApplyRecordsAsync([File(ChangeEntry.File)], CancellationToken.None);
        Assert.Equal(0, ranks);

        quick.Open();
        Assert.Equal(1, ranks);
        await quick.ApplyRecordsAsync([File(ChangeEntry.Directory)], CancellationToken.None);
        Assert.Equal(1, ranks);
        await quick.ApplyRecordsAsync([File(ChangeEntry.File)], CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, ranks);
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
