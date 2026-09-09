// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Input;
using System.Windows.Threading;
using uniffi.slate_uniffi;

namespace SlateWindows.Graph;

/// <summary>One verbosity level as the Graph menu shows it (C-9, C-12):
/// core's title and tag, and whether it is the selected level — the
/// preferences update it on every change.</summary>
internal sealed class GraphVerbosityChoice : BindableBase
{
    private bool _isSelected;

    internal GraphVerbosityChoice(GraphVerbositySpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        Spec = spec;
    }

    public GraphVerbositySpec Spec { get; }

    public string Title => Spec.Title;

    public string Tag => Spec.Tag;

    public bool IsSelected
    {
        get => _isSelected;
        internal set => SetField(ref _isSelected, value);
    }

    /// <summary>m1's rule: a re-selection re-asserts the check without
    /// storing or speaking anything.</summary>
    internal void Reassert() => OnPropertyChanged(nameof(IsSelected));
}

/// <summary>
/// W6-2 PR C (#746), contracts C-9 and C-10, rule W Terms W3–W7: the
/// graph's preferences — one per workspace, constructed after the view
/// state and before the navigator and the leaf, the one construction
/// counted. It holds <see cref="CurrentConfig"/> (the loaded config,
/// updated BY FIELD by each trigger before the schedule — the live view
/// state is never folded in, IGO-24) and its writable flag; exposes the
/// verbosity, core's level vector (fetched once per process), the menu's
/// choices and the parameterised setter; and runs the save's schedule
/// (Term W3: fold, reserve, pend, restart the 400 ms timer), the
/// hand-off (Term W4: the tick enqueues the pending pair directly into
/// the application writer and tracks its task) and the flush (Term W5:
/// shutdown stops the timer, enqueues a pending pair once, and refuses
/// every later schedule). The read serves the writer's newest
/// outstanding aggregate, else the file (Term W6).
/// </summary>
internal sealed class GraphPreferencesViewModel : BindableBase
{
    /// <summary>The mac cadence, a host constant (R-G).</summary>
    internal static readonly TimeSpan SaveWindow = TimeSpan.FromMilliseconds(400);

    private static readonly Lazy<IReadOnlyList<GraphVerbositySpec>> LevelsOnce =
        new(() => SlateUniffiMethods.GraphVerbosities());

    private enum SaveState
    {
        Active,
        Shut,
    }

    private readonly string _key;
    private readonly GraphConfigWriter _writer;
    private readonly DispatcherTimer _timer;
    private readonly List<Task> _outstanding = [];
    private (GraphConfig Aggregate, ulong Generation)? _pending;
    private SaveState _state = SaveState.Active;
    private GraphConfig _current;

