// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Plan 47 CB-20 (#1708, CD12): NVDA reads the reading view line by line as the
// caret's Line through the surface's Text pattern, where an embedded element
// reads as one space and its role. A line holding an element and no text
// therefore reads "grouping", or "blank", and nothing about what is there.
// This census holds every kind of element the reading view builds into its
// document: each kind has a row, each row says what its line must read, every
// row is met by the census note, every block kind core makes is in that note,
// every run that is in range for line reading only is marked so find and copy
// can skip it, and every place the shell builds an element into a flow
// document is a listed site.

using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Automation.Text;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using SlateWindows.Reading;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "reading-embedded-elements")]
public sealed class ReadingEmbeddedElementCensus
{
    /// <summary>One of each block kind core makes, and every element the
    /// reading view builds: drawn math, a WPF-Math coverage gap, a rendered
    /// diagram, an unsupported one, a task, a rule, a PNG and an SVG embed.</summary>
    internal const string Note =
        "# Census\n\nA paragraph with a [[elsewhere]] link.\n\n- [ ] a task to do\n\n> A quote.\n\n"
        + "```rust\nfn f() {}\n```\n\n$$\\nabla^2 f$$\n\n$$\\mathbb{R}$$\n\n"
        + "```mermaid\nflowchart LR\nA --> B\nB --> C\n```\n\n```mermaid\nweirdDiagram\nstuff\n```\n\n"
        + "| a | b |\n| --- | --- |\n| 1 | 2 |\n\n---\n\n<div>html</div>\n\n"
        + "![[pic.png]]\n\n![[pic.svg]]\n\nThe end.\n";

    private static readonly IReadOnlyDictionary<string, byte[]> Files = new Dictionary<string, byte[]>
    {
        ["pic.png"] = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8DwHwAFBQIAX8jx0gAAAABJRU5ErkJggg=="),
        ["pic.svg"] = System.Text.Encoding.UTF8.GetBytes(
            "<svg xmlns='http://www.w3.org/2000/svg' width='40' height='20'><rect width='40' height='20' fill='teal'/></svg>"),
    };

    /// <summary>The closed table: a kind of embedded element, told apart by
    /// its type and, for a diagram, its state.</summary>
    internal enum Row
    {
        /// <summary>A formula with core's speech: its line holds the speech.</summary>
        MathSpoken,

        /// <summary>A formula with no core artifact: its source is the text.</summary>
        MathUnmatched,

        /// <summary>A rendered diagram: its line holds its description.</summary>
        DiagramRendered,

        /// <summary>A diagram still rendering: its line holds "Diagram, rendering".</summary>
        DiagramPending,

        /// <summary>A diagram that failed: its line is its source's, never its
        /// description (CD12(a)).</summary>
        DiagramFailed,

        /// <summary>A task's checkbox: its line holds the item's text.</summary>
        Task,

        /// <summary>A thematic break: an enabled Separator with no name that
        /// takes no keys.</summary>
        Rule,

        /// <summary>An embed card's image: no peer, and its line holds the
        /// card's name (the owner's answer to CB-20's Call 4).</summary>
        EmbedImage,
    }

    private static Row? RowOf(UIElement child, Paragraph? host) => child switch
    {
        ReadingMathElement when host is not null && ReadingSemantics.CopyTextOf(host) is null => Row.MathUnmatched,
        ReadingMathElement => Row.MathSpoken,
        ReadingDiagramElement when host is not null && ReadingSemantics.PendingDiagramKeyOf(host) is not null => Row.DiagramPending,
        ReadingDiagramElement { Content: ReadingDiagramImage } => Row.DiagramRendered,
        ReadingDiagramElement => Row.DiagramFailed,
        CheckBox => Row.Task,
        Separator => Row.Rule,
        ReadingDiagramImage => Row.EmbedImage,
        _ => null,
    };

    private static void Run(Action body) =>
        StaThread.RunPumped(
            body, UnhurriedSvgDecodes.LandingWait(3) + TimeSpan.FromSeconds(60), "the census's STA body timed out.");

