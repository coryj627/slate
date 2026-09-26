// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// W7-7 PR 4 (#1247, contract R-5): no focus landing anywhere in the shell
// puts the keys on a bare container.
//
// A bare ListBox has no row for an arrow to move from, so the arrow goes
// to WPF's directional navigation, which searches the whole window: from
// Ctrl+R's "Right pane panels" and Shift+F6's Citations list, Down reached
// a top-level menu (NVDA pass F4). Spec review round 23 widened the rule
// to every ItemsControl container — a Selector, a TreeView, a DataGrid, a
// status bar or a menu — in every source file of the shell, the window's
// partials and the views alike: a tree with no selection keeps the keys
// itself and its Left and Right leave, and from a grid (or a status bar)
// every arrow does (measured: TreeLandingTests, GridLandingTests,
// ShellContainerLandingTests). A row is not a container: a MenuItem or a
// TreeViewItem is the landing, not the thing landed around.
//
// A container landing is a ROW through SelectorFocus — the list's
// FocusFirstOrSelectedItem, the tree's FocusSelectedOrFirstRow — or it is
// a PROVEN stop: a stop of its own with no row to land on, named per site
// below with the hosted fact that shows it keeps all four arrows. A combo
// box is one by type (it takes every arrow as a move between its choices).
//
// BOUND, not read: a landing is a call that binds to a parameterless
// Focus() (on a receiver, or on the object itself), to Keyboard.Focus, or
// to one of the shell's own focus funnels (a method that focuses its
// parameter, such as TryFocus), and the target is judged by its bound
// static type — an x:Name field binds through the XAML-generated partial
// (ShellCompilation), so `TryFocus(SomeList)` is caught as surely as
// `SomeList.Focus()`. A target typed as a base class (a leaf's first stop
// is a UIElement) is out of a static census's reach; SelectorFocus
// .LandOnStop routes those at run time, and the FlaUI journey
// RegionStops_ArrowsStayInRegion witnesses the Citations case.
//
// ANSWERED, not dropped (codex round 3): the helper answers false when
// nothing took the keys NOW — a row it could not realize yet, or rows
// that all refuse — and the populated list is never the landing, so the
// caller must land on its own stable stop. No landing discards the answer,
// nor the answer of a method that hands it back (LandOnStop).

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "selector-landing")]
public sealed class SelectorLandingCensus
{
    private const string Helper = "FocusFirstOrSelectedItem";

    /// <summary>The container landings that stay landings, per site
    /// (<c>Type.Method: the call as written</c>): stops of their own with no
    /// row to land on, each with the hosted fact that shows it keeps all four
    /// arrows — spec review round 23's "a hosted arrow assertion for every
    /// site the census discovers". An entry the shell no longer has fails the
    /// census: its witness would prove nothing.</summary>
    private static readonly (string Site, string Witness)[] ProvenStops =
    [
        ("AccessibleDataGrid.FocusFirstCell: _grid.Focus()", nameof(GridLandingTests.AnEmptyGridKeepsItsArrows)),
        ("MainWindow.TryLand: ShellStatusBar.Focus()", nameof(ShellContainerLandingTests.TheStatusBarKeepsItsArrows)),
        ("MainWindow.LandOnFilesTree: FilesTree.Focus()", nameof(FilesRegionLandingTests.FromTheFilesLandingEveryArrowStaysInTheRegion)),
    ];

    /// <summary>The containers that are their own stop by type, with the
    /// hosted fact that shows it.</summary>
    private static readonly (string Type, string Witness)[] OwnStopTypes =
    [
        ("System.Windows.Controls.ComboBox", nameof(ShellContainerLandingTests.TheCombinatorBoxKeepsItsArrows)),
    ];

