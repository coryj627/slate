// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// W7-7 PR 8 (#1253, contract R-10; owner decision OD-12's one entry): every
// editor landing request is created in ONE place, the shell's landing entry,
// which refuses under an open modal surface and puts the window's slot over a
// canvas or graph request BEFORE the request exists. Codex PR 8 round 8 found
// the workspace funnel still raising the documents' requests itself, ahead of
// the window, so a realized surface could seat or refuse them unowned — a
// fact about outcomes could not see the site, so this census reads the
// shipping sources.
//
// WB-15 (#1313): a census of the CALLERS alone is passed by a differently
// named landing — a request built or written anywhere else. So beside the
// admitted callers it pins, on the shell's bound compilation, the three other
// roles the code has: the documents that BUILD a request (publishers), the
// lifecycle that CLEARS it (clearers, with each property's backing setter),
// and the surfaces that REMEMBER it (relays) — and holds the AR-42
// restorations to callers inside their own surfaces.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SlateWindows.Tests.Censuses;

public sealed class EditorLandingEntryCensus
{
    /// <summary>The methods allowed to create a landing request: the entry's
    /// document half and its reading arm, and the two surface-owned
    /// restorations contract 40 scopes out (AR-42), each raised only for a
    /// reader already inside its surface.</summary>
    private static readonly string[] Permitted =
    [
        "MainWindow.ShellRegions.cs:LandDocument",
        "MainWindow.xaml.cs:FocusEditorPane",
        "Canvas/CanvasSurfaceView.cs:FocusProjection",
        "Graph/GraphSurfaceView.cs:RequestProjectionFocus",
    ];

    /// <summary>The only members that build an editor landing request — each
    /// document's own <c>RequestFocusLanding</c>, which the admitted callers
    /// invoke — each exactly once.</summary>
    private static readonly string[] Publishers =
    [
        "Canvas/CanvasDocumentViewModel.cs:RequestFocusLanding",
        "Graph/GraphDocumentViewModel.cs:RequestFocusLanding",
    ];

    /// <summary>The documents' lifecycle: members that write a document's
    /// request and may write only <c>null</c>.</summary>
    private static readonly string[] Clearers =
    [
        "Canvas/CanvasDocumentViewModel.cs:EndFocusLanding",
        "Canvas/CanvasDocumentViewModel.cs:DropRequestsAddressedOutside",
        "Canvas/CanvasDocumentViewModel.cs:Shutdown",
        "Graph/GraphDocumentViewModel.cs:EndFocus",
    ];

    /// <summary>Each <c>FocusRequest</c> property's own setter, which passes
    /// its field by <c>ref</c> to the document's change helper.</summary>
    private static readonly string[] Backing =
    [
        "Canvas/CanvasDocumentViewModel.cs:FocusRequest",
        "Graph/GraphDocumentViewModel.cs:FocusRequest",
    ];

    /// <summary>The surfaces' memory of the document's request: assigned only
    /// <c>null</c> or a read of a document's <c>FocusRequest</c> — they build
    /// and publish none.</summary>
    private static readonly string[] Relays =
    [
        "Canvas/CanvasSurfaceView.cs:_deferredRestoration",
        "Graph/GraphSurfaceView.cs:_deferredRestoration",
    ];

    private static readonly string[] RequestTypes = ["CanvasFocusRequest", "GraphFocusRequest"];

    private static readonly string[] DocumentTypes = ["CanvasDocumentViewModel", "GraphDocumentViewModel"];

    /// <summary>The AR-42 restorations, by the folder their callers must sit
    /// in: each surface's method and the presenter interface member through
    /// which its navigator reaches it.</summary>
    private static readonly (string Type, string Method, string Folder)[] Ar42Methods =
    [
        ("CanvasSurfaceView", "FocusProjection", "Canvas/"),
        ("ICanvasSurfacePresenter", "FocusProjection", "Canvas/"),
        ("GraphSurfaceView", "RequestProjectionFocus", "Graph/"),
        ("IGraphSurfacePresenter", "RequestProjectionFocus", "Graph/"),
    ];