    [Fact]
    public void EveryEmbeddedElementsLineCarriesItsText() => Run(() =>
    {
        var wrong = new List<string>();
        var met = new HashSet<Row>();
        using (var pending = ReadingDiagramTests.StageHarness.Open("census-embedded-pending", Note, hosted: true, files: Files))
        {
            AwaitImages(pending.Surface);
            Judge("pending", pending.Surface, wrong, met);
            pending.Land();
            AwaitImages(pending.Surface);
            Judge("landed", pending.Surface, wrong, met);
        }

        FlowDocument unmatched = Unmatched();
        foreach ((TextElement container, UIElement child, Paragraph? host) in Containers(unmatched))
        {
            if (RowOf(child, host) is not Row.MathUnmatched)
            {
                continue;
            }
            met.Add(Row.MathUnmatched);
            string name = AutomationProperties.GetName(child);
            string text = new TextRange(host!.ContentStart, host.ContentEnd).Text;
            if (name.Length == 0 || !text.Contains(name, StringComparison.Ordinal))
            {
                wrong.Add($"unmatched math: its paragraph reads \"{text}\", not its source \"{name}\"");
            }
        }

        Assert.True(wrong.Count == 0, "Embedded elements whose line does not say what they are:\n  " + string.Join("\n  ", wrong));
        Row[] unmet = Enum.GetValues<Row>().Where(row => !met.Contains(row)).ToArray();
        Assert.True(unmet.Length == 0, "Rows the census note never met (add the case to the note): " + string.Join(", ", unmet));
    });

    [Fact]
    public void TheCensusNoteHoldsEveryReadingBlockKind()
    {
        HashSet<Type> made = SlateUniffiMethods.ReadingBlocksSource(Note).Select(block => block.Kind.GetType()).ToHashSet();
        Type[] kinds = typeof(ReadingBlockKind).GetNestedTypes()
            .Where(type => type.IsSubclassOf(typeof(ReadingBlockKind)))
            .ToArray();
        Assert.True(kinds.Length >= 10, $"premise: only {kinds.Length} block kinds were found by reflection");
        Type[] missing = kinds.Where(kind => !made.Contains(kind)).ToArray();
        Assert.True(
            missing.Length == 0,
            "Block kinds the census note does not hold (add one, and its row if it builds an element): "
            + string.Join(", ", missing.Select(kind => kind.Name)));
    }

    [Fact]
    public void EveryHiddenRunIsMarked() => Run(() =>
    {
        var wrong = new List<string>();
        int hidden = 0;
        void Walk(string state, FlowDocument document)
        {
            foreach (Run run in Runs(document.Blocks))
            {
                bool looksHidden = run.Foreground is SolidColorBrush { Color.A: 0 } || run.FontSize < 1;
                bool marked = ReadingSemantics.IsHiddenSpeech(run);
                if (looksHidden != marked)
                {
                    wrong.Add($"{state}: \"{run.Text}\" looks {(looksHidden ? "hidden" : "shown")} and is {(marked ? "" : "not ")}marked");
                }
                if (marked)
                {
                    hidden++;
                    if (run.NextInline is not InlineUIContainer { Child: { } })
                    {
                        wrong.Add($"{state}: the hidden \"{run.Text}\" does not stand before the element it speaks for");
                    }
                }
            }
        }

        using (var harness = ReadingDiagramTests.StageHarness.Open("census-hidden-runs", Note, hosted: true, files: Files))
        {
            AwaitImages(harness.Surface);
            Walk("pending", harness.Surface.Document);
            harness.Land();
            AwaitImages(harness.Surface);
            Walk("landed", harness.Surface.Document);
        }
        Walk("unmatched", Unmatched());

        Assert.True(hidden >= 6, $"premise: only {hidden} hidden runs were met");
        Assert.True(wrong.Count == 0, "Runs whose mark and look disagree:\n  " + string.Join("\n  ", wrong));
    });

