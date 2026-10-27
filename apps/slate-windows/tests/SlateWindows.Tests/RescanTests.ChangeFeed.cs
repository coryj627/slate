// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Security.AccessControl;
using SlateWindows.Bases;
using SlateWindows.Reading;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// WB-10 (#1293, plan 43 21b; design
/// <c>docs/plans/43_mac_backlog_delivery/21_change_feed_design.md</c> §4.3–§4.7,
/// §6): the Windows host of core's change feed. Another Slate session on the
/// same vault — another window, or <c>slate-cli</c> — is a second
/// <see cref="VaultSession"/> with its own connection to the shared cache, as
/// a second process is. The vault watcher stays off for the process
/// (<c>Support/VaultWatcherSwitch.cs</c>), so another session's writes land on
/// the Refresh a fact presses; the watcher fact lifts it. The names are the
/// mac twins' (<c>VaultRescanTests+ChangeFeed.swift</c>).
/// </summary>
public sealed partial class RescanTests
{
    private const string UncountedNoChanges =
        "Files refreshed. No changes found; changes from other Slate sessions were not counted.";

    // ---------------------------------------------------------------------
    // Spec fact 1: AR-30 retired
    // ---------------------------------------------------------------------

    /// <summary>The owner's decision Q7 retired AR-30 (the TGC-17 rule: its
    /// witness, <c>AnotherSessionsChangesAreShownButNotCounted</c>, is
    /// rewritten first). Another session's modify, create and delete are
    /// applied — the open tab reloads, the deleted note's tab is marked
    /// missing, the tree and Quick Open follow — and an explicit Refresh
    /// counts them exactly. Its window is acknowledged, so the next Refresh
    /// counts nothing. Under Q2 a foreground run applies another session's
    /// change silently: it speaks only its own scan's.</summary>
    [Fact]
    public void AnotherSessionsChangesAreAppliedAndCountedExactly() => RunSta(() =>
    {
        using var h = new Harness("two-sessions", ("mod.md", "before\n"), ("gone.md", "gone\n"));
        WorkspaceTabViewModel mod = h.Open("mod.md");
        WorkspaceTabViewModel gone = h.Open("gone.md");
        using (VaultSession other = VaultSession.OpenFilesystem(h.Root))
        {
            _ = other.SaveText("mod.md", "changed by another window\n", null);
            _ = other.CreateExclusive("new.md", "# New\n");
            other.DeleteFile("gone.md");
        }

        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        Assert.Equal(["Files refreshed. 2 new or changed, 1 removed."], h.Spoken);
        Assert.Equal("changed by another window\n", mod.Text);
        Assert.False(mod.IsDirty);
        Assert.True(gone.IsMissingFromDisk);
        Assert.Contains(h.Sidebar.RootNodes, node => node.Path == "new.md");
        Assert.DoesNotContain(h.Sidebar.RootNodes, node => node.Path == "gone.md");
        Assert.Contains("new.md", h.QuickOpen.FilePathsForTests);
        Assert.DoesNotContain("gone.md", h.QuickOpen.FilePathsForTests);

        h.Events.Clear();
        h.Now += TimeSpan.FromMinutes(1);
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
        Assert.Equal([NoChanges], h.Spoken);

        using (VaultSession other = VaultSession.OpenFilesystem(h.Root))
        {
            _ = other.SaveText("mod.md", "and again\n", null);
        }

        h.Events.Clear();
        h.Now += TimeSpan.FromMinutes(1);
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Foreground));
        Assert.Empty(h.Events);
        Assert.Equal("and again\n", mod.Text);

