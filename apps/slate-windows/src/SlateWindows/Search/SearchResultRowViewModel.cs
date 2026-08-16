// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using uniffi.slate_uniffi;

namespace SlateWindows.Search;

/// <summary>
/// One search result row. Purely data: the view binds it, the overlay
/// view model navigates it.
/// </summary>
/// <remarks>
/// <para>
/// Rows arrive from core already sorted and are published in that order
/// (contract S1); core's <c>Score</c> is deliberately not surfaced here
/// so nothing can re-sort or present it. There is no line number —
/// <c>QueryHit</c> carries none by design (#92 item 1); the line shown
/// after activation is derived host-side from the loaded note.
/// </para>
/// <para>
/// Each row publishes exactly one UIA stop, named by
/// <see cref="AccessibleName"/> (contract S4). The emphasis runs in
/// <see cref="SnippetSegments"/> are presentation only and contribute
/// no stops; the full <see cref="Path"/> is a tooltip, not part of the
/// name.
/// </para>
/// </remarks>
internal sealed class SearchResultRowViewModel
{
    public SearchResultRowViewModel(QueryHit hit)
        : this(hit.Path, hit.Snippet)
    {
    }

    public SearchResultRowViewModel(string path, string snippet)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(snippet);
        Path = path;
        Basename = LastPathSegment(path);
        SnippetSegments = SnippetRuns.Split(snippet);
        StrippedSnippet = SnippetRuns.StripMarkers(snippet);
    }

    /// <summary>Vault-relative path, forward slashes on every platform
    /// (contract S1, <c>session.rs:297</c>). Bound as the row tooltip.</summary>
    public string Path { get; }

    /// <summary>The last path segment. Split on <c>'/'</c> only — vault
    /// paths never use <c>'\'</c> as a separator, and a mac-authored
    /// note name may legally contain one.</summary>
    public string Basename { get; }

    /// <summary>The snippet split into emphasis runs (contract S3).</summary>
    public IReadOnlyList<SnippetRun> SnippetSegments { get; }

    /// <summary>The snippet with both hit markers removed.</summary>
    public string StrippedSnippet { get; }

    /// <summary>
    /// The row's whole accessible name (contract S4), matching mac's
    /// <c>rowAccessibilityLabel</c> (<c>SearchOverlay.swift:519-524</c>).
    /// With an empty snippet this yields <c>"{basename}: "</c> exactly
    /// as mac does — every empty-query tag-scope row has one — and that
    /// parity is deliberate.
    /// </summary>
    public string AccessibleName => $"{Basename}: {StrippedSnippet}";

    private static string LastPathSegment(string path)
    {
        int separator = path.LastIndexOf('/');
        return separator < 0 ? path : path[(separator + 1)..];
    }
}
