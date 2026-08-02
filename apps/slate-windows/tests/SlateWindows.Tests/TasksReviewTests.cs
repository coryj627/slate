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
        List<string>? opens = null,
        bool openSucceeds = true,
        string? activePath = null,
        Func<string, TaskItem, string, ReviewToggleRoute>? toggleViaTab = null,
        List<TaskItem>? scrolls = null)
    {
        return new TasksReviewViewModel(
            _session,
            (announced ?? []).Add,
            (path, _) =>
            {
                opens?.Add(path);
                return openSucceeds;
            },
            () => activePath,
            task => scrolls?.Add(task),
            toggleViaTab ?? ((_, _, _) => ReviewToggleRoute.NoOpenTab),
            () => Clock,
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
        var opens = new List<string>();
        var scrolls = new List<TaskItem>();
        var review = MakeReview(
            announced, opens, activePath: "a.md", scrolls: scrolls);
        review.EnsureLoaded();
        ReviewTaskRowViewModel sameFile = review.Rows.First(
            r => r.Path == "a.md");
        ReviewTaskRowViewModel crossFile = review.Rows.First(
            r => r.Path == "b.md");

        // Same file: scroll in place, "Scrolled to …" — no open.
        review.OpenRow(sameFile);
        Assert.Empty(opens);
        Assert.Single(scrolls);
        var scrolled = Assert.Single(announced.OfType<A11yEvent.ScrolledToLine>());
        Assert.Equal("a.md", scrolled.Filename);

        // Cross file: open first, then scroll, "Opened …".
        review.OpenRow(crossFile);
        Assert.Equal("b.md", Assert.Single(opens));
        Assert.Equal(2, scrolls.Count);
        var opened = Assert.Single(announced.OfType<A11yEvent.OpenedAtLine>());
        Assert.Equal("b.md", opened.Filename);
    }

    [Fact]
    public void RefusedOpensAnnounceNothing()
    {
        var announced = new List<A11yEvent>();
        var opens = new List<string>();
        var review = MakeReview(announced, opens, openSucceeds: false);
        review.EnsureLoaded();

        review.OpenRow(review.Rows.First(r => r.Path == "b.md"));
        Assert.Single(opens);
        Assert.Empty(announced.OfType<A11yEvent.OpenedAtLine>());
        Assert.Empty(announced.OfType<A11yEvent.ScrolledToLine>());
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
