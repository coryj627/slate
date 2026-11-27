// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using SlateWindows.Grids;
using SlateWindows.Reading;

namespace SlateWindows.Tests;

/// <summary>
/// WB-15 (#1307 item 3, WD9): contract 28 I3 seals every input the shell's
/// window routes. The reading table grid is a window the shell owns, shown
/// modeless, so none of <c>MainWindow</c>'s routes reach it. Each row
/// opens the table over the shipped shell, lands its first cell, seals the
/// shell with <c>ComponentDispatcher.PushModal()</c> — which disables no
/// window, the state a running palette command leaves the table in — and
/// applies one of I3's routes: the sealed input must leave its observable
/// alone, and the same input once the seal lifts must change it (the
/// control that makes each row discriminating).
/// </summary>
public sealed class ShellOwnedWindowProbeTests
{
    /// <summary>One row per route I3 names: "a key, its release, text, a
    /// pointer press, a double-click, a wheel turn, a mnemonic" — the
    /// pointer press as its press and its release. Plan 47 CB-38 adds a
    /// touch drag, which pans the grid from the touch's own events before
    /// any press is promoted.</summary>
    [Theory]
    [InlineData("key-down")]
    [InlineData("key-up")]
    [InlineData("text")]
    [InlineData("pointer-down")]
    [InlineData("pointer-up")]
    [InlineData("wheel")]
    [InlineData("double-click")]
    [InlineData("mnemonic")]
    [InlineData("touch-drag")]
    public void AnOpenReadingTableUnderTheSealTakesEveryShellRoute(string route) => RunSta(() =>
    {
        using var host = new CommandPaletteTests.ShippedShellHost(shown: true);
        using var table = new OpenTable(host);
        (Action apply, Func<string> observe) = table.Route(route);

        string before = table.Settled(observe);
        ComponentDispatcher.PushModal();
        string sealedAfter;
        try
        {
            Assert.True(host.Palette.IsSealed, "premise: the modal section sealed the shell");
            apply();
            sealedAfter = table.Settled(observe);
        }
        finally
        {
            ComponentDispatcher.PopModal();
        }

        Assert.False(host.Palette.IsSealed);
        string unsealedBefore = table.Settled(observe);
        apply();
        string unsealedAfter = table.Settled(observe);

        Assert.True(
            sealedAfter == before,
            $"{route} acted in the table window under the seal: {before} → {sealedAfter}");
        Assert.True(
            unsealedAfter != unsealedBefore,
            $"{route} did nothing once the seal lifted ({unsealedBefore}), so the row proves nothing");
    });

    /// <summary>
    /// WB-15 red team: T9 lets Escape dismiss an open palette under the
    /// seal — from the palette's own window. An Escape in the table window
    /// is not on the palette: under the seal it is taken like any key, so
    /// the palette stays open and the table stays open. Once the seal lifts
    /// the same Escape closes the table (its own Escape-close) and still
    /// leaves the palette alone.
    /// </summary>
    [Fact]
    public void EscapeInAnOpenReadingTableLeavesAnOpenPaletteUnderTheSeal() => RunSta(() =>
    {
        using var host = new CommandPaletteTests.ShippedShellHost(shown: true);
        using var table = new OpenTable(host);
        CommandPaletteViewModel palette = host.Palette;
        palette.Open();
        PumpedDispatcher.Drain();
        DataGridCell cell = Descendants<DataGridCell>(table.Grid.Grid).First();

        ComponentDispatcher.PushModal();
        try
        {
            Assert.True(palette.IsSealed && palette.IsOpen, "premise: the palette is open and sealed");
            bool handled = host.Press(cell, Key.Escape, ModifierKeys.None);
            PumpedDispatcher.Drain();
            Assert.True(handled, "the sealed Escape in the table window was not taken");
            Assert.True(palette.IsOpen, "an Escape in the table window dismissed the shell's palette under the seal");
            Assert.True(table.Window.IsVisible, "an Escape in the table window closed the table under the seal");
        }
        finally
        {
            ComponentDispatcher.PopModal();
        }

        _ = host.Press(cell, Key.Escape, ModifierKeys.None);
        PumpedDispatcher.Drain();
        Assert.False(table.Window.IsVisible, "premise: unsealed, the table's Escape closes it");
        Assert.True(palette.IsOpen, "the table's Escape dismissed the shell's palette");
    });

