// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Windows.Documents;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W7-7 PR 7 (#1252, contract R-9): Files Sidebar → Refresh and the
/// foreground rescan reconcile what changed outside Slate — the tree, Quick
/// Open, every open tab — and say what they found in exactly one sentence.
/// </summary>
/// <remarks>
/// Every fact drives the REAL path: a real vault on disk, the lifecycle's
/// own <c>RescanAsync</c>, core's rescan and delta ledger through the
/// binding, and the workspace's real tabs. The only seams are the ones the
/// lifecycle already takes — the session-load worker (to park a scan or
/// make it throw), the clock, and the delta channel (a pass-through that can
/// fail a page or record when each ledger call arrives). Assertions over
/// what was spoken are over the WHOLE captured event sequence. Each fact
/// runs on its own STA thread, which owns the editor and reading documents
/// and drains every continuation the lifecycle awaits.
/// </remarks>
public sealed class RescanTests
{
    private const string Explicit1 = "Files refreshed. 1 new or changed, 0 removed.";
    private const string NoChanges = "Files refreshed. No changes.";
    private const string OneError = "Files refreshed with errors. 1 error; results may be incomplete.";

    // ---------------------------------------------------------------------
    // Refresh and the progress policy
    // ---------------------------------------------------------------------

    /// <summary>The whole captured sequence of an explicit Refresh is ONE
    /// <c>VaultRescanFinished</c> — no <c>VaultScanStarted</c>, progress or
    /// <c>VaultScanFinished</c> (the reason-aware progress policy) — and it
    /// runs from the sidebar's own command.</summary>
    [Fact]
    public void RefreshSpeaksExactlyOneCompletion() => RunSta(() =>
    {
        using var h = new Harness("refresh-one", ("alpha.md", "# Alpha\n"));
        h.Write("late.md", "# Late\n");

        h.Sidebar.RefreshCommand.Execute(null);
        h.Context.Await(h.Lifecycle.RescanCompletion);

        A11yEvent.VaultRescanFinished finished = Assert.IsType<A11yEvent.VaultRescanFinished>(
            Assert.Single(h.Events));
        Assert.Equal((RescanReason.Explicit, 1UL, 0UL), (finished.Reason, finished.Changed, finished.Removed));
        Assert.Equal([Explicit1], h.Spoken);
        Assert.Equal(Explicit1, h.Lifecycle.StatusText);
        Assert.Contains(h.Sidebar.RootNodes, node => node.Path == "late.md");
    });

    /// <summary>An unchanged foreground rescan posts NOTHING — the whole
    /// sequence is empty, not merely free of <c>VaultRescanFinished</c> — and
    /// a second one inside the cooldown does not even scan.</summary>
    [Fact]
    public void ForegroundRescanIsThrottledAndSilentWhenNothingChanged() => RunSta(() =>
    {
        using var h = new Harness("foreground-quiet", ("alpha.md", "# Alpha\n"));
        int scans = h.ScanCalls;

        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Foreground));
        Assert.Empty(h.Events);
        Assert.Equal(scans + 1, h.ScanCalls);

