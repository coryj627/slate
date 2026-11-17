// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

// CB-19 (#1783, #1836): every combo box in the shell is listed here with the
// family its rows belong to and the fact that opens it and checks its rows
// in both places UI Automation publishes them; one journey of each family is
// in the shell gate; and no combo box takes a shape the row tracker cannot
// serve (a virtualizing or recycling list, or an items panel of its own). The
// census reads sources only: XAML by XDocument with line info, and the
// shell's code bound by ShellCompilation, so a target-typed new() and a
// derived type are caught. Its proof is the planted cases, each reported
// with its file and line.

using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "combo-boxes")]
public sealed class ComboBoxCensus
{
    private const string Fact1 = nameof(ComboBoxRowPlacesTests.EveryRowOfAnOpenComboBoxSaysItsPlaceInBothTrees);
    private const string GateHelper = "AssertOpenListRowsSayTheirPlace";
    private const string Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private const string Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
    private const string TheTracker = "ComboBoxRowPlaces.cs";

    internal enum Family
    {
        /// <summary>Rows on <c>Slate.ComboRowBase</c>.</summary>
        ComboRowBase,

        /// <summary>Inline <c>ComboBoxItem</c>s, with no row style.</summary>
        Inline,

        /// <summary>Rows styled in code, ending in <c>ClassicRows.Apply</c>.</summary>
        CodeBuilt,

        /// <summary>None of the three: a new family needs a row style the
        /// tracker has been shown to serve, and a gate journey.</summary>
        Unknown,
    }

    /// <summary>One combo box: its key (the AutomationId, else the file and
    /// the name it is given), its family, the data row of fact 1 that opens it
    /// in the shell, and for one combo box of each family the gate journey
    /// that opens it and the AutomationId the journey finds it by.</summary>
    internal sealed record Row(string Site, Family Family, string Hosted, string? Journey = null, string? JourneyFindsItBy = null);

    internal static readonly Row[] Table =
    [
        new("AddPropertyType", Family.ComboRowBase, "AddPropertyType"),
        new("BulkRenameOldKeyType", Family.ComboRowBase, "BulkRenameOldKeyType"),
        new("DashboardEditorQueryPicker", Family.ComboRowBase, "DashboardEditorQueryPicker"),
        new("MainWindow.xaml#View override for", Family.Inline, "View override"),
        new("BuilderCombinator", Family.Inline, "BuilderCombinator", "BasesSurfaces_GridBuilderAndLeaves_AreClean", "BuilderCombinator"),
        new("SettingsTheme", Family.ComboRowBase, "SettingsTheme", "SettingsSheet_NamesFocusAndAxe_AreClean", "SettingsTheme"),
        new("SettingsAccent", Family.ComboRowBase, "SettingsAccent"),
        new("Bases/BaseSurfaceView.cs#_viewPicker", Family.CodeBuilt, "BaseViewPicker"),
        new("Graph/ConnectionsLeafView.cs#_depth", Family.CodeBuilt, "ConnectionsDepth", "GraphConnections_LeafWalkDepthAndReRoot_AreClean", "ConnectionsDepth"),
        new("Graph/GraphInspectorView.cs#ring", Family.CodeBuilt, "GraphInspectorGroupRing"),
        new("Graph/GraphInspectorView.cs#colour", Family.CodeBuilt, "GraphInspectorGroupColour"),
    ];

    /// <summary>One place the shell makes a combo box.</summary>
    internal sealed record Site(string File, int Line, string Key, Family Family);

    /// <summary>What a read of the shell's code finds: its combo box sites,
    /// its types that derive from ComboBox, and its shapes rule 3 refuses.</summary>
    internal sealed record CodeScan(List<Site> Sites, List<string> Derived, List<string> Shapes);

    // ---- Rule 1 ---------------------------------------------------------------

    [Fact]
    public void EveryComboBoxIsListedWithItsFamilyAndTheFactThatOpensIt()
    {
        List<Site> xaml = [.. ShellXaml().SelectMany(document => XamlSites(document.File, document.Document))];
        CodeScan code = ScanShellCode();
        Assert.True(xaml.Count >= 7, $"only {xaml.Count} combo boxes were found in the shell's XAML; the scrape is broken.");
        Assert.True(code.Sites.Count >= 4, $"only {code.Sites.Count} combo boxes were found in the shell's code; the scrape is broken.");

        var problems = new List<string>(code.Derived);
        problems.AddRange(Unlisted(xaml.Concat(code.Sites), Table));
        problems.AddRange(Stale(xaml.Concat(code.Sites), Table));
        problems.AddRange(Uncovered(Table, HostedData(Fact1Source()), GateMethod));

        Assert.True(
            problems.Count == 0,
            "Combo boxes the census does not hold to a family and a fact (CB-19):\n  " + string.Join("\n  ", problems));
    }

