// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Concurrent;
using System.Windows.Threading;
using SlateWindows.Bases;
using SlateWindows.FileManagement;
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
    private const string Gone = "# Gone\n\nSaved before it was deleted.\n";
    private static readonly TimeSpan Hold = TimeSpan.FromSeconds(10);
    private const string TaskNote = "---\nstatus: draft\n---\n\n- [ ] the task\n";
    private const string TaskNoteEdited = "---\nstatus: draft\n---\n\n- [ ] the task\nAn edit on its way to disk.\n";
    private const string NotesBase =
        "views:\n  - type: table\n    name: Main\n    order:\n      - file.name\n      - note.status\n";

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

    // ---- Task toggles (#1658) -------------------------------------------------

    /// <summary>A task toggle on an open note waits behind an operation admitted
    /// into the note's chain before it (a History restore, say), and toggles once
    /// when that ends, on the note's saved hash.</summary>
    [Fact]
    public void ATaskToggleBehindAnAdmittedOperationWaitsThenTogglesOnce() => RunSta(() =>
    {
        using var peers = new Peers("toggle-behind-op", TaskNote);
        var ahead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool> held = peers.H.Workspace.SavesForTests.Admit("a.md", _ => ahead.Task);
        var events = new List<A11yEvent>();
        try
        {
            Assert.Equal(TabTaskToggle.Started, peers.A.ToggleTask(TheTask(peers), events.Add));
            peers.H.Settle();
            Assert.Equal(TaskNote, peers.H.Read("a.md"));
            Assert.Empty(events);

            ahead.SetResult();
            peers.H.PumpUntil(
                () => !peers.A.IsTaskToggleInFlight && peers.H.Workspace.SavesIdle,
                "the toggle after the operation ahead of it");
            Assert.True(held.Result);
            Assert.Contains("- [x] the task", peers.H.Read("a.md"), StringComparison.Ordinal);
            _ = Assert.Single(events, e => e is A11yEvent.TaskToggled);
            Assert.DoesNotContain(events, e => e is A11yEvent.TaskToggleConflict);
        }
        finally
        {
            _ = ahead.TrySetResult();
        }
    });

    /// <summary>#1658's window: B's save parks; the reader takes the edit back
    /// (both tabs clean, so the toggle's request gates pass); A toggles a task,
    /// admitted behind the save; the edit is retyped. The save publishes first,
    /// and it changed the note, so the toggle's task (an ordinal in the text it
    /// was asked on) can't be trusted: it refuses as stale, and the save keeps
    /// its bytes.</summary>
    [Fact]
    public void ATaskToggleWhoseNoteASaveChangedWhileItWaitedRefusesAsStale() => RunSta(() =>
    {
        using var peers = new Peers("toggle-behind-save", TaskNote);
        TaskItem task = TheTask(peers);
        var events = new List<A11yEvent>();
        Task<bool> save = peers.ParkBsSave(TaskNoteEdited);
        try
        {
            peers.TakeBack(TaskNote);
            Assert.Equal(TabTaskToggle.Started, peers.A.ToggleTask(task, events.Add));
            peers.Retype(TaskNoteEdited);
            peers.H.Settle();
            Assert.DoesNotContain("- [x]", peers.H.Read("a.md"), StringComparison.Ordinal);

            peers.ReleaseSave();
            peers.H.PumpUntil(
                () => save.IsCompleted && !peers.A.IsTaskToggleInFlight && peers.H.Workspace.SavesIdle,
                "the save and then the toggle's refusal");
            Assert.True(save.Result);
            Assert.Equal(TaskNoteEdited, peers.H.Read("a.md"));
            _ = Assert.Single(events, e => e is A11yEvent.TaskToggleConflict);
            Assert.DoesNotContain(events, e => e is A11yEvent.TaskToggled);
        }
        finally
        {
            peers.ReleaseSave();
        }
    });

    /// <summary>The toggle waits behind an admitted operation; the reader types
    /// in the note meanwhile, so it is dirty when the toggle's turn comes: the
    /// toggle refuses with the unsaved-note sentence and writes nothing.</summary>
    [Fact]
    public void ATaskToggleThatFindsItsNoteDirtyWhenItStartsRefusesAndWritesNothing() => RunSta(() =>
    {
        using var peers = new Peers("toggle-dirty-at-start", TaskNote);
        var ahead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool> held = peers.H.Workspace.SavesForTests.Admit("a.md", _ => ahead.Task);
        var events = new List<A11yEvent>();
        try
        {
            Assert.Equal(TabTaskToggle.Started, peers.A.ToggleTask(TheTask(peers), events.Add));
            peers.A.Text = TaskNote + "An unsaved line.\n";
            Assert.True(peers.A.IsDirty);

            ahead.SetResult();
            peers.H.PumpUntil(
                () => !peers.A.IsTaskToggleInFlight && peers.H.Workspace.SavesIdle,
                "the toggle's refusal");
            Assert.True(held.Result);
            Assert.Equal(TaskNote, peers.H.Read("a.md"));
            _ = Assert.Single(events, e => e is A11yEvent.TaskToggleUnsaved);
            Assert.DoesNotContain(events, e => e is A11yEvent.TaskToggled);
        }
        finally
        {
            _ = ahead.TrySetResult();
        }
    });

    /// <summary>A toggle whose worker throws something other than a vault
    /// error (a panic surfacing through the binding), before its write or after
    /// it, still ends: the in-flight flag goes, the note's chain is freed, and
    /// the note's next save runs to its end (codex round 1).</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ATaskToggleWhoseWorkerThrowsEndsAndFreesTheNotesChain(bool afterTheWrite) => RunSta(() =>
    {
        using var peers = new Peers(afterTheWrite ? "toggle-throws-after" : "toggle-throws-before", TaskNote);
        Action thrower = () => throw new IOException("a panic surfacing through the binding (test seam)");
        if (afterTheWrite)
        {
            peers.A.TaskToggleFaultForTests = thrower;
        }
        else
        {
            peers.A.TaskToggleBeforeWriteForTests = thrower;
        }
        var events = new List<A11yEvent>();
        try
        {
            Assert.Equal(TabTaskToggle.Started, peers.A.ToggleTask(TheTask(peers), events.Add));
            peers.H.PumpUntil(
                () => !peers.A.IsTaskToggleInFlight && peers.H.Workspace.SavesIdle,
                "the throwing toggle's end");
            Assert.DoesNotContain(events, e => e is A11yEvent.TaskToggled);

            peers.A.TaskToggleFaultForTests = null;
            peers.A.TaskToggleBeforeWriteForTests = null;
            peers.A.Text = peers.A.Text + "A later edit.\n";
            Task<bool> save = peers.A.SaveAsync();
            peers.H.PumpUntil(() => save.IsCompleted && peers.H.Workspace.SavesIdle, "the next save");
        }
        finally
        {
            peers.A.TaskToggleFaultForTests = null;
            peers.A.TaskToggleBeforeWriteForTests = null;
        }
    });

    /// <summary>A toggle refused when its turn comes, dirty or stale, whose
    /// refusal sentence throws on its way out (the announcer reaches UIA): the
    /// toggle still ends, so the tab's next toggle starts rather than being
    /// refused as busy (codex round 2).</summary>
    [Theory]
    [InlineData("dirty")]
    [InlineData("stale")]
    public void ATaskToggleRefusedAtItsTurnEndsEvenWhenItsSentenceThrows(string refusal) => RunSta(() =>
    {
        using var peers = new Peers("toggle-refusal-throws-" + refusal, TaskNote);
        TaskItem task = TheTask(peers);
        var ahead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool>? held = null;
        Task<bool>? save = null;
        void Throwing(A11yEvent spoken)
        {
            if (spoken is A11yEvent.TaskToggleUnsaved or A11yEvent.TaskToggleConflict)
            {
                throw new System.Runtime.InteropServices.COMException("the announcer failed (test seam)");
            }
        }
        try
        {
            if (refusal == "dirty")
            {
                held = peers.H.Workspace.SavesForTests.Admit("a.md", _ => ahead.Task);
                Assert.Equal(TabTaskToggle.Started, peers.A.ToggleTask(task, Throwing));
                peers.A.Text = TaskNote + "An unsaved line.\n";
                ahead.SetResult();
            }
            else
            {
                save = peers.ParkBsSave(TaskNoteEdited);
                peers.TakeBack(TaskNote);
                Assert.Equal(TabTaskToggle.Started, peers.A.ToggleTask(task, Throwing));
                peers.Retype(TaskNoteEdited);
                peers.ReleaseSave();
            }
            peers.H.PumpUntil(
                () => !peers.A.IsTaskToggleInFlight && peers.H.Workspace.SavesIdle,
                "the refused toggle's end");
            if (refusal == "dirty")
            {
                peers.A.Text = TaskNote;
            }
            Assert.False(peers.A.IsDirty, "premise: the tab is clean for the next toggle");
            TaskItem next = peers.H.Session.TasksForFile("a.md")[0];
            Assert.Equal(TabTaskToggle.Started, peers.A.ToggleTask(next, _ => { }));
            peers.H.PumpUntil(
                () => !peers.A.IsTaskToggleInFlight && peers.H.Workspace.SavesIdle,
                "the next toggle");
        }
        finally
        {
            _ = ahead.TrySetResult();
            peers.ReleaseSave();
        }
    });

    private static TaskItem TheTask(Peers peers) => peers.H.Session.TasksForFile("a.md")[0];

    // ---- Bases cell writes (#1657) -------------------------------------------

    /// <summary>B's save parks; the reader takes the edit back (both tabs
    /// clean: the window); a Bases cell edit of a.md's status is admitted
    /// behind the save; the edit is retyped. No write until release; then
    /// NoteSaved, then one cell write on the saved hash, over the saved
    /// bytes.</summary>
    [Fact]
    public void ABasesCellEditWaitsForTheNotesAdmittedSaveAndWritesOnce() => RunSta(() =>
    {
        using var peers = new Peers("bases-behind-save", ("Notes.base", NotesBase));
        (BaseDocumentViewModel document, BasesRow row, BasesColumn column) = StatusCell(peers);
        Task<bool> save = peers.ParkBsSave(NoteEdited);
        try
        {
            peers.TakeBack(Note);
            document.ApplyPropertyEdit!(row, column, new PropertyValue.Text("done"));
            peers.Retype(NoteEdited);
            peers.H.Settle();
            Assert.Equal(0, peers.Writes);
            Assert.DoesNotContain("status: done", peers.H.Read("a.md"), StringComparison.Ordinal);

            peers.ReleaseSave();
            peers.H.PumpUntil(
                () => save.IsCompleted && peers.Writes == 1 && peers.H.Workspace.SavesIdle,
                "the save and then the cell write");
            Assert.True(save.Result);
            _ = Assert.Single(peers.H.Events, e => e is A11yEvent.NoteSaved);
            Assert.DoesNotContain(peers.H.Events, e => e is A11yEvent.BasesCellEditFailed or A11yEvent.PropertyEditConflict);
            Assert.Equal(1, peers.Writes);
            string saved = peers.H.Read("a.md");
            Assert.Contains("An edit on its way to disk.", saved, StringComparison.Ordinal);
            Assert.Contains("status: done", saved, StringComparison.Ordinal);
            Assert.False(peers.H.Workspace.PropertyWriteInFlightFor("a.md"));
        }
        finally
        {
            peers.ReleaseSave();
        }
    });

    /// <summary>The cell edit waits behind the save; the reader's take-back
    /// is not retyped, so the save leaves the note dirty: when the cell
    /// write's turn comes it refuses with the dirty-note sentence and writes
    /// nothing, and its lease is released.</summary>
    [Fact]
    public void ABasesCellEditThatFindsItsNoteDirtyWhenItStartsRefusesAndWritesNothing() => RunSta(() =>
    {
        using var peers = new Peers("bases-dirty-at-start", ("Notes.base", NotesBase));
        (BaseDocumentViewModel document, BasesRow row, BasesColumn column) = StatusCell(peers);
        Task<bool> save = peers.ParkBsSave(NoteEdited);
        try
        {
            peers.TakeBack(Note);
            document.ApplyPropertyEdit!(row, column, new PropertyValue.Text("done"));
            peers.H.Settle();
            Assert.Equal(0, peers.Writes);

            peers.ReleaseSave();
            peers.H.PumpUntil(
                () => save.IsCompleted && peers.H.Workspace.SavesIdle && !peers.H.Workspace.PropertyWriteInFlightFor("a.md"),
                "the save and then the cell write's refusal");
            Assert.True(save.Result);
            Assert.Equal(0, peers.Writes);
            Assert.DoesNotContain("status: done", peers.H.Read("a.md"), StringComparison.Ordinal);
            Assert.Contains(peers.H.Events, e => e is A11yEvent.HostComposed composed
                && composed.Text.StartsWith("Save the note before editing properties.", StringComparison.Ordinal));
        }
        finally
        {
            peers.ReleaseSave();
        }
    });

    /// <summary>The cell edit waits behind another admitted operation on the
    /// note (a History restore, say) while the reader closes both tabs on
    /// it, and the note's file is left as it was, replaced on disk, or
    /// replaced byte for byte (a note renamed away and copied back), or a
    /// replacement is reopened before the write's turn, in a tab of its own
    /// or in the original tab re-pointed away and back. None of those shows
    /// the document the edit was made on, so the write refuses and the file
    /// keeps its bytes (codex rounds 2 to 5).</summary>
    [Theory]
    [InlineData("closed")]
    [InlineData("replaced")]
    [InlineData("replaced byte for byte")]
    [InlineData("reopened in a new tab")]
    [InlineData("reopened in the original tab")]
    public void ABasesCellEditWhoseNoteLosesItsTabsWhileItWaitsWritesNothing(string what) => RunSta(() =>
    {
        using var peers = new Peers("bases-tabs-lost", ("Notes.base", NotesBase));
        (BaseDocumentViewModel document, BasesRow row, BasesColumn column) = StatusCell(peers);
        var ahead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool> held = peers.H.Workspace.SavesForTests.Admit("a.md", _ => ahead.Task);
        string onDisk = what switch
        {
            "closed" or "replaced byte for byte" => Note,
            _ => "---\nstatus: someone else's\n---\n\nA different note.\n",
        };
        bool keepA = what == "reopened in the original tab";
        try
        {
            document.ApplyPropertyEdit!(row, column, new PropertyValue.Text("done"));
            if (keepA)
            {
                // A goes to another note in place; B closes.
                peers.ActivateA();
                peers.H.Workspace.OpenPath("b.md", WorkspaceOpenTarget.CurrentTab);
                peers.H.Settle();
                Assert.Equal("b.md", peers.A.Path);
            }
            else
            {
                peers.H.Workspace.CloseTabCommand.Execute(peers.A);
            }
            peers.H.Workspace.CloseTabCommand.Execute(peers.B);
            peers.H.Settle();
            Assert.DoesNotContain(peers.H.Workspace.Groups.SelectMany(group => group.Tabs), tab => tab.Path == "a.md");
            if (what != "closed")
            {
                File.WriteAllText(Path.Combine(peers.H.Root, "a.md"), onDisk);
            }
            if (what == "reopened in a new tab")
            {
                WorkspaceTabViewModel shown = peers.H.Open("a.md");
                Assert.Equal(onDisk, shown.Text);
                Assert.False(shown.IsDirty);
            }
            else if (keepA)
            {
                peers.ActivateA();
                peers.H.Workspace.OpenPath("a.md", WorkspaceOpenTarget.CurrentTab);
                peers.H.Settle();
                Assert.Equal("a.md", peers.A.Path);
                Assert.Equal(onDisk, peers.A.Text);
                Assert.False(peers.A.IsDirty);
            }
            peers.H.Events.Clear();

            ahead.SetResult();
            peers.H.PumpUntil(
                () => peers.H.Workspace.SavesIdle && !peers.H.Workspace.PropertyWriteInFlightFor("a.md"),
                "the cell write's end");
            Assert.True(held.Result);
            Assert.Equal(onDisk, peers.H.Read("a.md"));
            Assert.Equal(0, peers.Writes);
            A11yEvent refusal = Assert.Single(
                peers.H.Events, e => e is A11yEvent.PropertyEditConflict or A11yEvent.BasesCellEditFailed);
            Assert.Equal(
                what is "closed" or "replaced" or "replaced byte for byte",
                refusal is A11yEvent.BasesCellEditFailed);
        }
        finally
        {
            _ = ahead.TrySetResult();
        }
    });

    private static (BaseDocumentViewModel Document, BasesRow Row, BasesColumn Column) StatusCell(Peers peers)
    {
        BaseDocumentViewModel document = peers.H.Workspace.BaseDocumentFor("Notes.base");
        peers.H.PumpUntil(
            () => document.Result?.Rows.Any(row => row.FilePath == "a.md") == true,
            "the base's rows");
        BasesRow row = document.Result!.Rows.Single(candidate => candidate.FilePath == "a.md");
        BasesColumn column = document.Result.Columns.Single(candidate => candidate.Id == "note.status");
        peers.H.Events.Clear();
        return (document, row, column);
    }

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

    /// <summary>#1673's sibling: the fixture's teardown waits for a header
    /// read it let go. The read is parked, then released by the teardown,
    /// and stands in the hook, before its session call, until the teardown
    /// says it is waiting or says the session is gone; the witness counts a
    /// read let through only by the second.</summary>
    [Fact]
    public void DisposingThePeersWaitsForAHeaderReadItReleased() => RunSta(() =>
    {
        var peers = new Peers("teardown-header-read");
        TeardownWitness teardown = peers.Teardown;
        try
        {
            peers.HoldTheHeaderReadAtTeardown();
            peers.ParkHeaderRead();
            peers.Properties.RefreshProperties();
            peers.H.Wait(peers.HeaderReadParked, "premise: the header's read never parked");
        }
        finally
        {
            peers.Dispose();
        }

        teardown.AssertTheTeardownWaited("the header read the fixture released");
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

    // ---- The rescan's reload (plan 47 CB-9a, #1889) ---------------------------

    /// <summary>A rescan's reload of a changed note is unadmitted, so it asks
    /// the chain: while any operation is admitted for the note, a clean tab
    /// keeps its buffer and its document and is marked stale against the
    /// index hash. The next rescan, on an idle chain, reloads it.</summary>
    [Fact]
    public void ARescanNeverReloadsATabUnderAnOperationAdmittedForItsNote() => RunSta(() =>
    {
        const string Outside = "written outside Slate\n";
        using var h = new Vault("rescan-under-admitted", ("a.md", "v0\n"));
        WorkspaceTabViewModel tab = h.OpenNow("a.md");
        var ahead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool> held = h.Workspace.SavesForTests.Admit("a.md", _ => ahead.Task);
        try
        {
            int document = tab.ItemIdentity;
            File.WriteAllText(Path.Combine(h.Root, "a.md"), Outside);
            using (var scan = new CancelToken())
            {
                _ = h.Session.Rescan(scan);
            }

            h.ReSyncDocuments("a.md");
            Assert.Equal(document, tab.ItemIdentity);
            Assert.Equal("v0\n", tab.Text);
            Assert.True(tab.IsExternallyStale, "the tab kept under the admitted operation was not marked stale");

            ahead.SetResult();
            h.PumpUntil(() => held.IsCompleted && h.Workspace.SavesIdle, "the operation ahead ending");
            h.ReSyncDocuments("a.md");
            Assert.NotEqual(document, tab.ItemIdentity);
            Assert.Equal(Outside, tab.Text);
            Assert.False(tab.IsExternallyStale);
            Assert.False(tab.IsDirty);
        }
        finally
        {
            _ = ahead.TrySetResult();
        }
    });

    // ---- A deleted file's recovery (plan 47 CB-9a, #1889; CD5(c)) -------------

    /// <summary>A recover asked while another operation is admitted for the
    /// path takes a ticket behind it: nothing is created until that
    /// operation ends, and then the file is recovered once.</summary>
    [Fact]
    public void ARecoverStartedUnderAnAdmittedOperationWaitsThenRecoversOnce() => RunSta(() =>
    {
        using var h = Vault.WithADeletedNote("recover-behind-op", "gone.md", Gone);
        (HistoryDeletedRow row, ConcurrentQueue<string> alerts) = RecoverSetup(h);
        var ahead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool> held = h.Workspace.SavesForTests.Admit("gone.md", _ => ahead.Task);
        var answers = new ConcurrentQueue<(bool Ok, bool Collision)>();
        try
        {
            h.Workspace.RecoverDeletedFile(row, (ok, collision) => answers.Enqueue((ok, collision)));
            Assert.Equal(2, h.Workspace.SavesForTests.AdmittedForTests);
            PumpedDispatcher.Drain();
            Assert.False(File.Exists(Path.Combine(h.Root, "gone.md")), "the recover created the file beside the operation admitted before it");
            Assert.Empty(answers);

            ahead.SetResult();
            h.PumpUntil(() => !answers.IsEmpty && h.Workspace.SavesIdle, "the recover after the operation ahead of it");
            Assert.True(held.Result);
            Assert.Equal([(true, false)], answers);
            _ = Assert.Single(h.Events, e => e is A11yEvent.RestoredFile);
            Assert.Equal(Gone, h.Read("gone.md"));
            Assert.Empty(alerts);
        }
        finally
        {
            _ = ahead.TrySetResult();
            h.ReleaseAndDrain();
        }
    });

    /// <summary>A tab that names the deleted path and holds no hash saves by
    /// create. Its save
    /// is held before its write when the recover is asked, so the recover
    /// waits; the save then creates the file, and core's exclusive create
    /// answers the recover that the path is taken: the row opens Restore As,
    /// nothing is spoken for the recover, and the file keeps the tab's
    /// text.</summary>
    [Fact]
    public void ARecoverBehindAGhostTabsSaveAnswersDestinationExistsAndOverwritesNothing() => RunSta(() =>
    {
        using var h = Vault.WithADeletedNote("recover-behind-ghost-save", "gone.md", Gone);
        (HistoryDeletedRow row, ConcurrentQueue<string> alerts) = RecoverSetup(h);
        WorkspaceTabViewModel ghost = h.OpenNow("gone.md");
        Assert.Null(ghost.SavedContentHash);
        var held = new HeldWork(hook => ghost.SaveWriteHookForTests = hook);
        var answers = new ConcurrentQueue<(bool Ok, bool Collision)>();
        try
        {
            ghost.Text = Edit;
            Task<bool> save = ghost.SaveAsync(announce: true);
            h.PumpUntil(() => held.Entered, "premise: the save reaching its hold before its write");
            h.Events.Clear();

            h.Workspace.RecoverDeletedFile(row, (ok, collision) => answers.Enqueue((ok, collision)));
            Assert.Equal(2, h.Workspace.SavesForTests.AdmittedForTests);
            PumpedDispatcher.Drain();
            Assert.False(File.Exists(Path.Combine(h.Root, "gone.md")), "the recover created the file beside the save admitted before it");

            held.Release();
            h.PumpUntil(() => save.IsCompleted && !answers.IsEmpty && h.Workspace.SavesIdle, "the save and then the recover's answer");
            Assert.True(save.Result);
            _ = Assert.Single(h.Events, e => e is A11yEvent.NoteSaved);
            Assert.Equal([(false, true)], answers);
            Assert.DoesNotContain(h.Events, e => e is A11yEvent.RestoredFile);
            Assert.Empty(alerts);
            Assert.Equal(Edit, h.Read("gone.md"));
        }
        finally
        {
            h.ReleaseAndDrain(held);
            ghost.SaveWriteHookForTests = null;
        }
        held.AssertHeldUntilReleased();
    });

    /// <summary>The other direction: the recover is held before its create
    /// when a tab on the path asks to save. The save waits for the recover's
    /// publication, then finds the recovered file and is blocked, so the
    /// recovered bytes are kept and the tab keeps its edits.</summary>
    [Fact]
    public void ASaveStartedUnderAnAdmittedRecoverWaitsForItsPublication() => RunSta(() =>
    {
        using var h = Vault.WithADeletedNote("save-behind-recover", "gone.md", Gone);
        (HistoryDeletedRow row, ConcurrentQueue<string> alerts) = RecoverSetup(h);
        WorkspaceTabViewModel ghost = h.OpenNow("gone.md");
        var held = new HeldWork(hook => h.Workspace.HistoryCreateHookForTests = hook);
        var answers = new ConcurrentQueue<(bool Ok, bool Collision)>();
        try
        {
            h.Events.Clear();
            h.Workspace.RecoverDeletedFile(row, (ok, collision) => answers.Enqueue((ok, collision)));
            h.PumpUntil(() => held.Entered, "premise: the recover reaching its hold before its create");

            ghost.Text = Edit;
            Task<bool> save = ghost.SaveAsync(announce: true);
            Assert.Equal(2, h.Workspace.SavesForTests.AdmittedForTests);
            PumpedDispatcher.Drain();
            Assert.False(save.IsCompleted, "the save did not wait for the recover");

            held.Release();
            h.PumpUntil(() => save.IsCompleted && !answers.IsEmpty && h.Workspace.SavesIdle, "the recover and then the save");
            Assert.Equal([(true, false)], answers);
            Assert.False(save.Result, "the save overwrote the recovered file");
            _ = Assert.Single(h.Events, e => e is A11yEvent.RestoredFile);
            _ = Assert.Single(h.Events, e => e is A11yEvent.NoteSaveBlocked);
            Assert.True(
                h.Events.FindIndex(e => e is A11yEvent.RestoredFile) < h.Events.FindIndex(e => e is A11yEvent.NoteSaveBlocked),
                "the save spoke before the recover it waited for");
            Assert.Empty(alerts);
            Assert.Equal(Gone, h.Read("gone.md"));
            Assert.Equal(Edit, ghost.Text);
        }
        finally
        {
            h.ReleaseAndDrain(held);
        }
        held.AssertHeldUntilReleased();
    });

    /// <summary>The admission moves the recover's publication behind the
    /// `Created` event's re-seat: the listener posts that event to the
    /// dispatcher before the worker posts its continuation, so the re-seat
    /// runs inside the ticket, sees an operation admitted for the path, and
    /// only marks a clean tab back on disk. The recover therefore reloads
    /// every clean tab on its path itself, inside its ticket: the tab shows
    /// the recovered file as a new document, with that file's identity. It
    /// takes a vault opened through the lifecycle, whose event listener is
    /// attached, in background mode; no workspace-level fact receives the
    /// event.</summary>
    [Fact]
    public void ARecoverReloadsACleanTabThatWasMissingOnItsPath() => RunSta(() =>
    {
        using var v = new OpenedVault("recover-reloads-tab", "gone.md", Gone);
        v.Workspace.OpenPath("gone.md", WorkspaceOpenTarget.NewTab);
        // What a Deleted event does to a tab on the path (the route
        // FinalMergeDeltaTests takes to a missing tab).
        v.Workspace.InvalidatePath("gone.md");
        PumpedDispatcher.Drain();
        WorkspaceTabViewModel tab = v.Workspace.Groups.SelectMany(group => group.Tabs).Single(candidate => candidate.Path == "gone.md");
        Assert.True(tab.IsMarkdown);
        Assert.False(tab.IsDirty);
        Assert.True(tab.IsMissingFromDisk, "premise: the tab on the deleted path does not report its file missing");
        int document = tab.ItemIdentity;
        string? identity = tab.FileIdentity;

        v.Workspace.History.LoadDeletedFiles();
        Assert.True(
            PumpedDispatcher.PumpUntil(
                () => v.Workspace.History.DeletedRows.Any(row => row.Path == "gone.md"), TimeSpan.FromSeconds(30)),
            "the deleted files list never loaded");
        HistoryDeletedRow deleted = v.Workspace.History.DeletedRows.Single(row => row.Path == "gone.md");
        var answers = new ConcurrentQueue<(bool Ok, bool Collision)>();
        v.Workspace.RecoverDeletedFile(deleted, (ok, collision) => answers.Enqueue((ok, collision)));
        Assert.True(
            PumpedDispatcher.PumpUntil(() => !answers.IsEmpty && v.Workspace.SavesIdle, TimeSpan.FromSeconds(30)),
            "the recover never answered its row");
        // The `Created` event's own turn, whichever side of the publication it
        // fell on, has run by the time the dispatcher is drained.
        PumpedDispatcher.Drain();

        Assert.Equal([(true, false)], answers);
        Assert.False(tab.IsMissingFromDisk);
        Assert.Equal(Gone, tab.Text);
        Assert.False(tab.IsDirty);
        Assert.NotEqual(document, tab.ItemIdentity);
        Assert.NotNull(tab.FileIdentity);
        Assert.NotEqual(identity, tab.FileIdentity);
    });

    /// <summary>The red team's F4: the reload stands INSIDE the recover's
    /// ticket. The recover is held before its create; the clean missing
    /// tab asks to save, which queues behind the recover; the save records
    /// what the tab shows when it runs. It runs against the recovered
    /// text, on a document that has already moved. The row's answer is the
    /// sharper witness: it is given inside the ticket, with both tickets
    /// still counted, and the tab is reloaded by then. A reload posted to
    /// run after the operation would not have run yet.</summary>
    [Fact]
    public void ASaveQueuedBehindARecoverRunsAgainstTheTabTheRecoverReloaded() => RunSta(() =>
    {
        using var v = new OpenedVault("recover-reloads-inside-ticket", "gone.md", Gone);
        v.Workspace.OpenPath("gone.md", WorkspaceOpenTarget.NewTab);
        v.Workspace.InvalidatePath("gone.md");
        PumpedDispatcher.Drain();
        WorkspaceTabViewModel tab = v.Workspace.Groups.SelectMany(group => group.Tabs).Single(candidate => candidate.Path == "gone.md");
        Assert.True(tab.IsMissingFromDisk, "premise: the tab on the deleted path does not report its file missing");
        Assert.False(tab.IsDirty);
        int document = tab.ItemIdentity;
        v.Workspace.History.LoadDeletedFiles();
        Assert.True(
            PumpedDispatcher.PumpUntil(
                () => v.Workspace.History.DeletedRows.Any(row => row.Path == "gone.md"), TimeSpan.FromSeconds(30)),
            "the deleted files list never loaded");
        HistoryDeletedRow deleted = v.Workspace.History.DeletedRows.Single(row => row.Path == "gone.md");
        var held = new HeldWork(hook => v.Workspace.HistoryCreateHookForTests = hook);
        var answers = new ConcurrentQueue<(bool Ok, bool Collision)>();
        try
        {
            string? textAtTheAnswer = null;
            int documentAtTheAnswer = -1;
            int admittedAtTheAnswer = -1;
            v.Workspace.RecoverDeletedFile(deleted, (ok, collision) =>
            {
                textAtTheAnswer = tab.Text;
                documentAtTheAnswer = tab.ItemIdentity;
                admittedAtTheAnswer = v.Workspace.SavesForTests.AdmittedForTests;
                answers.Enqueue((ok, collision));
            });
            Assert.True(PumpedDispatcher.PumpUntil(() => held.Entered, TimeSpan.FromSeconds(30)), "premise: the recover never reached its hold");

            string? textAtTheSave = null;
            int documentAtTheSave = -1;
            Task<bool> save = tab.SaveAsync(onSaved: () =>
            {
                textAtTheSave = tab.Text;
                documentAtTheSave = tab.ItemIdentity;
            });
            Assert.Equal(2, v.Workspace.SavesForTests.AdmittedForTests);

            held.Release();
            Assert.True(
                PumpedDispatcher.PumpUntil(() => save.IsCompleted && !answers.IsEmpty && v.Workspace.SavesIdle, TimeSpan.FromSeconds(30)),
                "the recover and then the save never ended");
            Assert.Equal([(true, false)], answers);
            Assert.True(save.Result);
            Assert.Equal(2, admittedAtTheAnswer);
            Assert.Equal(Gone, textAtTheAnswer);
            Assert.NotEqual(document, documentAtTheAnswer);
            Assert.Equal(Gone, textAtTheSave);
            Assert.NotEqual(document, documentAtTheSave);
            Assert.Equal(tab.ItemIdentity, documentAtTheSave);
        }
        finally
        {
            held.Release();
            _ = PumpedDispatcher.PumpUntil(() => v.Workspace.SavesIdle, TimeSpan.FromSeconds(30));
        }
        held.AssertHeldUntilReleased();
    });

    /// <summary>A vault opened through the vault lifecycle, as the app opens
    /// one: its event listener is attached, so a file core creates reaches
    /// the workspace as a `Created` event, and interaction work runs in the
    /// background. Its note at <c>deleted</c> was saved and deleted in an
    /// earlier session.</summary>
    private sealed class OpenedVault : IDisposable
    {
        private readonly SynchronizationContext? _previous;
        private readonly string _state;

        public OpenedVault(string label, string deleted, string text)
        {
            _previous = SynchronizationContext.Current;
            Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            Root = Path.Combine(Path.GetTempPath(), $"slate-save-chain-{label}-{Guid.NewGuid():N}");
            _state = Root + "-state";
            Directory.CreateDirectory(Root);
            Directory.CreateDirectory(_state);
            File.WriteAllText(Path.Combine(Root, "keeper.md"), "# Keeper\n");
            using (VaultSession earlier = VaultSession.OpenFilesystem(Root))
            {
                using (var scan = new CancelToken())
                {
                    earlier.ScanInitial(scan);
                }
                _ = earlier.SaveText(deleted, text, expectedContentHash: null);
                StaThread.Run(() => earlier.DeleteFile(deleted));
            }
            Lifecycle = new VaultLifecycleViewModel(
                pickVault: () => Task.FromResult<string?>(Root),
                enqueueUi: action => dispatcher.BeginInvoke(action),
                recentVaultsStore: new RecentVaultsStore(Path.Combine(_state, "recent-vaults.json")),
                announce: spoken =>
                {
                    BeforeAnnounce?.Invoke(spoken);
                    Events.Add(spoken);
                },
                confirmUnsavedClose: () => VaultCloseDecision.Discard);
            Task open = Lifecycle.OpenVaultAsync(Root);
            Assert.True(PumpedDispatcher.PumpUntil(() => open.IsCompleted, TimeSpan.FromSeconds(60)), "the vault never opened");
            open.GetAwaiter().GetResult();
            Workspace = Assert.IsType<WorkspaceViewModel>(Lifecycle.Workspace);
            Assert.True(Workspace.StartsInteractionBackgroundWorkForTests);
        }

        public string Root { get; }

        public VaultLifecycleViewModel Lifecycle { get; }

        public WorkspaceViewModel Workspace { get; }

        public List<A11yEvent> Events { get; } = [];

        /// <summary>Runs before each announcement is recorded: a fact makes
        /// the announcer throw here.</summary>
        public Action<A11yEvent>? BeforeAnnounce { get; set; }

        public void Dispose()
        {
            try
            {
                Lifecycle.Dispose();
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(_previous);
                foreach (string folder in (string[])[Root, _state])
                {
                    try
                    {
                        Directory.Delete(folder, recursive: true);
                    }
                    catch (IOException)
                    {
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                }
            }
        }
    }

    /// <summary>Found and fixed with the recover: a Restore As whose worker
    /// meets a fault outside VaultException (a core panic) still answers its
    /// row. It is a failed create, the existing "Can't restore" alert with
    /// the fault's message, and nothing is created.</summary>
    [Theory]
    [InlineData("version")]
    [InlineData("deleted")]
    public void ARestoreAsWhoseWorkerFaultsStillAnswersItsRow(string what) => RunSta(() =>
    {
        const string Destination = "restored copy.md";
        using Vault h = what == "version"
            ? new Vault("restore-as-faults-version", ("a.md", "v0\n"))
            : Vault.WithADeletedNote("restore-as-faults-deleted", "gone.md", Gone);
        var alerts = new ConcurrentQueue<string>();
        var answers = new ConcurrentQueue<(bool Ok, string? Refusal)>();
        HistoryVersionRow? version = what == "version" ? AnOlderVersion(h) : null;
        h.Workspace.HistoryAlert = (title, message) => alerts.Enqueue($"{title}: {message}");
        h.Workspace.HistoryCreateHookForTests = () =>
            throw new InvalidOperationException("the create's core call panicked (test seam)");
        try
        {
            if (version is not null)
            {
                h.Workspace.CommitRestoreAsVersion(
                    version.ContentHashAfter, version.AbsoluteDate, Destination, (ok, refusal) => answers.Enqueue((ok, refusal)));
            }
            else
            {
                h.Workspace.CommitRestoreAsDeleted("gone.md", Destination, (ok, refusal) => answers.Enqueue((ok, refusal)));
            }
            h.PumpUntil(() => !answers.IsEmpty, "the faulted Restore As answering its row");
            Assert.Equal([(false, null)], answers);
            Assert.Equal(["Can't restore: the create's core call panicked (test seam)"], alerts);
            Assert.False(File.Exists(Path.Combine(h.Root, Destination)));
        }
        finally
        {
            h.Workspace.HistoryCreateHookForTests = null;
            h.ReleaseAndDrain();
        }
    });

    // ---- A Bases cell edit on a note with no tab (plan 47 CB-9a, #1889) -------

    /// <summary>A cell edit on a note with no tab takes the note's property
    /// lease and a ticket in the note's chain: nothing is written until the
    /// operation admitted before it ends, and then it writes once.</summary>
    [Fact]
    public void ATablessBasesCellEditWaitsForAnOperationAdmittedForTheNoteAndWritesOnce() => RunSta(() =>
    {
        using var h = new Vault("tabless-cell-behind-op", ("a.md", Note), ("Notes.base", NotesBase));
        (BaseDocumentViewModel document, BasesRow row, BasesColumn column) = TablessStatusCell(h);
        int writes = 0;
        h.Workspace.BasesTablessWriteHookForTests = () => Interlocked.Increment(ref writes);
        var ahead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool> held = h.Workspace.SavesForTests.Admit("a.md", _ => ahead.Task);
        try
        {
            document.ApplyPropertyEdit!(row, column, new PropertyValue.Text("done"));
            Assert.Equal(2, h.Workspace.SavesForTests.AdmittedForTests);
            PumpedDispatcher.Drain();
            Assert.Equal(0, Volatile.Read(ref writes));
            Assert.DoesNotContain("status: done", h.Read("a.md"), StringComparison.Ordinal);
            Assert.True(h.Workspace.PropertyWriteInFlightFor("a.md"), "the lease was not held while the write waited");

            ahead.SetResult();
            h.PumpUntil(
                () => h.Events.Any(e => e is A11yEvent.BasesCellSaved) && h.Workspace.SavesIdle,
                "the cell write after the operation ahead of it");
            Assert.True(held.Result);
            Assert.Equal(1, Volatile.Read(ref writes));
            Assert.Contains("status: done", h.Read("a.md"), StringComparison.Ordinal);
            _ = Assert.Single(h.Events, e => e is A11yEvent.BasesCellSaved);
            Assert.DoesNotContain(h.Events, e => e is A11yEvent.BasesCellEditFailed or A11yEvent.PropertyEditConflict);
            Assert.False(h.Workspace.PropertyWriteInFlightFor("a.md"));
        }
        finally
        {
            _ = ahead.TrySetResult();
            h.Workspace.BasesTablessWriteHookForTests = null;
            h.ReleaseAndDrain();
        }
    });

    /// <summary>A pin of the present behaviour, not the wanted one (issue
    /// #1980). A Bases row carries no file identity, so a cell edit on a
    /// note with no tab names a path and nothing else. While the edit waits
    /// its turn, the note is renamed away and another note is created at
    /// the path. At its turn the edit lands on the note that is at the path
    /// now: the property is written into the new note, the renamed note is
    /// untouched, and the cell's saved sentence is spoken once. When #1980
    /// gives the row an identity this fact turns into a refusal: nothing is
    /// written, and the reader hears that the row no longer matches.</summary>
    [Fact]
    public void ATablessBasesCellEditLandsOnTheNoteAtItsPathAtItsTurnEvenWhenThatNoteWasReplaced() => RunSta(() =>
    {
        const string Replacement = "---\nstatus: other\n---\n\nAnother note at the path.\n";
        using var h = new Vault("tabless-cell-note-replaced", ("a.md", Note), ("Notes.base", NotesBase));
        (BaseDocumentViewModel document, BasesRow row, BasesColumn column) = TablessStatusCell(h);
        var ahead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool> held = h.Workspace.SavesForTests.Admit("a.md", _ => ahead.Task);
        try
        {
            document.ApplyPropertyEdit!(row, column, new PropertyValue.Text("done"));
            Assert.Equal(2, h.Workspace.SavesForTests.AdmittedForTests);

            // The note the row was read from moves away, and another takes
            // its path, while the edit waits.
            _ = h.Session.RenameFile("a.md", "b.md");
            _ = h.Session.SaveText("a.md", Replacement, expectedContentHash: null);
            Assert.Equal(Note, h.Read("b.md"));
            Assert.Equal(Replacement, h.Read("a.md"));
            h.Events.Clear();

            ahead.SetResult();
            h.PumpUntil(
                () => h.Workspace.SavesIdle
                    && !h.Workspace.PropertyWriteInFlightFor("a.md")
                    && h.Events.Any(e => e is A11yEvent.BasesCellSaved or A11yEvent.BasesCellRowNoLongerMatches or A11yEvent.BasesCellEditFailed),
                "the cell edit's outcome at its turn");
            Assert.True(held.Result);
            string atThePath = h.Read("a.md");
            Assert.Contains("status: done", atThePath, StringComparison.Ordinal);
            Assert.Contains("Another note at the path.", atThePath, StringComparison.Ordinal);
            Assert.Equal(Note, h.Read("b.md"));
            _ = Assert.Single(h.Events, e => e is A11yEvent.BasesCellSaved);
            Assert.DoesNotContain(h.Events, e => e is A11yEvent.BasesCellRowNoLongerMatches or A11yEvent.BasesCellEditFailed
                or A11yEvent.PropertyEditConflict);
        }
        finally
        {
            _ = ahead.TrySetResult();
            h.ReleaseAndDrain();
        }
    });

    /// <summary>The route is decided again at the write's turn. A tab that
    /// opened on the note while the write waited takes it: a clean tab
    /// writes on its saved hash and is rebaselined, and a dirty tab refuses
    /// with the dirty-note sentence. Either way the tabless write does not
    /// run too.</summary>
    [Theory]
    [InlineData("clean")]
    [InlineData("dirty")]
    public void ATablessBasesCellEditWhoseNoteOpenedWhileItWaitedTakesTheOpenNoteRoute(string tab) => RunSta(() =>
    {
        using var h = new Vault("tabless-cell-tab-opened-" + tab, ("a.md", Note), ("Notes.base", NotesBase));
        (BaseDocumentViewModel document, BasesRow row, BasesColumn column) = TablessStatusCell(h);
        int tablessWrites = 0;
        int tabWrites = 0;
        h.Workspace.BasesTablessWriteHookForTests = () => Interlocked.Increment(ref tablessWrites);
        var ahead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool> held = h.Workspace.SavesForTests.Admit("a.md", _ => ahead.Task);
        try
        {
            document.ApplyPropertyEdit!(row, column, new PropertyValue.Text("done"));
            Assert.Equal(2, h.Workspace.SavesForTests.AdmittedForTests);

            WorkspaceTabViewModel opened = h.OpenNow("a.md");
            opened.PropertyWriteHookForTests = () => Interlocked.Increment(ref tabWrites);
            if (tab == "dirty")
            {
                opened.Text = Note + "An unsaved line.\n";
            }
            h.Events.Clear();

            ahead.SetResult();
            h.PumpUntil(
                () => h.Workspace.SavesIdle
                    && !h.Workspace.PropertyWriteInFlightFor("a.md")
                    && h.Events.Any(e => tab == "dirty" ? e is A11yEvent.HostComposed : e is A11yEvent.BasesCellSaved),
                "the write's turn on the tab that opened");
            Assert.True(held.Result);
            Assert.Equal(0, Volatile.Read(ref tablessWrites));
            if (tab == "dirty")
            {
                Assert.Equal(0, Volatile.Read(ref tabWrites));
                Assert.DoesNotContain("status: done", h.Read("a.md"), StringComparison.Ordinal);
                _ = Assert.Single(
                    h.Events.OfType<A11yEvent.HostComposed>(),
                    line => line.Text == "Save the note before editing properties. The editor has unsaved changes in a.md.");
                Assert.DoesNotContain(h.Events, e => e is A11yEvent.BasesCellSaved);
            }
            else
            {
                Assert.Equal(1, Volatile.Read(ref tabWrites));
                string written = h.Read("a.md");
                Assert.Contains("status: done", written, StringComparison.Ordinal);
                Assert.Equal(written, opened.Text);
                Assert.False(opened.IsExternallyStale);
                Assert.False(opened.IsDirty);
                _ = Assert.Single(h.Events, e => e is A11yEvent.BasesCellSaved);
                Assert.DoesNotContain(h.Events, e => e is A11yEvent.BasesCellEditFailed or A11yEvent.PropertyEditConflict);
            }
        }
        finally
        {
            _ = ahead.TrySetResult();
            h.Workspace.BasesTablessWriteHookForTests = null;
            h.ReleaseAndDrain();
        }
    });

    /// <summary>The other direction: the tabless write is held before its
    /// core call when a tab opens on the note and saves. The save waits for
    /// the write's publication, which rebaselines the tab that raced open,
    /// so the save then finds a clean tab on the written bytes.</summary>
    [Fact]
    public void ASaveStartedUnderATablessBasesCellEditWaitsForItsPublication() => RunSta(() =>
    {
        using var h = new Vault("save-behind-tabless-cell", ("a.md", Note), ("Notes.base", NotesBase));
        (BaseDocumentViewModel document, BasesRow row, BasesColumn column) = TablessStatusCell(h);
        int writes = 0;
        var held = new HeldWork(hook => h.Workspace.BasesTablessWriteHookForTests = hook is null
            ? null
            : () =>
            {
                _ = Interlocked.Increment(ref writes);
                hook();
            });
        try
        {
            document.ApplyPropertyEdit!(row, column, new PropertyValue.Text("done"));
            h.PumpUntil(() => held.Entered, "premise: the cell write reaching its hold before its core call");

            WorkspaceTabViewModel opened = h.OpenNow("a.md");
            Assert.Equal(Note, opened.Text);
            string? textWhenTheSavePublished = null;
            Task<bool> save = opened.SaveAsync(onSaved: () => textWhenTheSavePublished = opened.Text, announce: true);
            Assert.Equal(2, h.Workspace.SavesForTests.AdmittedForTests);
            PumpedDispatcher.Drain();
            Assert.False(save.IsCompleted, "the save did not wait for the cell write");
            h.Events.Clear();

            held.Release();
            h.PumpUntil(
                () => save.IsCompleted && h.Events.Any(e => e is A11yEvent.BasesCellSaved) && h.Workspace.SavesIdle,
                "the cell write and then the save");
            Assert.True(save.Result);
            Assert.Equal(1, Volatile.Read(ref writes));
            string written = h.Read("a.md");
            Assert.Contains("status: done", written, StringComparison.Ordinal);
            Assert.Equal(written, textWhenTheSavePublished);
            Assert.Equal(written, opened.Text);
            Assert.False(opened.IsExternallyStale);
            Assert.False(opened.IsDirty);
            _ = Assert.Single(h.Events, e => e is A11yEvent.NoteSaved);
            _ = Assert.Single(h.Events, e => e is A11yEvent.BasesCellSaved);
            Assert.DoesNotContain(h.Events, e => e is A11yEvent.NoteSaveConflict or A11yEvent.PropertyEditConflict);
            Assert.False(h.Workspace.PropertyWriteInFlightFor("a.md"));
        }
        finally
        {
            h.ReleaseAndDrain(held);
        }
        held.AssertHeldUntilReleased();
    });

    /// <summary>Found and fixed with the tabless write: a property write
    /// rewrites the note and re-indexes it, tasks included, so the shared
    /// task-index repair lease brackets the session write, as it does for a
    /// tab's property write. While the write is in flight no task query
    /// gets a clean ticket, and a write that fails leaves the note waiting
    /// for a repair, since its index may no longer match the file.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ATablessBasesCellWriteHoldsTheTaskIndexRepairLeaseAcrossItsWrite(bool fails) => RunSta(() =>
    {
        using var h = new Vault("tabless-cell-repair-lease", ("a.md", Note), ("Notes.base", NotesBase));
        (BaseDocumentViewModel document, BasesRow row, BasesColumn column) = TablessStatusCell(h);
        TaskIndexRepairCoordinator repairs = h.Workspace.TaskIndexRepairsForTests;
        Assert.True(repairs.TryBeginCleanQuery(out _, out _), "premise: a write was already in flight");
        var held = new HeldWork(hook => h.Workspace.BasesTablessWriteHookForTests = hook is null
            ? null
            : () =>
            {
                hook();
                if (fails)
                {
                    throw new InvalidOperationException("the cell write failed (test seam)");
                }
            });
        try
        {
            document.ApplyPropertyEdit!(row, column, new PropertyValue.Text("done"));
            h.PumpUntil(() => held.Entered, "premise: the cell write reaching its hold before its core call");
            Assert.False(
                repairs.TryBeginCleanQuery(out _, out bool onlyByAWriteInFlight),
                "a task query could read the index while the cell write was in flight");
            Assert.True(onlyByAWriteInFlight);

            held.Release();
            h.PumpUntil(
                () => h.Workspace.SavesIdle && !h.Workspace.PropertyWriteInFlightFor("a.md"),
                "the cell write's end");
            Assert.Equal(fails, repairs.HasPendingFor("a.md"));
            Assert.Equal(!fails, repairs.TryBeginCleanQuery(out _, out bool stillInFlight));
            Assert.False(stillInFlight, "the lease outlived the write");
        }
        finally
        {
            h.ReleaseAndDrain(held);
        }
        held.AssertHeldUntilReleased();
    });

    /// <summary>Found and fixed with the tabless write (the red team's
    /// F1): a cell write that goes through a tab runs on the tab's
    /// property-write worker, whose catch leaves out of memory, stack
    /// overflow and access violation alone. Such a fault posts no
    /// completion, so without a backstop the operation never ends, the
    /// note's chain stays held and its property lease is never released.
    /// Both ways into that worker: a tab that opened while a tabless write
    /// waited, and a cell edit asked with the tab already open.</summary>
    [Theory]
    [InlineData("the tab opened while the write waited")]
    [InlineData("the tab was open at the request")]
    public void ABasesCellWriteThroughATabWhoseWorkerFaultsOutsideItsCatchStillEnds(string route) => RunSta(() =>
    {
        using var h = new Vault("cell-tab-worker-faults", ("a.md", Note), ("Notes.base", NotesBase));
        bool openAtTheRequest = route == "the tab was open at the request";
        WorkspaceTabViewModel? opened = openAtTheRequest ? h.OpenNow("a.md") : null;
        (BaseDocumentViewModel document, BasesRow row, BasesColumn column) = StatusCellOf(h);
        var ahead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool> held = h.Workspace.SavesForTests.Admit("a.md", _ => ahead.Task);
        try
        {
            document.ApplyPropertyEdit!(row, column, new PropertyValue.Text("done"));
            Assert.Equal(2, h.Workspace.SavesForTests.AdmittedForTests);
            opened ??= h.OpenNow("a.md");
            opened.PropertyWriteHookForTests = () =>
                throw new OutOfMemoryException("a fault the worker's catch does not take (test seam)");
            h.Events.Clear();

            ahead.SetResult();
            h.PumpUntil(
                () => h.Workspace.SavesIdle && !h.Workspace.PropertyWriteInFlightFor("a.md"),
                "the cell write ending after its worker faulted");
            Assert.True(held.Result);
            _ = Assert.Single(h.Events, e => e is A11yEvent.BasesCellEditFailed);
            Assert.DoesNotContain(h.Events, e => e is A11yEvent.BasesCellSaved);
            Assert.DoesNotContain("status: done", h.Read("a.md"), StringComparison.Ordinal);

            bool ran = false;
            Task<bool> later = h.Workspace.SavesForTests.Admit("a.md", _ =>
            {
                ran = true;
                return Task.CompletedTask;
            });
            h.PumpUntil(() => later.IsCompleted, "an operation admitted after the faulted write");
            Assert.True(ran && later.Result, "the note's chain was still held");
        }
        finally
        {
            _ = ahead.TrySetResult();
            if (opened is not null)
            {
                opened.PropertyWriteHookForTests = null;
            }
            h.ReleaseAndDrain();
        }
    });

    /// <summary>The base's row for a.md and its status column, with no tab
    /// on the note.</summary>
    private static (BaseDocumentViewModel Document, BasesRow Row, BasesColumn Column) TablessStatusCell(Vault h)
    {
        Assert.DoesNotContain(h.Workspace.Groups.SelectMany(group => group.Tabs), tab => tab.Path == "a.md");
        return StatusCellOf(h);
    }

    /// <summary>The base's row for a.md and its status column.</summary>
    private static (BaseDocumentViewModel Document, BasesRow Row, BasesColumn Column) StatusCellOf(Vault h)
    {
        BaseDocumentViewModel document = h.Workspace.BaseDocumentFor("Notes.base");
        h.PumpUntil(
            () => document.Result?.Rows.Any(row => row.FilePath == "a.md") == true,
            "the base's rows");
        BasesRow row = document.Result!.Rows.Single(candidate => candidate.FilePath == "a.md");
        BasesColumn column = document.Result.Columns.Single(candidate => candidate.Id == "note.status");
        h.Events.Clear();
        return (document, row, column);
    }

    // ---- A Tasks Review toggle on a note with no tab (plan 47 CB-9a, #1889) ---

    /// <summary>A review toggle on a note with no tab takes a ticket in the
    /// note's chain: the box is not ticked until the operation admitted
    /// before it ends, and then it is ticked once.</summary>
    [Fact]
    public void ATablessReviewToggleWaitsForAnOperationAdmittedForTheNoteAndTogglesOnce() => RunSta(() =>
    {
        using var h = new Vault("tabless-toggle-behind-op", ("a.md", TaskNote));
        (TasksReviewViewModel review, ReviewTaskRowViewModel row) = ReviewRow(h);
        var ahead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool> held = h.Workspace.SavesForTests.Admit("a.md", _ => ahead.Task);
        try
        {
            review.ToggleTask(row);
            Assert.Equal(2, h.Workspace.SavesForTests.AdmittedForTests);
            PumpedDispatcher.Drain();
            Assert.Equal(TaskNote, h.Read("a.md"));
            Assert.DoesNotContain(h.Events, e => e is A11yEvent.TaskToggled);

            ahead.SetResult();
            h.PumpUntil(
                () => h.Events.Any(e => e is A11yEvent.TaskToggled) && h.Workspace.SavesIdle,
                "the toggle after the operation ahead of it");
            Assert.True(held.Result);
            Assert.Contains("- [x] the task", h.Read("a.md"), StringComparison.Ordinal);
            _ = Assert.Single(h.Events, e => e is A11yEvent.TaskToggled);
            Assert.DoesNotContain(h.Events, e => e is A11yEvent.TaskToggleConflict or A11yEvent.TaskToggleFailed);
        }
        finally
        {
            _ = ahead.TrySetResult();
            ReleaseAndDrainTheReview(h, review);
        }
    });

    /// <summary>The row's task is an ordinal in the text the row was read
    /// from. The operation ahead rewrites the note, so when the toggle's turn
    /// comes core's compare-and-swap on the row's snapshot hash refuses it:
    /// the conflict is spoken once, the operation's bytes are kept, and the
    /// rows reload.</summary>
    [Fact]
    public void ATablessReviewToggleWhoseNoteChangedWhileItWaitedRefusesAsStale() => RunSta(() =>
    {
        using var h = new Vault("tabless-toggle-note-changed", ("a.md", TaskNote));
        (TasksReviewViewModel review, ReviewTaskRowViewModel row) = ReviewRow(h);
        var ahead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool> held = h.Workspace.SavesForTests.Admit("a.md", _ => ahead.Task);
        try
        {
            review.ToggleTask(row);
            Assert.Equal(2, h.Workspace.SavesForTests.AdmittedForTests);
            int loads = review.LoadRequestIdForTests;

            // The operation ahead writes the note, then ends.
            _ = h.Session.SaveText("a.md", TaskNoteEdited, expectedContentHash: null);
            ahead.SetResult();
            h.PumpUntil(
                () => h.Events.Any(e => e is A11yEvent.TaskToggleConflict) && h.Workspace.SavesIdle,
                "the toggle's refusal after the operation ahead of it");
            Assert.True(held.Result);
            Assert.Equal(TaskNoteEdited, h.Read("a.md"));
            _ = Assert.Single(h.Events, e => e is A11yEvent.TaskToggleConflict);
            Assert.DoesNotContain(h.Events, e => e is A11yEvent.TaskToggled);
            Assert.True(review.LoadRequestIdForTests > loads, "the rows did not reload after the refusal");
        }
        finally
        {
            _ = ahead.TrySetResult();
            ReleaseAndDrainTheReview(h, review);
        }
    });

    /// <summary>The route is decided again at the toggle's turn. A tab that
    /// opened on the note while it waited takes the toggle through its own
    /// admitted route, or refuses with that route's sentence when it is
    /// dirty. Either way the direct toggle does not run too.</summary>
    [Theory]
    [InlineData("clean")]
    [InlineData("dirty")]
    public void ATablessReviewToggleWhoseNoteOpenedWhileItWaitedTakesTheOpenNoteRoute(string tab) => RunSta(() =>
    {
        using var h = new Vault("tabless-toggle-tab-opened-" + tab, ("a.md", TaskNote));
        (TasksReviewViewModel review, ReviewTaskRowViewModel row) = ReviewRow(h);
        int direct = 0;
        review.DirectToggleBeforeWriteForTests = () => Interlocked.Increment(ref direct);
        var ahead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool> held = h.Workspace.SavesForTests.Admit("a.md", _ => ahead.Task);
        try
        {
            review.ToggleTask(row);
            Assert.Equal(2, h.Workspace.SavesForTests.AdmittedForTests);

            WorkspaceTabViewModel opened = h.OpenNow("a.md");
            if (tab == "dirty")
            {
                opened.Text = TaskNote + "An unsaved line.\n";
            }
            h.Events.Clear();

            ahead.SetResult();
            h.PumpUntil(
                () => h.Workspace.SavesIdle
                    && !opened.IsTaskToggleInFlight
                    && h.Events.Any(e => tab == "dirty" ? e is A11yEvent.TaskToggleUnsaved : e is A11yEvent.TaskToggled),
                "the toggle's turn on the tab that opened");
            Assert.True(held.Result);
            Assert.Equal(0, Volatile.Read(ref direct));
            if (tab == "dirty")
            {
                Assert.Equal(TaskNote, h.Read("a.md"));
                _ = Assert.Single(h.Events, e => e is A11yEvent.TaskToggleUnsaved);
                Assert.DoesNotContain(h.Events, e => e is A11yEvent.TaskToggled);
            }
            else
            {
                string written = h.Read("a.md");
                Assert.Contains("- [x] the task", written, StringComparison.Ordinal);
                Assert.Equal(written, opened.Text);
                Assert.False(opened.IsExternallyStale);
                Assert.False(opened.IsDirty);
                _ = Assert.Single(h.Events, e => e is A11yEvent.TaskToggled);
                Assert.DoesNotContain(h.Events, e => e is A11yEvent.TaskToggleConflict or A11yEvent.TaskToggleFailed);
            }
        }
        finally
        {
            _ = ahead.TrySetResult();
            review.DirectToggleBeforeWriteForTests = null;
            ReleaseAndDrainTheReview(h, review);
        }
    });

    /// <summary>The other direction: the direct toggle is held before its
    /// core call when a tab opens on the note and saves. The save waits for
    /// the toggle's publication. The tab holds the bytes from before the
    /// toggle, so that publication marks it stale with the sentence it gets
    /// today, and the one write is the toggle's.</summary>
    [Fact]
    public void ASaveStartedUnderATablessReviewToggleWaitsForItsPublication() => RunSta(() =>
    {
        using var h = new Vault("save-behind-tabless-toggle", ("a.md", TaskNote));
        (TasksReviewViewModel review, ReviewTaskRowViewModel row) = ReviewRow(h);
        var held = new HeldWork(hook => review.DirectToggleBeforeWriteForTests = hook);
        try
        {
            review.ToggleTask(row);
            h.PumpUntil(() => held.Entered, "premise: the toggle reaching its hold before its core call");

            WorkspaceTabViewModel opened = h.OpenNow("a.md");
            Assert.Equal(TaskNote, opened.Text);
            Task<bool> save = opened.SaveAsync(announce: true);
            Assert.Equal(2, h.Workspace.SavesForTests.AdmittedForTests);
            PumpedDispatcher.Drain();
            Assert.False(save.IsCompleted, "the save did not wait for the toggle");
            h.Events.Clear();

            held.Release();
            h.PumpUntil(
                () => save.IsCompleted && h.Events.Any(e => e is A11yEvent.TaskToggled) && h.Workspace.SavesIdle,
                "the toggle and then the save");
            Assert.True(save.Result);
            _ = Assert.Single(h.Events, e => e is A11yEvent.TaskToggled);
            _ = Assert.Single(h.Events, e => e is A11yEvent.NoteSaved);
            Assert.True(
                h.Events.FindIndex(e => e is A11yEvent.TaskToggled) < h.Events.FindIndex(e => e is A11yEvent.NoteSaved),
                "the save spoke before the toggle it waited for");
            Assert.True(opened.IsExternallyStale, "the tab that raced open was not told the disk moved");
            _ = Assert.Single(
                h.Events.OfType<A11yEvent.HostComposed>(),
                line => line.Text == "Task toggled on disk, but the editor no longer matches it. Reopen the note before editing.");
            Assert.Equal(TaskNote, opened.Text);
            Assert.Contains("- [x] the task", h.Read("a.md"), StringComparison.Ordinal);
        }
        finally
        {
            h.ReleaseAndDrain(held);
            ReleaseAndDrainTheReview(h, review);
        }
        held.AssertHeldUntilReleased();
    });

    /// <summary>A pin of the present behaviour, which is the spec's Design 6
    /// and not a wanted order. A review toggle on a note with no tab waits
    /// its turn. Meanwhile the note is opened in a tab, and a Bases cell
    /// edit on it is asked, which queues behind the toggle. At its turn the
    /// toggle finds the tab and hands itself to the tab's own admitted
    /// route, which takes a new ticket behind the cell edit. So the later
    /// request writes first, the tab's saved bytes move, and the toggle is
    /// refused as stale at its own turn: the conflict is spoken once, the
    /// task is not ticked, the rows reload and the chain is freed. Nothing
    /// is lost in silence. A fix would run the tab's toggle inside the
    /// review's ticket, so the earlier request kept its place.</summary>
    [Fact]
    public void ATablessReviewToggleReroutedThroughATabIsOvertakenByAWriteQueuedMeanwhileAndRefusesAloud() => RunSta(() =>
    {
        using var h = new Vault("toggle-rerouted-overtaken", ("a.md", TaskNote), ("Notes.base", NotesBase));
        (TasksReviewViewModel review, ReviewTaskRowViewModel task) = ReviewRow(h);
        int direct = 0;
        review.DirectToggleBeforeWriteForTests = () => Interlocked.Increment(ref direct);
        var ahead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool> held = h.Workspace.SavesForTests.Admit("a.md", _ => ahead.Task);
        string? onDiskAtTheRefusal = null;
        int loadsAtTheRefusal = -1;
        h.BeforeAnnounce = spoken =>
        {
            if (spoken is A11yEvent.TaskToggleConflict)
            {
                onDiskAtTheRefusal = h.Read("a.md");
                loadsAtTheRefusal = review.LoadRequestIdForTests;
            }
        };
        try
        {
            review.ToggleTask(task);
            Assert.Equal(2, h.Workspace.SavesForTests.AdmittedForTests);

            WorkspaceTabViewModel opened = h.OpenNow("a.md");
            (BaseDocumentViewModel document, BasesRow row, BasesColumn column) = StatusCellOf(h);
            document.ApplyPropertyEdit!(row, column, new PropertyValue.Text("done"));
            Assert.Equal(3, h.Workspace.SavesForTests.AdmittedForTests);
            int loads = review.LoadRequestIdForTests;
            h.Events.Clear();

            ahead.SetResult();
            h.PumpUntil(
                () => h.Workspace.SavesIdle
                    && !opened.IsTaskToggleInFlight
                    && !h.Workspace.PropertyWriteInFlightFor("a.md")
                    && h.Events.Any(e => e is A11yEvent.BasesCellSaved)
                    && h.Events.Any(e => e is A11yEvent.TaskToggleConflict or A11yEvent.TaskToggled or A11yEvent.TaskToggleFailed),
                "the cell edit and then the toggle's outcome");
            Assert.True(held.Result);

            // The later request wrote first, and whole.
            string written = h.Read("a.md");
            Assert.Contains("status: done", written, StringComparison.Ordinal);
            Assert.NotNull(onDiskAtTheRefusal);
            Assert.Contains("status: done", onDiskAtTheRefusal, StringComparison.Ordinal);
            _ = Assert.Single(h.Events, e => e is A11yEvent.BasesCellSaved);
            Assert.DoesNotContain(h.Events, e => e is A11yEvent.BasesCellEditFailed or A11yEvent.PropertyEditConflict);

            // The earlier request, the toggle, wrote nothing and said so.
            Assert.Contains("- [ ] the task", written, StringComparison.Ordinal);
            Assert.DoesNotContain("- [x]", written, StringComparison.Ordinal);
            Assert.Equal(written, onDiskAtTheRefusal);
            Assert.Equal(0, Volatile.Read(ref direct));
            _ = Assert.Single(h.Events, e => e is A11yEvent.TaskToggleConflict);
            Assert.DoesNotContain(h.Events, e => e is A11yEvent.TaskToggled or A11yEvent.TaskToggleFailed);
            // The rows' reload is asked when the cell write refreshes the tab,
            // with the re-routed toggle's refresh armed: before the refusal,
            // which only disarms it.
            Assert.True(loadsAtTheRefusal > loads, "the rows' reload was not asked by the time the toggle was refused");

            // The tab shows the written note, and the chain is free.
            Assert.Equal(written, opened.Text);
            Assert.False(opened.IsExternallyStale);
            Assert.False(opened.IsDirty);
            AssertALaterOperationRuns(h, "a.md");
        }
        finally
        {
            h.BeforeAnnounce = null;
            _ = ahead.TrySetResult();
            review.DirectToggleBeforeWriteForTests = null;
            ReleaseAndDrainTheReview(h, review);
        }
    });

    /// <summary>The review's row for a.md's task, loaded, with no tab on the
    /// note.</summary>
    private static (TasksReviewViewModel Review, ReviewTaskRowViewModel Row) ReviewRow(Vault h)
    {
        TasksReviewViewModel review = h.Workspace.TasksReview;
        review.EnsureLoaded();
        h.PumpUntil(() => !review.IsLoading && review.Rows.Any(row => row.Path == "a.md"), "the review's rows");
        ReviewTaskRowViewModel row = review.Rows.Single(candidate => candidate.Path == "a.md");
        Assert.DoesNotContain(h.Workspace.Groups.SelectMany(group => group.Tabs), tab => tab.Path == "a.md");
        h.Events.Clear();
        return (review, row);
    }

    /// <summary>For a fact's <c>finally</c>: the admitted work ends, and then
    /// the review's own workers (a reload a toggle's publication started)
    /// end, before the vault is disposed. It asserts nothing.</summary>
    private static void ReleaseAndDrainTheReview(Vault h, TasksReviewViewModel review)
    {
        h.ReleaseAndDrain();
        Task drained = review.WhenAllWorkDrained();
        _ = PumpedDispatcher.PumpUntil(() => drained.IsCompleted, TimeSpan.FromSeconds(30));
    }

    /// <summary>The deleted note's row, loaded, and the alerts the workspace
    /// raises from here on.</summary>
    private static (HistoryDeletedRow Row, ConcurrentQueue<string> Alerts) RecoverSetup(Vault h)
    {
        var alerts = new ConcurrentQueue<string>();
        h.Workspace.HistoryAlert = (title, message) => alerts.Enqueue($"{title}: {message}");
        h.Workspace.History.LoadDeletedFiles();
        h.PumpUntil(
            () => h.Workspace.History.DeletedRows.Any(row => row.Path == "gone.md"),
            "the deleted files list");
        HistoryDeletedRow row = h.Workspace.History.DeletedRows.Single(candidate => candidate.Path == "gone.md");
        Assert.True(row.Recoverable, "premise: the deleted note is not recoverable");
        h.Events.Clear();
        return (row, alerts);
    }

    // ---- The exclusive creates (plan 47 CB-9a, #1889; CD5(c)) -----------------

    /// <summary>The create class stays outside the save chain, and this is
    /// why it may: an exclusive create at a path the reader or the namer
    /// chose is its own check. Core refuses an occupied path, and a tab that
    /// names the path and holds no hash saves by create too, so whichever of
    /// the two lands second is refused and neither overwrites the other. One
    /// row for each creator the shell calls outside the chain (the census
    /// holds this list to them), in both orders.</summary>
    [Theory]
    [InlineData("CreateExclusiveReporting", false)]
    [InlineData("CreateExclusiveReporting", true)]
    [InlineData("RecoverDeletedFileAs", false)]
    [InlineData("RecoverDeletedFileAs", true)]
    [InlineData("CreateExclusiveBytes", false)]
    [InlineData("CreateExclusiveBytes", true)]
    public void AnExclusiveCreateAndAGhostTabsSaveNeverOverwriteEachOther(string creator, bool saveFirst) => RunSta(() =>
    {
        const string Copy = "copy.md";
        const string Imported = "imported bytes\n";
        using Vault h = creator == "RecoverDeletedFileAs"
            ? Vault.WithADeletedNote("create-class-deleted", "gone.md", Gone, background: false)
            : Vault.Inline("create-class", ("a.md", "v0\n"));
        // Restore As a version creates through CreateExclusiveReporting,
        // Restore As a deleted file through RecoverDeletedFileAs, and an
        // import through CreateExclusiveBytes, the session call made here.
        HistoryVersionRow? version = creator == "CreateExclusiveReporting" ? AnOlderVersion(h) : null;
        string created = creator switch
        {
            "CreateExclusiveReporting" => V1,
            "RecoverDeletedFileAs" => Gone,
            _ => Imported,
        };
        var answers = new List<(bool Ok, string? Refusal)>();
        void Create()
        {
            switch (creator)
            {
                case "CreateExclusiveReporting":
                    h.Workspace.CommitRestoreAsVersion(
                        version!.ContentHashAfter, version.AbsoluteDate, Copy, (ok, refusal) => answers.Add((ok, refusal)));
                    break;
                case "RecoverDeletedFileAs":
                    h.Workspace.CommitRestoreAsDeleted("gone.md", Copy, (ok, refusal) => answers.Add((ok, refusal)));
                    break;
                default:
                    try
                    {
                        _ = h.Session.CreateExclusiveBytes(Copy, System.Text.Encoding.UTF8.GetBytes(Imported));
                        answers.Add((true, null));
                    }
                    catch (VaultException.DestinationExists)
                    {
                        answers.Add((false, "A file already exists at copy.md."));
                    }
                    break;
            }
        }

        WorkspaceTabViewModel ghost = h.OpenNow(Copy);
        Assert.Null(ghost.SavedContentHash);
        ghost.Text = Edit;
        if (version is not null)
        {
            // The History panel followed the ghost tab when it opened; the
            // version being restored is a.md's.
            h.Workspace.History.NoteChanged("a.md");
            Assert.Equal("a.md", h.Workspace.History.Path);
        }
        if (saveFirst)
        {
            Task<bool> first = ghost.SaveAsync(announce: true);
            h.PumpUntil(() => first.IsCompleted && h.Workspace.SavesIdle, "the ghost tab's save");
            Assert.True(first.Result);

            Create();
            (bool ok, string? refusal) = Assert.Single(answers);
            Assert.False(ok, "the create overwrote the file the tab's save created");
            Assert.StartsWith("A file already exists at", refusal, StringComparison.Ordinal);
            Assert.Equal(Edit, h.Read(Copy));
            return;
        }

        var held = new HeldWork(hook => ghost.SaveWriteHookForTests = hook);
        try
        {
            Task<bool> save = ghost.SaveAsync(announce: true);
            h.PumpUntil(() => held.Entered, "premise: the ghost tab's save reaching its hold before its write");
            h.Events.Clear();

            Create();
            (bool ok, string? refusal) = Assert.Single(answers);
            Assert.True(ok, $"premise: the create was refused: {refusal}");
            Assert.Equal(created, h.Read(Copy));

            held.Release();
            h.PumpUntil(() => save.IsCompleted && h.Workspace.SavesIdle, "the save after the create");
            Assert.False(save.Result, "the ghost tab's save overwrote the created file");
            _ = Assert.Single(h.Events, e => e is A11yEvent.NoteSaveBlocked);
            Assert.Equal(created, h.Read(Copy));
        }
        finally
        {
            h.ReleaseAndDrain(held);
            ghost.SaveWriteHookForTests = null;
        }
        held.AssertHeldUntilReleased();
    });

    /// <summary>a.md with two saved versions, v1 then v2 on disk, its tab
    /// clean and the History panel listing it; v1's row. As
    /// <see cref="RestoreSetup"/>, waiting on conditions only.</summary>
    private static HistoryVersionRow AnOlderVersion(Vault h)
    {
        WorkspaceTabViewModel tab = h.OpenNow("a.md");
        tab.Text = V1;
        SaveNow(h, tab);
        string v1 = tab.SavedContentHash!;
        tab.Text = V2;
        SaveNow(h, tab);
        h.Workspace.History.NoteChanged("a.md");
        h.PumpUntil(() => Version(h, v1) is not null, "the history rows loading");
        return Version(h, v1)!;
    }

    // ---- Faults, teardown and the settle for the three routes (CB-9a) ---------
    //
    // WorkspaceSaveCoordinator.SettlePumping has no bound, so one ticket that
    // never ends hangs Close Vault and every later save of the note. Each
    // fact here drives one way an admitted route could have left its ticket
    // open, and shows the note's chain idle afterwards.

    /// <summary>A fault outside VaultException on the route's worker (a core
    /// panic) is the route's failed outcome, given once, and the operation
    /// ends: the chain goes idle and a later operation on the path runs.</summary>
    [Theory]
    [InlineData("recover")]
    [InlineData("cell write")]
    [InlineData("toggle before its write")]
    [InlineData("toggle after its write")]
    public void AnAdmittedSinglePathRouteWhoseWorkerFaultsEndsAndFreesTheNotesChain(string route) => RunSta(() =>
    {
        const string Fault = "the route's core call panicked (test seam)";
        using var r = new SinglePathRoute(route, "route-faults");
        Action thrower = () => throw new InvalidOperationException(Fault);
        if (route == "toggle after its write")
        {
            r.H.Workspace.TasksReview.DirectToggleFaultForTests = thrower;
        }
        else
        {
            r.SetSeam(thrower);
        }
        try
        {
            int before = r.H.Workspace.SavesForTests.AdmittedForTests;
            r.Ask();
            Assert.Equal(before + 1, r.H.Workspace.SavesForTests.AdmittedForTests);

            r.H.PumpUntil(() => r.FailureGiven && r.H.Workspace.SavesIdle, "the faulted route ending");
            r.AssertFailedOnce(Fault);
            if (route.StartsWith("toggle", StringComparison.Ordinal))
            {
                // The seam stands inside the body's try, after the repair
                // lease is taken, so the body's own arms settled the lease:
                // none is left held, whatever repair is pending.
                _ = r.H.Workspace.TaskIndexRepairsForTests.TryBeginCleanQuery(out _, out bool onlyALeaseBars);
                Assert.False(onlyALeaseBars, "the toggle's repair lease was left held");
            }
            r.AssertALaterOperationRuns();
        }
        finally
        {
            r.H.Workspace.TasksReview.DirectToggleFaultForTests = null;
            r.SetSeam(null);
            r.Drain();
        }
    });

    /// <summary>The same when the worker faults outside its own catch (the
    /// catch leaves out of memory, stack overflow and access violation
    /// alone), so no publication was posted: the route still fails aloud,
    /// once, writes nothing, and its operation ends.</summary>
    [Theory]
    [InlineData("cell write")]
    [InlineData("toggle")]
    public void AnAdmittedSinglePathRouteWhoseWorkerFaultsOutsideItsCatchStillEnds(string route) => RunSta(() =>
    {
        const string Fault = "a fault the worker's catch does not take (test seam)";
        using var r = new SinglePathRoute(route, "route-faults-outside-catch");
        r.SetSeam(() => throw new OutOfMemoryException(Fault));
        try
        {
            r.Ask();
            Assert.Equal(1, r.H.Workspace.SavesForTests.AdmittedForTests);

            r.H.PumpUntil(() => r.FailureGiven && r.H.Workspace.SavesIdle, "the route ending after its worker faulted");
            r.AssertFailedOnce(Fault);
            Assert.False(r.Written);
            r.AssertALaterOperationRuns();
        }
        finally
        {
            r.SetSeam(null);
            r.Drain();
        }
    });

    /// <summary>The recover's backstop. Its worker faults outside its catch
    /// (out of memory, which the catch leaves alone), so its own publication
    /// was never posted. The row is still answered, once, with "nothing
    /// happened"; the recover fails aloud with the existing alert; nothing is
    /// created; and the path's chain is free. The second row makes that
    /// alert throw: the row is answered all the same, and the operation
    /// still ends.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ARecoverWhoseWorkerFaultsOutsideItsCatchStillEndsAndAnswersItsRow(bool theAlertThrows) => RunSta(() =>
    {
        const string Fault = "a fault the worker's catch does not take (test seam)";
        using var r = new SinglePathRoute("recover", "recover-faults-outside-catch");
        if (theAlertThrows)
        {
            r.H.Workspace.HistoryAlert = (_, _) =>
                throw new System.Runtime.InteropServices.COMException("the alert failed (test seam)");
        }
        r.SetSeam(() => throw new OutOfMemoryException(Fault));
        try
        {
            r.Ask();
            Assert.Equal(1, r.H.Workspace.SavesForTests.AdmittedForTests);

            r.H.PumpUntil(() => !r.Answers.IsEmpty && r.H.Workspace.SavesIdle, "the recover ending after its worker faulted");
            PumpedDispatcher.Drain();
            Assert.Equal([(false, false)], r.Answers);
            if (!theAlertThrows)
            {
                Assert.Equal(["Can't restore: " + Fault], r.Alerts);
            }
            Assert.False(r.Written);
            Assert.DoesNotContain(r.H.Events, e => e is A11yEvent.RestoredFile);
            r.AssertALaterOperationRuns();
        }
        finally
        {
            r.SetSeam(null);
            r.Drain();
        }
    });

    /// <summary>The red team's F2. In the shell the "Can't restore" alert is
    /// a modal box. A close that arrives inside its loop settles the saves
    /// from a frame nested above the box, and that settle returns only when
    /// every ticket has ended. So a failed recover ends its operation
    /// first, then shows its alert, then answers its row: nothing was
    /// written, and a save queued behind may start once the failure is
    /// known. The alert here stands for that close: it pumps until the
    /// saves are idle, with a bound, and then settles them.</summary>
    [Theory]
    [InlineData("its worker's catch takes the fault")]
    [InlineData("its worker faults outside its catch")]
    [InlineData("no history is left")]
    public void ARecoverThatFailsEndsItsOperationBeforeItsAlert(string how) => RunSta(() =>
    {
        using var r = new SinglePathRoute("recover", "recover-ends-before-alert");
        int alerts = 0;
        bool idleInsideTheAlert = false;
        bool settledInsideTheAlert = false;
        r.H.Workspace.HistoryAlert = (_, _) =>
        {
            alerts++;
            idleInsideTheAlert = PumpedDispatcher.PumpUntil(() => r.H.Workspace.SavesIdle, TimeSpan.FromSeconds(10));
            settledInsideTheAlert = idleInsideTheAlert && r.H.Workspace.SettleSaves();
        };
        if (how == "its worker's catch takes the fault")
        {
            r.SetSeam(() => throw new InvalidOperationException("the create's core call panicked (test seam)"));
        }
        else if (how == "its worker faults outside its catch")
        {
            r.SetSeam(() => throw new OutOfMemoryException("a fault the worker's catch does not take (test seam)"));
        }
        int asked = how == "no history is left" ? 2 : 1;
        try
        {
            for (int ask = 0; ask < asked; ask++)
            {
                r.Ask();
            }
            Assert.Equal(asked, r.H.Workspace.SavesForTests.AdmittedForTests);

            r.H.PumpUntil(
                () => alerts == 1 && r.Answers.Count == asked && r.H.Workspace.SavesIdle,
                "the failed recover's alert and answer");
            Assert.True(idleInsideTheAlert, "the recover's operation was still open while its alert stood");
            Assert.True(settledInsideTheAlert, "a settle inside the alert did not return true");
            Assert.Equal((false, false), r.Answers.Last());
            PumpedDispatcher.Drain();
            Assert.Equal(1, alerts);
            Assert.Equal(asked, r.Answers.Count);
        }
        finally
        {
            r.SetSeam(null);
            r.Drain();
        }
    });

    /// <summary>The red team's F5. In background mode a recover whose
    /// create faults is answered by its catch or, were the catch narrow, by
    /// its backstop, and the two look alike. Run inline the worker is no
    /// task, so only the worker's own wide catch can answer the row: a
    /// fault outside VaultException is a failed recover, with the alert and
    /// the row's answer, and nothing is created.</summary>
    [Fact]
    public void ARecoverRunInlineWhoseCreateFaultsIsAnsweredByItsOwnCatch() => RunSta(() =>
    {
        const string Fault = "the create's core call panicked (test seam)";
        using var h = Vault.WithADeletedNote("recover-inline-fault", "gone.md", Gone, background: false);
        (HistoryDeletedRow row, ConcurrentQueue<string> alerts) = RecoverSetup(h);
        var answers = new ConcurrentQueue<(bool Ok, bool Collision)>();
        h.Workspace.HistoryCreateHookForTests = () => throw new InvalidOperationException(Fault);
        try
        {
            h.Workspace.RecoverDeletedFile(row, (ok, collision) => answers.Enqueue((ok, collision)));
            h.PumpUntil(() => h.Workspace.SavesIdle, "the inline recover ending");
            Assert.Equal([(false, false)], answers);
            Assert.Equal(["Can't restore: " + Fault], alerts);
            Assert.False(File.Exists(Path.Combine(h.Root, "gone.md")));
        }
        finally
        {
            h.Workspace.HistoryCreateHookForTests = null;
            h.ReleaseAndDrain();
        }
    });

    /// <summary>Two recovers of one path, the second queued behind the
    /// first. The first creates the file and drops the deleted file's
    /// history, so core answers the second that no such history is left: the
    /// existing "Can't restore" alert, the row answered that nothing
    /// happened, and the file written once.</summary>
    [Fact]
    public void ARecoverQueuedBehindAnotherRecoverOfItsPathIsToldNoHistoryIsLeft() => RunSta(() =>
    {
        using var r = new SinglePathRoute("recover", "recover-behind-recover");
        var held = new HeldWork(r.SetSeam);
        try
        {
            r.Ask();
            r.H.PumpUntil(() => held.Entered, "premise: the first recover reaching its hold before its create");
            r.Ask();
            Assert.Equal(2, r.H.Workspace.SavesForTests.AdmittedForTests);
            PumpedDispatcher.Drain();
            Assert.Empty(r.Answers);

            held.Release();
            r.H.PumpUntil(() => r.Answers.Count == 2 && r.H.Workspace.SavesIdle, "both recovers answering");
            Assert.Equal([(true, false), (false, false)], r.Answers);
            _ = Assert.Single(r.H.Events, e => e is A11yEvent.RestoredFile);
            Assert.StartsWith("Can't restore: ", Assert.Single(r.Alerts), StringComparison.Ordinal);
            Assert.Equal(Gone, r.H.Read("gone.md"));
        }
        finally
        {
            r.H.ReleaseAndDrain(held);
            r.Drain();
        }
        held.AssertHeldUntilReleased();
    });

    /// <summary>A tab that raced open can have a toggle of its own in flight
    /// when the tabless toggle's turn comes. The route decided again then
    /// refuses as busy, with the tab route's existing sentence, heard after
    /// the wait and not at the press; the direct toggle does not run, and
    /// the tab's own toggle is the one write.</summary>
    [Fact]
    public void ATablessReviewToggleWhoseNoteOpenedWithAToggleInFlightHearsBusyAndWritesNothing() => RunSta(() =>
    {
        using var h = new Vault("tabless-toggle-tab-busy", ("a.md", TaskNote));
        (TasksReviewViewModel review, ReviewTaskRowViewModel row) = ReviewRow(h);
        int direct = 0;
        review.DirectToggleBeforeWriteForTests = () => Interlocked.Increment(ref direct);
        var ahead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool> held = h.Workspace.SavesForTests.Admit("a.md", _ => ahead.Task);
        try
        {
            review.ToggleTask(row);
            Assert.Equal(2, h.Workspace.SavesForTests.AdmittedForTests);

            WorkspaceTabViewModel opened = h.OpenNow("a.md");
            Assert.Equal(TabTaskToggle.Started, opened.ToggleTask(h.Session.TasksForFile("a.md")[0], h.Events.Add));
            Assert.Equal(3, h.Workspace.SavesForTests.AdmittedForTests);
            h.Events.Clear();

            ahead.SetResult();
            h.PumpUntil(
                () => h.Workspace.SavesIdle && !opened.IsTaskToggleInFlight && h.Events.Any(e => e is A11yEvent.TaskToggled),
                "the tabless toggle's refusal and then the tab's own toggle");
            Assert.True(held.Result);
            _ = Assert.Single(
                h.Events.OfType<A11yEvent.HostComposed>(),
                line => line.Text == "A task update is already in progress.");
            _ = Assert.Single(h.Events, e => e is A11yEvent.TaskToggled);
            Assert.Equal(0, Volatile.Read(ref direct));
            Assert.Contains("- [x] the task", h.Read("a.md"), StringComparison.Ordinal);
        }
        finally
        {
            _ = ahead.TrySetResult();
            review.DirectToggleBeforeWriteForTests = null;
            ReleaseAndDrainTheReview(h, review);
        }
    });

    /// <summary>A publication that throws (the recover's row callback; the
    /// announcer under the cell write's failure and under the toggle's
    /// success) still ends the operation.</summary>
    [Theory]
    [InlineData("recover")]
    [InlineData("cell write")]
    [InlineData("toggle")]
    public void AnAdmittedSinglePathRouteEndsEvenWhenItsPublicationThrows(string route) => RunSta(() =>
    {
        using var r = new SinglePathRoute(route, "route-publication-throws");
        bool thrown = false;
        void ThrowOnce()
        {
            if (!thrown)
            {
                thrown = true;
                throw new System.Runtime.InteropServices.COMException("the publication failed (test seam)");
            }
        }
        switch (route)
        {
            case "recover":
                r.WhenAnswered = ThrowOnce;
                break;
            case "cell write":
                r.SetSeam(() => throw new InvalidOperationException("the cell write failed (test seam)"));
                r.H.BeforeAnnounce = spoken =>
                {
                    if (spoken is A11yEvent.BasesCellEditFailed)
                    {
                        ThrowOnce();
                    }
                };
                break;
            default:
                r.H.BeforeAnnounce = spoken =>
                {
                    if (spoken is A11yEvent.TaskToggled)
                    {
                        ThrowOnce();
                    }
                };
                break;
        }
        try
        {
            r.Ask();
            Assert.Equal(1, r.H.Workspace.SavesForTests.AdmittedForTests);

            r.H.PumpUntil(() => thrown && r.H.Workspace.SavesIdle, "the route ending after its publication threw");
            Assert.False(r.H.Workspace.PropertyWriteInFlightFor(r.Path), "the lease was not released");
            r.AssertALaterOperationRuns();
        }
        finally
        {
            r.H.BeforeAnnounce = null;
            r.SetSeam(null);
            r.Drain();
        }
    });

    /// <summary>Codex round 1 (C1), for the class: a sentence that throws in
    /// an admitted route's publication skips no state step of it. Here the
    /// recover's success sentence is spoken by an announcer that throws. The
    /// clean tab on the path is still reloaded, inside the ticket, so the
    /// save queued behind the recover runs against the recovered document;
    /// the Deleted list is still reloaded; the row is still answered that
    /// the file is back, once; and the note's chain is freed.</summary>
    [Fact]
    public void ARecoverWhoseSuccessSentenceThrowsStillReloadsItsTabAndAnswersItsRow() => RunSta(() =>
    {
        using var v = new OpenedVault("recover-sentence-throws", "gone.md", Gone);
        v.Workspace.OpenPath("gone.md", WorkspaceOpenTarget.NewTab);
        v.Workspace.InvalidatePath("gone.md");
        PumpedDispatcher.Drain();
        WorkspaceTabViewModel tab = v.Workspace.Groups.SelectMany(group => group.Tabs).Single(candidate => candidate.Path == "gone.md");
        Assert.True(tab.IsMissingFromDisk, "premise: the tab on the deleted path does not report its file missing");
        Assert.False(tab.IsDirty);
        int document = tab.ItemIdentity;
        v.Workspace.History.LoadDeletedFiles();
        Assert.True(
            PumpedDispatcher.PumpUntil(
                () => v.Workspace.History.DeletedRows.Any(row => row.Path == "gone.md"), TimeSpan.FromSeconds(30)),
            "the deleted files list never loaded");
        HistoryDeletedRow deleted = v.Workspace.History.DeletedRows.Single(row => row.Path == "gone.md");
        bool thrown = false;
        v.BeforeAnnounce = spoken =>
        {
            if (spoken is A11yEvent.RestoredFile && !thrown)
            {
                thrown = true;
                throw new System.Runtime.InteropServices.COMException("the announcer failed (test seam)");
            }
        };
        var held = new HeldWork(hook => v.Workspace.HistoryCreateHookForTests = hook);
        var answers = new ConcurrentQueue<(bool Ok, bool Collision)>();
        try
        {
            v.Workspace.RecoverDeletedFile(deleted, (ok, collision) => answers.Enqueue((ok, collision)));
            Assert.True(PumpedDispatcher.PumpUntil(() => held.Entered, TimeSpan.FromSeconds(30)), "premise: the recover never reached its hold");

            string? textAtTheSave = null;
            int documentAtTheSave = -1;
            Task<bool> save = tab.SaveAsync(onSaved: () =>
            {
                textAtTheSave = tab.Text;
                documentAtTheSave = tab.ItemIdentity;
            });
            Assert.Equal(2, v.Workspace.SavesForTests.AdmittedForTests);

            held.Release();
            Assert.True(
                PumpedDispatcher.PumpUntil(() => thrown && save.IsCompleted && v.Workspace.SavesIdle, TimeSpan.FromSeconds(30)),
                "the recover whose sentence threw, and then the save, never ended");
            bool listReloaded = PumpedDispatcher.PumpUntil(
                () => !v.Workspace.History.DeletedRows.Any(row => row.Path == "gone.md"), TimeSpan.FromSeconds(10));
            AssertNoStepSkipped(
                (textAtTheSave == Gone && documentAtTheSave != document, "the clean tab's reload before the queued save ran"),
                (listReloaded, "the Deleted list's reload"),
                (answers.SequenceEqual([(true, false)]), "the row's one answer that the file is back"));
            Assert.True(save.Result);

            bool ran = false;
            Task<bool> later = v.Workspace.SavesForTests.Admit("gone.md", _ =>
            {
                ran = true;
                return Task.CompletedTask;
            });
            Assert.True(
                PumpedDispatcher.PumpUntil(() => later.IsCompleted, TimeSpan.FromSeconds(30)),
                "an operation admitted after the recover never ran");
            Assert.True(ran && later.Result, "the note's chain was still held");
        }
        finally
        {
            v.BeforeAnnounce = null;
            held.Release();
            _ = PumpedDispatcher.PumpUntil(() => v.Workspace.SavesIdle, TimeSpan.FromSeconds(30));
        }
        held.AssertHeldUntilReleased();
    });

    /// <summary>The class again, for a Bases cell write on either route. A
    /// tab on the note is dirty by the time the write is published, so the
    /// publication tells it the disk moved, and that sentence is spoken by
    /// an announcer that throws. The base's refresh still runs (it carries
    /// the cell's own sentence), the lease is released, and the note's chain
    /// is freed.</summary>
    [Theory]
    [InlineData("no tab at the request: one opened while the write ran")]
    [InlineData("a tab at the request")]
    public void ABasesCellWriteStillRefreshesItsBaseWhenTheSentenceToADirtyTabThrows(string route) => RunSta(() =>
    {
        using var h = new Vault("cell-stale-sentence-throws", ("a.md", Note), ("Notes.base", NotesBase));
        WorkspaceTabViewModel? opened = route == "a tab at the request" ? h.OpenNow("a.md") : null;
        (BaseDocumentViewModel document, BasesRow row, BasesColumn column) = StatusCellOf(h);
        WorkspaceTabViewModel? atTheRequest = opened;
        var held = new HeldWork(hook =>
        {
            if (atTheRequest is not null)
            {
                atTheRequest.PropertyWriteHookForTests = hook;
            }
            else
            {
                h.Workspace.BasesTablessWriteHookForTests = hook;
            }
        });
        bool thrown = false;
        h.BeforeAnnounce = spoken =>
        {
            if (spoken is A11yEvent.HostComposed line
                && line.Text.StartsWith("Properties changed on disk", StringComparison.Ordinal)
                && !thrown)
            {
                thrown = true;
                throw new System.Runtime.InteropServices.COMException("the announcer failed (test seam)");
            }
        };
        try
        {
            document.ApplyPropertyEdit!(row, column, new PropertyValue.Text("done"));
            h.PumpUntil(() => held.Entered, "premise: the cell write reaching its hold before its core call");
            opened ??= h.OpenNow("a.md");
            opened.Text = Note + "An unsaved line.\n";
            Assert.True(opened.IsDirty, "premise: the tab is not dirty");
            h.Events.Clear();

            held.Release();
            h.PumpUntil(() => thrown && h.Workspace.SavesIdle, "the cell write ending after its sentence threw");
            bool baseRefreshed = PumpedDispatcher.PumpUntil(
                () => h.Events.Any(e => e is A11yEvent.BasesCellSaved), TimeSpan.FromSeconds(10));
            AssertNoStepSkipped(
                (opened.IsExternallyStale, "the dirty tab's stale flag"),
                (baseRefreshed, "the base's refresh, which carries the cell's sentence"),
                (!h.Workspace.PropertyWriteInFlightFor("a.md"), "the lease's release"));
            Assert.Contains("status: done", h.Read("a.md"), StringComparison.Ordinal);
            AssertALaterOperationRuns(h, "a.md");
        }
        finally
        {
            h.BeforeAnnounce = null;
            h.ReleaseAndDrain(held);
        }
        held.AssertHeldUntilReleased();
    });

    /// <summary>The class again, for a review toggle on a note with no tab:
    /// each sentence its publication speaks, spoken by an announcer that
    /// throws. The steps beside the sentence still run: the rows reload; a
    /// write that landed is still told to the workspace; after a failure
    /// that left the index behind the file, the repair still runs; and when
    /// the sentence that throws is the one to a tab that opened while the
    /// toggle ran, the toggle's own sentence is still spoken.</summary>
    [Theory]
    [InlineData("its success sentence")]
    [InlineData("the sentence to a tab that opened while it ran")]
    [InlineData("its conflict sentence")]
    [InlineData("its failure sentence, after the write landed")]
    public void ATablessReviewToggleStillRunsItsStateStepsWhenASentenceThrows(string sentence) => RunSta(() =>
    {
        using var h = new Vault("toggle-sentence-throws", ("a.md", TaskNote));
        (TasksReviewViewModel review, ReviewTaskRowViewModel row) = ReviewRow(h);
        TaskIndexRepairCoordinator repairs = h.Workspace.TaskIndexRepairsForTests;
        int landed = 0;
        Action<string, string>? toTheWorkspace = review.DiskWriteLanded;
        review.DiskWriteLanded = (path, hash) =>
        {
            landed++;
            toTheWorkspace?.Invoke(path, hash);
        };
        bool thrown = false;
        h.BeforeAnnounce = spoken =>
        {
            bool theSentence = sentence switch
            {
                "its success sentence" => spoken is A11yEvent.TaskToggled,
                "the sentence to a tab that opened while it ran" => spoken is A11yEvent.HostComposed line
                    && line.Text.StartsWith("Task toggled on disk", StringComparison.Ordinal),
                "its conflict sentence" => spoken is A11yEvent.TaskToggleConflict,
                _ => spoken is A11yEvent.TaskToggleFailed,
            };
            if (theSentence && !thrown)
            {
                thrown = true;
                throw new System.Runtime.InteropServices.COMException("the announcer failed (test seam)");
            }
        };
        var held = new HeldWork(hook => review.DirectToggleBeforeWriteForTests = hook);
        try
        {
            if (sentence == "its failure sentence, after the write landed")
            {
                review.DirectToggleFaultForTests = () =>
                    throw new InvalidOperationException("the toggle failed after its write (test seam)");
            }
            review.ToggleTask(row);
            h.PumpUntil(() => held.Entered, "premise: the toggle reaching its hold before its core call");
            WorkspaceTabViewModel? opened = sentence == "the sentence to a tab that opened while it ran" ? h.OpenNow("a.md") : null;
            if (sentence == "its conflict sentence")
            {
                _ = h.Session.SaveText("a.md", TaskNoteEdited, expectedContentHash: null);
            }
            int loads = review.LoadRequestIdForTests;
            h.Events.Clear();

            held.Release();
            h.PumpUntil(() => thrown && h.Workspace.SavesIdle, "the toggle ending after its sentence threw");
            bool rowsReloaded = review.LoadRequestIdForTests > loads;
            switch (sentence)
            {
                case "its success sentence":
                    AssertNoStepSkipped(
                        (landed == 1, "the workspace told that the write landed"),
                        (rowsReloaded, "the rows' reload"));
                    break;
                case "the sentence to a tab that opened while it ran":
                    AssertNoStepSkipped(
                        (opened!.IsExternallyStale, "the stale flag of the tab that opened"),
                        (h.Events.Count(e => e is A11yEvent.TaskToggled) == 1, "the toggle's own sentence"),
                        (rowsReloaded, "the rows' reload"));
                    break;
                case "its conflict sentence":
                    AssertNoStepSkipped((rowsReloaded, "the rows' reload"));
                    Assert.Equal(TaskNoteEdited, h.Read("a.md"));
                    break;
                default:
                    AssertNoStepSkipped(
                        (!repairs.HasPendingFor("a.md"), "the task index's repair"),
                        (landed == 1, "the workspace told that the write landed"),
                        (rowsReloaded, "the rows' reload"));
                    break;
            }
            AssertALaterOperationRuns(h, "a.md");
        }
        finally
        {
            h.BeforeAnnounce = null;
            review.DirectToggleFaultForTests = null;
            h.ReleaseAndDrain(held);
            ReleaseAndDrainTheReview(h, review);
        }
        held.AssertHeldUntilReleased();
    });

    /// <summary>The class once more, at the toggle's turn. A tab opened on
    /// the note while the toggle waited, over bytes the row was not read
    /// from, so the open-note route refuses as stale: it speaks the conflict
    /// and then answers, and the rows reload on its answer. That sentence
    /// is spoken by an announcer that throws, so the answer never comes.
    /// The rows still reload, the operation ends, and nothing is
    /// written.</summary>
    [Fact]
    public void ATablessReviewToggleRefusedAsStaleAtItsTurnStillReloadsItsRowsWhenTheSentenceThrows() => RunSta(() =>
    {
        using var h = new Vault("toggle-stale-at-turn-sentence-throws", ("a.md", TaskNote));
        (TasksReviewViewModel review, ReviewTaskRowViewModel row) = ReviewRow(h);
        int direct = 0;
        review.DirectToggleBeforeWriteForTests = () => Interlocked.Increment(ref direct);
        var ahead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool> held = h.Workspace.SavesForTests.Admit("a.md", _ => ahead.Task);
        bool thrown = false;
        try
        {
            review.ToggleTask(row);
            Assert.Equal(2, h.Workspace.SavesForTests.AdmittedForTests);
            _ = h.Session.SaveText("a.md", TaskNoteEdited, expectedContentHash: null);
            WorkspaceTabViewModel opened = h.OpenNow("a.md");
            Assert.Equal(TaskNoteEdited, opened.Text);
            int loads = review.LoadRequestIdForTests;
            h.BeforeAnnounce = spoken =>
            {
                if (spoken is A11yEvent.TaskToggleConflict && !thrown)
                {
                    thrown = true;
                    throw new System.Runtime.InteropServices.COMException("the announcer failed (test seam)");
                }
            };
            h.Events.Clear();

            ahead.SetResult();
            h.PumpUntil(() => thrown && h.Workspace.SavesIdle, "the toggle's refusal ending after its sentence threw");
            Assert.True(held.Result);
            AssertNoStepSkipped((review.LoadRequestIdForTests > loads, "the rows' reload"));
            Assert.Equal(0, Volatile.Read(ref direct));
            Assert.Equal(TaskNoteEdited, h.Read("a.md"));
            AssertALaterOperationRuns(h, "a.md");
        }
        finally
        {
            h.BeforeAnnounce = null;
            _ = ahead.TrySetResult();
            review.DirectToggleBeforeWriteForTests = null;
            ReleaseAndDrainTheReview(h, review);
        }
    });

    /// <summary>Each state step of a publication whose sentence threw, with
    /// whether it ran: the failure names every one that was skipped.</summary>
    private static void AssertNoStepSkipped(params (bool Ran, string Step)[] steps)
    {
        string[] skipped = [.. steps.Where(step => !step.Ran).Select(step => step.Step)];
        Assert.True(skipped.Length == 0, "the sentence that threw skipped: " + string.Join("; ", skipped));
    }

    /// <summary>The note's chain is free: an operation admitted now runs.</summary>
    private static void AssertALaterOperationRuns(Vault h, string path)
    {
        bool ran = false;
        Task<bool> later = h.Workspace.SavesForTests.Admit(path, _ =>
        {
            ran = true;
            return Task.CompletedTask;
        });
        h.PumpUntil(() => later.IsCompleted, "an operation admitted after the route ended");
        Assert.True(ran && later.Result, "the note's chain was still held");
    }

    /// <summary>The review's scheduler can skip a body it had queued, when
    /// it shuts down first. The toggle's worker then ends having posted no
    /// publication, and the operation still ends: nothing written, nothing
    /// spoken.</summary>
    [Fact]
    public void ATablessReviewToggleWhoseBodyAShutdownSkipsStillEndsItsOperation() => RunSta(() =>
    {
        using var r = new SinglePathRoute("toggle", "toggle-body-skipped");
        TasksReviewViewModel review = r.H.Workspace.TasksReview;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        review.GateWorkOn(gate.Task);
        try
        {
            r.Ask();
            Assert.Equal(1, r.H.Workspace.SavesForTests.AdmittedForTests);

            review.Shutdown();
            gate.SetResult();
            r.H.PumpUntil(() => r.H.Workspace.SavesIdle, "the toggle's operation ending after its body was skipped");
            Assert.False(r.Written);
            r.AssertNothingSpoken();
        }
        finally
        {
            _ = gate.TrySetResult();
            r.Drain();
        }
    });

    /// <summary>Contract 35 A-1 for the three routes: each worker is among
    /// the workers the save coordinator joins, without a bound, before the
    /// session goes. The hold is released inside the join, so the join
    /// returns only when the write has landed.</summary>
    [Theory]
    [InlineData("recover")]
    [InlineData("cell write")]
    [InlineData("toggle")]
    public void TeardownJoinsAnAdmittedRecoverAndTheTablessWrites(string route) => RunSta(() =>
    {
        using var r = new SinglePathRoute(route, "route-teardown-join");
        var held = new HeldWork(r.SetSeam);
        int trackedAtJoin = -1;
        r.H.Workspace.SavesForTests.BeforeJoinForTests = () =>
        {
            trackedAtJoin = r.H.Workspace.SavesForTests.TrackedWorkersForTests;
            held.Release();
        };
        try
        {
            r.Ask();
            r.H.PumpUntil(() => held.Entered, "premise: the route's worker reaching its hold");

            r.H.DisposeWorkspace();
            Assert.Equal(1, trackedAtJoin);
            Assert.True(r.Written, "teardown returned before the route's write landed");
        }
        finally
        {
            held.Release();
            r.Drain();
        }
        held.AssertHeldUntilReleased();
    });

    /// <summary>Close Vault's first step: settling the saves waits for an
    /// admitted route, and returns once its outcome has been published. The
    /// release is queued on the dispatcher, so it can only run inside the
    /// settle's own pumping.</summary>
    [Theory]
    [InlineData("recover")]
    [InlineData("cell write")]
    [InlineData("toggle")]
    public void SettlingSavesWaitsForAnAdmittedSinglePathRouteAndReturns(string route) => RunSta(() =>
    {
        using var r = new SinglePathRoute(route, "route-settle");
        var held = new HeldWork(r.SetSeam);
        try
        {
            r.Ask();
            r.H.PumpUntil(() => held.Entered, "premise: the route's worker reaching its hold");

            _ = Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(held.Release));
            Assert.True(r.H.Workspace.SettleSaves(), "the settle did not return true");
            Assert.True(r.Succeeded, "the settle returned before the route's outcome was published");
            Assert.True(r.H.Workspace.SavesIdle);
        }
        finally
        {
            held.Release();
            r.Drain();
        }
        held.AssertHeldUntilReleased();
    });

    /// <summary>A route still waiting its turn when the workspace is torn
    /// down never runs: nothing is written or spoken, the cell write's lease
    /// is released, and the recover's row is answered that nothing
    /// happened.</summary>
    [Theory]
    [InlineData("recover")]
    [InlineData("cell write")]
    [InlineData("toggle")]
    public void AnAdmittedSinglePathRouteStillWaitingAtTeardownEndsUnwritten(string route) => RunSta(() =>
    {
        using var r = new SinglePathRoute(route, "route-waiting-at-teardown");
        var ahead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool> held = r.H.Workspace.SavesForTests.Admit(r.Path, _ => ahead.Task);
        try
        {
            r.Ask();
            Assert.Equal(2, r.H.Workspace.SavesForTests.AdmittedForTests);

            r.H.DisposeWorkspace();
            ahead.SetResult();
            r.H.PumpUntil(
                () => r.H.Workspace.SavesForTests.IsIdle
                    && !r.H.Workspace.PropertyWriteInFlightFor(r.Path)
                    && (route != "recover" || !r.Answers.IsEmpty),
                "the queued route ending after teardown");
            Assert.True(held.Result);
            Assert.False(r.Written);
            r.AssertNothingSpoken();
            if (route == "recover")
            {
                Assert.Equal([(false, false)], r.Answers);
            }
        }
        finally
        {
            _ = ahead.TrySetResult();
            r.Drain();
        }
    });

    /// <summary>One of the three routes this chunk admits, in a vault of its
    /// own with no tab on its note, ready to be asked once: "recover", "cell
    /// write" or "toggle" (the route is its name's first word).</summary>
    private sealed class SinglePathRoute : IDisposable
    {
        private readonly string _kind;
        private readonly HistoryDeletedRow? _deleted;
        private readonly BaseDocumentViewModel? _document;
        private readonly BasesRow? _row;
        private readonly BasesColumn? _column;
        private readonly ReviewTaskRowViewModel? _task;

        public SinglePathRoute(string route, string label)
        {
            _kind = route.Split(' ')[0];
            switch (_kind)
            {
                case "recover":
                    H = Vault.WithADeletedNote(label + "-recover", "gone.md", Gone);
                    Path = "gone.md";
                    (_deleted, Alerts) = RecoverSetup(H);
                    break;
                case "cell":
                    H = new Vault(label + "-cell", ("a.md", Note), ("Notes.base", NotesBase));
                    Path = "a.md";
                    (_document, _row, _column) = TablessStatusCell(H);
                    break;
                default:
                    H = new Vault(label + "-toggle", ("a.md", TaskNote));
                    Path = "a.md";
                    (_, _task) = ReviewRow(H);
                    break;
            }
        }

        public Vault H { get; }

        public string Path { get; }

        public ConcurrentQueue<string> Alerts { get; } = new();

        /// <summary>The recover's row answers.</summary>
        public ConcurrentQueue<(bool Ok, bool Collision)> Answers { get; } = new();

        /// <summary>Runs inside the recover's row callback, after the answer
        /// is recorded: a fact makes the callback throw.</summary>
        public Action? WhenAnswered { get; set; }

        public void Ask()
        {
            switch (_kind)
            {
                case "recover":
                    H.Workspace.RecoverDeletedFile(_deleted!, (ok, collision) =>
                    {
                        Answers.Enqueue((ok, collision));
                        WhenAnswered?.Invoke();
                    });
                    break;
                case "cell":
                    _document!.ApplyPropertyEdit!(_row!, _column!, new PropertyValue.Text("done"));
                    break;
                default:
                    H.Workspace.TasksReview.ToggleTask(_task!);
                    break;
            }
        }

        /// <summary>The seam on the route's worker, before its core call.</summary>
        public void SetSeam(Action? hook)
        {
            switch (_kind)
            {
                case "recover":
                    H.Workspace.HistoryCreateHookForTests = hook;
                    break;
                case "cell":
                    H.Workspace.BasesTablessWriteHookForTests = hook;
                    break;
                default:
                    H.Workspace.TasksReview.DirectToggleBeforeWriteForTests = hook;
                    break;
            }
        }

        public bool Written => _kind switch
        {
            "recover" => File.Exists(System.IO.Path.Combine(H.Root, Path)),
            "cell" => H.Read(Path).Contains("status: done", StringComparison.Ordinal),
            _ => H.Read(Path).Contains("- [x] the task", StringComparison.Ordinal),
        };

        /// <summary>The route's outcome on success, published.</summary>
        public bool Succeeded => _kind switch
        {
            "recover" => Answers.Contains((true, false)) && H.Events.Any(e => e is A11yEvent.RestoredFile),
            "cell" => Written && !H.Workspace.PropertyWriteInFlightFor(Path),
            _ => H.Events.Any(e => e is A11yEvent.TaskToggled),
        };

        public bool FailureGiven => _kind switch
        {
            "recover" => !Alerts.IsEmpty && !Answers.IsEmpty,
            "cell" => H.Events.Any(e => e is A11yEvent.BasesCellEditFailed) && !H.Workspace.PropertyWriteInFlightFor(Path),
            _ => H.Events.Any(e => e is A11yEvent.TaskToggleFailed),
        };

        /// <summary>The route's existing failure, given once.</summary>
        public void AssertFailedOnce(string message)
        {
            switch (_kind)
            {
                case "recover":
                    Assert.Equal(["Can't restore: " + message], Alerts);
                    Assert.Equal([(false, false)], Answers);
                    Assert.DoesNotContain(H.Events, e => e is A11yEvent.RestoredFile);
                    break;
                case "cell":
                    _ = Assert.Single(H.Events, e => e is A11yEvent.BasesCellEditFailed);
                    Assert.False(H.Workspace.PropertyWriteInFlightFor(Path), "the lease was not released");
                    Assert.DoesNotContain(H.Events, e => e is A11yEvent.BasesCellSaved);
                    break;
                default:
                    _ = Assert.Single(H.Events, e => e is A11yEvent.TaskToggleFailed);
                    Assert.DoesNotContain(H.Events, e => e is A11yEvent.TaskToggled);
                    break;
            }
        }

        public void AssertNothingSpoken()
        {
            Assert.Empty(Alerts);
            Assert.DoesNotContain(H.Events, e => e is A11yEvent.RestoredFile
                or A11yEvent.BasesCellSaved or A11yEvent.BasesCellCleared or A11yEvent.BasesCellEditFailed
                or A11yEvent.TaskToggled or A11yEvent.TaskToggleConflict or A11yEvent.TaskToggleFailed);
        }

        /// <summary>The note's chain is free: an operation admitted now runs.</summary>
        public void AssertALaterOperationRuns()
        {
            bool ran = false;
            Task<bool> later = H.Workspace.SavesForTests.Admit(Path, _ =>
            {
                ran = true;
                return Task.CompletedTask;
            });
            H.PumpUntil(() => later.IsCompleted, "an operation admitted after the route ended");
            Assert.True(ran && later.Result, "the note's chain was still held");
        }

        /// <summary>For a fact's <c>finally</c>: everything admitted ends and
        /// the review's own workers end, before the vault is disposed.</summary>
        public void Drain() => ReleaseAndDrainTheReview(H, H.Workspace.TasksReview);

        public void Dispose() => H.Dispose();
    }

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

    // ---- No undo step restores another writer's bytes (plan 47 CB-9b, Design 0) --

    private const string TwoKeyNote = "---\nstatus: draft\ntitle: A\n---\n\nBody.\n";

    /// <summary>Plan 47 CB-9b, Design 0 (the plan lead's word of 2026-11-17).
    /// Another writer's bytes land in a clean tab on a.md and in its split
    /// peer. Afterwards neither tab can undo, so one Undo and a save from
    /// either tab leave the write on disk, and each tab's caret keeps its
    /// line (line 5, the blank line under the frontmatter, which every
    /// route's new text keeps). Today the whole-document replace stays on
    /// the source's undo stack and reaches the peer's through the mirroring,
    /// and an Undo then a save writes the old bytes back (M10, M11). The tag
    /// edit's row (stage B, Design 4) and the link rewrite's (Design 5) are
    /// red today on their first assertion: no tab follows a tag edit or a
    /// link rewrite at all. Each assertion names the tab it is about, so the
    /// peer route is held on its own: with the branch in
    /// MirrorDocumentStateFrom that makes a peer take another writer's bytes
    /// itself removed, every row fails on the split peer.</summary>
    [Theory]
    [InlineData("property set from the header")]
    [InlineData("property delete")]
    [InlineData("property add")]
    [InlineData("Bases cell edit through a tab")]
    [InlineData("tabless Bases cell write with tabs that raced open")]
    [InlineData("bulk rename")]
    [InlineData("tag edit")]
    [InlineData("link rewrite (rename)")]
    public void AnUndoCannotRestoreBytesAnotherWriterReplaced(string route) => RunSta(() =>
    {
        // The link rewrite's row: a.md links to b.md, which the reader
        // renames from the Files sidebar, so core rewrites a.md's link.
        using var h = route == "link rewrite (rename)"
            ? new Vault("undo-across-writer", ("a.md", TwoKeyNote + "See [[b]].\n"), ("b.md", "# B\n"))
            : new Vault("undo-across-writer", ("a.md", TwoKeyNote), ("Notes.base", NotesBase));
        bool tabless = route == "tabless Bases cell write with tabs that raced open";
        WorkspaceTabViewModel? a = null;
        WorkspaceTabViewModel? b = null;
        void OpenBoth()
        {
            a = h.OpenNow("a.md");
            WorkspaceGroupViewModel groupA = h.Workspace.ActiveGroup;
            b = OpenSplitPeer(h, "a.md");
            h.Workspace.Activate(groupA, a);
            foreach (WorkspaceTabViewModel tab in new[] { a, b })
            {
                Assert.False(tab.IsDirty, "premise: a tab opened dirty");
                Assert.False(tab.EditorDocument!.UndoStack.CanUndo, "premise: a tab opened with an undo step");
                tab.EditorCaretOffset = tab.EditorDocument.GetLineByNumber(5).Offset;
            }
        }
        HeldWork? held = null;
        try
        {
            string expected;
            Func<bool> landed;
            if (tabless)
            {
                (BaseDocumentViewModel document, BasesRow row, BasesColumn column) = TablessStatusCell(h);
                held = new HeldWork(hook => h.Workspace.BasesTablessWriteHookForTests = hook);
                document.ApplyPropertyEdit!(row, column, new PropertyValue.Text("done"));
                h.PumpUntil(() => held.Entered, "premise: the cell write reaching its hold before its core call");
                OpenBoth();
                held.Release();
                expected = "status: done";
                landed = () => h.Events.Any(e => e is A11yEvent.BasesCellSaved);
            }
            else
            {
                OpenBoth();
                (expected, landed) = WriteThroughAnOpenTab(h, route);
            }
            h.PumpUntil(() => landed() && h.Workspace.SavesIdle, $"the {route} landing");
            string written = h.Read("a.md");
            Assert.Contains(expected, written, StringComparison.Ordinal);

            foreach ((string name, WorkspaceTabViewModel tab) in new[] { ("the source", a!), ("the split peer", b!) })
            {
                Assert.True(
                    string.Equals(written, tab.Text, StringComparison.Ordinal),
                    $"{name} does not show the bytes the write left on disk; it shows "
                    + tab.Text.Replace("\n", "\\n", StringComparison.Ordinal));
                Assert.False(tab.IsDirty, $"{name} is dirty after the write");
                Assert.False(tab.EditorDocument!.UndoStack.CanUndo, $"{name} can undo across the bytes another writer wrote");
                Assert.InRange(tab.EditorCaretOffset, 0, tab.EditorDocument.TextLength);
                int line = tab.EditorDocument.GetLocation(tab.EditorCaretOffset).Line;
                Assert.True(line == 5, $"{name}'s caret is on line {line}; expected line 5");
            }
            foreach (WorkspaceTabViewModel tab in new[] { a!, b! })
            {
                tab.EditorDocument!.UndoStack.Undo();
                Task<bool> save = tab.SaveAsync();
                h.PumpUntil(() => save.IsCompleted && h.Workspace.SavesIdle, "a save after an Undo");
                Assert.Equal(written, h.Read("a.md"));
            }
        }
        finally
        {
            h.Workspace.BasesTablessWriteHookForTests = null;
            h.ReleaseAndDrain([.. held is null ? Array.Empty<HeldWork>() : [held]]);
        }
        held?.AssertHeldUntilReleased();
    });

    /// <summary>Plan 47 CB-9b, the lead's ruling 8: another writer's bytes
    /// keep the caret on its line, as the rescan's reload does
    /// (<c>WorkspaceTabViewModel.ReloadKeepingCaretLine</c>), in the tab the
    /// write rebaselines and in its split peer; a line past the new end
    /// clamps to the last line, and the column clamps to that line. Today
    /// the replace leaves the caret's offset where it was (headless; with
    /// the editor view, AvalonEdit moves it to an end of the new
    /// text).</summary>
    [Theory]
    [InlineData("a line the new text keeps")]
    [InlineData("a line past the new end")]
    public void ABaselineReplaceKeepsTheCaretsLineAndClampsALinePastTheNewEnd(string where) => RunSta(() =>
    {
        const string Before = "one\ntwo\nthree\nfour\nfive\n";
        bool past = where == "a line past the new end";
        string after = past ? "x\ny\n" : "a much longer first line than before\ntwo\nthree\nfour\nfive\n";
        using var h = new Vault("baseline-caret", ("a.md", Before));
        WorkspaceTabViewModel a = h.OpenNow("a.md");
        WorkspaceTabViewModel b = OpenSplitPeer(h, "a.md");
        foreach (WorkspaceTabViewModel tab in new[] { a, b })
        {
            // Line 5 ("five"), column 3.
            tab.EditorCaretOffset = tab.EditorDocument!.GetLineByNumber(5).Offset + 2;
        }

        SaveReport report = h.Session.SaveText("a.md", after, a.SavedContentHash);
        Assert.True(h.Workspace.ReconcileTabsAfterPropertyWrite("a.md", report.NewContentHash));

        // "x\ny\n" has three lines, the last empty: line 5 clamps to line 3,
        // and column 3 to that line's column 1.
        (int line, int column) = past ? (3, 1) : (5, 3);
        foreach ((string name, WorkspaceTabViewModel tab) in new[] { ("the source", a), ("the split peer", b) })
        {
            Assert.Equal(after, tab.Text);
            Assert.InRange(tab.EditorCaretOffset, 0, tab.EditorDocument!.TextLength);
            ICSharpCode.AvalonEdit.Document.TextLocation caret = tab.EditorDocument.GetLocation(tab.EditorCaretOffset);
            Assert.True(
                caret.Line == line && caret.Column == column,
                $"{name}'s caret is at line {caret.Line}, column {caret.Column}; expected line {line}, column {column}");
        }
    });

    /// <summary>Plan 47 CB-9b, Design 0 (the lead's ruling on stage A's
    /// departure (b), 2026-11-17). A transient tab, the one a Files selection
    /// shows its note in, stays transient when another writer's bytes land in
    /// it: the reader made no edit, so the next selection may still replace
    /// it. It takes the bytes clean, with no undo step back across them. Red
    /// by mutation: take the bytes with a replace that passes through a dirty
    /// state (ReplaceAll, then MarkSaved), which ends the tab's transience for
    /// good.</summary>
    [Theory]
    [InlineData("property set from the header")]
    [InlineData("tag edit")]
    public void ATransientTabStaysTransientWhenAnotherWritersBytesLand(string route) => RunSta(() =>
    {
        using var h = new Vault("transient-stays-transient", ("a.md", TwoKeyNote));
        h.Workspace.OpenPath("a.md", WorkspaceOpenTarget.CurrentTab, fromSelection: true);
        PumpedDispatcher.Drain();
        WorkspaceTabViewModel tab = h.Workspace.ActiveGroup.ActiveTab!;
        Assert.Equal("a.md", tab.Path);
        Assert.True(tab.IsTransient, "premise: a Files selection did not open a transient tab");
        try
        {
            (string expected, Func<bool> landed) = WriteThroughAnOpenTab(h, route);
            h.PumpUntil(() => landed() && h.Workspace.SavesIdle, $"the {route} landing");
            string written = h.Read("a.md");
            Assert.Contains(expected, written, StringComparison.Ordinal);
            Assert.Equal(written, tab.Text);
            Assert.True(tab.IsTransient, "the transient tab stopped being transient when another writer's bytes landed");
            Assert.False(tab.IsDirty);
            Assert.False(tab.EditorDocument!.UndoStack.CanUndo, "the transient tab can undo back across the bytes");
        }
        finally
        {
            h.ReleaseAndDrain();
        }
    });

    // ---- A bulk rename's apply in the notes' chain (plan 47 CB-9b, Design 2) ---

    /// <summary>Design 2 (CD5(a); M1). A save of an open note is admitted and
    /// held before its write; the reader applies a bulk rename whose preview
    /// listed the note. The apply takes a ticket behind the save and writes
    /// nothing while the save is held. After release the save publishes
    /// first, and the apply renames the key on the saved bytes: no conflict,
    /// and the tab ends clean and current.</summary>
    [Fact]
    public void ABulkRenameApplyWaitsForAnOpenNotesAdmittedSaveAndRenamesOnTheSavedBytes() => RunSta(() =>
    {
        using var h = new Vault("apply-behind-save", ("a.md", Note));
        WorkspaceTabViewModel tab = h.OpenNow("a.md");
        BulkRenameViewModel sheet = ArmedBulkRename(h);
        var held = new HeldWork(hook => tab.SaveWriteHookForTests = hook);
        try
        {
            tab.Text = NoteEdited;
            Task<bool> save = tab.SaveAsync(announce: true);
            h.PumpUntil(() => held.Entered, "premise: the save reaching its hold before its write");
            h.Events.Clear();

            Assert.True(sheet.Apply(), "premise: the apply was refused");
            Assert.Equal(2, h.Workspace.SavesForTests.AdmittedForTests);
            PumpedDispatcher.Drain();
            Assert.Equal(Note, h.Read("a.md"));

            held.Release();
            h.PumpUntil(
                () => save.IsCompleted && !sheet.WorkInFlight && h.Workspace.SavesIdle,
                "the save and then the apply");
            Assert.True(save.Result, "the save admitted before the apply was refused");
            int saved = h.Events.FindIndex(e => e is A11yEvent.NoteSaved);
            int summary = h.Events.FindIndex(e => e is A11yEvent.RenameSummary { Applied: true, Renamed: 1 });
            Assert.True(saved >= 0 && summary > saved, $"expected the save's sentence and then the summary; heard {Heard(h)}");
            Assert.DoesNotContain(h.Events, e => e is A11yEvent.NoteSaveConflict);
            string written = h.Read("a.md");
            Assert.Contains("state: draft", written, StringComparison.Ordinal);
            Assert.Contains("An edit on its way to disk.", written, StringComparison.Ordinal);
            Assert.Equal(written, tab.Text);
            Assert.False(tab.IsDirty);
            Assert.False(tab.IsExternallyStale);
        }
        finally
        {
            h.ReleaseAndDrain(held);
        }
        held.AssertHeldUntilReleased();
    });

    /// <summary>Design 2 (CD5(a); M2). The apply is held on its worker
    /// before its core call; the reader edits a note its preview listed and
    /// saves. The save takes a ticket behind the apply and does not complete
    /// while the apply is held; it ends after the apply's summary.</summary>
    [Fact]
    public void ASaveOfACandidateNoteAskedDuringAnApplyWaitsForItsPublication() => RunSta(() =>
    {
        using var h = new Vault("save-behind-apply", ("a.md", Note));
        WorkspaceTabViewModel tab = h.OpenNow("a.md");
        BulkRenameViewModel sheet = ArmedBulkRename(h);
        var held = new HeldWork(hook => sheet.ApplyHookForTests = hook);
        try
        {
            Assert.True(sheet.Apply(), "premise: the apply was refused");
            h.PumpUntil(() => held.Entered, "premise: the apply reaching its hold before its core call");
            tab.Text = NoteEdited;
            Task<bool> save = tab.SaveAsync(announce: true);
            Assert.Equal(2, h.Workspace.SavesForTests.AdmittedForTests);
            PumpedDispatcher.Drain();
            Assert.False(save.IsCompleted, "the save did not wait for the apply's publication");

            held.Release();
            h.PumpUntil(
                () => save.IsCompleted && !sheet.WorkInFlight && h.Workspace.SavesIdle,
                "the apply and then the save");
            int summary = h.Events.FindIndex(e => e is A11yEvent.RenameSummary { Applied: true });
            int saveOutcome = h.Events.FindIndex(e => e is A11yEvent.NoteSaved or A11yEvent.NoteSaveConflict);
            Assert.True(summary >= 0 && saveOutcome > summary, $"expected the summary and then the save's outcome; heard {Heard(h)}");
        }
        finally
        {
            h.ReleaseAndDrain(held);
        }
        held.AssertHeldUntilReleased();
    });

    /// <summary>Design 2 (Call 4). The apply holds only the notes its
    /// preview listed: while it is held, a save of a note the preview did not
    /// list completes.</summary>
    [Fact]
    public void ASaveOfANoteThePreviewDidNotListDoesNotWaitForAnApply() => RunSta(() =>
    {
        const string Edited = "# B\n\nEdited beside the apply.\n";
        using var h = new Vault("unlisted-save-beside-apply", ("a.md", Note), ("b.md", "# B\n"));
        WorkspaceTabViewModel other = h.OpenNow("b.md");
        BulkRenameViewModel sheet = ArmedBulkRename(h);
        Assert.DoesNotContain(sheet.Rows, row => row.Path == "b.md");
        var held = new HeldWork(hook => sheet.ApplyHookForTests = hook);
        try
        {
            Assert.True(sheet.Apply(), "premise: the apply was refused");
            h.PumpUntil(() => held.Entered, "premise: the apply reaching its hold before its core call");
            other.Text = Edited;
            Task<bool> save = other.SaveAsync(announce: true);
            h.PumpUntil(() => save.IsCompleted, "the save of a note the preview did not list, while the apply is held");
            Assert.True(save.Result);
            Assert.Equal(Edited, h.Read("b.md"));
            Assert.True(sheet.WorkInFlight, "premise: the apply was no longer held");
        }
        finally
        {
            h.ReleaseAndDrain(held);
        }
        held.AssertHeldUntilReleased();
    });

    /// <summary>Design 2 (Call 5, on WB-2's precedent). The apply is held;
    /// the reader edits a note its preview listed and saves. The save waits;
    /// the apply then renames the key under the dirty buffer, so the reader
    /// hears the property sentence and the summary, and the save that runs
    /// after them is refused: the renamed bytes stay on disk and the edit
    /// stays in the editor.</summary>
    [Fact]
    public void ADirtySaveAskedDuringAnApplyThatRewritesItsNoteIsRefusedAndOverwritesNothing() => RunSta(() =>
    {
        using var h = new Vault("dirty-save-behind-apply", ("a.md", Note));
        WorkspaceTabViewModel tab = h.OpenNow("a.md");
        BulkRenameViewModel sheet = ArmedBulkRename(h);
        var held = new HeldWork(hook => sheet.ApplyHookForTests = hook);
        try
        {
            Assert.True(sheet.Apply(), "premise: the apply was refused");
            h.PumpUntil(() => held.Entered, "premise: the apply reaching its hold before its core call");
            tab.Text = NoteEdited;
            Task<bool> save = tab.SaveAsync(announce: true);
            Assert.Equal(2, h.Workspace.SavesForTests.AdmittedForTests);

            held.Release();
            h.PumpUntil(
                () => save.IsCompleted && !sheet.WorkInFlight && h.Workspace.SavesIdle,
                "the apply and then the save");
            Assert.False(save.Result, "the dirty save over the renamed bytes was not refused");
            int stale = h.Events.FindIndex(e => e is A11yEvent.HostComposed composed
                && composed.Text.StartsWith("Properties changed on disk", StringComparison.Ordinal));
            int summary = h.Events.FindIndex(e => e is A11yEvent.RenameSummary { Applied: true, Renamed: 1 });
            int conflict = h.Events.FindIndex(e => e is A11yEvent.NoteSaveConflict);
            Assert.True(
                stale >= 0 && summary > stale && conflict > summary,
                $"expected the property sentence, the summary and then the save's conflict; heard {Heard(h)}");
            _ = Assert.Single(h.Events, e => e is A11yEvent.NoteSaveConflict);
            string disk = h.Read("a.md");
            Assert.Contains("state: draft", disk, StringComparison.Ordinal);
            Assert.DoesNotContain("An edit on its way to disk.", disk, StringComparison.Ordinal);
            Assert.True(tab.IsDirty);
            Assert.Equal(NoteEdited, tab.Text);
        }
        finally
        {
            h.ReleaseAndDrain(held);
        }
        held.AssertHeldUntilReleased();
    });

    /// <summary>Design 2's catch-up. While the apply holds a note its preview
    /// listed, the note is rewritten outside Slate without the key and a
    /// rescan's re-sync runs: the re-sync, which asks the chain, marks the
    /// clean tab stale and keeps its bytes. Core then skips the note, so no
    /// reconcile touches it; the apply's catch-up brings the tab to the
    /// outside bytes inside its ticket, with no second re-sync, and leaves no
    /// undo step back. Red by mutation: drop the catch-up.</summary>
    [Fact]
    public void AnApplyCatchesUpATabARescanMarkedStaleWhileItHeldTheNote() => RunSta(() =>
    {
        const string Outside = "---\ntitle: written outside Slate\n---\n\nThe key is gone.\n";
        using var h = new Vault("apply-catches-up-stale", ("a.md", Note));
        WorkspaceTabViewModel tab = h.OpenNow("a.md");
        BulkRenameViewModel sheet = ArmedBulkRename(h);
        var held = new HeldWork(hook => sheet.ApplyHookForTests = hook);
        try
        {
            Assert.True(sheet.Apply(), "premise: the apply was refused");
            h.PumpUntil(() => held.Entered, "premise: the apply reaching its hold before its core call");
            File.WriteAllText(Path.Combine(h.Root, "a.md"), Outside);
            using (var scan = new CancelToken())
            {
                _ = h.Session.Rescan(scan);
            }
            h.ReSyncDocuments("a.md");
            Assert.True(tab.IsExternallyStale, "premise: the re-sync reloaded the tab under the apply that holds its note");
            Assert.Equal(Note, tab.Text);

            held.Release();
            h.PumpUntil(
                () => !sheet.WorkInFlight && h.Workspace.SavesIdle && h.Events.Any(e => e is A11yEvent.RenameSummary),
                "the apply's publication");
            Assert.Equal(Outside, h.Read("a.md"));
            Assert.Equal(Outside, tab.Text);
            Assert.False(tab.IsExternallyStale);
            Assert.False(tab.IsDirty);
            Assert.False(tab.EditorDocument!.UndoStack.CanUndo, "the caught-up tab can undo back across the outside bytes");
        }
        finally
        {
            h.ReleaseAndDrain(held);
        }
        held.AssertHeldUntilReleased();
    });

    /// <summary>Design 2's catch-up, M13's shape on a note the preview
    /// listed. While the apply holds the note, its file goes missing and
    /// comes back with other bytes and no key, and the index is rescanned:
    /// the re-seat, which asks the chain, marks the tab back on disk with its
    /// old bytes, old identity and old hash. Core skips the note; the
    /// apply's catch-up brings the tab to the returned file. Red by
    /// mutation: drop the catch-up.</summary>
    [Fact]
    public void AnApplyCatchesUpATabReseatedWhileItHeldTheNote() => RunSta(() =>
    {
        const string Returned = "---\ntitle: back\n---\n\nIt came back without the key.\n";
        using var h = new Vault("apply-catches-up-reseat", ("a.md", Note));
        WorkspaceTabViewModel tab = h.OpenNow("a.md");
        BulkRenameViewModel sheet = ArmedBulkRename(h);
        var held = new HeldWork(hook => sheet.ApplyHookForTests = hook);
        try
        {
            Assert.True(sheet.Apply(), "premise: the apply was refused");
            h.PumpUntil(() => held.Entered, "premise: the apply reaching its hold before its core call");
            string full = Path.Combine(h.Root, "a.md");
            File.Delete(full);
            h.Workspace.InvalidatePath("a.md");
            Assert.True(tab.IsMissingFromDisk, "premise: the tab was not marked missing");
            File.WriteAllText(full, Returned);
            using (var scan = new CancelToken())
            {
                _ = h.Session.Rescan(scan);
            }
            h.Workspace.ReseatMissingTabs();
            Assert.False(tab.IsMissingFromDisk, "premise: the re-seat did not mark the tab back on disk");
            Assert.Equal(Note, tab.Text);

            held.Release();
            h.PumpUntil(
                () => !sheet.WorkInFlight && h.Workspace.SavesIdle && h.Events.Any(e => e is A11yEvent.RenameSummary),
                "the apply's publication");
            Assert.Equal(Returned, h.Read("a.md"));
            Assert.Equal(Returned, tab.Text);
            Assert.Equal(h.Session.ReadTextIdentified("a.md").FileIdentity, tab.FileIdentity);
            Assert.False(tab.IsExternallyStale);
            Assert.False(tab.IsDirty);
            Assert.False(tab.EditorDocument!.UndoStack.CanUndo, "the caught-up tab can undo back across the returned bytes");
        }
        finally
        {
            h.ReleaseAndDrain(held);
        }
        held.AssertHeldUntilReleased();
    });

    /// <summary>Design 2's catch-up when its read fails (the lead's ruling on
    /// stage A's departure (c), 2026-11-17). M13's shape: while the apply
    /// holds the note, its file goes missing and comes back with other bytes,
    /// and the re-seat marks the tab back on disk with its old bytes and old
    /// hash. The file is then held open with no sharing, so the catch-up's
    /// read of it fails, and RebaselineToRead never runs: the tab is left
    /// clean on its old bytes, unmarked and silent. Its next save is refused
    /// by the compare against that old hash, so nothing the file came back
    /// with is overwritten and the edit stays in the editor. Red by mutation:
    /// a save written with no expected hash overwrites the file.</summary>
    [Fact]
    public void ATabTheCatchUpCouldNotReadKeepsItsOldHashAndItsNextSaveIsRefused() => RunSta(() =>
    {
        const string Returned = "---\ntitle: back\n---\n\nIt came back without the key.\n";
        const string Edited = Note + "An edit after the catch-up could not read.\n";
        using var h = new Vault("catch-up-read-fails", ("a.md", Note));
        WorkspaceTabViewModel tab = h.OpenNow("a.md");
        string? oldHash = tab.SavedContentHash;
        BulkRenameViewModel sheet = ArmedBulkRename(h);
        var held = new HeldWork(hook => sheet.ApplyHookForTests = hook);
        string full = Path.Combine(h.Root, "a.md");
        FileStream? holdTheFile = null;
        try
        {
            Assert.True(sheet.Apply(), "premise: the apply was refused");
            h.PumpUntil(() => held.Entered, "premise: the apply reaching its hold before its core call");
            File.Delete(full);
            h.Workspace.InvalidatePath("a.md");
            File.WriteAllText(full, Returned);
            using (var scan = new CancelToken())
            {
                _ = h.Session.Rescan(scan);
            }
            h.Workspace.ReseatMissingTabs();
            Assert.False(tab.IsMissingFromDisk, "premise: the re-seat did not mark the tab back on disk");
            Assert.Equal(Note, tab.Text);
            holdTheFile = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.None);

            held.Release();
            h.PumpUntil(
                () => !sheet.WorkInFlight && h.Workspace.SavesIdle && h.Events.Any(e => e is A11yEvent.RenameSummary),
                "the apply's publication");
            Assert.Equal(Note, tab.Text);
            Assert.Equal(oldHash, tab.SavedContentHash);
            Assert.False(tab.IsDirty);
            Assert.False(tab.IsExternallyStale);
            Assert.DoesNotContain(h.Events, IsThePropertySentence);
            holdTheFile.Dispose();
            holdTheFile = null;

            h.Events.Clear();
            tab.Text = Edited;
            Task<bool> save = tab.SaveAsync(announce: true);
            h.PumpUntil(() => save.IsCompleted && h.Workspace.SavesIdle, "the tab's next save");
            Assert.False(save.Result, "the next save of a tab the catch-up could not read was not refused");
            _ = Assert.Single(h.Events, e => e is A11yEvent.NoteSaveConflict);
            Assert.Equal(Returned, h.Read("a.md"));
            Assert.True(tab.IsDirty);
            Assert.Equal(Edited, tab.Text);
        }
        finally
        {
            holdTheFile?.Dispose();
            h.ReleaseAndDrain(held);
        }
        held.AssertHeldUntilReleased();
    });

    /// <summary>Design 2. The preview writes nothing and is not admitted: it
    /// publishes its rows while a save of the note it lists is held, and takes
    /// no ticket. Red by mutation: route the preview through the
    /// admission.</summary>
    [Fact]
    public void TheBulkRenamePreviewWaitsForNothing() => RunSta(() =>
    {
        using var h = new Vault("preview-waits-for-nothing", ("a.md", Note));
        WorkspaceTabViewModel tab = h.OpenNow("a.md");
        var held = new HeldWork(hook => tab.SaveWriteHookForTests = hook);
        try
        {
            tab.Text = NoteEdited;
            Task<bool> save = tab.SaveAsync(announce: true);
            h.PumpUntil(() => held.Entered, "premise: the save reaching its hold before its write");
            int admitted = h.Workspace.SavesForTests.AdmittedForTests;

            h.Workspace.OpenBulkRenameSheet();
            BulkRenameViewModel sheet = h.Workspace.BulkRenameSheet!;
            sheet.OldKey = "status";
            sheet.NewKey = "state";
            sheet.Preview();
            h.PumpUntil(() => !sheet.WorkInFlight && sheet.Rows.Count > 0, "the preview publishing beside the held save");
            Assert.Equal(admitted, h.Workspace.SavesForTests.AdmittedForTests);
            Assert.Contains(sheet.Rows, row => row.Path == "a.md" && row.Status == "Will apply");
            Assert.False(save.IsCompleted, "premise: the save was no longer held");
        }
        finally
        {
            h.ReleaseAndDrain(held);
        }
        held.AssertHeldUntilReleased();
    });

    /// <summary>Design 2 (the red team's finding 3a). The apply waits behind
    /// an operation admitted for its note; the reader presses Escape. At its
    /// turn the apply still runs, with its token cancelled: core answers at
    /// once with the note cancelled, the reader hears what Escape gives today,
    /// "0 renamed, 0 skipped, 1 failed.", once, the sheet settles its close
    /// once, and nothing is written.</summary>
    [Fact]
    public void AnApplyCancelledWhileItWaitedSpeaksTodaysSummary() => RunSta(() =>
    {
        using var h = new Vault("apply-cancelled-while-waiting", ("a.md", Note));
        BulkRenameViewModel sheet = ArmedBulkRename(h);
        int settled = 0;
        sheet.CloseSettled += () => settled++;
        var ahead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool> held = h.Workspace.SavesForTests.Admit("a.md", _ => ahead.Task);
        try
        {
            Assert.True(sheet.Apply(), "premise: the apply was refused");
            Assert.Equal(2, h.Workspace.SavesForTests.AdmittedForTests);
            sheet.RequestClose();
            Assert.Equal(0, settled);

            ahead.SetResult();
            h.PumpUntil(() => settled > 0 && h.Workspace.SavesIdle, "the cancelled apply's publication");
            A11yEvent.RenameSummary summary = Assert.Single(h.Events.OfType<A11yEvent.RenameSummary>());
            Assert.Equal(new A11yEvent.RenameSummary(Applied: true, Renamed: 0, Skipped: 0, Failed: 1), summary);
            Assert.Equal(1, settled);
            Assert.Equal(Note, h.Read("a.md"));
            Assert.True(held.Result);
        }
        finally
        {
            _ = ahead.TrySetResult();
            h.ReleaseAndDrain();
        }
    });

    /// <summary>Design 2 (Call 2(d); the red team's finding 17). The apply
    /// re-checks the header-draft gate at its turn: a header row's draft set
    /// dirty while the apply waited refuses it with the draft sentence, once,
    /// and nothing is written.</summary>
    [Fact]
    public void AnApplyWhoseDraftsWentDirtyWhileItWaitedRefusesWithTheDraftSentence() => RunSta(() =>
    {
        using var h = new Vault("apply-drafts-dirty-at-turn", ("a.md", Note));
        _ = h.OpenNow("a.md");
        NotePropertiesViewModel header = LoadedHeader(h);
        BulkRenameViewModel sheet = ArmedBulkRename(h);
        var ahead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool> held = h.Workspace.SavesForTests.Admit("a.md", _ => ahead.Task);
        try
        {
            Assert.True(sheet.Apply(), "premise: the apply was refused at the press");
            Assert.Equal(2, h.Workspace.SavesForTests.AdmittedForTests);
            header.Rows.Single(row => row.Key == "status").EditorText = "edited in the header";
            Assert.True(header.AnyRowDirty, "premise: the header's draft is not dirty");

            ahead.SetResult();
            h.PumpUntil(() => !sheet.WorkInFlight && h.Workspace.SavesIdle, "the apply's turn");
            _ = Assert.Single(h.Events, e => e is A11yEvent.HostComposed composed
                && composed.Text == PropertyPhrase.BulkRenameDirtyDraftsReason);
            Assert.DoesNotContain(h.Events, e => e is A11yEvent.RenameSummary or A11yEvent.RenameFailed);
            Assert.Equal(Note, h.Read("a.md"));
            Assert.True(held.Result);
        }
        finally
        {
            _ = ahead.TrySetResult();
            h.ReleaseAndDrain();
        }
    });

    /// <summary>Design 2 (CB-9a Design 8). The apply's turn throws before its
    /// worker starts: the reader hears the existing RenameFailed once, the
    /// sheet is no longer in flight, a close asked meanwhile settles, and the
    /// notes' chains are free. Red by mutation: drop the turn's catch.</summary>
    [Fact]
    public void AnApplyWhoseTurnThrowsBeforeItsWorkerStartsEndsAndFreesTheChain() => RunSta(() =>
    {
        using var h = new Vault("apply-turn-throws", ("a.md", Note));
        BulkRenameViewModel sheet = ArmedBulkRename(h);
        int settled = 0;
        sheet.CloseSettled += () => settled++;
        var ahead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool> held = h.Workspace.SavesForTests.Admit("a.md", _ => ahead.Task);
        h.Workspace.BulkRenameApplyTurnHookForTests = () => throw new InvalidOperationException("the apply's turn failed (test seam)");
        try
        {
            Assert.True(sheet.Apply(), "premise: the apply was refused");
            Assert.Equal(2, h.Workspace.SavesForTests.AdmittedForTests);
            sheet.RequestClose();

            ahead.SetResult();
            h.PumpUntil(() => settled > 0 && h.Workspace.SavesIdle, "the apply's end");
            _ = Assert.Single(h.Events, e => e is A11yEvent.RenameFailed);
            Assert.DoesNotContain(h.Events, e => e is A11yEvent.RenameSummary);
            Assert.Equal(1, settled);
            Assert.False(sheet.WorkInFlight);
            Assert.Equal(Note, h.Read("a.md"));
            AssertALaterOperationRuns(h, "a.md");
        }
        finally
        {
            h.Workspace.BulkRenameApplyTurnHookForTests = null;
            _ = ahead.TrySetResult();
            h.ReleaseAndDrain();
        }
    });

    /// <summary>Design 2 (CB-9a Design 8). The apply's worker faults outside
    /// its catch, on one of the three faults the catch leaves alone, so its
    /// body reports nothing. The apply still ends, silently, as one that did
    /// not run: the sheet is no longer in flight and the notes' chains are
    /// free. Red by mutation: remove the backstop.</summary>
    [Fact]
    public void AnApplyWhoseWorkerFaultsOutsideItsCatchStillEndsAndFreesTheChain() => RunSta(() =>
    {
        using var h = new Vault("apply-worker-faults", ("a.md", Note));
        BulkRenameViewModel sheet = ArmedBulkRename(h);
        sheet.ApplyHookForTests = () => throw new OutOfMemoryException("the apply's worker faulted (test seam)");
        try
        {
            Assert.True(sheet.Apply(), "premise: the apply was refused");
            h.PumpUntil(() => !sheet.WorkInFlight && h.Workspace.SavesIdle, "the faulted apply's end");
            Assert.DoesNotContain(h.Events, e => e is A11yEvent.RenameSummary or A11yEvent.RenameFailed);
            Assert.Equal(Note, h.Read("a.md"));
            AssertALaterOperationRuns(h, "a.md");
        }
        finally
        {
            sheet.ApplyHookForTests = null;
            h.ReleaseAndDrain();
        }
    });

    /// <summary>Design 2. The announcer throws at the apply's summary: the
    /// reconcile before it still ran (the clean tab shows the renamed key),
    /// the sheet's own publication after it still ran (no longer in flight),
    /// and the notes' chains are free. Red by mutation: drop RunEvery.</summary>
    [Fact]
    public void AnApplyEndsEvenWhenItsPublicationThrows() => RunSta(() =>
    {
        using var h = new Vault("apply-publication-throws", ("a.md", Note));
        WorkspaceTabViewModel tab = h.OpenNow("a.md");
        BulkRenameViewModel sheet = ArmedBulkRename(h);
        h.BeforeAnnounce = spoken =>
        {
            if (spoken is A11yEvent.RenameSummary { Applied: true })
            {
                throw new InvalidOperationException("the announcer failed (test seam)");
            }
        };
        try
        {
            Assert.True(sheet.Apply(), "premise: the apply was refused");
            h.PumpUntil(() => !sheet.WorkInFlight && h.Workspace.SavesIdle, "the apply's end");
            Assert.Contains("state: draft", tab.Text, StringComparison.Ordinal);
            Assert.False(tab.IsDirty);
            Assert.Equal(h.Read("a.md"), tab.Text);
            AssertALaterOperationRuns(h, "a.md");
        }
        finally
        {
            h.BeforeAnnounce = null;
            h.ReleaseAndDrain();
        }
    });

    /// <summary>The lead's ruling 4 (2026-11-17). The leftover race of
    /// admitting the preview's candidate notes: a note gains the key after the
    /// preview, so the apply does not wait for it. A save of that note is held
    /// before its write, on the hash of the bytes the apply then reads and
    /// renames. The two meet on the note and end in one refused write: the
    /// save is refused with its conflict sentence, the note holds exactly the
    /// apply's bytes, and the edit stays in the editor. Nothing is silently
    /// overwritten. Red by mutation: the save writes with no expected hash.
    /// The mutation the ruling names, the apply skipping its own compare,
    /// needs a core seam between the apply's read and its write, which no
    /// headless fact has; in this order the save's compare is the one that
    /// stands between the two writers.</summary>
    [Fact]
    public void TheLeftoverRaceEndsInOneRefusedWriteAndNoOverwrite() => RunSta(() =>
    {
        const string Gained = "---\nstatus: gained after the preview\n---\n\nC.\n";
        const string Second = Gained + "A second edit, held before its write.\n";
        using var h = new Vault("leftover-race", ("a.md", Note), ("c.md", "# C\n\nNo key yet.\n"));
        WorkspaceTabViewModel c = h.OpenNow("c.md");
        BulkRenameViewModel sheet = ArmedBulkRename(h);
        Assert.DoesNotContain(sheet.Rows, row => row.Path == "c.md");
        c.Text = Gained;
        SaveNow(h, c);
        var held = new HeldWork(hook => c.SaveWriteHookForTests = hook);
        try
        {
            c.Text = Second;
            Task<bool> save = c.SaveAsync(announce: true);
            h.PumpUntil(() => held.Entered, "premise: the second save reaching its hold before its write");
            h.Events.Clear();

            Assert.True(sheet.Apply(), "premise: the apply was refused");
            h.PumpUntil(
                () => !sheet.WorkInFlight && h.Events.Any(e => e is A11yEvent.RenameSummary),
                "the apply, beside the held save of a note its preview did not list");
            string renamed = h.Read("c.md");
            Assert.Contains("state: gained after the preview", renamed, StringComparison.Ordinal);

            held.Release();
            h.PumpUntil(() => save.IsCompleted && h.Workspace.SavesIdle, "the held save");
            Assert.False(save.Result, "the save over the bytes the apply wrote was not refused");
            _ = Assert.Single(h.Events, e => e is A11yEvent.NoteSaveConflict);
            Assert.Equal(renamed, h.Read("c.md"));
            Assert.True(c.IsDirty, "the refused edit left the editor");
            Assert.Equal(Second, c.Text);
        }
        finally
        {
            h.ReleaseAndDrain(held);
        }
        held.AssertHeldUntilReleased();
    });

    // ---- Teardown never waits for a whole apply (plan 47 CB-9b, Design 3) ----

    /// <summary>Design 3 (Call 3; the red team's finding 11). Teardown's
    /// settle cancels every admitted apply before it pumps: the sheet's own;
    /// one of a sheet the headless open replaced; and one admitted inside the
    /// settle's own frame, cancelled at admission. Each apply's core call
    /// then runs with its token cancelled and stops before its first file,
    /// so the reader hears "0 renamed, 0 skipped, 1 failed." before the
    /// settle returns true, and nothing is written.</summary>
    [Theory]
    [InlineData("the sheet's own apply")]
    [InlineData("an apply of a sheet the headless open replaced")]
    [InlineData("an apply admitted inside the settle's frame")]
    public void SettlingSavesCancelsEveryAdmittedApplyAndReturnsAfterItsPublication(string which) => RunSta(() =>
    {
        using var h = new Vault("settle-cancels-applies", ("a.md", Note));
        BulkRenameViewModel sheet = ArmedBulkRename(h);
        Dispatcher dispatcher = h.Workspace.SavesForTests.Dispatcher;
        bool? cancelled = null;
        HeldWork? held = null;
        var ahead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            if (which == "an apply admitted inside the settle's frame")
            {
                // Something for the settle to wait on, so it pumps; the apply
                // is asked inside that frame.
                _ = h.Workspace.SavesForTests.Admit("b.md", _ => ahead.Task);
                _ = dispatcher.BeginInvoke(new Action(() =>
                {
                    Assert.True(sheet.Apply(), "premise: the apply was refused");
                    cancelled = sheet.InFlightCancelForTests!.IsCancelled();
                    ahead.SetResult();
                }));
            }
            else
            {
                held = new HeldWork(hook => sheet.ApplyHookForTests = hook);
                Assert.True(sheet.Apply(), "premise: the apply was refused");
                h.PumpUntil(() => held.Entered, "premise: the apply reaching its hold before its core call");
                CancelToken token = sheet.InFlightCancelForTests!;
                if (which == "an apply of a sheet the headless open replaced")
                {
                    h.Workspace.OpenBulkRenameSheet();
                    Assert.NotSame(sheet, h.Workspace.BulkRenameSheet);
                }
                HeldWork hold = held;
                _ = dispatcher.BeginInvoke(new Action(() =>
                {
                    cancelled = token.IsCancelled();
                    hold.Release();
                }));
            }

            Assert.True(h.Workspace.SettleSaves(), "the settle did not finish");
            Assert.True(cancelled == true, $"the apply's token was not cancelled before its core call ran (cancelled: {cancelled})");
            A11yEvent.RenameSummary summary = Assert.Single(h.Events.OfType<A11yEvent.RenameSummary>());
            Assert.Equal(new A11yEvent.RenameSummary(Applied: true, Renamed: 0, Skipped: 0, Failed: 1), summary);
            Assert.Equal(Note, h.Read("a.md"));
            Assert.True(h.Workspace.SavesIdle);
        }
        finally
        {
            _ = ahead.TrySetResult();
            h.ReleaseAndDrain([.. held is null ? Array.Empty<HeldWork>() : [held]]);
        }
        held?.AssertHeldUntilReleased();
    });

    /// <summary>Design 3 (contract 35 A-1; M5). The workspace is disposed
    /// with no settle while the apply is held on its worker: its token is
    /// cancelled before the join, the join holds the worker
    /// (<c>BeforeJoinForTests</c> sees one tracked worker), and the worker,
    /// let go there, gets core's report for a cancelled run on a session
    /// still open, which its publication speaks. Nothing is written.</summary>
    [Fact]
    public void TeardownCancelsARunningApplyBeforeItJoinsItsWorker() => RunSta(() =>
    {
        using var h = new Vault("teardown-cancels-apply", ("a.md", Note));
        BulkRenameViewModel sheet = ArmedBulkRename(h);
        var held = new HeldWork(hook => sheet.ApplyHookForTests = hook);
        int tracked = -1;
        bool cancelled = false;
        try
        {
            Assert.True(sheet.Apply(), "premise: the apply was refused");
            h.PumpUntil(() => held.Entered, "premise: the apply reaching its hold before its core call");
            CancelToken token = sheet.InFlightCancelForTests!;
            h.Workspace.SavesForTests.BeforeJoinForTests = () =>
            {
                tracked = h.Workspace.SavesForTests.TrackedWorkersForTests;
                cancelled = token.IsCancelled();
                held.Release();
            };

            h.DisposeWorkspace();
            Assert.Equal(1, tracked);
            Assert.True(cancelled, "teardown joined the apply's worker before it cancelled the apply");
            h.PumpUntil(() => h.Events.Any(e => e is A11yEvent.RenameSummary or A11yEvent.RenameFailed), "the apply's publication");
            Assert.Equal(
                new A11yEvent.RenameSummary(Applied: true, Renamed: 0, Skipped: 0, Failed: 1),
                Assert.Single(h.Events.OfType<A11yEvent.RenameSummary>()));
            Assert.DoesNotContain(h.Events, e => e is A11yEvent.RenameFailed);
            Assert.Equal(Note, h.Read("a.md"));
        }
        finally
        {
            h.ReleaseAndDrain(held);
        }
        held.AssertHeldUntilReleased();
    });

    /// <summary>Design 3 (the red team's finding 10). A preview over 300
    /// notes is running on the sheet's worker when the workspace is disposed:
    /// teardown drains it before it returns, so the session the lifecycle
    /// disposes next is never under it. The preview is held on its worker and
    /// let go as the join begins; Dispose returns only once the preview's
    /// work has ended. Without the drain, Dispose returns while the preview
    /// still reads the vault (a race the preview's length decides, so this
    /// fact's red depends on it).</summary>
    [Fact]
    public void TeardownDrainsARunningPreviewBeforeTheSessionGoes() => RunSta(() =>
    {
        (string Path, string Text)[] notes = [.. Enumerable.Range(0, 300)
            .Select(n => ($"n{n:D3}.md", $"---\nstatus: draft\n---\n\nNote {n}.\n"))];
        using var h = new Vault("teardown-drains-preview", notes);
        h.Workspace.OpenBulkRenameSheet();
        BulkRenameViewModel sheet = h.Workspace.BulkRenameSheet!;
        sheet.OldKey = "status";
        sheet.NewKey = "state";
        var held = new HeldWork(hook => sheet.PreviewHookForTests = hook);
        try
        {
            sheet.Preview();
            h.PumpUntil(() => held.Entered, "premise: the preview reaching its hold before its core call");
            // Teardown cancels the sheet's run, which would stop the preview
            // at its first note; the preview keeps its own token here, so it
            // reads the whole vault after the join lets it go.
            using var keep = new CancelToken();
            sheet.SetInFlightCancelForTests(keep);
            h.Workspace.SavesForTests.BeforeJoinForTests = held.Release;

            h.DisposeWorkspace();
            Assert.True(sheet.WhenWorkDrained().IsCompleted, "Dispose returned while the preview's worker still ran");
        }
        finally
        {
            h.ReleaseAndDrain(held);
        }
        held.AssertHeldUntilReleased();
    });

    /// <summary>Design 3 (Call 3; M6's shape). Close Vault while an apply
    /// over 300 notes is inside core's loop, on the real lifecycle: the
    /// close's settle cancels the apply, waits for at most one file's write
    /// and the apply's publication, and the reader hears the apply's summary
    /// before the close's sentence. The apply's worker has ended by the time
    /// the close returns.</summary>
    [Fact]
    public void CloseVaultDuringAnApplySpeaksItsSummaryBeforeTheVaultClosedSentence()
    {
        using var host = new PumpedSaveReentrancyTests.Host();
        for (int n = 0; n < 300; n++)
        {
            File.WriteAllText(Path.Combine(host.Root, $"p{n:D3}.md"), $"---\nstatus: draft\n---\n\nNote {n}.\n");
        }
        Task rescan = host.Lifecycle.RescanAsync(RescanReason.Explicit);
        Assert.True(PumpedDispatcher.PumpUntil(() => rescan.IsCompleted, TimeSpan.FromSeconds(120)), "premise: the rescan never finished");
        host.Workspace.OpenBulkRenameSheet(synchronousForTests: false);
        BulkRenameViewModel sheet = Assert.IsType<BulkRenameViewModel>(host.Workspace.BulkRenameSheet);
        sheet.OldKey = "status";
        sheet.NewKey = "state";
        sheet.Preview();
        Assert.True(PumpedDispatcher.PumpUntil(() => sheet.CanApply, TimeSpan.FromSeconds(60)), "premise: the preview never armed the apply");
        bool entered = false;
        sheet.ApplyHookForTests = () => Volatile.Write(ref entered, true);
        host.Announced.Clear();
        Assert.True(sheet.Apply(), "premise: the apply was refused");
        Assert.True(
            PumpedDispatcher.PumpUntil(() => Volatile.Read(ref entered), TimeSpan.FromSeconds(10)),
            "premise: the apply's worker never started");

        host.Lifecycle.CloseVault();
        Assert.True(host.Lifecycle.Workspace is null, $"the vault stayed open: {host.Lifecycle.StatusText}");
        Assert.True(sheet.WhenWorkDrained().IsCompleted, "the close returned while the apply's worker still ran");
        A11yEvent.RenameSummary spoken = Assert.Single(host.Announced.Select(item => item.Event).OfType<A11yEvent.RenameSummary>());
        Assert.True(
            spoken.Failed > 0 && spoken.Renamed + spoken.Failed == 300,
            $"the close waited for the whole apply instead of cancelling it: {spoken.Renamed} renamed, {spoken.Failed} failed");
        string heard = string.Join(" | ", host.Announced.Select(item => item.Event.GetType().Name));
        int summary = host.Announced.FindIndex(item => item.Event is A11yEvent.RenameSummary { Applied: true });
        int closed = host.Announced.FindIndex(item => item.Event
            is A11yEvent.VaultClosed or A11yEvent.VaultClosedAllSaved or A11yEvent.VaultClosedChangesDiscarded);
        Assert.True(summary >= 0 && closed > summary, $"expected the apply's summary and then the close's sentence; heard {heard}");
    }

    // ---- #1955's CB-9a items 1 and 3 (plan 47 CB-9b, Design 8) ----------------

    /// <summary>Design 8, #1955's CB-9a item 3 (all three parts). A property
    /// write from A's header is held on its worker; B, A's split peer, takes
    /// an unsaved edit, so both tabs are dirty when the write lands. The
    /// announcer throws at the property sentence a dirty tab is told, or at
    /// the failure sentence when the write lands and then faults. The fan-out
    /// still reaches every tab on the note (each is marked stale), and the
    /// header refresh and the rest of the completion still run; after a
    /// failure, the read-back reconcile still brings the clean tabs to the
    /// landed bytes. Red today: the first sentence that throws skips every
    /// step after it.</summary>
    [Theory]
    [InlineData("set", "the dirty tab's sentence")]
    [InlineData("add", "the dirty tab's sentence")]
    [InlineData("set", "the failure's sentence")]
    [InlineData("add", "the failure's sentence")]
    public void APropertyWriteCompletionRunsEveryStepWhenASentenceThrows(string write, string sentence) => RunSta(() =>
    {
        using var peers = new Peers("completion-steps");
        bool failure = sentence == "the failure's sentence";
        var held = new HeldWork(hook =>
        {
            peers.A.PropertyWriteHookForTests = hook;
            peers.B.PropertyWriteHookForTests = hook;
        });
        if (failure)
        {
            peers.A.PropertyWriteFaultForTests = () => throw new InvalidOperationException("the write landed and then failed (test seam)");
            peers.B.PropertyWriteFaultForTests = peers.A.PropertyWriteFaultForTests;
        }
        peers.H.BeforeAnnounce = spoken =>
        {
            if (spoken is A11yEvent.PropertyEditFailed
                || (spoken is A11yEvent.HostComposed composed
                    && composed.Text.StartsWith("Properties changed on disk", StringComparison.Ordinal)))
            {
                throw new InvalidOperationException("the announcer failed (test seam)");
            }
        };
        try
        {
            if (write == "set")
            {
                PropertyRowViewModel status = peers.Row("status");
                status.EditorText = "done";
                Assert.True(peers.H.Workspace.SetPanelProperty(status));
            }
            else
            {
                peers.ActivateA();
                peers.H.Workspace.OpenAddPropertySheet();
                AddPropertyViewModel sheet = peers.H.Workspace.AddPropertySheet!;
                sheet.Key = "owner";
                sheet.Value = "alice";
                Assert.True(sheet.Add(), "premise: the add was refused at commit");
            }
            peers.H.PumpUntil(() => held.Entered, "premise: the write reaching its hold before its core call");
            if (!failure)
            {
                peers.B.Text = NoteEdited;
                Assert.True(peers.A.IsDirty && peers.B.IsDirty, "premise: the peers are not both dirty");
            }

            held.Release();
            peers.H.PumpUntil(
                () => peers.H.Workspace.SavesIdle && !peers.H.Workspace.PropertyWriteInFlightFor("a.md"),
                "the write's completion");
            string disk = peers.H.Read("a.md");
            Assert.Contains(write == "set" ? "status: done" : "owner: alice", disk, StringComparison.Ordinal);
            if (failure)
            {
                // The read-back reconcile ran after the failure's sentence.
                foreach (WorkspaceTabViewModel tab in new[] { peers.A, peers.B })
                {
                    Assert.Equal(disk, tab.Text);
                    Assert.False(tab.IsDirty);
                    Assert.False(tab.IsExternallyStale);
                }
            }
            else
            {
                // The fan-out reached the tab after the one whose sentence threw.
                Assert.True(peers.A.IsExternallyStale, "the first tab on the note was not marked stale");
                Assert.True(peers.B.IsExternallyStale, "the fan-out stopped at the tab whose sentence threw");
                _ = Assert.Single(peers.H.Events, e => e is A11yEvent.PropertyChanged);
            }
            string hash = peers.H.Session.ReadNoteParts("a.md").ContentHash;
            peers.H.PumpUntil(
                () => !peers.Properties.IsLoading && peers.Properties.ContentHash == hash,
                "the header's refresh to the written bytes");
        }
        finally
        {
            peers.H.BeforeAnnounce = null;
            peers.A.PropertyWriteFaultForTests = null;
            peers.B.PropertyWriteFaultForTests = null;
            peers.H.ReleaseAndDrain(held);
        }
        held.AssertHeldUntilReleased();
    });

    /// <summary>Design 8, #1955's CB-9a item 1. A property write's worker
    /// faults outside its catch, on one of the three faults the catch leaves
    /// alone, so it posts no completion. The write still ends: the note's
    /// chain goes idle, the note's write lease is released, the row is no
    /// longer in flight (a set) and the sheet hears the add did not land (an
    /// add). Nothing is written and nothing new is spoken. Red today: the
    /// wait for the end runs out.</summary>
    [Theory]
    [InlineData("set")]
    [InlineData("add")]
    public void APropertyWriteWhoseWorkerFaultsOutsideItsCatchStillEnds(string write) => RunSta(() =>
    {
        using var peers = new Peers("property-worker-faults");
        Action fault = () => throw new OutOfMemoryException("the property write's worker faulted (test seam)");
        peers.A.PropertyWriteHookForTests = fault;
        peers.B.PropertyWriteHookForTests = fault;
        PropertyRowViewModel status = peers.Row("status");
        AddPropertyViewModel? sheet = null;
        try
        {
            if (write == "set")
            {
                status.EditorText = "done";
                Assert.True(peers.H.Workspace.SetPanelProperty(status));
            }
            else
            {
                peers.ActivateA();
                peers.H.Workspace.OpenAddPropertySheet();
                sheet = peers.H.Workspace.AddPropertySheet!;
                sheet.Key = "owner";
                sheet.Value = "alice";
                Assert.True(sheet.Add(), "premise: the add was refused at commit");
            }
            peers.H.PumpUntil(
                () => peers.H.Workspace.SavesIdle && !peers.H.Workspace.PropertyWriteInFlightFor("a.md"),
                "the faulted write's end");
            Assert.Equal(Note, peers.H.Read("a.md"));
            Assert.False(status.WriteInFlight);
            Assert.DoesNotContain(peers.H.Events, e => e is A11yEvent.PropertyChanged);
            AssertALaterOperationRuns(peers.H, "a.md");
        }
        finally
        {
            peers.A.PropertyWriteHookForTests = null;
            peers.B.PropertyWriteHookForTests = null;
            peers.H.ReleaseAndDrain();
        }
    });

    // ---- A tag edit in the notes' chains (plan 47 CB-9b, Design 4) ------------

    /// <summary>Design 4 (CD5(a); M3). A save of an open note is admitted and
    /// held before its write; the reader adds a tag to the note from the
    /// Files sidebar. The tag edit takes a ticket behind the save and writes
    /// nothing while the save is held. After release the save publishes
    /// first, and the tag edit tags the saved bytes: no conflict, and the tab
    /// ends clean and current. Red today: the tag edit runs at the press and
    /// takes no ticket, and the save is refused.</summary>
    [Fact]
    public void ATagEditWaitsForAnOpenNotesAdmittedSaveAndTagsTheSavedBytes() => RunSta(() =>
    {
        using var h = new Vault("tag-edit-behind-save", ("a.md", Note));
        WorkspaceTabViewModel tab = h.OpenNow("a.md");
        FilesSidebarViewModel sidebar = h.Sidebar();
        CheckForATagEdit(h, sidebar, "a.md");
        var held = new HeldWork(hook => tab.SaveWriteHookForTests = hook);
        try
        {
            tab.Text = NoteEdited;
            Task<bool> save = tab.SaveAsync(announce: true);
            h.PumpUntil(() => held.Entered, "premise: the save reaching its hold before its write");
            h.Events.Clear();

            sidebar.AddTagCommand.Execute(null);
            Assert.Equal(2, h.Workspace.SavesForTests.AdmittedForTests);
            PumpedDispatcher.Drain();
            Assert.Equal(Note, h.Read("a.md"));

            held.Release();
            h.PumpUntil(
                () => save.IsCompleted && h.Workspace.SavesIdle && TagSummaries(h) > 0,
                "the save and then the tag edit");
            Assert.True(save.Result, "the save admitted before the tag edit was refused");
            int saved = h.Events.FindIndex(e => e is A11yEvent.NoteSaved);
            int summary = h.Events.FindIndex(IsTagSummary);
            Assert.True(saved >= 0 && summary > saved, $"expected the save's sentence and then the tag edit's; heard {Heard(h)}");
            Assert.DoesNotContain(h.Events, e => e is A11yEvent.NoteSaveConflict);
            string written = h.Read("a.md");
            Assert.Contains("probe", written, StringComparison.Ordinal);
            Assert.Contains("An edit on its way to disk.", written, StringComparison.Ordinal);
            Assert.Equal(written, tab.Text);
            Assert.False(tab.IsDirty);
            Assert.False(tab.IsExternallyStale);
        }
        finally
        {
            h.ReleaseAndDrain(held);
        }
        held.AssertHeldUntilReleased();
    });

    /// <summary>Design 4 (M4). Two checked notes with clean tabs and an
    /// unchecked one with a clean tab. Each tagged tab is brought up to the
    /// tagged bytes at the index's hash, with no undo step back across them;
    /// the unchecked one is untouched; and a later save from a tagged tab
    /// lands. Red today: the tagged tabs keep the bytes from before the tag
    /// edit, and the later save is refused.</summary>
    [Fact]
    public void ATagEditBringsACleanTabOnEachTaggedNoteUpToTheTaggedBytes() => RunSta(() =>
    {
        using var h = new Vault("tag-edit-clean-tabs", ("a.md", Note), ("b.md", "# B\n"), ("c.md", "# C\n"));
        WorkspaceTabViewModel a = h.OpenNow("a.md");
        WorkspaceTabViewModel b = h.OpenNow("b.md");
        WorkspaceTabViewModel c = h.OpenNow("c.md");
        FilesSidebarViewModel sidebar = h.Sidebar();
        CheckForATagEdit(h, sidebar, "a.md", "b.md");
        string? untouched = c.SavedContentHash;
        try
        {
            sidebar.AddTagCommand.Execute(null);
            h.PumpUntil(() => h.Workspace.SavesIdle && TagSummaries(h) > 0, "the tag edit");
            foreach (WorkspaceTabViewModel tagged in new[] { a, b })
            {
                string disk = h.Read(tagged.Path);
                Assert.Contains("probe", disk, StringComparison.Ordinal);
                Assert.Equal(disk, tagged.Text);
                Assert.Equal(IndexedHash(h, tagged.Path), tagged.SavedContentHash);
                Assert.False(tagged.IsDirty, $"{tagged.Path} is dirty after the tag edit");
                Assert.False(tagged.IsExternallyStale, $"{tagged.Path} is stale after the tag edit");
                Assert.False(tagged.EditorDocument!.UndoStack.CanUndo, $"{tagged.Path} can undo back across the tag edit");
            }
            Assert.Equal("# C\n", c.Text);
            Assert.Equal(untouched, c.SavedContentHash);

            a.Text = a.Text + "A later edit.\n";
            Task<bool> save = a.SaveAsync(announce: true);
            h.PumpUntil(() => save.IsCompleted && h.Workspace.SavesIdle, "a later save from a tagged tab");
            Assert.True(save.Result, "a later save from a tagged tab was refused");
            Assert.Equal(a.Text, h.Read("a.md"));
        }
        finally
        {
            h.ReleaseAndDrain();
        }
    });

    /// <summary>Design 4 (Call 2(a)). A dirty tab on a note the tag edit
    /// rewrites is marked stale with the property sentence, once, before the
    /// tag edit's summary, and keeps the reader's edit. Red today: it is told
    /// nothing and is not stale.</summary>
    [Fact]
    public void ATagEditMarksADirtyTabOnATaggedNoteStaleAndSaysSo() => RunSta(() =>
    {
        using var h = new Vault("tag-edit-dirty-tab", ("a.md", Note));
        WorkspaceTabViewModel tab = h.OpenNow("a.md");
        FilesSidebarViewModel sidebar = h.Sidebar();
        CheckForATagEdit(h, sidebar, "a.md");
        tab.Text = NoteEdited;
        h.Events.Clear();
        try
        {
            sidebar.AddTagCommand.Execute(null);
            h.PumpUntil(() => h.Workspace.SavesIdle && TagSummaries(h) > 0, "the tag edit");
            _ = Assert.Single(h.Events, IsThePropertySentence);
            int stale = h.Events.FindIndex(IsThePropertySentence);
            int summary = h.Events.FindIndex(IsTagSummary);
            Assert.True(summary > stale, $"expected the property sentence and then the tag edit's summary; heard {Heard(h)}");
            Assert.True(tab.IsExternallyStale, "the dirty tab on the tagged note is not stale");
            Assert.True(tab.IsDirty);
            Assert.Equal(NoteEdited, tab.Text);
            string disk = h.Read("a.md");
            Assert.Contains("probe", disk, StringComparison.Ordinal);
            Assert.DoesNotContain("An edit on its way to disk.", disk, StringComparison.Ordinal);
        }
        finally
        {
            h.ReleaseAndDrain();
        }
    });

    /// <summary>Design 4 (M15). Core skips a checked note changed outside
    /// Slate since it was indexed, so the index's hash of that note does not
    /// move, and its clean tab is not reloaded: the before-and-after reads,
    /// not a read of the disk, decide which tabs the edit wrote. The summary
    /// counts only the files tagged. Green today, where no tab is reconciled
    /// at all; red by mutation: reconcile every checked note's tabs from a
    /// read of the disk.</summary>
    [Fact]
    public void ATagEditCoreSkippedLeavesItsCleanTabAlone() => RunSta(() =>
    {
        const string Outside = "---\nstatus: changed outside Slate\n---\n\nBody.\n";
        using var h = new Vault("tag-edit-core-skipped", ("a.md", Note), ("b.md", "# B\n"));
        WorkspaceTabViewModel a = h.OpenNow("a.md");
        WorkspaceTabViewModel b = h.OpenNow("b.md");
        FilesSidebarViewModel sidebar = h.Sidebar();
        CheckForATagEdit(h, sidebar, "a.md", "b.md");
        File.WriteAllText(Path.Combine(h.Root, "a.md"), Outside);
        string indexedBefore = IndexedHash(h, "a.md");
        string? saved = a.SavedContentHash;
        try
        {
            sidebar.AddTagCommand.Execute(null);
            h.PumpUntil(() => h.Workspace.SavesIdle && TagSummaries(h) > 0, "the tag edit");
            Assert.Equal(Outside, h.Read("a.md"));
            Assert.Equal(indexedBefore, IndexedHash(h, "a.md"));
            Assert.Equal(Note, a.Text);
            Assert.Equal(saved, a.SavedContentHash);
            Assert.False(a.IsExternallyStale);
            A11yEvent.HostComposed summary = Assert.IsType<A11yEvent.HostComposed>(Assert.Single(h.Events, IsTagSummary));
            Assert.Equal("Tagged 1 file with #probe.", summary.Text);
        }
        finally
        {
            h.ReleaseAndDrain();
        }
    });

    /// <summary>Design 4 (M14). A tag edit over 1,025 checked notes, more than
    /// one index lookup takes, three of them open in tabs: the two written
    /// are brought up to the tagged bytes, the one changed outside Slate is
    /// left alone, and the summary is spoken. The reads are made for the
    /// notes with an open tab only, in slices of core's bound. Red by
    /// mutation: one unsliced read over every checked note, which core
    /// refuses, so the open tabs are reconciled from the disk instead and the
    /// skipped note's tab is reloaded.</summary>
    [Fact]
    public void ATagEditOverMoreNotesThanOneIndexLookupTakesStillReconcilesAndSpeaks() => RunSta(() =>
    {
        const string Outside = "# Changed outside Slate\n";
        const int Count = 1_025;
        (string Path, string Text)[] files = [.. Enumerable.Range(0, Count).Select(n => ($"n{n:D4}.md", $"# Note {n}\n"))];
        Assert.True(Count > SlateUniffiMethods.MaxIndexedHashPaths(), "premise: the notes fit one index lookup");
        using var h = new Vault("tag-edit-past-one-lookup", files);
        WorkspaceTabViewModel first = h.OpenNow("n0000.md");
        WorkspaceTabViewModel last = h.OpenNow($"n{Count - 1:D4}.md");
        WorkspaceTabViewModel skipped = h.OpenNow("n0500.md");
        FilesSidebarViewModel sidebar = h.Sidebar();
        CheckForATagEdit(h, sidebar, [.. files.Select(file => file.Path)]);
        File.WriteAllText(Path.Combine(h.Root, "n0500.md"), Outside);
        try
        {
            sidebar.AddTagCommand.Execute(null);
            h.PumpUntil(() => h.Workspace.SavesIdle && TagSummaries(h) > 0, "the tag edit over every note");
            foreach (WorkspaceTabViewModel tagged in new[] { first, last })
            {
                string disk = h.Read(tagged.Path);
                Assert.Contains("probe", disk, StringComparison.Ordinal);
                Assert.Equal(disk, tagged.Text);
                Assert.False(tagged.IsDirty);
            }
            Assert.Equal("# Note 500\n", skipped.Text);
            Assert.Equal(Outside, h.Read("n0500.md"));
            A11yEvent.HostComposed summary = Assert.IsType<A11yEvent.HostComposed>(Assert.Single(h.Events, IsTagSummary));
            Assert.Equal("Tagged 1,024 files with #probe.", summary.Text);
        }
        finally
        {
            h.ReleaseAndDrain();
        }
    });

    /// <summary>Design 4 (the red team's finding 4). The index read after the
    /// core call fails: the open tabs on the checked notes are reconciled
    /// from a read of the disk instead, core's summary is still spoken, and
    /// the note's chain is free. Red today: no tab is reconciled. Red by
    /// mutation: let the failed read end the operation.</summary>
    [Fact]
    public void ATagEditWhoseIndexReadFailsStillReconcilesByReadAndSpeaks() => RunSta(() =>
    {
        using var h = new Vault("tag-edit-index-read-fails", ("a.md", Note));
        WorkspaceTabViewModel tab = h.OpenNow("a.md");
        FilesSidebarViewModel sidebar = h.Sidebar();
        CheckForATagEdit(h, sidebar, "a.md");
        h.Workspace.TagEditIndexReadHookForTests = when =>
        {
            if (when == "after")
            {
                throw new InvalidOperationException("the index read failed (test seam)");
            }
        };
        try
        {
            sidebar.AddTagCommand.Execute(null);
            h.PumpUntil(() => h.Workspace.SavesIdle && TagSummaries(h) > 0, "the tag edit");
            _ = Assert.Single(h.Events, IsTagSummary);
            string disk = h.Read("a.md");
            Assert.Contains("probe", disk, StringComparison.Ordinal);
            Assert.Equal(disk, tab.Text);
            Assert.False(tab.IsDirty);
            Assert.False(tab.EditorDocument!.UndoStack.CanUndo, "the tab reconciled by a read can undo back across the tag edit");
            AssertALaterOperationRuns(h, "a.md");
        }
        finally
        {
            h.Workspace.TagEditIndexReadHookForTests = null;
            h.ReleaseAndDrain();
        }
    });

    /// <summary>Design 4 (Call 2(c)). The tag edit's core call throws
    /// something other than a VaultException: the reader hears "Tag edit
    /// failed: " and the message, once, nothing is written, and the note's
    /// chain is free. Red today: the exception escapes the command.</summary>
    [Fact]
    public void ATagEditWhoseCoreCallFaultsSaysTagEditFailedAndFreesTheChain() => RunSta(() =>
    {
        using var h = new Vault("tag-edit-core-faults", ("a.md", Note));
        FilesSidebarViewModel sidebar = h.Sidebar();
        CheckForATagEdit(h, sidebar, "a.md");
        sidebar.TagEditHookForTests = () => throw new InvalidOperationException("the tag edit failed (test seam)");
        try
        {
            Exception? escaped = Record.Exception(() => sidebar.AddTagCommand.Execute(null));
            Assert.Null(escaped);
            h.PumpUntil(() => h.Workspace.SavesIdle, "the failed tag edit's end");
            _ = Assert.Single(h.Events, e => e is A11yEvent.HostComposed composed
                && composed.Text == "Tag edit failed: the tag edit failed (test seam)");
            Assert.Equal(0, TagSummaries(h));
            Assert.Equal(Note, h.Read("a.md"));
            AssertALaterOperationRuns(h, "a.md");
        }
        finally
        {
            sidebar.TagEditHookForTests = null;
            h.ReleaseAndDrain();
        }
    });

    /// <summary>Design 4, CB-9a's first teardown shape. Close Vault's first
    /// step, settling the saves, waits for a tag edit queued behind an
    /// operation admitted for its note, and returns once the tag edit has
    /// written and spoken. The release is queued on the dispatcher, so it
    /// can only run inside the settle's own pumping. Red today: nothing is
    /// admitted.</summary>
    [Fact]
    public void SettlingSavesWaitsForAQueuedTagEditAndReturns() => RunSta(() =>
    {
        using var h = new Vault("tag-edit-settle", ("a.md", Note));
        FilesSidebarViewModel sidebar = h.Sidebar();
        CheckForATagEdit(h, sidebar, "a.md");
        var ahead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool> held = h.Workspace.SavesForTests.Admit("a.md", _ => ahead.Task);
        try
        {
            sidebar.AddTagCommand.Execute(null);
            Assert.Equal(2, h.Workspace.SavesForTests.AdmittedForTests);
            Assert.Equal(Note, h.Read("a.md"));

            _ = Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() => ahead.SetResult()));
            Assert.True(h.Workspace.SettleSaves(), "the settle did not return true");
            Assert.Contains("probe", h.Read("a.md"), StringComparison.Ordinal);
            Assert.Equal(1, TagSummaries(h));
            Assert.True(h.Workspace.SavesIdle);
            Assert.True(held.Result);
        }
        finally
        {
            _ = ahead.TrySetResult();
            h.ReleaseAndDrain();
        }
    });

    /// <summary>Design 4, CB-9a's second teardown shape. A tag edit still
    /// waiting its turn when the workspace is torn down never runs: nothing
    /// is written or spoken. Red today: the tag edit wrote at the
    /// press.</summary>
    [Fact]
    public void ATagEditStillQueuedAtTeardownEndsUnwritten() => RunSta(() =>
    {
        using var h = new Vault("tag-edit-queued-at-teardown", ("a.md", Note));
        FilesSidebarViewModel sidebar = h.Sidebar();
        CheckForATagEdit(h, sidebar, "a.md");
        var ahead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool> held = h.Workspace.SavesForTests.Admit("a.md", _ => ahead.Task);
        try
        {
            sidebar.AddTagCommand.Execute(null);
            Assert.Equal(2, h.Workspace.SavesForTests.AdmittedForTests);

            h.DisposeWorkspace();
            ahead.SetResult();
            h.PumpUntil(() => h.Workspace.SavesForTests.IsIdle, "the queued tag edit ending after teardown");
            Assert.True(held.Result);
            Assert.Equal(Note, h.Read("a.md"));
            Assert.Equal(0, TagSummaries(h));
            Assert.DoesNotContain(h.Events, e => e is A11yEvent.HostComposed composed
                && composed.Text.StartsWith("Tag edit failed", StringComparison.Ordinal));
        }
        finally
        {
            _ = ahead.TrySetResult();
            h.ReleaseAndDrain();
        }
    });

    /// <summary>Checks <paramref name="paths"/> in the sidebar's tree for a
    /// tag edit of #probe, once each is listed, and drops what was
    /// spoken.</summary>
    private static void CheckForATagEdit(Vault h, FilesSidebarViewModel sidebar, params string[] paths)
    {
        HashSet<string> wanted = new(paths, StringComparer.Ordinal);
        h.PumpUntil(
            () => sidebar.RootNodes.Count(node => wanted.Contains(node.Path)) == wanted.Count,
            "premise: the sidebar listing the notes to tag");
        foreach (FileTreeNodeViewModel node in sidebar.RootNodes.Where(node => wanted.Contains(node.Path)))
        {
            node.IsBatchSelected = true;
        }
        sidebar.TagInput = "probe";
        h.Events.Clear();
    }

    private static bool IsTagSummary(A11yEvent spoken) =>
        spoken is A11yEvent.HostComposed composed && composed.Text.StartsWith("Tagged ", StringComparison.Ordinal);

    private static int TagSummaries(Vault h) => h.Events.Count(IsTagSummary);

    private static bool IsThePropertySentence(A11yEvent spoken) =>
        spoken is A11yEvent.HostComposed composed
        && composed.Text.StartsWith("Properties changed on disk", StringComparison.Ordinal);

    private static string IndexedHash(Vault h, string path)
    {
        using var cancel = new CancelToken();
        return h.Session.IndexedContentHashes([path], cancel)[0] ?? "<none>";
    }

    // ---- Clean tabs follow a link rewrite (plan 47 CB-9b, Design 5) -------------

    /// <summary>Design 5 (CD5(b); M9, M16). A note links to an item that a
    /// rename or a move from the Files sidebar renames or moves, and core
    /// rewrites the note's links. The note's tab is open and clean (for an
    /// undo or a redo, opened just before it, on the bytes it rewrites). The
    /// tab then shows the rewritten bytes at the index's hash, under the
    /// note's path after the change, is not stale, cannot undo back across
    /// them, nothing is spoken for it, and a later save from it lands. Red
    /// today: the tab keeps the bytes from before the rewrite, and the later
    /// save is refused.</summary>
    [Theory]
    [InlineData("rename")]
    [InlineData("folder-with-note rename")]
    [InlineData("a note inside a renamed folder")]
    [InlineData("move")]
    [InlineData("batch move")]
    [InlineData("undo of a rename")]
    [InlineData("redo of a move")]
    [InlineData("undo of a batch move")]
    public void ACleanTabOnANoteWhoseLinksAStructuralChangeRewroteShowsTheRewrittenBytes(string change) => RunSta(() =>
    {
        (string Path, string Text)[] files;
        string linking;
        string after;
        string expected;
        switch (change)
        {
            case "rename":
            case "undo of a rename":
                files = [("note3.md", "# Note 3\n\nSee [[note4]].\n"), ("note4.md", "# Note 4\n")];
                (linking, after) = ("note3.md", "note3.md");
                expected = change == "rename" ? "See [[renamed4]]." : "See [[note4]].";
                break;
            case "folder-with-note rename":
                // One note rewritten twice, chained (M16).
                files = [("F/F.md", "# Folder note\n"), ("a.md", "See [[F/F]] and [[F/F.md]].\n")];
                (linking, after, expected) = ("a.md", "a.md", "See [[G/G]] and [[G/G]].");
                break;
            case "a note inside a renamed folder":
                // Named in the report under its new path (M16).
                files = [("F/F.md", "# Folder note\n"), ("F/x.md", "Up [[F/F]].\n")];
                (linking, after, expected) = ("F/x.md", "G/x.md", "Up [[G/G]].");
                break;
            default:
                // A markdown link by its folder's path is rewritten when the
                // folder moves; a link by name alone still resolves and is
                // left as it is (core's no-churn rule).
                files = [("notes/target.md", "# Target\n"), ("y/keep.md", "# Keep\n"), ("linker.md", "See [t](notes/target.md).\n")];
                (linking, after) = ("linker.md", "linker.md");
                expected = change == "undo of a batch move" ? "See [t](notes/target.md)." : "See [t](y/notes/target.md).";
                break;
        }
        using var h = new Vault("link-rewrite", files);
        FilesSidebarViewModel sidebar = h.Sidebar();
        WorkspaceTabViewModel? tab = change.StartsWith("undo", StringComparison.Ordinal)
            || change.StartsWith("redo", StringComparison.Ordinal)
                ? null
                : h.OpenNow(linking);
        switch (change)
        {
            case "rename":
                RenameInTheSidebar(h, sidebar, "note4.md", "renamed4.md");
                break;
            case "folder-with-note rename":
            case "a note inside a renamed folder":
                RenameInTheSidebar(h, sidebar, "F", "G");
                break;
            case "move":
                MoveInTheSidebar(h, sidebar, "notes", "y");
                break;
            case "batch move":
                BatchMoveInTheSidebar(h, sidebar, "notes", "y");
                break;
            case "undo of a rename":
                RenameInTheSidebar(h, sidebar, "note4.md", "renamed4.md");
                tab = h.OpenNow(linking);
                sidebar.UndoStructural();
                break;
            case "redo of a move":
                MoveInTheSidebar(h, sidebar, "notes", "y");
                sidebar.UndoStructural();
                tab = h.OpenNow(linking);
                sidebar.RedoStructural();
                break;
            default:
                Assert.Equal("undo of a batch move", change);
                BatchMoveInTheSidebar(h, sidebar, "notes", "y");
                tab = h.OpenNow(linking);
                sidebar.UndoStructural();
                break;
        }
        try
        {
            string disk = h.Read(after);
            Assert.Contains(expected, disk, StringComparison.Ordinal);
            Assert.Equal(after, tab!.Path);
            Assert.Equal(disk, tab.Text);
            Assert.Equal(IndexedHash(h, after), tab.SavedContentHash);
            Assert.False(tab.IsExternallyStale, "the tab on the rewritten note is stale");
            Assert.False(tab.IsDirty, "the tab on the rewritten note is dirty");
            Assert.False(tab.EditorDocument!.UndoStack.CanUndo, "the tab can undo back across the rewritten links");
            Assert.DoesNotContain(h.Events, e => IsThePropertySentence(e) || e is A11yEvent.NoteSaveConflict);

            tab.Text = tab.Text + "A later edit.\n";
            Task<bool> save = tab.SaveAsync(announce: true);
            h.PumpUntil(() => save.IsCompleted && h.Workspace.SavesIdle, "a later save from the tab");
            Assert.True(save.Result, "a later save from the tab on the rewritten note was refused");
            Assert.Equal(tab.Text, h.Read(after));
        }
        finally
        {
            h.ReleaseAndDrain();
        }
    });

    /// <summary>Design 5. A link rewrite leaves alone a dirty tab on the
    /// rewritten note (no reload, no sentence: the change event marks it
    /// stale), and a clean tab whose note has an operation admitted, which
    /// publishes to it itself. Green today, where nothing follows a link
    /// rewrite; red by mutation: drop the HasAdmittedSaveFor guard, and the
    /// second row reloads under the operation.</summary>
    [Theory]
    [InlineData("a dirty tab")]
    [InlineData("a clean tab under an admitted operation")]
    public void ALinkRewriteLeavesADirtyTabAndATabUnderAnAdmittedOperationAsTheyAre(string which) => RunSta(() =>
    {
        const string Linking = "# Note 3\n\nSee [[note4]].\n";
        const string Edited = Linking + "An unsaved edit.\n";
        using var h = new Vault("link-rewrite-left-alone", ("note3.md", Linking), ("note4.md", "# Note 4\n"));
        WorkspaceTabViewModel tab = h.OpenNow("note3.md");
        FilesSidebarViewModel sidebar = h.Sidebar();
        bool dirty = which == "a dirty tab";
        var ahead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool>? held = null;
        if (dirty)
        {
            tab.Text = Edited;
        }
        else
        {
            held = h.Workspace.SavesForTests.Admit("note3.md", _ => ahead.Task);
            Assert.False(tab.HasPendingSaves, "premise: the tab has a save of its own pending");
        }
        string? saved = tab.SavedContentHash;
        h.Events.Clear();
        try
        {
            RenameInTheSidebar(h, sidebar, "note4.md", "renamed4.md");
            Assert.Contains("See [[renamed4]].", h.Read("note3.md"), StringComparison.Ordinal);
            Assert.Equal(dirty ? Edited : Linking, tab.Text);
            Assert.Equal(saved, tab.SavedContentHash);
            Assert.Equal(dirty, tab.IsDirty);
            Assert.DoesNotContain(h.Events, IsThePropertySentence);
        }
        finally
        {
            _ = ahead.TrySetResult();
            h.ReleaseAndDrain();
        }
        Assert.True(held is null || held.Result);
    });

    /// <summary>Renames <paramref name="path"/>, a note or a folder listed at
    /// the vault's root, to <paramref name="newName"/> from the sidebar, as
    /// the rename box does.</summary>
    private static void RenameInTheSidebar(Vault h, FilesSidebarViewModel sidebar, string path, string newName)
    {
        h.PumpUntil(() => sidebar.RootNodes.Any(node => node.Path == path), $"premise: the sidebar listing {path}");
        sidebar.SelectedNode = sidebar.RootNodes.Single(node => node.Path == path);
        sidebar.MutationName = newName;
        Assert.True(sidebar.TryRenameSelected(), $"premise: the rename of {path} was refused");
    }

    /// <summary>Moves <paramref name="path"/>, a note or a folder listed at
    /// the vault's root, into <paramref name="destination"/> through the Move
    /// To sheet: the single-item route.</summary>
    private static void MoveInTheSidebar(Vault h, FilesSidebarViewModel sidebar, string path, string destination)
    {
        h.PumpUntil(() => sidebar.RootNodes.Any(node => node.Path == path), $"premise: the sidebar listing {path}");
        sidebar.SelectedNode = sidebar.RootNodes.Single(node => node.Path == path);
        sidebar.MoveToCommand.Execute(null);
        h.PumpUntil(
            () => sidebar.MoveToSheet is { IsReady: true } sheet && sheet.Rows.Any(row => row.Destination == destination),
            $"premise: the Move To sheet ready, listing {destination}");
        MoveToPickerViewModel picker = sidebar.MoveToSheet!;
        picker.ActivateCommand.Execute(picker.Rows.Single(row => row.Destination == destination));
        Assert.Null(sidebar.MoveToSheet);
    }

    /// <summary>Moves the checked <paramref name="path"/>, a note or a folder
    /// listed at the vault's root, into <paramref name="destination"/> as a
    /// batch.</summary>
    private static void BatchMoveInTheSidebar(Vault h, FilesSidebarViewModel sidebar, string path, string destination)
    {
        h.PumpUntil(() => sidebar.RootNodes.Any(node => node.Path == path), $"premise: the sidebar listing {path}");
        sidebar.RootNodes.Single(node => node.Path == path).IsBatchSelected = true;
        sidebar.MoveDestination = destination;
        sidebar.BatchMoveCommand.Execute(null);
    }

    /// <summary>The workspace's bulk rename sheet, opened and armed by a
    /// preview of <paramref name="oldKey"/> to <paramref name="newKey"/>,
    /// with what the preview spoke dropped.</summary>
    private static BulkRenameViewModel ArmedBulkRename(Vault h, string oldKey = "status", string newKey = "state")
    {
        h.Workspace.OpenBulkRenameSheet();
        BulkRenameViewModel sheet = h.Workspace.BulkRenameSheet!;
        sheet.OldKey = oldKey;
        sheet.NewKey = newKey;
        sheet.Preview();
        h.PumpUntil(() => sheet.CanApply, "premise: the preview arming the apply");
        h.Events.Clear();
        return sheet;
    }

    /// <summary>What was spoken, by event, for a failure message.</summary>
    private static string Heard(Vault h) => string.Join(" | ", h.Events.Select(e => e.GetType().Name));

    /// <summary>Another writer's write to a.md, asked with its tabs open: what
    /// it writes, and when it has landed.</summary>
    private static (string Expected, Func<bool> Landed) WriteThroughAnOpenTab(Vault h, string route)
    {
        Func<bool> propertyChanged = () => h.Events.Any(e => e is A11yEvent.PropertyChanged);
        if (route == "property set from the header")
        {
            PropertyRowViewModel status = LoadedHeader(h).Rows.Single(row => row.Key == "status");
            status.EditorText = "done";
            Assert.True(h.Workspace.SetPanelProperty(status));
            return ("status: done", propertyChanged);
        }
        if (route == "property delete")
        {
            Assert.True(h.Workspace.DeletePanelProperty(LoadedHeader(h).Rows.Single(row => row.Key == "title")));
            return ("status: draft\n---", propertyChanged);
        }
        if (route == "property add")
        {
            _ = LoadedHeader(h);
            h.Workspace.OpenAddPropertySheet();
            AddPropertyViewModel add = h.Workspace.AddPropertySheet!;
            add.Key = "owner";
            add.Value = "alice";
            Assert.True(add.Add(), "premise: the add was refused at commit");
            return ("owner: alice", propertyChanged);
        }
        if (route == "Bases cell edit through a tab")
        {
            (BaseDocumentViewModel document, BasesRow row, BasesColumn column) = StatusCellOf(h);
            document.ApplyPropertyEdit!(row, column, new PropertyValue.Text("done"));
            return ("status: done", () => h.Events.Any(e => e is A11yEvent.BasesCellSaved));
        }
        if (route == "tag edit")
        {
            FilesSidebarViewModel tagging = h.Sidebar();
            CheckForATagEdit(h, tagging, "a.md");
            tagging.AddTagCommand.Execute(null);
            return ("probe", () => TagSummaries(h) > 0);
        }
        if (route == "link rewrite (rename)")
        {
            RenameInTheSidebar(h, h.Sidebar(), "b.md", "c.md");
            return ("See [[c]].", () => true);
        }
        Assert.Equal("bulk rename", route);
        BulkRenameViewModel sheet = h.Workspace.CreateBulkRename();
        sheet.OldKey = "status";
        sheet.NewKey = "state";
        sheet.Preview();
        h.PumpUntil(() => sheet.CanApply, "premise: the preview arming the apply");
        Assert.True(sheet.Apply(), "premise: the apply was refused");
        return (
            "state: draft",
            () => !sheet.WorkInFlight && h.Events.Any(e => e is A11yEvent.RenameSummary { Applied: true }));
    }

    /// <summary>A second tab on <paramref name="path"/>'s note, in a split to
    /// the right, opened with one drained turn and no fixed wait.</summary>
    private static WorkspaceTabViewModel OpenSplitPeer(Vault h, string path)
    {
        WorkspaceTabViewModel first = h.Workspace.Groups.SelectMany(group => group.Tabs).Single(tab => tab.Path == path);
        h.Workspace.OpenPath(path, WorkspaceOpenTarget.SplitRight);
        PumpedDispatcher.Drain();
        WorkspaceTabViewModel peer = h.Workspace.ActiveGroup.ActiveTab!;
        Assert.NotSame(first, peer);
        Assert.Equal(path, peer.Path);
        Assert.Equal(first.Text, peer.Text);
        return peer;
    }

    /// <summary>The active tab's properties header, loaded.</summary>
    private static NotePropertiesViewModel LoadedHeader(Vault h)
    {
        NotePropertiesViewModel header = h.Workspace.EnsureActiveTabProperties()!;
        h.PumpUntil(() => header.Rows.Count > 0 && !header.IsLoading, "the header loading");
        h.Events.Clear();
        return header;
    }

    // ---- Harness ---------------------------------------------------------------

    /// <summary>A vault on disk and a workspace over it in the app's mode:
    /// interaction work in the background (a History restore on its worker,
    /// a properties header's read on the pool), published on this fact's
    /// dispatcher, which every wait here pumps.</summary>
    private sealed class Vault : IDisposable
    {
        private readonly FixtureVault _fixture;
        private readonly VaultSession _session;
        private readonly List<FilesSidebarViewModel> _sidebars = [];
        private bool _workspaceDisposed;

        public Vault(string label, params (string Path, string Text)[] files)
            : this(label, background: true, earlierSession: null, files)
        {
        }

        /// <summary>A vault whose note at <paramref name="path"/> was saved
        /// and then deleted in an earlier session. Core builds the deleted
        /// list when a session opens, so the note is listed, and can be
        /// recovered, in this one (the two-session shape of
        /// <c>HistoryPanelTests</c>).</summary>
        public static Vault WithADeletedNote(
            string label, string path, string text, bool background = true, params (string Path, string Text)[] files) =>
            new(
                label,
                background,
                earlier =>
                {
                    _ = earlier.SaveText(path, text, expectedContentHash: null);
                    // The delete goes through the system trash, whose COM
                    // apartment is a thread of its own.
                    StaThread.Run(() => earlier.DeleteFile(path));
                },
                files);

        /// <summary>A vault whose workspace runs its interaction work inline
        /// (a History create and its publication, on the calling thread). A
        /// tab's save still writes on a worker and publishes on the pumped
        /// dispatcher, so it can be held.</summary>
        public static Vault Inline(string label, params (string Path, string Text)[] files) =>
            new(label, background: false, earlierSession: null, files);

        private Vault(string label, bool background, Action<VaultSession>? earlierSession, (string Path, string Text)[] files)
        {
            _fixture = FixtureVault.Create(0, label);
            foreach ((string path, string text) in files)
            {
                string full = Path.Combine(_fixture.Root, path);
                Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                File.WriteAllText(full, text);
            }
            if (earlierSession is not null)
            {
                using VaultSession earlier = VaultSession.OpenFilesystem(_fixture.Root);
                using (var scan = new CancelToken())
                {
                    earlier.ScanInitial(scan);
                }
                earlierSession(earlier);
            }
            _session = VaultSession.OpenFilesystem(_fixture.Root);
            using (var cancel = new CancelToken())
            {
                _session.ScanInitial(cancel);
            }
            Workspace = new WorkspaceViewModel(
                _session, _fixture.Root, () => [], Announce, startInteractionBackgroundWork: background);
            Assert.Equal(background, Workspace.StartsInteractionBackgroundWorkForTests);
        }

        /// <summary>Runs before each announcement is recorded: a fact makes
        /// the announcer throw here.</summary>
        public Action<A11yEvent>? BeforeAnnounce { get; set; }

        private void Announce(A11yEvent spoken)
        {
            BeforeAnnounce?.Invoke(spoken);
            Events.Add(spoken);
        }

        public string Root => _fixture.Root;

        public VaultSession Session => _session;

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

        /// <summary>A new tab on the path, waited for by one drained turn of
        /// the dispatcher and no fixed time. What its open speaks is left in
        /// <see cref="Events"/>: a fact that counts events clears them, or
        /// asks for the kinds it means.</summary>
        public WorkspaceTabViewModel OpenNow(string path)
        {
            Workspace.OpenPath(path, WorkspaceOpenTarget.NewTab);
            PumpedDispatcher.Drain();
            return Workspace.Groups.SelectMany(group => group.Tabs).Last(tab => tab.Path == path);
        }

        public string Read(string path) => File.ReadAllText(Path.Combine(Root, path));

        /// <summary>A Files sidebar on this vault's session, wired to the
        /// workspace as the vault lifecycle wires it
        /// (<c>VaultLifecycleViewModel.InitializeWorkspace</c>), speaking
        /// into <see cref="Events"/>. The vault shuts it down before the
        /// workspace goes.</summary>
        public FilesSidebarViewModel Sidebar()
        {
            var sidebar = new FilesSidebarViewModel(
                _session, Announce, vaultRoot: Root, localAppDataRoot: DeviceStateForTests.FreshFileRecentsRoot());
            sidebar.RetargetRequested = (oldPath, newPath) => Workspace.RetargetPath(oldPath, newPath);
            sidebar.TagEditRequested = Workspace.EditTagAdmitted;
            sidebar.LinksRewritten = Workspace.RebaselineCleanTabsAfterLinkRewrite;
            _sidebars.Add(sidebar);
            return sidebar;
        }

        public void PumpUntil(Func<bool> condition, string what) =>
            Assert.True(PumpedDispatcher.PumpUntil(condition, TimeSpan.FromSeconds(30)), $"{what} never happened");

        public void Wait(ManualResetEventSlim signal, string failure) =>
            Assert.True(PumpedDispatcher.PumpUntil(() => signal.IsSet, Hold), failure);

        /// <summary>For a fact's <c>finally</c>: let held work go and pump
        /// until every admitted operation has ended and the workspace's
        /// tracked workers have finished, so a failed fact tears down with
        /// nothing still on its way to the session. It asserts nothing: a
        /// failure here would hide the fact's own.</summary>
        public void ReleaseAndDrain(params HeldWork[] held)
        {
            foreach (HeldWork work in held)
            {
                work.Release();
            }
            _ = PumpedDispatcher.PumpUntil(
                () => Workspace.SavesIdle && Workspace.RetiredBasesDrains.All(drain => drain.IsCompleted),
                TimeSpan.FromSeconds(30));
        }

        /// <summary>The documents re-sync of a rescan whose index holds
        /// <paramref name="path"/>'s current bytes, run to its end.</summary>
        public void ReSyncDocuments(string path)
        {
            string hash;
            using (var cancel = new CancelToken())
            {
                hash = _session.IndexedContentHashes([path], cancel)[0]!;
            }
            Task<WorkspaceViewModel.RescanDocumentsOutcome> documents = Workspace.ReSyncOpenDocumentsAsync(
                new Dictionary<string, WorkspaceViewModel.IndexedPath>(StringComparer.Ordinal) { [path] = new(hash, OnDisk: true) },
                read => Task.Run<WorkspaceViewModel.RescanRead?>(() =>
                {
                    string text = _session.ReadText(read);
                    return new WorkspaceViewModel.RescanRead(text, SlateUniffiMethods.EditorTextContentHash(text));
                }),
                _ => Task.FromResult<IReadOnlyDictionary<string, WorkspaceViewModel.ReseatProbe>>(
                    new Dictionary<string, WorkspaceViewModel.ReseatProbe>(StringComparer.Ordinal)),
                CancellationToken.None);
            PumpUntil(() => documents.IsCompleted, "the documents re-sync");
            _ = documents.GetAwaiter().GetResult();
        }

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

        /// <summary>The workspace's teardown, once: a fact that disposes
        /// the workspace itself leaves the session to <see cref="Dispose"/>.</summary>
        public void DisposeWorkspace()
        {
            if (!_workspaceDisposed)
            {
                _workspaceDisposed = true;
                Workspace.Dispose();
            }
        }

        public void Dispose()
        {
            try
            {
                foreach (FilesSidebarViewModel sidebar in _sidebars)
                {
                    SidebarSessionShutdown shutdown = sidebar.BeginSessionShutdownAndCaptureWork();
                    _ = PumpedDispatcher.PumpUntil(
                        () => shutdown.SessionWork.IsCompleted && shutdown.TreeRefresh.IsCompleted
                            && shutdown.Filter.IsCompleted && shutdown.ChildExpansions.IsCompleted,
                        TimeSpan.FromSeconds(30));
                }
                DisposeWorkspace();
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
        private bool _holdTheHeaderReadAtTeardown;

        public Peers(string label, params (string Path, string Text)[] extra)
            : this(label, Note, extra)
        {
        }

        /// <summary>With a.md holding <paramref name="note"/>.</summary>
        public Peers(string label, string note, params (string Path, string Text)[] extra)
        {
            H = new Vault(label, [("a.md", note), ("b.md", "# B\n"), .. extra]);
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

        /// <summary>#1673: what this fixture's teardown did for the header
        /// read it let go.</summary>
        public TeardownWitness Teardown { get; } = new();

        /// <summary>#1673: a parked header read, once released, stands in
        /// the hook (before its session call) until this fixture's teardown
        /// waits for it or its session is gone. Let through late, it stops
        /// there: the hook throws, which presents as a failed read.</summary>
        public void HoldTheHeaderReadAtTeardown() => Volatile.Write(ref _holdTheHeaderReadAtTeardown, true);

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
                if (Volatile.Read(ref _holdTheHeaderReadAtTeardown) && Teardown.Hold())
                {
                    throw new IOException("the header's read ran after the fixture's session was gone (test seam)");
                }
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
            try
            {
                // #1673: a header read just let go ends before the session
                // goes. The workspace joins its save workers; nothing joins
                // the header's read, which calls the session right after the
                // hook returns.
                Teardown.DrainBeforeTheSessionGoes(Properties.WhenAllWorkDrained);
            }
            finally
            {
                B.SaveWriteHookForTests = null;
                Properties.RefreshReadHookForTests = null;
                H.Dispose();
                Teardown.SessionGone();
            }
            // The events are left to the collector: a worker still waking on
            // one must not meet a disposed event.
        }
    }
}
