// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// W7-7 (#1248, contract R-6): a sheet fences Tab. The NVDA pass found
// Tab in the template prompt sheet typed tab characters into the note
// behind it (record F5): the prompt TextBox declines TabForward, and the
// overlay — a focus scope — hands the unanswered command to the parent
// scope's focused element, the editor's TextArea. The harness reproduces
// that shape — a SlateTextEditor that last held focus, and a focus-scope
// sheet over it — and drives it three ways: REAL keystrokes through the
// input system (InputManager, so the PreviewKeyDown → KeyDown → command
// → KeyboardNavigation pipeline runs as it does for a key press), the two
// Tab commands as a command source drives them (ask, then execute on
// yes), and the two commands executed directly. The last fact runs the
// shipped MainWindow, to show the fence and the modal routing that owns
// shell chords under a sheet (contract 30 TR-7) leave each other alone.

using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using ICSharpCode.AvalonEdit.Document;
using SlateWindows.Canvas;
using SlateWindows.Grids;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

public sealed class SheetKeyboardFenceTests
{
    /// <summary>The note behind the sheet. Its caret line is indented so
    /// a leaked TabBackward (Shift+Tab, which unindents the caret line)
    /// changes the text as visibly as a leaked TabForward does.</summary>
    private const string Note = "\tIndented first line\nSecond line\n";

    /// <summary>
    /// Tab moves Topic → Attendees and Shift+Tab moves back, inside the
    /// sheet, and neither key changes the note behind it. The traversal is
    /// WPF's own: the fence refuses the command at the sheet's edge and
    /// lets the key route on (<c>ContinueRouting</c>), so KeyboardNavigation
    /// moves focus in the sheet's Cycle scope — with the key stopped too,
    /// focus would never leave Topic.
    /// </summary>
    [Fact]
    public void TabInsideASheetNeverReachesTheEditor() => RunSta(() =>
    {
        using var harness = new Harness(fenced: true);
        harness.FocusAfterTheEditor(harness.Topic);

        harness.Press(Key.Tab);
        Assert.Equal(Note, harness.Editor.Text);
        Assert.Same(harness.Attendees, Keyboard.FocusedElement);

        harness.Press(Key.Tab, ModifierKeys.Shift);
        Assert.Equal(Note, harness.Editor.Text);
        Assert.Same(harness.Topic, Keyboard.FocusedElement);

        // Three presses — the recorded repro — cycle inside the sheet:
        // Topic → Attendees → Done → Topic.
        harness.Press(Key.Tab);
        harness.Press(Key.Tab);
        Assert.Same(harness.Done, Keyboard.FocusedElement);
        harness.Press(Key.Tab);
        Assert.Same(harness.Topic, Keyboard.FocusedElement);
        Assert.Equal(Note, harness.Editor.Text);
    });

    /// <summary>
    /// The harness reproduces F5 without the fence: Tab types into the
    /// note and focus never moves, and Shift+Tab unindents the note's
    /// caret line. This is what makes the facts here evidence rather than
    /// a harness that could not have leaked in the first place.
    /// </summary>
    [Fact]
    public void WithoutTheFenceTabReachesTheEditorBehindTheSheet() => RunSta(() =>
    {
        using var harness = new Harness(fenced: false);
        harness.FocusAfterTheEditor(harness.Topic);

        harness.Press(Key.Tab);
        Assert.Same(harness.Topic, Keyboard.FocusedElement);
        Assert.Equal(Note.Insert(1, "\t"), harness.Editor.Text);

        harness.ResetNote();
        harness.FocusAfterTheEditor(harness.Topic);
        harness.Press(Key.Tab, ModifierKeys.Shift);
        Assert.Same(harness.Topic, Keyboard.FocusedElement);
        Assert.Equal("Indented first line\nSecond line\n", harness.Editor.Text);

        // The IME route arrives too, so the IME fact is not vacuous.
        harness.ResetNote();
        harness.FocusAfterTheEditor(harness.Topic);
        harness.Press(Key.Tab, imeProcessed: true);
        Assert.Equal(Note.Insert(1, "\t"), harness.Editor.Text);

        // So does a command source, for both commands: the sheet's focus
        // scope hands the query to the editor, which answers yes, and the
        // execution follows it there.
        harness.ResetNote();
        harness.FocusAfterTheEditor(harness.Topic);
        Assert.True(harness.RunAsACommandSource(EditingCommands.TabForward, harness.Topic));
        Assert.Equal(Note.Insert(1, "\t"), harness.Editor.Text);
        harness.ResetNote();
        harness.FocusAfterTheEditor(harness.Topic);
        Assert.True(harness.RunAsACommandSource(EditingCommands.TabBackward, harness.Topic));
        Assert.Equal("Indented first line\nSecond line\n", harness.Editor.Text);
    });

