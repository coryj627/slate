// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Threading;
using SlateWindows.Panels;
using SlateWindows.Templates;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// WB-2 (#1322, #1325; contract 38 D-10; WD2, answered 2026-10-21: wait, and
/// the property write waits for the header refresh). A History restore, a
/// template's same-path reload and a property write each join the note's
/// save chain: none acts beside a save admitted before it, a save requested
/// after it waits for its publication, and the operation's own reload keeps
/// the save queued behind it. Every hold is observed: a wait that times out
/// is the fact's failure, never a silent release.
/// </summary>
public sealed class SaveChainAdmissionTests
{
    private const string V1 = "v1 body\n";
    private const string V2 = "v2 body\n";
    private const string Edit = "v3 the reader's edit\n";
    private const string Note = "---\nstatus: draft\n---\n\nBody.\n";
    private const string NoteEdited = "---\nstatus: draft\n---\n\nBody.\nAn edit on its way to disk.\n";
    private static readonly TimeSpan Hold = TimeSpan.FromSeconds(10);

    // The app's mode (#1129): background interaction work, published on
    // this fact's pumped dispatcher, so a parked worker never blocks the
    // thread that pumps.
    private static void RunSta(Action body) =>
        StaThread.RunPumped(body, TimeSpan.FromSeconds(120), "STA test body timed out.");

    // ---- History restore ----------------------------------------------------

    /// <summary>Queue direction 1, a restore behind a save. The save parks;
    /// the reader takes the edit back (the tab clean, so staging passes),
    /// confirms a restore — admitted behind the save — and retypes the edit.
    /// After release the save publishes first and adopts the retyped text,
    /// so the tab is clean when the restore starts; the restore writes on
    /// the saved hash and its continuation reloads the tab once.</summary>
    [Fact]
    public void AHistoryRestoreStartedUnderAnAdmittedSaveWaitsThenRestoresOverTheSavedBytes() => RunSta(() =>
    {
        using var h = new Vault("restore-behind-save", ("a.md", "v0\n"));
        (WorkspaceTabViewModel tab, HistoryVersionRow v1, List<string> alerts) = RestoreSetup(h);
        using var parked = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        tab.SaveWriteHookForTests = () =>
        {
            parked.Set();
            _ = release.Wait(TimeSpan.FromSeconds(30));
        };
        try
        {
            tab.Text = Edit;
            Task<bool> save = tab.SaveAsync(announce: true);
            h.Wait(parked, "premise: the save never parked before its write");
            tab.Text = V2;
            Assert.False(tab.IsDirty, "premise: taking the edit back left the tab dirty");
            Assert.True(tab.HasPendingSaves);
            int document = tab.ItemIdentity;
            h.Events.Clear();

            h.Workspace.RequestRestoreVersion(v1);
            h.Settle();
            Assert.Equal(V2, tab.Text);
            Assert.Equal(document, tab.ItemIdentity);
            Assert.DoesNotContain(h.Events, e => e is A11yEvent.RestoredVersionFrom);

            tab.Text = Edit;
            release.Set();
            h.PumpUntil(
                () => save.IsCompleted && h.Events.Any(e => e is A11yEvent.RestoredVersionFrom) && h.Workspace.SavesIdle,
                "the save's publication and then the restore's");

            Assert.True(save.Result);
            Assert.Empty(alerts);
            _ = Assert.Single(h.Events, e => e is A11yEvent.NoteSaved);
            Assert.True(
                h.Events.FindIndex(e => e is A11yEvent.NoteSaved) < h.Events.FindIndex(e => e is A11yEvent.RestoredVersionFrom),
                "the restore spoke before the save it waited for");
            Assert.Equal(document + 1, tab.ItemIdentity);
            Assert.Equal(V1, tab.Text);
            Assert.False(tab.IsDirty);
            Assert.Equal(V1, h.Read("a.md"));
        }
        finally
        {
            release.Set();
            tab.SaveWriteHookForTests = null;
        }
    });

    /// <summary>The save a restore waited for is never silenced: its
    /// NoteSaved is spoken and its document kept, and only then, with the
    /// restore parked before its write, does anything else happen to the
    /// tab; the restore's own reload is the one document change.</summary>
    [Fact]
    public void AHistoryRestoreNeverSilencesTheSaveItWaitedFor() => RunSta(() =>
    {
        using var h = new Vault("restore-keeps-save", ("a.md", "v0\n"));
        (WorkspaceTabViewModel tab, HistoryVersionRow v1, _) = RestoreSetup(h);
        using var saveParked = new ManualResetEventSlim();
        using var saveRelease = new ManualResetEventSlim();
        using var restoreParked = new ManualResetEventSlim();
        using var restoreRelease = new ManualResetEventSlim();
        tab.SaveWriteHookForTests = () =>
        {
            saveParked.Set();
            _ = saveRelease.Wait(TimeSpan.FromSeconds(30));
        };
        h.Workspace.HistoryRestoreHookForTests = () =>
        {
            restoreParked.Set();
            _ = restoreRelease.Wait(TimeSpan.FromSeconds(30));
        };
        try
        {
            tab.Text = Edit;
            Task<bool> save = tab.SaveAsync(announce: true);
            h.Wait(saveParked, "premise: the save never parked before its write");
            tab.Text = V2;
            int document = tab.ItemIdentity;
            h.Events.Clear();
            h.Workspace.RequestRestoreVersion(v1);
            tab.Text = Edit;
            h.Settle();
            Assert.False(restoreParked.IsSet, "the restore started beside the save it was admitted behind");

            saveRelease.Set();
            h.PumpUntil(() => save.IsCompleted, "the save's publication");
            h.Wait(restoreParked, "the restore never started after the save");
            Assert.True(save.Result);
            _ = Assert.Single(h.Events, e => e is A11yEvent.NoteSaved);
            Assert.Equal(document, tab.ItemIdentity);
            Assert.DoesNotContain(h.Events, e => e is A11yEvent.RestoredVersionFrom);

            restoreRelease.Set();
            h.PumpUntil(() => h.Events.Any(e => e is A11yEvent.RestoredVersionFrom), "the restore's publication");
            Assert.True(
                h.Events.FindIndex(e => e is A11yEvent.NoteSaved) < h.Events.FindIndex(e => e is A11yEvent.RestoredVersionFrom));
            Assert.Equal(document + 1, tab.ItemIdentity);
        }
        finally
        {
            saveRelease.Set();
            restoreRelease.Set();
            tab.SaveWriteHookForTests = null;
            h.Workspace.HistoryRestoreHookForTests = null;
        }
    });

