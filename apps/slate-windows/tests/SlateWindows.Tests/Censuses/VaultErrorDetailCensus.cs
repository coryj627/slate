// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// W7-7 R-7/R-8 (#1249, #1251): a caught failure reaches speech in core's
// words, and a failed save shows the sentence it speaks.
//
// The host used to word these failures itself — VaultErrorText, a C# table
// of whole save-error sentences, and an editor-integrity status composed
// around the exception's text beside a different spoken line. Core now
// renders every vault error (a11y::vault_error_detail, pinned variant by
// variant) and the integrity sentence (a11y::editor_integrity_detail), and
// the binding exports both. The runtime facts (VaultErrorDetailTests,
// RecoveryAnnouncementTests, W2EditorInteractionTests) prove today's TEXT.
// They cannot prove PROVENANCE: a host literal that reads the same words
// passes them, and rewording core would then fail nothing. This census
// closes that from the source:
//
// - At each site that turns a caught failure into announced detail, the
//   detail IS core's rendering — SlateUniffiMethods.VaultErrorDetail(<the
//   caught exception>) in a VaultException catch, and at the save boundary
//   SlateUniffiMethods.EditorIntegrityDetail() in any other catch — through
//   locals if need be, with no string literal, interpolation or Message
//   read. Every catch of the save path is read, not only the VaultException
//   ones.
// - Every catch of the save path that announces writes Status, and every
//   Status write there is SlateUniffiMethods.A11yRender(e).Text for the
//   NoteSaveBlocked or NoteSaveConflict e that the same catch announces
//   (contract 38 D-10 as amended).

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "vault-error-detail")]
public sealed class VaultErrorDetailCensus
{
    private const string VaultDetail = "SlateUniffiMethods.VaultErrorDetail";
    private const string IntegrityDetail = "SlateUniffiMethods.EditorIntegrityDetail";
    private static readonly string[] FailedSaveEvents =
        ["A11yEvent.NoteSaveBlocked", "A11yEvent.NoteSaveConflict"];

    /// <summary>The sites where a caught failure becomes announced detail,
    /// as (file, method, constructed type, parameter, position, every
    /// catch): the save boundary's NoteSaveBlocked in each of its catches,
    /// and the embed resolver's ReadError reason in its VaultException
    /// catch (its other catch passes a non-vault exception's own message,
    /// mac's localizedDescription arm, by design).</summary>
    public static TheoryData<string, string, string, string, int, bool> Sites() => new()
    {
        { "WorkspaceViewModel.cs", "Save", "A11yEvent.NoteSaveBlocked", "Detail", 1, true },
        { "EditorInteractions.cs", "ResolveEmbedPreview", "EmbedUnresolvedReason.ReadError", "Message", 0, false },
    };

    [Theory]
    [MemberData(nameof(Sites))]
    public void ACaughtFailureIsAnnouncedInCoresWords(
        string file,
        string method,
        string construct,
        string parameter,
        int position,
        bool everyCatch)
    {
        MethodDeclarationSyntax declaration = CSharpSource.Load(file).Method(method);
        string[] failures = DetailFailures(declaration, construct, parameter, position, everyCatch).ToArray();
        Assert.True(failures.Length == 0, $"{file} {method}:\n" + string.Join("\n", failures));
    }

    [Fact]
    public void EveryFailedSaveStatusIsTheRenderedAnnouncement()
    {
        MethodDeclarationSyntax save = CSharpSource.Load("WorkspaceViewModel.cs").Method("Save");
        string[] failures = StatusFailures(save).ToArray();
        Assert.True(failures.Length == 0, string.Join("\n", failures));
    }

