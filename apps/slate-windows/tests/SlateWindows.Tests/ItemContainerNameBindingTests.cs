// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.ObjectModel;
using System.Dynamic;
using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Xml.Linq;
using SlateWindows.Tests.Censuses;

namespace SlateWindows.Tests;

/// <summary>
/// W7-7 PR 3 (#1246, contract R-4; codex PR 3 round 1): every pinned
/// container style, HOSTED. ItemContainerNameCensus reads what the source
/// says; these facts read what a screen reader reads — the item peer's
/// Name — from the style as authored (the XAML element itself, or the C#
/// factory that builds it), and read it again after the item's name
/// changes, so a style that binds the wrong property, names every
/// container alike, or names a container once and never again fails here.
/// </summary>
public sealed class ItemContainerNameBindingTests
{
    private const string FirstName = "First name";
    private const string SecondName = "Second name";

    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    /// <summary>The code-built hosts, by label, and the factory that
    /// builds each one's container style in production.</summary>
    private static readonly IReadOnlyDictionary<string, (Type Host, Func<Style> Style)> CodeBuilt =
        new Dictionary<string, (Type, Func<Style>)>(StringComparer.Ordinal)
        {
            ["BaseViewPicker"] = (typeof(ComboBox), () => Factory(typeof(Bases.BaseSurfaceView), "ViewPickerItemStyle")),
            ["BaseTabList"] = (typeof(ListBox), () => Factory(typeof(Bases.BaseSurfaceView), "BuildListItemStyle")),
            ["CanvasWarningRows"] = (typeof(ListBox), () => ItemContainerNames.BySelf(typeof(ListBoxItem))),
            ["ConnectionsDepth"] = (typeof(ComboBox), () => ItemContainerNames.BySelf(typeof(ComboBoxItem))),
            ["GraphInspectorGroupRing:"] = (typeof(ComboBox), () => Factory(typeof(Graph.GraphInspectorView), "PickerItemStyle")),
            ["GraphInspectorGroupColour:"] = (typeof(ComboBox), () => Factory(typeof(Graph.GraphInspectorView), "PickerItemStyle")),
            // The trees' styles target their own item types.
            ["CanvasOutlineTree"] = (typeof(Canvas.CanvasOutlineTree), () => Factory(typeof(Canvas.CanvasOutlineView), "RowContainerStyle")),
            ["ConnectionsTree"] = (typeof(Graph.ConnectionsTree), () => Factory(typeof(Graph.ConnectionsLeafView), "RowContainerStyle")),
        };

    public static TheoryData<string> XamlNamedHosts()
    {
        var data = new TheoryData<string>();
        foreach ((string label, ContainerNaming naming) in ItemContainerNameCensus.ExpectedNaming)
        {
            if (naming is ContainerNaming.Bound && !CodeBuilt.ContainsKey(label))
            {
                data.Add(label);
            }
        }
        return data;
    }

    public static TheoryData<string> CodeBuiltNamedHosts()
    {
        var data = new TheoryData<string>();
        foreach (string label in CodeBuilt.Keys)
        {
            data.Add(label);
        }
        return data;
    }

    /// <summary>Every named host is exercised by one of the two theories —
    /// a host pinned in the census but hosted by neither would go unread.</summary>
    [Fact]
    public void EveryNamedHostIsHosted()
    {
        string[] named = ItemContainerNameCensus.ExpectedNaming
            .Where(pair => pair.Value is ContainerNaming.Bound)
            .Select(pair => pair.Key)
            .ToArray();
        Assert.True(named.Length > 40, $"only {named.Length} named hosts — the table read is broken");
        Assert.All(
            CodeBuilt.Keys,
            label => Assert.IsType<ContainerNaming.Bound>(ItemContainerNameCensus.ExpectedNaming[label]));
    }

    [Theory]
    [MemberData(nameof(XamlNamedHosts))]
    public void AnAuthoredContainerStyleReadsItsPinnedNameAndFollowsTheItem(string label) => RunSta(() =>
    {
        var bound = (ContainerNaming.Bound)ItemContainerNameCensus.ExpectedNaming[label];
        (string file, XElement host) = ItemContainerNameCensus.XamlHost(label);
        XElement authored = ItemContainerNameCensus.ContainerStyle(host, ItemContainerNameCensus.KeyedStyles())
            ?? throw new Xunit.Sdk.XunitException($"{label}: {file} gives it no container style");
        (Style style, ResourceDictionary resources) = LoadStyle(authored, file);
        Type hostType = ItemContainerNameCensus.ResolveType(host)
            ?? throw new Xunit.Sdk.XunitException($"{label}: the host type does not resolve");
        IValueConverter? converter = bound.Converter is null
            ? null
            : (IValueConverter)resources[bound.Converter];
        Exercise(label, hostType, style, bound, converter);
    });

    [Theory]
    [MemberData(nameof(CodeBuiltNamedHosts))]
    public void ACodeBuiltContainerStyleReadsItsPinnedNameAndFollowsTheItem(string label) => RunSta(() =>
    {
        var bound = (ContainerNaming.Bound)ItemContainerNameCensus.ExpectedNaming[label];
        (Type host, Func<Style> style) = CodeBuilt[label];
        Exercise(label, host, style(), bound, converter: null);
    });