    /// <summary>
    /// The edge's refusal, driven the way a command source drives a
    /// command — ask, and execute only on yes. Neither Tab command is
    /// answered for a field inside the sheet that does not answer it
    /// itself: unfenced, the sheet's focus scope hands the query to the
    /// editor, which answers yes, and the execution that follows reaches
    /// it (the control fact above). The editor asked directly still
    /// answers: the fence is the sheet's, not the editor's.
    /// </summary>
    [Fact]
    public void ACommandSourceInsideAFencedSheetGetsNoAnswerForEitherTabCommand() => RunSta(() =>
    {
        using var harness = new Harness(fenced: true);
        harness.FocusAfterTheEditor(harness.Topic);

        // The note first, after each command: what reaches the editor is
        // the defect; the answer is how the edge is seen doing its part.
        bool forward = harness.RunAsACommandSource(EditingCommands.TabForward, harness.Topic);
        Assert.Equal(Note, harness.Editor.Text);
        bool backward = harness.RunAsACommandSource(EditingCommands.TabBackward, harness.Topic);
        Assert.Equal(Note, harness.Editor.Text);
        Assert.False(forward, "TabForward was answered inside the sheet");
        Assert.False(backward, "TabBackward was answered inside the sheet");
        Assert.True(EditingCommands.TabForward.CanExecute(null, harness.Editor.TextArea));
    });

    /// <summary>
    /// The PreviewExecuted guard. A direct Execute never asks CanExecute —
    /// RoutedCommand.ExecuteImpl raises PreviewExecuted and Executed and
    /// nothing else — so the edge's refusal is never consulted, and an
    /// unhandled Executed that reaches the sheet is handed to the editor.
    /// </summary>
    [Fact]
    public void ExecutingEitherTabCommandInsideAFencedSheetLeavesTheEditorAlone() => RunSta(() =>
    {
        using var harness = new Harness(fenced: true);
        harness.FocusAfterTheEditor(harness.Topic);

        // One at a time: a leaked TabForward indents the caret line and a
        // leaked TabBackward unindents it again, so checking only after
        // both would pass with both leaking.
        EditingCommands.TabForward.Execute(null, harness.Topic);
        Assert.Equal(Note, harness.Editor.Text);
        EditingCommands.TabBackward.Execute(null, harness.Topic);
        Assert.Equal(Note, harness.Editor.Text);
        Assert.Same(harness.Topic, Keyboard.FocusedElement);
    });

