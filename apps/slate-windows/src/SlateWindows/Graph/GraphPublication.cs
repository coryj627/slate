// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using uniffi.slate_uniffi;

namespace SlateWindows.Graph;

/// <summary>The four load states of contract A-4, in precedence order
/// as the surface applies them.</summary>
internal enum GraphLoadState
{
    /// <summary>A pair is in flight and no snapshot is held.</summary>
    Loading,

    /// <summary>The last PAIR failed; no snapshot is held.</summary>
    Error,

    /// <summary>A snapshot is held and the rows are empty.</summary>
    Empty,

    /// <summary>A snapshot is held and rows are published.</summary>
    Ready,
}

/// <summary>
/// W6-2 PR A (#746), contract A-2 (rule A): the ONE immutable publication
/// record the document installs in one property swap — the snapshot with
/// the filter it was fetched under and its generation, the rows, the
/// total, the accepted sort, the summary text and the load state. Every
/// observer binds from this record, so no handler can see a candidate
/// snapshot with old rows or new rows with an old total. It carries no
/// derived index of the snapshot (spec R-A): a membership question scans
/// <see cref="Snapshot"/>'s nodes.
/// </summary>
internal sealed class GraphPublication
{
    private GraphPublication(
        GraphSnapshot? snapshot,
        GraphFilter filter,
        ulong generation,
        IReadOnlyList<GraphTableRow> rows,
        ulong total,
        GraphTableSort acceptedSort,
        string summary,
        GraphLoadState state,
        string? error)
    {
        Snapshot = snapshot;
        Filter = filter;
        Generation = generation;
        Rows = rows;
        Total = total;
        AcceptedSort = acceptedSort;
        Summary = summary;
        State = state;
        Error = error;
    }

    /// <summary>The authority's snapshot; null under LOADING and ERROR.</summary>
    public GraphSnapshot? Snapshot { get; }

    /// <summary>The filter the snapshot was fetched under.</summary>
    public GraphFilter Filter { get; }

    /// <summary>The snapshot's generation; 0 when none is held.</summary>
    public ulong Generation { get; }

    /// <summary>Core's rows under the accepted sort — kept hidden under
    /// ERROR (the mac's row buffer survives, `AppState+GraphTable.swift:257–265`).</summary>
    public IReadOnlyList<GraphTableRow> Rows { get; }

    /// <summary>The node count under the backend filter alone (0b-7).</summary>
    public ulong Total { get; }

    /// <summary>The sort the rows are in.</summary>
    public GraphTableSort AcceptedSort { get; }

    /// <summary>Core's <c>audio_summary</c>, verbatim (contract A-7).</summary>
    public string Summary { get; }

    public GraphLoadState State { get; }

    /// <summary>The humanised failure message under ERROR.</summary>
    public string? Error { get; }

    /// <summary>READY in rule L's sense: a snapshot is held.</summary>
    public bool HoldsSnapshot => Snapshot is not null;

    /// <summary>Membership by scanning the snapshot's nodes — no derived
    /// set (spec R-A; the round-4 ledger's IGA-48).</summary>
    public bool ContainsNode(string stableKey)
    {
        if (Snapshot is null)
        {
            return false;
        }
        foreach (GraphNode node in Snapshot.Nodes)
        {
            if (string.Equals(node.StableKey, stableKey, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>The document's starting record: no snapshot, LOADING
    /// (nothing has been asked for yet; the first pair keeps it there).</summary>
    public static GraphPublication Initial(GraphFilter filter, GraphTableSort defaultSort) =>
        new(null, filter, 0, [], 0, defaultSort, string.Empty, GraphLoadState.Loading, null);

    /// <summary>A PAIR result replacing the authority (rule A).</summary>
    public static GraphPublication FromPair(
        GraphSnapshot snapshot,
        GraphFilter filter,
        GraphTableRows rows,
        GraphTableSort acceptedSort) =>
        new(
            snapshot,
            filter,
            snapshot.Generation,
            rows.Rows,
            rows.Total,
            acceptedSort,
            snapshot.AudioSummary,
            rows.Rows.Length == 0 ? GraphLoadState.Empty : GraphLoadState.Ready,
            null);

    /// <summary>A ROWS-ONLY result over the held authority (rule A).</summary>
    public GraphPublication WithRows(GraphTableRows rows, GraphTableSort acceptedSort) =>
        new(
            Snapshot,
            Filter,
            Generation,
            rows.Rows,
            rows.Total,
            acceptedSort,
            Summary,
            rows.Rows.Length == 0 ? GraphLoadState.Empty : GraphLoadState.Ready,
            null);

    /// <summary>A PAIR failure: the snapshot dropped, the rows kept
    /// hidden, ERROR with the message (contract A-2's first failure arm).</summary>
    public GraphPublication AsPairFailure(string message) =>
        new(null, Filter, 0, Rows, Total, AcceptedSort, Summary, GraphLoadState.Error, message);
}
