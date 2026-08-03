// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.ObjectModel;
using uniffi.slate_uniffi;

namespace SlateWindows.Panels;

/// <summary>Per-REQUEST announcement ownership for header refreshes
/// (adversarial round 3): exactly one header in a fan-out owns the
/// outcome announcement, and the mode is captured with the request —
/// a discarded stale publish can never migrate it to a later
/// path.</summary>
internal enum ReloadAnnounce
{
    /// <summary>Silent refresh (peer duplicates, initial load).</summary>
    None,

    /// <summary>Failure speaks PropertiesReloadFailed; success is
    /// silent (post-write and post-rename refresh funnels).</summary>
    FailureOnly,

    /// <summary>The explicit Reload resolution: success speaks
    /// PropertiesReloaded, failure PropertiesReloadFailed.</summary>
    SuccessAndFailure,
}

/// <summary>
/// W4-4 (#736): the in-note properties header — one instance PER
/// TAB, owned by WorkspaceTabViewModel, so drafts and the expansion
/// state survive tab switches (the mac parked-draft posture).
///
/// Rows always re-derive from AUTHORITATIVE BYTES (feature
/// contract 4): ParseFrontmatterProperties over a fresh
/// ReadNoteParts — never from a local draft — and each row pins the
/// bundle's content hash AND OWNER PATH as its CAS identity
/// (contract 1; adversarial round 1: a row must never be actionable
/// against a different note than the read that produced it, so a
/// path change clears rows SYNCHRONOUSLY). Publishes are guarded by
/// generation + requestId two-token staleness (the W4-2/3 posture);
/// the VM never writes — commits route through the workspace seam
/// injected as delegates.
/// </summary>
internal sealed class NotePropertiesViewModel : PanelWorkScheduler
{
    private readonly VaultSession _session;
    private readonly Action<PropertyRowViewModel> _commit;
    private readonly Action<PropertyRowViewModel> _revertAnnounce;
    private readonly Action<PropertyRowViewModel> _requestDelete;
    private readonly Action<A11yEvent>? _announce;
    private long _generation;
    private int _requestId;
    private bool _isExpanded = true;
    private bool _isLoading;
    private string? _loadError;
    private string _path = "";

    public NotePropertiesViewModel(
        VaultSession session,
        Action<PropertyRowViewModel> commit,
        Action<PropertyRowViewModel> revertAnnounce,
        Action<PropertyRowViewModel> requestDelete,
        Action<A11yEvent>? announce = null,
        bool synchronousForTests = false)
        : base(synchronousForTests)
    {
        _session = session;
        _commit = commit;
        _revertAnnounce = revertAnnounce;
        _requestDelete = requestDelete;
        _announce = announce;
    }

    public ObservableCollection<PropertyRowViewModel> Rows { get; } = [];

    public string Path => _path;

    /// <summary>The content hash of the read that produced the
    /// current rows — the CAS token for ADD writes, which have no
    /// row to pin one (contract 1, same discipline). Empty until the
    /// first successful publish, after a failed one, and while a
    /// path change is loading.</summary>
    public string ContentHash { get; private set; } = "";

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetField(ref _isExpanded, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetField(ref _isLoading, value);
    }

    /// <summary>Non-null after a failed load — the honest surface;
    /// the last good rows are never silently kept as ghosts.</summary>
    public string? LoadError
    {
        get => _loadError;
        private set
        {
            if (SetField(ref _loadError, value))
            {
                OnPropertyChanged(nameof(HeaderText));
                OnPropertyChanged(nameof(HeaderGroupName));
                OnPropertyChanged(nameof(ShowEmptyState));
            }
        }
    }

    public string HeaderText => PropertyPhrase.HeaderText(Rows.Count);

    public string HeaderGroupName => PropertyPhrase.HeaderGroupName(Rows.Count);

    public string EmptyStateText => PropertyPhrase.EmptyState;

    public bool ShowEmptyState => Rows.Count == 0 && _loadError is null && !_isLoading;

    /// <summary>The rows scroll region collapses when empty — a
    /// zero-size but UIA-visible list is an axe violation.</summary>
    public bool HasRows => Rows.Count > 0;

    public bool AnyRowDirty => Rows.Any(row => row.IsDirty);

    /// <summary>The keys currently on the note — the add-property
    /// duplicate gate reads this, not a separate query.</summary>
    public IReadOnlyList<string> CurrentKeys => Rows.Select(row => row.Key).ToList();