    [Fact]
    public void TheHelperIsDeclaredOnceAndTakesASelector()
    {
        var declarations = ShellCompilation.Sources
            .SelectMany(entry => entry.Source.Root.DescendantNodes().OfType<MethodDeclarationSyntax>()
                .Where(method => method.Identifier.ValueText == Helper)
                .Select(method => (entry.Relative, entry.Source, Method: method)))
            .ToArray();
        var declaration = Assert.Single(declarations);
        Assert.Equal("SelectorFocus.cs", declaration.Relative);
        IMethodSymbol symbol = ShellCompilation.ModelFor(declaration.Source).GetDeclaredSymbol(declaration.Method)
            ?? throw new Xunit.Sdk.XunitException($"{Helper} did not bind.");
        Assert.True(symbol.IsStatic, $"{Helper} must be static: any view lands its lists through it.");
        Assert.Equal(
            "System.Windows.Controls.Primitives.Selector",
            symbol.Parameters[0].Type.ToDisplayString());
        // The rest are an empty list's notices, and nothing else.
        Assert.All(
            symbol.Parameters.Skip(1),
            parameter => Assert.True(parameter.IsParams, $"{Helper}'s {parameter.Name} is not the notices"));
        Assert.Equal(SpecialType.System_Boolean, symbol.ReturnType.SpecialType);
    }

    [Fact]
    public void EveryContainerLandingInTheShellIsARowOrAProvenStop()
    {
        CSharpCompilation compilation = BindingCompilation();
        SyntaxTree[] sources = ShellSources(compilation).ToArray();
        Assert.True(sources.Length >= 200, $"only {sources.Length} shell sources were found; the census would read too little.");
        Assert.Contains(sources, tree => tree.FilePath.EndsWith("BaseSurfaceView.cs", StringComparison.OrdinalIgnoreCase));
        var proven = new HashSet<string>(StringComparer.Ordinal);

        string[] offenders = Offenders(compilation, sources, proven).ToArray();

        Assert.True(
            offenders.Length == 0,
            "Focus landings on a bare container (land on a row through SelectorFocus, or prove the stop keeps its arrows):\n  "
            + string.Join("\n  ", offenders));
        Assert.Equal(
            ProvenStops.Select(stop => stop.Site).Order(StringComparer.Ordinal),
            proven.Order(StringComparer.Ordinal));
    }

