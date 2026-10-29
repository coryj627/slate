// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SlateWindows.Canvas;
using SlateWindows.Commands;
using SlateWindows.Graph;
using SlateWindows.Grids;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// #1275 codex rounds 5 and 6: the shell's own routes — <c>MainWindow</c>'s
/// window-level <c>PreviewKeyDown</c>, its XAML input bindings and its menu
/// — must respect the palette's seal, not only the palette's key handler,
/// and with the palette closed as well as open. These facts raise REAL
/// input through those routes on the shipped shell (its key presses raised
/// on their target with a borrowed input source — the
/// <c>SheetKeyboardFenceTests</c> shape), so the shell's own admission is
/// exercised, not the view model's methods.
/// </summary>
public sealed partial class CommandPaletteTests
{
    /// <summary>
    /// Close Vault run from the palette over a dirty tab raises the
    /// unsaved-changes prompt inside the command — through the lifecycle's
    /// prompt seam, a message-box shaped loop over the shell (CI has no
    /// interactive session for a native box). Inside that loop the real
    /// Ctrl+Shift+N goes through the shell's key route: it is taken and
    /// ignored — the palette stays open, sealed and silent, and no template
    /// picker opens. Cancel on the prompt ends the command; only that
    /// invocation completes: its recent is written, the palette dismisses,
    /// and no template sheet ever appeared.
    /// </summary>
    [Fact]
    public void TheTemplateChordThroughTheShellIsRefusedWhileAPaletteCommandRuns() => RunSta(() =>
    {
        using var host = new ShippedShellHost(shown: true);
        SyntheticPrompt prompt = host.InstallClosePrompt();
        WorkspaceViewModel workspace = host.AttachDirtyWorkspace();
        CommandPaletteViewModel palette = host.Palette;
        palette.Open();
        Assert.True(
            PumpedDispatcher.PumpUntil(() => !palette.IsRankPending, TimeSpan.FromSeconds(10)),
            "the palette's rows never published");
        PumpedDispatcher.Drain();
        palette.Select(Assert.Single(palette.Rows, row => row.Id == ChordTable.Ids.VaultClose));
        host.Heard.Clear();
        int published = 0;
        palette.PropertyChanged += (_, change) =>
        {
            if (change.PropertyName == nameof(CommandPaletteViewModel.Rows))
            {
                published++;
            }
        };

        (bool Open, bool Sealed, bool Handled, bool PickerOpen, int Published, int Heard)? inside = null;
        int ticks = 0;
        var timer = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(50) };
        timer.Tick += (_, _) =>
        {
            Action? close = prompt.Closer();
            if (close is null && ++ticks < 200)
            {
                return;
            }

            timer.Stop();
            if (close is null)
            {
                return;
            }

            // The unsaved-changes prompt is up, inside Close Vault.
            bool handled = host.Press(
                host.Shell.CommandPaletteSearchTextBox,
                Key.N,
                ModifierKeys.Control | ModifierKeys.Shift);
            PumpedDispatcher.Drain();
            inside = (
                palette.IsOpen,
                palette.IsSealed,
                handled,
                workspace.TemplatePickerSheet is not null,
                published,
                host.Heard.Count);
            close();
        };
        timer.Start();
        bool enter = host.Press(host.Shell.CommandPaletteSearchTextBox, Key.Enter, ModifierKeys.None);
        timer.Stop();

        Assert.True(enter, "the palette's key route did not take Enter");
        Assert.NotNull(inside);
        Assert.True(inside.Value.Handled, "the shell let Ctrl+Shift+N through under the running command");
        Assert.True(
            inside.Value is (true, true, _, false, 0, 0),
            $"inside the running command the palette was open={inside.Value.Open}, sealed={inside.Value.Sealed}; "
            + $"a template picker opened={inside.Value.PickerOpen}; the palette published "
            + $"{inside.Value.Published} time(s) and made {inside.Value.Heard} announcement(s)");

        // Only the original invocation completed: Close Vault (cancelled at
        // its prompt) returned, its recent was written and the palette
        // dismissed; no template sheet ever opened and the vault is open.
        Assert.False(palette.IsSealed);
        Assert.False(palette.IsOpen);
        Assert.Null(workspace.TemplatePickerSheet);
        Assert.Null(workspace.TemplateFlowSheet);
        Assert.True(host.Lifecycle.IsVaultOpen);
        PumpedDispatcher.PumpUntilDrained(palette.RecordCompletion);
        Assert.Equal([ChordTable.Ids.VaultClose], host.PersistedRecents());
        Assert.Empty(host.Heard);
    });

    /// <summary>
    /// The audit's second find: with the palette closed but sealed — here a
    /// thread-modal section over the shell — the palette's own chord used to
    /// clear the way first, dismissing an open Quick Open, and only then
    /// have its open refused. Through the real key route, the chord is taken
    /// and nothing moves: Quick Open stays open, the palette stays shut.
    /// </summary>
    [Fact]
    public void ThePaletteChordThroughTheShellLeavesQuickOpenAloneWhileThePaletteIsSealed() => RunSta(() =>
    {
        using var host = new ShippedShellHost();
        QuickSwitcherViewModel switcher = host.AttachQuickOpen();
        switcher.Open();
        Assert.Equal(ModalSurface.QuickOpen, host.Shell.OpenModalSurface);

        ComponentDispatcher.PushModal();
        try
        {
            Assert.True(host.Palette.IsSealed);
            bool handled = host.Press(
                host.Shell.QuickSwitcherSearchTextBox,
                Key.P,
                ModifierKeys.Control | ModifierKeys.Shift);

            Assert.True(handled, "the shell let Ctrl+Shift+P through");
            Assert.True(switcher.IsOpen, "the refused palette open dismissed Quick Open first");
            Assert.False(host.Palette.IsOpen);
        }
        finally
        {
            ComponentDispatcher.PopModal();
        }

        Assert.False(host.Palette.IsSealed);
        Assert.True(switcher.IsOpen);
    });

    /// <summary>A shell route that must not act while the palette is
    /// closed but sealed.</summary>
    public enum ClosedSealRoute
    {
        /// <summary>Ctrl+Shift+N — the shell's own chord, which opens the
        /// template picker.</summary>
        TemplateChord,

        /// <summary>Ctrl+S — the window's XAML key binding to Save.</summary>
        SaveBinding,

        /// <summary>Alt+F — the File menu's access key: the key through
        /// the window's route, then the mnemonic itself through the access
        /// key manager in the shell's scope.</summary>
        FileMenuAccessKey,

        /// <summary>A pointer press and release on the File menu, through
        /// the window's route.</summary>
        FileMenuPointer,

        /// <summary>A double-click on the saved-queries list, whose
        /// double-click runs the selected query (codex round 7): WPF rebuilds
        /// a fresh double-click from a press the window already took.</summary>
        SavedQueryDoubleClick,
    }

    /// <summary>
    /// Codex round 6: the CLOSED-but-sealed state. Close Vault run from the
    /// palette over a dirty tab raises the unsaved-changes prompt through
    /// the lifecycle's seam (a message-box shaped loop over the shell);
    /// inside it Escape through the shell's route still dismisses the
    /// palette (T9), which leaves the palette closed while its command runs.
    /// Then the shell takes everything and nothing acts: the route under
    /// test goes through the real window and is taken — no template picker,
    /// no save, no File menu. Cancel on the prompt ends the command, which
    /// completes alone: its recent is written and the vault stays open.
    /// </summary>
    [Theory]
    [InlineData(ClosedSealRoute.TemplateChord)]
    [InlineData(ClosedSealRoute.SaveBinding)]
    [InlineData(ClosedSealRoute.FileMenuAccessKey)]
    [InlineData(ClosedSealRoute.FileMenuPointer)]
    [InlineData(ClosedSealRoute.SavedQueryDoubleClick)]
    public void WithThePaletteDismissedUnderItsRunningCommandTheShellTakesNothing(ClosedSealRoute route) => RunSta(() =>
    {
        using var host = new ShippedShellHost(shown: true, savedQuery: true);
        SyntheticPrompt prompt = host.InstallClosePrompt();
        WorkspaceViewModel workspace = host.AttachDirtyWorkspace();
        CommandPaletteViewModel palette = host.Palette;
        palette.Open();
        Assert.True(
            PumpedDispatcher.PumpUntil(() => !palette.IsRankPending, TimeSpan.FromSeconds(10)),
            "the palette's rows never published");
        PumpedDispatcher.Drain();
        palette.Select(Assert.Single(palette.Rows, row => row.Id == ChordTable.Ids.VaultClose));
        host.Heard.Clear();

        (bool Escaped, bool ClosedAndSealed, bool Handled, string? Acted)? inside = null;
        int ticks = 0;
        var timer = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(50) };
        timer.Tick += (_, _) =>
        {
            Action? close = prompt.Closer();
            if (close is null && ++ticks < 200)
            {
                return;
            }

            timer.Stop();
            if (close is null)
            {
                return;
            }

            // The unsaved-changes prompt is up, inside Close Vault. T9:
            // Escape through the shell's route dismisses the palette.
            bool escaped = host.Press(host.Shell.CommandPaletteSearchTextBox, Key.Escape, ModifierKeys.None);
            PumpedDispatcher.Drain();
            bool closedAndSealed = !palette.IsOpen && palette.IsSealed;
            (bool handled, string? acted) = TakeTheRoute(host, workspace, route);
            inside = (escaped, closedAndSealed, handled, acted);
            close();
        };
        timer.Start();
        bool enter = host.Press(host.Shell.CommandPaletteSearchTextBox, Key.Enter, ModifierKeys.None);
        timer.Stop();

        Assert.True(enter, "the palette's key route did not take Enter");
        Assert.NotNull(inside);
        Assert.True(inside.Value.Escaped, "Escape through the shell did not reach the sealed palette (T9)");
        Assert.True(inside.Value.ClosedAndSealed, "Escape did not leave the palette closed under its running command");
        Assert.True(inside.Value.Handled, $"the shell let {route} through while the palette was closed but sealed");
        Assert.True(inside.Value.Acted is null, $"{route} acted under the running command: {inside.Value.Acted}");

        // Only the original invocation completed: Close Vault (cancelled at
        // its prompt) returned and its recent was written; nothing else ran.
        Assert.False(palette.IsSealed);
        Assert.False(palette.IsOpen);
        Assert.Null(workspace.TemplatePickerSheet);
        Assert.True(workspace.HasDirtyTabs);
        Assert.True(host.Lifecycle.IsVaultOpen);
        PumpedDispatcher.PumpUntilDrained(palette.RecordCompletion);
        Assert.Equal([ChordTable.Ids.VaultClose], host.PersistedRecents());
        Assert.Empty(host.Heard);
    });

    /// <summary>Raises <paramref name="route"/> through the shell and says
    /// what, if anything, it did.</summary>
    private static (bool Handled, string? Acted) TakeTheRoute(
        ShippedShellHost host, WorkspaceViewModel workspace, ClosedSealRoute route)
    {
        switch (route)
        {
            case ClosedSealRoute.TemplateChord:
                {
                    bool handled = host.Press(host.Shell, Key.N, ModifierKeys.Control | ModifierKeys.Shift);
                    PumpedDispatcher.Drain();
                    return (handled, workspace.TemplatePickerSheet is not null ? "the template picker opened" : null);
                }

            case ClosedSealRoute.SaveBinding:
                {
                    string note = Path.Combine(host.VaultRoot, "note0.md");
                    string before = File.ReadAllText(note);
                    bool handled = host.Press(host.Shell, Key.S, ModifierKeys.Control);
                    PumpedDispatcher.Drain();
                    return (handled, !workspace.HasDirtyTabs || File.ReadAllText(note) != before
                        ? "the dirty tab was saved"
                        : null);
                }

            case ClosedSealRoute.FileMenuAccessKey:
                {
                    MenuItem file = FileMenu(host);
                    bool handled = host.PressSystem(host.Shell, Key.F, ModifierKeys.Alt);

                    // The mnemonic leg: WPF's loop turns an unhandled Alt+F into
                    // a mnemonic for the access key manager. Raised here whatever
                    // the key leg did, so the manager's own path under the seal
                    // is shown too. Witnessed by the menu's own SubmenuOpened,
                    // not its state afterwards: on a non-interactive session a
                    // menu that cannot take the mouse capture backs straight out.
                    int opened = 0;
                    var witness = new RoutedEventHandler((_, _) => opened++);
                    file.SubmenuOpened += witness;
                    try
                    {
                        host.Mnemonic("F");
                        PumpedDispatcher.Drain();
                    }
                    finally
                    {
                        file.SubmenuOpened -= witness;
                        file.IsSubmenuOpen = false;
                    }

                    return (handled, opened > 0 ? "the File menu opened" : null);
                }

            case ClosedSealRoute.FileMenuPointer:
                {
                    // A synthetic press cannot click a menu item (WPF reads the
                    // real cursor and button), so the witness is reach: the
                    // item's own button handlers see the press only if the
                    // window's route let it through unhandled.
                    MenuItem file = FileMenu(host);
                    int reached = 0;
                    var probe = new MouseButtonEventHandler((_, _) => reached++);
                    file.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent, probe);
                    file.AddHandler(UIElement.PreviewMouseLeftButtonUpEvent, probe);
                    try
                    {
                        bool handled = host.PressPointer(file);
                        PumpedDispatcher.Drain();
                        bool opened = file.IsSubmenuOpen;
                        file.IsSubmenuOpen = false;
                        return (handled, reached > 0 || opened
                            ? $"the File menu took {reached} pointer event(s), and opened={opened}"
                            : null);
                    }
                    finally
                    {
                        file.RemoveHandler(UIElement.PreviewMouseLeftButtonDownEvent, probe);
                        file.RemoveHandler(UIElement.PreviewMouseLeftButtonUpEvent, probe);
                    }
                }

            case ClosedSealRoute.SavedQueryDoubleClick:
                {
                    // The shipped list, with the fixture's saved query
                    // selected: its double-click runs that query in a tab.
                    ListBox list = host.Shell.QueriesSavedList;
                    SavedQuerySummary query = Assert.Single(workspace.SavedQueries);
                    Assert.Contains(query, list.Items.Cast<object>());
                    list.SelectedItem = query;
                    bool handled = host.PressPointer(list, clickCount: 2);

                    // The press itself never reaches the list's bubbling
                    // MouseDown: WPF raises no twin for a preview the seal
                    // handled (WpfRaisesNoBubblingTwinForAHandledPreview).
                    // What Control raises FRESH from a press it saw past
                    // handled is the double-click pair, on the control itself
                    // — the route the seal's class handler exists for (WB-15
                    // red team: without this leg, deleting that handler kept
                    // the row green). Raised here as Control raises them;
                    // ungated, the MouseDoubleClick runs the query.
                    foreach (RoutedEvent doubleClick in new[] { Control.PreviewMouseDoubleClickEvent, Control.MouseDoubleClickEvent })
                    {
                        var fresh = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                        {
                            RoutedEvent = doubleClick,
                        };
                        list.RaiseEvent(fresh);
                        handled &= fresh.Handled;
                    }

                    PumpedDispatcher.Drain();
                    return (handled, workspace.ActiveGroup.Tabs.Any(tab => tab.IsSavedQueryTab)
                        ? "the saved query ran in a tab"
                        : null);
                }

            default:
                throw new ArgumentOutOfRangeException(nameof(route), route, null);
        }
    }

    /// <summary>
    /// Codex round 7 (low): the wheel. A graph open in the shipped shell as
    /// a diagram pans on a wheel turn and zooms on Ctrl+wheel. Under the
    /// seal — here a thread-modal section that leaves the shell enabled,
    /// the state in which input can still reach it — neither turn moves the
    /// viewport; once the section ends the same turn pans, so the route the
    /// fact drives is live.
    /// </summary>
    [Fact]
    public void AGraphInTheShellNeitherPansNorZoomsUnderTheSeal() => RunSta(() =>
    {
        using var host = new ShippedShellHost(shown: true);
        WorkspaceViewModel workspace = host.AttachDirtyWorkspace();
        workspace.OpenGraph();
        GraphDocumentViewModel document = workspace.GraphDocument!;
        PumpedDispatcher.PumpUntilDrained(document.WhenAllWorkDrained());
        Assert.True(document.SetMode(GraphSurfaceMode.Diagram));
        Assert.True(
            PumpedDispatcher.PumpUntil(() => document.HasLiveDiagram || document.DiagramError is not null, TimeSpan.FromSeconds(30)),
            "the diagram never built");
        Assert.Null(document.DiagramError);
        host.Shell.UpdateLayout();
        GraphSurfaceView surface = Assert.Single(Descendants<GraphSurfaceView>(host.Shell));
        GraphDiagramView diagram = surface.DiagramForTests;
        Assert.True(
            PumpedDispatcher.PumpUntil(() => diagram.Entries.Count > 0, TimeSpan.FromSeconds(10)),
            "the diagram never rendered in the shell");
        Assert.IsType<GraphViewportOutcome.Zoomed>(surface.ViewportCommand(GraphViewportVerb.ActualSize));
        CanvasViewportState before = diagram.Viewport;

        ComponentDispatcher.PushModal();
        try
        {
            Assert.True(host.Palette.IsSealed);
            bool pan = host.Wheel(diagram, 120, ModifierKeys.None);
            bool zoom = host.Wheel(diagram, 120, ModifierKeys.Control);
            PumpedDispatcher.Drain();
            Assert.True(pan && zoom, "the shell let a wheel turn through under the seal");
            Assert.True(
                (diagram.Viewport.PanX, diagram.Viewport.PanY, diagram.Viewport.ZoomPercent)
                    == (before.PanX, before.PanY, before.ZoomPercent),
                $"the graph moved under the seal: pan ({before.PanX}, {before.PanY}) → "
                + $"({diagram.Viewport.PanX}, {diagram.Viewport.PanY}), zoom {before.ZoomPercent}% → {diagram.Viewport.ZoomPercent}%");
        }
        finally
        {
            ComponentDispatcher.PopModal();
        }

        Assert.False(host.Palette.IsSealed);
        _ = host.Wheel(diagram, 120, ModifierKeys.None);
        Assert.Equal(before.PanY + 120, diagram.Viewport.PanY, 6);
    });

    /// <summary>
    /// W7-7 PR 4's click rule (R-5, OD-11c) under the seal: a press on a
    /// populated list's EMPTY area puts the keys on one of its rows — never
    /// while the shell is sealed. The rule is a class handler for the
    /// bubbling MouseDown. WPF's MouseDevice raises no bubbling twin for a
    /// preview the admission handled, and neither does this host's
    /// PressPointer (#1307), so a sealed press cannot reach the rule; what
    /// keeps it out of every other raise of the twin is its registration
    /// WITHOUT handledEventsToo, which the fact reads from the source.
    /// Once the modal section ends the same press lands the keys on the
    /// saved query's row, so the route the fact drives is live.
    /// </summary>
    [Fact]
    public void AClickOnAListsEmptyAreaMovesNoKeysUnderTheSeal() => RunSta(() =>
    {
        string[] rule = CSharpSource.Load("SelectorFocus.cs").Root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(invocation => CSharpSource.Normalize(invocation.Expression) == "EventManager.RegisterClassHandler"
                && CSharpSource.Normalize(invocation.ArgumentList.Arguments[1].Expression) == "Mouse.MouseDownEvent")
            .Select(invocation => CSharpSource.Normalize(invocation.ArgumentList))
            .ToArray();
        Assert.Equal(
            [
                "(typeof(ListBox),Mouse.MouseDownEvent,newMouseButtonEventHandler(ClickedEmptyArea))",
                "(typeof(TreeView),Mouse.MouseDownEvent,newMouseButtonEventHandler(ClickedEmptyArea))",
            ],
            rule);

        using var host = new ShippedShellHost(shown: true, savedQuery: true);
        WorkspaceViewModel workspace = host.AttachDirtyWorkspace();
        workspace.ActiveLeaf = WorkspaceViewModel.Leaves.First(leaf => leaf.Id == "queries");
        PumpedDispatcher.Drain();
        host.Shell.UpdateLayout();
        ListBox list = host.Shell.QueriesSavedList;
        Assert.True(
            PumpedDispatcher.PumpUntil(() => list.IsVisible && list.HasItems, TimeSpan.FromSeconds(10)),
            "premise: the Queries leaf never showed the saved query");
        ListBox rail = host.Shell.RightPaneLeavesList;
        rail.UpdateLayout();
        var held = Assert.IsAssignableFrom<UIElement>(rail.ItemContainerGenerator.ContainerFromIndex(0));
        Assert.True(held.Focus(), "premise: the rail's row refused the keys");

        ComponentDispatcher.PushModal();
        try
        {
            Assert.True(host.Palette.IsSealed);
            bool handled = host.PressPointer(list);
            PumpedDispatcher.Drain();
            Assert.True(handled, "the shell let a press on the list through under the seal");
            Assert.Same(held, Keyboard.FocusedElement);
        }
        finally
        {
            ComponentDispatcher.PopModal();
        }

        Assert.False(host.Palette.IsSealed);
        _ = host.PressPointer(list);
        PumpedDispatcher.Drain();
        var row = Assert.IsType<ListBoxItem>(Keyboard.FocusedElement);
        Assert.Same(list, ItemsControl.ItemsControlFromItemContainer(row));
    });

    /// <summary>
    /// #1307: with the host raising input as WPF does, a press the seal took
    /// reaches no row — on the shipped Files tree, a press on another row
    /// leaves the keys, the selection and the shell's speech where they
    /// were, and once the seal lifts the same press selects and focuses
    /// that row.
    /// </summary>
    [Fact]
    public void ASealedPressOnAFilesTreeRowLeavesFocusSelectionAndSpeechAlone() => RunSta(() =>
    {
        using var host = new ShippedShellHost(shown: true, folder: true);
        FilesSidebarViewModel sidebar = host.AttachFilesSidebar();
        TreeView tree = host.Shell.FilesTree;
        Assert.True(
            PumpedDispatcher.PumpUntil(() => tree.IsVisible && tree.HasItems, TimeSpan.FromSeconds(10)),
            "premise: the Files tree never showed its rows");
        tree.UpdateLayout();
        TreeViewItem[] rows = [.. sidebar.RootNodes.Select(node =>
            Assert.IsAssignableFrom<TreeViewItem>(tree.ItemContainerGenerator.ContainerFromItem(node)))];
        Assert.True(rows.Length >= 2, "premise: the tree has two rows");
        TreeViewItem held = rows[0];
        TreeViewItem pressed = rows[^1];
        Assert.True(held.Focus(), "premise: the first row refused the keys");
        PumpedDispatcher.Drain();
        object? selected = tree.SelectedItem;
        (int palette, int files) heard = (host.Heard.Count, host.SidebarHeard.Count);

        ComponentDispatcher.PushModal();
        try
        {
            Assert.True(host.Palette.IsSealed);
            bool handled = host.PressPointer(pressed);
            PumpedDispatcher.Drain();
            Assert.True(handled, "the shell let a press on a tree row through under the seal");
            Assert.Same(held, Keyboard.FocusedElement);
            Assert.Same(selected, tree.SelectedItem);
            Assert.Equal(heard, (host.Heard.Count, host.SidebarHeard.Count));
        }
        finally
        {
            ComponentDispatcher.PopModal();
        }

        Assert.False(host.Palette.IsSealed);
        _ = host.PressPointer(pressed);
        PumpedDispatcher.Drain();
        Assert.Same(pressed, Keyboard.FocusedElement);
        Assert.Same(pressed.DataContext, tree.SelectedItem);
    });

    /// <summary>
    /// #1307: the same on a grid in the shell — a Bases view's cells. A
    /// sealed press on another row's cell leaves the current cell, the keys,
    /// the selection and the shell's speech alone; once the seal lifts the
    /// press makes that cell current and focused.
    /// </summary>
    [Fact]
    public void ASealedPressOnAGridCellLeavesFocusSelectionCurrentCellAndSpeechAlone() => RunSta(() =>
    {
        using var host = new ShippedShellHost(shown: true, savedQuery: true, folder: true);
        WorkspaceViewModel workspace = host.AttachDirtyWorkspace();
        // A new tab: the attached workspace's current tab is dirty.
        workspace.OpenPath("Notes.base", WorkspaceOpenTarget.NewTab);
        AccessibleDataGrid? grid = null;
        Assert.True(
            PumpedDispatcher.PumpUntil(
                () => (grid = Descendants<AccessibleDataGrid>(host.Shell).FirstOrDefault(candidate => candidate.IsVisible)) is { Grid.Items.Count: >= 2 },
                TimeSpan.FromSeconds(10)),
            $"premise: the base never showed a grid of two rows (the active tab is {workspace.ActiveGroup.ActiveTab?.Title})");
        DataGrid cells = grid!.Grid;
        cells.UpdateLayout();
        Assert.True(grid.FocusFirstCell(), "premise: the grid refused its first cell");
        PumpedDispatcher.Drain();
        DataGridCell held = Assert.IsType<DataGridCell>(Keyboard.FocusedElement);
        var second = Assert.IsType<DataGridRow>(cells.ItemContainerGenerator.ContainerFromIndex(1));
        DataGridCell pressed = Descendants<DataGridCell>(second).First();
        DataGridCellInfo current = cells.CurrentCell;
        object[] selection = [.. cells.SelectedItems.Cast<object>()];
        int heard = host.Heard.Count;

        ComponentDispatcher.PushModal();
        try
        {
            Assert.True(host.Palette.IsSealed);
            bool handled = host.PressPointer(pressed);
            PumpedDispatcher.Drain();
            Assert.True(handled, "the shell let a press on a grid cell through under the seal");
            Assert.Same(held, Keyboard.FocusedElement);
            Assert.Equal(current, cells.CurrentCell);
            Assert.Equal(selection, cells.SelectedItems.Cast<object>());
            Assert.Equal(heard, host.Heard.Count);
        }
        finally
        {
            ComponentDispatcher.PopModal();
        }

        Assert.False(host.Palette.IsSealed);
        _ = host.PressPointer(pressed);
        PumpedDispatcher.Drain();
        Assert.Same(pressed, Keyboard.FocusedElement);
        Assert.Same(second.Item, cells.CurrentCell.Item);
    });

    /// <summary>
    /// #1307, the platform measured: through the input manager — the path
    /// real input takes — WPF's KeyboardDevice and MouseDevice promote a
    /// preview to its bubbling twin only when the preview stayed unhandled.
    /// A preview handler marks the input handled; a listener past handled
    /// on the twin hears nothing. Without the preview handler it hears the
    /// twin once, so the input did reach the element.
    /// </summary>
    [Theory]
    [InlineData("key")]
    [InlineData("pointer")]
    public void WpfRaisesNoBubblingTwinForAHandledPreview(string device) => RunSta(() =>
    {
        var target = new Border { Focusable = true, Background = Brushes.White, Width = 100, Height = 50 };
        var window = new Window
        {
            Content = target,
            Width = 200,
            Height = 100,
            ShowInTaskbar = false,
            ShowActivated = false,
            WindowStyle = WindowStyle.None,
            Left = -10_000,
            Top = -10_000,
            WindowStartupLocation = WindowStartupLocation.Manual,
        };
        window.Show();
        try
        {
            PumpedDispatcher.Drain();
            PresentationSource source = PresentationSource.FromVisual(window)!;
            (RoutedEvent preview, RoutedEvent twin) = device == "key"
                ? (Keyboard.PreviewKeyDownEvent, Keyboard.KeyDownEvent)
                : (Mouse.PreviewMouseDownEvent, Mouse.MouseDownEvent);
            var heard = new List<bool>();
            target.AddHandler(twin, new RoutedEventHandler((_, e) => heard.Add(e.Handled)), handledEventsToo: true);
            bool handlePreview = true;
            target.AddHandler(preview, new RoutedEventHandler((_, e) => e.Handled = handlePreview));

            void Raise()
            {
                if (device == "key")
                {
                    Assert.True(target.Focus(), "premise: the target refused the keys");
                    InputManager.Current.ProcessInput(new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, Key.A)
                    {
                        RoutedEvent = preview,
                    });
                }
                else
                {
                    Assert.True(Mouse.Capture(target), "premise: the target refused the mouse capture");
                    try
                    {
                        InputManager.Current.ProcessInput(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                        {
                            RoutedEvent = preview,
                        });
                    }
                    finally
                    {
                        _ = Mouse.Capture(null);
                    }
                }

                PumpedDispatcher.Drain();
            }

            Raise();
            Assert.True(heard.Count == 0, $"WPF raised the {device} twin for a handled preview (handled: {string.Join(",", heard)})");
            handlePreview = false;
            Raise();
            Assert.Equal([false], heard);
        }
        finally
        {
            window.Close();
        }
    });

    /// <summary>#1307: the host raises a bubbling twin only for a preview
    /// that stayed unhandled, as WPF's MouseDevice, Keyboard and
    /// TextCompositionManager do — each helper, with a preview handler that
    /// marks the input handled, leaves a bubbling counter at 0, and without
    /// it reaches the counter once.</summary>
    [Fact]
    public void PressPointerRaisesNoTwinForAHandledPreview() => AssertNoTwinForAHandledPreview(
        (host, target) => host.PressPointer(target),
        [Mouse.PreviewMouseDownEvent, Mouse.PreviewMouseUpEvent],
        [Mouse.MouseDownEvent, Mouse.MouseUpEvent]);

    [Fact]
    public void WheelRaisesNoTwinForAHandledPreview() => AssertNoTwinForAHandledPreview(
        (host, target) => host.Wheel(target, -120, ModifierKeys.None),
        [Mouse.PreviewMouseWheelEvent],
        [Mouse.MouseWheelEvent]);

    [Fact]
    public void ReleaseKeyRaisesNoTwinForAHandledPreview() => AssertNoTwinForAHandledPreview(
        (host, target) => host.ReleaseKey(target, Key.Apps),
        [Keyboard.PreviewKeyUpEvent],
        [Keyboard.KeyUpEvent]);

    [Fact]
    public void TypeTextRaisesNoTwinForAHandledPreview() => AssertNoTwinForAHandledPreview(
        (_, target) => ShippedShellHost.TypeText(target, "r"),
        [TextCompositionManager.PreviewTextInputEvent],
        [TextCompositionManager.TextInputEvent]);

    private static void AssertNoTwinForAHandledPreview(
        Func<ShippedShellHost, UIElement, bool> raise, RoutedEvent[] previews, RoutedEvent[] twins) => RunSta(() =>
    {
        using var host = new ShippedShellHost();
        foreach (bool previewHandles in new[] { true, false })
        {
            var target = new Border();
            int heard = 0;
            foreach (RoutedEvent preview in previews)
            {
                target.AddHandler(preview, new RoutedEventHandler((_, e) => e.Handled = previewHandles));
            }

            foreach (RoutedEvent twin in twins)
            {
                target.AddHandler(twin, new RoutedEventHandler((_, _) => heard++));
            }

            bool handled = raise(host, target);
            Assert.Equal(previewHandles, handled);
            Assert.True(
                heard == (previewHandles ? 0 : twins.Length),
                $"the bubbling twin was raised {heard} time(s) for a preview that "
                + (previewHandles ? "was handled — WPF raises none" : "stayed unhandled — WPF raises one each"));
        }
    });

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

    private static MenuItem FileMenu(ShippedShellHost host)
    {
        MenuItem file = Assert.IsType<MenuItem>(host.Shell.MainMenu.Items[0]);
        Assert.Equal("_File", file.Header);
        Assert.True(file.IsVisible, "the File menu is not visible, so no input could reach it");
        return file;
    }

    /// <summary>
    /// The shipped shell — MoveToFocusTests' and SheetKeyboardFenceTests'
    /// shape: the real MainWindow, its XAML, lifecycle, handlers, registry
    /// and commands, with a fixture vault attached through the lifecycle's
    /// own setters and the palette's recents file in the fixture. Key
    /// presses are raised on their target element with an off-screen
    /// window's presentation source, so the window-level route runs as it
    /// does for a real key. Unshown by default; <c>shown</c> puts the shell
    /// on its own off-screen window (placement kept in the fixture, never
    /// activated), so the access key manager — which targets only visible
    /// elements in a live source — can reach its menu.
    /// </summary>
    internal sealed class ShippedShellHost : IDisposable
    {
        private readonly FixtureVault _fixture = FixtureVault.Create(1, "palette-shell-keys");
        private readonly Func<bool> _priorOverlayProbe = CanvasSurfaceView.ShellOverlayIsOpen;
        private readonly VaultSession _session;
        private readonly VaultLifecycleViewModel _lifecycle;
        private readonly Window _inputSource;
        private readonly string _recentsPath;
        private WorkspaceViewModel? _workspace;
        private QuickSwitcherViewModel? _switcher;
        private FilesSidebarViewModel? _sidebar;

        /// <param name="folder">Adds a collapsed folder holding one note, so
        /// the Files tree has a row that expands.</param>
        public ShippedShellHost(bool shown = false, bool savedQuery = false, bool folder = false)
        {
            Assert.Null(Application.Current);
            if (folder)
            {
                Directory.CreateDirectory(Path.Combine(_fixture.Root, "folder"));
                File.WriteAllText(Path.Combine(_fixture.Root, "folder", "inner.md"), "# Inner\n");
            }

            if (savedQuery)
            {
                File.WriteAllText(
                    Path.Combine(_fixture.Root, "Notes.base"),
                    "filters: 'file.ext == \"md\"'\nviews:\n  - type: table\n    name: Main\n    order:\n      - file.name\n");
            }

            _session = VaultSession.OpenFilesystem(_fixture.Root);
            using (var cancel = new CancelToken())
            {
                _session.ScanInitial(cancel);
            }

            if (savedQuery)
            {
                // BasesQueriesTests' saved query: the base's first view.
                ulong scratch = _session.OpenBase("Notes.base");
                try
                {
                    _ = _session.SaveQuery(
                        "All notes",
                        description: null,
                        _session.BaseViewQueryJson(scratch, 0),
                        SavedQuerySourceSyntax.Builder);
                }
                finally
                {
                    _session.CloseBase(scratch);
                }
            }

            _recentsPath = Path.Combine(_fixture.Root, "device-state", "command-palette-recents.json");
            Shell = new MainWindow(new CommandPaletteRecentsStore(_recentsPath), paletteLane: null);
            _lifecycle = Assert.IsType<VaultLifecycleViewModel>(Shell.DataContext);
            SetField("_session", _session);
            SetField("_vaultPath", _fixture.Root);
            SetProperty(nameof(VaultLifecycleViewModel.IsVaultOpen), true);

            FieldInfo announce = typeof(CommandPaletteViewModel).GetField(
                    "_announce", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("CommandPaletteViewModel._announce is gone");
            var funnel = (Action<A11yEvent>)announce.GetValue(Palette)!;
            announce.SetValue(Palette, (Action<A11yEvent>)(announcement =>
            {
                Heard.Add(announcement);
                funnel(announcement);
            }));

            // KeyEventArgs needs a live PresentationSource; the unshown
            // shell has none, so an off-screen stub lends its own.
            _inputSource = new Window
            {
                Content = new Border(),
                Width = 200,
                Height = 100,
                ShowInTaskbar = false,
                ShowActivated = false,
                WindowStyle = WindowStyle.None,
                Left = -10_000,
                Top = -10_000,
                WindowStartupLocation = WindowStartupLocation.Manual,
            };
            _inputSource.Show();

            if (shown)
            {
                // Placement is restored on the source's creation and saved
                // on close: both go to the fixture, never the user's file.
                (typeof(MainWindow).GetField("_windowPlacement", BindingFlags.NonPublic | BindingFlags.Instance)
                        ?? throw new InvalidOperationException("MainWindow._windowPlacement is gone"))
                    .SetValue(Shell, new WindowPlacementManager(
                        Shell, new WindowStateStore(Path.Combine(_fixture.Root, "device-state", "window.json"))));

                // A rendered editor needs the theme the app would supply.
                Shell.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri(
                        "pack://application:,,,/SlateWindows;component/Themes/Slate.Light.xaml",
                        UriKind.Absolute),
                });
                Shell.WindowStartupLocation = WindowStartupLocation.Manual;
                Shell.Left = -10_000;
                Shell.Top = -10_000;
                Shell.ShowActivated = false;
                Shell.ShowInTaskbar = false;
                Shell.Show();
                PumpedDispatcher.Drain();
            }
        }

        public MainWindow Shell { get; }

        public string VaultRoot => _fixture.Root;

        public VaultLifecycleViewModel Lifecycle => _lifecycle;

        public CommandPaletteViewModel Palette => _lifecycle.Palette;

        /// <summary>What the palette has announced, in order.</summary>
        public List<A11yEvent> Heard { get; } = [];

        public WorkspaceViewModel AttachDirtyWorkspace()
        {
            _workspace = new WorkspaceViewModel(
                _session,
                _fixture.Root,
                () => [],
                _ => { },
                startInteractionBackgroundWork: false,
                preferencesStore: new AppPreferencesStore(Path.Combine(_fixture.Root, "preferences.json")));
            SetProperty(nameof(VaultLifecycleViewModel.Workspace), _workspace);
            _workspace.OpenPath("note0.md");
            _workspace.ActiveGroup.ActiveTab!.Text += "\nUnsaved.";
            Assert.True(_workspace.HasDirtyTabs);
            _workspace.RefreshBaseQueries();
            PumpedDispatcher.Drain();
            return _workspace;
        }

        public QuickSwitcherViewModel AttachQuickOpen()
        {
            _switcher = new QuickSwitcherViewModel(_session, _fixture.Root, _ => { }, localAppDataRoot: _fixture.Root);
            SetProperty(nameof(VaultLifecycleViewModel.QuickSwitcher), _switcher);
            return _switcher;
        }

        /// <summary>What the Files sidebar has announced, in order.</summary>
        public List<A11yEvent> SidebarHeard { get; } = [];

        /// <summary>The shell's Files sidebar over the fixture, its tree
        /// loaded (FilesRegionLandingTests' shape, in the shipped
        /// window).</summary>
        public FilesSidebarViewModel AttachFilesSidebar()
        {
            _sidebar = new FilesSidebarViewModel(_session, SidebarHeard.Add, localAppDataRoot: _fixture.Root);
            PumpedDispatcher.PumpUntilDrained(_sidebar.TreeRefreshCompletion);
            SetProperty(nameof(VaultLifecycleViewModel.FileSidebar), _sidebar);
            PumpedDispatcher.Drain();
            return _sidebar;
        }

        public string[] PersistedRecents() => new CommandPaletteRecentsStore(_recentsPath).Load();

        /// <summary>The shell's unsaved-changes prompt with only its native
        /// box replaced by a message-box shaped loop over the shown shell,
        /// answering Cancel (<see cref="InstallClosePrompt"/>).</summary>
        public SyntheticPrompt InstallClosePrompt() =>
            CommandPaletteTests.InstallClosePrompt(Shell, fallback: null, LoopSignals.DisablesShell, MessageBoxResult.Cancel);

        /// <summary>A real key press on <paramref name="target"/>: Preview,
        /// then — unhandled — KeyDown, with exactly
        /// <paramref name="modifiers"/> held in the thread's key state.
        /// Returns whether the press was handled.</summary>
        public bool Press(UIElement target, Key key, ModifierKeys modifiers) =>
            Press(target, key, modifiers, system: false);

        /// <summary>A system key press — what WPF raises for Alt+letter:
        /// <c>Key.System</c>, the letter in <c>SystemKey</c>.</summary>
        public bool PressSystem(UIElement target, Key key, ModifierKeys modifiers) =>
            Press(target, key, modifiers, system: true);

        /// <summary>The mnemonic WPF's loop hands the access key manager for
        /// an unhandled Alt+<paramref name="key"/>: processed in the scope of
        /// the shell's presentation source with Alt held, as it is for a real
        /// key — the main menu joins the window's scope only while Alt is
        /// down, and keeps a scope of its own otherwise.</summary>
        public void Mnemonic(string key) =>
            ThreadModifiers.Hold(
                ModifierKeys.Alt,
                () => _ = AccessKeyManager.ProcessKey(PresentationSource.FromVisual(Shell)!, key, false));

        /// <summary>A left-button press and release on
        /// <paramref name="target"/> as the input manager raises them
        /// (<see cref="PressPointerDown"/>, then <see cref="ReleasePointer"/>).
        /// Returns whether both were handled.</summary>
        public bool PressPointer(UIElement target, int clickCount = 1) =>
            PressPointerDown(target, clickCount) & ReleasePointer(target);

        /// <summary>A left-button press on <paramref name="target"/> as WPF's
        /// MouseDevice raises it: Preview, then — only if the preview stayed
        /// unhandled — its bubbling twin (#1307: WPF raises no twin for a
        /// handled preview), the press with <paramref name="clickCount"/>.
        /// Raised over <c>Mouse.PrimaryDevice</c> with its button state
        /// unchanged, so <c>ButtonBase</c> never presses. Returns whether the
        /// press was handled.</summary>
        public bool PressPointerDown(UIElement target, int clickCount = 1) =>
            RaisePair(target, PointerArgs(Mouse.PreviewMouseDownEvent, clickCount), () => PointerArgs(Mouse.MouseDownEvent, clickCount));

        /// <summary>A left-button release on <paramref name="target"/>:
        /// Preview, then — unhandled — its bubbling twin. Returns whether
        /// the release was handled.</summary>
        public bool ReleasePointer(UIElement target) =>
            RaisePair(target, PointerArgs(Mouse.PreviewMouseUpEvent, 1), () => PointerArgs(Mouse.MouseUpEvent, 1));

        /// <summary>A key release on <paramref name="target"/>: Preview,
        /// then — unhandled — KeyUp, from the target's own presentation
        /// source (so it works in a window the shell owns). Returns whether
        /// the release was handled.</summary>
        public bool ReleaseKey(UIElement target, Key key)
        {
            PresentationSource source = SourceOf(target);
            return RaisePair(
                target,
                KeyArgs(source, key, system: false, Keyboard.PreviewKeyUpEvent),
                () => KeyArgs(source, key, system: false, Keyboard.KeyUpEvent));
        }

        /// <summary>Text arriving on <paramref name="target"/> as the text
        /// composition manager raises it: PreviewTextInput, then — unhandled
        /// — TextInput. Returns whether the text was handled.</summary>
        public static bool TypeText(UIElement target, string text)
        {
            var composition = new TextComposition(InputManager.Current, target, text);
            return RaisePair(
                target,
                new TextCompositionEventArgs(Keyboard.PrimaryDevice, composition)
                {
                    RoutedEvent = TextCompositionManager.PreviewTextInputEvent,
                },
                () => new TextCompositionEventArgs(Keyboard.PrimaryDevice, composition)
                {
                    RoutedEvent = TextCompositionManager.TextInputEvent,
                });
        }

        /// <summary>A wheel turn on <paramref name="target"/> with exactly
        /// <paramref name="modifiers"/> held: Preview, then — only if the
        /// preview stayed unhandled — its bubbling twin. Returns whether it
        /// was handled.</summary>
        public bool Wheel(UIElement target, int delta, ModifierKeys modifiers)
        {
            bool handled = false;
            ThreadModifiers.Hold(modifiers, () => handled = RaisePair(
                target,
                new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, delta)
                {
                    RoutedEvent = Mouse.PreviewMouseWheelEvent,
                },
                () => new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, delta)
                {
                    RoutedEvent = Mouse.MouseWheelEvent,
                }));
            return handled;
        }

        /// <summary>The input manager's shape for every pair: the preview,
        /// then the bubbling twin only when the preview stayed
        /// unhandled.</summary>
        private static bool RaisePair(UIElement target, RoutedEventArgs preview, Func<RoutedEventArgs> twin)
        {
            target.RaiseEvent(preview);
            if (preview.Handled)
            {
                return true;
            }

            RoutedEventArgs bubble = twin();
            target.RaiseEvent(bubble);
            return bubble.Handled;
        }

        private PresentationSource SourceOf(UIElement target) =>
            PresentationSource.FromVisual(target) ?? PresentationSource.FromVisual(_inputSource)!;

        public void Dispose()
        {
            try
            {
                // Detach the dirty workspace first: closing the shell asks
                // PrepareForApplicationClose, which would raise the real
                // unsaved-changes prompt over a dirty tab.
                if (_workspace is not null)
                {
                    SetProperty(nameof(VaultLifecycleViewModel.Workspace), null);
                    _workspace.Dispose();
                }

                if (_sidebar is not null)
                {
                    SetProperty(nameof(VaultLifecycleViewModel.FileSidebar), null);
                    PumpedDispatcher.PumpUntilDrained(_sidebar.BeginSessionShutdownAndCaptureWork().SessionWork);
                }

                if (_switcher is not null)
                {
                    _switcher.Dismiss();
                    SetProperty(nameof(VaultLifecycleViewModel.QuickSwitcher), null);
                    _switcher.Dispose();
                }

                SetProperty(nameof(VaultLifecycleViewModel.IsVaultOpen), false);
                SetField("_session", null);
                SetField("_vaultPath", string.Empty);
                _inputSource.Close();
                Shell.Close();
                PumpedDispatcher.Drain();
                _session.Dispose();
                _fixture.Dispose();
            }
            finally
            {
                CanvasSurfaceView.ShellOverlayIsOpen = _priorOverlayProbe;
            }
        }

        private static MouseButtonEventArgs PointerArgs(RoutedEvent routed, int clickCount)
        {
            var args = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
            {
                RoutedEvent = routed,
            };

            // What the mouse device stamps on a second click; internal set.
            (typeof(MouseButtonEventArgs).GetProperty(nameof(MouseButtonEventArgs.ClickCount))?.GetSetMethod(nonPublic: true)
                    ?? throw new InvalidOperationException("MouseButtonEventArgs.ClickCount has no setter"))
                .Invoke(args, [clickCount]);
            Assert.Equal(clickCount, args.ClickCount);
            return args;
        }

        private static KeyEventArgs KeyArgs(PresentationSource source, Key key, bool system, RoutedEvent routed)
        {
            var args = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key)
            {
                RoutedEvent = routed,
            };
            if (system)
            {
                (typeof(KeyEventArgs).GetMethod("MarkSystem", BindingFlags.NonPublic | BindingFlags.Instance)
                        ?? throw new InvalidOperationException("KeyEventArgs.MarkSystem is gone"))
                    .Invoke(args, null);
                Assert.Equal((Key.System, key), (args.Key, args.SystemKey));
            }

            return args;
        }

        private bool Press(UIElement target, Key key, ModifierKeys modifiers, bool system)
        {
            bool handled = false;
            ThreadModifiers.Hold(modifiers, () =>
            {
                PresentationSource source = PresentationSource.FromVisual(_inputSource)!;
                KeyEventArgs preview = KeyArgs(source, key, system, Keyboard.PreviewKeyDownEvent);
                target.RaiseEvent(preview);
                handled = preview.Handled;
                if (!handled)
                {
                    KeyEventArgs down = KeyArgs(source, key, system, Keyboard.KeyDownEvent);
                    target.RaiseEvent(down);
                    handled = down.Handled;
                }
            });
            return handled;
        }

        private void SetField(string name, object? value) =>
            (typeof(VaultLifecycleViewModel).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException($"VaultLifecycleViewModel.{name} is gone"))
            .SetValue(_lifecycle, value);

        private void SetProperty(string name, object? value) =>
            (typeof(VaultLifecycleViewModel).GetProperty(name, BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException($"VaultLifecycleViewModel.{name} is gone"))
            .SetValue(_lifecycle, value);
    }

    /// <summary>
    /// Holds exactly the given modifiers down in the thread's key state —
    /// what <c>Keyboard.Modifiers</c> reads — for the length of a press,
    /// re-applying until it reads back exactly (another process's injected
    /// input can resynchronize it on a shared desktop), then restores it.
    /// SheetKeyboardFenceTests' <c>WithModifiers</c>, for these facts.
    /// </summary>
    internal static class ThreadModifiers
    {
        private const int VkShift = 0x10;
        private const int VkControl = 0x11;
        private const int VkMenu = 0x12;
        private const int VkLeftShift = 0xA0;
        private const int VkLeftControl = 0xA2;
        private const int VkLeftMenu = 0xA4;
        private const byte KeyDown = 0x80;
        private static readonly int[] AllModifierKeys =
            [0x10, 0x11, 0x12, 0xA0, 0xA1, 0xA2, 0xA3, 0xA4, 0xA5, 0x5B, 0x5C];

        public static void Hold(ModifierKeys modifiers, Action press)
        {
            byte[] saved = new byte[256];
            Assert.True(GetKeyboardState(saved));
            byte[] pressed = (byte[])saved.Clone();
            foreach (int modifier in AllModifierKeys)
            {
                pressed[modifier] &= unchecked((byte)~KeyDown);
            }

            if (modifiers.HasFlag(ModifierKeys.Shift))
            {
                pressed[VkShift] = pressed[VkLeftShift] = KeyDown;
            }

            if (modifiers.HasFlag(ModifierKeys.Control))
            {
                pressed[VkControl] = pressed[VkLeftControl] = KeyDown;
            }

            if (modifiers.HasFlag(ModifierKeys.Alt))
            {
                pressed[VkMenu] = pressed[VkLeftMenu] = KeyDown;
            }

            try
            {
                for (int attempt = 0; ; attempt++)
                {
                    Assert.True(SetKeyboardState(pressed));
                    if (Keyboard.Modifiers == modifiers)
                    {
                        break;
                    }

                    Assert.True(
                        attempt < 40,
                        $"environmental: the thread's key state would not hold {modifiers} "
                        + $"(read back {Keyboard.Modifiers}) — another process is holding modifiers");
                    Thread.Sleep(25);
                }

                press();
            }
            finally
            {
                _ = SetKeyboardState(saved);
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetKeyboardState(byte[] keyState);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetKeyboardState(byte[] keyState);
    }
}
