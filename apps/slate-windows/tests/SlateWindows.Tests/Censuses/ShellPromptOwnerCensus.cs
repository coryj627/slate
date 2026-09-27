// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// #1275 (contract 28 I11, #1298): a message box never enters WPF's
// thread-modal state; the palette's seal sees it only because it disables
// its OWNER, the shell. CI's Windows session is non-interactive, so no fact
// there can raise a native box (the hosted modal-loop facts run a loop with
// the box's signals instead), and the rule that makes each box the shell's
// is pinned here: every message box the shell raises passes an owner, and
// the prompts that take an owner are handed the shell.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "shell-prompt-owner")]
public sealed class ShellPromptOwnerCensus
{
    /// <summary>The owner-taking prompts the shell installs, each of which
    /// must be handed the shell itself.</summary>
    private static readonly string[] OwnerTakingPrompts =
        ["WorkspaceViewModel.ShowHistoryAlert", "WorkspaceViewModel.AskBasesExportScope"];

    [Fact]
    public void EveryMessageBoxTheShellRaisesIsOwnedByTheShell()
    {
        List<(string Name, CompilationUnitSyntax Root)> shell = Directory
            .EnumerateFiles(SourceText.ShellSourceRoot(), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Select(path => (Path.GetFileName(path), CSharpSource.LoadPath(path).Root))
            .ToList();

        (List<string> offenders, int boxes) = UnownedBoxes(shell);
        Assert.True(boxes >= 10, $"the census found {boxes} message boxes — the scrape is broken");
        Assert.True(
            offenders.Count == 0,
            "message boxes the shell raises without an owner (a box disables only its owner, so the palette's "
            + "seal would not see it — contract 28 I11):\n  " + string.Join("\n  ", offenders));

        foreach (string prompt in OwnerTakingPrompts)
        {
            InvocationExpressionSyntax[] calls = shell
                .Where(unit => unit.Name.StartsWith("MainWindow", StringComparison.Ordinal))
                .SelectMany(unit => unit.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
                .Where(invocation => CSharpSource.Normalize(invocation.Expression) == prompt)
                .ToArray();
            Assert.True(calls.Length > 0, $"the shell no longer installs {prompt}");
            Assert.All(calls, call => Assert.Equal("this", CSharpSource.Normalize(call.ArgumentList.Arguments[0].Expression)));
        }
    }

    /// <summary>The detector catches an ownerless box and accepts the
    /// ownerless arm of an <c>owner is null</c> guard, which only a caller
    /// without a shell reaches.</summary>
    [Theory]
    [InlineData("class C { void M() { MessageBox.Show(\"text\"); } }", 1)]
    [InlineData("class C { void M() { System.Windows.MessageBox.Show(message, title); } }", 1)]
    [InlineData("class C { void M() { MessageBox.Show(this, \"text\"); } }", 0)]
    [InlineData("class C { void M(Window? owner) { _ = owner is null ? MessageBox.Show(\"t\") : MessageBox.Show(owner, \"t\"); } }", 0)]
    [InlineData("class C { void M(Window? owner) { if (owner is null) { MessageBox.Show(\"t\"); return; } MessageBox.Show(owner, \"t\"); } }", 0)]
    public void TheDetectorTellsAnOwnedBoxFromAnOwnerlessOne(string source, int unowned)
    {
        var root = (CompilationUnitSyntax)CSharpSyntaxTree.ParseText(source).GetRoot();
        Assert.Equal(unowned, UnownedBoxes([("C.cs", root)]).Offenders.Count);
    }

    private static (List<string> Offenders, int Boxes) UnownedBoxes(IEnumerable<(string Name, CompilationUnitSyntax Root)> shell)
    {
        var offenders = new List<string>();
        int boxes = 0;
        foreach ((string name, CompilationUnitSyntax root) in shell)
        {
            foreach (InvocationExpressionSyntax box in root.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Where(invocation => CSharpSource.Normalize(invocation.Expression) is "MessageBox.Show" or "System.Windows.MessageBox.Show"))
            {
                boxes++;
                string first = box.ArgumentList.Arguments.Count > 0
                    ? CSharpSource.Normalize(box.ArgumentList.Arguments[0].Expression)
                    : string.Empty;
                if (first is "this" or "owner" || OnTheOwnerlessArm(box))
                {
                    continue;
                }

                offenders.Add($"{name}:{box.GetLocation().GetLineSpan().StartLinePosition.Line + 1} MessageBox.Show({first}, …)");
            }
        }

        return (offenders, boxes);
    }

    /// <summary>Inside the true arm of an <c>owner is null</c> conditional
    /// or <c>if</c>.</summary>
    private static bool OnTheOwnerlessArm(SyntaxNode box) =>
        box.Ancestors().Any(ancestor => ancestor switch
        {
            ConditionalExpressionSyntax conditional =>
                CSharpSource.Normalize(conditional.Condition) == "ownerisnull"
                && conditional.WhenTrue.DescendantNodesAndSelf().Contains(box),
            IfStatementSyntax guard =>
                CSharpSource.Normalize(guard.Condition) == "ownerisnull"
                && guard.Statement.DescendantNodesAndSelf().Contains(box),
            _ => false,
        });
}
