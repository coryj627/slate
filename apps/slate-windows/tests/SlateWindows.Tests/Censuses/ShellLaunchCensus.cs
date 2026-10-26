// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SlateWindows.Tests.Censuses;

/// <summary>
/// #1331: every shell-gate launch carries the run's own device-state root.
/// The gate's journeys start the production executable from a
/// <c>ProcessStartInfo</c> built on <c>SlateWindowsExe()</c>; each such start
/// info must pass through <c>ShellLaunch.ApplyDeviceState</c> before anything
/// else takes it (<c>Process.Start</c> or a helper), or that launch writes the
/// developer's <c>%LOCALAPPDATA%\Slate</c>.
/// </summary>
/// <remarks>
/// <para>
/// Every use of <c>SlateWindowsExe()</c> is accounted for: a start info built
/// on it (its constructor argument or its initializer, explicitly or
/// target-typed), or a read of the path that starts nothing
/// (<see cref="PathReads"/>). Anything else — <c>Process.Start(SlateWindowsExe())</c>,
/// a field assigned later — fails, because the census cannot follow it.
/// </para>
/// <para>
/// It lives here, not in the gate's project: the gate also runs from its
/// binaries alone on a runner with no source, and only this project carries
/// Roslyn. It reads the gate's sources from the checkout.
/// </para>
/// </remarks>
public sealed class ShellLaunchCensus
{
    /// <summary>The launches counted on 2026-10-26: 31 in
    /// <c>ShellAccessibilityTests.cs</c> (<c>StartShellProcess</c>'s among
    /// them), 3 in <c>.Rescan.cs</c> and one in each of eight other partials.
    /// Fewer means the walk missed files, not that launches went away.</summary>
    private const int LaunchesAtLeast = 42;

    /// <summary>Calls that only read the executable's path and return
    /// nothing a launch could start (not <c>GetFullPath</c>, whose result is
    /// the executable again).</summary>
    private static readonly HashSet<string> PathReads = new(StringComparer.Ordinal)
    {
        "GetFileName",
        "GetFileNameWithoutExtension",
        "Exists",
    };

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
            foreach (InvocationExpressionSyntax exe in root.DescendantNodes().OfType<InvocationExpressionSyntax>().Where(IsSlateWindowsExe))
            {
                string where = $"{relative}:{exe.GetLocation().GetLineSpan().StartLinePosition.Line + 1}";
                BaseObjectCreationExpressionSyntax? creation = exe.Ancestors()
                    .TakeWhile(node => node is not StatementSyntax and not MemberDeclarationSyntax)
                    .OfType<BaseObjectCreationExpressionSyntax>()
                    .FirstOrDefault(IsStartInfo);
                if (creation is null)
                {
                    if (exe.Parent is ArgumentSyntax { Parent.Parent: InvocationExpressionSyntax reader }
                        && NameOf(reader) is { } read
                        && PathReads.Contains(read))
                    {
                        continue;
                    }
                    failures.Add($"{where}: SlateWindowsExe() is used outside a ProcessStartInfo the census can follow");
                    continue;
                }
                launches++;
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

    private static bool IsSlateWindowsExe(InvocationExpressionSyntax invocation) =>
        invocation.ArgumentList.Arguments.Count == 0 && NameOf(invocation) == "SlateWindowsExe";

    private static string? NameOf(InvocationExpressionSyntax invocation) => invocation.Expression switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
        _ => null,
    };

    private static bool IsStartInfo(BaseObjectCreationExpressionSyntax creation) => creation switch
    {
        ObjectCreationExpressionSyntax { Type: var type } => IsStartInfoType(type),
        ImplicitObjectCreationExpressionSyntax
        {
            Parent: EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax { Type: var declared } } },
        } => IsStartInfoType(declared),
        _ => false,
    };

    private static bool IsStartInfoType(TypeSyntax type) =>
        type is IdentifierNameSyntax { Identifier.ValueText: "ProcessStartInfo" }
            or QualifiedNameSyntax { Right.Identifier.ValueText: "ProcessStartInfo" };

    private static bool IsApplyDeviceState(InvocationExpressionSyntax invocation) =>
        invocation.Expression is MemberAccessExpressionSyntax
        {
            Expression: IdentifierNameSyntax { Identifier.ValueText: "ShellLaunch" },
            Name.Identifier.ValueText: "ApplyDeviceState",
        };
}
