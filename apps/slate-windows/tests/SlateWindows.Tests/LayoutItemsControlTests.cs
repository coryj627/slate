// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Data;

namespace SlateWindows.Tests;

/// <summary>
/// W7-7 PR 3 (#1246, contract R-4): a container that only WRAPS the
/// real stop is layout. WPF gives every item of a plain ItemsControl a
/// DataItem peer named by the item's <c>ToString()</c>, so NVDA spoke
/// "RecentVault { Path = …, LastOpenedMs = … }, data item" and
/// "SlateWindows.WorkspacePaneNodeViewModel, data item, 1 of 2" on the
/// way to the button or editor the reader actually landed on (record F3).
/// </summary>
public sealed class LayoutItemsControlTests
{
    private sealed record Row(string Label);

    /// <summary>The host is ONE group carrying its name; each container
    /// peer answers IsControlElement/IsContentElement false (NVDA treats
    /// such an element as layout and never speaks it) and carries no name,
    /// so not even a raw-view client can read the item's ToString; the
    /// wrapped control is the stop. The host itself is never a stop.</summary>
    [Fact]
    public void LayoutContainersLeaveTheControlViewAndTheHostIsANamedGroup() => RunSta(() =>
    {
        var button = new FrameworkElementFactory(typeof(Button));
        button.SetBinding(AutomationProperties.NameProperty, new Binding(nameof(Row.Label)));
        var host = new LayoutItemsControl
        {
            ItemsSource = new[] { new Row("Alpha"), new Row("Beta") },
            ItemTemplate = new DataTemplate { VisualTree = button },
        };
        AutomationProperties.SetName(host, "Recent vaults");
        var window = new Window
        {
            Content = host,
            Width = 300,
            Height = 200,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            AutomationPeer hostPeer = Assert.IsType<LayoutItemsControlAutomationPeer>(
                UIElementAutomationPeer.CreatePeerForElement(host));
            Assert.Equal(AutomationControlType.Group, hostPeer.GetAutomationControlType());
            Assert.Equal("Recent vaults", hostPeer.GetName());
            Assert.True(hostPeer.IsControlElement());
            Assert.False(host.Focusable);

            List<AutomationPeer> containers = hostPeer.GetChildren();
            Assert.Equal(2, containers.Count);
            foreach (AutomationPeer container in containers)
            {
                Assert.IsType<LayoutItemAutomationPeer>(container);
                Assert.False(container.IsControlElement());
                Assert.False(container.IsContentElement());
                Assert.Equal(string.Empty, container.GetName());
                AutomationPeer stop = Assert.Single(container.GetChildren());
                Assert.Equal(AutomationControlType.Button, stop.GetAutomationControlType());
                Assert.True(stop.IsControlElement());
            }
            Assert.Equal(
                ["Alpha", "Beta"],
                containers.Select(container => container.GetChildren()[0].GetName()));

            host.Visibility = Visibility.Collapsed;
            Assert.False(hostPeer.IsControlElement());
        }
        finally
        {
            window.Close();
        }
    });

    /// <summary>Spec §4.2 item 2: the split host is the "Editor panes"
    /// group and its containers are layout — the announcer already says
    /// "Editor pane 1 of 2, {title}." on pane moves (W7-6), so nothing
    /// positional is lost.</summary>
    [Fact]
    public void TheSplitHostIsTheEditorPanesGroup() => RunSta(() =>
    {
        var resources = Assert.IsType<ResourceDictionary>(Application.LoadComponent(
            new Uri("/SlateWindows;component/WorkspaceTemplates.xaml", UriKind.Relative)));
        DataTemplate template = Assert.IsType<DataTemplate>(resources["WorkspaceNodeTemplate"]);
        Grid root = Assert.IsType<Grid>(template.LoadContent());
        LayoutItemsControl host = Assert.Single(root.Children.OfType<LayoutItemsControl>());
        Assert.Equal("Editor panes", AutomationProperties.GetName(host));
    });

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
