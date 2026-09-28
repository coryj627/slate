// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// #1278 (locked decision 05 §1.1, contract 38): a resolved embed card's
// title is core's words. Core renders it once — resolved_embed_title, the
// UniFFI export SlateUniffiMethods.ResolvedEmbedTitle — and the host shows
// that rendering wherever a reader meets a card: the Ctrl+E popover's header
// (followed only by the " — source line N" locator), its cards and their UIA
// names, the embeds leaf's cards, and the reading view's card and nested
// headers and landmark name. The popover's own UIA name is core's rendering
// of the EmbedPreviewShown it announces.
//
// THE PRIMARY WITNESS IS AT RUNTIME: EmbedTitleRealizedSurfaceTests realizes
// the popover, the embeds leaf and the reading card under the app's real
// resources, in every theme, and compares what a reader gets with core's
// rendering computed at test time. This census is the SECONDARY net: it
// names a regression at its file and line, early and cheaply, but a static
// reading cannot enumerate every way WPF composes text, and it does not try
// to (codex round 3; the coordinator's ruling). Accepted residue, covered by
// the runtime witness and recorded in contract 38's EmbedPreviewShown row:
// text composed by a route this file does not read — a name or header set
// on a card from outside the renderer, a style, template or DataTemplate
// applied from code or reached by a key it cannot resolve, a template on a
// control inside the Expander's own template, a behavior or peer override
// elsewhere. Where a route is cheap to read, it is read: every name and text
// the renderer writes is a card's Title or a registered form and no name is
// read back (codex round 3's second SetName), and XAML types are resolved
// through the namespace map (codex round 3's `{x:Type wpf:Expander}`).
//
// History: the runtime facts (W2EditorInteractionTests, ReadingEmbedTests)
// prove today's TEXT but not PROVENANCE. This census's first form
// blacklisted the title phrases in host literals, and codex round 2 showed
// why that cannot hold: "Embedded " + "note: " + path spells the phrase in
// no one literal and reads identically.
//
// So the census reads provenance AT THE SINKS — every place a card title is
// handed on toward a reader — and asks what arrives there:
//
// - The sinks are ENUMERATED from the sources by what they write, never
//   listed by name: every EditorEmbedPreviewNode built (however it is spelled
//   — a target-typed new, an alias, a `with`), every read of a node's Title,
//   every write of the popover's header and name, every inline of a reading
//   embed header and every landmark name. The population is pinned: one card
//   per resolved kind (the kinds come from the binding's EmbedResolution), the
//   registered warnings, the renderer's six, the reading view's three, the
//   popover's four writers and the one locator helper with its one caller.
// - What arrives at a card sink is core's ResolvedEmbedTitle(...) — through
//   locals if need be, every assignment — fed from the resolution. A '+', an
//   interpolation, string.Format / Concat / Join, a StringBuilder, a literal,
//   a conditional, a parameter or any other call fails, naming file and line.
//   The popover header is core's title passed through WithSourceLineLocator,
//   which appends the locator and nothing else, from its one caller.
// - The hops from a sink to the reader are pass-throughs and are read too:
//   the node record's Title; the popover properties' accessors and backing
//   fields, and the peer the popover's host hands its name through; the
//   renderer's Header / Text / UIA Name, on WPF's own controls with no
//   format, template or style of the renderer's, and every other name or
//   text it writes a registered form; the landmark's store and read.
// - The XAML that binds the header, the popover image's name, the popover's
//   name and each card root is a plain {Binding}: no StringFormat, converter,
//   fallback text or MultiBinding, and no static sibling text or static name
//   beside it; and no Expander style or template, nor a template for every
//   string, with the type read through the namespace map
//   (EmbedPreviewTitleCensus.Xaml.cs).
//
// The phrase blacklist stays as a second net, and the publisher facts still
// read the one EmbedPreviewShown: its data is the content's Resolved and the
// popover's UIA name is the rendering of the event it announces. The mutation
// rows apply each regression to the shipped sources in memory — the mutant
// must parse and bind like the shipped file — and require the census to name
// its cause at its file.

using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit.Abstractions;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "embed-preview-title")]
public sealed partial class EmbedPreviewTitleCensus
{
    private const string Publisher = "PublishEmbedPreview";
    private const string Locator = " — source line ";

    private readonly ITestOutputHelper _output;

    public EmbedPreviewTitleCensus(ITestOutputHelper output)
    {
        _output = output;
    }

    [GeneratedRegex(@"Embedded (note|section|block|image|base)\b", RegexOptions.IgnoreCase)]
    private static partial Regex CardShape();

