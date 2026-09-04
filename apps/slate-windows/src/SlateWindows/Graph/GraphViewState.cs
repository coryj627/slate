// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using uniffi.slate_uniffi;

namespace SlateWindows.Graph;

/// <summary>
/// W6-2 PR A (#746), contract A-1 / spec §1 R-B: the ONE view state of
/// the graph — five fields, owned by the document, and nothing else. The
/// selected node key (the table's current row IS the selection), the
/// backend filter, the name query, the groups and the mode. PR B's leaf
/// re-roots through it, PR C's filter and presets write it, PR D's diagram
/// moves <see cref="SelectedKey"/>. Every other copy of a filter, query or
/// mode under <c>Graph/</c> is immutable — a request, a token, an
/// envelope, a publication (contract A-2) — and the no-shadow census
/// asserts no mutable second copy exists.
/// </summary>
internal sealed class GraphViewState : BindableBase
{
    private string? _selectedKey;
    private GraphFilter _filter = DefaultFilter();
    private string _nameQuery = string.Empty;
    private IReadOnlyList<GraphGroup> _groups = [];
    private GraphSurfaceMode _mode = GraphSurfaceMode.Table;

    /// <summary>Core's default filter (`GraphFilter::default()`,
    /// graph.rs): notes and unresolved targets in, attachments out,
    /// every node — the mac's `graphTableFilter` default verbatim.</summary>
    internal static GraphFilter DefaultFilter() =>
        new(IncludeAttachments: false, IncludeGhosts: true, OrphansOnly: false);

    /// <summary>The shared cross-projection selection, keyed by core's
    /// <c>stable_key</c> (0b-3). Null when nothing is selected.</summary>
    public string? SelectedKey
    {
        get => _selectedKey;
        set => SetField(ref _selectedKey, value);
    }

    /// <summary>The backend filter the snapshot is fetched under.</summary>
    public GraphFilter Filter
    {
        get => _filter;
        set => SetField(ref _filter, value);
    }

    /// <summary>The name filter's needle (PR C writes it; empty here).</summary>
    public string NameQuery
    {
        get => _nameQuery;
        set => SetField(ref _nameQuery, value ?? string.Empty);
    }

    /// <summary>The config's group rules (PR C reads them; empty here).</summary>
    public IReadOnlyList<GraphGroup> Groups
    {
        get => _groups;
        set => SetField(ref _groups, value ?? []);
    }

    /// <summary>The surface mode; only Table is reachable in PR A.</summary>
    public GraphSurfaceMode Mode
    {
        get => _mode;
        set => SetField(ref _mode, value);
    }

    /// <summary>Retirement (contract A-1): back to the defaults.</summary>
    internal void Reset()
    {
        SelectedKey = null;
        Filter = DefaultFilter();
        NameQuery = string.Empty;
        Groups = [];
        Mode = GraphSurfaceMode.Table;
    }
}
