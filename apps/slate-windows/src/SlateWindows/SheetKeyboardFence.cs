// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Reflection;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;

namespace SlateWindows;

/// <summary>
/// W7-7 (#1248, contract R-6): a sheet fences Tab — and, since WB-5
/// (#1259), every editing command its fields cannot answer. The
/// <see cref="EditingCommands.TabForward"/> and
/// <see cref="EditingCommands.TabBackward"/> a key or a command source
/// raises inside a sheet are answered inside it or not at all — never
/// handed to the element behind it — so Tab and Shift+Tab traverse the
/// sheet's own <c>Cycle</c> scope. Every other command in
/// <see cref="FencedCommands"/> (Undo, Redo, Cut, Paste, Delete, Select
/// All, caret navigation, the rest of <see cref="EditingCommands"/>) that
/// no field in the sheet answers is refused the same way, so it never
/// edits or moves the note behind it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The leak.</b> A <c>TextBox</c> binds Tab to <c>TabForward</c>. With
/// <c>AcceptsTab</c> false its CanExecute declines and sets
/// <c>ContinueRouting</c>, so the query bubbles out of the box. Every sheet
/// overlay is a focus scope, and <c>CommandManager</c>'s class handler
/// hands a command that reaches a focus scope unanswered to the PARENT
/// scope's logically focused element — the note editor's AvalonEdit
/// <c>TextArea</c> behind the sheet, whose <c>TabForward</c> binding has no
/// CanExecute and inserts a tab. The NVDA pass heard nothing move and found
/// three tab characters in the note behind the template prompt sheet
/// (record F5). Shift+Tab takes the same route to <c>TabBackward</c>, which
/// unindents the editor's caret line. A sheet opened from a control that
/// answers neither command (the Properties header button) never leaked,
/// which is why Add property and Bulk rename passed the run.
/// </para>
/// <para>
/// <b>The fence is the sheet's edge, not a key hook.</b> One
/// <see cref="CommandBinding"/> per fenced command sits on the sheet. The
/// class handler consults the sheet's own bindings BEFORE it hands a
/// command on, and a query reaches the sheet only when nothing inside
/// answered it — so the binding refuses exactly what would have left:
/// <c>CanExecute=false, Handled=true</c>. It stops the COMMAND, not the
/// key: <c>ContinueRouting=true</c> leaves an unanswered key to WPF's own
/// handling — KeyboardNavigation's traversal in the sheet's <c>Cycle</c>
/// scope, a grid's <c>OnTabKeyDown</c>, the IME — none of which reaches
/// the element behind the sheet. So an arrow key a caret-hidden read-only
/// field declines (the canvas card inspection sheet's) moves focus in the
/// sheet's <c>Contained</c> directional scope instead of being handed to
/// the element behind it. A control that owns Tab keeps it: an
/// <c>AcceptsTab</c> field answers the command itself and never reaches
/// the edge, and an editing grid cell handles its own key first. An
/// IME-processed Tab stays the IME's: <c>Key.ImeProcessed</c> means the IME
/// claimed the key, TSF delivers it to the IME only while the preview is
/// unhandled (TextServicesManager.PostProcessInput), and WPF's own
/// navigation never moves on it — so the fence takes no key at all.
/// </para>
/// <para>
/// <b>The edge's own gestures.</b> A binding's command carries its key
/// gestures, so the sheet itself maps every fenced command's chords —
/// Ctrl+Z, Ctrl+Y, Ctrl+X, Ctrl+V, Ctrl+A, Delete, Shift+Delete,
/// Shift+Insert, and <see cref="EditingCommands"/>' own (the arrows,
/// Ctrl+E, Ctrl+B and the other formatting chords). A press that reaches
/// the sheet unanswered meets the same refusal and routes on
/// (<c>ContinueRouting</c>), so the window's key bindings and the modal
/// routing still see it as before.
/// </para>
/// <para>
/// <b>An Execute that never asked.</b> <c>RoutedCommand.Execute</c> raises
/// PreviewExecuted and Executed without a CanExecute query, and an
/// unhandled Executed that reaches the sheet is handed on the same way
/// (the binding cannot stop it: <c>CommandBinding</c> runs an Executed
/// handler only after its own CanExecute answers yes). The PreviewExecuted
/// guard asks CanExecute on the command's target: something inside that
/// answers keeps the execution; otherwise the edge's refusal answers and
/// the execution is swallowed. For Paste that ask reads the clipboard, as
/// the field's own binding does again before it pastes (a
/// <c>CommandBinding</c> re-asks its CanExecute ahead of every Executed),
/// so the guard adds no failure a busy clipboard did not already have.
/// </para>
/// <para>
/// <b>Nothing a field answers is intercepted.</b> The sheet's own editing
/// chords reach its fields — a field with undo history undoes inside, and a
/// read-only field's Copy (not fenced) still copies — and suppressing shell
/// chords while a sheet is open stays
/// the shipped modal routing's job (<see cref="ModalSurfaces"/>, contract
/// 30 TR-7). <c>SheetFenceCensus</c> pins that every focus-scope overlay
/// in the shell's XAML carries this fence and cycles.
/// </para>
/// </remarks>
internal static class SheetKeyboardFence
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(SheetKeyboardFence),
            new PropertyMetadata(false, OnIsEnabledChanged));

    /// <summary>
    /// WB-5 (#1259): every editing command a sheet's field may decline —
    /// <c>Undo</c>, <c>Redo</c>, <c>Cut</c>, <c>Paste</c>, <c>Delete</c> and
    /// <c>SelectAll</c>, and every static command on
    /// <see cref="EditingCommands"/> (the two Tab commands among them),
    /// enumerated once by reflection so a new member is covered without an
    /// edit. <c>Copy</c> is left out: it changes nothing behind the sheet,
    /// and a read-only field's Copy answers inside it anyway.
    /// </summary>
    internal static readonly ICommand[] FencedCommands = Fenced();

    /// <summary>The edge: one refusal per fenced command. Shared — a binding
    /// holds no reference to the element whose collection holds it.</summary>
    private static readonly CommandBinding[] Edge = [.. FencedCommands.Select(Refusal)];

    private static readonly HashSet<ICommand> FencedSet = [.. FencedCommands];

    private static ICommand[] Fenced()
    {
        var commands = new List<ICommand>
        {
            ApplicationCommands.Undo,
            ApplicationCommands.Redo,
            ApplicationCommands.Cut,
            ApplicationCommands.Paste,
            ApplicationCommands.Delete,
            ApplicationCommands.SelectAll,
        };
        foreach (PropertyInfo property in typeof(EditingCommands).GetProperties(
            BindingFlags.Public | BindingFlags.Static))
        {
            if (property.GetValue(null) is RoutedUICommand command && !commands.Contains(command))
            {
                commands.Add(command);
            }
        }

        return [.. commands];
    }

    public static bool GetIsEnabled(DependencyObject element) =>
        (bool)element.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject element, bool value) =>
        element.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject element, DependencyPropertyChangedEventArgs change)
    {
        if (element is not UIElement sheet)
        {
            return;
        }

        if ((bool)change.NewValue)
        {
            sheet.CommandBindings.AddRange(Edge);
            CommandManager.AddPreviewExecutedHandler(sheet, OnPreviewExecuted);
        }
        else
        {
            foreach (CommandBinding binding in Edge)
            {
                sheet.CommandBindings.Remove(binding);
            }

            CommandManager.RemovePreviewExecutedHandler(sheet, OnPreviewExecuted);
        }
    }

    private static CommandBinding Refusal(ICommand command)
    {
        var binding = new CommandBinding(command);
        binding.CanExecute += OnUnansweredCanExecute;
        return binding;
    }

    /// <summary>The query reached the sheet unanswered: refuse it here,
    /// where the class handler would otherwise hand it to the element
    /// behind the sheet, and let the key it came from route on.</summary>
    private static void OnUnansweredCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = false;
        e.ContinueRouting = true;
        e.Handled = true;
    }

    /// <summary>A fenced command executed inside the sheet runs only if
    /// something inside the sheet answers it.</summary>
    private static void OnPreviewExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (IsFencedCommand(e.Command)
            && e.Command is RoutedCommand command
            && e.OriginalSource is IInputElement target
            && !command.CanExecute(e.Parameter, target))
        {
            e.Handled = true;
        }
    }

    private static bool IsFencedCommand(ICommand command) => FencedSet.Contains(command);
}
