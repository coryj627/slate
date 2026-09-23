// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// W7-7 PR 4 (#1247, contract R-5): no focus landing in the window's code
// puts the keys on a bare list.
//
// A bare ListBox has no row for an arrow to move from, so the arrow goes
// to WPF's directional navigation, which searches the whole window: from
// Ctrl+R's "Right pane panels" and Shift+F6's Citations list, Down reached
// a top-level menu (NVDA pass F4). MainWindow.FocusFirstOrSelectedItem
// lands on the selected row, else the first; this census holds every other
// landing in MainWindow*.cs to it.
//
// BOUND, not read: a landing is a call that binds to a parameterless
// Focus(), to Keyboard.Focus, or to one of the window's own focus funnels
// (a method that focuses its parameter, such as TryFocus), and the target
// is judged by its bound static type — an x:Name field binds through the
// XAML-generated partial (ShellCompilation), so `TryFocus(SomeList)` is
// caught as surely as `SomeList.Focus()`. A combo box is not a list
// landing (its items live in its drop-down, and the box is the stop), nor
// is a grid (its stop is a cell, which AccessibleDataGrid seats) — the
// window's own IsListLanding rule. A target typed as a base class (a
// leaf's first stop is a UIElement) is out of a static census's reach;
// LandOnStop routes those, and the FlaUI journey
// RegionStops_ArrowsStayInRegion witnesses the Citations case.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "selector-landing")]
public sealed class SelectorLandingCensus
{
    private const string Helper = "FocusFirstOrSelectedItem";

    [Fact]
    public void TheHelperIsDeclaredOnceAndTakesASelector()
    {
        var declarations = ShellCompilation.Sources
            .SelectMany(entry => entry.Source.Root.DescendantNodes().OfType<MethodDeclarationSyntax>()
                .Where(method => method.Identifier.ValueText == Helper)
                .Select(method => (entry.Relative, entry.Source, Method: method)))
            .ToArray();
        var declaration = Assert.Single(declarations);
        Assert.Equal("MainWindow.ShellRegions.cs", declaration.Relative);
        IMethodSymbol symbol = ShellCompilation.ModelFor(declaration.Source).GetDeclaredSymbol(declaration.Method)
            ?? throw new Xunit.Sdk.XunitException($"{Helper} did not bind.");
        Assert.True(symbol.IsStatic, $"{Helper} must be static: it lands any selector, not the window's own.");
        Assert.Equal(
            "System.Windows.Controls.Primitives.Selector",
            Assert.Single(symbol.Parameters).Type.ToDisplayString());
        Assert.Equal(SpecialType.System_Boolean, symbol.ReturnType.SpecialType);
    }

    [Fact]
    public void EveryListLandingInTheWindowGoesThroughTheHelper()
    {
        SyntaxTree[] partials = WindowPartials(ShellCompilation.Compilation).ToArray();
        Assert.True(partials.Length >= 10, $"only {partials.Length} MainWindow partials were found; the census would read too little.");
        string[] offenders = Offenders(ShellCompilation.Compilation, partials).ToArray();
        Assert.True(
            offenders.Length == 0,
            "Focus landings on a bare list (route them through " + Helper + "):\n  "
            + string.Join("\n  ", offenders));
    }

    /// <summary>The census's own witness: each landing shape it exists to
    /// catch, planted in a partial of the real window, is caught — and the
    /// combo box, the grid and the helper's own route are not.</summary>
    [Fact]
    public void ThePlantedBareLandingsAreCaught()
    {
        const string planted = """
            namespace SlateWindows;

            public partial class MainWindow
            {
                private void PlantedLandings(System.Windows.Controls.ListBox list)
                {
                    RightPaneLeavesList.Focus();
                    _ = TryFocus(PanelCitationsList);
                    list?.Focus();
                    System.Windows.Input.Keyboard.Focus(QueriesSavedList);
                    BuilderCombinatorBox.Focus();
                    new System.Windows.Controls.DataGrid().Focus();
                    _ = FocusFirstOrSelectedItem(RightPaneLeavesList);
                }
            }
            """;
        var options = (CSharpParseOptions)WindowPartials(ShellCompilation.Compilation).First().Options;
        SyntaxTree tree = CSharpSyntaxTree.ParseText(planted, options, path: "Planted.cs");
        CSharpCompilation compilation = ShellCompilation.Compilation.AddSyntaxTrees(tree);

        string[] offenders = Offenders(compilation, [tree]).ToArray();

        Assert.Equal(
            [
                "Planted.cs:7: RightPaneLeavesList.Focus() lands on a bare ListBox",
                "Planted.cs:8: TryFocus(PanelCitationsList) lands on a bare ListBox",
                "Planted.cs:9: list?.Focus() lands on a bare ListBox",
                "Planted.cs:10: System.Windows.Input.Keyboard.Focus(QueriesSavedList) lands on a bare ListBox",
            ],
            offenders);
    }

