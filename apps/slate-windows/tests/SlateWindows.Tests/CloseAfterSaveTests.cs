// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Threading;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// XB-11 (#1573, #1590; plan 45 XD1): a close after a save is never refused
/// for the tree refresh Slate's own save scheduled. A close that has begun
/// skips the save's debounced refresh, cancels and awaits a tree read already
/// in flight (bounded), and closes; only a read past the bound refuses, with
/// the sentence it always had. A close that stays open re-issues the refresh
/// it skipped. Every interleaving is placed by a held tree read, a queued
/// dispatcher callback or a seam, never by sleeping, as in
/// <see cref="PumpedSaveReentrancyTests"/>, whose host these facts share.
/// </summary>
public sealed class CloseAfterSaveTests
{
    private const string RefusalSentence =
        "File tree refresh cancellation requested. Close the vault again after the current directory read finishes.";

    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(10);

    private static bool AnnouncedTheRefusal(PumpedSaveReentrancyTests.Host host) =>
        host.Announced.Any(item => item.Event is A11yEvent.HostComposed { Text: RefusalSentence });

    /// <summary>The save's refresh is scheduled when the close begins: the
    /// fact closes as soon as the save's file change has been handled, well
    /// inside the 150 ms debounce. The unsaved-changes prompt pumps, as the
    /// modal dialog does, and the debounce runs out inside it. The close
    /// skips it.</summary>
    [Fact]
    public void ACloseInsideTheSavesDebounceWindowClosesWithoutARefusal()
    {
        using var host = new PumpedSaveReentrancyTests.Host(VaultCloseDecision.Discard);
        host.G1.ActiveTab = host.S;
        host.Type(host.S, "Marker-XB11-A");
        int changesBefore = host.Lifecycle.FileChangesHandledForTests;
        host.Workspace.SaveActiveCommand.Execute(null);
        Assert.True(
            PumpedDispatcher.PumpUntil(() => host.Lifecycle.FileChangesHandledForTests > changesBefore, Budget),
            "the save's file change never arrived");
        Assert.True(
            host.Lifecycle.DebouncedSidebarRefreshPendingForTests,
            "the save's tree refresh was not pending when its change was handled");
        int readsBefore = host.TreeReadsStarted;
        host.ClosePrompt = () =>
        {
            Assert.True(
                PumpedDispatcher.PumpUntil(() => !host.Lifecycle.DebouncedSidebarRefreshPendingForTests, Budget),
                "the save's debounce never ran out inside the close");
            return VaultCloseDecision.Discard;
        };

        host.Lifecycle.CloseVault();

        Assert.False(host.Lifecycle.IsVaultOpen, "the close was refused");
        Assert.False(AnnouncedTheRefusal(host), "the close spoke the refusal");
        PumpedDispatcher.Drain();
        Assert.Equal(readsBefore, host.TreeReadsStarted);
    }

    /// <summary>The save's refresh is reading when the close begins (the
    /// read is held). The close cancels it and waits; a release queued
    /// before the close runs inside that wait, so the wait pumps and the
    /// read ends inside it, and the close closes.</summary>
    [Fact]
    public void ACloseDuringTheSavesTreeReadAwaitsTheReadThenCloses()
    {
        using var host = new PumpedSaveReentrancyTests.Host(VaultCloseDecision.Discard);
        host.G1.ActiveTab = host.S;
        host.Type(host.S, "Marker-XB11-B");
        host.HoldTreeReads();
        host.Workspace.SaveActiveAndSettle();
        Assert.True(
            PumpedDispatcher.PumpUntil(() => host.TreeReadHeld.IsSet, Budget),
            "the save's tree refresh never started reading");
        FilesSidebarViewModel sidebar = Assert.IsType<FilesSidebarViewModel>(host.Lifecycle.FileSidebar);
        Assert.True(sidebar.IsRefreshingTree, "the save's tree read is not in flight");
        host.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(host.ReleaseTreeReads));

        host.Lifecycle.CloseVault();

