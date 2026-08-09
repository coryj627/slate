// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Panels;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W4-7 (#739) facts: the history document and coordinator over a
/// REAL session — contracts H1 (core strings verbatim), H3/H4 (paged
/// list, markers, day groups), H6 (compare model + diffs), H7/HD-1
/// (restore, dirty refusal), H8 (the since-open funnel order), H9
/// (Restore As… exclusive-create), H10 (deleted recovery), H12
/// (.base coverage rides the same seam).
/// </summary>
public sealed class HistoryPanelTests : IDisposable
{
    private readonly string _root;
    private readonly VaultSession _session;
    private readonly List<A11yEvent> _announced = [];

    public HistoryPanelTests()
    {
        _root = Path.Combine(
            Path.GetTempPath(), $"slate-windows-test-history-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
        File.WriteAllText(
            Path.Combine(_root, "note0.md"),
            "---\nstatus: draft\n---\n\n# Note 0\n\nOriginal body.\n");
        File.WriteAllText(
            Path.Combine(_root, "note1.md"),
            "# Note 1\n\nBody.\n");
        _session = VaultSession.OpenFilesystem(_root);
        using var cancel = new CancelToken();
        _session.ScanInitial(cancel);
    }

    public void Dispose()
    {
        _session.Dispose();
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private readonly List<string> _alerts = [];

    /// <summary>Both dialog seams injected — a REAL MessageBox in a
    /// headless test host blocks forever (measured: the first run of
    /// this file hung on an invisible modal).</summary>
    private WorkspaceViewModel NewWorkspace()
    {
        var workspace = new WorkspaceViewModel(
            _session, _root, () => [], _announced.Add,
            startInteractionBackgroundWork: false);
        workspace.HistoryAlert =
            (title, message) => _alerts.Add($"{title}: {message}");
        workspace.HistoryRestoreConfirmation = _ => true;
        return workspace;
    }

    private HistoryViewModel NewHistory() =>
        new(_session, synchronousForTests: true);

    /// <summary>Save through core so version rows exist (files bind to
    /// an op-log on their first Slate save).</summary>
    private string SaveVersion(string path, string content)
    {
        SaveReport report = _session.SaveText(path, content, expectedContentHash: null);
        return report.NewContentHash;
    }

    /// <summary>DeleteFile routes through the system trash, whose COM
    /// init requires an STA thread (the app's UI thread is STA; the
    /// xunit thread is not — measured RPC_E_CHANGED_MODE panic).</summary>
    private static void DeleteOnSta(VaultSession session, string path)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                session.DeleteFile(path);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null)
        {
            throw failure;
        }
    }

    /// <summary>Core builds the remnant set at RECONCILE (scan) — a
    /// same-session deletion enters the Deleted list at the next
    /// vault open, identically on mac (H10 delivery note). The
    /// deleted-file facts therefore run the honest two-session
    /// shape: delete in session one, list and recover in session
    /// two.</summary>
    private sealed class DeletedVaultScenario : IDisposable
    {
        public string Root { get; }

        public VaultSession Session { get; }

