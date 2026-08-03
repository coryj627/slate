// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.ObjectModel;
using uniffi.slate_uniffi;

namespace SlateWindows.Panels;

/// <summary>
/// W4-4 (#736): the property-key bulk-rename sheet over
/// rename_property_across_vault. ARMING RULE (feature contract 7):
/// Apply can only execute after a dry-run preview on the IDENTICAL
/// (old, new) key pair; any key edit disarms it (the grid stays
/// visible). The footer string is the A11yRender(RenameSummary)
/// output verbatim — the same core template renders the
/// announcement and the footer, so they cannot drift. Esc/close
/// cancels in-flight work through the CancelToken; a per-file
/// failure never aborts the run (core semantics, surfaced per row).
/// </summary>
internal sealed class BulkRenameViewModel : PanelWorkScheduler
{
    internal sealed record PreviewRow(
        string Path, string Status, string Before, string After);

    private readonly VaultSession _session;
    private readonly Action<A11yEvent> _announce;
    private readonly Func<bool> _anyOpenDraftDirty;
    private readonly Action<RenameReport> _reconcileTabs;
    private string _oldKey = "";
    private string _newKey = "";
    private string? _armedOldKey;
    private string? _armedNewKey;
    private bool _workInFlight;
    private string? _progressText;
    private string? _errorText;
    private string _footerText = "";
    private CancelToken? _inFlightCancel;
    private int _requestId;

    public BulkRenameViewModel(
        VaultSession session,
        Action<A11yEvent> announce,
        Func<bool> anyOpenDraftDirty,
        Action<RenameReport> reconcileTabs,
        bool synchronousForTests = false)
        : base(synchronousForTests)
    {
        _session = session;
        _announce = announce;
        _anyOpenDraftDirty = anyOpenDraftDirty;
        _reconcileTabs = reconcileTabs;
    }

    public ObservableCollection<PreviewRow> Rows { get; } = [];

    public string EmptyStateText => PropertyPhrase.BulkRenameEmptyState;

    public bool ShowEmptyState => Rows.Count == 0 && _errorText is null;

    /// <summary>Posted once, on sheet open (canonical).</summary>
    public void SheetShown() => _announce(new A11yEvent.BulkRenameSheetShown());

    public string OldKey
    {
        get => _oldKey;
        set
        {
            if (SetField(ref _oldKey, value))
            {
                Disarm();
            }
        }
    }

    public string NewKey
    {
        get => _newKey;
        set
        {
            if (SetField(ref _newKey, value))
            {
                Disarm();
            }
        }
    }

    public bool WorkInFlight
    {
        get => _workInFlight;
        private set => SetField(ref _workInFlight, value);
    }

    public string? ProgressText
    {
        get => _progressText;
        private set => SetField(ref _progressText, value);
    }

    public string? ErrorText
    {
        get => _errorText;
        private set
        {
            if (SetField(ref _errorText, value))
            {
                OnPropertyChanged(nameof(ShowEmptyState));
            }
        }
    }

    /// <summary>The A11yRender(RenameSummary) output, verbatim.</summary>
    public string FooterText
    {
        get => _footerText;
        private set => SetField(ref _footerText, value);
    }

    /// <summary>Contract 7: armed only after a preview on the exact
    /// current pair.</summary>
    public bool CanApply =>
        !_workInFlight
        && string.Equals(_armedOldKey, _oldKey, StringComparison.Ordinal)
        && string.Equals(_armedNewKey, _newKey, StringComparison.Ordinal)
        && _armedOldKey is not null;

    public bool CanPreview =>
        !_workInFlight && _oldKey.Trim().Length > 0 && _newKey.Trim().Length > 0;

    private void Disarm()
    {
        _armedOldKey = null;
        _armedNewKey = null;
        OnPropertyChanged(nameof(CanApply));
        OnPropertyChanged(nameof(CanPreview));
    }

    public void Preview() => Run(dryRun: true);