    private static IEnumerable<SyntaxTree> WindowPartials(CSharpCompilation compilation)
    {
        string root = Path.GetFullPath(SourceText.ShellSourceRoot()).TrimEnd(Path.DirectorySeparatorChar);
        return compilation.SyntaxTrees.Where(tree =>
            Path.GetFileName(tree.FilePath) is { } name
            && name.StartsWith("MainWindow", StringComparison.Ordinal)
            && name.EndsWith(".cs", StringComparison.Ordinal)
            && string.Equals(
                Path.GetDirectoryName(Path.GetFullPath(tree.FilePath)),
                root,
                StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> Offenders(CSharpCompilation compilation, IReadOnlyCollection<SyntaxTree> scanned)
    {
        INamedTypeSymbol selector = compilation.GetTypeByMetadataName("System.Windows.Controls.Primitives.Selector")
            ?? throw new Xunit.Sdk.XunitException("Selector did not bind: the census compilation lacks WPF.");
        INamedTypeSymbol[] ownStops =
        [
            compilation.GetTypeByMetadataName("System.Windows.Controls.ComboBox")!,
            compilation.GetTypeByMetadataName("System.Windows.Controls.DataGrid")!,
        ];
        INamedTypeSymbol keyboard = compilation.GetTypeByMetadataName("System.Windows.Input.Keyboard")!;
        Dictionary<IMethodSymbol, int> funnels = Funnels(
            compilation, WindowPartials(compilation).Concat(scanned).Distinct().ToArray(), selector);

        foreach (SyntaxTree tree in scanned)
        {
            SemanticModel model = compilation.GetSemanticModel(tree);
            foreach (InvocationExpressionSyntax call in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                string name = InvokedName(call);
                string where = $"{Path.GetFileName(tree.FilePath)}:{call.GetLocation().GetLineSpan().StartLinePosition.Line + 1}";
                if (model.GetSymbolInfo(call).Symbol is not IMethodSymbol method)
                {
                    if (name == "Focus" || funnels.Keys.Any(funnel => funnel.Name == name))
                    {
                        yield return $"{where}: {CSharpSource.Normalize(call)} did not bind, so its target cannot be judged";
                    }

                    continue;
                }

                ExpressionSyntax? target = null;
                if (method is { Name: "Focus", Parameters.Length: 0, IsStatic: false })
                {
                    target = Receiver(call);
                }
                else if (method.Name == "Focus" && SymbolEqualityComparer.Default.Equals(method.ContainingType, keyboard))
                {
                    target = call.ArgumentList.Arguments[0].Expression;
                }
                else if (funnels.TryGetValue(method.OriginalDefinition, out int ordinal))
                {
                    target = call.ArgumentList.Arguments.ElementAtOrDefault(ordinal)?.Expression;
                }

                if (target is null || call.Ancestors().OfType<MethodDeclarationSyntax>().Any(
                        declaration => declaration.Identifier.ValueText == Helper))
                {
                    continue;
                }

                ITypeSymbol? type = model.GetTypeInfo(target).Type;
                if (type is null || type.TypeKind == TypeKind.Error)
                {
                    yield return $"{where}: the target of {CSharpSource.Normalize(call)} did not bind (is the app built?)";
                }
                else if (IsListLanding(type, selector, ownStops))
                {
                    yield return $"{where}: {Written(call)} lands on a bare {type.Name}";
                }
            }
        }
    }

    /// <summary>The window's own methods that focus one of their parameters —
    /// directly, or through a pattern over it (<c>TryFocus</c>'s switch) —
    /// where that parameter could hold a list. The helper is the one
    /// sanctioned funnel and is not listed.</summary>
    private static Dictionary<IMethodSymbol, int> Funnels(
        CSharpCompilation compilation, IReadOnlyCollection<SyntaxTree> trees, INamedTypeSymbol selector)
    {
        var funnels = new Dictionary<IMethodSymbol, int>(SymbolEqualityComparer.Default);
        foreach (SyntaxTree tree in trees)
        {
            SemanticModel model = compilation.GetSemanticModel(tree);
            foreach (MethodDeclarationSyntax declaration in tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                if (declaration.Identifier.ValueText == Helper
                    || model.GetDeclaredSymbol(declaration) is not IMethodSymbol method)
                {
                    continue;
                }

                foreach (IParameterSymbol parameter in method.Parameters)
                {
                    if (!compilation.ClassifyConversion(selector, parameter.Type).IsImplicit)
                    {
                        continue;
                    }

                    bool focusesIt = declaration.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(call =>
                        InvokedName(call) == "Focus"
                        && Receiver(call) is { } receiver
                        && ReachesParameter(model, receiver, parameter));
                    if (focusesIt)
                    {
                        funnels[method] = parameter.Ordinal;
                    }
                }
            }
        }

        return funnels;
    }

    private static bool ReachesParameter(SemanticModel model, ExpressionSyntax receiver, IParameterSymbol parameter)
    {
        ISymbol? symbol = model.GetSymbolInfo(receiver).Symbol;
        if (SymbolEqualityComparer.Default.Equals(symbol, parameter))
        {
            return true;
        }

        // A pattern variable over the parameter: `target switch { UIElement e => e.Focus() }`
        // or `target is UIElement e && e.Focus()`.
        if (symbol is not ILocalSymbol local
            || local.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is not SingleVariableDesignationSyntax designation)
        {
            return false;
        }

        ExpressionSyntax? input = designation.Ancestors().Select(node => node switch
        {
            IsPatternExpressionSyntax pattern => pattern.Expression,
            SwitchExpressionArmSyntax arm => ((SwitchExpressionSyntax)arm.Parent!).GoverningExpression,
            SwitchSectionSyntax section => ((SwitchStatementSyntax)section.Parent!).Expression,
            _ => null,
        }).FirstOrDefault(expression => expression is not null);
        return input is not null
            && SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(input).Symbol, parameter);
    }

    /// <summary>The object a <c>Focus()</c> call focuses: <c>x</c> in
    /// <c>x.Focus()</c> and in <c>x?.Focus()</c>; null for the window's own
    /// implicit-this call.</summary>
    private static ExpressionSyntax? Receiver(InvocationExpressionSyntax call) => call.Expression switch
    {
        MemberAccessExpressionSyntax access => access.Expression,
        MemberBindingExpressionSyntax => call.Ancestors().OfType<ConditionalAccessExpressionSyntax>()
            .FirstOrDefault(conditional => conditional.WhenNotNull.Span.Contains(call.Span))?.Expression,
        _ => null,
    };

    /// <summary>The call as written, receiver included — <c>list?.Focus()</c>
    /// is a conditional access whose invocation part reads only
    /// <c>.Focus()</c>.</summary>
    private static SyntaxNode Written(InvocationExpressionSyntax call) =>
        call.Expression is MemberBindingExpressionSyntax
            && call.Ancestors().OfType<ConditionalAccessExpressionSyntax>().FirstOrDefault() is { } conditional
                ? conditional
                : call;

    private static string InvokedName(InvocationExpressionSyntax call) => call.Expression switch
    {
        MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
        MemberBindingExpressionSyntax binding => binding.Name.Identifier.ValueText,
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        GenericNameSyntax generic => generic.Identifier.ValueText,
        _ => string.Empty,
    };

    private static bool IsListLanding(ITypeSymbol type, INamedTypeSymbol selector, INamedTypeSymbol[] ownStops)
    {
        bool isSelector = false;
        for (ITypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            if (ownStops.Any(own => SymbolEqualityComparer.Default.Equals(current, own)))
            {
                return false;
            }

            isSelector |= SymbolEqualityComparer.Default.Equals(current, selector);
        }

        return isSelector;
    }
}
