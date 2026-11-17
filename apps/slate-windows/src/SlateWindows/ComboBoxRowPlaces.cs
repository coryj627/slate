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
/// same. A stamp that went stale would make the item peer, which a screen
/// reader reads today and which is right today, wrong: so every event that can
/// move a row or change the count restamps every realized row, and where WPF's
/// answer is not "index plus one, of the count" (a grouped list, whose places
/// are within each group, or rows that are recycled) the tracker removes its
/// stamps and leaves WPF's answer.
/// </summary>
internal sealed class ComboBoxRowPlaces
{
    private static readonly ConditionalWeakTable<ComboBox, ComboBoxRowPlaces> Trackers = new();
    private static int s_registered;

    private readonly WeakReference<ComboBox> _combo;

    /// <summary>The containers this tracker wrote to, so one the generator let
    /// go of can be cleared.</summary>
    private readonly HashSet<DependencyObject> _stamped = new(ReferenceEqualityComparer.Instance);

    /// <summary>It restamps when the generator makes or drops rows (a first
    /// open, a new items host after a theme change) and when the items change.
    /// It does not listen for the list opening: at every open after the first
    /// the stamps are already current, and with that subscription removed no
    /// fact turned red (CB-19, mutation A3).</summary>
    private ComboBoxRowPlaces(ComboBox combo)
    {
        _combo = new WeakReference<ComboBox>(combo);
        combo.ItemContainerGenerator.StatusChanged += (_, _) => Restamp();
        ((INotifyCollectionChanged)combo.Items).CollectionChanged += (_, _) => Restamp();
        Restamp();
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
    /// One pass over the items. A container the generator no longer owns loses
    /// both values; under grouping or recycling every realized row loses them;
    /// otherwise every realized row gets its item's first index plus one (two
    /// equal items both get the first one's place, as WPF's IndexOf gives
    /// them), of the count, written only where it differs. It runs from a
    /// class handler for every combo box, inside layout, so it never throws: a
    /// view held in DeferRefresh refuses Count and IndexOf, and the Reset that
    /// ends the deferral runs this again.
    /// </summary>
    internal void Restamp()
    {
        if (!_combo.TryGetTarget(out ComboBox? combo))
        {
            return;
        }

        ItemContainerGenerator generator = combo.ItemContainerGenerator;
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
                foreach (DependencyObject container in _stamped)
                {
                    Clear(container);
                }

                _stamped.Clear();
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
                }

                if (AutomationProperties.GetSizeOfSet(container) != count)
                {
                    AutomationProperties.SetSizeOfSet(container, count);
                }

                _ = _stamped.Add(container);
            }
        }
        catch (InvalidOperationException)
        {
            // A view under DeferRefresh: the stamps stay as they are until the
            // Reset that ends the deferral runs this again.
        }
    }

    private static void Clear(DependencyObject container)
    {
        container.ClearValue(AutomationProperties.PositionInSetProperty);
        container.ClearValue(AutomationProperties.SizeOfSetProperty);
    }
}
