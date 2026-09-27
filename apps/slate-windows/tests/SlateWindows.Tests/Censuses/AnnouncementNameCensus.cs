// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SlateWindows.Tests.Censuses;

/// <summary>
/// W7-7 PR 3 (#1246, R-4; codex PR 3 round 8, owner decision OD-9): one
/// spoken-name authority per item kind. An item a list names among its
/// siblings — a tab in its group, a Base view, a saved query, a recent vault —
/// is told apart there by the sibling rule; an announcement that names the
/// same item must speak the SAME composed name, or two items the list reads
/// apart are announced alike (round 8 found five such routes). So every
/// <c>new A11yEvent.…(…)</c> in the shell's C# is read for the raw member an
/// item of those kinds is named by — a tab's Title, a view's Name, a saved
/// query's Name, a recent vault's DisplayName — through locals, pattern
/// variables and the parameters of the method that builds the event (its
/// callers' arguments are read in turn). A raw member is allowed only inside
/// a call to its kind's authority, which composes it. (The grid row, the
/// fifth kind, is held by <see cref="ItemContainerNameCensus.EveryGridBindNamesItsRows"/>:
/// a row move that speaks the row's name speaks the grid's final composed
/// name.)
/// </summary>
public sealed class AnnouncementNameCensus
{
    /// <summary>The raw members each kind's items are named by, and the
    /// authority that must compose them for speech. A kind a sibling list
    /// adds joins this table.</summary>
    internal static readonly (string Type, string Member, string Authority)[] RawNames =
    [
        ("SlateWindows.WorkspaceTabViewModel", "Title", "WorkspaceGroupViewModel.SpokenNameOf"),
        ("SlateWindows.Bases.BaseDocumentViewModel", "ActiveViewName", "BaseDocumentViewModel.ActiveViewSpokenName"),
        ("uniffi.slate_uniffi.BaseViewSummary", "Name", "BaseDocumentViewModel.ActiveViewSpokenName"),
        ("uniffi.slate_uniffi.SavedQuerySummary", "Name", "WorkspaceViewModel.SavedQuerySpokenName"),
        ("uniffi.slate_uniffi.SavedQuery", "Name", "WorkspaceViewModel.SavedQuerySpokenName"),
        ("SlateWindows.RecentVault", "DisplayName", "RecentVault.SpokenName"),
        ("SlateWindows.VaultLifecycleViewModel", "VaultDisplayName", "RecentVault.SpokenName"),
    ];

    /// <summary>The authorities, as "Type.Method": a raw name inside a call
    /// to one of them is that authority's to compose.</summary>
    internal static readonly string[] Authorities =
    [
        "SiblingNames.Compose",
        "SiblingNames.SpokenAmong",
        "WorkspaceGroupViewModel.SpokenNameOf",
        "RecentVault.SpokenName",
        "WorkspaceViewModel.SavedQuerySpokenName",
        "VaultLifecycleViewModel.RecentVaultSpokenName",
    ];

    [Fact]
    public void EveryAnnouncementNamesASiblingItemThroughItsAuthority()
    {
        var offenders = new List<string>();
        int events = 0;
        var callers = new Lazy<ILookup<ISymbol, (ArgumentListSyntax Arguments, SemanticModel Model)>>(Callers);
        foreach ((string file, CSharpSource source) in ShellCompilation.Sources)
        {
            SemanticModel model = ShellCompilation.ModelFor(source);
            foreach (BaseObjectCreationExpressionSyntax creation in source.Root.DescendantNodes().OfType<BaseObjectCreationExpressionSyntax>())
            {
                if (model.GetTypeInfo(creation).Type is not INamedTypeSymbol
                    {
                        ContainingType: { Name: "A11yEvent" } container,
                    } eventType
                    || container.ContainingNamespace.ToDisplayString() != "uniffi.slate_uniffi"
                    || creation.ArgumentList is not { } arguments)
                {
                    continue;
                }
                events++;
                int line = creation.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                foreach (ArgumentSyntax argument in arguments.Arguments)
                {
                    foreach (string raw in RawNamesIn(argument.Expression, model, callers, [], depth: 0))
                    {
                        offenders.Add($"{file}:{line}: A11yEvent.{eventType.Name}({argument}) names an item by {raw}");
                    }
                }
            }
        }

        Assert.True(events > 300, $"the census found only {events} announcement constructions — the scan is broken");
        Assert.True(
            offenders.Count == 0,
            "announcements that name a sibling item by its raw name, not through its kind's spoken-name "
            + "authority (the name its list reads):\n  " + string.Join("\n  ", offenders.Distinct()));
    }

