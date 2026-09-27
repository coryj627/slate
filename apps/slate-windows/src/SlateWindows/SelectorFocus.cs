// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SlateWindows.Grids;

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
/// <para>
/// A stop is kept through the publications under it
/// (<see cref="KeepKeysThroughPublications"/>): the rows that fill an empty
/// list holding the keys, or the notice that stops being a stop, never
/// leave them on the bare list or strand them (codex round 5).
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

    /// <summary>The containers whose region owns their landing
    /// (<see cref="SetOwnLanding"/>).</summary>
    private static readonly ConditionalWeakTable<UIElement, Func<bool>> OwnLandings = new();

    /// <summary>
    /// W7-7 PR 4 (#1247, R-5 as the owner amended it): a click on the EMPTY
    /// area of a populated list or tree — below its last row, beside a row's
    /// header — puts the keys on a row, as a click inside a Win32 list does.
    /// WPF gives them to nothing there (measured: a ListBox, a DataGrid and a
    /// TreeView all leave the keys where they were), so a pointer user who
    /// clicked into a list was not in it. The landing is the container's —
    /// its own, else its current or first row — and selects nothing. A click
    /// on a row, or on the scroll bar, is left to the row and the bar. The
    /// window registers the rule as it is built; the class handlers are
    /// process-wide, registered once.
    /// </summary>
    internal static void RegisterClickRule()
    {
        if (_clickRuleRegistered)
        {
            return;
        }

        _clickRuleRegistered = true;
        EventManager.RegisterClassHandler(
            typeof(ListBox), Mouse.MouseDownEvent, new MouseButtonEventHandler(ClickedEmptyArea), handledEventsToo: true);
        EventManager.RegisterClassHandler(
            typeof(TreeView), Mouse.MouseDownEvent, new MouseButtonEventHandler(ClickedEmptyArea), handledEventsToo: true);
    }

    private static bool _clickRuleRegistered;

    private static void ClickedEmptyArea(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left
            || sender is not ItemsControl { HasItems: true, IsVisible: true, IsEnabled: true, IsKeyboardFocusWithin: false } container
            || (container is Selector && !IsListLanding(container))
            || e.OriginalSource is not DependencyObject source
            || InRowOrScrollBar(source, container))
        {
            return;
        }

        if (!LandOnStop(container))
        {
            // No row could take them (none realized yet): the keys stay
            // where they were — a click owes no stable stop of its own, and
            // the bare list is never one (R-5).
        }
    }

    /// <summary>Whether <paramref name="source"/> lies in one of
    /// <paramref name="container"/>'s rows or in a scroll bar.</summary>
    private static bool InRowOrScrollBar(DependencyObject source, ItemsControl container)
    {
        for (DependencyObject? current = source;
            current is not null && !ReferenceEquals(current, container);
            current = current is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current))
        {
            if (current is ScrollBar or ListBoxItem or TreeViewItem)
            {
                return true;
            }
        }

        return false;
    }

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
                // A notice that is no longer a stop is passed over even
                // while it still holds the keys: its own Focus() answers
                // true then, and the keys would stay on it for WPF's
                // re-evaluation to strand (codex round 5).
                if (notice is { IsVisible: true, Focusable: true } && notice.Focus())
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

    /// <summary>
    /// Give <paramref name="container"/> its region's own landing, which
    /// <see cref="LandOnStop"/> then takes wherever the container is the
    /// target — a focus restore's token included. The Files tree is one:
    /// its landing is the selected file's row, else its first row
    /// unselected, which opens nothing — never the bare tree (R-5)
    /// (<c>MainWindow.LandOnFilesTree</c>; the owner's focus-without-select).
    /// </summary>
    internal static void SetOwnLanding(UIElement container, Func<bool> land) =>
        OwnLandings.AddOrUpdate(container, land);

    /// <summary>Whether <paramref name="container"/>'s region owns its
    /// landing (<c>SelectorLandingCensus</c> holds every
    /// selection-committing container to it).</summary>
    internal static bool HasOwnLanding(UIElement container) => OwnLandings.TryGetValue(container, out _);

    /// <summary>Whether focusing <paramref name="element"/> itself would
    /// land on a bare list. A combo box is its own stop — its items live in
    /// its drop-down — and a grid's stop is a cell, which the grid seats
    /// (AccessibleDataGrid), not a row container.</summary>
    internal static bool IsListLanding(UIElement element) =>
        element is Selector and not ComboBox and not DataGrid;

    /// <summary>
    /// The ONE landing for a target typed only as an element — a leaf's
    /// first stop (the region ring's right-pane content landing, a leaf
    /// reveal's) and every focus RESTORE, whose token was captured as an
    /// <c>IInputElement</c>: a container whose region owns its landing
    /// lands through it (<see cref="SetOwnLanding"/>), a list on its row, a
    /// tree on its row, a grid on a cell through its
    /// <see cref="AccessibleDataGrid"/> (the one implementation of cell
    /// focus, W4-5 D-12) and silently, a plain items host inside an item,
    /// and anything else on itself. A grid no AccessibleDataGrid owns is not
    /// landed on: nothing keeps its arrows, so the caller's own stable stop
    /// takes the keys.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Codex round 4: a restore token captured while a list was EMPTY —
    /// its own stop then (AR-6) — was restored with <c>Focus()</c> after
    /// the list filled behind an overlay, onto the populated bare list
    /// (the F4 shape). A non-container is judged by where the keys END UP
    /// (W7-6 #1240): a stop that hands them on to an item or a cell of its
    /// own answers false from its own <c>Focus()</c> though the keys are
    /// inside it, and a caller that falls back on false would take them
    /// away again. <c>SelectorLandingCensus</c> holds every focus call on
    /// an element-typed target in the shell to this.
    /// </para>
    /// <para>
    /// A ROW token — a tree row, a list row, a grid's cell or row — whose
    /// container has an own landing lands through that landing, never on
    /// the row itself (the owner's S4; the completeness sweep's G6 and
    /// G16). Every selection-committing container has one: a row takes the
    /// keys by selecting itself (a tree row) or moving the grid's currency,
    /// and the commit follows — a palette restore onto a Tags row re-applied
    /// the tag the user had just cleared, a recycled Files row opened
    /// another note, and a canvas table cell moved and narrated the seat.
    /// The container's landing puts the reader on its own current row, as
    /// every other landing does.
    /// </para>
    /// </remarks>
    /// <returns>Whether the keys landed on the target or inside it.</returns>
    internal static bool LandOnStop(UIElement stop) =>
        OwnLandings.TryGetValue(stop, out Func<bool>? own)
        || (RowOwner(stop) is { } container && OwnLandings.TryGetValue(container, out own))
        ? own!()
        : stop switch
        {
            Selector list when IsListLanding(list) => FocusFirstOrSelectedItem(list),
            TreeView tree => FocusSelectedOrFirstRow(tree),
            DataGrid grid => AccessibleDataGrid.Owning(grid) is { } owner && owner.FocusCurrentOrFirstCell(),
            ItemsControl items when IsPlainItemsLanding(items) => LandInsideItems(items),
            RadioButton radio when RadioGroupArrows.CheckedPeer(radio) is { } chosen => chosen.Focus(),
            _ => stop.Focus() || stop.IsKeyboardFocusWithin,
        };

    /// <summary>
    /// W7-7 PR 4 (#1247; codex PR 4 round 7's findings 1, 2 and 4 — the
    /// owner's structural rule): the row a KEYBOARD action, or a menu the
    /// keyboard opened, acts on is the row that holds the keys — the row the
    /// reader hears — and the container's selection only when the keys are
    /// on no row of it.
    /// </summary>
    /// <remarks>
    /// A landing focuses a row without selecting it (the owner's
    /// focus-without-select, OD-11b), so the row the reader is on and the
    /// container's <c>SelectedItem</c> can part: Enter on a landed Backlinks
    /// row did nothing, Space on a landed task only selected it, Enter on a
    /// Bases list row the quick filter's Escape landed on opened nothing, and
    /// Connections' Enter and Shift+F10 acted on a selection hidden under a
    /// collapsed row. Every keyboard and keyboard-menu handler over a landing
    /// container resolves its row here (<c>FocusedRowCensus</c>).
    /// </remarks>
    internal static object? FocusedOrSelectedItem(ItemsControl container) =>
        FocusedItem(container) ?? container switch
        {
            Selector list => list.SelectedItem,
            TreeView tree => tree.SelectedItem,
            _ => null,
        };

    /// <summary>The item of <paramref name="container"/>'s row that holds
    /// the keys — the focused row, or the row the focused element sits in —
    /// else null.</summary>
    internal static object? FocusedItem(ItemsControl container) =>
        container.IsKeyboardFocusWithin && Keyboard.FocusedElement is DependencyObject focused
            ? ItemAt(container, focused)
            : null;

    /// <summary>
    /// W7-7 PR 4 (#1247; codex PR 4's final check): the item of the row a
    /// POINTER gesture hit — <paramref name="originalSource"/> lies in it —
    /// else null. A pointer activation acts on the row it hit, never on the
    /// keyboard's fallback: a double-click on a list's empty area first
    /// LANDS the keys on a row (the click rule, OD-11c), and resolving the
    /// second press through <see cref="FocusedOrSelectedItem"/> opened that
    /// row — one never clicked.
    /// </summary>
    internal static object? ClickedItem(ItemsControl container, object? originalSource) =>
        originalSource is DependencyObject source ? ItemAt(container, source) : null;

    /// <summary>The item of <paramref name="container"/>'s row that
    /// <paramref name="element"/> lies in, else null.</summary>
    private static object? ItemAt(ItemsControl container, DependencyObject element)
    {
        for (DependencyObject? current = element;
            current is not null && !ReferenceEquals(current, container);
            current = current is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(current) ?? LogicalTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current))
        {
            if (current is (ListBoxItem or TreeViewItem) and UIElement row
                && ReferenceEquals(RowOwner(row), container)
                && ItemsControl.ItemsControlFromItemContainer(row) is { } level)
            {
                object item = level.ItemContainerGenerator.ItemFromContainer(row);
                return ReferenceEquals(item, DependencyProperty.UnsetValue)
                    ? (row as FrameworkElement)?.DataContext
                    : item;
            }
        }

        return null;
    }

    /// <summary>The container a row token belongs to: a tree row's tree, a
    /// list row's list, a grid cell's or row's grid; null for anything
    /// else.</summary>
    private static UIElement? RowOwner(UIElement stop)
    {
        switch (stop)
        {
            case TreeViewItem row:
                ItemsControl? level = ItemsControl.ItemsControlFromItemContainer(row);
                while (level is TreeViewItem parent)
                {
                    level = ItemsControl.ItemsControlFromItemContainer(parent);
                }

                return level as TreeView;
            case ListBoxItem row:
                return ItemsControl.ItemsControlFromItemContainer(row);
            case DataGridCell or DataGridRow:
                for (DependencyObject? current = stop; current is not null; current = VisualTreeHelper.GetParent(current))
                {
                    if (current is DataGrid grid)
                    {
                        return grid;
                    }
                }

                return null;
            default:
                return null;
        }
    }

    /// <summary>Whether <paramref name="items"/> is an items host with no
    /// landing of its own — R-5's "other <c>ItemsControl</c> container": a
    /// plain host such as the split editor panes. A row (a tree row, a menu
    /// item: <see cref="HeaderedItemsControl"/>), a list, a grid, a combo
    /// box, a status bar and a menu each have theirs.</summary>
    internal static bool IsPlainItemsLanding(ItemsControl items) =>
        items is not (HeaderedItemsControl or Selector or TreeView or StatusBar or MenuBase);

    /// <summary>A plain items host's landing is INSIDE an item — its first
    /// focusable element, itself landed through <see cref="LandOnStop"/>;
    /// an EMPTY host is its own stop (AR-6); a populated host whose items
    /// take no keys is not landed on, and the caller's stable stop
    /// is.</summary>
    private static bool LandInsideItems(ItemsControl items)
    {
        if (!items.HasItems)
        {
            return items.Focus() || items.IsKeyboardFocusWithin;
        }

        items.UpdateLayout();
        return FirstFocusableWithin(items) is { } inner && LandOnStop(inner);
    }

    private static UIElement? FirstFocusableWithin(DependencyObject root)
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);
            if (child is UIElement { Focusable: true, IsEnabled: true, IsVisible: true } element)
            {
                return element;
            }

            if (FirstFocusableWithin(child) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }

    /// <summary>
    /// W7-7 PR 4 (#1247, R-5, spec §5.2.2; codex round 5): keep the keys on
    /// a real stop while a region's rows are published under them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An empty list is its own stop while its rows load (AR-6), and WPF
    /// hands the keys of a removed row to the bare list: a publication that
    /// filled the list left them on the bare POPULATED list, from which an
    /// arrow leaves the region. A notice that stops being a stop — its
    /// sentence turns to "Loading…", or it collapses — had its keys stranded
    /// by WPF's own focus re-evaluation.
    /// </para>
    /// <para>
    /// The Citations leaf's publish restore is the precedent
    /// (<c>MainWindow.RestoreCitationFocus</c>). After a publication — once
    /// however many rows it adds, at Loaded priority: after layout, and
    /// ahead of WPF's own re-evaluation of a focused element that went away,
    /// which runs at Input and would move the keys up to the bare list or
    /// the window first — and only while the keys are still on the scope's
    /// bare container or stranded from the scope (on nothing, the window, a
    /// detached, hidden or unfocusable element, or an ancestor WPF moved
    /// them up to): a bare POPULATED list re-lands on its row (the selected
    /// one, else the first); a bare EMPTY list on its notice once that is a
    /// stop (§5.2.2); stranded keys on the rows they were last in; any other
    /// container, or keys that were on none, on the scope's own landing. A
    /// notice that stops being a stop while it holds the keys hands them to
    /// its list first (Normal priority), so they stay in the region. Silent
    /// apart from the focus change itself, and keys held anywhere else are
    /// never taken.
    /// </para>
    /// </remarks>
    /// <param name="scope">Where the keys count as being in the region: a
    /// leaf body, a grid.</param>
    /// <param name="rows">The containers whose publications re-land.</param>
    /// <param name="notices">The scope's empty-state notices.</param>
    /// <param name="landInScope">The scope's own landing.</param>
    /// <param name="reLandPublications">False where the region restores
    /// its own publications (the Citations leaf): only the notices' hand-off
    /// is kept.</param>
    internal static void KeepKeysThroughPublications(
        UIElement scope,
        IReadOnlyList<ItemsControl> rows,
        IReadOnlyList<UIElement> notices,
        Func<bool> landInScope,
        bool reLandPublications = true) =>
        new PublicationKeeper(scope, rows, notices, landInScope, reLandPublications).Attach();

    private sealed class PublicationKeeper(
        UIElement scope,
        IReadOnlyList<ItemsControl> rows,
        IReadOnlyList<UIElement> notices,
        Func<bool> landInScope,
        bool reLandPublications)
    {
        /// <summary>The keys are in the scope, or were stranded from it — not
        /// taken since by a claim anywhere else.</summary>
        private bool _held;

        /// <summary>The rows container the keys were last in within the
        /// scope; null when they were on anything else there.</summary>
        private ItemsControl? _lastRows;

        private bool _pending;

        /// <summary>A removed row's hand-over was declined since the last
        /// publication: the next is let through, so a re-land that cannot
        /// take the keys never leaves them nowhere.</summary>
        private bool _declined;

        public void Attach()
        {
            scope.IsKeyboardFocusWithinChanged += (_, e) =>
                _held = (bool)e.NewValue || (_held && IsStranded(Keyboard.FocusedElement));
            scope.GotKeyboardFocus += (_, _) =>
                _lastRows = rows.FirstOrDefault(container => container.IsKeyboardFocusWithin);
            if (reLandPublications)
            {
                foreach (ItemsControl container in rows)
                {
                    ((INotifyCollectionChanged)container.Items).CollectionChanged += (_, _) =>
                    {
                        _declined = false;
                        if (container.HasItems)
                        {
                            ResolveLater();
                        }
                    };

                    // A row removed from the tree hands its keys to its
                    // container (ListBoxItem's own OnVisualParentChanged) —
                    // at the layout after a republish, when the container
                    // is POPULATED again: a UIA focus change on the bare
                    // list. The hand-over is declined, once, and the keys,
                    // still on the removed row, are re-landed on a row
                    // (stranded: the rows they were in) at Loaded.
                    container.PreviewGotKeyboardFocus += (_, e) =>
                    {
                        if (!_declined
                            && ReferenceEquals(e.NewFocus, container)
                            && container.HasItems
                            && e.OldFocus is Visual removed
                            && PresentationSource.FromVisual(removed) is null)
                        {
                            e.Handled = true;
                            _declined = true;
                            _held = true;
                            _lastRows = container;
                            ResolveLater();
                        }
                    };
                }
            }

            foreach (UIElement notice in notices)
            {
                notice.FocusableChanged += (_, _) => NoticeChanged(notice);
                notice.IsVisibleChanged += (_, _) => NoticeChanged(notice);
            }
        }

        private void NoticeChanged(UIElement notice)
        {
            if (notice.IsKeyboardFocused && !IsAStop(notice))
            {
                _ = scope.Dispatcher.InvokeAsync(
                    () =>
                    {
                        if (ReferenceEquals(Keyboard.FocusedElement, notice) && !IsAStop(notice) && !HandOff())
                        {
                            _ = landInScope();
                        }
                    },
                    DispatcherPriority.Normal);
                return;
            }

            if (reLandPublications && IsAStop(notice))
            {
                ResolveLater();
            }
        }

        private void ResolveLater()
        {
            if (!_held || _pending)
            {
                return;
            }

            _pending = true;
            _ = scope.Dispatcher.InvokeAsync(Resolve, DispatcherPriority.Loaded);
        }

        private void Resolve()
        {
            _pending = false;
            IInputElement? focused = Keyboard.FocusedElement;
            ItemsControl? bare = rows.FirstOrDefault(container => ReferenceEquals(container, focused));
            bool stranded = bare is null && _held && IsStranded(focused);
            if ((bare is null && !stranded) || !scope.IsVisible)
            {
                // The keys are on a real stop — or the scope is not on
                // screen (a projection switched away, a hidden leaf), where
                // a landing could take nothing and would only write state.
                return;
            }

            if ((bare ?? _lastRows) is Selector list && IsListLanding(list) && list.IsVisible)
            {
                // A populated list: its row. An empty one: its notice once
                // that is a stop; stranded keys come back to it even so.
                if ((list.HasItems || stranded || notices.Any(IsAStop))
                    && !FocusFirstOrSelectedItem(list, [.. notices]))
                {
                    _ = landInScope();
                }

                return;
            }

            if (stranded || bare!.HasItems)
            {
                _ = landInScope();
            }
        }

        /// <summary>A notice's keys go to its list: the row, another notice
        /// that is a stop, else the empty list itself (AR-6).</summary>
        private bool HandOff() =>
            rows.OfType<Selector>().FirstOrDefault(list => IsListLanding(list) && list.IsVisible) is { } list
            && FocusFirstOrSelectedItem(list, [.. notices]);

        private static bool IsAStop(UIElement notice) =>
            notice is { IsVisible: true, Focusable: true, IsEnabled: true };

        private bool IsStranded(IInputElement? focused) => focused switch
        {
            null or Window => true,
            UIElement element => PresentationSource.FromVisual(element) is null
                || !element.IsVisible
                || !element.Focusable
                || element.IsAncestorOf(scope),
            _ => false,
        };
    }

    /// <summary>
    /// W7-7 PR 4 (#1247, contract R-5 as the owner amended it; spec review
    /// round 23): a TREE's landing is a ROW — the selected one, else the
    /// first, focused UNSELECTED — and never the tree itself.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WPF hands a focused tree's keys to its selected container only when
    /// one exists. With no selection the bare tree keeps them: Up and Down
    /// then reach its first row, but Left and Right go to directional
    /// navigation and out of the region (<c>TreeLandingTests</c> measures
    /// both). A selected row the panel has not realized is brought into
    /// view level by level; one under a collapsed row is hidden, cannot take
    /// the keys, and is not a landing.
    /// </para>
    /// <para>
    /// With no selected row to land on — nothing selected, or the selection
    /// hidden — the landing is the first row that takes the keys, WITHOUT
    /// selecting it (<see cref="LandingTreeViewItem.FocusUnselected"/>): a
    /// selection in the shell's trees opens a note or applies a tag, and a
    /// landing does neither (the owner's focus-without-select, which
    /// replaced the Files region's filter-field fallback). The selection is
    /// never written here; the selected row, focused, selects itself only
    /// when its container had lost the selection, which re-states it. An
    /// EMPTY tree on show is its own stop (AR-6), as an empty list is.
    /// </para>
    /// </remarks>
    /// <param name="selectedPath">The selected row's items, root first, when
    /// the tree's own selection is not the source of truth (a recycled
    /// container drops it); null reads the tree's.</param>
    /// <returns>Whether a row — or the empty tree — took the keys now. False
    /// means nothing did, and the caller lands on its stable stop.</returns>
    internal static bool FocusSelectedOrFirstRow(TreeView tree, IReadOnlyList<object>? selectedPath = null)
    {
        ++_newestRequest;
        if (!tree.HasItems)
        {
            return tree.IsVisible && tree.Focus();
        }

        TreeViewItem? selectedRow = null;
        if (selectedPath is { Count: > 0 })
        {
            selectedRow = RealizedTreeRow(tree, selectedPath);
        }
        else if (tree.SelectedItem is { } selected)
        {
            selectedRow = SelectedTreeRow(tree)
                ?? (tree.Items.Contains(selected) ? RealizedTreeRow(tree, [selected]) : null);
        }

        if (selectedRow is not null && (selectedRow.Focus() || selectedRow.IsKeyboardFocusWithin))
        {
            return true;
        }

        return FocusFirstRowUnselected(tree);
    }

    /// <summary>The first root row that takes the keys, focused without
    /// selecting it; a row that cannot take them (disabled, hidden) is
    /// passed over for the next.</summary>
    private static bool FocusFirstRowUnselected(TreeView tree)
    {
        int searched = 0;
        foreach (object item in tree.Items)
        {
            if (++searched > FirstRowSearchLimit)
            {
                return false;
            }

            if (RealizedTreeRow(tree, [item]) is { IsEnabled: true } row && LandingTreeViewItem.FocusUnselected(row))
            {
                return true;
            }
        }

        return false;
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

    /// <summary>The row for <paramref name="item"/> in <paramref name="level"/>,
    /// realized if the level's panel will make it; null otherwise.</summary>
    internal static TreeViewItem? RealizedTreeRow(ItemsControl level, object item)
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
