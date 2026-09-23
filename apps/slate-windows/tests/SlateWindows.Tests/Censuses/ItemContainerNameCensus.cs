// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// W7-7 PR 3 (#1246, contract R-4): WPF names an item container with the
// item's ToString() when nothing else names it, and a view model's
// ToString() is its .NET type name ("SlateWindows.FileTreeNodeViewModel")
// or, for a record, a dump of every field ("RecentVault { Path = …,
// LastOpenedMs = 1790112463550 }"). The agent-operated NVDA pass of
// 2026-09-22 heard both on ten surfaces (record F3). DisplayMemberPath
// does NOT help: it templates the visible text, but the container's name
// is still its content's ToString() (measured: a ListBox with
// DisplayMemberPath publishes "Namespace.Type+Row" as every ListItem's
// name).
//
// So every items host in the shell's authored XAML that binds an
// ItemsSource either names its containers — an ItemContainerStyle
// (inline or a keyed resource, BasedOn followed) with a top-level
// AutomationProperties.Name setter — or marks them layout by being a
// LayoutItemsControl (containers out of the control view; the wrapped
// control is the stop) or an AutomationPresentationItemsControl (a
// presentation host that publishes no container peers at all). The
// runtime twin is the name census inside the FlaUI axe helper, which
// every journey runs; the CanvasDocumentTests TG2-9 check was this
// census's first, single-site shape.
//
// The grid substrate names its rows only when the host passes
// rowAutomationName (Grids/AccessibleDataGrid.cs OnLoadingRow): the
// second fact pins every Bind call through the syntax tree, never a
// regex over C#.

using System.Windows.Controls;
using System.Xaml.Schema;
using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "item-container-names")]
public sealed class ItemContainerNameCensus
{
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    private const string ClrNamespacePrefix = "clr-namespace:";

    /// <summary>Hosts whose containers are layout by construction.</summary>
    private static readonly Type[] LayoutHosts =
    [
        typeof(LayoutItemsControl),
        typeof(AutomationPresentationItemsControl),
    ];

    /// <summary>The spec §4.1 sites that live in authored XAML (the grid
    /// callers are C#), by AutomationId, x:Name or accessible name — a
    /// census that stops seeing one of them has stopped looking.</summary>
    private static readonly string[] SpecSites =
    [
        "SidebarFilterResults",
        "Recent vaults",
        "Editor panes",
        "PropertiesRows",
        "TemplateFlowPromptsList",
        "BulkRenameOldKeyType",
    ];

    [Fact]
    public void EveryItemsSourceHostNamesItsContainersOrMarksThemLayout()
    {
        string[] files = ShellViewXaml().ToArray();
        Assert.NotEmpty(files);
        Dictionary<string, List<XElement>> keyedStyles = KeyedStyles();
        var offenders = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        int hosts = 0;
        int layout = 0;
        foreach (string path in files)
        {
            string file = Path.GetFileName(path);
            XDocument document = XDocument.Load(path, LoadOptions.SetLineInfo);
            foreach (XElement element in document.Descendants().Where(HasItemsSource))
            {
                int line = ((IXmlLineInfo)element).LineNumber;
                string site = $"{file}:{line} <{element.Name.LocalName}> {Label(element)}";
                Type? type = ResolveType(element);
                if (type is null)
                {
                    // Fail-closed: a host whose type the census cannot see
                    // is a blind spot indistinguishable from the bug.
                    offenders.Add($"{site}: the census cannot resolve this element's type");
                    continue;
                }
                if (!typeof(ItemsControl).IsAssignableFrom(type))
                {
                    // A HierarchicalDataTemplate's ItemsSource feeds a
                    // TreeViewItem, whose container style comes from its
                    // TreeView (ItemsControl.PrepareItemsControl).
                    continue;
                }

                hosts++;
                _ = seen.Add(Label(element));
                if (LayoutHosts.Any(host => host.IsAssignableFrom(type)))
                {
                    layout++;
                    continue;
                }
                if (!NamesItsContainers(element, keyedStyles))
                {
                    offenders.Add(
                        $"{site}: names no item container (an ItemContainerStyle "
                        + "with an AutomationProperties.Name setter; DisplayMemberPath "
                        + "is not enough) and is not a LayoutItemsControl — WPF will "
                        + "name each container with its item's ToString()");
                }
            }
        }

        Assert.True(hosts > 20, $"the census found only {hosts} items hosts — the scrape is broken");
        Assert.True(layout > 0, "the census found no layout hosts — the type resolution is broken");
        string[] missing = SpecSites.Where(site => !seen.Contains(site)).ToArray();
        Assert.True(
            missing.Length == 0,
            "the census no longer sees these spec §4.1 sites: " + string.Join(", ", missing));
        Assert.True(
            offenders.Count == 0,
            "items hosts whose containers would be named by ToString():\n  "
            + string.Join("\n  ", offenders));
    }

