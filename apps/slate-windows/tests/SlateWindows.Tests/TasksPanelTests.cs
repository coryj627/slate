// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Panels;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W4-3 (#735): the note-scoped Tasks leaf over real core output —
/// the mac twin is TasksPanel.swift; labels here are the mac strings
/// verbatim.
/// </summary>
public sealed class TasksPanelTests : IDisposable
{
    private readonly FixtureVault _fixture;
    private readonly VaultSession _session;

    public TasksPanelTests()
    {
        _fixture = FixtureVault.Create(0, "tasks-panel");
        File.WriteAllText(
            Path.Combine(_fixture.Root, "todo.md"),
            "# Todo\n\n"
                + "- [ ] first open 📅 2026-03-01 ⏫\n"
                + "- [x] finished\n"
                + "- [/] rolling\n"
                + "- [ ] second open 🔁 every week\n");
        File.WriteAllText(
            Path.Combine(_fixture.Root, "bare.md"), "No tasks here.\n");
        // Task density (the W4-2 round-10 posture): rows beyond the
        // display cap must stay bounded with true totals.
        var dense = new System.Text.StringBuilder("# Dense\n\n");
        for (int i = 0; i < 600; i++)
        {
            dense.Append(i % 4 == 0 ? "- [x] task " : "- [ ] task ")
                .Append(i)
                .Append('\n');
        }
        File.WriteAllText(
            Path.Combine(_fixture.Root, "dense.md"), dense.ToString());
        _session = VaultSession.OpenFilesystem(_fixture.Root);
        using var cancel = new CancelToken();
        _session.ScanInitial(cancel);
    }

    public void Dispose()
    {
        _session.Dispose();
        _fixture.Dispose();
    }

    private RightPanePanelsViewModel MakePanels(
        List<A11yEvent>? announced = null,
        List<TaskItem>? toggles = null,
        List<TaskItem>? scrolls = null,
        TaskIndexRepairCoordinator? repairs = null)
    {
        var panels = new RightPanePanelsViewModel(
            _session,
            (announced ?? []).Add,
            (_, _) => true,
            _ => true,
            (_, _) => { },
            (task, _) =>
            {
                toggles?.Add(task);
                return true;
            },
            (task, _) => scrolls?.Add(task),
            repairs);
        return panels;
    }

    private RightPanePanelsViewModel LoadNote(
        string path,
        List<A11yEvent>? announced = null,
        List<TaskItem>? toggles = null,
        List<TaskItem>? scrolls = null)
    {
        RightPanePanelsViewModel panels = MakePanels(announced, toggles, scrolls);
        panels.NoteChanged(path);
        Assert.True(
            SpinWait.SpinUntil(
                () => panels.TasksEmptyMessage != "Loading tasks…"
                    && !panels.IsLoadingLinks,
                TimeSpan.FromSeconds(20)),
            $"the {path} tasks never finished loading");
        return panels;
    }

    [Fact]
    public void RowsGroupOpenThenDoneWithTheMacLabels()
    {
        var panels = LoadNote("todo.md");

        // Grouping is by the COMPLETED derivation only: '/' lists as
        // open (mac semantics).
        Assert.Equal(3, panels.OpenTasks.Count);
        Assert.Single(panels.DoneTasks);
        Assert.Equal("Tasks, 3 open of 4 tasks", panels.TasksHeader);
        Assert.Equal("Open (3)", panels.OpenTasksGroupHeader);
        Assert.Equal("Done (1)", panels.DoneTasksGroupHeader);
        Assert.Null(panels.TasksEmptyMessage);

        NoteTaskRowViewModel first = panels.OpenTasks[0];
        Assert.Equal(
            "Open. first open. Due 2026-03-01. Priority highest. Open task.",
            first.AutomationName);
        Assert.Equal("Due 2026-03-01 · Priority highest", first.MetadataCaption);
        Assert.Equal(
            "Scrolls the editor to this task's line.", first.AutomationHelpText);
        Assert.Equal("Mark complete", first.CheckboxLabel);
        Assert.Equal(
            "Toggles the task between open and done.", first.CheckboxHelpText);

        NoteTaskRowViewModel rolling = panels.OpenTasks[1];
        Assert.Equal(
            "In progress. rolling. In-progress task.", rolling.AutomationName);

        NoteTaskRowViewModel done = panels.DoneTasks[0];
        Assert.Equal("Done. finished. Done task.", done.AutomationName);
        Assert.Equal("Mark incomplete", done.CheckboxLabel);
    }

    [Fact]
    public void EmptyStatesSpeakTheMacSentences()
    {
        var panels = MakePanels();
        Assert.Equal("Select a note to see its tasks.", panels.TasksEmptyMessage);
        Assert.Equal("Tasks, none", panels.TasksHeader);

        var loaded = LoadNote("bare.md");
        Assert.Equal("No tasks in this note.", loaded.TasksEmptyMessage);
        Assert.Equal("Tasks, none", loaded.TasksHeader);
    }

    [Fact]
    public void TaskDenseNotesCapRowsWithTrueTotals()
    {
        var panels = LoadNote("dense.md");

        int shown = panels.OpenTasks.Count + panels.DoneTasks.Count;
        Assert.Equal(RightPanePanelsViewModel.MaxTaskRows, shown);
        Assert.Equal("Tasks, 450 open of 600 tasks", panels.TasksHeader);
        Assert.Equal("Showing 512 of 600 tasks.", panels.TasksTruncationNotice);
    }

    [Fact]
    public void ToggleAndActivationRouteThroughTheSeams()
    {
        var toggles = new List<TaskItem>();
        var scrolls = new List<TaskItem>();
        var panels = LoadNote("todo.md", toggles: toggles, scrolls: scrolls);

        panels.ToggleTask(panels.OpenTasks[0]);
        Assert.Equal("first open", Assert.Single(toggles).Text);

        panels.OpenTask(panels.DoneTasks[0]);
        Assert.Equal("finished", Assert.Single(scrolls).Text);
    }

    [Fact]
    public void SaveRefreshReloadsTasks()
    {
        var panels = LoadNote("todo.md");
        Assert.Equal(3, panels.OpenTasks.Count);

        File.WriteAllText(
            Path.Combine(_fixture.Root, "todo.md"),
            "# Todo\n\n- [x] first open\n- [x] finished\n");
        _session.SaveText(
            "todo.md", "# Todo\n\n- [x] first open\n- [x] finished\n", null);
        panels.NoteSaved("todo.md");
        Assert.True(
            SpinWait.SpinUntil(
                () => panels.DoneTasks.Count == 2, TimeSpan.FromSeconds(20)),
            "the save refresh never landed");
        Assert.Empty(panels.OpenTasks);
        Assert.Equal("Tasks, 0 open of 2 tasks", panels.TasksHeader);
    }

