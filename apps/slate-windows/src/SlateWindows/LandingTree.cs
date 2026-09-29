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
