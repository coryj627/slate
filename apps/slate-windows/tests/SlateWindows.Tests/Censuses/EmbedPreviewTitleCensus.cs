// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// #1278 (locked decision 05 §1.1, contract 38): a resolved embed card's
// title is core's words, and the Ctrl+E preview's announcement, UIA name
// and visible header all come from what the embed resolved to.
//
// The runtime facts (W2EditorInteractionTests.
// AResolvedEmbedAnnouncesItsPreviewWhenTheResultLands) prove today's TEXT
// and the event's structured fields. They cannot prove PROVENANCE: a host
// literal spelling "Embedded note: …" with the same words passes every one
// of them, and the corpus then pins a sentence core no longer owns. This
// census closes that from the source:
//
// - No host source file spells a card-title shape ("Embedded note",
//   "Embedded section", "Embedded block", "Embedded image", "Embedded
//   base") in a string literal or interpolation, and every ResolvedEmbed
//   the host builds is filled from the resolution, never from literal
//   text. The reading view's `.base` summary card (Bases contract C10) is
//   a card like the others: its header is core's Base title (codex r1).
// - The one EmbedPreviewShown construction carries the content's Resolved
//   data; the popover's UIA name is the rendering of that same event; and
//   the visible header is core's ResolvedEmbedTitle followed only by the
//   source-line locator.

using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "embed-preview-title")]
public sealed partial class EmbedPreviewTitleCensus
{
    private const string Publisher = "PublishEmbedPreview";
    private const string Locator = " — source line ";

