// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;

namespace SlateWindows;

/// <summary>
/// W7-7 PR 4b (#1247; the owner's decision on PR 4b's last codex check): a
/// date property's calendar commits once, when it closes. The row's
/// <see cref="DatePicker"/> binds its <c>SelectedDate</c> with
/// <c>UpdateSourceTrigger=Explicit</c>, and this pushes it — the row's one
/// commit — when the calendar closes: Enter or Space on a day, a click on a
/// day, Alt+Down, or a click away. Each arrow in the open calendar sets
/// <c>SelectedDate</c> (WPF's Calendar selects the day it moves to); pushed
/// at once, each wrote the note and republished every row, which unloaded
/// the picker and closed its calendar after one step.
/// </summary>
/// <remarks>
/// Escape reverts. WPF's DatePicker puts back the day the calendar opened on
/// AFTER it closes the popup, inside the same key press, so the close's push
/// waits for the input it came from (Input priority) and then finds the
/// original day: nothing is written. A close caused by the picker leaving
/// the tree (its tab closed, its row republished) commits nothing.
/// </remarks>
internal static class CalendarCommit
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled", typeof(bool), typeof(CalendarCommit),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject element) =>
        (bool)element.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject element, bool value) =>
        element.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not DatePicker picker)
        {
            return;
        }

        picker.CalendarClosed -= Closed;
        picker.CalendarOpened -= Opened;
        if (e.NewValue is true)
        {
            picker.CalendarClosed += Closed;
            picker.CalendarOpened += Opened;
        }
    }

    /// <summary>
    /// The calendar opens with the keys on the row's day, but WPF's Calendar
    /// moves its arrows from its own current date, which a fresh calendar
    /// takes from today (its display date), not from the day it selects: the
    /// first Right from 10 September spoke 29 September. Selecting the row's
    /// day again makes it the calendar's current date (Calendar does so for
    /// a single-date selection), so the arrows move from the day the keys
    /// are on. <see cref="DependencyObject.SetCurrentValue"/> keeps the
    /// picker's own binding of the calendar's selection.
    /// </summary>
    private static void Opened(object? sender, RoutedEventArgs e)
    {
        if (sender is DatePicker { SelectedDate: { } day } picker
            && picker.Template?.FindName("PART_Popup", picker) is System.Windows.Controls.Primitives.Popup { Child: Calendar calendar })
        {
            calendar.SetCurrentValue(Calendar.SelectedDateProperty, null);
            calendar.SetCurrentValue(Calendar.SelectedDateProperty, day);
        }
    }

    private static void Closed(object? sender, RoutedEventArgs e)
    {
        if (sender is DatePicker picker)
        {
            _ = picker.Dispatcher.BeginInvoke(DispatcherPriority.Input, () => Commit(picker));
        }
    }

    /// <summary>The picker's day, pushed to its row — unless the picker has
    /// left the tree, or its calendar opened again meanwhile.</summary>
    private static void Commit(DatePicker picker)
    {
        if (!picker.IsDropDownOpen && PresentationSource.FromVisual(picker) is not null)
        {
            BindingOperations.GetBindingExpression(picker, DatePicker.SelectedDateProperty)?.UpdateSource();
        }
    }
}