    /// <summary>The census's own witness: each landing shape it exists to
    /// catch, planted in a partial of the real window and in views of their
    /// own, is caught — a list, a grid, a tree, a menu, a status bar landed
    /// on anywhere but its proven site, and a container focusing itself —
    /// and the combo box, a row, and the helper's own route are not.</summary>
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
                    _ = SelectorFocus.FocusFirstOrSelectedItem(RightPaneLeavesList);
                    FilesTree.Focus();
                    ShellStatusBar.Focus();
                    new System.Windows.Controls.TreeViewItem().Focus();
                    MainMenu.Focus();
                }
            }

            internal sealed class PlantedView : System.Windows.Controls.UserControl
            {
                private readonly System.Windows.Controls.ListBox _list = new();

                private void Land() => _list.Focus();
            }

            internal sealed class PlantedTree : System.Windows.Controls.TreeView
            {
                private void Land() => Focus();
            }
            """;
        CSharpCompilation shell = BindingCompilation();
        var options = (CSharpParseOptions)ShellSources(shell).First().Options;
        SyntaxTree tree = CSharpSyntaxTree.ParseText(planted, options, path: "Planted.cs");
        CSharpCompilation compilation = shell.AddSyntaxTrees(tree);

        string[] offenders = Offenders(compilation, [tree], proven: null).ToArray();

        Assert.Equal(
            [
                "Planted.cs:7: RightPaneLeavesList.Focus() lands on a bare ListBox",
                "Planted.cs:8: TryFocus(PanelCitationsList) lands on a bare ListBox",
                "Planted.cs:9: list?.Focus() lands on a bare ListBox",
                "Planted.cs:10: System.Windows.Input.Keyboard.Focus(QueriesSavedList) lands on a bare ListBox",
                "Planted.cs:12: new System.Windows.Controls.DataGrid().Focus() lands on a bare DataGrid",
                "Planted.cs:14: FilesTree.Focus() lands on a bare TreeView",
                "Planted.cs:15: ShellStatusBar.Focus() lands on a bare StatusBar",
                "Planted.cs:17: MainMenu.Focus() lands on a bare Menu",
                "Planted.cs:25: _list.Focus() lands on a bare ListBox",
                "Planted.cs:30: Focus() lands on a bare PlantedTree",
            ],
            offenders);
    }

    /// <summary>Every landing uses its answer — falls back to its stable
    /// stop when nothing took the keys. The exemptions are structural, not
    /// argued: the rail's row IS the right pane's stable stop, with nothing
    /// behind it; and the region ring's <c>TryLand</c> judges every landing
    /// by where the keys end up (W7-6 #1240) — exempt only in a case that
    /// breaks to TryLand's closing statement, which is checked here to
    /// return that end-state comparison.</summary>
    [Fact]
    public void EveryUnlandedAnswerFallsToAStableStop()
    {
        CSharpCompilation compilation = BindingCompilation();
        SyntaxTree[] sources = ShellSources(compilation).ToArray();

        (string[] discards, int judged) = Discards(compilation, sources);

        Assert.True(judged >= 15, $"only {judged} landings were judged; the census would read too little.");
        Assert.True(
            discards.Length == 0,
            "Landings that discard whether they landed (fall back to the caller's stable stop):\n  "
            + string.Join("\n  ", discards));
    }

    /// <summary>The answer census's own witness: each way to drop the answer
    /// is caught — a discard, a bare call, a wrapper's answer, a void
    /// lambda, a branch of a discarded conditional, a planted wrapper's
    /// answer, and a ring case that returns before the end-state judge —
    /// and the rail's row, a tested answer, a fallback after <c>||</c>, an
    /// answer a lambda returns, and a ring case that breaks to the judge
    /// are not.</summary>
    [Fact]
    public void ThePlantedDiscardsAreCaught()
    {
        const string planted = """
            namespace SlateWindows;

            public partial class MainWindow
            {
                private void PlantedDiscards()
                {
                    _ = SelectorFocus.FocusFirstOrSelectedItem(PanelCitationsList);
                    SelectorFocus.FocusFirstOrSelectedItem(QueriesSavedList);
                    _ = SelectorFocus.LandOnStop(PanelCitationsList);
                    System.Action seat = () => SelectorFocus.FocusFirstOrSelectedItem(TemplatePickerList);
                    _ = FilterResultsList.IsVisible ? SelectorFocus.FocusFirstOrSelectedItem(FilterResultsList) : FilesTree.Focus();
                    _ = PlantedAnswer();
                    _ = SelectorFocus.FocusFirstOrSelectedItem(RightPaneLeavesList);
                    if (!SelectorFocus.FocusFirstOrSelectedItem(CanvasPromptChoicesList))
                    {
                        _ = FilesTree.Focus();
                    }

                    _ = SelectorFocus.FocusFirstOrSelectedItem(FilterResultsList) || FilesTree.Focus();
                    System.Func<bool> answer = () => SelectorFocus.FocusFirstOrSelectedItem(PanelOutlineList);
                }

                private bool PlantedAnswer() => SelectorFocus.FocusFirstOrSelectedItem(PanelBacklinksList);
            }

            internal sealed class PlantedRing : IShellRegionHost
            {
                private readonly System.Windows.Controls.ListBox _rows = new();

                bool IShellRegionHost.TryLand(ShellRegionKind region)
                {
                    switch (region)
                    {
                        case ShellRegionKind.RightPaneContent:
                            _ = SelectorFocus.FocusFirstOrSelectedItem(_rows);
                            break;
                        case ShellRegionKind.Files:
                            _ = SelectorFocus.FocusFirstOrSelectedItem(_rows);
                            return true;
                    }

                    return ((IShellRegionHost)this).FocusedRegion() == region;
                }
            }
            """;
        CSharpCompilation shell = BindingCompilation();
        var options = (CSharpParseOptions)ShellSources(shell).First().Options;
        SyntaxTree tree = CSharpSyntaxTree.ParseText(planted, options, path: "Planted.cs");
        CSharpCompilation compilation = shell.AddSyntaxTrees(tree);

        (string[] discards, int judged) = Discards(compilation, [tree]);

        Assert.Equal(
            [
                "Planted.cs:7: SelectorFocus.FocusFirstOrSelectedItem(PanelCitationsList) discards whether it landed",
                "Planted.cs:8: SelectorFocus.FocusFirstOrSelectedItem(QueriesSavedList) discards whether it landed",
                "Planted.cs:9: SelectorFocus.LandOnStop(PanelCitationsList) discards whether it landed",
                "Planted.cs:10: SelectorFocus.FocusFirstOrSelectedItem(TemplatePickerList) discards whether it landed",
                "Planted.cs:11: SelectorFocus.FocusFirstOrSelectedItem(FilterResultsList) discards whether it landed",
                "Planted.cs:12: PlantedAnswer() discards whether it landed",
                "Planted.cs:38: SelectorFocus.FocusFirstOrSelectedItem(_rows) discards whether it landed",
            ],
            discards);
        Assert.Equal(13, judged);
    }

    /// <summary>The shared shell compilation, with the editor's assembly
    /// referenced whatever this process has loaded so far: the shared one
    /// references the assemblies loaded when it was first built, so whether
    /// AvalonEdit's <c>TextArea</c> bound — <c>SlateTextEditor</c>'s
    /// <c>TextArea.Focus()</c> is a landing like any other — depended on
    /// which test ran first.</summary>
    private static CSharpCompilation BindingCompilation()
    {
        CSharpCompilation compilation = ShellCompilation.Compilation;
        string avalon = typeof(ICSharpCode.AvalonEdit.Editing.TextArea).Assembly.Location;
        return compilation.References.OfType<PortableExecutableReference>()
            .Any(reference => string.Equals(reference.FilePath, avalon, StringComparison.OrdinalIgnoreCase))
            ? compilation
            : compilation.AddReferences(MetadataReference.CreateFromFile(avalon));
    }

    /// <summary>The shell's own source files — every .cs under the source
    /// root, the XAML-generated partials under obj/ excluded: they bind the
    /// x:Name fields, and nobody wrote a landing in them.</summary>
    private static IEnumerable<SyntaxTree> ShellSources(CSharpCompilation compilation)
    {
        string root = Path.GetFullPath(SourceText.ShellSourceRoot()).TrimEnd(Path.DirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        return compilation.SyntaxTrees.Where(tree =>
            tree.FilePath.EndsWith(".cs", StringComparison.Ordinal)
            && Path.GetFullPath(tree.FilePath) is { } full
            && full.StartsWith(root, StringComparison.OrdinalIgnoreCase)
            && !Path.GetRelativePath(root, full).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(segment => segment is "obj" or "bin"));
    }

    private static IEnumerable<string> Offenders(
        CSharpCompilation compilation, IReadOnlyCollection<SyntaxTree> scanned, ISet<string>? proven)
    {
        INamedTypeSymbol itemsControl = compilation.GetTypeByMetadataName("System.Windows.Controls.ItemsControl")
            ?? throw new Xunit.Sdk.XunitException("ItemsControl did not bind: the census compilation lacks WPF.");
        INamedTypeSymbol[] rows =
        [
            compilation.GetTypeByMetadataName("System.Windows.Controls.MenuItem")!,
            compilation.GetTypeByMetadataName("System.Windows.Controls.TreeViewItem")!,
        ];
        INamedTypeSymbol[] ownStops = OwnStopTypes
            .Select(own => compilation.GetTypeByMetadataName(own.Type)
                ?? throw new Xunit.Sdk.XunitException($"{own.Type} did not bind."))
            .ToArray();
        INamedTypeSymbol keyboard = compilation.GetTypeByMetadataName("System.Windows.Input.Keyboard")!;
        Dictionary<IMethodSymbol, int> funnels = Funnels(
            compilation, ShellSources(compilation).Concat(scanned).Distinct().ToArray(), itemsControl);

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
                ITypeSymbol? implicitThis = null;
                if (method is { Name: "Focus", Parameters.Length: 0, IsStatic: false })
                {
                    target = Receiver(call);
                    if (target is null && call.Expression is IdentifierNameSyntax)
                    {
                        // `Focus()` on the object itself: its own type is the target.
                        implicitThis = model.GetEnclosingSymbol(call.SpanStart)?.ContainingType;
                    }
                }
                else if (method.Name == "Focus" && SymbolEqualityComparer.Default.Equals(method.ContainingType, keyboard))
                {
                    target = call.ArgumentList.Arguments[0].Expression;
                }
                else if (funnels.TryGetValue(method.OriginalDefinition, out int ordinal))
                {
                    target = call.ArgumentList.Arguments.ElementAtOrDefault(ordinal)?.Expression;
                }

                if ((target is null && implicitThis is null) || call.Ancestors().OfType<MethodDeclarationSyntax>().Any(
                        declaration => declaration.Identifier.ValueText == Helper))
                {
                    continue;
                }

                ITypeSymbol? type = target is null ? implicitThis : model.GetTypeInfo(target).Type;
                if (type is null || type.TypeKind == TypeKind.Error)
                {
                    yield return $"{where}: the target of {CSharpSource.Normalize(call)} did not bind (is the app built?)";
                }
                else if (IsContainerLanding(type, itemsControl, [.. rows, .. ownStops]))
                {
                    string site = $"{call.Ancestors().OfType<TypeDeclarationSyntax>().First().Identifier.ValueText}."
                        + $"{call.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText}: {Written(call)}";
                    if (ProvenStops.Any(stop => stop.Site == site))
                    {
                        proven?.Add(site);
                        continue;
                    }

                    yield return $"{where}: {Written(call)} lands on a bare {type.Name}";
                }
            }
        }
    }

    /// <summary>Every call to the helper or to a method that hands back its
    /// answer, judged: the discards that are not a stable stop
    /// themselves.</summary>
    private static (string[] Discards, int Judged) Discards(
        CSharpCompilation compilation, IReadOnlyCollection<SyntaxTree> scanned)
    {
        INamedTypeSymbol focus = compilation.GetTypeByMetadataName("SlateWindows.SelectorFocus")
            ?? throw new Xunit.Sdk.XunitException("SelectorFocus did not bind.");
        IMethodSymbol[] landings = [.. new[] { Helper, "FocusSelectedOrFirstRow" }.Select(name =>
            focus.GetMembers(name).OfType<IMethodSymbol>().SingleOrDefault()
                ?? throw new Xunit.Sdk.XunitException($"SelectorFocus.{name} did not bind."))];
        HashSet<IMethodSymbol> answering = AnsweringMethods(
            compilation, ShellSources(compilation).Concat(scanned).Distinct().ToArray(), landings);
        HashSet<string> names = answering.Select(method => method.Name).ToHashSet(StringComparer.Ordinal);

        var discards = new List<string>();
        int judged = 0;
        foreach (SyntaxTree tree in scanned)
        {
            SemanticModel model = compilation.GetSemanticModel(tree);
            foreach (InvocationExpressionSyntax call in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (!names.Contains(InvokedName(call)))
                {
                    continue;
                }

                string where = $"{Path.GetFileName(tree.FilePath)}:{call.GetLocation().GetLineSpan().StartLinePosition.Line + 1}";
                if (model.GetSymbolInfo(call).Symbol is not IMethodSymbol method)
                {
                    discards.Add($"{where}: {CSharpSource.Normalize(call)} did not bind, so its answer cannot be judged");
                    continue;
                }

                if (!answering.Contains(method.OriginalDefinition))
                {
                    continue;
                }

                judged++;
                if (Flow(model, call) == AnswerFlow.Discarded
                    && !LandsOnTheRail(model, call)
                    && !JudgedByTheRing(model, call))
                {
                    discards.Add($"{where}: {CSharpSource.Normalize(call)} discards whether it landed");
                }
            }
        }

        return (discards.ToArray(), judged);
    }

    /// <summary>The landings, and every method that returns one's answer —
    /// or the answer of one that does (LandOnStop).</summary>
    private static HashSet<IMethodSymbol> AnsweringMethods(
        CSharpCompilation compilation, IReadOnlyCollection<SyntaxTree> trees, IEnumerable<IMethodSymbol> landings)
    {
        var answering = new HashSet<IMethodSymbol>(landings, SymbolEqualityComparer.Default);
        bool grew = true;
        while (grew)
        {
            grew = false;
            HashSet<string> names = answering.Select(method => method.Name).ToHashSet(StringComparer.Ordinal);
            foreach (SyntaxTree tree in trees)
            {
                SemanticModel model = compilation.GetSemanticModel(tree);
                foreach (InvocationExpressionSyntax call in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    if (names.Contains(InvokedName(call))
                        && model.GetSymbolInfo(call).Symbol is IMethodSymbol method
                        && answering.Contains(method.OriginalDefinition)
                        && Flow(model, call) == AnswerFlow.Returned
                        && call.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault() is { } declaration
                        && model.GetDeclaredSymbol(declaration) is IMethodSymbol wrapper
                        && answering.Add(wrapper.OriginalDefinition))
                    {
                        grew = true;
                    }
                }
            }
        }

        return answering;
    }

    private enum AnswerFlow
    {
        Discarded,
        Returned,
        Used,
    }

    /// <summary>Where a call's answer goes: through parentheses, a cast, a
    /// branch of a conditional or the right side of <c>||</c>/<c>&amp;&amp;</c>
    /// it is still the answer; a statement, a discard or a void lambda
    /// drops it; a method's return hands it back; anything else — a test,
    /// the left side of <c>||</c> before a fallback — uses it.</summary>
    private static AnswerFlow Flow(SemanticModel model, ExpressionSyntax call)
    {
        SyntaxNode value = call;
        while (value.Parent is { } parent)
        {
            if (parent is ParenthesizedExpressionSyntax or CastExpressionSyntax
                || parent.IsKind(SyntaxKind.SuppressNullableWarningExpression)
                || (parent is ConditionalExpressionSyntax conditional && conditional.Condition != value)
                || (parent is BinaryExpressionSyntax binary && binary.Right == value
                    && binary.Kind() is SyntaxKind.LogicalOrExpression or SyntaxKind.LogicalAndExpression))
            {
                value = parent;
                continue;
            }

            // A switch expression's arm hands its value to the switch.
            if (parent is SwitchExpressionArmSyntax arm && arm.Expression == value && arm.Parent is { } switchExpression)
            {
                value = switchExpression;
                continue;
            }

            return parent switch
            {
                ExpressionStatementSyntax => AnswerFlow.Discarded,
                AssignmentExpressionSyntax assignment when assignment.Right == value =>
                    (model.GetSymbolInfo(assignment.Left).Symbol is IDiscardSymbol) ? AnswerFlow.Discarded : AnswerFlow.Used,
                LambdaExpressionSyntax lambda when lambda.Body == value =>
                    (model.GetSymbolInfo(lambda).Symbol is IMethodSymbol { ReturnsVoid: true }) ? AnswerFlow.Discarded : AnswerFlow.Used,
                ArrowExpressionClauseSyntax { Parent: MethodDeclarationSyntax declaration } =>
                    (model.GetDeclaredSymbol(declaration) is IMethodSymbol { ReturnsVoid: true }) ? AnswerFlow.Discarded : AnswerFlow.Returned,
                ReturnStatementSyntax statement =>
                    (statement.Ancestors().FirstOrDefault(node => node is AnonymousFunctionExpressionSyntax
                        or LocalFunctionStatementSyntax or BaseMethodDeclarationSyntax) is MethodDeclarationSyntax)
                        ? AnswerFlow.Returned
                        : AnswerFlow.Used,
                _ => AnswerFlow.Used,
            };
        }

        return AnswerFlow.Used;
    }

    /// <summary>The rail's row is the right pane's stable stop: a landing
    /// there has nothing behind it to fall back to.</summary>
    private static bool LandsOnTheRail(SemanticModel model, InvocationExpressionSyntax call) =>
        call.ArgumentList.Arguments.FirstOrDefault()?.Expression is { } argument
        && model.GetSymbolInfo(argument).Symbol is IFieldSymbol { Name: "RightPaneLeavesList", ContainingType.Name: "MainWindow" };

    /// <summary>A discard inside the region ring's <c>TryLand</c>, in a case
    /// that breaks to its closing statement — checked to return the
    /// end-state comparison, <c>FocusedRegion() == region</c>, so a landing
    /// that took nothing reads as the region's refusal and the ring moves
    /// on.</summary>
    private static bool JudgedByTheRing(SemanticModel model, InvocationExpressionSyntax call)
    {
        if (call.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault() is not { } declaration
            || model.GetDeclaredSymbol(declaration) is not IMethodSymbol method
            || !method.ExplicitInterfaceImplementations.Any(
                implemented => implemented is { Name: "TryLand", ContainingType.Name: "IShellRegionHost" })
            || call.Ancestors().OfType<SwitchSectionSyntax>().FirstOrDefault() is not { } section
            || !EndsInBreak(section.Statements.LastOrDefault())
            || declaration.Body?.Statements.LastOrDefault() is not ReturnStatementSyntax { Expression: { } judge })
        {
            return false;
        }

        return judge.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>().Any(invocation =>
            model.GetSymbolInfo(invocation).Symbol is IMethodSymbol { Name: "FocusedRegion" } focused
            && focused.ContainingType.Name == "IShellRegionHost");
    }

    private static bool EndsInBreak(StatementSyntax? statement) => statement switch
    {
        BreakStatementSyntax => true,
        BlockSyntax block => EndsInBreak(block.Statements.LastOrDefault()),
        _ => false,
    };

    /// <summary>The window's own methods that focus one of their parameters —
    /// directly, or through a pattern over it (<c>TryFocus</c>'s switch) —
    /// where that parameter could hold a list. The helper is the one
    /// sanctioned funnel and is not listed.</summary>
    private static Dictionary<IMethodSymbol, int> Funnels(
        CSharpCompilation compilation, IReadOnlyCollection<SyntaxTree> trees, INamedTypeSymbol itemsControl)
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
                    if (!compilation.ClassifyConversion(itemsControl, parameter.Type).IsImplicit)
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

    /// <summary>Whether <paramref name="type"/> is an ItemsControl container
    /// — not a row, and not a container that is its own stop.</summary>
    private static bool IsContainerLanding(ITypeSymbol type, INamedTypeSymbol itemsControl, INamedTypeSymbol[] exempt)
    {
        bool isContainer = false;
        for (ITypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            if (exempt.Any(row => SymbolEqualityComparer.Default.Equals(current, row)))
            {
                return false;
            }

            isContainer |= SymbolEqualityComparer.Default.Equals(current, itemsControl);
        }

        return isContainer;
    }
}
