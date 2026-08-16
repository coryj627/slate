// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Concurrent;
using SlateWindows.Search;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W5-2 (#742) search-overlay view-model facts against a fake
/// <see cref="ISearchSource"/>. Synchronous-mode facts use the Quick
/// Open test seam (no debounce, no thread hop); pipeline facts use a
/// pumped <see cref="SynchronizationContext"/> plus a manually fired
/// debounce so no real timer runs in the suite.
/// </summary>
public sealed class SearchOverlayViewModelTests
{
    // ---- toggle / open / close (S8, P14 shape) --------------------------

    [Fact]
    public void ToggleWithNoVaultAnnouncesSearchNeedsVaultAndStaysClosed()
    {
        var harness = new OverlayHarness();
        harness.Source.IsVaultOpen = false;

        harness.Overlay.Toggle();

        Assert.False(harness.Overlay.IsOpen);
        Assert.IsType<A11yEvent.SearchNeedsVault>(Assert.Single(harness.Announcements));
        Assert.Equal(SearchOverlayState.Idle, harness.Overlay.State);
        Assert.Empty(harness.Source.SearchCalls);
    }

    [Fact]
    public void ToggleOpensWhenClosedAndClosesWhenOpen()
    {
        var harness = new OverlayHarness();

        harness.Overlay.Toggle();
        Assert.True(harness.Overlay.IsOpen);

        harness.Overlay.Toggle();
        Assert.False(harness.Overlay.IsOpen);
        Assert.Contains("dismissed", harness.Log);
    }

    [Fact]
    public void VaultVanishingMidSessionIdlesInsteadOfErroring()
    {
        var harness = new OverlayHarness();
        harness.Overlay.Open();
        harness.Source.IsVaultOpen = false;

        harness.Overlay.Query = "orphaned";

        Assert.Equal(SearchOverlayState.Idle, harness.Overlay.State);
        Assert.Empty(harness.Source.SearchCalls);
        Assert.Empty(harness.Announcements);
    }

    // ---- the empty-query rule (S7 / mac scopeListsOnEmpty) --------------

    [Fact]
    public void EmptyQueryShortCircuitsToIdleWithoutCallingTheSource()
    {
        var harness = new OverlayHarness();
        harness.Overlay.Open();

        harness.Overlay.Query = "notes";
        Assert.Equal(SearchOverlayState.Results, harness.Overlay.State);

        harness.Overlay.Query = "   ";

        Assert.Equal(SearchOverlayState.Idle, harness.Overlay.State);
        Assert.Empty(harness.Overlay.Rows);
        Assert.Equal(string.Empty, harness.Overlay.Summary);
        Assert.Equal(-1, harness.Overlay.SelectedIndex);
        // Exactly one FFI call: whitespace never reached the source.
        Assert.Equal(("notes", "Vault"), Assert.Single(harness.Source.SearchCalls));
    }

    [Fact]
    public void TagScopeStillCallsTheSourceOnAnEmptyQuery()
    {
        var harness = new OverlayHarness();
        harness.Source.OnSearch = (_, _) => Results(
            "Tagged files.",
            Hit("a/tagged.md", string.Empty));
        harness.Overlay.Open();

        harness.Overlay.SetScope(new SearchScope.Tag("test"));

        // Core owns the empty-query tag listing (S/mac scopeListsOnEmpty):
        // the empty query must reach the FFI, not idle out.
        Assert.Equal(("", "Tag:test"), Assert.Single(harness.Source.SearchCalls));
        Assert.Equal(SearchOverlayState.Results, harness.Overlay.State);

        // Activating one of those rows raises the open request but never
        // records a blank recent (S9 + mac recordSearchRecent's guard).
        harness.Overlay.ActivateRow(harness.Overlay.Rows[0]);
        Assert.Empty(harness.Source.Recorded);
        SearchOpenRequest request = Assert.Single(harness.OpenRequests);
        Assert.Equal("a/tagged.md", request.Path);
        Assert.Equal(string.Empty, request.Query);
    }

    // ---- S1 / S2: core searches, the host renders -----------------------

    [Fact]
    public void SummaryIsDisplayedVerbatimFromTheSource()
    {
        var harness = new OverlayHarness();
        const string distinctive = "☂ core-authored summary — never recomposed ☂";
        harness.Source.OnSearch = (_, _) => Results(
            distinctive,
            Hit("one.md", "snippet"),
            Hit("two.md", "snippet"));
        harness.Overlay.Open();

        harness.Overlay.Query = "rain";

        Assert.Equal(distinctive, harness.Overlay.Summary);
        A11yEvent.SearchResultsSummary announced =
            Assert.IsType<A11yEvent.SearchResultsSummary>(Assert.Single(harness.Announcements));
        Assert.Equal(2u, announced.Count);
    }

    [Fact]
    public void RowsPublishExactlyAsOrderedNeverReSorted()
    {
        var harness = new OverlayHarness();
        // Deliberately non-alphabetical, and scores deliberately NOT in
        // ascending order: bm25 order is core's business (contract S1),
        // and neither name nor score may re-sort it host-side.
        harness.Source.OnSearch = (_, _) => Results(
            "3 results",
            Hit("zebra.md", "z", score: -0.25),
            Hit("alpha.md", "a", score: -9.5),
            Hit("mango.md", "m", score: -5.0));
        harness.Overlay.Open();

        harness.Overlay.Query = "fruit";

        Assert.Equal(
            ["zebra.md", "alpha.md", "mango.md"],
            harness.Overlay.Rows.Select(row => row.Path));
        Assert.Equal(0, harness.Overlay.SelectedIndex);
    }

    // ---- S4: accessible names -------------------------------------------

    [Fact]
    public void AccessibleNameComposesBasenameAndStrippedSnippet()
    {
        var row = new SearchResultRowViewModel(
            "notes/deep/Budget 2026.md",
            $"spent {SnippetRuns.HitStart}less{SnippetRuns.HitEnd} today");

        Assert.Equal("Budget 2026.md", row.Basename);
        Assert.Equal("Budget 2026.md: spent less today", row.AccessibleName);
        Assert.Equal(
            [
                new SnippetRun("spent ", false),
                new SnippetRun("less", true),
                new SnippetRun(" today", false),
            ],
            row.SnippetSegments);
    }

    [Fact]
    public void AccessibleNameWithAnEmptySnippetKeepsMacsTrailingSpace()
    {
        // "name: " exactly as mac composes it — deliberate parity
        // (contract S4), reachable through every empty-query tag row.
        var row = new SearchResultRowViewModel("tagged.md", string.Empty);

        Assert.Equal("tagged.md: ", row.AccessibleName);
        Assert.Empty(row.SnippetSegments);
    }

    // ---- S7: announcement dedup on the summary, none on the pipeline ----

    [Fact]
    public void DuplicateSummaryIsNotReAnnouncedButAChangedSummaryIs()
    {
        var harness = new OverlayHarness();
        string summary = "Search returned 1 result.";
        harness.Source.OnSearch = (_, _) => Results(summary, Hit("note.md", "s"));
        harness.Overlay.Open();

        harness.Overlay.Query = "alpha";
        Assert.Single(harness.Announcements.OfType<A11yEvent.SearchResultsSummary>());

        // Re-running the identical query MUST reach the source again
        // (no dedup on the pipeline — the S7 red-team decision) while
        // the identical rendered summary stays silent.
        harness.Overlay.ActivateRecent("alpha");
        Assert.Equal(2, harness.Source.SearchCalls.Count);
        Assert.Single(harness.Announcements.OfType<A11yEvent.SearchResultsSummary>());

        summary = "Search returned 2 results.";
        harness.Overlay.ActivateRecent("alpha");
        Assert.Equal(3, harness.Source.SearchCalls.Count);
        Assert.Equal(
            2,
            harness.Announcements.OfType<A11yEvent.SearchResultsSummary>().Count());
    }

    // ---- close / reopen (mac closeSearchOverlay) ------------------------

    [Fact]
    public void CloseResetsScopeAndStateButPreservesTheQuery()
    {
        var harness = new OverlayHarness();
        harness.Overlay.Open();
        harness.Overlay.SetScope(new SearchScope.Tag("project"));
        harness.Overlay.Query = "budget";
        Assert.Equal(SearchOverlayState.Results, harness.Overlay.State);

        harness.Overlay.Close();

        Assert.False(harness.Overlay.IsOpen);
        // A tag scope left armed would silently scope the next search
        // to a chip the user can't see.
        Assert.IsType<SearchScope.Vault>(harness.Overlay.Scope);
        Assert.Equal(SearchOverlayState.Idle, harness.Overlay.State);
        Assert.Empty(harness.Overlay.Rows);
        Assert.Equal(string.Empty, harness.Overlay.Summary);
        Assert.Equal("budget", harness.Overlay.Query);
    }

    [Fact]
    public void ReopenWithARetainedQueryReArmsAndReAnnounces()
    {
        var harness = new OverlayHarness();
        harness.Source.OnSearch = (_, _) => Results(
            "Search returned 1 result.",
            Hit("budget.md", "b"));
        harness.Overlay.Open();
        harness.Overlay.SetScope(new SearchScope.Tag("project"));
        harness.Overlay.Query = "budget";
        harness.Overlay.Close();
        int callsBeforeReopen = harness.Source.SearchCalls.Count;

        harness.Overlay.Toggle();

        // The retained query re-armed through the ordinary pipeline —
        // under Vault scope, because close reset the transient tag.
        Assert.Equal(callsBeforeReopen + 1, harness.Source.SearchCalls.Count);
        Assert.Equal(("budget", "Vault"), harness.Source.SearchCalls[^1]);
        Assert.Equal(SearchOverlayState.Results, harness.Overlay.State);
        // Close cleared the announcement memory, so the identical
        // summary announces again on reopen.
        Assert.Equal(
            2,
            harness.Announcements.OfType<A11yEvent.SearchResultsSummary>().Count());
    }

    // ---- S6: cancellation is benign, errors are terminal ----------------

    [Fact]
    public void CancelledLeavesEveryPieceOfPriorStateUntouched()
    {
        var harness = new OverlayHarness();
        harness.Source.OnSearch = (_, _) => Results(
            "Search returned 1 result.",
            Hit("kept.md", "kept"));
        harness.Overlay.Open();
        harness.Overlay.Query = "keep";
        int announcementsBefore = harness.Announcements.Count;

        harness.Source.OnSearch = (_, _) => throw new VaultException.Cancelled();
        harness.Overlay.ActivateRecent("keep");

        // No state change, no summary change, no announcement — the
        // panel keeps showing what it showed (contract S6). The
        // Searching transition happened at dispatch, exactly as on mac;
        // the cancellation handler itself touched nothing.
        Assert.Equal("kept.md", Assert.Single(harness.Overlay.Rows).Path);
        Assert.Equal("Search returned 1 result.", harness.Overlay.Summary);
        Assert.Equal(announcementsBefore, harness.Announcements.Count);
        Assert.NotEqual(SearchOverlayState.Error, harness.Overlay.State);
    }

    [Fact]
    public void ErrorAnnouncesSearchFailedAndKeepsThePanelInErrorState()
    {
        var harness = new OverlayHarness();
        harness.Source.OnSearch = (_, _) => throw new VaultException.Db("disk exploded");
        harness.Overlay.Open();

        harness.Overlay.Query = "doomed";

        Assert.Equal(SearchOverlayState.Error, harness.Overlay.State);
        A11yEvent.SearchFailed failed =
            Assert.IsType<A11yEvent.SearchFailed>(Assert.Single(harness.Announcements));
        // The raw message passes through untouched (S6: never parsed,
        // never re-classified)...
        Assert.Equal("disk exploded", failed.Message);
        // ...and the displayed line is core's own rendering of the same
        // event — one template, not two copies.
        Assert.Equal(
            SlateUniffiMethods.A11yRender(new A11yEvent.SearchFailed("disk exploded")).Text,
            harness.Overlay.Summary);
    }

    // ---- recents (S9 / S14) ---------------------------------------------

    [Fact]
    public void RecentActivationReRunsTheSearchAndDoesNotRecord()
    {
        var harness = new OverlayHarness();
        harness.Source.RecentsOnDisk.Add("old query");
        harness.Overlay.Open();
        Assert.Equal(["old query"], harness.Overlay.Recents);

        harness.Overlay.ActivateRecent("old query");

        Assert.Equal(("old query", "Vault"), Assert.Single(harness.Source.SearchCalls));
        Assert.Equal("old query", harness.Overlay.Query);
        Assert.Empty(harness.Source.Recorded);
    }

    [Fact]
    public void ClearRecentsReloadsFromDiskSoAFailedClearStaysHonest()
    {
        var harness = new OverlayHarness();
        harness.Source.RecentsOnDisk.Add("remembered");
        harness.Overlay.Open();

        harness.Overlay.ClearRecents();

        Assert.Equal(1, harness.Source.ClearRecentsCalls);
        // The fake's clear succeeded, so the reload shows empty; a
        // failed persist would have reloaded the surviving list.
        Assert.Empty(harness.Overlay.Recents);
    }

    [Fact]
    public void RecentRowFocusAnnouncesThroughTheTypedEvent()
    {
        var harness = new OverlayHarness();

        harness.Overlay.NotifyRecentRowFocused("budget 2026");

        A11yEvent.RecentSearchFocused focused =
            Assert.IsType<A11yEvent.RecentSearchFocused>(Assert.Single(harness.Announcements));
        Assert.Equal("budget 2026", focused.Query);
    }

    [Fact]
    public void IdleStateLabelsAreMacVerbatim()
    {
        // Contract S14's mac-verbatim strings, pinned so a rewording on
        // either platform shows up as a parity break.
        Assert.Equal("Type to search.", SearchOverlayViewModel.IdleHint);
        Assert.Equal("Recent Searches", SearchOverlayViewModel.RecentSearchesHeader);
        Assert.Equal("Clear recent searches", SearchOverlayViewModel.ClearRecentsLabel);
        Assert.Equal(
            "Forgets every remembered search in this vault.",
            SearchOverlayViewModel.ClearRecentsHint);
        Assert.Equal("Runs this search again.", SearchOverlayViewModel.RecentRowHint);
    }

    // ---- debounce (S7) and the four-way staleness check (S5) ------------

    [Fact]
    public async Task DebounceCoalescesKeystrokesIntoOneTrailingSearch()
    {
        using var harness = new AsyncOverlayHarness();
        harness.Overlay.Open();

        harness.Overlay.Query = "b";
        harness.Overlay.Query = "bu";
        harness.Overlay.Query = "bud";
        // Three keystrokes armed three windows; the first two were
        // cancelled by their successors. Fire only the trailing one.
        Assert.Equal(3, harness.Debounce.ScheduledCount);
        await harness.CompletePipeline();

        Assert.Equal(("bud", "Vault"), Assert.Single(harness.Source.SearchCalls));
        Assert.Equal(SearchOverlayState.Results, harness.Overlay.State);
    }

    [Fact]
    public async Task StaleTokenResultIsDiscardedWhenTheSameQueryIsSuperseded()
    {
        using var harness = new AsyncOverlayHarness();
        using var firstEntered = new ManualResetEventSlim();
        using var releaseFirst = new ManualResetEventSlim();
        int calls = 0;
        harness.Source.OnSearch = (query, _) =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                firstEntered.Set();
                releaseFirst.Wait(TimeSpan.FromSeconds(10));
                return Results("old summary", Hit("old.md", "o"));
            }

            return Results("new summary", Hit("new.md", "n"));
        };
        harness.Overlay.Open();

        try
        {
            harness.Overlay.Query = "x";
            Task first = harness.StartPipeline();
            Assert.True(firstEntered.Wait(TimeSpan.FromSeconds(5)));

            // Same query re-armed (a recent re-run): the query, session,
            // and open-flag arms all still pass when the first search
            // lands — ONLY the token identity is stale.
            harness.Overlay.ActivateRecent("x");
            await harness.CompletePipeline();
            Assert.Equal("new.md", Assert.Single(harness.Overlay.Rows).Path);

            releaseFirst.Set();
            await first.WaitAsync(TimeSpan.FromSeconds(5));
            harness.Context.Drain();

            Assert.Equal("new.md", Assert.Single(harness.Overlay.Rows).Path);
            Assert.Equal("new summary", harness.Overlay.Summary);
            A11yEvent.SearchResultsSummary announced =
                Assert.IsType<A11yEvent.SearchResultsSummary>(
                    Assert.Single(harness.Announcements));
            Assert.Equal(1u, announced.Count);
        }
        finally
        {
            releaseFirst.Set();
        }
    }

    [Fact]
    public async Task StaleQueryResultIsDiscardedWhenTypingRanAhead()
    {
        using var harness = new AsyncOverlayHarness();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        harness.Source.OnSearch = (_, _) =>
        {
            entered.Set();
            release.Wait(TimeSpan.FromSeconds(10));
            return Results("stale summary", Hit("stale.md", "s"));
        };
        harness.Overlay.Open();

        try
        {
            harness.Overlay.Query = "a";
            Task first = harness.StartPipeline();
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));

            // The debounce window lets the field run ahead of the
            // in-flight search. Its window is armed but never fired, so
            // the token is still current, the session unchanged, the
            // overlay open — ONLY the query arm is stale.
            harness.Overlay.Query = "ab";
            release.Set();
            await first.WaitAsync(TimeSpan.FromSeconds(5));
            harness.Context.Drain();

            Assert.Empty(harness.Overlay.Rows);
            Assert.Equal(string.Empty, harness.Overlay.Summary);
            Assert.Empty(harness.Announcements);
            Assert.Equal(SearchOverlayState.Searching, harness.Overlay.State);
        }
        finally
        {
            release.Set();
        }
    }

    [Fact]
    public async Task StaleSessionResultIsDiscardedWhenTheVaultSwitchesMidFlight()
    {
        using var harness = new AsyncOverlayHarness();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        harness.Source.OnSearch = (_, _) =>
        {
            entered.Set();
            release.Wait(TimeSpan.FromSeconds(10));
            return Results("vault A rows", Hit("a.md", "a"));
        };
        harness.Overlay.Open();

        try
        {
            harness.Overlay.Query = "x";
            Task first = harness.StartPipeline();
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));

            // A direct vault switch resolves while the search is in
            // flight (the mac #876 Codex round 2 bug): token, query, and
            // open-flag all still pass — ONLY the session identity is
            // stale. Vault A's rows must not land in vault B's overlay.
            harness.Source.SessionIdentity = new object();
            release.Set();
            await first.WaitAsync(TimeSpan.FromSeconds(5));
            harness.Context.Drain();

            Assert.Empty(harness.Overlay.Rows);
            Assert.Empty(harness.Announcements);
            Assert.Equal(SearchOverlayState.Searching, harness.Overlay.State);
        }
        finally
        {
            release.Set();
        }
    }

    [Fact]
    public async Task ClosedOverlayNeverResurrectsALateResult()
    {
        using var harness = new AsyncOverlayHarness();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        harness.Source.OnSearch = (_, _) =>
        {
            entered.Set();
            release.Wait(TimeSpan.FromSeconds(10));
            return Results("late summary", Hit("late.md", "l"));
        };
        harness.Overlay.Open();

        try
        {
            harness.Overlay.Query = "x";
            Task first = harness.StartPipeline();
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));

            // Close cancels the in-flight token as well, so the token
            // arm is also stale here — closing without cancelling is not
            // reachable through the public surface. The open-flag arm is
            // the belt-and-suspenders guarantee this fact pins: a closed
            // overlay stays closed and empty however late the result is.
            harness.Overlay.Close();
            release.Set();
            await first.WaitAsync(TimeSpan.FromSeconds(5));
            harness.Context.Drain();

            Assert.False(harness.Overlay.IsOpen);
            Assert.Empty(harness.Overlay.Rows);
            Assert.Equal(SearchOverlayState.Idle, harness.Overlay.State);
            Assert.Equal(string.Empty, harness.Overlay.Summary);
            Assert.Empty(harness.Announcements);
        }
        finally
        {
            release.Set();
        }
    }

    // ---- SD-4: reading-view tag activation (OpenTagScoped) --------------

    [Fact]
    public void OpenTagScopedClearsTheRetainedQueryAndFiresExactlyOneTagListing()
    {
        var harness = new OverlayHarness();
        harness.Source.OnSearch = (_, _) => Results(
            "Search returned 1 result.",
            Hit("tagged.md", string.Empty));
        // A prior session left a retained query behind (Close preserves
        // it) — the exact state where a wrong ordering double-fires.
        harness.Overlay.Open();
        harness.Overlay.Query = "stale";
        harness.Overlay.Close();
        int callsBefore = harness.Source.SearchCalls.Count;
        harness.Announcements.Clear();

        harness.Overlay.OpenTagScoped("projects");

        Assert.True(harness.Overlay.IsOpen);
        Assert.Equal(string.Empty, harness.Overlay.Query);
        Assert.Equal("projects", harness.Overlay.TagScopeName);
        // Exactly ONE new search, the empty-query tag listing: clearing
        // BEFORE opening kept the retained "stale" re-arm from firing a
        // Vault-scope search first, and arming the scope LAST kept the
        // listing from running under Vault scope (mac's ordering,
        // ReadingLinkRouter.swift:243-258).
        Assert.Equal(callsBefore + 1, harness.Source.SearchCalls.Count);
        Assert.Equal(("", "Tag:projects"), harness.Source.SearchCalls[^1]);
        Assert.Equal(SearchOverlayState.Results, harness.Overlay.State);
        // The overlay's own summary is the ONLY voice on the reading
        // path — never a HostComposed residue string (SD-4: the sidebar
        // filter's "Filtered files by tag" belongs to the editor path).
        A11yEvent announcement = Assert.Single(harness.Announcements);
        Assert.IsType<A11yEvent.SearchResultsSummary>(announcement);
    }

    [Fact]
    public void OpenTagScopedWhileOpenReplacesTheScopeWithoutReRunningTheOldQuery()
    {
        var harness = new OverlayHarness();
        harness.Overlay.Open();
        harness.Overlay.Query = "budget";
        int callsBefore = harness.Source.SearchCalls.Count;

        harness.Overlay.OpenTagScoped("projects");

        Assert.True(harness.Overlay.IsOpen);
        Assert.Equal(string.Empty, harness.Overlay.Query);
        Assert.Equal("projects", harness.Overlay.TagScopeName);
        // One new source call — the tag listing. The query clear itself
        // short-circuits at the empty-query idle rule (still Vault scope
        // at that instant), so "budget" is not re-run on the way.
        Assert.Equal(callsBefore + 1, harness.Source.SearchCalls.Count);
        Assert.Equal(("", "Tag:projects"), harness.Source.SearchCalls[^1]);
    }

    [Fact]
    public void OpenTagScopedWithNoVaultRefusesWithoutArmingTheScope()
    {
        var harness = new OverlayHarness();
        harness.Source.IsVaultOpen = false;

        harness.Overlay.OpenTagScoped("projects");

        Assert.False(harness.Overlay.IsOpen);
        // A scope armed on a closed overlay would silently scope the
        // NEXT open's first search — Close() never ran to reset it.
        Assert.IsType<SearchScope.Vault>(harness.Overlay.Scope);
        Assert.IsType<A11yEvent.SearchNeedsVault>(Assert.Single(harness.Announcements));
        Assert.Empty(harness.Source.SearchCalls);
    }

    // ---- S9: activation -------------------------------------------------

    [Fact]
    public async Task ActivationRecordsExactlyTheQueryThatProducedTheRows()
    {
        using var harness = new AsyncOverlayHarness();
        harness.Source.OnSearch = (query, _) => Results(
            $"summary for {query}",
            Hit("alpha.md", "a"));
        harness.Overlay.Open();

        harness.Overlay.Query = "alpha";
        await harness.CompletePipeline();
        Assert.Equal(SearchOverlayState.Results, harness.Overlay.State);

        // The debounce window: the live field already holds a newer
        // string while the visible rows still belong to "alpha". The
        // recent — and the open request — must carry the producing
        // query, not the live text (mac lastResultsQuery).
        harness.Overlay.Query = "alphabet";
        harness.Overlay.ActivateRow(harness.Overlay.Rows[0]);

        Assert.Equal("alpha", Assert.Single(harness.Source.Recorded));
        Assert.False(harness.Overlay.IsOpen);
        SearchOpenRequest request = Assert.Single(harness.OpenRequests);
        Assert.Equal("alpha.md", request.Path);
        Assert.Equal("alpha", request.Query);
    }

    // ---- phase-2 view surface: chip, panels, selection ------------------

    [Fact]
    public void TagScopeNameIsNullOutsideTagScopeAndTheNameInsideIt()
    {
        var harness = new OverlayHarness();
        harness.Overlay.Open();

        Assert.Null(harness.Overlay.TagScopeName);

        harness.Overlay.SetScope(new SearchScope.Tag("projects"));
        Assert.Equal("projects", harness.Overlay.TagScopeName);

        harness.Overlay.ClearScope();
        Assert.Null(harness.Overlay.TagScopeName);
    }

    [Fact]
    public void PanelFlagsAreMutuallyExclusiveAcrossEveryState()
    {
        var harness = new OverlayHarness();

        // Idle with no recents.
        harness.Overlay.Open();
        AssertPanels(harness.Overlay, idleHint: true);

        // Results with rows.
        harness.Source.OnSearch = (_, _) => Results("one", Hit("a.md", "s"));
        harness.Overlay.Query = "alpha";
        AssertPanels(harness.Overlay, resultRows: true);

        // Results with zero rows.
        harness.Source.OnSearch = (_, _) => Results("none");
        harness.Overlay.Query = "beta";
        AssertPanels(harness.Overlay, noResults: true);

        // Error.
        harness.Source.OnSearch = (_, _) =>
            throw new VaultException.InvalidQuery("bad syntax");
        harness.Overlay.Query = "gamma\"";
        AssertPanels(harness.Overlay, error: true);

        // Back to idle — and with recents on disk, the recents panel.
        harness.Overlay.Close();
        harness.Source.RecentsOnDisk.Add("alpha");
        harness.Overlay.Open();
        harness.Overlay.Query = string.Empty;
        AssertPanels(harness.Overlay, recents: true);
    }

    [Fact]
    public void RowCountFlippingWithoutAStateChangeStillRetargetsThePanels()
    {
        // PublishResults can land Results→Results with the row count
        // crossing zero; the flags must follow the rows, not only the
        // state transitions.
        var harness = new OverlayHarness();
        harness.Overlay.Open();

        harness.Source.OnSearch = (_, _) => Results("one", Hit("a.md", "s"));
        harness.Overlay.Query = "alpha";
        Assert.True(harness.Overlay.ShowsResultRows);

        bool sawResultRows = false;
        bool sawNoResults = false;
        harness.Overlay.PropertyChanged += (_, args) =>
        {
            sawResultRows |= args.PropertyName
                == nameof(SearchOverlayViewModel.ShowsResultRows);
            sawNoResults |= args.PropertyName
                == nameof(SearchOverlayViewModel.ShowsNoResults);
        };

        harness.Source.OnSearch = (_, _) => Results("none");
        harness.Overlay.Query = "beta";

        Assert.True(harness.Overlay.ShowsNoResults);
        Assert.False(harness.Overlay.ShowsResultRows);
        Assert.True(sawResultRows, "ShowsResultRows never notified");
        Assert.True(sawNoResults, "ShowsNoResults never notified");
    }

    [Fact]
    public void MoveSelectionWrapsLikeThePaletteAndIgnoresAnEmptyList()
    {
        var harness = new OverlayHarness();
        harness.Overlay.Open();

        // SD-1: arrows drive the list on Windows; mac has no arrow
        // handling at all. Empty list: a no-op, never a throw.
        harness.Overlay.MoveSelection(1);
        Assert.Equal(-1, harness.Overlay.SelectedIndex);

        harness.Source.OnSearch = (_, _) => Results(
            "three",
            Hit("a.md", "a"),
            Hit("b.md", "b"),
            Hit("c.md", "c"));
        harness.Overlay.Query = "alpha";
        Assert.Equal(0, harness.Overlay.SelectedIndex);

        harness.Overlay.MoveSelection(1);
        Assert.Equal(1, harness.Overlay.SelectedIndex);

        harness.Overlay.MoveSelection(-1);
        harness.Overlay.MoveSelection(-1);
        Assert.Equal(2, harness.Overlay.SelectedIndex);

        harness.Overlay.MoveSelection(1);
        Assert.Equal(0, harness.Overlay.SelectedIndex);
    }

    [Fact]
    public void OpenRequestCarriesTheMarkerStrippedSnippet()
    {
        var harness = new OverlayHarness();
        harness.Source.OnSearch = (_, _) => Results(
            "one",
            Hit("alpha.md", "before match after"));
        harness.Overlay.Open();

        harness.Overlay.Query = "match";
        harness.Overlay.ActivateSelected();

        SearchOpenRequest request = Assert.Single(harness.OpenRequests);
        Assert.Equal("before match after", request.Snippet);
    }

    private static void AssertPanels(
        SearchOverlayViewModel overlay,
        bool idleHint = false,
        bool recents = false,
        bool searching = false,
        bool resultRows = false,
        bool noResults = false,
        bool error = false)
    {
        Assert.Equal(idleHint, overlay.ShowsIdleHint);
        Assert.Equal(recents, overlay.ShowsRecents);
        Assert.Equal(searching, overlay.IsSearching);
        Assert.Equal(resultRows, overlay.ShowsResultRows);
        Assert.Equal(noResults, overlay.ShowsNoResults);
        Assert.Equal(error, overlay.ShowsError);
    }

    // ---- helpers --------------------------------------------------------

    private static QueryHit Hit(string path, string snippet, double score = -1.0) =>
        new(path, snippet, score);

    private static QueryResultSet Results(string summary, params QueryHit[] hits) =>
        new(hits, summary);

    private static string DescribeScope(SearchScope scope) => scope switch
    {
        SearchScope.Vault => "Vault",
        SearchScope.Folder folder => $"Folder:{folder.Path}",
        SearchScope.File file => $"File:{file.Path}",
        SearchScope.Tag tag => $"Tag:{tag.Name}",
        _ => scope.ToString() ?? "?",
    };

    private sealed class FakeSearchSource : ISearchSource
    {
        private readonly object _gate = new();
        private readonly List<(string Query, string Scope)> _searchCalls = [];

        public bool IsVaultOpen { get; set; } = true;

        public object? SessionIdentity { get; set; } = new object();

        /// <summary>Scripted result; null falls back to an empty set.
        /// Runs on the pipeline's worker thread in async facts.</summary>
        public Func<string, SearchScope, QueryResultSet>? OnSearch { get; set; }

        public List<string> RecentsOnDisk { get; } = [];

        public List<string> Recorded { get; } = [];

        public int ClearRecentsCalls { get; private set; }

        public IReadOnlyList<(string Query, string Scope)> SearchCalls
        {
            get
            {
                lock (_gate)
                {
                    return [.. _searchCalls];
                }
            }
        }

        public QueryResultSet Search(string query, SearchScope scope, CancelToken cancel)
        {
            lock (_gate)
            {
                _searchCalls.Add((query, DescribeScope(scope)));
            }

            Func<string, SearchScope, QueryResultSet>? scripted = OnSearch;
            return scripted is null
                ? Results($"fake summary for '{query}'")
                : scripted(query, scope);
        }

        public IReadOnlyList<string> LoadRecents() => [.. RecentsOnDisk];

        public void RecordRecent(string query) => Recorded.Add(query);

        public void ClearRecents()
        {
            ClearRecentsCalls++;
            RecentsOnDisk.Clear();
        }
    }

    private class OverlayHarness
    {
        public OverlayHarness(bool debounceSearches = false, Func<CancellationToken, Task>? debounceDelay = null)
        {
            Overlay = new SearchOverlayViewModel(
                Source,
                Announcements.Add,
                debounceSearches,
                debounceDelay);
            Overlay.OpenRequested += (_, request) =>
            {
                OpenRequests.Add(request);
                Log.Add($"open:{request.Path}:{request.Query}");
            };
            Overlay.Dismissed += (_, _) => Log.Add("dismissed");
        }

        public FakeSearchSource Source { get; } = new();

        public List<A11yEvent> Announcements { get; } = [];

        public List<SearchOpenRequest> OpenRequests { get; } = [];

        public List<string> Log { get; } = [];

        public SearchOverlayViewModel Overlay { get; }
    }

    /// <summary>
    /// The pipeline harness: a pumped synchronization context stands in
    /// for the dispatcher and a manually fired debounce stands in for
    /// the 150 ms timer, so every interleaving is driven explicitly.
    /// </summary>
    private sealed class AsyncOverlayHarness : IDisposable
    {
        public AsyncOverlayHarness()
        {
            SynchronizationContext? previous = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(Context);
            try
            {
                Inner = new OverlayHarness(debounceSearches: true, Debounce.Next);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previous);
            }
        }

        public OverlayHarness Inner { get; }

        public PumpSynchronizationContext Context { get; } = new();

        public ManualDebounce Debounce { get; } = new();

        public FakeSearchSource Source => Inner.Source;

        public List<A11yEvent> Announcements => Inner.Announcements;

        public List<SearchOpenRequest> OpenRequests => Inner.OpenRequests;

        public SearchOverlayViewModel Overlay => Inner.Overlay;

        /// <summary>Fire the trailing debounce window and pump until the
        /// off-thread search is dispatched; returns the pipeline task to
        /// await once the fake search is released.</summary>
        public Task StartPipeline()
        {
            Task completion = Overlay.SearchCompletion;
            Debounce.FireLatest();
            Context.Drain();
            return completion;
        }

        /// <summary>Drive the latest pipeline to its published end.</summary>
        public async Task CompletePipeline()
        {
            Task completion = StartPipeline();
            await completion.WaitAsync(TimeSpan.FromSeconds(5));
            Context.Drain();
        }

        public void Dispose() => Overlay.Dispose();
    }

    private sealed class ManualDebounce
    {
        private readonly object _gate = new();
        private readonly List<TaskCompletionSource> _pending = [];

        public int ScheduledCount
        {
            get
            {
                lock (_gate)
                {
                    return _pending.Count;
                }
            }
        }

        public Task Next(CancellationToken token)
        {
            // Synchronous continuations by design: FireLatest runs the
            // pipeline inline up to its dispatcher post, keeping the
            // test single-threaded until the worker hop.
            var source = new TaskCompletionSource();
            token.Register(() => source.TrySetCanceled(token));
            lock (_gate)
            {
                _pending.Add(source);
            }

            return source.Task;
        }

        public void FireLatest()
        {
            TaskCompletionSource source;
            lock (_gate)
            {
                source = _pending[^1];
            }

            Assert.True(source.TrySetResult(), "the trailing debounce window was already settled");
        }
    }

    private sealed class PumpSynchronizationContext : SynchronizationContext
    {
        private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _queue = [];

        public override void Post(SendOrPostCallback callback, object? state) =>
            _queue.Enqueue((callback, state));

        public void Drain()
        {
            while (_queue.TryDequeue(out var work))
            {
                work.Callback(work.State);
            }
        }
    }
}
