// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace SlateWindows.Canvas;

/// <summary>
/// W6-1 PR C-unit, task T1: what one publication attempt did.
/// </summary>
/// <remarks>
/// Result-BEARING, because a publisher that has to re-read the slot to
/// discover whether its own attempt landed has re-introduced the
/// second read the design spent three rounds removing. The
/// predecessor is the decision snapshot the transform saw; the
/// successor is what the slot now holds. On a decline both are the
/// snapshot and nothing was installed.
/// </remarks>
internal readonly record struct CanvasPublicationOutcome(
    bool Installed,
    CanvasPublication Predecessor,
    CanvasPublication Successor);

/// <summary>
/// W6-1 PR C-unit, task T1: the named install observer — obligation
/// I5's runtime half.
/// </summary>
/// <remarks>
/// <para>
/// Codex round 7's I5 arrangement is a publication reference coming
/// round again: a thread reads a shared value E, other writers move
/// the slot E to B and back to E, and the first thread's stale
/// compare-and-swap expecting E then succeeds. The slot refuses the
/// narrow case where a transform hands back its own snapshot, but
/// that says nothing about a cached, interned or sentinel record
/// installed twice at a distance.
/// </para>
/// <para>
/// This observer is the runtime proof: it sees every reference the
/// slot installs and reports whether any arrived twice. Obligation
/// I5's STRUCTURAL half — proving the transform algebra cannot
/// produce such a record at all — is task T4's analyzer, and until it
/// lands this is what the batteries assert against.
/// </para>
/// <para>
/// It is a NAMED observer, not a test hook: production attaches none,
/// and the slot's behaviour is identical with and without it. It does
/// RETAIN every publication it sees, which is exactly what makes it
/// unusable in a retention fact — attach it to prove freshness, never
/// to prove collection.
/// </para>
/// </remarks>
internal sealed class CanvasPublicationInstallObserver
{
    // Object-typed and explicitly reference-compared. A publication has
    // no value equality today, so the default comparer would do the same
    // thing — but "would do the same thing" is a property of the other
    // type, and this observer's whole claim is that it compares
    // REFERENCES. Spelling it here means adding an Equals to a
    // publication cannot quietly turn freshness into equality.
    private readonly HashSet<object> _seen = new(ReferenceEqualityComparer.Instance);

    private readonly object _gate = new();

    internal int Installs { get; private set; }

    /// <summary>How many installs presented a reference this observer
    /// had already seen installed. Freshness means zero.</summary>
    internal int RepeatedInstalls { get; private set; }

    internal void Installed(CanvasPublication publication)
    {
        lock (_gate)
        {
            Installs++;
            if (!_seen.Add(publication))
            {
                RepeatedInstalls++;
            }
        }
    }
}

