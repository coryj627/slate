// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// W6-2 PR 0b (contracts doc 0b-15): every name in core's
// `GRAPH_QUERY_SURFACE` is a method on the generated binding — a free
// function on `SlateUniffiMethods` or a `VaultSession` method — under the
// C# spelling. The list is read from core's source, never copied, so a
// query added there without a binding fails here.

using System.Reflection;
using System.Text.RegularExpressions;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "graph-query-surface")]
public class GraphQuerySurfaceCensus
{
    private static string RepoRoot
    {
        get
        {
            string dir = AppContext.BaseDirectory;
            for (int i = 0; i < 8; i++)
            {
                dir = Path.GetDirectoryName(dir)!;
            }
            return dir;
        }
    }

    /// <summary>The names inside core's `GRAPH_QUERY_SURFACE` const block.</summary>
    public static IReadOnlyList<string> CoreSurface()
    {
        string source = File.ReadAllText(
            Path.Combine(RepoRoot, "crates", "slate-core", "src", "graph_queries.rs"));
        int start = source.IndexOf("pub const GRAPH_QUERY_SURFACE", StringComparison.Ordinal);
        Assert.True(start >= 0, "GRAPH_QUERY_SURFACE is declared in graph_queries.rs");
        int end = source.IndexOf("];", start, StringComparison.Ordinal);
        return Regex.Matches(source[start..end], "\"(graph_[a-z_]+)\"")
            .Select(m => m.Groups[1].Value)
            .ToList();
    }

    public static string PascalCase(string snake) =>
        string.Concat(snake.Split('_').Select(part => char.ToUpperInvariant(part[0]) + part[1..]));

    [Fact]
    public void EveryCoreGraphQueryIsBound()
    {
        var surface = CoreSurface();
        // W6-2 PR A (A-5): `graph_table_default_sort` joined the surface.
        Assert.True(surface.Count >= 24, $"the surface list holds {surface.Count} names");
        foreach (string name in surface)
        {
            string method = PascalCase(name);
            bool free = typeof(SlateUniffiMethods).GetMethod(method, BindingFlags.Public | BindingFlags.Static) != null;
            bool session = typeof(VaultSession).GetMethod(method, BindingFlags.Public | BindingFlags.Instance) != null;
            Assert.True(free || session, $"{name} ({method}) is bound neither as a free function nor as a session method");
        }
    }
}
