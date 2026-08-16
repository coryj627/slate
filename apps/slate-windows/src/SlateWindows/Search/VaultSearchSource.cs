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
/// The recents store is constructed per call from the current vault
/// root: it is a stateless path holder (the file is re-read on every
/// operation, mac's documented single-writer discipline), so there is
/// nothing to cache and no per-vault instance to invalidate.
/// </para>
/// </remarks>
internal sealed class VaultSearchSource : ISearchSource
{
    private readonly Func<VaultSession?> _session;
    private readonly Func<string?> _vaultRoot;

    public VaultSearchSource(Func<VaultSession?> session, Func<string?> vaultRoot)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(vaultRoot);
        _session = session;
        _vaultRoot = vaultRoot;
    }

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
    public IReadOnlyList<string> LoadRecents() =>
        _vaultRoot() is string root ? new SearchRecentsStore(root).Load() : [];

    /// <inheritdoc />
    public void RecordRecent(string query)
    {
        if (_vaultRoot() is string root)
        {
            _ = new SearchRecentsStore(root).Add(query);
        }
    }

    /// <inheritdoc />
    public void ClearRecents()
    {
        if (_vaultRoot() is string root)
        {
            new SearchRecentsStore(root).Clear();
        }
    }
}
