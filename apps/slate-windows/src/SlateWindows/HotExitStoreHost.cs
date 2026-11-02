// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using uniffi.slate_uniffi;

namespace SlateWindows;

/// <summary>Plan 44 WB-29 §1: the kind of a call the store host makes.</summary>
internal enum HotExitCallKind
{
    Epoch,
    Read,
    Claim,
    Write,
    Retire,
    Discard,
}

/// <summary>What the test hooks see of a store call (WB-29 §1).</summary>
internal sealed record HotExitStoreCall(
    HotExitCallKind Kind,
    string KeyText,
    string? EntryId,
    IReadOnlyList<string> Incarnations,
    ulong Generation);

/// <summary>A failure a test hook injects (WB-29 §1). From
/// <c>BeforeCallForTests</c> the call is never issued; from
/// <c>AfterCallForTests</c> it was issued and its result is replaced (the
/// "errored after it reached the disk" shape).</summary>
internal abstract record HotExitFault
{
    internal sealed record Io : HotExitFault;

    internal sealed record Damaged : HotExitFault;

    internal sealed record Refused(HotExitRefusal Refusal) : HotExitFault;
}

/// <summary>How a store call resolved. The task carrying it never faults: a
/// store error is an outcome with core's reason (WB-29 §1).</summary>
internal sealed record HotExitResult<T>(T? Value, string? ErrorReason, bool Damaged, bool Moot)
{
    internal bool Succeeded => ErrorReason is null && !Moot;

    internal static HotExitResult<T> Ok(T value) => new(value, null, false, false);

    internal static HotExitResult<T> Failed(string reason, bool damaged = false) =>
        new(default, reason, damaged, false);

    /// <summary>The call can no longer change the store's outcome (§0 O5).</summary>
    internal static HotExitResult<T> Overtaken() => new(default, null, false, true);
}

/// <summary>What opening a vault's store held: the hold, or why not (§1).</summary>
internal sealed record HotExitHoldResult(bool Held, string? Reason);

/// <summary>
/// Plan 44 WB-29 §0-§1: the one <see cref="HotExitStore"/> of the process, the
/// ledger of every store call the host has made per vault key, and every call
/// in flight. It lives as long as the process, so its obligations outlive tabs
/// and workspaces. Nothing joins it: a store call is never a tracked worker,
/// disposal waits on nothing, and a call cut short by process exit is a crash.
/// Calls take the vault's key, never the session.
/// </summary>
internal sealed class HotExitStoreHost
{
    /// <summary>A shell built without installation (the tests that assert
    /// today's prompt) has no store and no reason: it keeps the prompt and
    /// posts nothing (§1, §6).</summary>
    internal static HotExitStoreHost Shared { get; private set; } = new(null, null);

    private readonly HotExitStore? _store;
    private readonly object _gate = new();
    private readonly Dictionary<string, VaultLedger> _ledgers = new(StringComparer.Ordinal);

    internal HotExitStoreHost(HotExitStore? store, string? unavailableReason)
    {
        _store = store;
        UnavailableReason = unavailableReason;
    }

    internal bool HasStore => _store is not null;

    /// <summary>Core's reason this host has no store; null for a host never
    /// installed.</summary>
    internal string? UnavailableReason { get; }

    /// <summary>Runs on the call's own thread before the store. May block (a
    /// held call). A fault means the call is never issued.</summary>
    internal Func<HotExitStoreCall, HotExitFault?>? BeforeCallForTests { get; set; }

    /// <summary>Runs on the call's own thread after the store. May block. A
    /// fault replaces the store's result.</summary>
    internal Func<HotExitStoreCall, HotExitFault?>? AfterCallForTests { get; set; }

    /// <summary>A failed retirement's first retry delay; doubles to the cap
    /// (§1). Facts shorten it.</summary>
    internal TimeSpan RetirementRetryDelay { get; set; } = TimeSpan.FromSeconds(1);

