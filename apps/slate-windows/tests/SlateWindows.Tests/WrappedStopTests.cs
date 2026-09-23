// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.ObjectModel;
using System.Dynamic;
using System.Runtime.ExceptionServices;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Xml.Linq;
using SlateWindows.Bases;
using SlateWindows.Panels;
using SlateWindows.Tests.Censuses;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W7-7 PR 3 (#1246, R-4's one-stop rule; the spec review of PR 3): a
/// container that WRAPS an item's own controls is layout — out of the
/// control view and nameless — and the controls are the item's stops.
/// Named, the wrapper was one more stop NVDA traversed beside them: a
/// property row's container read the editor's own label, and a builder
/// row's read "Condition" above its Remove button and expression box.
/// Where a wrapper's name told duplicate siblings apart, the control
/// carries that now: a recent vault's button its path, a builder row's
/// controls their position. Each host is loaded from its authored XAML
/// with its templates over real items, and every item's stops counted.
/// </summary>
public sealed class WrappedStopTests
{
    /// <summary>What a reader lands on for one item: its container when
    /// the container is in the control view, then each visible control.</summary>
    private sealed record ItemStops(AutomationPeer Container, IReadOnlyList<AutomationPeer> Stops)
    {
        public string[] Names => [.. Stops.Select(stop => stop.GetName())];
    }

    /// <summary>Two recent vaults share a display name. Each vault is
    /// exactly ONE stop — its button — and the button's name carries the
    /// path that tells the two apart.</summary>
    [Fact]
    public void EachRecentVaultIsOneStopItsButton() => RunSta(() =>
    {
        var vaults = new ObservableCollection<RecentVault>
        {
            new(@"C:\Work\notes", "notes", 3),
            new(@"D:\Home\notes", "notes", 2),
            new(@"C:\Work\journal", "journal", 1),
        };
        dynamic context = new ExpandoObject();
        context.RecentVaults = vaults;
        context.OpenRecentCommand = null;
        Hosted("Recent vaults", (object)context, items =>
        {
            Assert.Equal(vaults.Count, items.Count);
            for (int index = 0; index < vaults.Count; index++)
            {
                AutomationPeer stop = Assert.Single(items[index].Stops);
                Assert.Equal(AutomationControlType.Button, stop.GetAutomationControlType());
                Assert.True(stop.IsKeyboardFocusable());
                Assert.Equal(RecentVault.SpokenName(vaults[index], vaults), stop.GetName());
            }
            Assert.Equal(
                [@"notes, C:\Work\notes", @"notes, D:\Home\notes", "journal"],
                items.Select(item => item.Stops[0].GetName()));
        });
    });

    /// <summary>A builder row's stops are its Remove button and its
    /// expression box, and a group's member box, each carrying the row's
    /// position; the row's container is none of them.</summary>
    [Fact]
    public void EachBuilderRowsStopsAreItsOwnControls() => RunSta(() =>
    {
        var rows = new ObservableCollection<BuilderConditionRow>
        {
            new() { PreservedNode = JsonNode.Parse("{\"Stmt\":true}") },
            new(),
            new(),
            new() { GroupMembers = [new()] },
        };
        BuilderConditionRow.Number(rows);
        dynamic context = new ExpandoObject();
        context.ConditionRows = rows;
        Hosted("BuilderConditions", (object)context, items =>
        {
            string[][] expected =
            [
                ["Remove Existing filters (preserved)", "Existing filters (preserved) expression"],
                ["Remove Condition 2", "Condition 2 expression"],
                ["Remove Condition 3", "Condition 3 expression"],
                ["Remove Group 4", "Group 4 expression", "Group 4 condition 1 expression"],
            ];
            Assert.Equal(expected, items.Select(item => item.Names));
        });
    });

    /// <summary>A property row's stops are its editor and its buttons; the
    /// first is the editor, named by the label its container repeated.</summary>
    [Fact]
    public void EachPropertyRowsStopsAreItsEditorAndButtons() => RunSta(() =>
    {
        var rows = new ObservableCollection<PropertyRowViewModel>
        {
            Row(new Property("title", "text", "\"Hello\"", "title")),
            Row(new Property("count", "number", "3", "count")),
        };
        dynamic properties = new ExpandoObject();
        properties.Rows = rows;
        properties.HeaderGroupName = "Properties, 2 properties";
        dynamic context = new ExpandoObject();
        context.Properties = properties;
        Hosted("PropertiesRows", (object)context, items =>
        {
            Assert.Equal(rows.Count, items.Count);
            for (int index = 0; index < rows.Count; index++)
            {
                string[] names = items[index].Names;
                Assert.Equal(rows[index].AutomationName, names[0]);
                Assert.Contains(rows[index].SaveLabel, names);
                Assert.Contains(rows[index].DeleteLabel, names);
            }
        });
    });

    private static PropertyRowViewModel Row(Property property) =>
        new(property, "note.md", "hash", _ => { }, _ => { }, _ => { });

    /// <summary>Loads the authored host <paramref name="label"/> over
    /// <paramref name="dataContext"/>, reads each item's stops, and holds
    /// every host to the rule before <paramref name="check"/> reads them:
    /// no container is a stop, and the stops' names tell every control
    /// apart with no wrapper's help.</summary>
    private static void Hosted(string label, object dataContext, Action<List<ItemStops>> check)
    {
        (string file, XElement element) = ItemContainerNameCensus.XamlHost(label);
        (Grid root, ItemsControl host) = ShellXamlFragments.LoadHost(element, file);
        var window = new Window
        {
            DataContext = dataContext,
            Content = root,
            Width = 720,
            Height = 480,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            PumpedDispatcher.Drain();
            window.UpdateLayout();
            AutomationPeer hostPeer = UIElementAutomationPeer.CreatePeerForElement(host);
            hostPeer.ResetChildrenCache();
            var items = new List<ItemStops>();
            foreach (AutomationPeer container in hostPeer.GetChildren() ?? [])
            {
                items.Add(new ItemStops(container, Stops(container)));
            }
            Assert.NotEmpty(items);
            for (int index = 0; index < items.Count; index++)
            {
                Assert.False(
                    items[index].Container.IsControlElement(),
                    $"{label}: item {index + 1}'s container is a stop beside its own controls "
                    + $"(named '{items[index].Container.GetName()}')");
                Assert.NotEmpty(items[index].Stops);
            }
            string[] names = [.. items.SelectMany(item => item.Names)];
            Assert.All(names, name => Assert.False(string.IsNullOrWhiteSpace(name), $"{label}: an unnamed stop"));
            Assert.True(
                names.Distinct(StringComparer.Ordinal).Count() == names.Length,
                $"{label}: stops share a name: {string.Join(" | ", names)}");
            check(items);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>One item's stops: its container when the container is in
    /// the control view, then each visible control inside — a control is
    /// one stop with its own content (a button's text), and a nested plain
    /// items host contributes its items' stops.</summary>
    private static List<AutomationPeer> Stops(AutomationPeer container)
    {
        var stops = new List<AutomationPeer>();
        if (container.IsControlElement())
        {
            stops.Add(container);
        }
        Collect(container, stops);
        return stops;
    }

    private static void Collect(AutomationPeer peer, List<AutomationPeer> stops)
    {
        foreach (AutomationPeer child in peer.GetChildren() ?? [])
        {
            UIElement? owner = (child as UIElementAutomationPeer)?.Owner;
            if (owner is { IsVisible: false })
            {
                continue;
            }
            if (owner is ItemsControl and not Selector and not TreeView)
            {
                foreach (AutomationPeer item in child.GetChildren() ?? [])
                {
                    stops.AddRange(Stops(item));
                }
                continue;
            }
            if (owner is Control && child.IsControlElement())
            {
                stops.Add(child);
                continue;
            }
            Collect(child, stops);
        }
    }

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
