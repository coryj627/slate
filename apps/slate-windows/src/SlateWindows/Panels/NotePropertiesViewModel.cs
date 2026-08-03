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
/// bundle's content hash as its CAS token (contract 1). Publishes
/// are guarded by generation + requestId two-token staleness (the
/// W4-2/3 posture); the VM never writes — commits route through the
/// workspace seam injected as delegates.
/// </summary>
internal sealed class NotePropertiesViewModel : PanelWorkScheduler
{
    private readonly VaultSession _session;
    private readonly Action<PropertyRowViewModel> _commit;
    private readonly Action<PropertyRowViewModel> _revertAnnounce;
    private readonly Action<PropertyRowViewModel> _requestDelete;
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
        bool synchronousForTests = false)
        : base(synchronousForTests)
    {
        _session = session;
        _commit = commit;
        _revertAnnounce = revertAnnounce;
        _requestDelete = requestDelete;
    }

    public ObservableCollection<PropertyRowViewModel> Rows { get; } = [];

    public string Path => _path;

    /// <summary>The content hash of the read that produced the
    /// current rows — the CAS token for ADD writes, which have no
    /// row to pin one (contract 1, same discipline). Empty until the
    /// first successful publish and after a failed one.</summary>
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

    public bool AnyRowDirty => Rows.Any(row => row.IsDirty);

    /// <summary>The keys currently on the note — the add-property
    /// duplicate gate reads this, not a separate query.</summary>
    public IReadOnlyList<string> CurrentKeys => Rows.Select(row => row.Key).ToList();

    /// <summary>(Re)load for a path. A new PATH bumps the generation
    /// (parked work for the old path can never publish); a refresh
    /// on the same path takes a new requestId under the same
    /// generation.</summary>
    public void Load(string path)
    {
        if (path != _path)
        {
            _path = path;
            Interlocked.Increment(ref _generation);
        }
        RefreshProperties();
    }

    /// <summary>Fresh read of the CURRENT path (post-write refresh,
    /// save-funnel refresh, external-change reconcile).</summary>
    public void RefreshProperties()
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
                    generation, requestId, properties, parts.ContentHash, null));
            }
            catch (Exception exception) when (
                exception is not OutOfMemoryException
                    and not StackOverflowException
                    and not AccessViolationException)
            {
                Post(() => PublishProperties(
                    generation, requestId, [], "", exception.Message));
            }
        });
    }

    /// <summary>Publish seam (internal for deterministic test
    /// ordering). Mutations before notifications; stale tokens
    /// discard silently.</summary>
    internal void PublishProperties(
        long generation,
        int requestId,
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
                    property, contentHash, _commit, _revertAnnounce, _requestDelete));
            }
        }
        IsLoading = false;
        LoadError = loadError;
        OnPropertyChanged(nameof(HeaderText));
        OnPropertyChanged(nameof(HeaderGroupName));
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(AnyRowDirty));
    }

    internal override void Shutdown()
    {
        base.Shutdown();
        Interlocked.Increment(ref _generation);
    }
}
