// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using SlateWindows.Panels;

namespace SlateWindows.Tests;

/// <summary>
/// WB-16 (#1305): arrowing through the right-pane rail commits each leaf at
/// once (S4) but makes at most one workspace write, when the arrows settle —
/// the last held rail arrow released, or the keys leaving the rail — never
/// while an arrow is down, however long the keyboard's first-repeat delay.
/// DS-16: the arrows stay in the strip's five, so History and Queries, which
/// reload on a settle, are reached through More panels, one switch each. Keys are raised on the
/// focused rail row as WPF raises them (the preview, then the bubbling event
/// only if the preview stayed unhandled) from the shell's own presentation
/// source; the workspace's key-state seam reads the fact's own held keys.
/// The held-sweep model: one Down press, the dispatcher pumped until the
/// settle's timer has fired twice with the arrow still down (the
/// first-repeat delay longer than the timer, made deterministic), then
/// further presses with no release between them, as auto-repeat delivers,
/// then the release.
/// </summary>
public sealed class RightPaneRailTests
{
    [Fact]
    public void AFirstRepeatDelayLongerThanTheSettleIntervalSettlesNothingWhileTheArrowIsHeld() => RunSta(() =>
    {
        using var rail = new Rail("outline");

        rail.Press(Key.Down);
        Assert.Equal("backlinks", rail.Workspace.ActiveLeaf.Id);
        rail.PumpHeldTicks(2);
        Assert.Empty(rail.Writes);

        rail.Press(Key.Down);
        rail.Press(Key.Down);
        Assert.Equal("connections", rail.Workspace.ActiveLeaf.Id);
        rail.Release(Key.Down);

        Assert.Equal(["connections"], rail.Writes);
    });

    [Fact]
    public void AHeldArrowSweepMakesAtMostOneWorkspaceWriteAndNoneInsideAKeyPress() => RunSta(() =>
    {
        using var rail = new Rail("outline");

        rail.Press(Key.Down);
        rail.PumpHeldTicks(2);
        for (int press = 1; press < 17; press++)
        {
            rail.Press(Key.Down);
            Assert.Empty(rail.Writes);
        }

        string reached = rail.Workspace.ActiveLeaf.Id;
        Assert.NotEqual("outline", reached);
        Assert.Empty(rail.Writes);
        rail.Release(Key.Down);
        Assert.Equal([reached], rail.Writes);
    });

    /// <summary>DS-16: History and Queries live in More panels, out of the arrows'
    /// reach, so the settle's reload and refresh never wait on them. A pick from the
    /// menu is one switch: it reloads History, or refreshes Queries, once, and makes
    /// one workspace write, at once.</summary>
    [Fact]
    public void PickingHistoryOrQueriesFromMoreRefreshesThatLeafOnceAndWritesOnce() => RunSta(() =>
    {
        using var rail = new Rail("outline");
        int reloads = rail.Workspace.History.ReloadCountForTests;
        int refreshes = rail.Workspace.BaseQueriesRefreshCountForTests;

        rail.Workspace.ActiveLeaf = Rail.Leaf("history");
        Assert.Equal(reloads + 1, rail.Workspace.History.ReloadCountForTests);
        Assert.Equal(["history"], rail.Writes);

        rail.Workspace.ActiveLeaf = Rail.Leaf("queries");
        Assert.Equal(refreshes + 1, rail.Workspace.BaseQueriesRefreshCountForTests);
        Assert.Equal(["history", "queries"], rail.Writes);
        PumpedDispatcher.Drain();
        Assert.Equal(reloads + 1, rail.Workspace.History.ReloadCountForTests);
        Assert.Equal(["history", "queries"], rail.Writes);
    });

    [Fact]
    public void AKeyUpEndsTheSettleAtOnce() => RunSta(() =>
    {
        using var rail = new Rail("outline");
        rail.Press(Key.Down);
        rail.PumpHeldTicks(1);
        rail.Press(Key.Down);
        Assert.Empty(rail.Writes);

        rail.Release(Key.Down, drain: false);

        Assert.Single(rail.Writes);
    });

    /// <summary>WB-16 red team: a key whose release the rail never saw (the
    /// seal took it) and which chose no leaf — Up on the top row — is still
    /// in the held set; it must not hold back the next sweep's release.</summary>
    [Fact]
    public void AStaleHeldKeyDoesNotHoldBackTheReleasesSettle() => RunSta(() =>
    {
        using var rail = new Rail("outline");
        rail.Press(Key.Up);
        Assert.Equal("outline", rail.Workspace.ActiveLeaf.Id);
        Assert.True(rail.Workspace.RailSettle.IsHeld, "premise: the rail did not record the press");
        _ = rail.Held.Remove(Key.Up);

        rail.Press(Key.Down);
        rail.Release(Key.Down, drain: false);

        Assert.Single(rail.Writes);
    });

    [Fact]
    public void LeavingTheRailWhileTheArrowIsHeldSettlesAtOnce() => RunSta(() =>
    {
        using var rail = new Rail("outline");
        rail.Press(Key.Down);
        rail.PumpHeldTicks(1);
        Assert.Empty(rail.Writes);

        Assert.True(rail.Shell.Shell.FilesTree.Focus(), "premise: the Files tree refused the keys");
        // Posted after the focus change, at Input priority: the next turn.
        Assert.Empty(rail.Writes);
        PumpedDispatcher.Drain();

        Assert.Single(rail.Writes);
        Assert.True(rail.Held.Contains(Key.Down), "premise: the arrow is still held");
    });

