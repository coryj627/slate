// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Controls.Primitives;

namespace SlateWindows;

/// <summary>#1793: a button in a pane that runs a workspace command for the active pane's
/// note makes its own pane active first, as focus entering it would. A pointer click or Space
/// moves focus there anyway, but a screen reader's Invoke doesn't, so without this the
/// inactive pane's button acted on the other pane's note. A button raises Click before it
/// runs its command, so the command sees the pane already active.</summary>
internal static class PaneActivation
{
    public static readonly DependencyProperty ActivatesOwnPaneProperty = DependencyProperty.RegisterAttached(
        "ActivatesOwnPane",
        typeof(bool),
        typeof(PaneActivation),
        new PropertyMetadata(false, OnActivatesOwnPaneChanged));

    public static bool GetActivatesOwnPane(DependencyObject element) => (bool)element.GetValue(ActivatesOwnPaneProperty);

    public static void SetActivatesOwnPane(DependencyObject element, bool value) => element.SetValue(ActivatesOwnPaneProperty, value);

    private static void OnActivatesOwnPaneChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not ButtonBase button)
        {
            return;
        }

        button.Click -= OnClick;
        if ((bool)e.NewValue)
        {
            button.Click += OnClick;
        }
    }

    private static void OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is ButtonBase button
            && (Window.GetWindow(button)?.DataContext as VaultLifecycleViewModel)?.Workspace is { } workspace
            && MainWindow.FindAncestorDataContext<WorkspaceGroupViewModel>(button) is { } group)
        {
            workspace.SelectGroupFromKeyboardFocus(group);
        }
    }
}
