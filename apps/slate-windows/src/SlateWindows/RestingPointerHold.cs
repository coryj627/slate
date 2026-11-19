// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SlateWindows;

/// <summary>
/// #1939, #1849: a menu the keys open lands on its first row (or its checked row), and a
/// pointer resting where the menu appears mustn't take the keys from it. When a popup appears
/// under a resting pointer WPF finds a row under it and hovers it, and a hovered row takes the
/// keys and the selection (a hovered menu's row opens its menu too); WPF's own guard (a
/// <see cref="MenuItem"/>'s ignore-mouse flag) lapses at Background priority, and a menu that
/// slides in passes its rows under the pointer before it settles. While a hold lasts the rows
/// are out of hit testing, so nothing hovers. A move to another place gives them back; so does
/// a press, which then re-reads what is under the pointer, and the release works that row as a
/// menu's press-and-release does; and so does the menu closing, which its caller reports.
/// </summary>
/// <remarks>
/// <para>A hold is kept per route: the element whose preview route sees every move and press
/// over the menu (the menu itself, or the bar that owns the app menu's popup). A route has at
/// most one hold and one set of handlers, however often it is begun.</para>
/// <para>The rows held are the ones the begins were given, and a release clears exactly those:
/// a row added later is neither held nor cleared, so no row keeps a local value it was never
/// given back.</para>
/// </remarks>
internal static class RestingPointerHold
{
    /// <summary>Each route's hold. Keyed weakly, so a dropped menu doesn't keep its rows.</summary>
    private static readonly ConditionalWeakTable<UIElement, Hold> Holds = new();

    [ThreadStatic]
    private static Func<(int X, int Y)?>? t_pointerForTests;

    /// <summary>Where the pointer is now, or null when it can't be read (a session with no
    /// input desktop). A hosted fact replaces it on its own thread, where its menus live, and
    /// puts it back in a finally.</summary>
    internal static Func<(int X, int Y)?> PointerForTests
    {
        get => t_pointerForTests ?? NativePointer.Read;
        set => t_pointerForTests = value;
    }

    /// <summary>Holds <paramref name="rows"/> from the pointer while it rests where it is now.
    /// On a route with no hold it reads the pointer (nothing is held when it can't be read),
    /// hooks the route's move and press once, and records the place. On a held route it
    /// records the new place and holds these rows too.</summary>
    internal static void Begin(UIElement route, IEnumerable<MenuItem> rows)
    {
        if (PointerForTests() is not { } at)
        {
            return;
        }

        if (!Holds.TryGetValue(route, out Hold? hold))
        {
            hold = new Hold(route);
            Holds.Add(route, hold);
            // The menu holds the mouse while it's open, so every move and press reaches its
            // route, over its rows or not.
            route.PreviewMouseMove += hold.OnMove;
            route.PreviewMouseDown += hold.OnPress;
        }

        hold.At = at;
        foreach (MenuItem row in rows)
        {
            hold.Rows.Add(row);
            row.IsHitTestVisible = false;
        }
    }

    /// <summary>Gives every row this route's hold set back, unhooks it, and forgets it.
    /// Harmless on a route with no hold.</summary>
    internal static void Release(UIElement route)
    {
        if (!Holds.TryGetValue(route, out Hold? hold))
        {
            return;
        }

        Holds.Remove(route);
        route.PreviewMouseMove -= hold.OnMove;
        route.PreviewMouseDown -= hold.OnPress;
        foreach (MenuItem row in hold.Rows)
        {
            row.ClearValue(UIElement.IsHitTestVisibleProperty);
        }
    }

    private sealed class Hold(UIElement route)
    {
        internal (int X, int Y) At { get; set; }

        internal HashSet<MenuItem> Rows { get; } = [];

        /// <summary>WPF raises moves while a menu appears and slides under a still pointer;
        /// only one that finds the pointer somewhere else ends the rest.</summary>
        internal void OnMove(object sender, MouseEventArgs e)
        {
            if (PointerForTests() is { } now && now != At)
            {
                Release(route);
            }
        }

        internal void OnPress(object sender, MouseButtonEventArgs e)
        {
            Release(route);
            // The press went where the rows weren't; now they're back, the row under the
            // pointer is read again before the release, which a menu's row answers.
            Mouse.Synchronize();
        }
    }

    private static class NativePointer
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct Point
        {
            public int X;
            public int Y;
        }

        internal static (int X, int Y)? Read() => GetCursorPos(out Point at) ? (at.X, at.Y) : null;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out Point point);
    }
}