    public GraphPreferencesViewModel(
        string vaultRoot,
        GraphConfigWriter? writer = null,
        GraphConfigStore? store = null,
        Dispatcher? dispatcher = null,
        TimeSpan? window = null)
    {
        ArgumentNullException.ThrowIfNull(vaultRoot);
        _key = GraphConfigWriter.KeyOf(vaultRoot);
        _writer = writer ?? GraphConfigWriter.Shared;
        // Term W6: the newest outstanding aggregate IS the config — a reopen
        // during a straggling write reads what the straggler will write;
        // otherwise the file, through Term W7's decode arms.
        if (_writer.Newest(_key) is { } outstanding)
        {
            _current = outstanding;
            IsWritable = true;
        }
        else
        {
            GraphConfigLoad load = (store ?? new GraphConfigStore(vaultRoot)).Read();
            _current = load.Config;
            IsWritable = load.Writable;
            LoadFailure = load.Failure;
        }
        _timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher ?? Dispatcher.CurrentDispatcher)
        {
            Interval = window ?? SaveWindow,
        };
        _timer.Tick += (_, _) => Tick();
        Choices = LevelsOnce.Value
            .Select(spec => new GraphVerbosityChoice(spec) { IsSelected = spec.Verbosity == _current.Verbosity })
            .ToList();
        SetVerbosityCommand = new RelayCommand(
            parameter =>
            {
                if (parameter is string tag
                    && Levels.FirstOrDefault(level => string.Equals(level.Tag, tag, StringComparison.Ordinal)) is { } spec)
                {
                    SetVerbosity(spec.Verbosity);
                }
            },
            _ => true);
    }

    /// <summary>Term W7: the loaded config, updated by field; the aggregate
    /// every save persists.</summary>
    public GraphConfig CurrentConfig => _current;

    /// <summary>False after a failed read: every save is refused and the
    /// file is never touched (Term W7's decode arms).</summary>
    public bool IsWritable { get; }

    public string? LoadFailure { get; }

    /// <summary>The level every graph announcement renders at (0bD-7);
    /// Standard on a load failure (the default config's).</summary>
    public GraphVerbosity Verbosity => _current.Verbosity;

    /// <summary>Core's vector, fetched once per process (design B).</summary>
    public IReadOnlyList<GraphVerbositySpec> Levels => LevelsOnce.Value;

    /// <summary>The menu's choices, one per level in the vector's order (C-12).</summary>
    public IReadOnlyList<GraphVerbosityChoice> Choices { get; }

    public bool IsSelected(GraphVerbositySpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        return spec.Verbosity == Verbosity;
    }

    /// <summary>The parameterised setter the three check items bind, each
    /// passing its TAG; an unknown tag is ignored.</summary>
    public ICommand SetVerbosityCommand { get; }

    /// <summary>Raised after a level CHANGE: the workspace drops the relay's
    /// pending navigation class so a row line queued before the change
    /// does not speak at the old level (IGN-17, IGP-21).</summary>
    public event Action? VerbosityChanged;

    private void SetVerbosity(GraphVerbosity level)
    {
        if (level == _current.Verbosity)
        {
            // m1: the check items are bound one way; a click on the
            // selected one toggles it locally, and only a notification
            // re-pushes the binding. Nothing stored, nothing spoken.
            foreach (GraphVerbosityChoice choice in Choices)
            {
                choice.Reassert();
            }
            return;
        }
        _current = _current with { Verbosity = level };
        foreach (GraphVerbosityChoice choice in Choices)
        {
            choice.IsSelected = choice.Spec.Verbosity == level;
        }
        OnPropertyChanged(nameof(Verbosity));
        VerbosityChanged?.Invoke();
        ScheduleSave();
    }

    // --- Term W7's triggers, each updating ITS field before the schedule ---

    /// <summary>The navigator's needle (C-6) → <c>filters.nameQuery</c>.</summary>
    public void SetNameQuery(string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);
        _current = _current with { Filters = _current.Filters with { NameQuery = raw } };
        ScheduleSave();
    }

    /// <summary>The leaf's depth change → <c>connectionsDepth</c> (B-D4, BD-6 close).</summary>
    public void SetConnectionsDepth(uint depth)
    {
        if (depth == _current.ConnectionsDepth)
        {
            return;
        }
        _current = _current with { ConnectionsDepth = depth };
        ScheduleSave();
    }

    /// <summary>PR D's switch → <c>mode</c> (the mac's <c>setGraphMode</c>; IGO-23).</summary>
    public void SetMode(GraphSurfaceMode mode)
    {
        _current = _current with { Mode = mode };
        ScheduleSave();
    }

    /// <summary>The ONE structural mapper from the persisted filters onto
    /// core's query (C-10; IGP-14): the seed and the fresh open's re-apply
    /// are bound to it; the overlay is never persisted.</summary>
    public static GraphVisibilityQuery VisibilityQueryOf(GraphFilterConfig filters)
    {
        ArgumentNullException.ThrowIfNull(filters);
        return new GraphVisibilityQuery(
            new GraphFilter(filters.IncludeAttachments, filters.IncludeGhosts, filters.OrphansOnly),
            filters.NameQuery,
            null);
    }

    // --- Rule W, Terms W3–W5 ---------------------------------------------

    /// <summary>Term W3: the aggregate IS <see cref="CurrentConfig"/>; a
    /// generation is reserved at once, the pair pends (replacing an older
    /// pending pair) and the timer restarts. Refused after shutdown and
    /// while the config is read-only.</summary>
    public void ScheduleSave()
    {
        if (_state == SaveState.Shut || !IsWritable)
        {
            RefusedForTests++;
            return;
        }
        ulong generation = _writer.Reserve(_key);
        _pending = (_current, generation);
        _timer.Stop();
        _timer.Start();
    }

    /// <summary>Term W4: the tick takes the pending pair — one, or none —
    /// and enqueues it DIRECTLY into the writer, tracking the task.</summary>
    private void Tick()
    {
        _timer.Stop();
        TransferPending();
    }

    private void TransferPending()
    {
        if (_pending is { } pair)
        {
            _pending = null;
            // Term W4's set is the OUTSTANDING writes. Pruning only in the
            // drain left one completed task per edit for the workspace's
            // whole life, and the seam below counted incomplete tasks only,
            // so nothing said so (IPG-5). Both transitions run on the owner
            // dispatcher, so this prune is serialised with the adds.
            _ = _outstanding.RemoveAll(task => task.IsCompleted);
            _outstanding.Add(_writer.Enqueue(_key, pair.Aggregate, pair.Generation));
        }
    }

    /// <summary>Term W5: shutdown flushes once — the timer stopped, a
    /// pending pair enqueued at once and its task registered before this
    /// returns; every later schedule refused, every later tick empty. No
    /// timer outlives the workspace.</summary>
    public void Shutdown()
    {
        if (_state == SaveState.Shut)
        {
            return;
        }
        _state = SaveState.Shut;
        _timer.Stop();
        TransferPending();
    }

    /// <summary>The outstanding writes' drain, added to the workspace's
    /// bounded drains (Term W4); a write past the bound completes on the
    /// writer (CR-5).</summary>
    public Task WhenWritesDrained()
    {
        _outstanding.RemoveAll(task => task.IsCompleted);
        return Task.WhenAll(_outstanding);
    }

    internal string KeyForTests => _key;

    internal bool HasPendingForTests => _pending is not null;

    internal ulong? PendingGenerationForTests => _pending?.Generation;

    internal GraphConfig? PendingAggregateForTests => _pending?.Aggregate;

    internal int OutstandingForTests => _outstanding.Count(task => !task.IsCompleted);

    /// <summary>Every task the set still holds, complete or not — the
    /// count IPG-5's growth shows up in.</summary>
    internal int TrackedWritesForTests => _outstanding.Count;

    internal bool IsShutForTests => _state == SaveState.Shut;

    internal bool TimerEnabledForTests => _timer.IsEnabled;

    internal int RefusedForTests { get; private set; }

    /// <summary>Test seam: the timer's tick, now.</summary>
    internal void FireTickForTests() => Tick();
}
