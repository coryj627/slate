// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;

namespace SlateWindows;

/// <summary>DS-17 (DSD12): the Files toolbar's Sort and group menu. The six orders, then
/// Group by date, then the tag tree and the dual pane, which the stack of a combo box, a
/// check box and two toggle buttons held. Every row keeps its setting and its name.</summary>
public partial class MainWindow
{
    internal const string SidebarSortItemIdPrefix = "SidebarSort.";

    /// <summary>The menu while it's open, for the facts.</summary>
    internal ContextMenu? SidebarSortMenu { get; private set; }

    private void SidebarSortButton_Click(object sender, RoutedEventArgs e) => OpenSidebarSortMenu();

    /// <summary>Opens Sort and group under its button, its order checked and holding the
    /// keys. The keys go to the button first, so the menu's own restore brings them back
    /// there however it was opened.</summary>
    internal void OpenSidebarSortMenu()
    {
        if (SidebarSortButton.DataContext is not FilesSidebarViewModel sidebar
            || !SidebarSortButton.IsVisible
            || !SidebarSortButton.Focus())
        {
            return;
        }

        var menu = new ContextMenu
        {
            Style = (Style)FindResource("Slate.ContextMenuStyle"),
            PlacementTarget = SidebarSortButton,
            Placement = PlacementMode.Bottom,
        };
        AutomationProperties.SetName(menu, "Sort and group");
        var converter = new SidebarSortModeLabelConverter();
        foreach (SidebarSortMode mode in sidebar.SortModes)
        {
            var row = new CheckMenuItem
            {
                Header = (string)converter.Convert(mode, typeof(string), null!, CultureInfo.CurrentCulture),
                IsCheckable = true,
            };
            _ = row.SetBinding(
                MenuItem.IsCheckedProperty,
                new Binding(nameof(FilesSidebarViewModel.SortMode))
                {
                    Source = sidebar,
                    Mode = BindingMode.OneWay,
                    Converter = IsMode.Instance,
                    ConverterParameter = mode,
                });
            AutomationProperties.SetAutomationId(row, SidebarSortItemIdPrefix + mode);
            // The order lands as the row checks, inside the click and before the menu
            // closes, so the button's name already says it when the keys come back:
            // a reader hears it once (DS-16's More panels found the twice).
            row.Checked += (_, _) =>
            {
                if (menu.IsOpen && ReferenceEquals(SidebarSortButton.DataContext, sidebar) && sidebar.SortMode != mode)
                {
                    sidebar.SortMode = mode;
                }
            };
            // A pick of the current order changes nothing and keeps its check.
            row.Click += (_, _) =>
            {
                foreach (MenuItem other in menu.Items.OfType<MenuItem>())
                {
                    BindingOperations.GetBindingExpression(other, MenuItem.IsCheckedProperty)?.UpdateTarget();
                }

                menu.IsOpen = false;
            };
            _ = menu.Items.Add(row);
        }

        _ = menu.Items.Add(new Separator());
        _ = menu.Items.Add(Toggle(menu, sidebar, nameof(FilesSidebarViewModel.GroupByDate), "Group by date", "SidebarGroupDates", value => sidebar.GroupByDate = value));
        _ = menu.Items.Add(new Separator());
        _ = menu.Items.Add(Toggle(menu, sidebar, nameof(FilesSidebarViewModel.ShowTags), "Show tag tree", "SidebarShowTags", value => sidebar.ShowTags = value));
        _ = menu.Items.Add(Toggle(menu, sidebar, nameof(FilesSidebarViewModel.IsDualPaneEnabled), "Enable dual-pane files sidebar", "SidebarDualPaneToggle", value => sidebar.IsDualPaneEnabled = value));

        menu.Opened += (_, _) =>
        {
            MenuItem? current = menu.Items.OfType<MenuItem>()
                .FirstOrDefault(row => AutomationProperties.GetAutomationId(row).StartsWith(SidebarSortItemIdPrefix, StringComparison.Ordinal) && row.IsChecked)
                ?? menu.Items.OfType<MenuItem>().FirstOrDefault();
            _ = current?.Focus();
        };
        // Built fresh at each open and dropped at close; its rows' bindings watch the
        // sidebar weakly, so nothing outlives it. The keys go back to the button by the
        // menu's own restore.
        menu.Closed += (_, _) =>
        {
            if (ReferenceEquals(SidebarSortMenu, menu))
            {
                SidebarSortMenu = null;
            }
        };
        SidebarSortMenu = menu;
        menu.IsOpen = true;
    }

    /// <summary>A setting the stack held as a check box or a toggle button, as a checkable
    /// row by the same name and id. The model drives the check one way; the row writes
    /// back only while its menu is open and its sidebar is the live one, so a toggle that
    /// arrives after the menu closed or the vault changed writes nothing (Codex round 1).</summary>
    private CheckMenuItem Toggle(ContextMenu menu, FilesSidebarViewModel sidebar, string property, string name, string automationId, Action<bool> write)
    {
        var row = new CheckMenuItem { Header = name, IsCheckable = true };
        _ = row.SetBinding(MenuItem.IsCheckedProperty, new Binding(property) { Source = sidebar, Mode = BindingMode.OneWay });
        AutomationProperties.SetAutomationId(row, automationId);
        void Commit(bool value)
        {
            if (menu.IsOpen && ReferenceEquals(SidebarSortButton.DataContext, sidebar))
            {
                write(value);
            }
        }

        row.Checked += (_, _) => Commit(true);
        row.Unchecked += (_, _) => Commit(false);
        return row;
    }

    /// <summary>Whether the bound order is the parameter's.</summary>
    private sealed class IsMode : IValueConverter
    {
        internal static readonly IsMode Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            Equals(value, parameter);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            Binding.DoNothing;
    }
}
