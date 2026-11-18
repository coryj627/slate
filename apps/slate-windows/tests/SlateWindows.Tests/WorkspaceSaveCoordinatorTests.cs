// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Threading;

namespace SlateWindows.Tests;

/// <summary>
/// WB-2 (#1322, #1325; contract 38 D-10): <see cref="WorkspaceSaveCoordinator.Admit"/>
/// takes a ticket under the file's chain — even an idle one — and holds it
/// until the operation's own Task ends.
/// </summary>
public sealed class WorkspaceSaveCoordinatorTests
{
    private static void RunSta(Action body) =>
        StaThread.Run(body, TimeSpan.FromSeconds(60), "STA test body timed out.");

    [Fact]
    public void AdmitAlwaysReservesThePathEvenOnAnIdleChain() => RunSta(() =>
    {
        var saves = new WorkspaceSaveCoordinator(Dispatcher.CurrentDispatcher);
        var operation = new TaskCompletionSource();
        bool ran = false;
        Task<bool> admitted = saves.Admit("Notes/A.md", _ =>
        {
            ran = true;
            return operation.Task;
        });
        Assert.True(ran, "an idle chain did not run the operation in this turn");
        Assert.True(saves.HasAdmittedSaveFor("notes/a.md"), "the operation did not reserve its file's chain");

        bool started = false;
        Task<bool> save = saves.Enqueue(
            WorkspaceSaveCoordinator.KeysFor(Guid.NewGuid(), "Notes/A.md"),
            ticket =>
            {
                started = true;
                ticket.Complete(true);
            });
        PumpedDispatcher.Drain();
        Assert.False(started, "a save admitted after the operation started beside it");

        operation.SetResult();
        Assert.True(PumpedDispatcher.PumpUntil(() => save.IsCompleted, TimeSpan.FromSeconds(10)));
        Assert.True(started);
        Assert.True(admitted.Result);
        Assert.True(save.Result);
        Assert.True(saves.IsIdle);
    });

    [Fact]
    public void AdmitCompletesItsTicketOnlyWhenTheOperationsTaskCompletes() => RunSta(() =>
    {
        var saves = new WorkspaceSaveCoordinator(Dispatcher.CurrentDispatcher);
        var operation = new TaskCompletionSource();
        Task<bool> admitted = saves.Admit("a.md", _ => operation.Task);
        PumpedDispatcher.Drain();
        Assert.False(admitted.IsCompleted, "the ticket completed when the operation returned");
        Assert.False(saves.IsIdle);
        Assert.True(saves.HasAdmittedSaveFor("a.md"));

        operation.SetResult();
        Assert.True(PumpedDispatcher.PumpUntil(() => admitted.IsCompleted, TimeSpan.FromSeconds(10)));
        Assert.True(admitted.Result);
        Assert.True(saves.IsIdle);
        Assert.False(saves.HasAdmittedSaveFor("a.md"));

        // A faulted operation fails its ticket (logged once) and frees the chain.
        var faulting = new TaskCompletionSource();
        Task<bool> failed = saves.Admit("a.md", _ => faulting.Task);
        faulting.SetException(new InvalidOperationException("the operation failed (test)"));
        Assert.True(PumpedDispatcher.PumpUntil(() => failed.IsCompleted, TimeSpan.FromSeconds(10)));
        Assert.True(failed.IsFaulted);
        Assert.True(saves.IsIdle);
        Assert.False(saves.HasAdmittedSaveFor("a.md"));
    });

    /// <summary>Plan 47 CB-9b (#1889; CD5(a)): an admission over several
    /// notes takes one ticket under every note's chain. It runs only once the
    /// operation admitted for one of them before it has ended, and holds each
    /// note's chain until its own Task ends, so a save of any of them asked
    /// meanwhile waits for it. Spellings that fold to one chain are one note,
    /// and a note it does not name is not held.</summary>
    [Fact]
    public void AnAdmissionOverSeveralNotesWaitsForEachAndHoldsEachUntilItEnds() => RunSta(() =>
    {
        var saves = new WorkspaceSaveCoordinator(Dispatcher.CurrentDispatcher);
        var ahead = new TaskCompletionSource();
        Task<bool> first = saves.Admit("b.md", _ => ahead.Task);
        var operation = new TaskCompletionSource();
        bool ran = false;
        Task<bool> admitted = saves.Admit(["a.md", "B.md", "c.md", "A.md"], _ =>
        {
            ran = true;
            return operation.Task;
        });
        Assert.Equal(2, saves.AdmittedForTests);
        PumpedDispatcher.Drain();
        Assert.False(ran, "the operation ran beside the operation admitted for b.md before it");
        foreach (string path in new[] { "a.md", "b.md", "c.md" })
        {
            Assert.True(saves.HasAdmittedSaveFor(path), $"the admission did not hold {path}'s chain");
        }
        Assert.False(saves.HasAdmittedSaveFor("d.md"), "the admission held a note it does not name");

        bool savedC = false;
        Task<bool> saveC = saves.Enqueue(
            WorkspaceSaveCoordinator.KeysFor(Guid.NewGuid(), "c.md"),
            ticket =>
            {
                savedC = true;
                ticket.Complete(true);
            });
        bool savedD = false;
        Task<bool> saveD = saves.Enqueue(
            WorkspaceSaveCoordinator.KeysFor(Guid.NewGuid(), "d.md"),
            ticket =>
            {
                savedD = true;
                ticket.Complete(true);
            });
        Assert.True(savedD, "a save of a note the admission does not name waited for it");

        ahead.SetResult();
        Assert.True(PumpedDispatcher.PumpUntil(() => ran, TimeSpan.FromSeconds(10)), "the operation never ran");
        PumpedDispatcher.Drain();
        Assert.False(savedC, "a save of c.md started beside the operation that holds its chain");

        operation.SetResult();
        Assert.True(PumpedDispatcher.PumpUntil(() => admitted.IsCompleted && saveC.IsCompleted, TimeSpan.FromSeconds(10)));
        Assert.True(first.Result);
        Assert.True(admitted.Result);
        Assert.True(saveC.Result);
        Assert.True(saveD.Result);
        Assert.True(saves.IsIdle);
        foreach (string path in new[] { "a.md", "b.md", "c.md" })
        {
            Assert.False(saves.HasAdmittedSaveFor(path));
        }
    });

    [Fact]
    public void AdmitOnAClosedCoordinatorReturnsFalseAndRunsNothing() => RunSta(() =>
    {
        var saves = new WorkspaceSaveCoordinator(Dispatcher.CurrentDispatcher);
        saves.CloseAndJoinWorkers();
        bool ran = false;
        Task<bool> admitted = saves.Admit("a.md", _ =>
        {
            ran = true;
            return Task.CompletedTask;
        });
        Assert.True(admitted.IsCompleted);
        Assert.False(admitted.Result);
        Assert.False(ran);
        Assert.True(saves.IsIdle);
    });
}
