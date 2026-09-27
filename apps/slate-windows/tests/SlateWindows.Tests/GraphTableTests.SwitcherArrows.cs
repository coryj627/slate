// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SlateWindows.Graph;

namespace SlateWindows.Tests;

/// <summary>W7-7 PR 4 (#1247; codex PR 4 round 6 high 1, the owner's
/// decision): the graph view switcher's arrow commit is one utterance, and
/// the keys stay on the switcher.</summary>
public sealed partial class GraphTableTests
{
    /// <summary>An arrow on the graph's view switcher commits the mode it
    /// reaches as ONE spoken outcome: the destination radio is already
    /// checked when it takes focus, no mode line is posted on top of the
    /// focus speech, and the keys stay on the switcher — Term M4's hand-off
    /// to the projection is a click's, Space's and a command's, not an
    /// arrow's, so the next arrow moves on through the group.</summary>
    [Fact]
    public void AnArrowOnTheSwitcherCommitsAsOneUtteranceAndStaysOnIt()
    {
        RunSta(() =>
        {
            using var host = new Host(4, "graph-switcher-arrow");
            GraphDocumentViewModel document = host.Open();
            GraphSurfaceView view = SurfaceFor(host, document);
            using HostedWindow window = HostInWindow(view);
            RadioButton[] choices = [.. view.SwitcherForTests.Children.OfType<RadioButton>()];
            RadioButton from = choices.Single(choice => choice.IsChecked == true);
            RadioButton to = choices[(Array.IndexOf(choices, from) + 1) % choices.Length];
            Assert.True(from.Focus());
            bool? checkedWhenFocused = null;
            to.GotKeyboardFocus += (_, _) => checkedWhenFocused ??= to.IsChecked;
            host.GraphLines.Clear();

            InputManager.Current.ProcessInput(new KeyEventArgs(
                Keyboard.PrimaryDevice, PresentationSource.FromVisual(window.Window)!, Environment.TickCount, Key.Right)
            {
                RoutedEvent = Keyboard.PreviewKeyDownEvent,
            });
            PumpedDispatcher.Drain();
            window.UpdateLayout();
            PumpedDispatcher.Drain();

            Assert.True(to.IsChecked, "the arrow did not check its radio.");
            Assert.True(checkedWhenFocused == true, "the destination took focus UNCHECKED");
            Assert.True(
                host.GraphLines.Count == 0,
                $"the arrow posted authored line(s): {string.Join(" | ", host.GraphLines)}");
            Assert.Same(to, Keyboard.FocusedElement);
        });
    }
}