    /// <summary>The first fact without the retyping: once the save
    /// publishes, the reader's taken-back text is an unsaved change behind
    /// the saved bytes, so the restore's start speaks D-14 once and writes
    /// nothing; its ticket ends, so a later save writes.</summary>
    [Fact]
    public void AHistoryRestoreThatFindsItsTabDirtyWhenItStartsRefusesAndWritesNothing() => RunSta(() =>
    {
        using var h = new Vault("restore-finds-dirty", ("a.md", "v0\n"));
        (WorkspaceTabViewModel tab, HistoryVersionRow v1, List<string> alerts) = RestoreSetup(h);
        using var parked = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        tab.SaveWriteHookForTests = () =>
        {
            parked.Set();
            _ = release.Wait(TimeSpan.FromSeconds(30));
        };
        try
        {
            tab.Text = Edit;
            Task<bool> save = tab.SaveAsync(announce: true);
            h.Wait(parked, "premise: the save never parked before its write");
            tab.Text = V2;
            int document = tab.ItemIdentity;
            h.Events.Clear();
            h.Workspace.RequestRestoreVersion(v1);

            release.Set();
            h.PumpUntil(() => save.IsCompleted && h.Workspace.SavesIdle, "the save and the restore's start");

            Assert.True(tab.IsDirty, "premise: the taken-back text was not left behind the saved bytes");
            _ = Assert.Single(
                h.Events.OfType<A11yEvent.HostComposed>(),
                line => line.Text.StartsWith("Save the note before restoring a version.", StringComparison.Ordinal));
            Assert.DoesNotContain(h.Events, e => e is A11yEvent.RestoredVersionFrom);
            Assert.Empty(alerts);
            Assert.Equal(Edit, h.Read("a.md"));
            Assert.Equal(V2, tab.Text);
            Assert.Equal(document, tab.ItemIdentity);
        }
        finally
        {
            release.Set();
            tab.SaveWriteHookForTests = null;
        }

        Task<bool> later = tab.SaveAsync();
        h.PumpUntil(() => later.IsCompleted, "a save after the refused restore");
        Assert.True(later.Result);
        Assert.Equal(V2, h.Read("a.md"));
    });

    /// <summary>An admitted restore always ends: a fault outside
    /// VaultException on its worker (a core panic) is a failed restore —
    /// "Restore failed" with its message, nothing written — and its ticket
    /// ends, so a save of the note requested after it still writes.</summary>
    [Fact]
    public void AHistoryRestoreWhoseWorkerFaultsEndsAndFreesTheNotesChain() => RunSta(() =>
    {
        using var h = new Vault("restore-faults", ("a.md", "v0\n"));
        (WorkspaceTabViewModel tab, HistoryVersionRow v1, List<string> alerts) = RestoreSetup(h);
        h.Workspace.HistoryRestoreHookForTests = () =>
            throw new InvalidOperationException("the restore's core call panicked (test seam)");
        try
        {
            h.Events.Clear();
            h.Workspace.RequestRestoreVersion(v1);
            h.PumpUntil(() => alerts.Count > 0 && h.Workspace.SavesIdle, "the faulted restore ending");
            Assert.Equal("Restore failed: the restore's core call panicked (test seam)", Assert.Single(alerts));
            Assert.DoesNotContain(h.Events, e => e is A11yEvent.RestoredVersionFrom);
            Assert.Equal(V2, h.Read("a.md"));
        }
        finally
        {
            h.Workspace.HistoryRestoreHookForTests = null;
        }

        tab.Text = Edit;
        Task<bool> later = tab.SaveAsync(announce: true);
        h.PumpUntil(() => later.IsCompleted, "a save after the faulted restore");
        Assert.True(later.Result);
        Assert.Equal(Edit, h.Read("a.md"));
    });

    /// <summary>Queue direction 2, a save behind a restore: the restore's
    /// worker parks; the clean tab requests a save, which waits. After
    /// release the continuation reloads the tab despite that successor,
    /// whose epoch is carried, so it then publishes NoteSaved.</summary>
    [Fact]
    public void ASaveStartedUnderAnAdmittedRestoreWaitsForTheRestoresPublication() => RunSta(() =>
    {
        using var h = new Vault("save-behind-restore", ("a.md", "v0\n"));
        (WorkspaceTabViewModel tab, HistoryVersionRow v1, List<string> alerts) = RestoreSetup(h);
        using var parked = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        h.Workspace.HistoryRestoreHookForTests = () =>
        {
            parked.Set();
            _ = release.Wait(TimeSpan.FromSeconds(30));
        };
        try
        {
            int document = tab.ItemIdentity;
            h.Events.Clear();
            h.Workspace.RequestRestoreVersion(v1);
            h.Wait(parked, "premise: the restore never parked before its write");

            Task<bool> save = tab.SaveAsync(announce: true);
            h.Settle();
            Assert.False(save.IsCompleted, "the save did not wait for the restore");

            release.Set();
            h.PumpUntil(() => save.IsCompleted && h.Workspace.SavesIdle, "the restore and then the save");
            Assert.Empty(alerts);
            Assert.Equal(document + 1, tab.ItemIdentity);
            Assert.Equal(V1, tab.Text);
            Assert.True(save.Result, "the restore's reload retired the save queued behind it");
            _ = Assert.Single(h.Events, e => e is A11yEvent.NoteSaved);
            Assert.True(
                h.Events.FindIndex(e => e is A11yEvent.RestoredVersionFrom) < h.Events.FindIndex(e => e is A11yEvent.NoteSaved),
                "the save published before the restore it waited for");
            Assert.Equal(V1, h.Read("a.md"));
        }
        finally
        {
            release.Set();
            h.Workspace.HistoryRestoreHookForTests = null;
        }
    });