    /// <summary>
    /// A Tab the IME has claimed stays the IME's, and still never reaches
    /// the note. <c>Key.ImeProcessed</c> means the IME claimed the key; TSF
    /// hands it to the IME only if the preview reaches the end of its route
    /// unhandled (TextServicesManager.PostProcessInput), and WPF's own
    /// navigation never moves on it — so the fence takes no key, and the
    /// preview arrives at the field unhandled. When the keyboard device
    /// promotes the key anyway (no TSF, or an IMM32 IME), its gesture
    /// matches TabForward/TabBackward through the real key, and the edge
    /// refuses both.
    /// </summary>
    [Fact]
    public void AnImeClaimedTabStaysWithTheImeAndNeverReachesTheEditor() => RunSta(() =>
    {
        using var harness = new Harness(fenced: true);
        harness.FocusAfterTheEditor(harness.Topic);
        var previews = new List<(Key Key, bool Handled)>();
        harness.Topic.AddHandler(
            Keyboard.PreviewKeyDownEvent,
            new KeyEventHandler((_, e) => previews.Add((e.ImeProcessedKey, e.Handled))),
            handledEventsToo: true);

        harness.Press(Key.Tab, imeProcessed: true);
        Assert.Equal(Note, harness.Editor.Text);
        harness.Press(Key.Tab, ModifierKeys.Shift, imeProcessed: true);
        Assert.Equal(Note, harness.Editor.Text);

        Assert.Equal([(Key.Tab, false), (Key.Tab, false)], previews);
        Assert.Same(harness.Topic, Keyboard.FocusedElement);
    });

    /// <summary>
    /// A field that accepts Tab keeps it: the tab lands in the field
    /// itself — from the key, from Shift+Tab (a plain TextBox inserts a
    /// tab for both), and from a command source — never in the note, and
    /// focus stays put. The field answers the command before its query can
    /// reach the edge, which is why the refusal sits at the edge (a
    /// refusal on the way DOWN would reach it first); the guard lets the
    /// execution through because the field answers.
    /// </summary>
    [Fact]
    public void AnAcceptsTabFieldInASheetKeepsItsOwnTab() => RunSta(() =>
    {
        using var harness = new Harness(fenced: true, withNotes: true);
        TextBox notes = harness.Notes!;
        harness.FocusAfterTheEditor(notes);

        harness.Press(Key.Tab);
        Assert.Equal(Note, harness.Editor.Text);
        Assert.Equal("\t", notes.Text);
        Assert.Same(notes, Keyboard.FocusedElement);

        harness.Press(Key.Tab, ModifierKeys.Shift);
        Assert.Equal(Note, harness.Editor.Text);
        Assert.Equal("\t\t", notes.Text);

        Assert.True(harness.RunAsACommandSource(EditingCommands.TabForward, notes), "the field did not answer its own TabForward");
        Assert.Equal(Note, harness.Editor.Text);
        Assert.Equal("\t\t\t", notes.Text);
        Assert.Same(notes, Keyboard.FocusedElement);
    });

    /// <summary>
    /// An editing grid cell keeps its Tab: the AccessibleDataGrid's cell
    /// editor commits its draft and moves to the next cell on Tab and the
    /// previous one on Shift+Tab (the mac keys, contract C7/C8), inside the
    /// sheet, with the note untouched. The editor's own PreviewKeyDown
    /// handles the key — nothing on the way down may take it first.
    /// </summary>
    [Fact]
    public void AnEditingGridCellInASheetCommitsAndMovesOnTab() => RunSta(() =>
    {
        using var harness = new Harness(fenced: true, withGrid: true);
        AccessibleDataGrid grid = harness.Grid!;
        var commits = new List<(object Row, int Column, string Text, GridEditCommitNavigation Navigation)>();
        int cancels = 0;
        grid.ConfigureEditing(
            editDraft: (row, column) => column == 0 ? ((Person)row).Name : ((Person)row).Role,
            editCommit: (row, column, text, navigation) =>
            {
                commits.Add((row, column, text, navigation));
                grid.MoveCurrentCell(navigation);
            },
            editCancel: () => cancels++,
            editRefused: (_, _) => { });
        Assert.True(grid.FocusFirstCell(), "the grid's first cell did not take focus");
        Person charlie = harness.People[0];

        harness.EditCell(charlie, 0).Text = "Dana";
        harness.Press(Key.Tab);
        Assert.Equal(Note, harness.Editor.Text);
        Assert.Equal([(charlie, 0, "Dana", GridEditCommitNavigation.Next)], commits);
        Assert.Equal("Role", Assert.IsType<DataGridCell>(Keyboard.FocusedElement).Column.Header);
        Assert.Equal(1, grid.CurrentColumnIndexForTests());

        harness.EditCell(charlie, 1).Text = "Lead";
        harness.Press(Key.Tab, ModifierKeys.Shift);
        Assert.Equal(Note, harness.Editor.Text);
        Assert.Equal((charlie, 1, "Lead", GridEditCommitNavigation.Previous), commits[^1]);
        Assert.Equal("Name", Assert.IsType<DataGridCell>(Keyboard.FocusedElement).Column.Header);
        Assert.Equal(0, cancels);
        Assert.False(grid.IsEditSessionOpen);
    });

