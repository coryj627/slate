// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Controls;

namespace SlateWindows;

/// <summary>
/// #1779: Fluent's menu item hard-codes its shortcut text to the disabled brush at
/// 11 points, so an available shortcut reads as unavailable and doesn't scale with
/// its row. That brush is every other control's disabled text too, so this changes
/// the template part rather than the key. The style that runs it is based on
/// Fluent's and built in code, because a dictionary can't reach a sibling's style.
/// </summary>
internal static class MenuShortcuts
{
    internal static Style StyleFor(Style fluent)
    {
        var style = new Style(typeof(MenuItem), fluent);
        style.Setters.Add(new EventSetter(FrameworkElement.LoadedEvent, new RoutedEventHandler(OnLoaded)));
        return style;
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem item)
        {
            return;
        }

        item.IsEnabledChanged -= OnIsEnabledChanged;
        item.IsEnabledChanged += OnIsEnabledChanged;
        Update(item);
    }

    private static void OnIsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e) =>
        Update((MenuItem)sender);

    private static void Update(MenuItem item)
    {
        item.ApplyTemplate();
        if (item.Template?.FindName("InputGestureText", item) is not TextBlock text)
        {
            return;
        }

        text.FontSize = item.FontSize;
        text.VerticalAlignment = VerticalAlignment.Center;
        text.SetResourceReference(
            TextBlock.ForegroundProperty,
            item.IsEnabled ? "Slate.SecondaryTextBrush" : "TextFillColorDisabledBrush");
    }
}
