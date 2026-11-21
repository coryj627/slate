// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;

namespace SlateWindows.Tests;

/// <summary>
/// #1866 (CD11): what the keys-first recorder names. It names a control turned off with the
/// keys on it or inside it, even one turned back on in the same dispatcher operation, and
/// never a control turned off once the keys have left it.
/// </summary>
public sealed class KeysFirstRecorderTests
{
    [Fact]
    public void AFocusedButtonTurnedOffIsNamed() => WithAWindow(rig =>
    {
        TakeTheKeys(rig.First);

        List<string> off = KeysFirstRecorder.DisabledWithTheKeysInside(rig.First, () => rig.First.IsEnabled = false);

        Assert.Equal([rig.First.ToString()], off);
    });

    [Fact]
    public void AButtonTurnedOffAndOnInOneOperationIsNamed() => WithAWindow(rig =>
    {
        TakeTheKeys(rig.First);

        List<string> off = KeysFirstRecorder.DisabledWithTheKeysInside(rig.First, () =>
        {
            rig.First.IsEnabled = false;
            rig.First.IsEnabled = true;
        });

        Assert.Equal([rig.First.ToString()], off);
        Assert.True(rig.First.IsEnabled);
    });

    [Fact]
    public void AButtonTurnedOffWithTheKeysElsewhereIsNotNamed() => WithAWindow(rig =>
    {
        TakeTheKeys(rig.Second);

        List<string> off = KeysFirstRecorder.DisabledWithTheKeysInside(rig.First, () => rig.First.IsEnabled = false);

        Assert.Empty(off);
        Assert.False(rig.First.IsEnabled);
    });

    [Fact]
    public void TheKeysMovedOffFirstThenTheButtonTurnedOffIsNotNamed() => WithAWindow(rig =>
    {
        TakeTheKeys(rig.First);

        List<string> off = KeysFirstRecorder.DisabledWithTheKeysInside(rig.First, () =>
        {
            Assert.True(rig.Second.Focus());
            rig.First.IsEnabled = false;
        });

        Assert.Empty(off);
        Assert.False(rig.First.IsEnabled);
    });

    [Fact]
    public void AnAncestorTurnedOffWithTheKeysInsideTheRootIsNamed() => WithAWindow(rig =>
    {
        TakeTheKeys(rig.First);

        List<string> off = KeysFirstRecorder.DisabledWithTheKeysInside(rig.First, () => rig.Panel.IsEnabled = false);

        Assert.Equal([rig.First.ToString()], off);
    });

    [Fact]
    public void AFocusedHyperlinkTurnedOffIsNamed() => WithAWindow(rig =>
    {
        Assert.True(rig.Link.Focus(), "premise: the link could not take the keys");
        Assert.Same(rig.Link, Keyboard.FocusedElement);

        Xunit.Sdk.XunitException failed = Assert.ThrowsAny<Xunit.Sdk.XunitException>(
            () => KeysFirstRecorder.AssertTheKeysLeaveFirst("the link", rig.Link, () => rig.Link.IsEnabled = false));

        Assert.Contains("the link went off with the keys on", failed.Message, StringComparison.Ordinal);
    });

    [Fact]
    public void TheAssertionNamesWhatHeldTheKeys() => WithAWindow(rig =>
    {
        TakeTheKeys(rig.First);

        Xunit.Sdk.XunitException failed = Assert.ThrowsAny<Xunit.Sdk.XunitException>(
            () => KeysFirstRecorder.AssertTheKeysLeaveFirst("First", rig.First, () => rig.First.IsEnabled = false));

        Assert.Contains($"First went off with the keys on {rig.First}", failed.Message, StringComparison.Ordinal);
    });

    [Fact]
    public void TheAssertionPassesWhenTheKeysLeaveFirst() => WithAWindow(rig =>
    {
        TakeTheKeys(rig.First);

        KeysFirstRecorder.AssertTheKeysLeaveFirst("First", rig.First, () =>
        {
            Assert.True(rig.Second.Focus());
            rig.First.IsEnabled = false;
        });

        Assert.Same(rig.Second, Keyboard.FocusedElement);
    });

    /// <summary>A fact whose act never turns its root off measured nothing about the keys
    /// leaving first, so it fails rather than passing.</summary>
    [Fact]
    public void TheAssertionFailsWhenTheRootNeverGoesOff() => WithAWindow(rig =>
    {
        TakeTheKeys(rig.First);

        Xunit.Sdk.XunitException failed = Assert.ThrowsAny<Xunit.Sdk.XunitException>(
            () => KeysFirstRecorder.AssertTheKeysLeaveFirst("First", rig.First, () => { }));

        Assert.Contains("First never went off", failed.Message, StringComparison.Ordinal);
    });

    [Fact]
    public void TheAssertionFailsOnARootTheActNeverTurnsOff() => WithAWindow(rig =>
    {
        TakeTheKeys(rig.Second);

        Xunit.Sdk.XunitException failed = Assert.ThrowsAny<Xunit.Sdk.XunitException>(
            () => KeysFirstRecorder.AssertTheKeysLeaveFirst("Second", rig.Second, () => rig.First.IsEnabled = false));

        Assert.Contains("Second never went off", failed.Message, StringComparison.Ordinal);
    });

