// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Xml.Linq;
using SlateWindows.Tests.Censuses;

namespace SlateWindows.Tests;

/// <summary>
/// W7-7 PR 3 (#1246, contract R-4): loads a fragment of the shell's
/// authored XAML — an items host with its templates, a container style, a
/// converter — as the app would, so a hosted fact reads what the authored
/// markup publishes rather than a copy of it. A fragment keeps its
/// document's namespace prefixes (clr mappings bound to the shell
/// assembly, which is loaded as the LOCAL assembly so its internal types
/// resolve, as in its compiled XAML), takes the WorkspaceTemplates.xaml
/// resources it names (transitively, each after the resources it uses),
/// and loses its event handlers and event setters: a handler needs its
/// code-behind, and neither naming nor stops do.
/// </summary>
internal static class ShellXamlFragments
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    /// <summary>An authored items host, loaded with its templates inside a
    /// Grid that carries the resources they name.</summary>
    internal static (Grid Root, ItemsControl Host) LoadHost(XElement authored, string file)
    {
        XElement root = Root("Grid", authored);
        XElement host = Prepare(authored);
        var resources = new XElement(Presentation + "Grid.Resources");
        foreach (XElement resource in ResourcesFor(host, file, authored))
        {
            resources.Add(resource);
        }
        root.Add(resources, host);
        var grid = (Grid)Load(root);
        return (grid, (ItemsControl)grid.Children[0]);
    }

    /// <summary>An authored container style.</summary>
    internal static Style LoadStyle(XElement authored, string file)
    {
        XElement root = Root("ResourceDictionary", authored);
        XElement style = Prepare(authored);
        style.SetAttributeValue(Xaml + "Key", "__pinned");
        foreach (XElement resource in ResourcesFor(style, file, authored))
        {
            root.Add(resource);
        }
        root.Add(style);
        return (Style)((ResourceDictionary)Load(root))["__pinned"];
    }

    /// <summary>A converter as WorkspaceTemplates.xaml declares it.</summary>
    internal static IValueConverter LoadConverter(string key)
    {
        XElement templates = TemplatesDocument().Root!;
        XElement root = Root("ResourceDictionary", templates);
        root.Add(Prepare(Declared(key, "the pin")));
        return Assert.IsAssignableFrom<IValueConverter>(((ResourceDictionary)Load(root))[key]);
    }

    /// <summary>An empty element carrying the namespace declarations of
    /// <paramref name="fragment"/>'s document.</summary>
    private static XElement Root(string type, XElement fragment)
    {
        XElement documentRoot = fragment.Document?.Root
            ?? throw new InvalidOperationException("the fragment has no document");
        return new XElement(
            Presentation + type,
            documentRoot.Attributes()
                .Where(attribute => attribute.IsNamespaceDeclaration)
                .Select(attribute => new XAttribute(attribute.Name, Remap(attribute.Value))));
    }

    /// <summary>A copy with clr mappings bound to the shell assembly and
    /// no event handlers: an attribute naming an event of its element's
    /// type, or an EventSetter.</summary>
    private static XElement Prepare(XElement authored)
    {
        var copy = new XElement(authored);
        copy.Descendants().Where(element => element.Name.LocalName == "EventSetter").Remove();
        foreach (XElement element in copy.DescendantsAndSelf())
        {
            // A property element ("Style.Triggers") names no type.
            if (!element.Name.LocalName.Contains('.', StringComparison.Ordinal)
                && ItemContainerNameCensus.ResolveType(element) is { } type)
            {
                element.Attributes()
                    .Where(attribute => !attribute.IsNamespaceDeclaration
                        && attribute.Name.Namespace == XNamespace.None
                        && type.GetEvent(attribute.Name.LocalName) is not null)
                    .Remove();
            }
            element.Name = XNamespace.Get(Remap(element.Name.NamespaceName)) + element.Name.LocalName;
        }
        return copy;
    }

    /// <summary>The WorkspaceTemplates.xaml resources
    /// <paramref name="fragment"/> names, each after those it names: a
    /// StaticResource resolves while the markup loads, so a resource must
    /// precede its users.</summary>
    private static List<XElement> ResourcesFor(XElement fragment, string user, XElement authored)
    {
        var ordered = new List<XElement>();
        var placed = new HashSet<string>(StringComparer.Ordinal);
        void Visit(XElement element)
        {
            foreach (string key in Keys(element))
            {
                if (placed.Add(key))
                {
                    XElement resource = Prepare(Declared(key, user, authored));
                    Visit(resource);
                    ordered.Add(resource);
                }
            }
        }
        Visit(fragment);
        return ordered;
    }

    private static IEnumerable<string> Keys(XElement element) =>
        Regex.Matches(element.ToString(), @"\{StaticResource ([\w.]+)\}")
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.Ordinal);

    private static XElement Declared(string key, string user, XElement? authored = null) =>
        ScopedResource(key, authored)
        ?? TemplatesDocument().Root!.Elements()
            .FirstOrDefault(element => (string?)element.Attribute(Xaml + "Key") == key)
        ?? throw new Xunit.Sdk.XunitException(
            $"{user}: {key} is declared neither around the fragment nor in WorkspaceTemplates.xaml");

    /// <summary>A resource an ancestor of the authored fragment declares in
    /// its own dictionary (<c>&lt;Border.Resources&gt;</c>), nearest first —
    /// where a StaticResource looks before the application's.</summary>
    private static XElement? ScopedResource(string key, XElement? authored) =>
        authored?.Ancestors()
            .SelectMany(ancestor => ancestor.Elements()
                .Where(child => child.Name.LocalName.EndsWith(".Resources", StringComparison.Ordinal)))
            .SelectMany(resources => resources.Elements()
                .SelectMany(entry => entry.Name.LocalName == "ResourceDictionary" ? entry.Elements() : [entry]))
            .FirstOrDefault(entry => (string?)entry.Attribute(Xaml + "Key") == key);

    private static XDocument TemplatesDocument() =>
        XDocument.Load(Path.Combine(SourceText.ShellSourceRoot(), "WorkspaceTemplates.xaml"));

    private static object Load(XElement root)
    {
        using var text = new StringReader(root.ToString());
        using var xml = System.Xml.XmlReader.Create(text);
        using var reader = new System.Xaml.XamlXmlReader(
            xml,
            XamlReader.GetWpfSchemaContext(),
            new System.Xaml.XamlXmlReaderSettings { LocalAssembly = typeof(MainWindow).Assembly });
        return XamlReader.Load(reader);
    }

    private static string Remap(string xmlNamespace) =>
        xmlNamespace.StartsWith("clr-namespace:", StringComparison.Ordinal)
        && !xmlNamespace.Contains(";assembly=", StringComparison.Ordinal)
            ? xmlNamespace + ";assembly=SlateWindows"
            : xmlNamespace;
}