    /// <summary>R-4: every <c>AccessibleDataGrid.Bind</c> call passes
    /// <c>rowAutomationName</c>. Without it the realized DataGridRow's
    /// name is its item's ToString() — "SlateWindows.Bases.BaseGridRowViewModel,
    /// data item" in the Bases grid. Calls are BOUND, not matched by
    /// spelling (three navigators declare a Bind of their own; the
    /// bibliography and bulk-rename grids are x:Name fields), and the
    /// GridConformanceHost fixture must model the rule too. A bind whose
    /// rows are the empty collection literal has no row to name and is
    /// counted apart.</summary>
    [Fact]
    public void EveryGridBindNamesItsRows()
    {
        string hostPath = Path.Combine(
            SourceText.RepoRoot(), "apps", "slate-windows", "tools",
            "GridConformanceHost", "Program.cs");
        CSharpSource host = CSharpSource.LoadPath(hostPath);
        CSharpCompilation compilation = ShellCompilation.Compilation
            .AddSyntaxTrees(host.Root.SyntaxTree);
        IEnumerable<(string File, CSharpSource Source)> sources = ShellCompilation.Sources
            .Select(source => (source.Relative, source.Source))
            .Append(("tools/GridConformanceHost/Program.cs", host));

        var offenders = new List<string>();
        int named = 0;
        int empty = 0;
        foreach ((string file, CSharpSource source) in sources)
        {
            SemanticModel model = compilation.GetSemanticModel(source.Root.SyntaxTree);
            foreach (InvocationExpressionSyntax invocation in source.Root
                .DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Where(IsBindCall))
            {
                int line = invocation.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                SymbolInfo bound = model.GetSymbolInfo(invocation);
                IMethodSymbol? method = bound.Symbol as IMethodSymbol
                    ?? bound.CandidateSymbols.OfType<IMethodSymbol>().FirstOrDefault();
                if (method is null)
                {
                    // Fail-closed: an unbindable Bind could be the grid's.
                    offenders.Add($"{file}:{line}: `{invocation.Expression}` does not bind, so the census cannot tell whose Bind it is");
                    continue;
                }
                if (method.ContainingType.Name != "AccessibleDataGrid")
                {
                    continue;
                }
                if (RowsArgument(invocation) is CollectionExpressionSyntax { Elements.Count: 0 })
                {
                    empty++;
                    continue;
                }
                ArgumentSyntax? name = invocation.ArgumentList.Arguments.FirstOrDefault(
                    argument => argument.NameColon?.Name.Identifier.ValueText == "rowAutomationName");
                if (name is null || name.Expression.IsKind(SyntaxKind.NullLiteralExpression))
                {
                    offenders.Add($"{file}:{line}: `{invocation.Expression}` binds rows without rowAutomationName");
                    continue;
                }
                named++;
            }
        }

        Assert.True(named + offenders.Count >= 10, $"only {named + offenders.Count} row-bearing grid binds found — the scrape is broken");
        Assert.True(empty > 0, "no empty grid binds found — the rows-argument read is broken");
        Assert.True(
            offenders.Count == 0,
            "grid binds whose rows would be named by ToString():\n  "
            + string.Join("\n  ", offenders));
    }

    private static bool IsBindCall(InvocationExpressionSyntax invocation) =>
        invocation.Expression switch
        {
            MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText == "Bind",
            MemberBindingExpressionSyntax binding => binding.Name.Identifier.ValueText == "Bind",
            _ => false,
        };

    /// <summary>The <c>rows</c> argument: named, else the second positional.</summary>
    private static ExpressionSyntax? RowsArgument(InvocationExpressionSyntax invocation)
    {
        SeparatedSyntaxList<ArgumentSyntax> arguments = invocation.ArgumentList.Arguments;
        ArgumentSyntax? named = arguments.FirstOrDefault(
            argument => argument.NameColon?.Name.Identifier.ValueText == "rows");
        if (named is not null)
        {
            return named.Expression;
        }
        return arguments.Count > 1 && arguments[1].NameColon is null ? arguments[1].Expression : null;
    }

    private static bool IsBuildOutput(string path, string root)
    {
        string relative = Path.GetRelativePath(root, path);
        string first = relative.Split(Path.DirectorySeparatorChar)[0];
        return first is "obj" or "bin";
    }

