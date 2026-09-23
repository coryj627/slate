// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// W7-7 R-8 (#1251): an unavailable embed preview's body and name ARE
// core's rendering of the announced EmbedPreviewUnavailable event.
//
// The runtime facts (W2EditorInteractionTests) prove today's TEXT: body,
// name, live UIA name and the core rendering are equal. They cannot prove
// PROVENANCE. The host's card wording (EditorInteractions.Describe) spells
// the same reasons, so a surface fed from it would read identically and
// pass; and once the surfaces stop calling it, rewording that method can
// fail nothing. This census closes that from the source: each surface
// assignment in the presenter must consume the rendered core event —
// SlateUniffiMethods.A11yRender(<new A11yEvent.EmbedPreviewUnavailable>)
// .Text, through locals if need be — and none may call Describe or carry
// a string literal or interpolation.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "embed-unavailable-surface")]
public sealed class EmbedUnavailableSurfaceCensus
{
    private const string Presenter = "PresentUnavailableEmbed";
    private static readonly string[] Surfaces = ["PopoverBody", "PopoverAutomationName"];

    [Fact]
    public void TheUnavailableSurfacesConsumeTheRenderedCoreEvent()
    {
        CSharpSource source = CSharpSource.Load("EditorInteractions.cs");
        string[] failures = SurfaceFailures(source.Method(Presenter)).ToArray();
        Assert.True(failures.Length == 0, string.Join("\n", failures));

        // The production publish reaches the presenter for the unavailable
        // outcome, and nothing else constructs that event to compose its
        // own surfaces beside it.
        Assert.True(
            CSharpSource.Invokes(source.Method("PublishEmbedPreview"), Presenter),
            $"PublishEmbedPreview no longer presents its unavailable outcome through {Presenter}.");
        string[] constructors = source.Root.DescendantNodes()
            .OfType<ObjectCreationExpressionSyntax>()
            .Where(creation => CSharpSource.Normalize(creation.Type) == "A11yEvent.EmbedPreviewUnavailable")
            .Select(creation => creation.Ancestors().OfType<MethodDeclarationSyntax>().First().Identifier.ValueText)
            .ToArray();
        Assert.Equal([Presenter], constructors);
    }

    /// <summary>The census's teeth, on the shapes a regression takes: each
    /// rewired surface is named; the shipped shape and an inline render are
    /// clean.</summary>
    [Theory]
    [InlineData("string sentence = SlateUniffiMethods.A11yRender(unavailable).Text; PopoverBody = sentence; PopoverAutomationName = sentence;", "")]
    [InlineData("PopoverBody = SlateUniffiMethods.A11yRender(unavailable).Text; PopoverAutomationName = SlateUniffiMethods.A11yRender(unavailable).Text;", "")]
    [InlineData("string sentence = SlateUniffiMethods.A11yRender(unavailable).Text; PopoverBody = Describe(reason); PopoverAutomationName = sentence;", "PopoverBody = Describe(reason)")]
    [InlineData("string sentence = SlateUniffiMethods.A11yRender(unavailable).Text; PopoverBody = sentence; PopoverAutomationName = \"Embed preview unavailable.\";", "PopoverAutomationName = \"Embed preview unavailable.\"")]
    [InlineData("string sentence = SlateUniffiMethods.A11yRender(unavailable).Text; PopoverBody = $\"{sentence}\"; PopoverAutomationName = sentence;", "PopoverBody = $\"{sentence}\"")]
    [InlineData("string sentence = SlateUniffiMethods.A11yRender(new A11yEvent.EmbedPreviewShown(targetRaw, \"t\")).Text; PopoverBody = sentence; PopoverAutomationName = sentence;", "PopoverBody = sentence")]
    [InlineData("string body = Describe(reason); string sentence = body; PopoverBody = sentence; PopoverAutomationName = SlateUniffiMethods.A11yRender(unavailable).Text;", "PopoverBody = sentence")]
    public void TheCensusNamesEveryRewiredSurface(string body, string namedSite)
    {
        MethodDeclarationSyntax presenter = Parse(
            "void PresentUnavailableEmbed(string targetRaw, int sourceLine, EmbedUnresolvedReason reason) {"
            + " var unavailable = new A11yEvent.EmbedPreviewUnavailable(targetRaw, reason); "
            + body
            + " _announce(unavailable); }");
        string[] failures = SurfaceFailures(presenter).ToArray();
        if (namedSite.Length == 0)
        {
            Assert.Empty(failures);
        }
        else
        {
            Assert.Contains(failures, failure => failure.Contains(namedSite, StringComparison.Ordinal));
        }
    }

    /// <summary>Every problem with the surface assignments, each naming its
    /// site; empty when both consume the rendered core event.</summary>
    internal static IEnumerable<string> SurfaceFailures(MethodDeclarationSyntax presenter)
    {
        foreach (string surface in Surfaces)
        {
            AssignmentExpressionSyntax[] assignments = presenter.DescendantNodes()
                .OfType<AssignmentExpressionSyntax>()
                .Where(assignment => assignment.Left is IdentifierNameSyntax target
                    && target.Identifier.ValueText == surface)
                .ToArray();
            if (assignments.Length != 1)
            {
                yield return $"{surface}: {assignments.Length} assignments in {Presenter}; the census reads exactly one.";
                continue;
            }

            AssignmentExpressionSyntax assignment = assignments[0];
            string site = $"{assignment} (line {assignment.GetLocation().GetLineSpan().StartLinePosition.Line + 1})";
            ExpressionSyntax value = CSharpSource.Resolve(assignment.Right, presenter);
            if (HostCopy(assignment.Right) || HostCopy(value))
            {
                yield return $"{site}: host copy (Describe, a string literal or an interpolation) reaches the surface.";
            }
            else if (!IsRenderedUnavailableEvent(value, presenter))
            {
                yield return $"{site}: not the rendered A11yEvent.EmbedPreviewUnavailable the presenter announces.";
            }
        }
    }

    /// <summary><c>SlateUniffiMethods.A11yRender(e).Text</c>, where
    /// <c>e</c> is, or is a local initialised to, a
    /// <c>new A11yEvent.EmbedPreviewUnavailable(...)</c>.</summary>
    private static bool IsRenderedUnavailableEvent(ExpressionSyntax value, SyntaxNode scope) =>
        value is MemberAccessExpressionSyntax
        {
            Name.Identifier.ValueText: "Text",
            Expression: InvocationExpressionSyntax render,
        }
        && CSharpSource.Normalize(render.Expression) == "SlateUniffiMethods.A11yRender"
        && render.ArgumentList.Arguments.Count == 1
        && CSharpSource.Resolve(render.ArgumentList.Arguments[0].Expression, scope)
            is ObjectCreationExpressionSyntax creation
        && CSharpSource.Normalize(creation.Type) == "A11yEvent.EmbedPreviewUnavailable";

    private static bool HostCopy(SyntaxNode value) =>
        CSharpSource.Invokes(value, "Describe")
        || value.DescendantNodesAndSelf().Any(node =>
            node is InterpolatedStringExpressionSyntax
            || (node is LiteralExpressionSyntax literal
                && literal.IsKind(SyntaxKind.StringLiteralExpression)));

    private static MethodDeclarationSyntax Parse(string method) =>
        CSharpSyntaxTree.ParseText($"class C {{ {method} }}")
            .GetRoot()
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Single();
}
