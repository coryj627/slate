// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.IO;
using System.Text;
using uniffi.slate_uniffi;

namespace SlateWindows.Graph;

/// <summary>
/// W6-2 PR C (#746), rule W Term W2: the ONE writer of <c>graph.json</c>
/// in the process — the mac's <c>GraphConfigWriter.shared</c> — with its
/// state per vault KEY (Term W1: <c>Path.GetFullPath</c>, the trailing
/// separator trimmed, compared OrdinalIgnoreCase — the lifecycle's own
/// convention, so <c>C:\Vault</c> and <c>c:\vault\</c> are one key). Plan
/// 44 WB-28 PR 2 (open item 1) keys the state by the ROOT instead — a
/// <see cref="VaultRootScope"/>: the file system's id for a persistent
/// root, so every spelling of one folder is one scope and a spelling that
/// now names another folder is another; the session itself for a weak
/// root, whose ids can collide. A write pins the root at admission and
/// goes through that pin, never a path, and all writes share ONE serial
/// queue, so a reopen's write lands after a straggler's whatever their
/// scopes. Per scope, four facts under one lock: the
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
    private readonly Dictionary<VaultRootScope, VaultState> _vaults = [];

    // ONE tail for the process (WB-28 open item 1): per-scope queues let
    // a weak root's reopen, a new scope, overtake its straggler.
    private Task _tail = Task.CompletedTask;

    private sealed class VaultState
    {
        public ulong Counter;
        public ulong Admitted;
        public ulong LastWritten;
        public readonly SortedDictionary<ulong, GraphConfig> Outstanding = [];
    }

    internal GraphConfigWriter()
    {
    }

    /// <summary>Term W1's key for a vault root.</summary>
    public static string KeyOf(string vaultRoot)
    {
        ArgumentNullException.ThrowIfNull(vaultRoot);
        // The lifecycle's own primitive, which Term W1 cites
        // (`VaultLifecycleViewModel.cs:82-91`, `SyncAnnounceKey`): a bare
        // TrimEnd turns a DRIVE ROOT into a drive-RELATIVE path — `C:\`
        // became `C:`, and the store then wrote `C:.slate\graph.json`
        // under the process's current directory on that drive, leaving the
        // vault's own config untouched (IPG-2). TrimEndingDirectorySeparator
        // leaves a root alone and trims every other trailing separator.
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(vaultRoot));
    }

    /// <summary>The store a write goes to, given the root it pinned at
    /// admission; a fact points the writer at a store of its own.</summary>
    internal Func<VaultRootAnchor, GraphConfigStore> StoreFor { get; set; } =
        root => new GraphConfigStore(root.DisplayPath, root);

    /// <summary>Test seam: runs inside the queue's body, after the dequeue
    /// and before the store's write — a fact parks a write there or makes
    /// it fail.</summary>
    internal Action<VaultRootScope, GraphConfig>? WriteGateForTests { get; set; }

    /// <summary>Reserve the next generation for the root (Term W3 reserves
    /// at schedule time, so generations follow EDIT order).</summary>
    public ulong Reserve(VaultRootAnchor root)
    {
        ArgumentNullException.ThrowIfNull(root);
        lock (_lock)
        {
            VaultState state = StateOf(root.Scope);
            state.Counter++;
            return state.Counter;
        }
    }

    /// <summary>Admit and queue an aggregate under its reserved generation;
    /// the returned task completes after the write's attempt, succeeded or
    /// failed. A superseded generation is dropped at the call. The root is
    /// pinned here, so a write queued while its session lives lands in that
    /// root even when the session has closed by the time it runs; a root
    /// that cannot be pinned fails the write as the store would.</summary>
    public Task Enqueue(VaultRootAnchor root, GraphConfig aggregate, ulong generation)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(aggregate);
        // Pinned before the lock: the pin calls into core and duplicates a
        // handle, which no other writer operation should wait behind.
        VaultRootAnchor pinned;
        try
        {
            pinned = root.Pin();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            HostLog.Write(HostDiagnosticEvent.GraphConfigPersistFailed, exception);
            lock (_lock)
            {
                FailedForTests++;
            }
            return Task.CompletedTask;
        }
        lock (_lock)
        {
            VaultState state = StateOf(root.Scope);
            if (generation <= state.Admitted)
            {
                DroppedAtEnqueueForTests++;
                pinned.Dispose();
                return Task.CompletedTask;
            }
            state.Admitted = generation;
            state.Outstanding[generation] = aggregate;
            Task run = _tail.ContinueWith(
                _ => Write(pinned, state, aggregate, generation),
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);
            _tail = run;
            return run;
        }
    }

    /// <summary>The highest-generation outstanding aggregate — admitted and
    /// not yet attempted — else null (Term W6's read).</summary>
    public GraphConfig? Newest(VaultRootAnchor root)
    {
        ArgumentNullException.ThrowIfNull(root);
        lock (_lock)
        {
            if (!_vaults.TryGetValue(root.Scope, out VaultState? state) || state.Outstanding.Count == 0)
            {
                return null;
            }
            return state.Outstanding[state.Outstanding.Keys.Max()];
        }
    }

    private void Write(VaultRootAnchor pinned, VaultState state, GraphConfig aggregate, ulong generation)
    {
        try
        {
            WriteGateForTests?.Invoke(pinned.Scope, aggregate);
            StoreFor(pinned).Write(aggregate);
            lock (_lock)
            {
                // The outcome and the aggregate's REMOVAL under one lock
                // (IPG-35): they used to be two, and between them `Newest`
                // could hand a reopening workspace the aggregate whose write
                // had just FAILED — where Term W6 says the newest OUTSTANDING
                // one, never a failed one, and after a failure the file.
                state.LastWritten = generation;
                _ = state.Outstanding.Remove(generation);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or DecoderFallbackException or GraphConfigException)
        {
            // Best-effort persistence (the mac's actor): the failure is
            // logged through the privacy-safe host sink, never propagated;
            // the aggregate is lost and the next read is the file's (CR-7).
            HostLog.Write(HostDiagnosticEvent.GraphConfigPersistFailed, exception);
            lock (_lock)
            {
                FailedForTests++;
                _ = state.Outstanding.Remove(generation);
            }
        }
        catch
        {
            // ANY other exception — such as SecurityException or
            // ObjectDisposedException — still leaves nothing outstanding.
            // The `finally` this replaced
            // guaranteed that unconditionally, and dropping it traded
            // IPG-35's interval for a generation stuck in `Outstanding` for
            // the process's life, which is the same misreport for longer
            // (codoki on 4a00a3c5). It propagates as it always did: only the
            // named failures are best-effort.
            lock (_lock)
            {
                _ = state.Outstanding.Remove(generation);
            }
            throw;
        }
        finally
        {
            // The root pinned at admission is released with the write.
            pinned.Dispose();
        }
    }

    private VaultState StateOf(VaultRootScope scope)
    {
        if (!_vaults.TryGetValue(scope, out VaultState? state))
        {
            state = new VaultState();
            _vaults[scope] = state;
        }
        return state;
    }

    internal int DroppedAtEnqueueForTests { get; private set; }

    internal int FailedForTests { get; private set; }

    /// <summary>The process-wide queue's last write: done once every write
    /// enqueued before the read has been attempted.</summary>
    internal Task TailForTests
    {
        get
        {
            lock (_lock)
            {
                return _tail;
            }
        }
    }

    internal (ulong Counter, ulong Admitted, ulong LastWritten, int Outstanding) StateForTests(VaultRootAnchor root)
    {
        lock (_lock)
        {
            VaultState state = StateOf(root.Scope);
            return (state.Counter, state.Admitted, state.LastWritten, state.Outstanding.Count);
        }
    }
}