    /// <summary>
    /// A grid's cells keep WPF's own Tab handling: DataGrid.OnTabKeyDown
    /// moves the cell selection with focus. A fence that took Tab on the
    /// way down left the selection behind on the first cell while focus
    /// walked on (measured, 2026-09-22).
    /// </summary>
    [Fact]
    public void AGridInASheetKeepsItsOwnTabTraversal() => RunSta(() =>
    {
        using var harness = new Harness(fenced: true, withGrid: true);
        AccessibleDataGrid grid = harness.Grid!;
        Assert.True(grid.FocusFirstCell(), "the grid's first cell did not take focus");

        harness.Press(Key.Tab);

        DataGridCell focused = Assert.IsType<DataGridCell>(Keyboard.FocusedElement);
        Assert.Equal("Role", focused.Column.Header);
        DataGridCellInfo selected = Assert.Single(grid.Grid.SelectedCells);
        Assert.Same(focused.Column, selected.Column);
        Assert.Same(focused.DataContext, selected.Item);
        Assert.Equal(Note, harness.Editor.Text);
    });

    /// <summary>
    /// Nothing but the two Tab commands is fenced: an editing chord typed
    /// in the sheet's own field still reaches that field.
    /// </summary>
    [Fact]
    public void TheSheetsOwnEditingChordsStillReachItsField() => RunSta(() =>
    {
        using var harness = new Harness(fenced: true);
        harness.FocusAfterTheEditor(harness.Topic);
        harness.Topic.Text = "Quarterly sync";
        harness.Topic.CaretIndex = 0;

        harness.Press(Key.A, ModifierKeys.Control);

        Assert.Equal("Quarterly sync", harness.Topic.SelectedText);
        Assert.Equal(Note, harness.Editor.Text);
    });

    /// <summary>
    /// The fence beside the shipped modal routing, in the real shell:
    /// MainWindow.xaml's own Add property overlay, fenced by the XAML.
    /// Inside it the sheet's field keeps its editing chord (Ctrl+A), and
    /// Ctrl+Shift+R — which travels through the fenced overlay to the
    /// window's KeyBinding — is still refused by #1118's admission, so no
    /// bulk-rename sheet presents behind the scrim (contract 30 TR-7).
    /// The same chord with no sheet up does present one: the refusal is
    /// the routing's, not a key this harness never delivered.
    /// </summary>
    /// <remarks>
    /// This pins the ROUTING's side of the coexistence: each leg fails
    /// when the shipped routing swallows the editing chord under a sheet
    /// or admits the shell chord beneath one. The fence's own side — that
    /// it intercepts nothing but the two Tab commands — is
    /// <see cref="TheSheetsOwnEditingChordsStillReachItsField"/>.
    /// </remarks>
    [Fact]
    public void TheShippedModalRoutingStillGovernsChordsInsideAFencedSheet() => RunSta(() =>
    {
        using var host = new ShellHost();
        WorkspaceViewModel workspace = host.Workspace;

        host.Press(host.Element<UIElement>("FilesTree"), Key.R, ModifierKeys.Control | ModifierKeys.Shift);
        Assert.NotNull(workspace.BulkRenameSheet);
        workspace.CloseBulkRenameSheetCommand.Execute(null);
        Assert.Null(workspace.BulkRenameSheet);

        workspace.OpenAddPropertySheet(synchronousForTests: true);
        Assert.Equal(ModalSurface.AddProperty, host.Shell.OpenModalSurface);
        Assert.True(SheetKeyboardFence.GetIsEnabled(host.Element<UIElement>("AddPropertyOverlay")));
        TextBox key = host.Element<TextBox>("AddPropertyKeyTextBox");
        key.Text = "status";
        key.CaretIndex = 0;

        host.Press(key, Key.A, ModifierKeys.Control);
        Assert.Equal("status", key.SelectedText);

        host.Press(key, Key.R, ModifierKeys.Control | ModifierKeys.Shift);
        Assert.Null(workspace.BulkRenameSheet);
        Assert.NotNull(workspace.AddPropertySheet);
        Assert.Equal(ModalSurface.AddProperty, host.Shell.OpenModalSurface);
    });

