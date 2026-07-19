// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// W0-2 "hello, core" proof in test form (w0_spec §W0-2 item 1): the
// Windows scaffold opens a vault through the W0-1 uniffi binding and
// observes scan progress. The full-surface §W-E censuses land with W0-3.

using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

public class HelloCoreTests
{
    [Fact]
    public void OpenVault_InitialScan_ReportsProgressChoreography()
    {
        const int notes = 12;
        using var vault = FixtureVault.Create(notes);
        using var session = VaultSession.OpenFilesystem(vault.Root);
        using var token = new CancelToken();
        var recorder = new RecordingProgressListener();

        ScanReport report = session.ScanInitialWithProgress(token, recorder);

        Assert.Equal((ulong)notes, report.FilesIndexed);
        var events = recorder.Snapshot();
        Assert.Equal(1, events.Count(e => e is ScanProgress.Started));
        Assert.Equal(notes, events.Count(e => e is ScanProgress.FileIndexed));
        Assert.Equal(1, events.Count(e => e is ScanProgress.Finished));
        Assert.IsType<ScanProgress.Finished>(events[^1]);
        Assert.DoesNotContain(events, e => e is ScanProgress.Cancelled or ScanProgress.Failed);
    }

    [Fact]
    public void OpenVault_RootUnderExistingFile_MapsToTypedInvalidPath()
    {
        string filePath = Path.Combine(
            Path.GetTempPath(), $"slate-windows-test-file-{Guid.NewGuid():N}");
        File.WriteAllText(filePath, "not a directory");
        try
        {
            var ex = Assert.Throws<VaultException.InvalidPath>(
                () => VaultSession.OpenFilesystem(Path.Combine(filePath, "sub")));
            Assert.NotEmpty(ex.reason);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    private sealed class RecordingProgressListener : ScanProgressListener
    {
        private readonly object _lock = new();
        private readonly List<ScanProgress> _events = new();

        public void OnProgress(ScanProgress @event)
        {
            lock (_lock)
            {
                _events.Add(@event);
            }
        }

        public List<ScanProgress> Snapshot()
        {
            lock (_lock)
            {
                return new List<ScanProgress>(_events);
            }
        }
    }
}
