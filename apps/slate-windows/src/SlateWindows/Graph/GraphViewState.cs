// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using uniffi.slate_uniffi;

namespace SlateWindows.Graph;

/// <summary>
/// W6-2 PR A (#746), contract A-1 / spec §1 R-B: the ONE view state of
/// the graph — five fields, and nothing else. The selected node key (the
/// table's current row IS the selection), the backend filter, the name
/// query, the groups and the mode. Owned by the DOCUMENT until the owner's
/// amendment of A-1 and R-B on 2026-09-06 (W6-2 PR B2, B2D-1; B2-1): now
/// the WORKSPACE's one instance, constructed beside the relay, handed to
/// the graph document and the Connections leaf, surviving the document's
/// retirement and dropped with the workspace — there is no reset. The
/// document's guarded selection writes it, PR B2's leaf re-roots through
/// it, PR C's filter and presets write it, PR D's diagram moves
/// <see cref="SelectedKey"/>. Every other copy of a filter, query or mode
/// in the shell is immutable — a request, a token, an envelope, a
/// publication (contract A-2) — and the no-shadow census asserts no
/// mutable second copy exists, by type, across the whole shell.
/// </summary>
internal sealed class GraphViewState : BindableBase
{
    private string? _selectedKey;
    private int _selectionGeneration;
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
        set
        {
            // Every write moves the selection's generation, even to the same
            // key: a publication revalidates only the selection it observed
            // when its fetch began (IPC-8).
            Interlocked.Increment(ref _selectionGeneration);
            SetField(ref _selectedKey, value);
        }
    }

    /// <summary>The count of writes to <see cref="SelectedKey"/>: a graph
    /// load's worker reads it on the pool immediately before its snapshot
    /// crossing — not when the load was issued (codex post-implementation
    /// pass 3, IPC-13: a write between the issue and the fetch is OLDER than
    /// the snapshot and is that publication's to judge) — and its
    /// publication clears the key only while no write has happened since:
    /// an older snapshot never erases a key written after it was fetched
    /// (pass 2, IPC-8; A-7). Written on the owner's thread, read from the
    /// pool: a volatile read of an interlocked count.</summary>
    public int SelectionGeneration => Volatile.Read(ref _selectionGeneration);

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
}
