// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace SlateWindows;

/// <summary>
/// W7-7 PR 4 (#1247, contract R-5): a radio group whose arrows move the
/// CHOICE, the Windows convention.
/// </summary>
/// <remarks>
/// <para>
/// WPF's <c>RadioButton</c> leaves the arrow keys to directional
/// navigation, which moves keyboard focus and nothing else: Right on the
/// canvas's checked "Outline" landed on "Table" while Outline stayed
/// checked and the outline stayed on screen, so a screen reader announced
/// a choice the view had not made (NVDA pass F4). A Win32 auto radio
/// button is checked by the focus an arrow gives it; this gives the WPF
/// groups the same.
/// </para>
/// <para>
/// <b>The group owns its unmodified arrows.</b> On the panel's bubbling
/// <c>KeyDown</c> — after the focused radio has had its turn — Down and
/// Right move to the next enabled radio in the panel and Up and Left to
/// the previous one, wrapping. That is the dialog manager's GROUP order,
/// not geometry: the Tasks Review filters wrap onto a second row in a
/// narrow pane, where a geometric Down from "All" reaches "This week" or
/// nothing. The radio that takes focus is then checked through
/// <c>SetCurrentValue</c>, exactly as <c>RadioButton.OnToggle</c> checks
/// it, so a TwoWay binding (the review's filter chips) carries the choice
/// to its source and survives.
/// </para>
/// <para>
/// A modified arrow is never the group's: Ctrl+Alt+Arrow is the window's
/// pane chord, and handling it here would swallow it before the window's
/// <c>KeyBinding</c> saw it. Each owner also sets the panel's
/// <c>KeyboardNavigation.DirectionalNavigation</c> to <c>Cycle</c>, so an
/// arrow this does not take still cannot walk out of the group.
/// </para>
/// </remarks>
internal static class RadioGroupArrows
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled", typeof(bool), typeof(RadioGroupArrows),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject element) =>
        (bool)element.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject element, bool value) =>
        element.SetValue(IsEnabledProperty, value);

    /// <summary>
    /// The radio an arrow moves to from <paramref name="from"/>, or null
    /// when the key is not the group's.
    /// </summary>
    /// <remarks>
    /// <paramref name="from"/> itself is the answer when it is the only
    /// radio that can take focus: the arrow is still the group's, and
    /// does nothing, as in a Win32 group of one.
    /// </remarks>
    internal static RadioButton? Target(Panel panel, RadioButton from, Key key, ModifierKeys modifiers)
    {
        if (modifiers != ModifierKeys.None || !panel.Children.Contains(from))
        {
            return null;
        }

        bool mirrored = panel.FlowDirection == FlowDirection.RightToLeft;
        int step = key switch
        {
            Key.Down => 1,
            Key.Up => -1,
            Key.Right => mirrored ? -1 : 1,
            Key.Left => mirrored ? 1 : -1,
            _ => 0,
        };
        if (step == 0)
        {
            return null;
        }

        List<RadioButton> radios = panel.Children.OfType<RadioButton>()
            .Where(radio => ReferenceEquals(radio, from)
                || radio is { IsEnabled: true, IsVisible: true, Focusable: true })
            .ToList();
        int index = radios.IndexOf(from);
        return radios[(index + step + radios.Count) % radios.Count];
    }

    private static void OnIsEnabledChanged(DependencyObject element, DependencyPropertyChangedEventArgs change)
    {
        if (element is not Panel panel)
        {
            throw new InvalidOperationException(
                $"{nameof(RadioGroupArrows)} belongs on the Panel that holds the radios, not on {element.GetType().Name}.");
        }

        panel.KeyDown -= Panel_KeyDown;
        if ((bool)change.NewValue)
        {
            panel.KeyDown += Panel_KeyDown;
        }
    }

    private static void Panel_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Handled
            || sender is not Panel panel
            || e.OriginalSource is not RadioButton from
            || Target(panel, from, e.Key, e.KeyboardDevice.Modifiers) is not { } to)
        {
            return;
        }

        e.Handled = true;
        if (!ReferenceEquals(to, from) && to.Focus())
        {
            to.SetCurrentValue(ToggleButton.IsCheckedProperty, true);
        }
    }
}
