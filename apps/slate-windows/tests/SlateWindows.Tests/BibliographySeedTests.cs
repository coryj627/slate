// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Panels;

namespace SlateWindows.Tests;

/// <summary>
/// W4-5 (#737): the initialization-lifecycle design pass. The seed is
/// a TERMINAL OUTCOME, not a gate — the leaves need to know what
/// happened, not merely when it finished.
/// </summary>
public sealed class BibliographySeedTests
{
    /// <summary>
    /// The trap this design exists to avoid: a cancelled TASK throws
    /// out of the awaiting continuation into PanelWorkScheduler's
    /// catch, which swallows it and runs the body anyway — so the
    /// cancellation would be silently ineffective. Cancellation is a
    /// STATUS, and the task completes normally.
    /// </summary>
    [Fact]
    public async Task CancellationIsAStatusAndNeverAFaultedOrCancelledTask()
    {
        var seed = new BibliographySeed();
        seed.Cancel();

        BibliographySeedOutcome outcome = await seed.Completion;

        Assert.Equal(TaskStatus.RanToCompletion, seed.Completion.Status);
        Assert.Equal(BibliographySeedStatus.Cancelled, outcome.Status);
        Assert.False(outcome.MayReadEntries);
    }

    /// <summary>Teardown racing a landing seed must not overwrite a
    /// real outcome, nor the reverse. First settle wins.</summary>
    [Fact]
    public async Task TheFirstSettleWinsAndLaterOnesAreIgnored()
    {
        var seed = new BibliographySeed();
        seed.Complete(new BibliographySeedOutcome(BibliographySeedStatus.Seeded, []));
        seed.Cancel();

        Assert.Equal(BibliographySeedStatus.Seeded, (await seed.Completion).Status);
        Assert.Equal(BibliographySeedStatus.Seeded, seed.Outcome!.Status);
    }

    /// <summary>A waiter that wakes on Completion must always find a
    /// settled Outcome — the leaves read it the moment they wake.
    /// </summary>
    [Fact]
    public async Task OutcomeIsVisibleBeforeWaitersWake()
    {
        var seed = new BibliographySeed();
        Task<BibliographySeedOutcome?> observed = seed.Completion.ContinueWith(
            _ => seed.Outcome, TaskScheduler.Default);

        seed.Complete(new BibliographySeedOutcome(BibliographySeedStatus.Seeded, []));

        Assert.NotNull(await observed);
    }

    /// <summary>
    /// Only Seeded and NoSources permit reading core. Failed must not:
    /// core's set_bibliography_sources is all-or-nothing and returns
    /// BEFORE replacing anything, so the previous session's entries and
    /// index are still live and querying would answer from them.
    /// </summary>
    [Fact]
    public void OnlyASettledSuccessPermitsReadingCore()
    {
        // Exhaustive over the enum, so a new status cannot be added
        // without deciding which side of this line it falls on.
        var expected = new Dictionary<BibliographySeedStatus, bool>
        {
            [BibliographySeedStatus.Seeded] = true,
            [BibliographySeedStatus.NoSources] = true,
            [BibliographySeedStatus.Failed] = false,
            [BibliographySeedStatus.Cancelled] = false,
        };
        Assert.Equal(
            Enum.GetValues<BibliographySeedStatus>().Length,
            expected.Count);

        foreach ((BibliographySeedStatus status, bool mayRead) in expected)
        {
            Assert.Equal(
                mayRead,
                new BibliographySeedOutcome(status, []).MayReadEntries);
        }
    }

    /// <summary>"No sources configured" is not "sources failed" —
    /// contract 5 keeps them distinct all the way down.</summary>
    [Fact]
    public void NoSourcesIsDistinctFromFailed()
    {
        Assert.False(
            new BibliographySeedOutcome(BibliographySeedStatus.NoSources, []).HasSources);
        Assert.True(
            new BibliographySeedOutcome(BibliographySeedStatus.Failed, []).HasSources);
    }
}
