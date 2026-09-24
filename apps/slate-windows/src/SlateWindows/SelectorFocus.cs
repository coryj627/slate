// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace SlateWindows;

/// <summary>
/// W7-7 PR 4 (#1247, contract R-5): a list's landing is an ITEM — the
/// selected one, else the first that can take the keys — and a list that
/// has items never takes the keys itself.
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
/// A row whose container does not exist is realized NOW: a generator that
/// has not produced containers yet makes <c>ScrollIntoView</c> defer to
/// Loaded priority, so the list is laid out first — which generates the
/// viewport's containers — and the item is then brought in, which a
/// virtualizing panel realizes. Only a row that still has no container
/// (the list is not laid out, or has no items host) is left to a deferred
/// seat, and the call answers false so the caller lands on its own stable
/// stop. The seat serves the NEWEST request only — a later landing through
/// here, a caller's fallback to the rail among them, supersedes it — and
/// runs only while the keys are exactly where they were when the call was
/// made, so a fallback that moved them retires it too. Codex round 3: the
/// list itself used to hold the keys meanwhile — a real focus change,
/// which UIA reports, on the very element R-5 keeps the keys off.
/// </para>
/// <para>
/// An EMPTY list's stop is its notice when one is showing (spec §5.2.2):
/// the caller names the notices, and the first visible one that takes the
/// keys is the landing. With none, the empty list itself takes focus
/// (AR-6): there is no row to land on, and the list is still the stop. A
/// combo box or a grid is not a list landing (<see cref="IsListLanding"/>)
/// and is never passed.
/// </para>
/// </remarks>
internal static class SelectorFocus
{
    /// <summary>How far past disabled rows the first-row search reaches
    /// before giving up: realizing a container can cost a layout pass, and
    /// a real list's first row sits right after its first heading.</summary>
    private const int FirstRowSearchLimit = 64;

    /// <summary>The newest landing request on this UI thread; a deferred
    /// seat runs only for it.</summary>
    [ThreadStatic]
    private static int _newestRequest;

    private enum Landing
    {
        Landed,
        Unrealized,
        Refused,
    }

    /// <returns>Whether the keys landed now: on a row, or — for an EMPTY
    /// list — on its showing notice or on the list itself (AR-6). False
    /// means nothing took them, and the caller lands on its stable
    /// stop.</returns>
    internal static bool FocusFirstOrSelectedItem(Selector selector, params UIElement?[] emptyNotices)
    {
        ++_newestRequest;
        if (!selector.HasItems)
        {
            foreach (UIElement? notice in emptyNotices)
            {
                if (notice is { IsVisible: true } && notice.Focus())
                {
                    return true;
                }
            }

            return selector.Focus();
        }

        Landing landing = FocusLandingItem(selector);
        if (landing != Landing.Unrealized)
        {
            return landing == Landing.Landed;
        }

        SeatLater(selector, () => selector.HasItems && FocusLandingItem(selector) == Landing.Landed);
        return false;
    }

    /// <summary>
    /// Seat a landing that cannot be made now — a row or a cell with no
    /// container yet — once the dispatcher reaches Background priority:
    /// for the NEWEST request only, and only while the keys are exactly
    /// where they were when it was asked for, so any later landing, or a
    /// caller's fallback that moved them, retires it (codex round 3). It is
    /// tried once.
    /// </summary>
    internal static void SeatLater(DispatcherObject owner, Func<bool> land)
    {
        int request = ++_newestRequest;
        IInputElement? leftAt = Keyboard.FocusedElement;
        _ = owner.Dispatcher.InvokeAsync(
            () =>
            {
                if (request == _newestRequest && ReferenceEquals(Keyboard.FocusedElement, leftAt))
                {
                    _ = land();
                }
            },
            DispatcherPriority.Background);
    }

    /// <summary>Whether focusing <paramref name="element"/> itself would
    /// land on a bare list. A combo box is its own stop — its items live in
    /// its drop-down — and a grid's stop is a cell, which the grid seats
    /// (AccessibleDataGrid), not a row container.</summary>
    internal static bool IsListLanding(UIElement element) =>
        element is Selector and not ComboBox and not DataGrid;

    /// <summary>A leaf's first stop — the region ring's right-pane content
    /// landing and a leaf reveal's. A list lands on its row. Any other stop
    /// is judged by where the keys END UP (W7-6 #1240): a stop that hands
    /// them on to an item or a cell of its own answers false from its own
    /// <c>Focus()</c> though the keys are inside it, and a caller that
    /// falls back on false would take them away again.</summary>
    /// <returns>Whether the keys landed on the stop or inside it.</returns>
    internal static bool LandOnStop(UIElement stop) => stop switch
    {
        Selector list when IsListLanding(list) => FocusFirstOrSelectedItem(list),
        TreeView tree => FocusSelectedOrFirstRow(tree),
        _ => stop.Focus() || stop.IsKeyboardFocusWithin,
    };