    // ---- harnesses ------------------------------------------------------

    private sealed record Person(string Name, string Role);

    /// <summary>The F5 shape: a SlateTextEditor that last held focus, and
    /// a focus-scope sheet over it holding two prompt fields and a button
    /// (optionally a Tab-accepting notes field and a grid), cycling like
    /// every shell sheet.</summary>
    private sealed class Harness : IDisposable
    {
        private readonly Window _window;

        public Harness(bool fenced, bool withGrid = false, bool withNotes = false)
        {
            // Application's static constructor registers the pack:
            // scheme without constructing an Application (see
            // TextBoxAccessibilityTests for why that matters).
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(
                typeof(Application).TypeHandle);

            Editor = new SlateTextEditor();
            ResetNote();
            Topic = new TextBox { Name = "Topic" };
            Attendees = new TextBox { Name = "Attendees" };
            Done = new Button { Name = "Done", Content = "Done" };
            var fields = new StackPanel();
            fields.Children.Add(Topic);
            fields.Children.Add(Attendees);
            if (withNotes)
            {
                Notes = new TextBox { Name = "Notes", AcceptsTab = true, AcceptsReturn = true };
                fields.Children.Add(Notes);
            }

            if (withGrid)
            {
                Grid = new AccessibleDataGrid { Announce = _ => { } };
                Grid.Bind(
                    [
                        new AccessibleGridColumn { Header = "Name", Cell = row => ((Person)row).Name, IsRowHeader = true },
                        new AccessibleGridColumn { Header = "Role", Cell = row => ((Person)row).Role },
                    ],
                    People,
                    "2 rows.",
                    "People");
                fields.Children.Add(Grid);
            }

            fields.Children.Add(Done);
            Sheet = new Border { Child = fields, Background = System.Windows.Media.Brushes.White };
            FocusManager.SetIsFocusScope(Sheet, true);
            KeyboardNavigation.SetTabNavigation(Sheet, KeyboardNavigationMode.Cycle);
            KeyboardNavigation.SetControlTabNavigation(Sheet, KeyboardNavigationMode.Cycle);
            SheetKeyboardFence.SetIsEnabled(Sheet, fenced);

            var root = new System.Windows.Controls.Grid();
            root.Children.Add(Editor);
            root.Children.Add(Sheet);
            _window = OffscreenWindow(root);
            _window.Show();
            _window.UpdateLayout();
        }

        public SlateTextEditor Editor { get; }

        public Border Sheet { get; }

        public TextBox Topic { get; }

        public TextBox Attendees { get; }

        public Button Done { get; }

        public TextBox? Notes { get; }

        public AccessibleDataGrid? Grid { get; }

        public IReadOnlyList<Person> People { get; } =
            [new Person("Charlie", "Ops"), new Person("Alice", "Dev")];

        public void ResetNote()
        {
            Editor.Document = new TextDocument(Note);
            // The caret on the indented line, where TabBackward unindents.
            Editor.CaretOffset = 1;
        }

