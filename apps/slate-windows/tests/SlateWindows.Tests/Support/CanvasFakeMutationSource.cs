// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Canvas;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests.Support;

/// <summary>W6-1 §E TE-5: the funnel battery's write seam — scripted
/// outcomes per call, a snapshot on demand, and a call record.</summary>
internal sealed class CanvasFakeMutationSource : ICanvasMutationSource
{
    private int _applies;

    /// <summary>Scripted: null = succeed; an exception = throw it.</summary>
    internal Func<int, VaultException?>? ApplyScript { get; set; }

    /// <summary>Runs between admission and the scripted outcome —
    /// the mid-apply displacement window's hook.</summary>
    internal Action? OnApply { get; set; }

    internal bool ReportUnindexed { get; set; }

    internal int Applies => _applies;

    internal string NextHash { get; set; } = "rev-2";

    public CanvasApplyResult Apply(ulong handle, CanvasAction action)
    {
        int call = Interlocked.Increment(ref _applies);
        OnApply?.Invoke();
        if (ApplyScript?.Invoke(call) is { } refusal)
        {
            throw refusal;
        }
        return new CanvasApplyResult(
            NextHash, new CanvasAction($"undo {action.Name}", []), !ReportUnindexed);
    }

    public CanvasEditorSeed CurrentText(ulong handle) =>
        new("{}\n", "pre-conflict");
}
