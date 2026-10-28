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

    /// <summary>A folder another session renamed is a record for each note
    /// it moved: the reading model whose embed lived in the folder
    /// re-projects; one that embeds a note elsewhere does not.</summary>
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

    /// <summary>The owner's decision Q8 for the tree: a tree showing a
    /// failure that no rescan produced — here its tags could not load on an
    /// ordinary refresh, so the retry set is empty — is refreshed by an
    /// explicit Refresh with an empty window, and not by a foreground
    /// run.</summary>
    [Fact]
    public void AnUnchangedRefreshReChecksATreeShowingAFailure() => RunSta(() =>
    {
        using var h = new Harness("feed-recover-tree", ("alpha.md", "# Alpha\n\n#topic\n"));
        h.Sidebar.AfterTagTreeForTests = () => throw new VaultException.Io("injected tag tree failure");
        h.Context.Await(h.Sidebar.RefreshAsync());
        h.Sidebar.AfterTagTreeForTests = null;
        Assert.True(h.Sidebar.ShowsFailedLevelForRescan, "premise: the tree shows its tags' failure");
        Assert.Empty(h.Lifecycle.RetrySetForTests);
        int refreshes = h.Sidebar.TreeGenerationForTests;
        h.Events.Clear();

        h.Now += TimeSpan.FromMinutes(1);
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Foreground));
        Assert.Equal(refreshes, h.Sidebar.TreeGenerationForTests);
        Assert.True(h.Sidebar.ShowsFailedLevelForRescan, "a foreground run re-checked the tree");

        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
        Assert.True(h.Sidebar.TreeGenerationForTests > refreshes, "the explicit Refresh did not refresh the tree");
        Assert.False(h.Sidebar.ShowsFailedLevelForRescan);
        Assert.Contains(h.Sidebar.Tags, tag => tag.Full == "topic");
        Assert.Equal([NoChanges], h.Spoken);
    });

    // ---------------------------------------------------------------------
    // Review rounds (red team and codex round 1)
    // ---------------------------------------------------------------------

    /// <summary>A feed read or the acknowledgment that fails is one error and
    /// leaves the cursor where it was (F1): the next run reads the same
    /// window again and counts it, once; the run after that counts nothing.
    /// A window or page that cannot be read takes the full re-sync, so
    /// another session's note still reaches Quick Open.</summary>
    [Theory]
    [InlineData("window")]
    [InlineData("page")]
    [InlineData("head")]
    [InlineData("acknowledge")]
    public void AFailedFeedReadOrAcknowledgmentIsOneErrorAndTheWindowIsReadAgain(string operation) => RunSta(() =>
    {
        using var h = new Harness($"feed-fault-{operation}", ("alpha.md", "# Alpha\n"));
        List<ChangeFeedWindow> windows = RecordWindows(h);
        using (VaultSession other = VaultSession.OpenFilesystem(h.Root))
        {
            _ = other.CreateExclusive("new.md", "# New\n");
        }

        bool fail = true;
        h.Lifecycle.ChangeFeedFaultForTests = called =>
        {
            if (called != operation || !fail)
            {
                return false;
            }

            fail = false;
            return true;
        };

        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
        Assert.Equal([OneError], h.Spoken);
        Assert.Contains("new.md", h.QuickOpen.FilePathsForTests);

        h.Events.Clear();
        h.Now += TimeSpan.FromMinutes(1);
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
        Assert.Equal([Explicit1], h.Spoken);
        if (windows.Count > 1)
        {
            Assert.Equal(windows[0].FromSeq, windows[^1].FromSeq);
        }

        h.Events.Clear();
        h.Now += TimeSpan.FromMinutes(1);
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
        Assert.Equal([NoChanges], h.Spoken);
    });

    /// <summary>Codex round 1 (high, F1/F4): a run that moved a tab by another
    /// session's rename and then could not acknowledge its window leaves the
    /// cursor behind. When that session renames the file again, core reads
    /// both renames from the same cursor as one record from the first name to
    /// the last; the tab, already at the middle name, still follows it.</summary>
    [Fact]
    public void AnUnacknowledgedRenameIsFollowedByALaterRenameOfItsDestination() => RunSta(() =>
    {
        using var h = new Harness("feed-unacked-chain", ("a.md", "the note\n"));
        WorkspaceTabViewModel tab = h.Open("a.md");
        using (VaultSession other = VaultSession.OpenFilesystem(h.Root))
        {
            _ = other.RenameFile("a.md", "b.md");
        }

        bool fail = true;
        h.Lifecycle.ChangeFeedFaultForTests = called => called == "acknowledge" && fail;
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
        fail = false;
        Assert.Equal([OneError], h.Spoken);
        Assert.Equal("b.md", tab.Path);

        using (VaultSession other = VaultSession.OpenFilesystem(h.Root))
        {
            _ = other.RenameFile("b.md", "c.md");
        }

        h.Events.Clear();
        h.Now += TimeSpan.FromMinutes(1);
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        Assert.Equal("c.md", tab.Path);
        Assert.False(tab.IsMissingFromDisk);
        Assert.Equal("the note\n", tab.Text);
        Assert.Equal(["c.md"], h.QuickOpen.FilePathsForTests);
    });

    /// <summary>The sibling: the rename came home before the next run. Read
    /// from the same cursor the note never moved — there is no record of a
    /// rename at all — and the tab the unacknowledged run moved goes back to
    /// it.</summary>
    [Fact]
    public void AnUnacknowledgedRenameThatCameHomeMovesTheTabBack() => RunSta(() =>
    {
        using var h = new Harness("feed-unacked-home", ("a.md", "the note\n"));
        WorkspaceTabViewModel tab = h.Open("a.md");
        using (VaultSession other = VaultSession.OpenFilesystem(h.Root))
        {
            _ = other.RenameFile("a.md", "b.md");
        }

        bool fail = true;
        h.Lifecycle.ChangeFeedFaultForTests = called => called == "acknowledge" && fail;
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
        fail = false;
        Assert.Equal("b.md", tab.Path);

        using (VaultSession other = VaultSession.OpenFilesystem(h.Root))
        {
            _ = other.RenameFile("b.md", "a.md");
        }

        h.Events.Clear();
        h.Now += TimeSpan.FromMinutes(1);
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        Assert.Equal("a.md", tab.Path);
        Assert.False(tab.IsMissingFromDisk);
        Assert.Equal(["a.md"], h.QuickOpen.FilePathsForTests);
    });

    /// <summary>Codex round 2 (high, F4): a further pass's records name each
    /// file by its path now, not by its path at the cursor. Another session
    /// renames b to c and then a to b, two notes with the same bytes, and
    /// renames b to d while this run applies that window. The further pass
    /// moves the tab now at b — a's — to d, and leaves b's tab at c.</summary>
    [Fact]
    public void AFurtherPassMovesOnlyTheFileItsRecordNames() => RunSta(() =>
    {
        using Harness h = Harness.WithAsyncTree("feed-pass-provenance", ("a.md", "same\n"), ("b.md", "same\n"));
        h.Context.RunUntil(() => !h.Sidebar.IsRefreshingTree, "the open's tree");
        WorkspaceTabViewModel a = h.Open("a.md");
        WorkspaceTabViewModel b = h.Open("b.md");
        List<ChangeFeedWindow> windows = RecordWindows(h);
        using (VaultSession other = VaultSession.OpenFilesystem(h.Root))
        {
            _ = other.RenameFile("b.md", "c.md");
            _ = other.RenameFile("a.md", "b.md");
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
        Assert.Equal("b.md", a.Path);
        Assert.Equal("c.md", b.Path);
        using (VaultSession other = VaultSession.OpenFilesystem(h.Root))
        {
            _ = other.RenameFile("b.md", "d.md");
        }

        unpark.Set();
        h.Context.Await(run);

        Assert.Equal(2, windows.Count);
        Assert.Equal("d.md", a.Path);
        Assert.Equal("c.md", b.Path);
        Assert.False(a.IsMissingFromDisk);
        Assert.False(b.IsMissingFromDisk);
    });

    /// <summary>Codex round 2 (high, F4): a run swaps two tabs by another
    /// session's swap of two notes with the same bytes, then cannot
    /// acknowledge. The next run reads the same two renames again; each tab
    /// is already on its file, so neither moves back, and each keeps its own
    /// unsaved words.</summary>
    [Fact]
    public void AnUnacknowledgedSwapReadAgainLeavesEachTabOnItsFile() => RunSta(() =>
    {
        using var h = new Harness("feed-unacked-swap", ("a.md", "same\n"), ("b.md", "same\n"));
        WorkspaceTabViewModel a = h.Open("a.md");
        WorkspaceTabViewModel b = h.Open("b.md");
        a.Text = "a's unsaved words\n";
        b.Text = "b's unsaved words\n";
        using (VaultSession other = VaultSession.OpenFilesystem(h.Root))
        {
            _ = other.RenameFile("a.md", "swap.md");
            _ = other.RenameFile("b.md", "a.md");
            _ = other.RenameFile("swap.md", "b.md");
        }

        bool fail = true;
        h.Lifecycle.ChangeFeedFaultForTests = called => called == "acknowledge" && fail;
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
        fail = false;
        Assert.Equal("b.md", a.Path);
        Assert.Equal("a.md", b.Path);

        h.Events.Clear();
        h.Now += TimeSpan.FromMinutes(1);
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        Assert.Equal("b.md", a.Path);
        Assert.Equal("a.md", b.Path);
        Assert.Equal("a's unsaved words\n", a.Text);
        Assert.Equal("b's unsaved words\n", b.Text);
    });

    /// <summary>The sibling: another session renames b to c and then a to b,
    /// two notes with the same bytes, and the run cannot acknowledge. Read
    /// again, b to c finds a's tab at b — a path the run put a's file at — and
    /// leaves it there.</summary>
    [Fact]
    public void AnUnacknowledgedChainReadAgainMovesNoTabOnward() => RunSta(() =>
    {
        using var h = new Harness("feed-unacked-chain-again", ("a.md", "same\n"), ("b.md", "same\n"));
        WorkspaceTabViewModel a = h.Open("a.md");
        WorkspaceTabViewModel b = h.Open("b.md");
        using (VaultSession other = VaultSession.OpenFilesystem(h.Root))
        {
            _ = other.RenameFile("b.md", "c.md");
            _ = other.RenameFile("a.md", "b.md");
        }

        bool fail = true;
        h.Lifecycle.ChangeFeedFaultForTests = called => called == "acknowledge" && fail;
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
        fail = false;
        Assert.Equal("b.md", a.Path);
        Assert.Equal("c.md", b.Path);

        h.Events.Clear();
        h.Now += TimeSpan.FromMinutes(1);
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        Assert.Equal("b.md", a.Path);
        Assert.Equal("c.md", b.Path);
        Assert.False(a.IsMissingFromDisk);
        Assert.False(b.IsMissingFromDisk);
    });

    /// <summary>Codex round 2 (medium, F4): another session renames a to b
    /// and edits b; the run moves the tab, reloads it onto the edit, and
    /// cannot acknowledge. When that session renames b to c, core reads one
    /// record from a to c with a's old bytes as its hash, unlike the tab's
    /// baseline now; the tab still follows, since the run moved it.</summary>
    [Fact]
    public void AnUnacknowledgedRenameIsFollowedOnAfterTheMovedTabReloaded() => RunSta(() =>
    {
        using var h = new Harness("feed-unacked-reload", ("a.md", "first\n"));
        WorkspaceTabViewModel tab = h.Open("a.md");
        using (VaultSession other = VaultSession.OpenFilesystem(h.Root))
        {
            _ = other.RenameFile("a.md", "b.md");
            _ = other.SaveText("b.md", "second\n", null);
        }

        bool fail = true;
        h.Lifecycle.ChangeFeedFaultForTests = called => called == "acknowledge" && fail;
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
        fail = false;
        Assert.Equal("b.md", tab.Path);
        Assert.Equal("second\n", tab.Text);

        using (VaultSession other = VaultSession.OpenFilesystem(h.Root))
        {
            _ = other.RenameFile("b.md", "c.md");
        }

        h.Events.Clear();
        h.Now += TimeSpan.FromMinutes(1);
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        Assert.Equal("c.md", tab.Path);
        Assert.False(tab.IsMissingFromDisk);
        Assert.Equal("second\n", tab.Text);
    });

    /// <summary>The sibling: the reloaded note comes home before the next
    /// run, and its tab goes back with it, whatever its baseline is
    /// now.</summary>
    [Fact]
    public void AnUnacknowledgedRenameThatCameHomeMovesTheReloadedTabBack() => RunSta(() =>
    {
        using var h = new Harness("feed-unacked-reload-home", ("a.md", "first\n"));
        WorkspaceTabViewModel tab = h.Open("a.md");
        using (VaultSession other = VaultSession.OpenFilesystem(h.Root))
        {
            _ = other.RenameFile("a.md", "b.md");
            _ = other.SaveText("b.md", "second\n", null);
        }

        bool fail = true;
        h.Lifecycle.ChangeFeedFaultForTests = called => called == "acknowledge" && fail;
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
        fail = false;
        Assert.Equal("b.md", tab.Path);
        Assert.Equal("second\n", tab.Text);

        using (VaultSession other = VaultSession.OpenFilesystem(h.Root))
        {
            _ = other.RenameFile("b.md", "a.md");
        }

        h.Events.Clear();
        h.Now += TimeSpan.FromMinutes(1);
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        Assert.Equal("a.md", tab.Path);
        Assert.False(tab.IsMissingFromDisk);
        Assert.Equal("second\n", tab.Text);
    });

    /// <summary>Codex round 3 (high, F4): a run swaps two tabs by another
    /// session's swap and cannot acknowledge, and that session swaps the notes
    /// back before the next run. Read from the same cursor nothing was
    /// renamed, so each tab goes back to its own file, with its own unsaved
    /// words.</summary>
    [Fact]
    public void AnUnacknowledgedSwapUndoneBeforeTheReplayPutsEachTabBack() => RunSta(() =>
    {
        using var h = new Harness("feed-unacked-swap-undone", ("a.md", "same\n"), ("b.md", "same\n"));
        WorkspaceTabViewModel a = h.Open("a.md");
        WorkspaceTabViewModel b = h.Open("b.md");
        a.Text = "a's unsaved words\n";
        b.Text = "b's unsaved words\n";
        void Swap()
        {
            using VaultSession other = VaultSession.OpenFilesystem(h.Root);
            _ = other.RenameFile("a.md", "swap.md");
            _ = other.RenameFile("b.md", "a.md");
            _ = other.RenameFile("swap.md", "b.md");
        }

        Swap();
        bool fail = true;
        h.Lifecycle.ChangeFeedFaultForTests = called => called == "acknowledge" && fail;
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
        fail = false;
        Assert.Equal("b.md", a.Path);
        Assert.Equal("a.md", b.Path);

        Swap();
        h.Events.Clear();
        h.Now += TimeSpan.FromMinutes(1);
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        Assert.Equal("a.md", a.Path);
        Assert.Equal("b.md", b.Path);
        Assert.Equal("a's unsaved words\n", a.Text);
        Assert.Equal("b's unsaved words\n", b.Text);
    });

    /// <summary>Codex round 4 (high, F4): another session renames board b to
    /// c and then board a to b. b's tab stays — a board moves only when its old
    /// path is gone, and a's board took it — and the run cannot acknowledge;
    /// the run moved no tab, so it remembers no file. That session then renames
    /// b back to a. Read from the same cursor, b's own rename moves its tab to
    /// c, and the board that came home claims no tab.</summary>
    [Fact]
    public void ATabARunLeftInPlaceIsNotClaimedByAFileThatCameHome() => RunSta(() =>
    {
        using var h = new Harness("feed-unmoved-board", ("a.canvas", OneNodeCanvas), ("b.canvas", TwoNodeCanvas));
        WorkspaceTabViewModel board = h.Open("b.canvas");
        using (VaultSession other = VaultSession.OpenFilesystem(h.Root))
        {
            _ = other.RenameFile("b.canvas", "c.canvas");
            _ = other.RenameFile("a.canvas", "b.canvas");
        }

        bool fail = true;
        h.Lifecycle.ChangeFeedFaultForTests = called => called == "acknowledge" && fail;
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
        h.Settle();
        fail = false;
        Assert.Equal("b.canvas", board.Path);
        Assert.Empty(h.Lifecycle.FeedProvenanceForTests);

        using (VaultSession other = VaultSession.OpenFilesystem(h.Root))
        {
            _ = other.RenameFile("b.canvas", "a.canvas");
        }

        h.Events.Clear();
        h.Now += TimeSpan.FromMinutes(1);
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
        h.Settle();

        Assert.Equal("c.canvas", board.Path);
    });

    /// <summary>Codex round 5 (medium, F4): Connections is pinned to a note
    /// no tab holds. Another session renames it a to b, the run moves the pin
    /// and cannot acknowledge, and that session renames it b to c. Read from
    /// the same cursor, one record from a to c: the pin, which the run moved
    /// to b, follows it.</summary>
    [Fact]
    public void APinTheRunMovedWithoutATabFollowsALaterRename() => RunSta(() =>
    {
        using var h = new Harness("feed-unacked-pin", ("a.md", "pinned\n"), ("other.md", "other\n"));
        Assert.True(h.Workspace.Connections.PinTo("a.md"), "premise: Connections pins the note");
        using (VaultSession other = VaultSession.OpenFilesystem(h.Root))
        {
            _ = other.RenameFile("a.md", "b.md");
        }

        bool fail = true;
        h.Lifecycle.ChangeFeedFaultForTests = called => called == "acknowledge" && fail;
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
        fail = false;
        Assert.Equal("b.md", h.Workspace.Connections.Pin);

        using (VaultSession other = VaultSession.OpenFilesystem(h.Root))
        {
            _ = other.RenameFile("b.md", "c.md");
        }

        h.Events.Clear();
        h.Now += TimeSpan.FromMinutes(1);
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        Assert.Equal("c.md", h.Workspace.Connections.Pin);
    });

    /// <summary>The sibling: the pinned note comes home before the next run,
    /// and the pin goes back with it.</summary>
    [Fact]
    public void APinTheRunMovedWithoutATabGoesBackWithANoteThatCameHome() => RunSta(() =>
    {
        using var h = new Harness("feed-unacked-pin-home", ("a.md", "pinned\n"), ("other.md", "other\n"));
        Assert.True(h.Workspace.Connections.PinTo("a.md"), "premise: Connections pins the note");
        using (VaultSession other = VaultSession.OpenFilesystem(h.Root))
        {
            _ = other.RenameFile("a.md", "b.md");
        }

        bool fail = true;
        h.Lifecycle.ChangeFeedFaultForTests = called => called == "acknowledge" && fail;
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
        fail = false;
        Assert.Equal("b.md", h.Workspace.Connections.Pin);

        using (VaultSession other = VaultSession.OpenFilesystem(h.Root))
        {
            _ = other.RenameFile("b.md", "a.md");
        }

        h.Events.Clear();
        h.Now += TimeSpan.FromMinutes(1);
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        Assert.Equal("a.md", h.Workspace.Connections.Pin);
    });

    /// <summary>Codex round 6 (medium, F4): Connections pins a, neither note
    /// open. Another session renames b to c and then a to b; the run moves the
    /// pin to b and cannot acknowledge. That session renames b to d. Read
    /// again, b's own record (b to c) comes first, but the pin at b is a's: it
    /// stays for a's record, which takes it to d.</summary>
    [Fact]
    public void AnotherFilesRecordLeavesARememberedPinToItsOwnFile() => RunSta(() =>
    {
        using var h = new Harness("feed-unacked-pin-collision", ("a.md", "pinned\n"), ("b.md", "the other\n"));
        Assert.True(h.Workspace.Connections.PinTo("a.md"), "premise: Connections pins the note");
        using (VaultSession other = VaultSession.OpenFilesystem(h.Root))
        {
            _ = other.RenameFile("b.md", "c.md");
            _ = other.RenameFile("a.md", "b.md");
        }

        bool fail = true;
        h.Lifecycle.ChangeFeedFaultForTests = called => called == "acknowledge" && fail;
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
        fail = false;
        Assert.Equal("b.md", h.Workspace.Connections.Pin);

        using (VaultSession other = VaultSession.OpenFilesystem(h.Root))
        {
            _ = other.RenameFile("b.md", "d.md");
        }

        h.Events.Clear();
        h.Now += TimeSpan.FromMinutes(1);
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        Assert.Equal("d.md", h.Workspace.Connections.Pin);
    });

    /// <summary>The sibling: the pinned note comes home instead (b back to a);
    /// b's own record leaves the pin, and it goes home with a.</summary>
    [Fact]
    public void AnotherFilesRecordLeavesARememberedPinThatComesHome() => RunSta(() =>
    {
        using var h = new Harness("feed-unacked-pin-collision-home", ("a.md", "pinned\n"), ("b.md", "the other\n"));
        Assert.True(h.Workspace.Connections.PinTo("a.md"), "premise: Connections pins the note");
        using (VaultSession other = VaultSession.OpenFilesystem(h.Root))
        {
            _ = other.RenameFile("b.md", "c.md");
            _ = other.RenameFile("a.md", "b.md");
        }

        bool fail = true;
        h.Lifecycle.ChangeFeedFaultForTests = called => called == "acknowledge" && fail;
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
        fail = false;
        Assert.Equal("b.md", h.Workspace.Connections.Pin);

        using (VaultSession other = VaultSession.OpenFilesystem(h.Root))
        {
            _ = other.RenameFile("b.md", "a.md");
        }

        h.Events.Clear();
        h.Now += TimeSpan.FromMinutes(1);
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        Assert.Equal("a.md", h.Workspace.Connections.Pin);
    });

    /// <summary>Codex round 3 (medium, F4): another session renames a to b,
    /// the run moves the tab and cannot acknowledge; this session's user
    /// renames b to c, and the other session renames c to d. Core reads one
    /// record from a to d; the own rename carried the tab's provenance to c,
    /// so the tab follows to d.</summary>
    [Fact]
    public void AnOwnRenameAfterAnUnacknowledgedRenameCarriesTheTabsProvenance() => RunSta(() =>
    {
        using var h = new Harness("feed-unacked-own-rename", ("a.md", "the note\n"));
        WorkspaceTabViewModel tab = h.Open("a.md");
        using (VaultSession other = VaultSession.OpenFilesystem(h.Root))
        {
            _ = other.RenameFile("a.md", "b.md");
        }

        bool fail = true;
        h.Lifecycle.ChangeFeedFaultForTests = called => called == "acknowledge" && fail;
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
        fail = false;
        Assert.Equal("b.md", tab.Path);

        Assert.Equal("b.md", h.Lifecycle.FeedProvenanceForTests["a.md"]);
        _ = h.Lifecycle.SessionForTests!.RenameFile("b.md", "c.md");
        h.Context.Drain();
        Assert.Equal("c.md", tab.Path);
        Assert.Equal("c.md", h.Lifecycle.FeedProvenanceForTests["a.md"]);
        using (VaultSession other = VaultSession.OpenFilesystem(h.Root))
        {
            _ = other.RenameFile("c.md", "d.md");
        }

        h.Events.Clear();
        h.Now += TimeSpan.FromMinutes(1);
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        Assert.Equal("d.md", tab.Path);
        Assert.False(tab.IsMissingFromDisk);
        Assert.Equal("the note\n", tab.Text);
    });

    /// <summary>Codex round 3 (medium, F4): a duplicate of a dirty tab an
    /// earlier window marked stale carries the hash it was marked against, so
    /// both tabs follow another session's later rename with their unsaved
    /// words.</summary>
    [Fact]
    public void ADuplicateOfAStaleDirtyTabFollowsAForeignRename() => RunSta(() =>
    {
        using var h = new Harness("feed-stale-duplicate", ("x.md", "x one\n"));
        WorkspaceTabViewModel tab = h.Open("x.md");
        tab.Text = "my unsaved words\n";
        using (VaultSession other = VaultSession.OpenFilesystem(h.Root))
        {
            _ = other.SaveText("x.md", "x two, by another window\n", null);
        }

        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
        Assert.True(tab.IsExternallyStale, "premise: the dirty tab is marked stale");
        h.Workspace.DuplicateTabCommand.Execute(null);
        WorkspaceTabViewModel duplicate = Assert.IsType<WorkspaceTabViewModel>(h.Workspace.ActiveGroup.ActiveTab);
        Assert.NotSame(tab, duplicate);
        Assert.Equal("x.md", duplicate.Path);
        using (VaultSession other = VaultSession.OpenFilesystem(h.Root))
        {
            _ = other.RenameFile("x.md", "y.md");
        }

        h.Now += TimeSpan.FromMinutes(1);
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        Assert.Equal("y.md", tab.Path);
        Assert.Equal("y.md", duplicate.Path);
        Assert.False(duplicate.IsMissingFromDisk);
        Assert.Equal("my unsaved words\n", duplicate.Text);
    });

    /// <summary>Codex round 2 (low, F6): a journal that still moves after its
    /// third re-read leaves Quick Open in the retry set, with no error, and
    /// the next run's full reload brings every own file in.</summary>
    [Fact]
    public void AJournalStillMovingAfterItsThirdReReadLeavesQuickOpenToTheNextRun() => RunSta(() =>
    {
        using var h = new Harness("feed-journal-exhausted", ("alpha.md", "# Alpha\n"));
        using (VaultSession other = VaultSession.OpenFilesystem(h.Root))
        {
            _ = other.CreateExclusive("brief.md", "# Brief\n");
        }

        int written = 0;
        h.Lifecycle.ChangeFeedWindowObserverForTests = opened =>
        {
            if (written == 0)
            {
                _ = h.Lifecycle.SessionForTests!.CreateExclusive($"own{written++}.md", "# Own\n");
            }
        };
        h.Lifecycle.ChangeFeedFaultForTests = called =>
        {
            if (called == "summary" && written < 8)
            {
                _ = h.Lifecycle.SessionForTests!.CreateExclusive($"own{written++}.md", "# Own\n");
            }

            return false;
        };

        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
        Assert.Contains(RescanSurface.QuickOpen, h.Lifecycle.RetrySetForTests);
        Assert.True(written > 3, "premise: an own write landed during every re-read");
        Assert.DoesNotContain(h.Events, spoken => spoken is A11yEvent.VaultRescanIncomplete);

        h.Lifecycle.ChangeFeedFaultForTests = null;
        h.Lifecycle.ChangeFeedWindowObserverForTests = null;
        h.Events.Clear();
        h.CoreCalls.Clear();
        h.Now += TimeSpan.FromMinutes(1);
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Foreground));

        Assert.Contains("list", Operations(h));
        Assert.DoesNotContain(RescanSurface.QuickOpen, h.Lifecycle.RetrySetForTests);
        Assert.All(Enumerable.Range(0, written), n => Assert.Contains($"own{n}.md", h.QuickOpen.FilePathsForTests));
    });

    /// <summary>Red team 3 / codex round 1: a foreground run reloads no
    /// Quick Open list, so its records alone bring another session's
    /// creations, deletion and rename to Quick Open, page after page.</summary>
    [Fact]
    public void AForegroundRunAppliesQuickOpensRecordsWithoutReloadingItsList() => RunSta(() =>
    {
        using var h = new Harness(
            "feed-quick-open-records",
            ("edit.md", "edit one\n"),
            ("drop.md", "# Drop\n"),
            ("move.md", "move me\n"));
        h.Lifecycle.ChangeFeedPageLimitForTests = 2;
        using (VaultSession other = VaultSession.OpenFilesystem(h.Root))
        {
            _ = other.CreateExclusive("one.md", "# One\n");
            _ = other.CreateExclusive("two.md", "# Two\n");
            other.DeleteFile("drop.md");
            _ = other.RenameFile("move.md", "moved.md");
        }

        h.CoreCalls.Clear();
        h.Now += TimeSpan.FromMinutes(1);
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Foreground));

        Assert.DoesNotContain("list", Operations(h));
        Assert.True(Operations(h).Count(operation => operation == "page") >= 3, "the records in pages of two");
        Assert.Equal(["edit.md", "moved.md", "one.md", "two.md"], h.QuickOpen.FilePathsForTests.Order(StringComparer.Ordinal));
        Assert.Empty(h.Events);
    });

    /// <summary>Red team 4 (Q3): a dirty tab an earlier window marked stale
    /// keeps its older baseline, while core names the file's content as this
    /// session last saw it — the hash the tab was marked against — as the
    /// rename's <c>from_hash</c>. The tab follows another session's later
    /// rename with its unsaved words.</summary>
    [Fact]
    public void AForeignRenameMovesADirtyTabThatAnEarlierWindowMarkedStale() => RunSta(() =>
    {
        using var h = new Harness("feed-stale-rename", ("x.md", "x one\n"));
        WorkspaceTabViewModel tab = h.Open("x.md");
        tab.Text = "my unsaved words\n";
        using (VaultSession other = VaultSession.OpenFilesystem(h.Root))
        {
            _ = other.SaveText("x.md", "x two, by another window\n", null);
        }

        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
        Assert.True(tab.IsExternallyStale, "premise: the dirty tab is marked stale");

        using (VaultSession other = VaultSession.OpenFilesystem(h.Root))
        {
            _ = other.RenameFile("x.md", "y.md");
        }

        h.Now += TimeSpan.FromMinutes(1);
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        Assert.Equal("y.md", tab.Path);
        Assert.False(tab.IsMissingFromDisk);
        Assert.True(tab.IsDirty);
        Assert.Equal("my unsaved words\n", tab.Text);
    });

    /// <summary>Codex round 2 (medium, F2): the re-seat's twin of the fact
    /// below. A missing base comes back, the run re-seats its tab, and the
    /// re-seated document's load fails after its definition opened; it reads
    /// as current next run, so the Bases retry re-runs it and a foreground
    /// run with an empty window brings it back.</summary>
    [Fact]
    public void ARestoredBaseWhoseLoadFailsIsRetriedByTheNextForegroundRun() => RunSta(() =>
    {
        using var h = new Harness("feed-reseat-base-retry", ("Notes.base", MainBase), ("n.md", "# N\n"));
        WorkspaceTabViewModel tab = h.Open("Notes.base");
        h.PumpUntil(() => tab.Base?.State == BaseLoadState.Ready, "the base's first load");
        using (VaultSession other = VaultSession.OpenFilesystem(h.Root))
        {
            other.DeleteFile("Notes.base");
        }

        Task run = h.Lifecycle.RescanAsync(RescanReason.Explicit);
        h.PumpUntil(() => run.IsCompleted, "the deleting rescan ending");
        Assert.True(tab.IsMissingFromDisk, "premise: the deleted base's tab is missing");

        h.Write("Notes.base", MainBase);
        int fail = 1;
        h.Workspace.RescanPublicationForTests = (kind, _, publish) =>
        {
            if (kind == "base" && tab.Base is { } restored && Interlocked.Exchange(ref fail, 0) == 1)
            {
                restored.AfterOpenForTests = () =>
                {
                    restored.AfterOpenForTests = null;
                    throw new VaultException.Io("injected views failure");
                };
            }

            return publish();
        };
        h.Events.Clear();
        h.Now += TimeSpan.FromMinutes(1);
        run = h.Lifecycle.RescanAsync(RescanReason.Explicit);
        h.PumpUntil(() => run.IsCompleted, "the restoring rescan ending");
        h.Workspace.RescanPublicationForTests = null;
        Assert.False(tab.IsMissingFromDisk, "premise: the restored base's tab re-seated");
        BaseDocumentViewModel document = Assert.IsType<BaseDocumentViewModel>(tab.Base);
        Assert.Equal(BaseLoadState.Failed, document.State);
        Assert.Contains(RescanSurface.Bases, h.Lifecycle.RetrySetForTests);

        h.Events.Clear();
        h.Now += TimeSpan.FromMinutes(1);
        run = h.Lifecycle.RescanAsync(RescanReason.Foreground);
        h.PumpUntil(() => run.IsCompleted, "the next rescan ending");

        Assert.Equal(BaseLoadState.Ready, document.State);
        Assert.Empty(h.Events);
    });

    /// <summary>Red team 1: a base whose reopen fails after its definition
    /// opened (its views could not be read) reads as current on the next run,
    /// so no reopen is due there; the Bases surface's retry re-runs it, and a
    /// foreground run with an empty window brings it back.</summary>
    [Fact]
    public void AFailedBaseReopenIsRetriedByTheNextForegroundRun() => RunSta(() =>
    {
        using var h = new Harness("feed-base-retry", ("Notes.base", MainBase), ("n.md", "# N\n"));
        WorkspaceTabViewModel tab = h.Open("Notes.base");
        h.PumpUntil(() => tab.Base?.State == BaseLoadState.Ready, "the base's first load");
        BaseDocumentViewModel document = Assert.IsType<BaseDocumentViewModel>(tab.Base);
        int fail = 1;
        document.AfterOpenForTests = () =>
        {
            if (Interlocked.Exchange(ref fail, 0) == 1)
            {
                throw new VaultException.Io("injected views failure");
            }
        };
        using (VaultSession other = VaultSession.OpenFilesystem(h.Root))
        {
            _ = other.SaveText("Notes.base", MainBase.Replace("name: Main", "name: Renamed", StringComparison.Ordinal), null);
        }

        Task run = h.Lifecycle.RescanAsync(RescanReason.Explicit);
        h.PumpUntil(() => run.IsCompleted, "the rescan ending");
        Assert.Equal(BaseLoadState.Failed, document.State);
        Assert.IsType<A11yEvent.VaultRescanIncomplete>(Assert.Single(h.Events));
        Assert.Contains(RescanSurface.Bases, h.Lifecycle.RetrySetForTests);

        h.Events.Clear();
        h.Now += TimeSpan.FromMinutes(1);
        run = h.Lifecycle.RescanAsync(RescanReason.Foreground);
        h.PumpUntil(() => run.IsCompleted, "the next rescan ending");

        Assert.Equal(BaseLoadState.Ready, document.State);
        Assert.Equal("Renamed", document.ActiveViewName);
        Assert.Empty(h.Events);
    });

    /// <summary>Codex round 1 (F5): a dirty note another session edited stays
    /// marked stale, its index hash unlike its baseline, run after run; the
    /// history panel showing it reloads for the record, not for every later
    /// empty window.</summary>
    [Fact]
    public void AnEmptyForegroundWindowLeavesAStaleDirtyNotesHistoryAlone() => RunSta(() =>
    {
        using var h = new Harness("feed-stale-history", ("x.md", "x0\n"));
        WorkspaceTabViewModel x = h.Open("x.md");
        x.Text = "x0\nmine\n";
        h.Workspace.History.NoteChanged("x.md");
        using (VaultSession other = VaultSession.OpenFilesystem(h.Root))
        {
            _ = other.SaveText("x.md", "x1, by another window\n", null);
        }

        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
        h.Settle();
        Assert.True(x.IsExternallyStale, "premise: the dirty tab is marked stale");
        int loads = 0;
        h.Workspace.History.LoadInterleaveForTests = () => Interlocked.Increment(ref loads);

        h.Now += TimeSpan.FromMinutes(1);
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Foreground));
        h.Settle();

        Assert.Equal(0, Volatile.Read(ref loads));
        Assert.Equal("x0\nmine\n", x.Text);
    });

    /// <summary>Codex round 1 (F2): the retry set rides the full re-sync too.
    /// History fails, its window is acknowledged, and the next window is a
    /// gap: its ungated dependents pass still retries History, whose note's
    /// tab is current, so nothing but the retry reloads the panel.</summary>
    [Fact]
    public void AFullReSyncStillRetriesAFailedHistory() => RunSta(() =>
    {
        using Harness h = DependentsHarness("feed-retry-history-gap");
        _ = h.Open("embedded.md");
        h.Workspace.History.NoteChanged("embedded.md");
        h.Settle();
        bool fail = true;
        h.Workspace.RescanPublicationForTests = async (kind, _, publish) =>
        {
            if (kind == "history" && fail)
            {
                fail = false;
                throw new IOException("injected history publication failure");
            }

            await publish();
        };
        using (VaultSession other = VaultSession.OpenFilesystem(h.Root))
        {
            _ = other.SaveText("embedded.md", "Embedded after, by another window.\n", null);
        }

        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
        h.Settle();
        Assert.Contains(RescanSurface.History, h.Lifecycle.RetrySetForTests);
        int loads = 0;
        h.Workspace.History.LoadInterleaveForTests = () => Interlocked.Increment(ref loads);
        using (VaultSession other = VaultSession.OpenFilesystem(h.Root))
        {
            _ = other.CreateExclusive("late.md", "# Late\n");
        }

        CacheSql.TrimTheFeedPastTheCursor(h.Root);
        List<ChangeFeedWindow> windows = RecordWindows(h);
        h.Now += TimeSpan.FromMinutes(1);
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Foreground));
        h.Settle();

        Assert.IsType<ChangeFeedStatus.Gap>(Assert.Single(windows).Status);
        Assert.True(Volatile.Read(ref loads) > 0, "the full re-sync dropped History's retry");
        Assert.DoesNotContain(RescanSurface.History, h.Lifecycle.RetrySetForTests);
    });

    /// <summary>Codex round 1 / red team 2 (Q8): a reading model whose own
    /// projection failed — a note with no embeds, so it depends on no other
    /// file — is re-checked by an unchanged explicit Refresh, and not by a
    /// foreground run.</summary>
    [Fact]
    public void AnUnchangedRefreshReChecksAReadingModelWhoseOwnProjectionFailed() => RunSta(() =>
    {
        using var h = new Harness("feed-recover-own-reading", ("plain.md", "# Plain\n\nNo embeds.\n"));
        WorkspaceTabViewModel plain = h.Open("plain.md");
        plain.ToggleViewMode();
        h.PumpUntil(() => ReadingText(plain).Contains("No embeds.", StringComparison.Ordinal), "the first projection");
        // Codex round 2: the projection fails outside any rescan, so no
        // retry owns it — only the recovery set does.
        ReadingContentViewModel reading = plain.Reading!;
        reading.FetchFaultForTests = () => new VaultException.Io("injected projection failure");
        reading.Refresh();
        h.PumpUntil(() => reading.LastRefreshFailed, "the failed projection");
        reading.FetchFaultForTests = null;
        reading.BlocksAppended += _ => { };
        Assert.DoesNotContain(RescanSurface.Reading, h.Lifecycle.RetrySetForTests);
        h.Events.Clear();

        h.Now += TimeSpan.FromMinutes(1);
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Foreground));
        h.Settle();
        Assert.True(reading.LastRefreshFailed, "a foreground run re-checked the projection");

        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
        h.Settle();
        Assert.False(reading.LastRefreshFailed, "the explicit Refresh did not re-check the projection");
        Assert.Equal([NoChanges], h.Spoken);
    });

    /// <summary>Codex round 2 (medium, F2): a reloaded reading-mode note whose
    /// projection fails reads as current on the next run; its failure puts
    /// Reading in the retry set, so a foreground run with an empty window
    /// re-checks it and brings the projection back.</summary>
    [Fact]
    public void AReloadedNoteWhoseProjectionFailedIsRetriedByTheNextForegroundRun() => RunSta(() =>
    {
        using var h = new Harness("feed-reading-retry", ("plain.md", "# Plain\n\nNo embeds.\n"));
        WorkspaceTabViewModel plain = h.Open("plain.md");
        plain.ToggleViewMode();
        h.PumpUntil(() => ReadingText(plain).Contains("No embeds.", StringComparison.Ordinal), "the first projection");
        plain.ReadingCreatedForTests = model => model.FetchFaultForTests = () => new VaultException.Io("injected projection failure");
        h.Write("plain.md", "# Plain\n\nChanged outside Slate.\n");

        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
        h.Settle();
        ReadingContentViewModel reading = plain.Reading!;
        Assert.True(reading.LastRefreshFailed, "premise: the reloaded tab's projection failed");
        Assert.Contains(RescanSurface.Reading, h.Lifecycle.RetrySetForTests);
        plain.ReadingCreatedForTests = null;
        reading.FetchFaultForTests = null;
        reading.BlocksAppended += _ => { };
        h.Events.Clear();

        h.Now += TimeSpan.FromMinutes(1);
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Foreground));
        h.Settle();

        Assert.False(reading.LastRefreshFailed, "the foreground run never re-checked the failed projection");
        Assert.DoesNotContain(RescanSurface.Reading, h.Lifecycle.RetrySetForTests);
        Assert.Empty(h.Events);
    });

    /// <summary>Red team 2 / codex round 1 (F5): an embed whose target could
    /// not be read names no path, so no record names it; a window with any
    /// record re-projects a model holding an unresolved embed, and the card
    /// follows once its target is readable and changed.</summary>
    [Fact]
    public void AnEmbedThatCouldNotBeReadReProjectsWhenItsTargetChanges() => RunSta(() =>
    {
        using var h = new Harness(
            "feed-unreadable-embed",
            ("host.md", "# Host\n\n![[target]]\n"),
            ("target.md", "Target before.\n"));
        WorkspaceTabViewModel host;
        using (DenyAccess.To(Path.Combine(h.Root, "target.md"), FileSystemRights.ReadData))
        {
            host = h.Open("host.md");
            host.ToggleViewMode();
            h.PumpUntil(() => host.Reading?.Document is not null && !ReadingText(host).Contains("Target before.", StringComparison.Ordinal) && ReadingText(host).Length > 0, "the host's projection with an unreadable embed");
        }

        Assert.False(host.Reading!.HasPendingDependencyRefresh);
        using (VaultSession other = VaultSession.OpenFilesystem(h.Root))
        {
            _ = other.SaveText("target.md", "Target after, by another window.\n", null);
        }

        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
        h.Settle();

        Assert.True(host.Reading!.HasPendingDependencyRefresh, "the unresolved embed was never re-projected");
    });

    /// <summary>Red team 5 (Q3): another session swaps two notes with the
    /// same bytes, so both renames carry the same <c>from_hash</c>. Every
    /// move is planned against the tabs as they were, so each tab follows its
    /// own file once, and neither lands on the other's.</summary>
    [Fact]
    public void AForeignSwapOfTwoNotesMovesEachTabOnce() => RunSta(() =>
    {
        using var h = new Harness("feed-swap", ("a.md", "same\n"), ("b.md", "same\n"));
        WorkspaceTabViewModel a = h.Open("a.md");
        WorkspaceTabViewModel b = h.Open("b.md");
        using (VaultSession other = VaultSession.OpenFilesystem(h.Root))
        {
            _ = other.RenameFile("a.md", "swap.md");
            _ = other.RenameFile("b.md", "a.md");
            _ = other.RenameFile("swap.md", "b.md");
        }

        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        Assert.Equal("b.md", a.Path);
        Assert.Equal("a.md", b.Path);
        Assert.False(a.IsMissingFromDisk);
        Assert.False(b.IsMissingFromDisk);
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
