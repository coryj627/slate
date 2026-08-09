// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Panels;
using uniffi.slate_uniffi;

namespace SlateWindows;

/// <summary>
/// W4-8 (#740): the sync-diagnostics coordinator — the mac AppState
/// sync block's host half. The <see cref="SyncDiagnosticsViewModel"/>
/// owns the data and stays silent (SDINV-4); THIS layer owns the
/// refresh funnel (SD7) and the single announcement gate (SD6).
///
/// Deliberately absent: an ActiveLeaf reveal hook. Revealing the leaf
/// does NOT re-run detection (SD4/SDINV-7 — the trigger set is closed
/// to {vault open, explicit refresh, watcher fire}); the W4-6/W4-7
/// reveal-refresh convention does not apply because this data is
/// vault-scoped and watcher-fresh, not note-scoped and event-starved.
/// </summary>
internal sealed partial class WorkspaceViewModel
{
    /// <summary>PUBLIC, not internal: the leaf body's
    /// Model="{Binding SyncDiagnostics}" resolves through WPF
    /// reflection, which only sees public properties — an internal
    /// property fails SILENTLY and the surface renders nothing (the
    /// recorded W4-4 lesson). Declared here, assigned in the main
    /// ctor (the History partial's shape).</summary>
    public SyncDiagnosticsViewModel SyncDiagnostics { get; }

    /// <summary>Installed once, right after the VM is constructed:
    /// the surface's Refresh/Retry route through the workspace funnel,
    /// and every publish passes the SD6 gate HERE rather than in the
    /// VM (SDINV-4's separation).</summary>
    internal void InstallSyncDiagnosticsSeams()
    {
        SyncDiagnostics.RefreshFromSurface = RefreshSyncDiagnostics;
        SyncDiagnostics.Published += (_, _) =>
        {
            if (SyncDiagnostics.Report is { } report)
            {
                AnnounceSyncFindingsIfNeeded(report);
            }
        };
    }

    /// <summary>slate.diagnostics.refreshSync (contract SD7): re-run
    /// detection idempotently. It does NOT reveal the leaf — the mac
    /// command only refreshes, and no showPanel command exists for
    /// this leaf in either registry.</summary>
    public void RefreshSyncDiagnostics()
    {
        if (_workspaceDisposed)
        {
            return;
        }
        SyncDiagnostics.Reload();
    }

    private System.Windows.Input.ICommand? _refreshSyncDiagnosticsCommand;

    /// <summary>CanExecute is unconditionally true: SD7 requires an
    /// open session, and on Windows a WorkspaceViewModel only exists
    /// while one is open (the vault lifecycle constructs it at open
    /// and disposes it at close/switch). The mac's "disabled without a
    /// vault" state has no Windows analogue to model.</summary>
    public System.Windows.Input.ICommand RefreshSyncDiagnosticsCommand =>
        _refreshSyncDiagnosticsCommand ??= new RelayCommand(
            _ => RefreshSyncDiagnostics(), _ => true);

    /// <summary>The announce-once gate. A bool, not the compaction
    /// relay's per-path set: exactly ONE workspace exists per vault
    /// session, so "once per workspace instance" and "once per vault
    /// path per session" are the same statement here — and a vault
    /// switch builds a fresh workspace, which re-arms exactly as
    /// mac's per-path gate does (SDR-5).</summary>
    private bool _announcedSyncFindings;

    /// <summary>Contract SD6, the ONLY sync announcement: core's
    /// pre-rendered <c>AudioSummary</c> at High priority, iff the
    /// report carries a multi-sync warning OR a High-risk provider,
    /// and at most once per vault. Low/Medium-only and empty reports
    /// stay silent; a manual refresh or watcher republish that clears
    /// neither condition says nothing.</summary>
    internal void AnnounceSyncFindingsIfNeeded(SyncDetectionReport report)
    {
        if (_announcedSyncFindings)
        {
            return;
        }
        bool hasHighRisk = report.Providers.Any(
            provider => provider.RiskLevel == RiskLevel.High);
        if (report.MultiSyncWarning is null && !hasHighRisk)
        {
            return;
        }
        _announcedSyncFindings = true;
        // W0.5-3 residue: SyncDetectionReport.AudioSummary (the
        // sync-detection engine has no canonical a11y event; the mac
        // pins the same designation at AppState.swift:11702). A
        // canonical conversion is a four-place mirror change and is
        // deliberately out of W4-8.
        _announce(new A11yEvent.HostComposed(report.AudioSummary, A11yPriority.High));
    }
}
