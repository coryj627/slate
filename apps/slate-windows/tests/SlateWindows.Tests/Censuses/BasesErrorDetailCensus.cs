// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

// #1697: a Bases failure is spoken in core's words. A VaultException's
// Message is the binding's field rendering ("@message=..."), not the error's
// text; every announcement the Bases sources build from a caught failure goes
// through BasePhrase.ErrorDetail, which is core's VaultErrorDetail for a
// vault error and the exception's own message otherwise. This census reads
// every A11yEvent construction in those sources and names any argument that
// reads a Message.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "bases-error-detail")]
public sealed class BasesErrorDetailCensus
{
    /// <summary>The Bases sources: the workspace's Bases half and every file
    /// in the Bases folder.</summary>
    private static IEnumerable<string> BasesSources()
    {
        string root = SourceText.ShellSourceRoot();
        yield return Path.Combine(root, "WorkspaceViewModel.Bases.cs");
        foreach (string file in Directory.EnumerateFiles(Path.Combine(root, "Bases"), "*.cs").Order(StringComparer.Ordinal))
        {
            yield return file;
        }
    }

    [Fact]
    public void NoBasesAnnouncementReadsAnExceptionsMessage()
    {
        string[] failures = [.. BasesSources()
            .SelectMany(path => MessageReads(CSharpSource.LoadPath(path).Root, Path.GetFileName(path)))];
        Assert.True(failures.Length == 0, string.Join("\n", failures));
    }

    /// <summary>Codex round 1: a caught exception's Message never leaves the
    /// catch as text either (a local, a return, a property the sentence
    /// reads later), outside the helper itself.</summary>
    [Fact]
    public void NoBasesExceptionMessageIsReadOutsideTheHelper()
    {
        string[] failures = [.. BasesSources()
            .SelectMany(path => ExceptionMessageReads(CSharpSource.LoadPath(path).Root, Path.GetFileName(path)))];
        Assert.True(failures.Length == 0, string.Join("\n", failures));
    }

    /// <summary>The teeth of the wider rule: a caught exception's Message
    /// stored, returned or patterned is named; a model's own Message property
    /// is not.</summary>
    [Theory]
    [InlineData("try { Work(); } catch (VaultException failure) { detail = failure.Message; }", "failure.Message")]
    [InlineData("try { Work(); } catch (VaultException failure) { return failure.Message; }", "failure.Message")]
    [InlineData("if (thrown is VaultException vault) { detail = vault.Message; }", "vault.Message")]
    [InlineData("try { Work(); } catch (VaultException failure) { detail = BasePhrase.ErrorDetail(failure); }", "")]
    [InlineData("detail = validation.Message ?? \"Expression invalid\";", "")]
    public void TheCensusNamesACaughtMessageWhereverItGoes(string body, string named)
    {
        SyntaxNode root = CSharpSyntaxTree.ParseText(
            "class C { string M(Exception thrown) { string detail; " + body + " return detail; } }").GetRoot();
        string[] failures = [.. ExceptionMessageReads(root, "planted.cs")];
        if (named.Length == 0)
        {
            Assert.Empty(failures);
        }
        else
        {
            Assert.Contains(failures, failure => failure.Contains(named, StringComparison.Ordinal));
        }
    }

    /// <summary>Every <c>x.Message</c> read where <c>x</c> is declared in the
    /// same member as an exception: a catch variable, a
    /// <c>is SomeException x</c> pattern or an exception-typed parameter.
    /// <c>BasePhrase.ErrorDetail</c>, the one place a non-vault message is
    /// read on purpose, is exempt.</summary>
    internal static IEnumerable<string> ExceptionMessageReads(SyntaxNode root, string file)
    {
        foreach (MemberDeclarationSyntax member in root.DescendantNodes().OfType<MemberDeclarationSyntax>()
            .Where(candidate => candidate is BaseMethodDeclarationSyntax or PropertyDeclarationSyntax))
        {
            if (member is MethodDeclarationSyntax { Identifier.ValueText: "ErrorDetail" })
            {
                continue;
            }
            HashSet<string> exceptions = [.. member.DescendantNodes().SelectMany(ExceptionNames)];
            foreach (MemberAccessExpressionSyntax access in member.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
            {
                if (access.Name.Identifier.ValueText == "Message"
                    && access.Expression is IdentifierNameSyntax { Identifier.ValueText: var target }
                    && exceptions.Contains(target))
                {
                    yield return $"{file} line {access.GetLocation().GetLineSpan().StartLinePosition.Line + 1}: "
                        + $"{access} reads a caught exception's message; use BasePhrase.ErrorDetail.";
                }
            }
        }
    }

    private static IEnumerable<string> ExceptionNames(SyntaxNode node) => node switch
    {
        CatchDeclarationSyntax { Identifier.ValueText: { Length: > 0 } name } => [name],
        DeclarationPatternSyntax { Designation: SingleVariableDesignationSyntax designation } pattern
            when IsException(pattern.Type) => [designation.Identifier.ValueText],
        ParameterSyntax { Type: { } type } parameter when IsException(type) => [parameter.Identifier.ValueText],
        _ => [],
    };

    private static bool IsException(TypeSyntax type) =>
        CSharpSource.Normalize(type).TrimEnd('?').EndsWith("Exception", StringComparison.Ordinal);

    /// <summary>The census's teeth: a Message read in the constructor's
    /// argument, bare or interpolated, is named; core's rendering through
    /// the helper is clean.</summary>
    [Theory]
    [InlineData("_announce(new A11yEvent.BasesCellEditFailed(failure.Message));", "failure.Message")]
    [InlineData("_announce(new A11yEvent.BasesViewExportFailed($\"Export failed: {failure.Message}\"));", "failure.Message")]
    [InlineData("_announce(new A11yEvent.BasesSavedQueryCreateFailed(Detail: failure.Message));", "failure.Message")]
    [InlineData("_announce(new A11yEvent.BasesCellEditFailed(BasePhrase.ErrorDetail(failure)));", "")]
    [InlineData("_announce(new A11yEvent.BasesCellEditFailed(SlateUniffiMethods.VaultErrorDetail(failure)));", "")]
    public void TheCensusNamesAMessageRead(string body, string named)
    {
        SyntaxNode root = CSharpSyntaxTree.ParseText(
            "class C { void M(VaultException failure) { " + body + " } }").GetRoot();
        string[] failures = [.. MessageReads(root, "planted.cs")];
        if (named.Length == 0)
        {
            Assert.Empty(failures);
        }
        else
        {
            Assert.Contains(failures, failure => failure.Contains(named, StringComparison.Ordinal));
        }
    }

    internal static IEnumerable<string> MessageReads(SyntaxNode root, string file) =>
        root.DescendantNodes()
            .OfType<ObjectCreationExpressionSyntax>()
            .Where(creation => CSharpSource.Normalize(creation.Type).StartsWith("A11yEvent.", StringComparison.Ordinal))
            .SelectMany(creation => (creation.ArgumentList?.Arguments ?? default)
                .SelectMany(argument => argument.DescendantNodesAndSelf().OfType<MemberAccessExpressionSyntax>())
                .Where(access => access.Name.Identifier.ValueText == "Message")
                .Select(access =>
                    $"{file} line {creation.GetLocation().GetLineSpan().StartLinePosition.Line + 1}: "
                    + $"{CSharpSource.Normalize(creation.Type)} reads {access}; use BasePhrase.ErrorDetail."));
}
