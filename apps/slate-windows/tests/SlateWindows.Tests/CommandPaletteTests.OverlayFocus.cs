// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Xunit;

namespace SlateWindows.Tests;

/// <summary>#1690: an overlay disables the view under it, but only once the keys have left
/// that view. Disabled with the keys still inside, the focused control went unavailable, and
/// NVDA said "unavailable" before the overlay's field.</summary>
public sealed partial class CommandPaletteTests
{
    [Theory]
    [InlineData("QuickOpen")]
    [InlineData("Search")]
    [InlineData("Palette")]
    public void TheWorkspaceIsDisabledOnlyAfterTheKeysLeaveIt(string overlay) => RunSta(() =>
    {
        using var host = new PaletteShellHost();
        _ = host.AttachDirtyWorkspace();
        host.Settle();
        FrameworkElement root = host.Shell.WorkspaceRoot;
        UIElement editor = ShownShell.Descendants(root).OfType<ICSharpCode.AvalonEdit.Editing.TextArea>().First(area => area.IsVisible);
        Assert.True(
            editor.Focus() && ReferenceEquals(editor, Keyboard.FocusedElement),
            $"keyboard premise: the editor could not take the keys (they are on {Keyboard.FocusedElement})");
        host.Settle();
        Assert.True(root.IsEnabled, "premise: the workspace is enabled with no overlay open");

        TextBox? field = null;
        List<string> disabledUnder = DisabledWithTheKeysInside(root, () =>
        {
            field = OpenTheOverlay(host, overlay);
            host.Settle();
        });

        Assert.True(disabledUnder.Count == 0, $"the workspace was disabled with the keys on {string.Join(", ", disabledUnder)}");
        host.AssertFocusIsOn(field!);
        Assert.False(root.IsEnabled, "the workspace stayed enabled under the open overlay");
    });

    /// <summary>The welcome view does the same under the palette, which opens over it for
    /// Switch Theme.</summary>
    [Fact]
    public void TheWelcomeViewIsDisabledOnlyAfterTheKeysLeaveIt() => RunSta(() =>
    {
        using var host = new ShownShell();
        host.ShowWelcomeWithoutAWorkspace();
        FrameworkElement root = host.Shell.WelcomeRoot;
        host.Shell.Activate();
        host.Settle();
        Assert.True(
            host.Shell.OpenVaultButton.Focus() && ReferenceEquals(host.Shell.OpenVaultButton, Keyboard.FocusedElement),
            $"keyboard premise: Open Vault could not take the keys (they are on {Keyboard.FocusedElement})");
        host.Settle();

        List<string> disabledUnder = DisabledWithTheKeysInside(root, () =>
        {
            host.Lifecycle.SwitchThemeCommand.Execute(null);
            Assert.True(
                PumpedDispatcher.PumpUntil(() => host.Lifecycle.Palette.Rows.Count == 3, TimeSpan.FromSeconds(10)),
                "the themes never published");
            host.Settle();
        });

        Assert.True(disabledUnder.Count == 0, $"the welcome view was disabled with the keys on {string.Join(", ", disabledUnder)}");
        Assert.Same(host.Shell.CommandPaletteSearchTextBox, Keyboard.FocusedElement);
        Assert.False(root.IsEnabled, "the welcome view stayed enabled under the open palette");
    });

    /// <summary>Runs <paramref name="open"/> and returns the element that held the keys each
    /// time <paramref name="root"/> was disabled with the keys still inside it.</summary>
    private static List<string> DisabledWithTheKeysInside(FrameworkElement root, Action open)
    {
        var disabledUnder = new List<string>();
        DependencyPropertyChangedEventHandler record = (_, e) =>
        {
            if (e.NewValue is false && root.IsKeyboardFocusWithin)
            {
                disabledUnder.Add(Keyboard.FocusedElement?.ToString() ?? "nothing");
            }
        };
        root.IsEnabledChanged += record;
        try
        {
            open();
        }
        finally
        {
            root.IsEnabledChanged -= record;
        }

        return disabledUnder;
    }

    private static TextBox OpenTheOverlay(PaletteShellHost host, string overlay)
    {
        switch (overlay)
        {
            case "QuickOpen":
                host.Lifecycle.QuickSwitcher!.Open();
                return host.Shell.QuickSwitcherSearchTextBox;
            case "Search":
                host.Lifecycle.Search.Open();
                return host.Shell.SearchOverlaySearchTextBox;
            default:
                host.Palette.Open();
                return host.Shell.CommandPaletteSearchTextBox;
        }
    }
}