        // Inside the cooldown: no run starts, even with a change on disk.
        h.Now += VaultLifecycleViewModel.ForegroundRescanCooldown - TimeSpan.FromSeconds(1);
        h.Write("late.md", "# Late\n");
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Foreground));
        Assert.Equal(scans + 1, h.ScanCalls);
        Assert.Empty(h.Events);

        // Past it the change is found — and a changed foreground run speaks.
        h.Now += TimeSpan.FromSeconds(2);
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Foreground));
        Assert.Equal(scans + 2, h.ScanCalls);
        Assert.Equal([Explicit1], h.Spoken);
        Assert.Equal(
            RescanReason.Foreground,
            Assert.IsType<A11yEvent.VaultRescanFinished>(Assert.Single(h.Events)).Reason);
    });

    /// <summary>A slow-path re-read of the same bytes is not a change,
    /// through the host's gating and copy: the open says "N files, 0 new or
    /// changed", a foreground rescan is silent, an explicit one says "No
    /// changes".</summary>
    [Fact]
    public void ATouchedUnchangedVaultSaysNoChanges() => RunSta(() =>
    {
        string root = Harness.NewRoot("touched");
        Harness.Seed(root, ("a.md", "# A\n"), ("b.md", "# B\n"), ("c.md", "# C\n"));
        using (VaultSession warm = VaultSession.OpenFilesystem(root))
        using (var cancel = new CancelToken())
        {
            Assert.Equal(3UL, warm.ScanInitial(cancel).FilesIndexed);
        }

        Harness.TouchAll(root);
        using var h = Harness.OpenExisting(root);

        Assert.Equal(
            [
                $"Vault {h.Lifecycle.VaultDisplayName} opened. Scanning files for the sidebar.",
                "Scanning vault. 3 files to index.",
                "Scan complete. 3 files, 0 new or changed.",
            ],
            h.OpenSpoken);
        A11yEvent.VaultScanFinished launch = Assert.IsType<A11yEvent.VaultScanFinished>(h.OpenEvents[^1]);
        Assert.Equal((3UL, 0UL), (launch.FilesSeen, launch.FilesChanged));
        Assert.Equal("Scan finished: 3 files, 0 new or changed.", h.OpenStatusText);

        Harness.TouchAll(root);
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Foreground));
        Assert.Empty(h.Events);

        Harness.TouchAll(root);
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
        Assert.Equal([NoChanges], h.Spoken);
    });

    // ---------------------------------------------------------------------
    // The pending-reason lattice (parked first scans)
    // ---------------------------------------------------------------------

    /// <summary>Explicit ⊔ Foreground = Explicit: a Refresh during an
    /// unchanged foreground run still announces, from the ONE follow-up.</summary>
    [Fact]
    public void ExplicitRefreshDuringAForegroundRescanStillAnnounces() => RunSta(() =>
    {
        using var h = new Harness("lattice-explicit-over-foreground", ("alpha.md", "# Alpha\n"));
        int scans = h.ScanCalls;
        h.ParkAfterCall = scans + 1;

        Task run = h.Lifecycle.RescanAsync(RescanReason.Foreground);
        h.Context.RunUntil(() => h.Parked.IsSet, "the foreground scan parking");
        Assert.Same(run, h.Lifecycle.RescanAsync(RescanReason.Explicit));
        h.Release.Set();
        h.Context.Await(run);

        Assert.Equal(
            RescanReason.Explicit,
            Assert.IsType<A11yEvent.VaultRescanFinished>(Assert.Single(h.Events)).Reason);
        Assert.Equal([NoChanges], h.Spoken);
        Assert.Equal(scans + 2, h.ScanCalls);
    });

    /// <summary>A foreground request never downgrades a running explicit
    /// Refresh, and a pending Explicit stays Explicit when a foreground
    /// request joins it (Explicit ⊔ Foreground = Explicit).</summary>
    [Fact]
    public void AForegroundRequestDuringAnExplicitRescanStaysExplicit() => RunSta(() =>
    {
        // The running explicit run keeps its sentence; the foreground
        // follow-up it earned is silent.
        using (var h = new Harness("lattice-foreground-under-explicit", ("alpha.md", "# Alpha\n")))
        {
            int scans = h.ScanCalls;
            h.ParkAfterCall = scans + 1;
            Task run = h.Lifecycle.RescanAsync(RescanReason.Explicit);
            h.Context.RunUntil(() => h.Parked.IsSet, "the explicit scan parking");
            _ = h.Lifecycle.RescanAsync(RescanReason.Foreground);
            h.Release.Set();
            h.Context.Await(run);

            Assert.Equal(
                RescanReason.Explicit,
                Assert.IsType<A11yEvent.VaultRescanFinished>(Assert.Single(h.Events)).Reason);
            Assert.Equal([NoChanges], h.Spoken);
            Assert.Equal(scans + 2, h.ScanCalls);
        }

        // A pending Explicit is not overwritten by a later Foreground.
        using (var h = new Harness("lattice-pending-explicit", ("alpha.md", "# Alpha\n")))
        {
            int scans = h.ScanCalls;
            h.ParkAfterCall = scans + 1;
            Task run = h.Lifecycle.RescanAsync(RescanReason.Explicit);
            h.Context.RunUntil(() => h.Parked.IsSet, "the explicit scan parking");
            _ = h.Lifecycle.RescanAsync(RescanReason.Explicit);
            _ = h.Lifecycle.RescanAsync(RescanReason.Foreground);
            h.Release.Set();
            h.Context.Await(run);

            Assert.Equal([NoChanges, NoChanges], h.Spoken);
            Assert.All(h.Events, e => Assert.Equal(
                RescanReason.Explicit,
                Assert.IsType<A11yEvent.VaultRescanFinished>(e).Reason));
            Assert.Equal(scans + 2, h.ScanCalls);
        }
    });

    /// <summary>Foreground ⊔ Foreground = Foreground: two overlapping
    /// foreground requests make ONE silent follow-up, never an explicit
    /// announcement — the whole sequence stays empty.</summary>
    [Fact]
    public void TwoForegroundRequestsStayForeground() => RunSta(() =>
    {
        using var h = new Harness("lattice-foreground-twice", ("alpha.md", "# Alpha\n"));
        int scans = h.ScanCalls;
        h.ParkAfterCall = scans + 1;

        Task run = h.Lifecycle.RescanAsync(RescanReason.Foreground);
        h.Context.RunUntil(() => h.Parked.IsSet, "the foreground scan parking");
        _ = h.Lifecycle.RescanAsync(RescanReason.Foreground);
        _ = h.Lifecycle.RescanAsync(RescanReason.Foreground);
        h.Release.Set();
        h.Context.Await(run);

        Assert.Empty(h.Events);
        Assert.Equal(scans + 2, h.ScanCalls);
    });

    // ---------------------------------------------------------------------
    // Completeness
    // ---------------------------------------------------------------------

    /// <summary>A subtree the walk cannot list makes the rescan partial:
    /// its one sentence is <c>VaultRescanIncomplete</c> with an honest count,
    /// never "No changes" — and the refreshed tree still lists the folder
    /// (an incomplete walk prunes nothing).</summary>
    [Fact]
    public void APartialRescanNeverSaysNoChanges() => RunSta(() =>
    {
        using var h = new Harness(
            "partial",
            ("root.md", "# Root\n"),
            ("sub/nested.md", "# Nested\n"));
        using var deny = DenyAccess.To(Path.Combine(h.Root, "sub"), FileSystemRights.ListDirectory);

        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        Assert.Equal(1UL, Assert.IsType<A11yEvent.VaultRescanIncomplete>(Assert.Single(h.Events)).Errors);
        Assert.Equal([OneError], h.Spoken);
        Assert.Contains(h.Sidebar.RootNodes, node => node.Path == "sub" && node.IsDirectory);
    });

    /// <summary>One file whose read fails makes the rescan incomplete through
    /// the Windows completion path — even though the delta itself is empty.</summary>
    [Fact]
    public void AnUnreadableFileMakesTheRescanIncomplete() => RunSta(() =>
    {
        using var h = new Harness("unreadable", ("alpha.md", "# Alpha\n"));
        h.Write("locked.md", "# Locked\n");
        using var deny = DenyAccess.To(Path.Combine(h.Root, "locked.md"), FileSystemRights.ReadData);

        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        Assert.Equal(1UL, Assert.IsType<A11yEvent.VaultRescanIncomplete>(Assert.Single(h.Events)).Errors);
        Assert.Equal([OneError], h.Spoken);
    });

    /// <summary>A scan call that throws before any report exists posts
    /// <c>VaultRescanIncomplete { errors: 1 }</c> — never silence, never "No
    /// changes" — for either reason.</summary>
    [Fact]
    public void AScanThatThrowsBeforeAnyReportSpeaksIncomplete() => RunSta(() =>
    {
        using var h = new Harness("scan-throws", ("alpha.md", "# Alpha\n"));
        h.ThrowOnCall = _ => new IOException("injected scan failure");

        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
        Assert.Equal([OneError], h.Spoken);
        Assert.Equal(OneError, h.Lifecycle.StatusText);

        h.Events.Clear();
        h.Now += TimeSpan.FromMinutes(1);
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Foreground));
        Assert.Equal([OneError], h.Spoken);
    });

    // ---------------------------------------------------------------------
    // Reconciliation: tabs, Quick Open, the tree
    // ---------------------------------------------------------------------

    /// <summary>Through the real rescan → delta → tab path: a clean tab
    /// reloads (new text and baseline, not dirty, no undo back to the old
    /// bytes, caret line kept — or clamped), a reading-mode tab re-projects,
    /// a dirty tab keeps its edits and turns stale, and a deleted open
    /// file's tab is invalidated — SILENTLY: the whole sequence is the one
    /// completion sentence, no "missing from disk".</summary>
    [Fact]
    public void ARescanReconcilesOpenTabs() => RunSta(() =>
    {
        using var h = new Harness(
            "tabs",
            ("clean.md", "line one\nline two\nline three\n"),
            ("clamp.md", "1\n2\n3\n4\nfive five\n"),
            ("reading.md", "# Reading\n\nold body\n"),
            ("dirty.md", "dirty original\n"),
            ("gone.md", "gone\n"));
        WorkspaceTabViewModel clean = h.Open("clean.md");
        WorkspaceTabViewModel clamp = h.Open("clamp.md");
        WorkspaceTabViewModel reading = h.Open("reading.md");
        WorkspaceTabViewModel dirty = h.Open("dirty.md");
        WorkspaceTabViewModel gone = h.Open("gone.md");
        clean.EditorCaretOffset = OffsetOf(clean.Text, line: 3, column: 5);
        clamp.EditorCaretOffset = OffsetOf(clamp.Text, line: 5, column: 8);
        reading.ToggleViewMode();
        Assert.Contains("old body", ReadingText(reading));
        dirty.Text = "my unsaved edit\n";
        Assert.True(dirty.IsDirty);
        string? cleanHashBefore = clean.SavedContentHash;

        h.Write("clean.md", "LINE ONE\nLINE TWO\nLINE THREE, LONGER\n");
        h.Write("clamp.md", "only\ntwo lines\n");
        h.Write("reading.md", "# Reading\n\nnew body\n");
        h.Write("dirty.md", "written elsewhere\n");
        h.Delete("gone.md");
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        // One sentence, nothing else: 4 changed, 1 removed.
        A11yEvent.VaultRescanFinished finished = Assert.IsType<A11yEvent.VaultRescanFinished>(
            Assert.Single(h.Events));
        Assert.Equal((4UL, 1UL), (finished.Changed, finished.Removed));

        Assert.Equal("LINE ONE\nLINE TWO\nLINE THREE, LONGER\n", clean.Text);
        Assert.False(clean.IsDirty);
        Assert.NotEqual(cleanHashBefore, clean.SavedContentHash);
        Assert.Equal(SlateUniffiMethods.EditorTextContentHash(clean.Text), clean.SavedContentHash);
        Assert.False(clean.EditorDocument!.UndoStack.CanUndo, "undo could restore the pre-rescan bytes");
        Assert.Equal(OffsetOf(clean.Text, line: 3, column: 5), clean.EditorCaretOffset);

        Assert.Equal("only\ntwo lines\n", clamp.Text);
        Assert.Equal(OffsetOf(clamp.Text, line: 3, column: 1), clamp.EditorCaretOffset);

        Assert.True(reading.IsReadingMode);
        Assert.Contains("new body", ReadingText(reading));
        Assert.DoesNotContain("old body", ReadingText(reading));

        Assert.Equal("my unsaved edit\n", dirty.Text);
        Assert.True(dirty.IsDirty);
        Assert.True(dirty.IsExternallyStale);

        Assert.True(gone.IsMissingFromDisk);
        Assert.Equal("gone\n", gone.Text);
    });

    /// <summary>AR-8: an external delete of A plus an unrelated B with the
    /// same bytes retargets nothing — the dirty tab keeps its path and its
    /// buffer and is marked missing.</summary>
    [Fact]
    public void AUniqueSameHashDeleteCreateDoesNotRetargetADirtyTab() => RunSta(() =>
    {
        using var h = new Harness("same-hash", ("A.md", "identical\n"));
        WorkspaceTabViewModel tab = h.Open("A.md");
        tab.Text = "mine\n";

        h.Delete("A.md");
        h.Write("B.md", "identical\n");
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        Assert.Equal("A.md", tab.Path);
        Assert.Equal("mine\n", tab.Text);
        Assert.True(tab.IsDirty);
        Assert.True(tab.IsMissingFromDisk);
        Assert.DoesNotContain(h.AllTabs, other => other.Path == "B.md");
        Assert.Equal(["Files refreshed. 1 new or changed, 1 removed."], h.Spoken);
    });

    /// <summary>Round 25: the delta is Quick Open's ONLY rescan path. A
    /// multi-page reconciliation (one row a page) during which Slate writes
    /// create a path behind the cursor (<c>a-early.md</c>) and delete another
    /// (<c>keep.md</c>) ends with Quick Open listing exactly the files on disk:
    /// the Slate events reached it through the funnel, the delta's rows
    /// through the pages, and nothing reloads a list read before either. An
    /// open switcher ranks the new file. A page failure leaves Quick Open
    /// with only the applied pages' rows, and the retry completes it.</summary>
    [Fact]
    public void RescanUpdatesQuickOpenThroughTheDelta() => RunSta(() =>
    {
        using (var h = new Harness(
            "quick-open-delta",
            pageLimit: 1,
            files: [("alpha.md", "# Alpha\n"), ("doomed.md", "# Doomed\n"), ("keep.md", "# Keep\n")]))
        {
            h.Write("late.md", "# Late\n");
            h.Write("n2.md", "# Two\n");
            h.Delete("doomed.md");
            bool wrote = false;
            Exception? writeFailure = null;
            h.AfterRead(page =>
            {
                if (!wrote)
                {
                    wrote = true;
                    try
                    {
                        // On the read's pool thread: the create commits
                        // before the page's continuation is queued. The
                        // delete goes through the OS trash, whose COM
                        // apartment wants the UI (STA) thread — the thread
                        // the host's own delete runs on — so it is queued
                        // there, ahead of the page's effects.
                        h.CreateInCore("a-early.md", "# Early\n");
                        h.Context.Post(
                            _ =>
                            {
                                try
                                {
                                    h.DeleteInCore("keep.md");
                                }
                                catch (Exception exception)
                                {
                                    writeFailure = exception;
                                }
                            },
                            null);
                    }
                    catch (Exception exception)
                    {
                        writeFailure = exception;
                    }
                }
            });

            h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

            Assert.True(wrote);
            Assert.Null(writeFailure);
            Assert.Equal(
                h.OpenableFilesOnDisk(),
                h.QuickOpen.FilePathsForTests.Order(StringComparer.Ordinal).ToArray());
            Assert.Contains("a-early.md", h.QuickOpen.FilePathsForTests);
            Assert.DoesNotContain("keep.md", h.QuickOpen.FilePathsForTests);
            Assert.DoesNotContain("doomed.md", h.QuickOpen.FilePathsForTests);
            h.QuickOpen.Open();
            h.QuickOpen.Query = "late";
            h.Context.Await(h.QuickOpen.RankCompletion, "the Quick Open ranking");
            Assert.Contains(h.QuickOpen.Results, row => row.Path == "late.md");
            h.QuickOpen.Dismiss();
        }

        // Three creations a page at a time; page 2 fails: Quick Open holds
        // page 1's row only, and the retry brings it to the disk's list.
        using var paged = new Harness("quick-open-paged", pageLimit: 1, files: [("alpha.md", "# Alpha\n")]);
        paged.Write("n1.md", "1\n");
        paged.Write("n2.md", "2\n");
        paged.Write("n3.md", "3\n");
        paged.FailReads(read => read == 2);
        paged.Context.Await(paged.Lifecycle.RescanAsync(RescanReason.Explicit));

        Assert.Equal([OneError], paged.Spoken);
        Assert.Contains("n1.md", paged.QuickOpen.FilePathsForTests);
        Assert.DoesNotContain("n2.md", paged.QuickOpen.FilePathsForTests);

        paged.FailReads(_ => false);
        paged.Now += TimeSpan.FromMinutes(1);
        paged.Context.Await(paged.Lifecycle.RescanAsync(RescanReason.Explicit));
        Assert.Equal(
            paged.OpenableFilesOnDisk(),
            paged.QuickOpen.FilePathsForTests.Order(StringComparer.Ordinal).ToArray());
    });

    /// <summary>An import in flight refuses a rescan of either reason: no scan
    /// runs and nothing is spoken.</summary>
    [Fact]
    public void ARescanDuringAnImportIsRefused() => RunSta(() =>
    {
        var picker = new TaskCompletionSource<IReadOnlyList<string>>();
        using var h = new Harness("import-refused", pickImportSources: () => picker.Task, files: [("alpha.md", "# Alpha\n")]);
        int scans = h.ScanCalls;
        h.Sidebar.ImportCommand.Execute(null);
        Assert.True(h.Sidebar.IsImporting);
        h.Write("late.md", "# Late\n");

        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Foreground));

        Assert.Equal(scans, h.ScanCalls);
        Assert.Empty(h.Events);
        picker.SetResult([]);
        h.Context.RunUntil(() => !h.Sidebar.IsImporting, "the import settling");
        Assert.Equal(scans, h.ScanCalls);
    });

    /// <summary>A request joined to a running rescan is never dropped: when
    /// an import began meanwhile, the follow-up waits and runs once the
    /// import settles.</summary>
    [Fact]
    public void AFollowUpBlockedByAnImportRunsWhenTheImportSettles() => RunSta(() =>
    {
        var picker = new TaskCompletionSource<IReadOnlyList<string>>();
        using var h = new Harness("import-follow-up", pickImportSources: () => picker.Task, files: [("alpha.md", "# Alpha\n")]);
        int scans = h.ScanCalls;
        h.ParkAfterCall = scans + 1;
        Task run = h.Lifecycle.RescanAsync(RescanReason.Explicit);
        h.Context.RunUntil(() => h.Parked.IsSet, "the explicit scan parking");
        h.Write("late.md", "# Late\n");
        _ = h.Lifecycle.RescanAsync(RescanReason.Foreground);
        h.Sidebar.ImportCommand.Execute(null);
        Assert.True(h.Sidebar.IsImporting);
        h.Release.Set();
        h.Context.Await(run);
        Assert.Equal([NoChanges], h.Spoken);
        Assert.Equal(scans + 1, h.ScanCalls);

        picker.SetResult([]);
        h.Context.RunUntil(
            () => h.ScanCalls == scans + 2 && !h.Lifecycle.IsRescanActive,
            "the deferred follow-up");
        h.Context.Await(h.Lifecycle.RescanCompletion);
        Assert.Equal([NoChanges, Explicit1], h.Spoken);
    });

    // ---------------------------------------------------------------------
    // Paging, page failures and the retained ledger
    // ---------------------------------------------------------------------

    /// <summary>Five changes, two to a page; page 2 fails after page 1's
    /// effects landed. The run says only "results may be incomplete"; the
    /// retry resumes the generation from its stored cursor BEFORE a new
    /// scan, all five tabs reconcile, and only then is success spoken — for
    /// all five.</summary>
    [Fact]
    public void APageFailureResumesTheGenerationBeforeANewScan() => RunSta(() =>
    {
        using var h = new Harness(
            "page-failure",
            pageLimit: 2,
            files: [.. Enumerable.Range(1, 5).Select(n => ($"n{n}.md", $"old {n}\n"))]);
        WorkspaceTabViewModel[] tabs = [.. Enumerable.Range(1, 5).Select(n => h.Open($"n{n}.md"))];
        for (int n = 1; n <= 5; n++)
        {
            h.Write($"n{n}.md", $"new text {n}\n");
        }

        h.FailReads(read => read == 2);
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        Assert.Equal([OneError], h.Spoken);
        Assert.Equal(
            ["new text 1\n", "new text 2\n", "old 3\n", "old 4\n", "old 5\n"],
            tabs.Select(t => t.Text).ToArray());
        Assert.NotNull(h.Ledger.Pending);

        h.FailReads(_ => false);
        h.Now += TimeSpan.FromMinutes(1);
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        Assert.Equal([OneError, "Files refreshed. 5 new or changed, 0 removed."], h.Spoken);
        Assert.All(tabs, tab => Assert.False(tab.IsDirty));
        Assert.Equal(
            [.. Enumerable.Range(1, 5).Select(n => $"new text {n}\n")],
            tabs.Select(t => t.Text).ToArray());
        Assert.Equal(new ScanDeltaLedger(null, null), h.Ledger);
    });

    /// <summary>Each page is reported applied only AFTER its effects, and the
    /// last report — the Applied mark — only after the last page's.</summary>
    [Fact]
    public void TheAppliedMarkFollowsTheLastPagesEffects() => RunSta(() =>
    {
        using var h = new Harness("ack-order", pageLimit: 1, files: [("a.md", "a old\n"), ("b.md", "b old\n")]);
        WorkspaceTabViewModel a = h.Open("a.md");
        WorkspaceTabViewModel b = h.Open("b.md");
        // The hook runs on the ledger call's pool thread: it records the
        // tabs' saved hashes (plain fields), never their editor documents.
        var acknowledged = new List<(string? NextCursor, string? A, string? B)>();
        h.OnApplied((_, next) => acknowledged.Add((next, a.SavedContentHash, b.SavedContentHash)));
        h.Write("a.md", "a new text\n");
        h.Write("b.md", "b new text\n");

        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        Assert.Collection(
            acknowledged,
            first =>
            {
                Assert.NotNull(first.NextCursor);
                Assert.Equal(SlateUniffiMethods.EditorTextContentHash("a new text\n"), first.A);
            },
            last =>
            {
                Assert.Null(last.NextCursor);
                Assert.Equal(SlateUniffiMethods.EditorTextContentHash("b new text\n"), last.B);
            });
        Assert.Equal(["Files refreshed. 2 new or changed, 0 removed."], h.Spoken);
    });

    /// <summary>The generation is consumed removal-first across pages: a
    /// case-only rename's removal (page 1) marks the tab missing, page 2
    /// fails, and the retry's creation re-seats and reloads the tab — the
    /// Applied mark only afterwards. Silent throughout: no "missing from
    /// disk". (The created spelling sorts FIRST, so a path-ordered
    /// generation would create before it removes.)</summary>
    [Fact]
    public void AGenerationIsConsumedRemovalFirstAcrossPages() => RunSta(() =>
    {
        using var h = new Harness("removal-first", pageLimit: 1, files: [("ghost.md", "boo\n")]);
        if (!h.VolumeAliasesCase())
        {
            return;
        }

        WorkspaceTabViewModel tab = h.Open("ghost.md");
        File.Move(Path.Combine(h.Root, "ghost.md"), Path.Combine(h.Root, "Ghost.md"));
        h.FailReads(read => read == 2);
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        Assert.Equal([OneError], h.Spoken);
        Assert.True(tab.IsMissingFromDisk, "the removal on page 1 did not mark the tab missing");
        Assert.Equal("ghost.md", tab.Path);

        h.FailReads(_ => false);
        bool reseatedBeforeTheMark = false;
        h.OnApplied((_, next) =>
        {
            if (next is null)
            {
                reseatedBeforeTheMark = tab.Path == "Ghost.md" && !tab.IsMissingFromDisk;
            }
        });
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        Assert.True(reseatedBeforeTheMark, "the Applied mark preceded the re-seat");
        Assert.Equal("Ghost.md", tab.Path);
        Assert.False(tab.IsMissingFromDisk);
        Assert.Equal("boo\n", tab.Text);
        Assert.Equal([OneError, "Files refreshed. 1 new or changed, 1 removed."], h.Spoken);
    });

    /// <summary>A case-only rename whose removal and creation share ONE page:
    /// the page's removals mark the tab missing before its creations re-seat
    /// it, so the tab follows the new spelling with its content — silently,
    /// the one sentence counting a removal and a creation (AR-8: no rename
    /// correlation).</summary>
    [Fact]
    public void ACaseOnlyRenameInOnePageReseatsItsTab() => RunSta(() =>
    {
        using var h = new Harness("case-one-page", ("ghost.md", "boo\n"));
        if (!h.VolumeAliasesCase())
        {
            return;
        }

        WorkspaceTabViewModel tab = h.Open("ghost.md");
        File.Move(Path.Combine(h.Root, "ghost.md"), Path.Combine(h.Root, "Ghost.md"));
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        Assert.Equal("Ghost.md", tab.Path);
        Assert.False(tab.IsMissingFromDisk);
        Assert.Equal("boo\n", tab.Text);
        Assert.Equal(["Files refreshed. 1 new or changed, 1 removed."], h.Spoken);
    });

    /// <summary>Coordinator witness (a): A fails on page 2 and is recovered
    /// on retry 1, whose new generation B fails mid-page; retry 2 recovers B
    /// and its scan C succeeds. The final host state is every change's, the
    /// one success sentence covers A+B+C net per path since the last
    /// release, and nothing stays retained.</summary>
    [Fact]
    public void AConsecutivePageFailureIsRecoveredAndReleasedOnce() => RunSta(() =>
    {
        using var h = new Harness(
            "abc",
            pageLimit: 1,
            files: [("a.md", "h0\n"), ("b.md", "bee\n"), ("keep.md", "keep\n")]);
        WorkspaceTabViewModel a = h.Open("a.md");

        // A: a.md h0 → h1 (page 1), n1.md created (page 2 — fails).
        h.Write("a.md", "h1 text\n");
        h.Write("n1.md", "one\n");
        h.FailReads(read => read is 2 or 5);
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
        Assert.Equal("h1 text\n", a.Text);

        // Retry 1 recovers A; B (b.md removed, a.md h1 → h2) fails mid-page.
        h.Delete("b.md");
        h.Write("a.md", "h2 text, longer\n");
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
        ScanDeltaLedger afterRetry1 = h.Ledger;
        Assert.NotNull(afterRetry1.Pending);
        Assert.NotNull(afterRetry1.Applied);

        // Retry 2 recovers B; C (n2.md created) succeeds.
        h.Write("n2.md", "two\n");
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        Assert.Equal([OneError, OneError, "Files refreshed. 3 new or changed, 1 removed."], h.Spoken);
        Assert.Equal("h2 text, longer\n", a.Text);
        Assert.False(a.IsDirty);
        Assert.Equal(SlateUniffiMethods.EditorTextContentHash(a.Text), a.SavedContentHash);
        Assert.False(a.EditorDocument!.UndoStack.CanUndo);
        string[] tree = [.. h.Sidebar.RootNodes.Select(node => node.Path)];
        Assert.Contains("n1.md", tree);
        Assert.Contains("n2.md", tree);
        Assert.DoesNotContain("b.md", tree);
        Assert.Contains("n1.md", h.QuickOpen.FilePathsForTests);
        Assert.Contains("n2.md", h.QuickOpen.FilePathsForTests);
        Assert.DoesNotContain("b.md", h.QuickOpen.FilePathsForTests);
        Assert.Equal(new ScanDeltaLedger(null, null), h.Ledger);
    });

    /// <summary>The ledger bound through the host's own view of it: after
    /// EVERY ledger call of the consecutive-failure sequence (A fails on
    /// page 2; retry 1 recovers A and B fails mid-page; retry 2 recovers B
    /// and C succeeds) the ledger read succeeds — core refuses to read a
    /// state holding a second generation — and each run ends holding exactly
    /// the generations the protocol says: A Pending; then B Pending beside
    /// Applied A; then nothing.</summary>
    [Fact]
    public void TheLedgerNeverHoldsMoreThanOnePendingAndOneAppliedGeneration() => RunSta(() =>
    {
        using var h = new Harness("ledger-bound", pageLimit: 1, files: [("a.md", "h0\n"), ("b.md", "bee\n")]);
        var refusals = new List<string>();
        int reads = 0;
        h.AfterEveryLedgerCall(() =>
        {
            reads++;
            try
            {
                _ = h.Ledger;
            }
            catch (VaultException refusal)
            {
                refusals.Add(refusal.Message);
            }
        });

        // A: a.md h0 → h1 (page 1), n1.md created (page 2 — fails).
        h.Write("a.md", "h1 text\n");
        h.Write("n1.md", "one\n");
        h.FailReads(read => read is 2 or 5);
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
        ScanDeltaLedger afterA = h.Ledger;
        Assert.NotNull(afterA.Pending);
        Assert.NotNull(afterA.Pending!.Cursor);
        Assert.Null(afterA.Applied);

        // Retry 1: A is resumed and becomes the one Applied generation; B
        // (b.md removed, a.md h1 → h2) fails mid-page.
        h.Delete("b.md");
        h.Write("a.md", "h2 text, longer\n");
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
        ScanDeltaLedger afterB = h.Ledger;
        Assert.NotNull(afterB.Pending);
        Assert.NotEqual(afterA.Pending.Generation, afterB.Pending!.Generation);
        Assert.Equal(afterA.Pending.Generation, afterB.Applied?.Generation);
        Assert.Equal(2UL, afterB.Applied!.Rows);

        // Retry 2: B coalesces into A; C (n2.md created) coalesces too and
        // the one Applied generation is released.
        h.Write("n2.md", "two\n");
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        Assert.True(reads > 6, $"only {reads} ledger calls were observed");
        Assert.Empty(refusals);
        Assert.Equal(new ScanDeltaLedger(null, null), h.Ledger);
        Assert.Equal([OneError, OneError, "Files refreshed. 3 new or changed, 1 removed."], h.Spoken);
    });

    /// <summary>Round 25 (locked decision 05 §4.1): no ledger call runs on the
    /// UI thread. Across a page failure and its retry — the resume, a new
    /// generation, every page read, every applied mark and the release —
    /// every call the lifecycle makes into core's ledger lands on a pool
    /// thread, never the fact's STA (dispatcher) thread.</summary>
    [Fact]
    public void TheLedgerIsNeverCalledOnTheUiThread() => RunSta(() =>
    {
        using var h = new Harness(
            "ledger-threads", pageLimit: 1, files: [("a.md", "a0\n"), ("b.md", "b0\n")]);
        h.Write("a.md", "a1 text\n");
        h.Write("b.md", "b1 text\n");
        h.FailReads(read => read == 2);
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
        h.FailReads(_ => false);
        h.Now += TimeSpan.FromMinutes(1);
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        (string Call, int Thread, bool Pool)[] calls = [.. h.LedgerCalls];
        Assert.Equal(
            ["PageApplied", "Pending", "ReadPage", "Release"],
            calls.Select(call => call.Call).Distinct().Order(StringComparer.Ordinal).ToArray());
        Assert.All(calls, call =>
        {
            Assert.NotEqual(h.UiThread, call.Thread);
            Assert.True(call.Pool, $"{call.Call} ran on a non-pool thread");
        });
    });

    /// <summary>The conflicting pairs, each through the real page-failure →
    /// retry path: a change applied by a failed run, changed again before the
    /// retry. Effects are never netted — the final host state is the latest
    /// disk truth — and only the one success sentence reduces per path.</summary>
    [Fact]
    public void ConflictingPairsAcrossARetrySpeakTheNetAndLeaveTheLatestState() => RunSta(() =>
    {
        // h0 → h1 (applied on page 1) → h0: the tab ends at h0; only the
        // other page's creation is spoken.
        using (var h = new Harness("pair-h0h1h0", pageLimit: 1, files: [("a.md", "h0\n")]))
        {
            WorkspaceTabViewModel a = h.Open("a.md");
            h.Write("a.md", "h1 bytes\n");
            h.Write("z.md", "zed\n");
            h.FailReads(read => read == 2);
            h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
            Assert.Equal("h1 bytes\n", a.Text);

            h.Write("a.md", "h0\n");
            h.FailReads(_ => false);
            h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
            Assert.Equal([OneError, Explicit1], h.Spoken);
            Assert.Equal("h0\n", a.Text);
            Assert.Equal(SlateUniffiMethods.EditorTextContentHash("h0\n"), a.SavedContentHash);
            Assert.False(a.IsDirty);
            Assert.False(a.EditorDocument!.UndoStack.CanUndo);
        }

        // h0 → h1 → h0 with nothing else: the Applied mark fails, the retry
        // re-applies, and the retry can honestly say "No changes" while the
        // tab sits at h0.
        using (var h = new Harness("pair-h0h1h0-alone", pageLimit: 1, files: [("a.md", "h0\n")]))
        {
            WorkspaceTabViewModel a = h.Open("a.md");
            h.Write("a.md", "h1 bytes\n");
            h.FailAppliedMarks(true);
            h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
            Assert.Equal("h1 bytes\n", a.Text);

            h.Write("a.md", "h0\n");
            h.FailAppliedMarks(false);
            h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
            Assert.Equal([OneError, NoChanges], h.Spoken);
            Assert.Equal("h0\n", a.Text);
            Assert.False(a.EditorDocument!.UndoStack.CanUndo);
        }

        // create → remove: nothing spoken for it; the tab opened on it is
        // missing, the tree and Quick Open no longer have it.
        using (var h = new Harness("pair-create-remove", pageLimit: 1, files: [("seed.md", "seed\n")]))
        {
            h.Write("a-new.md", "new\n");
            h.Write("b-other.md", "other\n");
            h.FailReads(read => read == 2);
            h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
            WorkspaceTabViewModel opened = h.Open("a-new.md");

            h.Delete("a-new.md");
            h.FailReads(_ => false);
            h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
            Assert.Equal([OneError, Explicit1], h.Spoken);
            Assert.True(opened.IsMissingFromDisk);
            Assert.DoesNotContain(h.Sidebar.RootNodes, node => node.Path == "a-new.md");
            Assert.DoesNotContain("a-new.md", h.QuickOpen.FilePathsForTests);
            Assert.Contains("b-other.md", h.QuickOpen.FilePathsForTests);
        }

        // remove → create with the same bytes: nothing spoken for it; the
        // missing tab is re-seated with its content.
        using (var h = new Harness("pair-remove-create-same", pageLimit: 1, files: [("r.md", "same\n")]))
        {
            WorkspaceTabViewModel r = h.Open("r.md");
            h.Delete("r.md");
            h.Write("x.md", "ex\n");
            h.FailReads(read => read == 2);
            h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
            Assert.True(r.IsMissingFromDisk);

            h.Write("r.md", "same\n");
            h.FailReads(_ => false);
            h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
            Assert.Equal([OneError, Explicit1], h.Spoken);
            Assert.False(r.IsMissingFromDisk);
            Assert.Equal("same\n", r.Text);
        }

        // remove → create with new bytes: modified.
        using (var h = new Harness("pair-remove-create-new", pageLimit: 1, files: [("r.md", "old bytes\n")]))
        {
            WorkspaceTabViewModel r = h.Open("r.md");
            h.Delete("r.md");
            h.Write("x.md", "ex\n");
            h.FailReads(read => read == 2);
            h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

            h.Write("r.md", "brand new bytes\n");
            h.FailReads(_ => false);
            h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
            Assert.Equal([OneError, "Files refreshed. 2 new or changed, 0 removed."], h.Spoken);
            Assert.False(r.IsMissingFromDisk);
            Assert.Equal("brand new bytes\n", r.Text);
            Assert.False(r.IsDirty);
        }

        // modify → remove: removed; the tab keeps the last text it showed
        // and is missing.
        using (var h = new Harness("pair-modify-remove", pageLimit: 1, files: [("m.md", "m0\n")]))
        {
            WorkspaceTabViewModel m = h.Open("m.md");
            h.Write("m.md", "m1 bytes\n");
            h.Write("y.md", "why\n");
            h.FailReads(read => read == 2);
            h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
            Assert.Equal("m1 bytes\n", m.Text);

            h.Delete("m.md");
            h.FailReads(_ => false);
            h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
            Assert.Equal([OneError, "Files refreshed. 1 new or changed, 1 removed."], h.Spoken);
            Assert.True(m.IsMissingFromDisk);
            Assert.Equal("m1 bytes\n", m.Text);
        }
    });

    // ---------------------------------------------------------------------
    // Slate-owned writes supersede (round 23) and the re-check (round 24)
    // ---------------------------------------------------------------------

    /// <summary>Round 23 (a): page 1 applied x.md's removal (its tab went
    /// missing) and page 2 failed; the user recreates x.md through Slate —
    /// its own Created event re-seats the tab — and the retry recovers page
    /// 2. The recreate superseded x.md's removal row (still in its page,
    /// flagged), so the one sentence counts only z.md, and the final state is
    /// the disk's: x.md's tab live with the recreated text, the tree and
    /// Quick Open listing it, nothing retained.</summary>
    [Fact]
    public void ASlateRecreateAfterAnAppliedRemovalLeavesTheTabLiveAndUnspoken() => RunSta(() =>
    {
        using var h = new Harness(
            "supersede-recreate", pageLimit: 1, files: [("x.md", "x0\n"), ("keep.md", "keep\n")]);
        WorkspaceTabViewModel x = h.Open("x.md");
        h.Delete("x.md");
        h.Write("z.md", "zed\n");
        h.FailReads(read => read == 2);
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
        Assert.Equal([OneError], h.Spoken);
        Assert.True(x.IsMissingFromDisk);

        h.CreateThroughSlate("x.md", "x back\n");
        Assert.False(x.IsMissingFromDisk);
        Assert.Equal("x back\n", x.Text);
        ScanDeltaEntry removal = Assert.Single(h.PendingPage(cursor: null, limit: 1).Entries);
        Assert.Equal(
            (ScanDeltaKind.Removed, "x.md", true),
            (removal.Kind, removal.Path, removal.Superseded));

        h.FailReads(_ => false);
        h.Now += TimeSpan.FromMinutes(1);
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        Assert.Equal([OneError, Explicit1], h.Spoken);
        Assert.False(x.IsMissingFromDisk);
        Assert.Equal("x back\n", x.Text);
        Assert.False(x.IsDirty);
        Assert.Equal("x back\n", File.ReadAllText(Path.Combine(h.Root, "x.md")));
        Assert.Contains(h.Sidebar.RootNodes, node => node.Path == "x.md");
        Assert.Contains("x.md", h.QuickOpen.FilePathsForTests);
        Assert.Contains("z.md", h.QuickOpen.FilePathsForTests);
        Assert.Equal(new ScanDeltaLedger(null, null), h.Ledger);
    });

    /// <summary>Round 23 (b): y.md's external modification sits on the page
    /// that failed; the user opens y.md, edits it and saves it through Slate
    /// before the retry. The save superseded the unapplied row: the retry
    /// applies nothing for it — no reload, so the user's undo history
    /// survives — and never speaks it.</summary>
    [Fact]
    public void ASlateSaveBeforeTheRetrySupersedesAnUnappliedModification() => RunSta(() =>
    {
        using var h = new Harness(
            "supersede-save", pageLimit: 1, files: [("a.md", "a0\n"), ("y.md", "y0\n")]);
        h.Write("a.md", "a1 text\n");
        h.Write("y.md", "y1 external\n");
        h.FailReads(read => read == 2);
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
        Assert.Equal([OneError], h.Spoken);

        WorkspaceTabViewModel y = h.Open("y.md");
        Assert.Equal("y1 external\n", y.Text);
        y.Text = "y1 external\nmy line\n";
        Assert.True(y.Save());
        h.Context.Drain();
        Assert.True(y.EditorDocument!.UndoStack.CanUndo);
        ScanDeltaEntry modification = Assert.Single(h.PendingPage(h.Ledger.Pending!.Cursor, limit: 1).Entries);
        Assert.Equal(
            (ScanDeltaKind.Modified, "y.md", true),
            (modification.Kind, modification.Path, modification.Superseded));

        h.FailReads(_ => false);
        h.Now += TimeSpan.FromMinutes(1);
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        Assert.Equal([OneError, Explicit1], h.Spoken);
        Assert.Equal("y1 external\nmy line\n", y.Text);
        Assert.False(y.IsDirty);
        Assert.True(y.EditorDocument!.UndoStack.CanUndo, "the retry reloaded the tab Slate had just saved");
        Assert.Equal(new ScanDeltaLedger(null, null), h.Ledger);
    });

    /// <summary>Round 24 (a): the page holding x.md's removal is FETCHED; before
    /// the host applies it, x.md is recreated through Slate and its Created
    /// event is handled. The re-check in the applying turn sees the row
    /// superseded, so the cached removal is never applied: the tab stays
    /// live (a Created event re-seats only a MISSING tab, so the funnel
    /// leaves this one's buffer as it was), Quick Open keeps x.md (the
    /// replacement read before the recreate is re-based on its event), and
    /// x.md is never spoken.</summary>
    [Fact]
    public void APageRecheckedAfterASlateRecreateAppliesNoStaleRemoval() => RunSta(() =>
    {
        using var h = new Harness("recheck-recreate", ("x.md", "x0\n"), ("keep.md", "keep\n"));
        WorkspaceTabViewModel x = h.Open("x.md");
        h.Delete("x.md");
        h.Write("z.md", "zed\n");
        bool recreated = false;
        h.AfterRead(page =>
        {
            if (!recreated && page.Entries.Any(entry => entry.Path == "x.md" && !entry.Superseded))
            {
                // After the fetch, on the read's thread: the Created event is
                // posted ahead of the page's continuation, so the host
                // handles it before it applies the cached page.
                recreated = true;
                h.CreateInCore("x.md", "x back\n");
            }
        });

        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        Assert.True(recreated, "the page never held x.md's removal");
        Assert.False(x.IsMissingFromDisk, "the cached removal was applied over the handled recreate");
        Assert.Equal([Explicit1], h.Spoken);
        Assert.Contains("x.md", h.QuickOpen.FilePathsForTests);
    });

    /// <summary>Round 24 (b): the page is fetched, re-checked AND applied —
    /// x.md's tab goes missing — before the recreate commits; the recreate's
    /// own Created event, handled after the page's effects, re-seats the tab
    /// over them. The same final state as (a), and x.md is still never
    /// spoken: the write superseded its applied row before the
    /// release.</summary>
    [Fact]
    public void ASlateRecreateAfterThePageAppliedReconcilesOverIt() => RunSta(() =>
    {
        using var h = new Harness(
            "recreate-after-apply", pageLimit: 1, files: [("x.md", "x0\n"), ("keep.md", "keep\n")]);
        WorkspaceTabViewModel x = h.Open("x.md");
        h.Delete("x.md");
        h.Write("z.md", "zed\n");
        bool recreated = false;
        bool missingBeforeTheRecreate = false;
        h.OnApplied((_, next) =>
        {
            if (!recreated && next is not null)
            {
                // Page 1's effects are applied; its mark is being reported
                // off the UI thread. The recreate commits now and its event
                // is handled AFTER the page's effects.
                recreated = true;
                missingBeforeTheRecreate = x.IsMissingFromDisk;
                h.CreateInCore("x.md", "x back\n");
            }
        });

        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        Assert.True(recreated);
        Assert.True(missingBeforeTheRecreate, "page 1's removal was not applied first");
        Assert.False(x.IsMissingFromDisk);
        Assert.Equal("x back\n", x.Text);
        Assert.Equal([Explicit1], h.Spoken);
        Assert.Contains("x.md", h.QuickOpen.FilePathsForTests);
        Assert.Equal(new ScanDeltaLedger(null, null), h.Ledger);
    });

    /// <summary>Round 24 (c): a task toggle on y.md is IN FLIGHT — held before
    /// its core write — when the rescan applies y.md's external modification.
    /// The clean-tab reload waits for the save: no reload, the undo history
    /// intact, only the staleness derived; the toggle's own completion then
    /// reconciles the tab (here a conflict, since y.md changed on disk under
    /// it).</summary>
    [Fact]
    public void ACleanReloadWaitsForAnInFlightSlateSave() => RunSta(() =>
    {
        using var h = new Harness("inflight-save", ("y.md", "- [ ] task\n"));
        WorkspaceTabViewModel y = h.Open("y.md");
        y.Text = "- [ ] task\nmine\n";
        Assert.True(y.Save());
        h.Context.Drain();
        Assert.True(y.EditorDocument!.UndoStack.CanUndo);

        h.Write("y.md", "- [ ] task\nmine\nexternal\n");
        using var hold = new ManualResetEventSlim(false);
        using var holding = new ManualResetEventSlim(false);
        y.TaskToggleBeforeWriteForTests = () =>
        {
            holding.Set();
            _ = hold.Wait(TimeSpan.FromSeconds(30));
        };
        TaskItem task = h.Lifecycle.SessionForTests!.TasksForFile("y.md")[0];
        var toggleEvents = new List<A11yEvent>();
        Assert.Equal(TabTaskToggle.Started, y.ToggleTask(task, toggleEvents.Add));
        Assert.True(holding.Wait(TimeSpan.FromSeconds(10)), "the toggle never reached its write");
        Assert.True(y.IsTaskToggleInFlight);

        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        Assert.Equal(["Files refreshed. 1 new or changed, 0 removed."], h.Spoken);
        Assert.Equal("- [ ] task\nmine\n", y.Text);
        Assert.True(y.EditorDocument!.UndoStack.CanUndo, "the rescan reloaded a tab whose save was in flight");
        Assert.True(y.IsExternallyStale);

        hold.Set();
        Assert.True(
            PumpedDispatcher.PumpUntil(() => !y.IsTaskToggleInFlight),
            "the toggle never completed");
        _ = Assert.Single(toggleEvents.OfType<A11yEvent.TaskToggleConflict>());
        Assert.True(y.EditorDocument!.UndoStack.CanUndo);
    });

    // ---------------------------------------------------------------------
    // Cancellation (round 23)
    // ---------------------------------------------------------------------

    /// <summary>A close arriving before the FIRST page (the run's token
    /// cancelled at the first read) stops the reconciliation there: no host
    /// effect, nothing spoken, the generation Pending at cursor 0.</summary>
    [Fact]
    public void ACancelBeforeTheFirstPageAppliesNothingAndSaysNothing() => RunSta(() =>
    {
        using var h = new Harness(
            "cancel-first", pageLimit: 1, files: [("a.md", "a0\n"), ("gone.md", "g\n")]);
        WorkspaceTabViewModel a = h.Open("a.md");
        WorkspaceTabViewModel gone = h.Open("gone.md");
        h.Write("a.md", "a1 text\n");
        h.Delete("gone.md");
        h.CancelReads(read => read == 1);

        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        Assert.Empty(h.Events);
        Assert.Equal("a0\n", a.Text);
        Assert.False(gone.IsMissingFromDisk);
        ScanDeltaPending pending = Assert.IsType<ScanDeltaPending>(h.Ledger.Pending);
        Assert.Null(pending.Cursor);
        Assert.Equal(2UL, pending.Rows);
    });

    /// <summary>A close arriving after page 1 keeps page 1's effects, says
    /// nothing and leaves the generation Pending at page 2; a later rescan
    /// of the same session resumes exactly there (page 1 is never re-read)
    /// and speaks the whole outcome once. A vault reopened after a cancelled
    /// run starts with nothing retained: the closed session's generation died
    /// with it.</summary>
    [Fact]
    public void ACancelAfterPageOneKeepsItsEffectsAndTheNextRescanResumes() => RunSta(() =>
    {
        using var h = new Harness(
            "cancel-second", pageLimit: 1, files: [("a.md", "a0\n"), ("b.md", "b0\n")]);
        WorkspaceTabViewModel a = h.Open("a.md");
        WorkspaceTabViewModel b = h.Open("b.md");
        h.Write("a.md", "a1 text\n");
        h.Write("b.md", "b1 text\n");
        h.CancelReads(read => read == 2);

        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        Assert.Empty(h.Events);
        Assert.Equal("a1 text\n", a.Text);
        Assert.Equal("b0\n", b.Text);
        string? atPageTwo = Assert.IsType<ScanDeltaPending>(h.Ledger.Pending).Cursor;
        Assert.NotNull(atPageTwo);

        var read = new List<string>();
        h.AfterRead(page => read.AddRange(page.Entries.Select(entry => entry.Path)));
        h.CancelReads(_ => false);
        h.Now += TimeSpan.FromMinutes(1);
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        Assert.Equal(["b.md"], read);
        Assert.Equal(["Files refreshed. 2 new or changed, 0 removed."], h.Spoken);
        Assert.Equal("b1 text\n", b.Text);
        Assert.Equal(new ScanDeltaLedger(null, null), h.Ledger);

        // Cancel mid-generation again, then reopen the vault.
        h.AfterRead(null);
        h.Write("a.md", "a2 text, longer\n");
        h.Write("b.md", "b2 text, longer\n");
        int readsSoFar = h.Reads;
        h.CancelReads(ordinal => ordinal == readsSoFar + 2);
        h.Now += TimeSpan.FromMinutes(1);
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
        Assert.NotNull(h.Ledger.Pending);

        h.Events.Clear();
        h.Context.Await(h.Lifecycle.OpenVaultAsync(h.Root), "the reopen");
        Assert.Equal(new ScanDeltaLedger(null, null), h.Ledger);
        Assert.DoesNotContain(
            h.Events,
            e => e is A11yEvent.VaultRescanFinished or A11yEvent.VaultRescanIncomplete);
    });

    // ---------------------------------------------------------------------
    // The foreground route
    // ---------------------------------------------------------------------

    /// <summary>An activation during an explicit rescan — with a change that
    /// rescan's snapshot missed — is forwarded, joins the lattice, and its
    /// one follow-up reconciles the change.</summary>
    [Fact]
    public void AnActivationDuringAnExplicitRescanJoinsItsLattice() => RunSta(() =>
    {
        using var h = new Harness("route-join", ("alpha.md", "# Alpha\n"));
        var route = new ForegroundRescanRoute(h.Lifecycle.RescanAsync, () => false, () => h.Now);
        Assert.Null(route.OnActivated());
        route.OnDeactivated();
        h.Now += TimeSpan.FromSeconds(10);
        int scans = h.ScanCalls;
        h.ParkAfterCall = scans + 1;

        Task run = h.Lifecycle.RescanAsync(RescanReason.Explicit);
        h.Context.RunUntil(() => h.Parked.IsSet, "the explicit scan parking");
        h.Write("late.md", "# Late\n");
        Assert.NotNull(route.OnActivated());
        h.Release.Set();
        h.Context.Await(run);

        Assert.Equal([NoChanges, Explicit1], h.Spoken);
        Assert.Equal(RescanReason.Foreground, Assert.IsType<A11yEvent.VaultRescanFinished>(h.Events[1]).Reason);
        Assert.Equal(scans + 2, h.ScanCalls);
        Assert.Contains(h.Sidebar.RootNodes, node => node.Path == "late.md");
    });

    /// <summary>The same, with the rescan before the explicit one having ended
    /// under five seconds earlier: the cooldown applies only to STARTING a
    /// run, never to recording a pending one.</summary>
    [Fact]
    public void AnActivationJoinsTheLatticeEvenInsideTheCooldown() => RunSta(() =>
    {
        using var h = new Harness("route-cooldown", ("alpha.md", "# Alpha\n"));
        var route = new ForegroundRescanRoute(h.Lifecycle.RescanAsync, () => false, () => h.Now);
        Assert.Null(route.OnActivated());
        route.OnDeactivated();

        h.Now += TimeSpan.FromSeconds(3);
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
        h.Events.Clear();
        h.Now += TimeSpan.FromSeconds(1);
        int scans = h.ScanCalls;
        h.ParkAfterCall = scans + 1;
        Task run = h.Lifecycle.RescanAsync(RescanReason.Explicit);
        h.Context.RunUntil(() => h.Parked.IsSet, "the explicit scan parking");
        h.Write("late.md", "# Late\n");
        h.Now += TimeSpan.FromSeconds(1);
        Assert.NotNull(route.OnActivated());
        h.Release.Set();
        h.Context.Await(run);

        Assert.Equal([NoChanges, Explicit1], h.Spoken);
        Assert.Equal(scans + 2, h.ScanCalls);
    });

    /// <summary>The route's own rules: never on the first activation, never
    /// after under two seconds away, never under a modal surface.</summary>
    [Fact]
    public void TheForegroundRouteForwardsOnlyAReturnAfterBeingAway()
    {
        DateTimeOffset now = DateTimeOffset.UnixEpoch;
        bool modal = false;
        var forwarded = new List<RescanReason>();
        var route = new ForegroundRescanRoute(
            reason =>
            {
                forwarded.Add(reason);
                return Task.CompletedTask;
            },
            () => modal,
            () => now);

        Assert.Null(route.OnActivated());
        route.OnDeactivated();
        now += ForegroundRescanRoute.MinimumAway - TimeSpan.FromMilliseconds(1);
        Assert.Null(route.OnActivated());
        route.OnDeactivated();
        now += ForegroundRescanRoute.MinimumAway;
        modal = true;
        Assert.Null(route.OnActivated());
        modal = false;
        route.OnDeactivated();
        now += ForegroundRescanRoute.MinimumAway;
        Assert.NotNull(route.OnActivated());
        Assert.Null(route.OnActivated());

        Assert.Equal([RescanReason.Foreground], forwarded);
    }

    // ---------------------------------------------------------------------
    // The harness
    // ---------------------------------------------------------------------

    private static void RunSta(Action body)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                body();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(120)), "STA test body timed out.");
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private static int OffsetOf(string text, int line, int column)
    {
        var document = new ICSharpCode.AvalonEdit.Document.TextDocument(text);
        int clampedLine = Math.Clamp(line, 1, document.LineCount);
        ICSharpCode.AvalonEdit.Document.DocumentLine target = document.GetLineByNumber(clampedLine);
        return target.Offset + Math.Clamp(column, 1, target.Length + 1) - 1;
    }

    private static string ReadingText(WorkspaceTabViewModel tab)
    {
        FlowDocument document = Assert.IsType<FlowDocument>(tab.Reading?.Document);
        return new TextRange(document.ContentStart, document.ContentEnd).Text;
    }

    /// <summary>One thread, one queue: every continuation the lifecycle
    /// awaits comes back here and runs when the fact drains it, so the whole
    /// reconciliation runs on the thread that owns the editor documents.</summary>
    internal sealed class SerialContext : SynchronizationContext
    {
        private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _queue = new();

        public override void Post(SendOrPostCallback d, object? state) => _queue.Enqueue((d, state));

        public override void Send(SendOrPostCallback d, object? state) => d(state);

        public override SynchronizationContext CreateCopy() => this;

        public void Drain()
        {
            while (_queue.TryDequeue(out (SendOrPostCallback Callback, object? State) work))
            {
                work.Callback(work.State);
            }
        }

        public void RunUntil(Func<bool> condition, string what)
        {
            var clock = Stopwatch.StartNew();
            while (!condition())
            {
                Drain();
                if (clock.Elapsed > TimeSpan.FromSeconds(30))
                {
                    throw new TimeoutException($"{what} never happened");
                }

                Thread.Sleep(2);
            }

            Drain();
        }

        public void Await(Task task, string what = "the rescan")
        {
            RunUntil(() => task.IsCompleted, what);
            task.GetAwaiter().GetResult();
        }
    }

    /// <summary>A pass-through over the session's own ledger calls that can
    /// fail a page read (by its ordinal across every run of the vault) or
    /// the Applied mark, and observe each report.</summary>
    internal sealed class ScriptedChannel(IScanDeltaChannel inner, Func<int> nextRead) : IScanDeltaChannel
    {
        public Func<int, bool> FailRead { get; set; } = _ => false;

        /// <summary>Cancel the run's token at this read ordinal, BEFORE the
        /// read: a close or vault switch arriving between pages.</summary>
        public Func<int, bool> CancelAtRead { get; set; } = _ => false;

        /// <summary>Runs on the ledger call's own (pool) thread after a page
        /// read returns and before the host applies it: a Slate-owned write
        /// made here posts its event AHEAD of the page's continuation, so
        /// the host handles it between the fetch and the effects.</summary>
        public Action<ScanDeltaPage>? AfterRead { get; set; }

        /// <summary>Runs at the start of every call, on its thread.</summary>
        public Action<string>? OnCall { get; set; }

        public bool FailAppliedMark { get; set; }

        public Action<ulong, string?>? OnApplied { get; set; }

        /// <summary>Runs after every call that reached the session.</summary>
        public Action? AfterCall { get; set; }

        public ScanDeltaPending? Pending()
        {
            OnCall?.Invoke(nameof(Pending));
            ScanDeltaPending? pending = inner.Pending();
            AfterCall?.Invoke();
            return pending;
        }

        public ScanDeltaPage ReadPage(ulong generation, string? cursor, uint limit, CancelToken cancel)
        {
            OnCall?.Invoke(nameof(ReadPage));
            int read = nextRead();
            if (CancelAtRead(read))
            {
                cancel.Cancel();
            }

            if (FailRead(read))
            {
                throw new IOException($"injected failure of page read {read}");
            }

            ScanDeltaPage page = inner.ReadPage(generation, cursor, limit, cancel);
            AfterCall?.Invoke();
            AfterRead?.Invoke(page);
            return page;
        }

        public void PageApplied(ulong generation, string? nextCursor)
        {
            OnCall?.Invoke(nameof(PageApplied));
            OnApplied?.Invoke(generation, nextCursor);
            if (FailAppliedMark && nextCursor is null)
            {
                throw new IOException("injected failure of the Applied mark");
            }

            inner.PageApplied(generation, nextCursor);
            AfterCall?.Invoke();
        }

        public ScanDeltaOutcome Release()
        {
            OnCall?.Invoke(nameof(Release));
            ScanDeltaOutcome outcome = inner.Release();
            AfterCall?.Invoke();
            return outcome;
        }
    }

    /// <summary>A vault on disk, a lifecycle over it with every seam pointed
    /// at this fact, and the open already drained.</summary>
    internal sealed class Harness : IDisposable
    {
        private readonly SynchronizationContext? _previous;
        private readonly Func<int, bool>[] _failRead = [_ => false];
        private readonly Func<int, bool>[] _cancelRead = [_ => false];
        private Action<ScanDeltaPage>? _afterRead;
        private int _scanCalls;
        private int _reads;
        private bool _failAppliedMark;
        private Action<ulong, string?>? _onApplied;
        private Action? _afterLedgerCall;

        public Harness(string label, params (string Path, string Text)[] files)
            : this(NewRoot(label), files, VaultLifecycleViewModel.DefaultScanDeltaPageLimit, null)
        {
        }

        public Harness(
            string label,
            uint pageLimit = VaultLifecycleViewModel.DefaultScanDeltaPageLimit,
            Func<Task<IReadOnlyList<string>>>? pickImportSources = null,
            (string Path, string Text)[]? files = null)
            : this(NewRoot(label), files ?? [], pageLimit, pickImportSources)
        {
        }

        private Harness(
            string root,
            (string Path, string Text)[] files,
            uint pageLimit,
            Func<Task<IReadOnlyList<string>>>? pickImportSources)
        {
            Root = root;
            Seed(root, files);
            _previous = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(Context);
            Lifecycle = new VaultLifecycleViewModel(
                pickVault: () => Task.FromResult<string?>(Root),
                enqueueUi: action => Context.Post(_ => action(), null),
                recentVaultsStore: new RecentVaultsStore(Path.Combine(Root + "-device", "recent-vaults.json")),
                announce: Events.Add,
                pickImportSources: pickImportSources,
                scanClock: () => Now,
                sessionLoadWorker: RunOnWorker<(ScanReport Report, SwitcherFile[] SwitcherFiles)>,
                rescanWorker: RunOnWorker<RescanReport>,
                scanDeltaChannel: session => new ScriptedChannel(
                    new SessionScanDeltaChannel(session),
                    () => Interlocked.Increment(ref _reads))
                {
                    FailRead = read => _failRead[0](read),
                    CancelAtRead = read => _cancelRead[0](read),
                    AfterRead = page => _afterRead?.Invoke(page),
                    OnCall = call => LedgerCalls.Enqueue(
                        (call, Environment.CurrentManagedThreadId, Thread.CurrentThread.IsThreadPoolThread)),
                    FailAppliedMark = _failAppliedMark,
                    OnApplied = (generation, next) => _onApplied?.Invoke(generation, next),
                    AfterCall = () => _afterLedgerCall?.Invoke(),
                },
                scanDeltaPageLimit: pageLimit);
            Context.Await(Lifecycle.OpenVaultAsync(Root), "the vault open");
            OpenEvents = [.. Events];
            OpenStatusText = Lifecycle.StatusText;
            Events.Clear();
            // Well past the open scan: the foreground cooldown counts from it.
            Now += TimeSpan.FromMinutes(1);
        }

        public static Harness OpenExisting(string root) =>
            new(root, [], VaultLifecycleViewModel.DefaultScanDeltaPageLimit, null);

        public SerialContext Context { get; } = new();

        public string Root { get; }

        public List<A11yEvent> Events { get; } = [];

        public IReadOnlyList<A11yEvent> OpenEvents { get; }

        public string OpenStatusText { get; }

        public string[] OpenSpoken =>
            [.. OpenEvents.Select(e => SlateUniffiMethods.A11yRender(e).Text)];

        public string[] Spoken =>
            [.. Events.Select(e => SlateUniffiMethods.A11yRender(e).Text)];

        public VaultLifecycleViewModel Lifecycle { get; }

        public DateTimeOffset Now { get; set; } = DateTimeOffset.UnixEpoch;

        public int ScanCalls => Volatile.Read(ref _scanCalls);

        /// <summary>The worker call (1-based, the open included) that runs its
        /// scan and then parks before returning — the change a fact makes while
        /// it is parked is one that scan missed.</summary>
        public int? ParkAfterCall { get; set; }

        public ManualResetEventSlim Parked { get; } = new(false);

        public ManualResetEventSlim Release { get; } = new(false);

        public Func<int, Exception?>? ThrowOnCall { get; set; }

        public WorkspaceViewModel Workspace => Lifecycle.Workspace!;

        public FilesSidebarViewModel Sidebar => Lifecycle.FileSidebar!;

        public QuickSwitcherViewModel QuickOpen => Lifecycle.QuickSwitcher!;

        public IEnumerable<WorkspaceTabViewModel> AllTabs =>
            Workspace.Groups.SelectMany(group => group.Tabs);

        public ScanDeltaLedger Ledger => Lifecycle.SessionForTests!.ScanDeltaLedger();

        /// <summary>Fail page reads by their global ordinal (1-based) across
        /// every run of this vault.</summary>
        public void FailReads(Func<int, bool> predicate) => _failRead[0] = predicate;

        /// <summary>Cancel the run's token at these read ordinals (1-based,
        /// across every run of this vault), before the read.</summary>
        public void CancelReads(Func<int, bool> predicate) => _cancelRead[0] = predicate;

        /// <summary>Observe every fetched page before the host re-checks and
        /// applies it; null stops observing.</summary>
        public void AfterRead(Action<ScanDeltaPage>? observer) => _afterRead = observer;

        /// <summary>Every ledger call the lifecycle made: its name, thread
        /// and whether that was a pool thread.</summary>
        public ConcurrentQueue<(string Call, int Thread, bool Pool)> LedgerCalls { get; } = new();

        /// <summary>The fact's own (STA, UI) thread.</summary>
        public int UiThread { get; } = Environment.CurrentManagedThreadId;

        /// <summary>A Slate-owned create through the host's session, from
        /// whatever thread the fact is on; its Created event is POSTED to
        /// the UI queue by the lifecycle's real listener, not drained.</summary>
        public void CreateInCore(string path, string text) =>
            _ = Lifecycle.SessionForTests!.CreateExclusive(path, text);

        /// <summary>A Slate-owned delete through the host's session (to the
        /// OS trash, whose COM apartment wants the UI thread); its Deleted
        /// event is posted, not drained.</summary>
        public void DeleteInCore(string path) => Lifecycle.SessionForTests!.DeleteFile(path);

        /// <summary>The openable files on disk, vault-relative, sorted —
        /// what Quick Open must list.</summary>
        public string[] OpenableFilesOnDisk()
        {
            string cache = Path.Combine(Root, ".slate") + Path.DirectorySeparatorChar;
            return
            [
                .. Directory.EnumerateFiles(Root, "*.md", SearchOption.AllDirectories)
                    .Where(file => !file.StartsWith(cache, StringComparison.OrdinalIgnoreCase))
                    .Select(file => Path.GetRelativePath(Root, file).Replace('\\', '/'))
                    .Order(StringComparer.Ordinal),
            ];
        }

        /// <summary>Page reads so far, across every run of this vault.</summary>
        public int Reads => Volatile.Read(ref _reads);

        /// <summary>A Slate-owned create through the session the host holds
        /// — the core call the host's create commands make — and its
        /// Created event handled through the lifecycle's real listener.</summary>
        public void CreateThroughSlate(string path, string text)
        {
            _ = Lifecycle.SessionForTests!.CreateExclusive(path, text);
            Context.Drain();
        }

        /// <summary>A page of the Pending generation as core pages it now,
        /// flags included (reading never moves the cursor).</summary>
        public ScanDeltaPage PendingPage(string? cursor, uint limit)
        {
            using var cancel = new CancelToken();
            return Lifecycle.SessionForTests!.ScanDeltaPage(
                Ledger.Pending!.Generation, new Paging(cursor, limit), cancel);
        }

        /// <summary>Fail each generation's Applied mark (the last page's
        /// report) — takes effect from the next run's channel.</summary>
        public void FailAppliedMarks(bool fail) => _failAppliedMark = fail;

        public void OnApplied(Action<ulong, string?> observer) => _onApplied = observer;

        /// <summary>Observe the session after every ledger call the lifecycle
        /// makes that reached it.</summary>
        public void AfterEveryLedgerCall(Action observer) => _afterLedgerCall = observer;

        public static string NewRoot(string label)
        {
            string root = Path.Combine(Path.GetTempPath(), $"slate-rescan-{label}-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            return root;
        }

        public static void Seed(string root, params (string Path, string Text)[] files)
        {
            foreach ((string path, string text) in files)
            {
                string full = Path.Combine(root, path);
                Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                File.WriteAllText(full, text);
            }
        }

        /// <summary>Every file's bytes untouched, its mtime moved: the scan
        /// must re-read and hash each one.</summary>
        public static void TouchAll(string root)
        {
            string cache = Path.Combine(root, ".slate") + Path.DirectorySeparatorChar;
            foreach (string file in Directory.EnumerateFiles(root, "*.md", SearchOption.AllDirectories))
            {
                if (!file.StartsWith(cache, StringComparison.OrdinalIgnoreCase))
                {
                    File.SetLastWriteTimeUtc(file, File.GetLastWriteTimeUtc(file).AddSeconds(7));
                }
            }
        }

        /// <summary>Write, and move the mtime past anything a scan saw, so a
        /// same-size edit is never hidden by the short-circuit (AR-7).</summary>
        public void Write(string path, string text)
        {
            string full = Path.Combine(Root, path);
            DateTime? before = File.Exists(full) ? File.GetLastWriteTimeUtc(full) : null;
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, text);
            if (before is DateTime previous)
            {
                File.SetLastWriteTimeUtc(full, previous.AddSeconds(5));
            }
        }

        public void Delete(string path) => File.Delete(Path.Combine(Root, path));

        /// <summary>Open a tab as fact setup: the lines the open itself speaks
        /// (the tab's focus, its outline count) are not the rescan's and are
        /// dropped from the captured sequence.</summary>
        public WorkspaceTabViewModel Open(string path)
        {
            int before = Events.Count;
            Workspace.OpenPath(path, WorkspaceOpenTarget.NewTab);
            Context.Drain();
            Events.RemoveRange(before, Events.Count - before);
            return AllTabs.Last(tab => tab.Path == path);
        }

        public bool VolumeAliasesCase()
        {
            string probe = Path.Combine(Root, "Probe-Case.tmp");
            File.WriteAllText(probe, "p");
            try
            {
                return File.Exists(Path.Combine(Root, "probe-case.tmp"));
            }
            finally
            {
                File.Delete(probe);
            }
        }

        /// <summary>The open's session load and every rescan's scan: call
        /// <see cref="ScanCalls"/> may throw or park after its work.</summary>
        private Task<T> RunOnWorker<T>(Func<T> work)
        {
            int call = Interlocked.Increment(ref _scanCalls);
            if (ThrowOnCall?.Invoke(call) is Exception fault)
            {
                return Task.FromException<T>(fault);
            }

            if (ParkAfterCall == call)
            {
                return Task.Run(() =>
                {
                    T loaded = work();
                    Parked.Set();
                    Release.Wait(TimeSpan.FromSeconds(30));
                    return loaded;
                });
            }

            return Task.FromResult(work());
        }

        public void Dispose()
        {
            Release.Set();
            try
            {
                Lifecycle.Dispose();
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(_previous);
                foreach (string directory in new[] { Root, Root + "-device" })
                {
                    try
                    {
                        if (Directory.Exists(directory))
                        {
                            Directory.Delete(directory, recursive: true);
                        }
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

    /// <summary>A Deny ACE for the current user on one file or folder,
    /// removed on dispose — a real unreadable entry on NTFS.</summary>
    private sealed class DenyAccess : IDisposable
    {
        private readonly FileSystemInfo _target;
        private readonly FileSystemAccessRule _rule;

        private DenyAccess(FileSystemInfo target, FileSystemRights rights)
        {
            _target = target;
            _rule = new FileSystemAccessRule(
                WindowsIdentity.GetCurrent().User!,
                rights,
                AccessControlType.Deny);
            Apply(add: true);
        }

        public static DenyAccess To(string path, FileSystemRights rights) =>
            new(Directory.Exists(path) ? new DirectoryInfo(path) : new FileInfo(path), rights);

        public void Dispose() => Apply(add: false);

        private void Apply(bool add)
        {
            switch (_target)
            {
                case DirectoryInfo directory:
                    {
                        DirectorySecurity security = directory.GetAccessControl();
                        if (add)
                        {
                            security.AddAccessRule(_rule);
                        }
                        else
                        {
                            security.RemoveAccessRule(_rule);
                        }

                        directory.SetAccessControl(security);
                        break;
                    }

                case FileInfo file:
                    {
                        FileSecurity security = file.GetAccessControl();
                        if (add)
                        {
                            security.AddAccessRule(_rule);
                        }
                        else
                        {
                            security.RemoveAccessRule(_rule);
                        }

                        file.SetAccessControl(security);
                        break;
                    }
            }
        }
    }
}