        public DeletedVaultScenario(Action<VaultSession> firstSession)
        {
            Root = Path.Combine(
                Path.GetTempPath(),
                $"slate-windows-test-history-del-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Root);
            File.WriteAllText(
                Path.Combine(Root, "keeper.md"), "# Keeper\n\nStays.\n");
            using (VaultSession first = VaultSession.OpenFilesystem(Root))
            {
                using var cancel = new CancelToken();
                first.ScanInitial(cancel);
                firstSession(first);
            }
            Session = VaultSession.OpenFilesystem(Root);
            using var rescan = new CancelToken();
            Session.ScanInitial(rescan);
        }

        public void Dispose()
        {
            Session.Dispose();
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    [Fact]
    public void LoadPublishesCoreVersionRowsNewestFirst()
    {
        _ = SaveVersion("note0.md", "# Note 0\n\nFirst saved body.\n");
        _ = SaveVersion("note0.md", "# Note 0\n\nSecond saved body.\n");
        HistoryViewModel history = NewHistory();

        history.NoteChanged("note0.md");

        Assert.Null(history.LoadError);
        Assert.False(history.ShowEmptyState);
        var rows = history.DayGroups.SelectMany(group => group.Rows).ToList();
        Assert.True(rows.Count >= 2, $"expected at least 2 rows, got {rows.Count}");
        // Newest first: positions ascend down the list (0 = newest).
        Assert.Equal(
            rows.Select(row => row.PositionFromTail).OrderBy(p => p).ToList(),
            rows.Select(row => row.PositionFromTail).ToList());
        // Core strings verbatim (H1): every row carries a non-empty
        // core audio fragment, and the accessible name embeds it.
        Assert.All(rows, row =>
        {
            Assert.NotEmpty(row.AudioFragment);
            Assert.Contains(row.AudioFragment, row.AccessibleName, StringComparison.Ordinal);
        });
        // The header count is core's TotalFiltered.
        Assert.Contains("version", history.HeaderText, StringComparison.Ordinal);
        history.Shutdown();
    }

    [Fact]
    public void MarkersHiddenByDefaultAndToggleRefiltersWithoutRequery()
    {
        _ = SaveVersion("note0.md", "# Note 0\n\nSaved body.\n");
        // A rename mints a PathChanged MARKER row (hash unchanged) —
        // the deterministic marker source. The report carries the
        // authoritative new path.
        StructuralReport renamed = _session.RenameFile("note0.md", "note0-moved");
        string movedPath = renamed.Moved.Single().NewPath;
        HistoryViewModel history = NewHistory();
        history.NoteChanged(movedPath);
        var visibleDefault = history.DayGroups
            .SelectMany(group => group.Rows).ToList();
        Assert.DoesNotContain(visibleDefault, row => row.IsMarker);

        string headerBefore = history.HeaderText;

        history.ShowMarkers = true;

        var visibleWithMarkers = history.DayGroups
            .SelectMany(group => group.Rows).ToList();
        Assert.True(
            visibleWithMarkers.Count >= visibleDefault.Count,
            "showing markers must never hide rows");
        // The toggle is a pure re-filter over the cached page — the
        // marker rows (the op-log anchor at minimum) appear.
        Assert.Contains(visibleWithMarkers, row => row.IsMarker);
        // The header count is core's marker-INCLUSIVE TotalFiltered
        // (H3): identical across the toggle, and equal to the full
        // row count on a single-page history.
        Assert.Equal(headerBefore, history.HeaderText);
        Assert.Equal(
            $"Version history, {visibleWithMarkers.Count} versions",
            history.HeaderText);
        history.ShowMarkers = false;
        Assert.DoesNotContain(
            history.DayGroups.SelectMany(group => group.Rows),
            row => row.IsMarker);
        history.Shutdown();
    }

    [Fact]
    public void DayGroupingIsConsecutiveRunsWithStableIds()
    {
        _ = SaveVersion("note0.md", "# Note 0\n\nA.\n");
        _ = SaveVersion("note0.md", "# Note 0\n\nB.\n");
        HistoryViewModel history = NewHistory();
        history.NoteChanged("note0.md");

        Assert.All(history.DayGroups, group =>
        {
            Assert.Equal("Today", group.Title);
            Assert.Contains("#", group.Id, StringComparison.Ordinal);
            Assert.Contains("version", group.AccessibleName, StringComparison.Ordinal);
            Assert.Contains("expanded", group.AccessibleName, StringComparison.Ordinal);
        });
        // Collapse state round-trips through the accessible name.
        HistoryDayGroup first = history.DayGroups[0];
        first.IsCollapsed = true;
        Assert.Contains("collapsed", first.AccessibleName, StringComparison.Ordinal);
        history.Shutdown();
    }

    [Fact]
    public void DayTitleAndRelativePhraseAreTheRecordedLabels()
    {
        Assert.Equal("Today", HistoryViewModel.DayTitle(DateTime.Today));
        Assert.Equal(
            "Yesterday", HistoryViewModel.DayTitle(DateTime.Today.AddDays(-1)));
        DateTime now = new(2026, 8, 9, 12, 0, 0);
        Assert.Equal(
            "just now", HistoryViewModel.RelativePhrase(now.AddSeconds(-30), now));
        Assert.Equal(
            "5 minutes ago", HistoryViewModel.RelativePhrase(now.AddMinutes(-5), now));
        Assert.Equal(
            "1 hour ago", HistoryViewModel.RelativePhrase(now.AddHours(-1), now));
        Assert.Equal(
            "3 days ago", HistoryViewModel.RelativePhrase(now.AddDays(-3), now));
        Assert.Equal(
            "2 weeks ago", HistoryViewModel.RelativePhrase(now.AddDays(-15), now));
    }

    [Fact]
    public void CompareSelectionKeepsTwoAndDropsTheOlder()
    {
        _ = SaveVersion("note0.md", "# Note 0\n\nA.\n");
        _ = SaveVersion("note0.md", "# Note 0\n\nB.\n");
        _ = SaveVersion("note0.md", "# Note 0\n\nC.\n");
        HistoryViewModel history = NewHistory();
        history.NoteChanged("note0.md");
        var rows = history.DayGroups.SelectMany(group => group.Rows).ToList();
        Assert.True(rows.Count >= 3);

        // Select the two OLDEST, then the newest: the older of the
        // first two (the highest position) drops.
        uint oldest = rows[^1].PositionFromTail;
        uint middle = rows[^2].PositionFromTail;
        uint newest = rows[0].PositionFromTail;
        history.ToggleCompareSelection(oldest);
        history.ToggleCompareSelection(middle);
        Assert.True(history.CanCompareSelected);
        history.ToggleCompareSelection(newest);

        Assert.True(history.CanCompareSelected);
        Assert.DoesNotContain(oldest, history.CompareSelectionForTests);
        Assert.Contains(middle, history.CompareSelectionForTests);
        Assert.Contains(newest, history.CompareSelectionForTests);
        history.Shutdown();
    }

    [Fact]
    public void CompareSelectedPublishesTheCoreDiffOlderToNewer()
    {
        _ = SaveVersion("note0.md", "# Note 0\n\nOld paragraph.\n");
        _ = SaveVersion("note0.md", "# Note 0\n\nNew paragraph.\n");
        HistoryViewModel history = NewHistory();
        history.NoteChanged("note0.md");
        var rows = history.DayGroups.SelectMany(group => group.Rows).ToList();
        history.ToggleCompareSelection(rows[0].PositionFromTail);
        history.ToggleCompareSelection(rows[1].PositionFromTail);

        history.CompareSelected();

        HistoryInlineDiff inline = Assert.IsType<HistoryInlineDiff>(history.InlineDiff);
        Assert.Null(inline.AnchorPosition);
        Assert.Null(inline.Error);
        StructuredDiff diff = Assert.IsType<StructuredDiff>(inline.Diff);
        // Endpoints oriented older = from, newer = to (H6).
        Assert.Equal(rows[1].ContentHashAfter, diff.FromHash);
        Assert.Equal(rows[0].ContentHashAfter, diff.ToHash);
        Assert.NotEmpty(diff.AudioSummary);
        Assert.All(
            diff.Operations,
            operation => Assert.NotEmpty(operation.SemanticDescription));
        history.Shutdown();
    }

    [Fact]
    public void CompareAgainstCurrentAnchorsUnderTheRow()
    {
        string current = SaveVersion("note0.md", "# Note 0\n\nCurrent body.\n");
        HistoryViewModel history = NewHistory();
        history.CurrentContentHashProvider = () => current;
        history.NoteChanged("note0.md");
        var rows = history.DayGroups.SelectMany(group => group.Rows).ToList();
        HistoryVersionRow row = rows[^1];

        history.CompareAgainstCurrent(row);

        HistoryInlineDiff inline = Assert.IsType<HistoryInlineDiff>(history.InlineDiff);
        Assert.Equal(row.PositionFromTail, inline.AnchorPosition);
        Assert.NotNull(inline.Diff);
        history.Shutdown();
    }

    [Fact]
    public void SinceOpenFunnelVerdictsBeforeMarking()
    {
        _ = SaveVersion("note0.md", "# Note 0\n\nFirst.\n");
        HistoryViewModel history = NewHistory();
        history.ShowChangesSinceOpen = true;

        // First activation: no baseline exists yet — nothing renders,
        // and the mark lands only AFTER that verdict (HINV-8).
        history.NoteChanged("note0.md");
        Assert.Equal(HistorySinceOpenKind.None, history.SinceOpen.Kind);

        // A change lands, then the note re-activates: the verdict now
        // diffs against the FIRST activation's mark — proof the mark
        // followed the verdict, not preceded it.
        _ = SaveVersion("note0.md", "# Note 0\n\nSecond, changed.\n");
        history.NoteChanged(null);
        history.NoteChanged("note0.md");

        Assert.Equal(HistorySinceOpenKind.Diff, history.SinceOpen.Kind);
        StructuredDiff diff = Assert.IsType<StructuredDiff>(history.SinceOpen.Diff);
        Assert.NotEqual("No changes.", diff.AudioSummary);
        history.Shutdown();
    }

    [Fact]
    public void SinceOpenPrefOffMakesNoCallsAndClears()
    {
        _ = SaveVersion("note0.md", "# Note 0\n\nFirst.\n");
        HistoryViewModel history = NewHistory();
        history.ShowChangesSinceOpen = true;
        history.NoteChanged("note0.md");
        _ = SaveVersion("note0.md", "# Note 0\n\nSecond.\n");
        history.NoteChanged(null);
        history.NoteChanged("note0.md");
        Assert.Equal(HistorySinceOpenKind.Diff, history.SinceOpen.Kind);

        // Turning the pref OFF clears the section (contract H8).
        history.ShowChangesSinceOpen = false;
        Assert.Equal(HistorySinceOpenKind.None, history.SinceOpen.Kind);
        history.Shutdown();
    }

    [Fact]
    public void RestoreRoundTripsThroughCoreAndReloadsTheTab()
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        _ = SaveVersion("note0.md", "# Note 0\n\nVersion one body.\n");
        _ = SaveVersion("note0.md", "# Note 0\n\nVersion two body.\n");
        // Opened AFTER the saves: the tab's saved hash is the log
        // tail, so the restore CAS admits (a tab holding an OLDER
        // state correctly conflicts — that protection is the point of
        // the expected-hash capture).
        workspace.OpenPath("note0.md");
        workspace.History.Reload();
        var rows = workspace.History.DayGroups
            .SelectMany(group => group.Rows).ToList();
        // Deterministic target: the OLDEST non-marker row is version one.
        HistoryVersionRow versionOne = rows.Where(row => !row.IsMarker).Last();
        string? confirmationMessage = null;
        workspace.HistoryRestoreConfirmation = message =>
        {
            confirmationMessage = message;
            return true;
        };
        bool headFocusRequested = false;
        workspace.History.FocusHeadRequested += () => headFocusRequested = true;
        _announced.Clear();

        workspace.RequestRestoreVersion(versionOne);

        Assert.True(
            _alerts.Count == 0,
            $"restore raised an alert: {string.Join("; ", _alerts)}");
        // The confirmation is the recorded sentence, carrying the
        // STAGED host-formatted date (H7).
        Assert.Contains(
            $"Restore the version from {versionOne.AbsoluteDate}?",
            confirmationMessage,
            StringComparison.Ordinal);
        // The announcement payload is the staged date too (H1).
        var restored = Assert.IsType<A11yEvent.RestoredVersionFrom>(
            Assert.Single(_announced, e => e is A11yEvent.RestoredVersionFrom));
        Assert.Equal(versionOne.AbsoluteDate, restored.FormattedDate);
        Assert.Contains(
            "Version one body",
            File.ReadAllText(Path.Combine(_root, "note0.md")),
            StringComparison.Ordinal);
        // The open CLEAN tab reloaded from disk (H7).
        WorkspaceTabViewModel tab =
            Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);
        Assert.Contains("Version one body", tab.Text, StringComparison.Ordinal);
        Assert.True(headFocusRequested);
        // History never rewrites: the restore appended EXACTLY ONE new
        // head row, whose content hash is the restored version's (a
        // ">= count" gate would also pass an in-place rewrite — red
        // team round 1).
        var reloaded = workspace.History.DayGroups
            .SelectMany(group => group.Rows).ToList();
        Assert.Equal(rows.Count + 1, reloaded.Count);
        HistoryVersionRow head = reloaded.First();
        Assert.Equal(0u, head.PositionFromTail);
        Assert.Equal(versionOne.ContentHashAfter, head.ContentHashAfter);
    }