    /// <summary>Every place the shell builds an element into a flow document,
    /// by its method and the static type of the element it places, and the
    /// rows the elements it builds are judged by.</summary>
    private static readonly (Site Site, Row[] Rows)[] Listed =
    [
        (new("ReadingDocumentBuilder.NonListBlock", "Separator"), [Row.Rule]),
        (new("ReadingDocumentBuilder.MathBlockElement", "ReadingMathElement"), [Row.MathUnmatched]),
        (new("ReadingDocumentBuilder.MathBlockElement", "ReadingMathElement"), [Row.MathSpoken]),
        (new("ReadingDocumentBuilder.DiagramBlockElement", "ReadingDiagramElement"), [Row.DiagramPending]),
        (new("ReadingDocumentBuilder.ContainerOf", "ReadingDiagramElement"), [Row.DiagramRendered, Row.DiagramFailed]),
        (new("ReadingDocumentBuilder.AppendEmbedImage", "ReadingDiagramImage"), [Row.EmbedImage]),
        (new("ReadingDocumentBuilder.InlineParagraph", "CheckBox"), [Row.Task]),
    ];

    internal sealed record Site(string Member, string Placed);

    [Fact]
    public void EveryEmbeddedElementSiteIsInTheCensus()
    {
        List<Site> found = SitesIn(ShellCompilation.Compilation, ShellCompilation.Sources.Select(source => source.Source.Root.SyntaxTree));
        string[] actual = found.Select(site => $"{site.Member} places {site.Placed}").Order(StringComparer.Ordinal).ToArray();
        string[] expected = Listed.Select(row => $"{row.Site.Member} places {row.Site.Placed}").Order(StringComparer.Ordinal).ToArray();
        Assert.True(
            actual.SequenceEqual(expected),
            "The sites that build an element into a flow document are not the census's (add a row and its line fact):\n"
            + "  found:  " + string.Join("\n          ", actual) + "\n  listed: " + string.Join("\n          ", expected));
        Assert.Equal(Enum.GetValues<Row>().Order(), Listed.SelectMany(row => row.Rows).Distinct().Order());
    }

    [Fact]
    public void TheSiteCensusSeesEveryRoute()
    {
        SyntaxTree tree = CSharpSyntaxTree.ParseText("""
            using System.Windows;
            using System.Windows.Controls;
            using System.Windows.Documents;

            internal sealed class PlantedBlock : BlockUIContainer
            {
                public PlantedBlock(UIElement child) : base(child) { }
            }

            internal static class Planted
            {
                internal static void Explicit(Paragraph paragraph) => paragraph.Inlines.Add(new InlineUIContainer(new CheckBox()));
                internal static void TargetTyped(Paragraph paragraph) { InlineUIContainer container = new(new Button()); paragraph.Inlines.Add(container); }
                internal static void Implicit(Paragraph paragraph) => paragraph.Inlines.Add(new Image());
                internal static Block Initializer() => new BlockUIContainer { Child = new Border() };
                internal static void Assigned(BlockUIContainer block) => block.Child = new TextBox();
                internal static Block Derived() => new PlantedBlock(new Slider());
            }
            """);
        CSharpCompilation compilation = ShellCompilation.Compilation.RemoveAllSyntaxTrees().AddSyntaxTrees(tree);
        string[] found = SitesIn(compilation, [tree]).Select(site => $"{site.Member} places {site.Placed}").Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(
            new[]
            {
                "Planted.Assigned places TextBox",
                "Planted.Derived places Slider",
                "Planted.Explicit places CheckBox",
                "Planted.Implicit places Image",
                "Planted.Initializer places Border",
                "Planted.TargetTyped places Button",
            },
            found);
    }