    /// <summary>
    /// Plan 47 CB-38: a message box or a common dialog disables only its
    /// owner, so under one the reading table the shell owns stays enabled
    /// and a touch still reaches it; a WPF dialog disables every window on
    /// the thread, the table included. Step 0 measured a real touch panning
    /// the table six rows under the seal. The loop here is
    /// <c>LoopSignals.DisablesShell</c>, a message box's: the shell's window
    /// disabled and no thread-modal state. Under it, the shell sealed and
    /// disabled and the table enabled, a touch drag pans nothing; once the
    /// loop ends it pans.
    /// </summary>
    [Fact]
    public void AReadingTableUnderAPromptThatDisablesTheShellPansNothingByTouch() => RunSta(() =>
    {
        using var host = new CommandPaletteTests.ShippedShellHost(shown: true);
        using var table = new OpenTable(host);
        (Action apply, Func<string> observe) = table.Route("touch-drag");
        string before = table.Settled(observe);
        var prompt = new CommandPaletteTests.SyntheticPrompt(host.Shell, CommandPaletteTests.LoopSignals.DisablesShell);
        (bool Sealed, bool ShellEnabled, bool TableEnabled)? premise = null;
        string? underThePrompt = null;
        _ = host.Shell.Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            try
            {
                premise = (host.Palette.IsSealed, CommandPaletteTests.WindowIsEnabled(host.Shell), CommandPaletteTests.WindowIsEnabled(table.Window));
                apply();
                underThePrompt = table.Settled(observe);
            }
            finally
            {
                prompt.Closer()!();
            }
        });
        prompt.Run();

        Assert.True(prompt.Ran, "the prompt never ran");
        Assert.True(
            premise is (true, false, true),
            $"premise: under the prompt the shell is sealed and disabled and the table enabled (sealed, shell, table: {premise})");
        Assert.False(host.Palette.IsSealed);
        string unsealedBefore = table.Settled(observe);
        apply();
        string unsealedAfter = table.Settled(observe);

