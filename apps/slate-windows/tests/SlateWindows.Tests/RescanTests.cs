// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Windows.Documents;
using System.Windows.Input;
using SlateWindows.Canvas;
using SlateWindows.Commands;
using SlateWindows.Graph;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W7-7 PR 7 (#1252, contract R-9): Files Sidebar → Refresh and the
/// foreground rescan reconcile what changed outside Slate — the tree, Quick
/// Open, every open tab — and say what they found in exactly one sentence.
/// </summary>
/// <remarks>
/// Every fact drives the REAL path: a real vault on disk, the lifecycle's
/// own <c>RescanAsync</c>, core's rescan through the binding, the re-sync
/// from the index (AR-18's fallback) and the workspace's real tabs. The
/// only seams are the ones the lifecycle already takes — the rescan core
/// worker (to park a scan or make it throw), the clock, and the
/// publication seam (to park or fail one re-sync publication). Assertions over
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

    /// <summary>Through the real rescan → re-sync → tab path: a clean tab
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

    /// <summary>Codex PR 7 round 1, finding 11: while an import runs, Refresh
    /// is unavailable — its CanExecute, and the palette resolver's SPECIFIC
    /// reason, an availability rejection rather than a failure — with a
    /// CanExecuteChanged when the import starts and when it settles; then it
    /// is available again.</summary>
    [Fact]
    public void RefreshIsUnavailableDuringAnImportAndSaysWhy() => RunSta(() =>
    {
        var picker = new TaskCompletionSource<IReadOnlyList<string>>();
        using var h = new Harness("refresh-import", pickImportSources: () => picker.Task, files: [("alpha.md", "# Alpha\n")]);
        ICommand refresh = h.Sidebar.RefreshCommand;
        int changes = 0;
        refresh.CanExecuteChanged += (_, _) => changes++;
        Assert.True(refresh.CanExecute(null));
        Assert.Null(SlateCommandRegistrar.DisabledReason(h.Lifecycle, ChordTable.Ids.SidebarRefresh));

        h.Sidebar.ImportCommand.Execute(null);
        Assert.True(h.Sidebar.IsImporting);
        Assert.True(changes > 0, "the import's start requeried Refresh");
        Assert.False(refresh.CanExecute(null));
        string? reason = SlateCommandRegistrar.DisabledReason(h.Lifecycle, ChordTable.Ids.SidebarRefresh);
        Assert.Equal(SlateCommandRegistrar.StructuralMutationBusyReason, reason);
        Assert.True(SlateCommandRegistrar.IsAvailabilityRejection(reason!));

        int beforeSettle = changes;
        picker.SetResult([]);
        h.Context.RunUntil(() => !h.Sidebar.IsImporting, "the import settling");
        Assert.True(changes > beforeSettle, "the import's end requeried Refresh");
        Assert.True(refresh.CanExecute(null));
        Assert.Null(SlateCommandRegistrar.DisabledReason(h.Lifecycle, ChordTable.Ids.SidebarRefresh));
    });

    /// <summary>Finding 11, the trash arm: while a trash operation holds its
    /// confirmation, Refresh is unavailable with the same specific reason;
    /// once it ends, Refresh is available again.</summary>
    [Fact]
    public void RefreshIsUnavailableDuringATrashAndSaysWhy() => RunSta(() =>
    {
        using var h = new Harness("refresh-trash", ("full/one.md", "1\n"), ("full/two.md", "2\n"));
        ICommand refresh = h.Sidebar.RefreshCommand;
        bool? availableDuring = null;
        string? reasonDuring = null;
        h.Sidebar.ConfirmRecycle = _ =>
        {
            availableDuring = refresh.CanExecute(null);
            reasonDuring = SlateCommandRegistrar.DisabledReason(h.Lifecycle, ChordTable.Ids.SidebarRefresh);
            return false;
        };
        h.Sidebar.SelectedNode = Assert.Single(h.Sidebar.RootNodes, node => node.Path == "full");

        h.Sidebar.DeleteCommand.Execute(null);
        h.Context.RunUntil(() => availableDuring is not null, "the trash confirmation");
        h.Context.RunUntil(() => !h.Sidebar.IsTrashing, "the trash ending");

        Assert.False(availableDuring);
        Assert.Equal(SlateCommandRegistrar.StructuralMutationBusyReason, reasonDuring);
        Assert.True(refresh.CanExecute(null));
        Assert.Null(SlateCommandRegistrar.DisabledReason(h.Lifecycle, ChordTable.Ids.SidebarRefresh));
    });

    // ---------------------------------------------------------------------
    // Bounded reports, threading and the host's classification
    // ---------------------------------------------------------------------

    /// <summary>Rounds 25-27 (bounded errors): 1,200 unreadable notes cross
    /// the FFI as an exact count and at most five samples — the initial
    /// open's report and the rescan's alike — and the rescan's one sentence
    /// carries the whole count.</summary>
    [Fact]
    public void AScanFfiResultStaysBoundedUnderThousandsOfFailures() => RunSta(() =>
    {
        const int Failing = 1200;
        string root = Harness.NewRoot("bounded-ffi");
        Harness.Seed(
            root,
            [("ok.md", "# Ok\n"), .. Enumerable.Range(0, Failing).Select(n => ($"locked-{n:0000}.md", $"# Locked {n}\n"))]);
        var denials = new List<DenyAccess>(Failing);
        Harness? h = null;
        try
        {
            foreach (int n in Enumerable.Range(0, Failing))
            {
                denials.Add(DenyAccess.To(Path.Combine(root, $"locked-{n:0000}.md"), FileSystemRights.ReadData));
            }

            h = Harness.OpenExisting(root);
            ScanReport opened = Assert.IsType<ScanReport>(h.OpenReport);
            Assert.Equal((ulong)Failing, opened.ErrorCount);
            Assert.Equal(5, opened.ErrorSamples.Length);
            Assert.False(opened.Complete);

            h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

            ScanReport rescanned = Assert.IsType<ScanReport>(h.LastRescanReport);
            Assert.Equal((ulong)Failing, rescanned.ErrorCount);
            Assert.Equal(5, rescanned.ErrorSamples.Length);
            A11yEvent.VaultRescanIncomplete incomplete =
                Assert.IsType<A11yEvent.VaultRescanIncomplete>(Assert.Single(h.Events));
            Assert.Equal((ulong)Failing, incomplete.Errors);
            Assert.Equal([$"Files refreshed with errors. {Failing} errors; results may be incomplete."], h.Spoken);
        }
        finally
        {
            foreach (DenyAccess denial in denials)
            {
                denial.Dispose();
            }

            h?.Dispose();
        }
    });

    /// <summary>A case-only rename outside Slate: the re-sync finds no index
    /// row for the tab's spelling and marks it missing, then re-seats it on
    /// the spelling the filesystem stores (#1077), so the tab follows the new
    /// spelling with its content — silently, the one sentence counting a
    /// removal and a creation (AR-8: no rename correlation).</summary>
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

    /// <summary>Round 26 (locked decision 05 §4.1): ONE seam carries every
    /// rescan core call, and none runs on the UI thread. Across a rescan
    /// and a close that lands on a parked one, every operation — the
    /// token's creation, the scan, the re-sync's index reads, the cancel
    /// and the disposal — lands on a pool thread, never the fact's STA
    /// (dispatcher) thread.</summary>
    [Fact]
    public void NoRescanCoreCallRunsOnTheUiThread() => RunSta(() =>
    {
        using var h = new Harness("core-threads", ("a.md", "a0\n"), ("b.md", "b0\n"));
        h.Write("a.md", "a1 text\n");
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
        Assert.Equal(["Files refreshed. 1 new or changed, 0 removed."], h.Spoken);

        // A close lands on a parked rescan: its token is cancelled and
        // disposed through the seam too.
        h.Write("a.md", "a2 text, longer\n");
        h.Now += TimeSpan.FromMinutes(1);
        h.ParkAfterCall = h.ScanCalls + 1;
        Task parked = h.Lifecycle.RescanAsync(RescanReason.Explicit);
        h.Context.RunUntil(() => h.Parked.IsSet, "the scan parking");
        h.Release.Set();
        h.Lifecycle.CloseVault();
        h.Context.Await(parked, "the closed rescan");

        (string Operation, int Thread, bool Pool)[] calls = [.. h.CoreCalls];
        Assert.Contains("token", calls.Select(call => call.Operation));
        Assert.Contains("scan", calls.Select(call => call.Operation));
        Assert.Contains("cancel", calls.Select(call => call.Operation));
        Assert.Contains("dispose", calls.Select(call => call.Operation));
        Assert.All(calls, call =>
        {
            Assert.NotEqual(h.UiThread, call.Thread);
            Assert.True(call.Pool, $"{call.Operation} ran on a non-pool thread");
        });
    });

    /// <summary>Rounds 25-26 and v2 §1 (the worker read's ticket): a
    /// Slate-owned write that commits WHILE a clean tab's reload reads on the
    /// worker wins. The write's own event marks the tab stale before the read
    /// comes back, which moves the tab's content generation past the ticket
    /// the read captured, so the tab is never re-baselined on the bytes the
    /// worker read before the write; it keeps its text, marked stale. The
    /// scan's own count stands: it found the external change.</summary>
    [Fact]
    public void ASlateWriteDuringAReloadsReadWinsOverIt() => RunSta(() =>
    {
        using var h = new Harness("write-during-read", ("x.md", "x0\n"));
        WorkspaceTabViewModel x = h.Open("x.md");
        h.Write("x.md", "x1 external\n");
        bool wrote = false;
        h.AfterCoreCall = operation =>
        {
            if (operation == "read" && !wrote)
            {
                // On the read's worker, after the read and before its
                // continuation: the write commits in between.
                wrote = true;
                _ = h.Lifecycle.SessionForTests!.SaveText("x.md", "x2 through Slate\n", null);
            }
        };

        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
        h.Settle();

        Assert.True(wrote, "the reload never read x.md on the worker");
        Assert.Equal("x0\n", x.Text);
        Assert.False(x.IsDirty);
        Assert.True(x.IsExternallyStale, "the write's own event never reconciled the tab");
        Assert.Equal("x2 through Slate\n", File.ReadAllText(Path.Combine(h.Root, "x.md")));
        Assert.Equal([Explicit1], h.Spoken);
    });

    /// <summary>Rounds 26-27: the host's document classification — the
    /// Slate-owned event path's, Quick Open's and the Bases dependents' — IS
    /// core's: both exported sets, read off the UI thread.</summary>
    [Fact]
    public async Task TheHostDocumentClassificationIsCores()
    {
        string[] openable = [.. await Task.Run(SlateUniffiMethods.OpenableDocumentExtensions)];
        string[] markdown = [.. await Task.Run(SlateUniffiMethods.MarkdownDocumentExtensions)];
        Assert.Equal(
            openable.Order(StringComparer.Ordinal).ToArray(),
            CoreDocumentClassification.OpenableExtensions.Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(
            markdown.Order(StringComparer.Ordinal).ToArray(),
            CoreDocumentClassification.MarkdownExtensions.Order(StringComparer.Ordinal).ToArray());
    }

    // ---------------------------------------------------------------------
    // Kind-aware reconciliation (rounds 27-28)
    // ---------------------------------------------------------------------

    private const string OneNodeCanvas =
        """{"nodes":[{"id":"first","type":"text","text":"First","x":0,"y":0,"width":200,"height":60}],"edges":[]}""";

    private const string TwoNodeCanvas =
        """{"nodes":[{"id":"first","type":"text","text":"First","x":0,"y":0,"width":200,"height":60},{"id":"added","type":"text","text":"Added outside Slate","x":0,"y":100,"width":200,"height":60}],"edges":[]}""";

    /// <summary>Round 27: an open canvas is registry-cached, so the funnel's
    /// Markdown-only Modified arm would leave its board stale while the tree,
    /// Quick Open and the sentence reported the change. The rescan's canvas
    /// arm re-reads it: the node added outside Slate is on the board.</summary>
    [Fact]
    public void AnOpenCanvasTabReloadsAfterARescan() => RunSta(() =>
    {
        using var h = new Harness("canvas-reload", ("board.canvas", OneNodeCanvas));
        WorkspaceTabViewModel tab = h.Open("board.canvas");
        CanvasDocumentViewModel board = Assert.IsType<CanvasDocumentViewModel>(tab.Canvas);
        h.PumpUntil(() => board.RowFor("first") is not null, "the board's first load");
        Assert.Null(board.RowFor("added"));

        h.Write("board.canvas", TwoNodeCanvas);
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        Assert.NotNull(board.RowFor("added"));
        Assert.Equal(["Files refreshed. 1 new or changed, 0 removed."], h.Spoken);
    });

    /// <summary>Round 28: the run speaks only once every open document's
    /// reload has PUBLISHED. With the canvas reload parked, the run holds
    /// and says nothing; released, the board shows the change and the one
    /// sentence follows.</summary>
    [Fact]
    public void AParkedCanvasReloadHoldsTheSentence() => RunSta(() =>
    {
        using var h = new Harness("canvas-parked", ("board.canvas", OneNodeCanvas));
        WorkspaceTabViewModel tab = h.Open("board.canvas");
        CanvasDocumentViewModel board = Assert.IsType<CanvasDocumentViewModel>(tab.Canvas);
        h.PumpUntil(() => board.RowFor("first") is not null, "the board's first load");
        var parked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool reloadStarted = false;
        h.Workspace.RescanPublicationForTests = async (kind, _, reload) =>
        {
            if (kind == "canvas")
            {
                reloadStarted = true;
                await parked.Task;
            }

            await reload();
        };
        h.Write("board.canvas", TwoNodeCanvas);

        Task run = h.Lifecycle.RescanAsync(RescanReason.Explicit);
        h.PumpUntil(() => reloadStarted, "the canvas reload starting");
        h.Settle();

        Assert.False(run.IsCompleted);
        Assert.Empty(h.Events);

        parked.SetResult();
        h.PumpUntil(() => run.IsCompleted, "the rescan finishing");
        Assert.NotNull(board.RowFor("added"));
        Assert.Equal(["Files refreshed. 1 new or changed, 0 removed."], h.Spoken);
    });

    /// <summary>Round 28: a failed reload — an open canvas's, then an open
    /// base's — makes the run speak the honest count: a clean scan whose one
    /// re-sync operation failed is "1 error". Nothing is retained; the next
    /// rescan re-syncs from the index and completes the reload.</summary>
    [Fact]
    public void AFailedKindReloadSpeaksIncompleteAndTheNextRescanCompletesIt() => RunSta(() =>
    {
        foreach (string kind in new[] { "canvas", "base" })
        {
            (string Path, string Before, string After) file = kind == "canvas"
                ? ("board.canvas", OneNodeCanvas, TwoNodeCanvas)
                : ("Notes.base", "views:\n  - type: table\n    name: Main\n", "views:\n  - type: table\n    name: Renamed\n");
            using var h = new Harness($"{kind}-fails", (file.Path, file.Before), ("n.md", "# N\n"));
            WorkspaceTabViewModel tab = h.Open(file.Path);
            h.PumpUntil(
                () => kind == "canvas"
                    ? tab.Canvas?.RowFor("first") is not null
                    : tab.Base?.State == Bases.BaseLoadState.Ready,
                $"the {kind}'s first load");
            bool fail = true;
            h.Workspace.RescanPublicationForTests = async (reloading, _, reload) =>
            {
                if (reloading == kind && fail)
                {
                    throw new IOException($"injected {kind} reload failure");
                }

                await reload();
            };
            h.Write(file.Path, file.After);

            h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

            A11yEvent.VaultRescanIncomplete incomplete =
                Assert.IsType<A11yEvent.VaultRescanIncomplete>(Assert.Single(h.Events));
            Assert.Equal(1UL, incomplete.Errors);
            Assert.Equal([OneError], h.Spoken);

            fail = false;
            h.Now += TimeSpan.FromMinutes(1);
            h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
            // The next rescan is independent: its scan finds nothing new; its
            // re-sync completes the reload. Its one sentence is the re-sync
            // outcome's (contract R-9).
            Assert.Equal(2, h.Events.Count);
            if (kind == "canvas")
            {
                Assert.NotNull(tab.Canvas!.RowFor("added"));
            }
            else
            {
                h.PumpUntil(() => tab.Base?.ActiveViewName == "Renamed", "the base's reloaded view");
            }
        }
    });

    /// <summary>Round 27: an open base tab re-queries after a rescan — its own
    /// definition changed outside Slate reloads (the kind reload, awaited),
    /// and a note created outside Slate that it lists joins its rows (the
    /// Bases dependent).</summary>
    [Fact]
    public void AnOpenBaseTabRequeriesAfterARescan() => RunSta(() =>
    {
        using var h = new Harness(
            "base-requery",
            ("Notes.base", "filters: 'file.ext == \"md\"'\nviews:\n  - type: table\n    name: Main\n"),
            ("a.md", "# A\n"));
        WorkspaceTabViewModel tab = h.Open("Notes.base");
        h.PumpUntil(() => tab.Base?.State == Bases.BaseLoadState.Ready, "the base's first load");
        Assert.Equal(["a.md"], BaseRows(tab));

        h.Write("b.md", "# B\n");
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
        h.PumpUntil(() => BaseRows(tab).Contains("b.md"), "the base listing the new note");

        h.Write(
            "Notes.base",
            "filters: 'file.ext == \"md\"'\nviews:\n  - type: table\n    name: Renamed view\n");
        h.Now += TimeSpan.FromMinutes(1);
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
        Assert.Equal("Renamed view", tab.Base!.ActiveViewName);
    });

    /// <summary>Round 27: Bases' Markdown classification is core's. A base
    /// over .mdown notes re-queries when a .mdown note is created outside
    /// Slate — the Bases dependent no longer drops every change but a .md
    /// one.</summary>
    [Fact]
    public void BasesFollowCoreMarkdownClassification() => RunSta(() =>
    {
        using var h = new Harness(
            "bases-mdown",
            ("Mdown.base", "filters: 'file.ext == \"mdown\"'\nviews:\n  - type: table\n    name: Main\n"),
            ("one.mdown", "# One\n"));
        WorkspaceTabViewModel tab = h.Open("Mdown.base");
        h.PumpUntil(() => tab.Base?.State == Bases.BaseLoadState.Ready, "the base's first load");
        Assert.Equal(["one.mdown"], BaseRows(tab));

        h.Write("two.mdown", "# Two\n");
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        h.PumpUntil(() => BaseRows(tab).Contains("two.mdown"), "the base listing the new .mdown note");
    });

    /// <summary>Round 28 (the reading dependent), on the re-sync: a
    /// reading-mode note that embeds another owes its embed a re-render
    /// when the embedded note changes outside Slate. The re-sync carries no
    /// per-path delta (AR-18's fallback), so every reading model that
    /// depends on OTHER files re-projects on a rescan — the artifact digest
    /// makes an unchanged one a memo hit; with no surface attached (this
    /// harness) the model records the pending re-render its surface runs on
    /// rebind — while a reading-mode note that embeds nothing owes nothing:
    /// its own note's change reaches it through its tab's reload.</summary>
    [Fact]
    public void AReadingEmbedFollowsARescan() => RunSta(() =>
    {
        using var h = new Harness(
            "reading-embed",
            ("host.md", "# Host\n\n![[embedded]]\n"),
            ("embedded.md", "Embedded before.\n"),
            ("plain.md", "# Plain\n\nNo embeds.\n"));
        WorkspaceTabViewModel host = h.Open("host.md");
        host.ToggleViewMode();
        WorkspaceTabViewModel plain = h.Open("plain.md");
        plain.ToggleViewMode();
        h.PumpUntil(() => ReadingText(host).Contains("Embedded before.", StringComparison.Ordinal), "the embed's first render");
        h.PumpUntil(() => ReadingText(plain).Contains("No embeds.", StringComparison.Ordinal), "the plain note's first render");

        Assert.False(host.Reading!.HasPendingDependencyRefresh);
        Assert.False(plain.Reading!.HasPendingDependencyRefresh);

        h.Write("embedded.md", "Embedded after, changed outside Slate.\n");
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        Assert.True(host.Reading!.HasPendingDependencyRefresh, "the embedder owes no re-render");
        Assert.False(plain.Reading!.HasPendingDependencyRefresh, "a note that embeds nothing was re-rendered");
    });

    /// <summary>Round 28 (the history dependent): the history panel showing a
    /// note reloads its version list when a rescan finds the note changed
    /// outside Slate — the same NoteSaved seam a Slate-owned save drives.</summary>
    [Fact]
    public void TheHistoryPanelReloadsAfterARescan() => RunSta(() =>
    {
        using var h = new Harness("history-dependent", ("x.md", "x0\n"), ("y.md", "y0\n"));
        WorkspaceTabViewModel x = h.Open("x.md");
        // DIRTY: the tab keeps its buffer (marked stale), so its own clean
        // reload — which re-lists history on the way — never runs; only
        // the rescan's history dependent reaches the panel.
        x.Text = "x0\nmine\n";
        h.Workspace.History.NoteChanged("x.md");
        int loads = 0;
        h.Workspace.History.LoadInterleaveForTests = () => Interlocked.Increment(ref loads);

        h.Write("y.md", "y1, not the shown note\n");
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
        for (int turn = 0; turn < 20; turn++)
        {
            h.Context.Drain();
            PumpedDispatcher.Drain();
            Thread.Sleep(5);
        }

        Assert.Equal(0, Volatile.Read(ref loads));

        h.Write("x.md", "x1, changed outside Slate\n");
        h.Now += TimeSpan.FromMinutes(1);
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        // Awaited to its publication: reloaded by the time the run is done.
        Assert.True(Volatile.Read(ref loads) > 0, "the history panel never reloaded");
        Assert.True(x.IsDirty);
        Assert.Equal("x0\nmine\n", x.Text);
    });

    /// <summary>Rounds 28-29 (the graph dependent, ONE authority), on the
    /// re-sync: a rescan reaches the graph only through the re-sync's probe —
    /// the Connections leaf's, and the graph tab's when one is visible —
    /// exactly as a Slate-owned event does. The index phase's ScanFinished
    /// probe is withheld for a rescan (the open scan keeps it), so EVERY
    /// rescan probes exactly once: an unchanged one too, which is how a
    /// STALE or Error graph recovers on Refresh (codex round 1, finding 8),
    /// and a changed one never twice.</summary>
    [Fact]
    public void TheGraphProbesAfterARescan() => RunSta(() =>
    {
        using var h = new Harness("graph-dependent", ("x.md", "# X\n"));
        int ProbesSoFar()
        {
            lock (h.Workspace.Connections.CrossingsForTests)
            {
                return h.Workspace.Connections.CrossingsForTests["graph_generation"];
            }
        }

        void Settle()
        {
            for (int turn = 0; turn < 40; turn++)
            {
                h.Context.Drain();
                PumpedDispatcher.Drain();
                Thread.Sleep(5);
            }
        }

        Settle();
        int before = ProbesSoFar();
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
        Settle();
        Assert.Equal(1, ProbesSoFar() - before);

        h.Write("linker.md", "# Linker\n\n[[x]]\n");
        h.Now += TimeSpan.FromMinutes(1);
        before = ProbesSoFar();
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
        Settle();
        Assert.Equal(1, ProbesSoFar() - before);
    });

    /// <summary>Round 29: the re-sync's probe is the rescan's graph path —
    /// and a sufficient one. The Connections leaf following a note lists a
    /// backlink created outside Slate the moment the rescan completes (the
    /// probe, and the reload it issues, are awaited to publication); with
    /// the re-sync's probe removed the leaf stays stale, because the index
    /// phase does not probe for a rescan.</summary>
    [Fact]
    public void TheGraphFollowsARescanThroughTheRoutineAlone() => RunSta(() =>
    {
        using var h = new Harness("graph-follows", ("x.md", "# X\n"));
        ConnectionsLeafViewModel leaf = h.Workspace.Connections;
        h.Workspace.ActiveLeaf = WorkspaceViewModel.Leaves.First(option => option.Id == "connections");
        _ = h.Open("x.md");
        h.PumpUntil(() => leaf.Publication.HoldsTree && leaf.IsCurrent && !leaf.InFlight, "the leaf's first tree");
        Assert.DoesNotContain(leaf.Publication.Tree!.Incoming, row => row.Path == "linker.md");

        h.Write("linker.md", "# Linker\n\n[[x]]\n");
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        Assert.True(leaf.Publication.HoldsTree, "the leaf holds no tree after the rescan");
        Assert.Contains(leaf.Publication.Tree!.Incoming, row => row.Path == "linker.md");
    });

    /// <summary>Round 29 (ONE graph authority for a rescan), structurally:
    /// the rescan's graph probe has exactly one reference — the re-sync's
    /// dependents (<c>ReSyncDependentsAsync</c>, itself reached only from
    /// the lifecycle's re-sync) — and the index phase's ScanFinished probe
    /// is guarded by the rescan flag read where the scan emits it.
    /// <see cref="TheGraphFollowsARescanThroughTheRoutineAlone"/> is the
    /// behavioral half: without that call site the graph stays stale.</summary>
    [Fact]
    public void TheRescanGraphProbeHasOneCallSite()
    {
        Dictionary<string, string> sources = ShellSources();
        Assert.Equal([("WorkspaceViewModel.Rescan.cs", 1)], References(sources, "NotifyGraphOfRescanAsync"));
        Assert.Equal([("VaultLifecycleViewModel.Rescan.cs", 1)], References(sources, "ReSyncDependentsAsync"));

        Match arm = Regex.Match(
            sources["VaultLifecycleViewModel.cs"],
            @"private void HandleIndexPhase\((?<parameters>[^)]*)\)\s*\{(?<body>.*?)\n    \}",
            RegexOptions.Singleline);
        Assert.True(arm.Success, "HandleIndexPhase not found");
        Assert.Contains("bool duringRescan", arm.Groups["parameters"].Value, StringComparison.Ordinal);
        Assert.Matches(
            @"if \(generation == _generation && phase == IndexPhase\.ScanFinished && !duringRescan\)\s*\{\s*Workspace\?\.NotifyGraphOfVaultChange\(\);\s*\}",
            arm.Groups["body"].Value);
        Assert.Single(Regex.Matches(arm.Groups["body"].Value, @"\bNotifyGraphOf\w+"));
        Assert.Single(Regex.Matches(
            sources["VaultLifecycleViewModel.cs"],
            @"bool duringRescan = Volatile\.Read\(ref _rescanActive\);\s*_enqueueUi\(\(\) => HandleIndexPhase\(generation, phase, filesSeen, duringRescan\)\);"));
    }

    /// <summary>The shell's C# sources by root-relative path, comments
    /// stripped (a comment that names a method is not a call site).</summary>
    private static Dictionary<string, string> ShellSources()
    {
        string root = SourceText.ShellSourceRoot();
        var sources = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            if (relative.StartsWith("obj/", StringComparison.Ordinal) || relative.StartsWith("bin/", StringComparison.Ordinal))
            {
                continue;
            }

            string code = Regex.Replace(File.ReadAllText(file), @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
            sources[relative] = Regex.Replace(code, @"//[^\n]*", string.Empty);
        }

        return sources;
    }

    /// <summary>Every file that references <paramref name="name"/> other
    /// than by declaring it (a call, or a method group handed on), with
    /// its count.</summary>
    private static (string File, int Count)[] References(Dictionary<string, string> sources, string name) =>
    [
        .. sources
            .Select(source => (
                File: source.Key,
                Count: Regex.Matches(source.Value, $@"\b{name}\b").Count
                    - Regex.Matches(source.Value, $@"\bTask(?:<[^>]+>)?\s+{name}\s*\(").Count))
            .Where(reference => reference.Count > 0)
            .OrderBy(reference => reference.File, StringComparer.Ordinal),
    ];

    /// <summary>Round 29: every fallible dependent publication a rescan
    /// triggers — the reading models' (reverse dependencies), the history
    /// panel's, the Bases surfaces', the graph's — is awaited before the
    /// run speaks. With one parked, the run holds and says nothing;
    /// released, the one sentence follows.</summary>
    [Theory]
    [InlineData("reading")]
    [InlineData("history")]
    [InlineData("bases")]
    [InlineData("graph")]
    public void AParkedDependentPublicationHoldsTheSentence(string kind) => RunSta(() =>
    {
        using Harness h = DependentsHarness($"{kind}-parked");
        var parked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool started = false;
        h.Workspace.RescanPublicationForTests = async (publishing, _, publish) =>
        {
            if (publishing == kind)
            {
                started = true;
                await parked.Task;
            }

            await publish();
        };
        h.Write("embedded.md", "Embedded after, changed outside Slate.\n");

        Task run = h.Lifecycle.RescanAsync(RescanReason.Explicit);
        h.PumpUntil(() => started, $"the {kind} publication starting");
        h.Settle();

        Assert.False(run.IsCompleted);
        Assert.Empty(h.Events);

        parked.SetResult();
        h.PumpUntil(() => run.IsCompleted, "the rescan finishing");
        Assert.Equal([Explicit1], h.Spoken);
    });

    /// <summary>Round 29: an injected failure of a dependent's publication
    /// makes the run say "results may be incomplete" with the honest
    /// count; the next, independent rescan re-syncs and completes it.</summary>
    [Theory]
    [InlineData("reading")]
    [InlineData("history")]
    [InlineData("bases")]
    [InlineData("graph")]
    public void AFailedDependentPublicationSpeaksIncompleteAndTheNextRescanCompletesIt(string kind) => RunSta(() =>
    {
        using Harness h = DependentsHarness($"{kind}-fails");
        bool fail = true;
        h.Workspace.RescanPublicationForTests = async (publishing, _, publish) =>
        {
            if (publishing == kind && fail)
            {
                throw new IOException($"injected {kind} publication failure");
            }

            await publish();
        };
        h.Write("embedded.md", "Embedded after, changed outside Slate.\n");

        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        A11yEvent.VaultRescanIncomplete incomplete =
            Assert.IsType<A11yEvent.VaultRescanIncomplete>(Assert.Single(h.Events));
        Assert.Equal(1UL, incomplete.Errors);
        Assert.Equal([OneError], h.Spoken);

        fail = false;
        h.Now += TimeSpan.FromMinutes(1);
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
        Assert.Equal(2, h.Events.Count);
    });

    /// <summary>A vault whose one external modification reaches every
    /// awaited dependent: a reading-mode note embedding the modified note
    /// (a reading model), the history panel showing it, and the Bases and
    /// graph dependents every page notifies.</summary>
    private static Harness DependentsHarness(string label)
    {
        var h = new Harness(
            label,
            ("host.md", "# Host\n\n![[embedded]]\n"),
            ("embedded.md", "Embedded before.\n"));
        WorkspaceTabViewModel host = h.Open("host.md");
        host.ToggleViewMode();
        h.PumpUntil(
            () => ReadingText(host).Contains("Embedded before.", StringComparison.Ordinal),
            "the embed's first render");
        h.Workspace.History.NoteChanged("embedded.md");
        for (int turn = 0; turn < 20; turn++)
        {
            h.Context.Drain();
            PumpedDispatcher.Drain();
            Thread.Sleep(5);
        }

        h.Events.Clear();
        return h;
    }

    // ---------------------------------------------------------------------
    // Round 30: the tree's publication is awaited before the sentence
    // ---------------------------------------------------------------------

    /// <summary>Round 30: with the rescan's tree refresh parked ON ITS
    /// WORKER, the run holds — no sentence, the new file not yet in the
    /// tree; released, the tree publishes first and "Files refreshed"
    /// follows.</summary>
    [Fact]
    public void AParkedTreeRefreshHoldsTheSentence() => RunSta(() =>
    {
        using Harness h = Harness.WithAsyncTree("tree-parked", ("alpha.md", "# Alpha\n"));
        h.Context.RunUntil(() => !h.Sidebar.IsRefreshingTree, "the open's tree");
        using var parked = new ManualResetEventSlim(false);
        using var unpark = new ManualResetEventSlim(false);
        h.TreeWorker = (work, token) => Task.Run(
            () =>
            {
                parked.Set();
                _ = unpark.Wait(TimeSpan.FromSeconds(30));
                work();
            },
            token);
        h.Write("late.md", "# Late\n");

        Task run = h.Lifecycle.RescanAsync(RescanReason.Explicit);
        h.Context.RunUntil(() => parked.IsSet, "the tree refresh parking on its worker");
        h.Settle();

        Assert.False(run.IsCompleted);
        Assert.Empty(h.Events);
        Assert.DoesNotContain(h.Sidebar.RootNodes, node => node.Path == "late.md");

        unpark.Set();
        h.Context.Await(run);
        Assert.Contains(h.Sidebar.RootNodes, node => node.Path == "late.md");
        Assert.Equal([Explicit1], h.Spoken);
    });

    /// <summary>F2 (codex PR 7 design pass 2): a Slate write that lands
    /// after the rescan's tree snapshot was READ, but before it applies, is in
    /// the tree the run settles on — BEFORE its sentence. The write's event
    /// refreshes the tree at once while the rescan's tree publication is
    /// pending, so the stale snapshot fails its generation check; the
    /// ordinary 150 ms debounce would land after the sentence.</summary>
    [Fact]
    public void ASlateWriteDuringAParkedTreeApplyIsInTheTreeBeforeTheSentence() => RunSta(() =>
    {
        using Harness h = Harness.WithAsyncTree("tree-slate-write", ("alpha.md", "# Alpha\n"));
        h.Context.RunUntil(() => !h.Sidebar.IsRefreshingTree, "the open's tree");
        using var read = new ManualResetEventSlim(false);
        using var unpark = new ManualResetEventSlim(false);
        int treeReads = 0;
        h.TreeWorker = (work, token) => Interlocked.Increment(ref treeReads) == 1
            ? Task.Run(
                () =>
                {
                    // The rescan's snapshot is read, then parked before it
                    // applies.
                    work();
                    read.Set();
                    _ = unpark.Wait(TimeSpan.FromSeconds(30));
                },
                CancellationToken.None)
            : Task.Run(work, token);
        bool? writeShownAtTheSentence = null;
        h.AfterAnnounce = announced =>
        {
            if (announced is A11yEvent.VaultRescanFinished or A11yEvent.VaultRescanIncomplete)
            {
                writeShownAtTheSentence = h.Sidebar.RootNodes.Any(node => node.Path == "made.md");
            }
        };

        Task run = h.Lifecycle.RescanAsync(RescanReason.Explicit);
        h.Context.RunUntil(() => read.IsSet, "the rescan's tree snapshot");
        h.CreateInCore("made.md", "# Made\n");
        h.Context.RunUntil(() => h.QuickOpen.FilePathsForTests.Contains("made.md"), "the Slate write's event");
        unpark.Set();
        h.Context.Await(run);

        Assert.True(writeShownAtTheSentence, "the tree the sentence settled on lacked the Slate write");
        Assert.Equal([NoChanges], h.Spoken);
    });

    /// <summary>F5 (codex PR 7 design pass 2, mitigation): a caller's
    /// cancellation reaches the tree read's tag tree. Cancelled right after
    /// the native <c>TagTree</c> call returns, the refresh discards what it
    /// read — nothing publishes, nothing is spoken, the awaited Task is
    /// cancelled; cancelled before, the tag tree is never read.</summary>
    [Fact]
    public void ACancelAroundTheTagTreeCallPublishesNothing() => RunSta(() =>
    {
        using Harness h = Harness.WithAsyncTree("tags-cancel", ("alpha.md", "# Alpha\n"));
        h.Context.RunUntil(() => !h.Sidebar.IsRefreshingTree, "the open's tree");
        h.Write("late.md", "# Late\n\n#fresh\n");
        using (var indexing = new CancelToken())
        {
            // Indexed without the host hearing of it: a scan emits no event.
            _ = h.Lifecycle.SessionForTests!.Rescan(indexing);
        }

        using var cancellation = new CancellationTokenSource();
        int tagTreeReads = 0;
        h.Sidebar.AfterTagTreeForTests = () =>
        {
            _ = Interlocked.Increment(ref tagTreeReads);
            cancellation.Cancel();
        };

        Task refresh = h.Sidebar.RefreshAsync(cancellation: cancellation.Token);
        h.Context.RunUntil(() => refresh.IsCompleted, "the cancelled refresh");
        h.Settle();

        Assert.True(refresh.IsCanceled);
        Assert.Equal(1, Volatile.Read(ref tagTreeReads));
        Assert.DoesNotContain(h.Sidebar.RootNodes, node => node.Path == "late.md");
        Assert.DoesNotContain(h.Sidebar.Tags, tag => tag.Full == "fresh");
        Assert.Empty(h.Events);

        // Already cancelled: the tag tree is not read at all.
        Assert.True(h.Sidebar.RefreshAsync(cancellation: cancellation.Token).IsCanceled);
        h.Settle();
        Assert.Equal(1, Volatile.Read(ref tagTreeReads));

        // Control: an uncancelled refresh publishes both.
        h.Sidebar.AfterTagTreeForTests = null;
        h.Context.Await(h.Sidebar.RefreshAsync(), "the control refresh");
        Assert.Contains(h.Sidebar.RootNodes, node => node.Path == "late.md");
        Assert.Contains(h.Sidebar.Tags, tag => tag.Full == "fresh");
    });

    /// <summary>Round 30 and v2 §6: a tree refresh that FAILS makes the run
    /// say "results may be incomplete", never "Files refreshed" — and the
    /// rescan's own tree refresh is SILENT: its "Could not load files." is
    /// shown on the sidebar's status line, counted in the one sentence, never
    /// spoken. The next, independent rescan publishes the tree.</summary>
    [Fact]
    public void AFailedTreeRefreshSpeaksIncompleteAndTheNextRescanCompletesIt() => RunSta(() =>
    {
        using Harness h = Harness.WithAsyncTree("tree-fails", ("alpha.md", "# Alpha\n"));
        h.Context.RunUntil(() => !h.Sidebar.IsRefreshingTree, "the open's tree");
        bool fail = true;
        h.TreeWorker = (work, token) => Volatile.Read(ref fail)
            ? Task.FromException(new IOException("injected tree refresh failure"))
            : Task.Run(work, token);
        h.Write("late.md", "# Late\n");

        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        Assert.Equal([OneError], h.Spoken);
        Assert.StartsWith("Could not load files", h.Sidebar.Status, StringComparison.Ordinal);

        Volatile.Write(ref fail, false);
        h.Events.Clear();
        h.Now += TimeSpan.FromMinutes(1);
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
        Assert.Single(h.Events);
        Assert.Contains(h.Sidebar.RootNodes, node => node.Path == "late.md");
    });

    /// <summary>Round 28 (the interaction dependent): every open tab's editor
    /// interaction caches — its links, tasks and citations, the state the
    /// editor's link, task and citation actions act on — are dropped when a
    /// rescan changes the vault, so no action runs on a cache published
    /// before the change: the cache's generation advances (a fresh cache may
    /// already be republished for the new state by the time the run
    /// completes, the dispatcher pumped).</summary>
    [Fact]
    public void EditorInteractionCachesAreDroppedAfterARescan() => RunSta(() =>
    {
        using var h = new Harness("interaction-dependent", ("a.md", "# A\n\n[[late]]\n"));
        WorkspaceTabViewModel a = h.Open("a.md");
        EditorInteractionCoordinator interactions = Assert.IsType<EditorInteractionCoordinator>(a.EditorInteractions);
        interactions.RefreshArtifactCacheForTests();
        Assert.True(interactions.ArtifactCacheSourceCurrentForTests);
        int published = interactions.ArtifactCacheGenerationForTests;

        h.Write("late.md", "# Late\n");
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        Assert.True(
            interactions.ArtifactCacheGenerationForTests > published,
            "the interaction cache published before the change survived the rescan");
    });

    private static string[] BaseRows(WorkspaceTabViewModel tab) =>
        [.. (tab.Base?.Result?.Rows ?? []).Select(row => row.FilePath).Order(StringComparer.Ordinal)];

    /// <summary>Round 26: openability is core's. External .mdown and .MKD
    /// notes reach Quick Open through the re-sync — its list is core's
    /// openable-documents filter — a .txt file does not, and a deleted .mdown
    /// note leaves it again.</summary>
    [Fact]
    public void ExternalMdownAndMkdNotesReachQuickOpen() => RunSta(() =>
    {
        using var h = new Harness("mdown-mkd", ("alpha.md", "# Alpha\n"));
        h.Write("notes.mdown", "# Mdown\n");
        h.Write("more.MKD", "# Mkd\n");
        h.Write("data.txt", "text\n");

        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        Assert.Contains("notes.mdown", h.QuickOpen.FilePathsForTests);
        Assert.Contains("more.MKD", h.QuickOpen.FilePathsForTests);
        Assert.DoesNotContain("data.txt", h.QuickOpen.FilePathsForTests);
        Assert.Equal(["Files refreshed. 3 new or changed, 0 removed."], h.Spoken);

        h.Events.Clear();
        h.Delete("notes.mdown");
        h.Now += TimeSpan.FromMinutes(1);
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
        Assert.DoesNotContain("notes.mdown", h.QuickOpen.FilePathsForTests);
        Assert.Contains("more.MKD", h.QuickOpen.FilePathsForTests);
    });

    // ---------------------------------------------------------------------
    // Slate-owned writes supersede (round 23) and the re-check (round 24)
    // ---------------------------------------------------------------------

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
    // The re-sync's barriers (option B: v2 §1, §5, §6, §7, §9; F3, F6, F7)
    // ---------------------------------------------------------------------

    /// <summary>Parks the re-sync's Markdown read ON ITS WORKER, after the
    /// read and before its apply — the window a fact changes the tab in.</summary>
    private static (ManualResetEventSlim Parked, ManualResetEventSlim Release) ParkTheRead(Harness h)
    {
        var parked = new ManualResetEventSlim(false);
        var release = new ManualResetEventSlim(false);
        h.AfterCoreCall = operation =>
        {
            if (operation == "read")
            {
                parked.Set();
                _ = release.Wait(TimeSpan.FromSeconds(30));
            }
        };
        return (parked, release);
    }

    /// <summary>v2 §1 (codex design pass 1, critical 1): a transient tab
    /// reused IN PLACE for another note while the re-sync reads its old
    /// note is not reloaded with the old note's bytes — even when both notes
    /// had the same content hash (empty notes). The ticket carries the item
    /// and the tab's content generation; the new note stays as it is.</summary>
    [Fact]
    public void ATransientTabReusedDuringAParkedReadKeepsItsNewNote() => RunSta(() =>
    {
        using var h = new Harness("transient-aba", ("a.md", ""), ("b.md", ""));
        h.Workspace.OpenPath("a.md", WorkspaceOpenTarget.CurrentTab, fromSelection: true);
        h.Context.Drain();
        WorkspaceTabViewModel tab = Assert.Single(h.AllTabs, candidate => candidate.Path == "a.md");
        Assert.True(tab.IsTransient);
        string? emptyHash = tab.SavedContentHash;
        h.Write("a.md", "A, changed outside Slate\n");
        (ManualResetEventSlim parked, ManualResetEventSlim release) = ParkTheRead(h);
        using (parked)
        using (release)
        {
            h.Events.Clear();
            Task run = h.Lifecycle.RescanAsync(RescanReason.Explicit);
            h.Context.RunUntil(() => parked.IsSet, "the read parking");

            h.Workspace.OpenPath("b.md", WorkspaceOpenTarget.CurrentTab, fromSelection: true);
            h.Context.Drain();
            Assert.Same(tab, Assert.Single(h.AllTabs, candidate => candidate.Path == "b.md"));
            Assert.Equal(emptyHash, tab.SavedContentHash);

            release.Set();
            h.Context.Await(run);
        }

        Assert.Equal("b.md", tab.Path);
        Assert.Equal(string.Empty, tab.Text);
        Assert.False(tab.IsDirty);
        Assert.False(tab.IsExternallyStale);
        A11yEvent.VaultRescanFinished finished =
            Assert.Single(h.Events.OfType<A11yEvent.VaultRescanFinished>());
        Assert.Equal((1UL, 0UL), (finished.Changed, finished.Removed));
    });

    /// <summary>F3 (codex design pass 2): typing into a clean tab while the
    /// re-sync reads its changed file keeps the typing and marks the tab
    /// stale against the index hash — staleness is keyed by path and applied
    /// in the apply turn, separate from the reload, which only a full ticket
    /// earns.</summary>
    [Fact]
    public void TypingDuringAParkedReadKeepsTheEditAndMarksTheTabStale() => RunSta(() =>
    {
        using var h = new Harness("type-during-read", ("x.md", "x0\n"));
        WorkspaceTabViewModel x = h.Open("x.md");
        h.Write("x.md", "x1, changed outside Slate\n");
        (ManualResetEventSlim parked, ManualResetEventSlim release) = ParkTheRead(h);
        using (parked)
        using (release)
        {
            Task run = h.Lifecycle.RescanAsync(RescanReason.Explicit);
            h.Context.RunUntil(() => parked.IsSet, "the read parking");
            x.Text = "x0\nmine\n";
            Assert.True(x.IsDirty);
            release.Set();
            h.Context.Await(run);
        }

        Assert.Equal("x0\nmine\n", x.Text);
        Assert.True(x.IsDirty);
        Assert.True(x.IsExternallyStale, "the typed tab lost its stale mark");
        Assert.Equal([Explicit1], h.Spoken);
    });

    /// <summary>F3 (codex design pass 2): a tab re-baselined by ITS OWN SAVE
    /// while the re-sync reads is current — the ticket carries the baseline
    /// hash — so it is neither reloaded with the bytes the worker read nor
    /// marked stale.</summary>
    [Fact]
    public void ATabReBaselinedByItsOwnSaveDuringAParkedReadIsLeftCurrent() => RunSta(() =>
    {
        using var h = new Harness("own-save-during-read", ("x.md", "x0\n"));
        WorkspaceTabViewModel x = h.Open("x.md");
        h.Write("x.md", "x1, changed outside Slate\n");
        (ManualResetEventSlim parked, ManualResetEventSlim release) = ParkTheRead(h);
        using (parked)
        using (release)
        {
            Task run = h.Lifecycle.RescanAsync(RescanReason.Explicit);
            h.Context.RunUntil(() => parked.IsSet, "the read parking");

            // Another app puts the file back under the tab, and the tab saves
            // its own edit over it (its expected hash matches the disk).
            h.Write("x.md", "x0\n");
            x.Text = "mine\n";
            Assert.True(x.Save());
            h.Context.Drain();
            release.Set();
            h.Context.Await(run);
        }

        h.Settle();
        Assert.Equal("mine\n", x.Text);
        Assert.False(x.IsDirty);
        Assert.False(x.IsExternallyStale, "a tab re-baselined by its own save was marked stale");
        Assert.Equal("mine\n", File.ReadAllText(Path.Combine(h.Root, "x.md")));
    });

    /// <summary>The hash-conditioned read (owner ruling, option B): a tab
    /// never applies bytes whose hash differs from the index hash read for
    /// it. When the file moves again outside Slate after the re-sync read
    /// its index hash and before it reads the text, the clean tab keeps its
    /// text, is marked stale, and the run is honest: "1 error" — the next
    /// Refresh reloads it.</summary>
    [Fact]
    public void BytesTheIndexDoesNotVouchForAreNeverApplied() => RunSta(() =>
    {
        using var h = new Harness("unvouched-bytes", ("x.md", "x0\n"));
        WorkspaceTabViewModel x = h.Open("x.md");
        h.Write("x.md", "x1, indexed by the scan\n");
        bool moved = false;
        h.AfterCoreCall = operation =>
        {
            if (operation == "hashes" && !moved)
            {
                // After the index hashes were read, before the text is.
                moved = true;
                h.Write("x.md", "x2, not yet indexed\n");
            }
        };

        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        Assert.True(moved);
        Assert.Equal("x0\n", x.Text);
        Assert.False(x.IsDirty);
        Assert.True(x.IsExternallyStale);
        Assert.Equal([OneError], h.Spoken);

        h.AfterCoreCall = null;
        h.Events.Clear();
        h.Now += TimeSpan.FromMinutes(1);
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
        Assert.Equal("x2, not yet indexed\n", x.Text);
        Assert.False(x.IsExternallyStale);
        Assert.Equal([Explicit1], h.Spoken);
    });

    /// <summary>v2 §5 (codex design pass 1, high 5): the open paths are
    /// hashed in chunks of at most core's bound under the one token. With
    /// 1,025 distinct open notes — one more than a single call accepts — the
    /// note past the first chunk that changed outside Slate reloads, and the
    /// run is complete.</summary>
    [Fact]
    public void MoreOpenPathsThanOneHashCallAcceptsAreHashedInChunks() => RunSta(() =>
    {
        int count = checked((int)SlateUniffiMethods.MaxIndexedHashPaths()) + 1;
        (string Path, string Text)[] notes =
            [.. Enumerable.Range(0, count).Select(n => ($"n{n:0000}.md", $"{n}\n"))];
        using var h = new Harness("hash-chunks", notes);
        foreach ((string path, _) in notes)
        {
            h.Workspace.OpenPath(path, WorkspaceOpenTarget.NewTab);
        }

        h.Context.Drain();
        string last = notes[^1].Path;
        WorkspaceTabViewModel tail = Assert.Single(h.AllTabs, tab => tab.Path == last);
        Assert.Equal(count, h.AllTabs.Select(tab => tab.Path).Distinct().Count());
        h.Events.Clear();

        h.Write(last, "changed outside Slate\n");
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        Assert.Equal("changed outside Slate\n", tail.Text);
        Assert.Equal([Explicit1], h.Spoken);
    });

    /// <summary>v2 §7 (codex design pass 1, high 7): with Quick Open open,
    /// the re-sync's re-rank is SILENT — the rows stay while it ranks, no
    /// count is spoken — and the run completes only INSIDE that rank's
    /// publication: with the rank parked, the run holds and says nothing;
    /// released, the new note is listed and the one sentence follows.</summary>
    [Fact]
    public void AParkedQuickOpenRankHoldsTheSentence() => RunSta(() =>
    {
        using var h = new Harness("quickopen-parked", ("alpha.md", "# Alpha\n"));
        h.QuickOpen.Open();
        h.Context.RunUntil(() => !h.QuickOpen.IsRanking && h.QuickOpen.Results.Count > 0, "the open's rank");
        h.Events.Clear();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        h.QuickOpen.RankDelayForTests = _ => gate.Task;
        h.Write("late.md", "# Late\n");

        Task run = h.Lifecycle.RescanAsync(RescanReason.Explicit);
        h.Context.RunUntil(() => h.QuickOpen.IsRanking, "the re-sync's silent re-rank");
        h.Settle();

        Assert.False(run.IsCompleted);
        Assert.Empty(h.Events);
        Assert.Contains(h.QuickOpen.Results, row => row.Path == "alpha.md");

        gate.SetResult();
        h.Context.Await(run);
        Assert.Contains(h.QuickOpen.Results, row => row.Path == "late.md");
        Assert.Equal([Explicit1], h.Spoken);
    });

    /// <summary>F7 (codex design pass 2): Quick Open dismissed while the
    /// re-sync's rank is parked publishes no rank — and the rescan it holds
    /// is settled, not stalled: the one sentence follows.</summary>
    [Fact]
    public void DismissingQuickOpenWithARankParkedDoesNotStallTheRescan() => RunSta(() =>
    {
        using var h = new Harness("quickopen-dismissed", ("alpha.md", "# Alpha\n"));
        h.QuickOpen.Open();
        h.Context.RunUntil(() => !h.QuickOpen.IsRanking && h.QuickOpen.Results.Count > 0, "the open's rank");
        var never = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        h.QuickOpen.RankDelayForTests = token => never.Task.WaitAsync(token);
        h.Write("late.md", "# Late\n");
        h.Events.Clear();

        Task run = h.Lifecycle.RescanAsync(RescanReason.Explicit);
        h.Context.RunUntil(() => h.QuickOpen.IsRanking, "the re-sync's silent re-rank");
        h.QuickOpen.Dismiss();
        h.Context.Await(run);

        Assert.Contains(Explicit1, h.Spoken);
        Assert.Contains("late.md", h.QuickOpen.FilePathsForTests);
    });

    /// <summary>F7 (codex design pass 2): a vault close while the re-sync's
    /// rank is parked disposes the switcher, which settles the waiter, and
    /// cancels the run's token: the run ends silently.</summary>
    [Fact]
    public void AVaultCloseWithARankParkedEndsTheRescanSilently() => RunSta(() =>
    {
        using var h = new Harness("quickopen-closed", ("alpha.md", "# Alpha\n"));
        h.QuickOpen.Open();
        h.Context.RunUntil(() => !h.QuickOpen.IsRanking && h.QuickOpen.Results.Count > 0, "the open's rank");
        var never = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        h.QuickOpen.RankDelayForTests = token => never.Task.WaitAsync(token);
        h.Write("late.md", "# Late\n");
        h.Events.Clear();

        Task run = h.Lifecycle.RescanAsync(RescanReason.Explicit);
        h.Context.RunUntil(() => h.QuickOpen.IsRanking, "the re-sync's silent re-rank");
        h.Lifecycle.CloseVault();
        h.Context.Await(run, "the closed rescan");

        Assert.DoesNotContain(h.Events, e => e is A11yEvent.VaultRescanFinished or A11yEvent.VaultRescanIncomplete);
    });

    /// <summary>v2 §6 (codex design pass 1, high 6), the tag arm: a tag-tree
    /// failure during the rescan's own tree refresh is shown on the
    /// sidebar's status line and counted — "1 error" — never spoken on its
    /// own. The root list's arm is <see cref="AFailedTreeRefreshSpeaksIncompleteAndTheNextRescanCompletesIt"/>.</summary>
    [Fact]
    public void ATagTreeFailureIsCountedSilently() => RunSta(() =>
    {
        using var h = new Harness("tags-fail", ("alpha.md", "# Alpha\n\n#topic\n"));
        h.Sidebar.AfterTagTreeForTests = () => throw new VaultException.Io("injected tag tree failure");

        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        Assert.Equal([OneError], h.Spoken);
        Assert.StartsWith("Could not load tags", h.Sidebar.Status, StringComparison.Ordinal);

        h.Sidebar.AfterTagTreeForTests = null;
        h.Events.Clear();
        h.Now += TimeSpan.FromMinutes(1);
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
        Assert.Equal([NoChanges], h.Spoken);
        Assert.Contains(h.Sidebar.Tags, tag => tag.Full == "topic");
    });

    /// <summary>F6 (codex design pass 2): a dashboard open in this window
    /// that another session deletes is reloaded by the re-sync; its failure
    /// state is a publication (round 29) and its D-12 line speaks from its
    /// load as on main — then the one completion sentence.</summary>
    [Fact]
    public void AnotherSessionDeletingAnOpenDashboardSpeaksItsLineThenTheSentence() => RunSta(() =>
    {
        using var h = new Harness("dashboard-deleted", ("a.md", "# A\n"));
        string id = h.Lifecycle.SessionForTests!.SaveDashboard("Reading", []);
        h.Workspace.OpenDashboard(id, "Reading");
        WorkspaceTabViewModel tab = h.AllTabs.Last();
        h.PumpUntil(() => tab.Dashboard is { Name: "Reading" }, "the dashboard's first load");
        h.Settle();
        h.Events.Clear();
        using (VaultSession other = VaultSession.OpenFilesystem(h.Root))
        {
            other.DeleteDashboard(id);
        }

        Task run = h.Lifecycle.RescanAsync(RescanReason.Explicit);
        h.PumpUntil(() => run.IsCompleted, "the rescan");

        Assert.Collection(
            h.Events,
            first => Assert.IsType<A11yEvent.BasesDashboardLoadFailed>(first),
            second => Assert.IsType<A11yEvent.VaultRescanFinished>(second));
        Assert.Equal(NoChanges, h.Spoken[1]);
    });

    /// <summary>AR-30 (owner-accepted): a Refresh's counts are ITS OWN
    /// scan's. Another Slate session — another window, slate-cli — that
    /// modifies, creates and deletes files has already indexed them, so this
    /// window's re-sync SHOWS all three — the open tab reloads, the deleted
    /// note's tab is marked missing, the tree and Quick Open follow — while
    /// an explicit Refresh says "Files refreshed. No changes." and a
    /// foreground rescan stays silent.</summary>
    [Fact]
    public void AnotherSessionsChangesAreShownButNotCounted() => RunSta(() =>
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

        Assert.Equal([NoChanges], h.Spoken);
        Assert.Equal("changed by another window\n", mod.Text);
        Assert.False(mod.IsDirty);
        Assert.True(gone.IsMissingFromDisk);
        Assert.Contains(h.Sidebar.RootNodes, node => node.Path == "new.md");
        Assert.DoesNotContain(h.Sidebar.RootNodes, node => node.Path == "gone.md");
        Assert.Contains("new.md", h.QuickOpen.FilePathsForTests);
        Assert.DoesNotContain("gone.md", h.QuickOpen.FilePathsForTests);

        using (VaultSession other = VaultSession.OpenFilesystem(h.Root))
        {
            _ = other.SaveText("mod.md", "and again\n", null);
        }

        h.Events.Clear();
        h.Now += TimeSpan.FromMinutes(1);
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Foreground));
        Assert.Empty(h.Events);
        Assert.Equal("and again\n", mod.Text);
    });

    /// <summary>AR-26 (v2 §9), the two-refresh fact: a file whose read fails
    /// blocks the prune, so an open note deleted outside Slate keeps its
    /// index row — the first Refresh is incomplete, and the tree, Quick Open
    /// and the note's tab still show it (not missing); once the file reads
    /// again, the next, clean Refresh removes it from all three.</summary>
    [Fact]
    public void AFailedReadBlocksThePruneUntilTheNextCleanRefresh() => RunSta(() =>
    {
        using var h = new Harness("ar26-two-refresh", ("gone.md", "gone\n"), ("locked.md", "locked\n"));
        WorkspaceTabViewModel gone = h.Open("gone.md");
        h.Delete("gone.md");
        h.Write("locked.md", "locked, changed\n");
        using (DenyAccess.To(Path.Combine(h.Root, "locked.md"), FileSystemRights.ReadData))
        {
            h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
        }

        Assert.Equal([OneError], h.Spoken);
        Assert.False(gone.IsMissingFromDisk, "the blocked prune still removed the row");
        Assert.Contains(h.Sidebar.RootNodes, node => node.Path == "gone.md");
        Assert.Contains("gone.md", h.QuickOpen.FilePathsForTests);

        h.Events.Clear();
        h.Now += TimeSpan.FromMinutes(1);
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        Assert.Equal(["Files refreshed. 1 new or changed, 1 removed."], h.Spoken);
        Assert.True(gone.IsMissingFromDisk);
        Assert.DoesNotContain(h.Sidebar.RootNodes, node => node.Path == "gone.md");
        Assert.DoesNotContain("gone.md", h.QuickOpen.FilePathsForTests);
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

        // A window that lost the foreground before it ever had it (it
        // opened behind another) forwards nothing on its FIRST activation
        // either, however long it was away: the open scan just ran.
        var openedBehind = new ForegroundRescanRoute(
            reason =>
            {
                forwarded.Add(reason);
                return Task.CompletedTask;
            },
            () => false,
            () => now);
        openedBehind.OnDeactivated();
        now += ForegroundRescanRoute.MinimumAway + ForegroundRescanRoute.MinimumAway;
        Assert.Null(openedBehind.OnActivated());

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

        /// <summary>Drain this queue AND the fact thread's WPF dispatcher
        /// until the condition holds: a rescan awaits publications that
        /// land on the dispatcher (round 29 — the Connections leaf and the
        /// graph document apply there, as every surface does in the shell).</summary>
        public void RunUntil(Func<bool> condition, string what)
        {
            var clock = Stopwatch.StartNew();
            while (!condition())
            {
                Drain();
                PumpedDispatcher.Drain();
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

    /// <summary>The rescan core seam for the facts: every call runs through
    /// the PRODUCTION seam (the pool), recorded with its thread, and a scan
    /// may throw or park (<see cref="Harness.ThrowOnCall"/>,
    /// <see cref="Harness.ParkAfterCall"/>, numbered with the open's session
    /// load).</summary>
    internal sealed class RecordingRescanWorker(Harness harness) : IRescanCoreWorker
    {
        public Task<T> Run<T>(string operation, Func<T> call)
        {
            bool park = false;
            if (operation == "scan")
            {
                int number = harness.NextScanCall();
                if (harness.ThrowOnCall?.Invoke(number) is Exception fault)
                {
                    return Task.FromException<T>(fault);
                }

                park = harness.ParkAfterCall == number;
            }

            return ThreadPoolRescanCoreWorker.Instance.Run(operation, () =>
            {
                harness.RecordCoreCall(operation);
                T result = call();
                if (result is ScanReport report)
                {
                    harness.LastRescanReport = report;
                }
                harness.AfterCoreCall?.Invoke(operation);
                if (park)
                {
                    harness.Parked.Set();
                    _ = harness.Release.Wait(TimeSpan.FromSeconds(30));
                }

                return result;
            });
        }
    }

    /// <summary>A vault on disk, a lifecycle over it with every seam pointed
    /// at this fact, and the open already drained.</summary>
    internal sealed class Harness : IDisposable
    {
        private readonly SynchronizationContext? _previous;
        private int _scanCalls;
        private readonly List<Action> _heldUiActions = [];
        private bool _holdUiQueue;

        public Harness(string label, params (string Path, string Text)[] files)
            : this(NewRoot(label), files, null)
        {
        }

        public Harness(
            string label,
            Func<Task<IReadOnlyList<string>>>? pickImportSources,
            (string Path, string Text)[]? files = null)
            : this(NewRoot(label), files ?? [], pickImportSources)
        {
        }

        private Harness(
            string root,
            (string Path, string Text)[] files,
            Func<Task<IReadOnlyList<string>>>? pickImportSources,
            bool asyncTree = false)
        {
            Root = root;
            Seed(root, files);
            _previous = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(Context);
            Lifecycle = new VaultLifecycleViewModel(
                pickVault: () => Task.FromResult<string?>(Root),
                enqueueUi: EnqueueUi,
                recentVaultsStore: new RecentVaultsStore(Path.Combine(Root + "-device", "recent-vaults.json")),
                announce: Announce,
                pickImportSources: pickImportSources,
                scanClock: () => Now,
                sessionLoadWorker: RunOnWorker<(ScanReport Report, SwitcherFile[] SwitcherFiles)>,
                rescanWorker: new RecordingRescanWorker(this),
                treeUiContext: asyncTree ? Context : null,
                treeWorker: asyncTree ? RunTreeWorker : null);
            Context.Await(Lifecycle.OpenVaultAsync(Root), "the vault open");
            OpenEvents = [.. Events];
            OpenStatusText = Lifecycle.StatusText;
            Events.Clear();
            // Well past the open scan: the foreground cooldown counts from it.
            Now += TimeSpan.FromMinutes(1);
        }

        /// <summary>Every rescan core call's operation, on its worker, after
        /// the call returned and before its continuation is queued.</summary>
        public Action<string>? AfterCoreCall { get; set; }

        /// <summary>While set, what the lifecycle enqueues for the UI —
        /// file-change events, progress — is held instead of posted (the
        /// rescan's own continuations are not: they resume on the
        /// context).</summary>
        public bool HoldUiQueue
        {
            get => Volatile.Read(ref _holdUiQueue);
            set => Volatile.Write(ref _holdUiQueue, value);
        }

        public int HeldUiActions
        {
            get
            {
                lock (_heldUiActions)
                {
                    return _heldUiActions.Count;
                }
            }
        }

        /// <summary>Stop holding, and post everything held, in order.</summary>
        public void ReleaseUiQueue()
        {
            Action[] held;
            lock (_heldUiActions)
            {
                HoldUiQueue = false;
                held = [.. _heldUiActions];
                _heldUiActions.Clear();
            }

            foreach (Action action in held)
            {
                Context.Post(_ => action(), null);
            }
        }

        /// <summary>A while of turns — this queue and the WPF dispatcher
        /// both drained — for a fact that asserts nothing happened.</summary>
        public void Settle()
        {
            for (int turn = 0; turn < 20; turn++)
            {
                Context.Drain();
                PumpedDispatcher.Drain();
                Thread.Sleep(5);
            }
        }

        private void EnqueueUi(Action action)
        {
            lock (_heldUiActions)
            {
                if (HoldUiQueue)
                {
                    _heldUiActions.Add(action);
                    return;
                }
            }

            Context.Post(_ => action(), null);
        }

        public static Harness OpenExisting(string root) =>
            new(root, [], null);

        /// <summary>A harness whose sidebar refreshes its tree the way the
        /// shell does — on a worker (<see cref="TreeWorker"/>, the pool by
        /// default), published on this fact's context — rather than inline.</summary>
        public static Harness WithAsyncTree(string label, params (string Path, string Text)[] files) =>
            new(NewRoot(label), files, null, asyncTree: true);

        /// <summary>The asynchronous tree's worker (<see cref="WithAsyncTree"/>):
        /// null runs each tree read on the pool.</summary>
        public Func<Action, CancellationToken, Task>? TreeWorker { get; set; }

        private Task RunTreeWorker(Action work, CancellationToken token) =>
            TreeWorker is { } worker ? worker(work, token) : Task.Run(work, token);

        public SerialContext Context { get; } = new();

        public string Root { get; }

        public List<A11yEvent> Events { get; } = [];

        /// <summary>Runs with every announcement, as it is made — the
        /// moment a fact samples what the window shows when a sentence
        /// is spoken.</summary>
        public Action<A11yEvent>? AfterAnnounce { get; set; }

        private void Announce(A11yEvent announced)
        {
            Events.Add(announced);
            AfterAnnounce?.Invoke(announced);
        }

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

        /// <summary>The open scan's report as it crossed the FFI.</summary>
        public ScanReport? OpenReport { get; private set; }

        /// <summary>The last rescan's report as it crossed the FFI.</summary>
        public ScanReport? LastRescanReport { get; set; }

        public WorkspaceViewModel Workspace => Lifecycle.Workspace!;

        public FilesSidebarViewModel Sidebar => Lifecycle.FileSidebar!;

        public QuickSwitcherViewModel QuickOpen => Lifecycle.QuickSwitcher!;

        public IEnumerable<WorkspaceTabViewModel> AllTabs =>
            Workspace.Groups.SelectMany(group => group.Tabs);

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

        /// <summary>Drain the lifecycle's queue AND the WPF dispatcher (the
        /// canvas, base and reading documents publish there) until the
        /// condition holds.</summary>
        public void PumpUntil(Func<bool> condition, string what)
        {
            var clock = Stopwatch.StartNew();
            while (!condition())
            {
                Context.Drain();
                PumpedDispatcher.Drain();
                if (clock.Elapsed > TimeSpan.FromSeconds(30))
                {
                    throw new TimeoutException($"{what} never happened");
                }

                Thread.Sleep(5);
            }
        }

        /// <summary>A Slate-owned create through the session the host holds
        /// — the core call the host's create commands make — and its
        /// Created event handled through the lifecycle's real listener.</summary>
        public void CreateThroughSlate(string path, string text)
        {
            _ = Lifecycle.SessionForTests!.CreateExclusive(path, text);
            Context.Drain();
        }

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

        /// <summary>The next scan's 1-based number (the open's session load
        /// is call 1).</summary>
        public int NextScanCall() => Interlocked.Increment(ref _scanCalls);

        /// <summary>Every rescan core call the lifecycle made through its
        /// seam: the operation, its thread, and whether that was a pool
        /// thread.</summary>
        public ConcurrentQueue<(string Operation, int Thread, bool Pool)> CoreCalls { get; } = new();

        public void RecordCoreCall(string operation) => CoreCalls.Enqueue(
            (operation, Environment.CurrentManagedThreadId, Thread.CurrentThread.IsThreadPoolThread));

        /// <summary>The open's session load: call <see cref="ScanCalls"/>
        /// 1 may throw or park after its work.</summary>
        private Task<T> RunOnWorker<T>(Func<T> load)
        {
            Func<T> work = () =>
            {
                T loaded = load();
                if (loaded is ValueTuple<ScanReport, SwitcherFile[]> open)
                {
                    OpenReport = open.Item1;
                }

                return loaded;
            };
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
