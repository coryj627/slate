// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace SlateWindows.Tests;

/// <summary>#1272: an import whose source picker is still open until the
/// fact hands it one real file outside the vault; the worker runs the import
/// off the dispatcher and counts itself. While the picker is open the import
/// is running (<c>IsImporting</c>); a cancelled import still reports running
/// until the picker returns, so a fact tells "kept" from "cancelled" by
/// handing over the source and counting the worker's runs — one for an
/// import that kept running, none for a cancelled one.</summary>
internal sealed class PendingImport : IDisposable
{
    private readonly TaskCompletionSource<IReadOnlyList<string>> _sources =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly string _source = Path.Combine(
        Path.GetTempPath(), $"slate-import-source-{Guid.NewGuid():N}.md");
    private int _workerRuns;

    public PendingImport() => File.WriteAllText(_source, "# Imported\n");

    public int WorkerRuns => Volatile.Read(ref _workerRuns);

    public Task<IReadOnlyList<string>> PickSources() => _sources.Task;

    public Task Run(Action work, CancellationToken cancellation)
    {
        Interlocked.Increment(ref _workerRuns);
        return Task.Run(work, cancellation);
    }

    public void HandOverTheSource() => _sources.SetResult([_source]);

    public void Dispose()
    {
        _ = _sources.TrySetResult([]);
        try
        {
            File.Delete(_source);
        }
        catch (IOException)
        {
        }
    }
}
