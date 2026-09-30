// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;

namespace SlateWindows.AccessibilityTests;

/// <summary>
/// W7-7 PR 4b: the navigation keys as the keyboard's own keys — each sent
/// as its scan code with the extended flag. A virtual-key press goes out
/// without that flag, which Windows and a running screen reader read as the
/// numpad key of the same name: NVDA consumes numpad arrows, Home, End and
/// the page keys as review-cursor gestures before the app sees them, so a
/// journey that pressed them as virtual keys drove keys no user presses on
/// the navigation cluster, and failed whenever NVDA ran on the desktop
/// (measured on the PR 4 merge, with NVDA up).
/// </summary>
internal static class NavigationKeys
{
    private static readonly Dictionary<VirtualKeyShort, ushort> ScanCodes = new()
    {
        [VirtualKeyShort.UP] = 0x48,
        [VirtualKeyShort.DOWN] = 0x50,
        [VirtualKeyShort.LEFT] = 0x4B,
        [VirtualKeyShort.RIGHT] = 0x4D,
        [VirtualKeyShort.HOME] = 0x47,
        [VirtualKeyShort.END] = 0x4F,
        [VirtualKeyShort.PRIOR] = 0x49,
        [VirtualKeyShort.NEXT] = 0x51,
        [VirtualKeyShort.DELETE] = 0x53,
    };

    /// <summary>Types <paramref name="key"/>: a navigation-cluster key as
    /// its extended scan code, any other key as its virtual key.</summary>
    public static void Type(VirtualKeyShort key)
    {
        if (ScanCodes.TryGetValue(key, out ushort scanCode))
        {
            Keyboard.TypeScanCode(scanCode, true);
        }
        else
        {
            Keyboard.Type(key);
        }
    }
}
