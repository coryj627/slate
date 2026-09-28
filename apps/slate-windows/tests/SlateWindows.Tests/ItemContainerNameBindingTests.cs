// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.ObjectModel;
using System.Dynamic;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Data;
using System.Xml.Linq;
using SlateWindows.Tests.Censuses;

namespace SlateWindows.Tests;

/// <summary>
/// W7-7 PR 3 (#1246, contract R-4; codex PR 3 rounds 1 and 2, the spec
/// review's rounds 21-23): every pinned container style, HOSTED.
/// ItemContainerNameCensus reads what the source says; these facts read
/// what a screen reader reads — the item peer's Name — from the style as
/// authored (the XAML element itself, or the C# factory that builds it),
/// read it again after the item's name changes, and, for a host on the
/// sibling rule, read two namesakes: apart by their distinguisher, else by
/// their ordinal. A style that binds the wrong property, names every
/// container alike, names a container once and never again, or lets two
/// namesakes read alike fails here.
/// </summary>
public sealed class ItemContainerNameBindingTests
{
    private const string FirstName = "First name";
    private const string SecondName = "Second name";

    /// <summary>The code-built hosts, by label, and the factory that
    /// builds each one's container style in production.</summary>
    private static readonly IReadOnlyDictionary<string, (Type Host, Func<Style> Style)> CodeBuilt =
        new Dictionary<string, (Type, Func<Style>)>(StringComparer.Ordinal)
        {
            ["BaseViewPicker"] = (typeof(ComboBox), () => Factory(typeof(Bases.BaseSurfaceView), "ViewPickerItemStyle")),
            ["BaseTabList"] = (typeof(ListBox), () => Factory(typeof(Bases.BaseSurfaceView), "BuildListItemStyle")),
            ["CanvasWarningRows"] = (typeof(ListBox), () => SiblingNames.ContainerStyle(typeof(ListBoxItem))),
            ["{idRoot}Section{index}List"] = (typeof(ListBox), () => SiblingNames.ContainerStyle(typeof(ListBoxItem))),
            ["ConnectionsDepth"] = (typeof(ComboBox), () => ItemContainerNames.BySelf(typeof(ComboBoxItem))),
            ["GraphInspectorGroupRing:"] = (typeof(ComboBox), () => Factory(typeof(Graph.GraphInspectorView), "PickerItemStyle")),
            ["GraphInspectorGroupColour:"] = (typeof(ComboBox), () => Factory(typeof(Graph.GraphInspectorView), "PickerItemStyle")),
            // The trees' styles target their own item types.
            ["CanvasOutlineTree"] = (typeof(Canvas.CanvasOutlineTree), () => Factory(typeof(Canvas.CanvasOutlineView), "RowContainerStyle")),
            ["ConnectionsTree"] = (typeof(Graph.ConnectionsTree), () => Factory(typeof(Graph.ConnectionsLeafView), "RowContainerStyle")),
        };

    /// <summary>A pin as these facts exercise it: the item type, the
    /// property the name reads, a converter's key, and the sibling rule when
    /// the host names through it.</summary>
    private sealed record Pin(Type ItemType, string Path, string? Converter, SiblingRule? Rule);

    private static Pin? PinOf(ContainerNaming naming) => naming switch
    {
        ContainerNaming.Bound bound => new(bound.ItemType, bound.Path, bound.Converter, null),
        ContainerNaming.Sibling sibling => new(sibling.ItemType, sibling.Rule.NamePath, null, sibling.Rule),
        _ => null,
    };

