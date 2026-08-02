// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows;
using SlateWindows.Panels;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W4-3 (#735): the vault-wide Tasks Review leaf — the mac twin is
/// TasksReviewPanel.swift. Labels are the mac strings verbatim;
/// filter windows are UTC-midnight based on both platforms.
/// </summary>
public sealed class TasksReviewTests : IDisposable
{
    // A fixed clock: 2026-03-02 12:00 UTC — "today" is 2026-03-02.
    private static readonly DateTimeOffset Clock =
        new(2026, 3, 2, 12, 0, 0, TimeSpan.Zero);

    private readonly FixtureVault _fixture;
    private readonly VaultSession _session;

    public TasksReviewTests()
    {
        _fixture = FixtureVault.Create(0, "tasks-review");
        File.WriteAllText(
            Path.Combine(_fixture.Root, "a.md"),
            "- [ ] overdue one 📅 2026-03-01\n"
                + "- [ ] due today 📅 2026-03-02\n"
                + "- [ ] later this week 📅 2026-03-05\n"
                + "- [ ] undated\n"
                + "- [x] finished 📅 2026-03-01\n");
        File.WriteAllText(
            Path.Combine(_fixture.Root, "b.md"), "- [ ] from b\n");
        // Paging: more than one 200-row page.
        var many = new System.Text.StringBuilder();
        for (int i = 0; i < 230; i++)
        {
            many.Append("- [ ] bulk ").Append(i).Append('\n');
        }
        File.WriteAllText(
            Path.Combine(_fixture.Root, "many.md"), many.ToString());
        _session = VaultSession.OpenFilesystem(_fixture.Root);
        using var cancel = new CancelToken();
        _session.ScanInitial(cancel);
    }

    public void Dispose()
    {
        _session.Dispose();
        _fixture.Dispose();
    }

    private TasksReviewViewModel MakeReview(
        List<A11yEvent>? announced = null,
        Func<string, TaskItem, string, ReviewOpenRoute>? activateRow = null,
        Func<string, TaskItem, string, ReviewToggleRoute>? toggleViaTab = null,
        Func<DateTimeOffset>? clock = null,
        TaskIndexRepairCoordinator? repairs = null)
    {
        return new TasksReviewViewModel(
            _session,
            (announced ?? []).Add,
            activateRow ?? ((_, _, _) => ReviewOpenRoute.Opened),
            toggleViaTab ?? ((_, _, _) => ReviewToggleRoute.NoOpenTab),
            clock ?? (() => Clock),
            repairs,
            synchronousForTests: true);
    }

    [Fact]
    public void FirstPageLoadsWithTheMacHeaderAndRowLabels()
    {
        var review = MakeReview();
        review.EnsureLoaded();

        // 236 open+done tasks across three files; one page of 200.
        Assert.Equal(200, review.Rows.Count);
        Assert.True(review.HasMore);
        Assert.Equal("Tasks Review, showing 200 of 236", review.Header);

        // Order: due ASC NULLS LAST — the overdue task leads, and its
        // review label leads with the FILENAME.
        ReviewTaskRowViewModel first = review.Rows[0];
        Assert.Equal("a.md", first.FileName);
        Assert.Equal(
            "a.md. overdue one. Due 2026-03-01. Open task.",
            first.AutomationName);
        Assert.Equal(
            "Opens the source note at this task's line.",
            first.AutomationHelpText);

        review.LoadMore();
        Assert.Equal(236, review.Rows.Count);
        Assert.False(review.HasMore);
        Assert.Equal("Tasks Review, 236 shown", review.Header);
    }

    [Fact]
    public void FiltersMapTheMacUtcWindows()
    {
        var announced = new List<A11yEvent>();
        var review = MakeReview(announced);
        review.EnsureLoaded();

        review.ApplyFilter(TaskReviewFilter.Overdue);
        var set = Assert.Single(announced.OfType<A11yEvent.TasksFilterSet>());
        Assert.Equal("Overdue", set.FilterName);
        ReviewTaskRowViewModel overdue = Assert.Single(review.Rows);
        Assert.Equal("overdue one", overdue.Task.Text);

        review.ApplyFilter(TaskReviewFilter.DueToday);
        Assert.Equal("due today", Assert.Single(review.Rows).Task.Text);
        Assert.Equal(
            "Due today, 1 task",
            review.FilterAutomationName(TaskReviewFilter.DueToday));
        Assert.Equal(
            "Overdue", review.FilterAutomationName(TaskReviewFilter.Overdue));

        review.ApplyFilter(TaskReviewFilter.ThisWeek);
        // [today, +7d): due today + later this week; overdue and the
        // completed task are out; undated is out.
        Assert.Equal(2, review.Rows.Count);

        // Back to All: everything including done, page-capped again.
        review.ApplyFilter(TaskReviewFilter.All);
        Assert.Equal(200, review.Rows.Count);
        Assert.Equal("Tasks Review, showing 200 of 236", review.Header);
    }

    [Fact]
    public void EmptyFilterSpeaksTheMacSentence()
    {
        var review = MakeReview();
        review.EnsureLoaded();
        // Overdue after completing: complete the only overdue task
        // via a direct session write, then re-filter.
        review.ApplyFilter(TaskReviewFilter.Overdue);
        Assert.Single(review.Rows);
        _ = _session.ToggleTaskStatus("a.md", 0, "x", null);
        review.ForceReload();
        Assert.Empty(review.Rows);
        Assert.Equal("No tasks matching Overdue.", review.EmptyMessage);
    }

    [Fact]
    public void EnsureLoadedIsIdempotentAndForceReloadIsNot()
    {
        var review = MakeReview();
        review.EnsureLoaded();
        int requestAfterFirst = review.LoadRequestIdForTests;
        review.EnsureLoaded();
        Assert.Equal(requestAfterFirst, review.LoadRequestIdForTests);
        review.ForceReload();
        Assert.Equal(requestAfterFirst + 1, review.LoadRequestIdForTests);
    }

    [Fact]
    public void DirectToggleAnnouncesAndRequeriesPageOne()
    {
        var announced = new List<A11yEvent>();
        var review = MakeReview(announced);
        review.EnsureLoaded();
        review.ApplyFilter(TaskReviewFilter.Overdue);
        ReviewTaskRowViewModel row = Assert.Single(review.Rows);

        // No open tab (the seam returns false): the session toggles
        // directly, success is spoken, and page one re-queries —
        // completing the overdue task drops it from Overdue.
        review.ToggleTask(row);
        var composed = announced.OfType<A11yEvent.HostComposed>()
            .Select(c => c.Text)
            .ToList();
        Assert.Contains("Task completed.", composed);
        Assert.Empty(review.Rows);

        string content = File.ReadAllText(Path.Combine(_fixture.Root, "a.md"));
        Assert.Contains("- [x] overdue one", content);
    }

    [Fact]
    public void TabRoutedTogglesRefreshOnlyOnTheirOwnNoteRefresh()
    {
        var review = MakeReview(
            toggleViaTab: (_, _, _) => ReviewToggleRoute.Started);
        review.EnsureLoaded();
        int before = review.LoadRequestIdForTests;

        // The snapshot never chases unrelated saves...
        review.NoteRefreshed("a.md");
        Assert.Equal(before, review.LoadRequestIdForTests);

        // ...but a STARTED tab-routed toggle marks its own path
        // pending, and that note's whole-document refresh lands it.
        review.ToggleTask(review.Rows[0]);
        review.NoteRefreshed("some-other.md");
        Assert.Equal(before, review.LoadRequestIdForTests);
        review.NoteRefreshed(review.Rows[0].Path);
        Assert.Equal(before + 1, review.LoadRequestIdForTests);

        // One refresh per pending toggle, not a standing subscription.
        review.NoteRefreshed(review.Rows[0].Path);
        Assert.Equal(before + 1, review.LoadRequestIdForTests);
    }

    [Fact]
    public void RefusalsNeverArmTheRefreshFlag()
    {
        // Round 1: a lossy bool armed the pending refresh for DIRTY
        // refusals, letting any later refresh reset paging.
        var review = MakeReview(
            toggleViaTab: (_, _, _) => ReviewToggleRoute.RefusedDirty);
        review.EnsureLoaded();
        int before = review.LoadRequestIdForTests;

        review.ToggleTask(review.Rows[0]);
        review.NoteRefreshed(review.Rows[0].Path);
        Assert.Equal(before, review.LoadRequestIdForTests);
    }

    [Fact]
    public void StaleTabRefusalsReloadTheSnapshot()
    {
        var review = MakeReview(
            toggleViaTab: (_, _, _) => ReviewToggleRoute.RefusedStale);
        review.EnsureLoaded();
        int before = review.LoadRequestIdForTests;

        review.ToggleTask(review.Rows[0]);
        Assert.Equal(before + 1, review.LoadRequestIdForTests);
    }

    [Fact]
    public void StaleSnapshotTogglesConflictInsteadOfMutatingTheWrongTask()
    {
        var announced = new List<A11yEvent>();
        var review = MakeReview(announced);
        review.EnsureLoaded();
        review.ApplyFilter(TaskReviewFilter.Overdue);
        ReviewTaskRowViewModel row = Assert.Single(review.Rows);

        // The file changes AFTER the snapshot: a task is inserted
        // BEFORE the clicked one, so the row's ordinal now names a
        // different task. The toggle must refuse — the round-1
        // failure completed whichever task inherited the ordinal.
        _ = _session.SaveText(
            "a.md",
            "- [ ] brand new first 📅 2026-03-01\n".Replace("\\n", "\n")
                + File.ReadAllText(Path.Combine(_fixture.Root, "a.md")),
            null);

        review.ToggleTask(row);

        _ = Assert.Single(announced.OfType<A11yEvent.TaskToggleConflict>());
        Assert.DoesNotContain(
            announced.OfType<A11yEvent.HostComposed>(),
            composed => composed.Text == "Task completed.");
        // Nothing beyond the pre-existing done task was completed.
        string content = File.ReadAllText(Path.Combine(_fixture.Root, "a.md"));
        Assert.DoesNotContain(
            "- [x]", content.Replace("- [x] finished", ""));
        // The refusal reloaded the snapshot: both overdue tasks show.
        Assert.Equal(2, review.Rows.Count);
    }

    [Fact]
    public void LoadMoreReloadsWhenTheIndexMovedUnderTheCursor()
    {
        var review = MakeReview();
        review.EnsureLoaded();
        Assert.True(review.HasMore);
        int before = review.LoadRequestIdForTests;

        // A vault mutation between pages: the cursor belongs to a
        // dead snapshot — appending against the live index can
        // duplicate moved rows and skip others (round 1).
        _ = _session.SaveText("b.md", "- [ ] from b, edited\n".Replace("\\n", "\n"), null);

        review.LoadMore();
        Assert.Equal(before + 1, review.LoadRequestIdForTests);
        Assert.Equal(200, review.Rows.Count);
        Assert.False(review.IsLoadingMore);
    }

    [Fact]
    public void LoadMoreDetectsWritesThatLandBetweenCheckAndQuery()
    {
        var review = MakeReview();
        review.EnsureLoaded();
        Assert.True(review.HasMore);
        int before = review.LoadRequestIdForTests;

        // Round 2: the round-1 guard checked the generation only
        // BEFORE the query — a save landing in that window let the
        // dead cursor read the mutated index with drift undetected.
        // The seam commits a save exactly inside the window.
        review.InterleaveForTests = () =>
        {
            review.InterleaveForTests = null;
            _ = _session.SaveText("b.md", "- [ ] from b, edited\n", null);
        };
        review.LoadMore();

        Assert.Equal(before + 1, review.LoadRequestIdForTests);
        Assert.Equal(200, review.Rows.Count);
        Assert.False(review.IsLoadingMore);
    }

    [Fact]
    public void FirstPagesBindTheGenerationTheirRowsCameFrom()
    {
        var review = MakeReview();
        bool interleaved = false;
        review.InterleaveForTests = () =>
        {
            if (interleaved)
            {
                return;
            }
            interleaved = true;
            _ = _session.SaveText("b.md", "- [ ] from b, edited\n", null);
        };
        review.EnsureLoaded();
        Assert.True(interleaved);

        // The first-page worker retried past the interleaved write
        // and stored a generation matching its rows — so the next
        // load-more APPENDS (no false drift, no paging reset).
        int before = review.LoadRequestIdForTests;
        review.InterleaveForTests = null;
        review.LoadMore();
        Assert.Equal(before, review.LoadRequestIdForTests);
        Assert.Equal(236, review.Rows.Count);
    }

    [Fact]
    public void BusyTabRefusalsNeverArmTheRefreshFlag()
    {
        // Round 2: the tab's legacy bool reported its busy refusal
        // as true, which the round-1 mapping read as Started — the
        // review armed a refresh for an operation that never ran.
        var review = MakeReview(
            toggleViaTab: (_, _, _) => ReviewToggleRoute.RefusedBusy);
        review.EnsureLoaded();
        int before = review.LoadRequestIdForTests;

        review.ToggleTask(review.Rows[0]);
        review.NoteRefreshed(review.Rows[0].Path);
        Assert.Equal(before, review.LoadRequestIdForTests);
    }

    [Fact]
    public void MidnightRolloverCannotChangeTheWindowUnderTheCursor()
    {
        // >200 tasks due "today" so Due today pages.
        var dated = new System.Text.StringBuilder();
        for (int i = 0; i < 230; i++)
        {
            dated.Append("- [ ] dated ").Append(i).Append(" 📅 2026-03-02\n");
        }
        _ = _session.SaveText("dated.md", dated.ToString(), null);

        DateTimeOffset now = Clock;
        var review = MakeReview(clock: () => now);
        review.ApplyFilter(TaskReviewFilter.DueToday);
        Assert.Equal(200, review.Rows.Count);
        Assert.True(review.HasMore);
        int before = review.LoadRequestIdForTests;

        // UTC midnight passes between pages (adversarial round 3):
        // recomputing the window from the live clock would page a
        // DIFFERENT population — "due today" is now empty — while
        // the interaction generation never moved.
        now = new DateTimeOffset(2026, 3, 3, 0, 30, 0, TimeSpan.Zero);
        review.LoadMore();

        // The cursor paged the SNAPSHOT's window: every dated task
        // plus a.md's "due today" appended, no reload, no reset.
        Assert.Equal(before, review.LoadRequestIdForTests);
        Assert.Equal(231, review.Rows.Count);
        Assert.False(review.IsLoadingMore);
    }

    [Fact]
    public void FilterChipsExposeExactlyOneActiveSelection()
    {
        var announced = new List<A11yEvent>();
        var review = MakeReview(announced);
        review.EnsureLoaded();
        Assert.True(review.AllFilterActive);
        Assert.False(review.OverdueFilterActive);

        // The radio group's checked write commits the filter…
        review.OverdueFilterActive = true;
        Assert.Equal(TaskReviewFilter.Overdue, review.ActiveFilter);
        Assert.True(review.OverdueFilterActive);
        Assert.False(review.AllFilterActive);
        var set = Assert.Single(announced.OfType<A11yEvent.TasksFilterSet>());
        Assert.Equal("Overdue", set.FilterName);

        // …the sibling UNCHECK the group writes is ignored, and
        // re-checking the active chip is a no-op (no re-announce).
        review.AllFilterActive = false;
        review.OverdueFilterActive = true;
        Assert.Equal(TaskReviewFilter.Overdue, review.ActiveFilter);
        _ = Assert.Single(announced.OfType<A11yEvent.TasksFilterSet>());
    }

    [Fact]
    public void AbandonedTogglesDisarmTheirPendingRefresh()
    {
        // Round 3: a Started toggle whose write never landed (the
        // originating tab closed, the save failed) must not leave
        // the refresh armed for a later unrelated save to trip.
        var review = MakeReview(
            toggleViaTab: (_, _, _) => ReviewToggleRoute.Started);
        review.EnsureLoaded();
        int before = review.LoadRequestIdForTests;

        review.ToggleTask(review.Rows[0]);
        review.ToggleAbandoned(review.Rows[0].Path);
        review.NoteRefreshed(review.Rows[0].Path);
        Assert.Equal(before, review.LoadRequestIdForTests);
    }

    [Fact]
    public void FailedFilterChangesCannotLeaveOldRowsActionable()
    {
        var review = MakeReview();
        review.EnsureLoaded();
        Assert.Equal(200, review.Rows.Count);
        Assert.True(review.HasMore);

        // The chip commits Overdue but its load fails (adversarial
        // round 4): the old All rows and cursor must not stay
        // actionable under the new chip — mac hides the list here.
        review.InterleaveForTests = () =>
            throw new InvalidOperationException("boom");
        review.ApplyFilter(TaskReviewFilter.Overdue);

        Assert.Empty(review.Rows);
        Assert.False(review.HasMore);
        Assert.Equal("Couldn’t load tasks. boom", review.EmptyMessage);
        int requests = review.LoadRequestIdForTests;
        review.LoadMore();
        Assert.Equal(requests, review.LoadRequestIdForTests);
        Assert.Empty(review.Rows);

        // A successful retry publishes the NEW filter's population
        // and clears the transient error.
        review.InterleaveForTests = null;
        review.ForceReload();
        ReviewTaskRowViewModel row = Assert.Single(review.Rows);
        Assert.Equal("overdue one", row.Task.Text);
        Assert.Null(review.EmptyMessage);
    }

    [Fact]
    public void FilterTransitionsNeverLeaveOldRowsActionableWhileLoading()
    {
        var review = MakeReview();
        review.EnsureLoaded();
        Assert.Equal(200, review.Rows.Count);

        // Observed INSIDE the in-flight window of the Overdue load
        // (adversarial round 5): the All population must already be
        // gone — mislabeled rows under the new chip were clickable
        // until publication.
        int rowsDuringLoad = -1;
        bool hasMoreDuringLoad = true;
        review.InterleaveForTests = () =>
        {
            rowsDuringLoad = review.Rows.Count;
            hasMoreDuringLoad = review.HasMore;
        };
        review.ApplyFilter(TaskReviewFilter.Overdue);
        review.InterleaveForTests = null;

        Assert.Equal(0, rowsDuringLoad);
        Assert.False(hasMoreDuringLoad);
        ReviewTaskRowViewModel row = Assert.Single(review.Rows);
        Assert.Equal("overdue one", row.Task.Text);
    }

    [Fact]
    public void LoadMoreRecoveryClearsTheFailureBanner()
    {
        var review = MakeReview();
        review.EnsureLoaded();

        review.InterleaveForTests = () =>
        {
            review.InterleaveForTests = null;
            throw new InvalidOperationException("boom");
        };
        review.LoadMore();
        Assert.Equal("Couldn’t load tasks. boom", review.EmptyMessage);
        Assert.Equal(200, review.Rows.Count);

        // A successful retry is a recovery: the banner clears
        // (adversarial round 5: it reported vault failure forever).
        review.LoadMore();
        Assert.Equal(236, review.Rows.Count);
        Assert.Null(review.EmptyMessage);
    }

    [Fact]
    public void FailedSameFilterRefreshesKeepTheRowsTheyHad()
    {
        var review = MakeReview();
        review.EnsureLoaded();
        int rows = review.Rows.Count;

        // A same-filter refresh failure is a transient fault over a
        // still-valid population: rows survive (the round-2 posture,
        // narrowed by round 4 to same-filter only).
        review.InterleaveForTests = () =>
            throw new InvalidOperationException("boom");
        review.ForceReload();
        review.InterleaveForTests = null;

        Assert.Equal(rows, review.Rows.Count);
        Assert.Equal("Couldn’t load tasks. boom", review.EmptyMessage);
    }

    [Fact]
    public void PostWriteFailuresStillRefreshTheSnapshot()
    {
        // Adversarial round 11: the core writes the FILE before
        // committing the index, so an error without a SaveReport
        // does NOT mean disk is unchanged. The fault seam throws
        // AFTER the real write landed — the honest outcome is the
        // failure announcement PLUS reconciliation, never a clean
        // "nothing happened".
        var announced = new List<A11yEvent>();
        var review = MakeReview(announced);
        review.EnsureLoaded();
        ReviewTaskRowViewModel row = review.Rows.First(
            r => r.Path == "a.md" && r.Task.Text == "overdue one");
        var reconciled = new List<(string Path, string Hash)>();
        review.DiskWriteLanded = (path, hash) => reconciled.Add((path, hash));
        review.DirectToggleFaultForTests = () =>
            throw new InvalidOperationException("index commit failed");

        review.ToggleTask(row);

        // The write landed before the injected failure…
        Assert.Contains(
            "- [x] overdue one",
            File.ReadAllText(Path.Combine(_fixture.Root, "a.md")));
        // …the failure is spoken, with no success verb…
        _ = announced.OfType<A11yEvent.HostComposed>().Single(composed =>
            composed.Text == "Task could not be toggled: index commit failed");
        Assert.DoesNotContain(
            announced.OfType<A11yEvent.HostComposed>(),
            composed => composed.Text == "Task completed.");
        // …and the surfaces reconcile instead of assuming unchanged:
        // the workspace seam heard the moved hash and the snapshot
        // re-queried, so the toggled row reads Done and a retry
        // cannot conflict against ghost state.
        (string reconciledPath, string reconciledHash) = Assert.Single(reconciled);
        Assert.Equal("a.md", reconciledPath);
        Assert.NotEqual(row.ContentHash, reconciledHash);
        Assert.Contains(
            review.Rows,
            r => r.Path == "a.md"
                && r.Task.Text == "overdue one"
                && r.Task.Completed);
    }

    [Fact]
    public void RealPreCommitFailuresRepairTheIndexBeforeReloading()
    {
        using var _envLock = EnvFaultLock.Acquire();
        // Adversarial round 12: the round-11 seam injected AFTER the
        // core committed, so it never exercised the REAL window —
        // file written, index rolled back. The core's own fault seam
        // (env-var path trigger, unique to this fixture file) trips
        // exactly there; the recovery must repair the index before
        // reloading, or the rows resurrect the pre-write state.
        _ = _session.SaveText("fault-pre-commit.md", "- [ ] flip me\n", null);
        var announced = new List<A11yEvent>();
        var review = MakeReview(announced);
        review.EnsureLoaded();
        ReviewTaskRowViewModel row = review.Rows.First(
            r => r.Path == "fault-pre-commit.md");

        Environment.SetEnvironmentVariable(
            "SLATE_TEST_FAULT_AFTER_WRITE", "fault-pre-commit");
        try
        {
            review.ToggleTask(row);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                "SLATE_TEST_FAULT_AFTER_WRITE", null);
        }

        // Disk flipped, the failure spoke with no success verb…
        string diskPath = Path.Combine(_fixture.Root, "fault-pre-commit.md");
        Assert.Contains("- [x] flip me", File.ReadAllText(diskPath));
        _ = announced.OfType<A11yEvent.HostComposed>().Single(composed =>
            composed.Text.StartsWith(
                "Task could not be toggled:", StringComparison.Ordinal)
            && composed.Text.Contains("test fault"));
        Assert.DoesNotContain(
            announced.OfType<A11yEvent.HostComposed>(),
            composed => composed.Text == "Task completed.");

        // …and the reload shows DISK truth, not the rolled-back
        // index's ghost: the repair ran first.
        ReviewTaskRowViewModel repaired = review.Rows.First(
            r => r.Path == "fault-pre-commit.md");
        Assert.True(repaired.Task.Completed);

        // A retry is a clean fresh toggle, not a ghost conflict.
        review.ToggleTask(repaired);
        Assert.Empty(announced.OfType<A11yEvent.TaskToggleConflict>());
        Assert.Contains("- [ ] flip me", File.ReadAllText(diskPath));
    }

    [Fact]
    public void FailedRepairsNeverReloadTheStaleIndex()
    {
        using var _envLock = EnvFaultLock.Acquire();
        // Adversarial round 14: after a post-write failure the index
        // is KNOWN stale — if the repair itself fails, reloading
        // republishes the rolled-back rows as ghosts. Round 31: a
        // repair failing on a FILE condition resolves to CONTAINMENT
        // — the core drops the path's suspect rows honest-empty —
        // so the reload proceeds but the ghost open row can never
        // publish; disk truth returns once the file reads again.
        _ = _session.SaveText("fault-pre-commit.md", "- [ ] flip me\n", null);
        var announced = new List<A11yEvent>();
        var review = MakeReview(announced);
        review.EnsureLoaded();
        ReviewTaskRowViewModel row = review.Rows.First(
            r => r.Path == "fault-pre-commit.md");

        Environment.SetEnvironmentVariable(
            "SLATE_TEST_FAULT_AFTER_WRITE", "fault-pre-commit");
        Environment.SetEnvironmentVariable(
            "SLATE_TEST_FAULT_REINDEX", "fault-pre-commit");
        try
        {
            review.ToggleTask(row);

            // Disk flipped; the contained path serves NOTHING — the
            // one thing that must never appear is the rolled-back
            // ghost open row.
            Assert.Contains(
                "- [x] flip me",
                File.ReadAllText(Path.Combine(_fixture.Root, "fault-pre-commit.md")));
            Assert.DoesNotContain(
                review.Rows, r => r.Path == "fault-pre-commit.md");

            // With the fault STILL HELD, reload paths keep serving
            // the contained state — honest-empty for this path,
            // unrelated rows intact, never the ghost.
            review.ForceReload();
            Assert.DoesNotContain(
                review.Rows, r => r.Path == "fault-pre-commit.md");
            Assert.Contains(review.Rows, r => r.Path != "fault-pre-commit.md");

            review.LoadMore();
            Assert.DoesNotContain(
                review.Rows, r => r.Path == "fault-pre-commit.md");
            Assert.False(review.IsLoadingMore);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                "SLATE_TEST_FAULT_AFTER_WRITE", null);
            Environment.SetEnvironmentVariable(
                "SLATE_TEST_FAULT_REINDEX", null);
        }

        // Once the fault clears, the planted marker's sweep repairs
        // on the next query and converges to disk truth.
        review.ForceReload();
        Assert.True(
            review.Rows.First(r => r.Path == "fault-pre-commit.md")
                .Task.Completed);
        Assert.Null(review.EmptyMessage);
    }

    [Fact]
    public void OtherSessionsWritingTheSharedIndexDriftThePaging()
    {
        // Adversarial round 14: the session-local generation cannot
        // see another PROCESS committing to the shared cache DB — a
        // second session stands in for it. The revision folds
        // SQLite's data_version in, so the cursor drifts and reloads
        // instead of appending a mixed snapshot.
        var review = MakeReview();
        review.EnsureLoaded();
        Assert.True(review.HasMore);
        int before = review.LoadRequestIdForTests;

        using var otherSession = VaultSession.OpenFilesystem(_fixture.Root);
        review.InterleaveForTests = () =>
        {
            review.InterleaveForTests = null;
            _ = otherSession.SaveText("b.md", "- [ ] from b, moved\n", null);
        };
        review.LoadMore();

        Assert.Equal(before + 1, review.LoadRequestIdForTests);
        Assert.Equal(200, review.Rows.Count);
        Assert.False(review.IsLoadingMore);
    }

    [Fact]
    public void SweepsNeverEraseFailuresRegisteredMidRepair()
    {
        // Adversarial round 16: an unversioned removal let a sweep
        // that repaired a path erase a FRESH failure registered
        // concurrently — the quarantine went silently empty while
        // the index was stale again.
        var coordinator = new TaskIndexRepairCoordinator(_session);
        coordinator.NotePending("a.md");
        coordinator.BetweenRepairAndRemovalForTests = () =>
        {
            coordinator.BetweenRepairAndRemovalForTests = null;
            coordinator.NotePending("a.md");
        };

        RepairSweep sweep = coordinator.Retry();

        Assert.True(coordinator.HasPendingFor("a.md"));
        Assert.True(sweep.AnyPending);
        Assert.Empty(sweep.Repaired);
    }

    [Fact]
    public void QuarantineRegistrationsDuringTheQueryDiscardTheResult()
    {
        // Adversarial round 16: a post-write failure that registers
        // between the sweep and the query moves no revision counter
        // (its index transaction rolled back) — only the quarantine
        // epoch can prove the rows came from a clean index.
        var repairs = new TaskIndexRepairCoordinator(_session);
        var review = MakeReview(repairs: repairs);
        review.EnsureLoaded();
        int rows = review.Rows.Count;

        review.InterleaveForTests = () =>
        {
            review.InterleaveForTests = null;
            repairs.NotePending("a.md");
        };
        review.ForceReload();

        // The freshly queried page was discarded: the last honest
        // snapshot stays behind the failure banner.
        Assert.Equal(rows, review.Rows.Count);
        Assert.StartsWith("Couldn’t load tasks. ", review.EmptyMessage);

        // The pending path repairs on the next load and rows return.
        review.ForceReload();
        Assert.Null(review.EmptyMessage);
        Assert.Equal(rows, review.Rows.Count);
    }

    [Fact]
    public void UnreadableReadBacksQuarantineInsteadOfAssumingNoWrite()
    {
        // Adversarial round 16: core can write the file before its
        // index transaction fails, and the read-back can ALSO fail —
        // an unknown outcome must fail CLOSED, not read as "no
        // write happened". The fault hook deletes the file after the
        // real write so the read-back genuinely fails.
        _ = _session.SaveText("fault-unknown.md", "- [ ] flip me\n", null);
        var announced = new List<A11yEvent>();
        var review = MakeReview(announced);
        review.EnsureLoaded();
        ReviewTaskRowViewModel row = review.Rows.First(
            r => r.Path == "fault-unknown.md");
        string diskPath = Path.Combine(_fixture.Root, "fault-unknown.md");

        review.DirectToggleFaultForTests = () =>
        {
            review.DirectToggleFaultForTests = null;
            File.Delete(diskPath);
            throw new InvalidOperationException("boom after write");
        };
        review.ToggleTask(row);

        // Unknown outcome → fail-closed repair. With the file GONE,
        // the repair CONVERGES TO DELETION (round 23) instead of
        // barring forever: no ghost open row survives, no banner —
        // the honest truth is that the note no longer exists.
        review.ForceReload();
        Assert.Null(review.EmptyMessage);
        Assert.DoesNotContain(review.Rows, r => r.Path == "fault-unknown.md");
        Assert.DoesNotContain(
            announced.OfType<A11yEvent.HostComposed>(),
            composed => composed.Text == "Task completed.");

        // The note returns via an indexed write: rows show it again.
        _ = _session.SaveText("fault-unknown.md", "- [x] flip me\n", null);
        review.ForceReload();
        Assert.True(
            review.Rows.First(r => r.Path == "fault-unknown.md")
                .Task.Completed);
    }

    [Fact]
    public void SuccessfulFirstRepairsAdvanceTheEpoch()
    {
        // Adversarial round 17: a repair of a path that was never
        // pending must still advance the epoch — its interval is
        // exactly what an overlapping query needs to see, or a page
        // read from the rolled-back index publishes ghosts AFTER
        // the repair with nothing left to refresh them.
        var coordinator = new TaskIndexRepairCoordinator(_session);
        long before = coordinator.Epoch;

        Assert.True(coordinator.TryRepairNow("a.md", out string? error));

        Assert.Null(error);
        Assert.NotEqual(before, coordinator.Epoch);
        Assert.False(coordinator.HasPendingFor("a.md"));
    }

    [Fact]
    public void RepairsOverlappingAReviewQueryDiscardItsResult()
    {
        // The deterministic stand-in for the round-17 schedule: a
        // successful repair completes while a review query is in
        // flight — the query's result must be discarded, because a
        // page read before the repair would carry the pre-repair
        // ghost and no revision counter distinguishes the two.
        var repairs = new TaskIndexRepairCoordinator(_session);
        var review = MakeReview(repairs: repairs);
        review.EnsureLoaded();
        int rows = review.Rows.Count;

        bool repaired = false;
        review.InterleaveForTests = () =>
        {
            review.InterleaveForTests = null;
            repaired = repairs.TryRepairNow("a.md", out _);
        };
        review.ForceReload();

        Assert.True(repaired);
        Assert.Equal(rows, review.Rows.Count);
        Assert.StartsWith("Couldn’t load tasks. ", review.EmptyMessage);

        review.ForceReload();
        Assert.Null(review.EmptyMessage);
        Assert.Equal(rows, review.Rows.Count);
    }

    [Fact]
    public void FailingRepairsMidQueryStillDiscardTheResult()
    {
        using var _envLock = EnvFaultLock.Acquire();
        // Adversarial round 18: a repair interval registering while
        // the query is in flight advances the epoch — the atomic
        // ticket taken before the query plus the post-read
        // comparison must discard the result (the split
        // check-then-capture missed exactly this schedule). Round
        // 31: the failing repair now resolves to CONTAINMENT
        // (success for the coordinator), but its interval still
        // registers and removes, so the overlapped ticket still
        // invalidates.
        // Dated so the probe sorts into page one (round-1 lesson).
        _ = _session.SaveText(
            "quarantine-probe.md", "- [ ] probe \U0001F4C5 2026-01-01\n", null);
        var repairs = new TaskIndexRepairCoordinator(_session);
        var review = MakeReview(repairs: repairs);
        review.EnsureLoaded();
        int rows = review.Rows.Count;

        bool attemptContained = false;
        review.InterleaveForTests = () =>
        {
            review.InterleaveForTests = null;
            Environment.SetEnvironmentVariable(
                "SLATE_TEST_FAULT_REINDEX", "quarantine-probe");
            try
            {
                attemptContained = repairs.TryRepairNow(
                    "quarantine-probe.md", out _);
            }
            finally
            {
                Environment.SetEnvironmentVariable(
                    "SLATE_TEST_FAULT_REINDEX", null);
            }
        };
        review.ForceReload();

        Assert.True(attemptContained);
        Assert.Equal(rows, review.Rows.Count);
        Assert.StartsWith("Couldn’t load tasks. ", review.EmptyMessage);

        // Nothing stays pending; the next load's sweep repairs the
        // contained path (fault cleared) and converges — the probe
        // row is back.
        review.ForceReload();
        Assert.Null(review.EmptyMessage);
        Assert.Contains(review.Rows, r => r.Path == "quarantine-probe.md");
    }

    [Fact]
    public void ConcurrentRepairsNeverEraseRegistrationsOrHideIntervals()
    {
        // Adversarial round 18: overlapping same-path repairs could
        // erase the only active registration or complete invisibly.
        // Repairs are single-flight per path now (cross-thread
        // attempts block on the path lock); the same-thread reentry
        // below stands in for the overlap and pins the versioning:
        // the inner repair's interval advances the epoch, and the
        // outer sweep's stale snapshot must not double-clear or
        // resurrect anything.
        var coordinator = new TaskIndexRepairCoordinator(_session);
        coordinator.NotePending("a.md");
        long start = coordinator.Epoch;
        bool innerRepaired = false;
        coordinator.BetweenRepairAndRemovalForTests = () =>
        {
            coordinator.BetweenRepairAndRemovalForTests = null;
            innerRepaired = coordinator.TryRepairNow("a.md", out _);
        };

        RepairSweep sweep = coordinator.Retry();

        Assert.True(innerRepaired);
        Assert.False(coordinator.HasPendingFor("a.md"));
        Assert.False(sweep.AnyPending);
        // The inner interval is visible: register (+1) and remove
        // (+1) both advanced the epoch past the sweep's baseline.
        Assert.True(coordinator.Epoch >= start + 2);
        Assert.True(coordinator.TryBeginCleanQuery(out _, out _));
    }

    [Fact]
    public void TogglesOverlappingAQueryInvalidateItsTicket()
    {
        using var _envLock = EnvFaultLock.Acquire();
        // Adversarial round 19: the stale-index interval starts at
        // the file WRITE, not at the dispatcher-side completion — a
        // query whose clean ticket predates the toggle must
        // invalidate. The overlapped toggle here hits BOTH fault
        // seams, so its mutation lease converts atomically into the
        // pending repair with the epoch advanced.
        _ = _session.SaveText("lease-probe.md", "- [ ] probe\n", null);
        var repairs = new TaskIndexRepairCoordinator(_session);
        var announced = new List<A11yEvent>();
        var review = MakeReview(announced, repairs: repairs);
        review.EnsureLoaded();
        ReviewTaskRowViewModel probe = review.Rows.First(
            r => r.Path == "lease-probe.md");
        int rows = review.Rows.Count;

        review.InterleaveForTests = () =>
        {
            review.InterleaveForTests = null;
            Environment.SetEnvironmentVariable(
                "SLATE_TEST_FAULT_AFTER_WRITE", "lease-probe");
            Environment.SetEnvironmentVariable(
                "SLATE_TEST_FAULT_REINDEX", "lease-probe");
            try
            {
                review.ToggleTask(probe);
            }
            finally
            {
                Environment.SetEnvironmentVariable(
                    "SLATE_TEST_FAULT_AFTER_WRITE", null);
                Environment.SetEnvironmentVariable(
                    "SLATE_TEST_FAULT_REINDEX", null);
            }
        };
        review.ForceReload();

        // The overlapped ticket was invalidated — whatever
        // published, the one impossible outcome is the rolled-back
        // GHOST open row (round 31: containment may already serve
        // the path honest-empty instead) — and the write is on
        // disk.
        _ = rows;
        Assert.DoesNotContain(
            review.Rows,
            r => r.Path == "lease-probe.md" && !r.Task.Completed);
        Assert.Contains(
            "- [x] probe",
            File.ReadAllText(Path.Combine(_fixture.Root, "lease-probe.md")));

        // Faults cleared: the next load heals the path and
        // converges to disk truth.
        review.ForceReload();
        Assert.Null(review.EmptyMessage);
        Assert.True(
            review.Rows.First(r => r.Path == "lease-probe.md")
                .Task.Completed);
    }

    [Fact]
    public void SupersededLoadMoreCannotWedgeThePagingButton()
    {
        var review = MakeReview();
        review.EnsureLoaded();

        // A stale load-more completion (its token was invalidated by
        // a fresh first page) must neither append nor flip the flag
        // (round 1: the button stayed "Loading more tasks…" forever).
        int rows = review.Rows.Count;
        review.PublishLoadMore(
            token: -1,
            new TaskWithLocationPage([], null, 0),
            failure: null,
            drifted: false);
        Assert.Equal(rows, review.Rows.Count);
        Assert.False(review.IsLoadingMore);
        Assert.Equal("Load more tasks", review.LoadMoreLabel);
    }

    [Fact]
    public void ActivationSpeaksTheMacVerbsForWhatActuallyHappened()
    {
        var announced = new List<A11yEvent>();
        var activations = new List<(string Path, string Hash)>();
        var review = MakeReview(
            announced,
            activateRow: (path, _, hash) =>
            {
                activations.Add((path, hash));
                return path == "a.md"
                    ? ReviewOpenRoute.ScrolledInPlace
                    : ReviewOpenRoute.Opened;
            });
        review.EnsureLoaded();
        ReviewTaskRowViewModel sameFile = review.Rows.First(
            r => r.Path == "a.md");
        ReviewTaskRowViewModel crossFile = review.Rows.First(
            r => r.Path == "b.md");

        // Same file: scrolled in place, "Scrolled to …".
        review.OpenRow(sameFile);
        var scrolled = Assert.Single(announced.OfType<A11yEvent.ScrolledToLine>());
        Assert.Equal("a.md", scrolled.Filename);

        // Cross file: opened first, "Opened …".
        review.OpenRow(crossFile);
        var opened = Assert.Single(announced.OfType<A11yEvent.OpenedAtLine>());
        Assert.Equal("b.md", opened.Filename);

        // The seam receives each row's SNAPSHOT hash (round 6): the
        // workspace verifies it before any scroll.
        Assert.Equal(2, activations.Count);
        Assert.Equal(sameFile.ContentHash, activations[0].Hash);
        Assert.Equal(crossFile.ContentHash, activations[1].Hash);
        Assert.All(activations, a => Assert.NotEmpty(a.Hash));
    }

    [Fact]
    public void RefusedOpensAnnounceNothing()
    {
        var announced = new List<A11yEvent>();
        var review = MakeReview(
            announced, activateRow: (_, _, _) => ReviewOpenRoute.OpenFailed);
        review.EnsureLoaded();

        review.OpenRow(review.Rows.First(r => r.Path == "b.md"));
        Assert.Empty(announced.OfType<A11yEvent.OpenedAtLine>());
        Assert.Empty(announced.OfType<A11yEvent.ScrolledToLine>());
        Assert.Empty(announced.OfType<A11yEvent.HostComposed>());
    }

    [Fact]
    public void StaleActivationsRefuseReloadAndSayWhy()
    {
        // Round 6: the snapshot's byte offset only means anything
        // against the content it was read from — an unverified
        // scroll lands on unrelated text while announcing success.
        var announced = new List<A11yEvent>();
        var review = MakeReview(
            announced, activateRow: (_, _, _) => ReviewOpenRoute.RefusedStale);
        review.EnsureLoaded();
        int before = review.LoadRequestIdForTests;

        review.OpenRow(review.Rows.First(r => r.Path == "a.md"));

        var composed = Assert.Single(announced.OfType<A11yEvent.HostComposed>());
        Assert.Equal(
            "a.md changed since these tasks loaded. Refreshing.", composed.Text);
        Assert.Empty(announced.OfType<A11yEvent.ScrolledToLine>());
        Assert.Empty(announced.OfType<A11yEvent.OpenedAtLine>());
        Assert.Equal(before + 1, review.LoadRequestIdForTests);
    }

    [Fact]
    public void StaleFirstPagePublishesAreDiscarded()
    {
        var review = MakeReview();
        review.EnsureLoaded();
        int rows = review.Rows.Count;

        review.PublishFirstPage(
            review.LoadRequestIdForTests - 1,
            new TaskWithLocationPage([], null, 0),
            failure: null);
        Assert.Equal(rows, review.Rows.Count);
    }

    [Fact]
    public void FailedLoadsKeepRowsAndSpeakOnlyWhenEmpty()
    {
        var review = MakeReview();
        review.EnsureLoaded();
        int rows = review.Rows.Count;

        review.PublishFirstPage(
            review.LoadRequestIdForTests, page: null, failure: "boom");
        // Rows survive a failed reload, and the failure is spoken
        // loudly above them (the W4-2 honesty posture; the mac error
        // header wording, typographic apostrophe included).
        Assert.Equal(rows, review.Rows.Count);
        Assert.Equal("Couldn’t load tasks. boom", review.EmptyMessage);
    }
}
