// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// W7-7 PR 8 (#1253, contract R-10; owner decision OD-12's one entry): every
// editor landing request is created in ONE place, the shell's landing entry,
// which refuses under an open modal surface and puts the window's slot over a
// canvas or graph request BEFORE the request exists. Codex PR 8 round 8 found
// the workspace funnel still raising the documents' requests itself, ahead of
// the window, so a realized surface could seat or refuse them unowned — a
// fact about outcomes could not see the site, so this census reads the
// shipping sources.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SlateWindows.Tests.Censuses;

public sealed class EditorLandingEntryCensus
{
    /// <summary>The methods allowed to create a landing request: the entry's
    /// document half and its reading arm, and the two surface-owned
    /// restorations contract 40 scopes out (AR-42), each raised only for a
    /// reader already inside its surface.</summary>
    private static readonly string[] Permitted =
    [
        "MainWindow.ShellRegions.cs:LandDocument",
        "MainWindow.xaml.cs:FocusEditorPane",
        "Canvas/CanvasSurfaceView.cs:FocusProjection",
        "Graph/GraphSurfaceView.cs:RequestProjectionFocus",
    ];

    [Fact]
    public void EveryLandingRequestIsCreatedByTheShellsOneEntry()
    {
        string shellRoot = SourceText.ShellSourceRoot();
        var sites = new List<string>();
        foreach (string file in Directory.EnumerateFiles(shellRoot, "*.cs", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(shellRoot, file).Replace('\\', '/');
            if (relative.StartsWith("obj/", StringComparison.Ordinal)
                || relative.StartsWith("bin/", StringComparison.Ordinal)
                || relative.Contains("/obj/", StringComparison.Ordinal))
            {
                continue;
            }

            CSharpSource source = CSharpSource.LoadPath(file);
            foreach (InvocationExpressionSyntax invocation in Creations(source.Root))
            {
                string? member = invocation.Ancestors().OfType<MemberDeclarationSyntax>().FirstOrDefault() switch
                {
                    MethodDeclarationSyntax method => method.Identifier.ValueText,
                    PropertyDeclarationSyntax property => property.Identifier.ValueText,
                    ConstructorDeclarationSyntax => ".ctor",
                    _ => null,
                };
                sites.Add($"{relative}:{member}");
            }
        }

        string[] foreign = [.. sites.Where(site => !Permitted.Contains(site, StringComparer.Ordinal)).Distinct()];
        Assert.True(
            foreign.Length == 0,
            "an editor landing request is created outside the shell's one entry (OD-12) — a request raised "
            + "there is published before the window's slot owns it and without its modal check: "
            + string.Join("; ", foreign));
        string[] unused = [.. Permitted.Where(site => !sites.Contains(site, StringComparer.Ordinal))];
        Assert.True(
            unused.Length == 0,
            "a permitted creation site creates nothing, so this census exempts nothing there: " + string.Join("; ", unused));
    }

    [Fact]
    public void TheEntryOwnsADocumentRequestBeforeRaisingIt()
    {
        MethodDeclarationSyntax land = CSharpSource.Load("MainWindow.ShellRegions.cs").Method("LandDocument");
        InvocationExpressionSyntax[] holds =
            [.. land.DescendantNodes().OfType<InvocationExpressionSyntax>().Where(invocation => Named(invocation, "Hold"))];
        InvocationExpressionSyntax[] raises = [.. Creations(land)];
        Assert.True(holds.Length == 1, $"LandDocument holds the landing {holds.Length} times; the entry holds it once.");
        Assert.True(raises.Length > 0, "LandDocument raises no request, so it is no entry.");
        Assert.All(
            raises,
            raise => Assert.True(
                holds[0].SpanStart < raise.SpanStart,
                $"LandDocument raises a request (line {raise.GetLocation().GetLineSpan().StartLinePosition.Line + 1}) "
                + "before the window's slot holds the landing: a surface could seat or refuse it unowned."));
    }

    [Fact]
    public void TheEntryCreatesNothingUnderAnOpenModal()
    {
        MethodDeclarationSyntax entry = CSharpSource.Load("MainWindow.xaml.cs").Method("FocusEditorPane");
        StatementSyntax first = Assert.IsAssignableFrom<StatementSyntax>(entry.Body!.Statements[0]);
        var guard = Assert.IsType<IfStatementSyntax>(first);
        Assert.Contains("OpenModalSurface", guard.Condition.ToString(), StringComparison.Ordinal);
        // A modal loop over the shell (#1275's monitor) holds the keys too.
        Assert.Contains("_modalLoops.IsModalLoopActive", guard.Condition.ToString(), StringComparison.Ordinal);
        Assert.NotEmpty(guard.Statement.DescendantNodesAndSelf().OfType<ReturnStatementSyntax>());
    }

    private static IEnumerable<InvocationExpressionSyntax> Creations(SyntaxNode root) =>
        root.DescendantNodes().OfType<InvocationExpressionSyntax>().Where(invocation => Named(invocation, "RequestFocusLanding"));

    private static bool Named(InvocationExpressionSyntax invocation, string name) => invocation.Expression switch
    {
        MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText == name,
        MemberBindingExpressionSyntax binding => binding.Name.Identifier.ValueText == name,
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText == name,
        _ => false,
    };
}
