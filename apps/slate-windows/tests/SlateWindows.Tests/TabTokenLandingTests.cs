// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SlateWindows.Tests;

/// <summary>
/// W7-7 PR 4b (#1247, R-5 amendment (f); flag 3 of PR 4's S4): a tab takes
/// the keys by SELECTING itself, and the workspace's tab selection switches
/// the document. A restore whose token is another tab's header — the tab
/// the reader was on before a sheet, since switched away from — landed on
/// it with <c>Focus()</c> and switched the document back. It lands on the
/// ACTIVE tab, and the selection stays; the bare tab control lands there
/// too.
/// </summary>
public sealed class TabTokenLandingTests
{
    [Fact]
    public void ATokenOnAnotherTabLandsOnTheActiveTab() => RunSta(() =>
    {
        (Window window, TabControl tabs, Button outside) = Host();
        try
        {
            var other = (TabItem)tabs.Items[2];
            Assert.True(outside.Focus());

            Assert.True(SelectorFocus.LandOnStop(other));

            Assert.Same(tabs.Items[0], Keyboard.FocusedElement);
            Assert.Equal(0, tabs.SelectedIndex);
        }
        finally
        {
            window.Close();
        }
    });

    [Fact]
    public void TheBareTabControlLandsOnItsActiveTab() => RunSta(() =>
    {
        (Window window, TabControl tabs, Button outside) = Host();
        try
        {
            tabs.SelectedIndex = 1;
            Assert.True(outside.Focus());

            Assert.True(SelectorFocus.LandOnStop(tabs));

            Assert.Same(tabs.Items[1], Keyboard.FocusedElement);
            Assert.Equal(1, tabs.SelectedIndex);
        }
        finally
        {
            window.Close();
        }
    });

    [Fact]
    public void TheActiveTabsOwnTokenLandsOnItself() => RunSta(() =>
    {
        (Window window, TabControl tabs, Button outside) = Host();
        try
        {
            Assert.True(outside.Focus());

            Assert.True(SelectorFocus.LandOnStop((TabItem)tabs.Items[0]));

            Assert.Same(tabs.Items[0], Keyboard.FocusedElement);
            Assert.Equal(0, tabs.SelectedIndex);
        }
        finally
        {
            window.Close();
        }
    });

    private static (Window Window, TabControl Tabs, Button Outside) Host()
    {
        var tabs = new TabControl();
        for (int index = 0; index < 3; index++)
        {
            tabs.Items.Add(new TabItem { Header = $"Tab {index}", Content = new TextBox { Text = $"Body {index}" } });
        }

        tabs.SelectedIndex = 0;
        var outside = new Button { Content = "Outside" };
        var root = new DockPanel();
        DockPanel.SetDock(outside, Dock.Top);
        root.Children.Add(outside);
        root.Children.Add(tabs);
        var window = new Window
        {
            Content = root,
            Width = 500,
            Height = 300,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
            ShowActivated = false,
        };
        window.Show();
        window.UpdateLayout();
        return (window, tabs, outside);
    }

    private static void RunSta(Action body) =>
        StaThread.RunPumped(body, TimeSpan.FromSeconds(60), "STA test body timed out.");
}
