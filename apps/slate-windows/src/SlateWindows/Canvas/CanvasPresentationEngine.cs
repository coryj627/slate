// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Threading;

namespace SlateWindows.Canvas;

/// <summary>
/// The presentation commit (§D D1, obligations ID-1 and ID-2): one
/// renderer's two authorities — the applied publication it is told
/// about and the viewport value it owns — and the single-flight build
/// that turns their latest pair into one installed
/// <see cref="CanvasPresentationState"/>.
///
/// THE THREAD RULE (ID-1): every authority commit and every install
/// runs on the dispatcher this engine captured at construction, and
/// the engine ASSERTS that in production — a worker that reaches a
/// commit throws rather than racing. The document's apply may run
/// inline on a worker (the scheduler's recorded arm); the engine's
/// publication intake therefore marshals itself before touching an
/// authority, which is what makes the check-and-swap
/// same-thread-linearized: there is no thread on which an authority
/// can move between the final check and the swap.
///
/// THE PROGRESS RULE (ID-2): one running build, one replaceable
/// latest pending request. A request that arrives while a build runs
/// REPLACES the pending request; a completed build installs only if
/// its pair is still current, else the pending request builds next.
/// A request whose pair equals the installed state's is discarded —
/// deduplication, so a burst settles to exactly one final install.
/// </summary>
internal sealed class CanvasPresentationEngine
{
    private readonly Dispatcher _dispatcher;
    private readonly bool _synchronous;
    private CanvasViewportState _viewport = CanvasViewportState.Seed();
    private CanvasPublication? _source;
    private int _textScaleRevision;
    private int _themeRevision;
    private System.Collections.Immutable.ImmutableHashSet<CanvasPeerKey> _retained =
        [];
    private bool _building;
    private int _discarded;

    internal CanvasPresentationEngine(bool synchronousForTests = false)
    {
        _dispatcher = Dispatcher.CurrentDispatcher;
        _synchronous = synchronousForTests;
    }

    /// <summary>The installed state — the ONLY thing a peer or a draw
    /// pass reads. Null until the first source arrives.</summary>
    internal CanvasPresentationState? Current { get; private set; }

    /// <summary>Raised after an install, with the old and new states —
    /// UIA events derive from this pair, never from live fields.</summary>
    internal event Action<CanvasPresentationState?, CanvasPresentationState>? StateInstalled;

    /// <summary>Builds that completed and were NOT installed because
    /// their pair had been superseded — the coalescing facts read
    /// this. Test seam by name.</summary>
    internal int DiscardedBuildsForTests => _discarded;

    /// <summary>The publication intake — the document's post-apply
    /// notification calls this from WHATEVER thread the apply ran on,
    /// and the engine marshals itself (ID-1).</summary>
    internal void OnPublicationApplied(CanvasPublication applied)
    {
        ArgumentNullException.ThrowIfNull(applied);
        if (_dispatcher.CheckAccess())
        {
            CommitSource(applied);
            return;
        }
        _ = _dispatcher.BeginInvoke(() => CommitSource(applied));
    }

    /// <summary>A viewport command: commit the successor value FIRST,
    /// then request the build — so the delta is durable before any
    /// build exists to lose it (D1's ordering rule).</summary>
    internal void CommitViewport(Func<CanvasViewportState, CanvasViewportState> transform)
    {
        ArgumentNullException.ThrowIfNull(transform);
        AssertCommitThread();
        CanvasViewportState next = transform(_viewport);
        if (next.SameGeometry(_viewport))
        {
            return;
        }
        _viewport = next;
        RequestBuild();
    }

    /// <summary>The owned services' revisions (D11, D13): a bump is a
    /// new state.</summary>
    internal void CommitTextScaleRevision(int revision)
    {
        AssertCommitThread();
        if (_textScaleRevision == revision)
        {
            return;
        }
        _textScaleRevision = revision;
        RequestBuild();
    }

