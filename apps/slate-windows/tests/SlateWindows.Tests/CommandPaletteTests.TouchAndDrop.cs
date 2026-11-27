// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using SlateWindows.Grids;
using SlateWindows.Reading;

namespace SlateWindows.Tests;

/// <summary>
/// Plan 47 CB-38 (#1695): the seal takes a touch's own events and a drop
/// from another app. A touch pans a list from its own events, and a drag it
/// pans promotes no press; a window a prompt disabled still receives an OLE
/// drop; so the press gate saw neither. The window takes the three touch
/// previews and the three drag previews through <c>SealTakes</c>, which
/// tells a drag's source that nothing moved. The hosted facts drive a
/// synthetic touch device, which starts a manipulation as a digitizer's
/// touch does and needs no touch hardware, and raise a drop's routed events
/// in the order WPF's OLE drop target raises them. The two interactive
/// facts drive real touch and a real OLE drop, and skip where the desktop
/// refuses either, as the menu facts do (#1680); the touch fact also skips,
/// on its own cause, where WPF in its test process has not registered the
/// injected touch device.
/// </summary>
public sealed partial class CommandPaletteTests
{
    /// <summary>The text every drop here carries.</summary>
    private const string DroppedText = "cb38drop";

    /// <summary>A touch drag on the outline list, the shell window's own list
    /// that pans by touch, pans nothing while the seal holds and pans once it
    /// lifts.</summary>
    [Fact]
    public void ASealedTouchDragPansNoShellList() => RunSta(() =>
    {
        using var host = new ShippedShellHost(
            shown: true,
            seed: root => File.WriteAllText(Path.Combine(root, "long.md"), NoteWithManyHeadings()));
        _ = host.AttachWorkspace(_ => { }, "long.md");
        ListBox outline = host.Shell.PanelOutlineList;
        Assert.True(
            PumpedDispatcher.PumpUntil(() => outline.IsVisible && outline.Items.Count >= 30, TimeSpan.FromSeconds(20)),
            $"premise: the outline list never showed the note's headings (visible {outline.IsVisible}, rows {outline.Items.Count})");
        outline.UpdateLayout();
        ScrollViewer viewer = Descendants<ScrollViewer>(outline).First();
        Assert.True(
            viewer.PanningMode != PanningMode.None,
            "premise: the outline list pans by touch in this host, so a drag has something to move");
        Assert.True(viewer.ScrollableHeight > 0, "premise: the outline list has rows below its viewport");
        Point start = RowPointIn(outline, 6);
        string Offset() => string.Create(CultureInfo.InvariantCulture, $"offset {viewer.VerticalOffset}");

        string before = SettledIn(host.Shell, Offset);
        string sealedAfter;
        ComponentDispatcher.PushModal();
        try
        {
            Assert.True(host.Palette.IsSealed, "premise: the modal section sealed the shell");
            SyntheticTouchDrag(outline, start, new Vector(0, -150));
            sealedAfter = SettledIn(host.Shell, Offset);
        }
        finally
        {
            ComponentDispatcher.PopModal();
        }

        Assert.False(host.Palette.IsSealed);
        string unsealedBefore = SettledIn(host.Shell, Offset);
        SyntheticTouchDrag(outline, start, new Vector(0, -150));
        string unsealedAfter = SettledIn(host.Shell, Offset);

        Assert.True(sealedAfter == before, $"a touch drag panned the outline list under the seal: {before} to {sealedAfter}");
        Assert.True(
            unsealedAfter != unsealedBefore,
            $"a touch drag did not pan the outline list once the seal lifted ({unsealedBefore}), so the fact proves nothing");
    });