        /// <summary>The editor takes focus first — it is then the window
        /// scope's logical focus, the element the sheet's focus scope
        /// hands unanswered commands to — and a field in the sheet takes
        /// it next, as when a sheet opens from the editor.</summary>
        public void FocusAfterTheEditor(TextBox field)
        {
            Assert.True(Editor.FocusInputOwner(), "the editor did not take focus");
            Assert.True(field.Focus(), $"the sheet's {field.Name} field did not take focus");
            Assert.Same(Editor.TextArea, FocusManager.GetFocusedElement(_window));
            Assert.Same(field, Keyboard.FocusedElement);
        }

        /// <summary>Opens the grid's editor on one cell — the way F2 does —
        /// and waits for it to take the keyboard, which it does when it
        /// loads.</summary>
        public TextBox EditCell(Person row, int column)
        {
            Assert.True(Grid!.BeginEditAt(row, column), $"the grid refused to edit {row.Name}, column {column}");
            Assert.True(
                PumpedDispatcher.PumpUntil(() => Keyboard.FocusedElement is TextBox box && Grid.IsAncestorOf(box)),
                "the cell editor never took the keyboard");
            return (TextBox)Keyboard.FocusedElement;
        }

        /// <summary>One key press through the input system, delivered to
        /// the keyboard focus the way a physical press is. An IME-processed
        /// press is marked the way the keyboard device marks one —
        /// <c>Key</c> reads <c>ImeProcessed</c>, the real key is kept — and
        /// the device carries the mark onto the KeyDown it promotes.</summary>
        public void Press(Key key, ModifierKeys modifiers = ModifierKeys.None, bool imeProcessed = false) =>
            WithModifiers(modifiers, () =>
            {
                var press = new KeyEventArgs(
                    Keyboard.PrimaryDevice,
                    PresentationSource.FromVisual(_window)!,
                    Environment.TickCount,
                    key)
                {
                    RoutedEvent = Keyboard.PreviewKeyDownEvent,
                };
                if (imeProcessed)
                {
                    (typeof(KeyEventArgs).GetMethod("MarkImeProcessed", BindingFlags.NonPublic | BindingFlags.Instance)
                        ?? throw new InvalidOperationException("KeyEventArgs.MarkImeProcessed is gone; the IME fact needs another way in"))
                        .Invoke(press, null);
                    Assert.Equal(Key.ImeProcessed, press.Key);
                    Assert.Equal(key, press.ImeProcessedKey);
                }

                InputManager.Current.ProcessInput(press);
            });

        /// <summary>What a command source does with <paramref name="command"/>
        /// targeted at the focused <paramref name="field"/> (CommandHelpers'
        /// shape): ask, and execute only on yes. Returns the answer.</summary>
        public bool RunAsACommandSource(RoutedCommand command, TextBox field)
        {
            Assert.Same(field, Keyboard.FocusedElement);
            bool answered = command.CanExecute(null, field);
            if (answered)
            {
                command.Execute(null, field);
            }

            return answered;
        }

        public void Dispose() => _window.Close();
    }

    /// <summary>
    /// The shipped shell, unshown — MoveToFocusTests' shape: the real
    /// MainWindow (its XAML, lifecycle and handlers) over a scanned
    /// fixture vault's workspace, with no Application and no HWND, so
    /// nothing is persisted. An unshown window cannot hold keyboard
    /// focus, so its key presses are raised on their target element and
    /// promoted Preview → KeyDown the way the keyboard device promotes an
    /// unhandled preview.
    /// </summary>
    private sealed class ShellHost : IDisposable
    {
        private readonly FixtureVault _fixture = FixtureVault.Create(1, "sheet-fence-shell");
        private readonly Func<bool> _priorOverlayProbe = CanvasSurfaceView.ShellOverlayIsOpen;
        private readonly VaultSession _session;
        private readonly VaultLifecycleViewModel _lifecycle;
        private readonly Window _inputSource;

