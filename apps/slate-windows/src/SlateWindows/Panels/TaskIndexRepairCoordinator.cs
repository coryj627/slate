// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using uniffi.slate_uniffi;

namespace SlateWindows.Panels;

/// <summary>Outcome of one pending-repair sweep.</summary>
internal sealed record RepairSweep(
    string[] Repaired, bool AnyPending, string? LastError);

/// <summary>
/// Shared quarantine for paths whose index repair FAILED after a
/// post-write toggle failure (adversarial rounds 14-18): the index
/// is KNOWN stale for these paths, so any task surface that queries
/// it republishes the rolled-back rows as ghosts. One coordinator is
/// shared by the review, the note panel, and the workspace's toggle
/// recovery, so no surface can bypass another's quarantine.
///
/// Protocol (rounds 16-18):
/// - Entries are GENERATION-versioned: a repair only clears the
///   registration it installed or snapshotted — fresh failures
///   racing in survive.
/// - Every registration and removal advances <see cref="Epoch"/>.
///   Repairs REGISTER before attempting, so every repair interval
///   is visible to overlapping queries.
/// - Queries obtain an ATOMIC clean-state ticket
///   (<see cref="TryBeginCleanQuery"/>) — pending-check and epoch
///   capture under one lock — and re-validate the epoch after
///   reading: a split check-then-capture let a failure register in
///   the gap with its advanced epoch captured as the baseline.
/// - Repairs are SINGLE-FLIGHT per path (a per-path lock held
///   across the reindex), and a sweep revalidates its snapshot
///   after acquiring ownership: overlapping same-path repairs could
///   otherwise erase the only active registration or complete
///   invisibly.
///
/// Thread-safe: callers include background load workers and the
/// dispatcher-side toggle completion. Lock order: path lock, then
/// <c>_gate</c>; nothing acquires them in the other order.
/// </summary>
internal sealed class TaskIndexRepairCoordinator(VaultSession session)
{
    private readonly object _gate = new();
    private readonly Dictionary<string, long> _pending = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _leases = new(StringComparer.Ordinal);
    private readonly Dictionary<string, object> _pathLocks = new(StringComparer.Ordinal);
    private long _nextGeneration;
    private long _epoch;

    /// <summary>Take a mutation lease BEFORE a task write
    /// (adversarial round 19): the stale-index interval starts the
    /// moment the core writes the file, not when the failure reaches
    /// the dispatcher — a query in between could obtain a clean
    /// ticket, read the rolled-back index, and validate an unmoved
    /// epoch. While any lease is active, clean tickets refuse (as
    /// transiently lease-blocked) and the epoch has advanced, so
    /// tickets taken earlier invalidate.</summary>
    public void BeginMutation(string path)
    {
        lock (_gate)
        {
            _leases[path] = _leases.GetValueOrDefault(path) + 1;
            _epoch++;
        }
    }

    /// <summary>Release a mutation lease. <paramref
    /// name="indexConsistent"/> is true only for a successful
    /// indexed commit or a CERTAIN no-write (a pre-write conflict);
    /// anything else converts the lease into a pending repair in
    /// the SAME lock hold — there is never an instant where neither
    /// a lease nor a pending entry covers the stale interval.</summary>
    public void EndMutation(string path, bool indexConsistent)
    {
        lock (_gate)
        {
            int count = _leases.GetValueOrDefault(path);
            if (count <= 1)
            {
                _ = _leases.Remove(path);
            }
            else
            {
                _leases[path] = count - 1;
            }
            _epoch++;
            if (!indexConsistent)
            {
                _pending[path] = ++_nextGeneration;
            }
        }
    }

    /// <summary>Monotonic stamp of quarantine state: advances on
    /// every registration and removal. Prefer
    /// <see cref="TryBeginCleanQuery"/> for the initial capture and
    /// use this for the post-read comparison.</summary>
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

