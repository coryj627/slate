// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace SlateWindows;

/// <summary>
/// WB-15 (#1307 item 3, WD9): the seal's one admission for a window the
/// shell owns and shows modeless — today the reading table grid. Its key,
/// text, pointer and wheel routes tunnel from its own root, not
/// <c>MainWindow</c>'s, so none of the shell's window-level handlers reach
/// them; <see cref="Admit"/> puts the same check on each of them that the
/// shell's own window puts on its routes (<c>MainWindow.SealTakes</c>,
/// contract 28 I3). A modal dialog the shell owns is the prompt a
/// modal-loop seal waits on and is never admitted: <see cref="ShellOf"/>
/// resolves only the shell's own window and admitted windows, so no route
/// from an unadmitted window reaches the seal.
/// </summary>
internal static class ShellSeal
{
    /// <summary>The windows <see cref="Admit"/> admitted; weak, so a closed
    /// table window is collected.</summary>
    private static readonly ConditionalWeakTable<Window, object> AdmittedWindows = new();

    /// <summary>The shell whose seal governs <paramref name="element"/>:
    /// the shell's own window, or one <c>Owner</c> hop from a window
    /// <see cref="Admit"/> admitted — never an <c>Owner</c> walk from any
    /// other window.</summary>
    internal static MainWindow? ShellOf(DependencyObject element) => Window.GetWindow(element) switch
    {
        MainWindow shell => shell,
        Window window when AdmittedWindows.TryGetValue(window, out _) => window.Owner as MainWindow,
        _ => null,
    };

    /// <summary>The owning shell's admission for an input in
    /// <paramref name="owned"/>; <c>RoutedEventArgs</c>, because a
    /// mnemonic's <c>AccessKeyPressedEventArgs</c> is no input event. Not
    /// in the shell's window, so an Escape here is taken, never handed to
    /// the palette (T9 dismisses an open palette from the palette's own
    /// window only).</summary>
    internal static bool TakesFor(Window owned, RoutedEventArgs input) =>
        ShellOf(owned)?.SealTakes(input, inTheShellsWindow: false) ?? false;

    /// <summary>Admits a modeless window the shell owns: the shell's six
    /// preview routes and its access-key gate, each through the shell's
    /// one check. Called right after the window is built, before any other
    /// subscription on it, so the check runs first on every route.</summary>
    internal static void Admit(Window owned)
    {
        AdmittedWindows.AddOrUpdate(owned, owned);
        owned.PreviewKeyDown += Take;
        owned.PreviewKeyUp += Take;
        owned.PreviewTextInput += Take;
        owned.PreviewMouseDown += Take;
        owned.PreviewMouseUp += Take;
        owned.PreviewMouseWheel += Take;
        owned.AddHandler(AccessKeyManager.AccessKeyPressedEvent, new AccessKeyPressedEventHandler(TakeAccessKey), true);
    }

    private static void Take(object sender, RoutedEventArgs e) => _ = TakesFor((Window)sender, e);

    /// <summary>The access key manager asks each candidate for its target;
    /// under the seal no element in an admitted window is one.</summary>
    private static void TakeAccessKey(object sender, AccessKeyPressedEventArgs e)
    {
        if (TakesFor((Window)sender, e))
        {
            e.Target = null;
        }
    }
}