    [Fact]
    public void StaleTaskPublishesAreDiscarded()
    {
        var panels = LoadNote("todo.md");
        NoteTaskRowViewModel first = panels.OpenTasks[0];

        // An older in-flight read completing last must not overwrite
        // (the PublishOutline ordering pattern).
        panels.PublishTasks(
            panels.LoadGenerationForTests,
            requestId: -1,
            new NoteTasksPage([], 0, 0, "stale-hash"),
            failure: null);
        Assert.Same(first, panels.OpenTasks[0]);
        Assert.Equal(3, panels.OpenTasks.Count);
    }

    [Fact]
    public void ReadFailuresNeverMasqueradeAsTaskFreeNotes()
    {
        using var fixture = FixtureVault.Create(0, "tasks-panel-failure");
        File.WriteAllText(Path.Combine(fixture.Root, "n.md"), "- [ ] a\n");
        var session = VaultSession.OpenFilesystem(fixture.Root);
        using (var cancel = new CancelToken())
        {
            session.ScanInitial(cancel);
        }
        // Synchronous mode: the async worker's failure publish routes
        // through the xunit SynchronizationContext, which races the
        // assertion below even behind DrainForTests (latent since
        // round 0; surfaced by unrelated JIT-timing shifts in round 5).
        var panels = new RightPanePanelsViewModel(
            session, _ => { }, (_, _) => true, _ => true, (_, _) => { },
            (_, _) => true, (_, _) => { }, synchronousForTests: true);
        session.Dispose();

        panels.NoteChanged("n.md");
        Assert.StartsWith("Could not load tasks: ", panels.TasksEmptyMessage);
    }

    [Fact]
    public void OpenTasksReviewCommandRevealsAnnouncesAndLoads()
    {
        var announced = new List<A11yEvent>();
        using var workspace = new WorkspaceViewModel(
            _session,
            _fixture.Root,
            () => [],
            announced.Add,
            startInteractionBackgroundWork: false);

        workspace.OpenTasksReview();

        Assert.Equal("tasksReview", workspace.ActiveLeaf.Id);
        Assert.True(workspace.IsRightPaneVisible);
        var shown = Assert.Single(announced.OfType<A11yEvent.TasksReviewShown>());
        Assert.Equal("All", shown.FilterName);
        Assert.NotEmpty(workspace.TasksReview.Rows);
    }

    [Fact]
    public void PanelToggleWritesThroughTheGuardedTabPath()
    {
        var announced = new List<A11yEvent>();
        using var workspace = new WorkspaceViewModel(
            _session,
            _fixture.Root,
            () => [],
            announced.Add,
            startInteractionBackgroundWork: false);
        workspace.OpenPath("todo.md");
        Assert.NotEmpty(workspace.Panels.OpenTasks);

        workspace.Panels.ToggleTask(workspace.Panels.OpenTasks[0]);

        // The tab's background stage writes through the session; the
        // disk flip is the deterministic observable.
        string path = Path.Combine(_fixture.Root, "todo.md");
        Assert.True(
            SpinWait.SpinUntil(
                () => DiskContains(path, "- [x] first open"),
                TimeSpan.FromSeconds(20)),
            "the panel toggle never reached disk");
    }

    [Fact]
    public void PanelToggleRefusesDirtyEditorsLoudly()
    {
        var announced = new List<A11yEvent>();
        using var workspace = new WorkspaceViewModel(
            _session,
            _fixture.Root,
            () => [],
            announced.Add,
            startInteractionBackgroundWork: false);
        workspace.OpenPath("todo.md");
        WorkspaceTabViewModel tab = Assert.IsType<WorkspaceTabViewModel>(
            workspace.ActiveGroup.ActiveTab);
        tab.EditorDocument!.Insert(0, "dirty ");
        Assert.True(tab.IsDirty);

        workspace.Panels.ToggleTask(workspace.Panels.OpenTasks[0]);

        // The refusal speaks (the reading-view precedent) and disk
        // stays untouched.
        var refused = Assert.Single(
            announced.OfType<A11yEvent.TaskToggleUnsaved>());
        Assert.Equal("todo.md", refused.Filename);
        Assert.Contains(
            "- [ ] first open",
            File.ReadAllText(Path.Combine(_fixture.Root, "todo.md")));
    }

    [Fact]
    public void StalePanelRowsRefuseTogglesWithAConflict()
    {
        var announced = new List<A11yEvent>();
        using var workspace = new WorkspaceViewModel(
            _session,
            _fixture.Root,
            () => [],
            announced.Add,
            startInteractionBackgroundWork: false);
        workspace.OpenPath("todo.md");
        Assert.NotEmpty(workspace.Panels.OpenTasks);
        // Captured BEFORE the save: panel refreshes are async, so
        // rows read against the old content stay clickable while the
        // refresh is in flight (adversarial round 2).
        NoteTaskRowViewModel staleRow = workspace.Panels.OpenTasks[0];

        // A task is inserted BEFORE the captured row and saved
        // through the tab: the row's ordinal now names the inserted
        // task, and the tab's CURRENT hash would sail through the
        // core CAS — the round-2 failure completed the wrong task.
        WorkspaceTabViewModel tab = Assert.IsType<WorkspaceTabViewModel>(
            workspace.ActiveGroup.ActiveTab);
        tab.EditorDocument!.Insert(0, "- [ ] inserted before everything\n");
        Assert.True(tab.Save());

        workspace.Panels.ToggleTask(staleRow);

        _ = Assert.Single(announced.OfType<A11yEvent.TaskToggleConflict>());
        string content = File.ReadAllText(Path.Combine(_fixture.Root, "todo.md"));
        Assert.Contains("- [ ] inserted before everything", content);
        Assert.Contains("- [ ] first open", content);
        // The refusal re-snapshots: fresh rows carry the new hash and
        // the inserted task leads.
        Assert.True(
            SpinWait.SpinUntil(
                () => workspace.Panels.OpenTasks.Count == 4
                    && workspace.Panels.OpenTasks[0].Task.Text
                        == "inserted before everything",
                TimeSpan.FromSeconds(20)),
            "the conflict refusal never re-snapshotted the panel");
    }

    [Fact]
    public void BusyTabsReportBusyNotStarted()
    {
        using var workspace = new WorkspaceViewModel(
            _session,
            _fixture.Root,
            () => [],
            _ => { },
            startInteractionBackgroundWork: false);
        workspace.OpenPath("todo.md");
        WorkspaceTabViewModel tab = Assert.IsType<WorkspaceTabViewModel>(
            workspace.ActiveGroup.ActiveTab);
        TaskItem task = _session.TasksForFile("todo.md")[0];
        var announced = new List<A11yEvent>();

        // Round 2: the legacy bool conflated these — a busy refusal
        // read as Started armed review refresh state for an
        // operation that never ran.
        Assert.Equal(TabTaskToggle.Started, tab.ToggleTask(task, announced.Add));
        Assert.Equal(
            TabTaskToggle.RefusedBusy, tab.ToggleTask(task, announced.Add));
        Assert.Contains(
            announced.OfType<A11yEvent.HostComposed>(),
            composed => composed.Text == "A task update is already in progress.");
    }

