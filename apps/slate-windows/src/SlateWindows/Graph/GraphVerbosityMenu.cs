// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;

namespace SlateWindows.Graph;

/// <summary>
/// W6-2 PR C (#746), contracts C-9 and C-12: the Graph menu's Verbosity
/// submenu, BUILT FROM CORE'S VECTOR in code. A WPF <c>MenuItem</c>'s
/// <c>ItemsSource</c> generates plain <c>MenuItem</c> containers and no
/// style can make them the shell's derived <see cref="CheckMenuItem"/>
/// (IGO-17), so the window populates the submenu's <c>Items</c> from the
/// preferences' <see cref="GraphPreferencesViewModel.Choices"/> — one
/// <see cref="CheckMenuItem"/> per choice: <c>Header</c> = the title,
/// <c>CommandParameter</c> = the tag, <c>IsChecked</c> bound OneWay to
/// the choice's <c>IsSelected</c>, <c>Command</c> = the one parameterised
/// setter, <c>AutomationId</c> = "GraphVerbosity." + the tag. The XAML
/// declares the empty submenu and NO literal level (0b-1, 0b-12); the
/// window clears and rebuilds for EVERY observed workspace and unwires
/// the old (IGP-15).
/// </summary>
internal static class GraphVerbosityMenu
{
    internal const string AutomationIdPrefix = "GraphVerbosity.";

    /// <summary>Clear the submenu and build one check item per choice, in
    /// the vector's order.</summary>
    internal static void Populate(MenuItem submenu, GraphPreferencesViewModel preferences)
    {
        ArgumentNullException.ThrowIfNull(submenu);
        ArgumentNullException.ThrowIfNull(preferences);
        Clear(submenu);
        foreach (GraphVerbosityChoice choice in preferences.Choices)
        {
            var item = new CheckMenuItem
            {
                Header = choice.Title,
                IsCheckable = true,
                Command = preferences.SetVerbosityCommand,
                CommandParameter = choice.Tag,
            };
            _ = item.SetBinding(
                MenuItem.IsCheckedProperty,
                new Binding(nameof(GraphVerbosityChoice.IsSelected)) { Source = choice, Mode = BindingMode.OneWay });
            AutomationProperties.SetAutomationId(item, AutomationIdPrefix + choice.Tag);
            _ = submenu.Items.Add(item);
        }
    }

    /// <summary>Remove every built item, its binding cleared, so no item
    /// invokes or observes a workspace that is gone (IGP-15).</summary>
    internal static void Clear(MenuItem submenu)
    {
        ArgumentNullException.ThrowIfNull(submenu);
        foreach (object entry in submenu.Items.OfType<object>().ToArray())
        {
            if (entry is CheckMenuItem item)
            {
                BindingOperations.ClearBinding(item, MenuItem.IsCheckedProperty);
                item.Command = null;
                item.CommandParameter = null;
            }
        }
        submenu.Items.Clear();
    }
}
