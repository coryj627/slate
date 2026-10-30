// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace SlateWindows;

/// <summary>
/// WB-22 (#1112): Ctrl+G and Ctrl+Shift+G from the find bar and from the
/// note's text. Two <c>KeyBinding</c>s in <c>MainWindow.xaml</c> bind these
/// routed commands; the window's command bindings act for the find bar of
/// the note that holds keyboard focus and set <c>ContinueRouting</c> when
/// there is none, so the key leaves the window unhandled (a plain
/// <c>ICommand</c> binding would swallow it even when it cannot act —
/// <c>AccessibleDataGrid.FilterCommand</c>'s precedent). A step moves that
/// surface's caret and selection to the match without moving focus.
/// </summary>
internal static class FindInNoteKeyRoute
{
    /// <summary>No input gestures: the chord table's rows and the two
    /// <c>KeyBinding</c>s are the only place the chords are authored.</summary>
    public static readonly RoutedUICommand FindNext =
        new("Find Next", nameof(FindNext), typeof(FindInNoteKeyRoute));

    public static readonly RoutedUICommand FindPrevious =
        new("Find Previous", nameof(FindPrevious), typeof(FindInNoteKeyRoute));

    /// <param name="modalOpen">True while a sheet, an overlay or a modal
    /// loop owns the shell: the steps pass the key on then.</param>
    internal static void Bind(UIElement window, Func<bool> modalOpen)
    {
        CanExecuteRoutedEventHandler canExecute = (sender, e) => CanExecute(e, modalOpen);
        window.CommandBindings.Add(new CommandBinding(FindNext, (_, e) => Execute(e, forward: true), canExecute));
        window.CommandBindings.Add(new CommandBinding(FindPrevious, (_, e) => Execute(e, forward: false), canExecute));
    }

    /// <summary>
    /// The open find bar of the note that holds <paramref name="focused"/>:
    /// walks up (a content element through its logical parent, as
    /// <c>MainWindow.FindAncestorDataContext</c> does) to the first find bar,
    /// editor or reading surface; anything else answers null.
    /// </summary>
    internal static FindInNoteViewModel? SessionOwningFocus(DependencyObject? focused)
    {
        for (DependencyObject? current = focused; current is not null; current = Parent(current))
        {
            switch (current)
            {
                case FindInNoteBar bar:
                    return bar.DataContext as FindInNoteViewModel;
                case SlateTextEditor editor:
                    return (editor.DataContext as WorkspaceTabViewModel)?.FindInNote;
                case Reading.ReadingSurface surface:
                    return (surface.DataContext as WorkspaceTabViewModel)?.FindInNote;
            }
        }
        return null;
    }

    private static void CanExecute(CanExecuteRoutedEventArgs e, Func<bool> modalOpen)
    {
        e.CanExecute = !modalOpen()
            && SessionOwningFocus(Keyboard.FocusedElement as DependencyObject) is { IsOpen: true };
        e.ContinueRouting = !e.CanExecute;
        e.Handled = true;
    }

    private static void Execute(ExecutedRoutedEventArgs e, bool forward)
    {
        if (SessionOwningFocus(Keyboard.FocusedElement as DependencyObject) is not { IsOpen: true } session)
        {
            return;
        }
        if (forward)
        {
            session.Next();
        }
        else
        {
            session.Previous();
        }
        e.Handled = true;
    }

    private static DependencyObject? Parent(DependencyObject current) => current switch
    {
        FrameworkElement element => element.Parent ?? element.TemplatedParent ?? VisualTreeHelper.GetParent(element),
        FrameworkContentElement content => content.Parent ?? LogicalTreeHelper.GetParent(content),
        Visual or System.Windows.Media.Media3D.Visual3D => VisualTreeHelper.GetParent(current),
        _ => LogicalTreeHelper.GetParent(current),
    };
}
