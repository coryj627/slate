// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// W7-7 (#1248, contract R-6): every sheet fences Tab. A focus scope hands
// a command nothing inside it answered to its PARENT scope's focused
// element, so a sheet declared as a focus scope without the fence sends
// Tab from its text fields to whatever held focus before it opened — the
// note editor, in the NVDA pass (record F5). All sixteen overlays in
// MainWindow.xaml were focus scopes and none was fenced. The fence moves
// Tab inside the sheet's own cycle, so a fenced sheet must also cycle.

using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "sheet-fence")]
public sealed class SheetFenceCensus
{
    private const string FocusScope = "FocusManager.IsFocusScope";
    private const string Fence = "SheetKeyboardFence.IsEnabled";
    private const string TabNavigation = "KeyboardNavigation.TabNavigation";

    /// <summary>The CLR namespace the fence is declared in, as XAML maps
    /// it — whatever prefix a file binds to it.</summary>
    private static readonly XNamespace ShellNamespace = "clr-namespace:SlateWindows";

    /// <summary>Every authored XAML file in the shell, themes and App.xaml
    /// included: a focus scope declared in a template or a style hands
    /// commands to its parent scope exactly as an element does.</summary>
    private static IEnumerable<string> ShellXaml() =>
        Directory.EnumerateFiles(SourceText.ShellSourceRoot(), "*.xaml", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.Ordinal);

    [Fact]
    public void EveryFocusScopeInTheShellsXamlIsAFencedCyclingSheet()
    {
        var offenders = new List<string>();
        int scopes = 0;
        int files = 0;
        foreach (string path in ShellXaml())
        {
            files++;
            string file = Path.GetFileName(path);
            XDocument document = XDocument.Load(path, LoadOptions.SetLineInfo);
            foreach (XElement element in document.Descendants())
            {
                if (IsTrue(Attribute(element, FocusScope)))
                {
                    scopes++;
                    var missing = new List<string>();
                    if (!IsTrue(FenceAttribute(element)))
                    {
                        missing.Add($"local:{Fence}=\"True\"");
                    }

                    if (!string.Equals(Attribute(element, TabNavigation), "Cycle", StringComparison.OrdinalIgnoreCase))
                    {
                        missing.Add($"{TabNavigation}=\"Cycle\"");
                    }

                    if (missing.Count > 0)
                    {
                        offenders.Add($"{Describe(file, element)} lacks {string.Join(" and ", missing)}");
                    }
                }
                else if (IsFocusScopeSetter(element))
                {
                    // A style can make a focus scope too; its fence must
                    // ride the same style.
                    scopes++;
                    if (!element.Parent!.Elements().Any(IsFenceSetter))
                    {
                        offenders.Add($"{Describe(file, element)} sets {FocusScope} in a style with no {Fence} setter beside it");
                    }
                }
            }
        }

        Assert.True(files > 0, "the census found no shell XAML — the discovery is broken");
        Assert.True(scopes > 0, "the census found no focus scopes — the scrape is broken");
        Assert.True(
            offenders.Count == 0,
            "focus scopes that do not fence Tab (R-6: Tab from a text field in an unfenced "
            + "sheet reaches the element behind it):\n  " + string.Join("\n  ", offenders));
    }

    /// <summary>A focus scope made in code escapes the XAML scrape above,
    /// so the shell makes none. The two members are found by name — C#
    /// can alias a type but never a member, so <c>using static</c> or an
    /// alias still spells them — in syntax, so a comment or a string is
    /// not a site; a name is cleared only when it BINDS to some other
    /// type's member, so one that fails to bind still counts.</summary>
    [Fact]
    public void TheShellCreatesNoFocusScopeInCode()
    {
        var sites = ShellCompilation.Sources
            .SelectMany(entry => entry.Source.Root.DescendantNodes()
                .OfType<IdentifierNameSyntax>()
                .Where(name => name.Identifier.ValueText is "SetIsFocusScope" or "IsFocusScopeProperty")
                .Where(name => !BindsElsewhere(ShellCompilation.ModelFor(entry.Source).GetSymbolInfo(name)))
                .Select(name => $"{entry.Relative}:{name.GetLocation().GetLineSpan().StartLinePosition.Line + 1} {name.Parent}"))
            .ToArray();

        Assert.True(
            sites.Length == 0,
            "the shell makes a focus scope in code, where SheetFenceCensus cannot see it — "
            + "declare it in XAML with local:SheetKeyboardFence.IsEnabled=\"True\":\n  "
            + string.Join("\n  ", sites));
    }

    private static bool BindsElsewhere(SymbolInfo info)
    {
        ISymbol[] symbols = info.Symbol is { } symbol ? [symbol] : [.. info.CandidateSymbols];
        return symbols.Length > 0
            && symbols.All(candidate => candidate.ContainingType?.ToDisplayString() != "System.Windows.Input.FocusManager");
    }

    private static bool IsTrue(string? value) =>
        string.Equals(value, "True", StringComparison.OrdinalIgnoreCase);

    /// <summary>Attached properties are plain attributes whose local name
    /// carries the dot (<c>FocusManager.IsFocusScope</c>).</summary>
    private static string? Attribute(XElement element, string localName) =>
        element.Attributes()
            .FirstOrDefault(attribute =>
                attribute.Name.Namespace == XNamespace.None
                && string.Equals(attribute.Name.LocalName, localName, StringComparison.Ordinal))
            ?.Value;

    private static string? FenceAttribute(XElement element) =>
        element.Attribute(ShellNamespace + Fence)?.Value;

    private static bool IsFocusScopeSetter(XElement element) =>
        element.Name.LocalName == "Setter"
        && Attribute(element, "Property") == FocusScope
        && IsTrue(Attribute(element, "Value"));

    private static bool IsFenceSetter(XElement element) =>
        element.Name.LocalName == "Setter"
        && Attribute(element, "Property") is { } property
        && property.EndsWith(":" + Fence, StringComparison.Ordinal)
        && IsTrue(Attribute(element, "Value"));

    private static string Describe(string file, XElement element)
    {
        int line = ((IXmlLineInfo)element).LineNumber;
        string? name = element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value
            ?? Attribute(element, "AutomationProperties.AutomationId");
        return name is null
            ? $"{file}:{line} <{element.Name.LocalName}>"
            : $"{file}:{line} <{element.Name.LocalName} {name}>";
    }
}
