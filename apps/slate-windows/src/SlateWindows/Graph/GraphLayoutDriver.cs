// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Threading;
using uniffi.slate_uniffi;

namespace SlateWindows.Graph;

/// <summary>
/// W6-2 PR D (#746), rule G, Term G4 (DD-10): the settle loop over the
/// document's scheduler and nothing else. Each step is ONE always-async
/// compute — <c>Tick(20)</c>, the mac's cadence — whose apply installs the
/// frame (Term G5) and restarts a one-shot <see cref="DispatcherTimer"/> at
/// 16 ms (a host constant, R-G) that issues the next step: one step in
/// flight at a time, the loop ending at <c>Converged</c>, a cancel, a
/// retirement or a replacement. Under Reduce Motion the one compute is
/// <c>RunToConvergence(cancel)</c> and one frame is applied. Every run
/// carries a <see cref="CancelToken"/> cancelled at stop and disposed after
/// its last compute returns; an apply that finds its run cancelled applies
/// nothing. Every session call goes through the model's gate (Term G7).
/// </summary>
internal sealed class GraphLayoutDriver
{
    /// <summary>The document's <c>StartWorkAlwaysAsync</c>, handed in: the
    /// ONE scheduler the steps run on (no <c>Task.Run</c>, no second
    /// scheduler — the crossings census).</summary>
    internal delegate void Scheduler(Func<LayoutFrame?> compute, Action<LayoutFrame?> apply);

    /// <summary>R-G: the cadence between two steps.</summary>
    internal static readonly TimeSpan Cadence = TimeSpan.FromMilliseconds(16);

    /// <summary>The mac's <c>tick(iterations: 20)</c>.</summary>
    internal const uint IterationsPerStep = 20;

    /// <summary>The kernel's own ceiling — <c>LayoutConfig.MaxIterations</c>,
    /// the one <c>RunToConvergence</c> honours — applied per RUN to the tick
    /// loop (TGD-2's deviation): a multi-node graph may jitter at the
    /// temperature floor and never meet the predicate (graph_layout.rs's own
    /// note), and an unbounded 16 ms loop would tick forever.</summary>
    internal static readonly uint MaxIterationsPerRun = new LayoutConfig().MaxIterations;

    /// <summary>The steps a run can take before the ceiling ends it.</summary>
    internal static int MaxStepsPerRun => (int)((MaxIterationsPerRun + IterationsPerStep - 1) / IterationsPerStep);

    private readonly GraphDiagramModel _model;
    private readonly Scheduler _scheduler;
    private readonly Func<bool> _reduceMotion;
    private DispatcherTimer? _timer;
    private Run? _run;

