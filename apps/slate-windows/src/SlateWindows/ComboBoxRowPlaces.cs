// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace SlateWindows;

/// <summary>
/// #1783, #1836: WPF publishes an open combo box's rows twice. Under the combo
/// box each row is WPF's item peer, which works its place out from the items:
/// the item's index plus one, of the item count. Under the open list's own
/// window each row is the row container's own peer, which knows a place only
/// when one is set on the container, so a client that walks or hit-tests the
/// popup read every row as having none. One tracker for each combo box keeps
/// <see cref="AutomationProperties.PositionInSetProperty"/> and
/// <see cref="AutomationProperties.SizeOfSetProperty"/> on every realized row
/// equal to what WPF's item peer says without them, so both copies say the
/// same, and it keeps them only while the list is open, which is the only time
/// the popup's copies exist. When the list closes (DropDownClosed, which WPF
/// raises after IsDropDownOpen has turned false; until then the stamps are kept
/// current) it clears them, so a closed
/// combo box reports exactly what it reported with no tracker, and a list
/// whose source is refilled while closed costs no pass and no write. A stamp
/// that went stale would make the item peer, which a screen reader reads and
/// which is right with no stamp, wrong: so while the list is open every event
/// that can move a row or change the count restamps every realized row, and
/// where WPF's answer is not "index plus one, of the count" (a grouped list,
/// whose places are within each group, or rows that are recycled) the tracker
/// removes its stamps and leaves WPF's answer.
/// </summary>
internal sealed class ComboBoxRowPlaces
{
    private static readonly ConditionalWeakTable<ComboBox, ComboBoxRowPlaces> Trackers = new();
    private static int s_registered;

    private readonly WeakReference<ComboBox> _combo;

    /// <summary>The containers this tracker wrote to, so it can clear them when
    /// the list closes, and clear one the generator let go of.</summary>
    private readonly HashSet<DependencyObject> _stamped = new(ReferenceEqualityComparer.Instance);

    private readonly EventHandler _opened;
    private readonly EventHandler _closed;
    private readonly EventHandler _generated;
    private readonly NotifyCollectionChangedEventHandler _changed;

    /// <summary>It stamps when the list opens, and while it is open whenever the
    /// generator makes or drops rows (a first open, a new items host after a
    /// theme change) and whenever the items change, until DropDownClosed; it
    /// clears at DropDownClosed. A tracker that cannot attach (anything thrown while it
    /// subscribes or runs its first pass) takes its subscriptions back and
    /// stands down for good: the combo box keeps WPF's own answers, as it did
    /// with no tracker, and the table holds the failed tracker, so it is not
    /// tried again at every SizeChanged.</summary>
    private ComboBoxRowPlaces(ComboBox combo)
    {
        _combo = new WeakReference<ComboBox>(combo);
        _opened = (_, _) => Restamp();
        _closed = (_, _) => ClearStamps();
        _generated = (_, _) => Restamp();
        _changed = (_, _) => Restamp();
        try
        {
            combo.DropDownOpened += _opened;
            combo.DropDownClosed += _closed;
            combo.ItemContainerGenerator.StatusChanged += _generated;
            ((INotifyCollectionChanged)combo.Items).CollectionChanged += _changed;
            Restamp();
        }
        catch (Exception)
        {
            combo.DropDownOpened -= _opened;
            combo.DropDownClosed -= _closed;
            combo.ItemContainerGenerator.StatusChanged -= _generated;
            ((INotifyCollectionChanged)combo.Items).CollectionChanged -= _changed;
            ClearStamps();
            StoodDown = true;
        }
    }

    /// <summary>
    /// Attaches a tracker to every combo box in the process, with no edit at
    /// any combo box: a class handler on <see cref="FrameworkElement.SizeChangedEvent"/>,
    /// which every combo box raises when it is first laid out, before its list
    /// can open; and one on its rows, which a row raises when it is first laid
    /// out inside the open, for a combo box that has no size and so never
    /// raises its own. Registered once in a process; the window calls it as it
    /// is built, and a test that builds a combo box with no window calls it
    /// itself.
    /// </summary>
    internal static void Register()
    {
        if (Interlocked.Exchange(ref s_registered, 1) == 1)
        {
            return;
        }

        EventManager.RegisterClassHandler(
            typeof(ComboBox),
            FrameworkElement.SizeChangedEvent,
            new SizeChangedEventHandler(static (sender, _) => Attach((ComboBox)sender)));
        EventManager.RegisterClassHandler(
            typeof(ComboBoxItem),
            FrameworkElement.SizeChangedEvent,
            new SizeChangedEventHandler(static (sender, args) =>
            {
                if (ItemsControl.ItemsControlFromItemContainer((ComboBoxItem)sender) is ComboBox combo)
                {
                    _ = Attach(combo);
                }
            }));
    }

    /// <summary>The combo box's tracker, made and run the first time it is asked for.</summary>
    internal static ComboBoxRowPlaces Attach(ComboBox combo) =>
        Trackers.GetValue(combo, static key => new ComboBoxRowPlaces(key));

    /// <summary>The combo box's tracker, if it has one, for the facts.</summary>
    internal static ComboBoxRowPlaces? For(ComboBox combo) =>
        Trackers.TryGetValue(combo, out ComboBoxRowPlaces? tracker) ? tracker : null;

