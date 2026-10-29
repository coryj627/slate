// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Input;
using SlateWindows.Canvas;
using SlateWindows.Commands;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// WB-13 (#1260): the palette's Delete on a card whose outline row held the
/// keys when the palette opened. The palette holds the keys while the
/// command runs and gives them back afterwards through its own restore, whose
/// token is the deleted row's dead container; the restore lands that token
/// on the outline's seated row (contract 40 R-5 (h)). So the outline seats
/// the successor without taking the keys out of the palette, and the
/// restore lands there — never on the first row, which is where it lands
/// with nothing seated. In production the delete's publication is posted
/// after a worker transaction, so it can arrive while the palette is open,
/// between its dismissal and its restore, or after its restore; one fact per
/// order, in the shipped shell.
/// </summary>
public sealed partial class CommandPaletteTests
{
    private const string TrioCanvas = """
        {
          "nodes": [
            {"id":"alpha","type":"text","text":"Alpha","x":0,"y":0,"width":200,"height":100},
            {"id":"beta","type":"text","text":"Beta","x":300,"y":0,"width":200,"height":100},
            {"id":"gamma","type":"text","text":"Gamma","x":600,"y":0,"width":200,"height":100}
          ],
          "edges": []
        }
        """;

    /// <summary>No hold: the synchronous document publishes inside the
    /// palette's invoke, with the palette open and its field holding the
    /// keys. The outline seats the successor and leaves the keys alone. The
    /// state is captured inside the command and asserted after it: the
    /// registry turns an exception thrown there into a failed command.</summary>
    [Fact]
    public void APaletteDeletePublishedWhileThePaletteHoldsTheKeysLandsOnTheSuccessor() => RunSta(() =>
    {
        using var delete = new PaletteCanvasDelete();
        bool? paletteOpen = null;
        bool? outlineHadTheKeys = null;
        IInputElement? keysAfterTheRebuild = null;
        string? seatAfterTheRebuild = null;
        delete.OnPublished = () =>
        {
            paletteOpen = delete.Host.Palette.IsOpen;
            outlineHadTheKeys = delete.Outline.HasKeyboardFocus;
        };
        delete.AfterTheOutlineRebuilt = () =>
        {
            keysAfterTheRebuild = Keyboard.FocusedElement;
            seatAfterTheRebuild = delete.Document.Selection.Selected;
        };

        delete.Run();

        Assert.True(paletteOpen);
        Assert.False(outlineHadTheKeys);
        Assert.Same(delete.Host.Shell.CommandPaletteSearchTextBox, keysAfterTheRebuild);
        Assert.Equal("gamma", seatAfterTheRebuild);
        delete.AssertLandedOnTheSuccessor();
    });

    /// <summary>The publication runs from the palette's Dismissed — after
    /// it closed and posted its Input-priority restore, before that restore
    /// runs. The palette is closed and the outline has no keys: the lineage
    /// kept since the keys left for the palette is what seats the
    /// successor.</summary>
    [Fact]
    public void APaletteDeletePublishedBetweenDismissalAndRestoreLandsOnTheSuccessor() => RunSta(() =>
    {
        using var delete = new PaletteCanvasDelete();
        Action? held = null;
        delete.Document.HoldMutationForTests = apply => held = apply;
        delete.Host.Palette.Dismissed += (_, _) =>
        {
            Action? apply = held;
            held = null;
            apply?.Invoke();
        };
        delete.OnPublished = () =>
        {
            Assert.False(delete.Host.Palette.IsOpen);
            Assert.False(delete.Outline.HasKeyboardFocus);
        };

        delete.Run();

        Assert.Null(held);
        delete.AssertLandedOnTheSuccessor();
    });

    /// <summary>The publication runs after the restore put the keys back on
    /// the live row: the outline holds them, and delivers the successor
    /// itself.</summary>
    [Fact]
    public void APaletteDeletePublishedAfterThePalettesRestoreLandsOnTheSuccessor() => RunSta(() =>
    {
        using var delete = new PaletteCanvasDelete();
        Action? held = null;
        delete.Document.HoldMutationForTests = apply => held = apply;
        delete.OnPublished = () => Assert.True(delete.Outline.HasKeyboardFocus);

        delete.Run();
        Assert.Equal("beta", RowIdOf(Keyboard.FocusedElement));
        Assert.NotNull(held);
        held!();
        delete.Host.Settle();

        delete.AssertLandedOnTheSuccessor();
    });

    private static string? RowIdOf(IInputElement? focus) =>
        (focus as FrameworkElement)?.DataContext is CanvasOutlineRowViewModel row ? row.Id : null;