/// <summary>
/// W6-1 PR C-unit, task T1: THE SLOT — the one mutable currency
/// authority in the model, and the only place a publication is
/// installed.
/// </summary>
/// <remarks>
/// <para>
/// READS are free-threaded and cost one volatile read. Currency is
/// derived by comparing against what this field holds, so a reader
/// never takes anything.
/// </para>
/// <para>
/// WRITES serialize on a gate, and this is the T1 decision worth
/// reading twice, because the frozen design describes a retry loop
/// and this does not have one. Obligation I2 is codex round 7's
/// finding that the loop is lock-free but not TERMINATING: a failed
/// swap proves only that some other writer progressed, and never that
/// this publisher's own work is now consumed, superseded or retired.
/// A load delivery could lose to keystrokes indefinitely, re-doing a
/// full rebase each time, and teardown's unconditional retirement
/// could starve before it ever installed the absorbing state.
/// </para>
/// <para>
/// Serializing the decision and the install removes the loop instead
/// of bounding it. Inside the gate the snapshot cannot move between
/// the read and the swap, so every attempt succeeds on its first
/// swap and no publisher retries at all. What the gate costs is
/// waiting for at most one transform, and the transforms in this
/// design are small — they compute an immutable successor from a
/// snapshot and allocate identities, and the expensive work (opening
/// a handle, building a population, running a match) happens outside
/// them by construction. A monitor is not formally FIFO; what it does
/// give is that a waiter QUEUES rather than re-racing, which is the
/// difference between bounded waiting and unbounded retrying that I2
/// is about.
/// </para>
/// <para>
/// The compare-and-swap stays, and does two jobs. It is the install,
/// which is the ratified direction. And because it can only fail if
/// some writer reached the field without the gate, its failure branch
/// is a BYPASS DETECTOR. That branch is defensive and, in T1, is not
/// reachable by any fact — the field is private and this is the only
/// method that writes it — so it is declared here as a known
/// uncovered branch whose owner is task T4's publication-writer
/// census (obligation I8), which is what makes "no other write" a
/// compile-time property rather than a promise.
/// </para>
/// <para>
/// REENTRANCY is refused rather than tolerated. A transform that
/// publishes is not pure, and a monitor is reentrant, so without this
/// the inner publication would install against a snapshot the outer
/// one is still holding and the outer swap would then fail for a
/// reason that has nothing to do with a bypass. Refusing it keeps the
/// detector honest and catches the impurity the design wants at the
/// moment it happens. The transitive purity predicate that makes the
/// larger claim structural is obligation I4, task T4.
/// </para>
/// </remarks>
internal sealed class CanvasPublicationSlot
{
    private readonly object _gate = new();
    private readonly CanvasPublicationInstallObserver? _observer;
    private CanvasPublication _current;
    private bool _publishing;

    internal CanvasPublicationSlot(
        CanvasPublication seed, CanvasPublicationInstallObserver? observer = null)
    {
        ArgumentNullException.ThrowIfNull(seed);
        _current = seed;
        _observer = observer;
    }

    /// <summary>The one read. Free-threaded, and the only source of a
    /// currency answer.</summary>
    internal CanvasPublication Current => Volatile.Read(ref _current);

    /// <summary>
    /// Decide from one snapshot and install the successor.
    /// </summary>
    /// <remarks>
    /// The transform returns null to DECLINE — the arrangement it
    /// decided from does not warrant a publication — which is how a
    /// refusal states itself without a second channel. Returning the
    /// snapshot itself is refused rather than treated as a decline,
    /// because that is the identity-return path obligation I5 names
    /// and a decline already spells the same intent unambiguously.
    /// </remarks>
    internal CanvasPublicationOutcome Publish(
        Func<CanvasPublication, CanvasPublication?> transform)
    {
        ArgumentNullException.ThrowIfNull(transform);

        lock (_gate)
        {
            if (_publishing)
            {
                throw new InvalidOperationException(
                    "a publication transform published reentrantly, so it is not a "
                    + "pure function of its snapshot and the outer attempt would "
                    + "install over work it never saw");
            }

            _publishing = true;
            try
            {
                CanvasPublication snapshot = _current;
                CanvasPublication? successor = transform(snapshot);
                if (successor is null)
                {
                    return new CanvasPublicationOutcome(false, snapshot, snapshot);
                }

                if (ReferenceEquals(successor, snapshot))
                {
                    throw new InvalidOperationException(
                        "a publication transform returned its own snapshot; a "
                        + "publication reference is never installed twice, and a "
                        + "transform with nothing to say declines by returning null");
                }

                CanvasPublication seen =
                    Interlocked.CompareExchange(ref _current, successor, snapshot);
                if (!ReferenceEquals(seen, snapshot))
                {
                    throw new InvalidOperationException(
                        "the publication slot moved while its gate was held, so a "
                        + "writer reached the field without passing through this "
                        + "method");
                }

                _observer?.Installed(successor);
                return new CanvasPublicationOutcome(true, snapshot, successor);
            }
            finally
            {
                _publishing = false;
            }
        }
    }
}