    /// <summary>Queue direction 2 with an edit: the dirty tab is not
    /// reloaded, and its save, from the pre-restore hash, conflicts — the
    /// restore's bytes are never silently overwritten.</summary>
    [Fact]
    public void ADirtySaveQueuedBehindARestoreReportsAConflictAndOverwritesNothing() => RunSta(() =>
    {
        using var h = new Vault("dirty-save-behind-restore", ("a.md", "v0\n"));
        (WorkspaceTabViewModel tab, HistoryVersionRow v1, _) = RestoreSetup(h);
        using var parked = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        h.Workspace.HistoryRestoreHookForTests = () =>
        {
            parked.Set();
            _ = release.Wait(TimeSpan.FromSeconds(30));
        };
        try
        {
            h.Events.Clear();
            h.Workspace.RequestRestoreVersion(v1);
            h.Wait(parked, "premise: the restore never parked before its write");
            tab.Text = Edit;
            Task<bool> save = tab.SaveAsync(announce: true);

            release.Set();
            h.PumpUntil(() => save.IsCompleted && h.Workspace.SavesIdle, "the restore and then the save");
            Assert.False(save.Result);
            Assert.Equal(Edit, tab.Text);
            Assert.True(tab.IsDirty);
            _ = Assert.Single(h.Events, e => e is A11yEvent.NoteSaveConflict);
            Assert.Equal(V1, h.Read("a.md"));
        }
        finally
        {
            release.Set();
            h.Workspace.HistoryRestoreHookForTests = null;
        }
    });

    /// <summary>The guard kept for unadmitted callers: a reload outside any
    /// admission never replaces a tab under a save it admitted.</summary>
    [Fact]
    public void TheUnadmittedReloadStillKeepsATabWithAnAdmittedSave() => RunSta(() =>
    {
        using var h = new Vault("unadmitted-reload", ("a.md", "v0\n"));
        WorkspaceTabViewModel tab = h.Open("a.md");
        using var parked = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        tab.SaveWriteHookForTests = () =>
        {
            parked.Set();
            _ = release.Wait(TimeSpan.FromSeconds(30));
        };
        try
        {
            tab.Text = Edit;
            Task<bool> save = tab.SaveAsync(announce: true);
            h.Wait(parked, "premise: the save never parked before its write");
            tab.Text = "v0\n";
            Assert.False(tab.IsDirty);
            Assert.True(tab.HasPendingSaves);
            int document = tab.ItemIdentity;
            h.Events.Clear();

            h.Workspace.ReloadOpenTabFromDiskForTests("a.md");

            Assert.Equal(document, tab.ItemIdentity);
            Assert.Equal("v0\n", tab.Text);
            release.Set();
            h.PumpUntil(() => save.IsCompleted, "the save's publication");
            Assert.True(save.Result);
            _ = Assert.Single(h.Events, e => e is A11yEvent.NoteSaved);
        }
        finally
        {
            release.Set();
            tab.SaveWriteHookForTests = null;
        }
    });

    // ---- Template same-path reload ------------------------------------------

    /// <summary>A clean same-path tab with a parked save keeps its buffer and
    /// its document through the template create; a save it requests while
    /// the sweep waits queues behind the sweep. After release the parked
    /// save, a ghost's create, finds the note the template made and is
    /// blocked — it never overwrites it — then the sweep reloads the tab
    /// once, and the save queued behind it still publishes.</summary>
    [Fact]
    public void ATemplatesSamePathReloadWaitsForTheNotesAdmittedSave() => RunSta(() =>
    {
        using var h = new Vault(
            "template-behind-save",
            ("Templates/Plain.md", "# {{title}}\n\nCafé {{cursor}}end\n"),
            ("a.md", "a\n"));
        WorkspaceTabViewModel ghost = h.Open("Ghost.md");
        Assert.Equal(string.Empty, ghost.Text);
        using var parked = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        ghost.SaveWriteHookForTests = () =>
        {
            parked.Set();
            _ = release.Wait(TimeSpan.FromSeconds(30));
        };
        try
        {
            ghost.Text = Edit;
            Task<bool> first = ghost.SaveAsync(announce: true);
            h.Wait(parked, "premise: the save never parked before its write");
            ghost.Text = string.Empty;
            Assert.False(ghost.IsDirty, "premise: taking the edit back left the tab dirty");
            int document = ghost.ItemIdentity;
            h.Events.Clear();

            h.Workspace.OpenTemplatePicker();
            h.PumpUntil(
                () => h.Workspace.TemplatePickerSheet is { } picker && picker.Rows.Any(row => row.Name == "Plain"),
                "the template picker's rows");
            TemplatePickerViewModel sheet = h.Workspace.TemplatePickerSheet!;
            sheet.ActivateCommand.Execute(sheet.Rows.Single(row => row.Name == "Plain"));
            TemplateFlowViewModel flow = h.Workspace.TemplateFlowSheet!;
            flow.NoteName = "Ghost";
            flow.CreateCommand.Execute(null);
            Assert.Same(ghost, h.Workspace.ActiveGroup.ActiveTab);

            Assert.Equal(document, ghost.ItemIdentity);
            Assert.Equal(string.Empty, ghost.Text);
            Task<bool> second = ghost.SaveAsync(announce: true);
            h.Settle();
            Assert.Equal(document, ghost.ItemIdentity);

            release.Set();
            h.PumpUntil(() => first.IsCompleted && second.IsCompleted && h.Workspace.SavesIdle, "both saves and the sweep");
            Assert.False(first.Result, "the ghost's save overwrote the note the template created");
            _ = Assert.Single(h.Events, e => e is A11yEvent.NoteSaveBlocked);
            string created = h.Read("Ghost.md");
            Assert.StartsWith("# Ghost", created, StringComparison.Ordinal);
            Assert.Equal(document + 1, ghost.ItemIdentity);
            Assert.Equal(created, ghost.Text);
            Assert.True(second.Result, "the sweep's reload retired the save queued behind it");
            _ = Assert.Single(h.Events, e => e is A11yEvent.NoteSaved);
        }
        finally
        {
            release.Set();
            ghost.SaveWriteHookForTests = null;
        }
    });

    // ---- Property writes (#1325) --------------------------------------------

