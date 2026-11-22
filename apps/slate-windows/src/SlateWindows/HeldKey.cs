// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Input;
using System.Windows.Interop;

namespace SlateWindows;

/// <summary>
/// Plan 47 CB-17b (#1866, CD11; Codex round 2): whether a key-down is its key held down
/// repeating, by the keyboard's own account: the WM_KEYDOWN that WPF is routing carries the
/// previous-key-state bit (lParam bit 30). WPF's <see cref="KeyEventArgs.IsRepeat"/> is its
/// own guess, the same key as the last key-down with no key-up since: another key's key-up
/// (Shift let go during a held Shift+Enter) ends it while the key is still held, and a key-up
/// a native box took (Enter on Import…, whose box the reader cancels) leaves it standing for
/// the next new press. Only a key-down message for the event's own key counts: a key event
/// raised by code, with no keyboard message behind it or while another key's message is
/// current, is no repeat. The shell raises no key events of its own today, so those two
/// checks guard later callers.
/// </summary>
internal static class HeldKey
{
    private const int WmKeyDown = 0x0100;
    private const int WmSysKeyDown = 0x0104;
    private const long PreviousKeyState = 1L << 30;

    /// <summary>Whether <paramref name="e"/> is its key held down repeating.</summary>
    internal static bool IsRepeating(KeyEventArgs e)
    {
        MSG message = ComponentDispatcher.CurrentKeyboardMessage;
        return message.message is WmKeyDown or WmSysKeyDown
            && KeyInterop.KeyFromVirtualKey(unchecked((int)(long)message.wParam)) == e.Key
            && ((long)message.lParam & PreviousKeyState) != 0;
    }
}