    /// <summary>Every raw name <paramref name="expression"/> can carry, as
    /// "Type.Member (use Authority)", following locals, pattern variables
    /// and parameters; nothing inside an authority's call.</summary>
    private static IEnumerable<string> RawNamesIn(
        SyntaxNode expression,
        SemanticModel model,
        Lazy<ILookup<ISymbol, (ArgumentListSyntax Arguments, SemanticModel Model)>> callers,
        HashSet<ISymbol> followed,
        int depth)
    {
        if (depth > 8)
        {
            yield break;
        }
        var pending = new Stack<SyntaxNode>();
        pending.Push(expression);
        while (pending.Count > 0)
        {
            SyntaxNode node = pending.Pop();
            if (node is InvocationExpressionSyntax invocation
                && model.GetSymbolInfo(invocation).Symbol is IMethodSymbol invoked
                && Authorities.Contains($"{invoked.ContainingType.Name}.{invoked.Name}"))
            {
                continue;
            }
            if (node is AnonymousFunctionExpressionSyntax)
            {
                // A lambda's body is its own speech, read where it is built.
                continue;
            }
            if (node is IdentifierNameSyntax or MemberAccessExpressionSyntax)
            {
                ISymbol? symbol = model.GetSymbolInfo(node).Symbol;
                if (symbol is IPropertySymbol property
                    && RawNames.FirstOrDefault(raw =>
                        raw.Member == property.Name
                        && raw.Type == property.ContainingType.OriginalDefinition.ToDisplayString()) is { Type: not null } hit)
                {
                    yield return $"{hit.Type}.{hit.Member} (speak {hit.Authority})";
                    continue;
                }
                if (node is IdentifierNameSyntax && symbol is ILocalSymbol local && followed.Add(local))
                {
                    foreach (SyntaxNode source in SourcesOfLocal(local, node, model))
                    {
                        foreach (string raw in RawNamesIn(source, model, callers, followed, depth + 1))
                        {
                            yield return raw;
                        }
                    }
                    continue;
                }
                if (node is IdentifierNameSyntax
                    && symbol is IParameterSymbol { ContainingSymbol: IMethodSymbol { MethodKind: not MethodKind.AnonymousFunction } method } parameter
                    && followed.Add(parameter))
                {
                    foreach ((ArgumentListSyntax arguments, SemanticModel callerModel) in callers.Value[method.OriginalDefinition])
                    {
                        if (ArgumentFor(arguments, parameter) is { } argument)
                        {
                            foreach (string raw in RawNamesIn(argument, callerModel, callers, followed, depth + 1))
                            {
                                yield return raw;
                            }
                        }
                    }
                    continue;
                }
                if (node is MemberAccessExpressionSyntax && symbol is IPropertySymbol or IFieldSymbol)
                {
                    // Another member's value: what it reads is not its
                    // receiver's (a dialog's FileName is not the dialog).
                    continue;
                }
            }
            foreach (SyntaxNode child in node.ChildNodes())
            {
                pending.Push(child);
            }
        }
    }

    /// <summary>What a local can hold: its initializer, every assignment to
    /// it, and the tested expression of the pattern that declares it — read
    /// across the whole member that declares it, so a lambda that captures
    /// the local (a continuation announcing a name captured at dispatch) is
    /// read too.</summary>
    private static IEnumerable<SyntaxNode> SourcesOfLocal(ILocalSymbol local, SyntaxNode use, SemanticModel model)
    {
        SyntaxNode declaration = local.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() ?? use;
        SyntaxNode? body = declaration.AncestorsAndSelf().FirstOrDefault(ancestor =>
            ancestor is BaseMethodDeclarationSyntax or AccessorDeclarationSyntax or LocalFunctionStatementSyntax
                or PropertyDeclarationSyntax);
        if (body is null)
        {
            yield break;
        }
        bool Is(ISymbol? symbol) => SymbolEqualityComparer.Default.Equals(symbol, local);
        foreach (SyntaxNode node in body.DescendantNodes())
        {
            switch (node)
            {
                case VariableDeclaratorSyntax { Initializer: { } initializer } declarator
                    when Is(model.GetDeclaredSymbol(declarator)):
                    yield return initializer.Value;
                    break;
                case AssignmentExpressionSyntax { Left: IdentifierNameSyntax target } assignment
                    when Is(model.GetSymbolInfo(target).Symbol):
                    yield return assignment.Right;
                    break;
                case SingleVariableDesignationSyntax designation
                    when Is(model.GetDeclaredSymbol(designation))
                    && designation.Ancestors().OfType<IsPatternExpressionSyntax>().FirstOrDefault() is { } test:
                    yield return test.Expression;
                    break;
            }
        }
    }

    /// <summary>The argument a call passes for <paramref name="parameter"/>:
    /// by name, else by position.</summary>
    private static ExpressionSyntax? ArgumentFor(ArgumentListSyntax arguments, IParameterSymbol parameter)
    {
        if (arguments.Arguments.FirstOrDefault(argument => argument.NameColon?.Name.Identifier.ValueText == parameter.Name) is { } named)
        {
            return named.Expression;
        }
        return parameter.Ordinal < arguments.Arguments.Count && arguments.Arguments[parameter.Ordinal].NameColon is null
            ? arguments.Arguments[parameter.Ordinal].Expression
            : null;
    }