    /// <summary>B's save parks; the reader takes the edit back (both tabs
    /// clean: #1325's window); A commits <c>status</c>, admitted behind the
    /// save; the edit is retyped. No property write until release; then
    /// NoteSaved, then one write on the saved hash.</summary>
    [Fact]
    public void APropertyCommitInAPeerTabWaitsForTheOtherTabsSaveAndWritesOnce() => RunSta(() =>
    {
        using var peers = new Peers("prop-behind-save");
        PropertyRowViewModel status = peers.Row("status");
        Task<bool> save = peers.ParkBsSave(NoteEdited);
        try
        {
            peers.TakeBack(Note);
            status.EditorText = "done";
            Assert.True(peers.H.Workspace.SetPanelProperty(status));
            peers.Retype(NoteEdited);
            peers.H.Settle();
            Assert.Equal(0, peers.Writes);
            Assert.True(peers.H.Workspace.PropertyWriteInFlightFor("a.md"));

            peers.ReleaseSave();
            peers.H.PumpUntil(
                () => save.IsCompleted && peers.H.Events.Any(e => e is A11yEvent.PropertyChanged) && peers.H.Workspace.SavesIdle,
                "the save and then the property write");
            Assert.True(save.Result);
            _ = Assert.Single(peers.H.Events, e => e is A11yEvent.NoteSaved);
            _ = Assert.Single(peers.H.Events, e => e is A11yEvent.PropertyChanged);
            Assert.True(
                peers.H.Events.FindIndex(e => e is A11yEvent.NoteSaved) < peers.H.Events.FindIndex(e => e is A11yEvent.PropertyChanged));
            Assert.DoesNotContain(peers.H.Events, e => e is A11yEvent.PropertyEditConflict);
            Assert.Empty(peers.Dialogs);
            Assert.Equal(1, peers.Writes);
            string saved = peers.H.Read("a.md");
            Assert.Contains("An edit on its way to disk.", saved, StringComparison.Ordinal);
            Assert.Contains("status: done", saved, StringComparison.Ordinal);
        }
        finally
        {
            peers.ReleaseSave();
        }
    });

    /// <summary>WD2's answer: the admitted write waits until A's header has
    /// re-read the saved bytes, and takes its CAS token from that read.</summary>
    [Fact]
    public void AnAdmittedPropertyWriteWaitsForTheHeaderRefreshTheSavePublished() => RunSta(() =>
    {
        using var peers = new Peers("prop-waits-header");
        PropertyRowViewModel status = peers.Row("status");
        Task<bool> save = peers.ParkBsSave(NoteEdited);
        try
        {
            peers.TakeBack(Note);
            status.EditorText = "done";
            Assert.True(peers.H.Workspace.SetPanelProperty(status));
            peers.Retype(NoteEdited);
            peers.ParkHeaderRead();

            peers.ReleaseSave();
            peers.H.PumpUntil(() => save.IsCompleted, "the save's publication");
            Assert.True(save.Result);
            _ = Assert.Single(peers.H.Events, e => e is A11yEvent.NoteSaved);
            peers.H.Wait(peers.HeaderReadParked, "the save's header refresh never read");
            peers.H.PumpUntil(() => peers.Properties.PublishWaitersForTests == 1, "the write waiting on the header");
            Assert.Equal(0, peers.Writes);
            Assert.DoesNotContain("status: done", peers.H.Read("a.md"), StringComparison.Ordinal);
            Assert.True(peers.H.Workspace.PropertyWriteInFlightFor("a.md"));
            Assert.DoesNotContain(peers.H.Events, e => e is A11yEvent.PropertyEditConflict);

            peers.ReleaseHeaderRead();
            peers.H.PumpUntil(
                () => peers.H.Events.Any(e => e is A11yEvent.PropertyChanged) && peers.H.Workspace.SavesIdle,
                "the property write after the header refresh");
            _ = Assert.Single(peers.H.Events, e => e is A11yEvent.PropertyChanged);
            Assert.DoesNotContain(peers.H.Events, e => e is A11yEvent.PropertyEditConflict);
            Assert.Empty(peers.Dialogs);
            Assert.Equal(1, peers.Writes);
            string saved = peers.H.Read("a.md");
            Assert.Contains("An edit on its way to disk.", saved, StringComparison.Ordinal);
            Assert.Contains("status: done", saved, StringComparison.Ordinal);
            Assert.False(peers.H.Workspace.SavesForTests.HasAdmittedSaveFor("a.md"));
        }
        finally
        {
            peers.ReleaseSave();
            peers.ReleaseHeaderRead();
        }
    });

    /// <summary>The header's refresh fails while the write waits: nothing is
    /// written, one PropertyEditFailed, the lease and the ticket released,
    /// and a later save still publishes.</summary>
    [Fact]
    public void AnAdmittedPropertyWriteWhoseHeaderRefreshFailsWritesNothingAndEnds() => RunSta(() =>
    {
        using var peers = new Peers("prop-header-fails");
        PropertyRowViewModel status = peers.Row("status");
        Task<bool> save = peers.ParkBsSave(NoteEdited);
        try
        {
            peers.TakeBack(Note);
            status.EditorText = "done";
            Assert.True(peers.H.Workspace.SetPanelProperty(status));
            peers.Retype(NoteEdited);
            peers.ParkHeaderRead(thenFail: true);

            peers.ReleaseSave();
            peers.H.PumpUntil(() => save.IsCompleted, "the save's publication");
            peers.H.Wait(peers.HeaderReadParked, "the save's header refresh never read");
            peers.H.PumpUntil(() => peers.Properties.PublishWaitersForTests == 1, "the write waiting on the header");

            peers.ReleaseHeaderRead();
            peers.H.PumpUntil(
                () => peers.H.Events.Any(e => e is A11yEvent.PropertyEditFailed) && peers.H.Workspace.SavesIdle,
                "the write ending on the failed refresh");
            _ = Assert.Single(peers.H.Events, e => e is A11yEvent.PropertyEditFailed);
            Assert.DoesNotContain(peers.H.Events, e => e is A11yEvent.PropertyEditConflict);
            Assert.DoesNotContain(peers.H.Events, e => e is A11yEvent.PropertyChanged);
            Assert.Equal(0, peers.Writes);
            Assert.DoesNotContain("status: done", peers.H.Read("a.md"), StringComparison.Ordinal);
            Assert.False(peers.H.Workspace.PropertyWriteInFlightFor("a.md"));
            Assert.False(peers.H.Workspace.SavesForTests.HasAdmittedSaveFor("a.md"));
        }
        finally
        {
            peers.ReleaseSave();
            peers.ReleaseHeaderRead();
        }

        peers.B.Text = NoteEdited + "Later.\n";
        Task<bool> later = peers.B.SaveAsync(announce: true);
        peers.H.PumpUntil(() => later.IsCompleted, "a save after the ended write");
        Assert.True(later.Result);
    });

