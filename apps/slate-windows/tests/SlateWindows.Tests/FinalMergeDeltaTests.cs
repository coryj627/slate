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
/// admitted for its file (contract 38 D-10, R-9).
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
}
