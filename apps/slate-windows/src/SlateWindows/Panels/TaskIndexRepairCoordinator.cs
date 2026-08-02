// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using uniffi.slate_uniffi;

namespace SlateWindows.Panels;

/// <summary>Outcome of one pending-repair sweep.</summary>
internal sealed record RepairSweep(
    string[] Repaired, bool AnyPending, string? LastError);

/// <summary>
/// Shared quarantine for paths whose index repair FAILED after a
/// post-write toggle failure (adversarial rounds 14-16): the index
/// is KNOWN stale for these paths, so any task surface that queries
/// it republishes the rolled-back rows as ghosts. One coordinator is
/// shared by the review, the note panel, and the workspace's toggle
/// recovery, so no surface can bypass another's quarantine.
///
/// Linearizability (round 16): entries are GENERATION-versioned — a
/// sweep that repaired a path only removes the entry it snapshotted,
/// never a fresh failure registered concurrently — and every state
/// change advances <see cref="Epoch"/>, which query workers validate
/// AFTER querying and before publishing: a post-write failure that
/// registers between the sweep and the query invalidates the result,
/// because a rolled-back index transaction moves no revision counter
/// the query checks could see.
///
/// Thread-safe: callers include background load workers and the
/// dispatcher-side toggle completion.
/// </summary>
internal sealed class TaskIndexRepairCoordinator(VaultSession session)
{
    private readonly object _gate = new();
    private readonly Dictionary<string, long> _pending = new(StringComparer.Ordinal);
    private long _nextGeneration;
    private long _epoch;

    /// <summary>Monotonic stamp of quarantine state: advances on
    /// every registration and removal. Capture it after a clean
    /// sweep and re-validate before publishing query results.</summary>
    public long Epoch
    {
        get
        {
            lock (_gate)
            {
                return _epoch;
            }
        }
    }

    public void NotePending(string path)
    {
        lock (_gate)
        {
            _pending[path] = ++_nextGeneration;
            _epoch++;
        }
    }

    public bool HasPendingFor(string path)
    {
        lock (_gate)
        {
            return _pending.ContainsKey(path);
        }
    }

    /// <summary>Repair one path NOW; a failure registers it pending.
    /// A successful repair only clears the registration it observed
    /// at entry — a fresh failure racing in stays quarantined.</summary>
    public bool TryRepairNow(string path, out string? error)
    {
        long generationAtEntry;
        lock (_gate)
        {
            _ = _pending.TryGetValue(path, out generationAtEntry);
        }
        try
        {
            session.ReindexPath(path);
            RemoveIfUnchanged(path, generationAtEntry);
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
    /// it would publish ghost rows (round 15).</summary>
    public RepairSweep Retry()
    {
        KeyValuePair<string, long>[] snapshot;
        lock (_gate)
        {
            snapshot = [.. _pending];
        }
        List<string> repaired = [];
        string? lastError = null;
        foreach ((string path, long generation) in snapshot)
        {
            try
            {
                session.ReindexPath(path);
                BetweenRepairAndRemovalForTests?.Invoke();
                if (RemoveIfUnchanged(path, generation))
                {
                    repaired.Add(path);
                }
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                lastError = exception.Message;
            }
        }
        bool anyPending;
        lock (_gate)
        {
            anyPending = _pending.Count > 0;
        }
        return new RepairSweep([.. repaired], anyPending, lastError);
    }

    /// <summary>Test seam (round 16): runs between a sweep's
    /// successful repair and its removal — the window where a fresh
    /// concurrent failure must survive the removal.</summary>
    internal Action? BetweenRepairAndRemovalForTests { get; set; }

    /// <summary>Remove the path's entry only if its registration
    /// generation has not moved past what the repairing caller
    /// observed (round 16: an unversioned removal erased fresh
    /// failures registered mid-repair).</summary>
    private bool RemoveIfUnchanged(string path, long observedGeneration)
    {
        lock (_gate)
        {
            if (_pending.TryGetValue(path, out long current)
                && current <= observedGeneration)
            {
                _ = _pending.Remove(path);
                _epoch++;
                return true;
            }
            // Not pending (nothing to clear) or re-registered by a
            // NEWER failure — either way the entry is not ours to
            // remove. A pure TryRepairNow with no prior registration
            // still counts as success for the caller.
            return !_pending.ContainsKey(path);
        }
    }
}