    public bool Apply()
    {
        if (!CanApply)
        {
            return false;
        }
        if (_anyOpenDraftDirty())
        {
            // W0.5-3 residue: the blocked reason is spoken.
            _announce(new A11yEvent.HostComposed(
                PropertyPhrase.BulkRenameDirtyDraftsReason, A11yPriority.High));
            return false;
        }
        Run(dryRun: false);
        return true;
    }

    /// <summary>Esc / sheet close: cancel in-flight core work.</summary>
    public void CancelInFlight() => _inFlightCancel?.Cancel();

    private void Run(bool dryRun)
    {
        string oldKey = _oldKey.Trim();
        string newKey = _newKey.Trim();
        int requestId = Interlocked.Increment(ref _requestId);
        var cancel = new CancelToken();
        _inFlightCancel = cancel;
        WorkInFlight = true;
        ProgressText = dryRun
            ? PropertyPhrase.BulkRenamePreviewProgress
            : PropertyPhrase.BulkRenameApplyProgress;
        ErrorText = null;
        StartWork(() =>
        {
            RenameReport? report = null;
            string? error = null;
            try
            {
                report = _session.RenamePropertyAcrossVault(oldKey, newKey, dryRun, cancel);
            }
            catch (Exception exception) when (
                exception is not OutOfMemoryException
                    and not StackOverflowException
                    and not AccessViolationException)
            {
                error = exception.Message;
            }
            Post(() => PublishRun(requestId, dryRun, oldKey, newKey, report, error));
        });
    }

    /// <summary>Publish seam (internal for tests). Mutations before
    /// notifications; stale requests discard.</summary>
    internal void PublishRun(
        int requestId,
        bool dryRun,
        string oldKey,
        string newKey,
        RenameReport? report,
        string? error)
    {
        if (IsShutDown || requestId != _requestId)
        {
            return;
        }
        WorkInFlight = false;
        ProgressText = null;
        if (report is null)
        {
            ErrorText = $"Error: {error}";
            Disarm();
            _announce(new A11yEvent.RenameFailed(error ?? "unknown failure"));
            return;
        }
        Rows.Clear();
        foreach (var affected in report.Affected)
        {
            Rows.Add(new PreviewRow(
                affected.Path,
                affected.Applied ? "Applied" : "Will apply",
                affected.BeforeExcerpt,
                affected.AfterExcerpt));
        }
        foreach (var skipped in report.Skipped)
        {
            Rows.Add(new PreviewRow(skipped.Path, SkipStatus(skipped.Reason), "", ""));
        }
        foreach (var failed in report.Failed)
        {
            Rows.Add(new PreviewRow(
                failed.Path, FailStatus(failed.Kind, failed.Message), "", ""));
        }
        OnPropertyChanged(nameof(ShowEmptyState));

        var summary = new A11yEvent.RenameSummary(
            Applied: !dryRun,
            Renamed: (uint)report.Affected.Length,
            Skipped: (uint)report.Skipped.Length,
            Failed: (uint)report.Failed.Length);
        // The footer IS the canonical rendering — one template for
        // announcement and display, so they cannot drift.
        FooterText = SlateUniffiMethods.A11yRender(summary).Text;
        _announce(summary);

        if (dryRun)
        {
            _armedOldKey = oldKey;
            _armedNewKey = newKey;
        }
        else
        {
            Disarm();
            _reconcileTabs(report);
        }
        OnPropertyChanged(nameof(CanApply));
        OnPropertyChanged(nameof(CanPreview));
    }

    /// <summary>Row status strings, rendered from the TYPED reason
    /// enums (§2.5, verbatim).</summary>
    internal static string SkipStatus(RenameSkipReason reason) => reason switch
    {
        RenameSkipReason.NoSuchKey => "Skipped: key not present",
        RenameSkipReason.KeyCollision => "Skipped: new key already exists",
        _ => "Skipped: would change tags / list type",
    };

    internal static string FailStatus(RenameFailureKind kind, string message) => kind switch
    {
        RenameFailureKind.WriteConflict => "Failed: external write",
        RenameFailureKind.MalformedFrontmatter => "Failed: malformed YAML",
        RenameFailureKind.Cancelled => "Failed: cancelled",
        _ => $"Failed: error: {message}",
    };
}
