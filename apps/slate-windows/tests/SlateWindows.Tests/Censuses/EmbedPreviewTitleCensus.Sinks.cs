// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// EmbedPreviewTitleCensus, the C# half: where a card title is handed on
// toward a reader, and what may arrive there. The rule and its history are
// in EmbedPreviewTitleCensus.cs.

using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests.Censuses;

public sealed partial class EmbedPreviewTitleCensus
{
    private const string CoreTitle = "SlateUniffiMethods.ResolvedEmbedTitle";
    private const string LocatorHelper = "WithSourceLineLocator";
    private const string NodeRecord = "EditorEmbedPreviewNode";
    private const string NodeMetadataName = "SlateWindows.EditorEmbedPreviewNode";
    private const string Renderer = "EditorEmbedPreviewView";

    /// <summary>The element the popover's name is set on (XAML), and the
    /// peer it hands that name to assistive technology through.</summary>
    private const string PopoverHost = "AutomationLandmarkGrid";
    private const string PopoverPeer = "AutomationLandmarkPeer";

    // The causes as the failures spell them; the mutation rows look for these.
    internal const string PlusConcatenation = "a '+' concatenation";
    internal const string Interpolation = "an interpolated string";
    internal const string FormatCall = "string.Format";
    internal const string ConcatCall = "string.Concat";
    internal const string JoinCall = "string.Join";
    internal const string Builder = "a StringBuilder";
    internal const string Literal = "a string literal";

    private static readonly string[] CoreTitleSpellings =
    [
        CoreTitle,
        "uniffi.slate_uniffi." + CoreTitle,
        "global::uniffi.slate_uniffi." + CoreTitle,
    ];

    private static readonly string[] PopoverSurfaces = ["PopoverTitle", "PopoverAutomationName"];

    /// <summary>The members that write the popover's header and name: the
    /// loading state, the resolved card, the unavailable outcome and a
    /// citation. Only the resolved card's writes carry a card title (the
    /// publisher facts read them); the set is pinned so that no writer
    /// appears beside them unread.</summary>
    private static readonly string[] PopoverWriters =
        ["PreviewEmbed", Publisher, "PresentUnavailableEmbed", "PresentCitation"];

    /// <summary>The previews that are NOT cards — the depth limit, an
    /// unresolved embed, an unknown resolution, and the embeds leaf's two
    /// budget notices — registered by file, member and title exactly as
    /// written. Each renders as a warning, never as a card; every other node
    /// the host builds is a card.</summary>
    private static readonly (string File, string Member, string Title)[] RegisteredWarnings =
    [
        ("EditorInteractions.cs", "BuildEmbedNode", "message"),
        ("EditorInteractions.cs", "BuildEmbedNode", "Describe(unresolved.Reason)"),
        ("EditorInteractions.cs", "BuildEmbedNode", "\"The embed could not be resolved.\""),
        ("Panels/RightPanePanelsViewModel.cs", "OverBudget", "OverBudgetMessage"),
        ("Panels/RightPanePanelsViewModel.cs", "RowLimit", "message"),
    ];

    /// <summary>What the renderer does with a node's Title: the card's
    /// expander header and the warning's text, and the UIA name of the
    /// card, of a plain card, of its warning and of its image — six reads,
    /// each handed on whole.</summary>
    private const int RendererReads = 6;

    /// <summary>The reading view's card header and nested-card header.</summary>
    private const int ReadingHeaders = 2;

    /// <summary>What a resolution is when it is a card: the binding's
    /// EmbedResolution variants but Unresolved. Read from the binding, so a
    /// new variant widens the expectation instead of slipping past it.</summary>
    private static readonly Lazy<string[]> CardKinds = new(() => typeof(EmbedResolution)
        .GetNestedTypes(BindingFlags.Public)
        .Where(type => type.IsSubclassOf(typeof(EmbedResolution))
            && type != typeof(EmbedResolution.Unresolved))
        .Select(type => type.Name)
        .Order(StringComparer.Ordinal)
        .ToArray());

    internal enum SinkKind
    {
        Card,
        CardInitializer,
        Warning,
        Renderer,
        PopoverHeader,
        PopoverName,
        LocatorDeclaration,
        LocatorCall,
        ReadingHeader,
        ReadingLandmark,
        LandmarkStore,
        LandmarkRead,
        LandmarkUse,
        NodeRecordDeclaration,
        PopoverHostPeer,
        PopoverPeerName,
    }

    internal sealed record Sink(SinkKind Kind, string File, int Line, string Key, string What);

    internal sealed record FileScan(string File, ImmutableArray<Sink> Sinks, ImmutableArray<string> Failures);

    /// <summary>One value that can arrive at a sink, or why the census
    /// cannot follow it there.</summary>
    private readonly record struct Leaf(ExpressionSyntax? Value, string? Cause, SyntaxNode At);

    /// <summary>The shell's sources, each scanned once per test process.</summary>
    internal sealed class ShellScan
    {
        internal static readonly Lazy<ShellScan> Baseline = new(() =>
        {
            // The binding's types are loaded before the shared compilation is
            // first built, so its references include them.
            _ = typeof(SlateUniffiMethods).Assembly;
            return new ShellScan(ShellCompilation.Sources
                .Select(entry => ScanFile(
                    entry.Relative, entry.Source.Root, ShellCompilation.ModelFor(entry.Source)))
                .ToImmutableArray());
        });

        private ShellScan(ImmutableArray<FileScan> files)
        {
            Files = files;
        }

        internal ImmutableArray<FileScan> Files { get; }

        internal IEnumerable<Sink> Sinks => Files
            .SelectMany(file => file.Sinks)
            .OrderBy(sink => sink.Kind)
            .ThenBy(sink => sink.File, StringComparer.Ordinal)
            .ThenBy(sink => sink.Line);

        internal string[] Failures => Files.SelectMany(file => file.Failures).ToArray();
    }

    /// <summary>Every sink in one file, and every failure at them.</summary>
    internal static FileScan ScanFile(string file, SyntaxNode root, SemanticModel model)
    {
        var sinks = new List<Sink>();
        var failures = new List<string>(DecoyFailures(root, file));
        INamedTypeSymbol? node = model.Compilation.GetTypeByMetadataName(NodeMetadataName);

        foreach (SyntaxNode syntax in root.DescendantNodes())
        {
            switch (syntax)
            {
                case BaseObjectCreationExpressionSyntax creation:
                    if (BuildsNode(file, creation, model, node, failures))
                    {
                        ScanNode(file, creation, sinks, failures);
                    }
                    else if (InRenderer(creation) && model.GetTypeInfo(creation).Type is { } built
                        && built.Locations.Any(location => location.IsInSource))
                    {
                        // A host control could bring a peer that renames the
                        // card it shows; the renderer builds WPF's own.
                        failures.Add($"{file}:{Line(creation)}: the renderer builds {built.Name}, a host type whose "
                            + "automation peer the census does not read.");
                    }
                    break;
                case WithExpressionSyntax with:
                    ScanWith(file, with, model, node, sinks, failures);
                    break;
                case MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Title" } access
                    when ReadsNodeTitle(access, model, node):
                    ScanTitleRead(file, access, sinks, failures);
                    break;
                case MemberBindingExpressionSyntax { Name.Identifier.ValueText: "Title" } binding
                    when ReadsNodeTitle(binding, model, node):
                    // a?.Title is a binding under a conditional access, not a
                    // member access; the whole read is the conditional.
                    ScanTitleRead(
                        file,
                        binding.Parent is ConditionalAccessExpressionSyntax conditional
                            && conditional.WhenNotNull == binding
                                ? conditional
                                : binding,
                        sinks,
                        failures);
                    break;
                case AssignmentExpressionSyntax assignment when PopoverSurface(assignment.Left) is { } surface:
                    ScanPopoverWrite(file, assignment, surface, sinks, failures);
                    break;
                case PropertyDeclarationSyntax property
                    when PopoverSurfaces.Contains(property.Identifier.ValueText):
                    failures.AddRange(PassThroughFailures(file, property, root));
                    break;
                case RecordDeclarationSyntax record when record.Identifier.ValueText == NodeRecord:
                    ScanNodeRecord(file, record, sinks, failures);
                    break;
                case MethodDeclarationSyntax method when method.Identifier.ValueText == LocatorHelper:
                    ScanLocator(
                        file,
                        method,
                        method.ParameterList,
                        method.ExpressionBody?.Expression ?? SingleReturn(method.Body),
                        sinks,
                        failures);
                    break;
                case LocalFunctionStatementSyntax local when local.Identifier.ValueText == LocatorHelper:
                    ScanLocator(
                        file,
                        local,
                        local.ParameterList,
                        local.ExpressionBody?.Expression ?? SingleReturn(local.Body),
                        sinks,
                        failures);
                    break;
                case InvocationExpressionSyntax call:
                    ScanCall(file, call, sinks, failures);
                    break;
                case ClassDeclarationSyntax renderer when renderer.Identifier.ValueText == Renderer:
                    failures.AddRange(RendererWrapping(file, renderer));
                    failures.AddRange(RendererTextFailures(file, renderer));
                    break;
                case ClassDeclarationSyntax host when host.Identifier.ValueText == PopoverHost:
                    ScanPopoverHost(file, host, sinks, failures);
                    break;
                case ClassDeclarationSyntax peer when peer.Identifier.ValueText == PopoverPeer:
                    ScanPopoverPeer(file, peer, sinks, failures);
                    break;
            }
        }

        return new FileScan(file, [.. sinks], [.. failures]);
    }