    /// <summary>The third authority (task TD-3): the externally
    /// retained peer keys — realized or referenced by clients — whose
    /// off-window survivors carry tombstones. Committed on the
    /// dispatcher exactly as the viewport is.</summary>
    internal void CommitRetained(
        System.Collections.Immutable.ImmutableHashSet<CanvasPeerKey> retained)
    {
        ArgumentNullException.ThrowIfNull(retained);
        AssertCommitThread();
        if (_retained.SetEquals(retained))
        {
            return;
        }
        _retained = retained;
        RequestBuild();
    }

    /// <summary>The theme half of the revision pair.</summary>
    internal void CommitThemeRevision(int revision)
    {
        AssertCommitThread();
        if (_themeRevision == revision)
        {
            return;
        }
        _themeRevision = revision;
        RequestBuild();
    }

    private void CommitSource(CanvasPublication applied)
    {
        AssertCommitThread();
        if (ReferenceEquals(_source, applied))
        {
            return;
        }
        _source = applied;
        RequestBuild();
    }

    /// <summary>ID-1's production assertion: the commit owns its
    /// dispatcher, and an off-thread caller is a defect, not a race
    /// to survive.</summary>
    private void AssertCommitThread()
    {
        if (!_dispatcher.CheckAccess())
        {
            throw new InvalidOperationException(
                "the presentation commit ran off its dispatcher — every "
                + "authority commit and install is dispatcher-owned (§D ID-1).");
        }
    }

    private void RequestBuild()
    {
        if (_source is null)
        {
            return;
        }
        if (_building)
        {
            // ID-2: ONE replaceable pending request — and it needs no
            // flag, because the AUTHORITIES are the pending request:
            // any arrival that matters moved an authority, the
            // running build is therefore stale at install, and the
            // stale path re-requests from the current pair. A flag
            // here would be a second copy of that fact.
            return;
        }
        _building = true;
        CanvasPublication source = _source;
        CanvasViewportState viewport = _viewport;
        System.Collections.Immutable.ImmutableHashSet<CanvasPeerKey> retained = _retained;
        int textScale = _textScaleRevision;
        int theme = _themeRevision;
        if (_synchronous)
        {
            Install(Derive(source, viewport, retained, textScale, theme));
            return;
        }
        _ = System.Threading.Tasks.Task.Run(
                () => Derive(source, viewport, retained, textScale, theme))
            .ContinueWith(
                done => Install(done.Result),
                System.Threading.CancellationToken.None,
                System.Threading.Tasks.TaskContinuationOptions.OnlyOnRanToCompletion,
                System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext());
    }

    /// <summary>The pure derivation — off-thread in production, over
    /// an immutable snapshot pair. TD-3 grows this into the windowed
    /// topology build; the engine's discipline does not change when
    /// it does.</summary>
    private static CanvasPresentationState Derive(
        CanvasPublication source,
        CanvasViewportState viewport,
        System.Collections.Immutable.ImmutableHashSet<CanvasPeerKey> retained,
        int textScaleRevision,
        int themeRevision) =>
        new(
            source,
            viewport,
            source.Loaded?.Population is { } population
                ? CanvasPeerTopology.Derive(population, viewport, retained)
                : CanvasPeerTopology.Empty(),
            retained,
            textScaleRevision,
            themeRevision);

    private void Install(CanvasPresentationState built)
    {
        AssertCommitThread();
        _building = false;
        // The install-time revalidation (ID-1): the pair is re-read on
        // the SAME thread every commit runs on, so a stale build is
        // detected here and never installed.
        bool stale =
            !ReferenceEquals(built.Source, _source)
            || !ReferenceEquals(built.Retained, _retained)
            || !built.Viewport.SameGeometry(_viewport)
            || built.TextScaleRevision != _textScaleRevision
            || built.ThemeRevision != _themeRevision;
        if (stale)
        {
            _discarded++;
            RequestBuild();
            return;
        }
        CanvasPresentationState? was = Current;
        Current = built;
        StateInstalled?.Invoke(was, built);
    }
}
