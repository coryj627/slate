// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using uniffi.slate_uniffi;

namespace SlateWindows.Search;

/// <summary>
/// Everything the vault-search overlay needs from the session layer,
/// and nothing else — the search twin of
/// <see cref="Commands.IPaletteCommandSource"/>.
/// </summary>
/// <remarks>
/// <para>
/// Split out so the overlay view model can be built and tested against
/// a fake, and so it cannot reach past this seam into workspace state.
/// <see cref="Search"/> is a thin pass-through to
/// <c>VaultSession.FullTextSearch</c>, which <b>blocks</b>
/// (<c>lib.rs:1349-1358</c>): the threading — debounce, off-dispatcher
/// execution, marshal-back, staleness — lives in the view model, never
/// here (contract S5).
/// </para>
/// </remarks>
internal interface ISearchSource
{
    /// <summary>
    /// Whether a vault is open. The overlay refuses to open without one
    /// and announces <c>SearchNeedsVault</c> instead; the open-flag must
    /// never be set in that case (the palette's P14 shape).
    /// </summary>
    bool IsVaultOpen { get; }

    /// <summary>
    /// An opaque identity for the current session, or
    /// <see langword="null"/> with no vault open. Captured at search
    /// dispatch and compared by reference before a result publishes —
    /// the session arm of contract S5's four-way staleness check. Not
    /// theoretical: mac added the same guard after a direct vault
    /// switch published vault A's rows into vault B's overlay
    /// (<c>AppState.swift:8934-8949</c>).
    /// </summary>
    object? SessionIdentity { get; }

    /// <summary>
    /// Run <c>full_text_search</c> through the binding. Synchronous and
    /// blocking; the caller owns the thread hop (contract S5). Core
    /// owns matching, ranking, snippet extraction, and the summary
    /// string (contract S1) — implementations re-derive none of it.
    /// </summary>
    QueryResultSet Search(string query, SearchScope scope, CancelToken cancel);

    /// <summary>
    /// The persisted recent queries, most-recent-first (contract S14).
    /// A missing, malformed, oversized, or unreadable store degrades to
    /// an empty list, never a throw — the overlay must open regardless
    /// of recents state.
    /// </summary>
    IReadOnlyList<string> LoadRecents();

    /// <summary>
    /// Record an activated query (contract S9: activation is the only
    /// caller — never per keystroke). Persistence failure is non-fatal.
    /// </summary>
    void RecordRecent(string query);

    /// <summary>
    /// Forget every remembered query (contract S14). Persists an empty
    /// list rather than deleting the file. On a write failure the
    /// on-disk list survives; callers re-read through
    /// <see cref="LoadRecents"/> so the UI stays honest about it.
    /// </summary>
    void ClearRecents();
}