    /// <summary>The superseded refresh: A's header is pointed at another
    /// note while the write waits, so the parked read can never answer it.
    /// The write ends abandoned — silent, nothing written, lease and ticket
    /// released.</summary>
    [Fact]
    public void AnAdmittedPropertyWriteWhoseHeaderMovesToAnotherNoteWritesNothingAndEnds() => RunSta(() =>
    {
        using var peers = new Peers("prop-header-moves");
        PropertyRowViewModel status = peers.Row("status");
        Task<bool> save = peers.ParkBsSave(NoteEdited);
        try
        {
            peers.TakeBack(Note);
            status.EditorText = "done";
            Assert.True(peers.H.Workspace.SetPanelProperty(status));
            peers.Retype(NoteEdited);
            peers.ParkHeaderRead();

            peers.ReleaseSave();
            peers.H.PumpUntil(() => save.IsCompleted, "the save's publication");
            peers.H.Wait(peers.HeaderReadParked, "the save's header refresh never read");
            peers.H.PumpUntil(() => peers.Properties.PublishWaitersForTests == 1, "the write waiting on the header");
            peers.H.Events.Clear();

            peers.Properties.Load("b.md");
            peers.H.PumpUntil(
                () => !peers.H.Workspace.PropertyWriteInFlightFor("a.md") && peers.H.Workspace.SavesIdle,
                "the abandoned write ending");
            Assert.DoesNotContain(peers.H.Events, e => e is A11yEvent.PropertyChanged
                or A11yEvent.PropertyEditConflict or A11yEvent.PropertyEditFailed);
            Assert.Equal(0, peers.Writes);
            Assert.False(peers.H.Workspace.SavesForTests.HasAdmittedSaveFor("a.md"));

            peers.ReleaseHeaderRead();
            peers.H.Settle();
            Assert.Equal("b.md", peers.Properties.Path);
            Assert.DoesNotContain("status: done", peers.H.Read("a.md"), StringComparison.Ordinal);
        }
        finally
        {
            peers.ReleaseSave();
            peers.ReleaseHeaderRead();
        }
    });

    /// <summary>B's edit also changes the edited key's value: the fresh
    /// header's baseline is not the one A's edit was made against, so the
    /// existing conflict path runs and nothing is written.</summary>
    [Fact]
    public void AnAdmittedPropertyWriteWhoseKeyTheSaveChangedTakesTheConflictPath() => RunSta(() =>
    {
        const string Changed = "---\nstatus: review\n---\n\nBody.\nAn edit on its way to disk.\n";
        using var peers = new Peers("prop-key-changed");
        PropertyRowViewModel status = peers.Row("status");
        Task<bool> save = peers.ParkBsSave(Changed);
        try
        {
            peers.TakeBack(Note);
            status.EditorText = "done";
            Assert.True(peers.H.Workspace.SetPanelProperty(status));
            peers.Retype(Changed);

            peers.ReleaseSave();
            peers.H.PumpUntil(
                () => save.IsCompleted && peers.H.Events.Any(e => e is A11yEvent.PropertyEditConflict) && peers.H.Workspace.SavesIdle,
                "the save and the write's conflict");
            _ = Assert.Single(peers.H.Events, e => e is A11yEvent.PropertyEditConflict);
            Assert.Equal(["status"], peers.Dialogs);
            Assert.Equal(0, peers.Writes);
            Assert.Contains("status: review", peers.H.Read("a.md"), StringComparison.Ordinal);
            Assert.Equal("done", peers.Row("status").EditorText);
            Assert.False(peers.H.Workspace.PropertyWriteInFlightFor("a.md"));
            Assert.False(peers.H.Workspace.SavesForTests.HasAdmittedSaveFor("a.md"));
        }
        finally
        {
            peers.ReleaseSave();
        }
    });

    /// <summary>The #1325 fact without the retyping: the save's publication
    /// leaves the tabs dirty, so the write's start speaks the dirty-tab line
    /// once and writes nothing.</summary>
    [Fact]
    public void AnAdmittedPropertyWriteThatFindsItsNoteDirtyWhenItStartsRefusesAndWritesNothing() => RunSta(() =>
    {
        using var peers = new Peers("prop-finds-dirty");
        PropertyRowViewModel status = peers.Row("status");
        Task<bool> save = peers.ParkBsSave(NoteEdited);
        try
        {
            peers.TakeBack(Note);
            status.EditorText = "done";
            Assert.True(peers.H.Workspace.SetPanelProperty(status));

            peers.ReleaseSave();
            peers.H.PumpUntil(() => save.IsCompleted && peers.H.Workspace.SavesIdle, "the save and the write's start");
            Assert.True(peers.B.IsDirty, "premise: the taken-back text was not left behind the saved bytes");
            _ = Assert.Single(
                peers.H.Events.OfType<A11yEvent.HostComposed>(),
                line => line.Text == "Save the note before editing properties. The editor has unsaved changes in a.md.");
            Assert.DoesNotContain(peers.H.Events, e => e is A11yEvent.PropertyEditConflict);
            Assert.Equal(0, peers.Writes);
            Assert.False(peers.H.Workspace.PropertyWriteInFlightFor("a.md"));
            Assert.False(peers.H.Workspace.SavesForTests.HasAdmittedSaveFor("a.md"));
        }
        finally
        {
            peers.ReleaseSave();
        }
    });

