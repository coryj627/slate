// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using uniffi.slate_uniffi;

namespace SlateWindows;

/// <summary>
/// #1279 (locked decision 05 §4): one in-flight core request's
/// cancellation. The dispatcher cancels it when the request is retired —
/// closed, superseded, invalidated or disposed — and core stops the walk
/// at its next cooperative boundary with <c>VaultError::Cancelled</c>; the
/// worker disposes the token once its FFI calls have returned. The gate
/// keeps a late cancel off a disposed token. Used by the Ctrl+E preview,
/// the embeds leaf's batch and the reading view's embed cards.
/// </summary>
internal sealed class CoreRequestCancellation
{
    private readonly Lock _gate = new();
    private bool _finished;

    internal CancelToken Token { get; } = new();

    internal void Cancel()
    {
        lock (_gate)
        {
            if (!_finished)
            {
                Token.Cancel();
            }
        }
    }

    internal void Finish()
    {
        lock (_gate)
        {
            if (_finished)
            {
                return;
            }
            _finished = true;
            Token.Dispose();
        }
    }
}
