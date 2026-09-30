// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// W7-7 PR 4 (#1247, contract R-5; codex PR 4 round 7's findings 1, 2 and 4 —
// the owner's structural rule): with focus-without-select, a KEYBOARD action
// and a menu the keyboard opens act on the FOCUSED row — the row the reader
// hears — and the container's selection only when the keys are on no row of
// it. A landing focuses a row without selecting it (OD-11b), and every
// handler that read SelectedItem acted on the wrong row or on none: Enter on
// a landed Backlinks row did nothing, Space on a landed task only selected
// it, Enter on the Bases list row the quick filter's Escape landed on opened
// nothing, and Connections' Enter and Shift+F10 acted on a selection hidden
// under a collapsed row. The census binds every keyboard and menu handler in
// the shell's code — a method or lambda taking KeyEventArgs or
// ContextMenuEventArgs — and the shell methods it calls, two calls deep, and
// fails on any READ of a list's or a tree's SelectedItem, SelectedValue or
// SelectedIndex there (a member access or a property pattern): the row comes
// from SelectorFocus.FocusedOrSelectedItem (or FocusedItem). A combo box is
// its own stop, and a tab control's row selects itself when it takes the
// keys; neither can part focus from selection. An assignment is not a read.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "focused-row")]
public sealed class FocusedRowCensus
{
    private static readonly string[] SelectionMembers = ["SelectedItem", "SelectedValue", "SelectedIndex"];

    [Fact]
    public void EveryKeyboardAndMenuHandlerActsOnTheFocusedRow()
    {
        (string[] offenders, int handlers) = Offenders(
            ShellCompilation.Compilation,
            ShellCompilation.Sources.Select(source => source.Source.Root.SyntaxTree));

        Assert.True(handlers >= 30, $"only {handlers} keyboard and menu handlers were found; the scrape is broken.");
        Assert.True(
            offenders.Length == 0,
            "Keyboard or menu handlers that read a list's or a tree's selection instead of the row that holds the keys "
            + "(use SelectorFocus.FocusedOrSelectedItem):\n  " + string.Join("\n  ", offenders));
    }

    /// <summary>The census's own witness: a handler's read, a callee's read
    /// and a property pattern are caught; the helper, an assignment, a combo
    /// box, a tab control and a handler of another event are not.</summary>
    [Fact]
    public void ThePlantedSelectionReadsAreCaught()
    {
        SyntaxTree planted = CSharpSyntaxTree.ParseText(
            """
            using System.Windows.Controls;
            using System.Windows.Input;

            static class SelectorFocus
            {
                internal static object? FocusedOrSelectedItem(ItemsControl container) => null;
            }

            sealed class Planted
            {
                private readonly ListBox _list = new();
                private readonly TreeView _tree = new();
                private readonly ComboBox _combo = new();
                private readonly TabControl _tabs = new();

                void List_KeyDown(object sender, KeyEventArgs e)
                {
                    if (_list.SelectedItem is string row) { }
                }

                void Tree_KeyDown(object sender, KeyEventArgs e) => Act();

                void Act()
                {
                    object? current = _tree.SelectedItem;
                }

                void Pattern_KeyDown(object sender, KeyEventArgs e)
                {
                    if (sender is ListBox { SelectedIndex: 0 }) { }
                }

                void Menu_Opening(object sender, ContextMenuEventArgs e)
                {
                    object? target = SelectorFocus.FocusedOrSelectedItem(_list);
                    _list.SelectedItem = target;
                    object? choice = _combo.SelectedItem;
                    object? tab = _tabs.SelectedItem;
                }

                void List_SelectionChanged(object sender, SelectionChangedEventArgs e)
                {
                    object? selected = _list.SelectedItem;
                }
            }
            """,
            path: "Planted.cs");
        CSharpCompilation compilation = CSharpCompilation.Create(
            "planted",
            [planted],
            ShellCompilation.Compilation.References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        (string[] offenders, int handlers) = Offenders(compilation, [planted]);

        Assert.Equal(4, handlers);
        Assert.Equal(
            [
                "Planted.cs:18 in Planted.List_KeyDown: _list.SelectedItem",
                "Planted.cs:25 in Planted.Act (from Planted.Tree_KeyDown): _tree.SelectedItem",
                "Planted.cs:30 in Planted.Pattern_KeyDown: ListBox { SelectedIndex }",
            ],
            offenders);
    }

    private static (string[] Offenders, int Handlers) Offenders(CSharpCompilation compilation, IEnumerable<SyntaxTree> trees)
    {
        var offenders = new SortedSet<string>(StringComparer.Ordinal);
        int handlers = 0;
        foreach (SyntaxTree tree in trees)
        {
            SemanticModel model = compilation.GetSemanticModel(tree);
            foreach (SyntaxNode node in tree.GetRoot().DescendantNodes())
            {
                if (HandlerSymbol(node, model) is not { } handler)
                {
                    continue;
                }

                handlers++;
                var visited = new HashSet<SyntaxNode>();
                Visit(node, handler.ToDisplayString(Short), null, depth: 0);

                void Visit(SyntaxNode body, string where, string? from, int depth)
                {
                    if (!visited.Add(body))
                    {
                        return;
                    }

                    SemanticModel bodyModel = compilation.GetSemanticModel(body.SyntaxTree);
                    foreach (string read in SelectionReads(body, bodyModel))
                    {
                        offenders.Add($"{read.Split('|')[0]} in {where}{(from is null ? "" : $" (from {from})")}: {read.Split('|')[1]}");
                    }

                    if (depth >= 2)
                    {
                        return;
                    }

                    foreach (InvocationExpressionSyntax call in body.DescendantNodes().OfType<InvocationExpressionSyntax>())
                    {
                        // The one sanctioned reader: the focused row first,
                        // the selection only as its fallback.
                        if (bodyModel.GetSymbolInfo(call).Symbol is not IMethodSymbol callee
                            || callee is { Name: "FocusedOrSelectedItem", ContainingType.Name: "SelectorFocus" })
                        {
                            continue;
                        }

                        foreach (SyntaxReference reference in callee.DeclaringSyntaxReferences)
                        {
                            if (compilation.SyntaxTrees.Contains(reference.SyntaxTree))
                            {
                                Visit(reference.GetSyntax(), callee.ToDisplayString(Short), from ?? where, depth + 1);
                            }
                        }
                    }
                }
            }
        }

        return (offenders.ToArray(), handlers);
    }

    private static readonly SymbolDisplayFormat Short = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly,
        memberOptions: SymbolDisplayMemberOptions.IncludeContainingType);