    /// <summary>(Re)load for a path. A new PATH bumps the generation
    /// (parked work for the old path can never publish) and clears
    /// the old note's rows and hash SYNCHRONOUSLY — a stale row must
    /// not remain actionable while the new read is in flight
    /// (contract 1); a refresh on the same path takes a new
    /// requestId under the same generation.</summary>
    public void Load(string path)
    {
        if (path != _path)
        {
            _path = path;
            Interlocked.Increment(ref _generation);
            Rows.Clear();
            ContentHash = "";
            LoadError = null;
            OnPropertyChanged(nameof(HeaderText));
            OnPropertyChanged(nameof(HeaderGroupName));
            OnPropertyChanged(nameof(ShowEmptyState));
            OnPropertyChanged(nameof(HasRows));
            OnPropertyChanged(nameof(AnyRowDirty));
        }
        RefreshProperties();
    }

    /// <summary>Fresh read of the CURRENT path (post-write refresh,
    /// save-funnel refresh, external-change reconcile). The announce
    /// mode is CAPTURED with the request (round 3): the outcome is
    /// spoken at this refresh's completion, never eagerly, and a
    /// stale discarded publish cannot hand its announcement to a
    /// later request.</summary>
    public void RefreshProperties(ReloadAnnounce announce = ReloadAnnounce.None)
    {
        string path = _path;
        if (path.Length == 0)
        {
            return;
        }
        long generation = Interlocked.Read(ref _generation);
        int requestId = Interlocked.Increment(ref _requestId);
        IsLoading = true;
        StartWork(() =>
        {
            try
            {
                var parts = _session.ReadNoteParts(path);
                var properties = SlateUniffiMethods.ParseFrontmatterProperties(parts.FmSource);
                Post(() => PublishProperties(
                    generation, requestId, path, properties, parts.ContentHash, null,
                    announce));
            }
            catch (Exception exception) when (
                exception is not OutOfMemoryException
                    and not StackOverflowException
                    and not AccessViolationException)
            {
                Post(() => PublishProperties(
                    generation, requestId, path, [], "", exception.Message, announce));
            }
        });
    }

    /// <summary>Publish seam (internal for deterministic test
    /// ordering). Mutations before notifications; stale tokens
    /// discard silently.</summary>
    internal void PublishProperties(
        long generation,
        int requestId,
        string path,
        Property[] properties,
        string contentHash,
        string? loadError,
        ReloadAnnounce announce = ReloadAnnounce.None)
    {
        if (IsShutDown
            || generation != Interlocked.Read(ref _generation)
            || requestId != _requestId)
        {
            return;
        }
        // PARK dirty drafts before the rebuild (adversarial round 2,
        // contract 2 — the mac parked-draft posture): an authoritative
        // refresh must never silently destroy uncommitted input. The
        // parked draft lands on the rebuilt row for the same key with
        // the FRESH baseline and hash; if the refreshed disk value now
        // equals the draft, the row is naturally clean.
        var parkedDrafts = new Dictionary<string, PropertyDraft>(StringComparer.Ordinal);
        foreach (var existing in Rows)
        {
            if (existing.IsDirty)
            {
                parkedDrafts[existing.Key] = existing.Draft;
            }
        }
        Rows.Clear();
        ContentHash = loadError is null ? contentHash : "";
        if (loadError is null)
        {
            foreach (var property in properties)
            {
                var row = new PropertyRowViewModel(
                    property, path, contentHash, _commit, _revertAnnounce, _requestDelete);
                if (parkedDrafts.TryGetValue(row.Key, out PropertyDraft? parked))
                {
                    row.Draft = parked;
                }
                Rows.Add(row);
            }
        }
        IsLoading = false;
        LoadError = loadError;
        // Ownership-scoped announcements (round 3): only the request
        // that was GRANTED the outcome speaks — peer duplicate
        // headers refresh silently, so one user action announces
        // exactly once. The success echo is reserved for the
        // explicit reload flow.
        if (loadError is not null)
        {
            if (announce != ReloadAnnounce.None)
            {
                _announce?.Invoke(new A11yEvent.PropertiesReloadFailed(loadError));
            }
        }
        else if (announce == ReloadAnnounce.SuccessAndFailure)
        {
            _announce?.Invoke(new A11yEvent.PropertiesReloaded());
        }
        OnPropertyChanged(nameof(HeaderText));
        OnPropertyChanged(nameof(HeaderGroupName));
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(HasRows));
        OnPropertyChanged(nameof(AnyRowDirty));
    }

    internal override void Shutdown()
    {
        base.Shutdown();
        Interlocked.Increment(ref _generation);
    }
}