    [Fact]
    public void EveryLandingRequestIsCreatedByTheShellsOneEntry()
    {
        List<string> offenders = CallerOffenders(ShellUnits());
        Assert.True(offenders.Count == 0, string.Join("\n", offenders));
    }

    [Fact]
    public void TheEntryOwnsADocumentRequestBeforeRaisingIt()
    {
        MethodDeclarationSyntax land = CSharpSource.Load("MainWindow.ShellRegions.cs").Method("LandDocument");
        InvocationExpressionSyntax[] holds =
            [.. land.DescendantNodes().OfType<InvocationExpressionSyntax>().Where(invocation => Named(invocation, "Hold"))];
        InvocationExpressionSyntax[] raises = [.. Creations(land)];
        Assert.True(holds.Length == 1, $"LandDocument holds the landing {holds.Length} times; the entry holds it once.");
        Assert.True(raises.Length > 0, "LandDocument raises no request, so it is no entry.");
        Assert.All(
            raises,
            raise => Assert.True(
                holds[0].SpanStart < raise.SpanStart,
                $"LandDocument raises a request (line {raise.GetLocation().GetLineSpan().StartLinePosition.Line + 1}) "
                + "before the window's slot holds the landing: a surface could seat or refuse it unowned."));
    }

    [Fact]
    public void TheEntryCreatesNothingUnderAnOpenModal()
    {
        MethodDeclarationSyntax entry = CSharpSource.Load("MainWindow.xaml.cs").Method("FocusEditorPane");
        StatementSyntax first = Assert.IsAssignableFrom<StatementSyntax>(entry.Body!.Statements[0]);
        var guard = Assert.IsType<IfStatementSyntax>(first);
        Assert.Contains("OpenModalSurface", guard.Condition.ToString(), StringComparison.Ordinal);
        // A modal loop over the shell (#1275's monitor) holds the keys too.
        Assert.Contains("_modalLoops.IsModalLoopActive", guard.Condition.ToString(), StringComparison.Ordinal);
        Assert.NotEmpty(guard.Statement.DescendantNodesAndSelf().OfType<ReturnStatementSyntax>());
    }

    /// <summary>WB-15 (#1313): a request is built only by its document's
    /// <c>RequestFocusLanding</c>, cleared only by the document's lifecycle,
    /// stored behind the property's own setter, and remembered only by the
    /// surfaces' relay fields — a renamed landing anywhere else fails
    /// here.</summary>
    [Fact]
    public void EveryLandingRequestIsBuiltByItsDocumentAndWrittenOnlyByAPinnedRole()
    {
        List<string> offenders = RequestOffenders(ShellCompilation.Compilation, CompilationUnits());
        Assert.True(
            offenders.Count == 0,
            "an editor landing request is built or written outside its pinned roles (OD-12's one entry):\n  "
            + string.Join("\n  ", offenders));
    }

    /// <summary>WB-15 (#1313): the AR-42 restorations are scoped out of the
    /// entry because each is raised only for a reader already inside its
    /// surface — so every call sits in that surface's own folder, a call
    /// through the presenter interface included.</summary>
    [Fact]
    public void TheAr42MethodsAreCalledOnlyFromInsideTheirSurfaces()
    {
        List<string> offenders = Ar42CallOffenders(ShellCompilation.Compilation, CompilationUnits());
        Assert.True(
            offenders.Count == 0,
            "an AR-42 restoration is called from outside its surface, where the entry's checks do not hold:\n  "
            + string.Join("\n  ", offenders));
    }

