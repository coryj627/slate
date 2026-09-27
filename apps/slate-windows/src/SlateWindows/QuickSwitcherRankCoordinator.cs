// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using uniffi.slate_uniffi;

namespace SlateWindows;

/// <summary>
/// Process-lifetime admission lane for native Quick Open ranking. Cancellation
/// can remove queued work, but an admitted native call owns the lane until it
/// actually returns because the FFI operation itself is not cancellable.
/// </summary>
internal sealed class QuickSwitcherRankCoordinator
{
    internal static QuickSwitcherRankCoordinator Shared { get; } = new();

    private readonly SemaphoreSlim _nativeRankLane = new(1, 1);

    /// <param name="admitted">W7-7 PR 7 (codex PR 7 round 3, finding 5):
    /// told, on admission and BEFORE the admitted call's token is checked,
    /// of a Task that completes once the call has returned and released the
    /// lane — what a closing owner drains, since the native call itself no
    /// longer sees its token.</param>
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
