// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SlateWindows;

/// <summary>
/// #1275 (codex round 6, owner decision): the palette's seal holds over the
/// whole shell, not only over the palette. While the palette is sealed — a
/// command it ran is still running, or a modal loop is over the shell — only
/// Escape acts, and only on an open palette (contract 28 I3, T9); with the
/// palette closed (Escape dismissed it under a running command) nothing in
/// the shell acts at all.
/// </summary>
/// <remarks>
/// <para>
/// <b>One check, at the shell's edge.</b> <see cref="SealTakes"/> is the
/// only place the shell reads the seal. The window is the root of every
/// input route in the shell's tree, so its preview handlers run before any
/// element's: <c>Window_PreviewKeyDown</c> calls the check before its first
/// route, and the window's other preview input handlers are nothing else.
/// </para>
/// <para>
/// <b>What a taken input cannot reach.</b> A key the check marks handled
/// runs no later handler (none in the shell listens for handled keys but
/// the editor's IME composition trackers, which act on nothing), no
/// <c>InputBinding</c> — the window's own XAML bindings included, because
/// <c>CommandManager</c> translates only an unhandled <c>KeyDown</c> — and
/// no menu mode (WPF enters it on an Alt or F10 whose press went
/// unhandled). WPF's message loop translates only a key it left unhandled,
/// so a taken key types no character and raises no mnemonic. The key's
/// release is taken as well: the Apps key opens a context menu on its
/// release, and a context menu's own keys and clicks stay inside its popup,
/// out of every route here. A text input event is taken too, and so are
/// pointer presses, releases and wheel turns, so no button, menu item or
/// context menu is clicked and nothing pans or zooms. A mnemonic that
/// reaches the access key manager anyway finds no target in the shell.
/// (Text a TSF text service writes straight into a focused field — voice
/// typing, handwriting — goes through WPF's text store, not these routes:
/// contract 40 AR-48.)
/// </para>
/// <para>
/// <b>The double-click WPF rebuilds from a taken press</b> (codex round 7).
/// <c>Control</c> listens to left and right presses even once they are
/// handled, and on a second click raises a FRESH, unhandled
/// <c>PreviewMouseDoubleClick</c> or <c>MouseDoubleClick</c> directly on the
/// control — the saved-queries list runs a query on it. One class handler
/// per event, on <c>Control</c> and past handled, applies the owning
/// shell's admission to that event before any instance handler sees it.
/// </para>
/// <para>
/// <b>Why input, not execution.</b> Every shell command is a view model
/// <c>ICommand</c> run directly by its source, so no command-level routed
/// event sees it; and the command the palette is running executes while
/// sealed too, so a gate on execution would refuse the palette's own work.
/// Every user route to a command starts at an input this admission takes.
/// Closing the window is not gated: a command may close the shell while it
/// runs, and the palette already models that teardown (T8 step 6, T11).
/// </para>
/// <para>
/// <c>ShellSealAdmissionCensus</c> pins the shape: the check is the first
/// statement of every window-level input handler, the window declares no
/// other, the seal is read nowhere else in the shell, and nothing in the
/// shell listens past a handled input.
/// </para>
/// </remarks>
public partial class MainWindow
{
    /// <summary>The double-click gate, once per process: class handlers run
    /// before instance handlers, and past handled they see the fresh event
    /// <c>Control</c> raises from a press the admission already took.</summary>
    static MainWindow()
    {
        EventManager.RegisterClassHandler(
            typeof(Control),
            Control.PreviewMouseDoubleClickEvent,
            new MouseButtonEventHandler(OnDoubleClickUnderTheSeal),
            handledEventsToo: true);
        EventManager.RegisterClassHandler(
            typeof(Control),
            Control.MouseDoubleClickEvent,
            new MouseButtonEventHandler(OnDoubleClickUnderTheSeal),
            handledEventsToo: true);
    }

    /// <summary>
    /// The seal's one admission check. Returns whether the seal took the
    /// input — marked handled, so nothing after this handler acts on it —
    /// after letting an unmodified Escape dismiss an open palette (T9).
    /// Unsealed, it takes nothing.
    /// </summary>
    private bool SealTakes(RoutedEventArgs input)
    {
        if (!_viewModel.Palette.IsSealed)
        {
            return false;
        }

        // T9: Escape dismisses an open palette, sealed or not — the one key
        // that acts. With the palette closed it has nothing to dismiss.
        if (input is KeyEventArgs { Key: Key.Escape } escape
            && input.RoutedEvent == Keyboard.PreviewKeyDownEvent
            && Keyboard.Modifiers == ModifierKeys.None)
        {
            HandleCommandPaletteKey(escape, ModifierKeys.None);
        }

        input.Handled = true;
        return true;
    }

    private void Window_PreviewKeyUp(object sender, KeyEventArgs e) => _ = SealTakes(e);

    private void Window_PreviewTextInput(object sender, TextCompositionEventArgs e) => _ = SealTakes(e);

    private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e) => _ = SealTakes(e);

    private void Window_PreviewMouseUp(object sender, MouseButtonEventArgs e) => _ = SealTakes(e);

    private void Window_PreviewMouseWheel(object sender, MouseWheelEventArgs e) => _ = SealTakes(e);

    /// <summary>A double-click on a control in a shell goes through that
    /// shell's admission; one anywhere else is not the shell's.</summary>
    private static void OnDoubleClickUnderTheSeal(object sender, MouseButtonEventArgs e)
    {
        if (sender is DependencyObject element && GetWindow(element) is MainWindow shell)
        {
            _ = shell.SealTakes(e);
        }
    }

    /// <summary>The access key manager raises this on each candidate
    /// element to learn its target; under the seal, no element in the
    /// shell is one. Registered past handled so a menu item's own answer
    /// cannot hide the candidate from it.</summary>
    private void Window_AccessKeyPressed(object sender, AccessKeyPressedEventArgs e)
    {
        if (SealTakes(e))
        {
            e.Target = null;
        }
    }
}