    internal GraphLayoutDriver(GraphDiagramModel model, Scheduler scheduler, Func<bool> reduceMotion)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(reduceMotion);
        _model = model;
        _scheduler = scheduler;
        _reduceMotion = reduceMotion;
    }

    /// <summary>Term G5: a frame the model adopted — the renderer's rebuild.</summary>
    internal event Action<LayoutFrame>? FrameApplied;

    /// <summary>The run converged (a converged tick, or the Reduce Motion
    /// frame): the document speaks the settle line when armed (Term G4).</summary>
    internal event Action? Converged;

    /// <summary>A run is in flight — from its start to its convergence or stop.</summary>
    internal bool IsSettling => _run is not null;

    internal int StepsForTests { get; private set; }

    internal int ConvergeRunsForTests { get; private set; }

    internal int FramesAppliedForTests { get; private set; }

    internal int FramesDroppedForTests { get; private set; }

    internal bool LastRunWasReduceMotionForTests { get; private set; }

    /// <summary>The settle's start — the install (Term G2), the adoption
    /// (Term G6) and the motion flip (Term G4): the previous run stopped,
    /// the policy read NOW, one run issued.</summary>
    internal void StartSettle()
    {
        Stop();
        var run = new Run(new CancelToken());
        _run = run;
        bool reduce = _reduceMotion();
        LastRunWasReduceMotionForTests = reduce;
        if (reduce)
        {
            Converge(run);
        }
        else
        {
            Step(run);
        }
    }

    /// <summary>Term G7's first step: the run's token cancelled, the timer
    /// stopped; a step already computing returns into an apply that applies
    /// nothing.</summary>
    internal void Stop()
    {
        _timer?.Stop();
        if (_run is { } run)
        {
            _run = null;
            run.Cancel();
        }
    }

    // --- The step (Term G4): one Tick(20) through the scheduler ----------------

    private void Step(Run run)
    {
        StepsForTests++;
        run.BeginCompute();
        _scheduler(
            () =>
            {
                try
                {
                    if (run.Cancelled)
                    {
                        return null;
                    }
                    return _model.WithSession(
                        session =>
                        {
                            _model.Count("layout_tick");
                            return session.Tick(IterationsPerStep);
                        },
                        null);
                }
                finally
                {
                    run.EndCompute();
                }
            },
            frame => ApplyStep(run, frame));
    }

    private void ApplyStep(Run run, LayoutFrame? frame)
    {
        if (frame is null || run.Cancelled || !ReferenceEquals(run, _run))
        {
            // Refused at the gate, cancelled, or replaced: nothing applied.
            return;
        }
        _ = Apply(frame);
        run.Iterations += IterationsPerStep;
        if (frame.Converged || run.Iterations >= MaxIterationsPerRun)
        {
            LastRunEndedAtTheCeilingForTests = !frame.Converged;
            _run = null;
            run.Finish();
            Converged?.Invoke();
            return;
        }
        Timer.Start();
    }

    /// <summary>The last run ended at the kernel's ceiling, not the predicate.</summary>
    internal bool LastRunEndedAtTheCeilingForTests { get; private set; }

    // --- Reduce Motion (Term G4): one RunToConvergence, one frame ---------------

    private void Converge(Run run)
    {
        ConvergeRunsForTests++;
        run.BeginCompute();
        _scheduler(
            () =>
            {
                try
                {
                    if (run.Cancelled)
                    {
                        return null;
                    }
                    return _model.WithSession(
                        session =>
                        {
                            _model.Count("layout_run_to_convergence");
                            return session.RunToConvergence(run.Token);
                        },
                        null);
                }
                finally
                {
                    run.EndCompute();
                }
            },
            frame =>
            {
                if (frame is null || run.Cancelled || !ReferenceEquals(run, _run))
                {
                    return;
                }
                _ = Apply(frame);
                _run = null;
                run.Finish();
                Converged?.Invoke();
            });
    }

    /// <summary>Test seam (D-4): a frame handed to the apply as a step would
    /// hand it — the currency judged, nothing else.</summary>
    internal bool ApplyForTests(LayoutFrame frame) => Apply(frame);

    /// <summary>Term G5's currency: applied iff the frame's generation is the
    /// model's and its buffer pairs with the ids; else dropped (the mac's
    /// <c>:348–350</c>).</summary>
    private bool Apply(LayoutFrame frame)
    {
        if (frame.Generation != _model.Generation || frame.Positions.Length != _model.NodeIds.Length * 2)
        {
            FramesDroppedForTests++;
            return false;
        }
        _model.AdoptFrame(frame);
        FramesAppliedForTests++;
        FrameApplied?.Invoke(frame);
        return true;
    }

    // --- The cadence: the ONE named timer under Graph/ (DD-10, D-15 iii) ---------

    private DispatcherTimer Timer => _timer ??= NewTimer();

    private DispatcherTimer NewTimer()
    {
        // Built on the owner dispatcher in the first step's apply; a timer
        // that issues a scheduler step, never a body.
        var timer = new DispatcherTimer(Cadence, DispatcherPriority.Render, OnCadence, Dispatcher.CurrentDispatcher);
        timer.Stop();
        return timer;
    }

    private void OnCadence(object? sender, EventArgs e)
    {
        _timer?.Stop();
        if (_run is { } run && !run.Cancelled)
        {
            Step(run);
        }
    }

    /// <summary>One settle run: its token, cancelled at stop and disposed
    /// after its last compute returns (Term G4).</summary>
    private sealed class Run(CancelToken token)
    {
        private readonly object _lock = new();
        private bool _cancelled;
        private bool _computing;
        private bool _disposed;

        internal CancelToken Token { get; } = token;

        /// <summary>The iterations this run has ticked (the ceiling's count).</summary>
        internal uint Iterations { get; set; }

        internal bool Cancelled
        {
            get
            {
                lock (_lock)
                {
                    return _cancelled;
                }
            }
        }

        internal void Cancel()
        {
            lock (_lock)
            {
                if (_cancelled)
                {
                    return;
                }
                _cancelled = true;
                Token.Cancel();
                if (!_computing)
                {
                    Free();
                }
            }
        }

        /// <summary>A converged run: no further compute, the token released.</summary>
        internal void Finish()
        {
            lock (_lock)
            {
                _cancelled = true;
                if (!_computing)
                {
                    Free();
                }
            }
        }

        internal void BeginCompute()
        {
            lock (_lock)
            {
                _computing = true;
            }
        }

        internal void EndCompute()
        {
            lock (_lock)
            {
                _computing = false;
                if (_cancelled)
                {
                    Free();
                }
            }
        }

        private void Free()
        {
            if (!_disposed)
            {
                _disposed = true;
                Token.Dispose();
            }
        }
    }
}
