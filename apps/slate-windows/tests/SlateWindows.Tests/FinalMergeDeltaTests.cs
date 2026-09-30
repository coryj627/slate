// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W7-7 PR 7 (#1252), codex's final merge-delta check (on 66193dfd), on the
/// production-scheduled fixture of follow-up B's pumped saves: a real vault
/// opened through the vault lifecycle, its file events arriving on the
/// dispatcher as the production listener delivers them, every save writing
/// off the dispatcher. (1) A Created event never re-seats a tab under a save
/// admitted for its file (contract 38 D-10, R-9). (2) A rescan's same-item
/// reload keeps a faulted save's gates closed (D-10).
/// </summary>
public sealed class FinalMergeDeltaTests
{
    /// <summary>Finding 1 (critical): a missing note's first save is a create,
    /// off the dispatcher; core queues its Created event before the save
    /// publishes, and the reader takes the edit back before that event is
    /// applied. The event's re-seat keeps the buffer, its undo history and
    /// its document, and clears the missing state without retiring the save:
    /// the save publishes to the tab — "Saved" — and the undo stays an
    /// unsaved change behind the created bytes.</summary>
    [Fact]
    public void ACreatedEventNeverReseatsATabUnderItsAdmittedCreate()
    {
        using var host = new PumpedSaveReentrancyTests.Host();
        WorkspaceTabViewModel ghost = OpenMissing(host, "ghost.md", WorkspaceOpenTarget.NewTab);
        ghost.EditorDocument!.Insert(0, "draft\n");
        Assert.True(ghost.IsDirty);
        int document = ghost.ItemIdentity;

        Task<bool> save = SaveTakingTheEditBack(host, ghost);

        Assert.True(save.Result, "the create failed");
        Assert.Equal(string.Empty, ghost.Text);
        Assert.True(ghost.IsDirty, "the undo lost its place behind the created bytes (D-10)");
        Assert.True(ghost.EditorDocument!.UndoStack.CanRedo, "the re-seat destroyed the undo history");
        Assert.False(ghost.IsMissingFromDisk);
        Assert.Equal(document, ghost.ItemIdentity);
        Assert.Equal("draft\n", host.Disk("ghost.md"));
        Assert.Single(host.Announced, item => item.Event is A11yEvent.NoteSaved);
        Assert.Equal("Saved ghost.md.", ghost.Status);
    }

    /// <summary>Finding 1's everyday arm: the reader saves a missing note
    /// back, taking nothing back. The create's Created event is applied while
    /// the tab is still dirty, under the admitted save, and the note is back
    /// on disk: after the save the tab is clean, not missing, the same
    /// document, and "Saved" is spoken once. It used to stay "missing from
    /// disk" after the save.</summary>
    [Fact]
    public void SavingAMissingNoteBringsItBackOnDisk()
    {
        using var host = new PumpedSaveReentrancyTests.Host();
        WorkspaceTabViewModel ghost = OpenMissing(host, "ghost.md", WorkspaceOpenTarget.NewTab);
        ghost.EditorDocument!.Insert(0, "draft\n");
        int document = ghost.ItemIdentity;

        Task<bool> save = ghost.SaveAsync(announce: true);
        Assert.True(PumpedDispatcher.PumpUntil(() => save.IsCompleted, TimeSpan.FromSeconds(20)), "the save's publication");
        host.Settle();

        Assert.True(save.Result, "the create failed");
        Assert.False(ghost.IsDirty);
        Assert.False(ghost.IsMissingFromDisk, "the saved note still reads missing from disk");
        Assert.Equal(document, ghost.ItemIdentity);
        Assert.Equal("draft\n", host.Disk("ghost.md"));
        Assert.Single(host.Announced, item => item.Event is A11yEvent.NoteSaved);
        Assert.Equal("Saved ghost.md.", ghost.Status);
    }