    internal TimeSpan RetirementRetryCap { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Called beside the journal's installation, before any vault
    /// opens (App.OnStartup).</summary>
    internal static void InstallProductionLocation()
    {
        Shared = Install(SlateDeviceState.Root, SyncParentDirectory);
    }

    /// <summary>Makes the device root exist and durable in its parent, on
    /// every install whoever created it, then opens the store. Any failure is
    /// a host with no store and core's reason, logged once (§1).</summary>
    internal static HotExitStoreHost Install(string root, Func<string, bool> syncParent)
    {
        try
        {
            Directory.CreateDirectory(root);
            if (!syncParent(root))
            {
                HostLog.Write(HostDiagnosticEvent.HotExitStoreUnavailable);
                return new HotExitStoreHost(null, SlateUniffiMethods.HotExitReasonStoreUnwritable());
            }
            return new HotExitStoreHost(HotExitStore.Open(root), null);
        }
        catch (HotExitException exception)
        {
            HostLog.Write(HostDiagnosticEvent.HotExitStoreUnavailable, exception);
            return new HotExitStoreHost(null, ReasonOf(exception));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            HostLog.Write(HostDiagnosticEvent.HotExitStoreUnavailable, exception);
            return new HotExitStoreHost(null, SlateUniffiMethods.HotExitReasonStoreUnwritable());
        }
    }

    /// <summary>Core's reason sentence fragment for a store error.</summary>
    internal static string ReasonOf(HotExitException exception) => exception switch
    {
        HotExitException.Io io => io.@reason,
        HotExitException.InvalidEntryId invalid => invalid.@reason,
        HotExitException.InvalidIncarnation invalid => invalid.@reason,
        HotExitException.Damaged damaged => damaged.@reason,
        HotExitException.HeldElsewhere held => held.@reason,
        _ => SlateUniffiMethods.HotExitReasonStoreUnwritable(),
    };

    // ---- Holds (§1) ---------------------------------------------------------

    /// <summary>Acquires the vault's hold for an opening session, or reuses
    /// the one this process still owns. Another process's hold, or any other
    /// failure, says why there is no hot exit.</summary>
    internal HotExitHoldResult AcquireHold(HotExitVaultKey key)
    {
        if (_store is null)
        {
            return new HotExitHoldResult(false, UnavailableReason);
        }
        lock (_gate)
        {
            VaultLedger ledger = LedgerFor(key);
            if (ledger.Hold is null)
            {
                try
                {
                    ledger.Hold = _store.HoldVault(key);
                }
                catch (HotExitException.HeldElsewhere held)
                {
                    return new HotExitHoldResult(false, held.@reason);
                }
                catch (HotExitException exception)
                {
                    HostLog.Write(HostDiagnosticEvent.HotExitStoreUnavailable, exception);
                    return new HotExitHoldResult(false, SlateUniffiMethods.HotExitReasonStoreUnwritable());
                }
            }
            ledger.OpenSessions++;
            return new HotExitHoldResult(true, null);
        }
    }

    /// <summary>The session that acquired the hold is gone: a closed vault, a
    /// failed open, or disposal without a teardown. The hold is freed once no
    /// obligation of the key is unresolved.</summary>
    internal void ReleaseSession(HotExitVaultKey key)
    {
        lock (_gate)
        {
            VaultLedger ledger = LedgerFor(key);
            ledger.OpenSessions = Math.Max(0, ledger.OpenSessions - 1);
            ReleaseHoldIfIdle(ledger);
        }
    }

    internal bool HoldsVaultForTests(HotExitVaultKey key)
    {
        lock (_gate)
        {
            return LedgerFor(key).Hold is not null;
        }
    }

    /// <summary>The crash shape's "new process": drops every hold as the OS
    /// does at exit, and resolves every call still held in
    /// <see cref="BeforeCallForTests"/> unissued (a dead process never issues
    /// it). A call already issued has done whatever it did.</summary>
    internal void SimulateProcessDeathForTests()
    {
        lock (_gate)
        {
            _dead = true;
            foreach (VaultLedger ledger in _ledgers.Values)
            {
                ledger.Hold?.Release();
                ledger.Hold = null;
                ledger.OpenSessions = 0;
            }
        }
    }

    private volatile bool _dead;

    private void ReleaseHoldIfIdle(VaultLedger ledger)
    {
        if (ledger.OpenSessions == 0 && ledger.Hold is not null && !ledger.HasUnresolved)
        {
            ledger.Hold.Release();
            ledger.Hold = null;
        }
    }

    // ---- The ledger (§0) ----------------------------------------------------

    /// <summary>Whether any obligation of the key is still unresolved (a moot
    /// one counts as resolved, O5).</summary>
    internal bool HasUnresolved(HotExitVaultKey key)
    {
        lock (_gate)
        {
            return LedgerFor(key).HasUnresolved;
        }
    }

    /// <summary>Whether any vault discard of the key is unresolved (O6).</summary>
    internal bool HasUnresolvedDiscard(HotExitVaultKey key)
    {
        lock (_gate)
        {
            return LedgerFor(key).UnresolvedDiscards > 0;
        }
    }

    /// <summary>Completes when no obligation of the key is unresolved. Never
    /// faults; a caller bounds its own wait.</summary>
    internal Task WhenSettled(HotExitVaultKey key)
    {
        lock (_gate)
        {
            VaultLedger ledger = LedgerFor(key);
            if (!ledger.HasUnresolved)
            {
                return Task.CompletedTask;
            }
            ledger.Settled ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            return ledger.Settled.Task;
        }
    }

    /// <summary>Retries every parked retirement of the key now: a settle, and
    /// a reopen before it waits (§1).</summary>
    internal void RetryParkedRetirements(HotExitVaultKey key)
    {
        List<QueuedCall> ready = [];
        lock (_gate)
        {
            foreach (EntryQueue queue in LedgerFor(key).Queues.Values)
            {
                if (queue.Calls.First?.Value is { Parked: true, Running: false } head)
                {
                    head.Parked = false;
                    head.Running = true;
                    ready.Add(head);
                }
            }
        }
        foreach (QueuedCall call in ready)
        {
            Start(call);
        }
    }

    // ---- Calls --------------------------------------------------------------

    internal Task<HotExitResult<ulong>> EpochAsync(HotExitVaultKey key) =>
        Unqueued(
            key,
            new HotExitStoreCall(HotExitCallKind.Epoch, key.@text, null, [], 0),
            store => store.CurrentEpoch(key));

    internal Task<HotExitResult<HotExitSnapshot>> ReadAsync(HotExitVaultKey key) =>
        Unqueued(
            key,
            new HotExitStoreCall(HotExitCallKind.Read, key.@text, null, [], 0),
            store => store.ReadBuffers(key));

    /// <summary>A write is ordered by the claim it carries and has no queue
    /// (O2).</summary>
    internal Task<HotExitResult<HotExitWriteOutcome>> WriteAsync(
        HotExitVaultKey key,
        HotExitClaim claim,
        ulong generation,
        HotExitEntryWrite entry) =>
        Unqueued(
            key,
            new HotExitStoreCall(
                HotExitCallKind.Write,
                key.@text,
                claim.@entryId,
                [claim.@incarnation],
                generation),
            store => store.WriteBuffer(key, claim, generation, entry),
            incarnations: [claim.@incarnation],
            claimedAtEpoch: claim.@epoch);

    /// <summary>A discard joins no entry's queue: it fences at the epoch.
    /// Only a returned epoch commits it (§0 O5), and that makes older calls
    /// moot.</summary>
    internal Task<HotExitResult<ulong>> DiscardAsync(HotExitVaultKey key) =>
        Unqueued(
            key,
            new HotExitStoreCall(HotExitCallKind.Discard, key.@text, null, [], 0),
            store =>
            {
                ulong epoch = store.DiscardVault(key);
                OnDiscardCommitted(key, epoch);
                return epoch;
            },
            isDiscard: true);

    /// <summary>A claim runs in order for its entry id (O2). It carries the
    /// incarnation made for it, so a retry after an error is idempotent and a
    /// retirement can name it either way.</summary>
    internal Task<HotExitResult<HotExitClaimOutcome>> ClaimAsync(
        HotExitVaultKey key,
        ulong epoch,
        string entryId,
        string? expected,
        string incarnation) =>
        Queued(
            key,
            entryId,
            new HotExitStoreCall(HotExitCallKind.Claim, key.@text, entryId, [incarnation], 0),
            store => store.ClaimEntry(key, epoch, entryId, expected, incarnation),
            incarnations: [incarnation],
            claimedAtEpoch: epoch,
            parksOnError: false);

    /// <summary>A retirement runs in order for its entry id. It resolves only
    /// once the store confirms it: an error parks it at the head of its queue,
    /// and it is retried on a backoff, when the next call for its entry is
    /// queued, and by every settle (§0, §1). Damage resolves it with its
    /// reason, so a damaged vault never pins the hold.</summary>
    internal Task<HotExitResult<bool>> RetireAsync(
        HotExitVaultKey key,
        string entryId,
        IReadOnlyList<string> incarnations,
        ulong claimedAtEpoch) =>
        Queued(
            key,
            entryId,
            new HotExitStoreCall(HotExitCallKind.Retire, key.@text, entryId, incarnations, 0),
            store =>
            {
                store.RetireEntry(key, entryId, [.. incarnations]);
                OnRetirementConfirmed(key, entryId, incarnations);
                return true;
            },
            incarnations: incarnations,
            claimedAtEpoch: claimedAtEpoch,
            parksOnError: true);

    private Task<HotExitResult<T>> Unqueued<T>(
        HotExitVaultKey key,
        HotExitStoreCall call,
        Func<HotExitStore, T> body,
        IReadOnlyList<string>? incarnations = null,
        ulong? claimedAtEpoch = null,
        bool isDiscard = false)
    {
        if (_store is null)
        {
            return Task.FromResult(HotExitResult<T>.Failed(
                UnavailableReason ?? SlateUniffiMethods.HotExitReasonStoreUnwritable()));
        }
        Obligation obligation = new(call.Kind, call.EntryId, incarnations ?? [], claimedAtEpoch);
        var completion = new TaskCompletionSource<HotExitResult<T>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            VaultLedger ledger = LedgerFor(key);
            ledger.Add(obligation);
            if (isDiscard)
            {
                ledger.UnresolvedDiscards++;
            }
            obligation.OnMoot = () => completion.TrySetResult(HotExitResult<T>.Overtaken());
        }
        _ = Task.Factory.StartNew(
            () =>
            {
                HotExitResult<T> result = Invoke(call, body);
                lock (_gate)
                {
                    VaultLedger ledger = LedgerFor(key);
                    if (isDiscard)
                    {
                        ledger.UnresolvedDiscards--;
                    }
                    Resolve(ledger, obligation);
                }
                completion.TrySetResult(result);
            },
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
        return completion.Task;
    }

    private Task<HotExitResult<T>> Queued<T>(
        HotExitVaultKey key,
        string entryId,
        HotExitStoreCall call,
        Func<HotExitStore, T> body,
        IReadOnlyList<string> incarnations,
        ulong claimedAtEpoch,
        bool parksOnError)
    {
        if (_store is null)
        {
            return Task.FromResult(HotExitResult<T>.Failed(
                UnavailableReason ?? SlateUniffiMethods.HotExitReasonStoreUnwritable()));
        }
        var completion = new TaskCompletionSource<HotExitResult<T>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        Obligation obligation = new(call.Kind, entryId, incarnations, claimedAtEpoch);
        QueuedCall? start = null;
        lock (_gate)
        {
            VaultLedger ledger = LedgerFor(key);
            ledger.Add(obligation);
            EntryQueue queue = ledger.QueueFor(entryId);
            QueuedCall queued = new(ledger, queue, obligation);
            queued.Run = () =>
            {
                HotExitResult<T> result = Invoke(call, body);
                bool parked = parksOnError && result.ErrorReason is not null && !result.Damaged && !_dead;
                OnQueuedCallFinished(queued, parked);
                if (!parked)
                {
                    completion.TrySetResult(result);
                }
            };
            obligation.OnMoot = () =>
            {
                lock (_gate)
                {
                    queued.Queue.Calls.Remove(queued);
                }
                completion.TrySetResult(HotExitResult<T>.Overtaken());
            };
            queue.Calls.AddLast(queued);
            // First in its queue it starts now; behind a parked retirement it
            // gets that retirement retried first (§1).
            if (queue.Calls.First?.Value is { Running: false } head)
            {
                head.Parked = false;
                head.Running = true;
                start = head;
            }
        }
        if (start is not null)
        {
            Start(start);
        }
        return completion.Task;
    }

    private void Start(QueuedCall call) =>
        Task.Factory.StartNew(
            call.Run,
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);

    private void OnQueuedCallFinished(QueuedCall call, bool parked)
    {
        QueuedCall? next = null;
        TimeSpan? retryAfter = null;
        lock (_gate)
        {
            call.Running = false;
            if (parked)
            {
                call.Parked = true;
                retryAfter = call.NextRetryDelay ?? RetirementRetryDelay;
                call.NextRetryDelay = TimeSpan.FromTicks(
                    Math.Min(retryAfter.Value.Ticks * 2, RetirementRetryCap.Ticks));
            }
            else
            {
                call.Queue.Calls.Remove(call);
                Resolve(call.Ledger, call.Obligation);
                if (call.Queue.Calls.First?.Value is { Running: false } head)
                {
                    head.Parked = false;
                    head.Running = true;
                    next = head;
                }
            }
        }
        if (next is not null)
        {
            Start(next);
        }
        if (retryAfter is not null)
        {
            _ = Task.Delay(retryAfter.Value).ContinueWith(
                _ => RetryParked(call),
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);
        }
    }

    private void RetryParked(QueuedCall call)
    {
        lock (_gate)
        {
            if (!call.Parked || call.Running || _dead || call.Queue.Calls.First?.Value != call)
            {
                return;
            }
            call.Parked = false;
            call.Running = true;
        }
        Start(call);
    }

    private HotExitResult<T> Invoke<T>(HotExitStoreCall call, Func<HotExitStore, T> body)
    {
        try
        {
            if (BeforeCallForTests?.Invoke(call) is { } before)
            {
                return FromFault<T>(before);
            }
            if (_dead)
            {
                return HotExitResult<T>.Failed(SlateUniffiMethods.HotExitReasonStoreUnwritable());
            }
            T value = body(_store!);
            if (AfterCallForTests?.Invoke(call) is { } after)
            {
                return FromFault<T>(after);
            }
            return HotExitResult<T>.Ok(value);
        }
        catch (HotExitException.Damaged damaged)
        {
            return HotExitResult<T>.Failed(damaged.@reason, damaged: true);
        }
        catch (HotExitException exception)
        {
            return HotExitResult<T>.Failed(ReasonOf(exception));
        }
#pragma warning disable CA1031 // A store call's task never faults (§1).
        catch (Exception exception)
#pragma warning restore CA1031
        {
            HostLog.Write(HostDiagnosticEvent.HotExitStoreUnavailable, exception);
            return HotExitResult<T>.Failed(SlateUniffiMethods.HotExitReasonStoreUnwritable());
        }
    }

    private static HotExitResult<T> FromFault<T>(HotExitFault fault) => fault switch
    {
        // A fact that asserts damage's own sentence makes real damage through
        // the core store; an injected one only needs to act as damage.
        HotExitFault.Damaged => HotExitResult<T>.Failed(
            SlateUniffiMethods.HotExitReasonStoreUnwritable(),
            damaged: true),
        HotExitFault.Refused refused => HotExitResult<T>.Failed(
            SlateUniffiMethods.HotExitRefusalReason(refused.Refusal)),
        _ => HotExitResult<T>.Failed(SlateUniffiMethods.HotExitReasonStoreUnwritable()),
    };

    private void Resolve(VaultLedger ledger, Obligation obligation)
    {
        ledger.Remove(obligation);
        Signal(ledger);
    }

    private void Signal(VaultLedger ledger)
    {
        if (!ledger.HasUnresolved)
        {
            ledger.Settled?.TrySetResult();
            ledger.Settled = null;
            ReleaseHoldIfIdle(ledger);
        }
    }

    /// <summary>§0 O5: a confirmed retirement makes moot every write or claim
    /// of the incarnations it named.</summary>
    private void OnRetirementConfirmed(
        HotExitVaultKey key,
        string entryId,
        IReadOnlyList<string> incarnations)
    {
        MakeMoot(key, obligation =>
            obligation.Kind is HotExitCallKind.Write or HotExitCallKind.Claim
            && obligation.EntryId == entryId
            && obligation.Incarnations.Any(incarnations.Contains));
    }

    /// <summary>§0 O5: a committed discard makes moot every claim, write or
    /// retirement of an incarnation claimed at an epoch it passed.</summary>
    private void OnDiscardCommitted(HotExitVaultKey key, ulong newEpoch)
    {
        MakeMoot(key, obligation =>
            obligation.Kind is HotExitCallKind.Write or HotExitCallKind.Claim or HotExitCallKind.Retire
            && obligation.ClaimedAtEpoch < newEpoch);
    }

    private void MakeMoot(HotExitVaultKey key, Func<Obligation, bool> covered)
    {
        List<Action> notify = [];
        List<QueuedCall> ready = [];
        lock (_gate)
        {
            VaultLedger ledger = LedgerFor(key);
            foreach (Obligation obligation in ledger.Unresolved.Where(covered).ToList())
            {
                ledger.Remove(obligation);
                if (obligation.OnMoot is { } onMoot)
                {
                    notify.Add(onMoot);
                }
            }
            Signal(ledger);
        }
        foreach (Action action in notify)
        {
            action();
        }
        lock (_gate)
        {
            // A moot head leaves its queue (O5): whatever waited behind it
            // runs now.
            foreach (EntryQueue queue in LedgerFor(key).Queues.Values)
            {
                if (queue.Calls.First?.Value is { Running: false } head)
                {
                    head.Parked = false;
                    head.Running = true;
                    ready.Add(head);
                }
            }
        }
        foreach (QueuedCall call in ready)
        {
            Start(call);
        }
    }

    private VaultLedger LedgerFor(HotExitVaultKey key)
    {
        if (!_ledgers.TryGetValue(key.@text, out VaultLedger? ledger))
        {
            ledger = new VaultLedger();
            _ledgers[key.@text] = ledger;
        }
        return ledger;
    }

    private sealed class Obligation(
        HotExitCallKind kind,
        string? entryId,
        IReadOnlyList<string> incarnations,
        ulong? claimedAtEpoch)
    {
        internal HotExitCallKind Kind { get; } = kind;

        internal string? EntryId { get; } = entryId;

        internal IReadOnlyList<string> Incarnations { get; } = incarnations;

        internal ulong? ClaimedAtEpoch { get; } = claimedAtEpoch;

        internal Action? OnMoot { get; set; }
    }

    private sealed class EntryQueue
    {
        internal LinkedList<QueuedCall> Calls { get; } = new();
    }

    private sealed class QueuedCall(VaultLedger ledger, EntryQueue queue, Obligation obligation)
    {
        internal VaultLedger Ledger { get; } = ledger;

        internal EntryQueue Queue { get; } = queue;

        internal Obligation Obligation { get; } = obligation;

        internal Action Run { get; set; } = static () => { };

        internal bool Running { get; set; }

        internal bool Parked { get; set; }

        internal TimeSpan? NextRetryDelay { get; set; }
    }

    private sealed class VaultLedger
    {
        internal HashSet<Obligation> Unresolved { get; } = [];

        internal Dictionary<string, EntryQueue> Queues { get; } = new(StringComparer.Ordinal);

        internal HotExitVaultHold? Hold { get; set; }

        internal int OpenSessions { get; set; }

        internal int UnresolvedDiscards { get; set; }

        internal TaskCompletionSource? Settled { get; set; }

        internal bool HasUnresolved => Unresolved.Count > 0;

        internal void Add(Obligation obligation) => Unresolved.Add(obligation);

        internal void Remove(Obligation obligation) => Unresolved.Remove(obligation);

        internal EntryQueue QueueFor(string entryId)
        {
            if (!Queues.TryGetValue(entryId, out EntryQueue? queue))
            {
                queue = new EntryQueue();
                Queues[entryId] = queue;
            }
            return queue;
        }
    }

    // ---- The device root's durability (§1) ----------------------------------

    private const uint GenericWrite = 0x40000000;
    private const uint ShareAll = 0x7;
    private const uint OpenExisting = 3;
    private const uint FlagBackupSemantics = 0x02000000;

    /// <summary>Flushes the directory that holds <paramref name="root"/>, so
    /// the root's own name is durable (§1).</summary>
    private static bool SyncParentDirectory(string root)
    {
        string? parent = Path.GetDirectoryName(Path.GetFullPath(root));
        if (parent is null)
        {
            return false;
        }
        using SafeFileHandle handle = CreateFileW(
            parent,
            GenericWrite,
            ShareAll,
            IntPtr.Zero,
            OpenExisting,
            FlagBackupSemantics,
            IntPtr.Zero);
        return !handle.IsInvalid && FlushFileBuffers(handle);
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FlushFileBuffers(SafeFileHandle handle);
}