    /// <summary>Host <paramref name="style"/> on a <paramref name="hostType"/>
    /// over one item carrying the pinned property, and read the item peer's
    /// Name; then change the item's name and read it again.</summary>
    private static void Exercise(
        string label, Type hostType, Style style, ContainerNaming.Bound bound, IValueConverter? converter)
    {
        var host = (ItemsControl)Activator.CreateInstance(hostType, nonPublic: true)!;
        host.ItemContainerStyle = style;
        var items = new ObservableCollection<object>();
        Action rename;
        string expectedFirst;
        string expectedSecond;
        if (bound.Path.Length > 0)
        {
            // A mutable item: an ExpandoObject raises PropertyChanged, so a
            // binding follows it and a one-time name does not.
            var item = new ExpandoObject();
            var fields = (IDictionary<string, object?>)item;
            fields[bound.Path] = FirstName;
            items.Add(item);
            expectedFirst = FirstName;
            expectedSecond = SecondName;
            rename = () => fields[bound.Path] = SecondName;
        }
        else if (converter is not null)
        {
            // An enum item speaks through its converter: its label, never
            // its member name.
            object[] values = Enum.GetValues(bound.ItemType).Cast<object>().ToArray();
            items.Add(values[0]);
            expectedFirst = (string)converter.Convert(values[0], typeof(string), null!, CultureInfo.InvariantCulture);
            expectedSecond = (string)converter.Convert(values[1], typeof(string), null!, CultureInfo.InvariantCulture);
            Assert.NotEqual(values[0].ToString(), expectedFirst);
            rename = () => items[0] = values[1];
        }
        else
        {
            // A string item names its container by itself.
            items.Add(FirstName);
            expectedFirst = FirstName;
            expectedSecond = SecondName;
            rename = () => items[0] = SecondName;
        }
        host.ItemsSource = items;
        var window = new Window
        {
            Content = host,
            Width = 480,
            Height = 360,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
        };
        window.Show();
        try
        {
            Assert.Equal(expectedFirst, ItemName(host, label));
            rename();
            Assert.Equal(expectedSecond, ItemName(host, label));
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>The first item peer's Name — what UIA reads for the row.</summary>
    private static string ItemName(ItemsControl host, string label)
    {
        if (host is ComboBox combo)
        {
            combo.IsDropDownOpen = true;
        }
        host.UpdateLayout();
        PumpedDispatcher.Drain();
        host.UpdateLayout();
        AutomationPeer peer = UIElementAutomationPeer.CreatePeerForElement(host);
        peer.ResetChildrenCache();
        ItemAutomationPeer item = peer.GetChildren()?.OfType<ItemAutomationPeer>().FirstOrDefault()
            ?? throw new Xunit.Sdk.XunitException($"{label}: the host published no item peer");
        return item.GetName();
    }

    /// <summary>Loads an authored container style as the app would: its
    /// document's namespace prefixes (clr mappings bound to the shell
    /// assembly), any converter it names from WorkspaceTemplates.xaml, and
    /// no event setters (a handler needs its code-behind; naming does not).</summary>
    private static (Style Style, ResourceDictionary Resources) LoadStyle(XElement authored, string file)
    {
        XElement documentRoot = authored.Document?.Root
            ?? throw new InvalidOperationException("the style has no document");
        var root = new XElement(
            Presentation + "ResourceDictionary",
            documentRoot.Attributes()
                .Where(attribute => attribute.IsNamespaceDeclaration)
                .Select(attribute => new XAttribute(attribute.Name, Remap(attribute.Value))));
        var style = new XElement(authored);
        style.Descendants().Where(element => element.Name.LocalName == "EventSetter").Remove();
        foreach (XElement element in style.DescendantsAndSelf())
        {
            element.Name = XNamespace.Get(Remap(element.Name.NamespaceName)) + element.Name.LocalName;
        }
        style.SetAttributeValue(Xaml + "Key", "__pinned");

        XDocument templates = XDocument.Load(Path.Combine(SourceText.ShellSourceRoot(), "WorkspaceTemplates.xaml"));
        foreach (string key in Regex.Matches(style.ToString(), @"\{StaticResource ([\w.]+)\}")
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.Ordinal))
        {
            XElement? declared = templates.Root!.Elements()
                .FirstOrDefault(element => (string?)element.Attribute(Xaml + "Key") == key)
                ?? throw new Xunit.Sdk.XunitException($"{file}: the style uses {key}, which WorkspaceTemplates.xaml does not declare");
            var copy = new XElement(declared);
            copy.Name = XNamespace.Get(Remap(copy.Name.NamespaceName)) + copy.Name.LocalName;
            root.Add(copy);
        }
        root.Add(style);
        // The shell as the LOCAL assembly, as its compiled XAML is: its
        // converters are internal, which a plain XamlReader.Parse refuses.
        using var text = new StringReader(root.ToString());
        using var xml = System.Xml.XmlReader.Create(text);
        using var reader = new System.Xaml.XamlXmlReader(
            xml,
            XamlReader.GetWpfSchemaContext(),
            new System.Xaml.XamlXmlReaderSettings { LocalAssembly = typeof(MainWindow).Assembly });
        var resources = (ResourceDictionary)XamlReader.Load(reader);
        return ((Style)resources["__pinned"], resources);
    }

    private static string Remap(string xmlNamespace) =>
        xmlNamespace.StartsWith("clr-namespace:", StringComparison.Ordinal)
        && !xmlNamespace.Contains(";assembly=", StringComparison.Ordinal)
            ? xmlNamespace + ";assembly=SlateWindows"
            : xmlNamespace;

    private static Style Factory(Type owner, string method) =>
        (Style)owner.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, null)!;

    private static void RunSta(Action body)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                body();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(60)), "STA test body timed out.");
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