    [Fact]
    public void StaleTabsRefuseReviewTogglesWithAConflict()
    {
        var announced = new List<A11yEvent>();
        using var workspace = new WorkspaceViewModel(
            _session,
            _fixture.Root,
            () => [],
            announced.Add,
            startInteractionBackgroundWork: false);
        workspace.OpenPath("todo.md");

        // Disk moves under the OPEN tab (an external edit through the
        // session): the review snapshot carries the NEW hash, the tab
        // still holds the old baseline — toggling through that tab
        // would splice against the wrong bytes (round 1).
        _ = _session.SaveText(
            "todo.md", "- [ ] rewritten 📅 2026-01-01\n", null);
        workspace.TasksReview.ForceReload();
        ReviewTaskRowViewModel row = workspace.TasksReview.Rows.First(
            r => r.Path == "todo.md");

        workspace.TasksReview.ToggleTask(row);

        _ = Assert.Single(announced.OfType<A11yEvent.TaskToggleConflict>());
        Assert.Contains(
            "- [ ] rewritten",
            File.ReadAllText(Path.Combine(_fixture.Root, "todo.md")));
    }

    [Fact]
    public void ClosingTheOriginatingTabStillCompletesReviewToggles()
    {
        var announced = new List<A11yEvent>();
        using var workspace = new WorkspaceViewModel(
            _session,
            _fixture.Root,
            () => [],
            announced.Add,
            startInteractionBackgroundWork: false);
        workspace.OpenPath("todo.md");
        workspace.TasksReview.ForceReload();
        ReviewTaskRowViewModel row = workspace.TasksReview.Rows.First(
            r => r.Path == "todo.md" && r.Task.Text == "first open");

        // The toggle routes through the open tab (Started)…
        workspace.TasksReview.ToggleTask(row);
        WorkspaceTabViewModel tab = Assert.IsType<WorkspaceTabViewModel>(
            workspace.ActiveGroup.ActiveTab);
        // …and the user closes that tab before the publish runs
        // (adversarial round 3: disposal used to eat the outcome —
        // no announcement, review stale, refresh armed forever).
        workspace.CloseTabCommand.Execute(tab);

        string diskPath = Path.Combine(_fixture.Root, "todo.md");
        PumpDispatcherUntil(
            () => DiskContains(diskPath, "- [x] first open"),
            () => "the toggle never reached disk; announced: "
                + AnnouncementDump(announced));

        // The completion outlives the tab: the success announcement
        // still fires and the review re-snapshots.
        PumpDispatcherUntil(
            () => announced.OfType<A11yEvent.HostComposed>()
                .Any(composed => composed.Text == "Task completed."),
            "the disposed-tab completion never announced");
        Assert.Contains(
            workspace.TasksReview.Rows,
            r => r.Path == "todo.md"
                && r.Task.Text == "first open"
                && r.Task.Completed);
    }

    [Fact]
    public void ClosingTheActiveTabStillCompletesPanelToggles()
    {
        var announced = new List<A11yEvent>();
        using var workspace = new WorkspaceViewModel(
            _session,
            _fixture.Root,
            () => [],
            announced.Add,
            startInteractionBackgroundWork: false);
        workspace.OpenPath("todo.md");
        NoteTaskRowViewModel row = workspace.Panels.OpenTasks.First(
            r => r.Task.Text == "first open");

        // The panel toggle routes through the active tab (round 4:
        // this route lacked the review's terminal completion)…
        workspace.Panels.ToggleTask(row);
        WorkspaceTabViewModel tab = Assert.IsType<WorkspaceTabViewModel>(
            workspace.ActiveGroup.ActiveTab);
        // …and the user closes that tab before the publish runs.
        workspace.CloseTabCommand.Execute(tab);

        string diskPath = Path.Combine(_fixture.Root, "todo.md");
        PumpDispatcherUntil(
            () => DiskContains(diskPath, "- [x] first open"),
            () => "the panel toggle never reached disk; announced: "
                + AnnouncementDump(announced));

        // The terminal completion outlives the tab: the success
        // still announces instead of vanishing silently.
        PumpDispatcherUntil(
            () => announced.OfType<A11yEvent.HostComposed>()
                .Any(composed => composed.Text == "Task completed."),
            "the disposed-tab panel completion never announced");
    }

    [Fact]
    public void StalePanelRowsRefuseActivationUntilRefreshed()
    {
        using var workspace = new WorkspaceViewModel(
            _session,
            _fixture.Root,
            () => [],
            _ => { },
            startInteractionBackgroundWork: false);
        workspace.OpenPath("todo.md");
        // Captured BEFORE the save: rows read against the old content
        // stay actionable while a refresh is in flight (adversarial
        // round 7 — the review guard's note-panel twin).
        NoteTaskRowViewModel stale = workspace.Panels.OpenTasks.First(
            r => r.Task.Text == "second open");

        WorkspaceTabViewModel tab = Assert.IsType<WorkspaceTabViewModel>(
            workspace.ActiveGroup.ActiveTab);
        tab.EditorDocument!.Insert(0, "- [ ] inserted before everything\n");
        Assert.True(tab.Save());
        int caretBefore = tab.EditorCaretOffset;

        // The stale row's byte offset points into the OLD text: the
        // silent-refusal observable is the caret NOT moving.
        workspace.Panels.OpenTask(stale);
        Assert.Equal(caretBefore, tab.EditorCaretOffset);

        // A fresh row activates: the caret parks EXACTLY at its task
        // line's start in the new text.
        NoteTaskRowViewModel fresh = workspace.Panels.OpenTasks.First(
            r => r.Task.Text == "second open");
        Assert.NotSame(stale, fresh);
        workspace.Panels.OpenTask(fresh);
        Assert.Equal(
            tab.Text.IndexOf("- [ ] second open", StringComparison.Ordinal),
            tab.EditorCaretOffset);
    }

