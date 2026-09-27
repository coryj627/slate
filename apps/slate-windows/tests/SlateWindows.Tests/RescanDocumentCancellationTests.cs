// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Concurrent;
using SlateWindows.Bases;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W7-7 PR 7 (#1252, R-9; the ruling on codex PR 7 round 1, finding 6): the
/// base reloads a rescan's re-sync awaits honour the run's cancellation.
/// Core's <c>OpenBase</c> takes no token, so the document checks it before
/// the open; it trips <c>BaseExecute</c>'s own token; a result posted after
/// the cancel is discarded; and the awaited Task is cancelled at once.
/// </summary>
public sealed class RescanDocumentCancellationTests : IDisposable
{
    private readonly string _root;
    private readonly VaultSession _session;
    private readonly List<A11yEvent> _announced = [];

    public RescanDocumentCancellationTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"slate-windows-rescan-cancel-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "a.md"), "# A\n");
        File.WriteAllText(
            Path.Combine(_root, "Notes.base"),
            "filters: 'file.ext == \"md\"'\nviews:\n  - type: table\n    name: Main\n    order:\n      - file.name\n");
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

    /// <summary>A token cancelled before the reload: neither the reopen nor
    /// the re-run starts — nothing is published, the document keeps its
    /// state — and both awaited Tasks are cancelled.</summary>
    [Fact]
    public void ACancelledRescanReopensAndReRunsNothing()
    {
        var document = new BaseDocumentViewModel(_session, "Notes.base", _announced.Add, synchronousForTests: true);
        document.Load();
        Assert.Equal(BaseLoadState.Ready, document.State);
        int publications = 0;
        document.ResultPublished += (_, _) => publications++;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Task load = document.LoadAsync(cancellation.Token);
        Task refresh = document.RefreshAsync(cancellation.Token);

        Assert.True(load.IsCanceled);
        Assert.True(refresh.IsCanceled);
        Assert.Equal(0, publications);
        Assert.Equal(BaseLoadState.Ready, document.State);
        document.Shutdown();
    }

    /// <summary>A re-run whose result is already posted when the rescan is
    /// cancelled: the awaited Task is cancelled at once, and the posted
    /// result — a note the index gained — is discarded, never published.</summary>
    [Fact]
    public async Task AResultPostedAfterTheCancelIsDiscarded()
    {
        var pump = new PumpSynchronizationContext();
        BaseDocumentViewModel document = NewAsyncDocument(pump, "Notes.base");
        document.Load();
        await document.DrainForTests();
        pump.Drain();
        Assert.Equal(BaseLoadState.Ready, document.State);
        Assert.Single(document.Result!.Rows);

        _ = _session.CreateExclusive("b.md", "# B\n");
        using var cancellation = new CancellationTokenSource();
        Task refresh = document.RefreshAsync(cancellation.Token);
        await document.DrainForTests();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => refresh);
        pump.Drain();

        Assert.Single(document.Result!.Rows);
        document.Shutdown();
    }

    private BaseDocumentViewModel NewAsyncDocument(SynchronizationContext context, string path)
    {
        SynchronizationContext? previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            return new BaseDocumentViewModel(_session, path, _announced.Add, synchronousForTests: false);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    private sealed class PumpSynchronizationContext : SynchronizationContext
    {
        private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _queue = [];

        public override void Post(SendOrPostCallback callback, object? state) =>
            _queue.Enqueue((callback, state));

        public void Drain()
        {
            while (_queue.TryDequeue(out (SendOrPostCallback Callback, object? State) work))
            {
                work.Callback(work.State);
            }
        }
    }
}
