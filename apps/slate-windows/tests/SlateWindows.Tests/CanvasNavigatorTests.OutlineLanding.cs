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

    /// <summary>WB-13 (#1260): deleting the focused row republishes the
    /// outline with a cleared seat and every container rebuilt, so the row
    /// that held the keys is gone. They land on the next row in reading
    /// order, seated silently — never on the window.</summary>
    [Fact]
    public void DeletingTheFocusedRowSeatsTheNextRowInReadingOrder() => RunSta(() =>
    {
        CanvasDocumentViewModel document = Open("trio.canvas");
        using OutlineHost host = HostOutline(document);
        host.FocusRow("beta");
        host.RecordFocus();

        document.CanvasDeleteSelection();
        host.Settle();

        Assert.IsType<CanvasOutlineItem>(Keyboard.FocusedElement);
        Assert.Equal("gamma", FocusedRowId());
        Assert.Equal("gamma", document.Selection.Selected);
        host.AssertTreeNeverFocused();
    });

    /// <summary>With no row after it, the previous row takes the keys.</summary>
    [Fact]
    public void DeletingTheLastRowSeatsThePreviousOne() => RunSta(() =>
    {
        CanvasDocumentViewModel document = Open("trio.canvas");
        using OutlineHost host = HostOutline(document);
        host.FocusRow("gamma");

        document.CanvasDeleteSelection();
        host.Settle();

        Assert.Equal("beta", FocusedRowId());
        Assert.Equal("beta", document.Selection.Selected);
    });

    /// <summary>The only card gone, the outline has no rows: the tree is its
    /// own stop (AR-6) and takes the keys, with nothing seated.</summary>
    [Fact]
    public void DeletingTheOnlyRowLandsOnTheTree() => RunSta(() =>
    {
        CanvasDocumentViewModel document = Open("solo.canvas");
        using OutlineHost host = HostOutline(document);
        host.FocusRow("only");

        document.CanvasDeleteSelection();
        host.Settle();

        Assert.Same(host.Surface.OutlineForTests.TreeForTests, Keyboard.FocusedElement);
        Assert.Null(document.Selection.Selected);
        Assert.Empty(host.Surface.OutlineForTests.RootsForTests);
    });

    /// <summary>Contract C12 / CD-40: the landing is a delivery, so it is
    /// silent — the delete's own sentence is the one line the reader
    /// hears, and no move to the successor is narrated over it.</summary>
    [Fact]
    public void TheDeleteSpeaksOnceAndTheLandingIsSilent() => RunSta(() =>
    {
        CanvasDocumentViewModel document = Open("trio.canvas");
        using OutlineHost host = HostOutline(document);
        host.FocusRow("beta");
        _ = Lines(document);
        _announced.Clear();

        document.CanvasDeleteSelection();
        host.Settle();

        Assert.Equal("gamma", FocusedRowId());
        string line = Assert.Single(Lines(document));
        Assert.StartsWith("Deleted ", line, StringComparison.Ordinal);
        Assert.EndsWith($"{CanvasPhrase.UndoChord} to undo", line, StringComparison.Ordinal);
    });

    /// <summary>The issue's other half: the canvas chords answer from where
    /// the keys landed, with no F6 first — Ctrl+Z brings the card back.</summary>
    [Fact]
    public void UndoFromTheSeatedRowRestoresTheCard() => RunSta(() =>
    {
        CanvasDocumentViewModel document = Open("trio.canvas");
        using OutlineHost host = HostOutline(document);
        host.FocusRow("beta");
        document.CanvasDeleteSelection();
        host.Settle();
        Assert.DoesNotContain(document.Outline, row => row.NodeId == "beta");

        host.Press(Key.Z, ModifierKeys.Control);

        Assert.True(
            document.Outline.Any(row => row.NodeId == "beta"),
            "Ctrl+Z from " + FocusedRowId() + " did not restore Beta; heard: " + string.Join(" | ", Lines(document)));
    });

    /// <summary>The keys' lineage clears when the reader goes to another
    /// pane: a delete published after that leaves the keys where the reader
    /// put them and seats nothing (the operation's cleared seat stands).</summary>
    [Fact]
    public void ADeleteAfterTheKeysLeftTheOutlineLeavesThemWhereTheyAre() => RunSta(() =>
    {
        CanvasDocumentViewModel document = Open("trio.canvas");
        using OutlineHost host = HostOutline(document);
        host.FocusRow("beta");
        Assert.True(host.Beside.Focus());

        document.CanvasDeleteSelection();
        host.Settle();

        Assert.Same(host.Beside, Keyboard.FocusedElement);
        Assert.Null(document.Selection.Selected);
    });

    /// <summary>WB-13 red team (H1): the keys left the outline for an overlay
    /// (the palette) whose command showed the visual board. A delete on the
    /// board leaves the seat the operation cleared — the hidden outline seats
    /// no successor, which would pan the board to it.</summary>
    [Fact]
    public void ABoardDeleteAfterAnOverlayLeftTheOutlineSeatsNothing() => RunSta(() =>
    {
        CanvasDocumentViewModel document = Open("trio.canvas");
        using OutlineHost host = HostOutline(document);
        host.FocusRow("beta");
        host.LeaveForAnOverlay();
        document.ShowSurface(CanvasSurfaceKind.Visual);
        host.Settle();
        Assert.Equal("beta", document.Selection.Selected);

        document.CanvasDeleteSelection();
        host.Settle();

        Assert.Null(document.Selection.Selected);
    });

    /// <summary>WB-13 red team (H1): a filter that hides the seated card is
    /// selection-neutral — the kept lineage seats no successor for a
    /// publication that is not a mutation's.</summary>
    [Fact]
    public void AFilterAfterAnOverlayLeftTheOutlineMovesNoSeat() => RunSta(() =>
    {
        CanvasDocumentViewModel document = Open("trio.canvas");
        using OutlineHost host = HostOutline(document);
        host.FocusRow("beta");
        host.LeaveForAnOverlay();

        document.FilterText = "Alpha";
        Drain(document);
        host.Settle();

        Assert.Equal("beta", document.Selection.Selected);
    });

    /// <summary>WB-13 red team (H1): the overlay closed and its restore put
    /// the keys somewhere else — the lineage ends there, so a later delete
    /// leaves them where they are and seats nothing.</summary>
    [Fact]
    public void TheLineageEndsWhenTheKeysLandElsewhereAfterAnOverlay() => RunSta(() =>
    {
        CanvasDocumentViewModel document = Open("trio.canvas");
        using OutlineHost host = HostOutline(document);
        host.FocusRow("beta");
        host.LeaveForAnOverlay();
        Assert.True(host.Aside.Focus());

        document.CanvasDeleteSelection();
        host.Settle();

        Assert.Same(host.Aside, Keyboard.FocusedElement);
        Assert.Null(document.Selection.Selected);
    });

    /// <summary>W6-1 §E TE-4: the delete's publication carries its receipt,
    /// and the outline's return of the keys marks it — once.</summary>
    [Fact]
    public void ADeleteMarksItsFocusReturnOnce() => RunSta(() =>
    {
        CanvasDocumentViewModel document = Open("trio.canvas");
        using OutlineHost host = HostOutline(document);
        host.FocusRow("beta");
        var receipts = new List<CanvasOperationCompletion>();
        document.OutlinePublished += (_, e) =>
        {
            if (e is CanvasOutlinePublishedEventArgs { Receipt: { } receipt })
            {
                receipts.Add(receipt);
            }
        };

        document.CanvasDeleteSelection();
        host.Settle();

        Assert.Equal("gamma", FocusedRowId());
        CanvasOperationCompletion published = Assert.Single(receipts);
        Assert.False(
            published.TryMarkFocusReturned(),
            "the outline returned the keys without marking the operation's receipt");
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
        return new OutlineHost(window, surface, above, left);
    }

    private sealed class OutlineHost(Window window, CanvasSurfaceView surface, Button beside, Button aside) : IDisposable
    {
        private readonly List<IInputElement> _focusChanges = [];
        private bool _recording;

        internal CanvasSurfaceView Surface { get; } = surface;

        internal Button Beside { get; } = beside;

        /// <summary>A second stop outside the outline.</summary>
        internal Button Aside { get; } = aside;

        /// <summary>The keys leave the outline for a shell overlay (the
        /// palette): the loss classifies as <c>ModalOverlay</c> while the
        /// overlay probe answers true, and the keys sit on
        /// <see cref="Beside"/> as they would in the overlay's field.</summary>
        internal void LeaveForAnOverlay()
        {
            Func<bool> probe = CanvasSurfaceView.ShellOverlayIsOpen;
            CanvasSurfaceView.ShellOverlayIsOpen = static () => true;
            try
            {
                Assert.True(Beside.Focus());
            }
            finally
            {
                CanvasSurfaceView.ShellOverlayIsOpen = probe;
            }
        }

        internal void UpdateLayout() => window.UpdateLayout();

        /// <summary>Layout, then everything queued.</summary>
        internal void Settle()
        {
            window.UpdateLayout();
            PumpedDispatcher.Drain();
            window.UpdateLayout();
        }

        /// <summary>The card seated and its row holding the keys, as a
        /// reader who arrowed to it has them.</summary>
        internal void FocusRow(string nodeId)
        {
            Assert.True(Beside.Focus());
            Surface.Model!.SeatSelectionSilently(nodeId);
            window.UpdateLayout();
            Assert.True(Surface.FocusProjection());
            Assert.Equal(nodeId, FocusedRowId());
        }

        /// <summary>A real press through the input manager, with exactly
        /// these modifiers held in the thread's key state.</summary>
        internal void Press(Key key, ModifierKeys modifiers) =>
            CommandPaletteTests.ThreadModifiers.Hold(modifiers, () => Press(key));

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