    // ---- Rule 2 ---------------------------------------------------------------

    [Fact]
    public void EachFamilyHasAGateJourneyThatChecksItsOpenList()
    {
        var missing = new List<string>();
        foreach (Family family in new[] { Family.ComboRowBase, Family.Inline, Family.CodeBuilt })
        {
            bool covered = Table.Any(row => row.Family == family
                && row.Journey is not null
                && GateMethod(row.Journey) is { } method
                && CallsTheHelperOn(method, row.JourneyFindsItBy!));
            if (!covered)
            {
                missing.Add($"no gate journey opens a {family} combo box's list and calls {GateHelper} on it");
            }
        }

        Assert.True(missing.Count == 0, string.Join("\n", missing));
    }

    // ---- Rule 3 ---------------------------------------------------------------

    [Fact]
    public void NoComboBoxTakesAShapeTheTrackerCannotServe()
    {
        List<string> offenders = [.. XamlShapes(ShellXaml())];
        offenders.AddRange(ScanShellCode().Shapes);
        Assert.True(
            offenders.Count == 0,
            "Combo boxes in a shape the row tracker cannot serve (CB-19):\n  " + string.Join("\n  ", offenders));
    }

    // ---- Rule 4: the scrape bites ---------------------------------------------

    [Fact]
    public void APlantedXamlComboBoxInAnotherFileIsReportedWithItsFileAndLine()
    {
        XDocument planted = Parse(
            "<UserControl xmlns=\"" + Presentation + "\">\n"
            + "  <StackPanel>\n"
            + "    <ComboBox AutomationProperties.AutomationId=\"PlantedPicker\" />\n"
            + "  </StackPanel>\n"
            + "</UserControl>\n");
        List<Site> sites = [.. XamlSites("Planted.xaml", planted)];

        Assert.Equal(["Planted.xaml:3 PlantedPicker"], sites.Select(site => $"{site.File}:{site.Line} {site.Key}"));
        string unlisted = Assert.Single(Unlisted(sites, Table));
        Assert.StartsWith("Planted.xaml:3 ", unlisted, StringComparison.Ordinal);

        // A row whose combo box is gone is stale.
        Row gone = new("PlantedGone", Family.ComboRowBase, "PlantedGone");
        Assert.Contains("'PlantedGone' names no combo box", Assert.Single(Stale(sites, [gone])), StringComparison.Ordinal);
        Assert.Empty(Stale(sites, [new Row("PlantedPicker", Family.Unknown, "PlantedPicker")]));
    }

    [Fact]
    public void PlantedCodeComboBoxesAndASubclassAreReportedWithTheirFileAndLine()
    {
        CodeScan scan = ScanPlanted(
            "using System.Windows;\n"
            + "using System.Windows.Controls;\n"
            + "namespace Planted;\n"
            + "internal sealed class Builder\n"
            + "{\n"
            + "    private readonly ComboBox _made = new ComboBox();\n"
            + "    internal void Build()\n"
            + "    {\n"
            + "        ComboBox targetTyped = new();\n"
            + "        var factory = new FrameworkElementFactory(typeof(ComboBox));\n"
            + "    }\n"
            + "}\n"
            + "internal sealed class OwnCombo : ComboBox\n"
            + "{\n"
            + "}\n");

        Assert.Equal(
            ["Planted.cs:6 Planted.cs#_made", "Planted.cs:9 Planted.cs#targetTyped", "Planted.cs:10 Planted.cs#typeof(ComboBox)@10"],
            scan.Sites.Select(site => $"{site.File}:{site.Line} {site.Key}"));
        Assert.Equal(3, Unlisted(scan.Sites, Table).Count());
        string derived = Assert.Single(scan.Derived);
        Assert.StartsWith("Planted.cs:13 ", derived, StringComparison.Ordinal);
    }

