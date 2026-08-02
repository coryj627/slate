// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using uniffi.slate_uniffi;

namespace SlateWindows.Panels;

/// <summary>Outcome of one pending-repair sweep.</summary>
internal sealed record RepairSweep(
    string[] Repaired, bool AnyPending, string? LastError);

/// <summary>
/// Shared quarantine for paths whose index repair FAILED after a
/// post-write toggle failure (adversarial rounds 14-15): the index
/// is KNOWN stale for these paths, so any task surface that queries
/// it republishes the rolled-back rows as ghosts. One coordinator is
/// shared by the review, the note panel, and the workspace's toggle
/// recovery, so no surface can bypass another's quarantine. Entries
/// clear when a retry succeeds — or implicitly, when a later
/// successful save commits a fresh index for the path.
///
/// Thread-safe: callers include background load workers and the
/// dispatcher-side toggle completion.
/// </summary>
internal sealed class TaskIndexRepairCoordinator(VaultSession session)
{
    private readonly object _gate = new();
    private readonly HashSet<string> _pending = new(StringComparer.Ordinal);

    public void NotePending(string path)
    {
        lock (_gate)
        {
            _ = _pending.Add(path);
        }
    }

    public bool HasPendingFor(string path)
    {
        lock (_gate)
        {
            return _pending.Contains(path);
        }
    }

    /// <summary>Repair one path NOW; a failure registers it pending.
    /// <paramref name="error"/> carries the failure message for the
    /// caller's honest surface state.</summary>
    public bool TryRepairNow(string path, out string? error)
    {
        try
        {
            session.ReindexPath(path);
            lock (_gate)
            {
                _ = _pending.Remove(path);
            }
            error = null;
            return true;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            error = exception.Message;
            NotePending(path);
            return false;
        }
    }

    /// <summary>Retry every pending repair. Callers must treat
    /// <see cref="RepairSweep.AnyPending"/> as a QUERY BAR: while any
    /// repair remains failed the index is known stale, and querying
    /// it would publish ghost rows (round 15 — the round-14 retry
    /// reported only successes, so loads fell through).</summary>
    public RepairSweep Retry()
    {
        string[] snapshot;
        lock (_gate)
        {
            snapshot = [.. _pending];
        }
        List<string> repaired = [];
        string? lastError = null;
        foreach (string path in snapshot)
        {
            try
            {
                session.ReindexPath(path);
                repaired.Add(path);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                lastError = exception.Message;
            }
        }
        bool anyPending;
        lock (_gate)
        {
            foreach (string path in repaired)
            {
                _ = _pending.Remove(path);
            }
            anyPending = _pending.Count > 0;
        }
        return new RepairSweep([.. repaired], anyPending, lastError);
    }
}
