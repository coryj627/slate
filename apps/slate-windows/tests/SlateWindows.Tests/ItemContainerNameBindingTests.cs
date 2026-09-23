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
        Assert.True(named.Length > 35, $"only {named.Length} named hosts — the table read is broken");
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
        Style style = ShellXamlFragments.LoadStyle(authored, file);
        Type hostType = ItemContainerNameCensus.ResolveType(host)
            ?? throw new Xunit.Sdk.XunitException($"{label}: the host type does not resolve");
        // The pinned converter, loaded apart from the style: a style that
        // drops it must read differently, not switch this fact's mode.
        IValueConverter? converter = bound.Converter is null ? null : ShellXamlFragments.LoadConverter(bound.Converter);
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

    public static TheoryData<string> TriggeredHosts()
    {
        var data = new TheoryData<string>();
        foreach ((string label, ContainerNaming naming) in ItemContainerNameCensus.ExpectedNaming)
        {
            if (naming is ContainerNaming.Bound { Triggers.Count: > 0 })
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
        var bound = (ContainerNaming.Bound)ItemContainerNameCensus.ExpectedNaming[label];
        (string file, XElement element) = ItemContainerNameCensus.XamlHost(label);
        XElement authored = ItemContainerNameCensus.ContainerStyle(element, ItemContainerNameCensus.KeyedStyles())
            ?? throw new Xunit.Sdk.XunitException($"{label}: {file} gives it no container style");
        Type hostType = ItemContainerNameCensus.ResolveType(element)
            ?? throw new Xunit.Sdk.XunitException($"{label}: the host type does not resolve");
        var host = (ItemsControl)Activator.CreateInstance(hostType, nonPublic: true)!;
        host.ItemContainerStyle = ShellXamlFragments.LoadStyle(authored, file);

        // Every property a pinned state names, and the value that sets it.
        (string Property, object? On, object? Off)[] conditions =
        [
            .. bound.Triggers
                .SelectMany(state => state.When.Split(" & "))
                .Select(condition => condition.Split('='))
                .DistinctBy(pair => pair[0])
                .Select(pair => bool.TryParse(pair[1], out bool on)
                    ? (pair[0], (object?)on, (object?)!on)
                    : (pair[0], (object?)pair[1], (object?)null)),
        ];
        string[] titles = ["Alpha", "Beta"];
        var items = new ObservableCollection<object>();
        foreach (string title in titles)
        {
            var fields = (IDictionary<string, object?>)new ExpandoObject();
            fields[bound.Path] = title;
            foreach ((string property, _, object? off) in conditions)
            {
                fields[property] = off;
            }
            items.Add(fields);
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
            void Enter(string? when)
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
            void Expect(string state, Func<string, string> name) => Assert.True(
                titles.Select(name).SequenceEqual(ItemNames(host)),
                $"{label} {state}: expected [{string.Join(" | ", titles.Select(name))}], "
                + $"read [{string.Join(" | ", ItemNames(host))}]");

            Expect("at rest", title => title);
            foreach (TriggerNaming state in bound.Triggers)
            {
                Assert.Null(state.Converter);
                Enter(state.When);
                string format = state.Format is { } authoredFormat
                    ? (authoredFormat.StartsWith("{}", StringComparison.Ordinal) ? authoredFormat[2..] : authoredFormat)
                    : "{0}";
                Expect($"when {state.When}", title => string.Format(CultureInfo.InvariantCulture, format, title));
            }
            Enter(null);
            Expect("back at rest", title => title);
        }
        finally
        {
            window.Close();
        }
    });

    /// <summary>Every item peer's Name, in order.</summary>
    private static string[] ItemNames(ItemsControl host)
    {
        host.UpdateLayout();
        PumpedDispatcher.Drain();
        host.UpdateLayout();
        AutomationPeer peer = UIElementAutomationPeer.CreatePeerForElement(host);
        peer.ResetChildrenCache();
        return [.. (peer.GetChildren() ?? []).OfType<ItemAutomationPeer>().Select(item => item.GetName())];
    }

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
        else if (bound.ItemType.IsEnum)
        {
            // An enum item speaks through its converter: its label, never
            // its member name.
            Assert.True(converter is not null, $"{label}: an enum item needs a pinned converter to speak");
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
            Assert.Equal(typeof(string), bound.ItemType);
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
