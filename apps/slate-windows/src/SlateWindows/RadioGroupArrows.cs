// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

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
/// nothing.
/// </para>
/// <para>
/// <b>One arrow, one utterance</b> (codex PR 4 round 6 high 1; the owner's
/// decision). The radio an arrow reaches is CHECKED FIRST — through
/// <c>SetCurrentValue</c>, exactly as <c>RadioButton.OnToggle</c> checks
/// it, so a TwoWay binding (the review's filter chips) carries the choice
/// to its source and survives — and only then takes focus, so the focus
/// speech already says "checked" and names the choice made. The owners'
/// authored line for the choice (the review's "Filter set", the canvas's
/// surface line, the graph's mode line) would repeat it: they read
/// <see cref="IsCommittingByArrow"/> and stay silent on this route only; a
/// click, Space and a command still speak. Focus stays on the group (the
/// graph's M4 hand-off to the projection is a click's, not an arrow's).
/// </para>
/// <para>
/// <b>The group's stop is its checked radio.</b> Keys that enter the group
/// from outside onto an unchecked radio — Tab, whose <c>Once</c> group
/// remembers the last radio FOCUSED, not the one a command or a click
/// elsewhere checked; a region's first-stop landing; a restore — go on to
/// the checked radio in the same focus change (<see cref="CheckedPeer"/>;
/// <c>SelectorFocus.LandOnStop</c> lands a radio there directly and answers
/// that it landed): Ctrl+R put the reader on
/// "All" while "Overdue" was checked, and the next arrow committed a
/// filter one step from the wrong place (the completeness sweep's G15). A
/// check made while the group holds the keys brings them to the checked
/// radio — the click whose press the entry redirected, Space, a command
/// run with the keys on the group — so they never rest on an unchecked
/// radio.
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
    /// <summary>An arrow's check is in progress on this thread.</summary>
    [ThreadStatic]
    private static bool _committingByArrow;

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

    /// <summary>
    /// Whether this key, from this source, is one a radio group owns — the
    /// question a surface that tunnels its own chords asks before it takes
    /// an arrow.
    /// </summary>
    /// <remarks>
    /// A tunnelling handler runs before the group's bubbling one: the
    /// canvas surface's navigator takes every unmodified arrow while a Move
    /// or Resize mode is active, so Right on the switcher's checked
    /// "Outline" stepped the moving cards instead of choosing the Table
    /// projection (codex round 1 on W7-7 PR 4).
    /// </remarks>
    internal static bool OwnsKey(object? source, Key key, ModifierKeys modifiers) =>
        source is RadioButton { Parent: Panel panel } radio
        && GetIsEnabled(panel)
        && Target(panel, radio, key, modifiers) is not null;

    /// <summary>Whether the check being made is an arrow's — the route on
    /// which the choice's own announcement stays silent, the focus speech
    /// saying it.</summary>
    internal static bool IsCommittingByArrow => _committingByArrow;

    /// <summary>
    /// The checked radio of <paramref name="radio"/>'s group, when
    /// <paramref name="radio"/> is NOT it and the checked one can take the
    /// keys: a landing on the group lands there. Null when the radio is the
    /// checked one, the group has none checked, or it is not an arrow group.
    /// </summary>
    internal static RadioButton? CheckedPeer(RadioButton radio) =>
        radio is { IsChecked: not true, Parent: Panel panel } && GetIsEnabled(panel)
            ? panel.Children.OfType<RadioButton>().FirstOrDefault(peer =>
                peer is { IsChecked: true, IsEnabled: true, IsVisible: true, Focusable: true })
            : null;

    private static void OnIsEnabledChanged(DependencyObject element, DependencyPropertyChangedEventArgs change)
    {
        if (element is not Panel panel)
        {
            throw new InvalidOperationException(
                $"{nameof(RadioGroupArrows)} belongs on the Panel that holds the radios, not on {element.GetType().Name}.");
        }

        panel.KeyDown -= Panel_KeyDown;
        panel.PreviewGotKeyboardFocus -= Panel_PreviewGotKeyboardFocus;
        panel.RemoveHandler(ToggleButton.CheckedEvent, (RoutedEventHandler)Panel_Checked);
        if ((bool)change.NewValue)
        {
            panel.KeyDown += Panel_KeyDown;
            panel.PreviewGotKeyboardFocus += Panel_PreviewGotKeyboardFocus;
            panel.AddHandler(ToggleButton.CheckedEvent, (RoutedEventHandler)Panel_Checked);
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
        if (ReferenceEquals(to, from))
        {
            return;
        }

        // Checked BEFORE it takes focus, and silently: the focus speech is
        // the one utterance. Target() offers only a radio that can take the
        // keys.
        bool outer = _committingByArrow;
        _committingByArrow = true;
        try
        {
            to.SetCurrentValue(ToggleButton.IsCheckedProperty, true);
        }
        finally
        {
            _committingByArrow = outer;
        }

        _ = to.Focus();
    }

    /// <summary>Keys entering the group from outside onto an unchecked
    /// radio go on to the checked one, in the same focus change.</summary>
    private static void Panel_PreviewGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (e.Handled
            || sender is not Panel panel
            || e.NewFocus is not RadioButton radio
            || !panel.Children.Contains(radio)
            || (e.OldFocus is Visual old && panel.IsAncestorOf(old))
            || CheckedPeer(radio) is not { } chosen)
        {
            return;
        }

        e.Handled = chosen.Focus();
    }

    /// <summary>A check made while the group holds the keys brings them to
    /// the checked radio.</summary>
    private static void Panel_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is Panel { IsKeyboardFocusWithin: true } panel
            && e.OriginalSource is RadioButton { IsKeyboardFocused: false } radio
            && panel.Children.Contains(radio))
        {
            _ = radio.Focus();
        }
    }
}
