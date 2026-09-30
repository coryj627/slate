// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Controls;
using System.Windows.Input;
using SlateWindows.Canvas;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>W7-7 PR 4 (#1247; codex PR 4 round 6 high 1, the owner's
/// decision): the canvas view switcher's arrow commit is one
/// utterance.</summary>
public sealed partial class CanvasNavigatorTests
{
    /// <summary>An arrow on the canvas's view switcher commits the
    /// projection it reaches as ONE spoken outcome: the destination radio is
    /// already checked when it takes focus, it keeps the keys, and no
    /// authored line (CanvasSurfaceShown) is posted on top of the focus
    /// speech. A click, Space and the show commands still speak it.</summary>
    [Fact]
    public void AnArrowOnTheSwitcherCommitsAsOneUtterance() => RunSta(() =>
    {
        CanvasDocumentViewModel document = Open("board.canvas");
        using OutlineHost host = HostOutline(document);
        RadioButton outline = host.Surface.OutlineChoiceForTests;
        RadioButton table = host.Surface.TableChoiceForTests;
        Assert.True(outline.IsChecked);
        Assert.True(outline.Focus());
        Drain(document);
        bool? checkedWhenFocused = null;
        table.GotKeyboardFocus += (_, _) => checkedWhenFocused ??= table.IsChecked;
        host.RecordFocus();

        host.Press(Key.Right);

        IReadOnlyList<string> lines = Lines(document);
        Assert.Equal(CanvasSurfaceKind.Table, document.Selection.ActiveSurface);
        Assert.True(checkedWhenFocused == true, "the destination took focus UNCHECKED");
        Assert.Same(table, Keyboard.FocusedElement);
        Assert.True(lines.Count == 0, $"the arrow posted authored line(s): {string.Join(" | ", lines)}");

        // The command route is not an arrow's: it still speaks.
        document.ShowSurface(CanvasSurfaceKind.Outline);
        Assert.NotEmpty(Lines(document));
    });
}
