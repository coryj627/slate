// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// W7-7 PR 4 (#1247, contract R-5; codex PR 4's final check): a POINTER
// activation acts on the row the pointer HIT, never on the keyboard's row.
// With the click rule (OD-11c) the first press of a double-click on a list's
// empty area LANDS the keys on a row; a double-click handler that resolved
// the focused row, or read the selection, then opened a row never clicked —
// Citations and the Bases list did, and the Queries lists and Quick Open ran
// their selection from empty space. The census finds every MouseDoubleClick
// handler — named in the shell's XAML or subscribed in its code — and holds
// its body to a hit test of the pointer's own source: SelectorFocus.ClickedItem,
// PanelRowTargeting.TargetRowAt, ItemsControl.ContainerFromElement, the
// palette's ItemContainerOf, or the grid's TargetRowActionsAt.

using System.Xml.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "double-click-target")]
public sealed class DoubleClickTargetCensus
{
    private static readonly string[] HitTests =
    [
        "SelectorFocus.ClickedItem(",
        "PanelRowTargeting.TargetRowAt(",
        "ContainerFromElement(",
        "ItemContainerOf(",
        "TargetRowActionsAt(",
    ];

    [Fact]
    public void EveryDoubleClickHandlerHitTestsThePointersRow()
    {
        var handlers = new SortedSet<string>(StringComparer.Ordinal);
        foreach (string path in Directory.EnumerateFiles(SourceText.ShellSourceRoot(), "*.xaml", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)))
        {
            foreach (XAttribute attribute in XDocument.Load(path).Descendants().Attributes().Where(attribute => attribute.Name.LocalName == "MouseDoubleClick"))
            {
                handlers.Add(attribute.Value);
            }
        }

        foreach ((string _, CSharpSource source) in ShellCompilation.Sources)
        {
            foreach (AssignmentExpressionSyntax subscription in source.Root.DescendantNodes().OfType<AssignmentExpressionSyntax>())
            {
                if (subscription.OperatorToken.ValueText == "+="
                    && subscription.Left is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "MouseDoubleClick" }
                    && subscription.Right is IdentifierNameSyntax handler)
                {
                    handlers.Add(handler.Identifier.ValueText);
                }
            }
        }

        Assert.True(handlers.Count >= 15, $"only {handlers.Count} double-click handlers were found; the scrape is broken.");
        var offenders = new List<string>();
        foreach (string name in handlers)
        {
            MethodDeclarationSyntax[] bodies =
            [
                .. ShellCompilation.Sources
                    .SelectMany(source => source.Source.Root.DescendantNodes().OfType<MethodDeclarationSyntax>())
                    .Where(method => method.Identifier.ValueText == name),
            ];
            if (bodies.Length == 0)
            {
                offenders.Add($"{name}: no such method in the shell");
                continue;
            }

            if (!bodies.All(body => Reaches(body, depth: 0)))
            {
                offenders.Add($"{name}: activates without hit-testing the pointer's own row");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "Double-click handlers that can activate a row never clicked (use SelectorFocus.ClickedItem on e.OriginalSource):\n  "
            + string.Join("\n  ", offenders));
    }

    /// <summary>Whether <paramref name="method"/>'s body, or a shell method
    /// it calls by name (two deep), hit-tests the pointer's source.</summary>
    private static bool Reaches(MethodDeclarationSyntax method, int depth)
    {
        string text = method.ToString();
        if (HitTests.Any(test => text.Contains(test, StringComparison.Ordinal)))
        {
            return true;
        }

        if (depth >= 2)
        {
            return false;
        }

        foreach (InvocationExpressionSyntax call in method.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            string? callee = call.Expression switch
            {
                IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
                _ => null,
            };
            if (callee is null)
            {
                continue;
            }

            foreach (MethodDeclarationSyntax target in ShellCompilation.Sources
                .SelectMany(source => source.Source.Root.DescendantNodes().OfType<MethodDeclarationSyntax>())
                .Where(candidate => candidate.Identifier.ValueText == callee && !ReferenceEquals(candidate, method)))
            {
                if (Reaches(target, depth + 1))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