    [Fact]
    public void PlantedRecyclingAndItemsPanelShapesAreRefusedWithTheirFileAndLine()
    {
        XDocument planted = Parse(
            "<UserControl xmlns=\"" + Presentation + "\" xmlns:x=\"" + Xaml + "\">\n"
            + "  <UserControl.Resources>\n"
            + "    <Style x:Key=\"Planted.Picker\" TargetType=\"ComboBox\">\n"
            + "      <Setter Property=\"VirtualizingPanel.IsVirtualizing\" Value=\"True\" />\n"
            + "    </Style>\n"
            + "    <Style TargetType=\"ItemsControl\">\n"
            + "      <Setter Property=\"ItemsPanel\" Value=\"{x:Null}\" />\n"
            + "    </Style>\n"
            + "  </UserControl.Resources>\n"
            + "  <StackPanel>\n"
            + "    <ComboBox VirtualizingPanel.VirtualizationMode=\"Recycling\" />\n"
            + "    <ComboBox VirtualizingStackPanel.VirtualizationMode=\"Recycling\" />\n"
            + "    <ComboBox>\n"
            + "      <ComboBox.ItemsPanel>\n"
            + "        <ItemsPanelTemplate><VirtualizingStackPanel /></ItemsPanelTemplate>\n"
            + "      </ComboBox.ItemsPanel>\n"
            + "    </ComboBox>\n"
            + "    <ComboBox Style=\"{StaticResource Planted.Picker}\" />\n"
            + "    <ListBox VirtualizingStackPanel.VirtualizationMode=\"Recycling\" />\n"
            + "  </StackPanel>\n"
            + "</UserControl>\n");

        Assert.Equal(
            ["Planted.xaml:4", "Planted.xaml:7", "Planted.xaml:11", "Planted.xaml:12", "Planted.xaml:14"],
            XamlShapes([("Planted.xaml", planted)]).Select(LineOf).Distinct());

        CodeScan scan = ScanPlanted(
            "using System.Windows;\n"
            + "using System.Windows.Controls;\n"
            + "namespace Planted;\n"
            + "internal static class Shapes\n"
            + "{\n"
            + "    internal static void Make(ComboBox combo, TreeView tree, ItemsPanelTemplate panel)\n"
            + "    {\n"
            + "        VirtualizingPanel.SetVirtualizationMode(combo, VirtualizationMode.Recycling);\n"
            + "        VirtualizingStackPanel.SetIsVirtualizing(combo, true);\n"
            + "        combo.ItemsPanel = panel;\n"
            + "        _ = new ComboBox { ItemsPanel = panel };\n"
            + "        combo.SetValue(ItemsControl.ItemsPanelProperty, panel);\n"
            + "        _ = new Style(typeof(ComboBox));\n"
            + "        VirtualizingStackPanel.SetIsVirtualizing(tree, true);\n"
            + "    }\n"
            + "}\n");

        Assert.Equal(
            ["Planted.cs:8", "Planted.cs:9", "Planted.cs:10", "Planted.cs:11", "Planted.cs:12", "Planted.cs:13"],
            scan.Shapes.Select(LineOf));
    }

    [Fact]
    public void ARowWhoseFactDoesNotOpenItsComboBoxIsReported()
    {
        // A hosted name missing from fact 1's data.
        HashSet<string> data = HostedData(CSharpSyntaxTree.ParseText(
            "public sealed class ComboBoxRowPlacesTests\n"
            + "{\n"
            + "    [Theory]\n"
            + "    [InlineData(\"SettingsTheme\")]\n"
            + "    public void " + Fact1 + "(string key) { }\n"
            + "}\n"));
        Row theme = Table.Single(row => row.Site == "SettingsTheme");
        Row accent = Table.Single(row => row.Site == "SettingsAccent");
        Assert.Empty(Uncovered([theme], data, GateMethod));
        string missing = Assert.Single(Uncovered([accent], data, GateMethod));
        Assert.Contains("'SettingsAccent'", missing, StringComparison.Ordinal);

        // A gate journey that never calls the helper on its row's combo box.
        MethodDeclarationSyntax journey = Method(
            "void SettingsSheet_NamesFocusAndAxe_AreClean()\n"
            + "{\n"
            + "    AutomationElement combo = WaitForElement(window, \"SettingsTheme\", TimeSpan.FromSeconds(5));\n"
            + "    AutomationElement other = WaitForElement(window, \"SettingsAccent\", TimeSpan.FromSeconds(5));\n"
            + "    AssertOpenListRowsSayTheirPlace(window, other);\n"
            + "}\n");
        Assert.False(CallsTheHelperOn(journey, "SettingsTheme"));
        Assert.True(CallsTheHelperOn(journey, "SettingsAccent"));
        string unchecked_ = Assert.Single(Uncovered([theme], new HashSet<string> { "SettingsTheme" }, _ => journey));
        Assert.Contains("never calls " + GateHelper, unchecked_, StringComparison.Ordinal);
    }