        Assert.False(host.Lifecycle.IsVaultOpen, "the close was refused while the save's tree read was in flight");
        Assert.False(AnnouncedTheRefusal(host), "the close spoke the refusal");
        Assert.Equal("Vault closed.", host.Lifecycle.StatusText);
        Assert.True(sidebar.TreeRefreshCompletion.IsCompleted, "the close did not wait for the read");
    }

    /// <summary>A read that outlives the bound still refuses, with the
    /// sentence, word for word, at Medium priority, before the prompt; once
    /// the read ends, the next close closes.</summary>
    [Fact]
    public void ACloseWhoseTreeReadOutlivesTheBoundIsRefusedWithTheSentence()
    {
        TimeSpan bound = VaultLifecycleViewModel.TreeRefreshCloseBound;
        VaultLifecycleViewModel.TreeRefreshCloseBound = TimeSpan.FromMilliseconds(200);
        try
        {
            using var host = new PumpedSaveReentrancyTests.Host(VaultCloseDecision.Discard);
            host.G1.ActiveTab = host.S;
            host.Type(host.S, "Marker-XB11-C");
            host.HoldTreeReads();
            host.Workspace.SaveActiveAndSettle();
            Assert.True(
                PumpedDispatcher.PumpUntil(() => host.TreeReadHeld.IsSet, Budget),
                "the save's tree refresh never started reading");
            host.Announced.Clear();
            int prompts = host.ClosePrompts;
            FilesSidebarViewModel sidebar = Assert.IsType<FilesSidebarViewModel>(host.Lifecycle.FileSidebar);
            Task stuck = sidebar.TreeRefreshCompletion;
            // A guard, not the schedule: were the wait unbounded, the read
            // would end here and the close would close instead of refusing.
            using var guard = new Timer(_ => host.ReleaseTreeReads(), null, Budget, Timeout.InfiniteTimeSpan);

            host.Lifecycle.CloseVault();

            Assert.True(host.Lifecycle.IsVaultOpen, "a close whose read outlived the bound closed");
            Assert.Equal(RefusalSentence, host.Lifecycle.StatusText);
            Assert.Contains(
                host.Announced,
                item => item.Event is A11yEvent.HostComposed { Text: RefusalSentence, Priority: A11yPriority.Medium });
            Assert.Equal(prompts, host.ClosePrompts);
            // No read is re-issued past the bound: on a vault whose reads are
            // slow, the next close would only meet another (a re-issued
            // refresh would replace the sidebar's completion).
            Assert.Same(stuck, sidebar.TreeRefreshCompletion);

            // Once the stuck read ends, the next close closes, with the usual
            // bound.
            host.ReleaseTreeReads();
            VaultLifecycleViewModel.TreeRefreshCloseBound = bound;
            host.Lifecycle.CloseVault();
            Assert.False(host.Lifecycle.IsVaultOpen, "the close after the read ended did not close");
        }
        finally
        {
            VaultLifecycleViewModel.TreeRefreshCloseBound = bound;
        }
    }

    /// <summary>The window's close can pump while it waits (the tree read
    /// above), with the window enabled. File > Exit from inside that frame
    /// is answered by the close already deciding; it must not call
    /// <c>Close()</c> on a window that is closing, which throws before the
    /// unsaved-changes prompt.</summary>
    [Fact]
    public void FileExitInsideTheWindowsOwnCloseDoesNotThrow() =>
        StaThread.Run(
            () =>
            {
                var shell = new MainWindow();
                Exception? thrown = null;
                bool ran = false;
                shell.Closing += (_, _) =>
                {
                    ran = true;
                    try
                    {
                        System.Windows.Input.ApplicationCommands.Close.Execute(null, shell);
                    }
                    catch (InvalidOperationException exception)
                    {
                        thrown = exception;
                    }
                };
                shell.Show();
                shell.Close();
                Assert.True(ran, "the window's Closing never ran");
                Assert.Null(thrown);
            },
            TimeSpan.FromSeconds(60),
            "the window's close never returned");

    /// <summary>The save is admitted, not settled, when the close begins, so
    /// its file change lands inside the close and its refresh is skipped. The
    /// prompt's Cancel keeps the vault open, and the close re-issues that one
    /// refresh, so the tree does not stay stale.</summary>
    [Fact]
    public void ARefusedCloseReissuesTheRefreshItSkipped()
    {
        using var host = new PumpedSaveReentrancyTests.Host(VaultCloseDecision.Cancel);
        host.G1.ActiveTab = host.S;
        host.Type(host.S, "Marker-XB11-D");
        int changesBefore = host.Lifecycle.FileChangesHandledForTests;
        host.Workspace.SaveActiveCommand.Execute(null);
        host.ClosePrompt = () =>
        {
            Assert.True(
                PumpedDispatcher.PumpUntil(() => host.Lifecycle.FileChangesHandledForTests > changesBefore, Budget),
                "the save's file change never landed inside the close");
            return VaultCloseDecision.Cancel;
        };
        int readsBefore = host.TreeReadsStarted;

        host.Lifecycle.CloseVault();

        Assert.True(host.Lifecycle.IsVaultOpen, "Cancel must keep the vault open");
        Assert.False(
            host.Lifecycle.DebouncedSidebarRefreshPendingForTests,
            "the change inside the close scheduled its own refresh");
        Assert.True(
            PumpedDispatcher.PumpUntil(() => host.TreeReadsStarted > readsBefore, Budget),
            "the refused close never re-issued the refresh it skipped");
        host.Settle();
        Assert.Equal(readsBefore + 1, host.TreeReadsStarted);
    }
}