    [Fact]
    public void ActivationRefusesWhenDiskMovedUnderTheSnapshot()
    {
        var announced = new List<A11yEvent>();
        using var workspace = new WorkspaceViewModel(
            _session,
            _fixture.Root,
            () => [],
            announced.Add,
            startInteractionBackgroundWork: false);
        workspace.OpenTasksReview();
        ReviewTaskRowViewModel stale = workspace.TasksReview.Rows.First(
            r => r.Path == "todo.md");

        // The file is rewritten AFTER the snapshot: the row's byte
        // offset now points into different content — an unverified
        // scroll would land on unrelated text and announce success
        // (adversarial round 6).
        _ = _session.SaveText(
            "todo.md", "- [ ] rewritten \U0001F4C5 2026-01-01\n", null);

        workspace.TasksReview.OpenRow(stale);

        _ = announced.OfType<A11yEvent.HostComposed>().Single(composed =>
            composed.Text == "todo.md changed since these tasks loaded. Refreshing.");
        Assert.Empty(announced.OfType<A11yEvent.OpenedAtLine>());
        Assert.Empty(announced.OfType<A11yEvent.ScrolledToLine>());

        // The refusal re-snapshotted; the fresh row carries the new
        // hash and activates (the refused attempt opened the tab, so
        // this scrolls in place).
        ReviewTaskRowViewModel fresh = workspace.TasksReview.Rows.First(
            r => r.Path == "todo.md");
        workspace.TasksReview.OpenRow(fresh);
        var scrolled = Assert.Single(announced.OfType<A11yEvent.ScrolledToLine>());
        Assert.Equal("todo.md", scrolled.Filename);
    }

    [Fact]
    public void DirtyBuffersRefuseBothActivationRoutesLoudly()
    {
        // Adversarial round 8: unsaved edits shift the LIVE text
        // while SavedContentHash stays put, so a saved-content
        // offset can park the caret on unrelated words. Both
        // activation routes refuse dirty buffers with the
        // TaskToggleUnsaved family's wording — round 6's
        // scroll-anyway posture was a false green that asserted the
        // announcement but never where the caret landed.
        var announced = new List<A11yEvent>();
        using var workspace = new WorkspaceViewModel(
            _session,
            _fixture.Root,
            () => [],
            announced.Add,
            startInteractionBackgroundWork: false);
        workspace.OpenPath("todo.md");
        workspace.OpenTasksReview();
        ReviewTaskRowViewModel reviewRow = workspace.TasksReview.Rows.First(
            r => r.Path == "todo.md");
        NoteTaskRowViewModel panelRow = workspace.Panels.OpenTasks.First(
            r => r.Task.Text == "second open");

        WorkspaceTabViewModel tab = Assert.IsType<WorkspaceTabViewModel>(
            workspace.ActiveGroup.ActiveTab);
        tab.EditorDocument!.Insert(0, "- [ ] draft insertion\n");
        tab.EditorDocument.Remove(
            tab.EditorDocument.Text.IndexOf(
                "# Todo", StringComparison.Ordinal),
            "# Todo".Length);
        Assert.True(tab.IsDirty);
        int caretBefore = tab.EditorCaretOffset;
        int reviewRequests = workspace.TasksReview.LoadRequestIdForTests;

        // Review route: refusal announced, caret pinned, no reload
        // (the snapshot is still valid against SAVED content).
        workspace.TasksReview.OpenRow(reviewRow);
        Assert.Equal(caretBefore, tab.EditorCaretOffset);
        Assert.Empty(announced.OfType<A11yEvent.ScrolledToLine>());
        Assert.Empty(announced.OfType<A11yEvent.OpenedAtLine>());
        Assert.Equal(reviewRequests, workspace.TasksReview.LoadRequestIdForTests);

        // Panel route: same refusal, same pinned caret.
        workspace.Panels.OpenTask(panelRow);
        Assert.Equal(caretBefore, tab.EditorCaretOffset);

        Assert.Equal(
            2,
            announced.OfType<A11yEvent.HostComposed>().Count(composed =>
                composed.Text
                    == "Cannot open this task. The editor has unsaved changes in todo.md. Save the note first."));

        // Saving re-enables activation, and the caret lands EXACTLY
        // on the task's byte offset in the saved text.
        Assert.True(tab.Save());
        NoteTaskRowViewModel fresh = workspace.Panels.OpenTasks.First(
            r => r.Task.Text == "second open");
        workspace.Panels.OpenTask(fresh);
        Assert.Equal(
            tab.Text.IndexOf("- [ ] second open", StringComparison.Ordinal),
            tab.EditorCaretOffset);
    }

    [Fact]
    public void ExternallyModifiedCleanTabsRefuseEveryRouteUntilReconciled()
    {
        // Adversarial round 9: an external write leaves an open
        // CLEAN tab and task rows born from its baseline sharing the
        // same obsolete hash — SavedContentHash == row hash matches
        // vacuously and every guard would pass. The vault change
        // stream's Modified seam (VaultLifecycle drives it in
        // production) must derive staleness from the INDEX.
        var announced = new List<A11yEvent>();
        using var workspace = new WorkspaceViewModel(
            _session,
            _fixture.Root,
            () => [],
            announced.Add,
            startInteractionBackgroundWork: false);
        workspace.OpenPath("todo.md");
        workspace.OpenTasksReview();
        ReviewTaskRowViewModel staleReviewRow = workspace.TasksReview.Rows.First(
            r => r.Path == "todo.md" && r.Task.Text == "first open");
        NoteTaskRowViewModel stalePanelRow = workspace.Panels.OpenTasks.First(
            r => r.Task.Text == "second open");
        WorkspaceTabViewModel tab = Assert.IsType<WorkspaceTabViewModel>(
            workspace.ActiveGroup.ActiveTab);
        int caretBefore = tab.EditorCaretOffset;

        // Disk and index move on WITHOUT the tab.
        _ = _session.SaveText(
            "todo.md",
            "- [ ] rewritten externally \U0001F4C5 2026-01-01\n",
            null);
        workspace.InvalidateModifiedPath("todo.md");

        // Review activation: stale refusal, pinned caret, no
        // success verbs, snapshot reloaded.
        workspace.TasksReview.OpenRow(staleReviewRow);
        Assert.Equal(caretBefore, tab.EditorCaretOffset);
        Assert.Empty(announced.OfType<A11yEvent.ScrolledToLine>());
        Assert.Empty(announced.OfType<A11yEvent.OpenedAtLine>());
        _ = announced.OfType<A11yEvent.HostComposed>().Single(composed =>
            composed.Text == "todo.md changed since these tasks loaded. Refreshing.");

        // Panel activation: silent refusal, pinned caret.
        workspace.Panels.OpenTask(stalePanelRow);
        Assert.Equal(caretBefore, tab.EditorCaretOffset);

        // Both toggle routes refuse with a conflict instead of
        // starting a doomed write (the CAS retry loop): disk keeps
        // the external content untouched.
        workspace.TasksReview.ToggleTask(staleReviewRow);
        workspace.Panels.ToggleTask(stalePanelRow);
        Assert.Equal(2, announced.OfType<A11yEvent.TaskToggleConflict>().Count());
        Assert.DoesNotContain(
            announced.OfType<A11yEvent.HostComposed>(),
            composed => composed.Text == "Task completed.");
        Assert.Contains(
            "- [ ] rewritten externally",
            File.ReadAllText(Path.Combine(_fixture.Root, "todo.md")));
    }

