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

    internal async Task<SwitcherRankPage> RankAsync(
        Func<SwitcherRankPage> rank,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rank);
        await _nativeRankLane.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await Task.Run(rank, CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            _nativeRankLane.Release();
        }
    }
}
