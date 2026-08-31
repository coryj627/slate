// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Threading;
using SlateWindows.Canvas;
using uniffi.slate_uniffi;
using static SlateWindows.Tests.CanvasModelFixtures;

namespace SlateWindows.Tests;

/// <summary>
/// §D task TD-1: the presentation commit — obligations ID-1 (the
/// dispatcher-owned install with its production assertion) and ID-2
/// (single-flight coalescing with one replaceable pending request),
/// driven through a real WPF dispatcher pumped the house way.
/// </summary>
public sealed class CanvasPresentationEngineTests
{
    // ---------------------------------------------------------------
    // ID-1: the thread rule
    // ---------------------------------------------------------------

    /// <summary>The production assertion, positively: a worker that
    /// reaches a commit throws rather than racing the dispatcher —
    /// the defect is loud, not intermittent.</summary>
    [Fact]
    public void ACommitFromAWorkerThreadThrowsTheThreadRule()
    {
        var engine = new CanvasPresentationEngine(synchronousForTests: true);
        Exception? caught = null;
        var worker = new CanvasWorker(() =>
        {
            try
            {
                engine.CommitViewport(v => v.ZoomedIn(0, 0));
            }
            catch (InvalidOperationException exception)
            {
                caught = exception;
            }
        });
        worker.Start();
        worker.Join(TimeSpan.FromSeconds(10));
        Assert.True(
            caught is InvalidOperationException,
            "a worker-thread commit did not throw: the thread rule is the "
            + "assertion ID-1 demands of production, and without it the "
            + "check-and-swap argument is a hope.");
    }

    /// <summary>The intake marshals itself: a publication applied on a
    /// worker still commits on the engine's dispatcher — the
    /// scheduler's inline-post arm is survived, not assumed away.</summary>
    [Fact]
    public void AWorkerThreadApplyIsMarshalledOntoTheCommitThread()
    {
        var engine = new CanvasPresentationEngine(synchronousForTests: true);
        CanvasPublication pub = CanvasPublication.Seed().WithNeedleIntent("n");
        var worker = new CanvasWorker(() => engine.OnPublicationApplied(pub));
        worker.Start();
        worker.Join(TimeSpan.FromSeconds(10));
        Assert.True(
            engine.Current is null,
            "premise: the worker's intake must not have committed inline — "
            + "the commit belongs to the dispatcher this engine captured.");
        DrainDispatcher();
        Assert.True(
            ReferenceEquals(engine.Current?.Source, pub),
            "the marshalled intake never landed: the publication applied on "
            + "a worker must reach the installed state through the engine's "
            + "own dispatcher.");
    }

    // ---------------------------------------------------------------
    // ID-2: the progress rule, both barrier orders
    // ---------------------------------------------------------------

    /// <summary>Barrier, order one: a publication landing while a
    /// build is in flight supersedes it — the superseded build never
    /// installs.</summary>
    [Fact]
    public void APublicationLandingMidBuildIsNeverOvertaken()
    {
        WithPumpedContext(engine =>
        {
            var installs = new List<CanvasPresentationState>();
            engine.StateInstalled += (_, state) => installs.Add(state);
            CanvasPublication b = CanvasPublication.Seed().WithNeedleIntent("b");
            CanvasPublication c = b.WithNeedleIntent("c");
            engine.OnPublicationApplied(b);
            // C lands before the dispatcher pumps B's install
            // continuation: the arrangement the barrier fact exists for.
            engine.OnPublicationApplied(c);
            PumpUntil(() => ReferenceEquals(engine.Current?.Source, c));
            Assert.True(
                ReferenceEquals(engine.Current?.Source, c),
                "the latest publication never installed.");
            Assert.True(
                installs.TrueForAll(state => !ReferenceEquals(state.Source, b)),
                "B installed after C landed: the superseded build must be "
                + "discarded at the install-time revalidation — B's install "
                + "continuation cannot run before C's commit, so a B in the "
                + "log is the revalidation failing, not an ordering "
                + "accident.");
            Assert.True(
                ReferenceEquals(installs[^1].Source, c),
                "the final installed state is not the latest pair.");
        });
    }

    /// <summary>Barrier, order two: a viewport commit landing during a
    /// publication build is durable — committed before any build, so
    /// the stale build's discard cannot swallow the zoom.</summary>
    [Fact]
    public void AViewportCommitDuringAPublicationBuildIsNotLost()
    {
        WithPumpedContext(engine =>
        {
            CanvasPublication b = CanvasPublication.Seed().WithNeedleIntent("b");
            engine.OnPublicationApplied(b);
            // Mid-build: the zoom commits to the viewport authority
            // BEFORE the build's install pumps.
            engine.CommitViewport(v => v.ZoomedIn(0, 0));
            PumpUntil(() =>
                engine.Current is { } state
                && ReferenceEquals(state.Source, b)
                && state.Viewport.Zoom > 1.0);
            Assert.True(
                engine.Current is { } final
                && final.Viewport.Zoom == 1.25
                && ReferenceEquals(final.Source, b),
                "the zoom vanished: a viewport delta is durable in its own "
                + "authority and must survive every discarded build (ID-2's "
                + "sibling rule in D1).");
        });
    }

