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
