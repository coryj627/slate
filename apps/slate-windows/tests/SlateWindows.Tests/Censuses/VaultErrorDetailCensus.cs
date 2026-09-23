// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// W7-7 R-7/R-8 (#1249, #1251): a caught VaultException reaches speech in
// core's words.
//
// The host used to word these errors itself — VaultErrorText, a C# table
// of whole save-error sentences. Core now renders every variant
// (a11y::vault_error_detail, pinned variant by variant in core) and the
// binding exports it (SlateUniffiMethods.VaultErrorDetail). The runtime
// facts (VaultErrorDetailTests, RecoveryAnnouncementTests,
// W2EditorInteractionTests) prove today's TEXT. They cannot prove
// PROVENANCE: a host literal that reads the same words passes them, and
// rewording core would then fail nothing. This census closes that from the
// source. At each site that turns a caught VaultException into announced
// detail, the detail IS SlateUniffiMethods.VaultErrorDetail(<the caught
// exception>) — through locals if need be — with no string literal,
// interpolation or Message read; and the save boundary's status is the
// rendering of the event it announces.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "vault-error-detail")]
public sealed class VaultErrorDetailCensus
{
    private const string CoreDetail = "SlateUniffiMethods.VaultErrorDetail";

    /// <summary>The sites where a caught VaultException becomes announced
    /// detail, as (file, method, constructed type, parameter, position):
    /// the save boundary's NoteSaveBlocked and the embed resolver's
    /// ReadError reason.</summary>
    public static TheoryData<string, string, string, string, int> Sites() => new()
    {
        { "WorkspaceViewModel.cs", "Save", "A11yEvent.NoteSaveBlocked", "Detail", 1 },
        { "EditorInteractions.cs", "ResolveEmbedPreview", "EmbedUnresolvedReason.ReadError", "Message", 0 },
    };

    [Theory]
    [MemberData(nameof(Sites))]
    public void ACaughtVaultErrorIsAnnouncedInCoresWords(
        string file,
        string method,
        string construct,
        string parameter,
        int position)
    {
        MethodDeclarationSyntax declaration = CSharpSource.Load(file).Method(method);
        string[] failures = DetailFailures(declaration, construct, parameter, position).ToArray();
        Assert.True(failures.Length == 0, $"{file} {method}:\n" + string.Join("\n", failures));
    }

    [Fact]
    public void TheSaveFailureStatusIsTheRenderedAnnouncement()
    {
        MethodDeclarationSyntax save = CSharpSource.Load("WorkspaceViewModel.cs").Method("Save");
        string[] failures = StatusFailures(save).ToArray();
        Assert.True(failures.Length == 0, string.Join("\n", failures));
    }

    /// <summary>The census's teeth, on the shapes a regression takes: each
    /// host-worded detail is named; the shipped shape, a local and a named
    /// argument are clean; a catch that constructs nothing fails closed.</summary>
    [Theory]
    [InlineData("var blocked = new A11yEvent.NoteSaveBlocked(filename, SlateUniffiMethods.VaultErrorDetail(exception));", "")]
    [InlineData("string detail = SlateUniffiMethods.VaultErrorDetail(exception); var blocked = new A11yEvent.NoteSaveBlocked(filename, detail);", "")]
    [InlineData("var blocked = new A11yEvent.NoteSaveBlocked(Detail: SlateUniffiMethods.VaultErrorDetail(exception), Filename: filename);", "")]
    [InlineData("var blocked = new A11yEvent.NoteSaveBlocked(filename, \"Could not write the file.\");", "\"Could not write the file.\"")]
    [InlineData("var blocked = new A11yEvent.NoteSaveBlocked(filename, $\"Save failed: {exception.Message}\");", "$\"Save failed: {exception.Message}\"")]
    [InlineData("var blocked = new A11yEvent.NoteSaveBlocked(filename, exception.Message);", "(filename, exception.Message)")]
    [InlineData("string detail = VaultErrorText.HumanReadable(exception); var blocked = new A11yEvent.NoteSaveBlocked(filename, detail);", "(filename, detail)")]
    [InlineData("var blocked = new A11yEvent.NoteSaveBlocked(filename, SlateUniffiMethods.VaultErrorDetail(other));", "VaultErrorDetail(other)")]
    [InlineData("var blocked = new A11yEvent.NoteSaveBlocked(filename, SlateUniffiMethods.VaultErrorDetail(exception) + \".\");", "VaultErrorDetail(exception) + \".\"")]
    [InlineData("var blocked = new A11yEvent.NoteSaved(filename);", "constructs no A11yEvent.NoteSaveBlocked")]
    public void TheCensusNamesEveryHostWordedDetail(string body, string namedSite)
    {
        MethodDeclarationSyntax save = Parse(
            "bool Save() { try { Work(); return true; } catch (VaultException exception) {"
            + " string filename = Name(); VaultException other = exception; "
            + body
            + " _announce(blocked); return false; } }");
        string[] failures = DetailFailures(save, "A11yEvent.NoteSaveBlocked", "Detail", 1).ToArray();
        if (namedSite.Length == 0)
        {
            Assert.Empty(failures);
        }
        else
        {
            Assert.Contains(failures, failure => failure.Contains(namedSite, StringComparison.Ordinal));
        }
    }

