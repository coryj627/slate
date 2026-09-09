// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SlateWindows.Commands;
using SlateWindows.Graph;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests.Censuses;

/// <summary>
/// W6-2 PR C (#746), contract C-15 — the censuses, falsifiable, bound
/// semantically over the shell's compilation: the instance census (iii),
/// the load-starting census rebuilt transitive and rooted at the crossings
/// (iv), the preset's argument rule (v), the chord map's wall (vi), the
/// menu census (vii), the depth seam's one installer (viii), the
/// announcement boundary (ix), the no-host-trim census (xiv), the writer
/// census with the write class closed (xv), and the label theory (C-13).
/// </summary>
[Trait("census", "graph-navigator")]
public sealed class GraphNavigatorCensus
{
    private const string TheNavigatorType = "SlateWindows.Graph.GraphNavigator";
    private const string ThePreferencesType = "SlateWindows.Graph.GraphPreferencesViewModel";
    private const string TheDocumentType = "SlateWindows.Graph.GraphDocumentViewModel";
    private const string TheLeafType = "SlateWindows.Graph.ConnectionsLeafViewModel";
    private const string TheWriterType = "SlateWindows.Graph.GraphConfigWriter";
    private const string TheStoreType = "SlateWindows.Graph.GraphConfigStore";

    private static string OwnerOf(SyntaxNode node)
    {
        foreach (SyntaxNode ancestor in node.Ancestors())
        {
            switch (ancestor)
            {
                case MethodDeclarationSyntax method:
                    return method.Identifier.ValueText;
                case ConstructorDeclarationSyntax:
                    return "<ctor>";
                case PropertyDeclarationSyntax property:
                    return property.Identifier.ValueText;
                case FieldDeclarationSyntax field:
                    return field.Declaration.Variables[0].Identifier.ValueText;
            }
        }
        return "<top level>";
    }

    private static string DeclaredTypeOf(SemanticModel model, BaseObjectCreationExpressionSyntax creation) =>
        (model.GetTypeInfo(creation).Type ?? model.GetSymbolInfo(creation).Symbol?.ContainingType)?.ToDisplayString() ?? string.Empty;

    private static bool IsMethodOf(ISymbol? symbol, string type, string name) =>
        symbol is IMethodSymbol method && method.Name == name && method.ContainingType.ToDisplayString() == type;

    private static IEnumerable<ISymbol> Candidates(SymbolInfo info) =>
        info.Symbol is not null ? [info.Symbol] : info.CandidateSymbols;

    /// <summary>Every bound invocation and method-group reference of a method
    /// across the shell, by file and owner.</summary>
    private static List<string> CallersOf(string type, string name, bool includeInsideType = true)
    {
        var callers = new List<string>();
        foreach ((string relative, CSharpSource source) in ShellCompilation.Sources)
        {
            SemanticModel model = ShellCompilation.ModelFor(source);
            foreach (InvocationExpressionSyntax invocation in source.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (Candidates(model.GetSymbolInfo(invocation)).Any(s => IsMethodOf(s, type, name)))
                {
                    if (!includeInsideType && OwnerTypeOf(model, invocation) == type)
                    {
                        continue;
                    }
                    callers.Add($"{relative}:{OwnerOf(invocation)}");
                }
            }
            foreach (ExpressionSyntax reference in source.Root.DescendantNodes().OfType<ExpressionSyntax>().Where(GraphAnnouncerCensus.IsAMethodGroupReference))
            {
                // `x?.Name(...)`: the name inside the member binding is the
                // conditional invocation's callee, not a method group.
                if (reference.Parent is MemberBindingExpressionSyntax)
                {
                    continue;
                }
                if (Candidates(model.GetSymbolInfo(reference)).Any(s => IsMethodOf(s, type, name)))
                {
                    if (!includeInsideType && OwnerTypeOf(model, reference) == type)
                    {
                        continue;
                    }
                    callers.Add($"{relative}:{OwnerOf(reference)} (a method-group reference)");
                }
            }
        }
        return callers.Order(StringComparer.Ordinal).ToList();
    }

    private static string OwnerTypeOf(SemanticModel model, SyntaxNode node)
    {
        TypeDeclarationSyntax? type = node.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault();
        return type is null ? string.Empty : model.GetDeclaredSymbol(type)?.ToDisplayString() ?? string.Empty;
    }