    public static TheoryData<string> XamlNamedHosts()
    {
        var data = new TheoryData<string>();
        foreach ((string label, ContainerNaming naming) in ItemContainerNameCensus.ExpectedNaming)
        {
            if (PinOf(naming) is not null && !CodeBuilt.ContainsKey(label))
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
            .Where(pair => PinOf(pair.Value) is not null)
            .Select(pair => pair.Key)
            .ToArray();
        Assert.True(named.Length > 35, $"only {named.Length} named hosts — the table read is broken");
        Assert.All(CodeBuilt.Keys, label => Assert.NotNull(PinOf(ItemContainerNameCensus.ExpectedNaming[label])));
    }

    [Theory]
    [MemberData(nameof(XamlNamedHosts))]
    public void AnAuthoredContainerStyleReadsItsPinnedNameAndFollowsTheItem(string label) => RunSta(() =>
    {
        Pin pin = PinOf(ItemContainerNameCensus.ExpectedNaming[label])!;
        (ItemsControl host, string _) = AuthoredHost(label);
        // The pinned converter, loaded apart from the style: a style that
        // drops it must read differently, not switch this fact's mode.
        IValueConverter? converter = pin.Converter is null ? null : ShellXamlFragments.LoadConverter(pin.Converter);
        Exercise(label, host, pin, converter);
    });

    [Theory]
    [MemberData(nameof(CodeBuiltNamedHosts))]
    public void ACodeBuiltContainerStyleReadsItsPinnedNameAndFollowsTheItem(string label) => RunSta(() =>
    {
        Pin pin = PinOf(ItemContainerNameCensus.ExpectedNaming[label])!;
        (Type hostType, Func<Style> style) = CodeBuilt[label];
        var host = (ItemsControl)Activator.CreateInstance(hostType, nonPublic: true)!;
        host.ItemContainerStyle = style();
        // The view declares the rule on its host (the census pins that
        // declaration); a fresh host takes the pin's.
        if (pin.Rule is { } rule)
        {
            SiblingNames.SetNamePath(host, rule.NamePath);
            SiblingNames.SetDistinguisherPath(host, rule.DistinguisherPath);
            SiblingNames.SetNoun(host, rule.Noun);
            SiblingNames.SetStatePath(host, rule.StatePath);
            SiblingNames.SetPrefix(host, rule.Prefix);
        }
        Exercise(label, host, pin, converter: null);
    });

    public static TheoryData<string> StatedHosts()
    {
        var data = new TheoryData<string>();
        foreach ((string label, ContainerNaming naming) in ItemContainerNameCensus.ExpectedNaming)
        {
            if (naming is ContainerNaming.Sibling { Rule.StatePath: not null })
            {
                data.Add(label);
            }
        }
        return data;
    }

    /// <summary>Codex PR 3 round 5: a state is the sibling rule's INPUT,
    /// hosted from the authored style (the workspace tab's unsaved and
    /// missing states). Beside an item in a state sits one whose own name is
    /// exactly what the first reads with that state — "draft" unsaved beside
    /// "draft, unsaved changes" — and the pair read apart, the first still
    /// ending in its state; back at rest, each reads its own name. A state
    /// appended after the rule (a trigger's format over the converter, the
    /// shape this replaced) reads the pair alike, and a style that drops the
    /// state never speaks it: both fail here.</summary>
    [Theory]
    [MemberData(nameof(StatedHosts))]
    public void AStateIsTheRulesInputNeverTextAddedAfterIt(string label) => RunSta(() =>
    {
        SiblingRule rule = ((ContainerNaming.Sibling)ItemContainerNameCensus.ExpectedNaming[label]).Rule;
        foreach (string state in new[] { "a state", "a state, and another" })
        {
            (ItemsControl host, _) = AuthoredHost(label);
            IDictionary<string, object?> Item(string name, string place)
            {
                var fields = (IDictionary<string, object?>)new ExpandoObject();
                fields[rule.NamePath] = name;
                if (rule.DistinguisherPath is { } distinguisher)
                {
                    fields[distinguisher] = place;
                }
                fields[rule.StatePath!] = string.Empty;
                return fields;
            }
            string natural = $"draft, {state}";
            IDictionary<string, object?> stated = Item("draft", "A/draft.md");
            var items = new ObservableCollection<object> { stated, Item(natural, $"B/{natural}.md") };
            host.ItemsSource = items;
            Hosted(host, () =>
            {
                void Expect(string when, string[] expected)
                {
                    string[] read = ItemNames(host);
                    Assert.True(
                        expected.SequenceEqual(read),
                        $"{label} {when}: expected [{string.Join(" | ", expected)}], read [{string.Join(" | ", read)}]");
                }

                Expect("at rest", ["draft", natural]);
                stated[rule.StatePath!] = state;
                Expect(
                    $"in \"{state}\"",
                    rule.DistinguisherPath is null
                        ? [$"draft, {rule.Noun} 1, {state}", $"{natural}, {rule.Noun} 2"]
                        : [$"draft, A/draft.md, {state}", $"{natural}, B/{natural}.md"]);
                stated[rule.StatePath!] = string.Empty;
                Expect("back at rest", ["draft", natural]);
            });
        }
    });

    public static TheoryData<string> AuthoredLayoutRuleHosts()
    {
        var codeBuilt = new HashSet<string>(
            ItemContainerNameCensus.CodeItemsHosts.Values.OfType<CodeItemsHost.Pinned>().Select(pinned => pinned.Label),
            StringComparer.Ordinal);
        var data = new TheoryData<string>();
        foreach ((string label, ContainerNaming naming) in ItemContainerNameCensus.ExpectedNaming)
        {
            if (naming is ContainerNaming.Layout { Rule: not null } && !codeBuilt.Contains(label))
            {
                data.Add(label);
            }
        }
        return data;
    }

    /// <summary>The spec review, round 21: a LAYOUT host on the sibling rule
    /// carries each item's name on its structural container, and the one
    /// stop its template holds reads it from there — so the stops of two
    /// namesakes read apart by place, and a loner's stop reads its own name
    /// bare. Hosted from the authored XAML, template and all; a stop that
    /// binds its item directly reads two namesakes alike and fails.</summary>
    [Theory]
    [MemberData(nameof(AuthoredLayoutRuleHosts))]
    public void AnAuthoredLayoutHostsStopsReadTheirItemsApart(string label) => RunSta(() =>
    {
        SiblingRule rule = ((ContainerNaming.Layout)ItemContainerNameCensus.ExpectedNaming[label]).Rule!;
        (string file, XElement element) = ItemContainerNameCensus.XamlHost(label);
        (Grid root, ItemsControl host) = ShellXamlFragments.LoadHost(element, file);
        object Item(string name)
        {
            if (rule.Wrapped)
            {
                return new SiblingText(name);
            }
            var fields = (IDictionary<string, object?>)new ExpandoObject();
            fields[rule.NamePath] = name;
            return fields;
        }
        // Namesakes by the rule's reading; a rule over bare strings has no
        // two EQUAL items (its NoEqualItems reason), so its namesakes differ
        // in case only, as speech ignores it.
        host.ItemsSource = rule.NamePath.Length == 0
            ? new object[] { "Namesake", "NAMESAKE", "Loner" }
            : new[] { Item("Namesake"), Item("Namesake"), Item("Loner") };
        Hosted(root, () =>
        {
            string[] stops = LayoutStops(host);
            string read = string.Join(" | ", stops);
            Assert.True(stops.Length == 3, $"{label}: {stops.Length} stops: {read}");
            Assert.True(
                stops.Distinct(SiblingNames.ReadAlike).Count() == 3,
                $"{label}: stops read alike: {read}");
            // A prefix is the rule's input too (codex PR 3 round 5): every
            // stop reads it, and it comes from the host's declaration.
            Assert.True(
                stops.All(stop => stop.StartsWith(rule.Prefix ?? string.Empty, StringComparison.Ordinal)),
                $"{label}: a stop does not read the prefix \"{rule.Prefix}\": {read}");
            Assert.True(
                stops[0].Contains($", {rule.Noun} 1", StringComparison.Ordinal)
                && stops[1].Contains($", {rule.Noun} 2", StringComparison.Ordinal),
                $"{label}: the namesakes do not read their places: {read}");
            Assert.True(
                stops[2].Contains("Loner", StringComparison.Ordinal)
                && !stops[2].Contains($", {rule.Noun} ", StringComparison.Ordinal),
                $"{label}: the loner does not read its own name bare: {read}");
        });
    });

    /// <summary>Each layout container's stop — the first control-view
    /// element it holds — by name, in order.</summary>
    private static string[] LayoutStops(ItemsControl host)
    {
        host.UpdateLayout();
        PumpedDispatcher.Drain();
        host.UpdateLayout();
        AutomationPeer peer = UIElementAutomationPeer.CreatePeerForElement(host);
        peer.ResetChildrenCache();
        return
        [
            .. (peer.GetChildren() ?? [])
                .OfType<ItemAutomationPeer>()
                .Select(container => FirstStop(container)?.GetName() ?? "(no stop)"),
        ];
    }

    private static AutomationPeer? FirstStop(AutomationPeer peer)
    {
        foreach (AutomationPeer child in peer.GetChildren() ?? [])
        {
            if (child.IsControlElement())
            {
                return child;
            }
            if (FirstStop(child) is { } nested)
            {
                return nested;
            }
        }
        return null;
    }

    /// <summary>An authored host as the app builds it, minus its data: an
    /// instance of its type, its container style as authored, and the
    /// sibling rule as its XAML declares it (not as the census pins it, so
    /// an authored declaration that drifts reads differently here).</summary>
    internal static (ItemsControl Host, string File) AuthoredHost(string label)
    {
        (string file, XElement element) = ItemContainerNameCensus.XamlHost(label);
        XElement authored = ItemContainerNameCensus.ContainerStyle(element, ItemContainerNameCensus.KeyedStyles())
            ?? throw new Xunit.Sdk.XunitException($"{label}: {file} gives it no container style");
        Type hostType = ItemContainerNameCensus.ResolveType(element)
            ?? throw new Xunit.Sdk.XunitException($"{label}: the host type does not resolve");
        var host = (ItemsControl)Activator.CreateInstance(hostType, nonPublic: true)!;
        host.ItemContainerStyle = ShellXamlFragments.LoadStyle(authored, file);
        string? Declared(string property) =>
            (string?)element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == $"SiblingNames.{property}");
        if (Declared("NamePath") is { } namePath)
        {
            SiblingNames.SetNamePath(host, namePath);
            SiblingNames.SetDistinguisherPath(host, Declared("DistinguisherPath"));
            SiblingNames.SetStatePath(host, Declared("StatePath"));
            SiblingNames.SetPrefix(host, Declared("Prefix"));
            if (Declared("Noun") is { } noun)
            {
                SiblingNames.SetNoun(host, noun);
            }
        }
        return (host, file);
    }