    /// <summary>Every sink a card title reaches receives core's title and
    /// nothing else; the inventory is written to the test output.</summary>
    [Fact]
    public void EveryTitleSinkReceivesCoresTitleAlone()
    {
        ShellScan shell = ShellScan.Baseline.Value;
        foreach (Sink sink in shell.Sinks)
        {
            _output.WriteLine($"{sink.Kind,-18} {sink.File}:{sink.Line}  {sink.What}");
        }

        Assert.True(shell.Failures.Length == 0, string.Join("\n", shell.Failures));
    }

    /// <summary>The sink population is what the census expects, so a sink
    /// that is renamed, moved or rebuilt another way cannot leave its
    /// scope unread.</summary>
    [Fact]
    public void TheTitleSinkPopulationIsPinned()
    {
        string[] failures = PopulationFailures(ShellScan.Baseline.Value.Files).ToArray();
        Assert.True(failures.Length == 0, string.Join("\n", failures));
    }

    /// <summary>The XAML binds each title sink plainly; the bindings it
    /// read are written to the test output.</summary>
    [Fact]
    public void EveryXamlTitleSinkBindsPlainly()
    {
        IReadOnlyList<(string Relative, string Text)> files = XamlSources();
        Assert.Contains(files, file => file.Relative == "WorkspaceTemplates.xaml");
        Assert.Contains(files, file => file.Relative == "MainWindow.xaml");
        foreach (XamlSink sink in XamlScan(files).Sinks)
        {
            _output.WriteLine($"{sink.Where}  {sink.Element}.{sink.Attribute} = {sink.Value}");
        }

        string[] failures = XamlFailures(files).ToArray();
        Assert.True(failures.Length == 0, string.Join("\n", failures));
    }

    /// <summary>The second net: no host source spells a card-title shape in
    /// one literal or interpolation, and every ResolvedEmbed the host builds
    /// is filled from the resolution.</summary>
    [Fact]
    public void NoHostSourceSpellsACardTitle()
    {
        string root = SourceText.ShellSourceRoot();
        string[] files = Directory
            .EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(root, path)
                .Split(Path.DirectorySeparatorChar)
                .Any(part => part is "obj" or "bin"))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        Assert.True(files.Length > 50, $"only {files.Length} host sources enumerated");

