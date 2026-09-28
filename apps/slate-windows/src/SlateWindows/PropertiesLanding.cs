// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using SlateWindows.Panels;

namespace SlateWindows;

/// <summary>
/// W7-7 PR 4b round 3 (#1247, R-5 (h): "a removed row's keys come back to the
/// rows they were in"; codex r2 F3): the Properties header's rows are rebuilt
/// under the keys — a list property's items by Remove and Add
/// (<c>PropertyRowViewModel.RebuildItems</c>), every row by the refresh after
/// a committed edit (<c>NotePropertiesViewModel.PublishProperties</c>: Enter,
/// the boolean switch, a date pick) — and the keys went to the editor
/// region's landing, the note's body. The rows are templated per tab, so
/// they register their landing through this attached property
/// (WorkspaceTemplates.xaml), which lands by what went away
/// (<see cref="RegionFocusGuard.Holder"/>, and what it showed as it took the
/// keys): the same property's same control — a list item's by its place,
/// clamped, else the row's Add item — else the property's first stop; a
/// property gone, the row now at its place; the last one gone — the rows'
/// list collapses with it — the header's Add property. A list property's own
/// items need no landing of their own: a Remove rebuilds them inside a row
/// that stays, so the rows' landing finds the row and its item.
/// A control is known by its type and what it is bound to — its command, its
/// text, its check — never by its position among controls whose visibility a
/// commit changes (Save, Revert).
/// </summary>
internal static class PropertiesLanding
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled", typeof(bool), typeof(PropertiesLanding),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject element) =>
        (bool)element.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject element, bool value) =>
        element.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not ItemsControl rows || e.NewValue is not true)
        {
            return;
        }

        RegionFocusGuard.SetLanding(rows, () => LandInRows(rows));
        // The last property gone: the rows' list collapses with them.
        RegionFocusGuard.SetGoneLanding(rows, () => LandOnAddProperty(rows));
    }

    /// <summary>The same property's same control, else its first stop; the
    /// property gone, the row now at its place, else Add property.</summary>
    private static bool LandInRows(ItemsControl rows)
    {
        if (RegionFocusGuard.Holder is not FrameworkElement holder)
        {
            return false;
        }

        (PropertyRowViewModel? gone, int item) = RegionFocusGuard.HolderContext switch
        {
            PropertyRowViewModel published => (published, -1),
            PropertyListItemViewModel listed => (listed.Row, listed.Index),
            _ => ((PropertyRowViewModel?)null, -1),
        };
        if (gone is null)
        {
            return false;
        }

        rows.UpdateLayout();
        PropertyRowViewModel[] fresh = [.. rows.Items.OfType<PropertyRowViewModel>()];
        string role = RoleOf(holder);
        int same = Array.FindIndex(fresh, row => string.Equals(row.KeyIdentity, gone.KeyIdentity, StringComparison.Ordinal));
        if (same >= 0 && rows.ItemContainerGenerator.ContainerFromIndex(same) is DependencyObject property)
        {
            bool landed = item >= 0
                ? Controls(property).OfType<ItemsControl>().FirstOrDefault(list => list.DataContext == fresh[same]) is { } items
                    && (LandOnItem(items, role, item) || LandOnAddItem(fresh[same], items))
                : Land(Stop(property, role));
            if (landed || Land(Stop(property)))
            {
                return true;
            }
        }

        if (fresh.Length > 0
            && rows.ItemContainerGenerator.ContainerFromIndex(Math.Clamp(gone.Position, 0, fresh.Length - 1)) is DependencyObject neighbour
            && (Land(Stop(neighbour, role)) || Land(Stop(neighbour))))
        {
            return true;
        }

        return LandOnAddProperty(rows);
    }

    /// <summary>The header's Add property.</summary>
    private static bool LandOnAddProperty(ItemsControl rows) =>
        AncestorOf<Expander>(rows) is { } header
        && Land(Controls(header).FirstOrDefault(control =>
            System.Windows.Automation.AutomationProperties.GetAutomationId(control) == "PropertiesAddButton"));

    /// <summary>The item at <paramref name="index"/>, clamped, on the control
    /// playing <paramref name="role"/>.</summary>
    private static bool LandOnItem(ItemsControl items, string role, int index)
    {
        int count = items.Items.Count;
        return count > 0
            && items.ItemContainerGenerator.ContainerFromIndex(Math.Min(index, count - 1)) is DependencyObject container
            && Land(Stop(container, role));
    }

    /// <summary>The row's Add item, beside its items.</summary>
    private static bool LandOnAddItem(PropertyRowViewModel row, ItemsControl items) =>
        VisualTreeHelper.GetParent(items) is DependencyObject editor
        && Land(Controls(editor).FirstOrDefault(control =>
            control is ButtonBase { Command: var command } && ReferenceEquals(command, row.AddItemCommand)));

    /// <summary>What a control is: its type and what it is bound to.</summary>
    private static string RoleOf(FrameworkElement control)
    {
        DependencyProperty? bound = control switch
        {
            ToggleButton => ToggleButton.IsCheckedProperty,
            ButtonBase => ButtonBase.CommandProperty,
            TextBox => TextBox.TextProperty,
            DatePicker => DatePicker.SelectedDateProperty,
            _ => null,
        };
        string? path = bound is null ? null : BindingOperations.GetBinding(control, bound)?.Path?.Path;
        return control.GetType().Name + ":" + path;
    }

    /// <summary>The first stop under <paramref name="root"/> — playing
    /// <paramref name="role"/>, when one is named.</summary>
    private static FrameworkElement? Stop(DependencyObject root, string? role = null) =>
        Controls(root).FirstOrDefault(control => IsAStop(control) && (role is null || RoleOf(control) == role));

    private static bool IsAStop(FrameworkElement control) =>
        control.Focusable && control.IsVisible && control.IsEnabled && KeyboardNavigation.GetIsTabStop(control);

    private static bool Land(FrameworkElement? control) =>
        control is not null && IsAStop(control) && SelectorFocus.LandOnStop(control);

    /// <summary>The controls under <paramref name="root"/>, in tree order.</summary>
    private static IEnumerable<FrameworkElement> Controls(DependencyObject root)
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);
            if (child is FrameworkElement control)
            {
                yield return control;
            }

            foreach (FrameworkElement nested in Controls(child))
            {
                yield return nested;
            }
        }
    }

    private static T? AncestorOf<T>(DependencyObject element)
        where T : DependencyObject
    {
        for (DependencyObject? current = VisualTreeHelper.GetParent(element); current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is T found)
            {
                return found;
            }
        }

        return null;
    }
}
