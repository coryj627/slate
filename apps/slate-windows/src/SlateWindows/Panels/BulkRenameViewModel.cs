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
        private set
        {
            // In-flight state gates BOTH commands (adversarial
            // round 2): the buttons must disable the moment a run
            // starts, so a Preview can never supersede an in-flight
            // Apply.
            if (SetField(ref _workInFlight, value))
            {
                OnPropertyChanged(nameof(CanApply));
                OnPropertyChanged(nameof(CanPreview));
            }
        }
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
    /// current pair (compared TRIMMED — runs consume trimmed keys,
    /// so incidental whitespace must not leave a valid preview
    /// unarmed).</summary>
    public bool CanApply =>
        !_workInFlight
        && _armedOldKey is not null
        && string.Equals(_armedOldKey, _oldKey.Trim(), StringComparison.Ordinal)
        && string.Equals(_armedNewKey, _newKey.Trim(), StringComparison.Ordinal);

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

    /// <summary>Raised once at the end of every publish (rows and
    /// footer are final) — the window layer rebinds the preview grid
    /// on it instead of per-row collection churn.</summary>
    internal event Action? RunPublished;

    /// <summary>Raised when the sheet may actually be released — the
    /// workspace shuts it down and nulls the reference here.</summary>
    internal event Action? CloseSettled;

    private bool _closeRequested;

    /// <summary>Close request (adversarial round 1, contract 7): an
    /// idle sheet settles immediately; an in-flight run is CANCELLED
    /// and the sheet stays alive until the terminal publish lands the
    /// partial report, its cancellation partition, and the tab
    /// reconciliation — a mid-apply close must never discard them.</summary>
    public void RequestClose()
    {
        if (!_workInFlight)
        {
            CloseSettled?.Invoke();
            return;
        }
        _closeRequested = true;
        CancelInFlight();
    }

    private void Run(bool dryRun)
    {
        if (_workInFlight)
        {
            // Runtime guard behind the disabled buttons (adversarial
            // round 2): runs are strictly serialized — a superseding
            // request must never stale-discard an in-flight apply's
            // terminal report.
            return;
        }
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

    /// <summary>Publish seam (internal for tests). DISK TRUTH comes
    /// first (adversarial round 1, contracts 7/8/9): an apply
    /// report's tab reconciliation and its canonical announcements
    /// happen regardless of UI liveness — the writes already landed;
    /// only the visual state is gated on the sheet being alive.
    /// Mutations before notifications; stale requests discard.</summary>
    internal void PublishRun(
        int requestId,
        bool dryRun,
        string oldKey,
        string newKey,
        RenameReport? report,
        string? error)
    {
        // DISK TRUTH is unconditional (adversarial round 2): an apply
        // report reconciles open tabs and announces its summary even
        // if a later request or a shutdown superseded this run — the
        // writes already landed. Runs are serialized by the
        // WorkInFlight guard, so a stale requestId here is a test
        // construction, but the ordering keeps the guarantee
        // structural rather than incidental.
        if (report is not null && !dryRun)
        {
            _reconcileTabs(report);
            _announce(new A11yEvent.RenameSummary(
                Applied: true,
                Renamed: (uint)report.Affected.Length,
                Skipped: (uint)report.Skipped.Length,
                Failed: (uint)report.Failed.Length));
        }
        if (requestId != _requestId)
        {
            return;
        }
        if (IsShutDown)
        {
            SettleCloseIfRequested();
            return;
        }
        WorkInFlight = false;
        ProgressText = null;
        if (report is null)
        {
            ErrorText = $"Error: {error}";
            Disarm();
            _announce(new A11yEvent.RenameFailed(error ?? "unknown failure"));
            RunPublished?.Invoke();
            SettleCloseIfRequested();
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
        // announcement and display, so they cannot drift. Apply
        // summaries were already announced in the unconditional
        // disk-truth block above (exactly once per outcome).
        FooterText = SlateUniffiMethods.A11yRender(summary).Text;
        if (dryRun)
        {
            _announce(summary);
        }

        if (dryRun)
        {
            _armedOldKey = oldKey;
            _armedNewKey = newKey;
        }
        else
        {
            Disarm();
        }
        OnPropertyChanged(nameof(CanApply));
        OnPropertyChanged(nameof(CanPreview));
        RunPublished?.Invoke();
        SettleCloseIfRequested();
    }

    private void SettleCloseIfRequested()
    {
        if (_closeRequested)
        {
            _closeRequested = false;
            CloseSettled?.Invoke();
        }
    }

    internal void MarkWorkInFlightForTests() => WorkInFlight = true;

    internal int RequestIdForTests => _requestId;

    /// <summary>Row status strings, rendered from the TYPED reason
    /// enums (§2.5, verbatim).</summary>
    internal static string SkipStatus(RenameSkipReason reason) => reason switch
    {
        RenameSkipReason.NoSuchKey => "Skipped: key not present",
        RenameSkipReason.KeyCollision => "Skipped: new key already exists",
        RenameSkipReason.TagsKeyTypeDrift => "Skipped: would change tags / list type",
        _ => "Skipped",
    };

    internal static string FailStatus(RenameFailureKind kind, string message) => kind switch
    {
        RenameFailureKind.WriteConflict => "Failed: external write",
        RenameFailureKind.MalformedFrontmatter => "Failed: malformed YAML",
        RenameFailureKind.Cancelled => "Failed: cancelled",
        _ => $"Failed: error: {message}",
    };
}