    /// <summary>Finding 1, a note shown twice — in another pane, or a
    /// duplicate in the same one: both tabs of the path are missing, the
    /// reader types and saves in one and takes the edit back in it (the peer
    /// mirrors both). The Created event re-seats neither: both keep the
    /// reader's text and their documents, and neither is missing.</summary>
    [Theory]
    [InlineData("split")]
    [InlineData("duplicate")]
    public void ACreatedEventNeverReseatsAPeerUnderTheAdmittedCreate(string peerKind)
    {
        using var host = new PumpedSaveReentrancyTests.Host();
        WorkspaceTabViewModel ghost = OpenMissing(host, "ghost.md", WorkspaceOpenTarget.NewTab);
        if (peerKind == "split")
        {
            host.Workspace.OpenPath("ghost.md", WorkspaceOpenTarget.SplitRight);
        }
        else
        {
            host.Workspace.DuplicateTabCommand.Execute(null);
        }

        WorkspaceTabViewModel peer = host.Workspace.ActiveGroup.ActiveTab!;
        Assert.NotSame(ghost, peer);
        Assert.Equal("ghost.md", peer.Path);
        Assert.True(peer.IsMissingFromDisk);
        ghost.EditorDocument!.Insert(0, "draft\n");
        Assert.Equal("draft\n", peer.Text);
        (int Ghost, int Peer) documents = (ghost.ItemIdentity, peer.ItemIdentity);

        Task<bool> save = SaveTakingTheEditBack(host, ghost);

        Assert.True(save.Result, "the create failed");
        Assert.Equal(string.Empty, ghost.Text);
        Assert.Equal(string.Empty, peer.Text);
        Assert.True(ghost.IsDirty, "the undo lost its place behind the created bytes (D-10)");
        Assert.True(peer.IsDirty, "the peer lost the unsaved undo");
        Assert.False(ghost.IsMissingFromDisk);
        Assert.False(peer.IsMissingFromDisk);
        Assert.Equal(documents, (ghost.ItemIdentity, peer.ItemIdentity));
        Assert.Equal("draft\n", host.Disk("ghost.md"));
    }

    /// <summary>Finding 2: a save that faulted after its write was adopted
    /// leaves the tab clean but not saved in D-10's sense. The note changed
    /// outside Slate and a Refresh reloads the clean tab in place — the same
    /// item — and every gate still refuses: the fault is the item's.</summary>
    [Theory]
    [InlineData("close-tab")]
    [InlineData("close-pane")]
    [InlineData("replace")]
    [InlineData("teardown")]
    public void ARescansReloadKeepsAFaultedSavesGatesClosed(string site)
    {
        using var host = new PumpedSaveReentrancyTests.Host(VaultCloseDecision.Discard);
        FaultAfterAdoption(host);
        int document = host.S.ItemIdentity;

        File.WriteAllText(Path.Combine(host.Root, "note1.md"), "# Note 1\n\nChanged outside Slate.\n");
        Task run = host.Lifecycle.RescanAsync(RescanReason.Explicit);
        Assert.True(PumpedDispatcher.PumpUntil(() => run.IsCompleted, TimeSpan.FromSeconds(60)), "the rescan");
        host.Settle();
        Assert.Contains("Changed outside Slate.", host.S.Text, StringComparison.Ordinal);
        Assert.NotEqual(document, host.S.ItemIdentity);
        Assert.True(host.S.LastSaveFaulted, "the rescan's reload cleared the faulted save");
        host.Announced.Clear();

        RunGate(host, site);
        host.Settle();

        Assert.DoesNotContain(
            host.Announced,
            item => item.Event is A11yEvent.TabClosed
                or A11yEvent.VaultClosed
                or A11yEvent.VaultClosedAllSaved
                or A11yEvent.VaultClosedChangesDiscarded);
        switch (site)
        {
            case "close-tab": Assert.False(host.S.IsDisposed, "the tab closed over a faulted save"); break;
            case "close-pane": Assert.Contains(host.G1, host.Workspace.Groups); break;
            case "replace": Assert.Equal("note1.md", host.S.Path); break;
            case "teardown":
                Assert.NotNull(host.Lifecycle.Workspace);
                Assert.Equal(
                    "Vault remains open because one or more notes could not be saved.",
                    host.Lifecycle.StatusText);
                break;
        }
    }

