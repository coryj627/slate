// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using uniffi.slate_uniffi;

namespace SlateWindows.Graph;

/// <summary>
/// W6-2 PR B (B-5; 0bD-11): core's constants with accessible meaning,
/// fetched ONCE per process through <c>graph_constants</c> — the mac's
/// <c>graphConstantsOnce</c> (`AppState+Connections.swift:30`). A host that
/// re-types one has re-derived it; the leaf reads its depth bounds here
/// and clamps through core (B-15), never with a literal.
/// </summary>
internal static class GraphCoreConstants
{
    private static readonly Lazy<GraphConstants> Fetched = new(SlateUniffiMethods.GraphConstants);

    /// <summary>The record, fetched on first use and never again.</summary>
    public static GraphConstants Once => Fetched.Value;
}