    /// <summary>
    /// R-4's runtime witness (codex PR 3 round 8, the low finding): the
    /// item-name census — no item named by a .NET type name or a record dump
    /// — runs in EVERY shell journey, through <c>AssertAxeClean</c> (which runs
    /// it before its scan) or <c>AssertItemNamesAreSpeakable</c> at a
    /// representative state. A journey is a fact of the shell accessibility
    /// suite that launches the shell (<c>StartShellProcess</c>, or a process
    /// started from <c>SlateWindowsExe()</c>); its calls are followed through
    /// the suite's own helpers by name. The files tree, palette, sheet-fence
    /// and announcements journeys (and three more) never ran it. The grid
    /// fixture host's probes are not the shell; its matrix journey scans it.
    /// </summary>
    [Fact]
    public void EveryShellJourneyRunsTheItemNameCensus()
    {
        string suite = Path.Combine(SourceText.RepoRoot(), "apps", "slate-windows", "tests", "SlateWindows.AccessibilityTests");
        var bodies = new Dictionary<string, List<SyntaxNode>>(StringComparer.Ordinal);
        var journeys = new List<(string File, string Name)>();
        foreach (string file in Directory.EnumerateFiles(suite, "*.cs", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(suite, file).Replace('\\', '/');
            if (relative.StartsWith("obj/", StringComparison.Ordinal) || relative.StartsWith("bin/", StringComparison.Ordinal))
            {
                continue;
            }
            CompilationUnitSyntax root = CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetCompilationUnitRoot();
            foreach (MethodDeclarationSyntax method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                if (method.Parent is not ClassDeclarationSyntax { Identifier.ValueText: "ShellAccessibilityTests" })
                {
                    continue;
                }
                string name = method.Identifier.ValueText;
                if (!bodies.TryGetValue(name, out List<SyntaxNode>? list))
                {
                    bodies[name] = list = [];
                }
                list.Add(method);
                if (method.AttributeLists.SelectMany(attributes => attributes.Attributes)
                    .Any(attribute => attribute.Name.ToString() is "Fact" or "Theory" or "Xunit.Fact" or "Xunit.Theory"))
                {
                    journeys.Add((relative, name));
                }
            }
        }

        // Every name a method invokes, and whether it starts the shell.
        static IEnumerable<string> Invoked(SyntaxNode body) =>
            body.DescendantNodes().OfType<InvocationExpressionSyntax>().Select(invocation => invocation.Expression switch
            {
                IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
                MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
                GenericNameSyntax generic => generic.Identifier.ValueText,
                _ => string.Empty,
            });
        bool Reaches(string start, Func<string, bool> target)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal) { start };
            var pending = new Queue<string>([start]);
            while (pending.Count > 0)
            {
                string name = pending.Dequeue();
                if (!bodies.TryGetValue(name, out List<SyntaxNode>? methods))
                {
                    continue;
                }
                foreach (string invoked in methods.SelectMany(Invoked))
                {
                    if (target(invoked))
                    {
                        return true;
                    }
                    if (seen.Add(invoked))
                    {
                        pending.Enqueue(invoked);
                    }
                }
            }
            return false;
        }

        int launching = 0;
        var offenders = new List<string>();
        foreach ((string file, string name) in journeys.Distinct())
        {
            if (!Reaches(name, invoked => invoked is "StartShellProcess" or "SlateWindowsExe"))
            {
                continue;
            }
            launching++;
            if (!Reaches(name, invoked => invoked is "AssertAxeClean" or "AssertItemNamesAreSpeakable"))
            {
                offenders.Add($"{file}: {name} launches the shell and never runs the item-name census");
            }
        }

        Assert.True(launching >= 35, $"only {launching} shell journeys found — the scan is broken");
        Assert.True(
            offenders.Count == 0,
            "shell journeys that never run the runtime item-name census (AssertAxeClean or "
            + "AssertItemNamesAreSpeakable at a representative state):\n  " + string.Join("\n  ", offenders));
    }

    /// <summary>Every call site in the shell, by the method it calls.</summary>
    private static ILookup<ISymbol, (ArgumentListSyntax Arguments, SemanticModel Model)> Callers()
    {
        var calls = new List<(IMethodSymbol Method, ArgumentListSyntax Arguments, SemanticModel Model)>();
        foreach ((string _, CSharpSource source) in ShellCompilation.Sources)
        {
            SemanticModel model = ShellCompilation.ModelFor(source);
            foreach (InvocationExpressionSyntax invocation in source.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (model.GetSymbolInfo(invocation).Symbol is IMethodSymbol method)
                {
                    calls.Add((method.OriginalDefinition, invocation.ArgumentList, model));
                }
            }
        }
        return calls.ToLookup(
            call => call.Method,
            call => (call.Arguments, call.Model),
            SymbolEqualityComparer.Default);
    }
}
