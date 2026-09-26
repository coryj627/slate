// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace SlateWindows;

/// <summary>
/// W7-7 PR 7 (#1252, R-9, round 26; locked decision 05 §4.1): the ONE seam
/// every rescan core call goes through — the cancel token's creation, the
/// scan, the ledger's pending read, every page read, every applied mark
/// (core's coalesce rides the last), the release (core's reduction), and the
/// token's cancellation and disposal. Synchronous core APIs never run on the
/// dispatcher: <see cref="VaultLifecycleViewModel"/> refuses any call this
/// seam would run on the UI thread.
/// </summary>
/// <remarks>
/// Production runs each call on the thread pool
/// (<see cref="ThreadPoolRescanCoreWorker"/>). The seam exists so the
/// rescan facts can park a scan, fail one, and record where every call ran;
/// the calls themselves are always the session's real ones.
/// </remarks>
internal interface IRescanCoreWorker
{
    /// <summary>Run one core call, named by <paramref name="operation"/>,
    /// off the dispatcher.</summary>
    Task<T> Run<T>(string operation, Func<T> call);
}

/// <summary>The production seam: every rescan core call on the pool.</summary>
internal sealed class ThreadPoolRescanCoreWorker : IRescanCoreWorker
{
    public static ThreadPoolRescanCoreWorker Instance { get; } = new();

    public Task<T> Run<T>(string operation, Func<T> call) => Task.Run(call);
}
