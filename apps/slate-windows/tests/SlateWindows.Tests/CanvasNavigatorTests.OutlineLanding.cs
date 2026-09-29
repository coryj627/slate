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
/// tree, which WPF left holding the keys with no card seated; the facts
/// hold the landing that replaced it — the seated card's row, else the
/// first — reached from the bare tree too (a landing tree), with every
/// arrow from it staying in the outline. Keys are real presses through the input manager, so the
/// surface's own key handling and WPF's directional navigation both
/// answer them.
/// </summary>
public sealed partial class CanvasNavigatorTests
{
    /// <summary>The outline is a landing tree (W7-7 PR 4, R-5 as the owner
    /// amended it): WPF alone kept the keys on a focused tree with no card
    /// seated, from where Left and Right left the outline. Keys sent to the
    /// bare outline — a click on its empty area, Tab, UI Automation's
    /// SetFocus — go on through the projection's own landing in the same
    /// focus change: the seated card's row, else the first row, seated
    /// silently. The tree never has them.</summary>
    [Fact]
    public void KeysSentToTheBareOutlineLandAsTheProjectionDoes() => RunSta(() =>
    {
        CanvasDocumentViewModel document = Open("board.canvas");
        document.SeatSelectionSilently(null);
        using OutlineHost host = HostOutline(document);
        TreeView tree = host.Surface.OutlineForTests.TreeForTests;
        Assert.True(host.Beside.Focus());
        Drain(document);
        host.RecordFocus();

        _ = tree.Focus();

        CanvasOutlineRowViewModel first = host.Surface.OutlineForTests.RootsForTests[0];
        Assert.Equal(first.Id, FocusedRowId());
        Assert.Empty(Lines(document));
        host.AssertTreeNeverFocused();

        document.SeatSelectionSilently("loose");
        host.UpdateLayout();
        Assert.True(host.Beside.Focus());
        _ = tree.Focus();
        Assert.Equal("loose", FocusedRowId());
        Assert.Empty(Lines(document));
        host.AssertTreeNeverFocused();
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

    /// <summary>Codex round 5: a restore token that is the BARE outline —
    /// nothing seated — lands through the projection's own landing (the
    /// first row, seated silently), not the generic tree landing, whose
    /// row's selection echo narrates a move on top of the row the reader
    /// hears (t0 §1.5).</summary>
    [Fact]
    public void ARestoreTokenOnTheBareOutlineLandsAsTheProjectionDoes() => RunSta(() =>
    {
        CanvasDocumentViewModel document = Open("board.canvas");
        document.SeatSelectionSilently(null);
        using OutlineHost host = HostOutline(document);
        IInputElement token = host.Surface.OutlineForTests.TreeForTests;
        Assert.True(host.Beside.Focus());
        Drain(document);
        host.RecordFocus();

        Assert.True(SelectorFocus.LandOnStop((UIElement)token));

        CanvasOutlineRowViewModel first = host.Surface.OutlineForTests.RootsForTests[0];
        Assert.Equal(first.Id, FocusedRowId());
        Assert.Equal(first.Id, document.Selection.Selected);
        Assert.Empty(Lines(document));
        host.AssertTreeNeverFocused();
    });

    /// <summary>The owner's S4 (the completeness sweep's G6): a restore whose
    /// token is a ROW of the outline lands through the projection's own
    /// landing — the seated card's row — never on the token's row, whose
    /// focus would select it, move the seat and narrate the move. The seat
    /// moved since the token was taken (a palette verb, a command).</summary>
    [Fact]
    public void ARestoreTokenOnAnOutlineRowLandsOnTheSeatedRowSilently() => RunSta(() =>
    {
        CanvasDocumentViewModel document = Open("board.canvas");
        using OutlineHost host = HostOutline(document);
        document.SeatSelectionSilently("loose");
        host.UpdateLayout();
        Assert.True(host.Surface.FocusProjection());
        Assert.Equal("loose", FocusedRowId());
        CanvasOutlineRowViewModel other = host.Surface.OutlineForTests.RootsForTests
            .First(row => row is { IsConnection: false } && row.Id != "loose");
        document.SeatSelectionSilently(other.Id);
        host.UpdateLayout();
        IInputElement token = Keyboard.FocusedElement;
        Assert.True(host.Beside.Focus());
        document.SeatSelectionSilently("loose");
        host.UpdateLayout();
        Drain(document);

        Assert.True(SelectorFocus.LandOnStop((UIElement)token));

        Assert.Equal("loose", FocusedRowId());
        Assert.Equal("loose", document.Selection.Selected);
        Assert.Empty(Lines(document));
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
            ShowActivated = false,
        };
        window.Show();
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