    /// <summary>The shell's view XAML: the FocusableLayoutHostCensus
    /// discovery — theme dictionaries and App.xaml declare styles and
    /// templates, not instances.</summary>
    private static IEnumerable<string> ShellViewXaml() =>
        AllShellXaml()
            .Where(path => !path.Contains(
                $"{Path.DirectorySeparatorChar}Themes{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase))
            .Where(path => !string.Equals(
                Path.GetFileName(path), "App.xaml", StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<string> AllShellXaml() =>
        Directory.EnumerateFiles(
                SourceText.ShellSourceRoot(), "*.xaml", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path, SourceText.ShellSourceRoot()))
            .OrderBy(path => path, StringComparer.Ordinal);

    /// <summary>Every keyed Style in any shell XAML: a StaticResource can
    /// resolve up to App.xaml. A key declared more than once must name its
    /// containers in every declaration.</summary>
    private static Dictionary<string, List<XElement>> KeyedStyles()
    {
        var styles = new Dictionary<string, List<XElement>>(StringComparer.Ordinal);
        foreach (string path in AllShellXaml())
        {
            foreach (XElement style in XDocument.Load(path).Descendants()
                .Where(element => element.Name.LocalName == "Style"))
            {
                if ((string?)style.Attribute(Xaml + "Key") is { } key)
                {
                    if (!styles.TryGetValue(key, out List<XElement>? declared))
                    {
                        declared = [];
                        styles[key] = declared;
                    }
                    declared.Add(style);
                }
            }
        }
        return styles;
    }

    private static bool HasItemsSource(XElement element) =>
        element.Attribute("ItemsSource") is not null
        || element.Elements().Any(child => child.Name.LocalName.EndsWith(".ItemsSource", StringComparison.Ordinal));

    private static bool NamesItsContainers(
        XElement host, Dictionary<string, List<XElement>> keyedStyles)
    {
        XElement? inline = host.Elements()
            .FirstOrDefault(child => child.Name.LocalName.EndsWith(".ItemContainerStyle", StringComparison.Ordinal))
            ?.Elements().FirstOrDefault(child => child.Name.LocalName == "Style");
        if (inline is not null)
        {
            return StyleNames(inline, keyedStyles, depth: 0);
        }
        return ResourceKey((string?)host.Attribute("ItemContainerStyle")) is { } key
            && keyedStyles.TryGetValue(key, out List<XElement>? styles)
            && styles.All(style => StyleNames(style, keyedStyles, depth: 0));
    }

    /// <summary>A top-level Name setter with a value, on the style or the
    /// chain it is BasedOn. A setter inside a trigger names only some
    /// containers, so it does not count.</summary>
    private static bool StyleNames(
        XElement style, Dictionary<string, List<XElement>> keyedStyles, int depth)
    {
        IEnumerable<XElement> setters = style.Elements()
            .SelectMany(child => child.Name.LocalName == "Style.Setters" ? child.Elements() : [child])
            .Where(child => child.Name.LocalName == "Setter");
        if (setters.Any(setter =>
                (string?)setter.Attribute("Property") == "AutomationProperties.Name"
                && (!string.IsNullOrWhiteSpace((string?)setter.Attribute("Value"))
                    || setter.Elements().Any(child => child.Name.LocalName == "Setter.Value"))))
        {
            return true;
        }
        return depth < 8
            && ResourceKey((string?)style.Attribute("BasedOn")) is { } basedOn
            && keyedStyles.TryGetValue(basedOn, out List<XElement>? bases)
            && bases.All(based => StyleNames(based, keyedStyles, depth + 1));
    }

    /// <summary>The key of a <c>{StaticResource K}</c> or
    /// <c>{DynamicResource K}</c> markup extension; null otherwise.</summary>
    private static string? ResourceKey(string? value)
    {
        if (value is null || !value.StartsWith('{') || !value.EndsWith('}'))
        {
            return null;
        }
        string[] parts = value[1..^1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || parts[0] is not ("StaticResource" or "DynamicResource"))
        {
            return null;
        }
        return parts[1].StartsWith("ResourceKey=", StringComparison.Ordinal)
            ? parts[1]["ResourceKey=".Length..]
            : parts[1];
    }

    /// <summary>The element's CLR type: a <c>clr-namespace:</c> element
    /// from the shell assembly (or the one its mapping names); a WPF
    /// element through WPF's own XAML schema context.</summary>
    private static Type? ResolveType(XElement element)
    {
        string xmlNamespace = element.Name.NamespaceName;
        string name = element.Name.LocalName;
        if (xmlNamespace.StartsWith(ClrNamespacePrefix, StringComparison.Ordinal))
        {
            string[] mapping = xmlNamespace[ClrNamespacePrefix.Length..].Split(';');
            string? assembly = mapping.Skip(1)
                .FirstOrDefault(part => part.StartsWith("assembly=", StringComparison.Ordinal))
                ?["assembly=".Length..];
            return assembly is null
                ? typeof(MainWindow).Assembly.GetType($"{mapping[0]}.{name}")
                : Type.GetType($"{mapping[0]}.{name}, {assembly}");
        }
        return System.Windows.Markup.XamlReader.GetWpfSchemaContext()
            .GetXamlType(new XamlTypeName(xmlNamespace, name))?.UnderlyingType;
    }

    /// <summary>How a failure names a host: AutomationId, else x:Name,
    /// else its accessible name, else "(unlabelled)".</summary>
    private static string Label(XElement element) =>
        (string?)element.Attribute("AutomationProperties.AutomationId")
        ?? (string?)element.Attribute(Xaml + "Name")
        ?? (string?)element.Attribute("AutomationProperties.Name")
        ?? "(unlabelled)";
}