    /// <summary>Coalescing: a burst of distinct publications settles
    /// to EXACTLY one install — one running build, one replaceable
    /// pending request, no queue.</summary>
    [Fact]
    public void ABurstOfPublicationsSettlesToOneInstall()
    {
        WithPumpedContext(engine =>
        {
            var installs = new List<CanvasPresentationState>();
            engine.StateInstalled += (_, state) => installs.Add(state);
            CanvasPublication pub = CanvasPublication.Seed();
            for (var i = 0; i < 10; i++)
            {
                pub = pub.WithNeedleIntent($"n{i}");
                engine.OnPublicationApplied(pub);
            }
            CanvasPublication last = pub;
            PumpUntil(() => ReferenceEquals(engine.Current?.Source, last));
            Assert.True(
                installs.Count == 1 && ReferenceEquals(installs[0].Source, last),
                $"a ten-publication burst produced {installs.Count} installs; "
                + "single-flight with one replaceable pending request settles "
                + "to exactly one, and it is the latest (ID-2).");
        });
    }

    /// <summary>Deduplication: an identity transform commits nothing
    /// and builds nothing.</summary>
    [Fact]
    public void AnIdentityViewportTransformBuildsNothing()
    {
        var engine = new CanvasPresentationEngine(synchronousForTests: true);
        engine.OnPublicationApplied(CanvasPublication.Seed());
        var installs = 0;
        engine.StateInstalled += (_, _) => installs++;
        engine.CommitViewport(v => v.PannedTo(v.PanX, v.PanY));
        Assert.True(
            installs == 0,
            "an identity transform installed a state: the geometry "
            + "comparison is the deduplication ID-2 names.");
    }

    // ---------------------------------------------------------------
    // The viewport value's arithmetic (D1's pinned constants)
    // ---------------------------------------------------------------

    /// <summary>The clamp, the step, and actual size.</summary>
    [Fact]
    public void ZoomStepsAndClampsAsPinned()
    {
        CanvasViewportState v = CanvasViewportState.Seed();
        Assert.True(v.Zoom == 1.0 && v.FollowSelection, "the seed is 1.0, follow ON.");
        v = v.ZoomedIn(0, 0);
        Assert.True(v.Zoom == 1.25, "one step in is 1.25.");
        for (var i = 0; i < 20; i++)
        {
            v = v.ZoomedIn(0, 0);
        }
        Assert.True(v.Zoom == 4.0, $"the ceiling is 4.0; got {v.Zoom}.");
        for (var i = 0; i < 40; i++)
        {
            v = v.ZoomedOut(0, 0);
        }
        Assert.True(v.Zoom == 0.1, $"the floor is 0.1; got {v.Zoom}.");
        v = v.AtActualSize(0, 0);
        Assert.True(v.Zoom == 1.0, "actual size is 1.0.");
    }

    /// <summary>Centre preservation: the document point under the
    /// given view-space centre stays under it across a zoom.</summary>
    [Fact]
    public void AZoomPreservesTheDocumentPointUnderItsCentre()
    {
        CanvasViewportState v = CanvasViewportState.Seed()
            .WithViewSize(800, 600)
            .PannedTo(-100, -50);
        const double centreX = 400;
        const double centreY = 300;
        double documentX = (centreX - v.PanX) / v.Zoom;
        double documentY = (centreY - v.PanY) / v.Zoom;
        CanvasViewportState zoomed = v.ZoomedIn(centreX, centreY);
        double documentXAfter = (centreX - zoomed.PanX) / zoomed.Zoom;
        double documentYAfter = (centreY - zoomed.PanY) / zoomed.Zoom;
        Assert.True(
            Math.Abs(documentX - documentXAfter) < 1e-9
                && Math.Abs(documentY - documentYAfter) < 1e-9,
            "the centre moved: a zoom must keep the document point under "
            + "the pointer or the focused card exactly where it is.");
    }

    // ---------------------------------------------------------------
    // The harness
    // ---------------------------------------------------------------

