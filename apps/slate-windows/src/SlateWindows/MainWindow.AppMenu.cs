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
        LandTheKeys(AppMenuItem);
        AppMenuItem.IsSubmenuOpen = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            if (target is null)
            {
                LandTheKeys(menus.FirstOrDefault(menu => menu.IsEnabled));
                return;
            }

            LandTheKeys(target);
            target.IsSubmenuOpen = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
                LandTheKeys(target.Items.OfType<MenuItem>().FirstOrDefault(row => row.IsEnabled && row.IsVisible)));
        });
        return true;
    }

    /// <summary>Where the pointer rested when the keys opened the menu, while it hasn't moved
    /// since; null otherwise.</summary>
    private NativePointer.Point? _restingPointer;

    /// <summary>True while a key's own handling, or the app's landing, moves the keys: those
    /// moves are the keys', wherever the pointer rests.</summary>
    private bool _keysMoveTheKeys;

    /// <summary>#1939: the keys open the menu on its first row, and a pointer resting where the
    /// popup appears mustn't take them. When a popup appears under a resting pointer WPF finds
    /// a row under it and hovers it, and a hovered row takes the keys; WPF's own guard (a
    /// <see cref="MenuItem"/>'s ignore-mouse flag) lapses at Background priority, so on a slow
    /// machine the row under the pointer took the keys, Exit among them. While the keys hold
    /// the menu and the pointer hasn't moved, a row the pointer is over can't take the keys,
    /// though the keys themselves can move there; the rows still take a click. A move, a press
    /// or the menu closing ends it, and hovering works as before.</summary>
    private void HoldTheRestingPointer()
    {
        if (!NativePointer.GetCursorPos(out NativePointer.Point at))
        {
            return;
        }

        if (_restingPointer is null)
        {
            // Focus moving to a row inside the popup routes through the logo, and the open menu
            // holds the mouse, so every move and press reaches the bar's own route.
            AppMenuItem.PreviewGotKeyboardFocus += KeepTheKeysFromTheRestingPointer;
            AppMenuItem.PreviewKeyDown += LetTheKeyMoveTheKeys;
            MainMenu.PreviewMouseMove += ReleaseThePointerOnceItMoves;
            MainMenu.PreviewMouseDown += ReleaseThePointerOnAPress;
            AppMenuItem.SubmenuClosed += ReleaseThePointerWhenTheMenuCloses;
        }

        _restingPointer = at;
    }

    /// <summary>Gives <paramref name="row"/> the keys as the app's own landing, which the
    /// resting pointer doesn't hold back.</summary>
    private void LandTheKeys(MenuItem? row)
    {
        bool was = _keysMoveTheKeys;
        _keysMoveTheKeys = true;
        try
        {
            row?.Focus();
        }
        finally
        {
            _keysMoveTheKeys = was;
        }
    }

    private void KeepTheKeysFromTheRestingPointer(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (!_keysMoveTheKeys
            && _restingPointer is { } rested
            && e.NewFocus is MenuItem { IsMouseOver: true } row
            && !ReferenceEquals(row, AppMenuItem)
            && NativePointer.GetCursorPos(out NativePointer.Point now)
            && now.X == rested.X
            && now.Y == rested.Y)
        {
            e.Handled = true;
        }
    }

    private void LetTheKeyMoveTheKeys(object sender, KeyEventArgs e)
    {
        // The key's own handling runs on from here, in this same input operation; the reset
        // waits behind it, ahead of the next input.
        _keysMoveTheKeys = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Normal, () => _keysMoveTheKeys = false);
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

    private void ReleaseThePointerOnAPress(object sender, MouseButtonEventArgs e) => ReleaseThePointer();

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
        AppMenuItem.PreviewGotKeyboardFocus -= KeepTheKeysFromTheRestingPointer;
        AppMenuItem.PreviewKeyDown -= LetTheKeyMoveTheKeys;
        MainMenu.PreviewMouseMove -= ReleaseThePointerOnceItMoves;
        MainMenu.PreviewMouseDown -= ReleaseThePointerOnAPress;
        AppMenuItem.SubmenuClosed -= ReleaseThePointerWhenTheMenuCloses;
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
