// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace SlateWindows;

/// <summary>The shell regions F6 / Shift+F6 cycle through (W7-6, #1240,
/// spec §1), in ring order. <see cref="EmptyEditor"/> stands in for
/// <see cref="TabBar"/> + <see cref="Editor"/> when no tab is open.</summary>
internal enum ShellRegionKind
{
    MenuBar,
    Files,
    TabBar,
    Editor,
    EmptyEditor,
    RightPaneContent,
    RightPaneRail,
    StatusBar,
}

/// <summary>What the ring depends on, read live on every press.</summary>
internal readonly record struct ShellRegionLayout(
    bool HasTabs,
    bool RightPaneVisible,
    bool RightPaneHasContentStop);

/// <summary>The F6 ring as a pure function: no window, no focus, no
/// timing — <c>ShellRegionRingTests</c> pins every layout shape.</summary>
internal static class ShellRegionRing
{
    /// <summary>The spec's full order; presence rules remove entries.</summary>
    private static readonly ShellRegionKind[] Order =
    [
        ShellRegionKind.MenuBar,
        ShellRegionKind.Files,
        ShellRegionKind.TabBar,
        ShellRegionKind.Editor,
        ShellRegionKind.EmptyEditor,
        ShellRegionKind.RightPaneContent,
        ShellRegionKind.RightPaneRail,
        ShellRegionKind.StatusBar,
    ];

    public static IReadOnlyList<ShellRegionKind> Ring(ShellRegionLayout layout) =>
        Order.Where(region => IsPresent(region, layout)).ToArray();

    /// <summary>The region after (<paramref name="direction"/> = +1) or
    /// before (-1) <paramref name="origin"/>, wrapping. A null origin, or
    /// one no longer in the ring, is placed by its slot in the full order
    /// so a press from a vanished region still lands on its neighbour.</summary>
    public static ShellRegionKind Next(ShellRegionLayout layout, ShellRegionKind? origin, int direction)
    {
        IReadOnlyList<ShellRegionKind> ring = Ring(layout);
        int step = direction < 0 ? -1 : 1;
        if (origin is null)
        {
            return step > 0 ? ring[0] : ring[^1];
        }

        int index = IndexOf(ring, origin.Value);
        if (index >= 0)
        {
            return ring[(index + step + ring.Count) % ring.Count];
        }

        // Absent origin: walk the full order from its slot in the pressed
        // direction until a present region is met (wrapping).
        int slot = Array.IndexOf(Order, origin.Value);
        for (int probe = 1; probe <= Order.Length; probe++)
        {
            ShellRegionKind candidate = Order[(slot + probe * step + Order.Length * probe) % Order.Length];
            if (IsPresent(candidate, layout))
            {
                return candidate;
            }
        }

        return step > 0 ? ring[0] : ring[^1];
    }

    private static bool IsPresent(ShellRegionKind region, ShellRegionLayout layout) => region switch
    {
        ShellRegionKind.TabBar or ShellRegionKind.Editor => layout.HasTabs,
        ShellRegionKind.EmptyEditor => !layout.HasTabs,
        ShellRegionKind.RightPaneContent => layout.RightPaneVisible && layout.RightPaneHasContentStop,
        ShellRegionKind.RightPaneRail => layout.RightPaneVisible,
        _ => true,
    };

    private static int IndexOf(IReadOnlyList<ShellRegionKind> ring, ShellRegionKind region)
    {
        for (int index = 0; index < ring.Count; index++)
        {
            if (ring[index] == region)
            {
                return index;
            }
        }

        return -1;
    }
}
