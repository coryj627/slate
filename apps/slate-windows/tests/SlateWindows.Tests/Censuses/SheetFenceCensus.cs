// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// W7-7 (#1248, contract R-6): every sheet fences Tab. A focus scope hands
// a command nothing inside it answered to its PARENT scope's focused
// element, so a sheet declared as a focus scope without the fence sends
// Tab from its text fields to whatever held focus before it opened — the
// note editor, in the NVDA pass (record F5). All sixteen overlays in
// MainWindow.xaml were focus scopes and none was fenced. The fence leaves
// Tab to WPF's own traversal, which stays inside the sheet only when the
// sheet cycles — so a fenced sheet must also cycle, whether an element or
// a style makes it a focus scope.

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
    private const string FenceRequirement = "local:" + Fence + "=\"True\"";
    private const string CycleRequirement = TabNavigation + "=\"Cycle\"";

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
            (List<string> found, int declared) = Offenders(
                XDocument.Load(path, LoadOptions.SetLineInfo), Path.GetFileName(path));
            offenders.AddRange(found);
            scopes += declared;
        }

        Assert.True(files > 0, "the census found no shell XAML — the discovery is broken");
        Assert.True(scopes > 0, "the census found no focus scopes — the scrape is broken");
        Assert.True(
            offenders.Count == 0,
            "focus scopes that do not fence Tab (R-6: Tab from a text field in an unfenced "
            + "sheet reaches the element behind it):\n  " + string.Join("\n  ", offenders));
    }

    /// <summary>The rule over a focus scope a STYLE makes: its setters
    /// must fence and cycle too, and a missing one is named by file and
    /// line (the IsFocusScope setter's).</summary>
    [Theory]
    [InlineData(true, true, null)]
    [InlineData(true, false, CycleRequirement)]
    [InlineData(false, true, FenceRequirement)]
    public void AStyleThatMakesAFocusScopeMustFenceAndCycleToo(bool fences, bool cycles, string? missing)
    {
        string xaml = $"""
            <ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                                xmlns:local="clr-namespace:SlateWindows">
              <Style x:Key="Sheet" TargetType="Border" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                <Setter Property="FocusManager.IsFocusScope" Value="True" />
                {(fences ? "<Setter Property=\"local:SheetKeyboardFence.IsEnabled\" Value=\"True\" />" : "")}
                {(cycles ? "<Setter Property=\"KeyboardNavigation.TabNavigation\" Value=\"Cycle\" />" : "")}
              </Style>
            </ResourceDictionary>
            """;

        AssertTheRule(xaml, "Synthetic.xaml:4 <Setter>", missing);
    }

    /// <summary>The same rule over a focus scope an ELEMENT makes.</summary>
    [Theory]
    [InlineData(true, true, null)]
    [InlineData(true, false, CycleRequirement)]
    [InlineData(false, true, FenceRequirement)]
    public void AnElementThatIsAFocusScopeMustFenceAndCycleToo(bool fences, bool cycles, string? missing)
    {
        string xaml = $"""
            <Grid xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                  xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                  xmlns:local="clr-namespace:SlateWindows">
              <Border x:Name="SyntheticOverlay"
                      FocusManager.IsFocusScope="True"
                      {(fences ? "local:SheetKeyboardFence.IsEnabled=\"True\"" : "")}
                      {(cycles ? "KeyboardNavigation.TabNavigation=\"Cycle\"" : "")} />
            </Grid>
            """;

        AssertTheRule(xaml, "Synthetic.xaml:4 <Border SyntheticOverlay>", missing);
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

    /// <summary>The census's rule over one XAML document: every element
    /// that is a focus scope, and every style setter that makes one,
    /// carries the fence and cycles Tab. Returns the offenders — file,
    /// line, element and what it lacks — and the scopes it examined.</summary>
    private static (List<string> Offenders, int Scopes) Offenders(XDocument document, string file)
    {
        var offenders = new List<string>();
        int scopes = 0;
        foreach (XElement element in document.Descendants())
        {
            var missing = new List<string>();
            if (IsTrue(Attribute(element, FocusScope)))
            {
                scopes++;
                if (!IsTrue(FenceAttribute(element)))
                {
                    missing.Add(FenceRequirement);
                }

                if (!IsCycle(Attribute(element, TabNavigation)))
                {
                    missing.Add(CycleRequirement);
                }
            }
            else if (IsSetter(element, FocusScope, IsTrue))
            {
                // A style makes a focus scope too; its fence and its
                // cycle must ride the same setters.
                scopes++;
                IEnumerable<XElement> siblings = element.Parent!.Elements();
                if (!siblings.Any(sibling => IsSetter(sibling, Fence, IsTrue)))
                {
                    missing.Add(FenceRequirement);
                }

                if (!siblings.Any(sibling => IsSetter(sibling, TabNavigation, IsCycle)))
                {
                    missing.Add(CycleRequirement);
                }
            }

            if (missing.Count > 0)
            {
                offenders.Add($"{Describe(file, element)} lacks {string.Join(" and ", missing)}");
            }
        }

        return (offenders, scopes);
    }

    private static void AssertTheRule(string xaml, string site, string? missing)
    {
        (List<string> offenders, int scopes) = Offenders(
            XDocument.Parse(xaml, LoadOptions.SetLineInfo), "Synthetic.xaml");

        Assert.Equal(1, scopes);
        if (missing is null)
        {
            Assert.Empty(offenders);
            return;
        }

        string offender = Assert.Single(offenders);
        Assert.StartsWith(site + " lacks ", offender, StringComparison.Ordinal);
        Assert.EndsWith(missing, offender, StringComparison.Ordinal);
    }

    private static bool BindsElsewhere(SymbolInfo info)
    {
        ISymbol[] symbols = info.Symbol is { } symbol ? [symbol] : [.. info.CandidateSymbols];
        return symbols.Length > 0
            && symbols.All(candidate => candidate.ContainingType?.ToDisplayString() != "System.Windows.Input.FocusManager");
    }

    private static bool IsTrue(string? value) =>
        string.Equals(value, "True", StringComparison.OrdinalIgnoreCase);

    private static bool IsCycle(string? value) =>
        string.Equals(value, "Cycle", StringComparison.OrdinalIgnoreCase);

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

    /// <summary>A <c>Setter</c> for <paramref name="property"/> — written
    /// bare (<c>FocusManager.IsFocusScope</c>) or behind a namespace prefix
    /// (<c>local:SheetKeyboardFence.IsEnabled</c>) — whose value passes
    /// <paramref name="value"/>.</summary>
    private static bool IsSetter(XElement element, string property, Func<string?, bool> value) =>
        element.Name.LocalName == "Setter"
        && Attribute(element, "Property") is { } written
        && (written == property || written.EndsWith(":" + property, StringComparison.Ordinal))
        && value(Attribute(element, "Value"));

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