    /// <summary>The add twin: A's add sheet opened before the save pins the
    /// pre-save hash; the admitted add waits for the header and adds on the
    /// saved hash.</summary>
    [Fact]
    public void AnAdmittedPropertyAddWaitsForTheHeaderRefreshAndAddsOnTheSavedHash() => RunSta(() =>
    {
        using var peers = new Peers("prop-add-behind-save");
        peers.ActivateA();
        peers.H.Workspace.OpenAddPropertySheet();
        AddPropertyViewModel sheet = peers.H.Workspace.AddPropertySheet!;
        Task<bool> save = peers.ParkBsSave(NoteEdited);
        try
        {
            peers.TakeBack(Note);
            sheet.Key = "owner";
            sheet.Value = "alice";
            Assert.True(sheet.Add(), "the add was refused at commit");
            peers.Retype(NoteEdited);
            peers.ParkHeaderRead();

            peers.ReleaseSave();
            peers.H.PumpUntil(() => save.IsCompleted, "the save's publication");
            peers.H.Wait(peers.HeaderReadParked, "the save's header refresh never read");
            peers.H.PumpUntil(() => peers.Properties.PublishWaitersForTests == 1, "the add waiting on the header");
            Assert.Equal(0, peers.Writes);

            peers.ReleaseHeaderRead();
            peers.H.PumpUntil(
                () => peers.H.Events.Any(e => e is A11yEvent.PropertyChanged) && peers.H.Workspace.SavesIdle,
                "the add after the header refresh");
            _ = Assert.Single(peers.H.Events, e => e is A11yEvent.PropertyChanged);
            Assert.DoesNotContain(peers.H.Events, e => e is A11yEvent.PropertyEditConflict);
            Assert.Null(peers.H.Workspace.AddPropertySheet);
            Assert.Equal(1, peers.Writes);
            string saved = peers.H.Read("a.md");
            Assert.Contains("An edit on its way to disk.", saved, StringComparison.Ordinal);
            Assert.Contains("owner: alice", saved, StringComparison.Ordinal);
        }
        finally
        {
            peers.ReleaseSave();
            peers.ReleaseHeaderRead();
        }
    });

    /// <summary>A save behind an admitted property write: the write's worker
    /// parks; the clean tab's save waits; after release the property's
    /// publication comes first and the save, on the re-baselined tab,
    /// writes nothing more.</summary>
    [Fact]
    public void ASaveStartedUnderAnAdmittedPropertyWriteWaitsForItsCompletion() => RunSta(() =>
    {
        using var peers = new Peers("save-behind-prop");
        PropertyRowViewModel status = peers.Row("status");
        using var parked = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        int writes = 0;
        foreach (WorkspaceTabViewModel tab in new[] { peers.A, peers.B })
        {
            tab.PropertyWriteHookForTests = () =>
            {
                _ = Interlocked.Increment(ref writes);
                parked.Set();
                _ = release.Wait(TimeSpan.FromSeconds(30));
            };
        }
        try
        {
            status.EditorText = "done";
            Assert.True(peers.H.Workspace.SetPanelProperty(status));
            peers.H.Wait(parked, "premise: the property write never parked before its write");
            Task<bool> save = peers.A.SaveAsync(announce: true);
            peers.H.Settle();
            Assert.False(save.IsCompleted, "the save did not wait for the property write");

            release.Set();
            peers.H.PumpUntil(() => save.IsCompleted && peers.H.Workspace.SavesIdle, "the write and then the save");
            Assert.True(save.Result);
            Assert.True(
                peers.H.Events.FindIndex(e => e is A11yEvent.PropertyChanged) < peers.H.Events.FindIndex(e => e is A11yEvent.NoteSaved),
                "the save published before the property write it waited for");
            _ = Assert.Single(peers.H.Events, e => e is A11yEvent.NoteSaved);
            Assert.Equal(1, writes);
            Assert.Contains("status: done", peers.H.Read("a.md"), StringComparison.Ordinal);
        }
        finally
        {
            release.Set();
            peers.A.PropertyWriteHookForTests = null;
            peers.B.PropertyWriteHookForTests = null;
        }
    });

    // ---- Teardown -------------------------------------------------------------