    // ---- reading the XAML -------------------------------------------------------

    private static IEnumerable<(string File, XDocument Document)> ShellXaml()
    {
        string root = SourceText.ShellSourceRoot();
        foreach (string path in Directory.EnumerateFiles(root, "*.xaml", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .OrderBy(path => path, StringComparer.Ordinal))
        {
            yield return (Path.GetRelativePath(root, path).Replace('\\', '/'), XDocument.Load(path, LoadOptions.SetLineInfo));
        }
    }

    private static bool IsBuildOutput(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
        || path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);

    private static XDocument Parse(string text) => XDocument.Parse(text, LoadOptions.SetLineInfo);

    internal static IEnumerable<Site> XamlSites(string file, XDocument document) =>
        document.Descendants()
            .Where(element => element.Name.LocalName == "ComboBox")
            .Select(combo => new Site(file, LineOf(combo), XamlKey(file, combo), XamlFamily(combo)));

    /// <summary>The AutomationId, else the x:Name, else the file and the
    /// accessible name (a bound name by the literal its format starts with).</summary>
    private static string XamlKey(string file, XElement combo)
    {
        if (Attribute(combo, "AutomationProperties.AutomationId") is { Length: > 0 } id)
        {
            return id;
        }

        if (combo.Attribute(XName.Get("Name", Xaml))?.Value is { Length: > 0 } name)
        {
            return $"{Path.GetFileName(file)}#{name}";
        }

        string spoken = Attribute(combo, "AutomationProperties.Name") ?? string.Empty;
        int format = spoken.IndexOf("StringFormat=", StringComparison.Ordinal);
        if (spoken.StartsWith('{') && format >= 0)
        {
            string literal = spoken[(format + "StringFormat=".Length)..];
            int hole = literal.IndexOf('{', StringComparison.Ordinal);
            spoken = (hole >= 0 ? literal[..hole] : literal).Trim(' ', '\'', '}');
        }

        return $"{Path.GetFileName(file)}#{(spoken.Length > 0 ? spoken : "line " + LineOf(combo))}";
    }

    private static Family XamlFamily(XElement combo)
    {
        bool onTheRowBase = combo.Elements()
            .Where(element => element.Name.LocalName == "ComboBox.ItemContainerStyle")
            .Descendants()
            .Any(element => element.Name.LocalName == "Style"
                && (Attribute(element, "BasedOn") ?? string.Empty).Contains("Slate.ComboRowBase", StringComparison.Ordinal));
        if (onTheRowBase)
        {
            return Family.ComboRowBase;
        }

        return combo.Elements().Any(element => element.Name.LocalName == "ComboBoxItem") ? Family.Inline : Family.Unknown;
    }

    /// <summary>Rule 3 in XAML: a combo box, a style it names (and every
    /// style that one is based on), and any style aimed at ComboBox, Selector
    /// or ItemsControl, must not set an items panel or a virtualizing panel's
    /// attached property, by attribute, setter or property element.</summary>
    internal static IEnumerable<string> XamlShapes(IEnumerable<(string File, XDocument Document)> documents)
    {
        (string File, XDocument Document)[] all = [.. documents];
        var keyed = new Dictionary<string, (string File, XElement Style)>(StringComparer.Ordinal);
        foreach ((string file, XDocument document) in all)
        {
            foreach (XElement style in document.Descendants().Where(element => element.Name.LocalName == "Style"))
            {
                if (style.Attribute(XName.Get("Key", Xaml))?.Value is { Length: > 0 } key)
                {
                    keyed[key] = (file, style);
                }
            }
        }

        var offenders = new List<string>();
        var read = new HashSet<XElement>();
        void ReadStyle(string file, XElement style)
        {
            if (!read.Add(style))
            {
                return;
            }

            foreach (XElement setter in style.Descendants().Where(element => element.Name.LocalName == "Setter"))
            {
                if (Attribute(setter, "Property") is { } property && Unservable(property))
                {
                    offenders.Add($"{file}:{LineOf(setter)} a style that reaches a combo box sets {property}: {Why}");
                }
            }

            if (ResourceKey(Attribute(style, "BasedOn")) is { } basedOn && keyed.TryGetValue(basedOn, out (string File, XElement Style) parent))
            {
                ReadStyle(parent.File, parent.Style);
            }
        }

        foreach ((string file, XDocument document) in all)
        {
            foreach (XElement element in document.Descendants())
            {
                if (element.Name.LocalName == "Style"
                    && Attribute(element, "TargetType") is { } target
                    && ReachesAComboBox(target))
                {
                    ReadStyle(file, element);
                }

                if (element.Name.LocalName != "ComboBox")
                {
                    continue;
                }

                foreach (XAttribute attribute in element.Attributes().Where(attribute => Unservable(attribute.Name.LocalName)))
                {
                    offenders.Add($"{file}:{LineOf(element)} a combo box sets {attribute.Name.LocalName}: {Why}");
                }

                foreach (XElement property in element.Elements().Where(child => child.Name.LocalName == "ComboBox.ItemsPanel"))
                {
                    offenders.Add($"{file}:{LineOf(property)} a combo box sets its own ComboBox.ItemsPanel: {Why}");
                }

                foreach (XElement style in element.Elements()
                    .Where(child => child.Name.LocalName == "ComboBox.Style")
                    .Elements()
                    .Where(child => child.Name.LocalName == "Style"))
                {
                    ReadStyle(file, style);
                }

                if (ResourceKey(Attribute(element, "Style")) is { } named && keyed.TryGetValue(named, out (string File, XElement Style) found))
                {
                    ReadStyle(found.File, found.Style);
                }
            }
        }

        return offenders.OrderBy(LineOrder, StringComparer.Ordinal);
    }

    private const string Why =
        "a recycled row cannot be stamped, so its copy under the open list would lose its place again";

    private static bool Unservable(string property) =>
        property.EndsWith("ItemsPanel", StringComparison.Ordinal)
        || property.Contains("VirtualizingPanel.", StringComparison.Ordinal)
        || property.Contains("VirtualizingStackPanel.", StringComparison.Ordinal);

    private static bool ReachesAComboBox(string target)
    {
        string type = target.Replace("{x:Type", string.Empty, StringComparison.Ordinal).Trim(' ', '{', '}');
        return type is "ComboBox" or "Selector" or "ItemsControl" or "Primitives:Selector";
    }

    private static string? ResourceKey(string? reference)
    {
        if (reference is null)
        {
            return null;
        }

        foreach (string kind in new[] { "{StaticResource ", "{DynamicResource " })
        {
            if (reference.StartsWith(kind, StringComparison.Ordinal))
            {
                return reference[kind.Length..].TrimEnd('}').Trim();
            }
        }

        return null;
    }

    private static string? Attribute(XElement element, string localName) =>
        element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == localName)?.Value;

