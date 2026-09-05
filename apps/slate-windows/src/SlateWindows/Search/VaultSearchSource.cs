// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using uniffi.slate_uniffi;

namespace SlateWindows.Search;

/// <summary>
/// The production <see cref="ISearchSource"/>: live delegates onto the
/// shell's current session and vault root, so one app-lifetime instance
/// survives vault switches the way <see cref="Commands.PaletteCommandSource"/>
/// survives them through <c>ISlateCommandHost</c>.
/// </summary>
/// <remarks>
/// <para>
/// Reading the session per call — rather than capturing one at
/// construction — is what gives <see cref="SessionIdentity"/> its
/// meaning: after a vault switch the property returns the new session
/// object, and any in-flight search dispatched against the old one
/// fails the view model's identity comparison and is discarded
/// (contract S5).
/// </para>
/// <para>
/// The recents store is cached, one instance per vault root, and
/// rebuilt when the root changes. The file is still re-read on every
/// operation (mac's documented single-writer discipline) — the cache
/// exists for the store's one piece of state:
/// <see cref="SearchRecentsStore.LastSaveError"/>. The first version
/// constructed a store per call, which discarded every save failure
/// with the instance that recorded it, leaving the property dead
/// (red-team round 1).
/// </para>
/// </remarks>
internal sealed class VaultSearchSource : ISearchSource
{
    private readonly Func<VaultSession?> _session;
    private readonly Func<string?> _vaultRoot;

    private SearchRecentsStore? _recents;
    private string? _recentsRoot;

    public VaultSearchSource(Func<VaultSession?> session, Func<string?> vaultRoot)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(vaultRoot);
        _session = session;
        _vaultRoot = vaultRoot;
    }

    /// <summary>The cached store's last persistence failure, or
    /// <see langword="null"/> — non-fatal, but observable, which is the
    /// point of caching the store at all.</summary>
    public Exception? LastRecentsSaveError => _recents?.LastSaveError;

    /// <inheritdoc />
    public bool IsVaultOpen => _session() is not null;

    /// <inheritdoc />
    public object? SessionIdentity => _session();

    /// <inheritdoc />
    public QueryResultSet Search(string query, SearchScope scope, CancelToken cancel)
    {
        VaultSession session = _session()
            ?? throw new InvalidOperationException(
                "Search was dispatched with no vault open.");
        return session.FullTextSearch(query, scope, cancel);
    }

    /// <inheritdoc />
    public IReadOnlyList<string> LoadRecents() => RecentsStore()?.Load() ?? [];

    /// <inheritdoc />
    public void RecordRecent(string query) => _ = RecentsStore()?.Add(query);

    /// <inheritdoc />
    public void ClearRecents() => RecentsStore()?.Clear();

    /// <summary>
    /// The one store for the current vault root, or
    /// <see langword="null"/> with no vault open. Identical behaviour
    /// to per-call construction — the store re-reads the file on every
    /// operation — except that <see cref="SearchRecentsStore.LastSaveError"/>
    /// now survives the call that set it.
    /// </summary>
    private SearchRecentsStore? RecentsStore()
    {
        if (_vaultRoot() is not string root)
        {
            return null;
        }

        if (_recents is null
            || !string.Equals(_recentsRoot, root, StringComparison.Ordinal))
        {
            _recents = new SearchRecentsStore(root);
            _recentsRoot = root;
        }

        return _recents;
    }
}