    [Fact]
    public void AReleaseTheSealTookStillSettlesOnTheNextTick() => RunSta(() =>
    {
        using var rail = new Rail("outline");
        rail.Press(Key.Down);
        rail.PumpHeldTicks(1);
        rail.Press(Key.Down);

        ComponentDispatcher.PushModal();
        try
        {
            Assert.True(rail.Shell.Shell.DataContext is VaultLifecycleViewModel { Palette.IsSealed: true }, "premise: the shell is not sealed");
            Assert.True(rail.Release(Key.Down, drain: false), "the seal did not take the release");
            Assert.Empty(rail.Writes);
            Assert.True(
                PumpedDispatcher.PumpUntil(() => rail.Writes.Count > 0, TimeSpan.FromSeconds(10)),
                "the settle never ran after a release the seal took");
        }
        finally
        {
            ComponentDispatcher.PopModal();
        }

        Assert.Single(rail.Writes);
    });

    [Fact]
    public void DisposingTheWorkspaceWithASettlePendingWritesTheChosenLeafOnce() => RunSta(() =>
    {
        using var rail = new Rail("connections");
        rail.Press(Key.Down);
        rail.PumpHeldTicks(1);
        Assert.Equal("inspector", rail.Workspace.ActiveLeaf.Id);
        Assert.True(rail.Workspace.RailSettle.IsArmed && rail.Workspace.RailSettle.IsHeld, "premise: the settle is not pending");

        rail.Workspace.Dispose();

        Assert.Equal(["inspector"], rail.Writes);
        Assert.False(rail.Workspace.RailSettle.IsArmed);
        Assert.False(rail.Workspace.RailSettle.IsHeld);

        _ = rail.Release(Key.Down);
        Assert.Equal(["inspector"], rail.Writes);
    });

    /// <summary>The shipped rail on a shown shell, its row for
    /// <paramref name="start"/> holding the keys, every workspace write
    /// counted by the leaf it wrote.</summary>
    private sealed class Rail : IDisposable
    {
        public Rail(string start)
        {
            Shell = new ShownShell(("note.md", "# Note\n"));
            Workspace.RailSettleInterval = static () => TimeSpan.FromMilliseconds(50);
            Workspace.RailKeyIsDown = Held.Contains;
            Workspace.ActiveLeaf = Leaf(start);
            Shell.Settle();
            ListBox list = Shell.Shell.RightPaneLeavesList;
            list.UpdateLayout();
            var row = Assert.IsAssignableFrom<ListBoxItem>(list.ItemContainerGenerator.ContainerFromItem(Leaf(start)));
            Assert.True(row.Focus(), $"premise: the rail's {start} row refused the keys");
            PumpedDispatcher.Drain();
            Workspace.PersistedForTests = snapshot => Writes.Add(snapshot.ActiveLeaf);
        }

        public ShownShell Shell { get; }

        public WorkspaceViewModel Workspace => Shell.Workspace;

        /// <summary>The leaf each workspace write carried, in order.</summary>
        public List<string> Writes { get; } = [];

        /// <summary>The keys the fact holds down.</summary>
        public HashSet<Key> Held { get; } = [];

        public static WorkspaceLeafOption Leaf(string id) =>
            WorkspaceViewModel.Leaves.Single(leaf => string.Equals(leaf.Id, id, StringComparison.Ordinal));

        /// <summary>A press — the first or an auto-repeat — on the focused
        /// row, with a drained dispatcher turn after it.</summary>
        public void Press(Key key)
        {
            _ = Held.Add(key);
            _ = Raise(Keyboard.PreviewKeyDownEvent, Keyboard.KeyDownEvent, key);
            PumpedDispatcher.Drain();
        }

        /// <summary>The key's release; returns whether it was handled.</summary>
        public bool Release(Key key, bool drain = true)
        {
            _ = Held.Remove(key);
            bool handled = Raise(Keyboard.PreviewKeyUpEvent, Keyboard.KeyUpEvent, key);
            if (drain)
            {
                PumpedDispatcher.Drain();
            }

            return handled;
        }

        /// <summary>Pumps until the settle's timer has fired
        /// <paramref name="ticks"/> more times with an arrow still down.</summary>
        public void PumpHeldTicks(int ticks)
        {
            int target = Workspace.RailSettle.HeldTicksForTests + ticks;
            Assert.True(
                PumpedDispatcher.PumpUntil(() => Workspace.RailSettle.HeldTicksForTests >= target, TimeSpan.FromSeconds(10)),
                "the settle's timer never fired with the arrow held");
        }

        public void Dispose() => Shell.Dispose();

        private bool Raise(RoutedEvent preview, RoutedEvent twin, Key key)
        {
            var target = Assert.IsAssignableFrom<UIElement>(Keyboard.FocusedElement);
            PresentationSource source = PresentationSource.FromVisual(Shell.Shell)!;
            var first = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key) { RoutedEvent = preview };
            target.RaiseEvent(first);
            if (first.Handled)
            {
                return true;
            }

            var second = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key) { RoutedEvent = twin };
            target.RaiseEvent(second);
            return second.Handled;
        }
    }

    private static void RunSta(Action body) =>
        StaThread.RunPumped(body, TimeSpan.FromSeconds(60), "STA test body timed out.");
}
