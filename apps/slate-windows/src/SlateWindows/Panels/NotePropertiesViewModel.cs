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

/// <summary>WB-2: how a wait for the header to publish a note's saved bytes
/// ended (<see cref="NotePropertiesViewModel.WhenPublished"/>).</summary>
internal enum HeaderRefreshOutcome
{
    /// <summary>A publication holds the saved bytes, or bytes read after the
    /// wait began (the disk moved past the save).</summary>
    Published,

    /// <summary>The read failed: the header shows its LoadError.</summary>
    Failed,

    /// <summary>The header shut down or went to another note; nothing it
    /// publishes is for this wait.</summary>
    Abandoned,
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
    // WB-2: waits for a publication of a note's saved bytes, settled by
    // every accepted publish and abandoned by a path change or shutdown.
    private readonly List<PublishWaiter> _publishWaiters = [];

    private sealed record PublishWaiter(
        string? SavedHash,
        int Floor,
        TaskCompletionSource<HeaderRefreshOutcome> Completion);

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
        private set
        {
            if (SetField(ref _isLoading, value))
            {
                // ShowEmptyState is derived from this. Without the
                // re-raise, RefreshProperties flips IsLoading silently
                // and the view keeps the stale value — so the header
                // renders "Loading properties" and "No properties yet.
                // Add one to start." at the same time, both reachable
                // by AT (WorkspaceTemplates.xaml:358-368).
                OnPropertyChanged(nameof(ShowEmptyState));
            }
        }
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

    /// <summary>The AUTHORITATIVE top-level YAML keys of the last
    /// publish (round 6): properties flatten nested containers away,
    /// so only this can tell the add sheet that a flat `person`
    /// would replace an existing `person:` mapping.</summary>
    public IReadOnlyList<string> TopLevelKeys { get; private set; } = [];

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
            // The in-flight read for the old note is discarded with the
            // generation, so a wait on it can never be answered.
            AbandonPublishWaiters();
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
    /// <summary>Discard the named draft on THIS header only — used by
    /// the conflict resolution so the edit that was actually
    /// conflicted is the one dropped (round 7).</summary>
    public void ReloadDiscarding(string discardDraftKey) =>
        RefreshProperties(ReloadAnnounce.SuccessAndFailure, discardDraftKey);

