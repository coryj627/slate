// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.ObjectModel;
using System.Dynamic;
using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
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
    /// property the name reads, a converter's key, the sibling rule when
    /// the host names through it, and the pinned trigger states.</summary>
    private sealed record Pin(
        Type ItemType,
        string Path,
        string? Converter,
        SiblingRule? Rule,
        IReadOnlyList<TriggerNaming> Triggers);

    private static Pin? PinOf(ContainerNaming naming) => naming switch
    {
        ContainerNaming.Bound bound => new(bound.ItemType, bound.Path, bound.Converter, null, bound.Triggers),
        ContainerNaming.Sibling sibling => new(
            sibling.ItemType, sibling.Rule.NamePath, null, sibling.Rule, sibling.Triggers),
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
        }
        Exercise(label, host, pin, converter: null);
    });

    public static TheoryData<string> TriggeredHosts()
    {
        var data = new TheoryData<string>();
        foreach ((string label, ContainerNaming naming) in ItemContainerNameCensus.ExpectedNaming)
        {
            if (PinOf(naming) is { Triggers.Count: > 0 })
            {
                data.Add(label);
            }
        }
        return data;
    }

    /// <summary>Codex PR 3 round 2: every pinned trigger state, hosted. Two
    /// items walk from rest through each pinned state (the workspace tab's
    /// dirty, missing and both) and back, and in each the container reads
    /// the state's format over the item's OWN name — so a format that drops
    /// the name (every dirty tab called "duplicate") fails here as well as
    /// in the census.</summary>
    [Theory]
    [MemberData(nameof(TriggeredHosts))]
    public void EveryPinnedTriggerStateReadsItsOwnName(string label) => RunSta(() =>
    {
        Pin pin = PinOf(ItemContainerNameCensus.ExpectedNaming[label])!;
        (ItemsControl host, _) = AuthoredHost(label);
        (string Property, object? On, object? Off)[] conditions = Conditions(pin.Triggers);
        string[] titles = ["Alpha", "Beta"];
        var items = new ObservableCollection<object>();
        foreach (string title in titles)
        {
            var fields = (IDictionary<string, object?>)new ExpandoObject();
            fields[pin.Path] = title;
            foreach ((string property, _, object? off) in conditions)
            {
                fields[property] = off;
            }
            items.Add(fields);
        }
        host.ItemsSource = items;
        Hosted(host, () =>
        {
            void Expect(string state, Func<string, string> name) => Assert.True(
                titles.Select(name).SequenceEqual(ItemNames(host)),
                $"{label} {state}: expected [{string.Join(" | ", titles.Select(name))}], "
                + $"read [{string.Join(" | ", ItemNames(host))}]");

            Expect("at rest", title => title);
            foreach (TriggerNaming state in pin.Triggers)
            {
                Enter(items, conditions, state.When);
                Expect($"when {state.When}", title => Formatted(state.Format, title));
            }
            Enter(items, conditions, null);
            Expect("back at rest", title => title);
        });
    });

    /// <summary>The properties the pinned states name, each with the value
    /// that enters it and the one that leaves it.</summary>
    internal static (string Property, object? On, object? Off)[] Conditions(IReadOnlyList<TriggerNaming> triggers) =>
        [
            .. triggers
                .SelectMany(state => state.When.Split(" & "))
                .Select(condition => condition.Split('='))
                .DistinctBy(pair => pair[0])
                .Select(pair => bool.TryParse(pair[1], out bool on)
                    ? (pair[0], (object?)on, (object?)!on)
                    : (pair[0], (object?)pair[1], (object?)null)),
        ];

    /// <summary>Puts every item into the state <paramref name="when"/>
    /// names (null: at rest).</summary>
    internal static void Enter(
        IEnumerable<object> items, (string Property, object? On, object? Off)[] conditions, string? when)
    {
        HashSet<string> on = when is null ? [] : [.. when.Split(" & ").Select(condition => condition.Split('=')[0])];
        foreach (IDictionary<string, object?> fields in items.Cast<IDictionary<string, object?>>())
        {
            foreach ((string property, object? onValue, object? off) in conditions)
            {
                fields[property] = on.Contains(property) ? onValue : off;
            }
        }
    }

    /// <summary>A state's format applied as WPF applies it: the XAML
    /// <c>{}</c> escape dropped.</summary>
    internal static string Formatted(string? format, string name)
    {
        string applied = format is null ? "{0}" : format.StartsWith("{}", StringComparison.Ordinal) ? format[2..] : format;
        return string.Format(CultureInfo.InvariantCulture, applied, name);
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
            Assert.Equal([$"{SecondName}, {rule.Noun} 1", $"{SecondName}, {rule.Noun} 2"], ItemNames(host));
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

    internal static void RunSta(Action body)
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
