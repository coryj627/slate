// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// §W-E vault-event delivery census (w0_spec §W0-3 item 2, #715): all
// three VaultEventListener methods deliver in one session — index phases
// in exact choreography, file changes for saves, and on_error from a
// genuinely failed background compaction. Ported from the W0-1 probe's
// vault-events section (with its runner-race fix: the oplog is flagged
// read-only within the same growth iteration that crosses the compaction
// threshold, so the worker's rewrite deterministically fails).

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
        // oplog with snapshot-heavy saves and flag it read-only within the
        // same iteration that crosses the 5 MiB threshold: the background
        // worker's rewrite (tmp + rename-over) hits Windows read-only
        // semantics and dispatches CompactionFailed from its worker thread.
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
            Assert.True(flagged != null, $"round {round}: oplog never crossed the compaction threshold");
            try
            {
                sawError = Waiting.WaitFor(() => recorder.Locked(r => r.Errors.Count) > 0, 10_000);
            }
            finally
            {
                File.SetAttributes(flagged!.FullName, FileAttributes.Normal);
            }
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
}