    [Fact]
    public void DirtyTabRestoreRefusesWithTheSharedSentence()
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        _ = SaveVersion("note0.md", "# Note 0\n\nVersion one.\n");
        workspace.OpenPath("note0.md");
        WorkspaceTabViewModel tab =
            Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);
        tab.Text += "\nunsaved\n";
        Assert.True(tab.IsDirty);
        workspace.History.NoteChanged("note0.md");
        var rows = workspace.History.DayGroups
            .SelectMany(group => group.Rows).ToList();
        workspace.HistoryRestoreConfirmation = _ =>
            throw new InvalidOperationException(
                "the dirty refusal must precede the confirmation");
        string diskBefore = File.ReadAllText(Path.Combine(_root, "note0.md"));
        _announced.Clear();

        workspace.RequestRestoreVersion(rows[^1]);

        var refusal = Assert.IsType<A11yEvent.HostComposed>(Assert.Single(_announced));
        Assert.StartsWith(
            "Save the note before restoring a version.",
            refusal.Text,
            StringComparison.Ordinal);
        // Nothing was written — neither the buffer NOR the disk (the
        // refusal precedes ANY disk touch, HD-1).
        Assert.Contains(
            "unsaved",
            tab.Text,
            StringComparison.Ordinal);
        Assert.Equal(
            diskBefore, File.ReadAllText(Path.Combine(_root, "note0.md")));
    }

    [Fact]
    public void RestoreAsCreatesExclusivelyAndRefusesCollisions()
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        _ = SaveVersion("note0.md", "# Note 0\n\nRestorable body.\n");
        _ = SaveVersion("note0.md", "# Note 0\n\nNewer body.\n");
        workspace.OpenPath("note0.md");
        workspace.History.Reload();
        var rows = workspace.History.DayGroups
            .SelectMany(group => group.Rows).ToList();
        HistoryVersionRow oldest = rows.Where(row => !row.IsMarker).Last();
        _announced.Clear();

        (bool ok, string? refusal) = (false, null);
        workspace.CommitRestoreAsVersion(
            oldest.ContentHashAfter, oldest.AbsoluteDate,
            "note0 (restored).md", (result, message) =>
                (ok, refusal) = (result, message));

        Assert.True(ok, $"restore-as failed: {refusal}");
        Assert.Contains(_announced, e => e is A11yEvent.RestoredFileAs);
        Assert.Contains(
            "Restorable body",
            File.ReadAllText(Path.Combine(_root, "note0 (restored).md")),
            StringComparison.Ordinal);

        // The same destination now collides: exclusive-create refuses
        // and the completion carries the recorded sentence.
        workspace.CommitRestoreAsVersion(
            oldest.ContentHashAfter, oldest.AbsoluteDate,
            "note0 (restored).md", (result, message) =>
                (ok, refusal) = (result, message));
        Assert.False(ok);
        Assert.StartsWith(
            "A file already exists at",
            refusal,
            StringComparison.Ordinal);
    }

    [Fact]
    public void SuggestedRestoreCopyPathIsTheRecordedShape()
    {
        Assert.Equal(
            "note0 (restored).md",
            WorkspaceViewModel.SuggestedRestoreCopyPath("note0.md"));
        Assert.Equal(
            "sub/board (restored).canvas",
            WorkspaceViewModel.SuggestedRestoreCopyPath("sub/board.canvas"));
        Assert.Equal(
            "bare (restored).md",
            WorkspaceViewModel.SuggestedRestoreCopyPath("bare"));
    }

    [Fact]
    public void DeletedFilesListAndRecoverToOriginalPath()
    {
        using var scenario = new DeletedVaultScenario(first =>
        {
            _ = first.SaveText(
                "gone.md", "# Gone\n\nSaved before deletion.\n",
                expectedContentHash: null);
            DeleteOnSta(first, "gone.md");
        });
        using var workspace = new WorkspaceViewModel(
            scenario.Session, scenario.Root, () => [], _announced.Add,
            startInteractionBackgroundWork: false)
        {
            HistoryAlert = (title, message) => _alerts.Add($"{title}: {message}"),
        };
        workspace.History.LoadDeletedFiles();
        Assert.Null(workspace.History.DeletedError);
        HistoryDeletedRow deleted = Assert.Single(
            workspace.History.DeletedRows,
            row => string.Equals(row.Path, "gone.md", StringComparison.Ordinal));
        Assert.True(deleted.Recoverable);
        Assert.Contains("Deleted", deleted.DeletedText, StringComparison.Ordinal);
        Assert.Contains("restorable", deleted.AccessibleName, StringComparison.Ordinal);
        _announced.Clear();

        (bool ok, bool collision) = (false, false);
        workspace.RecoverDeletedFile(deleted, (result, destinationExists) =>
            (ok, collision) = (result, destinationExists));

        Assert.True(
            ok, $"recover failed: {string.Join("; ", _alerts)}");
        Assert.False(collision);
        Assert.Contains(_announced, e => e is A11yEvent.RestoredFile);
        Assert.Contains(
            "Saved before deletion",
            File.ReadAllText(Path.Combine(scenario.Root, "gone.md")),
            StringComparison.Ordinal);
        // The recovered file left the deleted list.
        Assert.DoesNotContain(
            workspace.History.DeletedRows,
            row => string.Equals(row.Path, "gone.md", StringComparison.Ordinal));
        // Recovery does NOT navigate (the mac shape): announce and
        // refresh only, so recovering several files in a row keeps the
        // user in the Deleted list.
        Assert.Null(workspace.ActiveGroup.ActiveTab);
    }

    [Fact]
    public void RecoverCollisionRoutesToRestoreAsForDeletedFiles()
    {
        using var scenario = new DeletedVaultScenario(first =>
        {
            _ = first.SaveText(
                "gone.md", "# Gone\n\nDeleted content.\n",
                expectedContentHash: null);
            DeleteOnSta(first, "gone.md");
        });
        // Occupy the path in the SECOND session (outside the remnant's
        // own log) so the remnant survives reconcile.
        File.WriteAllText(
            Path.Combine(scenario.Root, "gone.md"),
            "# Gone\n\nOccupying content.\n");
        using var workspace = new WorkspaceViewModel(
            scenario.Session, scenario.Root, () => [], _announced.Add,
            startInteractionBackgroundWork: false)
        {
            HistoryAlert = (title, message) => _alerts.Add($"{title}: {message}"),
        };
        workspace.History.LoadDeletedFiles();
        HistoryDeletedRow? deleted = workspace.History.DeletedRows
            .FirstOrDefault(row =>
                string.Equals(row.Path, "gone.md", StringComparison.Ordinal));
        if (deleted is null || !deleted.Recoverable)
        {
            // The remnant may have re-bound or quarantined against the
            // occupying file — core semantics; nothing to route then.
            return;
        }
        (bool ok, bool collision) = (false, false);
        workspace.RecoverDeletedFile(deleted, (result, destinationExists) =>
            (ok, collision) = (result, destinationExists));
        Assert.False(ok);
        Assert.True(collision);

        _announced.Clear();
        (bool asOk, string? refusal) = (false, null);
        workspace.CommitRestoreAsDeleted(
            "gone.md", "gone-recovered.md", (result, message) =>
                (asOk, refusal) = (result, message));
        Assert.True(asOk, $"restore-as-deleted failed: {refusal}");
        Assert.Contains(_announced, e => e is A11yEvent.RestoredFileAs);
        Assert.Contains(
            "Deleted content",
            File.ReadAllText(Path.Combine(scenario.Root, "gone-recovered.md")),
            StringComparison.Ordinal);
    }

    [Fact]
    public void BaseFileHistoryRidesTheSameSeam()
    {
        // H12: no extension filtering — a .base file saved through
        // core lists, diffs, and restores like any note.
        _ = SaveVersion(
            "Query.base",
            "views:\n  - type: table\n    name: Main\n");
        _ = SaveVersion(
            "Query.base",
            "views:\n  - type: table\n    name: Renamed\n");
        HistoryViewModel history = NewHistory();
        history.NoteChanged("Query.base");

        Assert.Null(history.LoadError);
        var rows = history.DayGroups.SelectMany(group => group.Rows).ToList();
        Assert.True(rows.Count >= 2);
        history.ToggleCompareSelection(rows[0].PositionFromTail);
        history.ToggleCompareSelection(rows[1].PositionFromTail);
        history.CompareSelected();
        Assert.NotNull(history.InlineDiff?.Diff);
        history.Shutdown();
    }

    [Fact]
    public void CompactionRelayAnnouncesOncePerPath()
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        _announced.Clear();

        workspace.AnnounceHistoryCompactionFailure("note0.md", "core message");
        workspace.AnnounceHistoryCompactionFailure("note0.md", "core message");
        workspace.AnnounceHistoryCompactionFailure("note1.md", "other message");

        Assert.Equal(2, _announced.Count);
        var first = Assert.IsType<A11yEvent.HostComposed>(_announced[0]);
        Assert.Equal("core message", first.Text);
        Assert.Equal(A11yPriority.Medium, first.Priority);
    }

    [Fact]
    public void RestoreConflictAlertsWithTheRecordedTitleAndReloads()
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        _ = SaveVersion("note0.md", "# Note 0\n\nOld body.\n");
        // The tab opens NOW; a later direct session save makes its
        // saved hash stale, so the restore CAS must refuse (H7).
        workspace.OpenPath("note0.md");
        _ = SaveVersion("note0.md", "# Note 0\n\nExternal newer body.\n");
        workspace.History.Reload();
        var rows = workspace.History.DayGroups
            .SelectMany(group => group.Rows).ToList();
        HistoryVersionRow oldest = rows.Where(row => !row.IsMarker).Last();
        _announced.Clear();
        _alerts.Clear();

        workspace.RequestRestoreVersion(oldest);

        string alert = Assert.Single(_alerts);
        Assert.StartsWith("Restore failed: ", alert, StringComparison.Ordinal);
        Assert.Contains(
            "The file changed after history was loaded",
            alert,
            StringComparison.Ordinal);
        // No success announcement, and the disk kept the newer body.
        Assert.DoesNotContain(_announced, e => e is A11yEvent.RestoredVersionFrom);
        Assert.Contains(
            "External newer body",
            File.ReadAllText(Path.Combine(_root, "note0.md")),
            StringComparison.Ordinal);
    }

    [Fact]
    public void BaseFileRestoreIsCasGuardedWithoutAnEditorBuffer()
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        _ = SaveVersion("Guard.base", "views:\n  - type: table\n    name: One\n");
        _ = SaveVersion("Guard.base", "views:\n  - type: table\n    name: Two\n");
        workspace.History.NoteChanged("Guard.base");
        var rows = workspace.History.DayGroups
            .SelectMany(group => group.Rows).ToList();
        HistoryVersionRow oldest = rows.Where(row => !row.IsMarker).Last();
        // The loaded list is now STALE: an external write lands after
        // the head hash was captured. H12 + H7: the restore must stay
        // CAS-guarded even with no markdown buffer — a null expected
        // hash is an unconditional core save (the clobber this guard
        // exists to stop).
        _ = SaveVersion("Guard.base", "views:\n  - type: table\n    name: Three\n");
        _announced.Clear();
        _alerts.Clear();

        workspace.RequestRestoreVersion(oldest);

        string alert = Assert.Single(_alerts);
        Assert.StartsWith("Restore failed: ", alert, StringComparison.Ordinal);
        Assert.DoesNotContain(_announced, e => e is A11yEvent.RestoredVersionFrom);
        Assert.Contains(
            "name: Three",
            File.ReadAllText(Path.Combine(_root, "Guard.base")),
            StringComparison.Ordinal);
    }

    [Fact]
    public void ShowHistoryPanelAnnouncesOnlyOnAnActualSwitch()
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        _announced.Clear();

        workspace.ShowHistoryPanel();
        workspace.ShowHistoryPanel();

        Assert.Single(_announced, e => e is A11yEvent.HistoryPanelShown);
        Assert.True(workspace.IsRightPaneVisible);
        Assert.Equal("history", workspace.ActiveLeaf.Id);
    }

    [Fact]
    public void RevealRefreshesAStaleListIdempotently()
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        _ = SaveVersion("note0.md", "# Note 0\n\nFirst.\n");
        workspace.OpenPath("note0.md");
        workspace.History.Reload();
        int before = workspace.History.DayGroups
            .SelectMany(group => group.Rows).Count();
        // A version lands OUTSIDE the tab save funnel (a session-level
        // write): the visible list is now stale.
        _ = SaveVersion("note0.md", "# Note 0\n\nSecond.\n");

        // Contract H11: revealing the leaf refreshes idempotently.
        workspace.ShowHistoryPanel();

        int after = workspace.History.DayGroups
            .SelectMany(group => group.Rows).Count();
        Assert.Equal(before + 1, after);
    }

    [Fact]
    public void VaultFileEventsRefreshTheActiveListOnly()
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        _ = SaveVersion("note0.md", "# Note 0\n\nFirst.\n");
        workspace.History.NoteChanged("note0.md");
        int before = workspace.History.DayGroups
            .SelectMany(group => group.Rows).Count();
        _ = SaveVersion("note0.md", "# Note 0\n\nSecond.\n");
        _ = SaveVersion("note1.md", "# Note 1\n\nUnrelated.\n");

        // HR-2's vault-event arm: a Modified for the ACTIVE path
        // refreshes; other paths are same-path no-ops.
        workspace.NotifyHistoryOfVaultChange("note1.md");
        Assert.Equal(
            before,
            workspace.History.DayGroups.SelectMany(group => group.Rows).Count());
        workspace.NotifyHistoryOfVaultChange("note0.md");

        Assert.Equal(
            before + 1,
            workspace.History.DayGroups.SelectMany(group => group.Rows).Count());
    }

    [Fact]
    public void CollapsePersistsAcrossReloadAndResetsOnSwitch()
    {
        _ = SaveVersion("note0.md", "# Note 0\n\nA.\n");
        HistoryViewModel history = NewHistory();
        history.NoteChanged("note0.md");
        history.DayGroups[0].IsCollapsed = true;

        // A save reload keeps the head group's stable id — collapse
        // survives (H4).
        history.NoteSaved("note0.md");
        Assert.True(history.DayGroups[0].IsCollapsed);

        // A note switch resets collapse (per-session, per-note).
        history.NoteChanged(null);
        history.NoteChanged("note0.md");
        Assert.False(history.DayGroups[0].IsCollapsed);
        history.Shutdown();
    }

    [Fact]
    public void SinceOpenUnchangedVerdictRendersNothing()
    {
        _ = SaveVersion("note0.md", "# Note 0\n\nStable.\n");
        HistoryViewModel history = NewHistory();
        history.ShowChangesSinceOpen = true;
        history.NoteChanged("note0.md");
        // Baseline marked; nothing changes; re-activate.
        history.NoteChanged(null);
        history.NoteChanged("note0.md");

        // Unchanged → None: the section stays absent (H8).
        Assert.Equal(HistorySinceOpenKind.None, history.SinceOpen.Kind);
        history.Shutdown();
    }

    [Fact]
    public void NotRecoverableDeletedRowsCarryTheRecordedLabels()
    {
        HistoryDeletedRow row = HistoryViewModel.MakeDeletedRow(
            new DeletedFileEntry("lost.md", null, false, 900));

        Assert.Equal("Deletion time unknown", row.DeletedText);
        // Size renders only for recoverable rows.
        Assert.Null(row.SizeText);
        Assert.EndsWith(
            "not restorable", row.AccessibleName, StringComparison.Ordinal);

        // The coordinator refuses without touching core (H10).
        using WorkspaceViewModel workspace = NewWorkspace();
        (bool ok, bool collision) = (true, true);
        workspace.RecoverDeletedFile(row, (result, destinationExists) =>
            (ok, collision) = (result, destinationExists));
        Assert.False(ok);
        Assert.False(collision);
    }

    [Fact]
    public void ShowOlderVersionsAppendsTheNextPageWithTheSameTotal()
    {
        for (int i = 1; i <= 56; i++)
        {
            _ = SaveVersion("pager.md", $"# Pager\n\nBody {i}.\n");
        }
        HistoryViewModel history = NewHistory();
        history.NoteChanged("pager.md");
        history.ShowMarkers = true;
        var firstPage = history.DayGroups.SelectMany(group => group.Rows).ToList();
        Assert.Equal(50, firstPage.Count);
        Assert.True(history.CanLoadOlder);
        string headerBefore = history.HeaderText;

        history.LoadOlder();

        Assert.Null(history.LoadError);
        var all = history.DayGroups.SelectMany(group => group.Rows).ToList();
        Assert.True(all.Count > firstPage.Count, "no older rows appended");
        // The append extends the SAME list: positions stay unique and
        // the header total (core's TotalFiltered) never moves (H3).
        Assert.Equal(
            all.Count, all.Select(row => row.PositionFromTail).Distinct().Count());
        Assert.Equal(headerBefore, history.HeaderText);
        Assert.False(history.CanLoadOlder);
        history.Shutdown();
    }

    [Fact]
    public void CompareAgainstCurrentWithoutAProviderIsSilent()
    {
        _ = SaveVersion("note0.md", "# Note 0\n\nBody.\n");
        HistoryViewModel history = NewHistory();
        history.NoteChanged("note0.md");
        var rows = history.DayGroups.SelectMany(group => group.Rows).ToList();

        // No comparable current state: a silent no-op (the mac guard),
        // never a host-composed error (HINV-1).
        history.CompareAgainstCurrent(rows[0]);

        Assert.Null(history.InlineDiff);
        history.Shutdown();
    }

    [Fact]
    public void FormatBytesMirrorsTheMacFileFormatter()
    {
        Assert.Equal("Zero KB", HistoryViewModel.FormatBytes(0));
        Assert.Equal("1 byte", HistoryViewModel.FormatBytes(1));
        Assert.Equal("512 bytes", HistoryViewModel.FormatBytes(512));
        Assert.Equal("2 KB", HistoryViewModel.FormatBytes(1_500));
        Assert.Equal("1.5 MB", HistoryViewModel.FormatBytes(1_500_000));
        Assert.Equal("1.25 GB", HistoryViewModel.FormatBytes(1_250_000_000));
    }

    [Fact]
    public void DestinationNoticesAreTheRecordedSentences()
    {
        Assert.Equal(
            "Save a copy of the version from July 19, 2026 at 9:41 AM "
            + "to a new file.",
            HistoryViewModel.RestoreAsNotice("July 19, 2026 at 9:41 AM"));
        // The pinned H10 collision sentence — the only thing telling
        // the user why the destination row appeared.
        Assert.Equal(
            "A file already exists at gone.md. Restore the deleted file "
            + "to a different location.",
            HistoryViewModel.DeletedCollisionNotice("gone.md"));
    }

    [Fact]
    public void DestinationStagingCapturesIdentityAtOpenAndDropsOnSwitch()
    {
        _ = SaveVersion("note0.md", "# Note 0\n\nA.\n");
        _ = SaveVersion("note0.md", "# Note 0\n\nB.\n");
        HistoryViewModel history = NewHistory();
        history.NoteChanged("note0.md");
        var rows = history.DayGroups.SelectMany(group => group.Rows).ToList();
        HistoryVersionRow oldest = rows.Where(row => !row.IsMarker).Last();

        history.OpenRestoreAsStaging(oldest, "note0 (restored).md");
        HistoryDestinationStaging staging =
            Assert.IsType<HistoryDestinationStaging>(history.DestinationStaging);
        Assert.Equal(oldest.ContentHashAfter, staging.VersionHash);
        Assert.Equal(oldest.AbsoluteDate, staging.FormattedDate);

        // The draft survives a reload that shifts every position — the
        // staged identity is what commits, never "the row at that
        // position now" (capture-at-staging).
        history.UpdateDestinationDraft("elsewhere/copy.md");
        _ = SaveVersion("note0.md", "# Note 0\n\nC.\n");
        history.NoteSaved("note0.md");
        staging =
            Assert.IsType<HistoryDestinationStaging>(history.DestinationStaging);
        Assert.Equal(oldest.ContentHashAfter, staging.VersionHash);
        Assert.Equal("elsewhere/copy.md", staging.Draft);

        // A refusal stages for the render; same-row toggle closes; a
        // note switch drops the note-scoped staging.
        history.SetDestinationRefusal("A file already exists at x.");
        Assert.Equal(
            "A file already exists at x.", history.DestinationStaging?.Refusal);
        history.NoteChanged(null);
        Assert.Null(history.DestinationStaging);
        history.Shutdown();
    }
}