    /// <summary>The detectors catch each bypass they exist for: an
    /// unauthorized construction (three forms), an unauthorized write, a
    /// clearer that writes a request, an unpinned request member, an
    /// unauthorized call, and an AR-42 call from outside its
    /// surface.</summary>
    [Theory]
    [InlineData("construction-renamed")]
    [InlineData("construction-target-typed")]
    [InlineData("construction-with-copy")]
    [InlineData("write-outside-role")]
    [InlineData("clearer-writes-a-request")]
    [InlineData("unpinned-request-member")]
    [InlineData("call-outside-admission")]
    [InlineData("ar42-call-outside-surface")]
    [InlineData("write-by-deconstruction")]
    [InlineData("write-by-ref-local")]
    [InlineData("request-in-a-container")]
    public void EachLandingDetectorCatchesItsBypass(string bypass)
    {
        CSharpCompilation compilation = ShellCompilation.Compilation;
        List<(string Relative, SyntaxTree Tree)> units = CompilationUnits();
        Assert.Empty(RequestOffenders(compilation, units));
        Assert.Empty(Ar42CallOffenders(compilation, units));
        Assert.Empty(CallerOffenders(ShellUnits()));

        (List<string> found, string planted) = bypass switch
        {
            "construction-renamed" => (Requests(InsertMember(
                "Canvas/CanvasDocumentViewModel.cs",
                "internal void PublishLanding(object owner) => FocusRequest = new CanvasFocusRequest(owner, null);")), "PublishLanding"),
            "construction-target-typed" => (Requests(AddFile(
                "Other.cs", "class Other { GraphFocusRequest M(object owner) => new(owner); }")), "Other.cs:M"),
            "construction-with-copy" => (Requests(AddFile(
                "Other.cs", "class Other { CanvasFocusRequest M(CanvasFocusRequest request) => request with { NodeId = \"x\" }; }")), "Other.cs:M"),
            "write-outside-role" => (Requests(InsertMember(
                "Graph/GraphDocumentViewModel.cs", "internal void Forget() => FocusRequest = null;")), "Forget"),
            "clearer-writes-a-request" => (Requests(Replace(
                "Graph/GraphDocumentViewModel.cs", "FocusRequest = null;", "FocusRequest = request;")), "EndFocus"),
            "unpinned-request-member" => (Requests(AddFile(
                "Other.cs",
                "class Other { private CanvasFocusRequest? _stash; void M(CanvasDocumentViewModel document) => _stash = document.FocusRequest; }")),
                "_stash"),
            "call-outside-admission" => (CallerOffenders(ShellUnits().Append((
                "MainWindow.Planted.cs",
                Parse("namespace SlateWindows;\npartial class MainWindow { void Nudge(CanvasDocumentViewModel canvas, object tab) => canvas.RequestFocusLanding(tab); }")))),
                "MainWindow.Planted.cs:Nudge"),
            "ar42-call-outside-surface" => (Ar42Calls(AddFile(
                "MainWindow.Planted.cs",
                "partial class MainWindow { void Poke(ICanvasSurfacePresenter presenter) => presenter.FocusProjection(); }")),
                "MainWindow.Planted.cs:Poke"),
            "write-by-deconstruction" => (Requests(InsertMember(
                "Graph/GraphDocumentViewModel.cs", "internal void Swap(GraphFocusRequest request) => (FocusRequest, _) = (request, 0);")), "Swap"),
            "write-by-ref-local" => (Requests(InsertMember(
                "Graph/GraphDocumentViewModel.cs",
                "internal void Alias(GraphFocusRequest request) { ref GraphFocusRequest? held = ref _focusRequest; held = request; }")), "Alias"),
            "request-in-a-container" => (Requests(AddFile(
                "Other.cs",
                "class Other { private readonly System.Collections.Generic.List<CanvasFocusRequest?> _kept = []; void M(CanvasDocumentViewModel document) => _kept.Add(document.FocusRequest); }")),
                "_kept"),
            _ => throw new ArgumentOutOfRangeException(nameof(bypass), bypass, "no such row"),
        };

        Assert.True(
            found.Any(offender => offender.Contains(planted, StringComparison.Ordinal)),
            $"the census missed a planted {bypass} bypass ({planted}); it reported:\n  " + string.Join("\n  ", found));
    }

