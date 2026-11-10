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
