// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// W6-2 PR A (#746): the graph's directory censuses — the canvas funnel
// guard's twin keyed on `Graph/` by NORMALISED PATH (contract A-10; the
// round-3 ledger's IGA-32), plus the structural facts contracts A-1, A-5
// and A-6 pin: one caller of the document's load, one cell lookup, no
// typed column header, no mutable shadow of the view state.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "graph-funnel")]
public sealed class GraphAnnouncerCensus
{
    private static string GraphSourceRoot() =>
        Path.Combine(SourceText.ShellSourceRoot(), "Graph");

    /// <summary>The one file allowed to reach the dispatcher and the
    /// renderer, by its path relative to the graph root — a nested file of
    /// the same name is NOT the relay.</summary>
    private const string TheRelay = "GraphAnnouncer.cs";

    private static IEnumerable<string> GraphSources()
    {
        string root = GraphSourceRoot();
        Assert.True(Directory.Exists(root), $"the graph source root is missing: {root}");
        return Directory
            .EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .OrderBy(file => file, StringComparer.Ordinal);
    }

    private static string Relative(string file) =>
        Path.GetRelativePath(GraphSourceRoot(), file).Replace('\\', '/');

    /// <summary>The terminal identifier of a DELEGATE callee (IPC-3): a
    /// bare name, a this- or base-qualified name (<c>this._announce</c>),
    /// or either under parentheses; null for anything else — a method on
    /// another object (<c>_announcer.Announce(…)</c>, the relay seam the
    /// seam census governs) is not a delegate call.</summary>
    internal static string? TerminalIdentifier(ExpressionSyntax expression) =>
        TerminalIdentifierNode(expression)?.Identifier.ValueText;