    /// <summary>The shipped shell showing <c>trio.canvas</c> — Alpha, Beta,
    /// Gamma — as the outline, Beta's row holding the keys, then the palette
    /// opened and its Delete Selection row entered.</summary>
    private sealed class PaletteCanvasDelete : IDisposable
    {
        private readonly List<RenderedAnnouncement> _announced = [];
        private CanvasOperationCompletion? _receipt;
        private int _publications;

        public PaletteCanvasDelete()
        {
            Host = new PaletteShellHost(root => File.WriteAllText(Path.Combine(root, "trio.canvas"), TrioCanvas));
            Document = Host.AttachCanvasWorkspace("trio.canvas", _announced.Add);
            Outline = Host.CanvasSurface().OutlineForTests;
            Assert.Equal(CanvasSurfaceKind.Outline, Document.Selection.ActiveSurface);
            Document.SeatSelectionSilently("beta");
            Host.Settle();
            Assert.True(Outline.FocusTree(), "keyboard premise: Beta's row did not take the keys");
            Assert.Equal("beta", RowIdOf(Keyboard.FocusedElement));
            Document.AnnouncerForTests.FlushForTests();
            _announced.Clear();
            // After the outline's own handler, which subscribed when the
            // surface took the model.
            Document.OutlinePublished += (_, e) =>
            {
                if (e is not CanvasOutlinePublishedEventArgs { Receipt: { } receipt })
                {
                    return;
                }
                _receipt = receipt;
                _publications++;
                AfterTheOutlineRebuilt?.Invoke();
            };
        }

        public PaletteShellHost Host { get; }

        public CanvasDocumentViewModel Document { get; }

        public CanvasOutlineView Outline { get; }

        /// <summary>Checked as the delete's publication starts.</summary>
        public Action? OnPublished { get; set; }

        /// <summary>Checked once the outline has rebuilt from it.</summary>
        public Action? AfterTheOutlineRebuilt { get; set; }

        /// <summary>Open the palette over Beta's row, Enter on Delete
        /// Selection, and let everything queued — the restore included —
        /// run.</summary>
        public void Run()
        {
            Host.OpenThePalette();
            Host.AssertFocusIsOn(Host.Shell.CommandPaletteSearchTextBox);
            // Ahead of the outline's handler: the state the publication
            // finds, before the rebuild changes it.
            EventHandler early = (_, e) =>
            {
                if (e is CanvasOutlinePublishedEventArgs)
                {
                    OnPublished?.Invoke();
                }
            };
            InsertFirst(early);
            Host.Palette.Select(Assert.Single(Host.Palette.Rows, row => row.Id == ChordTable.Ids.CanvasDelete));
            Host.PressEnterInThePalette();
            Host.Settle();
        }

        /// <summary>The common end of every order: the keys on Gamma's row
        /// and Gamma seated; on the way, no stop on Alpha's row or the bare
        /// tree; one sentence, the delete's; the receipt already marked by
        /// the outline's return.</summary>
        public void AssertLandedOnTheSuccessor()
        {
            Assert.Equal(1, _publications);
            Assert.False(Host.Palette.IsOpen);
            Assert.IsType<CanvasOutlineItem>(Keyboard.FocusedElement);
            Assert.Equal("gamma", RowIdOf(Keyboard.FocusedElement));
            Assert.Equal("gamma", Document.Selection.Selected);
            List<IInputElement> moves = Host.FocusChangesSinceEnter;
            Assert.Equal("gamma", RowIdOf(moves[^1]));
            Assert.DoesNotContain(moves, focus => RowIdOf(focus) == "alpha");
            Assert.DoesNotContain(moves, focus => ReferenceEquals(focus, Outline.TreeForTests));
            Document.AnnouncerForTests.FlushForTests();
            string line = Assert.Single(_announced).Text;
            Assert.StartsWith("Deleted ", line, StringComparison.Ordinal);
            Assert.EndsWith($"{CanvasPhrase.UndoChord} to undo", line, StringComparison.Ordinal);
            Assert.NotNull(_receipt);
            Assert.False(
                _receipt!.TryMarkFocusReturned(),
                "the keys came back to the successor without the outline marking the operation's receipt");
        }

        public void Dispose()
        {
            Document.HoldMutationForTests = null;
            Host.Dispose();
        }

        /// <summary>Puts a handler ahead of every subscriber already on
        /// <see cref="CanvasDocumentViewModel.OutlinePublished"/>.</summary>
        private void InsertFirst(EventHandler handler)
        {
            System.Reflection.FieldInfo field = typeof(CanvasDocumentViewModel).GetField(
                    nameof(CanvasDocumentViewModel.OutlinePublished),
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?? throw new InvalidOperationException("CanvasDocumentViewModel.OutlinePublished's field is gone");
            var existing = (EventHandler?)field.GetValue(Document);
            field.SetValue(Document, (EventHandler)Delegate.Combine(handler, existing));
        }
    }
}
