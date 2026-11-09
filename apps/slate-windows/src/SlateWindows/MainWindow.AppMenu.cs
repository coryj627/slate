// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace SlateWindows;

/// <summary>
/// DS-13 (#1814): the menus sit inside the logo's popup (<c>AppMenuItem</c>), where their
/// headers' letters aren't live until it opens. So Alt and a menu's letter opens the popup at
/// that menu, and F10 opens it, as <c>design/windows/readme.md:120</c> and
/// <c>wpf-feasibility.md:115</c> ask. Plain Alt keeps WPF's menu mode on the logo.
/// </summary>
public partial class MainWindow
{
    /// <summary>Opens the app menu for a system key, answering whether it took the key: F10
    /// alone, or Alt and one of the six letters (<see cref="AppMenuKeys"/>). Nothing under a
    /// disabled bar, and a letter a live control holds (the find bar's) stays that control's.
    /// While the popup is open its own headers hold the letters, so they don't count.</summary>
    private bool TryOpenAppMenu(Key systemKey, ModifierKeys modifiers)
    {
        if (!MainMenu.IsEnabled)
        {
            return false;
        }

        if (systemKey == Key.F10 && modifiers == ModifierKeys.None)
        {
            return OpenAppMenu(null);
        }

        if (modifiers != ModifierKeys.Alt)
        {
            return false;
        }

        return AppMenuKeys.Decide(
                systemKey,
                OpenModalSurface is not null,
                () => !AppMenuItem.IsSubmenuOpen && AccessKeyManager.IsKeyRegistered(null, systemKey.ToString())) is { } menu
            && OpenAppMenu(menu);
    }

    /// <summary>Opens the logo's popup and, when named, that menu in it, with the keys on its
    /// first enabled row; with none named, on the popup's first row. A letter while open switches
    /// menus: WPF closes a menu's submenu when a sibling row takes the keys.</summary>
    private bool OpenAppMenu(string? automationId)
    {
        MenuItem[] menus = [.. AppMenuItem.Items.OfType<MenuItem>()];
        MenuItem? target = automationId is null
            ? null
            : menus.FirstOrDefault(menu => AutomationProperties.GetAutomationId(menu) == automationId);
        if (automationId is not null && target is null)
        {
            return false;
        }

        AppMenuItem.Focus();
        AppMenuItem.IsSubmenuOpen = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            if (target is null)
            {
                menus.FirstOrDefault(menu => menu.IsEnabled)?.Focus();
                return;
            }

            target.Focus();
            target.IsSubmenuOpen = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
                target.Items.OfType<MenuItem>().FirstOrDefault(row => row.IsEnabled && row.IsVisible)?.Focus());
        });
        return true;
    }
}