    /// <summary>Finding 2's other side (D-10): the fault belongs to its item,
    /// so an ACTUAL item change still leaves it behind — the reader edits the
    /// note after the fault, opens another note into its tab and discards;
    /// the tab shows that note, clean and not faulted, and closes.</summary>
    [Fact]
    public void AnItemChangeStillLeavesAFaultedSaveBehind()
    {
        using var host = new PumpedSaveReentrancyTests.Host(VaultCloseDecision.Discard);
        FaultAfterAdoption(host);

        host.Type(host.S, "Marker-S2");
        host.TabPrompt = _ => WorkspaceDirtyNavigationDecision.Discard;
        host.Workspace.OpenPath("note3.md");
        host.Settle();
        Assert.Equal("note3.md", host.S.Path);
        Assert.False(host.S.IsDirty);
        Assert.False(host.S.LastSaveFaulted, "the fault followed the tab to another note");

        host.Workspace.CloseTabCommand.Execute(host.S);
        host.Settle();
        Assert.True(host.S.IsDisposed, "a clean note with no faulted save stayed open");
    }

    /// <summary>A note that does not exist, opened (its load reads nothing,
    /// so it carries no content hash) and swept as missing — what a restore
    /// of a vanished note produces.</summary>
    private static WorkspaceTabViewModel OpenMissing(PumpedSaveReentrancyTests.Host host, string path, WorkspaceOpenTarget target)
    {
        host.Workspace.OpenPath(path, target);
        WorkspaceTabViewModel tab = host.Workspace.ActiveGroup.ActiveTab!;
        Assert.Equal(path, tab.Path);
        host.Workspace.InvalidatePath(path);
        Assert.True(tab.IsMissingFromDisk);
        PumpedDispatcher.Drain();
        host.Announced.Clear();
        return tab;
    }

    /// <summary>Save the tab — a create, off the dispatcher — parked on its
    /// worker before the write; take the edit back meanwhile (the tab is
    /// clean again: its baseline is still the empty note the create started
    /// from), then let the create land. Core queues the create's Created
    /// event from inside the write, so the event is applied — the tab clean,
    /// the save admitted — before the save publishes. Returns the settled
    /// save.</summary>
    private static Task<bool> SaveTakingTheEditBack(PumpedSaveReentrancyTests.Host host, WorkspaceTabViewModel tab)
    {
        using var parked = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        tab.SaveWriteHookForTests = () =>
        {
            parked.Set();
            _ = release.Wait(TimeSpan.FromSeconds(30));
        };
        try
        {
            Task<bool> save = tab.SaveAsync(announce: true);
            Assert.True(PumpedDispatcher.PumpUntil(() => parked.IsSet, TimeSpan.FromSeconds(10)), "the create parking");
            tab.EditorDocument!.UndoStack.Undo();
            Assert.False(tab.IsDirty, "premise: the undo is back at the baseline the create started from");
            Assert.True(tab.HasPendingSaves, "premise: the create is admitted");
            release.Set();
            Assert.True(PumpedDispatcher.PumpUntil(() => save.IsCompleted, TimeSpan.FromSeconds(20)), "the save's publication");
            host.Settle();
            return save;
        }
        finally
        {
            release.Set();
            tab.SaveWriteHookForTests = null;
        }
    }

    /// <summary>The note under test (S) saved with a fault after its write
    /// was adopted: clean, and not saved in D-10's sense.</summary>
    private static void FaultAfterAdoption(PumpedSaveReentrancyTests.Host host)
    {
        host.Workspace.SaveActiveAndSettle();
        Assert.False(host.Workspace.HasDirtyTabs, "the arrangement left a dirty tab");
        host.G1.ActiveTab = host.S;
        host.Type(host.S, "Marker-S");
        host.S.SaveAdoptedHookForTests = () => throw new InjectedFault();
        host.Workspace.SaveActiveCommand.Execute(null);
        host.Settle();
        host.S.SaveAdoptedHookForTests = null;
        Assert.False(host.S.IsDirty);
        Assert.True(host.S.LastSaveFaulted, "premise: the save's publication did not fault after adoption");
    }

    /// <summary>The gates, on the note under test (S) in the first pane.</summary>
    private static void RunGate(PumpedSaveReentrancyTests.Host host, string site)
    {
        switch (site)
        {
            case "close-tab": host.Workspace.CloseTabCommand.Execute(host.S); break;
            case "close-pane": host.Workspace.ClosePaneCommand.Execute(null); break;
            case "replace": host.Workspace.OpenPath("note3.md"); break;
            case "teardown": host.Lifecycle.CloseVault(); break;
        }
    }

    private sealed class InjectedFault() : Exception("injected fault after adoption");
}
