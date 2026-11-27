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
/// <b>Touch, pen and a drop</b> (plan 47 CB-38, #1695). A touch's own
/// events are taken, so no list starts a pan under the seal: a list whose
/// <c>PanningMode</c> is not None starts a manipulation from
/// <c>TouchDown</c>, and a drag it pans promotes no press, only
/// <c>PreviewMouseMove</c>, which the seal does not take (step 0's probe
/// logs). A tap's promoted press is taken as a press. A drop from another
/// app is taken under either cause, since a window a modal prompt disabled
/// still receives an OLE drop, and the source is told nothing moved, so OLE
/// sends DragLeave and no Drop and a source that deletes on Move keeps its
/// text. Pen input is promoted to the pointer routes and starts no
/// manipulation, so a pen tap or drag needs no take of its own. A pen
/// flick, which WPF turns into a page scroll or an application command, is
/// not taken: with an application command and window activation it is one
/// of the seal's three known exclusions (contract 28's route table, #2086).
/// The seal takes no stylus preview, so <c>ShellSealAdmissionCensus</c>
/// refuses any stylus handler in the shell. UI Automation pattern and range
/// calls and UI Automation's <c>SetFocus</c> carry no input and stay outside
/// the seal by the owner's decision (contract 28 PR-6, CD26).
/// </para>
/// <para>
/// <b>Why input, not execution.</b> Every shell command is a view model
/// <c>ICommand</c> run directly by its source, so no command-level routed
/// event sees it; and the command the palette is running executes while
/// sealed too, so a gate on execution would refuse the palette's own work.
/// Every user route to a command starts at an input this admission takes,
/// but one: an application command (a keyboard's Undo or Paste key, a pen
/// flick), which WPF raises on the focused element outside the window's
/// previews and <c>CommandManager</c> runs. It is a known exclusion, with a
/// pen flick and window activation (contract 28's route table, #2086).
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
    /// after letting an unmodified Escape dismiss an open palette (T9). A
    /// taken drag tells its source that nothing moved. Unsealed, it takes
    /// nothing. <paramref name="inTheShellsWindow"/> is false for a window
    /// the shell owns (<see cref="ShellSeal"/>): an Escape there is not on
    /// the palette, so it is taken like any key.
    /// </summary>
    internal bool SealTakes(RoutedEventArgs input, bool inTheShellsWindow = true)
    {
        if (!_viewModel.Palette.IsSealed)
        {
            return false;
        }

        // T9: Escape dismisses an open palette, sealed or not — the one key
        // that acts. With the palette closed it has nothing to dismiss.
        if (inTheShellsWindow
            && input is KeyEventArgs { Key: Key.Escape } escape
            && input.RoutedEvent == Keyboard.PreviewKeyDownEvent
            && Keyboard.Modifiers == ModifierKeys.None)
        {
            HandleCommandPaletteKey(escape, ModifierKeys.None);
        }

        // CB-38: OLE tells the source the effect a drag ends with; None
        // ends the drag with DragLeave and no Drop, and DoDragDrop returns
        // None, so a source that deletes on Move keeps its text.
        if (input is DragEventArgs drag)
        {
            drag.Effects = DragDropEffects.None;
        }

        input.Handled = true;
        return true;
    }

    private void Window_PreviewKeyUp(object sender, KeyEventArgs e) => _ = SealTakes(e);

    private void Window_PreviewTextInput(object sender, TextCompositionEventArgs e) => _ = SealTakes(e);

    private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e) => _ = SealTakes(e);

    private void Window_PreviewMouseUp(object sender, MouseButtonEventArgs e) => _ = SealTakes(e);

    private void Window_PreviewMouseWheel(object sender, MouseWheelEventArgs e) => _ = SealTakes(e);

    private void Window_PreviewTouchDown(object sender, TouchEventArgs e) => _ = SealTakes(e);

    private void Window_PreviewTouchMove(object sender, TouchEventArgs e) => _ = SealTakes(e);

    private void Window_PreviewTouchUp(object sender, TouchEventArgs e) => _ = SealTakes(e);

    private void Window_PreviewDragEnter(object sender, DragEventArgs e) => _ = SealTakes(e);

    private void Window_PreviewDragOver(object sender, DragEventArgs e) => _ = SealTakes(e);

    private void Window_PreviewDrop(object sender, DragEventArgs e) => _ = SealTakes(e);

    /// <summary>A double-click on a control in a shell — or in a modeless
    /// window it admitted (<see cref="ShellSeal"/>) — goes through that
    /// shell's admission; one anywhere else is not the shell's.</summary>
    private static void OnDoubleClickUnderTheSeal(object sender, MouseButtonEventArgs e)
    {
        if (sender is DependencyObject element && ShellSeal.ShellOf(element) is MainWindow shell)
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