    /// <summary>Host <paramref name="host"/> over one item carrying the
    /// pinned property, and read the item peer's Name; change the item's
    /// name and read it again; and, on the sibling rule, add a namesake.</summary>
    private static void Exercise(string label, ItemsControl host, Pin pin, IValueConverter? converter)
    {
        var items = new ObservableCollection<object>();
        Action rename;
        string expectedFirst;
        string expectedSecond;
        if (pin.Path.Length > 0)
        {
            // A mutable item: an ExpandoObject raises PropertyChanged, so a
            // binding follows it and a one-time name does not.
            var fields = (IDictionary<string, object?>)new ExpandoObject();
            fields[pin.Path] = FirstName;
            items.Add(fields);
            expectedFirst = FirstName;
            expectedSecond = SecondName;
            rename = () => fields[pin.Path] = SecondName;
        }
        else if (pin.ItemType.IsEnum)
        {
            // An enum item speaks through its converter: its label, never
            // its member name.
            Assert.True(converter is not null, $"{label}: an enum item needs a pinned converter to speak");
            object[] values = Enum.GetValues(pin.ItemType).Cast<object>().ToArray();
            items.Add(values[0]);
            expectedFirst = (string)converter.Convert(values[0], typeof(string), null!, CultureInfo.InvariantCulture);
            expectedSecond = (string)converter.Convert(values[1], typeof(string), null!, CultureInfo.InvariantCulture);
            Assert.NotEqual(values[0].ToString(), expectedFirst);
            rename = () => items[0] = values[1];
        }
        else
        {
            // A string item names its container by itself.
            Assert.Equal(typeof(string), pin.ItemType);
            items.Add(FirstName);
            expectedFirst = FirstName;
            expectedSecond = SecondName;
            rename = () => items[0] = SecondName;
        }
        host.ItemsSource = items;
        Hosted(host, () =>
        {
            Assert.Equal([expectedFirst], ItemNames(host));
            rename();
            Assert.Equal([expectedSecond], ItemNames(host));

            // The sibling rule over two namesakes. A string item cannot
            // meet its namesake here: WPF gives value-equal items ONE peer.
            if (pin.Rule is not { } rule || pin.Path.Length == 0)
            {
                return;
            }
            var namesake = (IDictionary<string, object?>)new ExpandoObject();
            namesake[pin.Path] = SecondName;
            var first = (IDictionary<string, object?>)items[0];
            if (rule.DistinguisherPath is { } distinguisher)
            {
                first[distinguisher] = "d1";
                namesake[distinguisher] = "d2";
                items.Add(namesake);
                Assert.Equal([$"{SecondName}, d1", $"{SecondName}, d2"], ItemNames(host));
                namesake[distinguisher] = "d1";
            }
            else
            {
                items.Add(namesake);
            }
            // Namesakes that share their distinguisher too read it AND
            // their place: the ordinal never takes a distinguisher back.
            string shared = rule.DistinguisherPath is null ? SecondName : $"{SecondName}, d1";
            Assert.Equal([$"{shared}, {rule.Noun} 1", $"{shared}, {rule.Noun} 2"], ItemNames(host));
            namesake[pin.Path] = FirstName;
            Assert.Equal([SecondName, FirstName], ItemNames(host));
        });
    }

    /// <summary>Every item peer's Name, in order — what UIA reads for the
    /// rows (a combo's are read from its open drop-down).</summary>
    internal static string[] ItemNames(ItemsControl host)
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
        return [.. (peer.GetChildren() ?? []).OfType<ItemAutomationPeer>().Select(item => item.GetName())];
    }

    internal static void Hosted(FrameworkElement content, Action body)
    {
        var window = new Window
        {
            Content = content,
            Width = 480,
            Height = 360,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
        };
        window.Show();
        try
        {
            body();
        }
        finally
        {
            window.Close();
        }
    }

    private static Style Factory(Type owner, string method) =>
        (Style)owner.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, null)!;

    internal static void RunSta(Action body) =>
        StaThread.Run(body, TimeSpan.FromSeconds(60), "STA test body timed out.");
}
