// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.ObjectModel;
using uniffi.slate_uniffi;

namespace SlateWindows.Panels;

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
    private bool _announceReloadOutcome;

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
    /// save-funnel refresh, external-change reconcile). With
    /// announceReloadOutcome the publish speaks the canonical
    /// PropertiesReloaded / PropertiesReloadFailed for THIS refresh
    /// — success is announced at completion, never eagerly
    /// (contract 9, adversarial round 1).</summary>
    public void RefreshProperties(bool announceReloadOutcome = false)
    {
        string path = _path;
        if (path.Length == 0)
        {
            return;
        }
        if (announceReloadOutcome)
        {
            _announceReloadOutcome = true;
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
                    generation, requestId, path, properties, parts.ContentHash, null));
            }
            catch (Exception exception) when (
                exception is not OutOfMemoryException
                    and not StackOverflowException
                    and not AccessViolationException)
            {
                Post(() => PublishProperties(
                    generation, requestId, path, [], "", exception.Message));
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
        string? loadError)
    {
        if (IsShutDown
            || generation != Interlocked.Read(ref _generation)
            || requestId != _requestId)
        {
            return;
        }
        Rows.Clear();
        ContentHash = loadError is null ? contentHash : "";
        if (loadError is null)
        {
            foreach (var property in properties)
            {
                Rows.Add(new PropertyRowViewModel(
                    property, path, contentHash, _commit, _revertAnnounce, _requestDelete));
            }
        }
        IsLoading = false;
        LoadError = loadError;
        bool announceOutcome = _announceReloadOutcome;
        _announceReloadOutcome = false;
        // Any failed refresh surfaces the canonical reload-failure
        // event (the honest-containment posture); the success echo is
        // reserved for the explicit reload flow.
        if (loadError is not null)
        {
            _announce?.Invoke(new A11yEvent.PropertiesReloadFailed(loadError));
        }
        else if (announceOutcome)
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
