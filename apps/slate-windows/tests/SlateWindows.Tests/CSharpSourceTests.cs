// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SlateWindows.Tests;

/// <summary>
/// #1108: the three bypasses that defeated regex scrapes must not defeat
/// the syntax layer that replaced them.
/// </summary>
/// <remarks>
/// The drift twins are only as good as this. Each case below is an attack
/// that was demonstrated against the regex version, run here against the
/// query style the twins now use.
/// </remarks>
public sealed class CSharpSourceTests
{
    private const string Sample = """
        namespace Probe;

        internal static class Handler
        {
            private const string Documentation = "the old arm was Key.Ghost";

            private static void Route(KeyEventArgs e)
            {
                // case Key.Commented:
                switch (e.Key)
                {
                    case Key.Live:
                        Deliver();
                        return;
                }
        #if NEVER_DEFINED
                if (e.Key == Key.Disabled)
                {
                    Deliver();
                }
        #endif
            }
        }
        """;

    [Fact]
    public void CommentedOutCodeIsNotAKeyReference()
    {
        string[] keys = Keys(Sample);

        Assert.Contains("Live", keys);
        Assert.DoesNotContain("Commented", keys);
    }

    [Fact]
    public void AKeyNamedInsideAStringLiteralIsNotAKeyReference()
    {
        // The bypass the comment stripper could never close: it must keep
        // literal contents, or a "//" inside a URL would eat live code.
        Assert.DoesNotContain("Ghost", Keys(Sample));
    }

    [Fact]
    public void CodeInsideAnInactiveConditionalIsNotAKeyReference()
    {
        Assert.DoesNotContain("Disabled", Keys(Sample));
    }

    [Fact]
    public void OverloadAmbiguityFailsRatherThanPickingTheFirstDeclaration()
    {
        const string overloaded = """
            namespace Probe;

            internal static class Handler
            {
                private static void Route() { }

                private static void Route(KeyEventArgs e) { }
            }
            """;

        Exception failure = Assert.ThrowsAny<Exception>(
            () => SingleMethod(overloaded, "Route"));
        Assert.Contains("2 methods named 'Route'", failure.Message, System.StringComparison.Ordinal);
    }

    [Fact]
    public void AMissingMethodFailsRatherThanReturningNothingQuietly()
    {
        Exception failure = Assert.ThrowsAny<Exception>(
            () => SingleMethod(Sample, "Absent"));
        Assert.Contains("declares no method named 'Absent'", failure.Message, System.StringComparison.Ordinal);
    }

    [Fact]
    public void AParseErrorFailsLoudlyInsteadOfYieldingAPartialTree()
    {
        // A tree built from source the parser did not understand answers
        // every query with less than the truth — silently, which is the
        // failure mode this whole class replaces.
        SyntaxTree tree = CSharpSyntaxTree.ParseText(
            "internal class Broken { void M( {", new CSharpParseOptions(LanguageVersion.Preview));
        Assert.Contains(
            tree.GetDiagnostics(),
            diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void InvokesMatchesACallAndNotItsNameInProse()
    {
        const string source = """
            namespace Probe;

            internal static class Handler
            {
                private static void Go()
                {
                    // Search.Focus();
                    var note = "call Search.Focus() here";
                    Search.Focus();
                }
            }
            """;

        MethodDeclarationSyntax method = SingleMethod(source, "Go");
        Assert.True(CSharpSource.Invokes(method, "Search.Focus"));
        Assert.False(CSharpSource.Invokes(method, "Search.Blur"));
    }

    /// <summary>
    /// An alias resolves to what it was initialised with.
    /// </summary>
    [Fact]
    public void ResolveFollowsALocalToItsInitializer()
    {
        ExpressionSyntax resolved = ResolveArgument("""
            namespace Probe;

            internal static class Handler
            {
                private static object Build(Workspace? workspace)
                {
                    bool isDashboardOpen = workspace?.BaseQueryBuilderSheet is not null;
                    return new State(DashboardEditor: isDashboardOpen);
                }
            }
            """);

        Assert.Equal(
            "workspace?.BaseQueryBuilderSheetisnotnull",
            CSharpSource.Normalize(resolved));
    }

    /// <summary>
    /// Two locals of the same name in different scopes resolve to
    /// neither.
    /// </summary>
    /// <remarks>
    /// Guessing which one reaches the call would be inference, and picking
    /// wrong is worse than declining: the caller compares what it gets
    /// against an expectation, so returning the identifier as written
    /// fails loudly rather than passing against the wrong initializer.
    /// </remarks>
    [Fact]
    public void ResolveDeclinesWhenTheNameIsAmbiguous()
    {
        ExpressionSyntax resolved = ResolveArgument("""
            namespace Probe;

            internal static class Handler
            {
                private static object Build(Workspace? workspace, bool flag)
                {
                    if (flag)
                    {
                        bool isDashboardOpen = workspace?.DashboardEditorSheet is not null;
                        Use(isDashboardOpen);
                    }

                    bool isDashboardOpen = workspace?.BaseQueryBuilderSheet is not null;
                    return new State(DashboardEditor: isDashboardOpen);
                }
            }
            """);

        Assert.Equal("isDashboardOpen", CSharpSource.Normalize(resolved));
    }

    /// <summary>
    /// Only the initializer is followed — a later assignment is not.
    /// </summary>
    /// <remarks>
    /// Reassignment is control flow, and following it would mean deciding
    /// which write reaches the call. The declared value is what this
    /// reports, and the expectation is written against that.
    /// </remarks>
    [Fact]
    public void ResolveFollowsTheInitializerAndNotALaterAssignment()
    {
        ExpressionSyntax resolved = ResolveArgument("""
            namespace Probe;

            internal static class Handler
            {
                private static object Build(Workspace? workspace)
                {
                    bool isDashboardOpen = workspace?.DashboardEditorSheet is not null;
                    isDashboardOpen = workspace?.BaseQueryBuilderSheet is not null;
                    return new State(DashboardEditor: isDashboardOpen);
                }
            }
            """);

        Assert.Equal(
            "workspace?.DashboardEditorSheetisnotnull",
            CSharpSource.Normalize(resolved));
    }

    /// <summary>
    /// Resolves the single named argument of the sample's
    /// <c>new State(...)</c>.
    /// </summary>
    private static ExpressionSyntax ResolveArgument(string source)
    {
        SyntaxNode root = Root(source);
        ObjectCreationExpressionSyntax creation = root
            .DescendantNodes()
            .OfType<ObjectCreationExpressionSyntax>()
            .Single();
        ArgumentSyntax argument = creation.ArgumentList!.Arguments.Single();
        SyntaxNode scope = creation.FirstAncestorOrSelf<MethodDeclarationSyntax>()!;
        return CSharpSource.Resolve(argument.Expression, scope);
    }

    private static string[] Keys(string source) =>
        CSharpSource.KeyNames(Root(source)).ToArray();

    private static MethodDeclarationSyntax SingleMethod(string source, string name)
    {
        MethodDeclarationSyntax[] declarations = Root(source)
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Where(method => method.Identifier.ValueText == name)
            .ToArray();

        // Mirrors CSharpSource.Method's contract on an in-memory sample.
        Assert.True(
            declarations.Length > 0,
            $"probe declares no method named '{name}'.");
        Assert.True(
            declarations.Length == 1,
            $"probe declares {declarations.Length} methods named '{name}'.");
        return declarations[0];
    }

    private static SyntaxNode Root(string source) =>
        CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview))
            .GetRoot();
}
