// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SlateWindows.Canvas;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W7-7 PR 4 (#1247, contract R-5; spec review round 23): the outline's
/// landing is a ROW. <c>CanvasOutlineView.FocusTree</c> focused the bare
/// tree; the first fact measures what WPF did with that on the real
/// outline, and the rest hold the landing that replaced it — the seated
/// card's row, else the first — with every arrow from it staying in the
/// outline. Keys are real presses through the input manager, so the
/// surface's own key handling and WPF's directional navigation both
/// answer them.
/// </summary>
public sealed partial class CanvasNavigatorTests
{
    /// <summary>The platform, measured on the outline itself: with no card
    /// seated a focused tree keeps the keys; with one seated WPF hands them
    /// to its row, while the tree's own Focus() answers false.</summary>
    [Fact]
    public void WpfHandsTheOutlinesKeysToItsSeatedRowOnly() => RunSta(() =>
    {
        CanvasDocumentViewModel document = Open("board.canvas");
        document.SeatSelectionSilently(null);
        using OutlineHost host = HostOutline(document);
        TreeView tree = host.Surface.OutlineForTests.TreeForTests;

        Assert.True(host.Beside.Focus());
        Assert.True(tree.Focus());
        Assert.Same(tree, Keyboard.FocusedElement);

        document.SeatSelectionSilently("loose");
        host.UpdateLayout();
        Assert.True(host.Beside.Focus());
        Assert.False(tree.Focus());
        Assert.Equal("loose", FocusedRowId());
    });

    /// <summary>The seated card's row takes the keys, and the tree itself
    /// never has them.</summary>
    [Fact]
    public void TheOutlineLandsOnTheSeatedRow() => RunSta(() =>
    {
        CanvasDocumentViewModel document = Open("board.canvas");
        using OutlineHost host = HostOutline(document);
        document.SeatSelectionSilently("loose");
        host.UpdateLayout();
        Assert.True(host.Beside.Focus());
        host.RecordFocus();

        Assert.True(host.Surface.FocusProjection());

        Assert.Equal("loose", FocusedRowId());
        host.AssertTreeNeverFocused();
    });

    /// <summary>With no card seated the first row takes the keys — seated
    /// silently, as a delivery is — and the tree itself never has
    /// them.</summary>
    [Fact]
    public void WithNothingSeatedTheOutlineLandsOnItsFirstRow() => RunSta(() =>
    {
        CanvasDocumentViewModel document = Open("board.canvas");
        document.SeatSelectionSilently(null);
        using OutlineHost host = HostOutline(document);
        Assert.True(host.Beside.Focus());
        Drain(document);
        host.RecordFocus();

        Assert.True(host.Surface.FocusProjection());

        CanvasOutlineRowViewModel first = host.Surface.OutlineForTests.RootsForTests[0];
        Assert.Equal(first.Id, FocusedRowId());
        Assert.Equal(first.Id, document.Selection.Selected);
        Assert.Empty(Lines(document));
        host.AssertTreeNeverFocused();
    });

    /// <summary>The arrow witness: from the landed row each arrow keeps the
    /// keys in the outline — the navigator moves or follows, or answers at
    /// an end — and none reaches the buttons beside it.</summary>
    [Theory]
    [InlineData(Key.Left)]
    [InlineData(Key.Right)]
    [InlineData(Key.Up)]
    [InlineData(Key.Down)]
    public void FromTheLandedRowEveryArrowStaysInTheOutline(Key key) => RunSta(() =>
    {
        CanvasDocumentViewModel document = Open("board.canvas");
        document.SeatSelectionSilently(null);
        using OutlineHost host = HostOutline(document);
        Assert.True(host.Beside.Focus());
        Assert.True(host.Surface.FocusProjection());
        host.RecordFocus();

        host.Press(key);

        TreeView tree = host.Surface.OutlineForTests.TreeForTests;
        Assert.True(
            tree.IsKeyboardFocusWithin && !ReferenceEquals(tree, Keyboard.FocusedElement),
            $"{key} took the keys off the outline's rows, to {Keyboard.FocusedElement}");
        host.AssertTreeNeverFocused();
    });

    private static string? FocusedRowId() =>
        (Keyboard.FocusedElement as FrameworkElement)?.DataContext is CanvasOutlineRowViewModel row ? row.Id : null;

    /// <summary>The outline projection with a button above and one to each
    /// side, so an arrow that leaves the outline lands somewhere
    /// observable.</summary>
    private static OutlineHost HostOutline(CanvasDocumentViewModel document)
    {
        var surface = new CanvasSurfaceView { Model = document };
        var root = new DockPanel();
        var above = new Button { Content = "Above" };
        var left = new Button { Content = "Left" };
        var right = new Button { Content = "Right" };
        DockPanel.SetDock(above, Dock.Top);
        DockPanel.SetDock(left, Dock.Left);
        DockPanel.SetDock(right, Dock.Right);
        root.Children.Add(above);
        root.Children.Add(left);
        root.Children.Add(right);
        root.Children.Add(surface);
        var window = new Window
        {
            Content = root,
            Width = 900,
            Height = 700,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
        };
        window.Show();
        window.Activate();
        window.UpdateLayout();
        Assert.Equal(CanvasSurfaceKind.Outline, document.Selection.ActiveSurface);
        return new OutlineHost(window, surface, above);
    }

    private sealed class OutlineHost(Window window, CanvasSurfaceView surface, Button beside) : IDisposable
    {
        private readonly List<IInputElement> _focusChanges = [];
        private bool _recording;

        internal CanvasSurfaceView Surface { get; } = surface;

        internal Button Beside { get; } = beside;

        internal void UpdateLayout() => window.UpdateLayout();

        /// <summary>Every keyboard focus change from here on is kept.</summary>
        internal void RecordFocus()
        {
            if (!_recording)
            {
                _recording = true;
                window.AddHandler(
                    Keyboard.GotKeyboardFocusEvent,
                    new KeyboardFocusChangedEventHandler((_, e) => _focusChanges.Add(e.NewFocus)),
                    handledEventsToo: true);
            }
        }

        internal void AssertTreeNeverFocused()
        {
            TreeView tree = Surface.OutlineForTests.TreeForTests;
            Assert.True(
                !_focusChanges.Any(focus => ReferenceEquals(focus, tree)),
                "the bare outline tree took the keys; focus went "
                + string.Join(" → ", _focusChanges.Select(focus => focus.GetType().Name)));
        }

        internal void Press(Key key)
        {
            InputManager.Current.ProcessInput(new KeyEventArgs(
                Keyboard.PrimaryDevice, PresentationSource.FromVisual(window)!, Environment.TickCount, key)
            {
                RoutedEvent = Keyboard.PreviewKeyDownEvent,
            });
            PumpedDispatcher.Drain();
            window.UpdateLayout();
        }

        public void Dispose() => window.Close();
    }
}