    /// <summary>Contract 35 A-1: teardown joins an admitted restore's worker
    /// and a running property write's worker before the session goes — each
    /// is among the workers the save coordinator joins without a bound, not
    /// left to a bounded drain — and a property write waiting on its header
    /// ends abandoned when the tab's header shuts down, so the chain goes
    /// idle and nothing is spoken.</summary>
    [Fact]
    public void TeardownJoinsAnAdmittedRestoreAndPropertyWrite() => StaThread.RunPumped(() =>
    {
        using FixtureVault fixture = FixtureVault.Create(0, "admitted-teardown");
        File.WriteAllText(Path.Combine(fixture.Root, "a.md"), "v0\n");
        File.WriteAllText(Path.Combine(fixture.Root, "b.md"), Note);
        using VaultSession session = VaultSession.OpenFilesystem(fixture.Root);
        using (var cancel = new CancelToken())
        {
            session.ScanInitial(cancel);
        }

        // The restore, parked on its worker when teardown begins.
        var announced = new List<A11yEvent>();
        var restoring = new WorkspaceViewModel(session, fixture.Root, () => [], announced.Add, startInteractionBackgroundWork: true);
        restoring.HistoryAlert = (_, _) => { };
        restoring.HistoryRestoreConfirmation = _ => true;
        restoring.OpenPath("a.md");
        WorkspaceTabViewModel a = restoring.ActiveGroup.ActiveTab!;
        a.Text = V1;
        Assert.True(PumpedWait.Result(Dispatcher.CurrentDispatcher, a.SaveAsync()));
        string v1 = a.SavedContentHash!;
        a.Text = V2;
        Assert.True(PumpedWait.Result(Dispatcher.CurrentDispatcher, a.SaveAsync()));
        restoring.History.NoteChanged("a.md");
        Assert.True(PumpedDispatcher.PumpUntil(
            () => restoring.History.DayGroups.SelectMany(group => group.Rows).Any(row => row.ContentHashAfter == v1),
            TimeSpan.FromSeconds(20)), "premise: the history rows never loaded");
        HistoryVersionRow target = restoring.History.DayGroups.SelectMany(group => group.Rows).First(row => row.ContentHashAfter == v1);
        using var parked = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        bool workerFinished = false;
        restoring.HistoryRestoreHookForTests = () =>
        {
            parked.Set();
            _ = release.Wait(TimeSpan.FromSeconds(30));
            Thread.Sleep(200);
            Volatile.Write(ref workerFinished, true);
        };
        int trackedAtJoin = -1;
        restoring.SavesForTests.BeforeJoinForTests = () =>
        {
            trackedAtJoin = restoring.SavesForTests.TrackedWorkersForTests;
            release.Set();
        };
        restoring.RequestRestoreVersion(target);
        Assert.True(PumpedDispatcher.PumpUntil(() => parked.IsSet, Hold), "premise: the restore never parked before its write");
        restoring.Dispose();
        Assert.Equal(1, trackedAtJoin);
        Assert.True(Volatile.Read(ref workerFinished), "teardown returned while the restore's worker still ran");

        // The property write, parked in its header wait when teardown begins.
        var spoken = new List<A11yEvent>();
        var writing = new WorkspaceViewModel(session, fixture.Root, () => [], spoken.Add, startInteractionBackgroundWork: true);
        writing.OpenPath("b.md");
        WorkspaceTabViewModel b = writing.ActiveGroup.ActiveTab!;
        NotePropertiesViewModel header = writing.EnsureActiveTabProperties(synchronousForTests: false)!;
        Assert.True(PumpedDispatcher.PumpUntil(() => header.Rows.Count > 0 && !header.IsLoading, TimeSpan.FromSeconds(20)));
        int writes = 0;
        b.PropertyWriteHookForTests = () => Interlocked.Increment(ref writes);
        using var saveParked = new ManualResetEventSlim();
        using var saveRelease = new ManualResetEventSlim();
        using var readParked = new ManualResetEventSlim();
        using var readRelease = new ManualResetEventSlim();
        b.SaveWriteHookForTests = () =>
        {
            saveParked.Set();
            _ = saveRelease.Wait(TimeSpan.FromSeconds(30));
        };
        try
        {
            b.Text = NoteEdited;
            writing.SaveActiveCommand.Execute(null);
            Assert.True(PumpedDispatcher.PumpUntil(() => saveParked.IsSet, Hold), "premise: the save never parked before its write");
            b.Text = Note;
            PropertyRowViewModel status = header.Rows.Single(row => row.Key == "status");
            status.EditorText = "done";
            Assert.True(writing.SetPanelProperty(status));
            b.Text = NoteEdited;
            header.RefreshReadHookForTests = () =>
            {
                readParked.Set();
                _ = readRelease.Wait(TimeSpan.FromSeconds(30));
            };
            saveRelease.Set();
            Assert.True(PumpedDispatcher.PumpUntil(() => readParked.IsSet, Hold), "the save's header refresh never read");
            Assert.True(PumpedDispatcher.PumpUntil(() => header.PublishWaitersForTests == 1, TimeSpan.FromSeconds(20)), "the write never waited on the header");
            spoken.Clear();

            writing.Dispose();
            Assert.True(PumpedDispatcher.PumpUntil(() => writing.SavesForTests.IsIdle, TimeSpan.FromSeconds(20)), "the waiting write never ended after teardown");
            Assert.Equal(0, writes);
            Assert.DoesNotContain(spoken, e => e is A11yEvent.PropertyChanged or A11yEvent.PropertyEditConflict or A11yEvent.PropertyEditFailed);
        }
        finally
        {
            saveRelease.Set();
            readRelease.Set();
        }

        // The property write, running on its worker when teardown begins.
        var committing = new WorkspaceViewModel(session, fixture.Root, () => [], _ => { }, startInteractionBackgroundWork: true);
        committing.OpenPath("b.md");
        WorkspaceTabViewModel c = committing.ActiveGroup.ActiveTab!;
        NotePropertiesViewModel committed = committing.EnsureActiveTabProperties()!;
        Assert.True(
            PumpedDispatcher.PumpUntil(() => committed.Rows.Count > 0 && !committed.IsLoading, TimeSpan.FromSeconds(20)),
            "premise: the header never loaded");
        using var writeParked = new ManualResetEventSlim();
        using var writeRelease = new ManualResetEventSlim();
        bool writeFinished = false;
        c.PropertyWriteHookForTests = () =>
        {
            writeParked.Set();
            _ = writeRelease.Wait(TimeSpan.FromSeconds(30));
            Thread.Sleep(200);
            Volatile.Write(ref writeFinished, true);
        };
        int writesAtJoin = -1;
        committing.SavesForTests.BeforeJoinForTests = () =>
        {
            writesAtJoin = committing.SavesForTests.TrackedWorkersForTests;
            writeRelease.Set();
        };
        try
        {
            PropertyRowViewModel draft = committed.Rows.Single(row => row.Key == "status");
            draft.EditorText = "done";
            Assert.True(committing.SetPanelProperty(draft));
            Assert.True(PumpedDispatcher.PumpUntil(() => writeParked.IsSet, Hold), "premise: the property write never parked before its write");
            committing.Dispose();
            Assert.Equal(1, writesAtJoin);
            Assert.True(Volatile.Read(ref writeFinished), "teardown returned while the property write's worker still ran");
        }
        finally
        {
            writeRelease.Set();
        }
    }, TimeSpan.FromSeconds(120), "STA test body timed out.");

    // ---- Harness ---------------------------------------------------------------

    /// <summary>A vault on disk and a workspace over it in the app's mode:
    /// interaction work in the background (a History restore on its worker,
    /// a properties header's read on the pool), published on this fact's
    /// dispatcher, which every wait here pumps.</summary>
    private sealed class Vault : IDisposable
    {
        private readonly FixtureVault _fixture;
        private readonly VaultSession _session;

        public Vault(string label, params (string Path, string Text)[] files)
        {
            _fixture = FixtureVault.Create(0, label);
            foreach ((string path, string text) in files)
            {
                string full = Path.Combine(_fixture.Root, path);
                Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                File.WriteAllText(full, text);
            }
            _session = VaultSession.OpenFilesystem(_fixture.Root);
            using (var cancel = new CancelToken())
            {
                _session.ScanInitial(cancel);
            }
            Workspace = new WorkspaceViewModel(
                _session, _fixture.Root, () => [], Events.Add, startInteractionBackgroundWork: true);
            Assert.True(Workspace.StartsInteractionBackgroundWorkForTests);
        }

        public string Root => _fixture.Root;

        public WorkspaceViewModel Workspace { get; }

        public List<A11yEvent> Events { get; } = [];

        /// <summary>A new tab on the path, and what its open spoke dropped.</summary>
        public WorkspaceTabViewModel Open(string path)
        {
            int before = Events.Count;
            Workspace.OpenPath(path, WorkspaceOpenTarget.NewTab);
            Settle();
            Events.RemoveRange(before, Events.Count - before);
            return Workspace.Groups.SelectMany(group => group.Tabs).Last(tab => tab.Path == path);
        }

        public string Read(string path) => File.ReadAllText(Path.Combine(Root, path));

        public void PumpUntil(Func<bool> condition, string what) =>
            Assert.True(PumpedDispatcher.PumpUntil(condition, TimeSpan.FromSeconds(30)), $"{what} never happened");