    /// <summary>The sites in <paramref name="trees"/>, bound: an object
    /// creation of a flow container (or a type derived from one) that is
    /// given its element, a call that binds to <c>InlineCollection.Add(UIElement)</c>,
    /// and an assignment to a flow container's <c>Child</c>, in an
    /// initializer or after. XAML flow content is outside it; the shell
    /// declares none.</summary>
    private static List<Site> SitesIn(CSharpCompilation compilation, IEnumerable<SyntaxTree> trees)
    {
        INamedTypeSymbol inline = compilation.GetTypeByMetadataName("System.Windows.Documents.InlineUIContainer")!;
        INamedTypeSymbol block = compilation.GetTypeByMetadataName("System.Windows.Documents.BlockUIContainer")!;
        INamedTypeSymbol inlines = compilation.GetTypeByMetadataName("System.Windows.Documents.InlineCollection")!;
        INamedTypeSymbol element = compilation.GetTypeByMetadataName("System.Windows.UIElement")!;
        Assert.True(inline is not null && block is not null && inlines is not null && element is not null, "premise: WPF's flow types did not bind");

        bool IsContainer(ITypeSymbol? type)
        {
            for (ITypeSymbol? at = type; at is not null; at = at.BaseType)
            {
                if (SymbolEqualityComparer.Default.Equals(at, inline) || SymbolEqualityComparer.Default.Equals(at, block))
                {
                    return true;
                }
            }
            return false;
        }

        static string PlacedBy(IOperation value) =>
            (value is IConversionOperation conversion ? conversion.Operand : value).Type?.Name ?? "?";

        var sites = new List<Site>();
        foreach (SyntaxTree tree in trees)
        {
            SemanticModel model = compilation.GetSemanticModel(tree);
            foreach (SyntaxNode node in tree.GetRoot().DescendantNodes())
            {
                string? placed = model.GetOperation(node) switch
                {
                    IObjectCreationOperation creation when IsContainer(creation.Type) && creation.Arguments.Length > 0 =>
                        PlacedBy(creation.Arguments[0].Value),
                    IInvocationOperation call when SymbolEqualityComparer.Default.Equals(call.TargetMethod.ContainingType, inlines)
                        && call.TargetMethod.Parameters.Length == 1
                        && SymbolEqualityComparer.Default.Equals(call.TargetMethod.Parameters[0].Type, element) =>
                        PlacedBy(call.Arguments[0].Value),
                    ISimpleAssignmentOperation { Target: IPropertyReferenceOperation { Property.Name: "Child" } property } assignment
                        when IsContainer(property.Property.ContainingType) =>
                        PlacedBy(assignment.Value),
                    _ => null,
                };
                if (placed is null)
                {
                    continue;
                }
                MemberDeclarationSyntax member = node.Ancestors().OfType<MemberDeclarationSyntax>()
                    .First(candidate => candidate is BaseMethodDeclarationSyntax or PropertyDeclarationSyntax or FieldDeclarationSyntax);
                string type = member.Ancestors().OfType<BaseTypeDeclarationSyntax>().First().Identifier.Text;
                string name = member switch
                {
                    MethodDeclarationSyntax method => method.Identifier.Text,
                    ConstructorDeclarationSyntax constructor => constructor.Identifier.Text,
                    PropertyDeclarationSyntax property => property.Identifier.Text,
                    _ => member.Kind().ToString(),
                };
                sites.Add(new Site($"{type}.{name}", placed));
            }
        }
        return sites;
    }

    private static void Judge(string state, ReadingSurface surface, List<string> wrong, HashSet<Row> met)
    {
        foreach ((TextElement container, UIElement child, Paragraph? host) in Containers(surface.Document))
        {
            if (RowOf(child, host) is not { } row)
            {
                wrong.Add($"{state}: a new embedded kind, {child.GetType().Name}: add its row and its line fact");
                continue;
            }
            met.Add(row);
            string name = AutomationProperties.GetName(child);
            string where = $"{state}: {row} ({child.GetType().Name} \"{name}\")";
            if (row == Row.Rule)
            {
                AutomationPeer peer = UIElementAutomationPeer.CreatePeerForElement(child);
                if (peer.GetAutomationControlType() != AutomationControlType.Separator
                    || !peer.IsEnabled()
                    || peer.GetName().Length > 0
                    || peer.IsKeyboardFocusable())
                {
                    wrong.Add($"{where}: its peer is {peer.GetAutomationControlType()}, enabled {peer.IsEnabled()}, named \"{peer.GetName()}\", keyboard focusable {peer.IsKeyboardFocusable()}");
                }
                continue;
            }
            if (container is not InlineUIContainer inlineContainer)
            {
                wrong.Add($"{where}: it stands alone in a block, so its line has no text");
                continue;
            }
            string line = Words(LineAt(surface, inlineContainer.ElementStart));
            switch (row)
            {
                case Row.EmbedImage:
                    string card = inlineContainer.Parent is Paragraph { Parent: Section section } ? ReadingSemantics.EmbedNameOf(section) : string.Empty;
                    if (UIElementAutomationPeer.CreatePeerForElement(child) is not null)
                    {
                        wrong.Add($"{where}: it has a peer");
                    }
                    if (card.Length == 0 || !line.Contains(card, StringComparison.Ordinal))
                    {
                        wrong.Add($"{where}: its line reads \"{line}\", not its card's name \"{card}\"");
                    }
                    break;
                case Row.DiagramFailed:
                    if (line.Length == 0 || line.Contains(name, StringComparison.Ordinal))
                    {
                        wrong.Add($"{where}: its line reads \"{line}\"");
                    }
                    break;
                default:
                    if (name.Length == 0 || !line.Contains(name.Replace('\n', ' '), StringComparison.Ordinal))
                    {
                        wrong.Add($"{where}: its line reads \"{line}\"");
                    }
                    break;
            }
        }
    }

