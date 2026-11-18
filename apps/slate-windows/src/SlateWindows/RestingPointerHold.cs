// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Controls;

namespace SlateWindows;

/// <summary>#1849: the facts' stub; the hold comes with the fix.</summary>
internal static class RestingPointerHold
{
    internal static Func<(int X, int Y)?> PointerForTests { get; set; } = () => null;

    internal static void Begin(UIElement route, IEnumerable<MenuItem> rows)
    {
    }

    internal static void Release(UIElement route)
    {
    }
}
