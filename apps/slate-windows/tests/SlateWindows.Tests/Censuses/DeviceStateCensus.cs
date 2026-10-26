// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SlateWindows.Tests.Censuses;

/// <summary>
/// #1331: the suites keep their device state off the developer's profile.
/// Every store a test builds names its own file, every recents owner its own
/// root, and both CI test steps run under the whole-run guard — the gate's
/// from the copy its binaries-only artifact carries.
/// </summary>
public sealed class DeviceStateCensus
{
    /// <summary>The recents owners and the position of their device-state
    /// parameter: a named <c>localAppDataRoot:</c> or that many leading
    /// positional arguments pass it.</summary>
    private static readonly Dictionary<string, int> RecentsOwners = new(StringComparer.Ordinal)
    {
        ["FilesSidebarViewModel"] = 8,
        ["QuickSwitcherViewModel"] = 5,
    };

    /// <summary>The stores whose first argument is their file.</summary>
    private static readonly HashSet<string> Stores = new(StringComparer.Ordinal)
    {
        "RecentVaultsStore",
        "WindowStateStore",
        "AppPreferencesStore",
        "CommandPaletteRecentsStore",
    };

    /// <summary>The constructions counted on 2026-10-26. Fewer means the walk
    /// missed files.</summary>
    private const int ConstructionsAtLeast = 90;

    [Fact]
    public void EveryTestConstructionOfARecentsOwnerPassesItsRoot()
    {
        string tests = Path.Combine(SourceText.RepoRoot(), "apps", "slate-windows", "tests");
        var found = 0;
        var failures = new List<string>();
        foreach (string file in Directory.EnumerateFiles(tests, "*.cs", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(tests, file).Replace('\\', '/');
            if (relative.Contains("/obj/", StringComparison.Ordinal) || relative.Contains("/bin/", StringComparison.Ordinal))
            {
                continue;
            }
            CompilationUnitSyntax root = CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetCompilationUnitRoot();
            foreach (BaseObjectCreationExpressionSyntax creation in root.DescendantNodes().OfType<BaseObjectCreationExpressionSyntax>())
            {
                string? type = CreatedTypeName(creation);
                if (type is null || (!RecentsOwners.ContainsKey(type) && !Stores.Contains(type)))
                {
                    continue;
                }
                found++;
                SeparatedSyntaxList<ArgumentSyntax> arguments = creation.ArgumentList?.Arguments ?? default;
                bool passes = RecentsOwners.TryGetValue(type, out int position)
                    ? arguments.Any(argument => argument.NameColon?.Name.Identifier.ValueText == "localAppDataRoot" && !IsNothing(argument.Expression))
                        || (arguments.Count >= position
                            && arguments.Take(position).All(argument => argument.NameColon is null)
                            && !IsNothing(arguments[position - 1].Expression))
                    : arguments.Count > 0 && !IsNothing(arguments[0].Expression);
                if (!passes)
                {
                    failures.Add($"{relative}:{creation.GetLocation().GetLineSpan().StartLinePosition.Line + 1}: new {type}(…) takes the device-state default");
                }
            }
        }

        Assert.True(
            found >= ConstructionsAtLeast,
            $"the census found {found} constructions; the suites held {ConstructionsAtLeast} or more when it was written, so the walk is partial.");
        Assert.True(failures.Count == 0, "test constructions that write the default device state (#1331):\n" + string.Join("\n", failures));
    }

    [Fact]
    public void TheGateRunsUnderTheGuardItsArtifactCarries()
    {
        string repo = SourceText.RepoRoot();
        string gateProject = Path.Combine(repo, "apps", "slate-windows", "tests", "SlateWindows.AccessibilityTests");

        // The gate's project copies the guard into its output, which the
        // binaries-only artifact carries to the gate runner.
        XDocument project = XDocument.Load(Path.Combine(gateProject, "SlateWindows.AccessibilityTests.csproj"));
        XNamespace ns = project.Root?.Name.Namespace ?? XNamespace.None;
        XElement content = Assert.Single(
            project.Descendants(ns + "Content"),
            element => ((string?)element.Attribute("Include"))?.Replace('/', '\\')
                .EndsWith(@"scripts\windows\guard-device-state.ps1", StringComparison.Ordinal) == true);
        Assert.Equal(@"tools\guard-device-state.ps1", ((string?)content.Attribute("Link"))?.Replace('/', '\\'));
        Assert.Equal("PreserveNewest", (string?)content.Attribute("CopyToOutputDirectory"));

        // This build's own output segment (bin\<Configuration>\<framework>),
        // applied to the gate's project: the copy there is the script, byte for byte.
        string[] segments = Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory).Split(Path.DirectorySeparatorChar);
        int bin = Array.LastIndexOf(segments, "bin");
        Assert.True(bin >= 0 && bin + 2 < segments.Length, $"no bin\\<configuration>\\<framework> in {AppContext.BaseDirectory}");
        string carried = Path.Combine(gateProject, segments[bin], segments[bin + 1], segments[bin + 2], "tools", "guard-device-state.ps1");
        Assert.True(File.Exists(carried), $"the gate's output does not carry {carried}");
        Assert.Equal(
            File.ReadAllBytes(Path.Combine(repo, "scripts", "windows", "guard-device-state.ps1")),
            File.ReadAllBytes(carried));

        // Both CI test steps run under the guard: the unit step from the
        // checkout, the gate step from the artifact's copy (that job has no
        // checkout).
        string workflow = File.ReadAllText(Path.Combine(repo, ".github", "workflows", "windows.yml"));
        Assert.Contains(
            "pwsh -NoProfile -File scripts/windows/guard-device-state.ps1 -- dotnet test apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj",
            Step(workflow, "windows", "Test"),
            StringComparison.Ordinal);
        Assert.Contains(
            "pwsh -NoProfile -File apps/slate-windows/tests/SlateWindows.AccessibilityTests/bin/Release/net10.0-windows/tools/guard-device-state.ps1"
            + " -- dotnet test apps/slate-windows/tests/SlateWindows.AccessibilityTests/bin/Release/net10.0-windows/SlateWindows.AccessibilityTests.dll",
            Step(workflow, "flaui", "Shell accessibility gate (FlaUI + axe-windows)"),
            StringComparison.Ordinal);
        Assert.DoesNotContain("actions/checkout", Job(workflow, "flaui"), StringComparison.Ordinal);
    }

