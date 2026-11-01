// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Graph;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// Plan 44 WB-28 PR 2: the writer facts' by-path forms. The writer keys its
/// state by the root (<see cref="VaultRootAnchor.Scope"/>); a fact with no
/// session names the root by its path, which only this assembly may do, and
/// the forms live here so the writer's own callers stay the census's.
/// </summary>
internal static class GraphConfigWriterByPath
{
    internal static ulong Reserve(this GraphConfigWriter writer, string root) =>
        writer.Reserve(VaultRootAnchor.ByPathForTests(root));

    internal static Task Enqueue(this GraphConfigWriter writer, string root, GraphConfig aggregate, ulong generation) =>
        writer.Enqueue(VaultRootAnchor.ByPathForTests(root), aggregate, generation);

    internal static GraphConfig? Newest(this GraphConfigWriter writer, string root) =>
        writer.Newest(VaultRootAnchor.ByPathForTests(root));

    internal static (ulong Counter, ulong Admitted, ulong LastWritten, int Outstanding) StateForTests(
        this GraphConfigWriter writer,
        string root) =>
        writer.StateForTests(VaultRootAnchor.ByPathForTests(root));
}
