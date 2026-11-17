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

    /// <summary>Plan 47 CB-9b (#1889; CD5(a)): one apply the sheet asked
    /// for, as the workspace admits it: its request, the trimmed keys and the
    /// key type, the run's token, and the paths of the rows the armed preview
    /// listed (its affected, skipped and failed notes), whose chains it waits
    /// for and holds.</summary>
    internal sealed record BulkRenameApply(
        int RequestId,
        string OldKey,
        string NewKey,
        PropertyKeyType? OldKeyType,
        CancelToken Cancel,
        IReadOnlyList<string> Paths);

    /// <summary>What an apply's worker ended with: core's report, or the
    /// failure's message.</summary>
    internal sealed record ApplyOutcome(RenameReport? Report, string? Error);

    internal sealed record KeyTypeChoice(string Label, PropertyKeyType? Kind);
    public static IReadOnlyList<KeyTypeChoice> KeyTypes { get; } = [
        new("Any key type", null), new("String", PropertyKeyType.String),
        new("Integer", PropertyKeyType.Integer), new("Boolean", PropertyKeyType.Boolean),
        new("Null", PropertyKeyType.Null), new("Number", PropertyKeyType.Real),
    ];
    private KeyTypeChoice _oldKeyType = KeyTypes[0];
    private PropertyKeyType? _armedOldKeyType;

    public KeyTypeChoice OldKeyType
    {
        get => _oldKeyType;
        set { if (SetField(ref _oldKeyType, value)) { Disarm(); } }
    }

    private readonly VaultSession _session;
    private readonly Action<A11yEvent> _announce;
    private readonly Func<bool> _anyOpenDraftDirty;
    private readonly Action<BulkRenameViewModel, BulkRenameApply> _requestApply;
    private string _oldKey = "";
    private string _newKey = "";
    private string? _armedOldKey;
    private string? _armedNewKey;
    private IReadOnlyList<string> _armedPaths = [];
    private bool _workInFlight;
    private string? _progressText;
    private string? _errorText;
    private string _footerText = "";
    private CancelToken? _inFlightCancel;
    private int _requestId;

    /// <param name="requestApply">Plan 47 CB-9b: takes each apply the sheet
    /// asks for. The workspace admits it into the chains of the notes the
    /// armed preview listed, starts its worker at its turn
    /// (<see cref="StartApplyWork"/>), reconciles the notes' tabs and speaks
    /// the summary inside that turn, and ends the apply on the sheet
    /// (<see cref="PublishApply"/>, <see cref="EndApplyUnrun"/> or
    /// <see cref="RefuseApplyForDirtyDrafts"/>).</param>
    public BulkRenameViewModel(
        VaultSession session,
        Action<A11yEvent> announce,
        Func<bool> anyOpenDraftDirty,
        Action<BulkRenameViewModel, BulkRenameApply> requestApply,
        bool synchronousForTests = false)
        : base(synchronousForTests)
    {
        _session = session;
        _announce = announce;
        _anyOpenDraftDirty = anyOpenDraftDirty;
        _requestApply = requestApply;
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
        && _armedOldKeyType == _oldKeyType.Kind
        && string.Equals(_armedOldKey, _oldKey.Trim(), StringComparison.Ordinal)
        && string.Equals(_armedNewKey, _newKey.Trim(), StringComparison.Ordinal);

    public bool CanPreview =>
        !_workInFlight && (_oldKey.Trim().Length > 0 || _oldKeyType.Kind == PropertyKeyType.Null) && _newKey.Trim().Length > 0;

    private void Disarm()
    {
        _armedOldKey = null;
        _armedNewKey = null;
        OnPropertyChanged(nameof(CanApply));
        OnPropertyChanged(nameof(CanPreview));
    }

    /// <summary>Plan 47 CB-9b (#1889): the dry run is a run of its own. It
    /// writes nothing (core returns before any save), so it is not admitted
    /// into any note's chain and waits for nothing; its worker is drained at
    /// teardown with the sheet's other work.</summary>
    public void Preview() => RunPreview();

    /// <summary>Plan 47 CB-9b (#1889; CD5(a)): Apply checks its two gates,
    /// marks the sheet in flight and hands the apply to the workspace, which
    /// admits it into the chains of the notes the armed preview listed and
    /// starts its worker at its turn (<see cref="StartApplyWork"/>).</summary>
    public bool Apply()
    {
        if (!CanApply)
        {
            return false;
        }
        if (_anyOpenDraftDirty())
        {
            AnnounceDirtyDrafts();
            return false;
        }
        int requestId = Interlocked.Increment(ref _requestId);
        var cancel = new CancelToken();
        _inFlightCancel = cancel;
        WorkInFlight = true;
        ProgressText = PropertyPhrase.BulkRenameApplyProgress;
        ErrorText = null;
        _requestApply(this, new BulkRenameApply(
            requestId, _oldKey.Trim(), _newKey.Trim(), _oldKeyType.Kind, cancel, _armedPaths));
        return true;
    }

    /// <summary>W0.5-3 residue: the blocked reason is spoken, at the press
    /// and when the apply's turn finds a header draft dirty (plan 47 CB-9b,
    /// Call 2(d)).</summary>
    private void AnnounceDirtyDrafts() =>
        _announce(new A11yEvent.HostComposed(
            PropertyPhrase.BulkRenameDirtyDraftsReason, A11yPriority.High));

    /// <summary>Plan 47 CB-9b test seam: runs on the apply's worker before
    /// its core call. A fact holds the apply there, or throws.</summary>
    internal Action? ApplyHookForTests { get; set; }

    /// <summary>Plan 47 CB-9b test seam: runs on the preview's worker before
    /// its core call.</summary>
    internal Action? PreviewHookForTests { get; set; }

    /// <summary>Plan 47 CB-9b test seam: the token of the run in flight.</summary>
    internal CancelToken? InFlightCancelForTests => _inFlightCancel;

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

    private void RunPreview()
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
        PropertyKeyType? oldKeyType = _oldKeyType.Kind;
        int requestId = Interlocked.Increment(ref _requestId);
        var cancel = new CancelToken();
        _inFlightCancel = cancel;
        WorkInFlight = true;
        ProgressText = PropertyPhrase.BulkRenamePreviewProgress;
        ErrorText = null;
        StartWork(() =>
        {
            RenameReport? report = null;
            string? error = null;
            try
            {
                PreviewHookForTests?.Invoke();
                // The dry run, written as the constant: the save-chain census
                // reads a call whose dryRun is the constant true as no write.
                report = oldKeyType is { } kind
                    ? _session.RenamePropertyByIdentityAcrossVault(
                        SlateUniffiMethods.PropertyKeyIdentityForType(oldKey, kind), newKey, dryRun: true, cancel)
                    : _session.RenamePropertyAcrossVault(oldKey, newKey, dryRun: true, cancel);
            }
            catch (Exception exception) when (
                exception is not OutOfMemoryException
                    and not StackOverflowException
                    and not AccessViolationException)
            {
                error = exception.Message;
            }
            Post(() => PublishPreview(requestId, oldKey, newKey, report, error, oldKeyType));
        });
    }

    /// <summary>
    /// Plan 47 CB-9b (#1889; CD5(a)): the apply's worker, started by the
    /// workspace at the apply's turn in the notes' chains. The body is the
    /// apply's core call; every fault but out of memory, stack overflow and
    /// access violation is its failure, reported with the report through
    /// <paramref name="ran"/>. A body a shutdown skipped, or one that faulted
    /// outside its catch, never calls <paramref name="ran"/>: the workspace
    /// ends that apply as one that did not run. Returns the tracked task, or
    /// a completed one when the body ran inline or was refused.
    /// </summary>
    internal Task StartApplyWork(BulkRenameApply apply, Action<ApplyOutcome> ran) =>
        StartTrackedWork(() =>
        {
            RenameReport? report = null;
            string? error = null;
            try
            {
                ApplyHookForTests?.Invoke();
                report = apply.OldKeyType is { } kind
                    ? _session.RenamePropertyByIdentityAcrossVault(
                        SlateUniffiMethods.PropertyKeyIdentityForType(apply.OldKey, kind), apply.NewKey, false, apply.Cancel)
                    : _session.RenamePropertyAcrossVault(apply.OldKey, apply.NewKey, false, apply.Cancel);
            }
            catch (Exception exception) when (
                exception is not OutOfMemoryException
                    and not StackOverflowException
                    and not AccessViolationException)
            {
                error = exception.Message;
            }
            ran(new ApplyOutcome(report, error));
        });

    /// <summary>The preview's publication (internal for tests): the rows,
    /// the footer and the summary, and the arming on the exact pair, with
    /// the paths of every row it lists, which the apply is admitted over.
    /// Stale requests and a shut-down sheet discard.</summary>
    internal void PublishPreview(
        int requestId,
        string oldKey,
        string newKey,
        RenameReport? report,
        string? error,
        PropertyKeyType? oldKeyType = null)
    {
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
            PublishFailure(error);
            return;
        }
        ShowRows(report);
        var summary = new A11yEvent.RenameSummary(
            Applied: false,
            Renamed: (uint)report.Affected.Length,
            Skipped: (uint)report.Skipped.Length,
            Failed: (uint)report.Failed.Length);
        // The footer IS the canonical rendering — one template for
        // announcement and display, so they cannot drift.
        FooterText = SlateUniffiMethods.A11yRender(summary).Text;
        _announce(summary);
        _armedOldKey = oldKey;
        _armedOldKeyType = oldKeyType;
        _armedNewKey = newKey;
        _armedPaths = [.. report.Affected.Select(row => row.Path)
            .Concat(report.Skipped.Select(row => row.Path))
            .Concat(report.Failed.Select(row => row.Path))
            .Distinct(StringComparer.Ordinal)];
        OnPropertyChanged(nameof(CanApply));
        OnPropertyChanged(nameof(CanPreview));
        RunPublished?.Invoke();
        SettleCloseIfRequested();
    }

    /// <summary>
    /// The apply's publication on the sheet (internal for tests), after the
    /// workspace has reconciled the notes' tabs and spoken the summary,
    /// inside the apply's turn: the rows and the footer, or the failure and
    /// its RenameFailed, then the disarm. DISK TRUTH is the workspace's and
    /// never gated here (adversarial rounds 1 and 2, contracts 7/8/9); only
    /// the visual state is, so a stale request or a shut-down sheet shows
    /// nothing and settles a requested close.
    /// </summary>
    internal void PublishApply(BulkRenameApply apply, RenameReport? report, string? error)
    {
        if (apply.RequestId != _requestId)
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
            PublishFailure(error);
            return;
        }
        ShowRows(report);
        // The summary was spoken by the workspace; the footer renders the
        // same event.
        FooterText = SlateUniffiMethods.A11yRender(new A11yEvent.RenameSummary(
            Applied: true,
            Renamed: (uint)report.Affected.Length,
            Skipped: (uint)report.Skipped.Length,
            Failed: (uint)report.Failed.Length)).Text;
        Disarm();
        RunPublished?.Invoke();
        SettleCloseIfRequested();
    }

    /// <summary>Plan 47 CB-9b: an apply that never ran its core call (the
    /// chain closed first, the workspace was torn down, or its worker's body
    /// was skipped or faulted outside its catch) ends silently: the sheet is
    /// no longer in flight, the apply is disarmed, and a requested close
    /// settles.</summary>
    internal void EndApplyUnrun(BulkRenameApply apply)
    {
        if (apply.RequestId != _requestId)
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
        Disarm();
        RunPublished?.Invoke();
        SettleCloseIfRequested();
    }

    /// <summary>Plan 47 CB-9b (Call 2(d)): the apply's turn found a header
    /// draft dirty. It is refused with the sentence the press gives, and ends
    /// as one that did not run.</summary>
    internal void RefuseApplyForDirtyDrafts(BulkRenameApply apply)
    {
        AnnounceDirtyDrafts();
        EndApplyUnrun(apply);
    }

    private void PublishFailure(string? error)
    {
        ErrorText = $"Error: {error}";
        Disarm();
        _announce(new A11yEvent.RenameFailed(error ?? "unknown failure"));
        RunPublished?.Invoke();
        SettleCloseIfRequested();
    }

    private void ShowRows(RenameReport report)
    {
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

    internal void SetInFlightCancelForTests(CancelToken token) => _inFlightCancel = token;

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