    /// <summary>One top-level job's text, from its key to the next job's.</summary>
    private static string Job(string workflow, string job)
    {
        string[] lines = workflow.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        int start = Array.FindIndex(lines, line => line == $"  {job}:");
        Assert.True(start >= 0, $"windows.yml has no job '{job}'");
        int end = Array.FindIndex(lines, start + 1, line => Regex.IsMatch(line, "^  [A-Za-z0-9_-]+:\\s*$"));
        return string.Join("\n", lines[start..(end < 0 ? lines.Length : end)]);
    }

    /// <summary>One step of a job, its white space collapsed, so a folded
    /// <c>run: &gt;-</c> command reads as the one line the shell gets.</summary>
    private static string Step(string workflow, string job, string name)
    {
        string[] steps = Job(workflow, job).Split("\n      - ");
        string step = Assert.Single(steps, candidate => candidate.StartsWith($"name: {name}\n", StringComparison.Ordinal));
        return Regex.Replace(step, "\\s+", " ");
    }

    private static string? CreatedTypeName(BaseObjectCreationExpressionSyntax creation) => creation switch
    {
        ObjectCreationExpressionSyntax { Type: var type } => Simple(type),
        ImplicitObjectCreationExpressionSyntax implicitCreation => implicitCreation.Parent switch
        {
            EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax { Type: var declared } } } => Simple(declared),
            EqualsValueClauseSyntax { Parent: PropertyDeclarationSyntax { Type: var property } } => Simple(property),
            _ => null,
        },
        _ => null,
    };

    private static string? Simple(TypeSyntax type) => type switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
        NullableTypeSyntax nullable => Simple(nullable.ElementType),
        _ => null,
    };

    /// <summary>An argument that can be null: a <c>null</c> or <c>default</c>
    /// literal, seen through parentheses, casts and <c>!</c>, or a conditional
    /// either of whose branches is one.</summary>
    private static bool IsNothing(ExpressionSyntax expression) => expression switch
    {
        LiteralExpressionSyntax literal =>
            literal.IsKind(SyntaxKind.NullLiteralExpression) || literal.IsKind(SyntaxKind.DefaultLiteralExpression),
        DefaultExpressionSyntax => true,
        ParenthesizedExpressionSyntax parenthesized => IsNothing(parenthesized.Expression),
        CastExpressionSyntax cast => IsNothing(cast.Expression),
        PostfixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.SuppressNullableWarningExpression } suppressed => IsNothing(suppressed.Operand),
        ConditionalExpressionSyntax conditional => IsNothing(conditional.WhenTrue) || IsNothing(conditional.WhenFalse),
        _ => false,
    };
}
