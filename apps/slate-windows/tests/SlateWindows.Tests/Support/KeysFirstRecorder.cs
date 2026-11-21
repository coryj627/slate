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
    /// menu item, for the menu).</summary>
    internal static void AssertTheKeysLeaveFirst(string what, UIElement root, Action act)
    {
        List<string> offUnder = DisabledWithTheKeysInside(root, act);
        Assert.True(offUnder.Count == 0, $"{what} went off with the keys on {string.Join(", ", offUnder)}");
    }

    /// <summary>The same for a focusable content element (a Hyperlink), which holds the
    /// keys itself.</summary>
    internal static void AssertTheKeysLeaveFirst(string what, ContentElement root, Action act)
    {
        var offUnder = new List<string>();
        DependencyPropertyChangedEventHandler record = (_, e) =>
        {
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
    /// element the keys just left still reads true.</summary>
    internal static bool TheKeysAreIn(UIElement element) =>
        Keyboard.FocusedElement is DependencyObject keys
        && (ReferenceEquals(keys, element) || (keys is Visual && element.IsAncestorOf(keys)));

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
