// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;

namespace SlateWindows;

/// <summary>
/// W7-7 (#1248, contract R-6): a sheet fences Tab. The
/// <see cref="EditingCommands.TabForward"/> and
/// <see cref="EditingCommands.TabBackward"/> a key or a command source
/// raises inside a sheet are answered inside it or not at all — never
/// handed to the element behind it — so Tab and Shift+Tab traverse the
/// sheet's own <c>Cycle</c> scope.
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
/// <see cref="CommandBinding"/> per Tab command sits on the sheet. The
/// class handler consults the sheet's own bindings BEFORE it hands a
/// command on, and a query reaches the sheet only when nothing inside
/// answered it — so the binding refuses exactly what would have left:
/// <c>CanExecute=false, Handled=true</c>. It stops the COMMAND, not the
/// key: <c>ContinueRouting=true</c> leaves an unanswered Tab to WPF's own
/// handling — KeyboardNavigation's traversal in the sheet's <c>Cycle</c>
/// scope, a grid's <c>OnTabKeyDown</c>, the IME — none of which reaches
/// the element behind the sheet. A control that owns Tab keeps it: an
/// <c>AcceptsTab</c> field answers the command itself and never reaches
/// the edge, and an editing grid cell handles its own key first. An
/// IME-processed Tab stays the IME's: <c>Key.ImeProcessed</c> means the IME
/// claimed the key, TSF delivers it to the IME only while the preview is
/// unhandled (TextServicesManager.PostProcessInput), and WPF's own
/// navigation never moves on it — so the fence takes no key at all.
/// </para>
/// <para>
/// <b>An Execute that never asked.</b> <c>RoutedCommand.Execute</c> raises
/// PreviewExecuted and Executed without a CanExecute query, and an
/// unhandled Executed that reaches the sheet is handed on the same way
/// (the binding cannot stop it: <c>CommandBinding</c> runs an Executed
/// handler only after its own CanExecute answers yes). The PreviewExecuted
/// guard asks CanExecute on the command's target: something inside that
/// answers keeps the execution; otherwise the edge's refusal answers and
/// the execution is swallowed.
/// </para>
/// <para>
/// <b>Nothing else is intercepted.</b> The sheet's own editing chords reach
/// its fields, and suppressing shell chords while a sheet is open stays
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

    /// <summary>The edge: one refusal per Tab command. Shared — a binding
    /// holds no reference to the element whose collection holds it.</summary>
    private static readonly CommandBinding[] Edge =
    [
        Refusal(EditingCommands.TabForward),
        Refusal(EditingCommands.TabBackward),
    ];

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

    /// <summary>A Tab command executed inside the sheet runs only if
    /// something inside the sheet answers it.</summary>
    private static void OnPreviewExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (IsTabCommand(e.Command)
            && e.Command is RoutedCommand command
            && e.OriginalSource is IInputElement target
            && !command.CanExecute(e.Parameter, target))
        {
            e.Handled = true;
        }
    }

    private static bool IsTabCommand(ICommand command) =>
        ReferenceEquals(command, EditingCommands.TabForward)
        || ReferenceEquals(command, EditingCommands.TabBackward);
}