    /// <summary>The kept caller check (W7-7 PR 8): every
    /// <c>RequestFocusLanding</c> call sits in a <see cref="Permitted"/>
    /// member, and every permitted member makes one.</summary>
    internal static List<string> CallerOffenders(IEnumerable<(string Relative, SyntaxNode Root)> units)
    {
        var sites = new List<string>();
        foreach ((string relative, SyntaxNode root) in units)
        {
            foreach (InvocationExpressionSyntax invocation in Creations(root))
            {
                sites.Add($"{relative}:{MemberName(invocation)}");
            }
        }

        var offenders = new List<string>();
        string[] foreign = [.. sites.Where(site => !Permitted.Contains(site, StringComparer.Ordinal)).Distinct()];
        if (foreign.Length > 0)
        {
            offenders.Add(
                "an editor landing request is created outside the shell's one entry (OD-12) — a request raised "
                + "there is published before the window's slot owns it and without its modal check: "
                + string.Join("; ", foreign));
        }

        string[] unused = [.. Permitted.Where(site => !sites.Contains(site, StringComparer.Ordinal))];
        if (unused.Length > 0)
        {
            offenders.Add("a permitted creation site creates nothing, so this census exempts nothing there: " + string.Join("; ", unused));
        }

        return offenders;
    }

    /// <summary>Every construction and write of a landing request, bound
    /// through <paramref name="compilation"/>, against the pinned roles; and
    /// every pin, against what its member does.</summary>
    internal static List<string> RequestOffenders(CSharpCompilation compilation, IEnumerable<(string Relative, SyntaxTree Tree)> units)
    {
        var offenders = new List<string>();
        var constructions = new Dictionary<string, int>(StringComparer.Ordinal);
        var writers = new HashSet<string>(StringComparer.Ordinal);
        foreach ((string relative, SyntaxTree tree) in units)
        {
            SemanticModel model = compilation.GetSemanticModel(tree);
            SyntaxNode root = tree.GetRoot();

            // A store that holds a request: a field, or a property with a
            // setter. A getter-only computed property stores nothing.
            foreach (SyntaxNode declaration in root.DescendantNodes().Where(node => node is VariableDeclaratorSyntax or PropertyDeclarationSyntax))
            {
                ISymbol? store = model.GetDeclaredSymbol(declaration);
                // A store whose type only CONTAINS a request — a list, an
                // array, a tuple — holds one as surely (WB-15 red team).
                bool stores = store switch
                {
                    IFieldSymbol field => IsRequest(field.Type) || ContainsRequest(field.Type),
                    IPropertySymbol property => (IsRequest(property.Type) || ContainsRequest(property.Type)) && property.SetMethod is not null,
                    _ => false,
                };
                if (stores && !IsDocumentRequest(store!) && !Relays.Contains($"{relative}:{store!.Name}", StringComparer.Ordinal))
                {
                    offenders.Add($"{relative}:{store!.Name} holds a landing request in no pinned role");
                }
            }

            foreach (ExpressionSyntax creation in root.DescendantNodes().OfType<ExpressionSyntax>()
                .Where(node => node is BaseObjectCreationExpressionSyntax or WithExpressionSyntax))
            {
                if (!IsRequest(model.GetTypeInfo(creation).Type))
                {
                    continue;
                }

                string member = $"{relative}:{MemberName(creation)}";
                constructions[member] = constructions.GetValueOrDefault(member) + 1;
                if (!Publishers.Contains(member, StringComparer.Ordinal))
                {
                    offenders.Add($"{member} builds a landing request — only its document's RequestFocusLanding builds one");
                }
            }

            foreach (AssignmentExpressionSyntax assignment in root.DescendantNodes().OfType<AssignmentExpressionSyntax>()
                .Where(node => node.IsKind(SyntaxKind.SimpleAssignmentExpression) || node.IsKind(SyntaxKind.CoalesceAssignmentExpression)))
            {
                if (model.GetSymbolInfo(assignment.Left).Symbol is { } target && IsRequestStore(target))
                {
                    CheckWrite(model, relative, assignment, target, assignment.Right, byRef: false, writers, offenders);
                }
            }

            foreach (ArgumentSyntax argument in root.DescendantNodes().OfType<ArgumentSyntax>()
                .Where(node => node.RefKindKeyword.IsKind(SyntaxKind.RefKeyword) || node.RefKindKeyword.IsKind(SyntaxKind.OutKeyword)))
            {
                if (argument.Parent?.Parent is InvocationExpressionSyntax or BaseObjectCreationExpressionSyntax
                    && model.GetSymbolInfo(argument.Expression).Symbol is { } target && IsRequestStore(target))
                {
                    CheckWrite(model, relative, argument, target, value: null, byRef: true, writers, offenders);
                }
            }

            // A deconstruction writes each element of its tuple, and a ref
            // local or ref return aliases the store for a later write: both
            // write the request with nothing the roles can read (WB-15 red
            // team), so they are writes in no role.
            foreach (ExpressionSyntax element in root.DescendantNodes()
                .OfType<AssignmentExpressionSyntax>()
                .Where(assignment => assignment.Left is TupleExpressionSyntax)
                .SelectMany(assignment => ((TupleExpressionSyntax)assignment.Left).DescendantNodes().OfType<ArgumentSyntax>())
                .Select(argument => argument.Expression))
            {
                if (model.GetSymbolInfo(element).Symbol is { } target && IsRequestStore(target))
                {
                    offenders.Add($"{relative}:{element.GetLocation().GetLineSpan().StartLinePosition.Line + 1} ({MemberName(element)}) "
                        + $"writes {target.Name} by deconstruction, which no pinned role does");
                }
            }

            foreach (RefExpressionSyntax alias in root.DescendantNodes().OfType<RefExpressionSyntax>())
            {
                if (model.GetSymbolInfo(alias.Expression).Symbol is { } target && IsRequestStore(target))
                {
                    offenders.Add($"{relative}:{alias.GetLocation().GetLineSpan().StartLinePosition.Line + 1} ({MemberName(alias)}) "
                        + $"takes a ref to {target.Name}, which no pinned role does");
                }
            }
        }

        foreach (string publisher in Publishers)
        {
            if (constructions.GetValueOrDefault(publisher) != 1)
            {
                offenders.Add($"{publisher} is pinned as a publisher but builds {constructions.GetValueOrDefault(publisher)} request(s), not one");
            }
        }

        foreach (string pinned in Clearers.Concat(Backing).Concat(Relays))
        {
            if (!writers.Contains(pinned))
            {
                offenders.Add($"{pinned} is pinned but never writes a landing request — a stale pin exempts nothing");
            }
        }

        return offenders;
    }