    [Fact]
    public void TheContentAssertionFailsWhenTheLinkNeverGoesOff() => WithAWindow(rig =>
    {
        Assert.True(rig.Link.Focus(), "premise: the link could not take the keys");

        Xunit.Sdk.XunitException failed = Assert.ThrowsAny<Xunit.Sdk.XunitException>(
            () => KeysFirstRecorder.AssertTheKeysLeaveFirst("the link", rig.Link, () => { }));

        Assert.Contains("the link never went off", failed.Message, StringComparison.Ordinal);
    });

    /// <summary>A root that does not hold the keys when the act starts can never go off
    /// holding them, so a fact on it measures nothing and fails (Codex round 1, finding 1).</summary>
    [Fact]
    public void TheAssertionFailsWhenNothingHoldsTheKeys() => WithAWindow(rig =>
    {
        Keyboard.ClearFocus();
        Assert.Null(Keyboard.FocusedElement);

        Xunit.Sdk.XunitException failed = Assert.ThrowsAny<Xunit.Sdk.XunitException>(
            () => KeysFirstRecorder.AssertTheKeysLeaveFirst("First", rig.First, () => rig.First.IsEnabled = false));

        Assert.Contains("First did not hold the keys", failed.Message, StringComparison.Ordinal);
    });

    [Fact]
    public void TheAssertionFailsWhenTheKeysAreElsewhere() => WithAWindow(rig =>
    {
        TakeTheKeys(rig.Second);

        Xunit.Sdk.XunitException failed = Assert.ThrowsAny<Xunit.Sdk.XunitException>(
            () => KeysFirstRecorder.AssertTheKeysLeaveFirst("First", rig.First, () => rig.First.IsEnabled = false));

        Assert.Contains("First did not hold the keys", failed.Message, StringComparison.Ordinal);
    });

    [Fact]
    public void TheContentAssertionFailsWhenTheKeysAreElsewhere() => WithAWindow(rig =>
    {
        TakeTheKeys(rig.First);

        Xunit.Sdk.XunitException failed = Assert.ThrowsAny<Xunit.Sdk.XunitException>(
            () => KeysFirstRecorder.AssertTheKeysLeaveFirst("the link", rig.Link, () => rig.Link.IsEnabled = false));

        Assert.Contains("the link did not hold the keys", failed.Message, StringComparison.Ordinal);
    });

    /// <summary>A popup's content lives in its own visual tree, so the keys on a button in a
    /// popup that the root holds are inside the root by the logical tree only (a menu's
    /// submenu rows are).</summary>
    [Fact]
    public void KeysInAPopupTheRootHoldsAreInside() => WithAWindow(rig =>
    {
        var inPopup = new Button { Content = "In the popup" };
        var popup = new Popup { Child = inPopup, PlacementTarget = rig.First, StaysOpen = true };
        rig.Panel.Children.Add(popup);
        popup.IsOpen = true;
        PumpedDispatcher.Drain();
        TakeTheKeys(inPopup);
        Assert.False(rig.Panel.IsAncestorOf(inPopup), "premise: the popup's content is outside the panel's visual tree");

        Assert.True(KeysFirstRecorder.TheKeysAreIn(rig.Panel), $"the keys on {inPopup} were not counted inside the panel that holds its popup");
        List<string> off = KeysFirstRecorder.DisabledWithTheKeysInside(rig.Panel, () => rig.Panel.IsEnabled = false);

        popup.IsOpen = false;
        Assert.Equal([inPopup.ToString()], off);
    });

    private sealed record Rig(StackPanel Panel, Button First, Button Second, Hyperlink Link);

    private static void TakeTheKeys(UIElement element) => Assert.True(
        element.Focus() && ReferenceEquals(element, Keyboard.FocusedElement),
        $"keyboard premise: {element} could not take the keys (they are on {Keyboard.FocusedElement})");

    private static void WithAWindow(Action<Rig> body) => StaThread.Run(
        () =>
        {
            var first = new Button { Content = "First" };
            var second = new Button { Content = "Second" };
            var link = new Hyperlink(new Run("Link"));
            var inner = new StackPanel();
            inner.Children.Add(first);
            var outer = new StackPanel();
            outer.Children.Add(inner);
            outer.Children.Add(second);
            outer.Children.Add(new TextBlock(link));
            var window = new Window
            {
                Content = outer,
                Width = 320,
                Height = 240,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -20_000,
                Top = -20_000,
            };
            try
            {
                window.Show();
                window.Activate();
                window.UpdateLayout();
                PumpedDispatcher.Drain();
                body(new Rig(inner, first, second, link));
            }
            finally
            {
                window.Close();
            }
        },
        TimeSpan.FromSeconds(60),
        "the recorder's window timed out.");
}
