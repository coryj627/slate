// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace SlateWindows;

/// <summary>
/// DS-13 (#1814): the menus sit inside the logo's popup (<c>AppMenuItem</c>), where their
/// headers' letters aren't live until it opens. So Alt and a menu's letter opens the popup at
/// that menu, and F10 opens it, as <c>design/windows/readme.md:120</c> and
/// <c>wpf-feasibility.md:115</c> ask. Plain Alt keeps WPF's menu mode on the logo.
/// </summary>
public partial class MainWindow
{
    /// <summary>Opens the app menu for a system key, answering whether it took the key: F10
    /// alone, or Alt and one of the six letters (<see cref="AppMenuKeys"/>). Nothing under a
    /// disabled bar, and a letter a live control holds (the find bar's) stays that control's.
    /// While the popup is open its own headers hold the letters, so they don't count.</summary>
    private bool TryOpenAppMenu(Key systemKey, ModifierKeys modifiers)
    {
        if (!MainMenu.IsEnabled)
        {
            return false;
        }

        if (systemKey == Key.F10 && modifiers == ModifierKeys.None)
        {
            return OpenAppMenu(null);
        }

        if (modifiers != ModifierKeys.Alt)
        {
            return false;
        }

        return AppMenuKeys.Decide(
                systemKey,
                OpenModalSurface is not null,
                () => !AppMenuItem.IsSubmenuOpen && AccessKeyManager.IsKeyRegistered(null, systemKey.ToString())) is { } menu
            && OpenAppMenu(menu);
    }

    /// <summary>Opens the logo's popup and, when named, that menu in it, with the keys on its
    /// first enabled row; with none named, on the popup's first row. A letter while open switches
    /// menus: WPF closes a menu's submenu when a sibling row takes the keys.</summary>
    private bool OpenAppMenu(string? automationId)
    {
        MenuItem[] menus = [.. AppMenuItem.Items.OfType<MenuItem>()];
        MenuItem? target = automationId is null
            ? null
            : menus.FirstOrDefault(menu => AutomationProperties.GetAutomationId(menu) == automationId);
        if (automationId is not null && target is null)
        {
            return false;
        }

        HoldTheRestingPointer();
        AppMenuItem.Focus();
        AppMenuItem.IsSubmenuOpen = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            if (target is null)
            {
                menus.FirstOrDefault(menu => menu.IsEnabled)?.Focus();
                return;
            }

            target.Focus();
            target.IsSubmenuOpen = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
                target.Items.OfType<MenuItem>().FirstOrDefault(row => row.IsEnabled && row.IsVisible)?.Focus());
        });
        return true;
    }

    /// <summary>Where the pointer rested when the keys opened the menu, while it hasn't moved
    /// since; null otherwise.</summary>
    private NativePointer.Point? _restingPointer;

    /// <summary>#1939: the keys open the menu on its first row, and a pointer resting where the
    /// popup appears mustn't take them. Windows sends a mouse move when a popup appears under
    /// the pointer, and WPF's own guard (a <see cref="MenuItem"/>'s ignore-mouse flag) lapses at
    /// Background priority, so on a slow machine the row under the pointer took the keys, Exit
    /// among them. The menu's rows stop answering the pointer until it moves or the menu
    /// closes; then hovering works as before.</summary>
    private void HoldTheRestingPointer()
    {
        if (!NativePointer.GetCursorPos(out NativePointer.Point at))
        {
            return;
        }

        if (_restingPointer is null)
        {
            // The open menu holds the mouse, so every move reaches the bar's own route.
            MainMenu.PreviewMouseMove += ReleaseThePointerOnceItMoves;
            AppMenuItem.SubmenuClosed += ReleaseThePointerWhenTheMenuCloses;
        }

        _restingPointer = at;
        foreach (MenuItem row in RowsOf(AppMenuItem))
        {
            row.IsHitTestVisible = false;
        }
    }

    private void ReleaseThePointerOnceItMoves(object sender, MouseEventArgs e)
    {
        if (_restingPointer is { } rested
            && NativePointer.GetCursorPos(out NativePointer.Point now)
            && (now.X != rested.X || now.Y != rested.Y))
        {
            ReleaseThePointer();
        }
    }

    private void ReleaseThePointerWhenTheMenuCloses(object sender, RoutedEventArgs e)
    {
        // A menu inside the popup closing bubbles here too; only the popup's own close counts.
        if (!AppMenuItem.IsSubmenuOpen)
        {
            ReleaseThePointer();
        }
    }

    private void ReleaseThePointer()
    {
        if (_restingPointer is null)
        {
            return;
        }

        _restingPointer = null;
        MainMenu.PreviewMouseMove -= ReleaseThePointerOnceItMoves;
        AppMenuItem.SubmenuClosed -= ReleaseThePointerWhenTheMenuCloses;
        foreach (MenuItem row in RowsOf(AppMenuItem))
        {
            row.ClearValue(IsHitTestVisibleProperty);
        }
    }

    /// <summary>Every row under <paramref name="menu"/>, at any depth.</summary>
    private static IEnumerable<MenuItem> RowsOf(MenuItem menu)
    {
        foreach (MenuItem row in menu.Items.OfType<MenuItem>())
        {
            yield return row;
            foreach (MenuItem inner in RowsOf(row))
            {
                yield return inner;
            }
        }
    }

    private static class NativePointer
    {
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        internal struct Point
        {
            public int X;
            public int Y;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        internal static extern bool GetCursorPos(out Point point);
    }
}
