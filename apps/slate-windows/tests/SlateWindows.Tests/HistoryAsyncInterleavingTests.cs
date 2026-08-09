// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Panels;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W4-7 (#739): the history document under PRODUCTION scheduling
/// (synchronousForTests: false). The synchronous mode orders load,
/// diff, and mark bodies deterministically, so the generation/path
/// guards and the pagination/mark serialization rules are dead code
/// under every other history fact (red team round 1). These facts
/// hold workers at the VM's interleave seams so each race is a
/// deterministic schedule, never a timing bet — the citations
/// async-suite lesson.
///
/// The VM is constructed with a NULL SynchronizationContext so
/// publishes run inline on the worker: after a drain, every publish
/// has been applied (with xunit's context they would still be
/// queued).
/// </summary>
public sealed class HistoryAsyncInterleavingTests : IDisposable
{
    private readonly string _root;
    private readonly VaultSession _session;

    public HistoryAsyncInterleavingTests()
    {
        _root = Path.Combine(
            Path.GetTempPath(),
            $"slate-windows-test-history-async-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "seed.md"), "# Seed\n");
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

    private string SaveVersion(string path, string content) =>
        _session.SaveText(path, content, expectedContentHash: null).NewContentHash;

    private HistoryViewModel NewAsyncHistory()
    {
        SynchronizationContext? previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(null);
        try
        {
            return new HistoryViewModel(_session, synchronousForTests: false);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    /// <summary>Drain repeatedly: a drained body can have queued
    /// follow-up work (the mark, a cursor restart) that the drain's
    /// snapshot missed.</summary>
    private static async Task QuiesceAsync(HistoryViewModel history)
    {
        for (int round = 0; round < 10; round++)
        {
            await history.DrainForTests();
            await Task.Delay(2);
        }
    }

    private static List<HistoryVersionRow> Rows(HistoryViewModel history) =>
        history.DayGroups.SelectMany(group => group.Rows).ToList();

    [Fact]
    public async Task NoteSwitchClearsTheOldRowsBeforeTheNextLoadLands()
    {
        _ = SaveVersion("a.md", "# A\n\nOne.\n");
        _ = SaveVersion("a.md", "# A\n\nTwo.\n");
        _ = SaveVersion("b.md", "# B\n\nOnly.\n");
        HistoryViewModel history = NewAsyncHistory();
        history.NoteChanged("a.md");
        await QuiesceAsync(history);
        Assert.NotEmpty(Rows(history));
        using var gate = new ManualResetEventSlim(false);
        history.LoadInterleaveForTests = () => gate.Wait(TimeSpan.FromSeconds(10));

        history.NoteChanged("b.md");

        // While b's page-one load is in flight (log-length-
        // proportional, HR-1), note a's rows must not render
        // actionable under b's path — the switch clears NOW, not at
        // publish time.
        Assert.Empty(history.DayGroups);
        Assert.Equal("b.md", history.Path);
        gate.Set();
        history.LoadInterleaveForTests = null;
        await QuiesceAsync(history);
        Assert.Single(Rows(history));
        history.Shutdown();
    }

    [Fact]
    public async Task AReloadInvalidatesPaginationBeforeItLands()
    {
        for (int i = 1; i <= 56; i++)
        {
            _ = SaveVersion("pager.md", $"# Pager\n\nBody {i}.\n");
        }
        HistoryViewModel history = NewAsyncHistory();
        history.NoteChanged("pager.md");
        await QuiesceAsync(history);
        Assert.True(history.CanLoadOlder);
        using var gate = new ManualResetEventSlim(false);
        history.LoadInterleaveForTests = () => gate.Wait(TimeSpan.FromSeconds(10));
        string newestHash = SaveVersion("pager.md", "# Pager\n\nFinal body.\n");

        history.NoteSaved("pager.md");

        // The reload invalidated the cursor SYNCHRONOUSLY: core
        // cursors survive appends, so a concurrent "Show older
        // versions" click would otherwise append the pre-save page
        // onto a stale snapshot while the reload is dropped by
        // generation — and the just-saved version never appears.
        Assert.False(history.CanLoadOlder);
        history.LoadOlder();
        gate.Set();
        history.LoadInterleaveForTests = null;
        await QuiesceAsync(history);
        List<HistoryVersionRow> rows = Rows(history);
        Assert.Equal(newestHash, rows.First().ContentHashAfter);
        Assert.Equal(
            rows.Count,
            rows.Select(row => row.PositionFromTail).Distinct().Count());
        Assert.True(history.CanLoadOlder);
        history.Shutdown();
    }

    [Fact]
    public async Task AStaleDiffNeverRepublishesIntoAFreshActivation()
    {
        _ = SaveVersion("diff.md", "# Diff\n\nOld body.\n");
        string headHash = SaveVersion("diff.md", "# Diff\n\nNew body.\n");
        _ = SaveVersion("other.md", "# Other\n\nBody.\n");
        HistoryViewModel history = NewAsyncHistory();
        history.CurrentContentHashProvider = () => headHash;
        history.NoteChanged("diff.md");
        await QuiesceAsync(history);
        HistoryVersionRow oldest = Rows(history).Last(row => !row.IsMarker);
        using var gate = new ManualResetEventSlim(false);
        history.DiffInterleaveForTests = () => gate.Wait(TimeSpan.FromSeconds(10));

        history.CompareAgainstCurrent(oldest);
        // A → B → A while the diff worker is still in flight: its
        // (generation, path) both match again on the return leg, so
        // only the reset's generation bump can drop the stale publish
        // (HINV-5, H6's reset-on-switch rule).
        history.NoteChanged("other.md");
        Assert.True(SpinWait.SpinUntil(
            () => string.Equals(history.Path, "other.md", StringComparison.Ordinal)
                && history.DayGroups.Count > 0,
            TimeSpan.FromSeconds(5)));
        history.NoteChanged("diff.md");
        Assert.True(SpinWait.SpinUntil(
            () => string.Equals(history.Path, "diff.md", StringComparison.Ordinal)
                && history.DayGroups.Count > 0,
            TimeSpan.FromSeconds(5)));

        gate.Set();
        history.DiffInterleaveForTests = null;
        await QuiesceAsync(history);
        Assert.Null(history.InlineDiff);
        history.Shutdown();
    }

    [Fact]
    public async Task SinceOpenOptOutMidFlightSuppressesTheSectionAndTheMark()
    {
        _ = SaveVersion("so.md", "# So\n\nFirst.\n");
        HistoryViewModel history = NewAsyncHistory();
        history.ShowChangesSinceOpen = true;
        history.NoteChanged("so.md");
        await QuiesceAsync(history);
        _ = SaveVersion("so.md", "# So\n\nSecond.\n");
        history.NoteChanged(null);
        await QuiesceAsync(history);
        using var gate = new ManualResetEventSlim(false);
        history.LoadInterleaveForTests = () => gate.Wait(TimeSpan.FromSeconds(10));

        history.NoteChanged("so.md");
        // The verdict (a Diff against the marked baseline) is already
        // computed and parked; the user opts out before it publishes.
        history.ShowChangesSinceOpen = false;
        gate.Set();
        history.LoadInterleaveForTests = null;
        await QuiesceAsync(history);

        // The section must not reappear after the opt-out (H8) …
        Assert.Equal(HistorySinceOpenKind.None, history.SinceOpen.Kind);
        // … and the baseline must not have been MARKED with the pref
        // off (HINV-8): re-enabling still owes the same diff.
        history.ShowChangesSinceOpen = true;
        history.NoteChanged(null);
        await QuiesceAsync(history);
        history.NoteChanged("so.md");
        await QuiesceAsync(history);
        Assert.Equal(HistorySinceOpenKind.Diff, history.SinceOpen.Kind);
        history.Shutdown();
    }

    [Fact]
    public async Task APendingMarksChangesAreNeverReReported()
    {
        _ = SaveVersion("mk.md", "# Mk\n\nFirst.\n");
        HistoryViewModel history = NewAsyncHistory();
        history.ShowChangesSinceOpen = true;
        // Activation one establishes a marked baseline (ungated).
        history.NoteChanged("mk.md");
        await QuiesceAsync(history);
        _ = SaveVersion("mk.md", "# Mk\n\nSecond.\n");
        history.NoteChanged(null);
        await QuiesceAsync(history);
        using var markGate = new ManualResetEventSlim(false);
        history.MarkInterleaveForTests = () => markGate.Wait(TimeSpan.FromSeconds(10));

        // Activation two SHOWS the diff; its MarkOpened parks at the
        // gate. (No quiesce while the gate is held — the drain would
        // wait on the parked mark.)
        history.NoteChanged("mk.md");
        Assert.True(SpinWait.SpinUntil(
            () => history.SinceOpen.Kind == HistorySinceOpenKind.Diff,
            TimeSpan.FromSeconds(5)));
        // The note re-activates rapidly (A→B→A) while the mark is
        // still pending. The new verdict must OBSERVE that mark: the
        // user was ALREADY shown these changes, so re-reporting them
        // against the stale baseline is the defect (HINV-8's
        // cross-activation half).
        history.NoteChanged(null);
        history.NoteChanged("mk.md");
        // Give the unserialized (pre-fix) verdict time to publish its
        // re-report BEFORE the mark releases — without this window a
        // lucky pre-fix interleaving could mark first and pass
        // vacuously. Post-fix the load is parked on the pending mark
        // and the window simply elapses.
        _ = SpinWait.SpinUntil(
            () => history.SinceOpen.Kind == HistorySinceOpenKind.Diff,
            TimeSpan.FromMilliseconds(500));
        markGate.Set();
        history.MarkInterleaveForTests = null;
        await QuiesceAsync(history);

        Assert.Equal(HistorySinceOpenKind.None, history.SinceOpen.Kind);
        history.Shutdown();
    }
}