    private static void CheckWrite(
        SemanticModel model, string relative, SyntaxNode write, ISymbol target, ExpressionSyntax? value, bool byRef,
        HashSet<string> writers, List<string> offenders)
    {
        string member = $"{relative}:{MemberName(write)}";
        string where = $"{relative}:{write.GetLocation().GetLineSpan().StartLinePosition.Line + 1} ({MemberName(write)})";
        if (IsDocumentRequest(target))
        {
            bool built = value is BaseObjectCreationExpressionSyntax && IsRequest(model.GetTypeInfo(value).Type);
            bool cleared = value is LiteralExpressionSyntax { RawKind: (int)SyntaxKind.NullLiteralExpression };
            bool backing = byRef && Backing.Contains(member, StringComparer.Ordinal);
            if ((Publishers.Contains(member, StringComparer.Ordinal) && built)
                || (Clearers.Contains(member, StringComparer.Ordinal) && cleared)
                || backing)
            {
                _ = writers.Add(member);
                return;
            }

            offenders.Add($"{where} writes {target.ContainingType.Name}.{target.Name} outside its pinned roles — a publisher "
                + "writes the request it built, a clearer writes null, and only the property's setter passes the field by ref");
            return;
        }

        string relay = $"{relative}:{target.Name}";
        if (Relays.Contains(relay, StringComparer.Ordinal))
        {
            bool cleared = value is LiteralExpressionSyntax { RawKind: (int)SyntaxKind.NullLiteralExpression };
            bool relayed = value is not null && model.GetSymbolInfo(value).Symbol is IPropertySymbol { Name: "FocusRequest" } read
                && DocumentTypes.Contains(read.ContainingType.Name, StringComparer.Ordinal);
            if (!byRef && (cleared || relayed))
            {
                _ = writers.Add(relay);
                return;
            }

            offenders.Add($"{where} writes the relay {relay} with something other than null or a document's FocusRequest");
            return;
        }

        offenders.Add($"{where} writes {target.Name}, a landing request in no pinned role");
    }