    [Fact]
    public void NavigationClearsStalenessSoTheNextNoteWorks()
    {
        // Adversarial round 10: the staleness verdict belongs to the
        // note that went stale — current-tab navigation reuses the
        // tab in place, and the replacement note must not inherit
        // the flag or every identity guard falsely refuses its
        // fresh rows.
        var announced = new List<A11yEvent>();
        using var workspace = new WorkspaceViewModel(
            _session,
            _fixture.Root,
            () => [],
            announced.Add,
            startInteractionBackgroundWork: false);
        workspace.OpenPath("todo.md");
        WorkspaceTabViewModel tab = Assert.IsType<WorkspaceTabViewModel>(
            workspace.ActiveGroup.ActiveTab);

        // Note A goes externally stale under the open tab.
        _ = _session.SaveText(
            "todo.md", "- [ ] rewritten externally\n", null);
        workspace.InvalidateModifiedPath("todo.md");

        // Current-tab navigation reuses the SAME tab for note B.
        workspace.OpenPath("dense.md");
        Assert.Same(tab, workspace.ActiveGroup.ActiveTab);
        workspace.OpenTasksReview();

        // Panel activation lands exactly.
        NoteTaskRowViewModel panelRow = workspace.Panels.OpenTasks.First(
            r => r.Task.Text == "task 1");
        workspace.Panels.OpenTask(panelRow);
        Assert.Equal(
            tab.Text.IndexOf("- [ ] task 1\n", StringComparison.Ordinal),
            tab.EditorCaretOffset);

        // Review activation speaks the same-file verb.
        ReviewTaskRowViewModel reviewRow = workspace.TasksReview.Rows.First(
            r => r.Path == "dense.md" && r.Task.Text == "task 2");
        workspace.TasksReview.OpenRow(reviewRow);
        _ = Assert.Single(announced.OfType<A11yEvent.ScrolledToLine>());

        // Both toggle routes write through to disk. The tab allows
        // one in-flight toggle at a time, so each PUMPS while
        // waiting — a queued failure publish must be able to surface
        // (an unpumped dispatcher makes a transient toggle error
        // look like a silent 20s timeout on CI). A leaked stale flag
        // would refuse IMMEDIATELY with a conflict, so the refusal
        // asserts run right after each call.
        string densePath = Path.Combine(_fixture.Root, "dense.md");
        workspace.Panels.ToggleTask(panelRow);
        Assert.Empty(announced.OfType<A11yEvent.TaskToggleConflict>());
        PumpDispatcherUntil(
            () => DiskContains(densePath, "- [x] task 1\n")
                && announced.OfType<A11yEvent.HostComposed>()
                    .Any(composed => composed.Text == "Task completed."),
            () => "the panel toggle never completed; announced: "
                + AnnouncementDump(announced));

        // The panel toggle re-baselined the tab, so rows captured
        // before it are stale BY DESIGN — the review route needs a
        // fresh snapshot row.
        workspace.TasksReview.ForceReload();
        ReviewTaskRowViewModel freshReviewRow = workspace.TasksReview.Rows.First(
            r => r.Path == "dense.md" && r.Task.Text == "task 2");
        workspace.TasksReview.ToggleTask(freshReviewRow);
        Assert.Empty(announced.OfType<A11yEvent.TaskToggleConflict>());
        PumpDispatcherUntil(
            () => DiskContains(densePath, "- [x] task 2\n"),
            () => "the review toggle never reached disk; announced: "
                + AnnouncementDump(announced));
        Assert.Empty(announced.OfType<A11yEvent.TaskToggleConflict>());
        Assert.DoesNotContain(
            announced.OfType<A11yEvent.HostComposed>(),
            composed => composed.Text
                == "A task update is already in progress.");
    }

    private static string AnnouncementDump(List<A11yEvent> announced) =>
        string.Join(
            " | ",
            announced.Select(a => a switch
            {
                A11yEvent.HostComposed composed => composed.Text,
                _ => a.GetType().Name,
            }));

    [Fact]
    public void PostWriteToggleFailuresReconcileInsteadOfAssumingUnchanged()
    {
        // Adversarial round 11, tab route: the checkbox flips on
        // disk, then the failure lands — the clean editor is now
        // obsolete and the rows are ghosts. The completion must
        // reconcile, not assume "disk never changed".
        var announced = new List<A11yEvent>();
        using var workspace = new WorkspaceViewModel(
            _session,
            _fixture.Root,
            () => [],
            announced.Add,
            startInteractionBackgroundWork: false);
        workspace.OpenPath("todo.md");
        NoteTaskRowViewModel row = workspace.Panels.OpenTasks.First(
            r => r.Task.Text == "first open");
        WorkspaceTabViewModel tab = Assert.IsType<WorkspaceTabViewModel>(
            workspace.ActiveGroup.ActiveTab);
        tab.TaskToggleFaultForTests = () =>
            throw new InvalidOperationException("index commit failed");

        workspace.Panels.ToggleTask(row);
        string diskPath = Path.Combine(_fixture.Root, "todo.md");
        PumpDispatcherUntil(
            () => DiskContains(diskPath, "- [x] first open"),
            () => "the write never landed; announced: "
                + AnnouncementDump(announced));
        // (The wrapped VaultException renders its message with the
        // generated "@message=" prefix — match on both halves.)
        PumpDispatcherUntil(
            () => announced.OfType<A11yEvent.HostComposed>().Any(composed =>
                composed.Text.StartsWith(
                    "Task could not be toggled:", StringComparison.Ordinal)
                && composed.Text.Contains("index commit failed")),
            "the failure never published");

        // The completion reconciled: divergence honesty on the tab,
        // fresh panel rows showing what disk actually holds, and
        // further toggles refuse until the note is reopened.
        _ = announced.OfType<A11yEvent.HostComposed>().Single(composed =>
            composed.Text
                == "Task toggled on disk, but the editor no longer matches it. Reopen the note before editing.");
        Assert.DoesNotContain(
            announced.OfType<A11yEvent.HostComposed>(),
            composed => composed.Text == "Task completed.");
        Assert.Contains(
            workspace.Panels.DoneTasks, r => r.Task.Text == "first open");

        NoteTaskRowViewModel second = workspace.Panels.OpenTasks.First(
            r => r.Task.Text == "second open");
        workspace.Panels.ToggleTask(second);
        _ = Assert.Single(announced.OfType<A11yEvent.TaskToggleConflict>());
        Assert.Contains("- [ ] second open", File.ReadAllText(diskPath));
    }

