// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using uniffi.slate_uniffi;

namespace SlateWindows;

/// <summary>
/// Process-lifetime admission lane for native Quick Open ranking. Cancellation
/// can remove queued work; an admitted native call owns the lane until it
/// actually returns. Since WB-25 stage 2 that call sees its cancelled core
/// token and stops early, but it still returns before the lane is free.
/// </summary>
internal sealed class QuickSwitcherRankCoordinator
{
    internal static QuickSwitcherRankCoordinator Shared { get; } = new();

    private readonly SemaphoreSlim _nativeRankLane = new(1, 1);

    /// <param name="admitted">W7-7 PR 7 (codex PR 7 round 3, finding 5):
    /// told, on admission and BEFORE the admitted call's token is checked,
    /// of a Task that completes once the call has returned and released the
    /// lane — what a closing owner drains, so the call returns before the
    /// session it reads is disposed.</param>
    internal async Task<SwitcherRankPage> RankAsync(
        Func<SwitcherRankPage> rank,
        CancellationToken cancellationToken,
        Action<Task>? admitted = null)
    {
        ArgumentNullException.ThrowIfNull(rank);
        await _nativeRankLane.WaitAsync(cancellationToken).ConfigureAwait(false);
        var released = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            admitted?.Invoke(released.Task);
            cancellationToken.ThrowIfCancellationRequested();
            return await Task.Run(rank, CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            _nativeRankLane.Release();
            _ = released.TrySetResult();
        }
    }
}
