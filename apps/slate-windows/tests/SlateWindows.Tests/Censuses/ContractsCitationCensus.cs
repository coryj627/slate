// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// W6-1 PR A (#745): the contracts document's §A cites no identifier
// that does not exist.
//
// The contracts doc is the evidence ledger PR H reconciles every row
// against, so a citation naming a test that was renamed — or never
// written — is not a typo. It is a row that reads as evidenced and is
// not, and it survives exactly as long as nobody re-reads the whole
// section by hand. PR A's first cut shipped FIVE such names.
//
// Deliberately not a hand-kept list of "the test names §A is allowed to
// use": that is the same artefact one level up, and it rots the same
// way. The rule is mechanical — every long PascalCase citation in §A
// must resolve to SOMETHING declared in the shell or its test projects.

using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "contracts-citations")]
public sealed class ContractsCitationCensus
{
    private const string ContractsDoc = "34_canvas_contracts.md";
    private const string SectionStart = "## PR A — the canvas document, the tab, and the outline";
    private const string SectionEnd = "## §W-G canonical-consumption audit";

    /// <summary>
    /// Long PascalCase names are the ones that read as code. Short ones
    /// (`Ready`, `Invoke`, `Tree`) are ordinary prose in this document
    /// and are deliberately out of scope — the floor is set where a
    /// citation stops being a word and starts being an identifier a
    /// reviewer would try to grep.
    /// </summary>
    private const int IdentifierFloor = 15;

    [Fact]
    public void EveryIdentifierCitedInSectionAExists()
    {
        string section = SectionA();
        HashSet<string> declared = DeclaredNames();

        var missing = new SortedSet<string>(StringComparer.Ordinal);
        foreach (Match match in Regex.Matches(section, @"`([A-Za-z_][A-Za-z0-9_]*)`"))
        {
            string cited = match.Groups[1].Value;
            if (cited.Length >= IdentifierFloor
                && char.IsUpper(cited[0])
                && !declared.Contains(cited))
            {
                _ = missing.Add(cited);
            }
        }

        Assert.True(
            missing.Count == 0,
            "§A of the contracts document cites identifiers that do not exist "
            + "anywhere in the Windows shell or its tests. A contract row citing "
            + "a test that was renamed reads as evidenced and is not — which is "
            + "the failure class PR H's reconciliation depends on catching:\n  "
            + string.Join("\n  ", missing));
    }

    /// <summary>The census's own premise: a §A that stopped citing
    /// anything, or a section marker that moved, would make the check
    /// above pass over nothing.</summary>
    [Fact]
    public void TheSectionIsFoundAndCitesIdentifiers()
    {
        string section = SectionA();
        Assert.True(section.Length > 5_000, "§A is implausibly short — did the marker move?");
        int citations = Regex.Matches(section, @"`([A-Z][A-Za-z0-9_]{14,})`").Count;
        Assert.True(
            citations >= 30,
            $"§A cites only {citations} identifiers; the guard would be scanning "
            + "almost nothing.");
    }

    private static string SectionA()
    {
        string path = Path.Combine(
            SourceText.RepoRoot(), "docs", "plans", ContractsDoc);
        Assert.True(File.Exists(path), $"the contracts document is missing at {path}");
        string text = File.ReadAllText(path);
        int start = text.IndexOf(SectionStart, StringComparison.Ordinal);
        int end = text.IndexOf(SectionEnd, StringComparison.Ordinal);
        Assert.True(start >= 0, $"§A's heading is missing: {SectionStart}");
        Assert.True(end > start, $"§A's terminator is missing: {SectionEnd}");
        return text[start..end];
    }

    /// <summary>Every name DECLARED in the shell and its test projects —
    /// types, members, locals-that-matter. Syntax only: resolving
    /// symbols would need a compilation, and what a citation needs is
    /// that the name is written down somewhere real.</summary>
    private static HashSet<string> DeclaredNames()
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        string[] roots =
        [
            Path.Combine(SourceText.RepoRoot(), "apps", "slate-windows", "src", "SlateWindows"),
            // The generated uniffi bindings ARE a declaration site —
            // every `CanvasA11yEvent` variant §A cites is declared
            // there. Git-ignored, but nothing in this project compiles
            // without them, so a run that got this far has them.
            Path.Combine(SourceText.RepoRoot(), "apps", "slate-windows", "src", "SlateUniffi"),
            Path.Combine(SourceText.RepoRoot(), "apps", "slate-windows", "tests"),
            Path.Combine(SourceText.RepoRoot(), "apps", "slate-windows", "tools"),
            Path.Combine(SourceText.RepoRoot(), "apps", "slate-windows", "benchmarks"),
        ];
        foreach (string root in roots)
        {
            foreach (string file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                        StringComparison.Ordinal)
                    || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                        StringComparison.Ordinal))
                {
                    continue;
                }
                SyntaxNode root2 = CSharpSyntaxTree
                    .ParseText(File.ReadAllText(file), new CSharpParseOptions(LanguageVersion.Preview))
                    .GetRoot();
                foreach (SyntaxNode node in root2.DescendantNodes())
                {
                    switch (node)
                    {
                        case BaseTypeDeclarationSyntax type:
                            _ = names.Add(type.Identifier.ValueText);
                            break;
                        case MethodDeclarationSyntax method:
                            _ = names.Add(method.Identifier.ValueText);
                            break;
                        case PropertyDeclarationSyntax property:
                            _ = names.Add(property.Identifier.ValueText);
                            break;
                        case VariableDeclaratorSyntax variable:
                            _ = names.Add(variable.Identifier.ValueText);
                            break;
                        case EnumMemberDeclarationSyntax member:
                            _ = names.Add(member.Identifier.ValueText);
                            break;
                        case ParameterSyntax parameter:
                            _ = names.Add(parameter.Identifier.ValueText);
                            break;
                        default:
                            break;
                    }
                }
            }
        }
        // Names §A legitimately cites that are declared in a language
        // or assembly this census cannot parse. Each is listed with
        // where it really lives, so the escape hatch stays auditable
        // rather than becoming a place to hide a typo.
        foreach (string external in new[]
        {
            // The mac twins §A compares against.
            "A11yResidueCensusTests", "CanvasAnnouncerTests", "CanvasCardRef",
            "CanvasDocument", "CanvasContainerView", "CanvasOutlineView",
            // WPF / .NET.
            "TreeViewItemAutomationPeer", "VirtualizingStackPanel",
            "IInvokeProvider", "AutomationProperties", "RaiseNotificationEvent",
            "SelectedItemChanged", "VirtualizationMode", "DispatcherTimer",
        })
        {
            _ = names.Add(external);
        }
        return names;
    }
}
