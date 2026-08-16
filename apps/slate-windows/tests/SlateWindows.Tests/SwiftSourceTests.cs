// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace SlateWindows.Tests;

/// <summary>
/// #1108: the mac parity gates read Swift with no parser available, so
/// the comment stripper standing in for one is pinned in both directions.
/// </summary>
/// <remarks>
/// Too little stripping restores the dead-source bypass. Too much deletes
/// a live declaration and reports drift that is not there. Both are
/// failures, and only one of them is loud.
/// </remarks>
public sealed class SwiftSourceTests
{
    [Fact]
    public void ACommentedOutDeclarationIsRemoved()
    {
        const string source = """
            enum SlateCommandID {
                // static let ghost = "slate.ghost"
                static let live = "slate.live"
            }
            """;

        string stripped = SwiftSource.WithoutComments(source);
        Assert.DoesNotContain("slate.ghost", stripped, System.StringComparison.Ordinal);
        Assert.Contains("slate.live", stripped, System.StringComparison.Ordinal);
    }

    [Fact]
    public void BlockCommentsNestTheWaySwiftSaysTheyDo()
    {
        // C#'s rules would end this comment at the FIRST */, leaving the
        // trailing text as live code and re-declaring the dead id.
        const string source = """
            /* outer /* inner */ static let ghost = "slate.ghost" */
            static let live = "slate.live"
            """;

        string stripped = SwiftSource.WithoutComments(source);
        Assert.DoesNotContain("slate.ghost", stripped, System.StringComparison.Ordinal);
        Assert.Contains("slate.live", stripped, System.StringComparison.Ordinal);
    }

    [Fact]
    public void SlashesInsideLiteralsDoNotStartAComment()
    {
        // Over-stripping is the quiet failure: it deletes live source and
        // surfaces as parity drift that does not exist.
        const string source = """
            static let docs = "https://example.invalid/x"
            static let live = "slate.live"
            """;

        string stripped = SwiftSource.WithoutComments(source);
        Assert.Contains("slate.live", stripped, System.StringComparison.Ordinal);
        Assert.Contains("https://example.invalid/x", stripped, System.StringComparison.Ordinal);
    }

    [Fact]
    public void RawAndMultilineLiteralsSurviveIntact()
    {
        const string source = """"
            static let raw = #"a \ b // not a comment"#
            static let live = "slate.live"
            """";

        string stripped = SwiftSource.WithoutComments(source);
        Assert.Contains("not a comment", stripped, System.StringComparison.Ordinal);
        Assert.Contains("slate.live", stripped, System.StringComparison.Ordinal);
    }

    [Fact]
    public void AnEscapedQuoteDoesNotEndTheLiteralEarly()
    {
        const string source = """
            static let label = "he said \"go\" // still a string"
            static let live = "slate.live"
            """;

        string stripped = SwiftSource.WithoutComments(source);
        Assert.Contains("still a string", stripped, System.StringComparison.Ordinal);
        Assert.Contains("slate.live", stripped, System.StringComparison.Ordinal);
    }

    [Fact]
    public void TheRealMacCatalogSurvivesStripping()
    {
        // The end-to-end guard on over-stripping: whatever the rules do,
        // they must not eat mac's actual declarations.
        string commands = File.ReadAllText(MacCatalogParityTests.MacCommandsPath());
        string stripped = SwiftSource.WithoutComments(commands);

        Assert.Contains("slate.vault.open", stripped, System.StringComparison.Ordinal);
        Assert.Contains("static let", stripped, System.StringComparison.Ordinal);
        Assert.True(
            stripped.Length > commands.Length / 2,
            $"stripping removed {commands.Length - stripped.Length} of "
            + $"{commands.Length} characters, which is far more than this "
            + "file's comments — the rules are eating live source.");
    }
}