    private static List<string> CreationsOf(string type)
    {
        var creations = new List<string>();
        foreach ((string relative, CSharpSource source) in ShellCompilation.Sources)
        {
            SemanticModel model = ShellCompilation.ModelFor(source);
            foreach (UsingDirectiveSyntax directive in source.Root.DescendantNodes().OfType<UsingDirectiveSyntax>())
            {
                if (directive.Alias is not null && directive.NamespaceOrType.ToString().EndsWith(type.Split('.')[^1], StringComparison.Ordinal))
                {
                    creations.Add($"{relative}:using alias {directive.Alias.Name}");
                }
            }
            foreach (BaseObjectCreationExpressionSyntax creation in source.Root.DescendantNodes().OfType<BaseObjectCreationExpressionSyntax>())
            {
                string declared = DeclaredTypeOf(model, creation);
                if (declared == type || (declared.Length == 0 && creation.ToString().Contains(type.Split('.')[^1], StringComparison.Ordinal)))
                {
                    creations.Add($"{relative}:{OwnerOf(creation)}");
                }
            }
        }
        return creations.Order(StringComparer.Ordinal).ToList();
    }

    private static string ShellRoot => SourceText.ShellSourceRoot();

    // --- (iii) the instance census ---------------------------------------------

    /// <summary>C-1, C-9, C-15 (iii): exactly one navigator and one
    /// preferences object are constructed in the shell — each by its
    /// workspace factory, each factory called once from the workspace's
    /// constructor as the field's direct assignment, outside any repeatable
    /// construct.</summary>
    [Fact]
    public void ExactlyOneNavigatorAndOnePreferencesAreConstructedInTheWorkspaceConstructor()
    {
        Assert.Equal(["Graph/WorkspaceViewModel.Graph.cs:NewGraphNavigator"], CreationsOf(TheNavigatorType));
        Assert.Equal(["Graph/WorkspaceViewModel.Graph.cs:NewGraphPreferences"], CreationsOf(ThePreferencesType));
        foreach ((string factory, string field) in new[] { ("NewGraphNavigator", "_graphNavigator"), ("NewGraphPreferences", "_graphPreferences") })
        {
            var uses = new List<string>();
            foreach ((string relative, CSharpSource source) in ShellCompilation.Sources)
            {
                SemanticModel model = ShellCompilation.ModelFor(source);
                foreach (InvocationExpressionSyntax call in source.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    if (!Candidates(model.GetSymbolInfo(call)).Any(s => IsMethodOf(s, "SlateWindows.WorkspaceViewModel", factory)))
                    {
                        continue;
                    }
                    bool direct = call.Parent is AssignmentExpressionSyntax { Left: IdentifierNameSyntax left } && left.Identifier.ValueText == field;
                    bool repeatable = call.Ancestors()
                        .TakeWhile(ancestor => ancestor is not ConstructorDeclarationSyntax)
                        .Any(ancestor => ancestor is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax
                            or ForStatementSyntax or ForEachStatementSyntax or WhileStatementSyntax or DoStatementSyntax
                            or IfStatementSyntax or ConditionalExpressionSyntax or SwitchStatementSyntax or SwitchExpressionSyntax
                            or QueryExpressionSyntax or TryStatementSyntax);
                    uses.Add($"{relative}:{OwnerOf(call)}{(direct ? string.Empty : " (not the field's direct assignment)")}{(repeatable ? " (inside a repeatable construct)" : string.Empty)}");
                }
                foreach (ExpressionSyntax reference in source.Root.DescendantNodes().OfType<ExpressionSyntax>().Where(GraphAnnouncerCensus.IsAMethodGroupReference))
                {
                    if (Candidates(model.GetSymbolInfo(reference)).Any(s => IsMethodOf(s, "SlateWindows.WorkspaceViewModel", factory)))
                    {
                        uses.Add($"{relative}:{OwnerOf(reference)} (a method-group reference)");
                    }
                }
            }
            Assert.Equal(["WorkspaceViewModel.cs:<ctor>"], uses);
        }
    }

    // --- (iv) the load-starting census, transitive and rooted -----------------

    /// <summary>C-15 (iv), IGN-15: every member of the document from which the
    /// scheduler's StartWorkAlwaysAsync is reachable through the document's
    /// own call graph is in a CLOSED list, and every outside invocation of a
    /// listed entry is in that entry's named set — Load from the follow
    /// method alone; Request from the navigator's needle and preset and the
    /// table view's external sort; Probe from the vault-change notification;
    /// the private arms from nowhere.</summary>
    [Fact]
    public void TheLoadStartingMembersAreTheClosedListAndTheirOutsideCallersTheNamedSets()
    {
        (string Relative, CSharpSource Source) documentFile = ShellCompilation.Sources.Single(s => s.Relative == "Graph/GraphDocumentViewModel.cs");
        SemanticModel model = ShellCompilation.ModelFor(documentFile.Source);
        ClassDeclarationSyntax document = documentFile.Source.Root.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .Single(c => c.Identifier.ValueText == "GraphDocumentViewModel");
        // The intra-type call graph: member → the document's members it invokes.
        var edges = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var starters = new HashSet<string>(StringComparer.Ordinal);
        foreach (MemberDeclarationSyntax member in document.Members)
        {
            string name = member switch
            {
                MethodDeclarationSyntax m => m.Identifier.ValueText,
                ConstructorDeclarationSyntax => "<ctor>",
                PropertyDeclarationSyntax p => p.Identifier.ValueText,
                _ => string.Empty,
            };
            if (name.Length == 0)
            {
                continue;
            }
            var callees = edges.TryGetValue(name, out HashSet<string>? existing) ? existing : (edges[name] = new HashSet<string>(StringComparer.Ordinal));
            foreach (InvocationExpressionSyntax call in member.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                foreach (ISymbol candidate in Candidates(model.GetSymbolInfo(call)))
                {
                    if (candidate is not IMethodSymbol callee)
                    {
                        continue;
                    }
                    if (callee.Name == "StartWorkAlwaysAsync")
                    {
                        _ = starters.Add(name);
                    }
                    else if (callee.ContainingType.ToDisplayString() == TheDocumentType)
                    {
                        _ = callees.Add(callee.Name);
                    }
                }
            }
            foreach (ExpressionSyntax reference in member.DescendantNodes().OfType<ExpressionSyntax>().Where(GraphAnnouncerCensus.IsAMethodGroupReference))
            {
                foreach (ISymbol candidate in Candidates(model.GetSymbolInfo(reference)))
                {
                    if (candidate is IMethodSymbol callee && callee.ContainingType.ToDisplayString() == TheDocumentType)
                    {
                        _ = callees.Add(callee.Name);
                    }
                }
            }
        }
        Assert.Equal(["Issue", "Probe"], starters.Order(StringComparer.Ordinal));
        // Transitive closure: every member reaching a starter.
        var reaching = new HashSet<string>(starters, StringComparer.Ordinal);
        bool grew = true;
        while (grew)
        {
            grew = false;
            foreach ((string member, HashSet<string> callees) in edges)
            {
                if (!reaching.Contains(member) && callees.Any(reaching.Contains))
                {
                    _ = reaching.Add(member);
                    grew = true;
                }
            }
        }
        Assert.Equal(
            ["Issue", "IssueReplacing", "Load", "Probe", "Receive", "ReceiveForTests", "Request"],
            reaching.Order(StringComparer.Ordinal));
        // The outside callers of each entry, bound across the shell.
        Assert.Equal(["Graph/WorkspaceViewModel.Graph.cs:GraphFollowActiveTab"], CallersOf(TheDocumentType, "Load", includeInsideType: false));
        Assert.Equal(
            ["Graph/GraphNavigator.cs:RunPreset", "Graph/GraphNavigator.cs:SetNameQuery", "Graph/GraphTableView.cs:OnExternalSort"],
            CallersOf(TheDocumentType, "Request", includeInsideType: false));
        Assert.Equal(["Graph/WorkspaceViewModel.Graph.cs:NotifyGraphOfVaultChange"], CallersOf(TheDocumentType, "Probe", includeInsideType: false));
        foreach (string arm in new[] { "Issue", "IssueReplacing", "Receive", "ReceiveForTests" })
        {
            Assert.Empty(CallersOf(TheDocumentType, arm, includeInsideType: false));
        }
    }

    /// <summary>C-15 (iv), IGP-23: the census is rooted at the crossings too —
    /// every GraphSnapshot, GraphTableRows and GraphGeneration invocation on
    /// a session anywhere in the shell is inside the document's Fetch or Probe
    /// (the leaf's generation probe is B2's, named) — and no Task.Run,
    /// ThreadPool, Thread, Dispatcher.BeginInvoke or InvokeAsync under Graph/
    /// reaches a load: the named sites are the leaf view's focus retries.</summary>
    [Fact]
    public void TheCrossingsAreRootedInFetchAndProbeAndNoSchedulerUnderGraphReachesALoad()
    {
        var crossings = new List<string>();
        var schedulers = new List<string>();
        foreach ((string relative, CSharpSource source) in ShellCompilation.Sources)
        {
            SemanticModel model = ShellCompilation.ModelFor(source);
            foreach (InvocationExpressionSyntax call in source.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                foreach (ISymbol candidate in Candidates(model.GetSymbolInfo(call)))
                {
                    if (candidate is IMethodSymbol { Name: "GraphSnapshot" or "GraphTableRows" or "GraphGeneration" } method
                        && method.ContainingType.Name == "VaultSession")
                    {
                        crossings.Add($"{relative}:{OwnerOf(call)}:{method.Name}");
                    }
                }
                if (!relative.StartsWith("Graph/", StringComparison.Ordinal))
                {
                    continue;
                }
                string callee = CSharpSource.Normalize(call.Expression);
                if (callee.EndsWith("Task.Run", StringComparison.Ordinal)
                    || callee.Contains("ThreadPool.", StringComparison.Ordinal)
                    || callee.EndsWith("Dispatcher.BeginInvoke", StringComparison.Ordinal)
                    || callee.EndsWith("Dispatcher.InvokeAsync", StringComparison.Ordinal)
                    || callee.EndsWith("BeginInvoke", StringComparison.Ordinal)
                    || callee.EndsWith("InvokeAsync", StringComparison.Ordinal))
                {
                    bool reachesALoad = call.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(inner =>
                        Candidates(model.GetSymbolInfo(inner)).Any(s => s is IMethodSymbol { Name: "Load" or "Request" or "Probe" or "Issue" or "IssueReplacing" } m
                            && m.ContainingType.ToDisplayString() is TheDocumentType or TheLeafType));
                    schedulers.Add($"{relative}:{OwnerOf(call)}{(reachesALoad ? " (reaches a load)" : string.Empty)}");
                }
            }
            foreach (ObjectCreationExpressionSyntax creation in source.Root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
            {
                if (relative.StartsWith("Graph/", StringComparison.Ordinal) && creation.Type.ToString() is "Thread" or "System.Threading.Thread")
                {
                    schedulers.Add($"{relative}:{OwnerOf(creation)} (new Thread)");
                }
            }
        }
        Assert.Equal(
            [
                "Graph/ConnectionsLeafViewModel.cs:Probe:GraphGeneration",
                "Graph/GraphDocumentViewModel.cs:Fetch:GraphSnapshot",
                "Graph/GraphDocumentViewModel.cs:Fetch:GraphTableRows",
                "Graph/GraphDocumentViewModel.cs:Probe:GraphGeneration",
            ],
            crossings.Order(StringComparer.Ordinal));
        // The named sites — the leaf view's focus retries and the relay's
        // marshalling — none reaching a load.
        Assert.Equal(
            ["Graph/ConnectionsLeafView.cs:<ctor>", "Graph/ConnectionsLeafView.cs:RetryFocusInside", "Graph/GraphAnnouncer.cs:Emit"],
            schedulers.Order(StringComparer.Ordinal));
    }

    // --- (v) the preset's argument ------------------------------------------------

    /// <summary>C-15 (v), C-4: the preset's ApplyQuery argument is DIRECTLY the
    /// GraphPresetQuery crossing's invocation; the restore's is the recorded
    /// local; no ApplyQuery under the navigator takes a literal record.</summary>
    [Fact]
    public void ThePresetsApplyQueryArgumentIsTheCrossingsInvocation()
    {
        (string Relative, CSharpSource Source) file = ShellCompilation.Sources.Single(s => s.Relative == "Graph/GraphNavigator.cs");
        SemanticModel model = ShellCompilation.ModelFor(file.Source);
        MethodDeclarationSyntax runPreset = file.Source.Method("RunPreset");
        var arguments = new List<string>();
        foreach (InvocationExpressionSyntax call in runPreset.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (!Candidates(model.GetSymbolInfo(call)).Any(s => IsMethodOf(s, "SlateWindows.Graph.GraphViewState", "ApplyQuery")))
            {
                continue;
            }
            ExpressionSyntax argument = call.ArgumentList.Arguments.Single().Expression;
            bool crossing = argument is InvocationExpressionSyntax crossingCall
                && Candidates(model.GetSymbolInfo(crossingCall)).Any(s => s is IMethodSymbol { Name: "GraphPresetQuery" } m && m.ContainingType.Name == "SlateUniffiMethods");
            arguments.Add(crossing ? "the crossing" : argument is IdentifierNameSyntax id ? "local " + id.Identifier.ValueText : "OTHER: " + argument);
        }
        Assert.Equal(["the crossing", "local recorded"], arguments);
        Assert.DoesNotContain(
            file.Source.Root.DescendantNodes().OfType<InvocationExpressionSyntax>(),
            call => Candidates(model.GetSymbolInfo(call)).Any(s => IsMethodOf(s, "SlateWindows.Graph.GraphViewState", "ApplyQuery"))
                && call.ArgumentList.Arguments.Single().Expression is BaseObjectCreationExpressionSyntax);
    }

    // --- (vi) the chord map's wall -------------------------------------------------

    /// <summary>C-1, C-15 (vi): HandleKey is the canvas navigator's body verbatim —
    /// the null guard, ONE AttachPresenter, and the return of the map's
    /// TryGetValue joined to the handler's call; no branch, no other statement.</summary>
    [Fact]
    public void TheHandleKeyBodyIsTheFourStatements()
    {
        MethodDeclarationSyntax handleKey = CSharpSource.Load("Graph", "GraphNavigator.cs").Method("HandleKey");
        Assert.NotNull(handleKey.Body);
        StatementSyntax[] statements = [.. handleKey.Body!.Statements];
        Assert.Equal(3, statements.Length);
        Assert.True(
            statements[0] is ExpressionStatementSyntax { Expression: InvocationExpressionSyntax guard }
                && CSharpSource.Normalize(guard.Expression) == "ArgumentNullException.ThrowIfNull",
            "the first statement is not the null guard");
        Assert.True(
            statements[1] is ExpressionStatementSyntax { Expression: InvocationExpressionSyntax attach }
                && CSharpSource.Normalize(attach.Expression) == "AttachPresenter",
            "the second statement is not the one AttachPresenter");
        Assert.True(
            statements[2] is ReturnStatementSyntax
            {
                Expression: BinaryExpressionSyntax
                {
                    Left: InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "TryGetValue" } } lookup,
                    Right: InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: "action" } },
                } and { OperatorToken.ValueText: "&&" },
            } && CSharpSource.Normalize(((MemberAccessExpressionSyntax)lookup.Expression).Expression) == "_chords",
            "the third statement is not the map's TryGetValue joined to the handler's call");
        Assert.Empty(handleKey.Body.DescendantNodes().OfType<IfStatementSyntax>());
        Assert.Empty(handleKey.Body.DescendantNodes().OfType<SwitchStatementSyntax>());
        // The canvas navigator's is the same three, so the two cannot drift apart.
        MethodDeclarationSyntax canvas = CSharpSource.Load("Canvas", "CanvasNavigator.cs").Method("HandleKey");
        Assert.Equal(
            canvas.Body!.Statements.Select(s => CSharpSource.Normalize(s)),
            statements.Select(s => CSharpSource.Normalize(s)));
    }

    // --- (vii) the menu census --------------------------------------------------

    /// <summary>C-9, C-12, C-15 (vii): the XAML declares the Verbosity submenu
    /// EMPTY (no CheckMenuItem, no ItemsSource); the populating loop iterates
    /// the preferences' Choices, bound; and ObserveWorkspace wires and
    /// unwires the graph for every observed workspace.</summary>
    [Fact]
    public void TheVerbositySubmenuIsBuiltFromTheVectorAndWiredPerWorkspace()
    {
        XElement submenu = XDocument.Load(Path.Combine(ShellRoot, "MainWindow.xaml")).Descendants()
            .Single(element => element.Attributes().Any(a => a.Name.LocalName.EndsWith("AutomationId", StringComparison.Ordinal) && a.Value == "GraphVerbosityMenu"));
        Assert.Empty(submenu.Elements());
        Assert.Null(submenu.Attribute("ItemsSource"));
        (string Relative, CSharpSource Source) builder = ShellCompilation.Sources.Single(s => s.Relative == "Graph/GraphVerbosityMenu.cs");
        SemanticModel model = ShellCompilation.ModelFor(builder.Source);
        MethodDeclarationSyntax populate = builder.Source.Method("Populate");
        ForEachStatementSyntax loop = Assert.Single(populate.DescendantNodes().OfType<ForEachStatementSyntax>());
        Assert.True(
            model.GetSymbolInfo(loop.Expression).Symbol is IPropertySymbol { Name: "Choices" } choices
                && choices.ContainingType.ToDisplayString() == ThePreferencesType,
            "the populating loop does not iterate the preferences' Choices");
        Assert.Contains(
            populate.DescendantNodes().OfType<BaseObjectCreationExpressionSyntax>(),
            creation => DeclaredTypeOf(model, creation) == "SlateWindows.CheckMenuItem");
        // No level typed: no string literal in the builder equals a core title.
        string[] titles = [.. SlateUniffiMethods.GraphVerbosities().Select(l => l.Title)];
        Assert.DoesNotContain(
            builder.Source.Root.DescendantNodes().OfType<LiteralExpressionSyntax>(),
            literal => literal.IsKind(SyntaxKind.StringLiteralExpression) && titles.Contains(literal.Token.ValueText));
        (string Relative, CSharpSource Source) window = ShellCompilation.Sources.Single(s => s.Relative == "MainWindow.xaml.cs");
        MethodDeclarationSyntax observe = window.Source.Method("ObserveWorkspace");
        string[] calls = [.. observe.DescendantNodes().OfType<InvocationExpressionSyntax>().Select(c => CSharpSource.Normalize(c.Expression))];
        Assert.Contains("WireWorkspaceGraph", calls);
        Assert.Contains("UnwireWorkspaceGraph", calls);
    }

    // --- (viii) the depth seam's one installer ---------------------------------

    /// <summary>C-10, C-15 (viii): the leaf's DepthChanged seam is installed
    /// exactly once in the shell — by NewConnectionsLeaf — and its handler
    /// calls the preferences' depth trigger.</summary>
    [Fact]
    public void TheDepthSeamHasOneInstaller()
    {
        var installers = new List<string>();
        foreach ((string relative, CSharpSource source) in ShellCompilation.Sources)
        {
            SemanticModel model = ShellCompilation.ModelFor(source);
            foreach (AssignmentExpressionSyntax assignment in source.Root.DescendantNodes().OfType<AssignmentExpressionSyntax>())
            {
                if (model.GetSymbolInfo(assignment.Left).Symbol is IPropertySymbol { Name: "DepthChanged" } property
                    && property.ContainingType.ToDisplayString() == TheLeafType)
                {
                    bool trigger = assignment.Right.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>()
                        .Any(call => Candidates(model.GetSymbolInfo(call)).Any(s => IsMethodOf(s, ThePreferencesType, "SetConnectionsDepth")));
                    installers.Add($"{relative}:{OwnerOf(assignment)}{(trigger ? string.Empty : " (not the preferences' trigger)")}");
                }
            }
        }
        Assert.Equal(["WorkspaceViewModel.Connections.cs:NewConnectionsLeaf"], installers);
    }

    // --- (ix) the announcement boundary ------------------------------------------

    /// <summary>C-8, C-15 (ix): the document's boundary gains AnnounceWhereAmI —
    /// the bound callers of AnnounceIfEffective are the receiver's three lines
    /// and the Where-am-I seam, that seam's one outside caller is the
    /// navigator's verb, and the navigator posts nothing itself: no
    /// invocation in GraphNavigator.cs binds to a relay instance method.</summary>
    [Fact]
    public void TheAnnouncementBoundaryGainsWhereAmIAndTheNavigatorPostsNothing()
    {
        Assert.Equal(
            [
                "Graph/GraphDocumentViewModel.cs:AnnounceWhereAmI",
                "Graph/GraphDocumentViewModel.cs:Receive",
                "Graph/GraphDocumentViewModel.cs:Receive",
                "Graph/GraphDocumentViewModel.cs:Receive",
            ],
            CallersOf(TheDocumentType, "AnnounceIfEffective"));
        Assert.Equal(["Graph/GraphNavigator.cs:WhereAmI"], CallersOf(TheDocumentType, "AnnounceWhereAmI", includeInsideType: false));
        (string Relative, CSharpSource Source) navigator = ShellCompilation.Sources.Single(s => s.Relative == "Graph/GraphNavigator.cs");
        SemanticModel model = ShellCompilation.ModelFor(navigator.Source);
        var relayCalls = new List<string>();
        foreach (InvocationExpressionSyntax call in navigator.Source.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            foreach (ISymbol candidate in Candidates(model.GetSymbolInfo(call)))
            {
                if (candidate is IMethodSymbol method && method.ContainingType.ToDisplayString() == "SlateWindows.Graph.GraphAnnouncer")
                {
                    relayCalls.Add($"{OwnerOf(call)}:{method.Name}{(method.IsStatic ? " (static)" : string.Empty)}");
                }
            }
        }
        Assert.Equal(["WhereAmI:RenderLabel (static)"], relayCalls);
    }

    // --- (xiv) the no-host-trim census -------------------------------------------

    /// <summary>C-5, C-15 (xiv): no host trim touches the needle — under Graph/
    /// the only Trim* invocations are the leaf's path normaliser and the
    /// writer's key; the navigator, the surface, the document and the view
    /// state trim nothing.</summary>
    [Fact]
    public void NoHostTrimTouchesTheNeedle()
    {
        var trims = new List<string>();
        foreach ((string relative, CSharpSource source) in ShellCompilation.Sources)
        {
            if (!relative.StartsWith("Graph/", StringComparison.Ordinal))
            {
                continue;
            }
            foreach (InvocationExpressionSyntax call in source.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (call.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Trim" or "TrimStart" or "TrimEnd" })
                {
                    trims.Add($"{relative}:{OwnerOf(call)}");
                }
            }
        }
        Assert.Equal(["Graph/ConnectionsLeafViewModel.cs:Normalize", "Graph/GraphConfigWriter.cs:KeyOf"], trims.Order(StringComparer.Ordinal));
    }

    // --- (xv) the writer census ------------------------------------------------------

    /// <summary>Rule W, C-15 (xv): exactly one GraphConfigWriter in the process
    /// (the static Shared's initializer; the tests' own are outside the
    /// shell); Reserve called from the preferences' schedule alone, Enqueue
    /// from its one transfer (the tick's and the shutdown's) alone, Newest from
    /// the constructor's read alone; the store's Write reached from the
    /// writer's queue body alone; and the WRITE CLASS closed across the shell:
    /// the literal "graph.json" in the store's path builder alone, and every
    /// filesystem mutation API under Graph/ inside the store's Write.</summary>
    [Fact]
    public void TheWriterIsOneStaticAndItsOperationsHaveTheirNamedCallersAndTheWriteClassIsClosed()
    {
        Assert.Equal(["Graph/GraphConfigWriter.cs:Shared"], CreationsOf(TheWriterType));
        Assert.Equal(["Graph/GraphPreferencesViewModel.cs:ScheduleSave"], CallersOf(TheWriterType, "Reserve"));
        Assert.Equal(["Graph/GraphPreferencesViewModel.cs:TransferPending"], CallersOf(TheWriterType, "Enqueue"));
        Assert.Equal(
            ["Graph/GraphPreferencesViewModel.cs:Shutdown", "Graph/GraphPreferencesViewModel.cs:Tick"],
            CallersOf(ThePreferencesType, "TransferPending"));
        Assert.Equal(["Graph/GraphPreferencesViewModel.cs:<ctor>"], CallersOf(TheWriterType, "Newest"));
        Assert.Equal(["Graph/GraphConfigWriter.cs:Write"], CallersOf(TheStoreType, "Write"));
        var literals = new List<string>();
        var mutations = new List<string>();
        foreach ((string relative, CSharpSource source) in ShellCompilation.Sources)
        {
            foreach (LiteralExpressionSyntax literal in source.Root.DescendantNodes().OfType<LiteralExpressionSyntax>())
            {
                if (literal.IsKind(SyntaxKind.StringLiteralExpression) && literal.Token.ValueText == "graph.json")
                {
                    literals.Add($"{relative}:{OwnerOf(literal)}");
                }
            }
            if (!relative.StartsWith("Graph/", StringComparison.Ordinal))
            {
                continue;
            }
            foreach (InvocationExpressionSyntax call in source.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                string callee = CSharpSource.Normalize(call.Expression);
                if (callee is "File.Move" or "File.Replace" or "File.WriteAllText" or "File.WriteAllBytes" or "File.Copy" or "File.Delete" or "File.AppendAllText")
                {
                    mutations.Add($"{relative}:{OwnerOf(call)}:{callee}");
                }
            }
            foreach (ObjectCreationExpressionSyntax creation in source.Root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
            {
                string type = creation.Type.ToString();
                if (type is "FileStream" or "StreamWriter")
                {
                    mutations.Add($"{relative}:{OwnerOf(creation)}:new {type}");
                }
            }
        }
        Assert.Equal(["Graph/GraphConfigStore.cs:FileName"], literals);
        Assert.Equal(
            ["Graph/GraphConfigStore.cs:Write:File.Move", "Graph/GraphConfigStore.cs:Write:File.WriteAllText"],
            mutations.Order(StringComparer.Ordinal));
    }

    // --- C-13: the label theory ----------------------------------------------------

    /// <summary>C-13: every graph label is the inventory's — the mac's byte for
    /// byte, the canvas's reused, the Windows-authored named — read from the
    /// surface's constants, the chord rows and the menu's XAML; and the three
    /// level titles are never typed under Graph/.</summary>
    [Fact]
    public void EveryLabelIsTheInventorys()
    {
        Assert.Equal("Filter graph by note name", GraphPhrase.FilterFieldName);
        Assert.Equal("Filter notes", GraphPhrase.FilterFieldHint);
        Assert.Equal("Graph: Where Am I?", GraphPhrase.WhereAmILabel);
        Assert.Equal("Read the selected node's row copy, its component, the zoom level, and the active filters.", GraphPhrase.WhereAmIHint);
        Assert.Equal(["Where am I?", "Close", "Clear", "Clear filter"], [GraphPhrase.WhereAmIHeading, GraphPhrase.WhereAmICloseLabel, GraphPhrase.ClearLabel, GraphPhrase.ClearFilterName]);
        Assert.Equal("Filter results: ", GraphPhrase.FilterSummaryPrefix);
        Assert.Equal(["Loading graph…", "Loading graph.", "No notes match the current filters.", "Graph error: "], [GraphPhrase.LoadingText, GraphPhrase.LoadingAccessibleName, GraphPhrase.EmptyText, GraphPhrase.ErrorAccessiblePrefix]);
        // The surface's constants ARE the inventory's.
        Assert.Equal(GraphPhrase.FilterFieldName, GraphSurfaceView.FilterFieldName);
        Assert.Equal(GraphPhrase.FilterFieldHint, GraphSurfaceView.FilterFieldHint);
        Assert.Equal(GraphPhrase.FilterSummaryPrefix, GraphSurfaceView.FilterSummaryPrefix);
        Assert.Equal(GraphPhrase.ClearFilterName, GraphSurfaceView.ClearFilterName);
        Assert.Equal(GraphPhrase.WhereAmIHeading, GraphSurfaceView.WhereAmIHeading);
        Assert.Equal(GraphPhrase.WhereAmICloseLabel, GraphSurfaceView.WhereAmICloseLabel);
        Assert.Equal(GraphPhrase.LoadingText, GraphSurfaceView.LoadingText);
        Assert.Equal(GraphPhrase.LoadingAccessibleName, GraphSurfaceView.LoadingAccessibleName);
        Assert.Equal(GraphPhrase.EmptyText, GraphSurfaceView.EmptyText);
        Assert.Equal(GraphPhrase.ErrorAccessiblePrefix, GraphSurfaceView.ErrorAccessiblePrefix);
        // The chord rows' labels and hints.
        foreach ((string id, string label, string hint) in new[]
        {
            (ChordTable.Ids.GraphOrphans, GraphPhrase.OrphansLabel, GraphPhrase.OrphansHint),
            (ChordTable.Ids.GraphUnresolved, GraphPhrase.UnresolvedLabel, GraphPhrase.UnresolvedHint),
            (ChordTable.Ids.GraphMostLinked, GraphPhrase.MostLinkedLabel, GraphPhrase.MostLinkedHint),
            (ChordTable.Ids.GraphWhereAmI, GraphPhrase.WhereAmILabel, GraphPhrase.WhereAmIHint),
        })
        {
            ChordTableEntry row = ChordTable.Entries.Single(r => r.Id == id);
            Assert.Equal(label, row.Label);
            Assert.Equal(hint, row.Hint);
        }
        // The menu's headers, the accelerator underscore aside.
        XDocument xaml = XDocument.Load(Path.Combine(ShellRoot, "MainWindow.xaml"));
        string HeaderOf(string automationId) => xaml.Descendants()
            .Single(e => e.Attributes().Any(a => a.Name.LocalName.EndsWith("AutomationId", StringComparison.Ordinal) && a.Value == automationId))
            .Attribute("Header")!.Value.Replace("_", string.Empty, StringComparison.Ordinal);
        Assert.Equal(GraphPhrase.GraphMenuHeader, HeaderOf("GraphMenu"));
        Assert.Equal(GraphPhrase.VerbosityMenuHeader, HeaderOf("GraphVerbosityMenu"));
        // The level titles come from core's vector, never typed under Graph/.
        string[] titles = [.. SlateUniffiMethods.GraphVerbosities().Select(l => l.Title)];
        foreach ((string relative, CSharpSource source) in ShellCompilation.Sources)
        {
            if (!relative.StartsWith("Graph/", StringComparison.Ordinal))
            {
                continue;
            }
            Assert.DoesNotContain(
                source.Root.DescendantNodes().OfType<LiteralExpressionSyntax>(),
                literal => literal.IsKind(SyntaxKind.StringLiteralExpression) && titles.Contains(literal.Token.ValueText));
        }
    }
    // --- C-16: the parity matrix rows ---------------------------------------------

    /// <summary>C-16: the three preset ids and Where-am-I carry the PR C
    /// status in the generated parity matrix (the generator's own string,
    /// read from its source), and the w_c_matrix carries the row.</summary>
    [Fact]
    public void TheParityMatrixCarriesTheFourRowsAtThePrCStatus()
    {
        string repo = SourceText.RepoRoot();
        string script = File.ReadAllText(Path.Combine(repo, "scripts", "generate-parity-matrix.py"));
        var status = System.Text.RegularExpressions.Regex.Match(
            script, "W6_2_PR_C_STATUS = \\(\\s*\"([^\"]+)\"\\s*\"([^\"]+)\"\\s*\\)");
        Assert.True(status.Success, "the generator's W6_2_PR_C_STATUS is not in its two-string shape");
        string expected = status.Groups[1].Value + status.Groups[2].Value;
        string matrix = File.ReadAllText(Path.Combine(repo, "docs", "plans", "18_windows_port", "parity_matrix.md"));
        foreach (string id in new[] { ChordTable.Ids.GraphOrphans, ChordTable.Ids.GraphUnresolved, ChordTable.Ids.GraphMostLinked, ChordTable.Ids.GraphWhereAmI })
        {
            string? row = matrix.Split('\n').FirstOrDefault(line => line.StartsWith("| `" + id + "`", StringComparison.Ordinal));
            Assert.True(row is not null, $"{id} has no parity row");
            Assert.EndsWith("| " + expected + " |", row.TrimEnd(), StringComparison.Ordinal);
        }
        string wc = File.ReadAllText(Path.Combine(repo, "docs", "plans", "18_windows_port", "w_c_matrix.md"));
        Assert.Contains("| Graph navigator, filter and Where-am-I (W6-2 PR C) |", wc, StringComparison.Ordinal);
    }

}
