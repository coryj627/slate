// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Specialized;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace SlateWindows;

/// <summary>
/// W7-7 PR 4 (#1247, contract R-5 as the owner amended it): a tree that
/// never holds the keys itself while it has rows. Every tree in the shell
/// is one (<c>SelectorLandingCensus</c>).
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="TreeView"/> is a Tab stop. WPF hands its keys on to the
/// selected row when there is one; with nothing selected the bare tree
/// kept them, and from there Left and Right went to directional
/// navigation and out of the region (codex PR 4 round 6; the repro's
/// "Tab rests on the bare Files tree"). Here, keys on their way to the
/// populated tree itself — Tab, a click on its empty area, a restore, UI
/// Automation's SetFocus, WPF's own re-evaluation — are declined, and the
/// tree's landing (<see cref="SelectorFocus.LandOnStop"/>) takes them in
/// the same focus change: its region's own landing when it has one, else
/// the selected row, else the first row UNSELECTED
/// (<see cref="LandingTreeViewItem"/>). The tree itself never takes them,
/// so neither UI Automation nor a keyboard-focus listener ever sees it
/// focused while it has rows. A landing that takes nothing lets the tree
/// have them after all, rather than leave them nowhere.
/// </para>
/// <para>
/// An EMPTY tree is its own stop (AR-6) and keeps them; rows published
/// under them land them on a row once the rows are laid out (the launch
/// landing, which may reach the Files tree before its first publication).
/// The subclass takes no implicit style of its own: where the shell's
/// XAML styles a tree, it names the <see cref="TreeView"/> style.
/// </para>
/// </remarks>
internal class LandingTreeView : TreeView
{
    /// <summary>A landing the hand-on itself made is not handed on
    /// again.</summary>
    [ThreadStatic]
    private static bool _handingOn;

    private bool _landingAfterPublication;

    static LandingTreeView() => RealizedRowHold.Register(typeof(LandingTreeView));

    protected override DependencyObject GetContainerForItemOverride() => new LandingTreeViewItem();

    protected override AutomationPeer OnCreateAutomationPeer() => new LandingTreeViewAutomationPeer(this);

    protected override void OnPreviewGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnPreviewGotKeyboardFocus(e);
        if (e.Handled || _handingOn || !ReferenceEquals(e.NewFocus, this) || !HasItems)
        {
            return;
        }

        _handingOn = true;
        try
        {
            e.Handled = SelectorFocus.LandOnStop(this);
        }
        finally
        {
            _handingOn = false;
        }
    }

    protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e)
    {
        base.OnItemsChanged(e);
        RealizedRowHold.HoldThroughTheNextLayout(this, e);
        if (!IsKeyboardFocused || !HasItems || _landingAfterPublication)
        {
            return;
        }

        _landingAfterPublication = true;
        _ = Dispatcher.InvokeAsync(
            () =>
            {
                _landingAfterPublication = false;
                if (IsKeyboardFocused && HasItems && !SelectorFocus.LandOnStop(this))
                {
                    // No row could take them yet: once more, when the rows
                    // have been realized.
                    SelectorFocus.SeatLater(this, () => HasItems && SelectorFocus.LandOnStop(this));
                }
            },
            DispatcherPriority.Loaded);
    }
}

/// <summary>
/// A landing tree's peer: UI Automation's SetFocus on the tree succeeds
/// when the keys land INSIDE it.
/// </summary>
/// <remarks>
/// WPF's peer throws <see cref="InvalidOperationException"/> whenever the
/// tree's own <c>Focus()</c> answers false — and it answers false whenever
/// the tree hands its keys on to a row: WPF's own hand-on to a selected row
/// (the quirk the journeys worked around), and every landing tree's
/// hand-on. SetFocus lands the tree as every landing does, and succeeds
/// when the keys are in it.
/// </remarks>
internal class LandingTreeViewAutomationPeer(TreeView owner) : TreeViewAutomationPeer(owner)
{
    protected override void SetFocusCore()
    {
        if (!SelectorFocus.LandOnStop(Owner))
        {
            throw new InvalidOperationException("The tree could not take the keyboard focus.");
        }
    }
}