    /// <summary>Run a body against an ASYNC engine whose install
    /// continuations land on this thread's dispatcher — the
    /// DispatcherFrame pump the windowed batteries already use.</summary>
    private static void WithPumpedContext(Action<CanvasPresentationEngine> body)
    {
        SynchronizationContext? previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(
            new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        try
        {
            body(new CanvasPresentationEngine(synchronousForTests: false));
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    private static void PumpUntil(Func<bool> condition)
    {
        var budget = System.Diagnostics.Stopwatch.StartNew();
        while (!condition() && budget.Elapsed < TimeSpan.FromSeconds(10))
        {
            DrainDispatcher();
            Thread.Yield();
        }
        Assert.True(condition(), "premise: the pumped condition never held.");
    }

    private static void DrainDispatcher()
    {
        var frame = new DispatcherFrame();
        _ = Dispatcher.CurrentDispatcher.BeginInvoke(
            DispatcherPriority.Background,
            () => frame.Continue = false);
        Dispatcher.PushFrame(frame);
    }

    /// <summary>Task TD-3: the retained set is the third authority —
    /// a commit rebuilds the installed state, and an identical set is
    /// deduplicated exactly as an identity viewport transform is.</summary>
    [Fact]
    public void ARetainedCommitRebuildsAndAnEqualSetDoesNot()
    {
        var engine = new CanvasPresentationEngine(synchronousForTests: true);
        engine.OnPublicationApplied(CanvasPublication.Seed());
        var installs = 0;
        engine.StateInstalled += (_, _) => installs++;
        engine.CommitRetained([CanvasPeerKey.Card("x")]);
        Assert.True(
            installs == 1
                && engine.Current is { } state
                && state.Retained.Contains(CanvasPeerKey.Card("x")),
            "the retained commit did not rebuild the installed state with "
            + "its snapshot (TD-3's third authority).");
        engine.CommitRetained([CanvasPeerKey.Card("x")]);
        Assert.True(
            installs == 1,
            "an equal retained set rebuilt anyway: set equality is the "
            + "deduplication, exactly as geometry is the viewport's.");
    }

    // ---------------------------------------------------------------
    // §D task TD-4: the rendered selection derives from the
    // publication (ID-5, ID-6's settled direction)
    // ---------------------------------------------------------------

    /// <summary>Obligation ID-5's arrangement: the filter matched only
    /// A, the reader selected the dimmed B — the state's selection is
    /// the PUBLICATION's answer, not the unit's filtered
    /// resolution.</summary>
    [Fact]
    public void TheStateSelectionFollowsThePublicationNotTheUnit()
    {
        var slot = new CanvasPublicationSlot(CanvasPublication.Seed());
        var request = new CanvasRequestIdentity("L1");
        var lease = new CanvasHandleLease(1, _ => { });
        _ = slot.Publish(s => s.WithLoads(s.Loads.Requested(request)));
        CanvasPopulation population = Population("a", "b");
        Assert.True(
            CanvasLeaseTransfer.TryAccept(slot, request, lease, population).Accepted,
            "premise: the load must land.");
        _ = slot.Publish(s => s.WithSelectedIntent("b"));
        _ = slot.Publish(s => s.WithUnit(CanvasProjectionUnit
                .Unfiltered(population)
                .Pending(new CanvasRequestIdentity("F1"), "x")
                .Answered(population, ["a"])));
        var engine = new CanvasPresentationEngine(synchronousForTests: true);
        engine.OnPublicationApplied(slot.Current);
        Assert.True(
            engine.Current?.Selection == "b",
            "the rendered selection lost the dimmed card: ID-5 derives it "
            + "from the publication's intent resolved against the "
            + "population, and the unit's filtered semantics stay the "
            + "unit's.");
    }

    /// <summary>T5 cannot reach this surface: two installed states
    /// answer with their own selections — a retained reference never
    /// changes its answer.</summary>
    [Fact]
    public void TwoStatesAnswerWithTheirOwnSelections()
    {
        var slot = new CanvasPublicationSlot(CanvasPublication.Seed());
        var request = new CanvasRequestIdentity("L1");
        var lease = new CanvasHandleLease(1, _ => { });
        _ = slot.Publish(s => s.WithLoads(s.Loads.Requested(request)));
        _ = CanvasLeaseTransfer.TryAccept(slot, request, lease, Population("a", "b"));
        var engine = new CanvasPresentationEngine(synchronousForTests: true);
        _ = slot.Publish(s => s.WithSelectedIntent("a"));
        engine.OnPublicationApplied(slot.Current);
        CanvasPresentationState first = engine.Current!;
        _ = slot.Publish(s => s.WithSelectedIntent("b"));
        engine.OnPublicationApplied(slot.Current);
        CanvasPresentationState second = engine.Current!;
        Assert.True(
            first.Selection == "a" && second.Selection == "b",
            "a retained state changed its answer — the selection must be a "
            + "pure function of the state's own publication, or the "
            + "mid-apply reader is back to two authorities.");
    }
}