    /// <summary>The census's teeth on the VaultException catch: each
    /// host-worded detail is named; the shipped shape, a local and a named
    /// argument are clean; a catch that constructs nothing fails
    /// closed.</summary>
    [Theory]
    [InlineData("var blocked = new A11yEvent.NoteSaveBlocked(filename, SlateUniffiMethods.VaultErrorDetail(exception));", "")]
    [InlineData("string detail = SlateUniffiMethods.VaultErrorDetail(exception); var blocked = new A11yEvent.NoteSaveBlocked(filename, detail);", "")]
    [InlineData("var blocked = new A11yEvent.NoteSaveBlocked(Detail: SlateUniffiMethods.VaultErrorDetail(exception), Filename: filename);", "")]
    [InlineData("var blocked = new A11yEvent.NoteSaveBlocked(filename, \"Could not write the file.\");", "\"Could not write the file.\"")]
    [InlineData("var blocked = new A11yEvent.NoteSaveBlocked(filename, $\"Save failed: {exception.Message}\");", "$\"Save failed: {exception.Message}\"")]
    [InlineData("var blocked = new A11yEvent.NoteSaveBlocked(filename, exception.Message);", "(filename, exception.Message)")]
    [InlineData("string detail = VaultErrorText.HumanReadable(exception); var blocked = new A11yEvent.NoteSaveBlocked(filename, detail);", "(filename, detail)")]
    [InlineData("var blocked = new A11yEvent.NoteSaveBlocked(filename, SlateUniffiMethods.VaultErrorDetail(other));", "VaultErrorDetail(other)")]
    [InlineData("var blocked = new A11yEvent.NoteSaveBlocked(filename, SlateUniffiMethods.EditorIntegrityDetail());", "EditorIntegrityDetail()")]
    [InlineData("var blocked = new A11yEvent.NoteSaveBlocked(filename, SlateUniffiMethods.VaultErrorDetail(exception) + \".\");", "VaultErrorDetail(exception) + \".\"")]
    [InlineData("var blocked = new A11yEvent.NoteSaved(filename);", "constructs no A11yEvent.NoteSaveBlocked")]
    public void TheCensusNamesEveryHostWordedDetail(string body, string namedSite)
    {
        MethodDeclarationSyntax save = Parse(
            "bool Save() { try { Work(); return true; } catch (VaultException exception) {"
            + " string filename = Name(); VaultException other = exception; "
            + body
            + " _announce(blocked); return false; } }");
        AssertNamed(DetailFailures(save, "A11yEvent.NoteSaveBlocked", "Detail", 1, everyCatch: true), namedSite);
    }

    /// <summary>The teeth on the save boundary's OTHER catch (codex round 2
    /// on PR 6): the editor-integrity catch is a failed save too. The
    /// round-1 shape — a host-worded status beside a spoken line carrying
    /// the exception's text — is named on both counts; so is a literal or
    /// a vault renderer for the detail, an announcement with no status, and
    /// a status that renders some other event.</summary>
    [Theory]
    [InlineData("var blocked = new A11yEvent.NoteSaveBlocked(filename, SlateUniffiMethods.EditorIntegrityDetail()); Status = SlateUniffiMethods.A11yRender(blocked).Text; _announce(blocked);", "")]
    [InlineData("Status = $\"Save blocked by editor integrity check: {exception.Message}\"; _announce(new A11yEvent.NoteSaveBlocked(filename, exception.Message));", "(filename, exception.Message)|Status = $\"Save blocked by editor integrity check: {exception.Message}\"")]
    [InlineData("var blocked = new A11yEvent.NoteSaveBlocked(filename, \"The editor's text failed its integrity check.\"); Status = SlateUniffiMethods.A11yRender(blocked).Text; _announce(blocked);", "\"The editor's text failed its integrity check.\"")]
    [InlineData("var blocked = new A11yEvent.NoteSaveBlocked(filename, SlateUniffiMethods.VaultErrorDetail(exception)); Status = SlateUniffiMethods.A11yRender(blocked).Text; _announce(blocked);", "not SlateUniffiMethods.EditorIntegrityDetail()")]
    [InlineData("var blocked = new A11yEvent.NoteSaveBlocked(filename, SlateUniffiMethods.EditorIntegrityDetail()); _announce(blocked);", "no Status write")]
    [InlineData("var blocked = new A11yEvent.HostComposed(\"Save blocked.\", A11yPriority.High); Status = SlateUniffiMethods.A11yRender(blocked).Text; _announce(blocked);", "Status = SlateUniffiMethods.A11yRender(blocked).Text (line 1): not")]
    public void TheCensusReadsTheIntegrityCatchToo(string body, string namedSites)
    {
        MethodDeclarationSyntax save = Parse(
            "bool Save() {"
            + " try { Snapshot(); } catch (Exception exception) when (exception is not OutOfMemoryException) {"
            + " string filename = Name(); "
            + body
            + " return false; }"
            + " try { Work(); return true; } catch (VaultException exception) {"
            + " string filename = Name();"
            + " var blocked = new A11yEvent.NoteSaveBlocked(filename, SlateUniffiMethods.VaultErrorDetail(exception));"
            + " Status = SlateUniffiMethods.A11yRender(blocked).Text; _announce(blocked); return false; } }");
        AssertNamed(
            DetailFailures(save, "A11yEvent.NoteSaveBlocked", "Detail", 1, everyCatch: true)
                .Concat(StatusFailures(save)),
            namedSites);
    }