    /// <summary>The method a keyboard or menu handler is — a method, local
    /// function or lambda with a KeyEventArgs or ContextMenuEventArgs
    /// parameter — else null.</summary>
    private static IMethodSymbol? HandlerSymbol(SyntaxNode node, SemanticModel model)
    {
        IMethodSymbol? method = node switch
        {
            MethodDeclarationSyntax declaration => model.GetDeclaredSymbol(declaration),
            LocalFunctionStatementSyntax local => model.GetDeclaredSymbol(local) as IMethodSymbol,
            AnonymousFunctionExpressionSyntax lambda => model.GetSymbolInfo(lambda).Symbol as IMethodSymbol,
            _ => null,
        };
        return method is not null && method.Parameters.Any(parameter => parameter.Type.ToDisplayString()
            is "System.Windows.Input.KeyEventArgs" or "System.Windows.Controls.ContextMenuEventArgs")
            ? method
            : null;
    }

    /// <summary>Every read of a landing container's selection in
    /// <paramref name="body"/>, as "file:line|what".</summary>
    private static IEnumerable<string> SelectionReads(SyntaxNode body, SemanticModel model)
    {
        foreach (SyntaxNode node in body.DescendantNodes())
        {
            switch (node)
            {
                case MemberAccessExpressionSyntax access
                    when SelectionMembers.Contains(access.Name.Identifier.ValueText)
                        && !(access.Parent is AssignmentExpressionSyntax assignment && assignment.Left == access):
                    bool? verdict = IsLandingContainer(model.GetTypeInfo(access.Expression).Type);
                    if (verdict == true)
                    {
                        yield return $"{Where(access)}|{access}";
                    }
                    else if (verdict is null)
                    {
                        yield return $"{Where(access)}|{access} (unbound — build the shell first)";
                    }

                    break;
                case SubpatternSyntax { ExpressionColon.Expression: IdentifierNameSyntax name } subpattern
                    when SelectionMembers.Contains(name.Identifier.ValueText)
                        && subpattern.Parent?.Parent is RecursivePatternSyntax { Type: { } typeSyntax }
                        && IsLandingContainer(model.GetTypeInfo(typeSyntax).Type) == true:
                    yield return $"{Where(subpattern)}|{typeSyntax} {{ {name.Identifier.ValueText} }}";
                    break;
            }
        }
    }

    /// <summary>True for a list or a tree, false for anything else — a
    /// combo box and a tab control among them — and null when the type
    /// did not bind.</summary>
    private static bool? IsLandingContainer(ITypeSymbol? type)
    {
        if (type is null or IErrorTypeSymbol)
        {
            return null;
        }

        bool Is(string name)
        {
            for (ITypeSymbol? current = type; current is not null; current = current.BaseType)
            {
                if (current.ToDisplayString() == name)
                {
                    return true;
                }
            }

            return false;
        }

        if (Is("System.Windows.Controls.ComboBox") || Is("System.Windows.Controls.TabControl"))
        {
            return false;
        }

        return Is("System.Windows.Controls.Primitives.Selector") || Is("System.Windows.Controls.TreeView");
    }

    private static string Where(SyntaxNode node)
    {
        FileLinePositionSpan span = node.GetLocation().GetLineSpan();
        return $"{Path.GetFileName(span.Path)}:{span.StartLinePosition.Line + 1}";
    }
}