    /// <summary>The population, pinned: what a renamed, moved or rebuilt
    /// sink would change.</summary>
    internal static IEnumerable<string> PopulationFailures(IReadOnlyList<FileScan> files)
    {
        Sink[] sinks = files.SelectMany(file => file.Sinks).ToArray();
        int Count(SinkKind kind, string? key = null) =>
            sinks.Count(sink => sink.Kind == kind && (key is null || sink.Key == key));
        string At(SinkKind kind) => string.Join(
            ", ", sinks.Where(sink => sink.Kind == kind).Select(sink => $"{sink.File}:{sink.Line}"));

        foreach (string kind in CardKinds.Value)
        {
            if (Count(SinkKind.Card, kind) != 1)
            {
                yield return $"the {kind} card is built {Count(SinkKind.Card, kind)} times; the census reads "
                    + $"exactly one card per resolved kind ({string.Join(", ", CardKinds.Value)}, from the "
                    + "binding's EmbedResolution).";
            }
        }
        foreach ((string file, string member, string title) in RegisteredWarnings)
        {
            int built = Count(SinkKind.Warning, $"{file}|{member}|{title}");
            if (built != 1)
            {
                yield return $"the registered warning `{title}` in {file} {member} is built {built} times; "
                    + "the census reads each exactly once.";
            }
        }
        if (Count(SinkKind.CardInitializer) != 0)
        {
            yield return $"a card's Title is set after it is built ({At(SinkKind.CardInitializer)}); the "
                + "census pins none — build the card with core's title.";
        }
        if (Count(SinkKind.Renderer) != RendererReads)
        {
            yield return $"a card's Title is read {Count(SinkKind.Renderer)} times ({At(SinkKind.Renderer)}); "
                + $"the census reads the renderer's {RendererReads}.";
        }
        foreach ((SinkKind kind, string surface) in new[]
        {
            (SinkKind.PopoverHeader, "PopoverTitle"),
            (SinkKind.PopoverName, "PopoverAutomationName"),
        })
        {
            string[] writers = sinks.Where(sink => sink.Kind == kind).Select(sink => sink.Key)
                .Order(StringComparer.Ordinal).ToArray();
            if (!writers.SequenceEqual(PopoverWriters.Order(StringComparer.Ordinal)))
            {
                yield return $"{surface} is written in [{string.Join(", ", writers)}]; the census reads "
                    + $"exactly one write in each of [{string.Join(", ", PopoverWriters)}].";
            }
        }
        if (Count(SinkKind.LocatorDeclaration) != 1)
        {
            yield return $"{LocatorHelper} is declared {Count(SinkKind.LocatorDeclaration)} times "
                + $"({At(SinkKind.LocatorDeclaration)}); the locator is appended in one named place.";
        }
        string[] callers = sinks.Where(sink => sink.Kind == SinkKind.LocatorCall).Select(sink => sink.Key).ToArray();
        if (callers is not [Publisher])
        {
            yield return $"{LocatorHelper} is called from [{string.Join(", ", callers)}]; its one caller is "
                + $"{Publisher}'s header.";
        }
        if (Count(SinkKind.ReadingHeader) != ReadingHeaders)
        {
            yield return $"the reading view has {Count(SinkKind.ReadingHeader)} embed-header title runs "
                + $"({At(SinkKind.ReadingHeader)}); the census reads the card's and the nested card's.";
        }
        foreach ((SinkKind kind, string what) in new[]
        {
            (SinkKind.ReadingLandmark, "embed landmark name (ReadingSemantics.MarkEmbed)"),
            (SinkKind.LandmarkStore, "store of the embed landmark name"),
            (SinkKind.LandmarkRead, "read of the embed landmark name"),
            (SinkKind.LandmarkUse, "use of the embed landmark name (ReadingSemantics.EmbedNameOf)"),
            (SinkKind.NodeRecordDeclaration, $"{NodeRecord} declaration"),
            (SinkKind.PopoverHostPeer, $"{PopoverHost} declaration"),
            (SinkKind.PopoverPeerName, $"{PopoverPeer} declaration"),
        })
        {
            if (Count(kind) != 1)
            {
                yield return $"{Count(kind)} of the {what} ({At(kind)}); the census reads exactly one.";
            }
        }
    }

    /// <summary>Every failure the C# census reports over the shipped sources
    /// with one file mutated in memory. The edits are (original, replacement)
    /// pairs; each original occurs exactly once, and the mutant must parse
    /// and bind like the shipped file.</summary>
    private static string[] CSharpMutantFailures(string file, string[] edits)
    {
        Assert.True(edits.Length > 0 && edits.Length % 2 == 0, "a row's edits come in (original, replacement) pairs");
        (string Relative, CSharpSource Source) shipped =
            Assert.Single(ShellCompilation.Sources, entry => entry.Relative == file);
        SyntaxTree shippedTree = shipped.Source.Root.SyntaxTree;
        string text = shippedTree.GetText().ToString();
        var spans = new List<(int Start, int Length)>();
        for (int i = 0; i < edits.Length; i += 2)
        {
            Assert.True(
                Occurrences(text, edits[i]) == 1,
                $"{file} does not carry the row's original exactly once, so the row mutates nothing it can name: {edits[i]}");
            int at = text.IndexOf(edits[i], StringComparison.Ordinal);
            int delta = edits[i + 1].Length - edits[i].Length;
            for (int span = 0; span < spans.Count; span++)
            {
                if (spans[span].Start > at)
                {
                    spans[span] = (spans[span].Start + delta, spans[span].Length);
                }
            }
            text = string.Concat(text.AsSpan(0, at), edits[i + 1], text.AsSpan(at + edits[i].Length));
            spans.Add((at, edits[i + 1].Length));
        }

        SyntaxTree mutant = CSharpSyntaxTree.ParseText(
            text, (CSharpParseOptions)shippedTree.Options, path: shippedTree.FilePath);
        Diagnostic[] unparsed = mutant.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
        Assert.True(
            unparsed.Length == 0,
            $"the {file} mutant does not parse, so it proves nothing about the rule:\n"
            + string.Join("\n", unparsed.Select(diagnostic => diagnostic.ToString())));

        CSharpCompilation compilation = ShellCompilation.Compilation.ReplaceSyntaxTree(shippedTree, mutant);
        SemanticModel model = compilation.GetSemanticModel(mutant);
        HashSet<string> shippedErrors = ShippedErrors.GetOrAdd(file, _ => ShellCompilation.ModelFor(shipped.Source)
            .GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(diagnostic => diagnostic.Id)
            .ToHashSet(StringComparer.Ordinal));
        Diagnostic[] unbound = spans
            .SelectMany(span => model.GetDiagnostics(new TextSpan(span.Start, Math.Max(1, span.Length))))
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error
                && !shippedErrors.Contains(diagnostic.Id))
            .ToArray();
        Assert.True(
            unbound.Length == 0,
            $"the {file} mutant does not bind like the shipped file, so it proves nothing about the rule:\n"
            + string.Join("\n", unbound.Select(diagnostic => diagnostic.ToString())));

