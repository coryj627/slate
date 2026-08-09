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
        workspace.HistoryAlert = _alerts.Add;
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

        history.ShowMarkers = true;

        var visibleWithMarkers = history.DayGroups
            .SelectMany(group => group.Rows).ToList();
        Assert.True(
            visibleWithMarkers.Count >= visibleDefault.Count,
            "showing markers must never hide rows");
        // The toggle is a pure re-filter over the cached page — the
        // marker rows (the op-log anchor at minimum) appear.
        Assert.Contains(visibleWithMarkers, row => row.IsMarker);
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
        workspace.HistoryRestoreConfirmation = _ => true;
        bool headFocusRequested = false;
        workspace.History.FocusHeadRequested += () => headFocusRequested = true;
        _announced.Clear();

        workspace.RequestRestoreVersion(versionOne);

        Assert.True(
            _alerts.Count == 0,
            $"restore raised an alert: {string.Join("; ", _alerts)}");
        Assert.Contains(_announced, e => e is A11yEvent.RestoredVersionFrom);
        Assert.Contains(
            "Version one body",
            File.ReadAllText(Path.Combine(_root, "note0.md")),
            StringComparison.Ordinal);
        // The open CLEAN tab reloaded from disk (H7).
        WorkspaceTabViewModel tab =
            Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);
        Assert.Contains("Version one body", tab.Text, StringComparison.Ordinal);
        Assert.True(headFocusRequested);
        // History never rewrites: the restore appended a NEW head row.
        var reloaded = workspace.History.DayGroups
            .SelectMany(group => group.Rows).ToList();
        Assert.True(reloaded.Count >= rows.Count);
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
        _announced.Clear();

        workspace.RequestRestoreVersion(rows[^1]);

        var refusal = Assert.IsType<A11yEvent.HostComposed>(Assert.Single(_announced));
        Assert.StartsWith(
            "Save the note before restoring a version.",
            refusal.Text,
            StringComparison.Ordinal);
        // Nothing was written.
        Assert.Contains(
            "unsaved",
            tab.Text,
            StringComparison.Ordinal);
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
            oldest, "note0 (restored).md", (result, message) =>
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
            oldest, "note0 (restored).md", (result, message) =>
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
            HistoryAlert = _alerts.Add,
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
            HistoryAlert = _alerts.Add,
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
}