        h.Now += TimeSpan.FromMinutes(1);
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
        Assert.Equal([NoChanges], h.Spoken);
    });

    // ---------------------------------------------------------------------
    // The open
    // ---------------------------------------------------------------------

    /// <summary>Design §4.2, §6: the open registers this session's cursor
    /// after its scan, so the open scan's own files (which
    /// <c>VaultScanFinished</c> spoke) are not in the first window, and the
    /// first Refresh's window is exact and counted. A registration that fails
    /// is logged with no path and never spoken; the first window is then a
    /// gap, whose full re-sync shows the index and whose acknowledgment
    /// re-creates the cursor.</summary>
    [Fact]
    public void TheOpenRegistersTheChangeFeedAfterItsScan() => RunSta(() =>
    {
        using var h = new Harness("feed-registers", ("alpha.md", "# Alpha\n"), ("beta.md", "# Beta\n"));
        List<ChangeFeedWindow> windows = RecordWindows(h);

        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        ChangeFeedWindow window = Assert.Single(windows);
        Assert.IsType<ChangeFeedStatus.Exact>(window.Status);
        Assert.Equal(0UL, window.RecordCount);
        Assert.True(window.Counted);
        Assert.Equal([NoChanges], h.Spoken);

        string root = Harness.NewRoot("feed-unregistered");
        Harness.Seed(root, ("alpha.md", "# Alpha\n"));
        using var log = new HostLogLines();
        using Harness unregistered = Harness.OpenExistingWith(
            root,
            lifecycle => lifecycle.RegisterChangeFeedForTests = _ => throw new VaultException.Io("injected registration failure"));
        Assert.Contains(log.Lines, line => line.StartsWith("SlateWindows.ChangeFeedRegisterFailed", StringComparison.Ordinal));
        List<ChangeFeedWindow> gap = RecordWindows(unregistered);
        using (VaultSession other = VaultSession.OpenFilesystem(root))
        {
            _ = other.CreateExclusive("new.md", "# New\n");
        }

        unregistered.Context.Await(unregistered.Lifecycle.RescanAsync(RescanReason.Explicit));
        Assert.Equal<ChangeFeedStatus>(new ChangeFeedStatus.Gap(ChangeFeedGap.NoCursor), Assert.Single(gap).Status);
        Assert.Equal([UncountedNoChanges], unregistered.Spoken);
        Assert.Contains("new.md", unregistered.QuickOpen.FilePathsForTests);

        gap.Clear();
        unregistered.Events.Clear();
        unregistered.Now += TimeSpan.FromMinutes(1);
        unregistered.Context.Await(unregistered.Lifecycle.RescanAsync(RescanReason.Explicit));
        Assert.IsType<ChangeFeedStatus.Exact>(Assert.Single(gap).Status);
        Assert.Equal([NoChanges], unregistered.Spoken);
    });

    // ---------------------------------------------------------------------
    // Spec fact 2: the gap and the oversized window
    // ---------------------------------------------------------------------

    /// <summary>Spec fact 2 (AR-76): rows trimmed past this session's cursor,
    /// as the 100,000-row cap trims them, make the window a gap. The run
    /// reads no record, takes R-9's full re-sync (another session's note
    /// reaches Quick Open and the tree all the same), and says changes from
    /// other sessions were not counted, with this scan's own counts. Its
    /// acknowledgment heals the feed: the next Refresh is exact and counted.
    /// A foreground run in a gap speaks the uncounted sentence only when its
    /// own scan found something.</summary>
    [Fact]
    public void ATrimmedFeedFallsBackToTheFullReSyncWithTheNotCountedSentence() => RunSta(() =>
    {
        using var h = new Harness("feed-trimmed", ("alpha.md", "# Alpha\n"));
        List<ChangeFeedWindow> windows = RecordWindows(h);
        using (VaultSession other = VaultSession.OpenFilesystem(h.Root))
        {
            _ = other.CreateExclusive("new.md", "# New\n");
        }

        CacheSql.TrimTheFeedPastTheCursor(h.Root);
        h.CoreCalls.Clear();

        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        Assert.Equal<ChangeFeedStatus>(new ChangeFeedStatus.Gap(ChangeFeedGap.Trimmed), Assert.Single(windows).Status);
        Assert.DoesNotContain("page", Operations(h));
        Assert.Equal([UncountedNoChanges], h.Spoken);
        Assert.Contains("new.md", h.QuickOpen.FilePathsForTests);
        Assert.Contains(h.Sidebar.RootNodes, node => node.Path == "new.md");

        windows.Clear();
        h.Events.Clear();
        h.Now += TimeSpan.FromMinutes(1);
        using (VaultSession other = VaultSession.OpenFilesystem(h.Root))
        {
            _ = other.SaveText("new.md", "# New, again\n", null);
        }

        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
        Assert.IsType<ChangeFeedStatus.Exact>(Assert.Single(windows).Status);
        Assert.Equal([Explicit1], h.Spoken);

        // A foreground run in a gap: silent when its own scan found nothing.
        h.Events.Clear();
        h.Now += TimeSpan.FromMinutes(1);
        using (VaultSession other = VaultSession.OpenFilesystem(h.Root))
        {
            _ = other.CreateExclusive("quiet.md", "# Quiet\n");
        }

        CacheSql.TrimTheFeedPastTheCursor(h.Root);
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Foreground));
        Assert.Empty(h.Events);
        Assert.Contains("quiet.md", h.QuickOpen.FilePathsForTests);

        h.Now += TimeSpan.FromMinutes(1);
        using (VaultSession other = VaultSession.OpenFilesystem(h.Root))
        {
            _ = other.CreateExclusive("more.md", "# More\n");
        }

        CacheSql.TrimTheFeedPastTheCursor(h.Root);
        h.Write("dropped.md", "# Dropped\n");
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Foreground));
        Assert.Equal(
            ["Files refreshed. 1 new or changed, 0 removed; changes from other Slate sessions were not counted."],
            h.Spoken);
    });

    /// <summary>Another session's scan indexes 10,001 notes, one row past a
    /// window's limit (the owner's decision Q5). The window is too large and
    /// reads nothing; the full re-sync shows every note; the sentence says
    /// other sessions' changes were not counted. The acknowledgment moves the
    /// cursor past them: the next Refresh is exact and says nothing
    /// changed.</summary>
    [Fact]
    public void ATooLargeWindowFallsBackWithTheNotCountedSentence() => StaThread.Run(
        () =>
        {
            using var h = new Harness("feed-too-large", ("alpha.md", "# Alpha\n"));
            List<ChangeFeedWindow> windows = RecordWindows(h);
            string folder = Path.Combine(h.Root, "bulk");
            _ = Directory.CreateDirectory(folder);
            for (int index = 0; index < 10_001; index++)
            {
                File.WriteAllText(Path.Combine(folder, $"note-{index}.md"), "n\n");
            }

            using (VaultSession other = VaultSession.OpenFilesystem(h.Root))
            using (var cancel = new CancelToken())
            {
                _ = other.ScanInitial(cancel);
            }

            h.CoreCalls.Clear();
            h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

            Assert.IsType<ChangeFeedStatus.TooLarge>(Assert.Single(windows).Status);
            Assert.DoesNotContain("page", Operations(h));
            Assert.Equal([UncountedNoChanges], h.Spoken);
            Assert.Equal(10_002, h.QuickOpen.FilePathsForTests.Count);

            windows.Clear();
            h.Events.Clear();
            h.Now += TimeSpan.FromMinutes(1);
            h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
            Assert.IsType<ChangeFeedStatus.Exact>(Assert.Single(windows).Status);
            Assert.Equal([NoChanges], h.Spoken);
        },
        TimeSpan.FromMinutes(5),
        "the oversized-window fact timed out.");

    // ---------------------------------------------------------------------
    // Spec fact 3: the window's paths, and nothing else
    // ---------------------------------------------------------------------

    /// <summary>Spec fact 3: the dependents follow the window's paths.
    /// Another session's edit of an embedded note re-projects the reading
    /// model that embeds it, not one that embeds another note, and an
    /// unrelated note's history panel does not reload.</summary>
    [Fact]
    public void ReadingAndHistoryAreNotifiedOnlyForTheWindowsPaths() => RunSta(() =>
    {
        using var h = new Harness(
            "feed-reading-paths",
            ("host.md", "# Host\n\n![[embedded]]\n"),
            ("embedded.md", "Embedded before.\n"),
            ("bystander.md", "# Bystander\n\n![[aside]]\n"),
            ("aside.md", "Aside.\n"));
        WorkspaceTabViewModel host = h.Open("host.md");
        host.ToggleViewMode();
        WorkspaceTabViewModel bystander = h.Open("bystander.md");
        bystander.ToggleViewMode();
        h.PumpUntil(() => ReadingText(host).Contains("Embedded before.", StringComparison.Ordinal), "the host's embed");
        h.PumpUntil(() => ReadingText(bystander).Contains("Aside.", StringComparison.Ordinal), "the bystander's embed");
        h.Workspace.History.NoteChanged("aside.md");
        h.Settle();
        int loads = 0;
        h.Workspace.History.LoadInterleaveForTests = () => Interlocked.Increment(ref loads);
        Assert.False(host.Reading!.HasPendingDependencyRefresh);
        Assert.False(bystander.Reading!.HasPendingDependencyRefresh);

        using (VaultSession other = VaultSession.OpenFilesystem(h.Root))
        {
            _ = other.SaveText("embedded.md", "Embedded after, by another window.\n", null);
        }

        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
        h.Settle();

        Assert.Equal([Explicit1], h.Spoken);
        Assert.True(host.Reading!.HasPendingDependencyRefresh, "the embedder owes no re-render");
        Assert.False(bystander.Reading!.HasPendingDependencyRefresh, "a note that embeds another note re-projected");
        Assert.Equal(0, Volatile.Read(ref loads));
    });

    /// <summary>A folder another session renamed touches every path under
    /// it: the reading model whose embed lived in the folder re-projects; one
    /// that embeds a note elsewhere does not.</summary>
    [Fact]
    public void AReadingModelEmbeddingANoteUnderARenamedFolderReProjects() => RunSta(() =>
    {
        using var h = new Harness(
            "feed-folder-embed",
            ("host.md", "# Host\n\n![[dir/x]]\n"),
            ("dir/x.md", "X before.\n"),
            ("bystander.md", "# Bystander\n\n![[aside]]\n"),
            ("aside.md", "Aside.\n"));
        WorkspaceTabViewModel host = h.Open("host.md");
        host.ToggleViewMode();
        WorkspaceTabViewModel bystander = h.Open("bystander.md");
        bystander.ToggleViewMode();
        h.PumpUntil(() => ReadingText(host).Contains("X before.", StringComparison.Ordinal), "the host's embed");
        h.PumpUntil(() => ReadingText(bystander).Contains("Aside.", StringComparison.Ordinal), "the bystander's embed");

        using (VaultSession other = VaultSession.OpenFilesystem(h.Root))
        {
            _ = other.RenameFolder("dir", "moved");
        }

        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
        h.Settle();

        Assert.True(host.Reading!.HasPendingDependencyRefresh, "the embedder of a note under the renamed folder");
        Assert.False(bystander.Reading!.HasPendingDependencyRefresh, "an unrelated embedder re-projected");
    });

    /// <summary>WB-10a's trigger on the host: a window whose only record is
    /// another session's folder still moved core's graph generation, so the
    /// graph probe runs — and the Bases re-run, since any record may move a
    /// base's membership. A foreground run, so the explicit recovery probe
    /// cannot mask the record trigger, and nothing failed before, so the
    /// retry set is empty.</summary>
    [Fact]
    public void ADirectoryOnlyForeignWindowStillProbesTheGraphAndBases() => RunSta(() =>
    {
        using var h = new Harness("feed-folder-only", ("x.md", "# X\n"));
        h.Settle();
        int before = ConnectionsProbes(h);
        List<string> kinds = RecordKinds(h);
        List<ChangeFeedWindow> windows = RecordWindows(h);
        using (VaultSession other = VaultSession.OpenFilesystem(h.Root))
        {
            _ = other.CreateFolder("empty");
        }

        h.Now += TimeSpan.FromMinutes(1);
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Foreground));
        h.Settle();

        Assert.Equal(1UL, Assert.Single(windows).RecordCount);
        Assert.Equal(1, ConnectionsProbes(h) - before);
        Assert.Contains("graph", kinds);
        Assert.Contains("bases", kinds);
        Assert.Contains(h.Sidebar.RootNodes, node => node.Path == "empty");
        Assert.Empty(h.Events);
    });

    /// <summary>A foreground run that finds nothing reads its window and the
    /// open tabs' hashes, reads the head, acknowledges, and touches nothing
    /// else: no listing, no page, no tree refresh, no dependent, no
    /// probe.</summary>
    [Fact]
    public void ANoChangeForegroundRunTouchesNothingButTheTabComparison() => RunSta(() =>
    {
        using var h = new Harness("feed-quiet-foreground", ("alpha.md", "# Alpha\n"), ("beta.md", "# Beta\n"));
        _ = h.Open("alpha.md");
        h.Settle();
        List<string> kinds = RecordKinds(h);
        int before = ConnectionsProbes(h);
        int refreshes = h.Sidebar.TreeGenerationForTests;
        h.CoreCalls.Clear();

        h.Now += TimeSpan.FromMinutes(1);
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Foreground));
        h.Settle();

        Assert.Equal(["scan", "window", "hashes", "head", "acknowledge"], Operations(h));
        Assert.Empty(kinds);
        Assert.Equal(0, ConnectionsProbes(h) - before);
        Assert.Equal(refreshes, h.Sidebar.TreeGenerationForTests);
        Assert.Empty(h.Events);
    });

    /// <summary>The owner's decision Q8: an unchanged explicit Refresh adds
    /// only the recovery set — Quick Open's whole list, the Bases re-run
    /// (dashboards and saved queries live in the cache, outside the feed) and
    /// one graph probe — and no reading or history publication when nothing
    /// shows a failure.</summary>
    [Fact]
    public void ANoChangeRefreshAddsOnlyTheRecoverySet() => RunSta(() =>
    {
        using var h = new Harness("feed-quiet-refresh", ("alpha.md", "# Alpha\n"));
        _ = h.Open("alpha.md");
        h.Settle();
        List<string> kinds = RecordKinds(h);
        int before = ConnectionsProbes(h);
        h.CoreCalls.Clear();

        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
        h.Settle();

        Assert.Equal(["scan", "list", "window", "hashes", "head", "acknowledge"], Operations(h));
        Assert.Equal(["bases", "connections", "graph"], kinds.Distinct().Order(StringComparer.Ordinal));
        Assert.Equal(1, ConnectionsProbes(h) - before);
        Assert.Equal([NoChanges], h.Spoken);
    });

    // ---------------------------------------------------------------------
    // Spec fact 4: pages
    // ---------------------------------------------------------------------

    /// <summary>Spec fact 4: at a page limit of two, another session's five
    /// changes come as seven records in four pages, renames first, and every
    /// one is applied: Quick Open's list, the tree, and the open tab on the
    /// renamed note.</summary>
    [Fact]
    public void AWindowSpanningSeveralPagesAppliesEveryRecord() => RunSta(() =>
    {
        using var h = new Harness(
            "feed-pages",
            ("edit.md", "edit one\n"),
            ("drop.md", "# Drop\n"),
            ("move.md", "move me\n"));
        WorkspaceTabViewModel move = h.Open("move.md");
        h.Lifecycle.ChangeFeedPageLimitForTests = 2;
        using (VaultSession other = VaultSession.OpenFilesystem(h.Root))
        {
            _ = other.CreateExclusive("one.md", "# One\n");
            _ = other.CreateExclusive("two.md", "# Two\n");
            _ = other.SaveText("edit.md", "edit two\n", null);
            other.DeleteFile("drop.md");
            _ = other.RenameFile("move.md", "moved.md");
        }

        h.CoreCalls.Clear();
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        Assert.Equal(["Files refreshed. 4 new or changed, 2 removed."], h.Spoken);
        Assert.True(Operations(h).Count(operation => operation == "page") >= 4, "seven records in pages of two");
        Assert.Equal(["edit.md", "moved.md", "one.md", "two.md"], h.QuickOpen.FilePathsForTests.Order(StringComparer.Ordinal));
        Assert.Equal(
            ["edit.md", "moved.md", "one.md", "two.md"],
            h.Sidebar.RootNodes.Select(node => node.Path).Order(StringComparer.Ordinal));
        Assert.Equal("moved.md", move.Path);
        Assert.Equal("move me\n", move.Text);
        Assert.False(move.IsMissingFromDisk);
    });

    /// <summary>Renames precede removals in page order, so a rename's origin
    /// removal can come on a later page. A tab never loaded (its read failed
    /// when it opened) follows a rename only when its old path is gone at the
    /// window's target — decided over the whole window: at one record a
    /// page, the tab at <c>a.md</c> still moves to <c>b.md</c>, and the tab
    /// comparison then loads it.</summary>
    [Fact]
    public void ARenameWhoseOriginRemovalIsOnALaterPageStillMovesAnUnloadedTab() => RunSta(() =>
    {
        using var h = new Harness("feed-unloaded-rename", ("a.md", "a note\n"));
        WorkspaceTabViewModel tab;
        using (DenyAccess.To(Path.Combine(h.Root, "a.md"), FileSystemRights.ReadData))
        {
            tab = h.Open("a.md");
        }

        Assert.Null(tab.SavedContentHash);
        h.Lifecycle.ChangeFeedPageLimitForTests = 1;
        using (VaultSession other = VaultSession.OpenFilesystem(h.Root))
        {
            _ = other.RenameFile("a.md", "b.md");
        }

        h.CoreCalls.Clear();
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        Assert.True(Operations(h).Count(operation => operation == "page") >= 3, "one record a page");
        Assert.Equal("b.md", tab.Path);
        Assert.Equal("a note\n", tab.Text);
        Assert.False(tab.IsMissingFromDisk);
    });

    // ---------------------------------------------------------------------
    // AR-31: passes
    // ---------------------------------------------------------------------

    /// <summary>AR-31: another session commits while the run's tree refresh
    /// is parked on its worker. The head the run reads after it says so, and
    /// a further window applies the commit: at the end the tree and Quick
    /// Open agree, and the Refresh counts both changes, once.</summary>
    [Fact]
    public void ACommitDuringTheReSyncIsAppliedByAFurtherPass() => RunSta(() =>
    {
        using Harness h = Harness.WithAsyncTree("feed-passes", ("alpha.md", "# Alpha\n"));
        h.Context.RunUntil(() => !h.Sidebar.IsRefreshingTree, "the open's tree");
        List<ChangeFeedWindow> windows = RecordWindows(h);
        using (VaultSession other = VaultSession.OpenFilesystem(h.Root))
        {
            _ = other.CreateExclusive("one.md", "# One\n");
        }

        using var parked = new ManualResetEventSlim(false);
        using var unpark = new ManualResetEventSlim(false);
        int treeReads = 0;
        h.TreeWorker = (work, token) => Task.Run(
            () =>
            {
                if (Interlocked.Increment(ref treeReads) == 1)
                {
                    parked.Set();
                    _ = unpark.Wait(TimeSpan.FromSeconds(30));
                }

                work();
            },
            token);

        Task run = h.Lifecycle.RescanAsync(RescanReason.Explicit);
        h.Context.RunUntil(() => parked.IsSet, "the run's tree refresh parking");
        using (VaultSession other = VaultSession.OpenFilesystem(h.Root))
        {
            _ = other.CreateExclusive("two.md", "# Two\n");
        }

        unpark.Set();
        h.Context.Await(run);

        Assert.Equal(2, windows.Count);
        Assert.All(windows, window => Assert.IsType<ChangeFeedStatus.Exact>(window.Status));
        Assert.Equal(windows[0].ToSeq, windows[1].FromSeq);
        Assert.Contains("two.md", h.QuickOpen.FilePathsForTests);
        Assert.Equal(
            h.QuickOpen.FilePathsForTests.Order(StringComparer.Ordinal),
            h.Sidebar.RootNodes.Select(node => node.Path).Order(StringComparer.Ordinal));
        Assert.Equal(["Files refreshed. 2 new or changed, 0 removed."], h.Spoken);

        h.Events.Clear();
        h.Now += TimeSpan.FromMinutes(1);
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
        Assert.Equal([NoChanges], h.Spoken);
    });

    // ---------------------------------------------------------------------
    // The Slate-write journal and the tab comparison
    // ---------------------------------------------------------------------

    /// <summary>Another session creates a note, and this session's user
    /// renames it while the run's window is open. The window's record says
    /// the note is at its first name, but the rename is journaled and both
    /// its names are re-read from the index after the records: Quick Open
    /// does not get the old name back.</summary>
    [Fact]
    public void ASlateWriteDuringTheWindowIsReReadNotReverted() => RunSta(() =>
    {
        using var h = new Harness("feed-journal", ("alpha.md", "# Alpha\n"));
        using (VaultSession other = VaultSession.OpenFilesystem(h.Root))
        {
            _ = other.CreateExclusive("brief.md", "# Brief\n");
        }

        bool renamed = false;
        h.Lifecycle.ChangeFeedWindowObserverForTests = opened =>
        {
            if (!renamed)
            {
                renamed = true;
                _ = h.Lifecycle.SessionForTests!.RenameFile("brief.md", "kept.md");
            }
        };

        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        Assert.True(renamed);
        Assert.DoesNotContain("brief.md", h.QuickOpen.FilePathsForTests);
        Assert.Contains("kept.md", h.QuickOpen.FilePathsForTests);
    });

    /// <summary>Another session edits the open note, and this session's user
    /// renames it after the run's window opened. The rename moves the tab
    /// (its event is journaled); the tab comparison then finds the edit under
    /// the new name and reloads the clean tab onto it.</summary>
    [Fact]
    public void AnOwnRenameAfterTheWindowOpensStillGetsTheOtherSessionsEdit() => RunSta(() =>
    {
        using var h = new Harness("feed-own-rename-late", ("draft.md", "draft one\n"));
        WorkspaceTabViewModel tab = h.Open("draft.md");
        using (VaultSession other = VaultSession.OpenFilesystem(h.Root))
        {
            _ = other.SaveText("draft.md", "draft two\n", null);
        }

        bool renamed = false;
        h.Lifecycle.ChangeFeedWindowObserverForTests = opened =>
        {
            if (!renamed)
            {
                renamed = true;
                _ = h.Lifecycle.SessionForTests!.RenameFile("draft.md", "final.md");
            }
        };

        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        Assert.True(renamed);
        Assert.Equal("final.md", tab.Path);
        Assert.Equal("draft two\n", tab.Text);
        Assert.False(tab.IsDirty);
        Assert.Equal(["final.md"], h.QuickOpen.FilePathsForTests);
    });

    /// <summary>Design §4.3's first row: another session edits a note and
    /// this session's user renames it before the next run. The window moves
    /// the edit to the new name — one modified, nothing removed — and the tab
    /// the rename moved reloads onto it.</summary>
    [Fact]
    public void AnOwnRenameOfANoteAnotherSessionEditedReloadsTheMovedTab() => RunSta(() =>
    {
        using var h = new Harness("feed-own-rename", ("draft.md", "draft one\n"));
        WorkspaceTabViewModel tab = h.Open("draft.md");
        using (VaultSession other = VaultSession.OpenFilesystem(h.Root))
        {
            _ = other.SaveText("draft.md", "draft two\n", null);
        }

        _ = h.Lifecycle.SessionForTests!.RenameFile("draft.md", "final.md");
        h.Context.Drain();
        Assert.Equal("final.md", tab.Path);
        h.Events.Clear();

        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        Assert.Equal([Explicit1], h.Spoken);
        Assert.Equal("final.md", tab.Path);
        Assert.Equal("draft two\n", tab.Text);
    });

    /// <summary>The disk moves past the index between the scan and the tab's
    /// read, so the clean tab cannot be vouched for; it keeps its text and
    /// the run counts one error. The next foreground run compares the tab
    /// again, whatever its window holds, and reloads it.</summary>
    [Fact]
    public void AnUnverifiedTabReloadIsRetriedByTheNextForegroundRun() => RunSta(() =>
    {
        using var h = new Harness("feed-unverified", ("draft.md", "draft one\n"));
        WorkspaceTabViewModel tab = h.Open("draft.md");
        using (VaultSession other = VaultSession.OpenFilesystem(h.Root))
        {
            _ = other.SaveText("draft.md", "draft two\n", null);
        }

        bool moved = false;
        h.AfterCoreCall = operation =>
        {
            if (operation == "scan" && !moved)
            {
                moved = true;
                h.Write("draft.md", "draft three\n");
            }
        };

        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        Assert.Equal([OneError], h.Spoken);
        Assert.Equal("draft one\n", tab.Text);

        h.AfterCoreCall = null;
        h.Events.Clear();
        h.Now += TimeSpan.FromMinutes(1);
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Foreground));
        Assert.Equal("draft three\n", tab.Text);
        Assert.Equal([Explicit1], h.Spoken);
    });

    // ---------------------------------------------------------------------
    // Renames another session made (the owner's decision Q3)
    // ---------------------------------------------------------------------

    /// <summary>Q3 (AR-8 amended): another session renames two open notes.
    /// Each tab that held one moves to the new name as for this session's
    /// own rename — the clean one with its text, the dirty one keeping its
    /// unsaved words — never marked missing, and each rename counts one new
    /// and one removed (Q4).</summary>
    [Fact]
    public void AForeignRenameRetargetsTheOpenTab() => RunSta(() =>
    {
        using var h = new Harness("feed-foreign-rename", ("dirty.md", "dirty one\n"), ("clean.md", "clean one\n"));
        WorkspaceTabViewModel dirty = h.Open("dirty.md");
        dirty.Text = "my unsaved words\n";
        WorkspaceTabViewModel clean = h.Open("clean.md");
        using (VaultSession other = VaultSession.OpenFilesystem(h.Root))
        {
            _ = other.RenameFile("clean.md", "clean-renamed.md");
            _ = other.RenameFile("dirty.md", "dirty-renamed.md");
        }

        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        Assert.Equal(["Files refreshed. 2 new or changed, 2 removed."], h.Spoken);
        Assert.Equal("clean-renamed.md", clean.Path);
        Assert.Equal("clean one\n", clean.Text);
        Assert.False(clean.IsMissingFromDisk);
        Assert.Equal("dirty-renamed.md", dirty.Path);
        Assert.Equal("my unsaved words\n", dirty.Text);
        Assert.True(dirty.IsDirty);
        Assert.False(dirty.IsMissingFromDisk);
        Assert.Equal(["clean-renamed.md", "dirty-renamed.md"], h.QuickOpen.FilePathsForTests.Order(StringComparer.Ordinal));
    });

    /// <summary>Design §4.3's table: another session renames the open note,
    /// and this session's user renames it again before the run. One renamed
    /// record, from where the tab is to where the file is, moves the tab
    /// there.</summary>
    [Fact]
    public void AForeignRenameThenAnOwnRenameOfItsDestinationMovesTheTabThere() => RunSta(() =>
    {
        using var h = new Harness("feed-rename-chain", ("first.md", "first\n"));
        WorkspaceTabViewModel tab = h.Open("first.md");
        using (VaultSession other = VaultSession.OpenFilesystem(h.Root))
        {
            _ = other.RenameFile("first.md", "second.md");
        }

        _ = h.Lifecycle.SessionForTests!.RenameFile("second.md", "third.md");
        h.Context.Drain();
        // The rename's own aftermath, before any rescan and as on main
        // (#1683): this session announces first.md, which another session
        // moved, as deleted, and marks its tab missing.
        h.Settle();
        h.Settle();
        h.Events.Clear();

        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        Assert.Equal(["Files refreshed. 1 new or changed, 1 removed."], h.Spoken);
        Assert.Equal("third.md", tab.Path);
        Assert.Equal("first\n", tab.Text);
        Assert.False(tab.IsMissingFromDisk);
        Assert.Equal(["third.md"], h.QuickOpen.FilePathsForTests);
    });

    /// <summary>Design §4.3's table (the <c>from_hash</c> guard): another
    /// session renames a note, and this session's user renames another note
    /// into the freed name before the run. Only the tab that held the renamed
    /// note moves; the tab of the note now at that name stays with
    /// it.</summary>
    [Fact]
    public void ANoteThatTookARenamedNotesPathKeepsItsTab() => RunSta(() =>
    {
        using var h = new Harness("feed-took-the-path", ("old.md", "old note\n"), ("other.md", "other note\n"));
        WorkspaceTabViewModel old = h.Open("old.md");
        WorkspaceTabViewModel took = h.Open("other.md");
        using (VaultSession other = VaultSession.OpenFilesystem(h.Root))
        {
            _ = other.RenameFile("old.md", "moved.md");
        }

        _ = h.Lifecycle.SessionForTests!.RenameFile("other.md", "old.md");
        h.Context.Drain();
        Assert.Equal("old.md", took.Path);
        h.Events.Clear();

        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        Assert.Equal([Explicit1], h.Spoken);
        Assert.Equal("moved.md", old.Path);
        Assert.Equal("old note\n", old.Text);
        Assert.Equal("old.md", took.Path);
        Assert.Equal("other note\n", took.Text);
    });

    // ---------------------------------------------------------------------
    // Cancellation, and the retry set
    // ---------------------------------------------------------------------

    /// <summary>A run cancelled after its window opened acknowledges
    /// nothing, with the lifecycle and the session kept: the cursor stays,
    /// and the next run reads the same window again and counts it.</summary>
    [Fact]
    public void ACancelledRunAcknowledgesNothingAndTheNextRunReadsTheSameWindow() => RunSta(() =>
    {
        using var h = new Harness("feed-cancelled", ("alpha.md", "# Alpha\n"));
        List<ChangeFeedWindow> windows = RecordWindows(h);
        using (VaultSession other = VaultSession.OpenFilesystem(h.Root))
        {
            _ = other.CreateExclusive("new.md", "# New\n");
        }

        var parked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool started = false;
        h.Workspace.RescanPublicationForTests = async (kind, _, publish) =>
        {
            if (kind == "graph" && !started)
            {
                started = true;
                await parked.Task;
            }

            await publish();
        };
        h.CoreCalls.Clear();

        Task run = h.Lifecycle.RescanAsync(RescanReason.Explicit);
        h.PumpUntil(() => started, "the run's graph publication parking");
        h.Context.Await(h.Lifecycle.CancelRunningRescanForTests());
        parked.SetResult();
        h.Context.Await(run);

        Assert.Empty(h.Events);
        Assert.DoesNotContain("acknowledge", Operations(h));
        ChangeFeedWindow first = Assert.Single(windows);

        h.Now += TimeSpan.FromMinutes(1);
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        Assert.Equal(2, windows.Count);
        Assert.Equal(first.FromSeq, windows[1].FromSeq);
        Assert.Equal(first.RecordCount, windows[1].RecordCount);
        Assert.Equal([Explicit1], h.Spoken);
        Assert.Contains("new.md", h.QuickOpen.FilePathsForTests);
    });

    /// <summary>The retry set (design §4.3 :662-673): a tree refresh or a
    /// Quick Open reload that fails counts one error and enters the set; the
    /// window is still acknowledged. The next run — a foreground one, which
    /// neither reloads Quick Open's list nor refreshes the tree for an empty
    /// window — retries the surface by its full reload, and it leaves the
    /// set.</summary>
    [Theory]
    [InlineData("tree")]
    [InlineData("quick-open")]
    public void AFailedQuickOpenOrTreeOperationIsRetriedByTheNextRun(string surface) => RunSta(() =>
    {
        using Harness h = Harness.WithAsyncTree($"feed-retry-{surface}", ("alpha.md", "# Alpha\n"));
        h.Context.RunUntil(() => !h.Sidebar.IsRefreshingTree, "the open's tree");
        RescanSurface expected = surface == "tree" ? RescanSurface.Tree : RescanSurface.QuickOpen;
        bool fail = true;
        int treeReads = 0;
        h.TreeWorker = (work, token) =>
        {
            _ = Interlocked.Increment(ref treeReads);
            return surface == "tree" && fail
                ? Task.FromException(new IOException("injected tree failure"))
                : Task.Run(work, token);
        };
        h.AfterCoreCall = operation =>
        {
            if (surface == "quick-open" && operation == "list" && fail)
            {
                throw new IOException("injected Quick Open failure");
            }
        };
        using (VaultSession other = VaultSession.OpenFilesystem(h.Root))
        {
            _ = other.CreateExclusive("new.md", "# New\n");
        }

        h.CoreCalls.Clear();
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        Assert.Equal([OneError], h.Spoken);
        Assert.Contains("acknowledge", Operations(h));
        Assert.Contains(expected, h.Lifecycle.RetrySetForTests);

        fail = false;
        h.Events.Clear();
        h.CoreCalls.Clear();
        int treeReadsBefore = Volatile.Read(ref treeReads);
        h.Now += TimeSpan.FromMinutes(1);
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Foreground));

        if (expected == RescanSurface.Tree)
        {
            Assert.True(Volatile.Read(ref treeReads) > treeReadsBefore, "the tree was not refreshed");
            Assert.Contains(h.Sidebar.RootNodes, node => node.Path == "new.md");
        }
        else
        {
            Assert.Contains("list", Operations(h));
        }

        Assert.DoesNotContain(expected, h.Lifecycle.RetrySetForTests);
        Assert.Empty(h.Events);
    });

    /// <summary>The retry set for the dependents (design §4.3 :660-671): a
    /// dependent whose publication fails counts one error and enters the
    /// set, and the run still acknowledges its window. The next run, whatever
    /// its reason and with nothing changed, retries exactly that dependent
    /// once, and its own witness holds; the run after it leaves it alone.
    /// History shows a note that is also open in a clean tab, so only the
    /// retry can reload the panel once that tab is current.</summary>
    [Theory]
    [InlineData("reading", RescanReason.Foreground)]
    [InlineData("reading", RescanReason.Explicit)]
    [InlineData("history", RescanReason.Foreground)]
    [InlineData("history", RescanReason.Explicit)]
    [InlineData("bases", RescanReason.Foreground)]
    [InlineData("bases", RescanReason.Explicit)]
    [InlineData("graph", RescanReason.Foreground)]
    [InlineData("graph", RescanReason.Explicit)]
    public void AFailedDependentIsRetriedByTheNextRunAfterItsWindowWasAcknowledged(string kind, RescanReason reason) => RunSta(() =>
    {
        using var h = new Harness(
            $"feed-retry-{kind}-{reason}",
            ("host.md", "# Host\n\n![[embedded]]\n"),
            ("embedded.md", "Embedded before.\n"),
            ("Notes.base", MainBase));
        WorkspaceTabViewModel host = h.Open("host.md");
        host.ToggleViewMode();
        h.PumpUntil(() => ReadingText(host).Contains("Embedded before.", StringComparison.Ordinal), "the embed's first render");
        _ = h.Open("embedded.md");
        WorkspaceTabViewModel notes = h.Open("Notes.base");
        h.PumpUntil(() => notes.Base?.State == BaseLoadState.Ready, "the base's first load");
        h.Workspace.History.NoteChanged("embedded.md");
        h.Settle();
        int loads = 0;
        h.Workspace.History.LoadInterleaveForTests = () => Interlocked.Increment(ref loads);
        var seen = new List<string>();
        bool fail = true;
        h.Workspace.RescanPublicationForTests = async (publishing, _, publish) =>
        {
            if (publishing == kind)
            {
                seen.Add(publishing);
                if (fail)
                {
                    fail = false;
                    throw new IOException($"injected {kind} publication failure");
                }
            }

            await publish();
        };
        using (VaultSession other = VaultSession.OpenFilesystem(h.Root))
        {
            _ = other.SaveText("embedded.md", "Embedded after, by another window.\n", null);
            _ = other.CreateExclusive("new.md", "# New\n");
        }

        h.Events.Clear();
        h.CoreCalls.Clear();
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
        h.Settle();

        Assert.Equal([OneError], h.Spoken);
        Assert.Contains("acknowledge", Operations(h));
        Assert.Equal([kind], seen);
        bool pendingAfterFirst = host.Reading!.HasPendingDependencyRefresh;
        string[] rowsAfterFirst = BaseRows(notes);
        int loadsAfterFirst = Volatile.Read(ref loads);
        int probesAfterFirst = ConnectionsProbes(h);

        List<ChangeFeedWindow> windows = RecordWindows(h);
        seen.Clear();
        h.Events.Clear();
        h.Now += TimeSpan.FromMinutes(1);
        h.Context.Await(h.Lifecycle.RescanAsync(reason));
        h.Settle();

        ChangeFeedWindow window = Assert.Single(windows);
        Assert.IsType<ChangeFeedStatus.Exact>(window.Status);
        Assert.Equal(0UL, window.RecordCount);
        Assert.Equal([kind], seen);
        switch (kind)
        {
            case "reading":
                Assert.False(pendingAfterFirst, "premise: the failed run never re-projected the embedder");
                Assert.True(host.Reading!.HasPendingDependencyRefresh, "the retry never re-projected the embedder");
                break;
            case "history":
                Assert.True(Volatile.Read(ref loads) > loadsAfterFirst, "the retry never reloaded the history panel");
                break;
            case "bases":
                Assert.DoesNotContain("new.md", rowsAfterFirst);
                Assert.Contains("new.md", BaseRows(notes));
                break;
            case "graph":
                Assert.Equal(1, ConnectionsProbes(h) - probesAfterFirst);
                break;
        }

        string[] spoken = reason == RescanReason.Explicit ? [NoChanges] : [];
        Assert.Equal(spoken, h.Spoken);
        Assert.Empty(h.Lifecycle.RetrySetForTests);

        seen.Clear();
        h.Now += TimeSpan.FromMinutes(1);
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Foreground));
        h.Settle();
        Assert.Empty(seen);
    });

    /// <summary>The owner's decision Q8: an explicit Refresh re-checks every
    /// dependent showing a failure state, even one no rescan operation
    /// produced (so the retry set is empty) and with an empty window. A
    /// foreground run does not: the recovery set is Refresh's.</summary>
    [Theory]
    [InlineData("reading")]
    [InlineData("history")]
    public void AnUnchangedRefreshReChecksADependentShowingAFailure(string kind) => RunSta(() =>
    {
        using Harness h = DependentsHarness($"feed-recover-{kind}");
        WorkspaceTabViewModel host = h.AllTabs.Single(tab => tab.Path == "host.md");
        _ = h.Open("embedded.md");
        h.Workspace.History.NoteChanged("embedded.md");
        h.Settle();
        ReadingContentViewModel reading = host.Reading!;
        if (kind == "reading")
        {
            reading.BlocksAppended += _ => { };
            reading.FetchFaultForTests = () => new VaultException.Io("injected reading failure");
            Task failing = reading.NotifyVaultFileChangedAsync(FileChangeKind.Modified, "embedded.md");
            h.PumpUntil(() => failing.IsCompleted, "the failing re-projection");
            reading.FetchFaultForTests = null;
            Assert.True(reading.LastRefreshFailed, "premise: the reading model shows its failure");
        }
        else
        {
            h.Workspace.History.LoadFaultForTests = () => new VaultException.Io("injected history failure");
            h.Workspace.History.NoteSaved("embedded.md");
            h.PumpUntil(() => h.Workspace.History.LoadError is not null, "the history panel's failure");
            h.Workspace.History.LoadFaultForTests = null;
        }

        bool Failed() => kind == "reading" ? reading.LastRefreshFailed : h.Workspace.History.LoadError is not null;
        List<string> seen = RecordKinds(h);
        h.Events.Clear();

        h.Now += TimeSpan.FromMinutes(1);
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Foreground));
        h.Settle();
        Assert.DoesNotContain(kind, seen);
        Assert.True(Failed(), "a foreground run re-checked the failure");

        List<ChangeFeedWindow> windows = RecordWindows(h);
        seen.Clear();
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
        h.Settle();
        ChangeFeedWindow window = Assert.Single(windows);
        Assert.Equal(0UL, window.RecordCount);
        Assert.Single(seen, entry => entry == kind);
        Assert.False(Failed(), "the explicit Refresh did not re-check the failure");
        Assert.Equal([NoChanges], h.Spoken);
    });

    // ---------------------------------------------------------------------
    // The watcher and the graph
    // ---------------------------------------------------------------------

    /// <summary>Spec fact 6: a file another session writes reaches this
    /// session through core's vault watcher, and the Watcher run applies the
    /// window — the tree and Quick Open show it — with no Refresh and nothing
    /// spoken (Q2: its own scan found nothing).</summary>
    [Fact]
    public void AWatcherDrivenChangeFromAnotherSessionIsAppliedWithoutARefresh() => RunSta(() =>
    {
        using IDisposable watcher = VaultWatcherSwitch.Lift();
        using var h = new Harness("feed-watcher", ("alpha.md", "# Alpha\n"));
        using (VaultSession other = VaultSession.OpenFilesystem(h.Root))
        {
            _ = other.CreateExclusive("late.md", "# Late\n");
        }

        h.Context.RunUntil(
            () => h.Sidebar.RootNodes.Any(node => node.Path == "late.md") && !h.Lifecycle.IsRescanActive,
            "the watcher's run listing late.md");
        h.Context.Await(h.Lifecycle.RescanCompletion);

        Assert.Contains("late.md", h.QuickOpen.FilePathsForTests);
        Assert.Empty(h.Events);
    });

    /// <summary>WB-10a's host witness: another session's link reaches the
    /// Connections leaf after a Refresh — the window dropped core's graph,
    /// and the run's probe, which follows the window, rebuilds it.</summary>
    [Fact]
    public void AnotherSessionsLinkReachesTheConnectionsLeafAfterRefresh() => RunSta(() =>
    {
        using var h = new Harness("feed-graph", ("x.md", "# X\n"));
        Graph.ConnectionsLeafViewModel leaf = h.Workspace.Connections;
        h.Workspace.ActiveLeaf = WorkspaceViewModel.Leaves.First(option => option.Id == "connections");
        _ = h.Open("x.md");
        h.PumpUntil(() => leaf.Publication.HoldsTree && leaf.IsCurrent && !leaf.InFlight, "the leaf's first tree");
        Assert.DoesNotContain(leaf.Publication.Tree!.Incoming, row => row.Path == "linker.md");

        using (VaultSession other = VaultSession.OpenFilesystem(h.Root))
        {
            _ = other.CreateExclusive("linker.md", "# Linker\n\n[[x]]\n");
        }

        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        Assert.True(leaf.Publication.HoldsTree, "the leaf holds no tree after the rescan");
        Assert.Contains(leaf.Publication.Tree!.Incoming, row => row.Path == "linker.md");
    });

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    /// <summary>Every window the lifecycle opens from now on, in order.</summary>
    private static List<ChangeFeedWindow> RecordWindows(Harness h)
    {
        var windows = new List<ChangeFeedWindow>();
        h.Lifecycle.ChangeFeedWindowObserverForTests = windows.Add;
        return windows;
    }

    /// <summary>Every rescan publication's kind from now on, each passed
    /// through to the real publication.</summary>
    private static List<string> RecordKinds(Harness h)
    {
        var kinds = new List<string>();
        h.Workspace.RescanPublicationForTests = async (kind, _, publish) =>
        {
            lock (kinds)
            {
                kinds.Add(kind);
            }

            await publish();
        };
        return kinds;
    }

    /// <summary>The rescan core calls since the fact last cleared them, by
    /// operation, without the run's token's own calls.</summary>
    private static string[] Operations(Harness h) =>
        [.. h.CoreCalls.Select(call => call.Operation).Where(operation => operation is not ("token" or "dispose" or "cancel"))];

    private static int ConnectionsProbes(Harness h)
    {
        lock (h.Workspace.Connections.CrossingsForTests)
        {
            return h.Workspace.Connections.CrossingsForTests["graph_generation"];
        }
    }
}
