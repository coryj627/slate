// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// W7-7 PR 4b (#1247, R-5; the owner's S5): a grid landing is the reader's
// CURRENT cell, else the first — silently — through the grid's owner
// (SelectorFocus.LandOnStop, AccessibleDataGrid.FocusCurrentOrFirstCell).
// FocusFirstCell always seats row 0 and announces the move: from a canvas
// table's filter it moved the canvas seat to card 1 and narrated it (the
// completeness sweep's G16), and a Bases Escape did the same. It is the §8.7
// window-open entry point, and only such an entry point — or the grid's own
// empty-grid stop — may call it; every call in the shell's code is bound
// and held to the list written here.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "focus-first-cell")]
public sealed class FocusFirstCellCensus
{
    /// <summary>The callers allowed FocusFirstCell, by declaring type and
    /// member, and why.</summary>
    private static readonly Dictionary<string, string> Allowed = new(StringComparer.Ordinal)
    {
        ["SlateWindows.Reading.ReadingTableGrid.Show"] = "the reading table window's open: the §8.7 entry point, headers and the first cell",
        ["SlateWindows.Grids.AccessibleDataGrid.FocusCurrentOrFirstCell"] = "an EMPTY grid's own stop (AR-6), the grid itself",
    };

    [Fact]
    public void OnlyAWindowOpenEntryPointCallsFocusFirstCell()
    {
        var callers = new List<string>();
        foreach ((string _, CSharpSource source) in ShellCompilation.Sources)
        {
            SemanticModel model = ShellCompilation.ModelFor(source);
            foreach (InvocationExpressionSyntax call in source.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (model.GetSymbolInfo(call).Symbol is not IMethodSymbol { Name: "FocusFirstCell" } method
                    || method.ContainingType.Name != "AccessibleDataGrid")
                {
                    continue;
                }

                ISymbol? caller = model.GetEnclosingSymbol(call.SpanStart);
                while (caller is IMethodSymbol { MethodKind: MethodKind.AnonymousFunction or MethodKind.LocalFunction })
                {
                    caller = caller.ContainingSymbol;
                }

                callers.Add($"{caller?.ContainingType?.ToDisplayString()}.{caller?.Name}");
            }
        }

        Assert.NotEmpty(callers);
        string[] unlisted = [.. callers.Where(caller => !Allowed.ContainsKey(caller)).Distinct()];
        Assert.True(
            unlisted.Length == 0,
            "FocusFirstCell called from a landing that is no window-open entry point (S5 — land through "
            + "SelectorFocus.LandOnStop for the current or first cell, silently):\n  " + string.Join("\n  ", unlisted));
        Assert.All(Allowed.Keys, caller => Assert.Contains(caller, callers));
    }
}