    /// <summary>Whether this tracker failed to attach and stood down.</summary>
    internal bool StoodDown { get; private set; }

    /// <summary>For the facts: how many passes over the items have run.</summary>
    internal int Passes { get; private set; }

    /// <summary>For the facts: how many values the passes have written.</summary>
    internal int Writes { get; private set; }

    /// <summary>For the facts: how many containers hold a stamp of this tracker's.</summary>
    internal int StampedCount => _stamped.Count;

    /// <summary>Whether WPF's own answer is something other than "index plus
    /// one, of the count": a grouped list (each row's place is within its
    /// group), or rows that are recycled (a container changes its item with no
    /// event this tracker hears).</summary>
    internal static bool LeavesItToWpf(ComboBox combo) => combo.IsGrouping || Recycles(combo);

    private static bool Recycles(ComboBox combo)
    {
        if (VirtualizingPanel.GetVirtualizationMode(combo) != VirtualizationMode.Recycling
            || !VirtualizingPanel.GetIsVirtualizing(combo))
        {
            return false;
        }

        ItemContainerGenerator generator = combo.ItemContainerGenerator;
        for (int index = 0; index < combo.Items.Count; index++)
        {
            if (generator.ContainerFromIndex(index) is DependencyObject container)
            {
                return VisualTreeHelper.GetParent(container) is VirtualizingPanel;
            }
        }

        return false;
    }

    /// <summary>
    /// One pass over the items, while the list is open and until DropDownClosed
    /// has cleared the stamps. WPF turns IsDropDownOpen false before it raises
    /// DropDownClosed, which comes later from the dispatcher, so in between the
    /// rows still hold stamps and a change to the items must still restamp
    /// them (Codex round 1). Once they are cleared and the list is closed, it
    /// does nothing: no pass, no map. A container the generator no longer owns
    /// loses both values; under grouping or recycling every realized row loses
    /// them; otherwise every realized row gets its item's first index plus one
    /// (two equal items both get the first one's place, as WPF's IndexOf gives
    /// them), of the count, written only where it differs. It runs from class
    /// and instance handlers inside layout, so it catches the
    /// <see cref="InvalidOperationException"/> a view held in DeferRefresh gives
    /// for Count and IndexOf (nothing else is caught), and the stamps then stay
    /// as they were until the next event that runs the pass: the list opening,
    /// the generator making or dropping rows, or a change to the items. A
    /// deferral that changed no sort, filter or grouping ends with no Reset, so
    /// it reruns nothing by itself.
    /// </summary>
    internal void Restamp()
    {
        if (StoodDown || !_combo.TryGetTarget(out ComboBox? combo) || (!combo.IsDropDownOpen && _stamped.Count == 0))
        {
            return;
        }

        ItemContainerGenerator generator = combo.ItemContainerGenerator;
        Passes++;
        try
        {
            if (_stamped.Count > 0)
            {
                List<DependencyObject>? released = null;
                foreach (DependencyObject container in _stamped)
                {
                    if (generator.ItemFromContainer(container) == DependencyProperty.UnsetValue)
                    {
                        (released ??= []).Add(container);
                    }
                }

                foreach (DependencyObject container in released ?? [])
                {
                    Clear(container);
                    _ = _stamped.Remove(container);
                }
            }

            ItemCollection items = combo.Items;
            int count = items.Count;
            if (LeavesItToWpf(combo))
            {
                ClearStamps();
                for (int index = 0; index < count; index++)
                {
                    if (generator.ContainerFromItem(items[index]) is DependencyObject container)
                    {
                        Clear(container);
                    }
                }

                return;
            }

            var first = new Dictionary<object, int>(count);
            int firstNull = -1;
            for (int index = 0; index < count; index++)
            {
                if (items[index] is { } item)
                {
                    _ = first.TryAdd(item, index);
                }
                else if (firstNull < 0)
                {
                    firstNull = index;
                }
            }

            for (int index = 0; index < count; index++)
            {
                if (generator.ContainerFromIndex(index) is not DependencyObject container)
                {
                    continue;
                }

                object item = generator.ItemFromContainer(container);
                int at = item is null ? firstNull : first.TryGetValue(item, out int found) ? found : -1;
                if (at < 0)
                {
                    continue;
                }

                if (AutomationProperties.GetPositionInSet(container) != at + 1)
                {
                    AutomationProperties.SetPositionInSet(container, at + 1);
                    Writes++;
                }

                if (AutomationProperties.GetSizeOfSet(container) != count)
                {
                    AutomationProperties.SetSizeOfSet(container, count);
                    Writes++;
                }

                _ = _stamped.Add(container);
            }
        }
        catch (InvalidOperationException)
        {
            // A view held in DeferRefresh: the stamps stay as they were until
            // the next event that runs the pass.
        }
    }

    /// <summary>Every stamp this tracker made is cleared: the list closed, or
    /// its rows are left to WPF.</summary>
    private void ClearStamps()
    {
        foreach (DependencyObject container in _stamped)
        {
            Clear(container);
        }

        _stamped.Clear();
    }

    private static void Clear(DependencyObject container)
    {
        container.ClearValue(AutomationProperties.PositionInSetProperty);
        container.ClearValue(AutomationProperties.SizeOfSetProperty);
    }
}
