// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text;
using uniffi.slate_uniffi;

namespace SlateProbe;

internal static class EventSections
{
    /// <summary>
    /// ScanProgressListener choreography: exactly one Started with the
    /// right total, one FileIndexed per file with a monotonic counter,
    /// exactly one terminal Finished whose report matches, and dispatch
    /// from a non-C# thread (the scanner's Rust thread).
    /// </summary>
    public static bool ScanProgressSection(Probe p)
    {
        bool ok = true;
        const int notes = 40;
        using var vault = FixtureVault.Create(notes, "progress");
        using var session = VaultSession.OpenFilesystem(vault.Root);
        using var token = new CancelToken();
        var recorder = new ProgressRecorder();
        int mainThread = Environment.CurrentManagedThreadId;

        var report = session.ScanInitialWithProgress(token, recorder);
        var events = recorder.Snapshot();

        var started = events.OfType<ScanProgress.Started>().ToList();
        var indexed = events.OfType<ScanProgress.FileIndexed>().ToList();
        var finished = events.OfType<ScanProgress.Finished>().ToList();
        ok &= p.Check(started.Count == 1, $"exactly one Started (got {started.Count})");
        ok &= p.Check(indexed.Count == notes, $"one FileIndexed per file (got {indexed.Count}/{notes})");
        ok &= p.Check(finished.Count == 1 && events[^1] is ScanProgress.Finished,
            "exactly one terminal Finished");
        ok &= p.Check(!events.Any(e => e is ScanProgress.Cancelled or ScanProgress.Failed),
            "no spurious Cancelled/Failed");

        bool monotonic = true;
        for (int i = 1; i < indexed.Count; i++)
        {
            if (indexed[i].Indexed != indexed[i - 1].Indexed + 1) { monotonic = false; break; }
        }
        ok &= p.Check(monotonic && indexed.Count > 0 && indexed[0].Indexed == 1,
            "FileIndexed counter is 1..N monotonic");
        ok &= p.Check(finished.Count == 1 && finished[0].Report.FilesIndexed == (ulong)notes
                && report.FilesIndexed == (ulong)notes,
            "Finished.report and returned report agree");
        bool inline = recorder.ThreadIds.Count == 1 && recorder.ThreadIds.Contains(mainThread);
        p.Finding($"scan progress dispatch: {(inline ? "inline on the scanning (calling) thread" : $"threads {string.Join(",", recorder.ThreadIds)} vs caller {mainThread}")}");
        return ok;
    }