    [GeneratedRegex(@"Embedded (note|section|block|image|base)\b", RegexOptions.IgnoreCase)]
    private static partial Regex CardShape();

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
                Path.GetRelativePath(root, file)))
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

        string[] failures = PublisherFailures(source.Method(Publisher)).ToArray();
        Assert.True(failures.Length == 0, string.Join("\n", failures));
    }

    /// <summary>The spelling half's teeth: a card shape in a literal or an
    /// interpolation is named, and a ResolvedEmbed built from literal text
    /// is named; the shipped shapes are clean.</summary>
    [Theory]
    [InlineData("string M(EmbedResolution.FullNote full) => ResolvedEmbeds.TitleOf(full)!;", "")]
    [InlineData("ResolvedEmbed M(EmbedResolution.Section s) => new ResolvedEmbed.Section(s.TargetPath, s.Heading);", "")]
    [InlineData("string M(EmbedResolution.FullNote full) => $\"Embedded note: {full.TargetPath}\";", "Embedded note")]
    [InlineData("string M(EmbedResolution.Block block) => \"Embedded block from \" + block.TargetPath;", "Embedded block")]
    [InlineData("string M(string alt) => $\"embedded image: {alt}\";", "embedded image")]
    [InlineData("string M(BaseEmbedProjection b) => SlateUniffiMethods.ResolvedEmbedTitle(new ResolvedEmbed.Base(b.TargetPath));", "")]
    [InlineData("string M(BaseEmbedProjection b) => $\"Embedded base: {System.IO.Path.GetFileNameWithoutExtension(b.TargetPath)}\";", "Embedded base")]
    [InlineData("ResolvedEmbed M() => new ResolvedEmbed.Note(\"note.md\");", "new ResolvedEmbed.Note(\"note.md\")")]
    public void TheSpellingCensusNamesEveryHostTitle(string member, string namedSite)
    {
        string[] failures = SpellingFailures(
            CSharpSyntaxTree.ParseText($"class C {{ {member} }}").GetRoot(),
            "synthetic.cs").ToArray();
        AssertNamed(failures, namedSite);
    }

    /// <summary>The publisher half's teeth: a host-worded header, a
    /// host-composed name, a ResolvedEmbed not taken from the content, a
    /// name rendered from another event and a header with more host text
    /// than the locator are each named; the shipped shape is clean.</summary>
    [Theory]
    [InlineData(
        "var shown = new A11yEvent.EmbedPreviewShown(targetRaw, content.Resolved);"
        + " PopoverTitle = $\"{SlateUniffiMethods.ResolvedEmbedTitle(content.Resolved)} — source line {sourceLine}\";"
        + " PopoverAutomationName = SlateUniffiMethods.A11yRender(shown).Text; _announce(shown);",
        "")]
    [InlineData(
        "var shown = new A11yEvent.EmbedPreviewShown(targetRaw, content.Resolved);"
        + " PopoverTitle = $\"Embedded note: {targetRaw} — source line {sourceLine}\";"
        + " PopoverAutomationName = SlateUniffiMethods.A11yRender(shown).Text; _announce(shown);",
        "PopoverTitle = $\"Embedded note:")]
    [InlineData(
        "var shown = new A11yEvent.EmbedPreviewShown(targetRaw, content.Resolved);"
        + " PopoverTitle = $\"{SlateUniffiMethods.ResolvedEmbedTitle(content.Resolved)} — source line {sourceLine}\";"
        + " PopoverAutomationName = $\"Embed preview for {targetRaw}, source line {sourceLine}.\"; _announce(shown);",
        "PopoverAutomationName = $\"Embed preview for")]
    [InlineData(
        "var shown = new A11yEvent.EmbedPreviewShown(targetRaw, new ResolvedEmbed.Note(targetRaw));"
        + " PopoverTitle = $\"{SlateUniffiMethods.ResolvedEmbedTitle(content.Resolved)} — source line {sourceLine}\";"
        + " PopoverAutomationName = SlateUniffiMethods.A11yRender(shown).Text; _announce(shown);",
        "new ResolvedEmbed.Note(targetRaw)")]
    [InlineData(
        "var shown = new A11yEvent.EmbedPreviewShown(targetRaw, content.Resolved);"
        + " PopoverTitle = $\"{SlateUniffiMethods.ResolvedEmbedTitle(content.Resolved)} — source line {sourceLine}\";"
        + " PopoverAutomationName = SlateUniffiMethods.A11yRender(new A11yEvent.EmbedPreviewShown(targetRaw, content.Resolved)).Text; _announce(shown);",
        "PopoverAutomationName = SlateUniffiMethods.A11yRender(new")]
    [InlineData(
        "var shown = new A11yEvent.EmbedPreviewShown(targetRaw, content.Resolved);"
        + " PopoverTitle = $\"Preview of {SlateUniffiMethods.ResolvedEmbedTitle(content.Resolved)} — source line {sourceLine}\";"
        + " PopoverAutomationName = SlateUniffiMethods.A11yRender(shown).Text; _announce(shown);",
        "PopoverTitle = $\"Preview of")]
    public void ThePublisherCensusNamesEveryHostSurface(string body, string namedSite)
    {
        MethodDeclarationSyntax publisher = CSharpSyntaxTree
            .ParseText($"class C {{ void {Publisher}(string targetRaw, int sourceLine, EmbedPreviewContent content) {{ {body} }} }}")
            .GetRoot()
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Single();
        AssertNamed(PublisherFailures(publisher).ToArray(), namedSite);
    }

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
                && CSharpSource.Normalize(creation.Type).StartsWith("ResolvedEmbed.", StringComparison.Ordinal)
                && creation.ArgumentList is { } arguments
                && arguments.Arguments.Any(argument => HostCopy(argument.Expression)))
            {
                yield return $"{file}:{Line(node)}: {creation} — a ResolvedEmbed filled from "
                    + "literal text, not from the resolution.";
            }
        }
    }

    internal static IEnumerable<string> PublisherFailures(MethodDeclarationSyntax publisher)
    {
        ObjectCreationExpressionSyntax? shown = publisher.DescendantNodes()
            .OfType<ObjectCreationExpressionSyntax>()
            .SingleOrDefault(creation =>
                CSharpSource.Normalize(creation.Type) == "A11yEvent.EmbedPreviewShown"
                && creation.Parent is not ArgumentSyntax);
        if (shown is null)
        {
            yield return $"{Publisher} constructs no EmbedPreviewShown of its own; the census reads one.";
            yield break;
        }

        // The event's data: the content's Resolved, never a hand-built one.
        ExpressionSyntax? resolved = shown.ArgumentList?.Arguments.Count == 2
            ? shown.ArgumentList.Arguments[1].Expression
            : null;
        if (resolved is null
            || CSharpSource.Resolve(resolved, publisher) is not MemberAccessExpressionSyntax
            {
                Name.Identifier.ValueText: "Resolved",
            })
        {
            yield return $"{shown} (line {Line(shown)}): the event's ResolvedEmbed is not the "
                + "content's Resolved — what the embed actually resolved to.";
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
                yield return $"{assignment} (line {Line(assignment)}): the popover's name is not "
                    + "the rendering of the EmbedPreviewShown it announces.";
            }
        }

        foreach (AssignmentExpressionSyntax assignment in Assignments(publisher, "PopoverTitle"))
        {
            ExpressionSyntax value = CSharpSource.Resolve(assignment.Right, publisher);
            if (!IsCoreTitleWithLocator(value))
            {
                yield return $"{assignment} (line {Line(assignment)}): the visible header is not "
                    + "core's ResolvedEmbedTitle followed only by the source-line locator.";
            }
        }

        if (!Assignments(publisher, "PopoverAutomationName").Any()
            || !Assignments(publisher, "PopoverTitle").Any())
        {
            yield return $"{Publisher} sets no popover name or header; the census reads both.";
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
            yield return $"{Publisher} does not announce the EmbedPreviewShown it renders.";
        }
    }

    /// <summary><c>$"{SlateUniffiMethods.ResolvedEmbedTitle(x.Resolved)} — source line {n}"</c>,
    /// or the bare title: one interpolation of core's title from the
    /// content's Resolved, and no host text but the locator.</summary>
    private static bool IsCoreTitleWithLocator(ExpressionSyntax value)
    {
        static bool IsCoreTitle(ExpressionSyntax expression) =>
            expression is InvocationExpressionSyntax invocation
            && CSharpSource.Normalize(invocation.Expression) == "SlateUniffiMethods.ResolvedEmbedTitle"
            && invocation.ArgumentList.Arguments.Count == 1
            && invocation.ArgumentList.Arguments[0].Expression
                is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Resolved" };

        if (IsCoreTitle(value))
        {
            return true;
        }
        if (value is not InterpolatedStringExpressionSyntax interpolated)
        {
            return false;
        }
        InterpolatedStringContentSyntax[] contents = interpolated.Contents.ToArray();
        return contents.Length == 3
            && contents[0] is InterpolationSyntax { Expression: var title } && IsCoreTitle(title)
            && contents[1] is InterpolatedStringTextSyntax { TextToken.ValueText: Locator }
            && contents[2] is InterpolationSyntax;
    }

    private static IEnumerable<AssignmentExpressionSyntax> Assignments(SyntaxNode scope, string target) =>
        scope.DescendantNodes()
            .OfType<AssignmentExpressionSyntax>()
            .Where(assignment => assignment.Left is IdentifierNameSyntax identifier
                && identifier.Identifier.ValueText == target);

    private static bool HostCopy(SyntaxNode value) =>
        value.DescendantNodesAndSelf().Any(node =>
            node is InterpolatedStringExpressionSyntax
            || (node is LiteralExpressionSyntax literal
                && literal.IsKind(SyntaxKind.StringLiteralExpression)));

    private static int Line(SyntaxNode node) =>
        node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

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