    /// <summary>The status half's teeth: a host-worded status and the
    /// rendering of an event the catch does not announce are both named.</summary>
    [Theory]
    [InlineData("Status = SlateUniffiMethods.A11yRender(blocked).Text;", "")]
    [InlineData("string sentence = SlateUniffiMethods.A11yRender(blocked).Text; Status = sentence;", "")]
    [InlineData("Status = $\"Save blocked: {detail}\";", "Status = $\"Save blocked: {detail}\"")]
    [InlineData("Status = SlateUniffiMethods.A11yRender(new A11yEvent.NoteSaved(filename)).Text;", "Status = SlateUniffiMethods.A11yRender(new A11yEvent.NoteSaved(filename)).Text")]
    [InlineData("", "no Status write")]
    public void TheCensusNamesEveryHostWordedStatus(string body, string namedSite)
    {
        MethodDeclarationSyntax save = Parse(
            "bool Save() { try { Work(); return true; } catch (VaultException exception) {"
            + " string filename = Name(); string detail = SlateUniffiMethods.VaultErrorDetail(exception);"
            + " var blocked = new A11yEvent.NoteSaveBlocked(filename, detail); "
            + body
            + " _announce(blocked); return false; } }");
        string[] failures = StatusFailures(save).ToArray();
        if (namedSite.Length == 0)
        {
            Assert.Empty(failures);
        }
        else
        {
            Assert.Contains(failures, failure => failure.Contains(namedSite, StringComparison.Ordinal));
        }
    }

    /// <summary>Every problem with the detail at the constructions of
    /// <paramref name="construct"/> inside the method's
    /// <c>catch (VaultException x)</c> clauses, each naming its site;
    /// empty when every one passes core's rendering of the caught
    /// exception.</summary>
    internal static IEnumerable<string> DetailFailures(
        MethodDeclarationSyntax method,
        string construct,
        string parameter,
        int position)
    {
        var constructions = VaultCatches(method)
            .SelectMany(clause => clause.Block.DescendantNodes()
                .OfType<ObjectCreationExpressionSyntax>()
                .Where(creation => CSharpSource.Normalize(creation.Type) == construct)
                .Select(creation => (Clause: clause, Creation: creation)))
            .ToArray();
        if (constructions.Length == 0)
        {
            yield return $"{method.Identifier.ValueText} constructs no {construct} in a "
                + "catch (VaultException …) clause; the census reads at least one.";
            yield break;
        }

        foreach ((CatchClauseSyntax clause, ObjectCreationExpressionSyntax creation) in constructions)
        {
            string site = Site(creation);
            string caught = clause.Declaration!.Identifier.ValueText;
            ExpressionSyntax? written = Argument(creation, parameter, position);
            if (written is null)
            {
                yield return $"{site}: no {parameter} argument.";
                continue;
            }

            ExpressionSyntax value = CSharpSource.Resolve(written, method);
            if (HostCopy(written) || HostCopy(value))
            {
                yield return $"{site}: host copy (a string literal, an interpolation or a "
                    + "Message read) reaches the detail.";
            }
            else if (caught.Length == 0 || !IsCoreDetail(value, caught))
            {
                yield return $"{site}: the detail is not {CoreDetail}({(caught.Length == 0 ? "<unnamed>" : caught)}), "
                    + "core's rendering of the caught exception.";
            }
        }
    }