        public ShellHost()
        {
            Assert.Null(Application.Current);
            _session = VaultSession.OpenFilesystem(_fixture.Root);
            using (var cancel = new CancelToken())
            {
                _session.ScanInitial(cancel);
            }

            Workspace = new WorkspaceViewModel(
                _session,
                _fixture.Root,
                () => [],
                _ => { },
                startInteractionBackgroundWork: false,
                preferencesStore: new AppPreferencesStore(Path.Combine(_fixture.Root, "preferences.json")));
            Shell = new MainWindow();
            _lifecycle = Assert.IsType<VaultLifecycleViewModel>(Shell.DataContext);
            SetWorkspace(Workspace);
            // KeyEventArgs needs a live PresentationSource; the unshown
            // shell has none, so an offscreen stub lends its own.
            _inputSource = OffscreenWindow(new Border());
            _inputSource.Show();
        }

        public MainWindow Shell { get; }

        public WorkspaceViewModel Workspace { get; }

        public T Element<T>(string name)
            where T : class =>
            Assert.IsAssignableFrom<T>(Shell.FindName(name));

        public void Press(UIElement target, Key key, ModifierKeys modifiers) =>
            WithModifiers(modifiers, () =>
            {
                PresentationSource source = PresentationSource.FromVisual(_inputSource)!;
                var preview = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key)
                {
                    RoutedEvent = Keyboard.PreviewKeyDownEvent,
                };
                target.RaiseEvent(preview);
                if (!preview.Handled)
                {
                    target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key)
                    {
                        RoutedEvent = Keyboard.KeyDownEvent,
                    });
                }
            });

        public void Dispose()
        {
            try
            {
                SetWorkspace(null);
                Workspace.Dispose();
                _inputSource.Close();
                Shell.Close();
                _session.Dispose();
                _fixture.Dispose();
            }
            finally
            {
                CanvasSurfaceView.ShellOverlayIsOpen = _priorOverlayProbe;
            }
        }

        /// <summary>The lifecycle's real setter, so the shell's own
        /// observation wires the workspace's admissions.</summary>
        private void SetWorkspace(WorkspaceViewModel? workspace) =>
            (typeof(VaultLifecycleViewModel).GetProperty(
                    nameof(VaultLifecycleViewModel.Workspace), BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException("VaultLifecycleViewModel.Workspace is gone"))
            .SetValue(_lifecycle, workspace);
    }

    private static Window OffscreenWindow(UIElement content) => new()
    {
        Content = content,
        Width = 520,
        Height = 360,
        ShowInTaskbar = false,
        ShowActivated = false,
        WindowStyle = WindowStyle.None,
        Left = -10_000,
        Top = -10_000,
        WindowStartupLocation = WindowStartupLocation.Manual,
    };

    /// <summary>Holds exactly <paramref name="modifiers"/> down in the
    /// thread's key state — <c>GetKeyState</c>, what <c>Keyboard.Modifiers</c>
    /// reads — for the length of <paramref name="press"/>, then restores
    /// it. Every modifier is cleared first, and the state is re-applied
    /// until it reads back exactly: on a shared desktop the thread's key
    /// state can be resynchronized from input another process is
    /// injecting (measured: a cleared Shift read back down, a set Shift
    /// read back up), and a press made under the wrong modifiers would
    /// fail for a reason that is not the fence's.</summary>
    private static void WithModifiers(ModifierKeys modifiers, Action press)
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

    private const int VkShift = 0x10;
    private const int VkControl = 0x11;
    private const int VkLeftShift = 0xA0;
    private const int VkLeftControl = 0xA2;
    private const byte KeyDown = 0x80;

    /// <summary>Shift, Ctrl, Alt and Windows, generic and sided.</summary>
    private static readonly int[] AllModifierKeys =
        [0x10, 0x11, 0x12, 0xA0, 0xA1, 0xA2, 0xA3, 0xA4, 0xA5, 0x5B, 0x5C];

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetKeyboardState(byte[] keyState);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetKeyboardState(byte[] keyState);

    private static void RunSta(Action body)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                PumpedDispatcher.Run(body);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(60)), "STA test body timed out.");
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