/// <summary>
/// W7-7 PR 4 (#1247, R-5 as amended; the owner's focus-without-select): a
/// tree row that can take the keys WITHOUT selecting itself.
/// </summary>
/// <remarks>
/// <para>
/// WPF's <see cref="TreeViewItem"/> selects itself whenever it takes focus
/// (<c>OnGotFocus</c> calls <c>Select(true)</c>), and a selection in the
/// shell's trees does something: a Files row OPENS its note (R-2, OD-2), a
/// Tags row applies its tag filter (R-3). A landing on the first row of a
/// tree with nothing selected therefore used to open or filter, which is
/// why the Files landing fell back to the filter field (codex round 5).
/// The owner's decision replaces that fallback: the landing focuses the
/// first row UNSELECTED — the state a Win32 tree shows before its first
/// arrow — and opens, filters and says nothing. An arrow, a click, Enter
/// or Space acts from there exactly as before; only a landing
/// (<see cref="FocusUnselected"/>) skips the self-selection.
/// </para>
/// <para>
/// The skip keeps the rest of the focus change: FrameworkElement brings
/// the focused row into view, and UIElement raises the routed
/// <c>GotFocus</c> — the two halves of the base chain below
/// <see cref="TreeViewItem"/> (measured: <c>TreeViewItem.OnGotFocus</c> is
/// <c>Select(true)</c> then <c>FrameworkElement.OnGotFocus</c>, which is
/// the bring-into-view then <c>UIElement.OnGotFocus</c>, which raises the
/// event).
/// </para>
/// <para>
/// WPF moves Up and Down from a row only while it is SELECTED
/// (<c>TreeViewItem.AllowHandleKeyEvent</c>), and the tree's own fallback
/// focuses its first row — the row the keys are already on — so from an
/// unselected row both arrows did nothing (measured). The row takes them
/// itself: Down focuses the next row the tree shows and Up the previous,
/// each selecting itself as it takes the keys, exactly as an arrow from a
/// selected row does; at the tree's edge, where there is no row further,
/// the arrow selects the row it is on. An arrow is never dead.
/// </para>
/// </remarks>
internal class LandingTreeViewItem : TreeViewItem
{
    /// <summary>The row a landing is focusing unselected, for the length
    /// of its <c>Focus()</c> call.</summary>
    [ThreadStatic]
    private static TreeViewItem? _focusingUnselected;

    static LandingTreeViewItem() => RealizedRowHold.Register(typeof(LandingTreeViewItem));

    /// <summary>
    /// Focus <paramref name="row"/> without selecting it — a landing's
    /// focus. A row that is not a <see cref="LandingTreeViewItem"/> (a tree
    /// outside the shell) selects itself as WPF's rows do.
    /// </summary>
    /// <returns>Whether the keys are on the row or inside it.</returns>
    internal static bool FocusUnselected(TreeViewItem row)
    {
        TreeViewItem? outer = _focusingUnselected;
        _focusingUnselected = row;
        try
        {
            return row.Focus() || row.IsKeyboardFocusWithin;
        }
        finally
        {
            _focusingUnselected = outer;
        }
    }

    protected override DependencyObject GetContainerForItemOverride() => new LandingTreeViewItem();

    protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e)
    {
        base.OnItemsChanged(e);
        RealizedRowHold.HoldThroughTheNextLayout(this, e);
    }

    protected override void OnGotFocus(RoutedEventArgs e)
    {
        if (!ReferenceEquals(_focusingUnselected, this))
        {
            base.OnGotFocus(e);
            return;
        }

        if (IsKeyboardFocused)
        {
            BringIntoView();
        }

        RaiseEvent(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled
            || IsSelected
            || !IsKeyboardFocused
            || e.KeyboardDevice.Modifiers != ModifierKeys.None
            || e.Key is not (Key.Up or Key.Down))
        {
            return;
        }

        TreeViewItem? next = e.Key == Key.Down ? NextRow(this) : PreviousRow(this);
        e.Handled = next is not null && next.Focus();
        if (!e.Handled)
        {
            SetCurrentValue(IsSelectedProperty, true);
            e.Handled = true;
        }
    }

    /// <summary>The row the tree shows below <paramref name="row"/>: its
    /// first child when it is expanded, else the next sibling of it or of
    /// the nearest ancestor that has one.</summary>
    private static TreeViewItem? NextRow(TreeViewItem row)
    {
        if (row is { IsExpanded: true, HasItems: true } && SelectorFocus.RealizedTreeRow(row, row.Items[0]) is { } child)
        {
            return child;
        }

        for (TreeViewItem? current = row; current is not null; current = ParentRow(current))
        {
            if (ItemsControl.ItemsControlFromItemContainer(current) is { } owner
                && owner.ItemContainerGenerator.IndexFromContainer(current) is int index and >= 0
                && index + 1 < owner.Items.Count)
            {
                return SelectorFocus.RealizedTreeRow(owner, owner.Items[index + 1]);
            }
        }

        return null;
    }

    /// <summary>The row the tree shows above <paramref name="row"/>: the
    /// last shown row under its previous sibling, else its parent.</summary>
    private static TreeViewItem? PreviousRow(TreeViewItem row)
    {
        if (ItemsControl.ItemsControlFromItemContainer(row) is not { } owner
            || owner.ItemContainerGenerator.IndexFromContainer(row) is not (int index and >= 0))
        {
            return null;
        }

        if (index == 0)
        {
            return owner as TreeViewItem;
        }

        TreeViewItem? previous = SelectorFocus.RealizedTreeRow(owner, owner.Items[index - 1]);
        while (previous is { IsExpanded: true, HasItems: true }
            && SelectorFocus.RealizedTreeRow(previous, previous.Items[^1]) is { } last)
        {
            previous = last;
        }

        return previous;
    }

    private static TreeViewItem? ParentRow(TreeViewItem row) =>
        ItemsControl.ItemsControlFromItemContainer(row) as TreeViewItem;
}

