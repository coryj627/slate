// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SlateWindows.Tests.Censuses;

/// <summary>
/// #1331: every shell-gate launch carries the run's own device-state root.
/// The gate's journeys start the production executable from
/// <c>new ProcessStartInfo(SlateWindowsExe())</c>; each such start info must
/// pass through <c>ShellLaunch.ApplyDeviceState</c> before anything else
/// takes it (<c>Process.Start</c> or a helper), or that launch writes the
/// developer's <c>%LOCALAPPDATA%\Slate</c>.
/// </summary>
/// <remarks>
/// It lives here, not in the gate's project: the gate also runs from its
/// binaries alone on a runner with no source, and only this project carries
/// Roslyn. It reads the gate's sources from the checkout.
/// </remarks>
public sealed class ShellLaunchCensus
{
    /// <summary>The launches counted on 2026-10-26: 31 in
    /// <c>ShellAccessibilityTests.cs</c> (<c>StartShellProcess</c>'s among
    /// them), 3 in <c>.Rescan.cs</c> and one in each of eight other partials.
    /// Fewer means the walk missed files, not that launches went away.</summary>
    private const int LaunchesAtLeast = 42;

    [Fact]
    public void EveryShellLaunchAppliesTheDeviceState()
    {
        string suite = Path.Combine(
            SourceText.RepoRoot(), "apps", "slate-windows", "tests", "SlateWindows.AccessibilityTests");
        var launches = 0;
        var failures = new List<string>();
        foreach (string file in Directory.EnumerateFiles(suite, "*.cs", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(suite, file).Replace('\\', '/');
            if (relative.StartsWith("obj/", StringComparison.Ordinal) || relative.StartsWith("bin/", StringComparison.Ordinal))
            {
                continue;
            }
            CompilationUnitSyntax root = CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetCompilationUnitRoot();
            foreach (ObjectCreationExpressionSyntax creation in root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
            {
                if (!IsShellStartInfo(creation))
                {
                    continue;
                }
                launches++;
                string where = $"{relative}:{creation.GetLocation().GetLineSpan().StartLinePosition.Line + 1}";
                if (creation.Parent is not EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax declarator })
                {
                    failures.Add($"{where}: the start info is not held in a local, so nothing can be checked about it");
                    continue;
                }
                string local = declarator.Identifier.ValueText;
                SyntaxNode? body = declarator.Ancestors().FirstOrDefault(node =>
                    node is BaseMethodDeclarationSyntax or LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax);
                if (body is null)
                {
                    failures.Add($"{where}: no enclosing method");
                    continue;
                }
                // Every call that takes the start info, in source order: the
                // first must be ShellLaunch.ApplyDeviceState.
                InvocationExpressionSyntax[] takers = [.. body.DescendantNodes()
                    .OfType<InvocationExpressionSyntax>()
                    .Where(invocation => invocation.SpanStart > declarator.SpanStart
                        && invocation.ArgumentList.Arguments.Any(argument =>
                            argument.Expression is IdentifierNameSyntax { Identifier.ValueText: var name } && name == local))
                    .OrderBy(invocation => invocation.SpanStart)];
                if (takers.Length == 0 || !IsApplyDeviceState(takers[0]))
                {
                    failures.Add(takers.Length == 0
                        ? $"{where}: '{local}' never reaches ShellLaunch.ApplyDeviceState"
                        : $"{where}: '{local}' reaches {takers[0].Expression} before ShellLaunch.ApplyDeviceState");
                }
            }
        }

        Assert.True(
            launches >= LaunchesAtLeast,
            $"the census found {launches} shell launches; the suite held {LaunchesAtLeast} when it was written, so the walk is partial.");
        Assert.True(failures.Count == 0, "shell launches without the run's device-state root (#1331):\n" + string.Join("\n", failures));
    }

    private static bool IsShellStartInfo(ObjectCreationExpressionSyntax creation) =>
        creation.Type is IdentifierNameSyntax { Identifier.ValueText: "ProcessStartInfo" }
            or QualifiedNameSyntax { Right.Identifier.ValueText: "ProcessStartInfo" }
        && creation.ArgumentList is { Arguments: [{ Expression: InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: "SlateWindowsExe" } } }] };

    private static bool IsApplyDeviceState(InvocationExpressionSyntax invocation) =>
        invocation.Expression is MemberAccessExpressionSyntax
        {
            Expression: IdentifierNameSyntax { Identifier.ValueText: "ShellLaunch" },
            Name.Identifier.ValueText: "ApplyDeviceState",
        };
}
