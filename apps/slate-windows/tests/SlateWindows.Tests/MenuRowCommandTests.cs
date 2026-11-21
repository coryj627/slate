// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace SlateWindows.Tests;

/// <summary>
/// #1866 (CD11): the proof behind the focus manifest's menu-row exclusion. A menu row's
/// command runs only after its menu has closed and given the keys back, whether the row is
/// pressed with Enter or invoked through its peer, so a command that turns itself off as it
/// runs never takes a row holding the keys with it. The census therefore keys no menu row
/// as a command site, unless it stays open on click. A row that stays open is the control:
/// its command runs with the keys still on it, which is why such a row stays a site.
/// A desktop that refuses a menu the keys (the hosted runners, #1680) measures nothing
/// here, and the fact says so instead of passing.
/// </summary>
public sealed class MenuRowCommandTests
{
    [Theory]
    [InlineData("Slate", "Enter", false)]
    [InlineData("Slate", "Invoke", false)]
    [InlineData("Slate", "Enter", true)]
    [InlineData("Slate", "Invoke", true)]
    [InlineData("More", "Enter", false)]
    [InlineData("More", "Invoke", false)]
    [InlineData("More", "Enter", true)]
    [InlineData("More", "Invoke", true)]
    public void AMenuRowLetsTheKeysGoBeforeItsCommandRuns(string menu, string how, bool staysOpenOnClick) => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "# A\n"));
        host.Workspace.OpenPath("a.md");
        host.Settle();
        host.Shell.Activate();
        host.Settle();

        (MenuItem row, FrameworkElement owner, Func<bool> isOpen) = menu == "Slate" ? SlateMenuRow(host) : MoreMenuRow(host);
        KeysFirstRecorder.TakeTheMenuKeys(row);
        host.Settle();
        Assert.True(isOpen(), $"premise: the {menu} menu is open with the keys on its row");
        row.StaysOpenOnClick = staysOpenOnClick;

        (bool ran, string holder, bool keysInTheMenu, bool open) = RunTheRow(host, row, how, owner, isOpen);

        Assert.True(ran, $"the {menu} menu's row never ran its command");
        if (staysOpenOnClick)
        {
            Assert.True(keysInTheMenu, $"a row that stays open ran its command with the keys on {holder}, not on the row");
            Assert.True(open, "a row that stays open ran its command with its menu shut");
        }
        else
        {
            Assert.False(keysInTheMenu, $"the {menu} menu's row ran its command with the keys on {holder}, still in the menu");
            Assert.False(open, $"the {menu} menu's row ran its command with the menu still open");
        }
    });

    /// <summary>The Slate menu opened, its first submenu (File) opened, and that
    /// submenu's first row that runs a command (no submenu, enabled, on screen).</summary>
    private static (MenuItem Row, FrameworkElement Owner, Func<bool> IsOpen) SlateMenuRow(ShownShell host)
    {
        Menu menu = host.Shell.MainMenu;
        MenuItem top = menu.Items.OfType<MenuItem>().First();
        top.IsSubmenuOpen = true;
        host.Settle();
        MenuItem submenu = top.Items.OfType<MenuItem>().FirstOrDefault(candidate => candidate.IsVisible && candidate.Items.Count > 0)
            ?? KeysFirstRecorder.SkipWhereTheDesktopRefusesTheMenu("no submenu of the open Slate menu is on screen");
        submenu.IsSubmenuOpen = true;
        host.Settle();
        MenuItem row = submenu.Items.OfType<MenuItem>()
            .FirstOrDefault(candidate => candidate.IsEnabled && candidate.IsVisible && candidate.Items.Count == 0 && candidate.Command is not null)
            ?? KeysFirstRecorder.SkipWhereTheDesktopRefusesTheMenu($"no row of the open {submenu.Header} menu is on screen");
        return (row, menu, () => top.IsSubmenuOpen || submenu.IsSubmenuOpen);
    }

    /// <summary>⋯ opened as a reader opens it, through its peer, and its first row.</summary>
    private static (MenuItem Row, FrameworkElement Owner, Func<bool> IsOpen) MoreMenuRow(ShownShell host)
    {
        Button more = ShownShell.Descendants(host.Shell).OfType<Button>()
            .Single(button => button.IsVisible && AutomationProperties.GetAutomationId(button) == "NoteHeaderMore");
        ContextMenu menu = more.ContextMenu!;
        Assert.True(more.Focus(), "premise: ⋯ could not take the keys");
        ((IInvokeProvider)UIElementAutomationPeer.CreatePeerForElement(more).GetPattern(PatternInterface.Invoke)).Invoke();
        host.Settle();
        host.Settle();
        MenuItem row = menu.Items.OfType<MenuItem>()
            .FirstOrDefault(candidate => candidate.IsEnabled && candidate.IsVisible && candidate.Command is not null)
            ?? KeysFirstRecorder.SkipWhereTheDesktopRefusesTheMenu("no row of the open ⋯ menu is on screen");
        return (row, menu, () => menu.IsOpen);
    }

    /// <summary>Puts a recording command on <paramref name="row"/>, activates the row, and
    /// returns what held the keys when the command ran: whether they were on the row or on
    /// any row of its menu (by the focused element's logical parents, since a submenu's
    /// rows live in their popup's visual tree), and whether the menu was open. The row's
    /// own command is put back after.</summary>
    private static (bool Ran, string Holder, bool KeysInTheMenu, bool Open) RunTheRow(
        ShownShell host, MenuItem row, string how, FrameworkElement owner, Func<bool> isOpen)
    {
        (bool Ran, string Holder, bool KeysInTheMenu, bool Open) seen = (false, "nothing", false, false);
        BindingBase? binding = BindingOperations.GetBindingBase(row, MenuItem.CommandProperty);
        ICommand? command = row.Command;
        row.Command = new Recording(() => seen = (
            true,
            Keyboard.FocusedElement?.ToString() ?? "nothing",
            Keyboard.FocusedElement is DependencyObject keys && InTheMenu(keys, owner),
            isOpen()));
        try
        {
            if (how == "Enter")
            {
                InputManager.Current.ProcessInput(
                    new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(row)!, Environment.TickCount, Key.Enter)
                    {
                        RoutedEvent = Keyboard.PreviewKeyDownEvent,
                    });
            }
            else
            {
                ((IInvokeProvider)UIElementAutomationPeer.CreatePeerForElement(row).GetPattern(PatternInterface.Invoke)).Invoke();
            }

            host.Settle();
            host.Settle();
        }
        finally
        {
            if (binding is not null)
            {
                _ = row.SetBinding(MenuItem.CommandProperty, binding);
            }
            else
            {
                row.Command = command;
            }
        }

        return seen;
    }

    /// <summary>Whether <paramref name="keys"/> is a row of <paramref name="owner"/>'s menu,
    /// at any depth: a menu item whose logical parents reach the menu.</summary>
    private static bool InTheMenu(DependencyObject keys, FrameworkElement owner)
    {
        if (keys is not MenuItem)
        {
            return false;
        }

        for (DependencyObject? at = keys; at is not null; at = LogicalTreeHelper.GetParent(at))
        {
            if (ReferenceEquals(at, owner))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>A command that records the moment it runs.</summary>
    private sealed class Recording(Action ran) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => ran();
    }

    private static void RunSta(Action body) =>
        StaThread.Run(body, TimeSpan.FromSeconds(120), "STA test body timed out.");
}