    [Fact]
    public void RealPreCommitFailuresRepairTheIndexOnTheTabRoute()
    {
        using var _envLock = EnvFaultLock.Acquire();
        // Adversarial round 12, tab route at the REAL boundary: the
        // core's fault seam rolls the index back after the file
        // write, so recovery must reindex the path before any
        // surface reloads — a stale-index reload shows the ghost
        // pre-write rows.
        _ = _session.SaveText("fault-tab-target.md", "- [ ] flip me\n", null);
        var announced = new List<A11yEvent>();
        using var workspace = new WorkspaceViewModel(
            _session,
            _fixture.Root,
            () => [],
            announced.Add,
            startInteractionBackgroundWork: false);
        workspace.OpenPath("fault-tab-target.md");
        NoteTaskRowViewModel row = workspace.Panels.OpenTasks.First(
            r => r.Task.Text == "flip me");
        WorkspaceTabViewModel tab = Assert.IsType<WorkspaceTabViewModel>(
            workspace.ActiveGroup.ActiveTab);

        Environment.SetEnvironmentVariable(
            "SLATE_TEST_FAULT_AFTER_WRITE", "fault-tab-target");
        try
        {
            workspace.Panels.ToggleTask(row);
            PumpDispatcherUntil(
                () => announced.OfType<A11yEvent.HostComposed>().Any(composed =>
                    composed.Text.StartsWith(
                        "Task could not be toggled:", StringComparison.Ordinal)
                    && composed.Text.Contains("test fault")),
                () => "the injected failure never published; announced: "
                    + AnnouncementDump(announced));
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                "SLATE_TEST_FAULT_AFTER_WRITE", null);
        }

        // Disk flipped; the repaired index feeds the re-snapshot, so
        // the panel shows disk truth; the clean tab gets the
        // divergence honesty and stays guarded.
        string diskPath = Path.Combine(_fixture.Root, "fault-tab-target.md");
        Assert.Contains("- [x] flip me", File.ReadAllText(diskPath));
        Assert.Contains(
            workspace.Panels.DoneTasks, r => r.Task.Text == "flip me");
        _ = announced.OfType<A11yEvent.HostComposed>().Single(composed =>
            composed.Text
                == "Task toggled on disk, but the editor no longer matches it. Reopen the note before editing.");
        Assert.DoesNotContain(
            announced.OfType<A11yEvent.HostComposed>(),
            composed => composed.Text == "Task completed.");