    /// <summary>
    /// W7-7 PR 4 (#1247, contract R-5; spec review round 23): a TREE's
    /// landing is a ROW — the selected one, else the first — and never the
    /// tree itself.
    /// </summary>
    /// <remarks>
    /// WPF hands a focused tree's keys to its selected container only when
    /// one exists. With no selection the bare tree keeps them: Up and Down
    /// then reach its first row, but Left and Right go to directional
    /// navigation and out of the region (<c>TreeLandingTests</c> measures
    /// both). A selected row the panel has not realized is brought into
    /// view level by level; one under a collapsed row is hidden, cannot take
    /// the keys, and is not a landing. The selection is not written here — a
    /// row selects itself
    /// when it takes focus, as the first row does — and an empty tree is not
    /// a landing either: the caller's stable stop is.
    /// </remarks>
    /// <param name="selectedPath">The selected row's items, root first, when
    /// the tree's own selection is not the source of truth (a recycled
    /// container drops it); null reads the tree's.</param>
    /// <returns>Whether a row took the keys now. False means none did, and
    /// the caller lands on its stable stop.</returns>
    internal static bool FocusSelectedOrFirstRow(TreeView tree, IReadOnlyList<object>? selectedPath = null)
    {
        ++_newestRequest;
        if (!tree.HasItems)
        {
            return false;
        }

        TreeViewItem? row;
        if (selectedPath is { Count: > 0 })
        {
            row = RealizedTreeRow(tree, selectedPath);
        }
        else if (tree.SelectedItem is { } selected)
        {
            row = SelectedTreeRow(tree)
                ?? (tree.Items.Contains(selected) ? RealizedTreeRow(tree, [selected]) : null);
        }
        else
        {
            row = RealizedTreeRow(tree, [tree.Items[0]]);
        }

        return row is not null && (row.Focus() || row.IsKeyboardFocusWithin);
    }

    private static Landing FocusLandingItem(Selector selector)
    {
        if (selector.SelectedItem is { } selected && selector.Items.Contains(selected))
        {
            return RealizedContainer(selector, selected) is not { } container
                ? Landing.Unrealized
                : container.Focus() ? Landing.Landed : Landing.Refused;
        }

        int searched = 0;
        foreach (object item in selector.Items)
        {
            if (++searched > FirstRowSearchLimit)
            {
                return Landing.Refused;
            }

            if (RealizedContainer(selector, item) is not { } container)
            {
                return Landing.Unrealized;
            }

            if (container.Focus())
            {
                return Landing.Landed;
            }
        }

        return Landing.Refused;
    }

    private static UIElement? RealizedContainer(Selector selector, object item)
    {
        if (Container(selector, item) is { } realized)
        {
            return realized;
        }

        if (selector.ItemContainerGenerator.Status != GeneratorStatus.ContainersGenerated)
        {
            selector.UpdateLayout();
            if (Container(selector, item) is { } generated)
            {
                return generated;
            }
        }

        if (selector.ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated)
        {
            (selector as ListBox)?.ScrollIntoView(item);
            selector.UpdateLayout();
        }

        return Container(selector, item);
    }

    private static UIElement? Container(Selector selector, object item) =>
        selector.ItemContainerGenerator.ContainerFromItem(item) as UIElement;

    /// <summary>The realized selected row at any depth, searching only rows
    /// that are shown.</summary>
    private static TreeViewItem? SelectedTreeRow(ItemsControl level)
    {
        foreach (object item in level.Items)
        {
            if (level.ItemContainerGenerator.ContainerFromItem(item) is not TreeViewItem row)
            {
                continue;
            }

            if (row.IsSelected)
            {
                return row;
            }

            if (row.IsExpanded && SelectedTreeRow(row) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }

    /// <summary>The row at the end of <paramref name="path"/>, realized level
    /// by level; null when a level will not realize it. A row under a
    /// collapsed row is not shown, so it cannot take the keys either.</summary>
    private static TreeViewItem? RealizedTreeRow(TreeView tree, IReadOnlyList<object> path)
    {
        ItemsControl level = tree;
        TreeViewItem? row = null;
        foreach (object item in path)
        {
            row = RealizedTreeRow(level, item);
            if (row is null)
            {
                return null;
            }

            level = row;
        }

        return row;
    }

    private static TreeViewItem? RealizedTreeRow(ItemsControl level, object item)
    {
        if (level.ItemContainerGenerator.ContainerFromItem(item) is TreeViewItem realized)
        {
            return realized;
        }

        level.UpdateLayout();
        if (level.ItemContainerGenerator.ContainerFromItem(item) is TreeViewItem laidOut)
        {
            return laidOut;
        }

        // A virtualizing level realizes an index it is asked to bring into
        // view; a tree has no ScrollIntoView of its own.
        int index = level.Items.IndexOf(item);
        if (index < 0 || ItemsHost(level) is not VirtualizingPanel panel)
        {
            return null;
        }

        panel.BringIndexIntoViewPublic(index);
        level.UpdateLayout();
        return level.ItemContainerGenerator.ContainerFromItem(item) as TreeViewItem;
    }

    /// <summary>The panel that hosts <paramref name="level"/>'s own rows —
    /// never a nested row's.</summary>
    private static VirtualizingPanel? ItemsHost(ItemsControl level)
    {
        var pending = new Queue<DependencyObject>();
        pending.Enqueue(level);
        while (pending.Count > 0)
        {
            DependencyObject current = pending.Dequeue();
            for (int index = 0; index < VisualTreeHelper.GetChildrenCount(current); index++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(current, index);
                if (child is VirtualizingPanel panel && ReferenceEquals(ItemsControl.GetItemsOwner(panel), level))
                {
                    return panel;
                }

                if (child is not TreeViewItem)
                {
                    pending.Enqueue(child);
                }
            }
        }

        return null;
    }
}
