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
        List<TaskItem>? scrolls = null)
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
            task => scrolls?.Add(task));
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
        var panels = new RightPanePanelsViewModel(
            session, _ => { }, (_, _) => true, _ => true, (_, _) => { },
            (_, _) => true, _ => { });
        session.Dispose();

        panels.NoteChanged("n.md");
        panels.DrainForTests().GetAwaiter().GetResult();
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
                () => File.ReadAllText(path).Contains("- [x] first open"),
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
        Assert.True(
            SpinWait.SpinUntil(
                () => File.ReadAllText(diskPath).Contains("- [x] first open"),
                TimeSpan.FromSeconds(20)),
            "the toggle never reached disk");

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
    }

    private static void PumpDispatcherUntil(Func<bool> condition, string reason)
    {
        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);
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
        Assert.True(condition(), reason);
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
