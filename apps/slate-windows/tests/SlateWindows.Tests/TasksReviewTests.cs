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
        Func<string, TaskItem, bool>? toggleViaTab = null,
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
            toggleViaTab ?? ((_, _) => false),
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
        var review = MakeReview(toggleViaTab: (_, _) => true);
        review.EnsureLoaded();
        int before = review.LoadRequestIdForTests;

        // The snapshot never chases unrelated saves...
        review.NoteRefreshed("a.md");
        Assert.Equal(before, review.LoadRequestIdForTests);

        // ...but a tab-routed toggle marks its refresh pending, and
        // the note's whole-document refresh lands it.
        review.ToggleTask(review.Rows[0]);
        review.NoteRefreshed(review.Rows[0].Path);
        Assert.Equal(before + 1, review.LoadRequestIdForTests);

        // One refresh per pending toggle, not a standing subscription.
        review.NoteRefreshed(review.Rows[0].Path);
        Assert.Equal(before + 1, review.LoadRequestIdForTests);
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
