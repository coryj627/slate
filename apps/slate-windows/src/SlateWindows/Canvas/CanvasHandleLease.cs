// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace SlateWindows.Canvas;

/// <summary>
/// W6-1 PR C-unit, task T2: THE LEASE — one open canvas handle's host
/// identity, its FFI lock, and its close-once record.
/// </summary>
/// <remarks>
/// <para>
/// A lease has NO mutable liveness flag, which is the whole point of it
/// being a separate object. "Is this lease live?" is a currency
/// question and currency in this design is DERIVED: the answer is
/// whether the publication slot still names this lease, and the lease
/// itself cannot answer it and is not asked. What the lease owns is the
/// two things that are not currency — the lock that serialises calls
/// through the handle, and the record that makes closing it happen
/// exactly once.
/// </para>
/// <para>
/// THE CLOSE-ONCE RECORD is the one piece of non-currency mutable model
/// state the restated U1 enumerates rather than forbids, and its
/// disposition is in the reconciliation record. It is read for exactly
/// one purpose — to make a second close a no-op — and never to decide
/// admission, validation, effect legality or delivery. That boundary
/// matters more than it looks: the moment a close flag starts answering
/// "may this call proceed", the model has a second currency authority
/// and the whole derived-currency design is gone. So
/// <see cref="Invoke"/> below takes its admission from a caller-supplied
/// slot-derived predicate and never consults the close record.
/// </para>
/// <para>
/// WHY THAT IS SAFE rather than merely principled: a lease is closed
/// only by <c>CanvasLeaseTransfer</c>, and only when the live
/// publication does not name it. So "closed" implies "not named by the
/// publication", which implies the admission predicate refuses — the
/// closed handle is unreachable through the admission path rather than
/// guarded behind a flag. The battery pins that implication directly,
/// including through a retained unit whose lease was closed by a
/// reload.
/// </para>
/// <para>
/// THE FFI LOCK is what makes physical close wait for the last in-flight
/// call rather than racing it. Close takes the same lock a call takes,
/// so a close arriving mid-call blocks until the call returns and a call
/// arriving mid-close blocks until the handle is gone — and then refuses
/// on admission, because by then the publication has moved.
/// </para>
/// </remarks>
internal sealed class CanvasHandleLease
{
    private readonly object _ffiLock = new();
    private readonly ulong _handle;
    private readonly Action<ulong> _close;
    private int _closed;

    /// <param name="handle">The open canvas handle. A raw integer that
    /// the core session reuses freely, which is why nothing in this
    /// design ever treats it as an identity — the LEASE is the
    /// identity, and it is a reference.</param>
    /// <param name="close">The closing capability, supplied by whoever
    /// opened the handle. A delegate rather than a call into the
    /// session type so that this file names no FFI surface: the lease
    /// owns the LIFECYCLE, the loader owns the call.</param>
    internal CanvasHandleLease(ulong handle, Action<ulong> close)
    {
        ArgumentNullException.ThrowIfNull(close);
        _handle = handle;
        _close = close;
    }

    /// <summary>
    /// LEASE-class readable fact: whether the handle has been closed.
    /// </summary>
    /// <remarks>
    /// Readable because the five-class table lists physical close as a
    /// lease-class fact, and because the frozen verification plan makes
    /// the CLOSE OBSERVATION the instrument for native release — weak
    /// references cannot prove it in either direction. It is not a
    /// currency answer and no boundary treats it as one.
    /// </remarks>
    internal bool IsClosed => Volatile.Read(ref _closed) == 1;

    /// <summary>
    /// Close the handle, exactly once, however many times this is
    /// called.
    /// </summary>
    /// <remarks>
    /// The exchange decides; the lock orders. Winning the exchange
    /// before taking the lock would let a second caller return while
    /// the handle is still open, so the exchange happens INSIDE the
    /// lock and a loser leaves having waited for the winner — which is
    /// what makes "closed" mean "the call has returned" rather than
    /// "somebody has started closing".
    /// </remarks>
    internal void Close()
    {
        lock (_ffiLock)
        {
            if (Interlocked.Exchange(ref _closed, 1) == 1)
            {
                return;
            }

            _close(_handle);
        }
    }

    /// <summary>
    /// Call through the handle under the FFI lock, with the admission
    /// re-checked at LOCK time.
    /// </summary>
    /// <param name="admits">The slot-derived currency check — normally
    /// "does the live publication still name this lease". Evaluated
    /// INSIDE the lock, because a check written before the lock cannot
    /// see a retirement that landed while this caller was waiting for
    /// it. It must not take the publication gate; reading the slot is a
    /// volatile read and takes nothing, so there is no lock ordering
    /// here to get wrong.</param>
    /// <param name="call">The FFI call. Receives the handle, and is the
    /// only thing in this design that ever sees it.</param>
    /// <returns>Whether the call ran. False means admission refused,
    /// and nothing was invoked.</returns>
    internal bool Invoke(Func<bool> admits, Action<ulong> call)
    {
        ArgumentNullException.ThrowIfNull(admits);
        ArgumentNullException.ThrowIfNull(call);

        lock (_ffiLock)
        {
            if (!admits())
            {
                return false;
            }

            // The DETECTOR, and it is reachable — unlike the slot's
            // bypass branch. If admission said yes while the handle is
            // gone, then something closed a lease the publication still
            // names, which is round 7's blocker 1 arrangement arriving
            // by another door. Better to say so than to call into a
            // freed handle.
            if (Volatile.Read(ref _closed) == 1)
            {
                throw new InvalidOperationException(
                    "a call was admitted through a CLOSED lease: admission is "
                    + "derived from the publication, and a lease is closed only "
                    + "when the publication does not name it, so these two "
                    + "cannot both be true unless a lease was released while "
                    + "still current");
            }

            call(_handle);
            return true;
        }
    }
}
