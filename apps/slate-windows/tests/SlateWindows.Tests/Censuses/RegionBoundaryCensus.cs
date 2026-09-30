// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// W7-7 PR 4 (#1247, contract R-5 — "arrows never leave the region"; the
// owner's staged structural close, S1): every focusable element in the
// shell sits under exactly ONE region boundary, and every boundary keeps
// its arrows. An arrow a focused control does not take is judged by WPF's
// directional navigation in the nearest navigation group; with no
// boundary between the control and the window, that group is the window
// itself, and the arrow reached the nearest element in any region: from
// the Connections anchor Right reached the rail and Left the 'Resize right
// pane' splitter, whose next Left resized the pane (codex PR 4 round 6 and
// the completeness sweep's G1, G8, G17, G18). The census reads the XAML:
// each region root declares its DirectionalNavigation; each focusable
// element — resolved by its type's own Focusable default, or declared —
// has exactly one region root above it; a GridSplitter, in no region,
// stays out of the Tab order; and the tab content's template root
// contains its arrows, so Up from the Properties header never selects a
// tab.

using System.Windows;
using System.Windows.Controls;
using System.Xml;
using System.Xml.Linq;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "region-boundaries")]
public sealed class RegionBoundaryCensus
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
    private const string Directional = "KeyboardNavigation.DirectionalNavigation";

    [Fact]
    public void EveryRegionRootKeepsItsArrows()
    {
        XDocument window = Shell();
        IReadOnlyList<(XElement Root, string[] Allowed)> roots = RegionRoots(window);
        Assert.True(roots.Count >= 30, $"only {roots.Count} region roots were found; the scrape is broken.");

        string[] offenders = roots
            .Where(root => !root.Allowed.Contains((string?)root.Root.Attribute(Directional)))
            .Select(root => $"{Describe(root.Root)}: {Directional} is "
                + $"'{(string?)root.Root.Attribute(Directional) ?? "unset"}', not {string.Join(" or ", root.Allowed)}")
            .ToArray();
        Assert.True(
            offenders.Length == 0,
            "Region roots that let an arrow leave (R-5):\n  " + string.Join("\n  ", offenders));
    }

    [Fact]
    public void EveryFocusableSitsUnderExactlyOneRegionBoundary()
    {
        XDocument window = Shell();
        (string[] offenders, int judged) = Unbounded(window);

        Assert.True(judged >= 150, $"only {judged} focusable elements were judged; the scrape is broken.");
        Assert.True(
            offenders.Length == 0,
            "Focusable elements outside exactly one region boundary (R-5: an arrow from one searches the "
            + "whole window):\n  " + string.Join("\n  ", offenders));
    }

    /// <summary>The census's own witness: a focusable outside every region,
    /// a region root that does not contain its arrows, and a splitter in
    /// the Tab order are each caught; a focusable in a region, a declared
    /// non-focusable, a splitter out of the Tab order, a template in the
    /// resources and a context menu (its own popup) are not.</summary>
    [Fact]
    public void ThePlantedEscapesAreCaught()
    {
        XDocument planted = XDocument.Parse(
            """
            <Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
              <Window.Resources>
                <DataTemplate x:Key="Row"><Button Content="In a template" /></DataTemplate>
              </Window.Resources>
              <Grid>
                <Border x:Name="FilesPaneBorder" KeyboardNavigation.DirectionalNavigation="Contained">
                  <StackPanel>
                    <TextBox x:Name="Inside" />
                    <TextBlock Text="Not focusable" />
                    <Button x:Name="Declared" Focusable="False" />
                    <ListBox>
                      <ListBox.ContextMenu><ContextMenu><MenuItem Header="In a popup" /></ContextMenu></ListBox.ContextMenu>
                    </ListBox>
                  </StackPanel>
                </Border>
                <Button x:Name="Outside" />
                <GridSplitter x:Name="TabStopSplitter" />
                <GridSplitter x:Name="QuietSplitter" KeyboardNavigation.IsTabStop="False" />
                <Border x:Name="ContentPaneBorder" />
              </Grid>
            </Window>
            """,
            LoadOptions.SetLineInfo);

        (string[] offenders, _) = Unbounded(planted);
        string[] roots = RegionRoots(planted)
            .Where(root => !root.Allowed.Contains((string?)root.Root.Attribute(Directional)))
            .Select(root => Describe(root.Root))
            .ToArray();

        Assert.Equal(
            [
                "line 17 <Button Outside>: under 0 region roots",
                "line 18 <GridSplitter TabStopSplitter>: a splitter in the Tab order, in no region",
            ],
            offenders);
        Assert.Equal(["line 20 <Border ContentPaneBorder>"], roots);
    }

    /// <summary>The tab content — the Properties header, the editor, a
    /// surface — contains its arrows: the tab strip is a region of its own
    /// directly above, and Up from the header selected a tab (the sweep's
    /// G17).</summary>
    [Fact]
    public void TheTabContentKeepsItsArrows()
    {
        XDocument templates = XDocument.Load(
            Path.Combine(SourceText.ShellSourceRoot(), "WorkspaceTemplates.xaml"), LoadOptions.SetLineInfo);
        XElement template = templates.Descendants()
            .Single(element => (string?)element.Attribute(Xaml + "Key") == "WorkspaceTabContentTemplate");
        XElement root = Assert.Single(template.Elements());
        Assert.Equal("Contained", (string?)root.Attribute(Directional));
    }

    private static XDocument Shell() =>
        XDocument.Load(Path.Combine(SourceText.ShellSourceRoot(), "MainWindow.xaml"), LoadOptions.SetLineInfo);

    /// <summary>The shell's region roots and what each may declare: the
    /// menu bar keeps arrows out altogether (None, MenuBarCensus); every
    /// other root contains them — the Files pane, the editor, each right
    /// pane leaf body (every column-0 child of the leaf host but the docked
    /// placeholder), the rail, the status bar's border, the welcome view,
    /// and every focus-scope overlay (a sheet may cycle).</summary>
    private static IReadOnlyList<(XElement Root, string[] Allowed)> RegionRoots(XDocument window)
    {
        var roots = new List<(XElement, string[])>();
        foreach (XElement element in window.Descendants())
        {
            string? name = (string?)element.Attribute(Xaml + "Name");
            XElement? parent = element.Parent;
            if (name == "MainMenu")
            {
                roots.Add((element, ["None"]));
            }
            else if (name is "FilesPaneBorder" or "ContentPaneBorder" or "RightPaneLeavesList" or "WelcomeRoot")
            {
                roots.Add((element, ["Contained"]));
            }
            else if (parent is not null
                && (string?)parent.Attribute(Xaml + "Name") == "RightPaneLeafHost"
                && !element.Name.LocalName.Contains('.', StringComparison.Ordinal)
                && ((string?)element.Attribute("Grid.Column") ?? "0") == "0"
                && name != "RightPaneDockedPlaceholder")
            {
                roots.Add((element, ["Contained"]));
            }
            else if (element.Elements().Any(child => (string?)child.Attribute(Xaml + "Name") == "ShellStatusBar"))
            {
                roots.Add((element, ["Contained"]));
            }
            else if ((string?)element.Attribute("FocusManager.IsFocusScope") == "True")
            {
                roots.Add((element, ["Contained", "Cycle"]));
            }
        }

        return roots;
    }

    private static (string[] Offenders, int Judged) Unbounded(XDocument window)
    {
        HashSet<XElement> roots = RegionRoots(window).Select(root => root.Root).ToHashSet();
        Dictionary<string, string> namespaces = ClrNamespaces(window);
        var offenders = new List<string>();
        int judged = 0;
        foreach (XElement element in window.Root!.Descendants())
        {
            if (element.Name.LocalName.Contains('.', StringComparison.Ordinal)
                || InDetachedTree(element)
                || Resolve(element, namespaces) is not { } type)
            {
                continue;
            }

            bool focusable = (string?)element.Attribute("Focusable") switch
            {
                "False" => false,
                "True" => true,
                { } bound when bound.StartsWith('{') => true,
                _ => DefaultFocusable(type),
            };

            // A shell control builds its focusables in code (the history,
            // sync, base and dashboard views, the Connections leaf, the
            // grids): where it is hosted is where they are.
            bool hostsFocusables = type.Assembly == typeof(MainWindow).Assembly && typeof(Control).IsAssignableFrom(type);
            if (!(focusable || hostsFocusables) || roots.Contains(element) || element.Descendants().Any(roots.Contains))
            {
                continue;
            }

            judged++;
            if (typeof(GridSplitter).IsAssignableFrom(type))
            {
                if ((string?)element.Attribute("KeyboardNavigation.IsTabStop") != "False")
                {
                    offenders.Add($"{Describe(element)}: a splitter in the Tab order, in no region");
                }

                continue;
            }

            int above = element.Ancestors().Count(roots.Contains);
            if (above != 1)
            {
                offenders.Add($"{Describe(element)}: under {above} region roots");
            }
        }

        return (offenders.ToArray(), judged);
    }

    /// <summary>Elements that are not placed where they are written:
    /// resources and styles (templates applied elsewhere, each inside the
    /// region of its use) and context menus and tool tips, each its own
    /// popup with its own focus world.</summary>
    private static bool InDetachedTree(XElement element) =>
        element.AncestorsAndSelf().Any(ancestor =>
            ancestor.Name.LocalName.EndsWith(".Resources", StringComparison.Ordinal)
            || ancestor.Name.LocalName.EndsWith(".ContextMenu", StringComparison.Ordinal)
            || ancestor.Name.LocalName.EndsWith(".ToolTip", StringComparison.Ordinal)
            || ancestor.Name.LocalName == "Style");

    /// <summary>The CLR namespace each XAML prefix names in this document
    /// (<c>clr-namespace:X</c> or <c>clr-namespace:X;assembly=Y</c>).</summary>
    private static Dictionary<string, string> ClrNamespaces(XDocument window) =>
        window.Root!.Attributes()
            .Where(attribute => attribute.IsNamespaceDeclaration
                && attribute.Value.StartsWith("clr-namespace:", StringComparison.Ordinal))
            .ToDictionary(
                attribute => attribute.Value,
                attribute => attribute.Value["clr-namespace:".Length..].Split(';')[0]);

    /// <summary>The WPF element types the presentation namespace maps,
    /// by name — the controls' namespaces first, as XAML resolves
    /// them.</summary>
    private static readonly Lazy<Dictionary<string, Type>> WpfElements = new(() =>
    {
        string[] order =
        [
            "System.Windows.Controls",
            "System.Windows.Controls.Primitives",
            "System.Windows.Documents",
            "System.Windows.Shapes",
            "System.Windows",
        ];
        var types = new Dictionary<string, Type>(StringComparer.Ordinal);
        foreach (string ns in order)
        {
            foreach (Type type in new[] { typeof(FrameworkElement).Assembly, typeof(UIElement).Assembly }
                .SelectMany(assembly => assembly.GetExportedTypes())
                .Where(type => type.Namespace == ns))
            {
                _ = types.TryAdd(type.Name, type);
            }
        }

        return types;
    });

    /// <summary>The UIElement type an element names, or null for a
    /// non-visual (a binding, a definition, a trigger).</summary>
    private static Type? Resolve(XElement element, Dictionary<string, string> namespaces)
    {
        Type? type = element.Name.Namespace == Presentation
            ? WpfElements.Value.GetValueOrDefault(element.Name.LocalName)
            : namespaces.TryGetValue(element.Name.NamespaceName, out string? clr)
                ? typeof(MainWindow).Assembly.GetType($"{clr}.{element.Name.LocalName}")
                : null;
        return type is not null && typeof(UIElement).IsAssignableFrom(type) ? type : null;
    }

    /// <summary>The type's own Focusable default — its class constructors
    /// run first, since a metadata override is only registered when they
    /// do.</summary>
    private static bool DefaultFocusable(Type type)
    {
        for (Type? current = type; current is not null; current = current.BaseType)
        {
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(current.TypeHandle);
        }

        return (bool)UIElement.FocusableProperty.GetMetadata(type).DefaultValue;
    }

    private static string Describe(XElement element)
    {
        string name = (string?)element.Attribute(Xaml + "Name")
            ?? (string?)element.Attribute("AutomationProperties.AutomationId")
            ?? string.Empty;
        return $"line {((IXmlLineInfo)element).LineNumber} <{element.Name.LocalName}{(name.Length > 0 ? " " + name : "")}>";
    }
}
