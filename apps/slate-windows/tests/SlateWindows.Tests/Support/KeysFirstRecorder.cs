// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SlateWindows.Tests;

/// <summary>
/// #1866 (CD11): whether a control holding the keys was turned off before they left it.
/// Off with the keys on it, a control says "unavailable" in NVDA and the keys fall to
/// whatever is behind it. A keys-first fact calls <see cref="AssertTheKeysLeaveFirst(string, UIElement, Action)"/>
/// in its own code: the focus integrity manifest's census reads that call in the fact's
/// own method, not in a helper it calls, before it takes the fact as a site's keys-first
/// witness. #1867's overlay facts moved here from CommandPaletteTests.
/// </summary>
internal static class KeysFirstRecorder
{
    /// <summary>Runs <paramref name="act"/> and fails, naming what held the keys, if
    /// <paramref name="root"/> turned off while the keys were on it or inside it. The root
    /// is the site's element, or a descendant that the site's change turns off with it (a
    /// menu item, for the menu). It also fails when the act never turns the root off: such
    /// a fact measured nothing about the keys leaving first, and when the root does not hold
    /// the keys as the act starts: it could never go off holding them.</summary>
    internal static void AssertTheKeysLeaveFirst(string what, UIElement root, Action act)
    {
        Assert.True(TheKeysAreIn(root), $"{what} did not hold the keys when the act began (they were on {Keyboard.FocusedElement?.ToString() ?? "nothing"}), so nothing about them leaving first was measured");
        bool wentOff = false;
        DependencyPropertyChangedEventHandler off = (_, e) => wentOff |= e.NewValue is false;
        List<string> offUnder;
        root.IsEnabledChanged += off;
        try
        {
            offUnder = DisabledWithTheKeysInside(root, act);
        }
        finally
        {
            root.IsEnabledChanged -= off;
        }

        Assert.True(offUnder.Count == 0, $"{what} went off with the keys on {string.Join(", ", offUnder)}");
        Assert.True(wentOff, $"{what} never went off in the act, so nothing about the keys leaving first was measured");
    }

    /// <summary>Plan 47 CB-17b: the measured form of "its own press never turns it off", for
    /// a control that used to go off under the keys and now stays on. Runs
    /// <paramref name="act"/> and fails unless <paramref name="root"/> holds the keys as the
    /// act starts, never goes off in it, and <paramref name="reached"/> is true after it.
    /// The predicate says the driver that used to turn the control off ran (the publication
    /// applied, the run completed, the sheet closed): without it a fact that never pumped
    /// as far as the off would pass having measured nothing.</summary>
    internal static void AssertItStaysOn(string what, UIElement root, Action act, Func<bool> reached)
    {
        Assert.True(TheKeysAreIn(root), $"{what} did not hold the keys when the act began (they were on {Keyboard.FocusedElement?.ToString() ?? "nothing"}), so nothing about it staying on under them was measured");
        Assert.True(root.IsEnabled, $"{what} was already off when the act began");
        Assert.False(reached(), $"{what}: reached was already true before the act, so it cannot say the act reached what used to turn the control off");
        var off = new List<string>();
        DependencyPropertyChangedEventHandler record = (_, e) =>
        {
            if (e.NewValue is false)
            {
                off.Add(Keyboard.FocusedElement?.ToString() ?? "nothing");
            }
        };
        root.IsEnabledChanged += record;
        try
        {
            act();
        }
        finally
        {
            root.IsEnabledChanged -= record;
        }

        Assert.True(off.Count == 0, $"{what} went off in the act, with the keys on {string.Join(", ", off)}");
        Assert.True(reached(), $"{what} stayed on, but the act never reached what used to turn it off, so nothing was measured");
    }

    /// <summary>The same for a focusable content element (a Hyperlink), which holds the
    /// keys itself.</summary>
    internal static void AssertTheKeysLeaveFirst(string what, ContentElement root, Action act)
    {
        Assert.True(ReferenceEquals(Keyboard.FocusedElement, root), $"{what} did not hold the keys when the act began (they were on {Keyboard.FocusedElement?.ToString() ?? "nothing"}), so nothing about them leaving first was measured");
        var offUnder = new List<string>();
        bool wentOff = false;
        DependencyPropertyChangedEventHandler record = (_, e) =>
        {
            wentOff |= e.NewValue is false;
            if (e.NewValue is false && ReferenceEquals(Keyboard.FocusedElement, root))
            {
                offUnder.Add(Keyboard.FocusedElement?.ToString() ?? "nothing");
            }
        };
        root.IsEnabledChanged += record;
        try
        {
            act();
        }
        finally
        {
            root.IsEnabledChanged -= record;
        }

        Assert.True(offUnder.Count == 0, $"{what} went off with the keys on {string.Join(", ", offUnder)}");
        Assert.True(wentOff, $"{what} never went off in the act, so nothing about the keys leaving first was measured");
    }

    /// <summary>Runs <paramref name="act"/> and returns the element that held the keys each
    /// time <paramref name="root"/> was turned off with the keys still on it or inside it.
    /// Every change to off is recorded, so a control that goes off and comes back within
    /// one dispatcher operation is named: off with the keys on it is the defect however
    /// short.</summary>
    internal static List<string> DisabledWithTheKeysInside(UIElement root, Action act)
    {
        var disabledUnder = new List<string>();
        DependencyPropertyChangedEventHandler record = (_, e) =>
        {
            if (e.NewValue is false && TheKeysAreIn(root))
            {
                disabledUnder.Add(Keyboard.FocusedElement?.ToString() ?? "nothing");
            }
        };
        root.IsEnabledChanged += record;
        try
        {
            act();
        }
        finally
        {
            root.IsEnabledChanged -= record;
        }

        return disabledUnder;
    }

    /// <summary>Whether the keys are on <paramref name="element"/> or inside it, by where
    /// they are rather than by its IsKeyboardFocusWithin: WPF moves the keys first and
    /// updates that flag along each chain after, so an ancestor's trigger can run while an
    /// element the keys just left still reads true. A popup's content (a submenu's rows) is
    /// outside its holder's visual tree, so the walk also goes up the logical tree, and the
    /// visual tree where an element has no logical parent (a template's parts).</summary>
    internal static bool TheKeysAreIn(UIElement element)
    {
        if (Keyboard.FocusedElement is not DependencyObject keys)
        {
            return false;
        }

        if (ReferenceEquals(keys, element) || (keys is Visual && element.IsAncestorOf(keys)))
        {
            return true;
        }

        for (DependencyObject? at = keys; at is not null;
            at = LogicalTreeHelper.GetParent(at) ?? (at is Visual ? VisualTreeHelper.GetParent(at) : null))
        {
            if (ReferenceEquals(at, element))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The keys onto a menu item. A desktop whose session has no foreground window
    /// (the hosted runners, #1680) lets no menu keep the keys or open its popup: there the
    /// fact says it measured nothing rather than passing.</summary>
    internal static void TakeTheMenuKeys(UIElement item)
    {
        if (!item.Focus() || !ReferenceEquals(item, Keyboard.FocusedElement))
        {
            _ = SkipWhereTheDesktopRefusesTheMenu($"the keys stayed on {Keyboard.FocusedElement}");
        }
    }

    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    internal static MenuItem SkipWhereTheDesktopRefusesTheMenu(string what)
    {
        Assert.Skip($"this desktop refused the menu the keys (#1680): {what}, so nothing about the menu holding them was measured on it");
        throw new System.Diagnostics.UnreachableException("Assert.Skip returned instead of ending the fact");
    }
}
