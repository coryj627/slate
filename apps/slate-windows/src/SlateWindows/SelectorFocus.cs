// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace SlateWindows;

/// <summary>
/// W7-7 PR 4 (#1247, contract R-5): a list's landing is an ITEM — the
/// selected one, else the first that can take the keys — never the bare
/// container.
/// </summary>
/// <remarks>
/// <para>
/// A bare list has no row for an arrow to move from, so the arrow goes to
/// WPF's directional navigation, which searches the whole window: Ctrl+R's
/// "Right pane panels" and Shift+F6's Citations list both handed Down to a
/// top-level menu (NVDA pass F4). Every list landing in the shell goes
/// through here (<c>SelectorLandingCensus</c>).
/// </para>
/// <para>
/// The selection is read, never written: selecting would switch the rail's
/// shown leaf or open a filter result. A row that takes focus unselected is
/// the state a Win32 list shows before its first arrow; an arrow or Space
/// selects from there. A row that cannot take the keys — a Bases list's
/// group heading is a disabled separator — is passed over for the next.
/// </para>
/// <para>
/// An EMPTY list's stop is its notice when one is showing (spec §5.2.2): the
/// caller names the notices, and the first visible one that takes the keys
/// is the landing. With none, the list itself takes focus (AR-6): there is
/// no row to land on, and the list is still the stop. A combo box or a grid
/// is not a list landing (<see cref="IsListLanding"/>) and is never passed.
/// </para>
/// <para>
/// A row whose container has not been generated yet — a virtualizing panel
/// after a republish; measured on the Citations list (#1098), whose
/// generator still answered null at Input priority — holds focus on the
/// list, so it is never stranded, and seats it on the row once the
/// container exists. That second step stands down unless focus is still
/// exactly on the list.
/// </para>
/// </remarks>
internal static class SelectorFocus
{
    /// <summary>How far past disabled rows the first-row search reaches
    /// before giving up: realizing a container can cost a layout pass, and
    /// a real list's first row sits right after its first heading.</summary>
    private const int FirstRowSearchLimit = 64;

    /// <returns>Whether a row took focus now.</returns>
    internal static bool FocusFirstOrSelectedItem(Selector selector, params UIElement?[] emptyNotices)
    {
        if (!selector.HasItems)
        {
            foreach (UIElement? notice in emptyNotices)
            {
                if (notice is { IsVisible: true } && notice.Focus())
                {
                    return false;
                }
            }

            _ = selector.Focus();
            return false;
        }

        if (FocusLandingItem(selector))
        {
            return true;
        }

        _ = selector.Focus();
        _ = selector.Dispatcher.InvokeAsync(
            () =>
            {
                if (selector.IsKeyboardFocused && selector.HasItems)
                {
                    _ = FocusLandingItem(selector);
                }
            },
            System.Windows.Threading.DispatcherPriority.Background);
        return false;
    }

    /// <summary>Whether focusing <paramref name="element"/> itself would
    /// land on a bare list. A combo box is its own stop — its items live in
    /// its drop-down — and a grid's stop is a cell, which the grid seats
    /// (AccessibleDataGrid), not a row container.</summary>
    internal static bool IsListLanding(UIElement element) =>
        element is Selector and not ComboBox and not DataGrid;

    private static bool FocusLandingItem(Selector selector)
    {
        if (selector.SelectedItem is { } selected && selector.Items.Contains(selected))
        {
            return RealizedContainer(selector, selected) is { } container && container.Focus();
        }

        int searched = 0;
        foreach (object item in selector.Items)
        {
            if (++searched > FirstRowSearchLimit
                || RealizedContainer(selector, item) is not { } container)
            {
                return false;
            }

            if (container.Focus())
            {
                return true;
            }
        }

        return false;
    }

    private static UIElement? RealizedContainer(Selector selector, object item)
    {
        if (selector.ItemContainerGenerator.ContainerFromItem(item) is UIElement realized)
        {
            return realized;
        }

        (selector as ListBox)?.ScrollIntoView(item);
        selector.UpdateLayout();
        return selector.ItemContainerGenerator.ContainerFromItem(item) as UIElement;
    }
}
