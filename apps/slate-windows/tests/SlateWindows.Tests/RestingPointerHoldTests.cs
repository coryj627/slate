// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SlateWindows.Tests;

/// <summary>#1849: the one hold a menu's route keeps from a resting pointer. It holds the
/// rows it was given while the pointer stays where it was read, keeps one set of handlers
/// however often it is begun, and gives back exactly the rows it held. Each fact replaces
/// the pointer's read on its own thread, so no desktop is needed.</summary>
public sealed class RestingPointerHoldTests
{
    /// <summary>A hold begun twice at one place, released, and begun at another place is
    /// one hold at the new place: a handler left over from the first would still compare
    /// the pointer with the old place, and a move at the new one would give the rows back.</summary>
    [Fact]
    public void ABeginOnAHeldRouteHooksNoSecondSetOfHandlers() => RunWithPointer((pointer, route, rows) =>
    {
        pointer.At = (10, 10);
        RestingPointerHold.Begin(route, rows);
        RestingPointerHold.Begin(route, rows);
        RestingPointerHold.Release(route);
        AssertGivenBack(rows, "after the release");

        pointer.At = (300, 200);
        RestingPointerHold.Begin(route, rows);
        AssertHeld(rows, "after a begin at the new place");
        Move(route);
        AssertHeld(rows, "after a move that left the pointer at the new place");

        pointer.At = (301, 200);
        Move(route);
        AssertGivenBack(rows, "after a move to another place");
    });

    [Fact]
    public void AReleaseOnARouteWithNoHoldIsHarmless() => RunWithPointer((pointer, route, rows) =>
    {
        RestingPointerHold.Release(route);
        AssertGivenBack(rows, "after a release with no hold");

        RestingPointerHold.Begin(route, rows);
        RestingPointerHold.Release(route);
        RestingPointerHold.Release(route);
        AssertGivenBack(rows, "after two releases");

        RestingPointerHold.Begin(route, rows);
        AssertHeld(rows, "after a begin that followed two releases");
        RestingPointerHold.Release(route);
        AssertGivenBack(rows, "after the last release");
    });

    /// <summary>The held rows are the ones the begins were given: a row that comes later
    /// is neither held nor given back, so a value someone else set on it stays.</summary>
    [Fact]
    public void ARowAddedAfterTheLastBeginIsNeitherHeldNorCleared() => RunWithPointer((pointer, route, rows) =>
    {
        RestingPointerHold.Begin(route, rows);
        var later = new MenuItem { Header = "Later" };
        route.Items.Add(later);
        Assert.True(later.ReadLocalValue(UIElement.IsHitTestVisibleProperty) == DependencyProperty.UnsetValue, "a row added after the begin was held.");

        later.IsHitTestVisible = false;
        RestingPointerHold.Release(route);
        AssertGivenBack(rows, "after the release");
        Assert.True(later.ReadLocalValue(UIElement.IsHitTestVisibleProperty) is false, "the release cleared a row it never held.");
    });

    [Fact]
    public void ARowGivenToASecondBeginIsClearedByTheOneRelease() => RunWithPointer((pointer, route, rows) =>
    {
        var second = new MenuItem { Header = "Second" };
        RestingPointerHold.Begin(route, rows);
        RestingPointerHold.Begin(route, [second]);
        AssertHeld([.. rows, second], "after two begins");

        RestingPointerHold.Release(route);
        AssertGivenBack([.. rows, second], "after the one release");
    });

    /// <summary>A desktop with no input (a CI session) has no pointer to read: nothing is
    /// held, so nothing can stay held.</summary>
    [Fact]
    public void APointerThatCannotBeReadHoldsNothing() => RunWithPointer((pointer, route, rows) =>
    {
        pointer.At = null;
        RestingPointerHold.Begin(route, rows);
        AssertGivenBack(rows, "after a begin with no pointer to read");

        pointer.At = (10, 10);
        Move(route);
        RestingPointerHold.Release(route);
        AssertGivenBack(rows, "after a move and a release");
    });

    /// <summary>Sort and group lands its checked order on every open, a pointer's click too, so
    /// every open holds its rows: a pointer resting where they appear, or one they slide under,
    /// can't take the keys. Its close gives them back.</summary>
    [Fact]
    public void SortAndGroupHoldsItsRowsOnAPointersOpen() => StaThread.RunPumped(() => WithPointerAt((40, 40), () =>
    {
        using var host = new ShownShell(("note.md", "# Note\n"));
        using FilesToolbarTests.Attached files = FilesToolbarTests.Attach(host);
        ClickAsAPointer(host.Shell.SidebarSortButton);
        host.Settle();
        host.Settle();
        ContextMenu menu = Assert.IsType<ContextMenu>(host.Shell.SidebarSortMenu);
        AssertHeldThenGivenBackOnClose(host, menu, "Sort and group");
    }));

