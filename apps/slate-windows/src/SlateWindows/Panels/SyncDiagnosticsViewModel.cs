// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using uniffi.slate_uniffi;

namespace SlateWindows.Panels;

/// <summary>The five mutually-exclusive leaf states (contract SD2),
/// chosen by <see cref="SyncDiagnosticsViewModel.SelectState"/> — the
/// mac SyncDiagnosticsPanelState twin.</summary>
internal enum SyncDiagnosticsState
{
    Unsupported,
    Error,
    Loading,
    Empty,
    Populated,
}

/// <summary>
/// W4-8 (#740): the sync-diagnostics document — the mac AppState sync
/// block's twin over <c>DetectSync()</c> + <c>LivesyncConfig()</c>.
/// Both FFI calls ride ONE off-dispatcher hop (SD5/SDR-6: either
/// throwing fails the PAIR to the error state) and every publish
/// passes the (shutdown, sequence) guard (SDINV-3).
///
/// Session identity needs no guard here: the workspace constructs
/// exactly one of these per <see cref="VaultSession"/> and disposes it
/// at vault close/switch (VaultLifecycleViewModel), so the mac's
/// <c>currentSession === session</c> recheck collapses into the
/// shutdown flag. That same one-VM-per-vault fact is why there is no
/// <c>Clear()</c>: a vault switch destroys this object rather than
/// resetting it.
///
/// The VM never announces (SDINV-4) — the workspace coordinator owns
/// the single SD6 gate.
/// </summary>
internal sealed class SyncDiagnosticsViewModel : PanelWorkScheduler
{
    private readonly VaultSession _session;
    private int _loadSeq;
    private SyncDetectionReport? _report;
    private LiveSyncConfigStatus? _liveSyncConfig;
    private string? _loadError;
    private bool _isLoading;

    public SyncDiagnosticsViewModel(
        VaultSession session,
        bool synchronousForTests = false)
        : base(synchronousForTests)
    {
        _session = session;
    }

    /// <summary>Test seam: block a worker between its FFI completion
    /// and its publish (the history/citations InterleaveForTests
    /// shape) so the SDINV-3 stale-publish fact is a deterministic
    /// schedule, never a timing bet.</summary>
    internal Action? LoadInterleaveForTests { get; set; }

    /// <summary>The surface's Refresh button routes THROUGH the
    /// workspace command path (installed by
    /// <c>InstallSyncDiagnosticsSeams</c>) so the button, the menu
    /// item, and the watcher share one refresh funnel — CanExecute and
    /// announce semantics stay in one place (SD7).</summary>
    internal Action? RefreshFromSurface { get; set; }

    // --- Published state (mutated on the UI context only) ---

    public SyncDetectionReport? Report
    {
        get => _report;
        private set
        {
            if (SetField(ref _report, value))
            {
                OnPropertyChanged(nameof(State));
            }
        }
    }

    public LiveSyncConfigStatus? LiveSyncConfig
    {
        get => _liveSyncConfig;
        private set => SetField(ref _liveSyncConfig, value);
    }

    public string? LoadError
    {
        get => _loadError;
        private set
        {
            if (SetField(ref _loadError, value))
            {
                OnPropertyChanged(nameof(State));
            }
        }
    }

    /// <summary>A probe is in flight. Deliberately NOT an input to
    /// <see cref="SelectState"/>: the mac's loading state is "no report
    /// yet" (<c>syncReport == nil</c>), so a refresh over a populated
    /// report keeps rendering that report instead of blanking the leaf
    /// on every watcher fire. Published for the facts and for any
    /// future busy affordance.</summary>
    public bool IsLoading
    {
        get => _isLoading;
        private set => SetField(ref _isLoading, value);
    }

    public SyncDiagnosticsState State => SelectState(_report, _loadError);

    /// <summary>Contract SD2's precedence, PURE so the matrix is
    /// coverable without rendering (mac
    /// <c>SyncDiagnosticsPanel.state(report:error:)</c>): unsupported →
    /// error → loading → empty → populated. Unsupported wins over
    /// everything (the report exists and says so); an error shows even
    /// when a stale report is retained underneath.</summary>
    internal static SyncDiagnosticsState SelectState(
        SyncDetectionReport? report, string? loadError)
    {
        if (report is { Supported: false })
        {
            return SyncDiagnosticsState.Unsupported;
        }
        if (loadError is not null)
        {
            return SyncDiagnosticsState.Error;
        }
        if (report is null)
        {
            return SyncDiagnosticsState.Loading;
        }
        return report.Providers.Length == 0
            ? SyncDiagnosticsState.Empty
            : SyncDiagnosticsState.Populated;
    }

    /// <summary>The leaf republished wholesale — the view rebinds on
    /// this, and the workspace's SD6 gate reads the fresh report from
    /// it.</summary>
    public event EventHandler? Published;

    /// <summary>Re-run detection + the config read (SD5). The sequence
    /// is bumped SYNCHRONOUSLY on the caller's thread before the work
    /// starts, so an older overlapping probe resuming out of order can
    /// never clobber a newer report (SDINV-3). Idempotent: the three
    /// SDINV-7 triggers (vault open, explicit refresh, watcher fire)
    /// all land here and nowhere else.</summary>
    public void Reload()
    {
        if (IsShutDown)
        {
            return;
        }
        int seq = Interlocked.Increment(ref _loadSeq);
        IsLoading = true;
        StartWork(() => LoadBody(seq));
    }

    private void LoadBody(int seq)
    {
        SyncDetectionReport? report = null;
        LiveSyncConfigStatus? config = null;
        string? failure = null;
        try
        {
            // ONE hop for BOTH calls (SDR-6, the mac single detached
            // task): if either throws, the pair fails together to the
            // error state with core's message.
            report = _session.DetectSync();
            config = _session.LivesyncConfig();
        }
        catch (VaultException exception)
        {
            report = null;
            config = null;
            failure = exception.Message;
        }
        LoadInterleaveForTests?.Invoke();
        Post(() =>
        {
            if (IsShutDown || Volatile.Read(ref _loadSeq) != seq)
            {
                return;
            }
            IsLoading = false;
            if (failure is not null)
            {
                // The error REPLACES the state (SD5, mac behavior) but
                // does not wipe the retained report: SelectState's
                // precedence is what hides it.
                LoadError = failure;
                Published?.Invoke(this, EventArgs.Empty);
                return;
            }
            LoadError = null;
            Report = report;
            LiveSyncConfig = config;
            Published?.Invoke(this, EventArgs.Empty);
        });
    }

    /// <summary>Teardown also invalidates in-flight publishes: the
    /// base flag alone is checked before the Post lands, and the bump
    /// makes the guard hold even for a body that already read it
    /// (SDINV-8).</summary>
    internal override void Shutdown()
    {
        base.Shutdown();
        _ = Interlocked.Increment(ref _loadSeq);
    }
}