    /// <summary>Every call of an AR-42 restoration — through the surface or
    /// the presenter interface, invoked or taken as a method group — sits
    /// under its surface's folder; and each restoration has a caller, so the
    /// check is not vacuous.</summary>
    internal static List<string> Ar42CallOffenders(CSharpCompilation compilation, IEnumerable<(string Relative, SyntaxTree Tree)> units)
    {
        var offenders = new List<string>();
        var called = new HashSet<string>(StringComparer.Ordinal);
        foreach ((string relative, SyntaxTree tree) in units)
        {
            SemanticModel model = compilation.GetSemanticModel(tree);
            foreach (SimpleNameSyntax name in tree.GetRoot().DescendantNodes().OfType<SimpleNameSyntax>())
            {
                if (model.GetSymbolInfo(name).Symbol is not IMethodSymbol method)
                {
                    continue;
                }

                IMethodSymbol definition = method.OriginalDefinition;
                foreach ((string type, string methodName, string folder) in Ar42Methods)
                {
                    if (definition.Name != methodName || definition.ContainingType.Name != type)
                    {
                        continue;
                    }

                    _ = called.Add(type);
                    if (!relative.StartsWith(folder, StringComparison.Ordinal))
                    {
                        offenders.Add($"{relative}:{MemberName(name)} calls {type}.{methodName} from outside {folder}");
                    }
                }
            }
        }

        foreach ((string type, string methodName, _) in Ar42Methods)
        {
            if (!called.Contains(type))
            {
                offenders.Add($"no caller of {type}.{methodName} was bound — the census sees nothing there");
            }
        }

        return offenders;
    }

    private static bool IsRequest(ITypeSymbol? type) =>
        type is not null && RequestTypes.Contains(type.WithNullableAnnotation(NullableAnnotation.NotAnnotated).Name, StringComparer.Ordinal);

    private static bool ContainsRequest(ITypeSymbol type) => type switch
    {
        IArrayTypeSymbol array => IsRequest(array.ElementType) || ContainsRequest(array.ElementType),
        INamedTypeSymbol named => named.TypeArguments.Any(argument => IsRequest(argument) || ContainsRequest(argument)),
        _ => false,
    };

    private static bool IsRequestStore(ISymbol symbol) => symbol switch
    {
        IFieldSymbol field => IsRequest(field.Type),
        IPropertySymbol property => IsRequest(property.Type),
        _ => false,
    };

    /// <summary>A document's own request: its <c>FocusRequest</c> property
    /// or <c>_focusRequest</c> field.</summary>
    private static bool IsDocumentRequest(ISymbol symbol) =>
        symbol is IFieldSymbol { Name: "_focusRequest" } or IPropertySymbol { Name: "FocusRequest" }
        && DocumentTypes.Contains(symbol.ContainingType.Name, StringComparer.Ordinal);

    /// <summary>The member a node sits in, as the census names it: a method
    /// or local function's enclosing method, an accessor's property, a
    /// field's own name.</summary>
    private static string? MemberName(SyntaxNode node) =>
        node.Ancestors().OfType<MemberDeclarationSyntax>().FirstOrDefault() switch
        {
            MethodDeclarationSyntax method => method.Identifier.ValueText,
            PropertyDeclarationSyntax property => property.Identifier.ValueText,
            ConstructorDeclarationSyntax => ".ctor",
            FieldDeclarationSyntax field => field.Declaration.Variables.FirstOrDefault()?.Identifier.ValueText,
            _ => null,
        };