    /// <summary>More panels the same: it lands its open panel's row on every open.</summary>
    [Fact]
    public void MorePanelsHoldsItsRowsOnAPointersOpen() => StaThread.RunPumped(() => WithPointerAt((40, 40), () =>
    {
        using var host = new ShownShell(("note.md", "# Note\n"));
        ClickAsAPointer(host.Shell.RightPaneMorePanelsButton);
        host.Settle();
        host.Settle();
        ContextMenu menu = Assert.IsType<ContextMenu>(host.Shell.MorePanelsMenu);
        AssertHeldThenGivenBackOnClose(host, menu, "More panels");
    }));

    /// <summary>A pointer's click on <paramref name="button"/>: its mouse-up, then its Click.</summary>
    private static void ClickAsAPointer(System.Windows.Controls.Primitives.ButtonBase button)
    {
        button.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseLeftButtonUpEvent });
        button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
    }

    /// <summary>Every row of the open <paramref name="menu"/> is held; once it closes (its popup
    /// fades out before it raises Closed), every row is given back.</summary>
    private static void AssertHeldThenGivenBackOnClose(ShownShell host, ContextMenu menu, string name)
    {
        Assert.True(menu.IsOpen, $"{name} didn't open.");
        MenuItem[] rows = [.. menu.Items.OfType<MenuItem>()];
        Assert.NotEmpty(rows);
        AssertHeld(rows, $"with {name} opened by a pointer's click");
        bool closed = false;
        menu.Closed += (_, _) => closed = true;
        menu.IsOpen = false;
        Assert.True(
            SpinWait.SpinUntil(
                () =>
                {
                    host.Settle();
                    return closed;
                },
                TimeSpan.FromSeconds(5)),
            $"{name} never raised Closed.");
        AssertGivenBack(rows, $"once {name} closed");
    }

    /// <summary>Runs <paramref name="fact"/> with the hold reading <paramref name="at"/> as the
    /// pointer, on the calling thread; the read is put back after.</summary>
    private static void WithPointerAt((int X, int Y) at, Action fact)
    {
        Func<(int X, int Y)?> saved = RestingPointerHold.PointerForTests;
        RestingPointerHold.PointerForTests = () => at;
        try
        {
            fact();
        }
        finally
        {
            RestingPointerHold.PointerForTests = saved;
        }
    }

    /// <summary>The pointer the hold reads, which a fact moves.</summary>
    private sealed class FakePointer
    {
        internal (int X, int Y)? At { get; set; } = (10, 10);
    }

    private static void RunWithPointer(Action<FakePointer, ContextMenu, MenuItem[]> body) => StaThread.Run(() =>
    {
        var pointer = new FakePointer();
        Func<(int X, int Y)?> saved = RestingPointerHold.PointerForTests;
        RestingPointerHold.PointerForTests = () => pointer.At;
        try
        {
            var menu = new ContextMenu();
            MenuItem[] rows = [new MenuItem { Header = "First" }, new MenuItem { Header = "Second" }, new MenuItem { Header = "Third" }];
            foreach (MenuItem row in rows)
            {
                menu.Items.Add(row);
            }

            body(pointer, menu, rows);
        }
        finally
        {
            RestingPointerHold.PointerForTests = saved;
        }
    });

    /// <summary>A pointer move on the route, as WPF raises one over the menu.</summary>
    private static void Move(UIElement route) =>
        route.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount) { RoutedEvent = UIElement.PreviewMouseMoveEvent });

    private static void AssertHeld(IEnumerable<MenuItem> rows, string when)
    {
        foreach (MenuItem row in rows)
        {
            Assert.True(row.ReadLocalValue(UIElement.IsHitTestVisibleProperty) is false, $"{row.Header} is not held {when}.");
        }
    }

    private static void AssertGivenBack(IEnumerable<MenuItem> rows, string when)
    {
        foreach (MenuItem row in rows)
        {
            Assert.True(
                row.ReadLocalValue(UIElement.IsHitTestVisibleProperty) == DependencyProperty.UnsetValue,
                $"{row.Header} keeps a local hit-test value {when}.");
        }
    }
}