        Assert.True(underThePrompt == before, $"a touch drag panned the reading table under a message box's loop: {before} to {underThePrompt}");
        Assert.True(
            unsealedAfter != unsealedBefore,
            $"a touch drag did not pan the table once the prompt closed ({unsealedBefore}), so the fact proves nothing");
    });

    /// <summary>
    /// Plan 47 CB-38: the take, and this fact, are defensive. Nothing in the
    /// reading table sets <c>AllowDrop</c>, so a real drag raises no drag
    /// events there today; should the table ever take a drop, a drag
    /// entering it under the seal is taken as it is in the shell's own
    /// window, and the source is told nothing would move. Unsealed, nothing
    /// in the table takes it.
    /// </summary>
    [Fact]
    public void AReadingTableUnderTheSealTakesADrop() => RunSta(() =>
    {
        using var host = new CommandPaletteTests.ShippedShellHost(shown: true);
        using var table = new OpenTable(host);
        DataGridCell cell = table.Cell(4);
        DragEventArgs sealedEnter;
        ComponentDispatcher.PushModal();
        try
        {
            Assert.True(host.Palette.IsSealed, "premise: the modal section sealed the shell");
            sealedEnter = CommandPaletteTests.RaisePreviewDragEnter(cell);
        }
        finally
        {
            ComponentDispatcher.PopModal();
        }

        Assert.False(host.Palette.IsSealed);
        DragEventArgs unsealedEnter = CommandPaletteTests.RaisePreviewDragEnter(cell);

        Assert.True(
            sealedEnter is { Handled: true, Effects: DragDropEffects.None },
            $"a drag entered the reading table under the seal (handled {sealedEnter.Handled}, the source told {sealedEnter.Effects})");
        Assert.True(
            unsealedEnter is { Handled: false, Effects: CommandPaletteTests.OfferedEffects },
            $"unsealed, something in the table took the drag (handled {unsealedEnter.Handled}, the source told {unsealedEnter.Effects}), so the fact proves nothing");
    });

    /// <summary>
    /// A modal dialog the shell owns is the prompt a modal-loop seal waits
    /// on (<c>HistoryConfirmationDialog</c>'s shape: <c>Owner</c> = the
    /// shell, never admitted), so the seal must not take its input: a
    /// double-click on its button reaches the button while the shell is
    /// sealed. The global double-click class handler is the route that could
    /// reach it — it resolves the owning shell through
    /// <c>ShellSeal.ShellOf</c> — so that is the route the fact drives. The
    /// seal is <c>ComponentDispatcher.PushModal()</c>, the thread-modal state
    /// a <c>ShowDialog</c> enters, and the window is shown modeless and not
    /// activated: a real <c>ShowDialog</c> takes the desktop's foreground
    /// from the facts running beside this one.
    /// </summary>
    [Fact]
    public void AModalDialogTheShellOwnsIsNotSealed() => RunSta(() =>
    {
        using var host = new CommandPaletteTests.ShippedShellHost(shown: true);
        int doubleClicks = 0;
        var button = new Button { Content = "Answer" };
        button.MouseDoubleClick += (_, _) => doubleClicks++;
        var dialog = new Window
        {
            Owner = host.Shell,
            Content = button,
            Width = 200,
            Height = 100,
            ShowInTaskbar = false,
            ShowActivated = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -10_000,
            Top = -10_000,
        };
        dialog.Show();
        try
        {
            PumpedDispatcher.Drain();
            ComponentDispatcher.PushModal();
            try
            {
                Assert.True(host.Palette.IsSealed, "premise: the modal section did not seal the shell");
                foreach (RoutedEvent routed in new[] { Control.PreviewMouseDoubleClickEvent, Control.MouseDoubleClickEvent })
                {
                    button.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                    {
                        RoutedEvent = routed,
                    });
                }
            }
            finally
            {
                ComponentDispatcher.PopModal();
            }
        }
        finally
        {
            dialog.Close();
            PumpedDispatcher.Drain();
        }

        Assert.Equal(1, doubleClicks);
    });

    /// <summary>The reading table window over the shipped shell, on its
    /// first cell, with each route's input and observable.</summary>
    private sealed class OpenTable : IDisposable
    {
        private const int RowCount = 40;
        private const int TypeAheadRow = 20;
        private readonly CommandPaletteTests.ShippedShellHost _host;

        public OpenTable(CommandPaletteTests.ShippedShellHost host)
        {
            _host = host;
            var source = new StringBuilder("| Name | Value |\n| --- | --- |\n");
            for (int row = 0; row < RowCount; row++)
            {
                string name = row == TypeAheadRow ? "river" : string.Create(CultureInfo.InvariantCulture, $"a{row:D2}");
                source.Append(CultureInfo.InvariantCulture, $"| {name} | {row} |\n");
            }

            // Not activated: an activated window takes the desktop's
            // foreground from the facts (and the FlaUI app) running beside
            // this one.
            Window = Assert.IsType<Window>(ReadingTableGrid.Show(source.ToString(), host.Shell, activate: false));
            Window.Left = -10_000;
            Window.Top = -10_000;
            Grid = Assert.IsType<AccessibleDataGrid>(Window.Content);
            Assert.True(
                PumpedDispatcher.PumpUntil(() => Keyboard.FocusedElement is DataGridCell, TimeSpan.FromSeconds(10)),
                $"premise: the table never landed its first cell (focus on {Keyboard.FocusedElement})");
            Assert.Equal(0, CurrentRow());
            Assert.Same(host.Shell, Window.Owner);
        }

        public Window Window { get; }

        public AccessibleDataGrid Grid { get; }

        public (Action Apply, Func<string> Observe) Route(string route)
        {
            DataGridCell first = Cell(0);
            switch (route)
            {
                case "key-down":
                    return (
                        () =>
                        {
                            _ = _host.Press(Focused(), Key.Down, ModifierKeys.None);
                            PumpedDispatcher.Drain();
                            if (Window.IsVisible)
                            {
                                _ = _host.Press(Focused(), Key.Escape, ModifierKeys.None);
                            }
                        },
                        () => $"row {CurrentRow()}, open {Window.IsVisible}");
                case "key-up":
                    {
                        int released = 0;
                        first.KeyUp += (_, _) => released++;
                        return (() => _host.ReleaseKey(first, Key.Apps), () => $"key-up heard {released}");
                    }

                case "text":
                    return (
                        () => CommandPaletteTests.ShippedShellHost.TypeText(Focused(), "r"),
                        () => $"row {CurrentRow()}");
                case "pointer-down":
                    {
                        DataGridCell fifth = Cell(4);
                        return (
                            () => _host.PressPointer(fifth),
                            () => $"row {CurrentRow()}, focus {Describe(Keyboard.FocusedElement)}, heard {_host.Heard.Count}");
                    }

                case "pointer-up":
                    {
                        int released = 0;
                        first.MouseUp += (_, _) => released++;
                        return (() => _host.ReleasePointer(first), () => $"mouse-up heard {released}");
                    }

                case "wheel":
                    {
                        ScrollViewer viewer = Descendants<ScrollViewer>(Grid.Grid).First();
                        return (
                            () => _host.Wheel(first, -120, ModifierKeys.None),
                            () => string.Create(CultureInfo.InvariantCulture, $"offset {viewer.VerticalOffset}"));
                    }

                case "double-click":
                    {
                        int clicks = 0;
                        first.MouseDoubleClick += (_, _) => clicks++;
                        return (
                            () =>
                            {
                                foreach (RoutedEvent routed in new[] { Control.PreviewMouseDoubleClickEvent, Control.MouseDoubleClickEvent })
                                {
                                    first.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                                    {
                                        RoutedEvent = routed,
                                    });
                                }
                            },
                            () => $"double-clicks heard {clicks}");
                    }

                case "mnemonic":
                    {
                        DataGridColumnHeader header = Descendants<DataGridColumnHeader>(Grid.Grid)
                            .First(candidate => ReferenceEquals(candidate.Column, Grid.Grid.Columns[0]));
                        AccessKeyManager.Register("q", header);
                        return (
                            () => AccessKeyManager.ProcessKey(PresentationSource.FromVisual(Window)!, "q", false),
                            () => $"sort {Grid.Grid.Columns[0].SortDirection?.ToString() ?? "none"}");
                    }

                case "touch-drag":
                    {
                        ScrollViewer viewer = Descendants<ScrollViewer>(Grid.Grid).First();
                        Assert.True(
                            viewer.PanningMode != PanningMode.None && viewer.ScrollableHeight > 0,
                            $"premise: the table's grid pans by touch and has rows to pan to (PanningMode {viewer.PanningMode})");
                        DataGridCell from = Cell(8);
                        Point start = from.TranslatePoint(new Point(10, from.ActualHeight / 2), Grid.Grid);
                        return (
                            () => CommandPaletteTests.SyntheticTouchDrag(Grid.Grid, start, new Vector(0, -200)),
                            () => string.Create(CultureInfo.InvariantCulture, $"offset {viewer.VerticalOffset}"));
                    }

                default:
                    throw new ArgumentOutOfRangeException(nameof(route), route, "no such route");
            }
        }

        /// <summary>The observable once the dispatcher and layout have
        /// settled.</summary>
        public string Settled(Func<string> observe)
        {
            PumpedDispatcher.Drain();
            if (Window.IsVisible)
            {
                Window.UpdateLayout();
            }

            PumpedDispatcher.Drain();
            return observe();
        }

        public void Dispose()
        {
            if (Window.IsVisible)
            {
                Window.Close();
            }

            PumpedDispatcher.Drain();
        }

        private int CurrentRow() =>
            Grid.Grid.CurrentCell.Item is { } item ? Grid.Grid.Items.IndexOf(item) : -1;

        private UIElement Focused() =>
            Assert.IsAssignableFrom<UIElement>(Keyboard.FocusedElement);

        public DataGridCell Cell(int row)
        {
            Grid.Grid.UpdateLayout();
            var container = Assert.IsType<DataGridRow>(Grid.Grid.ItemContainerGenerator.ContainerFromIndex(row));
            return Descendants<DataGridCell>(container).First(cell => ReferenceEquals(cell.Column, Grid.Grid.Columns[0]));
        }

        private static string Describe(IInputElement? element) => element switch
        {
            DataGridCell cell => $"cell in row {ItemsControl.ItemsControlFromItemContainer(DataGridRow.GetRowContainingElement(cell))?.Items.IndexOf(cell.DataContext)}",
            null => "nothing",
            _ => element.GetType().Name,
        };
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        var pending = new Stack<DependencyObject>([root]);
        while (pending.Count > 0)
        {
            DependencyObject node = pending.Pop();
            if (node is T match)
            {
                yield return match;
            }

            for (int i = VisualTreeHelper.GetChildrenCount(node) - 1; i >= 0; i--)
            {
                pending.Push(VisualTreeHelper.GetChild(node, i));
            }
        }
    }

    private static void RunSta(Action body) =>
        StaThread.Run(body, TimeSpan.FromSeconds(60), "STA test body timed out.");
}