    /// <summary>A drop from another app lands nothing under the seal, moves
    /// no keys, and tells its source None at every step, so a source that
    /// deletes on Move keeps its text. Once the seal lifts the same drop
    /// lands.</summary>
    [Theory]
    [InlineData("editor")]
    [InlineData("files-filter")]
    public void ASealedDropLandsNothingAndTellsTheSourceNone(string target) => RunSta(() =>
    {
        using var host = new ShippedShellHost(shown: true);
        _ = host.AttachFilesSidebar();
        WorkspaceViewModel workspace = host.AttachWorkspace(_ => { }, "note0.md");
        (UIElement over, Point at, Func<string> landed) = target switch
        {
            "editor" => EditorDropTarget(host, workspace),
            "files-filter" => FilterDropTarget(host),
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, "no such drop target"),
        };

        if (over.IsKeyboardFocusWithin)
        {
            // The note's open lands the keys in its editor; a drop that moves
            // them there shows only from elsewhere.
            Assert.True(host.Shell.SidebarFilterTextBox.Focus(), "premise: the Files filter field took the keys");
        }

        string before = SettledIn(host.Shell, landed);
        IInputElement? keys = Keyboard.FocusedElement;
        Assert.False(over.IsKeyboardFocusWithin, $"premise: the keys start outside the {target}");
        string toldSealed;
        string sealedAfter;
        IInputElement? keysAfter;
        ComponentDispatcher.PushModal();
        try
        {
            Assert.True(host.Palette.IsSealed, "premise: the modal section sealed the shell");
            toldSealed = RoutedDrop(over, at, DroppedText);
            sealedAfter = SettledIn(host.Shell, landed);
            keysAfter = Keyboard.FocusedElement;
        }
        finally
        {
            ComponentDispatcher.PopModal();
        }

        Assert.False(host.Palette.IsSealed);
        string unsealedBefore = SettledIn(host.Shell, landed);
        string toldUnsealed = RoutedDrop(over, at, DroppedText);
        string unsealedAfter = SettledIn(host.Shell, landed);

        Assert.True(sealedAfter == before, $"a drop landed in the {target} under the seal: {before} to {sealedAfter} (the source was told {toldSealed})");
        Assert.True(
            ReferenceEquals(keysAfter, keys),
            $"a drop moved the keys under the seal, from {keys?.GetType().Name ?? "nothing"} to {keysAfter?.GetType().Name ?? "nothing"}");
        Assert.True(
            toldSealed == "None;None;None",
            $"under the seal the source was told {toldSealed} for DragEnter, DragOver and Drop; it must be told None at "
            + "each, or a source that deletes on Move loses its text");
        Assert.True(
            unsealedAfter != unsealedBefore,
            $"a drop did nothing in the {target} once the seal lifted ({unsealedBefore}, told {toldUnsealed}), so the fact proves nothing");
    });

    /// <summary>
    /// Real touch on the reading table under the seal (a thread-modal
    /// section, which disables no window): a drag under the seal reaches the
    /// table and pans nothing; the first tap once the seal lifts moves the
    /// current row (a take of the stylus previews broke that tap in step 0);
    /// and a drag then pans, which makes the sealed drag's stillness mean
    /// something. It skips where the desktop refuses the foreground, touch
    /// injection or the touch itself (#1680), and, on a cause of its own,
    /// where WPF in this test process has not registered the injected touch
    /// device: it runs alone as well as in the whole run.
    /// </summary>
    [Fact]
    public void ARealTouchPanAndTheNextTap() => RunSta(() =>
    {
        RealInputDesk.PrepareTouchInjection();
        using var host = new ShippedShellHost(shown: true);
        using var desk = new RealInputDesk(host.Shell, "a real touch pan on the reading table");
        Window table = desk.TableInTheColumn(host.Shell);
        try
        {
            DataGrid grid = Assert.IsType<AccessibleDataGrid>(table.Content).Grid;
            ScrollViewer viewer = Descendants<ScrollViewer>(grid).First();

            // A touch reaches the table as stylus events first, and the
            // stylus up is raised whatever takes the touch, so it says the
            // contact arrived; counted past handled.
            int stylusUps = 0;
            table.AddHandler(UIElement.PreviewStylusUpEvent, new StylusEventHandler((_, _) => stylusUps++), handledEventsToo: true);

            // Real touch must reach the table before anything is measured:
            // tap the first row, where the keys already are, until a touch
            // arrives, and skip where none does.
            bool arrived = false;
            for (int attempt = 0; attempt < 5 && !arrived; attempt++)
            {
                if (desk.RealTouch(RealInputDesk.TouchTapFrames(grid, CellPointIn(grid, 0))) is { } refused)
                {
                    RealInputDesk.SkipWhereThisDesktopRefusesRealInput($"touch injection ({refused})", "a real touch pan");
                }

                arrived = PumpedDispatcher.PumpUntil(() => stylusUps > 0, TimeSpan.FromSeconds(1));
            }

            if (!arrived)
            {
                RealInputDesk.SkipWhereThisDesktopRefusesRealInput("real touch on the reading table", "a real touch pan");
            }

            // Once a touch has reached the table, injection that fails is a
            // failure of the fact, not the desktop refusing it.
            void Touch((Point Screen, uint Flags)[] frames)
            {
                if (desk.RealTouch(frames) is { } failed)
                {
                    Assert.Fail($"touch injection failed after a touch had reached the table: {failed}");
                }
            }

            PumpedDispatcher.Drain();
            double offset = viewer.VerticalOffset;
            int upsBefore = stylusUps;
            ComponentDispatcher.PushModal();
            try
            {
                Assert.True(host.Palette.IsSealed, "premise: the modal section sealed the shell");
                Touch(RealInputDesk.TouchDragFrames(grid, CellPointIn(grid, 8), new Vector(0, -150)));
                _ = PumpedDispatcher.PumpUntil(() => stylusUps > upsBefore, TimeSpan.FromSeconds(5));
            }
            finally
            {
                ComponentDispatcher.PopModal();
            }

            Assert.True(stylusUps > upsBefore, "premise: the sealed drag reached the reading table (no stylus up arrived there)");
            double sealedOffset = viewer.VerticalOffset;
            int row = CurrentRowOf(grid);
            Touch(RealInputDesk.TouchTapFrames(grid, CellPointIn(grid, 4)));
            bool tapped = PumpedDispatcher.PumpUntil(() => CurrentRowOf(grid) != row, TimeSpan.FromSeconds(5));

            double unsealedOffset = viewer.VerticalOffset;
            Touch(RealInputDesk.TouchDragFrames(grid, CellPointIn(grid, 8), new Vector(0, -150)));
            bool panned = PumpedDispatcher.PumpUntil(() => viewer.VerticalOffset != unsealedOffset, TimeSpan.FromSeconds(5));

            Assert.True(
                sealedOffset == offset,
                string.Create(CultureInfo.InvariantCulture, $"a real touch drag panned the reading table under the seal: offset {offset} to {sealedOffset}"));
            Assert.True(tapped, $"the first tap once the seal lifted left the current row on {row}; the seal broke the next tap");
            Assert.True(
                panned,
                string.Create(CultureInfo.InvariantCulture, $"a real touch drag did not pan the table once the seal lifted (offset {unsealedOffset}), so the fact proves nothing"));
        }
        finally
        {
            table.Close();
            PumpedDispatcher.Drain();
        }
    });

    /// <summary>
    /// A real OLE drop of text from another window onto the editor while a
    /// real WPF dialog the shell owns is up: the dialog disables the shell
    /// (and every other window on the thread), and OLE still offers the drop
    /// to it. Nothing lands and <c>DoDragDrop</c> returns None. Once the
    /// dialog closes the same drop lands, which makes the refusal mean
    /// something. It skips where the desktop refuses the foreground, the
    /// drag source's window or the drag (#1680).
    /// </summary>
    [Fact]
    public void ARealDropUnderAPromptLandsNothing() => RunSta(() =>
    {
        using var host = new ShippedShellHost(shown: true);
        WorkspaceViewModel workspace = host.AttachWorkspace(_ => { }, "note0.md");
        using var desk = new RealInputDesk(host.Shell, "a real drop under a prompt");
        (UIElement editor, Point at, Func<string> landed) = EditorDropTarget(host, workspace);
        int entered = 0;
        host.Shell.AddHandler(DragDrop.PreviewDragEnterEvent, new DragEventHandler((_, _) => entered++), handledEventsToo: true);

        string before = SettledIn(host.Shell, landed);
        (bool Sealed, bool ShellEnabled, DragDropEffects? Effect) underThePrompt =
            desk.UnderARealPrompt(() => (host.Palette.IsSealed, WindowIsEnabled(host.Shell), desk.RealOleDrop(editor, at, DroppedText)));
        if (underThePrompt.Effect is null || entered == 0)
        {
            RealInputDesk.SkipWhereThisDesktopRefusesRealInput("the drag", "a real drop under a prompt");
        }

        string sealedAfter = SettledIn(host.Shell, landed);
        Assert.False(host.Palette.IsSealed);
        string unsealedBefore = SettledIn(host.Shell, landed);
        DragDropEffects? unsealedEffect = desk.RealOleDrop(editor, at, DroppedText);
        if (unsealedEffect is null)
        {
            RealInputDesk.SkipWhereThisDesktopRefusesRealInput("the drag once the prompt closed", "a real drop under a prompt");
        }

        bool unsealedLanded = PumpedDispatcher.PumpUntil(() => landed() != unsealedBefore, TimeSpan.FromSeconds(5));

        Assert.True(
            underThePrompt is { Sealed: true, ShellEnabled: false },
            $"premise: the prompt sealed the shell and disabled its window (sealed {underThePrompt.Sealed}, enabled {underThePrompt.ShellEnabled})");
        Assert.True(sealedAfter == before, $"a real drop landed in the editor under a prompt: {before} to {sealedAfter}");
        Assert.True(
            underThePrompt.Effect == DragDropEffects.None,
            $"DoDragDrop returned {underThePrompt.Effect} under a prompt; it must return None, or a source that deletes on Move loses its text");
        Assert.True(
            unsealedLanded,
            $"a real drop did not land once the prompt closed ({unsealedBefore}, DoDragDrop returned {unsealedEffect}), so the fact proves nothing");
    });

    /// <summary>
    /// A touch drag on <paramref name="target"/> through a synthetic touch
    /// device: down at <paramref name="from"/> (in the target's coordinates),
    /// twelve moves along <paramref name="drag"/>, three still frames, then
    /// up. The device routes each frame through WPF's input manager as a
    /// digitizer's touch would, without the stylus stack, so it starts a
    /// manipulation and never promotes a press.
    /// </summary>
    internal static void SyntheticTouchDrag(UIElement target, Point from, Vector drag)
    {
        PresentationSource source = PresentationSource.FromVisual(target)
            ?? throw new InvalidOperationException("the touch target has no presentation source");
        Point start = target.TranslatePoint(from, (UIElement)source.RootVisual);
        var device = new SyntheticTouchDevice(source);
        NextTouchFrame(() => device.Down(start));
        for (int step = 1; step <= 12; step++)
        {
            NextTouchFrame(() => device.Move(start + (drag * step / 12)));
        }

        for (int still = 0; still < 3; still++)
        {
            NextTouchFrame(() => device.Move(start + drag));
        }

        NextTouchFrame(device.Up);
    }

    /// <summary>One frame of a synthetic touch, reported once the tick count
    /// has moved on, then pumped. WPF stamps a touch frame with the tick
    /// count, and a drag whose frames were reported inside one tick panned
    /// nothing, sealed or not; with each frame on a tick of its own the drag
    /// pans as a real one does.</summary>
    private static void NextTouchFrame(Action report)
    {
        long stamp = Environment.TickCount64;
        Assert.True(
            PumpedDispatcher.PumpUntil(() => Environment.TickCount64 != stamp, TimeSpan.FromSeconds(2)),
            "the tick count never moved on");
        report();
        PumpedDispatcher.Drain();
    }

    /// <summary>
    /// A drop of <paramref name="text"/> from another app, raised on
    /// <paramref name="target"/> at <paramref name="at"/> as WPF's OLE drop
    /// target raises it: for DragEnter, DragOver and Drop, one arguments
    /// object, raised as the preview and then, only while it stays
    /// unhandled, as the bubbling event. The effect it ends with is what OLE
    /// tells the source. Returns the three effects, separated by ";".
    /// </summary>
    internal static string RoutedDrop(UIElement target, Point at, string text)
    {
        var data = new DataObject(DataFormats.UnicodeText, text);
        var told = new List<string>();
        foreach ((RoutedEvent preview, RoutedEvent bubble) in new[]
        {
            (DragDrop.PreviewDragEnterEvent, DragDrop.DragEnterEvent),
            (DragDrop.PreviewDragOverEvent, DragDrop.DragOverEvent),
            (DragDrop.PreviewDropEvent, DragDrop.DropEvent),
        })
        {
            DragEventArgs args = DragArguments(data, target, at);
            args.RoutedEvent = preview;
            target.RaiseEvent(args);
            if (!args.Handled)
            {
                args.RoutedEvent = bubble;
                target.RaiseEvent(args);
            }

            told.Add(args.Effects.ToString());
        }

        return string.Join(";", told);
    }

    /// <summary>A drag of text entering <paramref name="target"/>: only its
    /// preview, raised as WPF's OLE drop target raises it, so a caller reads
    /// whether the window took it and what the source would be told.</summary>
    internal static DragEventArgs RaisePreviewDragEnter(UIElement target)
    {
        DragEventArgs args = DragArguments(new DataObject(DataFormats.UnicodeText, DroppedText), target, new Point(10, 10));
        args.RoutedEvent = DragDrop.PreviewDragEnterEvent;
        target.RaiseEvent(args);
        return args;
    }

    /// <summary>The effects a drag of text from another app offers.</summary>
    internal const DragDropEffects OfferedEffects = DragDropEffects.Copy | DragDropEffects.Move;

    /// <summary>Whether Windows lets <paramref name="window"/> take input; a
    /// modal prompt disables the window it is modal to.</summary>
    internal static bool WindowIsEnabled(Window window) =>
        TouchAndDropNative.IsWindowEnabled(new WindowInteropHelper(window).Handle);

    /// <summary>WPF builds a drag's arguments itself; its constructor is
    /// internal.</summary>
    private static DragEventArgs DragArguments(IDataObject data, DependencyObject target, Point at)
    {
        System.Reflection.ConstructorInfo constructor = typeof(DragEventArgs).GetConstructor(
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public,
                binder: null,
                [typeof(IDataObject), typeof(DragDropKeyStates), typeof(DragDropEffects), typeof(DependencyObject), typeof(Point)],
                modifiers: null)
            ?? throw new InvalidOperationException("DragEventArgs' constructor is gone");
        return (DragEventArgs)constructor.Invoke([data, DragDropKeyStates.None, OfferedEffects, target, at]);
    }

    /// <summary>The note's editor, a point in its text, and how many drops
    /// its text holds.</summary>
    private static (UIElement Over, Point At, Func<string> Landed) EditorDropTarget(ShippedShellHost host, WorkspaceViewModel workspace)
    {
        SlateTextEditor? editor = null;
        Assert.True(
            PumpedDispatcher.PumpUntil(
                () => (editor = Descendants<SlateTextEditor>(host.Shell).FirstOrDefault(candidate =>
                    candidate.IsVisible && ReferenceEquals(candidate.DataContext, workspace.ActiveGroup.ActiveTab))) is not null,
                TimeSpan.FromSeconds(20)),
            "premise: the note's editor never showed");
        SlateTextEditor shown = editor!;
        return (shown.TextArea, new Point(120, 30), () => $"drops {Occurrences(shown.Document.Text)}, length {shown.Document.TextLength}");
    }

    /// <summary>The Files filter field, a point in it, and its text.</summary>
    private static (UIElement Over, Point At, Func<string> Landed) FilterDropTarget(ShippedShellHost host)
    {
        TextBox filter = host.Shell.SidebarFilterTextBox;
        Assert.True(
            PumpedDispatcher.PumpUntil(() => filter.IsVisible, TimeSpan.FromSeconds(10)),
            "premise: the Files filter field never showed");
        return (filter, new Point(30, filter.ActualHeight / 2), () => $"text \"{filter.Text}\"");
    }

    private static int Occurrences(string text) =>
        (text.Length - text.Replace(DroppedText, string.Empty, StringComparison.Ordinal).Length) / DroppedText.Length;

    /// <summary>The observable once the dispatcher and layout have
    /// settled.</summary>
    private static string SettledIn(Window window, Func<string> observe)
    {
        PumpedDispatcher.Drain();
        window.UpdateLayout();
        PumpedDispatcher.Drain();
        return observe();
    }

    /// <summary>A point inside the list row <paramref name="slot"/> rows down
    /// the list's viewport.</summary>
    private static Point RowPointIn(ListBox list, int slot)
    {
        list.UpdateLayout();
        ListBoxItem[] rows = [.. Descendants<ListBoxItem>(list)
            .Where(row => row.IsVisible && row.TranslatePoint(default, list).Y >= 0 && row.TranslatePoint(default, list).Y < list.ActualHeight - 30)
            .OrderBy(row => row.TranslatePoint(default, list).Y)];
        Assert.True(rows.Length > 0, "premise: the list shows no rows");
        ListBoxItem chosen = rows[Math.Min(slot, rows.Length - 1)];
        return chosen.TranslatePoint(new Point(40, chosen.ActualHeight / 2), list);
    }

    /// <summary>A point inside the grid cell <paramref name="slot"/> rows down
    /// the grid's viewport.</summary>
    private static Point CellPointIn(DataGrid grid, int slot)
    {
        grid.UpdateLayout();
        DataGridRow[] rows = [.. Descendants<DataGridRow>(grid)
            .Where(row => row.IsVisible && row.TranslatePoint(default, grid).Y >= 30 && row.TranslatePoint(default, grid).Y < grid.ActualHeight - 40)
            .OrderBy(row => row.TranslatePoint(default, grid).Y)];
        Assert.True(rows.Length > 0, "premise: the grid shows no rows");
        DataGridCell cell = Descendants<DataGridCell>(rows[Math.Min(slot, rows.Length - 1)]).First();
        return cell.TranslatePoint(new Point(Math.Min(20, cell.ActualWidth / 2), cell.ActualHeight / 2), grid);
    }

    private static int CurrentRowOf(DataGrid grid) =>
        grid.CurrentCell.Item is { } item ? grid.Items.IndexOf(item) : -1;

    /// <summary>A note with eighty headings, long enough that its outline
    /// list scrolls.</summary>
    private static string NoteWithManyHeadings()
    {
        var text = new StringBuilder("# Long note\n\n");
        for (int section = 0; section < 80; section++)
        {
            text.Append(CultureInfo.InvariantCulture, $"## Section {section}\n\nParagraph {section} of the long note.\n\n");
        }

        return text.ToString();
    }

    /// <summary>A touch contact WPF routes through its input manager, as a
    /// digitizer's would, without the stylus stack. Positions are in the
    /// source's root coordinates.</summary>
    private sealed class SyntheticTouchDevice(PresentationSource source) : TouchDevice(Interlocked.Increment(ref s_nextTouchId))
    {
        private static int s_nextTouchId = 38_000;
        private Point _position;
        private TouchAction _action = TouchAction.Down;

        public void Down(Point root)
        {
            SetActiveSource(source);
            _position = root;
            _action = TouchAction.Down;
            Activate();
            _ = ReportDown();
        }

        public void Move(Point root)
        {
            _position = root;
            _action = TouchAction.Move;
            _ = ReportMove();
        }

        public void Up()
        {
            _action = TouchAction.Up;
            _ = ReportUp();
            Deactivate();
        }

        public override TouchPoint GetTouchPoint(IInputElement relativeTo)
        {
            Point point = _position;
            if (relativeTo is Visual visual && ActiveSource?.RootVisual is Visual root && !ReferenceEquals(visual, root)
                && root.TransformToDescendant(visual) is { } transform)
            {
                point = transform.Transform(point);
            }

            return new TouchPoint(this, point, new Rect(point, new Size(1, 1)), _action);
        }

        public override TouchPointCollection GetIntermediateTouchPoints(IInputElement relativeTo) => new();
    }

    /// <summary>
    /// The shell on the screen and in front, with a column to its right for
    /// a drag source, a prompt and the reading table, for the facts that
    /// drive real input. Each refusal skips the fact (#1680). Disposing it
    /// lets go of any modifier key or mouse button the input left down.
    /// </summary>
    private sealed class RealInputDesk : IDisposable
    {
        private const ushort VkControl = 0x11;
        private readonly MainWindow _shell;
        private readonly Rect _column;

        public RealInputDesk(MainWindow shell, string measured)
        {
            _shell = shell;
            Rect work = SystemParameters.WorkArea;
            double width = Math.Max(640, Math.Min(960, work.Width - 380));
            double height = Math.Max(480, Math.Min(680, work.Height - 40));
            shell.Topmost = true;
            shell.Left = work.Left + 20;
            shell.Top = work.Top + 20;
            shell.Width = width;
            shell.Height = height;
            shell.UpdateLayout();
            PumpedDispatcher.Drain();
            _column = new Rect(shell.Left + width + 20, work.Top + 20, Math.Max(240, Math.Min(320, work.Right - shell.Left - width - 30)), height);
            if (!TakeTheForeground(shell))
            {
                TouchAndDropNative.LetGoOfHeldInput();
                shell.Topmost = false;
                SkipWhereThisDesktopRefusesRealInput("the foreground", measured);
            }
        }

        /// <summary>A real touch's frames: a screen point in pixels and its
        /// pointer flags.</summary>
        public static (Point Screen, uint Flags)[] TouchTapFrames(UIElement target, Point local)
        {
            Point at = target.PointToScreen(local);
            return [(at, TouchAndDropNative.Down), (at, TouchAndDropNative.Update), (at, TouchAndDropNative.Up)];
        }

        public static (Point Screen, uint Flags)[] TouchDragFrames(UIElement target, Point local, Vector delta)
        {
            Point from = target.PointToScreen(local);
            Point to = target.PointToScreen(local + delta);
            var frames = new List<(Point, uint)> { (from, TouchAndDropNative.Down) };
            for (int step = 1; step <= 15; step++)
            {
                frames.Add((from + ((to - from) * step / 15), TouchAndDropNative.Update));
            }

            for (int still = 0; still < 4; still++)
            {
                frames.Add((to, TouchAndDropNative.Update));
            }

            frames.Add((to, TouchAndDropNative.Up));
            return [.. frames];
        }

        [DoesNotReturn]
        public static void SkipWhereThisDesktopRefusesRealInput(string refused, string unmeasured)
        {
            Assert.Skip($"this desktop refused {refused} (#1680), so {unmeasured} was not measured on it");
            throw new UnreachableException("Assert.Skip returned instead of ending the fact");
        }

        /// <summary>The reading table, shown in the column, in front.</summary>
        public Window TableInTheColumn(MainWindow owner)
        {
            var source = new StringBuilder("| Name | Value |\n| --- | --- |\n");
            for (int row = 0; row < 60; row++)
            {
                source.Append(CultureInfo.InvariantCulture, $"| a{row:D2} | {row} |\n");
            }

            Window table = Assert.IsType<Window>(ReadingTableGrid.Show(source.ToString(), owner, activate: false));
            table.Topmost = true;
            table.Left = _column.Left;
            table.Top = _column.Top + 260;
            table.Width = _column.Width;
            table.Height = Math.Max(240, _column.Height - 260);
            DataGrid grid = Assert.IsType<AccessibleDataGrid>(table.Content).Grid;
            Assert.True(
                PumpedDispatcher.PumpUntil(() => grid.IsVisible && Descendants<DataGridRow>(grid).Count() > 8, TimeSpan.FromSeconds(10)),
                "premise: the reading table never showed its rows");
            table.UpdateLayout();
            PumpedDispatcher.Drain();
            return table;
        }

        /// <summary>
        /// Sets up touch injection before the fact builds a window, and checks
        /// that WPF has registered the injection's touch device. The device,
        /// once set up, stays for the life of the test process. WPF reads a
        /// thread's tablets once, as the thread's first window is made, and
        /// drops a touch from a device it did not read: set up after the
        /// fact's first window, no injected touch arrived. Measured on
        /// 2026-11-27: run alone, the fact's thread reads the device at once;
        /// in an 851-fact run it read it too; but in short runs where a fact
        /// with a shell window ran just before it, no thread of the process
        /// read the device for ten seconds, and no touch arrived. So the check
        /// looks once, from a thread of its own that spends none of the fact
        /// thread's one read. Where the desktop refuses touch injection the
        /// fact skips as the menu facts do (#1680); where WPF in this process
        /// has not registered the device, which is no desktop's refusal, it
        /// skips on that cause.
        /// </summary>
        public static void PrepareTouchInjection()
        {
            if (!TouchAndDropNative.InitializeTouchInjection(1, TouchAndDropNative.TouchFeedbackNone))
            {
                SkipWhereThisDesktopRefusesRealInput(
                    $"touch injection (InitializeTouchInjection, error {Marshal.GetLastWin32Error()})", "real touch");
            }

            bool registered = StaThread.Run(
                () => Tablet.TabletDevices.Cast<TabletDevice>().Any(tablet => tablet.Type == TabletDeviceType.Touch),
                TimeSpan.FromSeconds(20),
                "the look at WPF's tablets timed out.");
            if (!registered)
            {
                Assert.Skip(
                    "WPF in this test process has not registered the injected touch device (in short runs where a fact "
                    + "with a shell window ran just before this one it never does; run the fact alone), so a real touch "
                    + "pan was not measured");
            }
        }

        /// <summary>Injects <paramref name="frames"/> as real touch, one frame
        /// every 16 ms, from a worker thread while this thread pumps. Returns
        /// null, or what failed: the caller decides whether that is the
        /// desktop refusing touch or a failure of the fact.</summary>
        public string? RealTouch((Point Screen, uint Flags)[] frames)
        {
            Task<string?> injected = Task.Run(() =>
            {
                bool down = false;
                try
                {
                    foreach ((Point screen, uint flags) in frames)
                    {
                        if (!TouchAndDropNative.InjectTouch(screen, flags))
                        {
                            return $"InjectTouchInput, error {Marshal.GetLastWin32Error()}";
                        }

                        down = flags != TouchAndDropNative.Up;
                        Thread.Sleep(16);
                    }
                }
                finally
                {
                    if (down)
                    {
                        _ = TouchAndDropNative.InjectTouch(frames[^1].Screen, TouchAndDropNative.Up);
                    }
                }

                return null;
            });
            Assert.True(PumpedDispatcher.PumpUntil(() => injected.IsCompleted, TimeSpan.FromSeconds(20)), "the touch injector never finished");
            return injected.Result;
        }

        /// <summary>How long a drag may run before its source cancels it: a
        /// release the desktop refused would otherwise hold
        /// <c>DoDragDrop</c> for good.</summary>
        private static readonly TimeSpan DragDeadline = TimeSpan.FromSeconds(15);

        /// <summary>
        /// A real OLE drag of <paramref name="text"/> from a window on a
        /// thread of its own to <paramref name="local"/> on
        /// <paramref name="target"/>: the left button goes down on the source
        /// window, which starts <c>DoDragDrop</c> there, the cursor moves to
        /// the target, and the button comes up. Returns the effect
        /// <c>DoDragDrop</c> reported, or null where the desktop refused the
        /// drag: the press never reached the source, or the drag was still
        /// running at <see cref="DragDeadline"/> and the source cancelled it.
        /// Skips where the source's window never shows. The source's thread
        /// runs through <see cref="StaThread"/>, and its dispatcher is shut
        /// down as the drag ends, so it leaves no window class behind.
        /// </summary>
        public DragDropEffects? RealOleDrop(UIElement target, Point local, string text)
        {
            double pixelsPerDip = VisualTreeHelper.GetDpi(_shell).PixelsPerDip;
            var sourceDip = new Rect(_column.Left, _column.Top, Math.Min(180, _column.Width), 90);
            var sourcePixel = new Point((sourceDip.Left + (sourceDip.Width / 2)) * pixelsPerDip, (sourceDip.Top + (sourceDip.Height / 2)) * pixelsPerDip);
            Point targetPixel = target.PointToScreen(local);
            var ready = new ManualResetEventSlim();
            var started = new ManualResetEventSlim();
            var ended = new ManualResetEventSlim();
            DragDropEffects effect = DragDropEffects.None;
            bool cancelled = false;
            Dispatcher? sourceDispatcher = null;
            Task sourceThread = Task.Run(() => StaThread.Run(
                () =>
                {
                    var border = new Border { Background = Brushes.Gold, Child = new TextBlock { Text = "CB-38 drag source" } };
                    var window = new Window
                    {
                        Content = border,
                        Topmost = true,
                        ShowActivated = false,
                        ShowInTaskbar = false,
                        WindowStyle = WindowStyle.ToolWindow,
                        WindowStartupLocation = WindowStartupLocation.Manual,
                        Left = sourceDip.Left,
                        Top = sourceDip.Top,
                        Width = sourceDip.Width,
                        Height = sourceDip.Height,
                    };
                    var clock = new Stopwatch();

                    // OLE asks the source whether to go on as the mouse or a
                    // key changes; past the deadline the answer is Cancel,
                    // and DoDragDrop returns.
                    border.QueryContinueDrag += (_, e) =>
                    {
                        if (clock.Elapsed > DragDeadline)
                        {
                            cancelled = true;
                            e.Action = DragAction.Cancel;
                            e.Handled = true;
                        }
                    };
                    border.PreviewMouseLeftButtonDown += (_, e) =>
                    {
                        e.Handled = true;
                        started.Set();
                        clock.Start();
                        effect = DragDrop.DoDragDrop(border, new DataObject(DataFormats.UnicodeText, text), OfferedEffects);
                        ended.Set();
                        Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
                    };
                    window.Show();
                    TouchAndDropNative.NeverActivate(window);
                    sourceDispatcher = Dispatcher.CurrentDispatcher;
                    ready.Set();
                    Dispatcher.Run();
                },
                TimeSpan.FromSeconds(90),
                "the CB-38 drag source timed out."));
            Task driver = Task.Run(() =>
            {
                if (!ready.Wait(TimeSpan.FromSeconds(30)))
                {
                    return;
                }

                Thread.Sleep(300);
                TouchAndDropNative.MoveCursor(sourcePixel);
                Thread.Sleep(150);
                TouchAndDropNative.LeftButton(down: true);
                try
                {
                    if (!started.Wait(TimeSpan.FromSeconds(3)))
                    {
                        return;
                    }

                    Thread.Sleep(250);
                    for (int step = 1; step <= 15; step++)
                    {
                        TouchAndDropNative.MoveCursor(sourcePixel + ((targetPixel - sourcePixel) * step / 15));
                        Thread.Sleep(40);
                    }

                    for (int still = 0; still < 6; still++)
                    {
                        TouchAndDropNative.MoveCursor(targetPixel + new Vector(still % 2, 0));
                        Thread.Sleep(80);
                    }
                }
                finally
                {
                    TouchAndDropNative.LeftButton(down: false);
                }

                // A drag whose release the desktop refused runs on: a nudge of
                // the cursor makes OLE ask the source again, which cancels it
                // once the deadline has passed.
                for (int nudge = 0; nudge < 60 && !ended.Wait(TimeSpan.FromMilliseconds(500)); nudge++)
                {
                    TouchAndDropNative.MoveCursor(targetPixel + new Vector(nudge % 2, 1));
                }
            });
            Assert.True(PumpedDispatcher.PumpUntil(() => driver.IsCompleted, TimeSpan.FromSeconds(75)), "the drag's driver never finished");
            driver.GetAwaiter().GetResult();
            if (!ready.IsSet)
            {
                if (sourceThread.IsFaulted)
                {
                    sourceThread.GetAwaiter().GetResult();
                }

                // A window shown late still ends its loop here.
                _ = PumpedDispatcher.PumpUntil(() => sourceDispatcher is not null || sourceThread.IsCompleted, TimeSpan.FromSeconds(5));
                sourceDispatcher?.BeginInvokeShutdown(DispatcherPriority.Send);
                SkipWhereThisDesktopRefusesRealInput("the drag source's window", "a real drop");
            }

            if (!started.IsSet && sourceDispatcher is { } idle)
            {
                // The press never reached the source, so nothing ends its loop.
                idle.BeginInvokeShutdown(DispatcherPriority.Send);
            }

            Assert.True(PumpedDispatcher.PumpUntil(() => sourceThread.IsCompleted, TimeSpan.FromSeconds(30)), "the drag source's thread never ended");
            sourceThread.GetAwaiter().GetResult();
            TouchAndDropNative.MoveCursor(sourcePixel);
            PumpedDispatcher.Drain();
            return started.IsSet && !cancelled ? effect : null;
        }

        /// <summary>Runs <paramref name="inside"/> while a real WPF dialog the
        /// shell owns is up (<c>ShowDialog</c>: every other window on the
        /// thread is disabled, the shell included, and the thread is modal),
        /// from the dialog's own loop, then closes it.</summary>
        public T UnderARealPrompt<T>(Func<T> inside)
        {
            T result = default!;
            ExceptionDispatchInfo? failure = null;
            var dialog = new Window
            {
                Owner = _shell,
                Title = "CB-38 prompt",
                Content = new TextBlock { Text = "CB-38 prompt", Margin = new Thickness(12) },
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = _column.Left,
                Top = _column.Top + 110,
                Width = _column.Width,
                Height = 130,
                ShowInTaskbar = false,
                Topmost = true,
            };
            _ = dialog.Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
            {
                try
                {
                    result = inside();
                }
                catch (Exception thrown)
                {
                    failure = ExceptionDispatchInfo.Capture(thrown);
                }
                finally
                {
                    dialog.Close();
                }
            });
            _ = dialog.ShowDialog();
            PumpedDispatcher.Drain();
            failure?.Throw();
            return result;
        }

        public void Dispose()
        {
            TouchAndDropNative.LetGoOfHeldInput();
            _shell.Topmost = false;
        }

        private static bool TakeTheForeground(Window window)
        {
            IntPtr handle = new WindowInteropHelper(window).Handle;
            for (int attempt = 0; attempt < 5 && TouchAndDropNative.GetForegroundWindow() != handle; attempt++)
            {
                if (attempt > 0)
                {
                    // A tap of Control makes this process the last to take
                    // input, which lets it set the foreground.
                    TouchAndDropNative.Key(VkControl, up: false);
                    TouchAndDropNative.Key(VkControl, up: true);
                }

                _ = window.Activate();
                _ = TouchAndDropNative.SetForegroundWindow(handle);
                _ = PumpedDispatcher.PumpUntil(() => TouchAndDropNative.GetForegroundWindow() == handle, TimeSpan.FromMilliseconds(300));
            }

            return TouchAndDropNative.GetForegroundWindow() == handle;
        }
    }

    /// <summary>Win32 input for the interactive facts.</summary>
    private static class TouchAndDropNative
    {
        public const uint TouchFeedbackNone = 3;
        public const uint Down = PointerFlagDown | PointerFlagInRange | PointerFlagInContact;
        public const uint Update = PointerFlagUpdate | PointerFlagInRange | PointerFlagInContact;
        public const uint Up = PointerFlagUp;

        private const uint PointerTypeTouch = 2;
        private const uint PointerFlagInRange = 0x2;
        private const uint PointerFlagInContact = 0x4;
        private const uint PointerFlagDown = 0x10000;
        private const uint PointerFlagUpdate = 0x20000;
        private const uint PointerFlagUp = 0x40000;
        private const uint TouchMaskContactArea = 0x1;
        private const uint TouchMaskOrientation = 0x2;
        private const uint TouchMaskPressure = 0x4;
        private const uint InputMouse = 0;
        private const uint InputKeyboard = 1;
        private const uint MouseMoved = 0x0001;
        private const uint MouseLeftDown = 0x0002;
        private const uint MouseLeftUp = 0x0004;
        private const uint MouseAbsolute = 0x8000;
        private const uint KeyUp = 0x0002;
        private const int GwlExStyle = -20;
        private const long WsExNoActivate = 0x08000000;

        /// <summary>Both sides of Shift, Control and Alt, and the Windows
        /// keys.</summary>
        private static readonly ushort[] Modifiers = [0x10, 0x11, 0x12, 0xA0, 0xA1, 0xA2, 0xA3, 0xA4, 0xA5, 0x5B, 0x5C];

        public static bool InjectTouch(Point screen, uint flags)
        {
            int x = (int)Math.Round(screen.X);
            int y = (int)Math.Round(screen.Y);
            var contact = new PointerTouchInfo
            {
                PointerInfo = new PointerInfo
                {
                    PointerType = PointerTypeTouch,
                    PointerId = 0,
                    PointerFlags = flags,
                    PixelLocation = new NativePoint { X = x, Y = y },
                },
                TouchMask = TouchMaskContactArea | TouchMaskOrientation | TouchMaskPressure,
                Contact = new NativeRect { Left = x - 2, Top = y - 2, Right = x + 2, Bottom = y + 2 },
                Orientation = 90,
                Pressure = 32000,
            };
            return InjectTouchInput(1, [contact]);
        }

        public static void MoveCursor(Point screen)
        {
            int width = GetSystemMetrics(0);
            int height = GetSystemMetrics(1);
            Send(new SentInput
            {
                Type = InputMouse,
                Union = new InputUnion
                {
                    Pointer = new MouseInput
                    {
                        X = (int)Math.Round(screen.X * 65535 / (width - 1)),
                        Y = (int)Math.Round(screen.Y * 65535 / (height - 1)),
                        Flags = MouseMoved | MouseAbsolute,
                    },
                },
            });
        }

        public static void LeftButton(bool down) =>
            Send(new SentInput { Type = InputMouse, Union = new InputUnion { Pointer = new MouseInput { Flags = down ? MouseLeftDown : MouseLeftUp } } });

        public static void Key(ushort key, bool up) =>
            Send(new SentInput { Type = InputKeyboard, Union = new InputUnion { Keys = new KeyboardInput { Key = key, Flags = up ? KeyUp : 0 } } });

        /// <summary>After real input: no modifier and no left button left
        /// down; each held one gets one release.</summary>
        public static void LetGoOfHeldInput()
        {
            foreach (ushort modifier in Modifiers.Where(modifier => (GetAsyncKeyState(modifier) & 0x8000) != 0))
            {
                Key(modifier, up: true);
            }

            if ((GetAsyncKeyState(0x01) & 0x8000) != 0)
            {
                LeftButton(down: false);
            }
        }

        /// <summary>A drag source that never takes the foreground from the
        /// shell when it is pressed.</summary>
        public static void NeverActivate(Window window)
        {
            IntPtr handle = new WindowInteropHelper(window).Handle;
            _ = SetWindowLongPtr(handle, GwlExStyle, new IntPtr(GetWindowLongPtr(handle, GwlExStyle).ToInt64() | WsExNoActivate));
        }

        private static void Send(SentInput input) => _ = SendInput(1, [input], Marshal.SizeOf<SentInput>());

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PointerInfo
        {
            public uint PointerType;
            public uint PointerId;
            public uint FrameId;
            public uint PointerFlags;
            public IntPtr SourceDevice;
            public IntPtr HwndTarget;
            public NativePoint PixelLocation;
            public NativePoint HimetricLocation;
            public NativePoint PixelLocationRaw;
            public NativePoint HimetricLocationRaw;
            public uint Time;
            public uint HistoryCount;
            public int InputData;
            public uint KeyStates;
            public ulong PerformanceCount;
            public int ButtonChangeType;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PointerTouchInfo
        {
            public PointerInfo PointerInfo;
            public uint TouchFlags;
            public uint TouchMask;
            public NativeRect Contact;
            public NativeRect ContactRaw;
            public uint Orientation;
            public uint Pressure;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SentInput
        {
            public uint Type;
            public InputUnion Union;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)]
            public MouseInput Pointer;
            [FieldOffset(0)]
            public KeyboardInput Keys;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MouseInput
        {
            public int X;
            public int Y;
            public uint Data;
            public uint Flags;
            public uint Time;
            public IntPtr ExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KeyboardInput
        {
            public ushort Key;
            public ushort Scan;
            public uint Flags;
            public uint Time;
            public IntPtr ExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool InitializeTouchInjection(uint maxCount, uint feedback);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool InjectTouchInput(uint count, [In] PointerTouchInfo[] contacts);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint count, [In] SentInput[] inputs, int size);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int key);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int index);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr window);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindowEnabled(IntPtr window);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        private static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value);
    }
}
