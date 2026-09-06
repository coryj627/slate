// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// W6-2 PR B, slice B1 (#746), contract B-19 (iii)–(v): the leaf's load and
// speech entry points have exactly the callers rule C names; every writer
// of the pane's visibility reaches the pending mount's consume; the
// constructor seeds the initial mount; the graph document enqueues its
// filter count through the relay's GATED entry and nothing enqueues one
// ungated; `SyncPanels` is the only recorder of the leaf's root and the
// mutation boundary its only reconciler; and the depth's DATAFLOW — every
// write to the storage is the FFI clamp's result, and only the named
// producers reach the clamp's argument.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "connections-leaf")]
public sealed class ConnectionsLeafCensus
{
    private static string ShellRoot => SourceText.ShellSourceRoot();

    private static IEnumerable<(string Relative, CSharpSource Source)> ShellSources()
    {
        foreach (string file in Directory.EnumerateFiles(ShellRoot, "*.cs", SearchOption.AllDirectories)
            .OrderBy(f => f, StringComparer.Ordinal))
        {
            string relative = Path.GetRelativePath(ShellRoot, file).Replace('\\', '/');
            if (relative.StartsWith("obj/", StringComparison.Ordinal) || relative.Contains("/obj/", StringComparison.Ordinal))
            {
                continue;
            }
            yield return (relative, CSharpSource.LoadPath(file));
        }
    }

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

    /// <summary>Invocations of a member on the leaf, the receiver RESOLVED
    /// (codex post-implementation pass 1, IPB-5 — the receiver's spelling
    /// was the test): <c>Connections.X(</c> and <c>this.Connections.X(</c>;
    /// a local that a same-member initialiser binds to the leaf
    /// (<c>var document = Connections; document.X()</c>) or to its
    /// construction; a local or parameter declared as
    /// <c>ConnectionsLeafViewModel</c>.</summary>
    private const string TheLeafType = "SlateWindows.Graph.ConnectionsLeafViewModel";

    /// <summary>Invocations of a member of the leaf, BOUND (codex
    /// post-implementation pass 2, IPB-9 — locals were resolved, fields,
    /// properties, casts and method groups were not): every invocation whose
    /// bound method, or every candidate when overload resolution is
    /// incomplete, is the leaf type's member of that name, whatever the
    /// receiver's spelling.</summary>
    private static IEnumerable<(string Relative, string Owner, string Member)> LeafCalls(string member)
    {
        foreach ((string relative, CSharpSource source) in ShellCompilation.Sources)
        {
            SemanticModel model = ShellCompilation.ModelFor(source);
            foreach (InvocationExpressionSyntax call in source.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                SymbolInfo info = model.GetSymbolInfo(call);
                IEnumerable<ISymbol> candidates = info.Symbol is { } bound ? [bound] : info.CandidateSymbols;
                if (candidates.OfType<IMethodSymbol>().Any(method =>
                        method.Name == member
                        && method.ContainingType.ToDisplayString() == TheLeafType))
                {
                    yield return (relative, OwnerOf(call), member);
                }
            }
        }
    }

