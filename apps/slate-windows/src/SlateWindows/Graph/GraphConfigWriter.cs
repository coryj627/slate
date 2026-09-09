// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;
using System.IO;
using uniffi.slate_uniffi;

namespace SlateWindows.Graph;

/// <summary>
/// W6-2 PR C (#746), rule W Term W2: the ONE writer of <c>graph.json</c>
/// in the process — the mac's <c>GraphConfigWriter.shared</c> — with its
/// state per vault KEY (Term W1: <c>Path.GetFullPath</c>, the trailing
/// separator trimmed, compared OrdinalIgnoreCase — the lifecycle's own
/// convention, so <c>C:\Vault</c> and <c>c:\vault\</c> are one key). Per
/// key, a serial queue on the pool and four facts under one lock: the
/// generation COUNTER, the highest ADMITTED generation, the OUTSTANDING
/// admitted aggregates and the LAST SUCCESSFUL write's generation. Three
/// operations: <see cref="Reserve"/> (synchronous, monotonic, never reset
/// in the process), <see cref="Enqueue"/> (admission ATOMIC under the
/// lock — a generation at or below the highest admitted is dropped at
/// the call, never queued; an admitted aggregate joins the outstanding
/// set and the queue, and its write removes it whether it succeeded or
/// failed, the failure logged) and <see cref="Newest"/> (the
/// highest-generation OUTSTANDING aggregate, never a doomed or a failed
/// one). A test constructs its own instance; production has
/// <see cref="Shared"/> alone (the writer census).
/// </summary>
internal sealed class GraphConfigWriter
{
    public static GraphConfigWriter Shared { get; } = new();

    private readonly object _lock = new();
    private readonly Dictionary<string, VaultState> _vaults = new(StringComparer.OrdinalIgnoreCase);

    private sealed class VaultState
    {
        public ulong Counter;
        public ulong Admitted;
        public ulong LastWritten;
        public readonly SortedDictionary<ulong, GraphConfig> Outstanding = [];
        public Task Tail = Task.CompletedTask;
    }

    internal GraphConfigWriter()
    {
    }

    /// <summary>Term W1's key for a vault root.</summary>
    public static string KeyOf(string vaultRoot)
    {
        ArgumentNullException.ThrowIfNull(vaultRoot);
        return Path.GetFullPath(vaultRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    /// <summary>The store a key's writes go to — the key is a full path;
    /// a fact points the writer at a store of its own.</summary>
    internal Func<string, GraphConfigStore> StoreFor { get; set; } = key => new GraphConfigStore(key);

    /// <summary>Test seam: runs inside the queue's body, after the dequeue
    /// and before the store's write — a fact parks a write there or makes
    /// it fail.</summary>
    internal Action<string, GraphConfig>? WriteGateForTests { get; set; }

    /// <summary>Reserve the next generation for the key (Term W3 reserves at
    /// schedule time, so generations follow EDIT order).</summary>
    public ulong Reserve(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        lock (_lock)
        {
            VaultState state = StateOf(key);
            state.Counter++;
            return state.Counter;
        }
    }

    /// <summary>Admit and queue an aggregate under its reserved generation;
    /// the returned task completes after the write's attempt, succeeded or
    /// failed. A superseded generation is dropped at the call.</summary>
    public Task Enqueue(string key, GraphConfig aggregate, ulong generation)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(aggregate);
        lock (_lock)
        {
            VaultState state = StateOf(key);
            if (generation <= state.Admitted)
            {
                DroppedAtEnqueueForTests++;
                return Task.CompletedTask;
            }
            state.Admitted = generation;
            state.Outstanding[generation] = aggregate;
            Task run = state.Tail.ContinueWith(
                _ => Write(key, state, aggregate, generation),
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);
            state.Tail = run;
            return run;
        }
    }

    /// <summary>The highest-generation outstanding aggregate — admitted and
    /// not yet attempted — else null (Term W6's read).</summary>
    public GraphConfig? Newest(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        lock (_lock)
        {
            if (!_vaults.TryGetValue(KeyOf(key), out VaultState? state) || state.Outstanding.Count == 0)
            {
                return null;
            }
            return state.Outstanding[state.Outstanding.Keys.Max()];
        }
    }

    private void Write(string key, VaultState state, GraphConfig aggregate, ulong generation)
    {
        try
        {
            WriteGateForTests?.Invoke(key, aggregate);
            StoreFor(key).Write(aggregate);
            lock (_lock)
            {
                state.LastWritten = generation;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or GraphConfigException)
        {
            // Best-effort persistence (the mac's actor): the failure is
            // logged with the full vault path, never propagated; the
            // aggregate is lost and the next read is the file's (CR-7).
            Trace.TraceWarning("Failed to persist graph.json for vault '{0}': {1}", key, exception.Message);
            lock (_lock)
            {
                FailedForTests++;
            }
        }
        finally
        {
            lock (_lock)
            {
                _ = state.Outstanding.Remove(generation);
            }
        }
    }

    private VaultState StateOf(string key)
    {
        string normalised = KeyOf(key);
        if (!_vaults.TryGetValue(normalised, out VaultState? state))
        {
            state = new VaultState();
            _vaults[normalised] = state;
        }
        return state;
    }

    internal int DroppedAtEnqueueForTests { get; private set; }

    internal int FailedForTests { get; private set; }

    internal (ulong Counter, ulong Admitted, ulong LastWritten, int Outstanding) StateForTests(string key)
    {
        lock (_lock)
        {
            VaultState state = StateOf(key);
            return (state.Counter, state.Admitted, state.LastWritten, state.Outstanding.Count);
        }
    }
}
