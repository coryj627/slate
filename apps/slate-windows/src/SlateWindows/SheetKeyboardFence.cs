// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;

namespace SlateWindows;

/// <summary>
/// W7-7 (#1248, contract R-6): a sheet fences Tab. Tab and Shift+Tab
/// traverse inside the sheet, and the <see cref="EditingCommands.TabForward"/>
/// / <see cref="EditingCommands.TabBackward"/> they raise never leave it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The leak.</b> A <c>TextBox</c> binds Tab to <c>TabForward</c>. With
/// <c>AcceptsTab</c> false its CanExecute declines and sets
/// <c>ContinueRouting</c>, so the query bubbles out of the box. Every sheet
/// overlay is a focus scope, and <c>CommandManager</c> hands an unanswered
/// command that reaches a focus scope to the PARENT scope's logically
/// focused element — the note editor's AvalonEdit <c>TextArea</c> behind
/// the sheet, whose <c>TabForward</c> binding has no CanExecute and inserts
/// a tab. The NVDA pass heard nothing move and found three tab characters
/// in the note behind the template prompt sheet (record F5). Shift+Tab
/// takes the same route to <c>TabBackward</c>, which unindents the editor's
/// caret line. A sheet opened from a control that answers neither command
/// (the Properties header button) leaves Tab to WPF's own navigation, which
/// is why Add property and Bulk rename passed the run.
/// </para>
/// <para>
/// <b>The fence, two halves.</b> (a) <c>PreviewKeyDown</c> takes Tab and
/// Shift+Tab (no other modifier) from a text field in the sheet and moves
/// focus itself, inside the sheet's <c>Cycle</c> scope, before the key can
/// become a command. (b) A <c>PreviewCanExecute</c> handler refuses both
/// commands while keyboard focus is inside the sheet —
/// <c>CanExecute=false, ContinueRouting=false, Handled=true</c> — so a Tab
/// that reaches the command layer some other way (an IME-processed key, a
/// programmatic query) still cannot cross to the editor; a
/// <c>PreviewExecuted</c> handler closes the programmatic Execute path
/// the same way, since <c>ExecuteCore</c> never asks CanExecute first.
/// </para>
/// <para>
/// <b>Why (a) is limited to text fields.</b> Only the text-editor hosts
/// (<see cref="TextBoxBase"/>, <see cref="PasswordBox"/>) turn Tab into a
/// command, so they are the only stops where (b) removes WPF's traversal
/// and (a) must supply it. Every other stop keeps WPF's own Tab handling,
/// which cannot leak. Taking Tab from those too broke the bulk-rename
/// preview grid: <c>DataGrid.OnTabKeyDown</c> moves the cell selection
/// with focus, and a preempted Tab left the selection on the first cell
/// while focus walked on (measured, 2026-09-22).
/// </para>
/// <para>
/// <b>Nothing else is intercepted.</b> The sheet's own editing chords reach
/// its fields, and suppressing shell chords while a sheet is open stays
/// the shipped modal routing's job (<see cref="ModalSurfaces"/>, contract
/// 30 TR-7); <c>MainWindow.Window_PreviewKeyDown</c> runs before this
/// fence on every key because the window heads the tunnel.
/// <c>SheetFenceCensus</c> pins that every focus-scope overlay in the
/// shell's XAML carries this fence and cycles.
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
            sheet.PreviewKeyDown += OnPreviewKeyDown;
            CommandManager.AddPreviewCanExecuteHandler(sheet, OnPreviewCanExecute);
            CommandManager.AddPreviewExecutedHandler(sheet, OnPreviewExecuted);
        }
        else
        {
            sheet.PreviewKeyDown -= OnPreviewKeyDown;
            CommandManager.RemovePreviewCanExecuteHandler(sheet, OnPreviewCanExecute);
            CommandManager.RemovePreviewExecutedHandler(sheet, OnPreviewExecuted);
        }
    }

    /// <summary>(a) The traversal WPF would have performed had nothing
    /// behind the sheet answered the command. The event only tunnels
    /// through this sheet when the key's target is inside it.</summary>
    private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Tab
            || (e.KeyboardDevice.Modifiers & ~ModifierKeys.Shift) != ModifierKeys.None
            || Keyboard.FocusedElement is not Control field
            || field is not (TextBoxBase or PasswordBox))
        {
            return;
        }

        _ = field.MoveFocus(new TraversalRequest(
            e.KeyboardDevice.Modifiers == ModifierKeys.Shift
                ? FocusNavigationDirection.Previous
                : FocusNavigationDirection.Next));
        e.Handled = true;
    }

    /// <summary>(b) Neither Tab command is answered anywhere while the
    /// keyboard is in this sheet, so neither is ever handed on to the
    /// element behind it.</summary>
    private static void OnPreviewCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        if (IsTabCommand(e.Command) && sender is UIElement { IsKeyboardFocusWithin: true })
        {
            e.CanExecute = false;
            e.ContinueRouting = false;
            e.Handled = true;
        }
    }

    private static void OnPreviewExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (IsTabCommand(e.Command) && sender is UIElement { IsKeyboardFocusWithin: true })
        {
            e.Handled = true;
        }
    }

    private static bool IsTabCommand(ICommand command) =>
        ReferenceEquals(command, EditingCommands.TabForward)
        || ReferenceEquals(command, EditingCommands.TabBackward);
}