    private static IEnumerable<(string Relative, SyntaxNode Root)> ShellUnits()
    {
        string shellRoot = SourceText.ShellSourceRoot();
        foreach (string file in Directory.EnumerateFiles(shellRoot, "*.cs", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(shellRoot, file).Replace('\\', '/');
            if (relative.StartsWith("obj/", StringComparison.Ordinal)
                || relative.StartsWith("bin/", StringComparison.Ordinal)
                || relative.Contains("/obj/", StringComparison.Ordinal))
            {
                continue;
            }

            yield return (relative, CSharpSource.LoadPath(file).Root);
        }
    }

    private static List<(string Relative, SyntaxTree Tree)> CompilationUnits() =>
        [.. ShellCompilation.Sources.Select(source => (source.Relative, source.Source.Root.SyntaxTree))];

    private static List<string> Requests((CSharpCompilation Compilation, List<(string, SyntaxTree)> Units) planted) =>
        RequestOffenders(planted.Compilation, planted.Units);

    private static List<string> Ar42Calls((CSharpCompilation Compilation, List<(string, SyntaxTree)> Units) planted) =>
        Ar42CallOffenders(planted.Compilation, planted.Units);

    /// <summary>The shell with one planted file added.</summary>
    private static (CSharpCompilation, List<(string, SyntaxTree)>) AddFile(string relative, string source)
    {
        SyntaxTree planted = CSharpSyntaxTree.ParseText(
            "using SlateWindows.Canvas;\nusing SlateWindows.Graph;\nnamespace SlateWindows;\n" + source,
            Options,
            path: Path.Combine(SourceText.ShellSourceRoot(), relative));
        return (ShellCompilation.Compilation.AddSyntaxTrees(planted), [.. CompilationUnits(), (relative, planted)]);
    }

    /// <summary>The shell with a member inserted into the one class a file
    /// declares (the documents are not partial, so a planted member goes
    /// into the real class).</summary>
    private static (CSharpCompilation, List<(string, SyntaxTree)>) InsertMember(string relative, string member) =>
        Planted(relative, root =>
        {
            ClassDeclarationSyntax type = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
                .Single(candidate => candidate.Identifier.ValueText == Path.GetFileNameWithoutExtension(relative));
            return root.ReplaceNode(type, type.AddMembers(SyntaxFactory.ParseMemberDeclaration(member)!));
        });

    /// <summary>The shell with one anchor in a file replaced.</summary>
    private static (CSharpCompilation, List<(string, SyntaxTree)>) Replace(string relative, string anchor, string replacement) =>
        Planted(relative, root =>
        {
            string text = root.ToFullString();
            Assert.True(text.Split(anchor).Length == 2, $"the planted row no longer finds its one anchor in {relative} — update the row");
            return CSharpSyntaxTree.ParseText(text.Replace(anchor, replacement, StringComparison.Ordinal), Options).GetRoot();
        });

    private static (CSharpCompilation, List<(string, SyntaxTree)>) Planted(string relative, Func<SyntaxNode, SyntaxNode> plant)
    {
        SyntaxTree real = ShellCompilation.Sources.Single(source => source.Relative == relative).Source.Root.SyntaxTree;
        SyntaxTree planted = CSharpSyntaxTree.Create(
            (CSharpSyntaxNode)plant(real.GetRoot()), (CSharpParseOptions)real.Options, real.FilePath);
        return (
            ShellCompilation.Compilation.ReplaceSyntaxTree(real, planted),
            [.. CompilationUnits().Select(unit => unit.Relative == relative ? (relative, planted) : unit)]);
    }

    private static CSharpParseOptions Options =>
        (CSharpParseOptions)ShellCompilation.Sources[0].Source.Root.SyntaxTree.Options;

    private static SyntaxNode Parse(string source) => CSharpSyntaxTree.ParseText(source, Options).GetRoot();

    private static IEnumerable<InvocationExpressionSyntax> Creations(SyntaxNode root) =>
        root.DescendantNodes().OfType<InvocationExpressionSyntax>().Where(invocation => Named(invocation, "RequestFocusLanding"));

    private static bool Named(InvocationExpressionSyntax invocation, string name) => invocation.Expression switch
    {
        MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText == name,
        MemberBindingExpressionSyntax binding => binding.Name.Identifier.ValueText == name,
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText == name,
        _ => false,
    };
}