    /// <summary>
    /// VaultEventListener across an operation: all three event kinds —
    /// on_index_phase (scan), on_file_change (saves), and on_error via a
    /// genuinely failed background compaction (read-only oplog file, the
    /// Windows-semantics failure), then unregistration seals the stream.
    /// </summary>
    public static bool VaultEvents(Probe p)
    {
        bool ok = true;
        using var vault = FixtureVault.Create(6, "events");
        using var session = VaultSession.OpenFilesystem(vault.Root);
        using var token = new CancelToken();
        var recorder = new EventRecorder();
        ulong registration = session.RegisterEventListener(recorder);

        // Kind 1: index phases from a scan, in choreography order.
        session.ScanInitial(token);
        ok &= p.Check(
            WaitFor(() => recorder.Locked(r => r.IndexPhases.Count) >= 4, 5000),
            "scan delivered index-phase events");
        var phases = recorder.Locked(r => r.IndexPhases.Select(e => e.Phase).ToList());
        ok &= p.Check(
            phases.SequenceEqual(new[]
            {
                IndexPhase.ScanStarted, IndexPhase.ReconcileStarted,
                IndexPhase.ReconcileFinished, IndexPhase.ScanFinished,
            }),
            $"phase order Started→ReconcileStarted→ReconcileFinished→Finished (got {string.Join(",", phases)})");
        ok &= p.Check(
            recorder.Locked(r => r.IndexPhases.Last().FilesSeen) == 6,
            "ScanFinished carries files_seen");

        // Kind 2: file changes — a fresh path then an overwrite.
        session.SaveText("created.md", "born\n", null);
        session.SaveText("created.md", "changed\n", null);
        ok &= p.Check(
            WaitFor(() => recorder.Locked(r => r.FileChanges.Count) >= 2, 5000),
            "saves delivered file-change events");
        var changes = recorder.Locked(r => r.FileChanges.ToList());
        ok &= p.Check(
            changes.Any(c => c.Kind == FileChangeKind.Created && c.Path == "created.md")
                && changes.Any(c => c.Kind == FileChangeKind.Modified && c.Path == "created.md"),
            "Created then Modified for the saved path");

        // Kind 3: on_error from a real failed compaction. Grow one note's
        // oplog past the 5 MiB threshold with snapshot-heavy saves and flip
        // the log read-only **immediately after the threshold-crossing
        // save**: the background worker's rewrite (tmp + rename-over) hits
        // Windows read-only semantics and dispatches CompactionFailed.
        // The worker needs tens of ms to fold a >5 MiB log, but on a slow
        // shared runner it can win if the flag lands a whole save-batch
        // later — so the flag is set inside the growth loop, within the
        // same iteration that crosses the threshold (observed race loss on
        // the GitHub-hosted x64 lane, all 4 batch-then-flag rounds lost).
        const long compactionThresholdBytes = 5 * 1024 * 1024;
        string bigBody = new StringBuilder(270_000)
            .Insert(0, "compaction ballast paragraph without structure\n", 5_700)
            .ToString();
        var slateDir = new DirectoryInfo(Path.Combine(vault.Root, ".slate"));
        bool sawError = false;
        for (int round = 0; round < 4 && !sawError; round++)
        {
            FileInfo? flagged = null;
            for (int i = 0; i < 22 && flagged == null; i++)
            {
                session.SaveText("hot.md", bigBody + $"tail {round}/{i}\n", null);
                var log = slateDir.Exists
                    ? slateDir
                        .EnumerateFiles("*.oplog", SearchOption.AllDirectories)
                        .OrderByDescending(f => f.Length)
                        .FirstOrDefault()
                    : null;
                if (log != null && log.Length > compactionThresholdBytes)
                {
                    File.SetAttributes(log.FullName, FileAttributes.ReadOnly);
                    flagged = log;
                }
            }
            if (flagged == null)
            {
                p.Note($"round {round}: oplog never crossed the {compactionThresholdBytes / 1024 / 1024} MiB threshold");
                break;
            }
            try
            {
                sawError = WaitFor(() => recorder.Locked(r => r.Errors.Count) > 0, 10_000);
            }
            finally
            {
                File.SetAttributes(flagged.FullName, FileAttributes.Normal);
            }
            if (!sawError)
            {
                p.Note($"round {round}: compaction won the race (log {flagged.Length / 1024} KiB); regrowing");
            }
        }
        ok &= p.Check(sawError, "failed compaction dispatched on_error");
        if (sawError)
        {
            var (code, path, message) = recorder.Locked(r => r.Errors[0]);
            ok &= p.Check(code == EventErrorCode.CompactionFailed, "code is CompactionFailed");
            ok &= p.Check(path == "hot.md", $"error names the hot path (got {path})");
            ok &= p.Check(message.Contains("hot.md"), "message carries user-facing copy");
        }

        int mainThread = Environment.CurrentManagedThreadId;
        p.Finding($"VaultEventListener: all three kinds delivered in one session; dispatch " +
                  $"threads {string.Join(",", recorder.ThreadIds)} (caller {mainThread})");

        // Unregister seals the stream — later activity must not reach it.
        session.UnregisterEventListener(registration);
        int sealedCount = recorder.TotalCount;
        session.SaveText("post-unregister.md", "silent\n", null);
        Thread.Sleep(400);
        ok &= p.Check(recorder.TotalCount == sealedCount,
            "no events delivered after unregister_event_listener");

        // Unknown token unregister is a no-op, not an error.
        session.UnregisterEventListener(9_999_999);
        p.Note("ok: unknown unregister token tolerated");
        return ok;
    }

    internal static bool WaitFor(Func<bool> condition, int timeoutMs)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            if (condition()) return true;
            Thread.Sleep(25);
        }
        return condition();
    }
}