        public void Wait(ManualResetEventSlim signal, string failure) =>
            Assert.True(PumpedDispatcher.PumpUntil(() => signal.IsSet, Hold), failure);

        /// <summary>A while of pumped turns, for a fact that asserts nothing
        /// happened.</summary>
        public void Settle()
        {
            for (int turn = 0; turn < 20; turn++)
            {
                PumpedDispatcher.Drain();
                Thread.Sleep(5);
            }
        }

        public void Dispose()
        {
            try
            {
                Workspace.Dispose();
                _session.Dispose();
            }
            finally
            {
                _fixture.Dispose();
            }
        }
    }

    /// <summary>A note with two saved versions — v1, then v2 on disk — its
    /// tab open and clean, and the History panel listing it; v1's row.</summary>
    private static (WorkspaceTabViewModel Tab, HistoryVersionRow V1, List<string> Alerts) RestoreSetup(Vault h)
    {
        WorkspaceTabViewModel tab = h.Open("a.md");
        tab.Text = V1;
        SaveNow(h, tab);
        string v1 = tab.SavedContentHash!;
        tab.Text = V2;
        SaveNow(h, tab);
        var alerts = new List<string>();
        h.Workspace.HistoryAlert = (title, message) => alerts.Add($"{title}: {message}");
        h.Workspace.HistoryRestoreConfirmation = _ => true;
        h.Workspace.History.NoteChanged("a.md");
        h.PumpUntil(() => Version(h, v1) is not null, "the history rows loading");
        return (tab, Version(h, v1)!, alerts);
    }

    private static void SaveNow(Vault h, WorkspaceTabViewModel tab)
    {
        Task<bool> save = tab.SaveAsync();
        h.PumpUntil(() => save.IsCompleted, "a setup save");
        Assert.True(save.Result, "premise: a setup save failed");
    }

    private static HistoryVersionRow? Version(Vault h, string hash) =>
        h.Workspace.History.DayGroups
            .SelectMany(group => group.Rows)
            .FirstOrDefault(row => !row.IsMarker && row.ContentHashAfter == hash);

    /// <summary>Two tabs on one note in a split, each with the header its
    /// activation attached — asynchronous, its read on the pool and its
    /// publish posted to the dispatcher, as in the app; A's header the one
    /// that edits, B the tab that saves; the conflict dialog intercepted
    /// and property writes counted on both tabs.</summary>
    private sealed class Peers : IDisposable
    {
        private readonly ManualResetEventSlim _saveParked = new();
        private readonly ManualResetEventSlim _saveRelease = new();
        private readonly ManualResetEventSlim _readRelease = new();
        private readonly WorkspaceGroupViewModel _groupA;
        private readonly WorkspaceGroupViewModel _groupB;
        private int _writes;

        public Peers(string label)
        {
            H = new Vault(label, ("a.md", Note), ("b.md", "# B\n"));
            A = H.Open("a.md");
            _groupA = H.Workspace.ActiveGroup;
            H.Workspace.OpenPath("a.md", WorkspaceOpenTarget.SplitRight);
            H.Settle();
            _groupB = H.Workspace.ActiveGroup;
            B = _groupB.ActiveTab!;
            Assert.NotSame(A, B);
            Assert.Equal("a.md", B.Path);
            ActivateA();
            Properties = H.Workspace.EnsureActiveTabProperties()!;
            H.PumpUntil(() => Properties.Rows.Count > 0 && !Properties.IsLoading, "A's header loading");
            H.Workspace.PropertyConflictDialog = (_, key, _, _) => Dialogs.Add(key);
            A.PropertyWriteHookForTests = () => Interlocked.Increment(ref _writes);
            B.PropertyWriteHookForTests = () => Interlocked.Increment(ref _writes);
            H.Events.Clear();
        }

        public Vault H { get; }

        public WorkspaceTabViewModel A { get; }

        public WorkspaceTabViewModel B { get; }

        public NotePropertiesViewModel Properties { get; }

        public List<string> Dialogs { get; } = [];

        public int Writes => Volatile.Read(ref _writes);

        public ManualResetEventSlim HeaderReadParked { get; } = new();

        public PropertyRowViewModel Row(string key) => Properties.Rows.Single(row => row.Key == key);

        public void ActivateA() => H.Workspace.Activate(_groupA, A);

        /// <summary>B types <paramref name="edited"/> and saves as the Save
        /// command does — announced, its onSaved refreshing the note's
        /// headers (WorkspaceViewModel.SaveActive) — and the write parks.</summary>
        public Task<bool> ParkBsSave(string edited)
        {
            B.Text = edited;
            B.SaveWriteHookForTests = () =>
            {
                _saveParked.Set();
                _ = _saveRelease.Wait(TimeSpan.FromSeconds(30));
            };
            Task<bool> save = B.SaveAsync(
                onSaved: () => H.Workspace.RefreshPropertiesFor(B.Path),
                announce: true);
            H.Wait(_saveParked, "premise: B's save never parked before its write");
            H.Events.Clear();
            return save;
        }

        public void TakeBack(string text)
        {
            B.Text = text;
            Assert.False(A.IsDirty, "premise: taking the edit back left A dirty");
            Assert.False(B.IsDirty, "premise: taking the edit back left B dirty");
            Assert.True(B.HasPendingSaves, "premise: B's save was no longer admitted");
        }

        public void Retype(string text) => B.Text = text;

        public void ReleaseSave() => _saveRelease.Set();

        public void ParkHeaderRead(bool thenFail = false)
        {
            bool failed = false;
            Properties.RefreshReadHookForTests = () =>
            {
                HeaderReadParked.Set();
                _ = _readRelease.Wait(TimeSpan.FromSeconds(30));
                if (thenFail && !failed)
                {
                    failed = true;
                    throw new IOException("the header's read failed (test seam)");
                }
            };
        }

        public void ReleaseHeaderRead() => _readRelease.Set();

        public void Dispose()
        {
            _saveRelease.Set();
            _readRelease.Set();
            B.SaveWriteHookForTests = null;
            Properties.RefreshReadHookForTests = null;
            H.Dispose();
            _saveParked.Dispose();
            _saveRelease.Dispose();
            _readRelease.Dispose();
            HeaderReadParked.Dispose();
        }
    }
}
