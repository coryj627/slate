// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// W7-7 (#1248, contract R-6): a sheet fences Tab. The NVDA pass found
// Tab in the template prompt sheet typed tab characters into the note
// behind it (record F5): the prompt TextBox declines TabForward, and the
// overlay — a focus scope — hands the unanswered command to the parent
// scope's focused element, the editor's TextArea. The first five facts
// drive REAL keystrokes through the input system (InputManager, so the
// PreviewKeyDown → KeyDown → command → KeyboardNavigation pipeline runs as
// it does for a key press) into a window that reproduces that shape: a
// SlateTextEditor that last held focus, and a focus-scope sheet over it.
// The last runs the shipped MainWindow, to show the fence and the modal
// routing that owns shell chords under a sheet (contract 30 TR-7) leave
// each other alone.

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
    /// sheet, and neither key changes the note behind it.
    /// </summary>
    [Fact]
    public void TabInsideASheetNeverReachesTheEditor() => RunSta(() =>
    {
        using var harness = new Harness(fenced: true);
        harness.FocusTopicAfterTheEditor();

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
    /// caret line. This is what makes the fact above evidence rather than
    /// a harness that could not have leaked in the first place.
    /// </summary>
    [Fact]
    public void WithoutTheFenceTabReachesTheEditorBehindTheSheet() => RunSta(() =>
    {
        using var harness = new Harness(fenced: false);
        harness.FocusTopicAfterTheEditor();

        harness.Press(Key.Tab);
        Assert.Same(harness.Topic, Keyboard.FocusedElement);
        Assert.Equal(Note.Insert(1, "\t"), harness.Editor.Text);

        harness.ResetNote();
        harness.FocusTopicAfterTheEditor();
        harness.Press(Key.Tab, ModifierKeys.Shift);
        Assert.Same(harness.Topic, Keyboard.FocusedElement);
        Assert.Equal("Indented first line\nSecond line\n", harness.Editor.Text);
    });

    /// <summary>
    /// The command half of the fence: while the keyboard is in the
    /// sheet, neither Tab command is answered, and executing one — the
    /// one path that never asks CanExecute first — changes nothing
    /// behind the sheet.
    /// </summary>
    [Fact]
    public void TheTabCommandsNeverLeaveAFencedSheet() => RunSta(() =>
    {
        using var harness = new Harness(fenced: true);
        harness.FocusTopicAfterTheEditor();

        Assert.False(EditingCommands.TabForward.CanExecute(null, harness.Topic));
        Assert.False(EditingCommands.TabBackward.CanExecute(null, harness.Topic));

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
    /// The fence takes Tab only from text fields, the one kind of stop
    /// that turns Tab into a command. A grid in a sheet keeps WPF's own
    /// Tab handling: DataGrid.OnTabKeyDown moves the cell selection with
    /// focus, and a fence that preempted it left the selection behind on
    /// the first cell (measured before the fence was narrowed).
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
        harness.FocusTopicAfterTheEditor();
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
    /// or admits the shell chord beneath one. An unshown shell cannot
    /// hold keyboard focus, so the fence's focus guard leaves it inert
    /// here; the fence's own side — that it intercepts nothing but the
    /// two Tab commands — is <see cref="TheSheetsOwnEditingChordsStillReachItsField"/>.
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
    /// (optionally a grid), cycling like every shell sheet.</summary>
    private sealed class Harness : IDisposable
    {
        private readonly Window _window;

        public Harness(bool fenced, bool withGrid = false)
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
            if (withGrid)
            {
                Grid = new AccessibleDataGrid { Announce = _ => { } };
                Grid.Bind(
                    [
                        new AccessibleGridColumn { Header = "Name", Cell = row => ((Person)row).Name, IsRowHeader = true },
                        new AccessibleGridColumn { Header = "Role", Cell = row => ((Person)row).Role },
                    ],
                    [new Person("Charlie", "Ops"), new Person("Alice", "Dev")],
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

        public AccessibleDataGrid? Grid { get; }

        public void ResetNote()
        {
            Editor.Document = new TextDocument(Note);
            // The caret on the indented line, where TabBackward unindents.
            Editor.CaretOffset = 1;
        }

        /// <summary>The editor takes focus first — it is then the window
        /// scope's logical focus, the element the sheet's focus scope
        /// hands unanswered commands to — and the sheet's first field
        /// takes it next, as when a sheet opens from the editor.</summary>
        public void FocusTopicAfterTheEditor()
        {
            Assert.True(Editor.FocusInputOwner(), "the editor did not take focus");
            Assert.True(Topic.Focus(), "the sheet's first field did not take focus");
            Assert.Same(Editor.TextArea, FocusManager.GetFocusedElement(_window));
            Assert.Same(Topic, Keyboard.FocusedElement);
        }

        /// <summary>One key press through the input system, delivered to
        /// the keyboard focus the way a physical press is.</summary>
        public void Press(Key key, ModifierKeys modifiers = ModifierKeys.None) =>
            WithModifiers(modifiers, () => InputManager.Current.ProcessInput(
                new KeyEventArgs(
                    Keyboard.PrimaryDevice,
                    PresentationSource.FromVisual(_window)!,
                    Environment.TickCount,
                    key)
                {
                    RoutedEvent = Keyboard.PreviewKeyDownEvent,
                }));

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
    /// thread's key state — what <c>Keyboard.Modifiers</c> reads — for
    /// the length of <paramref name="press"/>, then restores it. Every
    /// modifier is cleared first: a thread's key state can start with a
    /// modifier another process was holding when it was created, and on a
    /// shared desktop something usually is.</summary>
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

        Assert.True(SetKeyboardState(pressed));
        try
        {
            Assert.Equal(modifiers, Keyboard.Modifiers);
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