        FileScan rescanned = ScanFile(file, mutant.GetRoot(), model);
        FileScan[] files = ShellScan.Baseline.Value.Files
            .Select(scan => scan.File == file ? rescanned : scan)
            .ToArray();
        SyntaxNode interactions = file == "EditorInteractions.cs"
            ? mutant.GetRoot()
            : Assert.Single(ShellCompilation.Sources, entry => entry.Relative == "EditorInteractions.cs").Source.Root;
        MethodDeclarationSyntax publisher = Assert.Single(
            interactions.DescendantNodes().OfType<MethodDeclarationSyntax>(),
            method => method.Identifier.ValueText == Publisher);
        return
        [
            .. files.SelectMany(scan => scan.Failures),
            .. PopulationFailures(files),
            .. PublisherFailures(publisher, "EditorInteractions.cs"),
            .. SpellingFailures(mutant.GetRoot(), file),
        ];
    }

    private static readonly ConcurrentDictionary<string, HashSet<string>> ShippedErrors = new(StringComparer.Ordinal);

    // ---- the node: constructions, `with`, the record -------------------

    /// <summary>Whether this creation builds an EditorEmbedPreviewNode —
    /// bound, so a target-typed new or an alias is found; a creation
    /// spelled with the record's name that does not bind to it means the
    /// binding is broken, and the census would read less than the truth.</summary>
    private static bool BuildsNode(
        string file,
        BaseObjectCreationExpressionSyntax creation,
        SemanticModel model,
        INamedTypeSymbol? node,
        List<string> failures)
    {
        bool bound = node is not null
            && SymbolEqualityComparer.Default.Equals(model.GetTypeInfo(creation).Type, node);
        bool spelled = creation is ObjectCreationExpressionSyntax explicitCreation
            && RightmostName(explicitCreation.Type) == NodeRecord;
        if (spelled && !bound)
        {
            failures.Add($"{file}:{Line(creation)}: `{Short(creation)}` did not bind to {NodeMetadataName}; "
                + "the census's binding is broken and would read less than the truth.");
        }
        return bound || spelled;
    }

    private static void ScanNode(
        string file, BaseObjectCreationExpressionSyntax creation, List<Sink> sinks, List<string> failures)
    {
        int line = Line(creation);
        string member = MemberName(creation);
        foreach (AssignmentExpressionSyntax write in TitleWrites(creation.Initializer))
        {
            sinks.Add(new Sink(SinkKind.CardInitializer, file, Line(write), member,
                $"a card's Title set in an initializer, in {member}"));
            failures.AddRange(CardFailures(file, write.Right, write, $"a card's Title set in an initializer ({member})"));
        }

        ExpressionSyntax? title = ArgumentFor(creation.ArgumentList, "Title", 0);
        if (title is null)
        {
            failures.Add($"{file}:{line}: `{Short(creation)}` — the census cannot find the node's Title argument.");
            return;
        }

        string written = CSharpSource.Normalize(title);
        if (RegisteredWarnings.Any(warning =>
            warning.File == file && warning.Member == member && warning.Title == written))
        {
            sinks.Add(new Sink(SinkKind.Warning, file, line, $"{file}|{member}|{written}",
                $"the warning `{written}`, in {member}"));
            if (!IsBoolean(ArgumentFor(creation.ArgumentList, "IsWarning", 6), true)
                || !IsBoolean(ArgumentFor(creation.ArgumentList, "IsDisclosure", 4), false))
            {
                failures.Add($"{file}:{line}: the registered warning `{written}` in {member} does not render "
                    + "as one (IsWarning: true, IsDisclosure: false), so it is a card with a host title.");
            }
            return;
        }

        string? kind = CardKind(creation, out string? unattributed);
        string card = kind is null ? "an unattributed card's" : $"the {kind} card's";
        sinks.Add(new Sink(SinkKind.Card, file, line, kind ?? "?", $"{card} Title, in {member}"));
        if (unattributed is not null)
        {
            failures.Add($"{file}:{line}: {unattributed}");
        }
        failures.AddRange(CardFailures(file, title, creation, $"{card} Title ({NodeRecord}, in {member})"));
    }

    private static void ScanWith(
        string file,
        WithExpressionSyntax with,
        SemanticModel model,
        INamedTypeSymbol? node,
        List<Sink> sinks,
        List<string> failures)
    {
        AssignmentExpressionSyntax[] writes = TitleWrites(with.Initializer).ToArray();
        if (writes.Length == 0)
        {
            return;
        }
        // Another record's Title is not a card's; an unbound one might be,
        // and is read.
        ITypeSymbol? type = model.GetTypeInfo(with).Type;
        if (type is not null && type.TypeKind != TypeKind.Error
            && !SymbolEqualityComparer.Default.Equals(type, node))
        {
            return;
        }
        string member = MemberName(with);
        foreach (AssignmentExpressionSyntax write in writes)
        {
            sinks.Add(new Sink(SinkKind.CardInitializer, file, Line(write), member,
                $"a card's Title set by `with`, in {member}"));
            failures.AddRange(CardFailures(file, write.Right, write, $"a card's Title set by `with` ({member})"));
        }
    }

    /// <summary>The record carries what it is built with: Title is its
    /// positional string and nothing redeclares it.</summary>
    private static void ScanNodeRecord(
        string file, RecordDeclarationSyntax record, List<Sink> sinks, List<string> failures)
    {
        sinks.Add(new Sink(SinkKind.NodeRecordDeclaration, file, Line(record), NodeRecord, $"the {NodeRecord} record"));
        ParameterSyntax? first = record.ParameterList?.Parameters.FirstOrDefault();
        bool positional = first is { Identifier.ValueText: "Title", Type: { } type }
            && CSharpSource.Normalize(type) == "string";
        bool redeclared = record.Members.Any(member => member switch
        {
            PropertyDeclarationSyntax property => property.Identifier.ValueText == "Title",
            FieldDeclarationSyntax field => field.Declaration.Variables.Any(
                variable => variable.Identifier.ValueText == "Title"),
            _ => false,
        });
        if (!positional || redeclared)
        {
            failures.Add($"{file}:{Line(record)}: {NodeRecord}'s Title is not its positional `string Title` "
                + "alone, so what a card is built with may not be what it shows.");
        }
    }

    // ---- the renderer ---------------------------------------------------

    private static bool ReadsNodeTitle(ExpressionSyntax read, SemanticModel model, INamedTypeSymbol? node) =>
        node is not null
        && model.GetSymbolInfo(read).Symbol is IPropertySymbol { Name: "Title" } property
        && SymbolEqualityComparer.Default.Equals(property.ContainingType, node);

    /// <summary>A card's Title is read in one place — the renderer — and
    /// handed to WPF whole: a UIA name, an expander header, a warning's
    /// text.</summary>
    private static void ScanTitleRead(string file, ExpressionSyntax read, List<Sink> sinks, List<string> failures)
    {
        int line = Line(read);
        string? form = RendererForm(read);
        string owner = read.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText ?? "?";
        sinks.Add(new Sink(SinkKind.Renderer, file, line, form ?? "?",
            $"the renderer's {form ?? "composition"} of a card's Title"));
        if (owner != Renderer)
        {
            failures.Add($"{file}:{line}: a card's Title is read outside {Renderer}: `{Short(read.Parent ?? read)}`; "
                + "the renderer is the one place a card's title is shown and named.");
        }
        else if (form is null)
        {
            failures.Add($"{file}:{line}: the renderer composes a card's Title: `{Short(read.Parent ?? read)}`; "
                + "it shows and names a card with its Title whole.");
        }
    }

    private static string? RendererForm(ExpressionSyntax read) => read.Parent switch
    {
        ArgumentSyntax { NameColon: null } argument
            when argument.RefKindKeyword.IsKind(SyntaxKind.None)
                && argument.Parent is ArgumentListSyntax { Arguments.Count: 2 } list
                && list.Arguments.IndexOf(argument) == 1
                && list.Parent is InvocationExpressionSyntax call
                && CSharpSource.Normalize(call.Expression) is "AutomationProperties.SetName"
                    or "System.Windows.Automation.AutomationProperties.SetName" => "UIA name",
        AssignmentExpressionSyntax { Left: IdentifierNameSyntax { Identifier.ValueText: "Header" or "Text" } target } assignment
            when assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
                && assignment.Right == read
                && assignment.Parent is InitializerExpressionSyntax => target.Identifier.ValueText,
        _ => null,
    };

    /// <summary>The renderer hands a title to WPF whole; a format, template
    /// or style set there — assigned, or applied through SetValue, a
    /// resource reference or a binding — would wrap it in text the census
    /// cannot read.</summary>
    private static IEnumerable<string> RendererWrapping(string file, ClassDeclarationSyntax renderer)
    {
        foreach (SyntaxNode node in renderer.DescendantNodes())
        {
            string? target = node switch
            {
                AssignmentExpressionSyntax { Left: IdentifierNameSyntax identifier } => identifier.Identifier.ValueText,
                AssignmentExpressionSyntax { Left: MemberAccessExpressionSyntax access } => access.Name.Identifier.ValueText,
                InvocationExpressionSyntax { ArgumentList.Arguments: [var property, ..] } call
                    when InvokedName(call) is "SetValue" or "SetResourceReference" or "SetBinding"
                        or "SetCurrentValue" =>
                    CSharpSource.Normalize(property.Expression).Split('.')[^1] is var named
                        && named.EndsWith("Property", StringComparison.Ordinal)
                            ? named[..^"Property".Length]
                            : null,
                _ => null,
            };
            if (target is not null
                && (target.EndsWith("StringFormat", StringComparison.Ordinal)
                    || target.EndsWith("Template", StringComparison.Ordinal)
                    || target.EndsWith("TemplateSelector", StringComparison.Ordinal)
                    || target == "Style"))
            {
                yield return $"{file}:{Line(node)}: the renderer sets {target}, which can wrap a card's title in "
                    + "text the census cannot read.";
            }
        }
    }

    /// <summary>What the renderer may say besides a card's Title, by target
    /// and value exactly as written: the body's and the Jump button's names,
    /// the body's text, the expander's content, the button's label and the
    /// view's own content. Each occurs once.</summary>
    private static readonly (string Target, string Value)[] RendererTexts =
    [
        ("Name", "\"Embedded content\""),
        ("Name", "$\"Jump to source: {sourcePath}\""),
        ("Text", "part.Text"),
        ("Content", "content"),
        ("Content", "\"Jump to source\""),
        ("Content", "Rootisnull?null:BuildNode(Root)"),
    ];

    private static readonly string[] TextTargets = ["Name", "Header", "Content", "Text", "ToolTip"];

    /// <summary>Every UIA Name the renderer writes and every text it sets on a
    /// control is a card's Title (read above) or a registered form; a name is
    /// never read back to compose, and no other route writes one (codex round
    /// 3: a second SetName after the validated one, built from GetName).</summary>
    private static IEnumerable<string> RendererTextFailures(string file, ClassDeclarationSyntax renderer)
    {
        var seen = new List<(string Target, string Value)>();
        foreach (SyntaxNode node in renderer.DescendantNodes())
        {
            (string Target, ExpressionSyntax Value)? write = null;
            switch (node)
            {
                case InvocationExpressionSyntax call when AutomationMember(call) is { } member:
                    switch (member)
                    {
                        case "SetName" when call.ArgumentList.Arguments is [_, var named]:
                            write = ("Name", named.Expression);
                            break;
                        case "GetName" or "SetLabeledBy":
                            yield return $"{file}:{Line(call)}: the renderer calls AutomationProperties.{member} — a "
                                + $"card's name is never read back or borrowed: `{Short(call)}`.";
                            break;
                    }
                    break;
                case InvocationExpressionSyntax { ArgumentList.Arguments: [var property, ..] } call
                    when InvokedName(call) is "SetValue" or "SetCurrentValue" or "SetBinding" or "SetResourceReference"
                        && CSharpSource.Normalize(property.Expression).Split('.')[^1] is var named
                        && named.EndsWith("Property", StringComparison.Ordinal)
                        && TextTargets.Contains(named[..^"Property".Length]):
                    yield return $"{file}:{Line(call)}: the renderer writes {named[..^"Property".Length]} through "
                        + $"{InvokedName(call)}, a route the census does not read: `{Short(call)}`.";
                    break;
                case AssignmentExpressionSyntax assignment
                    when (assignment.Left switch
                    {
                        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
                        MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
                        _ => null,
                    }) is { } target && TextTargets.Contains(target):
                    write = (target, assignment.Right);
                    break;
            }

            if (write is not { } found || IsTitleRead(found.Value))
            {
                continue;
            }
            string value = CSharpSource.Normalize(found.Value);
            if (RendererTexts.Contains((found.Target, value)))
            {
                seen.Add((found.Target, value));
            }
            else
            {
                yield return $"{file}:{Line(node)}: the renderer writes {found.Target} = `{Short(found.Value)}`, "
                    + "which is neither a card's Title nor a text the census registers.";
            }
        }
        foreach ((string target, string value) in RendererTexts.Where(text => seen.Count(written => written == text) != 1))
        {
            yield return $"{file}: the renderer writes {target} = `{value}` {seen.Count(written => written == (target, value))} "
                + "times; the census registers it once.";
        }
    }

    private static string? AutomationMember(InvocationExpressionSyntax call) =>
        call.Expression is MemberAccessExpressionSyntax access
        && CSharpSource.Normalize(access.Expression) is "AutomationProperties"
            or "System.Windows.Automation.AutomationProperties"
            or "global::System.Windows.Automation.AutomationProperties"
            ? access.Name.Identifier.ValueText
            : null;

    private static bool IsTitleRead(ExpressionSyntax value) => Unwrap(value) switch
    {
        MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Title" } => true,
        ConditionalAccessExpressionSyntax { WhenNotNull: MemberBindingExpressionSyntax { Name.Identifier.ValueText: "Title" } } => true,
        _ => false,
    };

    private static bool InRenderer(SyntaxNode node) =>
        node.Ancestors().OfType<ClassDeclarationSyntax>().Any(type => type.Identifier.ValueText == Renderer);

    /// <summary>The popover's name reaches assistive technology through the
    /// peer its host element creates.</summary>
    private static void ScanPopoverHost(
        string file, ClassDeclarationSyntax host, List<Sink> sinks, List<string> failures)
    {
        sinks.Add(new Sink(SinkKind.PopoverHostPeer, file, Line(host), PopoverHost,
            $"{PopoverHost}, the popover's host, creates its peer"));
        MethodDeclarationSyntax? create = host.Members.OfType<MethodDeclarationSyntax>()
            .SingleOrDefault(method => method.Identifier.ValueText == "OnCreateAutomationPeer");
        ExpressionSyntax? created = create?.ExpressionBody?.Expression ?? SingleReturn(create?.Body);
        bool creates = created is ObjectCreationExpressionSyntax { ArgumentList.Arguments: [{ Expression: ThisExpressionSyntax }] } peer
            && RightmostName(peer.Type) == PopoverPeer;
        if (!creates)
        {
            failures.Add($"{file}:{Line(create ?? (SyntaxNode)host)}: {PopoverHost} does not hand its name to "
                + $"assistive technology through {PopoverPeer}, the peer the census reads.");
        }
    }

    /// <summary>That peer hands the name on as set: it does not compose its
    /// own.</summary>
    private static void ScanPopoverPeer(
        string file, ClassDeclarationSyntax peer, List<Sink> sinks, List<string> failures)
    {
        sinks.Add(new Sink(SinkKind.PopoverPeerName, file, Line(peer), PopoverPeer,
            $"{PopoverPeer}, which hands the popover's name on"));
        if (peer.BaseList?.Types.FirstOrDefault()?.Type is not { } baseType
            || RightmostName(baseType) != "FrameworkElementAutomationPeer")
        {
            failures.Add($"{file}:{Line(peer)}: {PopoverPeer} does not derive from WPF's "
                + "FrameworkElementAutomationPeer, so the census cannot tell how it names the popover.");
        }
        foreach (MethodDeclarationSyntax named in peer.Members.OfType<MethodDeclarationSyntax>()
            .Where(method => method.Identifier.ValueText == "GetNameCore"))
        {
            failures.Add($"{file}:{Line(named)}: {PopoverPeer} overrides GetNameCore, which would compose the "
                + "popover's name instead of handing on core's.");
        }
    }

    // ---- the popover ------------------------------------------------------

    private static string? PopoverSurface(ExpressionSyntax left) => left switch
    {
        IdentifierNameSyntax { Identifier.ValueText: var name } when PopoverSurfaces.Contains(name) => name,
        MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax, Name.Identifier.ValueText: var name }
            when PopoverSurfaces.Contains(name) => name,
        _ => null,
    };

    private static void ScanPopoverWrite(
        string file, AssignmentExpressionSyntax assignment, string surface, List<Sink> sinks, List<string> failures)
    {
        string member = MemberName(assignment);
        sinks.Add(new Sink(
            surface == "PopoverTitle" ? SinkKind.PopoverHeader : SinkKind.PopoverName,
            file,
            Line(assignment),
            member,
            $"{surface}, written in {member}"));
        if (!assignment.IsKind(SyntaxKind.SimpleAssignmentExpression))
        {
            failures.Add($"{file}:{Line(assignment)}: `{Short(assignment)}` composes onto {surface} "
                + $"({assignment.OperatorToken.Text}).");
        }
    }

    /// <summary>The popover's header and name reach XAML through their
    /// properties: the getter returns the backing field, the setter stores
    /// `value`, and nothing else touches the field.</summary>
    private static IEnumerable<string> PassThroughFailures(string file, PropertyDeclarationSyntax property, SyntaxNode root)
    {
        string name = property.Identifier.ValueText;
        AccessorDeclarationSyntax? getter = property.AccessorList?.Accessors
            .SingleOrDefault(accessor => accessor.IsKind(SyntaxKind.GetAccessorDeclaration));
        AccessorDeclarationSyntax? setter = property.AccessorList?.Accessors
            .SingleOrDefault(accessor => accessor.IsKind(SyntaxKind.SetAccessorDeclaration));
        string? field = (getter?.ExpressionBody?.Expression ?? SingleReturn(getter?.Body))
            is IdentifierNameSyntax { Identifier.ValueText: var backing }
                ? backing
                : null;
        if (field is null)
        {
            yield return $"{file}:{Line(property)}: {name}'s getter does not return its backing field as stored.";
            yield break;
        }

        ExpressionSyntax? store = setter?.ExpressionBody?.Expression ?? SingleStatement(setter?.Body);
        bool stores = store is InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: "SetField" } } call
            && call.ArgumentList.Arguments is [var target, var value]
            && target.RefKindKeyword.IsKind(SyntaxKind.RefKeyword)
            && target.Expression is IdentifierNameSyntax { Identifier.ValueText: var stored }
            && stored == field
            && value.Expression is IdentifierNameSyntax { Identifier.ValueText: "value" };
        if (!stores)
        {
            SyntaxNode at = setter ?? (SyntaxNode)property;
            yield return $"{file}:{Line(at)}: {name}'s setter does not store `value` as given: `{Short(at)}`.";
        }

        int touches = root.DescendantNodes().OfType<IdentifierNameSyntax>()
            .Count(identifier => identifier.Identifier.ValueText == field);
        if (touches != 2)
        {
            yield return $"{file}:{Line(property)}: {name}'s backing field {field} is touched {touches} times; "
                + "only its getter and setter may, so nothing writes around the property.";
        }
    }

    /// <summary>The one place the locator is appended: core's title, the
    /// locator, the line — nothing else.</summary>
    private static void ScanLocator(
        string file,
        SyntaxNode declaration,
        ParameterListSyntax parameters,
        ExpressionSyntax? body,
        List<Sink> sinks,
        List<string> failures)
    {
        int line = Line(declaration);
        sinks.Add(new Sink(SinkKind.LocatorDeclaration, file, line, LocatorHelper,
            $"{LocatorHelper}, the one place the locator is appended"));
        if (parameters.Parameters is not [var title, var sourceLine]
            || title.Type is null || CSharpSource.Normalize(title.Type) != "string"
            || sourceLine.Type is null || CSharpSource.Normalize(sourceLine.Type) != "int")
        {
            failures.Add($"{file}:{line}: {LocatorHelper} takes something other than (string title, int sourceLine).");
            return;
        }

        bool exact = body is InterpolatedStringExpressionSyntax
        {
            Contents:
            [
                InterpolationSyntax { AlignmentClause: null, FormatClause: null, Expression: IdentifierNameSyntax first },
                InterpolatedStringTextSyntax text,
                InterpolationSyntax { AlignmentClause: null, FormatClause: null, Expression: IdentifierNameSyntax second },
            ],
        }
            && first.Identifier.ValueText == title.Identifier.ValueText
            && text.TextToken.ValueText == Locator
            && second.Identifier.ValueText == sourceLine.Identifier.ValueText;
        if (!exact)
        {
            failures.Add($"{file}:{line}: {LocatorHelper} adds host text beyond the source-line locator: "
                + $"`{Short(body ?? declaration)}`; it returns core's title, \"{Locator}\" and the line, nothing else.");
        }
    }

    /// <summary>The popover header is core's title of what the event
    /// carries, passed through the locator helper; null when it is.</summary>
    private static string? HeaderFailure(ExpressionSyntax value, ExpressionSyntax? resolved)
    {
        foreach (Leaf leaf in Leaves(value, 0))
        {
            if (leaf.Cause is { } cause)
            {
                return cause;
            }
            ExpressionSyntax header = Unwrap(leaf.Value!);
            if (header is not InvocationExpressionSyntax { ArgumentList.Arguments: [var title, var line] } call
                || InvokedName(call) != LocatorHelper)
            {
                string what = Composition(header);
                bool locator = header.DescendantTokens()
                    .Any(token => token.ValueText.Contains(Locator.Trim(), StringComparison.Ordinal));
                return locator ? $"the source-line locator appended outside {LocatorHelper} ({what})" : what;
            }
            if (!IsCoreTitle(title.Expression, out ExpressionSyntax? titled))
            {
                return Composition(title.Expression);
            }
            if (resolved is not null
                && CSharpSource.Normalize(CSharpSource.Resolve(titled!, MemberScope(titled!)))
                    != CSharpSource.Normalize(resolved))
            {
                return $"core's title of `{Short(titled!)}`, not of what the event carries";
            }
            if (HostComposition(titled!) is { } fed)
            {
                return $"core's title fed host text ({fed.Cause})";
            }
            if (HostComposition(line.Expression) is { } lined)
            {
                return $"host text in the locator's line ({lined.Cause})";
            }
        }
        return null;
    }

    // ---- the reading view --------------------------------------------------

    private static void ScanCall(string file, InvocationExpressionSyntax call, List<Sink> sinks, List<string> failures)
    {
        string? invoked = InvokedName(call);
        switch (invoked)
        {
            case LocatorHelper:
                sinks.Add(new Sink(SinkKind.LocatorCall, file, Line(call), MemberName(call),
                    $"{LocatorHelper}, called in {MemberName(call)}"));
                break;
            case "MarkEmbedHeader" when OnReadingSemantics(call):
                ScanReadingHeader(file, call, sinks, failures);
                break;
            case "MarkEmbed" when OnReadingSemantics(call):
                ScanLandmark(file, call, sinks, failures);
                break;
            case "SetValue" when call.ArgumentList.Arguments is [var property, _] && IsEmbedName(property.Expression):
                ScanLandmarkStore(file, call, sinks, failures);
                break;
            case "GetValue" when call.ArgumentList.Arguments is [var property] && IsEmbedName(property.Expression):
                ScanLandmarkRead(file, call, sinks, failures);
                break;
            case "EmbedNameOf" when OnReadingSemantics(call):
                sinks.Add(new Sink(SinkKind.LandmarkUse, file, Line(call), MemberName(call),
                    $"the embed landmark name, read in {MemberName(call)}"));
                if (call.Parent is not ArgumentSyntax)
                {
                    failures.Add($"{file}:{Line(call)}: the embed landmark name is composed where it is read: "
                        + $"`{Short(call.Parent ?? call)}`; it is handed on whole.");
                }
                break;
        }
    }

    /// <summary>An embed header paragraph says core's title — one run —
    /// then a whitespace spacer and the Jump link, and nothing else.</summary>
    private static void ScanReadingHeader(
        string file, InvocationExpressionSyntax mark, List<Sink> sinks, List<string> failures)
    {
        string member = MemberName(mark);
        int line = Line(mark);
        SyntaxNode scope = MemberScope(mark);
        if (mark.ArgumentList.Arguments.FirstOrDefault()?.Expression
            is not IdentifierNameSyntax { Identifier.ValueText: var paragraph })
        {
            failures.Add($"{file}:{line}: an embed header the census cannot follow: `{Short(mark)}`.");
            return;
        }

        VariableDeclaratorSyntax[] declared = scope.DescendantNodes().OfType<VariableDeclaratorSyntax>()
            .Where(declarator => declarator.Identifier.ValueText == paragraph)
            .ToArray();
        if (declared is not [{ Initializer.Value: BaseObjectCreationExpressionSyntax created }]
            || created.ArgumentList is { Arguments.Count: > 0 }
            || created.Initializer?.Expressions.OfType<AssignmentExpressionSyntax>()
                .Any(assignment => assignment.Left is IdentifierNameSyntax { Identifier.ValueText: "Inlines" }) == true)
        {
            failures.Add($"{file}:{line}: the embed header in {member} is not one `new Paragraph {{ … }}` without "
                + "inline content of its own, so the census cannot read everything it says.");
        }

        int titles = 0;
        foreach (IdentifierNameSyntax use in scope.DescendantNodes().OfType<IdentifierNameSyntax>()
            .Where(identifier => identifier.Identifier.ValueText == paragraph))
        {
            switch (use.Parent)
            {
                case MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Inlines" } inlines
                    when inlines.Expression == use:
                    if (inlines.Parent is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Add" } add
                        && add.Parent is InvocationExpressionSyntax { ArgumentList.Arguments: [var added] })
                    {
                        titles += ScanHeaderInline(file, member, added.Expression, sinks, failures);
                    }
                    else
                    {
                        failures.Add($"{file}:{Line(inlines)}: the embed header's inlines are changed other than "
                            + $"by Add: `{Short(inlines.Parent ?? inlines)}`.");
                    }
                    break;
                case MemberAccessExpressionSyntax property
                    when property.Expression == use
                        && property.Parent is AssignmentExpressionSyntax { Left: var left } setting
                        && left == property
                        && setting.IsKind(SyntaxKind.SimpleAssignmentExpression):
                    // One of the paragraph's own properties (a margin, a
                    // weight) is set; that says nothing.
                    break;
                case ArgumentSyntax { Parent: ArgumentListSyntax { Parent: InvocationExpressionSyntax consumer } }
                    when IsHeaderConsumer(consumer):
                    break;
                case ReturnStatementSyntax:
                    break;
                default:
                    failures.Add($"{file}:{Line(use)}: the embed header is handed where the census cannot follow: "
                        + $"`{Short(use.Parent ?? use)}`.");
                    break;
            }
        }
        if (titles != 1)
        {
            failures.Add($"{file}:{line}: the embed header in {member} carries {titles} title runs; the census "
                + "reads exactly one.");
        }
    }

    private static int ScanHeaderInline(
        string file, string member, ExpressionSyntax added, List<Sink> sinks, List<string> failures)
    {
        switch (added)
        {
            case ObjectCreationExpressionSyntax run
                when RightmostName(run.Type) == "Run"
                    && run.Initializer is null
                    && run.ArgumentList?.Arguments is [var only]:
                if (only.Expression is LiteralExpressionSyntax literal && IsText(literal))
                {
                    string spoken = literal.Token.ValueText;
                    if (spoken.Length > 0 && spoken.All(char.IsWhiteSpace))
                    {
                        // The spacer before the Jump link.
                        return 0;
                    }
                    failures.Add($"{file}:{Line(run)}: static text beside the header's title: `{Short(run)}`; the "
                        + "header says core's title and nothing else.");
                    return 0;
                }
                sinks.Add(new Sink(SinkKind.ReadingHeader, file, Line(run), member,
                    $"the reading embed header's title run, in {member}"));
                failures.AddRange(CardFailures(file, only.Expression, run, $"the reading embed header (new Run, in {member})"));
                return 1;
            case InvocationExpressionSyntax jump when InvokedName(jump) == "JumpToSourceLink":
                return 0;
            default:
                failures.Add($"{file}:{Line(added)}: an inline the census cannot read beside the header's title: "
                    + $"`{Short(added)}`.");
                return 0;
        }
    }

    private static bool IsHeaderConsumer(InvocationExpressionSyntax call) =>
        (InvokedName(call) is "MarkEmbedHeader" or "MarkEmbedJump" && OnReadingSemantics(call))
        || CSharpSource.Normalize(call.Expression).EndsWith(".Blocks.Add", StringComparison.Ordinal);

    private static void ScanLandmark(
        string file, InvocationExpressionSyntax mark, List<Sink> sinks, List<string> failures)
    {
        string member = MemberName(mark);
        if (mark.ArgumentList.Arguments is not [_, var name])
        {
            failures.Add($"{file}:{Line(mark)}: `{Short(mark)}` is a second route into the embed landmark; the "
                + "census reads the one that names the card.");
            return;
        }
        sinks.Add(new Sink(SinkKind.ReadingLandmark, file, Line(mark), member,
            $"the reading card's landmark name, in {member}"));
        failures.AddRange(CardFailures(
            file, name.Expression, mark, $"the reading card's landmark name (ReadingSemantics.MarkEmbed, in {member})"));
    }

    private static void ScanLandmarkStore(
        string file, InvocationExpressionSyntax store, List<Sink> sinks, List<string> failures)
    {
        sinks.Add(new Sink(SinkKind.LandmarkStore, file, Line(store), MemberName(store), "the embed landmark's store"));
        ArgumentSyntax stored = store.ArgumentList.Arguments[1];
        MethodDeclarationSyntax? method = store.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        bool asGiven = method is { Identifier.ValueText: "MarkEmbed", ParameterList.Parameters: [_, var name] }
            && stored.Expression is IdentifierNameSyntax { Identifier.ValueText: var value }
            && value == name.Identifier.ValueText;
        if (!asGiven)
        {
            failures.Add($"{file}:{Line(store)}: the embed landmark stores `{Short(stored)}`, not MarkEmbed's name "
                + "as given.");
        }
    }

    private static void ScanLandmarkRead(
        string file, InvocationExpressionSyntax read, List<Sink> sinks, List<string> failures)
    {
        sinks.Add(new Sink(SinkKind.LandmarkRead, file, Line(read), MemberName(read), "the embed landmark's read"));
        MethodDeclarationSyntax? method = read.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        ExpressionSyntax? body = method?.ExpressionBody?.Expression ?? SingleReturn(method?.Body);
        if (method?.Identifier.ValueText != "EmbedNameOf" || body is null || HostComposition(body) is not null)
        {
            failures.Add($"{file}:{Line(read)}: the embed landmark is read back other than as stored: "
                + $"`{Short(body ?? read)}`.");
        }
    }

    private static bool OnReadingSemantics(InvocationExpressionSyntax call) => call.Expression switch
    {
        MemberAccessExpressionSyntax access => CSharpSource.Normalize(access.Expression) is "ReadingSemantics"
            or "Reading.ReadingSemantics" or "SlateWindows.Reading.ReadingSemantics"
            or "global::SlateWindows.Reading.ReadingSemantics",
        IdentifierNameSyntax => call.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault()
            ?.Identifier.ValueText == "ReadingSemantics",
        _ => false,
    };

    private static bool IsEmbedName(ExpressionSyntax property) =>
        CSharpSource.Normalize(property) is "EmbedNameProperty" or "ReadingSemantics.EmbedNameProperty";

    // ---- the value rule --------------------------------------------------

    /// <summary>Every failure of one card sink's value: each value that can
    /// arrive there — through locals, fields and the card arms of a switch
    /// — must be core's title fed from the resolution.</summary>
    private static IEnumerable<string> CardFailures(string file, ExpressionSyntax value, SyntaxNode sink, string what)
    {
        foreach (Leaf leaf in Leaves(value, 0))
        {
            (string Cause, SyntaxNode At)? failure = leaf.Cause is { } cause
                ? (cause, leaf.At)
                : CardLeafFailure(leaf.Value!);
            if (failure is { } found)
            {
                string via = sink.Span.Contains(found.At.Span) ? string.Empty : $" (reaching it at line {Line(sink)})";
                yield return $"{file}:{Line(found.At)}: {what} receives {found.Cause}: `{Short(found.At)}`{via}; a "
                    + $"card title is core's {CoreTitle}(...) and nothing else.";
            }
        }
    }

    private static (string Cause, SyntaxNode At)? CardLeafFailure(ExpressionSyntax leaf)
    {
        if (!IsCoreTitle(leaf, out ExpressionSyntax? resolved))
        {
            return (Composition(leaf), leaf);
        }
        return HostComposition(resolved!) is { } fed ? ($"core's title fed host text ({fed.Cause})", fed.At) : null;
    }

    private static bool IsCoreTitle(ExpressionSyntax value, out ExpressionSyntax? resolved)
    {
        resolved = null;
        if (Unwrap(value) is InvocationExpressionSyntax { ArgumentList.Arguments: [var only] } call
            && only.NameColon is null
            && only.RefKindKeyword.IsKind(SyntaxKind.None)
            && CoreTitleSpellings.Contains(CSharpSource.Normalize(call.Expression)))
        {
            resolved = only.Expression;
            return true;
        }
        return false;
    }

    /// <summary>Each value that can arrive at a sink: identifiers are
    /// followed to every assignment, a switch to its card arms (a null or
    /// an Unresolved arm of a switch over the resolution itself is not a
    /// card).</summary>
    private static IEnumerable<Leaf> Leaves(ExpressionSyntax value, int depth)
    {
        value = Unwrap(value);
        if (depth > 8)
        {
            yield return new Leaf(null, "an alias chain too deep to follow", value);
            yield break;
        }
        switch (value)
        {
            case IdentifierNameSyntax identifier:
                foreach (Leaf leaf in Assigned(identifier, depth))
                {
                    yield return leaf;
                }
                break;
            case SwitchExpressionSyntax switched:
                bool overResolution = OverTheResolution(switched.GoverningExpression, 0);
                foreach (SwitchExpressionArmSyntax arm in switched.Arms
                    .Where(arm => !(overResolution && IsNonCardPattern(arm.Pattern))))
                {
                    foreach (Leaf leaf in Leaves(arm.Expression, depth + 1))
                    {
                        yield return leaf;
                    }
                }
                break;
            default:
                yield return new Leaf(value, null, value);
                break;
        }
    }

    private static IEnumerable<Leaf> Assigned(IdentifierNameSyntax identifier, int depth)
    {
        string name = identifier.Identifier.ValueText;
        SyntaxNode scope = MemberScope(identifier);
        if (scope.DescendantNodes().Any(node => node switch
        {
            SingleVariableDesignationSyntax designation => designation.Identifier.ValueText == name,
            ForEachStatementSyntax loop => loop.Identifier.ValueText == name,
            _ => false,
        }))
        {
            yield return new Leaf(null, "a pattern, out or foreach variable", identifier);
            yield break;
        }

        VariableDeclaratorSyntax[] locals = scope.DescendantNodes().OfType<VariableDeclaratorSyntax>()
            .Where(declarator => declarator.Identifier.ValueText == name)
            .ToArray();
        if (locals.Length == 0)
        {
            if (IsParameter(identifier, name))
            {
                yield return new Leaf(null, "a parameter, whose value the census cannot see", identifier);
                yield break;
            }
            foreach (Leaf leaf in MemberValues(identifier, name, depth))
            {
                yield return leaf;
            }
            yield break;
        }

        foreach (VariableDeclaratorSyntax local in locals)
        {
            if (local.Initializer is { } initializer)
            {
                foreach (Leaf leaf in Leaves(initializer.Value, depth + 1))
                {
                    yield return leaf;
                }
            }
        }
        foreach (Leaf leaf in Writes(scope, name, depth))
        {
            yield return leaf;
        }
    }

    /// <summary>A field or property of the containing type: its initializer,
    /// its getter and every assignment in the type.</summary>
    private static IEnumerable<Leaf> MemberValues(IdentifierNameSyntax identifier, string name, int depth)
    {
        TypeDeclarationSyntax? type = identifier.FirstAncestorOrSelf<TypeDeclarationSyntax>();
        VariableDeclaratorSyntax[] fields = type?.Members.OfType<FieldDeclarationSyntax>()
            .SelectMany(field => field.Declaration.Variables)
            .Where(variable => variable.Identifier.ValueText == name)
            .ToArray() ?? [];
        PropertyDeclarationSyntax[] properties = type?.Members.OfType<PropertyDeclarationSyntax>()
            .Where(property => property.Identifier.ValueText == name)
            .ToArray() ?? [];
        if (type is null || fields.Length + properties.Length == 0)
        {
            yield return new Leaf(null, $"`{name}`, whose value the census cannot see", identifier);
            yield break;
        }

        foreach (VariableDeclaratorSyntax field in fields)
        {
            if (field.Initializer is { } initializer)
            {
                foreach (Leaf leaf in Leaves(initializer.Value, depth + 1))
                {
                    yield return leaf;
                }
            }
        }
        foreach (PropertyDeclarationSyntax property in properties)
        {
            ExpressionSyntax? computed = property.ExpressionBody?.Expression
                ?? property.AccessorList?.Accessors
                    .Where(accessor => accessor.IsKind(SyntaxKind.GetAccessorDeclaration))
                    .Select(accessor => accessor.ExpressionBody?.Expression ?? SingleReturn(accessor.Body))
                    .FirstOrDefault();
            ExpressionSyntax? source = computed ?? property.Initializer?.Value;
            if (source is not null)
            {
                foreach (Leaf leaf in Leaves(source, depth + 1))
                {
                    yield return leaf;
                }
            }
        }
        foreach (Leaf leaf in Writes(type, name, depth))
        {
            yield return leaf;
        }
    }

    private static IEnumerable<Leaf> Writes(SyntaxNode scope, string name, int depth)
    {
        foreach (AssignmentExpressionSyntax write in scope.DescendantNodes().OfType<AssignmentExpressionSyntax>()
            .Where(assignment => Target(assignment.Left) == name))
        {
            if (!write.IsKind(SyntaxKind.SimpleAssignmentExpression))
            {
                yield return new Leaf(null, $"a compound assignment ('{write.OperatorToken.Text}')", write);
                continue;
            }
            if (InNonCardSection(write))
            {
                continue;
            }
            foreach (Leaf leaf in Leaves(write.Right, depth + 1))
            {
                yield return leaf;
            }
        }
        foreach (ArgumentSyntax argument in scope.DescendantNodes().OfType<ArgumentSyntax>()
            .Where(argument => (argument.RefKindKeyword.IsKind(SyntaxKind.RefKeyword)
                    || argument.RefKindKeyword.IsKind(SyntaxKind.OutKeyword))
                && Target(argument.Expression) == name))
        {
            yield return new Leaf(null, "a ref or out argument", argument);
        }
    }

    private static string? Target(ExpressionSyntax left) => left switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax, Name: var member } => member.Identifier.ValueText,
        _ => null,
    };

    private static bool IsParameter(SyntaxNode identifier, string name) =>
        identifier.Ancestors().Any(ancestor => ancestor switch
        {
            BaseMethodDeclarationSyntax method => Named(method.ParameterList, name),
            LocalFunctionStatementSyntax local => Named(local.ParameterList, name),
            ParenthesizedLambdaExpressionSyntax lambda => Named(lambda.ParameterList, name),
            SimpleLambdaExpressionSyntax lambda => lambda.Parameter.Identifier.ValueText == name,
            AnonymousMethodExpressionSyntax anonymous => Named(anonymous.ParameterList, name),
            TypeDeclarationSyntax type => Named(type.ParameterList, name),
            IndexerDeclarationSyntax indexer => indexer.ParameterList.Parameters
                .Any(parameter => parameter.Identifier.ValueText == name),
            AccessorDeclarationSyntax accessor => name == "value"
                && (accessor.IsKind(SyntaxKind.SetAccessorDeclaration)
                    || accessor.IsKind(SyntaxKind.InitAccessorDeclaration)),
            _ => false,
        });

    private static bool Named(ParameterListSyntax? parameters, string name) =>
        parameters?.Parameters.Any(parameter => parameter.Identifier.ValueText == name) == true;

    /// <summary>What a value is, named for a failure: the composition the
    /// census recognises, or the expression itself.</summary>
    private static string Composition(ExpressionSyntax value) => Unwrap(value) switch
    {
        BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.AddExpression) => PlusConcatenation,
        BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.CoalesceExpression) => "a '??' fallback",
        ConditionalExpressionSyntax => "a conditional (?:)",
        InterpolatedStringExpressionSyntax => Interpolation,
        LiteralExpressionSyntax literal when IsText(literal) => Literal,
        InvocationExpressionSyntax call => TextCall(call)
            ?? $"a call to {CSharpSource.Normalize(call.Expression)}, not {CoreTitle}",
        BaseObjectCreationExpressionSyntax creation when IsBuilderCreation(creation) => Builder,
        var other => $"`{Short(other)}`, not {CoreTitle}",
    };

    /// <summary>The first host text anywhere in a value — a literal, an
    /// interpolation, a '+', a string method or a StringBuilder — or null.</summary>
    internal static (string Cause, SyntaxNode At)? HostComposition(SyntaxNode value)
    {
        foreach (SyntaxNode node in value.DescendantNodesAndSelf())
        {
            string? cause = node switch
            {
                LiteralExpressionSyntax literal when IsText(literal) => Literal,
                InterpolatedStringExpressionSyntax => Interpolation,
                BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.AddExpression) => PlusConcatenation,
                InvocationExpressionSyntax call => TextCall(call),
                BaseObjectCreationExpressionSyntax creation when IsBuilderCreation(creation) => Builder,
                _ => null,
            };
            if (cause is not null)
            {
                return (cause, node);
            }
        }
        return null;
    }

    private static string? TextCall(InvocationExpressionSyntax call)
    {
        string invoked = CSharpSource.Normalize(call.Expression);
        foreach ((string method, string cause) in new[]
        {
            ("Format", FormatCall),
            ("Concat", ConcatCall),
            ("Join", JoinCall),
        })
        {
            if (invoked is var spelled
                && (spelled == $"string.{method}" || spelled == $"String.{method}"
                    || spelled == $"System.String.{method}" || spelled == $"global::System.String.{method}"))
            {
                return cause;
            }
        }
        return call.DescendantNodesAndSelf().OfType<BaseObjectCreationExpressionSyntax>().Any(IsBuilderCreation)
            || ReceiverIsBuilder(call.Expression)
                ? Builder
                : null;
    }

    private static bool ReceiverIsBuilder(ExpressionSyntax expression)
    {
        for (int depth = 0; depth < 16; depth++)
        {
            switch (expression)
            {
                case MemberAccessExpressionSyntax access:
                    expression = access.Expression;
                    continue;
                case InvocationExpressionSyntax call:
                    expression = call.Expression;
                    continue;
                case IdentifierNameSyntax identifier:
                    return MemberScope(identifier).DescendantNodes().OfType<VariableDeclarationSyntax>()
                        .Where(declaration => declaration.Variables
                            .Any(variable => variable.Identifier.ValueText == identifier.Identifier.ValueText))
                        .Any(declaration => IsBuilderType(declaration.Type)
                            || declaration.Variables.Any(variable => variable.Initializer?.Value
                                .DescendantNodesAndSelf().OfType<BaseObjectCreationExpressionSyntax>()
                                .Any(IsBuilderCreation) == true));
                default:
                    return false;
            }
        }
        return false;
    }

    private static bool IsBuilderCreation(BaseObjectCreationExpressionSyntax creation) =>
        creation is ObjectCreationExpressionSyntax { Type: var type } && IsBuilderType(type);

    private static bool IsBuilderType(TypeSyntax type) => CSharpSource.Normalize(type) is "StringBuilder"
        or "System.Text.StringBuilder" or "global::System.Text.StringBuilder";

    private static bool IsText(LiteralExpressionSyntax literal) =>
        literal.IsKind(SyntaxKind.StringLiteralExpression)
        || literal.IsKind(SyntaxKind.Utf8StringLiteralExpression)
        || literal.IsKind(SyntaxKind.CharacterLiteralExpression);

    private static ExpressionSyntax Unwrap(ExpressionSyntax value)
    {
        while (true)
        {
            switch (value)
            {
                case ParenthesizedExpressionSyntax parenthesized:
                    value = parenthesized.Expression;
                    continue;
                case PostfixUnaryExpressionSyntax postfix
                    when postfix.IsKind(SyntaxKind.SuppressNullableWarningExpression):
                    value = postfix.Operand;
                    continue;
                case CastExpressionSyntax cast:
                    value = cast.Expression;
                    continue;
                default:
                    return value;
            }
        }
    }

    // ---- patterns and kinds ----------------------------------------------

    /// <summary>A null or an Unresolved arm: what arrives there is not a
    /// card, whatever it says.</summary>
    private static bool IsNonCardPattern(PatternSyntax pattern) => pattern switch
    {
        ConstantPatternSyntax constant => constant.Expression.IsKind(SyntaxKind.NullLiteralExpression)
            || IsUnresolved(constant.Expression),
        DeclarationPatternSyntax declaration => IsUnresolved(declaration.Type),
        TypePatternSyntax type => IsUnresolved(type.Type),
        RecursivePatternSyntax { Type: { } type } => IsUnresolved(type),
        _ => false,
    };

    /// <summary>Whether a switch reads the resolution itself — a parameter,
    /// or a local or member path read as it is, from one — so that a null
    /// or an Unresolved arm receives only what is not a card. A computed
    /// value (`x is FullNote ? null : x`) could steer a card into those
    /// arms, so over one every arm is read as a card.</summary>
    private static bool OverTheResolution(ExpressionSyntax governing, int depth)
    {
        ExpressionSyntax path = governing is ParenthesizedExpressionSyntax parenthesized
            ? parenthesized.Expression
            : governing;
        if (depth > 8 || !path.DescendantNodesAndSelf().All(node => node is IdentifierNameSyntax
            or MemberAccessExpressionSyntax
            or ConditionalAccessExpressionSyntax
            or MemberBindingExpressionSyntax))
        {
            return false;
        }

        IdentifierNameSyntax root = path.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>().First();
        string name = root.Identifier.ValueText;
        SyntaxNode scope = MemberScope(root);
        VariableDeclaratorSyntax[] locals = scope.DescendantNodes().OfType<VariableDeclaratorSyntax>()
            .Where(declarator => declarator.Identifier.ValueText == name)
            .ToArray();
        if (locals.Length == 0)
        {
            return IsParameter(root, name);
        }
        return locals is [{ Initializer.Value: { } initializer }]
            && !scope.DescendantNodes().OfType<AssignmentExpressionSyntax>()
                .Any(assignment => Target(assignment.Left) == name)
            && !scope.DescendantNodes().OfType<ArgumentSyntax>()
                .Any(argument => !argument.RefKindKeyword.IsKind(SyntaxKind.None)
                    && !argument.RefKindKeyword.IsKind(SyntaxKind.InKeyword)
                    && Target(argument.Expression) == name)
            && !scope.DescendantNodes().OfType<SingleVariableDesignationSyntax>()
                .Any(designation => designation.Identifier.ValueText == name)
            && OverTheResolution(initializer, depth + 1);
    }

    private static bool InNonCardSection(SyntaxNode node) =>
        node.FirstAncestorOrSelf<SwitchSectionSyntax>() is { Parent: SwitchStatementSyntax statement } section
        && OverTheResolution(statement.Expression, 0)
        && section.Labels.All(label => label switch
        {
            CaseSwitchLabelSyntax constant => constant.Value.IsKind(SyntaxKind.NullLiteralExpression)
                || IsUnresolved(constant.Value),
            CasePatternSwitchLabelSyntax pattern => IsNonCardPattern(pattern.Pattern),
            _ => false,
        });

    private static bool IsUnresolved(SyntaxNode type) =>
        ResolutionKind(type) == nameof(EmbedResolution.Unresolved);

    /// <summary>`EmbedResolution.X`, however qualified, is X.</summary>
    private static string? ResolutionKind(SyntaxNode type)
    {
        string spelled = CSharpSource.Normalize(type);
        foreach (string prefix in new[]
        {
            "EmbedResolution.",
            "uniffi.slate_uniffi.EmbedResolution.",
            "global::uniffi.slate_uniffi.EmbedResolution.",
        })
        {
            if (spelled.StartsWith(prefix, StringComparison.Ordinal) && !spelled[prefix.Length..].Contains('.'))
            {
                return spelled[prefix.Length..];
            }
        }
        return null;
    }

    /// <summary>The resolved kind a card is built for: the nearest switch
    /// arm or case naming one card kind, or the one card-kind parameter of
    /// the method it is built in.</summary>
    private static string? CardKind(SyntaxNode creation, out string? problem)
    {
        problem = null;
        foreach (SyntaxNode ancestor in creation.Ancestors())
        {
            string[] kinds;
            bool member;
            switch (ancestor)
            {
                case SwitchExpressionArmSyntax arm:
                    kinds = PatternKinds(arm.Pattern).Distinct().ToArray();
                    member = false;
                    break;
                case SwitchSectionSyntax section:
                    kinds = section.Labels.SelectMany(LabelKinds).Distinct().ToArray();
                    member = false;
                    break;
                case BaseMethodDeclarationSyntax method:
                    kinds = ParameterKinds(method.ParameterList).Distinct().ToArray();
                    member = true;
                    break;
                case LocalFunctionStatementSyntax local:
                    kinds = ParameterKinds(local.ParameterList).Distinct().ToArray();
                    member = true;
                    break;
                case MemberDeclarationSyntax:
                    kinds = [];
                    member = true;
                    break;
                default:
                    continue;
            }

            if (kinds.Length == 1)
            {
                return kinds[0];
            }
            if (kinds.Length > 1)
            {
                problem = $"a card built for several resolved kinds at once ({string.Join(", ", kinds)}); the "
                    + "census attributes each card to one.";
                return null;
            }
            if (member)
            {
                break;
            }
        }
        problem = "a card the census cannot attribute to a resolved kind — build it in a switch arm or a "
            + "method over one EmbedResolution kind.";
        return null;
    }

    private static IEnumerable<string> PatternKinds(PatternSyntax pattern) => pattern switch
    {
        ConstantPatternSyntax constant => CardKindOf(constant.Expression),
        DeclarationPatternSyntax declaration => CardKindOf(declaration.Type),
        TypePatternSyntax type => CardKindOf(type.Type),
        RecursivePatternSyntax { Type: { } type } => CardKindOf(type),
        BinaryPatternSyntax binary => PatternKinds(binary.Left).Concat(PatternKinds(binary.Right)),
        ParenthesizedPatternSyntax parenthesized => PatternKinds(parenthesized.Pattern),
        _ => [],
    };

    private static IEnumerable<string> LabelKinds(SwitchLabelSyntax label) => label switch
    {
        CasePatternSwitchLabelSyntax pattern => PatternKinds(pattern.Pattern),
        CaseSwitchLabelSyntax constant => CardKindOf(constant.Value),
        _ => [],
    };

    private static IEnumerable<string> ParameterKinds(ParameterListSyntax parameters) =>
        parameters.Parameters.Where(parameter => parameter.Type is not null)
            .SelectMany(parameter => CardKindOf(parameter.Type!));

    private static IEnumerable<string> CardKindOf(SyntaxNode type) =>
        ResolutionKind(type) is { } kind && CardKinds.Value.Contains(kind) ? [kind] : [];

    // ---- decoys and small helpers -----------------------------------------

    /// <summary>The sink rule recognises core by its spelling, so no host
    /// declaration or alias may answer to that spelling.</summary>
    internal static IEnumerable<string> DecoyFailures(SyntaxNode root, string file)
    {
        foreach (SyntaxNode node in root.DescendantNodes())
        {
            string? declared = node switch
            {
                BaseTypeDeclarationSyntax { Identifier.ValueText: "SlateUniffiMethods" } => "SlateUniffiMethods",
                MethodDeclarationSyntax { Identifier.ValueText: "ResolvedEmbedTitle" or "A11yRender" } method =>
                    method.Identifier.ValueText,
                LocalFunctionStatementSyntax { Identifier.ValueText: "ResolvedEmbedTitle" or "A11yRender" } local =>
                    local.Identifier.ValueText,
                _ => null,
            };
            if (declared is not null)
            {
                yield return $"{file}:{Line(node)}: the host declares {declared}, which the sink rule would read "
                    + "as core's; core's is the binding's.";
            }
            if (node is UsingDirectiveSyntax { Alias.Name.Identifier.ValueText: "SlateUniffiMethods" } alias)
            {
                yield return $"{file}:{Line(node)}: the host aliases SlateUniffiMethods to "
                    + $"`{Short(alias.NamespaceOrType)}`, which the sink rule would read as core's.";
            }
        }
    }

    private static IEnumerable<AssignmentExpressionSyntax> TitleWrites(InitializerExpressionSyntax? initializer) =>
        initializer?.Expressions.OfType<AssignmentExpressionSyntax>()
            .Where(assignment => assignment.Left is IdentifierNameSyntax { Identifier.ValueText: "Title" })
        ?? [];

    private static ExpressionSyntax? ArgumentFor(ArgumentListSyntax? list, string name, int position)
    {
        if (list is null)
        {
            return null;
        }
        ArgumentSyntax? named = list.Arguments.FirstOrDefault(
            argument => argument.NameColon?.Name.Identifier.ValueText == name);
        if (named is not null)
        {
            return named.Expression;
        }
        return position < list.Arguments.Count
            && list.Arguments.Take(position + 1).All(argument => argument.NameColon is null)
                ? list.Arguments[position].Expression
                : null;
    }

    private static bool IsBoolean(ExpressionSyntax? value, bool expected) =>
        value is LiteralExpressionSyntax literal
        && literal.IsKind(expected ? SyntaxKind.TrueLiteralExpression : SyntaxKind.FalseLiteralExpression);

    private static string? RightmostName(TypeSyntax type) => type switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
        AliasQualifiedNameSyntax alias => alias.Name.Identifier.ValueText,
        GenericNameSyntax generic => generic.Identifier.ValueText,
        _ => null,
    };

    private static string? InvokedName(InvocationExpressionSyntax call) => call.Expression switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        GenericNameSyntax generic => generic.Identifier.ValueText,
        MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
        MemberBindingExpressionSyntax binding => binding.Name.Identifier.ValueText,
        _ => null,
    };

    private static string MemberName(SyntaxNode node) => node.Ancestors()
        .Select(ancestor => ancestor switch
        {
            LocalFunctionStatementSyntax local => local.Identifier.ValueText,
            MethodDeclarationSyntax method => method.Identifier.ValueText,
            ConstructorDeclarationSyntax constructor => constructor.Identifier.ValueText,
            PropertyDeclarationSyntax property => property.Identifier.ValueText,
            FieldDeclarationSyntax field => field.Declaration.Variables.First().Identifier.ValueText,
            BaseTypeDeclarationSyntax type => type.Identifier.ValueText,
            _ => null,
        })
        .FirstOrDefault(name => name is not null) ?? "<file>";

    /// <summary>The member a node belongs to — a method, constructor,
    /// property or field, never a lambda or local function inside one — or
    /// the file.</summary>
    private static SyntaxNode MemberScope(SyntaxNode node) =>
        node.Ancestors().FirstOrDefault(ancestor => ancestor is MemberDeclarationSyntax
            and not BaseTypeDeclarationSyntax
            and not BaseNamespaceDeclarationSyntax)
        ?? node.SyntaxTree.GetRoot();

    private static ExpressionSyntax? SingleReturn(BlockSyntax? body) =>
        body?.Statements is [ReturnStatementSyntax { Expression: { } returned }] ? returned : null;

    private static ExpressionSyntax? SingleStatement(BlockSyntax? body) =>
        body?.Statements is [ExpressionStatementSyntax { Expression: var expression }] ? expression : null;

    private static int Line(SyntaxNode node) =>
        node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

    private static string Short(SyntaxNode node)
    {
        string text = string.Join(' ', node.ToString().Split(
            (char[])[' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries));
        return text.Length <= 140 ? text : text[..137] + "...";
    }
}