    /// <summary>B-19 (iii): the trigger entry points and their one owner each.</summary>
    [Fact]
    public void EveryTriggerEntryPointHasExactlyTheCallersRuleCNames()
    {
        (string Member, string[] Owners)[] expected =
        [
            ("Mounted", ["WorkspaceViewModel.Connections.cs:ConsumePendingMount"]),
            ("Shown", ["WorkspaceViewModel.Connections.cs:OnLeafShown"]),
            ("Show", ["WorkspaceViewModel.Connections.cs:ShowConnections"]),
            ("NoteChanged", ["WorkspaceViewModel.Connections.cs:ReconcileConnectionsRootTo"]),
            // Rule D's entries (W6-2 PR B2, B2-2): the pin and the pop are
            // the funnels' (T3); the rename and delete hooks the workspace's.
            ("PinTo", ["WorkspaceViewModel.Connections.cs:ReRootConnectionsOn"]),
            ("PopTo", ["WorkspaceViewModel.Connections.cs:ConnectionsBack"]),
            ("RepairSharedKey", ["WorkspaceViewModel.Connections.cs:ReRootConnectionsOn"]),
            ("Retarget", ["WorkspaceViewModel.cs:RetargetPath"]),
            ("Prune", ["WorkspaceViewModel.cs:InvalidatePath"]),
            ("Probe", ["WorkspaceViewModel.Connections.cs:ProbeConnections"]),
            ("Deeper", ["WorkspaceViewModel.Connections.cs:ConnectionsDeeperCommand"]),
            ("Shallower", ["WorkspaceViewModel.Connections.cs:ConnectionsShallowerCommand"]),
            ("ViewCollapsed", ["WorkspaceViewModel.Connections.cs:OnRightPaneVisibilityChanged"]),
            // Term 9's entry lines are the document's own two calls (bound,
            // IPB-9: a call inside the leaf counts like any other).
            ("AnnounceStatus", ["Graph/ConnectionsLeafViewModel.cs:FocusEntered", "Graph/ConnectionsLeafViewModel.cs:FocusEntered", "WorkspaceViewModel.Connections.cs:ShowConnections"]),
        ];
        var failures = new List<string>();
        foreach ((string member, string[] owners) in expected)
        {
            string[] found = [.. LeafCalls(member).Select(c => $"{c.Relative}:{c.Owner}").OrderBy(s => s, StringComparer.Ordinal)];
            if (!found.SequenceEqual(owners.OrderBy(s => s, StringComparer.Ordinal)))
            {
                failures.Add($"{member}: callers [{string.Join(", ", found)}], expected [{string.Join(", ", owners)}]");
            }
        }
        // A method-group reference to a trigger, or a call to a trigger's
        // name that binds to nothing, is an offence (codex post-implementation
        // pass 3, IPB-17): a delegate-bound call hides behind `Action.Invoke`,
        // and an unbound reference — a member the compilation cannot see —
        // is refused rather than trusted.
        failures.AddRange(ProtectedReferences([.. expected.Select(e => e.Member)]));
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    private static IEnumerable<ISymbol> Candidates(SymbolInfo info) =>
        info.Symbol is { } bound ? [bound] : info.CandidateSymbols;

    private static string CalleeName(InvocationExpressionSyntax call) => call.Expression switch
    {
        MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
        IdentifierNameSyntax bare => bare.Identifier.ValueText,
        _ => string.Empty,
    };

    private const string TheWorkspaceType = "SlateWindows.WorkspaceViewModel";

    /// <summary>Every method-group reference to one of a type's named
    /// members, and every call to one of those names the compilation binds
    /// to nothing (IPB-17, IPB-28).</summary>
    private static IEnumerable<string> ProtectedReferences(string[] members, string containingType = TheLeafType)
    {
        foreach ((string relative, CSharpSource source) in ShellCompilation.Sources)
        {
            SemanticModel model = ShellCompilation.ModelFor(source);
            foreach (ExpressionSyntax reference in source.Root.DescendantNodes().OfType<ExpressionSyntax>())
            {
                if (!GraphAnnouncerCensus.IsAMethodGroupReference(reference))
                {
                    continue;
                }
                if (Candidates(model.GetSymbolInfo(reference)).OfType<IMethodSymbol>()
                    .Any(method => members.Contains(method.Name) && method.ContainingType.ToDisplayString() == containingType))
                {
                    yield return $"{relative}:{OwnerOf(reference)} references {CSharpSource.Normalize(reference)} as a method group";
                }
            }
            foreach (InvocationExpressionSyntax call in source.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (!members.Contains(CalleeName(call)))
                {
                    continue;
                }
                if (!Candidates(model.GetSymbolInfo(call)).Any())
                {
                    yield return $"{relative}:{OwnerOf(call)} calls {CSharpSource.Normalize(call.Expression)} and the compilation binds it to nothing";
                }
            }
        }
    }

    /// <summary>B-19 (iii): the root's writers — <c>SyncPanels</c> records,
    /// the boundary reconciles, nothing else reaches the leaf's root.</summary>
    [Fact]
    public void SyncPanelsIsTheOnlyRecorderAndTheBoundaryTheOnlyReconciler()
    {
        // BOUND (codex post-implementation pass 4, IPB-28): the recorder's and
        // the reconciler's callers by their method symbols, a method-group
        // reference to either an offence, an unbound call to either refused.
        var recorders = new List<string>();
        var reconcilers = new List<string>();
        var funnels = new List<string>();
        var boundarySyncs = new List<string>();
        foreach ((string relative, CSharpSource source) in ShellCompilation.Sources)
        {
            SemanticModel model = ShellCompilation.ModelFor(source);
            foreach (InvocationExpressionSyntax call in source.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                IMethodSymbol[] methods = [.. Candidates(model.GetSymbolInfo(call)).OfType<IMethodSymbol>()
                    .Where(method => method.ContainingType.ToDisplayString() == TheWorkspaceType)];
                if (methods.Any(method => method.Name == "SyncConnectionsRoot"))
                {
                    recorders.Add($"{relative}:{OwnerOf(call)}");
                }
                if (methods.Any(method => method.Name == "ReconcileConnectionsRoot"))
                {
                    reconcilers.Add($"{relative}:{OwnerOf(call)}");
                }
                // The funnel that owns the leaf's ONE NoteChanged call (pass
                // 5, IPB-31): reached only by the recorder outside a mutation
                // and by the boundary's reconciler — a direct call would pass
                // the buffering.
                if (methods.Any(method => method.Name == "ReconcileConnectionsRootTo"))
                {
                    funnels.Add($"{relative}:{OwnerOf(call)}");
                }
                // The boundary's sync (W6-2 PR B2, IGL-2): the panels synced
                // with the root RECORDED, then reconciled once — called by
                // the mutation runner's boundary and nothing else.
                if (methods.Any(method => method.Name == "SyncPanelsAtTheBoundary"))
                {
                    boundarySyncs.Add($"{relative}:{OwnerOf(call)}");
                }
            }
        }
        Assert.Equal(["WorkspaceViewModel.cs:SyncPanels"], recorders);
        Assert.Equal(["WorkspaceViewModel.Connections.cs:SyncPanelsAtTheBoundary"], reconcilers);
        Assert.Equal(["WorkspaceViewModel.Persistence.cs:RunWorkspaceMutation"], boundarySyncs);
        Assert.Equal(["WorkspaceViewModel.Connections.cs:SyncConnectionsRoot", "WorkspaceViewModel.Connections.cs:ReconcileConnectionsRoot"], funnels);
        string[] offenders = [.. ProtectedReferences(["SyncConnectionsRoot", "ReconcileConnectionsRoot", "ReconcileConnectionsRootTo", "SyncPanelsAtTheBoundary"], TheWorkspaceType)];
        Assert.True(offenders.Length == 0, "the root's recorder, reconciler or funnel is reached other than by a bound call:\n" + string.Join("\n", offenders));
    }

    /// <summary>A call the compilation binds to the workspace's named method
    /// (IPB-32): a same-suffixed name elsewhere is not it.</summary>
    private static bool IsWorkspaceMethod(SemanticModel model, InvocationExpressionSyntax call, string name) =>
        Candidates(model.GetSymbolInfo(call)).OfType<IMethodSymbol>()
            .Any(method => method.Name == name && method.ContainingType.ToDisplayString() == TheWorkspaceType);

    /// <summary>W6-2 PR B2, B2-3 / B2-10 (iii): the re-root funnel's callers,
    /// bound, are exactly the three entrances — the leaf's seam installer,
    /// the table's addressed wrapper, the Bases' command and seam — and
    /// Back's are exactly its key owner's installer and its command (T4);
    /// a method-group reference to either is an offence.</summary>
    [Fact]
    public void TheReRootFunnelAndBackAreReachedByTheirEntrancesAlone()
    {
        var reRoots = new List<string>();
        var backs = new List<string>();
        foreach ((string relative, CSharpSource source) in ShellCompilation.Sources)
        {
            SemanticModel model = ShellCompilation.ModelFor(source);
            foreach (InvocationExpressionSyntax call in source.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                IMethodSymbol[] methods = [.. Candidates(model.GetSymbolInfo(call)).OfType<IMethodSymbol>()
                    .Where(method => method.ContainingType.ToDisplayString() == TheWorkspaceType)];
                if (methods.Any(method => method.Name == "ReRootConnectionsOn"))
                {
                    reRoots.Add($"{relative}:{OwnerOf(call)}");
                }
                if (methods.Any(method => method.Name == "ConnectionsBack"))
                {
                    backs.Add($"{relative}:{OwnerOf(call)}");
                }
            }
        }
        Assert.Equal(
            [
                "Graph/WorkspaceViewModel.Graph.cs:ReRootGraphRowFromSurface",
                "WorkspaceViewModel.Bases.cs:BasesShowConnectionsFor",
                "WorkspaceViewModel.Connections.cs:NewConnectionsLeaf",
            ],
            reRoots.Order(StringComparer.Ordinal));
        Assert.Equal(
            [
                "WorkspaceViewModel.Connections.cs:ConnectionsBackCommand",
                "WorkspaceViewModel.Connections.cs:NewConnectionsLeaf",
            ],
            backs.Order(StringComparer.Ordinal));
        string[] offenders = [.. ProtectedReferences(["ReRootConnectionsOn", "ConnectionsBack"], TheWorkspaceType)];
        Assert.True(offenders.Length == 0, "the funnel or Back is reached other than by a bound call:\n" + string.Join("\n", offenders));
    }

    /// <summary>W6-2 PR B2, B2-10 (ix): every row-action <c>Execute</c> under
    /// <c>Graph/</c> guards the row's currency — the document's
    /// <c>IsRowCurrent</c> — before any seam, and the Bases surface's
    /// <c>RowCommand</c> guards the captured result's currency before its
    /// body (TGB-9's class, IGJ-7, IGJ-8).</summary>
    [Fact]
    public void EveryRowActionGuardsTheRowsCurrencyBeforeItsSeam()
    {
        var failures = new List<string>();
        foreach ((string file, string method, string guard) in new[]
        {
            ("Graph/GraphDocumentViewModel.cs", "Execute", "IsRowCurrent"),
            ("Graph/ConnectionsLeafViewModel.cs", "Execute", "IsRowCurrent"),
            ("Bases/BaseSurfaceView.cs", "RowCommand", "ReferenceEquals"),
        })
        {
            (string relative, CSharpSource source) = ShellCompilation.Sources.First(entry => entry.Relative == file);
            MethodDeclarationSyntax execute = source.Root.DescendantNodes().OfType<MethodDeclarationSyntax>()
                .First(candidate => candidate.Identifier.ValueText == method);
            InvocationExpressionSyntax? guarded = execute.DescendantNodes().OfType<InvocationExpressionSyntax>()
                .FirstOrDefault(call => CalleeName(call) == guard);
            if (guarded is null)
            {
                failures.Add($"{relative}:{method} has no {guard} guard");
                continue;
            }
            // Every seam invocation (a delegate property's call) sits AFTER
            // the guard in the method's text — the guard is its admission.
            foreach (InvocationExpressionSyntax call in execute.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                bool seam = call.Expression is PostfixUnaryExpressionSyntax { Operand: IdentifierNameSyntax name }
                    && name.Identifier.ValueText.EndsWith("FromSurface", StringComparison.Ordinal)
                    || call.Expression is IdentifierNameSyntax { Identifier.ValueText: "body" };
                if (seam && call.SpanStart < guarded.SpanStart)
                {
                    failures.Add($"{relative}:{method} reaches {CSharpSource.Normalize(call.Expression)} before its {guard} guard");
                }
            }
        }
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    /// <summary>The consume, BOUND (IPB-18): the workspace's own method; an
    /// invocation the compilation cannot bind is not a consume.</summary>
    private static bool IsConsume(IOperation operation) =>
        operation is IInvocationOperation invocation
        && invocation.TargetMethod.Name == "ConsumePendingMount"
        && invocation.TargetMethod.ContainingType.ToDisplayString() == "SlateWindows.WorkspaceViewModel";

    /// <summary>The control-flow graph the reveal lives in: the member's,
    /// then — descending outermost first — every anonymous function's or
    /// local function's enclosing the reveal (IPB-18: a reveal inside a
    /// command lambda used to fall back to statement order). Null when the
    /// graph cannot be built, which the census treats as an offence.</summary>
    private static ControlFlowGraph? GraphFor(SemanticModel model, SyntaxNode scope, SyntaxNode target)
    {
        ControlFlowGraph? graph;
        try
        {
            graph = model.GetOperation(scope) switch
            {
                IMethodBodyOperation body => ControlFlowGraph.Create(body),
                IConstructorBodyOperation body => ControlFlowGraph.Create(body),
                _ => null,
            };
        }
        catch (ArgumentException)
        {
            return null;
        }
        if (graph is null)
        {
            return null;
        }
        SyntaxNode[] functions =
        [
            .. target.Ancestors()
                .TakeWhile(ancestor => !ReferenceEquals(ancestor, scope))
                .Where(ancestor => ancestor is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax)
                .Reverse(),
        ];
        foreach (SyntaxNode function in functions)
        {
            ControlFlowGraph? inner = null;
            try
            {
                if (function is LocalFunctionStatementSyntax local)
                {
                    if (model.GetDeclaredSymbol(local) is IMethodSymbol symbol)
                    {
                        inner = graph.GetLocalFunctionControlFlowGraph(symbol);
                    }
                }
                else
                {
                    IFlowAnonymousFunctionOperation? lambda = graph.Blocks
                        .SelectMany(BlockOperations)
                        .SelectMany(operation => operation.DescendantsAndSelf())
                        .OfType<IFlowAnonymousFunctionOperation>()
                        .FirstOrDefault(operation => operation.Syntax.Span == function.Span);
                    inner = lambda is null ? null : graph.GetAnonymousFunctionControlFlowGraph(lambda);
                }
            }
            catch (ArgumentException)
            {
                inner = null;
            }
            if (inner is null)
            {
                return null;
            }
            graph = inner;
        }
        return graph;
    }

    private static IEnumerable<IOperation> BlockOperations(BasicBlock block) =>
        block.BranchValue is { } branch ? block.Operations.Append(branch) : block.Operations;

    private static IEnumerable<BasicBlock> Successors(BasicBlock block)
    {
        if (block.FallThroughSuccessor?.Destination is { } fall)
        {
            yield return fall;
        }
        if (block.ConditionalSuccessor?.Destination is { } conditional)
        {
            yield return conditional;
        }
    }

    /// <summary>Every path from the reveal to the member's exit passes a
    /// consume (IPB-9): Roslyn's control-flow graph over the member, the
    /// reveal's block found by its syntax, a consume later in that block
    /// satisfying it at once, otherwise the walk over successors stopping at
    /// every block that consumes; reaching the exit is the offence. A
    /// member the graph cannot be built for (an expression body the
    /// operation tree does not model, a reveal inside a lambda) falls back
    /// to the statement order in the reveal's own or an enclosing block.</summary>
    private static bool ConsumePostDominates(SemanticModel model, SyntaxNode scope, AssignmentExpressionSyntax assignment)
    {
        ControlFlowGraph? graph = GraphFor(model, scope, assignment);
        if (graph is null)
        {
            // Fail closed (IPB-18): a reveal whose execution scope cannot be
            // analysed is an offence, not a fallback.
            return false;
        }
        BasicBlock? start = null;
        int startIndex = -1;
        foreach (BasicBlock block in graph.Blocks)
        {
            IOperation[] operations = [.. BlockOperations(block)];
            for (int index = 0; index < operations.Length; index++)
            {
                if (operations[index].DescendantsAndSelf().Any(op => op.Syntax.Span == assignment.Span))
                {
                    start = block;
                    startIndex = index;
                }
            }
        }
        if (start is null)
        {
            return false;
        }
        if (BlockOperations(start).Skip(startIndex + 1).Any(op => op.DescendantsAndSelf().Any(IsConsume)))
        {
            return true;
        }
        var seen = new HashSet<BasicBlock>();
        var pending = new Stack<BasicBlock>(Successors(start));
        while (pending.Count > 0)
        {
            BasicBlock block = pending.Pop();
            if (!seen.Add(block))
            {
                continue;
            }
            if (block.Kind == BasicBlockKind.Exit)
            {
                return false;
            }
            if (BlockOperations(block).Any(op => op.DescendantsAndSelf().Any(IsConsume)))
            {
                continue;
            }
            foreach (BasicBlock next in Successors(block))
            {
                pending.Push(next);
            }
        }
        return true;
    }

    /// <summary>B-19 (iii): every writer of <c>IsRightPaneVisible = true</c>
    /// in the shell reaches the pending mount's consume at its route's end
    /// — the consume call in the same method, or the outermost mutation
    /// boundary (a <c>RunWorkspaceMutation</c> ancestor) — and the
    /// constructor seeds the initial mount.</summary>
    [Fact]
    public void EveryPaneRevealConsumesThePendingMountAndTheConstructorSeedsIt()
    {
        var offenders = new List<string>();
        int writers = 0;
        bool seeded = false;
        foreach ((string relative, CSharpSource source) in ShellCompilation.Sources)
        {
            foreach (AssignmentExpressionSyntax assignment in source.Root.DescendantNodes().OfType<AssignmentExpressionSyntax>())
            {
                string target = CSharpSource.Normalize(assignment.Left);
                if (!target.EndsWith("IsRightPaneVisible", StringComparison.Ordinal))
                {
                    continue;
                }
                string value = CSharpSource.Normalize(assignment.Right);
                if (value == "false")
                {
                    continue;
                }
                writers++;
                SyntaxNode? scope = assignment.Ancestors().FirstOrDefault(a => a is MethodDeclarationSyntax or ConstructorDeclarationSyntax or PropertyDeclarationSyntax or AccessorDeclarationSyntax);
                // The consume must POST-DOMINATE the reveal (IPB-5, IPB-9):
                // every path from the assignment to the member's exit passes a
                // consume — over Roslyn's control-flow graph, so a `return`
                // between the two is the offence it is.
                SemanticModel model = ShellCompilation.ModelFor(source);
                bool consumes = scope is not null && ConsumePostDominates(model, scope, assignment);
                // Inside the mutation's OWN delegate (pass 5, IPB-32): the
                // reveal sits in an anonymous function that is an argument of
                // a call the compilation binds to the workspace's
                // RunWorkspaceMutation — a call whose name merely ends that
                // way binds to something else and exempts nothing.
                bool insideMutation = assignment.Ancestors().OfType<AnonymousFunctionExpressionSyntax>()
                    .Any(function => function.Parent is ArgumentSyntax argument
                        && argument.Parent?.Parent is InvocationExpressionSyntax call
                        && IsWorkspaceMethod(model, call, "RunWorkspaceMutation"));
                // The setter itself is the arm, not a route.
                bool isTheSetter = scope is AccessorDeclarationSyntax && relative == "WorkspaceViewModel.cs";
                if (!consumes && !insideMutation && !isTheSetter)
                {
                    offenders.Add($"{relative}:{OwnerOf(assignment)} — {target} = {value}");
                }
            }
            foreach (InvocationExpressionSyntax call in source.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (IsWorkspaceMethod(ShellCompilation.ModelFor(source), call, "SeedInitialConnectionsMount")
                    && relative == "WorkspaceViewModel.cs"
                    && OwnerOf(call) == "<ctor>")
                {
                    seeded = true;
                }
            }
        }
        Assert.True(writers >= 8, $"the shell writes IsRightPaneVisible at {writers} sites; the census expected the known eight or more");
        Assert.True(offenders.Count == 0, "a pane reveal reaches no consume:\n" + string.Join("\n", offenders));
        Assert.True(seeded, "the constructor does not seed the initial mount (Term 3(a), IGH-7)");
    }

    /// <summary>Term 10 / B-19 (iii): the graph document enqueues its filter
    /// count through the relay's GATED entry, with the effective predicate,
    /// and no graph source enqueues a <c>GraphFilterCount</c> ungated.</summary>
    [Fact]
    public void TheFilterCountIsEnqueuedThroughTheGatedEntryAndNowhereUngated()
    {
        var gated = new List<string>();
        var ungated = new List<string>();
        foreach ((string relative, CSharpSource source) in ShellCompilation.Sources)
        {
            if (!relative.StartsWith("Graph/", StringComparison.Ordinal) || relative == "Graph/GraphAnnouncer.cs")
            {
                continue;
            }
            SemanticModel model = ShellCompilation.ModelFor(source);
            foreach (InvocationExpressionSyntax call in source.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                // BOUND (pass 5, IPB-33): the callee is the relay's own method
                // and the argument's TYPE is the filter-count event — a local
                // alias carries the type the text hid; a relay call the
                // compilation cannot bind is refused rather than trusted.
                IMethodSymbol[] relayMethods = [.. Candidates(model.GetSymbolInfo(call)).OfType<IMethodSymbol>()
                    .Where(method => method.ContainingType.ToDisplayString() == "SlateWindows.Graph.GraphAnnouncer")];
                bool unboundRelayCall = relayMethods.Length == 0
                    && CalleeName(call).StartsWith("Announce", StringComparison.Ordinal)
                    && !Candidates(model.GetSymbolInfo(call)).Any();
                bool carriesCount = call.ArgumentList.Arguments.Any(argument => IsTheFilterCountEvent(model.GetTypeInfo(argument.Expression).Type));
                if (relayMethods.Any(method => method.Name == "AnnounceGatedFilterCount"))
                {
                    gated.Add($"{relative}:{OwnerOf(call)}");
                }
                else if (carriesCount && (relayMethods.Length > 0 || unboundRelayCall))
                {
                    ungated.Add($"{relative}:{OwnerOf(call)}");
                }
            }
        }
        Assert.Equal(["Graph/GraphDocumentViewModel.cs:AnnounceFilterCountIfEffective"], gated);
        Assert.Empty(ungated);
    }

    /// <summary>The bindings' <c>GraphA11yEvent.GraphFilterCount</c>, by its
    /// bound type (IPB-33).</summary>
    private static bool IsTheFilterCountEvent(ITypeSymbol? type) =>
        type is { Name: "GraphFilterCount", ContainingType.Name: "GraphA11yEvent" };

    /// <summary>B-13: the leaf body is gated on the leaf id, hosts the view
    /// bound to the workspace's public document, and the generic placeholder
    /// collapses for the leaf beside the leaves it names.</summary>
    [Fact]
    public void TheLeafBodyIsGatedOnItsIdAndThePlaceholderRetiresForIt()
    {
        string xaml = File.ReadAllText(Path.Combine(ShellRoot, "MainWindow.xaml"));
        int body = xaml.IndexOf("AutomationProperties.AutomationId=\"ConnectionsLeafBody\"", StringComparison.Ordinal);
        Assert.True(body >= 0, "the leaf body is not in MainWindow.xaml");
        string bodyBlock = xaml.Substring(body, Math.Min(2000, xaml.Length - body));
        Assert.Contains("<DataTrigger Binding=\"{Binding ActiveLeaf.Id}\" Value=\"connections\">", bodyBlock, StringComparison.Ordinal);
        Assert.Contains("<graph:ConnectionsLeafView", bodyBlock, StringComparison.Ordinal);
        Assert.Contains("Model=\"{Binding Connections}\"", bodyBlock, StringComparison.Ordinal);
        // The placeholder's style: a collapse trigger for the leaf id.
        int placeholder = xaml.IndexOf("This panel is docked and ready for its feature surface.", StringComparison.Ordinal);
        Assert.True(placeholder >= 0, "the generic placeholder is not in MainWindow.xaml");
        string placeholderStyle = xaml.Substring(Math.Max(0, placeholder - 4000), Math.Min(4000, placeholder));
        int trigger = placeholderStyle.LastIndexOf("Value=\"connections\"", StringComparison.Ordinal);
        Assert.True(trigger >= 0, "the placeholder does not collapse for the leaf (B-13)");
        Assert.Contains("<Setter Property=\"Visibility\" Value=\"Collapsed\" />", placeholderStyle.Substring(trigger), StringComparison.Ordinal);
        // The right pane's landmark is the named focus boundary (Term 9).
        Assert.Contains("x:Name=\"RightPaneBorder\"", xaml, StringComparison.Ordinal);
        string window = File.ReadAllText(Path.Combine(ShellRoot, "MainWindow.xaml.cs"));
        Assert.Contains("RightPaneBorder.IsKeyboardFocusWithin", window, StringComparison.Ordinal);
        Assert.Contains("ConnectionsLeafSurface.FocusAnchor()", window, StringComparison.Ordinal);
    }

    /// <summary>B-20: the parity matrix carries the three command rows at
    /// the W6-2 status the generator names, and the W-C matrix carries the
    /// leaf's row (its cells the evidence census's).</summary>
    [Fact]
    public void TheParityMatrixCarriesTheThreeRowsAtTheW62Status()
    {
        string repo = SourceText.RepoRoot();
        string script = File.ReadAllText(Path.Combine(repo, "scripts", "generate-parity-matrix.py"));
        var status = System.Text.RegularExpressions.Regex.Match(
            script, "W6_2_STATUS = \\(\\s*\"([^\"]+)\"\\s*\"([^\"]+)\"\\s*\\)");
        Assert.True(status.Success, "the generator's W6_2_STATUS is not in its two-string shape");
        string expected = status.Groups[1].Value + status.Groups[2].Value;
        string matrix = File.ReadAllText(Path.Combine(repo, "docs", "plans", "18_windows_port", "parity_matrix.md"));
        foreach (string id in new[] { "slate.graph.showConnections", "slate.graph.connectionsDeeper", "slate.graph.connectionsShallower" })
        {
            string? row = matrix.Split('\n').FirstOrDefault(line => line.StartsWith("| `" + id + "`", StringComparison.Ordinal));
            Assert.True(row is not null, $"{id} has no parity row");
            Assert.EndsWith("| " + expected + " |", row.TrimEnd(), StringComparison.Ordinal);
        }
        string wc = File.ReadAllText(Path.Combine(repo, "docs", "plans", "18_windows_port", "w_c_matrix.md"));
        Assert.Contains("| Graph connections leaf (W6-2 PR B) |", wc, StringComparison.Ordinal);
    }

    /// <summary>B-19 (v): the depth's dataflow, across the whole shell —
    /// every write to the leaf's depth storage takes the FFI clamp's result
    /// (through the counting wrapper whose body IS the FFI call), and every
    /// value reaching the clamp is one of the named producers.</summary>
    [Fact]
    public void EveryDepthWriteIsTheFfiClampsResultAndOnlyTheNamedProducersReachIt()
    {
        CSharpSource leaf = CSharpSource.Load("Graph", "ConnectionsLeafViewModel.cs");
        // (1) The wrapper's body is the FFI clamp of its own parameter.
        MethodDeclarationSyntax wrapper = leaf.Root.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Single(m => m.Identifier.ValueText == "ClampDepth");
        string wrapperReturn = CSharpSource.Normalize(
            wrapper.DescendantNodes().OfType<ReturnStatementSyntax>().Single().Expression!);
        Assert.Equal("SlateUniffiMethods.GraphClampConnectionsDepth(requested)", wrapperReturn);

        // (2) Every assignment to `_depth` takes `ClampDepth(...)`'s result —
        // directly or through a local initialised by it in the same method.
        foreach (AssignmentExpressionSyntax assignment in leaf.Root.DescendantNodes().OfType<AssignmentExpressionSyntax>()
            .Where(a => CSharpSource.Normalize(a.Left) == "_depth"))
        {
            string right = CSharpSource.Normalize(assignment.Right);
            bool direct = right.StartsWith("ClampDepth(", StringComparison.Ordinal);
            bool viaLocal = assignment.Ancestors().OfType<MethodDeclarationSyntax>().First()
                .DescendantNodes().OfType<VariableDeclaratorSyntax>()
                .Any(v => v.Identifier.ValueText == right
                    && v.Initializer is not null
                    && CSharpSource.Normalize(v.Initializer.Value).StartsWith("ClampDepth(", StringComparison.Ordinal));
            Assert.True(direct || viaLocal, $"_depth is written from `{right}`, not the clamp's result");
        }

        // (3) The clamp's only caller is SetDepth, with its own parameter.
        var clampCalls = leaf.Root.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Where(c => CSharpSource.Normalize(c.Expression) == "ClampDepth")
            .Select(c => (Owner: OwnerOf(c), Argument: CSharpSource.Normalize(c.ArgumentList.Arguments.Single().Expression)))
            .ToList();
        Assert.Equal([("SetDepth", "requested")], clampCalls);

        // (4) SetDepth's callers, across the shell, pass exactly the named
        // producers: core's minimum in the constructor, `_depth + 1` in
        // Deeper, `_depth - 1` in Shallower, and the view's index
        // conversion; nothing else, and no comparison, conditional,
        // `Math.*` or helper on the way.
        var producers = new List<string>();
        var offenders = new List<string>();
        foreach ((string relative, CSharpSource source) in ShellCompilation.Sources)
        {
            SemanticModel model = ShellCompilation.ModelFor(source);
            foreach (InvocationExpressionSyntax call in source.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (CalleeName(call) != "SetDepth")
                {
                    continue;
                }
                // BOUND (IPB-17): the leaf's SetDepth, whatever the receiver's
                // spelling; a call the compilation binds to nothing is refused.
                ISymbol[] candidates = [.. Candidates(model.GetSymbolInfo(call))];
                if (candidates.Length == 0)
                {
                    offenders.Add($"{relative}:{OwnerOf(call)} calls SetDepth and the compilation binds it to nothing");
                    continue;
                }
                if (!candidates.OfType<IMethodSymbol>().Any(method => method.ContainingType.ToDisplayString() == TheLeafType))
                {
                    continue;
                }
                string argument = CSharpSource.Normalize(call.ArgumentList.Arguments.Single().Expression);
                producers.Add($"{relative}:{OwnerOf(call)}({argument})");
                Assert.False(
                    argument.Contains("Math.", StringComparison.Ordinal) || argument.Contains('?') || argument.Contains('<') || argument.Contains('>'),
                    $"a host clamp or comparison reaches the depth: {argument}");
            }
            // A method-group reference to SetDepth would call it through a
            // delegate, outside the inventory (IPB-17).
            foreach (ExpressionSyntax reference in source.Root.DescendantNodes().OfType<ExpressionSyntax>())
            {
                if (GraphAnnouncerCensus.IsAMethodGroupReference(reference)
                    && Candidates(model.GetSymbolInfo(reference)).OfType<IMethodSymbol>()
                        .Any(method => method.Name == "SetDepth" && method.ContainingType.ToDisplayString() == TheLeafType))
                {
                    offenders.Add($"{relative}:{OwnerOf(reference)} references SetDepth as a method group");
                }
            }
        }
        Assert.True(offenders.Count == 0, "the depth is reached other than by a bound call: " + string.Join("; ", offenders));
        string[] allowed =
        [
            "Graph/ConnectionsLeafViewModel.cs:<ctor>(GraphCoreConstants.Once.ConnectionsDepthMin)",
            // Normalised text carries no spaces.
            "Graph/ConnectionsLeafViewModel.cs:Deeper(_depth+1)",
            "Graph/ConnectionsLeafViewModel.cs:Shallower(_depth-1)",
            // The view's ONE producer: the depth control's selected index
            // converted (codex post-implementation pass 2, IPB-10: the view
            // was admitted by path, so any literal in it passed).
            "Graph/ConnectionsLeafView.cs:OnDepthSelectionChanged(checked((uint)(_depth.SelectedIndex+1)))",
        ];
        string[] unexpected = [.. producers.Where(p => !allowed.Contains(p))];
        Assert.True(unexpected.Length == 0, "an unnamed producer reaches the depth: " + string.Join("; ", unexpected));
        foreach (string name in allowed)
        {
            Assert.Contains(name, producers);
        }
        // No `Math.Clamp`/`Min`/`Max` on a depth in the graph sources or the
        // leaf's partial (the reading view's LIST depth is another word).
        foreach ((string relative, CSharpSource source) in ShellSources())
        {
            if (!relative.StartsWith("Graph/", StringComparison.Ordinal) && relative != "WorkspaceViewModel.Connections.cs")
            {
                continue;
            }
            foreach (InvocationExpressionSyntax call in source.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                string callee = CSharpSource.Normalize(call.Expression);
                if (callee is "Math.Clamp" or "Math.Min" or "Math.Max"
                    && call.ArgumentList.ToString().Contains("Depth", StringComparison.OrdinalIgnoreCase))
                {
                    Assert.Fail($"{relative}:{OwnerOf(call)} clamps a depth host-side: {call}");
                }
            }
        }
    }
}