    private static int LineOf(XObject node) => ((IXmlLineInfo)node).LineNumber;

    private static string LineOf(string report) => report[..report.IndexOf(' ', StringComparison.Ordinal)];

    private static string LineOrder(string report)
    {
        string at = LineOf(report);
        int colon = at.LastIndexOf(':');
        return at[..colon] + ":" + int.Parse(at[(colon + 1)..], System.Globalization.CultureInfo.InvariantCulture).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    }

    // ---- reading the code -------------------------------------------------------

    private static readonly Lazy<CodeScan> ShellCode = new(() =>
        Scan(ShellCompilation.Compilation, ShellCompilation.Sources.Select(source => (source.Relative, source.Source.Root.SyntaxTree))));

    private static CodeScan ScanShellCode() => ShellCode.Value;

    private static CodeScan ScanPlanted(string text)
    {
        SyntaxTree tree = CSharpSyntaxTree.ParseText(
            text,
            (CSharpParseOptions)ShellCompilation.Sources[0].Source.Root.SyntaxTree.Options,
            path: "Planted.cs");
        CSharpCompilation compilation = ShellCompilation.Compilation.AddSyntaxTrees(tree);
        Diagnostic[] errors = [.. compilation.GetSemanticModel(tree).GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)];
        Assert.True(errors.Length == 0, "the planted code does not bind:\n" + string.Join("\n", errors.Select(d => d.ToString())));
        return Scan(compilation, [("Planted.cs", tree)]);
    }

    internal static CodeScan Scan(CSharpCompilation compilation, IEnumerable<(string Relative, SyntaxTree Tree)> trees)
    {
        INamedTypeSymbol comboBox = compilation.GetTypeByMetadataName("System.Windows.Controls.ComboBox")
            ?? throw new Xunit.Sdk.XunitException("ComboBox did not bind.");
        INamedTypeSymbol style = compilation.GetTypeByMetadataName("System.Windows.Style")
            ?? throw new Xunit.Sdk.XunitException("Style did not bind.");
        INamedTypeSymbol selector = compilation.GetTypeByMetadataName("System.Windows.Controls.Primitives.Selector")
            ?? throw new Xunit.Sdk.XunitException("Selector did not bind.");
        INamedTypeSymbol itemsControl = compilation.GetTypeByMetadataName("System.Windows.Controls.ItemsControl")
            ?? throw new Xunit.Sdk.XunitException("ItemsControl did not bind.");
        var panels = new[]
        {
            compilation.GetTypeByMetadataName("System.Windows.Controls.VirtualizingPanel"),
            compilation.GetTypeByMetadataName("System.Windows.Controls.VirtualizingStackPanel"),
        };

        bool IsComboBox(ITypeSymbol? type)
        {
            for (ITypeSymbol? at = type; at is not null; at = at.BaseType)
            {
                if (SymbolEqualityComparer.Default.Equals(at, comboBox))
                {
                    return true;
                }
            }

            return false;
        }

        bool IsPanelType(ITypeSymbol? type) => panels.Any(panel => SymbolEqualityComparer.Default.Equals(panel, type));

        static int Line(SyntaxNode node) => node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

        static string LastName(ExpressionSyntax expression) => expression switch
        {
            IdentifierNameSyntax name => name.Identifier.Text,
            MemberAccessExpressionSyntax member => member.Name.Identifier.Text,
            _ => string.Empty,
        };

        var scan = new CodeScan([], [], []);
        foreach ((string relative, SyntaxTree tree) in trees)
        {
            SemanticModel model = compilation.GetSemanticModel(tree);
            string file = relative.Replace('\\', '/');
            bool theTracker = Path.GetFileName(file) == TheTracker;
            foreach (SyntaxNode node in tree.GetRoot().DescendantNodes())
            {
                switch (node)
                {
                    case BaseObjectCreationExpressionSyntax creation:
                        ITypeSymbol? created = model.GetTypeInfo(creation).Type;
                        if (IsComboBox(created))
                        {
                            int line = Line(creation);
                            scan.Sites.Add(new Site(file, line, $"{file}#{NameGiven(creation, line)}", Family.CodeBuilt));
                        }
                        else if (SymbolEqualityComparer.Default.Equals(created, style)
                            && creation.ArgumentList?.Arguments.FirstOrDefault()?.Expression is TypeOfExpressionSyntax target
                            && model.GetTypeInfo(target.Type).Type is { } styled
                            && (IsComboBox(styled) || SymbolEqualityComparer.Default.Equals(styled, selector) || SymbolEqualityComparer.Default.Equals(styled, itemsControl)))
                        {
                            scan.Shapes.Add($"{file}:{Line(creation)} a style built in code for {styled.Name}: the census cannot read what it sets, and {Why}");
                        }

                        break;
                    case TypeOfExpressionSyntax typeOf when !theTracker && IsComboBox(model.GetTypeInfo(typeOf.Type).Type):
                        scan.Sites.Add(new Site(file, Line(typeOf), $"{file}#typeof({typeOf.Type})@{Line(typeOf)}", Family.CodeBuilt));
                        break;
                    case ClassDeclarationSyntax declaration when model.GetDeclaredSymbol(declaration)?.BaseType is { } baseType && IsComboBox(baseType):
                        scan.Derived.Add($"{file}:{Line(declaration)} {declaration.Identifier.Text} derives from ComboBox: a subclass is refused, because the XAML scan matches combo boxes by the element name ComboBox");
                        break;
                    case InvocationExpressionSyntax invocation
                        when LastName(invocation.Expression).StartsWith("Set", StringComparison.Ordinal)
                            && model.GetOperation(invocation) is IInvocationOperation call:
                        int at = Line(invocation);
                        if (IsPanelType(call.TargetMethod.ContainingType)
                            && call.TargetMethod.Name.StartsWith("Set", StringComparison.Ordinal)
                            && call.Arguments.Length >= 1
                            && IsComboBox(Unconverted(call.Arguments[0].Value).Type))
                        {
                            scan.Shapes.Add($"{file}:{at} a combo box is given {call.TargetMethod.ContainingType.Name}.{call.TargetMethod.Name}: {Why}");
                        }
                        else if (call.TargetMethod.Name == "SetValue"
                            && IsComboBox(call.Instance?.Type)
                            && call.Arguments.Length >= 1
                            && Unconverted(call.Arguments[0].Value) is IFieldReferenceOperation field
                            && (field.Field.Name == "ItemsPanelProperty" || IsPanelType(field.Field.ContainingType)))
                        {
                            scan.Shapes.Add($"{file}:{at} a combo box is given {field.Field.ContainingType.Name}.{field.Field.Name}: {Why}");
                        }

                        break;
                    case AssignmentExpressionSyntax assignment
                        when LastName(assignment.Left) == "ItemsPanel"
                            && model.GetOperation(assignment) is ISimpleAssignmentOperation { Target: IPropertyReferenceOperation { Property.Name: "ItemsPanel" } property }
                            && IsComboBox(property.Instance?.Type):
                        scan.Shapes.Add($"{file}:{Line(assignment)} a combo box is given its own ItemsPanel: {Why}");
                        break;
                }
            }
        }

        return scan;
    }

    private static IOperation Unconverted(IOperation operation) =>
        operation is IConversionOperation { IsImplicit: true } conversion ? conversion.Operand : operation;

    /// <summary>The name a code-built combo box is given: the variable it
    /// initialises, or the field or property it is assigned to.</summary>
    private static string NameGiven(SyntaxNode creation, int line) => creation.Parent switch
    {
        EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax declarator } => declarator.Identifier.Text,
        AssignmentExpressionSyntax { Left: IdentifierNameSyntax name } => name.Identifier.Text,
        AssignmentExpressionSyntax { Left: MemberAccessExpressionSyntax member } => member.Name.Identifier.Text,
        _ => "new@" + line,
    };

    // ---- the table against the sites and the facts --------------------------------

    internal static IEnumerable<string> Unlisted(IEnumerable<Site> sites, IReadOnlyList<Row> table)
    {
        Site[] all = [.. sites];
        foreach (Site site in all)
        {
            Row? row = table.FirstOrDefault(candidate => candidate.Site == site.Key);
            if (row is null)
            {
                yield return $"{site.File}:{site.Line} a combo box ({site.Key}) has no row in ComboBoxCensus.Table: "
                    + $"add one with its family and a data row of {Fact1} that opens it";
            }
            else if (row.Family != site.Family)
            {
                yield return $"{site.File}:{site.Line} {site.Key}'s rows are {site.Family}, and the table says {row.Family}";
            }
        }
    }

    internal static IEnumerable<string> Stale(IEnumerable<Site> sites, IReadOnlyList<Row> table)
    {
        Site[] all = [.. sites];
        foreach (Row row in table)
        {
            int found = all.Count(site => site.Key == row.Site);
            if (found == 0)
            {
                yield return $"ComboBoxCensus.Table: the row '{row.Site}' names no combo box in the shell (stale: remove it)";
            }
            else if (found > 1)
            {
                yield return $"ComboBoxCensus.Table: the row '{row.Site}' matches {found} sites; give each its own key";
            }
        }
    }

    internal static IEnumerable<string> Uncovered(
        IEnumerable<Row> rows, IReadOnlySet<string> hostedData, Func<string, MethodDeclarationSyntax?> gateMethod)
    {
        foreach (Row row in rows)
        {
            if (!hostedData.Contains(row.Hosted))
            {
                yield return $"{row.Site}: {Fact1} has no data row '{row.Hosted}', so nothing opens this combo box and checks its rows";
            }

            if (row.Journey is null)
            {
                continue;
            }

            if (gateMethod(row.Journey) is not { } method)
            {
                yield return $"{row.Site}: the gate has no journey named {row.Journey}";
            }
            else if (!CallsTheHelperOn(method, row.JourneyFindsItBy!))
            {
                yield return $"{row.Site}: {row.Journey} never calls {GateHelper} on the combo box it finds as '{row.JourneyFindsItBy}'";
            }
        }
    }

    private static SyntaxTree Fact1Source() => CSharpSyntaxTree.ParseText(
        File.ReadAllText(Path.Combine(SourceText.RepoRoot(), "apps", "slate-windows", "tests", "SlateWindows.Tests", "ComboBoxRowPlacesTests.cs")));

    /// <summary>The keys fact 1's theory is run with: its InlineData, or the
    /// string literals of the member its MemberData names.</summary>
    internal static HashSet<string> HostedData(SyntaxTree source)
    {
        MethodDeclarationSyntax fact = Assert.Single(
            source.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>(),
            method => method.Identifier.Text == Fact1);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (AttributeSyntax attribute in fact.AttributeLists.SelectMany(list => list.Attributes))
        {
            string name = attribute.Name.ToString();
            if (name is "InlineData" or "InlineDataAttribute"
                && attribute.ArgumentList?.Arguments.FirstOrDefault()?.Expression is LiteralExpressionSyntax literal
                && literal.IsKind(SyntaxKind.StringLiteralExpression))
            {
                keys.Add(literal.Token.ValueText);
            }
            else if (name is "MemberData" or "MemberDataAttribute"
                && attribute.ArgumentList?.Arguments.FirstOrDefault()?.Expression is { } member)
            {
                string memberName = member is InvocationExpressionSyntax { ArgumentList.Arguments: [{ Expression: var inner }] }
                    ? inner.ToString().Split('.')[^1]
                    : member.ToString().Trim('"');
                SyntaxNode? declaration = fact.Parent?.ChildNodes().FirstOrDefault(node => node switch
                {
                    PropertyDeclarationSyntax property => property.Identifier.Text == memberName,
                    MethodDeclarationSyntax method => method.Identifier.Text == memberName,
                    FieldDeclarationSyntax field => field.Declaration.Variables.Any(variable => variable.Identifier.Text == memberName),
                    _ => false,
                });
                foreach (LiteralExpressionSyntax value in declaration?.DescendantNodes().OfType<LiteralExpressionSyntax>() ?? [])
                {
                    if (value.IsKind(SyntaxKind.StringLiteralExpression))
                    {
                        keys.Add(value.Token.ValueText);
                    }
                }
            }
        }

        return keys;
    }

    private static readonly Lazy<MethodDeclarationSyntax[]> GateMethods = new(() =>
    {
        string gate = Path.Combine(SourceText.RepoRoot(), "apps", "slate-windows", "tests", "SlateWindows.AccessibilityTests");
        return
        [
            .. Directory.EnumerateFiles(gate, "ShellAccessibilityTests*.cs")
                .SelectMany(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path)
                    .GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()),
        ];
    });

    private static MethodDeclarationSyntax? GateMethod(string name)
    {
        MethodDeclarationSyntax[] found = [.. GateMethods.Value.Where(method => method.Identifier.Text == name)];
        return found.Length == 1 ? found[0] : null;
    }

    /// <summary>Whether <paramref name="method"/> calls the gate's helper on a
    /// combo box it found by <paramref name="automationId"/>: the helper's
    /// second argument holds the literal, or is a local that is declared or
    /// assigned from an expression that holds it.</summary>
    internal static bool CallsTheHelperOn(MethodDeclarationSyntax method, string automationId)
    {
        bool Holds(SyntaxNode? node) =>
            node is not null && node.DescendantNodesAndSelf().OfType<LiteralExpressionSyntax>()
                .Any(literal => literal.IsKind(SyntaxKind.StringLiteralExpression) && literal.Token.ValueText == automationId);

        foreach (InvocationExpressionSyntax invocation in method.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            string called = invocation.Expression switch
            {
                IdentifierNameSyntax name => name.Identifier.Text,
                MemberAccessExpressionSyntax member => member.Name.Identifier.Text,
                _ => string.Empty,
            };
            if (called != GateHelper || invocation.ArgumentList.Arguments.Count < 2)
            {
                continue;
            }

            ExpressionSyntax combo = invocation.ArgumentList.Arguments[1].Expression;
            if (Holds(combo))
            {
                return true;
            }

            if (combo is IdentifierNameSyntax local)
            {
                string name = local.Identifier.Text;
                bool declared = method.DescendantNodes().OfType<VariableDeclaratorSyntax>()
                    .Any(declarator => declarator.Identifier.Text == name && Holds(declarator.Initializer));
                bool assigned = method.DescendantNodes().OfType<AssignmentExpressionSyntax>()
                    .Any(assignment => assignment.Left is IdentifierNameSyntax target && target.Identifier.Text == name && Holds(assignment.Right));
                if (declared || assigned)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static MethodDeclarationSyntax Method(string text) =>
        CSharpSyntaxTree.ParseText("class Planted\n{\n" + text + "}\n").GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
}