    /// <summary>The status half's teeth: a host-worded status, the
    /// rendering of an event the catch does not announce, and a catch that
    /// announces with no status are all named.</summary>
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
        AssertNamed(StatusFailures(save), namedSite);
    }

    /// <summary>Every problem with the detail at the constructions of
    /// <paramref name="construct"/> inside the method's catch clauses
    /// (every catch, or only <c>catch (VaultException x)</c>), each naming
    /// its site; empty when every one passes core's rendering.</summary>
    internal static IEnumerable<string> DetailFailures(
        MethodDeclarationSyntax method,
        string construct,
        string parameter,
        int position,
        bool everyCatch)
    {
        var constructions = Catches(method, everyCatch)
            .SelectMany(clause => clause.Block.DescendantNodes()
                .OfType<ObjectCreationExpressionSyntax>()
                .Where(creation => CSharpSource.Normalize(creation.Type) == construct)
                .Select(creation => (Clause: clause, Creation: creation)))
            .ToArray();
        if (constructions.Length == 0)
        {
            yield return $"{method.Identifier.ValueText} constructs no {construct} in a "
                + (everyCatch ? "catch clause" : "catch (VaultException …) clause")
                + "; the census reads at least one.";
            yield break;
        }

        foreach ((CatchClauseSyntax clause, ObjectCreationExpressionSyntax creation) in constructions)
        {
            string site = Site(creation);
            ExpressionSyntax? written = Argument(creation, parameter, position);
            if (written is null)
            {
                yield return $"{site}: no {parameter} argument.";
                continue;
            }

            ExpressionSyntax value = CSharpSource.Resolve(written, clause);
            if (HostCopy(written) || HostCopy(value))
            {
                yield return $"{site}: host copy (a string literal, an interpolation or a "
                    + "Message read) reaches the detail.";
            }
            else if (!IsCoreDetail(value, clause))
            {
                yield return $"{site}: the detail is not {ExpectedDetail(clause)}, core's rendering "
                    + "for this catch.";
            }
        }
    }

    /// <summary>Every problem with the failed-save statuses: each catch of
    /// the method that announces must write Status, and each Status write
    /// in a catch must be <c>SlateUniffiMethods.A11yRender(e).Text</c> for
    /// the NoteSaveBlocked or NoteSaveConflict <c>e</c> that the same catch
    /// announces.</summary>
    internal static IEnumerable<string> StatusFailures(MethodDeclarationSyntax method)
    {
        CatchClauseSyntax[] catches = Catches(method, everyCatch: true).ToArray();
        int writes = 0;
        foreach (CatchClauseSyntax clause in catches)
        {
            AssignmentExpressionSyntax[] statuses = clause.Block.DescendantNodes()
                .OfType<AssignmentExpressionSyntax>()
                .Where(assignment => assignment.Left is IdentifierNameSyntax { Identifier.ValueText: "Status" })
                .ToArray();
            bool announces = Announcements(clause).Any();
            if (announces && statuses.Length == 0)
            {
                yield return $"{CatchSite(clause)}: announces with no Status write; "
                    + "the status must show the sentence that is spoken.";
            }

            foreach (AssignmentExpressionSyntax assignment in statuses)
            {
                writes++;
                string site = Site(assignment);
                ExpressionSyntax value = CSharpSource.Resolve(assignment.Right, clause);
                if (HostCopy(assignment.Right) || HostCopy(value))
                {
                    yield return $"{site}: host copy reaches the status.";
                }
                else if (!IsRenderingOfAnnounced(value, clause))
                {
                    yield return $"{site}: not SlateUniffiMethods.A11yRender(e).Text for the "
                        + "NoteSaveBlocked or NoteSaveConflict e this catch announces.";
                }
            }
        }

        if (writes == 0)
        {
            yield return $"{method.Identifier.ValueText} has no Status write in any catch "
                + "clause; the census reads at least one.";
        }
    }

    private static IEnumerable<CatchClauseSyntax> Catches(MethodDeclarationSyntax method, bool everyCatch) =>
        method.DescendantNodes()
            .OfType<CatchClauseSyntax>()
            .Where(clause => everyCatch || IsVaultCatch(clause));

    private static bool IsVaultCatch(CatchClauseSyntax clause) =>
        clause.Declaration is { } declaration
        && CSharpSource.Normalize(declaration.Type) == "VaultException";

    private static string ExpectedDetail(CatchClauseSyntax clause) =>
        IsVaultCatch(clause)
            ? $"{VaultDetail}({(clause.Declaration!.Identifier.ValueText is { Length: > 0 } caught ? caught : "<unnamed>")})"
            : $"{IntegrityDetail}()";

    /// <summary>Core's rendering for the catch: in a VaultException catch,
    /// <c>SlateUniffiMethods.VaultErrorDetail(caught)</c> exactly; in any
    /// other catch, <c>SlateUniffiMethods.EditorIntegrityDetail()</c>.</summary>
    private static bool IsCoreDetail(ExpressionSyntax value, CatchClauseSyntax clause)
    {
        if (value is not InvocationExpressionSyntax invocation)
        {
            return false;
        }

        string callee = CSharpSource.Normalize(invocation.Expression);
        if (!IsVaultCatch(clause))
        {
            return callee == IntegrityDetail && invocation.ArgumentList.Arguments.Count == 0;
        }

        string caught = clause.Declaration!.Identifier.ValueText;
        return callee == VaultDetail
            && caught.Length > 0
            && invocation.ArgumentList.Arguments.Count == 1
            && invocation.ArgumentList.Arguments[0].Expression
                is IdentifierNameSyntax { Identifier.ValueText: var argument }
            && argument == caught;
    }

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

    /// <summary>The single arguments passed to <c>_announce</c> in the
    /// clause.</summary>
    private static IEnumerable<ExpressionSyntax> Announcements(CatchClauseSyntax clause) =>
        clause.Block.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(invocation => CSharpSource.Normalize(invocation.Expression) == "_announce"
                && invocation.ArgumentList.Arguments.Count == 1)
            .Select(invocation => invocation.ArgumentList.Arguments[0].Expression);

    /// <summary><c>SlateUniffiMethods.A11yRender(e).Text</c>, where
    /// <c>e</c> is a local of the clause initialised to a
    /// <c>new A11yEvent.NoteSaveBlocked(…)</c> or
    /// <c>new A11yEvent.NoteSaveConflict(…)</c>, and the clause passes that
    /// same local to <c>_announce</c>.</summary>
    private static bool IsRenderingOfAnnounced(ExpressionSyntax value, CatchClauseSyntax clause) =>
        value is MemberAccessExpressionSyntax
        {
            Name.Identifier.ValueText: "Text",
            Expression: InvocationExpressionSyntax render,
        }
        && CSharpSource.Normalize(render.Expression) == "SlateUniffiMethods.A11yRender"
        && render.ArgumentList.Arguments.Count == 1
        && render.ArgumentList.Arguments[0].Expression
            is IdentifierNameSyntax { Identifier.ValueText: var announced }
        && CSharpSource.Resolve(render.ArgumentList.Arguments[0].Expression, clause)
            is ObjectCreationExpressionSyntax creation
        && FailedSaveEvents.Contains(CSharpSource.Normalize(creation.Type))
        && Announcements(clause).Any(argument =>
            argument is IdentifierNameSyntax { Identifier.ValueText: var passed }
            && passed == announced);

    private static bool HostCopy(SyntaxNode value) =>
        value.DescendantNodesAndSelf().Any(node =>
            node is InterpolatedStringExpressionSyntax
            || (node is LiteralExpressionSyntax literal
                && literal.IsKind(SyntaxKind.StringLiteralExpression))
            || (node is MemberAccessExpressionSyntax access
                && access.Name.Identifier.ValueText is "Message" or "message"));

    private static void AssertNamed(IEnumerable<string> failures, string namedSites)
    {
        string[] found = failures.ToArray();
        if (namedSites.Length == 0)
        {
            Assert.Empty(found);
            return;
        }

        foreach (string named in namedSites.Split('|'))
        {
            Assert.True(
                found.Any(failure => failure.Contains(named, StringComparison.Ordinal)),
                $"no failure names \"{named}\":\n" + string.Join("\n", found));
        }
    }

    private static string Site(SyntaxNode node) =>
        $"{node} (line {node.GetLocation().GetLineSpan().StartLinePosition.Line + 1})";

    private static string CatchSite(CatchClauseSyntax clause) =>
        $"catch {clause.Declaration} (line {clause.GetLocation().GetLineSpan().StartLinePosition.Line + 1})";

    private static MethodDeclarationSyntax Parse(string method) =>
        CSharpSyntaxTree.ParseText($"class C {{ {method} }}")
            .GetRoot()
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Single();
}