    /// <summary>Every problem with the status writes inside the method's
    /// <c>catch (VaultException x)</c> clauses: each must be
    /// <c>SlateUniffiMethods.A11yRender(e).Text</c> for an event
    /// <c>e</c> that the same clause announces.</summary>
    internal static IEnumerable<string> StatusFailures(MethodDeclarationSyntax method)
    {
        var writes = VaultCatches(method)
            .SelectMany(clause => clause.Block.DescendantNodes()
                .OfType<AssignmentExpressionSyntax>()
                .Where(assignment => assignment.Left is IdentifierNameSyntax { Identifier.ValueText: "Status" })
                .Select(assignment => (Clause: clause, Assignment: assignment)))
            .ToArray();
        if (writes.Length == 0)
        {
            yield return $"{method.Identifier.ValueText} has no Status write in a "
                + "catch (VaultException …) clause; the census reads at least one.";
            yield break;
        }

        foreach ((CatchClauseSyntax clause, AssignmentExpressionSyntax assignment) in writes)
        {
            string site = Site(assignment);
            ExpressionSyntax value = CSharpSource.Resolve(assignment.Right, method);
            if (HostCopy(assignment.Right) || HostCopy(value))
            {
                yield return $"{site}: host copy reaches the status.";
            }
            else if (!IsRenderingOfAnnounced(value, clause, method))
            {
                yield return $"{site}: not SlateUniffiMethods.A11yRender(e).Text for an event e "
                    + "this catch announces.";
            }
        }
    }

    private static IEnumerable<CatchClauseSyntax> VaultCatches(MethodDeclarationSyntax method) =>
        method.DescendantNodes()
            .OfType<CatchClauseSyntax>()
            .Where(clause => clause.Declaration is { } declaration
                && CSharpSource.Normalize(declaration.Type) == "VaultException");

    /// <summary>The argument bound to <paramref name="parameter"/>: named
    /// if written with its name, otherwise the positional one.</summary>
    private static ExpressionSyntax? Argument(
        ObjectCreationExpressionSyntax creation,
        string parameter,
        int position)
    {
        SeparatedSyntaxList<ArgumentSyntax> arguments =
            creation.ArgumentList?.Arguments ?? default;
        ArgumentSyntax? named = arguments.FirstOrDefault(argument =>
            argument.NameColon?.Name.Identifier.ValueText == parameter);
        if (named is not null)
        {
            return named.Expression;
        }
        return arguments.Count > position && arguments[position].NameColon is null
            ? arguments[position].Expression
            : null;
    }

    /// <summary><c>SlateUniffiMethods.VaultErrorDetail(caught)</c>, exactly.</summary>
    private static bool IsCoreDetail(ExpressionSyntax value, string caught) =>
        value is InvocationExpressionSyntax invocation
        && CSharpSource.Normalize(invocation.Expression) == CoreDetail
        && invocation.ArgumentList.Arguments.Count == 1
        && invocation.ArgumentList.Arguments[0].Expression
            is IdentifierNameSyntax { Identifier.ValueText: var argument }
        && argument == caught;

    /// <summary><c>SlateUniffiMethods.A11yRender(e).Text</c>, where
    /// <c>e</c> is a local initialised to a <c>new A11yEvent.…</c> and the
    /// clause passes that same local to <c>_announce</c>.</summary>
    private static bool IsRenderingOfAnnounced(
        ExpressionSyntax value,
        CatchClauseSyntax clause,
        MethodDeclarationSyntax method) =>
        value is MemberAccessExpressionSyntax
        {
            Name.Identifier.ValueText: "Text",
            Expression: InvocationExpressionSyntax render,
        }
        && CSharpSource.Normalize(render.Expression) == "SlateUniffiMethods.A11yRender"
        && render.ArgumentList.Arguments.Count == 1
        && render.ArgumentList.Arguments[0].Expression
            is IdentifierNameSyntax { Identifier.ValueText: var announced }
        && CSharpSource.Resolve(render.ArgumentList.Arguments[0].Expression, method)
            is ObjectCreationExpressionSyntax creation
        && CSharpSource.Normalize(creation.Type).StartsWith("A11yEvent.", StringComparison.Ordinal)
        && clause.Block.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Any(invocation => CSharpSource.Normalize(invocation.Expression) == "_announce"
                && invocation.ArgumentList.Arguments.Count == 1
                && invocation.ArgumentList.Arguments[0].Expression
                    is IdentifierNameSyntax { Identifier.ValueText: var passed }
                && passed == announced);

    private static bool HostCopy(SyntaxNode value) =>
        value.DescendantNodesAndSelf().Any(node =>
            node is InterpolatedStringExpressionSyntax
            || (node is LiteralExpressionSyntax literal
                && literal.IsKind(SyntaxKind.StringLiteralExpression))
            || (node is MemberAccessExpressionSyntax access
                && access.Name.Identifier.ValueText is "Message" or "message"));

    private static string Site(SyntaxNode node) =>
        $"{node} (line {node.GetLocation().GetLineSpan().StartLinePosition.Line + 1})";

    private static MethodDeclarationSyntax Parse(string method) =>
        CSharpSyntaxTree.ParseText($"class C {{ {method} }}")
            .GetRoot()
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Single();
}
