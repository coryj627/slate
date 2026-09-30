// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using SlateWindows.Bases;
using SlateWindows.Canvas;
using SlateWindows.Grids;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W7-7 PR 7 (#1252, R-9) over PR 4 (#1247, R-5): the rescan's re-seat of a
/// board or base renamed outside Slate (case only) replaces the tab's
/// document under the reader, through the workspace's replace. Each surface
/// is bound to its tab as the shell's template binds it, in a shown window,
/// so focus really moves: the keys come back to the reader's own row of the
/// document at the stored spelling — never stranded, never the first row.
/// </summary>
public sealed partial class RescanTests
{
    /// <summary>The keys on the board's SECOND card in the outline: the
    /// re-seated board starts from the retired board's selection (a rename's
    /// seed, CD-32), so the outline's landing puts them back on that card's
    /// row, and the run speaks only its sentence.</summary>
    [Fact]
    public void ACaseOnlyReseatKeepsTheKeysOnTheReadersCard() => RunSta(() =>
    {
        using var h = new Harness("reseat-canvas-keys", ("board.canvas", TwoNodeCanvas));
        if (!h.VolumeAliasesCase())
        {
            return;
        }

        WorkspaceTabViewModel tab = h.Open("board.canvas");
        h.PumpUntil(() => tab.Canvas?.RowFor("added") is not null, "the board's first load");
        CanvasDocumentViewModel retired = Assert.IsType<CanvasDocumentViewModel>(tab.Canvas);
        var surface = new CanvasSurfaceView { DataContext = tab };
        _ = surface.SetBinding(CanvasSurfaceView.ModelProperty, new Binding(nameof(WorkspaceTabViewModel.Canvas)));
        Window window = ShowHosted(surface);
        try
        {
            Assert.Equal(CanvasSurfaceKind.Outline, surface.Projection);
            Assert.True(surface.FocusRow("added"));
            PumpedDispatcher.Drain();
            Assert.Equal("added", FocusedOutlineNode());
            h.Events.Clear();

            File.Move(Path.Combine(h.Root, "board.canvas"), Path.Combine(h.Root, "Board.canvas"));
            h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
            h.PumpUntil(
                () => tab.Canvas is { Path: "Board.canvas" } board && board.RowFor("added") is not null,
                "the re-seated board's load");
            Settle(window);

            CanvasDocumentViewModel reseated = Assert.IsType<CanvasDocumentViewModel>(tab.Canvas);
            Assert.NotSame(retired, reseated);
            AssertKeysInside(surface);
            Assert.IsAssignableFrom<TreeViewItem>(Keyboard.FocusedElement);
            Assert.Equal("added", FocusedOutlineNode());
            Assert.Equal("added", reseated.Selection.Selected);
            Assert.Equal(CanvasSurfaceKind.Outline, surface.Projection);
            Assert.Equal(["Files refreshed. 1 new or changed, 1 removed."], h.Spoken);
        }
        finally
        {
            window.Close();
        }
    });

    /// <summary>The keys on a base's MIDDLE row: the re-seated base's first
    /// publication binds its grid through the grid's key restore, which reads
    /// the reader's row key off the retired document's rows, so the keys stay
    /// on that note's row.</summary>
    [Fact]
    public void ACaseOnlyReseatKeepsTheKeysOnTheReadersBaseRow() => RunSta(() =>
    {
        using var h = new Harness(
            "reseat-base-keys",
            ("notes.base", NamedColumnsBase),
            ("b.md", "# B\n"),
            ("d.md", "# D\n"),
            ("f.md", "# F\n"));
        if (!h.VolumeAliasesCase())
        {
            return;
        }

        WorkspaceTabViewModel tab = h.Open("notes.base");
        h.PumpUntil(() => tab.Base?.State == BaseLoadState.Ready, "the base's first load");
        BaseDocumentViewModel retired = Assert.IsType<BaseDocumentViewModel>(tab.Base);
        var surface = new BaseSurfaceView { DataContext = tab };
        _ = surface.SetBinding(BaseSurfaceView.TabProperty, new Binding());
        _ = surface.SetBinding(BaseSurfaceView.ModelProperty, new Binding(nameof(WorkspaceTabViewModel.Base)));
        Window window = ShowHosted(surface);
        List<IInputElement> changes = RecordFocusChanges(window);
        try
        {
            AccessibleDataGrid grid = surface.GridForTests;
            Assert.True(grid.SelectRow(row => ((BaseGridRowViewModel)row).RowKey == "d.md", moveFocus: true));
            PumpedDispatcher.Drain();
            Assert.Equal("d.md", RowKeyOfFocusedCell());
            h.Events.Clear();
            changes.Clear();
            OpenALoadingGap(h);

            File.Move(Path.Combine(h.Root, "notes.base"), Path.Combine(h.Root, "Notes.base"));
            h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
            h.PumpUntil(
                () => tab.Base is { Path: "Notes.base", State: BaseLoadState.Ready },
                "the re-seated base's load");
            Settle(window);

            Assert.NotSame(retired, tab.Base);
            AssertKeysInside(surface);
            Assert.Equal("d.md", RowKeyOfFocusedCell());
            AssertOneFocusChangeNeverTheWindow(changes);
            Assert.Equal(["Files refreshed. 1 new or changed, 1 removed."], h.Spoken);
        }
        finally
        {
            window.Close();
        }
    });

    private const string ListViewBase =
        "filters: 'file.ext == \"md\"'\nviews:\n  - type: list\n    name: Main\n";

    /// <summary>Codex's merge-delta check (finding 2): the keys on a
    /// list-view base's THIRD row. The re-seat swapped in a document with
    /// nothing published: the list collapsed under the keys, WPF stranded
    /// them on the window, and the new document knew no reader row. Now the
    /// list keeps its rows while they hold the keys, the new document starts
    /// from the retired one's reader row, and its first publication lands
    /// the keys on that row — one focus change, never the window.</summary>
    [Fact]
    public void ACaseOnlyReseatKeepsTheKeysOnTheReadersListRow() => RunSta(() =>
    {
        using var h = new Harness(
            "reseat-list-keys",
            ("notes.base", ListViewBase),
            ("b.md", "# B\n"),
            ("d.md", "# D\n"),
            ("f.md", "# F\n"),
            ("h.md", "# H\n"));
        if (!h.VolumeAliasesCase())
        {
            return;
        }

        WorkspaceTabViewModel tab = h.Open("notes.base");
        h.PumpUntil(() => tab.Base?.State == BaseLoadState.Ready, "the base's first load");
        BaseDocumentViewModel retired = Assert.IsType<BaseDocumentViewModel>(tab.Base);
        var surface = new BaseSurfaceView { DataContext = tab };
        _ = surface.SetBinding(BaseSurfaceView.TabProperty, new Binding());
        _ = surface.SetBinding(BaseSurfaceView.ModelProperty, new Binding(nameof(WorkspaceTabViewModel.Base)));
        Window window = ShowHosted(surface);
        List<IInputElement> changes = RecordFocusChanges(window);
        try
        {
            ListBox list = surface.ListForTests;
            Assert.True(list.IsVisible, "premise: the base renders as a list.");
            BaseListItemViewModel reader = list.Items.Cast<BaseListItemViewModel>()
                .Single(item => item.Row?.FilePath == "f.md");
            Assert.NotSame(reader, list.Items[0]);
            list.SelectedItem = reader;
            list.UpdateLayout();
            Assert.True(Assert.IsType<ListBoxItem>(list.ItemContainerGenerator.ContainerFromItem(reader)).Focus());
            PumpedDispatcher.Drain();
            Assert.Equal("f.md", ListRowWithKeys());
            h.Events.Clear();
            changes.Clear();
            OpenALoadingGap(h);

            File.Move(Path.Combine(h.Root, "notes.base"), Path.Combine(h.Root, "Notes.base"));
            h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
            h.PumpUntil(
                () => tab.Base is { Path: "Notes.base", State: BaseLoadState.Ready },
                "the re-seated base's load");
            Settle(window);

            Assert.NotSame(retired, tab.Base);
            AssertKeysInside(surface);
            Assert.Equal("f.md", ListRowWithKeys());
            Assert.Equal("f.md", tab.Base!.SelectedRow?.FilePath);
            AssertOneFocusChangeNeverTheWindow(changes);
            Assert.Equal(["Files refreshed. 1 new or changed, 1 removed."], h.Spoken);
        }
        finally
        {
            window.Close();
        }
    });

    /// <summary>Production's gap: a re-seated base's document loads on a
    /// worker, so the shell draws it between the re-seat and its first
    /// publication. The harness loads inline; this parks the re-seat's load
    /// at the rescan's publication seam for a few dispatcher turns.</summary>
    private static void OpenALoadingGap(Harness h) =>
        h.Workspace.RescanPublicationForTests = async (reloading, _, reload) =>
        {
            if (reloading == "base")
            {
                await Task.Delay(150);
            }

            await reload();
        };

    private static string? ListRowWithKeys() =>
        Keyboard.FocusedElement is ListBoxItem { DataContext: BaseListItemViewModel { Row: { } row } } ? row.FilePath : null;

    /// <summary>Every keyboard focus change in <paramref name="window"/> from
    /// now on, handled or not.</summary>
    private static List<IInputElement> RecordFocusChanges(Window window)
    {
        var changes = new List<IInputElement>();
        window.AddHandler(
            Keyboard.GotKeyboardFocusEvent,
            new KeyboardFocusChangedEventHandler((_, e) => changes.Add(e.NewFocus)),
            handledEventsToo: true);
        return changes;
    }

    /// <summary>The keys moved once — from the reader's row to its
    /// successor — and never through the window.</summary>
    private static void AssertOneFocusChangeNeverTheWindow(List<IInputElement> changes) =>
        Assert.True(
            changes.Count == 1 && changes[0] is not Window,
            "the keys moved " + string.Join(" → ", changes.Select(focus => focus.GetType().Name)));

    private static Window ShowHosted(FrameworkElement surface)
    {
        var window = new Window
        {
            Content = surface,
            Width = 700,
            Height = 500,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
            ShowActivated = false,
        };
        window.Show();
        window.UpdateLayout();
        return window;
    }

    /// <summary>Two rounds of layout and a drain: the re-seat's model swap,
    /// then its load's publication and the landings each schedules.</summary>
    private static void Settle(Window window)
    {
        for (int round = 0; round < 2; round++)
        {
            window.UpdateLayout();
            PumpedDispatcher.Drain();
        }
    }

    /// <summary>The keys are on a live element of the surface — not on
    /// nothing, the window, or a row the re-seat detached.</summary>
    private static void AssertKeysInside(FrameworkElement surface)
    {
        IInputElement? focused = Keyboard.FocusedElement;
        Assert.True(
            focused is Visual visual
                && PresentationSource.FromVisual(visual) is not null
                && visual.IsDescendantOf(surface),
            $"the re-seat left the keys on {Describe(focused)}");
    }

    private static string? FocusedOutlineNode() =>
        Keyboard.FocusedElement is FrameworkElement { DataContext: CanvasOutlineRowViewModel row } ? row.Id : null;

    private static string Describe(IInputElement? focused) => focused switch
    {
        null => "nothing",
        FrameworkElement element =>
            $"{element.GetType().Name} (data {element.DataContext?.GetType().Name ?? "none"}, "
            + $"attached {PresentationSource.FromVisual(element) is not null}, visible {element.IsVisible})",
        _ => focused.GetType().Name,
    };
}