    /// <summary>Atomically verify that NOTHING is pending or leased
    /// and hand back the epoch from the same lock hold (adversarial
    /// rounds 18-19): checking and capturing separately let a
    /// failing repair register in the gap — its advanced epoch
    /// became the caller's baseline, so the post-read comparison
    /// passed over a known-stale read. <paramref
    /// name="blockedOnlyByLeases"/> distinguishes a transient
    /// in-flight write (retry briefly) from a failed repair (bar the
    /// query honestly).</summary>
    public bool TryBeginCleanQuery(out long epoch, out bool blockedOnlyByLeases)
    {
        lock (_gate)
        {
            epoch = _epoch;
            blockedOnlyByLeases = _pending.Count == 0 && _leases.Count > 0;
            return _pending.Count == 0 && _leases.Count == 0;
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

    /// <summary>Repair one path NOW; a failure leaves it pending.
    /// Single-flight per path, and the path is REGISTERED before the
    /// repair is attempted (rounds 17-18): the repair interval must
    /// be visible to every overlapping query's epoch validation, and
    /// two same-path attempts must never interleave — an overwrite
    /// of the sole generation let one attempt erase the other's
    /// active registration or complete invisibly.</summary>
    public bool TryRepairNow(string path, out string? error)
    {
        lock (PathLock(path))
        {
            long generation;
            lock (_gate)
            {
                generation = ++_nextGeneration;
                _pending[path] = generation;
                _epoch++;
            }
            try
            {
                // Repair-or-contain (adversarial round 31): a path
                // whose repair persistently fails on a FILE
                // condition must not bar every task surface forever.
                // Contained means the core dropped the path's
                // suspect rows honest-empty and planted a
                // self-healing durable marker — queries are safe, so
                // the gate releases exactly like a repair. Declined
                // (another process committed mid-attempt) keeps the
                // registration for the next retry; database
                // failures throw and stay pending — loud.
                TaskIndexRepairOutcome outcome = session.RepairOrContainPath(path);
                if (outcome == TaskIndexRepairOutcome.Declined)
                {
                    error = "repair declined by a concurrent write; will retry";
                    return false;
                }
                _ = RemoveIfUnchanged(path, generation);
                error = null;
                return true;
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                // The pre-registration stays: the path remains
                // quarantined until a later attempt lands.
                error = exception.Message;
                return false;
            }
        }
    }

    /// <summary>Retry every pending repair. Callers must follow with
    /// <see cref="TryBeginCleanQuery"/> — the sweep's own outcome is
    /// advisory (retries plus the repaired list for cross-surface
    /// refresh); only the atomic ticket proves a clean index.</summary>
    public RepairSweep Retry()
    {
        string[] snapshot;
        lock (_gate)
        {
            snapshot = [.. _pending.Keys];
        }
        List<string> repaired = [];
        string? lastError = null;
        foreach (string path in snapshot)
        {
            lock (PathLock(path))
            {
                // Revalidate AFTER acquiring ownership (round 18): a
                // concurrent repair may have handled this path while
                // we waited — its interval already advanced the
                // epoch, and repairing blind here would race the
                // registrations.
                long current;
                lock (_gate)
                {
                    if (!_pending.TryGetValue(path, out current))
                    {
                        continue;
                    }
                }
                try
                {
                    // Round 31: Contained releases the gate exactly
                    // like Repaired (queries are safe; the core's
                    // planted marker heals cross-process). Declined
                    // stays pending for the next sweep.
                    TaskIndexRepairOutcome outcome = session.RepairOrContainPath(path);
                    BetweenRepairAndRemovalForTests?.Invoke();
                    if (outcome == TaskIndexRepairOutcome.Declined)
                    {
                        lastError = "repair declined by a concurrent write; will retry";
                    }
                    else if (RemoveIfUnchanged(path, current))
                    {
                        repaired.Add(path);
                    }
                }
                catch (Exception exception) when (
                    exception is not OutOfMemoryException)
                {
                    lastError = exception.Message;
                }
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
    /// concurrent failure must survive the removal. Runs while the
    /// path's single-flight lock is held.</summary>
    internal Action? BetweenRepairAndRemovalForTests { get; set; }

    private object PathLock(string path)
    {
        lock (_gate)
        {
            if (!_pathLocks.TryGetValue(path, out object? pathLock))
            {
                pathLock = new object();
                _pathLocks[path] = pathLock;
            }
            return pathLock;
        }
    }

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
            // remove. A repair with no surviving registration still
            // counts as success for the caller.
            return !_pending.ContainsKey(path);
        }
    }
}