/// <summary>
/// #1515: a landing tree, or one of its rows, keeps every container its
/// panel has realized through the first layout pass after its own rows
/// change, so a row a publication kept keeps its container — and the rows
/// under a kept open folder keep theirs, and their UI Automation elements.
/// </summary>
/// <remarks>
/// <para>
/// A recycling <see cref="VirtualizingStackPanel"/> recycles, BEFORE it
/// generates and measures, every realized container outside the range its
/// previous pass covered: its first item and that pass's count of items, kept
/// as the estimate (dotnet/wpf <c>MeasureOverrideImpl</c> calls
/// <c>CleanupContainers</c> with <c>_itemsInExtendedViewportCount</c>). A row
/// inserted above the rows it realized pushes the last of them out of that
/// stale range, so the panel recycles them and realizes them again at once,
/// in other containers. A row whose container changed keeps its node and, at
/// the root, its element, which the tree's peer keys by item, but an open
/// folder's rows belong to its container's peer, so every row under a
/// recycled open folder got a new element. 20b's scratch run of 2026-10-19 measured that
/// (<c>ScratchNestedRowProbe</c>, arm <c>insert-first</c>: the last root row,
/// an open folder, came back in a new container, and its note with a new
/// element). A removal, or a row inserted after the last realized one, leaves
/// the range whole.
/// </para>
/// <para>
/// So a change to a landing tree's or a row's own rows — an Add, Remove, Move
/// or Replace; a Reset regenerates every container anyway — holds that items
/// control's realized containers through its next layout pass. The panel asks
/// the items control before it recycles one (<c>CleanUpVirtualizedItem</c>,
/// raised on the tree or on the row whose rows those are), and a holding one
/// declines. The panel cleans up with its new range on the pass after; a
/// container then outside the viewport stays one pass longer, as the one the
/// keys are in always does (the panel never recycles it). The hold ends at
/// <see cref="DispatcherPriority.Loaded"/>, after the layout the change
/// queued.
/// </para>
/// </remarks>
internal static class RealizedRowHold
{
    private static readonly DependencyProperty IsHoldingProperty = DependencyProperty.RegisterAttached(
        "IsHolding", typeof(bool), typeof(RealizedRowHold), new PropertyMetadata(false));

    /// <summary>Have <paramref name="owner"/>'s instances decline their
    /// panel's cleanup while they hold.</summary>
    internal static void Register(Type owner) =>
        EventManager.RegisterClassHandler(
            owner,
            VirtualizingStackPanel.CleanUpVirtualizedItemEvent,
            new CleanUpVirtualizedItemEventHandler(OnCleanUpVirtualizedItem));

    /// <summary><paramref name="owner"/>'s own rows changed: hold its
    /// realized containers through the layout pass the change
    /// queued.</summary>
    internal static void HoldThroughTheNextLayout(ItemsControl owner, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset || (bool)owner.GetValue(IsHoldingProperty))
        {
            return;
        }

        owner.SetValue(IsHoldingProperty, true);
        _ = owner.Dispatcher.InvokeAsync(() => owner.ClearValue(IsHoldingProperty), DispatcherPriority.Loaded);
    }

    private static void OnCleanUpVirtualizedItem(object sender, CleanUpVirtualizedItemEventArgs e)
    {
        if (sender is DependencyObject owner && (bool)owner.GetValue(IsHoldingProperty))
        {
            e.Cancel = true;
        }
    }
}