    /// <summary>The census note built with no core artifacts: math with no
    /// artifact shows its source.</summary>
    private static FlowDocument Unmatched()
    {
        ReadingBlock[] blocks = SlateUniffiMethods.ReadingBlocksSource(Note);
        ReadingBlockInlines[] inlines = SlateUniffiMethods.ReadingInlineSegmentsSource(
            Note, Array.Empty<RenderedCitation>(), Array.Empty<OutgoingLink>());
        return ReadingDocumentBuilder.Build(
            blocks.Zip(inlines, (block, inline) => (block, inline)).ToList(),
            new ReadingListBuildContext(),
            Array.Empty<CodeBlock>(),
            Array.Empty<MathBlock>(),
            Array.Empty<DiagramBlock>()).Document;
    }

    private static void AwaitImages(ReadingSurface surface) =>
        Assert.True(
            PumpedDispatcher.PumpUntil(
                () => Containers(surface.Document).Count(entry => entry.Child is ReadingDiagramImage { Source: not null }) == 2,
                UnhurriedSvgDecodes.LandingWait(2)),
            "the census note's two embedded images never showed");

    private static string LineAt(ReadingSurface surface, TextPointer at)
    {
        surface.Selection.Select(at, at);
        surface.CaretPosition = at;
        PumpedDispatcher.Drain();
        var provider = (ITextProvider)UIElementAutomationPeer.CreatePeerForElement(surface).GetPattern(PatternInterface.Text);
        ITextRangeProvider line = provider.GetSelection()[0];
        line.ExpandToEnclosingUnit(TextUnit.Line);
        return line.GetText(-1);
    }

    private static string Words(string line) => line.Trim(' ', '￼', '\r', '\n');

    private static IEnumerable<(TextElement Container, UIElement Child, Paragraph? Host)> Containers(FlowDocument document) =>
        Blocks(document.Blocks).SelectMany(Containers);

    private static IEnumerable<(TextElement Container, UIElement Child, Paragraph? Host)> Containers(Block block) => block switch
    {
        BlockUIContainer { Child: { } child } container => [(container, child, null)],
        Paragraph paragraph => Inlines(paragraph.Inlines)
            .OfType<InlineUIContainer>()
            .Where(container => container.Child is not null)
            .Select(container => ((TextElement)container, container.Child, (Paragraph?)paragraph)),
        _ => [],
    };

    private static IEnumerable<Block> Blocks(IEnumerable<Block> blocks)
    {
        foreach (Block block in blocks)
        {
            yield return block;
            IEnumerable<Block> inner = block switch
            {
                Section section => section.Blocks,
                List list => list.ListItems.SelectMany(item => item.Blocks),
                Table table => table.RowGroups.SelectMany(group => group.Rows).SelectMany(row => row.Cells).SelectMany(cell => cell.Blocks),
                _ => [],
            };
            foreach (Block nested in Blocks(inner))
            {
                yield return nested;
            }
        }
    }

    private static IEnumerable<Inline> Inlines(IEnumerable<Inline> inlines)
    {
        foreach (Inline inline in inlines)
        {
            yield return inline;
            if (inline is Span span)
            {
                foreach (Inline nested in Inlines(span.Inlines))
                {
                    yield return nested;
                }
            }
        }
    }

    private static IEnumerable<Run> Runs(IEnumerable<Block> blocks) =>
        Blocks(blocks).OfType<Paragraph>().SelectMany(paragraph => Inlines(paragraph.Inlines)).OfType<Run>();
}