    public void RefreshProperties(
        ReloadAnnounce announce = ReloadAnnounce.None, string? discardDraftKey = null)
    {
        string path = _path;
        if (path.Length == 0)
        {
            return;
        }
        long generation = Interlocked.Read(ref _generation);
        int requestId = Interlocked.Increment(ref _requestId);
        IsLoading = true;
        Action? readHook = RefreshReadHookForTests;
        StartWork(() =>
        {
            try
            {
                readHook?.Invoke();
                var parts = _session.ReadNoteParts(path);
                var properties = SlateUniffiMethods.ParseFrontmatterProperties(parts.FmSource);
                var topLevel =
                    SlateUniffiMethods.FrontmatterTopLevelKeys(parts.FmSource);
                Post(() => PublishProperties(
                    generation, requestId, path, properties, parts.ContentHash, null,
                    announce, discardDraftKey, topLevel));
            }
            catch (Exception exception) when (
                exception is not OutOfMemoryException
                    and not StackOverflowException
                    and not AccessViolationException)
            {
                Post(() => PublishProperties(
                    generation, requestId, path, [], "", exception.Message, announce,
                    discardDraftKey, []));
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
        ReloadAnnounce announce = ReloadAnnounce.None,
        string? discardDraftKey = null,
        IReadOnlyList<string>? topLevelKeys = null)
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
        //
        // The EXPLICIT "Reload from Disk" resolution discards ONE
        // draft — the edit its dialog was about (rounds 5+6). It is
        // scoped by KEY, so unrelated dirty rows (and peer headers,
        // which never receive the key) keep their input: "discard
        // this property edit" is singular.
        var parkedDrafts = new Dictionary<string, PropertyDraft>(StringComparer.Ordinal);
        foreach (var existing in Rows)
        {
            if (existing.IsDirty
                && !string.Equals(existing.KeyIdentity, discardDraftKey, StringComparison.Ordinal))
            {
                parkedDrafts[existing.KeyIdentity] = existing.Draft;
            }
        }
        Rows.Clear();
        ContentHash = loadError is null ? contentHash : "";
        TopLevelKeys = loadError is null ? (topLevelKeys ?? []) : [];
        if (loadError is null)
        {
            foreach (var property in properties)
            {
                var row = new PropertyRowViewModel(
                    property, path, contentHash, _commit, _revertAnnounce, _requestDelete)
                {
                    Owner = this,
                    Position = Rows.Count,
                };
                if (parkedDrafts.TryGetValue(row.KeyIdentity, out PropertyDraft? parked))
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
        SettlePublishWaiters(requestId, contentHash, loadError);
    }

    internal override void Shutdown()
    {
        base.Shutdown();
        Interlocked.Increment(ref _generation);
        AbandonPublishWaiters();
    }

    /// <summary>WB-2 test seam: runs on the refresh worker inside the read's
    /// <c>try</c>, before <c>ReadNoteParts</c> — a fact parks a read there,
    /// and a hook that throws presents as a failed read.</summary>
    internal Action? RefreshReadHookForTests { get; set; }

    /// <summary>WB-2 test seam: the waits registered and not yet
    /// settled.</summary>
    internal int PublishWaitersForTests => _publishWaiters.Count;

    /// <summary>
    /// WB-2 (#1325; WD2, answered 2026-10-21): a task that ends once this
    /// header has published the bytes a save of <paramref name="path"/> wrote
    /// (<paramref name="savedHash"/>), so an admitted property write takes its
    /// CAS token from rows read AFTER the save — the header stays the single
    /// hash authority (contract 4). It always ends: <c>Published</c> when a
    /// publication holds the saved bytes, or bytes read after the wait began
    /// (the disk moved; the caller's gates take that to the conflict path);
    /// <c>Failed</c> on a failed read; <c>Abandoned</c> once the header shuts
    /// down or goes to another note (the in-flight read is then discarded).
    /// </summary>
    /// <remarks>Called and settled on the header's owner thread. A refresh
    /// already in flight — the Save command's, which speaks a failure — is
    /// never superseded; with none in flight the wait requests one, silently.
    /// A publication of other bytes from a request no later than the wait's
    /// floor may predate the save, so it re-arms the wait and asks once more.
    /// The task runs its continuations asynchronously, so a waiting operation
    /// never resumes inside a publish.</remarks>
    internal Task<HeaderRefreshOutcome> WhenPublished(string path, string? savedHash)
    {
        if (IsShutDown || !string.Equals(_path, path, StringComparison.Ordinal))
        {
            return Task.FromResult(HeaderRefreshOutcome.Abandoned);
        }
        if (_loadError is null
            && ContentHash.Length > 0
            && string.Equals(ContentHash, savedHash, StringComparison.Ordinal))
        {
            return Task.FromResult(HeaderRefreshOutcome.Published);
        }
        var waiter = new PublishWaiter(
            savedHash,
            _requestId,
            new TaskCompletionSource<HeaderRefreshOutcome>(TaskCreationOptions.RunContinuationsAsynchronously));
        // Registered before any request, so a synchronous header's inline
        // publish settles it.
        _publishWaiters.Add(waiter);
        if (!IsLoading)
        {
            RefreshProperties(ReloadAnnounce.None);
        }
        return waiter.Completion.Task;
    }

    private void SettlePublishWaiters(int requestId, string contentHash, string? loadError)
    {
        if (_publishWaiters.Count == 0)
        {
            return;
        }
        PublishWaiter[] waiters = [.. _publishWaiters];
        _publishWaiters.Clear();
        bool askAgain = false;
        foreach (PublishWaiter waiter in waiters)
        {
            if (loadError is not null)
            {
                _ = waiter.Completion.TrySetResult(HeaderRefreshOutcome.Failed);
            }
            else if (string.Equals(contentHash, waiter.SavedHash, StringComparison.Ordinal)
                || requestId > waiter.Floor)
            {
                _ = waiter.Completion.TrySetResult(HeaderRefreshOutcome.Published);
            }
            else
            {
                // Other bytes from a read that may predate the save: wait on.
                _publishWaiters.Add(waiter);
                askAgain = true;
            }
        }
        if (askAgain)
        {
            RefreshProperties(ReloadAnnounce.None);
        }
    }

    private void AbandonPublishWaiters()
    {
        PublishWaiter[] waiters = [.. _publishWaiters];
        _publishWaiters.Clear();
        foreach (PublishWaiter waiter in waiters)
        {
            _ = waiter.Completion.TrySetResult(HeaderRefreshOutcome.Abandoned);
        }
    }
}
