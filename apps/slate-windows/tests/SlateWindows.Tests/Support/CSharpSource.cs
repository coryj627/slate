// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SlateWindows.Tests;

/// <summary>
/// A production C# file, parsed, for the drift twins to read (#1108).
/// </summary>
/// <remarks>
/// <para>
/// These tests exist to prove the shipped code agrees with the
/// declarative tables. Regexes could not do that, because a regex cannot
/// tell live code from dead text. Four adversarial review rounds on #741
/// demonstrated three bypasses, and the syntax tree closes all three by
/// construction rather than by another pattern:
/// </para>
/// <list type="bullet">
/// <item><description>Comments and inactive <c>#if</c> regions are
/// trivia, never nodes, so commented-out code cannot answer a
/// query.</description></item>
/// <item><description>A string's contents are a
/// <c>LiteralExpressionSyntax</c>, structurally distinct from the code it
/// might quote — <c>"Key.Up"</c> is not a member access.</description></item>
/// <item><description>Declarations are enumerable, so overload ambiguity
/// is a countable fact rather than whatever
/// <c>Regex.Match</c> happened to hit first.</description></item>
/// </list>
/// <para>
/// Syntax only, deliberately. Resolving symbols would need a compilation
/// and every referenced assembly; these queries are about what the source
/// SAYS, and keeping it syntactic keeps the twins in the unit suite.
/// </para>
/// </remarks>
internal sealed class CSharpSource
{
    private CSharpSource(string path, CompilationUnitSyntax root)
    {
        Path = path;
        Root = root;
    }

    /// <summary>The file this was parsed from, for failure messages.</summary>
    internal string Path { get; }

    /// <summary>The parsed compilation unit.</summary>
    internal CompilationUnitSyntax Root { get; }

    /// <summary>
    /// Parses a file under the Windows shell's source root.
    /// </summary>
    /// <remarks>
    /// A parse error is a hard failure rather than a warning. A tree built
    /// from source the parser did not understand is partial, and every
    /// query over it silently returns less than the truth — which is the
    /// precise failure mode this class replaces.
    /// </remarks>
    internal static CSharpSource Load(params string[] relativeSegments)
    {
        string path = System.IO.Path.Combine(
            new[] { SourceText.ShellSourceRoot() }.Concat(relativeSegments).ToArray());
        Assert.True(File.Exists(path), $"{path} does not exist.");

        SyntaxTree tree = CSharpSyntaxTree.ParseText(
            File.ReadAllText(path),
            new CSharpParseOptions(LanguageVersion.Preview),
            path: path);

        Diagnostic[] errors = tree.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        Assert.True(
            errors.Length == 0,
            $"{path} did not parse cleanly, so every query over it would "
            + "return less than the truth:\n"
            + string.Join("\n", errors.Select(diagnostic => diagnostic.ToString())));

        return new CSharpSource(path, (CompilationUnitSyntax)tree.GetRoot());
    }

    /// <summary>
    /// The one method with this name. Overload ambiguity fails.
    /// </summary>
    /// <remarks>
    /// Codex's round-4 attack: declare an unused overload above the
    /// shipping handler carrying the text a scrape looks for, then gut the
    /// real signature. A first-match lookup reads the decoy. Counting the
    /// declarations makes that a failure instead.
    /// </remarks>
    internal MethodDeclarationSyntax Method(string name)
    {
        MethodDeclarationSyntax[] declarations = Root
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Where(method => method.Identifier.ValueText == name)
            .ToArray();

        Assert.True(
            declarations.Length > 0,
            $"{Path} declares no method named '{name}'. The query that reads "
            + "it would return nothing at all, so it fails here instead.");
        Assert.True(
            declarations.Length == 1,
            $"{Path} declares {declarations.Length} methods named '{name}'. "
            + "This query reads one of them, so the rest go unchecked — "
            + "disambiguate before adding an overload.");
        return declarations[0];
    }

    /// <summary>
    /// Every <c>Key.X</c> member access under <paramref name="node"/>,
    /// as the bare member name.
    /// </summary>
    internal static IEnumerable<string> KeyNames(SyntaxNode node) =>
        MemberAccesses(node, "Key").Select(access => access.Name.Identifier.ValueText);

    /// <summary>
    /// Every <c>{qualifier}.Something</c> member access under
    /// <paramref name="node"/>.
    /// </summary>
    internal static IEnumerable<MemberAccessExpressionSyntax> MemberAccesses(
        SyntaxNode node, string qualifier) =>
        node.DescendantNodesAndSelf()
            .OfType<MemberAccessExpressionSyntax>()
            .Where(access => access.Expression is IdentifierNameSyntax identifier
                && identifier.Identifier.ValueText == qualifier);

    /// <summary>
    /// Whether <paramref name="node"/> contains a call to
    /// <paramref name="expression"/>, written exactly that way.
    /// </summary>
    internal static bool Invokes(SyntaxNode node, string expression) =>
        node.DescendantNodesAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .Any(invocation => Normalize(invocation.Expression) == expression);

    /// <summary>
    /// Source text with trivia and whitespace removed, for comparing an
    /// expression against a written form.
    /// </summary>
    internal static string Normalize(SyntaxNode node) =>
        string.Concat(node.ToString().Where(character => !char.IsWhiteSpace(character)));
}