    /// <summary>The identifier NODE a delegate callee resolves to (IPD-5):
    /// a bare name; a this- or base-qualified name, the qualifier itself
    /// possibly parenthesised; a delegate's <c>.Invoke</c> over either;
    /// the whole under parentheses. Null for a method on another object.</summary>
    internal static IdentifierNameSyntax? TerminalIdentifierNode(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }
        switch (expression)
        {
            case IdentifierNameSyntax identifier:
                return identifier;
            case MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Invoke" } invoke:
                return TerminalIdentifierNode(invoke.Expression);
            case MemberAccessExpressionSyntax access:
                ExpressionSyntax receiver = access.Expression;
                while (receiver is ParenthesizedExpressionSyntax inner)
                {
                    receiver = inner.Expression;
                }
                return receiver is ThisExpressionSyntax or BaseExpressionSyntax ? access.Name as IdentifierNameSyntax : null;
            case MemberBindingExpressionSyntax { Name.Identifier.ValueText: "Invoke" }:
                // `x?.Invoke(…)`: the receiver is the conditional access's
                // expression, resolved by the caller.
                return null;
            default:
                return null;
        }
    }

    /// <summary>Every invocation in a tree, with `x?.Invoke(…)` folded to
    /// its receiver (IPD-5), as (callee identifier node, argument list).</summary>
    internal static IEnumerable<(IdentifierNameSyntax Callee, ArgumentListSyntax Arguments)> DelegateCalls(SyntaxNode root)
    {
        foreach (InvocationExpressionSyntax call in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (call.Expression is MemberBindingExpressionSyntax { Name.Identifier.ValueText: "Invoke" }
                && call.Parent is ConditionalAccessExpressionSyntax conditional)
            {
                if (TerminalIdentifierNode(conditional.Expression) is { } receiver)
                {
                    yield return (receiver, call.ArgumentList);
                }
                continue;
            }
            if (TerminalIdentifierNode(call.Expression) is { } callee)
            {
                yield return (callee, call.ArgumentList);
            }
        }
    }

    /// <summary>Contract A-10 / R-C: no graph source announces on its own;
    /// exactly one relay exists, at the root of the directory.</summary>
    [Fact]
    public void NoGraphSourceAnnouncesOutsideTheRelay()
    {
        string[] relays = [.. GraphSources().Where(file => Relative(file) == TheRelay)];
        Assert.Single(relays);
        var offenders = new List<string>();
        int relaySeeds = 0;
        foreach (string file in GraphSources())
        {
            string label = Relative(file);
            if (label == TheRelay)
            {
                continue;
            }
            CSharpSource source = CSharpSource.LoadPath(file);
            foreach (string name in source.Root.DescendantNodes()
                .OfType<IdentifierNameSyntax>()
                .Select(identifier => identifier.Identifier.ValueText))
            {
                if (name is "AccessibilityNotificationDispatcher")
                {
                    offenders.Add($"{label}: {name}");
                }
            }
            foreach (MemberAccessExpressionSyntax access in source.Root
                .DescendantNodes()
                .OfType<MemberAccessExpressionSyntax>())
            {
                string member = access.Name.Identifier.ValueText;
                if (member is "RaiseNotificationEvent" or "A11yRender")
                {
                    offenders.Add($"{label}: {member}");
                }
            }
            foreach (ObjectCreationExpressionSyntax creation in source.Root
                .DescendantNodes()
                .OfType<ObjectCreationExpressionSyntax>())
            {
                if (creation.Type.ToString().EndsWith("HostComposed", StringComparison.Ordinal))
                {
                    offenders.Add($"{label}: A11yEvent.HostComposed");
                }
                // IPA-7: a graph-family event CONSTRUCTED outside the relay is
                // a posting site whatever it is handed to — the workspace's
                // own `_announce` delegate included, which no member access
                // above can see.
                if (creation.Type.ToString().EndsWith("A11yEvent.Graph", StringComparison.Ordinal))
                {
                    offenders.Add($"{label}: new A11yEvent.Graph(…)");
                }
            }
            // IPB-5: the textual rule above can be dodged by a type alias, a
            // target-typed `new`, or an event built elsewhere and handed to
            // the workspace's delegate — so under Graph/ no `using` alias
            // exists at all, and every call on an announce delegate passes
            // exactly one EXPLICIT construction of a non-graph shell event.
            foreach (UsingDirectiveSyntax directive in source.Root.DescendantNodes().OfType<UsingDirectiveSyntax>())
            {
                // IPC-3: `using static …A11yEvent;` would let a bare `new
                // Graph(…)` dodge the suffix rule — no static import either.
                if (directive.Alias is not null || directive.StaticKeyword.RawKind != 0)
                {
                    offenders.Add($"{label}: using directive {directive.NamespaceOrType}");
                }
            }
            // Every call on an announce delegate — the callee normalised
            // (IPC-3, IPD-5): `_announce(…)`, `this._announce(…)`,
            // `(this)._announce(…)`, `_announce.Invoke(…)`, `_announce?.Invoke(…)`
            // — passes exactly one EXPLICIT construction of a non-graph shell
            // event; and every OTHER reference to such a delegate (captured,
            // passed along, assigned) is an offender, so no event built
            // elsewhere can reach it.
            var validatedCallees = new HashSet<IdentifierNameSyntax>();
            foreach ((IdentifierNameSyntax callee, ArgumentListSyntax arguments) in DelegateCalls(source.Root))
            {
                string calleeName = callee.Identifier.ValueText;
                if (!calleeName.EndsWith("announce", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                bool explicitShellEvent = arguments.Arguments.Count == 1
                    && arguments.Arguments[0].Expression is ObjectCreationExpressionSyntax created
                    && created.Type.ToString().StartsWith("A11yEvent.", StringComparison.Ordinal)
                    && !created.Type.ToString().EndsWith(".Graph", StringComparison.Ordinal);
                if (!explicitShellEvent)
                {
                    offenders.Add($"{label}: {calleeName}({arguments.Arguments})");
                }
                _ = validatedCallees.Add(callee);
            }
            foreach (IdentifierNameSyntax reference in source.Root.DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                // The workspace's delegate by its field name — `_announce`
                // (its constructor parameter `announce` is declared outside
                // Graph/; under Graph/ that bare name is the load policy's
                // parameter) — not the relay's `Announce` method, which the
                // seam census governs.
                if (reference.Identifier.ValueText != "_announce"
                    || validatedCallees.Contains(reference))
                {
                    continue;
                }
                if (reference.Parent is MemberAccessExpressionSyntax qualified && qualified.Name != reference)
                {
                    // `_announce.Something` — the receiver of a member access
                    // that DelegateCalls did not validate as `.Invoke(…)`.
                    offenders.Add($"{label}: {qualified}");
                    continue;
                }
                offenders.Add($"{label}: reference {reference.Identifier.ValueText} outside a validated call");
            }
            // IPE-5, IPF-6: the workspace's RENDERED-announcement delegate is
            // the relay's one input — under Graph/ it is referenced exactly
            // once, as the argument of the root `new GraphAnnouncer(…)` in
            // the workspace's own `NewGraphDocument`; any other reference (a
            // direct call, a capture, a hand-off) is an offender, or the
            // one-relay wall has a second door. The name is the WORKSPACE
            // FIELD's and nothing else's: no local, parameter or member of
            // that name is declared under Graph/, so the identifier cannot
            // be a same-named stand-in that leaves the real field unused.
            foreach (SyntaxNode declaration in source.Root.DescendantNodes())
            {
                string? declared = declaration switch
                {
                    VariableDeclaratorSyntax variable => variable.Identifier.ValueText,
                    ParameterSyntax parameter => parameter.Identifier.ValueText,
                    PropertyDeclarationSyntax property => property.Identifier.ValueText,
                    SingleVariableDesignationSyntax designation => designation.Identifier.ValueText,
                    _ => null,
                };
                if (declared == "_announceRendered")
                {
                    offenders.Add($"{label}: a declaration named _announceRendered shadows the workspace's field");
                }
            }
            foreach (IdentifierNameSyntax reference in source.Root.DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                if (reference.Identifier.ValueText != "_announceRendered")
                {
                    continue;
                }
                // A-10 as amended (W6-2 PR B, BD-12): the ONE seed is the
                // workspace's relay, constructed in NewGraphRelay — no
                // document constructs one any more.
                bool seedsTheRelay = reference.Parent is ArgumentSyntax argument
                    && argument.Parent is ArgumentListSyntax list
                    && list.Parent is ObjectCreationExpressionSyntax relayCreation
                    && relayCreation.Type.ToString().EndsWith("GraphAnnouncer", StringComparison.Ordinal)
                    && reference.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault()
                        is { Identifier.ValueText: "NewGraphRelay" };
                if (seedsTheRelay)
                {
                    relaySeeds++;
                    continue;
                }
                offenders.Add($"{label}: _announceRendered referenced outside the relay's construction");
            }
        }
        Assert.True(
            offenders.Count == 0,
            "graph code must announce through GraphAnnouncer (contract A-10), never directly:\n"
            + string.Join("\n", offenders));
        Assert.Equal(1, relaySeeds);
    }

    /// <summary>W6-2 PR B, B-19 (i) — A-10 as amended (BD-12): exactly ONE
    /// <c>GraphAnnouncer</c> is constructed anywhere in the shell, by
    /// declared type whatever the receiver is named, inside
    /// <c>NewGraphRelay</c>, over the workspace's rendered seam; a second
    /// anywhere, under any name, fails here (IGF-11, IGG-1).</summary>
    private const string TheRelayType = "SlateWindows.Graph.GraphAnnouncer";

    /// <summary>The creation's type as BOUND (IPB-4, IPB-8): an explicit
    /// creation through an alias (<c>using Relay = ...GraphAnnouncer; new
    /// Relay(...)</c>) and a target-typed <c>new(...)</c> typed by a local,
    /// a return, an assignment or a parameter both resolve to the same
    /// symbol; a creation binding yields nothing for falls back to the
    /// written type — the explicit one's, or the nearest enclosing
    /// declaration's for an implicit one, the local's before the method's.</summary>
    private static string CreationType(SemanticModel model, BaseObjectCreationExpressionSyntax creation)
    {
        TypeInfo info = model.GetTypeInfo(creation);
        ITypeSymbol? bound = info.Type is { TypeKind: not TypeKind.Error } type ? type : info.ConvertedType;
        if (bound is { TypeKind: not TypeKind.Error })
        {
            return bound.ToDisplayString();
        }
        return creation switch
        {
            ObjectCreationExpressionSyntax explicitly => explicitly.Type.ToString(),
            _ => creation.Ancestors().OfType<VariableDeclarationSyntax>().FirstOrDefault()?.Type.ToString()
                ?? creation.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault()?.ReturnType.ToString()
                ?? string.Empty,
        };
    }

    [Fact]
    public void ExactlyOneGraphRelayIsConstructedInTheShell()
    {
        // Every creation, explicit or target-typed, is typed by BINDING over
        // the shell's own compilation (codex post-implementation passes 1 and
        // 2, IPB-4 and IPB-8: the method's return type was read first, so a
        // `GraphAnnouncer extra = new(...)` inside a void method was
        // invisible; an explicit creation was read by its spelling, so a
        // `using` alias evaded it). An alias naming the relay anywhere in the
        // shell is forbidden outright as well.
        var creations = new List<string>();
        var factoryUses = new List<string>();
        foreach ((string relative, CSharpSource source) in ShellCompilation.Sources)
        {
            SemanticModel model = ShellCompilation.ModelFor(source);
            foreach (UsingDirectiveSyntax directive in source.Root.DescendantNodes().OfType<UsingDirectiveSyntax>())
            {
                if (directive.Alias is not null
                    && directive.NamespaceOrType.ToString().EndsWith("GraphAnnouncer", StringComparison.Ordinal))
                {
                    creations.Add($"{relative}:using alias {directive.Alias.Name} = {directive.NamespaceOrType}");
                }
            }
            foreach (BaseObjectCreationExpressionSyntax creation in source.Root
                .DescendantNodes()
                .OfType<BaseObjectCreationExpressionSyntax>())
            {
                string declared = CreationType(model, creation);
                if (declared != TheRelayType && !declared.EndsWith("GraphAnnouncer", StringComparison.Ordinal))
                {
                    continue;
                }
                MethodDeclarationSyntax? owner = creation.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
                string firstArgument = creation.ArgumentList?.Arguments.FirstOrDefault()?.Expression.ToString() ?? "<none>";
                creations.Add($"{relative}:{owner?.Identifier.ValueText ?? "<top level>"}({firstArgument})");
            }
            // One allocation SITE is not one instance (codex post-implementation
            // pass 3, IPB-16): the factory's calls and its method-group
            // references, bound — exactly the constructor's one call, and no
            // reference that could call it later.
            foreach (InvocationExpressionSyntax call in source.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (IsTheFactory(model.GetSymbolInfo(call)))
                {
                    factoryUses.Add($"{relative}:{OwnerOf(call)}");
                }
            }
            foreach (ExpressionSyntax reference in source.Root.DescendantNodes().OfType<ExpressionSyntax>())
            {
                if (IsAMethodGroupReference(reference) && IsTheFactory(model.GetSymbolInfo(reference)))
                {
                    factoryUses.Add($"{relative}:{OwnerOf(reference)} (a method-group reference)");
                }
            }
        }
        Assert.Equal(
            ["Graph/WorkspaceViewModel.Graph.cs:NewGraphRelay(_announceRendered)"],
            creations);
        Assert.Equal(["WorkspaceViewModel.cs:<ctor>"], factoryUses);
    }

    private static bool IsTheFactory(SymbolInfo info)
    {
        IEnumerable<ISymbol> candidates = info.Symbol is { } bound ? [bound] : info.CandidateSymbols;
        return candidates.OfType<IMethodSymbol>().Any(method =>
            method.Name == "NewGraphRelay" && method.ContainingType.ToDisplayString() == "SlateWindows.WorkspaceViewModel");
    }

    /// <summary>A name or a member access that is neither an invocation's
    /// callee nor the member half of a larger access — the shape in which a
    /// method group converts to a delegate (IPB-16, IPB-17).</summary>
    internal static bool IsAMethodGroupReference(ExpressionSyntax expression) =>
        expression is IdentifierNameSyntax or MemberAccessExpressionSyntax
        && !(expression.Parent is InvocationExpressionSyntax invocation && ReferenceEquals(invocation.Expression, expression))
        && !(expression is IdentifierNameSyntax && expression.Parent is MemberAccessExpressionSyntax access && ReferenceEquals(access.Name, expression));

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
            }
        }
        return "<top level>";
    }

    /// <summary>The relay is the one file that renders (contract A-10):
    /// <c>RenderLabel</c> is a member of it, and no other graph source
    /// names the renderer.</summary>
    [Fact]
    public void TheRelayIsTheOneFileThatRenders()
    {
        CSharpSource relay = CSharpSource.Load("Graph", TheRelay);
        Assert.Contains(
            relay.Root.DescendantNodes().OfType<MemberAccessExpressionSyntax>(),
            access => access.Name.Identifier.ValueText == "A11yRender");
        _ = relay.Method("RenderLabel");
    }

    /// <summary>Every shared grid under <c>Graph/</c> has its
    /// <c>Announce</c> seam swapped onto the document's relay seam.</summary>
    [Fact]
    public void EveryGridUnderGraphRidesTheRelay()
    {
        CSharpSource document = CSharpSource.Load("Graph", "GraphDocumentViewModel.cs");
        string[] relaySeams =
        [
            .. document.Root.DescendantNodes()
                .OfType<PropertyDeclarationSyntax>()
                .Where(property => property.ExpressionBody?.Expression
                    is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Relay" } access
                    && CSharpSource.Normalize(access.Expression)
                        .EndsWith("announcer", StringComparison.OrdinalIgnoreCase))
                .Select(property => property.Identifier.ValueText),
        ];
        Assert.Contains("GridRelaySeam", relaySeams);
        var gridFiles = new List<string>();
        var offenders = new List<string>();
        foreach (string file in GraphSources())
        {
            CSharpSource source = CSharpSource.LoadPath(file);
            bool constructsGrid = source.Root.DescendantNodes()
                .OfType<ObjectCreationExpressionSyntax>()
                .Any(creation => creation.Type.ToString().EndsWith("AccessibleDataGrid", StringComparison.Ordinal));
            if (!constructsGrid)
            {
                continue;
            }
            gridFiles.Add(Relative(file));
            bool swaps = source.Root.DescendantNodes()
                .OfType<AssignmentExpressionSyntax>()
                .Any(assignment => assignment.Left is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Announce" }
                    && assignment.Right is MemberAccessExpressionSyntax right
                    && relaySeams.Contains(right.Name.Identifier.ValueText, StringComparer.Ordinal));
            if (!swaps)
            {
                offenders.Add(Relative(file));
            }
        }
        Assert.NotEmpty(gridFiles);
        Assert.True(offenders.Count == 0, "a grid under Graph/ never swaps its Announce seam onto the relay: " + string.Join(", ", offenders));
    }

    /// <summary>Rule L, Term 1 (contract A-1): the document's load entry
    /// points have ONE caller outside the document — the follow method in
    /// the workspace's graph partial — so nothing else in the shell can
    /// start a graph load.</summary>
    [Fact]
    public void TheDocumentsLoadHasOneCallerOutsideTheDocument()
    {
        string shellRoot = SourceText.ShellSourceRoot();
        var callers = new List<string>();
        foreach (string file in Directory.EnumerateFiles(shellRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || Path.GetFileName(file) == "GraphDocumentViewModel.cs")
            {
                continue;
            }
            CSharpSource source = CSharpSource.LoadPath(file);
            foreach (InvocationExpressionSyntax call in source.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (call.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Load" } access)
                {
                    continue;
                }
                // A graph load names its kind; other documents' Load() take none.
                if (!call.ArgumentList.Arguments.Any(a => a.ToString().Contains("GraphLoadKind", StringComparison.Ordinal)))
                {
                    continue;
                }
                MethodDeclarationSyntax? owner = call.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
                callers.Add($"{Path.GetRelativePath(shellRoot, file).Replace('\\', '/')}:{owner?.Identifier.ValueText}");
            }
        }
        Assert.Equal(["Graph/WorkspaceViewModel.Graph.cs:GraphFollowActiveTab"], callers);
    }

    /// <summary>Contract A-6 (IGA-69): the ONE cell lookup — no source
    /// under <c>Graph/</c> reads a row's <c>Cells</c> except the document's
    /// lookup members, so no host-side position knowledge can be typed in
    /// any syntax.</summary>
    [Fact]
    public void OnlyTheDocumentsLookupReadsARowsCells()
    {
        var readers = new List<string>();
        foreach (string file in GraphSources())
        {
            CSharpSource source = CSharpSource.LoadPath(file);
            foreach (MemberAccessExpressionSyntax access in source.Root.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
            {
                if (access.Name.Identifier.ValueText != "Cells")
                {
                    continue;
                }
                MethodDeclarationSyntax? owner = access.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
                readers.Add($"{Relative(file)}:{owner?.Identifier.ValueText}");
            }
        }
        Assert.Equal(
            ["GraphDocumentViewModel.cs:CellAt", "GraphDocumentViewModel.cs:CellAt", "GraphDocumentViewModel.cs:CellOf", "GraphDocumentViewModel.cs:CellOf"],
            readers.OrderBy(r => r, StringComparer.Ordinal));
        // IPA-11: the permitted helpers key by the VECTOR — CellOf indexes
        // through CellIndexOf, and neither helper indexes a row by a literal.
        CSharpSource document = CSharpSource.Load("Graph", "GraphDocumentViewModel.cs");
        MethodDeclarationSyntax cellOf = document.Method("CellOf");
        Assert.Contains(
            cellOf.DescendantNodes().OfType<InvocationExpressionSyntax>(),
            call => call.Expression is IdentifierNameSyntax { Identifier.ValueText: "CellIndexOf" });
        foreach (string helper in new[] { "CellOf", "CellAt" })
        {
            MethodDeclarationSyntax method = document.Method(helper);
            Assert.DoesNotContain(
                method.DescendantNodes().OfType<ElementAccessExpressionSyntax>(),
                access => access.ArgumentList.Arguments.Any(argument => argument.Expression is LiteralExpressionSyntax));
        }
    }

    /// <summary>Contract A-5: none of core's nine headers is typed under
    /// <c>Graph/</c> — the grid is built from the fetched vector.</summary>
    [Fact]
    public void NoColumnHeaderIsTypedUnderGraph()
    {
        string[] headers = [.. uniffi.slate_uniffi.SlateUniffiMethods.GraphTableColumns().Select(s => s.Header)];
        Assert.Equal(9, headers.Length);
        var offenders = new List<string>();
        foreach (string file in GraphSources())
        {
            CSharpSource source = CSharpSource.LoadPath(file);
            foreach (LiteralExpressionSyntax literal in source.Root.DescendantNodes().OfType<LiteralExpressionSyntax>())
            {
                if (literal.Token.Value is string text && headers.Contains(text, StringComparer.Ordinal))
                {
                    offenders.Add($"{Relative(file)}: \"{text}\"");
                }
            }
        }
        Assert.True(offenders.Count == 0, "a column header is typed under Graph/: " + string.Join(", ", offenders));
    }

    /// <summary>Contract A-1 / spec R-B: no other type under <c>Graph/</c>
    /// declares a MUTABLE field of the view state's five names or a second
    /// mutable copy of the filter, the query or the mode; the immutable
    /// request, token, envelope and publication carry copies by design.</summary>
    [Fact]
    public void NoMutableShadowOfTheViewStateExistsUnderGraph()
    {
        string[] names = ["SelectedKey", "Filter", "NameQuery", "Groups", "Mode"];
        var offenders = new List<string>();
        foreach (string file in GraphSources())
        {
            if (Relative(file) == "GraphViewState.cs")
            {
                continue;
            }
            CSharpSource source = CSharpSource.LoadPath(file);
            foreach (PropertyDeclarationSyntax property in source.Root.DescendantNodes().OfType<PropertyDeclarationSyntax>())
            {
                bool mutable = property.AccessorList?.Accessors.Any(a => a.Kind() == Microsoft.CodeAnalysis.CSharp.SyntaxKind.SetAccessorDeclaration) == true;
                if (mutable && names.Contains(property.Identifier.ValueText, StringComparer.Ordinal))
                {
                    offenders.Add($"{Relative(file)}: {property.Identifier.ValueText}");
                }
            }
            foreach (FieldDeclarationSyntax field in source.Root.DescendantNodes().OfType<FieldDeclarationSyntax>())
            {
                bool immutable = field.Modifiers.Any(m => m.ValueText is "readonly" or "const");
                foreach (VariableDeclaratorSyntax variable in field.Declaration.Variables)
                {
                    string bare = variable.Identifier.ValueText.TrimStart('_');
                    if (!immutable && names.Any(n => string.Equals(n, bare, StringComparison.OrdinalIgnoreCase)))
                    {
                        offenders.Add($"{Relative(file)}: {variable.Identifier.ValueText}");
                    }
                }
            }
        }
        Assert.True(offenders.Count == 0, "a mutable shadow of the view state exists under Graph/: " + string.Join(", ", offenders));
    }
}
