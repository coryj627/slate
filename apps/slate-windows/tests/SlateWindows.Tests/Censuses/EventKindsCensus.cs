// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// §W-E vault-event delivery census (w0_spec §W0-3 item 2, #715): all
// three VaultEventListener methods deliver in one session — index phases
// in exact choreography, file changes for saves, and on_error from a
// genuinely failed background compaction. Ported from the W0-1 probe's
// vault-events section. #1896: the probe flagged the oplog read-only after
// the save that crossed the compaction threshold, which raced the worker; the
// log is now held open without delete sharing from before that save, so the
// worker's rename-over fails whenever it runs.

using System.Text;
using SlateWindows.Tests.Support;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "event-kinds")]
public class EventKindsCensus
{
    [Fact]
    public void AllThreeEventKinds_DeliverInOneSession()
    {
        using var vault = FixtureVault.Create(6);
        using var session = VaultSession.OpenFilesystem(vault.Root);
        var recorder = new EventRecorder();
        ulong registration = session.RegisterEventListener(recorder);
        using var token = new CancelToken();

        // Kind 1: index phases — exact quiet-session choreography.
        session.ScanInitial(token);
        Assert.True(
            Waiting.WaitFor(() => recorder.Locked(r => r.IndexPhases.Count) >= 4, 5000),
            "scan delivered no index-phase events");
        Assert.Equal(
            new[]
            {
                IndexPhase.ScanStarted, IndexPhase.ReconcileStarted,
                IndexPhase.ReconcileFinished, IndexPhase.ScanFinished,
            },
            recorder.Locked(r => r.IndexPhases.Select(e => e.Phase).ToList()));
        Assert.Equal(6UL, recorder.Locked(r => r.IndexPhases.Last().FilesSeen));

        // Kind 2: file changes — a fresh path then an overwrite.
        session.SaveText("created.md", "born\n", null);
        session.SaveText("created.md", "changed\n", null);
        Assert.True(
            Waiting.WaitFor(() => recorder.Locked(r => r.FileChanges.Count) >= 2, 5000),
            "saves delivered no file-change events");
        var changes = recorder.Locked(r => r.FileChanges.ToList());
        Assert.Contains(changes, c => c.Kind == FileChangeKind.Created && c.Path == "created.md");
        Assert.Contains(changes, c => c.Kind == FileChangeKind.Modified && c.Path == "created.md");

        // Kind 3: on_error from a real failed compaction. Grow one note's
        // oplog with snapshot-heavy saves past the 5 MiB threshold, which
        // enqueues the background compaction (tmp + rename-over). #1896: the
        // fact used to flag the log read-only only AFTER the crossing save,
        // racing the worker: a worker that compacted first left a small log,
        // and the round never saw the crossing. Now the log is held open
        // from before the crossing save without FILE_SHARE_DELETE. Core's
        // appends and reads share read and write with that handle, but
        // Windows refuses to replace a file another handle holds without
        // delete sharing, so the rename-over fails whenever the worker runs
        // and dispatches CompactionFailed from its thread; and since nothing
        // can shrink the log meanwhile, the crossing is always seen.
        const long compactionThresholdBytes = 5 * 1024 * 1024;
        string bigBody = new StringBuilder(270_000)
            .Insert(0, "compaction ballast paragraph without structure\n", 5_700)
            .ToString();
        var slateDir = new DirectoryInfo(Path.Combine(vault.Root, ".slate"));
        session.SaveText("hot.md", bigBody + "tail first\n", null);
        FileInfo log = Assert.IsType<FileInfo>(slateDir
            .EnumerateFiles("*.oplog", SearchOption.AllDirectories)
            .OrderByDescending(f => f.Length)
            .FirstOrDefault());
        Assert.True(log.Length < compactionThresholdBytes, $"premise: one save stays under the threshold ({log.Length} bytes)");
        bool sawError;
        // The held handle's own length is the file's: a directory entry's
        // size can lag while another process appends.
        using (FileStream held = HoldWithoutDeleteSharing(log))
        {
            for (int i = 0; i < 40 && held.Length <= compactionThresholdBytes; i++)
            {
                session.SaveText("hot.md", bigBody + $"tail {i}\n", null);
            }
            Assert.True(
                held.Length > compactionThresholdBytes,
                $"hot.md's oplog never crossed the compaction threshold ({held.Length} bytes)");
            sawError = Waiting.WaitFor(() => recorder.Locked(r => r.Errors.Count) > 0, 10_000);
        }
        Assert.True(sawError, "failed compaction dispatched no on_error");
        var (code, path, message) = recorder.Locked(r => r.Errors[0]);
        Assert.Equal(EventErrorCode.CompactionFailed, code);
        Assert.Equal("hot.md", path);
        Assert.Contains("hot.md", message);

        // Dispatch arrived off the test thread (scanner / worker threads).
        Assert.Contains(recorder.ThreadIds, id => id != Environment.CurrentManagedThreadId);

        // Unregister seals the stream; unknown tokens are a no-op.
        session.UnregisterEventListener(registration);
        int sealedCount = recorder.TotalCount;
        session.SaveText("post-unregister.md", "silent\n", null);
        Thread.Sleep(400);
        Assert.Equal(sealedCount, recorder.TotalCount);
        session.UnregisterEventListener(9_999_999);
    }

    /// <summary>A read handle on <paramref name="file"/> that shares read and
    /// write but not delete: another process's appends and reads go on,
    /// and a rename over the file is refused while it is open.</summary>
    internal static FileStream HoldWithoutDeleteSharing(FileInfo file) =>
        new(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
}
