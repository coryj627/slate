// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace SlateWindows;

/// <summary>
/// W4-7 (#739): the restore confirmation — contract H7 pins the
/// button labels "Cancel" / "Restore" (the mac alert's roles), which
/// MessageBox cannot supply. A minimal owner-centered modal: message,
/// Cancel (IsCancel, initial focus — the safe default for a
/// destructive confirmation), and the confirm verb.
/// </summary>
internal static class HistoryConfirmationDialog
{
    internal static bool Confirm(string title, string message, string confirmLabel)
    {
        bool confirmed = false;
        var dialog = new Window
        {
            Title = title,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            SizeToContent = SizeToContent.WidthAndHeight,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            MaxWidth = 460,
        };
        if (Application.Current?.MainWindow is { IsVisible: true } owner)
        {
            dialog.Owner = owner;
        }
        AutomationProperties.SetName(dialog, title);
        AutomationProperties.SetHelpText(dialog, message);
        var body = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12),
        };
        var cancel = new Button
        {
            Content = "Cancel",
            IsCancel = true,
            MinWidth = 80,
            Margin = new Thickness(0, 0, 8, 0),
            Padding = new Thickness(10, 2, 10, 2),
        };
        var confirm = new Button
        {
            Content = confirmLabel,
            MinWidth = 80,
            Padding = new Thickness(10, 2, 10, 2),
        };
        confirm.Click += (_, _) =>
        {
            confirmed = true;
            dialog.DialogResult = true;
        };
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        buttons.Children.Add(cancel);
        buttons.Children.Add(confirm);
        var layout = new StackPanel { Margin = new Thickness(16) };
        layout.Children.Add(body);
        layout.Children.Add(buttons);
        dialog.Content = layout;
        dialog.Loaded += (_, _) => _ = cancel.Focus();
        _ = dialog.ShowDialog();
        return confirmed;
    }
}
