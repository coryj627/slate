// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Controls;

namespace SlateWindows.Panels;

/// <summary>
/// Context-menu row targeting for the panel ListBoxes — the
/// AccessibleDataGrid rounds-5/6 contract, ported: a POINTER-invoked
/// menu acts on the CLICKED row (WPF moves selection only on the
/// left-button path, so right-clicking row B with row A selected
/// would otherwise open A), a pointer request over empty chrome gets
/// no menu, and the keyboard path (Menu / Shift+F10) keeps the
/// current selection — refusing the menu outright when nothing is
/// selected rather than opening one that silently does nothing.
/// </summary>
internal static class PanelRowTargeting
{
    /// <summary>True when the menu may open; the clicked row (pointer
    /// path) is selected and focused as a side effect.</summary>
    internal static bool TargetRowAt(
        ListBox list, object originalSource, bool pointerRequest)
    {
        if (!pointerRequest)
        {
            return list.SelectedItem is not null;
        }

        DependencyObject? current = originalSource as DependencyObject;
        while (current is not null and not ListBoxItem)
        {
            current = current is System.Windows.Media.Visual
                or System.Windows.Media.Media3D.Visual3D
                    ? System.Windows.Media.VisualTreeHelper.GetParent(current)
                    : LogicalTreeHelper.GetParent(current);
        }

        if (current is not ListBoxItem item
            || !ReferenceEquals(
                ItemsControl.ItemsControlFromItemContainer(item), list))
        {
            return false;
        }

        item.IsSelected = true;
        _ = item.Focus();
        return true;
    }

    /// <summary>Compose the outgoing-links menu for the targeted row
    /// (adversarial round 3): every action shown must be able to
    /// honor its label. Internal rows get the tab/split set, external
    /// rows only the browser action (a "New Tab" that launches the
    /// browser is a lie), and unresolved rows get NO menu — the only
    /// thing every item could do is announce failure. Items are
    /// matched by Tag ("internal"/"external") so the XAML stays the
    /// single source of the labels.</summary>
    internal static bool ComposeOutgoingMenu(
        ContextMenu? menu, OutgoingLinkRowViewModel row)
    {
        if (menu is null || row.IsUnresolved)
        {
            return false;
        }

        bool external = row.Link.IsExternal;
        foreach (MenuItem item in menu.Items.OfType<MenuItem>())
        {
            bool forExternal = string.Equals(
                item.Tag as string, "external", StringComparison.Ordinal);
            item.Visibility = forExternal == external
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
        return true;
    }
}