        // Retrying through the obsolete tab refuses with a conflict
        // (the tab never re-baselined) instead of writing blind.
        NoteTaskRowViewModel fresh = workspace.Panels.DoneTasks.First(
            r => r.Task.Text == "flip me");
        workspace.Panels.ToggleTask(fresh);
        _ = Assert.Single(announced.OfType<A11yEvent.TaskToggleConflict>());
        Assert.Contains("- [x] flip me", File.ReadAllText(diskPath));
    }

    [Fact]
    public void ThePanelHonorsTheSharedRepairQuarantine()
    {
        using var _envLock = EnvFaultLock.Acquire();
        // Adversarial round 15: pending repairs were review-only —
        // the note panel queried NoteTasks directly, so navigating
        // away and back republished the rolled-back row and its
        // ghost hash. The quarantine is shared now: while the repair
        // keeps failing, the panel shows its honest read-fault
        // surface; once it lands, the rows show disk truth.
        _ = _session.SaveText("fault-tab-target.md", "- [ ] flip me\n", null);
        var announced = new List<A11yEvent>();
        using var workspace = new WorkspaceViewModel(
            _session,
            _fixture.Root,
            () => [],
            announced.Add,
            startInteractionBackgroundWork: false);
        workspace.OpenPath("fault-tab-target.md");
        NoteTaskRowViewModel row = workspace.Panels.OpenTasks.First(
            r => r.Task.Text == "flip me");

        Environment.SetEnvironmentVariable(
            "SLATE_TEST_FAULT_AFTER_WRITE", "fault-tab-target");
        Environment.SetEnvironmentVariable(
            "SLATE_TEST_FAULT_REINDEX", "fault-tab-target");
        try
        {
            workspace.Panels.ToggleTask(row);
            PumpDispatcherUntil(
                () => announced.OfType<A11yEvent.HostComposed>().Any(composed =>
                    composed.Text.StartsWith(
                        "Task could not be toggled:", StringComparison.Ordinal)),
                () => "the injected failure never published; announced: "
                    + AnnouncementDump(announced));

            // Navigate away and back with the fault still held: the
            // panel must NOT republish the ghost open row from the
            // known-stale index — the honest read fault shows.
            workspace.OpenPath("todo.md");
            workspace.OpenPath("fault-tab-target.md");
            Assert.Empty(workspace.Panels.OpenTasks);
            Assert.Empty(workspace.Panels.DoneTasks);
            Assert.StartsWith(
                "Could not load tasks: ",
                workspace.Panels.TasksEmptyMessage);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                "SLATE_TEST_FAULT_AFTER_WRITE", null);
            Environment.SetEnvironmentVariable(
                "SLATE_TEST_FAULT_REINDEX", null);
        }

        // The fault cleared: the next visit retries the repair and
        // the rows show DISK truth.
        workspace.OpenPath("todo.md");
        workspace.OpenPath("fault-tab-target.md");
        Assert.Contains(
            workspace.Panels.DoneTasks, r => r.Task.Text == "flip me");
    }

    [Fact]
    public void PanelQueriesDiscardResultsWhenThePathQuarantinesMidRead()
    {
        // Adversarial round 16: a post-write failure registering the
        // path between the panel's quarantine gate and its NoteTasks
        // read moves no revision the read could see — the page may
        // hold the ghost pre-write row, so it must be discarded.
        var repairs = new TaskIndexRepairCoordinator(_session);
        var panels = MakePanels(repairs: repairs);
        panels.TasksInterleaveForTests = () =>
        {
            panels.TasksInterleaveForTests = null;
            repairs.NotePending("todo.md");
        };
        panels.NoteChanged("todo.md");
        Assert.True(
            SpinWait.SpinUntil(
                () => panels.TasksEmptyMessage is { } message
                    && message != "Loading tasks…",
                TimeSpan.FromSeconds(20)),
            "the tasks load never settled");
        Assert.StartsWith("Could not load tasks: ", panels.TasksEmptyMessage);
        Assert.Empty(panels.OpenTasks);

        // The pending path repairs on the next load and rows return.
        panels.NoteSaved("todo.md");
        Assert.True(
            SpinWait.SpinUntil(
                () => panels.OpenTasks.Count == 3, TimeSpan.FromSeconds(20)),
            "the repaired reload never landed");
        Assert.Null(panels.TasksEmptyMessage);
    }

    [Fact]
    public void TabRouteUnknownOutcomesFailClosed()
    {
        // Adversarial round 16, tab route: the toggle write lands,
        // the failure hook deletes the file, and the read-back
        // fails — an unknown outcome must quarantine the path, not
        // read as "no write happened".
        _ = _session.SaveText("fault-unknown-tab.md", "- [ ] flip me\n", null);
        var announced = new List<A11yEvent>();
        using var workspace = new WorkspaceViewModel(
            _session,
            _fixture.Root,
            () => [],
            announced.Add,
            startInteractionBackgroundWork: false);
        workspace.OpenPath("fault-unknown-tab.md");
        NoteTaskRowViewModel row = workspace.Panels.OpenTasks.First(
            r => r.Task.Text == "flip me");
        WorkspaceTabViewModel tab = Assert.IsType<WorkspaceTabViewModel>(
            workspace.ActiveGroup.ActiveTab);
        string diskPath = Path.Combine(_fixture.Root, "fault-unknown-tab.md");

        tab.TaskToggleFaultForTests = () =>
        {
            tab.TaskToggleFaultForTests = null;
            File.Delete(diskPath);
            throw new InvalidOperationException("boom after write");
        };
        workspace.Panels.ToggleTask(row);
        PumpDispatcherUntil(
            () => announced.OfType<A11yEvent.HostComposed>().Any(composed =>
                composed.Text.StartsWith(
                    "Task could not be toggled:", StringComparison.Ordinal)),
            () => "the injected failure never published; announced: "
                + AnnouncementDump(announced));

        // Unknown outcome → fail-closed repair. With the file GONE,
        // the repair CONVERGES TO DELETION (round 23): the panel's
        // reload shows the honest empty state — never the ghost
        // open row, and no eternal read-fault bar.
        workspace.Panels.ReloadTasks();
        Assert.True(
            SpinWait.SpinUntil(
                () => workspace.Panels.OpenTasks.Count == 0
                    && workspace.Panels.DoneTasks.Count == 0
                    && workspace.Panels.TasksEmptyMessage is not null
                    && workspace.Panels.TasksEmptyMessage != "Loading tasks…",
                TimeSpan.FromSeconds(20)),
            "the converged reload never settled");
        Assert.DoesNotContain(
            announced.OfType<A11yEvent.HostComposed>(),
            composed => composed.Text == "Task completed.");

        // The note returns via an indexed write: the panel shows
        // disk truth again.
        _ = _session.SaveText("fault-unknown-tab.md", "- [x] flip me\n", null);
        workspace.Panels.ReloadTasks();
        Assert.True(
            SpinWait.SpinUntil(
                () => workspace.Panels.DoneTasks.Any(
                    r => r.Task.Text == "flip me"),
                TimeSpan.FromSeconds(20)),
            "the recreated note never landed");
    }

    [Fact]
    public void RepairsOverlappingAPanelReadDiscardItsResult()
    {
        // Adversarial round 17: a register-repair-remove completing
        // DURING the panel's read leaves nothing pending while the
        // page still holds the pre-repair ghost — only the epoch
        // sees the interval, so the result is discarded.
        var repairs = new TaskIndexRepairCoordinator(_session);
        var panels = MakePanels(repairs: repairs);
        bool repaired = false;
        panels.TasksInterleaveForTests = () =>
        {
            panels.TasksInterleaveForTests = null;
            repaired = repairs.TryRepairNow("todo.md", out _);
        };
        panels.NoteChanged("todo.md");
        Assert.True(
            SpinWait.SpinUntil(
                () => panels.TasksEmptyMessage is { } message
                    && message != "Loading tasks…",
                TimeSpan.FromSeconds(20)),
            "the tasks load never settled");
        Assert.True(repaired);
        Assert.StartsWith("Could not load tasks: ", panels.TasksEmptyMessage);
        Assert.Empty(panels.OpenTasks);

        // The next load runs against the repaired index and settles.
        panels.NoteSaved("todo.md");
        Assert.True(
            SpinWait.SpinUntil(
                () => panels.OpenTasks.Count == 3, TimeSpan.FromSeconds(20)),
            "the post-repair reload never landed");
        Assert.Null(panels.TasksEmptyMessage);
    }

    [Fact]
    public void TogglesOverlappingAPanelReadInvalidateItsTicket()
    {
        using var _envLock = EnvFaultLock.Acquire();
        // Adversarial round 19, panel surface: a direct review
        // toggle of an UNRELATED path fails post-write (both fault
        // seams) while the panel reads its own note — the panel's
        // ticket predates the toggle's lease, so the read result
        // must be discarded rather than published over a vault whose
        // index is now part-stale.
        // Dated so the probe sorts into page one ahead of the 600
        // undated dense rows (the round-1 fixture lesson).
        _ = _session.SaveText(
            "lease-panel-probe.md", "- [ ] probe \U0001F4C5 2026-01-01\n", null);
        var announced = new List<A11yEvent>();
        using var workspace = new WorkspaceViewModel(
            _session,
            _fixture.Root,
            () => [],
            announced.Add,
            startInteractionBackgroundWork: false);
        workspace.OpenTasksReview();
        ReviewTaskRowViewModel probe = workspace.TasksReview.Rows.First(
            r => r.Path == "lease-panel-probe.md");

        workspace.Panels.TasksInterleaveForTests = () =>
        {
            workspace.Panels.TasksInterleaveForTests = null;
            Environment.SetEnvironmentVariable(
                "SLATE_TEST_FAULT_AFTER_WRITE", "lease-panel-probe");
            Environment.SetEnvironmentVariable(
                "SLATE_TEST_FAULT_REINDEX", "lease-panel-probe");
            try
            {
                workspace.TasksReview.ToggleTask(probe);
            }
            finally
            {
                Environment.SetEnvironmentVariable(
                    "SLATE_TEST_FAULT_AFTER_WRITE", null);
                Environment.SetEnvironmentVariable(
                    "SLATE_TEST_FAULT_REINDEX", null);
            }
        };
        workspace.OpenPath("todo.md");
        Assert.True(
            SpinWait.SpinUntil(
                () => workspace.Panels.TasksEmptyMessage is { } message
                    && message != "Loading tasks…",
                TimeSpan.FromSeconds(20)),
            "the tasks load never settled");
        Assert.StartsWith(
            "Could not load tasks: ", workspace.Panels.TasksEmptyMessage);

        // Faults cleared: the panel's own sweep repairs the pending
        // path on the next reload — the review does not have to run.
        workspace.Panels.ReloadTasks();
        Assert.True(
            SpinWait.SpinUntil(
                () => workspace.Panels.OpenTasks.Count == 3,
                TimeSpan.FromSeconds(20)),
            "the post-repair reload never landed");
        Assert.Null(workspace.Panels.TasksEmptyMessage);
    }

    [Fact]
    public void ManualSavesFailingPostWriteQuarantineBothSurfaces()
    {
        using var _envLock = EnvFaultLock.Acquire();
        // Adversarial round 20: ordinary saves route through the
        // same file-before-index pipeline as toggles — a manual
        // task edit whose save fails post-write must enter the same
        // quarantine, or panel and review queries publish pre-save
        // ghost rows with clean tickets.
        _ = _session.SaveText(
            "manual-probe.md", "- [ ] probe \U0001F4C5 2026-01-01\n", null);
        var announced = new List<A11yEvent>();
        using var workspace = new WorkspaceViewModel(
            _session,
            _fixture.Root,
            () => [],
            announced.Add,
            startInteractionBackgroundWork: false);
        workspace.OpenPath("manual-probe.md");
        workspace.OpenTasksReview();
        WorkspaceTabViewModel tab = Assert.IsType<WorkspaceTabViewModel>(
            workspace.ActiveGroup.ActiveTab);

        // A MANUAL task edit through the editor, saved while the
        // core hits the post-write fault; the repair fault holds so
        // the quarantine's bar is observable.
        tab.EditorDocument!.Insert(
            0, "- [x] done manually \U0001F4C5 2026-01-02\n");
        Environment.SetEnvironmentVariable(
            "SLATE_TEST_FAULT_AFTER_WRITE", "manual-probe");
        Environment.SetEnvironmentVariable(
            "SLATE_TEST_FAULT_REINDEX", "manual-probe");
        try
        {
            Assert.False(tab.Save());
            Assert.StartsWith("Save blocked: ", tab.Status);
            // The write landed; the index rolled back.
            Assert.Contains(
                "- [x] done manually",
                File.ReadAllText(Path.Combine(_fixture.Root, "manual-probe.md")));

            // Both surfaces are barred while the repair keeps
            // failing — no pre-save ghost publishes.
            workspace.TasksReview.ForceReload();
            Assert.StartsWith(
                "Couldn’t load tasks. ", workspace.TasksReview.EmptyMessage);
            workspace.Panels.ReloadTasks();
            Assert.True(
                SpinWait.SpinUntil(
                    () => workspace.Panels.TasksEmptyMessage is { } message
                        && message.StartsWith(
                            "Could not load tasks: ", StringComparison.Ordinal),
                    TimeSpan.FromSeconds(20)),
                "the panel quarantine never surfaced");
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                "SLATE_TEST_FAULT_AFTER_WRITE", null);
            Environment.SetEnvironmentVariable(
                "SLATE_TEST_FAULT_REINDEX", null);
        }

        // Faults cleared: both surfaces repair and converge to disk
        // truth — the manually completed task is real on both.
        workspace.TasksReview.ForceReload();
        Assert.Null(workspace.TasksReview.EmptyMessage);
        Assert.Contains(
            workspace.TasksReview.Rows,
            r => r.Path == "manual-probe.md"
                && r.Task.Text == "done manually"
                && r.Task.Completed);
        workspace.Panels.ReloadTasks();
        Assert.True(
            SpinWait.SpinUntil(
                () => workspace.Panels.DoneTasks.Any(
                    r => r.Task.Text == "done manually"),
                TimeSpan.FromSeconds(20)),
            "the panel never converged to disk truth");
    }

    [Fact]
    public void DirectWritesReconcileTabsThatRacedOpen()
    {
        var announced = new List<A11yEvent>();
        using var workspace = new WorkspaceViewModel(
            _session,
            _fixture.Root,
            () => [],
            announced.Add,
            startInteractionBackgroundWork: false);
        workspace.OpenPath("todo.md");
        WorkspaceTabViewModel tab = Assert.IsType<WorkspaceTabViewModel>(
            workspace.ActiveGroup.ActiveTab);

        // The round-3 race, staged deterministically: the review's
        // NoOpenTab route was decided, THEN this tab opened, THEN
        // the direct write landed — the workspace re-checks at write
        // completion through the DiskWriteLanded seam.
        SaveReport report = _session.SaveText(
            "todo.md",
            "# Todo\n\n- [ ] rewritten elsewhere \U0001F4C5 2026-01-01\n",
            null);
        workspace.TasksReview.DiskWriteLanded!("todo.md", report.NewContentHash);

        // The raced-open tab is a stale editor over changed disk:
        // the splice path's divergence honesty, verbatim.
        A11yEvent.HostComposed diverged = announced
            .OfType<A11yEvent.HostComposed>()
            .Single(composed => composed.Text.StartsWith(
                "Task toggled on disk", StringComparison.Ordinal));
        Assert.Equal(
            "Task toggled on disk, but the editor no longer matches it. Reopen the note before editing.",
            diverged.Text);
        Assert.Equal(diverged.Text, tab.Status);

        // Round 17: the raced-open note PANEL finished a pre-write
        // read too — the write completion re-snapshots it, so its
        // rows converge to disk truth instead of retaining obsolete
        // checkboxes until a manual refresh.
        Assert.True(
            SpinWait.SpinUntil(
                () => workspace.Panels.OpenTasks.Any(
                    r => r.Task.Text == "rewritten elsewhere"),
                TimeSpan.FromSeconds(20)),
            "the raced-open panel never converged to disk truth");
    }

    /// <summary>Disk poll tolerant of the session's atomic
    /// temp+rename write: a read that lands mid-rename throws a
    /// sharing violation out of the SpinWait lambda.</summary>
    private static bool DiskContains(string path, string needle)
    {
        try
        {
            return File.ReadAllText(path).Contains(needle);
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static void PumpDispatcherUntil(Func<bool> condition, string reason) =>
        PumpDispatcherUntil(condition, () => reason);

    private static void PumpDispatcherUntil(Func<bool> condition, Func<string> reason)
    {
        // 60s: on a loaded CI runner the thread pool injects threads
        // at ~1/500ms, so a queued Task.Run toggle worker can wait
        // tens of seconds before its write even starts.
        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(60);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            var frame = new System.Windows.Threading.DispatcherFrame();
            var timer = new System.Windows.Threading.DispatcherTimer(
                System.Windows.Threading.DispatcherPriority.Send)
            {
                Interval = TimeSpan.FromMilliseconds(50),
            };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                frame.Continue = false;
            };
            timer.Start();
            System.Windows.Threading.Dispatcher.PushFrame(frame);
        }
        Assert.True(condition(), reason());
    }

    [Fact]
    public void DisplayBoundsWhileTaskDataStaysExact()
    {
        string giant = new('t', 1024 * 1024);
        var row = new NoteTaskRowViewModel(
            new TaskItem(
                Ordinal: 0, Text: giant, StatusChar: " ", Completed: false,
                DueMs: null, ScheduledMs: null, Priority: null, Recurrence: null,
                Line: 1, ByteOffset: 0, CheckboxStartByte: 2, CheckboxEndByte: 5),
            contentHash: "hash");

        Assert.True(row.DisplayText.Length <= 4097);
        Assert.True(row.AutomationName.Length <= 4300);
        Assert.Equal(1024 * 1024, row.Task.Text.Length);
    }
}