        string[] failures = files
            .SelectMany(file => SpellingFailures(
                CSharpSource.LoadPath(file).Root,
                Path.GetRelativePath(root, file).Replace('\\', '/')))
            .ToArray();
        Assert.True(failures.Length == 0, string.Join("\n", failures));
    }

    [Fact]
    public void ThePreviewSpeaksNamesAndTitlesItFromWhatItResolvedTo()
    {
        CSharpSource source = CSharpSource.Load("EditorInteractions.cs");
        ObjectCreationExpressionSyntax[] constructions = source.Root.DescendantNodes()
            .OfType<ObjectCreationExpressionSyntax>()
            .Where(creation => CSharpSource.Normalize(creation.Type) == "A11yEvent.EmbedPreviewShown")
            .ToArray();
        Assert.True(
            constructions.Length == 1
                && constructions[0].Ancestors().OfType<MethodDeclarationSyntax>().First()
                    .Identifier.ValueText == Publisher,
            $"expected one EmbedPreviewShown, in {Publisher}; found {constructions.Length}");

        string[] failures = PublisherFailures(source.Method(Publisher), "EditorInteractions.cs").ToArray();
        Assert.True(failures.Length == 0, string.Join("\n", failures));
    }

    /// <summary>The spelling net's teeth: a card shape in a literal or an
    /// interpolation is named, and a ResolvedEmbed built from host text is
    /// named; the shipped shapes are clean.</summary>
    [Theory]
    [InlineData("string M(EmbedResolution.FullNote full) => SlateUniffiMethods.ResolvedEmbedTitle(ResolvedEmbeds.Of(full)!);", "")]
    [InlineData("ResolvedEmbed M(EmbedResolution.Section s) => new ResolvedEmbed.Section(s.TargetPath, s.Heading);", "")]
    [InlineData("string M(EmbedResolution.FullNote full) => $\"Embedded note: {full.TargetPath}\";", "Embedded note")]
    [InlineData("string M(EmbedResolution.Block block) => \"Embedded block from \" + block.TargetPath;", "Embedded block")]
    [InlineData("string M(string alt) => $\"embedded image: {alt}\";", "embedded image")]
    [InlineData("string M(BaseEmbedProjection b) => SlateUniffiMethods.ResolvedEmbedTitle(new ResolvedEmbed.Base(b.TargetPath));", "")]
    [InlineData("string M(BaseEmbedProjection b) => $\"Embedded base: {System.IO.Path.GetFileNameWithoutExtension(b.TargetPath)}\";", "Embedded base")]
    [InlineData("ResolvedEmbed M() => new ResolvedEmbed.Note(\"note.md\");", "new ResolvedEmbed.Note(\"note.md\")")]
    [InlineData("ResolvedEmbed M(EmbedResolution.FullNote full) => new ResolvedEmbed.Note(string.Concat(full.TargetPath, Suffix));", "new ResolvedEmbed.Note(string.Concat")]
    public void TheSpellingCensusNamesEveryHostTitle(string member, string namedSite)
    {
        string[] failures = SpellingFailures(
            CSharpSyntaxTree.ParseText($"class C {{ {member} }}").GetRoot(),
            "synthetic.cs").ToArray();
        AssertNamed(failures, namedSite);
    }

    /// <summary>The publisher's teeth: a host-worded header, a host-composed
    /// name, a ResolvedEmbed not taken from the content, a name rendered from
    /// another event, a header with host text beyond the locator, a locator
    /// appended outside its one helper and a split literal passed to it are
    /// each named; the shipped shape is clean.</summary>
    [Theory]
    [InlineData(
        "var shown = new A11yEvent.EmbedPreviewShown(targetRaw, content.Resolved);"
        + " PopoverTitle = WithSourceLineLocator(SlateUniffiMethods.ResolvedEmbedTitle(content.Resolved), sourceLine);"
        + " PopoverAutomationName = SlateUniffiMethods.A11yRender(shown).Text; _announce(shown);",
        "")]
    [InlineData(
        "var shown = new A11yEvent.EmbedPreviewShown(targetRaw, content.Resolved);"
        + " PopoverTitle = $\"Embedded note: {targetRaw} — source line {sourceLine}\";"
        + " PopoverAutomationName = SlateUniffiMethods.A11yRender(shown).Text; _announce(shown);",
        "PopoverTitle = $\"Embedded note:")]
    [InlineData(
        "var shown = new A11yEvent.EmbedPreviewShown(targetRaw, content.Resolved);"
        + " PopoverTitle = WithSourceLineLocator(SlateUniffiMethods.ResolvedEmbedTitle(content.Resolved), sourceLine);"
        + " PopoverAutomationName = $\"Embed preview for {targetRaw}, source line {sourceLine}.\"; _announce(shown);",
        "PopoverAutomationName = $\"Embed preview for")]
    [InlineData(
        "var shown = new A11yEvent.EmbedPreviewShown(targetRaw, new ResolvedEmbed.Note(targetRaw));"
        + " PopoverTitle = WithSourceLineLocator(SlateUniffiMethods.ResolvedEmbedTitle(content.Resolved), sourceLine);"
        + " PopoverAutomationName = SlateUniffiMethods.A11yRender(shown).Text; _announce(shown);",
        "new ResolvedEmbed.Note(targetRaw)")]
    [InlineData(
        "var shown = new A11yEvent.EmbedPreviewShown(targetRaw, content.Resolved);"
        + " PopoverTitle = WithSourceLineLocator(SlateUniffiMethods.ResolvedEmbedTitle(content.Resolved), sourceLine);"
        + " PopoverAutomationName = SlateUniffiMethods.A11yRender(new A11yEvent.EmbedPreviewShown(targetRaw, content.Resolved)).Text; _announce(shown);",
        "PopoverAutomationName = SlateUniffiMethods.A11yRender(new")]
    [InlineData(
        "var shown = new A11yEvent.EmbedPreviewShown(targetRaw, content.Resolved);"
        + " PopoverTitle = $\"Preview of {SlateUniffiMethods.ResolvedEmbedTitle(content.Resolved)} — source line {sourceLine}\";"
        + " PopoverAutomationName = SlateUniffiMethods.A11yRender(shown).Text; _announce(shown);",
        "PopoverTitle = $\"Preview of")]
    [InlineData(
        "var shown = new A11yEvent.EmbedPreviewShown(targetRaw, content.Resolved);"
        + " PopoverTitle = $\"{SlateUniffiMethods.ResolvedEmbedTitle(content.Resolved)} — source line {sourceLine}\";"
        + " PopoverAutomationName = SlateUniffiMethods.A11yRender(shown).Text; _announce(shown);",
        "the source-line locator appended outside WithSourceLineLocator")]
    [InlineData(
        "var shown = new A11yEvent.EmbedPreviewShown(targetRaw, content.Resolved);"
        + " PopoverTitle = WithSourceLineLocator(\"Embedded \" + \"note: \" + targetRaw, sourceLine);"
        + " PopoverAutomationName = SlateUniffiMethods.A11yRender(shown).Text; _announce(shown);",
        PlusConcatenation)]
    public void ThePublisherCensusNamesEveryHostSurface(string body, string namedSite)
    {
        MethodDeclarationSyntax publisher = CSharpSyntaxTree
            .ParseText($"class C {{ void {Publisher}(string targetRaw, int sourceLine, EmbedPreviewContent content) {{ {body} }} }}")
            .GetRoot()
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Single();
        AssertNamed(PublisherFailures(publisher, "synthetic.cs").ToArray(), namedSite);
    }

    /// <summary>The sink census's teeth, on the shipped sources: each row
    /// applies one regression in memory — the codex round 2 split literal
    /// among them — and the census must name its cause at its file. The
    /// mutant must parse and bind as cleanly as the shipped file, so a row
    /// cannot pass by breaking the tree instead of the rule.</summary>
    [Theory]
    [InlineData("EditorInteractions.cs", new[] { "SlateUniffiMethods.ResolvedEmbedTitle(ResolvedEmbeds.Of(full)!)", "\"Embedded \" + \"note: \" + full.TargetPath" }, PlusConcatenation)]
    [InlineData("EditorInteractions.cs", new[] { "SlateUniffiMethods.ResolvedEmbedTitle(ResolvedEmbeds.Of(section)!)", "$\"Embedded section: {section.Heading} from {section.TargetPath}\"" }, Interpolation)]
    [InlineData("EditorInteractions.cs", new[] { "SlateUniffiMethods.ResolvedEmbedTitle(ResolvedEmbeds.Of(block)!)", "string.Concat(\"Embedded block from \", block.TargetPath)" }, ConcatCall)]
    [InlineData("EditorInteractions.cs", new[] { "SlateUniffiMethods.ResolvedEmbedTitle(ResolvedEmbeds.Of(image)!)", "string.Format(\"Embedded image: {0}\", image.Alt ?? image.TargetPath)" }, FormatCall)]
    [InlineData("EditorInteractions.cs", new[] { "SlateUniffiMethods.ResolvedEmbedTitle(ResolvedEmbeds.Of(full)!)", "new System.Text.StringBuilder(\"Embedded note: \").Append(full.TargetPath).ToString()" }, Builder)]
    [InlineData("EditorInteractions.cs", new[] { "SlateUniffiMethods.ResolvedEmbedTitle(ResolvedEmbeds.Of(full)!)", "\"Embedded note\"" }, Literal)]
    [InlineData("EditorInteractions.cs", new[] { "EmbedResolution.FullNote full => new EditorEmbedPreviewNode(", "EmbedResolution.FullNote full => new(", "SlateUniffiMethods.ResolvedEmbedTitle(ResolvedEmbeds.Of(full)!)", "\"Embedded \" + \"note: \" + full.TargetPath" }, PlusConcatenation)]
    [InlineData("EditorInteractions.cs", new[] { "            root = root with\n            {\n", "            root = root with\n            {\n                Title = \"Embedded note\",\n" }, Literal)]
    [InlineData("EditorInteractions.cs", new[] { "PopoverTitle = WithSourceLineLocator(\n            SlateUniffiMethods.ResolvedEmbedTitle(content.Resolved),\n            sourceLine);", "PopoverTitle = $\"{SlateUniffiMethods.ResolvedEmbedTitle(content.Resolved)} — source line {sourceLine}\";" }, "the source-line locator appended outside WithSourceLineLocator")]
    [InlineData("EditorInteractions.cs", new[] { "$\"{coreTitle} — source line {sourceLine}\"", "$\"Embedded {coreTitle} — source line {sourceLine}\"" }, "adds host text beyond the source-line locator")]
    [InlineData("EditorInteractions.cs", new[] { "private set => SetField(ref _popoverTitle, value);", "private set => SetField(ref _popoverTitle, \"Embedded \" + value);" }, "PopoverTitle's setter")]
    [InlineData("EditorEmbedPreview.cs", new[] { "Header = node.Title,", "Header = \"Embedded \" + node.Title," }, "the renderer composes a card's Title")]
    [InlineData("EditorEmbedPreview.cs", new[] { "        AutomationProperties.SetName(expander, node.Title);\n", "        AutomationProperties.SetName(expander, node.Title);\n        AutomationProperties.SetName(expander, \"Preview \" + AutomationProperties.GetName(expander));\n" }, "which is neither a card's Title nor a text the census registers")]
    [InlineData("EditorEmbedPreview.cs", new[] { "        AutomationProperties.SetName(expander, node.Title);\n", "        AutomationProperties.SetName(expander, node.Title);\n        expander.Header = \"Preview: \" + expander.Header;\n" }, "which is neither a card's Title nor a text the census registers")]
    [InlineData("EditorEmbedPreview.cs", new[] { "        var expander = new Expander\n", "        var expander = new EmbedCardExpander\n", "internal sealed record EditorEmbedPreviewPart(", "internal sealed class EmbedCardExpander : Expander\n{\n}\n\ninternal sealed record EditorEmbedPreviewPart(" }, "a host type whose automation peer")]
    [InlineData("EditorEmbedPreview.cs", new[] { "        AutomationProperties.SetName(expander, node.Title);\n", "        AutomationProperties.SetName(expander, node.Title);\n        expander.SetResourceReference(FrameworkElement.StyleProperty, \"EmbedCardStyle\");\n" }, "the renderer sets Style")]
    [InlineData("AutomationLandmark.cs", new[] { "    protected override string GetClassNameCore() => \"SlateLandmark\";", "    protected override string GetClassNameCore() => \"SlateLandmark\";\n\n    protected override string GetNameCore() => \"Embedded \" + base.GetNameCore();" }, "overrides GetNameCore")]
    [InlineData("Reading/ReadingDocumentBuilder.cs", new[] { "_ => SlateUniffiMethods.ResolvedEmbedTitle(ResolvedEmbeds.Of(resolution)!),", "_ => \"Embedded \" + \"note: \" + key," }, PlusConcatenation)]
    [InlineData("Reading/ReadingDocumentBuilder.cs", new[] { "_ => SlateUniffiMethods.ResolvedEmbedTitle(ResolvedEmbeds.Of(child.Resolution)!),", "_ => $\"Embedded note: {child.RawTarget}\"," }, Interpolation)]
    [InlineData("Reading/ReadingDocumentBuilder.cs", new[] { "        string headerName = resolution switch\n        {\n            null => $\"Embed: {key}\",", "        string headerName = (resolution is EmbedResolution.FullNote ? null : resolution) switch\n        {\n            null => \"Embedded \" + \"note: \" + key," }, PlusConcatenation)]
    [InlineData("Reading/ReadingDocumentBuilder.cs", new[] { "header.Inlines.Add(new Run(headerName));", "header.Inlines.Add(new Run(\"Embedded \"));\n        header.Inlines.Add(new Run(headerName));" }, "static text beside the header's title")]
    [InlineData("Reading/ReadingSemantics.cs", new[] { "section.SetValue(EmbedNameProperty, name);", "section.SetValue(EmbedNameProperty, \"Embedded \" + name);" }, "the embed landmark stores")]
    [InlineData("ResolvedEmbeds.cs", new[] { "new ResolvedEmbed.Note(full.TargetPath)", "new ResolvedEmbed.Note(\"Embedded \" + full.TargetPath)" }, "a ResolvedEmbed filled from host text")]
    public void EveryHostCompositionReachingACSharpSinkIsNamed(string file, string[] edits, string cause)
    {
        string[] failures = CSharpMutantFailures(file, edits);
        Assert.True(
            failures.Any(failure => failure.StartsWith(file + ":", StringComparison.Ordinal)
                && failure.Contains(cause, StringComparison.Ordinal)),
            $"no failure at {file} names \"{cause}\":\n" + string.Join("\n", failures));
    }

    /// <summary>The XAML half's teeth, on the shipped XAML: a StringFormat, a
    /// converter, fallback text, static sibling text, a MultiBinding or a
    /// static name at a title sink is each named at its file.</summary>
    [Theory]
    [InlineData("WorkspaceTemplates.xaml", "<TextBlock Text=\"{Binding EditorInteractions.PopoverTitle}\"", "<TextBlock Text=\"{Binding EditorInteractions.PopoverTitle, StringFormat='Embedded {0}'}\"", "StringFormat")]
    [InlineData("WorkspaceTemplates.xaml", "<TextBlock Text=\"{Binding EditorInteractions.PopoverTitle}\"", "<TextBlock Text=\"{Binding EditorInteractions.PopoverTitle, Converter={StaticResource EmbeddedPrefixConverter}}\"", "Converter")]
    [InlineData("WorkspaceTemplates.xaml", "<TextBlock Text=\"{Binding EditorInteractions.PopoverTitle}\"", "<TextBlock Text=\"{Binding EditorInteractions.PopoverTitle, FallbackValue='Embedded note'}\"", "FallbackValue")]
    [InlineData("WorkspaceTemplates.xaml", "<TextBlock Text=\"{Binding EditorInteractions.PopoverTitle}\"\n                                   FontSize=\"16\"\n                                   FontWeight=\"SemiBold\"\n                                   AutomationProperties.HeadingLevel=\"Level2\" />", "<TextBlock FontSize=\"16\" FontWeight=\"SemiBold\" AutomationProperties.HeadingLevel=\"Level2\"><Run Text=\"Embedded \" /><Run Text=\"{Binding EditorInteractions.PopoverTitle}\" /></TextBlock>", "static sibling text")]
    [InlineData("WorkspaceTemplates.xaml", "<TextBlock Text=\"{Binding EditorInteractions.PopoverTitle}\"\n                                   FontSize=\"16\"\n                                   FontWeight=\"SemiBold\"\n                                   AutomationProperties.HeadingLevel=\"Level2\" />", "<TextBlock FontSize=\"16\"><TextBlock.Text><MultiBinding StringFormat=\"Embedded {0}\"><Binding Path=\"EditorInteractions.PopoverTitle\" /></MultiBinding></TextBlock.Text></TextBlock>", "MultiBinding")]
    [InlineData("WorkspaceTemplates.xaml", "<TextBlock Text=\"{Binding EditorInteractions.PopoverTitle}\"", "<TextBlock Text=\"{Binding EditorInteractions.PopoverTitle}\" AutomationProperties.Name=\"Embedded preview\"", "a static AutomationProperties.Name replaces the title")]
    [InlineData("WorkspaceTemplates.xaml", "AutomationProperties.Name=\"{Binding EditorInteractions.PopoverAutomationName}\"", "AutomationProperties.Name=\"{Binding EditorInteractions.PopoverAutomationName, StringFormat='Embedded {0}'}\"", "StringFormat")]
    [InlineData("WorkspaceTemplates.xaml", "AutomationProperties.Name=\"{Binding EditorInteractions.PopoverTitle}\" />", "AutomationProperties.Name=\"{Binding EditorInteractions.PopoverTitle, Converter={StaticResource EmbeddedPrefixConverter}}\" />", "Converter")]
    [InlineData("MainWindow.xaml", "<local:EditorEmbedPreviewView Root=\"{Binding Node}\"", "<local:EditorEmbedPreviewView Root=\"{Binding Node, Converter={StaticResource EmbeddedPrefixConverter}}\"", "Converter")]
    [InlineData("WorkspaceTemplates.xaml", "<TextBlock Text=\"{Binding EditorInteractions.PopoverTitle}\"", "<local:TitleBlock Text=\"{Binding EditorInteractions.PopoverTitle}\"", "a title sink the census does not expect")]
    [InlineData("WorkspaceTemplates.xaml", "    <local:IsNotNullConverter x:Key=\"IsNotNullConverter\" />", "    <local:IsNotNullConverter x:Key=\"IsNotNullConverter\" />\n    <Style xmlns:wpf=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" TargetType=\"{x:Type wpf:Expander}\"><Setter Property=\"HeaderTemplate\"><Setter.Value><DataTemplate><TextBlock Text=\"{Binding StringFormat='Preview: {0}'}\" /></DataTemplate></Setter.Value></Setter></Style>", "an Expander style sets HeaderTemplate")]
    [InlineData("WorkspaceTemplates.xaml", "    <local:IsNotNullConverter x:Key=\"IsNotNullConverter\" />", "    <local:IsNotNullConverter x:Key=\"IsNotNullConverter\" />\n    <Style xmlns:c=\"clr-namespace:System.Windows.Controls;assembly=PresentationFramework\" x:Key=\"{x:Type TypeName=c:Expander}\"><Setter Property=\"Expander.HeaderStringFormat\" Value=\"Preview: {0}\" /></Style>", "an Expander style sets Expander.HeaderStringFormat")]
    [InlineData("WorkspaceTemplates.xaml", "    <local:IsNotNullConverter x:Key=\"IsNotNullConverter\" />", "    <local:IsNotNullConverter x:Key=\"IsNotNullConverter\" />\n    <DataTemplate xmlns:sys=\"clr-namespace:System;assembly=System.Runtime\" DataType=\"{x:Type sys:String}\"><TextBlock Text=\"{Binding StringFormat='Preview: {0}'}\" /></DataTemplate>", "a DataTemplate for every string")]
    [InlineData("MainWindow.xaml", "<local:EditorEmbedPreviewView Root=\"{Binding Node}\"", "<TextBlock Text=\"{Binding Node.Title, StringFormat='Embedded {0}'}\" /><local:EditorEmbedPreviewView Root=\"{Binding Node}\"", "StringFormat")]
    public void EveryHostCompositionReachingAXamlSinkIsNamed(
        string file, string original, string replacement, string cause)
    {
        IReadOnlyList<(string Relative, string Text)> shipped = XamlSources();
        (string Relative, string Text) target = Assert.Single(shipped, source => source.Relative == file);
        Assert.True(
            Occurrences(target.Text, original) == 1,
            $"{file} does not carry the row's original exactly once, so the row mutates nothing it can name: {original}");
        string mutant = target.Text.Replace(original, replacement, StringComparison.Ordinal);
        // A mutant that is not well-formed XML proves nothing about a rule.
        _ = System.Xml.Linq.XDocument.Parse(mutant);

        string[] failures = XamlFailures(
            shipped.Select(source => source.Relative == file ? (source.Relative, mutant) : source).ToArray())
            .ToArray();
        Assert.True(
            failures.Any(failure => failure.StartsWith(file + ":", StringComparison.Ordinal)
                && failure.Contains(cause, StringComparison.Ordinal)),
            $"no failure at {file} names \"{cause}\":\n" + string.Join("\n", failures));
    }

    /// <summary>The sink rule recognises core by its spelling, so a host
    /// declaration or alias that would answer to that spelling is named.</summary>
    [Theory]
    [InlineData("namespace SlateWindows { internal static class SlateUniffiMethods { } }", "declares SlateUniffiMethods")]
    [InlineData("namespace SlateWindows { internal static class Titles { internal static string ResolvedEmbedTitle(object r) => \"x\"; } }", "declares ResolvedEmbedTitle")]
    [InlineData("namespace SlateWindows { internal static class Titles { internal static string A11yRender(object e) => \"x\"; } }", "declares A11yRender")]
    [InlineData("using SlateUniffiMethods = SlateWindows.Titles;", "aliases SlateUniffiMethods")]
    [InlineData("namespace SlateWindows { internal static class Titles { internal static string Title(object r) => \"x\"; } }", "")]
    public void AHostDeclarationShadowingCoreIsNamed(string source, string namedSite) =>
        AssertNamed(
            DecoyFailures(CSharpSyntaxTree.ParseText(source).GetRoot(), "synthetic.cs").ToArray(),
            namedSite);

    /// <summary>The second net over one file: a card-title shape in one
    /// literal or interpolation, and a ResolvedEmbed filled from host text.</summary>
    internal static IEnumerable<string> SpellingFailures(SyntaxNode root, string file)
    {
        foreach (SyntaxNode node in root.DescendantNodes())
        {
            string? text = node switch
            {
                LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression) =>
                    literal.Token.ValueText,
                InterpolatedStringTextSyntax part => part.TextToken.ValueText,
                _ => null,
            };
            if (text is not null && CardShape().Match(text) is { Success: true } match)
            {
                yield return $"{file}:{Line(node)}: \"{match.Value}\" — a card title shape "
                    + "spelled by the host; core words it (ResolvedEmbedTitle).";
            }

            if (node is ObjectCreationExpressionSyntax creation
                && IsResolvedEmbedType(creation.Type)
                && creation.ArgumentList is { } arguments
                && arguments.Arguments
                    .Select(argument => HostComposition(argument.Expression))
                    .FirstOrDefault(found => found is not null) is { } host)
            {
                yield return $"{file}:{Line(node)}: {creation} — a ResolvedEmbed filled from host text "
                    + $"({host.Cause}), not from the resolution.";
            }
        }
    }

    /// <summary>The publisher's surfaces: the event carries the content's
    /// Resolved, the popover's name is the rendering of that event, the
    /// header is core's title of what the event carries passed through the
    /// locator helper, and the event is announced.</summary>
    internal static IEnumerable<string> PublisherFailures(MethodDeclarationSyntax publisher, string file)
    {
        ObjectCreationExpressionSyntax? shown = publisher.DescendantNodes()
            .OfType<ObjectCreationExpressionSyntax>()
            .SingleOrDefault(creation =>
                CSharpSource.Normalize(creation.Type) == "A11yEvent.EmbedPreviewShown"
                && creation.Parent is not ArgumentSyntax);
        if (shown is null)
        {
            yield return $"{file}:{Line(publisher)}: {Publisher} constructs no EmbedPreviewShown of its own; "
                + "the census reads one.";
            yield break;
        }

        // The event's data: the content's Resolved, never a hand-built one.
        ExpressionSyntax? resolved = shown.ArgumentList?.Arguments.Count == 2
            ? CSharpSource.Resolve(shown.ArgumentList.Arguments[1].Expression, publisher)
            : null;
        if (resolved is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Resolved" })
        {
            yield return $"{file}:{Line(shown)}: {shown} — the event's ResolvedEmbed is not the "
                + "content's Resolved, what the embed actually resolved to.";
        }

        string? local = shown.Parent is EqualsValueClauseSyntax
        {
            Parent: VariableDeclaratorSyntax declarator,
        }
            ? declarator.Identifier.ValueText
            : null;

        foreach (AssignmentExpressionSyntax assignment in Assignments(publisher, "PopoverAutomationName"))
        {
            ExpressionSyntax value = CSharpSource.Resolve(assignment.Right, publisher);
            bool rendered = value is MemberAccessExpressionSyntax
            {
                Name.Identifier.ValueText: "Text",
                Expression: InvocationExpressionSyntax render,
            }
                && CSharpSource.Normalize(render.Expression) == "SlateUniffiMethods.A11yRender"
                && render.ArgumentList.Arguments.Count == 1
                && render.ArgumentList.Arguments[0].Expression
                    is IdentifierNameSyntax { Identifier.ValueText: var name }
                && name == local;
            if (!rendered)
            {
                yield return $"{file}:{Line(assignment)}: {assignment} — the popover's name is not the "
                    + "rendering of the EmbedPreviewShown it announces.";
            }
        }

        foreach (AssignmentExpressionSyntax assignment in Assignments(publisher, "PopoverTitle"))
        {
            if (HeaderFailure(assignment.Right, resolved) is { } cause)
            {
                yield return $"{file}:{Line(assignment)}: {assignment} — the visible header is not "
                    + $"{LocatorHelper}(core's title of what the event carries, the line): {cause}.";
            }
        }

        if (!Assignments(publisher, "PopoverAutomationName").Any()
            || !Assignments(publisher, "PopoverTitle").Any())
        {
            yield return $"{file}:{Line(publisher)}: {Publisher} sets no popover name or header; the census "
                + "reads both.";
        }

        bool announced = local is not null && publisher.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Any(invocation => CSharpSource.Normalize(invocation.Expression) == "_announce"
                && invocation.ArgumentList.Arguments.Count == 1
                && invocation.ArgumentList.Arguments[0].Expression
                    is IdentifierNameSyntax { Identifier.ValueText: var passed }
                && passed == local);
        if (!announced)
        {
            yield return $"{file}:{Line(publisher)}: {Publisher} does not announce the EmbedPreviewShown it "
                + "renders.";
        }
    }

    private static IEnumerable<AssignmentExpressionSyntax> Assignments(SyntaxNode scope, string target) =>
        scope.DescendantNodes()
            .OfType<AssignmentExpressionSyntax>()
            .Where(assignment => assignment.Left is IdentifierNameSyntax identifier
                && identifier.Identifier.ValueText == target);

    private static bool IsResolvedEmbedType(TypeSyntax type)
    {
        string spelled = CSharpSource.Normalize(type);
        foreach (string prefix in new[] { "global::uniffi.slate_uniffi.", "uniffi.slate_uniffi." })
        {
            if (spelled.StartsWith(prefix, StringComparison.Ordinal))
            {
                spelled = spelled[prefix.Length..];
                break;
            }
        }
        return spelled.StartsWith("ResolvedEmbed.", StringComparison.Ordinal);
    }

    private static int Occurrences(string text, string value)
    {
        int count = 0;
        for (int at = text.IndexOf(value, StringComparison.Ordinal);
            at >= 0;
            at = text.IndexOf(value, at + 1, StringComparison.Ordinal))
        {
            count++;
        }
        return count;
    }

    private static void AssertNamed(string[] failures, string namedSite)
    {
        if (namedSite.Length == 0)
        {
            Assert.True(failures.Length == 0, string.Join("\n", failures));
            return;
        }
        Assert.True(
            failures.Any(failure => failure.Contains(namedSite, StringComparison.Ordinal)),
            $"no failure names \"{namedSite}\":\n" + string.Join("\n", failures));
    }
}
